
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;
/// <summary>
/// Generates terrain with customizable noise, textures, biomes, and objects.
/// Supports multithreading for map, terrain, and biome object generation.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // Constants
    /// <summary>
    /// Fixed size of a terrain chunk.
    /// </summary>
    [Tooltip("Fixed size of a terrain chunk.")]
    public const int chunkSize = 241;

    /// <summary>
    /// Gets the size of a terrain chunk.
    /// </summary>
    [Tooltip("Property to get the terrain chunk size.")]
    public int ChunkSize => chunkSize;

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

    [Header("Other Configurations")]
    /// <summary>
    /// Level of detail for terrain generation, controlling mesh resolution.
    /// </summary>
    [Tooltip("Level of detail for terrain generation, controlling mesh resolution.")]
    [SerializeField][Range(0, 6)] private int levelOfDetail=6;

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

    /// <summary>
    /// Intensity of object clustering, controlling grouping strength.
    /// </summary>
    [Tooltip("Intensity of object clustering, controlling grouping strength.")]
    [SerializeField] private float clusteringIntensity = 1f;

    // Private fields
    private float[,] heightMap;

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

    /// <summary>
    /// Updates the minimum and maximum height values based on the given height.
    /// </summary>
    /// <param name="height">The height to evaluate.</param>
    public void UpdateMinMaxHeight(float height)
    {
        if (height > MaxHeight) MaxHeight = height;
        if (height < MinHeight) MinHeight = height;
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
        // Initialize thread-safe queues for handling map data, terrain data, and biome object data from worker threads.
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<DataStructure.TerrainData>>();
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        // Initialize Voronoi cache with a seed value.
        //VoronoiCache.Instance.Initialize(VoronoiSeed);

        // Precompute density maps for each biome object in the biome definitions.
        foreach (BiomeInstance biomeInstance in biomeDefinitions)
        {
            foreach (BiomeObject biomeObject in biomeInstance.runtimeObjects)
            {
                // If the density map for a biome object is not already computed, generate one.
                if (biomeObject.densityMap == null)
                {
                    biomeObject.densityMap = ObjectSpawner.GenerateClusteredDensityMap(
                        chunkSize, chunkSize,                          // Dimensions of the density map
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
        heightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);
        return new MapData(heightMap, null);
    }

    /// <summary>
    /// Generates a biome map based on the given global offset and height map.
    /// </summary>
    /// <param name="globalOffset">The global offset for the biome map.</param>
    /// <param name="heightMap">The height map for the terrain.</param>
    /// <returns>A 2D array of Biome objects.</returns>
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        Biome[,] biomeMap = new Biome[chunkSize, chunkSize];

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);
                Biome chosenBiome = VoronoiBiomeGenerator.GetBiomeAtPosition(
                    worldPos,
                    VoronoiScale,
                    NumVoronoiPoints,
                    biomeDefinitions.Select(b => b.BiomePrefab).ToList(),
                    VoronoiSeed,
                    useWeightedBiome
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
    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset)
    {
        ThreadStart threadStart = delegate {
            MapDataThread(callback, globalOffset);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Threaded method for generating map data.
    /// </summary>
    /// <param name="callback">The callback to execute with the generated map data.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    void MapDataThread(Action<MapData> callback, Vector2 globalOffset)
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
    /// <param name="lod">The level of detail for the terrain mesh.</param>
    public void RequestTerrainData(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, int lod = 0)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset, lod);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Threaded method for generating terrain data.
    /// </summary>
    /// <param name="mapData">The input map data for terrain generation.</param>
    /// <param name="callback">The callback to execute with the generated terrain data.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="lod">The level of detail for the terrain mesh.</param>
    void TerrainDataThread(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, int lod = 0)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap, levelOfDetail);
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
            BiomeObjectThread(callback, terrainData, globalOffset, chunkTransform);
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
    void BiomeObjectThread(Action<BiomeObjectData> callback, DataStructure.TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        if (callback == null)
        {
            Debug.LogError("Callback is null");
            return;
        }

        BiomeObjectData biomeObjectData = new BiomeObjectData(terrainData.heightMap, globalOffset, terrainData.terrainGenerator, terrainData.biomeMap, chunkTransform);

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
                    splatMap = SplatMapGenerator.GenerateSplatMaps(this, threadInfo.parameter.biomeMap);
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
                            ObjectSpawner.PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiomeInstance, threadInfo.parameter.heightMap, x, y);
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
