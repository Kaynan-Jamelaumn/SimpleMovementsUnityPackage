using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

// ObjectPlacementEngine, part 3: landmarks - rare objects placed at the best-suited spots of each world region,
// with per-region limits, world-wide spacing, guaranteed and unique placement (see ObjectPlacementEngine.cs).
public static partial class ObjectPlacementEngine
{
    private sealed class LandmarkPick
    {
        public float X, Z;
        public float Priority;
        public float Score;
    }

    private struct RegionKey : IEquatable<RegionKey>
    {
        public int Seed, Salt, X, Z;

        public bool Equals(RegionKey other) => Seed == other.Seed && Salt == other.Salt && X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is RegionKey other && Equals(other);
        public override int GetHashCode() => ((Seed * 397 ^ Salt) * 397 ^ X) * 397 ^ Z;
    }

    // A region's picks depend only on the world, the seed and the type's rules, so they are computed once and shared.
    private static readonly ConcurrentDictionary<RegionKey, Lazy<List<LandmarkPick>>> RegionPicks = new ConcurrentDictionary<RegionKey, Lazy<List<LandmarkPick>>>();
    private static readonly ConcurrentDictionary<RegionKey, Lazy<LandmarkPick>> UniquePicks = new ConcurrentDictionary<RegionKey, Lazy<LandmarkPick>>();

    /// <summary>Forgets the cached landmark choices (call when settings change; <see cref="WaterGenerator.ClearCaches"/> does).</summary>
    public static void ClearCaches()
    {
        RegionPicks.Clear();
        UniquePicks.Clear();
    }

    private static void PlaceLandmarks(ChunkState state, PlacementType type)
    {
        PlacementPlan plan = state.Plan;
        ChunkInput input = state.Input;
        LimitRules limits = type.Def.limits;
        float size = Mathf.Max(16f, limits.regionSize);
        float margin = type.Margin;

        var picks = new List<LandmarkPick>();
        if (limits.unique)
        {
            LandmarkPick unique = UniquePick(plan, type);
            if (unique != null)
                picks.Add(unique);
        }
        else
        {
            int rx0 = Mathf.FloorToInt((input.OriginX - margin) / size), rx1 = Mathf.FloorToInt((input.OriginX + input.Span + margin) / size);
            int rz0 = Mathf.FloorToInt((input.OriginY - margin) / size), rz1 = Mathf.FloorToInt((input.OriginY + input.Span + margin) / size);
            for (int rz = rz0; rz <= rz1; rz++)
                for (int rx = rx0; rx <= rx1; rx++)
                    picks.AddRange(FinalPicks(plan, type, rx, rz));
        }

        picks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        foreach (LandmarkPick pick in picks)
        {
            if (!state.Owns(pick.X, pick.Z))
            {
                // Possibly placed in a neighbouring chunk: later types must respect it.
                if (pick.X >= input.OriginX - margin && pick.X < input.OriginX + input.Span + margin &&
                    pick.Z >= input.OriginY - margin && pick.Z < input.OriginY + input.Span + margin)
                    state.Potential[type.Index].Add(new Entry { X = pick.X, Z = pick.Z, Type = type.Index, Priority = pick.Priority });
                continue;
            }

            state.Result.Tried[type.Index]++;
            if (state.AcceptedCount[type.Index] >= type.MaxPerChunk)
            {
                state.Reject(Stage.Limits);
                continue;
            }

            var c = new Candidate
            {
                X = pick.X,
                Z = pick.Z,
                Priority = pick.Priority,
                CellX = Mathf.FloorToInt(pick.X * 16f),
                CellY = Mathf.FloorToInt(pick.Z * 16f),
                Biome = -1,
            };
            Biome biome = state.Env.BiomeAtCell(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Z));
            if (biome == null || !plan.BiomeIndex.TryGetValue(biome, out int biomeIndex) || !type.AllowedBiome[biomeIndex])
            {
                state.Reject(Stage.Biome);
                continue;
            }
            c.Biome = biomeIndex;

            // The spot was chosen from the world's coarse shape; confirm it with this chunk's exact terrain.
            if (!EvaluateSite(state, type, ref c))
                continue;
            if (!RelationsAllow(state, type, c.X, c.Z))
            {
                state.Reject(Stage.Relations);
                continue;
            }
            Accept(state, type, c, null);
        }
    }

    /// <summary>A region's picks after world-wide spacing: a pick is dropped for a higher-priority pick of a nearby region that is too close.</summary>
    private static List<LandmarkPick> FinalPicks(PlacementPlan plan, PlacementType type, int rx, int rz)
    {
        List<LandmarkPick> own = RawPicks(plan, type, rx, rz);
        float minDistance = type.Def.limits.minDistanceBetween;
        if (minDistance <= 0f || own.Count == 0)
            return own;

        float size = Mathf.Max(16f, type.Def.limits.regionSize);
        int reach = Mathf.CeilToInt(minDistance / size);
        var kept = new List<LandmarkPick>(own.Count);
        foreach (LandmarkPick pick in own)
        {
            bool dropped = false;
            for (int dz = -reach; dz <= reach && !dropped; dz++)
            {
                for (int dx = -reach; dx <= reach && !dropped; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;
                    foreach (LandmarkPick other in RawPicks(plan, type, rx + dx, rz + dz))
                    {
                        float ddx = other.X - pick.X, ddz = other.Z - pick.Z;
                        if (ddx * ddx + ddz * ddz < minDistance * minDistance && other.Priority > pick.Priority)
                        {
                            dropped = true;
                            break;
                        }
                    }
                }
            }
            if (!dropped)
                kept.Add(pick);
        }
        return kept;
    }

    /// <summary>
    /// A region's landmark spots: its candidate spots are judged by the world's coarse shape (biome, height,
    /// slope, water, climate, all before erosion), and for each slot (Max Per Region) the chance roll decides
    /// whether one is placed - always when Guaranteed - at the best-scoring spot left, keeping Min Distance Between.
    /// </summary>
    private static List<LandmarkPick> RawPicks(PlacementPlan plan, PlacementType type, int rx, int rz)
    {
        var key = new RegionKey { Seed = type.Seed, Salt = type.Salt, X = rx, Z = rz };
        Lazy<List<LandmarkPick>> lazy = RegionPicks.GetOrAdd(key, k => new Lazy<List<LandmarkPick>>(
            () => ComputeRawPicks(plan, type, rx, rz, type.Def.limits.guaranteed), LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private static List<LandmarkPick> ComputeRawPicks(PlacementPlan plan, PlacementType type, int rx, int rz, bool guaranteed)
    {
        LimitRules limits = type.Def.limits;
        float size = Mathf.Max(16f, limits.regionSize);
        int attempts = Mathf.Clamp(limits.candidatesPerRegion, 1, 1024);
        int regionSalt = type.Salt ^ 0x51ED270B;

        var passing = new List<LandmarkPick>();
        var gaps = new List<KeyValuePair<Biome, float>>(8);
        for (int k = 0; k < attempts; k++)
        {
            float x = (rx + PlacementRandom.Value(type.Seed, regionSalt, rx * 1024 + k, rz, 0)) * size;
            float z = (rz + PlacementRandom.Value(type.Seed, regionSalt, rx * 1024 + k, rz, 1)) * size;
            if (CoarseSite(plan, type, x, z, gaps, out float score))
                passing.Add(new LandmarkPick { X = x, Z = z, Score = score, Priority = PlacementRandom.Value(type.Seed, regionSalt, rx * 1024 + k, rz, 2) });
        }

        var picks = new List<LandmarkPick>();
        if (passing.Count == 0)
            return picks;
        passing.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : b.Priority.CompareTo(a.Priority));

        int slots = Mathf.Max(1, limits.maxPerRegion);
        float chance = Mathf.Clamp01(type.Def.probabilityToSpawn * 0.01f);
        float minDistance = limits.minDistanceBetween;
        for (int slot = 0; slot < slots; slot++)
        {
            if (!guaranteed && PlacementRandom.Value(type.Seed, regionSalt, rx, rz, 100 + slot) >= chance)
                continue;
            foreach (LandmarkPick candidate in passing)
            {
                if (picks.Contains(candidate))
                    continue;
                bool tooClose = false;
                foreach (LandmarkPick placed in picks)
                {
                    float dx = placed.X - candidate.X, dz = placed.Z - candidate.Z;
                    if (dx * dx + dz * dz < Mathf.Max(minDistance, type.HardSpacing) * Mathf.Max(minDistance, type.HardSpacing))
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;
                picks.Add(candidate);
                break;
            }
        }
        return picks;
    }

    /// <summary>The one spot of a unique landmark: the best pick of the nearest region (in a spiral from the search centre) that has one.</summary>
    private static LandmarkPick UniquePick(PlacementPlan plan, PlacementType type)
    {
        var key = new RegionKey { Seed = type.Seed, Salt = type.Salt, X = int.MinValue, Z = int.MinValue };
        Lazy<LandmarkPick> lazy = UniquePicks.GetOrAdd(key, k => new Lazy<LandmarkPick>(() =>
        {
            LimitRules limits = type.Def.limits;
            float size = Mathf.Max(16f, limits.regionSize);
            int cx = Mathf.FloorToInt(limits.uniqueSearchCenter.x / size), cz = Mathf.FloorToInt(limits.uniqueSearchCenter.y / size);
            int rings = Mathf.Max(0, Mathf.CeilToInt(limits.uniqueSearchRadius / size));
            for (int ring = 0; ring <= rings; ring++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring)
                            continue;
                        List<LandmarkPick> picks = ComputeRawPicks(plan, type, cx + dx, cz + dz, true);
                        if (picks.Count > 0)
                            return picks[0];
                    }
                }
            }
            return null;
        }, LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    /// <summary>
    /// A landmark spot judged by the world's coarse shape only (no chunk data needed, so any chunk can compute
    /// any region the same way): biome and borders, height and slope before erosion, sea and lakes, climate,
    /// and the land's shape. Returns the soft-preference score.
    /// </summary>
    private static bool CoarseSite(PlacementPlan plan, PlacementType type, float x, float z, List<KeyValuePair<Biome, float>> gaps, out float score)
    {
        score = 0f;
        BiomeObject def = type.Def;
        TerrainHeightSampler sampler = plan.Sampler;

        Biome biome = sampler.SampleBiome(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
        if (biome == null || !plan.BiomeIndex.TryGetValue(biome, out int biomeIndex) || !type.AllowedBiome[biomeIndex])
            return false;
        float soft = 1f;
        if (type.UsesBorders && biome.placement == BiomePlacement.Land)
        {
            sampler.GetBiomeGaps(x, z, gaps);
            float fade = BorderFactor(plan, type, gaps, biomeIndex);
            if (fade <= 0f)
                return false;
            soft *= fade;
        }

        const float d = 2f;
        float h = sampler.SampleBaseHeight(x, z);
        float hx = sampler.SampleBaseHeight(x + d, z) - sampler.SampleBaseHeight(x - d, z);
        float hz = sampler.SampleBaseHeight(x, z + d) - sampler.SampleBaseHeight(x, z - d);
        float slope = Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) / (2f * d)) * Mathf.Rad2Deg;
        // A little margin, so the exact (eroded) terrain rarely fails what the coarse shape passed.
        if (slope > type.MaxSlope - 2f || slope < type.MinSlope + 2f * (type.MinSlope > 0f ? 1f : 0f) || !SurfaceAllowed(def.slope.surfaces, slope))
            return false;
        if (def.slope.usePreferredSlope)
            soft *= RangePreference(slope, def.slope.preferredMinSlope, def.slope.preferredMaxSlope, Mathf.Max(0.1f, def.slope.slopeFalloff));

        AltitudeRules altitude = def.altitude;
        if (altitude.limitAltitude && (h < altitude.minAltitude || h > altitude.maxAltitude))
            return false;
        if (def.useCustomHeightPreference)
        {
            float range = Mathf.Max(0.01f, def.preferredMaxHeight - def.preferredMinHeight);
            soft *= Gaussian((h - def.preferredOptimalHeight) / (range * 0.5f), def.heightPreferenceStrength);
        }

        // Water: the sea and lakes (rivers are narrow; the chunk's exact check catches them).
        bool wet = false;
        WaterSettings water = plan.Water;
        if (water != null)
        {
            if (water.OceansEnabled && h < water.SeaLevel && OceanGenerator.LandSide(water, x, z) < 0f)
                wet = true;
            else if (LakeGenerator.FindLakeContaining(new Vector2(x, z), null, water, sampler, LakeFeature.InnerFraction) != null)
                wet = true;
        }
        WaterPlacement mode = def.water.placement;
        if (mode == WaterPlacement.InWater ? !wet : (wet && mode != WaterPlacement.Anywhere))
            return false;

        if (altitude.terrainPosition != TerrainPosition.Any)
        {
            // Relief from a ring of samples at the relief radius.
            float radius = Mathf.Max(2f, altitude.reliefRadius);
            float mean = 0f, meanSquares = 0f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float sample = sampler.SampleBaseHeight(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius);
                mean += sample;
                meanSquares += sample * sample;
            }
            mean /= 8f;
            float roughness = Mathf.Sqrt(Mathf.Max(0f, meanSquares / 8f - mean * mean));
            float fit = PositionFit(altitude.terrainPosition, h - mean, roughness, slope);
            if (altitude.terrainPositionRequired && fit < 0.5f)
                return false;
            soft *= altitude.terrainPositionRequired ? fit : Mathf.Lerp(1f, fit, altitude.positionStrength);
        }

        ClimateRules climate = def.climate;
        if (climate.useMoisture || climate.useTemperature)
        {
            Vector2 p = new Vector2(x, z);
            float temperature = ClimateGenerator.GetTemperature(p, plan.VoronoiSeed, plan.ClimateNoiseScale);
            float moisture = ClimateGenerator.GetMoisture(p, plan.VoronoiSeed, plan.ClimateNoiseScale);
            if (plan.Climate != null)
                plan.Climate.Apply(p, ref temperature, ref moisture);
            if (climate.useMoisture)
                soft *= RangeAllowance(moisture, climate.minMoisture, climate.maxMoisture, climate.softness);
            if (climate.useTemperature)
                soft *= RangeAllowance(temperature, climate.minTemperature, climate.maxTemperature, climate.softness);
        }

        score = soft;
        return soft > 0f;
    }
}
