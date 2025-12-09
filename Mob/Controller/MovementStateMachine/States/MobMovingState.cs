using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Represents the moving state of the mob in the movement state machine.
///  with dynamic path adjustment and obstacle avoidance.
/// </summary>
public class MobMovingState : MobMovementState
{
    private float pathRecalculationInterval = 1f;
    private float lastPathRecalculation = 0f;

    public MobMovingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }

    public override void EnterState()
    {
        if (Context.Anim != null)
        {
            Context.Anim.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        }

        if (!alreadyMoving)
        {
            if (Context.MobReference != null && Context.MobReference.WaitToReachDestinationRoutine != null)
            {
                Context.MobReference.StopCoroutine(Context.MobReference.WaitToReachDestinationRoutine);
            }

            if (Context.MobReference != null)
            {
                Context.MobReference.WaitToReachDestinationRoutine =
                    Context.MobReference.StartCoroutine(WaitToReachDestinationRoutine());
            }
        }
        lastPathRecalculation = Time.time;
    }

    public override void ExitState()
    {
        alreadyMoving = false;
        lastPathRecalculation = 0f;
    }

    public override void UpdateState()
    {
        // Continuously check for opportunities or threats while moving
        if (Time.time - lastPathRecalculation >= pathRecalculationInterval)
        {
            CheckChaseConditions();
            OptimizePath();
            lastPathRecalculation = Time.time;
        }
    }

    public override MobMovementStateMachine.EMobMovementState GetNextState()
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

    /// <summary>
    /// Optimizes the current path if conditions warrant it.
    /// </summary>
    private void OptimizePath()
    {
        if (Context.NavMeshAgentReference == null) return;

        // Check if we're moving efficiently
        if (Context.NavMeshAgentReference.hasPath && !Context.NavMeshAgentReference.pathPending)
        {
            // If we're moving too slowly or path seems inefficient, recalculate
            if (Context.NavMeshAgentReference.velocity.magnitude < Context.NavMeshAgentReference.speed * 0.5f)
            {
                // Check if we're actually stuck or just starting/stopping
                if (Context.NavMeshAgentReference.remainingDistance > Context.NavMeshAgentReference.stoppingDistance + 1f)
                {
                    // Try to find a better path
                    Vector3 currentDestination = Context.NavMeshAgentReference.destination;

                    // Validate the destination is still valid
                    if (NavMesh.SamplePosition(currentDestination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    {
                        SafeSetDestination(hit.position);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates the utility of continuing movement behavior.
    /// </summary>
    /// <returns>Utility score for current behavior.</returns>
    protected override float EvaluateCurrentBehavior()
    {
        if (Context.NavMeshAgentReference == null) return 0.3f;

        // Moving towards a goal is generally productive
        if (Context.NavMeshAgentReference.hasPath &&
            Context.NavMeshAgentReference.remainingDistance > Context.NavMeshAgentReference.stoppingDistance)
        {
            return 0.6f; // Good to continue moving when we have a valid destination
        }

        return 0.3f; // Lower utility if path is unclear
    }
}