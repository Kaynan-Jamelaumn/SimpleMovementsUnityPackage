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
public static partial class VoronoiBiomeGenerator
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
        /// <summary>The terrain's influence on climate-based placement (null = none; see <see cref="TerrainClimate"/>).</summary>
        public TerrainClimate Climate;

        /// <summary>These options without the terrain climate (for layers whose positions aren't world positions).</summary>
        public LayoutOptions WithoutClimate()
        {
            if (Climate == null)
                return this;
            var copy = (LayoutOptions)MemberwiseClone();
            copy.Climate = null;
            return copy;
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
            Neighborhoods.Clear();
            CellPositions.Clear();
            foreach (var labels in CellLabels)
                labels.Clear();
        }
        // Mountain territories follow the biome layout.
        MountainMassifs.ClearCache();
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
            long statStart = GenerationStats.Start();
            try
            {
                GenerateChunkVoronoiLocked(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement,
                    climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);
            }
            finally
            {
                GenerationStats.Record(GenerationStats.VoronoiPoints, statStart);
            }
        }
    }

    /// <summary>The body of <see cref="GenerateChunkVoronoi"/>, called under its lock for a cell without points yet.</summary>
    private static void GenerateChunkVoronoiLocked(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float clusterStrength, float clusterRadius, float repeatPenalty, LayoutOptions options)
    {
        {
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
        // The Voronoi points of this chunk and its neighbors (generated first if missing).
        Neighborhood hood = GetNeighborhood(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);

        // Variable to track the closest biome.
        Biome closestBiome = null;
        // Variable to track the minimum distance squared (avoiding expensive square roots).
        float minDistanceSquared = float.MaxValue;
        // Iterate over all Voronoi points in the relevant chunks.
        Vector2[] positions = hood.Positions;
        for (int i = 0; i < positions.Length; i++)
        {
            // Calculate the squared distance between the (warped) world position and the Voronoi point.
            float distanceSquared = (warpedPosition - positions[i]).sqrMagnitude;

            // If this point is closer, update the closest biome.
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closestBiome = hood.PointBiomes[i];
            }
        }

        return closestBiome;
    }

    /// <summary>
    /// Adds to <paramref name="into"/> every biome <see cref="GetBiomeAtPosition"/> (same settings) could return
    /// anywhere in the rectangle [<paramref name="min"/>, <paramref name="max"/>] - the biomes of the Voronoi points
    /// that could be nearest somewhere in it (a few more at most, never fewer). A quick way to tell that an area
    /// holds no biome of some kind, or only one, without looking up every position in it.
    /// </summary>
    public static void GetBiomesInArea(Vector2 min, Vector2 max, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float warpStrength, float warpScale,
        float clusterStrength, float clusterRadius, float repeatPenalty, LayoutOptions options, HashSet<Biome> into)
    {
        // Positions are warped before the lookup: widen the rectangle by the most the warp can move them (its
        // noise is about [-1,1]; Unity's Perlin noise can overshoot slightly, hence the extra 10%).
        float reach = warpStrength > 0f ? 1.1f * Mathf.Min(warpStrength, Mathf.Max(0.0001f, warpScale) * 0.35f) : 0f;
        float minX = min.x - reach, minY = min.y - reach, maxX = max.x + reach, maxY = max.y + reach;
        Vector2Int from = GetChunkCoord(new Vector2(minX, minY), scale);
        Vector2Int to = GetChunkCoord(new Vector2(maxX, maxY), scale);
        for (int cy = from.y; cy <= to.y; cy++)
        {
            for (int cx = from.x; cx <= to.x; cx++)
            {
                // Warped positions in this Voronoi chunk are looked up among its neighborhood's points.
                Neighborhood hood = GetNeighborhood(new Vector2Int(cx, cy), scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement,
                    climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);
                Vector2[] positions = hood.Positions;
                if (positions.Length == 0)
                    continue;
                float x0 = Mathf.Max(minX, cx * scale), x1 = Mathf.Min(maxX, (cx + 1) * scale);
                float y0 = Mathf.Max(minY, cy * scale), y1 = Mathf.Min(maxY, (cy + 1) * scale);

                // A point can't be nearest anywhere in the rectangle if even its closest spot is farther than
                // some other point's farthest spot.
                float bound = float.MaxValue;
                for (int i = 0; i < positions.Length; i++)
                {
                    float dx = Mathf.Max(Mathf.Abs(positions[i].x - x0), Mathf.Abs(positions[i].x - x1));
                    float dy = Mathf.Max(Mathf.Abs(positions[i].y - y0), Mathf.Abs(positions[i].y - y1));
                    bound = Mathf.Min(bound, dx * dx + dy * dy);
                }
                for (int i = 0; i < positions.Length; i++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(x0 - positions[i].x, positions[i].x - x1));
                    float dy = Mathf.Max(0f, Mathf.Max(y0 - positions[i].y, positions[i].y - y1));
                    if (dx * dx + dy * dy <= bound)
                        into.Add(hood.PointBiomes[i]);
                }
            }
        }
    }
}
