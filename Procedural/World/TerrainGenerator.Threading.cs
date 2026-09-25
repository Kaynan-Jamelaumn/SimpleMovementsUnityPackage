using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;

// TerrainGenerator, part 4 of 4: background-thread requests and main-thread callbacks (see TerrainGenerator.cs).
public partial class TerrainGenerator : MonoBehaviour
{
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
            enableTextureVariations ? globalOffset : Vector2.zero,
            mapData.waterData != null ? mapData.waterData.Wetness : null,
            MeshGenerator.SkirtDepth(this, mapData.heightMap)
        );

        // The splat maps' per-pixel biome blend is computed here, off the main thread, together with the
        // biome map (the main thread then only writes the pixels - see Update).
        bool computeSplatBlend = terrainTextureBasedOnVoronoiPoints && UseBiomeBlendedTexturing;
        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap, computeSplatBlend, out SplatBlendData splatBlend);

        DataStructure.TerrainData terrainData = new DataStructure.TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap, mapData.erosionDeltaMap, mapData.waterData);
        terrainData.splatBlend = splatBlend;

        lock (terrainDataThreadInfoQueue)
        {
            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<DataStructure.TerrainData>(callback, terrainData));
        }
    }

    // Finished distance-LOD meshes waiting for the main thread. Created on first use (under a static lock that
    // always exists) rather than by a field initializer, so it can never be null - Unity can leave non-serialized
    // fields null, e.g. after scripts are recompiled while the game is running.
    private static readonly object lodMeshQueueLock = new object();
    private Queue<MapThreadInfo<MeshData>> lodMeshThreadInfoQueue;

    /// <summary>
    /// Builds a chunk's terrain mesh at another level of detail on a background thread (distance LOD - see
    /// <see cref="LodForDistance"/>), calling <paramref name="callback"/> on the main thread when it is ready.
    /// Same heights, wetness and texture coordinates as the chunk's main mesh, only coarser.
    /// </summary>
    public void RequestLodMesh(Action<MeshData> callback, float[,] heightMap, WaterMapData waterData, int lod, Vector2 globalOffset)
    {
        ThreadStart threadStart = delegate {
            MeshData meshData = MeshGenerator.GenerateTerrainMesh(
                this,
                heightMap,
                lod,
                false,
                enableTextureVariations ? globalOffset : Vector2.zero,
                waterData != null ? waterData.Wetness : null,
                MeshGenerator.SkirtDepth(this, heightMap)
            );

            lock (lodMeshQueueLock)
            {
                if (lodMeshThreadInfoQueue == null)
                    lodMeshThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();
                lodMeshThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
            }
        };

        new Thread(threadStart).Start();
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

        BiomeObjectData biomeObjectData = new BiomeObjectData(terrainData.heightMap, globalOffset, terrainData.terrainGenerator, terrainData.biomeMap, chunkTransform, meshData, terrainData.waterData);

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
                        enableTextureVariations ? threadInfo.parameter.globalOffset : Vector2.zero,
                        threadInfo.parameter.splatBlend
                    );
                }

                // Assign the generated splat map to the terrain data and trigger the callback.
                threadInfo.parameter.splatMap = splatMap;
                threadInfo.callback(threadInfo.parameter);
            }
        }

        // Distance-LOD meshes that finished building.
        MapThreadInfo<MeshData>[] readyLodMeshes = null;
        lock (lodMeshQueueLock)
        {
            if (lodMeshThreadInfoQueue != null && lodMeshThreadInfoQueue.Count > 0)
            {
                readyLodMeshes = lodMeshThreadInfoQueue.ToArray();
                lodMeshThreadInfoQueue.Clear();
            }
        }
        if (readyLodMeshes != null)
        {
            foreach (MapThreadInfo<MeshData> threadInfo in readyLodMeshes)
                threadInfo.callback?.Invoke(threadInfo.parameter);
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
                            // Skip land objects (trees, rocks, etc.) at cells now covered by water -
                            // without this, biomes with a low baseElevation/near WaterLevel would still
                            // spawn their normal land object set on what is now a lake/river bed.
                            WaterMapData waterData = threadInfo.parameter.waterData;
                            bool isUnderwater = waterData != null && waterData.IsWet(x, y);

                            if (!isUnderwater)
                            {
                                // Find the corresponding biome instance for the chosen biome.
                                BiomeInstance chosenBiomeInstance = threadInfo.parameter.terrainGenerator.biomeDefinitions
                                    .FirstOrDefault(b => b.BiomePrefab == chosenBiome);

                                // Place objects for the selected biome at the calculated position.
                                ObjectSpawner.PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiomeInstance, threadInfo.parameter.heightMap, x, y, threadInfo.parameter.meshData, lodFactor);
                            }
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
