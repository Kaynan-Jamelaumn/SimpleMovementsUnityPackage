using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;

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
    PlayerAbilityController playerAbilityController;
    private PlayerInput playerInput;


    private void Awake()
    {
        playerInput = new PlayerInput();
        ValiDateConstraints();

        playerAbilityController = GetComponent<PlayerAbilityController>();
        InitializeAbilityActions(playerAbilityController);


        context = new AbilitiesContext(playerInput, animationModel, abilityController, abilityAction);    
        InitializeStates();

    }
    private void InitializeAbilityActions(PlayerAbilityController playerAbilityController)
    {
        for (int i = 0; i < playerAbilityController.Abilities.Count; i++)
        {
            var abilityStateMachine = this.AddComponent<AbilityStateMachine>();
            var action = new AbilityAction(playerAbilityController.Abilities[i].AbilityActionReference, abilityStateMachine);
            abilityAction.Add(action);
        }
    }



    private void Start()
    {

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
        Assert.IsNotNull(playerInput, "PlayerInput is Not playerInput");
        Assert.IsNotNull(animationModel, "PlayerAnimationModel is Not animationModel");
    }
    private void InitializeStates()
    {
        States.Add(EAbilitiesState.Available, new AvailableState(context, EAbilitiesState.Available));
        States.Add(EAbilitiesState.Unavailable, new UnavailableState(context, EAbilitiesState.Unavailable));
        CurrentState = States[EAbilitiesState.Available];
    }
}

