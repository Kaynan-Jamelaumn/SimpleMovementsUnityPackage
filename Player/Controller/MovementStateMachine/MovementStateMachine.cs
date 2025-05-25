using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


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
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private PlayerMovementController movementController;

    [SerializeField] private PlayerStatusController statusController;
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private PlayerAnimationModel animationModel;

    [SerializeField] private PlayerCameraModel cameraModel;
    [SerializeField] private PlayerCameraController cameraController;

    [SerializeField] private AvailabilityStateMachine availabilityStateMachine;

    private PlayerInput playerInput;

    private void Awake()
    {
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        movementController = this.CheckComponent(movementController, nameof(movementController));
        statusController = this.CheckComponent(statusController, nameof(statusController));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
        animationModel = this.CheckComponent(animationModel, nameof(animationModel));
        cameraModel = this.CheckComponent(cameraModel, nameof(cameraModel));
        cameraController = this.CheckComponent(cameraController, nameof(cameraController));
        availabilityStateMachine = this.CheckComponent(availabilityStateMachine, nameof(availabilityStateMachine));
        playerInput = new PlayerInput();
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

    private void OnEnable()
    {
        playerInput.Player.Enable();
    }

    private void OnDisable()
    {
        // Desabilita todas as ações do player input
        playerInput.Player.Disable();
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

