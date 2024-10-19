using UnityEngine;

public class MobIdleState : MobMovementState
{
    public MobIdleState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }
    public override void EnterState()
    {
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override MobMovementStateMachine.EMobMovementState GetNextState()
    {

        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}