using System.Collections.Generic;
using UnityEngine;

// LandformGenerator, part 2 of 4: the land landforms - mountains, hills, plains, dunes, wetland, plateau, highlands, glacial (see LandformGenerator.cs).
public static partial class LandformGenerator
{
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
}
