using UnityEngine;

/// <summary>
/// Swimming in deep water, Subnautica-style by default (see <see cref="SwimSettings"/>). The machine enters it from any
/// grounded or falling state once the feet are deep enough in deep enough water, and it ends when the water gets
/// shallow (the player stands up and walks out) or with a jump at the surface.
/// <list type="bullet">
/// <item>Forward swims where the camera looks (yaw and pitch), the side keys strafe, Jump held goes straight up,
/// Crouch held straight down, Sprint swims faster.</item>
/// <item>The swimmer has momentum (<see cref="MovementContext.SwimVelocity"/>, part of the network snapshot): it speeds
/// up at the acceleration and glides to a stop with the water drag.</item>
/// <item>With no up/down control the buoyancy decides: Neutral stays at the depth, Floats rises, Sinks sinks.</item>
/// <item>Nobody rises above floating at the surface (head out): looking up at the surface keeps you swimming along it.</item>
/// </list>
/// Gravity is off while swimming. Stamina is spent over time while swimming (more when fast, optionally while treading
/// water); out of stamina the player swims slower, cannot swim up or fast, and may sink.
/// </summary>
public class SwimmingState : MovementState
{
    // How strongly the player is held at the floating depth (1/s): the vertical speed allowed above it is the distance
    // to that depth times this, so it settles there instead of bobbing past it.
    private const float SurfacePull = 3f;
    // At the surface (feet within this of the floating depth), a jump press hops out.
    private const float SurfaceTolerance = 0.2f;

    public SwimmingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
    }

    public override void EnterState()
    {
        SwimSettings swim = Context.Swim;
        Context.MovementModel.CurrentSpeed = SwimSpeed(swim, false, false);
        // The splash eats most of a fall's speed; the horizontal momentum the player had carries on a little.
        float vv = Mathf.Clamp(Context.MovementModel.VerticalVelocity * 0.35f, -swim.verticalSpeed * 2f, swim.verticalSpeed);
        Vector3 horizontal = WorldMoveDirection(flat: true) * Context.MovementModel.CurrentSpeed * 0.5f;
        Context.SwimVelocity = new Vector3(horizontal.x, vv, horizontal.z);
        Context.MovementModel.VerticalVelocity = vv;
        Context.StaminaDrainTimer = MovementSnapshot.None;
        Context.MovementModel.IsSwimming = true;
    }

    public override void ExitState()
    {
        EndStaminaDrain();
        Context.MovementModel.IsSwimming = false;
        Context.SwimVelocity = Vector3.zero;
        // Leaving the water onto the ground (Jumping sets its own vertical speed when it is entered next).
        if (Context.MovementModel.VerticalVelocity > 0f)
            Context.MovementModel.VerticalVelocity = 0f;
    }

    public override void UpdateState()
    {
        SwimSettings swim = Context.Swim;
        MovementInput input = Context.Input;
        bool exhausted = IsExhausted(swim);
        bool moving = HasMoveInput() && Context.AvailabilityState.CanMove();
        bool fast = moving && input.Sprint && !exhausted && swim.fastSwimMultiplier > 1f;
        bool up = input.JumpHeld && !exhausted;
        bool down = input.Crouch && !up;

        // Where the player wants to go: along the look direction (or level), plus straight up / down.
        float speed = Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(SwimSpeed(swim, fast, exhausted));
        Context.MovementModel.CurrentSpeed = speed;
        Vector3 target = moving ? WorldMoveDirection(flat: !swim.swimWhereCameraLooks) * speed : Vector3.zero;
        if (up)
            target.y = Mathf.Max(target.y, swim.verticalSpeed);
        else if (down)
            target.y = Mathf.Min(target.y, -swim.verticalSpeed);
        else if (exhausted && swim.sinkWhenExhausted)
            target.y = Mathf.Min(target.y, -swim.sinkSpeed);
        else if (!moving || !swim.swimWhereCameraLooks)
        {
            if (swim.buoyancy == SwimBuoyancy.Floats)
                target.y = swim.riseSpeed;
            else if (swim.buoyancy == SwimBuoyancy.Sinks)
                target.y = -swim.sinkSpeed;
        }

        // Nobody rises above floating at the surface (head out), whatever they hold or look at.
        float toSurface = Context.FeetDepth - swim.floatDepth; // > 0: deeper than floating, < 0: higher than floating
        target.y = Mathf.Min(target.y, toSurface * SurfacePull);

        // Momentum: speed up towards the target, glide to a stop with the drag when nothing is asked.
        bool driving = moving || up || down || target.sqrMagnitude > 0.0001f;
        float rate = (driving ? swim.acceleration : swim.waterDrag) * Context.Dt;
        Vector3 v = Vector3.MoveTowards(Context.SwimVelocity, target, rate);
        if (v.y > toSurface * SurfacePull)
            v.y = toSurface * SurfacePull; // the surface stops an upward glide too
        Context.SwimVelocity = v;
        Context.MovementModel.VerticalVelocity = v.y; // animation (SwimVertical) and the snapshot

        Context.Move(v * Context.Dt);

        // Stamina: a stroke, a fast stroke, or treading water.
        float cost = fast ? swim.fastSwimStaminaCost
            : moving || up || down ? swim.staminaCost
            : swim.treadingStaminaCost;
        TickVariableStaminaDrain(cost);
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        if (!Context.ShouldKeepSwimming)
        {
            // Shallow water or out of it: on the feet again.
            if (Context.MovementModel.VerticalVelocity > 0.5f && !Context.MovementController.IsGrounded())
                return MovementStateMachine.EMovementState.Jumping;
            return IsMoving() ? MovementStateMachine.EMovementState.Walking : MovementStateMachine.EMovementState.Idle;
        }

        // A jump at the surface while swimming somewhere hops out (onto a bank, a boat, a pier).
        SwimSettings swim = Context.Swim;
        if (swim.jumpOutAtSurface && Context.FeetDepth <= swim.floatDepth + SurfaceTolerance && IsMoving()
            && TriggeredJump() && !IsExhausted(swim))
            return MovementStateMachine.EMovementState.Jumping;

        return StateKey;
    }

    /// <summary>
    /// The direction the move keys ask for, in the world: turned like walking (camera yaw, or the body's facing), and
    /// - unless <paramref name="flat"/> - tilted by the camera's pitch, so forward goes where the camera looks.
    /// </summary>
    private Vector3 WorldMoveDirection(bool flat)
    {
        Vector3 local = Context.MovementModel.Direction; // x = strafe, z = forward
        float yaw = Context.Input.RotateWithCamera ? Context.Input.CameraYaw : Context.MovementModel.LastRotation;
        if (Context.Input.RotateWithCamera && Context.MovementModel.PlayerTransform != null)
            Context.MovementModel.LastRotation = Context.MovementModel.PlayerTransform.rotation.eulerAngles.y;
        float pitch = 0f;
        if (!flat)
        {
            float raw = Mathf.Clamp(Context.Input.CameraPitch, -89f, 89f);
            float dead = Context.Swim.lookPitchDeadZone;
            pitch = Mathf.Abs(raw) <= dead ? 0f : raw - Mathf.Sign(raw) * dead;
        }
        // Forward and right of the look: pitch tilts forward only (strafing stays level, as in Subnautica).
        Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = look * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 dir = forward * local.z + right * local.x;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    private float SwimSpeed(SwimSettings swim, bool fast, bool exhausted)
    {
        float speed = GetBaseSpeed() * swim.speedMultiplier;
        if (fast)
            speed *= swim.fastSwimMultiplier;
        if (exhausted)
            speed *= swim.exhaustedSpeedMultiplier;
        return speed;
    }

    private bool IsExhausted(SwimSettings swim)
    {
        if (!Context.MovementModel.ShouldConsumeStamina)
            return false;
        StaminaManager stamina = Context.StatusController.StaminaManager;
        return stamina != null && stamina.CurrentValue < Mathf.Max(0.01f, swim.staminaCost);
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
