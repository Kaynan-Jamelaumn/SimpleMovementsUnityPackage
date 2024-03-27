using UnityEngine;

public class IdleState : MovementState
{
    public IdleState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    public override void EnterState() 
    {
        Context.MovementModel.CurrentSpeed = 0;
    }
    public override void ExitState() { }
    public override void UpdateState() {
        //GetNextState();
    }
    public override MovementStateMachine.EMovementState GetNextState()
    {
        
        if (Context.PlayerInput.Player.Jump.triggered)
            return MovementStateMachine.EMovementState.Jumping;  
        if (!Context.AnimationModel.IsRolling && !Context.AnimationModel.IsDashing && Context.PlayerInput.Player.Dash.triggered && (!Context.MovementModel.ShouldConsumeStamina || Context.MovementModel.ShouldConsumeStamina && Context.StatusController.StaminaManager.HasEnoughStamina(Context.StatusController.Dashmodel.AmountOfDashStaminaCost)))
            return MovementStateMachine.EMovementState.Dashing;
        if (!Context.AnimationModel.IsDashing && !Context.AnimationModel.IsRolling && Context.PlayerInput.Player.Roll.triggered && (!Context.MovementModel.ShouldConsumeStamina || Context.MovementModel.ShouldConsumeStamina && Context.StatusController.StaminaManager.HasEnoughStamina(Context.StatusController.RollModel.AmountOfRollStaminaCost)))
            return MovementStateMachine.EMovementState.Rolling;
        if (Context.PlayerInput.Player.Movement.ReadValue<Vector2>().sqrMagnitude != 0)
            return MovementStateMachine.EMovementState.Walking;
        
        return StateKey;
        
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}