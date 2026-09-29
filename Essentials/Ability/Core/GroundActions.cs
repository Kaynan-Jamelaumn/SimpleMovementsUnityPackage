using System;
using System.Collections.Generic;
using UnityEngine;

// ====================================================================================================== ground surge
/// <summary>Eruptions that travel along the ground: spike lines, shockwave rings, fissure stars, seeking tremors.</summary>
[Serializable, AbilityMenu("Ground/Ground Surge", "Eruptions travelling along the ground: a line of spikes, an expanding shockwave ring, a star of fissures or a tremor that seeks the target.", 0)]
public class GroundSurgeAction : CastAction, ITravellingAction
{
    [Tooltip("Line: straight ahead. Ring: expanding shockwave. Star: several lines around the origin. Seek: a line that turns toward the target.")]
    public SurgePattern pattern = SurgePattern.Line;
    [Tooltip("Star: number of lines spread evenly around. Line: parallel lines side by side.")]
    [Min(1)] public int lines = 1;
    [Tooltip("Line with several lines: angle between them (degrees). 0 = parallel lines 'Eruption Radius x 2' apart.")]
    [Range(0f, 90f)] public float lineAngle = 0f;
    [Tooltip("How far it travels (metres, multiplied by the Range modifier).")]
    [Min(1f)] public float distance = 12f;
    [Tooltip("Travel speed (metres per second).")]
    [Min(0.5f)] public float speed = 14f;
    [Tooltip("Distance between eruptions (metres).")]
    [Min(0.3f)] public float step = 1.5f;
    [Tooltip("Radius of each eruption (Line/Star/Seek) or thickness of the wave (Ring), in metres.")]
    [Min(0.2f)] public float eruptionRadius = 1.2f;
    [Tooltip("Seek: degrees per second it turns toward the target.")]
    [Min(0f)] public float seekTurnRate = 90f;
    [Tooltip("Stop a line when it reaches a wall.")]
    public bool stopAtObstacles = true;
    [Tooltip("Each character is hit only once by the whole surge.")]
    public bool hitOncePerTarget = true;
    [Tooltip("Vertical reach of each eruption (metres).")]
    [Min(0.2f)] public float height = 2.5f;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 14f }, new KnockbackEffect { direction = DisplacementDirection.Up, distance = 1.5f });

    [Header("Feedback")]
    [Tooltip("Effect spawned at each eruption (spikes, dust, lava).")]
    public GameObject eruptionVfx;
    public AudioClip eruptionSound;

    public float Distance(in AbilityStats s) => distance * s.range;
    public float TravelTime(in AbilityStats s, float d) => d / Mathf.Max(0.1f, speed);

    public override void Execute(AbilityCastInstance cast)
    {
        ActionFrame frame = cast.GetFrame(anchor);
        AbilityRuntime.Add(new SurgeInstance(this, cast, frame));
        cast.EmitNoise(frame.position, 1f);
    }

    /// <summary>Directions (flat) of the lines for a frame.</summary>
    internal int GetLineDirections(Quaternion rotation, List<Vector3> dirs, List<Vector3> lateralOffsets)
    {
        dirs.Clear();
        lateralOffsets.Clear();
        Vector3 fwd = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;
        switch (pattern)
        {
            case SurgePattern.Star:
            {
                int n = Mathf.Max(1, lines);
                for (int i = 0; i < n; i++)
                {
                    dirs.Add(Quaternion.AngleAxis(360f / n * i, Vector3.up) * fwd);
                    lateralOffsets.Add(Vector3.zero);
                }
                break;
            }
            case SurgePattern.Ring:
                break;
            default:
            {
                int n = pattern == SurgePattern.Seek ? 1 : Mathf.Max(1, lines);
                for (int i = 0; i < n; i++)
                {
                    float k = i - (n - 1) * 0.5f;
                    if (lineAngle > 0f)
                    {
                        dirs.Add(Quaternion.AngleAxis(k * lineAngle, Vector3.up) * fwd);
                        lateralOffsets.Add(Vector3.zero);
                    }
                    else
                    {
                        dirs.Add(fwd);
                        lateralOffsets.Add(right * (k * eruptionRadius * 2f));
                    }
                }
                break;
            }
        }
        return dirs.Count;
    }

    private static readonly List<Vector3> tmpDirs = new List<Vector3>(8);
    private static readonly List<Vector3> tmpOffsets = new List<Vector3>(8);

    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        ActionFrame frame = cast.GetFrame(anchor);
        float len = Distance(cast.Stats);
        float w = eruptionRadius * 2f * cast.Stats.area;
        if (pattern == SurgePattern.Ring)
        {
            shapes.Add(ResolvedShape.Resolve(HitShape.CircleShape(len + eruptionRadius), frame.position, frame.rotation, 1f));
            return true;
        }
        GetLineDirections(frame.rotation, tmpDirs, tmpOffsets);
        for (int i = 0; i < tmpDirs.Count; i++)
        {
            var line = new HitShape { type = HitShapeType.Line, length = len, width = w, height = height, baseOffset = -1f };
            shapes.Add(ResolvedShape.Resolve(line, frame.position + tmpOffsets[i], Quaternion.LookRotation(tmpDirs[i]), 1f));
        }
        return true;
    }

    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) => Distance(s) / Mathf.Max(0.1f, speed) + 0.2f;
    public override TargetFilter HazardFilter => hit.filter;

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        float r = Distance(s) + eruptionRadius * s.area;
        return anchor == ActionAnchor.Caster ? r : def.Range(s) + r;
    }

    public override bool WouldHit(in CastPreview p)
    {
        ActionFrame f = p.FrameFor(anchor);
        float len = Distance(p.stats);
        float r = eruptionRadius * p.stats.area;
        if (pattern == SurgePattern.Ring)
        {
            float d = CombatQuery.FlatDistance(f.position, p.targetVolume.basePosition);
            return d <= len + r + p.targetVolume.radius;
        }
        if (pattern == SurgePattern.Seek)
            return CombatQuery.FlatDistance(f.position, p.targetVolume.basePosition) <= len;
        GetLineDirections(p.aimRotation, tmpDirs, tmpOffsets);
        for (int i = 0; i < tmpDirs.Count; i++)
        {
            var line = new HitShape { type = HitShapeType.Line, length = len, width = r * 2f, height = height, baseOffset = -1f };
            if (ResolvedShape.Resolve(line, f.position + tmpOffsets[i], Quaternion.LookRotation(tmpDirs[i]), 1f).Overlaps(p.targetVolume))
                return true;
        }
        return false;
    }

    public override float EstimateDamage(in AbilityStats s) => hit.EstimateDamage(s) * (hitOncePerTarget ? 1f : 2f);
    public override float EstimateControl(in AbilityStats s) => hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string shape = pattern == SurgePattern.Star ? $"{lines}-line star" : pattern.ToString().ToLowerInvariant();
        return $"Ground surge ({shape}, {Distance(s):0.#}m): {hit.Describe(s)}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(def, owner, errors, warnings);
        hit.Validate(owner, errors, warnings);
        if (step > eruptionRadius * 2.2f)
            warnings.Add($"{owner}: Step ({step}m) is larger than an eruption ({eruptionRadius * 2f}m wide); characters can stand in the gaps.");
        if (pattern == SurgePattern.Seek && def.targeting.mode == AbilityTargetingMode.Point)
            warnings.Add($"{owner}: Seek follows the target character; Point targeting may not have one.");
    }

    public override void Prewarm()
    {
        if (eruptionVfx != null) AbilityPool.Prewarm(eruptionVfx, Mathf.Clamp(Mathf.CeilToInt(distance / step), 2, 12));
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (GroundSurgeAction)base.Clone();
        c.hit = hit.Clone();
        return c;
    }

    /// <summary>Runs one surge (all its lines or its ring).</summary>
    private sealed class SurgeInstance : IAbilityRuntimeObject
    {
        private struct Head
        {
            public Vector3 position;
            public Vector3 direction;
            public float travelled;
            public float nextEruption;
            public bool stopped;
            public HazardArea hazard;
        }

        private readonly GroundSurgeAction action;
        private readonly AbilityCastInstance cast;
        private readonly Vector3 origin;
        private readonly Head[] heads;
        private readonly HashSet<CombatEntity> hitSet;
        private readonly float maxDistance;
        private readonly float radius;
        private float ringRadius;
        private float nextRing;
        private HazardArea ringHazard;
        private readonly HitShape eruption;

        public SurgeInstance(GroundSurgeAction action, AbilityCastInstance cast, ActionFrame frame)
        {
            this.action = action;
            this.cast = cast;
            origin = frame.position;
            maxDistance = action.Distance(cast.Stats);
            radius = action.eruptionRadius * cast.Stats.area;
            hitSet = action.hitOncePerTarget ? new HashSet<CombatEntity>() : null;
            eruption = new HitShape { type = HitShapeType.Circle, radius = radius, height = action.height, baseOffset = -1f };

            if (action.pattern == SurgePattern.Ring)
            {
                heads = Array.Empty<Head>();
                nextRing = Mathf.Min(action.step, maxDistance);
                return;
            }
            action.GetLineDirections(frame.rotation, tmpDirs, tmpOffsets);
            heads = new Head[tmpDirs.Count];
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = new Head
                {
                    position = origin + tmpOffsets[i],
                    direction = tmpDirs[i],
                    travelled = 0f,
                    nextEruption = Mathf.Min(radius, maxDistance),
                };
            }
        }

        public bool Tick(float dt)
        {
            float move = action.speed * dt;
            if (action.pattern == SurgePattern.Ring)
                return TickRing(move);

            bool anyActive = false;
            for (int i = 0; i < heads.Length; i++)
            {
                ref Head h = ref heads[i];
                if (h.stopped)
                    continue;

                if (action.pattern == SurgePattern.Seek && cast.Target != null && cast.Target.IsAlive)
                {
                    Vector3 want = CombatQuery.FlatDirection(h.position, cast.Target.Position, h.direction);
                    h.direction = Vector3.RotateTowards(h.direction, want, action.seekTurnRate * Mathf.Deg2Rad * dt, 0f);
                }

                float remaining = move;
                while (remaining > 0f && !h.stopped)
                {
                    float toNext = h.nextEruption - h.travelled;
                    if (toNext > remaining)
                    {
                        Advance(ref h, remaining);
                        remaining = 0f;
                    }
                    else
                    {
                        Advance(ref h, Mathf.Max(0f, toNext));
                        remaining -= Mathf.Max(0f, toNext);
                        if (h.stopped)
                            break;
                        Erupt(h.position, h.direction);
                        h.nextEruption += action.step;
                        if (h.travelled >= maxDistance - 1e-3f || h.nextEruption > maxDistance + 1e-3f)
                            h.stopped = true;
                    }
                }

                UpdateHeadHazard(ref h);
                if (!h.stopped)
                    anyActive = true;
            }
            return anyActive;
        }

        private void Advance(ref Head h, float d)
        {
            if (d <= 0f)
                return;
            if (action.stopAtObstacles && CombatQuery.RaycastObstacle(h.position + Vector3.up * 0.8f, h.direction, d, out _))
            {
                h.stopped = true;
                return;
            }
            Vector3 next = h.position + h.direction * d;
            if (CombatQuery.GroundHeight(next, out float y, 2f, 4f))
                next.y = y;
            h.position = next;
            h.travelled += d;
        }

        private void Erupt(Vector3 at, Vector3 dir)
        {
            ResolvedShape r = ResolvedShape.Resolve(eruption, at, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward), 1f);
            cast.HitArea(r, action.hit, dir, hitSet);
            if (action.eruptionVfx != null)
                AbilityPool.PlayVfx(action.eruptionVfx, at, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward), 0f, cast.Stats.area);
            if (action.eruptionSound != null)
                AbilityPool.PlaySound(action.eruptionSound, at, cast.Definition.presentation.volume * 0.7f);
        }

        private void UpdateHeadHazard(ref Head h)
        {
            if (h.stopped)
            {
                HazardRegistry.Remove(h.hazard);
                h.hazard = null;
                return;
            }
            float ahead = Mathf.Min(action.speed * 0.6f, maxDistance - h.travelled);
            Vector3 end = h.position + h.direction * Mathf.Max(0.1f, ahead);
            if (h.hazard == null || !h.hazard.alive)
                h.hazard = HazardRegistry.AddCapsule(cast.CasterEntity, action.hit.filter, h.position, end, radius, Time.time, Time.time + 0.7f, cast.Severity);
            else
            {
                h.hazard.capsuleStart = h.position;
                h.hazard.capsuleEnd = end;
                h.hazard.activeUntil = Time.time + 0.7f;
            }
        }

        private bool TickRing(float move)
        {
            ringRadius += move;
            while (ringRadius >= nextRing && nextRing <= maxDistance + 1e-3f)
            {
                var ring = new HitShape
                {
                    type = HitShapeType.Ring,
                    radius = nextRing + radius * 0.5f,
                    innerRadius = Mathf.Max(0f, nextRing - radius * 0.5f),
                    height = action.height,
                    baseOffset = -1f,
                };
                ResolvedShape r = ResolvedShape.Resolve(ring, origin, Quaternion.identity, 1f);
                cast.HitArea(r, action.hit, Vector3.zero, hitSet);
                if (action.eruptionVfx != null)
                {
                    int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * nextRing / Mathf.Max(0.5f, action.step * 1.5f)), 4, 24);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2f;
                        Vector3 p = CombatQuery.SnapToGround(origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * nextRing, 2f, 4f);
                        AbilityPool.PlayVfx(action.eruptionVfx, p, Quaternion.identity, 0f, cast.Stats.area);
                    }
                }
                if (action.eruptionSound != null)
                    AbilityPool.PlaySound(action.eruptionSound, origin, cast.Definition.presentation.volume * 0.7f);
                nextRing += action.step;
            }

            // Hazard: the band the wave will cross in the next half second.
            float outer = Mathf.Min(maxDistance, ringRadius + action.speed * 0.5f) + radius;
            var band = new HitShape { type = HitShapeType.Ring, radius = outer, innerRadius = Mathf.Max(0f, ringRadius - radius), height = action.height, baseOffset = -1f };
            ResolvedShape hz = ResolvedShape.Resolve(band, origin, Quaternion.identity, 1f);
            if (ringHazard == null || !ringHazard.alive)
                ringHazard = HazardRegistry.AddShape(cast.CasterEntity, action.hit.filter, hz, Time.time, Time.time + 0.6f, cast.Severity);
            else
            {
                ringHazard.shape = hz;
                ringHazard.activeUntil = Time.time + 0.6f;
            }
            return ringRadius < maxDistance + radius;
        }

        public void Dispose()
        {
            for (int i = 0; i < heads.Length; i++)
            {
                HazardRegistry.Remove(heads[i].hazard);
                heads[i].hazard = null;
            }
            HazardRegistry.Remove(ringHazard);
            ringHazard = null;
        }
    }
}

// ====================================================================================================== zone
/// <summary>A lingering area: poison pools, fire fields, healing circles, auras, slowing fields.</summary>
[Serializable, AbilityMenu("Ground/Zone", "A lingering area that affects everyone inside every tick: poison pools, burning ground, healing circles, auras (Follow Caster), slowing fields.", 1)]
public class ZoneAction : CastAction
{
    [Tooltip("Shape and size of the zone.")]
    public HitShape shape = HitShape.CircleShape(3.5f);
    [Tooltip("Seconds the zone lasts (multiplied by the Duration modifier).")]
    [Min(0.1f)] public float duration = 5f;
    [Tooltip("Seconds between ticks.")]
    [Min(0.05f)] public float tickInterval = 0.5f;
    [Tooltip("The zone moves with the caster (aura) instead of staying on the ground.")]
    public bool followCaster = false;
    [Tooltip("Seconds to grow from nothing to full size (0 = full size at once).")]
    [Min(0f)] public float growTime = 0f;
    [Tooltip("Each character is affected only once (on entering) instead of every tick.")]
    public bool oncePerTarget = false;
    [Tooltip("Mobs avoid walking into it (when it hurts them).")]
    public bool markAsHazard = true;
    [Tooltip("Stop the zone when the caster dies.")]
    public bool endWithCaster = false;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 4f, variance = 0f });

    [Header("Visuals")]
    [Tooltip("Looping effect placed on the zone (scaled with the Area modifier).")]
    public GameObject zoneVfx;
    [Tooltip("Draw the zone's outline on the ground while it lasts.")]
    public bool showOutline = true;

    public float Duration(in AbilityStats s) => duration * s.duration;

    public override void Execute(AbilityCastInstance cast)
    {
        AbilityRuntime.Add(new ZoneInstance(this, cast));
        cast.EmitNoise(cast.GetFrame(anchor).position, 0.6f);
    }

    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        ActionFrame f = cast.GetFrame(anchor);
        shapes.Add(ResolvedShape.Resolve(shape, f.position, f.rotation, cast.Stats.area));
        return true;
    }

    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) => Duration(s);
    public override TargetFilter HazardFilter => hit.filter;

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        float r = shape.Reach(s.area);
        return anchor == ActionAnchor.Caster ? r : def.Range(s) + r;
    }

    public override bool WouldHit(in CastPreview p) =>
        ResolvedShape.Resolve(shape, p.FrameFor(anchor).position, p.aimRotation, p.stats.area).Overlaps(p.targetVolume);

    public override int CountHits(in CastPreview p, List<CombatEntity> buffer) =>
        CountInShape(p, ResolvedShape.Resolve(shape, p.FrameFor(anchor).position, p.aimRotation, p.stats.area), hit.filter, buffer);

    public override float EstimateDamage(in AbilityStats s)
    {
        float ticks = oncePerTarget ? 1f : Mathf.Max(1f, Duration(s) / tickInterval * 0.4f); // targets rarely stay inside
        return hit.EstimateDamage(s) * ticks;
    }

    public override float EstimateControl(in AbilityStats s) => hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s) =>
        $"Zone {shape.Describe(s.area)} for {Duration(s):0.#}s, every {tickInterval:0.##}s: {hit.Describe(s)}";

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(def, owner, errors, warnings);
        shape.Validate(owner, errors);
        hit.Validate(owner, errors, warnings);
        if (tickInterval > duration)
            warnings.Add($"{owner}: Tick Interval is longer than the Duration; it will tick once.");
    }

    public override void Prewarm()
    {
        if (zoneVfx != null) AbilityPool.Prewarm(zoneVfx, 1);
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (ZoneAction)base.Clone();
        c.shape = shape.Clone();
        c.hit = hit.Clone();
        return c;
    }

    private sealed class ZoneInstance : IAbilityRuntimeObject
    {
        private readonly ZoneAction action;
        private readonly AbilityCastInstance cast;
        private readonly ActionFrame fixedFrame;
        private readonly float totalTime;
        private readonly HashSet<CombatEntity> hitSet;
        private float age;
        private float nextTick;
        private GameObject vfx;
        private AbilityTelegraph outline;
        private HazardArea hazard;

        public ZoneInstance(ZoneAction action, AbilityCastInstance cast)
        {
            this.action = action;
            this.cast = cast;
            fixedFrame = cast.GetFrame(action.anchor);
            totalTime = action.Duration(cast.Stats);
            hitSet = action.oncePerTarget ? new HashSet<CombatEntity>() : null;
            ResolvedShape r = Current(0f);
            if (action.zoneVfx != null)
            {
                Transform follow = action.followCaster && cast.CasterEntity != null ? cast.CasterEntity.transform : null;
                vfx = AbilityPool.Spawn(action.zoneVfx, r.origin, r.rotation, follow);
                if (vfx != null)
                {
                    vfx.transform.SetPositionAndRotation(r.origin, r.rotation);
                    vfx.transform.localScale = action.zoneVfx.transform.localScale * cast.Stats.area;
                }
            }
            if (action.showOutline)
            {
                Color c = cast.RelationTo(CombatEntity.NearestPlayer(r.origin, out _)) == CombatRelation.Enemy
                    ? CombatSettings.Instance.enemyTelegraphColor
                    : CombatSettings.Instance.allyTelegraphColor;
                if (cast.Definition.presentation.telegraphColor.a > 0.01f)
                    c = cast.Definition.presentation.telegraphColor;
                outline = AbilityTelegraph.Show(r, c);
                outline?.SetProgress(1f);
            }
            if (action.markAsHazard && action.hit.effects.Count > 0 && action.hit.EstimateDamage(cast.Stats) + action.hit.EstimateControl(cast.Stats) > 0f)
                hazard = HazardRegistry.AddShape(cast.CasterEntity, action.hit.filter, r, Time.time, Time.time + totalTime, Mathf.Min(1f, cast.Severity + 0.2f));
        }

        private ResolvedShape Current(float t)
        {
            ActionFrame f = action.followCaster && cast.CasterEntity != null ? cast.GetFrame(ActionAnchor.Caster) : fixedFrame;
            float grow = action.growTime > 0f ? Mathf.Clamp01(t / action.growTime) : 1f;
            return ResolvedShape.Resolve(action.shape, f.position, f.rotation, cast.Stats.area * Mathf.Max(0.05f, grow));
        }

        public bool Tick(float dt)
        {
            age += dt;
            if (action.endWithCaster && !cast.CasterAlive)
                return false;
            ResolvedShape r = Current(age);
            if (action.followCaster || action.growTime > 0f)
            {
                outline?.SetShape(r);
                if (hazard != null && hazard.alive)
                    hazard.shape = r;
            }
            nextTick -= dt;
            if (nextTick <= 0f)
            {
                nextTick += action.tickInterval;
                cast.HitArea(r, action.hit, cast.AimDirection, hitSet);
            }
            return age < totalTime;
        }

        public void Dispose()
        {
            if (vfx != null)
                AbilityPool.Release(vfx);
            vfx = null;
            outline?.Release();
            outline = null;
            HazardRegistry.Remove(hazard);
            hazard = null;
        }
    }
}

// ====================================================================================================== beam
/// <summary>A continuous ray from the caster: lasers, fire breath, life drain.</summary>
[Serializable, AbilityMenu("Ground/Beam", "A continuous ray from the caster (laser, fire breath, drain) hitting everything along it every tick. The caster channels it (stays in the Launching phase).", 2)]
public class BeamAction : CastAction
{
    [Tooltip("Length of the beam (metres, multiplied by the Range modifier).")]
    [Min(0.5f)] public float length = 12f;
    [Tooltip("Width of the beam (metres, multiplied by the Area modifier).")]
    [Min(0.1f)] public float width = 1f;
    [Tooltip("Seconds the beam lasts (multiplied by the Duration modifier). The caster channels during this time.")]
    [Min(0.05f)] public float duration = 2f;
    [Tooltip("Seconds between hits.")]
    [Min(0.05f)] public float tickInterval = 0.2f;
    [Tooltip("Degrees per second the beam turns to follow the target (0 = fixed direction). Low values can be outrun.")]
    [Min(0f)] public float turnRate = 40f;
    [Tooltip("Walls cut the beam short.")]
    public bool stopAtObstacles = true;
    [Tooltip("Stop the beam if the caster is stunned or silenced.")]
    public bool breakOnControl = true;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 3f, variance = 0f });

    [Header("Visuals")]
    [Tooltip("Prefab stretched along the beam (its Z axis is scaled to the length). Empty = a coloured line.")]
    public GameObject beamPrefab;
    public Color beamColor = new Color(1f, 0.55f, 0.2f, 0.9f);
    [Tooltip("Effect at the end of the beam.")]
    public GameObject endVfx;

    public float Length(in AbilityStats s) => length * s.range;
    public float Duration(in AbilityStats s) => duration * s.duration;

    public BeamAction()
    {
        anchor = ActionAnchor.Caster;
    }

    public override void Execute(AbilityCastInstance cast)
    {
        AbilityRuntime.Add(new BeamInstance(this, cast));
        cast.EmitNoise(cast.CasterPosition, 0.8f);
    }

    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        shapes.Add(ResolvedShape.Resolve(LineShape(cast.Stats, Length(cast.Stats)), cast.CasterPosition, cast.AimRotation, 1f));
        return true;
    }

    internal HitShape LineShape(in AbilityStats s, float len) =>
        new HitShape { type = HitShapeType.Line, length = len, width = width * s.area, height = 3f, baseOffset = -1f };

    public override float LaunchDuration(AbilityDefinition def, in AbilityStats s) => Duration(s);
    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) => Duration(s);
    public override TargetFilter HazardFilter => hit.filter;
    public override float Reach(AbilityDefinition def, in AbilityStats s) => Length(s);

    public override bool WouldHit(in CastPreview p) =>
        ResolvedShape.Resolve(LineShape(p.stats, Length(p.stats)), p.casterPosition, p.aimRotation, 1f).Overlaps(p.targetVolume);

    public override int CountHits(in CastPreview p, List<CombatEntity> buffer) =>
        CountInShape(p, ResolvedShape.Resolve(LineShape(p.stats, Length(p.stats)), p.casterPosition, p.aimRotation, 1f), hit.filter, buffer);

    public override float EstimateDamage(in AbilityStats s) => hit.EstimateDamage(s) * Mathf.Max(1f, Duration(s) / tickInterval * 0.5f);
    public override float EstimateControl(in AbilityStats s) => hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s) =>
        $"Beam {Length(s):0.#}m for {Duration(s):0.#}s, every {tickInterval:0.##}s: {hit.Describe(s)}";

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        hit.Validate(owner, errors, warnings);
        if (anchor != ActionAnchor.Caster)
            warnings.Add($"{owner}: beams always start at the caster; Anchor is ignored.");
        if (def.movementWhileCasting == CasterMovementRule.Free)
            warnings.Add($"{owner}: the caster can move freely while channelling the beam.");
    }

    public override void Prewarm()
    {
        if (beamPrefab != null) AbilityPool.Prewarm(beamPrefab, 1);
        if (endVfx != null) AbilityPool.Prewarm(endVfx, 1);
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (BeamAction)base.Clone();
        c.hit = hit.Clone();
        return c;
    }

    private sealed class BeamInstance : IAbilityRuntimeObject
    {
        private readonly BeamAction action;
        private readonly AbilityCastInstance cast;
        private readonly float total;
        private Vector3 direction;
        private float age;
        private float nextTick;
        private GameObject visual;
        private LineRenderer line;
        private GameObject end;
        private HazardArea hazard;

        public BeamInstance(BeamAction action, AbilityCastInstance cast)
        {
            this.action = action;
            this.cast = cast;
            total = action.Duration(cast.Stats);
            direction = cast.AimDirection;
            if (action.beamPrefab != null)
            {
                visual = AbilityPool.Spawn(action.beamPrefab, cast.CastPoint, Quaternion.LookRotation(direction));
            }
            else
            {
                visual = new GameObject("Beam");
                line = visual.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = CombatSettings.Instance.TelegraphMaterial;
                line.widthMultiplier = Mathf.Max(0.05f, action.width * cast.Stats.area * 0.6f);
                line.startColor = action.beamColor;
                Color endColor = action.beamColor;
                endColor.a *= 0.6f;
                line.endColor = endColor;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (action.endVfx != null)
                end = AbilityPool.Spawn(action.endVfx, cast.CastPoint, Quaternion.identity);
        }

        public bool Tick(float dt)
        {
            age += dt;
            if (cast.Interrupted || !cast.CasterAlive)
                return false;
            if (action.breakOnControl && cast.CasterEntity != null && (cast.CasterEntity.IsStunned || cast.CasterEntity.IsSilenced))
                return false;

            if (action.turnRate > 0f && cast.Target != null && cast.Target.IsAlive)
            {
                Vector3 want = CombatQuery.FlatDirection(cast.CasterPosition, cast.Target.Position, direction);
                direction = Vector3.RotateTowards(direction, want, action.turnRate * Mathf.Deg2Rad * dt, 0f);
                cast.AimDirection = direction; // the caster keeps facing the beam
            }

            Vector3 from = cast.CastPoint;
            Vector3 aim3 = direction;
            if (cast.Target != null && cast.Target.IsAlive)
            {
                // Tilt toward the target's height so the visual meets it.
                Vector3 to = cast.Target.AimPosition - from;
                float flat = new Vector2(to.x, to.z).magnitude;
                if (flat > 0.5f)
                    aim3 = (direction * flat + Vector3.up * Mathf.Clamp(to.y, -flat, flat)).normalized;
            }
            float len = action.Length(cast.Stats);
            if (action.stopAtObstacles && CombatQuery.RaycastObstacle(from, aim3, len, out RaycastHit wall))
                len = wall.distance;
            Vector3 endPoint = from + aim3 * len;

            if (line != null)
            {
                line.SetPosition(0, from);
                line.SetPosition(1, endPoint);
            }
            else if (visual != null)
            {
                visual.transform.SetPositionAndRotation(from, Quaternion.LookRotation(aim3));
                Vector3 sc = action.beamPrefab.transform.localScale;
                visual.transform.localScale = new Vector3(sc.x * cast.Stats.area, sc.y * cast.Stats.area, sc.z * len);
            }
            if (end != null)
                end.transform.position = endPoint;

            float flatLen = new Vector2(aim3.x, aim3.z).magnitude * len;
            ResolvedShape r = ResolvedShape.Resolve(action.LineShape(cast.Stats, Mathf.Max(0.3f, flatLen)), cast.CasterPosition, Quaternion.LookRotation(direction), 1f);
            if (hazard == null || !hazard.alive)
                hazard = HazardRegistry.AddShape(cast.CasterEntity, action.hit.filter, r, Time.time, Time.time + (total - age) + 0.1f, cast.Severity);
            else
                hazard.shape = r;

            nextTick -= dt;
            if (nextTick <= 0f)
            {
                nextTick += action.tickInterval;
                cast.HitArea(r, action.hit, direction);
            }
            return age < total;
        }

        public void Dispose()
        {
            if (line != null)
            {
                UnityEngine.Object.Destroy(visual);
            }
            else if (visual != null)
            {
                AbilityPool.Release(visual);
            }
            visual = null;
            line = null;
            if (end != null)
                AbilityPool.Release(end);
            end = null;
            HazardRegistry.Remove(hazard);
            hazard = null;
        }
    }
}
