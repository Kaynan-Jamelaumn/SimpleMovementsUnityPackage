using UnityEngine;

/// <summary>
/// Backs away from the target while facing it: ranged mobs and kiters get back to their preferred range,
/// skirmishers disengage after hitting. Ends when far enough, after a short time, or when cornered.
/// </summary>
public class MobRetreatingState : MobMovementState
{
    private float until;
    private float goalDistance;
    private float nextRepath;

    public MobRetreatingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        CombatEntity t = Brain.Target;
        if (t == null)
        {
            Finish();
            return;
        }
        bool skirmish = Brain.Style == MobCombatStyle.Skirmisher && !Brain.RangedRole;
        goalDistance = skirmish ? Brain.PreferredMax + Profile.circleDistance + 2f : Mathf.Lerp(Brain.PreferredMin, Brain.PreferredMax, 0.6f);
        until = Time.time + (skirmish ? Random.Range(1f, 1.8f) : Random.Range(1.5f, 3f));
        nextRepath = 0f;
    }

    protected override void OnUpdate(float dt)
    {
        CombatEntity t = Brain.Target;
        if (t == null || !t.IsAlive)
        {
            Finish();
            return;
        }
        float dist = CombatQuery.FlatDistance(Position, t.Position);
        if (dist >= goalDistance || Time.time > until)
        {
            Finish();
            return;
        }

        FaceTarget();
        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + 0.35f;

        if (Motor.TryFindEscapePoint(t.Position, Mathf.Max(3f, goalDistance - dist + 2f), out Vector3 point, out float quality) && quality > 0.15f)
        {
            // Backpedal when close, turn and run when the target is far behind.
            Motor.MoveTo(point, dist < 3f ? MobMoveMode.Run : MobMoveMode.Strafe, 0.3f);
        }
        else
        {
            // Nowhere to go: stand and fight.
            Finish();
        }
    }
}
