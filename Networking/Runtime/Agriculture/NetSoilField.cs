#if FISHNET
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Makes a <see cref="SoilField"/> part of a network session, with the server deciding everything. Put it on the field
    /// next to a NetworkObject (Tools ▸ SimpleMovements ▸ Agriculture ▸ Make Selected Field Networked); a field in the
    /// city scene is a scene object, spawned with the scene.
    /// <list type="bullet">
    /// <item><b>The server keeps the field</b>: the soil (every hidden number of it), the plants, their genes, the weather
    /// history. Clients get a <b>picture</b> of it - the soil as it looks (type, wetness, working, what lies on it, holes)
    /// and every plant as it looks (stage, size, health, symptoms, pests, fruit) - each part after it changed (at most
    /// every <see cref="sendInterval"/> seconds: a plant that grew does not resend the soil), both now and then to set
    /// their clocks right. Nothing hidden ever leaves the server, so no client can read a soil's chemistry or a seed's
    /// genes it has not measured.</item>
    /// <item><b>Only what changed</b>: each watcher gets the whole picture once, then only what changed since the picture
    /// it holds (see <see cref="SoilPictureDelta"/>) - plants added, gone or changed, the stretches of soil that changed -
    /// so a garden that wakes and catches up on hours of weather sends what grew, not everything again. The whole picture
    /// goes again only to a client that has none, holds one the server no longer keeps, or when the changes would be no
    /// smaller. A client keeps the picture of the last few gardens it walked away from (packed) and the server the last one
    /// of a garden nobody watches, so coming back to a garden that slept costs what grew meanwhile. Every change names the
    /// picture it applies to and the checksum of the result: a client that missed one, or would not end exactly with the
    /// server's picture, keeps what it has and asks again.</item>
    /// <item><b>Only to those near it</b>: a client watches a garden while its player is within the garden's Draw
    /// Distance (plus a margin) and forgets it - picture, mesh and all - when it walks away; the server sends pictures
    /// only to watchers, and none while the garden sleeps (nobody near it: see <see cref="AgriSettings.sleepWhenAlone"/>).
    /// A big city with hundreds of gardens costs each client only the few around it.</item>
    /// <item><b>Working the field</b> is an interaction (relayed by the player's NetInteractor): the server opens the field
    /// window on the player's screen and sends their skills. Every action asks the server, which checks the request (a
    /// known action, a spot on the field, a plant within reach, a field players may work, not too many too fast), does
    /// it with the player's own items, tools and skills, and always answers - with what they see.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SoilField))]
    public class NetSoilField : NetworkBehaviour, ISoilFieldNet
    {
        /// <summary>Longest message sent to a client.</summary>
        public const int MaxMessageLength = 200;
        /// <summary>Largest field picture a client takes (bytes, unpacked).</summary>
        public const int MaxSnapshotSize = 8 * 1024 * 1024;
        private const int MaxTextLength = 120;
        private const byte SnapshotData = 0, ResultData = 1, SkillsData = 2, DeltaData = 3;
        /// <summary>
        /// Pictures the server keeps besides the last one: a client that missed a change says which it holds and gets the
        /// rest from there; one that holds an older picture gets the whole again.
        /// </summary>
        private const int RecentPictures = 4;
        /// <summary>Changes packed smaller than this are sent without weighing them against the whole picture.</summary>
        private const int SmallDelta = 256;
        /// <summary>Seconds between a client's asks while it waits for what it missed.</summary>
        private const float AskBehindEvery = 2f;
        /// <summary>Gardens a client walked away from whose last picture it keeps (packed, about as big as one whole picture sent).</summary>
        private const int KeptPictures = 16;
        /// <summary>Requests a player may send at once before they must wait (an action and the look that follows it, a few clicks).</summary>
        private const float RequestBurst = 8f;
        /// <summary>Metres beyond the Draw Distance within which a client watches the garden, and further beyond which it forgets it.</summary>
        private const float WatchMargin = 10f, ForgetMargin = 30f;
        /// <summary>The server drops a watcher this far beyond where the client should have forgotten (it lies, or it lags).</summary>
        private const float WatcherSlack = 60f;
        private const int MaxWatchers = 512;

        [Tooltip("Least seconds between two pictures of the field sent to the clients (changes in between go together).")]
        [SerializeField, Range(0.1f, 5f)] private float sendInterval = 0.5f;
        [Tooltip("Seconds between pictures sent even when nothing changed (clocks, late joiners).")]
        [SerializeField, Range(5f, 120f)] private float refreshInterval = 30f;
        [Tooltip("Seconds a player's requests must be apart on average (a few may come at once).")]
        [SerializeField, Range(0f, 1f)] private float requestInterval = 0.08f;

        private SoilField soilField;

        // Server
        private bool dirty, wasSimulated;
        private float nextSend, nextRefresh, nextPrune;
        private int transfer, sentSoil = int.MinValue, sentPlants = int.MinValue;
        private readonly Dictionary<int, (float tokens, float at)> requestTokens = new Dictionary<int, (float, float)>();
        private readonly Dictionary<int, float> nextWatch = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextRefusal = new Dictionary<int, float>();
        /// <summary>
        /// The clients watching the garden (their player is near it), and those still owed a whole picture - by identity: a
        /// connection that ended compares unequal even to itself (its id is gone), so it could never be taken out otherwise.
        /// </summary>
        private readonly HashSet<NetworkConnection> watchers = new HashSet<NetworkConnection>(ReferenceComparer<NetworkConnection>.Instance);
        private readonly HashSet<NetworkConnection> owed = new HashSet<NetworkConnection>(ReferenceComparer<NetworkConnection>.Instance);
        /// <summary>Watchers owed what changed since the picture they hold, at once (they asked, or opened the window).</summary>
        private readonly HashSet<NetworkConnection> due = new HashSet<NetworkConnection>(ReferenceComparer<NetworkConnection>.Instance);
        /// <summary>The picture each watcher holds (what the server last sent it): its next changes are counted from there.</summary>
        private readonly Dictionary<NetworkConnection, SoilPicture> held = new Dictionary<NetworkConnection, SoilPicture>(ReferenceComparer<NetworkConnection>.Instance);
        /// <summary>The field's picture as last made (null: none, or the field's pictures cannot be cut up - then sent whole).</summary>
        private SoilPicture shown;
        private readonly List<SoilPicture> recent = new List<SoilPicture>();
        /// <summary>The changes of one send, by the picture they start from (watchers holding the same picture share them).</summary>
        private readonly Dictionary<SoilPicture, byte[]> deltas = new Dictionary<SoilPicture, byte[]>();
        private int revision, wholeSize = -1;
        private SoilPicture wholeSizeOf;
        /// <summary>
        /// The last picture shown, packed, kept once nobody watches (a sleeping garden): a client that comes back holding it
        /// gets only what changed since - what grew while the garden slept - not the whole again.
        /// </summary>
        private byte[] parked;
        private int parkedRevision;
        private ulong parkedHash;
        /// <summary>The field's pictures came out other than this side cuts them up: everything is sent whole (as it was before changes were sent).</summary>
        private bool wholeOnly, formatChecked;
        private NetSession session;
        private readonly List<NetworkConnection> scratch = new List<NetworkConnection>();

        // Clients
        private readonly NetPayloadAssembler targetParts = new NetPayloadAssembler();
        private bool openWhenReady, haveSnapshot, watching;
        /// <summary>The server's picture this client holds (what its changes apply to); null when it holds none it can build on.</summary>
        private SoilPicture mirror;
        /// <summary>A change came that does not fit what this client holds: it asks for what it missed until it comes.</summary>
        private bool behind;
        /// <summary>
        /// The picture this client held when it walked away, packed (the field itself, its mesh and memory, are freed): it
        /// says so when it comes back, and gets only what changed since. Kept for the last few gardens only.
        /// </summary>
        private byte[] kept;
        private static readonly List<NetSoilField> keeping = new List<NetSoilField>();
        private float askAgainAt, nextLook;

        private SoilField Field => soilField != null ? soilField : (soilField = GetComponent<SoilField>());

        public bool RunsRules => IsServerInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => keeping.Clear();

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Field.Net = this;
            dirty = true;
            session = NetSession.Instance;
            if (session != null) session.ClientLeft += OnClientLeft;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (session != null) session.ClientLeft -= OnClientLeft;
            session = null;
            if (soilField != null && ReferenceEquals(soilField.Net, this)) soilField.Net = null;
            requestTokens.Clear();
            nextWatch.Clear();
            nextRefusal.Clear();
            watchers.Clear();
            owed.Clear();
            due.Clear();
            DropPictures();
            parked = null;
            wholeOnly = formatChecked = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return; // the host runs the rules itself
            Field.Net = this;
            haveSnapshot = false;
            watching = false;
            mirror = null;
            behind = false;
            // Nothing is drawn until the server's picture comes, and only while the player is near.
            Field.ForgetPicture();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized || soilField == null) return;
            FarmWindow.Close(soilField);
            if (ReferenceEquals(soilField.Net, this)) soilField.Net = null;
            // The server's picture goes: the field is this machine's own again (its save, once the rules run here).
            soilField.ClearMirror();
            openWhenReady = false;
            haveSnapshot = false;
            watching = false;
            mirror = null;
            behind = false;
            Unkeep();
        }

        /// <summary>Server: a client left - it watches no more (told while its id is still its own).</summary>
        private void OnClientLeft(NetworkConnection conn)
        {
            if (conn == null) return;
            Unwatch(conn);
            requestTokens.Remove(conn.ClientId);
            nextWatch.Remove(conn.ClientId);
            nextRefusal.Remove(conn.ClientId);
        }

        // ------------------------------------------------------------------ server: the field for everyone
        void ISoilFieldNet.Changed()
        {
            if (IsServerInitialized) dirty = true;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (IsServerInitialized)
            {
                if (now >= nextPrune)
                {
                    nextPrune = now + 1f;
                    PruneWatchers();
                }
                // A host playing alone (or nobody near) sends nothing: the field and its window are its own. The pictures
                // kept for watchers go too, all but the last one, packed (parked): a client that comes back to the garden
                // - after it slept, above all - holding that one gets only what changed since.
                if (watchers.Count == 0)
                {
                    dirty = false;
                    if (shown != null || recent.Count > 0)
                    {
                        Park();
                        DropPictures();
                    }
                }
                // Asleep or being brought up to date: nothing to show yet (nobody is near a sleeping garden; those
                // watching a catch-up keep what they hold).
                if (!Field.Simulated)
                {
                    wasSimulated = false;
                    return;
                }
                if (!wasSimulated)
                {
                    // Awake again, or caught up: both parts are looked at afresh (the field may have been read again from
                    // its save), and each watcher gets what changed since the picture it holds - not the whole again.
                    wasSimulated = true;
                    sentSoil = sentPlants = int.MinValue;
                    dirty = true;
                    nextSend = 0f;
                }
                if (watchers.Count > 0) Publish(now);
                return;
            }
            if (IsClientInitialized && now >= nextLook)
            {
                nextLook = now + 0.5f;
                LookAround(now);
            }
        }

        /// <summary>
        /// Server: what the watchers are owed - what changed, at most every <see cref="sendInterval"/>; something to
        /// everyone every <see cref="refreshInterval"/> (their clocks, and the checksum that tells a client its copy is
        /// still the server's); and at once, to those owed a whole picture or their changes. Each watcher gets the changes
        /// since the picture it holds (watchers holding the same share them), or the whole picture when it holds none or the
        /// changes would be no smaller.
        /// </summary>
        private void Publish(float now)
        {
            bool refresh = now >= nextRefresh;
            bool timed = refresh || dirty && now >= nextSend;
            if (!timed && owed.Count == 0 && due.Count == 0) return;
            if (timed)
            {
                dirty = false;
                nextSend = now + sendInterval;
                if (refresh) nextRefresh = now + refreshInterval;
            }
            bool changed = TakePicture(refresh);
            // A picture made for one watcher is news to the others too (at the next send).
            if (changed && !timed) dirty = true;
            double clock = FarmClock.Now;
            byte[] whole = null;
            deltas.Clear();
            scratch.Clear();
            scratch.AddRange(watchers);
            foreach (NetworkConnection c in scratch)
            {
                if (!c.IsActive) continue;
                bool owesWhole = owed.Contains(c), owesNow = owesWhole || due.Contains(c);
                if (!owesNow && !timed) continue;
                held.TryGetValue(c, out SoilPicture has);
                // Holds it all already: its clock waits for the refresh.
                if (!owesNow && !refresh && (wholeOnly ? !changed : has == shown)) continue;
                byte kind = SnapshotData;
                byte[] data = null;
                if (!owesWhole && has != null && shown != null)
                {
                    data = DeltaFor(has, clock, ref whole);
                    if (data != null) kind = DeltaData;
                }
                if (data == null) data = whole ??= PackWhole(clock);
                SendBytes(kind, data, c);
                if (shown != null) held[c] = shown;
                else held.Remove(c);
            }
            owed.Clear();
            due.Clear();
            deltas.Clear();
        }

        /// <summary>
        /// Server: brings <see cref="shown"/> up to date - only the parts whose version moved are asked of the field (both
        /// with <paramref name="everything"/>: some of the soil's look changes without a version, as it dries). True when
        /// it changed (a new revision).
        /// </summary>
        private bool TakePicture(bool everything)
        {
            FieldState st = Field.State;
            bool soil = everything || shown == null || st.Grid.Version != sentSoil;
            bool plants = everything || shown == null || st.PlantVersion != sentPlants;
            if (!soil && !plants) return false;
            sentSoil = st.Grid.Version;
            sentPlants = st.PlantVersion;
            if (wholeOnly) return true;
            SoilPicture next = SoilPicture.Make(soil ? Field.ExportVisual(true, false) : null, plants ? Field.ExportVisual(false, true) : null, shown, revision + 1);
            // Another shape (the field was made again): a picture afresh, from which only whole pictures go out.
            if (next == null && shown != null)
                next = SoilPicture.Make(Field.ExportVisual(true, false), Field.ExportVisual(false, true), null, revision + 1);
            if (next != null && !formatChecked)
            {
                // Once: the picture put back together must be the field's own, byte for byte - else this side does not
                // know how the field writes its pictures any more, and sends them whole as they come.
                formatChecked = true;
                if (!next.Matches(Field.ExportVisual())) next = null;
            }
            if (next == null)
            {
                Debug.LogWarning($"Agriculture: the pictures of field '{Field.DisplayName}' cannot be cut into changes - they are sent whole.", this);
                wholeOnly = true;
                DropPictures();
                return true;
            }
            if (ReferenceEquals(next, shown)) return false;
            revision = next.Revision;
            shown = next;
            recent.Add(next);
            if (recent.Count > RecentPictures) recent.RemoveAt(0);
            return true;
        }

        /// <summary>The changes from <paramref name="has"/> to <see cref="shown"/>, packed; null when the whole picture is no bigger.</summary>
        private byte[] DeltaFor(SoilPicture has, double clock, ref byte[] whole)
        {
            if (deltas.TryGetValue(has, out byte[] packed)) return packed;
            byte[] raw = SoilPictureDelta.Make(has, shown, clock);
            // Packed harder than whole pictures: they are small, made once for every watcher holding the same picture,
            // and mostly runs of unchanged bytes, which the harder packing finds much better.
            packed = raw != null ? Deflate(raw, System.IO.Compression.CompressionLevel.Optimal) : null;
            if (packed != null && packed.Length > SmallDelta)
            {
                // Weighed against the whole picture. Its packed size changes slowly, so it is packed again only when
                // the call is close (or never was): the changes then cost less to make than the whole did before.
                if (wholeSizeOf == null || !ReferenceEquals(wholeSizeOf, shown) && packed.Length * 4 >= wholeSize * 3)
                {
                    whole ??= PackWhole(clock);
                    wholeSize = whole.Length;
                    wholeSizeOf = shown;
                }
                if (packed.Length >= wholeSize) packed = null;
            }
            deltas[has] = packed;
            return packed;
        }

        /// <summary>
        /// The whole picture, packed: the revision it is and the length of its soil (so a client can cut it up as the
        /// server does), then the picture itself. Revision 0: the field's own picture, nothing to build on.
        /// </summary>
        private byte[] PackWhole(double clock)
        {
            if (shown != null && !wholeOnly) return Pack(shown, clock);
            byte[] picture = Field.ExportVisual();
            var data = new byte[picture.Length + 8];
            SoilPicture.WriteInt32(data, 4, -1);
            Buffer.BlockCopy(picture, 0, data, 8, picture.Length);
            return Deflate(data);
        }

        /// <summary>Server: forgets the pictures kept for watchers (what they hold too: they get the whole next).</summary>
        private void DropPictures()
        {
            shown = null;
            recent.Clear();
            held.Clear();
            deltas.Clear();
            wholeSizeOf = null;
            sentSoil = sentPlants = int.MinValue;
        }

        /// <summary>Server: the last picture shown, packed, once nobody watches (one per garden, the size of a whole picture sent).</summary>
        private void Park()
        {
            if (shown == null || wholeOnly) return;
            parked = PackWhole(FarmClock.Now);
            parkedRevision = shown.Revision;
            parkedHash = shown.Hash;
        }

        /// <summary>Server: a picture kept that a client says it holds (its revision and checksum), else null.</summary>
        private SoilPicture Known(int holds, ulong hash)
        {
            if (holds == 0 || wholeOnly) return null;
            for (int i = recent.Count - 1; i >= 0; i--)
                if (recent[i].Revision == holds && recent[i].Hash == hash)
                    return recent[i];
            if (parked == null || holds != parkedRevision || hash != parkedHash) return null;
            SoilPicture back = Unpack(parked);
            if (back == null || back.Hash != hash) return null;
            recent.Add(back);
            if (recent.Count > RecentPictures) recent.RemoveAt(0);
            return back;
        }

        /// <summary>A whole picture as <see cref="PackWhole"/> made it, cut up again (null: not one).</summary>
        private static SoilPicture Unpack(byte[] packed)
        {
            byte[] data = Inflate(packed, MaxSnapshotSize + 8);
            if (data == null || data.Length < 8) return null;
            int rev = SoilPicture.ReadInt32(data, 0), soilLength = SoilPicture.ReadInt32(data, 4);
            if (rev == 0) return null;
            var picture = new byte[data.Length - 8];
            Buffer.BlockCopy(data, 8, picture, 0, picture.Length);
            return SoilPicture.FromWhole(picture, soilLength, rev);
        }

        /// <summary>Packs a picture as a whole one is sent (its revision, its soil's length, the picture).</summary>
        private static byte[] Pack(SoilPicture p, double clock)
        {
            byte[] picture = p.ToVisual(clock, true, true);
            using (var ms = new MemoryStream(picture.Length / 3 + 16))
            {
                using (var z = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    var head = new byte[8];
                    SoilPicture.WriteInt32(head, 0, p.Revision);
                    SoilPicture.WriteInt32(head, 4, p.Soil.Length);
                    z.Write(head, 0, head.Length);
                    z.Write(picture, 0, picture.Length);
                }
                return ms.ToArray();
            }
        }

        private void Unwatch(NetworkConnection c)
        {
            watchers.Remove(c);
            owed.Remove(c);
            due.Remove(c);
            held.Remove(c);
        }

        /// <summary>Server: watchers that left, went to another scene or are far beyond the garden are dropped.</summary>
        private void PruneWatchers()
        {
            if (watchers.Count == 0) return;
            scratch.Clear();
            float limit = Field.drawDistance + WatchMargin + ForgetMargin + WatcherSlack;
            foreach (NetworkConnection c in watchers)
            {
                CombatEntity p = c.IsActive ? NetObjects.PlayerOf(c) : null;
                if (p == null || p.gameObject.scene != Field.gameObject.scene || Field.DistanceFromOutline(p.transform.position) > limit)
                    scratch.Add(c);
            }
            foreach (NetworkConnection c in scratch) Unwatch(c);
        }

        /// <summary>Sends bytes to one client, in parts.</summary>
        private void SendBytes(byte kind, byte[] data, NetworkConnection to)
        {
            if (to == null) return;
            List<byte[]> parts = NetPayload.Split(data ?? Array.Empty<byte>());
            int id = ++transfer;
            for (int i = 0; i < parts.Count; i++)
                TargetData(to, kind, id, (short)i, (short)parts.Count, parts[i]);
        }

        private void SendText(byte kind, string text, NetworkConnection to) => SendBytes(kind, NetPayload.Pack(text ?? ""), to);

        // ------------------------------------------------------------------ server: to one player
        private NetworkConnection RemoteOf(CombatEntity player)
        {
            if (!IsServerInitialized || player == null) return null;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            return conn != null && conn.IsActive && !conn.IsLocalClient ? conn : null;
        }

        bool ISoilFieldNet.Open(CombatEntity player)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn == null) return false;
            watchers.Add(conn);
            // Up to date before the window opens: what changed since the picture it holds, else the whole picture - at
            // once while the garden is awake, else when it wakes.
            if (held.ContainsKey(conn)) due.Add(conn);
            else owed.Add(conn);
            if (Field.Simulated) Publish(Time.unscaledTime);
            TargetOpen(conn);
            return true;
        }

        void ISoilFieldNet.SendResult(CombatEntity player, FarmResult result)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn == null || result == null) return;
            // Only what the player sees: the items, the experience and the pollen's genes stay on the server.
            var seen = new FarmResult { ok = result.ok, seq = result.seq, message = result.message ?? "", note = result.note ?? "", plant = result.plant };
            if (result.lines != null) seen.lines.AddRange(result.lines);
            SendText(ResultData, JsonUtility.ToJson(seen), conn);
        }

        void ISoilFieldNet.SendSkills(CombatEntity player, string json)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && !string.IsNullOrEmpty(json)) SendText(SkillsData, json, conn);
        }

        void ISoilFieldNet.Tell(CombatEntity player, string text)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && !string.IsNullOrEmpty(text)) TargetMessage(conn, Clip(text));
        }

        private static string Clip(string text) => text.Length > MaxMessageLength ? text.Substring(0, MaxMessageLength) : text;

        // ------------------------------------------------------------------ server: requests
        private static bool Allowed(Dictionary<int, float> next, NetworkConnection conn, float interval)
        {
            float now = Time.unscaledTime;
            if (next.TryGetValue(conn.ClientId, out float at) && now < at) return false;
            if (next.Count > 64) next.Clear(); // players come and go
            next[conn.ClientId] = now + interval;
            return true;
        }

        /// <summary>
        /// A player's requests may come a few at once (an action, then the look at what it did) but on average no faster
        /// than one per <see cref="requestInterval"/>.
        /// </summary>
        private bool TakeRequestToken(NetworkConnection conn)
        {
            float now = Time.unscaledTime;
            float rate = 1f / Mathf.Max(0.005f, requestInterval);
            if (!requestTokens.TryGetValue(conn.ClientId, out (float tokens, float at) b))
            {
                if (requestTokens.Count > 64) requestTokens.Clear(); // players come and go
                b = (RequestBurst, now);
            }
            b.tokens = Mathf.Min(RequestBurst, b.tokens + (now - b.at) * rate);
            b.at = now;
            bool ok = b.tokens >= 1f;
            if (ok) b.tokens -= 1f;
            requestTokens[conn.ClientId] = b;
            return ok;
        }

        /// <summary>Is the request well formed (a known action, a spot on or near the field, short texts)?</summary>
        private bool Valid(FarmRequest r)
        {
            if (!Enum.IsDefined(typeof(FarmActionKind), r.kind)) return false;
            if (float.IsNaN(r.spot.x) || float.IsNaN(r.spot.y) || float.IsInfinity(r.spot.x) || float.IsInfinity(r.spot.y)) return false;
            if (float.IsNaN(r.amount) || float.IsInfinity(r.amount) || r.amount < 0f || r.amount > 1000f) return false;
            if (r.item != null && r.item.Length > MaxTextLength || r.text != null && r.text.Length > MaxTextLength) return false;
            Rect b = Field.layout.Bounds();
            float margin = Field.Settings.workRange + 2f;
            return r.spot.x >= b.xMin - margin && r.spot.x <= b.xMax + margin && r.spot.y >= b.yMin - margin && r.spot.y <= b.yMax + margin;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequest(string json, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive) return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null) return; // nobody to answer
            // A request is answered (the player's window waits for it), with the number it came with.
            int seq = 0;
            void Refuse(string why) => ((ISoilFieldNet)this).SendResult(player, new FarmResult { ok = false, message = why, seq = seq });
            if (!TakeRequestToken(conn))
            {
                // A window sends one request at a time, so only a flood gets here: answered once a second (read for its
                // number only then), the rest dropped unread.
                if (Allowed(nextRefusal, conn, 1f))
                {
                    TryRead(json, out FarmRequest flood);
                    seq = flood.seq;
                    Refuse("Not so fast.");
                }
                return;
            }
            bool readable = TryRead(json, out FarmRequest request);
            seq = request.seq;
            if (!readable)
            {
                Refuse("That cannot be done.");
                return;
            }
            if (!Valid(request))
            {
                Refuse("That cannot be done.");
                return;
            }
            if (!Field.interactable)
            {
                Refuse("This soil cannot be worked.");
                return;
            }
            if (!player.IsAlive || player.gameObject.scene != Field.gameObject.scene)
            {
                Refuse("You cannot work the soil now.");
                return;
            }
            // Only from beside the field (as its prompt allows): a request from afar never loads or wakes the garden.
            if (Field.DistanceFromOutline(player.transform.position) > Field.Settings.workRange + Field.extraReach + 2f)
            {
                Refuse("That is out of reach.");
                return;
            }
            // Done with the server's own field, the player's own items and skills (the spot and any plant named must be
            // within reach of the player's server position); the answer goes back through SendResult.
            Field.PerformHere(request, player);
        }

        /// <summary>A request from its JSON (false, and an empty request, when it is too long or unreadable).</summary>
        private static bool TryRead(string json, out FarmRequest request)
        {
            request = default;
            if (string.IsNullOrEmpty(json) || json.Length > 4096) return false;
            try
            {
                request = JsonUtility.FromJson<FarmRequest>(json);
                return true;
            }
            catch (Exception)
            {
                request = default;
                return false;
            }
        }

        /// <summary>
        /// A client starts (or stops) watching the garden: its player is near (or went away) - or asks again for what it
        /// missed. It says which picture it holds (<paramref name="holds"/>, 0 for none, and its checksum): when the server
        /// still keeps that one, the changes since then follow; otherwise the whole picture.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerWatch(bool watch, int holds, ulong holdsHash, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || conn.IsLocalClient) return;
            if (!watch)
            {
                Unwatch(conn);
                return;
            }
            if (!Allowed(nextWatch, conn, 0.5f)) return; // the client asks again if nothing came
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null || player.gameObject.scene != Field.gameObject.scene) return;
            if (Field.DistanceFromOutline(player.transform.position) > Field.drawDistance + WatchMargin + WatcherSlack) return;
            if (!watchers.Contains(conn) && watchers.Count >= MaxWatchers) return;
            watchers.Add(conn);
            // Sent at once if the garden is awake (it wakes for this player soon).
            SoilPicture known = Known(holds, holdsHash);
            if (known != null)
            {
                held[conn] = known;
                owed.Remove(conn);
                due.Add(conn);
            }
            else
            {
                held.Remove(conn);
                owed.Add(conn);
            }
        }

        // ------------------------------------------------------------------ clients: requests
        private bool IsRemoteClient => IsClientInitialized && !IsServerInitialized;

        void ISoilFieldNet.SendRequest(FarmRequest request)
        {
            if (!IsRemoteClient) return;
            if (request.item != null && request.item.Length > MaxTextLength) request.item = request.item.Substring(0, MaxTextLength);
            if (request.text != null && request.text.Length > MaxTextLength) request.text = request.text.Substring(0, MaxTextLength);
            ServerRequest(JsonUtility.ToJson(request));
        }

        // ------------------------------------------------------------------ clients: watching
        /// <summary>
        /// Client: watches the garden while this machine's player (or camera) is within its Draw Distance plus a margin, and
        /// forgets it - picture, mesh and memory - when it is further than that plus another margin.
        /// </summary>
        private void LookAround(float now)
        {
            if (IsServerInitialized || Field == null) return;
            float d = LocalDistance();
            float watchAt = Field.drawDistance + WatchMargin;
            if (!watching)
            {
                if (d > watchAt) return;
                watching = true;
                haveSnapshot = false;
                // Back at a garden walked away from: what it was then is what the changes apply to (the server says).
                mirror = kept != null ? Unpack(kept) : null;
                Unkeep();
                behind = false;
                askAgainAt = now + 5f;
                AskFromMirror();
            }
            else if (d > watchAt + ForgetMargin && !openWhenReady && !FarmWindow.IsShowing(soilField))
            {
                watching = false;
                haveSnapshot = false;
                Keep();
                mirror = null;
                behind = false;
                ServerWatch(false, 0, 0UL);
                soilField.ForgetPicture();
            }
            else if ((!haveSnapshot || behind) && now >= askAgainAt)
            {
                // The picture or a change was lost on the way (or the server dropped this watcher): ask again, saying
                // what is held, so only what is missing comes when the server still keeps that picture.
                askAgainAt = now + (behind ? AskBehindEvery : 5f);
                AskFromMirror();
            }
        }

        private void AskFromMirror() => ServerWatch(true, mirror?.Revision ?? 0, mirror?.Hash ?? 0UL);

        /// <summary>Client: walking away - the picture held is kept, packed, for the last few gardens (the oldest goes).</summary>
        private void Keep()
        {
            Unkeep();
            if (mirror == null || behind) return;
            kept = Pack(mirror, 0.0);
            keeping.Add(this);
            while (keeping.Count > KeptPictures)
            {
                NetSoilField oldest = keeping[0];
                keeping.RemoveAt(0);
                if (oldest != null) oldest.kept = null;
            }
        }

        private void Unkeep()
        {
            if (kept == null) return;
            kept = null;
            keeping.Remove(this);
        }

        /// <summary>
        /// Client: a change came that does not fit the picture held - one before it was missed (<paramref name="keep"/>:
        /// the picture held is still the server's, the rest is asked from it), or the result would not be exactly the
        /// server's (it is not built on again: the whole picture is asked for). What is drawn stays until it comes.
        /// </summary>
        private void FellBehind(bool keep)
        {
            if (!keep) mirror = null;
            float now = Time.unscaledTime;
            if (behind && now < askAgainAt) return; // asked already
            behind = true;
            askAgainAt = now + AskBehindEvery;
            AskFromMirror();
        }

        /// <summary>Metres from the garden's outline to this machine's player (else its camera).</summary>
        private float LocalDistance()
        {
            CombatEntity me = LocalPlayer();
            if (me != null) return Field.DistanceFromOutline(me.transform.position);
            Camera cam = Camera.main;
            return cam != null ? Field.DistanceFromOutline(cam.transform.position) : float.MaxValue;
        }

        // ------------------------------------------------------------------ clients: receiving
        [TargetRpc]
        private void TargetData(NetworkConnection conn, byte kind, int id, short index, short count, byte[] part) => ReceivePart(targetParts, kind, id, index, count, part);

        private void ReceivePart(NetPayloadAssembler parts, byte kind, int id, short index, short count, byte[] part)
        {
            if (IsServerInitialized || Field == null) return;
            if (!parts.Add(id, index, count, part, out byte[] whole)) return;
            switch (kind)
            {
                case SnapshotData:
                {
                    if (!watching && !openWhenReady) return; // forgotten meanwhile: stays forgotten
                    byte[] data = Inflate(whole, MaxSnapshotSize + 8);
                    if (data == null || data.Length < 8) return;
                    int rev = SoilPicture.ReadInt32(data, 0), soilLength = SoilPicture.ReadInt32(data, 4);
                    var picture = new byte[data.Length - 8];
                    Buffer.BlockCopy(data, 8, picture, 0, picture.Length);
                    if (!soilField.ImportVisual(picture)) return;
                    // What the next changes apply to (none when the server sends only whole pictures).
                    mirror = rev != 0 ? SoilPicture.FromWhole(picture, soilLength, rev) : null;
                    behind = false;
                    haveSnapshot = true;
                    if (openWhenReady) OpenWindow();
                    break;
                }
                case DeltaData:
                {
                    if (!watching && !openWhenReady) return;
                    if (mirror == null) return; // waiting for a whole picture (asked for already)
                    byte[] raw = Inflate(whole, MaxSnapshotSize);
                    SoilPicture next = SoilPictureDelta.Apply(mirror, raw, out double clock, out bool soil, out bool plants);
                    if (next == null)
                    {
                        // Not for the picture held (one was missed), or not ending exactly with the server's picture.
                        FellBehind(raw == null || SoilPictureDelta.BaseOf(raw) != mirror.Revision);
                        return;
                    }
                    // The field takes only the parts that changed (a plant that grew does not redraw the soil) - both when
                    // nothing is drawn yet (back at a garden: the changes came on the picture kept).
                    if (!haveSnapshot) soil = plants = true;
                    if (!soilField.ImportVisual(next.ToVisual(clock, soil, plants)))
                    {
                        FellBehind(false);
                        return;
                    }
                    mirror = next;
                    behind = false;
                    haveSnapshot = true;
                    if (openWhenReady) OpenWindow();
                    break;
                }
                case ResultData:
                {
                    if (!NetPayload.TryUnpack(whole, out string json)) return;
                    FarmResult r;
                    try { r = JsonUtility.FromJson<FarmResult>(json); }
                    catch (Exception) { return; }
                    if (r != null) soilField.ReceiveAnswer(LocalPlayer(), r);
                    break;
                }
                case SkillsData:
                {
                    if (!NetPayload.TryUnpack(whole, out string json)) return;
                    CombatEntity me = LocalPlayer();
                    if (me != null) AgriKnowledge.For(me)?.ReceiveFromServer(json);
                    break;
                }
            }
        }

        [TargetRpc]
        private void TargetOpen(NetworkConnection conn)
        {
            openWhenReady = true;
            watching = true; // the server counts this client as watching now
            if (haveSnapshot) OpenWindow();
        }

        private void OpenWindow()
        {
            openWhenReady = false;
            CombatEntity me = LocalPlayer();
            if (me != null && soilField != null) FarmWindow.Show(soilField, me);
        }

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(text) || text.Length > MaxMessageLength) return;
            CombatEntity me = LocalPlayer();
            if (me != null) InventoryManager.For(me)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        }

        private static CombatEntity LocalPlayer()
        {
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                    return players[i];
            return null;
        }

        // ------------------------------------------------------------------ packing
        private static byte[] Deflate(byte[] data, System.IO.Compression.CompressionLevel level = System.IO.Compression.CompressionLevel.Fastest)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new DeflateStream(ms, level, true))
                    z.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Inflate(byte[] data, int maxSize)
        {
            if (data == null) return null;
            try
            {
                using (var input = new MemoryStream(data))
                using (var z = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[16384];
                    int n;
                    while ((n = z.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, n);
                        if (output.Length > maxSize) return null; // a bomb, or a broken picture
                    }
                    return output.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // ---------------------------------------------------------------------- pictures and what changed between two (pure)
    /// <summary>
    /// A field's picture as the network side keeps it: what <see cref="SoilField.ExportVisual"/> writes, cut where it
    /// changes - the soil's bytes, and each plant's bytes by the plant's id - with a revision (which of the server's pictures
    /// it is) and a checksum of what it shows. The server keeps the last one it made and the one each watcher holds; a
    /// client keeps the one it holds; so what changed between the two can be sent instead of everything
    /// (<see cref="SoilPictureDelta"/>). Pure: no scene, no network.
    /// </summary>
    public sealed class SoilPicture
    {
        /// <summary>Bytes before the soil in a picture: its format, which parts it has, the clock, the grid's width and height.</summary>
        public const int HeaderSize = 21;
        /// <summary>Most plants a picture may have (as many as a field takes).</summary>
        public const int MaxPlants = 65536;
        /// <summary>Largest soil or plant part taken (bytes).</summary>
        public const int MaxBytes = 8 * 1024 * 1024;
        internal const byte SoilPart = 1, PlantPart = 2;
        /// <summary>
        /// A plant as the field writes it, after its id and species: position, stage, growth, health and stress, when it was
        /// planted, how long it has been dead, flags, symptoms, pests (8), diseases (6), canopy, dead wood and wounds.
        /// Then its label, and its produce (each piece: id, stage, growth, size, place, pollinated).
        /// </summary>
        private const int PlantFixedBytes = 8 + 1 + 4 + 8 + 4 + 1 + PlantState.SymptomCount + 8 + 6 + 3, FruitBytes = 4 + 5;
        private const ulong FnvStart = 14695981039346656037UL, FnvPrime = 1099511628211UL;

        public readonly int Revision, Format, Width, Height;
        /// <summary>The soil as it looks: the field's cells as the picture writes them.</summary>
        public readonly byte[] Soil;
        /// <summary>
        /// Each plant's bytes, in the field's order. When the plants could not be told apart (<see cref="Ids"/> null: the
        /// field writes them in a way this side does not know), one: all of them, sent whole when they change.
        /// </summary>
        public readonly byte[][] Plants;
        /// <summary>The plants' ids, in the same order (null: not told apart).</summary>
        public readonly int[] Ids;
        /// <summary>A checksum of the soil and plants (not the clock): a client that put a picture together checks it.</summary>
        public readonly ulong Hash;
        private Dictionary<int, int> index;

        internal SoilPicture(int revision, int format, int width, int height, byte[] soil, byte[][] plants, int[] ids)
        {
            Revision = revision;
            Format = format;
            Width = width;
            Height = height;
            Soil = soil;
            Plants = plants;
            Ids = ids;
            ulong h = Fnv(FnvStart, soil, 0, soil.Length);
            if (ids != null)
            {
                var count = new byte[4];
                WriteInt32(count, 0, plants.Length);
                h = Fnv(h, count, 0, 4);
                foreach (byte[] p in plants) h = Fnv(h, p, 0, p.Length);
            }
            else h = Fnv(h, plants[0], 0, plants[0].Length);
            Hash = h;
        }

        public bool Keyed => Ids != null;

        /// <summary>Bytes of the plant part (their count, then each plant).</summary>
        public int PlantBytes
        {
            get
            {
                if (Ids == null) return Plants[0].Length;
                int n = 4;
                foreach (byte[] p in Plants) n += p.Length;
                return n;
            }
        }

        /// <summary>Where the plant with this id is in <see cref="Plants"/> (-1: none, or the plants are not told apart).</summary>
        public int IndexOf(int id)
        {
            if (Ids == null) return -1;
            if (index == null)
            {
                index = new Dictionary<int, int>(Ids.Length);
                for (int i = 0; i < Ids.Length; i++) index[Ids[i]] = i;
            }
            return index.TryGetValue(id, out int at) ? at : -1;
        }

        /// <summary>
        /// The picture after <paramref name="previous"/>, from the field's own pictures of only its soil
        /// (<c>ExportVisual(true, false)</c>) and only its plants (<c>ExportVisual(false, true)</c>); a part not given is
        /// the previous one's. What did not change keeps the previous one's bytes (the changes then cost nothing to find),
        /// and when nothing changed the previous picture itself comes back - no new revision. Null when the bytes are not
        /// such pictures, or are of another shape than <paramref name="previous"/>.
        /// </summary>
        public static SoilPicture Make(byte[] soilPicture, byte[] plantPicture, SoilPicture previous, int revision)
        {
            int format = 0, width = 0, height = 0;
            bool known = false;
            if (soilPicture != null && !ReadHeader(soilPicture, SoilPart, ref known, ref format, ref width, ref height)) return null;
            if (plantPicture != null && !ReadHeader(plantPicture, PlantPart, ref known, ref format, ref width, ref height)) return null;
            if (!known) return previous;
            if (previous != null && (previous.Format != format || previous.Width != width || previous.Height != height)) return null;
            byte[] soil = previous?.Soil;
            if (soilPicture != null)
            {
                soil = Slice(soilPicture, HeaderSize, soilPicture.Length - HeaderSize);
                if (previous != null && Same(soil, previous.Soil)) soil = previous.Soil;
            }
            byte[][] plants = previous?.Plants;
            int[] ids = previous?.Ids;
            if (plantPicture != null) Split(plantPicture, HeaderSize, plantPicture.Length, previous, out plants, out ids);
            if (soil == null || plants == null) return null; // a first picture needs both parts
            if (previous != null && ReferenceEquals(soil, previous.Soil) && SameRecords(plants, ids, previous)) return previous;
            return new SoilPicture(revision, format, width, height, soil, plants, ids);
        }

        /// <summary>A whole picture (both parts) as a client receives it, its soil <paramref name="soilLength"/> bytes. Null when it is not one.</summary>
        public static SoilPicture FromWhole(byte[] picture, int soilLength, int revision)
        {
            int format = 0, width = 0, height = 0;
            bool known = false;
            if (picture == null || !ReadHeader(picture, SoilPart | PlantPart, ref known, ref format, ref width, ref height)) return null;
            if (soilLength < 0 || soilLength > picture.Length - HeaderSize) return null;
            byte[] soil = Slice(picture, HeaderSize, soilLength);
            Split(picture, HeaderSize + soilLength, picture.Length, null, out byte[][] plants, out int[] ids);
            return new SoilPicture(revision, format, width, height, soil, plants, ids);
        }

        /// <summary>The picture as the field takes it (<see cref="SoilField.ImportVisual"/>), with the parts asked for.</summary>
        public byte[] ToVisual(double clock, bool soil, bool plants)
        {
            var b = new byte[HeaderSize + (soil ? Soil.Length : 0) + (plants ? PlantBytes : 0)];
            WriteInt32(b, 0, Format);
            b[4] = (byte)((soil ? SoilPart : 0) | (plants ? PlantPart : 0));
            WriteInt64(b, 5, BitConverter.DoubleToInt64Bits(clock));
            WriteInt32(b, 13, Width);
            WriteInt32(b, 17, Height);
            int at = HeaderSize;
            if (soil)
            {
                Buffer.BlockCopy(Soil, 0, b, at, Soil.Length);
                at += Soil.Length;
            }
            if (!plants) return b;
            if (Ids == null)
            {
                Buffer.BlockCopy(Plants[0], 0, b, at, Plants[0].Length);
                return b;
            }
            WriteInt32(b, at, Plants.Length);
            at += 4;
            foreach (byte[] p in Plants)
            {
                Buffer.BlockCopy(p, 0, b, at, p.Length);
                at += p.Length;
            }
            return b;
        }

        /// <summary>Is this the field's whole picture <paramref name="picture"/>, byte for byte (its clock aside)?</summary>
        public bool Matches(byte[] picture)
        {
            if (picture == null || picture.Length < HeaderSize) return false;
            byte[] mine = ToVisual(BitConverter.Int64BitsToDouble(ReadInt64(picture, 5)), true, true);
            return Same(mine, picture);
        }

        // ---------------------------------------------------------------- reading the field's pictures
        private static bool ReadHeader(byte[] b, int parts, ref bool known, ref int format, ref int width, ref int height)
        {
            if (b.Length < HeaderSize || b[4] != parts) return false;
            int f = ReadInt32(b, 0), w = ReadInt32(b, 13), h = ReadInt32(b, 17);
            if (known && (f != format || w != width || h != height)) return false;
            known = true;
            format = f;
            width = w;
            height = h;
            return true;
        }

        /// <summary>
        /// The plant part from <paramref name="from"/> to <paramref name="end"/>, one record per plant by id - or, when it
        /// cannot be read so (another way of writing plants, two plants with one id), all of it as one. Records that did not
        /// change keep <paramref name="previous"/>'s bytes. The same bytes always come out the same way, on both sides.
        /// </summary>
        internal static void Split(byte[] b, int from, int end, SoilPicture previous, out byte[][] plants, out int[] ids)
        {
            if (TryRecords(b, from, end, out byte[][] records, out int[] found))
            {
                if (previous != null && previous.Keyed)
                    for (int i = 0; i < records.Length; i++)
                    {
                        int j = previous.IndexOf(found[i]);
                        if (j >= 0 && Same(records[i], previous.Plants[j])) records[i] = previous.Plants[j];
                    }
                plants = records;
                ids = found;
                return;
            }
            byte[] all = Slice(b, from, end - from);
            if (previous != null && !previous.Keyed && Same(all, previous.Plants[0])) all = previous.Plants[0];
            plants = new[] { all };
            ids = null;
        }

        private static bool TryRecords(byte[] b, int from, int end, out byte[][] records, out int[] ids)
        {
            records = null;
            ids = null;
            if (end - from < 4) return false;
            int count = ReadInt32(b, from);
            if (count < 0 || count > MaxPlants || count > (end - from) / 8) return false;
            records = new byte[count][];
            ids = new int[count];
            var seen = new HashSet<int>();
            int at = from + 4;
            for (int n = 0; n < count; n++)
            {
                int start = at;
                if (!Skip(b, ref at, end, 4)) return false;
                int id = ReadInt32(b, start);
                if (!SkipString(b, ref at, end) || !Skip(b, ref at, end, PlantFixedBytes) || !SkipString(b, ref at, end) || !Skip(b, ref at, end, 2)) return false;
                int fruits = (short)(b[at - 2] | b[at - 1] << 8);
                if (fruits < 0 || !Skip(b, ref at, end, fruits * FruitBytes) || !seen.Add(id)) return false;
                ids[n] = id;
                records[n] = Slice(b, start, at - start);
            }
            return at == end;
        }

        private static bool Skip(byte[] b, ref int at, int end, int n)
        {
            if (n < 0 || n > end - at) return false;
            at += n;
            return true;
        }

        /// <summary>Over a string as the field writes it (its length in 7-bit groups, then its bytes).</summary>
        private static bool SkipString(byte[] b, ref int at, int end)
        {
            int length = 0;
            for (int shift = 0; ; shift += 7)
            {
                if (at >= end || shift > 28) return false;
                byte x = b[at++];
                length |= (x & 0x7F) << shift;
                if ((x & 0x80) == 0) break;
            }
            return Skip(b, ref at, end, length);
        }

        private static bool SameRecords(byte[][] plants, int[] ids, SoilPicture previous)
        {
            if (ReferenceEquals(plants, previous.Plants)) return true;
            if (plants.Length != previous.Plants.Length || (ids == null) != (previous.Ids == null)) return false;
            for (int i = 0; i < plants.Length; i++)
                if (!ReferenceEquals(plants[i], previous.Plants[i])) return false;
            return true;
        }

        // ---------------------------------------------------------------- bytes
        internal static byte[] Slice(byte[] b, int at, int n)
        {
            var s = new byte[n];
            Buffer.BlockCopy(b, at, s, 0, n);
            return s;
        }

        internal static bool Same(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static ulong Fnv(ulong h, byte[] b, int at, int n)
        {
            for (int i = at; i < at + n; i++)
            {
                h ^= b[i];
                h *= FnvPrime;
            }
            return h;
        }

        /// <summary>Little-endian, as the field's pictures are written on every machine.</summary>
        public static int ReadInt32(byte[] b, int at) => b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24;

        public static void WriteInt32(byte[] b, int at, int v)
        {
            b[at] = (byte)v;
            b[at + 1] = (byte)(v >> 8);
            b[at + 2] = (byte)(v >> 16);
            b[at + 3] = (byte)(v >> 24);
        }

        internal static long ReadInt64(byte[] b, int at) => (uint)ReadInt32(b, at) | (long)ReadInt32(b, at + 4) << 32;

        internal static void WriteInt64(byte[] b, int at, long v)
        {
            WriteInt32(b, at, (int)v);
            WriteInt32(b, at + 4, (int)(v >> 32));
        }
    }

    /// <summary>
    /// What changed from one <see cref="SoilPicture"/> to a later one, as the server sends it to a watcher that holds the
    /// first: the stretches of soil that changed, the plants gone (by id), those that changed and the new ones, and the
    /// order only when it is not simply the old one with the new at the end. What changed goes as the difference from what
    /// the watcher holds, byte by byte (wrapping round): unchanged bytes are zeros and growth a small step, both of which
    /// pack to almost nothing; a plant whose length changed (a fruit set, a label) goes whole. It names the revision it applies to, the one it makes and the checksum of the result, so a client that does
    /// not hold the first, or would not end with exactly the server's picture, knows it and asks again. The ids and lengths
    /// come first and the bytes after, which packs better. Pure: no scene, no network.
    /// </summary>
    public static class SoilPictureDelta
    {
        /// <summary>The revision it applies to, the one it makes, the clock and the checksum.</summary>
        public const int HeaderSize = 24;
        /// <summary>Unchanged soil bytes between two changed ones that go along (as zeros) rather than starting a new stretch.</summary>
        private const int Gap = 24;
        private const byte Unchanged = 0, Changes = 1, Whole = 2;

        /// <summary>The revision a change applies to (0: not a change).</summary>
        public static int BaseOf(byte[] delta) => delta != null && delta.Length >= 4 ? SoilPicture.ReadInt32(delta, 0) : 0;

        /// <summary>What changed from <paramref name="from"/> to <paramref name="to"/>, unpacked. Null when they are of different shapes.</summary>
        public static byte[] Make(SoilPicture from, SoilPicture to, double clock)
        {
            if (from == null || to == null || from.Format != to.Format || from.Width != to.Width || from.Height != to.Height) return null;
            var head = new MemoryStream(64);
            var data = new MemoryStream();
            var fixedPart = new byte[HeaderSize];
            SoilPicture.WriteInt32(fixedPart, 0, from.Revision);
            SoilPicture.WriteInt32(fixedPart, 4, to.Revision);
            SoilPicture.WriteInt64(fixedPart, 8, BitConverter.DoubleToInt64Bits(clock));
            SoilPicture.WriteInt64(fixedPart, 16, (long)to.Hash);
            head.Write(fixedPart, 0, fixedPart.Length);

            if (SoilPicture.Same(from.Soil, to.Soil)) head.WriteByte(Unchanged);
            else if (from.Soil.Length != to.Soil.Length)
            {
                head.WriteByte(Whole);
                WriteVar(head, to.Soil.Length);
                data.Write(to.Soil, 0, to.Soil.Length);
            }
            else
            {
                head.WriteByte(Changes);
                SoilRuns(from.Soil, to.Soil, head, data);
            }

            if (SamePlants(from, to)) head.WriteByte(Unchanged);
            else if (from.Keyed && to.Keyed)
            {
                head.WriteByte(Changes);
                PlantChanges(from, to, head, data);
            }
            else
            {
                byte[] section = Section(to);
                head.WriteByte(Whole);
                WriteVar(head, section.Length);
                data.Write(section, 0, section.Length);
            }
            head.Write(data.GetBuffer(), 0, (int)data.Length);
            return head.ToArray();
        }

        /// <summary>
        /// <paramref name="from"/> with the changes in <paramref name="delta"/>: exactly the server's picture, or null -
        /// the changes are for another picture, broken, or would not end with the server's (its checksum). Which parts
        /// changed comes out too (the field takes only those).
        /// </summary>
        public static SoilPicture Apply(SoilPicture from, byte[] delta, out double clock, out bool soilChanged, out bool plantsChanged)
        {
            clock = 0.0;
            soilChanged = plantsChanged = false;
            if (from == null || delta == null || delta.Length < HeaderSize + 2 || SoilPicture.ReadInt32(delta, 0) != from.Revision) return null;
            try
            {
                int revision = SoilPicture.ReadInt32(delta, 4);
                double at = BitConverter.Int64BitsToDouble(SoilPicture.ReadInt64(delta, 8));
                ulong hash = (ulong)SoilPicture.ReadInt64(delta, 16);
                var r = new Cursor(delta, HeaderSize);

                // What comes, first (ids and lengths); the bytes follow in the same order.
                byte soilMode = r.Byte();
                int soilWhole = 0;
                List<(int gap, int length)> runs = null;
                if (soilMode == Whole) soilWhole = r.Length(SoilPicture.MaxBytes);
                else if (soilMode == Changes)
                {
                    int n = r.Length(from.Soil.Length);
                    runs = new List<(int, int)>(n);
                    for (int i = 0; i < n; i++) runs.Add((r.Length(from.Soil.Length), r.Length(from.Soil.Length)));
                }
                else if (soilMode != Unchanged) return null;

                byte plantMode = r.Byte();
                int plantWhole = 0;
                int[] removed = null, changed = null, changedLength = null, addedLength = null, order = null;
                if (plantMode == Whole) plantWhole = r.Length(SoilPicture.MaxBytes);
                else if (plantMode == Changes)
                {
                    if (!from.Keyed) return null;
                    removed = r.Ids(r.Length(from.Ids.Length));
                    changed = r.Ids(r.Length(from.Ids.Length));
                    changedLength = new int[changed.Length];
                    for (int i = 0; i < changed.Length; i++) changedLength[i] = r.Length(SoilPicture.MaxBytes); // 0: as long as before
                    addedLength = new int[r.Length(SoilPicture.MaxPlants)];
                    for (int i = 0; i < addedLength.Length; i++) addedLength[i] = r.Length(SoilPicture.MaxBytes);
                    if (r.Byte() != 0) order = r.Ids(r.Length(SoilPicture.MaxPlants));
                }
                else if (plantMode != Unchanged) return null;

                // The soil.
                byte[] soil = from.Soil;
                if (soilMode == Whole) soil = r.Bytes(soilWhole);
                else if (soilMode == Changes)
                {
                    soil = (byte[])from.Soil.Clone();
                    int pos = 0;
                    foreach ((int gap, int length) in runs)
                    {
                        pos += gap;
                        if (length < 1 || length > soil.Length - pos) return null;
                        r.AddDifferences(soil, pos, length);
                        pos += length;
                    }
                }

                // The plants.
                byte[][] plants = from.Plants;
                int[] ids = from.Ids;
                if (plantMode == Whole)
                    SoilPicture.Split(r.Bytes(plantWhole), 0, plantWhole, null, out plants, out ids);
                else if (plantMode == Changes)
                {
                    var gone = new HashSet<int>();
                    foreach (int id in removed)
                        if (from.IndexOf(id) < 0 || !gone.Add(id)) return null;
                    var now = new Dictionary<int, byte[]>(changed.Length);
                    for (int i = 0; i < changed.Length; i++)
                    {
                        int j = from.IndexOf(changed[i]);
                        if (j < 0 || gone.Contains(changed[i]) || now.ContainsKey(changed[i])) return null;
                        byte[] was = from.Plants[j];
                        byte[] rec;
                        if (changedLength[i] == 0)
                        {
                            rec = (byte[])was.Clone();
                            r.AddDifferences(rec, 0, rec.Length);
                        }
                        else rec = r.Bytes(changedLength[i] - 1);
                        now[changed[i]] = rec;
                    }
                    var list = new List<byte[]>(from.Plants.Length + addedLength.Length);
                    for (int i = 0; i < from.Ids.Length; i++)
                    {
                        int id = from.Ids[i];
                        if (gone.Contains(id)) continue;
                        list.Add(now.TryGetValue(id, out byte[] rec) ? rec : from.Plants[i]);
                    }
                    foreach (int length in addedLength) list.Add(r.Bytes(length));
                    if (list.Count > SoilPicture.MaxPlants) return null;
                    // Each plant's id is its first bytes (as the server reads it): one plant per id.
                    var byId = new Dictionary<int, byte[]>(list.Count);
                    foreach (byte[] rec in list)
                        if (rec.Length < 4 || !byId.TryAdd(SoilPicture.ReadInt32(rec, 0), rec)) return null;
                    if (order != null)
                    {
                        if (order.Length != list.Count) return null;
                        list.Clear();
                        foreach (int id in order)
                        {
                            if (!byId.TryGetValue(id, out byte[] rec)) return null;
                            byId.Remove(id);
                            list.Add(rec);
                        }
                    }
                    plants = list.ToArray();
                    ids = new int[plants.Length];
                    for (int i = 0; i < plants.Length; i++) ids[i] = SoilPicture.ReadInt32(plants[i], 0);
                }
                if (!r.End) return null;

                var result = new SoilPicture(revision, from.Format, from.Width, from.Height, soil, plants, ids);
                if (result.Hash != hash) return null;
                clock = at;
                soilChanged = soilMode != Unchanged;
                plantsChanged = plantMode != Unchanged;
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- making
        /// <summary>The stretches of soil that changed: where (from the end of the last) and how long, then their differences.</summary>
        private static void SoilRuns(byte[] a, byte[] b, MemoryStream head, MemoryStream data)
        {
            var runs = new List<(int at, int length)>();
            for (int i = 0; i < a.Length; )
            {
                if (a[i] == b[i])
                {
                    i++;
                    continue;
                }
                int last = i;
                for (int j = i + 1; j < a.Length && j - last <= Gap; j++)
                    if (a[j] != b[j]) last = j;
                runs.Add((i, last - i + 1));
                i = last + 1;
            }
            WriteVar(head, runs.Count);
            int end = 0;
            foreach ((int at, int length) in runs)
            {
                WriteVar(head, at - end);
                WriteVar(head, length);
                for (int k = at; k < at + length; k++) data.WriteByte((byte)(b[k] - a[k]));
                end = at + length;
            }
        }

        private static void PlantChanges(SoilPicture from, SoilPicture to, MemoryStream head, MemoryStream data)
        {
            var removed = new List<int>();
            foreach (int id in from.Ids)
                if (to.IndexOf(id) < 0) removed.Add(id);
            var changed = new List<int>();
            var added = new List<int>();
            for (int i = 0; i < to.Ids.Length; i++)
            {
                int j = from.IndexOf(to.Ids[i]);
                if (j < 0) added.Add(i);
                else if (!SoilPicture.Same(to.Plants[i], from.Plants[j])) changed.Add(i);
            }
            WriteIds(head, removed);
            var changedIds = new List<int>(changed.Count);
            foreach (int i in changed) changedIds.Add(to.Ids[i]);
            WriteIds(head, changedIds);
            foreach (int i in changed)
            {
                byte[] now = to.Plants[i], was = from.Plants[from.IndexOf(to.Ids[i])];
                if (now.Length == was.Length)
                {
                    WriteVar(head, 0);
                    for (int k = 0; k < now.Length; k++) data.WriteByte((byte)(now[k] - was[k]));
                }
                else
                {
                    WriteVar(head, now.Length + 1);
                    data.Write(now, 0, now.Length);
                }
            }
            WriteVar(head, added.Count);
            foreach (int i in added)
            {
                WriteVar(head, to.Plants[i].Length);
                data.Write(to.Plants[i], 0, to.Plants[i].Length);
            }
            // The order a client makes by itself: the old one without the gone, the new at the end (as plants come and go).
            bool kept = true;
            int k2 = 0;
            foreach (int id in from.Ids)
            {
                if (to.IndexOf(id) < 0) continue;
                if (to.Ids[k2++] != id)
                {
                    kept = false;
                    break;
                }
            }
            head.WriteByte((byte)(kept ? 0 : 1));
            if (!kept) WriteIds(head, to.Ids);
        }

        private static bool SamePlants(SoilPicture a, SoilPicture b)
        {
            if (ReferenceEquals(a.Plants, b.Plants)) return true;
            if (a.Keyed && b.Keyed)
            {
                if (a.Plants.Length != b.Plants.Length) return false;
                for (int i = 0; i < a.Plants.Length; i++)
                    if (!SoilPicture.Same(a.Plants[i], b.Plants[i])) return false;
                return true;
            }
            return SoilPicture.Same(Section(a), Section(b));
        }

        /// <summary>The plant part as the field writes it (their count, then each).</summary>
        private static byte[] Section(SoilPicture p)
        {
            if (!p.Keyed) return p.Plants[0];
            byte[] v = p.ToVisual(0.0, false, true);
            return SoilPicture.Slice(v, SoilPicture.HeaderSize, v.Length - SoilPicture.HeaderSize);
        }

        /// <summary>Ids as differences from the one before (plants are mostly in the order of their ids: small numbers).</summary>
        private static void WriteIds(MemoryStream s, IReadOnlyList<int> ids)
        {
            WriteVar(s, ids.Count);
            int prev = 0;
            foreach (int id in ids)
            {
                int d = unchecked(id - prev);
                WriteVar(s, unchecked((uint)((d << 1) ^ (d >> 31))));
                prev = id;
            }
        }

        private static void WriteVar(MemoryStream s, int v) => WriteVar(s, (uint)v);

        private static void WriteVar(MemoryStream s, uint v)
        {
            while (v >= 0x80)
            {
                s.WriteByte((byte)(v | 0x80));
                v >>= 7;
            }
            s.WriteByte((byte)v);
        }

        // ---------------------------------------------------------------- reading
        /// <summary>Reads a change, refusing (by throwing) anything out of bounds.</summary>
        private sealed class Cursor
        {
            private readonly byte[] b;
            private int at;

            public Cursor(byte[] bytes, int start)
            {
                b = bytes;
                at = start;
            }

            public bool End => at == b.Length;

            public byte Byte()
            {
                if (at >= b.Length) throw new InvalidDataException("cut short");
                return b[at++];
            }

            private uint Var()
            {
                uint v = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    byte x = Byte();
                    v |= (uint)(x & 0x7F) << shift;
                    if ((x & 0x80) == 0) return v;
                }
                throw new InvalidDataException("a number too long");
            }

            /// <summary>A count or length of at most <paramref name="max"/>.</summary>
            public int Length(int max)
            {
                uint v = Var();
                if (v > (uint)max) throw new InvalidDataException("too large");
                return (int)v;
            }

            public int[] Ids(int count)
            {
                var ids = new int[count];
                int prev = 0;
                for (int i = 0; i < count; i++)
                {
                    uint z = Var();
                    prev = unchecked(prev + ((int)(z >> 1) ^ -(int)(z & 1)));
                    ids[i] = prev;
                }
                return ids;
            }

            public byte[] Bytes(int n)
            {
                if (n < 0 || n > b.Length - at) throw new InvalidDataException("cut short");
                byte[] s = SoilPicture.Slice(b, at, n);
                at += n;
                return s;
            }

            /// <summary>Adds <paramref name="n"/> differences to <paramref name="into"/> from <paramref name="pos"/> (each byte wraps round).</summary>
            public void AddDifferences(byte[] into, int pos, int n)
            {
                if (n < 0 || n > b.Length - at) throw new InvalidDataException("cut short");
                for (int k = 0; k < n; k++) into[pos + k] += b[at + k];
                at += n;
            }
        }
    }
}
#endif
