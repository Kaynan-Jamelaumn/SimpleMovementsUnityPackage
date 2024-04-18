using System.Collections.Generic;
using UnityEngine;
public static class HeightGenerator
{
    private static List<Biome> availableBiomes;

    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        float[,] heightMap = new float[terrainGenerator.GridWidthSize + 1, terrainGenerator.GridDepthSize + 1];

        int voronoiSeed = terrainGenerator.VoronoiSeed;// Mathf.RoundToInt(x + y); 
        float height = 0;

        availableBiomes = new List<Biome>(terrainGenerator.Biomes);
        float inverseWidth = 1f / terrainGenerator.GridWidthSize;
        float inverseDepth = 1f / terrainGenerator.GridDepthSize;

        for (int y = 0; y <= terrainGenerator.GridDepthSize; y++)
        {
            float worldPosY = globalOffset.y + y;
            for (int x = 0; x <= terrainGenerator.GridWidthSize; x++)
            {
                float worldPosX = globalOffset.x + x;
                heightMap[x, y] = CalculateHeight(terrainGenerator, worldPosX, worldPosY, voronoiSeed, height, availableBiomes, inverseWidth, inverseDepth);
            }
        }
        return heightMap;
    }
    private static float CalculateHeight(TerrainGenerator terrainGenerator, float x, float y, int voronoiSeed, float height, List<Biome> availableBiomes, float inverseWidth, float inverseDepth)
    {
        Biome biome = Noise.Voronoi(new Vector2(x, y), voronoiSeed, terrainGenerator.VoronoiScale, terrainGenerator.NumVoronoiPoints, availableBiomes);
        float amplitude = biome.amplitude;
        float frequency = biome.frequency;
        float persistence = biome.persistence;

        //AnimationCurve heightCurve =  new AnimationCurve(terrainGenerator.heightCurve.keys);
        //AnimationCurve heightCurve = new AnimationCurve(biome.heightCurve.keys);


        for (int o = 0; o < terrainGenerator.Octaves; o++)
        {
            float sampleX = (x * inverseWidth) * frequency;
            float sampleY = (y * inverseDepth) * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude;

            frequency *= terrainGenerator.Lacunarity;
            amplitude *= persistence;

            // Early exit optimization
            if (amplitude < 0.001f)
                break;
        }
        // Update terrainGenerator's Min and Max Height if needed
        if (terrainGenerator.TerrainTextureBasedOnVoronoiPoints == false) terrainGenerator.UpdateMinMaxHeight(height);

        return height;
        //AnimationCurve heightCurve =  new AnimationCurve(terrainGenerator.heightCurve.keys);
        //AnimationCurve heightCurve = new AnimationCurve(biome.heightCurve.keys);

        //if (GenericMethods.IsCurveConstant(heightCurve)) return height;
        //// Normalize height to a range between 0 and 1
        //float normalizedHeight = Mathf.InverseLerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, height);

        //// Apply AnimationCurve to the normalized height
        //float curvedHeight = heightCurve.Evaluate(normalizedHeight);

        //// Desnormalize curvedHeight to original scale
        //float originalHeight = Mathf.Lerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, curvedHeight);

        //return originalHeight;
    }

}
public static class Noise
{
    // Generate Voronoi noise based on a point's positionSSS
    public static Biome Voronoi(Vector2 point, int seed, float scale, int numPoints, List<Biome> availableBiomes)
    {
        System.Random prng = new System.Random(seed);
        List<Vector2> points = new List<Vector2>();
        Dictionary<Vector2, Biome> pointBiomeMap = new Dictionary<Vector2, Biome>();

        // Generate random points
        for (int i = 0; i < numPoints; i++)
        {
            float randX = (float)prng.NextDouble() * scale;
            float randY = (float)prng.NextDouble() * scale;
            Vector2 newPoint = new Vector2(randX, randY);
            points.Add(newPoint);
            Biome biome = availableBiomes[prng.Next(availableBiomes.Count)];
            pointBiomeMap[newPoint] = biome;
        }
        Biome closestBiome = availableBiomes[0]; // Default value
        float minDist = float.MaxValue;
        // Find the closest point
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

        return closestBiome ?? availableBiomes[0];
    }
}