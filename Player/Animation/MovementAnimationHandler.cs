using System.Collections.Generic;
using UnityEngine;

public class MovementAnimationHandler
{
    private readonly PlayerAnimationModel model;
    private readonly PlayerMovementModel movementModel;
    private readonly MovementStateMachine movementStateMachine;

    private readonly float smoothTime;
    private readonly bool useAdvancedBlending;

    private Vector2 currentMovement, movementVelocity;

    public MovementAnimationHandler(PlayerAnimationModel model, PlayerMovementModel movementModel,
        MovementStateMachine movementStateMachine, float smoothTime, bool useAdvancedBlending)
    {
        this.model = model;
        this.movementModel = movementModel;
        this.movementStateMachine = movementStateMachine;
        this.smoothTime = smoothTime;
        this.useAdvancedBlending = useAdvancedBlending;
    }

    public void ApplyMovementAnimations()
    {
        var movement = movementModel.Movement2D;

        if (useAdvancedBlending)
        {
            currentMovement.x = Mathf.SmoothDamp(currentMovement.x, movement.x, ref movementVelocity.x, smoothTime);
            currentMovement.y = Mathf.SmoothDamp(currentMovement.y, movement.y, ref movementVelocity.y, smoothTime);
        }
        else
        {
            currentMovement = movement;
        }

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["MovementX"] = currentMovement.x,
            ["MovementZ"] = currentMovement.y,
            ["MovementSpeed"] = currentMovement.magnitude
        });
    }

    public void ApplyStateAnimations()
    {
        if (movementStateMachine?.CurrentState == null) return;

        var state = movementStateMachine.CurrentState.StateKey;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsWalking"] = state == MovementStateMachine.EMovementState.Walking,
            ["IsRunning"] = state == MovementStateMachine.EMovementState.Running,
            ["IsCrouching"] = state == MovementStateMachine.EMovementState.Crouching,
            ["IsJumping"] = state == MovementStateMachine.EMovementState.Jumping,
            ["IsRolling"] = state == MovementStateMachine.EMovementState.Rolling || model.IsRolling,
            ["IsDashing"] = state == MovementStateMachine.EMovementState.Dashing || model.IsDashing,
            ["IsAttacking"] = model.IsAttacking,
            ["IsInputLocked"] = model.IsInputLocked
        });
    }

    private void SetAnimationParameters(Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
            model.SetParameterSafe(kvp.Key, kvp.Value);
    }
}