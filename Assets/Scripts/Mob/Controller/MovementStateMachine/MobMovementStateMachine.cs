using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

/// <summary>
/// State machine that manages the movement states of a mob in the game.
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

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// Initializes components and validates assignments.
    /// </summary>
    private void Awake()
    {
        // Obtain the required components from the GameObject.
        statusController = GetComponent<MobStatusController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = transform.GetChild(0).GetChild(0).GetComponent<Animator>();

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
    /// Validates the assignments of necessary components.
    /// </summary>
    public void ValidateAssignments()
    {
        actionsController = GetComponentOrLogError(ref actionsController, "MobActionsController");
        animator = GetComponentOrLogError(ref animator, "MobAnimator");
        navMeshAgent = GetComponentOrLogError(ref navMeshAgent, "NavMeshAgent");
        statusController = GetComponentOrLogError(ref statusController, "MobStatusController");
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
}
