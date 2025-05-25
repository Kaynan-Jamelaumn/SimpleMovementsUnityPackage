using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public class AbilitiesContext
{

    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    private AvailabilityStateMachine availabilityStateMachine;

    private List<AbilityAction> abilityAction;
    public AbilitiesContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController, AvailabilityStateMachine availabilityStateMachine, List<AbilityAction> abilityAction)
    {

        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.availabilityStateMachine = availabilityStateMachine;
        this.abilityAction = abilityAction;
    }

    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public AvailabilityStateMachine AvailabilityState => availabilityStateMachine;
    public List<AbilityAction> AbilityAction => abilityAction;

}

