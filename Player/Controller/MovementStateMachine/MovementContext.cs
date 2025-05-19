using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public class MovementContext
{
    private PlayerMovementModel movementModel;
    private PlayerStatusController statusController;
    private PlayerInput playerInput;
    private PlayerMovementController movementController;
    private PlayerAnimationModel animationModel;
    public MovementContext(PlayerMovementModel movementModel, PlayerStatusController statusController, PlayerInput playerInput, PlayerMovementController movementController, PlayerAnimationModel animationModel) {
        this.movementModel = movementModel;
        this.statusController = statusController;
        this.playerInput = playerInput;
        this.movementController = movementController;
        this.animationModel = animationModel;
    }
    public PlayerMovementModel MovementModel => movementModel;
    public PlayerStatusController StatusController => statusController;
    public PlayerInput PlayerInput => playerInput;
    public PlayerMovementController MovementController => movementController;
    public PlayerAnimationModel AnimationModel => animationModel;

}

