using System;
using UnityEngine;

/// <summary>
/// Abstract base class representing a state in the state machine.
/// </summary>
/// <typeparam name="EState">The enum type representing the state key.</typeparam>
public abstract class BaseState<EState> where EState : Enum
{
    /// <summary>
    /// Constructor for the base state.
    /// </summary>
    /// <param name="key">The state key for the state.</param>
    public BaseState(EState key)
    {
        StateKey = key;
    }

    /// <summary>
    /// Gets the state key for the state.
    /// </summary>
    public EState StateKey { get; private set; }

    /// <summary>
    /// Method called when the state is entered.
    /// </summary>
    public abstract void EnterState();

    /// <summary>
    /// Method called when the state is exited.
    /// </summary>
    public abstract void ExitState();

    /// <summary>
    /// Method called to update the state.
    /// </summary>
    public abstract void UpdateState();

    /// <summary>
    /// Gets the next state to transition to.
    /// </summary>
    /// <returns>The next state.</returns>
    public abstract EState GetNextState();

    /// <summary>
    /// Method called when a trigger collider enters the state.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    public abstract void OnTriggerEnter(Collider other);

    /// <summary>
    /// Method called when a trigger collider stays in the state.
    /// </summary>
    /// <param name="other">The collider that is staying in the trigger.</param>
    public abstract void OnTriggerStay(Collider other);

    /// <summary>
    /// Method called when a trigger collider exits the state.
    /// </summary>
    /// <param name="other">The collider that exited the trigger.</param>
    public abstract void OnTriggerExit(Collider other);

    public abstract void LateUpdateState();
}
