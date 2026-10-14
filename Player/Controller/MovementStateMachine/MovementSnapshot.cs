using System;
using UnityEngine;

/// <summary>
/// Everything the player's movement needs to continue exactly from one moment: position and facing, vertical speed,
/// state and time in it, speed, timers (jump buffer, dash and roll cooldowns, dash/roll progress, stamina drain and
/// regeneration), stamina, gravity suspension, the control lock, the teleport count and the movement traits' runtime
/// state. Taken with
/// <see cref="MovementStateMachine.Capture"/> and put back with <see cref="MovementStateMachine.Restore"/>; the
/// network layer sends it from the server to correct the owner's prediction.
/// </summary>
/// <remarks>
/// Timers are stored as time LEFT (or elapsed), never as absolute times, so a snapshot means the same on any machine.
/// "Nothing pending" is <see cref="None"/> (-1).
/// </remarks>
[Serializable]
public struct MovementSnapshot
{
    /// <summary>Value of a timer that is not running (no buffered jump, cooldown over).</summary>
    public const float None = -1f;

    /// <summary>World position of the CharacterController's object.</summary>
    public Vector3 Position;
    /// <summary>World yaw of the CharacterController's object.</summary>
    public float RootYaw;
    /// <summary>World yaw of the body (PlayerMovementModel.PlayerTransform; the dash/roll direction).</summary>
    public float BodyYaw;
    /// <summary>Vertical speed (m/s).</summary>
    public float VerticalVelocity;
    /// <summary>Current movement state.</summary>
    public MovementStateMachine.EMovementState State;
    /// <summary>State before the last transition (a jump from Running is a running jump).</summary>
    public MovementStateMachine.EMovementState PreviousState;
    /// <summary>Seconds spent in <see cref="State"/>.</summary>
    public float StateTime;
    /// <summary>The movement clock (seconds simulated so far).</summary>
    public float SimTime;
    /// <summary>Horizontal speed chosen when the state was entered.</summary>
    public float CurrentSpeed;
    /// <summary>Yaw used for the move direction when not following the camera.</summary>
    public float LastRotation;
    /// <summary>Seconds a buffered jump press is still remembered, or <see cref="None"/>.</summary>
    public float JumpBufferLeft;
    /// <summary>Seconds until a dash is allowed again, or <see cref="None"/> when it is ready.</summary>
    public float DashCooldownLeft;
    /// <summary>Seconds until a roll is allowed again, or <see cref="None"/> when it is ready.</summary>
    public float RollCooldownLeft;
    /// <summary>Seconds the current dash or roll has been moving.</summary>
    public float ActionElapsed;
    /// <summary>Current stamina.</summary>
    public float Stamina;
    /// <summary>Seconds until sprinting or crouching spends stamina again, or <see cref="None"/>.</summary>
    public float StaminaDrainTimer;
    /// <summary>Stamina is being spent over time (blocks regeneration).</summary>
    public bool StaminaConsuming;
    /// <summary>Stamina regeneration is running.</summary>
    public bool StaminaRegenerating;
    /// <summary>Seconds until the next regeneration tick.</summary>
    public float StaminaRegenTimer;
    /// <summary>A trait (wall climb) holds the player against gravity.</summary>
    public bool SuspendGravity;
    /// <summary>Controls are locked (travel, dungeon transitions).</summary>
    public bool Frozen;
    /// <summary>Air jumps used since the last landing (Double Jump trait).</summary>
    public byte AirJumpsUsed;
    /// <summary>Climbing a wall (Wall Climb trait).</summary>
    public bool Climbing;
    /// <summary>Seconds climbed since the last landing (Wall Climb trait).</summary>
    public float ClimbTime;
    /// <summary>Velocity while swimming (m/s, world space): the swimmer glides on and slows down in the water.</summary>
    public Vector3 SwimVelocity;
    /// <summary>
    /// How many teleports the player has had (wraps at 255): when it changes, the player was moved somewhere else in
    /// one go and its smoothed model must snap there (see MovementStateMachine.TeleportCount).
    /// </summary>
    public byte Teleports;

    /// <summary>The bools above packed into one byte (network form).</summary>
    [Flags]
    public enum Flags : byte
    {
        None = 0,
        StaminaConsuming = 1 << 0,
        StaminaRegenerating = 1 << 1,
        SuspendGravity = 1 << 2,
        Frozen = 1 << 3,
        Climbing = 1 << 4,
    }

    /// <summary>Packs the bools into a byte.</summary>
    public byte PackFlags()
    {
        Flags f = Flags.None;
        if (StaminaConsuming) f |= Flags.StaminaConsuming;
        if (StaminaRegenerating) f |= Flags.StaminaRegenerating;
        if (SuspendGravity) f |= Flags.SuspendGravity;
        if (Frozen) f |= Flags.Frozen;
        if (Climbing) f |= Flags.Climbing;
        return (byte)f;
    }

    /// <summary>Sets the bools from a byte made by <see cref="PackFlags"/>.</summary>
    public void UnpackFlags(byte packed)
    {
        var f = (Flags)packed;
        StaminaConsuming = (f & Flags.StaminaConsuming) != 0;
        StaminaRegenerating = (f & Flags.StaminaRegenerating) != 0;
        SuspendGravity = (f & Flags.SuspendGravity) != 0;
        Frozen = (f & Flags.Frozen) != 0;
        Climbing = (f & Flags.Climbing) != 0;
    }

    /// <summary>
    /// Time left of a window that started at <paramref name="since"/> and lasts <paramref name="window"/> seconds, at
    /// time <paramref name="now"/>: <see cref="None"/> once more than <paramref name="window"/> seconds have passed
    /// (or when it never started), else window - elapsed (0 exactly at the end of the window, which still counts as
    /// inside it).
    /// </summary>
    public static float Remaining(float now, float since, float window)
    {
        if (float.IsNegativeInfinity(since) || float.IsNaN(since))
            return None;
        float elapsed = now - since;
        if (elapsed > window)
            return None;
        return Mathf.Max(0f, window - elapsed);
    }

    /// <summary>
    /// The start time that gives <paramref name="remaining"/> seconds left of a <paramref name="window"/> at
    /// <paramref name="now"/> (the inverse of <see cref="Remaining"/>); negative infinity for <see cref="None"/>.
    /// </summary>
    public static float Since(float now, float remaining, float window)
    {
        if (remaining < 0f || float.IsNaN(remaining))
            return float.NegativeInfinity;
        return now - (window - Mathf.Min(remaining, window));
    }

    public override string ToString() =>
        $"{State} (from {PreviousState}, {StateTime:0.00}s) at {Position} yaw {BodyYaw:0} vv {VerticalVelocity:0.00} stamina {Stamina:0.#}{(Frozen ? " frozen" : "")}";
}
