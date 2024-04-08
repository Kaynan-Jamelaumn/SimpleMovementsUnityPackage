using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] private int gridWidthSize = 320;
    [SerializeField] private int gridDepthSize = 320;
    [SerializeField] private float scaleFactor = 3;
    [SerializeField] private float amplitude = 120f;
    [SerializeField] private float baseFrequency = 0.4f;
    [SerializeField] private float persistence = 1f;
    [SerializeField] private int octaves = 5;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private float[,] cellMap;
    [SerializeField] private Texture2D defaultTexture;
    [SerializeField] private ComputeShader splatMapShader;
    [SerializeField] private Mesh mesh;
    [SerializeField] public AnimationCurve heightCurve;
    [SerializeField] private float minHeight;
    [SerializeField] private float maxHeight;
    public float Lacunarity { get => lacunarity; }
    public int Octaves { get => octaves; }
    public float Persistence { get => persistence; }
    public float BaseFrequency { get => baseFrequency; }
    public float Amplitude { get => amplitude; }
    public int GridDepthSize { get => gridDepthSize; }
    public int GridWidthSize { get => gridWidthSize; }
    public float[,] CellMap { get => cellMap; set => cellMap = value; }
    public Biome[] Biomes { get => biomes; set => biomes = value; }
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public Mesh Mesh { get => mesh; set => mesh = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }

    [SerializeField] private Biome[] biomes;



    public TerrainGenerator(int width = 320, int depth = 320, float amplitude = 100f, float baseFrequency = 0.4f, float persistence=1f, int octaves=5, float lacunarity=2f)
    {
        gridWidthSize = width;
        gridDepthSize = depth;
        this.amplitude = amplitude;
        this.baseFrequency = baseFrequency;
        this.persistence = persistence;
        this.octaves = octaves;
        this.lacunarity = lacunarity;
        cellMap = new float[width + 1, depth + 1];
    }
    private void Awake()
    {
        cellMap ??= new float[gridWidthSize + 1, gridDepthSize + 1];
        mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Allows for larger meshes
        };
        mesh.MarkDynamic(); // Mark the mesh as dynamic
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }
    public void Start()
    {
        GenerateTerrain();
    }

    void GenerateTerrain()
    {
        HeightGenerator.GenerateHeightMap(this);
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(this);
        Texture2D splatmapcustom = SplatMapGenerator.GenerateSplatMap(this);
        if (splatmapcustom == null) Debug.Log("aa");
        GenerateTexture.AssignMaterial(splatmapcustom, this);
        meshData.UpdateMesh(mesh);
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
}


public static class HeightGenerator
{

    public static void GenerateHeightMap(TerrainGenerator terrainGenerator)
    {
        for (int y = 0; y <= terrainGenerator.GridDepthSize; y++)
        {
            for (int x = 0; x <= terrainGenerator.GridWidthSize; x++)
            {
                terrainGenerator.CellMap[x, y] = CalculateHeight(x, y, terrainGenerator);
            }
        }

    }
    private static float CalculateHeight(int x, int y, TerrainGenerator terrainGenerator)
    {
        float amplitude = terrainGenerator.Amplitude;
        float frequency = terrainGenerator.BaseFrequency;
        float height = 0;

        for (int o = 0; o < terrainGenerator.Octaves; o++)
        {
            float sampleX = x / (float)terrainGenerator.GridWidthSize * frequency;
            float sampleY = y / (float)terrainGenerator.GridDepthSize * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude ;
            //height = terrainGenerator.heightCurve.Evaluate(height);

            amplitude *= terrainGenerator.Persistence;
            frequency *= terrainGenerator.Lacunarity;

            // Early exit optimization
            if (amplitude < 0.001f)
                break;
        }
        if (height> terrainGenerator.MaxHeight) terrainGenerator.MaxHeight = height;
        if (height< terrainGenerator.MinHeight) terrainGenerator.MinHeight = height;
        if (GenericMethods.IsCurveConstant(terrainGenerator.heightCurve)) return height;
        // Normalize height to a range between 0 and 1
        float normalizedHeight = Mathf.InverseLerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, height);

        // Apply AnimationCurve to the normalized height
        float curvedHeight = terrainGenerator.heightCurve.Evaluate(normalizedHeight);

        // Desnormalize curvedHeight to original scale
        float originalHeight = Mathf.Lerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, curvedHeight);

        return originalHeight;
    }

}
public class MeshData 
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;
  
    public MeshData(int width, int depth)
    {
        vertices = new Vector3[(width + 1) * (depth + 1)];
        triangles = new int[width * depth * 6];
        uvs = new Vector2[(width + 1) * (depth + 1)];
    }

    public void UpdateMesh(Mesh mesh)
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
    }

}



public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator)
    {
        int width = terrainGenerator.GridWidthSize;
        int depth = terrainGenerator.GridDepthSize;
        MeshData meshData = new MeshData(width, depth);

        int vertexIndex = 0;
        int triangleIndex = 0;

        // Create the vertices and UVs based on the height map
        for (int y = 0; y <= depth; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                meshData.vertices[vertexIndex] = new Vector3(x * terrainGenerator.ScaleFactor, terrainGenerator.CellMap[x, y], y * terrainGenerator.ScaleFactor);
                meshData.uvs[vertexIndex] = new Vector2((float)x / width, (float)y / depth);
                vertexIndex++;

                // Skip the last row and column
                if (x < width && y < depth)
                {
                    int vert = y * (width + 1) + x;
                    meshData.triangles[triangleIndex++] = vert;
                    meshData.triangles[triangleIndex++] = vert + width + 1;
                    meshData.triangles[triangleIndex++] = vert + 1;
                    meshData.triangles[triangleIndex++] = vert + 1;
                    meshData.triangles[triangleIndex++] = vert + width + 1;
                    meshData.triangles[triangleIndex++] = vert + width + 2;
                }
            }
        }
        return meshData;
    }

}
public class SplatMapData
{
    private RenderTexture splatMap;
    private Texture2D splatMap2D;
    private ComputeBuffer biomeThresholdBuffer;

    public RenderTexture SplatMap { get => splatMap; set => splatMap = value; }
    public Texture2D SplatMap2D { get => splatMap2D; set => splatMap2D = value; }
}

public static class SplatMapGenerator
{
    public static Texture2D GenerateSplatMap(TerrainGenerator terrainGenerator)
    {

        Texture2D splatMap = new Texture2D(terrainGenerator.GridWidthSize, terrainGenerator.GridDepthSize);
        for (int y = 0; y < terrainGenerator.GridDepthSize; y++)
        {
            for (int x = 0; x < terrainGenerator.GridWidthSize; x++)
            {
                float height = terrainGenerator.CellMap[x, y];
                Color channelWeight = new Color(0, 0, 0, 0);
                // Normalize height to a range between 0 and 1
                float normalizedHeight = Mathf.InverseLerp(terrainGenerator.MinHeight, terrainGenerator.MaxHeight, height);
                // Convert normalized height to a range between 0 and 10
                float scaledHeight = normalizedHeight * 10f;


                // Here you would determine the blend weights for each texture
                // based on height or other criteria, like slope, noise, etc.
                // For a simple height-based blend, you might do something like this:
                for (int i = 0; i < terrainGenerator.Biomes.Length; i++)
                {
                    if (scaledHeight >= terrainGenerator.Biomes[i].minHeight && scaledHeight <= terrainGenerator.Biomes[i].maxHeight)
                    {
                        channelWeight[i] = 1; // Assign full weight to the corresponding channel
                        break; // Exit the loop since we found our height range
                    }
                }

                // Set the pixel values on the splat map
                splatMap.SetPixel(x, y, channelWeight);
            }
        }
        splatMap.Apply(); // Apply all SetPixel changes
        return splatMap;
    }
    public static void GenerateSplatMapOnGPU(TerrainGenerator terrainGenerator, SplatMapData splatMapData)
    {
        int kernelHandle = terrainGenerator.SplatMapShader.FindKernel("CSMain");

        // Create a RenderTexture with the same dimensions as the height map
        splatMapData.SplatMap = new RenderTexture(terrainGenerator.GridWidthSize, terrainGenerator.GridDepthSize, 0, RenderTextureFormat.ARGB32);
        splatMapData.SplatMap.enableRandomWrite = true;
        splatMapData.SplatMap.Create();

        // Convert heightMap to a single-dimensional array for ComputeBuffer
        float[] heightMapArray = new float[terrainGenerator.GridWidthSize * terrainGenerator.GridDepthSize];
        Buffer.BlockCopy(terrainGenerator.CellMap, 0, heightMapArray, 0, heightMapArray.Length * sizeof(float));

        // Create ComputeBuffer for height map and biome thresholds
        using (ComputeBuffer heightMapBuffer = new ComputeBuffer(heightMapArray.Length, sizeof(float)))
        using (ComputeBuffer biomeThresholdBuffer = new ComputeBuffer(terrainGenerator.Biomes.Length, sizeof(float) * 2))
        {
            // Set data for buffers
            heightMapBuffer.SetData(heightMapArray);

            // Create an array for biome thresholds and set data
            Vector2[] biomeThresholds = new Vector2[terrainGenerator.Biomes.Length];
            for (int i = 0; i < terrainGenerator.Biomes.Length; i++)
            {
                biomeThresholds[i] = new Vector2(terrainGenerator.Biomes[i].minHeight, terrainGenerator.Biomes[i].maxHeight);
            }
            biomeThresholdBuffer.SetData(biomeThresholds);

            // Set buffers for the Compute Shader
            terrainGenerator.SplatMapShader.SetBuffer(kernelHandle, "HeightMap", heightMapBuffer);
            terrainGenerator.SplatMapShader.SetBuffer(kernelHandle, "BiomeThresholds", biomeThresholdBuffer);
            terrainGenerator.SplatMapShader.SetTexture(kernelHandle, "Result", splatMapData.SplatMap);

            // Dispatch the Compute Shader
            terrainGenerator.SplatMapShader.Dispatch(kernelHandle, terrainGenerator.GridWidthSize / 8, terrainGenerator.GridDepthSize / 8, 1); //splatMapShader.Dispatch(kernelHandle, gridWidthSize * 0.125, gridDephtSize * 0.125, 1);
        }

        // The splatMap RenderTexture can now be used as the splat map.
        // You must update AssignMaterial to accept a RenderTexture instead of a Texture2D.
    }
}
public static class GenerateTexture
{
    public static void AssignMaterial(Texture2D splatMap, TerrainGenerator terrainGenerator)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.Biomes[0].texture);
        mat.SetTexture("_TextureG", terrainGenerator.Biomes[1].texture);
        mat.SetTexture("_TextureB", terrainGenerator.Biomes[2].texture);
        mat.SetTexture("_TextureA", terrainGenerator.Biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        terrainGenerator.GetComponent<Renderer>().sharedMaterial = mat;
    }
    public static void AssignMaterial(RenderTexture splatMap, TerrainGenerator terrainGenerator)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.Biomes[0].texture);
        mat.SetTexture("_TextureG", terrainGenerator.Biomes[1].texture);
        mat.SetTexture("_TextureB", terrainGenerator.Biomes[2].texture);
        mat.SetTexture("_TextureA", terrainGenerator.Biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        terrainGenerator.GetComponent<Renderer>().sharedMaterial = mat;
    }


}