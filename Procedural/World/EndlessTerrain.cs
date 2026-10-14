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
/// <remarks>
/// Several viewers: <see cref="SetViewers"/> streams chunks around a set of transforms at once - on a network server
/// every player out in the world needs ground, colliders and NavMesh under them. A chunk exists while it is within
/// reach of any viewer and is generated, shown and given its NavMesh by its distance to the nearest one; its level of
/// detail follows the first (primary) viewer, the one this machine looks through. With a single viewer (the
/// <see cref="viewer"/> field, single player) everything behaves exactly as with one viewer before.
/// </remarks>
/// <example>
/// Attach the `EndlessTerrain` script to an empty GameObject.
/// Assign a `TerrainGenerator` script to the scene and link it to the EndlessTerrain.
/// Set a `viewer` Transform (e.g., the player's camera).
/// </example>
public partial class EndlessTerrain : MonoBehaviour
{
    [Header("Viewer")]
    [Tooltip("REQUIRED. The object whose position decides which chunks exist - usually the player (or the camera following it). Nothing is generated while this is empty. Also the fallback 'player' for the portal and mob spawners when nothing is tagged Player. In a network session the hub world sets it (and the other viewers, see SetViewers) every frame.")]
    public Transform viewer;

    /// <summary>The (primary) viewer's position in world space (x, z), ignoring height - read by the chunks for distances.</summary>
    public static Vector2 viewerPosition;

    /// <summary>
    /// Raised on the machine that decides (offline, or the server) when a placed object is removed for good
    /// (<see cref="RemovePlacedObject"/>, <see cref="RemovePlacedObjectById"/>): the terrain and the object's id. The
    /// network relay sends it to the clients.
    /// </summary>
    public static event System.Action<EndlessTerrain, PlacedObjectId> PlacedObjectRemoved;

    /// <summary>Raised when the whole set of removed objects is replaced (<see cref="SetRemovedObjects"/>, e.g. a save loaded).</summary>
    public static event System.Action<EndlessTerrain> RemovedObjectsReplaced;

    static readonly List<EndlessTerrain> activeTerrains = new List<EndlessTerrain>();

    /// <summary>The enabled endless terrains (normally one: the World's).</summary>
    public static IReadOnlyList<EndlessTerrain> Active => activeTerrains;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activeTerrains.Clear();
        PlacedObjectRemoved = null;
        RemovedObjectsReplaced = null;
        viewerPosition = Vector2.zero;
    }

    // Found in the scene at Start (there must be exactly one TerrainGenerator).
    static TerrainGenerator mapGenerator;
    // This terrain's generator (the same one, kept per instance for its world seed).
    TerrainGenerator generator;

    // Viewers given with SetViewers (no nulls, no duplicates; the first is also `viewer`). They are used while `viewer`
    // is still the one they set: code that assigns `viewer` directly takes back the single-viewer behaviour.
    readonly List<Transform> setViewers = new List<Transform>();
    Transform setViewersPrimary;
    // The viewers streamed around this frame, the primary first.
    readonly List<Transform> currentViewers = new List<Transform>();
    // Their positions (x, z). Read by worker threads for job priorities: the array is only ever replaced by a larger
    // one and readers clamp the count to its length, so a racing read sees a slightly stale position at worst.
    Vector2[] viewerPoints = new Vector2[4];
    volatile int viewerPointCount;

    // Read from the TerrainGenerator at Start.
    float scaleFactor = 1.0f;
    int chunkSize;

    [Header("View Distance")]
    [Tooltip("Chunks whose nearest edge is within this distance (world units) of the viewer are generated and shown. A chunk is Terrain Size - 1 units wide (240 for Extra Large). Recommended 250-600: every extra ring of chunks costs generation time and memory.")]
    [Min(10f)] public float maxViewDst = 250;

    [Tooltip("Cap the number of chunks per side (see Max Chunks Per Side), whatever the view distance.")]
    public bool shouldHaveMaxChunkPerSide = true;

    [Tooltip("At most this many chunks from the viewer's chunk in each direction are generated (with Should Have Max Chunk Per Side). 5 = up to 11 x 11 chunks.")]
    [Min(0)] public int maxChunksPerSide = 5;

    [Header("Chunk Lifecycle")]
    [Tooltip("Chunks whose nearest edge is farther than this (world units) from the viewer are destroyed, freeing their meshes, textures, colliders, NavMesh, objects, portals and mobs; closer hidden chunks are only hidden. 0 = View Distance plus one chunk. A destroyed chunk that comes back into view is generated exactly as it was.")]
    [Min(0f)] public float unloadDistance = 0f;

    [Tooltip("How many unloaded chunks keep their generated data (heights, water, biomes, mesh data, object placements - no GameObjects) in memory, so they come back without being generated again. Each costs roughly 1-5 MB. 0 = none. Recommended 16-64.")]
    [Range(0, 256)] public int chunkDataCacheSize = 16;

    [Header("Navigation")]
    [Tooltip("Build a NavMesh for chunks. Needed by mobs (they walk on it and are spawned on it) and by portals with Require NavMesh. It is built in the background, after the chunk's objects exist; the chunk's portal and mob spawners start once it is done.")]
    public bool bakeNavMesh = true;

    [Tooltip("Only chunks whose nearest edge is within this distance (world units) of the viewer get a NavMesh - and start their portal and mob spawners. 0 = every chunk within the view distance. Mob Settings' Activation Distance should be below this.")]
    [Min(0f)] public float navMeshDistance = 250f;

    [Header("Rendering")]
    [Tooltip("Use the HDRP versions of the terrain shaders. Off = URP / Built-in shaders (lighter). Must match the project's render pipeline.")]
    public bool shouldUseHDRPShaders = false;

    [Header("Portals")]
    [Tooltip("Where world portals (dungeon entrances) appear, which prefabs, and what happens after use. Each chunk's PortalSpawner reads these. Needs at least one entry in Prefabs.")]
    [SerializeField]
    PortalSettings portalSettings = new PortalSettings();

    [Header("Mobs")]
    [Tooltip("Which mobs live on the terrain, how many per chunk, and when they spawn, despawn and respawn. Each chunk's MobSpawner reads these. Needs at least one entry in Prefabs and Bake NavMesh on.")]
    [SerializeField]
    MobSettings mobSettings = new MobSettings();

    [Header("Debugging")]
    [Tooltip("Log chunk creation, data arrival and spawner starts to the Console (noisy).")]
    public bool enableDebugging = false;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

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

    /// <summary>True once the terrain has found its TerrainGenerator (from Start on).</summary>
    public bool IsReady => generator != null;

    /// <summary>The world seed this terrain generates from (the TerrainGenerator's Voronoi Seed); 0 before <see cref="IsReady"/>.</summary>
    public int WorldSeed => generator != null ? generator.VoronoiSeed : 0;

    /// <summary>The transforms chunks were streamed around in the last update, the primary viewer first.</summary>
    public IReadOnlyList<Transform> Viewers => currentViewers;

    /// <summary>
    /// Streams chunks around every transform in <paramref name="transforms"/> (nulls and duplicates are skipped). The
    /// first becomes <see cref="viewer"/> - the primary viewer, whose distance decides the meshes' level of detail and
    /// whom weather, spawners and tools follow; chunks are created, kept, shown and given NavMesh by their distance to
    /// the nearest viewer. Call it whenever the set changes (calling it every frame with a reused list is fine: nothing
    /// is allocated). An empty list clears the viewers (nothing is streamed, as with an empty <see cref="viewer"/>).
    /// Assigning <see cref="viewer"/> directly afterwards returns to the single viewer.
    /// </summary>
    public void SetViewers(IReadOnlyList<Transform> transforms)
    {
        setViewers.Clear();
        if (transforms != null)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                Transform t = transforms[i];
                if (t != null && !setViewers.Contains(t))
                    setViewers.Add(t);
            }
        }
        viewer = setViewers.Count > 0 ? setViewers[0] : null;
        setViewersPrimary = viewer;
    }

    /// <summary>Goes back to the single <see cref="viewer"/> (which is kept), forgetting the other viewers given with <see cref="SetViewers"/>.</summary>
    public void ClearExtraViewers()
    {
        setViewers.Clear();
        setViewersPrimary = null;
    }

    /// <summary>The portal settings every chunk's PortalSpawner uses.</summary>
    public PortalSettings Portals => portalSettings;

    /// <summary>The mob settings every chunk's MobSpawner uses.</summary>
    public MobSettings Mobs => mobSettings;

    /// <summary>
    /// The portals planned in an area (world x, z; e.g. for map markers or quests) - known before their chunks exist.
    /// Planned points: once the chunk is ready a portal stands within Search Radius of its point, or (rarely, when
    /// no suitable spot is near) not at all.
    /// </summary>
    public void FindPortalSites(Vector2 min, Vector2 max, List<PortalSitePlanner.Site> into)
    {
        TerrainGenerator generator = mapGenerator != null ? mapGenerator : Object.FindAnyObjectByType<TerrainGenerator>();
        PortalSitePlanner.SitesInArea(portalSettings, generator != null ? generator.VoronoiSeed : 0, min, max, into);
    }

    void OnValidate()
    {
        portalSettings?.ValidateSettings();
        mobSettings?.ValidateSettings();
    }

    void Start()
    {
        mapGenerator = Object.FindAnyObjectByType<TerrainGenerator>();

        if (mapGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found! Please add a TerrainGenerator to the scene.");
            return;
        }

        generator = mapGenerator;
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

    void OnEnable()
    {
        if (!activeTerrains.Contains(this))
            activeTerrains.Add(this);
    }

    void OnDisable()
    {
        activeTerrains.Remove(this);
    }

    void Update()
    {
        if (mapGenerator == null || !CollectViewers()) return;

        UpdateVisibleChunks();
    }

    /// <summary>
    /// Gathers this frame's viewers (<see cref="viewer"/> first, then the other ones given with <see cref="SetViewers"/>)
    /// and their positions. False when there is none - nothing is updated then, as with an empty viewer before.
    /// </summary>
    bool CollectViewers()
    {
        currentViewers.Clear();
        if (viewer != null)
            currentViewers.Add(viewer);
        if (setViewers.Count > 0)
        {
            if (viewer != setViewersPrimary)
            {
                // Something assigned `viewer` directly: back to the single viewer.
                setViewers.Clear();
                setViewersPrimary = null;
            }
            else
            {
                for (int i = 0; i < setViewers.Count; i++)
                {
                    Transform t = setViewers[i];
                    if (t != null && t != viewer && !currentViewers.Contains(t))
                        currentViewers.Add(t);
                }
            }
        }

        int count = currentViewers.Count;
        if (count == 0)
        {
            viewerPointCount = 0;
            return false;
        }
        if (viewerPoints.Length < count)
        {
            // Filled before it is published, so worker threads never see an empty slot.
            var larger = new Vector2[Mathf.NextPowerOfTwo(count)];
            System.Array.Copy(viewerPoints, larger, viewerPoints.Length);
            for (int i = 0; i < count; i++)
                larger[i] = Flat(currentViewers[i].position);
            viewerPoints = larger;
        }
        else
        {
            for (int i = 0; i < count; i++)
                viewerPoints[i] = Flat(currentViewers[i].position);
        }
        viewerPointCount = count;
        viewerPosition = viewerPoints[0];
        return true;
    }

    static Vector2 Flat(Vector3 position) => new Vector2(position.x, position.z);

    /// <summary>
    /// Distance from the nearest viewer to the area min..max (thread-safe: used as job priorities). Before the first
    /// update it measures from <see cref="viewerPosition"/>, as before.
    /// </summary>
    internal float DistanceToNearestViewer(Vector2 min, Vector2 max)
    {
        Vector2[] points = viewerPoints;
        int count = viewerPointCount;
        if (count <= 0)
            return GameNet.WorldChunkMath.DistanceToRect(viewerPosition, min, max);
        return GameNet.WorldChunkMath.NearestDistanceToRect(points, count, min, max);
    }

    /// <summary>Distance from the primary viewer (the one this machine looks through) to the area min..max.</summary>
    internal float DistanceToPrimaryViewer(Vector2 min, Vector2 max)
    {
        Vector2[] points = viewerPoints;
        Vector2 primary = viewerPointCount > 0 && points.Length > 0 ? points[0] : viewerPosition;
        return GameNet.WorldChunkMath.DistanceToRect(primary, min, max);
    }

    /// <summary>World units between chunk origins.</summary>
    float ChunkWorldSize => chunkSize * scaleFactor;

    /// <summary>Distance (world units) within which chunks are created: a little beyond the view distance, so they are ready when they come into view.</summary>
    float CreateDistance => maxViewDst + ChunkWorldSize * 0.25f;

    /// <summary>Distance beyond which chunks are destroyed (see Unload Distance).</summary>
    float UnloadDistance => Mathf.Max(unloadDistance > 0f ? unloadDistance : maxViewDst + ChunkWorldSize, CreateDistance + 1f);

    /// <summary>
    /// Creates the chunks that came within reach of a viewer (the primary viewer's first, each viewer's nearest first,
    /// so they are generated in that order too), updates every loaded chunk (visibility, level of detail, collider,
    /// NavMesh), and destroys the ones far from every viewer.
    /// </summary>
    void UpdateVisibleChunks()
    {
        float size = ChunkWorldSize;
        int range = Mathf.CeilToInt(CreateDistance / size);
        if (shouldHaveMaxChunkPerSide)
            range = Mathf.Min(range, Mathf.Max(0, maxChunksPerSide));
        UpdateOffsets(range);

        Vector2[] points = viewerPoints;
        int viewerCount = Mathf.Min(viewerPointCount, points.Length);
        float createDistance = CreateDistance;
        for (int v = 0; v < viewerCount; v++)
        {
            // The chunk under the viewer: chunk (x, y) covers [x, x + 1) x [y, y + 1) chunk sizes.
            Vector2 point = points[v];
            Vector2Int center = GameNet.WorldChunkMath.ChunkCoordOf(point, size);
            foreach (Vector2Int offset in offsets)
            {
                Vector2 coord = new Vector2(center.x + offset.x, center.y + offset.y);
                if (terrainChunkDictionary.ContainsKey(coord))
                    continue;
                if (DistanceToChunk(coord, point) <= createDistance)
                    CreateNewChunk(coord);
            }
        }

        float unloadDist = UnloadDistance;
        chunkScratch.Clear();
        chunkScratch.AddRange(terrainChunkDictionary.Values);
        terrainChunksVisibleLastUpdate.Clear();
        foreach (TerrainChunk chunk in chunkScratch)
        {
            if (!IsWithinReachOfAViewer(chunk, points, viewerCount, size, range, unloadDist))
            {
                UnloadChunk(chunk);
                continue;
            }

            chunk.UpdateTerrainChunk();
            if (chunk.IsVisible())
                terrainChunksVisibleLastUpdate.Add(chunk);
        }
    }

    /// <summary>A chunk stays loaded while some viewer has it within its square of chunks and within the unload distance.</summary>
    static bool IsWithinReachOfAViewer(TerrainChunk chunk, Vector2[] points, int viewerCount, float size, int range, float unloadDist)
    {
        Vector2 coord = chunk.Coord;
        for (int v = 0; v < viewerCount; v++)
        {
            Vector2Int center = GameNet.WorldChunkMath.ChunkCoordOf(points[v], size);
            if (GameNet.WorldChunkMath.WithinChunkRange(coord, center, range) && chunk.DistanceTo(points[v]) <= unloadDist)
                return true;
        }
        return false;
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

    /// <summary>Distance from a viewer at <paramref name="point"/> to the nearest point of a chunk.</summary>
    float DistanceToChunk(Vector2 coord, Vector2 point)
    {
        float size = ChunkWorldSize;
        Vector2 min = coord * size;
        return GameNet.WorldChunkMath.DistanceToRect(point, min, min + Vector2.one * size);
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
    /// <remarks>
    /// The machine that decides does it: offline this one, in a network session the server - every client generates the
    /// same objects with the same ids (<see cref="PlacedObjectId"/>) and the network relay removes the object there
    /// too (<see cref="PlacedObjectRemoved"/>). On a network client this refuses (false): the server's removals arrive
    /// by themselves.
    /// </remarks>
    public bool RemovePlacedObject(GameObject placedObject)
    {
        if (placedObject == null || GameNet.NetAuthority.IsClientOnly)
            return false;
        foreach (TerrainChunk chunk in terrainChunkDictionary.Values)
        {
            if (chunk.TryGetObjectIndex(placedObject, out int index))
            {
                var id = new PlacedObjectId((int)chunk.Coord.x, (int)chunk.Coord.y, index);
                removedObjects.Add(id);
                chunk.ForgetObject(index);
                Destroy(placedObject);
                PlacedObjectRemoved?.Invoke(this, id);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes a placed object by its id, whether its chunk is loaded or not (if loaded, the object is destroyed; either
    /// way it is not created again). Same rules as <see cref="RemovePlacedObject"/>: false on a network client, or when
    /// it was already removed.
    /// </summary>
    public bool RemovePlacedObjectById(PlacedObjectId id)
    {
        if (GameNet.NetAuthority.IsClientOnly || id.Index < 0 || !removedObjects.Add(id))
            return false;
        DestroyLoadedPlacedObject(id);
        PlacedObjectRemoved?.Invoke(this, id);
        return true;
    }

    /// <summary>
    /// Makes this terrain's removed objects exactly <paramref name="ids"/> - the server's set, applied on a network client
    /// by the relay. Newly removed objects that are loaded are destroyed; objects no longer in the set come back the next
    /// time their chunk is generated (as with <see cref="SetRemovedObjects"/>).
    /// </summary>
    public void SyncRemovedObjects(IReadOnlyList<PlacedObjectId> ids)
    {
        syncScratch.Clear();
        if (ids != null)
            for (int i = 0; i < ids.Count; i++)
                syncScratch.Add(ids[i]);
        removedObjects.RemoveWhere(id => !syncScratch.Contains(id));
        foreach (PlacedObjectId id in syncScratch)
            if (removedObjects.Add(id))
                DestroyLoadedPlacedObject(id);
        syncScratch.Clear();
    }

    readonly HashSet<PlacedObjectId> syncScratch = new HashSet<PlacedObjectId>();

    /// <summary>Destroys the object of a loaded chunk with this id (also while the chunk's objects are still being created).</summary>
    void DestroyLoadedPlacedObject(PlacedObjectId id)
    {
        if (terrainChunkDictionary.TryGetValue(new Vector2(id.ChunkX, id.ChunkY), out TerrainChunk chunk))
        {
            GameObject placed = chunk.TakeObject(id.Index);
            if (placed != null)
                Destroy(placed);
        }
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

    /// <summary>Restores removed objects (from a save). Affects chunks generated from now on. In a network session only the server's call counts (clients get the server's set).</summary>
    public void SetRemovedObjects(IEnumerable<PlacedObjectId> ids)
    {
        removedObjects.Clear();
        if (ids != null)
            foreach (PlacedObjectId id in ids)
                removedObjects.Add(id);
        RemovedObjectsReplaced?.Invoke(this);
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
