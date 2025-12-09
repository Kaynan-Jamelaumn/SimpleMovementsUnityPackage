using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Represents the chasing state of the mob in the movement state machine.
/// Enhanced with predictive movement, tactical positioning, and intelligent combat behaviors.
/// </summary>
public class MobChasingState : MobMovementState
{
    private Vector3 lastTargetPosition;
    private Vector3 predictedTargetPosition;
    private float predictionTime = 0.5f; // How far ahead to predict target movement
    private float circlingRadius = 3f; // Radius for circling behavior
    private float circlingAngle = 0f; // Current angle for circling
    private bool useCirclingBehavior = false; // Whether to use circling tactic

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
        if (Context.MobReference != null)
        {
            Context.MobReference.StopAllCoroutines();
        }

        if (Context.Anim != null)
        {
            Context.Anim.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        }

        lastTargetPosition = Context.MobReference.CurrentChaseTarget != null ?
            Context.MobReference.CurrentChaseTarget.transform.position :
            Context.MobReference.CurrentPlayerTarget?.transform.position ?? Vector3.zero;

        // Decide on tactics based on health and target
        DecideChaseStrategy();
        HandleChaseState();
    }

    public override void ExitState()
    {
        lastTargetPosition = Vector3.zero;
        predictedTargetPosition = Vector3.zero;
        circlingAngle = 0f;
        useCirclingBehavior = false;
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
    /// Decides on the chase strategy based on health and target type.
    /// </summary>
    private void DecideChaseStrategy()
    {
        if (Context.StatusController == null || Context.StatusController.HealthManager == null) return;

        float healthRatio = Context.StatusController.HealthManager.CurrentValue /
                           Context.StatusController.HealthManager.MaxValue;

        // Use circling behavior if health is moderate and chasing player
        if (Context.MobReference.CurrentPlayerTarget != null && healthRatio > 0.4f && healthRatio < 0.8f)
        {
            useCirclingBehavior = UnityEngine.Random.value > 0.5f; // 50% chance to use circling
        }
    }

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
        if (Context.NavMeshAgentReference == null) return targetPosition;

        float interceptTime = Vector3.Distance(Context.MobReference.TransformReference.position, targetPosition) /
                             Mathf.Max(Context.NavMeshAgentReference.speed, 0.1f);
        Vector3 interceptPosition = targetPosition + targetVelocity * interceptTime;

        // Validate and adjust intercept position
        if (NavMesh.SamplePosition(interceptPosition, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return targetPosition;
    }

    /// <summary>
    /// Calculates a circling position around the target for tactical positioning.
    /// </summary>
    /// <param name="targetPosition">Position of the target.</param>
    /// <returns>Circling position.</returns>
    private Vector3 CalculateCirclingPosition(Vector3 targetPosition)
    {
        // Increment circling angle for continuous movement
        circlingAngle += Time.deltaTime * 60f; // 60 degrees per second
        if (circlingAngle >= 360f) circlingAngle -= 360f;

        Vector3 offset = new Vector3(
            Mathf.Cos(circlingAngle * Mathf.Deg2Rad) * circlingRadius,
            0f,
            Mathf.Sin(circlingAngle * Mathf.Deg2Rad) * circlingRadius
        );

        Vector3 circlingPosition = targetPosition + offset;

        if (NavMesh.SamplePosition(circlingPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
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
        float strategyReassessmentInterval = 2f;
        float lastStrategyReassessment = Time.time;

        // Continue chasing the prey while it exists and is outside the stopping distance.
        while (Context.MobReference.CurrentChaseTarget != null &&
               Context.NavMeshAgentReference != null &&
               Vector3.Distance(Context.MobReference.TransformReference.position,
                              Context.MobReference.CurrentChaseTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            // If the chase duration exceeds the maximum allowed time or the target is lost, stop the chase.
            if (Time.time - startTime >= Context.MobReference.MaxChaseTime ||
                Context.MobReference.CurrentChaseTarget == null)
            {
                StopChase();
                yield break;
            }

            // Reassess strategy periodically
            if (Time.time - lastStrategyReassessment >= strategyReassessmentInterval)
            {
                DecideChaseStrategy();
                lastStrategyReassessment = Time.time;
            }

            shouldChangeToChasingState = true;

            // Update path periodically with prediction
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                Vector3 currentTargetPos = Context.MobReference.CurrentChaseTarget.transform.position;
                Vector3 targetVelocity = (currentTargetPos - lastTargetPosition) / pathUpdateInterval;

                Vector3 destinationPos;
                if (useCirclingBehavior)
                {
                    destinationPos = CalculateCirclingPosition(currentTargetPos);
                }
                else
                {
                    // Use interception for faster, smarter chasing
                    destinationPos = CalculateInterceptPosition(currentTargetPos, targetVelocity);
                }

                SafeSetDestination(destinationPos);

                lastTargetPosition = currentTargetPos;
                lastPathUpdate = Time.time;
            }

            // Check if the target is within attack distance and perform an attack if possible.
            if (Context.MobReference.CurrentChaseTarget != null &&
                Context.MobReference.CurrentChaseTarget.isActiveAndEnabled)
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position,
                                                  Context.MobReference.CurrentChaseTarget.transform.position);

                if (distance <= Context.MobReference.AttackDistance)
                {
                    // Attack the target and inflict damage.
                    Context.MobReference.CurrentChaseTarget.ReceiveDamage(Context.MobReference.BiteDamage);

                    // Wait for the bite cooldown before attempting another attack.
                    yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                    // Evaluate if should continue chasing or find new target
                    Context.MobReference.CurrentChaseTarget = null;

                    // Look for new targets immediately using utility-based selection
                    Transform newTarget = Context.ActionsController.AvailableTarget();
                    if (newTarget != null)
                    {
                        MobActionsController newPrey = newTarget.GetComponent<MobActionsController>();
                        if (newPrey != null)
                        {
                            Context.MobReference.CurrentChaseTarget = newPrey;
                            lastTargetPosition = newPrey.transform.position;
                            DecideChaseStrategy(); // Reassess strategy for new target
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
    /// Coroutine to chase the player with predictive movement and tactical behaviors.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator ChasePlayerCoroutine()
    {
        // Record the start time of the chase to measure the duration.
        float startTime = Time.time;
        float timeSinceLastBit = Time.time;
        float pathUpdateInterval = 0.15f; // More frequent updates for player
        float lastPathUpdate = 0f;
        float strategyReassessmentInterval = 1.5f;
        float lastStrategyReassessment = Time.time;

        // Continue chasing the player while they exist and are outside the stopping distance.
        while (Context.MobReference.CurrentPlayerTarget != null &&
               Context.NavMeshAgentReference != null &&
               Vector3.Distance(Context.MobReference.TransformReference.position,
                              Context.MobReference.CurrentPlayerTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            // If the player has a maximum chase time and the chase duration exceeds it, stop the chase.
            if (Context.MobReference.PlayerHasMaxChaseTime &&
                Time.time - startTime >= Context.MobReference.MaxChaseTime)
            {
                StopChase();
                yield break;
            }

            // Reassess strategy periodically based on changing conditions
            if (Time.time - lastStrategyReassessment >= strategyReassessmentInterval)
            {
                DecideChaseStrategy();
                lastStrategyReassessment = Time.time;
            }

            shouldChangeToChasingState = true;

            // Update path with prediction and tactics
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                Vector3 currentPlayerPos = Context.MobReference.CurrentPlayerTarget.transform.position;
                predictedTargetPosition = PredictTargetPosition(currentPlayerPos, lastTargetPosition);

                Vector3 destinationPos;
                if (useCirclingBehavior)
                {
                    destinationPos = CalculateCirclingPosition(currentPlayerPos);
                }
                else
                {
                    // Set destination to predicted position for better interception
                    destinationPos = predictedTargetPosition;
                }

                SafeSetDestination(destinationPos);

                lastTargetPosition = currentPlayerPos;
                lastPathUpdate = Time.time;
            }

            // Check if enough time has passed since the last bite attempt.
            if (Time.time - timeSinceLastBit >= Context.MobReference.BiteCooldown)
            {
                Vector3 boxPosition = Context.ActionsController.MobTransform.position +
                                     Context.ActionsController.OffSetDetectionDistance;
                Vector3 size = new Vector3(Context.ActionsController.DetectionDistance.x,
                                          Context.ActionsController.DetectionDistance.y,
                                          Context.ActionsController.DetectionDistance.z);
                Collider[] hits = Physics.OverlapBox(boxPosition, size / 2f,
                                                     Context.ActionsController.MobTransform.rotation);

                foreach (Collider hit in hits)
                {
                    if (hit == null) continue;

                    // If the player is within the detection box, inflict damage.
                    if (hit.gameObject == Context.MobReference.CurrentPlayerTarget.gameObject)
                    {
                        timeSinceLastBit = Time.time;
                        Context.MobReference.CurrentPlayerTarget.HpManager.ConsumeHP(Context.MobReference.BiteDamage);

                        // Wait for the bite cooldown before attempting another attack.
                        if (!Context.MobReference.IsPartialWait)
                        {
                            yield return new WaitForSeconds(Context.MobReference.BiteCooldown);
                        }

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
        if (Context.MobReference.CurrentPlayerTarget == null)
        {
            StopChase();
        }
    }


    /// <summary>
    /// Stops chasing the current target.
    /// </summary>
    private void StopChase()
    {
        if (Context.NavMeshAgentReference != null)
        {
            Context.NavMeshAgentReference.ResetPath();
        }

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
    /// Coroutine to run away from the predator with intelligent pathfinding and evasive maneuvers.
    /// </summary>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator RunFromPredator()
    {
        float pathUpdateInterval = 0.25f;
        float lastPathUpdate = 0f;
        float zigzagTimer = 0f;
        float zigzagInterval = 1f;
        bool zigzagLeft = true;

        // Wait until a predator is detected within the detection range.
        while (Context.MobReference.CurrentPredator == null ||
               Vector3.Distance(Context.MobReference.TransformReference.position,
                              Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
        {
            yield return null;
        }

        // Continue running away from the predator while it is within detection range.
        while (Context.MobReference.CurrentPredator != null &&
               Vector3.Distance(Context.MobReference.TransformReference.position,
                              Context.MobReference.CurrentPredator.transform.position) <= Context.MobReference.DetectionRange)
        {
            zigzagTimer += Time.deltaTime;

            // Update escape path periodically with evasive maneuvers
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                RunAwayFromPredator(zigzagLeft);
                lastPathUpdate = Time.time;
            }

            // Add zigzag pattern for more realistic evasion
            if (zigzagTimer >= zigzagInterval)
            {
                zigzagLeft = !zigzagLeft;
                zigzagTimer = 0f;
            }

            yield return null;
        }

        // If the path is not pending and there is remaining distance to the stopping distance, wait for the next frame.
        if (Context.NavMeshAgentReference != null &&
            !Context.NavMeshAgentReference.pathPending &&
            Context.NavMeshAgentReference.remainingDistance > Context.NavMeshAgentReference.stoppingDistance)
        {
            yield return null;
        }

        // Transition to the idle state after escaping the predator.
        shouldChangeToIdleState = true;
    }

    /// <summary>
    /// Executes the logic to run away from the predator using tactical positioning with evasive maneuvers.
    /// </summary>
    /// <param name="zigzagLeft">Whether to zigzag left or right.</param>
    private void RunAwayFromPredator(bool zigzagLeft)
    {
        // Ensure the NavMeshAgent is active and enabled.
        if (Context.NavMeshAgentReference != null && Context.NavMeshAgentReference.isActiveAndEnabled)
        {
            // Check if the agent is not currently calculating a path and has reached its previous destination.
            if (!Context.NavMeshAgentReference.pathPending &&
                Context.NavMeshAgentReference.remainingDistance < Context.NavMeshAgentReference.stoppingDistance + 2f)
            {
                // Use the enhanced escape position calculation with zigzag
                Vector3 escapeDirection = Context.ActionsController.CalculateBestEscapePosition(
                    Context.MobReference.CurrentPredator.transform.position,
                    Context.MobReference.EscapeMaxDistance);

                // Add zigzag offset for unpredictability
                Vector3 perpendicular = Vector3.Cross(escapeDirection - Context.MobReference.TransformReference.position,
                                                     Vector3.up).normalized;
                Vector3 zigzagOffset = perpendicular * (zigzagLeft ? -2f : 2f);
                Vector3 finalDestination = escapeDirection + zigzagOffset;

                if (NavMesh.SamplePosition(finalDestination, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    SafeSetDestination(hit.position);
                }
                else
                {
                    SafeSetDestination(escapeDirection);
                }
            }

            // If the predator is no longer within detection range, stop the chase.
            if (Context.MobReference.CurrentPredator != null &&
                Vector3.Distance(Context.MobReference.TransformReference.position,
                               Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
            {
                StopChase();
            }
        }
    }
}