using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Represents the patrol state of the mob in the movement state machine.
///  with intelligent patrol point selection and threat awareness during patrol.
/// </summary>
public class MobPatrolState : MobMovementState
{
    private float lastPatrolCheck = 0f;
    private float patrolCheckInterval = 0.5f;

    /// <summary>
    /// Constructor for the MobPatrolState.
    /// </summary>
    /// <param name="context">The context for the mob's movement state.</param>
    /// <param name="estate">The state key for the state.</param>
    public MobPatrolState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }

    /// <summary>
    /// Called when entering the patrol state.
    /// Sets the destination to the next patrol point and starts the wait-to-reach-destination routine.
    /// </summary>
    public override void EnterState()
    {
        if (Context.MobReference.PatrolPoints == null || Context.MobReference.PatrolPoints.Length == 0)
        {
            // No patrol points set, transition to idle
            shouldChangeToIdleState = true;
            return;
        }

        if (Context.Anim != null)
        {
            Context.Anim.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        }

        // Select best patrol point considering current situation
        int targetPatrolPoint = SelectNextPatrolPoint();
        Context.MobReference.CurrentPatrolPoint = targetPatrolPoint;

        // Validate patrol point exists
        if (targetPatrolPoint >= Context.MobReference.PatrolPoints.Length)
        {
            Context.MobReference.CurrentPatrolPoint = 0;
        }

        // Use safe method to set destination
        if (SafeSetDestination(Context.MobReference.PatrolPoints[Context.MobReference.CurrentPatrolPoint]))
        {
            if (Context.MobReference.WaitToReachDestinationRoutine != null)
            {
                Context.MobReference.StopCoroutine(Context.MobReference.WaitToReachDestinationRoutine);
            }

            Context.MobReference.WaitToReachDestinationRoutine =
                Context.MobReference.StartCoroutine(WaitToReachDestinationRoutine());
        }
        else
        {
            // Failed to set destination, go to idle
            shouldChangeToIdleState = true;
            return;
        }

        if (Context.MobReference.HasReachedDestinationWithMargin())
        {
            Context.MobReference.CurrentPatrolPoint =
                (Context.MobReference.CurrentPatrolPoint + 1) % Context.MobReference.PatrolPoints.Length;
        }

        lastPatrolCheck = Time.time;
    }

    public override void ExitState()
    {
        lastPatrolCheck = 0f;
    }


    public override void UpdateState()
    {
        // Periodic awareness checks while patrolling
        if (Time.time - lastPatrolCheck >= patrolCheckInterval)
        {
            CheckChaseConditions(); // Stay alert for threats/prey
            lastPatrolCheck = Time.time;
        }
    }

    public override MobMovementStateMachine.EMobMovementState GetNextState()
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
    public override void LateUpdateState() { }

    /// <summary>
    /// Selects the next patrol point based on current conditions and proximity.
    /// </summary>
    /// <returns>Index of the selected patrol point.</returns>
    private int SelectNextPatrolPoint()
    {
        if (Context.MobReference.PatrolPoints == null || Context.MobReference.PatrolPoints.Length == 0)
            return 0;

        // Default behavior: sequential patrol
        int nextPoint = (Context.MobReference.CurrentPatrolPoint + 1) % Context.MobReference.PatrolPoints.Length;

        // Check if we should skip to a different patrol point based on threats
        // This creates more dynamic and realistic patrol behavior
        float closestThreatDistance = float.MaxValue;

        if (Context.MobReference.DetectionCast != null)
        {
            Collider[] nearbyObjects = Context.MobReference.DetectionCast.DetectObjects(Context.MobReference.TransformReference);

            if (nearbyObjects != null)
            {
                foreach (var collider in nearbyObjects)
                {
                    if (collider == null) continue;

                    MobActionsController potentialThreat = collider.GetComponent<MobActionsController>();
                    if (potentialThreat != null && potentialThreat.PreysReference != null &&
                        potentialThreat.PreysReference.Contains(Context.MobReference.type))
                    {
                        float distance = Vector3.Distance(Context.MobReference.TransformReference.position,
                                                          potentialThreat.transform.position);
                        if (distance < closestThreatDistance)
                        {
                            closestThreatDistance = distance;

                            // Remember this threat
                            if (!threatMemory.ContainsKey(potentialThreat.gameObject))
                            {
                                threatMemory[potentialThreat.gameObject] = Time.time;
                            }
                        }
                    }
                }
            }
        }

        // If there's a nearby threat, prefer patrol points further from current position
        if (closestThreatDistance < Context.MobReference.DetectionRange * 0.7f)
        {
            float maxDistance = 0f;
            int safestPoint = nextPoint;

            for (int i = 0; i < Context.MobReference.PatrolPoints.Length; i++)
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position,
                                                  Context.MobReference.PatrolPoints[i]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    safestPoint = i;
                }
            }

            return safestPoint;
        }

        return nextPoint;
    }

    /// <summary>
    /// Evaluates the utility of continuing patrol behavior.
    /// </summary>
    /// <returns>Utility score for current behavior.</returns>
    protected override float EvaluateCurrentBehavior()
    {
        // Patrolling is valuable for area coverage and threat detection
        if (Context.MobReference.PatrolPoints != null && Context.MobReference.PatrolPoints.Length > 0)
        {
            return 0.7f; // Good utility when patrol points are available
        }

        return 0.2f; // Low utility if no patrol points
    }
}