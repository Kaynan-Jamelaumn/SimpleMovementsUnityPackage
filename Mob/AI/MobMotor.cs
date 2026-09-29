using UnityEngine;
using UnityEngine.AI;

/// <summary>Movement pace of a mob.</summary>
public enum MobMoveMode
{
    Walk,
    Run,
    Strafe,
}

/// <summary>
/// Everything about moving a mob with its NavMeshAgent: speed (from its Speed Manager, crowd control and casting),
/// throttled path requests, arrival and stuck detection, facing a target while strafing, dodging, recovering when
/// spawned off the NavMesh, and finding good points to go to (wander, flee, flank, line of sight) that avoid hazards.
/// </summary>
public sealed class MobMotor
{
    private readonly MobMovementContext ctx;
    private readonly NavMeshAgent agent;
    private readonly Transform transform;
    private readonly NavMeshPath scratchPath = new NavMeshPath();

    private Vector3 requested;
    private float lastRequestTime = -999f;
    private float stoppingDistance = 0.3f;
    private bool hasDestination;
    private Vector3 lastPosition;
    private float stuckTimer;
    private float nextOffMeshAttempt;
    private bool facingOverride;
    private Vector3 facePoint;
    private bool faceIsDirection;
    private Vector3 faceDirection;
    private bool warnedOffMesh;
    private float baseAngularSpeed;

    public MobMoveMode Mode { get; private set; } = MobMoveMode.Walk;
    public bool HasDestination => hasDestination;
    public Vector3 Destination => requested;
    /// <summary>True when the mob has been trying to move without progress for a while.</summary>
    public bool IsStuck { get; private set; }
    /// <summary>True when the agent is not on a NavMesh (and could not be moved onto one).</summary>
    public bool IsOffMesh { get; private set; }
    /// <summary>The last path request could not reach its destination (partial / invalid path).</summary>
    public bool PathIncomplete { get; private set; }

    public MobMotor(MobMovementContext context)
    {
        ctx = context;
        agent = context.NavMeshAgentReference;
        transform = context.Transform;
        lastPosition = transform.position;
        if (agent != null)
        {
            baseAngularSpeed = agent.angularSpeed;
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, context.Profile.turnSpeed);
            agent.acceleration = Mathf.Max(agent.acceleration, context.Profile.acceleration);
            agent.autoBraking = true;
        }

        // A non-kinematic Rigidbody fights the NavMeshAgent (jitter, sliding, falling through slopes).
        Rigidbody rb = context.Entity != null ? context.Entity.Body : null;
        if (rb != null && !rb.isKinematic && agent != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    public bool Usable => agent != null && agent.enabled && agent.isOnNavMesh;

    /// <summary>Can the mob move by itself right now (not stunned/rooted/displaced, on the NavMesh)?</summary>
    public bool CanMove => Usable && ctx.Entity.CanMove && !ctx.Entity.IsBeingDisplaced;

    public Vector3 Velocity => Usable ? agent.velocity : Vector3.zero;

    public float CurrentSpeed
    {
        get
        {
            Vector3 v = Velocity;
            return new Vector2(v.x, v.z).magnitude;
        }
    }

    /// <summary>The Speed Manager's current speed (the mob's base movement speed).</summary>
    public float BaseSpeed
    {
        get
        {
            SpeedManager sm = ctx.StatusController != null ? ctx.StatusController.SpeedManager : null;
            if (sm != null && sm.Speed > 0.01f)
                return sm.Speed;
            return agent != null && agent.speed > 0.01f ? agent.speed : 3.5f;
        }
    }

    public float SpeedFor(MobMoveMode mode)
    {
        MobProfile p = ctx.Profile;
        switch (mode)
        {
            case MobMoveMode.Run: return BaseSpeed * p.runSpeedMultiplier;
            case MobMoveMode.Strafe: return BaseSpeed * p.strafeSpeedMultiplier;
            default: return BaseSpeed * p.walkSpeedMultiplier;
        }
    }

    public float RunSpeed => SpeedFor(MobMoveMode.Run);

    // ------------------------------------------------------------------ per frame
    public void Tick(float dt)
    {
        if (agent == null || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
        {
            TryRecoverNavMesh();
            return;
        }
        IsOffMesh = false;

        // Speed: pace x crowd control x casting.
        float speed = SpeedFor(Mode) * ctx.Entity.MoveSpeedMultiplier;
        bool displaced = ctx.Entity.IsBeingDisplaced;
        agent.speed = Mathf.Max(0f, speed);
        bool shouldStop = displaced || speed <= 0.01f || !hasDestination;
        if (agent.isStopped != shouldStop && !displaced)
            agent.isStopped = shouldStop;

        // Facing.
        if (facingOverride)
        {
            agent.updateRotation = false;
            Vector3 dir = faceIsDirection ? faceDirection : facePoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f && ctx.Entity.CanMove)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, ctx.Profile.turnSpeed * dt);
            }
        }
        else if (!agent.updateRotation)
        {
            agent.updateRotation = true;
        }
        facingOverride = false; // must be requested every frame

        // Stuck detection.
        Vector3 pos = transform.position;
        float moved = (pos - lastPosition).magnitude;
        lastPosition = pos;
        bool trying = hasDestination && !agent.pathPending && !shouldStop && !HasArrived();
        if (trying && moved < 0.2f * speed * dt + 0.001f)
        {
            stuckTimer += dt;
            if (stuckTimer > 1.5f && !IsStuck)
            {
                IsStuck = true;
                // Nudge: ask again; a fresh path often solves temporary blocks (other mobs, carving).
                agent.ResetPath();
                agent.SetDestination(requested);
            }
        }
        else
        {
            stuckTimer = 0f;
            if (moved > 0.05f)
                IsStuck = false;
        }
    }

    private void TryRecoverNavMesh()
    {
        IsOffMesh = true;
        if (Time.time < nextOffMeshAttempt)
            return;
        nextOffMeshAttempt = Time.time + 1f;
        if (CombatQuery.SampleNavMesh(transform.position, 4f, out Vector3 onMesh, agent.areaMask))
        {
            agent.Warp(onMesh);
            IsOffMesh = false;
            warnedOffMesh = false;
            return;
        }
        if (!warnedOffMesh)
        {
            warnedOffMesh = true;
            Debug.LogWarning($"[{ctx.MobReference.name}] is not on a NavMesh (bake the NavMesh or place the mob on it). It will wait until one is available.", ctx.MobReference);
        }
    }

    // ------------------------------------------------------------------ commands
    /// <summary>
    /// Walks/runs to <paramref name="destination"/>. Repeated calls with nearly the same point are cheap (the path is
    /// only recalculated when the point moved or after a short interval). Returns false if the agent cannot move.
    /// </summary>
    public bool MoveTo(Vector3 destination, MobMoveMode mode, float stopDistance = 0.3f, bool force = false)
    {
        Mode = mode;
        if (!Usable)
            return false;
        stoppingDistance = Mathf.Max(0.05f, stopDistance);
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = mode != MobMoveMode.Run;

        float moved = (destination - requested).sqrMagnitude;
        float interval = mode == MobMoveMode.Run ? 0.2f : 0.4f;
        if (!force && hasDestination && moved < 0.25f && Time.time - lastRequestTime < 1f)
            return true;
        if (!force && hasDestination && moved < 4f && Time.time - lastRequestTime < interval)
            return true;

        requested = destination;
        lastRequestTime = Time.time;
        hasDestination = true;
        bool ok = agent.SetDestination(destination);
        if (!ok && CombatQuery.SampleNavMesh(destination, 3f, out Vector3 onMesh, agent.areaMask))
        {
            requested = onMesh;
            ok = agent.SetDestination(onMesh);
        }
        PathIncomplete = !ok || (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete);
        return ok;
    }

    /// <summary>Stops moving (keeps facing requests working).</summary>
    public void Stop()
    {
        hasDestination = false;
        IsStuck = false;
        stuckTimer = 0f;
        if (Usable)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    public void SetMode(MobMoveMode mode) => Mode = mode;

    /// <summary>True when the current destination has been reached (or there is none).</summary>
    public bool HasArrived(float extra = 0.25f)
    {
        if (!hasDestination || !Usable)
            return true;
        if (agent.pathPending)
            return false;
        if (agent.remainingDistance <= stoppingDistance + extra)
            return true;
        // Also arrived when very close in a straight line (remainingDistance can lag behind).
        return CombatQuery.FlatDistance(transform.position, requested) <= stoppingDistance + extra;
    }

    public float RemainingDistance => Usable && hasDestination && !agent.pathPending ? agent.remainingDistance : 0f;

    /// <summary>Faces a point this frame (disables the agent's own rotation). Call every frame while needed.</summary>
    public void FaceTowards(Vector3 point)
    {
        facingOverride = true;
        faceIsDirection = false;
        facePoint = point;
    }

    /// <summary>Faces a direction this frame (used while casting).</summary>
    public void FaceDirection(Vector3 direction)
    {
        facingOverride = true;
        faceIsDirection = true;
        faceDirection = direction;
    }

    /// <summary>Turns instantly toward a point (used when an attack starts).</summary>
    public void SnapFacing(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    /// <summary>A quick sidestep/roll. Returns false if there is no room.</summary>
    public bool Dodge(Vector3 direction, float distance, float duration)
    {
        if (!Usable || ctx.Entity.IsBeingDisplaced || !ctx.Entity.CanMove)
            return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-4f)
            return false;
        direction.Normalize();
        Vector3 from = transform.position;
        Vector3 to = from + direction * distance;
        if (NavMesh.Raycast(from, to, out NavMeshHit hit, agent.areaMask))
            to = hit.position;
        float actual = CombatQuery.FlatDistance(from, to);
        if (actual < distance * 0.4f)
            return false;
        Stop();
        ForcedMovement.GetOrAdd(ctx.Entity).Begin(to - from, duration, 0.25f, false);
        return true;
    }

    // ------------------------------------------------------------------ point finding
    /// <summary>A reachable NavMesh point near <paramref name="point"/> (false if none).</summary>
    public bool SamplePoint(Vector3 point, float maxDistance, out Vector3 result)
    {
        int mask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        return CombatQuery.SampleNavMesh(point, maxDistance, out result, mask);
    }

    /// <summary>True if a full path exists from the mob to <paramref name="point"/> (costs a path calculation).</summary>
    public bool IsReachable(Vector3 point)
    {
        if (!Usable)
            return false;
        if (!NavMesh.CalculatePath(transform.position, point, agent.areaMask, scratchPath))
            return false;
        return scratchPath.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>Is standing at <paramref name="point"/> safe from hazards for the next <paramref name="horizon"/> seconds?</summary>
    public bool IsSafe(Vector3 point, float horizon = 1.5f)
    {
        if (!ctx.Profile.avoidHazards)
            return true;
        CombatEntity e = ctx.Entity;
        return !HazardRegistry.Query(e, point, e.Radius, e.Height, horizon, out _);
    }

    /// <summary>A random reachable, safe point within <paramref name="radius"/> of <paramref name="center"/>.</summary>
    public bool TryFindWanderPoint(Vector3 center, float radius, out Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (!SamplePoint(p, Mathf.Max(2f, radius * 0.3f), out Vector3 s))
                continue;
            if (CombatQuery.FlatDistance(s, transform.position) < 1.5f)
                continue;
            if (!IsSafe(s, 3f))
                continue;
            point = s;
            return true;
        }
        point = transform.position;
        return false;
    }

    /// <summary>
    /// The best point to run to, away from <paramref name="threat"/>: far from it, reachable, not through it, safe
    /// from hazards. <paramref name="quality"/> is 0 (cornered) to 1 (clear escape).
    /// </summary>
    public bool TryFindEscapePoint(Vector3 threat, float distance, out Vector3 best, out float quality)
    {
        Vector3 pos = transform.position;
        Vector3 away = CombatQuery.FlatDirection(threat, pos, -transform.forward);
        float bestScore = float.MinValue;
        best = pos;
        quality = 0f;
        float startDist = CombatQuery.FlatDistance(pos, threat);
        for (int i = 0; i < 9; i++)
        {
            float angle = (i - 4) * 25f + Random.Range(-8f, 8f);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * away;
            Vector3 target = pos + dir * distance;
            Vector3 candidate;
            if (Usable && NavMesh.Raycast(pos, target, out NavMeshHit hit, agent.areaMask))
                candidate = hit.position;
            else if (!SamplePoint(target, 3f, out candidate))
                continue;
            float gained = CombatQuery.FlatDistance(candidate, threat) - startDist;
            float travel = CombatQuery.FlatDistance(candidate, pos);
            if (travel < 1f)
                continue;
            float score = gained + travel * 0.3f - Mathf.Abs(angle) * 0.02f;
            if (!IsSafe(candidate, 2f))
                score -= 10f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                quality = Mathf.Clamp01(gained / Mathf.Max(1f, distance * 0.6f));
            }
        }
        return bestScore > float.MinValue;
    }

    /// <summary>The nearest safe point (out of hazards) around the mob.</summary>
    public bool TryFindSafePoint(Vector3 preferredDirection, float radius, out Vector3 point)
    {
        Vector3 pos = transform.position;
        Vector3 fwd = preferredDirection.sqrMagnitude > 1e-4f ? preferredDirection.normalized : transform.right;
        for (int ring = 1; ring <= 3; ring++)
        {
            float r = radius * ring / 3f + 1f;
            for (int i = 0; i < 8; i++)
            {
                float angle = (i % 2 == 0 ? 1 : -1) * (i / 2) * 45f;
                Vector3 p = pos + Quaternion.AngleAxis(angle, Vector3.up) * fwd * r;
                if (!SamplePoint(p, 1.5f, out Vector3 s))
                    continue;
                if (IsSafe(s, 2f))
                {
                    point = s;
                    return true;
                }
            }
        }
        point = pos;
        return false;
    }

    /// <summary>A reachable point at <paramref name="angleDeg"/> (world yaw) and <paramref name="radius"/> around <paramref name="center"/>.</summary>
    public bool TryFindPointAround(Vector3 center, float radius, float angleDeg, out Vector3 point)
    {
        for (int i = 0; i < 5; i++)
        {
            float a = angleDeg + (i % 2 == 0 ? 1 : -1) * (i / 2 + (i > 0 ? 1 : 0)) * 25f;
            Vector3 p = center + MobCombatCoordinator.Direction(a) * radius;
            if (!SamplePoint(p, 1.5f, out Vector3 s))
                continue;
            if (!IsSafe(s, 1.5f))
                continue;
            point = s;
            return true;
        }
        point = center;
        return false;
    }

    /// <summary>A point between <paramref name="minRange"/> and <paramref name="maxRange"/> of the target with a clear shot.</summary>
    public bool TryFindLineOfSightPoint(CombatEntity target, float minRange, float maxRange, out Vector3 point)
    {
        Vector3 tp = target.Position;
        Vector3 from = CombatQuery.FlatDirection(tp, transform.position, Vector3.forward);
        float baseAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
        float eye = ctx.Profile.EffectiveEyeHeight(ctx.Entity.Height);
        float range = Mathf.Clamp((minRange + maxRange) * 0.5f, minRange, maxRange);
        for (int i = 0; i < 8; i++)
        {
            float a = baseAngle + (i % 2 == 0 ? 1 : -1) * (i / 2 + 1) * 30f;
            Vector3 p = tp + MobCombatCoordinator.Direction(a) * range;
            if (!SamplePoint(p, 2f, out Vector3 s))
                continue;
            if (!IsSafe(s, 2f))
                continue;
            if (!CombatQuery.HasLineOfSight(s + Vector3.up * eye, target.AimPosition))
                continue;
            point = s;
            return true;
        }
        point = transform.position;
        return false;
    }

    public void Dispose()
    {
        if (agent != null && agent.enabled)
        {
            agent.updateRotation = true;
            agent.angularSpeed = baseAngularSpeed > 0f ? baseAngularSpeed : agent.angularSpeed;
        }
    }
}
