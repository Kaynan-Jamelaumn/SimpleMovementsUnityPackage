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
    /// one player at a time work the station, opens the window on their screen and sends their journal. What they do
    /// comes back as requests, checked against the player working there, the range, and a
    /// <see cref="CraftRequestGuard"/> (no faster than real hands, never steadier than the player's practice) before
    /// the station runs them like a local player's.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CraftStation))]
    public class NetCraftStation : NetworkBehaviour, ICraftStationNet
    {
        /// <summary>Longest message sent to a client.</summary>
        public const int MaxMessageLength = 200;
        private const byte Packet = 0, Snapshot = 1, Journal = 2;

        [Tooltip("Seconds between two clock updates while someone works here or a fire burns.")]
        [SerializeField, Range(0.05f, 1f)] private float busyInterval = 0.1f;
        [Tooltip("Seconds between two clock updates otherwise (the copies simulate in between).")]
        [SerializeField, Range(0.1f, 5f)] private float idleInterval = 0.5f;
        [Tooltip("A checksum goes with every Nth clock update; a copy that differs asks for the workshop again.")]
        [SerializeField, Range(1, 100)] private int checksumEvery = 10;
        [Tooltip("Metres a player may be beyond the station's reach (server positions) and still work it.")]
        [SerializeField, Range(0f, 5f)] private float rangeTolerance = 1.5f;
        [Tooltip("Least seconds between two journal updates sent to the player working here.")]
        [SerializeField, Range(0.2f, 10f)] private float journalInterval = 1f;

        private CraftStation station;

        // Server
        private readonly CraftWorkshopFeed feed = new CraftWorkshopFeed();
        private readonly StationBeat beat = new StationBeat();
        private bool replaced;
        private int sentMeta = int.MinValue;
        private int transfer;
        private readonly Dictionary<int, CraftRequestGuard> guards = new Dictionary<int, CraftRequestGuard>();
        private readonly Dictionary<int, float> nextSnapshot = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextTend = new Dictionary<int, float>();
        private CraftKnowledge watchedJournal;
        private NetworkConnection journalTo;
        private bool journalDirty;
        private float nextJournal, nextWatch;

        private WeaponNetMath.RateLimiter messageLimiter = new WeaponNetMath.RateLimiter(4f, 4f);

        // Clients
        private CraftWorkshopFollower follower;
        private readonly Dictionary<long, CraftRequest> settings = new Dictionary<long, CraftRequest>();
        private float nextSetting;
        private readonly NetPayloadAssembler observerParts = new NetPayloadAssembler();
        private readonly NetPayloadAssembler targetParts = new NetPayloadAssembler();
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
            guards.Clear();
            nextSnapshot.Clear();
            nextTend.Clear();
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
            s.SetMirror(null); // nothing to show until the server's workshop arrives
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized || station == null)
                return;
            CraftStationUI.Close(station);
            station.ClearMirror();
            if (ReferenceEquals(station.Net, this))
                station.Net = null;
            follower = null;
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
                SendJournal(watchedJournal, journalTo, now);
            if (feed.Workshop != s.Workshop)
                OnWorkshopReplaced();
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
        /// <summary>The player working here walked away, died or left: the station is free again.</summary>
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
            SendJournal(CraftKnowledge.For(player), conn, Time.unscaledTime);
            TargetOpen(conn);
            return true;
        }

        void ICraftStationNet.SendTending(bool tending)
        {
            if (IsClientInitialized && !IsServerInitialized)
                ServerTend(tending);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTend(bool tending, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextTend, conn, 0.2f))
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
            SendJournal(CraftKnowledge.For(player), conn, Time.unscaledTime);
        }

        // ------------------------------------------------------------------ server: the journal of the player working here
        private void WatchJournal(CraftKnowledge k, NetworkConnection conn)
        {
            if (k == watchedJournal && conn == journalTo)
                return;
            if (watchedJournal != null)
                watchedJournal.Changed -= OnJournalChanged;
            watchedJournal = k;
            journalTo = conn;
            if (watchedJournal != null)
                watchedJournal.Changed += OnJournalChanged;
        }

        private void OnJournalChanged() => journalDirty = true;

        private void SendJournal(CraftKnowledge k, NetworkConnection conn, float now)
        {
            journalDirty = false;
            nextJournal = now + journalInterval;
            if (k != null && conn != null && conn.IsActive && !conn.IsLocalClient)
                SendText(Journal, k.Export(), conn);
        }

        // ------------------------------------------------------------------ server: requests
        [ServerRpc(RequireOwnership = false)]
        private void ServerSnapshot(NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextSnapshot, conn, 1f))
                return;
            SendSnapshot(conn);
        }

        /// <summary>A client's player did something at the station; the server checks it and runs it like a local player's action.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerRequest(CraftNetRequest m, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !m.IsWellFormed)
                return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            CraftStation s = Station;
            if (player == null || !player.IsAlive)
                return;
            if (s.Tender != player)
            {
                TargetMessage(conn, "You are not working at this station.");
                return;
            }
            if (!InReach(player))
            {
                TargetMessage(conn, "Too far away to work there.");
                return;
            }
            CraftRequest r = m.ToRequest();
            if (!GuardOf(conn, player).Check(ref r, Time.unscaledTimeAsDouble, out string reason))
            {
                Refuse(conn, reason);
                return;
            }
            if (!s.Request(player, r, out reason))
                Refuse(conn, reason);
        }

        /// <summary>Why a request was refused - a few times a second at most (a held button does not flood the screen).</summary>
        private void Refuse(NetworkConnection conn, string reason)
        {
            if (!string.IsNullOrEmpty(reason) && messageLimiter.TryTake(Time.unscaledTime))
                TargetMessage(conn, Clip(reason));
        }

        private CraftRequestGuard GuardOf(NetworkConnection conn, CombatEntity player)
        {
            if (!guards.TryGetValue(conn.ClientId, out CraftRequestGuard g))
            {
                if (guards.Count > 32)
                    guards.Clear(); // players come and go: a fresh guard for whoever works here next
                g = new CraftRequestGuard(conn.ClientId * 7919 + 17, 0);
                guards[conn.ClientId] = g;
            }
            g.Skill = CraftJournal.HandSkill(CraftKnowledge.For(player)?.Data);
            return g;
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
                TargetMessage(conn, Clip(text));
        }

        private static string Clip(string text) => text.Length > MaxMessageLength ? text.Substring(0, MaxMessageLength) : text;

        // ------------------------------------------------------------------ clients
        void ICraftStationNet.SendRequest(CraftRequest r)
        {
            if (!IsClientInitialized || IsServerInitialized)
                return;
            if (IsSetting(r.kind))
            {
                // A slider dragged (the air vent, a vat's heat, the oven): only its latest value goes, ten times a second.
                settings[(long)r.kind << 40 | (long)(r.i0 & 0xFFFFF) << 1 | (r.flag ? 1L : 0L)] = r;
                return;
            }
            Forward(r);
        }

        private static bool IsSetting(CraftRequestKind kind) => kind == CraftRequestKind.Damper || kind == CraftRequestKind.VatHeat || kind == CraftRequestKind.Oven;

        private void FlushSettings()
        {
            if (settings.Count == 0 || Time.unscaledTime < nextSetting)
                return;
            nextSetting = Time.unscaledTime + 0.1f;
            foreach (CraftRequest r in settings.Values)
                Forward(r);
            settings.Clear();
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
