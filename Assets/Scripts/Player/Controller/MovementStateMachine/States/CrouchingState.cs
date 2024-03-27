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
            Context.StatusController.StaminaManager.IsConsumingStamina = true;

        }

    }
    public override void ExitState()
    {

        if (Context.MovementModel.CrouchCoroutine != null) Context.MovementController.StopCoroutine(Context.MovementModel.CrouchCoroutine);
        Context.StatusController.StaminaManager.IsConsumingStamina = false;

    }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (Context.PlayerInput.Player.Jump.triggered)
            return MovementStateMachine.EMovementState.Jumping;
        if (!Context.AnimationModel.IsRolling && !Context.AnimationModel.IsDashing && Context.PlayerInput.Player.Dash.triggered && (!Context.MovementModel.ShouldConsumeStamina || Context.MovementModel.ShouldConsumeStamina && Context.StatusController.StaminaManager.HasEnoughStamina(Context.StatusController.Dashmodel.AmountOfDashStaminaCost)))
            return MovementStateMachine.EMovementState.Dashing;
        if (!Context.AnimationModel.IsDashing && !Context.AnimationModel.IsRolling && Context.PlayerInput.Player.Roll.triggered && (!Context.MovementModel.ShouldConsumeStamina || Context.MovementModel.ShouldConsumeStamina && Context.StatusController.StaminaManager.HasEnoughStamina(Context.StatusController.RollModel.AmountOfRollStaminaCost)))
            return MovementStateMachine.EMovementState.Rolling;
        if (Context.PlayerInput.Player.Movement.ReadValue<Vector2>() == Vector2.zero && !Context.AnimationModel.IsDashing && !Context.AnimationModel.IsRolling)
            return MovementStateMachine.EMovementState.Idle;
        if (Context.StatusController.StaminaManager.HasEnoughStamina(Context.MovementModel.AmountOfCrouchStaminaCost) == false || Context.PlayerInput.Player.Movement.ReadValue<Vector2>() != Vector2.zero && !Context.PlayerInput.Player.Crouch.IsPressed())
            return MovementStateMachine.EMovementState.Walking;
        

        return StateKey;
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}