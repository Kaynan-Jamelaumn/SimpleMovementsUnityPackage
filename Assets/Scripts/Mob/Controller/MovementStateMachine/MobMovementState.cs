using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public abstract class MobMovementState : BaseState<MobMovementStateMachine.EMobMovementState>
{
    protected MobMovementContext Context;
    protected bool alreadyMoving = false;
    protected Vector3 playerPosition;
    protected bool shouldChangeToMovingState;
    protected bool shouldChangeToPatrolState;
    protected bool shouldChangeToIdleState;
    protected bool shouldChangeToChasingState;
    public MobMovementState(MobMovementContext context, MobMovementStateMachine.EMobMovementState stateKey) : base(stateKey)
    {
        Context = context;
    }
    public IEnumerator WaitToReachDestinationRoutine()
    {
        // Record the start time for measuring the walk time.
        float startTime = Time.time;

        // Continue the loop until the destination is reached or the maximum walk time is exceeded.
        //while (navMeshAgent.pathPending || (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance))
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
        if (!Context.MobReference.CurrentPlayerTarget) shouldChangeToIdleState= true;
        else CheckChaseConditions();
    }


    public void CheckChaseConditions()
    {
        // If already chasing a target or being chased by a predator, do nothing.
        if (Context.MobReference.CurrentChaseTarget || Context.MobReference.CurrentPredator || Context.MobReference.CurrentPlayerTarget)
            return;

        // Define as camadas desejadas (Player e Mob)
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
    private void StartChase(MobActionsController prey)
    {
        Context.MobReference.CurrentChaseTarget = prey;         // Set the current chase target.
        shouldChangeToChasingState = true;        // Set the state to Chase.

    }
    private void StartPlayerChase(PlayerStatusController player)
    {
        Context.MobReference.CurrentPlayerTarget = player;         // Set the current chase target.
        shouldChangeToChasingState = true;        // Set the state to Chase.

    }



}

