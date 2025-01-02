using System.Collections.Generic;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;

public class VoronoiCache
{
    private static VoronoiCache _instance;
    private ConcurrentDictionary<Vector2Int, List<Vector2>> chunkPoints; // Store points per chunk
    private ConcurrentDictionary<Vector2, Biome> pointBiomeMap;
    private ThreadLocal<System.Random> prng;
    public bool IsInitialized { get; private set; }

    public static VoronoiCache Instance
    {
        get
        {
            if (_instance == null)
                _instance = new VoronoiCache();
            return _instance;
        }
    }

    private VoronoiCache()
    {
        chunkPoints = new ConcurrentDictionary<Vector2Int, List<Vector2>>();
        pointBiomeMap = new ConcurrentDictionary<Vector2, Biome>();
    }

    public void Initialize(int seed = 0)
    {
        if (IsInitialized) return;

        // Thread-safe random number generator
        prng = new ThreadLocal<System.Random>(() => new System.Random(seed));
        IsInitialized = true;
    }

    private void GeneratePointsForChunk(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
    {
        if (chunkPoints.ContainsKey(chunkCoord))
            return;

        var points = new List<Vector2>(numPoints);
        int chunkSeed = (chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663); // Unique seed per chunk
        System.Random localPrng = new System.Random(chunkSeed);

        for (int i = 0; i < numPoints; i++)
        {
            float randX = chunkCoord.x * scale + (float)localPrng.NextDouble() * scale;
            float randY = chunkCoord.y * scale + (float)localPrng.NextDouble() * scale;

            Vector2 newPoint = new Vector2(randX, randY);
            points.Add(newPoint);

            // Assign biome randomly using local PRNG
            Biome biome = availableBiomes[localPrng.Next(availableBiomes.Count)];
            pointBiomeMap[newPoint] = biome;
        }

        chunkPoints[chunkCoord] = points;
    }

    private IEnumerable<Vector2Int> GetAdjacentChunks(Vector2Int chunkCoord)
    {
        // Returns the current chunk and all direct/diagonal neighbors
        yield return chunkCoord;                         // Current chunk
        yield return chunkCoord + Vector2Int.left;       // Left
        yield return chunkCoord + Vector2Int.right;      // Right
        yield return chunkCoord + Vector2Int.up;         // Up
        yield return chunkCoord + Vector2Int.down;       // Down
        yield return chunkCoord + Vector2Int.up + Vector2Int.left;   // Top-left
        yield return chunkCoord + Vector2Int.up + Vector2Int.right;  // Top-right
        yield return chunkCoord + Vector2Int.down + Vector2Int.left; // Bottom-left
        yield return chunkCoord + Vector2Int.down + Vector2Int.right;// Bottom-right
    }

    public Biome GetClosestBiome(Vector2 worldPosition, Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
    {
        // Generate points for all adjacent chunks
        var generatedChunks = new HashSet<Vector2Int>();
        foreach (var adjChunk in GetAdjacentChunks(chunkCoord))
        {
            if (!generatedChunks.Contains(adjChunk))
            {
                GeneratePointsForChunk(adjChunk, scale, numPoints, availableBiomes);
                generatedChunks.Add(adjChunk);
            }
        }

        Biome closestBiome = null;
        float minDist = float.MaxValue;

        foreach (var adjChunk in generatedChunks)
        {
            if (!chunkPoints.TryGetValue(adjChunk, out var points)) continue;

            foreach (var point in points)
            {
                // Calculate the distance from the given position to each point
                float dist = (worldPosition - point).sqrMagnitude; // Compare squared distances for efficiency
                if (dist < minDist)
                {
                    minDist = dist;
                    closestBiome = pointBiomeMap[point];
                }
            }
        }

        // Return the closest biome based on calculated proximity
        return closestBiome;
    }
}

public static class NoiseGenerator
{
    public static Biome Voronoi(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
    {
        VoronoiCache.Instance.Initialize(seed);

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / TerrainGenerator.chunkSize),
            Mathf.FloorToInt(worldPosition.y / TerrainGenerator.chunkSize)
        );

        return VoronoiCache.Instance.GetClosestBiome(worldPosition, chunkCoord, scale, numPoints, availableBiomes);
    }
}
