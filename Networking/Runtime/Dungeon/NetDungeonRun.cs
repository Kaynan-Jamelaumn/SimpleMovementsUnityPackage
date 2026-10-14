#if FISHNET
using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>One published state of a dungeon element, as sent to clients (see <see cref="DungeonNet"/>).</summary>
    public struct DungeonElementStateMsg
    {
        /// <summary>The element's id (see <see cref="DungeonNetIds"/>).</summary>
        public int id;
        /// <summary>The <see cref="DungeonNet.Channel"/>.</summary>
        public byte channel;
        /// <summary>The state.</summary>
        public int state;
        /// <summary>Optional data (null or empty for most states).</summary>
        public byte[] payload;
    }

    /// <summary>
    /// The network relay of one dungeon run. It lives on a scene NetworkObject in the dungeon instance template scene
    /// (<see cref="DungeonInstanceSceneMarker"/>), so every stacked copy of that scene - one per run - has its own, and
    /// only the clients whose player is in that run observe it.
    /// </summary>
    /// <remarks>
    /// <para><b>Server.</b> Finds the run of its scene (<see cref="DungeonSession.RunAt"/>) and, whenever the run's
    /// dungeon changes (a new one, or the next one of a chain), sends the run header (profile name, origin, grid size,
    /// floor count - never the seed) and then the floors as the party reaches them: the arrival floor at once, the floor
    /// below when a player of the run comes close to a way down (stairs, drop, climb) or stands on a floor whose chasm
    /// falls lead there. Each floor packet carries its keyed build seed (<see cref="DungeonBuildSeed"/>). Element states
    /// (<see cref="DungeonNet.Publish"/>) go to the run's clients for floors they have, and with the floor otherwise.
    /// Clients that arrive later get everything sent so far. A client's request to use an element is checked (its player
    /// is alive, in this run and close enough, the element exists on a floor it was sent) before the element's own
    /// server code runs. Players who arrive keep their controls frozen until their client reports the floor they stand on
    /// built (<see cref="DungeonSession.ArrivalHold"/>), at most <see cref="ArrivalTimeout"/> seconds.</para>
    /// <para><b>Clients.</b> Build a presentation-only copy (<see cref="DungeonInstance.IsReplica"/>) with
    /// <see cref="DungeonBuilder.BeginReplica"/>: each floor is meshed on a worker thread and built in order, the
    /// server-only components are switched off, and received states are applied (or kept until their floor exists).</para>
    /// <para>Nothing here runs offline: without a session the dungeon is the single-player one.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetDungeonRun : NetworkBehaviour
    {
        /// <summary>Seconds a held player waits for their client before getting the controls back anyway.</summary>
        public const float ArrivalTimeout = 30f;
        /// <summary>Fewest seconds between two use requests of one client.</summary>
        public const float InteractInterval = 0.2f;
        /// <summary>Slack added to an element's use range (metres): positions on the server lag a little.</summary>
        public const float RangeTolerance = 0.75f;

        [Tooltip("How close (metres) a player of the run must come to a way down (stairs, drop, climb) before the floor below is sent to the run's clients.")]
        [Min(4f)] public float revealDistance = 18f;
        [Tooltip("Seconds between two checks of which floors the party has reached (server).")]
        [Min(0.05f)] public float revealInterval = 0.25f;

        private static readonly List<NetDungeonRun> all = new List<NetDungeonRun>();
        private static readonly Relay relay = new Relay();
        private static Func<global::Player, Interactable, bool> chainedInteract;
        private static Func<global::Player, Interactable, bool> interactRoute;

        /// <summary>The relays of every run scene loaded here.</summary>
        public static IReadOnlyList<NetDungeonRun> All => all;

        /// <summary>The relay of the run scene <paramref name="scene"/>, or null.</summary>
        public static NetDungeonRun ForScene(Scene scene)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].gameObject.scene == scene)
                    return all[i];
            return null;
        }

        /// <summary>The generation of the dungeon shown (server: built; client: received). 0 = none yet.</summary>
        public int Generation => IsServerInitialized ? serverGeneration : clientGeneration;

        /// <summary>Server: the deepest floor sent to the run's clients (-1 = none).</summary>
        public int RevealedFloor => serverFloors.Count - 1;

        /// <summary>Client: the presentation-only copy of the run's dungeon, or null.</summary>
        public DungeonInstance Replica => replicaBuilder != null ? replicaBuilder.Instance : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            chainedInteract = null;
            interactRoute = null;
        }

        // ================================================================== lifetime

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!all.Contains(this))
                all.Add(this);
            DungeonNet.Relay = relay;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            all.Remove(this);
            if (all.Count == 0 && ReferenceEquals(DungeonNet.Relay, relay))
                DungeonNet.Relay = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            DungeonSession.ArrivalHold = HoldArrival;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            // Whoever is still held gets their controls back (the run is going away with its scene).
            foreach (Hold h in holds)
                if (h.player != null)
                    DungeonSession.SetControls(h.player, true);
            holds.Clear();
            bool another = false;
            foreach (NetDungeonRun r in all)
                another |= r != null && r != this && r.IsServerInitialized;
            if (!another && DungeonSession.ArrivalHold == (Func<DungeonRun, GameObject, bool>)HoldArrival)
                DungeonSession.ArrivalHold = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized)
                return;   // the host's client shows the server's own dungeon
            InstallInteractRoute();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsServerInitialized)
                ClearReplica();
        }

        private void OnDestroy()
        {
            all.Remove(this);
            ClearReplica();
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate();
            else if (IsClientInitialized && interactRoute != null && global::Player.NetInteractHandler != interactRoute)
                InstallInteractRoute();   // another module put its handler there (its player respawned): stay in front of it
        }

        // ================================================================== server

        private struct Hold
        {
            public DungeonRun run;
            public GameObject player;
            public NetworkConnection conn;
            public float until;
        }

        private struct StoredState
        {
            public DungeonElementStateMsg msg;
            public int floor;
        }

        private struct Built
        {
            public int generation;
            public int floor;
        }

        private DungeonRun serverRun;
        private DungeonInstance serverInstance;
        private int serverGeneration;
        private byte[] serverHeader;
        private readonly List<byte[]> serverFloors = new List<byte[]>();
        private readonly Dictionary<long, StoredState> serverStates = new Dictionary<long, StoredState>();
        private readonly Dictionary<int, Built> builtByClient = new Dictionary<int, Built>();
        private readonly Dictionary<int, float> lastInteract = new Dictionary<int, float>();
        private readonly List<Hold> holds = new List<Hold>();
        private bool[] floorHasFall;
        private float nextReveal;
        private bool warnedUnkeyedSeeds;

        private void ServerUpdate()
        {
            DungeonRun run = DungeonSession.RunAt(this);
            if (run != null && run.SceneLease != null && run.Scene != gameObject.scene)
                run = null;
            DungeonInstance instance = run != null ? run.Instance : null;
            if (run != serverRun || instance != serverInstance)
            {
                serverRun = run;
                serverInstance = instance;
                if (instance != null)
                    StartGeneration(run, instance);
            }
            if (serverInstance != null && Time.unscaledTime >= nextReveal)
            {
                nextReveal = Time.unscaledTime + revealInterval;
                RevealReached();
            }
            UpdateHolds();
        }

        /// <summary>The run's dungeon is new: the header and the arrival floor go to every client of the run.</summary>
        private void StartGeneration(DungeonRun run, DungeonInstance instance)
        {
            serverGeneration++;
            serverFloors.Clear();
            serverStates.Clear();
            builtByClient.Clear();
            DungeonManager manager = run.Manager;
            var header = new DungeonNetHeader
            {
                Generation = serverGeneration,
                Profile = manager != null && manager.profile != null ? manager.profile.name : "",
                Label = run.Request != null && !string.IsNullOrEmpty(run.Request.label) ? run.Request.label : "Dungeon",
                Origin = instance.transform.position,
                Rotation = instance.transform.rotation,
                CellSize = instance.Layout.CellSize,
                FloorSpacing = instance.Layout.FloorSpacing,
                Width = instance.Layout.Width,
                Height = instance.Layout.Height,
                FloorCount = instance.FloorCount,
                ExitAction = (byte)(manager != null ? manager.exitPortalAction : DungeonPortal.PortalAction.CompleteDungeon),
                Slot = run.SceneLease != null ? run.SceneLease.Slot : -1,
                SlotSize = run.SceneLease != null ? run.SceneLease.Bounds.size.x : 0f,
                OwnPhysics = run.SceneLease != null && run.SceneLease.OwnPhysics,
            };
            serverHeader = DungeonLayoutCodec.EncodeHeader(header);

            // States the dungeon's objects published while it was being built.
            foreach (DungeonElement e in instance.Elements)
            {
                if (e.HasState)
                    Store(e, DungeonNet.Channel.State, e.State, e.StatePayload);
                if (e.HasPresence)
                    Store(e, DungeonNet.Channel.Presence, e.Presence, e.PresencePayload);
            }

            // Floors whose chasm falls lead to the floor below.
            RoomEventSettings m = instance.Profile != null ? instance.Profile.Mechanics : null;
            ChasmFall rule = m != null ? m.chasmFallRule : ChasmFall.Death;
            floorHasFall = new bool[instance.FloorCount];
            for (int f = 0; f < instance.FloorCount - 1; f++)
            {
                if (rule == ChasmFall.Death)
                    break;
                TileGrid g = instance.Layout.Floors[f].Grid;
                for (int c = 0; c < g.Count && !floorHasFall[f]; c++)
                    floorHasFall[f] = g.IsChasm(c);
            }

            RpcHeader(serverHeader);
            int arrival = instance.Layout.PlayerSpawn.Valid ? instance.Layout.PlayerSpawn.Floor : 0;
            RevealUpTo(Mathf.Max(0, arrival));
            nextReveal = 0f;
        }

        /// <summary>Sends the floors the party has reached (and the one below a way down they are close to).</summary>
        private void RevealReached()
        {
            DungeonInstance d = serverInstance;
            DungeonRun run = serverRun;
            if (d == null || run == null || d.Layout == null)
                return;
            int want = -1;
            IReadOnlyList<GameObject> group = run.Participants;
            for (int i = 0; i < group.Count; i++)
            {
                GameObject p = group[i];
                if (p == null || p.scene != gameObject.scene)
                    continue;
                Vector3 pos = p.transform.position;
                int f = d.FloorAt(pos);
                want = Mathf.Max(want, f);
                if (f + 1 >= d.FloorCount)
                    continue;
                if (floorHasFall != null && f < floorHasFall.Length && floorHasFall[f])
                {
                    want = Mathf.Max(want, f + 1);
                    continue;
                }
                foreach (VerticalLink link in d.Layout.Links)
                {
                    if (link == null || link.UpperFloor != f)
                        continue;
                    RectInt fp = link.Footprint;
                    Vector3 centre = d.CellToWorld(f, new Vector2(fp.x + fp.width * 0.5f, fp.y + fp.height * 0.5f), 0f);
                    float reach = revealDistance + Mathf.Max(fp.width, fp.height) * d.CellSize * 0.5f;
                    Vector2 flat = new Vector2(pos.x - centre.x, pos.z - centre.z);
                    if (flat.sqrMagnitude <= reach * reach)
                    {
                        want = Mathf.Max(want, Mathf.Min(link.LowerFloor, f + 1));
                        break;
                    }
                }
            }
            if (want >= 0)
                RevealUpTo(want);
        }

        private void RevealUpTo(int floor)
        {
            DungeonInstance d = serverInstance;
            if (d == null)
                return;
            floor = Mathf.Min(floor, d.FloorCount - 1);
            CompiledProfile profile = d.Profile;
            while (serverFloors.Count <= floor)
            {
                int f = serverFloors.Count;
                byte[] packet;
                try
                {
                    packet = DungeonLayoutCodec.EncodeFloor(d.Layout, f, KeyedBuildSeed(d.Layout, f), p => DungeonBuilder.BuiltOnClients(p, profile));
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    return;
                }
                serverFloors.Add(packet);
                RpcFloor(serverGeneration, f, packet);
                List<DungeonElementStateMsg> states = StatesUpTo(f, f);
                if (states.Count > 0)
                    RpcStates(serverGeneration, states.ToArray());
                // The floor's mobs and item pickups kept back until now are network-spawned (see DungeonNet.SpawnContent).
                DungeonNet.FloorRevealed(d, f);
            }
        }

        /// <summary>
        /// The build seed sent with floor <paramref name="floor"/>: the keyed one the server meshed with. A layout without
        /// keyed seeds (generated before the session started) would otherwise send a seed derived from the dungeon seed, so
        /// a keyed one is sent instead - its cave walls may differ slightly from the server's, its seed stays secret.
        /// </summary>
        private int KeyedBuildSeed(DungeonLayout layout, int floor)
        {
            if (layout.FloorBuildSeeds != null && floor < layout.FloorBuildSeeds.Length)
                return layout.FloorBuildSeeds[floor];
            if (!warnedUnkeyedSeeds)
            {
                warnedUnkeyedSeeds = true;
                Debug.LogWarning("[Net] This dungeon was meshed without network build seeds (generated outside a session?): its " +
                                 "clients get keyed seeds, so cave walls may differ slightly from the server's.", this);
            }
            return DungeonBuildSeed.ForFloor(layout.AttemptSeed, floor);
        }

        private List<DungeonElementStateMsg> StatesUpTo(int fromFloor, int toFloor)
        {
            var list = new List<DungeonElementStateMsg>();
            foreach (StoredState s in serverStates.Values)
                if (s.floor >= fromFloor && s.floor <= toFloor)
                    list.Add(s.msg);
            return list;
        }

        private static long Key(int id, DungeonNet.Channel channel) => ((long)id << 8) | (byte)channel;

        private void Store(DungeonElement e, DungeonNet.Channel channel, int state, byte[] payload)
        {
            serverStates[Key(e.Id, channel)] = new StoredState
            {
                msg = new DungeonElementStateMsg { id = e.Id, channel = (byte)channel, state = state, payload = payload ?? Array.Empty<byte>() },
                floor = e.Floor,
            };
        }

        /// <summary>Server: an element of this run changed (see <see cref="DungeonNet.Publish"/>).</summary>
        private void ServerPublish(DungeonElement e, DungeonNet.Channel channel, int state, byte[] payload)
        {
            if (!IsServerInitialized || e == null || e.Dungeon == null || e.Dungeon != serverInstance)
                return;
            Store(e, channel, state, payload);
            if (e.Floor < serverFloors.Count)
                RpcState(serverGeneration, new DungeonElementStateMsg { id = e.Id, channel = (byte)channel, state = state, payload = payload ?? Array.Empty<byte>() });
        }

        /// <summary>A client of the run sees this object: it gets the dungeon as far as it was sent.</summary>
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (connection == null || connection.IsLocalClient || serverHeader == null || serverInstance == null)
                return;
            builtByClient.Remove(connection.ClientId);
            TargetHeader(connection, serverHeader);
            for (int f = 0; f < serverFloors.Count; f++)
                TargetFloor(connection, serverGeneration, f, serverFloors[f]);
            List<DungeonElementStateMsg> states = StatesUpTo(0, serverFloors.Count - 1);
            if (states.Count > 0)
                TargetStates(connection, serverGeneration, states.ToArray());
        }

        public override void OnDespawnServer(NetworkConnection connection)
        {
            base.OnDespawnServer(connection);
            if (connection == null)
                return;
            builtByClient.Remove(connection.ClientId);
            lastInteract.Remove(connection.ClientId);
        }

        /// <summary>Server: may <paramref name="player"/>'s controls wait for their client (installed as <see cref="DungeonSession.ArrivalHold"/>)?</summary>
        private static bool HoldArrival(DungeonRun run, GameObject player)
        {
            if (run == null || player == null || run.SceneLease == null)
                return false;
            NetworkObject nob = NetObjects.Of(player);
            NetworkConnection owner = nob != null && nob.IsSpawned ? nob.Owner : null;
            if (owner == null || !owner.IsActive || owner.IsLocalClient)
                return false;   // unowned, or the host's own player: it sees the server's dungeon
            NetDungeonRun relayHere = ForScene(run.SceneLease.Scene);
            if (relayHere == null || !relayHere.IsServerInitialized)
                return false;   // nobody would give the controls back
            relayHere.holds.RemoveAll(h => h.player == player);
            relayHere.holds.Add(new Hold { run = run, player = player, conn = owner, until = Time.unscaledTime + ArrivalTimeout });
            return true;
        }

        private void UpdateHolds()
        {
            for (int i = holds.Count - 1; i >= 0; i--)
            {
                Hold h = holds[i];
                if (h.player == null || h.run == null || h.run.State == DungeonRunState.Ended || !h.run.IsParticipant(h.player))
                {
                    holds.RemoveAt(i);
                    continue;
                }
                if (h.run.State != DungeonRunState.Inside || serverInstance == null)
                    continue;   // still generating (next dungeon): keep waiting
                bool ready = false;
                if (h.conn != null && h.conn.IsActive && builtByClient.TryGetValue(h.conn.ClientId, out Built b) && b.generation == serverGeneration)
                    ready = b.floor >= serverInstance.FloorAt(h.player.transform.position);
                bool late = Time.unscaledTime >= h.until || h.conn == null || !h.conn.IsActive;
                if (!ready && !late)
                    continue;
                if (late && !ready)
                    Debug.LogWarning($"[Net] The client of '{h.player.name}' did not report its dungeon floor built within {ArrivalTimeout:0}s - giving the controls back anyway.", this);
                holds.RemoveAt(i);
                DungeonSession.ReleaseArrival(h.run, h.player);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerFloorBuilt(int generation, int floor, NetworkConnection conn = null)
        {
            if (conn == null || generation != serverGeneration || floor < 0 || floor >= serverFloors.Count)
                return;
            if (builtByClient.TryGetValue(conn.ClientId, out Built b) && b.generation == generation && b.floor >= floor)
                return;
            builtByClient[conn.ClientId] = new Built { generation = generation, floor = floor };
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerInteract(int generation, int elementId, NetworkConnection conn = null)
        {
            if (conn == null || generation != serverGeneration || serverInstance == null || serverRun == null || !serverRun.IsInside)
                return;
            float now = Time.unscaledTime;
            if (lastInteract.TryGetValue(conn.ClientId, out float last) && now - last < InteractInterval)
                return;   // spam
            lastInteract[conn.ClientId] = now;

            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null || !player.IsAlive)
                return;
            GameObject mover = PlayerLocator.MovableRoot(player);
            if (mover == null || !serverRun.IsParticipant(mover) || player.gameObject.scene != gameObject.scene)
                return;   // only the players of this run, inside it
            DungeonElement element = serverInstance.Element(elementId);
            if (element == null || element.Floor >= serverFloors.Count || !element.gameObject.activeInHierarchy)
                return;   // unknown, on a floor the client was never sent, or hidden
            IDungeonNetInteract target = element.GetComponentInChildren<IDungeonNetInteract>(true);
            if (target == null)
                return;
            Vector3 d = player.transform.position - element.transform.position;
            float range = Mathf.Max(0f, target.NetInteractRange) + RangeTolerance;
            if (Mathf.Abs(d.y) > 3f || new Vector2(d.x, d.z).sqrMagnitude > range * range)
                return;   // too far from the server's positions
            try
            {
                target.NetInteract(player);
            }
            catch (Exception e)
            {
                Debug.LogException(e, element);
            }
        }

        // ================================================================== client

        private int clientGeneration;
        private DungeonNetHeader clientHeader;
        private DungeonLayout replicaLayout;
        private CompiledProfile replicaProfile;
        private DungeonBuilder replicaBuilder;
        private DungeonObjectPool replicaPool;
        private GameObject replicaParent;
        private bool replicaBroken;
        private readonly SortedDictionary<int, DungeonNetFloor> pendingFloors = new SortedDictionary<int, DungeonNetFloor>();
        private readonly Dictionary<long, DungeonElementStateMsg> pendingStates = new Dictionary<long, DungeonElementStateMsg>();
        private Coroutine buildRoutine;
        private float lastClientInteract;

        [ObserversRpc(ExcludeServer = true)]
        private void RpcHeader(byte[] data) => ClientHeader(data);

        [TargetRpc]
        private void TargetHeader(NetworkConnection conn, byte[] data) => ClientHeader(data);

        [ObserversRpc(ExcludeServer = true)]
        private void RpcFloor(int generation, int floor, byte[] data) => ClientFloor(generation, floor, data);

        [TargetRpc]
        private void TargetFloor(NetworkConnection conn, int generation, int floor, byte[] data) => ClientFloor(generation, floor, data);

        [ObserversRpc(ExcludeServer = true)]
        private void RpcState(int generation, DungeonElementStateMsg state) => ClientState(generation, state);

        [ObserversRpc(ExcludeServer = true)]
        private void RpcStates(int generation, DungeonElementStateMsg[] states) => ClientStates(generation, states);

        [TargetRpc]
        private void TargetStates(NetworkConnection conn, int generation, DungeonElementStateMsg[] states) => ClientStates(generation, states);

        [ObserversRpc(ExcludeServer = true)]
        private void RpcMessage(string text, bool important)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 512)
                return;
            CombatEntity local = PlayerLocator.Local;
            if (local == null || local.gameObject.scene == gameObject.scene)
                DungeonMessages.Show(text, important);
        }

        private void ClientHeader(byte[] data)
        {
            if (IsServerInitialized)
                return;
            DungeonNetHeader h = DungeonLayoutCodec.DecodeHeader(data);
            if (h == null)
            {
                Debug.LogError("[Net] Received a malformed dungeon header.", this);
                return;
            }
            if (h.Generation == clientGeneration && replicaBuilder != null)
                return;   // already building this one (a late-join copy of what arrived)
            ClearReplica();
            clientGeneration = h.Generation;
            clientHeader = h;
            replicaBroken = false;
            DungeonScenes.PlaceRemote(gameObject.scene, h.Slot, h.Origin, h.SlotSize);

            DungeonProfile profile = string.IsNullOrEmpty(h.Profile) ? null : DungeonProfile.FindLoaded(h.Profile);
            if (!string.IsNullOrEmpty(h.Profile) && profile == null)
            {
                replicaBroken = true;
                Debug.LogError($"[Net] The dungeon profile '{h.Profile}' is not loaded on this client, so the dungeon cannot be shown. " +
                               "Keep the profile referenced by a loaded scene or asset, or put it in a Resources folder.", this);
                return;
            }
            replicaProfile = CompiledProfile.Compile(profile);
            replicaLayout = DungeonLayoutCodec.CreateReplicaLayout(h);
            replicaPool = new DungeonObjectPool();
            replicaParent = new GameObject("Dungeon (network copy)");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replicaParent, gameObject.scene);
            replicaParent.transform.SetPositionAndRotation(h.Origin, h.Rotation);
            replicaBuilder = new DungeonBuilder(replicaProfile, replicaLayout, new List<FloorMeshData>(), replicaPool);
            replicaBuilder.BeginReplica(replicaParent.transform, h.FloorCount, h.Label, new DungeonBuilder.Settings
            {
                Replica = true,
                ExitAction = (DungeonPortal.PortalAction)h.ExitAction,
            });
        }

        private void ClientFloor(int generation, int floor, byte[] data)
        {
            if (IsServerInitialized || generation != clientGeneration || clientHeader == null)
                return;
            if (replicaBroken)
            {
                // Nothing can be shown, but the player must not stay frozen waiting for it.
                ServerFloorBuilt(generation, floor);
                return;
            }
            if (replicaLayout == null || floor < replicaLayout.Floors.Count || pendingFloors.ContainsKey(floor))
                return;   // already have it
            DungeonNetFloor decoded = DungeonLayoutCodec.DecodeFloor(data, clientHeader.Width, clientHeader.Height);
            if (decoded == null || decoded.Floor.Index != floor || floor >= clientHeader.FloorCount)
            {
                Debug.LogError($"[Net] Received a malformed dungeon floor ({floor}).", this);
                return;
            }
            pendingFloors[floor] = decoded;
            if (buildRoutine == null)
                buildRoutine = StartCoroutine(BuildFloors(generation));
        }

        /// <summary>Builds the received floors in order: merged into the layout, meshed on a worker thread, then built.</summary>
        private IEnumerator BuildFloors(int generation)
        {
            while (generation == clientGeneration && replicaBuilder != null && pendingFloors.TryGetValue(replicaLayout.Floors.Count, out DungeonNetFloor next))
            {
                int f = next.Floor.Index;
                pendingFloors.Remove(f);
                if (!DungeonLayoutCodec.Merge(replicaLayout, next))
                    break;

                // Mesh the floor away from the main thread; nothing touches the layout meanwhile (floors build one by one).
                var job = new MeshJob { Layout = replicaLayout, Profile = replicaProfile, Floor = f };
                if (Application.isPlaying)
                    DungeonWorkers.Pool.Enqueue(job.Run);
                else
                    job.Run();
                while (!job.Done)
                    yield return null;
                if (generation != clientGeneration || replicaBuilder == null)
                    break;
                FloorMeshData meshes = job.Result;
                if (job.Error != null || meshes == null)
                {
                    Debug.LogException(job.Error ?? new InvalidOperationException("No mesh data."), this);
                    ServerFloorBuilt(generation, f);
                    break;
                }

                replicaBuilder.SetFloorMeshes(f, meshes);
                IEnumerator build = replicaBuilder.BuildReplicaFloor(f);
                while (true)
                {
                    bool more;
                    try
                    {
                        more = build.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, this);
                        more = false;
                    }
                    if (!more)
                        break;
                    yield return build.Current;
                    if (generation != clientGeneration || replicaBuilder == null)
                        yield break;
                }
                ApplyPendingStates();
                ServerFloorBuilt(generation, f);
            }
            buildRoutine = null;
            // A floor that arrived while the last one was finishing.
            if (generation == clientGeneration && replicaBuilder != null && replicaLayout != null &&
                pendingFloors.ContainsKey(replicaLayout.Floors.Count))
                buildRoutine = StartCoroutine(BuildFloors(generation));
        }

        /// <summary>One floor's meshing on a worker thread.</summary>
        private sealed class MeshJob
        {
            public DungeonLayout Layout;
            public CompiledProfile Profile;
            public int Floor;
            public FloorMeshData Result;
            public Exception Error;
            private volatile bool done;

            public bool Done => done;

            public void Run()
            {
                try
                {
                    Result = DungeonMeshing.BuildFloor(Layout, Profile, Floor);
                }
                catch (Exception e)
                {
                    Error = e;
                }
                finally
                {
                    done = true;
                }
            }
        }

        private void ClientState(int generation, DungeonElementStateMsg state)
        {
            if (IsServerInitialized || generation != clientGeneration)
                return;
            ApplyOrKeep(state);
        }

        private void ClientStates(int generation, DungeonElementStateMsg[] states)
        {
            if (IsServerInitialized || generation != clientGeneration || states == null)
                return;
            foreach (DungeonElementStateMsg s in states)
                ApplyOrKeep(s);
        }

        private void ApplyOrKeep(DungeonElementStateMsg s)
        {
            if (s.channel > (byte)DungeonNet.Channel.Presence)
                return;
            DungeonInstance replica = Replica;
            DungeonElement e = replica != null ? replica.Element(s.id) : null;
            if (e == null || !replicaBuilder.IsFloorBuilt(e.Floor))
            {
                pendingStates[Key(s.id, (DungeonNet.Channel)s.channel)] = s;   // its floor is not built yet
                return;
            }
            DungeonNet.Apply(e, (DungeonNet.Channel)s.channel, s.state, s.payload);
        }

        private void ApplyPendingStates()
        {
            if (pendingStates.Count == 0 || Replica == null)
                return;
            var ready = new List<long>();
            foreach (KeyValuePair<long, DungeonElementStateMsg> kv in pendingStates)
            {
                DungeonElement e = Replica.Element(kv.Value.id);
                if (e != null && replicaBuilder.IsFloorBuilt(e.Floor))
                    ready.Add(kv.Key);
            }
            foreach (long key in ready)
            {
                DungeonElementStateMsg s = pendingStates[key];
                pendingStates.Remove(key);
                DungeonNet.Apply(Replica.Element(s.id), (DungeonNet.Channel)s.channel, s.state, s.payload);
            }
        }

        /// <summary>Removes the client's copy (a new dungeon replaces it, or the run's scene goes).</summary>
        private void ClearReplica()
        {
            if (buildRoutine != null)
            {
                StopCoroutine(buildRoutine);
                buildRoutine = null;
            }
            pendingFloors.Clear();
            pendingStates.Clear();
            if (replicaBuilder != null)
                replicaBuilder.Teardown();
            replicaBuilder = null;
            if (replicaParent != null)
                Destroy(replicaParent);
            replicaParent = null;
            replicaPool?.Clear();
            replicaPool = null;
            replicaLayout = null;
            replicaProfile = null;
            clientHeader = null;
        }

        /// <summary>Client: asks the server to use <paramref name="element"/> for this machine's player.</summary>
        private bool ClientRequestInteract(DungeonElement element)
        {
            if (IsServerInitialized || !IsClientInitialized || element == null || element.Dungeon != Replica || clientGeneration == 0)
                return false;
            if (Time.unscaledTime - lastClientInteract < InteractInterval)
                return true;   // just sent one
            lastClientInteract = Time.unscaledTime;
            ServerInteract(clientGeneration, element.Id);
            return true;
        }

        /// <summary>
        /// Puts the dungeon in front of the player's interaction relay (<see cref="global::Player.NetInteractHandler"/>):
        /// a dungeon element goes to its run's relay, anything else to the handler that was there before.
        /// </summary>
        private static void InstallInteractRoute()
        {
            if (interactRoute == null)
                interactRoute = RouteInteract;
            if (global::Player.NetInteractHandler == interactRoute)
                return;
            chainedInteract = global::Player.NetInteractHandler;
            global::Player.NetInteractHandler = interactRoute;
        }

        private static bool RouteInteract(global::Player player, Interactable target)
        {
            if (target != null && DungeonNet.TryRelayInteract(target, player != null ? player.gameObject : null))
                return true;
            Func<global::Player, Interactable, bool> next = chainedInteract;
            return next != null && next != interactRoute && next(player, target);
        }

        // ================================================================== the relay

        /// <summary>Routes <see cref="DungeonNet"/>'s calls to the relay of the element's run scene.</summary>
        private sealed class Relay : DungeonNet.IRelay
        {
            public void PublishState(DungeonElement element, DungeonNet.Channel channel, int state, byte[] payload)
            {
                if (element == null || element.Dungeon == null)
                    return;
                NetDungeonRun r = ForScene(element.Dungeon.gameObject.scene);
                if (r != null)
                    r.ServerPublish(element, channel, state, payload);
            }

            public void Message(DungeonInstance dungeon, string text, bool important)
            {
                if (dungeon == null || string.IsNullOrEmpty(text))
                    return;
                NetDungeonRun r = ForScene(dungeon.gameObject.scene);
                if (r != null && r.IsServerInitialized && r.serverInstance == dungeon)
                    r.RpcMessage(text.Length > 512 ? text.Substring(0, 512) : text, important);
            }

            public bool RequestInteract(DungeonElement element, GameObject player)
            {
                if (element == null || element.Dungeon == null)
                    return false;
                NetDungeonRun r = ForScene(element.Dungeon.gameObject.scene);
                return r != null && r.ClientRequestInteract(element);
            }

            public bool IsFloorRevealed(DungeonInstance dungeon, int floor)
            {
                if (dungeon == null)
                    return true;
                NetDungeonRun r = ForScene(dungeon.gameObject.scene);
                if (r == null || !r.IsServerInitialized)
                    return true;   // no relay serves this scene: nothing would ever reveal the floor
                // A dungeon the relay has not taken up yet (it is still being built) has no floor sent yet.
                return r.serverInstance == dungeon && floor >= 0 && floor < r.serverFloors.Count;
            }
        }
    }
}
#endif
