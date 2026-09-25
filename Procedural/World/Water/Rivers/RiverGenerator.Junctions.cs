using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// RiverGenerator, part 6: river junctions (see RiverGenerator.cs).
//
// Rivers are traced independently, so two of them often end up in the same valley. Where a river meets a
// bigger one, it should flow into it: its water drops to the other river's level at the confluence and it
// ends there, instead of running alongside at its own, higher level (which left its water standing next to
// ground the other river had carved lower - visible "leaks" at sharp bends and confluences).
//
// Which river continues is decided by a fixed rule that doesn't depend on which is looked at first: the
// longer one (ties broken by a per-river hash). A river only ever checks the other rivers as they were
// traced, before their own junctions, so one river's junctions never depend on another's - no chains of
// rivers waiting on each other, and every chunk sees exactly the same result.
public static partial class RiverGenerator
{
    // A river counts as having entered another once its centerline is within the other's channel plus this
    // share of its own width.
    private const float JunctionOverlap = 0.5f;
    // The river flowing in may meet the other's water at most this much above its own surface.
    private const float JunctionRiseTolerance = 0.5f;
    private const float JunctionGridCell = 40f;
    // How many points before the confluence share the step down to the other river's level.
    private const int JunctionApproachPoints = 3;

    /// <summary>A river with its junction applied (the same river when it doesn't flow into another one).</summary>
    private static RiverPath Resolve(RiverPath river, WaterSettings s, TerrainHeightSampler sampler)
    {
        if (river == null)
            return null;

        Lazy<RiverPath> resolved = river.Resolved;
        if (resolved == null)
        {
            var candidate = new Lazy<RiverPath>(() => SafeResolve(river, s, sampler), LazyThreadSafetyMode.ExecutionAndPublication);
            resolved = Interlocked.CompareExchange(ref river.Resolved, candidate, null) ?? candidate;
        }
        return resolved.Value;
    }

    private static RiverPath SafeResolve(RiverPath river, WaterSettings s, TerrainHeightSampler sampler)
    {
        try
        {
            return ApplyJunction(river, s, sampler);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return river;
        }
    }

    /// <summary>True when <paramref name="b"/> keeps flowing where it meets <paramref name="a"/>.</summary>
    private static bool Dominates(RiverPath b, RiverPath a)
    {
        return b.Length > a.Length || (b.Length == a.Length && b.Rank < a.Rank);
    }

    private static RiverPath ApplyJunction(RiverPath a, WaterSettings s, TerrainHeightSampler sampler)
    {
        var others = new List<RiverPath>();
        GatherRivers(a.BoundsMin, a.BoundsMax, s, sampler, others, false);

        // The points of every river that would continue if they met, bucketed by position.
        var grid = new Dictionary<long, List<KeyValuePair<RiverPath, int>>>();
        for (int r = 0; r < others.Count; r++)
        {
            RiverPath b = others[r];
            if (b == a || !Dominates(b, a))
                continue;
            for (int k = 0; k < b.Points.Length; k++)
            {
                long key = GridKey(b.Points[k]);
                if (!grid.TryGetValue(key, out var bucket))
                    grid[key] = bucket = new List<KeyValuePair<RiverPath, int>>();
                bucket.Add(new KeyValuePair<RiverPath, int>(b, k));
            }
        }
        if (grid.Count == 0)
            return a;

        // Walk this river from its source; the first point inside a continuing river's channel is the confluence.
        for (int i = 1; i < a.Points.Length; i++)
        {
            Vector2 p = a.Points[i];
            if (a.SourceLake != null && a.SourceLake.TryGetLocal(p.x, p.y, out float rho, out _) && rho < 1.25f)
                continue;   // still leaving its own lake

            RiverPath bestRiver = null;
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            int gx = Mathf.FloorToInt(p.x / JunctionGridCell), gy = Mathf.FloorToInt(p.y / JunctionGridCell);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!grid.TryGetValue(GridKey(gx + dx, gy + dy), out var bucket))
                        continue;
                    for (int n = 0; n < bucket.Count; n++)
                    {
                        RiverPath b = bucket[n].Key;
                        int k = bucket[n].Value;
                        float distance = Vector2.Distance(p, b.Points[k]);
                        float reach = b.HalfWidth[k] + JunctionOverlap * a.HalfWidth[i] + TraceStep * 0.5f;
                        if (distance > reach || b.Surface[k] > a.Surface[i] + JunctionRiseTolerance)
                            continue;
                        // Deterministic choice: nearest, then the continuing river's own rank, then point order.
                        if (distance < bestDistance || (distance == bestDistance && bestRiver != null && (b.Rank < bestRiver.Rank || (b == bestRiver && k < bestIndex))))
                        {
                            bestDistance = distance;
                            bestRiver = b;
                            bestIndex = k;
                        }
                    }
                }
            }

            if (bestRiver != null)
                return EndAtJunction(a, i, bestRiver, bestIndex, s);
        }

        return a;
    }

    /// <summary>
    /// The river cut at point <paramref name="lastIndex"/>, plus one final point on the other river's centerline
    /// at that river's water level - so the water steps down into the other river (a steep enough drop shows
    /// as a small waterfall) instead of standing above it.
    /// </summary>
    private static RiverPath EndAtJunction(RiverPath a, int lastIndex, RiverPath into, int intoIndex, WaterSettings s)
    {
        int n = lastIndex + 2;
        var river = new RiverPath
        {
            Points = new Vector2[n],
            Surface = new float[n],
            Bed = new float[n],
            HalfWidth = new float[n],
            ValleyHalfWidth = new float[n],
            MouthType = WaterBodyType.River,
            TerminalLake = null,
            SourceLake = a.SourceLake,
            Length = lastIndex * TraceStep,
            Rank = a.Rank,
            JoinsRiver = into,
        };
        Array.Copy(a.Points, river.Points, n - 1);
        Array.Copy(a.Surface, river.Surface, n - 1);
        Array.Copy(a.Bed, river.Bed, n - 1);
        Array.Copy(a.HalfWidth, river.HalfWidth, n - 1);
        Array.Copy(a.ValleyHalfWidth, river.ValleyHalfWidth, n - 1);

        int last = n - 1;
        float depth = a.Surface[lastIndex] - a.Bed[lastIndex];
        river.Points[last] = into.Points[intoIndex];
        river.Surface[last] = Mathf.Min(a.Surface[lastIndex], into.Surface[intoIndex]);
        river.Bed[last] = Mathf.Min(river.Surface[last] - depth, into.Bed[intoIndex]);
        river.HalfWidth[last] = a.HalfWidth[lastIndex];
        river.ValleyHalfWidth[last] = a.ValleyHalfWidth[lastIndex];

        // The final stretch that runs inside the other river's channel and banks is at that river's own level
        // there: water standing higher inside the other river's banks would sit against ground the other river
        // carved lower. Only ever lowers the water.
        float bank = Mathf.Max(4f, 1.5f * s.LodCells);
        int zoneStart = last;
        for (int i = last - 1; i >= 1; i--)
        {
            float distance = DistanceToRiver(into, river.Points[i], out int nearest);
            if (distance > into.HalfWidth[nearest] + river.HalfWidth[i] + bank + 2f)
                break;
            zoneStart = i;
            LowerTo(river, i, into.Surface[nearest]);
        }
        for (int i = zoneStart + 1; i <= last; i++)
            LowerTo(river, i, river.Surface[i - 1]);   // keep it flowing downhill

        // Spread the step down to that level over the few points before (a short cascade rather than one
        // abrupt drop at the other river's bank).
        int first = Mathf.Max(0, zoneStart - JunctionApproachPoints);
        for (int i = first + 1; i < zoneStart; i++)
        {
            float t = (i - first) / (float)(zoneStart - first);
            LowerTo(river, i, Mathf.Lerp(river.Surface[first], river.Surface[zoneStart], t));
        }

        // Waterfalls on the part that's kept.
        var falls = new List<RiverFall>();
        for (int f = 0; f < a.Falls.Length; f++)
        {
            if (NearestPointIndex(a.Points, a.Falls[f].Position) < lastIndex)
                falls.Add(a.Falls[f]);
        }
        river.Falls = falls.ToArray();

        float maxValley = 0f;
        Vector2 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 boundsMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < n; i++)
        {
            maxValley = Mathf.Max(maxValley, river.ValleyHalfWidth[i]);
            boundsMin = Vector2.Min(boundsMin, river.Points[i]);
            boundsMax = Vector2.Max(boundsMax, river.Points[i]);
        }
        Vector2 pad = new Vector2(maxValley + ValleyFade, maxValley + ValleyFade);
        river.BoundsMin = boundsMin - pad;
        river.BoundsMax = boundsMax + pad;
        return river;
    }

    /// <summary>Lowers a point's water surface (and bed with it) to <paramref name="level"/> if it is higher.</summary>
    private static void LowerTo(RiverPath river, int i, float level)
    {
        if (level >= river.Surface[i])
            return;
        float depth = river.Surface[i] - river.Bed[i];
        river.Surface[i] = level;
        river.Bed[i] = level - depth;
    }

    /// <summary>Distance from a position to a river's centerline, and the river point nearest to it.</summary>
    private static float DistanceToRiver(RiverPath river, Vector2 position, out int nearest)
    {
        nearest = NearestPointIndex(river.Points, position);
        float best = Vector2.Distance(position, river.Points[nearest]);
        for (int k = Mathf.Max(0, nearest - 1); k < Mathf.Min(river.Points.Length - 1, nearest + 1); k++)
        {
            Vector2 a = river.Points[k], b = river.Points[k + 1];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(position - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(position, a + ab * t));
        }
        return best;
    }

    private static int NearestPointIndex(Vector2[] points, Vector2 position)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i] - position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    private static long GridKey(Vector2 p) => GridKey(Mathf.FloorToInt(p.x / JunctionGridCell), Mathf.FloorToInt(p.y / JunctionGridCell));
    private static long GridKey(int x, int y) => ((long)x << 32) ^ (uint)y;
}
