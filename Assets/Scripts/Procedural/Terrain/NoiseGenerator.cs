using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEngine;
public class VoronoiCache
{
    public class WeightedBiome
    {
        public Biome Biome { get; set; }
        public float Weight { get; set; }

        // Additional properties or methods if needed
    }

    private static VoronoiCache _instance;
    private List<Vector2> points;
    private Dictionary<Vector2, Biome> pointBiomeMap;
    public bool isInitialized = false;
    System.Random prng;
    System.Random rnd;
    private Dictionary<Vector2, WeightedBiome> weightedBiomes;

    public static VoronoiCache Instance
    {
        get
        {
            if (_instance == null)
                _instance = new VoronoiCache();
            return _instance;
        }
    }

    private VoronoiCache() { }

    public void Initialize(float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
    {
        if (isInitialized)
            return;
        prng = seed != 0 ? new System.Random(seed) : new System.Random();
        rnd = seed != 0 ? new System.Random(seed) : new System.Random();
        points = new List<Vector2>();
        pointBiomeMap = new Dictionary<Vector2, Biome>();

       // weightedBiomes = new Dictionary<Vector2, WeightedBiome>();

        for (int i = 0; i < numPoints; i++)
        {
            float randX = (float)prng.NextDouble() * scale;
            float randY = (float)prng.NextDouble() * scale;
            Vector2 newPoint = new Vector2(randX, randY);
            points.Add(newPoint);
            Biome biome = availableBiomes[prng.Next(availableBiomes.Count)];
            pointBiomeMap[newPoint] = biome;
          //  weightedBiomes[newPoint] = new WeightedBiome { Biome = pointBiomeMap[newPoint], Weight = 1.0f };
        }

        isInitialized = true;
    }
    public Biome GetClosestBiome(Vector2 worldChunkPosition)
    {
        Biome closestBiome = null; // Default value
        float minDist = float.MaxValue;

        foreach (var seedPoint in points)
        {
            //float dist = Vector2.Distance(point, seedPoint);
            float dist = (worldChunkPosition - seedPoint).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestBiome = pointBiomeMap[seedPoint];
            }
        }

        return closestBiome;
    }
    public Biome GetClosestBiomeWithWeight(Vector2 worldChunkPosition, bool check = false)
    {
        Biome closestBiome = null; // Default value
        float minDist = float.MaxValue;
        Vector2 savedSeedPoint = Vector2.zero;

        foreach (var seedPoint in points)
        {
            float dist = (worldChunkPosition - seedPoint).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestBiome = pointBiomeMap[seedPoint];
                savedSeedPoint = seedPoint; // Update savedSeedPoint here
            }
        }

        // If we're not checking weights, return the closest biome immediately
        if (!check) return closestBiome;

        CheckBiomeProbability(savedSeedPoint);

        return weightedBiomes[savedSeedPoint].Biome;
        
    }
    private void CheckBiomeProbability(Vector2 savedSeedPoint)
    {
        // Adjust weight
        weightedBiomes[savedSeedPoint].Weight *= 0.9f;

        // Probability calculation
        double roll = rnd.NextDouble();
        double threshold = 1.0 - weightedBiomes[savedSeedPoint].Weight;
        List<Vector2> pointsCopy = new List<Vector2>(points);

        if (roll < threshold) // Swap biomes if roll is less than threshold
        {

            SwapBiomePointPositions(savedSeedPoint);
        }
    }
    public void SwapBiomePointPositions(Vector2 savedSeedPoint)
    {
        // swap the biomes between the selected point and the random point
        int randomIndex = rnd.Next(points.Count);
        Vector2 randomPoint = points[randomIndex];

        // Swap biomes
        Biome tempBiome = weightedBiomes[savedSeedPoint].Biome;
        weightedBiomes[savedSeedPoint].Biome = weightedBiomes[randomPoint].Biome;
        weightedBiomes[randomPoint].Biome = tempBiome;

        // Also, update pointBiomeMap for both points
        pointBiomeMap[savedSeedPoint] = weightedBiomes[savedSeedPoint].Biome;
        pointBiomeMap[randomPoint] = weightedBiomes[randomPoint].Biome;
    }
}

public static class NoiseGenerator
{
    public static Vector2 lastCheckedPosition = Vector2.zero;
    public static Biome Voronoi(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
    {
        // Initialize the cache if it hasn't been already.
        VoronoiCache.Instance.Initialize(scale, numPoints, availableBiomes, seed);
        // Retrieve the closest biome from the cache.
        //    return VoronoiCache.Instance.GetClosestBiomeWithWeight(worldPosition, true);

        //}
        //return VoronoiCache.Instance.GetClosestBiomeWithWeight(worldPosition);
        return VoronoiCache.Instance.GetClosestBiome(worldPosition);
    }
}