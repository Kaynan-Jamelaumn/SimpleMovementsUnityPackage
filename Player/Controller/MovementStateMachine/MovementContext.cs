using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        PlayerCameraModel cameraModel, PlayerCameraController cameraController, AvailabilityStateMachine availabilityStateMachine) {

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
    public PlayerInput PlayerInput => playerInput;
    public PlayerMovementController MovementController => movementController;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerCameraModel CameraModel => cameraModel;
    public PlayerCameraController CameraController => cameraController;
    public AvailabilityStateMachine AvailabilityState => availabilityStateMachine;
}

