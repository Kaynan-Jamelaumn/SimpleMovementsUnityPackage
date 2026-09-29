using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Raises solid segments: straight walls, curved walls, or a cage that traps whoever is inside.</summary>
[Serializable, AbilityMenu("Ground/Barrier (Wall, Arc, Cage)", "Raises solid blocks that stop movement and projectiles: a straight wall, a curved wall, or a closed cage that traps the target (anchor it on the Target Unit).", 3)]
public class BarrierAction : CastAction
{
    [Tooltip("Wall: straight line across the aim. Arc: part of a circle around the anchor. Cage: closed circle around the anchor.")]
    public BarrierLayout layout = BarrierLayout.Wall;
    [Tooltip("Wall: total length in metres (multiplied by the Area modifier).")]
    [Min(0.5f)] public float length = 6f;
    [Tooltip("Arc / Cage: radius of the circle in metres (multiplied by the Area modifier).")]
    [Min(0.5f)] public float radius = 3f;
    [Tooltip("Arc: angle covered in degrees, centred on the aim direction.")]
    [Range(10f, 360f)] public float arcAngle = 120f;
    [Tooltip("Wall: distance in front of the anchor where the wall stands (metres).")]
    public float forwardOffset = 2f;
    [Tooltip("Height of the blocks (metres).")]
    [Min(0.2f)] public float height = 2.5f;
    [Tooltip("Thickness of the blocks (metres).")]
    [Min(0.1f)] public float thickness = 0.6f;
    [Tooltip("Width of one block (metres). Smaller = smoother curves, more objects.")]
    [Min(0.2f)] public float segmentWidth = 1f;
    [Tooltip("Seconds the barrier stands (multiplied by the Duration modifier).")]
    [Min(0.1f)] public float duration = 5f;
    [Tooltip("Seconds the blocks take to rise out of (and sink back into) the ground.")]
    [Min(0f)] public float riseTime = 0.25f;

    [Header("Blocks")]
    [Tooltip("Prefab for one block, modelled as a 1x1x1 m cube with the pivot at its centre (it is scaled to Segment Width x Height x Thickness). Empty = Combat Settings' default or a plain grey block.")]
    public GameObject segmentPrefab;
    [Tooltip("Physics layer of the blocks. It must collide with characters, and be in Combat Settings' Obstacle Layers to stop projectiles and sight.")]
    [LayerIndex] public int layer = 0;
    [Tooltip("Carve the NavMesh so mobs path around the barrier (and cannot walk out of a cage).")]
    public bool carveNavMesh = true;
    [Tooltip("Characters standing where a block appears are pushed out of it (to the side they are on).")]
    public bool pushOutOverlapping = true;

    [Header("Contact (optional)")]
    [Tooltip("Effects applied every Contact Interval to characters touching the barrier (fire wall, thorns). No effects = harmless.")]
    public HitSettings contactHit = new HitSettings(TargetFilter.Enemies);
    [Min(0.1f)] public float contactInterval = 0.5f;

    [Header("Feedback")]
    public GameObject spawnVfx;
    public AudioClip spawnSound;

    public float Duration(in AbilityStats s) => duration * s.duration;

    public BarrierAction()
    {
        anchor = ActionAnchor.AimPoint;
    }

    public override void Execute(AbilityCastInstance cast)
    {
        ActionFrame frame = cast.GetFrame(anchor);
        AbilityRuntime.Add(new BarrierInstance(this, cast, frame));
        if (spawnSound != null)
            AbilityPool.PlaySound(spawnSound, frame.position, cast.Definition.presentation.volume);
        cast.EmitNoise(frame.position, 0.9f);
    }

    /// <summary>Area covered by the barrier (used for telegraphs and contact hits).</summary>
    internal ResolvedShape Footprint(in ActionFrame frame, in AbilityStats s, float margin)
    {
        float a = s.area;
        switch (layout)
        {
            case BarrierLayout.Cage:
                return ResolvedShape.Resolve(new HitShape
                {
                    type = HitShapeType.Ring,
                    radius = radius * a + thickness * 0.5f + margin,
                    innerRadius = Mathf.Max(0f, radius * a - thickness * 0.5f - margin),
                    height = height + 1f,
                    baseOffset = -1f,
                }, frame.position, frame.rotation, 1f);
            case BarrierLayout.Arc:
                return ResolvedShape.Resolve(new HitShape
                {
                    type = HitShapeType.Cone,
                    radius = radius * a + thickness * 0.5f + margin,
                    innerRadius = Mathf.Max(0f, radius * a - thickness * 0.5f - margin),
                    angle = arcAngle,
                    height = height + 1f,
                    baseOffset = -1f,
                }, frame.position, frame.rotation, 1f);
            default:
                return ResolvedShape.Resolve(new HitShape
                {
                    type = HitShapeType.Rectangle,
                    width = length * a + margin * 2f,
                    length = thickness + margin * 2f,
                    startAtOrigin = false,
                    offset = new Vector3(0f, 0f, forwardOffset),
                    height = height + 1f,
                    baseOffset = -1f,
                }, frame.position, frame.rotation, 1f);
        }
    }

    /// <summary>Positions and rotations of the blocks (base centre on the ground).</summary>
    internal void Layout(in ActionFrame frame, in AbilityStats s, List<Pose> poses, out float segWidth)
    {
        poses.Clear();
        Quaternion rot = frame.rotation;
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        float a = s.area;
        switch (layout)
        {
            case BarrierLayout.Wall:
            {
                float len = length * a;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / segmentWidth));
                segWidth = len / n;
                Vector3 centre = frame.position + fwd * forwardOffset;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = centre + right * ((i - (n - 1) * 0.5f) * segWidth);
                    poses.Add(new Pose(p, rot));
                }
                break;
            }
            default:
            {
                float r = radius * a;
                float span = layout == BarrierLayout.Cage ? 360f : arcAngle;
                float arcLen = r * span * Mathf.Deg2Rad;
                int n = Mathf.Max(layout == BarrierLayout.Cage ? 6 : 2, Mathf.CeilToInt(arcLen / segmentWidth));
                segWidth = arcLen / n * 1.08f; // slight overlap closes gaps on curves
                for (int i = 0; i < n; i++)
                {
                    float t = layout == BarrierLayout.Cage ? i / (float)n : (n == 1 ? 0.5f : i / (float)(n - 1));
                    float ang = layout == BarrierLayout.Cage ? t * 360f : Mathf.Lerp(-span * 0.5f, span * 0.5f, t);
                    if (layout == BarrierLayout.Arc && n > 1)
                        segWidth = arcLen / (n - 1) * 1.08f;
                    Vector3 radial = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                    poses.Add(new Pose(frame.position + radial * r, Quaternion.LookRotation(radial, Vector3.up)));
                }
                break;
            }
        }
    }

    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        shapes.Add(Footprint(cast.GetFrame(anchor), cast.Stats, 0f));
        return true;
    }

    public override TargetFilter HazardFilter => contactHit.filter;
    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) => contactHit.effects.Count > 0 ? Duration(s) : 0.3f;

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        float r = layout == BarrierLayout.Wall ? Mathf.Abs(forwardOffset) + length * s.area * 0.5f : radius * s.area;
        return anchor == ActionAnchor.Caster ? r : def.Range(s) + r;
    }

    public override bool WouldHit(in CastPreview p)
    {
        if (anchor == ActionAnchor.Caster)
            return true;
        return CombatQuery.FlatDistance(p.casterPosition, p.targetVolume.basePosition) <= p.definition.Range(p.stats) + p.targetVolume.radius;
    }

    public override float EstimateDamage(in AbilityStats s) => contactHit.EstimateDamage(s);
    public override float EstimateControl(in AbilityStats s) => layout == BarrierLayout.Cage ? Duration(s) * 0.5f : 0f;

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string what = layout == BarrierLayout.Wall ? $"Wall {length * s.area:0.#}m" : (layout == BarrierLayout.Cage ? $"Cage r{radius * s.area:0.#}m" : $"Arc wall r{radius * s.area:0.#}m {arcAngle:0}°");
        string contact = contactHit.effects.Count > 0 ? $"; touching it: {contactHit.Describe(s)}" : "";
        return $"{what} for {Duration(s):0.#}s{contact}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        if (contactHit.effects.Count > 0)
            contactHit.Validate(owner, errors, warnings);
        if (layout == BarrierLayout.Cage && anchor == ActionAnchor.Caster)
            warnings.Add($"{owner}: a cage anchored on the Caster traps the caster; use Target Unit to trap the target.");
        if (layer < 0 || layer > 31)
            errors.Add($"{owner}: Layer must be between 0 and 31.");
        else if ((CombatSettings.Instance.obstacleLayers.value & (1 << layer)) == 0)
            warnings.Add($"{owner}: layer {LayerMask.LayerToName(layer)} is not in Combat Settings' Obstacle Layers; projectiles and sight will pass through.");
        int count = Mathf.CeilToInt((layout == BarrierLayout.Wall ? length : radius * (layout == BarrierLayout.Cage ? 360f : arcAngle) * Mathf.Deg2Rad) / Mathf.Max(0.2f, segmentWidth));
        if (count > 60)
            warnings.Add($"{owner}: {count} blocks is a lot; increase Segment Width.");
    }

    public override void Prewarm()
    {
        GameObject p = BarrierInstance.ResolvePrefab(segmentPrefab);
        if (p != null)
            AbilityPool.Prewarm(p, 8);
        if (spawnVfx != null)
            AbilityPool.Prewarm(spawnVfx, 1);
    }

    public override CastAction Clone()
    {
        var c = (BarrierAction)base.Clone();
        c.contactHit = contactHit.Clone();
        return c;
    }

    // ------------------------------------------------------------------ runtime
    private sealed class BarrierInstance : IAbilityRuntimeObject
    {
        private static GameObject generatedPrefab;
        private static readonly List<Pose> poses = new List<Pose>(32);
        private static readonly List<CombatEntity> nearby = new List<CombatEntity>(16);

        private readonly BarrierAction action;
        private readonly AbilityCastInstance cast;
        private readonly List<GameObject> segments = new List<GameObject>(16);
        private readonly List<float> baseY = new List<float>(16);
        private readonly ResolvedShape footprint;
        private readonly float total;
        private readonly float segHeight;
        private float age;
        private float nextContact;
        private HazardArea hazard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => generatedPrefab = null;

        public static GameObject ResolvePrefab(GameObject prefab)
        {
            if (prefab != null)
                return prefab;
            CombatSettings settings = CombatSettings.Instance;
            if (settings.defaultBarrierSegmentPrefab != null)
                return settings.defaultBarrierSegmentPrefab;
            if (generatedPrefab == null && Application.isPlaying)
            {
                generatedPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                generatedPrefab.name = "Barrier Block";
                Material m = settings.BarrierMaterial;
                if (m != null)
                    generatedPrefab.GetComponent<MeshRenderer>().sharedMaterial = m;
                NavMeshObstacle o = generatedPrefab.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Box;
                o.size = Vector3.one;
                o.carving = true;
                generatedPrefab.SetActive(false);
                AbilityRuntime rt = AbilityRuntime.Instance;
                if (rt != null)
                    generatedPrefab.transform.SetParent(rt.transform, false);
            }
            return generatedPrefab;
        }

        public BarrierInstance(BarrierAction action, AbilityCastInstance cast, ActionFrame frame)
        {
            this.action = action;
            this.cast = cast;
            total = action.Duration(cast.Stats);
            segHeight = action.height;
            footprint = action.Footprint(frame, cast.Stats, 0.5f);

            GameObject prefab = ResolvePrefab(action.segmentPrefab);
            action.Layout(frame, cast.Stats, poses, out float segWidth);
            for (int i = 0; i < poses.Count; i++)
            {
                Vector3 p = poses[i].position;
                if (CombatQuery.GroundHeight(p, out float gy, 3f, 6f))
                    p.y = gy;
                baseY.Add(p.y);
                GameObject go = AbilityPool.Spawn(prefab, p + Vector3.up * (segHeight * 0.5f - (action.riseTime > 0f ? segHeight : 0f)), poses[i].rotation);
                if (go == null)
                    continue;
                if (!go.activeSelf)
                    go.SetActive(true);
                go.layer = action.layer;
                go.transform.localScale = new Vector3(segWidth, segHeight, action.thickness);
                NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
                if (obstacle == null && action.carveNavMesh)
                {
                    obstacle = go.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = Vector3.one;
                }
                if (obstacle != null)
                {
                    obstacle.carving = action.carveNavMesh;
                    obstacle.enabled = action.carveNavMesh;
                }
                segments.Add(go);
                if (action.spawnVfx != null && (i % 3 == 0 || poses.Count <= 4))
                    AbilityPool.PlayVfx(action.spawnVfx, p, poses[i].rotation, 0f, cast.Stats.area);
            }
            poses.Clear();

            if (action.pushOutOverlapping)
                PushOut();

            if (action.contactHit.effects.Count > 0)
                hazard = HazardRegistry.AddShape(cast.CasterEntity, action.contactHit.filter, footprint, Time.time, Time.time + total, cast.Severity);
        }

        private void PushOut()
        {
            footprint.GetBounds(out Vector3 c, out float r);
            CombatQuery.InRadius(c, r + 1f, null, TargetFilter.All, nearby);
            for (int k = 0; k < nearby.Count; k++)
            {
                CombatEntity e = nearby[k];
                for (int i = 0; i < segments.Count; i++)
                {
                    Transform t = segments[i].transform;
                    Vector3 local = Quaternion.Inverse(t.rotation) * (e.Position - t.position);
                    float halfW = t.localScale.x * 0.5f + e.Radius;
                    float halfT = t.localScale.z * 0.5f + e.Radius;
                    if (Mathf.Abs(local.x) > halfW || Mathf.Abs(local.z) > halfT)
                        continue;
                    float side = action.layout == BarrierLayout.Cage && cast.Target == e ? -1f : Mathf.Sign(local.z == 0f ? -1f : local.z);
                    float push = halfT - Mathf.Abs(local.z) + 0.15f;
                    Vector3 dir = t.rotation * new Vector3(0f, 0f, side);
                    e.ApplyDisplacement(dir * push, 0.12f, 0f, false, cast.CasterEntity);
                    break;
                }
            }
            nearby.Clear();
        }

        public bool Tick(float dt)
        {
            age += dt;
            float rise = action.riseTime;
            if (rise > 0f && (age <= rise + dt || age >= total - rise))
            {
                float up = age < total - rise ? Mathf.Clamp01(age / rise) : Mathf.Clamp01((total - age) / rise);
                up = 1f - (1f - up) * (1f - up);
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] == null)
                        continue;
                    Vector3 p = segments[i].transform.position;
                    p.y = baseY[i] + segHeight * 0.5f - segHeight * (1f - up);
                    segments[i].transform.position = p;
                }
            }

            if (action.contactHit.effects.Count > 0)
            {
                nextContact -= dt;
                if (nextContact <= 0f)
                {
                    nextContact += action.contactInterval;
                    cast.HitArea(footprint, action.contactHit, Vector3.zero);
                }
            }
            return age < total;
        }

        public void Dispose()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                    AbilityPool.Release(segments[i]);
            }
            segments.Clear();
            HazardRegistry.Remove(hazard);
            hazard = null;
        }
    }
}
