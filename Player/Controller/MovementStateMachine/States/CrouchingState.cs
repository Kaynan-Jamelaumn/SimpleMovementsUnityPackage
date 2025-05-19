using UnityEngine;

public class CrouchingState : MovementState
{
    public CrouchingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.MovementModel.CurrentSpeed = Context.MovementModel.Speed * Context.MovementModel.SpeedWhileCrouchingMultiplier;

        if (Context.MovementModel.ShouldConsumeStamina)
        {
            Context.MovementModel.CrouchCoroutine = Context.MovementModel.StartCoroutine(Context.StatusController.StaminaManager.ConsumeStaminaRoutine(Context.MovementModel.AmountOfCrouchStaminaCost, 0.8f));
            Context.StatusController.StaminaManager.IsConsuming = true;

        }

    }
    public override void ExitState()
    {

        if (Context.MovementModel.CrouchCoroutine != null)
        {
            Context.MovementController.StopCoroutine(Context.MovementModel.CrouchCoroutine);
            Context.MovementModel.CrouchCoroutine = null; // Reset coroutine reference
        }
        Context.StatusController.StaminaManager.IsConsuming = false;

    }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredSprint()) return MovementStateMachine.EMovementState.Running;

        if (Context.PlayerInput.Player.Movement.ReadValue<Vector2>() == Vector2.zero && !Context.AnimationModel.IsDashing && !Context.AnimationModel.IsRolling)
            return MovementStateMachine.EMovementState.Idle;
        if (Context.StatusController.StaminaManager.HasEnougCurrentValue(Context.MovementModel.AmountOfCrouchStaminaCost) == false || Context.PlayerInput.Player.Movement.ReadValue<Vector2>() != Vector2.zero && !Context.PlayerInput.Player.Crouch.IsPressed())
            return MovementStateMachine.EMovementState.Walking;


        return StateKey;
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}