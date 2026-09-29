using UnityEngine;
using UnityEngine.AI;

/// <summary>Dead: stops everything, plays the death animation and optionally turns off its colliders.</summary>
public class MobDeadState : MobMovementState
{
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
            foreach (Collider c in Context.Transform.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }
    }

    protected override void OnUpdate(float dt) { }
}
