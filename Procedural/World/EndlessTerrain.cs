﻿using UnityEngine;
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

    [Tooltip("If Should use HDRP Shader if not, will use URP Shaders(Lighter)")]
    [SerializeField]
    public bool shouldUseHDRPShaders = false;

    /// <summary>
    /// The maximum distance (in world units) from the viewer within which terrain chunks are displayed.
    /// </summary>
    [Tooltip("Maximum distance (in world units) from the viewer within which terrain chunks are displayed.")]
    [SerializeField]
    public const float maxViewDst = 250;

    /// <summary>
    /// The object (e.g., player or camera) whose position determines the visibility of terrain chunks.
    /// </summary>
    [Tooltip("The object (e.g., player or camera) whose position determines terrain visibility.")]
    public Transform viewer;

    /// <summary>
    /// The viewer's position in world space, represented as a 2D coordinate (x, z).
    /// Used for efficient distance calculations, ignoring the y-axis (height).
    /// </summary>
    [Tooltip("Viewer's position in world space (x, z), ignoring height.")]
    public static Vector2 viewerPosition;

    /// <summary>
    /// Reference to the terrain generator, which creates terrain features such as height maps and textures.
    /// </summary>
    [Tooltip("Reference to the terrain generator responsible for creating terrain data.")]
    static TerrainGenerator mapGenerator;

    /// <summary>
    /// Scale factor affecting terrain features, such as size and spacing.
    /// </summary>
    [Tooltip("Scale factor affecting terrain features (size, spacing, etc.).")]
    float scaleFactor = 1.0f;

    /// <summary>
    /// The size of each terrain chunk in world units.
    /// </summary>
    [Tooltip("The size of each terrain chunk in world units.")]
    int chunkSize;

    /// <summary>
    /// Number of terrain chunks visible within the maximum viewing distance.
    /// </summary>
    [Tooltip("Number of terrain chunks visible within the maximum viewing distance.")]
    int chunksVisibleInViewDst;

    /// <summary>
    /// Enables or disables limiting the number of visible chunks per side.
    /// Helps optimize performance by reducing terrain generation beyond a certain limit.
    /// </summary>
    [Tooltip("Enable to limit the number of visible chunks per side.")]
    public bool shouldHaveMaxChunkPerSide = true;

    /// <summary>
    /// The maximum number of terrain chunks generated per side if limiting is enabled.
    /// </summary>
    [Tooltip("Maximum number of terrain chunks generated per side.")]
    public int maxChunksPerSide = 5;

    /// <summary>
    /// Dictionary storing terrain chunks, keyed by their 2D coordinates.
    /// </summary>
    [Tooltip("Dictionary storing terrain chunks, keyed by their 2D coordinates.")]
    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();

    /// <summary>
    /// List of terrain chunks that were visible during the last frame update.
    /// Used to hide chunks no longer in the viewing area.
    /// </summary>
    [Tooltip("List of terrain chunks visible during the last frame update.")]
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    /// <summary>
    /// Settings for spawning portals in the terrain system.
    /// </summary>
    [Tooltip("Configuration settings for spawning portals.")]
    [SerializeField]
    PortalSettings portalSettings;

    /// <summary>
    /// Settings for spawning mobs in the terrain system.
    /// </summary>
    [Tooltip("Configuration settings for spawning mobs.")]
    [SerializeField]
    MobSettings mobSettings;


    int count = -1;

    void Start()
    {
        mapGenerator = Object.FindFirstObjectByType<TerrainGenerator>();

        chunkSize = TerrainGenerator.chunkSize - 1;  // Adjust for overlap at edges.
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);
        scaleFactor = mapGenerator.ScaleFactor;
    }

    /// <summary>
    /// Updates the viewer's position and recalculates visible chunks each frame.
    /// </summary>
    void Update()
    {
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

                // Limit the number of chunks if maximum chunks per side is enabled.
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

                // Check if chunk exists, otherwise create it
                if (!terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
                {
                    chunk = CreateNewChunk(viewedChunkCoord);
                }

                chunk.UpdateTerrainChunk();
            }
        }

        // Update visibility for chunks not in `chunksToBeVisible`
        UpdateChunkVisibility(chunksToBeVisible);
    }

    //Create a new chunk
    TerrainChunk CreateNewChunk(Vector2 coord)
    {
        count++;
        int scaledChunkSize = Mathf.RoundToInt(chunkSize * scaleFactor);
        TerrainChunk newChunk = new TerrainChunk(coord, scaledChunkSize, transform, portalSettings, mobSettings, count, shouldUseHDRPShaders);
        terrainChunkDictionary.Add(coord, newChunk);
        return newChunk;
    }

    // Update visibility for old and new chunks
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

        /// <summary>
        /// Creates a new terrain chunk at the specified coordinates.
        /// </summary>
        /// <param name="coord">The coordinate of the chunk in chunk space.</param>
        /// <param name="size">The size of the chunk in world units.</param>
        /// <param name="parent">The parent transform for the chunk.</param>

        public TerrainChunk(Vector2 coord, int size, Transform parent, PortalSettings portalSettings, MobSettings mobSettings,  int count, bool shouldUseHDRPShaders)
        {
            this.shouldUseHDRPShaders |= shouldUseHDRPShaders;

            position = coord * size; // The chunk's world position is its grid coordinate multiplied by its size.
            bounds = new Bounds(position, Vector2.one * size);  // The chunk's bounding box.
            Vector3 positionV3 = new Vector3(position.x, 0, position.y); // Convert 2D position to 3D for the GameObject.
            globalOffset = new Vector2(position.x, position.y); // Store the global offset for terrain data.

            // Create the chunk GameObject and components.
            meshObject = new GameObject("Terrain Chunk" + count);
            meshObject.tag = "Ground";
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();


            // Add NavMeshSurface component to the terrain chunk
            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;


            // PortalManager Initialization
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


            // Request map data from the terrain generator.
            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset);
            this.shouldUseHDRPShaders = shouldUseHDRPShaders;
        }

        /// <summary>
        /// Receives map data and requests terrain data for the chunk.
        /// </summary>
        void OnMapDataReceived(MapData mapData)
        {
            // Request terrain data using the received map data.
            mapGenerator.RequestTerrainData(mapData, OnTerrainDataReceived, globalOffset);
        }

        /// <summary>
        /// Receives biome object data, initializes the biome spawner, and bakes the NavMesh for AI navigation.
        /// </summary>
        /// <param name="biomeObjectData">The data containing biome object information, including biome map and height map.</param>

        void OnBiomeObjectDataReceived(BiomeObjectData biomeObjectData)
        {

            // Bake the NavMesh for AI navigation and start spawning mobs.
            BakeNavMesh();

            MobSpawner mobSpawner = meshObject.GetComponent<MobSpawner>();
            mobSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, terrainGenerator.ChunkSize, meshObject.transform, biomeObjectData.biomeMap);

            PortalSpawner portalSpawner = meshObject.GetComponent<PortalSpawner>();
            portalSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, TerrainGenerator.chunkSize, meshObject.transform, biomeObjectData.biomeMap);
            



        }

        /// <summary>
        /// Receives terrain da ta and applies it to the chunk.
        /// </summary>
        void OnTerrainDataReceived(DataStructure.TerrainData terrainData)
        {
            // Apply terrain data to the chunk's components.
            terrainGenerator = terrainData.terrainGenerator;
            TextureGenerator textureGenerator = new TextureGenerator();
            textureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer, shouldUseHDRPShaders);
            //TextureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer);   
            heightmap = terrainData.heightMap;

            // Update the mesh and collider.
            Mesh mesh = terrainData.meshData.UpdateMesh();
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;

            // Request biome object data to populate the chunk.
            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
        }

        /// <summary>
        /// Builds the NavMesh for this chunk, allowing AI navigation.
        /// </summary>
        void BakeNavMesh()
        {
            navMeshSurface.BuildNavMesh(); // Bake the NavMesh for the chunk
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
        /// <summary>
        /// Sets the visibility of the chunk.
        /// </summary>
        /// <param name="visible">True to make the chunk visible, false to hide it.</param>
        public void SetVisible(bool visible)
        {

            meshObject.SetActive(visible);
        }
        /// <summary>
        /// Checks if the chunk is currently visible.
        /// </summary>
        /// <returns>True if visible, false otherwise.</returns>
        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }

    }
}