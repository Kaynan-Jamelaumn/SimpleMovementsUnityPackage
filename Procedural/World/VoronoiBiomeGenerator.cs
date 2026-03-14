using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Generates Voronoi-based biomes for a procedurally generated map.
/// Points are placed on a jittered grid (not pure uniform random) so cell sizes stay reasonably
/// even instead of occasionally producing huge wedge/triangle cells around an isolated point.
/// Biome assignment always has spatial memory: a new point is biased toward whatever biome
/// already dominates its immediate neighborhood, so territories grow into contiguous blobs
/// instead of scattering independently - this holds even with climate-driven placement off.
/// Climate fitness (temperature/moisture), when enabled, adds physically-plausible clustering on
/// top of that (deserts land somewhere hot/dry, etc.). Cell boundaries are domain-warped so they
/// read as organic coastlines instead of straight polygon edges, and callers can request a smooth
/// two-biome blend near cell borders instead of a hard cutoff.
/// </summary>
public static class VoronoiBiomeGenerator
{
    // Represents a point in the Voronoi diagram with an assigned biome.
    private class VoronoiPoint
    {
        public Vector2 Position;    // Position of the Voronoi point in the world.
        public Biome AssignedBiome; // Biome associated with this Voronoi point.

        // Constructor to initialize a Voronoi point with its position and assigned biome.
        public VoronoiPoint(Vector2 position, Biome biome)
        {
            Position = position;
            AssignedBiome = biome;
        }
    }

    /// <summary>
    /// A biome paired with how strongly it contributes at a queried position (weights across
    /// all entries returned by <see cref="GetBiomeBlend"/> for a position sum to 1).
    /// </summary>
    public readonly struct BiomeWeight
    {
        public readonly Biome Biome;
        public readonly float Weight;
        /// <summary>How much farther (world units, after border warp) this biome's nearest point is than the nearest point of any biome. 0 for the biome that owns the position.</summary>
        public readonly float Gap;
        /// <summary>The transition band (world units of <see cref="Gap"/>) over which <see cref="Weight"/> fades to 0.</summary>
        public readonly float Band;
        /// <summary>
        /// Like <see cref="Gap"/>, but without the kinks <see cref="Gap"/> has along the edges between cells of
        /// the same biome (where the nearest point switches). Used by landform terrain, which shapes slopes
        /// from it and would otherwise show those kinks as creases. Never more than <see cref="SmoothingSlack"/>
        /// below <see cref="Gap"/>.
        /// </summary>
        public readonly float SmoothGap;
        /// <summary>Largest amount <see cref="SmoothGap"/> can fall below <see cref="Gap"/> at this position.</summary>
        public readonly float SmoothingSlack;

        public BiomeWeight(Biome biome, float weight)
        {
            Biome = biome;
            Weight = weight;
            Gap = 0f;
            Band = 1f;
            SmoothGap = 0f;
            SmoothingSlack = 0f;
        }

        public BiomeWeight(Biome biome, float weight, float gap, float band, float smoothGap, float smoothingSlack)
        {
            Biome = biome;
            Weight = weight;
            Gap = gap;
            Band = band;
            SmoothGap = smoothGap;
            SmoothingSlack = smoothingSlack;
        }
    }

    /// <summary>
    /// Optional biome-layout behavior (see <see cref="TerrainGenerator.BiomeLayout"/>). Null means the
    /// original behavior: order-dependent clustering and no landform-aware placement.
    /// </summary>
    public sealed class LayoutOptions
    {
        /// <summary>
        /// Assign biomes so the result depends only on the seed and settings, never on which part of the
        /// world happened to be generated first (see <see cref="GenerateChunkVoronoi"/>).
        /// </summary>
        public bool OrderIndependent;
        /// <summary>Decides each biome's effective landform, which placement uses for mountain belts.</summary>
        public TerrainShapeMode ShapeMode = TerrainShapeMode.ClassicOnly;
        /// <summary>0-1: how strongly mountain-type landforms gather along long belts (0 = off).</summary>
        public float BeltStrength;
        /// <summary>World-unit spacing of the mountain belts.</summary>
        public float BeltScale = 1800f;
        /// <summary>
        /// Also list biomes that are near but not yet blending in (weight 0), up to this smoothed gap
        /// (world units). Landform terrain uses them to start adjusting toward a neighbor before its
        /// weight begins, so nothing changes abruptly where a biome enters the blend. 0 = off. Entries
        /// with weight 0 don't change anything that sums weights (texturing, erosion, Classic terrain).
        /// </summary>
        public float NearbyReach;
    }

    // Dictionary to store Voronoi points for different chunks (keyed by chunk coordinates).
    private static readonly Dictionary<Vector2Int, List<VoronoiPoint>> ChunkVoronoiPoints = new();

    // Lock object used to ensure thread-safe access to the ChunkVoronoiPoints dictionary.
    private static readonly object LockObject = new();

    /// <summary>
    /// Clears every cached chunk's Voronoi points. Call this whenever a fresh world should be
    /// generated (e.g. a new/changed <c>VoronoiSeed</c>, or any other change to scale/point
    /// count/biome list). Without this, a chunk coordinate that was already visited keeps its
    /// OLD points and biome forever - this cache is a static dictionary, keyed only by chunk
    /// coordinate (not by seed), so it survives for the lifetime of the process. That includes
    /// surviving between separate Unity Editor Play sessions if "Reload Domain" is disabled under
    /// Project Settings > Editor > Enter Play Mode Settings, which otherwise silently mixes
    /// stale, previously-generated biome data into a "new" run and looks exactly like a biome
    /// pattern repeating/not updating correctly.
    /// </summary>
    public static void ClearCache()
    {
        lock (LockObject)
        {
            ChunkVoronoiPoints.Clear();
            CellPositions.Clear();
            foreach (var labels in CellLabels)
                labels.Clear();
        }
    }

    /// <summary>
    /// Generates Voronoi biome data for a given chunk or retrieves it if already generated.
    /// </summary>
    /// <param name="chunkCoord">The coordinates of the chunk to generate.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of Voronoi points to generate for the chunk.</param>
    /// <param name="availableBiomes">A list of biomes to assign to the Voronoi points.</param>
    /// <param name="seed">A seed for random number generation, ensuring reproducibility.</param>
    /// <param name="useWeightedBiome">Whether to use each biome's <see cref="Biome.weight"/> as a selection multiplier.</param>
    /// <param name="useClimatePlacement">Whether to additionally bias biome assignment toward biomes whose ideal climate matches this point's temperature/moisture.</param>
    /// <param name="climateNoiseScale">Scale of the temperature/moisture noise fields, in world units.</param>
    /// <param name="clusterStrength">0-1: how strongly a new point's biome is pulled toward whatever biome already dominates its immediate neighborhood. 0 disables spatial clustering entirely (pure i.i.d. random/climate per point).</param>
    /// <param name="clusterRadius">World-unit radius within which already-placed points influence a new point's biome choice.</param>
    /// <param name="repeatPenalty">0-1+: how strongly an already-common biome is discouraged from spawning a brand new, disconnected patch far from its existing territory. 0 disables this (a biome can freely recur in multiple separate spots by chance).</param>
    public static void GenerateChunkVoronoi(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement = true, float climateNoiseScale = 500f, float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f,
        LayoutOptions options = null)
    {
        // Ensure thread-safe access to the shared dictionary.
        lock (LockObject)
        {
            // If the chunk's Voronoi points have already been generated, do nothing.
            if (ChunkVoronoiPoints.ContainsKey(chunkCoord))
                return;

            if (options != null && options.OrderIndependent)
            {
                var args = new LayoutArgs
                {
                    Scale = scale,
                    NumPoints = numPoints,
                    Biomes = availableBiomes,
                    Seed = seed,
                    UseWeightedBiome = useWeightedBiome,
                    UseClimatePlacement = useClimatePlacement,
                    ClimateNoiseScale = climateNoiseScale,
                    ClusterStrength = clusterStrength,
                    ClusterRadius = clusterRadius,
                    RepeatPenalty = repeatPenalty,
                    Options = options,
                };
                ChunkVoronoiPoints[chunkCoord] = BuildOrderIndependentPoints(chunkCoord, args);
                return;
            }

            var chunkPoints = new List<VoronoiPoint>();
            // Create a random generator with a seed based on the chunk coordinates.
            var random = new System.Random(GenerateSeed(chunkCoord, seed));

            // Points already known from neighboring chunks that were generated earlier (this call
            // holds the lock reentrantly, so it's safe to read the shared cache here). Combined with
            // points placed earlier in this same chunk below, this is what lets territories grow into
            // contiguous blobs instead of every point rolling its biome in isolation.
            List<VoronoiPoint> knownNeighborPoints = GetRelevantVoronoiPoints(chunkCoord);

            var jitteredPositions = GenerateJitteredGridPoints(chunkCoord, scale, numPoints, random);

            foreach (var randomPoint in jitteredPositions)
            {
                List<VoronoiPoint> nearbyKnownPoints = ((clusterStrength > 0f || repeatPenalty > 0f) && (knownNeighborPoints.Count > 0 || chunkPoints.Count > 0))
                    ? knownNeighborPoints.Concat(chunkPoints).ToList()
                    : null;

                Biome selectedBiome = SelectBiomeForPoint(randomPoint, availableBiomes, random, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale,
                    nearbyKnownPoints, clusterRadius, clusterStrength, repeatPenalty, options);

                // Add the Voronoi point to the chunk's points list.
                chunkPoints.Add(new VoronoiPoint(randomPoint, selectedBiome));
            }

            ChunkVoronoiPoints[chunkCoord] = chunkPoints;
        }
    }

    /// <summary>
    /// Determines the biome at a given world position using Voronoi-based logic (hard nearest-point cutoff).
    /// </summary>
    /// <param name="worldPosition">The world position to evaluate.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of Voronoi points per chunk.</param>
    /// <param name="availableBiomes">List of available biomes for selection.</param>
    /// <param name="seed">Seed for random generation.</param>
    /// <param name="useWeightedBiome">Whether biome selection at point-generation time should favor higher-weight biomes.</param>
    /// <param name="useClimatePlacement">Whether biome assignment should additionally follow climate fitness for physically-plausible placement.</param>
    /// <param name="climateNoiseScale">Scale of the temperature/moisture noise fields, in world units.</param>
    /// <param name="warpStrength">World-unit strength of the domain warp applied to cell borders. 0 disables warping (straight Voronoi edges).</param>
    /// <param name="warpScale">Noise scale of the domain warp, in world units.</param>
    /// <param name="clusterStrength">0-1: how strongly new points are pulled toward their neighbors' biome. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <param name="clusterRadius">World-unit radius of spatial clustering influence.</param>
    /// <param name="repeatPenalty">0-1+: discourages an already-common biome from spawning a new disconnected patch. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <returns>The closest biome at the specified world position.</returns>
    public static Biome GetBiomeAtPosition(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement = true, float climateNoiseScale = 500f, float warpStrength = 0f, float warpScale = 150f,
        float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f, LayoutOptions options = null)
    {
        Vector2 warpedPosition = WarpPosition(worldPosition, warpStrength, warpScale, seed);

        // Determine the chunk coordinates for the (warped) world position.
        var chunkCoord = GetChunkCoord(warpedPosition, scale);
        // Ensure the chunk and its neighboring chunks have their Voronoi points generated.
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);

        // Variable to track the closest biome.
        Biome closestBiome = null;
        // Variable to track the minimum distance squared (avoiding expensive square roots).
        float minDistanceSquared = float.MaxValue;
        // Iterate over all Voronoi points in the relevant chunks.
        foreach (var point in GetRelevantVoronoiPoints(chunkCoord))
        {
            // Calculate the squared distance between the (warped) world position and the Voronoi point.
            float distanceSquared = (warpedPosition - point.Position).sqrMagnitude;

            // If this point is closer, update the closest biome.
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closestBiome = point.AssignedBiome;
            }
        }

        return closestBiome;
    }

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
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);

        var relevantPoints = GetRelevantVoronoiPoints(chunkCoord);
        if (relevantPoints.Count == 0)
            return result;

        // Distance to the nearest point of each distinct biome nearby.
        var biomes = new List<Biome>(4);
        var nearest = new List<float>(4);
        float closest = float.MaxValue;
        foreach (var point in relevantPoints)
        {
            float distance = (warpedPosition - point.Position).magnitude;
            int index = biomes.IndexOf(point.AssignedBiome);
            if (index < 0)
            {
                biomes.Add(point.AssignedBiome);
                nearest.Add(distance);
            }
            else if (distance < nearest[index])
            {
                nearest[index] = distance;
            }

            if (distance < closest)
                closest = distance;
        }

        int count = biomes.Count;

        // Smoothed per-biome distances (soft minimum over each biome's points, and over all points).
        float softness = 0.08f * scale / Mathf.Sqrt(Mathf.Max(1, numPoints));
        var softSums = new double[count];
        var pointCounts = new int[count];
        foreach (var point in relevantPoints)
        {
            int index = biomes.IndexOf(point.AssignedBiome);
            float distance = (warpedPosition - point.Position).magnitude;
            softSums[index] += System.Math.Exp(-(distance - closest) / softness);
            pointCounts[index]++;
        }
        double softTotal = 0.0;
        for (int i = 0; i < count; i++)
            softTotal += softSums[i];

        float baseBand = Mathf.Max(0.01f, scale * Mathf.Clamp01(blendRange));
        // Slope-safe bands are capped at roughly one biome cell (cells are ~scale/sqrt(points) across),
        // so even very different neighbors stay recognizably themselves instead of melting together.
        float maxBand = Mathf.Max(baseBand, scale / Mathf.Sqrt(Mathf.Max(1, numPoints)));

        // Preliminary weights over the configured band - only used to decide which neighbors a biome's
        // slope-safe band should be sized against (so that choice changes smoothly too).
        float[] preliminary = new float[count];
        for (int i = 0; i < count; i++)
            preliminary[i] = BlendFalloff((nearest[i] - closest) / baseBand);

        float[] weights = new float[count];
        float[] bands = new float[count];
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

    /// <summary>
    /// Picks a biome for a newly generated Voronoi point. The base weight per biome comes from
    /// climate fitness (if enabled) or the biome's configured weight; on top of that, biomes that
    /// already dominate the point's immediate neighborhood (already-placed nearby points, from this
    /// chunk or already-cached neighboring chunks) get a strong multiplicative boost, so territories
    /// grow into contiguous blobs by spatial proximity alone - this holds even with climate off.
    /// </summary>
    private static Biome SelectBiomeForPoint(Vector2 pointPosition, List<Biome> availableBiomes, System.Random random, int seed,
        bool useWeightedBiome, bool useClimatePlacement, float climateNoiseScale,
        List<VoronoiPoint> nearbyKnownPoints, float clusterRadius, float clusterStrength, float repeatPenalty, LayoutOptions options = null)
    {
        if (availableBiomes == null || availableBiomes.Count == 0)
            return null;

        float[] weights = ComputeBaseWeights(pointPosition, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, options);

        ApplyNeighborClusterBias(weights, availableBiomes, pointPosition, nearbyKnownPoints, clusterRadius, clusterStrength, repeatPenalty);

        return WeightedRandomBiome(availableBiomes, weights, random);
    }

    /// <summary>
    /// A point's biome weights before any neighbor influence: climate fitness (if enabled) or each
    /// biome's configured weight, times the landform placement affinity (mountain belts) when active.
    /// Depends only on the point's position and settings.
    /// </summary>
    private static float[] ComputeBaseWeights(Vector2 pointPosition, List<Biome> availableBiomes, int seed,
        bool useWeightedBiome, bool useClimatePlacement, float climateNoiseScale, LayoutOptions options)
    {
        float[] weights = new float[availableBiomes.Count];

        if (useClimatePlacement)
        {
            float temperature = ClimateGenerator.GetTemperature(pointPosition, seed, climateNoiseScale);
            float moisture = ClimateGenerator.GetMoisture(pointPosition, seed, climateNoiseScale);

            for (int i = 0; i < availableBiomes.Count; i++)
            {
                float fitness = availableBiomes[i].ClimateFitness(temperature, moisture);
                weights[i] = useWeightedBiome ? fitness * Mathf.Max(0.0001f, availableBiomes[i].weight) : fitness;
            }

            // No biome fits this climate well enough to register a usable weight: fall back to plain
            // weight/uniform rather than always picking the same "least bad" biome out-of-range.
            if (weights.Sum() <= 0.0001f)
            {
                for (int i = 0; i < availableBiomes.Count; i++)
                    weights[i] = useWeightedBiome ? Mathf.Max(0.0001f, availableBiomes[i].weight) : 1f;
            }
        }
        else
        {
            for (int i = 0; i < availableBiomes.Count; i++)
                weights[i] = useWeightedBiome ? Mathf.Max(0.0001f, availableBiomes[i].weight) : 1f;
        }

        if (options != null && options.BeltStrength > 0f && options.ShapeMode != TerrainShapeMode.ClassicOnly)
        {
            float belt = LandformGenerator.MountainBelt(pointPosition, seed, options.BeltScale);
            for (int i = 0; i < availableBiomes.Count; i++)
            {
                LandformType landform = LandformGenerator.Effective(availableBiomes[i], options.ShapeMode);
                weights[i] *= LandformGenerator.PlacementAffinity(landform, belt, options.BeltStrength);
            }
        }

        return weights;
    }

    /// <summary>
    /// Two opposing biases, both driven by the same "who's already nearby" data:
    ///
    /// 1. A positive bias that boosts whichever biome(s) already dominate the immediate neighborhood
    ///    (within clusterRadius), so a new point extends an existing territory rather than starting
    ///    its own - this is what makes territories contiguous instead of speckled.
    ///
    /// 2. A negative bias against biomes that are already common somewhere in the known area but have
    ///    no instance within clusterRadius of this specific point. Without this, nothing stops the
    ///    same biome from being rolled again far from its own territory purely by chance, producing
    ///    several separate same-shaped, same-sized disconnected patches of one biome scattered inside
    ///    a single chunk. This only fires when there's no nearby point to legitimately grow from, so
    ///    real contiguous growth is never penalized.
    ///
    /// A point with no known neighbors at all (e.g. the very first point ever generated in an empty
    /// region) is left untouched by both - there's nothing to anchor either bias to yet.
    /// </summary>
    private static void ApplyNeighborClusterBias(float[] weights, List<Biome> availableBiomes, Vector2 pointPosition,
        List<VoronoiPoint> nearbyKnownPoints, float clusterRadius, float clusterStrength, float repeatPenalty)
    {
        if (nearbyKnownPoints == null || nearbyKnownPoints.Count == 0 || clusterRadius <= 0f)
            return;

        float[] nearbyInfluence = new float[availableBiomes.Count]; // distance-weighted, only within clusterRadius.
        int[] totalCount = new int[availableBiomes.Count];          // unweighted count anywhere in the known set.
        float totalNearbyInfluence = 0f;

        foreach (var knownPoint in nearbyKnownPoints)
        {
            int biomeIndex = availableBiomes.IndexOf(knownPoint.AssignedBiome);
            if (biomeIndex < 0)
                continue;

            totalCount[biomeIndex]++;

            float dist = Vector2.Distance(pointPosition, knownPoint.Position);
            if (dist >= clusterRadius)
                continue;

            float influence = 1f - dist / clusterRadius; // linear falloff: 1 at the point itself, 0 at the radius edge.
            nearbyInfluence[biomeIndex] += influence;
            totalNearbyInfluence += influence;
        }

        if (clusterStrength > 0f && totalNearbyInfluence > 0f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                float neighborShare = nearbyInfluence[i] / totalNearbyInfluence; // 0-1: this biome's share of nearby support.
                // Up to a ~9x boost at full clusterStrength when a biome fully dominates the neighborhood;
                // no change at all when clusterStrength is 0.
                weights[i] *= Mathf.Lerp(1f, 1f + neighborShare * 8f, clusterStrength);
            }
        }

        if (repeatPenalty > 0f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                // A same-biome point is already within reach: that's legitimate growth, not a repeat - never penalize it.
                if (nearbyInfluence[i] > 0f || totalCount[i] <= 0)
                    continue;

                // The more instances of this biome already exist elsewhere (out of reach here), the
                // less likely a brand new disconnected patch of it should spawn at this location.
                weights[i] /= 1f + totalCount[i] * repeatPenalty * 2f;
            }
        }
    }

    // ------------------------------------------------------------------ order-independent layout
    //
    // The original assignment (above) lets each new point look at whichever neighboring points already
    // exist - so the result depends on which part of the world was generated first, and chunks are
    // generated on worker threads in no fixed order. This version gets the same kind of clustering from
    // information that never depends on order: every point first gets a provisional biome from its own
    // position alone; then, in a couple of passes, each point re-rolls its biome with the neighbor
    // clustering / repeat-penalty biases applied to its neighbors' labels from the previous pass. Every
    // label is a pure function of (cell, point index, seed, settings), so any visiting order - and any
    // thread - produces the same world. Point positions are identical to the original path.

    private const int OrderIndependentPasses = 2;
    private static readonly Dictionary<Vector2Int, Vector2[]> CellPositions = new Dictionary<Vector2Int, Vector2[]>();
    private static readonly Dictionary<Vector2Int, int[]>[] CellLabels =
    {
        new Dictionary<Vector2Int, int[]>(), new Dictionary<Vector2Int, int[]>(), new Dictionary<Vector2Int, int[]>(),
    };

    private sealed class LayoutArgs
    {
        public float Scale;
        public int NumPoints;
        public List<Biome> Biomes;
        public int Seed;
        public bool UseWeightedBiome;
        public bool UseClimatePlacement;
        public float ClimateNoiseScale;
        public float ClusterStrength;
        public float ClusterRadius;
        public float RepeatPenalty;
        public LayoutOptions Options;
    }

    private static List<VoronoiPoint> BuildOrderIndependentPoints(Vector2Int cell, LayoutArgs args)
    {
        Vector2[] positions = CellPointPositions(cell, args);
        int[] labels = CellLabelsAt(cell, OrderIndependentPasses, args);
        var points = new List<VoronoiPoint>(positions.Length);
        for (int k = 0; k < positions.Length; k++)
            points.Add(new VoronoiPoint(positions[k], labels[k] >= 0 ? args.Biomes[labels[k]] : null));
        return points;
    }

    private static Vector2[] CellPointPositions(Vector2Int cell, LayoutArgs args)
    {
        if (CellPositions.TryGetValue(cell, out Vector2[] positions))
            return positions;

        // Same seed and generator as the original path, so points sit in exactly the same places.
        var random = new System.Random(GenerateSeed(cell, args.Seed));
        positions = GenerateJitteredGridPoints(cell, args.Scale, args.NumPoints, random).ToArray();
        CellPositions[cell] = positions;
        return positions;
    }

    private static int[] CellLabelsAt(Vector2Int cell, int pass, LayoutArgs args)
    {
        Dictionary<Vector2Int, int[]> cache = CellLabels[pass];
        if (cache.TryGetValue(cell, out int[] labels))
            return labels;

        Vector2[] positions = CellPointPositions(cell, args);
        labels = new int[positions.Length];
        bool clustering = args.ClusterRadius > 0f && (args.ClusterStrength > 0f || args.RepeatPenalty > 0f);

        if (pass > 0 && !clustering)
        {
            labels = CellLabelsAt(cell, 0, args);
            cache[cell] = labels;
            return labels;
        }

        // Neighbors from the previous pass: every point within reach of this cell's points.
        List<VoronoiPoint> neighborhood = null;
        float reach = Mathf.Max(args.ClusterRadius, args.Scale * 1.5f);
        if (pass > 0)
        {
            int cellRadius = Mathf.Clamp(Mathf.CeilToInt(reach / args.Scale), 1, 5);
            neighborhood = new List<VoronoiPoint>();
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    Vector2Int other = new Vector2Int(cell.x + dx, cell.y + dy);
                    Vector2[] otherPositions = CellPointPositions(other, args);
                    int[] otherLabels = CellLabelsAt(other, pass - 1, args);
                    for (int m = 0; m < otherPositions.Length; m++)
                    {
                        if (otherLabels[m] >= 0)
                            neighborhood.Add(new VoronoiPoint(otherPositions[m], args.Biomes[otherLabels[m]]));
                    }
                }
            }
        }

        var nearby = new List<VoronoiPoint>();
        for (int k = 0; k < positions.Length; k++)
        {
            float[] weights = ComputeBaseWeights(positions[k], args.Biomes, args.Seed, args.UseWeightedBiome,
                args.UseClimatePlacement, args.ClimateNoiseScale, args.Options);

            if (neighborhood != null)
            {
                nearby.Clear();
                foreach (VoronoiPoint neighbor in neighborhood)
                {
                    // The point itself is not its own neighbor.
                    if (neighbor.Position == positions[k])
                        continue;
                    if ((neighbor.Position - positions[k]).sqrMagnitude < reach * reach)
                        nearby.Add(neighbor);
                }
                ApplyNeighborClusterBias(weights, args.Biomes, positions[k], nearby, args.ClusterRadius, args.ClusterStrength, args.RepeatPenalty);
            }

            unchecked
            {
                var random = new System.Random(GenerateSeed(cell, args.Seed ^ (int)(0x9E3779B9u * (uint)(k + 1)) ^ (pass * 0x2545F491)));
                labels[k] = WeightedRandomIndex(weights, random);
            }
        }

        cache[cell] = labels;
        return labels;
    }

    private static int WeightedRandomIndex(float[] weights, System.Random random)
    {
        if (weights.Length == 0)
            return -1;

        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f)
            return random.Next(weights.Length);

        float randomValue = (float)random.NextDouble() * totalWeight;
        for (int i = 0; i < weights.Length; i++)
        {
            if (randomValue < weights[i])
                return i;
            randomValue -= weights[i];
        }
        return weights.Length - 1;
    }

    /// <summary>
    /// Scatters points across a chunk on a jittered sub-grid (stratified sampling) instead of pure
    /// uniform random placement. Pure random scatter can leave large gaps and tight clumps by chance,
    /// which in a Voronoi diagram directly causes huge, often wedge/triangle-shaped cells around
    /// isolated points. Jittering within evenly-sized sub-cells keeps spacing - and therefore cell
    /// size - much more consistent while still looking organic rather than a rigid grid.
    /// </summary>
    private static List<Vector2> GenerateJitteredGridPoints(Vector2Int chunkCoord, float scale, int numPoints, System.Random random)
    {
        var points = new List<Vector2>(Mathf.Max(0, numPoints));
        if (numPoints <= 0)
            return points;

        int gridResolution = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(numPoints)));
        int totalSlots = gridResolution * gridResolution;
        float cellSize = scale / gridResolution;

        // Shuffle which sub-cells get used (Fisher-Yates) so, when numPoints doesn't fill the grid
        // exactly, the same corner isn't always left empty across every chunk.
        var slots = new List<int>(totalSlots);
        for (int i = 0; i < totalSlots; i++)
            slots.Add(i);

        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        // Rotate the whole sub-grid skeleton by a random angle around the chunk's center. Without
        // this, every chunk lays points out on the exact same NxN topology (only which sub-cells get
        // used, and the jitter within each, differ) - combined with neighbor clustering, that rigid
        // shared skeleton is what makes territories look like the same recognizable blob arrangement
        // recurring in every chunk. Rotating around the center can't push a point any farther from
        // its own chunk's center than an unrotated corner point already could, so this stays safely
        // within the 3x3-neighbor lookup's existing coverage - no correctness risk, purely cosmetic decorrelation.
        float rotation = (float)(random.NextDouble() * Mathf.PI * 2.0);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        Vector2 chunkCenter = new Vector2(chunkCoord.x * scale + scale * 0.5f, chunkCoord.y * scale + scale * 0.5f);

        int pointsToPlace = Mathf.Min(numPoints, totalSlots);
        for (int i = 0; i < pointsToPlace; i++)
        {
            int slot = slots[i];
            int sx = slot % gridResolution;
            int sy = slot / gridResolution;

            float jitterX = (float)random.NextDouble() * cellSize;
            float jitterY = (float)random.NextDouble() * cellSize;

            float x = chunkCoord.x * scale + sx * cellSize + jitterX;
            float y = chunkCoord.y * scale + sy * cellSize + jitterY;

            Vector2 offset = new Vector2(x, y) - chunkCenter;
            Vector2 rotatedOffset = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
            points.Add(chunkCenter + rotatedOffset);
        }

        return points;
    }

    /// <summary>
    /// Determines the chunk coordinates for a given world position based on the scale of the Voronoi cells.
    /// </summary>
    /// <param name="worldPosition">The world position.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <returns>The chunk coordinates.</returns>
    private static Vector2Int GetChunkCoord(Vector2 worldPosition, float scale)
    {
        // Determine the chunk coordinates by dividing the world position by the scale.
        int x = Mathf.FloorToInt(worldPosition.x / scale);
        int y = Mathf.FloorToInt(worldPosition.y / scale);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Ensures that the specified chunk and its neighboring chunks have their Voronoi points generated.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates to check.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of points to generate per chunk.</param>
    /// <param name="availableBiomes">The list of available biomes.</param>
    /// <param name="seed">The seed for random generation.</param>
    /// <param name="useWeightedBiome">Whether to use weighted biome selection.</param>
    /// <param name="useClimatePlacement">Whether biome assignment should additionally follow climate fitness.</param>
    /// <param name="climateNoiseScale">Scale of the temperature/moisture noise fields.</param>
    /// <param name="clusterStrength">0-1: spatial clustering strength. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <param name="clusterRadius">World-unit radius of spatial clustering influence.</param>
    private static void EnsureChunkAndNeighbors(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float clusterStrength, float clusterRadius, float repeatPenalty, LayoutOptions options = null)
    {
        // Get the neighboring chunks that do not yet have Voronoi points generated.
        var neighbors = GetAdjacentChunks(chunkCoord).Where(neighbor =>
        {
            lock (LockObject)
            {
                return !ChunkVoronoiPoints.ContainsKey(neighbor);
            }
        });

        // Generate Voronoi points for the neighboring chunks.
        foreach (var neighbor in neighbors)
        {
            GenerateChunkVoronoi(neighbor, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);
        }
    }
    /// <summary>
    /// Returns the coordinates of the neighboring chunks surrounding a given chunk.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates.</param>
    /// <returns>An enumerable of neighboring chunk coordinates.</returns>
    private static IEnumerable<Vector2Int> GetAdjacentChunks(Vector2Int chunkCoord)
    {
        // Yield the current chunk and its 8 surrounding neighbors.
        yield return chunkCoord;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x != 0 || y != 0) // Skip the center chunk since it's already included.
                    yield return chunkCoord + new Vector2Int(x, y);
            }
        }
    }

    /// <summary>
    /// Retrieves all Voronoi points from the specified chunk and its neighboring chunks.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates.</param>
    /// <returns>A list of Voronoi points from the relevant chunks.</returns>
    private static List<VoronoiPoint> GetRelevantVoronoiPoints(Vector2Int chunkCoord)
    {
        // Create a list to store the Voronoi points from the relevant chunks.
        var points = new List<VoronoiPoint>();
        foreach (var neighbor in GetAdjacentChunks(chunkCoord))
        {
            // Iterate through the chunk and its neighbors to gather all Voronoi points.
            lock (LockObject)
            {
                // If the neighboring chunk has Voronoi points, add them to the list.
                if (ChunkVoronoiPoints.TryGetValue(neighbor, out var neighborPoints))
                {
                    points.AddRange(neighborPoints);
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Generates a unique seed using the chunk coordinates and a base seed.
    /// </summary>
    /// <param name="chunkCoord">The coordinates of the chunk.</param>
    /// <param name="baseSeed">The base seed for randomness.</param>
    /// <returns>A unique integer seed for the given chunk.</returns>
    /// <summary>
    /// Turns a chunk coordinate + base seed into a well-mixed 32-bit seed. This matters a lot more
    /// than it looks: the old version here (`baseSeed + x*73856093 ^ y*19349663`) has almost no
    /// avalanche - two multiplications XORed together with no bit-mixing - so nearby/small chunk
    /// coordinates get *correlated* seeds (notably, XOR with y=0 is a no-op, so every chunk along
    /// that row collapses to the same base-x-only seed). That correlation is invisible under pure
    /// per-point randomness, but the neighbor-clustering bias above amplifies it directly into a
    /// visibly repeating biome pattern across chunks. This uses Squirrel Eiserloh's bit-noise hash,
    /// which has strong avalanche even for small, sequential inputs.
    /// </summary>
    private static int GenerateSeed(Vector2Int chunkCoord, int baseSeed)
    {
        unchecked
        {
            const uint BitNoise1 = 0xB5297A4Du;
            const uint BitNoise2 = 0x68E31DA4u;
            const uint BitNoise3 = 0x1B56C4E9u;

            uint mangled = (uint)chunkCoord.x;
            mangled *= BitNoise1;
            mangled += (uint)baseSeed;
            mangled ^= (uint)chunkCoord.y * BitNoise2;
            mangled += (mangled << 13) ^ BitNoise2;
            mangled ^= mangled >> 7;
            mangled *= BitNoise3;
            mangled ^= mangled << 17;

            return (int)mangled;
        }
    }

    /// <summary>
    /// Selects a biome randomly from a list of biomes, weighted by a parallel array of weights.
    /// </summary>
    /// <param name="biomes">The list of available biomes.</param>
    /// <param name="weights">Weights parallel to <paramref name="biomes"/>; larger values are more likely to be picked.</param>
    /// <param name="random">The random number generator to use.</param>
    /// <returns>A randomly selected biome, based on weight distribution.</returns>
    private static Biome WeightedRandomBiome(List<Biome> biomes, float[] weights, System.Random random)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f)
            return biomes[random.Next(biomes.Count)];

        // Generate a random value within the range of the total weight.
        float randomValue = (float)random.NextDouble() * totalWeight;

        // Iterate through the biomes to find the one corresponding to the random value.
        for (int i = 0; i < biomes.Count; i++)
        {
            if (randomValue < weights[i])
                return biomes[i];

            randomValue -= weights[i];
        }

        // Return the last biome as a fallback in case of floating-point rounding error.
        return biomes[biomes.Count - 1];
    }
}