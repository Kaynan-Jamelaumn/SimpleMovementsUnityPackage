#if FISHNET
using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Utility.Template;
using HubWorld;
using ProceduralDungeon;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a character's combat state: health, death, crowd control, buffs and debuffs, speed and weight for
    /// everyone who sees it; mana, survival needs, experience and points for its owner; the trait list for the player's
    /// own movement traits. Put it on the root of the player prefab (next to <see cref="NetPlayer"/>) and on the root of
    /// every networked mob prefab, next to their NetworkObject.
    /// </summary>
    /// <remarks>
    /// <para>The server (or host) runs every status rule unchanged - regeneration, hunger, effects, damage, death - and
    /// this component polls the results each tick into SyncVars (nothing is sent while values are steady). On a pure
    /// client every manager of the copy becomes a replica (<see cref="StatusManager.IsReplica"/>,
    /// <see cref="CombatEntity.IsReplica"/>...): it simulates nothing, ignores local gameplay writes and shows the
    /// server's values, applied in the SyncVar change handlers. Stamina is the exception: the movement module predicts
    /// it on the owner and corrects it with its reconcile, so only its limits (max, regeneration, factors) come from
    /// here.</para>
    /// <para>Hits and heals reach the clients as presentation-only events (<see cref="CombatEntity.ReplicatedDamage"/>,
    /// <see cref="CombatEntity.DamageFeedback"/>), deaths as <see cref="CombatEntity.ReplicatedLifeChanged"/>: no
    /// gameplay event (Died, Killed, Damaged) is ever raised on a client, so loot, absorption and kill rewards happen
    /// once, on the server.</para>
    /// <para>Dead players are revived by the server after <see cref="respawnDelay"/> at the City spawn point with
    /// <see cref="respawnHealthFraction"/> of their health (duels never kill, so they never respawn from a duel).</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetCombatStatus : TickNetworkBehaviour
    {
        // ------------------------------------------------------------------ wire types

        /// <summary>A resource as sent: current and max value.</summary>
        public struct Resource : IEquatable<Resource>
        {
            public float current;
            public float max;

            public bool Equals(Resource o) => current == o.current && max == o.max;
            public override bool Equals(object obj) => obj is Resource o && Equals(o);
            public override int GetHashCode() => current.GetHashCode() * 397 ^ max.GetHashCode();
        }

        /// <summary>The speed values the movement step reads (exact, so prediction matches the server).</summary>
        public struct SpeedValues : IEquatable<SpeedValues>
        {
            public float current;
            public float baseSpeed;
            public float runMultiplier;
            public float crouchMultiplier;

            public bool Equals(SpeedValues o) => current == o.current && baseSpeed == o.baseSpeed &&
                                                 runMultiplier == o.runMultiplier && crouchMultiplier == o.crouchMultiplier;
            public override bool Equals(object obj) => obj is SpeedValues o && Equals(o);
            public override int GetHashCode() => current.GetHashCode() * 397 ^ baseSpeed.GetHashCode() ^ runMultiplier.GetHashCode() * 31 ^ crouchMultiplier.GetHashCode();
        }

        /// <summary>Stamina limits decided by the server (the current value is predicted by the movement module).</summary>
        public struct StaminaLimits : IEquatable<StaminaLimits>
        {
            public float max;
            public float increment;
            public float gainFactor;
            public float lossFactor;

            public bool Equals(StaminaLimits o) => max == o.max && increment == o.increment && gainFactor == o.gainFactor && lossFactor == o.lossFactor;
            public override bool Equals(object obj) => obj is StaminaLimits o && Equals(o);
            public override int GetHashCode() => max.GetHashCode() * 397 ^ increment.GetHashCode() ^ gainFactor.GetHashCode() * 31 ^ lossFactor.GetHashCode();
        }

        /// <summary>Crowd control as server END TICKS (0 = not running): no value changes while a timer runs down.</summary>
        public struct ControlState : IEquatable<ControlState>
        {
            public uint stunEnd;
            public uint silenceEnd;
            public uint rootEnd;
            public uint slowEnd;
            public uint tauntEnd;
            public uint invulnerableEnd;
            public float slowFraction;
            /// <summary>Who taunted the character (null = nobody or not networked).</summary>
            public NetworkObject tauntSource;

            public bool Equals(ControlState o) => stunEnd == o.stunEnd && silenceEnd == o.silenceEnd && rootEnd == o.rootEnd &&
                                                  slowEnd == o.slowEnd && tauntEnd == o.tauntEnd && invulnerableEnd == o.invulnerableEnd &&
                                                  slowFraction == o.slowFraction && tauntSource == o.tauntSource;
            public override bool Equals(object obj) => obj is ControlState o && Equals(o);
            public override int GetHashCode() => (int)(stunEnd * 397 ^ silenceEnd ^ rootEnd * 31 ^ slowEnd ^ tauntEnd * 17 ^ invulnerableEnd);
        }

        /// <summary>Sleep and breathing state of the owner (flags from <see cref="StatusReplication.PackSurvival"/>).</summary>
        public struct SurvivalState : IEquatable<SurvivalState>
        {
            public byte flags;
            public float oxygenTank;
            public float environmentTemperature;

            public bool Equals(SurvivalState o) => flags == o.flags && oxygenTank == o.oxygenTank && environmentTemperature == o.environmentTemperature;
            public override bool Equals(object obj) => obj is SurvivalState o && Equals(o);
            public override int GetHashCode() => flags * 397 ^ oxygenTank.GetHashCode() ^ environmentTemperature.GetHashCode() * 31;
        }

        /// <summary>Unconsciousness (everyone who sees the character: the downed pose, revive prompts).</summary>
        public struct DownedState : IEquatable<DownedState>
        {
            public bool down;
            /// <summary>Server tick when the character dies if nobody revives it (0 = no timer).</summary>
            public uint endTick;
            /// <summary>Damage below 0 health so far (quantized).</summary>
            public float deficit;
            /// <summary>Asleep (other players see the sleeping pose; the owner gets it with its survival state too).</summary>
            public bool sleeping;

            public bool Equals(DownedState o) => down == o.down && endTick == o.endTick && deficit == o.deficit && sleeping == o.sleeping;
            public override bool Equals(object obj) => obj is DownedState o && Equals(o);
            public override int GetHashCode() => (down ? 1 : 0) * 397 ^ (int)endTick ^ deficit.GetHashCode();
        }

        /// <summary>The owner's progression (the level itself is sent to everyone).</summary>
        public struct Progress : IEquatable<Progress>
        {
            public int experience;
            public int experienceToNext;
            public int skillPoints;
            public int attributePoints;
            public int statUpgradePoints;
            public int traitPoints;

            public bool Equals(Progress o) => experience == o.experience && experienceToNext == o.experienceToNext &&
                                              skillPoints == o.skillPoints && attributePoints == o.attributePoints &&
                                              statUpgradePoints == o.statUpgradePoints && traitPoints == o.traitPoints;
            public override bool Equals(object obj) => obj is Progress o && Equals(o);
            public override int GetHashCode() => experience * 397 ^ experienceToNext ^ skillPoints * 31 ^ attributePoints * 17 ^ statUpgradePoints * 7 ^ traitPoints;
        }

        /// <summary>One buff or debuff as shown to clients (display only).</summary>
        public struct BuffView : IEquatable<BuffView>
        {
            public string name;
            public bool debuff;
            public byte element;
            public float duration;
            public uint endTick;

            public bool Equals(BuffView o) => name == o.name && debuff == o.debuff && element == o.element && duration == o.duration && endTick == o.endTick;
            public override bool Equals(object obj) => obj is BuffView o && Equals(o);
            public override int GetHashCode() => (name != null ? name.GetHashCode() : 0) * 397 ^ (int)endTick;
        }

        /// <summary>A hit as reported to the clients that see the character (presentation only).</summary>
        public struct HitEvent
        {
            public float amount;
            public byte element;
            public byte damageType;
            public byte delivery;
            /// <summary><see cref="StatusReplication.HitFlags"/>.</summary>
            public byte flags;
            public NetworkObject source;
            public Vector3 point;
            public Vector3 direction;
        }

        // ------------------------------------------------------------------ settings

        [Header("Respawn (networked players)")]
        [Tooltip("The server revives dead players (outside a running dungeon) at the City spawn point.")]
        public bool respawnPlayers = true;

        [Tooltip("Seconds a dead player waits before the server revives them.")]
        [Min(0f)] public float respawnDelay = 5f;

        [Tooltip("Health the player comes back with (fraction of the max).")]
        [Range(0.05f, 1f)] public float respawnHealthFraction = 0.5f;

        [Tooltip("Seconds of invulnerability after a respawn (0 = none).")]
        [Min(0f)] public float spawnProtection = 2f;

        [Tooltip("Apply the hub world's arrival penalties when the player respawns in the City.")]
        public bool respawnPenalties;

        [Tooltip("When every member of a dungeon run is dead, the server ends the run (the group leaves) and respawns them in " +
                 "the City. Off: dead dungeon players wait for a rest point or the dungeon's own rules.")]
        public bool endDungeonOnWipe = true;

        [Header("Feedback")]
        [Tooltip("Send hits and heals to clients as presentation events (damage numbers, hit flashes, reactions).")]
        public bool replicateHits = true;

        [Header("Requests")]
        [Tooltip("Minimum seconds between two requests of the same kind from the owner (stat upgrade, sleep).")]
        [Min(0f)] public float requestInterval = 0.25f;

        // ------------------------------------------------------------------ synchronized state

        // Everyone who sees the character. (Send rates are explicit: settings equal to Fish-Net's defaults would be
        // replaced by the manager's rate.)
        private readonly SyncVar<Resource> health = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.05f, Channel.Reliable));
        private readonly SyncVar<bool> dead = new SyncVar<bool>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.02f, Channel.Reliable));
        private readonly SyncVar<ControlState> control = new SyncVar<ControlState>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.02f, Channel.Reliable));
        private readonly SyncVar<int> level = new SyncVar<int>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Reliable));
        private readonly SyncVar<SpeedValues> speed = new SyncVar<SpeedValues>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.05f, Channel.Reliable));
        private readonly SyncVar<Resource> weight = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Reliable));
        private readonly SyncVar<StaminaLimits> staminaLimits = new SyncVar<StaminaLimits>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Reliable));
        private readonly SyncList<BuffView> buffs = new SyncList<BuffView>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Reliable));
        private readonly SyncList<string> traits = new SyncList<string>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.1f, Channel.Reliable));

        // The owner only.
        private readonly SyncVar<Resource> mana = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.1f, Channel.Reliable));
        private readonly SyncVar<Resource> hunger = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<Resource> thirst = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<Resource> sleepiness = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<Resource> sanity = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<Resource> bodyHeat = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<Resource> oxygen = new SyncVar<Resource>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.25f, Channel.Reliable));
        private readonly SyncVar<SurvivalState> survival = new SyncVar<SurvivalState>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.1f, Channel.Reliable));
        private readonly SyncVar<Progress> progress = new SyncVar<Progress>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.1f, Channel.Reliable));
        private readonly SyncVar<uint> respawnTick = new SyncVar<uint>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0f, Channel.Reliable));
        private readonly SyncVar<DownedState> downed = new SyncVar<DownedState>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.05f, Channel.Reliable));

        // ------------------------------------------------------------------ runtime

        private static readonly Dictionary<CombatEntity, NetCombatStatus> byEntity =
            new Dictionary<CombatEntity, NetCombatStatus>(new ReferenceComparer<CombatEntity>());

        private bool resolved;
        private CombatEntity entity;
        private PlayerStatusController playerStatus;
        private MobStatusController mobStatus;
        private HealthManager healthManager;
        private SpeedManager speedManager;
        private TraitManager traitManager;
        private ExperienceManager xpManager;
        private TimedStatModifiers timed;

        // Server.
        private bool serverHooked;
        private CombatEntity.ControlTimers lastTimers;
        private bool timersSent;
        private Coroutine respawnRoutine;
        private Pose spawnPose;
        private bool hasSpawnPose;
        private float lastUpgradeRequest = -1000f;
        private float lastSleepRequest = -1000f;
        private float lastGiveUpRequest = -1000f;
        private readonly List<string> traitNames = new List<string>();

        // Client.
        private bool replicaSet;
        private bool clientHooked;
        private bool reapplyPending;
        private bool revertWhenEnabled;
        private bool buffsDirty;
        private bool traitsDirty;
        private readonly List<TimedStatModifiers.View> viewBuffer = new List<TimedStatModifiers.View>();
        private readonly List<string> currentTraitNames = new List<string>();
        private readonly List<string> traitsToAdd = new List<string>();
        private readonly List<string> traitsToRemove = new List<string>();
        private readonly HashSet<string> unknownTraits = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => byEntity.Clear();

        /// <summary>The status component networking <paramref name="e"/> on this machine (null when none).</summary>
        public static NetCombatStatus Of(CombatEntity e) => e != null && byEntity.TryGetValue(e, out NetCombatStatus s) ? s : null;

        /// <summary>The combat entity this component networks.</summary>
        public CombatEntity Entity
        {
            get
            {
                Resolve();
                return entity;
            }
        }

        /// <summary>
        /// Owner side: seconds until the server revives this dead player (0 when no respawn is pending). For a "respawning
        /// in..." display.
        /// </summary>
        public float RespawnSecondsLeft => TimeManager != null ? StatusReplication.SecondsLeft(respawnTick.Value, TimeManager.Tick, TimeManager.TickDelta) : 0f;

        private void Awake()
        {
            SetTickCallbacks(TickCallback.Tick);
        }

        private void OnDestroy()
        {
            Unregister();
        }

        // Finds the character's parts. Done when the network starts (after every Awake of the object ran), never earlier.
        private void Resolve()
        {
            if (resolved)
                return;
            resolved = true;
            playerStatus = GetComponentInChildren<PlayerStatusController>(true);
            mobStatus = playerStatus == null ? GetComponentInChildren<MobStatusController>(true) : null;
            BaseStatusController status = playerStatus != null ? playerStatus : mobStatus != null ? mobStatus : (BaseStatusController)GetComponentInChildren<BaseStatusController>(true);
            entity = status != null ? CombatEntity.GetOrAdd(status.gameObject) : GetComponentInChildren<CombatEntity>(true);
            if (playerStatus != null)
            {
                healthManager = playerStatus.HpManager;
                speedManager = playerStatus.SpeedManager;
                traitManager = playerStatus.TraitManager != null ? playerStatus.TraitManager : playerStatus.GetComponent<TraitManager>();
                xpManager = playerStatus.XPManager;
            }
            else if (mobStatus != null)
            {
                healthManager = mobStatus.HealthManager;
                speedManager = mobStatus.SpeedManager;
            }
            if (healthManager == null && entity != null)
                healthManager = entity.Health;
            if (entity != null)
            {
                timed = TimedStatModifiers.For(entity);
            }
            else
            {
                Debug.LogWarning($"[{name}] NetCombatStatus found no status controller or CombatEntity to network.", this);
            }
        }

        private void Unregister()
        {
            if (entity != null && byEntity.TryGetValue(entity, out NetCombatStatus s) && s == this)
                byEntity.Remove(entity);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Resolve();
            if (entity != null)
                byEntity[entity] = this;
        }

        // ------------------------------------------------------------------ server

        public override void OnStartServer()
        {
            base.OnStartServer();
            Resolve();
            if (entity == null)
                return;
            HookServer(true);
            GameObject mover = PlayerLocator.MovableRoot(entity);
            if (mover == null)
                mover = gameObject;
            spawnPose = new Pose(mover.transform.position, mover.transform.rotation);
            hasSpawnPose = true;
            timersSent = false;
            PollServer();
            RebuildBuffs();
            RebuildTraits();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HookServer(false);
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        private void HookServer(bool on)
        {
            if (on == serverHooked || entity == null)
                return;
            serverHooked = on;
            if (on)
            {
                entity.DamageFeedback += OnServerHit;
                entity.HealFeedback += OnServerHeal;
                entity.Died += OnServerDied;
                entity.Revived += OnServerRevived;
                if (timed != null)
                    timed.Changed += RebuildBuffs;
                if (traitManager != null)
                    traitManager.OnTraitsChanged += RebuildTraits;
            }
            else
            {
                entity.DamageFeedback -= OnServerHit;
                entity.HealFeedback -= OnServerHeal;
                entity.Died -= OnServerDied;
                entity.Revived -= OnServerRevived;
                if (timed != null)
                    timed.Changed -= RebuildBuffs;
                if (traitManager != null)
                    traitManager.OnTraitsChanged -= RebuildTraits;
            }
        }

        protected override void TimeManager_OnTick()
        {
            if (IsServerInitialized && entity != null)
                PollServer();
        }

        // Copies the simulation's results into the synchronized state (equal values are not sent again).
        private void PollServer()
        {
            if (healthManager != null)
                health.Value = Quantized(healthManager);
            dead.Value = entity.IsDead;
            PollControl();
            PollDowned();

            if (speedManager != null && playerStatus != null)
            {
                speed.Value = new SpeedValues
                {
                    current = speedManager.Speed,
                    baseSpeed = speedManager.BaseSpeed,
                    runMultiplier = speedManager.SpeedWhileRunningMultiplier,
                    crouchMultiplier = speedManager.SpeedWhileCrouchingMultiplier,
                };
            }
            if (playerStatus == null)
                return;

            WeightManager w = playerStatus.WeightManager;
            if (w != null)
                weight.Value = new Resource { current = w.CurrentValue, max = w.MaxValue };
            StaminaManager st = playerStatus.StaminaManager;
            if (st != null)
                staminaLimits.Value = new StaminaLimits { max = st.MaxValue, increment = st.IncrementValue, gainFactor = st.IncrementFactor, lossFactor = st.DecrementFactor };
            if (xpManager != null)
                level.Value = Mathf.Max(1, xpManager.CurrentLevel);

            // Owner-only values (not sent to anyone else, so polling them costs only the comparison).
            if (playerStatus.ManaManager != null) mana.Value = Quantized(playerStatus.ManaManager);
            if (playerStatus.HungerManager != null) hunger.Value = Quantized(playerStatus.HungerManager);
            if (playerStatus.ThirstManager != null) thirst.Value = Quantized(playerStatus.ThirstManager);
            if (playerStatus.SleepManager != null) sleepiness.Value = Quantized(playerStatus.SleepManager);
            if (playerStatus.SanityManager != null) sanity.Value = Quantized(playerStatus.SanityManager);
            if (playerStatus.BodyHeatManager != null) bodyHeat.Value = Quantized(playerStatus.BodyHeatManager);
            if (playerStatus.OxygenManager != null) oxygen.Value = Quantized(playerStatus.OxygenManager);

            SleepManager sl = playerStatus.SleepManager;
            OxygenManager ox = playerStatus.OxygenManager;
            BodyHeatManager bh = playerStatus.BodyHeatManager;
            survival.Value = new SurvivalState
            {
                flags = StatusReplication.PackSurvival(
                    sl != null && sl.IsSleeping, sl != null && sl.HasCollapsed,
                    ox != null && ox.IsUnderwater, ox != null && ox.IsAtHighAltitude, ox != null && ox.IsInPoorVentilation,
                    ox != null && ox.HasOxygenTank),
                oxygenTank = ox != null ? StatusReplication.Quantize(ox.OxygenTankAmount) : 0f,
                environmentTemperature = bh != null ? StatusReplication.Quantize(bh.EnvironmentalTemperature) : 0f,
            };

            if (xpManager != null || traitManager != null)
            {
                progress.Value = new Progress
                {
                    experience = xpManager != null ? xpManager.CurrentExperience : 0,
                    experienceToNext = xpManager != null ? xpManager.ExperienceToNextLevel : 0,
                    skillPoints = xpManager != null ? xpManager.SkillPoints : 0,
                    attributePoints = xpManager != null ? xpManager.AttributePoints : 0,
                    statUpgradePoints = xpManager != null ? xpManager.StatUpgradePoints : 0,
                    traitPoints = traitManager != null ? traitManager.AvailableTraitPoints : 0,
                };
            }
        }

        private void PollDowned()
        {
            Unconsciousness u = entity.Downed;
            bool sleeping = playerStatus != null && playerStatus.SleepManager != null && playerStatus.SleepManager.IsSleeping;
            if (u == null || !u.IsDown)
            {
                if (downed.Value.down || downed.Value.sleeping != sleeping)
                    downed.Value = new DownedState { sleeping = sleeping };
                return;
            }
            float left = u.Remaining;
            DownedState next = new DownedState
            {
                down = true,
                endTick = float.IsInfinity(left) ? 0u : StatusReplication.EndTick(Time.time + left, Time.time, TimeManager.Tick, TimeManager.TickDelta),
                deficit = StatusReplication.Quantize(u.Deficit),
                sleeping = sleeping,
            };
            // The end tick is computed from a float timer: only send it again when it really moved (not every tick).
            DownedState current = downed.Value;
            if (current.down && current.sleeping == next.sleeping && current.deficit == next.deficit &&
                (next.endTick > current.endTick ? next.endTick - current.endTick : current.endTick - next.endTick) <= 2u)
                return;
            downed.Value = next;
        }

        private static Resource Quantized(StatusManager m) => new Resource
        {
            current = StatusReplication.Quantize(m.CurrentValue),
            max = StatusReplication.Quantize(m.MaxValue),
        };

        // The timers only change when crowd control is applied, cleansed or ends early: compare the raw values and send
        // their end ticks once (clients count down themselves).
        private void PollControl()
        {
            CombatEntity.ControlTimers t = entity.GetControlTimers();
            if (timersSent && t.SameAs(lastTimers))
                return;
            lastTimers = t;
            timersSent = true;
            float now = Time.time;
            uint tick = TimeManager.Tick;
            double dt = TimeManager.TickDelta;
            NetworkObject taunter = t.tauntSource != null && t.tauntUntil > now ? NetObjects.Of(t.tauntSource) : null;
            control.Value = new ControlState
            {
                stunEnd = StatusReplication.EndTick(t.stunUntil, now, tick, dt),
                silenceEnd = StatusReplication.EndTick(t.silenceUntil, now, tick, dt),
                rootEnd = StatusReplication.EndTick(t.rootUntil, now, tick, dt),
                slowEnd = StatusReplication.EndTick(t.slowUntil, now, tick, dt),
                tauntEnd = StatusReplication.EndTick(t.tauntUntil, now, tick, dt),
                invulnerableEnd = StatusReplication.EndTick(t.invulnerableUntil, now, tick, dt),
                slowFraction = t.slowUntil > now ? t.slowFraction : 0f,
                tauntSource = NetObjects.IsSpawned(taunter) ? taunter : null,
            };
        }

        private void RebuildBuffs()
        {
            if (!IsServerInitialized || timed == null)
                return;
            float now = Time.time;
            uint tick = TimeManager.Tick;
            double dt = TimeManager.TickDelta;
            IReadOnlyList<TimedStatModifiers.Active> all = timed.All;
            buffs.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                TimedStatModifiers.Active a = all[i];
                if (a == null || a.until <= now)
                    continue;
                buffs.Add(new BuffView
                {
                    name = a.name ?? string.Empty,
                    debuff = a.debuff,
                    element = (byte)a.element,
                    duration = a.duration,
                    endTick = StatusReplication.EndTick(a.until, now, tick, dt),
                });
            }
        }

        private void RebuildTraits()
        {
            if (!IsServerInitialized || traitManager == null)
                return;
            traitNames.Clear();
            foreach (Trait t in traitManager.ActiveTraits)
                if (t != null && !string.IsNullOrEmpty(t.Name))
                    traitNames.Add(t.Name);
            if (StatusReplication.SameSequence(traits.Collection, traitNames))
                return;
            traits.Clear();
            for (int i = 0; i < traitNames.Count; i++)
                traits.Add(traitNames[i]);
        }

        private void OnServerHit(DamageInfo info)
        {
            if (!replicateHits || !IsServerInitialized)
                return;
            NetworkObject src = info.source != null ? NetObjects.Of(info.source) : null;
            ObserversHit(new HitEvent
            {
                amount = float.IsNaN(info.amount) || float.IsInfinity(info.amount) ? 0f : Mathf.Max(0f, info.amount),
                element = (byte)info.element,
                damageType = (byte)info.type,
                delivery = (byte)info.delivery,
                flags = StatusReplication.PackHit(info),
                source = NetObjects.IsSpawned(src) ? src : null,
                point = info.point,
                direction = info.direction,
            });
        }

        private void OnServerHeal(float amount)
        {
            if (!replicateHits || !IsServerInitialized || !(amount > 0f) || float.IsInfinity(amount))
                return;
            ObserversHeal(amount);
        }

        // ------------------------------------------------------------------ respawn (server)

        private void OnServerDied(CombatEntity killer)
        {
            if (!respawnPlayers || playerStatus == null || !IsServerInitialized)
                return;
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private void OnServerRevived()
        {
            // Revived by something else (a rest point, a script): no respawn is pending any more.
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            respawnTick.Value = 0;
        }

        private IEnumerator RespawnRoutine()
        {
            float delay = Mathf.Max(0f, respawnDelay);
            respawnTick.Value = StatusReplication.EndTick(Time.time + delay, Time.time, TimeManager.Tick, TimeManager.TickDelta);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Inside a dungeon run the fallen wait for a rest point; when the whole group is down the run ends.
            float leaveDeadline = -1f;
            while (true)
            {
                if (entity == null || !IsServerInitialized || !entity.IsDead)
                {
                    respawnRoutine = null;
                    yield break;
                }
                GameObject mover = PlayerLocator.MovableRoot(entity);
                DungeonRun run = mover != null ? DungeonSession.RunOf(mover) : null;
                if (run == null || run.State == DungeonRunState.Ended)
                    break;
                if (leaveDeadline < 0f && endDungeonOnWipe && !AnyParticipantAlive(run))
                {
                    DungeonSession.Exit(run, false);
                    leaveDeadline = Time.time + 20f;
                }
                if (leaveDeadline >= 0f && Time.time > leaveDeadline)
                    break; // the run did not let them go: respawn anyway
                respawnTick.Value = 0;
                yield return new WaitForSeconds(0.5f);
            }

            respawnRoutine = null;
            respawnTick.Value = 0;
            RespawnNow();
        }

        private static bool AnyParticipantAlive(DungeonRun run)
        {
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
            {
                CombatEntity p = players[i];
                if (p != null && !p.IsDead && run.IsParticipant(PlayerLocator.MovableRoot(p)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Server: revives this player now at the City spawn point (through the hub world when it runs), with
        /// <see cref="respawnHealthFraction"/> of its health and <see cref="spawnProtection"/> seconds of invulnerability.
        /// </summary>
        public void RespawnNow()
        {
            Resolve();
            if (entity == null || !IsServerInitialized)
                return;
            HubWorldDirector director = HubWorldDirector.Instance;
            PlayerRealmState state = director != null ? PlayerRealmState.Of(entity) : null;
            if (state != null)
            {
                director.TravelToCity(state, respawnPenalties, true);
            }
            else
            {
                RealmScene city = RealmScene.Find(RealmKind.City);
                bool local = NetAuthority.IsLocallyControlled(entity.gameObject);
                if (city != null)
                    RealmTravel.Arrive(entity, city.Scene, city.SpawnPose(0), local);
                else if (hasSpawnPose)
                    RealmTravel.Place(entity, spawnPose);
            }
            entity.Revive(respawnHealthFraction);
            if (spawnProtection > 0f)
                entity.SetInvulnerable(spawnProtection);
            if (traitManager != null)
                RebuildTraits();
        }

        // ------------------------------------------------------------------ requests (owner -> server)

        /// <summary>The owner asks to spend a stat upgrade point; the server checks the name, its points and the rate.</summary>
        [ServerRpc]
        private void ServerUpgradeStat(string stat)
        {
            if (xpManager == null || entity == null || entity.IsDead)
                return;
            if (!StatusReplication.Allow(ref lastUpgradeRequest, Time.unscaledTime, requestInterval))
                return;
            string valid = StatusReplication.ValidStatName(stat, xpManager.AvailableStats);
            if (valid != null)
                xpManager.UpgradeStat(valid);
        }

        /// <summary>The owner asks to fall asleep (true) or wake up (false); the server checks it is alive and not stunned.</summary>
        [ServerRpc]
        private void ServerSetSleeping(bool sleep)
        {
            if (playerStatus == null || entity == null || playerStatus.SleepManager == null)
                return;
            if (!StatusReplication.Allow(ref lastSleepRequest, Time.unscaledTime, requestInterval))
                return;
            if (sleep && (entity.IsDead || entity.IsStunned || entity.IsUnconscious))
                return;
            if (sleep)
                playerStatus.SleepManager.StartSleeping();
            else
                playerStatus.SleepManager.StopSleeping();
        }

        private bool SendUpgrade(string stat)
        {
            if (!IsClientInitialized || !IsOwner || string.IsNullOrEmpty(stat) || stat.Length > StatusReplication.MaxNameLength)
                return false;
            ServerUpgradeStat(stat);
            return true;
        }

        private void SendSleep(bool sleep)
        {
            if (IsClientInitialized && IsOwner)
                ServerSetSleeping(sleep);
        }

        /// <summary>The unconscious owner gives up (dies at once); the server checks it is really down and the rules allow it.</summary>
        [ServerRpc]
        private void ServerGiveUp()
        {
            if (entity == null || entity.Downed == null || !entity.Downed.IsDown || !entity.Downed.Rules.allowGiveUp)
                return;
            if (!StatusReplication.Allow(ref lastGiveUpRequest, Time.unscaledTime, requestInterval))
                return;
            entity.Downed.GiveUp();
        }

        private void SendGiveUp()
        {
            if (IsClientInitialized && IsOwner)
                ServerGiveUp();
        }

        // ------------------------------------------------------------------ client

        public override void OnStartClient()
        {
            base.OnStartClient();
            Resolve();
            if (entity == null || IsServerInitialized)
                return; // the host simulates its characters itself
            SetReplica(true);
            HookClient(true);
            ApplyAll();
            reapplyPending = true;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            HookClient(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            // Objects that outlive the session (scene objects) simulate on this machine again. An inactive object cannot
            // restart its routines now: it does so when it is enabled again.
            if (replicaSet)
            {
                if (gameObject.activeInHierarchy)
                    SetReplica(false);
                else
                    revertWhenEnabled = true;
            }
            Unregister();
        }

        private void OnEnable()
        {
            if (!revertWhenEnabled || !replicaSet || IsClientInitialized)
                return;
            revertWhenEnabled = false;
            SetReplica(false);
        }

        private void SetReplica(bool on)
        {
            replicaSet = on;
            if (on)
                revertWhenEnabled = false;
            entity.SetReplica(on);
            if (playerStatus != null)
            {
                foreach (StatusManager m in PlayerManagers())
                    m.SetReplica(on);
                // Stamina is predicted by the movement module on every client copy: only its limits come from the server.
                if (playerStatus.StaminaManager != null)
                    playerStatus.StaminaManager.SetReplicatedLimits(on);
                if (traitManager != null)
                    traitManager.IsMirror = on;
                if (xpManager != null)
                    xpManager.UpgradeRequest = on && IsOwner ? (Func<string, bool>)SendUpgrade : null;
                playerStatus.SleepRequest = on && IsOwner ? (Action<bool>)SendSleep : null;
            }
            if (entity.Downed != null)
                entity.Downed.GiveUpRequest = on && IsOwner ? (Action)SendGiveUp : null;
            else if (healthManager != null)
            {
                healthManager.SetReplica(on);
            }
            if (speedManager != null)
                speedManager.SetReplica(on);
        }

        // Every manager of a player except stamina (see SetReplica).
        private IEnumerable<StatusManager> PlayerManagers()
        {
            StatusManager[] all =
            {
                playerStatus.HpManager, playerStatus.ManaManager, playerStatus.HungerManager, playerStatus.ThirstManager,
                playerStatus.SleepManager, playerStatus.SanityManager, playerStatus.BodyHeatManager, playerStatus.OxygenManager,
                playerStatus.WeightManager,
            };
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null)
                    yield return all[i];
        }

        private void HookClient(bool on)
        {
            if (on == clientHooked)
                return;
            clientHooked = on;
            if (on)
            {
                health.OnChange += OnHealthChanged;
                dead.OnChange += OnDeadChanged;
                control.OnChange += OnControlChanged;
                level.OnChange += OnLevelChanged;
                speed.OnChange += OnSpeedChanged;
                weight.OnChange += OnWeightChanged;
                staminaLimits.OnChange += OnStaminaLimitsChanged;
                buffs.OnChange += OnBuffsChanged;
                traits.OnChange += OnTraitsChanged;
                mana.OnChange += OnOwnerResourceChanged;
                hunger.OnChange += OnOwnerResourceChanged;
                thirst.OnChange += OnOwnerResourceChanged;
                sleepiness.OnChange += OnOwnerResourceChanged;
                sanity.OnChange += OnOwnerResourceChanged;
                bodyHeat.OnChange += OnOwnerResourceChanged;
                oxygen.OnChange += OnOwnerResourceChanged;
                survival.OnChange += OnSurvivalChanged;
                progress.OnChange += OnProgressChanged;
                downed.OnChange += OnDownedChanged;
            }
            else
            {
                health.OnChange -= OnHealthChanged;
                dead.OnChange -= OnDeadChanged;
                control.OnChange -= OnControlChanged;
                level.OnChange -= OnLevelChanged;
                speed.OnChange -= OnSpeedChanged;
                weight.OnChange -= OnWeightChanged;
                staminaLimits.OnChange -= OnStaminaLimitsChanged;
                buffs.OnChange -= OnBuffsChanged;
                traits.OnChange -= OnTraitsChanged;
                mana.OnChange -= OnOwnerResourceChanged;
                hunger.OnChange -= OnOwnerResourceChanged;
                thirst.OnChange -= OnOwnerResourceChanged;
                sleepiness.OnChange -= OnOwnerResourceChanged;
                sanity.OnChange -= OnOwnerResourceChanged;
                bodyHeat.OnChange -= OnOwnerResourceChanged;
                oxygen.OnChange -= OnOwnerResourceChanged;
                survival.OnChange -= OnSurvivalChanged;
                progress.OnChange -= OnProgressChanged;
                downed.OnChange -= OnDownedChanged;
            }
        }

        // Change handlers: only a client-only copy applies (the host's own values are the server's, asServer is ignored).
        private bool Applies(bool asServer) => !asServer && replicaSet && !IsServerInitialized;

        private void OnHealthChanged(Resource prev, Resource next, bool asServer) { if (Applies(asServer)) ApplyHealth(next); }
        private void OnDeadChanged(bool prev, bool next, bool asServer) { if (Applies(asServer)) entity.SetReplicatedDeath(next); }
        private void OnControlChanged(ControlState prev, ControlState next, bool asServer) { if (Applies(asServer)) ApplyControl(next); }
        private void OnLevelChanged(int prev, int next, bool asServer) { if (Applies(asServer)) ApplyProgress(); }
        private void OnSpeedChanged(SpeedValues prev, SpeedValues next, bool asServer) { if (Applies(asServer)) ApplySpeed(next); }
        private void OnWeightChanged(Resource prev, Resource next, bool asServer) { if (Applies(asServer)) ApplyWeight(next); }
        private void OnStaminaLimitsChanged(StaminaLimits prev, StaminaLimits next, bool asServer) { if (Applies(asServer)) ApplyStaminaLimits(next); }
        private void OnOwnerResourceChanged(Resource prev, Resource next, bool asServer) { if (Applies(asServer)) ApplyOwnerResources(); }
        private void OnSurvivalChanged(SurvivalState prev, SurvivalState next, bool asServer) { if (Applies(asServer)) ApplySurvival(next); }
        private void OnProgressChanged(Progress prev, Progress next, bool asServer) { if (Applies(asServer)) ApplyProgress(); }
        private void OnDownedChanged(DownedState prev, DownedState next, bool asServer) { if (Applies(asServer)) ApplyDowned(next); }

        private void ApplyDowned(DownedState d)
        {
            if (entity == null || TimeManager == null)
                return;
            // Other players' copies: the sleeping pose (the owner gets it from its survival state).
            if (!IsOwner && playerStatus != null && playerStatus.SleepManager != null)
                playerStatus.SleepManager.ApplyReplicatedState(d.sleeping, false);
            if (entity.Downed == null)
                return;
            float left = d.endTick == 0u ? float.PositiveInfinity : StatusReplication.SecondsLeft(d.endTick, TimeManager.Tick, TimeManager.TickDelta);
            entity.Downed.ApplyReplicated(d.down, left, d.deficit, null);
        }

        private void OnBuffsChanged(SyncListOperation op, int index, BuffView oldItem, BuffView newItem, bool asServer)
        {
            if (!Applies(asServer))
                return;
            buffsDirty = true;
            if (op == SyncListOperation.Complete)
                ApplyBuffs();
        }

        private void OnTraitsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (!Applies(asServer))
                return;
            traitsDirty = true;
            if (op == SyncListOperation.Complete)
                ApplyTraits();
        }

        private void Update()
        {
            if (!replicaSet)
                return;
            if (reapplyPending)
            {
                // Once more after the first frame: covers parts whose Start ran after the copy became a replica.
                reapplyPending = false;
                ApplyAll();
                return;
            }
            if (buffsDirty)
                ApplyBuffs();
            if (traitsDirty)
                ApplyTraits();
        }

        private void ApplyAll()
        {
            if (!replicaSet || entity == null)
                return;
            ApplyHealth(health.Value);
            entity.SetReplicatedDeath(dead.Value);
            ApplyControl(control.Value);
            ApplySpeed(speed.Value);
            ApplyWeight(weight.Value);
            ApplyStaminaLimits(staminaLimits.Value);
            ApplyOwnerResources();
            ApplySurvival(survival.Value);
            ApplyProgress();
            ApplyBuffs();
            ApplyTraits();
            ApplyDowned(downed.Value);
        }

        private void ApplyHealth(Resource r)
        {
            // Nothing arrived yet (max 0): keep the prefab's values rather than showing an empty bar.
            if (healthManager != null && r.max > 0f)
                healthManager.ApplyReplicated(r.current, r.max);
        }

        private void ApplyControl(ControlState c)
        {
            if (entity == null || TimeManager == null)
                return;
            float now = Time.time;
            uint tick = TimeManager.Tick;
            double dt = TimeManager.TickDelta;
            CombatEntity taunter = c.tauntSource != null ? CombatEntity.Resolve(c.tauntSource.gameObject) : null;
            entity.SetReplicatedControl(new CombatEntity.ControlTimers
            {
                stunUntil = StatusReplication.LocalUntil(c.stunEnd, tick, dt, now),
                silenceUntil = StatusReplication.LocalUntil(c.silenceEnd, tick, dt, now),
                rootUntil = StatusReplication.LocalUntil(c.rootEnd, tick, dt, now),
                slowUntil = StatusReplication.LocalUntil(c.slowEnd, tick, dt, now),
                tauntUntil = StatusReplication.LocalUntil(c.tauntEnd, tick, dt, now),
                invulnerableUntil = StatusReplication.LocalUntil(c.invulnerableEnd, tick, dt, now),
                slowFraction = c.slowFraction,
                tauntSource = taunter,
            });
        }

        private void ApplySpeed(SpeedValues s)
        {
            if (speedManager != null && playerStatus != null && (s.baseSpeed > 0f || s.current > 0f))
                speedManager.ApplyReplicated(s.current, s.baseSpeed, s.runMultiplier, s.crouchMultiplier);
        }

        private void ApplyWeight(Resource r)
        {
            WeightManager w = playerStatus != null ? playerStatus.WeightManager : null;
            if (w != null && r.max > 0f)
                w.ApplyReplicated(r.current, r.max);
        }

        private void ApplyStaminaLimits(StaminaLimits l)
        {
            StaminaManager st = playerStatus != null ? playerStatus.StaminaManager : null;
            if (st != null && l.max > 0f)
                st.ApplyReplicatedLimits(l.max, l.increment, l.gainFactor, l.lossFactor);
        }

        // Owner-only values never reach other clients (they stay at their defaults there), so only the owner applies them.
        private void ApplyOwnerResources()
        {
            if (playerStatus == null || !IsOwner)
                return;
            ApplyResource(playerStatus.ManaManager, mana.Value);
            ApplyResource(playerStatus.HungerManager, hunger.Value);
            ApplyResource(playerStatus.ThirstManager, thirst.Value);
            ApplyResource(playerStatus.SleepManager, sleepiness.Value);
            ApplyResource(playerStatus.SanityManager, sanity.Value);
            ApplyResource(playerStatus.BodyHeatManager, bodyHeat.Value);
            ApplyResource(playerStatus.OxygenManager, oxygen.Value);
        }

        private static void ApplyResource(StatusManager m, Resource r)
        {
            if (m != null && r.max > 0f)
                m.ApplyReplicated(r.current, r.max);
        }

        private void ApplySurvival(SurvivalState s)
        {
            if (playerStatus == null || !IsOwner)
                return;
            byte f = s.flags;
            if (playerStatus.SleepManager != null)
                playerStatus.SleepManager.ApplyReplicatedState(
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.Sleeping),
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.Collapsed));
            if (playerStatus.OxygenManager != null)
                playerStatus.OxygenManager.ApplyReplicatedEnvironment(
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.Underwater),
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.HighAltitude),
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.PoorVentilation),
                    StatusReplication.Has(f, StatusReplication.SurvivalFlags.HasOxygenTank),
                    s.oxygenTank);
            if (playerStatus.BodyHeatManager != null)
                playerStatus.BodyHeatManager.ApplyReplicatedEnvironment(s.environmentTemperature);
        }

        private void ApplyProgress()
        {
            if (xpManager == null || level.Value < 1)
                return;
            if (IsOwner)
            {
                Progress p = progress.Value;
                xpManager.ApplyReplicated(level.Value, p.experience, p.experienceToNext, p.skillPoints, p.attributePoints, p.statUpgradePoints);
                if (traitManager != null && p.traitPoints >= 0)
                    traitManager.SetTraitPoints(p.traitPoints);
            }
            else
            {
                // Others only learn the level (nameplates, level-based growth); keep the rest as it is.
                xpManager.ApplyReplicated(level.Value, Mathf.Max(0, xpManager.CurrentExperience), Mathf.Max(0, xpManager.ExperienceToNextLevel),
                    Mathf.Max(0, xpManager.SkillPoints), Mathf.Max(0, xpManager.AttributePoints), Mathf.Max(0, xpManager.StatUpgradePoints));
            }
        }

        private void ApplyBuffs()
        {
            buffsDirty = false;
            if (timed == null || TimeManager == null)
                return;
            uint tick = TimeManager.Tick;
            double dt = TimeManager.TickDelta;
            viewBuffer.Clear();
            List<BuffView> list = buffs.Collection;
            for (int i = 0; i < list.Count; i++)
            {
                BuffView b = list[i];
                viewBuffer.Add(new TimedStatModifiers.View
                {
                    name = b.name,
                    debuff = b.debuff,
                    element = Enum.IsDefined(typeof(ElementType), (int)b.element) ? (ElementType)b.element : ElementType.None,
                    duration = b.duration,
                    remaining = StatusReplication.SecondsLeft(b.endTick, tick, dt),
                });
            }
            timed.SetReplicatedView(viewBuffer);
        }

        // Follows the server's trait list so the movement traits (double jump, wall climb, glide) exist where the movement
        // is predicted or replayed. The traits' stat changes are absorbed by the replica managers.
        private void ApplyTraits()
        {
            traitsDirty = false;
            if (traitManager == null)
                return;
            currentTraitNames.Clear();
            foreach (Trait t in traitManager.ActiveTraits)
                if (t != null)
                    currentTraitNames.Add(t.Name);
            StatusReplication.Diff(currentTraitNames, traits.Collection, traitsToAdd, traitsToRemove);
            for (int i = 0; i < traitsToRemove.Count; i++)
            {
                Trait t = FindOwnedTrait(traitsToRemove[i]);
                // Release every grant (a mirror normally holds one) until the trait is gone.
                for (int guard = 0; t != null && traitManager.GetTraitInfo(t) != null && guard < 16; guard++)
                    traitManager.RemoveTrait(t, true);
            }
            for (int i = 0; i < traitsToAdd.Count; i++)
            {
                string n = traitsToAdd[i];
                Trait t = FindTrait(n);
                if (t == null)
                    continue;
                if (traitManager.GetTraitInfo(t) != null)
                    traitManager.ResumeTrait(t); // owned but suspended here
                else
                    traitManager.AddTrait(t, true);
            }
        }

        private Trait FindOwnedTrait(string traitName)
        {
            foreach (Trait t in traitManager.ActiveTraits)
                if (t != null && t.Name == traitName)
                    return t;
            return null;
        }

        private Trait FindTrait(string traitName)
        {
            if (string.IsNullOrEmpty(traitName) || traitName.Length > StatusReplication.MaxNameLength)
                return null;
            TraitDatabase db = traitManager.Database;
            Trait t = db != null ? db.GetTraitByName(traitName) : null;
            if (t != null)
                return t;
            foreach (Trait s in traitManager.StartingTraits)
                if (s != null && s.Name == traitName)
                    return s;
            PlayerClass pc = playerStatus != null ? playerStatus.CurrentPlayerClass : null;
            if (pc != null)
                foreach (Trait s in pc.GetStartingTraits())
                    if (s != null && s.Name == traitName)
                        return s;
            if (unknownTraits.Contains(traitName))
                return null;
            TraitDatabase global = TraitDatabase.Instance;
            t = global != null ? global.GetTraitByName(traitName) : null;
            if (t == null)
            {
                unknownTraits.Add(traitName);
                Debug.LogWarning($"[{name}] The server gave this player the trait '{traitName}', which is in no trait database here.", this);
            }
            return t;
        }

        // ------------------------------------------------------------------ presentation events (server -> clients)

        /// <summary>A hit on this character (presentation only; the host shows its own hits directly).</summary>
        [ObserversRpc(ExcludeServer = true)]
        private void ObserversHit(HitEvent hit)
        {
            if (!replicaSet || entity == null)
                return;
            var info = new DamageInfo
            {
                amount = float.IsNaN(hit.amount) || float.IsInfinity(hit.amount) ? 0f : Mathf.Max(0f, hit.amount),
                source = hit.source != null ? CombatEntity.Resolve(hit.source.gameObject) : null,
                target = entity,
                element = Enum.IsDefined(typeof(ElementType), (int)hit.element) ? (ElementType)hit.element : ElementType.None,
                type = Enum.IsDefined(typeof(DamageType), (int)hit.damageType) ? (DamageType)hit.damageType : DamageType.Physical,
                delivery = Enum.IsDefined(typeof(DamageDelivery), (int)hit.delivery) ? (DamageDelivery)hit.delivery : DamageDelivery.Unknown,
                point = Finite(hit.point) ? hit.point : entity.transform.position,
                direction = Finite(hit.direction) ? hit.direction : Vector3.zero,
            };
            StatusReplication.UnpackHit(hit.flags, ref info);
            entity.PresentReplicatedDamage(info);
        }

        /// <summary>Healing this character received (presentation only).</summary>
        [ObserversRpc(ExcludeServer = true)]
        private void ObserversHeal(float amount)
        {
            if (replicaSet && entity != null)
                entity.PresentReplicatedHeal(amount);
        }

        private static bool Finite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
#endif
