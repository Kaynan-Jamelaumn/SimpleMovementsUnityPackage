using UnityEngine;

public class CrouchingState : MovementState
{
    public CrouchingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.GetSpeedWithMultiplier(
            Context.StatusController.SpeedManager.SpeedWhileCrouchingMultiplier);

        BeginStaminaDrain(Context.MovementModel.AmountOfCrouchStaminaCost);
    }

    public override void ExitState()
    {
        EndStaminaDrain();
    }

    public override void UpdateState()
    {
        MovePlayer();
        TickStaminaDrain(Context.MovementModel.AmountOfCrouchStaminaCost);
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredSprint()) return MovementStateMachine.EMovementState.Running;

        if (!HasMoveInput())
            return MovementStateMachine.EMovementState.Idle;

        if (Context.StatusController.StaminaManager.HasEnougCurrentValue(Context.MovementModel.AmountOfCrouchStaminaCost) == false ||
            !Context.Input.Crouch)
            return MovementStateMachine.EMovementState.Walking;

        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
