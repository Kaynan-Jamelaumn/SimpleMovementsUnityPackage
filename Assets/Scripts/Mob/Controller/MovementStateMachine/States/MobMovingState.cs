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

    //private IEnumerator WaitToReachDestination()
    //{
    //    // Record the start time for measuring the walk time.
    //    float startTime = Time.time;

    //    // Continue the loop until the destination is reached or the maximum walk time is exceeded.
    //    //while (navMeshAgent.pathPending || (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance))
    //    while (Context.MobReference.NavMeshAgentReference.pathPending || (Context.MobReference.NavMeshAgentReference.isActiveAndEnabled && Context.MobReference.NavMeshAgentReference.isOnNavMesh &&
    //    !HasReachedDestinationWithMargin()))

    //    {
    //        // If the maximum walk time is exceeded, reset the path and set the state to Idle.
    //        if (currentPlayerTarget != null && playerHasMaxChaseTime && Time.time - startTime >= maxWalkTime || currentPlayerTarget == null && Time.time - startTime >= maxWalkTime)
    //        {
    //            if (currentPredator) currentPredator = null;
    //            navMeshAgent.ResetPath();
    //            SetState(MobState.Idle);
    //            yield break;
    //        }

    //        // Check conditions to enter the Chase state during movement.
    //        CheckChaseConditions();

    //        yield return null;
    //    }

    //    // Destination has been reached, set the state to Idle.
    //    if (!currentPlayerTarget) SetState(MobState.Idle);
    //    else CheckChaseConditions();
    //}

}