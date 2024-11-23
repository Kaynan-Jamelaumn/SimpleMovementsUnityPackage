//using System.Collections.Generic;
//using System.Drawing;
//using System.Linq;
//using System.Net.NetworkInformation;
//using Unity.VisualScripting;
//using UnityEngine;
//using System.Collections.Concurrent;
//using System.Threading;

//public class VoronoiCache
//{
//    public class WeightedBiome
//    {
//        public Biome Biome { get; set; }
//        public float Weight { get; set; }
//    }

//    private static VoronoiCache _instance;
//    private ConcurrentDictionary<Vector2Int, List<Vector2>> chunkPoints; // Store points per chunk
//    private ConcurrentDictionary<Vector2, Biome> pointBiomeMap;
//    public bool isInitialized = false;
//    ThreadLocal<System.Random> prng;

//    public static VoronoiCache Instance
//    {
//        get
//        {
//            if (_instance == null)
//                _instance = new VoronoiCache();
//            return _instance;
//        }
//    }

//    private VoronoiCache()
//    {
//        chunkPoints = new ConcurrentDictionary<Vector2Int, List<Vector2>>();
//        pointBiomeMap = new ConcurrentDictionary<Vector2, Biome>();
//    }

//    public void Initialize(int seed = 0)
//    {
//        if (isInitialized)
//            return;

//        // Thread-safe random number generator
//        prng = new ThreadLocal<System.Random>(() => seed != 0 ? new System.Random(seed) : new System.Random());
//        isInitialized = true;
//    }

//    // Generate points dynamically for a given chunk

//    // Method to check for neighbors within a chunk
//    private bool HasFiveNeighborsInEachDirection(Vector2 point, Vector2Int chunkCoord, float neighborDistance = 1f)
//    {
//        int neighborCount = 0;

//        // Get points for the current chunk
//        if (!chunkPoints.TryGetValue(chunkCoord, out List<Vector2> points))
//        {
//            return false; // No points in the chunk
//        }

//        // Define directions (8 directions: left, right, top, bottom, diagonals)
//        Vector2[] directions = new Vector2[]
//        {
//            Vector2.left, Vector2.right, Vector2.up, Vector2.down,
//            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
//        };

//        // Check each direction for neighbors within the specified distance
//        foreach (var direction in directions)
//        {
//            bool foundNeighbor = points.Any(p => Vector2.Distance(p, point + direction * neighborDistance) <= neighborDistance);
//            if (foundNeighbor)
//            {
//                neighborCount++;
//            }

//            // If we already found neighbors in all 8 directions, return true
//            if (neighborCount >= directions.Length)
//            {
//                return true;
//            }
//        }

//        return false;
//    }

//    // Modified point generation method with neighbor check
//    private void GeneratePointsForChunk(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
//    {
//        if (chunkPoints.ContainsKey(chunkCoord))
//            return;

//        List<Vector2> points = new List<Vector2>();

//        for (int i = 0; i < numPoints; i++)
//        {
//            float randX = chunkCoord.x + (float)prng.Value.NextDouble() * scale;
//            float randY = chunkCoord.y + (float)prng.Value.NextDouble() * scale;
//            Vector2 newPoint = new Vector2(randX, randY);

//            // Check if the point has 5 neighbors in all directions
//            if (!HasFiveNeighborsInEachDirection(newPoint, chunkCoord))
//            {
//                points.Add(newPoint);
//                Biome biome = availableBiomes[prng.Value.Next(availableBiomes.Count)];
//                pointBiomeMap[newPoint] = biome;
//            }
//            // Otherwise, don't add the point
//        }

//        chunkPoints[chunkCoord] = points;
//    }

//    // Get the closest biome for the given world position
//    public Biome GetClosestBiome(Vector2 worldChunkPosition, Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
//    {
//        // Ensure points are generated for the chunk
//        GeneratePointsForChunk(chunkCoord, scale, numPoints, availableBiomes);

//        Biome closestBiome = null;
//        float minDist = float.MaxValue;

//        // Check all points in this chunk
//        foreach (var seedPoint in chunkPoints[chunkCoord])
//        {
//            float dist = (worldChunkPosition - seedPoint).sqrMagnitude;
//            if (dist < minDist)
//            {
//                minDist = dist;
//                closestBiome = pointBiomeMap[seedPoint];
//            }
//        }

//        return closestBiome;
//    }
//}

//public static class NoiseGenerator
//{
//    public static Biome Voronoi(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
//    {
//        // Initialize the cache if it hasn't been already.
//        VoronoiCache.Instance.Initialize(seed);

//        // Determine the chunk coordinates (assuming chunkSize is a known constant)
//        Vector2Int chunkCoord = new Vector2Int(Mathf.FloorToInt(worldPosition.x / TerrainGenerator.chunkSize),
//                                               Mathf.FloorToInt(worldPosition.y / TerrainGenerator.chunkSize));

//        return VoronoiCache.Instance.GetClosestBiome(worldPosition, chunkCoord, scale, numPoints, availableBiomes);
//    }
//}
