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
            // Swimming (optional Animator parameters): in deep water, head under the surface, up/down speed (m/s).
            ["IsSwimming"] = state == MovementStateMachine.EMovementState.Swimming,
            ["IsUnderwater"] = state == MovementStateMachine.EMovementState.Swimming && movementModel.HeadUnderwater,
            ["SwimVertical"] = state == MovementStateMachine.EMovementState.Swimming ? movementModel.VerticalVelocity : 0f,
            // Angle of the swim (degrees: + = rising, - = diving) and its speed (m/s), for a swim blend tree.
            ["SwimPitch"] = state == MovementStateMachine.EMovementState.Swimming ? SwimPitch(movementStateMachine.SwimVelocity) : 0f,
            ["SwimSpeed"] = state == MovementStateMachine.EMovementState.Swimming ? movementStateMachine.SwimVelocity.magnitude : 0f,
            ["IsAttacking"] = model.IsAttacking,
            ["IsInputLocked"] = model.IsInputLocked
        });
    }

    private static float SwimPitch(Vector3 v)
    {
        float flat = new Vector2(v.x, v.z).magnitude;
        return flat < 0.05f && Mathf.Abs(v.y) < 0.05f ? 0f : Mathf.Atan2(v.y, flat) * Mathf.Rad2Deg;
    }

    private void SetAnimationParameters(Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
            model.SetParameterSafe(kvp.Key, kvp.Value);
    }
}