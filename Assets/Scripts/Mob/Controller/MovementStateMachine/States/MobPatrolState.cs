using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Represents the patrol state of the mob in the movement state machine.
/// </summary>
public class MobPatrolState : MobMovementState
{
    /// <summary>
    /// Constructor for the MobPatrolState.
    /// </summary>
    /// <param name="context">The context for the mob's movement state.</param>
    /// <param name="estate">The state key for the state.</param>
    public MobPatrolState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }

    /// <summary>
    /// Called when entering the patrol state.
    /// Sets the destination to the next patrol point and starts the wait-to-reach-destination routine.
    /// </summary>
    public override void EnterState()
    {
        if (Context.MobReference.PatrolPoints.Length == 0) return; // No patrol points set

        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        Context.MobReference.NavMeshAgentReference.SetDestination(Context.MobReference.PatrolPoints[Context.MobReference.CurrentPatrolPoint]);
        Context.MobReference.WaitToReachDestinationRoutine = Context.MobReference.StartCoroutine(WaitToReachDestinationRoutine());

        if (Context.MobReference.HasReachedDestinationWithMargin())
        {
            Context.MobReference.CurrentPatrolPoint = (Context.MobReference.CurrentPatrolPoint + 1) % Context.MobReference.PatrolPoints.Length;
            // SetState(MobState.Idle); // Continue to the next patrol point
        }
    }

    /// <summary>
    /// Called when exiting the patrol state.
    /// </summary>
    public override void ExitState() { }

    /// <summary>
    /// Called to update the patrol state.
    /// </summary>
    public override void UpdateState()
    {
        // GetNextState();
    }

    /// <summary>
    /// Determines the next state to transition to based on various conditions.
    /// </summary>
    /// <returns>The next state to transition to.</returns>
    public override MobMovementStateMachine.EMobMovementState GetNextState()
    {
        if (shouldChangeToIdleState)
        {
            shouldChangeToIdleState = false;
            return MobMovementStateMachine.EMobMovementState.Idle;
        }
        if (shouldChangeToMovingState)
        {
            shouldChangeToMovingState = false;
            return MobMovementStateMachine.EMobMovementState.Moving;
        }
        if (shouldChangeToChasingState)
        {
            shouldChangeToChasingState = false;
            return MobMovementStateMachine.EMobMovementState.Chasing;
        }
        return StateKey;
    }

    /// <summary>
    /// Called when a trigger collider enters the state.
    /// </summary>
    /// <param name="other">The collider that entered the trigger.</param>
    public override void OnTriggerEnter(Collider other) { }

    /// <summary>
    /// Called when a trigger collider stays in the state.
    /// </summary>
    /// <param name="other">The collider that is staying in the trigger.</param>
    public override void OnTriggerStay(Collider other) { }

    /// <summary>
    /// Called when a trigger collider exits the state.
    /// </summary>
    /// <param name="other">The collider that exited the trigger.</param>
    public override void OnTriggerExit(Collider other) { }
}
