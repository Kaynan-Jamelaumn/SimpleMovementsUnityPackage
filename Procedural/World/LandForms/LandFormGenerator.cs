using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Landform terrain. Every function here depends only on world position, the world seed and settings -
/// never on the chunk being generated - so features continue seamlessly across chunk borders and the
/// same seed always produces the same terrain. Noise offsets are keyed by landform type and seed (not
/// by biome), so two neighboring biomes with the same landform share the same underlying terrain and
/// their border is invisible in the ground itself.
///
/// Each landform returns RELIEF: height above (or, for low landforms, slightly below) the biome's base
/// elevation. <see cref="LandHeight"/> blends base elevations and reliefs separately at biome borders:
/// base elevation over the wide, slope-limited blend band, and relief over a narrower, wavering band -
/// so a mountain range sinks into foothills before its border instead of being averaged with the plain
/// next to it, and the edge of the relief doesn't trace the straight Voronoi cell outline.
/// </summary>
public static partial class LandformGenerator
{
    // ------------------------------------------------------------------ landform choice

    /// <summary>The landform a biome actually uses under the given mode.</summary>
    public static LandformType Effective(Biome biome, TerrainShapeMode mode)
    {
        if (biome == null || mode == TerrainShapeMode.ClassicOnly)
            return LandformType.Classic;
        if (biome.landform != LandformType.Classic || mode == TerrainShapeMode.PerBiome)
            return biome.landform;
        return Suggest(biome);
    }

    /// <summary>
    /// Suggested landform for a biome from its existing settings: hot and dry = Dunes, very wet and flat =
    /// Wetland, tall = Mountains, moderately rough = Hills, otherwise Plains.
    /// </summary>
    public static LandformType Suggest(Biome biome)
    {
        if (biome.placement == BiomePlacement.Ocean)
            return LandformType.SeaPlain;
        if (biome.idealMoisture <= 0.2f && biome.idealTemperature >= 0.7f)
            return LandformType.Dunes;
        if (biome.idealMoisture >= 0.8f && biome.amplitude <= 5f)
            return LandformType.Wetland;
        if (biome.amplitude >= 35f)
            return biome.idealTemperature <= 0.2f ? LandformType.Glacial : LandformType.Mountains;
        if (biome.amplitude >= 18f)
            return LandformType.Highlands;
        if (biome.amplitude >= 9f)
            return LandformType.Hills;
        return LandformType.Plains;
    }

    /// <summary>Typical relief height (world units) of a biome's landform, used to size transitions.</summary>
    public static float TypicalRelief(LandformType landform, Biome biome, int classicOctaves)
    {
        float a = Mathf.Abs(biome.amplitude);
        switch (landform)
        {
            case LandformType.Mountains: return 1.3f * a;
            case LandformType.Glacial: return 1.3f * a;
            case LandformType.Plateau: return 1.2f * a;
            case LandformType.Highlands: return 1.2f * a;
            case LandformType.SeaPlain:
            case LandformType.SeaRavines:
            case LandformType.SeaReef:
            case LandformType.SeaRocky: return 0.5f * a;
            case LandformType.Hills: return 0.9f * a;
            case LandformType.Dunes: return 0.7f * a;
            case LandformType.Plains: return 0.5f * a;
            case LandformType.Wetland: return 0.4f * a;
            default: return 0.5f * biome.EstimateMaxHeightAmplitude(classicOctaves);
        }
    }

    // ------------------------------------------------------------------ blending

    /// <summary>
    /// Land height at a world position from a biome blend. When every contributing biome is Classic this
    /// is exactly the original formula. Otherwise base elevations blend with the normal weights, Classic
    /// biomes keep their original weighted terrain, and landform relief uses its own transition weights
    /// (see the class summary). <paramref name="relief"/> is the height minus the blended base elevation.
    /// </summary>
    public static float LandHeight(List<VoronoiBiomeGenerator.BiomeWeight> blend, float x, float y, LandformSettings s, out float relief)
    {
        int count = blend.Count;
        bool anyLandform = false;
        for (int i = 0; i < count && !anyLandform; i++)
            anyLandform = Effective(blend[i].Biome, s.Mode) != LandformType.Classic;

        if (!anyLandform)
            return HeightGenerator.LandHeightFromBlend(blend, x, y, s.ClassicOctaves, s.ClassicLacunarity, 1f / s.ChunkWidth, s, out relief);

        // Relief transition weights. A biome's relief is at full strength until its gap (how much farther
        // its nearest point is than the nearest point overall) nears the relief band, and gone past it.
        // Neighbors fade over the SAME distance (the wider of their two wanted bands, eased in by how near
        // the neighbor is), so one side never drops away much faster than the other. Each band is kept
        // inside the biome's own blend band, so relief is always gone by the time the biome's weight is -
        // that keeps the height continuous everywhere.
        float[] wanted = Scratch(ref scratchWanted, count);
        float[] presence = Scratch(ref scratchPresence, count);
        for (int i = 0; i < count; i++)
        {
            Biome biome = blend[i].Biome;
            wanted[i] = Mathf.Max(s.TransitionWidth * s.PointSpacing,
                1.333f * TypicalRelief(Effective(biome, s.Mode), biome, s.ClassicOctaves) / Mathf.Max(0.05f, s.TransitionSlopeTangent));
            presence[i] = Presence(blend[i].SmoothGap, s);
        }

        float[] reliefWeights = Scratch(ref scratchReliefWeights, count);
        float reliefTotal = 0f;
        for (int i = 0; i < count; i++)
        {
            float bandWanted = wanted[i];
            for (int j = 0; j < count; j++)
            {
                if (j != i)
                    bandWanted = Mathf.Max(bandWanted, Mathf.Lerp(wanted[i], wanted[j], presence[j]));
            }

            // Kept inside the biome's own blend band, so the relief fades out alongside its weight.
            float band = Mathf.Max(1f, Mathf.Min(bandWanted, blend[i].Band / 1.3f));

            // Wavering edge: each biome's relief edge is shifted by its own slow noise, so where two
            // landforms meet the line wanders instead of following the straight cell outline.
            float wobble = Noise(x / (0.7f * s.PointSpacing), y / (0.7f * s.PointSpacing), s.StableNameHash(blend[i].Biome), 7) * 0.3f * band;
            // The gate makes the relief vanish exactly as the biome's weight does (and stay 0 for nearby
            // biomes that aren't blending in yet), which keeps the height continuous everywhere.
            float gate = SmoothStep(0f, 0.03f, blend[i].Weight);
            reliefWeights[i] = gate * Falloff(Mathf.Max(0f, blend[i].SmoothGap + wobble) / band);
            reliefTotal += reliefWeights[i];
        }

        float height = 0f;
        float baseElevation = 0f;
        for (int i = 0; i < count; i++)
        {
            Biome biome = blend[i].Biome;
            float weight = blend[i].Weight;
            LandformType landform = Effective(biome, s.Mode);
            baseElevation += weight * biome.baseElevation;

            if (landform == LandformType.Classic)
            {
                if (weight <= 0f)
                    continue;
                height += weight * HeightGenerator.ComputeClassicBiomeNoise(biome, s.ClassicPhase(biome), x, y, s.ClassicOctaves, s.ClassicLacunarity, 1f / s.ChunkWidth);
                continue;
            }

            height += weight * biome.baseElevation;
            float share = reliefTotal > 0f ? reliefWeights[i] / reliefTotal : 0f;
            if (share <= 0f)
                continue;

            float landformRelief = Relief(landform, biome, x, y, s);
            height += share * LimitToFront(landformRelief, landform, biome, blend, i, s);
        }

        relief = height - baseElevation;
        return height;
    }

    // Per-thread scratch arrays for LandHeight (called for every terrain cell, on several threads at once).
    [System.ThreadStatic] private static float[] scratchWanted;
    [System.ThreadStatic] private static float[] scratchPresence;
    [System.ThreadStatic] private static float[] scratchReliefWeights;

    private static float[] Scratch(ref float[] buffer, int size)
    {
        if (buffer == null || buffer.Length < size)
            buffer = new float[Mathf.Max(size, 16)];
        return buffer;
    }

    /// <summary>
    /// How strongly a nearby biome counts as a neighbor here: 1 while it is close (blending in or about
    /// to), easing to 0 toward the edge of the nearby-biome reach, so a neighbor's influence on landform
    /// borders fades in gradually instead of starting abruptly.
    /// </summary>
    private static float Presence(float smoothGap, LandformSettings s)
    {
        float reach = Mathf.Max(1f, s.NearbyReach);
        return 1f - SmoothStep(0.5f * reach, reach, smoothGap);
    }

    /// <summary>
    /// Mountain-front envelope. Near a border with a DIFFERENT landform, the part of a landform's relief
    /// that stands above the neighbor's own typical height is scaled down - by how far the border is,
    /// climbing at most about the front slope - so a mountain comes down into foothills at the level of
    /// the land next to it (never below it: nothing here digs a ditch along the border), keeping its
    /// ridges and valleys at a smaller size. A landform lower than its neighbor is left alone. Each
    /// neighbor's influence is weighted by <see cref="Presence"/>, so it fades in smoothly. Deep inside
    /// a territory this changes nothing.
    /// </summary>
    private static float LimitToFront(float relief, LandformType landform, Biome biome, List<VoronoiBiomeGenerator.BiomeWeight> blend, int index, LandformSettings s)
    {
        float typical = TypicalRelief(landform, biome, s.ClassicOctaves);
        // Mountains and plateaus are meant to be hard to cross: their front may rise more steeply than
        // walkable ground (valleys reaching the edge still give walkable ways in).
        float frontSlope = landform == LandformType.Mountains || landform == LandformType.Plateau || landform == LandformType.Glacial
            ? Mathf.Max(s.TransitionSlopeTangent, MountainFrontSlope)
            : s.TransitionSlopeTangent;
        float foothill = Mathf.Max(2f, 0.12f * s.PointSpacing);

        float floor = 0f;
        float scale = 1f;
        for (int j = 0; j < blend.Count; j++)
        {
            Biome other = blend[j].Biome;
            LandformType otherLandform = Effective(other, s.Mode);
            if (j == index || otherLandform == landform)
                continue;
            float presence = Presence(blend[j].SmoothGap, s);
            if (presence <= 0f)
                continue;

            floor = Mathf.Max(floor, presence * TypicalRelief(otherLandform, other, s.ClassicOctaves));

            // The gap grows by up to 2 per world unit moved straight across a border, so half the gap
            // difference is a safe (never overestimated) distance to it.
            float distance = 0.5f * (blend[j].SmoothGap - blend[index].SmoothGap);

            // Soft ramp: level at the border, steepening to frontSlope within a short foothill distance,
            // fading smoothly past the border.
            float z = distance / foothill;
            float softplus = z > 20f ? z : Mathf.Log(1f + Mathf.Exp(z));
            float envelope = frontSlope * foothill * softplus;

            // Scale reaches 1 once the envelope is about as high as the landform's typical relief.
            float t = envelope / Mathf.Max(0.01f, 0.9f * typical);
            float t4 = t * t * t * t;
            float pairScale = Mathf.Min(1f, 1.07f * t / Mathf.Sqrt(Mathf.Sqrt(1f + t4)));
            scale = Mathf.Min(scale, Mathf.Lerp(1f, pairScale, presence));
        }

        if (relief <= floor || scale >= 1f)
            return relief;
        return floor + (relief - floor) * scale;
    }

    /// <summary>A landform's relief (world units above the biome's base elevation) at a world position.</summary>
    public static float Relief(LandformType landform, Biome biome, float x, float y, LandformSettings s)
    {
        float wavelength = s.ChunkWidth / Mathf.Max(0.05f, Mathf.Abs(biome.frequency));
        float amplitude = biome.amplitude;
        float roughness = Mathf.Clamp(biome.persistence, 0.2f, 0.7f);
        int seed = s.Seed;

        switch (landform)
        {
            case LandformType.Mountains: return Mountains(x, y, wavelength, amplitude, roughness, seed);
            case LandformType.Hills: return Hills(x, y, wavelength, amplitude, roughness, seed);
            case LandformType.Plains: return Plains(x, y, wavelength, amplitude, seed);
            case LandformType.Dunes: return Dunes(x, y, wavelength, amplitude, seed);
            case LandformType.Wetland: return Wetland(x, y, wavelength, amplitude, seed);
            case LandformType.Plateau: return Plateau(x, y, wavelength, amplitude, roughness, seed);
            case LandformType.Highlands: return Highlands(x, y, wavelength, amplitude, roughness, seed);
            case LandformType.Glacial: return Glacial(x, y, wavelength, amplitude, roughness, seed);
            case LandformType.SeaPlain: return SeaPlain(x, y, wavelength, amplitude, seed);
            case LandformType.SeaRavines: return SeaRavines(x, y, wavelength, amplitude, seed);
            case LandformType.SeaReef: return SeaReef(x, y, wavelength, amplitude, seed);
            case LandformType.SeaRocky: return SeaRocky(x, y, wavelength, amplitude, roughness, seed);
            default: return 0f;
        }
    }

    // ------------------------------------------------------------------ placement

    /// <summary>
    /// 0-1 field of long, connected belts (lines where a slow, bent noise field crosses zero). Mountain
    /// landforms are favored on belts, lowland landforms away from them, so mountain territories line up
    /// into ranges several chunks long instead of scattered blobs.
    /// </summary>
    public static float MountainBelt(Vector2 position, int seed, float beltScale)
    {
        float inv = 1f / Mathf.Max(1f, beltScale);
        float warpX = Fbm(position.x * inv, position.y * inv, Key(seed, 50, 1), 2, 0.5f);
        float warpY = Fbm(position.x * inv, position.y * inv, Key(seed, 50, 2), 2, 0.5f);
        float qx = position.x * inv + warpX * 0.4f;
        float qy = position.y * inv + warpY * 0.4f;
        float line = 1f - Mathf.Abs(Fbm(qx, qy, Key(seed, 50, 3), 2, 0.45f)) * 2.2f;
        return Mathf.Clamp01(line) * Mathf.Clamp01(line);
    }

    /// <summary>Biome selection multiplier for a landform at a given belt value.</summary>
    public static float PlacementAffinity(LandformType landform, float belt, float strength)
    {
        float affinity;
        switch (landform)
        {
            case LandformType.Mountains:
            case LandformType.Glacial:
            case LandformType.Plateau:
                affinity = 0.1f + 3f * belt;
                break;
            case LandformType.Hills:
            case LandformType.Highlands:
                affinity = 0.6f + 1.2f * Mathf.Sqrt(belt);
                break;
            case LandformType.Plains:
            case LandformType.Wetland:
            case LandformType.Dunes:
                affinity = 1.2f - 0.9f * belt;
                break;
            default:
                return 1f;
        }
        return Mathf.Lerp(1f, affinity, Mathf.Clamp01(strength));
    }
}
