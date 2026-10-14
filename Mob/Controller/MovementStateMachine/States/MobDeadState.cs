using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dead: stops everything, plays the death animation and optionally turns off its colliders. A dead mob normally never
/// leaves this state; only a network client's copy does, when the server revives it, and then the colliders this state
/// turned off are turned back on.
/// </summary>
public class MobDeadState : MobMovementState
{
    private readonly List<Collider> disabledColliders = new List<Collider>();

    public MobDeadState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    public override bool IsCommitted => true;

    protected override void OnEnter()
    {
        Motor.Stop();
        MobCombatCoordinator.Disengage(Context);
        Context.Perception.Unsubscribe();
        Context.Animation.OnDeath();

        NavMeshAgent agent = Context.NavMeshAgentReference;
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;
        }
        if (Profile.disableCollidersOnDeath)
        {
            disabledColliders.Clear();
            foreach (Collider c in Context.Transform.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled)
                    continue;
                c.enabled = false;
                disabledColliders.Add(c);
            }
        }
    }

    protected override void OnExit()
    {
        foreach (Collider c in disabledColliders)
            if (c != null)
                c.enabled = true;
        disabledColliders.Clear();
    }

    protected override void OnUpdate(float dt) { }
}
