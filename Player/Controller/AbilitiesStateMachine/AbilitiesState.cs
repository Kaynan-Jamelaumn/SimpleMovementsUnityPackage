using System;
using System.Collections.Generic;
using UnityEngine;
public abstract class AbilitiesState : BaseState<AbilitiesStateMachine.EAbilitiesState>
{
    protected AbilitiesContext Context;
    public AbilitiesState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState stateKey) : base(stateKey)
    {
        Context = context;
    }
    protected bool IsAbilityTriggered(int index)
    {
        var abilityAction = Context?.AbilityAction?[index];
        return abilityAction?.abilityActionReference?.action?.triggered == true
               && abilityAction?.abilityStateMachine?.CurrentState?.StateKey == AbilityStateMachine.EAbilityState.Ready;
    }

    protected bool TriggeredAbility1() => Context.AbilityAction[0].abilityActionReference.action.triggered && Context.AbilityAction[0].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[0].abilityEffect != null;
    protected bool TriggeredAbility2() => Context.AbilityAction[1].abilityActionReference.action.triggered && Context.AbilityAction[1].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[1].abilityEffect != null;
    protected bool TriggeredAbility3() => Context.AbilityAction[2].abilityActionReference.action.triggered && Context.AbilityAction[2].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[2].abilityEffect != null;
    protected bool TriggeredAbility4() => Context.AbilityAction[3].abilityActionReference.action.triggered && Context.AbilityAction[3].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[3].abilityEffect != null;
    protected bool TriggeredAbility5() => Context.AbilityAction[4].abilityActionReference.action.triggered && Context.AbilityAction[4].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[4].abilityEffect != null;
    protected bool TriggeredAbility6() => Context.AbilityAction[5].abilityActionReference.action.triggered && Context.AbilityAction[5].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[5].abilityEffect != null;
    protected bool TriggeredAbility7() => Context.AbilityAction[6].abilityActionReference.action.triggered && Context.AbilityAction[6].abilityStateMachine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && Context.AbilityController.Abilities[6].abilityEffect != null;

}

