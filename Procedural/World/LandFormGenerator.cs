using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shape of the ground a biome sits on. A biome describes an environment (textures, climate,
/// objects, water); its landform decides what the terrain there actually looks like.
/// </summary>
public enum LandformType
{
    /// <summary>The original terrain: layered smooth noise from the biome's amplitude/frequency/persistence.</summary>
    Classic = 0,
    /// <summary>Broad, low undulation with occasional shallow basins.</summary>
    Plains = 1,
    /// <summary>Rounded, rolling hills with gentle slopes and broad lows between them.</summary>
    Hills = 2,
    /// <summary>Mountain ranges: connected peaks and ridges of varied height and shape, separated by valleys.</summary>
    Mountains = 3,
    /// <summary>Wind-aligned sand dunes (gentle windward side, steep lee side) in fields separated by flat pans.</summary>
    Dunes = 4,
    /// <summary>Flat, low ground with hummocks and shallow hollows.</summary>
    Wetland = 5,
    /// <summary>Flat-topped tablelands with stepped cliffs, cut by narrow canyons.</summary>
    Plateau = 6,
    /// <summary>Rugged uplands: big rolling relief broken by rock ledges and ravines you have to walk around (e.g. forests).</summary>
    Highlands = 7,
    /// <summary>High mountain terrain carved by glaciers: broad, flat-floored U-shaped valleys with steep walls and hanging side valleys.</summary>
    Glacial = 8,
    /// <summary>Ocean biomes: deep, gently rolling seafloor with scattered seamounts.</summary>
    SeaPlain = 9,
    /// <summary>Ocean biomes: seafloor cut by deep submarine ravines and canyons.</summary>
    SeaRavines = 10,
    /// <summary>Ocean biomes: shallow reef banks and atoll rings rising to just below the surface, with lagoons.</summary>
    SeaReef = 11,
    /// <summary>Ocean biomes: rough rocky seabed with ledges and boulder fields.</summary>
    SeaRocky = 12,
}

/// <summary>Where a biome may be placed (see <see cref="Biome.placement"/>).</summary>
public enum BiomePlacement
{
    /// <summary>A normal land biome, placed by the Voronoi biome layout.</summary>
    Land = 0,
    /// <summary>Only used on the ocean floor (textures, objects, and a seafloor landform). Needs Oceans enabled.</summary>
    Ocean = 1,
    /// <summary>Only used on volcanoes: painted over the volcano's cone, caldera and lava fields.</summary>
    Volcanic = 2,
}

/// <summary>How the terrain generator decides each biome's landform (see <see cref="TerrainGenerator"/>).</summary>
public enum TerrainShapeMode
{
    /// <summary>Every biome uses the original Classic terrain; landform settings are ignored.</summary>
    ClassicOnly = 0,
    /// <summary>Each biome uses its own Landform setting (Classic keeps the original terrain for that biome).</summary>
    PerBiome = 1,
    /// <summary>Every biome uses a landform; biomes still set to Classic get a suggested one.</summary>
    LandformsOnly = 2,
}

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
public static class LandformGenerator
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
            return HeightGenerator.LandHeightFromBlend(blend, x, y, s.ClassicOctaves, s.ClassicLacunarity, 1f / s.ChunkWidth, out relief);

        // Relief transition weights. A biome's relief is at full strength until its gap (how much farther
        // its nearest point is than the nearest point overall) nears the relief band, and gone past it.
        // Neighbors fade over the SAME distance (the wider of their two wanted bands, eased in by how near
        // the neighbor is), so one side never drops away much faster than the other. Each band is kept
        // inside the biome's own blend band, so relief is always gone by the time the biome's weight is -
        // that keeps the height continuous everywhere.
        float[] wanted = new float[count];
        float[] presence = new float[count];
        for (int i = 0; i < count; i++)
        {
            Biome biome = blend[i].Biome;
            wanted[i] = Mathf.Max(s.TransitionWidth * s.PointSpacing,
                1.333f * TypicalRelief(Effective(biome, s.Mode), biome, s.ClassicOctaves) / Mathf.Max(0.05f, s.TransitionSlopeTangent));
            presence[i] = Presence(blend[i].SmoothGap, s);
        }

        float[] reliefWeights = new float[count];
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
            float wobble = Noise(x / (0.7f * s.PointSpacing), y / (0.7f * s.PointSpacing), StableHash(blend[i].Biome.name), 7) * 0.3f * band;
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
                height += weight * HeightGenerator.ComputeClassicBiomeNoise(biome, x, y, s.ClassicOctaves, s.ClassicLacunarity, 1f / s.ChunkWidth);
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

    // ------------------------------------------------------------------ landforms

    /// <summary>
    /// Mountain ranges, built at three scales that shape each other:
    ///  - Large: range crest lines (where a slow, bent noise field crosses zero) with broad valleys
    ///    between ranges, and a very slow "uplift" field so some ranges stand higher than others.
    ///  - Medium: ridged layers at the biome's feature size - peaks joined by saddles and spurs, V-shaped
    ///    valleys between them. Each layer is weighted by the one below, so detail gathers on ridges and
    ///    peaks while valley floors stay smoother. A slow "sharpness" field mixes sharp crests with broad,
    ///    rounded summits, and bending (domain warping) makes ridges curve and peaks lopsided.
    ///  - Small: rock detail, stronger on high ground.
    /// </summary>
    // Mountain shaping constants, tuned against slope statistics (median ~30 degrees, ~20% of faces steeper
    // than 50) and peak counts on typical mountain biome settings (amplitude 60, frequency 1.5).
    private const float PeakSpacing = 1.4f;          // ridge-noise wavelength, in biome feature sizes
    private const float LayerFalloff = 0.5f;         // extra per-layer falloff on top of persistence (keeps fine layers from making cliffs)
    private const float MountainHeight = 2.5f;       // relief scale, in biome amplitudes
    private const float PeakContrast = 1.2f;         // >1 deepens valleys and narrows summits
    private const float PeakGain = 1.3f;
    private const float RangeSpacing = 3.2f;         // range-line wavelength, in peak spacings
    private const float SmallestFeature = 6f;        // world units
    private const float RidgeFeedback = 2f;          // how strongly each layer follows the ridges of the one below
    private const float CrestSharpness = 2.3f;
    private const float RangeNarrow = 1.5f;          // how narrow range crest lines are (higher = narrower ranges, wider valleys)
    private const float MountainFrontSlope = 1.19f;  // tan(50 degrees): steepest a mountain front may rise from its border

    private static float Mountains(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.Mountains;
        float inv = 1f / wavelength;
        float peakInv = inv / PeakSpacing;

        // Bend the whole field so ridges curve and peaks are asymmetric.
        float warpX = Fbm(x * peakInv * 0.5f, y * peakInv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * peakInv * 0.5f, y * peakInv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.45f / peakInv;
        float qy = y + warpY * 0.45f / peakInv;

        // Large scale: range crest lines and the valleys between ranges.
        float rangeInv = peakInv / RangeSpacing;
        float rangeNoise = Fbm(qx * rangeInv, qy * rangeInv, Key(seed, T, 3), 2, 0.5f);
        float range = 1f - Mathf.Abs(rangeNoise) * RangeNarrow;
        float rangeMask = 0.1f + 0.9f * SmoothStep(0f, 0.8f, range);
        float uplift = 0.55f + 0.45f * Noise01(x * rangeInv / 2.5f, y * rangeInv / 2.5f, Key(seed, T, 4));

        // Medium scale: ridged peaks, sharp or rounded depending on the region.
        float sharpness = SmoothStep(0.3f, 0.7f, Noise01(x * peakInv / 1.5f, y * peakInv / 1.5f, Key(seed, T, 5)));
        int octaves = OctavesFor(1f / peakInv, SmallestFeature);
        float frequency = peakInv;
        float layerAmplitude = 1f;
        float weight = 1f;
        float sum = 0f;
        float norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            float n = Noise(qx * frequency, qy * frequency, Key(seed, T, 10 + o), 0);
            float ridge = 1f - Mathf.Abs(n);
            float crest = o < 2 ? Mathf.Lerp(1f - n * n, ridge, sharpness) : ridge;
            crest = Mathf.Pow(Mathf.Clamp01(crest), CrestSharpness);
            crest *= weight;
            weight = Mathf.Clamp01(crest * RidgeFeedback);
            sum += crest * layerAmplitude;
            norm += layerAmplitude;
            frequency *= 2f;
            layerAmplitude *= roughness * LayerFalloff;
        }
        float peaks = norm > 0f ? sum / norm : 0f;
        peaks = Mathf.Min(1f, Mathf.Pow(peaks, PeakContrast) * PeakGain);

        float relief = amplitude * MountainHeight * uplift * rangeMask * peaks;

        // Small scale: rock detail, stronger higher up.
        float detail = Fbm(x * 0.2f, y * 0.2f, Key(seed, T, 30), 3, 0.5f);
        relief += amplitude * 0.02f * detail * (0.3f + Mathf.Clamp01(peaks * rangeMask * 1.5f));
        return relief;
    }

    /// <summary>
    /// Rolling hills: few, rounded high points with gentle slopes and broad lows between them. Hill size
    /// varies by region so the hills don't all look alike.
    /// </summary>
    private static float Hills(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.Hills;
        float inv = 1f / wavelength;

        float warpX = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.25f * wavelength;
        float qy = y + warpY * 0.25f * wavelength;

        float shape = Fbm01(qx * inv / 1.3f, qy * inv / 1.3f, Key(seed, T, 3), 3, Mathf.Min(roughness, 0.45f));
        // Broad lows between hills, and a smooth rise to rounded summits. The upper edge sits above the
        // noise's range, so summits stay domed instead of being clipped flat.
        float hills = SmoothStep(0.3f, 1.15f, shape);
        float size = 0.55f + 0.45f * Noise01(x * inv / 4.5f, y * inv / 4.5f, Key(seed, T, 4));

        float relief = amplitude * 2.2f * size * hills;
        relief += amplitude * 0.02f * Fbm(x * 0.2f, y * 0.2f, Key(seed, T, 5), 2, 0.5f);
        return relief;
    }

    /// <summary>Plains: long, low swells and occasional shallow basins.</summary>
    private static float Plains(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.Plains;
        float inv = 1f / wavelength;

        float swell = Fbm(x * inv / 2f, y * inv / 2f, Key(seed, T, 1), 2, 0.4f);
        float basin = SmoothStep(0.55f, 0.85f, Fbm01(x * inv / 3.5f, y * inv / 3.5f, Key(seed, T, 2), 2, 0.5f));
        float detail = Fbm(x * inv * 2f, y * inv * 2f, Key(seed, T, 3), 2, 0.5f);
        return amplitude * (0.9f * swell - 0.8f * basin + 0.08f * detail);
    }

    /// <summary>
    /// Dunes: crests across a seed-chosen wind direction, a gentle windward side and a steep lee side,
    /// crests that wander, break and change height, in dune fields separated by flatter sand.
    /// </summary>
    private static float Dunes(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.Dunes;
        float inv = 1f / wavelength;

        float angle = Hash01(seed, T, 99) * Mathf.PI * 2f;
        float dirX = Mathf.Cos(angle), dirY = Mathf.Sin(angle);
        float along = x * dirX + y * dirY;
        float across = -x * dirY + y * dirX;

        float spacing = 0.45f * wavelength;
        float phase = along / spacing
            + 1.2f * Fbm(x * inv / 2f, y * inv / 2f, Key(seed, T, 1), 2, 0.5f)
            + 0.35f * Fbm(x * inv / 0.9f, y * inv / 0.9f, Key(seed, T, 2), 2, 0.5f);
        float t = phase - Mathf.Floor(phase);
        const float windward = 0.72f;
        float wave = t < windward
            ? SmoothStep(0f, 1f, t / windward)
            : 1f - SmoothStep(0f, 1f, (t - windward) / (1f - windward));

        float crest = 0.45f + 0.55f * Fbm01(across * inv / 1.3f, along * inv / 3f, Key(seed, T, 3), 2, 0.5f);
        float field = SmoothStep(0.3f, 0.6f, Fbm01(x * inv / 5f, y * inv / 5f, Key(seed, T, 4), 2, 0.5f));
        float broad = Fbm(x * inv / 3f, y * inv / 3f, Key(seed, T, 5), 2, 0.5f);

        return amplitude * (0.5f * broad + 1.3f * field * crest * wave);
    }

    /// <summary>Wetland: flat, low ground with small hummocks and shallow hollows (where ponds settle).</summary>
    private static float Wetland(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.Wetland;
        float inv = 1f / wavelength;

        float hummocks = Fbm(x * inv * 1.5f, y * inv * 1.5f, Key(seed, T, 1), 3, 0.5f);
        float hollow = SmoothStep(0.5f, 0.8f, Fbm01(x * inv / 1.2f, y * inv / 1.2f, Key(seed, T, 2), 2, 0.5f));
        return amplitude * (0.5f * hummocks - 0.9f * hollow);
    }

    /// <summary>
    /// Plateau: flat-topped tablelands in steps, with steep cliff risers between the steps, cut by narrow
    /// canyons - cliffs that come from the landform itself rather than from a biome border.
    /// </summary>
    private static float Plateau(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.Plateau;
        float inv = 1f / wavelength;

        float warpX = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.3f * wavelength;
        float qy = y + warpY * 0.3f * wavelength;

        const float levels = 3f;
        float table = Fbm01(qx * inv / 2f, qy * inv / 2f, Key(seed, T, 3), 3, 0.5f);
        float stepped = SmoothStep(0.3f, 0.8f, table) * levels;
        float tier = Mathf.Floor(stepped);
        float step = (tier + SmoothStep(0.72f, 0.95f, stepped - tier)) / levels;

        float canyon = Mathf.Pow(1f - Mathf.Abs(Noise(qx * inv / 1.6f, qy * inv / 1.6f, Key(seed, T, 4), 0)), 10f);
        float relief = amplitude * 2f * step * (1f - 0.7f * canyon * step);
        relief += amplitude * 0.03f * Fbm(x * 0.2f, y * 0.2f, Key(seed, T, 5), 2, roughness);
        return relief;
    }

    /// <summary>
    /// Highlands: big rolling uplands (larger than Hills) broken by bands of rock ledges - steep risers of
    /// several metres that follow the contours - and narrow ravines. Ledges and ravines only occur in
    /// patches, and taper out at their ends, so there is always a way around rather than a wall.
    /// </summary>
    private static float Highlands(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.Highlands;
        float inv = 1f / wavelength;

        float warpX = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.3f * wavelength;
        float qy = y + warpY * 0.3f * wavelength;

        // Large rounded uplands.
        float shape = Fbm01(qx * inv / 1.6f, qy * inv / 1.6f, Key(seed, T, 3), 3, Mathf.Min(roughness, 0.5f));
        float uplands = SmoothStep(0.22f, 1.1f, shape);
        float size = 0.6f + 0.4f * Noise01(x * inv / 5f, y * inv / 5f, Key(seed, T, 4));
        float relief = amplitude * 2.2f * size * uplands;

        // Rock ledges: the contour lines of a slow field become steep risers, only inside ledge zones,
        // whose soft edges shrink the risers to walkable ramps.
        float zone = SmoothStep(0.45f, 0.62f, Noise01(x * inv / 2.2f, y * inv / 2.2f, Key(seed, T, 5)));
        if (zone > 0f)
        {
            const float tiers = 3f;
            float field = Fbm01(qx * inv / 0.9f, qy * inv / 0.9f, Key(seed, T, 6), 2, 0.5f) * tiers;
            float tier = Mathf.Floor(field);
            float riser = SmoothStep(0.8f, 0.9f, field - tier);
            float ledgeHeight = Mathf.Max(3.5f, 0.5f * Mathf.Abs(amplitude));
            relief += zone * ledgeHeight * (tier + riser - 0.5f * tiers);
        }

        // Narrow ravines in their own patches.
        float ravineZone = SmoothStep(0.5f, 0.65f, Noise01(x * inv / 3f, y * inv / 3f, Key(seed, T, 7)));
        if (ravineZone > 0f)
        {
            float line = 1f - Mathf.Abs(Noise(qx * inv / 1.3f, qy * inv / 1.3f, Key(seed, T, 8), 0));
            float ravine = Mathf.Pow(Mathf.Clamp01(line), 14f);
            relief -= ravineZone * ravine * Mathf.Max(3f, 0.7f * Mathf.Abs(amplitude));
        }

        relief += amplitude * 0.03f * Fbm(x * 0.2f, y * 0.2f, Key(seed, T, 9), 2, 0.5f);
        return relief;
    }

    /// <summary>
    /// Glacial valleys: high mountain terrain (see <see cref="Mountains"/>) with broad, flat-floored,
    /// steep-walled U-shaped troughs carved along long winding lines, plus smaller hanging side valleys
    /// whose floors sit high on the main valley walls. Trough floors carry low moraine hummocks and
    /// differ in height from valley to valley; walls get extra rock detail.
    /// </summary>
    private static float Glacial(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.Glacial;
        float mountains = 1.1f * Mountains(x, y, wavelength, amplitude, roughness, seed ^ 0x5F3759DF);

        float troughScale = 4f * wavelength;
        float inv = 1f / troughScale;
        float warpX = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.5f, y * inv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.4f * troughScale;
        float qy = y + warpY * 0.4f * troughScale;

        // Main troughs: flat floor across the middle ~45% of the width, steep walls, open above.
        float across = Mathf.Abs(Fbm(qx * inv, qy * inv, Key(seed, T, 3), 2, 0.5f)) / 0.11f;
        float trough = 1f - SmoothStep(0.45f, 1f, across);
        float floor = amplitude * (0.1f + 0.35f * Noise01(x * inv / 2.5f, y * inv / 2.5f, Key(seed, T, 4)))
                      + amplitude * 0.03f * Fbm(x / 70f, y / 70f, Key(seed, T, 5), 2, 0.5f);

        // Hanging side valleys: narrower troughs whose floors sit about halfway up.
        float sideInv = 1f / (1.3f * wavelength);
        float sideAcross = Mathf.Abs(Fbm(qx * sideInv, qy * sideInv, Key(seed, T, 6), 2, 0.5f)) / 0.09f;
        float sideTrough = 1f - SmoothStep(0.45f, 1f, sideAcross);
        float sideFloor = Mathf.Lerp(floor, mountains, 0.5f);

        float relief = mountains;
        relief = Mathf.Lerp(relief, Mathf.Min(relief, sideFloor), sideTrough * 0.9f);
        relief = Mathf.Lerp(relief, Mathf.Min(relief, floor), trough);

        // Rugged rock around the valley walls.
        float wall = trough * (1f - trough) * 4f;
        relief += amplitude * 0.04f * Fbm(x * 0.15f, y * 0.15f, Key(seed, T, 7), 3, 0.5f) * (0.3f + wall);
        return relief;
    }

    /// <summary>Ocean biomes: deep rolling seafloor with scattered seamounts (relief relative to the normal seafloor).</summary>
    private static float SeaPlain(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaPlain;
        float inv = 1f / wavelength;
        float roll = Fbm(x * inv / 2.5f, y * inv / 2.5f, Key(seed, T, 1), 2, 0.5f) * 0.25f;
        float seamount = Mathf.Pow(SmoothStep(0.72f, 0.98f, Noise01(x * inv / 1.6f, y * inv / 1.6f, Key(seed, T, 2))), 1.5f) * 1.2f;
        return amplitude * (-0.25f + roll + seamount);
    }

    /// <summary>Ocean biomes: seafloor cut by deep, branching submarine ravines (relief relative to the normal seafloor).</summary>
    private static float SeaRavines(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaRavines;
        float inv = 1f / wavelength;
        float warpX = Fbm(x * inv * 0.4f, y * inv * 0.4f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.4f, y * inv * 0.4f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.5f * wavelength;
        float qy = y + warpY * 0.5f * wavelength;

        float main = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise(qx * inv / 1.8f, qy * inv / 1.8f, Key(seed, T, 3), 0))), 10f);
        float branch = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise(qx * inv / 0.7f, qy * inv / 0.7f, Key(seed, T, 4), 0))), 14f);
        float zone = 0.4f + 0.6f * SmoothStep(0.3f, 0.6f, Noise01(x * inv / 4f, y * inv / 4f, Key(seed, T, 5)));
        float base0 = Fbm(x * inv / 3f, y * inv / 3f, Key(seed, T, 6), 2, 0.5f) * 0.2f;
        return amplitude * (base0 - (main + 0.5f * branch) * zone);
    }

    /// <summary>Ocean biomes: reef banks and atoll rings rising toward the surface, with lagoons and coral heads.</summary>
    private static float SeaReef(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaReef;
        float inv = 1f / wavelength;
        float bank = SmoothStep(0.58f, 0.72f, Fbm01(x * inv, y * inv, Key(seed, T, 1), 3, 0.5f));
        // Lagoons: the middle of the larger banks sits lower, leaving a reef ring (an atoll) around it.
        float lagoon = SmoothStep(0.72f, 0.82f, Fbm01(x * inv, y * inv, Key(seed, T, 1), 3, 0.5f));
        float heads = SmoothStep(0.55f, 0.9f, Noise01(x / 9f, y / 9f, Key(seed, T, 3)));
        return amplitude * (bank * (1f - 0.55f * lagoon) + 0.08f * heads * bank);
    }

    /// <summary>Ocean biomes: rough rocky seabed with stepped ledges and boulder fields.</summary>
    private static float SeaRocky(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.SeaRocky;
        float inv = 1f / wavelength;
        float sum = 0f, norm = 0f, a = 1f, f = inv;
        for (int o = 0; o < 3; o++)
        {
            float ridge = 1f - Mathf.Abs(Noise(x * f, y * f, Key(seed, T, 1 + o), 0));
            sum += ridge * ridge * a;
            norm += a;
            a *= roughness;
            f *= 2f;
        }
        float rock = sum / norm;
        float stepped = Mathf.Lerp(rock, Mathf.Round(rock * 4f) / 4f, 0.5f);
        float boulders = SmoothStep(0.6f, 0.9f, Noise01(x / 6f, y / 6f, Key(seed, T, 5))) * 0.15f;
        return amplitude * (0.6f * stepped - 0.3f + boulders);
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

    // ------------------------------------------------------------------ noise helpers

    /// <summary>1 at 0 down to 0 at 1, with zero slope at both ends.</summary>
    private static float Falloff(float t)
    {
        if (t <= 0f) return 1f;
        if (t >= 1f) return 0f;
        return 1f - t * t * (3f - 2f * t);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static int OctavesFor(float wavelength, float smallestFeature)
    {
        int octaves = 1;
        float w = wavelength;
        while (w > smallestFeature * 2f && octaves < 8)
        {
            w *= 0.5f;
            octaves++;
        }
        return Mathf.Max(3, octaves);
    }

    /// <summary>
    /// Gradient noise in about [-1,1], offset by a per-(seed, landform, layer) key so layers and landforms
    /// are independent. Landforms use their own noise (rather than Mathf.PerlinNoise) so their value range -
    /// which the shaping thresholds depend on - is known exactly and identical on every platform.
    /// </summary>
    private static float Noise(float x, float y, int key, int salt)
    {
        uint h = Mix((uint)key + (uint)salt * 0x27D4EB2Fu);
        float ox = (h & 0xFFFF) * (1f / 65536f) * 256f;
        float oy = ((h >> 16) & 0xFFFF) * (1f / 65536f) * 256f;
        return Gradient(x + ox, y + oy);
    }

    private static readonly int[] Permutation = BuildPermutation();

    private static int[] BuildPermutation()
    {
        var p = new int[512];
        var source = new int[256];
        for (int i = 0; i < 256; i++)
            source[i] = i;
        uint state = 0x2F6B3A1Du;
        for (int i = 255; i > 0; i--)
        {
            state = Mix(state + (uint)i);
            int j = (int)(state % (uint)(i + 1));
            int t = source[i]; source[i] = source[j]; source[j] = t;
        }
        for (int i = 0; i < 512; i++)
            p[i] = source[i & 255];
        return p;
    }

    /// <summary>2D Perlin gradient noise, scaled to about [-1,1] (99% within +/-0.9); repeats every 256 units.</summary>
    private static float Gradient(float x, float y)
    {
        float fx = Mathf.Floor(x), fy = Mathf.Floor(y);
        int xi = (int)fx & 255, yi = (int)fy & 255;
        x -= fx; y -= fy;
        float u = x * x * x * (x * (x * 6f - 15f) + 10f);
        float v = y * y * y * (y * (y * 6f - 15f) + 10f);
        int[] p = Permutation;
        int a = p[xi] + yi, b = p[xi + 1] + yi;
        float n00 = Dot(p[a], x, y), n10 = Dot(p[b], x - 1f, y);
        float n01 = Dot(p[a + 1], x, y - 1f), n11 = Dot(p[b + 1], x - 1f, y - 1f);
        float nx0 = n00 + u * (n10 - n00);
        float nx1 = n01 + u * (n11 - n01);
        return Mathf.Clamp((nx0 + v * (nx1 - nx0)) * 1.96f, -1f, 1f);
    }

    // Eight unit gradient directions at 22.5 + k * 45 degrees. None is parallel to a grid axis: with
    // axis-aligned gradients the noise is exactly zero along some whole cell edges, which ridged shapes
    // (1 - |noise|) turn into straight lines.
    private static readonly float[] GradientX = { 0.9239f, 0.3827f, -0.3827f, -0.9239f, -0.9239f, -0.3827f, 0.3827f, 0.9239f };
    private static readonly float[] GradientY = { 0.3827f, 0.9239f, 0.9239f, 0.3827f, -0.3827f, -0.9239f, -0.9239f, -0.3827f };

    private static float Dot(int hash, float x, float y)
    {
        int h = hash & 7;
        return GradientX[h] * x + GradientY[h] * y;
    }

    private static float Noise01(float x, float y, int key)
    {
        return Mathf.Clamp01(0.5f + 0.5f * Noise(x, y, key, 0));
    }

    /// <summary>Layered noise, normalized to about [-1,1].</summary>
    private static float Fbm(float x, float y, int key, int octaves, float persistence)
    {
        float sum = 0f, norm = 0f, amplitude = 1f, frequency = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Noise(x * frequency, y * frequency, key, o) * amplitude;
            norm += amplitude;
            amplitude *= persistence;
            frequency *= 2.03f;
        }
        return norm > 0f ? Mathf.Clamp(sum / norm * 1.3f, -1f, 1f) : 0f;
    }

    private static float Fbm01(float x, float y, int key, int octaves, float persistence)
    {
        return 0.5f + 0.5f * Fbm(x, y, key, octaves, persistence);
    }

    private static int Key(int seed, int landform, int layer)
    {
        unchecked
        {
            return (int)Mix((uint)seed * 0x9E3779B1u ^ (uint)landform * 0x85EBCA77u ^ (uint)layer * 0xC2B2AE3Du);
        }
    }

    private static float Hash01(int seed, int landform, int layer)
    {
        return (Mix((uint)Key(seed, landform, layer)) & 0xFFFFFF) / 16777216f;
    }

    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>String hash that is the same on every platform and run (string.GetHashCode is not guaranteed to be).</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (text != null)
            {
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 16777619u;
                }
            }
            return (int)h;
        }
    }
}
