using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class MobMovingState : MobMovementState
{
    public MobMovingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        if (!alreadyMoving) Context.MobReference.WaitToReachDestinationRoutine = Context.MobReference.StartCoroutine(WaitToReachDestinationRoutine());
    }
    public override void ExitState()

    {
    }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override
    MobMovementStateMachine.EMobMovementState GetNextState()
    {
        if (shouldChangeToIdleState)
        {
            shouldChangeToIdleState = false;
            return MobMovementStateMachine.EMobMovementState.Idle;
        }
        if (shouldChangeToPatrolState)
        {
            shouldChangeToPatrolState = false;
            return MobMovementStateMachine.EMobMovementState.Patrol;
        }
        if (shouldChangeToChasingState)
        {
            shouldChangeToChasingState = false;
            return MobMovementStateMachine.EMobMovementState.Chasing;
        }
        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

}