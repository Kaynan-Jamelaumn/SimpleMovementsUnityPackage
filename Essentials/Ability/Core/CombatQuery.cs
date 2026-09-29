using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Allocation-free spatial queries used by abilities and AI: characters inside a shape or radius, line of sight,
/// ground height, NavMesh sampling and target prediction.
/// </summary>
public static class CombatQuery
{
    private static readonly RaycastHit[] rayHits = new RaycastHit[32];
    private static readonly List<float> sortKeys = new List<float>(64);

    /// <summary>Living characters whose body overlaps <paramref name="shape"/> (results cleared first).</summary>
    public static void Overlap(in ResolvedShape shape, List<CombatEntity> results)
    {
        results.Clear();
        shape.GetBounds(out Vector3 c, out float r);
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e == null || !e.IsAlive)
                continue;
            float reach = r + e.Radius + e.Height;
            if ((e.Center - c).sqrMagnitude > reach * reach)
                continue;
            if (shape.Overlaps(e.Volume))
                results.Add(e);
        }
    }

    /// <summary>
    /// Living characters within <paramref name="radius"/> of <paramref name="center"/> (body edge distance) that pass
    /// <paramref name="filter"/> relative to <paramref name="relativeTo"/>.
    /// </summary>
    public static void InRadius(Vector3 center, float radius, CombatEntity relativeTo, TargetFilter filter, List<CombatEntity> results)
    {
        results.Clear();
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e == null || !e.IsAlive)
                continue;
            if (relativeTo != null && !CombatRelations.Passes(filter, relativeTo, e))
                continue;
            float reach = radius + e.Radius;
            Vector3 d = e.Position - center;
            d.y *= 0.5f; // vertical distance counts half (slopes, stairs)
            if (d.sqrMagnitude <= reach * reach)
                results.Add(e);
        }
    }

    /// <summary>The nearest living character passing the filter within <paramref name="radius"/>, or null.</summary>
    public static CombatEntity Nearest(Vector3 center, float radius, CombatEntity relativeTo, TargetFilter filter, CombatEntity exclude = null)
    {
        CombatEntity best = null;
        float bestSqr = radius * radius;
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e == null || e == exclude || !e.IsAlive)
                continue;
            if (relativeTo != null && !CombatRelations.Passes(filter, relativeTo, e))
                continue;
            float d = (e.Position - center).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = e;
            }
        }
        return best;
    }

    /// <summary>
    /// The character closest to a view ray (crosshair) within <paramref name="maxAngle"/> degrees and
    /// <paramref name="maxRange"/> metres: used by player auto-aim.
    /// </summary>
    public static CombatEntity BestInCone(Vector3 origin, Vector3 forward, float maxAngle, float maxRange, CombatEntity relativeTo, TargetFilter filter, bool requireSight = true)
    {
        if (maxAngle <= 0f || forward.sqrMagnitude < 1e-6f)
            return null;
        forward.Normalize();
        float cosMax = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
        CombatEntity best = null;
        float bestScore = float.MinValue;
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e == null || e == relativeTo || !e.IsAlive)
                continue;
            if (relativeTo != null && !CombatRelations.Passes(filter, relativeTo, e))
                continue;
            Vector3 to = e.AimPosition - origin;
            float dist = to.magnitude;
            if (dist > maxRange + e.Radius || dist < 0.01f)
                continue;
            float cos = Vector3.Dot(to / dist, forward);
            if (cos < cosMax)
                continue;
            if (requireSight && !HasLineOfSight(origin, e.AimPosition))
                continue;
            float score = cos * 2f - dist / Mathf.Max(1f, maxRange);
            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    /// <summary>Sorts <paramref name="list"/> by distance to <paramref name="from"/> (nearest first).</summary>
    public static void SortByDistance(List<CombatEntity> list, Vector3 from)
    {
        sortKeys.Clear();
        for (int i = 0; i < list.Count; i++)
            sortKeys.Add((list[i].Position - from).sqrMagnitude);
        // Insertion sort: lists are short and often nearly sorted.
        for (int i = 1; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            float k = sortKeys[i];
            int j = i - 1;
            while (j >= 0 && sortKeys[j] > k)
            {
                list[j + 1] = list[j];
                sortKeys[j + 1] = sortKeys[j];
                j--;
            }
            list[j + 1] = e;
            sortKeys[j + 1] = k;
        }
    }

    /// <summary>
    /// True if nothing solid is between the two points. Characters do not block sight (only scenery on the
    /// obstacle layers of <see cref="CombatSettings"/>).
    /// </summary>
    public static bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.05f)
            return true;
        int n = Physics.RaycastNonAlloc(from, d / dist, rayHits, dist, CombatSettings.Instance.obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = rayHits[i].collider;
            if (col == null)
                continue;
            if (CombatEntity.Resolve(col) != null)
                continue;
            return false;
        }
        return true;
    }

    /// <summary>First obstacle (not a character) along a ray. Returns false when the way is clear.</summary>
    public static bool RaycastObstacle(Vector3 from, Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (distance <= 0f || direction.sqrMagnitude < 1e-8f)
            return false;
        int n = Physics.RaycastNonAlloc(from, direction.normalized, rayHits, distance, CombatSettings.Instance.obstacleLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayHits[i];
            if (h.collider == null || h.distance >= best)
                continue;
            if (CombatEntity.Resolve(h.collider) != null)
                continue;
            best = h.distance;
            hit = h;
            found = true;
        }
        return found;
    }

    /// <summary>Ground height below (or slightly above) a point. False if there is no ground.</summary>
    public static bool GroundHeight(Vector3 p, out float y, float searchUp = 3f, float searchDown = 30f)
    {
        y = p.y;
        Vector3 from = p + Vector3.up * searchUp;
        int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, searchUp + searchDown, CombatSettings.Instance.groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayHits[i];
            if (h.collider == null || h.distance >= best)
                continue;
            if (CombatEntity.Resolve(h.collider) != null)
                continue;
            best = h.distance;
            y = h.point.y;
            found = true;
        }
        return found;
    }

    /// <summary><paramref name="p"/> moved onto the ground (unchanged if no ground is found).</summary>
    public static Vector3 SnapToGround(Vector3 p, float searchUp = 3f, float searchDown = 30f)
    {
        if (GroundHeight(p, out float y, searchUp, searchDown))
            p.y = y;
        return p;
    }

    /// <summary>Nearest NavMesh position within <paramref name="maxDistance"/>.</summary>
    public static bool SampleNavMesh(Vector3 p, float maxDistance, out Vector3 result, int areaMask = NavMesh.AllAreas)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, maxDistance, areaMask))
        {
            result = hit.position;
            return true;
        }
        result = p;
        return false;
    }

    /// <summary>
    /// Where to aim a projectile of <paramref name="speed"/> fired from <paramref name="shooter"/> to meet a target
    /// moving at constant <paramref name="targetVelocity"/>. Falls back to the current position.
    /// </summary>
    public static bool Intercept(Vector3 shooter, float speed, Vector3 targetPosition, Vector3 targetVelocity, out Vector3 aimPoint, out float time)
    {
        aimPoint = targetPosition;
        time = 0f;
        if (speed <= 0.01f)
            return false;
        Vector3 d = targetPosition - shooter;
        float a = Vector3.Dot(targetVelocity, targetVelocity) - speed * speed;
        float b = 2f * Vector3.Dot(d, targetVelocity);
        float c = Vector3.Dot(d, d);
        float t;
        if (Mathf.Abs(a) < 1e-4f)
        {
            if (Mathf.Abs(b) < 1e-4f)
                return false;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f)
                return false;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a);
            float t2 = (-b + sq) / (2f * a);
            t = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
        }
        if (t <= 0f || float.IsNaN(t))
            return false;
        time = t;
        aimPoint = targetPosition + targetVelocity * t;
        return true;
    }

    /// <summary>Where <paramref name="target"/> will probably be in <paramref name="seconds"/> (flat velocity).</summary>
    public static Vector3 Predict(CombatEntity target, float seconds)
    {
        if (target == null)
            return Vector3.zero;
        Vector3 v = target.Velocity;
        v.y = 0f;
        if (v.sqrMagnitude > 400f)
            v = v.normalized * 20f; // ignore teleports / bad velocity
        return target.Position + v * Mathf.Max(0f, seconds);
    }

    /// <summary>Flat direction from a to b (falls back to <paramref name="fallback"/>).</summary>
    public static Vector3 FlatDirection(Vector3 from, Vector3 to, Vector3 fallback)
    {
        Vector3 d = to - from;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-6f)
        {
            fallback.y = 0f;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }
        return d.normalized;
    }

    /// <summary>Flat distance between two points.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
