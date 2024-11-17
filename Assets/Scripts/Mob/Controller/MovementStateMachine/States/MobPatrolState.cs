using UnityEngine;
using UnityEngine.AI;

public class MobPatrolState : MobMovementState
{
    public MobPatrolState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }
    public override void EnterState()
    {
        if (Context.MobReference.PatrolPoints.Length == 0) return; // No patrol points set

        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        Context.MobReference.NavMeshAgentReference.SetDestination(Context.MobReference.PatrolPoints[Context.MobReference.CurrentPatrolPoint]);
        Context.MobReference.WaitToReachDestinationRoutine = Context.MobReference.StartCoroutine(WaitToReachDestinationRoutine());
     

        if (Context.MobReference.HasReachedDestinationWithMargin())
        {
            Context.MobReference.CurrentPatrolPoint = (Context.MobReference.CurrentPatrolPoint + 1) % Context.MobReference.PatrolPoints.Length;
            // SetState(MobState.Idle); // Continue to the next patrol point
        }

    }
    public override void ExitState()
    {

    }
    public override void UpdateState()
    {
        // GetNextState();
    }
    public override
    MobMovementStateMachine.EMobMovementState GetNextState()
    {
        if (shouldChangeToIdleState)
        {
            shouldChangeToIdleState = false;
            return MobMovementStateMachine.EMobMovementState.Idle;
        }
        if (shouldChangeToMovingState)
        {
            shouldChangeToMovingState = false;
            return MobMovementStateMachine.EMobMovementState.Moving;
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

}