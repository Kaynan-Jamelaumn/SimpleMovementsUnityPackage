#if FISHNET
using System;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Housing in a network session, with the server deciding everything. Put it on the "Network World" object (Tools ▸
    /// SimpleMovements ▸ Network adds the global relays).
    /// <list type="bullet">
    /// <item><b>Requests</b>: a client's housing window sends each request (renting a room, an order, a renewal, a deposit,
    /// a claim, letting someone in) here. The server checks it - the player from the connection, a lasting key, not too
    /// many too fast, an open session with the innkeeper or realtor in range where the request needs one - and does it with
    /// the player's own wallet and books (<see cref="HousingService"/>), then answers with the request's number and sends
    /// the window's fresh view. A request sent twice (a retry) is done once.</item>
    /// <item><b>Views</b>: only the player's own (their stay, leases, order, account, messages): nobody else's offers,
    /// keys or money ever leave the server. Windows that stay open are refreshed when the books change.</item>
    /// <item><b>Access</b>: each player receives where they may go (their places and those they were let into) and which
    /// houses nobody rents, so their prompts ("Locked", "Not your garden") match the server, which checks again. Read from
    /// the books once for everyone when they change; a player is sent their summary when it changed for them (and once
    /// when they join).</item>
    /// <item><b>Doors</b>: the open or closed state of every <see cref="LockableDoor"/>, for every client - shown again
    /// whenever a scene loads on the client (a late joiner, a player back in the City).</item>
    /// </list>
    /// Offline it does nothing (the housing office runs on the machine).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetHousing : NetworkBehaviour
    {
        public static NetHousing Instance { get; private set; }

        private const int MaxRequestLength = 16 * 1024;
        private const byte InnViewData = 0, RealtorViewData = 1, ReplyData = 2;

        [Tooltip("Seconds a client's requests must be apart on average (a few may come at once).")]
        [SerializeField, Range(0.02f, 2f)] private float requestInterval = 0.2f;
        [Tooltip("Seconds between two checks of what each player may access (changes are sent at once besides).")]
        [SerializeField, Range(0.5f, 30f)] private float accessCheckInterval = 2f;

        /// <summary>Every lockable door's state, by id (doors under a NetInteractable replicate themselves too).</summary>
        private readonly SyncDictionary<string, bool> doors = new SyncDictionary<string, bool>();

        // Server (by client id; forgotten when the client leaves)
        private readonly Dictionary<int, (float tokens, float at)> tokens = new Dictionary<int, (float, float)>();
        private readonly Dictionary<int, float> nextRefusal = new Dictionary<int, float>();
        /// <summary>What each client was last sent of its access (see <see cref="SendAccess"/>).</summary>
        private readonly Dictionary<int, SentAccess> sentAccess = new Dictionary<int, SentAccess>();
        private readonly AccessIndex accessIndex = new AccessIndex();
        private NetSession session;
        /// <summary>Clients with a housing window open: whether it shows their own housing, and (own) until when it counts as open.</summary>
        private readonly Dictionary<int, (bool own, float until)> watching = new Dictionary<int, (bool, float)>();
        /// <summary>Real seconds an own-housing window counts as open after its last request (it asks every 20 s while open).</summary>
        private const float OwnWatchSeconds = 45f;
        private bool booksChanged;
        private float nextAccessCheck, nextViews;
        private int transfer;

        // Client
        private readonly NetPayloadAssembler parts = new NetPayloadAssembler();

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[Net] More than one NetHousing: keep one, on the Network World object.", this);
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
            HousingDirector.BooksChanged += OnBooksChanged;
            HousingDirector.AccessChanged += OnAccessChanged;
            LockableDoor.AnyStateChanged += OnDoor;
            session = NetSession.Instance;
            if (session != null) session.ClientLeft += OnClientLeft;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            SeedDoors();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HousingDirector.BooksChanged -= OnBooksChanged;
            HousingDirector.AccessChanged -= OnAccessChanged;
            LockableDoor.AnyStateChanged -= OnDoor;
            if (session != null) session.ClientLeft -= OnClientLeft;
            session = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            tokens.Clear();
            nextRefusal.Clear();
            sentAccess.Clear();
            watching.Clear();
            accessIndex.Clear();
            if (Instance == this) Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return; // the host runs the office itself
            Instance = this;
            HousingClient.Remote = SendRequest;
            doors.OnChange += OnDoorsChanged;
            // The doors of a scene that loads later (the City after the start scenes, again after a dungeon) are shown then.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            ShowDoors();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized) return;
            doors.OnChange -= OnDoorsChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (HousingClient.Remote == (Action<HousingRequest>)SendRequest) HousingClient.Remote = null;
            HousingGate.ClearLocalAccess();
            parts.Reset();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            HousingDirector.BooksChanged -= OnBooksChanged;
            HousingDirector.AccessChanged -= OnAccessChanged;
            LockableDoor.AnyStateChanged -= OnDoor;
            doors.OnChange -= OnDoorsChanged;
            if (session != null) session.ClientLeft -= OnClientLeft;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        /// <summary>Server: what was kept for a client goes with it (a later client may be given the same id).</summary>
        private void OnClientLeft(NetworkConnection conn)
        {
            if (conn == null) return;
            int id = conn.ClientId;
            tokens.Remove(id);
            nextRefusal.Remove(id);
            sentAccess.Remove(id);
            watching.Remove(id);
        }

        // ------------------------------------------------------------------ doors
        private void OnDoor(LockableDoor d)
        {
            if (IsServerInitialized && d != null) SetDoor(d.DoorId, d.IsOpen);
        }

        private void SetDoor(string id, bool open)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!doors.TryGetValue(id, out bool was) || was != open) doors[id] = open;
        }

        /// <summary>Server: every loaded door is listed with its state.</summary>
        private void SeedDoors()
        {
            foreach (LockableDoor d in LockableDoor.All)
                if (d != null) SetDoor(d.DoorId, d.IsOpen);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (IsServerInitialized) SeedDoors(); // the scene's doors, as they start
            else if (IsClientInitialized) ShowDoors(); // the server's state of the doors that just loaded here
        }

        /// <summary>Server: doors that are gone (their scene unloaded) are no longer listed - an old state is never shown again.</summary>
        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (!IsServerInitialized) return;
            List<string> gone = null;
            foreach (KeyValuePair<string, bool> kv in doors)
                if (LockableDoor.Find(kv.Key) == null) (gone ??= new List<string>()).Add(kv.Key);
            if (gone != null)
                foreach (string id in gone) doors.Remove(id);
        }

        private void ShowDoors()
        {
            foreach (KeyValuePair<string, bool> kv in doors) ShowDoor(kv.Key, kv.Value);
        }

        private void OnDoorsChanged(SyncDictionaryOperation op, string key, bool value, bool asServer)
        {
            if (asServer || IsServerInitialized) return;
            if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set) ShowDoor(key, value);
        }

        private static void ShowDoor(string id, bool open)
        {
            LockableDoor d = LockableDoor.Find(id);
            if (d != null) d.Show(open);
        }

        // ------------------------------------------------------------------ client: requests
        private void SendRequest(HousingRequest r)
        {
            if (!IsClientInitialized || IsServerInitialized || r == null) return;
            string json = JsonUtility.ToJson(r);
            if (json.Length > MaxRequestLength)
            {
                HousingReply refused = HousingReply.Fail("That is too much to ask at once.");
                refused.seq = r.seq;
                HousingClient.ReceiveReply(refused);
                return;
            }
            ServerRequest(json);
        }

        // ------------------------------------------------------------------ server: requests
        private bool TakeToken(NetworkConnection conn)
        {
            float now = Time.unscaledTime;
            const float burst = 6f;
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

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequest(string json, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || conn.IsLocalClient) return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null) return;
            if (string.IsNullOrEmpty(json) || json.Length > MaxRequestLength) return;
            HousingRequest r;
            try { r = JsonUtility.FromJson<HousingRequest>(json); }
            catch (Exception) { return; }
            if (r == null) return;
            if (!TakeToken(conn))
            {
                // A window sends one request at a time: only a flood gets here. Answered once a second at most.
                float now = Time.unscaledTime;
                if (!nextRefusal.TryGetValue(conn.ClientId, out float at) || now >= at)
                {
                    nextRefusal[conn.ClientId] = now + 1f;
                    SendReply(conn, new HousingReply { seq = r.seq, ok = false, message = "Not so fast." });
                }
                return;
            }
            Clip(r);
            // Where the player stands is the server's own: their open session with an innkeeper or a realtor in range.
            HousingDesk desk = HousingDesks.Of(player);
            HousingReply reply = HousingService.Handle(player, r, desk);
            SendReply(conn, reply);
            SendView(conn, player, desk, r.own);
            watching[conn.ClientId] = (r.own, Time.unscaledTime + OwnWatchSeconds);
            SendAccess(conn, player); // what the request changed for them
        }

        /// <summary>Bounds every text and list of a request (the rules check everything else).</summary>
        private static void Clip(HousingRequest r)
        {
            r.requestId = Cut(r.requestId, 64);
            r.placeId = Cut(r.placeId, 64);
            r.roomId = Cut(r.roomId, 64);
            r.fingerprint = Cut(r.fingerprint, 16);
            r.houses ??= new List<string>();
            r.terms ??= new List<int>();
            r.offersMilli ??= new List<long>();
            for (int i = 0; i < r.houses.Count; i++) r.houses[i] = Cut(r.houses[i], 64);
            if (float.IsNaN(r.protection) || float.IsInfinity(r.protection)) r.protection = 0f;
            r.nights = Mathf.Clamp(r.nights, 0, 10000);
        }

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length > max ? s.Substring(0, max) : s;

        // ------------------------------------------------------------------ server: answers and views
        private void SendReply(NetworkConnection conn, HousingReply reply) => SendText(conn, ReplyData, JsonUtility.ToJson(reply));

        /// <summary>The window's view: the player's own housing (<paramref name="own"/>), else their desk's.</summary>
        private void SendView(NetworkConnection conn, CombatEntity player, HousingDesk desk, bool own)
        {
            if (own) SendText(conn, RealtorViewData, JsonUtility.ToJson(HousingService.RealtorViewFor(player, null)));
            else if (desk.inn != null) SendText(conn, InnViewData, JsonUtility.ToJson(HousingService.InnViewFor(player, desk.inn)));
            else SendText(conn, RealtorViewData, JsonUtility.ToJson(HousingService.RealtorViewFor(player, desk.realtor)));
        }

        private void SendText(NetworkConnection conn, byte kind, string text)
        {
            if (conn == null || !conn.IsActive) return;
            List<byte[]> list = NetPayload.Split(NetPayload.Pack(text ?? ""));
            int id = ++transfer;
            for (int i = 0; i < list.Count; i++) TargetData(conn, kind, id, (short)i, (short)list.Count, list[i]);
        }

        private void OnBooksChanged()
        {
            if (IsServerInitialized) booksChanged = true;
        }

        private void OnAccessChanged(string key)
        {
            if (!IsServerInitialized) return;
            CombatEntity p = HousingDirector.Online(key);
            NetworkConnection conn = p != null ? NetObjects.OwnerOf(p) : null;
            if (conn != null && conn.IsActive && !conn.IsLocalClient) SendAccess(conn, p);
        }

        private void Update()
        {
            if (!IsServerInitialized) return;
            float now = Time.unscaledTime;
            bool sweep = now >= nextAccessCheck;
            if (booksChanged && now >= nextViews)
            {
                booksChanged = false;
                nextViews = now + 0.5f;
                RefreshWatchers();
                sweep = true; // what the change did to everyone's access goes out with it
            }
            if (sweep)
            {
                // Cheap for every player whose access did not change: nothing is built for them.
                nextAccessCheck = now + accessCheckInterval;
                foreach (NetworkConnection c in Connections())
                {
                    CombatEntity p = NetObjects.PlayerOf(c);
                    if (p != null) SendAccess(c, p);
                }
            }
        }

        /// <summary>Open housing windows are refreshed (the books changed: a clearing, a neighbour's renting, a payout).</summary>
        private void RefreshWatchers()
        {
            if (watching.Count == 0) return;
            float now = Time.unscaledTime;
            var gone = new List<int>();
            var live = new HashSet<int>();
            foreach (NetworkConnection c in Connections())
            {
                if (!watching.TryGetValue(c.ClientId, out (bool own, float until) w)) continue;
                live.Add(c.ClientId);
                CombatEntity p = NetObjects.PlayerOf(c);
                HousingDesk desk = p != null && !w.own ? HousingDesks.Of(p) : default;
                // A desk's window lasts while its session does; the player's own window while it keeps asking.
                if (p == null || (w.own ? now > w.until : !desk.AtDesk))
                {
                    gone.Add(c.ClientId);
                    continue;
                }
                SendView(c, p, desk, w.own);
            }
            foreach (int id in watching.Keys)
                if (!live.Contains(id)) gone.Add(id); // the connection went away
            foreach (int id in gone) watching.Remove(id);
        }

        private static IEnumerable<NetworkConnection> Connections()
        {
            NetSession session = NetSession.Instance;
            if (session == null) yield break;
            foreach (NetworkConnection c in session.Clients)
                if (c != null && c.IsActive && !c.IsLocalClient) yield return c;
        }

        /// <summary>What a client was last sent of its access.</summary>
        private struct SentAccess
        {
            /// <summary>The <see cref="AccessIndex.Build"/> and the key it was made from.</summary>
            public int build;
            public string key;
            /// <summary>Its places and what it may do there, as text; and the <see cref="AccessIndex.VacantBuild"/> it got.</summary>
            public string places;
            public int vacantBuild;
        }

        /// <summary>
        /// What a player may access (their places and those they were let into, with what they may do) and the houses nobody
        /// rents - sent when it changed for them, and always to a client that has not had it yet. A player whose key and
        /// whose books' reading did not change since their last summary costs nothing; otherwise only their own places are
        /// looked at (the books are read once for everyone, <see cref="AccessIndex"/>).
        /// </summary>
        private void SendAccess(NetworkConnection conn, CombatEntity player)
        {
            HousingBook book = HousingDirector.Book;
            if (book == null || conn == null) return;
            accessIndex.Refresh(book, HousingDirector.Now);
            string key = HousingDirector.KeyOf(player);
            bool known = sentAccess.TryGetValue(conn.ClientId, out SentAccess last);
            if (known && last.build == accessIndex.Build && last.key == key) return;
            List<KeyValuePair<string, int>> mine = accessIndex.Of(key);
            var sig = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in mine) sig.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            var sent = new SentAccess { build = accessIndex.Build, key = key, places = sig.ToString(), vacantBuild = accessIndex.VacantBuild };
            if (sentAccess.Count > 4096) sentAccess.Clear(); // (clients that left are forgotten anyway)
            sentAccess[conn.ClientId] = sent;
            if (known && last.places == sent.places && last.vacantBuild == sent.vacantBuild) return;
            var places = new string[mine.Count];
            var access = new int[mine.Count];
            for (int i = 0; i < mine.Count; i++)
            {
                places[i] = mine[i].Key;
                access[i] = mine[i].Value;
            }
            TargetAccess(conn, places, access, accessIndex.Vacant);
        }

        /// <summary>
        /// Server: who may go where, read from the books once for every player - each key's places (in the order of the
        /// registered places, with what they may do there: everything for the tenant, what they were let do for someone
        /// let in, as <see cref="HousingBook.AccessOf"/> answers) and the houses nobody rents (houses only: a free inn room
        /// stays locked for everyone). Read again only when the books change (their revision), the registered places
        /// change, or a tenancy reaches its end.
        /// </summary>
        private sealed class AccessIndex
        {
            private static readonly List<KeyValuePair<string, int>> Nothing = new List<KeyValuePair<string, int>>();

            private readonly Dictionary<string, List<KeyValuePair<string, int>>> byKey = new Dictionary<string, List<KeyValuePair<string, int>>>();
            /// <summary>The registered properties, inns and rooms it was read for, in order.</summary>
            private readonly List<UnityEngine.Object> places = new List<UnityEngine.Object>();
            private HousingBook book;
            private int revision;
            private double until = double.NegativeInfinity;
            private int placesCheckedFrame = -1;
            private string vacantText;

            /// <summary>Changes every time it is read again from the books.</summary>
            public int Build { get; private set; }

            /// <summary>Changes every time the houses nobody rents change.</summary>
            public int VacantBuild { get; private set; }

            /// <summary>The houses nobody rents (shared: never changed once given out).</summary>
            public string[] Vacant { get; private set; } = new string[0];

            /// <summary>A key's places and what it may do there (never null; empty for no key).</summary>
            public List<KeyValuePair<string, int>> Of(string key) =>
                !string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out List<KeyValuePair<string, int>> list) ? list : Nothing;

            public void Clear()
            {
                book = null;
                byKey.Clear();
                places.Clear();
                until = double.NegativeInfinity;
            }

            public void Refresh(HousingBook b, double now)
            {
                if (b == null) return;
                bool same = b == book && b.Revision == revision && now < until;
                // The registered places are compared once a frame at most (a reference each).
                if (same && placesCheckedFrame != Time.frameCount)
                {
                    placesCheckedFrame = Time.frameCount;
                    same = SamePlaces();
                }
                if (!same) Read(b, now);
            }

            private bool SamePlaces()
            {
                int i = 0;
                foreach (Property p in HousingDirector.Properties)
                    if (i >= places.Count || !ReferenceEquals(places[i++], p)) return false;
                foreach (Inn inn in HousingDirector.Inns)
                {
                    if (i >= places.Count || !ReferenceEquals(places[i++], inn)) return false;
                    if (inn == null) continue;
                    foreach (InnRoom r in inn.rooms)
                        if (i >= places.Count || !ReferenceEquals(places[i++], r)) return false;
                }
                return i == places.Count;
            }

            private void Read(HousingBook b, double now)
            {
                book = b;
                revision = b.Revision;
                until = double.PositiveInfinity;
                placesCheckedFrame = Time.frameCount;
                byKey.Clear();
                places.Clear();
                var vacant = new List<string>();
                foreach (Property p in HousingDirector.Properties)
                {
                    places.Add(p);
                    if (p == null || p.OutOfOrder) continue; // a duplicate: nobody may use it, and it is not offered
                    Tenancy t = b.TenancyOf(p.PropertyId, null, now);
                    if (t == null) vacant.Add(p.PropertyId);
                    else Add(t, p.PropertyId);
                }
                foreach (Inn inn in HousingDirector.Inns)
                {
                    places.Add(inn);
                    if (inn == null) continue;
                    foreach (InnRoom r in inn.rooms)
                    {
                        places.Add(r);
                        if (r == null || inn.OutOfOrder || r.OutOfOrder) continue;
                        Tenancy t = b.TenancyOf(inn.InnId, r.RoomId, now);
                        if (t != null) Add(t, HousingGate.KeyOf(inn.InnId, r.RoomId));
                    }
                }
                Build++;
                string text = string.Join(";", vacant);
                if (text != vacantText)
                {
                    vacantText = text;
                    Vacant = vacant.ToArray();
                    VacantBuild++;
                }
            }

            private void Add(Tenancy t, string place)
            {
                if (t.end < until) until = t.end; // it ends by itself then: read again
                Put(t.key, place, HousingAccess.All);
                List<HousingGrant> grants = t.grants;
                if (grants == null) return;
                for (int i = 0; i < grants.Count; i++)
                {
                    HousingGrant g = grants[i];
                    if (g == null || string.IsNullOrEmpty(g.key) || g.key == t.key) continue;
                    bool earlier = false; // a key's first grant is the one that counts
                    for (int j = 0; j < i && !earlier; j++)
                        earlier = grants[j] != null && grants[j].key == g.key;
                    if (!earlier) Put(g.key, place, g.Access);
                }
            }

            private void Put(string key, string place, HousingAccess access)
            {
                if (string.IsNullOrEmpty(key) || access == HousingAccess.None) return;
                if (!byKey.TryGetValue(key, out List<KeyValuePair<string, int>> list))
                    byKey[key] = list = new List<KeyValuePair<string, int>>();
                list.Add(new KeyValuePair<string, int>(place, (int)access));
            }
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
                    case InnViewData: HousingClient.ReceiveInnView(JsonUtility.FromJson<InnView>(json)); break;
                    case RealtorViewData: HousingClient.ReceiveRealtorView(JsonUtility.FromJson<RealtorView>(json)); break;
                    case ReplyData: HousingClient.ReceiveReply(JsonUtility.FromJson<HousingReply>(json)); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] A broken housing message ({e.Message}).");
            }
        }

        [TargetRpc]
        private void TargetAccess(NetworkConnection conn, string[] places, int[] access, string[] vacant)
        {
            if (IsServerInitialized) return;
            if (places == null || access == null || places.Length > 4096 || access.Length != places.Length || (vacant != null && vacant.Length > 8192)) return;
            HousingGate.SetLocalAccess(places, access, vacant);
        }
    }
}
#endif
