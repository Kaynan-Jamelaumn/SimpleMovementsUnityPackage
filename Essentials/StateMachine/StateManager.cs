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

    /// <summary><see cref="CurrentTime"/> at which the current state was entered.</summary>
    public float StateEnteredTime { get; private set; }

    /// <summary>Seconds spent in the current state.</summary>
    public float TimeInState => CurrentTime - StateEnteredTime;

    /// <summary>Raised after every transition: (previous state, new state).</summary>
    public event Action<EState, EState> StateChanged;

    /// <summary>True once the initial state has been entered (Start, or the first manual step).</summary>
    protected bool HasStarted { get; private set; }

    /// <summary>
    /// The clock the machine measures state times with. Time.time by default; a machine that is stepped by a
    /// simulation (player movement, which network replays run again) uses its own simulated clock instead.
    /// </summary>
    protected virtual float CurrentTime => Time.time;

    /// <summary>
    /// True while transitions must not raise <see cref="StateChanged"/> - a network replay re-simulating steps that
    /// already happened, whose transitions were announced the first time.
    /// </summary>
    protected virtual bool SuppressStateEvents => false;

    /// <summary>
    /// Start is called before the first frame update.
    /// Initializes the current state.
    /// </summary>
    protected virtual void Start()
    {
        StartMachine();
    }

    /// <summary>
    /// Enters the initial state once. Start does it; a machine stepped before its Start ran (a network tick on the
    /// frame the object appeared) does it from its first step.
    /// </summary>
    protected void StartMachine()
    {
        if (HasStarted)
            return;
        if (CurrentState == null)
        {
            Debug.LogError($"{GetType().Name} on '{name}' has no initial state. Assign CurrentState in Awake.", this);
            enabled = false;
            return;
        }
        HasStarted = true;
        PreviousStateKey = CurrentState.StateKey;
        StateEnteredTime = CurrentTime;
        CurrentState.EnterState();
    }

    /// <summary>
    /// Update is called once per frame.
    /// Manages the state transitions and updates the current state.
    /// </summary>
    protected virtual void Update()
    {
        StepMachine();
    }

    /// <summary>
    /// One step of the machine: asks the current state for the next one, then either updates the current state or
    /// transitions (Exit, then Enter; the new state's UpdateState runs from the next step). Update calls it every
    /// frame; a machine driven by a simulation calls it from its own step instead.
    /// </summary>
    protected void StepMachine()
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
        StateEnteredTime = CurrentTime;
        CurrentState.EnterState();
        IsTransitioningState = false;
        if (!SuppressStateEvents)
            StateChanged?.Invoke(previous, stateKey);
    }

    /// <summary>
    /// Puts the machine back into a saved situation WITHOUT running ExitState/EnterState or raising
    /// <see cref="StateChanged"/>: the current state, the one before it and when it was entered (in
    /// <see cref="CurrentTime"/>). Used to restore a snapshot (network reconciliation); the caller restores whatever
    /// the states keep elsewhere.
    /// </summary>
    public void RestoreState(EState current, EState previous, float enteredTime)
    {
        if (!States.TryGetValue(current, out BaseState<EState> state))
        {
            Debug.LogError($"{GetType().Name} on '{name}': cannot restore state {current}, it is not registered.", this);
            return;
        }
        CurrentState = state;
        PreviousStateKey = previous;
        StateEnteredTime = enteredTime;
        IsTransitioningState = false;
        HasStarted = true;
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
