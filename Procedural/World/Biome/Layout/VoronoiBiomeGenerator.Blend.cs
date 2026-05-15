using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// VoronoiBiomeGenerator, part 2 of 5: blending neighbouring biomes, slope-safe borders and border warping (see VoronoiBiomeGenerator.cs).
public static partial class VoronoiBiomeGenerator
{
    /// <summary>
    /// Determines the biome(s) contributing at a given world position, blending neighboring Voronoi
    /// cells smoothly near their borders instead of cutting hard between them. Deep inside a cell a
    /// single biome is returned with weight 1; near a border the neighboring biome(s) fade in, reaching
    /// an even split right at the boundary. Entries are sorted by weight (largest first) and sum to 1.
    ///
    /// Every biome is weighted by the distance to ITS OWN nearest point (relative to the nearest point
    /// of any biome), through a smooth falloff. Those distances are continuous everywhere, so the
    /// weights - and the terrain height blended from them - have no jumps anywhere, including where
    /// three biomes meet or where a cell's neighbor changes. (Only ever looking at the two nearest
    /// points would ignore a third, different biome just as close, and switch blend partners abruptly -
    /// which shows up as creases and steps along cell edges.)
    /// </summary>
    /// <param name="blendRange">Fraction (0-1) of the Voronoi cell scale used as the transition band around each border.</param>
    /// <param name="clusterStrength">0-1: how strongly new points are pulled toward their neighbors' biome. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <param name="clusterRadius">World-unit radius of spatial clustering influence.</param>
    /// <param name="repeatPenalty">0-1+: discourages an already-common biome from spawning a new disconnected patch. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <param name="octaves">fBm octave count, used with each biome's amplitude/persistence to estimate how far apart two biomes' worst-case heights are. See <see cref="Biome.EstimateMaxHeightAmplitude"/>.</param>
    /// <param name="maxBoundarySlopeTangent">
    /// tan() of the steepest slope considered traversable at a biome border. When &gt; 0, each biome's
    /// transition band widens (beyond <paramref name="blendRange"/> if needed, capped at about one biome cell)
    /// so the elevation change between two very different biomes never has to happen faster than this
    /// slope purely because of the biome swap - see <see cref="TerrainGenerator.BiomeBoundaryMaxSlopeTangent"/>.
    /// 0 (or less) disables this and keeps the border width exactly as <paramref name="blendRange"/> specifies.
    /// </param>
    public static List<BiomeWeight> GetBiomeBlend(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float warpStrength, float warpScale, float blendRange,
        float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f,
        int octaves = 1, float maxBoundarySlopeTangent = 0f, LayoutOptions options = null)
    {
        var result = new List<BiomeWeight>(2);

        Vector2 warpedPosition = WarpPosition(worldPosition, warpStrength, warpScale, seed);
        var chunkCoord = GetChunkCoord(warpedPosition, scale);
        Neighborhood hood = GetNeighborhood(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);

        Vector2[] positions = hood.Positions;
        int pointCount = positions.Length;
        if (pointCount == 0)
            return result;

        // Distance to the nearest point of each distinct biome nearby. Each point's distance is computed
        // once and reused below (the same expression, so the same value, as recomputing it).
        Biome[] biomes = hood.Biomes;
        int[] biomeIndex = hood.BiomeIndex;
        int[] firstPoint = hood.FirstPoint;
        int count = biomes.Length;
        float[] distances = Scratch(ref scratchDistances, pointCount);
        float[] nearest = Scratch(ref scratchNearest, count);
        float closest = float.MaxValue;
        for (int p = 0; p < pointCount; p++)
        {
            float distance = (warpedPosition - positions[p]).magnitude;
            distances[p] = distance;
            int index = biomeIndex[p];
            if (p == firstPoint[index])
                nearest[index] = distance;
            else if (distance < nearest[index])
                nearest[index] = distance;

            if (distance < closest)
                closest = distance;
        }

        // Smoothed per-biome distances (soft minimum over each biome's points, and over all points).
        float softness = 0.08f * scale / Mathf.Sqrt(Mathf.Max(1, numPoints));
        double[] softSums = ScratchDoubles(count);
        int[] pointCounts = hood.PointCounts;
        for (int p = 0; p < pointCount; p++)
            softSums[biomeIndex[p]] += System.Math.Exp(-(distances[p] - closest) / softness);
        double softTotal = 0.0;
        for (int i = 0; i < count; i++)
            softTotal += softSums[i];

        float baseBand = Mathf.Max(0.01f, scale * Mathf.Clamp01(blendRange));
        // Slope-safe bands are capped at roughly one biome cell (cells are ~scale/sqrt(points) across),
        // so even very different neighbors stay recognizably themselves instead of melting together.
        float maxBand = Mathf.Max(baseBand, scale / Mathf.Sqrt(Mathf.Max(1, numPoints)));

        // Preliminary weights over the configured band - only used to decide which neighbors a biome's
        // slope-safe band should be sized against (so that choice changes smoothly too).
        float[] preliminary = Scratch(ref scratchPreliminary, count);
        for (int i = 0; i < count; i++)
            preliminary[i] = BlendFalloff((nearest[i] - closest) / baseBand);

        float[] weights = Scratch(ref scratchWeights, count);
        float[] bands = Scratch(ref scratchBands, count);
        float total = 0f;
        for (int c = 0; c < count; c++)
        {
            float gap = nearest[c] - closest; // >= 0: how much farther this biome is than the nearest one
            float band = baseBand;

            // Computed for the nearest biome too (gap 0): its weight is 1 whatever the band, but Band is
            // reported to callers and must not jump when which biome is nearest changes.
            if (maxBoundarySlopeTangent > 0f)
            {
                // Band sized against the biomes this one borders here (weighted by how present they
                // are), widened enough that the height change between them stays under the max slope.
                float weighted = 0f;
                float presence = 0f;
                for (int b = 0; b < count; b++)
                {
                    if (b == c || preliminary[b] <= 0f)
                        continue;

                    weighted += preliminary[b] * Mathf.Max(baseBand, RequiredSlopeSafeBlendDistance(biomes[c], biomes[b], octaves, maxBoundarySlopeTangent));
                    presence += preliminary[b];
                }

                // Eased toward the base band as the neighbors fade out, so Band stays continuous (for a biome
                // that isn't the nearest, the nearest one always counts fully, so this changes nothing there).
                if (presence > 0f)
                    band = Mathf.Lerp(baseBand, Mathf.Min(maxBand, weighted / presence), Mathf.Min(1f, presence));
            }

            bands[c] = band;
            weights[c] = BlendFalloff(gap / band);
            total += weights[c];
        }

        float nearbyReach = options != null ? options.NearbyReach : 0f;
        for (int c = 0; c < count; c++)
        {
            float smoothGapC = (float)(softness * System.Math.Log(softTotal / System.Math.Max(1e-300, softSums[c])));
            if (weights[c] <= 0f)
            {
                if (smoothGapC < nearbyReach)
                    result.Add(new BiomeWeight(biomes[c], 0f, nearest[c] - closest, bands[c], smoothGapC, softness * (float)System.Math.Log(Mathf.Max(1, pointCounts[c]))));
                continue;
            }

            float weight = weights[c] / total;
            int insertAt = 0;
            while (insertAt < result.Count && result[insertAt].Weight >= weight)
                insertAt++;
            float slack = softness * (float)System.Math.Log(Mathf.Max(1, pointCounts[c]));
            result.Insert(insertAt, new BiomeWeight(biomes[c], weight, nearest[c] - closest, bands[c], smoothGapC, slack));
        }

        return result;
    }

    // Per-thread scratch arrays for GetBiomeBlend's intermediate values (it runs for every terrain cell,
    // on several worker threads at once), so those don't allocate on every call.
    [System.ThreadStatic] private static float[] scratchDistances;
    [System.ThreadStatic] private static float[] scratchNearest;
    [System.ThreadStatic] private static float[] scratchPreliminary;
    [System.ThreadStatic] private static float[] scratchWeights;
    [System.ThreadStatic] private static float[] scratchBands;
    [System.ThreadStatic] private static double[] scratchSoftSums;

    /// <summary>A zeroed per-thread double buffer (the soft-minimum sums start from 0).</summary>
    private static double[] ScratchDoubles(int size)
    {
        if (scratchSoftSums == null || scratchSoftSums.Length < size)
            scratchSoftSums = new double[System.Math.Max(size, 16)];
        System.Array.Clear(scratchSoftSums, 0, size);
        return scratchSoftSums;
    }

    private static float[] Scratch(ref float[] buffer, int size)
    {
        if (buffer == null || buffer.Length < size)
            buffer = new float[Mathf.Max(size, 64)];
        return buffer;
    }

    /// <summary>1 at 0, smoothly down to 0 at 1 (and beyond) - zero slope at both ends, so bands start and end without a kink.</summary>
    private static float BlendFalloff(float x)
    {
        if (x <= 0f) return 1f;
        if (x >= 1f) return 0f;
        return 1f - x * x * (3f - 2f * x);
    }

    /// <summary>
    /// Band width (in the same "distance gap" units as <see cref="GetBiomeBlend"/>) needed so that the
    /// height change caused by blending from biome A into biome B never exceeds the given slope.
    ///
    /// In <see cref="GetBiomeBlend"/>, B's share on A's side of the border is k/(1+k) with k the falloff of
    /// gap/band; its steepest point has slope ~0.667 per unit of gap/band. The gap (distance to B's point
    /// minus distance to A's) changes by up to 2 per world unit - moving toward one point is moving away
    /// from the other - so the share changes by at most ~1.333/band per world unit. Times the worst-case
    /// height difference between the two biomes, that has to stay under the max slope tangent, which
    /// gives the band below. Treating each biome's height as locally constant across the band is a safe
    /// approximation: it only ever makes the required band wider, never narrower.
    ///
    /// This intentionally only bounds the slope contributed by the biome transition itself - each biome's
    /// own noise still varies within the band exactly as it does everywhere else, so a genuinely steep
    /// biome (mountains) keeps its own natural cliffs right up to the border.
    ///
    /// The height difference it plans for is the systematic one - the gap between the two biomes'
    /// baseElevations - plus their typical (half of worst-case) noise swing. Planning for the absolute
    /// worst case (one biome exactly at its noise peak where the other is at its trough) would blend
    /// every border so widely that biomes lose their identity, for a case that almost never happens;
    /// with the typical case, borders are walkable along most of their length, and the few spots where
    /// both noises happen to peak apart are local steep patches rather than a continuous wall.
    /// </summary>
    private static float RequiredSlopeSafeBlendDistance(Biome biomeA, Biome biomeB, int octaves, float maxSlopeTangent)
    {
        float roughnessGap = 0.5f * (biomeA.EstimateMaxHeightAmplitude(octaves) + biomeB.EstimateMaxHeightAmplitude(octaves));
        float baseElevationGap = Mathf.Abs(biomeA.baseElevation - biomeB.baseElevation);
        float heightGap = roughnessGap + baseElevationGap;
        if (heightGap <= 0f)
            return 0f;

        return (1.333f * heightGap) / maxSlopeTangent;
    }

    /// <summary>
    /// Applies a domain warp to a world position before it's used for Voronoi distance checks, so
    /// cell borders become organic, wobbly curves instead of straight polygon edges.
    /// </summary>
    private static Vector2 WarpPosition(Vector2 position, float warpStrength, float warpScale, int seed)
    {
        if (warpStrength <= 0f)
            return position;

        warpScale = Mathf.Max(0.0001f, warpScale);
        // However the caller tuned it, never let the warp amplitude exceed a modest fraction of its
        // own wavelength - past that point the warp field folds over itself and tears cell borders
        // into jagged, self-intersecting shapes instead of a gentle organic wobble.
        warpStrength = Mathf.Min(warpStrength, warpScale * 0.35f);

        float offsetX = FractalWarpNoise(position.x, position.y, warpScale, seed, 0f) * warpStrength;
        float offsetY = FractalWarpNoise(position.x, position.y, warpScale, seed, 100f) * warpStrength;

        return new Vector2(position.x + offsetX, position.y + offsetY);
    }

    /// <summary>
    /// Multi-octave (fBm) warp noise in [-1,1]. A single fixed-frequency Perlin call would give
    /// every biome border in the whole map the exact same wobble wavelength and amplitude - which
    /// reads as an obviously repeating pattern the moment you compare more than one border, because
    /// it genuinely is the same waveform relocated. Layering a few octaves with a non-integer
    /// lacunarity (so they don't realign into a new single repeating period) breaks that up into
    /// varied, non-periodic-looking wobble instead.
    /// </summary>
    private static float FractalWarpNoise(float x, float y, float baseScale, int seed, float axisOffset)
    {
        float seedShift = seed * 0.0001f + axisOffset;
        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float amplitudeSum = 0f;

        for (int o = 0; o < 3; o++)
        {
            float sampleX = (x / baseScale) * frequency + seedShift;
            float sampleY = (y / baseScale) * frequency + seedShift;
            sum += (Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f) * amplitude;
            amplitudeSum += amplitude;

            amplitude *= 0.5f;
            frequency *= 2.3f; // Non-integer lacunarity so octaves don't realign into a new single repeating period.
        }

        return amplitudeSum > 0f ? sum / amplitudeSum : 0f;
    }
}
