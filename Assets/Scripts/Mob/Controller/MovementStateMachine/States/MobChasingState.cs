using UnityEngine;
using System.Collections;

/// <summary>
/// Represents the chasing state of the mob in the movement state machine.
/// </summary>
public class MobChasingState : MobMovementState
{
    /// <summary>
    /// Constructor for the MobChasingState.
    /// </summary>
    /// <param name="context">The context for the mob's movement state.</param>
    /// <param name="estate">The state key for the state.</param>
    public MobChasingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }

    /// <summary>
    /// Called when entering the chasing state.
    /// Stops all coroutines, plays the chasing animation, and handles the chase state.
    /// </summary>
    public override void EnterState()
    {
        Context.MobReference.StopAllCoroutines();
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        HandleChaseState();
    }

    /// <summary>
    /// Called when exiting the chasing state.
    /// </summary>
    public override void ExitState() { }

    /// <summary>
    /// Called to update the chasing state.
    /// </summary>
    public override void UpdateState() { }

    /// <summary>
    /// Determines the next state to transition to based on various conditions.
    /// </summary>
    /// <returns>The next state to transition to.</returns>
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
        if (shouldChangeToMovingState)
        {
            shouldChangeToMovingState = false;
            return MobMovementStateMachine.EMobMovementState.Moving;
        }
        return StateKey;
    }

    /// <summary>
    /// Called when a trigger collider enters the state.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    public override void OnTriggerEnter(Collider other) { }

    /// <summary>
    /// Called when a trigger collider stays in the state.
    /// </summary>
    /// <param name="other">The collider that is staying in the trigger.</param>
    public override void OnTriggerStay(Collider other) { }

    /// <summary>
    /// Called when a trigger collider exits the state.
    /// </summary>
    /// <param name="other">The collider that exited the trigger.</param>
    public override void OnTriggerExit(Collider other) { }

    /// <summary>
    /// Coroutine to chase the prey.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator ChasePrey()
    {
        // Record the start time of the chase to measure the duration.
        float startTime = Time.time;

        // Continue chasing the prey while it exists and is outside the stopping distance.
        while (Context.MobReference.CurrentChaseTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentChaseTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            // If the chase duration exceeds the maximum allowed time or the target is lost, stop the chase.
            if (Time.time - startTime >= Context.MobReference.MaxChaseTime || Context.MobReference.CurrentChaseTarget == null)
            {
                StopChase();
                yield break;
            }

            shouldChangeToChasingState = true;
            Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentChaseTarget.transform.position);

            // Check if the target is within attack distance and perform an attack if possible.
            if (Context.MobReference.CurrentChaseTarget != null && Context.MobReference.CurrentChaseTarget.isActiveAndEnabled)
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentChaseTarget.transform.position);

                if (distance <= Context.MobReference.AttackDistance)
                {
                    // Attack the target and inflict damage.
                    Context.MobReference.CurrentChaseTarget.ReceiveDamage(Context.MobReference.BiteDamage);

                    // Wait for the bite cooldown before attempting another attack.
                    yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                    // Reset the chase target and handle the chase state again.
                    Context.MobReference.CurrentChaseTarget = null;
                    HandleChaseState();
                    CheckChaseConditions();
                }
                else
                {
                    // Continue chasing the target if it is not within attack distance.
                    Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentChaseTarget.transform.position);
                }
            }
            yield return null;
        }
    }

    /// <summary>
    /// Starts chasing the player.
    /// </summary>
    /// <param name="player">The player to chase.</param>
    public void ChasePlayer(PlayerStatusController player)
    {
        shouldChangeToChasingState = true;
        Context.MobReference.CurrentPlayerTarget = player;
        Context.ActionsController.StartCoroutine(ChasePlayerCoroutine());
    }

    /// <summary>
    /// Coroutine to chase the player.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator ChasePlayerCoroutine()
    {
        // Record the start time of the chase to measure the duration.
        float startTime = Time.time;
        float timeSinceLastBit = Time.time;

        // Continue chasing the player while they exist and are outside the stopping distance.
        while (Context.MobReference.CurrentPlayerTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPlayerTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            // If the player has a maximum chase time and the chase duration exceeds it, stop the chase.
            if (Context.MobReference.PlayerHasMaxChaseTime && Time.time - startTime >= Context.MobReference.MaxChaseTime)
            {
                StopChase();
                yield break;
            }

            shouldChangeToChasingState = true;
            Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentPlayerTarget.transform.position);

            // Check if enough time has passed since the last bite attempt.
            if (Time.time - timeSinceLastBit >= Context.MobReference.BiteCooldown)
            {
                Vector3 boxPosition = Context.ActionsController.MobTransform.position + Context.ActionsController.OffSetDetectionDistance;
                Vector3 size = new Vector3(Context.ActionsController.DetectionDistance.x, Context.ActionsController.DetectionDistance.y, Context.ActionsController.DetectionDistance.z);
                Collider[] hits = Physics.OverlapBox(boxPosition, size, Context.ActionsController.MobTransform.rotation, LayerMask.NameToLayer("player"));

                foreach (Collider hit in hits)
                {
                    // If the player is within the detection box, inflict damage.
                    if (hit.gameObject == Context.MobReference.CurrentPlayerTarget.gameObject)
                    {
                        timeSinceLastBit = Time.time;
                        Context.MobReference.CurrentPlayerTarget.HpManager.ConsumeHP(Context.MobReference.BiteDamage);

                        // Wait for the bite cooldown before attempting another attack.
                        if (!Context.MobReference.IsPartialWait) yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                        Context.MobReference.CurrentPlayerTarget = null;
                        CheckChaseConditions();
                        break;
                    }
                }
            }
            yield return null;
        }

        // If the player target is lost, stop the chase.
        if (!Context.MobReference.CurrentPlayerTarget) StopChase();
    }
    

    /// <summary>
    /// Stops chasing the current target.
    /// </summary>
    private void StopChase()
    {
        Context.NavMeshAgentReference.ResetPath();

        if (Context.MobReference.CurrentChaseTarget != null)
        {
            Context.MobReference.CurrentChaseTarget.CurrentPredator = null;
            Context.MobReference.CurrentChaseTarget = null;
        }
        else
        {
            Context.MobReference.CurrentPlayerTarget = null;
        }
        shouldChangeToIdleState = true;
    }
    /// <summary>
    /// Handles the chase state logic.
    /// </summary>
    public void HandleChaseState()
    {
        // If there is a current player target, start the coroutine to chase the player.
        if (Context.MobReference.CurrentPlayerTarget != null)
        {
            Context.MobReference.StartCoroutine(ChasePlayerCoroutine());
            return;
        }

        // If there is a current chase target, alert the prey and start the coroutine to chase the prey.
        if (Context.MobReference.CurrentChaseTarget != null)
        {
            AlertPrey(Context.ActionsController);
            Context.MobReference.StartCoroutine(ChasePrey());
        }
    }

    /// <summary>
    /// Alerts the prey and starts running from the predator.
    /// </summary>
    /// <param name="predator">The predator to run from.</param>
    public void AlertPrey(MobActionsController predator)
    {
        // Set the state to chasing and clear the current chase target.
        shouldChangeToChasingState = true;
        Context.MobReference.CurrentChaseTarget = null;

        // Set the current predator and start the coroutine to run from the predator.
        Context.MobReference.CurrentPredator = predator;
        Context.MobReference.StartCoroutine(RunFromPredator());
    }

    /// <summary>
    /// Coroutine to run away from the predator.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator RunFromPredator()
    {
        // Wait until a predator is detected within the detection range.
        while (Context.MobReference.CurrentPredator == null || Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
        {
            yield return null;
        }

        // Continue running away from the predator while it is within detection range.
        while (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) <= Context.MobReference.DetectionRange)
        {
            RunAwayFromPredator();
            yield return null;
        }

        // If the path is not pending and there is remaining distance to the stopping distance, wait for the next frame.
        if (!Context.NavMeshAgentReference.pathPending && Context.NavMeshAgentReference.remainingDistance > Context.NavMeshAgentReference.stoppingDistance)
        {
            yield return null;
        }

        // Transition to the idle state after escaping the predator.
        shouldChangeToIdleState = true;
    }

    /// <summary>
    /// Executes the logic to run away from the predator.
    /// </summary>
    private void RunAwayFromPredator()
    {
        // Ensure the NavMeshAgent is active and enabled.
        if (Context.NavMeshAgentReference != null && Context.NavMeshAgentReference.isActiveAndEnabled)
        {
            // Check if the agent is not currently calculating a path and has reached its previous destination.
            if (!Context.NavMeshAgentReference.pathPending && Context.NavMeshAgentReference.remainingDistance < Context.NavMeshAgentReference.stoppingDistance)
            {
                // Calculate the direction away from the predator.
                Vector3 runDirection = Context.MobReference.TransformReference.position - Context.MobReference.CurrentPredator.transform.position;

                // Determine a random escape destination in the opposite direction.
                Vector3 escapeDestination = Context.MobReference.TransformReference.position + runDirection.normalized * (Context.MobReference.EscapeMaxDistance * 2);

                // Set the new destination for the NavMeshAgent.
                Context.NavMeshAgentReference.SetDestination(Context.MobReference.GetRandomNavMeshPosition(escapeDestination, Context.MobReference.EscapeMaxDistance));
            }

            // If the predator is no longer within detection range, stop the chase.
            if (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
            {
                StopChase();
            }
        }
    }



}