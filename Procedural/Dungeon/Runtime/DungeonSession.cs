using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProceduralDungeon
{
    /// <summary>How a dungeon run ended.</summary>
    public enum DungeonRunEnd
    {
        /// <summary>The group left through the entrance portal (or the game ended the run).</summary>
        Left,
        /// <summary>The group left through the exit portal with the dungeon completed.</summary>
        Completed,
        /// <summary>Generation failed: the group never got in.</summary>
        Failed,
    }

    public enum DungeonRunState
    {
        /// <summary>The dungeon is being generated; the group waits at the portal with controls off.</summary>
        Entering,
        /// <summary>The group is inside.</summary>
        Inside,
        /// <summary>Over: the group was sent back (or on, through <see cref="DungeonSession.ExitHandler"/>).</summary>
        Ended,
    }

    /// <summary>
    /// Extra options of a dungeon entry: a run others may join (shared and linked portals), a player limit and where
    /// the entering group arrives.
    /// </summary>
    public sealed class DungeonEntryOptions
    {
        /// <summary>
        /// Runs started with a key can be joined while they last (see <see cref="DungeonSession.FindShared"/> and
        /// <see cref="DungeonSession.Join"/>); portals that share a key lead into the same run. Null = a private run.
        /// </summary>
        public string ShareKey;
        /// <summary>Most players in the run at once (0 = no limit). The group entering is cut to it.</summary>
        public int MaxPlayers;
        /// <summary>Where the entering group arrives: 0 = the dungeon's spawn; 1, 2... = <see cref="DungeonEntrances"/>.</summary>
        public int Entrance;
    }

    /// <summary>
    /// One group's visit to one dungeon: its manager (and scene, when <see cref="DungeonScenes.Enabled"/>), the players in
    /// it, where each one goes back to, and its checkpoint. Several runs can exist at once (one per group) when dungeons
    /// are built in scenes of their own. A shared run (<see cref="ShareKey"/>) takes more players while it lasts.
    /// </summary>
    public sealed class DungeonRun
    {
        internal struct Traveller
        {
            public GameObject root;
            public Vector3 returnPosition;
            public Quaternion returnRotation;
            public Scene returnScene;
            /// <summary>Where this player arrives (0 = the dungeon's spawn, see <see cref="DungeonEntrances"/>).</summary>
            public int entrance;
        }

        private static int nextId = 1;

        internal readonly List<Traveller> travellers = new List<Traveller>(4);
        internal readonly List<GameObject> participants = new List<GameObject>(4);
        internal bool ownsManager;
        internal bool pausedWorld;
        internal bool atmosphereApplied;
        internal Action<DungeonInstance> onReady;
        internal Action<string> onFailed;

        internal DungeonRun()
        {
            Id = nextId++;
            StartTime = Time.time;
        }

        /// <summary>Unique id of this run (for logs and your own bookkeeping).</summary>
        public int Id { get; }
        public DungeonRunState State { get; internal set; } = DungeonRunState.Entering;
        public bool IsEntering => State == DungeonRunState.Entering;
        public bool IsInside => State == DungeonRunState.Inside;
        public DungeonManager Manager { get; internal set; }
        public DungeonRequest Request { get; internal set; }
        public DungeonInstance Instance => Manager != null ? Manager.Current : null;
        /// <summary>The scene of this dungeon when it has one of its own (null in the classic mode).</summary>
        public DungeonScenes.Lease SceneLease { get; internal set; }
        /// <summary>The scene the dungeon is built in.</summary>
        public Scene Scene => SceneLease != null ? SceneLease.Scene : Manager != null ? Manager.gameObject.scene : default;
        /// <summary>Time.time when the group entered the portal.</summary>
        public float StartTime { get; }
        /// <summary>How the run ended (valid once <see cref="State"/> is Ended).</summary>
        public DungeonRunEnd EndReason { get; internal set; }

        /// <summary>The players in the run (the one who entered first).</summary>
        public IReadOnlyList<GameObject> Participants => participants;

        /// <summary>The key players join this run with (shared and linked portals), or null for a private run.</summary>
        public string ShareKey { get; internal set; }

        /// <summary>Most players in the run at once (0 = no limit).</summary>
        public int MaxPlayers { get; internal set; }

        /// <summary>Players in the run now (those who left the game are not counted).</summary>
        public int PlayerCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < participants.Count; i++)
                    if (participants[i] != null)
                        n++;
                return n;
            }
        }

        /// <summary>The run has reached <see cref="MaxPlayers"/>.</summary>
        public bool IsFull => MaxPlayers > 0 && PlayerCount >= MaxPlayers;

        /// <summary>The run is over (run, how). Raised once, after every player was sent back.</summary>
        public event Action<DungeonRun, DungeonRunEnd> Ended;

        /// <summary>The run's checkpoint (set by rest fountains), null until one is used.</summary>
        public Pose? Checkpoint { get; private set; }

        /// <summary>Raised when a rest point sets a new checkpoint.</summary>
        public event Action<Pose> CheckpointSet;

        public void SetCheckpoint(Pose pose)
        {
            Checkpoint = pose;
            CheckpointSet?.Invoke(pose);
            DungeonSession.RaiseCheckpointSet(pose);
        }

        internal void ClearCheckpoint() => Checkpoint = null;

        /// <summary>Is <paramref name="go"/> (or the player it belongs to) in this run?</summary>
        public bool IsParticipant(GameObject go)
        {
            if (go == null)
                return false;
            for (int i = 0; i < participants.Count; i++)
            {
                GameObject p = participants[i];
                if (p != null && (go == p || go.transform.IsChildOf(p.transform) || p.transform.IsChildOf(go.transform)))
                    return true;
            }
            return false;
        }

        /// <summary>Is this machine's player in this run? (Single player: the player.)</summary>
        public bool HasLocalPlayer
        {
            get
            {
                CombatEntity local = PlayerLocator.Local;
                return local != null && IsParticipant(local.gameObject);
            }
        }

        /// <summary>The participant closest to <paramref name="position"/>, or null.</summary>
        public Transform NearestParticipant(Vector3 position)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < participants.Count; i++)
            {
                GameObject p = participants[i];
                if (p == null) continue;
                float d = (p.transform.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p.transform; }
            }
            return best;
        }

        /// <summary>Where <paramref name="who"/> returns to (in front of the world portal) and the scene that was in.</summary>
        public bool TryGetReturn(GameObject who, out Pose pose, out Scene scene)
        {
            for (int i = 0; i < travellers.Count; i++)
            {
                Traveller t = travellers[i];
                if (t.root != null && who != null && (t.root == who || who.transform.IsChildOf(t.root.transform) || t.root.transform.IsChildOf(who.transform)))
                {
                    pose = new Pose(t.returnPosition, t.returnRotation);
                    scene = t.returnScene;
                    return true;
                }
            }
            pose = default;
            scene = default;
            return false;
        }

        internal void RaiseEnded(DungeonRunEnd how) => Ended?.Invoke(this, how);
    }

    /// <summary>
    /// Moves players between the world and dungeons. Each group that enters a portal gets a <see cref="DungeonRun"/>:
    /// <list type="number">
    /// <item>Enter: remembers where each player stood, freezes their controls, starts generation (and, in the classic
    /// mode, pauses the world - terrain streaming, weather, world spawners - so nothing keeps generating above the
    /// dungeon).</item>
    /// <item>Ready: moves the group to the spawn in front of the entrance portal (into the dungeon's own scene when
    /// <see cref="DungeonScenes.Enabled"/>), applies the theme's atmosphere for the local player, gives controls back.</item>
    /// <item>Failure: undoes all of it and puts the group back - controls are never left disabled.</item>
    /// <item>Exit (portals): sends the group back (or hands each player to <see cref="ExitHandler"/>), clears the dungeon
    /// and unloads its scene.</item>
    /// </list>
    /// The static members without a run (<see cref="InDungeon"/>, <see cref="Current"/>...) are about the local player's
    /// run (single player: the only one), so older code keeps working.
    /// </summary>
    public static class DungeonSession
    {
        /// <summary>
        /// Takes over where a player goes when their run ends (e.g. back to the city because the world they came from is
        /// gone). Return true when you moved the player: you must take them out of the dungeon's scene right away (it is
        /// unloaded next) and give them their controls back when they arrive. False = the default (back in front of
        /// the world portal).
        /// </summary>
        public interface IExitHandler
        {
            bool TakePlayer(DungeonRun run, GameObject player, Pose returnPose, Scene returnScene, DungeonRunEnd how);
        }

        /// <summary>Height above the spawn point the player is placed at (they drop onto the floor).</summary>
        public static float SpawnLift = 1f;

        /// <summary>Seconds after leaving a dungeon during which portals don't send the player back in.</summary>
        public static float ReentryCooldown = 3f;

        /// <summary>See <see cref="IExitHandler"/>. Null = every player goes back in front of the world portal.</summary>
        public static IExitHandler ExitHandler { get; set; }

        /// <summary>
        /// Network sessions: may a player who arrives in a run's dungeon get their controls only later? Return true to keep
        /// them frozen - the network layer (GameNet.NetDungeonRun) then calls <see cref="ReleaseArrival"/> once that
        /// player's client has built the floor they stand on (or after a timeout). Null offline: controls come back at once.
        /// </summary>
        public static Func<DungeonRun, GameObject, bool> ArrivalHold;

        private static readonly List<DungeonRun> runs = new List<DungeonRun>(4);
        private static readonly Dictionary<GameObject, float> lastExit = new Dictionary<GameObject, float>();
        private static float lastExitAny = -100f;

        /// <summary>Every run in progress.</summary>
        public static IReadOnlyList<DungeonRun> Runs => runs;

        // ------------------------------------------------------------------ events (any run)

        /// <summary>A group is in a freshly built dungeon.</summary>
        public static event Action<DungeonInstance> Entered;
        /// <summary>A group left (true = through the exit portal after completing it).</summary>
        public static event Action<bool> Exited;
        public static event Action<string> Failed;
        /// <summary>A run started (the group stepped into a portal; generation begins).</summary>
        public static event Action<DungeonRun> RunStarted;
        /// <summary>A run's group is inside (first floors playable).</summary>
        public static event Action<DungeonRun> RunEntered;
        /// <summary>A run is over (after its players were sent back).</summary>
        public static event Action<DungeonRun, DungeonRunEnd> RunEnded;
        /// <summary>
        /// A player joined a run whose group was already inside (<see cref="Join"/>) and was just placed in the dungeon.
        /// Players who join while the dungeon is still being generated arrive with the group (<see cref="RunEntered"/>).
        /// </summary>
        public static event Action<DungeonRun, GameObject> PlayerJoined;
        /// <summary>Raised when a rest point sets a new checkpoint (any run).</summary>
        public static event Action<Pose> CheckpointSet;

        // Static state survives play-mode entry when domain reload is disabled: start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            runs.Clear();
            lastExit.Clear();
            lastExitAny = -100f;
            ExitHandler = null;
            ArrivalHold = null;
            Entered = null;
            Exited = null;
            Failed = null;
            RunStarted = null;
            RunEntered = null;
            RunEnded = null;
            PlayerJoined = null;
            CheckpointSet = null;
        }

        // ------------------------------------------------------------------ finding runs

        /// <summary>The run <paramref name="who"/> (a player or any of its objects) is in, or null.</summary>
        public static DungeonRun RunOf(GameObject who)
        {
            if (who == null)
                return null;
            for (int i = 0; i < runs.Count; i++)
                if (runs[i].IsParticipant(who))
                    return runs[i];
            return null;
        }

        /// <summary>The run that built <paramref name="dungeon"/>, or null.</summary>
        public static DungeonRun RunOf(DungeonInstance dungeon)
        {
            if (dungeon == null)
                return null;
            for (int i = 0; i < runs.Count; i++)
                if (runs[i].Instance == dungeon || (runs[i].Manager != null && dungeon.Manager == runs[i].Manager))
                    return runs[i];
            return null;
        }

        /// <summary>The run whose dungeon <paramref name="inside"/> belongs to (a trap, a portal, a fountain...), or null.</summary>
        public static DungeonRun RunAt(Component inside)
        {
            if (inside == null)
                return null;
            DungeonRun run = RunOf(inside.GetComponentInParent<DungeonInstance>());
            if (run != null)
                return run;
            Scene scene = inside.gameObject.scene;
            for (int i = 0; i < runs.Count; i++)
                if (runs[i].SceneLease != null && runs[i].Scene == scene)
                    return runs[i];
            return null;
        }

        /// <summary>The run that is going with <paramref name="shareKey"/> (see <see cref="DungeonEntryOptions.ShareKey"/>), or null.</summary>
        public static DungeonRun FindShared(string shareKey)
        {
            if (string.IsNullOrEmpty(shareKey))
                return null;
            for (int i = 0; i < runs.Count; i++)
                if (runs[i].State != DungeonRunState.Ended && runs[i].ShareKey == shareKey)
                    return runs[i];
            return null;
        }

        /// <summary>The local player's run, else the first one (single player: the only one), or null.</summary>
        public static DungeonRun FocusRun
        {
            get
            {
                if (runs.Count == 0)
                    return null;
                CombatEntity local = PlayerLocator.Local;
                if (local != null)
                {
                    DungeonRun mine = RunOf(local.gameObject);
                    if (mine != null)
                        return mine;
                }
                return runs[0];
            }
        }

        // ------------------------------------------------------------------ the local player's run (older API)

        public static bool InDungeon => FocusRun != null && FocusRun.IsInside;
        public static bool IsEntering => FocusRun != null && FocusRun.IsEntering;
        public static DungeonManager Manager => FocusRun?.Manager;
        public static DungeonRequest CurrentRequest => FocusRun?.Request;
        public static DungeonInstance Current => FocusRun?.Instance;

        /// <summary>The players of the local player's run (empty outside dungeons).</summary>
        public static IReadOnlyList<GameObject> Participants => FocusRun != null ? FocusRun.Participants : (IReadOnlyList<GameObject>)Array.Empty<GameObject>();

        /// <summary>Is <paramref name="go"/> (or the player it belongs to) in any dungeon?</summary>
        public static bool IsParticipant(GameObject go) => RunOf(go) != null;

        /// <summary>The participant of any run closest to <paramref name="position"/> (null when nobody is in a dungeon).</summary>
        public static Transform NearestParticipant(Vector3 position)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < runs.Count; i++)
            {
                Transform t = runs[i].NearestParticipant(position);
                if (t == null) continue;
                float d = (t.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            return best;
        }

        /// <summary>The checkpoint of the local player's run (set by rest fountains), null until one is used.</summary>
        public static Pose? Checkpoint => FocusRun?.Checkpoint;

        /// <summary>Sets the checkpoint of the local player's run (prefer <see cref="DungeonRun.SetCheckpoint"/>).</summary>
        public static void SetCheckpoint(Pose pose) => FocusRun?.SetCheckpoint(pose);

        internal static void RaiseCheckpointSet(Pose pose) => CheckpointSet?.Invoke(pose);

        /// <summary>False right after the local player left a dungeon (so standing in the world portal doesn't re-enter it).</summary>
        public static bool CanEnter
        {
            get
            {
                CombatEntity local = PlayerLocator.Local;
                if (local != null)
                    return CanEnterFor(PlayerLocator.MovableRoot(local));
                return (DungeonScenes.Enabled || runs.Count == 0) && Time.time - lastExitAny >= ReentryCooldown;
            }
        }

        /// <summary>
        /// May <paramref name="who"/> start a dungeon now? Not when already in one, right after leaving one, or - in the
        /// classic mode, where every dungeon is built at the same place - while another group is in one.
        /// </summary>
        public static bool CanEnterFor(GameObject who)
        {
            if (who == null || RunOf(who) != null)
                return false;
            if (!DungeonScenes.Enabled && !DungeonScenes.LoadsThroughNetwork && runs.Count > 0)
                return false;
            return !lastExit.TryGetValue(who, out float t) || Time.time - t >= ReentryCooldown;
        }

        // ------------------------------------------------------------------ entering

        /// <summary>
        /// Sends <paramref name="who"/> into a dungeon. <paramref name="managerOrPrefab"/> may be a DungeonManager in the
        /// scene, a prefab with one (instantiated at <paramref name="origin"/>), or null (a manager is created using
        /// <paramref name="profile"/>). Returns the run, or null when it could not start.
        /// </summary>
        public static DungeonRun Enter(GameObject who, DungeonRequest request, DungeonManager managerOrPrefab = null, DungeonProfile profile = null,
            Vector3? origin = null, Pose? returnPose = null)
        {
            if (who == null)
                return null;
            return Enter(new[] { who }, request, managerOrPrefab, profile, origin, returnPose);
        }

        /// <summary>
        /// Sends a group into a dungeon (the player who entered first, then the party members travelling with them).
        /// Everyone is placed around the spawn and comes back around <paramref name="returnPose"/>. With
        /// <see cref="DungeonScenes.Enabled"/> the dungeon gets a scene (and slot) of its own and <paramref name="origin"/>
        /// is ignored. Returns the run, or null when it could not start.
        /// </summary>
        public static DungeonRun Enter(IReadOnlyList<GameObject> group, DungeonRequest request, DungeonManager managerOrPrefab = null,
            DungeonProfile profile = null, Vector3? origin = null, Pose? returnPose = null) =>
            Enter(group, request, managerOrPrefab, profile, origin, returnPose, null);

        /// <summary>
        /// <see cref="Enter(IReadOnlyList{GameObject}, DungeonRequest, DungeonManager, DungeonProfile, Vector3?, Pose?)"/>
        /// with <paramref name="options"/>: a run others may join later (its <see cref="DungeonEntryOptions.ShareKey"/>),
        /// a player limit (the group is cut to it) and the entrance the group arrives at.
        /// </summary>
        public static DungeonRun Enter(IReadOnlyList<GameObject> group, DungeonRequest request, DungeonManager managerOrPrefab,
            DungeonProfile profile, Vector3? origin, Pose? returnPose, DungeonEntryOptions options)
        {
            if (group == null || group.Count == 0 || group[0] == null)
                return null;
            if (!GameNet.NetAuthority.IsServer)
                return null;   // a network client never starts runs: the server does, for its copies of the players
            GameObject leader = group[0];

            // Called for a group already inside: they go on to another dungeon (older code used Enter for this).
            DungeonRun current = RunOf(leader);
            if (current != null)
            {
                if (current.IsInside)
                    Advance(current, request);
                return current;
            }
            if (!CanEnterFor(leader))
                return null;

            var run = new DungeonRun();
            if (options != null)
            {
                run.ShareKey = string.IsNullOrEmpty(options.ShareKey) ? null : options.ShareKey;
                run.MaxPlayers = Mathf.Max(0, options.MaxPlayers);
            }
            int entrance = options != null ? Mathf.Max(0, options.Entrance) : 0;
            Vector3 basePos = returnPose.HasValue ? returnPose.Value.position : leader.transform.position;
            Quaternion baseRot = returnPose.HasValue ? returnPose.Value.rotation : leader.transform.rotation;
            for (int i = 0; i < group.Count; i++)
            {
                GameObject g = group[i];
                if (g == null || run.participants.Contains(g) || (i > 0 && RunOf(g) != null))
                    continue; // already in another dungeon: stays behind
                if (run.MaxPlayers > 0 && run.participants.Count >= run.MaxPlayers)
                    break; // the dungeon takes no more: the rest of the group stays behind
                run.travellers.Add(new DungeonRun.Traveller
                {
                    root = g,
                    returnPosition = basePos + Offset(run.travellers.Count, baseRot),
                    returnRotation = baseRot,
                    returnScene = g.scene,
                    entrance = entrance,
                });
                run.participants.Add(g);
            }
            SetControls(run, false);

            if (DungeonScenes.LoadsThroughNetwork)
            {
                // A network server: the run's scene is a stacked copy of the network template, loaded through Fish-Net
                // (always - a dungeon built into a shared realm scene could not be shown to its players only). The run
                // waits (Entering, controls off) until it exists, then generates as usual.
                string netLabel = request != null && !string.IsNullOrEmpty(request.label) ? request.label : "";
                runs.Add(run);
                run.Request = SecretRequest(request);
                RunStarted?.Invoke(run);
                DungeonManager template = managerOrPrefab;
                DungeonProfile chosen = profile;
                DungeonScenes.CreateAsync(netLabel, lease => OnLeaseReady(run, lease, template, chosen));
                return run;
            }

            if (GameNet.NetAuthority.IsNetworked)
                Debug.LogWarning("DungeonSession: a network session without a dungeon instance scene (Dungeon Scene Settings > " +
                                 "Network Scene Name) builds dungeons on the server only - the players' clients will not see them.");
            if (DungeonScenes.Enabled)
            {
                string label = request != null && !string.IsNullOrEmpty(request.label) ? request.label : "";
                run.SceneLease = DungeonScenes.Create(label);
                if (run.SceneLease == null)
                {
                    SetControls(run, true);
                    return null;
                }
                run.Manager = CreateManagerInScene(managerOrPrefab, profile, run.SceneLease);
                run.ownsManager = true;
            }
            else
            {
                run.Manager = ResolveManager(managerOrPrefab, profile, origin ?? new Vector3(0f, -10000f, 0f), out bool owns);
                run.ownsManager = owns;
            }
            if (profile != null)
                run.Manager.profile = profile;

            // Classic mode: the dungeon is under the world, so the world is paused while anyone is inside (not in a
            // network session: the world is shared, one group's dungeon must not freeze it for everybody else).
            if (run.SceneLease == null && !DungeonWorldPause.IsPaused && !GameNet.NetAuthority.IsNetworked)
            {
                DungeonWorldPause.Pause(run.Manager.pauseWhileInside, run.Manager.hideWhileInside);
                run.pausedWorld = true;
            }

            runs.Add(run);
            run.Request = SecretRequest(request);
            RunStarted?.Invoke(run);
            Generate(run);
            return run;
        }

        /// <summary>
        /// The run's own copy of a request. In a network session the seed is replaced by a keyed one (see
        /// GameNet.DungeonBuildSeed.SecretDungeonSeed): a world portal's seed comes from the world seed, which clients
        /// know, so they could otherwise generate the dungeon themselves. Single player keeps the seed as it is.
        /// </summary>
        private static DungeonRequest SecretRequest(DungeonRequest request)
        {
            DungeonRequest copy = request != null ? request.Clone() : new DungeonRequest();
            if (GameNet.NetAuthority.IsNetworked && GameNet.NetAuthority.IsServer)
                copy.seed = GameNet.DungeonBuildSeed.SecretDungeonSeed(copy.seed);
            return copy;
        }

        /// <summary>A network run's scene has loaded (or failed to): its manager is created there and generation starts.</summary>
        private static void OnLeaseReady(DungeonRun run, DungeonScenes.Lease lease, DungeonManager template, DungeonProfile profile)
        {
            if (run.State == DungeonRunState.Ended)
            {
                DungeonScenes.Release(lease);   // the run was ended while its scene loaded
                return;
            }
            if (lease == null)
            {
                End(run, DungeonRunEnd.Failed, "No dungeon scene could be loaded.");
                return;
            }
            run.SceneLease = lease;
            run.Manager = CreateManagerInScene(template, profile, lease);
            run.ownsManager = true;
            if (profile != null)
                run.Manager.profile = profile;
            Generate(run);
        }

        // ------------------------------------------------------------------ joining a run that is going

        /// <summary>
        /// Can <paramref name="who"/> join <paramref name="run"/> now? Not when the run is over or full, when they are in a
        /// dungeon already, or right after they left one.
        /// </summary>
        public static bool CanJoin(DungeonRun run, GameObject who)
        {
            if (run == null || who == null || run.State == DungeonRunState.Ended || run.IsFull || RunOf(who) != null)
                return false;
            return !lastExit.TryGetValue(who, out float t) || Time.time - t >= ReentryCooldown;
        }

        /// <summary>
        /// Adds players to a run that is going (a shared or linked portal entered again): each comes back around
        /// <paramref name="returnPose"/> when the run ends and arrives at <paramref name="entrance"/> (0 = the dungeon's
        /// spawn, 1, 2... = <see cref="DungeonEntrances"/>). While the dungeon is still being generated they wait with the
        /// group and arrive with it; once it is inside they are placed in the dungeon at once (in a network session their
        /// client loads the run's scene and is sent the floors reached so far). Players over <see cref="DungeonRun.MaxPlayers"/>,
        /// already in a dungeon or just out of one stay behind. Server only. Returns how many joined.
        /// </summary>
        public static int Join(DungeonRun run, IReadOnlyList<GameObject> group, Pose? returnPose = null, int entrance = 0)
        {
            if (run == null || group == null || run.State == DungeonRunState.Ended || !GameNet.NetAuthority.IsServer)
                return 0;
            int joined = 0;
            entrance = Mathf.Max(0, entrance);
            for (int i = 0; i < group.Count; i++)
            {
                GameObject g = group[i];
                if (!CanJoin(run, g))
                    continue;
                Vector3 basePos = returnPose.HasValue ? returnPose.Value.position : g.transform.position;
                Quaternion baseRot = returnPose.HasValue ? returnPose.Value.rotation : g.transform.rotation;
                var t = new DungeonRun.Traveller
                {
                    root = g,
                    returnPosition = basePos + Offset(joined, baseRot),
                    returnRotation = baseRot,
                    returnScene = g.scene,
                    entrance = entrance,
                };
                run.travellers.Add(t);
                run.participants.Add(g);
                SetControls(g, false);
                joined++;
                if (run.IsInside && run.Instance != null)
                {
                    Arrive(run, run.Instance, run.travellers.Count - 1);
                    PlayerJoined?.Invoke(run, g);
                }
            }
            return joined;
        }

        /// <summary>
        /// Gives a player held by <see cref="ArrivalHold"/> their controls back (when their run is still on and they are
        /// still in it). Returns true when the controls were given back.
        /// </summary>
        public static bool ReleaseArrival(DungeonRun run, GameObject player)
        {
            if (run == null || player == null || run.State != DungeonRunState.Inside || !run.IsParticipant(player))
                return false;
            SetControls(player, true);
            return true;
        }

        /// <summary>The group of <paramref name="run"/> goes on to another dungeon (exit portals with Next Dungeon).</summary>
        public static void Advance(DungeonRun run, DungeonRequest request = null)
        {
            if (run == null || run.State != DungeonRunState.Inside || run.Manager == null)
                return;
            run.State = DungeonRunState.Entering;
            SetControls(run, false);
            run.Manager.Cancel();
            run.Manager.Clear();
            // A request from outside gets its seed keyed in a session like Enter's; the next one of a chain comes from the
            // run's (already keyed) request.
            run.Request = request != null ? SecretRequest(request) : (run.Request ?? new DungeonRequest()).Next().Clone();
            Generate(run);
        }

        private static void Generate(DungeonRun run)
        {
            DungeonManager m = run.Manager;
            Unsubscribe(run);
            run.onReady = dungeon => OnReady(run, dungeon);
            run.onFailed = message => OnFailed(run, message);
            m.Ready += run.onReady;
            m.Failed += run.onFailed;
            m.Generate(run.Request);
        }

        private static void Unsubscribe(DungeonRun run)
        {
            if (run.Manager == null)
                return;
            if (run.onReady != null)
                run.Manager.Ready -= run.onReady;
            if (run.onFailed != null)
                run.Manager.Failed -= run.onFailed;
            run.onReady = null;
            run.onFailed = null;
        }

        private static DungeonManager ResolveManager(DungeonManager managerOrPrefab, DungeonProfile profile, Vector3 origin, out bool owns)
        {
            if (managerOrPrefab != null && managerOrPrefab.gameObject.scene.IsValid())
            {
                owns = false;
                return managerOrPrefab;
            }
            owns = true;
            if (managerOrPrefab != null)
                return Object.Instantiate(managerOrPrefab, origin, Quaternion.identity);
            var go = new GameObject("Dungeon");
            go.transform.position = origin;
            DungeonManager m = go.AddComponent<DungeonManager>();
            m.profile = profile;
            return m;
        }

        /// <summary>A manager of its own for the run, in the run's scene at its slot (a scene manager is used as a template).</summary>
        private static DungeonManager CreateManagerInScene(DungeonManager managerOrPrefab, DungeonProfile profile, DungeonScenes.Lease lease)
        {
            DungeonManager m;
            if (managerOrPrefab != null)
            {
                m = Object.Instantiate(managerOrPrefab, lease.Origin, Quaternion.identity);
                m.generateOnStart = false;
            }
            else
            {
                var go = new GameObject("Dungeon");
                go.transform.position = lease.Origin;
                m = go.AddComponent<DungeonManager>();
                m.profile = profile;
            }
            SceneManager.MoveGameObjectToScene(m.gameObject, lease.Scene);
            return m;
        }

        private static void OnReady(DungeonRun run, DungeonInstance dungeon)
        {
            Unsubscribe(run);
            if (run.State == DungeonRunState.Ended)
                return;
            // This machine's player is in the run (single player without a located player counts; a dedicated server has none).
            bool local = run.HasLocalPlayer || (PlayerLocator.Local == null && !GameNet.NetAuthority.IsNetworked);
            if (run.SceneLease != null)
            {
                foreach (DungeonRun.Traveller t in run.travellers)
                    MoveToScene(t.root, run.SceneLease.Scene);
                // A server keeps its active scene (runtime objects of other systems are created there); clients follow
                // the scene they are moved to by themselves.
                if (local && run.SceneLease.Scene.isLoaded && !GameNet.NetAuthority.IsNetworked)
                    SceneManager.SetActiveScene(run.SceneLease.Scene);
            }
            for (int i = 0; i < run.travellers.Count; i++)
                Teleport(run.travellers[i].root, ArrivalPosition(run, dungeon, i, out Quaternion facing), facing);
            if (local && dungeon.Profile != null)
            {
                DungeonAtmosphere.Apply(dungeon.Profile.Theme);
                run.atmosphereApplied = true;
            }
            run.State = DungeonRunState.Inside;
            foreach (DungeonRun.Traveller t in run.travellers)
                GiveControlsOnArrival(run, t.root);
            RunEntered?.Invoke(run);
            Entered?.Invoke(dungeon);
        }

        /// <summary>A player joining a run that is inside: into its scene, to their entrance, controls back (or held).</summary>
        private static void Arrive(DungeonRun run, DungeonInstance dungeon, int index)
        {
            DungeonRun.Traveller t = run.travellers[index];
            if (run.SceneLease != null)
                MoveToScene(t.root, run.SceneLease.Scene);
            Teleport(t.root, ArrivalPosition(run, dungeon, index, out Quaternion facing), facing);
            // This machine's player joined (a host): the dungeon's atmosphere, as for the group.
            if (!run.atmosphereApplied && dungeon.Profile != null && run.HasLocalPlayer)
            {
                DungeonAtmosphere.Apply(dungeon.Profile.Theme);
                run.atmosphereApplied = true;
            }
            GiveControlsOnArrival(run, t.root);
        }

        /// <summary>
        /// Where traveller <paramref name="index"/> arrives: their entrance (see <see cref="DungeonEntrances"/>), on a ring
        /// around it with the others of the same entrance, a little above the floor.
        /// </summary>
        private static Vector3 ArrivalPosition(DungeonRun run, DungeonInstance dungeon, int index, out Quaternion facing)
        {
            int entrance = run.travellers[index].entrance;
            Pose at = DungeonEntrances.Get(dungeon, entrance);
            int slot = 0;
            for (int i = 0; i < index; i++)
                if (run.travellers[i].entrance == entrance && run.travellers[i].root != null)
                    slot++;
            facing = at.rotation;
            return at.position + Offset(slot, at.rotation) + Vector3.up * SpawnLift;
        }

        /// <summary>Controls back on arrival - unless the network layer holds them until the player's client built the floor.</summary>
        private static void GiveControlsOnArrival(DungeonRun run, GameObject player)
        {
            bool held = false;
            if (ArrivalHold != null)
            {
                try
                {
                    held = ArrivalHold(run, player);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            if (!held)
                SetControls(player, true);
        }

        private static void OnFailed(DungeonRun run, string message)
        {
            Unsubscribe(run);
            End(run, DungeonRunEnd.Failed, message);
        }

        // ------------------------------------------------------------------ leaving

        /// <summary>The local player's group leaves its dungeon (true = completed).</summary>
        public static void Exit(bool completed) => Exit(FocusRun, completed);

        /// <summary>The group of <paramref name="run"/> leaves its dungeon (true = completed) and goes back.</summary>
        public static void Exit(DungeonRun run, bool completed)
        {
            if (run == null || run.State == DungeonRunState.Ended)
                return;
            End(run, completed ? DungeonRunEnd.Completed : DungeonRunEnd.Left, null);
        }

        /// <summary>
        /// Called by <see cref="DungeonPortal"/>. Only a participant of the portal's run can use it; the whole group travels
        /// with them (leaving, completing, or going deeper).
        /// </summary>
        public static void UsePortal(DungeonPortal portal, GameObject who)
        {
            if (!GameNet.NetAuthority.IsServer)
                return;   // travel is the server's decision
            DungeonRun run = RunAt(portal) ?? RunOf(who);
            if (run == null || !run.IsInside || !run.IsParticipant(who))
                return;
            switch (portal.action)
            {
                case DungeonPortal.PortalAction.NextDungeon:
                    Advance(run);
                    break;
                case DungeonPortal.PortalAction.CompleteDungeon:
                    Exit(run, true);
                    break;
                default:
                    Exit(run, false);
                    break;
            }
        }

        private static void End(DungeonRun run, DungeonRunEnd how, string failure)
        {
            if (run.State == DungeonRunState.Ended)
                return;
            run.State = DungeonRunState.Ended;
            run.EndReason = how;
            run.ClearCheckpoint();
            SetControls(run, false);

            if (run.atmosphereApplied)
            {
                // A dungeon scene's own lighting goes away with it: only the suns come back.
                DungeonAtmosphere.Release(run.SceneLease == null);
                run.atmosphereApplied = false;
            }

            // Players first (out of the dungeon's scene, which is unloaded below).
            foreach (DungeonRun.Traveller t in run.travellers)
            {
                if (t.root == null)
                    continue;
                bool taken = false;
                if (ExitHandler != null)
                {
                    try
                    {
                        taken = ExitHandler.TakePlayer(run, t.root, new Pose(t.returnPosition, t.returnRotation), t.returnScene, how);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                if (!taken)
                    ReturnHome(run, t);
                lastExit[t.root] = Time.time;
            }
            lastExitAny = Time.time;

            ClearDungeon(run);
            if (run.SceneLease != null)
                DungeonScenes.Release(run.SceneLease);
            runs.Remove(run);
            if (run.pausedWorld)
                DungeonWorldPause.Resume();

            run.RaiseEnded(how);
            RunEnded?.Invoke(run, how);
            if (how == DungeonRunEnd.Failed)
                Failed?.Invoke(failure);
            else
                Exited?.Invoke(how == DungeonRunEnd.Completed);
        }

        /// <summary>The default way back: in front of the world portal, in the scene the player came from.</summary>
        private static void ReturnHome(DungeonRun run, DungeonRun.Traveller t)
        {
            if (run.SceneLease != null)
            {
                Scene back = t.returnScene.IsValid() && t.returnScene.isLoaded ? t.returnScene : FirstSceneOtherThan(run.SceneLease.Scene);
                MoveToScene(t.root, back);
                bool local = PlayerLocator.Local == null || run.HasLocalPlayer;
                if (local && back.IsValid() && back.isLoaded && SceneManager.GetActiveScene() == run.SceneLease.Scene)
                    SceneManager.SetActiveScene(back);
            }
            Teleport(t.root, t.returnPosition, t.returnRotation);
            SetControls(t.root, true);
        }

        private static void ClearDungeon(DungeonRun run)
        {
            Unsubscribe(run);
            if (run.Manager == null)
                return;
            run.Manager.Cancel();
            run.Manager.Clear();
            if (run.ownsManager && run.SceneLease == null)
                Object.Destroy(run.Manager.gameObject);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Spot of the i-th traveller around a point: the first on it, the others on a ring beside it.</summary>
        private static Vector3 Offset(int index, Quaternion facing)
        {
            if (index <= 0)
                return Vector3.zero;
            float angle = 90f + (index - 1) * 60f; // right, then around
            return Quaternion.Euler(0f, angle, 0f) * (facing * Vector3.forward) * 1.4f;
        }

        private static void SetControls(DungeonRun run, bool on)
        {
            foreach (DungeonRun.Traveller t in run.travellers)
                SetControls(t.root, on);
        }

        /// <summary>Switches a player's movement (and its CharacterController) on or off.</summary>
        public static void SetControls(GameObject who, bool on)
        {
            if (who == null)
                return;
            // Networked players are frozen through their movement (switching components off would break prediction).
            if (GameNet.NetAuthority.IsNetworkedObject(who))
            {
                GameNet.IControlLock controlLock = who.GetComponentInChildren<GameNet.IControlLock>(true);
                if (controlLock != null)
                {
                    controlLock.SetControlsLocked(!on);
                    return;
                }
            }
            var movement = who.GetComponent<PlayerMovementController>();
            if (movement != null)
                movement.enabled = on;
            var cc = who.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = on;
        }

        /// <summary>Moves the whole object <paramref name="who"/> belongs to (its top-level root) into <paramref name="scene"/>.</summary>
        /// <remarks>On a server, networked players move through the network (their client loads the scene and follows).</remarks>
        public static void MoveToScene(GameObject who, Scene scene) => GameNet.NetScenes.MoveToScene(who, scene);

        private static Scene FirstSceneOtherThan(Scene leaving)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active != leaving && active.isLoaded)
                return active;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s != leaving && s.isLoaded && DungeonScenes.Find(s) == null)
                    return s;
            }
            return default;
        }

        /// <summary>Moves a character at once (its CharacterController is switched off for the move).</summary>
        public static void Teleport(GameObject who, Vector3 position, Quaternion rotation)
        {
            if (who == null)
                return;
            var cc = who.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;
            who.transform.SetPositionAndRotation(position, rotation);
            if (cc != null)
                cc.enabled = was;
            Physics.SyncTransforms();
            // A predicted (networked) movement snaps instead of smoothing across the jump.
            if (GameNet.NetAuthority.IsNetworked)
            {
                MovementStateMachine movement = who.GetComponentInChildren<MovementStateMachine>(true);
                if (movement != null)
                    movement.NotifyTeleported();
            }
        }
    }

    /// <summary>
    /// Pauses the open world while the player is underground in the classic mode (dungeons built under the world):
    /// terrain streaming (EndlessTerrain follows the viewer's x/z and would keep generating chunks above the dungeon),
    /// weather, the chunk spawners (see <see cref="WorldSpawnRegistry.Paused"/>: no new portals or mobs, world mobs
    /// switched off) and the SpawnerManager, plus anything listed on the DungeonManager. Resume restores exactly what it
    /// paused. Not used when dungeons have scenes of their own (<see cref="DungeonScenes"/>).
    /// </summary>
    public static class DungeonWorldPause
    {
        private static readonly List<Behaviour> Paused = new List<Behaviour>();
        private static readonly List<GameObject> Hidden = new List<GameObject>();

        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Paused.Clear();
            Hidden.Clear();
            IsPaused = false;
        }

        public static void Pause(IEnumerable<Behaviour> extra = null, IEnumerable<GameObject> hide = null)
        {
            if (IsPaused)
                return;
            IsPaused = true;
            // Chunk spawners (portals, mobs) stop spawning and switch their mobs off until Resume.
            WorldSpawnRegistry.Paused = true;
            Disable(FindAll<EndlessTerrain>());
            Disable(FindAll<WeatherSystem>());
            Disable(FindAll<SpawnerManager>());
            if (extra != null)
                Disable(extra);
            if (hide != null)
            {
                foreach (GameObject go in hide)
                {
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        Hidden.Add(go);
                    }
                }
            }
        }

        public static void Resume()
        {
            foreach (GameObject go in Hidden)
                if (go != null)
                    go.SetActive(true);
            Hidden.Clear();
            foreach (Behaviour b in Paused)
                if (b != null)
                    b.enabled = true;
            Paused.Clear();
            IsPaused = false;
            WorldSpawnRegistry.Paused = false;
        }

        /// <summary>Every active object of a type (Unity 6.4 deprecated the overloads taking a sort mode).</summary>
        internal static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_4_OR_NEWER
            return Object.FindObjectsByType<T>();
#else
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
#endif
        }

        private static void Disable<T>(IEnumerable<T> behaviours) where T : Behaviour
        {
            foreach (T b in behaviours)
            {
                if (b != null && b.enabled)
                {
                    b.enabled = false;
                    Paused.Add(b);
                }
            }
        }
    }

    /// <summary>Applies a theme's ambient light and fog (and turns off the sun) while inside; restores the scene's afterwards.</summary>
    public static class DungeonAtmosphere
    {
        private static bool applied;
        private static AmbientMode ambientMode;
        private static Color ambientLight;
        private static bool fog;
        private static Color fogColor;
        private static FogMode fogMode;
        private static float fogDensity;
        private static readonly List<Light> Suns = new List<Light>();
        // The scene whose lighting settings were changed (the active one when the atmosphere was applied).
        private static Scene appliedScene;

        /// <summary>Is a dungeon atmosphere applied on this machine?</summary>
        public static bool IsApplied => applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            applied = false;
            appliedScene = default;
            Suns.Clear();
        }

        public static void Apply(DungeonTheme theme)
        {
            if (theme == null || !theme.applyAtmosphere)
                return;
            if (!applied)
            {
                ambientMode = RenderSettings.ambientMode;
                ambientLight = RenderSettings.ambientLight;
                fog = RenderSettings.fog;
                fogColor = RenderSettings.fogColor;
                fogMode = RenderSettings.fogMode;
                fogDensity = RenderSettings.fogDensity;
                appliedScene = SceneManager.GetActiveScene();
                foreach (Light l in DungeonWorldPause.FindAll<Light>())
                {
                    if (l.type == LightType.Directional && l.enabled)
                    {
                        l.enabled = false;
                        Suns.Add(l);
                    }
                }
                applied = true;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambientLight;
            RenderSettings.fog = theme.fog;
            RenderSettings.fogColor = theme.fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = theme.fogDensity;
        }

        /// <summary>
        /// The theme's atmosphere adjusted for a floor modifier: flooded floors are blue-green and misty, molten ones glow
        /// red, overgrown ones green, dark ones nearly black with thick fog, frozen ones cold and pale. Does nothing when the
        /// theme doesn't apply an atmosphere.
        /// </summary>
        public static void ApplyModifier(DungeonTheme theme, FloorModifier modifier)
        {
            if (theme == null || !theme.applyAtmosphere)
                return;
            Apply(theme);
            Color ambient = theme.ambientLight, fogColor = theme.fogColor;
            float density = theme.fogDensity;
            switch (modifier)
            {
                case FloorModifier.Flooded:
                    ambient = Color.Lerp(ambient, new Color(0.08f, 0.16f, 0.18f), 0.6f);
                    fogColor = Color.Lerp(fogColor, new Color(0.04f, 0.1f, 0.12f), 0.7f);
                    density *= 1.35f;
                    break;
                case FloorModifier.Molten:
                    ambient = Color.Lerp(ambient, new Color(0.28f, 0.1f, 0.05f), 0.65f);
                    fogColor = Color.Lerp(fogColor, new Color(0.18f, 0.05f, 0.02f), 0.75f);
                    density *= 0.85f;
                    break;
                case FloorModifier.Overgrown:
                    ambient = Color.Lerp(ambient, new Color(0.08f, 0.17f, 0.08f), 0.55f);
                    fogColor = Color.Lerp(fogColor, new Color(0.04f, 0.09f, 0.04f), 0.65f);
                    density *= 1.15f;
                    break;
                case FloorModifier.Darkness:
                    ambient *= 0.3f;
                    fogColor *= 0.4f;
                    density *= 1.9f;
                    break;
                case FloorModifier.Frozen:
                    ambient = Color.Lerp(ambient, new Color(0.17f, 0.21f, 0.28f), 0.65f);
                    fogColor = Color.Lerp(fogColor, new Color(0.16f, 0.2f, 0.26f), 0.7f);
                    density *= 1.2f;
                    break;
            }
            RenderSettings.ambientLight = ambient;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = density;
        }

        public static void Restore() => Release(true);

        /// <summary>
        /// Turns the suns back on and, with <paramref name="restoreSettings"/>, puts the saved ambient light and fog back.
        /// A dungeon with its own scene skips the settings: its scene (and its lighting) is unloaded anyway, and the
        /// scene the player returns to kept its own.
        /// </summary>
        public static void Release(bool restoreSettings)
        {
            if (!applied)
                return;
            // The settings belong to the scene that was active: a dungeon scene takes them along when it is unloaded,
            // any other scene (a network session keeps the realm scene active) gets its own back.
            if (restoreSettings || (appliedScene.IsValid() && appliedScene.isLoaded && DungeonScenes.Find(appliedScene) == null))
            {
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = fogDensity;
            }
            foreach (Light l in Suns)
                if (l != null)
                    l.enabled = true;
            Suns.Clear();
            applied = false;
            appliedScene = default;
        }
    }
}
