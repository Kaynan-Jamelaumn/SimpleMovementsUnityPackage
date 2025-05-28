using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.SceneView;
public abstract class MovementState : BaseState<MovementStateMachine.EMovementState>
{
    protected MovementContext Context;
    protected float speedAfterWeightApplied;
    public MovementState(MovementContext context, MovementStateMachine.EMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }

    protected void MovePlayer() {


        Vector3 currentDirection = Context.MovementModel.Direction;
        RotatePlayerByCameraAngle(ref currentDirection);
        speedAfterWeightApplied = Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(Context.MovementModel.CurrentSpeed);
        Context.MovementModel.Controller.Move(speedAfterWeightApplied * currentDirection * Time.deltaTime);
    }
    protected bool HasStaminaForAction(float staminaCost)
    {
        return !Context.MovementModel.ShouldConsumeStamina || Context.StatusController.StaminaManager.HasEnougCurrentValue(staminaCost);
    }
    protected bool TriggeredJump() => Context.PlayerInput.Player.Jump.triggered && HasStaminaForAction(Context.MovementModel.AmountOfJumpStaminaCost) && Context.AvailabilityState.CanMove();
    protected bool TriggeredDash() => Context.PlayerInput.Player.Dash.triggered && Context.StatusController.DashController.Ready && Context.AvailabilityState.CanMove();
    protected bool TriggeredRoll() => Context.PlayerInput.Player.Roll.triggered && Context.StatusController.RollController.Ready && Context.AvailabilityState.CanMove();
    protected bool IsMoving() => Context.PlayerInput.Player.Movement.ReadValue<Vector2>().sqrMagnitude != 0 && Context.AvailabilityState.CanMove();

    protected bool TriggeredSprint() => Context.PlayerInput.Player.Sprint.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfSprintStaminaCost) && Context.AvailabilityState.CanMove();
    protected bool TriggeredCrouch() => Context.PlayerInput.Player.Crouch.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfCrouchStaminaCost) && Context.AvailabilityState.CanMove();
    protected void RotatePlayerByCameraAngle(ref Vector3 currentDirection)
    {
        if (Context.CameraModel.PlayerShouldRotateByCameraAngle || Context.CameraModel.IsFirstPerson)
        {
            currentDirection = Context.CameraController.DirectionToMoveByCamera(Context.MovementModel.Direction);
            Context.MovementModel.LastRotation = Context.MovementModel.PlayerTransform.rotation.eulerAngles.y;
        }
        else
        {
            currentDirection = Quaternion.AngleAxis(Context.MovementModel.LastRotation, Vector3.up) * currentDirection;
        }
    }

}

