using UnityEngine;

public class UnnafectedState : AvailabilityState
{
    public UnnafectedState(AvailabilityContext context) : base(context, EAvailabilityState.UnnafectedState)
    {
    }

    public override void EnterState()
    {
        Debug.Log("Player is now Ready");
        // Enable movement if not silenced
        //if (Context.MovementStateMachine != null)
        //{
        //    Context.MovementStateMachine.enabled = true;
        //}
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