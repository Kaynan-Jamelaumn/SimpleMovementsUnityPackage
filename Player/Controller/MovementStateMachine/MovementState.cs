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
    protected bool TriggeredJump() => Context.PlayerInput.Player.Jump.triggered && HasStaminaForAction(Context.MovementModel.AmountOfJumpStaminaCost);
    protected bool TriggeredDash() => Context.PlayerInput.Player.Dash.triggered && HasStaminaForAction(Context.StatusController.DashModel.AmountOfDashStaminaCost);
    protected bool TriggeredRoll() => Context.PlayerInput.Player.Roll.triggered && HasStaminaForAction(Context.StatusController.RollModel.AmountOfRollStaminaCost);
    protected bool IsMoving() => Context.PlayerInput.Player.Movement.ReadValue<Vector2>().sqrMagnitude != 0;

    protected bool TriggeredSprint() => Context.PlayerInput.Player.Sprint.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfSprintStaminaCost);
    protected bool TriggeredCrouch() => Context.PlayerInput.Player.Crouch.IsPressed() && HasStaminaForAction(Context.MovementModel.AmountOfCrouchStaminaCost);
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

