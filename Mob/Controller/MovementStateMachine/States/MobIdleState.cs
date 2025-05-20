using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Represents the idle state of the mob in the movement state machine.
/// </summary>
public class MobIdleState : MobMovementState
{
    /// <summary>
    /// Constructor for the MobIdleState.
    /// </summary>
    /// <param name="context">The context for the mob's movement state.</param>
    /// <param name="estate">The state key for the state.</param>
    public MobIdleState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }

    /// <summary>
    /// Called when entering the idle state.
    /// Plays the idle animation and starts the wait-to-move routine.
    /// </summary>
    public override void EnterState()
    {
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        Context.ActionsController.WaitToMoveRoutine = Context.ActionsController.StartCoroutine(WaitToMoveRoutine());
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        // GetNextState();
    }

    /// <summary>
    /// Determines the next state to transition to based on various conditions.
    /// </summary>
    /// <returns>The next state to transition to.</returns>
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
    public override void LateUpdateState() { }

    /// <summary>
    /// Coroutine that waits for the mob to move to a new location.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
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

            // Get a random destination on the NavMesh and set it for the mob.
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
            else
            {
                shouldChangeToPatrolState = true;
            }
        }
    }
}
