using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;

public class TerrainGenerator : MonoBehaviour
{
    public const int chunkSize = 241; // divisble by 2, 4, 6, 8, 10, 12

    [SerializeField] private int gridWidthSize = 241;
    [SerializeField] private int gridDepthSize = 241;
    [SerializeField] private float scaleFactor = 3;
    [SerializeField] private float amplitude = 120f;
    [SerializeField] private float baseFrequency = 0.4f;
    [SerializeField] private float persistence = 1f;
    [SerializeField] private int octaves = 5;
    [SerializeField] private float lacunarity = 2f;
    //[SerializeField] private float[,] cellMap;
    [SerializeField] private Texture2D defaultTexture;
    [SerializeField] private ComputeShader splatMapShader;
    [SerializeField] public AnimationCurve heightCurve;
    [SerializeField] private float minHeight;
    [SerializeField] private float maxHeight;

    [SerializeField][Range(0,6)]private int levelOfDetail; 
    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }
    public float Persistence { get => persistence; }
    public float BaseFrequency { get => baseFrequency; }
    public float Amplitude { get => amplitude; }
    public int GridDepthSize { get => gridDepthSize; }
    public int GridWidthSize { get => gridWidthSize; }
    //public float[,] CellMap { get => cellMap; set => cellMap = value; }


    [SerializeField] private Biome[] biomes;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    //Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();
    Queue<MapThreadInfo<TerrainData>> terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();

    public Biome[] Biomes { get => biomes; set => biomes = value; }
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }

    public TerrainGenerator(int width = 320, int depth = 320, float amplitude = 100f, float baseFrequency = 0.4f, float persistence=1f, int octaves=5, float lacunarity=2f)
    {
        gridWidthSize = width;
        gridDepthSize = depth;
        this.amplitude = amplitude;
        this.baseFrequency = baseFrequency;
        this.persistence = persistence;
        this.octaves = octaves;
        this.lacunarity = lacunarity;
        //cellMap = new float[width + 1, depth + 1];
    }
    private void Awake()
    {
        //cellMap ??= new float[gridWidthSize + 1, gridDepthSize + 1];
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        //meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();

    }
    //public void Start()
    //{
    //    GenerateTerrain();
    //}

    MapData GenerateTerrain()
    {
        float[,] heightMap = HeightGenerator.GenerateHeightMap(this);
        //MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, heightMap);

        //Texture2D splatMap = new Texture2D(gridWidthSize, gridDepthSize);
        //splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, heightMap, splatMap);

        ////Texture2D splatMap = SplatMapGenerator.GenerateSplatMap(this, heightMap);

        //GenerateTexture.AssignTexture(splatMap, this);
        //Mesh mesh = meshData.UpdateMesh();
        //GetComponent<MeshFilter>().mesh = mesh;
        //GetComponent<MeshCollider>().sharedMesh = mesh;

        return new MapData(heightMap, null);

    }
        public void RequestMapData(Action<MapData> callback)
    {
        ThreadStart threadStart = delegate {
            MapDataThread(callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Action<MapData> callback)
    {
        MapData mapData = GenerateTerrain();
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));

        }
    }

    public void RequestTerrainhData(MapData mapData, Action<TerrainData> callback)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback);
        };

        new Thread(threadStart).Start();
    }

    void TerrainDataThread(MapData mapData, Action<TerrainData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap);
        TerrainData terrainData = new TerrainData(meshData, null, mapData.heightMap, this);

        lock (terrainDataThreadInfoQueue)
        {
            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<TerrainData>(callback, terrainData));
        }
    }


    void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                //CreateTextureForMapData(threadInfo.parameter);
                threadInfo.callback(threadInfo.parameter);
            }
        }

        //if (meshDataThreadInfoQueue.Count > 0)
        //{
        //    for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
        //    {
        //        MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
        //        Texture2D splatMap = new Texture2D(gridWidthSize, gridDepthSize);
        //        splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.heightMap, splatMap);
        //        threadInfo.parameter.splatMap = splatMap;
        //        Mesh mesh = threadInfo.parameter.UpdateMesh();
        //        threadInfo.callback(threadInfo.parameter);
        //    }
        //}
        if (terrainDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < terrainDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<TerrainData> threadInfo = terrainDataThreadInfoQueue.Dequeue();
                Texture2D splatMap = new Texture2D(gridWidthSize, gridDepthSize);
                splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.heightMap, splatMap);
                threadInfo.parameter.splatMap = splatMap;
                //Mesh mesh = threadInfo.parameter.meshData.UpdateMesh();
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
    //public void RequestMeshData(MapData mapData, Action<MeshData> callback)
    //{
    //    ThreadStart threadStart = delegate {
    //        MeshDataThread(mapData, callback);
    //    };

    //    new Thread(threadStart).Start();
    //}

    //void MeshDataThread(MapData mapData, Action<MeshData> callback)
    //{
    //    MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap);

    //    lock (meshDataThreadInfoQueue)
    //    {
    //        meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
    //    }
    //}
}
[System.Serializable]
public class Biome
{
    public string name;
    public float minHeight;
    public float maxHeight;
    //public color color;
    public Texture2D texture;
}
public struct MapData
{
    public readonly float[,] heightMap;
    public  Texture2D splatMap;
    public MapData(float[,] heightMap, Texture2D splatMap = null)
    {
        this.heightMap = heightMap;
        this.splatMap = splatMap;
    }
}
public struct TerrainData
{
    public readonly MeshData meshData;
    public Texture2D splatMap;
    public readonly float[,] heightMap;
    public readonly TerrainGenerator terrainGenerator;
    public TerrainData(MeshData meshData, Texture2D splatMap, float[,] heightMap, TerrainGenerator terrainGenerator)
    {
        this.meshData = meshData;
        this.splatMap = splatMap;
        this.heightMap = heightMap;
        this.terrainGenerator = terrainGenerator;
    }
}