using System;
using System.Collections.Generic;
using UnityEngine;
public abstract class MovementState : BaseState<MovementStateMachine.EMovementState>
{
    protected MovementContext Context;
    public MovementState(MovementContext context, MovementStateMachine.EMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }
    protected bool HasStaminaForAction(float staminaCost)
    {
        return !Context.MovementModel.ShouldConsumeStamina || Context.StatusController.StaminaManager.HasEnoughStamina(staminaCost);
    }
    protected bool TriggeredJump() => Context.PlayerInput.Player.Jump.triggered && HasStaminaForAction(Context.MovementModel.AmountOfJumpStaminaCost);
    protected bool TriggeredDash() => Context.PlayerInput.Player.Dash.triggered && HasStaminaForAction(Context.StatusController.Dashmodel.AmountOfDashStaminaCost);
    protected bool TriggeredRoll() => Context.PlayerInput.Player.Roll.triggered && HasStaminaForAction(Context.StatusController.RollModel.AmountOfRollStaminaCost);
    protected bool IsIdle() => Context.PlayerInput.Player.Movement.ReadValue<Vector2>() == Vector2.zero;
    protected bool IsWalking() => Context.PlayerInput.Player.Movement.ReadValue<Vector2>().sqrMagnitude != 0;

    protected bool TriggeredSprint() => Context.PlayerInput.Player.Sprint.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfSprintStaminaCost);
    protected bool TriggeredCrouch() => Context.PlayerInput.Player.Crouch.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfCrouchStaminaCost);
}

