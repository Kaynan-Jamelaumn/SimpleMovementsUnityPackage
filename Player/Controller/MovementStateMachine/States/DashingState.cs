using System.Collections;
using UnityEngine;

public class DashingState : MovementState
{
    public DashingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    private float startTime;
    private Coroutine dashRoutine;

    public override void EnterState()
    {
        // Use SpeedManager for base speed
        Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.Speed;

        // Start dash routine and consume stamina
        if (Context.StatusController.DashModel.ShouldConsumeStamina)
        {
            Context.StatusController.StaminaManager.ConsumeStamina(Context.StatusController.DashModel.StaminaCost);
        }

        Context.AnimationModel.IsDashing = true;
        startTime = Time.time;
        Context.StatusController.DashModel.LastActionTime = Time.time;

        // Start the dash movement routine
        dashRoutine = Context.MovementModel.StartCoroutine(DashMovementRoutine());
    }

    public override void ExitState()
    {
        Context.AnimationModel.IsDashing = false;

        if (dashRoutine != null)
        {
            Context.MovementModel.StopCoroutine(dashRoutine);
            dashRoutine = null;
        }
    }

    public override void UpdateState()
    {
        // Movement is handled by the dash routine
        // Only apply gravity and basic physics here if needed
        Vector3 gravityMovement = new Vector3(0, Context.MovementModel.VerticalVelocity, 0) * Time.deltaTime;
        Context.MovementModel.Controller.Move(gravityMovement);
    }

    private IEnumerator DashMovementRoutine()
    {
        float elapsed = 0f;
        while (elapsed < Context.StatusController.DashModel.DashDuration)
        {
            Vector3 movement = Context.StatusController.DashModel.DashSpeed *
                             Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(
                                 Context.StatusController.SpeedManager.Speed) *
                             Time.deltaTime *
                             Context.MovementController.PlayerForwardPosition();

            Context.MovementModel.Controller.Move(movement);
            elapsed += Time.deltaTime;
            yield return null;
        }

        dashRoutine = null;
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        // Stay in dashing state until dash duration is complete
        if (Time.time - startTime < Context.StatusController.DashModel.DashDuration && dashRoutine != null)
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