using System;
using System.Collections.Generic;
using UnityEngine;
public abstract class MobMovementState : BaseState<MobMovementStateMachine.EMobMovementState>
{
    protected MobMovementContext Context;
    public MobMovementState(MobMovementContext context, MobMovementStateMachine.EMobMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }
}

