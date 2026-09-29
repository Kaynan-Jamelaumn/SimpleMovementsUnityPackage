using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A dangerous area the AI knows about: an incoming telegraphed attack, a burning zone, a projectile's path, a lava
/// pool... Mobs avoid walking into hazards and dodge out of incoming ones.
/// </summary>
public sealed class HazardArea
{
    public int id;
    /// <summary>Who created it (null = the environment: dangerous to everyone).</summary>
    public CombatEntity source;
    /// <summary>Who it hurts, relative to <see cref="source"/>.</summary>
    public TargetFilter filter = TargetFilter.AllButSelf;
    public bool isCapsule;
    public ResolvedShape shape;
    public Vector3 capsuleStart, capsuleEnd;
    public float capsuleRadius;
    /// <summary>Time.time when it starts hurting (the impact of a telegraphed attack).</summary>
    public float activeFrom;
    /// <summary>Time.time when it stops being dangerous (float.MaxValue = until removed).</summary>
    public float activeUntil;
    /// <summary>0-1: how bad it is (a stun + big damage = 1, a light tick = 0.2).</summary>
    public float severity = 0.5f;
    /// <summary>Time.time when the hazard was registered (AI reaction delays count from here).</summary>
    public float createdTime;
    internal bool alive;

    /// <summary>False once the hazard was removed (the object may be reused by the pool).</summary>
    public bool IsAlive => alive;

    public bool IsActiveAt(float time) => time >= activeFrom && time <= activeUntil;

    public bool Overlaps(in TargetVolume v)
    {
        if (!isCapsule)
            return shape.Overlaps(v);
        Vector3 c = v.basePosition + Vector3.up * (v.height * 0.5f);
        Vector3 ab = capsuleEnd - capsuleStart;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(c - capsuleStart, ab) / len2) : 0f;
        Vector3 closest = capsuleStart + ab * t;
        float reach = capsuleRadius + v.radius + v.height * 0.5f;
        return (c - closest).sqrMagnitude <= reach * reach;
    }

    public Vector3 EscapeDirection(Vector3 position, out float distance)
    {
        if (!isCapsule)
            return shape.EscapeDirection(position, out distance);
        Vector3 ab = capsuleEnd - capsuleStart;
        ab.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, ab.sqrMagnitude > 1e-6f ? ab.normalized : Vector3.forward);
        Vector3 rel = position - capsuleStart;
        float s = Vector3.Dot(rel, side);
        distance = Mathf.Max(0f, capsuleRadius + 0.5f - Mathf.Abs(s));
        return s >= 0f ? side : -side;
    }
}

/// <summary>What <see cref="HazardRegistry.Query"/> found.</summary>
public struct HazardInfo
{
    public HazardArea hazard;
    /// <summary>Seconds until it hurts (0 or less = already hurting).</summary>
    public float timeToImpact;
    public Vector3 escapeDirection;
    public float escapeDistance;
}

/// <summary>Registry of <see cref="HazardArea"/>s the AI queries (no allocations in queries).</summary>
public static class HazardRegistry
{
    private static readonly List<HazardArea> hazards = new List<HazardArea>(64);
    private static readonly Stack<HazardArea> pool = new Stack<HazardArea>(32);
    private static int nextId = 1;

    public static int Count => hazards.Count;
    public static IReadOnlyList<HazardArea> All => hazards;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        for (int i = 0; i < hazards.Count; i++)
            hazards[i].alive = false;
        hazards.Clear();
    }

    private static HazardArea Take()
    {
        HazardArea h = pool.Count > 0 ? pool.Pop() : new HazardArea();
        h.id = nextId++;
        h.alive = true;
        h.createdTime = Time.time;
        hazards.Add(h);
        return h;
    }

    /// <summary>Registers a shaped hazard active between two times.</summary>
    public static HazardArea AddShape(CombatEntity source, TargetFilter filter, in ResolvedShape shape, float activeFrom, float activeUntil, float severity)
    {
        HazardArea h = Take();
        h.source = source;
        h.filter = filter;
        h.isCapsule = false;
        h.shape = shape;
        h.activeFrom = activeFrom;
        h.activeUntil = activeUntil;
        h.severity = Mathf.Clamp01(severity);
        return h;
    }

    /// <summary>Registers a capsule hazard (a projectile's path, a beam).</summary>
    public static HazardArea AddCapsule(CombatEntity source, TargetFilter filter, Vector3 start, Vector3 end, float radius, float activeFrom, float activeUntil, float severity)
    {
        HazardArea h = Take();
        h.source = source;
        h.filter = filter;
        h.isCapsule = true;
        h.capsuleStart = start;
        h.capsuleEnd = end;
        h.capsuleRadius = radius;
        h.activeFrom = activeFrom;
        h.activeUntil = activeUntil;
        h.severity = Mathf.Clamp01(severity);
        return h;
    }

    /// <summary>Removes a hazard (safe to call twice).</summary>
    public static void Remove(HazardArea h)
    {
        if (h == null || !h.alive)
            return;
        h.alive = false;
        int i = hazards.IndexOf(h);
        if (i >= 0)
        {
            int last = hazards.Count - 1;
            hazards[i] = hazards[last];
            hazards.RemoveAt(last);
        }
        h.source = null;
        pool.Push(h);
    }

    /// <summary>Drops expired hazards (called by the ability runtime every frame).</summary>
    public static void Cleanup(float now)
    {
        for (int i = hazards.Count - 1; i >= 0; i--)
        {
            HazardArea h = hazards[i];
            if (now > h.activeUntil)
                Remove(h);
        }
    }

    private static bool Threatens(HazardArea h, CombatEntity self)
    {
        if (h.source == null)
            return true;
        if (self == null)
            return true;
        if (h.source == self)
            return (h.filter & TargetFilter.Self) != 0;
        return CombatRelations.Passes(h.filter, h.source, self);
    }

    /// <summary>
    /// The most urgent hazard that would hurt <paramref name="self"/> standing at <paramref name="position"/> within
    /// the next <paramref name="horizon"/> seconds.
    /// </summary>
    public static bool Query(CombatEntity self, Vector3 position, float bodyRadius, float bodyHeight, float horizon, out HazardInfo info)
    {
        info = default;
        float now = Time.time;
        float bestScore = float.MinValue;
        var volume = new TargetVolume(position, bodyRadius, bodyHeight);

        for (int i = 0; i < hazards.Count; i++)
        {
            HazardArea h = hazards[i];
            if (now > h.activeUntil || h.activeFrom > now + horizon)
                continue;
            if (!Threatens(h, self))
                continue;
            if (!h.Overlaps(volume))
                continue;
            float tti = h.activeFrom - now;
            float score = h.severity * 2f - Mathf.Max(0f, tti);
            if (score > bestScore)
            {
                bestScore = score;
                info.hazard = h;
                info.timeToImpact = tti;
                info.escapeDirection = h.EscapeDirection(position, out info.escapeDistance);
            }
        }
        return info.hazard != null;
    }

    /// <summary>True if no hazard threatening <paramref name="self"/> covers <paramref name="position"/> soon.</summary>
    public static bool IsSafe(CombatEntity self, Vector3 position, float bodyRadius, float horizon = 1.5f)
    {
        return !Query(self, position, bodyRadius, self != null ? self.Height : 2f, horizon, out _);
    }
}

/// <summary>
/// Marks a static or moving danger for the AI (lava, spike traps, fire pits). Mobs will not path through it and step
/// out of it. Does not deal damage by itself.
/// </summary>
[AddComponentMenu("Combat/AI Hazard Source")]
public class AIHazardSource : MonoBehaviour
{
    [Tooltip("Area of the hazard, relative to this object (forward = this object's forward).")]
    public HitShape shape = new HitShape { type = HitShapeType.Circle, radius = 2f };
    [Tooltip("How much mobs fear it (0-1). High values make them avoid it even when chasing.")]
    [Range(0f, 1f)] public float severity = 0.8f;
    [Tooltip("Update the hazard position every frame (for moving hazards).")]
    public bool moving = false;

    private HazardArea area;

    private void OnEnable() => Register();

    private void OnDisable()
    {
        HazardRegistry.Remove(area);
        area = null;
    }

    private void Register()
    {
        HazardRegistry.Remove(area);
        ResolvedShape r = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
        area = HazardRegistry.AddShape(null, TargetFilter.All, r, float.MinValue, float.MaxValue, severity);
    }

    private void Update()
    {
        if (!moving || area == null)
            return;
        area.shape = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        var r = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
        var outer = new List<Vector3>();
        var inner = new List<Vector3>();
        r.GetOutline(outer, inner);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
        for (int i = 1; i < outer.Count; i++)
            Gizmos.DrawLine(outer[i - 1], outer[i]);
        for (int i = 1; i < inner.Count; i++)
            Gizmos.DrawLine(inner[i - 1], inner[i]);
    }
}
