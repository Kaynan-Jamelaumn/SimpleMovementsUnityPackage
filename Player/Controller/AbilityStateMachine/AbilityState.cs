using System;
using System.Collections.Generic;
using UnityEngine;
public abstract class AbilityState : BaseState<AbilityStateMachine.EAbilityState>
{
    protected AbilityContext Context;
    public AbilityState(AbilityContext context, AbilityStateMachine.EAbilityState stateKey) : base(stateKey)
    {
        Context = context;
    }

}

