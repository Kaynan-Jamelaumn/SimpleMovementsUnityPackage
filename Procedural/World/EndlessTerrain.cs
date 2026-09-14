using UnityEngine;
using System.Collections.Generic;
using static DataStructure;

/// <summary>
/// Manages the creation and updating of an infinite terrain system around the viewer's position.
/// Chunks are generated in the background (nearest first) as they come within the view distance, hidden beyond
/// it, and destroyed - freeing their meshes, textures, colliders, NavMesh and objects - once they are farther
/// than the unload distance. Generation is deterministic: a chunk that comes back is generated exactly as it
/// was (same heights, water, textures and objects), and a small cache of recently unloaded chunks' data lets
/// it come back without being generated again.
/// </summary>
/// <example>
/// Attach the `EndlessTerrain` script to an empty GameObject.
/// Assign a `TerrainGenerator` script to the scene and link it to the EndlessTerrain.
/// Set a `viewer` Transform (e.g., the player's camera).
/// </example>
public partial class EndlessTerrain : MonoBehaviour
{
    [Tooltip("Enable to show debug messages in the console")]
    [SerializeField]
    public bool enableDebugging = false;

    [Tooltip("If Should use HDRP Shader if not, will use URP Shaders(Lighter)")]
    [SerializeField]
    public bool shouldUseHDRPShaders = false;

    [Tooltip("Chunks whose nearest edge is within this distance (world units) of the viewer are generated and shown.")]
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

    [Tooltip("Enable to limit the number of visible chunks per side.")]
    public bool shouldHaveMaxChunkPerSide = true;

    [Tooltip("At most this many chunks from the viewer's chunk in each direction are generated, whatever the view distance.")]
    public int maxChunksPerSide = 5;

    [Header("Chunk Lifecycle")]
    [Tooltip("Chunks whose nearest edge is farther than this (world units) from the viewer are destroyed, freeing their meshes, textures, colliders, NavMesh and objects; closer hidden chunks are only hidden. 0 = View Distance plus one chunk. A destroyed chunk that comes back into view is generated exactly as it was.")]
    public float unloadDistance = 0f;

    [Tooltip("How many unloaded chunks keep their generated data (heights, water, biomes, mesh data, object placements - no GameObjects) in memory, so they come back without being generated again. Each costs roughly 1-5 MB. 0 = none.")]
    [Range(0, 256)] public int chunkDataCacheSize = 16;

    [Tooltip("Build a NavMesh for chunks (mobs need it). It is built in the background, after the chunk's objects exist.")]
    public bool bakeNavMesh = true;

    [Tooltip("Only chunks whose nearest edge is within this distance (world units) of the viewer get a NavMesh (and start their mob and portal spawners). 0 = every chunk within the view distance.")]
    public float navMeshDistance = 250f;

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

    // Recently unloaded chunks' generated data, most recently used last.
    readonly LinkedList<CachedChunkData> dataCache = new LinkedList<CachedChunkData>();
    // Objects the game removed (see RemovePlacedObject); they stay removed when their chunk is regenerated.
    readonly HashSet<PlacedObjectId> removedObjects = new HashSet<PlacedObjectId>();
    readonly List<TerrainChunk> chunkScratch = new List<TerrainChunk>();
    readonly List<Vector2Int> offsets = new List<Vector2Int>();
    int offsetsRange = -1;

    int count = -1;

    /// <summary>The generated data of an unloaded chunk (see Chunk Data Cache Size).</summary>
    sealed class CachedChunkData
    {
        public Vector2 Coord;
        public DataStructure.TerrainData TerrainData;
        public PlacementResult Placements;
    }

    /// <summary>Chunks currently loaded (generated or being generated), visible or hidden.</summary>
    public int LoadedChunkCount => terrainChunkDictionary.Count;

    void Start()
    {
        mapGenerator = Object.FindAnyObjectByType<TerrainGenerator>();

        if (mapGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found! Please add a TerrainGenerator to the scene.");
            return;
        }

        chunkSize = mapGenerator.ChunkSize - 1;

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
            Debug.Log($"EndlessTerrain initialized - ChunkSize: {chunkSize}, ScaleFactor: {scaleFactor}, MaxViewDistance: {maxViewDst}");
    }

    void Update()
    {
        if (mapGenerator == null || viewer == null) return;

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }

    /// <summary>World units between chunk origins.</summary>
    float ChunkWorldSize => chunkSize * scaleFactor;

    /// <summary>Distance (world units) within which chunks are created: a little beyond the view distance, so they are ready when they come into view.</summary>
    float CreateDistance => maxViewDst + ChunkWorldSize * 0.25f;

    /// <summary>Distance beyond which chunks are destroyed (see Unload Distance).</summary>
    float UnloadDistance => Mathf.Max(unloadDistance > 0f ? unloadDistance : maxViewDst + ChunkWorldSize, CreateDistance + 1f);

    /// <summary>
    /// Creates the chunks that came within reach (nearest first, so they are generated in that order too), updates
    /// every loaded chunk (visibility, level of detail, collider, NavMesh), and destroys the ones far away.
    /// </summary>
    void UpdateVisibleChunks()
    {
        float size = ChunkWorldSize;
        // The chunk under the viewer: chunk (x, y) covers [x, x + 1) x [y, y + 1) chunk sizes.
        int currentChunkCoordX = Mathf.FloorToInt(viewerPosition.x / size);
        int currentChunkCoordY = Mathf.FloorToInt(viewerPosition.y / size);

        int range = Mathf.CeilToInt(CreateDistance / size);
        if (shouldHaveMaxChunkPerSide)
            range = Mathf.Min(range, Mathf.Max(0, maxChunksPerSide));
        UpdateOffsets(range);

        float createDistance = CreateDistance;
        foreach (Vector2Int offset in offsets)
        {
            Vector2 coord = new Vector2(currentChunkCoordX + offset.x, currentChunkCoordY + offset.y);
            if (terrainChunkDictionary.ContainsKey(coord))
                continue;
            if (DistanceToChunk(coord) <= createDistance)
                CreateNewChunk(coord);
        }

        float unloadDist = UnloadDistance;
        chunkScratch.Clear();
        chunkScratch.AddRange(terrainChunkDictionary.Values);
        terrainChunksVisibleLastUpdate.Clear();
        foreach (TerrainChunk chunk in chunkScratch)
        {
            Vector2 coord = chunk.Coord;
            bool inRange = Mathf.Abs(coord.x - currentChunkCoordX) <= range && Mathf.Abs(coord.y - currentChunkCoordY) <= range;
            if (!inRange || chunk.DistanceToViewer() > unloadDist)
            {
                UnloadChunk(chunk);
                continue;
            }

            chunk.UpdateTerrainChunk();
            if (chunk.IsVisible())
                terrainChunksVisibleLastUpdate.Add(chunk);
        }
    }

    /// <summary>Chunk offsets around the viewer's chunk, nearest first (rebuilt only when the range changes).</summary>
    void UpdateOffsets(int range)
    {
        if (range == offsetsRange)
            return;
        offsetsRange = range;
        offsets.Clear();
        for (int y = -range; y <= range; y++)
            for (int x = -range; x <= range; x++)
                offsets.Add(new Vector2Int(x, y));
        offsets.Sort((a, b) => (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y));
    }

    /// <summary>Distance from the viewer to the nearest point of a chunk.</summary>
    float DistanceToChunk(Vector2 coord)
    {
        float size = ChunkWorldSize;
        Vector2 min = coord * size;
        float dx = Mathf.Max(0f, Mathf.Max(min.x - viewerPosition.x, viewerPosition.x - (min.x + size)));
        float dy = Mathf.Max(0f, Mathf.Max(min.y - viewerPosition.y, viewerPosition.y - (min.y + size)));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    TerrainChunk CreateNewChunk(Vector2 coord)
    {
        count++;
        if (enableDebugging)
            Debug.Log($"Creating new chunk {count} at coord {coord}, chunkSize: {chunkSize}, scaleFactor: {scaleFactor}");

        CachedChunkData cached = TakeCachedData(coord);
        TerrainChunk newChunk = new TerrainChunk(this, coord, chunkSize, scaleFactor, transform, portalSettings, mobSettings, count, shouldUseHDRPShaders, enableDebugging, maxViewDst,
            cached != null ? cached.TerrainData : (DataStructure.TerrainData?)null, cached?.Placements);
        terrainChunkDictionary.Add(coord, newChunk);
        return newChunk;
    }

    /// <summary>Destroys a chunk and everything it owns, keeping its generated data in the cache.</summary>
    void UnloadChunk(TerrainChunk chunk)
    {
        if (chunk.TryGetGeneratedData(out DataStructure.TerrainData data, out PlacementResult placements))
            StoreCachedData(chunk.Coord, data, placements);
        chunk.Unload();
        terrainChunkDictionary.Remove(chunk.Coord);
    }

    void StoreCachedData(Vector2 coord, DataStructure.TerrainData data, PlacementResult placements)
    {
        if (chunkDataCacheSize <= 0)
            return;
        TakeCachedData(coord);
        // Not needed again: objects are placed from the cached placements.
        data.placementFields = null;
        data.splatBlend = null;
        dataCache.AddLast(new CachedChunkData { Coord = coord, TerrainData = data, Placements = placements });
        while (dataCache.Count > chunkDataCacheSize)
            dataCache.RemoveFirst();
    }

    CachedChunkData TakeCachedData(Vector2 coord)
    {
        for (LinkedListNode<CachedChunkData> node = dataCache.First; node != null; node = node.Next)
        {
            if (node.Value.Coord == coord)
            {
                dataCache.Remove(node);
                return node.Value;
            }
        }
        return null;
    }

    /// <summary>Forgets the cached data of unloaded chunks (e.g. after changing generation settings at runtime).</summary>
    public void ClearChunkDataCache()
    {
        dataCache.Clear();
    }

    /// <summary>Destroys every chunk (freeing everything they own) and forgets the cached data; chunks in view are generated again.</summary>
    public void UnloadAllChunks()
    {
        chunkScratch.Clear();
        chunkScratch.AddRange(terrainChunkDictionary.Values);
        foreach (TerrainChunk chunk in chunkScratch)
            chunk.Unload();
        terrainChunkDictionary.Clear();
        terrainChunksVisibleLastUpdate.Clear();
        dataCache.Clear();
    }

    void OnDestroy()
    {
        // This (or the whole scene) is going away: destroy the chunks' objects instead of pooling them - the pool's
        // holder can't be created or moved under objects that are being destroyed, and would be left behind.
        PlacementInstantiator instantiator = mapGenerator != null ? mapGenerator.ObjectInstantiator : null;
        if (instantiator != null)
            instantiator.PoolingSuspended = true;
        UnloadAllChunks();
        if (instantiator != null)
            instantiator.PoolingSuspended = false;
        LoadedTerrain.Clear();
    }

    // ------------------------------------------------------------------ placed objects

    /// <summary>
    /// Destroys a placed object (tree, rock...) and remembers it, so it stays gone when its chunk is unloaded and
    /// generated again. Save <see cref="RemovedObjects"/> with your game and give it back with
    /// <see cref="SetRemovedObjects"/> to keep them gone across sessions. False if it isn't a placed object.
    /// </summary>
    public bool RemovePlacedObject(GameObject placedObject)
    {
        if (placedObject == null)
            return false;
        foreach (TerrainChunk chunk in terrainChunkDictionary.Values)
        {
            if (chunk.TryGetObjectIndex(placedObject, out int index))
            {
                removedObjects.Add(new PlacedObjectId((int)chunk.Coord.x, (int)chunk.Coord.y, index));
                chunk.ForgetObject(index);
                Destroy(placedObject);
                return true;
            }
        }
        return false;
    }

    /// <summary>The identity of a placed object (false if it isn't one), e.g. to store per-object state.</summary>
    public bool TryGetPlacedObjectId(GameObject placedObject, out PlacedObjectId id)
    {
        id = default;
        if (placedObject == null)
            return false;
        foreach (TerrainChunk chunk in terrainChunkDictionary.Values)
        {
            if (chunk.TryGetObjectIndex(placedObject, out int index))
            {
                id = new PlacedObjectId((int)chunk.Coord.x, (int)chunk.Coord.y, index);
                return true;
            }
        }
        return false;
    }

    /// <summary>Placed objects removed with <see cref="RemovePlacedObject"/> (for saving).</summary>
    public IReadOnlyCollection<PlacedObjectId> RemovedObjects => removedObjects;

    /// <summary>Restores removed objects (from a save). Affects chunks generated from now on.</summary>
    public void SetRemovedObjects(IEnumerable<PlacedObjectId> ids)
    {
        removedObjects.Clear();
        if (ids != null)
            foreach (PlacedObjectId id in ids)
                removedObjects.Add(id);
    }

    internal bool IsRemoved(Vector2 coord, int index)
    {
        return removedObjects.Count > 0 && removedObjects.Contains(new PlacedObjectId((int)coord.x, (int)coord.y, index));
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
}
