using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Represents the idle state of the mob in the movement state machine.
///  with intelligent perception, threat assessment, and proactive behavior initiation.
/// </summary>
public class MobIdleState : MobMovementState
{
    private float perceptionCheckInterval = 0.5f; // Check surroundings twice per second
    private float lastPerceptionCheck = 0f;
    private bool waitingForNavMesh = false; // Track if we're waiting for NavMesh to become available

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
    /// Plays the idle animation and starts the wait-to-move routine with perception checks.
    /// </summary>
    public override void EnterState()
    {
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        Context.ActionsController.WaitToMoveRoutine = Context.ActionsController.StartCoroutine(WaitToMoveRoutine());
    }

    public override void ExitState()
    {
        lastPerceptionCheck = 0f;
        waitingForNavMesh = false; // Reset flag when leaving idle
    }

    public override void UpdateState()
    {
        // Periodic perception check even while idle
        if (Time.time - lastPerceptionCheck >= perceptionCheckInterval)
        {
            PerformPerceptionCheck();
            lastPerceptionCheck = Time.time;
        }

        // If waiting for NavMesh, check if it's available now
        if (waitingForNavMesh && Context.NavMeshAgentReference != null)
        {
            if (Context.NavMeshAgentReference.isActiveAndEnabled &&
                Context.NavMeshAgentReference.isOnNavMesh)
            {
                // NavMesh is now available! Try to start moving
                Debug.Log($"[{Context.MobReference.name}] NavMesh detected! Starting movement.");
                waitingForNavMesh = false;

                // Restart the idle routine to trigger movement
                if (Context.ActionsController.WaitToMoveRoutine != null)
                {
                    Context.ActionsController.StopCoroutine(Context.ActionsController.WaitToMoveRoutine);
                }
                Context.ActionsController.WaitToMoveRoutine = Context.ActionsController.StartCoroutine(WaitToMoveRoutine());
            }
        }
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
    /// Performs a perception check to detect threats and opportunities in the environment.
    /// </summary>
    private void PerformPerceptionCheck()
    {
        // Quick check for immediate threats or targets
        Collider[] nearbyObjects = Context.MobReference.DetectionCast.DetectObjects(Context.MobReference.TransformReference);

        float highestThreatLevel = 0f;
        float bestOpportunity = 0f;

        foreach (var collider in nearbyObjects)
        {
            // Check for predators (threats)
            MobActionsController potentialPredator = collider.GetComponent<MobActionsController>();
            if (potentialPredator != null && potentialPredator.PreysReference.Contains(Context.MobReference.type))
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position, potentialPredator.transform.position);
                float threatLevel = 1f - Mathf.Clamp01(distance / Context.MobReference.DetectionRange);

                if (threatLevel > highestThreatLevel)
                {
                    highestThreatLevel = threatLevel;
                }
            }

            // Check for prey (opportunities)
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();
            if (player != null && Context.MobReference.PreysReference.Contains("Player"))
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position, player.transform.position);
                float opportunity = 1f - Mathf.Clamp01(distance / Context.MobReference.DetectionRange);

                if (opportunity > bestOpportunity)
                {
                    bestOpportunity = opportunity;
                }
            }
        }

        // React to threats with higher priority
        if (highestThreatLevel > 0.6f)
        {
            CheckChaseConditions(); // This will handle fleeing if needed
        }
        // React to opportunities
        else if (bestOpportunity > 0.5f)
        {
            CheckChaseConditions(); // This will initiate chase if appropriate
        }
    }

    /// <summary>
    /// Coroutine that waits for the mob to move to a new location with intelligent decision-making.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator WaitToMoveRoutine()
    {
        // Immediate perception check on entering idle
        PerformPerceptionCheck();

        // Check if there's an immediate threat or target
        if (shouldChangeToChasingState)
        {
            yield break; // Exit early if we need to chase
        }

        // Check if the player is within the detection range.
        if (Context.MobReference.CurrentPlayerTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPlayerTarget.transform.position) <= Context.MobReference.DetectionRange)
        {
            // Set the destination to the player's position using safe method.
            SafeSetDestination(Context.MobReference.CurrentPlayerTarget.transform.position);
            shouldChangeToMovingState = true;
        }
        else
        {
            // Calculate wait time with some variation for natural behavior
            float baseWaitTime = Context.MobReference.IdleTime;
            float variation = Random.Range(-0.3f, 0.3f);
            float waitTime = baseWaitTime * (1f + variation);

            float elapsedTime = 0f;

            // Wait with periodic checks instead of single WaitForSeconds
            while (elapsedTime < waitTime)
            {
                yield return new WaitForSeconds(perceptionCheckInterval);
                elapsedTime += perceptionCheckInterval;

                // Check if something important happened during wait
                if (shouldChangeToChasingState)
                {
                    yield break;
                }
            }

            // Decide next action after waiting
            DecideNextAction();
        }
    }

    /// <summary>
    /// Decides the next action for the mob using utility-based evaluation.
    /// </summary>
    private void DecideNextAction()
    {
        // Evaluate different behavior options
        float wanderUtility = EvaluateWanderUtility();
        float patrolUtility = EvaluatePatrolUtility();
        float stayIdleUtility = EvaluateStayIdleUtility();

        // Choose action with highest utility
        if (patrolUtility > wanderUtility && patrolUtility > stayIdleUtility)
        {
            shouldChangeToPatrolState = true;
        }
        else if (wanderUtility > stayIdleUtility)
        {
            InitiateWander();
        }
        // else stay idle (do nothing, will loop back to WaitToMoveRoutine)
    }

    /// <summary>
    /// Evaluates the utility of wandering behavior.
    /// </summary>
    /// <returns>Utility score for wandering.</returns>
    private float EvaluateWanderUtility()
    {
        // Wandering is good when there are no patrol points and we're not currently engaged
        if (Context.MobReference.PatrolPoints.Length > 0)
            return 0.3f; // Lower utility if patrol points exist

        // Check if we've been in same area too long
        return 0.7f; // Default wander utility
    }

    /// <summary>
    /// Evaluates the utility of patrol behavior.
    /// </summary>
    /// <returns>Utility score for patrolling.</returns>
    private float EvaluatePatrolUtility()
    {
        // Patrolling is highly valuable when patrol points are set
        if (Context.MobReference.PatrolPoints.Length == 0)
            return 0f;

        return 0.8f; // High utility for patrol when points exist
    }

    /// <summary>
    /// Evaluates the utility of staying idle.
    /// </summary>
    /// <returns>Utility score for staying idle.</returns>
    private float EvaluateStayIdleUtility()
    {
        // Sometimes it's good to just rest
        return 0.4f;
    }

    /// <summary>
    /// Initiates wandering behavior by setting a random destination.
    /// </summary>
    private void InitiateWander()
    {
        // Get a random destination on the NavMesh and set it for the mob.
        Vector3 randomDestination = Context.MobReference.GetRandomNavMeshPosition(
            Context.MobReference.TransformReference.position,
            Context.MobReference.WanderDistance);

        // Use safe method to set destination
        if (!SafeSetDestination(randomDestination))
        {
            // Failed to set destination - probably not on NavMesh yet
            // Set flag so we'll retry when NavMesh becomes available
            waitingForNavMesh = true;
            Debug.Log($"[{Context.MobReference.name}] Waiting for NavMesh... (mob might be falling or NavMesh not baked)");
            shouldChangeToIdleState = true;
            return;
        }

        // Successfully set destination
        waitingForNavMesh = false;

        if (!Context.MobReference.CurrentPlayerTarget)
        {
            shouldChangeToMovingState = true;
            alreadyMoving = true;
        }
    }

    /// <summary>
    /// Evaluates the utility of continuing idle behavior.
    /// </summary>
    /// <returns>Utility score for current behavior.</returns>
    protected override float EvaluateCurrentBehavior()
    {
        // Idle is generally a neutral state - not actively good or bad
        // But becomes less desirable over time
        return 0.4f;
    }
}