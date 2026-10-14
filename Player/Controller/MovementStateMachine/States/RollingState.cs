using UnityEngine;

public class RollingState : MovementState
{
    public RollingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    public override void EnterState()
    {
        Context.AnimationModel.IsRolling = true;

        // Consume stamina if required
        if (Context.StatusController.RollModel.ShouldConsumeStamina)
        {
            Context.StatusController.StaminaManager.ConsumeStamina(Context.StatusController.RollModel.StaminaCost);
        }

        Context.StatusController.RollModel.LastActionTime = Context.SimTime;

        // The roll moves for RollDuration seconds of the movement clock (it used to be a coroutine, which a network
        // replay cannot re-run). Its first step happens right away, as the coroutine's did.
        Context.ActionElapsed = 0f;
        if (Context.ActionElapsed < Context.StatusController.RollModel.RollDuration)
            RollStep();
    }

    public override void ExitState()
    {
        Context.AnimationModel.IsRolling = false;
    }

    public override void UpdateState()
    {
        // Only apply gravity and basic physics here; the forward roll is RollStep
        Vector3 gravityMovement = new Vector3(0, Context.MovementModel.VerticalVelocity, 0) * Context.Dt;
        Context.Move(gravityMovement);

        if (Context.ActionElapsed < Context.StatusController.RollModel.RollDuration)
            RollStep();
    }

    private void RollStep()
    {
        Vector3 movement = Context.MovementController.PlayerForwardPosition() *
                           Context.StatusController.RollModel.RollSpeedModifier *
                           Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(
                               Context.StatusController.SpeedManager.Speed) *
                           Context.Dt;

        Context.Move(movement);
        Context.ActionElapsed += Context.Dt;
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        // Stay in rolling state until roll duration is complete
        if (Context.ActionElapsed < Context.StatusController.RollModel.RollDuration)
            return StateKey;

        // After roll is complete, transition based on input
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
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
