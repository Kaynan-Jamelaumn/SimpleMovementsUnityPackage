using UnityEngine;

public class UnnafectedState : AvailabilityState
{
    public UnnafectedState(AvailabilityContext context) : base(context, EAvailabilityState.UnnafectedState)
    {
    }

    public override void EnterState()
    {

    }

    public override void ExitState()
    {
    }

    public override void UpdateState()
    {
        // Ready state logic
    }

    public override EAvailabilityState GetNextState()
    {
        // This will be handled by the AvailabilityStateMachine
        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}