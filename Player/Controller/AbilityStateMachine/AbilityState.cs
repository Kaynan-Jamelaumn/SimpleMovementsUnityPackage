using System;
using System.Collections.Generic;
using UnityEngine;
using static AbilityStateMachine;
public abstract class AbilityState : BaseState<AbilityStateMachine.EAbilityState>
{
    protected AbilityContext Context;
    public AbilityState(AbilityContext context, AbilityStateMachine.EAbilityState stateKey) : base(stateKey)
    {
        Context = context;
    }

    

    public bool Available()
    {
        return Context.cachedAvailability;
    }

    protected void RecalculateAvailability(EAbilityState stateKey)
    {
        bool isAvailable;
        if (Context.AbilityHolder.abilityEffect.StateAvailabilityDict.TryGetValue(stateKey, out bool availability))
            isAvailable = availability;
        else
            isAvailable = true;

        Context.SetCachedAvailability(isAvailable);
    }
}

