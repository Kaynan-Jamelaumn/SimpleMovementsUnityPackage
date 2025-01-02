
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;
public class TerrainGenerator : MonoBehaviour
{

    public const int chunkSize = 241;
    public int ChunkSize { get => chunkSize; }

    [Header("Noise Configuration")]

    [SerializeField] private float scaleFactor = 3;

    [SerializeField] private int octaves = 5;

    [SerializeField] private float lacunarity = 2f;

    [Header("Texture")]

    [HideInInspector][SerializeField] private Texture2D defaultTexture;

    [HideInInspector][SerializeField] private ComputeShader splatMapShader;

    [SerializeField] private float minHeight;

    [SerializeField] private float maxHeight;

    [SerializeField] private bool terrainTextureBasedOnVoronoiPoints = true;

    [Header("Voronoi Noise Configuration")]

    [SerializeField] public int NumVoronoiPoints = 3;

    [SerializeField] public int VoronoiSeed = 0;

    [SerializeField] public float VoronoiScale = 1;

    [Header("Other Configurations")]

    [SerializeField][Range(0, 6)] private int levelOfDetail;

    [Header("Biomes")]

    [SerializeField] private BiomeInstance[] biomeDefinitions;

    [Header("Objects")]

    [SerializeField] private bool shouldSpawnObjects = true;

    [SerializeField] private float clusterBaseFrequency = 0.1f;

    [SerializeField] private float clusterAmplitude = 1f;

    [SerializeField] private float clusteringIntensity = 2f;

    float[,] heightMap;
    Vector2 globalOffset;

    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();

    Queue<MapThreadInfo<DataStructure.TerrainData>> terrainDataThreadInfoQueue = new Queue<MapThreadInfo<DataStructure.TerrainData>>();

    Queue<MapThreadInfo<BiomeObjectData>> biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

    public BiomeInstance[] BiomeDefinitions { get => biomeDefinitions; set => biomeDefinitions = value; }
    public bool TerrainTextureBasedOnVoronoiPoints { get => terrainTextureBasedOnVoronoiPoints; }
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }
    bool isR;

    public void UpdateMinMaxHeight(float height)
    {
        if (height > MaxHeight) MaxHeight = height;
        if (height < MinHeight) MinHeight = height;
    }

    public TerrainGenerator(float amplitude = 100f, float lacunarity = 2f)
    {

        this.lacunarity = lacunarity;

    }
    private void Awake()
    {

        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<DataStructure.TerrainData>>();
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        VoronoiCache.Instance.Initialize(VoronoiSeed);

        foreach (BiomeInstance biomeInstance in biomeDefinitions)
        {
            Biome biome = biomeInstance.BiomePrefab;
            foreach (BiomeObject biomeObject in biomeInstance.runtimeObjects)
            {
                if (biomeObject.densityMap == null)
                {
                    biomeObject.densityMap = ObjectSpawner.GenerateClusteredDensityMap(
                        chunkSize, chunkSize,
                        biomeObject.clusterCount, biomeObject.clusterRadius,
                        clusterBaseFrequency, clusterAmplitude, scaleFactor
                    );
                }
            }
        }

    }

    MapData GenerateTerrain(Vector2 globalOffset)
    {
        this.globalOffset = globalOffset;

        heightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);

        return new MapData(heightMap, null);

    }

    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        Biome[,] biomeMap = new Biome[chunkSize, chunkSize];

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {

                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);

                Biome chosenBiome = NoiseGenerator.Voronoi(worldPos,VoronoiScale,NumVoronoiPoints,biomeDefinitions.Select(b => b.BiomePrefab).ToList(),VoronoiSeed
  );


                biomeMap[x, y] = chosenBiome;
            }
        }

        return biomeMap;
    }

    public void RequestMapData(Action<MapData> callback, Vector2 globalOffset)
    {

        ThreadStart threadStart = delegate {
            MapDataThread(callback, globalOffset);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Action<MapData> callback, Vector2 globalOffset)
    {

        MapData mapData = GenerateTerrain(globalOffset);

        lock (mapDataThreadInfoQueue)
        {

            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));

        }
    }

    public void RequestTerrainData(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset, int lod = 0)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset, lod);
        };

        new Thread(threadStart).Start();
    }

    void TerrainDataThread(MapData mapData, Action<DataStructure.TerrainData> callback, Vector2 globalOffset,
        int lod = 0)
    {

        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap, levelOfDetail);

        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap);

        DataStructure.TerrainData terrainData = new DataStructure.TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap);

        lock (terrainDataThreadInfoQueue)
        {

            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<DataStructure.TerrainData>(callback, terrainData));
        }
    }

    public void RequestBiomeObjectData(Action<BiomeObjectData> callback, DataStructure.TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        ThreadStart threadStart = delegate {
            BiomeObjectThread(callback, terrainData, globalOffset, chunkTransform);
        };

        new Thread(threadStart).Start();
    }
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

    void Update()
    {

        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();

                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (terrainDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < terrainDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<DataStructure.TerrainData> threadInfo = terrainDataThreadInfoQueue.Dequeue();

                Texture2D splatMap = new Texture2D(chunkSize, chunkSize);
                if (terrainTextureBasedOnVoronoiPoints)
                {

                    splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.biomeMap, splatMap);

                }
                else { splatMap = SplatMapGenerator.GenerateSplatMapBasedOnHeight(this, threadInfo.parameter.heightMap, splatMap); }
                threadInfo.parameter.splatMap = splatMap;
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (biomeObjectDataThreadInfoQueue.Count > 0)
        {
            bool shouldBreak = false;
            for (int i = 0; i < biomeObjectDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<BiomeObjectData> threadInfo = biomeObjectDataThreadInfoQueue.Dequeue();

                for (int y = 0; y < threadInfo.parameter.terrainGenerator.ChunkSize; y++)
                {
                    if (shouldBreak) break;
                    for (int x = 0; x < threadInfo.parameter.terrainGenerator.ChunkSize; x++)
                    {

                        Vector2 worldPos2D = new Vector2(threadInfo.parameter.globalOffset.x + x, threadInfo.parameter.globalOffset.y + y);
                        Vector3 worldPos3D = new Vector3(worldPos2D.x, 0, worldPos2D.y);
                        Biome chosenBiome = threadInfo.parameter.biomeMap[x, y];
                        if (!shouldSpawnObjects)
                        {
                            shouldBreak = true;
                            break;
                        }
                        else
                        {
                            // Find the corresponding BiomeInstance
                            BiomeInstance chosenBiomeInstance = threadInfo.parameter.terrainGenerator.biomeDefinitions
                                .FirstOrDefault(b => b.BiomePrefab == chosenBiome);
                            ObjectSpawner.PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiomeInstance, threadInfo.parameter.heightMap, x, y);
                        }
                    }

                }
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

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
