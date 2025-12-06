using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

/// <summary>
/// State machine that manages the movement states of a mob in the game.
///  utility-based state evaluation for more intelligent behavior selection.
/// </summary>
public class MobMovementStateMachine : StateManager<MobMovementStateMachine.EMobMovementState>, IAssignmentsValidator
{
    // Context for the movement state machine.
    private MobMovementContext context;

    /// <summary>
    /// Enumeration of possible movement states.
    /// </summary>
    public enum EMobMovementState
    {
        Idle,
        Moving,
        Chasing,
        Patrol,
    }

    [SerializeField] private MobActionsController actionsController;
    [SerializeField] private Mob mob;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private MobStatusController statusController;

    // State utility evaluation interval
    private float stateEvaluationInterval = 0.5f;
    private float lastStateEvaluation = 0f;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// Initializes components and validates assignments.
    /// </summary>
    private void Awake()
    {
        // Obtain the required components from the GameObject.
        statusController = GetComponent<MobStatusController>();
        navMeshAgent = GetComponent<NavMeshAgent>();

        // Get animator from hierarchy (Model/VisualModel/Animator)
        if (animator == null && transform.childCount > 0)
        {
            Transform modelParent = transform.GetChild(0);
            if (modelParent != null && modelParent.childCount > 0)
            {
                Transform visualModel = modelParent.GetChild(0);
                if (visualModel != null)
                {
                    animator = visualModel.GetComponent<Animator>();
                }
            }
        }

        // Validate the assignments of necessary components.
        ValidateAssignments();

        // Set the NavMeshAgent speed from the mob's status.
        navMeshAgent.speed = statusController.SpeedManager.Speed;

        // Create the context with all necessary components.
        context = new MobMovementContext(mob, actionsController, animator, statusController, navMeshAgent);

        // Initialize the state machine states.
        InitializeStates();
    }

    /// <summary>
    /// Update is called once per frame.
    /// periodic state utility evaluation for adaptive behavior.
    /// </summary>
    private void Update()
    {
        // Perform periodic utility evaluation to ensure we're in the best state
        if (Time.time - lastStateEvaluation >= stateEvaluationInterval)
        {
            EvaluateStateUtility();
            lastStateEvaluation = Time.time;
        }
    }

    /// <summary>
    /// Evaluates whether the current state is still optimal or if a transition would be beneficial.
    /// This allows the AI to be more responsive and adaptive to changing conditions.
    /// </summary>
    private void EvaluateStateUtility()
    {
        // Skip evaluation if we're in a transitioning state or in critical situations
        if (IsTransitioningState)
            return;

        // Check for high-priority interrupts (e.g., immediate threats)
        if (CheckForHighPriorityInterrupts())
            return;

        // Normal utility evaluation for state optimization
        // This is handled by the individual state's GetNextState method
        // which now incorporates utility scoring
    }

    /// <summary>
    /// Checks for high-priority situations that should interrupt current behavior.
    /// </summary>
    /// <returns>True if a high-priority interrupt was triggered.</returns>
    private bool CheckForHighPriorityInterrupts()
    {
        // Check for immediate predator threats
        if (mob.CurrentPredator != null)
        {
            float distanceToPredator = Vector3.Distance(mob.TransformReference.position, mob.CurrentPredator.transform.position);
            if (distanceToPredator < mob.DetectionRange * 0.5f && CurrentState.StateKey != EMobMovementState.Chasing)
            {
                // Immediate threat - switch to chasing state to flee
                TransitionToState(EMobMovementState.Chasing);
                return true;
            }
        }

        // Check for critical health situations
        if (statusController.HealthManager.CurrentValue < statusController.HealthManager.MaxValue * 0.2f)
        {
            // Low health - might want to be more cautious, but this depends on mob type
            // Implementation can be extended based on specific mob behaviors
        }

        return false;
    }

    /// <summary>
    /// Validates the assignments of necessary components.
    /// </summary>
    public void ValidateAssignments()
    {
        actionsController = GetComponentOrLogError(ref actionsController, "MobActionsController");
        navMeshAgent = GetComponentOrLogError(ref navMeshAgent, "NavMeshAgent");
        statusController = GetComponentOrLogError(ref statusController, "MobStatusController");

        // Animator is on child hierarchy, not root - get it if not already assigned
        if (animator == null)
        {
            if (transform.childCount > 0)
            {
                Transform modelParent = transform.GetChild(0);
                if (modelParent != null && modelParent.childCount > 0)
                {
                    animator = modelParent.GetChild(0).GetComponent<Animator>();
                }
            }

            Assert.IsNotNull(animator,
                "MobAnimator is not assigned. Required hierarchy: Mob → Model → VisualModel (with Animator component). " +
                "Use Tools → Mob Setup → Create New Mob to auto-create this structure.");
        }
    }

    protected T GetComponentOrLogError<T>(ref T field, string fieldName) where T : Component
    {
        field = GetComponent<T>();
        Assert.IsNotNull(field, $"{fieldName} is not assigned.");
        return field;
    }


    /// <summary>
    /// Initializes the states of the state machine.
    /// </summary>
    private void InitializeStates()
    {
        // Add states to the state machine with their respective context.
        States.Add(EMobMovementState.Idle, new MobIdleState(context, EMobMovementState.Idle));
        States.Add(EMobMovementState.Moving, new MobMovingState(context, EMobMovementState.Moving));
        States.Add(EMobMovementState.Chasing, new MobChasingState(context, EMobMovementState.Chasing));
        States.Add(EMobMovementState.Patrol, new MobPatrolState(context, EMobMovementState.Patrol));

        // Set the initial state to Idle.
        CurrentState = States[EMobMovementState.Idle];
    }

    /// <summary>
    /// Gets the context for debugging or external access.
    /// </summary>
    public MobMovementContext GetContext()
    {
        return context;
    }
}