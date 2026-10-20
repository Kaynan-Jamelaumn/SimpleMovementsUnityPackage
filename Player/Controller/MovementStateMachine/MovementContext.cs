using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// What the movement states share: the player's components, the input and length of the step being simulated, the
/// movement clock, and the small timers that used to live in coroutines (jump buffer, dash/roll progress, stamina
/// drain). Everything the states decide depends only on this, so a step can be simulated again (network replays).
/// </summary>
public class MovementContext
{
    private PlayerMovementModel movementModel;
    private PlayerStatusController statusController;
    private PlayerInput playerInput;
    private PlayerMovementController movementController;
    private PlayerAnimationModel animationModel;
    private PlayerCameraModel cameraModel;
    private PlayerCameraController cameraController;
    private AvailabilityStateMachine availabilityStateMachine;

    public MovementContext(PlayerMovementModel movementModel, PlayerStatusController statusController,
        PlayerInput playerInput, PlayerMovementController movementController, PlayerAnimationModel animationModel,
        PlayerCameraModel cameraModel, PlayerCameraController cameraController, AvailabilityStateMachine availabilityStateMachine)
    {

        this.movementModel = movementModel;
        this.statusController = statusController;
        this.playerInput = playerInput;
        this.movementController = movementController;
        this.animationModel = animationModel;
        this.cameraModel = cameraModel;
        this.cameraController = cameraController;
        this.availabilityStateMachine = availabilityStateMachine;
    }
    public PlayerMovementModel MovementModel => movementModel;
    public PlayerStatusController StatusController => statusController;
    /// <summary>The machine's own copy of the input actions (device reads happen only in <see cref="MovementStateMachine.ReadDeviceInput"/>).</summary>
    public PlayerInput PlayerInput => playerInput;
    public PlayerMovementController MovementController => movementController;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerCameraModel CameraModel => cameraModel;
    public PlayerCameraController CameraController => cameraController;
    public AvailabilityStateMachine AvailabilityState => availabilityStateMachine;

    /// <summary>The machine that owns this context.</summary>
    public MovementStateMachine Machine { get; set; }

    private CombatEntity bodyEntity;

    /// <summary>
    /// Does the player's body allow <paramref name="action"/> (injuries: a broken leg stops sprinting, a missing foot
    /// rolling)? Always true without the injury system. Client copies use the state the server sends, so predicted
    /// steps match the server's.
    /// </summary>
    public bool BodyAllows(BodyAction action)
    {
        if (bodyEntity == null && statusController != null)
            bodyEntity = CombatEntity.Resolve(statusController.gameObject);
        return bodyEntity == null || bodyEntity.AllowsAction(action);
    }

    /// <summary>
    /// How the player's legs let it move (injuries: limping, hopping on one leg, crawling without legs); normal without
    /// the injury system. Client copies use the state the server sends, like <see cref="BodyAllows"/>.
    /// </summary>
    public BodyLocomotion BodyLocomotion
    {
        get
        {
            if (bodyEntity == null && statusController != null)
                bodyEntity = CombatEntity.Resolve(statusController.gameObject);
            return bodyEntity != null ? bodyEntity.Locomotion : BodyLocomotion.Normal;
        }
    }

    /// <summary>
    /// The body's speed multiplier for this step: steady while limping or crawling, in bursts while hopping (measured on
    /// <see cref="SimTime"/>, so a replayed step moves the same).
    /// </summary>
    public float BodySpeed => BodyLocomotion.SpeedAt(SimTime);

    // ------------------------------------------------------------------ the step being simulated

    /// <summary>The input of the step being simulated (already sanitized; neutral while frozen or stunned).</summary>
    public MovementInput Input { get; set; }

    /// <summary>Length of the step being simulated (seconds): the frame time offline, the tick time in a network session.</summary>
    public float Dt { get; set; }

    /// <summary>The movement clock: seconds simulated so far. Cooldowns, buffers and time in state are measured with it.</summary>
    public float SimTime { get; set; }

    /// <summary>
    /// True while a network replay re-simulates a step that already happened: the result must be the same, but
    /// presentation (sounds, effects, state-change events) must not fire a second time.
    /// </summary>
    public bool IsReplaying { get; set; }

    /// <summary>Controls are locked: the states still run (with no input) but nothing moves the player.</summary>
    public bool Frozen { get; set; }

    /// <summary>Seconds the current dash or roll has moved (replaces the old movement coroutines).</summary>
    public float ActionElapsed { get; set; }

    /// <summary>Seconds left before sprinting or crouching spends stamina again (<see cref="MovementSnapshot.None"/> = not draining).</summary>
    public float StaminaDrainTimer { get; set; } = MovementSnapshot.None;

    /// <summary>Seconds between two stamina costs while sprinting or crouching.</summary>
    public const float StaminaDrainInterval = 0.8f;

    // ------------------------------------------------------------------ jump buffer

    /// <summary>Seconds a jump press is remembered (a press just before landing still jumps).</summary>
    public float JumpBufferTime = 0.2f;
    /// <summary><see cref="SimTime"/> of the last jump press that has not been used yet (negative infinity = none).</summary>
    public float LastJumpPressTime { get; set; } = float.NegativeInfinity;
    public void RecordJumpPress() => LastJumpPressTime = SimTime;
    public void ConsumeJumpPress() => LastJumpPressTime = float.NegativeInfinity;
    public bool JumpPressBuffered => SimTime - LastJumpPressTime <= JumpBufferTime;

    // ------------------------------------------------------------------ water (sampled by the machine every step)

    /// <summary>There is water at the player's position this step (terrain water or a SwimmableWater volume).</summary>
    public bool InWater { get; set; }
    /// <summary>Height of the water surface at the player (NaN out of water).</summary>
    public float WaterSurface { get; set; } = float.NaN;
    /// <summary>How deep the water is there, surface to bottom (metres; 0 out of water).</summary>
    public float WaterDepth { get; set; }
    /// <summary>How far the player's feet are under the surface (metres; negative above it, 0 out of water).</summary>
    public float FeetDepth { get; set; }

    /// <summary>Velocity while swimming (m/s, world space). Part of the snapshot, so a replay glides the same way.</summary>
    public Vector3 SwimVelocity { get; set; }

    /// <summary>The player's swimming settings.</summary>
    public SwimSettings Swim => movementModel.Swimming;

    /// <summary>The water here is deep enough to swim in (and swimming is on).</summary>
    public bool CanSwimHere => Swim.enabled && InWater && WaterDepth >= Swim.minWaterDepth;

    /// <summary>The player should start swimming: deep water, feet at least <see cref="SwimSettings.enterDepth"/> under.</summary>
    public bool ShouldStartSwimming => CanSwimHere && FeetDepth >= Swim.enterDepth;

    /// <summary>
    /// The player keeps swimming: the water is still deep enough (a little shallower than to start, so wading out
    /// along a slope does not flicker between swimming and walking) and the feet are still under
    /// <see cref="SwimSettings.exitDepth"/>.
    /// </summary>
    public bool ShouldKeepSwimming => Swim.enabled && InWater && WaterDepth >= Swim.minWaterDepth - 0.1f && FeetDepth >= Swim.exitDepth;

    // ------------------------------------------------------------------ moving

    /// <summary>
    /// Moves the player's CharacterController by <paramref name="delta"/> - unless the controls are locked or the
    /// controller is switched off (a travel or a teleport in progress), where moving would only log warnings.
    /// </summary>
    public void Move(Vector3 delta)
    {
        if (Frozen)
            return;
        CharacterController cc = movementModel != null ? movementModel.Controller : null;
        if (cc == null || !cc.enabled || !cc.gameObject.activeInHierarchy)
            return;
        cc.Move(delta);
    }
}
