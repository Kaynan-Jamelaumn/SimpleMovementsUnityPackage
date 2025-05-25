using UnityEngine;


// Death State - Player is completely unavailable
public class DeathState : AvailabilityState
{
    public DeathState(AvailabilityContext context) : base(context, EAvailabilityState.Death)
    {
    }

    public override void EnterState()
    {
        Debug.Log("Player has died");
        // Disable all systems
        //if (Context.MovementStateMachine != null)
        //{
        //    Context.MovementStateMachine.enabled = false;
        //}
    }

    public override void ExitState()
    {
        Debug.Log("Player has been revived");
    }

    public override void UpdateState()
    {
        // Death state logic
    }

    public override EAvailabilityState GetNextState()
    {
        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
