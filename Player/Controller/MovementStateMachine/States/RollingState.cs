using System.Collections;
using UnityEngine;

public class RollingState : MovementState
{
    public RollingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    private float startTime;
    private Coroutine rollRoutine;

    public override void EnterState()
    {
        Context.AnimationModel.IsRolling = true;

        // Consume stamina if required
        if (Context.StatusController.RollModel.ShouldConsumeStamina)
        {
            Context.StatusController.StaminaManager.ConsumeStamina(Context.StatusController.RollModel.StaminaCost);
        }

        startTime = Time.time;
        Context.StatusController.RollModel.LastActionTime = Time.time;

        // Start the roll movement routine
        rollRoutine = Context.MovementModel.StartCoroutine(RollMovementRoutine());
    }

    public override void ExitState()
    {
        Context.AnimationModel.IsRolling = false;

        if (rollRoutine != null)
        {
            Context.MovementModel.StopCoroutine(rollRoutine);
            rollRoutine = null;
        }
    }

    public override void UpdateState()
    {
        // Movement is handled by the roll routine
        // Only apply gravity and basic physics here if needed
        Vector3 gravityMovement = new Vector3(0, Context.MovementModel.VerticalVelocity, 0) * Time.deltaTime;
        Context.MovementModel.Controller.Move(gravityMovement);
    }

    private IEnumerator RollMovementRoutine()
    {
        float elapsed = 0f;
        while (elapsed < Context.StatusController.RollModel.RollDuration)
        {
            Vector3 movement = Context.MovementController.PlayerForwardPosition() *
                             Context.StatusController.RollModel.RollSpeedModifier *
                             Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(
                                 Context.StatusController.SpeedManager.Speed) *
                             Time.deltaTime;

            Context.MovementModel.Controller.Move(movement);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rollRoutine = null;
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        // Stay in rolling state until roll duration is complete
        if (Time.time - startTime < Context.StatusController.RollModel.RollDuration && rollRoutine != null)
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