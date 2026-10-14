using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base of the player's movement states. States read the step's input, length and clock from the
/// <see cref="MovementContext"/> (never the devices, Time or the camera), so the same step gives the same result on
/// the server, on the owner's machine and when the owner replays it after a correction.
/// </summary>
public abstract class MovementState : BaseState<MovementStateMachine.EMovementState>
{
    protected MovementContext Context;
    protected float speedAfterWeightApplied;

    public MovementState(MovementContext context, MovementStateMachine.EMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }

    protected void MovePlayer()
    {
        Vector3 currentDirection = Context.MovementModel.Direction;
        RotatePlayerByCameraAngle(ref currentDirection);
        speedAfterWeightApplied = Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(Context.MovementModel.CurrentSpeed);
        Context.Move(speedAfterWeightApplied * currentDirection * Context.Dt);
    }

    protected bool HasStaminaForAction(float staminaCost)
    {
        return !Context.MovementModel.ShouldConsumeStamina || Context.StatusController.StaminaManager.HasEnougCurrentValue(staminaCost);
    }

    // Buffered: the press is remembered for a moment (it used to count only on its exact frame, so presses right
    // before or while landing were lost).
    protected bool TriggeredJump() => Context.JumpPressBuffered && HasStaminaForAction(Context.MovementModel.AmountOfJumpStaminaCost) && Context.AvailabilityState.CanMove();
    protected bool TriggeredDash() => Context.Input.DashPressed && CanDash() && Context.AvailabilityState.CanMove();
    protected bool TriggeredRoll() => Context.Input.RollPressed && CanRoll() && Context.AvailabilityState.CanMove();
    protected bool IsMoving() => HasMoveInput() && Context.AvailabilityState.CanMove();

    protected bool TriggeredSprint() => Context.Input.Sprint && HasStaminaForAction(Context.MovementModel.AmountOfSprintStaminaCost) && Context.AvailabilityState.CanMove();
    protected bool TriggeredCrouch() => Context.Input.Crouch && HasStaminaForAction(Context.MovementModel.AmountOfCrouchStaminaCost) && Context.AvailabilityState.CanMove();

    /// <summary>The move stick / keys are pushed this step.</summary>
    protected bool HasMoveInput() => Context.Input.Move.sqrMagnitude != 0f;

    // Helper methods for speed calculations using SpeedManager
    protected float GetBaseSpeed() => Context.StatusController.SpeedManager.Speed;
    protected float GetRunningSpeed() => Context.StatusController.SpeedManager.GetSpeedWithMultiplier(Context.StatusController.SpeedManager.SpeedWhileRunningMultiplier);
    protected float GetCrouchingSpeed() => Context.StatusController.SpeedManager.GetSpeedWithMultiplier(Context.StatusController.SpeedManager.SpeedWhileCrouchingMultiplier);

    // Helper methods for action availability (replacing controller dependencies)
    protected bool CanDash()
    {
        return Context.SimTime - Context.StatusController.DashModel.LastActionTime > Context.StatusController.DashModel.CooldownDuration &&
               (!Context.StatusController.DashModel.ShouldConsumeStamina ||
                Context.StatusController.StaminaManager.HasEnougCurrentValue(Context.StatusController.DashModel.StaminaCost));
    }

    protected bool CanRoll()
    {
        return Context.SimTime - Context.StatusController.RollModel.LastActionTime > Context.StatusController.RollModel.CooldownDuration &&
               (!Context.StatusController.RollModel.ShouldConsumeStamina ||
                Context.StatusController.StaminaManager.HasEnougCurrentValue(Context.StatusController.RollModel.StaminaCost));
    }

    protected void RotatePlayerByCameraAngle(ref Vector3 currentDirection)
    {
        // The camera's yaw comes with the input: the server has no camera, and a replay must use the yaw of its tick.
        if (Context.Input.RotateWithCamera)
        {
            currentDirection = Quaternion.AngleAxis(Context.Input.CameraYaw, Vector3.up) * Context.MovementModel.Direction;
            Context.MovementModel.LastRotation = Context.MovementModel.PlayerTransform.rotation.eulerAngles.y;
        }
        else
        {
            currentDirection = Quaternion.AngleAxis(Context.MovementModel.LastRotation, Vector3.up) * currentDirection;
        }
    }

    // ------------------------------------------------------------------ stamina spent over time (sprint, crouch)
    // These replace StaminaManager.ConsumeStaminaRoutine (a WaitForSeconds coroutine, which cannot be replayed): the
    // first cost is paid on entering, then one every StaminaDrainInterval seconds of the state, until stamina is gone.

    /// <summary>Starts spending <paramref name="cost"/> stamina over time (when the movement model consumes stamina).</summary>
    protected void BeginStaminaDrain(float cost)
    {
        StaminaManager stamina = Context.StatusController.StaminaManager;
        if (!Context.MovementModel.ShouldConsumeStamina || stamina == null)
        {
            Context.StaminaDrainTimer = MovementSnapshot.None;
            return;
        }
        stamina.IsConsuming = true; // blocks regeneration while sprinting / crouching
        Context.StaminaDrainTimer = MovementContext.StaminaDrainInterval;
        DrainStamina(stamina, cost);
    }

    /// <summary>Advances the drain timer by the step and pays the cost when it runs out.</summary>
    protected void TickStaminaDrain(float cost)
    {
        if (Context.StaminaDrainTimer < 0f)
            return;
        StaminaManager stamina = Context.StatusController.StaminaManager;
        if (stamina == null)
            return;
        Context.StaminaDrainTimer -= Context.Dt;
        if (Context.StaminaDrainTimer > 0f)
            return;
        Context.StaminaDrainTimer = MovementContext.StaminaDrainInterval;
        DrainStamina(stamina, cost);
    }

    /// <summary>Stops spending stamina over time (regeneration may start again).</summary>
    protected void EndStaminaDrain()
    {
        Context.StaminaDrainTimer = MovementSnapshot.None;
        if (Context.StatusController.StaminaManager != null)
            Context.StatusController.StaminaManager.IsConsuming = false;
    }

    /// <summary>
    /// Stamina spent over time at a cost that may change every step (swimming: stroke, fast stroke, treading water).
    /// A cost of 0 spends nothing and lets stamina regenerate; once there is a cost, it is paid every
    /// <see cref="MovementContext.StaminaDrainInterval"/> seconds (the first one after a full interval).
    /// </summary>
    protected void TickVariableStaminaDrain(float cost)
    {
        StaminaManager stamina = Context.StatusController.StaminaManager;
        if (!Context.MovementModel.ShouldConsumeStamina || stamina == null)
        {
            Context.StaminaDrainTimer = MovementSnapshot.None;
            return;
        }
        if (cost <= 0f || stamina.CurrentValue <= 0f)
        {
            if (Context.StaminaDrainTimer >= 0f || stamina.IsConsuming)
                EndStaminaDrain();
            return;
        }
        if (Context.StaminaDrainTimer < 0f)
            Context.StaminaDrainTimer = MovementContext.StaminaDrainInterval;
        stamina.IsConsuming = true;
        Context.StaminaDrainTimer -= Context.Dt;
        if (Context.StaminaDrainTimer > 0f)
            return;
        Context.StaminaDrainTimer = MovementContext.StaminaDrainInterval;
        DrainStamina(stamina, cost);
    }

    private void DrainStamina(StaminaManager stamina, float cost)
    {
        stamina.ConsumeStamina(cost);
        stamina.UpdateStaminaBar(); // also keeps the value within 0..max, as the old routine did after every cost
        if (stamina.CurrentValue <= 0f)
        {
            // Out of stamina: the old routine ended here and let regeneration start.
            Context.StaminaDrainTimer = MovementSnapshot.None;
            stamina.IsConsuming = false;
        }
    }
}
