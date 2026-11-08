#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Makes a <see cref="CraftStation"/> (forge, smeltery, tannery, leatherworks) part of a network session, with the
    /// server deciding everything. Put it on the station's root next to a NetworkObject (the station's inspector has a
    /// Make Networked button; the Create Smithy... menus add both when Fish-Net is in the project).
    /// <list type="bullet">
    /// <item><b>The server runs the workshop</b>: it takes the materials from the player's inventory, gives back the
    /// pieces and the finished weapons and armour (whose numbers it evaluated), and keeps the player's journal. A client
    /// never adds or removes items and cannot teach itself anything.</item>
    /// <item><b>Every client keeps a copy</b> that follows the server's: the workshop is deterministic, so the server
    /// sends only the operations players make (numbered) and its clock; each copy simulates the heat, the fires and the
    /// tanning itself, so the glow, the sparks and the steam show the same everywhere. A client that joins (or falls out
    /// of step: a checksum now and then tells) gets the workshop whole.</item>
    /// <item><b>Working it</b> is an interaction (relayed by the player's <see cref="NetInteractor"/>): the server lets
    /// one player at a time work the station, opens the window on their screen and sends their journal, and the lines
    /// they learn (a new rank, an insight) as they learn them. What they do comes back as requests, checked against the
    /// player working there and the range before the station runs them like a local player's - bounded, as everyone's
    /// are, by the player's <see cref="CraftRequestGuard"/> (no faster than real hands, never steadier than the player's
    /// practice).</item>
    /// <item><b>Every request is answered</b> (done or not, and why - also a request refused before it reached the station,
    /// and work granted only in part), right after the packet that carries its operations: the client raises the answer
    /// (<see cref="CraftStation.Answered"/>) once its copy shows them, so its window reads the result.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CraftStation))]
    public class NetCraftStation : NetworkBehaviour, ICraftStationNet
    {
        /// <summary>Longest message sent to a client.</summary>
        public const int MaxMessageLength = 200;
        /// <summary>Longest answer or learnt line sent to a client (they are sent whole).</summary>
        public const int MaxLineLength = 600;
        /// <summary>Seconds before the same refusal of the 3D work is told again (a different one is told at once).</summary>
        public const float RepeatInterval = CraftStation.RepeatInterval;
        private const byte Packet = 0, Snapshot = 1, Journal = 2;

        [Tooltip("Seconds between two clock updates while someone works here or a fire burns.")]
        [SerializeField, Range(0.05f, 1f)] private float busyInterval = 0.1f;
        [Tooltip("Seconds between two clock updates otherwise (the copies simulate in between).")]
        [SerializeField, Range(0.1f, 5f)] private float idleInterval = 0.5f;
        [Tooltip("A checksum goes with every Nth clock update; a copy that differs asks for the workshop again.")]
        [SerializeField, Range(1, 100)] private int checksumEvery = 10;
        [Tooltip("Metres a player may be beyond the station's reach (server positions) and still work it.")]
        [SerializeField, Range(0f, 5f)] private float rangeTolerance = 1.5f;
        [Tooltip("Least seconds between two journal updates sent to the player working here (it is sent only when it changed).")]
        [SerializeField, Range(0.2f, 10f)] private float journalInterval = 1f;

        private CraftStation station;

        // Server
        private readonly CraftWorkshopFeed feed = new CraftWorkshopFeed();
        private readonly StationBeat beat = new StationBeat();
        private bool replaced;
        private int sentMeta = int.MinValue;
        private int transfer;
        private readonly Dictionary<int, float> nextSnapshot = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextTend = new Dictionary<int, float>();
        private readonly Dictionary<int, (string text, float at)> lastTold = new Dictionary<int, (string, float)>();
        private readonly List<(NetworkConnection conn, CraftAnswer answer)> answersOut = new List<(NetworkConnection, CraftAnswer)>();
        private readonly List<string> learntOut = new List<string>();
        private CraftKnowledge watchedJournal;
        private NetworkConnection journalTo;
        private string journalSent;
        private bool journalDirty;
        private float nextJournal, nextWatch;

        // Clients
        private CraftWorkshopFollower follower;
        private readonly CraftSettingQueue settings = new CraftSettingQueue();
        private readonly List<CraftRequest> settingsDue = new List<CraftRequest>();
        private readonly NetPayloadAssembler observerParts = new NetPayloadAssembler();
        private readonly NetPayloadAssembler targetParts = new NetPayloadAssembler();
        private readonly CraftAnswerQueue answers = new CraftAnswerQueue();
        private readonly List<CraftEvent> answerEvents = new List<CraftEvent>();
        private bool openWhenReady;

        private CraftStation Station => station != null ? station : (station = GetComponent<CraftStation>());

        public bool RunsStationRules => IsServerInitialized; // not "IsServer": Fish-Net's NetworkBehaviour already has one

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            CraftStation s = Station;
            s.Net = this;
            s.WorkshopReplaced += OnWorkshopReplaced;
            feed.Attach(s.Workshop);
            beat.Interval = busyInterval;
            beat.IdleInterval = idleInterval;
            beat.ChecksumEvery = checksumEvery;
            sentMeta = int.MinValue;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (station != null)
            {
                station.WorkshopReplaced -= OnWorkshopReplaced;
                if (ReferenceEquals(station.Net, this))
                    station.Net = null;
            }
            feed.Attach(null);
            WatchJournal(null, null);
            nextSnapshot.Clear();
            nextTend.Clear();
            lastTold.Clear();
            answersOut.Clear();
            learntOut.Clear();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized)
                return; // the host runs the station itself
            CraftStation s = Station;
            s.Net = this;
            follower = new CraftWorkshopFollower(s.Rules);
            follower.Replaced += OnCopyReplaced;
            s.WorkshopEvent += OnCopyEvent;
            s.SetMirror(null); // nothing to show until the server's workshop arrives
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized || station == null)
                return;
            CraftStationUI.Close(station);
            station.WorkshopEvent -= OnCopyEvent;
            station.ClearMirror();
            if (ReferenceEquals(station.Net, this))
                station.Net = null;
            follower = null;
            answers.Clear();
            settings.Clear();
            openWhenReady = false;
        }

        // ------------------------------------------------------------------ server: the feed
        private void OnWorkshopReplaced()
        {
            if (!IsServerInitialized)
                return;
            feed.Attach(station.Workshop);
            replaced = true; // everyone gets it whole at the end of the frame
        }

        private void LateUpdate()
        {
            if (IsServerInitialized)
                ServerLateUpdate();
        }

        private void ServerLateUpdate()
        {
            CraftStation s = Station;
            float now = Time.unscaledTime;
            if (now >= nextWatch)
            {
                nextWatch = now + 0.5f;
                WatchTender();
            }
            if (journalDirty && journalTo != null && now >= nextJournal)
                SendJournal(watchedJournal, journalTo, now, false);
            if (feed.Workshop != s.Workshop)
                OnWorkshopReplaced();
            SendFeed(now);
            // The answers (and the lines learnt doing that work) go after what carries their operations: the same reliable
            // channel keeps them in order, so a client's copy shows the work before its answer is raised.
            SendAnswers();
        }

        /// <summary>The workshop whole (it was replaced) or what happened since the last packet, when a beat is due.</summary>
        private void SendFeed(float now)
        {
            CraftStation s = Station;
            bool metaChanged = s.MetaVersion != sentMeta;
            if (replaced)
            {
                replaced = false;
                feed.DropPending(); // the snapshot has them
                sentMeta = s.MetaVersion;
                SendText(Snapshot, feed.Snapshot(Meta()).ToJson(), null);
                beat.Sent(now, Busy);
                return;
            }
            if (!beat.Due(now, feed.HasPending || metaChanged))
                return;
            bool checksum = beat.Sent(now, Busy);
            CraftMirrorPacket p = feed.TakePacket(checksum, metaChanged ? Meta() : null);
            sentMeta = s.MetaVersion;
            SendText(Packet, p.ToJson(), null);
        }

        private bool Busy
        {
            get
            {
                CraftWorkshop w = station != null ? station.Workshop : null;
                return station != null && station.Tender != null || w != null && (w.Forge.lit || w.Furnace.fire.lit);
            }
        }

        private CraftStationMeta Meta()
        {
            CraftStation s = Station;
            var m = new CraftStationMeta { version = s.MetaVersion, tender = s.Tender != null ? PlayerIds.Of(s.Tender) : 0 };
            m.inspected.AddRange(s.InspectedIds);
            return m;
        }

        /// <summary>Sends text in parts: to everyone watching (<paramref name="to"/> null) or to one client.</summary>
        private void SendText(byte kind, string json, NetworkConnection to)
        {
            List<byte[]> parts = NetPayload.Split(NetPayload.Pack(json));
            int id = ++transfer;
            for (int i = 0; i < parts.Count; i++)
            {
                if (to == null)
                    ObserversData(kind, id, (short)i, (short)parts.Count, parts[i]);
                else
                    TargetData(to, kind, id, (short)i, (short)parts.Count, parts[i]);
            }
        }

        private void SendSnapshot(NetworkConnection conn) => SendText(Snapshot, feed.Snapshot(Meta()).ToJson(), conn);

        // ------------------------------------------------------------------ server: who works here
        /// <summary>The player working here walked away, died or left: the station is free again (and forgets a player who left).</summary>
        private void WatchTender()
        {
            CraftStation s = Station;
            CombatEntity t = s.Tender;
            if (t == null || CardTable.IsLocalPlayer(t))
            {
                WatchJournal(null, null); // nobody, or the host's own player (its window watches itself)
                return;
            }
            NetworkConnection conn = NetObjects.OwnerOf(t);
            if (conn == null || !conn.IsActive || !t.isActiveAndEnabled || !t.IsAlive || !InReach(t))
            {
                s.EndTending(t);
                if (conn != null && conn.IsActive)
                    TargetClose(conn);
                else
                    s.ForgetPlayer(t); // the connection went: their guard goes with them
                WatchJournal(null, null);
                return;
            }
            WatchJournal(CraftKnowledge.For(t), conn);
        }

        private bool InReach(CombatEntity player)
        {
            CraftStation s = Station;
            if (player == null || player.gameObject.scene != s.gameObject.scene)
                return false;
            Vector3 a = s.InteractionPoint.position, b = player.Position;
            a.y = b.y = 0f;
            float reach = s.InteractionRange * 1.6f + 0.5f + rangeTolerance;
            return (a - b).sqrMagnitude <= reach * reach;
        }

        bool ICraftStationNet.Open(CombatEntity player)
        {
            if (!IsServerInitialized)
                return false;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            if (conn == null || !conn.IsActive || conn.IsLocalClient)
                return false;
            SendSnapshot(conn);
            CraftKnowledge k = CraftKnowledge.For(player);
            WatchJournal(k, conn); // what they learn from the first moment reaches them
            SendJournal(k, conn, Time.unscaledTime, true);
            TargetOpen(conn);
            return true;
        }

        void ICraftStationNet.SendTending(bool tending)
        {
            if (!IsClientInitialized || IsServerInitialized)
                return;
            if (!tending)
                FlushSettings(true); // the last value of a setting the hand turned reaches the station before the hands leave it
            ServerTend(tending);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTend(bool tending, NetworkConnection conn = null)
        {
            // Stopping is never held back (a quick open and close must not leave the station taken); starting a little.
            if (conn == null || !conn.IsActive || tending && !Allowed(nextTend, conn, 0.2f))
                return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            CraftStation s = Station;
            if (player == null)
                return;
            if (!tending)
            {
                if (s.Tender == player)
                    s.EndTending(player);
                return;
            }
            if (s.Tender == player)
                return;
            if (!player.IsAlive || !InReach(player))
            {
                TargetClose(conn);
                return;
            }
            if (s.Tender != null && s.Tender.isActiveAndEnabled)
            {
                TargetMessage(conn, "Someone is working here.");
                TargetClose(conn);
                return;
            }
            s.BeginTending(player);
            CraftKnowledge k = CraftKnowledge.For(player);
            WatchJournal(k, conn);
            SendJournal(k, conn, Time.unscaledTime, true);
        }

        // ------------------------------------------------------------------ server: the journal of the player working here
        private void WatchJournal(CraftKnowledge k, NetworkConnection conn)
        {
            if (k == watchedJournal && conn == journalTo)
                return;
            if (watchedJournal != null)
            {
                watchedJournal.Changed -= OnJournalChanged;
                watchedJournal.Learnt -= OnJournalLearnt;
            }
            watchedJournal = k;
            journalTo = conn;
            journalSent = null; // the next one goes whole
            learntOut.Clear();
            if (watchedJournal != null)
            {
                watchedJournal.Changed += OnJournalChanged;
                watchedJournal.Learnt += OnJournalLearnt;
            }
        }

        private void OnJournalChanged() => journalDirty = true;

        /// <summary>The player working here learnt something (a new rank, an insight): it goes to their window, whole, after the work's answer.</summary>
        private void OnJournalLearnt(string line)
        {
            if (!string.IsNullOrEmpty(line) && journalTo != null && learntOut.Count < 32)
                learntOut.Add(line);
        }

        /// <summary>The journal, compact - only when something in it changed since the last one sent (<paramref name="whole"/>: anyway).</summary>
        private void SendJournal(CraftKnowledge k, NetworkConnection conn, float now, bool whole)
        {
            journalDirty = false;
            nextJournal = now + journalInterval;
            if (k == null || conn == null || !conn.IsActive || conn.IsLocalClient)
                return;
            string json = k.Export(false);
            if (!whole && json == journalSent)
                return;
            journalSent = json;
            SendText(Journal, json, conn);
        }

        // ------------------------------------------------------------------ server: requests
        [ServerRpc(RequireOwnership = false)]
        private void ServerSnapshot(NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextSnapshot, conn, 1f))
                return;
            SendSnapshot(conn);
        }

        /// <summary>
        /// A client's player did something at the station; the server checks it and runs it like a local player's action
        /// (the station bounds it with the player's guard), and answers it - also when it never reached the station.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerRequest(CraftNetRequest m, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive)
                return;
            if (!m.IsWellFormed)
            {
                Answer(conn, m.seq, false, CraftRequestGuard.TooMuch);
                return;
            }
            CombatEntity player = NetObjects.PlayerOf(conn);
            CraftStation s = Station;
            if (player == null || !player.IsAlive)
            {
                Answer(conn, m.seq, false, "You cannot work now.");
                return;
            }
            if (s.Tender != player)
            {
                Answer(conn, m.seq, false, "You are not working at this station.");
                return;
            }
            if (!InReach(player))
            {
                Answer(conn, m.seq, false, "Too far away to work there.");
                return;
            }
            int before = feed.Seq;
            bool ok = s.Request(player, m.ToRequest(), out string reason);
            // the operations it recorded: numbered before+1 .. feed.Seq (none when the feed did not move)
            Answer(conn, m.seq, ok, reason, before + 1, feed.Seq > before ? feed.Seq : 0);
        }

        /// <summary>
        /// The answer to a client's request: with a number, it goes after the packet that carries the request's operations
        /// (at the end of the frame); without one (the 3D work places), a refusal or a note is told as a message.
        /// </summary>
        private void Answer(NetworkConnection conn, int seq, bool ok, string reason, int firstOp = 0, int lastOp = 0)
        {
            if (seq > 0)
                answersOut.Add((conn, new CraftAnswer(seq, ok, reason, firstOp, lastOp)));
            else
                TellOnce(conn, reason);
        }

        private void SendAnswers()
        {
            for (int i = 0; i < answersOut.Count; i++)
            {
                (NetworkConnection conn, CraftAnswer a) = answersOut[i];
                if (conn != null && conn.IsActive)
                    TargetAnswer(conn, a.seq, a.ok, Clip(a.reason, MaxLineLength), a.firstOp, a.lastOp);
            }
            answersOut.Clear();
            if (learntOut.Count == 0)
                return;
            if (journalTo != null && journalTo.IsActive && !journalTo.IsLocalClient)
                foreach (string line in learntOut)
                    TargetLearnt(journalTo, Clip(line, MaxLineLength));
            learntOut.Clear();
        }

        /// <summary>A refusal (or a note) for a request nobody waits for: told at once, the same words again only after a moment (a held button does not flood the screen).</summary>
        private void TellOnce(NetworkConnection conn, string reason)
        {
            if (string.IsNullOrEmpty(reason) || conn == null || !conn.IsActive)
                return;
            float now = Time.unscaledTime;
            if (lastTold.TryGetValue(conn.ClientId, out (string text, float at) last) && last.text == reason && now - last.at < RepeatInterval)
                return;
            if (lastTold.Count > 64)
                lastTold.Clear();
            lastTold[conn.ClientId] = (reason, now);
            TargetMessage(conn, Clip(reason, MaxMessageLength));
        }

        private static bool Allowed(Dictionary<int, float> next, NetworkConnection conn, float interval)
        {
            float now = Time.unscaledTime;
            if (next.TryGetValue(conn.ClientId, out float at) && now < at)
                return false;
            next[conn.ClientId] = now + interval;
            return true;
        }

        void ICraftStationNet.Tell(CombatEntity player, string text)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(text))
                return;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            if (conn != null && conn.IsActive && !conn.IsLocalClient)
                TargetMessage(conn, Clip(text, MaxMessageLength));
        }

        private static string Clip(string text, int most) => text == null ? "" : text.Length > most ? text.Substring(0, most) : text;

        // ------------------------------------------------------------------ clients
        void ICraftStationNet.SendRequest(CraftRequest r)
        {
            if (!IsClientInitialized || IsServerInitialized)
                return;
            if (CraftRequest.IsSetting(r.kind))
            {
                // A slider dragged (the air vent, a vat's heat, the oven): only its latest value goes, ten times a second.
                r.seq = 0;
                settings.Put(r);
                return;
            }
            Forward(r);
        }

        /// <summary>Sends the latest value of each setting turned: ten times a second, or at once (<paramref name="now"/>: the hands leave the station).</summary>
        private void FlushSettings(bool now = false)
        {
            if (settings.Count == 0 || !settings.Take(Time.unscaledTimeAsDouble, settingsDue, now))
                return;
            foreach (CraftRequest r in settingsDue)
                Forward(r);
            settingsDue.Clear();
        }

        private void Forward(CraftRequest r)
        {
            string itemId = r.item != null ? ItemDatabase.Id(r.item) : r.itemId;
            if (r.item != null && string.IsNullOrEmpty(itemId))
            {
                Station.ShowLocalMessage($"{r.item.Name} is not in the Item Database: it cannot be used in a network session.");
                return;
            }
            ServerRequest(CraftNetRequest.From(r, itemId));
        }

        private void Update()
        {
            if (follower == null || IsServerInitialized || !IsClientInitialized)
                return;
            FlushSettings();
            if (follower.State.ShouldAsk(Time.unscaledTime))
                ServerSnapshot();
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ObserversData(byte kind, int id, short index, short count, byte[] part) => ReceivePart(observerParts, kind, id, index, count, part);

        [TargetRpc]
        private void TargetData(NetworkConnection conn, byte kind, int id, short index, short count, byte[] part) => ReceivePart(targetParts, kind, id, index, count, part);

        private void ReceivePart(NetPayloadAssembler parts, byte kind, int id, short index, short count, byte[] part)
        {
            if (IsServerInitialized || follower == null)
                return;
            if (!parts.Add(id, index, count, part, out byte[] whole) || !NetPayload.TryUnpack(whole, out string json))
                return;
            switch (kind)
            {
                case Snapshot:
                {
                    CraftMirrorSnapshot s = CraftMirrorSnapshot.FromJson(json);
                    if (s == null)
                        return;
                    follower.Restore(s);
                    ShowMeta(follower.Meta);
                    if (openWhenReady)
                        OpenWindow();
                    PumpAnswers();
                    break;
                }
                case Packet:
                {
                    CraftMirrorPacket p = CraftMirrorPacket.FromJson(json);
                    if (p == null)
                        return;
                    FollowResult r = follower.Apply(p);
                    if (p.hasMeta)
                        ShowMeta(follower.Meta);
                    if (r != FollowResult.Waiting)
                        station.MirrorChanged();
                    PumpAnswers();
                    break;
                }
                case Journal:
                {
                    CombatEntity me = LocalPlayer();
                    if (me != null)
                        CraftKnowledge.For(me)?.ReceiveFromServer(json);
                    break;
                }
            }
        }

        /// <summary>The copy raised an event: while it applies an operation, it is that operation's (for the request's answer).</summary>
        private void OnCopyEvent(CraftEvent e)
        {
            if (follower != null)
                answers.Raised(follower.Applying, e);
        }

        /// <summary>The server's answer to one of this machine's requests: raised once the copy shows what the request did.</summary>
        [TargetRpc]
        private void TargetAnswer(NetworkConnection conn, int seq, bool ok, string reason, int firstOp, int lastOp)
        {
            if (IsServerInitialized || follower == null)
                return;
            answers.Arrived(new CraftAnswer(seq, ok, reason, firstOp, lastOp));
            PumpAnswers();
        }

        /// <summary>Raises the answers the copy now shows, in the order they came, each with the events its operations raised.</summary>
        private void PumpAnswers()
        {
            if (follower == null || station == null)
                return;
            while (answers.Next(follower.State.LastSeq, out CraftAnswer a))
            {
                answers.EventsOf(a, answerEvents);
                station.Answer(a.seq, a.ok, a.reason, answerEvents);
            }
        }

        /// <summary>A line the player learnt (from the server's journal): the station's window shows and keeps it - not the inventory's line, which has it already.</summary>
        [TargetRpc]
        private void TargetLearnt(NetworkConnection conn, string text)
        {
            if (!IsServerInitialized && station != null && !string.IsNullOrEmpty(text))
                station.ShowLearnt(text);
        }

        private void OnCopyReplaced(CraftWorkshop copy)
        {
            if (station != null)
                station.SetMirror(copy);
        }

        private void ShowMeta(CraftStationMeta m)
        {
            if (m != null && station != null)
                station.SetMirrorMeta(m.tender > 0 ? PlayerIds.Find(m.tender) : null, m.inspected);
        }

        [TargetRpc]
        private void TargetOpen(NetworkConnection conn)
        {
            openWhenReady = true;
            if (follower != null && follower.State.Ready)
                OpenWindow();
        }

        private void OpenWindow()
        {
            openWhenReady = false;
            CombatEntity me = LocalPlayer();
            if (me != null && station != null && station.Workshop != null)
                CraftStationUI.Show(station, me);
        }

        [TargetRpc]
        private void TargetClose(NetworkConnection conn)
        {
            openWhenReady = false;
            if (station != null)
                CraftStationUI.Close(station);
        }

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text)
        {
            if (!IsServerInitialized && station != null && !string.IsNullOrEmpty(text) && text.Length <= MaxMessageLength)
                station.ShowLocalMessage(text);
        }

        private static CombatEntity LocalPlayer()
        {
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                    return players[i];
            return null;
        }
    }
}
#endif
