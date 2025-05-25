using UnityEngine;

// Stunned State - Player cannot move or act
public class StunnedState : AvailabilityState
{
    public StunnedState(AvailabilityContext context) : base(context, EAvailabilityState.Stunned)
    {
    }

    public override void EnterState()
    {
        Debug.Log("Player is now Stunned");
        // Disable movement
        //if (Context.MovementStateMachine != null)
        //{
        //    Context.MovementStateMachine.enabled = false;
        //}
    }

    public override void ExitState()
    {
        Debug.Log("Player is no longer Stunned");
        // Re-enable movement if leaving stunned state
        //if (Context.MovementStateMachine != null)
        //{
        //    Context.MovementStateMachine.enabled = true;
        //}
    }

    public override void UpdateState()
    {
        // Stunned state logic - player cannot act
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
