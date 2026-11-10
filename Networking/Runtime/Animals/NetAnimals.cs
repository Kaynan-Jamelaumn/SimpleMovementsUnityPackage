#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Animal husbandry in a network session, with the server deciding everything. Put it on the "Network World" object
    /// (Tools ▸ SimpleMovements ▸ Network ▸ Add Missing Relays To Network World): one relay serves every pen, so pens
    /// need no NetworkObject of their own.
    /// <list type="bullet">
    /// <item><b>The server keeps the animals</b> (the <see cref="AnimalDirector"/>'s book): their genes, nutrition, toxins,
    /// the feeder's lots. Clients get a <b>picture</b> of each pen near their player (<see cref="AnimalPenView"/>: the
    /// animals as they look - coarse levels and signs, never the numbers -, the feeder, trough and grass as they look, and
    /// what that player may do there), when it changes and now and then, with the farm clock.</item>
    /// <item><b>Only to those near</b>: a client watches the pens within the rules' Show Radius of its player and forgets
    /// them beyond Hide Radius; the server checks where the player really is, sends pictures only to watchers, and drops
    /// a watcher that walked (or lied) away.</item>
    /// <item><b>Requests</b>: every action asks the server, which checks it - the player from the connection, not too
    /// many too fast, a well-formed request, the pen within reach of where the player really stands - and does it with the
    /// player's own items (<see cref="AnimalPen.PerformHere"/>). Every request is answered with its number; a request
    /// already done (a retry with the same token) is answered and never done again.</item>
    /// </list>
    /// Offline it does nothing (the animals live on the machine).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetAnimals : NetworkBehaviour, IAnimalNetRelay
    {
        public static NetAnimals Instance { get; private set; }

        private const int MaxRequestLength = 4096;
        private const int MaxIdLength = 128;
        private const int MaxTextLength = 120;
        private const int MaxMessageLength = 200;
        private const int MaxWatchedPens = 64;
        private const byte ViewData = 0, AnswerData = 1;
        /// <summary>Metres beyond Hide Radius the server tolerates before it drops a watcher (lag, a fast mount).</summary>
        private const float WatcherSlack = 30f;
        /// <summary>The most a fee the player was shown can be (CallVet's expected fee).</summary>
        private const long MaxExpected = 1000000000L;
        /// <summary>Real seconds between two veterinarian visits one client may ask for.</summary>
        private const float VetInterval = 2f;

        [Tooltip("Least seconds between two pictures of a pen sent to one client (changes in between go together).")]
        [SerializeField, Range(0.1f, 5f)] private float sendInterval = 0.5f;
        [Tooltip("Seconds between pictures sent even when nothing changed (clocks, late changes).")]
        [SerializeField, Range(5f, 120f)] private float refreshInterval = 30f;
        [Tooltip("Seconds a client's requests must be apart on average (a few may come at once).")]
        [SerializeField, Range(0.02f, 1f)] private float requestInterval = 0.1f;

        // Server (by client id; forgotten when the client leaves)
        private readonly Dictionary<int, (float tokens, float at)> tokens = new Dictionary<int, (float, float)>();
        private readonly Dictionary<int, float> nextRefusal = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextWatch = new Dictionary<int, float>();
        /// <summary>When each client may next send for the veterinarian (a visit costs money and work: a few, never a flood).</summary>
        private readonly Dictionary<int, float> nextVet = new Dictionary<int, float>();
        /// <summary>Who watches each pen: the connection, the picture's revision it holds, when it was sent.</summary>
        private readonly Dictionary<string, Dictionary<int, Watcher>> watchers = new Dictionary<string, Dictionary<int, Watcher>>(StringComparer.Ordinal);
        private readonly List<int> drop = new List<int>();
        private readonly List<string> emptyPens = new List<string>();
        private NetSession session;
        private float nextSend, nextPrune;
        private int transfer;

        private struct Watcher
        {
            public NetworkConnection conn;
            public int revision;
            public float sentAt;
        }

        // Client
        private readonly NetPayloadAssembler parts = new NetPayloadAssembler();
        private string openWhenReady = "";

        [Serializable]
        private class Answer
        {
            public string pen = "";
            public AnimalResult result;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[Net] More than one NetAnimals: keep one, on the Network World object.", this);
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
            AnimalNet.Relay = this;
            session = NetSession.Instance;
            if (session != null) session.ClientLeft += OnClientLeft;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (session != null) session.ClientLeft -= OnClientLeft;
            session = null;
            tokens.Clear();
            nextRefusal.Clear();
            nextWatch.Clear();
            nextVet.Clear();
            watchTokens.Clear();
            watchers.Clear();
            if (ReferenceEquals(AnimalNet.Relay, this)) AnimalNet.Relay = null;
            if (Instance == this) Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return; // the host runs the animals itself
            Instance = this;
            AnimalNet.Relay = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized) return;
            if (ReferenceEquals(AnimalNet.Relay, this)) AnimalNet.Relay = null;
            parts.Reset();
            openWhenReady = "";
            // What this client was shown belongs to the session: it is forgotten with it.
            foreach (AnimalPen p in AnimalDirector.Pens) if (p != null) p.ForgetView();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            if (session != null) session.ClientLeft -= OnClientLeft;
            if (ReferenceEquals(AnimalNet.Relay, this)) AnimalNet.Relay = null;
            if (Instance == this) Instance = null;
        }

        private void OnClientLeft(NetworkConnection conn)
        {
            if (conn == null) return;
            int id = conn.ClientId;
            tokens.Remove(id);
            nextRefusal.Remove(id);
            nextWatch.Remove(id);
            nextVet.Remove(id);
            watchTokens.Remove(id);
            foreach (Dictionary<int, Watcher> w in watchers.Values) w.Remove(id);
        }

        private bool IsRemoteClient => IsClientInitialized && !IsServerInitialized;

        // ------------------------------------------------------------------ server: pictures
        private void LateUpdate()
        {
            if (!IsServerInitialized || watchers.Count == 0) return;
            float now = Time.unscaledTime;
            if (now >= nextPrune)
            {
                nextPrune = now + 1f;
                Prune();
            }
            if (now < nextSend) return;
            nextSend = now + 0.25f;
            foreach (KeyValuePair<string, Dictionary<int, Watcher>> kv in watchers)
            {
                if (kv.Value.Count == 0) continue;
                AnimalPen pen = AnimalDirector.FindPen(kv.Key);
                if (pen == null) continue;
                int revision = AnimalDirector.RevisionOf(kv.Key);
                drop.Clear();
                foreach (KeyValuePair<int, Watcher> w in kv.Value) drop.Add(w.Key);
                foreach (int id in drop)
                {
                    Watcher w = kv.Value[id];
                    bool due = (w.revision != revision && now - w.sentAt >= sendInterval) || now - w.sentAt >= refreshInterval;
                    if (!due) continue;
                    CombatEntity player = NetObjects.PlayerOf(w.conn);
                    if (player == null) continue;
                    SendViewTo(w.conn, pen, player);
                    w.revision = AnimalDirector.RevisionOf(kv.Key);
                    w.sentAt = now;
                    kv.Value[id] = w;
                }
            }
        }

        /// <summary>Drops watchers whose connection ended or whose player is far from the pen (where the server sees them).</summary>
        private void Prune()
        {
            float limit = AnimalDatabase.Instance.Rules.hideRadius + WatcherSlack;
            emptyPens.Clear();
            foreach (KeyValuePair<string, Dictionary<int, Watcher>> kv in watchers)
            {
                AnimalPen pen = AnimalDirector.FindPen(kv.Key);
                drop.Clear();
                foreach (KeyValuePair<int, Watcher> w in kv.Value)
                {
                    CombatEntity player = w.Value.conn != null && w.Value.conn.IsActive ? NetObjects.PlayerOf(w.Value.conn) : null;
                    bool apart = player != null && pen != null && player.gameObject.scene != pen.gameObject.scene &&
                                 MobRealms.AreScenesSeparated(player.gameObject.scene, pen.gameObject.scene);
                    if (pen == null || player == null || apart || pen.DistanceFromOutline(player.transform.position) > limit) drop.Add(w.Key);
                }
                foreach (int id in drop) kv.Value.Remove(id);
                if (kv.Value.Count == 0) emptyPens.Add(kv.Key);
            }
            foreach (string id in emptyPens) watchers.Remove(id);
        }

        private void SendViewTo(NetworkConnection conn, AnimalPen pen, CombatEntity player)
        {
            AnimalPenView view = AnimalDirector.ViewFor(pen, player);
            if (view != null) SendText(conn, ViewData, view.ToJson());
        }

        private void AddWatcher(NetworkConnection conn, AnimalPen pen, CombatEntity player)
        {
            int watched = 0;
            foreach (Dictionary<int, Watcher> w in watchers.Values) if (w.ContainsKey(conn.ClientId)) watched++;
            if (!watchers.TryGetValue(pen.PenId, out Dictionary<int, Watcher> list))
            {
                if (watched >= MaxWatchedPens) return;
                watchers[pen.PenId] = list = new Dictionary<int, Watcher>();
            }
            else if (!list.ContainsKey(conn.ClientId) && watched >= MaxWatchedPens) return;
            SendViewTo(conn, pen, player);
            list[conn.ClientId] = new Watcher { conn = conn, revision = AnimalDirector.RevisionOf(pen.PenId), sentAt = Time.unscaledTime };
        }

        private void SendText(NetworkConnection conn, byte kind, string text)
        {
            if (conn == null || !conn.IsActive || conn.IsLocalClient) return;
            List<byte[]> list = NetPayload.Split(NetPayload.Pack(text ?? ""));
            int id = ++transfer;
            for (int i = 0; i < list.Count; i++) TargetData(conn, kind, id, (short)i, (short)list.Count, list[i]);
        }

        private static NetworkConnection RemoteOf(CombatEntity player)
        {
            NetworkConnection conn = NetObjects.OwnerOf(player);
            return conn != null && conn.IsActive && !conn.IsLocalClient ? conn : null;
        }

        // ------------------------------------------------------------------ server: requests
        private bool TakeToken(NetworkConnection conn)
        {
            float now = Time.unscaledTime;
            const float burst = 8f;
            float rate = 1f / Mathf.Max(0.02f, requestInterval);
            if (!tokens.TryGetValue(conn.ClientId, out (float tokens, float at) b))
            {
                if (tokens.Count > 512) tokens.Clear();
                b = (burst, now);
            }
            b.tokens = Mathf.Min(burst, b.tokens + (now - b.at) * rate);
            b.at = now;
            bool ok = b.tokens >= 1f;
            if (ok) b.tokens -= 1f;
            tokens[conn.ClientId] = b;
            return ok;
        }

        private readonly Dictionary<int, (float tokens, float at)> watchTokens = new Dictionary<int, (float, float)>();

        private bool TakeWatchToken(NetworkConnection conn)
        {
            float now = Time.unscaledTime;
            const float burst = 16f, rate = 4f;
            if (!watchTokens.TryGetValue(conn.ClientId, out (float tokens, float at) b))
            {
                if (watchTokens.Count > 512) watchTokens.Clear();
                b = (burst, now);
            }
            b.tokens = Mathf.Min(burst, b.tokens + (now - b.at) * rate);
            b.at = now;
            bool ok = b.tokens >= 1f;
            if (ok) b.tokens -= 1f;
            watchTokens[conn.ClientId] = b;
            return ok;
        }

        private static bool Allowed(Dictionary<int, float> next, NetworkConnection conn, float interval)
        {
            float now = Time.unscaledTime;
            if (next.TryGetValue(conn.ClientId, out float at) && now < at) return false;
            if (next.Count > 512) next.Clear();
            next[conn.ClientId] = now + interval;
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequest(string json, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || conn.IsLocalClient) return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null) return; // nobody to answer
            if (string.IsNullOrEmpty(json) || json.Length > MaxRequestLength) return;
            AnimalRequest r;
            try { r = JsonUtility.FromJson<AnimalRequest>(json); }
            catch (Exception) { return; }
            string penId = Cut(r.pen, MaxIdLength);
            void Refuse(string why) => SendAnswer(conn, penId, new AnimalResult { ok = false, seq = r.seq, message = why });
            if (!TakeToken(conn))
            {
                if (Allowed(nextRefusal, conn, 1f)) Refuse("Not so fast.");
                return;
            }
            if (!Valid(ref r))
            {
                Refuse("That cannot be done.");
                return;
            }
            // Sending for the veterinarian: at most one every few seconds per client (each visit is real work and money). The
            // slot is taken whatever the request's outcome (a changed fee is refused too), so the text claims no visit.
            if (r.kind == AnimalActionKind.CallVet && !Allowed(nextVet, conn, VetInterval))
            {
                Refuse("Wait a moment before sending for the veterinarian again.");
                return;
            }
            AnimalPen pen = AnimalDirector.FindPen(r.pen);
            if (pen == null || !pen.interactable)
            {
                Refuse("There is no such pen here.");
                return;
            }
            if (!player.IsAlive || (player.gameObject.scene != pen.gameObject.scene && MobRealms.AreScenesSeparated(player.gameObject.scene, pen.gameObject.scene)))
            {
                Refuse("You cannot tend animals now.");
                return;
            }
            // Where the player stands is the server's own: a request from afar is refused before anything is read.
            if (pen.DistanceFromOutline(player.transform.position) > pen.Rules.workRange + pen.extraReach + 2f)
            {
                Refuse("That is out of reach.");
                return;
            }
            pen.PerformHere(r, player); // answers through SendResult
            SendViewTo(conn, pen, player);
        }

        /// <summary>
        /// A well-formed request: a known action, bounded ids, texts and counts, an expected fee within bounds (the rules check
        /// everything else: a care action's id - in the text, bounded like any text -, the fee itself, rights and items).
        /// </summary>
        private static bool Valid(ref AnimalRequest r)
        {
            if (!Enum.IsDefined(typeof(AnimalActionKind), r.kind)) return false;
            r.pen = Cut(r.pen, MaxIdLength);
            r.animal = Cut(r.animal, MaxIdLength);
            r.item = Cut(r.item, MaxIdLength);
            r.text = Cut(r.text, MaxTextLength);
            r.token = Cut(r.token, 64);
            if (string.IsNullOrEmpty(r.pen)) return false;
            if (r.count < 0 || r.count > 1000) return false;
            if (r.expected < 0L) r.expected = 0L;
            else if (r.expected > MaxExpected) r.expected = MaxExpected;
            return true;
        }

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length > max ? s.Substring(0, max) : s;

        [ServerRpc(RequireOwnership = false)]
        private void ServerWatch(string penId, bool watch, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || conn.IsLocalClient) return;
            if (string.IsNullOrEmpty(penId) || penId.Length > MaxIdLength) return;
            if (!watch)
            {
                if (watchers.TryGetValue(penId, out Dictionary<int, Watcher> list)) list.Remove(conn.ClientId);
                return;
            }
            if (!TakeWatchToken(conn)) return; // a street of pens comes into view at once: a burst, then a few a second
            CombatEntity player = NetObjects.PlayerOf(conn);
            AnimalPen pen = AnimalDirector.FindPen(penId);
            if (player == null || pen == null) return;
            if (player.gameObject.scene != pen.gameObject.scene && MobRealms.AreScenesSeparated(player.gameObject.scene, pen.gameObject.scene)) return;
            if (pen.DistanceFromOutline(player.transform.position) > AnimalDatabase.Instance.Rules.hideRadius + WatcherSlack) return;
            AddWatcher(conn, pen, player);
        }

        private void SendAnswer(NetworkConnection conn, string penId, AnimalResult result) =>
            SendText(conn, AnswerData, JsonUtility.ToJson(new Answer { pen = penId ?? "", result = Visible(result) }));

        /// <summary>Only what the player is shown leaves the server.</summary>
        private static AnimalResult Visible(AnimalResult r)
        {
            if (r == null) return new AnimalResult { ok = false, message = "That cannot be done." };
            var v = new AnimalResult { ok = r.ok, seq = r.seq, message = Cut(r.message, 400), animal = Cut(r.animal, MaxIdLength), repeated = r.repeated };
            if (r.lines != null)
                for (int i = 0; i < r.lines.Count && i < 32; i++) v.lines.Add(Cut(r.lines[i], 400));
            return v;
        }

        // ------------------------------------------------------------------ IAnimalNetRelay
        bool IAnimalNetRelay.SendRequest(AnimalRequest request)
        {
            if (!IsRemoteClient) return false;
            request.text = Cut(request.text, MaxTextLength);
            ServerRequest(JsonUtility.ToJson(request));
            return true;
        }

        void IAnimalNetRelay.Watch(string penId, bool watching)
        {
            if (!IsRemoteClient || string.IsNullOrEmpty(penId)) return;
            ServerWatch(penId, watching);
        }

        void IAnimalNetRelay.SendView(CombatEntity player, AnimalPenView view)
        {
            if (!IsServerInitialized || view == null) return;
            NetworkConnection conn = RemoteOf(player);
            if (conn != null) SendText(conn, ViewData, view.ToJson());
        }

        void IAnimalNetRelay.SendResult(CombatEntity player, string penId, AnimalResult result)
        {
            if (!IsServerInitialized) return;
            NetworkConnection conn = RemoteOf(player);
            if (conn != null) SendAnswer(conn, penId, result);
        }

        void IAnimalNetRelay.Open(CombatEntity player, string penId)
        {
            if (!IsServerInitialized) return;
            NetworkConnection conn = RemoteOf(player);
            AnimalPen pen = AnimalDirector.FindPen(penId);
            if (conn == null || pen == null) return;
            AddWatcher(conn, pen, player);
            TargetOpen(conn, penId);
        }

        void IAnimalNetRelay.Tell(CombatEntity player, string text)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(text)) return;
            NetworkConnection conn = RemoteOf(player);
            if (conn != null) TargetMessage(conn, Cut(text, MaxMessageLength));
        }

        // ------------------------------------------------------------------ client: receiving
        [TargetRpc]
        private void TargetData(NetworkConnection conn, byte kind, int id, short index, short count, byte[] part)
        {
            if (IsServerInitialized) return;
            if (!parts.Add(id, index, count, part, out byte[] whole)) return;
            if (!NetPayload.TryUnpack(whole, out string json)) return;
            try
            {
                switch (kind)
                {
                    case ViewData:
                    {
                        AnimalPenView v = AnimalPenView.FromJson(json);
                        if (v == null) return;
                        FarmClock.FromServer(v.now);
                        AnimalPen pen = AnimalDirector.FindPen(v.id);
                        if (pen == null) return;
                        pen.ReceiveView(v);
                        if (openWhenReady == v.id)
                        {
                            openWhenReady = "";
                            CombatEntity me = AnimalDirector.LocalPlayer();
                            if (me != null) AnimalPenWindow.Show(pen, me);
                        }
                        break;
                    }
                    case AnswerData:
                    {
                        Answer a = JsonUtility.FromJson<Answer>(json);
                        AnimalPen pen = a != null ? AnimalDirector.FindPen(a.pen) : null;
                        CombatEntity me = AnimalDirector.LocalPlayer();
                        if (pen != null && me != null && a.result != null) pen.ReceiveAnswer(me, a.result);
                        else if (me != null && a?.result != null && !a.result.ok) InventoryManager.For(me)?.ShowMessage(a.result.message, InventoryFeedback.Kind.Warning);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] A broken animal message ({e.Message}).");
            }
        }

        [TargetRpc]
        private void TargetOpen(NetworkConnection conn, string penId)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(penId) || penId.Length > MaxIdLength) return;
            AnimalPen pen = AnimalDirector.FindPen(penId);
            CombatEntity me = AnimalDirector.LocalPlayer();
            if (pen == null || me == null) return;
            if (pen.View != null) AnimalPenWindow.Show(pen, me);
            else openWhenReady = penId; // it opens when the picture comes
        }

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(text)) return;
            CombatEntity me = AnimalDirector.LocalPlayer();
            if (me != null) InventoryManager.For(me)?.ShowMessage(text.Length > MaxMessageLength ? text.Substring(0, MaxMessageLength) : text, InventoryFeedback.Kind.Info);
        }
    }
}
#endif
