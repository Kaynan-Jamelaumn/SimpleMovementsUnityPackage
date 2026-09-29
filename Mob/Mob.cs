using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A creature driven by the mob AI (<see cref="MobMovementStateMachine"/>). Holds its identity (type, preys, team),
/// home and patrol route, its behaviour profile and the old per-mob settings (still used to build a profile when
/// none is assigned).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobStatusController))]
[DefaultExecutionOrder(-20)]
public class Mob : MonoBehaviour, ICombatHostility, ISummonable
{
    /// <summary>Delegate for the event raised when the mob is destroyed (spawners count kills with it).</summary>
    public delegate void MobDestroyedHandler();
    public event MobDestroyedHandler OnMobDestroyed;

    /// <summary>Raised once when the mob dies (this mob, killer - may be null).</summary>
    public event Action<Mob, CombatEntity> Died;

    [Tooltip("Health, speed and death of this mob. Empty = the MobStatusController on this object.")]
    [SerializeField] private MobStatusController statusController;

    [Header("AI Profile")]
    [Tooltip("Behaviour of this mob type: temperament, senses, movement, fighting style, dodging, fleeing, animation. Empty = built from the old fields below (Detection Range, Wander Distance, Chase Time, Preys...).")]
    [SerializeField] protected MobProfile profile;

    [Tooltip("Combat team. Mobs of the same team are allies. Empty = the mob Type (mobs of the same type help each other).")]
    [SerializeField] protected string teamOverride = "";

    [Header("Wander")]
    [Tooltip("How far the animal can move in one go (used when no profile is assigned).")]
    [SerializeField] private float wanderDistance = 50f;

    [Tooltip("Maximum time the animal will wander (used when no profile is assigned).")]
    [SerializeField] private float maxWalkTime = 6f;

    [Tooltip("Points that the animal will patrol (world positions, or relative to its spawn point when 'Patrol Points Relative To Home' is on).")]
    [SerializeField] protected Vector3[] patrolPoints;

    [Tooltip("Optional: a transform whose children are the patrol points (easier to edit in the scene). Used instead of Patrol Points when set.")]
    [SerializeField] protected Transform patrolRoute;

    [Tooltip("Patrol Points are offsets from where the mob spawned (use this for prefabs placed by spawners).")]
    [SerializeField] protected bool patrolPointsRelativeToHome = false;

    [Tooltip("Current patrol point index.")]
    [SerializeField] protected int currentPatrolPoint = 0;

    [Header("Idle")]
    [Tooltip("How long the animal takes a break for (used when no profile is assigned).")]
    [SerializeField] private float idleTime = 5f;

    [Header("Identity")]
    [Tooltip("Types of mobs (a helper list for the inspector).")]
    [SerializeField] public List<string> mobTypes = new List<string> { "Player", "Sheep", "Wolf", "Fox" };

    [Tooltip("The type of this mob. Other mobs list it in their Preys to hunt it; mobs of the same type are allies unless a Team is set.")]
    [SerializeField] public string type;

    [Tooltip("Sight range used when no profile is assigned.")]
    [SerializeField] protected float detectionRange = 10f;

    [Tooltip("Old detection shape (kept for compatibility; the AI now uses the profile's sight, field of view and hearing).")]
    [SerializeField] protected Cast detectionCast;

    [Header("Prey Variables")]
    [Tooltip("How far the prey runs from a predator (used when no profile is assigned).")]
    [SerializeField] protected float escapeMaxDistance = 80f;

    [Tooltip("The predator currently chasing this mob (read only, set by the AI).")]
    [SerializeField] protected MobActionsController currentPredator = null;

    [Header("Predator Variables")]
    [Tooltip("The maximum time the predator chases a target it cannot catch (players only when 'Player Has Max Chase Time').")]
    [SerializeField] protected float maxChaseTime = 10f;

    [Tooltip("Damage of the automatic basic attack (used when the mob has no melee ability).")]
    [SerializeField] protected int biteDamage = 3;

    [Tooltip("OFF: after a basic attack the mob pauses for a moment (recovery). ON: it keeps moving.")]
    [SerializeField] protected bool isPartialWait = false;

    [Tooltip("Cooldown of the automatic basic attack (seconds).")]
    [SerializeField] protected float biteCooldown = 1f;

    [Tooltip("Reach of the automatic basic attack (metres).")]
    [SerializeField] protected float attackDistance = 2f;

    [Tooltip("The mob this one is hunting (read only, set by the AI).")]
    [SerializeField] protected MobActionsController currentChaseTarget;

    [Header("Player Chase Variables")]
    [Tooltip("Give up chasing a player after Max Chase Time.")]
    [SerializeField] protected bool playerHasMaxChaseTime = false;

    [Tooltip("The player this mob is fighting (read only, set by the AI).")]
    [SerializeField] protected PlayerStatusController currentPlayerTarget;

    [Tooltip("What this mob hunts: 'Player' for players, and mob Types (e.g. 'Sheep').")]
    [SerializeField] protected List<string> Preys;

    [Tooltip("Extra margin used by HasReachedDestinationWithMargin (metres).")]
    [SerializeField] private float stoppingMargin = 0;

    private Coroutine waitToMoveRoutine;
    private Coroutine waitToReachDestinationRoutine;

    protected NavMeshAgent navMeshAgent;
    protected Animator animator;

    private MobProfile runtimeProfile;
    private MobMovementStateMachine ai;
    private CombatEntity entity;
    private bool dead;
    private int patrolDirection = 1;

    // ------------------------------------------------------------------ properties (old API kept)
    public float WanderDistance { get => wanderDistance; set => wanderDistance = value; }
    public float DetectionRange { get => detectionRange; }
    public Transform TransformReference { get => transform; }
    public Coroutine WaitToMoveRoutine { get => waitToMoveRoutine; set => waitToMoveRoutine = value; }
    public Coroutine WaitToReachDestinationRoutine { get => waitToReachDestinationRoutine; set => waitToReachDestinationRoutine = value; }
    public PlayerStatusController CurrentPlayerTarget { get => currentPlayerTarget; set => currentPlayerTarget = value; }
    public MobActionsController CurrentPredator { get => currentPredator; set => currentPredator = value; }
    public MobActionsController CurrentChaseTarget { get => currentChaseTarget; set => currentChaseTarget = value; }
    public Cast DetectionCast { get => detectionCast; }
    public List<string> PreysReference { get => Preys; }
    public bool PlayerHasMaxChaseTime { get => playerHasMaxChaseTime; }
    public bool IsPartialWait { get => isPartialWait; }
    public float MaxWalkTime { get => maxWalkTime; }
    public float MaxChaseTime { get => maxChaseTime; }
    public float IdleTime { get => idleTime; set => idleTime = value; }
    public int CurrentPatrolPoint { get => currentPatrolPoint; set => currentPatrolPoint = value; }
    public Vector3[] PatrolPoints { get => patrolPoints; set => patrolPoints = value; }
    public NavMeshAgent NavMeshAgentReference { get => navMeshAgent; set => navMeshAgent = value; }
    public int BiteDamage { get => biteDamage; }
    public float BiteCooldown { get => biteCooldown; }
    public float AttackDistance { get => attackDistance; }
    public float EscapeMaxDistance { get => escapeMaxDistance; }

    // ------------------------------------------------------------------ new API
    /// <summary>The behaviour profile in use (the assigned asset, or one built from the old fields).</summary>
    public MobProfile Profile
    {
        get
        {
            if (profile != null)
                return profile;
            if (runtimeProfile == null)
                runtimeProfile = MobProfile.FromLegacy(this);
            return runtimeProfile;
        }
    }

    /// <summary>The assigned profile asset (may be null).</summary>
    public MobProfile ProfileAsset => profile;

    /// <summary>Team used for combat relations: the Team field, or the mob type.</summary>
    public virtual string CombatTeam => !string.IsNullOrEmpty(teamOverride) ? teamOverride : (string.IsNullOrEmpty(type) ? name : type);

    /// <summary>Where the mob lives (its spawn point). The AI wanders, patrols and returns around it.</summary>
    public Vector3 HomePosition { get; set; }

    /// <summary>The combat identity of this mob.</summary>
    public CombatEntity Entity
    {
        get
        {
            if (entity == null)
                entity = CombatEntity.GetOrAdd(gameObject);
            return entity;
        }
    }

    /// <summary>The AI state machine (null if the mob has none).</summary>
    public MobMovementStateMachine AI
    {
        get
        {
            if (ai == null)
                ai = GetComponent<MobMovementStateMachine>();
            return ai;
        }
    }

    public MobStatusController StatusController => statusController;
    public Animator Animator => animator;
    public bool IsDead => dead || (entity != null && entity.IsDead);

    /// <summary>Summoned mobs with this flag never drop absorbable abilities.</summary>
    public bool SummonedPreventAbsorption { get; private set; }

    /// <summary>Direction of travel along the patrol route (ping-pong).</summary>
    public int PatrolDirection { get => patrolDirection; set => patrolDirection = value >= 0 ? 1 : -1; }

    public int PatrolPointCount
    {
        get
        {
            if (patrolRoute != null)
                return patrolRoute.childCount;
            return patrolPoints != null ? patrolPoints.Length : 0;
        }
    }

    public Vector3 GetPatrolPoint(int index)
    {
        if (patrolRoute != null)
        {
            if (patrolRoute.childCount == 0)
                return transform.position;
            return patrolRoute.GetChild(Mathf.Clamp(index, 0, patrolRoute.childCount - 1)).position;
        }
        if (patrolPoints == null || patrolPoints.Length == 0)
            return transform.position;
        Vector3 p = patrolPoints[Mathf.Clamp(index, 0, patrolPoints.Length - 1)];
        if (patrolPointsRelativeToHome)
            p += Application.isPlaying ? HomePosition : transform.position;
        return p;
    }

    // ------------------------------------------------------------------ lifecycle
    protected virtual void Awake()
    {
        if (statusController == null)
            statusController = GetComponent<MobStatusController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        HomePosition = transform.position;
        entity = CombatEntity.GetOrAdd(gameObject);
        entity.Hostility = this;
    }

    protected virtual void Start()
    {
        ValidateAssignments();
        InitializeAnimal();
    }

    private void ValidateAssignments()
    {
        if (statusController == null)
            Debug.LogWarning($"[{name}] Mob has no MobStatusController.", this);
        if (string.IsNullOrEmpty(type))
            Debug.LogWarning($"[{name}] Mob Type is empty; set it so other mobs can recognise it (prey/predator) and allies can help.", this);
    }

    /// <summary>Finds the Animator and sets up the NavMeshAgent.</summary>
    protected virtual void InitializeAnimal()
    {
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
        animator = MobMovementStateMachine.FindAnimator(transform);
        if (statusController != null && statusController.SpeedManager != null && statusController.SpeedManager.Speed > 0f && navMeshAgent != null)
            navMeshAgent.speed = statusController.SpeedManager.Speed;
    }

    /// <summary>Checks if the mob has reached its destination, considering a margin of error.</summary>
    public bool HasReachedDestinationWithMargin()
    {
        if (navMeshAgent == null || !navMeshAgent.isActiveAndEnabled || !navMeshAgent.isOnNavMesh)
            return true;
        return navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + stoppingMargin;
    }

    /// <summary>A random position on the NavMesh within <paramref name="distance"/> of <paramref name="origin"/> (or the origin).</summary>
    public Vector3 GetRandomNavMeshPosition(Vector3 origin, float distance)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * distance + origin;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navMeshHit, distance, NavMesh.AllAreas))
                return navMeshHit.position;
        }
        return origin;
    }

    /// <summary>Deals damage to the mob (attacker unknown). Prefer CombatEntity.ApplyDamage with a source.</summary>
    public virtual void ReceiveDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;
        Entity.ApplyDamage(new DamageInfo { amount = damage, point = Entity.Center });
        if (statusController != null && statusController.HealthManager != null && statusController.HealthManager.CurrentValue <= 0)
            Die();
    }

    /// <summary>Kills the mob (death animation, drops, absorption, removal after the profile's delay).</summary>
    protected virtual void Die()
    {
        if (statusController != null)
            statusController.Kill();
        else
            Destroy(gameObject);
    }

    /// <summary>Called once by the status controller when the mob dies.</summary>
    public virtual void OnDeath(CombatEntity killer)
    {
        if (dead)
            return;
        dead = true;
        StopAllCoroutines();
        MobMovementStateMachine machine = AI;
        if (machine != null)
            machine.OnDeath();
        Died?.Invoke(this, killer);
    }

    protected virtual void OnDestroy()
    {
        OnMobDestroyed?.Invoke();
    }

    // ------------------------------------------------------------------ relations & summons
    /// <summary>Is this mob hostile to <paramref name="other"/>? (Used by abilities' target filters.)</summary>
    public bool IsHostileTo(CombatEntity other)
    {
        MobMovementStateMachine machine = AI;
        if (machine != null && machine.Context != null)
            return machine.Context.Brain.IsHostileTo(other);
        if (other == null)
            return false;
        if (other.Kind == CombatEntity.EntityKind.Player)
            return Preys != null && Preys.Contains("Player");
        return other.Mob != null && Preys != null && Preys.Contains(other.Mob.type);
    }

    public void OnSummoned(CombatEntity summoner, CombatEntity target, float lifetime, bool preventAbsorption)
    {
        SummonedPreventAbsorption = preventAbsorption;
        HomePosition = transform.position;
        if (summoner != null)
            Entity.Summoner = summoner;
        MobMovementStateMachine machine = AI;
        if (machine != null && machine.Context != null)
            machine.Context.Brain.OnSummoned(target);
        else if (target != null && machine != null)
            StartCoroutine(SetTargetNextFrame(machine, target));
    }

    private IEnumerator SetTargetNextFrame(MobMovementStateMachine machine, CombatEntity target)
    {
        yield return null;
        if (machine != null && target != null)
            machine.SetTarget(target);
    }

    /// <summary>Keeps the old inspector fields (current player / chase target / predator) in sync with the AI.</summary>
    public void SyncLegacyTargets(CombatEntity target, CombatEntity threat)
    {
        currentPlayerTarget = target != null ? target.Status as PlayerStatusController : null;
        currentChaseTarget = target != null ? target.Mob as MobActionsController : null;
        currentPredator = threat != null ? threat.Mob as MobActionsController : null;
    }
}
