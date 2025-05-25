using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum EAvailabilityState
{
    UnnafectedState,      // Normal state
    Stunned,    // Cannot move or act
    Death       // Completely unavailable
}

public abstract class AvailabilityState : BaseState<EAvailabilityState>
{
    protected AvailabilityContext Context;


    public AvailabilityState(AvailabilityContext context, EAvailabilityState stateKey) : base(stateKey)
    {
        Context = context;
    }
}