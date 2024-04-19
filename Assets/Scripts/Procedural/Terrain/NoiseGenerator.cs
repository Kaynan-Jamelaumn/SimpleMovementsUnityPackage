using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class VoronoiCache
{
    private static VoronoiCache _instance;
    private List<Vector2> points;
    private Dictionary<Vector2, Biome> pointBiomeMap;
    private bool isInitialized = false;

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
        System.Random prng;
        if (seed!= 0)
            prng = new System.Random(seed);
        else 
           prng = new System.Random();
        points = new List<Vector2>();
        pointBiomeMap = new Dictionary<Vector2, Biome>();

        for (int i = 0; i < numPoints; i++)
        {
            float randX = (float)prng.NextDouble() * scale;
            float randY = (float)prng.NextDouble() * scale;
            Vector2 newPoint = new Vector2(randX, randY);
            points.Add(newPoint);
            Biome biome = availableBiomes[prng.Next(availableBiomes.Count)];
            pointBiomeMap[newPoint] = biome;
        }

        isInitialized = true;
    }

    public Biome GetClosestBiome(Vector2 point)
    {
        Biome closestBiome = null; // Default value
        float minDist = float.MaxValue;

        foreach (var seedPoint in points)
        {
            //float dist = Vector2.Distance(point, seedPoint);
            float dist = (point - seedPoint).sqrMagnitude;
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
    public static Biome Voronoi(Vector2 point, float scale, int numPoints, List<Biome> availableBiomes, int seed = 0)
    {
        // Initialize the cache if it hasn't been already.
        VoronoiCache.Instance.Initialize(scale, numPoints, availableBiomes, seed);

        // Retrieve the closest biome from the cache.
        return VoronoiCache.Instance.GetClosestBiome(point);
    }
}