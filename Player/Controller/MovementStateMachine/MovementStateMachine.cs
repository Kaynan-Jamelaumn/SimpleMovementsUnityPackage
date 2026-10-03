using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


/// <summary>
/// The player's movement: Idle, Walking, Running (Sprint), Crouching, Jumping, Dashing and Rolling, driven by the
/// Player input actions (Movement, Sprint, Crouch, Jump, Dash, Roll). Speeds come from the SpeedManager, stamina costs
/// from the models, and the AvailabilityStateMachine blocks movement while stunned or dead.
/// </summary>
public class MovementStateMachine : StateManager<MovementStateMachine.EMovementState>
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

        InitializeStates();
    }

    protected override void Update()
    {
        // Remember jump presses (used by the states for a short buffer).
        if (context != null && playerInput != null && playerInput.Player.Jump.WasPressedThisFrame())
            context.RecordJumpPress();
        base.Update();
    }

    private void OnEnable()
    {
        playerInput?.Player.Enable();
    }

    private void OnDisable()
    {
        // Desabilita todas as ações do player input
        playerInput?.Player.Disable();
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
        CurrentState = States[EMovementState.Idle];
    }
}

