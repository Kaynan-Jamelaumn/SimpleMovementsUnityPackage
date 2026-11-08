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
    /// their clocks right, nothing while no remote client is connected. Nothing hidden ever leaves the server, so no
    /// client can read a soil's chemistry or a seed's genes it has not measured.</item>
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
        private const byte SnapshotData = 0, ResultData = 1, SkillsData = 2;
        /// <summary>Requests a player may send at once before they must wait (an action and the look that follows it, a few clicks).</summary>
        private const float RequestBurst = 8f;

        [Tooltip("Least seconds between two pictures of the field sent to the clients (changes in between go together).")]
        [SerializeField, Range(0.1f, 5f)] private float sendInterval = 0.5f;
        [Tooltip("Seconds between pictures sent even when nothing changed (clocks, late joiners).")]
        [SerializeField, Range(5f, 120f)] private float refreshInterval = 30f;
        [Tooltip("Seconds a player's requests must be apart on average (a few may come at once).")]
        [SerializeField, Range(0f, 1f)] private float requestInterval = 0.08f;

        private SoilField soilField;

        // Server
        private bool dirty;
        private float nextSend, nextRefresh;
        private int transfer, sentSoil = int.MinValue, sentPlants = int.MinValue;
        private readonly Dictionary<int, (float tokens, float at)> requestTokens = new Dictionary<int, (float, float)>();
        private readonly Dictionary<int, float> nextAsk = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextRefusal = new Dictionary<int, float>();

        // Clients
        private readonly NetPayloadAssembler observerParts = new NetPayloadAssembler();
        private readonly NetPayloadAssembler targetParts = new NetPayloadAssembler();
        private bool openWhenReady, haveSnapshot;
        private float askAgainAt;

        private SoilField Field => soilField != null ? soilField : (soilField = GetComponent<SoilField>());

        public bool RunsRules => IsServerInitialized;

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Field.Net = this;
            dirty = true;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (soilField != null && ReferenceEquals(soilField.Net, this)) soilField.Net = null;
            requestTokens.Clear();
            nextAsk.Clear();
            nextRefusal.Clear();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return; // the host runs the rules itself
            Field.Net = this;
            haveSnapshot = false;
            askAgainAt = Time.unscaledTime + 3f;
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
        }

        /// <summary>A client that starts watching the field gets its picture.</summary>
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (connection != null && !connection.IsLocalClient) SendSnapshot(connection);
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
                bool refresh = now >= nextRefresh;
                if (!(dirty && now >= nextSend) && !refresh) return;
                dirty = false;
                nextSend = now + sendInterval;
                if (refresh) nextRefresh = now + refreshInterval;
                // A host playing alone sends nothing (the field and its window are its own).
                if (!AnyRemoteClient()) return;
                FieldState st = Field.State;
                bool soil = refresh || st.Grid.Version != sentSoil, plants = refresh || st.PlantVersion != sentPlants;
                if (soil || plants) SendSnapshot(null, soil, plants);
                return;
            }
            // A client that never got the picture (lost on the way, joined at a bad moment) asks for it.
            if (IsClientInitialized && !haveSnapshot && now >= askAgainAt)
            {
                askAgainAt = now + 5f;
                ServerSnapshot();
            }
        }

        /// <summary>The field's picture (its soil and/or its plants): to everyone watching (<paramref name="to"/> null) or to one client.</summary>
        private void SendSnapshot(NetworkConnection to, bool soil = true, bool plants = true)
        {
            FieldState st = Field.State;
            if (to == null)
            {
                // What everyone has now (a picture for one client tells only that client).
                if (soil) sentSoil = st.Grid.Version;
                if (plants) sentPlants = st.PlantVersion;
            }
            SendBytes(SnapshotData, Deflate(soilField.ExportVisual(soil, plants)), to);
        }

        /// <summary>Is a client connected that is not this machine (the host's own client needs no pictures)?</summary>
        private static bool AnyRemoteClient()
        {
            NetSession session = NetSession.Instance;
            if (session == null) return true; // cannot tell: send
            foreach (NetworkConnection c in session.Clients)
                if (!c.IsLocalClient) return true;
            return false;
        }

        /// <summary>Sends bytes in parts: to everyone watching (<paramref name="to"/> null) or to one client.</summary>
        private void SendBytes(byte kind, byte[] data, NetworkConnection to)
        {
            List<byte[]> parts = NetPayload.Split(data ?? Array.Empty<byte>());
            int id = ++transfer;
            for (int i = 0; i < parts.Count; i++)
            {
                if (to == null) ObserversData(kind, id, (short)i, (short)parts.Count, parts[i]);
                else TargetData(to, kind, id, (short)i, (short)parts.Count, parts[i]);
            }
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
            SendSnapshot(conn);
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

        [ServerRpc(RequireOwnership = false)]
        private void ServerSnapshot(NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextAsk, conn, 2f)) return;
            SendSnapshot(conn);
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

        // ------------------------------------------------------------------ clients: receiving
        [ObserversRpc(ExcludeServer = true)]
        private void ObserversData(byte kind, int id, short index, short count, byte[] part) => ReceivePart(observerParts, kind, id, index, count, part);

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
                    byte[] data = Inflate(whole, MaxSnapshotSize);
                    if (data == null || !soilField.ImportVisual(data)) return;
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
        private static byte[] Deflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
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
}
#endif
