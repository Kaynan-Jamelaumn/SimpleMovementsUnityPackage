using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Represents the chasing state of the mob in the movement state machine.
///  with predictive movement, tactical positioning, and intelligent combat behaviors.
/// </summary>
public class MobChasingState : MobMovementState
{
    private Vector3 lastTargetPosition;
    private Vector3 predictedTargetPosition;
    private float predictionTime = 0.5f; // How far ahead to predict target movement

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
        lastTargetPosition = Context.MobReference.CurrentChaseTarget != null ? 
            Context.MobReference.CurrentChaseTarget.transform.position : 
            Context.MobReference.CurrentPlayerTarget?.transform.position ?? Vector3.zero;
        HandleChaseState();
    }
    
    public override void ExitState() 
    { 
        lastTargetPosition = Vector3.zero;
        predictedTargetPosition = Vector3.zero;
    }
    
    public override void UpdateState() { }
    public override void LateUpdateState() { }

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
    
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    
    /// <summary>
    /// Predicts target's future position based on current velocity.
    /// </summary>
    /// <param name="currentPosition">Current position of target.</param>
    /// <param name="previousPosition">Previous position of target.</param>
    /// <returns>Predicted future position.</returns>
    private Vector3 PredictTargetPosition(Vector3 currentPosition, Vector3 previousPosition)
    {
        Vector3 velocity = (currentPosition - previousPosition) / Time.deltaTime;
        Vector3 prediction = currentPosition + velocity * predictionTime;
        
        // Ensure predicted position is on NavMesh
        if (NavMesh.SamplePosition(prediction, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return currentPosition;
    }

    /// <summary>
    /// Calculates an intercept position to cut off the target's path.
    /// </summary>
    /// <param name="targetPosition">Current target position.</param>
    /// <param name="targetVelocity">Target's movement velocity.</param>
    /// <returns>Optimal intercept position.</returns>
    private Vector3 CalculateInterceptPosition(Vector3 targetPosition, Vector3 targetVelocity)
    {
        float interceptTime = Vector3.Distance(Context.MobReference.TransformReference.position, targetPosition) / Context.NavMeshAgentReference.speed;
        Vector3 interceptPosition = targetPosition + targetVelocity * interceptTime;
        
        // Validate and adjust intercept position
        if (NavMesh.SamplePosition(interceptPosition, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return targetPosition;
    }

    private IEnumerator ChasePrey()
    {
        // Record the start time of the chase to measure the duration.
        float startTime = Time.time;
        float pathUpdateInterval = 0.2f; // Update path 5 times per second
        float lastPathUpdate = 0f;

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
            
            // Update path periodically with prediction
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                Vector3 currentTargetPos = Context.MobReference.CurrentChaseTarget.transform.position;
                Vector3 targetVelocity = (currentTargetPos - lastTargetPosition) / pathUpdateInterval;
                
                // Use interception for faster, smarter chasing
                Vector3 interceptPos = CalculateInterceptPosition(currentTargetPos, targetVelocity);
                Context.NavMeshAgentReference.SetDestination(interceptPos);
                
                lastTargetPosition = currentTargetPos;
                lastPathUpdate = Time.time;
            }

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

                    // Evaluate if should continue chasing or find new target
                    Context.MobReference.CurrentChaseTarget = null;
                    
                    // Look for new targets immediately
                    Transform newTarget = Context.ActionsController.AvailableTarget();
                    if (newTarget != null)
                    {
                        MobActionsController newPrey = newTarget.GetComponent<MobActionsController>();
                        if (newPrey != null)
                        {
                            Context.MobReference.CurrentChaseTarget = newPrey;
                            lastTargetPosition = newPrey.transform.position;
                        }
                    }
                    else
                    {
                        HandleChaseState();
                        CheckChaseConditions();
                    }
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
    /// Coroutine to chase the player with predictive movement.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator ChasePlayerCoroutine()
    {
        // Record the start time of the chase to measure the duration.
        float startTime = Time.time;
        float timeSinceLastBit = Time.time;
        float pathUpdateInterval = 0.15f; // More frequent updates for player
        float lastPathUpdate = 0f;

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
            
            // Update path with prediction
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                Vector3 currentPlayerPos = Context.MobReference.CurrentPlayerTarget.transform.position;
                predictedTargetPosition = PredictTargetPosition(currentPlayerPos, lastTargetPosition);
                
                // Set destination to predicted position for better interception
                Context.NavMeshAgentReference.SetDestination(predictedTargetPosition);
                
                lastTargetPosition = currentPlayerPos;
                lastPathUpdate = Time.time;
            }

            // Check if enough time has passed since the last bite attempt.
            if (Time.time - timeSinceLastBit >= Context.MobReference.BiteCooldown)
            {
                Vector3 boxPosition = Context.ActionsController.MobTransform.position + Context.ActionsController.OffSetDetectionDistance;
                Vector3 size = new Vector3(Context.ActionsController.DetectionDistance.x, Context.ActionsController.DetectionDistance.y, Context.ActionsController.DetectionDistance.z);
                Collider[] hits = Physics.OverlapBox(boxPosition, size / 2f, Context.ActionsController.MobTransform.rotation);

                foreach (Collider hit in hits)
                {
                    // If the player is within the detection box, inflict damage.
                    if (hit.gameObject == Context.MobReference.CurrentPlayerTarget.gameObject)
                    {
                        timeSinceLastBit = Time.time;
                        Context.MobReference.CurrentPlayerTarget.HpManager.ConsumeHP(Context.MobReference.BiteDamage);

                        // Wait for the bite cooldown before attempting another attack.
                        if (!Context.MobReference.IsPartialWait) yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                        // Re-evaluate targets after attack
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
            lastTargetPosition = Context.MobReference.CurrentPlayerTarget.transform.position;
            Context.MobReference.StartCoroutine(ChasePlayerCoroutine());
            return;
        }

        // If there is a current chase target, alert the prey and start the coroutine to chase the prey.
        if (Context.MobReference.CurrentChaseTarget != null)
        {
            lastTargetPosition = Context.MobReference.CurrentChaseTarget.transform.position;
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
    /// Coroutine to run away from the predator with intelligent pathfinding.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator RunFromPredator()
    {
        float pathUpdateInterval = 0.3f;
        float lastPathUpdate = 0f;
        
        // Wait until a predator is detected within the detection range.
        while (Context.MobReference.CurrentPredator == null || Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
        {
            yield return null;
        }

        // Continue running away from the predator while it is within detection range.
        while (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) <= Context.MobReference.DetectionRange)
        {
            // Update escape path periodically
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                RunAwayFromPredator();
                lastPathUpdate = Time.time;
            }
            
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
    /// Executes the logic to run away from the predator using tactical positioning.
    /// </summary>
    private void RunAwayFromPredator()
    {
        // Ensure the NavMeshAgent is active and enabled.
        if (Context.NavMeshAgentReference != null && Context.NavMeshAgentReference.isActiveAndEnabled)
        {
            // Check if the agent is not currently calculating a path and has reached its previous destination.
            if (!Context.NavMeshAgentReference.pathPending && Context.NavMeshAgentReference.remainingDistance < Context.NavMeshAgentReference.stoppingDistance + 2f)
            {
                // Use the enhanced escape position calculation
                Vector3 escapeDestination = Context.ActionsController.CalculateBestEscapePosition(
                    Context.MobReference.CurrentPredator.transform.position, 
                    Context.MobReference.EscapeMaxDistance);

                // Set the new destination for the NavMeshAgent.
                Context.NavMeshAgentReference.SetDestination(escapeDestination);
            }

            // If the predator is no longer within detection range, stop the chase.
            if (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
            {
                StopChase();
            }
        }
    }
}