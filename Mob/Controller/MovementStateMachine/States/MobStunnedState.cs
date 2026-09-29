/// <summary>Stunned (or knocked down): stands still until the crowd control ends.</summary>
public class MobStunnedState : MobMovementState
{
    public MobStunnedState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        Motor.Stop();
        MobCombatCoordinator.ReleaseToken(Context);
    }

    protected override void OnUpdate(float dt)
    {
        if (!Self.IsStunned)
            Finish();
    }
}
