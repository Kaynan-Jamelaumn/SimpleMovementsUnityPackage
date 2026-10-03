using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Readme;

/// <summary>Crowd-control and displacement kinds a character can be immune to (bosses, constructs...).</summary>
[Flags]
public enum ControlImmunity
{
    None = 0,
    Stun = 1 << 0,
    Silence = 1 << 1,
    Root = 1 << 2,
    Slow = 1 << 3,
    Taunt = 1 << 4,
    Knockback = 1 << 5,
    Pull = 1 << 6,
    AllControl = Stun | Silence | Root | Slow | Taunt,
    AllDisplacement = Knockback | Pull,
    Everything = AllControl | AllDisplacement,
}

/// <summary>Lets a character decide who it is hostile to (implemented by the mob AI).</summary>
public interface ICombatHostility
{
    bool IsHostileTo(CombatEntity other);
}

/// <summary>
/// One fighting character (player, mob, summon): its team, body size, health, crowd-control state, who hurt it and
/// whether it is dead. Every combat system talks to characters through this component, so the player and mobs are
/// handled the same way.
/// </summary>
/// <remarks>
/// It is added automatically at runtime to any object with a status controller the first time combat needs it, so
/// existing prefabs work without changes. Add it yourself to set a team, body size or immunities.
/// </remarks>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class CombatEntity : MonoBehaviour
{
    public enum EntityKind { Player, Mob, Other }

    [Header("Identity")]
    [Tooltip("Team name. Same team = allies. Empty = automatic: 'Player' for players, the mob type for mobs.")]
    [SerializeField] private string teamOverride = "";
    [Tooltip("Faction (Kingdom, Bandits, Wildlife...): decides allies, enemies and neutrals between characters of different " +
             "teams. Empty = the Mob's faction, or none (then the team and AI decide, as before).")]
    [SerializeField] private CombatFaction faction;
    [Tooltip("Party id: characters with the same non-zero id are party members (never harmed by each other unless friendly " +
             "fire is on). Usually set at runtime (CombatParties / your multiplayer code), 0 = no party.")]
    [SerializeField] private int partyId;

    [Header("Body (0 = automatic from the collider / controller / NavMeshAgent)")]
    [Tooltip("Body radius used by hit shapes (metres). 0 = automatic.")]
    [SerializeField, Min(0f)] private float radiusOverride = 0f;
    [Tooltip("Body height used by hit shapes (metres). 0 = automatic.")]
    [SerializeField, Min(0f)] private float heightOverride = 0f;
    [Tooltip("Optional point projectiles and line of sight aim at (chest / head). Empty = 70% of the body height.")]
    [SerializeField] private Transform aimPoint;

    [Header("Resistances")]
    [Tooltip("Crowd control and displacement this character ignores (e.g. bosses immune to stun and knockback).")]
    public ControlImmunity immunities = ControlImmunity.None;
    [Tooltip("Multiplies all damage taken (0.5 = takes half). Separate from the health manager's damage factor.")]
    [Min(0f)] public float damageTakenMultiplier = 1f;
    [Tooltip("Multiplies how long stuns, silences, roots, slows and taunts last on this character (0.7 = 30% shorter). Traits change it at runtime.")]
    [Min(0f)] public float controlDurationMultiplier = 1f;

    // ------------------------------------------------------------------ cached components
    public EntityKind Kind { get; private set; } = EntityKind.Other;
    public BaseStatusController Status { get; private set; }
    public HealthManager Health { get; private set; }
    public SpeedManager Speed { get; private set; }
    public AvailabilityStateMachine Availability { get; private set; }
    public PlayerAnimationModel PlayerAnimation { get; private set; }
    public PlayerMovementModel PlayerMovement { get; private set; }
    public CharacterController Controller { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public Rigidbody Body { get; private set; }
    public Mob Mob { get; private set; }
    public AbilityCaster Caster { get; internal set; }
    public Collider MainCollider { get; private set; }

    /// <summary>Set by the mob AI so relations can ask it who it is hostile to.</summary>
    public ICombatHostility Hostility { get; set; }

    /// <summary>The character that summoned this one (summons stay on their summoner's team).</summary>
    public CombatEntity Summoner { get; set; }

    // ------------------------------------------------------------------ events
    /// <summary>Raised after this character takes damage.</summary>
    public event Action<DamageInfo> Damaged;
    /// <summary>Raised after this character is healed (amount).</summary>
    public event Action<float> Healed;
    /// <summary>Raised once when this character dies (killer may be null).</summary>
    public event Action<CombatEntity> Died;
    /// <summary>Raised when this character is revived / respawned (timed buffs and debuffs are cleared).</summary>
    public event Action Revived;
    /// <summary>
    /// Asked when damage would kill this character, before it dies. A handler that restores some health and returns
    /// true saves it (e.g. the Cheat Death trait). Handlers are asked in order until one saves it.
    /// </summary>
    public event Func<DamageInfo, bool> LethalDamage;
    /// <summary>Raised when crowd control is applied (type, duration, source).</summary>
    public event Action<ControlType, float, CombatEntity> ControlApplied;
    /// <summary>Raised when a hit was stopped completely before reaching health (fully blocked or parried).</summary>
    public event Action<DamageInfo> DamageStopped;

    /// <summary>How many hits were stopped completely (blocks, parries) so far: attackers compare it to know a hit was stopped.</summary>
    public int StoppedHitCount { get; private set; }

    // ------------------------------------------------------------------ state
    private float stunUntil, silenceUntil, rootUntil, slowUntil, tauntUntil, invulnerableUntil;
    private float slowFraction;
    private CombatEntity tauntSource;
    private bool deathRaised;
    private bool healthSeen;
    private float lastKnownHealth;
    private CombatEntity lastAttacker;
    private float lastAttackTime = -999f;
    private CombatEntity lastTarget;
    private float lastTargetTime = -999f;
    private CombatEntity periodicSource;
    private float periodicSourceUntil;
    private Vector3 lastPosition;
    private Vector3 velocity;
    private float nextNoiseTime;
    private float playerSpeedDelta;
    private float radius = 0.5f, height = 2f;
    private string team;

    private struct ThreatEntry
    {
        public CombatEntity entity;
        /// <summary>Threat at <see cref="lastTime"/> (it fades after that, see <see cref="Decayed"/>).</summary>
        public float threat;
        public float lastTime;
    }

    private readonly List<IDamageDealtModifier> damageDealtModifiers = new List<IDamageDealtModifier>(1);

    /// <summary>The character's combat stats (set by <see cref="CombatStats"/> itself), or null.</summary>
    public CombatStats Stats { get; internal set; }

    /// <summary>Raised when threat is added (source, amount added, kind): threat meters, network sync.</summary>
    public event Action<CombatEntity, float, ThreatKind> ThreatAdded;

    private readonly List<ThreatEntry> threats = new List<ThreatEntry>(4);
    private readonly List<IDamageTakenModifier> damageTakenModifiers = new List<IDamageTakenModifier>(2);
    private readonly List<IDamageInterceptor> damageInterceptors = new List<IDamageInterceptor>(2);
    private readonly List<IDisplacementModifier> displacementModifiers = new List<IDisplacementModifier>(1);

    // ------------------------------------------------------------------ registry
    private static readonly List<CombatEntity> all = new List<CombatEntity>(64);
    private static readonly List<CombatEntity> players = new List<CombatEntity>(4);
    private static readonly Dictionary<Collider, CombatEntity> byCollider = new Dictionary<Collider, CombatEntity>(256, ReferenceComparer<Collider>.Instance);

    /// <summary>Every active combat entity.</summary>
    public static IReadOnlyList<CombatEntity> All => all;

    /// <summary>Every active player entity.</summary>
    public static IReadOnlyList<CombatEntity> Players => players;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        players.Clear();
        byCollider.Clear();
    }

    /// <summary>Forgets cached collider lookups (call after large scene changes).</summary>
    public static void ClearLookupCache() => byCollider.Clear();

    /// <summary>
    /// The combat entity a collider belongs to, or null for scenery. Adds the component to characters that have a
    /// status controller but no CombatEntity yet. Results are cached per collider.
    /// </summary>
    public static CombatEntity Resolve(Collider collider)
    {
        if (collider == null)
            return null;
        Collider id = collider;
        if (byCollider.TryGetValue(id, out CombatEntity cached))
        {
            if (cached != null || ReferenceEquals(cached, null))
                return cached;
            byCollider.Remove(id); // destroyed entity
        }

        CombatEntity entity = collider.GetComponentInParent<CombatEntity>();
        if (entity == null)
        {
            BaseStatusController status = collider.GetComponentInParent<BaseStatusController>();
            if (status != null)
                entity = GetOrAdd(status.gameObject);
        }
        byCollider[id] = entity;
        return entity;
    }

    /// <summary>The combat entity of a GameObject (adds it to characters with a status controller).</summary>
    public static CombatEntity Resolve(GameObject go)
    {
        if (go == null)
            return null;
        CombatEntity entity = go.GetComponentInParent<CombatEntity>();
        if (entity != null)
            return entity;
        BaseStatusController status = go.GetComponentInParent<BaseStatusController>();
        if (status == null)
            status = go.GetComponentInChildren<BaseStatusController>();
        if (status == null)
            return null;
        return GetOrAdd(status.gameObject);
    }

    /// <summary>The CombatEntity on exactly this GameObject, added if missing.</summary>
    public static CombatEntity GetOrAdd(GameObject go)
    {
        CombatEntity e = go.GetComponent<CombatEntity>();
        if (e == null)
            e = go.AddComponent<CombatEntity>();
        return e;
    }

    // Unity overloads == for destroyed/missing objects, so '??' must not be used with components.
    private static T First<T>(T a, T b) where T : UnityEngine.Object => a != null ? a : b;
    private static T First<T>(T a, T b, T c) where T : UnityEngine.Object => a != null ? a : (b != null ? b : c);

    /// <summary>The nearest living player to a position (null if there are none).</summary>
    public static CombatEntity NearestPlayer(Vector3 position, out float distance)
    {
        CombatEntity best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p.IsDead)
                continue;
            float d = (p.Position - position).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }
        distance = best != null ? Mathf.Sqrt(bestSqr) : float.MaxValue;
        return best;
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        CacheComponents();
        BodyPartController.EnsureFor(this);
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        if (Kind == EntityKind.Player && !players.Contains(this))
            players.Add(this);
        lastPosition = transform.position;
        if (PlayerAnimation != null)
        {
            PlayerAnimation.OnAttackStart -= OnPlayerAttackStart;
            PlayerAnimation.OnAttackStart += OnPlayerAttackStart;
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        players.Remove(this);
        if (PlayerAnimation != null)
            PlayerAnimation.OnAttackStart -= OnPlayerAttackStart;
    }

    private void OnDestroy()
    {
        if (MainCollider != null)
            byCollider.Remove(MainCollider);
    }

    /// <summary>Finds the status controller, health, movement and AI components of this character.</summary>
    public void CacheComponents()
    {
        Status = First(GetComponent<BaseStatusController>(), GetComponentInParent<BaseStatusController>(), GetComponentInChildren<BaseStatusController>());
        Mob = First(GetComponent<Mob>(), GetComponentInParent<Mob>());

        if (Status is PlayerStatusController player)
        {
            Kind = EntityKind.Player;
            Health = player.HpManager;
            Speed = player.SpeedManager;
            PlayerMovement = player.MovementModel;
        }
        else if (Status is MobStatusController mob)
        {
            Kind = EntityKind.Mob;
            Health = mob.HealthManager;
            Speed = mob.SpeedManager;
        }
        else
        {
            Kind = Mob != null ? EntityKind.Mob : EntityKind.Other;
            Health = GetComponentInChildren<HealthManager>();
            Speed = GetComponentInChildren<SpeedManager>();
        }

        if (Health == null)
            Health = GetComponentInChildren<HealthManager>();
        if (Speed == null)
            Speed = GetComponentInChildren<SpeedManager>();

        if (Kind == EntityKind.Player)
        {
            Availability = First(GetComponent<AvailabilityStateMachine>(), GetComponentInChildren<AvailabilityStateMachine>(), GetComponentInParent<AvailabilityStateMachine>());
            PlayerAnimation = First(GetComponent<PlayerAnimationModel>(), GetComponentInChildren<PlayerAnimationModel>());
            if (PlayerMovement == null)
                PlayerMovement = GetComponentInChildren<PlayerMovementModel>();
        }

        Controller = First(GetComponent<CharacterController>(), GetComponentInChildren<CharacterController>());
        Agent = GetComponent<NavMeshAgent>();
        Body = GetComponent<Rigidbody>();
        if (Caster == null)
            Caster = GetComponent<AbilityCaster>();

        MainCollider = FindMainCollider();
        MeasureBody();
        team = null;
    }

    private Collider FindMainCollider()
    {
        if (Controller != null)
            return Controller;
        Collider best = null;
        float bestVolume = -1f;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled)
                continue;
            Vector3 s = c.bounds.size;
            float v = s.x * s.y * s.z;
            if (v > bestVolume)
            {
                bestVolume = v;
                best = c;
            }
        }
        return best;
    }

    /// <summary>Measures the body again after its size changed (Character Body height).</summary>
    public void RemeasureBody() => MeasureBody();

    private void MeasureBody()
    {
        float r = 0.5f, h = 2f;
        if (Controller != null)
        {
            Vector3 s = Controller.transform.lossyScale;
            r = Controller.radius * Mathf.Max(s.x, s.z);
            h = Controller.height * s.y;
        }
        else if (MainCollider is CapsuleCollider capsule)
        {
            Vector3 s = capsule.transform.lossyScale;
            r = capsule.radius * Mathf.Max(s.x, s.z);
            h = capsule.height * s.y;
        }
        else if (Agent != null)
        {
            r = Agent.radius;
            h = Agent.height;
        }
        else if (MainCollider != null)
        {
            Vector3 e = MainCollider.bounds.extents;
            r = Mathf.Max(e.x, e.z);
            h = e.y * 2f;
        }
        radius = radiusOverride > 0f ? radiusOverride : Mathf.Clamp(r, 0.1f, 20f);
        height = heightOverride > 0f ? heightOverride : Mathf.Clamp(h, 0.2f, 40f);
    }

    // ------------------------------------------------------------------ body
    public float Radius => radius;
    public float Height => height;

    /// <summary>Feet position (bottom centre of the body).</summary>
    public Vector3 BasePosition
    {
        get
        {
            if (MainCollider != null && MainCollider.enabled)
            {
                Bounds b = MainCollider.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
            return transform.position;
        }
    }

    public Vector3 Position => transform.position;
    public Vector3 Center => BasePosition + Vector3.up * (height * 0.5f);

    /// <summary>
    /// The point of this character's body (an upright capsule) facing <paramref name="from"/>, at <paramref name="from"/>'s
    /// height (kept within the body): where a blow or a shot coming from there lands.
    /// </summary>
    public Vector3 SurfacePointToward(Vector3 from)
    {
        Vector3 b = BasePosition;
        float y = Mathf.Clamp(from.y, b.y + 0.02f, b.y + Mathf.Max(0.05f, height) - 0.02f);
        var axis = new Vector3(b.x, y, b.z);
        Vector3 flat = from - axis;
        flat.y = 0f;
        return flat.sqrMagnitude > 1e-6f ? axis + flat.normalized * radius : axis;
    }
    public Vector3 AimPosition => aimPoint != null ? aimPoint.position : BasePosition + Vector3.up * (height * 0.7f);
    public Vector3 Forward => transform.forward;
    public TargetVolume Volume => new TargetVolume(BasePosition, radius, height);

    /// <summary>Smoothed world velocity (metres per second).</summary>
    public Vector3 Velocity
    {
        get
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                return Agent.velocity;
            if (Controller != null && Controller.enabled)
                return Controller.velocity;
            return velocity;
        }
    }

    // ------------------------------------------------------------------ team
    public string Team
    {
        get
        {
            if (!string.IsNullOrEmpty(teamOverride))
                return teamOverride;
            if (Summoner != null)
                return Summoner.Team;
            if (team == null)
            {
                if (Kind == EntityKind.Player)
                    team = "Player";
                else if (Mob != null)
                    team = "Mob:" + Mob.CombatTeam;
                else
                    team = "Other:" + name;
            }
            return team;
        }
    }

    /// <summary>Overrides the team at runtime (summons, charmed mobs). Empty restores the automatic team.</summary>
    public void SetTeam(string newTeam)
    {
        teamOverride = newTeam ?? "";
        team = null;
    }

    /// <summary>The faction: this entity's, else its Mob's, else its summoner's (null = none).</summary>
    public CombatFaction Faction
    {
        get
        {
            if (faction != null) return faction;
            if (Mob != null && Mob.Faction != null) return Mob.Faction;
            return Summoner != null ? Summoner.Faction : null;
        }
    }

    /// <summary>Changes the faction at runtime (charm, disguise, joining a guild). Null = back to the automatic one.</summary>
    public void SetFaction(CombatFaction newFaction) => faction = newFaction;

    /// <summary>Party id (0 = none). Summons belong to their summoner's party.</summary>
    public int PartyId => partyId != 0 ? partyId : Summoner != null ? Summoner.PartyId : 0;

    /// <summary>Joins party <paramref name="id"/> (0 = leaves the party).</summary>
    public void SetParty(int id) => partyId = Mathf.Max(0, id);

    // ------------------------------------------------------------------ health
    public bool IsDead
    {
        get
        {
            if (deathRaised)
                return true;
            if (Kind == EntityKind.Player && Availability != null && Availability.CurrentState != null &&
                Availability.CurrentState.StateKey == EAvailabilityState.Death)
                return true;
            return healthSeen && Health != null && Health.CurrentValue <= 0f;
        }
    }

    public bool IsAlive => !IsDead && isActiveAndEnabled;

    public float HealthRatio => Health != null && Health.MaxValue > 0f ? Mathf.Clamp01(Health.CurrentValue / Health.MaxValue) : 1f;
    public float MaxHealth => Health != null ? Health.MaxValue : 0f;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    public void SetInvulnerable(float duration) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);

    /// <summary>Ends any invulnerability immediately.</summary>
    public void ClearInvulnerable() => invulnerableUntil = 0f;

    /// <summary>The character this one damaged most recently (summons use their summoner's to assist it).</summary>
    public CombatEntity LastTarget => lastTarget != null && lastTarget.IsAlive && Time.time - lastTargetTime < 12f ? lastTarget : null;

    /// <summary>Records that this character damaged <paramref name="victim"/>.</summary>
    public void NoteAttacked(CombatEntity victim)
    {
        if (victim == null || victim == this)
            return;
        lastTarget = victim;
        lastTargetTime = Time.time;
    }

    /// <summary>The last character that hurt this one within <paramref name="window"/> seconds.</summary>
    public CombatEntity RecentAttacker(float window = 10f)
    {
        if (lastAttacker == null || Time.time - lastAttackTime > window)
            return null;
        return lastAttacker;
    }

    public float LastDamagedTime => lastAttackTime;

    /// <summary>Time.time when this character last damaged someone (-999 = never).</summary>
    public float LastDealtDamageTime => lastTargetTime;

    /// <summary>Asks the <see cref="LethalDamage"/> handlers to save this character. True if one did (health is above 0 again).</summary>
    private bool TrySurviveLethal(DamageInfo info)
    {
        Func<DamageInfo, bool> handlers = LethalDamage;
        if (handlers == null || Health == null)
            return false;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try
            {
                if (((Func<DamageInfo, bool>)d)(info) && Health.CurrentValue > 0f)
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
        return false;
    }

    /// <summary>
    /// Deals damage (reduced by <see cref="damageTakenMultiplier"/>, the registered damage-taken modifiers - armor,
    /// resistances - and the health manager's damage factor). Returns the health actually removed.
    /// </summary>
    public float ApplyDamage(DamageInfo info)
    {
        if (IsDead || IsInvulnerable || info.amount <= 0f)
            return 0f;

        info.target = this;
        // The attacker's outgoing modifiers first (elemental damage...), then this character's.
        if (info.source != null && info.source != this && info.source.damageDealtModifiers.Count > 0)
            info.amount = info.source.ApplyDamageDealtModifiers(info);
        info.amount *= damageTakenMultiplier;
        if (damageInterceptors.Count > 0 && !RunInterceptors(ref info))
        {
            // Stopped before reaching health (a full block, a parry): the attacker is remembered (aggro), nothing else.
            StoppedHitCount++;
            RecordAttacker(info.source, 0f);
            DamageStopped?.Invoke(info);
            return 0f;
        }
        if (damageTakenModifiers.Count > 0)
            info.amount = ApplyDamageTakenModifiers(info);
        if (info.amount <= 0f)
        {
            // Fully resisted: the attacker is still remembered (aggro), but nothing else happens.
            RecordAttacker(info.source, 0f);
            return 0f;
        }
        float removed = info.amount;

        if (Health != null)
        {
            float before = Health.CurrentValue;
            Health.ConsumeHP(info.amount);
            removed = Mathf.Max(0f, before - Health.CurrentValue);
            if (Health.CurrentValue <= 0f && !deathRaised)
            {
                DamageInfo lethal = info;
                lethal.amount = removed;
                TrySurviveLethal(lethal);
            }
            lastKnownHealth = Health.CurrentValue;
            healthSeen = true;
        }
        else if (Mob != null)
        {
            Mob.ReceiveDamage(Mathf.CeilToInt(info.amount));
        }

        info.amount = removed;
        RecordAttacker(info.source, removed, info.threatMultiplier ?? 1f);
        Damaged?.Invoke(info);
        CombatEvents.RaiseDamaged(info);

        if (Health != null && Health.CurrentValue <= 0f)
            HandleDeath(info.source);
        return removed;
    }

    /// <summary>
    /// Adds something that scales the damage this character takes (armor, resistances...). Adding the same modifier
    /// twice has no effect. Remove it with <see cref="RemoveDamageTakenModifier"/> when it no longer applies.
    /// </summary>
    public void AddDamageTakenModifier(IDamageTakenModifier modifier)
    {
        if (modifier != null && !damageTakenModifiers.Contains(modifier))
            damageTakenModifiers.Add(modifier);
    }

    public void RemoveDamageTakenModifier(IDamageTakenModifier modifier) => damageTakenModifiers.Remove(modifier);

    /// <summary>Adds something that scales the damage this character DEALS (elemental damage bonuses...).</summary>
    public void AddDamageDealtModifier(IDamageDealtModifier modifier)
    {
        if (modifier != null && !damageDealtModifiers.Contains(modifier))
            damageDealtModifiers.Add(modifier);
    }

    public void RemoveDamageDealtModifier(IDamageDealtModifier modifier) => damageDealtModifiers.Remove(modifier);

    private float ApplyDamageDealtModifiers(in DamageInfo info)
    {
        float amount = info.amount;
        for (int i = 0; i < damageDealtModifiers.Count; i++)
        {
            IDamageDealtModifier m = damageDealtModifiers[i];
            if (m == null || (m is UnityEngine.Object o && o == null))
                continue;
            try { amount = Mathf.Max(0f, m.ModifyDamageDealt(info, amount)); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        return amount;
    }

    /// <summary>
    /// Adds something that sees each hit before armour and resistances (body parts, shield blocks). Adding the same one
    /// twice has no effect; they run in <see cref="IDamageInterceptor.InterceptOrder"/>.
    /// </summary>
    public void AddDamageInterceptor(IDamageInterceptor interceptor)
    {
        if (interceptor == null || damageInterceptors.Contains(interceptor))
            return;
        int i = 0;
        while (i < damageInterceptors.Count && damageInterceptors[i].InterceptOrder <= interceptor.InterceptOrder)
            i++;
        damageInterceptors.Insert(i, interceptor);
    }

    public void RemoveDamageInterceptor(IDamageInterceptor interceptor) => damageInterceptors.Remove(interceptor);

    /// <summary>Adds something that changes the knockbacks and pulls this character receives (a raised shield).</summary>
    public void AddDisplacementModifier(IDisplacementModifier modifier)
    {
        if (modifier != null && !displacementModifiers.Contains(modifier))
            displacementModifiers.Add(modifier);
    }

    public void RemoveDisplacementModifier(IDisplacementModifier modifier) => displacementModifiers.Remove(modifier);

    /// <summary>Runs the interceptors in order. False = the hit was stopped completely.</summary>
    private bool RunInterceptors(ref DamageInfo info)
    {
        for (int i = 0; i < damageInterceptors.Count; i++)
        {
            IDamageInterceptor x = damageInterceptors[i];
            if (x == null || (x is UnityEngine.Object o && o == null))
                continue;
            try
            {
                if (!x.InterceptDamage(ref info))
                    return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            if (info.amount <= 0f)
                return false;
        }
        return true;
    }

    private float ApplyDamageTakenModifiers(in DamageInfo info)
    {
        float amount = info.amount;
        for (int i = 0; i < damageTakenModifiers.Count; i++)
        {
            IDamageTakenModifier m = damageTakenModifiers[i];
            if (m == null)
                continue;
            try
            {
                amount = Mathf.Max(0f, m.ModifyDamageTaken(info, amount));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
        return amount;
    }

    /// <summary>Heals (increased by the health manager's heal factor).</summary>
    public void ApplyHeal(float amount, CombatEntity source = null)
    {
        if (IsDead || amount <= 0f || Health == null)
            return;
        float before = Health.CurrentValue;
        Health.AddCurrentValue(amount);
        lastKnownHealth = Health.CurrentValue;
        Healed?.Invoke(amount);
        float healed = Mathf.Max(0f, Health.CurrentValue - before);
        if (source != null && healed > 0f)
            SpreadHealingThreat(source, healed);
    }

    /// <summary>
    /// Healing makes the healer a threat to every mob already fighting the healed character (shared between them).
    /// </summary>
    private void SpreadHealingThreat(CombatEntity healer, float healed)
    {
        float perPoint = CombatSettings.Instance.threatPerHealing;
        if (perPoint <= 0f)
            return;
        IReadOnlyList<CombatEntity> all = All;
        int count = 0;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i] != healer && all[i] != this && all[i].IsAlive && all[i].GetThreat(this) > 0f)
                count++;
        if (count == 0)
            return;
        float share = healed * perPoint / count;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity mob = all[i];
            if (mob != null && mob != healer && mob != this && mob.IsAlive && mob.GetThreat(this) > 0f)
                mob.AddThreat(healer, share, ThreatKind.Healing);
        }
    }

    /// <summary>
    /// Damage over time applied through the status managers is attributed to <paramref name="source"/> for
    /// <paramref name="duration"/> seconds.
    /// </summary>
    public void RegisterPeriodicSource(CombatEntity source, float duration)
    {
        if (source == null)
            return;
        periodicSource = source;
        periodicSourceUntil = Mathf.Max(periodicSourceUntil, Time.time + duration + 0.5f);
        RecordAttacker(source, 0f);
    }

    private void RecordAttacker(CombatEntity source, float amount, float threatScale = 1f)
    {
        if (source == null || source == this)
            return;
        lastAttacker = source;
        lastAttackTime = Time.time;
        AddThreat(source, Mathf.Max(1f, amount) * CombatSettings.Instance.threatPerDamage * Mathf.Max(0f, threatScale), ThreatKind.Damage);
        source.NoteAttacked(this);
    }

    /// <summary>Marks this character dead (normally automatic when health reaches 0).</summary>
    public void HandleDeath(CombatEntity killer)
    {
        if (deathRaised)
            return;
        deathRaised = true;
        if (killer == null)
            killer = RecentAttacker(15f);
        if (Caster != null) Caster.Interrupt(CastInterruptReason.Death, true);
        Died?.Invoke(killer);
        CombatEvents.RaiseKilled(this, killer);
        if (Kind == EntityKind.Player && Availability != null && CombatSettings.Instance.killPlayersAtZeroHealth)
            Availability.Kill();
    }

    /// <summary>Clears the dead flag (player revive, pooled mob respawn).</summary>
    public void Revive()
    {
        deathRaised = false;
        healthSeen = false;
        threats.Clear();
        lastAttacker = null;
        if (Kind == EntityKind.Player && Availability != null)
            Availability.Revive();
        Revived?.Invoke();
    }

    // ------------------------------------------------------------------ threat (used by mob AI)
    /// <summary>
    /// Adds threat as it is (no multipliers): scripts and older callers. Prefer <see cref="AddThreat(CombatEntity, float, ThreatKind)"/>.
    /// </summary>
    public void AddThreat(CombatEntity source, float amount) => AddThreatRaw(source, amount, ThreatKind.Other);

    /// <summary>
    /// Adds threat from <paramref name="source"/>, scaled by its Threat stat. The first character to cause threat on this
    /// one (by attacking or being noticed) gets the First Contact head start. Threat fades after a while without new
    /// threat (Combat Settings ▸ Threat). Runs on the authority (server / host) in multiplayer; clients only need the
    /// resulting target.
    /// </summary>
    public void AddThreat(CombatEntity source, float amount, ThreatKind kind)
    {
        if (source == null || source == this || amount <= 0f)
            return;
        CombatStats st = source.Stats;
        if (st != null)
            amount *= st.ThreatMultiplier;
        AddThreatRaw(source, amount, kind);
    }

    private void AddThreatRaw(CombatEntity source, float amount, ThreatKind kind)
    {
        if (source == null || source == this)
            return;
        float now = Time.time;
        bool firstEver = true;
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity != null && threats[i].entity.IsAlive && Decayed(threats[i], now) > 0.01f)
            {
                firstEver = false;
                break;
            }
        }
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity == source)
            {
                ThreatEntry t = threats[i];
                t.threat = Decayed(t, now) + amount;
                t.lastTime = now;
                threats[i] = t;
                ThreatAdded?.Invoke(source, amount, kind);
                return;
            }
        }
        float bonus = firstEver ? CombatSettings.Instance.firstContactThreat * Mathf.Max(10f, MaxHealth) : 0f;
        threats.Add(new ThreatEntry { entity = source, threat = amount + bonus, lastTime = now });
        ThreatAdded?.Invoke(source, amount + bonus, kind);
    }

    /// <summary>A taunt: <paramref name="taunter"/> goes above everyone else's threat (and stays there after the taunt).</summary>
    public void TauntThreat(CombatEntity taunter)
    {
        if (taunter == null || taunter == this)
            return;
        float top = 0f;
        float now = Time.time;
        for (int i = 0; i < threats.Count; i++)
            if (threats[i].entity != taunter)
                top = Mathf.Max(top, Decayed(threats[i], now));
        float mine = GetThreat(taunter, float.MaxValue);
        float wanted = Mathf.Max(top * CombatSettings.Instance.tauntThreatMultiplier, 1f);
        if (wanted > mine)
            AddThreatRaw(taunter, wanted - mine, ThreatKind.Taunt);
    }

    /// <summary>The threat of an entry now, after fading.</summary>
    private static float Decayed(in ThreatEntry t, float now)
    {
        CombatSettings cs = CombatSettings.Instance;
        float idle = now - t.lastTime - cs.threatDecayDelay;
        if (idle <= 0f || cs.threatDecayPerSecond <= 0f)
            return t.threat;
        return t.threat * Mathf.Pow(1f - Mathf.Clamp01(cs.threatDecayPerSecond), idle);
    }

    /// <summary>Threat accumulated from <paramref name="source"/> (after fading), 0 when forgotten for <paramref name="memory"/> seconds.</summary>
    public float GetThreat(CombatEntity source, float memory = 20f)
    {
        float now = Time.time;
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity == source)
                return now - threats[i].lastTime > memory ? 0f : Decayed(threats[i], now);
        }
        return 0f;
    }

    /// <summary>The living character that caused the most threat recently, or null.</summary>
    public CombatEntity TopThreat(float memory = 20f)
    {
        CombatEntity best = null;
        float bestThreat = 0f;
        float now = Time.time;
        for (int i = threats.Count - 1; i >= 0; i--)
        {
            ThreatEntry t = threats[i];
            if (t.entity == null || t.entity.IsDead || now - t.lastTime > memory)
            {
                threats.RemoveAt(i);
                continue;
            }
            float v = Decayed(t, now);
            if (v > bestThreat)
            {
                bestThreat = v;
                best = t.entity;
            }
        }
        return best;
    }

    /// <summary>The threat table now (after fading), highest first: threat meters, debugging, network sync.</summary>
    public List<KeyValuePair<CombatEntity, float>> GetThreatTable(List<KeyValuePair<CombatEntity, float>> into = null)
    {
        into = into ?? new List<KeyValuePair<CombatEntity, float>>();
        into.Clear();
        float now = Time.time;
        for (int i = 0; i < threats.Count; i++)
            if (threats[i].entity != null && threats[i].entity.IsAlive)
                into.Add(new KeyValuePair<CombatEntity, float>(threats[i].entity, Decayed(threats[i], now)));
        into.Sort((a, b) => b.Value.CompareTo(a.Value));
        return into;
    }

    /// <summary>Lowers the threat of <paramref name="source"/> (fade, vanish, threat-drop abilities). Never below 0.</summary>
    public void ReduceThreat(CombatEntity source, float amount)
    {
        if (source == null || amount <= 0f)
            return;
        float now = Time.time;
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity != source)
                continue;
            ThreatEntry t = threats[i];
            t.threat = Mathf.Max(0f, Decayed(t, now) - amount);
            t.lastTime = now;
            threats[i] = t;
            return;
        }
    }

    public void ClearThreat() => threats.Clear();

    // ------------------------------------------------------------------ crowd control
    public bool IsStunned
    {
        get
        {
            if (Time.time < stunUntil)
                return true;
            return Kind == EntityKind.Player && Availability != null && Availability.HasStatusEffect(EStatusEffect.Stunned);
        }
    }

    public bool IsSilenced
    {
        get
        {
            if (Time.time < silenceUntil)
                return true;
            return Kind == EntityKind.Player && Availability != null && Availability.HasStatusEffect(EStatusEffect.Silenced);
        }
    }

    public bool IsRooted => Time.time < rootUntil;
    public bool IsSlowed => Time.time < slowUntil && slowFraction > 0f;
    public float StunRemaining => Mathf.Max(0f, stunUntil - Time.time);

    /// <summary>
    /// Movement multiplier imposed by this character's own abilities while casting (set by its
    /// <see cref="AbilityCaster"/>; 1 = free, 0 = must stand still).
    /// </summary>
    public float CastMoveMultiplier { get; set; } = 1f;

    /// <summary>Movement multiplier while guarding (set by the <see cref="BlockController"/>; 1 = free).</summary>
    public float GuardMoveMultiplier { get; set; } = 1f;

    /// <summary>Movement speed multiplier from crowd control, casting and guarding (0 when stunned or rooted).</summary>
    public float MoveSpeedMultiplier
    {
        get
        {
            if (IsStunned || IsRooted)
                return 0f;
            float m = IsSlowed ? 1f - slowFraction : 1f;
            return m * Mathf.Clamp01(CastMoveMultiplier) * Mathf.Clamp01(GuardMoveMultiplier);
        }
    }

    /// <summary>The character forcing this one to attack it (taunt), or null.</summary>
    public CombatEntity TauntedBy => Time.time < tauntUntil && tauntSource != null && tauntSource.IsAlive ? tauntSource : null;

    /// <summary>Can this character use abilities right now?</summary>
    public bool CanCast => !IsDead && !IsStunned && !IsSilenced;

    /// <summary>Can this character move by itself right now?</summary>
    public bool CanMove => !IsDead && !IsStunned && !IsRooted;

    /// <summary>
    /// Applies crowd control. <paramref name="magnitude"/> is the slow fraction (0-0.95) for Slow. Returns false when
    /// immune or dead.
    /// </summary>
    public bool ApplyControl(ControlType type, float duration, CombatEntity source, float magnitude = 0f)
    {
        duration *= controlDurationMultiplier; // traits (Control Duration Taken)
        if (Stats != null)
            duration *= Stats.ControlTakenMultiplier; // Crowd Control Resistance
        if (source != null && source != this && source.Stats != null)
            duration *= source.Stats.ControlDealtMultiplier; // the attacker's Crowd Control Duration
        if (IsDead || duration <= 0f)
            return false;
        float until = Time.time + duration;

        switch (type)
        {
            case ControlType.Stun:
                if ((immunities & ControlImmunity.Stun) != 0) return false;
                stunUntil = Mathf.Max(stunUntil, until);
                if (Kind == EntityKind.Player && Availability != null)
                    Availability.ApplyStun(duration);
                if (Caster != null) Caster.Interrupt(CastInterruptReason.Stun);
                break;
            case ControlType.Silence:
                if ((immunities & ControlImmunity.Silence) != 0) return false;
                silenceUntil = Mathf.Max(silenceUntil, until);
                if (Kind == EntityKind.Player && Availability != null)
                    Availability.ApplySilence(duration);
                if (Caster != null) Caster.Interrupt(CastInterruptReason.Silence);
                break;
            case ControlType.Root:
                if ((immunities & ControlImmunity.Root) != 0) return false;
                rootUntil = Mathf.Max(rootUntil, until);
                break;
            case ControlType.Slow:
                if ((immunities & ControlImmunity.Slow) != 0) return false;
                magnitude = Mathf.Clamp(magnitude, 0f, 0.95f);
                if (!IsSlowed || magnitude >= slowFraction)
                {
                    slowFraction = magnitude;
                    slowUntil = until;
                }
                else
                {
                    slowUntil = Mathf.Max(slowUntil, until);
                }
                break;
            case ControlType.Taunt:
                if ((immunities & ControlImmunity.Taunt) != 0 || source == null) return false;
                tauntSource = source;
                tauntUntil = Mathf.Max(tauntUntil, until);
                break;
        }

        if (source != null && source != this)
        {
            RecordAttacker(source, 0f, 0f);
            if (type == ControlType.Taunt)
                TauntThreat(source);
            else
                AddThreat(source, CombatSettings.Instance.threatPerControl * Mathf.Max(10f, MaxHealth), ThreatKind.Control);
        }
        ControlApplied?.Invoke(type, duration, source);
        return true;
    }

    /// <summary>Removes crowd control (cleanse).</summary>
    public void ClearControl()
    {
        stunUntil = silenceUntil = rootUntil = slowUntil = tauntUntil = 0f;
        slowFraction = 0f;
        tauntSource = null;
        if (Kind == EntityKind.Player && Availability != null)
        {
            Availability.RemoveStatusEffect(EStatusEffect.Stunned);
            Availability.RemoveStatusEffect(EStatusEffect.Silenced);
        }
    }

    /// <summary>
    /// Moves this character by <paramref name="displacement"/> over <paramref name="duration"/> seconds (knockback,
    /// pull), optionally in an arc. Returns false when immune.
    /// </summary>
    public bool ApplyDisplacement(Vector3 displacement, float duration, float arcHeight, bool isPull, CombatEntity source)
    {
        if (IsDead)
            return false;
        if ((immunities & (isPull ? ControlImmunity.Pull : ControlImmunity.Knockback)) != 0)
            return false;
        for (int i = 0; i < displacementModifiers.Count; i++)
        {
            IDisplacementModifier m = displacementModifiers[i];
            if (m == null || (m is UnityEngine.Object o && o == null))
                continue;
            try { displacement = m.ModifyDisplacement(displacement, source, isPull); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        if (displacement.sqrMagnitude < 1e-4f)
            return false;
        ForcedMovement.GetOrAdd(this).Begin(displacement, Mathf.Max(0.05f, duration), arcHeight, false);
        if (source != null && source != this)
            RecordAttacker(source, 0f);
        return true;
    }

    /// <summary>True while a knockback, pull or dash is moving this character.</summary>
    public bool IsBeingDisplaced
    {
        get
        {
            ForcedMovement fm = ForcedMovement.Find(this);
            return fm != null && fm.IsActive;
        }
    }

    // ------------------------------------------------------------------ per frame
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        if (dt > 1e-5f)
            velocity = Vector3.Lerp(velocity, (pos - lastPosition) / dt, 0.5f);
        lastPosition = pos;

        TrackHealthChanges();

        if (Kind == EntityKind.Player)
        {
            UpdatePlayerSpeedDebuff();
            EmitPlayerNoise();
        }
    }

    /// <summary>Detects health lost outside the ability system (weapons, legacy effects) and attributes it.</summary>
    private void TrackHealthChanges()
    {
        if (Health == null)
            return;
        float hp = Health.CurrentValue;
        if (!healthSeen)
        {
            if (hp > 0f)
            {
                healthSeen = true;
                lastKnownHealth = hp;
            }
            return;
        }

        DamageInfo info = default;
        bool lostHealth = hp < lastKnownHealth - 0.01f && !deathRaised;
        if (lostHealth)
        {
            float lost = lastKnownHealth - hp;
            CombatEntity source = null;
            bool inferred = false;
            bool periodic = false;
            if (periodicSource != null && Time.time < periodicSourceUntil)
            {
                source = periodicSource;
                periodic = true;
            }
            else
            {
                source = InferAttacker();
                inferred = source != null;
            }

            info = new DamageInfo
            {
                amount = lost,
                source = source,
                target = this,
                point = Center,
                direction = source != null ? (Center - source.Center).normalized : Vector3.zero,
                isPeriodic = periodic,
                sourceInferred = inferred,
            };
        }

        // Health reached 0 outside the ability system (weapons, legacy effects): give Cheat Death & co. a chance first.
        if (hp <= 0f && !deathRaised && TrySurviveLethal(lostHealth ? info : new DamageInfo { target = this, point = Center }))
            hp = Health.CurrentValue;

        if (lostHealth)
        {
            RecordAttacker(info.source, info.amount);
            Damaged?.Invoke(info);
            CombatEvents.RaiseDamaged(info);
        }

        lastKnownHealth = hp;
        if (hp <= 0f && !deathRaised)
            HandleDeath(RecentAttacker(15f));
    }

    private CombatEntity InferAttacker()
    {
        float radius = CombatSettings.Instance.unattributedDamageRadius;
        if (radius <= 0f)
            return null;
        CombatEntity best = null;
        float bestSqr = radius * radius;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p == this || p.IsDead)
                continue;
            float d = (p.Position - Position).sqrMagnitude;
            if (d <= bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }
        return best;
    }

    /// <summary>Players move by their SpeedManager: slows and roots change its current speed and are undone exactly.</summary>
    private void UpdatePlayerSpeedDebuff()
    {
        if (Speed == null)
            return;
        float fraction = IsRooted ? 1f : (IsSlowed ? slowFraction : 0f);
        fraction = Mathf.Max(fraction, 1f - Mathf.Clamp01(CastMoveMultiplier));
        float original = Speed.Speed - playerSpeedDelta;
        float wanted = -original * fraction;
        if (Mathf.Abs(wanted - playerSpeedDelta) > 0.001f)
        {
            float before = Speed.Speed;
            Speed.ModifySpeed(wanted - playerSpeedDelta);
            playerSpeedDelta += Speed.Speed - before;
        }
    }

    private void EmitPlayerNoise()
    {
        if (Time.time < nextNoiseTime || IsDead)
            return;
        nextNoiseTime = Time.time + 0.3f;
        float speed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
        if (speed < 0.5f)
            return;
        float noiseRadius = Mathf.Lerp(3f, 14f, Mathf.Clamp01(speed / 9f));
        if (PlayerMovement != null && PlayerMovement.CurrentPlayerState == PlayerMovementModel.PlayerState.Crouching)
            noiseRadius *= 0.3f;
        CombatEvents.EmitNoise(Position, noiseRadius, this, 0.3f);
    }

    private void OnPlayerAttackStart()
    {
        CombatEvents.RaiseMeleeSwing(this);
        CombatEvents.EmitNoise(Position, 10f, this, 0.8f);
    }

    /// <summary>Is this character crouching/sneaking (harder to notice)?</summary>
    public bool IsSneaking => PlayerMovement != null && PlayerMovement.CurrentPlayerState == PlayerMovementModel.PlayerState.Crouching;

    /// <summary>Is this character in a dodge (roll/dash) right now?</summary>
    public bool IsEvading
    {
        get
        {
            if (PlayerMovement == null)
                return false;
            var s = PlayerMovement.CurrentPlayerState;
            return s == PlayerMovementModel.PlayerState.Rolling || s == PlayerMovementModel.PlayerState.Dashing;
        }
    }

    /// <summary>Is this character swinging a weapon (players) right now?</summary>
    public bool IsAttackingMelee => PlayerAnimation != null && PlayerAnimation.IsAttacking;
}

/// <summary>Who counts as an ally, an enemy or neutral.</summary>
public static class CombatRelations
{
    /// <summary>
    /// How <paramref name="to"/> relates to <paramref name="from"/>, checked in this order:
    /// self → same party → factions (when both have one: ally / enemy; neutral falls through) → same team → players
    /// (PvP rule) → the AI's hostility → summoner → default. Characters without parties or factions behave as before.
    /// </summary>
    public static CombatRelation Get(CombatEntity from, CombatEntity to)
    {
        if (from == null || to == null)
            return CombatRelation.Neutral;
        if (from == to)
            return CombatRelation.Self;
        if (CombatParties.SameParty(from, to))
            return CombatRelation.Party;

        CombatFaction fa = from.Faction, fb = to.Faction;
        if (fa != null && fb != null)
        {
            FactionStance stance = fa.StanceTowards(fb);
            if (stance == FactionStance.Ally) return CombatRelation.Ally;
            if (stance == FactionStance.Enemy) return CombatRelation.Enemy;
            // Neutral factions: the rules below (PvP, AI hostility) decide; players stay neutral to them.
            if (from.Kind == CombatEntity.EntityKind.Player && to.Kind != CombatEntity.EntityKind.Player)
                return CombatRelation.Neutral;
        }
        else if (fa != null || fb != null)
        {
            // Only one side has a faction: a faction hostile to everyone makes them enemies; otherwise as before.
            FactionStance stance = fa != null ? fa.StanceTowards(null) : fb.StanceTowards(null);
            if (stance == FactionStance.Enemy) return CombatRelation.Enemy;
        }

        if (from.Team == to.Team)
            return CombatRelation.Ally;

        if (from.Kind == CombatEntity.EntityKind.Player)
        {
            if (to.Kind == CombatEntity.EntityKind.Player)
                return CombatSettings.Instance.playersCanHurtEachOther ? CombatRelation.Enemy : CombatRelation.Ally;
            return CombatRelation.Enemy;
        }

        if (from.Hostility != null)
            return from.Hostility.IsHostileTo(to) ? CombatRelation.Enemy : CombatRelation.Neutral;

        if (from.Summoner != null)
            return Get(from.Summoner, to) == CombatRelation.Enemy ? CombatRelation.Enemy : CombatRelation.Neutral;

        // Characters without AI: enemies of players, neutral to everyone else.
        return to.Kind == CombatEntity.EntityKind.Player ? CombatRelation.Enemy : CombatRelation.Neutral;
    }

    public static bool Passes(TargetFilter filter, CombatRelation relation)
    {
        switch (relation)
        {
            case CombatRelation.Self: return (filter & TargetFilter.Self) != 0;
            case CombatRelation.Party: return (filter & (TargetFilter.Party | TargetFilter.Allies)) != 0; // party members are allies too
            case CombatRelation.Ally: return (filter & TargetFilter.Allies) != 0;
            case CombatRelation.Enemy: return (filter & TargetFilter.Enemies) != 0;
            default: return (filter & TargetFilter.Neutral) != 0;
        }
    }

    public static bool Passes(TargetFilter filter, CombatEntity from, CombatEntity to) => Passes(filter, Get(from, to));

    public static bool IsEnemy(CombatEntity from, CombatEntity to) => Get(from, to) == CombatRelation.Enemy;
}
