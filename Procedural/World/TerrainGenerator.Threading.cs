using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static DataStructure;
using Debug = UnityEngine.Debug;

// TerrainGenerator, part 4 of 4: generating chunks on a pool of worker threads, and applying the results on the
// main thread a little at a time (see TerrainGenerator.cs).
//
// Every request takes an optional priority - normally its chunk's distance to the viewer, lower runs sooner -
// that the worker threads re-evaluate each time they pick a job, so the chunks next to the player are always
// generated first; and an optional WorkToken that, once cancelled (the chunk was unloaded), drops the job and
// its result. Results reach the main thread through a queue that Update works through within Main Thread
// Budget Ms per frame, and objects are created within Object Spawn Budget Ms (see PlacementInstantiator).
public partial class TerrainGenerator : MonoBehaviour
{
    // Created on first use rather than in Awake, so none of these can be null - Unity leaves fields it doesn't
    // serialize null e.g. after scripts are recompiled while the game is running.
    private static readonly object mainThreadLock = new object();
    private static readonly object poolLock = new object();
    private TerrainWorkerPool workerPool;
    private Queue<Action> mainThreadQueue;
    private PlacementInstantiator objectInstantiator;
    private FarObjectSwitcher farObjectSwitcher;
    private PlacementPlan placementPlan;
    private Stopwatch frameWatch;

    // Biome -> splat map index, worked out on the main thread (only it may read asset names) for the workers.
    private Dictionary<string, int> splatIndexByName;
    private Dictionary<Biome, int> splatIndexByBiome;

    /// <summary>Biome name -> splat index, prepared on the main thread (null until <see cref="PrepareForGeneration"/>).</summary>
    public Dictionary<string, int> SplatIndexByName => splatIndexByName;
    /// <summary>Biome -> splat index, prepared on the main thread (null until <see cref="PrepareForGeneration"/>).</summary>
    public Dictionary<Biome, int> SplatIndexByBiome => splatIndexByBiome;

    /// <summary>The worker threads that generate chunks (see Worker Threads); created on first use.</summary>
    public TerrainWorkerPool WorkerPool
    {
        get
        {
            lock (poolLock)
            {
                if (workerPool == null)
                    workerPool = new TerrainWorkerPool(WorkerThreads);
                return workerPool;
            }
        }
    }

    /// <summary>Creates objects decided by object placement, a few per frame (see <see cref="PlacementInstantiator"/>).</summary>
    public PlacementInstantiator ObjectInstantiator
    {
        get
        {
            if (objectInstantiator == null)
                objectInstantiator = new PlacementInstantiator { PoolParent = transform };
            objectInstantiator.PoolingEnabled = PoolObjects;
            objectInstantiator.MaxPooled = MaxPooledObjects;
            return objectInstantiator;
        }
    }

    /// <summary>Switches the objects of chunks beyond Full Object Distance to far and back (see <see cref="FarObjectSwitcher"/>).</summary>
    public FarObjectSwitcher FarObjects
    {
        get
        {
            if (farObjectSwitcher == null)
                farObjectSwitcher = new FarObjectSwitcher();
            farObjectSwitcher.FullDistance = FullObjectDistance;
            farObjectSwitcher.Parts = farObjectParts;
            return farObjectSwitcher;
        }
    }

    /// <summary>Jobs waiting for a worker thread (0 before the first request).</summary>
    public int PendingWorkerJobs => workerPool != null ? workerPool.PendingCount : 0;

    /// <summary>Results waiting for the main thread.</summary>
    public int PendingMainThreadWork
    {
        get
        {
            lock (mainThreadLock)
                return mainThreadQueue != null ? mainThreadQueue.Count : 0;
        }
    }

    /// <summary>
    /// Main thread: works out, once, what the worker threads need but may not compute themselves (anything that
    /// reads Unity assets). Every Request method calls it; call it yourself before running generation code on
    /// your own threads.
    /// </summary>
    public void PrepareForGeneration()
    {
        if (splatIndexByName == null && biomeDefinitions != null)
        {
            Dictionary<string, int> byName = SplatMapGenerator.ComputeBiomeIndexMap(this);
            var byBiome = new Dictionary<Biome, int>();
            foreach (BiomeInstance instance in biomeDefinitions)
            {
                if (instance != null && instance.BiomePrefab != null && byName.TryGetValue(instance.BiomePrefab.name, out int index))
                    byBiome[instance.BiomePrefab] = index;
            }
            splatIndexByBiome = byBiome;
            splatIndexByName = byName;
        }
    }

    /// <summary>The compiled object rules (built on the main thread on first use, measuring each prefab once).</summary>
    public PlacementPlan GetPlacementPlan()
    {
        if (placementPlan == null)
        {
            placementPlan = PlacementPlan.Build(this, PrefabShapeCache.Get);
            foreach (string warning in placementPlan.Warnings)
                Debug.LogWarning("Terrain objects: " + warning, this);
        }
        return placementPlan;
    }

    /// <summary>
    /// Forgets the compiled object rules, measured prefabs and cached biome indices, so changed settings apply to
    /// chunks generated from now on (chunks already generated keep their objects).
    /// </summary>
    public void RefreshGenerationCaches()
    {
        placementPlan = null;
        PrefabShapeCache.Clear();
        ObjectPlacementEngine.ClearCaches();
        // Rebuilt right away rather than left empty, so worker threads still generating see a complete map.
        splatIndexByName = null;
        splatIndexByBiome = null;
        PrepareForGeneration();
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the main thread during Update, in the order queued, within Main Thread
    /// Budget Ms per frame (at least one action per frame). Callable from any thread.
    /// </summary>
    public void RunOnMainThread(Action action)
    {
        if (action == null)
            return;
        lock (mainThreadLock)
        {
            if (mainThreadQueue == null)
                mainThreadQueue = new Queue<Action>();
            mainThreadQueue.Enqueue(action);
        }
    }

    /// <summary>Queues a result's callback for the main thread, unless the work was cancelled (checked again just before calling).</summary>
    private void Deliver<T>(Action<T> callback, T value, WorkToken token)
    {
        if (callback == null || (token != null && token.IsCancelled))
            return;
        RunOnMainThread(() =>
        {
            if (token == null || !token.IsCancelled)
                callback(value);
        });
    }

    /// <summary>
    /// Handles asynchronous requests for generating map data.
    /// </summary>
    /// <param name="callback">The callback to execute when the map data is ready.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset, bool enableDebugging = false)
    {
        RequestMapData(callback, globalOffset, null, null);
    }

    /// <summary>
    /// Generates a chunk's heights and water on a worker thread; <paramref name="callback"/> gets them on the main
    /// thread. <paramref name="priority"/>: lower runs sooner (see the top of this file); nothing is called back
    /// once <paramref name="token"/> is cancelled.
    /// </summary>
    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset, Func<float> priority, WorkToken token)
    {
        PrepareForGeneration();
        bool placementFields = NeedsObjectPlacement;
        WorkerPool.Enqueue(() => Deliver(callback, GenerateTerrain(globalOffset, placementFields), token), priority, token);
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
        RequestTerrainData(mapData, callback, globalOffset, null, null);
    }

    /// <summary>
    /// Builds a chunk's mesh, biome map and splat map pixels from its map data on a worker thread; on the main
    /// thread the splat map textures are made (<see cref="DataStructure.TerrainData.splatMap"/>) and
    /// <paramref name="callback"/> is called.
    /// </summary>
    public void RequestTerrainData(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, Func<float> priority, WorkToken token)
    {
        PrepareForGeneration();
        WorkerPool.Enqueue(() =>
        {
            DataStructure.TerrainData terrainData = BuildTerrainData(mapData, globalOffset);
            Deliver<DataStructure.TerrainData>(data =>
            {
                if (data.splatPixels != null)
                    data.splatMap = SplatMapGenerator.CreateSplatTextures(data.splatPixels, ChunkSize);
                callback?.Invoke(data);
            }, terrainData, token);
        }, priority, token);
    }

    /// <summary>
    /// Generates everything of a chunk that doesn't need the main thread - heights, water, mesh and water mesh data,
    /// biome map, splat map pixels and the object placement environment - as one job; <paramref name="callback"/> gets it on the
    /// main thread (no textures are created: see <see cref="TextureGenerator.CreateChunkMaterial"/>).
    /// </summary>
    public void RequestChunkData(Action<DataStructure.TerrainData> callback, Vector2 globalOffset, Func<float> priority, WorkToken token)
    {
        PrepareForGeneration();
        bool placementFields = NeedsObjectPlacement;
        WorkerPool.Enqueue(() =>
        {
            MapData mapData = GenerateTerrain(globalOffset, placementFields);
            if (token != null && token.IsCancelled)
                return;
            Deliver(callback, BuildTerrainData(mapData, globalOffset), token);
        }, priority, token);
    }

    /// <summary>The mesh, biome map, splat pixels and water mesh of a chunk from its map data (worker thread).</summary>
    private DataStructure.TerrainData BuildTerrainData(MapData mapData, Vector2 globalOffset)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(
            this,
            mapData.heightMap,
            levelOfDetail,
            false,
            enableTextureVariations ? globalOffset : Vector2.zero,
            mapData.waterData != null ? mapData.waterData.Wetness : null,
            MeshGenerator.SkirtDepth(this, mapData.heightMap)
        );
        if (prepareMeshesOnWorkers)
            meshData.Prepare();

        // The splat maps' per-pixel biome blend is computed together with the biome map.
        bool computeSplatBlend = terrainTextureBasedOnVoronoiPoints && UseBiomeBlendedTexturing;
        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap, computeSplatBlend, out SplatBlendData splatBlend);

        var terrainData = new DataStructure.TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap, mapData.erosionDeltaMap, mapData.waterData);
        terrainData.splatBlend = splatBlend;
        terrainData.placementFields = mapData.placementFields;
        if (terrainTextureBasedOnVoronoiPoints)
            terrainData.splatPixels = SplatMapGenerator.GenerateSplatPixels(this, biomeMap, globalOffset, splatBlend);
        if (EnableWater && mapData.waterData != null)
            terrainData.waterMeshData = MeshGenerator.GenerateWaterMesh(this, mapData.heightMap, mapData.waterData, levelOfDetail, globalOffset);
        return terrainData;
    }

    /// <summary>
    /// Builds a chunk's terrain mesh at another level of detail on a background thread (distance LOD - see
    /// <see cref="LodForDistance"/>), calling <paramref name="callback"/> on the main thread when it is ready.
    /// Same heights, wetness and texture coordinates as the chunk's main mesh, only coarser.
    /// </summary>
    public void RequestLodMesh(Action<MeshData> callback, float[,] heightMap, WaterMapData waterData, int lod, Vector2 globalOffset, Func<float> priority = null, WorkToken token = null)
    {
        WorkerPool.Enqueue(() =>
        {
            MeshData meshData = MeshGenerator.GenerateTerrainMesh(
                this,
                heightMap,
                lod,
                false,
                enableTextureVariations ? globalOffset : Vector2.zero,
                waterData != null ? waterData.Wetness : null,
                MeshGenerator.SkirtDepth(this, heightMap)
            );
            if (prepareMeshesOnWorkers)
                meshData.Prepare();
            Deliver(callback, meshData, token);
        }, priority, token);
    }

    /// <summary>
    /// Decides where a chunk's objects go on a worker thread (see <see cref="ObjectPlacementEngine"/>);
    /// <paramref name="callback"/> gets the result on the main thread - nothing is created yet (see
    /// <see cref="ObjectInstantiator"/>). An empty result when objects are off or no biome has any.
    /// </summary>
    public void RequestObjectPlacement(Action<PlacementResult> callback, DataStructure.TerrainData terrainData, Func<float> priority = null, WorkToken token = null)
    {
        PlacementPlan plan = ShouldSpawnObjects ? GetPlacementPlan() : null;
        if (plan == null || plan.IsEmpty || terrainData.heightMap == null)
        {
            Deliver(callback, new PlacementResult(), token);
            return;
        }

        var input = new ObjectPlacementEngine.ChunkInput
        {
            OriginX = Mathf.RoundToInt(terrainData.globalOffset.x),
            OriginY = Mathf.RoundToInt(terrainData.globalOffset.y),
            Span = ChunkSize - 1,
            HeightMap = terrainData.heightMap,
            BiomeMap = terrainData.biomeMap,
            Water = terrainData.waterData,
            Fields = terrainData.placementFields,
            LodFactor = levelOfDetail > 0 ? levelOfDetail * 2 : 1,
        };
        WorkerPool.Enqueue(() =>
        {
            PlacementResult result = ObjectPlacementEngine.Place(plan, input, token);
            if (result != null)
                Deliver(callback, result, token);
        }, priority, token);
    }

    /// <summary>
    /// Places a chunk's objects: decided on a worker thread, then created under <paramref name="chunkTransform"/> a
    /// few per frame; <paramref name="callback"/> runs once they all exist, with the placements filled in.
    /// (EndlessTerrain uses <see cref="RequestObjectPlacement"/> and <see cref="ObjectInstantiator"/> directly.)
    /// </summary>
    /// <param name="callback">The callback to execute when the biome object data is ready.</param>
    /// <param name="terrainData">The terrain data used for object placement.</param>
    /// <param name="globalOffset">The global offset for the terrain generation.</param>
    /// <param name="chunkTransform">The transform of the terrain chunk.</param>
    public void RequestBiomeObjectData(Action<BiomeObjectData> callback, DataStructure.TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        RequestObjectPlacement(result =>
        {
            var data = new BiomeObjectData(terrainData.heightMap, globalOffset, this, terrainData.biomeMap, chunkTransform, terrainData.meshData, terrainData.waterData)
            {
                placements = result,
                plan = result.Plan,
            };
            if (chunkTransform == null || result.Plan == null)
            {
                callback?.Invoke(data);
                return;
            }
            ObjectInstantiator.Enqueue(chunkTransform, result.Plan, result, null, null, null, batch => callback?.Invoke(data));
        }, terrainData);
    }

    /// <summary>
    /// Applies finished chunk data on the main thread (within Main Thread Budget Ms), then creates waiting objects
    /// (within Object Spawn Budget Ms).
    /// </summary>
    void Update()
    {
        if (frameWatch == null)
            frameWatch = new Stopwatch();
        frameWatch.Restart();
        float budget = MainThreadBudgetMs;
        while (true)
        {
            Action action;
            lock (mainThreadLock)
            {
                if (mainThreadQueue == null || mainThreadQueue.Count == 0)
                    break;
                action = mainThreadQueue.Dequeue();
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (frameWatch.Elapsed.TotalMilliseconds >= budget)
                break;
        }

        farObjectSwitcher?.Update(ObjectSpawnBudgetMs);
        objectInstantiator?.Update(ObjectSpawnBudgetMs, MaxObjectsPerFrame);
    }

    /// <summary>Stops the worker threads and frees what every chunk shared.</summary>
    private void OnDestroy()
    {
        lock (poolLock)
        {
            workerPool?.Dispose();
            workerPool = null;
        }
        lock (mainThreadLock)
            mainThreadQueue?.Clear();
        objectInstantiator?.Clear();
        objectInstantiator?.ClearPool();
        TextureGenerator.ReleaseSharedTextures();
    }
}
