using System;
using UnityEngine;

/// <summary>
/// The buttons of one <see cref="MovementInput"/> packed into a byte (the network layer sends this instead of seven
/// bools). The values never change: they are part of the data exchanged between client and server.
/// </summary>
[Flags]
public enum MovementButtons : byte
{
    None = 0,
    Sprint = 1 << 0,
    Crouch = 1 << 1,
    JumpPressed = 1 << 2,
    JumpHeld = 1 << 3,
    DashPressed = 1 << 4,
    RollPressed = 1 << 5,
    RotateWithCamera = 1 << 6,

    /// <summary>The one-frame presses (a new press of Jump, Dash or Roll).</summary>
    Edges = JumpPressed | DashPressed | RollPressed,

    /// <summary>Every button that exists; anything else in a received byte is ignored.</summary>
    All = Sprint | Crouch | JumpPressed | JumpHeld | DashPressed | RollPressed | RotateWithCamera,
}

/// <summary>
/// What the player asks the movement to do for one step: one frame in single player, one network tick in a network
/// session. It is built from the devices on the machine that controls the player
/// (<see cref="MovementStateMachine.ReadDeviceInput"/>) and consumed by <see cref="MovementStateMachine.Simulate(in MovementInput, float)"/>;
/// with Fish-Net it is what the owning client sends to the server every tick. Everything else the movement needs
/// (speeds, stamina, stun, position) comes from the authoritative state, never from the input.
/// </summary>
[Serializable]
public struct MovementInput
{
    /// <summary>Move stick / WASD (x = right, y = forward), at most 1 long after <see cref="Sanitized"/>.</summary>
    public Vector2 Move;
    /// <summary>Sprint is held.</summary>
    public bool Sprint;
    /// <summary>Crouch is held.</summary>
    public bool Crouch;
    /// <summary>Jump was pressed during this step (a new press, remembered by the jump buffer).</summary>
    public bool JumpPressed;
    /// <summary>Jump is held (glide, wall climb).</summary>
    public bool JumpHeld;
    /// <summary>Dash was pressed during this step.</summary>
    public bool DashPressed;
    /// <summary>Roll was pressed during this step.</summary>
    public bool RollPressed;
    /// <summary>The movement follows the camera (first person, or right mouse held in third person).</summary>
    public bool RotateWithCamera;
    /// <summary>Yaw of the active camera in degrees, used when <see cref="RotateWithCamera"/> is on.</summary>
    public float CameraYaw;
    /// <summary>
    /// Pitch of the active camera in degrees (-89 = looking straight up, 89 = straight down, Unity's convention). Only
    /// swimming uses it: underwater the player swims where the camera looks.
    /// </summary>
    public float CameraPitch;

    /// <summary>True when a one-step press (Jump, Dash or Roll) is part of this input.</summary>
    public bool HasEdges => JumpPressed || DashPressed || RollPressed;

    /// <summary>The buttons packed into a byte.</summary>
    public byte PackButtons()
    {
        MovementButtons b = MovementButtons.None;
        if (Sprint) b |= MovementButtons.Sprint;
        if (Crouch) b |= MovementButtons.Crouch;
        if (JumpPressed) b |= MovementButtons.JumpPressed;
        if (JumpHeld) b |= MovementButtons.JumpHeld;
        if (DashPressed) b |= MovementButtons.DashPressed;
        if (RollPressed) b |= MovementButtons.RollPressed;
        if (RotateWithCamera) b |= MovementButtons.RotateWithCamera;
        return (byte)b;
    }

    /// <summary>Rebuilds an input from its packed form (unknown bits are ignored).</summary>
    public static MovementInput FromPacked(Vector2 move, byte buttons, float cameraYaw) => FromPacked(move, buttons, cameraYaw, 0f);

    /// <summary>Rebuilds an input from its packed form, with the camera's pitch (unknown bits are ignored).</summary>
    public static MovementInput FromPacked(Vector2 move, byte buttons, float cameraYaw, float cameraPitch)
    {
        var b = (MovementButtons)buttons & MovementButtons.All;
        return new MovementInput
        {
            Move = move,
            Sprint = (b & MovementButtons.Sprint) != 0,
            Crouch = (b & MovementButtons.Crouch) != 0,
            JumpPressed = (b & MovementButtons.JumpPressed) != 0,
            JumpHeld = (b & MovementButtons.JumpHeld) != 0,
            DashPressed = (b & MovementButtons.DashPressed) != 0,
            RollPressed = (b & MovementButtons.RollPressed) != 0,
            RotateWithCamera = (b & MovementButtons.RotateWithCamera) != 0,
            CameraYaw = cameraYaw,
            CameraPitch = cameraPitch,
        };
    }

    /// <summary>Removes the one-step presses from a packed byte (used when an input is repeated for a guessed tick).</summary>
    public static byte WithoutEdges(byte buttons) => (byte)((MovementButtons)buttons & MovementButtons.All & ~MovementButtons.Edges);

    /// <summary>
    /// A copy that is safe to simulate whatever the sender put in it: a non-finite move becomes zero, the move is
    /// clamped to length 1 (no speed hack through a longer stick vector), and the yaw is finite and within 0..360.
    /// </summary>
    public MovementInput Sanitized()
    {
        MovementInput s = this;
        if (!IsFinite(s.Move.x) || !IsFinite(s.Move.y))
            s.Move = Vector2.zero;
        s.Move = Vector2.ClampMagnitude(s.Move, 1f);
        s.CameraYaw = IsFinite(s.CameraYaw) ? Mathf.Repeat(s.CameraYaw, 360f) : 0f;
        s.CameraPitch = IsFinite(s.CameraPitch) ? Mathf.Clamp(Mathf.DeltaAngle(0f, s.CameraPitch), -89f, 89f) : 0f;
        return s;
    }

    /// <summary>
    /// The same input with nothing asked for (no move, no button), keeping where the camera looks: what a frozen or
    /// stunned player simulates.
    /// </summary>
    public MovementInput Neutralized() => new MovementInput { CameraYaw = CameraYaw, CameraPitch = CameraPitch, RotateWithCamera = RotateWithCamera };

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    public override string ToString() =>
        $"move ({Move.x:0.##}, {Move.y:0.##}) buttons {(MovementButtons)PackButtons()} yaw {CameraYaw:0.#}";
}
