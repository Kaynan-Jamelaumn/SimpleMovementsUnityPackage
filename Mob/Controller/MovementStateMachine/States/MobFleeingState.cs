using UnityEngine;

/// <summary>
/// Runs away from a threat (predator, feared type, the attacker of a coward at low health), weaving a little, until
/// it is far enough and out of sight. When there is nowhere left to run it is cornered and fights back.
/// </summary>
public class MobFleeingState : MobMovementState
{
    private float nextRepath;
    private float corneredTimer;
    private float weave;
    private float safeSince;

    public MobFleeingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        nextRepath = 0f;
        corneredTimer = 0f;
        safeSince = -1f;
        weave = Random.value < 0.5f ? -1f : 1f;
        Motor.SetMode(MobMoveMode.Run);
    }

    protected override void OnUpdate(float dt)
    {
        CombatEntity threat = Brain.Threat;
        if (threat == null || !threat.IsAlive)
        {
            Finish();
            return;
        }

        float dist = CombatQuery.FlatDistance(Position, threat.Position);
        MobMemoryEntry e = Context.Memory.Get(threat);
        bool seen = e != null && e.visible;
        float safe = Mathf.Max(Profile.fleeDistance, Profile.fleeFromPlayersWithin * 1.5f);
        if (dist > safe && !seen)
        {
            if (safeSince < 0f) safeSince = Time.time;
            if (Time.time - safeSince > 2f)
            {
                Motor.Stop();
                Finish();
                return;
            }
        }
        else
        {
            safeSince = -1f;
        }

        bool arrived = Motor.HasArrived(1f);
        if (Time.time < nextRepath && !arrived && !Motor.IsStuck)
            return;
        nextRepath = Time.time + 0.8f;
        weave = -weave;

        Vector3 threatPos = threat.Position + Vector3.Cross(Vector3.up, CombatQuery.FlatDirection(threat.Position, Position, Vector3.forward)) * (weave * 2f);
        if (Motor.TryFindEscapePoint(threatPos, Mathf.Min(Profile.fleeDistance, 15f), out Vector3 point, out float quality) && quality > 0.1f)
        {
            corneredTimer = 0f;
            Motor.MoveTo(point, MobMoveMode.Run, 0.5f);
        }
        else
        {
            corneredTimer += 0.8f;
            if (corneredTimer >= 1.5f && dist < 6f)
            {
                Brain.SetCornered(6f);
                Finish();
            }
        }
    }
}
