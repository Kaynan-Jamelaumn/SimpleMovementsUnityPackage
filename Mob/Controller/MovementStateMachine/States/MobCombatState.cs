using UnityEngine;

/// <summary>
/// Engaged with the target between attacks. Melee mobs hold their own angle around the target (close in when a
/// melee slot is free, circle further out while others attack); ranged mobs keep their preferred distance, strafe
/// sideways and move to regain line of sight. Always faces the target and steps out of hazards.
/// </summary>
public class MobCombatState : MobMovementState
{
    private float strafeDir;
    private float nextStrafeFlip;
    private float angleDrift;
    private float nextLosSearch;
    private Vector3 losPoint;
    private bool hasLosPoint;

    public MobCombatState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        if (Brain.Target != null)
            MobCombatCoordinator.Engage(Context, Brain.Target);
        strafeDir = Random.value < 0.5f ? -1f : 1f;
        nextStrafeFlip = Time.time + Random.Range(1.5f, 3f);
        angleDrift = 0f;
        hasLosPoint = false;
    }

    protected override void OnUpdate(float dt)
    {
        CombatEntity t = Brain.Target;
        if (t == null || !t.IsAlive)
        {
            Finish();
            return;
        }
        if (EscapeHazardIfNeeded())
            return;

        if (Time.time >= nextStrafeFlip)
        {
            strafeDir = -strafeDir;
            nextStrafeFlip = Time.time + Random.Range(1.5f, 3.5f);
        }

        if (Brain.RangedRole || Brain.Style == MobCombatStyle.Kiter)
            UpdateRanged(t, dt);
        else
            UpdateMelee(t, dt);
    }

    private void UpdateMelee(CombatEntity t, float dt)
    {
        float reach = Mathf.Max(0.8f, Brain.PreferredMax);
        bool waiting = Profile.useAttackTokens && Self.Summoner == null && MobCombatCoordinator.MeleeSlotsFull(Context, t);
        float radius = waiting ? reach + Profile.circleDistance : Mathf.Max(0.6f, reach * 0.8f);
        radius += t.Radius + Self.Radius * 0.5f;

        // Waiting mobs circle slowly; attackers hold their slot.
        if (waiting && Profile.strafeAmount > 0f)
            angleDrift += strafeDir * 25f * Profile.strafeAmount * dt;
        float angle = MobCombatCoordinator.GetSlotAngle(Context, t) + angleDrift;

        float dist = CombatQuery.FlatDistance(Position, t.Position);
        Vector3 want = t.Position + MobCombatCoordinator.Direction(angle) * radius;
        float off = CombatQuery.FlatDistance(Position, want);
        bool far = dist > radius + 3f;
        if (off > 0.75f)
        {
            if (Motor.TryFindPointAround(t.Position, radius, angle, out Vector3 point))
                Motor.MoveTo(point, far ? MobMoveMode.Run : MobMoveMode.Strafe, 0.25f);
            else
                Motor.MoveTo(t.Position, MobMoveMode.Run, reach * 0.8f);
        }
        else
        {
            Motor.Stop();
        }
        if (!far)
            FaceTarget(); // strafe/hold facing the target; run facing the path
    }

    private void UpdateRanged(CombatEntity t, float dt)
    {
        float min = Brain.PreferredMin, max = Brain.PreferredMax;
        float ideal = Mathf.Lerp(min, max, 0.55f);
        float dist = CombatQuery.FlatDistance(Position, t.Position);

        if (dist <= max + 2f)
            FaceTarget();

        // No clear shot: find one.
        if (!Brain.TargetVisible || !Context.Perception.HasLineOfSightTo(t))
        {
            if (Time.time >= nextLosSearch || !hasLosPoint)
            {
                nextLosSearch = Time.time + 1f;
                hasLosPoint = Motor.TryFindLineOfSightPoint(t, min, max, out losPoint);
            }
            if (hasLosPoint)
                Motor.MoveTo(losPoint, MobMoveMode.Run, 0.3f);
            else
                Motor.MoveTo(t.Position, MobMoveMode.Run, Mathf.Max(1f, min));
            return;
        }
        hasLosPoint = false;

        if (dist > max)
        {
            Vector3 dir = CombatQuery.FlatDirection(t.Position, Position, Context.Transform.forward);
            Motor.MoveTo(t.Position + dir * ideal, MobMoveMode.Run, 0.5f);
            return;
        }
        if (dist < min)
        {
            Vector3 dir = CombatQuery.FlatDirection(t.Position, Position, -Context.Transform.forward);
            if (Motor.SamplePoint(Position + dir * (min - dist + 1.5f), 2f, out Vector3 back))
                Motor.MoveTo(back, MobMoveMode.Strafe, 0.3f);
            return;
        }

        // In the band: strafe sideways to be harder to hit.
        if (Profile.strafeAmount > 0.05f)
        {
            Vector3 toT = CombatQuery.FlatDirection(Position, t.Position, Context.Transform.forward);
            Vector3 side = Vector3.Cross(Vector3.up, toT) * strafeDir;
            Vector3 goal = Position + side * (2f + 2f * Profile.strafeAmount) + toT * (dist - ideal) * 0.5f;
            if (Motor.SamplePoint(goal, 1.5f, out Vector3 s) && Motor.IsSafe(s))
                Motor.MoveTo(s, MobMoveMode.Strafe, 0.3f);
            else
                strafeDir = -strafeDir;
        }
        else
        {
            Motor.Stop();
        }
    }
}
