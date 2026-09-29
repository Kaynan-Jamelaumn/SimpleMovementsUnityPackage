using UnityEngine;

/// <summary>
/// A quick sidestep/roll out of a telegraphed attack or away from a weapon swing, optionally invulnerable. Tries the
/// opposite and the perpendicular directions when there is no room.
/// </summary>
public class MobDodgingState : MobMovementState
{
    private float duration;
    private bool moving;

    public MobDodgingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    public override bool IsCommitted => moving && !IsFinished;

    protected override void OnEnter()
    {
        moving = false;
        if (!Brain.ConsumeDodge(out MobDodgeRequest request))
        {
            Finish();
            return;
        }
        duration = Profile.dodgeDuration;
        Vector3 dir = request.direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f)
            dir = Context.Transform.right;
        dir.Normalize();

        Vector3[] tries = { dir, Quaternion.AngleAxis(45f, Vector3.up) * dir, Quaternion.AngleAxis(-45f, Vector3.up) * dir, Vector3.Cross(Vector3.up, dir), -Vector3.Cross(Vector3.up, dir) };
        for (int i = 0; i < tries.Length && !moving; i++)
        {
            Vector3 d = tries[i];
            Vector3 end = Position + d * request.distance;
            if (i < tries.Length - 1 && !Motor.IsSafe(end, duration + 0.5f))
                continue;
            moving = Motor.Dodge(d, request.distance, duration);
        }

        if (!moving)
        {
            Finish();
            return;
        }
        if (Profile.dodgeInvulnerable)
            Self.SetInvulnerable(duration);
        Context.Animation.OnDodge();
        Brain.NotifyDodged();
    }

    protected override void OnUpdate(float dt)
    {
        FaceTarget();
        if (!Self.IsBeingDisplaced || TimeInState > duration + 0.15f)
        {
            moving = false;
            Finish();
        }
    }
}
