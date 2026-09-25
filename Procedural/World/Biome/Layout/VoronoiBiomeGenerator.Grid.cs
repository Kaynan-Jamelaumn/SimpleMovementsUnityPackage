using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// VoronoiBiomeGenerator, part 5 of 5: the jittered point grid, chunk cells and their seeds (see VoronoiBiomeGenerator.cs).
public static partial class VoronoiBiomeGenerator
{
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
    /// Everything <see cref="GetBiomeBlend"/> and <see cref="GetBiomeAtPosition"/> need about the Voronoi
    /// points around one chunk coordinate: the points of the chunk and its 8 neighbors (in exactly the
    /// order <see cref="GetRelevantVoronoiPoints"/> returns them), the distinct biomes among them in order
    /// of first appearance, and which of those biomes each point has. Once all 9 chunks exist their points
    /// never change (until <see cref="ClearCache"/>), so this is built once per chunk coordinate instead of
    /// being regathered - with a lock per chunk, a new list and a biome search per point - for every cell.
    /// </summary>
    private sealed class Neighborhood
    {
        public Vector2[] Positions;
        public Biome[] PointBiomes;     // per point: its own biome
        public Biome[] Biomes;          // distinct biomes, in order of first appearance
        public int[] BiomeIndex;        // per point: index into Biomes
        public int[] FirstPoint;        // per biome: index of its first point
        public int[] PointCounts;       // per biome: how many points it has here

        public static Neighborhood Build(List<VoronoiPoint> points)
        {
            var biomes = new List<Biome>(4);
            var first = new List<int>(4);
            var counts = new List<int>(4);
            var hood = new Neighborhood { Positions = new Vector2[points.Count], PointBiomes = new Biome[points.Count], BiomeIndex = new int[points.Count] };
            for (int i = 0; i < points.Count; i++)
            {
                hood.Positions[i] = points[i].Position;
                hood.PointBiomes[i] = points[i].AssignedBiome;
                // Same equality (List.IndexOf) as the per-cell search this replaces.
                int index = biomes.IndexOf(points[i].AssignedBiome);
                if (index < 0)
                {
                    index = biomes.Count;
                    biomes.Add(points[i].AssignedBiome);
                    first.Add(i);
                    counts.Add(0);
                }
                counts[index]++;
                hood.BiomeIndex[i] = index;
            }
            hood.Biomes = biomes.ToArray();
            hood.FirstPoint = first.ToArray();
            hood.PointCounts = counts.ToArray();
            return hood;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Vector2Int, Neighborhood> Neighborhoods =
        new System.Collections.Concurrent.ConcurrentDictionary<Vector2Int, Neighborhood>();

    /// <summary>
    /// The points around a chunk coordinate, generating any missing chunks first (as the per-cell lookups
    /// always did). Cached once all 9 chunks exist; never cached from an incomplete set (e.g. if the cache
    /// was cleared mid-generation), which then behaves exactly like the uncached lookup.
    /// </summary>
    private static Neighborhood GetNeighborhood(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome,
        bool useClimatePlacement, float climateNoiseScale, float clusterStrength, float clusterRadius, float repeatPenalty, LayoutOptions options)
    {
        if (Neighborhoods.TryGetValue(chunkCoord, out Neighborhood cached))
            return cached;

        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, clusterStrength, clusterRadius, repeatPenalty, options);
        lock (LockObject)
        {
            Neighborhood hood = Neighborhood.Build(GetRelevantVoronoiPoints(chunkCoord));
            bool complete = true;
            foreach (var neighbor in GetAdjacentChunks(chunkCoord))
                complete &= ChunkVoronoiPoints.ContainsKey(neighbor);
            if (complete)
                Neighborhoods[chunkCoord] = hood;
            return hood;
        }
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
}
