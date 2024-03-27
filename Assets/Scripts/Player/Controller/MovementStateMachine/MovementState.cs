using System;
using System.Collections.Generic;

public abstract class MovementState : BaseState<MovementStateMachine.EMovementState>
{
    protected MovementContext Context;
    public MovementState(MovementContext context, MovementStateMachine.EMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }
}

