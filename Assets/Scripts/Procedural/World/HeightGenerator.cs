using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class HeightGenerator
{
    private static List<Biome> availableBiomes;

    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        float[,] heightMap = new float[terrainGenerator.ChunkSize + 1, terrainGenerator.ChunkSize + 1];

        int voronoiSeed = terrainGenerator.VoronoiSeed;// Mathf.RoundToInt(x + y); 
        float height = 0;

        availableBiomes = terrainGenerator.BiomeDefinitions.Select(biomeInstance => biomeInstance.BiomePrefab)
     .ToList();
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