using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using Unity.VisualScripting;

public class TerrainGenerator : MonoBehaviour
{
    public const int chunkSize = 241; // divisble by 2, 4, 6, 8, 10, 12
        public int ChunkSize { get => chunkSize; }

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
    [SerializeField] private bool shouldSpawnObjects = true;

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
    Queue<MapThreadInfo<BiomeObjectData>> biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

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
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        VoronoiCache.Instance.Initialize(VoronoiSeed);
        //VoronoiCache.Instance.Initialize(VoronoiScale, biomes.ToList(), VoronoiSeed);
        //VoronoiCache.Instance.Initialize(VoronoiScale, NumVoronoiPoints, biomes.ToList(), VoronoiSeed);


    }

    MapData GenerateTerrain(Vector2 globalOffset)
    {
        float[,] heightMap = HeightGenerator.GenerateHeightMap(this, globalOffset);

        return new MapData(heightMap, null);

    }
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        Biome[,] biomeMap = new Biome[gridWidthSize, gridDepthSize];

        for (int y = 0; y < gridDepthSize; y++)
        {
            for (int x = 0; x < gridWidthSize; x++)
            {
                Vector2 worldPos = new Vector2(globalOffset.x + x, globalOffset.y + y);
                Biome chosenBiome = NoiseGenerator.Voronoi(worldPos, VoronoiScale, NumVoronoiPoints, Biomes.ToList(), VoronoiSeed);
                biomeMap[x, y] = chosenBiome;
                //Vector3 worldPos3D = new Vector3(worldPos.x, 0, worldPos.y);
                //PlaceObjectsForBiome(this, worldPos3D, chosenBiome, heightMap);
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

    public void RequestTerrainData(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset, int lod = 0)
    {
        ThreadStart threadStart = delegate {
            TerrainDataThread(mapData, callback, globalOffset, lod);
        };

        new Thread(threadStart).Start();
    }


    void TerrainDataThread(MapData mapData, Action<TerrainData> callback, Vector2 globalOffset,
        int lod = 0)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this, mapData.heightMap);
        Biome[,] biomeMap = GenerateBiomeMap(globalOffset, mapData.heightMap);
        TerrainData terrainData = new TerrainData(meshData, null, mapData.heightMap, this, globalOffset, biomeMap);

        lock (terrainDataThreadInfoQueue)
        {
            terrainDataThreadInfoQueue.Enqueue(new MapThreadInfo<TerrainData>(callback, terrainData));
        }
    }




    public void RequestBiomeObjectData(Action<BiomeObjectData> callback, TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        ThreadStart threadStart = delegate {
            BiomeObjectThread(callback, terrainData, globalOffset, chunkTransform);
        };

        new Thread(threadStart).Start();
    }
    void BiomeObjectThread(Action<BiomeObjectData> callback, TerrainData terrainData, Vector2 globalOffset, Transform chunkTransform)
    {
        if (callback == null)
        {
            Debug.LogError("Callback is null");
            return;
        }
        BiomeObjectData biomeObjectData = new BiomeObjectData(terrainData.heightMap,  globalOffset, terrainData.terrainGenerator, terrainData.biomeMap, chunkTransform);



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
                    //Biome[,] biomeMap = GenerateBiomeMap(threadInfo.parameter.globalOffset, threadInfo.parameter.heightMap);
                    splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.biomeMap, splatMap);

                }
                else {splatMap = SplatMapGenerator.GenerateSplatMapOutsideMainThread(this, threadInfo.parameter.heightMap, splatMap);
                   

                    
                    }
                threadInfo.parameter.splatMap = splatMap;
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (biomeObjectDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < biomeObjectDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<BiomeObjectData> threadInfo = biomeObjectDataThreadInfoQueue.Dequeue();


                for (int y = 0; y < threadInfo.parameter.terrainGenerator.GridDepthSize; y++)
                {
                    for (int x = 0; x < threadInfo.parameter.terrainGenerator.GridWidthSize; x++)
                    {
                        //may debug the global offset
                        Vector2 worldPos2D = new Vector2(threadInfo.parameter.globalOffset.x + x, threadInfo.parameter.globalOffset.y + y);
                        Vector3 worldPos3D = new Vector3(worldPos2D.x, 0, worldPos2D.y);
                        Biome chosenBiome = threadInfo.parameter.biomeMap[x, y];
                        if (shouldSpawnObjects)
                            PlaceObjectsForBiome(threadInfo.parameter.chunkTransform, worldPos3D, chosenBiome, threadInfo.parameter.heightMap, x, y);
                    }

                }
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

    public void PlaceObjectsForBiome(Transform chunkTransform, Vector3 worldPosition, Biome biome, float[,] heightMap, int x, int y)
    {
        foreach (BiomeObject biomeObject in biome.objects)
        {
            //if (biomeObject.currentNumberOfThisObject >= biomeObject.maxNumberOfThisObject)
            //    continue;

            if ((UnityEngine.Random.value * 100) < biomeObject.probabilityToSpawn)
            {
                Vector3 spawnPosition = GetSpawnPosition(worldPosition, heightMap, x, y); // this function here for you to get the coordinates of the objects
                InstantiateBiomeObject(chunkTransform, biomeObject.terrainObject, spawnPosition, heightMap, x, y);
                biomeObject.currentNumberOfThisObject++;
            }
        }
    }
    private Vector3 GetSpawnPosition(Vector3 worldPosition, float[,] heightMap, int x, int y)
    {

        // Get the height at the position
        float height = heightMap[x, y];

        // Return the position with the correct height
        return new Vector3(worldPosition.x, height, worldPosition.z);
    }

    private static void InstantiateBiomeObject(Transform chunkTransform, GameObject prefab, Vector3 position, float[,] heightMap, int x, int y)
    {
        // Get the terrain's normal vector at the spawn position
        Vector3 normal = CalculateTerrainNormal(heightMap, x, y);

        // Calculate a random Y rotation for the object
        Quaternion randomYRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

        // Align the object's up direction with the terrain normal
        Quaternion terrainAlignmentRotation = Quaternion.FromToRotation(Vector3.up, normal);

        // Combine the random Y rotation with the terrain alignment rotation
        Quaternion finalRotation = terrainAlignmentRotation * randomYRotation;

        // Instantiate the object with the calculated rotation
        GameObject obj = Instantiate(prefab, position, finalRotation);
        obj.transform.parent = chunkTransform;
    }

    private static Vector3 CalculateTerrainNormal(float[,] heightMap, int x, int y)
    {
        // Get the height differences between neighboring points
        float heightL = heightMap[Mathf.Max(0, x - 1), y];
        float heightR = heightMap[Mathf.Min(heightMap.GetLength(0) - 1, x + 1), y];
        float heightD = heightMap[x, Mathf.Max(0, y - 1)];
        float heightU = heightMap[x, Mathf.Min(heightMap.GetLength(1) - 1, y + 1)];

        // Calculate the normal vector using cross product
        Vector3 normal = new Vector3(heightL - heightR, 2f, heightD - heightU).normalized;

        return normal;
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
    public List<BiomeObject> objects;
    public float maxNumberOfObjects;
    public List<SpawnableMob> mobList;
}
[System.Serializable]
public class BiomeObject
{
    public GameObject terrainObject;
    public float weight;
    public float probabilityToSpawn;
    public int currentNumberOfThisObject;
    public int maxNumberOfThisObject;

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
    public Biome[,] biomeMap;
    public TerrainData(MeshData meshData, Texture2D splatMap, float[,] heightMap, TerrainGenerator terrainGenerator, Vector2 globalOffset, Biome[,] biomeMap)
    {
        this.meshData = meshData;
        this.splatMap = splatMap;
        this.heightMap = heightMap;
        this.terrainGenerator = terrainGenerator;
        this.globalOffset = globalOffset;
        this.biomeMap = biomeMap;
    }
}
public struct BiomeObjectData
{
    public readonly float[,] heightMap;
    public Vector2 globalOffset;    
    public readonly TerrainGenerator terrainGenerator;
    //public readonly TerrainData terrainData;
    public readonly Biome[,] biomeMap;
    public readonly Transform chunkTransform;
    public BiomeObjectData(float[,] heightMap, Vector2 globalOffset, TerrainGenerator terrainGenerator, Biome[,] biomeMap, Transform chunkTransform)
    {
        this.heightMap = heightMap;
        this.globalOffset = globalOffset;
        this.terrainGenerator = terrainGenerator;
        this.biomeMap = biomeMap;
        this.chunkTransform = chunkTransform;
    }
}

