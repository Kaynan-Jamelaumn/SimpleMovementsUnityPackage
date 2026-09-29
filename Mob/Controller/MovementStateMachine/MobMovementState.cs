using UnityEngine;
using EState = MobMovementStateMachine.EMobMovementState;

/// <summary>
/// Base of every mob AI state. A state carries out one behaviour (wander, chase, attack...) using the context's
/// motor, perception and abilities; the <see cref="MobBrain"/> decides which state comes next. A state calls
/// <see cref="Finish"/> when its job is done, and can be "committed" (e.g. mid-attack) so the brain waits for it.
/// </summary>
public abstract class MobMovementState : BaseState<EState>
{
    protected MobMovementContext Context;
    protected float enterTime;
    private bool finished;

    protected MobMovementState(MobMovementContext context, EState stateKey) : base(stateKey)
    {
        Context = context;
    }

    /// <summary>The state has done its job and waits for the brain's next decision.</summary>
    public bool IsFinished => finished;

    /// <summary>While true the brain cannot switch away (except to Stunned or Dead).</summary>
    public virtual bool IsCommitted => false;

    /// <summary>Attacking: can the wind-up be cancelled to dodge (profile "Dodge Cancels Attacks")?</summary>
    public virtual bool CanCancelForDodge => false;

    protected float TimeInState => Time.time - enterTime;
    protected MobMotor Motor => Context.Motor;
    protected MobBrain Brain => Context.Brain;
    protected MobProfile Profile => Context.Profile;
    protected CombatEntity Self => Context.Entity;
    protected Vector3 Position => Context.Transform.position;

    public override void EnterState()
    {
        enterTime = Time.time;
        finished = false;
        OnEnter();
    }

    public override void ExitState() => OnExit();

    public override void UpdateState()
    {
        if (!finished)
            OnUpdate(Time.deltaTime);
    }

    public override EState GetNextState() => Context.Brain.ResolveNext(this);

    public override void LateUpdateState() { }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

    protected abstract void OnEnter();
    protected virtual void OnExit() { }
    protected abstract void OnUpdate(float dt);

    /// <summary>Marks the state as done and asks the brain for the next step.</summary>
    protected void Finish()
    {
        if (finished)
            return;
        finished = true;
        Context.Brain.RequestDecision();
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>Faces the current target (for strafing and backing off).</summary>
    protected void FaceTarget()
    {
        CombatEntity t = Brain.Target;
        if (t != null)
            Motor.FaceTowards(t.Position);
    }

    /// <summary>
    /// If the mob stands in (or is about to be hit by) a hazard, walks out of it. Returns true while escaping.
    /// </summary>
    protected bool EscapeHazardIfNeeded()
    {
        if (!Profile.avoidHazards || !Brain.StandingInHazard && Motor.IsSafe(Position, 0.6f))
            return false;
        Vector3 away = Brain.Target != null ? CombatQuery.FlatDirection(Brain.Target.Position, Position, Context.Transform.right) : Context.Transform.right;
        if (HazardRegistry.Query(Self, Position, Self.Radius, Self.Height, 1.5f, out HazardInfo info))
            away = info.escapeDirection;
        if (Motor.TryFindSafePoint(away, 5f, out Vector3 safe))
        {
            Motor.MoveTo(safe, MobMoveMode.Run, 0.2f);
            return true;
        }
        return false;
    }

    /// <summary>Old helper kept for custom states: moves to <paramref name="destination"/> at the current pace.</summary>
    protected bool SafeSetDestination(Vector3 destination) => Motor.MoveTo(destination, Motor.Mode);

    /// <summary>Old helper kept for custom states: a reachable point near the direction of <paramref name="originalDestination"/>.</summary>
    protected Vector3 FindAlternativePath(Vector3 originalDestination)
    {
        Vector3 d = originalDestination - Position;
        float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        return Motor.TryFindPointAround(Position, 5f, angle + Random.Range(-90f, 90f), out Vector3 p) ? p : Position;
    }
}
