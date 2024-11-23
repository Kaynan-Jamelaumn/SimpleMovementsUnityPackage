using System.Collections.Generic;
using UnityEngine;
public static class HeightGenerator
{
    private static List<Biome> availableBiomes;

    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        float[,] heightMap = new float[terrainGenerator.ChunkSize + 1, terrainGenerator.ChunkSize + 1];

        int voronoiSeed = terrainGenerator.VoronoiSeed;// Mathf.RoundToInt(x + y); 
        float height = 0;

        availableBiomes = new List<Biome>(terrainGenerator.Biomes);
        float inverseWidth = 1f / terrainGenerator.ChunkSize;
        float inverseDepth = 1f / terrainGenerator.ChunkSize;


        for (int y = 0; y <= terrainGenerator.ChunkSize; y++)
        {
            float worldPosY = globalOffset.y + y;
            for (int x = 0; x <= terrainGenerator.ChunkSize; x++)
            {
                float worldPosX = globalOffset.x + x;
                heightMap[x, y] = CalculateHeight(terrainGenerator, worldPosX, worldPosY, voronoiSeed, height, availableBiomes, inverseWidth, inverseDepth);
            }
        }
        return heightMap;
    }



    private static float CalculateHeight(TerrainGenerator terrainGenerator, float x, float y, int voronoiSeed, float height, List<Biome> availableBiomes, float inverseWidth, float inverseDepth)
    {
        Biome biome = NoiseGenerator.Voronoi(new Vector2(x, y), terrainGenerator.VoronoiScale, terrainGenerator.NumVoronoiPoints, availableBiomes, voronoiSeed);
        // Biome biome = NoiseGenerator.Voronoi(new Vector2(x, y), terrainGenerator.VoronoiScale, terrainGenerator.NumVoronoiPoints, availableBiomes, voronoiSeed);
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


//private static float CalculateHeight(
//    TerrainGenerator terrainGenerator,
//    float x, float y,
//    int voronoiSeed,
//    float height,
//    List<Biome> availableBiomes,
//    float inverseWidth,
//    float inverseDepth)
//{
//    // Find the two closest biomes and their weights
//    Vector2 position = new Vector2(x, y);
//    (Biome biomeA, Biome biomeB, float weightA, float weightB) = GetBlendedBiomes(
//        position, terrainGenerator.VoronoiScale, terrainGenerator.NumVoronoiPoints, availableBiomes, voronoiSeed);

//    // Blend biome properties
//    float blendedAmplitude = Mathf.Lerp(biomeA.amplitude, biomeB.amplitude, weightB);
//    float blendedFrequency = Mathf.Lerp(biomeA.frequency, biomeB.frequency, weightB);
//    float blendedPersistence = Mathf.Lerp(biomeA.persistence, biomeB.persistence, weightB);

//    // Calculate height with blended properties
//    for (int o = 0; o < terrainGenerator.Octaves; o++)
//    {
//        float sampleX = (x * inverseWidth) * blendedFrequency;
//        float sampleY = (y * inverseDepth) * blendedFrequency;

//        float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
//        height += perlinValue * blendedAmplitude;

//        blendedFrequency *= terrainGenerator.Lacunarity;
//        blendedAmplitude *= blendedPersistence;

//        // Early exit optimization
//        if (blendedAmplitude < 0.001f)
//            break;
//    }

//    // Update terrainGenerator's Min and Max Height if needed
//    if (!terrainGenerator.TerrainTextureBasedOnVoronoiPoints)
//        terrainGenerator.UpdateMinMaxHeight(height);

//    return height;
//}


//private static (Biome, Biome, float, float) GetBlendedBiomes(
//    Vector2 position, float scale, int numPoints, List<Biome> availableBiomes, int seed)
//{
//    // Retrieve closest biomes from VoronoiCache
//    VoronoiCache.Instance.Initialize(seed);

//    Vector2Int chunkCoord = new Vector2Int(
//        Mathf.FloorToInt(position.x / TerrainGenerator.chunkSize),
//        Mathf.FloorToInt(position.y / TerrainGenerator.chunkSize));

//    // Ensure points are generated for this chunk
//    VoronoiCache.Instance.GeneratePointsForChunk(chunkCoord, scale, numPoints, availableBiomes);

//    // Now safely access chunkPoints
//    List<Vector2> points = VoronoiCache.Instance.chunkPoints[chunkCoord];

//    Biome biomeA = null, biomeB = null;
//    float minDistA = float.MaxValue, minDistB = float.MaxValue;

//    foreach (var seedPoint in points)
//    {
//        float dist = (position - seedPoint).sqrMagnitude;
//        if (dist < minDistA)
//        {
//            minDistB = minDistA;
//            biomeB = biomeA;

//            minDistA = dist;
//            biomeA = VoronoiCache.Instance.pointBiomeMap[seedPoint];
//        }
//        else if (dist < minDistB)
//        {
//            minDistB = dist;
//            biomeB = VoronoiCache.Instance.pointBiomeMap[seedPoint];
//        }
//    }

//    // Calculate weights
//    float weightA = 1f - Mathf.Clamp01(minDistA / (minDistA + minDistB));
//    float weightB = 1f - weightA;

//    return (biomeA, biomeB, weightA, weightB);

//}