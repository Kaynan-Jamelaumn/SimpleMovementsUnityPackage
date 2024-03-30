using UnityEngine;
public class DashingState : MovementState
{
    public DashingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    private float startTime;
    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = Context.MovementModel.Speed;
        Context.StatusController.DashController.Dash();
        startTime = Time.time;
    }
    public override void ExitState() { }
    public override void UpdateState() { }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (Time.time - startTime < Context.StatusController.Dashmodel.DashDuration) return StateKey;


        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (!IsMoving()) return MovementStateMachine.EMovementState.Idle;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;
        if (TriggeredSprint()) return MovementStateMachine.EMovementState.Running;
        if (TriggeredCrouch()) return MovementStateMachine.EMovementState.Crouching;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;


        if (!Context.AnimationModel.IsDashing)
            return MovementStateMachine.EMovementState.Idle;


        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}