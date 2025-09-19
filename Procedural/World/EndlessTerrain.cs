using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.AI.Navigation;
using System.Drawing;
using static DataStructure;
/// <summary>
/// Manages the creation and updating of an infinite terrain system around the viewer's position.
/// This system generates terrain chunks dynamically based on proximity, ensuring performance optimization 
/// by only displaying terrain chunks within a specified viewing distance.
/// </summary>
/// <example>
/// Attach the `EndlessTerrain` script to an empty GameObject.
/// Assign a `TerrainGenerator` script to the scene and link it to the EndlessTerrain.
/// Set a `viewer` Transform (e.g., the player's camera).
/// </example>
public class EndlessTerrain : MonoBehaviour
{
    [Tooltip("Enable to show debug messages in the console")]
    [SerializeField]
    public bool enableDebugging = false;

    [Tooltip("If Should use HDRP Shader if not, will use URP Shaders(Lighter)")]
    [SerializeField]
    public bool shouldUseHDRPShaders = false;

    [Tooltip("Maximum distance (in world units) from the viewer within which terrain chunks are displayed.")]
    [SerializeField]
    public const float maxViewDst = 250;

    [Tooltip("The object (e.g., player or camera) whose position determines terrain visibility.")]
    public Transform viewer;
    /// <summary>
    /// The viewer's position in world space, represented as a 2D coordinate (x, z).
    /// Used for efficient distance calculations, ignoring the y-axis (height).
    /// </summary>
    [Tooltip("Viewer's position in world space (x, z), ignoring height.")]
    public static Vector2 viewerPosition;

    [Tooltip("Reference to the terrain generator responsible for creating terrain data.")]
    static TerrainGenerator mapGenerator;

    [Tooltip("Scale factor affecting terrain features (size, spacing, etc.).")]
    [SerializeField] float scaleFactor = 1.0f;

    [Tooltip("The size of each terrain chunk in world units.")]
    int chunkSize;

    [Tooltip("Number of terrain chunks visible within the maximum viewing distance.")]
    int chunksVisibleInViewDst;

    [Tooltip("Enable to limit the number of visible chunks per side.")]
    public bool shouldHaveMaxChunkPerSide = true;

    [Tooltip("Maximum number of terrain chunks generated per side.")]
    public int maxChunksPerSide = 5;

    [Tooltip("Dictionary storing terrain chunks, keyed by their 2D coordinates.")]
    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();

    [Tooltip("List of terrain chunks visible during the last frame update.")]
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    [Tooltip("Configuration settings for spawning portals.")]
    [SerializeField]
    PortalSettings portalSettings;

    [Tooltip("Configuration settings for spawning mobs.")]
    [SerializeField]
    MobSettings mobSettings;


    int count = -1;

    void Start()
    {
        mapGenerator = Object.FindFirstObjectByType<TerrainGenerator>();

        if (mapGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found! Please add a TerrainGenerator to the scene.");
            return;
        }

        chunkSize = TerrainGenerator.chunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        // Get scaleFactor and ensure it's not 0
        scaleFactor = mapGenerator.ScaleFactor;
        if (scaleFactor <= 0)
        {
            if (enableDebugging)
                Debug.LogWarning($"TerrainGenerator ScaleFactor was {scaleFactor}, setting to 1.0");
            scaleFactor = 1.0f;
            mapGenerator.ScaleFactor = 1.0f;
        }

        if (enableDebugging)
            Debug.Log($"EndlessTerrain initialized - ChunkSize: {chunkSize}, ScaleFactor: {scaleFactor}, ChunksVisible: {chunksVisibleInViewDst}");
    }

    void Update()
    {
        if (mapGenerator == null) return;

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }
    /// <summary>
    /// Updates the visibility of terrain chunks based on the viewer's current position.
    /// This method ensures only chunks within the viewing distance are active,
    /// dynamically hiding or creating chunks as necessary.
    /// </summary>
    void UpdateVisibleChunks()
    {
        // Calculate the viewer's current chunk coordinates in the chunk grid.
        // Each chunk is `chunkSize` units wide, so we divide the viewer's position by `chunkSize`
        // and round to the nearest integer to get the chunk's grid coordinates.
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();
        // Iterate through chunks in view distance

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                // Calculate the coordinates of the chunk being considered.
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (shouldHaveMaxChunkPerSide &&
                // This condition ensures that the number of chunks generated or rendered does not exceed the maximum allowed per side.
                // It checks if the absolute value of either the x or y coordinate of the chunk exceeds the limit set by 'maxChunksPerSide'.
                // If either coordinate is outside the allowed range (greater than 'maxChunksPerSide'), the chunk is skipped to optimize performance,
                // preventing the generation or rendering of chunks that are too far from the viewer's position.
                // For example, if 'maxChunksPerSide' is 3, the system will only generate chunks within a 7x7 grid centered on the viewer (from -3 to 3 on both axes).
                (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
                {
                    continue;
                }

                chunksToBeVisible.Add(viewedChunkCoord);

                if (!terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
                {
                    chunk = CreateNewChunk(viewedChunkCoord);
                }

                chunk.UpdateTerrainChunk();
            }
        }

        UpdateChunkVisibility(chunksToBeVisible);
    }

    TerrainChunk CreateNewChunk(Vector2 coord)
    {
        count++;
        if (enableDebugging)
            Debug.Log($"Creating new chunk {count} at coord {coord}, chunkSize: {chunkSize}, scaleFactor: {scaleFactor}");

        TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, scaleFactor, transform, portalSettings, mobSettings, count, shouldUseHDRPShaders, enableDebugging);
        terrainChunkDictionary.Add(coord, newChunk);
        return newChunk;
    }

    void UpdateChunkVisibility(HashSet<Vector2> chunksToBeVisible)
    {
        for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
        {
            TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
            Vector2 chunkCoord = chunk.Position / chunkSize;

            if (!chunksToBeVisible.Contains(chunkCoord))
            {
                chunk.SetVisible(false);
                terrainChunksVisibleLastUpdate.RemoveAt(i);
            }
        }

        foreach (Vector2 coord in chunksToBeVisible)
        {
            TerrainChunk chunk = terrainChunkDictionary[coord];
            if (chunk.IsVisible() && !terrainChunksVisibleLastUpdate.Contains(chunk))
            {
                terrainChunksVisibleLastUpdate.Add(chunk);
            }
        }
    }

    public TerrainChunk GetRandomActiveChunk()
    {
        if (terrainChunksVisibleLastUpdate.Count == 0) return null;
        return terrainChunksVisibleLastUpdate[Random.Range(0, terrainChunksVisibleLastUpdate.Count)];
    }
    /// <summary>
    /// Represents a single terrain chunk in the endless terrain system.
    /// Manages its mesh, texture, collision, navigation mesh, and spawner systems dynamically.
    /// </summary>
    public class TerrainChunk
    {
        bool shouldUseHDRPShaders = false;
        bool enableDebugging = false;

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        NavMeshSurface navMeshSurface;
        private TerrainGenerator terrainGenerator;
        public float[,] heightmap;


        Vector2 globalOffset;
        int maxMobs;
        public Vector2 Position { get { return position; } }

        public TerrainChunk(Vector2 coord, int size, float scaleFactor, Transform parent, PortalSettings portalSettings, MobSettings mobSettings, int count, bool shouldUseHDRPShaders, bool enableDebugging)
        {
            this.shouldUseHDRPShaders |= shouldUseHDRPShaders;
            this.enableDebugging = enableDebugging;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            // Fix: Use position directly for globalOffset, not position * scaleFactor
            globalOffset = position;

            if (enableDebugging)
                Debug.Log($"Creating TerrainChunk {count} at coord: {coord}, position: {position}, size: {size}, scaleFactor: {scaleFactor}, globalOffset: {globalOffset}");

            meshObject = new GameObject("Terrain Chunk" + count);
            meshObject.tag = "Ground";
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();


            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;


            PortalSpawner portalSpawner = meshObject.AddComponent<PortalSpawner>();
            portalSpawner.spawnablePrefabs = portalSettings.prefabs;
            portalSpawner.globalMaxInstances = portalSettings.maxNumberOfPortals;
            portalSpawner.shouldWaitToStartSpawning = portalSettings.shouldWaitToStartSpawning;
            portalSpawner.waitingTime = portalSettings.waitingTime;
            portalSpawner.minWaitingTime = portalSettings.minWaitingTime;
            portalSpawner.maxWaitingTime = portalSettings.maxWaitingTime;
            portalSpawner.shouldHaveRandomWaitingTime = portalSettings.shouldHaveRandomWaitingTime;
            portalSpawner.retryingSpawnTime = portalSettings.retryingSpawnTime;

            MobSpawner mobSpawner = meshObject.AddComponent<MobSpawner>();
            mobSpawner.spawnablePrefabs = mobSettings.prefabs;
            mobSpawner.globalMaxInstances = mobSettings.maxNumberOfMobs;
            mobSpawner.shouldWaitToStartSpawning = mobSettings.shouldWaitToStartSpawning;
            mobSpawner.waitingTime = mobSettings.waitingTime;
            mobSpawner.minWaitingTime = mobSettings.minWaitingTime;
            mobSpawner.maxWaitingTime = mobSettings.maxWaitingTime;
            mobSpawner.shouldHaveRandomWaitingTime = mobSettings.shouldHaveRandomWaitingTime;
            mobSpawner.retryingSpawnTime = mobSettings.retryingSpawnTime;


            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);


            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset, enableDebugging);
            this.shouldUseHDRPShaders = shouldUseHDRPShaders;
        }

        void OnMapDataReceived(MapData mapData)
        {
            if (enableDebugging)
                Debug.Log($"OnMapDataReceived for chunk at {globalOffset}");
            mapGenerator.RequestTerrainData(mapData, OnTerrainDataReceived, globalOffset, enableDebugging);
        }
        /// <summary>
        /// Receives biome object data, initializes the biome spawner, and bakes the NavMesh for AI navigation.
        /// </summary>
        /// <param name="biomeObjectData">The data containing biome object information, including biome map and height map.</param>

        void OnBiomeObjectDataReceived(BiomeObjectData biomeObjectData)
        {
            if (enableDebugging)
                Debug.Log($"OnBiomeObjectDataReceived for chunk at {globalOffset}");

            BakeNavMesh();

            MobSpawner mobSpawner = meshObject.GetComponent<MobSpawner>();
            mobSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, terrainGenerator.ChunkSize, meshObject.transform, biomeObjectData.biomeMap);

            PortalSpawner portalSpawner = meshObject.GetComponent<PortalSpawner>();
            portalSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, TerrainGenerator.chunkSize, meshObject.transform, biomeObjectData.biomeMap);
        }

        void OnTerrainDataReceived(DataStructure.TerrainData terrainData)
        {
            if (enableDebugging)
                Debug.Log($"OnTerrainDataReceived for chunk at {globalOffset}");

            terrainGenerator = terrainData.terrainGenerator;
            TextureGenerator textureGenerator = new TextureGenerator();
            textureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer, shouldUseHDRPShaders);
            heightmap = terrainData.heightMap;

            Mesh mesh = terrainData.meshData.UpdateMesh();

            if (enableDebugging)
                Debug.Log($"Mesh stats - Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length / 3}, Bounds: {mesh.bounds}");

            if (mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0)
            {
                meshFilter.mesh = mesh;

                if (enableDebugging)
                    Debug.Log("Setting MeshCollider...");
                meshCollider.sharedMesh = mesh;
                if (enableDebugging)
                    Debug.Log("MeshCollider set successfully");
            }
            else
            {
                Debug.LogError($"Invalid mesh generated for chunk at {globalOffset}");
            }

            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
        }

        void BakeNavMesh()
        {
            navMeshSurface.BuildNavMesh();
        }
        /// <summary>
        /// Updates the visibility of this chunk based on its distance to the viewer.
        /// </summary>
        /// /// <remarks>
        /// Chunks are dynamically created or re-used to reduce overhead. 
        /// Visible chunks are determined by checking their distance from the viewer.
        /// Chunks outside the viewing distance are hidden but not destroyed for faster reactivation.
        /// </remarks>
        public void UpdateTerrainChunk()
        {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDstFromNearestEdge <= maxViewDst;

            SetVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }
}