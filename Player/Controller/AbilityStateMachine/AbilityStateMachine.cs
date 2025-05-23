using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using static AbilityStateMachine;




public class AbilityStateMachine : StateManager<AbilityStateMachine.EAbilityState>
{
    private AbilityContext context;
    public AbilityContext Context => context;
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
    [SerializeField] private PlayerAbilityHolder abilityHolder;


    public bool Available()
    {
        return context.cachedAvailability;
    }


    public PlayerInput playerInput;
    public PlayerAbilityHolder AbilityHolder { get => abilityHolder; set => abilityHolder = value; }

    private void Awake()
    {
        abilityController = this.CheckComponent(abilityController, nameof(abilityController));
        animationModel = this.CheckComponent(animationModel, nameof(animationModel));

        playerInput = new PlayerInput();
        context = new AbilityContext(playerInput, animationModel, abilityController, abilityHolder);
        InitializeStates();
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
        States.Add(EAbilityState.Ready, new ReadyState(context, EAbilityState.Ready));
        States.Add(EAbilityState.Casting, new CastingState(context, EAbilityState.Casting));
        States.Add(EAbilityState.Launching, new LaunchingState(context, EAbilityState.Launching));
        States.Add(EAbilityState.Active, new ActiveState(context, EAbilityState.Active));
        States.Add(EAbilityState.InCooldown, new InCooldownState(context, EAbilityState.InCooldown));
        CurrentState = States[EAbilityState.Ready];
    }



}

