using UnityEngine;

/// <summary>
/// Walks to the next patrol point (Loop, Ping Pong or Random order), then hands over to Idle for the patrol wait.
/// </summary>
public class MobPatrolState : MobMovementState
{
    public MobPatrolState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        Mob mob = Context.MobReference;
        int count = mob.PatrolPointCount;
        if (count == 0)
        {
            Finish();
            return;
        }
        mob.CurrentPatrolPoint = Mathf.Clamp(mob.CurrentPatrolPoint, 0, count - 1);
        Vector3 point = mob.GetPatrolPoint(mob.CurrentPatrolPoint);

        // Already standing on it: go straight to the next one.
        if (CombatQuery.FlatDistance(Position, point) < 1f)
        {
            Advance(mob, count);
            point = mob.GetPatrolPoint(mob.CurrentPatrolPoint);
        }
        if (!Motor.MoveTo(point, MobMoveMode.Walk, 0.5f, true))
            Finish();
    }

    protected override void OnUpdate(float dt)
    {
        Mob mob = Context.MobReference;
        int count = mob.PatrolPointCount;
        if (count == 0)
        {
            Finish();
            return;
        }
        bool timeout = TimeInState > Profile.maxWalkTime * 2f;
        if (Motor.HasArrived() || Motor.IsStuck || timeout)
        {
            Advance(mob, count);
            Finish();
        }
    }

    private void Advance(Mob mob, int count)
    {
        if (count <= 1)
            return;
        switch (Profile.patrolMode)
        {
            case MobPatrolMode.Random:
            {
                int next = Random.Range(0, count - 1);
                if (next >= mob.CurrentPatrolPoint) next++;
                mob.CurrentPatrolPoint = next;
                break;
            }
            case MobPatrolMode.PingPong:
            {
                int next = mob.CurrentPatrolPoint + mob.PatrolDirection;
                if (next >= count || next < 0)
                {
                    mob.PatrolDirection = -mob.PatrolDirection;
                    next = mob.CurrentPatrolPoint + mob.PatrolDirection;
                }
                mob.CurrentPatrolPoint = Mathf.Clamp(next, 0, count - 1);
                break;
            }
            default:
                mob.CurrentPatrolPoint = (mob.CurrentPatrolPoint + 1) % count;
                break;
        }
    }
}
