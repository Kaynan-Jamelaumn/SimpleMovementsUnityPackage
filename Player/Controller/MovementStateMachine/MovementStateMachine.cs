using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


/// <summary>
/// The player's movement: Idle, Walking, Running (Sprint), Crouching, Jumping, Dashing, Rolling and Swimming, driven by
/// the Player input actions (Movement, Sprint, Crouch, Jump, Dash, Roll). Speeds come from the SpeedManager, stamina costs
/// from the models, and the AvailabilityStateMachine blocks movement while stunned or dead.
/// </summary>
/// <remarks>
/// <para>All movement happens in one deterministic step, <see cref="Simulate"/>: it takes a <see cref="MovementInput"/>
/// (what the player asks for) and a step length, and depends on nothing else but the player's authoritative state
/// (position, speeds, stamina, stun). In single player Update calls it every frame with the devices' input
/// (<see cref="ReadDeviceInput"/>) and the frame time. In a network session the network layer sets
/// <see cref="ExternallyDriven"/> and calls it once per network tick - on the server with the input the owner sent, on
/// the owner to predict (and again to replay after a correction), on other clients to follow the forwarded input.</para>
/// <para><see cref="Capture"/> and <see cref="Restore"/> save and put back everything the step changes
/// (<see cref="MovementSnapshot"/>), which is what the server sends to correct a client's prediction.</para>
/// </remarks>
public class MovementStateMachine : StateManager<MovementStateMachine.EMovementState>, GameNet.IControlLock
{
    MovementContext context;
    public enum EMovementState
    {
        Idle,
        Walking,
        Running,
        Crouching,
        Jumping,
        Dashing,
        Rolling,
        /// <summary>In deep water (see <see cref="SwimSettings"/>). Always last: the values are sent over the network.</summary>
        Swimming,
    }
    [Header("References (all required; press Auto-assign in the inspector)")]
    [Tooltip("Movement settings: stamina costs, gravity, jump force, CharacterController.")]
    [SerializeField] private PlayerMovementModel movementModel;
    [Tooltip("Applies gravity and rotation and checks the ground.")]
    [SerializeField] private PlayerMovementController movementController;

    [Tooltip("Speed, stamina, weight, dash and roll settings.")]
    [SerializeField] private PlayerStatusController statusController;
    [Tooltip("Stamina spent by sprinting, jumping, dashing and rolling.")]
    [SerializeField] private StaminaManager staminaManager;
    [Tooltip("Animator parameters (IsRunning, IsDashing...).")]
    [SerializeField] private PlayerAnimationModel animationModel;

    [Tooltip("Camera settings (rotate with camera, first person).")]
    [SerializeField] private PlayerCameraModel cameraModel;
    [Tooltip("Turns input into camera-relative directions.")]
    [SerializeField] private PlayerCameraController cameraController;

    [Tooltip("Blocks movement while stunned or dead.")]
    [SerializeField] private AvailabilityStateMachine availabilityStateMachine;

    [Header("Simulation")]
    [Tooltip("Write the movement state into the Movement Model's Current Player State every step. Combat reads it: " +
             "crouching makes the player sneak (less noise) and rolling/dashing lets attacks miss. OFF keeps the game as " +
             "it always played (the value was never written before).")]
    [SerializeField] private bool reportStateToCombat;

    [Tooltip("A jump of the player's position between two movement steps longer than this (metres), made by something " +
             "other than the movement (a portal, travel, a respawn), counts as a teleport: in a network session the " +
             "players' smoothed models then snap to the new place instead of sliding there.")]
    [SerializeField, Min(0.5f)] private float teleportDetectDistance = 3f;

    [Tooltip("On the machine that controls the player, write a warning in the Console when a move key is held but does " +
             "not reach the movement, or when the player is asked to move but stays in place (with the reason). The " +
             "CharacterController always ignores the player's own colliders (weapons, hit boxes), whatever this says.")]
    [SerializeField] private bool diagnoseBlockedMovement = true;

    [Tooltip("On the streamed open-world terrain, never move the player onto ground whose chunk is not ready yet " +
             "(still generating, collider not cooked): a long dash or a fast run stops at the edge of the loaded ground " +
             "and continues once it is there, instead of falling through the world. A player put somewhere not loaded " +
             "yet (a respawn, a travel) waits in place until the ground under it is ready.")]
    [SerializeField] private bool waitForStreamedGround = true;

    [Tooltip("Seconds the player may wait for the ground to load under it before it is let go (with a warning): a " +
             "safety net if a chunk never gets a collider.")]
    [SerializeField, Min(1f)] private float streamedGroundTimeout = 8f;

    private MovementGuard guard;
    private float groundWait;
    private bool groundWaitReported;
    private bool heldByStreaming;

    /// <summary>Fills empty references from this object, its children and parents (inspector button).</summary>
    public void AutoAssignReferences()
    {
        movementModel = Find(movementModel);
        movementController = Find(movementController);
        statusController = Find(statusController);
        staminaManager = Find(staminaManager);
        animationModel = Find(animationModel);
        cameraModel = Find(cameraModel);
        cameraController = Find(cameraController);
        availabilityStateMachine = Find(availabilityStateMachine);
    }

    private T Find<T>(T current) where T : Component
    {
        if (current != null) return current;
        T c = GetComponent<T>();
        if (c == null) c = GetComponentInChildren<T>(true);
        if (c == null) c = GetComponentInParent<T>();
        return c;
    }

    private PlayerInput playerInput;
    private TraitManager traitManager;
    private bool deviceInputEnabled = true;
    private bool frozen;
    private bool hasSimulated;
    private bool warnedUndriven;
    private byte teleports;
    private bool hasStepEnd;
    private Vector3 stepEnd;

    // ------------------------------------------------------------------ public state

    /// <summary>
    /// True while something else calls <see cref="Simulate"/> (the network layer, once per tick): Update then reads no
    /// devices and steps nothing. False in single player, where Update simulates every frame.
    /// </summary>
    public bool ExternallyDriven { get; set; }

    /// <summary>
    /// Controls locked (travel, dungeon transitions): the machine keeps stepping but every input is ignored and
    /// nothing moves the player - no walking, no gravity. Server state, part of the snapshot.
    /// </summary>
    public bool Frozen
    {
        get => frozen;
        set
        {
            frozen = value;
            if (context != null)
                context.Frozen = value;
        }
    }

    /// <summary>
    /// True while a network replay re-simulates steps that already happened: same results, but no presentation and
    /// no events (they fired the first time). Set by the network layer around <see cref="Simulate"/>.
    /// </summary>
    public bool IsReplaying
    {
        get => context != null && context.IsReplaying;
        set
        {
            if (context != null)
                context.IsReplaying = value;
        }
    }

    /// <summary>True once <see cref="Simulate"/> has run at least one step.</summary>
    public bool HasSimulated => hasSimulated;

    /// <summary>The input of the last simulated step (after sanitizing; neutral while frozen or stunned).</summary>
    public MovementInput LastInput => context != null ? context.Input : default;

    /// <summary>The movement clock: seconds simulated so far (cooldowns, buffers and state times use it).</summary>
    public float SimTime => context != null ? context.SimTime : 0f;

    /// <summary>Seconds until a dash is allowed again (0 = ready), on the movement clock.</summary>
    public float DashCooldownLeft => CooldownLeft(statusController != null ? statusController.DashModel : null);

    /// <summary>Seconds until a roll is allowed again (0 = ready), on the movement clock.</summary>
    public float RollCooldownLeft => CooldownLeft(statusController != null ? statusController.RollModel : null);

    /// <summary>
    /// How many teleports this player has had (wraps at 255). It changes when the server moves the player somewhere
    /// else in one go (detected automatically, or reported with <see cref="NotifyTeleported"/>) and travels to the
    /// clients with the snapshot, so every machine knows to snap the player's smoothed model instead of sliding it.
    /// </summary>
    public byte TeleportCount => teleports;

    /// <summary>Raised (outside replays) when this machine detects or is told about a teleport.</summary>
    public event Action Teleported;

    /// <summary>The object the CharacterController moves (the player's root on a networked prefab).</summary>
    private Transform MovedRoot => movementModel != null && movementModel.Controller != null ? movementModel.Controller.transform : transform;

    private StaminaManager Stamina => statusController != null && statusController.StaminaManager != null ? statusController.StaminaManager : staminaManager;

    protected override float CurrentTime => context != null ? context.SimTime : Time.time;

    protected override bool SuppressStateEvents => context != null && context.IsReplaying;

    // ------------------------------------------------------------------ lifecycle

    private void Awake()
    {
        // Created first: when a missing reference disables this component during Awake, Unity calls OnDisable
        // right away, and it must find the input already there.
        playerInput = new PlayerInput();
        InputBindingStore.Register(playerInput.asset); // the saved key bindings

        // References placed on a child (the character model) or a parent are found as well, not only on this object.
        AutoAssignReferences();
        if (animationModel == null)
        {
            // The animation model only needs an Animator somewhere under the player: add it instead of failing.
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animationModel = gameObject.AddComponent<PlayerAnimationModel>();
                Debug.LogWarning($"[Movement] '{name}' had no PlayerAnimationModel; one was added (it uses the Animator on '{animator.name}'). Add it to the player prefab to keep it.", this);
            }
        }

        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        movementController = this.CheckComponent(movementController, nameof(movementController));
        statusController = this.CheckComponent(statusController, nameof(statusController));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
        animationModel = this.CheckComponent(animationModel, nameof(animationModel));
        cameraModel = this.CheckComponent(cameraModel, nameof(cameraModel));
        cameraController = this.CheckComponent(cameraController, nameof(cameraController));
        availabilityStateMachine = this.CheckComponent(availabilityStateMachine, nameof(availabilityStateMachine));
        if (!enabled)
            return; // a required reference is missing (logged above): stay disabled instead of running half set up

        context = new MovementContext(
            movementModel,
            statusController,
            playerInput,
            movementController,
            animationModel,
            cameraModel,
            cameraController,
            availabilityStateMachine
        );
        context.Machine = this;
        context.Frozen = frozen;
        guard = MovementGuard.Attach(this, movementModel.Controller);
        if (guard != null)
            guard.LogWarnings = diagnoseBlockedMovement;

        traitManager = statusController.TraitManager;
        if (traitManager == null)
            traitManager = GetComponentInParent<TraitManager>();
        if (traitManager == null)
            traitManager = statusController.GetComponentInChildren<TraitManager>(true);

        // Dash and roll are ready from the start (their last use was "never", not the clock's zero).
        if (statusController.DashModel != null)
            statusController.DashModel.LastActionTime = float.NegativeInfinity;
        if (statusController.RollModel != null)
            statusController.RollModel.LastActionTime = float.NegativeInfinity;

        InitializeStates();
        SetDriving(isActiveAndEnabled);
    }

    protected override void Update()
    {
        if (ExternallyDriven || context == null)
            return;
        if (GameNet.NetAuthority.IsNetworkedObject(gameObject))
        {
            // A networked player is stepped by its NetPlayerMovement on the network tick. Without one, stepping it
            // here would make every copy follow this machine's keyboard.
            if (!warnedUndriven)
            {
                warnedUndriven = true;
                Debug.LogWarning($"[Movement] '{name}' is networked but nothing drives its movement: add a NetPlayerMovement to the player prefab.", this);
            }
            return;
        }
        Simulate(ReadDeviceInput(), Time.deltaTime);
    }

    private void OnEnable()
    {
        if (deviceInputEnabled)
            InputBindingStore.EnableGameplayMap(playerInput?.asset);
        SetDriving(context != null);
    }

    private void OnDisable()
    {
        // Desabilita todas as ações do player input
        InputBindingStore.DisableGameplayMap(playerInput?.asset);
        SetDriving(false);
    }

    private void OnDestroy()
    {
        SetDriving(false);
        // The generated input class owns a copy of the actions asset: free it (the binding store forgets destroyed
        // assets on its own).
        if (playerInput != null)
        {
            InputBindingStore.DisableGameplayMap(playerInput.asset);
            playerInput.Dispose();
            playerInput = null;
        }
    }

    // While this machine runs, its step applies gravity and rotation, regenerates stamina and runs the movement traits
    // (instead of their own Update/FixedUpdate/LateUpdate), so a network replay runs them again in the same order.
    private void SetDriving(bool on)
    {
        if (movementController != null)
            movementController.DrivenBySimulation = on;
        StaminaManager st = Stamina;
        if (st != null)
            st.DrivenBySimulation = on;
        if (traitManager != null)
            traitManager.MovementTraitsDriven = on;
    }

    private void InitializeStates()
    {
        States.Add(EMovementState.Idle, new IdleState(context, EMovementState.Idle));
        States.Add(EMovementState.Walking, new WalkingState(context, EMovementState.Walking));
        States.Add(EMovementState.Crouching, new CrouchingState(context, EMovementState.Crouching));
        States.Add(EMovementState.Running, new RunningState(context, EMovementState.Running));
        States.Add(EMovementState.Jumping, new JumpingState(context, EMovementState.Jumping));
        States.Add(EMovementState.Dashing, new DashingState(context, EMovementState.Dashing));
        States.Add(EMovementState.Rolling, new RollingState(context, EMovementState.Rolling));
        States.Add(EMovementState.Swimming, new SwimmingState(context, EMovementState.Swimming));
        CurrentState = States[EMovementState.Idle];
    }

    // ------------------------------------------------------------------ input

    /// <summary>
    /// Reads this player's devices into a <see cref="MovementInput"/>: the move keys/stick, Sprint, Crouch and Jump
    /// held, this frame's new Jump/Dash/Roll presses, and the active camera's yaw and rotate-with-camera mode. Only
    /// the machine that controls the player calls it (single player every frame; the owning client to build what it
    /// sends to the server). Returns a neutral input while device input is off, and while the gameplay keys are held off
    /// (a crafting or alchemy station is being worked: the player stands at it and does not walk away).
    /// </summary>
    public MovementInput ReadDeviceInput()
    {
        var input = new MovementInput();
        if (playerInput != null && deviceInputEnabled && !InputBindingStore.GameplayInputBlocked)
        {
            PlayerInput.PlayerActions p = playerInput.Player;
            input.Move = p.Movement.ReadValue<Vector2>();
            input.Sprint = p.Sprint.IsPressed();
            input.Crouch = p.Crouch.IsPressed();
            input.JumpPressed = p.Jump.WasPressedThisFrame();
            input.JumpHeld = p.Jump.IsPressed();
            input.DashPressed = p.Dash.triggered;
            input.RollPressed = p.Roll.triggered;
        }

        Transform cam = cameraModel != null ? cameraModel.CameraTransform : null;
        if (cam != null)
        {
            input.CameraYaw = cam.eulerAngles.y;
            input.CameraPitch = Mathf.DeltaAngle(0f, cam.eulerAngles.x);
            input.RotateWithCamera = cameraModel.PlayerShouldRotateByCameraAngle || cameraModel.IsFirstPerson;
        }
        else
        {
            // No camera (yet): move relative to where the body faces.
            Transform body = movementModel != null && movementModel.PlayerTransform != null ? movementModel.PlayerTransform : transform;
            input.CameraYaw = body.eulerAngles.y;
        }
        return input;
    }

    /// <summary>
    /// Turns this machine's own device reads on or off. The network layer switches them off on every copy that is not
    /// controlled from this machine (other players, every player on a dedicated server), so a copy never reacts to
    /// the local keyboard.
    /// </summary>
    public void SetDeviceInputEnabled(bool on)
    {
        deviceInputEnabled = on;
        if (playerInput == null)
            return;
        if (on && isActiveAndEnabled)
            InputBindingStore.EnableGameplayMap(playerInput.asset);
        else if (!on)
            InputBindingStore.DisableGameplayMap(playerInput.asset);
    }

    /// <summary>This machine's own Movement action (diagnostics; null before Awake).</summary>
    public UnityEngine.InputSystem.InputAction MovementAction => playerInput != null ? playerInput.Player.Movement : null;

    /// <summary>Is this machine reading its devices (see <see cref="SetDeviceInputEnabled"/>)?</summary>
    public bool DeviceInputEnabled => deviceInputEnabled;

    // ------------------------------------------------------------------ the step

    /// <summary>
    /// Simulates one movement step of <paramref name="dt"/> seconds with <paramref name="input"/>. The order is fixed
    /// (a replay must reproduce it): sanitize the input (neutral while frozen, stunned or dead), advance the movement
    /// clock, buffer a jump press, set the move direction, look for water (and start swimming in deep water), step the
    /// state machine, the movement traits, gravity (not while swimming), the body's rotation and stamina regeneration;
    /// then keep the player on the streamed terrain's ready ground.
    /// </summary>
    /// <remarks>
    /// Gravity is applied here AND by the Jumping state while in the air: the game was tuned with that double
    /// integration (jump height and air time), so both stay.
    /// </remarks>
    public void Simulate(in MovementInput input, float dt)
    {
        if (context == null || CurrentState == null)
            return;
        StartMachine();

        float step = dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt) ? dt : 0f;
        MovementInput clean = input.Sanitized();
        if (frozen || (availabilityStateMachine != null && !availabilityStateMachine.CanMove()))
            clean = clean.Neutralized();

        DetectExternalTeleport();

        context.Frozen = frozen;
        context.Input = clean;
        context.Dt = step;
        context.SimTime += step;
        if (clean.JumpPressed)
            context.RecordJumpPress();

        movementModel.Movement2D = clean.Move;
        movementModel.Direction = new Vector3(clean.Move.x, 0f, clean.Move.y);

        Vector3 before = MovedRoot.position;
        if (guard != null && !context.IsReplaying)
            guard.Reapply();

        SampleWater();
        if (CurrentState.StateKey != EMovementState.Swimming && context.ShouldStartSwimming && CanStartSwimming())
            TransitionToState(EMovementState.Swimming);
        StepMachine();

        if (traitManager != null && traitManager.isActiveAndEnabled)
            traitManager.TickMovementTraits(clean, step);

        // A switched-off movement controller (single player travel) applied neither gravity nor rotation before.
        bool swimming = CurrentState.StateKey == EMovementState.Swimming;
        if (movementController.isActiveAndEnabled)
        {
            if (!frozen && !swimming)
                movementController.ApplyGravity(step);
            movementController.ApplyRotation(clean.CameraYaw, clean.RotateWithCamera);
        }

        StaminaManager st = Stamina;
        if (st != null && st.DrivenBySimulation)
            st.TickRegen(step);

        if (reportStateToCombat)
            movementModel.CurrentPlayerState = (PlayerMovementModel.PlayerState)(int)CurrentState.StateKey;

        heldByStreaming = KeepOnStreamedGround(before, step);
        movementModel.IsSwimming = CurrentState.StateKey == EMovementState.Swimming;
        movementModel.HeadUnderwater = context.InWater && EyeY() < context.WaterSurface;

        hasSimulated = true;
        stepEnd = MovedRoot.position;
        hasStepEnd = true;

        if (guard != null && !context.IsReplaying && !heldByStreaming)
            guard.Observe(input.Sanitized(), before, stepEnd, step, CurrentState.StateKey.ToString(), frozen,
                availabilityStateMachine == null || availabilityStateMachine.CanMove(), movementModel.CurrentSpeed);
    }

    /// <summary>Velocity while swimming (m/s, world space; zero out of the water).</summary>
    public Vector3 SwimVelocity => context != null ? context.SwimVelocity : Vector3.zero;

    /// <summary>True while the player waits at the edge of the streamed terrain for the ground ahead (or under it) to load.</summary>
    public bool IsWaitingForGround => heldByStreaming;

    // ------------------------------------------------------------------ water

    // The water at the player's position, into the context (the states read it) and the model (UI, animation).
    private void SampleWater()
    {
        Transform root = MovedRoot;
        float feet = FeetY();
        if (WaterQuery.TryGetWater(new Vector3(root.position.x, feet, root.position.z), root.gameObject, out float surface, out float bottom))
        {
            context.InWater = true;
            context.WaterSurface = surface;
            context.WaterDepth = Mathf.Max(0f, surface - bottom);
            context.FeetDepth = surface - feet;
        }
        else
        {
            context.InWater = false;
            context.WaterSurface = float.NaN;
            context.WaterDepth = 0f;
            context.FeetDepth = 0f;
        }
        movementModel.WaterSurface = context.WaterSurface;
    }

    // Swimming starts from any state but a dash or roll in progress (they finish first), a jump still going up (out
    // of the water) and a climb (a trait holds the player).
    private bool CanStartSwimming()
    {
        EMovementState state = CurrentState.StateKey;
        if (state == EMovementState.Dashing || state == EMovementState.Rolling)
            return false;
        if (state == EMovementState.Jumping && movementModel.VerticalVelocity > 0f)
            return false;
        return !movementModel.SuspendGravity;
    }

    // Height of the bottom of the CharacterController (the player's feet).
    private float FeetY()
    {
        Transform root = MovedRoot;
        CharacterController cc = movementModel.Controller;
        if (cc == null)
            return root.position.y;
        return root.position.y + (cc.center.y - cc.height * 0.5f) * Mathf.Abs(root.lossyScale.y);
    }

    // Height of the player's eyes: the swim settings' eye height, else just under the top of the CharacterController.
    private float EyeY()
    {
        float eye = movementModel.Swimming.eyeHeight;
        CharacterController cc = movementModel.Controller;
        if (eye <= 0f)
            eye = cc != null ? Mathf.Max(0.3f, cc.height * Mathf.Abs(MovedRoot.lossyScale.y) - Submersion.EyeDepth) : 1.6f;
        return FeetY() + eye;
    }

    // ------------------------------------------------------------------ streamed ground

    /// <summary>
    /// The open world's terrain is generated around the players a little ahead of them, and a chunk's collider comes
    /// a frame or more after its data. A long dash can outrun that: past the last ready chunk there is no ground and
    /// the player falls through the world. After each step, a player that ended over ground that is not ready is put
    /// back where the step started (the edge of the ready ground); one whose start was not ready either (a respawn
    /// or travel into an area not loaded yet) waits there, without falling, until it is. Only the machine that
    /// decides the movement does this (the server, single player, and the owner predicting): other clients load only
    /// the ground around their own player. Returns true while the player is held.
    /// </summary>
    private bool KeepOnStreamedGround(Vector3 before, float step)
    {
        if (!waitForStreamedGround || !(GameNet.NetAuthority.IsServer || deviceInputEnabled))
            return false;
        Transform root = MovedRoot;
        if (!StreamedGround.Applies(root.gameObject) || StreamedGround.IsReady(root.position))
        {
            groundWait = 0f;
            groundWaitReported = false;
            return false;
        }

        bool startReady = StreamedGround.IsReady(before);
        if (!startReady)
        {
            // Waiting for the ground under the player: let go after the timeout (a chunk that never gets a collider).
            groundWait += step;
            if (groundWait > streamedGroundTimeout)
            {
                if (!groundWaitReported && !context.IsReplaying)
                {
                    groundWaitReported = true;
                    Debug.LogWarning($"[Movement] '{name}' waited {streamedGroundTimeout:0}s for the terrain under {root.position} to load; letting it go. " +
                                     "Is the EndlessTerrain streaming around this player (a viewer), and does it make colliders?", this);
                }
                return false;
            }
        }

        CharacterController cc = movementModel.Controller;
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null)
            cc.enabled = false;
        root.position = before;
        if (cc != null)
            cc.enabled = ccWasEnabled;
        if (guard != null)
            guard.Reapply(); // switching the controller off made Unity forget the ignored own colliders
        if (movementModel.VerticalVelocity < 0f)
            movementModel.VerticalVelocity = 0f;
        return true;
    }

    // ------------------------------------------------------------------ control lock and teleports

    /// <summary><see cref="GameNet.IControlLock"/>: locking the controls freezes the movement (see <see cref="Frozen"/>).</summary>
    public void SetControlsLocked(bool locked) => Frozen = locked;

    /// <summary>
    /// Tells the movement that the player was just moved somewhere else in one go (server side): the teleport count
    /// changes, so every machine snaps the player's smoothed model to the new place. Teleports longer than the detect
    /// distance are noticed without this call.
    /// </summary>
    public void NotifyTeleported()
    {
        if (context == null)
            return;
        unchecked { teleports++; }
        stepEnd = MovedRoot.position;
        hasStepEnd = true;
        if (!context.IsReplaying)
            Teleported?.Invoke();
    }

    // Something other than the movement (a portal, travel, a respawn) moved the player far since the last step. Only
    // the machine that decides counts it (server or single player); clients learn it from the snapshot.
    private void DetectExternalTeleport()
    {
        if (!hasStepEnd || context.IsReplaying || !GameNet.NetAuthority.IsServer)
            return;
        Vector3 moved = MovedRoot.position - stepEnd;
        if (moved.sqrMagnitude > teleportDetectDistance * teleportDetectDistance)
            NotifyTeleported();
    }

    // ------------------------------------------------------------------ snapshots

    /// <summary>Saves everything the movement step changes (see <see cref="MovementSnapshot"/>).</summary>
    public MovementSnapshot Capture()
    {
        var s = new MovementSnapshot();
        if (context == null)
            return s;
        float now = context.SimTime;
        Transform root = MovedRoot;
        Transform body = movementModel.PlayerTransform != null ? movementModel.PlayerTransform : root;
        s.Position = root.position;
        s.RootYaw = root.eulerAngles.y;
        s.BodyYaw = body.eulerAngles.y;
        s.VerticalVelocity = movementModel.VerticalVelocity;
        s.State = CurrentState != null ? CurrentState.StateKey : EMovementState.Idle;
        s.PreviousState = PreviousStateKey;
        s.StateTime = HasStarted ? Mathf.Max(0f, now - StateEnteredTime) : 0f;
        s.SimTime = now;
        s.CurrentSpeed = movementModel.CurrentSpeed;
        s.LastRotation = movementModel.LastRotation;
        s.JumpBufferLeft = MovementSnapshot.Remaining(now, context.LastJumpPressTime, context.JumpBufferTime);
        PlayerDashModel dash = statusController.DashModel;
        PlayerRollModel roll = statusController.RollModel;
        s.DashCooldownLeft = dash != null ? MovementSnapshot.Remaining(now, dash.LastActionTime, dash.CooldownDuration) : MovementSnapshot.None;
        s.RollCooldownLeft = roll != null ? MovementSnapshot.Remaining(now, roll.LastActionTime, roll.CooldownDuration) : MovementSnapshot.None;
        s.ActionElapsed = context.ActionElapsed;
        s.StaminaDrainTimer = context.StaminaDrainTimer;
        StaminaManager st = Stamina;
        if (st != null)
        {
            s.Stamina = st.CurrentValue;
            s.StaminaConsuming = st.IsConsuming;
            st.GetRegenState(out s.StaminaRegenerating, out s.StaminaRegenTimer);
        }
        s.SuspendGravity = movementModel.SuspendGravity;
        s.SwimVelocity = context.SwimVelocity;
        s.Frozen = frozen;
        s.Teleports = teleports;
        if (traitManager != null)
            traitManager.CaptureMovementTraits(ref s);
        return s;
    }

    /// <summary>
    /// Puts the movement back exactly as <paramref name="s"/> describes, without running any state's Enter/Exit or
    /// raising events (network reconciliation: the replayed steps follow). The CharacterController is switched off
    /// around the position change so its collision position follows, then left as it was.
    /// </summary>
    public void Restore(in MovementSnapshot s)
    {
        if (context == null)
            return;
        float now = Finite(s.SimTime, context.SimTime);
        context.SimTime = now;

        CharacterController cc = movementModel.Controller;
        Transform root = MovedRoot;
        Transform body = movementModel.PlayerTransform != null ? movementModel.PlayerTransform : root;
        if (Finite(s.Position))
        {
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;
            root.position = s.Position;
            if (body != root && Finite(s.RootYaw) && Mathf.Abs(Mathf.DeltaAngle(root.eulerAngles.y, s.RootYaw)) > 0.001f)
                root.rotation = Quaternion.Euler(0f, s.RootYaw, 0f);
            if (cc != null)
                cc.enabled = ccWasEnabled;
            if (guard != null)
                guard.Reapply(); // switching the controller off made Unity forget the ignored own colliders
        }
        if (Finite(s.BodyYaw))
            body.rotation = Quaternion.Euler(0f, s.BodyYaw, 0f);

        movementModel.VerticalVelocity = Finite(s.VerticalVelocity, 0f);
        movementModel.CurrentSpeed = Finite(s.CurrentSpeed, 0f);
        movementModel.LastRotation = Finite(s.LastRotation, movementModel.LastRotation);
        movementModel.SuspendGravity = s.SuspendGravity;
        context.SwimVelocity = Finite(s.SwimVelocity) ? s.SwimVelocity : Vector3.zero;
        Frozen = s.Frozen;

        EMovementState state = Defined(s.State) ? s.State : EMovementState.Idle;
        EMovementState previous = Defined(s.PreviousState) ? s.PreviousState : state;
        RestoreState(state, previous, now - Mathf.Max(0f, Finite(s.StateTime, 0f)));

        context.LastJumpPressTime = MovementSnapshot.Since(now, s.JumpBufferLeft, context.JumpBufferTime);
        PlayerDashModel dash = statusController.DashModel;
        PlayerRollModel roll = statusController.RollModel;
        if (dash != null)
            dash.LastActionTime = MovementSnapshot.Since(now, s.DashCooldownLeft, dash.CooldownDuration);
        if (roll != null)
            roll.LastActionTime = MovementSnapshot.Since(now, s.RollCooldownLeft, roll.CooldownDuration);
        context.ActionElapsed = Mathf.Max(0f, Finite(s.ActionElapsed, 0f));
        context.StaminaDrainTimer = Finite(s.StaminaDrainTimer, MovementSnapshot.None);

        StaminaManager st = Stamina;
        if (st != null)
        {
            if (Finite(s.Stamina))
                st.SetCurrentValue(s.Stamina);
            st.IsConsuming = s.StaminaConsuming;
            st.SetRegenState(s.StaminaRegenerating, s.StaminaRegenTimer);
        }

        // Flags the Dashing/Rolling states keep while they run (Enter sets, Exit clears); read by combat and animation.
        if (animationModel != null)
        {
            animationModel.IsDashing = state == EMovementState.Dashing;
            animationModel.IsRolling = state == EMovementState.Rolling;
        }
        movementModel.IsSwimming = state == EMovementState.Swimming;
        if (reportStateToCombat)
            movementModel.CurrentPlayerState = (PlayerMovementModel.PlayerState)(int)state;

        teleports = s.Teleports;
        if (traitManager != null)
            traitManager.RestoreMovementTraits(s);

        stepEnd = root.position;
        hasStepEnd = true;
    }

    private float CooldownLeft(PlayerActionModelBase model)
    {
        if (model == null || context == null)
            return 0f;
        float left = MovementSnapshot.Remaining(context.SimTime, model.LastActionTime, model.CooldownDuration);
        return left < 0f ? 0f : left;
    }

    private static bool Defined(EMovementState s) => s >= EMovementState.Idle && s <= EMovementState.Swimming;
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static float Finite(float v, float fallback) => Finite(v) ? v : fallback;
    private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
}
