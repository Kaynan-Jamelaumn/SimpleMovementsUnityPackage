using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base class representing a state in the mob's movement state machine.
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
    /// Coroutine that waits for the mob to reach its destination.
    /// </summary>
    public IEnumerator WaitToReachDestinationRoutine()
    {
        // Record the start time for measuring the walk time.
        float startTime = Time.time;

        // Continue the loop until the destination is reached or the maximum walk time is exceeded.
        while (Context.NavMeshAgentReference.pathPending || (Context.NavMeshAgentReference.isActiveAndEnabled && Context.NavMeshAgentReference.isOnNavMesh &&
        !Context.MobReference.HasReachedDestinationWithMargin()))
        {
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
    /// Checks the conditions for entering the Chase state.
    /// </summary>
    public void CheckChaseConditions()
    {
        // If already chasing a target or being chased by a predator, do nothing.
        if (Context.MobReference.CurrentChaseTarget || Context.MobReference.CurrentPredator || Context.MobReference.CurrentPlayerTarget)
            return;

        // Detect objects in the mob's detection range.
        Collider[] detectedObjects = Context.MobReference.DetectionCast.DetectObjects(Context.MobReference.TransformReference);

        MobActionsController backUpPrey = null;

        // Iterate through detected colliders to find prey.
        foreach (var collider in detectedObjects)
        {
            MobActionsController prey = collider.GetComponent<MobActionsController>();
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            if (player != null && Context.MobReference.PreysReference.Contains("Player"))
            {
                StartPlayerChase(player);
                return;
            }

            if (prey != null && Context.MobReference.PreysReference.Contains(prey.type))
            {
                backUpPrey = prey;
            }
        }

        if (backUpPrey != null)
        {
            StartChase(backUpPrey);
            return;
        }

        // Reset the chase target if no prey is found.
        Context.MobReference.CurrentPlayerTarget = null;
        Context.MobReference.CurrentChaseTarget = null;
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
}
