using UnityEngine;

// LandformGenerator, part 5: the shape of mountain massifs (see MountainMassifs for where they stand).
public static partial class LandformGenerator
{
    /// <summary>
    /// Height of a mountain massif at a world position, from how deep inside the mountain territory it is:
    ///
    ///  - Envelope (largest scale): the height the massif's crests can reach here. It rises with depth - gently
    ///    in the foothills (which start outside the territory), then steeply, up to the massif's full height -
    ///    so big territories make tall, broad massifs and small ones small formations, and a range's main ridge
    ///    follows the middle of its territory, dipping into saddles where the territory narrows.
    ///  - Character (slow fields): each massif has its own stature (some much taller than others), steepness
    ///    (difficult massifs with steep flanks and cliff bands, accessible ones with long gentle flanks), gentle
    ///    and steep sides, and profile (pointed, or broad-shouldered with a wide top).
    ///  - Ridges and valleys (medium scale): a ridged, bent noise network divides the massif into crests, peaks,
    ///    spurs and saddles. Valley floors sit at a fraction of the envelope, so they cut deep between peaks in
    ///    the core and rise gradually from the foothills into the massif: natural routes up. Crests stand at the
    ///    envelope, so ridgelines climb all the way to the peaks. Narrow ravines cut some valley floors.
    ///  - Rock (small scale): cliff bands and ledges on difficult faces, benches on accessible ones, rock detail.
    /// </summary>
    internal static float MassifHeight(float x, float y, float depth, float amplitude, float wavelength, float roughness, int seed, float apronLimit)
    {
        const int T = (int)LandformType.Mountains;
        float peakWave = wavelength * PeakSpacing;
        float massifWave = peakWave * 3f;
        float invMassif = 1f / massifWave;

        // Character of this part of the mountains.
        float stature = 0.45f + 0.55f * SmoothStep(0.2f, 0.8f, Noise01(x * invMassif / 1.7f, y * invMassif / 1.7f, Key(seed, T, 40)));
        float difficulty = SmoothStep(0.3f, 0.7f, Noise01(x * invMassif / 1.3f, y * invMassif / 1.3f, Key(seed, T, 41)));
        float gentleSide = SmoothStep(0.35f, 0.65f, Noise01(x * invMassif * 1.6f, y * invMassif * 1.6f, Key(seed, T, 42)));
        float breadth = SmoothStep(0.25f, 0.75f, Noise01(x * invMassif / 1.1f, y * invMassif / 1.1f, Key(seed, T, 43)));

        // Envelope: how high the crests reach here.
        float fullHeight = amplitude * MountainMassifs.MassifHeightScale * stature;
        float flankDegrees = Mathf.Clamp(Mathf.Lerp(27f, 50f, difficulty) + Mathf.Lerp(7f, -9f, gentleSide), MountainMassifs.GentlestFlank, 58f);
        float fullDepth = fullHeight / Mathf.Tan(flankDegrees * Mathf.Deg2Rad);
        float apron = Mathf.Min(0.4f * fullDepth, apronLimit);
        float t = (depth + apron) / (fullDepth + apron);
        if (t <= 0f)
            return 0f;
        t = Mathf.Min(t, 1.25f);
        float tc = Mathf.Min(t, 1f);
        // Pointed: gentle foothills steepening to the summit. Broad: shoulders early and keeps a wide top.
        float pointed = tc * Mathf.Sqrt(tc);
        float broad = SmoothStep(0f, 0.8f, tc);
        float envelope = fullHeight * Mathf.Lerp(pointed, broad, breadth);

        // Bend the ridge network so crests curve and peaks are lopsided.
        float peakInv = 1f / peakWave;
        float warpX = Fbm(x * peakInv * 0.5f, y * peakInv * 0.5f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * peakInv * 0.5f, y * peakInv * 0.5f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.45f * peakWave;
        float qy = y + warpY * 0.45f * peakWave;

        // Ridged layers: peaks joined by crests and saddles, sharp or rounded by region; each layer follows the
        // ridges of the one below, so detail gathers on crests while valley floors stay smoother.
        float sharpness = SmoothStep(0.3f, 0.7f, Noise01(x * peakInv / 1.5f, y * peakInv / 1.5f, Key(seed, T, 5)));
        // Small massifs and foothills are narrower than the main ridge spacing: there the finer layers get more
        // of the weight, so they break into their own spurs and several peaks instead of one smooth cone.
        float smallness = 1f - SmoothStep(0.15f, 0.65f, envelope / Mathf.Max(1f, amplitude * MountainMassifs.MassifHeightScale));
        float firstFalloff = Mathf.Lerp(roughness * LayerFalloff, 0.85f, smallness);
        int octaves = OctavesFor(peakWave, SmallestFeature);
        float frequency = peakInv;
        float layerAmplitude = 1f;
        float weight = 1f;
        float sum = 0f;
        float norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            float n = Noise(qx * frequency, qy * frequency, Key(seed, T, 10 + o), 0);
            // Slightly rounded crest line (a soft |n|): sharp arêtes, but never thinner than the terrain mesh.
            float ridge = 1.04f - Mathf.Sqrt(n * n + 0.0016f);
            float crest = o < 2 ? Mathf.Lerp(1f - n * n, ridge, sharpness) : ridge;
            crest = Mathf.Pow(Mathf.Clamp01(crest), CrestSharpness);
            crest *= weight;
            weight = Mathf.Clamp01(crest * RidgeFeedback);
            sum += crest * layerAmplitude;
            norm += layerAmplitude;
            frequency *= 2f;
            layerAmplitude *= o == 0 ? firstFalloff : roughness * LayerFalloff;
        }
        float ridges = norm > 0f ? Mathf.Min(1f, Mathf.Pow(sum / norm, PeakContrast) * PeakGain) : 0f;

        // Valleys cut to a floor that is shallow in the foothills and deep in the core; crests stand at the envelope.
        float core = SmoothStep(0.1f, 0.9f, tc);
        float floor = Mathf.Lerp(0.62f, 0.2f, core) * Mathf.Lerp(1.1f, 0.85f, difficulty);
        float height = envelope * (floor + (1f - floor) * ridges);

        // The highest crests of a tall massif stand out as horns.
        height += envelope * 0.2f * sharpness * SmoothStep(0.62f, 1f, ridges) * core * core;

        // Ravines: narrow, V-shaped cuts along some valley floors, more on difficult massifs.
        float ravineNoise = Noise(qx * peakInv * 1.8f, qy * peakInv * 1.8f, Key(seed, T, 44), 0);
        float ravine = (1f - SmoothStep(0f, 0.08f, Mathf.Abs(ravineNoise))) * (1f - SmoothStep(0.15f, 0.45f, ridges));
        height -= envelope * ravine * Mathf.Lerp(0.05f, 0.16f, difficulty) * SmoothStep(0.2f, 0.55f, tc);

        // Cliff bands and ledges on some difficult faces; a few shelves on accessible ones. The rock layers are
        // tilted and bent (an offset field), so bands wander across the slope instead of ringing it like contours.
        float step = Mathf.Clamp(amplitude * 0.2f, 6f, 32f);
        float strata = step * 1.6f * Fbm(x * peakInv * 0.6f, y * peakInv * 0.6f, Key(seed, T, 47), 2, 0.5f);
        float cliffs = difficulty * SmoothStep(0.58f, 0.78f, Noise01(qx * peakInv * 1.2f, qy * peakInv * 1.2f, Key(seed, T, 45))) * SmoothStep(0.3f, 0.6f, tc);
        if (cliffs > 0.001f)
            height = Mathf.Lerp(height, Terrace(height + strata, step, 0.18f) - strata, cliffs);
        float benches = (1f - difficulty) * 0.4f * SmoothStep(0.62f, 0.85f, Noise01(qx * peakInv * 1.4f, qy * peakInv * 1.4f, Key(seed, T, 46))) * SmoothStep(0.2f, 0.45f, tc);
        if (benches > 0.001f)
            height = Mathf.Lerp(height, Terrace(height + strata, step * 0.5f, 0.5f) - strata, benches);

        // Rock detail, stronger higher up; nothing at the foot of the foothills, so they meet the land smoothly.
        float rock = Fbm(x * 0.07f, y * 0.07f, Key(seed, T, 30), 2, 0.45f);
        height += amplitude * 0.018f * rock * SmoothStep(0f, 0.2f, tc) * (0.4f + tc);
        return height;
    }

    /// <summary>
    /// Where a massif samples the territory depth: bent by noise, with the depth itself varied, so massif outlines
    /// and main ridges wander instead of following the straight edges of the biome cells.
    /// </summary>
    internal static void MassifDepthWarp(float x, float y, int seed, float scale, out float wx, out float wy, out float depthOffset)
    {
        const int T = (int)LandformType.Mountains;
        // Gentle enough never to fold the terrain (the bend changes by well under one unit per unit moved).
        float inv = 1f / Mathf.Max(1f, 2f * scale);
        wx = Noise(x * inv, y * inv, Key(seed, T, 50), 0) * 0.25f * scale;
        wy = Noise(x * inv, y * inv, Key(seed, T, 51), 0) * 0.25f * scale;
        depthOffset = Fbm(x * inv * 1.5f, y * inv * 1.5f, Key(seed, T, 52), 2, 0.45f) * 0.18f * scale;
    }

    /// <summary>
    /// Terraced version of a height: flat-ish shelves and short risers taking <paramref name="riser"/> of each
    /// step (small = sheer cliff bands). Continuous: it only redistributes the climb within each step.
    /// </summary>
    private static float Terrace(float height, float step, float riser)
    {
        float s = height / step;
        float k = Mathf.Floor(s);
        float f = s - k;
        float rise = SmoothStep(1f - riser, 1f, f);
        return step * (k + 0.12f * f + 0.88f * rise);
    }
}
