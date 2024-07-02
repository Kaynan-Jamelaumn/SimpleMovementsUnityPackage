using System;
using System.Collections.Generic;

public class AbilityContext
{
    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    public AbilityContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController) 
    {

        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
    }

    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;

}

