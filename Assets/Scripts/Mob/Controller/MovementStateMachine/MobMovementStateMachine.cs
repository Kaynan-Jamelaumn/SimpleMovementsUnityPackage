using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;


public class MobMovementStateMachine : StateManager<MobMovementStateMachine.EMobMovementState>
{
    MobMovementContext context;
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


    private void Awake()
    {
        statusController = GetComponent<MobStatusController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = transform.GetChild(0).GetChild(0).GetComponent<Animator>();
        ValiDateConstraints();

        navMeshAgent.speed = statusController.SpeedManager.Speed;

        context = new MobMovementContext(mob, actionsController, animator, statusController, navMeshAgent);
        InitializeStates();
    }


    private void ValiDateConstraints()
    {
        Assert.IsNotNull(actionsController, "MobActionsController is Not Assigned");
        Assert.IsNotNull(animator, "MobAnimator is Not Assigned");
        Assert.IsNotNull(navMeshAgent, "MobActionsController is Not Assigned");
        Assert.IsNotNull(statusController, "MobStatusController is Not Assigned");

    }
    private void InitializeStates()
    {
        States.Add(EMobMovementState.Idle, new MobIdleState(context, EMobMovementState.Idle));
        States.Add(EMobMovementState.Moving, new MobMovingState(context, EMobMovementState.Moving));
        States.Add(EMobMovementState.Chasing, new MobChasingState(context, EMobMovementState.Chasing));
        States.Add(EMobMovementState.Patrol, new MobPatrolState(context, EMobMovementState.Patrol));

        CurrentState = States[EMobMovementState.Idle];
    }
}

