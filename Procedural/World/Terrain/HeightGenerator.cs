using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Static class responsible for generating height maps for terrain based on Voronoi diagrams, Perlin noise, and biome-specific parameters.
/// Height is blended across the two nearest biomes near their border (instead of cutting hard between them)
/// so adjacent biomes with different amplitude/frequency don't produce a visible cliff, and the result is
/// optionally weathered by thermal and hydraulic (water) erosion, itself modulated by the local climate.
/// When water is enabled, oceans, lakes, ponds and rivers shape the terrain around that erosion pass - see
/// <see cref="ChunkWaterContext"/>.
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
        return GenerateHeightMap(terrainGenerator, globalOffset, out erosionDeltaMap, out _);
    }

    /// <summary>
    /// Generates a height map for a terrain chunk, additionally reporting the erosion debug delta (see
    /// the other overload) and the chunk's water (oceans, lakes, ponds, rivers - see <see cref="WaterGenerator"/>).
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing configuration parameters such as chunk size, Voronoi scale, and biome definitions.</param>
    /// <param name="globalOffset">The global offset for the chunk's position in the world.</param>
    /// <param name="erosionDeltaMap">See the other overload.</param>
    /// <param name="waterMap">Per-cell water surface, type and shoreline level. Null when <see cref="TerrainGenerator.EnableWater"/> is off.</param>
    /// <returns>A 2D array representing the height map of the terrain chunk.</returns>
    public static float[,] GenerateHeightMap(TerrainGenerator terrainGenerator, Vector2 globalOffset, out float[,] erosionDeltaMap, out WaterMapData waterMap)
    {
        int chunkSize = terrainGenerator.ChunkSize;
        int finalSize = chunkSize + 1;

        bool erosionEnabled = terrainGenerator.EnableErosion;
        bool captureErosionDebug = erosionEnabled && terrainGenerator.VisualizeErosionDebug;
        int padding = erosionEnabled ? Mathf.Max(0, terrainGenerator.ErosionPadding) : 0;
        int paddedSize = finalSize + padding * 2;

        int voronoiSeed = terrainGenerator.VoronoiSeed;
        Vector2 paddedOrigin = globalOffset - new Vector2(padding, padding);

        WaterSettings waterSettings = terrainGenerator.EnableWater ? WaterSettings.From(terrainGenerator) : null;
        TerrainHeightSampler sampler = new TerrainHeightSampler(terrainGenerator, waterSettings);
        ChunkWaterContext water = waterSettings != null
            ? WaterGenerator.CreateChunkContext(waterSettings, sampler, paddedOrigin, paddedSize)
            : null;

        float[,] paddedHeights;
        float[,] preErosionHeights = null;
        bool seamless = erosionEnabled && ErosionTiles.Applies(terrainGenerator, padding);

        if (seamless && !captureErosionDebug)
        {
            // Seamless erosion builds the eroded heights from shared, cached world tiles (see ErosionTiles),
            // so this chunk only needs what its water step uses: where the coastline is.
            paddedHeights = new float[paddedSize, paddedSize];
            if (water != null)
            {
                for (int y = 0; y < paddedSize; y++)
                    for (int x = 0; x < paddedSize; x++)
                        water.RecordLandSide(x, y, sampler.LandSideAt(paddedOrigin.x + x, paddedOrigin.y + y));
            }
            ErosionTiles.Assemble(terrainGenerator, paddedHeights, paddedOrigin, padding);
        }
        else
        {
            paddedHeights = BuildBaseHeights(terrainGenerator, sampler, water, paddedOrigin, paddedSize, erosionEnabled, out float[,] resistanceMap, out float[,] rainfallMap);

            // Snapshot the pre-erosion heights only when the debug visualization actually needs the
            // before/after comparison - this is a full extra heightmap-sized copy, so it stays opt-in.
            preErosionHeights = captureErosionDebug ? (float[,])paddedHeights.Clone() : null;

            if (erosionEnabled)
            {
                if (seamless)
                    ErosionTiles.ErodeSeamlessly(terrainGenerator, paddedHeights, resistanceMap, rainfallMap, paddedOrigin, padding);
                else
                    Erode(terrainGenerator, paddedHeights, resistanceMap, rainfallMap, paddedOrigin);
            }
        }

        erosionDeltaMap = null;
        if (captureErosionDebug)
        {
            // Captured before the water guarantees below, so it shows erosion's own effect only.
            erosionDeltaMap = new float[finalSize, finalSize];
            for (int y = 0; y < finalSize; y++)
            {
                for (int x = 0; x < finalSize; x++)
                {
                    // Positive = erosion removed material here, negative = erosion deposited material here.
                    erosionDeltaMap[x, y] = preErosionHeights[x + padding, y + padding] - paddedHeights[x + padding, y + padding];
                }
            }
        }

        if (water != null)
            water.ApplyPostErosion(paddedHeights);

        float[,] heightMap = new float[finalSize, finalSize];
        bool trackMinMax = !terrainGenerator.TerrainTextureBasedOnVoronoiPoints;

        for (int y = 0; y < finalSize; y++)
        {
            for (int x = 0; x < finalSize; x++)
            {
                float finalHeight = paddedHeights[x + padding, y + padding];
                heightMap[x, y] = finalHeight;

                if (trackMinMax)
                {
                    terrainGenerator.UpdateMinMaxHeight(finalHeight);
                }
            }
        }

        waterMap = water != null ? water.BuildWaterMap(paddedHeights, padding, finalSize) : null;
        return heightMap;
    }

    /// <summary>
    /// The terrain of a (padded) area before erosion: biome-blended land, coast and volcano shaping, and
    /// the lake/river carving that erosion then weathers. Also fills the per-cell erosion resistance and
    /// rainfall maps erosion needs (null when <paramref name="erosionEnabled"/> is off).
    /// </summary>
    public static float[,] BuildBaseHeights(TerrainGenerator terrainGenerator, TerrainHeightSampler sampler, ChunkWaterContext water,
        Vector2 paddedOrigin, int paddedSize, bool erosionEnabled, out float[,] resistanceMap, out float[,] rainfallMap)
    {
        int voronoiSeed = terrainGenerator.VoronoiSeed;
        float[,] paddedHeights = new float[paddedSize, paddedSize];
        resistanceMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;
        rainfallMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;
        // The terrain's (large-scale, slowly varying) influence on rainfall, sampled on a world-aligned lattice.
        TerrainClimate climate = erosionEnabled ? terrainGenerator.TerrainClimate : null;
        int originX = Mathf.RoundToInt(paddedOrigin.x), originY = Mathf.RoundToInt(paddedOrigin.y);
        TerrainClimate.Grid climateMoisture = climate != null ? climate.MoistureGrid(originX, originY, paddedSize) : null;

        for (int y = 0; y < paddedSize; y++)
        {
            float worldPosY = paddedOrigin.y + y;
            for (int x = 0; x < paddedSize; x++)
            {
                float worldPosX = paddedOrigin.x + x;

                List<VoronoiBiomeGenerator.BiomeWeight> blend = sampler.GetBlend(worldPosX, worldPosY);
                float height = sampler.LandHeight(blend, worldPosX, worldPosY, out float relief);
                height = sampler.ShapeLand(worldPosX, worldPosY, height, relief, out float landSide);

                if (water != null)
                    water.RecordLandSide(x, y, landSide);

                if (erosionEnabled)
                {
                    float moisture = ClimateGenerator.GetMoisture(new Vector2(worldPosX, worldPosY), voronoiSeed, terrainGenerator.ClimateNoiseScale);
                    if (climateMoisture != null)
                        moisture = Mathf.Clamp01(moisture + climateMoisture.At(originX + x, originY + y));

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

                if (water != null)
                    height = water.ApplyPreErosion(x, y, worldPosX, worldPosY, height);

                paddedHeights[x, y] = height;
            }
        }

        return paddedHeights;
    }

    /// <summary>Thermal then hydraulic erosion of a padded height area, in place.</summary>
    public static void Erode(TerrainGenerator terrainGenerator, float[,] paddedHeights, float[,] resistanceMap, float[,] rainfallMap, Vector2 paddedOrigin)
    {
        int voronoiSeed = terrainGenerator.VoronoiSeed;
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

    /// <summary>
    /// Biome-blended land height at a world position: each contributing biome's noise (including its
    /// baseElevation) weighted by its blend weight. <paramref name="relief"/> is the same without the
    /// baseElevation part - just the terrain's roughness - which the ocean reuses for seafloor detail.
    /// </summary>
    public static float LandHeightFromBlend(List<VoronoiBiomeGenerator.BiomeWeight> blend, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize, out float relief)
    {
        return LandHeightFromBlend(blend, worldX, worldY, octaves, lacunarity, inverseChunkSize, null, out relief);
    }

    /// <summary>
    /// As above, taking each biome's noise phase from <paramref name="phases"/> (a per-chunk memo) when given,
    /// instead of recomputing it from the biome's name for every cell. Same result either way.
    /// </summary>
    public static float LandHeightFromBlend(List<VoronoiBiomeGenerator.BiomeWeight> blend, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize, LandformSettings phases, out float relief)
    {
        float height = 0f;
        float baseElevation = 0f;
        for (int i = 0; i < blend.Count; i++)
        {
            if (blend[i].Weight <= 0f)
                continue; // a nearby biome that isn't blending in yet (see LayoutOptions.NearbyReach)
            Biome biome = blend[i].Biome;
            float phaseOffset = phases != null ? phases.ClassicPhase(biome) : ClassicPhaseOffset(biome);
            height += blend[i].Weight * ComputeBiomeNoise(biome, phaseOffset, worldX, worldY, octaves, lacunarity, inverseChunkSize, inverseChunkSize);
            baseElevation += blend[i].Weight * blend[i].Biome.baseElevation;
        }

        relief = height - baseElevation;
        return height;
    }

    /// <summary>Classic (original) terrain of one biome at a world position - see <see cref="LandformType.Classic"/>.</summary>
    public static float ComputeClassicBiomeNoise(Biome biome, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize)
    {
        return ComputeBiomeNoise(biome, ClassicPhaseOffset(biome), worldX, worldY, octaves, lacunarity, inverseChunkSize, inverseChunkSize);
    }

    /// <summary>As above with the biome's phase offset already known (see <see cref="ClassicPhaseOffset"/>).</summary>
    public static float ComputeClassicBiomeNoise(Biome biome, float phaseOffset, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize)
    {
        return ComputeBiomeNoise(biome, phaseOffset, worldX, worldY, octaves, lacunarity, inverseChunkSize, inverseChunkSize);
    }

    /// <summary>
    /// A small, stable per-biome phase offset keeps every biome from sampling the exact same
    /// noise field (which would otherwise make adjacent biomes' shapes line up suspiciously,
    /// and make every biome just look like a rescaled copy of the same terrain).
    /// </summary>
    public static float ClassicPhaseOffset(Biome biome)
    {
        return biome.name != null ? (biome.name.GetHashCode() % 1000) * 0.137f : 0f;
    }

    /// <summary>
    /// Applies a biome's fractal/fBm Perlin noise (amplitude, frequency, persistence) at a world
    /// position, plus its baseElevation offset (see <see cref="Biome.baseElevation"/>) - this is what
    /// lets two biomes with identical roughness still sit at genuinely different elevations.
    /// </summary>
    private static float ComputeBiomeNoise(Biome biome, float phaseOffset, float worldX, float worldY, int octaves, float lacunarity, float inverseWidth, float inverseDepth)
    {
        float amplitude = biome.amplitude;
        float frequency = biome.frequency;
        float persistence = biome.persistence;

        float height = biome.baseElevation;
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
