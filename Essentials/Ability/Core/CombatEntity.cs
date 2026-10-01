using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    /// <summary>
    /// Asked when damage would kill this character, before it dies. A handler that restores some health and returns
    /// true saves it (e.g. the Cheat Death trait). Handlers are asked in order until one saves it.
    /// </summary>
    public event Func<DamageInfo, bool> LethalDamage;
    /// <summary>Raised when crowd control is applied (type, duration, source).</summary>
    public event Action<ControlType, float, CombatEntity> ControlApplied;

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
        public float threat;
        public float lastTime;
    }

    private readonly List<ThreatEntry> threats = new List<ThreatEntry>(4);
    private readonly List<IDamageTakenModifier> damageTakenModifiers = new List<IDamageTakenModifier>(2);

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
        info.amount *= damageTakenMultiplier;
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
        RecordAttacker(info.source, removed);
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
        Health.AddCurrentValue(amount);
        lastKnownHealth = Health.CurrentValue;
        Healed?.Invoke(amount);
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

    private void RecordAttacker(CombatEntity source, float amount)
    {
        if (source == null || source == this)
            return;
        lastAttacker = source;
        lastAttackTime = Time.time;
        AddThreat(source, Mathf.Max(1f, amount));
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
    }

    // ------------------------------------------------------------------ threat (used by mob AI)
    public void AddThreat(CombatEntity source, float amount)
    {
        if (source == null || source == this)
            return;
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity == source)
            {
                ThreatEntry t = threats[i];
                t.threat += amount;
                t.lastTime = Time.time;
                threats[i] = t;
                return;
            }
        }
        threats.Add(new ThreatEntry { entity = source, threat = amount, lastTime = Time.time });
    }

    /// <summary>Threat accumulated from <paramref name="source"/>, decaying after <paramref name="memory"/> seconds.</summary>
    public float GetThreat(CombatEntity source, float memory = 20f)
    {
        for (int i = 0; i < threats.Count; i++)
        {
            if (threats[i].entity == source)
                return Time.time - threats[i].lastTime > memory ? 0f : threats[i].threat;
        }
        return 0f;
    }

    /// <summary>The living character that caused the most threat recently, or null.</summary>
    public CombatEntity TopThreat(float memory = 20f)
    {
        CombatEntity best = null;
        float bestThreat = 0f;
        for (int i = threats.Count - 1; i >= 0; i--)
        {
            ThreatEntry t = threats[i];
            if (t.entity == null || t.entity.IsDead || Time.time - t.lastTime > memory)
            {
                threats.RemoveAt(i);
                continue;
            }
            if (t.threat > bestThreat)
            {
                bestThreat = t.threat;
                best = t.entity;
            }
        }
        return best;
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

    /// <summary>Movement speed multiplier from crowd control and casting (0 when stunned or rooted).</summary>
    public float MoveSpeedMultiplier
    {
        get
        {
            if (IsStunned || IsRooted)
                return 0f;
            float m = IsSlowed ? 1f - slowFraction : 1f;
            return m * Mathf.Clamp01(CastMoveMultiplier);
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
        duration *= controlDurationMultiplier;
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
            RecordAttacker(source, 0f);
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
