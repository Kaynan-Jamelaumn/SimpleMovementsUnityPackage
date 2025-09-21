
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Threading;
//using UnityEngine;

////CURRENTLY UNSUED BUT FOR ROLLBACK/STUDY PUPORSES THIS IS STILL MANTAINED
//public class VoronoiCache
//{
//    private static VoronoiCache _instance;
//    private ConcurrentDictionary<Vector2Int, List<Vector2>> chunkPoints; // Store points per chunk
//    private ConcurrentDictionary<Vector2, Biome> pointBiomeMap;
//    private ThreadLocal<System.Random> prng;
//    public bool IsInitialized { get; private set; }

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
//        if (IsInitialized) return;

//        prng = new ThreadLocal<System.Random>(() => new System.Random(seed));
//        IsInitialized = true;
//    }

//    private void GeneratePointsForChunk(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
//    {
//        if (chunkPoints.ContainsKey(chunkCoord)) return;

//        var points = new List<Vector2>(numPoints);
//        int chunkSeed = HashSeed(chunkCoord);
//        System.Random localPrng = new System.Random(chunkSeed);

//        for (int i = 0; i < numPoints; i++)
//        {
//            Vector2 newPoint = GenerateRandomPoint(chunkCoord, scale, localPrng);
//            points.Add(newPoint);

//            Biome biome = availableBiomes[localPrng.Next(availableBiomes.Count)];
//            pointBiomeMap[newPoint] = biome;
//        }

//        chunkPoints[chunkCoord] = points;
//    }

//    private int HashSeed(Vector2Int chunkCoord)
//    {
//        return (chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663);
//    }

//    private Vector2 GenerateRandomPoint(Vector2Int chunkCoord, float scale, System.Random random)
//    {
//        float randX = chunkCoord.x * scale + (float)random.NextDouble() * scale;
//        float randY = chunkCoord.y * scale + (float)random.NextDouble() * scale;
//        return new Vector2(randX, randY);
//    }

//    private IEnumerable<Vector2Int> GetAdjacentChunks(Vector2Int chunkCoord)
//    {
//        yield return chunkCoord;
//        yield return chunkCoord + Vector2Int.left;
//        yield return chunkCoord + Vector2Int.right;
//        yield return chunkCoord + Vector2Int.up;
//        yield return chunkCoord + Vector2Int.down;
//        yield return chunkCoord + Vector2Int.up + Vector2Int.left;
//        yield return chunkCoord + Vector2Int.up + Vector2Int.right;
//        yield return chunkCoord + Vector2Int.down + Vector2Int.left;
//        yield return chunkCoord + Vector2Int.down + Vector2Int.right;
//    }

//    public Biome GetClosestBiome(Vector2 worldPosition, Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes)
//    {
//        var relevantChunks = new HashSet<Vector2Int>();
//        foreach (var adjChunk in GetAdjacentChunks(chunkCoord))
//        {
//            if (!chunkPoints.ContainsKey(adjChunk))
//            {
//                GeneratePointsForChunk(adjChunk, scale, numPoints, availableBiomes);
//            }
//            relevantChunks.Add(adjChunk);
//        }

//        Biome closestBiome = null;
//        float minDist = float.MaxValue;

//        foreach (var chunk in relevantChunks)
//        {
//            if (!chunkPoints.TryGetValue(chunk, out var points)) continue;

//            foreach (var point in points)
//            {
//                float dist = (worldPosition - point).sqrMagnitude;
//                if (dist < minDist)
//                {
//                    minDist = dist;
//                    closestBiome = pointBiomeMap[point];
//                }
//            }
//        }

//        return closestBiome;
//    }
//}

//public static class NoiseGenerator
//{
//    public static Biome Voronoi(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
//    {
//        VoronoiCache.Instance.Initialize(seed);
    
//        Vector2Int chunkCoord = new Vector2Int(
//            Mathf.FloorToInt(worldPosition.x / TerrainGenerator.chunkSize),
//            Mathf.FloorToInt(worldPosition.y / TerrainGenerator.chunkSize)
//        );

//        return VoronoiCache.Instance.GetClosestBiome(worldPosition, chunkCoord, scale, numPoints, availableBiomes);
//    }
//}