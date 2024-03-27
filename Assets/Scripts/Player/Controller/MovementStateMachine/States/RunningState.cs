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

        if (Context.MovementModel.SprintCoroutine != null) Context.MovementController.StopCoroutine(Context.MovementModel.SprintCoroutine);
        Context.StatusController.StaminaManager.IsConsumingStamina = false;

    }
    public override void UpdateState()
    {
        // GetNextState();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (Context.PlayerInput.Player.Jump.triggered && HasStaminaForAction(Context.MovementModel.AmountOfJumpStaminaCost))
            return MovementStateMachine.EMovementState.Jumping;

        if (Context.PlayerInput.Player.Dash.triggered && HasStaminaForAction(Context.StatusController.Dashmodel.AmountOfDashStaminaCost))
            return MovementStateMachine.EMovementState.Dashing;

        if (Context.PlayerInput.Player.Roll.triggered && HasStaminaForAction(Context.StatusController.RollModel.AmountOfRollStaminaCost))
            return MovementStateMachine.EMovementState.Rolling; ;


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