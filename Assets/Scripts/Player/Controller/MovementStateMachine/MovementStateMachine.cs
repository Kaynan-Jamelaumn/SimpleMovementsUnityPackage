using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Windows;
using static UnityEditor.Timeline.TimelinePlaybackControls;

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

    private PlayerInput playerInput;

    private void Awake()
    {        
        playerInput = new PlayerInput();
        ValiDateConstraints();
        context = new MovementContext(movementModel, statusController, playerInput, movementController, animationModel);
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

    private void ValiDateConstraints()
    {
        Assert.IsNotNull(movementModel, "PlayerMovementModel is Not Assigned");
        Assert.IsNotNull(statusController, "PlayerStatusController is Not Assigned");
        Assert.IsNotNull(playerInput, "PlayerInput is Not playerInput");
        Assert.IsNotNull(movementController, "PlayerMovementController is Not movementController");
        Assert.IsNotNull(animationModel, "PlayerAnimationModel is Not animationModel");
    }
    private void InitializeStates()
    {
        States.Add(EMovementState.Idle, new IdleState(context, EMovementState.Idle));
        States.Add(EMovementState.Walking, new WalkingState(context, EMovementState.Walking));
        States.Add(EMovementState.Crouching, new CrouchingState(context, EMovementState.Crouching));
        States.Add(EMovementState.Running, new RunningState(context, EMovementState.Running));
        States.Add(EMovementState.Jumping, new JumpingState(context, EMovementState.Jumping));
        States.Add(EMovementState.Dashing, new DashingState(context, EMovementState.Dashing));
        States.Add(EMovementState.Rolling, new RollState(context, EMovementState.Rolling));
        CurrentState = States[EMovementState.Idle];
    }
}

