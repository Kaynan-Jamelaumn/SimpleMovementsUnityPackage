using UnityEngine;

/// <summary>
/// Goes back home after a fight, a chase or fleeing (running when far). After being leashed it can heal and
/// ignore attacks on the way (profile "Evade While Returning").
/// </summary>
public class MobReturningState : MobMovementState
{
    private float nextRepath;

    public MobReturningState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        nextRepath = 0f;
        MobCombatCoordinator.Disengage(Context);
    }

    protected override void OnExit()
    {
        if (Brain.Leashed && Profile.evadeWhileReturning && Self.IsAlive && !IsFinished)
            Self.ClearInvulnerable();
    }

    protected override void OnUpdate(float dt)
    {
        Vector3 home = Context.Home;
        float dist = CombatQuery.FlatDistance(Position, home);
        if (dist <= 1.5f || TimeInState > 60f)
        {
            Motor.Stop();
            Brain.OnReachedHome();
            Finish();
            return;
        }
        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + 1f;
        MobMoveMode mode = dist > 15f || Brain.Leashed ? MobMoveMode.Run : MobMoveMode.Walk;
        if (!Motor.MoveTo(home, mode, 1f) || Motor.IsStuck)
        {
            // Home unreachable (moved NavMesh, carved obstacle): make the current place the new home.
            if (TimeInState > 5f || Motor.IsOffMesh)
            {
                Context.MobReference.HomePosition = Position;
                Brain.OnReachedHome();
                Finish();
            }
        }
    }
}
