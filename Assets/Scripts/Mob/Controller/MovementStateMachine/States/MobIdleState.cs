using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.LowLevel;
public class MobIdleState : MobMovementState
{
    public MobIdleState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        Context.ActionsController.WaitToMoveRoutine = Context.ActionsController.StartCoroutine(WaitToMoveRoutine());
    }

    public override void ExitState() { }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override MobMovementStateMachine.EMobMovementState GetNextState()
    {
        if (shouldChangeToMovingState)
        {
            shouldChangeToMovingState = false;
            return MobMovementStateMachine.EMobMovementState.Moving;
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
    private IEnumerator WaitToMoveRoutine()
    {

        // Check if the player is within the detection range.
        if (Context.MobReference.CurrentPlayerTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPlayerTarget.transform.position) <= Context.MobReference.DetectionRange)
        {
            // Set the destination to the player's position.
            Context.MobReference.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentPlayerTarget.transform.position);
            shouldChangeToMovingState = true;
        }
        else
        {
            // If the player is not in range, move to a random destination.
            float waitTime = Random.Range(Context.MobReference.IdleTime / 2, Context.MobReference.IdleTime * 2);
            yield return new WaitForSeconds(waitTime);

            // Get a random destination on the NavMesh and set it for the Animal.
            if (Context.MobReference.PatrolPoints.Length == 0)
            {
                Vector3 randomDestination = Context.MobReference.GetRandomNavMeshPosition(Context.MobReference.TransformReference.position, Context.MobReference.WanderDistance);
                Context.MobReference.NavMeshAgentReference.SetDestination(randomDestination);
                if (!Context.MobReference.CurrentPlayerTarget)
                {
                    shouldChangeToMovingState = true;
                    alreadyMoving = true;
                }
            }
            else shouldChangeToPatrolState = true;
        }
    }
}
