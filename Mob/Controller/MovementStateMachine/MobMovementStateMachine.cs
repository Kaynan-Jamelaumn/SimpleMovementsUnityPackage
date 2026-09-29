using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The mob's AI: a finite state machine whose states (idle, wander, patrol, investigate, chase, combat, attack,
/// dodge, retreat, flee, return, stunned, dead) carry out what the <see cref="MobBrain"/> decides. Each frame it
/// updates the senses and decisions (at a rate set by the distance to players), the movement and the animation,
/// then runs the current state.
/// </summary>
[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
public class MobMovementStateMachine : StateManager<MobMovementStateMachine.EMobMovementState>, IAssignmentsValidator
{
    /// <summary>States of the mob AI. The first four keep their old values.</summary>
    public enum EMobMovementState
    {
        Idle,
        /// <summary>Wandering around home (or following its summoner).</summary>
        Moving,
        Chasing,
        Patrol,
        Investigate,
        /// <summary>Engaged: positioning around the target between attacks.</summary>
        Combat,
        Attacking,
        Dodging,
        Retreating,
        Fleeing,
        Returning,
        Stunned,
        Dead,
    }

    [Header("References (empty = found automatically on this object)")]
    [Tooltip("The mob component (MobActionsController) on this object. Found automatically when empty.")]
    [SerializeField] private MobActionsController actionsController;
    [Tooltip("The Mob component (same object as the MobActionsController). Found automatically when empty.")]
    [SerializeField] private Mob mob;
    [Tooltip("Animator driven by the AI (speed, combat, hit, dodge, death...). Empty = Model/VisualModel's Animator or the first Animator in the children.")]
    [SerializeField] private Animator animator;
    [Tooltip("Moves the mob on the baked NavMesh. Required.")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [Tooltip("Health, speed and death of the mob. Required (the AI reads its health and reacts to death).")]
    [SerializeField] private MobStatusController statusController;

    [Header("Debug")]
    [Tooltip("Log decisions (targets, attacks, fleeing, leashing) to the Console.")]
    [SerializeField] private bool debugLog = false;
    [Tooltip("Draw sight range, field of view, leash, patrol route, target and destination when selected.")]
    [SerializeField] private bool drawGizmos = true;
    [Tooltip("Current state (read only).")]
    [SerializeField] private EMobMovementState debugState;
    [Tooltip("Current target (read only).")]
    [SerializeField] private string debugTarget;

    private MobMovementContext context;
    private MobAbilityController abilities;
    private bool asleep;

    /// <summary>The AI context (brain, perception, motor...). Null before Awake.</summary>
    public MobMovementContext Context => context;

    public MobMovementContext GetContext() => context;

    public EMobMovementState CurrentStateKey => CurrentState != null ? CurrentState.StateKey : EMobMovementState.Idle;

    /// <summary>True when the current state has completed its job and is waiting for the next decision.</summary>
    public bool CurrentStateFinished => CurrentState is MobMovementState s && s.IsFinished;

    private void Awake()
    {
        ResolveComponents();
        abilities = GetComponent<MobAbilityController>();
        if (abilities != null)
            abilities.Initialize();

        context = new MobMovementContext(this, mob, actionsController, animator, statusController, navMeshAgent, abilities)
        {
            DebugLog = debugLog,
        };
        InitializeStates();
    }

    /// <summary>Fills every empty reference from this object (inspector button and setup tools).</summary>
    public void AutoAssignReferences() => ResolveComponents();

    /// <summary>The references as currently assigned (for the inspector).</summary>
    public Animator AnimatorReference => animator;
    public NavMeshAgent AgentReference => navMeshAgent;
    public MobStatusController StatusReference => statusController;
    public Mob MobReference => mob;

    private void ResolveComponents()
    {
        if (mob == null) mob = GetComponent<Mob>();
        if (actionsController == null) actionsController = mob as MobActionsController;
        if (actionsController == null) actionsController = GetComponent<MobActionsController>();
        if (mob == null) mob = actionsController;
        if (statusController == null) statusController = GetComponent<MobStatusController>();
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = FindAnimator(transform);
    }

    /// <summary>The Animator of a mob: Model/VisualModel (old layout) or the first one in the children.</summary>
    public static Animator FindAnimator(Transform root)
    {
        if (root.childCount > 0)
        {
            Transform model = root.GetChild(0);
            if (model.childCount > 0)
            {
                Animator a = model.GetChild(0).GetComponent<Animator>();
                if (a != null)
                    return a;
            }
        }
        return root.GetComponentInChildren<Animator>();
    }

    protected override void Start()
    {
        ValidateAssignments();
        context.Perception.Subscribe();
        context.Brain.RecomputeRanges();
        if (abilities != null)
            abilities.SlotChanged += OnSlotChanged;
        base.Start();
    }

    private void OnEnable()
    {
        if (context != null && Application.isPlaying && CurrentState != null)
            context.Perception.Subscribe();
    }

    private void OnDisable()
    {
        if (context == null)
            return;
        context.Perception.Unsubscribe();
        MobCombatCoordinator.Disengage(context);
    }

    private void OnDestroy()
    {
        if (context == null)
            return;
        context.Perception.Unsubscribe();
        MobCombatCoordinator.Disengage(context);
        context.Motor.Dispose();
        if (abilities != null)
            abilities.SlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(int slot) => context.Brain.RecomputeRanges();

    /// <summary>Validates the setup (warnings only - a mob with a missing piece keeps running what it can).</summary>
    public void ValidateAssignments()
    {
        if (mob == null)
            Debug.LogWarning($"[{name}] MobMovementStateMachine needs a Mob / MobActionsController component on the same object.", this);
        if (navMeshAgent == null)
            Debug.LogWarning($"[{name}] MobMovementStateMachine needs a NavMeshAgent.", this);
        if (statusController == null)
            Debug.LogWarning($"[{name}] MobMovementStateMachine needs a MobStatusController.", this);
        if (animator == null)
            Debug.LogWarning($"[{name}] No Animator found (expected Model/VisualModel/Animator or any child). The mob will work without animations.", this);
    }

    private void InitializeStates()
    {
        MobMovementContext c = context;
        States.Add(EMobMovementState.Idle, new MobIdleState(c, EMobMovementState.Idle));
        States.Add(EMobMovementState.Moving, new MobMovingState(c, EMobMovementState.Moving));
        States.Add(EMobMovementState.Chasing, new MobChasingState(c, EMobMovementState.Chasing));
        States.Add(EMobMovementState.Patrol, new MobPatrolState(c, EMobMovementState.Patrol));
        States.Add(EMobMovementState.Investigate, new MobInvestigateState(c, EMobMovementState.Investigate));
        States.Add(EMobMovementState.Combat, new MobCombatState(c, EMobMovementState.Combat));
        States.Add(EMobMovementState.Attacking, new MobAttackingState(c, EMobMovementState.Attacking));
        States.Add(EMobMovementState.Dodging, new MobDodgingState(c, EMobMovementState.Dodging));
        States.Add(EMobMovementState.Retreating, new MobRetreatingState(c, EMobMovementState.Retreating));
        States.Add(EMobMovementState.Fleeing, new MobFleeingState(c, EMobMovementState.Fleeing));
        States.Add(EMobMovementState.Returning, new MobReturningState(c, EMobMovementState.Returning));
        States.Add(EMobMovementState.Stunned, new MobStunnedState(c, EMobMovementState.Stunned));
        States.Add(EMobMovementState.Dead, new MobDeadState(c, EMobMovementState.Dead));
        CurrentState = States[EMobMovementState.Idle];
    }

    protected override void Update()
    {
        if (context == null || CurrentState == null)
            return;
        float now = Time.time;
        float dt = Time.deltaTime;
        MobMovementContext c = context;
        c.DebugLog = debugLog;

        if (CurrentStateKey != EMobMovementState.Dead)
        {
            c.Scheduler.UpdateTier(now, transform.position, c.Brain.InCombat || c.Brain.Mode == MobMode.Flee, c.Profile.alwaysFullRate);
            if (c.Scheduler.ShouldPerceive(now))
                c.Perception.Tick(now);
            if (c.Scheduler.Awake)
            {
                asleep = false;
                c.Brain.Tick(now, c.Scheduler.ShouldDecide(now, c.Profile.decisionInterval));
            }
            else if (!asleep)
            {
                asleep = true;
                c.Brain.EnterSleep(); // far from every player: stand still and stop thinking
            }
        }

        c.Motor.Tick(dt);
        if (c.Scheduler.ShouldAnimate())
            c.Animation.Tick(dt);

        // A finished state that the brain wants again restarts (e.g. another idle pause, the next patrol point).
        if (CurrentState is MobMovementState s && s.IsFinished && c.Brain.ResolveNext(s) == s.StateKey && s.StateKey != EMobMovementState.Dead)
            TransitionToState(s.StateKey);

        base.Update();

        debugState = CurrentStateKey;
        debugTarget = c.Brain.Target != null ? c.Brain.Target.name : "";
    }

    // ------------------------------------------------------------------ external API
    /// <summary>Called by the status controller when the mob dies.</summary>
    public void OnDeath()
    {
        if (context == null)
            return;
        context.Brain.ForceDead();
        if (CurrentStateKey != EMobMovementState.Dead && HasState(EMobMovementState.Dead))
            TransitionToState(EMobMovementState.Dead);
    }

    /// <summary>Makes the mob attack <paramref name="target"/> (scripts, quests, summons).</summary>
    public void SetTarget(CombatEntity target)
    {
        if (context == null || target == null)
            return;
        context.Scheduler.WakeUp();
        context.Brain.ForceTarget(target);
    }

    /// <summary>Makes the mob go and look at a position (noise, alarm).</summary>
    public void Investigate(Vector3 position, float urgency = 0.7f)
    {
        if (context == null)
            return;
        context.Scheduler.WakeUp();
        context.Memory.SetInvestigate(position, urgency);
        context.Brain.RequestDecision();
    }

    // ------------------------------------------------------------------ gizmos
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;
        Mob m = mob != null ? mob : GetComponent<Mob>();
        MobProfile p = context != null ? context.Profile : (m != null ? m.ProfileAsset : null);
        Vector3 pos = transform.position;

        if (p != null)
        {
            // Sight and field of view.
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            DrawArc(pos, transform.forward, p.sightRange, p.fieldOfView);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.6f);
            DrawArc(pos, transform.forward, p.closeSenseRadius, 360f);

            Vector3 home = Application.isPlaying && m != null ? m.HomePosition : pos;
            if (p.leashDistance > 0f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                DrawArc(home, Vector3.forward, p.leashDistance, 360f);
            }
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.4f);
            DrawArc(home, Vector3.forward, p.wanderRadius, 360f);
            if (p.aggression == MobAggression.Territorial)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.4f);
                DrawArc(home, Vector3.forward, p.territoryRadius, 360f);
            }
        }

        if (m != null && m.PatrolPointCount > 0)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = m.GetPatrolPoint(0);
            Gizmos.DrawWireSphere(prev, 0.3f);
            for (int i = 1; i < m.PatrolPointCount; i++)
            {
                Vector3 next = m.GetPatrolPoint(i);
                Gizmos.DrawLine(prev, next);
                Gizmos.DrawWireSphere(next, 0.3f);
                prev = next;
            }
        }

        if (!Application.isPlaying || context == null)
            return;
        MobBrain brain = context.Brain;
        if (brain.Target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos + Vector3.up, brain.Target.Center);
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            DrawArc(brain.Target.Position, Vector3.forward, brain.PreferredMin, 360f);
            DrawArc(brain.Target.Position, Vector3.forward, brain.PreferredMax, 360f);
        }
        if (brain.Threat != null)
        {
            Gizmos.color = new Color(0.6f, 0f, 1f);
            Gizmos.DrawLine(pos + Vector3.up * 1.2f, brain.Threat.Center);
        }
        if (context.Motor.HasDestination)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pos, context.Motor.Destination);
            Gizmos.DrawWireSphere(context.Motor.Destination, 0.25f);
        }
        if (context.Memory.HasInvestigatePoint)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
            Gizmos.DrawWireCube(context.Memory.InvestigatePoint, Vector3.one * 0.5f);
        }
    }

    private static void DrawArc(Vector3 center, Vector3 forward, float radius, float angle)
    {
        if (radius <= 0f)
            return;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.forward;
        forward.Normalize();
        int segments = Mathf.Max(8, Mathf.CeilToInt(angle / 8f));
        float half = Mathf.Min(angle, 360f) * 0.5f;
        Vector3 prev = center + Quaternion.AngleAxis(-half, Vector3.up) * forward * radius + Vector3.up * 0.1f;
        if (angle < 359f)
            Gizmos.DrawLine(center + Vector3.up * 0.1f, prev);
        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments);
            Vector3 next = center + Quaternion.AngleAxis(a, Vector3.up) * forward * radius + Vector3.up * 0.1f;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        if (angle < 359f)
            Gizmos.DrawLine(prev, center + Vector3.up * 0.1f);
    }
}
