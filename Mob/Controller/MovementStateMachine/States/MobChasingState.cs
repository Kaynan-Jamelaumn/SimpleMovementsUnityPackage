using UnityEngine;

/// <summary>
/// Runs after the target: aims where it is heading (intercept), approaches from its own angle when close so a group
/// surrounds the target, goes to the last known position when sight is lost, and steps out of hazards.
/// </summary>
public class MobChasingState : MobMovementState
{
    public MobChasingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        if (Brain.Target != null)
            MobCombatCoordinator.Engage(Context, Brain.Target);
        Motor.SetMode(MobMoveMode.Run);
    }

    protected override void OnUpdate(float dt)
    {
        CombatEntity t = Brain.Target;
        MobMemoryEntry entry = Brain.TargetEntry;
        if (t == null || entry == null)
        {
            Finish();
            return;
        }
        if (EscapeHazardIfNeeded())
            return;

        Vector3 goal;
        float stop = Brain.RangedRole ? Mathf.Max(0.5f, Brain.PreferredMax * 0.5f) : 0.4f;
        if (entry.visible)
        {
            float dist = CombatQuery.FlatDistance(Position, t.Position);
            float lead = Mathf.Clamp(dist / Mathf.Max(1f, Motor.RunSpeed), 0f, 1.2f) * 0.6f;
            goal = CombatQuery.Predict(t, lead);
            if (!Brain.RangedRole && dist < 7f)
            {
                float angle = MobCombatCoordinator.GetSlotAngle(Context, t);
                goal = t.Position + MobCombatCoordinator.Direction(angle) * Mathf.Max(0.5f, Brain.PreferredMax * 0.7f);
            }
        }
        else
        {
            goal = entry.lastKnownPosition;
            stop = 0.8f;
        }

        Motor.MoveTo(goal, MobMoveMode.Run, stop);

        if (!entry.visible && (Motor.HasArrived(0.5f) || Motor.IsStuck))
            Finish(); // the brain decides: search, or give up
    }
}
