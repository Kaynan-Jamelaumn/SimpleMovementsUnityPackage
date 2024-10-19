using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Windows;
using static UnityEditor.Timeline.TimelinePlaybackControls;

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


    private void Awake()
    {        
        ValiDateConstraints();
        context = new MobMovementContext(actionsController);
        InitializeStates();
    }


    private void ValiDateConstraints()
    {
        Assert.IsNotNull(actionsController, "PlayerMovementModel is Not Assigned");

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

