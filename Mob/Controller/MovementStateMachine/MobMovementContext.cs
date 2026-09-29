using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Decides how often a mob thinks, from its distance to the nearest player: full rate nearby (and always while
/// fighting), slower far away, asleep very far away. Updates are spread over frames with a random phase.
/// </summary>
public sealed class MobAIScheduler
{
    public enum Tier
    {
        /// <summary>Full rate (within Combat Settings' AI Near Distance of a player, or fighting).</summary>
        Near,
        /// <summary>Slightly slower (up to AI Far Distance).</summary>
        Mid,
        /// <summary>Slow (up to AI Sleep Distance).</summary>
        Far,
        /// <summary>No perception or decisions.</summary>
        Sleep,
    }

    public Tier Current { get; private set; } = Tier.Near;
    public bool Awake => Current != Tier.Sleep;

    private readonly float phase;
    private float nextTierCheck;
    private float nextPerception;
    private float nextDecision;
    private int frameOffset;

    public MobAIScheduler()
    {
        phase = Random.value;
        frameOffset = Random.Range(0, 3);
        nextPerception = Time.time + phase * 0.15f;
        nextDecision = Time.time + phase * 0.2f;
    }

    public void UpdateTier(float now, Vector3 position, bool inCombat, bool alwaysFull)
    {
        if (now < nextTierCheck)
            return;
        nextTierCheck = now + (Current == Tier.Sleep ? 1.5f : 0.75f) + phase * 0.25f;
        if (alwaysFull || inCombat || CombatEntity.Players.Count == 0)
        {
            Current = Tier.Near;
            return;
        }
        CombatEntity.NearestPlayer(position, out float distance);
        CombatSettings s = CombatSettings.Instance;
        if (distance <= s.aiNearDistance)
            Current = Tier.Near;
        else if (distance <= s.aiFarDistance)
            Current = Tier.Mid;
        else if (distance <= s.aiSleepDistance)
            Current = Tier.Far;
        else
            Current = Tier.Sleep;
    }

    public void WakeUp()
    {
        Current = Tier.Near;
        nextTierCheck = Time.time + 2f;
    }

    public bool ShouldPerceive(float now)
    {
        if (!Awake || now < nextPerception)
            return false;
        float interval = Current == Tier.Near ? 0.15f : (Current == Tier.Mid ? 0.3f : 0.6f);
        nextPerception = now + interval * (0.9f + phase * 0.2f);
        return true;
    }

    public bool ShouldDecide(float now, float interval)
    {
        if (!Awake || now < nextDecision)
            return false;
        float scaled = Current == Tier.Near ? interval : (Current == Tier.Mid ? interval * 1.5f : Mathf.Max(0.6f, interval * 3f));
        nextDecision = now + scaled * (0.9f + phase * 0.2f);
        return true;
    }

    public bool ShouldAnimate()
    {
        if (Current == Tier.Near || Current == Tier.Mid)
            return true;
        if (Current == Tier.Sleep)
            return false;
        return (Time.frameCount + frameOffset) % 3 == 0;
    }
}

/// <summary>
/// Everything a mob's states and AI systems share: component references, the profile, and the AI parts
/// (memory, perception, brain, motor, ability selector, animation, scheduler).
/// </summary>
public class MobMovementContext
{
    private readonly MobActionsController actionsController;
    private readonly Mob mob;
    private readonly Animator animator;
    private readonly MobStatusController statusController;
    private readonly NavMeshAgent navMeshAgent;

    /// <summary>Legacy constructor (only the component references).</summary>
    public MobMovementContext(Mob mob, MobActionsController actionsController, Animator animator, MobStatusController statusController, NavMeshAgent navMeshAgent)
    {
        this.actionsController = actionsController;
        this.mob = mob;
        this.animator = animator;
        this.statusController = statusController;
        this.navMeshAgent = navMeshAgent;
        Transform = mob != null ? mob.transform : (navMeshAgent != null ? navMeshAgent.transform : null);
    }

    /// <summary>Full AI context used by <see cref="MobMovementStateMachine"/>.</summary>
    public MobMovementContext(MobMovementStateMachine machine, Mob mob, MobActionsController actionsController, Animator animator, MobStatusController statusController, NavMeshAgent navMeshAgent, MobAbilityController abilities)
        : this(mob, actionsController, animator, statusController, navMeshAgent)
    {
        Machine = machine;
        Transform = machine.transform;
        Entity = mob != null ? mob.Entity : CombatEntity.GetOrAdd(machine.gameObject);
        Abilities = abilities;
        Profile = mob != null ? mob.Profile : MobProfile.FromLegacy(null);

        Memory = new MobMemory();
        Scheduler = new MobAIScheduler();
        Selector = new MobAbilitySelector(this);
        Brain = new MobBrain(this);
        Perception = new MobPerception(this);
        Motor = new MobMotor(this);
        Animation = new MobAnimationDriver(this);
    }

    // ------------------------------------------------------------------ legacy references
    public MobActionsController ActionsController => actionsController;
    public Mob MobReference => mob;
    public Animator Anim => animator;
    public MobStatusController StatusController => statusController;
    public NavMeshAgent NavMeshAgentReference => navMeshAgent;

    // ------------------------------------------------------------------ AI
    public MobMovementStateMachine Machine { get; }
    public Transform Transform { get; }
    public CombatEntity Entity { get; }
    public MobAbilityController Abilities { get; }
    public MobProfile Profile { get; }
    public MobMemory Memory { get; }
    public MobPerception Perception { get; }
    public MobBrain Brain { get; }
    public MobMotor Motor { get; }
    public MobAbilitySelector Selector { get; }
    public MobAnimationDriver Animation { get; }
    public MobAIScheduler Scheduler { get; }

    /// <summary>Where the mob lives (spawn point; its summoner for summons).</summary>
    public Vector3 Home
    {
        get
        {
            if (Entity != null && Entity.Summoner != null && Entity.Summoner.IsAlive)
                return Entity.Summoner.Position;
            return mob != null ? mob.HomePosition : Transform.position;
        }
    }

    /// <summary>Log AI decisions to the Console (Debug Log on the state machine).</summary>
    public bool DebugLog { get; set; }

    public void Log(string message)
    {
        if (DebugLog && mob != null)
            Debug.Log($"[AI {mob.name}] {message}", mob);
    }
}
