using System.Collections.Generic;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;

public class VoronoiCache
{
    public class WeightedBiome
    {
        public Biome Biome { get; set; }
        public float Weight { get; set; }
    }

    private static VoronoiCache _instance;
    private ConcurrentDictionary<Vector2Int, List<Vector2>> chunkPoints; // Store points per chunk
    private ConcurrentDictionary<Vector2, Biome> pointBiomeMap;
    public bool isInitialized = false;
    ThreadLocal<System.Random> prng;

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
        if (isInitialized)
            return;

        // Thread-safe random number generator
        prng = new ThreadLocal<System.Random>(() => seed != 0 ? new System.Random(seed) : new System.Random());
        isInitialized = true;
    }

    // Generate points dynamically for a given chunk
    private void GeneratePointsForChunk(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
    {
        if (chunkPoints.ContainsKey(chunkCoord))
            return;

        List<Vector2> points = new List<Vector2>();

        // Generate random points within this chunk
        for (int i = 0; i < numPoints; i++)
        {
            float randX = chunkCoord.x + (float)prng.Value.NextDouble() * scale;
            float randY = chunkCoord.y + (float)prng.Value.NextDouble() * scale;
            Vector2 newPoint = new Vector2(randX, randY);
            points.Add(newPoint);

            Biome biome = availableBiomes[prng.Value.Next(availableBiomes.Count)];
            pointBiomeMap[newPoint] = biome;
        }

        chunkPoints[chunkCoord] = points;
    }

    // Get the closest biome for the given world position
    public Biome GetClosestBiome(Vector2 worldChunkPosition, Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
    {
        // Ensure points are generated for the chunk
        GeneratePointsForChunk(chunkCoord, scale, numPoints, availableBiomes);

        Biome closestBiome = null;
        float minDist = float.MaxValue;

        // Check all points in this chunk
        foreach (var seedPoint in chunkPoints[chunkCoord])
        {
            float dist = (worldChunkPosition - seedPoint).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestBiome = pointBiomeMap[seedPoint];
            }
        }

        return closestBiome;
    }
}

public static class NoiseGenerator
{
    public static Biome Voronoi(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
    {
        // Initialize the cache if it hasn't been already.
        VoronoiCache.Instance.Initialize(seed);

        // Determine the chunk coordinates (assuming chunkSize is a known constant)
        Vector2Int chunkCoord = new Vector2Int(Mathf.FloorToInt(worldPosition.x / TerrainGenerator.chunkSize),
                                               Mathf.FloorToInt(worldPosition.y / TerrainGenerator.chunkSize));

        return VoronoiCache.Instance.GetClosestBiome(worldPosition, chunkCoord, scale, numPoints, availableBiomes);
    }
}
