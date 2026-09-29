using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Fires projectiles: bullets, arrows, fireballs, spreads, rings, homing missiles, lobbed bombs and rains.</summary>
[Serializable, AbilityMenu("Projectile/Projectile", "Fires projectiles: bullets, arrows, fireballs, spreads, rings, homing missiles, lobbed bombs and rain from the sky.", 0)]
public class ProjectileAction : CastAction, ITravellingAction
{
    [Header("Projectile")]
    [Tooltip("Visual prefab (mesh/particles/trail). Its forward axis points along the flight. Empty = Combat Settings' default projectile, or a small glowing sphere.")]
    public GameObject prefab;
    [Tooltip("Speed in metres per second (multiplied by the Projectile Speed modifier).")]
    [Min(0.1f)] public float speed = 22f;
    [Tooltip("Maximum travel distance in metres (multiplied by the Range modifier).")]
    [Min(0.5f)] public float maxDistance = 25f;
    [Tooltip("Collision radius in metres (how thick the projectile is).")]
    [Min(0.01f)] public float radius = 0.25f;
    [Tooltip("Spawn offset from the cast point (x right, y up, z forward).")]
    public Vector3 spawnOffset = Vector3.zero;
    [Tooltip("Aim at the target's chest height instead of flying level (needed for targets above or below).")]
    public bool aimAtTargetHeight = true;

    [Header("Pattern")]
    [Tooltip("Projectiles per volley (the Projectile Count modifiers change this: e.g. an absorbed copy fires 1 instead of 3).")]
    [Min(1)] public int count = 1;
    [Tooltip("Spread: fanned evenly over Spread Angle. Ring: all around. Parallel: side by side. Random: random inside Spread Angle. Rain: falling around the aim point.")]
    public ProjectilePattern pattern = ProjectilePattern.Spread;
    [Tooltip("Spread / Random: total angle covered (degrees).")]
    [Range(0f, 360f)] public float spreadAngle = 30f;
    [Tooltip("Parallel: metres between projectiles.")]
    [Min(0f)] public float spacing = 0.6f;
    [Tooltip("Number of volleys.")]
    [Min(1)] public int volleys = 1;
    [Tooltip("Seconds between volleys.")]
    [Min(0.02f)] public float volleyInterval = 0.25f;
    [Tooltip("Later volleys aim at the target's new position (machine gun). Off = all volleys use the first aim.")]
    public bool reaimEachVolley = true;

    [Header("Flight")]
    [Tooltip("Degrees per second the projectile turns toward its target (homing). 0 = flies straight.")]
    [Min(0f)] public float homingTurnRate = 0f;
    [Tooltip("Seconds of straight flight before homing starts.")]
    [Min(0f)] public float homingDelay = 0.15f;
    [Tooltip("Downward acceleration (m/s²) for straight shots. 0 = no drop.")]
    [Min(0f)] public float gravity = 0f;
    [Tooltip("Lob: fly in an arc that lands on the aimed point (grenades, boulders, spit).")]
    public bool lob = false;
    [Tooltip("Lob: height of the arc above the higher end (metres).")]
    [Min(0.1f)] public float arcHeight = 3f;
    [Tooltip("Rain: height the projectiles fall from.")]
    [Min(1f)] public float rainHeight = 12f;
    [Tooltip("Rain: radius around the aim point where they land.")]
    [Min(0f)] public float rainRadius = 3f;

    [Header("Collision")]
    [Tooltip("Characters it passes through before stopping (0 = stops at the first).")]
    [Min(0)] public int pierce = 0;
    [Tooltip("Strength kept after each character passed (0.7 = 30% weaker each time).")]
    [Range(0f, 1f)] public float pierceFalloff = 1f;
    [Tooltip("Times it bounces off walls before stopping.")]
    [Min(0)] public int bounces = 0;
    [Tooltip("Radius of the explosion when it stops (0 = only the character hit is affected).")]
    [Min(0f)] public float explosionRadius = 0f;
    [Tooltip("Also explode when reaching max distance or hitting a wall (otherwise it just vanishes).")]
    public bool explodeAtEnd = false;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect());

    [Header("Feedback")]
    public GameObject impactVfx;
    public AudioClip launchSound;
    public AudioClip impactSound;

    // ------------------------------------------------------------------ helpers
    public float Speed(in AbilityStats s) => speed * s.projectileSpeed;
    public float Distance(in AbilityStats s) => maxDistance * s.range;
    public float TravelTime(in AbilityStats s, float distance) =>
        pattern == ProjectilePattern.Rain ? rainHeight / Mathf.Max(0.1f, Speed(s)) : distance / Mathf.Max(0.1f, Speed(s));

    private bool FallsOnPoint => pattern == ProjectilePattern.Rain || lob;

    // ------------------------------------------------------------------ execution
    public override void Execute(AbilityCastInstance cast)
    {
        int v = cast.Stats.VolleyCount(volleys);
        FireVolley(cast, 0);
        for (int i = 1; i < v; i++)
        {
            int volley = i;
            AbilityRuntime.Schedule(volleyInterval * i, () => { if (!cast.Interrupted) FireVolley(cast, volley); }, cast);
        }
    }

    private void FireVolley(AbilityCastInstance cast, int volleyIndex)
    {
        AbilityStats s = cast.Stats;
        int n = s.ProjectileCount(count);
        float spd = Speed(s);

        // Aim.
        Vector3 flatDir = cast.AimDirection;
        Vector3 aimPoint = cast.AimPoint;
        CombatEntity target = cast.Target != null && cast.Target.IsAlive ? cast.Target : null;
        if (volleyIndex > 0 && reaimEachVolley && target != null && cast.CasterAlive)
        {
            float t = CombatQuery.FlatDistance(cast.CasterPosition, target.Position) / Mathf.Max(0.1f, spd);
            Vector3 predicted = Vector3.Lerp(target.Position, CombatQuery.Predict(target, t), cast.Definition.targeting.leadTarget);
            flatDir = CombatQuery.FlatDirection(cast.CasterPosition, predicted, flatDir);
            aimPoint = predicted;
        }
        Quaternion yaw = Quaternion.LookRotation(flatDir, Vector3.up);

        Vector3 origin;
        if (anchor == ActionAnchor.Caster)
            origin = cast.CastPoint + yaw * spawnOffset;
        else
            origin = cast.GetFrame(anchor).position + Vector3.up * 1f + yaw * spawnOffset;

        // Pitch toward the target's body (or, without a target, toward what the aim ray hit).
        float pitchSin = 0f;
        if (aimAtTargetHeight && !FallsOnPoint && (target != null || cast.HasAimTargetPoint))
        {
            Vector3 to = (target != null ? target.AimPosition : cast.AimTargetPoint) - origin;
            float flat = new Vector2(to.x, to.z).magnitude;
            if (flat > 0.1f)
                pitchSin = Mathf.Clamp(to.y / Mathf.Sqrt(flat * flat + to.y * to.y), -0.8f, 0.8f);
        }

        if (launchSound != null)
            AbilityPool.PlaySound(launchSound, origin, cast.Definition.presentation.volume);

        for (int i = 0; i < n; i++)
        {
            Vector3 start = origin;
            Vector3 dir;
            float g = gravity;
            Vector3 velocity;

            switch (pattern)
            {
                case ProjectilePattern.Rain:
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * rainRadius * s.area;
                    Vector3 land = CombatQuery.SnapToGround(aimPoint + new Vector3(r.x, 0f, r.y));
                    start = land + Vector3.up * rainHeight + flatDir * (-rainHeight * 0.15f);
                    dir = (land - start).normalized;
                    velocity = dir * spd;
                    g = 0f;
                    break;
                }
                case ProjectilePattern.Ring:
                {
                    float a = 360f / n * i;
                    dir = Quaternion.AngleAxis(a, Vector3.up) * flatDir;
                    velocity = dir * spd;
                    break;
                }
                case ProjectilePattern.Parallel:
                {
                    float lateral = (i - (n - 1) * 0.5f) * spacing;
                    start = origin + yaw * new Vector3(lateral, 0f, 0f);
                    dir = Pitched(flatDir, pitchSin);
                    velocity = dir * spd;
                    break;
                }
                case ProjectilePattern.Random:
                {
                    float a = UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
                    dir = Pitched(Quaternion.AngleAxis(a, Vector3.up) * flatDir, pitchSin + UnityEngine.Random.Range(-0.05f, 0.05f));
                    velocity = dir * spd;
                    break;
                }
                default:
                {
                    float a = n > 1 ? Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(n - 1)) : 0f;
                    dir = Pitched(Quaternion.AngleAxis(a, Vector3.up) * flatDir, pitchSin);
                    velocity = dir * spd;
                    break;
                }
            }

            float distance = Distance(s);
            if (lob && pattern != ProjectilePattern.Rain)
            {
                float a = n > 1 && pattern != ProjectilePattern.Parallel ? Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(n - 1)) : 0f;
                Vector3 offsetFromCaster = aimPoint - cast.CasterPosition;
                offsetFromCaster.y = 0f;
                Vector3 land = cast.CasterPosition + Quaternion.AngleAxis(a, Vector3.up) * offsetFromCaster;
                if (pattern == ProjectilePattern.Parallel)
                    land += yaw * new Vector3((i - (n - 1) * 0.5f) * spacing, 0f, 0f);
                land = CombatQuery.SnapToGround(land);
                velocity = LobVelocity(start, land, spd, arcHeight, out g);
                distance = CombatQuery.FlatDistance(start, land) * 2f + arcHeight * 2f + 5f;
            }

            ProjectileInstance.Launch(this, cast, start, velocity, g, distance, target);
        }
        cast.EmitNoise(origin, 0.7f);
    }

    private static Vector3 Pitched(Vector3 flat, float pitchSin)
    {
        if (Mathf.Abs(pitchSin) < 1e-4f)
            return flat;
        float cos = Mathf.Sqrt(1f - pitchSin * pitchSin);
        return new Vector3(flat.x * cos, pitchSin, flat.z * cos).normalized;
    }

    /// <summary>Initial velocity for an arc from <paramref name="from"/> landing on <paramref name="to"/>.</summary>
    public static Vector3 LobVelocity(Vector3 from, Vector3 to, float horizontalSpeed, float apexHeight, out float gravityOut)
    {
        Vector3 flat = to - from;
        flat.y = 0f;
        float d = flat.magnitude;
        float T = Mathf.Max(0.2f, d / Mathf.Max(0.1f, horizontalSpeed));
        float apex = Mathf.Max(from.y, to.y) + Mathf.Max(0.1f, apexHeight);
        float h = apex - from.y;
        float delta = to.y - from.y;
        float root = Mathf.Sqrt(Mathf.Max(0f, 1f - delta / h));
        float vy = 2f * h / T * (1f + root);
        gravityOut = vy * vy / (2f * h);
        Vector3 horizontal = d > 1e-4f ? flat / d * (d / T) : Vector3.zero;
        return horizontal + Vector3.up * vy;
    }

    // ------------------------------------------------------------------ impact
    internal void Impact(AbilityCastInstance cast, Vector3 position, Vector3 direction, CombatEntity directHit, HashSet<CombatEntity> alreadyHit, float multiplier, bool atEnd)
    {
        bool explode = explosionRadius > 0f && (directHit != null || explodeAtEnd || !atEnd);
        if (explode)
        {
            var shape = new HitShape { type = HitShapeType.Sphere, radius = explosionRadius };
            ResolvedShape r = ResolvedShape.Resolve(shape, position, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward), cast.Stats.area);
            if (directHit != null && !alreadyHit.Contains(directHit))
            {
                cast.ApplyHit(hit, directHit, position, direction, multiplier);
                alreadyHit.Add(directHit);
            }
            HitSettings blast = hit;
            cast.HitArea(r, blast, direction, alreadyHit);
        }
        else if (directHit != null)
        {
            cast.ApplyHit(hit, directHit, position, direction, multiplier);
        }

        if (impactVfx != null && (directHit != null || explode || !atEnd))
            AbilityPool.PlayVfx(impactVfx, position, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? -direction : Vector3.up), 0f, explode ? cast.Stats.area : 1f);
        if (impactSound != null && (directHit != null || explode))
            AbilityPool.PlaySound(impactSound, position, cast.Definition.presentation.volume);
        if (explode)
            cast.EmitNoise(position, 0.8f);
    }

    // ------------------------------------------------------------------ AI / telegraph
    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        AbilityStats s = cast.Stats;
        if (FallsOnPoint)
        {
            float r = (pattern == ProjectilePattern.Rain ? rainRadius * s.area : 0f) + Mathf.Max(explosionRadius * s.area, radius + 0.5f);
            shapes.Add(ResolvedShape.Resolve(HitShape.CircleShape(r), cast.AimPoint, cast.AimRotation, 1f));
            return true;
        }
        float len = Distance(s);
        int n = s.ProjectileCount(count);
        Vector3 origin = anchor == ActionAnchor.Caster ? cast.CasterPosition : cast.GetFrame(anchor).position;
        if (pattern == ProjectilePattern.Ring)
        {
            shapes.Add(ResolvedShape.Resolve(HitShape.CircleShape(len), origin, cast.AimRotation, 1f));
            return true;
        }
        if (n > 1 && (pattern == ProjectilePattern.Spread || pattern == ProjectilePattern.Random) && spreadAngle > 2f)
        {
            shapes.Add(ResolvedShape.Resolve(HitShape.ConeShape(len, spreadAngle + 4f), origin, cast.AimRotation, 1f));
            return true;
        }
        float width = radius * 2f + (pattern == ProjectilePattern.Parallel ? (n - 1) * spacing : 0f) + 0.4f;
        shapes.Add(ResolvedShape.Resolve(HitShape.LineShape(len, width), origin, cast.AimRotation, 1f));
        return true;
    }

    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) =>
        FallsOnPoint ? TravelTime(s, def.Range(s)) + 0.3f : Distance(s) / Mathf.Max(0.1f, Speed(s)) + (s.VolleyCount(volleys) - 1) * volleyInterval;

    public override TargetFilter HazardFilter => hit.filter;

    public override float LaunchDuration(AbilityDefinition def, in AbilityStats s) => (s.VolleyCount(volleys) - 1) * volleyInterval;

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        if (FallsOnPoint)
            return def.Range(s) + (rainRadius + explosionRadius) * s.area;
        return Distance(s) + explosionRadius * s.area;
    }

    public override bool WouldHit(in CastPreview p)
    {
        float dist = CombatQuery.FlatDistance(p.casterPosition, p.targetVolume.basePosition);
        if (FallsOnPoint)
            return dist <= p.definition.Range(p.stats) + p.targetVolume.radius;
        return dist <= Distance(p.stats) + p.targetVolume.radius;
    }

    public override int CountHits(in CastPreview p, List<CombatEntity> buffer)
    {
        if (!WouldHit(p))
            return 0;
        if (explosionRadius <= 0f && pattern != ProjectilePattern.Rain)
            return 1;
        float r = explosionRadius * p.stats.area + (pattern == ProjectilePattern.Rain ? rainRadius * p.stats.area : 0f);
        ResolvedShape shape = ResolvedShape.Resolve(HitShape.CircleShape(Mathf.Max(0.5f, r)), p.aimPoint, p.aimRotation, 1f);
        return Mathf.Max(1, CountInShape(p, shape, hit.filter, buffer));
    }

    public override float EstimateDamage(in AbilityStats s)
    {
        int n = s.ProjectileCount(count);
        float perVolley;
        switch (pattern)
        {
            case ProjectilePattern.Ring: perVolley = 1f; break;
            case ProjectilePattern.Parallel: perVolley = Mathf.Min(n, 2f); break;
            case ProjectilePattern.Rain: perVolley = Mathf.Max(1f, n * 0.35f); break;
            default: perVolley = n == 1 ? 1f : Mathf.Min(n, 1f + n * (spreadAngle <= 15f ? 0.8f : 0.3f)); break;
        }
        return hit.EstimateDamage(s) * perVolley * s.VolleyCount(volleys);
    }

    public override float EstimateControl(in AbilityStats s) => hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        int n = s.ProjectileCount(count);
        int v = s.VolleyCount(volleys);
        string what = n == 1 ? "1 projectile" : $"{n} projectiles ({pattern})";
        if (v > 1) what += $" x{v} volleys";
        if (homingTurnRate > 0f) what += ", homing";
        if (lob) what += ", lobbed";
        int p = s.Pierce(pierce);
        if (p > 0) what += $", pierces {p}";
        if (explosionRadius > 0f) what += $", explodes r{explosionRadius * s.area:0.#}m";
        return $"{what}: {hit.Describe(s)}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        hit.Validate(owner, errors, warnings);
        if (speed <= 0f)
            errors.Add($"{owner}: Speed must be above 0.");
        if (pattern == ProjectilePattern.Rain && def.targeting.mode == AbilityTargetingMode.Self)
            warnings.Add($"{owner}: Rain falls around the aim point; with Self targeting that is in front of the caster.");
        if (lob && def.targeting.mode == AbilityTargetingMode.Direction)
            warnings.Add($"{owner}: Lob lands on the aimed point; Point or Unit targeting gives better control.");
        if (homingTurnRate > 0f && def.targeting.mode == AbilityTargetingMode.Point)
            warnings.Add($"{owner}: homing needs a target character; Point targeting may not have one.");
        if (explosionRadius <= 0f && explodeAtEnd)
            warnings.Add($"{owner}: Explode At End needs an Explosion Radius above 0.");
        if (radius > 2f)
            warnings.Add($"{owner}: collision radius {radius}m is very large.");
    }

    public override void Prewarm()
    {
        GameObject p = ProjectileInstance.ResolvePrefab(prefab);
        if (p != null) AbilityPool.Prewarm(p, Mathf.Clamp(count * volleys, 1, 12));
        if (impactVfx != null) AbilityPool.Prewarm(impactVfx, 2);
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (ProjectileAction)base.Clone();
        c.hit = hit.Clone();
        return c;
    }
}

/// <summary>One projectile in flight (pooled, updated by <see cref="AbilityRuntime"/>).</summary>
public sealed class ProjectileInstance : IAbilityRuntimeObject
{
    private static readonly Stack<ProjectileInstance> pool = new Stack<ProjectileInstance>(64);
    private static readonly Stack<HashSet<CombatEntity>> setPool = new Stack<HashSet<CombatEntity>>(32);
    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private static readonly Collider[] overlaps = new Collider[16];
    private static GameObject fallbackPrefab;

    private ProjectileAction action;
    private AbilityCastInstance cast;
    private Vector3 position;
    private Vector3 velocity;
    private float gravity;
    private float travelled;
    private float maxDistance;
    private float radius;
    private int pierceLeft;
    private int bouncesLeft;
    private float multiplier;
    private float age;
    private CombatEntity homingTarget;
    private HashSet<CombatEntity> alreadyHit;
    private GameObject visual;
    private HazardArea hazard;
    private bool firstTick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool.Clear();
        setPool.Clear();
        fallbackPrefab = null;
    }

    /// <summary>The prefab used when an action has none (Combat Settings' default, or a generated glowing sphere).</summary>
    public static GameObject ResolvePrefab(GameObject prefab)
    {
        if (prefab != null)
            return prefab;
        CombatSettings settings = CombatSettings.Instance;
        if (settings.defaultProjectilePrefab != null)
            return settings.defaultProjectilePrefab;
        if (fallbackPrefab == null && Application.isPlaying)
        {
            fallbackPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackPrefab.name = "Default Projectile";
            UnityEngine.Object.Destroy(fallbackPrefab.GetComponent<Collider>());
            fallbackPrefab.transform.localScale = Vector3.one * 0.35f;
            var mr = fallbackPrefab.GetComponent<MeshRenderer>();
            mr.sharedMaterial = settings.TelegraphMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = fallbackPrefab.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.widthMultiplier = 0.2f;
            trail.sharedMaterial = settings.TelegraphMaterial;
            trail.startColor = new Color(1f, 0.8f, 0.4f, 0.9f);
            trail.endColor = new Color(1f, 0.5f, 0.2f, 0f);
            fallbackPrefab.SetActive(false);
            AbilityRuntime rt = AbilityRuntime.Instance;
            if (rt != null)
                fallbackPrefab.transform.SetParent(rt.transform, false);
        }
        return fallbackPrefab;
    }

    internal static void Launch(ProjectileAction action, AbilityCastInstance cast, Vector3 start, Vector3 velocity, float gravity, float maxDistance, CombatEntity target)
    {
        ProjectileInstance p = pool.Count > 0 ? pool.Pop() : new ProjectileInstance();
        p.action = action;
        p.cast = cast;
        p.position = start;
        p.velocity = velocity;
        p.gravity = gravity;
        p.travelled = 0f;
        p.maxDistance = maxDistance;
        p.radius = action.radius;
        p.pierceLeft = cast.Stats.Pierce(action.pierce);
        p.bouncesLeft = action.bounces;
        p.multiplier = 1f;
        p.age = 0f;
        p.firstTick = true;
        p.homingTarget = action.homingTurnRate > 0f ? target : null;
        p.alreadyHit = setPool.Count > 0 ? setPool.Pop() : new HashSet<CombatEntity>();
        if (cast.CasterEntity != null)
            p.alreadyHit.Add(cast.CasterEntity); // never hits its own caster

        GameObject prefab = ResolvePrefab(action.prefab);
        Quaternion rot = Quaternion.LookRotation(velocity.sqrMagnitude > 1e-6f ? velocity : Vector3.forward);
        p.visual = prefab != null ? AbilityPool.Spawn(prefab, start, rot) : null;
        if (p.visual != null)
        {
            if (!p.visual.activeSelf)
                p.visual.SetActive(true);
            // Clear old trail segments of pooled visuals.
            TrailRenderer tr = p.visual.GetComponentInChildren<TrailRenderer>();
            if (tr != null) tr.Clear();
        }
        p.hazard = HazardRegistry.AddCapsule(cast.CasterEntity, action.hit.filter, start, start, p.radius + 0.4f, Time.time, Time.time + 0.5f, cast.Severity);
        AbilityRuntime.Add(p);
    }

    public bool Tick(float dt)
    {
        age += dt;
        CombatSettings settings = CombatSettings.Instance;

        if (firstTick)
        {
            firstTick = false;
            // Point-blank: something already overlapping the spawn point.
            int n0 = Physics.OverlapSphereNonAlloc(position, radius, overlaps, settings.characterLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n0; i++)
            {
                CombatEntity e = CombatEntity.Resolve(overlaps[i]);
                if (e != null && TryHitCharacter(e, position))
                    return false;
            }
        }

        // Homing.
        if (homingTarget != null && age >= action.homingDelay)
        {
            if (!homingTarget.IsAlive)
            {
                homingTarget = null;
            }
            else
            {
                Vector3 desired = (homingTarget.AimPosition - position).normalized * velocity.magnitude;
                velocity = Vector3.RotateTowards(velocity, desired, action.homingTurnRate * Mathf.Deg2Rad * dt, 0f);
            }
        }
        if (gravity > 0f)
            velocity += Vector3.down * (gravity * dt);

        Vector3 step = velocity * dt;
        float stepLen = step.magnitude;
        if (stepLen < 1e-5f)
            return age < 10f;
        Vector3 dir = step / stepLen;

        // Cast from slightly behind so things touching the current position are not missed.
        Vector3 castFrom = position - dir * radius;
        int mask = settings.characterLayers | settings.obstacleLayers;
        int n = Physics.SphereCastNonAlloc(castFrom, radius, dir, hits, stepLen + radius, mask, QueryTriggerInteraction.Collide);
        SortHits(n);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hits[i];
            Collider col = h.collider;
            if (col == null)
                continue;
            Vector3 point = h.distance <= 0f ? position : h.point;
            CombatEntity e = CombatEntity.Resolve(col);
            if (e != null)
            {
                if (TryHitCharacter(e, point))
                    return false;
                continue;
            }
            if (col.isTrigger)
                continue;
            if ((settings.obstacleLayers.value & (1 << col.gameObject.layer)) == 0)
                continue;

            // Wall / ground.
            if (bouncesLeft > 0 && h.distance > 0f)
            {
                bouncesLeft--;
                position = castFrom + dir * Mathf.Max(0f, h.distance) + h.normal * 0.02f;
                velocity = Vector3.Reflect(velocity, h.normal);
                UpdateVisual();
                return true;
            }
            action.Impact(cast, point, dir, null, alreadyHit, multiplier, true);
            return false;
        }

        position += step;
        travelled += stepLen;
        UpdateVisual();
        UpdateHazard();

        if (travelled >= maxDistance)
        {
            if (action.explodeAtEnd)
                action.Impact(cast, position, dir, null, alreadyHit, multiplier, true);
            return false;
        }
        return age < 30f;
    }

    /// <summary>Handles touching a character. Returns true if the projectile stops.</summary>
    private bool TryHitCharacter(CombatEntity e, Vector3 point)
    {
        if (!e.IsAlive || alreadyHit.Contains(e))
            return false;
        if (!cast.Passes(action.hit.filter, e))
            return false; // passes through allies / neutrals
        alreadyHit.Add(e);
        Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
        if (pierceLeft > 0)
        {
            cast.ApplyHit(action.hit, e, point, dir, multiplier);
            pierceLeft--;
            multiplier *= action.pierceFalloff;
            return false;
        }
        action.Impact(cast, point, dir, e, alreadyHit, multiplier, false);
        return true;
    }

    private static void SortHits(int n)
    {
        for (int i = 1; i < n; i++)
        {
            RaycastHit h = hits[i];
            int j = i - 1;
            while (j >= 0 && hits[j].distance > h.distance)
            {
                hits[j + 1] = hits[j];
                j--;
            }
            hits[j + 1] = h;
        }
    }

    private void UpdateVisual()
    {
        if (visual == null)
            return;
        visual.transform.position = position;
        if (velocity.sqrMagnitude > 1e-6f)
            visual.transform.rotation = Quaternion.LookRotation(velocity);
    }

    private void UpdateHazard()
    {
        if (hazard == null || !hazard.alive)
            return;
        hazard.capsuleStart = position;
        hazard.capsuleEnd = position + velocity * 0.5f;
        hazard.activeFrom = Time.time;
        hazard.activeUntil = Time.time + 0.5f;
    }

    public void Dispose()
    {
        if (visual != null)
        {
            GameObject v = visual;
            visual = null;
            // Let trails fade before returning to the pool.
            TrailRenderer tr = v.GetComponentInChildren<TrailRenderer>();
            AbilityPool.ReleaseAfter(v, tr != null ? tr.time : 0f);
        }
        HazardRegistry.Remove(hazard);
        hazard = null;
        if (alreadyHit != null)
        {
            alreadyHit.Clear();
            setPool.Push(alreadyHit);
            alreadyHit = null;
        }
        action = null;
        cast = null;
        homingTarget = null;
        pool.Push(this);
    }
}
