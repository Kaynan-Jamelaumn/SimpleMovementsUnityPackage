using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Abstract base class for managing states in a state machine.
/// </summary>
/// <remarks>
/// Unity calls <c>Update</c>/<c>LateUpdate</c>/<c>Start</c> on the most derived class only. These are therefore
/// <c>protected virtual</c>: a derived machine that needs its own per-frame logic overrides them and calls
/// <c>base.Update()</c>, instead of declaring a new private <c>Update</c> that silently hides the state machine
/// (which is what left the old mob state machine frozen in its first state).
/// </remarks>
/// <typeparam name="EState">The enum type representing the state key.</typeparam>
public abstract class StateManager<EState> : MonoBehaviour where EState : Enum
{
    // Compares state keys without boxing (EState.Equals(object) allocated on every Update of every machine).
    private static readonly EqualityComparer<EState> KeyComparer = EqualityComparer<EState>.Default;

    // Dictionary to hold the states.
    protected Dictionary<EState, BaseState<EState>> States = new Dictionary<EState, BaseState<EState>>();

    // The current state of the state machine.
    public BaseState<EState> CurrentState;

    // Flag to indicate whether a state transition is in progress.
    protected bool IsTransitioningState = false;

    /// <summary>Key of the state before the last transition (the initial state until the first transition).</summary>
    public EState PreviousStateKey { get; private set; }

    /// <summary>Time.time at which the current state was entered.</summary>
    public float StateEnteredTime { get; private set; }

    /// <summary>Seconds spent in the current state.</summary>
    public float TimeInState => Time.time - StateEnteredTime;

    /// <summary>Raised after every transition: (previous state, new state).</summary>
    public event Action<EState, EState> StateChanged;

    /// <summary>
    /// Start is called before the first frame update.
    /// Initializes the current state.
    /// </summary>
    protected virtual void Start()
    {
        if (CurrentState == null)
        {
            Debug.LogError($"{GetType().Name} on '{name}' has no initial state. Assign CurrentState in Awake.", this);
            enabled = false;
            return;
        }
        PreviousStateKey = CurrentState.StateKey;
        StateEnteredTime = Time.time;
        CurrentState.EnterState();
    }

    /// <summary>
    /// Update is called once per frame.
    /// Manages the state transitions and updates the current state.
    /// </summary>
    protected virtual void Update()
    {
        if (CurrentState == null)
            return;

        EState nextStateKey = CurrentState.GetNextState();

        // If not transitioning and next state is the same as the current state, update the current state.
        if (!IsTransitioningState && KeyComparer.Equals(nextStateKey, CurrentState.StateKey))
        {
            CurrentState.UpdateState();
        }
        // Otherwise, transition to the next state.
        else if (!IsTransitioningState)
        {
            TransitionToState(nextStateKey);
        }
    }

    protected virtual void LateUpdate()
    {
        CurrentState?.LateUpdateState();
    }

    /// <summary>
    /// Transitions to the specified state.
    /// </summary>
    /// <param name="stateKey">The key of the state to transition to.</param>
    public void TransitionToState(EState stateKey)
    {
        if (!States.TryGetValue(stateKey, out BaseState<EState> next))
        {
            Debug.LogError($"{GetType().Name} on '{name}': state {stateKey} is not registered.", this);
            return;
        }

        IsTransitioningState = true;
        EState previous = CurrentState != null ? CurrentState.StateKey : stateKey;
        CurrentState?.ExitState();
        CurrentState = next;
        PreviousStateKey = previous;
        StateEnteredTime = Time.time;
        CurrentState.EnterState();
        IsTransitioningState = false;
        StateChanged?.Invoke(previous, stateKey);
    }

    /// <summary>True if a state with this key is registered.</summary>
    public bool HasState(EState stateKey) => States.ContainsKey(stateKey);

    /// <summary>
    /// Called when a trigger collider enters the state.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    protected virtual void OnTriggerEnter(Collider other)
    {
        CurrentState?.OnTriggerEnter(other);
    }

    /// <summary>
    /// Called when a trigger collider stays in the state.
    /// </summary>
    /// <param name="other">The collider that is staying in the trigger.</param>
    protected virtual void OnTriggerStay(Collider other)
    {
        CurrentState?.OnTriggerStay(other);
    }

    /// <summary>
    /// Called when a trigger collider exits the state.
    /// </summary>
    /// <param name="other">The collider that exited the trigger.</param>
    protected virtual void OnTriggerExit(Collider other)
    {
        CurrentState?.OnTriggerExit(other);
    }
}
