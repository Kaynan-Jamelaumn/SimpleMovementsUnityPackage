using System.Collections.Generic;
using UnityEngine;

/// <summary>Snapshot of the settings landform terrain needs, taken once per chunk.</summary>
public sealed class LandformSettings
{
    public TerrainShapeMode Mode;
    public int Seed;
    /// <summary>Chunk size in cells; biome frequency is "features per chunk width", as for Classic terrain.</summary>
    public float ChunkWidth;
    /// <summary>Typical distance between Voronoi biome points (world units).</summary>
    public float PointSpacing;
    /// <summary>Fraction of <see cref="PointSpacing"/> over which a landform's relief fades out at a biome border.</summary>
    public float TransitionWidth;
    /// <summary>tan() of the steepest slope a relief fade may cause on its own.</summary>
    public float TransitionSlopeTangent;
    public int ClassicOctaves;
    public float ClassicLacunarity;
    /// <summary>How far (smoothed gap, world units) nearby biomes are listed before they blend in - see <see cref="NearbyReachFor"/>.</summary>
    public float NearbyReach;

    public bool VolcanoesEnabled;
    public float VolcanoSpacing;
    public float VolcanoChance;
    public float VolcanoMinRadius;
    public float VolcanoMaxRadius;
    public float VolcanoMinHeight;
    public float VolcanoMaxHeight;
    public float CalderaChance;

    // Values derived from a biome's name, computed once per biome for this snapshot (a chunk's generation)
    // instead of reading the name - a native call that allocates a new string in Unity - for every cell.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Biome, int> stableNameHashes =
        new System.Collections.Concurrent.ConcurrentDictionary<Biome, int>();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Biome, float> classicPhases =
        new System.Collections.Concurrent.ConcurrentDictionary<Biome, float>();

    private static readonly System.Func<Biome, int> StableNameHashFactory = LandformGenerator.StableNameHash;
    private static readonly System.Func<Biome, float> ClassicPhaseFactory = HeightGenerator.ClassicPhaseOffset;

    /// <summary>LandformGenerator.StableHash of the biome's name.</summary>
    public int StableNameHash(Biome biome)
    {
        if (biome == null)
            return LandformGenerator.StableNameHash(biome);   // throws exactly as reading the name always did
        return stableNameHashes.TryGetValue(biome, out int hash) ? hash : stableNameHashes.GetOrAdd(biome, StableNameHashFactory);
    }

    /// <summary>The Classic terrain noise phase offset of a biome (see HeightGenerator.ClassicPhaseOffset).</summary>
    public float ClassicPhase(Biome biome)
    {
        if (biome == null)
            return HeightGenerator.ClassicPhaseOffset(biome);
        return classicPhases.TryGetValue(biome, out float phase) ? phase : classicPhases.GetOrAdd(biome, ClassicPhaseFactory);
    }

    /// <summary>
    /// Reach used for <see cref="VoronoiBiomeGenerator.LayoutOptions.NearbyReach"/>: about 1.5 biome-point
    /// spacings, so landform borders start adjusting toward a neighbor before its weight begins. Capped at
    /// half the Voronoi cell size: biome points are looked up in the 3x3 cells around a position, and a
    /// longer reach could list a biome on one side of a cell line and miss it on the other.
    /// </summary>
    public static float NearbyReachFor(TerrainGenerator tg)
    {
        float spacing = tg.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, tg.NumVoronoiPoints));
        return Mathf.Min(1.5f * spacing, 0.5f * tg.VoronoiScale);
    }

    public static LandformSettings From(TerrainGenerator tg)
    {
        float boundarySlope = tg.BiomeBoundaryMaxSlopeTangent;
        return new LandformSettings
        {
            Mode = tg.TerrainShapeMode,
            Seed = tg.VoronoiSeed,
            ChunkWidth = tg.ChunkSize,
            PointSpacing = tg.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, tg.NumVoronoiPoints)),
            TransitionWidth = tg.LandformTransitionWidth,
            // With the boundary slope limit off, still keep relief fades themselves under ~40 degrees.
            TransitionSlopeTangent = boundarySlope > 0f ? boundarySlope : Mathf.Tan(40f * Mathf.Deg2Rad),
            ClassicOctaves = tg.Octaves,
            ClassicLacunarity = tg.Lacunarity,
            NearbyReach = NearbyReachFor(tg),

            VolcanoesEnabled = tg.EnableVolcanoes && tg.VolcanoChance > 0f,
            VolcanoSpacing = Mathf.Max(500f, tg.VolcanoSpacing),
            VolcanoChance = Mathf.Clamp01(tg.VolcanoChance),
            VolcanoMinRadius = Mathf.Max(50f, tg.VolcanoMinRadius),
            VolcanoMaxRadius = Mathf.Max(Mathf.Max(50f, tg.VolcanoMinRadius), tg.VolcanoMaxRadius),
            VolcanoMinHeight = Mathf.Max(5f, tg.VolcanoMinHeight),
            VolcanoMaxHeight = Mathf.Max(Mathf.Max(5f, tg.VolcanoMinHeight), tg.VolcanoMaxHeight),
            CalderaChance = Mathf.Clamp01(tg.CalderaChance),
        };
    }
}
