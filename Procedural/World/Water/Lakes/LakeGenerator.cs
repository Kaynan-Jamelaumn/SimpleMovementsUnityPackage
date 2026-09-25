using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Places lakes and ponds as sparse, deterministic features: the world is divided into a grid (a coarse
/// one for lakes, a finer one for ponds) and each cell holds at most one candidate, whose footprint is
/// kept entirely inside its own cell. A candidate only becomes a lake/pond if its site suits one:
///
/// - gentle enough ground (a lake doesn't sit on a mountainside),
/// - away from the ocean's coastal zone,
/// - preferably a natural depression (the center lower than its surroundings),
/// - in a biome that likes that kind of water (<see cref="Biome.lakeLikelihood"/>/<see cref="Biome.pondLikelihood"/>).
///
/// The water level comes from the site itself (the lowest point of the natural rim), so a lake sits in
/// the landscape instead of at some global height. Ponds are the same system at a much smaller scale,
/// shallower, allowed on slightly steeper ground, and never overlapping a lake.
/// </summary>
public static class LakeGenerator
{
    private const int LakeSalt = 0x1A4E;
    private const int PondSalt = 0x70D5;
    private const int TerminalSalt = 0x7E21;
    private const int RingSamples = 12;
    private const float MaxHarmonicSum = 0.38f;
    // Upper bound on (biome likelihood x depression bonus), so the cheap early-out roll never rejects a
    // cell that the full evaluation could still accept.
    private const float MaxAcceptanceMultiplier = 3.75f;

    private static readonly ConcurrentDictionary<long, Lazy<LakeFeature>> Lakes = new ConcurrentDictionary<long, Lazy<LakeFeature>>();
    private static readonly ConcurrentDictionary<long, Lazy<LakeFeature>> Ponds = new ConcurrentDictionary<long, Lazy<LakeFeature>>();

    public static void ClearCache()
    {
        Lakes.Clear();
        Ponds.Clear();
    }

    /// <summary>
    /// Width (world units) of the band past a lake's outline in which the terrain is guaranteed to stay
    /// above the lake. At least 1.5 terrain mesh vertices, so the water mesh edge always lands on it.
    /// </summary>
    public static float RimWidth(WaterSettings s) => Mathf.Max(s.ShoreRimWidth, 1.5f * s.LodCells);

    public static float DamFadeLength(WaterSettings s) => Mathf.Max(16f, RimWidth(s) * 2f);

    /// <summary>Grid spacing actually used - enlarged if needed so the largest possible footprint fits inside one cell.</summary>
    public static float EffectiveSpacing(WaterSettings s, bool pond)
    {
        float maxRadius = pond ? s.PondMaxRadius : s.LakeMaxRadius;
        float spacing = pond ? s.PondSpacing : s.LakeSpacing;
        float extent = maxRadius * (1f + MaxHarmonicSum) + RimWidth(s) + DamFadeLength(s);
        return Mathf.Max(spacing, 2f * extent + 4f);
    }

    public static LakeFeature GetLake(Vector2Int cell, WaterSettings s, TerrainHeightSampler sampler)
    {
        return s.LakesEnabled ? Get(Lakes, cell, false, s, sampler) : null;
    }

    public static LakeFeature GetPond(Vector2Int cell, WaterSettings s, TerrainHeightSampler sampler)
    {
        return s.PondsEnabled ? Get(Ponds, cell, true, s, sampler) : null;
    }

    /// <summary>Adds every lake and pond whose footprint overlaps the given world rectangle.</summary>
    public static void GatherForRect(Vector2 rectMin, Vector2 rectMax, WaterSettings s, TerrainHeightSampler sampler, List<LakeFeature> into)
    {
        if (s.LakesEnabled)
            GatherGrid(false, rectMin, rectMax, s, sampler, into);
        if (s.PondsEnabled)
            GatherGrid(true, rectMin, rectMax, s, sampler, into);
    }

    /// <summary>
    /// The grid lake (not pond) whose outline, scaled by <paramref name="rhoLimit"/>, contains
    /// <paramref name="position"/>, if any. Lakes never extend past their own grid cell, so only that
    /// one cell needs checking.
    /// </summary>
    public static LakeFeature FindLakeContaining(Vector2 position, LakeFeature exclude, WaterSettings s, TerrainHeightSampler sampler, float rhoLimit = 1f)
    {
        if (!s.LakesEnabled)
            return null;

        LakeFeature lake = GetLake(WaterGenerator.CellOf(position, EffectiveSpacing(s, false)), s, sampler);
        if (lake == null || lake == exclude)
            return null;

        return lake.TryGetLocal(position.x, position.y, out float rho, out _) && rho < rhoLimit ? lake : null;
    }

    /// <summary>
    /// A lake formed where a river got trapped in a basin with no way further downhill (an endorheic
    /// lake). It isn't on the lake grid - its river owns it.
    /// </summary>
    public static LakeFeature CreateTerminalLake(Vector2 center, float radius, WaterSettings s, TerrainHeightSampler sampler)
    {
        System.Random rng = new System.Random(WaterGenerator.Hash(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y), s.Seed, TerminalSalt));
        LakeFeature lake = CreateShape(WaterBodyType.Lake, radius, rng, s);
        lake.Center = center;
        EvaluateSite(lake, s, sampler, Mathf.Min(s.LakeMaxDepth, 2f + radius * 0.1f), out _, out _);
        HookUpWaterLevel(lake, s, sampler);
        return lake;
    }

    private static void GatherGrid(bool pond, Vector2 rectMin, Vector2 rectMax, WaterSettings s, TerrainHeightSampler sampler, List<LakeFeature> into)
    {
        float spacing = EffectiveSpacing(s, pond);
        Vector2Int min = WaterGenerator.CellOf(rectMin, spacing);
        Vector2Int max = WaterGenerator.CellOf(rectMax, spacing);

        for (int cy = min.y; cy <= max.y; cy++)
        {
            for (int cx = min.x; cx <= max.x; cx++)
            {
                Vector2Int cell = new Vector2Int(cx, cy);
                LakeFeature feature = pond ? GetPond(cell, s, sampler) : GetLake(cell, s, sampler);
                if (feature != null && feature.Intersects(rectMin, rectMax))
                    into.Add(feature);
            }
        }
    }

    private static LakeFeature Get(ConcurrentDictionary<long, Lazy<LakeFeature>> cache, Vector2Int cell, bool pond, WaterSettings s, TerrainHeightSampler sampler)
    {
        long key = WaterGenerator.CellKey(cell);
        if (!cache.TryGetValue(key, out Lazy<LakeFeature> lazy))
        {
            // Lazy + ExecutionAndPublication: many chunk threads can ask for the same cell at once, but
            // it's evaluated exactly once and everyone gets the identical result.
            lazy = cache.GetOrAdd(key, NewEntry(cell, pond, s, sampler));
        }
        return lazy.Value;
    }

    // Separate from Get so the lambda's captured variables are only allocated when a cell is first seen,
    // not on every lookup (C# allocates a method's closure when the method starts).
    private static Lazy<LakeFeature> NewEntry(Vector2Int cell, bool pond, WaterSettings s, TerrainHeightSampler sampler)
    {
        return new Lazy<LakeFeature>(() => SafeEvaluate(cell, pond, s, sampler), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static LakeFeature SafeEvaluate(Vector2Int cell, bool pond, WaterSettings s, TerrainHeightSampler sampler)
    {
        try
        {
            return Evaluate(cell, pond, s, sampler);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    private static LakeFeature Evaluate(Vector2Int cell, bool pond, WaterSettings s, TerrainHeightSampler sampler)
    {
        System.Random rng = new System.Random(WaterGenerator.Hash(cell.x, cell.y, s.Seed, pond ? PondSalt : LakeSalt));
        float chance = pond ? s.PondChance : s.LakeChance;
        float roll = (float)rng.NextDouble();
        if (roll >= Mathf.Min(1f, chance * MaxAcceptanceMultiplier))
            return null;

        float minRadius = pond ? s.PondMinRadius : s.LakeMinRadius;
        float maxRadius = pond ? s.PondMaxRadius : s.LakeMaxRadius;
        float sizeRoll = Mathf.Pow((float)rng.NextDouble(), 1.6f); // skewed toward the smaller end
        float radius = Mathf.Lerp(minRadius, maxRadius, sizeRoll);

        LakeFeature lake = CreateShape(pond ? WaterBodyType.Pond : WaterBodyType.Lake, radius, rng, s);

        // Keep the whole footprint (outline + rim + dam fade) inside this cell, so a position only ever
        // needs to check the lake of its own cell and two lakes can never overlap.
        float spacing = EffectiveSpacing(s, pond);
        float margin = lake.BoundRadius + 1f;
        float span = Mathf.Max(0f, spacing - 2f * margin);
        lake.Center = new Vector2(
            cell.x * spacing + margin + (float)rng.NextDouble() * span,
            cell.y * spacing + margin + (float)rng.NextDouble() * span);

        float targetDepth = pond
            ? s.PondDepth * Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble())
            : s.LakeMaxDepth * Mathf.Lerp(0.45f, 1f, sizeRoll);
        float outletRoll = (float)rng.NextDouble();

        float maxSlope = pond ? s.PondMaxSiteSlope : s.LakeMaxSiteSlope;
        if (!EvaluateSite(lake, s, sampler, targetDepth, out float depressionScore, out float lowestAngle, maxSlope))
            return null;

        Biome biome = sampler.SampleBiome(lake.Center.x, lake.Center.y);
        float likelihood = biome == null || !biome.allowsWaterBodies ? 0f : (pond ? biome.pondLikelihood : biome.lakeLikelihood);
        float acceptance = chance * Mathf.Max(0f, likelihood) * Mathf.Lerp(0.35f, 1.25f, depressionScore);
        if (roll >= acceptance)
            return null;

        if (pond)
        {
            if (OverlapsLake(lake, s, sampler))
                return null;
        }
        else if (outletRoll < s.LakeOutletChance)
        {
            // Outlet through the lowest point of the rim - where the water would naturally spill over.
            Vector2 direction = new Vector2(Mathf.Cos(lowestAngle), Mathf.Sin(lowestAngle));
            lake.HasOutlet = true;
            lake.OutletDirection = direction;
            lake.OutletPoint = lake.Center + direction * (lake.ShapeRadius(lowestAngle) * 0.7f);
        }

        HookUpWaterLevel(lake, s, sampler);
        return lake;
    }

    private static void HookUpWaterLevel(LakeFeature lake, WaterSettings s, TerrainHeightSampler sampler)
    {
        if (s.RiversEnabled)
            lake.SetWaterLevelSource(() => RiverGenerator.ComputeLakeWaterLevel(lake, s, sampler));
    }

    private static LakeFeature CreateShape(WaterBodyType type, float radius, System.Random rng, WaterSettings s)
    {
        LakeFeature lake = new LakeFeature
        {
            Type = type,
            Radius = radius,
            RimWidth = RimWidth(s),
            DamFadeLength = DamFadeLength(s),
            Freeboard = s.ShoreFreeboard,
            HarmonicAmplitude = new float[3],
            HarmonicPhase = new float[3],
        };

        // A few low harmonics give irregular, organic outlines (bays, lobes, elongation) without ever
        // folding back on themselves - amplitudes sum to at most MaxHarmonicSum.
        lake.HarmonicAmplitude[0] = 0.06f + 0.14f * (float)rng.NextDouble();
        lake.HarmonicAmplitude[1] = 0.03f + 0.08f * (float)rng.NextDouble();
        lake.HarmonicAmplitude[2] = 0.02f + 0.05f * (float)rng.NextDouble();
        for (int i = 0; i < 3; i++)
            lake.HarmonicPhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;

        float amplitudeSum = lake.HarmonicAmplitude[0] + lake.HarmonicAmplitude[1] + lake.HarmonicAmplitude[2];
        lake.BoundRadius = radius * (1f + amplitudeSum) + lake.RimWidth + lake.DamFadeLength;
        return lake;
    }

    /// <summary>
    /// Samples the undisturbed terrain around the lake's outline to decide whether the site works and to
    /// derive the water level (just below the lowest rim point) and bowl depth. Returns false for sites
    /// that are too steep, too close to the ocean or would need an unreasonably deep cut.
    /// </summary>
    private static bool EvaluateSite(LakeFeature lake, WaterSettings s, TerrainHeightSampler sampler, float targetDepth,
        out float depressionScore, out float lowestAngle, float maxSlope = float.PositiveInfinity)
    {
        depressionScore = 0f;
        lowestAngle = 0f;
        bool enforce = !float.IsPositiveInfinity(maxSlope);
        float coastBuffer = s.CoastBlendWidth * s.ContinentGradient;

        if (enforce && s.OceansEnabled && OceanGenerator.LandSide(s, lake.Center.x, lake.Center.y) < coastBuffer)
            return false;

        float centerHeight = sampler.SampleBaseHeight(lake.Center.x, lake.Center.y);
        float ringMin = float.PositiveInfinity;
        float ringMax = float.NegativeInfinity;
        float ringSum = 0f;

        for (int k = 0; k < RingSamples; k++)
        {
            float angle = k * (Mathf.PI * 2f / RingSamples);
            Vector2 point = lake.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * lake.ShapeRadius(angle);

            if (enforce && s.OceansEnabled && OceanGenerator.LandSide(s, point.x, point.y) < coastBuffer)
                return false;

            float h = sampler.SampleBaseHeight(point.x, point.y);
            ringSum += h;
            ringMax = Mathf.Max(ringMax, h);
            if (h < ringMin)
            {
                ringMin = h;
                lowestAngle = angle;
            }
        }

        float slope = (ringMax - ringMin) / (2f * lake.Radius);
        if (enforce && slope > maxSlope)
            return false;

        lake.Level = ringMin - s.ShoreFreeboard;
        lake.BowlDepth = Mathf.Max(0f, centerHeight - lake.Level) + targetDepth;

        if (enforce && lake.BowlDepth > Mathf.Max(targetDepth * 4f, lake.Radius * 0.5f))
            return false;

        float concavity = ringSum / RingSamples - centerHeight;
        depressionScore = Mathf.Clamp01(0.5f + concavity / (0.06f * lake.Radius + 1f));
        return true;
    }

    private static bool OverlapsLake(LakeFeature pond, WaterSettings s, TerrainHeightSampler sampler)
    {
        if (!s.LakesEnabled)
            return false;

        float spacing = EffectiveSpacing(s, false);
        Vector2 extent = new Vector2(pond.BoundRadius, pond.BoundRadius);
        Vector2Int min = WaterGenerator.CellOf(pond.Center - extent, spacing);
        Vector2Int max = WaterGenerator.CellOf(pond.Center + extent, spacing);

        for (int cy = min.y; cy <= max.y; cy++)
        {
            for (int cx = min.x; cx <= max.x; cx++)
            {
                LakeFeature lake = GetLake(new Vector2Int(cx, cy), s, sampler);
                if (lake != null && (lake.Center - pond.Center).magnitude < lake.BoundRadius + pond.BoundRadius)
                    return true;
            }
        }

        return false;
    }
}
