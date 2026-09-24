using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Samples the deterministic base terrain (biome-blended land height, then ocean/coast shaping and
/// volcanoes) at any world position. <see cref="HeightGenerator"/> builds each chunk from exactly these
/// functions, and the water feature generators (<see cref="LakeGenerator"/>, <see cref="RiverGenerator"/>)
/// use the same ones to pick lake sites and trace rivers - so a feature is always placed against the
/// same terrain it ends up carved into, no matter which chunk asks first.
///
/// Biomes come in placement roles (<see cref="BiomePlacement"/>): land biomes form the normal Voronoi
/// layout; ocean biomes form their own, separate layout under the sea (which shapes the seafloor and, via
/// <see cref="GetTextureBlend"/>, takes over texturing at the coastline); a volcanic biome is painted over
/// volcanoes.
/// </summary>
public sealed class TerrainHeightSampler
{
    // The ocean biome layout is the same Voronoi layout function, looked up this far east, so it gets its
    // own independent cells (and cache entries). It is looked up at a reduced scale so ocean provinces
    // are a few times larger than land biome cells, as seafloor regions are.
    private const float OceanLayerOffset = 262144f;
    private const float OceanLayerScale = 2.5f;

    private readonly TerrainGenerator terrainGenerator;
    private readonly List<Biome> availableBiomes;
    private readonly List<Biome> oceanBiomes;
    private readonly Biome volcanicBiome;
    private readonly float inverseChunkSize;
    private readonly float boundaryMaxSlopeTangent;
    private readonly WaterSettings water;
    private readonly LandformSettings landforms;
    private readonly VoronoiBiomeGenerator.LayoutOptions layout;

    public TerrainHeightSampler(TerrainGenerator terrainGenerator, WaterSettings water)
    {
        this.terrainGenerator = terrainGenerator;
        this.water = water;
        List<Biome> all = terrainGenerator.BiomeDefinitions.Select(biomeInstance => biomeInstance.BiomePrefab).ToList();
        availableBiomes = all.Where(b => b == null || b.placement == BiomePlacement.Land).ToList();
        if (availableBiomes.Count == 0)
            availableBiomes = all;

        List<Biome> ocean = all.Where(b => b != null && b.placement == BiomePlacement.Ocean).ToList();
        oceanBiomes = ocean.Count > 0 && water != null && water.OceansEnabled ? ocean : null;
        volcanicBiome = all.FirstOrDefault(b => b != null && b.placement == BiomePlacement.Volcanic);

        inverseChunkSize = 1f / terrainGenerator.ChunkSize;
        boundaryMaxSlopeTangent = terrainGenerator.BiomeBoundaryMaxSlopeTangent;
        landforms = LandformSettings.From(terrainGenerator);
        layout = terrainGenerator.BiomeLayout;
    }

    /// <summary>True when ocean or volcanic biomes change texturing (see <see cref="GetTextureBlend"/>).</summary>
    public bool HasSpecialBiomes => oceanBiomes != null || (volcanicBiome != null && landforms.VolcanoesEnabled);

    /// <summary>The land biome blend (the normal Voronoi layout) at a world position.</summary>
    public List<VoronoiBiomeGenerator.BiomeWeight> GetBlend(float x, float y)
    {
        return Layout(availableBiomes, new Vector2(x, y));
    }

    private List<VoronoiBiomeGenerator.BiomeWeight> GetOceanBlend(float x, float y)
    {
        return Layout(oceanBiomes, new Vector2(x / OceanLayerScale + OceanLayerOffset, y / OceanLayerScale));
    }

    private List<VoronoiBiomeGenerator.BiomeWeight> Layout(List<Biome> biomes, Vector2 position)
    {
        TerrainGenerator tg = terrainGenerator;
        return VoronoiBiomeGenerator.GetBiomeBlend(
            position,
            tg.VoronoiScale,
            tg.NumVoronoiPoints,
            biomes,
            tg.VoronoiSeed,
            tg.useWeightedBiome,
            tg.UseNaturalClimatePlacement,
            tg.ClimateNoiseScale,
            tg.VoronoiWarpStrength,
            tg.VoronoiWarpScale,
            tg.BiomeBlendRange,
            tg.BiomeClusterStrength,
            tg.BiomeClusterRadius,
            tg.BiomeRepeatPenalty,
            tg.Octaves,
            boundaryMaxSlopeTangent,
            layout
        );
    }

    public float LandHeight(List<VoronoiBiomeGenerator.BiomeWeight> blend, float x, float y, out float relief)
    {
        return LandformGenerator.LandHeight(blend, x, y, landforms, out relief);
    }

    /// <summary>
    /// Everything applied on top of the biome-blended land height: ocean/coast shaping (cliffs, sea stacks,
    /// an ocean biome's seafloor) and volcanoes. <paramref name="landSide"/> is the continent field
    /// (<see cref="OceanGenerator.LandSide"/>), float.MaxValue when oceans are off.
    /// </summary>
    public float ShapeLand(float x, float y, float land, float relief, out float landSide)
    {
        float height = ShapeCoast(x, y, land, relief, out landSide);
        return VolcanoGenerator.Apply(landforms, this, x, y, height);
    }

    private float ShapeCoast(float x, float y, float land, float relief, out float landSide)
    {
        landSide = float.MaxValue;
        if (water == null || !water.OceansEnabled)
            return land;

        landSide = OceanGenerator.LandSide(water, x, y);
        float seafloor = float.NaN;
        if (landSide < 0f && oceanBiomes != null)
            seafloor = LandformGenerator.LandHeight(GetOceanBlend(x, y), x, y, landforms, out _);
        return OceanGenerator.ShapeHeight(water, x, y, land, relief, landSide, seafloor);
    }

    /// <summary>Terrain height before erosion and before any lake/river carving.</summary>
    public float SampleBaseHeight(float x, float y)
    {
        float land = LandHeight(GetBlend(x, y), x, y, out float relief);
        return ShapeLand(x, y, land, relief, out _);
    }

    /// <summary>Base terrain without volcanoes - what a volcano is built on top of.</summary>
    public float SamplePreVolcanoHeight(float x, float y)
    {
        float land = LandHeight(GetBlend(x, y), x, y, out float relief);
        return ShapeCoast(x, y, land, relief, out _);
    }

    /// <summary>
    /// Biome blend for texturing and objects: the land layout, handing over to the ocean biome layout
    /// along the coastline (within about 10 units of the waterline) when there are ocean biomes, and to
    /// the volcanic biome over volcanoes. Sorted by weight, largest first; identical to
    /// <see cref="GetBlend"/> when there are no ocean/volcanic biomes.
    /// </summary>
    public List<VoronoiBiomeGenerator.BiomeWeight> GetTextureBlend(float x, float y)
    {
        List<VoronoiBiomeGenerator.BiomeWeight> blend = GetBlend(x, y);
        if (!HasSpecialBiomes)
            return blend;

        var weights = new List<KeyValuePair<Biome, float>>(blend.Count + 2);
        float landFactor = 1f;
        if (oceanBiomes != null)
        {
            float side = OceanGenerator.LandSide(water, x, y);
            landFactor = WaterGenerator.SmoothStep01((side / water.ContinentGradient + 10f) / 20f);
        }
        for (int i = 0; i < blend.Count; i++)
            AddWeight(weights, blend[i].Biome, blend[i].Weight * landFactor);
        if (landFactor < 1f)
        {
            List<VoronoiBiomeGenerator.BiomeWeight> ocean = GetOceanBlend(x, y);
            for (int i = 0; i < ocean.Count; i++)
                AddWeight(weights, ocean[i].Biome, ocean[i].Weight * (1f - landFactor));
        }

        if (volcanicBiome != null)
        {
            float volcanic = VolcanoGenerator.SurfaceMask(landforms, this, x, y);
            if (volcanic > 0f)
            {
                for (int i = 0; i < weights.Count; i++)
                    weights[i] = new KeyValuePair<Biome, float>(weights[i].Key, weights[i].Value * (1f - volcanic));
                AddWeight(weights, volcanicBiome, volcanic);
            }
        }

        weights.Sort((a, b) => b.Value.CompareTo(a.Value));
        var result = new List<VoronoiBiomeGenerator.BiomeWeight>(weights.Count);
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i].Value > 0f)
                result.Add(new VoronoiBiomeGenerator.BiomeWeight(weights[i].Key, weights[i].Value));
        }
        return result;
    }

    private static void AddWeight(List<KeyValuePair<Biome, float>> weights, Biome biome, float weight)
    {
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i].Key == biome)
            {
                weights[i] = new KeyValuePair<Biome, float>(biome, weights[i].Value + weight);
                return;
            }
        }
        weights.Add(new KeyValuePair<Biome, float>(biome, weight));
    }

    /// <summary>The biome used for a cell's objects/texture (nearest land biome, or the top of <see cref="GetTextureBlend"/>).</summary>
    public Biome SampleBiome(float x, float y)
    {
        if (HasSpecialBiomes)
        {
            List<VoronoiBiomeGenerator.BiomeWeight> blend = GetTextureBlend(x, y);
            return blend.Count > 0 ? blend[0].Biome : null;
        }

        TerrainGenerator tg = terrainGenerator;
        return VoronoiBiomeGenerator.GetBiomeAtPosition(
            new Vector2(x, y),
            tg.VoronoiScale,
            tg.NumVoronoiPoints,
            availableBiomes,
            tg.VoronoiSeed,
            tg.useWeightedBiome,
            tg.UseNaturalClimatePlacement,
            tg.ClimateNoiseScale,
            tg.VoronoiWarpStrength,
            tg.VoronoiWarpScale,
            tg.BiomeClusterStrength,
            tg.BiomeClusterRadius,
            tg.BiomeRepeatPenalty,
            layout
        );
    }
}

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

        float[,] paddedHeights = new float[paddedSize, paddedSize];
        float[,] resistanceMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;
        float[,] rainfallMap = erosionEnabled ? new float[paddedSize, paddedSize] : null;

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
    /// Biome-blended land height at a world position: each contributing biome's noise (including its
    /// baseElevation) weighted by its blend weight. <paramref name="relief"/> is the same without the
    /// baseElevation part - just the terrain's roughness - which the ocean reuses for seafloor detail.
    /// </summary>
    public static float LandHeightFromBlend(List<VoronoiBiomeGenerator.BiomeWeight> blend, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize, out float relief)
    {
        float height = 0f;
        float baseElevation = 0f;
        for (int i = 0; i < blend.Count; i++)
        {
            if (blend[i].Weight <= 0f)
                continue; // a nearby biome that isn't blending in yet (see LayoutOptions.NearbyReach)
            height += blend[i].Weight * ComputeBiomeNoise(blend[i].Biome, worldX, worldY, octaves, lacunarity, inverseChunkSize, inverseChunkSize);
            baseElevation += blend[i].Weight * blend[i].Biome.baseElevation;
        }

        relief = height - baseElevation;
        return height;
    }

    /// <summary>Classic (original) terrain of one biome at a world position - see <see cref="LandformType.Classic"/>.</summary>
    public static float ComputeClassicBiomeNoise(Biome biome, float worldX, float worldY, int octaves, float lacunarity, float inverseChunkSize)
    {
        return ComputeBiomeNoise(biome, worldX, worldY, octaves, lacunarity, inverseChunkSize, inverseChunkSize);
    }

    /// <summary>
    /// Applies a biome's fractal/fBm Perlin noise (amplitude, frequency, persistence) at a world
    /// position, plus its baseElevation offset (see <see cref="Biome.baseElevation"/>) - this is what
    /// lets two biomes with identical roughness still sit at genuinely different elevations.
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
