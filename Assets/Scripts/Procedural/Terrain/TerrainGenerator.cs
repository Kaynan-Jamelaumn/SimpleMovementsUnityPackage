using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;

/// <summary>
/// Generates terrain data and biomes using noise and Voronoi algorithms.
/// Provides tools for texture generation, biome mapping, and object placement.
/// Supports customization of terrain properties, procedural textures, and multithreaded processing.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // Constant size of each terrain chunk, designed for compatibility with different levels of detail.
    public const int chunkSize = 241; // divisble by 2, 4, 6, 8, 10, 12 for smooth LOD scaling.
    public int ChunkSize { get => chunkSize; }
    public PortalManager portalManager;

    //[SerializeField] private int gridWidthSize = 241;
    //[SerializeField] private int gridDepthSize = 241;
    [Header("Noise Configuration")]
    /// <summary>Scaling factor applied to the noise function to control terrain detail granularity.</summary>
    [SerializeField] private float scaleFactor = 3;

    /// <summary>Number of noise octaves, determining the layers of detail in the terrain.</summary>
    [SerializeField] private int octaves = 5;

    /// <summary>
    /// Lacunarity controls the frequency increase between noise octaves. Higher values create finer details.
    /// </summary>
    [SerializeField] private float lacunarity = 2f;


    [Header("Texture")]
    /// <summary>The default texture applied to the terrain in the absence of procedural texturing.</summary>
    [SerializeField] private Texture2D defaultTexture;

    /// <summary>
    /// Compute shader used to generate procedural splat maps for blending textures based on terrain features.
    /// </summary>
    [SerializeField] private ComputeShader splatMapShader;

    /// <summary>Minimum height of the terrain, used to normalize height map values.</summary>
    [SerializeField] private float minHeight;

    /// <summary>Maximum height of the terrain, used to normalize height map values.</summary>
    [SerializeField] private float maxHeight;

    /// <summary>
    /// Determines whether the terrain texture is influenced by Voronoi-based biome mappings.
    /// If true, biome regions influence texture application.
    /// </summary>
    [SerializeField] private bool terrainTextureBasedOnVoronoiPoints = true;

    [Header("Voronoi Noise Configuration")]
    /// <summary>Number of Voronoi points used for biome generation, influencing biome placement and size.</summary>
    [SerializeField] public int NumVoronoiPoints = 3;

    /// <summary>Seed value for Voronoi point generation, ensuring consistent procedural results.</summary>
    [SerializeField] public int VoronoiSeed = 0;

    /// <summary>
    /// Scaling factor applied to Voronoi calculations, adjusting the spread and influence of biome regions.
    /// </summary>
    [SerializeField] public float VoronoiScale = 1;

    [Header("Other Configurations")]
    /// <summary>Level of detail (LOD) setting for mesh generation, affecting vertex count and rendering performance.</summary>
    [SerializeField][Range(0, 6)] private int levelOfDetail;

    [Header("Biomes")]
    /// <summary>Array of biomes available for terrain generation, each defining unique characteristics.</summary>
    [SerializeField] private Biome[] biomes;

    [Header("Objects")]
    /// <summary>Determines whether objects such as trees or rocks should be spawned on the terrain.</summary>
    [SerializeField] private bool shouldSpawnObjects = true;

    /// <summary>
    /// Base frequency for cluster noise, influencing the spatial distribution of object clusters within biomes.
    /// </summary>
    [SerializeField] private float clusterBaseFrequency = 0.1f;

    /// <summary>
    /// Amplitude of the cluster noise function, controlling the intensity of object clustering.
    /// </summary>
    [SerializeField] private float clusterAmplitude = 1f;

    /// <summary>
    /// Exponential factor controlling clustering behavior. Higher values result in more concentrated clusters.
    /// </summary>
    [SerializeField] private float clusteringIntensity = 2f;

    float[,] heightMap;
    Vector2 globalOffset;
    Coroutine awaitingPortalDataRoutine;

    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }
    //public int GridDepthSize { get => gridDepthSize; }
    //public int GridWidthSize { get => gridWidthSize; }


    // Queues for multithreaded processing of terrain, map, and biome data.

    /// <summary>Queue for managing multithreaded processing of map data requests.</summary>
    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();

    /// <summary>Queue for managing multithreaded processing of terrain data requests.</summary>
    Queue<MapThreadInfo<TerrainData>> terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();

    /// <summary>Queue for managing multithreaded processing of biome object data requests.</summary>
    Queue<MapThreadInfo<BiomeObjectData>> biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();


    public Biome[] Biomes { get => biomes; set => biomes = value; }
    public bool TerrainTextureBasedOnVoronoiPoints { get => terrainTextureBasedOnVoronoiPoints;}
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }
    bool isR;

    /// <summary>
    /// Updates the minimum and maximum height values based on a given height.
    /// </summary>
    /// <param name="height">Height value to evaluate.</param>
    public void UpdateMinMaxHeight(float height)
    {
        if (height > MaxHeight) MaxHeight = height;
        if (height < MinHeight) MinHeight = height;
    }

    public TerrainGenerator(/*int width = 320, int depth = 320, */float amplitude = 100f,/* float baseFrequency = 0.4f, float persistence=1f, int octaves=5,*/ float lacunarity=2f)
    {
        //gridWidthSize = width;
        //gridDepthSize = depth;
        this.lacunarity = lacunarity;
        //cellMap = new float[width + 1, depth + 1];
    }
    private void Awake()
    {
        // Initializes thread-safe queues for multi-threaded terrain generation.
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        // Prepares Voronoi-based biome configuration with a given seed.
        VoronoiCache.Instance.Initialize(VoronoiSeed);

        // Generates clustered density maps for objects in each biome.
        foreach (Biome biome in biomes)
        {
            foreach (BiomeObject biomeObject in biome.objects)
            {
                if (biomeObject.densityMap == null)
                {
                    // Generates a density map using clustering parameters for object placement.
                    biomeObject.densityMap = GenerateClusteredDensityMap(
                        chunkSize, chunkSize,
                        biomeObject.clusterCount, biomeObject.clusterRadius,
                        clusterBaseFrequency, clusterAmplitude
                    );
                }
            }
        }
    }
    //private void OnEnable()
    //{
    //    Debug.Log($"vasco instantiated under parent: {this.transform.gameObject.name}");
    //    Debug.Log($"teste instantiated under parent: {this.transform.parent.name}");
    //    if (globalOffset != null && heightMap != null)
    //    {
    //        portalManager = GetComponent<PortalManager>();
    //        portalManager.SpawnPortalsInChunk(globalOffset, transform, heightMap, chunkSize);
    //    }
    //    else
    //    {
    //        awaitingPortalDataRoutine = StartCoroutine(WaitForInitialization());
    //    }
    //}
    //private void OnDisable()
    //{
    //    if (awaitingPortalDataRoutine != null) StopCoroutine(awaitingPortalDataRoutine);
    //    if (portalManager.portalRoutine != null) portalManager.StopSpawningPortals(transform);
    //}
    private void Start()
    {
        StartCoroutine(TestCoroutine());
    }
    private IEnumerator TestCoroutine()
    {
        while (true)
        {

            if (isR == false) isR = true;
            Debug.Log("waba zCoroutine running...");
            yield return new WaitForSeconds(3);
        }

    }
    //private IEnumerator WaitForInitialization()
    //{
    //    // Wait until both globalOffset and heightMap are not null
    //    while (globalOffset == null || heightMap == null)
    //    {
    //        yield return new WaitForSeconds(3); // Wait for the next frame
    //    }
    //    portalManager = GetComponentInParent<PortalManager>();

    //    // Once both are not null, initialize the portal manager
    //    portalManager.SpawnPortalsInChunk(globalOffset, transform, heightMap, chunkSize);
    //}


    /// <summary>
    /// Generates the terrain height map using noise-based algorithms.
    /// The height map defines the elevation of the terrain's surface.
    /// </summary>
    /// <param name="globalOffset">
    /// A 2D world-space offset used to position the terrain in a seamless manner across multiple chunks.
    /// Ensures terrain continuity and alignment in a global coordinate system.
    /// </param>
    /// <returns>
    /// A <see cref="MapData"/> object containing the generated height map.
    /// </returns>
    MapData GenerateTerrain(Vector2 globalOffset)
    {
        this.globalOffset = globalOffset;
        // Generate the height map using noise algorithms, scaled by the global offset.
        heightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);

        // Return the generated height map encapsulated in a MapData object.
        return new MapData(heightMap, null);

    }

    /// <summary>
    /// Generates a biome map using Voronoi noise for biome classification.
    /// Voronoi noise divides the terrain into regions based on proximity to random points,
    /// assigning different biomes to distinct areas.
    /// </summary>
    /// <param name="globalOffset">
    /// A 2D world-space offset to align the biome map with the terrain height map.
    /// </param>
    /// <param name="heightMap">
    /// A 2D height map used to influence biome placement. Higher elevations may correspond to specific biomes.
    /// </param>
    /// <returns>
    /// A 2D array of <see cref="Biome"/> objects representing the biome distribution across the terrain.
    /// </returns>
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        Biome[,] biomeMap = new Biome[chunkSize, chunkSize];

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                // Calculate the world position for the current cell.
                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);

                // Use Voronoi noise to assign a biome to the cell based on proximity to predefined points.
                Biome chosenBiome = NoiseGenerator.Voronoi(worldPos, VoronoiScale, NumVoronoiPoints, Biomes.ToList(), VoronoiSeed);

                // Store the biome in the biome map.
                biomeMap[x, y] = chosenBiome;
            }
        }

        return biomeMap;
    }

    /// <summary>
    /// Requests map data generation on a separate thread to avoid blocking the main thread.
    /// Useful for large-scale terrain generation without impacting performance.
    /// </summary>
    /// <param name="callback">
    /// A function to execute upon completion of map data generation.
    /// </param>
    /// <param name="globalOffset">
    /// World-space offset to align the generated map data.
    /// </param>
    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset)
    {
        // Start a new thread to execute the map data generation process.
        ThreadStart threadStart = delegate {
            MapDataThread(callback, globalOffset);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Generates map data in a separate thread and queues it for processing on the main thread.
    /// </summary>
    /// <param name="callback">Callback function to process the generated map data.</param>
    /// <param name="globalOffset">World-space offset for terrain generation.</param>
    void MapDataThread(Action<MapData> callback, Vector2 globalOffset)
    {
        // Generate the terrain height map data.
        MapData mapData = GenerateTerrain(globalOffset);

        // Queue the generated data for processing in the main thread.
        lock (mapDataThreadInfoQueue)
        {
            // Enqueue the map data along with its callback for later processing in the main thread.
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));

        }
    }
    /// <summary>
    /// Requests terrain data generation, including mesh and biome map, in a separate thread.
    /// </summary>
    /// <param name="mapData">The base map data containing height values.</param>
    /// <param name="callback">Function to execute upon completion of terrain data generation.</param>
    /// <param name="globalOffset">World-space offset for biome alignment.</param>
    /// <param name="lod">Level of detail for the generated terrain mesh.</param>
    public void RequestTerrainData(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset, int lod = 0)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset, lod);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Performs terrain data generation, including mesh and biome map creation, in a separate thread.
    /// </summary>
    /// <param name="mapData">Input height map data used for mesh and biome generation.</param>
    /// <param name="callback">Callback function for returning the generated terrain data.</param>
    /// <param name="globalOffset">World-space offset for alignment of terrain and biomes.</param>
    /// <param name="lod">Level of detail for the terrain mesh.</param>
    void TerrainDataThread(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset,
        int lod = 0)
    {
        // Generate terrain mesh data from the height map.
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap);

        // Generate a biome map based on the height map.
        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap);

        // Create a TerrainData object containing the mesh, height map, and biome map.
        TerrainData terrainData = new TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap);

        // Queue the terrain data for processing in the main thread.
        lock (terrainDataThreadInfoQueue)
        {
            // Enqueue the terrain data for main-thread processing.
            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<TerrainData>(callback, terrainData));
        }
    }




    public void RequestBiomeObjectData(Action<BiomeObjectData> callback, TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        ThreadStart threadStart = delegate {
            BiomeObjectThread(callback, terrainData, globalOffset, chunkTransform);
        };

        new Thread(threadStart).Start();
    }
    void BiomeObjectThread(Action<BiomeObjectData> callback, TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        if (callback == null)
        {
            Debug.LogError("Callback is null");
            return;
        }
        BiomeObjectData biomeObjectData = new BiomeObjectData(terrainData.heightMap,  globalOffset, terrainData.terrainGenerator, terrainData.biomeMap, chunkTransform);



        lock (biomeObjectDataThreadInfoQueue)
        {
            biomeObjectDataThreadInfoQueue.Enqueue(new MapThreadInfo<BiomeObjectData>(callback, biomeObjectData));
        }
    }

    /// <summary>
    /// Updates the queues for map, terrain, and biome object data, processing completed tasks.
    /// Ensures that results generated on separate threads are safely processed on the main thread.
    /// </summary>
    void Update()
    {
        // Process map data tasks.
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                //CreateTextureForMapData(threadInfo.parameter);
                threadInfo.callback(threadInfo.parameter);
            }
        }
        // Process terrain data tasks.
        if (terrainDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < terrainDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<TerrainData> threadInfo = terrainDataThreadInfoQueue.Dequeue();

                // Generate the splat map for the terrain based on height or biomes.
                Texture2D splatMap = new Texture2D(chunkSize, chunkSize);
                if (terrainTextureBasedOnVoronoiPoints)
                {
                    //Biome[,] biomeMap = GenerateBiomeMap(threadInfo.parameter.globalOffset, threadInfo.parameter.heightMap);
                    splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.biomeMap, splatMap);

                }
                else {splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.heightMap, splatMap);
                   

                    
                    }
                threadInfo.parameter.splatMap = splatMap;
                threadInfo.callback(threadInfo.parameter);
            }
        }
        // Process biome object data tasks.
        if (biomeObjectDataThreadInfoQueue.Count > 0)
        {
            bool shouldBreak = false; // To control the break of loops when it's not necessary to spawn objects
            for (int i = 0; i < biomeObjectDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<BiomeObjectData> threadInfo = biomeObjectDataThreadInfoQueue.Dequeue();

                // Iterate over terrain cells to place biome objects.
                for (int y = 0; y < threadInfo.parameter.terrainGenerator.ChunkSize; y++)
                {
                    if (shouldBreak) break; 
                    for (int x = 0; x < threadInfo.parameter.terrainGenerator.ChunkSize; x++)
                    {
                        //may debug the global offset
                        Vector2 worldPos2D = new Vector2(threadInfo.parameter.globalOffset.x + x, threadInfo.parameter.globalOffset.y + y);
                        Vector3 worldPos3D = new Vector3(worldPos2D.x, 0, worldPos2D.y);
                        Biome chosenBiome = threadInfo.parameter.biomeMap[x, y];
                        if (!shouldSpawnObjects)
                        {
                            shouldBreak = true;
                            break;
                        };
                            PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiome, threadInfo.parameter.heightMap, x, y);
                    }   

                }
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }


    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public  T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }

    }

    //public void PlaceObjectsForBiome(Transform chunkTransform, Vector3 worldPosition, Biome biome, float[,] heightMap, int x, int y)
    //{
    //    foreach (BiomeObject biomeObject in biome.objects)
    //    {
    //        //if (biomeObject.currentNumberOfThisObject >= biomeObject.maxNumberOfThisObject)
    //        //    continue;

    //        // Check global probability to spawn
    //        if ((UnityEngine.Random.value * 100) < biomeObject.probabilityToSpawn)
    //        {
    //            Vector3 spawnPosition = GetSpawnPosition(worldPosition, heightMap, x, y); // this function here for you to get the coordinates of the objects
    //            InstantiateBiomeObject(chunkTransform, biomeObject.terrainObject, spawnPosition, heightMap, x, y);
    //            biomeObject.currentNumberOfThisObject++;
    //        }
    //    }
    //}
    /// <summary>
    /// Places objects on the terrain for a given biome, considering various factors like clustering, probability of spawn, and slope constraints.
    /// </summary>
    /// <param name="chunkTransform">The transform of the chunk to attach the objects to.</param>
    /// <param name="worldPosition">The world position of the terrain cell being processed.</param>
    /// <param name="biome">The biome in which the object will be placed.</param>
    /// <param name="heightMap">The height map that provides the height values for the terrain.</param>
    /// <param name="x">The X index of the terrain cell being processed.</param>
    /// <param name="y">The Y index of the terrain cell being processed.</param>
    public void PlaceObjectsForBiome(Transform chunkTransform, Vector3 worldPosition, Biome biome, float[,] heightMap, int x, int y)
        {
            // Iterate through all objects defined in the biome
            foreach (BiomeObject biomeObject in biome.objects)
            {
                // Skip if the maximum number of objects has been reached for this biome object
                if (biomeObject.currentNumberOfThisObject >= biomeObject.maxNumberOfThisObject)
                    continue;


                float adjustedProbability = biomeObject.probabilityToSpawn;
                // Adjust the probability to spawn based on clustering (if enabled)
                if (biomeObject.isClusterable)
                {
                    // Retrieve density map value for clustering at the current cell
                    float densityWeight = biomeObject.densityMap[x, y];
                    // Increase spawn probability near cluster centers by amplifying the density weight
                    adjustedProbability *= Mathf.Pow(densityWeight, 2); // Amplify clustering
                }

                // Check if an object should be spawned based on a random probability roll
                if ((UnityEngine.Random.value * 100) < adjustedProbability)
                {
                    // Get the position to spawn the object, including terrain height
                    Vector3 spawnPosition = GetSpawnPosition(worldPosition, heightMap, x, y);

                    // Calculate the slope at the spawn position and skip placement if it exceeds the allowed slope threshold for the object
                    Vector3 normal = CalculateTerrainNormal(heightMap, x, y);
                    if (Vector3.Angle(Vector3.up, normal) > biomeObject.slopeThreshold)
                        continue;

                    // Check if the spawn position is free of overlapping objects
                    if (!IsPositionFree(spawnPosition, biomeObject.terrainObject))
                    continue;

                    // Instantiate the object at the determined position with correct rotation
                    InstantiateBiomeObject(chunkTransform, biomeObject.terrainObject, spawnPosition, heightMap, x, y);
                    biomeObject.currentNumberOfThisObject++; // Increment the count of objects placed
            }

            }
        }
    /// <summary>
    /// Retrieves the position on the terrain where an object should be spawned based on the height map at the given coordinates.
    /// </summary>
    /// <param name="worldPosition">The world position of the terrain cell being processed.</param>
    /// <param name="heightMap">The height map that contains terrain height values.</param>
    /// <param name="x">The X index of the terrain cell.</param>
    /// <param name="y">The Y index of the terrain cell.</param>
    /// <returns>The 3D position (X, Y, Z) for spawning the object with correct height.</returns>
    private Vector3 GetSpawnPosition(Vector3 worldPosition, float[,] heightMap, int x, int y)
    {

        // Retrieve the height value at the specific (x, y) location from the height map
        float height = heightMap[x, y];

        // Return the position with the correct height (Y-value) for the terrain
        return new Vector3(worldPosition.x, height, worldPosition.z);
    }
    /// <summary>
    /// Instantiates a biome object at a given position with a correct rotation, based on terrain normals.
    /// </summary>
    /// <param name="chunkTransform">The transform of the chunk to parent the object to.</param>
    /// <param name="prefab">The prefab of the biome object to instantiate.</param>
    /// <param name="position">The position where the object should be spawned.</param>
    /// <param name="heightMap">The height map used to adjust the object's placement.</param>
    /// <param name="x">The X index of the terrain cell being processed.</param>
    /// <param name="y">The Y index of the terrain cell being processed.</param>
    private static void InstantiateBiomeObject(Transform chunkTransform, GameObject prefab, Vector3 position, float[,] heightMap, int x, int y)
    {
        // Calculate the terrain normal at the given position (X, Y)
        Vector3 normal = CalculateTerrainNormal(heightMap, x, y);

        // Randomize the Y-axis rotation of the object to make its orientation more varied
        Quaternion randomYRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

        // Align the object’s up direction with the terrain’s normal (the slope of the terrain)
        Quaternion terrainAlignmentRotation = Quaternion.FromToRotation(Vector3.up, normal);

        // Combine both rotations: terrain alignment and random Y-axis rotation
        Quaternion finalRotation = terrainAlignmentRotation * randomYRotation;

        // Instantiate the object at the determined position with the calculated final rotation
        GameObject obj = Instantiate(prefab, position, finalRotation);
        obj.transform.parent = chunkTransform; // Parent the object to the chunk's transform
    }
    /// <summary>
    /// Calculates the terrain normal at a given position based on the surrounding height values from the height map.
    /// </summary>
    /// <param name="heightMap">The height map used to calculate the terrain's normals.</param>
    /// <param name="x">The X index of the terrain cell.</param>
    /// <param name="y">The Y index of the terrain cell.</param>
    /// <returns>The calculated normal vector of the terrain at the given position.</returns>
    private static Vector3 CalculateTerrainNormal(float[,] heightMap, int x, int y)
    {
        // Get the height values of the neighboring cells (left, right, down, up)
        float heightL = heightMap[Mathf.Max(0, x - 1), y];
        float heightR = heightMap[Mathf.Min(heightMap.GetLength(0) - 1, x + 1), y];
        float heightD = heightMap[x, Mathf.Max(0, y - 1)];
        float heightU = heightMap[x, Mathf.Min(heightMap.GetLength(1) - 1, y + 1)];

        // Calculate the normal using the cross product of vectors representing the slope in X and Y directions
        Vector3 normal = new Vector3(heightL - heightR, 2f, heightD - heightU).normalized;

        return normal;
    }

    /// <summary>
    /// Generates a density map for clustering objects on the terrain, using Perlin noise and cluster centers.
    /// </summary>
    /// <param name="width">The width of the density map.</param>
    /// <param name="height">The height of the density map.</param>
    /// <param name="clusterCount">The number of clusters to generate.</param>
    /// <param name="clusterRadius">The radius of each cluster.</param>
    /// <param name="baseFrequency">The frequency of Perlin noise for base density.</param>
    /// <param name="amplitude">The amplitude of Perlin noise to scale the density.</param>
    /// <returns>A 2D array representing the density map values, where higher values indicate more dense areas.</returns>
    private float[,] GenerateClusteredDensityMap(int width, int height, int clusterCount, float clusterRadius, float baseFrequency, float amplitude)
    {
        //amplitude: Scales the values of the noise. Higher values increase the intensity of the noise (making the contrast between low and high values more noticeable).
        //baseFrequency: Controls the "zoom level" of the Perlin noise pattern. Higher values create tighter noise patterns (smaller "islands"), while lower values produce broader, smoother patterns.
        
        // Initialize an empty density map
        float[,] densityMap = new float[width, height];
        System.Random prng = new System.Random();

        Vector2[] clusterCenters = new Vector2[clusterCount]; 
        clusterRadius = clusterRadius * scaleFactor; // Scale factor to adjust cluster size
        float[] clusterRadii = new float[clusterCount];

        // Generate random cluster centers and radii
        for (int i = 0; i < clusterCount; i++)
        {
            clusterCenters[i] = new Vector2(prng.Next(0, width), prng.Next(0, height));
            clusterRadii[i] = clusterRadius * (0.8f + (0.4f * (float)prng.NextDouble())); // Slight radius variation between 80% and 120%
            //Case 1: prng.NextDouble() = 0.0: Multiplier: 0.8f + 0.4f * 0.0 = 0.8
            //Case 2: prng.NextDouble() = 0.5: Multiplier: 0.8f + 0.4f * 0.5 = 0.8 + 0.2 = 1.0
            //Case 3: prng.NextDouble() = 1.0: Multiplier: 0.8f + 0.4f * 1.0 = 0.8 + 0.4 = 1.2

        }
        // Loop through every cell in the density map

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float maxDensity = 0f;

                // Calculate density contribution from each cluster
                foreach (Vector2 center in clusterCenters)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center); // the distance from the current cell position to the cluster center
                    float radius = clusterRadii[Array.IndexOf(clusterCenters, center)];
                    if (distance < radius) //if the distance from the cluster center is less than the cluster radius(is inside the cluster area then...)
                    {
                        // float clusterDensity = Mathf.Max(0, 1 - (distance / radius)); // Linear falloff
                        //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)
                        //Value:    1.0    0.75   0.5   0.25   0.0


                        float clusterDensity = Mathf.Pow(1 - (distance / radius), 2); // Exponential falloff   // Quadratic falloff: closer to the center -> higher density
                        //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)
                        // Value:   1.0    0.56   0.25  0.06   0.0
                        maxDensity = Mathf.Max(maxDensity, clusterDensity);
                    }
                }

                // Combine the cluster density with Perlin noise for overall terrain density
                float baseDensity = Mathf.PerlinNoise(x * baseFrequency, y * baseFrequency) * amplitude;


                // Calculate the final density value and clamp it between 0 and 1
                float rawDensity = Mathf.Clamp01(maxDensity + baseDensity);
                // Normalize the value to be between 1 and 2
                densityMap[x, y] = 1 + rawDensity; // This will map rawDensity from [0, 1] to [1, 2]
            }
        }

        return densityMap;
    }
    /// <summary>
    /// Checks if the specified position is free of any overlapping objects, ensuring that the spawn location is valid for placing a new object.
    /// </summary>
    /// <param name="position">The position in world space where the object will be placed.</param>
    /// <param name="objectPrefab">The prefab of the object being placed, used to get its collider for overlap checks.</param>
    /// <returns>
    /// Returns `true` if the position is free of any overlapping objects (other than the terrain), otherwise `false` if there is overlap.
    /// </returns>
    bool IsPositionFree(Vector3 position, GameObject objectPrefab)
    {
        // Get the collider component attached to the prefab (used for determining its size and boundaries)
        Collider collider = objectPrefab.GetComponent<Collider>();
        // If the prefab doesn't have a collider, it cannot be placed, so return false
        if (collider == null)
        {
            Debug.LogWarning($"Prefab {objectPrefab.name} has no Collider component!");
            return false;
        }

        // Calculate the size of the object (bounds size) and derive half of it to perform the overlap check
        Vector3 objectSize = collider.bounds.size;
        Vector3 halfExtents = objectSize / 2f;

        // Perform an overlap box check to detect other colliders in the area
        Collider[] hitColliders = Physics.OverlapBox(position, halfExtents, Quaternion.identity);
        // Iterate through all colliders found in the overlap area
        foreach (Collider hitCollider in hitColliders)
        {
            // If the hit object is the terrain (ground), we ignore it since it's acceptable
            if (hitCollider.gameObject.CompareTag("Ground"))
                continue; // Ignore terrain

            return false; // If a non-terrain object overlaps, return false as the position is not free
        }

        return true; // No overlap with non-terrain objects, return true to indicate the position is free
    }


}

/// <summary>
/// Represents a biome within the terrain. A biome defines its characteristics, 
/// including its height range, texture, terrain variation, and associated objects and mobs.
/// </summary>
[System.Serializable]
public class Biome
{
    /// <summary>Name of the biome.</summary>
    public string name;

    /// <summary>Minimum height value for the biome. Used to define its elevation range.</summary>
    public float minHeight;

    /// <summary>Maximum height value for the biome. Used to define its elevation range.</summary>
    public float maxHeight;

    /// <summary>Texture associated with this biome, used for rendering its appearance.</summary>
    public Texture2D texture;

    /// <summary>Amplitude of height variations within the biome.</summary>
    public float amplitude;

    /// <summary>Frequency of height details within the biome. Higher values result in more details.</summary>
    public float frequency;

    /// <summary>
    /// Persistence controls the detail added or removed at each noise octave. 
    /// Ranges from 0 to 1, where higher values retain more detail.
    /// </summary>
    [Range(0, 1)]
    public float persistence = 1;

    /// <summary>List of objects that can spawn within this biome.</summary>
    public List<BiomeObject> objects;

    /// <summary>Maximum number of objects that can exist within this biome.</summary>
    public float maxNumberOfObjects;

    /// <summary>List of mobs that can spawn within this biome.</summary>
    public List<SpawnableMob> mobList;
}

/// <summary>
/// Represents an object that can be placed in a biome, such as trees, rocks, or structures.
/// Includes properties for spawning probability, clustering, and slope restrictions.
/// </summary>
[System.Serializable]
public class BiomeObject
{
    /// <summary>The prefab representing the object to place in the biome.</summary>
    public GameObject terrainObject;

    /// <summary>Weight affecting the likelihood of this object spawning compared to others.</summary>
    public float weight;

    /// <summary>Probability (out of 100) for this object to spawn in the biome.</summary>
    public float probabilityToSpawn;

    /// <summary>Current count of this object within the biome.</summary>
    public int currentNumberOfThisObject;

    /// <summary>Maximum allowable count of this object within the biome.</summary>
    public int maxNumberOfThisObject;

    /// <summary>Maximum slope angle (in degrees) where this object can be placed.</summary>
    public float slopeThreshold = 80f;

    /// <summary>Density map representing the distribution of this object within the biome.</summary>
    public float[,] densityMap;

    /// <summary>Indicates whether this object supports clustering.</summary>
    public bool isClusterable = true;

    /// <summary>Number of clusters for this object in the biome.</summary>
    public int clusterCount = 5;

    /// <summary>Radius of each cluster, determining object grouping proximity.</summary>
    public float clusterRadius = 50f;
}

/// <summary>
/// Contains height map data and optional splat map for the terrain. 
/// Used for rendering and biome assignment.
/// </summary>
public struct MapData
{
    /// <summary>2D array representing the terrain's elevation values.</summary>
    public readonly float[,] heightMap;

    /// <summary>Optional texture representing terrain textures or splat layers.</summary>
    public Texture2D splatMap;

    /// <summary>Initializes a new instance of the <see cref="MapData"/> struct.</summary>
    /// <param name="heightMap">The terrain's elevation values.</param>
    /// <param name="splatMap">Optional splat map for texture representation.</param>
    public MapData(float[,] heightMap, Texture2D splatMap = null)
    {
        this.heightMap = heightMap;
        this.splatMap = splatMap;
    }
}

/// <summary>
/// Holds all data related to a generated terrain chunk, including its mesh, height map, 
/// biome map, and splat map.
/// </summary>
public struct TerrainData
{
    /// <summary>Mesh data for the terrain, including vertices and triangles.</summary>
    public readonly MeshData meshData;

    /// <summary>Texture applied to the terrain, representing biome or material distribution.</summary>
    public Texture2D splatMap;

    /// <summary>Height map used for generating terrain elevation.</summary>
    public readonly float[,] heightMap;

    /// <summary>The terrain generator that created this chunk.</summary>
    public readonly TerrainGenerator terrainGenerator;

    /// <summary>World-space offset for positioning the terrain in the global system.</summary>
    public Vector2 globalOffset;

    /// <summary>2D array of biomes corresponding to terrain cells.</summary>
    public Biome[,] biomeMap;

    /// <summary>Initializes a new instance of the <see cref="TerrainData"/> struct.</summary>
    /// <param name="meshData">Generated mesh data for the terrain.</param>
    /// <param name="splatMap">Optional texture map for the terrain.</param>
    /// <param name="heightMap">Elevation values of the terrain.</param>
    /// <param name="terrainGenerator">Reference to the terrain generator instance.</param>
    /// <param name="globalOffset">World-space offset for terrain positioning.</param>
    /// <param name="biomeMap">Map of biomes across the terrain.</param>
    public TerrainData(
        MeshData meshData,
        Texture2D splatMap,
        float[,] heightMap,
        TerrainGenerator terrainGenerator,
        Vector2 globalOffset,
        Biome[,] biomeMap)
    {
        this.meshData = meshData;
        this.splatMap = splatMap;
        this.heightMap = heightMap;
        this.terrainGenerator = terrainGenerator;
        this.globalOffset = globalOffset;
        this.biomeMap = biomeMap;
    }
}

/// <summary>
/// Represents data for objects spawned within biomes, including placement details and biome references.
/// </summary>
public struct BiomeObjectData
{
    /// <summary>Height map of the terrain for object placement decisions.</summary>
    public readonly float[,] heightMap;

    /// <summary>World-space offset to position objects globally.</summary>
    public Vector2 globalOffset;

    /// <summary>Reference to the terrain generator that spawned this data.</summary>
    public readonly TerrainGenerator terrainGenerator;

    /// <summary>Map of biomes used for object placement logic.</summary>
    public readonly Biome[,] biomeMap;

    /// <summary>Transform of the chunk where objects will be placed.</summary>
    public readonly Transform chunkTransform;

    /// <summary>Initializes a new instance of the <see cref="BiomeObjectData"/> struct.</summary>
    /// <param name="heightMap">Height map of the terrain.</param>
    /// <param name="globalOffset">World-space offset for object placement.</param>
    /// <param name="terrainGenerator">Reference to the terrain generator.</param>
    /// <param name="biomeMap">Biome map for object distribution.</param>
    /// <param name="chunkTransform">Transform for object placement within the chunk.</param>
    public BiomeObjectData(
        float[,] heightMap,
        Vector2 globalOffset,
        TerrainGenerator terrainGenerator,
        Biome[,] biomeMap,
        Transform chunkTransform)
    {
        this.heightMap = heightMap;
        this.globalOffset = globalOffset;
        this.terrainGenerator = terrainGenerator;
        this.biomeMap = biomeMap;
        this.chunkTransform = chunkTransform;
    }
}


//I need help improving the way I spawn objects on a Unity terrain. Specifically, I create clusters of specific object types in designated areas of the terrain. This means objects of a particular type should spawn more frequently within their respective clusters or regions, creating a natural-looking distribution.

//Please review my current implementation and either enhance it to include better this functionality or provide an entirely new solution that achieves this clustering effect effectively and efficiently or better object spawning