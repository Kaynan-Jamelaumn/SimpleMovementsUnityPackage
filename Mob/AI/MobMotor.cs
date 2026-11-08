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
/// <para>On top of the path it applies the profile's <see cref="MobLocomotion"/>: the medium the mob is in (ground,
/// water, air) and its speed there, the height its body is held at above the NavMesh (a flyer's altitude, a swimmer's
/// depth under the surface, a hop's arc - through the agent's Base Offset, which the network carries with the
/// position), the sideways drift of a zigzag or a slither, and where it may go at all (an aquatic mob only into water,
/// a ground mob not into water deeper than it can wade). Points it may not go to are never chosen to wander, flee or
/// flank to, and a destination it may not reach is moved to the nearest place it may (the water's edge), the path then
/// counting as incomplete so the AI treats the target as out of reach.</para>
/// </summary>
public sealed class MobMotor
{
    private readonly MobMovementContext ctx;
    private readonly NavMeshAgent agent;
    private readonly Transform transform;
    private readonly NavMeshPath scratchPath = new NavMeshPath();
    private readonly MobLocomotion loco;

    private MobGaitState gait;
    private MobGaitStep step = MobGaitStep.Still;
    private float mediumHeight;          // the height held above the NavMesh for the medium (altitude, swim depth), eased
    private float originalBaseOffset;
    private float waterSurface = float.NaN;
    private float waterDepth;
    private float nextWaterSample;
    private float idleTime;
    private bool landed;                 // a flyer resting on the ground
    private float strandedTime;
    private float nextStrandedHit;
    private float nextWaterSearch;
    private Vector3 asked;               // the place last asked for (before the habitat moved it)
    private float nextUnreachableTry;
    private bool pulsedAcceleration;
    private float corpseFallSpeed;
    private bool corpseSettling;
    private float corpseTargetY;

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
    /// <summary>The last path request could not reach its destination (partial / invalid path, or a place its habitat keeps it out of).</summary>
    public bool PathIncomplete { get; private set; }

    /// <summary>Where it lives and how it moves (the profile's).</summary>
    public MobLocomotion Locomotion => loco;
    /// <summary>What it is moving through now: the ground, water or the air.</summary>
    public MobMedium Medium { get; private set; } = MobMedium.Ground;
    public bool IsFlying => Medium == MobMedium.Air;
    public bool IsSwimming => Medium == MobMedium.Water;
    /// <summary>In the air in the middle of a hop, bounce or leap (not flying).</summary>
    public bool IsAirborne => step.airborne;
    /// <summary>For the model: 1 its normal shape, under 1 squashed, over 1 stretched (a slime's bounce).</summary>
    public float Squash => step.squash;
    /// <summary>How high its body is above the NavMesh under it (metres): altitude, swim height, a hop's arc.</summary>
    public float Lift => agent != null ? agent.baseOffset - originalBaseOffset : 0f;
    /// <summary>An aquatic mob out of water.</summary>
    public bool IsStranded => strandedTime > 0f;
    /// <summary>A flyer that has landed to rest.</summary>
    public bool IsLanded => landed;

    public MobMotor(MobMovementContext context)
    {
        ctx = context;
        agent = context.NavMeshAgentReference;
        transform = context.Transform;
        lastPosition = transform.position;
        loco = context.Profile.locomotion ?? new MobLocomotion();
        gait = MobGaitState.For(transform.GetInstanceID());
        if (agent != null)
        {
            baseAngularSpeed = agent.angularSpeed;
            originalBaseOffset = agent.baseOffset;
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, context.Profile.turnSpeed);
            agent.acceleration = Mathf.Max(agent.acceleration, context.Profile.acceleration);
            agent.autoBraking = true;
            // Hops and darts change speed sharply: the agent must follow at once.
            pulsedAcceleration = loco.landGait == MobLandGait.Hop || loco.landGait == MobLandGait.Bounce || loco.landGait == MobLandGait.Leap ||
                                 loco.waterGait == MobWaterGait.Dart;
            if (pulsedAcceleration)
                agent.acceleration = Mathf.Max(agent.acceleration, 80f);
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

    /// <summary>
    /// The pace of <paramref name="mode"/> in the medium it is in now: <see cref="SpeedFor"/> × the medium's speed (a
    /// crawler's run capped at its Crawl Run Limit), before crowd control and the gait's own rhythm.
    /// </summary>
    public float PaceIn(MobMoveMode mode)
    {
        float pace = SpeedFor(mode);
        if (Medium == MobMedium.Ground && loco.landGait == MobLandGait.Crawl)
            pace = Mathf.Min(pace, SpeedFor(MobMoveMode.Walk) * loco.crawlRunLimit);
        return pace * loco.SpeedIn(Medium) * (IsStranded ? 0.25f : 1f);
    }

    /// <summary>The running pace in the medium it is in now (scales the movement animations).</summary>
    public float RunPace => PaceIn(MobMoveMode.Run);

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

        // The medium (and the water under it), the habitat's rules for it.
        SenseMedium(dt);

        // Speed: pace in the medium x crowd control x casting, then the gait's rhythm (a hop moves only in the air).
        float pace = PaceIn(Mode) * ctx.Entity.MoveSpeedMultiplier;
        bool displaced = ctx.Entity.IsBeingDisplaced;
        bool arrived = hasDestination && !agent.pathPending && agent.remainingDistance <= stoppingDistance + 0.05f;
        bool travelling = hasDestination && !displaced && pace > 0.01f && !arrived; // arrived: no zigzag on the spot (a hop still lands)
        step = GaitStep(travelling ? pace : 0f, dt);
        float speed = pace * step.speedScale;
        agent.speed = Mathf.Max(0f, speed);
        bool shouldStop = displaced || pace <= 0.01f || !hasDestination;
        if (agent.isStopped != shouldStop && !displaced)
            agent.isStopped = shouldStop;

        // Height above the NavMesh: the medium's (eased at its climb and dive rates) plus the gait's (a hop's arc).
        if (!displaced)
        {
            float target = MediumHeight();
            float up = Medium == MobMedium.Air ? loco.climbRate : Medium == MobMedium.Water ? 1.5f : 4f;
            float down = Medium == MobMedium.Air ? loco.diveRate : Medium == MobMedium.Water ? 1.5f : 6f;
            mediumHeight = MobGait.Approach(mediumHeight, target, up, down, dt);
            float offset = originalBaseOffset + mediumHeight + step.lift;
            if (Mathf.Abs(agent.baseOffset - offset) > 0.0005f)
                agent.baseOffset = offset;
            // Sideways: a zigzag, a slither (the path's own steering brings it back to its line).
            if (travelling && Mathf.Abs(step.lateral) > 0.001f)
            {
                Vector3 right = transform.right;
                right.y = 0f;
                if (right.sqrMagnitude > 1e-4f)
                    agent.Move(right.normalized * (step.lateral * dt));
            }
        }

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
        if (trying && moved < 0.2f * pace * dt * (pulsedAcceleration ? 0.25f : 1f) + 0.001f)
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

    // ------------------------------------------------------------------ locomotion
    private float BodyHeight => ctx.Entity != null ? Mathf.Max(0.2f, ctx.Entity.Height) : 1.8f;

    /// <summary>The NavMesh height under the mob (its feet without the Base Offset).</summary>
    private float GroundY => agent != null && agent.isOnNavMesh ? agent.nextPosition.y : transform.position.y;

    /// <summary>
    /// The water under the mob (sampled a few times a second) and the medium it is in by its habitat; a flyer's landing
    /// and taking off; an aquatic mob stranded on land suffocating and heading for water.
    /// </summary>
    private void SenseMedium(float dt)
    {
        float now = Time.time;
        if (now >= nextWaterSample)
        {
            nextWaterSample = now + 0.15f;
            Vector3 feet = agent.nextPosition;
            if (WaterQuery.TryGetWater(new Vector3(feet.x, feet.y + 0.05f, feet.z), transform.gameObject, out float surface, out _) && surface > feet.y)
            {
                waterSurface = surface;
                waterDepth = surface - feet.y;
            }
            else
            {
                waterSurface = float.NaN;
                waterDepth = 0f;
            }
        }

        float h = BodyHeight;
        switch (loco.habitat)
        {
            case MobHabitat.Flying:
            {
                bool busy = hasDestination || (ctx.Brain != null && (ctx.Brain.InCombat || ctx.Brain.Mode == MobMode.Flee));
                if (busy)
                {
                    idleTime = 0f;
                    landed = false; // takes off
                }
                else
                {
                    idleTime += dt;
                    if (loco.landsToRest && !landed && idleTime >= loco.restAfter && waterDepth < loco.WadeDepth(h))
                        landed = true;
                }
                Medium = landed ? MobMedium.Ground : MobMedium.Air;
                break;
            }
            case MobHabitat.Aquatic:
            case MobHabitat.Amphibious:
                Medium = waterDepth >= loco.minSwimDepth ? MobMedium.Water : MobMedium.Ground;
                break;
            default:
                // A land animal swims (paddling, head out) only where it cannot wade.
                Medium = waterDepth > loco.WadeDepth(h) ? MobMedium.Water : MobMedium.Ground;
                break;
        }

        if (loco.habitat == MobHabitat.Aquatic && Medium != MobMedium.Water && ctx.Entity != null && ctx.Entity.IsAlive)
            Stranded(dt, now);
        else
            strandedTime = 0f;

        // A land animal pushed into deep water, with nothing else to do, swims for the shore.
        if (loco.habitat == MobHabitat.Ground && Medium == MobMedium.Water && !hasDestination && now >= nextWaterSearch)
        {
            nextWaterSearch = now + 1f;
            if (TryFindAllowedNear(transform.position, 14f, out Vector3 shore))
                MoveTo(shore, MobMoveMode.Walk, 0.3f, true);
        }
    }

    /// <summary>An aquatic mob out of water: after its grace it suffocates (a hit a second); with nothing else to do it heads for water.</summary>
    private void Stranded(float dt, float now)
    {
        strandedTime += dt;
        if (loco.strandedDamagePerSecond > 0f && strandedTime > loco.strandedGrace && now >= nextStrandedHit)
        {
            nextStrandedHit = now + 1f;
            ctx.Entity.ApplyDamage(new DamageInfo
            {
                amount = loco.strandedDamagePerSecond,
                target = ctx.Entity,
                point = transform.position,
                direction = Vector3.up,
                type = DamageType.True,
                isPeriodic = true,
            });
        }
        if (!hasDestination && now >= nextWaterSearch)
        {
            nextWaterSearch = now + 1f;
            if (TryFindAllowedNear(transform.position, 14f, out Vector3 water))
                MoveTo(water, MobMoveMode.Run, 0.3f, true);
        }
    }

    /// <summary>This frame of the gait of the medium it is in, at <paramref name="speed"/> (0 = not travelling).</summary>
    private MobGaitStep GaitStep(float speed, float dt)
    {
        float h = BodyHeight;
        switch (Medium)
        {
            case MobMedium.Air: return MobGait.Air(loco.airGait, loco.AirShape(h), ref gait, speed, dt);
            case MobMedium.Water:
                return MobGait.Water(loco.habitat == MobHabitat.Ground ? MobWaterGait.Paddle : loco.waterGait, loco.WaterShape(h), ref gait, speed, dt);
            default:
                if (IsStranded)
                    return MobGait.Land(MobLandGait.Hop, MobGait.DefaultShape(MobLandGait.Hop, h * 0.5f), ref gait, speed, dt); // flopping
                return MobGait.Land(loco.landGait, loco.LandShape(h), ref gait, speed, dt);
        }
    }

    /// <summary>The height to hold above the NavMesh in the medium it is in (before the gait's own lift).</summary>
    private float MediumHeight()
    {
        float ground = GroundY;
        switch (Medium)
        {
            case MobMedium.Air:
            {
                float altitude = loco.cruiseAltitude;
                CombatEntity target = ctx.Brain != null && ctx.Brain.InCombat ? ctx.Brain.Target : null;
                if (target != null && CombatQuery.FlatDistance(transform.position, target.Position) <= loco.descendWithin)
                    altitude = loco.combatAltitude;
                return MobGait.FlyOffset(ground, waterSurface, altitude);
            }
            case MobMedium.Water:
            {
                if (float.IsNaN(waterSurface))
                    return 0f;
                bool surface = loco.habitat == MobHabitat.Ground || loco.waterGait == MobWaterGait.Paddle || loco.swimDepth <= 0f;
                return MobGait.SwimOffset(waterSurface, ground, loco.swimDepth, BodyHeight * 0.5f, surface);
            }
            default:
                return 0f;
        }
    }

    /// <summary>The depth of water over <paramref name="point"/> (a NavMesh point; 0 = dry land).</summary>
    private float WaterDepthAt(Vector3 point) =>
        WaterQuery.TryGetWater(new Vector3(point.x, point.y + 0.05f, point.z), transform.gameObject, out float surface, out _) ? Mathf.Max(0f, surface - point.y) : 0f;

    /// <summary>
    /// May its habitat take it to <paramref name="point"/> (a NavMesh point)? Aquatic mobs only into water at least Min
    /// Swim Depth deep, ground mobs not into water deeper than they wade; flyers and amphibians anywhere.
    /// </summary>
    public bool AllowsPoint(Vector3 point)
    {
        if (loco.habitat == MobHabitat.Flying || loco.habitat == MobHabitat.Amphibious)
            return true;
        return loco.AllowsDepth(WaterDepthAt(point), BodyHeight);
    }

    /// <summary>
    /// The place nearest <paramref name="destination"/> it may go to, looking back along the way from the destination to
    /// the mob (a shore for a fish chasing someone on land), then around the mob.
    /// </summary>
    private bool TryClampToAllowed(Vector3 destination, out Vector3 point)
    {
        Vector3 from = transform.position;
        for (int i = 1; i <= 10; i++)
        {
            Vector3 p = Vector3.Lerp(destination, from, i / 10f);
            if (SamplePoint(p, 1.5f, out Vector3 s) && AllowsPoint(s))
            {
                point = s;
                return true;
            }
        }
        return TryFindAllowedNear(from, 10f, out point);
    }

    /// <summary>A NavMesh point within <paramref name="radius"/> of <paramref name="center"/> that its habitat allows, nearest first.</summary>
    private bool TryFindAllowedNear(Vector3 center, float radius, out Vector3 point)
    {
        for (int ring = 1; ring <= 4; ring++)
        {
            float r = radius * ring / 4f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = center + MobCombatCoordinator.Direction(i * 45f + ring * 11f) * r;
                if (SamplePoint(p, Mathf.Max(1.5f, r * 0.3f), out Vector3 s) && AllowsPoint(s))
                {
                    point = s;
                    return true;
                }
            }
        }
        point = center;
        return false;
    }

    // ------------------------------------------------------------------ the corpse
    /// <summary>
    /// The mob died (its agent is about to go off): a flyer's or a hopper's body falls to the ground, a swimmer's floats
    /// up to the surface - a carcass hanging in the air or deep under water could not be reached. Call
    /// <see cref="SettleCorpse"/> each frame afterwards.
    /// </summary>
    public void BeginCorpseSettle()
    {
        corpseSettling = false;
        if (agent == null)
            return;
        float lift = Lift;
        float ground = GroundY;
        if (Medium == MobMedium.Water && !float.IsNaN(waterSurface))
        {
            corpseTargetY = waterSurface - BodyHeight * 0.35f; // floating, mostly under
            corpseSettling = Mathf.Abs(transform.position.y - corpseTargetY) > 0.05f;
        }
        else if (lift > 0.05f)
        {
            corpseTargetY = ground + originalBaseOffset;
            corpseSettling = true;
        }
        corpseFallSpeed = 0f;
    }

    /// <summary>Moves a settling corpse a frame (falling under gravity, or floating up). Returns true while it still moves.</summary>
    public bool SettleCorpse(float dt)
    {
        if (!corpseSettling)
            return false;
        Vector3 p = transform.position;
        if (p.y > corpseTargetY)
        {
            corpseFallSpeed += 9.81f * dt;
            p.y = Mathf.Max(corpseTargetY, p.y - corpseFallSpeed * dt);
        }
        else
        {
            p.y = Mathf.Min(corpseTargetY, p.y + 0.8f * dt);
        }
        transform.position = p;
        corpseSettling = Mathf.Abs(p.y - corpseTargetY) > 0.001f;
        return corpseSettling;
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

        // Throttled by the place asked for (before any change for the habitat).
        float moved = (destination - asked).sqrMagnitude;
        float interval = mode == MobMoveMode.Run ? 0.2f : 0.4f;
        if (!force && hasDestination && moved < 0.25f && Time.time - lastRequestTime < 1f)
            return true;
        if (!force && hasDestination && moved < 4f && Time.time - lastRequestTime < interval)
            return true;
        if (!force && !hasDestination && moved < 4f && Time.time < nextUnreachableTry)
            return false; // looked for a way there a moment ago and there was none

        // A place its habitat keeps it out of (dry land for a fish, a lake for a wolf): the nearest place it may go
        // instead, and the path counts as incomplete (the target is out of its reach).
        asked = destination;
        bool clamped = false;
        if (!AllowsPoint(destination))
        {
            if (!TryClampToAllowed(destination, out Vector3 allowed))
            {
                PathIncomplete = true;
                nextUnreachableTry = Time.time + 1f;
                return false;
            }
            destination = allowed;
            clamped = true;
        }

        requested = destination;
        lastRequestTime = Time.time;
        hasDestination = true;
        bool ok = agent.SetDestination(destination);
        if (!ok && CombatQuery.SampleNavMesh(destination, 3f, out Vector3 onMesh, agent.areaMask))
        {
            requested = onMesh;
            ok = agent.SetDestination(onMesh);
        }
        PathIncomplete = clamped || !ok || (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete);
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
        if (actual < distance * 0.4f || !AllowsPoint(to))
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
        if (!Usable || !AllowsPoint(point))
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
            if (!AllowsPoint(s))
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
            if (!AllowsPoint(candidate))
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
                if (IsSafe(s, 2f) && AllowsPoint(s))
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
            if (!IsSafe(s, 1.5f) || !AllowsPoint(s))
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
            if (!IsSafe(s, 2f) || !AllowsPoint(s))
                continue;
            if (!CombatQuery.HasLineOfSight(s + Vector3.up * (eye + MediumLiftAt(s)), target.AimPosition))
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
            agent.baseOffset = originalBaseOffset;
        }
    }

    /// <summary>The height its body would be held at over <paramref name="point"/> (a flyer's altitude; 0 on the ground): where its eyes would be.</summary>
    private float MediumLiftAt(Vector3 point) => loco.habitat == MobHabitat.Flying ? MobGait.FlyOffset(point.y, float.NaN, loco.combatAltitude) : 0f;
}
