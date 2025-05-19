using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
public class AbilityStateMachine : StateManager<AbilityStateMachine.EAbilityState>
{
    AbilityContext context;
    public enum EAbilityState
    {
        Ready,
        Casting,
        Launching,
        Active,
        InCooldown
    }
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private PlayerAnimationModel animationModel;

    public PlayerInput playerInput;


    private void Awake()
    {
        playerInput = new PlayerInput();
        //ValiDateConstraints();
        context = new AbilityContext(playerInput, animationModel, abilityController);
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
        Assert.IsNotNull(playerInput, "PlayerInput is Not playerInput");
        Assert.IsNotNull(animationModel, "PlayerAnimationModel is Not animationModel");
    }
    private void InitializeStates()
    {
        States.Add(EAbilityState.Ready, new ReadyState(context, EAbilityState.Ready));
        States.Add(EAbilityState.Casting, new CastingState(context, EAbilityState.Casting));
        States.Add(EAbilityState.Launching, new LaunchingState(context, EAbilityState.Launching));
        States.Add(EAbilityState.Active, new ActiveState(context, EAbilityState.Active));
        States.Add(EAbilityState.InCooldown, new InCooldownState(context, EAbilityState.InCooldown));
        CurrentState = States[EAbilityState.Ready];
    }
}

