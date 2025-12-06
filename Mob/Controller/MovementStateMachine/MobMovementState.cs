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

        // Continue the loop until the destination is reached or the maximum walk time is exceeded.
        while (Context.NavMeshAgentReference.pathPending || (Context.NavMeshAgentReference.isActiveAndEnabled && Context.NavMeshAgentReference.isOnNavMesh &&
        !Context.MobReference.HasReachedDestinationWithMargin()))
        {
            // Check if stuck (not moving significantly)
            if (Vector3.Distance(lastPosition, Context.MobReference.TransformReference.position) < 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 2f)
                {
                    // Try to find alternative path
                    Vector3 alternativeDestination = Context.MobReference.GetRandomNavMeshPosition(
                        Context.NavMeshAgentReference.destination, 5f);
                    Context.NavMeshAgentReference.SetDestination(alternativeDestination);
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = Context.MobReference.TransformReference.position;
            }

            // If the maximum walk time is exceeded, reset the path and set the state to Idle.
            if (Context.MobReference.CurrentPlayerTarget != null && Context.MobReference.PlayerHasMaxChaseTime && Time.time - startTime >= Context.MobReference.MaxWalkTime || Context.MobReference.CurrentPlayerTarget == null && Time.time - startTime >= Context.MobReference.MaxWalkTime)
            {
                if (Context.MobReference.CurrentPredator) Context.MobReference.CurrentPredator = null;
                Context.NavMeshAgentReference.ResetPath();
                shouldChangeToIdleState = true;
                yield break;
            }

            // Check conditions to enter the Chase state during movement.
            CheckChaseConditions();

            yield return null;
        }

        // Destination has been reached, set the state to Idle.
        if (!Context.MobReference.CurrentPlayerTarget) shouldChangeToIdleState = true;
        else CheckChaseConditions();
    }

    /// <summary>
    /// Checks the conditions for entering the Chase state using utility-based evaluation.
    /// </summary>
    public void CheckChaseConditions()
    {
        // If already chasing a target or being chased by a predator, do nothing.
        if (Context.MobReference.CurrentChaseTarget || Context.MobReference.CurrentPredator || Context.MobReference.CurrentPlayerTarget)
            return;

        // Detect objects in the mob's detection range.
        Collider[] detectedObjects = Context.MobReference.DetectionCast.DetectObjects(Context.MobReference.TransformReference);

        Transform bestTarget = null;
        float bestUtility = 0f;
        bool isPlayer = false;

        // Iterate through detected colliders to find and score potential targets.
        foreach (var collider in detectedObjects)
        {
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            if (player != null && Context.MobReference.PreysReference.Contains("Player"))
            {
                float utility = EvaluateTargetUtility(player.transform, true);
                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestTarget = player.transform;
                    isPlayer = true;
                }
            }

            MobActionsController prey = collider.GetComponent<MobActionsController>();

            if (prey != null && Context.MobReference.PreysReference.Contains(prey.type))
            {
                float utility = EvaluateTargetUtility(prey.transform, false);
                if (utility > bestUtility && !isPlayer) // Players have priority
                {
                    bestUtility = utility;
                    bestTarget = prey.transform;
                }
            }
        }

        // Start chase if a suitable target was found
        if (bestTarget != null && bestUtility > 0.3f) // Threshold for initiating chase
        {
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
    /// Evaluates the utility of pursuing a target based on multiple factors.
    /// </summary>
    /// <param name="targetTransform">Transform of the potential target.</param>
    /// <param name="isPlayer">Whether the target is a player.</param>
    /// <returns>Utility score for pursuing this target.</returns>
    protected float EvaluateTargetUtility(Transform targetTransform, bool isPlayer)
    {
        float distance = Vector3.Distance(Context.MobReference.TransformReference.position, targetTransform.position);

        // Distance factor - closer targets are more attractive
        float distanceFactor = 1f - Mathf.Clamp01(distance / Context.MobReference.DetectionRange);

        // Urgency factor - players typically have higher priority
        float urgencyFactor = isPlayer ? 0.8f : 0.5f;

        // Safety factor - consider if we're at a good health level to engage
        float safetyFactor = 0.7f;

        // Calculate direction alignment - prefer targets in front
        Vector3 directionToTarget = (targetTransform.position - Context.MobReference.TransformReference.position).normalized;
        Vector3 forward = Context.MobReference.TransformReference.forward;
        float alignment = (Vector3.Dot(forward, directionToTarget) + 1f) / 2f; // Normalize to 0-1

        // Combine factors
        float utility = (distanceFactor * DISTANCE_WEIGHT) +
                       (urgencyFactor * URGENCY_WEIGHT) +
                       (safetyFactor * SAFETY_WEIGHT);

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