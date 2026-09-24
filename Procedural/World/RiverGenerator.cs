using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// One traced river: a polyline plus, per point, its water surface, channel bed, channel half-width and
/// valley half-width. The surface never rises downstream.
/// </summary>
public sealed class RiverPath
{
    public Vector2[] Points;
    public float[] Surface;
    public float[] Bed;
    public float[] HalfWidth;
    public float[] ValleyHalfWidth;
    public Vector2 BoundsMin;
    public Vector2 BoundsMax;
    public WaterBodyType MouthType;
    public LakeFeature TerminalLake;
    /// <summary>The lake this river is the outlet of, if any.</summary>
    public LakeFeature SourceLake;
    public float Length;
    /// <summary>Waterfalls along this river (empty when none) - lip position and the water level above and below.</summary>
    public RiverFall[] Falls = new RiverFall[0];

    public bool Intersects(Vector2 min, Vector2 max)
    {
        return BoundsMax.x >= min.x && BoundsMin.x <= max.x && BoundsMax.y >= min.y && BoundsMin.y <= max.y;
    }
}

/// <summary>One waterfall drop on a river.</summary>
public struct RiverFall
{
    public Vector2 Position;
    public float Top;
    public float Bottom;
}

/// <summary>
/// Per-cell river data for one chunk (flattened, index = y * size + x), filled by <see cref="RiverGenerator.Rasterize"/>.
/// </summary>
public sealed class RiverRaster
{
    /// <summary>Pre-erosion valley/channel carve target (terrain is lowered to at most this).</summary>
    public readonly float[] Carve;
    /// <summary>Post-erosion channel profile (terrain is kept at or below this inside the channel).</summary>
    public readonly float[] Channel;
    /// <summary>Post-erosion bank height (terrain right beside the channel is kept at or above this).</summary>
    public readonly float[] Bank;
    /// <summary>Water surface of the river owning this cell's channel.</summary>
    public readonly float[] Surface;
    /// <summary>Distance to the owning river's centerline divided by its half-width (&lt;= 1 = inside the channel).</summary>
    public readonly float[] OwnerNorm;
    /// <summary>Water surface of the nearest river within its valley, for shoreline extension.</summary>
    public readonly float[] Hint;
    public readonly float[] HintDistance;

    public RiverRaster(int count)
    {
        Carve = Filled(count, float.PositiveInfinity);
        Channel = Filled(count, float.PositiveInfinity);
        Bank = Filled(count, float.NegativeInfinity);
        Surface = Filled(count, float.NaN);
        OwnerNorm = Filled(count, float.PositiveInfinity);
        Hint = Filled(count, float.NaN);
        HintDistance = Filled(count, float.PositiveInfinity);
    }

    private static float[] Filled(int count, float value)
    {
        float[] array = new float[count];
        for (int i = 0; i < count; i++)
            array[i] = value;
        return array;
    }
}

/// <summary>
/// Rivers are traced paths, not a height range: each one starts at a spring on higher ground (or at a
/// lake's outlet), then walks the terrain downhill with some momentum and a meander wobble, until it
/// reaches the ocean or a lake - or gets trapped in a basin, where it ends in a small lake of its own.
///
/// The path is traced over the same deterministic base terrain every chunk generates from, once, and
/// cached globally; each chunk then only rasterizes the segments that touch it. That's what keeps a
/// river continuous across chunk borders - no chunk ever decides a river's course on its own.
///
/// Along the path the water surface is forced to only go downhill (it's the running minimum of the
/// terrain beside the river), which is what lets a river cut a gorge through a rise instead of flowing
/// uphill. The channel sits below that surface, and a valley whose width depends on how deep the cut
/// is (shallow = wide gentle valley, deep = gorge) is carved around it.
/// </summary>
public static class RiverGenerator
{
    public const float TraceStep = 10f;
    public const float ValleyFade = 24f;
    private const float GradientEpsilon = 8f;
    private const float Inertia = 0.55f;
    // Steps without reaching lower ground before a river counts as trapped in a pit.
    private const int PitSteps = 8;
    private const int MaxPitSpills = 40;
    // Spill search: coarse grid around a pit, and how deep a pit may be (below its lowest saddle) before
    // it counts as an enclosed basin rather than something a river would overflow.
    private const int SpillGridRadius = 30;
    private const float SpillGridSpacing = 20f;
    private const float MaxFillDepth = 40f;
    private const float CoastBias = 0.5f;
    private const float InletReach = 1.15f;
    private const float CoastBiasEpsilon = 150f;
    private const float CarveFadeLift = 1000f;
    private const float MaxTerminalLakeRadius = 70f;
    private const int SpringSalt = 0x5A11;
    private const int OutletSalt = 0x0B7E;

    private static readonly ConcurrentDictionary<long, Lazy<RiverPath>> Springs = new ConcurrentDictionary<long, Lazy<RiverPath>>();
    private static readonly ConcurrentDictionary<long, Lazy<RiverPath>> Outlets = new ConcurrentDictionary<long, Lazy<RiverPath>>();

    public static void ClearCache()
    {
        Springs.Clear();
        Outlets.Clear();
    }

    /// <summary>
    /// Adds every river that passes within influence of the given world rectangle. A river can be at
    /// most <see cref="WaterSettings.RiverMaxLength"/> long, so only sources within that distance can
    /// reach the rectangle.
    /// </summary>
    public static void Gather(Vector2 rectMin, Vector2 rectMax, WaterSettings s, TerrainHeightSampler sampler, List<RiverPath> into)
    {
        if (!s.RiversEnabled)
            return;

        float reach = s.RiverMaxLength + s.RiverMaxValleyHalfWidth + ValleyFade + MaxTerminalLakeRadius * 2f + TraceStep * 4f;
        Vector2 reachVector = new Vector2(reach, reach);

        Vector2Int min = WaterGenerator.CellOf(rectMin - reachVector, s.RiverSpacing);
        Vector2Int max = WaterGenerator.CellOf(rectMax + reachVector, s.RiverSpacing);
        for (int cy = min.y; cy <= max.y; cy++)
        {
            for (int cx = min.x; cx <= max.x; cx++)
            {
                RiverPath river = GetCached(Springs, new Vector2Int(cx, cy), false, s, sampler);
                if (river != null && river.Intersects(rectMin, rectMax))
                    into.Add(river);
            }
        }

        if (!s.LakesEnabled)
            return;

        float lakeSpacing = LakeGenerator.EffectiveSpacing(s, false);
        min = WaterGenerator.CellOf(rectMin - reachVector, lakeSpacing);
        max = WaterGenerator.CellOf(rectMax + reachVector, lakeSpacing);
        for (int cy = min.y; cy <= max.y; cy++)
        {
            for (int cx = min.x; cx <= max.x; cx++)
            {
                RiverPath river = GetCached(Outlets, new Vector2Int(cx, cy), true, s, sampler);
                if (river != null && river.Intersects(rectMin, rectMax))
                    into.Add(river);
            }
        }
    }

    private static RiverPath GetCached(ConcurrentDictionary<long, Lazy<RiverPath>> cache, Vector2Int cell, bool outlet, WaterSettings s, TerrainHeightSampler sampler)
    {
        long key = WaterGenerator.CellKey(cell);
        if (!cache.TryGetValue(key, out Lazy<RiverPath> lazy))
        {
            // Lazy + ExecutionAndPublication: many chunk threads can ask for the same river at once, but
            // it's traced exactly once and everyone gets the identical result.
            lazy = cache.GetOrAdd(key, new Lazy<RiverPath>(() => SafeEvaluate(cell, outlet, s, sampler), LazyThreadSafetyMode.ExecutionAndPublication));
        }
        return lazy.Value;
    }

    private static RiverPath SafeEvaluate(Vector2Int cell, bool outlet, WaterSettings s, TerrainHeightSampler sampler)
    {
        try
        {
            return outlet ? EvaluateOutlet(cell, s, sampler) : EvaluateSpring(cell, s, sampler);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    private static RiverPath EvaluateSpring(Vector2Int cell, WaterSettings s, TerrainHeightSampler sampler)
    {
        System.Random rng = new System.Random(WaterGenerator.Hash(cell.x, cell.y, s.Seed, SpringSalt));
        float roll = (float)rng.NextDouble();
        // 3 = the highest biome riverSpringLikelihood allowed, so this early-out never rejects a spring
        // the full evaluation below could accept.
        if (roll >= Mathf.Min(1f, s.RiverChance * 3f))
            return null;

        Vector2 spring = new Vector2(
            (cell.x + 0.15f + 0.7f * (float)rng.NextDouble()) * s.RiverSpacing,
            (cell.y + 0.15f + 0.7f * (float)rng.NextDouble()) * s.RiverSpacing);
        float phase = (float)rng.NextDouble() * 100f;

        if (s.OceansEnabled && OceanGenerator.LandSide(s, spring.x, spring.y) < s.CoastBlendWidth * s.ContinentGradient)
            return null;

        // Springs favor higher ground: none below the minimum elevation, ramping up above it.
        float elevation = sampler.SampleBaseHeight(spring.x, spring.y) - s.SeaLevel;
        float elevationFactor = WaterGenerator.SmoothStep01((elevation - s.RiverMinSpringElevation) / Mathf.Max(1f, s.RiverMinSpringElevation));
        if (elevationFactor <= 0f)
            return null;

        if (LakeGenerator.FindLakeContaining(spring, null, s, sampler) != null)
            return null;

        Biome biome = sampler.SampleBiome(spring.x, spring.y);
        float likelihood = biome == null || !biome.allowsWaterBodies ? 0f : Mathf.Max(0f, biome.riverSpringLikelihood);
        if (roll >= s.RiverChance * likelihood * elevationFactor)
            return null;

        return Trace(spring, Vector2.zero, float.PositiveInfinity, null, phase, true, s, sampler);
    }

    private static RiverPath EvaluateOutlet(Vector2Int lakeCell, WaterSettings s, TerrainHeightSampler sampler)
    {
        LakeFeature lake = LakeGenerator.GetLake(lakeCell, s, sampler);
        if (lake == null || !lake.HasOutlet)
            return null;

        System.Random rng = new System.Random(WaterGenerator.Hash(lakeCell.x, lakeCell.y, s.Seed, OutletSalt));
        float phase = (float)rng.NextDouble() * 100f;
        return Trace(lake.OutletPoint, lake.OutletDirection, lake.Level, lake, phase, false, s, sampler);
    }

    private static RiverPath Trace(Vector2 start, Vector2 initialDirection, float surfaceCap, LakeFeature sourceLake,
        float phase, bool isSpring, WaterSettings s, TerrainHeightSampler sampler)
    {
        int maxPoints = Mathf.Max(3, Mathf.CeilToInt(s.RiverMaxLength / TraceStep) + 1);
        List<Vector2> points = new List<Vector2>(maxPoints + 8);
        List<float> natural = new List<float>(maxPoints + 8);
        List<bool> insideSource = new List<bool>(maxPoints + 8);
        Queue<Vector2> spillPath = new Queue<Vector2>();

        Vector2 position = start;
        Vector2 direction = initialDirection.sqrMagnitude > 1e-6f ? initialDirection.normalized : Vector2.zero;
        bool leavingSource = sourceLake != null;
        float lowest = float.PositiveInfinity;
        int lowestIndex = 0;
        int spills = 0;
        bool followingSpill = false;
        WaterBodyType mouth = WaterBodyType.None;
        LakeFeature receivingLake = null;

        while (points.Count < maxPoints)
        {
            float height = sampler.SampleBaseHeight(position.x, position.y);
            int index = points.Count;
            points.Add(position);
            natural.Add(height);

            if (leavingSource)
            {
                // An outlet heads straight out through its lake's lowest rim point before following the
                // terrain - otherwise the lake's own basin could pull it right back in.
                bool stillInside = sourceLake.TryGetLocal(position.x, position.y, out float rho, out _) && rho < 1.25f;
                insideSource.Add(stillInside);
                if (stillInside)
                {
                    position += direction * TraceStep;
                    continue;
                }

                leavingSource = false;
            }
            else
            {
                insideSource.Add(false);
            }

            if (followingSpill)
            {
                // Just arrived past the saddle: measure progress from here, not from the pit bottom
                // (the ground past a saddle only has to be lower than the saddle, not than the pit).
                if (spillPath.Count == 0)
                {
                    followingSpill = false;
                    lowest = height;
                    lowestIndex = index;
                }
            }
            else if (height < lowest - 0.05f)
            {
                lowest = height;
                lowestIndex = index;
            }

            if (index > 0)
            {
                if (s.OceansEnabled && OceanGenerator.LandSide(s, position.x, position.y) < 0f)
                {
                    mouth = WaterBodyType.Ocean;
                    break;
                }

                // Slightly beyond the outline: a river passing that close flows into the lake as an inlet
                // rather than grazing (and cutting through) its rim.
                LakeFeature lake = LakeGenerator.FindLakeContaining(position, sourceLake, s, sampler, InletReach);
                if (lake != null)
                {
                    mouth = WaterBodyType.Lake;
                    receivingLake = lake;
                    break;
                }
            }

            if (spillPath.Count > 0)
            {
                // Following a spill path out of a pit: the water surface stays at the pit's level along it,
                // so crossing the rim becomes a gorge cut through its lowest saddle.
                Vector2 next = spillPath.Dequeue();
                direction = (next - position).sqrMagnitude > 1e-6f ? (next - position).normalized : direction;
                position = next;
                continue;
            }

            if (index - lowestIndex > PitSteps)
            {
                // No lower ground for a while: trapped in a pit. Back up to its lowest point and overflow
                // it the way water would - through the lowest saddle of its rim. A basin too deep to fill
                // (or out of spills) ends the river in a terminal lake instead.
                TruncateAfter(points, natural, insideSource, lowestIndex);
                position = points[lowestIndex];

                if (spills >= MaxPitSpills || !TryFindSpillPath(position, lowest, sourceLake, s, sampler, spillPath))
                    break;

                spills++;
                followingSpill = true;
                // Trimmed back to the pit bottom, so the first spill waypoint comes next.
                Vector2 first = spillPath.Dequeue();
                direction = (first - position).sqrMagnitude > 1e-6f ? (first - position).normalized : direction;
                position = first;
                lowestIndex = points.Count - 1;
                continue;
            }

            // Downhill direction from a fairly wide finite difference, so small noise bumps don't steer it.
            float heightX = sampler.SampleBaseHeight(position.x + GradientEpsilon, position.y) - sampler.SampleBaseHeight(position.x - GradientEpsilon, position.y);
            float heightY = sampler.SampleBaseHeight(position.x, position.y + GradientEpsilon) - sampler.SampleBaseHeight(position.x, position.y - GradientEpsilon);
            Vector2 downhill = new Vector2(-heightX, -heightY);
            if (downhill.sqrMagnitude > 1e-10f)
                downhill.Normalize();
            else
                downhill = direction.sqrMagnitude > 0f ? direction : new Vector2(1f, 0f);

            if (s.OceansEnabled)
            {
                // Continental drainage: a gentle pull toward the nearest coast (the continent field's
                // downhill direction, sampled wide so it reflects the landmass rather than local detail).
                // Local terrain still dominates - this only tilts a step by up to ~27 degrees - but on
                // flat or undulating ground it keeps rivers heading for the sea instead of wandering.
                float sideX = OceanGenerator.LandSide(s, position.x + CoastBiasEpsilon, position.y) - OceanGenerator.LandSide(s, position.x - CoastBiasEpsilon, position.y);
                float sideY = OceanGenerator.LandSide(s, position.x, position.y + CoastBiasEpsilon) - OceanGenerator.LandSide(s, position.x, position.y - CoastBiasEpsilon);
                Vector2 seaward = new Vector2(-sideX, -sideY);
                if (seaward.sqrMagnitude > 1e-12f)
                    downhill = (downhill + seaward.normalized * CoastBias).normalized;
            }

            if (direction.sqrMagnitude == 0f)
                direction = downhill;

            // Meander: rotate the downhill direction by a smooth, arc-length-driven wobble (two octaves so
            // bends aren't all the same size). Capped below 90 degrees, so every step still goes downhill.
            float arc = index * TraceStep;
            float wiggle = s.RiverMeander * (
                (Mathf.PerlinNoise(arc / s.RiverMeanderWavelength + phase, 0.37f) * 2f - 1f)
                + 0.5f * (Mathf.PerlinNoise(arc / (s.RiverMeanderWavelength * 0.37f) + phase * 1.7f, 5.1f) * 2f - 1f));
            Vector2 desired = Rotate(downhill, wiggle);

            Vector2 blended = desired * (1f - Inertia) + direction * Inertia;
            direction = blended.sqrMagnitude > 1e-8f ? blended.normalized : desired;
            position += direction * TraceStep;
        }

        if (mouth == WaterBodyType.None)
        {
            // Trapped in an enclosed basin, or out of length: end at the lowest point reached, where a
            // terminal lake will form.
            TruncateAfter(points, natural, insideSource, Mathf.Max(1, lowestIndex));
        }

        if (points.Count < 3)
            return null;

        float length = (points.Count - 1) * TraceStep;
        if (isSpring && length < s.RiverMinLength)
            return null;

        // Run the channel a little way into whatever the river flows into, so the two waters connect.
        if (mouth == WaterBodyType.Ocean)
        {
            Vector2 last = points[points.Count - 1];
            Vector2 heading = (last - points[points.Count - 2]).normalized;
            for (int k = 1; k <= 3; k++)
                AddPoint(points, natural, insideSource, last + heading * (TraceStep * k), sampler);
        }
        else if (mouth == WaterBodyType.Lake)
        {
            Vector2 last = points[points.Count - 1];
            Vector2 target = last + (receivingLake.Center - last) * 0.45f;
            for (int k = 1; k <= 3; k++)
                AddPoint(points, natural, insideSource, Vector2.Lerp(last, target, k / 3f), sampler);
        }

        LakeFeature terminal = null;
        if (mouth == WaterBodyType.None)
        {
            float radius = Mathf.Clamp(18f + length * 0.012f, 18f, MaxTerminalLakeRadius);
            terminal = LakeGenerator.CreateTerminalLake(points[points.Count - 1], radius, s, sampler);
            mouth = WaterBodyType.Lake;
            receivingLake = terminal;
        }

        RiverPath river = BuildPath(points, natural, insideSource, length, surfaceCap, mouth, receivingLake, terminal, phase, s, sampler);
        river.SourceLake = sourceLake;
        return river;
    }

    /// <summary>
    /// Finds where a pit would overflow if it filled with water: a priority-flood on a coarse grid around
    /// the pit's lowest point, always expanding the cell that needs the least water to reach, until it
    /// reaches ground lower than the pit (or the ocean, or a lake). That's exactly how a basin fills and
    /// spills, so the route it took crosses the rim at its lowest saddle. Tried at a fine spacing first,
    /// then a coarse one to catch outflows across wide basins. Returns false for a basin that would need more than
    /// <see cref="MaxFillDepth"/> of water, or whose outflow is farther than the search area - a genuinely
    /// enclosed basin, where the river ends in a terminal lake.
    /// </summary>
    private static bool TryFindSpillPath(Vector2 pit, float pitHeight, LakeFeature sourceLake, WaterSettings s, TerrainHeightSampler sampler, Queue<Vector2> path)
    {
        return TryFindSpillPath(pit, pitHeight, SpillGridSpacing, sourceLake, s, sampler, path)
            || TryFindSpillPath(pit, pitHeight, SpillGridSpacing * 3f, sourceLake, s, sampler, path);
    }

    private static bool TryFindSpillPath(Vector2 pit, float pitHeight, float spacing, LakeFeature sourceLake, WaterSettings s, TerrainHeightSampler sampler, Queue<Vector2> path)
    {
        const int radius = SpillGridRadius;
        const int size = radius * 2 + 1;
        int cells = size * size;
        bool[] visited = new bool[cells];
        int[] parent = new int[cells];
        CellHeap open = new CellHeap(256);

        int startIndex = radius * size + radius;
        visited[startIndex] = true;
        parent[startIndex] = -1;
        open.Push(pitHeight, startIndex);
        int found = -1;

        while (open.Count > 0 && found < 0)
        {
            open.Pop(out float level, out int current);
            if (level > pitHeight + MaxFillDepth)
                break;

            int cx = current % size;
            int cy = current / size;
            for (int dy = -1; dy <= 1 && found < 0; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                        continue;

                    int neighbor = ny * size + nx;
                    if (visited[neighbor])
                        continue;

                    visited[neighbor] = true;
                    parent[neighbor] = current;
                    Vector2 point = pit + new Vector2((nx - radius) * spacing, (ny - radius) * spacing);

                    // Only ground lower than the pit's own bottom counts as an outflow: anything higher
                    // is still part of the same (combined) basin, and would just drain back into the pit.
                    float height = sampler.SampleBaseHeight(point.x, point.y);
                    if (height < pitHeight - 1f
                        || (s.OceansEnabled && height < s.SeaLevel && OceanGenerator.LandSide(s, point.x, point.y) < 0f)
                        || LakeGenerator.FindLakeContaining(point, sourceLake, s, sampler) != null)
                    {
                        found = neighbor;
                        break;
                    }

                    open.Push(Mathf.Max(level, height), neighbor);
                }
            }
        }

        if (found < 0)
            return false;

        List<Vector2> route = new List<Vector2>();
        for (int cell = found; cell >= 0; cell = parent[cell])
            route.Add(pit + new Vector2((cell % size - radius) * spacing, (cell / size - radius) * spacing));
        route.Reverse();

        // The grid route is a staircase of 45/90 degree moves - smooth it, then walk it at TraceStep.
        route = ChaikinSmooth(ChaikinSmooth(route));
        path.Clear();
        float carried = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 a = route[i - 1];
            Vector2 b = route[i];
            float segment = (b - a).magnitude;
            float t = TraceStep - carried;
            while (t <= segment)
            {
                path.Enqueue(Vector2.Lerp(a, b, t / segment));
                t += TraceStep;
            }
            carried = segment - (t - TraceStep);
        }

        if (path.Count == 0)
            path.Enqueue(route[route.Count - 1]);
        return true;
    }

    private static List<Vector2> ChaikinSmooth(List<Vector2> points)
    {
        if (points.Count < 3)
            return points;

        List<Vector2> smoothed = new List<Vector2>(points.Count * 2);
        smoothed.Add(points[0]);
        for (int i = 0; i < points.Count - 1; i++)
        {
            smoothed.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
            smoothed.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
        }
        smoothed.Add(points[points.Count - 1]);
        return smoothed;
    }

    /// <summary>Minimal binary min-heap of (key, cell index), for the spill priority-flood.</summary>
    private sealed class CellHeap
    {
        private float[] keys;
        private int[] values;
        public int Count;

        public CellHeap(int capacity)
        {
            keys = new float[capacity];
            values = new int[capacity];
        }

        public void Push(float key, int value)
        {
            if (Count == keys.Length)
            {
                Array.Resize(ref keys, Count * 2);
                Array.Resize(ref values, Count * 2);
            }

            int i = Count++;
            while (i > 0)
            {
                int parentIndex = (i - 1) / 2;
                if (keys[parentIndex] <= key)
                    break;
                keys[i] = keys[parentIndex];
                values[i] = values[parentIndex];
                i = parentIndex;
            }
            keys[i] = key;
            values[i] = value;
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0];
            value = values[0];
            Count--;
            if (Count == 0)
                return;

            float lastKey = keys[Count];
            int lastValue = values[Count];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= Count)
                    break;
                if (child + 1 < Count && keys[child + 1] < keys[child])
                    child++;
                if (lastKey <= keys[child])
                    break;
                keys[i] = keys[child];
                values[i] = values[child];
                i = child;
            }
            keys[i] = lastKey;
            values[i] = lastValue;
        }
    }

    /// <summary>
    /// A lake's actual water level: its natural <see cref="LakeFeature.Level"/>, lowered to the surface of
    /// any river that cuts through its rim below that level (other than its own inlets, which never sit
    /// below it, and its own outlet). Such a river would otherwise leave the lake's water standing next
    /// to a lower channel - it drains the lake instead, the way a river cutting into a pond does.
    /// </summary>
    public static float ComputeLakeWaterLevel(LakeFeature lake, WaterSettings s, TerrainHeightSampler sampler)
    {
        float level = lake.Level;
        if (!s.RiversEnabled)
            return level;

        List<RiverPath> rivers = new List<RiverPath>();
        Vector2 extent = new Vector2(lake.BoundRadius, lake.BoundRadius);
        Gather(lake.Center - extent, lake.Center + extent, s, sampler, rivers);

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            if (river.TerminalLake == lake || river.SourceLake == lake)
                continue;

            for (int i = 0; i < river.Points.Length; i++)
            {
                Vector2 point = river.Points[i];
                if (!lake.TryGetLocal(point.x, point.y, out _, out float beyond))
                    continue;

                if (beyond - river.HalfWidth[i] <= lake.RimWidth && river.Surface[i] < level)
                    level = river.Surface[i];
            }
        }

        return level;
    }

    private static void TruncateAfter(List<Vector2> points, List<float> natural, List<bool> insideSource, int lastIndex)
    {
        int keep = lastIndex + 1;
        if (keep >= points.Count)
            return;

        points.RemoveRange(keep, points.Count - keep);
        natural.RemoveRange(keep, natural.Count - keep);
        insideSource.RemoveRange(keep, insideSource.Count - keep);
    }

    private static void AddPoint(List<Vector2> points, List<float> natural, List<bool> insideSource, Vector2 point, TerrainHeightSampler sampler)
    {
        points.Add(point);
        natural.Add(sampler.SampleBaseHeight(point.x, point.y));
        insideSource.Add(false);
    }

    private static RiverPath BuildPath(List<Vector2> points, List<float> natural, List<bool> insideSource, float length, float surfaceCap,
        WaterBodyType mouth, LakeFeature receivingLake, LakeFeature terminal, float phase, WaterSettings s, TerrainHeightSampler sampler)
    {
        int n = points.Count;
        var halfWidth = new List<float>(n);
        var depth = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            float progress = Mathf.Clamp01(i * TraceStep / Mathf.Max(TraceStep, length));
            float widthNoise = Mathf.PerlinNoise(i * TraceStep / 110f + phase * 2.3f, 7.7f) * 2f - 1f;
            float width = Mathf.Lerp(s.RiverSourceWidth, s.RiverMouthWidth, Mathf.Pow(progress, 0.6f)) * (1f + s.RiverWidthVariation * widthNoise);
            halfWidth.Add(Mathf.Max(0.75f, width * 0.5f));
            depth.Add(s.RiverDepth * Mathf.Lerp(0.4f, 1f, progress));
        }

        // Water surface: running minimum of the terrain beside the river (minus freeboard), so it only
        // ever goes downhill and always sits below both banks where they were sampled.
        var surfaces = new List<float>(n);
        float surface = surfaceCap;
        for (int i = 0; i < n; i++)
        {
            if (!insideSource[i])
            {
                Vector2 tangent = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : new Vector2(1f, 0f);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float offset = halfWidth[i] + 2f;
                Vector2 left = points[i] + normal * offset;
                Vector2 right = points[i] - normal * offset;
                float bankMin = Mathf.Min(natural[i], Mathf.Min(sampler.SampleBaseHeight(left.x, left.y), sampler.SampleBaseHeight(right.x, right.y)));
                surface = Mathf.Min(surface, bankMin - s.RiverBankFreeboard);
            }

            surfaces.Add(surface);
        }

        // Never below whatever it flows into (still non-increasing: max of a non-increasing series and a constant).
        float receivingLevel = mouth == WaterBodyType.Ocean ? s.SeaLevel : receivingLake.Level;
        for (int i = 0; i < n; i++)
            surfaces[i] = Mathf.Max(surfaces[i], receivingLevel);

        var extraDepth = new List<float>(new float[n]);
        RiverFall[] falls = s.WaterfallsEnabled
            ? Waterfalls.Shape(points, natural, halfWidth, depth, surfaces, extraDepth, s)
            : new RiverFall[0];

        n = points.Count;
        RiverPath river = new RiverPath
        {
            Points = points.ToArray(),
            Surface = surfaces.ToArray(),
            Bed = new float[n],
            HalfWidth = halfWidth.ToArray(),
            ValleyHalfWidth = new float[n],
            MouthType = mouth,
            TerminalLake = terminal,
            Length = length,
            Falls = falls,
        };

        float maxValley = 0f;
        Vector2 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 boundsMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < n; i++)
        {
            river.Bed[i] = river.Surface[i] - depth[i] - extraDepth[i];

            // How far below the untouched terrain the river runs decides the valley: a shallow cut makes
            // a wide gentle valley, a deep one (cutting through a rise) a gorge, capped at the max width.
            float cut = Mathf.Max(0f, natural[i] - river.Surface[i] - s.RiverBankFreeboard);
            float valley = river.HalfWidth[i] * 2.2f + 6f + cut / s.RiverValleySlopeTan;
            valley = Mathf.Min(valley, Mathf.Max(river.HalfWidth[i] * 2.2f + 6f, s.RiverMaxValleyHalfWidth));
            river.ValleyHalfWidth[i] = valley;

            maxValley = Mathf.Max(maxValley, valley);
            boundsMin = Vector2.Min(boundsMin, points[i]);
            boundsMax = Vector2.Max(boundsMax, points[i]);
        }

        Vector2 pad = new Vector2(maxValley + ValleyFade, maxValley + ValleyFade);
        boundsMin -= pad;
        boundsMax += pad;
        if (terminal != null)
        {
            Vector2 lakePad = new Vector2(terminal.BoundRadius, terminal.BoundRadius);
            boundsMin = Vector2.Min(boundsMin, terminal.Center - lakePad);
            boundsMax = Vector2.Max(boundsMax, terminal.Center + lakePad);
        }

        river.BoundsMin = boundsMin;
        river.BoundsMax = boundsMax;
        return river;
    }

    /// <summary>
    /// Writes every river segment's influence into a chunk's per-cell arrays. All combinations are
    /// min/max, so the result doesn't depend on the order rivers or segments are processed in.
    /// </summary>
    public static void Rasterize(List<RiverPath> rivers, WaterSettings s, Vector2 origin, int size, RiverRaster raster)
    {
        Vector2 rectMin = origin;
        Vector2 rectMax = origin + new Vector2(size - 1, size - 1);
        float bankWidth = Mathf.Max(4f, 1.5f * s.LodCells);
        float valleyTan = s.RiverValleySlopeTan;

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            if (!river.Intersects(rectMin, rectMax))
                continue;

            for (int i = 0; i < river.Points.Length - 1; i++)
            {
                Vector2 a = river.Points[i];
                Vector2 b = river.Points[i + 1];
                float reach = Mathf.Max(river.ValleyHalfWidth[i], river.ValleyHalfWidth[i + 1]) + ValleyFade;

                int x0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.x, b.x) - reach - origin.x));
                int x1 = Mathf.Min(size - 1, Mathf.FloorToInt(Mathf.Max(a.x, b.x) + reach - origin.x));
                int y0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, b.y) - reach - origin.y));
                int y1 = Mathf.Min(size - 1, Mathf.FloorToInt(Mathf.Max(a.y, b.y) + reach - origin.y));
                if (x0 > x1 || y0 > y1)
                    continue;

                Vector2 ab = b - a;
                float lengthSq = ab.sqrMagnitude;

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = origin.x + x - a.x;
                        float py = origin.y + y - a.y;
                        float t = lengthSq > 1e-8f ? Mathf.Clamp01((px * ab.x + py * ab.y) / lengthSq) : 0f;
                        float dx = px - ab.x * t;
                        float dy = py - ab.y * t;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        float valley = Mathf.Lerp(river.ValleyHalfWidth[i], river.ValleyHalfWidth[i + 1], t);
                        if (distance > valley + ValleyFade)
                            continue;

                        int index = y * size + x;
                        float surface = Mathf.Lerp(river.Surface[i], river.Surface[i + 1], t);
                        float bed = Mathf.Lerp(river.Bed[i], river.Bed[i + 1], t);
                        float halfWidth = Mathf.Lerp(river.HalfWidth[i], river.HalfWidth[i + 1], t);
                        float crest = surface + s.RiverBankFreeboard;
                        float target;

                        if (distance <= halfWidth)
                        {
                            // Rounded channel: bed at the centerline, back up to bank height at the edge.
                            float q = distance / halfWidth;
                            target = bed + (crest - bed) * q * q;
                            raster.Channel[index] = Mathf.Min(raster.Channel[index], target);

                            if (q < raster.OwnerNorm[index])
                            {
                                raster.OwnerNorm[index] = q;
                                raster.Surface[index] = surface;
                            }
                        }
                        else
                        {
                            target = crest + (distance - halfWidth) * valleyTan;
                            if (distance <= halfWidth + bankWidth)
                                raster.Bank[index] = Mathf.Max(raster.Bank[index], crest);
                        }

                        // Past the valley edge the carve fades out (by lifting its target far above any
                        // terrain), so the valley blends into the untouched land instead of ending in a step.
                        float fade = 1f - WaterGenerator.SmoothStep01((distance - valley) / ValleyFade);
                        raster.Carve[index] = Mathf.Min(raster.Carve[index], target + (1f - fade) * CarveFadeLift);

                        if (distance < raster.HintDistance[index])
                        {
                            raster.HintDistance[index] = distance;
                            raster.Hint[index] = surface;
                        }
                    }
                }
            }
        }
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}

/// <summary>
/// Turns steep stretches of a river into waterfalls. Wherever the water surface drops faster than a
/// steep-rapids grade over a meaningful height (a river running off a cliff, a plateau edge, a volcano
/// flank or into a valley), the drop is rebuilt as one or more falls: a flat pool, a rock lip, a sheer
/// drop and a plunge pool, repeated as a multi-tier fall when the drop is taller than one tier. Each
/// pool sits at the level the original surface had at the pool's downstream end, so the water is never
/// raised above its banks, and the surface still only ever goes downhill.
/// </summary>
public static class Waterfalls
{
    private const float FallGrade = 0.3f;   // surface drop per unit of river length that counts as "falling"
    private const float LipHalfGap = 0.35f; // the sheer drop happens over twice this distance

    public static RiverFall[] Shape(List<Vector2> points, List<float> natural, List<float> halfWidth, List<float> depth,
        List<float> surface, List<float> extraDepth, WaterSettings s)
    {
        int n = points.Count;
        if (n < 3)
            return new RiverFall[0];

        var arc = new float[n];
        for (int i = 1; i < n; i++)
            arc[i] = arc[i - 1] + Vector2.Distance(points[i - 1], points[i]);

        // Find steep zones (steep segments, allowing one short gentler segment between them).
        var lipArc = new List<float>();
        var lipTop = new List<float>();
        var lipBottom = new List<float>();
        var pointLevel = new float[n];
        for (int i = 0; i < n; i++)
            pointLevel[i] = float.NaN;

        int k = 0;
        while (k < n - 1)
        {
            if (!Steep(k, arc, surface))
            {
                k++;
                continue;
            }

            int a = k, b = k + 1, j = k + 1;
            while (j < n - 1)
            {
                if (Steep(j, arc, surface)) { b = j + 1; j++; }
                else if (j + 1 < n - 1 && Steep(j + 1, arc, surface) && arc[j + 1] - arc[j] < 12f) { j++; }
                else break;
            }
            k = b;

            float drop = surface[a] - surface[b];
            if (drop < s.WaterfallMinDrop)
                continue;

            float start = arc[a], end = arc[b], length = end - start;
            int tiers = Mathf.Clamp(Mathf.CeilToInt(drop / s.WaterfallTierHeight), 1, 4);
            tiers = Mathf.Max(1, Mathf.Min(tiers, Mathf.FloorToInt(length / 6f)));

            var lips = new float[tiers + 1];
            for (int t = 0; t < tiers; t++)
                lips[t] = Mathf.Min(start + length * t / tiers + 0.6f, end - 0.6f);
            lips[tiers] = end;

            var levels = new float[tiers];
            for (int t = 0; t < tiers; t++)
                levels[t] = SurfaceAt(lips[t + 1], arc, surface);

            for (int t = 0; t < tiers; t++)
            {
                lipArc.Add(lips[t]);
                lipTop.Add(t == 0 ? SurfaceAt(lips[0], arc, surface) : levels[t - 1]);
                lipBottom.Add(levels[t]);
            }

            for (int q = a + 1; q <= b; q++)
            {
                int passed = 0;
                while (passed < tiers && lips[passed] < arc[q])
                    passed++;
                if (passed > 0)
                    pointLevel[q] = levels[passed - 1];
            }
        }

        if (lipArc.Count == 0)
            return new RiverFall[0];

        // Rebuild the lists with two points around each lip (upper and lower water level).
        var newPoints = new List<Vector2>(n + lipArc.Count * 2);
        var newNatural = new List<float>(newPoints.Capacity);
        var newHalfWidth = new List<float>(newPoints.Capacity);
        var newDepth = new List<float>(newPoints.Capacity);
        var newSurface = new List<float>(newPoints.Capacity);
        var newArc = new List<float>(newPoints.Capacity);
        var falls = new List<RiverFall>(lipArc.Count);
        int lip = 0;
        for (int i = 0; i < n; i++)
        {
            while (i > 0 && lip < lipArc.Count && lipArc[lip] < arc[i])
            {
                float p = lipArc[lip];
                float gap = Mathf.Min(LipHalfGap, 0.25f * (arc[i] - arc[i - 1]));
                AddAt(p - gap, lipTop[lip], i - 1, arc, points, natural, halfWidth, depth, newPoints, newNatural, newHalfWidth, newDepth, newSurface, newArc);
                AddAt(p + gap, lipBottom[lip], i - 1, arc, points, natural, halfWidth, depth, newPoints, newNatural, newHalfWidth, newDepth, newSurface, newArc);
                falls.Add(new RiverFall { Position = PointAt(p, i - 1, arc, points), Top = lipTop[lip], Bottom = lipBottom[lip] });
                lip++;
            }

            newPoints.Add(points[i]);
            newNatural.Add(natural[i]);
            newHalfWidth.Add(halfWidth[i]);
            newDepth.Add(depth[i]);
            newSurface.Add(float.IsNaN(pointLevel[i]) ? surface[i] : pointLevel[i]);
            newArc.Add(arc[i]);
        }

        // Plunge pools below each fall; a shallower rock lip just above it.
        var extra = new float[newPoints.Count];
        for (int f = 0; f < falls.Count; f++)
        {
            float p = lipArc[f];
            float fallHeight = falls[f].Top - falls[f].Bottom;
            for (int i = 0; i < newPoints.Count; i++)
            {
                float d = newArc[i] - p;
                float pool = Mathf.Max(3f, 1.5f * newHalfWidth[i]);
                if (d > 0f && d < pool)
                    extra[i] = Mathf.Max(extra[i], Mathf.Min(0.4f * fallHeight, 1.5f * newDepth[i]) * (1f - d / pool));
                else if (d <= 0f && d > -2f)
                    extra[i] = Mathf.Min(extra[i], -0.4f * newDepth[i]);
            }
        }

        points.Clear(); points.AddRange(newPoints);
        natural.Clear(); natural.AddRange(newNatural);
        halfWidth.Clear(); halfWidth.AddRange(newHalfWidth);
        depth.Clear(); depth.AddRange(newDepth);
        surface.Clear(); surface.AddRange(newSurface);
        extraDepth.Clear(); extraDepth.AddRange(extra);
        return falls.ToArray();
    }

    private static bool Steep(int i, float[] arc, List<float> surface)
    {
        float length = Mathf.Max(0.5f, arc[i + 1] - arc[i]);
        return surface[i] - surface[i + 1] >= FallGrade * length;
    }

    private static float SurfaceAt(float p, float[] arc, List<float> surface)
    {
        int i = Segment(p, arc);
        float t = Mathf.InverseLerp(arc[i], arc[i + 1], p);
        return Mathf.Lerp(surface[i], surface[i + 1], t);
    }

    private static Vector2 PointAt(float p, int segment, float[] arc, List<Vector2> points)
    {
        float t = Mathf.InverseLerp(arc[segment], arc[segment + 1], p);
        return Vector2.Lerp(points[segment], points[segment + 1], t);
    }

    private static int Segment(float p, float[] arc)
    {
        int i = 0;
        while (i < arc.Length - 2 && arc[i + 1] < p)
            i++;
        return i;
    }

    private static void AddAt(float p, float level, int segment, float[] arc, List<Vector2> points, List<float> natural, List<float> halfWidth, List<float> depth,
        List<Vector2> outPoints, List<float> outNatural, List<float> outHalfWidth, List<float> outDepth, List<float> outSurface, List<float> outArc)
    {
        float t = Mathf.InverseLerp(arc[segment], arc[segment + 1], p);
        outPoints.Add(Vector2.Lerp(points[segment], points[segment + 1], t));
        outNatural.Add(Mathf.Lerp(natural[segment], natural[segment + 1], t));
        outHalfWidth.Add(Mathf.Lerp(halfWidth[segment], halfWidth[segment + 1], t));
        outDepth.Add(Mathf.Lerp(depth[segment], depth[segment + 1], t));
        outSurface.Add(level);
        outArc.Add(p);
    }
}
