using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Abstract base class for managing states in a state machine.
/// </summary>
/// <typeparam name="EState">The enum type representing the state key.</typeparam>
public abstract class StateManager<EState> : MonoBehaviour where EState : Enum
{
    // Dictionary to hold the states.
    protected Dictionary<EState, BaseState<EState>> States = new Dictionary<EState, BaseState<EState>>();

    // The current state of the state machine.
    public BaseState<EState> CurrentState;

    // Flag to indicate whether a state transition is in progress.
    protected bool IsTransitioningState = false;

    /// <summary>
    /// Start is called before the first frame update.
    /// Initializes the current state.
    /// </summary>
    void Start()
    {
        CurrentState.EnterState();
    }

    /// <summary>
    /// Update is called once per frame.
    /// Manages the state transitions and updates the current state.
    /// </summary>
    void Update()
    {
        EState nextStateKey = CurrentState.GetNextState();

        // If not transitioning and next state is the same as the current state, update the current state.
        if (!IsTransitioningState && nextStateKey.Equals(CurrentState.StateKey))
        {
            CurrentState.UpdateState();
        }
        // Otherwise, transition to the next state.
        else if (!IsTransitioningState)
        {
            TransitionToState(nextStateKey);
        }
    }

    /// <summary>
    /// Transitions to the specified state.
    /// </summary>
    /// <param name="stateKey">The key of the state to transition to.</param>
    public void TransitionToState(EState stateKey)
    {
        IsTransitioningState = true;
        CurrentState.ExitState();
        CurrentState = States[stateKey];
        CurrentState.EnterState();
        IsTransitioningState = false;
    }

    /// <summary>
    /// Called when a trigger collider enters the state.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    void OnTriggerEnter(Collider other)
    {
        CurrentState.OnTriggerEnter(other);
    }

    /// <summary>
    /// Called when a trigger collider stays in the state.
    /// </summary>
    /// <param name="other">The collider that is staying in the trigger.</param>
    void OnTriggerStay(Collider other)
    {
        CurrentState.OnTriggerStay(other);
    }

    /// <summary>
    /// Called when a trigger collider exits the state.
    /// </summary>
    /// <param name="other">The collider that exited the trigger.</param>
    void OnTriggerExit(Collider other)
    {
        CurrentState.OnTriggerExit(other);
    }
}
