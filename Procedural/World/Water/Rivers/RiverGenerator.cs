using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

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
public static partial class RiverGenerator
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
        GatherRivers(rectMin, rectMax, s, sampler, into, s.RiverJunctions);
    }

    /// <summary>
    /// Traces every river that could reach the rectangle, spread over all CPU cores, so a following
    /// <see cref="Gather"/> of that area only reads the cache. The rivers are exactly the same either way (each is
    /// traced once, from its own cell); this is for tools that need a large area at once, like the editor's world preview.
    /// </summary>
    public static void Prefetch(Vector2 rectMin, Vector2 rectMax, WaterSettings s, TerrainHeightSampler sampler)
    {
        if (!s.RiversEnabled)
            return;

        float reach = s.RiverMaxLength + s.RiverMaxValleyHalfWidth + ValleyFade + MaxTerminalLakeRadius * 2f + TraceStep * 4f;
        Vector2 reachVector = new Vector2(reach, reach);
        var cells = new List<KeyValuePair<Vector2Int, bool>>();
        AddCells(cells, rectMin - reachVector, rectMax + reachVector, s.RiverSpacing, false);
        if (s.LakesEnabled)
            AddCells(cells, rectMin - reachVector, rectMax + reachVector, LakeGenerator.EffectiveSpacing(s, false), true);

        // Base traces first (junctions compare against other rivers' base traces), then junctions.
        System.Threading.Tasks.Parallel.ForEach(cells, cell => GetCached(cell.Value ? Outlets : Springs, cell.Key, cell.Value, s, sampler));
        if (s.RiverJunctions)
            System.Threading.Tasks.Parallel.ForEach(cells, cell => Resolve(GetCached(cell.Value ? Outlets : Springs, cell.Key, cell.Value, s, sampler), s, sampler));
    }

    private static void AddCells(List<KeyValuePair<Vector2Int, bool>> cells, Vector2 min, Vector2 max, float spacing, bool outlet)
    {
        Vector2Int first = WaterGenerator.CellOf(min, spacing);
        Vector2Int last = WaterGenerator.CellOf(max, spacing);
        for (int cy = first.y; cy <= last.y; cy++)
            for (int cx = first.x; cx <= last.x; cx++)
                cells.Add(new KeyValuePair<Vector2Int, bool>(new Vector2Int(cx, cy), outlet));
    }

    /// <summary>As <see cref="Gather"/>; <paramref name="resolveJunctions"/> = false gives the rivers as traced, before junctions.</summary>
    private static void GatherRivers(Vector2 rectMin, Vector2 rectMax, WaterSettings s, TerrainHeightSampler sampler, List<RiverPath> into, bool resolveJunctions)
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
                if (resolveJunctions)
                    river = Resolve(river, s, sampler);
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
                if (resolveJunctions)
                    river = Resolve(river, s, sampler);
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
            lazy = cache.GetOrAdd(key, NewEntry(cell, outlet, s, sampler));
        }
        return lazy.Value;
    }

    // Separate from GetCached so the lambda's captured variables are only allocated when a cell is first
    // seen, not on every lookup (C# allocates a method's closure when the method starts).
    private static Lazy<RiverPath> NewEntry(Vector2Int cell, bool outlet, WaterSettings s, TerrainHeightSampler sampler)
    {
        return new Lazy<RiverPath>(() => SafeEvaluate(cell, outlet, s, sampler), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static RiverPath SafeEvaluate(Vector2Int cell, bool outlet, WaterSettings s, TerrainHeightSampler sampler)
    {
        try
        {
            RiverPath river = outlet ? EvaluateOutlet(cell, s, sampler) : EvaluateSpring(cell, s, sampler);
            if (river != null)
                river.Rank = WaterGenerator.Hash(cell.x, cell.y, s.Seed, outlet ? OutletSalt + 1 : SpringSalt + 1);
            return river;
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
        // 3 = the highest biome riverSpringLikelihood allowed (times the most snowmelt can add), so this
        // early-out never rejects a spring the full evaluation below could accept.
        if (roll >= Mathf.Min(1f, s.RiverChance * 3f * (1f + s.SnowmeltSprings)))
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
        // Snowmelt: above the snow line, springs are more likely (fading in over a quarter of the snow line's height).
        float snow = s.SnowmeltSprings > 0f && s.SnowLineHeight > 0f
            ? WaterGenerator.SmoothStep01((elevation - s.SnowLineHeight) / Mathf.Max(1f, 0.25f * s.SnowLineHeight))
            : 0f;
        if (roll >= s.RiverChance * likelihood * elevationFactor * (1f + s.SnowmeltSprings * snow))
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
}
