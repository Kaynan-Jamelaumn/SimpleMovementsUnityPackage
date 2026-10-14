using System;
using System.Collections;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HubWorld
{
    /// <summary>State of the World scene in this process.</summary>
    public enum WorldLoadState
    {
        Unloaded,
        Loading,
        Ready,
        Unloading,
    }

    /// <summary>
    /// The authority of the hub world - in a networked game, the part that runs on the server. It owns:
    /// <list type="bullet">
    /// <item>the dimensional cycle (<see cref="Clock"/>): warnings, the jump, a new world seed and modifier each cycle;</item>
    /// <item>the realms: which scene each player is in (<see cref="PlayerRealmState"/>), the city gate and the return gate,
    /// loading the World scene with the cycle's seed and unloading it when nobody is out there;</item>
    /// <item>the jump's rules (<see cref="CycleRules"/>): players out in the world are lost between dimensions - back to
    /// the city, part of their loot gone, Dimensional Disorientation; players in a dungeon finish their run first;</item>
    /// <item>the zones (<see cref="Zones"/>): safe and dangerous rings, PvP, dungeon difficulty and mob strength by
    /// distance from the city;</item>
    /// <item>the combat rules of the city and the zones (<see cref="HubCombatPolicy"/>), and duels (<see cref="DuelSystem"/>).</item>
    /// </list>
    /// Put one in the City scene (Tools ▸ SimpleMovements ▸ Hub World ▸ Build City Scene does it). It survives scene
    /// loads, adds the duel system, the sky effects and the HUD next to itself, and needs a <see cref="RealmScene"/> in
    /// the City and World scenes.
    /// </summary>
    /// <remarks>
    /// <para><b>Network sessions.</b> Everything above is decided only where <c>GameNet.NetAuthority.IsServer</c> (a host or
    /// a dedicated server). On a pure client the director is a mirror (<see cref="IsMirror"/>): it raises no cycle event of
    /// its own, loads no scene and moves nobody; its public state (the clock, cycle index, world seed, modifiers, jumping,
    /// gate) is written by <c>NetHubWorld</c> through the <c>ApplyServer...</c> / <c>ShowServer...</c> methods, and each
    /// player's <see cref="PlayerRealmState"/> by <c>NetPlayerRealm</c> (<see cref="ApplyReplicatedRealm"/>), so the HUD,
    /// the sky, the zone label and the combat rules work there unchanged. The World scene a client loads (because the
    /// server moved its player in) is adopted when it finishes loading, after the server's seed and city areas were
    /// installed (<see cref="ApplyServerWorld"/>).</para>
    /// <para>On the server, travel goes through <c>GameNet.NetScenes</c>: the World is loaded and unloaded through
    /// Fish-Net, players move between scenes there at once and their client follows, and a remote player stays frozen after
    /// an arrival until their client confirms it has the ground under them (<see cref="ConfirmArrival"/>). The server's
    /// active scene never changes, and nothing is hidden on a dedicated server.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public class HubWorldDirector : MonoBehaviour, DungeonSession.IExitHandler
    {
        [Tooltip("OPTIONAL. The settings asset. Empty = Resources/HubWorldSettings, else the defaults.")]
        [SerializeField] private HubWorldSettings settings;
        [Tooltip("Create the HUD (zone, time until the jump, warnings, duel prompts) automatically.")]
        [SerializeField] private bool createHud = true;
        [Tooltip("Players found in no realm scene (DontDestroyOnLoad, spawned by a character creation scene...) are placed at the city's spawn point.")]
        [SerializeField] private bool adoptUnplacedPlayers = true;

        public static HubWorldDirector Instance { get; private set; }

        /// <summary>
        /// A network client: the director mirrors the server's hub world instead of deciding anything. Read when acting
        /// (a process learns it is a client when it connects). False offline, on a host and on a dedicated server.
        /// </summary>
        public static bool IsMirror => GameNet.NetAuthority.IsClientOnly;

        /// <summary>
        /// Network clients: sends a gate request to the server (the asking player, the gate). Installed by the networking
        /// layer; the server checks the player stands at such a gate and decides as usual.
        /// </summary>
        public static Action<CombatEntity, RealmGate.GateKind> ClientTravelRequest;

        /// <summary>
        /// Optional: gives a world terrain every transform it must stream around (all players out in the world on a
        /// server; this machine's player elsewhere), returning true when handled. Null or false = the terrain's single
        /// <see cref="EndlessTerrain.viewer"/> is used (this machine's player, else the first player out there). Called every
        /// frame with a reused list.
        /// </summary>
        public static Func<EndlessTerrain, IReadOnlyList<Transform>, bool> TerrainViewers;

        /// <summary>
        /// This machine's player: in single player <see cref="PlayerLocator.Local"/>; in a network session the player this
        /// machine controls - none on a dedicated server, and none on a client until its own player has spawned (other
        /// players are never taken for it).
        /// </summary>
        public static CombatEntity LocalPlayer
        {
            get
            {
                if (!GameNet.NetAuthority.IsNetworked)
                    return PlayerLocator.Local;
                if (!GameNet.NetAuthority.HasLocalPlayer)
                    return null;
                CombatEntity local = PlayerLocator.Local;
                if (IsControlledHere(local))
                    return local;
                IReadOnlyList<CombatEntity> all = CombatEntity.Players;
                for (int i = 0; i < all.Count; i++)
                    if (IsControlledHere(all[i]))
                        return all[i];
                return null;
            }
        }

        private static bool IsControlledHere(CombatEntity e) =>
            e != null && GameNet.NetAuthority.IsNetworkedObject(e.gameObject) && GameNet.NetAuthority.IsLocallyControlled(e.gameObject);

        /// <summary>Seconds a server waits for a client to confirm an arrival before giving the player their controls anyway.</summary>
        public const float ClientArrivalTimeout = 30f;

        /// <summary>Seconds a server waits for a network scene load or unload before treating it as failed.</summary>
        private const float NetworkSceneTimeout = 90f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClientTravelRequest = null;
            TerrainViewers = null;
        }

        public HubWorldSettings Settings => settings;
        public WorldCycleClock Clock { get; private set; }
        public ZoneMap Zones { get; private set; }
        public int ShardSeed { get; private set; }
        public int CycleIndex => Clock != null ? Clock.CycleIndex : 0;
        /// <summary>The seed of the current cycle's world (sent to clients, which rebuild the terrain from it).</summary>
        public int WorldSeed => IsMirror && mirrorSynced ? mirroredWorldSeed : CycleSeeds.WorldSeed(ShardSeed, CycleIndex);
        /// <summary>The seed of the next cycle's world (known in advance: the server may prepare it). 0 on a network client (never sent).</summary>
        public int NextWorldSeed => IsMirror ? 0 : CycleSeeds.WorldSeed(ShardSeed, CycleIndex + 1);
        public DimensionalModifier CurrentModifier { get; private set; }
        /// <summary>The modifier of the next cycle, announced in the city before the jump.</summary>
        public DimensionalModifier NextModifier { get; private set; }
        public bool IsJumping { get; private set; }
        public WorldLoadState WorldState { get; private set; } = WorldLoadState.Unloaded;
        public Scene WorldScene { get; private set; }
        public IReadOnlyList<PlayerRealmState> Players => players;

        /// <summary>The time the cycle runs on (game time, or real time with Use Unscaled Time).</summary>
        public double Now => settings != null && settings.useUnscaledTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
        public double SecondsUntilJump => Clock != null ? Clock.Remaining(Now) : 0d;
        /// <summary>Can players go out through the city gate now? (A network client shows the server's answer.)</summary>
        public bool GateOpen => IsMirror && mirrorSynced ? mirroredGateOpen : CycleRules.GateOpen(IsJumping, SecondsUntilJump, settings.closeGateSecondsBeforeJump);

        /// <summary>A warning before the jump (seconds left).</summary>
        public event Action<float> CycleWarning;
        /// <summary>The city jumped (the new cycle's index).</summary>
        public event Action<int> CycleJumped;
        /// <summary>The next cycle's modifier was rolled (null = none).</summary>
        public event Action<DimensionalModifier> ModifierAnnounced;
        public event Action WorldLoaded;
        public event Action WorldUnloaded;
        /// <summary>A player changed realm (player, previous, current).</summary>
        public event Action<CombatEntity, RealmKind, RealmKind> PlayerRealmChanged;
        /// <summary>A player changed zone (player, previous, current).</summary>
        public event Action<CombatEntity, ZoneInfo, ZoneInfo> PlayerZoneChanged;
        /// <summary>A player was lost between dimensions (player, what they lost).</summary>
        public event Action<CombatEntity, CyclePenalties.LootLossResult> PlayerLostBetweenDimensions;

        private readonly List<PlayerRealmState> players = new List<PlayerRealmState>();
        private readonly List<CycleEvent> cycleEvents = new List<CycleEvent>();
        private readonly Dictionary<PlayerRealmState, Coroutine> travels = new Dictionary<PlayerRealmState, Coroutine>();
        private readonly List<Action<bool>> worldReadyQueue = new List<Action<bool>>();
        private readonly List<EndlessTerrain> worldTerrains = new List<EndlessTerrain>();
        private readonly List<WeatherSystem> worldWeathers = new List<WeatherSystem>();
        private HubCombatPolicy policy;
        private System.Random penaltyRandom;
        private AudioSource audioSource;
        private float nextZoneCheck;
        private float nextPlayerScan;
        private float unloadWorldAt = -1f;
        private bool loadWorldAfterUnload;
        private bool cityLoading;
        private int arrivalIndex;
        private string loadedWorldName;

        // Network sessions.
        private readonly Dictionary<PlayerRealmState, float> arrivalHolds = new Dictionary<PlayerRealmState, float>();
        private readonly List<PlayerRealmState> expiredHolds = new List<PlayerRealmState>();
        private readonly List<Transform> viewers = new List<Transform>();
        private int arrivalSerial;
        private bool mirrorSynced;
        private int mirroredWorldSeed;
        private bool mirroredGateOpen = true;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"HubWorldDirector: another director ('{Instance.name}') is already running - '{name}' is removed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);

            if (settings == null)
                settings = HubWorldSettings.Instance;
            if (settings.logEvents)
                HubWorldMessages.LogToConsole = true;
            ShardSeed = settings.shardSeed != 0 ? settings.shardSeed : CycleSeeds.RandomShardSeed();
            penaltyRandom = new System.Random(ShardSeed ^ 0x5EED);
            Clock = new WorldCycleClock(settings.CycleSeconds, settings.WarningSeconds(), Now, 0);
            Zones = settings.CreateZoneMap();
            CurrentModifier = RollModifier(0);
            NextModifier = RollModifier(1);

            InstallWorldRules();
            DungeonScenes.Settings = settings.dungeonScenes ?? new DungeonSceneSettings();
            DungeonScenes.Enabled = settings.dungeonsInOwnScenes;
            DungeonSession.ExitHandler = this;
            DungeonSession.RunEntered += OnRunEntered;
            DungeonSession.PlayerJoined += OnPlayerJoinedRun;
            policy = new HubCombatPolicy(settings);
            CombatRules.Register(policy);
            CombatRules.Separation = AreInSeparateRealms;
            MobRealms.SceneSeparation = AreSeparateRealmScenes;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (GetComponent<DuelSystem>() == null)
                gameObject.AddComponent<DuelSystem>();
            if (GetComponent<CycleAtmosphere>() == null)
                gameObject.AddComponent<CycleAtmosphere>();
            if (GetComponent<CityAbilityRelay>() == null)
                gameObject.AddComponent<CityAbilityRelay>();
            if (createHud && !GameNet.NetAuthority.IsHeadless && GetComponent<HubWorldHud>() == null)
                gameObject.AddComponent<HubWorldHud>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            GameNet.NetAuthority.ModeChanged += OnNetModeChanged;
        }

        private void Start()
        {
            // A World scene already open (testing it directly) is adopted for the first cycle.
            RealmScene world = RealmScene.Find(RealmKind.World);
            if (world != null)
            {
                WorldScene = world.Scene;
                loadedWorldName = WorldScene.name;
                WorldState = WorldLoadState.Ready;
                ConfigureWorld();
            }
            // In a network session the City is loaded through Fish-Net (the server at start, each client when it joins).
            if (!GameNet.NetAuthority.IsNetworked && RealmScene.Find(RealmKind.City) == null && settings.loadCityOnStart && !string.IsNullOrEmpty(settings.citySceneName))
                StartCoroutine(LoadCity());
            ScanPlayers();
            if (IsMirror)
                return; // the server's cycle is shown once its state arrives (ApplyServerWorld)
            Log($"Cycle {CycleIndex} started (world seed {WorldSeed}{(CurrentModifier != null ? ", " + CurrentModifier.displayName : "")}); jump in {HubWorldMessages.FormatTime(SecondsUntilJump)}.");
            if (CurrentModifier != null)
                HubWorldMessages.Broadcast($"This dimension: {CurrentModifier.displayName}", HubMessageKind.Info);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            GameNet.NetAuthority.ModeChanged -= OnNetModeChanged;
            CombatRules.Unregister(policy);
            if (CombatRules.Separation == (Func<CombatEntity, CombatEntity, bool>)AreInSeparateRealms)
                CombatRules.Separation = null;
            if (MobRealms.SceneSeparation == (Func<Scene, Scene, bool>)AreSeparateRealmScenes)
                MobRealms.SceneSeparation = null;
            if (ReferenceEquals(DungeonSession.ExitHandler, this))
                DungeonSession.ExitHandler = null;
            DungeonSession.RunEntered -= OnRunEntered;
            DungeonSession.PlayerJoined -= OnPlayerJoinedRun;
            DungeonScenes.Enabled = false;
            WorldSpawnHooks.MobSpawned = null;
            WorldSpawnHooks.MobWeight = null;
            WorldSpawnHooks.PortalDifficulty = null;
            WorldSpawnHooks.DungeonType = null;
            WorldSpawnHooks.ZoneAt = null;
            WorldAreas.Clear();
            TerrainGenerator.SeedOverride = null;
        }

        /// <summary>
        /// Entities in two different realm scenes (the City and the World stand at overlapping coordinates; each dungeon
        /// run has a scene of its own) cannot harm or target each other. Players parked between scenes are not separated.
        /// </summary>
        private static bool AreInSeparateRealms(CombatEntity a, CombatEntity b)
        {
            Scene sa = a.gameObject.scene, sb = b.gameObject.scene;
            if (sa == sb)
                return false;
            return IsRealmScene(sa) && IsRealmScene(sb);
        }

        /// <summary>Scene-level form of <see cref="AreInSeparateRealms"/> for things without an entity (chunk spawners).</summary>
        private static bool AreSeparateRealmScenes(Scene a, Scene b) => a != b && IsRealmScene(a) && IsRealmScene(b);

        private static bool IsRealmScene(Scene s) => s.IsValid() && (RealmScene.ForScene(s) != null || DungeonScenes.Find(s) != null);

        private void Update()
        {
            if (Time.unscaledTime >= nextPlayerScan)
            {
                nextPlayerScan = Time.unscaledTime + 0.5f;
                ScanPlayers();
            }
            if (IsMirror)
            {
                // A network client: the server decides everything; only what this machine shows is updated here.
                UpdatePresentation();
                return;
            }
            AdvanceCycle();
            ExpireArrivalHolds();
            if (Time.time >= nextZoneCheck)
            {
                nextZoneCheck = Time.time + settings.zones.checkInterval;
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null)
                        UpdateZone(players[i]);
            }
            if (unloadWorldAt >= 0f && Time.time >= unloadWorldAt)
            {
                unloadWorldAt = -1f;
                if (CanUnloadWorld())
                    UnloadWorld(null);
            }
            UpdatePresentation();
        }

        // ------------------------------------------------------------------ players

        /// <summary>Is this the player of this machine? (Single player: the player. Network: see <see cref="LocalPlayer"/>.)</summary>
        public static bool IsLocal(PlayerRealmState s)
        {
            if (s == null)
                return false;
            if (!GameNet.NetAuthority.IsNetworked)
            {
                CombatEntity single = PlayerLocator.Local;
                return single == null || single == s.Entity;
            }
            CombatEntity local = LocalPlayer;
            return local != null && local == s.Entity;
        }

        /// <summary>This machine's player's state, or null.</summary>
        public PlayerRealmState LocalState()
        {
            CombatEntity local = LocalPlayer;
            return local != null ? PlayerRealmState.Of(local) : null;
        }

        /// <summary>The state of <paramref name="player"/>, created and listed when missing (without placing them anywhere).</summary>
        public PlayerRealmState EnsureState(CombatEntity player)
        {
            if (player == null)
                return null;
            PlayerRealmState s = PlayerRealmState.Of(player);
            if (s == null)
            {
                s = player.gameObject.AddComponent<PlayerRealmState>();
                s.Entity = player;
            }
            else if (s.Entity == null)
                s.Entity = player;
            if (!players.Contains(s))
                players.Add(s);
            return s;
        }

        /// <summary>The realm of a player (None when unknown).</summary>
        public RealmKind RealmOf(CombatEntity player)
        {
            PlayerRealmState s = PlayerRealmState.Of(player);
            return s != null ? s.Realm : RealmKind.None;
        }

        private void ScanPlayers()
        {
            IReadOnlyList<CombatEntity> all = CombatEntity.Players;
            for (int i = 0; i < all.Count; i++)
            {
                CombatEntity p = all[i];
                if (p == null)
                    continue;
                if (IsMirror)
                {
                    EnsureState(p); // the realm comes from the server (ApplyReplicatedRealm)
                    continue;
                }
                PlayerRealmState s = PlayerRealmState.Of(p);
                if (s == null)
                {
                    s = p.gameObject.AddComponent<PlayerRealmState>();
                    s.Entity = p;
                    players.Add(s);
                    PlaceNewPlayer(s);
                }
                else if (!players.Contains(s))
                {
                    if (s.Entity == null)
                        s.Entity = p;
                    players.Add(s);
                }
            }
            players.RemoveAll(s => s == null || s.Entity == null);
            // A player who left the session mid-travel (their object despawned): their travel ended by itself.
            if (travels.Count > 0 || travellingFromDungeon.Count > 0)
            {
                expiredHolds.Clear();
                foreach (PlayerRealmState s in travels.Keys)
                    if (s == null)
                        expiredHolds.Add(s);
                foreach (PlayerRealmState s in expiredHolds)
                    travels.Remove(s);
                expiredHolds.Clear();
                travellingFromDungeon.RemoveWhere(s => s == null);
            }
        }

        /// <summary>A player the hub world has not seen yet: in the realm of their scene, or adopted by the city.</summary>
        private void PlaceNewPlayer(PlayerRealmState s)
        {
            GameObject mover = RealmTravel.Mover(s.Entity);
            RealmScene realm = mover != null ? RealmScene.ForScene(mover.scene) : null;
            if (realm != null)
            {
                if (realm.realm == RealmKind.World)
                    s.WorldCycle = CycleIndex;
                SetRealm(s, realm.realm);
                Log($"{s.Entity.name} joined in the {realm.realm}.");
                return;
            }
            if (DungeonSession.RunOf(mover) != null)
            {
                SetRealm(s, RealmKind.Dungeon);
                return;
            }
            if (adoptUnplacedPlayers && (RealmScene.Find(RealmKind.City) != null || cityLoading))
            {
                Log($"{s.Entity.name} is in no realm scene - placing them in the city.");
                TravelToCity(s, false, true);
            }
        }

        private void SetRealm(PlayerRealmState s, RealmKind realm, RealmKind destination = RealmKind.None)
        {
            RealmKind previous = s.Realm;
            s.SetRealm(realm, destination);
            UpdateZone(s);
            if (previous != s.Realm)
            {
                Log($"{s.Entity.name}: {previous} -> {s.Realm}{(s.Realm == RealmKind.Transit ? " (to " + destination + ")" : "")}");
                PlayerRealmChanged?.Invoke(s.Entity, previous, s.Realm);
            }
            if (IsLocal(s))
                UpdatePresentation();
        }

        private void UpdateZone(PlayerRealmState s)
        {
            if (s == null || s.Entity == null)
                return;
            ZoneInfo zone;
            switch (s.Realm)
            {
                case RealmKind.World:
                    zone = Zones.At(s.Entity.Position);
                    break;
                case RealmKind.City:
                    zone = Zones.AtDistance(0f);
                    zone.name = "City";
                    break;
                case RealmKind.Dungeon:
                    DungeonRun run = DungeonSession.RunOf(RealmTravel.Mover(s.Entity));
                    zone = new ZoneInfo
                    {
                        index = -2,
                        name = "Dungeon",
                        pvp = settings.zones.pvpInDungeons,
                        dungeonDifficulty = run != null && run.Request != null ? run.Request.difficulty : 1f,
                        mobStrength = 1f,
                        rewardMultiplier = 1f,
                        color = new Color(0.8f, 0.75f, 0.65f),
                    };
                    break;
                case RealmKind.Transit:
                    zone = new ZoneInfo { index = -3, name = "Between Dimensions", color = new Color(0.7f, 0.6f, 1f) };
                    break;
                default:
                    zone = new ZoneInfo { index = -4, name = "", color = Color.white };
                    break;
            }
            ZoneInfo previous = s.Zone;
            s.SetZone(zone);
            if (previous.index == zone.index && previous.pvp == zone.pvp && previous.name == zone.name)
                return;
            PlayerZoneChanged?.Invoke(s.Entity, previous, zone);
            if (s.Realm == RealmKind.World && !string.IsNullOrEmpty(zone.name))
                HubWorldMessages.Send(s.Entity, zone.pvp ? $"{zone.name} - PvP Active" : zone.name, HubMessageKind.Banner);
        }

        // ------------------------------------------------------------------ the cycle

        private void AdvanceCycle()
        {
            cycleEvents.Clear();
            Clock.Advance(Now, cycleEvents);
            for (int i = 0; i < cycleEvents.Count; i++)
            {
                CycleEvent e = cycleEvents[i];
                if (e.Kind == CycleEventKind.Warning)
                    OnWarning(e.SecondsLeft);
                else
                    PerformJump();
            }
        }

        /// <summary>Is the warning at <paramref name="secondsLeft"/> the last one before the jump?</summary>
        private bool IsFinalWarning(float secondsLeft)
        {
            foreach (float w in Clock.Warnings)
                if (w < secondsLeft && w < Clock.Duration)
                    return false;
            return true;
        }

        private void OnWarning(float secondsLeft)
        {
            bool final = IsFinalWarning(secondsLeft);
            string text = string.Format(settings.warningText, HubWorldMessages.FormatSpan(secondsLeft));
            HubWorldMessages.Broadcast(text, HubMessageKind.Banner);
            if (NextModifier != null && secondsLeft >= Clock.Warnings[0] - 0.01f)
                HubWorldMessages.Broadcast($"The next dimension: {NextModifier.displayName}. {NextModifier.announcement}", HubMessageKind.Info);
            PlaySound(final && settings.finalWarningSound != null ? settings.finalWarningSound : settings.warningSound);
            Log($"Warning: {secondsLeft:0}s before the jump.");
            CycleWarning?.Invoke(secondsLeft);
        }

        /// <summary>
        /// The city jumps: players out in the world are lost between dimensions, the world is replaced (new seed and
        /// modifier), players in dungeons carry on and learn their fate when they leave. Runs at the end of every cycle;
        /// <see cref="JumpNow"/> forces it.
        /// </summary>
        private void PerformJump()
        {
            IsJumping = true;
            int cycle = CycleIndex;
            Log($"Jump! Cycle {cycle} begins (world seed {WorldSeed}).");

            var snapshot = new List<PlayerRealmState>(players);
            foreach (PlayerRealmState s in snapshot)
            {
                if (s == null || s.Entity == null)
                    continue;
                GameObject mover = RealmTravel.Mover(s.Entity);
                DungeonRun run = DungeonSession.RunOf(mover);
                switch (s.Realm)
                {
                    case RealmKind.World:
                        if (run != null)
                        {
                            // Still at the portal while the dungeon generates: the exit handler sends them to the city.
                            DungeonSession.Exit(run, false);
                            break;
                        }
                        LoseBetweenDimensions(s);
                        break;
                    case RealmKind.Dungeon:
                        // In the classic mode the dungeon lies under the world being replaced: everyone comes out now.
                        if (!DungeonScenes.Enabled && run != null)
                            DungeonSession.Exit(run, false);
                        break;
                    case RealmKind.Transit:
                        // On the way out of the city: turned back at the gate (players coming back from a dungeon are
                        // redirected by their own travel, which notices the jump).
                        if (s.Destination == RealmKind.World && !travellingFromDungeon.Contains(s))
                        {
                            HubWorldMessages.Send(s.Entity, "The gate closed as the city jumped.", HubMessageKind.Warning);
                            TravelToCity(s, false, false);
                        }
                        break;
                }
            }

            // A new world: forget the old one's closed portals and killed mobs, roll the next modifier.
            WorldSpawnRegistry.SetClosedPortalSites(null);
            WorldSpawnRegistry.ClearMobMemory();
            CurrentModifier = NextModifier;
            NextModifier = RollModifier(cycle + 1);
            InstallWorldRules();

            PlaySound(settings.jumpSound);
            HubWorldMessages.Broadcast(settings.jumpText, HubMessageKind.Banner);
            if (CurrentModifier != null)
                HubWorldMessages.Broadcast($"This dimension: {CurrentModifier.displayName}", HubMessageKind.Info);
            CycleJumped?.Invoke(cycle);
            ModifierAnnounced?.Invoke(NextModifier);

            UnloadWorld(() =>
            {
                IsJumping = false;
                if (!settings.unloadWorldWhenEmpty || WorldOccupied())
                    EnsureWorld(null);
            });
        }

        private DimensionalModifier RollModifier(int cycle)
        {
            if (settings.modifiers == null || settings.modifiers.Count == 0)
                return null;
            var weights = new List<float>(settings.modifiers.Count);
            foreach (DimensionalModifier m in settings.modifiers)
                weights.Add(m != null ? m.weight : 0f);
            int pick = CycleSeeds.PickWeighted(ShardSeed, (uint)cycle * 2654435761u ^ 0x4D4F44u, weights, settings.noModifierWeight);
            return pick >= 0 ? settings.modifiers[pick] : null;
        }

        private void LoseBetweenDimensions(PlayerRealmState s)
        {
            Log($"{s.Entity.name} was lost between dimensions.");
            TravelToCity(s, true, true);
        }

        private void ApplyPenalties(PlayerRealmState s)
        {
            CyclePenalties.LootLossResult loss = CyclePenalties.ApplyLootLoss(s.Entity, settings.lootLoss, penaltyRandom);
            bool debuff = CyclePenalties.ApplyDisorientation(s.Entity, settings.disorientation);
            string text = string.Format(settings.lostText, loss.Describe());
            if (debuff)
                text += $" ({settings.disorientation.label})";
            HubWorldMessages.Send(s.Entity, text, HubMessageKind.Banner);
            if (loss.lines != null && loss.lines.Count > 0)
                HubWorldMessages.Send(s.Entity, "Lost: " + string.Join(", ", loss.lines), HubMessageKind.Warning);
            PlayerLostBetweenDimensions?.Invoke(s.Entity, loss);
        }

        // ------------------------------------------------------------------ requests (the gates)

        /// <summary>A player asks to go out through the city gate. False with a reason when they may not.</summary>
        public bool RequestLeaveCity(CombatEntity player, out string reason)
        {
            PlayerRealmState s = PlayerRealmState.Of(player);
            reason = null;
            if (s == null || !s.InCity)
            {
                reason = "You are not in the city.";
                return false;
            }
            if (!GateOpen)
            {
                reason = IsJumping ? "The city is jumping dimensions..." : "The gate is sealed until the city has jumped.";
                return false;
            }
            if (string.IsNullOrEmpty(WorldSceneName()))
            {
                reason = "No World scene is set (Hub World Settings).";
                return false;
            }
            if (IsMirror)
                return SendTravelRequest(player, RealmGate.GateKind.CityGate, out reason);
            TravelToWorld(s, null, false);
            return true;
        }

        /// <summary>A player asks to go back into the city through the return gate.</summary>
        public bool RequestReturnToCity(CombatEntity player, out string reason)
        {
            PlayerRealmState s = PlayerRealmState.Of(player);
            reason = null;
            if (s == null || !s.InWorld)
            {
                reason = "You are not out in the world.";
                return false;
            }
            if (IsMirror)
                return SendTravelRequest(player, RealmGate.GateKind.ReturnGate, out reason);
            TravelToCity(s, false, false);
            return true;
        }

        private static bool SendTravelRequest(CombatEntity player, RealmGate.GateKind gate, out string reason)
        {
            reason = null;
            if (ClientTravelRequest == null)
            {
                reason = "Not connected to the server.";
                return false;
            }
            ClientTravelRequest(player, gate);
            return true; // sent: the server decides and moves the player
        }

        // ------------------------------------------------------------------ travel

        private readonly HashSet<PlayerRealmState> travellingFromDungeon = new HashSet<PlayerRealmState>();

        private void StartTravel(PlayerRealmState s, IEnumerator routine)
        {
            if (IsMirror)
                return; // a network client never moves anyone: the server does
            CancelTravel(s);
            ClearArrivalHold(s);
            travels[s] = StartCoroutine(routine);
        }

        private void CancelTravel(PlayerRealmState s)
        {
            if (s != null && travels.TryGetValue(s, out Coroutine c))
            {
                if (c != null)
                    StopCoroutine(c);
                travels.Remove(s);
            }
            travellingFromDungeon.Remove(s);
        }

        /// <summary>Sends a player into the city (at its spawn point with <paramref name="toSpawn"/>, else just inside the gate).</summary>
        public void TravelToCity(PlayerRealmState s, bool penalties, bool toSpawn)
        {
            if (s == null || s.Entity == null)
                return;
            StartTravel(s, ToCity(s, penalties, toSpawn));
        }

        private IEnumerator ToCity(PlayerRealmState s, bool penalties, bool toSpawn)
        {
            SetRealm(s, RealmKind.Transit, RealmKind.City);
            RealmTravel.Hold(s.Entity);
            RealmScene city;
            while ((city = RealmScene.Find(RealmKind.City)) == null)
            {
                if (!cityLoading)
                {
                    Debug.LogError($"HubWorldDirector: no City scene is loaded (a RealmScene with Realm = City) - '{s.Entity.name}' cannot be placed.", this);
                    RealmTravel.SetControls(s.Entity, true);
                    travels.Remove(s);
                    yield break;
                }
                yield return null;
                if (s == null || s.Entity == null)
                    yield break;
            }
            int index = arrivalIndex++ % 12;
            Pose pose = toSpawn ? city.SpawnPose(index) : city.ArrivalPose(index);
            bool waitForClient = NeedsClientArrival(s);
            RealmTravel.Arrive(s.Entity, city.Scene, pose, ActivatesScenes(s), !waitForClient);
            travels.Remove(s);
            SetRealm(s, RealmKind.City);
            if (waitForClient)
                HoldForClient(s);
            if (penalties)
                ApplyPenalties(s);
            ScheduleWorldUnload();
        }

        /// <summary>
        /// Sends a player out into the world: in front of the return gate (<paramref name="arrival"/> null), or at a pose (back
        /// from a dungeon). The world is loaded first if needed; the player waits frozen until the ground under them exists.
        /// </summary>
        public void TravelToWorld(PlayerRealmState s, Pose? arrival, bool fromDungeon)
        {
            if (s == null || s.Entity == null)
                return;
            StartTravel(s, ToWorld(s, arrival, fromDungeon));
        }

        private IEnumerator ToWorld(PlayerRealmState s, Pose? arrival, bool fromDungeon)
        {
            if (fromDungeon)
                travellingFromDungeon.Add(s);
            int cycle = CycleIndex;
            SetRealm(s, RealmKind.Transit, RealmKind.World);
            RealmTravel.Hold(s.Entity);
            if (arrival.HasValue)
                RealmTravel.Place(s.Entity, new Pose(arrival.Value.position + Vector3.up * 2f, arrival.Value.rotation));

            bool done = false, ok = false;
            EnsureWorld(success => { done = true; ok = success; });
            while (!done)
            {
                yield return null;
                if (s == null || s.Entity == null)
                    yield break;
            }
            if (!ok || CycleIndex != cycle)
            {
                // The world they were heading for failed to load, or is gone (the city jumped meanwhile).
                travels.Remove(s);
                travellingFromDungeon.Remove(s);
                bool penalised = fromDungeon && CycleRules.PenalisedOnDungeonExit(s.WorldCycle, CycleIndex);
                TravelToCity(s, penalised, penalised);
                yield break;
            }

            RealmScene world = RealmScene.Find(RealmKind.World);
            Pose pose = arrival ?? (world != null ? world.ArrivalPose(arrivalIndex++ % 12) : new Pose(new Vector3(settings.zones.cityCenter.x, settings.zones.cityGroundHeight, settings.zones.cityCenter.y), Quaternion.identity));
            if (ActivatesScenes(s) && WorldScene.IsValid() && WorldScene.isLoaded)
                SceneManager.SetActiveScene(WorldScene); // runtime objects of the world go in its scene
            RealmTravel.Place(s.Entity, new Pose(pose.position + Vector3.up * 2f, pose.rotation));
            UpdatePresentation(); // the terrain now streams around the waiting player

            float waited = 0f, height;
            while (!FindWorldGround(pose.position, out height))
            {
                if (waited >= settings.arrivalGroundTimeout)
                {
                    height = RealmTravel.GroundGuess(pose.position);
                    Debug.LogWarning($"HubWorldDirector: no ground under {pose.position} after {waited:0}s - placing '{s.Entity.name}' anyway.", this);
                    break;
                }
                yield return null;
                waited += Time.unscaledDeltaTime;
                if (s == null || s.Entity == null)
                    yield break;
                if (CycleIndex != cycle || WorldState != WorldLoadState.Ready)
                {
                    travels.Remove(s);
                    travellingFromDungeon.Remove(s);
                    bool penalised = fromDungeon && CycleRules.PenalisedOnDungeonExit(s.WorldCycle, CycleIndex);
                    TravelToCity(s, penalised, penalised);
                    yield break;
                }
            }
            pose.position = new Vector3(pose.position.x, height + 0.15f, pose.position.z);
            bool waitForClient = NeedsClientArrival(s);
            RealmTravel.Arrive(s.Entity, WorldScene, pose, ActivatesScenes(s), !waitForClient);
            travels.Remove(s);
            travellingFromDungeon.Remove(s);
            if (!fromDungeon || s.WorldCycle < 0)
                s.WorldCycle = CycleIndex;
            SetRealm(s, RealmKind.World);
            if (waitForClient)
                HoldForClient(s);
        }

        /// <summary>
        /// The ground under an arrival in the World. In a network session the City stays loaded and solid where the rules
        /// run (the server simulates it for the players there) and overlaps the World's copy of it, so only the World's own
        /// ground counts; single player hides the City first and takes the first ground found, as before.
        /// </summary>
        private bool FindWorldGround(Vector3 at, out float height) =>
            GameNet.NetAuthority.IsNetworked ? RealmTravel.TryFindGroundIn(at, WorldScene, out height) : RealmTravel.TryFindGround(at, out height);

        // ------------------------------------------------------------------ network arrivals (server)

        /// <summary>Does arriving switch the active scene? Single player only: a server's active scene stays the City.</summary>
        private static bool ActivatesScenes(PlayerRealmState s) => !GameNet.NetAuthority.IsNetworked && IsLocal(s);

        /// <summary>
        /// Is <paramref name="s"/> a remote client's player on a server? Their client loads the destination only now, so they
        /// stay frozen after the server placed them until the client confirms it has the ground (or the timeout).
        /// </summary>
        private static bool NeedsClientArrival(PlayerRealmState s)
        {
            if (s == null || s.Entity == null || !GameNet.NetAuthority.IsNetworked || !GameNet.NetAuthority.IsServer)
                return false;
            GameObject mover = RealmTravel.Mover(s.Entity);
            return mover != null && GameNet.NetAuthority.IsNetworkedObject(mover) && !GameNet.NetAuthority.IsLocallyControlled(mover);
        }

        private void HoldForClient(PlayerRealmState s)
        {
            arrivalSerial = arrivalSerial >= int.MaxValue - 1 ? 1 : arrivalSerial + 1;
            s.ArrivalToken = arrivalSerial;
            arrivalHolds[s] = Time.unscaledTime + ClientArrivalTimeout;
        }

        private void ClearArrivalHold(PlayerRealmState s)
        {
            if (s == null)
                return;
            s.ArrivalToken = 0;
            arrivalHolds.Remove(s);
        }

        /// <summary>
        /// Server side: <paramref name="player"/>'s client has loaded where the server put them and has the ground under
        /// them (<paramref name="token"/> = their <see cref="PlayerRealmState.ArrivalToken"/>): their controls come back.
        /// False when nothing was waiting for that token (stale or forged confirmations are ignored).
        /// </summary>
        public bool ConfirmArrival(CombatEntity player, int token)
        {
            if (IsMirror || token == 0)
                return false;
            PlayerRealmState s = PlayerRealmState.Of(player);
            if (s == null || s.ArrivalToken != token || !arrivalHolds.ContainsKey(s))
                return false;
            ReleaseArrival(s);
            return true;
        }

        private void ReleaseArrival(PlayerRealmState s)
        {
            ClearArrivalHold(s);
            if (s.Entity != null && !s.InTransit && !travels.ContainsKey(s))
                RealmTravel.SetControls(s.Entity, true);
        }

        private void ExpireArrivalHolds()
        {
            if (arrivalHolds.Count == 0)
                return;
            expiredHolds.Clear();
            foreach (KeyValuePair<PlayerRealmState, float> hold in arrivalHolds)
                if (hold.Key == null || Time.unscaledTime >= hold.Value)
                    expiredHolds.Add(hold.Key);
            foreach (PlayerRealmState s in expiredHolds)
            {
                if (s == null)
                {
                    arrivalHolds.Remove(s);
                    continue;
                }
                Debug.LogWarning($"HubWorldDirector: the client of '{(s.Entity != null ? s.Entity.name : s.name)}' did not confirm its arrival in {ClientArrivalTimeout:0}s - giving the controls back anyway.", this);
                ReleaseArrival(s);
            }
            expiredHolds.Clear();
        }

        // ------------------------------------------------------------------ dungeons

        private void OnRunEntered(DungeonRun run)
        {
            if (IsMirror)
                return; // the server tracks the realms
            foreach (GameObject p in run.Participants)
            {
                PlayerRealmState s = PlayerRealmState.Of(PlayerLocator.FromObject(p));
                if (s == null)
                    continue;
                if (s.WorldCycle < 0)
                    s.WorldCycle = CycleIndex;
                SetRealm(s, RealmKind.Dungeon);
            }
            ScheduleWorldUnload();
        }

        /// <summary>A player joined a shared or linked portal's dungeon that was already going: they are in a dungeon now.</summary>
        private void OnPlayerJoinedRun(DungeonRun run, GameObject player)
        {
            if (IsMirror)
                return;
            PlayerRealmState s = PlayerRealmState.Of(PlayerLocator.FromObject(player));
            if (s == null)
                return;
            if (s.WorldCycle < 0)
                s.WorldCycle = CycleIndex;
            SetRealm(s, RealmKind.Dungeon);
            ScheduleWorldUnload();
        }

        /// <summary>
        /// Where a player goes when their dungeon run ends (called by <see cref="DungeonSession"/>): back to the world they
        /// came from if its cycle is still running, otherwise to the city with the jump's penalties.
        /// </summary>
        public bool TakePlayer(DungeonRun run, GameObject player, Pose returnPose, Scene returnScene, DungeonRunEnd how)
        {
            if (IsMirror)
                return true; // a network client moves nobody: the server's director takes the player out
            PlayerRealmState s = PlayerRealmState.Of(PlayerLocator.FromObject(player));
            if (s == null)
                return false;
            if (how == DungeonRunEnd.Failed && s.Realm == RealmKind.World && !IsJumping)
                return false; // never got in: they are still in front of the portal, in the world's scene
            if (s.WorldCycle < 0)
                s.WorldCycle = CycleIndex;
            RealmKind destination = CycleRules.DungeonExitDestination(s.WorldCycle, CycleIndex);
            if (destination == RealmKind.City)
            {
                bool penalised = CycleRules.PenalisedOnDungeonExit(s.WorldCycle, CycleIndex);
                if (penalised)
                    Log($"{s.Entity.name} left a dungeon after the jump: lost between dimensions.");
                TravelToCity(s, penalised, true);
                return true;
            }
            TravelToWorld(s, returnPose, true);
            return true;
        }

        // ------------------------------------------------------------------ the World scene

        /// <summary>The World scene of the current cycle (a modifier may name its own).</summary>
        public string WorldSceneName() =>
            CurrentModifier != null && !string.IsNullOrEmpty(CurrentModifier.worldSceneOverride) ? CurrentModifier.worldSceneOverride : settings.worldSceneName;

        /// <summary>Is any player out in the world (or on the way there)?</summary>
        public bool WorldOccupied()
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerRealmState s = players[i];
                if (s == null)
                    continue;
                if (s.Realm == RealmKind.World || (s.Realm == RealmKind.Transit && s.Destination == RealmKind.World))
                    return true;
                if (s.Realm == RealmKind.Dungeon && !DungeonScenes.Enabled)
                    return true; // classic mode: the dungeon is built under the world
            }
            return false;
        }

        private bool CanUnloadWorld() => settings.unloadWorldWhenEmpty && WorldState == WorldLoadState.Ready && !IsJumping && !WorldOccupied();

        private void ScheduleWorldUnload()
        {
            if (CanUnloadWorld())
                unloadWorldAt = Time.time + 3f; // a short grace: stepping out and straight back in reloads nothing
        }

        /// <summary>Loads the World scene of the current cycle if needed; <paramref name="onDone"/>(success) when ready.</summary>
        public void EnsureWorld(Action<bool> onDone)
        {
            switch (WorldState)
            {
                case WorldLoadState.Ready:
                    onDone?.Invoke(true);
                    return;
                case WorldLoadState.Loading:
                    if (onDone != null) worldReadyQueue.Add(onDone);
                    return;
                case WorldLoadState.Unloading:
                    if (onDone != null) worldReadyQueue.Add(onDone);
                    loadWorldAfterUnload = true;
                    return;
                default:
                    if (onDone != null) worldReadyQueue.Add(onDone);
                    StartCoroutine(LoadWorld());
                    return;
            }
        }

        private IEnumerator LoadWorld()
        {
            WorldState = WorldLoadState.Loading;
            unloadWorldAt = -1f;
            InstallWorldRules(); // the seed and areas must be set before the terrain wakes up
            string sceneName = WorldSceneName();
            if (GameNet.NetScenes.ThroughNetwork && !string.IsNullOrEmpty(sceneName))
            {
                // Through Fish-Net: the server loads it alone; each client loads it when one of its players is moved in.
                bool finished = false;
                Scene loaded = default;
                GameNet.NetScenes.LoadAdditive(sceneName, LocalPhysicsMode.None, false, scene =>
                {
                    loaded = scene;
                    finished = true;
                });
                float waited = 0f;
                while (!finished && waited < NetworkSceneTimeout)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }
                if (!loaded.IsValid() || !loaded.isLoaded)
                {
                    Debug.LogError($"HubWorldDirector: the World scene '{sceneName}' could not be loaded through the network - add it to File ▸ Build Settings.", this);
                    WorldState = WorldLoadState.Unloaded;
                    FlushWorldQueue(false);
                    yield break;
                }
                WorldScene = loaded;
            }
            else
            {
                AsyncOperation op = string.IsNullOrEmpty(sceneName) ? null : SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"HubWorldDirector: cannot load the World scene '{sceneName}' - add it to File ▸ Build Settings.", this);
                    WorldState = WorldLoadState.Unloaded;
                    FlushWorldQueue(false);
                    yield break;
                }
                while (!op.isDone)
                    yield return null;
                WorldScene = FindLoadedScene(sceneName);
            }
            loadedWorldName = WorldScene.IsValid() ? WorldScene.name : sceneName;
            WorldState = WorldLoadState.Ready;
            if (RealmScene.ForScene(WorldScene) == null)
                Debug.LogWarning($"HubWorldDirector: the World scene '{sceneName}' has no RealmScene (Realm = World) - players arrive at the city centre.", this);
            ConfigureWorld();
            Log($"World loaded: '{sceneName}', seed {WorldSeed}.");
            WorldLoaded?.Invoke();
            FlushWorldQueue(true);
        }

        private void FlushWorldQueue(bool success)
        {
            var queue = new List<Action<bool>>(worldReadyQueue);
            worldReadyQueue.Clear();
            foreach (Action<bool> a in queue)
            {
                try { a(success); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        /// <summary>Unloads the World scene (after any load in progress); <paramref name="then"/> when it is gone.</summary>
        public void UnloadWorld(Action then)
        {
            StartCoroutine(UnloadWorldRoutine(then));
        }

        private IEnumerator UnloadWorldRoutine(Action then)
        {
            while (WorldState == WorldLoadState.Loading || WorldState == WorldLoadState.Unloading)
                yield return null;
            if (WorldState == WorldLoadState.Ready && WorldScene.IsValid() && WorldScene.isLoaded)
            {
                WorldState = WorldLoadState.Unloading;
                RealmScene city = RealmScene.Find(RealmKind.City);
                if (SceneManager.GetActiveScene() == WorldScene && city != null)
                    SceneManager.SetActiveScene(city.Scene);
                if (GameNet.NetScenes.ThroughNetwork)
                {
                    // Through Fish-Net: the clients still in it unload it too.
                    bool gone = false;
                    GameNet.NetScenes.Unload(WorldScene, () => gone = true);
                    float waited = 0f;
                    while (!gone && waited < NetworkSceneTimeout)
                    {
                        yield return null;
                        waited += Time.unscaledDeltaTime;
                    }
                }
                else
                {
                    AsyncOperation op = SceneManager.UnloadSceneAsync(WorldScene);
                    while (op != null && !op.isDone)
                        yield return null;
                }
                Log($"World unloaded ('{loadedWorldName}').");
            }
            WorldScene = default;
            worldTerrains.Clear();
            worldWeathers.Clear();
            WorldState = WorldLoadState.Unloaded;
            WorldUnloaded?.Invoke();
            then?.Invoke();
            if (loadWorldAfterUnload || worldReadyQueue.Count > 0)
            {
                loadWorldAfterUnload = false;
                if (WorldState == WorldLoadState.Unloaded)
                    StartCoroutine(LoadWorld());
            }
        }

        /// <summary>Sets up a freshly loaded world: the cycle's weather, and the viewer of its terrain.</summary>
        private void ConfigureWorld()
        {
            worldTerrains.Clear();
            worldWeathers.Clear();
            if (!WorldScene.IsValid())
                return;
            // Found once: the terrain's hierarchy grows large, it must not be searched every frame.
            foreach (GameObject root in WorldScene.GetRootGameObjects())
            {
                worldTerrains.AddRange(root.GetComponentsInChildren<EndlessTerrain>(true));
                worldWeathers.AddRange(root.GetComponentsInChildren<WeatherSystem>(true));
            }
            if (CurrentModifier != null && CurrentModifier.forceWeather)
                foreach (WeatherSystem weather in worldWeathers)
                    weather.ForceWeather(CurrentModifier.weather, CurrentModifier.weatherStrength, 0f);
            UpdatePresentation();
        }

        /// <summary>A loaded scene by name or path ("World", "Assets/Scenes/World.unity").</summary>
        private static Scene FindLoadedScene(string nameOrPath)
        {
            Scene byName = SceneManager.GetSceneByName(nameOrPath);
            if (byName.IsValid() && byName.isLoaded)
                return byName;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && (s.path == nameOrPath || s.path.EndsWith("/" + nameOrPath + ".unity", StringComparison.Ordinal)))
                    return s;
            }
            return default;
        }

        /// <summary>The world-side rules of this cycle: the seed, the city's levelled and kept-clear area, the spawn hooks.</summary>
        private void InstallWorldRules()
        {
            TerrainGenerator.SeedOverride = WorldSeed;
            ZoneSettings z = settings.zones;
            WorldAreaRules rules = WorldAreaRules.NoObjects | WorldAreaRules.NoMobs | WorldAreaRules.NoPortals | WorldAreaRules.NoWater;
            if (z.flattenCityGround)
                rules |= WorldAreaRules.Flatten;
            WorldAreas.Set(new[]
            {
                new WorldArea(z.cityCenter, Mathf.Max(z.cityWallRadius, z.keepClearRadius), z.flattenBlend, z.cityGroundHeight, rules),
            });
            WorldSpawnHooks.PortalDifficulty = (position, _) =>
                Zones.At(position).dungeonDifficulty * (CurrentModifier != null ? CurrentModifier.dungeonDifficultyMultiplier : 1f);
            WorldSpawnHooks.MobSpawned = (mob, type, position) =>
            {
                float strength = settings.zones.scaleWorldMobs ? Zones.At(position).mobStrength : 1f;
                if (CurrentModifier != null)
                    strength *= CurrentModifier.mobStrengthMultiplier;
                ZoneMobStrength.Apply(mob, strength);
            };
            WorldSpawnHooks.MobWeight = (type, position) => CurrentModifier != null && type != null ? CurrentModifier.MobWeightFor(type.mobPrefab) : 1f;
            WorldSpawnHooks.DungeonType = (position, seed) => CurrentModifier != null ? CurrentModifier.ProfileFor(seed) : null;
            // The rings around the city, for the zone rules of portal and mob types (Spawnable Portal / Mob > Zones).
            WorldSpawnHooks.ZoneAt = position =>
            {
                ZoneInfo zone = Zones.At(position);
                return new SpawnZone { Known = true, Index = zone.index, Name = zone.name, Pvp = zone.pvp };
            };
        }

        // ------------------------------------------------------------------ this machine's view

        /// <summary>
        /// What this machine shows: the City's objects are switched off while the local player is out (the World's exterior
        /// copy stands in the same place), and the world's terrain streams around a player who is out there.
        /// </summary>
        private void UpdatePresentation()
        {
            PlayerRealmState local = LocalState();
            if (local == null && players.Count == 1 && !GameNet.NetAuthority.IsNetworked)
                local = players[0];
            RealmScene city = RealmScene.Find(RealmKind.City);
            if (city != null && GameNet.NetAuthority.HasLocalPlayer) // a dedicated server shows nothing and hides nothing
            {
                bool inCity = local == null || local.Realm == RealmKind.City || local.Realm == RealmKind.None ||
                              (local.Realm == RealmKind.Transit && local.Destination == RealmKind.City);
                if (city.IsHidden == inCity)
                    city.SetHidden(!inCity);
            }

            if (WorldState != WorldLoadState.Ready || !WorldScene.IsValid())
                return;
            // This machine's player first; a server also streams the terrain around every other player out there (a
            // client only knows its own surroundings).
            viewers.Clear();
            if (local != null && local.Entity != null && HeadingForWorld(local))
                viewers.Add(local.Entity.transform);
            if (!IsMirror)
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && players[i].Entity != null && HeadingForWorld(players[i]) && !viewers.Contains(players[i].Entity.transform))
                        viewers.Add(players[i].Entity.transform);
            Transform viewer = viewers.Count > 0 ? viewers[0] : null;
            for (int i = 0; i < worldTerrains.Count; i++)
            {
                EndlessTerrain terrain = worldTerrains[i];
                if (terrain == null)
                    continue;
                if (TerrainViewers != null && TerrainViewers(terrain, viewers))
                    continue;
                if (terrain.viewer != viewer)
                    terrain.viewer = viewer;
            }
            if (viewer != null)
                for (int i = 0; i < worldWeathers.Count; i++)
                    if (worldWeathers[i] != null && worldWeathers[i].Viewer != viewer)
                        worldWeathers[i].Viewer = viewer;
        }

        private static bool HeadingForWorld(PlayerRealmState s) => s.InWorld || (s.InTransit && s.Destination == RealmKind.World);

        // ------------------------------------------------------------------ the City scene

        private IEnumerator LoadCity()
        {
            if (cityLoading)
                yield break;
            if (FindLoadedScene(settings.citySceneName).IsValid())
                yield break;
            cityLoading = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(settings.citySceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"HubWorldDirector: cannot load the City scene '{settings.citySceneName}' - add it to File ▸ Build Settings.", this);
                cityLoading = false;
                yield break;
            }
            while (!op.isDone)
                yield return null;
            cityLoading = false;
            Scene scene = FindLoadedScene(settings.citySceneName);
            PlayerRealmState local = LocalState();
            if (scene.IsValid() && (local == null || local.Realm == RealmKind.None || local.InCity))
                SceneManager.SetActiveScene(scene);
            ScanPlayers();
        }

        // ------------------------------------------------------------------ network: the server's side

        /// <summary>The length of a cycle in seconds (what the clock runs on).</summary>
        public double CycleDuration => Clock != null ? Clock.Duration : settings.CycleSeconds;

        /// <summary>The index of <paramref name="modifier"/> in the settings' modifier list (-1 = none or unknown): what is sent over the network.</summary>
        public int ModifierIndex(DimensionalModifier modifier) =>
            modifier == null || settings.modifiers == null ? -1 : settings.modifiers.IndexOf(modifier);

        /// <summary>The modifier at <paramref name="index"/> in the settings' list, or null (out of range = none).</summary>
        public DimensionalModifier ModifierAt(int index) =>
            settings.modifiers != null && index >= 0 && index < settings.modifiers.Count ? settings.modifiers[index] : null;

        /// <summary>
        /// Server side: uses <paramref name="seed"/> as the shard seed (a dedicated server's <c>-shard</c> argument): the
        /// worlds, modifiers and loot-loss rolls of every cycle follow from it. Best called before anyone went out; a World
        /// already loaded keeps its seed until the next jump.
        /// </summary>
        public void UseShardSeed(int seed)
        {
            if (IsMirror || seed == 0 || seed == ShardSeed)
                return;
            ShardSeed = seed;
            penaltyRandom = new System.Random(ShardSeed ^ 0x5EED);
            CurrentModifier = RollModifier(CycleIndex);
            NextModifier = RollModifier(CycleIndex + 1);
            if (WorldState == WorldLoadState.Unloaded)
                InstallWorldRules();
            else
                Debug.LogWarning($"HubWorldDirector: shard seed {seed} set while a World is loaded - it applies from the next jump.", this);
            Log($"Shard seed {seed}: cycle {CycleIndex} world seed {WorldSeed}{(CurrentModifier != null ? ", " + CurrentModifier.displayName : "")}.");
        }

        // ------------------------------------------------------------------ network: a client's mirror

        /// <summary>
        /// Network clients: the server's clock - cycle <paramref name="cycleIndex"/>, <paramref name="secondsLeft"/> until
        /// the jump, cycles of <paramref name="duration"/> seconds. The local clock is only moved when it drifted (more than a
        /// quarter of a second), so the countdown runs smoothly between updates. Ignored where the rules run.
        /// </summary>
        public void ApplyServerCycle(int cycleIndex, double secondsLeft, double duration)
        {
            if (!IsMirror || Clock == null)
                return;
            if (duration >= 1d && Math.Abs(Clock.Duration - duration) > 0.001d)
                Clock.SetDuration(duration);
            if (double.IsNaN(secondsLeft) || double.IsInfinity(secondsLeft))
                return;
            secondsLeft = Math.Max(0d, Math.Min(Clock.Duration, secondsLeft));
            double end = Now + secondsLeft;
            if (Clock.CycleIndex != cycleIndex || Math.Abs(Clock.CycleEnd - end) > 0.25d)
                Clock.Restore(cycleIndex, end - Clock.Duration, Now);
        }

        /// <summary>
        /// Network clients: the server's world - its seed (never the shard seed or a dungeon seed) and the current and next
        /// modifiers (indexes in the settings' list, -1 = none). The world rules (terrain seed, the city's levelled area)
        /// are installed at once, so a World scene loaded afterwards generates the server's terrain.
        /// </summary>
        public void ApplyServerWorld(int worldSeed, int currentModifier, int nextModifier)
        {
            if (!IsMirror)
                return;
            bool first = !mirrorSynced;
            DimensionalModifier current = ModifierAt(currentModifier);
            bool rulesChanged = first || worldSeed != mirroredWorldSeed || current != CurrentModifier;
            mirroredWorldSeed = worldSeed;
            mirrorSynced = true;
            CurrentModifier = current;
            NextModifier = ModifierAt(nextModifier);
            if (rulesChanged)
                InstallWorldRules();
            if (first)
            {
                Log($"Joined cycle {CycleIndex} (world seed {WorldSeed}{(CurrentModifier != null ? ", " + CurrentModifier.displayName : "")}).");
                if (CurrentModifier != null)
                    HubWorldMessages.DeliverLocal(null, $"This dimension: {CurrentModifier.displayName}", HubMessageKind.Info);
            }
        }

        /// <summary>Network clients: the server's jumping flag and whether its city gate is open.</summary>
        public void ApplyServerFlags(bool jumping, bool gateOpen)
        {
            if (!IsMirror)
                return;
            IsJumping = jumping;
            mirroredGateOpen = gateOpen;
        }

        /// <summary>Network clients: a warning of the server's cycle - its sound and <see cref="CycleWarning"/> (the text arrives as a message).</summary>
        public void ShowServerWarning(float secondsLeft)
        {
            if (!IsMirror || Clock == null)
                return;
            PlaySound(IsFinalWarning(secondsLeft) && settings.finalWarningSound != null ? settings.finalWarningSound : settings.warningSound);
            Log($"Warning: {secondsLeft:0}s before the jump.");
            CycleWarning?.Invoke(secondsLeft);
        }

        /// <summary>
        /// Network clients: the server's city jumped into cycle <paramref name="cycleIndex"/> (apply the new world with
        /// <see cref="ApplyServerWorld"/> first): the jump's sound, <see cref="CycleJumped"/> and <see cref="ModifierAnnounced"/>.
        /// </summary>
        public void ShowServerJump(int cycleIndex)
        {
            if (!IsMirror)
                return;
            IsJumping = true;
            PlaySound(settings.jumpSound);
            Log($"Jump! Cycle {cycleIndex} begins (world seed {WorldSeed}).");
            CycleJumped?.Invoke(cycleIndex);
            ModifierAnnounced?.Invoke(NextModifier);
        }

        /// <summary>Network clients: <paramref name="player"/> (this machine's) was lost between dimensions on the server.</summary>
        public void ShowLostBetweenDimensions(CombatEntity player, int itemsLost, int stacksHit, int currencyLost, IReadOnlyList<string> lines)
        {
            if (!IsMirror || player == null)
                return;
            var loss = new CyclePenalties.LootLossResult
            {
                itemsLost = itemsLost,
                stacksHit = stacksHit,
                currencyLost = currencyLost,
                lines = lines != null ? new List<string>(lines) : new List<string>(),
            };
            PlayerLostBetweenDimensions?.Invoke(player, loss);
        }

        /// <summary>
        /// Network clients: a player's realm as the server decided it (realm, destination while in transit, the cycle they
        /// went out in, their zone, the arrival token). Raises <see cref="PlayerRealmChanged"/> / <see cref="PlayerZoneChanged"/>
        /// like the server does. Ignored where the rules run.
        /// </summary>
        public void ApplyReplicatedRealm(CombatEntity player, RealmKind realm, RealmKind destination, int worldCycle, ZoneInfo zone, int arrivalToken)
        {
            if (!IsMirror || player == null)
                return;
            PlayerRealmState s = EnsureState(player);
            s.WorldCycle = worldCycle;
            s.ArrivalToken = arrivalToken;
            RealmKind previous = s.Realm;
            s.SetRealm(realm, destination);
            if (previous != s.Realm)
            {
                Log($"{player.name}: {previous} -> {s.Realm} (server)");
                PlayerRealmChanged?.Invoke(player, previous, s.Realm);
            }
            ZoneInfo previousZone = s.Zone;
            s.SetZone(zone);
            if (previousZone.index != zone.index || previousZone.pvp != zone.pvp || previousZone.name != zone.name)
                PlayerZoneChanged?.Invoke(player, previousZone, zone);
            if (IsLocal(s))
                UpdatePresentation();
        }

        /// <summary>
        /// The process joined or left a network session while this director existed: a new client drops its own travels
        /// (the server decides from now on); a process back offline forgets the server's mirrored values.
        /// </summary>
        private void OnNetModeChanged(GameNet.NetMode mode)
        {
            foreach (PlayerRealmState s in new List<PlayerRealmState>(arrivalHolds.Keys))
                ClearArrivalHold(s);
            arrivalHolds.Clear();
            if (mode == GameNet.NetMode.Client)
            {
                foreach (PlayerRealmState s in new List<PlayerRealmState>(travels.Keys))
                    CancelTravel(s);
                worldReadyQueue.Clear();
            }
            if (mode != GameNet.NetMode.Client && mirrorSynced)
            {
                mirrorSynced = false;
                IsJumping = false;
            }
        }

        /// <summary>A network client adopts the World scene it loaded (its player was moved there by the server).</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsMirror || !scene.IsValid())
                return;
            RealmScene realm = RealmScene.ForScene(scene);
            if (realm == null || realm.realm != RealmKind.World || (WorldScene == scene && WorldState == WorldLoadState.Ready))
                return;
            WorldScene = scene;
            loadedWorldName = scene.name;
            WorldState = WorldLoadState.Ready;
            ConfigureWorld();
            Log($"World loaded here: '{scene.name}', seed {WorldSeed}.");
            WorldLoaded?.Invoke();
        }

        /// <summary>A network client forgets the World scene once it unloaded it (its player left, or the server's jump).</summary>
        private void OnSceneUnloaded(Scene scene)
        {
            // Compared by handle: an unloaded scene is no longer valid.
            if (!IsMirror || WorldState == WorldLoadState.Unloaded || scene.handle != WorldScene.handle)
                return;
            WorldScene = default;
            worldTerrains.Clear();
            worldWeathers.Clear();
            WorldState = WorldLoadState.Unloaded;
            Log($"World unloaded here ('{loadedWorldName}').");
            WorldUnloaded?.Invoke();
        }

        // ------------------------------------------------------------------ admin / debug

        /// <summary>Ends the cycle now (admin command, tests).</summary>
        [ContextMenu("Jump Now")]
        public void JumpNow()
        {
            if (Clock != null && !IsMirror)
                Clock.EndNow(Now);
        }

        /// <summary>Moves the cycle so <paramref name="seconds"/> remain (admin command, tests). Later warnings will still fire.</summary>
        public void SetSecondsUntilJump(double seconds)
        {
            if (Clock != null && !IsMirror)
                Clock.SetRemaining(seconds, Now);
        }

        [ContextMenu("Skip To 70 Seconds Before The Jump")]
        private void SkipToFinalMinute() => SetSecondsUntilJump(70d);

        [ContextMenu("Skip To 5 Minutes 10 Seconds Before The Jump")]
        private void SkipToFiveMinutes() => SetSecondsUntilJump(310d);

        // ------------------------------------------------------------------ helpers

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, settings.soundVolume);
        }

        private void Log(string message)
        {
            if (settings != null && settings.logEvents)
                Debug.Log("[Hub World] " + message, this);
        }
    }
}
