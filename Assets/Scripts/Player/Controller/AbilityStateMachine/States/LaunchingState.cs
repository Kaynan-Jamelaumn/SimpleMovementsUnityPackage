using UnityEngine;

public class LaunchingState : AbilityState
{
    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        //if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        //if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        //if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        //if (IsMoving()) return MovementStateMachine.EMovementState.Walking;

        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}