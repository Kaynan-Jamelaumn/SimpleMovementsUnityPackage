using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;

public class SingleTerrainGenerator : MonoBehaviour
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

    [SerializeField][Range(0, 6)] private int levelOfDetail;
    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }
    public float Persistence { get => persistence; }
    public float BaseFrequency { get => baseFrequency; }
    public float Amplitude { get => amplitude; }
    public int GridDepthSize { get => gridDepthSize; }
    public int GridWidthSize { get => gridWidthSize; }
    //public float[,] CellMap { get => cellMap; set => cellMap = value; }


    [SerializeField] private SingleBiome[] biomes;


    public SingleBiome[] Biomes { get => biomes; set => biomes = value; }
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }

    public SingleTerrainGenerator(int width = 320, int depth = 320, float amplitude = 100f, float baseFrequency = 0.4f, float persistence = 1f, int octaves = 5, float lacunarity = 2f)
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

    }
    public void Start()
    {
        GenerateTerrain();
    }

    void GenerateTerrain()
    {
        float[,] heightMap = SingleHeightGenerator.GenerateHeightMap(this);
        SingleMeshData meshData = SingleMeshGenerator.GenerateTerrainMesh(this, heightMap);
        Texture2D splatmapcustom = SingleSplatMapGenerator.GenerateSplatMap(this, heightMap);
        SingleGenerateTexture.SingleAssignTexture(splatmapcustom, this);
        Mesh mesh = meshData.UpdateMesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

    }

}
[System.Serializable]
public class SingleBiome
{
    public string name;
    public float minHeight;
    public float maxHeight;
    //public color color;
    public Texture2D texture;
}

