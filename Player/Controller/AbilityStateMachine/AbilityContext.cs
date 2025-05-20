using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public class AbilityContext
{
    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    private PlayerAbilityHolder abilityHolder;

    public bool cachedAvailability;

    public AbilityContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController, PlayerAbilityHolder abilityHolder) 
    {

        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.abilityHolder = abilityHolder;
    }

    public event Action<bool> AvailabilityChanged;

    public void SetCachedAvailability(bool value)
    {
        if (cachedAvailability != value)
        {
            cachedAvailability = value;
            AvailabilityChanged?.Invoke(value);  // Broadcasts the new availability
        }
    }

    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public PlayerAbilityHolder AbilityHolder => abilityHolder;

}

