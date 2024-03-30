using UnityEngine;

public class RunningState : MovementState
{
    public RunningState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = Context.MovementModel.Speed * Context.MovementModel.SpeedWhileRunningMultiplier;

        if (Context.MovementModel.ShouldConsumeStamina)
        {
            Context.MovementModel.SprintCoroutine = Context.MovementModel.StartCoroutine(Context.StatusController.StaminaManager.ConsumeStaminaRoutine(Context.MovementModel.AmountOfSprintStaminaCost, 0.8f));
            Context.StatusController.StaminaManager.IsConsumingStamina = true;

        }

    }
    public override void ExitState()
    {

        if (Context.MovementModel.SprintCoroutine != null)
        {
            Context.MovementModel.StopCoroutine(Context.MovementModel.SprintCoroutine);
            Context.MovementModel.SprintCoroutine = null; // Reset coroutine reference
        }
        Context.StatusController.StaminaManager.IsConsumingStamina = false;

    }
    public override void UpdateState()
    {
        // GetNextState();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredCrouch()) return MovementStateMachine.EMovementState.Crouching;

        if (Context.PlayerInput.Player.Movement.ReadValue<Vector2>() == Vector2.zero && !Context.AnimationModel.IsDashing && !Context.AnimationModel.IsRolling)
            return MovementStateMachine.EMovementState.Idle;
        if (Context.StatusController.StaminaManager.HasEnoughStamina(Context.MovementModel.AmountOfSprintStaminaCost) == false || Context.PlayerInput.Player.Movement.ReadValue<Vector2>() != Vector2.zero && !Context.PlayerInput.Player.Sprint.IsPressed())
           return MovementStateMachine.EMovementState.Walking;


        return StateKey;
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}