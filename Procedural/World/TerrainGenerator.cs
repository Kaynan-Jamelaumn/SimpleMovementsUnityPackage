using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;

/// <summary>
/// Available terrain sizes for chunk generation.
/// Values follow the pattern of (divisible number) + 1 for optimal LOD performance.
/// </summary>
public enum TerrainSize
{
    Small = 61,    // 60 + 1
    Medium = 121,  // 120 + 1
    Large = 181,   // 180 + 1
    ExtraLarge = 241  // 240 + 1 (maximum size)
}

/// <summary>
/// Generates terrain with customizable noise, textures, biomes, and objects.
/// Supports multithreading for map, terrain, and biome object generation.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Configuration")]
    /// <summary>
    /// Configurable size of a terrain chunk.
    /// </summary>
    [Tooltip("Size of the terrain chunk. Larger sizes provide more detail but require more processing.")]
    [SerializeField] private TerrainSize terrainSize = TerrainSize.ExtraLarge;

    /// <summary>
    /// Fixed maximum size of a terrain chunk for backward compatibility.
    /// </summary>
    [Tooltip("Fixed maximum size of a terrain chunk for backward compatibility.")]
    public static readonly int maxChunkSize = 241;

    /// <summary>
    /// Gets the current size of a terrain chunk based on the selected terrain size.
    /// </summary>
    [Tooltip("Property to get the current terrain chunk size.")]
    public int ChunkSize => (int)terrainSize;

    /// <summary>
    /// Gets the maximum chunk size (for backward compatibility).
    /// </summary>
    [Tooltip("Property to get the maximum terrain chunk size.")]
    public static int MaxChunkSize => maxChunkSize;

    [Header("Noise Configuration")]
    /// <summary>
    /// Scaling factor for terrain generation.
    /// </summary>
    [Tooltip("Scaling factor for terrain generation.")]
    [HideInInspector][SerializeField] private float scaleFactor = 1;

    /// <summary>
    /// Number of noise octaves for terrain generation.
    /// </summary>
    [Tooltip("Number of noise octaves for terrain generation.")]
    [SerializeField] private int octaves = 5;

    /// <summary>
    /// Lacunarity value for noise generation, affecting frequency scaling.
    /// </summary>
    [Tooltip("Lacunarity value for noise generation, affecting frequency scaling.")]
    [SerializeField] private float lacunarity = 2f;

    [Header("Texture")]
    /// <summary>
    /// Default texture for the terrain.
    /// </summary>
    [Tooltip("Default texture for the terrain.")]
    [HideInInspector][SerializeField] private Texture2D defaultTexture;

    /// <summary>
    /// Compute shader for generating splat maps.
    /// </summary>
    [Tooltip("Compute shader for generating splat maps.")]
    [HideInInspector][SerializeField] private ComputeShader splatMapShader;

    /// <summary>
    /// Minimum height for the terrain if it's NOT Voronoi-based texture.
    /// </summary>
    [Tooltip("Minimum height for the terrain if it's NOT Voronoi-based texture.")]
    [SerializeField] private float minHeight;

    /// <summary>
    /// Maximum height for the terrain if it's NOT Voronoi-based texture.
    /// </summary>
    [Tooltip("Maximum height for the terrain if it's NOT Voronoi-based texture.")]
    [SerializeField] private float maxHeight;

    /// <summary>
    /// Determines if terrain textures are based on Voronoi points.
    /// </summary>
    [Tooltip("Determines if terrain textures are based on Voronoi points.")]
    [SerializeField] private bool terrainTextureBasedOnVoronoiPoints = true;

    [Header("Texture Variation Settings")]
    /// <summary>
    /// Master toggle for all texture variation features. When OFF, uses original code path, which
    /// maps UVs as a plain linear (heightMapX * uvScale) with no rotation/offset - the same texture
    /// image then tiles identically every time, with zero variation, which is very noticeable for
    /// detailed textures (it reads as an obviously repeating pattern, both within one chunk, where
    /// it tiles several times, and continuing seamlessly across chunk borders). Strongly recommended on.
    /// </summary>
    [Tooltip("Master toggle for all texture variation features. Strongly recommended ON - without it, textures tile with zero variation and read as an obviously repeating pattern.")]
    [SerializeField] private bool enableTextureVariations = true;

    [Header("UV Rotation (Requires Texture Variations)")]
    /// <summary>
    /// Enable random UV rotation per chunk to reduce texture repetition.
    /// </summary>
    [Tooltip("Enable random UV rotation per chunk to reduce texture repetition.")]
    [SerializeField] private bool enableUVRotation = true;

    [Header("UV Noise Offset (Requires Texture Variations)")]
    /// <summary>
    /// Enable noise-based UV offset for additional texture variation.
    /// </summary>
    [Tooltip("Enable noise-based UV offset for additional texture variation.")]
    [SerializeField] private bool enableUVNoise = true;

    /// <summary>
    /// Strength of the noise-based UV offset.
    /// </summary>
    [Tooltip("Strength of the noise-based UV offset.")]
    [SerializeField][Range(0f, 1f)] private float uvNoiseStrength = 0.3f;

    /// <summary>
    /// Scale of the noise pattern used for UV offset.
    /// </summary>
    [Tooltip("Scale of the noise pattern used for UV offset.")]
    [SerializeField] private float uvNoiseScale = 0.1f;

    [Header("Texture Scale Variation (Requires Texture Variations)")]
    /// <summary>
    /// Enable random texture scale variation per chunk.
    /// </summary>
    [Tooltip("Enable random texture scale variation per chunk.")]
    [SerializeField] private bool enableTextureScaleVariation = true;

    /// <summary>
    /// Range of texture scale variation (multiplier).
    /// </summary>
    [Tooltip("Range of texture scale variation (multiplier).")]
    [SerializeField][Range(0.1f, 2f)] private float textureScaleVariationRange = 0.3f;

    [Header("Shader-Based Enhancements (Requires Texture Variations)")]
    /// <summary>
    /// Enable advanced shader-based texture blending and variations.
    /// </summary>
    [Tooltip("Enable advanced shader-based texture blending and variations.")]
    [SerializeField] private bool enableShaderEnhancements = false;

    /// <summary>
    /// Strength of UV rotation in shader (0-1).
    /// </summary>
    [Tooltip("Strength of UV rotation in shader (0-1).")]
    [SerializeField][Range(0f, 1f)] private float shaderUVRotationStrength = 0.5f;

    /// <summary>
    /// Scale variation for textures in shader.
    /// </summary>
    [Tooltip("Scale variation for textures in shader.")]
    [SerializeField][Range(0.1f, 3f)] private float shaderUVScaleVariation = 1.2f;

    /// <summary>
    /// Texture blend sharpness in shader.
    /// </summary>
    [Tooltip("Texture blend sharpness in shader.")]
    [SerializeField][Range(1f, 10f)] private float shaderTextureBlendSharpness = 4f;

    [Header("Voronoi Noise Configuration")]
    /// <summary>
    /// Number of Voronoi points to generate for terrain features.
    /// </summary>
    [Tooltip("Number of Voronoi points to generate for terrain features.")]
    [SerializeField] public int NumVoronoiPoints = 8;

    /// <summary>
    /// Seed value for Voronoi noise generation.
    /// </summary>
    [Tooltip("Seed value for Voronoi noise generation.")]
    [SerializeField] public int VoronoiSeed = 0;

    /// <summary>
    /// Scaling factor for Voronoi noise, affecting detail size.
    /// </summary>
    [Tooltip("Scaling factor for Voronoi noise, affecting detail size.")]
    [SerializeField] public float VoronoiScale = 350;

    /// <summary>
    /// Determines if biome selection should be weighted or random.
    /// </summary>
    [Tooltip("Determines if biome selection should be weighted or random.")]
    [SerializeField] public bool useWeightedBiome = true;

    [Header("Natural Biome Placement")]
    /// <summary>
    /// How strongly a newly placed Voronoi point's biome is pulled toward whatever biome already
    /// dominates its immediate neighborhood (already-placed nearby points). This is what actually
    /// turns scattered points into contiguous territories - it works independently of, and even
    /// with, climate-driven placement off, since it's purely about spatial proximity to neighbors.
    /// 0 disables it entirely, reproducing the original "every point rolls its biome in isolation"
    /// behavior. Keep this moderate rather than maxed out - pushed too high (close to 1), the
    /// outcome becomes close to deterministic once a pattern starts forming, which crushes organic
    /// variety and makes territories look like the same recognizable arrangement recurring everywhere.
    /// </summary>
    [Tooltip("How strongly a new biome cell is pulled toward matching its already-placed neighbors, purely by proximity. This is the main thing that turns scattered points into contiguous natural-looking territories. Keep it moderate (0.4-0.6) - too high and results become near-deterministic, looking like the same layout repeating.")]
    [SerializeField][Range(0f, 1f)] private float biomeClusterStrength = 0.5f;

    /// <summary>
    /// Radius of spatial clustering influence, expressed as a multiplier of VoronoiScale so it stays
    /// proportioned to the cell size no matter what VoronoiScale is set to.
    /// </summary>
    [Tooltip("Cluster influence radius = VoronoiScale * this multiplier. Larger values let territories grow across more cells before a different biome can take over.")]
    [SerializeField] private float biomeClusterRadiusMultiplier = 1.5f;

    /// <summary>
    /// How strongly a biome that's already common elsewhere is discouraged from spawning a brand new,
    /// disconnected patch of itself far from its existing territory. Without this, biomeClusterStrength
    /// alone only rewards growing an existing territory when a same-biome point happens to be nearby -
    /// it does nothing to stop the same biome from being independently rolled again somewhere else in
    /// the same area purely by chance, which is what produces several separate same-sized, same-shaped
    /// "islands" of one biome scattered inside a single chunk. 0 disables this (a biome can freely
    /// recur in multiple separate spots). This never penalizes legitimate contiguous growth - only
    /// picks where there's no nearby same-biome point to extend from are affected.
    /// </summary>
    [Tooltip("Discourages an already-common biome from spawning a brand new, disconnected patch far from its existing territory - this is what stops several separate same-shaped 'islands' of one biome inside a single chunk. Never penalizes growing an existing territory, only brand new disconnected ones.")]
    [SerializeField][Range(0f, 1f)] private float biomeRepeatPenalty = 0.6f;

    /// <summary>
    /// When enabled, biomes are assigned to Voronoi cells based on how well each biome's ideal
    /// temperature/moisture matches the climate at that point, instead of pure randomness -
    /// producing natural clustering (deserts in hot/dry areas, tundra in cold areas, etc.).
    /// This is an additional realism layer on top of biomeClusterStrength, not a replacement for it.
    /// </summary>
    [Tooltip("Biomes are placed by matching their ideal climate (temperature/moisture) instead of pure randomness, so similar biomes cluster naturally. Additional to biomeClusterStrength above, not a replacement for it.")]
    [SerializeField] private bool useNaturalClimatePlacement = true;

    /// <summary>
    /// Scale of the temperature/moisture noise fields, expressed as a multiplier of VoronoiScale so
    /// climate zones always span several biome cells (giving natural, clustered territories) no matter
    /// what VoronoiScale is set to. Must be well above 1 - a climate scale close to VoronoiScale still
    /// varies from cell to cell and produces the same "salt and pepper" patchwork as no climate placement at all.
    /// </summary>
    [Tooltip("Climate noise scale = VoronoiScale * this multiplier. Keep well above 1 (5-10 is a good range) so climate - and therefore biome clustering - spans several cells instead of varying cell-to-cell.")]
    [SerializeField] private float climateScaleMultiplier = 6f;

    /// <summary>
    /// World-unit strength of the domain warp applied to Voronoi cell borders, making them read as
    /// organic, wobbly boundaries instead of straight polygon edges. 0 disables warping. Internally
    /// clamped relative to the warp noise scale so borders can never fold into self-intersecting shapes.
    /// </summary>
    [Tooltip("Strength of the organic domain-warp applied to biome cell borders. 0 = straight Voronoi edges.")]
    [SerializeField] private float voronoiWarpStrength = 50f;

    /// <summary>
    /// Scale of the domain-warp noise, expressed as a multiplier of VoronoiScale so the warp's
    /// wavelength always stays proportional to the cell size no matter what VoronoiScale is set to.
    /// Too small a multiplier makes the warp oscillate multiple times across a single cell edge,
    /// tearing it into jagged, self-crossing shapes instead of a gentle organic wobble.
    /// </summary>
    [Tooltip("Warp noise scale = VoronoiScale * this multiplier. Keep at or above 1 so the warp's wavelength isn't shorter than a cell, which tears borders into jagged, self-crossing shapes.")]
    [SerializeField] private float voronoiWarpScaleMultiplier = 1.5f;

    /// <summary>
    /// Fraction (0-1) of a Voronoi cell's scale used as the smooth transition band around each biome
    /// border, so height and (optionally) texture blend gradually between biomes instead of cutting hard.
    /// </summary>
    [Tooltip("Fraction of a biome cell's size used as a smooth blend band around its border.")]
    [SerializeField][Range(0f, 1f)] private float biomeBlendRange = 0.25f;

    /// <summary>
    /// When enabled (and TerrainTextureBasedOnVoronoiPoints is on), splat map weights are blended
    /// across biome borders the same way height is, instead of a hard one-biome-per-pixel texture.
    /// </summary>
    [Tooltip("Blend splat map textures across biome borders to match the blended terrain height.")]
    [SerializeField] private bool useBiomeBlendedTexturing = true;

    [Header("Erosion")]
    /// <summary>
    /// Master toggle for the erosion post-process (thermal + hydraulic/water erosion).
    /// </summary>
    [Tooltip("Enable thermal and hydraulic (water) erosion post-processing on generated heightmaps.")]
    [SerializeField] private bool enableErosion = true;

    /// <summary>
    /// Extra heightmap cells generated around each chunk purely to give erosion the neighboring
    /// context it needs, then discarded. Should comfortably exceed how far a droplet can travel
    /// (roughly dropletLifetime cells) so neighboring chunks erode consistently near their shared edge.
    /// </summary>
    [Tooltip("Padding (in cells) generated around each chunk for erosion context, then cropped away. Should exceed the droplet's typical travel distance (roughly dropletLifetime cells).")]
    [SerializeField] private int erosionPadding = 40;

    /// <summary>
    /// Number of thermal erosion relaxation passes. Higher values produce smoother, more settled slopes.
    /// </summary>
    [Tooltip("Number of thermal erosion passes. Higher = smoother, more settled slopes.")]
    [SerializeField] private int thermalIterations = 5;

    /// <summary>
    /// Base slope angle (degrees) above which material starts sliding downhill. Scaled per-cell by biome erosion resistance.
    /// </summary>
    [Tooltip("Base slope angle (degrees) above which material starts sliding downhill.")]
    [SerializeField][Range(1f, 89f)] private float talusAngle = 33f;

    /// <summary>
    /// Fraction of excess slope height moved per thermal erosion pass.
    /// </summary>
    [Tooltip("Fraction of excess slope height moved per thermal erosion pass.")]
    [SerializeField][Range(0f, 1f)] private float thermalErosionRate = 0.5f;

    /// <summary>
    /// Average number of water droplets simulated per heightmap cell. 0 disables hydraulic erosion.
    /// </summary>
    [Tooltip("Average number of water droplets simulated per heightmap cell. 0 disables hydraulic (water) erosion.")]
    [SerializeField] private float hydraulicDropletDensity = 0.12f;

    /// <summary>
    /// Maximum number of simulation steps a single water droplet can take before it evaporates.
    /// </summary>
    [Tooltip("Maximum number of steps a single water droplet can take before evaporating.")]
    [SerializeField] private int dropletLifetime = 30;

    /// <summary>
    /// How much a droplet keeps its previous direction versus following the slope. Higher values carve straighter channels.
    /// </summary>
    [Tooltip("How much a droplet keeps its previous direction versus following the slope. Higher = straighter channels.")]
    [SerializeField][Range(0f, 1f)] private float dropletInertia = 0.05f;

    /// <summary>
    /// Multiplier controlling how much sediment a droplet can carry relative to its speed and water volume.
    /// </summary>
    [Tooltip("How much sediment a droplet can carry relative to its speed and water volume.")]
    [SerializeField] private float sedimentCapacityFactor = 4f;

    /// <summary>
    /// Minimum sediment capacity, preventing near-zero-slope droplets from carrying (and thus eroding) nothing at all.
    /// </summary>
    [Tooltip("Minimum sediment a droplet can carry, even on nearly flat ground.")]
    [SerializeField] private float minSedimentCapacity = 0.01f;

    /// <summary>
    /// How quickly a droplet erodes terrain when it has spare carrying capacity.
    /// </summary>
    [Tooltip("How quickly a droplet erodes terrain when it has spare carrying capacity.")]
    [SerializeField][Range(0f, 1f)] private float erodeSpeed = 0.3f;

    /// <summary>
    /// How quickly a droplet deposits sediment once it's over capacity.
    /// </summary>
    [Tooltip("How quickly a droplet deposits sediment once it's over capacity.")]
    [SerializeField][Range(0f, 1f)] private float depositSpeed = 0.3f;

    /// <summary>
    /// Fraction of a droplet's water lost per step. Higher values make droplets die out sooner.
    /// </summary>
    [Tooltip("Fraction of a droplet's water lost per step. Higher = droplets die out sooner.")]
    [SerializeField][Range(0f, 1f)] private float evaporateSpeed = 0.02f;

    /// <summary>
    /// Gravity constant used to accelerate droplets on downhill slopes.
    /// </summary>
    [Tooltip("Gravity constant used to accelerate droplets on downhill slopes.")]
    [SerializeField] private float erosionGravity = 4f;

    /// <summary>
    /// Radius (in cells) of the brush used to erode/deposit terrain around a droplet.
    /// </summary>
    [Tooltip("Radius (in cells) of the brush used to erode/deposit terrain around a droplet.")]
    [SerializeField][Range(1f, 6f)] private float erosionRadius = 3f;

    [Header("Other Configurations")]
    /// <summary>
    /// Level of detail for terrain generation, controlling mesh resolution.
    /// </summary>
    [Tooltip("Level of detail for terrain generation, controlling mesh resolution.")]
    [SerializeField][Range(0, 6)] private int levelOfDetail = 6;

    [Header("Biomes")]
    /// <summary>
    /// Definitions for biomes used in terrain generation.
    /// </summary>
    [Tooltip("Definitions for biomes used in terrain generation.")]
    [SerializeField] private BiomeInstance[] biomeDefinitions;

    [Header("Objects")]
    /// <summary>
    /// Determines if objects (e.g., trees, rocks) should be spawned on the terrain.
    /// </summary>
    [Tooltip("Determines if objects (e.g., trees, rocks) should be spawned on the terrain.")]
    [SerializeField] private bool shouldSpawnObjects = true;

    /// <summary>
    /// Base frequency for clustering spawned objects.
    /// </summary>
    [Tooltip("Base frequency for clustering spawned objects.")]
    [SerializeField] private float clusterBaseFrequency = 1f;

    /// <summary>
    /// Amplitude for object clustering, affecting density variations.
    /// </summary>
    [Tooltip("Amplitude for object clustering, affecting density variations.")]
    [SerializeField] private float clusterAmplitude = 1f;

    // Thread-safe queues
    private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue;
    private Queue<MapThreadInfo<DataStructure.TerrainData>> terrainDataThreadInfoQueue;
    private Queue<MapThreadInfo<BiomeObjectData>> biomeObjectDataThreadInfoQueue;

    // Properties
    public float Lacunarity => lacunarity;
    public int Octaves => octaves;
    public BiomeInstance[] BiomeDefinitions { get => biomeDefinitions; set => biomeDefinitions = value; }
    public bool TerrainTextureBasedOnVoronoiPoints => terrainTextureBasedOnVoronoiPoints;
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }
    public TerrainSize TerrainSizeValue { get => terrainSize; set => terrainSize = value; }

    // Texture Variation Properties (only active when enableTextureVariations is true)
    public bool EnableTextureVariations => enableTextureVariations;
    public bool EnableUVRotation => enableTextureVariations && enableUVRotation;
    public bool EnableUVNoise => enableTextureVariations && enableUVNoise;
    public float UVNoiseStrength => uvNoiseStrength;
    public float UVNoiseScale => uvNoiseScale;
    public bool EnableTextureScaleVariation => enableTextureVariations && enableTextureScaleVariation;
    public float TextureScaleVariationRange => textureScaleVariationRange;
    public bool EnableShaderEnhancements => enableTextureVariations && enableShaderEnhancements;
    public float ShaderUVRotationStrength => shaderUVRotationStrength;
    public float ShaderUVScaleVariation => shaderUVScaleVariation;
    public float ShaderTextureBlendSharpness => shaderTextureBlendSharpness;

    // Natural Biome Placement Properties
    public float BiomeClusterStrength => biomeClusterStrength;
    public float BiomeRepeatPenalty => biomeRepeatPenalty;
    // Both derived scales are multiples of VoronoiScale so they stay correctly proportioned to the
    // biome cell size regardless of what VoronoiScale is configured to - see the fields' tooltips.
    public float BiomeClusterRadius => VoronoiScale * biomeClusterRadiusMultiplier;
    public bool UseNaturalClimatePlacement => useNaturalClimatePlacement;
    public float ClimateNoiseScale => VoronoiScale * climateScaleMultiplier;
    public float VoronoiWarpStrength => voronoiWarpStrength;
    public float VoronoiWarpScale => VoronoiScale * voronoiWarpScaleMultiplier;
    public float BiomeBlendRange => biomeBlendRange;
    public bool UseBiomeBlendedTexturing => useBiomeBlendedTexturing;

    // Erosion Properties
    public bool EnableErosion => enableErosion;
    public int ErosionPadding => erosionPadding;
    public int ThermalIterations => thermalIterations;
    public float TalusAngle => talusAngle;
    public float ThermalErosionRate => thermalErosionRate;
    public float HydraulicDropletDensity => hydraulicDropletDensity;
    public int DropletLifetime => dropletLifetime;
    public float DropletInertia => dropletInertia;
    public float SedimentCapacityFactor => sedimentCapacityFactor;
    public float MinSedimentCapacity => minSedimentCapacity;
    public float ErodeSpeed => erodeSpeed;
    public float DepositSpeed => depositSpeed;
    public float EvaporateSpeed => evaporateSpeed;
    public float ErosionGravity => erosionGravity;
    public float ErosionRadius => erosionRadius;

    // Guards minHeight/maxHeight, which UpdateMinMaxHeight below mutates from multiple
    // concurrent per-chunk worker threads (all sharing this one TerrainGenerator instance).
    private readonly object heightRangeLock = new object();

    /// <summary>
    /// Updates the minimum and maximum height values based on the given height.
    /// Thread-safe: called once per heightmap cell from worker threads in HeightGenerator,
    /// potentially from several chunks generating concurrently.
    /// </summary>
    /// <param name="height">The height to evaluate.</param>
    public void UpdateMinMaxHeight(float height)
    {
        lock (heightRangeLock)
        {
            if (height > maxHeight) maxHeight = height;
            if (height < minHeight) minHeight = height;
        }
    }

    /// <summary>
    /// Constructor for the TerrainGenerator class.
    /// </summary>
    /// <param name="amplitude">Amplitude for noise generation.</param>
    /// <param name="lacunarity">Lacunarity for noise generation.</param>
    public TerrainGenerator(float amplitude = 100f, float lacunarity = 2f)
    {
        this.lacunarity = lacunarity;
    }

    /// <summary>
    /// Initializes the system by setting up thread-safe queues, Voronoi cache, and precomputing density maps for biome objects.
    /// </summary>
    private void Awake()
    {
        // Reset the (static, process-lifetime) Voronoi point cache before generating anything.
        // Without this, a chunk coordinate visited in a previous Play session (or before a
        // seed/scale/biome-list change) keeps serving its OLD cached points forever - the cache
        // is keyed only by chunk coordinate, not by seed, so nothing else invalidates it. This is
        // especially easy to hit with "Reload Domain" disabled in Enter Play Mode Settings, where
        // static fields like this one otherwise survive between separate Play sessions.
        VoronoiBiomeGenerator.ClearCache();

        // Initialize thread-safe queues for handling map data, terrain data, and biome object data from worker threads.
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<DataStructure.TerrainData>>();
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        // Precompute density maps for each biome object in the biome definitions.
        foreach (BiomeInstance biomeInstance in biomeDefinitions)
        {
            foreach (BiomeObject biomeObject in biomeInstance.runtimeObjects)
            {
                // If the density map for a biome object is not already computed, generate one.
                if (biomeObject.densityMap == null)
                {
                    biomeObject.densityMap = ObjectSpawner.GenerateClusteredDensityMap(
                        ChunkSize, ChunkSize,                          // Dimensions of the density map
                        biomeObject.clusterCount, biomeObject.clusterRadius,  // Cluster properties
                        clusterBaseFrequency, clusterAmplitude, scaleFactor // Frequency, amplitude, and scale for clustering
                    );
                }
            }
        }
    }


    /// <summary>
    /// Generates terrain data based on the given global offset.
    /// </summary>
    /// <param name="globalOffset">The global offset for the terrain.</param>
    /// <returns>A MapData object containing the height map.</returns>
    private MapData GenerateTerrain(Vector2 globalOffset)
    {
        // Local, not a field: this runs on its own worker thread per chunk (see RequestMapData),
        // and every chunk shares this same TerrainGenerator instance, so a shared field here would
        // race between concurrently-generating chunks.
        float[,] localHeightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);
        return new MapData(localHeightMap, null);
    }

    /// <summary>
    /// Generates a biome map based on the given global offset and height map.
    /// </summary>
    /// <param name="globalOffset">The global offset for the biome map.</param>
    /// <param name="heightMap">The height map for the terrain.</param>
    /// <returns>A 2D array of Biome objects.</returns>
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        Biome[,] biomeMap = new Biome[ChunkSize, ChunkSize];

        for (int y = 0; y < ChunkSize; y++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);
                Biome chosenBiome = VoronoiBiomeGenerator.GetBiomeAtPosition(
                    worldPos,
                    VoronoiScale,
                    NumVoronoiPoints,
                    biomeDefinitions.Select(b => b.BiomePrefab).ToList(),
                    VoronoiSeed,
                    useWeightedBiome,
                    UseNaturalClimatePlacement,
                    ClimateNoiseScale,
                    VoronoiWarpStrength,
                    VoronoiWarpScale,
                    BiomeClusterStrength,
                    BiomeClusterRadius,
                    BiomeRepeatPenalty
                );
                biomeMap[x, y] = chosenBiome;
            }
        }

        return biomeMap;
    }

    /// <summary>
    /// Handles asynchronous requests for generating map data.
    /// </summary>
    /// <param name="callback">The callback to execute when the map data is ready.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset, bool enableDebugging = false)
    {
        ThreadStart threadStart = delegate {
            MapDataThread(callback, globalOffset, enableDebugging);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Threaded method for generating map data.
    /// </summary>
    /// <param name="callback">The callback to execute with the generated map data.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    void MapDataThread(Action<MapData> callback, Vector2 globalOffset, bool enableDebugging)
    {
        MapData mapData = GenerateTerrain(globalOffset);

        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    /// <summary>
    /// Handles asynchronous requests for generating terrain data.
    /// </summary>
    /// <param name="mapData">The input map data for terrain generation.</param>
    /// <param name="callback">The callback to execute when the terrain data is ready.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    /// <param name="lod">The level of detail for the terrain mesh.</param>
    public void RequestTerrainData(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, bool enableDebugging = false, int lod = 0)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset, enableDebugging, lod);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Threaded method for generating terrain data.
    /// </summary>
    /// <param name="mapData">The input map data for terrain generation.</param>
    /// <param name="callback">The callback to execute with the generated terrain data.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    /// <param name="lod">The level of detail for the terrain mesh.</param>
    void TerrainDataThread(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, bool enableDebugging, int lod = 0)
    {
        // Pass globalOffset only if texture variations are enabled, otherwise pass Vector2.zero for original behavior
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(
            this,
            mapData.heightMap,
            levelOfDetail,
            enableDebugging,
            enableTextureVariations ? globalOffset : Vector2.zero
        );

        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap);

        DataStructure.TerrainData terrainData = new DataStructure.TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap);

        lock (terrainDataThreadInfoQueue)
        {
            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<DataStructure.TerrainData>(callback, terrainData));
        }
    }

    /// <summary>
    /// Handles asynchronous requests for generating biome object data.
    /// </summary>
    /// <param name="callback">The callback to execute when the biome object data is ready.</param>
    /// <param name="terrainData">The terrain data used for object placement.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="chunkTransform">The transform of the terrain chunk.</param>
    public void RequestBiomeObjectData(Action<BiomeObjectData> callback, DataStructure.TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        ThreadStart threadStart = delegate {
            BiomeObjectThread(callback, terrainData, globalOffset, chunkTransform, terrainData.meshData);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Threaded method for generating biome object data.
    /// </summary>
    /// <param name="callback">The callback to execute with the generated biome object data.</param>
    /// <param name="terrainData">The terrain data used for object placement.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="chunkTransform">The transform of the terrain chunk.</param>
    void BiomeObjectThread(Action<BiomeObjectData> callback, DataStructure.TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform, MeshData meshData)
    {
        if (callback == null)
        {
            Debug.LogError("Callback is null");
            return;
        }

        BiomeObjectData biomeObjectData = new BiomeObjectData(terrainData.heightMap, globalOffset, terrainData.terrainGenerator, terrainData.biomeMap, chunkTransform, meshData);

        lock (biomeObjectDataThreadInfoQueue)
        {
            biomeObjectDataThreadInfoQueue.Enqueue(new MapThreadInfo<BiomeObjectData>(callback, biomeObjectData));
        }
    }


    /// <summary>
    /// Processes queued thread results for map data, terrain data, and biome object data, updating them in the main thread.
    /// </summary>
    void Update()
    {
        // Process and update map data if any queued results are available.
        if (mapDataThreadInfoQueue.Count > 0)
        {
            // Iterate over the queued map data and call the associated callback for each result.
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        // Process and update terrain data if any queued results are available.
        if (terrainDataThreadInfoQueue.Count > 0)
        {
            // Iterate over the queued terrain data and process it.
            for (int i = 0; i < terrainDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<DataStructure.TerrainData> threadInfo = terrainDataThreadInfoQueue.Dequeue();

                // Generate splat maps if terrain texture is based on Voronoi points.
                Texture2D[] splatMap = null;
                if (terrainTextureBasedOnVoronoiPoints)
                {
                    // worldOrigin is always the chunk's true global offset (needed for biome blending);
                    // the variation offset is only passed through when texture variations are enabled.
                    splatMap = SplatMapGenerator.GenerateSplatMaps(
                        this,
                        threadInfo.parameter.biomeMap,
                        threadInfo.parameter.globalOffset,
                        enableTextureVariations ? threadInfo.parameter.globalOffset : Vector2.zero
                    );
                }

                // Assign the generated splat map to the terrain data and trigger the callback.
                threadInfo.parameter.splatMap = splatMap;
                threadInfo.callback(threadInfo.parameter);
            }
        }

        // Process and update biome object data if any queued results are available.
        if (biomeObjectDataThreadInfoQueue.Count > 0)
        {
            bool shouldBreak = false;
            int lodFactor = levelOfDetail > 0 ? levelOfDetail * 2 : 1;
            // Iterate over the queued biome object data and place objects based on biome information.
            for (int i = 0; i < biomeObjectDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<BiomeObjectData> threadInfo = biomeObjectDataThreadInfoQueue.Dequeue();

                // Loop through the terrain chunk to place objects at specific coordinates.
                for (int y = 0; y < threadInfo.parameter.terrainGenerator.ChunkSize; y++)
                {
                    if (shouldBreak) break;

                    for (int x = 0; x < threadInfo.parameter.terrainGenerator.ChunkSize; x++)
                    {
                        // Calculate the world position for the current chunk coordinates.
                        Vector2 worldPos2D = new Vector2(threadInfo.parameter.globalOffset.x + x, threadInfo.parameter.globalOffset.y + y);
                        Vector3 worldPos3D = new Vector3(worldPos2D.x, 0, worldPos2D.y);


                        // Determine the biome at the current position.
                        Biome chosenBiome = threadInfo.parameter.biomeMap[x, y];

                        // Check if objects should be spawned in the current biome.
                        if (!shouldSpawnObjects)
                        {
                            shouldBreak = true;
                            break;
                        }
                        else
                        {
                            // Find the corresponding biome instance for the chosen biome.
                            BiomeInstance chosenBiomeInstance = threadInfo.parameter.terrainGenerator.biomeDefinitions
                                .FirstOrDefault(b => b.BiomePrefab == chosenBiome);

                            // Place objects for the selected biome at the calculated position.
                            ObjectSpawner.PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiomeInstance, threadInfo.parameter.heightMap, x, y, threadInfo.parameter.meshData, lodFactor);
                        }
                    }
                }

                // Trigger the callback once the biome object data has been processed.
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    /// <summary>
    /// Thread-safe container for map thread information.
    /// </summary>
    /// <typeparam name="T">The type of data being passed.</typeparam>
    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

}