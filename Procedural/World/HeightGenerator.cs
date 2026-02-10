using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Static class responsible for generating height maps for terrain based on Voronoi diagrams, Perlin noise, and biome-specific parameters.
/// Height is blended across the two nearest biomes near their border (instead of cutting hard between them)
/// so adjacent biomes with different amplitude/frequency don't produce a visible cliff, and the result is
/// optionally weathered by thermal and hydraulic (water) erosion, itself modulated by the local climate.
/// </summary>
public static class HeightGenerator
{
    /// <summary>
    /// Generates a height map for a terrain chunk based on the provided terrain generator configuration and global offset.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing configuration parameters such as chunk size, Voronoi scale, and biome definitions.</param>
    /// <param name="globalOffset">The global offset for the chunk's position in the world.</param>
    /// <returns>A 2D array representing the height map of the terrain chunk.</returns>
    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        return GenerateHeightMap(terrainGenerator, globalOffset, out _);
    }

    /// <summary>
    /// Generates a height map for a terrain chunk, additionally reporting how much erosion changed each
    /// cell (for the "Visualize Erosion" debug tool - see <see cref="TerrainGenerator.VisualizeErosionDebug"/>).
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing configuration parameters such as chunk size, Voronoi scale, and biome definitions.</param>
    /// <param name="globalOffset">The global offset for the chunk's position in the world.</param>
    /// <param name="erosionDeltaMap">
    /// Same dimensions as the returned height map. Positive = height removed by erosion at that cell,
    /// negative = height deposited/added by erosion, zero = untouched. Null when erosion is disabled or
    /// <see cref="TerrainGenerator.VisualizeErosionDebug"/> is off (it is never computed unless needed,
    /// since capturing it costs an extra heightmap-sized array copy).
    /// </param>
    /// <returns>A 2D array representing the height map of the terrain chunk.</returns>
    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset, out float[,] erosionDeltaMap)
    {
        int chunkSize = terrainGenerator.ChunkSize;
        int finalSize = chunkSize + 1;

        bool erosionEnabled = terrainGenerator.EnableErosion;
        bool captureErosionDebug = erosionEnabled && terrainGenerator.VisualizeErosionDebug;
        int padding = erosionEnabled ? Mathf.Max(0, terrainGenerator.ErosionPadding) : 0;
        int paddedSize = finalSize + padding * 2;

        int voronoiSeed = terrainGenerator.VoronoiSeed;
        float inverseWidth = 1f / chunkSize;
        float inverseDepth = 1f / chunkSize;

        List<Biome> availableBiomes = terrainGenerator.BiomeDefinitions
            .Select(biomeInstance => biomeInstance.BiomePrefab)
            .ToList();

        Vector2 paddedOrigin = globalOffset - new Vector2(padding, padding);

        float[,] paddedHeights = new float[paddedSize, paddedSize];
        float[,] resistanceMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;
        float[,] rainfallMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;

        for (int y = 0; y < paddedSize; y++)
        {
            float worldPosY = paddedOrigin.y + y;
            for (int x = 0; x < paddedSize; x++)
            {
                float worldPosX = paddedOrigin.x + x;

                List<VoronoiBiomeGenerator.BiomeWeight> blend = VoronoiBiomeGenerator.GetBiomeBlend(
                    new Vector2(worldPosX, worldPosY),
                    terrainGenerator.VoronoiScale,
                    terrainGenerator.NumVoronoiPoints,
                    availableBiomes,
                    voronoiSeed,
                    terrainGenerator.useWeightedBiome,
                    terrainGenerator.UseNaturalClimatePlacement,
                    terrainGenerator.ClimateNoiseScale,
                    terrainGenerator.VoronoiWarpStrength,
                    terrainGenerator.VoronoiWarpScale,
                    terrainGenerator.BiomeBlendRange,
                    terrainGenerator.BiomeClusterStrength,
                    terrainGenerator.BiomeClusterRadius,
                    terrainGenerator.BiomeRepeatPenalty
                );

                float height = 0f;
                for (int i = 0; i < blend.Count; i++)
                {
                    height += blend[i].Weight * ComputeBiomeNoise(blend[i].Biome, worldPosX, worldPosY, terrainGenerator.Octaves, terrainGenerator.Lacunarity, inverseWidth, inverseDepth);
                }

                paddedHeights[x, y] = height;

                if (erosionEnabled)
                {
                    float moisture = ClimateGenerator.GetMoisture(new Vector2(worldPosX, worldPosY), voronoiSeed, terrainGenerator.ClimateNoiseScale);

                    float resistance = 0f;
                    float rainfall = 0f;
                    for (int i = 0; i < blend.Count; i++)
                    {
                        resistance += blend[i].Weight * blend[i].Biome.erosionResistance;
                        rainfall += blend[i].Weight * blend[i].Biome.rainfallErosionMultiplier;
                    }

                    resistanceMap[x, y] = Mathf.Clamp01(resistance);
                    // Rainfall-driven ("climate") erosion strength: how much water this cell's climate feeds into passing droplets.
                    rainfallMap[x, y] = Mathf.Clamp01(moisture * rainfall);
                }
            }
        }

        // Snapshot the pre-erosion heights only when the debug visualization actually needs the
        // before/after comparison - this is a full extra heightmap-sized copy, so it stays opt-in.
        float[,] preErosionHeights = captureErosionDebug ? (float[,])paddedHeights.Clone() : null;

        if (erosionEnabled)
        {
            if (terrainGenerator.ThermalIterations > 0 && terrainGenerator.ThermalErosionRate > 0f)
            {
                ErosionGenerator.ThermalErode(paddedHeights, resistanceMap, terrainGenerator.ThermalIterations, terrainGenerator.TalusAngle, terrainGenerator.ThermalErosionRate);
            }

            if (terrainGenerator.HydraulicDropletDensity > 0f)
            {
                Vector2Int worldOrigin = new Vector2Int(Mathf.RoundToInt(paddedOrigin.x), Mathf.RoundToInt(paddedOrigin.y));
                ErosionGenerator.HydraulicErode(
                    paddedHeights,
                    resistanceMap,
                    rainfallMap,
                    worldOrigin,
                    voronoiSeed,
                    terrainGenerator.HydraulicDropletDensity,
                    terrainGenerator.DropletLifetime,
                    terrainGenerator.DropletInertia,
                    terrainGenerator.SedimentCapacityFactor,
                    terrainGenerator.MinSedimentCapacity,
                    terrainGenerator.ErodeSpeed,
                    terrainGenerator.DepositSpeed,
                    terrainGenerator.EvaporateSpeed,
                    terrainGenerator.ErosionGravity,
                    terrainGenerator.ErosionRadius
                );
            }
        }

        float[,] heightMap = new float[finalSize, finalSize];
        erosionDeltaMap = captureErosionDebug ? new float[finalSize, finalSize] : null;
        bool trackMinMax = !terrainGenerator.TerrainTextureBasedOnVoronoiPoints;

        for (int y = 0; y < finalSize; y++)
        {
            for (int x = 0; x < finalSize; x++)
            {
                int paddedX = x + padding;
                int paddedY = y + padding;
                float finalHeight = paddedHeights[paddedX, paddedY];
                heightMap[x, y] = finalHeight;

                if (captureErosionDebug)
                {
                    // Positive = erosion removed material here, negative = erosion deposited material here.
                    erosionDeltaMap[x, y] = preErosionHeights[paddedX, paddedY] - finalHeight;
                }

                if (trackMinMax)
                {
                    terrainGenerator.UpdateMinMaxHeight(finalHeight);
                }
            }
        }

        return heightMap;
    }

    /// <summary>
    /// Applies a biome's fractal/fBm Perlin noise (amplitude, frequency, persistence) at a world position.
    /// </summary>
    private static float ComputeBiomeNoise(Biome biome, float worldX, float worldY, int octaves, float lacunarity, float inverseWidth, float inverseDepth)
    {
        float amplitude = biome.amplitude;
        float frequency = biome.frequency;
        float persistence = biome.persistence;

        // A small, stable per-biome phase offset keeps every biome from sampling the exact same
        // noise field (which would otherwise make adjacent biomes' shapes line up suspiciously,
        // and make every biome just look like a rescaled copy of the same terrain).
        float phaseOffset = biome.name != null ? (biome.name.GetHashCode() % 1000) * 0.137f : 0f;

        float height = 0f;
        for (int o = 0; o < octaves; o++)
        {
            float sampleX = (worldX * inverseWidth) * frequency + phaseOffset;
            float sampleY = (worldY * inverseDepth) * frequency + phaseOffset;

            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude;

            frequency *= lacunarity;
            amplitude *= persistence;

            if (amplitude < 0.001f)
                break;
        }

        return height;
    }
}
