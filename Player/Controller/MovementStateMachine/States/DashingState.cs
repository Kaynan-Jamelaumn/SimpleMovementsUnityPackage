using UnityEngine;

public class DashingState : MovementState
{
    public DashingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    public override void EnterState()
    {
        // Use SpeedManager for base speed
        Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.Speed;

        // Consume stamina
        if (Context.StatusController.DashModel.ShouldConsumeStamina)
        {
            Context.StatusController.StaminaManager.ConsumeStamina(Context.StatusController.DashModel.StaminaCost);
        }

        Context.AnimationModel.IsDashing = true;
        Context.StatusController.DashModel.LastActionTime = Context.SimTime;

        // The dash moves for DashDuration seconds of the movement clock (it used to be a coroutine, which a network
        // replay cannot re-run). Its first step happens right away, as the coroutine's did.
        Context.ActionElapsed = 0f;
        if (Context.ActionElapsed < Context.StatusController.DashModel.DashDuration)
            DashStep();
    }

    public override void ExitState()
    {
        Context.AnimationModel.IsDashing = false;
    }

    public override void UpdateState()
    {
        // Only apply gravity and basic physics here; the forward burst is DashStep
        Vector3 gravityMovement = new Vector3(0, Context.MovementModel.VerticalVelocity, 0) * Context.Dt;
        Context.Move(gravityMovement);

        if (Context.ActionElapsed < Context.StatusController.DashModel.DashDuration)
            DashStep();
    }

    private void DashStep()
    {
        Vector3 movement = Context.StatusController.DashModel.DashSpeed *
                           Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(
                               Context.StatusController.SpeedManager.Speed) *
                           Context.Dt *
                           Context.MovementController.PlayerForwardPosition();

        Context.Move(movement);
        Context.ActionElapsed += Context.Dt;
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        // Stay in dashing state until dash duration is complete
        if (Context.ActionElapsed < Context.StatusController.DashModel.DashDuration)
            return StateKey;

        // After dash is complete, transition based on input
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredSprint() && IsMoving()) return MovementStateMachine.EMovementState.Running;
        if (TriggeredCrouch() && IsMoving()) return MovementStateMachine.EMovementState.Crouching;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;

        return MovementStateMachine.EMovementState.Idle;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
