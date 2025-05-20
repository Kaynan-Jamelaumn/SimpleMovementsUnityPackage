using UnityEngine;

public class IdleState : MovementState
{
    public IdleState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = 0;
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        MovePlayer();
    }
    public override MovementStateMachine.EMovementState GetNextState()
    {
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;

        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


}