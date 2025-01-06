using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Static class responsible for generating height maps for terrain based on Voronoi diagrams, Perlin noise, and biome-specific parameters.
/// </summary>
public static class HeightGenerator
{
    /// <summary>
    /// List of available biomes used for height generation.
    /// </summary>
    private static List<Biome> availableBiomes;

    /// <summary>
    /// Generates a height map for a terrain chunk based on the provided terrain generator configuration and global offset.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing configuration parameters such as chunk size, Voronoi scale, and biome definitions.</param>
    /// <param name="globalOffset">The global offset for the chunk's position in the world.</param>
    /// <returns>A 2D array representing the height map of the terrain chunk.</returns>
    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        // Initialize the height map with dimensions based on the chunk size.
        float[,] heightMap = new float[terrainGenerator.ChunkSize + 1, terrainGenerator.ChunkSize + 1];

        // Get the Voronoi seed from the terrain generator.
        int voronoiSeed = terrainGenerator.VoronoiSeed;

        // Initialize height value and calculate inverse dimensions for normalization.
        float height = 0;
        float inverseWidth = 1f / terrainGenerator.ChunkSize;
        float inverseDepth = 1f / terrainGenerator.ChunkSize;

        // Populate the available biomes based on the terrain generator's definitions.
        availableBiomes = terrainGenerator.BiomeDefinitions
            .Select(biomeInstance => biomeInstance.BiomePrefab)
            .ToList();

        // Iterate through each point in the chunk grid and calculate the height.
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

    /// <summary>
    /// Calculates the height at a specific world position using biome parameters and noise functions.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing noise and biome configurations.</param>
    /// <param name="x">The x-coordinate of the world position.</param>
    /// <param name="y">The y-coordinate of the world position.</param>
    /// <param name="voronoiSeed">The seed for generating Voronoi diagrams.</param>
    /// <param name="height">The initial height value (used for accumulation).</param>
    /// <param name="availableBiomes">The list of available biomes.</param>
    /// <param name="inverseWidth">The inverse width of the chunk, used for normalization.</param>
    /// <param name="inverseDepth">The inverse depth of the chunk, used for normalization.</param>
    /// <returns>The calculated height at the specified position.</returns>
    private static float CalculateHeight(TerrainGenerator terrainGenerator, float x, float y, int voronoiSeed, float height, List<Biome> availableBiomes, float inverseWidth, float inverseDepth)
    {
        // Determine the biome at the given position using Voronoi diagrams.
        Biome biome = VoronoiBiomeGenerator.GetBiomeAtPosition(
            new Vector2(x, y),
            terrainGenerator.VoronoiScale,
            terrainGenerator.NumVoronoiPoints,
            availableBiomes,
            voronoiSeed,
            terrainGenerator.useWeightedBiome
        );

        // Retrieve biome-specific parameters for noise generation.
        float amplitude = biome.amplitude;
        float frequency = biome.frequency;
        float persistence = biome.persistence;

        // Apply Perlin noise with multiple octaves for detailed height generation.
        for (int o = 0; o < terrainGenerator.Octaves; o++)
        {
            float sampleX = (x * inverseWidth) * frequency;
            float sampleY = (y * inverseDepth) * frequency;

            // Generate Perlin noise and adjust height accordingly.
            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude;

            // Update frequency and amplitude for the next octave.
            frequency *= terrainGenerator.Lacunarity;
            amplitude *= persistence;

            // Early exit optimization to avoid unnecessary calculations.
            if (amplitude < 0.001f)
                break;
        }

        // Update terrain generator's minimum and maximum height values if required.
        if (!terrainGenerator.TerrainTextureBasedOnVoronoiPoints)
        {
            terrainGenerator.UpdateMinMaxHeight(height);
        }

        return height;
    }
}
