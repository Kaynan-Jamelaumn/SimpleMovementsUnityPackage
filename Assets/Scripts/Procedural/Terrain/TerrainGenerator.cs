using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;

public class TerrainGenerator : MonoBehaviour
{
    public const int chunkSize = 241; // divisble by 2, 4, 6, 8, 10, 12

    [SerializeField] private int gridWidthSize = 241;
    [SerializeField] private int gridDepthSize = 241;
    [SerializeField] private float scaleFactor = 3;
    //[SerializeField] private float amplitude = 120f;
    //[SerializeField] private float baseFrequency = 0.4f;
    //[SerializeField][Range(0, 2)] private float persistence = 1f;
    [SerializeField] private int octaves = 5;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private Texture2D defaultTexture;
    [SerializeField] private ComputeShader splatMapShader;
    [SerializeField] public AnimationCurve heightCurve;
    [SerializeField] private float minHeight;
    [SerializeField] private float maxHeight;

    [SerializeField] private bool terrainTextureBasedOnVoronoiPoints = true;
    [SerializeField] public int NumVoronoiPoints = 3;
    [SerializeField] public int VoronoiSeed = 0;
    [SerializeField] public float VoronoiScale = 1;

    [SerializeField][Range(0,6)]private int levelOfDetail; 
    
    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }
    //public float Persistence { get => persistence; }
    //public float BaseFrequency { get => baseFrequency; }
    //public float Amplitude { get => amplitude; }
    public int GridDepthSize { get => gridDepthSize; }
    public int GridWidthSize { get => gridWidthSize; }

    [SerializeField] private Biome[] biomes;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<TerrainData>> terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();

    public Biome[] Biomes { get => biomes; set => biomes = value; }
    public bool TerrainTextureBasedOnVoronoiPoints { get => terrainTextureBasedOnVoronoiPoints;}
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }
    public void UpdateMinMaxHeight(float height)
    {
        if (height > MaxHeight) MaxHeight = height;
        if (height < MinHeight) MinHeight = height;
    }

    public TerrainGenerator(int width = 320, int depth = 320, float amplitude = 100f,/* float baseFrequency = 0.4f, float persistence=1f, int octaves=5,*/ float lacunarity=2f)
    {
        gridWidthSize = width;
        gridDepthSize = depth;
        //this.amplitude = amplitude;
        //this.baseFrequency = baseFrequency;
        //this.persistence = persistence;
        //this.octaves = octaves;
        this.lacunarity = lacunarity;
        //cellMap = new float[width + 1, depth + 1];
    }
    private void Awake()
    {
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<TerrainData>>();
        //Noise.InitializeVoronoiCache(VoronoiSeed, VoronoiScale, NumVoronoiPoints, biomes.ToList());
        VoronoiCache.Instance.Initialize(VoronoiScale, NumVoronoiPoints, biomes.ToList(), VoronoiSeed);

    }
    MapData GenerateTerrain(Vector2 globalOffset)
    {
        float[,] heightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);

        return new MapData(heightMap, null);

    }
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset)
    {
        Biome[,] biomeMap = new Biome[gridWidthSize, gridDepthSize];

        for (int y = 0; y < gridDepthSize; y++)
        {
            for (int x = 0; x < gridWidthSize; x++)
            {
                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);
                biomeMap[x, y] = NoiseGenerator.Voronoi(worldPos, VoronoiScale, NumVoronoiPoints, Biomes.ToList(), VoronoiSeed);
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

    public void RequestTerrainhData(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset);
        };

        new Thread(threadStart).Start();
    }

    void TerrainDataThread(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap);
        TerrainData terrainData = new TerrainData(meshData, null, mapData.heightMap, this, globalOffset);

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
        if (terrainDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < terrainDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<TerrainData> threadInfo = terrainDataThreadInfoQueue.Dequeue();
                Texture2D splatMap = new Texture2D(gridWidthSize, gridDepthSize);
                if (terrainTextureBasedOnVoronoiPoints)
                {
                    Biome[,] biomeMap = GenerateBiomeMap(threadInfo.parameter.globalOffset);
                    splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, biomeMap, splatMap);

                }
                else {splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.heightMap, splatMap);
                   

                    
                    }
                threadInfo.parameter.splatMap = splatMap;
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
}
[System.Serializable]
public class Biome
{
    public string name;
    public float minHeight;
    public float maxHeight;
    //public color color;
    public Texture2D texture;
    public float amplitude; // Height variation within the biome
    public float frequency; // Detail level of the biome's terrain
    [Range(0, 1)] public float persistence =1; // How much detail is added or removed at each octave
    //public AnimationCurve heightCurve; // Curve to apply to heights within this biome
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
    public Vector2 globalOffset;
    public TerrainData(MeshData meshData, Texture2D splatMap, float[,] heightMap, TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        this.meshData = meshData;
        this.splatMap = splatMap;
        this.heightMap = heightMap;
        this.terrainGenerator = terrainGenerator;
        this.globalOffset = globalOffset;
    }
}