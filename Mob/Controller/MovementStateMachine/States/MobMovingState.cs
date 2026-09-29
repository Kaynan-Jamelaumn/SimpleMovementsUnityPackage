using UnityEngine;

/// <summary>
/// Idle walking: strolls to a random safe point around home. Summoned mobs use it to follow their summoner.
/// </summary>
public class MobMovingState : MobMovementState
{
    private bool following;
    private float nextFollowUpdate;

    public MobMovingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        CombatEntity summoner = Self.Summoner;
        following = summoner != null && summoner.IsAlive;
        nextFollowUpdate = 0f;
        if (following)
            return;

        float radius = Mathf.Max(1f, Profile.wanderRadius);
        if (!Motor.TryFindWanderPoint(Context.Home, radius, out Vector3 destination) || !Motor.MoveTo(destination, MobMoveMode.Walk, 0.4f, true))
            Finish();
    }

    protected override void OnUpdate(float dt)
    {
        if (following)
        {
            UpdateFollow();
            return;
        }
        if (Motor.HasArrived() || Motor.IsStuck || Motor.IsOffMesh || TimeInState > Profile.maxWalkTime)
            Finish();
    }

    private void UpdateFollow()
    {
        CombatEntity summoner = Self.Summoner;
        if (summoner == null || !summoner.IsAlive)
        {
            Finish();
            return;
        }
        float d = CombatQuery.FlatDistance(Position, summoner.Position);
        if (d < 3f)
        {
            Motor.Stop();
            Finish();
            return;
        }
        if (Time.time >= nextFollowUpdate)
        {
            nextFollowUpdate = Time.time + 0.5f;
            // Walk to a spot beside/behind the summoner rather than into it.
            Vector3 side = Quaternion.AngleAxis(Random.Range(120f, 240f), Vector3.up) * summoner.Forward;
            Vector3 goal = summoner.Position + side * 2.5f;
            Motor.MoveTo(goal, d > 8f ? MobMoveMode.Run : MobMoveMode.Walk, 0.5f);
        }
    }
}
