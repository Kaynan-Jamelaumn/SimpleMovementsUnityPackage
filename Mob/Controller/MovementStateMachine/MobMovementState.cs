using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base class representing a state in the mob's movement state machine.
///  with utility-based decision making for intelligent state transitions.
/// </summary>
public abstract class MobMovementState : BaseState<MobMovementStateMachine.EMobMovementState>
{
    protected MobMovementContext Context;
    protected bool alreadyMoving = false;
    protected Vector3 playerPosition;
    protected bool shouldChangeToMovingState;
    protected bool shouldChangeToPatrolState;
    protected bool shouldChangeToIdleState;
    protected bool shouldChangeToChasingState;

    // Utility weights for decision making
    protected const float URGENCY_WEIGHT = 0.4f;
    protected const float DISTANCE_WEIGHT = 0.3f;
    protected const float SAFETY_WEIGHT = 0.3f;

    //  perception parameters
    protected const float THREAT_MEMORY_DURATION = 5f;
    protected const float OPPORTUNITY_MEMORY_DURATION = 3f;
    protected Dictionary<GameObject, float> threatMemory = new Dictionary<GameObject, float>();
    protected Dictionary<GameObject, float> opportunityMemory = new Dictionary<GameObject, float>();

    /// <summary>
    /// Safely sets the NavMeshAgent destination with validation checks.
    /// </summary>
    /// <param name="destination">Target destination position.</param>
    /// <returns>True if destination was set successfully, false otherwise.</returns>
    protected bool SafeSetDestination(Vector3 destination)
    {
        // Validate NavMeshAgent is ready
        if (Context.NavMeshAgentReference == null)
        {
            Debug.LogError($"[{Context.MobReference?.name}] NavMeshAgent is null!");
            return false;
        }

        if (!Context.NavMeshAgentReference.isActiveAndEnabled)
        {
            Debug.LogWarning($"[{Context.MobReference.name}] NavMeshAgent is not active. Skipping SetDestination.");
            return false;
        }

        if (!Context.NavMeshAgentReference.isOnNavMesh)
        {
            Debug.LogWarning($"[{Context.MobReference.name}] NavMeshAgent is not on NavMesh! " +
                           $"Make sure NavMesh is baked (Window → AI → Navigation → Bake) " +
                           $"and the mob is placed on the NavMesh surface.");
            return false;
        }

        // All checks passed, set destination
        Context.NavMeshAgentReference.SetDestination(destination);
        return true;
    }

    /// <summary>
    /// Constructor for the mob movement state.
    /// </summary>
    /// <param name="context">The context for the mob's movement state.</param>
    /// <param name="stateKey">The state key for the state.</param>
    public MobMovementState(MobMovementContext context, MobMovementStateMachine.EMobMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }

    /// <summary>
    /// Coroutine that waits for the mob to reach its destination with improved path monitoring.
    /// </summary>
    public IEnumerator WaitToReachDestinationRoutine()
    {
        // Record the start time for measuring the walk time.
        float startTime = Time.time;
        Vector3 lastPosition = Context.MobReference.TransformReference.position;
        float stuckTimer = 0f;
        float perceptionCheckTimer = 0f;
        const float PERCEPTION_INTERVAL = 0.3f;
        const float STUCK_THRESHOLD = 0.1f;
        const float STUCK_TIME = 2f;

        // Continue the loop until the destination is reached or the maximum walk time is exceeded.
        while (Context.NavMeshAgentReference.pathPending ||
               (Context.NavMeshAgentReference.isActiveAndEnabled &&
                Context.NavMeshAgentReference.isOnNavMesh &&
                !Context.MobReference.HasReachedDestinationWithMargin()))
        {
            //  periodic perception check
            perceptionCheckTimer += Time.deltaTime;
            if (perceptionCheckTimer >= PERCEPTION_INTERVAL)
            {
                UpdateMemory();
                CheckChaseConditions();
                perceptionCheckTimer = 0f;
            }

            // Check if stuck (not moving significantly)
            float distanceMoved = Vector3.Distance(lastPosition, Context.MobReference.TransformReference.position);
            if (distanceMoved < STUCK_THRESHOLD)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > STUCK_TIME)
                {
                    // Try to find alternative path using smart pathfinding
                    Vector3 alternativeDestination = FindAlternativePath(Context.NavMeshAgentReference.destination);
                    if (SafeSetDestination(alternativeDestination))
                    {
                        stuckTimer = 0f;
                    }
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = Context.MobReference.TransformReference.position;
            }

            // Check if maximum walk time is exceeded
            bool timeExceeded = false;
            if (Context.MobReference.CurrentPlayerTarget != null && Context.MobReference.PlayerHasMaxChaseTime)
            {
                timeExceeded = Time.time - startTime >= Context.MobReference.MaxWalkTime;
            }
            else if (Context.MobReference.CurrentPlayerTarget == null)
            {
                timeExceeded = Time.time - startTime >= Context.MobReference.MaxWalkTime;
            }

            if (timeExceeded)
            {
                if (Context.MobReference.CurrentPredator != null)
                {
                    Context.MobReference.CurrentPredator = null;
                }
                Context.NavMeshAgentReference.ResetPath();
                shouldChangeToIdleState = true;
                yield break;
            }

            // Check conditions to enter the Chase state during movement.
            if (shouldChangeToChasingState)
                yield break;

            yield return null;
        }

        // Destination has been reached, set the state to Idle.
        if (!Context.MobReference.CurrentPlayerTarget)
        {
            shouldChangeToIdleState = true;
        }
        else
        {
            CheckChaseConditions();
        }
    }

    /// <summary>
    /// Finds an alternative path when stuck, using intelligent pathfinding.
    /// </summary>
    /// <param name="originalDestination">The original destination that couldn't be reached.</param>
    /// <returns>Alternative destination position.</returns>
    protected Vector3 FindAlternativePath(Vector3 originalDestination)
    {
        // Try multiple angles to find a clear path
        for (int angle = 30; angle <= 180; angle += 30)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * (originalDestination - Context.MobReference.TransformReference.position).normalized;
            Vector3 testPosition = Context.MobReference.TransformReference.position + direction * 5f;

            if (NavMesh.SamplePosition(testPosition, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                // Check if this path is actually navigable
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(Context.MobReference.TransformReference.position, hit.position, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                        return hit.position;
                }
            }
        }

        // Fallback: random position nearby
        return Context.MobReference.GetRandomNavMeshPosition(
            Context.MobReference.TransformReference.position, 5f);
    }

    /// <summary>
    /// Updates memory of threats and opportunities, removing outdated entries.
    /// </summary>
    protected void UpdateMemory()
    {
        float currentTime = Time.time;
        List<GameObject> expiredThreats = new List<GameObject>();
        List<GameObject> expiredOpportunities = new List<GameObject>();

        foreach (var kvp in threatMemory)
        {
            if (kvp.Key == null || currentTime - kvp.Value > THREAT_MEMORY_DURATION)
                expiredThreats.Add(kvp.Key);
        }

        foreach (var kvp in opportunityMemory)
        {
            if (kvp.Key == null || currentTime - kvp.Value > OPPORTUNITY_MEMORY_DURATION)
                expiredOpportunities.Add(kvp.Key);
        }

        foreach (var expired in expiredThreats)
            threatMemory.Remove(expired);

        foreach (var expired in expiredOpportunities)
            opportunityMemory.Remove(expired);
    }

    /// <summary>
    /// Checks the conditions for entering the Chase state using utility-based evaluation with memory.
    /// </summary>
    public void CheckChaseConditions()
    {
        // If already chasing a target or being chased by a predator, do nothing.
        if (Context.MobReference.CurrentChaseTarget != null ||
            Context.MobReference.CurrentPredator != null ||
            Context.MobReference.CurrentPlayerTarget != null)
            return;

        // Detect objects in the mob's detection range.
        Collider[] detectedObjects = Context.MobReference.DetectionCast.DetectObjects(Context.MobReference.TransformReference);

        Transform bestTarget = null;
        float bestUtility = 0f;
        bool isPlayer = false;
        GameObject bestTargetObject = null;

        // Iterate through detected colliders to find and score potential targets.
        foreach (var collider in detectedObjects)
        {
            if (collider == null) continue;

            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            if (player != null && Context.MobReference.PreysReference.Contains("Player"))
            {
                float utility = EvaluateTargetUtility(player.transform, true, player.gameObject);
                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestTarget = player.transform;
                    bestTargetObject = player.gameObject;
                    isPlayer = true;
                }
            }

            MobActionsController prey = collider.GetComponent<MobActionsController>();

            if (prey != null && Context.MobReference.PreysReference.Contains(prey.type))
            {
                float utility = EvaluateTargetUtility(prey.transform, false, prey.gameObject);
                if (utility > bestUtility && !isPlayer) // Players have priority
                {
                    bestUtility = utility;
                    bestTarget = prey.transform;
                    bestTargetObject = prey.gameObject;
                }
            }
        }

        // Dynamic threshold based on current state and memory
        float threshold = CalculateDynamicThreshold();

        // Start chase if a suitable target was found
        if (bestTarget != null && bestUtility > threshold)
        {
            // Remember this opportunity
            if (bestTargetObject != null)
            {
                opportunityMemory[bestTargetObject] = Time.time;
            }

            if (isPlayer)
            {
                StartPlayerChase(bestTarget.GetComponent<PlayerStatusController>());
            }
            else
            {
                StartChase(bestTarget.GetComponent<MobActionsController>());
            }
        }
    }

    /// <summary>
    /// Calculates a dynamic threshold for initiating chase based on current state and health.
    /// </summary>
    /// <returns>Threshold value for chase initiation.</returns>
    protected float CalculateDynamicThreshold()
    {
        float baseThreshold = 0.3f;

        // Lower threshold if health is high (more aggressive)
        float healthFactor = Context.StatusController.HealthManager.CurrentValue / Context.StatusController.HealthManager.MaxValue;
        float healthAdjustment = (healthFactor - 0.5f) * 0.2f;

        // Higher threshold if we have remembered threats nearby
        float threatAdjustment = threatMemory.Count * 0.1f;

        return Mathf.Clamp(baseThreshold - healthAdjustment + threatAdjustment, 0.2f, 0.6f);
    }

    /// <summary>
    /// Evaluates the utility of pursuing a target based on multiple factors with memory influence.
    /// </summary>
    /// <param name="targetTransform">Transform of the potential target.</param>
    /// <param name="isPlayer">Whether the target is a player.</param>
    /// <param name="targetObject">GameObject of the target for memory lookup.</param>
    /// <returns>Utility score for pursuing this target.</returns>
    protected float EvaluateTargetUtility(Transform targetTransform, bool isPlayer, GameObject targetObject)
    {
        float distance = Vector3.Distance(Context.MobReference.TransformReference.position, targetTransform.position);

        // Distance factor - closer targets are more attractive
        float distanceFactor = 1f - Mathf.Clamp01(distance / Context.MobReference.DetectionRange);

        // Urgency factor - players typically have higher priority
        float urgencyFactor = isPlayer ? 0.8f : 0.5f;

        // Safety factor - consider if we're at a good health level to engage
        float healthRatio = Context.StatusController.HealthManager.CurrentValue / Context.StatusController.HealthManager.MaxValue;
        float safetyFactor = Mathf.Lerp(0.3f, 0.9f, healthRatio);

        // Calculate direction alignment - prefer targets in front
        Vector3 directionToTarget = (targetTransform.position - Context.MobReference.TransformReference.position).normalized;
        Vector3 forward = Context.MobReference.TransformReference.forward;
        float alignment = (Vector3.Dot(forward, directionToTarget) + 1f) / 2f; // Normalize to 0-1

        // Memory bonus - targets we've seen before get a small bonus
        float memoryBonus = 0f;
        if (targetObject != null && opportunityMemory.ContainsKey(targetObject))
        {
            float timeSinceLastSeen = Time.time - opportunityMemory[targetObject];
            if (timeSinceLastSeen < OPPORTUNITY_MEMORY_DURATION)
                memoryBonus = 0.1f * (1f - timeSinceLastSeen / OPPORTUNITY_MEMORY_DURATION);
        }

        // Combine factors
        float utility = (distanceFactor * DISTANCE_WEIGHT) +
                       (urgencyFactor * URGENCY_WEIGHT) +
                       (safetyFactor * SAFETY_WEIGHT) +
                       memoryBonus;

        // Apply alignment bonus
        utility *= (0.7f + alignment * 0.3f);

        return utility;
    }

    /// <summary>
    /// Starts chasing the given prey.
    /// </summary>
    /// <param name="prey">The prey to chase.</param>
    private void StartChase(MobActionsController prey)
    {
        // Set the current chase target and change the state to Chasing.
        Context.MobReference.CurrentChaseTarget = prey;
        shouldChangeToChasingState = true;
    }

    /// <summary>
    /// Starts chasing the given player.
    /// </summary>
    /// <param name="player">The player to chase.</param>
    private void StartPlayerChase(PlayerStatusController player)
    {
        // Set the current player target and change the state to Chasing.
        Context.MobReference.CurrentPlayerTarget = player;
        shouldChangeToChasingState = true;
    }

    /// <summary>
    /// Evaluates whether the mob should continue current behavior or switch states.
    /// </summary>
    /// <returns>Utility score for continuing current behavior.</returns>
    protected virtual float EvaluateCurrentBehavior()
    {
        return 0.5f; // Default neutral score
    }
}