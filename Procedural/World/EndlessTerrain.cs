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
    public float maxViewDst = 250;

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

        chunkSize = mapGenerator.ChunkSize - 1;
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
            Debug.Log($"EndlessTerrain initialized - ChunkSize: {chunkSize}, ScaleFactor: {scaleFactor}, ChunksVisible: {chunksVisibleInViewDst}, MaxViewDistance: {maxViewDst}");
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

        // Calculate the effective chunk range based on both view distance and max chunks per side setting
        int effectiveChunkRange = shouldHaveMaxChunkPerSide ?
            Mathf.Min(chunksVisibleInViewDst, maxChunksPerSide) :
            chunksVisibleInViewDst;

        if (enableDebugging && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
        {
            Debug.Log($"Effective chunk range: {effectiveChunkRange} (chunksVisibleInViewDst: {chunksVisibleInViewDst}, maxChunksPerSide: {maxChunksPerSide}, shouldHaveMaxChunkPerSide: {shouldHaveMaxChunkPerSide})");
        }

        HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();
        // Iterate through chunks in effective range

        for (int yOffset = -effectiveChunkRange; yOffset <= effectiveChunkRange; yOffset++)
        {
            for (int xOffset = -effectiveChunkRange; xOffset <= effectiveChunkRange; xOffset++)
            {
                // Calculate the coordinates of the chunk being considered.
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

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

        TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, scaleFactor, transform, portalSettings, mobSettings, count, shouldUseHDRPShaders, enableDebugging, maxViewDst);
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
    /// Draws the erosion debug overlay (see <see cref="TerrainGenerator.VisualizeErosionDebug"/>) for
    /// every currently visible chunk. Terrain chunks only exist once generated at runtime, so this only
    /// has anything to draw while in Play mode with the viewer having moved terrain into view.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (mapGenerator == null || !mapGenerator.VisualizeErosionDebug || terrainChunksVisibleLastUpdate == null)
            return;

        foreach (TerrainChunk chunk in terrainChunksVisibleLastUpdate)
        {
            chunk?.DrawErosionGizmos(mapGenerator);
        }
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

        /// <summary>
        /// Debug-only: per-cell erosion delta captured alongside <see cref="heightmap"/> when
        /// <see cref="TerrainGenerator.VisualizeErosionDebug"/> is enabled. Positive = erosion removed
        /// material at that cell, negative = erosion deposited material. Null otherwise.
        /// </summary>
        public float[,] erosionDeltaMap;

        /// <summary>Per-cell water (surface, type, shoreline level). Null when <see cref="TerrainGenerator.EnableWater"/> is off. See <see cref="WaterGenerator"/>.</summary>
        public WaterMapData waterData;

        GameObject waterObject;
        MeshFilter waterMeshFilter;
        MeshRenderer waterMeshRenderer;
        BoxCollider waterCollider;

        // Built-in fallback water materials (one per water type), shared by every chunk - unlike the
        // terrain material (which bakes per-chunk texture data into a unique Material instance), these
        // have nothing chunk-specific in them, so one shared instance each is fine.
        private static readonly Material[] fallbackWaterMaterials = new Material[WaterMeshData.SubmeshCount];
        private static readonly bool[] fallbackWaterMaterialAttempted = new bool[WaterMeshData.SubmeshCount];

        Vector2 globalOffset;
        int maxMobs;
        float maxViewDistance;
        float scaleFactor = 1f;
        public Vector2 Position { get { return position; } }

        public TerrainChunk(Vector2 coord, int size, float scaleFactor, Transform parent, PortalSettings portalSettings, MobSettings mobSettings, int count, bool shouldUseHDRPShaders, bool enableDebugging, float maxViewDistance)
        {
            this.shouldUseHDRPShaders |= shouldUseHDRPShaders;
            this.enableDebugging = enableDebugging;
            this.maxViewDistance = maxViewDistance;
            this.scaleFactor = scaleFactor;

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
            portalSpawner.SetSettings(portalSettings); // Pass the entire settings object
            portalSpawner.spawnablePrefabs = portalSettings.prefabs;
            portalSpawner.globalMaxInstances = portalSettings.maxNumberOfPortals;
            portalSpawner.shouldWaitToStartSpawning = portalSettings.shouldWaitToStartSpawning;
            portalSpawner.waitingTime = portalSettings.waitingTime;
            portalSpawner.minWaitingTime = portalSettings.minWaitingTime;
            portalSpawner.maxWaitingTime = portalSettings.maxWaitingTime;
            portalSpawner.shouldHaveRandomWaitingTime = portalSettings.shouldHaveRandomWaitingTime;
            portalSpawner.retryingSpawnTime = portalSettings.retryingSpawnTime;

            MobSpawner mobSpawner = meshObject.AddComponent<MobSpawner>();
            mobSpawner.SetSettings(mobSettings); // Pass the entire settings object
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
            portalSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, mapGenerator.ChunkSize, meshObject.transform, biomeObjectData.biomeMap);
        }

        void OnTerrainDataReceived(DataStructure.TerrainData terrainData)
        {
            if (enableDebugging)
                Debug.Log($"OnTerrainDataReceived for chunk at {globalOffset}");

            terrainGenerator = terrainData.terrainGenerator;
            TextureGenerator textureGenerator = new TextureGenerator();
            textureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer, shouldUseHDRPShaders);
            heightmap = terrainData.heightMap;
            erosionDeltaMap = terrainData.erosionDeltaMap;
            waterData = terrainData.waterData;

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

            UpdateWaterMesh(terrainData);

            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
        }

        /// <summary>
        /// Builds (or updates) this chunk's water GameObject from the water map computed alongside its
        /// heightmap - see <see cref="HeightGenerator"/>/<see cref="WaterGenerator"/>. One mesh, one
        /// submesh (and material) per water type. A no-op if water is disabled or this chunk is dry.
        /// </summary>
        void UpdateWaterMesh(DataStructure.TerrainData terrainData)
        {
            if (!terrainGenerator.EnableWater || terrainData.waterData == null)
                return;

            WaterMeshData waterMeshData = MeshGenerator.GenerateWaterMesh(terrainGenerator, terrainData.heightMap, terrainData.waterData, terrainGenerator.LevelOfDetail, globalOffset);
            if (waterMeshData == null)
                return;

            Mesh waterMesh = waterMeshData.BuildMesh();

            if (waterObject == null)
            {
                waterObject = new GameObject("Water");
                waterObject.transform.parent = meshObject.transform;
                waterObject.transform.localPosition = Vector3.zero;
                waterObject.transform.localRotation = Quaternion.identity;

                waterMeshFilter = waterObject.AddComponent<MeshFilter>();
                waterMeshRenderer = waterObject.AddComponent<MeshRenderer>();
                waterMeshRenderer.sharedMaterials = GetWaterMaterials(terrainGenerator);

                if (terrainGenerator.EnableSwimDetection)
                {
                    waterCollider = waterObject.AddComponent<BoxCollider>();
                    waterCollider.isTrigger = true;
                    waterObject.AddComponent<WaterVolume>();
                }

                // Best-effort: the consuming project may not have defined a "Water" tag, and an
                // undefined tag throws rather than silently no-opping - water still works fine
                // untagged, so this is guarded rather than allowed to abort chunk setup entirely.
                try
                {
                    waterObject.tag = "Water";
                }
                catch (UnityException)
                {
                    if (enableDebugging)
                        Debug.LogWarning("TerrainChunk: no 'Water' tag defined in this project - water objects will stay 'Untagged'. Add a 'Water' tag under Project Settings > Tags and Layers if you want to filter/query by it.");
                }
            }

            waterMeshFilter.sharedMesh = waterMesh;

            if (waterCollider != null)
            {
                UpdateWaterColliderBounds(waterCollider, terrainData.heightMap, terrainData.waterData);
            }
        }

        /// <summary>
        /// Sizes this chunk's water trigger volume as a single axis-aligned box spanning the chunk's
        /// full XZ footprint and just tall enough (from the lowest water bed to the highest water
        /// surface actually present) to contain every wet cell. This is a coarse approximation of the
        /// water body's true shape, not an exact fit - see <see cref="WaterVolume"/>'s remarks for why
        /// that's an acceptable tradeoff here (a universally trigger-compatible primitive collider
        /// rather than a non-convex mesh collider, whose trigger support varies by Unity version).
        /// </summary>
        void UpdateWaterColliderBounds(BoxCollider collider, float[,] heightMap, WaterMapData water)
        {
            int width = water.Size;
            int depth = water.Size;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool anyWater = false;

            for (int y = 0; y < depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!water.IsWet(x, y))
                        continue;

                    anyWater = true;
                    float surface = water.Surface[x, y];
                    if (surface > maxY) maxY = surface;

                    // A wet cell's terrain is always below its water surface - it's that cell's floor.
                    float bed = heightMap[x, y];
                    if (bed < minY) minY = bed;
                }
            }

            if (!anyWater)
            {
                collider.enabled = false;
                return;
            }

            collider.enabled = true;

            float chunkWorldSize = (width - 1) * scaleFactor;
            float centerY = (minY + maxY) * 0.5f;

            collider.center = new Vector3(chunkWorldSize * 0.5f, centerY, chunkWorldSize * 0.5f);
            collider.size = new Vector3(chunkWorldSize, Mathf.Max(0.1f, maxY - minY), chunkWorldSize);
        }

        /// <summary>
        /// One material per water mesh submesh (ocean, lake, pond, river - see <see cref="WaterMeshData.SubmeshIndex"/>):
        /// whatever the TerrainGenerator assigns for that type, otherwise a simple built-in transparent
        /// fallback tinted per type (created once and shared by every chunk).
        /// </summary>
        static Material[] GetWaterMaterials(TerrainGenerator terrainGenerator)
        {
            Material[] materials = new Material[WaterMeshData.SubmeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                WaterBodyType type = WaterMeshData.SubmeshType(i);
                Material assigned = terrainGenerator.GetWaterMaterial(type);
                materials[i] = assigned != null ? assigned : GetFallbackWaterMaterial(i);
            }
            return materials;
        }

        static Material GetFallbackWaterMaterial(int submesh)
        {
            if (fallbackWaterMaterials[submesh] == null && !fallbackWaterMaterialAttempted[submesh])
            {
                fallbackWaterMaterialAttempted[submesh] = true;
                fallbackWaterMaterials[submesh] = CreateFallbackWaterMaterial(FallbackWaterColor(submesh));
            }

            return fallbackWaterMaterials[submesh];
        }

        // Fully qualified: this file also has a stray `using System.Drawing;`, whose Color type would
        // otherwise make the bare `Color` identifier ambiguous with UnityEngine.Color (see the same fix in
        // DrawErosionGizmos below).
        static UnityEngine.Color FallbackWaterColor(int submesh)
        {
            switch (WaterMeshData.SubmeshType(submesh))
            {
                case WaterBodyType.Ocean: return new UnityEngine.Color(0.06f, 0.24f, 0.42f, 0.78f); // deep blue, most opaque
                case WaterBodyType.Lake: return new UnityEngine.Color(0.10f, 0.34f, 0.42f, 0.62f);  // clear blue-teal
                case WaterBodyType.Pond: return new UnityEngine.Color(0.20f, 0.34f, 0.24f, 0.66f);  // murky green
                default: return new UnityEngine.Color(0.22f, 0.44f, 0.50f, 0.55f);                  // river: lighter, shallower
            }
        }

        /// <summary>
        /// Tries a small list of shaders, in order, that are likely to exist depending on the host
        /// project's render pipeline (URP, Built-in/Standard, or an ancient/stripped project that only
        /// has the truly universal legacy/unlit ones), so this works out of the box without knowing
        /// which pipeline the consuming project uses - this package ships no shader assets of its own
        /// (see how <see cref="TextureGenerator"/> already expects "Custom/TerrainSplatMapShaderURP" to
        /// be provided by the host project; this fallback exists so water doesn't have that same
        /// hard requirement). Assign materials on the TerrainGenerator for full control over the look
        /// (reflections, flow, refraction, etc).
        /// </summary>
        static Material CreateFallbackWaterMaterial(UnityEngine.Color waterColor)
        {
            string[] candidateShaders =
            {
                "Universal Render Pipeline/Lit",
                "Standard",
                "Legacy Shaders/Transparent/Diffuse",
                "Unlit/Transparent",
                "Sprites/Default",
            };

            foreach (string shaderName in candidateShaders)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                    continue;

                Material material = new Material(shader) { color = waterColor };

                // Property names vary per shader/pipeline, so each is set only if actually present
                // rather than assumed - these are best-effort transparency hints, not required for the
                // material to work at all.
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", waterColor);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); // URP: 1 = Transparent
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f); // Standard shader: 3 = Transparent
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                return material;
            }

            Debug.LogWarning("TerrainChunk: none of the fallback shaders (URP/Standard/legacy/unlit/sprite) were found - water will render with Unity's default material. Assign water materials on the TerrainGenerator to fix this.");
            return null;
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
            bool visible = viewerDstFromNearestEdge <= maxViewDistance;

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

        /// <summary>
        /// Draws one Scene-view gizmo cube per sampled cell where erosion changed this chunk's height
        /// by more than <see cref="TerrainGenerator.ErosionDebugMinDelta"/>: red/orange where erosion
        /// removed material, blue/cyan where it deposited material, with color intensity and cube size
        /// both scaling toward <see cref="TerrainGenerator.ErosionDebugMaxDelta"/>. Only called when
        /// <see cref="TerrainGenerator.VisualizeErosionDebug"/> is enabled (see <see cref="EndlessTerrain.OnDrawGizmos"/>).
        /// </summary>
        public void DrawErosionGizmos(TerrainGenerator generator)
        {
            if (erosionDeltaMap == null || heightmap == null || meshObject == null || !meshObject.activeSelf)
                return;

            int width = erosionDeltaMap.GetLength(0);
            int depth = erosionDeltaMap.GetLength(1);
            int stride = Mathf.Max(1, generator.ErosionDebugStride);
            float minDelta = Mathf.Max(0f, generator.ErosionDebugMinDelta);
            float maxDelta = Mathf.Max(0.0001f, generator.ErosionDebugMaxDelta);
            float baseSize = Mathf.Max(0.01f, generator.ErosionDebugGizmoSize);
            float heightOffset = generator.ErosionDebugHeightOffset;
            int maxGizmos = Mathf.Max(0, generator.ErosionDebugMaxGizmosPerChunk);

            Vector3 origin = meshObject.transform.position;
            int drawn = 0;

            for (int y = 0; y < depth && drawn < maxGizmos; y += stride)
            {
                for (int x = 0; x < width && drawn < maxGizmos; x += stride)
                {
                    float delta = erosionDeltaMap[x, y];
                    float magnitude = Mathf.Abs(delta);
                    if (magnitude < minDelta)
                        continue;

                    float t = Mathf.Clamp01(magnitude / maxDelta);

                    // Erosion (material removed): orange -> red. Deposition (material added): cyan -> blue.
                    // Fully qualified: this file also has a stray `using System.Drawing;`, whose Color type
                    // would otherwise make the bare `Color` identifier ambiguous with UnityEngine.Color.
                    Gizmos.color = delta > 0f
                        ? UnityEngine.Color.Lerp(new UnityEngine.Color(1f, 0.75f, 0f, 0.55f), new UnityEngine.Color(1f, 0f, 0f, 0.95f), t)
                        : UnityEngine.Color.Lerp(new UnityEngine.Color(0f, 0.85f, 1f, 0.55f), new UnityEngine.Color(0.1f, 0.1f, 1f, 0.95f), t);

                    float worldHeight = heightmap[x, y];
                    Vector3 worldPos = origin + new Vector3(x * scaleFactor, worldHeight + heightOffset, y * scaleFactor);
                    float cubeSize = Mathf.Max(0.02f, Mathf.Lerp(baseSize * 0.35f, baseSize, t) * scaleFactor);

                    Gizmos.DrawCube(worldPos, Vector3.one * cubeSize);
                    drawn++;
                }
            }
        }
    }
}