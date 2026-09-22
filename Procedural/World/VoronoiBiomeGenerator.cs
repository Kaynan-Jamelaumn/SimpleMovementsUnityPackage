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

        public BiomeWeight(Biome biome, float weight)
        {
            Biome = biome;
            Weight = weight;
        }
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
        bool useClimatePlacement = true, float climateNoiseScale = 500f, float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f)
    {
        // Ensure thread-safe access to the shared dictionary.
        lock (LockObject)
        {
            // If the chunk's Voronoi points have already been generated, do nothing.
            if (ChunkVoronoiPoints.ContainsKey(chunkCoord))
                return;

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
                    nearbyKnownPoints, clusterRadius, clusterStrength, repeatPenalty);

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
        float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f)
    {
        Vector2 warpedPosition = WarpPosition(worldPosition, warpStrength, warpScale, seed);

        // Determine the chunk coordinates for the (warped) world position.
        var chunkCoord = GetChunkCoord(warpedPosition, scale);
        // Ensure the chunk and its neighboring chunks have their Voronoi points generated.
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty);

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
    /// Determines the biome(s) contributing at a given world position, blending the two nearest
    /// Voronoi cells smoothly near their shared border instead of cutting hard between them.
    /// Deep inside a cell a single biome is returned with weight 1; near a border two biomes are
    /// returned whose weights sum to 1, approaching 0.5/0.5 right at the boundary.
    /// </summary>
    /// <param name="blendRange">Fraction (0-1) of the Voronoi cell scale used as the transition band around each border.</param>
    /// <param name="clusterStrength">0-1: how strongly new points are pulled toward their neighbors' biome. See <see cref="GenerateChunkVoronoi"/>.</param>
    /// <param name="clusterRadius">World-unit radius of spatial clustering influence.</param>
    /// <param name="repeatPenalty">0-1+: discourages an already-common biome from spawning a new disconnected patch. See <see cref="GenerateChunkVoronoi"/>.</param>
    public static List<BiomeWeight> GetBiomeBlend(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float warpStrength, float warpScale, float blendRange,
        float clusterStrength = 0.85f, float clusterRadius = 450f, float repeatPenalty = 0.6f)
    {
        var result = new List<BiomeWeight>(2);

        Vector2 warpedPosition = WarpPosition(worldPosition, warpStrength, warpScale, seed);
        var chunkCoord = GetChunkCoord(warpedPosition, scale);
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty);

        var relevantPoints = GetRelevantVoronoiPoints(chunkCoord);
        if (relevantPoints.Count == 0)
            return result;

        float closestDistSq = float.MaxValue;
        float secondDistSq = float.MaxValue;
        VoronoiPoint closestPoint = null;
        VoronoiPoint secondPoint = null;

        foreach (var point in relevantPoints)
        {
            float distSq = (warpedPosition - point.Position).sqrMagnitude;
            if (distSq < closestDistSq)
            {
                secondDistSq = closestDistSq;
                secondPoint = closestPoint;
                closestDistSq = distSq;
                closestPoint = point;
            }
            else if (distSq < secondDistSq)
            {
                secondDistSq = distSq;
                secondPoint = point;
            }
        }

        if (closestPoint == null)
            return result;

        if (secondPoint == null || secondPoint.AssignedBiome == closestPoint.AssignedBiome)
        {
            result.Add(new BiomeWeight(closestPoint.AssignedBiome, 1f));
            return result;
        }

        float closestDist = Mathf.Sqrt(closestDistSq);
        float secondDist = Mathf.Sqrt(secondDistSq);
        float gap = secondDist - closestDist; // >= 0; how far past the border the point sits, toward its own cell.
        float blendDistance = Mathf.Max(0.01f, scale * Mathf.Clamp01(blendRange));

        if (gap >= blendDistance)
        {
            // Deep inside the cell, well past the transition band: a single, undiluted biome.
            result.Add(new BiomeWeight(closestPoint.AssignedBiome, 1f));
            return result;
        }

        // t = 0 right at the border (50/50) -> t = 1 at the edge of the blend band (fully closest biome).
        float t = Mathf.SmoothStep(0f, 1f, gap / blendDistance);
        float closestWeight = Mathf.Lerp(0.5f, 1f, t);

        result.Add(new BiomeWeight(closestPoint.AssignedBiome, closestWeight));
        result.Add(new BiomeWeight(secondPoint.AssignedBiome, 1f - closestWeight));
        return result;
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
        List<VoronoiPoint> nearbyKnownPoints, float clusterRadius, float clusterStrength, float repeatPenalty)
    {
        if (availableBiomes == null || availableBiomes.Count == 0)
            return null;

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

        ApplyNeighborClusterBias(weights, availableBiomes, pointPosition, nearbyKnownPoints, clusterRadius, clusterStrength, repeatPenalty);

        return WeightedRandomBiome(availableBiomes, weights, random);
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
        bool useClimatePlacement, float climateNoiseScale, float clusterStrength, float clusterRadius, float repeatPenalty)
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
            GenerateChunkVoronoi(neighbor, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty);
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