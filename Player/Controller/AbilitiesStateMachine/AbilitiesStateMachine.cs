using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class AbilitiesStateMachine : StateManager<AbilitiesStateMachine.EAbilitiesState>
{
    AbilitiesContext context;
    public enum EAbilitiesState
    {
        Available,
        Unavailable,
    }

    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private PlayerAnimationModel animationModel;
    [SerializeField] private List<AbilityAction> abilityAction = new List<AbilityAction>();
    private PlayerInput playerInput;
       
    private void Awake()
    {
        abilityController = this.CheckComponent(abilityController, nameof(abilityController));
        animationModel = this.CheckComponent(animationModel, nameof(animationModel));

        playerInput = new PlayerInput();


        context = new AbilitiesContext(playerInput, animationModel, abilityController, abilityAction);    
        InitializeStates();
        for (int i = 0; i < abilityAction.Count; i++)
        {
            if (abilityAction[i].AbilityStateMachine.AbilityHolder.abilityEffect != null)
            abilityAction[i].AbilityStateMachine.AbilityHolder.attackCast = abilityAction[i].AbilityStateMachine.AbilityHolder.abilityEffect.effects[0].attackCast;
        }

    }

    private void OnEnable()
    {
        playerInput.Player.Enable();
    }

    private void OnDisable()
    {
        playerInput.Player.Disable();
    }


    private void InitializeStates()
    {
        States.Add(EAbilitiesState.Available, new AvailableState(context, EAbilitiesState.Available));
        States.Add(EAbilitiesState.Unavailable, new UnavailableState(context, EAbilitiesState.Unavailable));
        CurrentState = States[EAbilitiesState.Available];
    }

    public AbilityAction FindAbilityActionByInputAction(InputAction inputAction)
    {
        // Search for the first AbilityAction that has a matching InputActionReference
        return abilityAction.FirstOrDefault(action =>
            action.abilityActionReference != null &&
            action.abilityActionReference.action == inputAction);
    }
}

