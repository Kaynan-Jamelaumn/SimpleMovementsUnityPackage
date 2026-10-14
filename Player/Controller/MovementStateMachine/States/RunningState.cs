using UnityEngine;

public class RunningState : MovementState
{
    public RunningState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.GetSpeedWithMultiplier(
            Context.StatusController.SpeedManager.SpeedWhileRunningMultiplier);

        BeginStaminaDrain(Context.MovementModel.AmountOfSprintStaminaCost);
    }
    public override void ExitState()
    {
        EndStaminaDrain();
    }
    public override void UpdateState()
    {
        MovePlayer();
        TickStaminaDrain(Context.MovementModel.AmountOfSprintStaminaCost);
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredCrouch()) return MovementStateMachine.EMovementState.Crouching;

        if (!HasMoveInput())
            return MovementStateMachine.EMovementState.Idle;
        if (Context.StatusController.StaminaManager.HasEnougCurrentValue(Context.MovementModel.AmountOfSprintStaminaCost) == false || !Context.Input.Sprint)
           return MovementStateMachine.EMovementState.Walking;


        return StateKey;
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


}
