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
    private CombatEntity body;
    private bool bodySearched;

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

        model.SetParameterSafe("MovementX", currentMovement.x);
        model.SetParameterSafe("MovementZ", currentMovement.y);
        model.SetParameterSafe("MovementSpeed", currentMovement.magnitude);
    }

    public void ApplyStateAnimations()
    {
        if (movementStateMachine?.CurrentState == null) return;

        var state = movementStateMachine.CurrentState.StateKey;

        // Set one by one (same parameters, same order as before): a dictionary built every frame was garbage to collect.
        bool swimming = state == MovementStateMachine.EMovementState.Swimming;
        model.SetParameterSafe("IsWalking", state == MovementStateMachine.EMovementState.Walking);
        model.SetParameterSafe("IsRunning", state == MovementStateMachine.EMovementState.Running);
        model.SetParameterSafe("IsCrouching", state == MovementStateMachine.EMovementState.Crouching);
        model.SetParameterSafe("IsJumping", state == MovementStateMachine.EMovementState.Jumping);
        model.SetParameterSafe("IsRolling", state == MovementStateMachine.EMovementState.Rolling || model.IsRolling);
        model.SetParameterSafe("IsDashing", state == MovementStateMachine.EMovementState.Dashing || model.IsDashing);
        // Swimming (optional Animator parameters): in deep water, head under the surface, up/down speed (m/s).
        model.SetParameterSafe("IsSwimming", swimming);
        model.SetParameterSafe("IsUnderwater", swimming && movementModel.HeadUnderwater);
        model.SetParameterSafe("SwimVertical", swimming ? movementModel.VerticalVelocity : 0f);
        // Angle of the swim (degrees: + = rising, - = diving) and its speed (m/s), for a swim blend tree.
        model.SetParameterSafe("SwimPitch", swimming ? SwimPitch(movementStateMachine.SwimVelocity) : 0f);
        model.SetParameterSafe("SwimSpeed", swimming ? movementStateMachine.SwimVelocity.magnitude : 0f);
        model.SetParameterSafe("IsAttacking", model.IsAttacking);
        model.SetParameterSafe("IsInputLocked", model.IsInputLocked);
        ApplyBodyAnimations();
    }

    /// <summary>
    /// How the legs let the player move (injury system, optional Animator parameters): limping, hopping on one leg,
    /// crawling without legs, and what is missing - for limp / hop / crawl / one-armed animations.
    /// </summary>
    private void ApplyBodyAnimations()
    {
        if (body == null && !bodySearched && movementModel != null)
        {
            bodySearched = true;
            body = CombatEntity.Resolve(movementModel.gameObject);
        }
        BodyLocomotion l = body != null ? body.Locomotion : BodyLocomotion.Normal;
        float time = movementStateMachine != null ? movementStateMachine.SimTime : Time.time;
        model.SetParameterSafe("LocomotionMode", (int)l.mode);
        model.SetParameterSafe("IsLimping", l.mode == LocomotionMode.Limping);
        model.SetParameterSafe("LimpAmount", l.mode == LocomotionMode.Normal ? 0f : l.limp);
        model.SetParameterSafe("LimpSide", l.limpSide);
        model.SetParameterSafe("IsHopping", l.mode == LocomotionMode.Hopping);
        model.SetParameterSafe("HopCycle", l.mode == LocomotionMode.Hopping ? l.HopPhase(time) : 0f);
        model.SetParameterSafe("IsCrawling", l.mode == LocomotionMode.Crawling);
        model.SetParameterSafe("IsLegless", l.missingLegs >= 2);
        model.SetParameterSafe("MissingLegs", l.missingLegs);
        model.SetParameterSafe("MissingArms", l.missingArms);
    }

    private static float SwimPitch(Vector3 v)
    {
        float flat = new Vector2(v.x, v.z).magnitude;
        return flat < 0.05f && Mathf.Abs(v.y) < 0.05f ? 0f : Mathf.Atan2(v.y, flat) * Mathf.Rad2Deg;
    }
}