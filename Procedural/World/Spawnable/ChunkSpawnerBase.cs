using UnityEngine;

/// <summary>
/// Base of the spawners that live on each terrain chunk (<see cref="PortalSpawner"/>, <see cref="MobSpawner"/>).
/// <see cref="EndlessTerrain"/> adds them to every chunk and calls <see cref="Begin"/> once the chunk is ready -
/// its data, objects and (when baked) NavMesh exist - and <see cref="End"/> just before it is unloaded. What they
/// spawn is parented under the chunk, so it disappears with it; whatever must survive unloading (closed portal
/// sites, killed mobs) is kept in <see cref="WorldSpawnRegistry"/>.
///
/// Spawners work in ticks driven by Update, so they pause by themselves while their chunk is hidden (inactive) and
/// carry on when it shows again. Nothing is spawned while <see cref="WorldSpawnRegistry.Paused"/> (the player is in
/// a dungeon) or while the EndlessTerrain is disabled.
/// </summary>
public abstract class ChunkSpawnerBase : MonoBehaviour
{
    [Header("Debug (read only)")]
    [Tooltip("Log what this spawner does (spawns, rejections, despawns). Set from the settings' Enable Detailed Logging.")]
    [SerializeField] protected bool detailedLogging;

    /// <summary>The chunk's coordinate in the chunk grid.</summary>
    public Vector2Int Coord { get; private set; }
    /// <summary>World (x, z) of the chunk's corner.</summary>
    public Vector2 Origin { get; private set; }
    /// <summary>World units the chunk covers per side.</summary>
    public float Span { get; private set; }
    /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
    public bool Started { get; private set; }

    protected EndlessTerrain Terrain { get; private set; }
    protected TerrainGenerator Generator { get; private set; }
    /// <summary>The world seed (TerrainGenerator's Voronoi Seed): every placement decision derives from it.</summary>
    protected int WorldSeed { get; private set; }

    private float nextTick;

    /// <summary>Things currently spawned by this spawner (for statistics and debugging).</summary>
    public abstract int ActiveCount { get; }

    /// <summary>One line describing the spawner's state (for the SpawnerManager overlay).</summary>
    public abstract string Describe();

    /// <summary>Starts the spawner for a ready chunk (called by EndlessTerrain; once).</summary>
    public void Begin(EndlessTerrain terrain, TerrainGenerator generator, Vector2Int coord, Vector2 origin, float span)
    {
        if (Started)
            return;
        Terrain = terrain;
        Generator = generator;
        Coord = coord;
        Origin = origin;
        Span = Mathf.Max(1f, span);
        WorldSeed = generator != null ? generator.VoronoiSeed : 0;
        Started = true;
        WorldSpawnRegistry.Register(this);
        // Spread the first ticks of chunks that start together over a second.
        nextTick = Time.time + PlacementRandom.Value(WorldSeed, 0x51A7, coord.x, coord.y, 0);
        OnBegin();
    }

    /// <summary>Stops the spawner before its chunk is unloaded (called by EndlessTerrain): despawns and remembers what must be.</summary>
    public void End()
    {
        if (!Started)
            return;
        Started = false;
        OnEnd();
        WorldSpawnRegistry.Unregister(this);
    }

    protected abstract void OnBegin();
    protected abstract void OnEnd();

    /// <summary>Called every <see cref="TickInterval"/> seconds while the chunk is active and started.</summary>
    protected abstract void Tick();

    /// <summary>Seconds between ticks.</summary>
    protected virtual float TickInterval => 1f;

    protected virtual void Update()
    {
        if (!Started || Time.time < nextTick)
            return;
        nextTick = Time.time + Mathf.Max(0.05f, TickInterval) * Mathf.Max(0.1f, WorldSpawnRegistry.IntervalMultiplier);
        Tick();
    }

    protected virtual void OnDestroy()
    {
        End();
    }

    /// <summary>True while spawning is paused: the player is in a dungeon, or the EndlessTerrain is disabled.</summary>
    protected bool IsPaused => WorldSpawnRegistry.Paused || Terrain == null || !Terrain.isActiveAndEnabled;

    /// <summary>The EndlessTerrain's viewer (used when nothing has the player tag).</summary>
    protected Transform Viewer => Terrain != null ? Terrain.viewer : null;

    /// <summary>True when world (x, z) is inside this chunk.</summary>
    public bool Contains(float x, float z) => x >= Origin.x && z >= Origin.y && x < Origin.x + Span && z < Origin.y + Span;

    /// <summary>Horizontal distance from a world position to the nearest point of the chunk.</summary>
    public float DistanceTo(Vector3 position)
    {
        float dx = Mathf.Max(0f, Mathf.Max(Origin.x - position.x, position.x - (Origin.x + Span)));
        float dz = Mathf.Max(0f, Mathf.Max(Origin.y - position.z, position.z - (Origin.y + Span)));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>A child of the chunk holding what this spawner creates (so it is removed with the chunk).</summary>
    protected Transform GetContainer(string containerName)
    {
        Transform t = transform.Find(containerName);
        if (t != null)
            return t;
        var go = new GameObject(containerName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    protected void Log(string message)
    {
        if (detailedLogging)
            Debug.Log($"[{GetType().Name} {Coord.x},{Coord.y}] {message}", this);
    }
}
