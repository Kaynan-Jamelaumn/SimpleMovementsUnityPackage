using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class NewTerrain : MonoBehaviour
{
    [SerializeField] private int gridWidthSize = 320; // Number of grid cells
    [SerializeField] private int gridDephtSize = 320; // Number of grid cells
    [SerializeField] private float scaleFactor = 3.0f; // Scale factor for the terrain size
    [SerializeField] private float lodDistance = 50f; // Distance for LOD switching
    [SerializeField] private float persistence = 1f; // Adjust persistence to control the smoothness of terrain
    [SerializeField] private int octaves = 5; // Octaves for fractal noise
    [SerializeField] private float baseFrequency = 0.4f; // Adjust base frequency to control the scale of terrain features
    [SerializeField] private float lacunarity = 2f; // Adjust base frequency to control the scale of terrain features
    [SerializeField] private float amplitude = 100f; // Adjust amplitude to control the height of terrain features

    [SerializeField] private float maxHeight;
    [SerializeField] private float minHeight;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;
    //private Color[] colors; // Array to store color data for each vertex
    [SerializeField] private Texture2D defaultTexture;
    private float[,] heightMap; // Declare heightMap as a member variable

    [System.Serializable]
    public class biome
    {
        public string name;
        public float minHeight;
        public float maxHeight;
        //public color color;
        public Texture2D texture;
    }
    [SerializeField] private biome[] biomes;


    [SerializeField] private ComputeShader splatMapShader;
    private RenderTexture splatMap;
    private ComputeBuffer biomeThresholdBuffer;

    private void Start()
    {
        mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Allows for larger meshes
        };
        mesh.MarkDynamic(); // Mark the mesh as dynamic
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        GenerateTerrainAsync();
    }
    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private async void GenerateTerrainAsync()
    {
        await Task.Run(() => CreateShape());
        //Texture2D splatMap = GenerateSplatMap(heightMap); // Ensure you have a heightMap variable accessible
        //AssignMaterial(splatMap);
        GenerateSplatMapOnGPU(heightMap); // New method for GPU processing
        AssignMaterial(splatMap); // Use RenderTexture instead of Texture2D
        UpdateMesh();
    }
    // void AssignMaterial(Texture2D splatMap)
    private void AssignMaterial(RenderTexture splatMap)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", biomes[0].texture);
        mat.SetTexture("_TextureG", biomes[1].texture);
        mat.SetTexture("_TextureB", biomes[2].texture);
        mat.SetTexture("_TextureA", biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        GetComponent<Renderer>().sharedMaterial = mat;
    }
    private void AssignMaterial(Texture2D splatMap)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", biomes[0].texture);
        mat.SetTexture("_TextureG", biomes[1].texture);
        mat.SetTexture("_TextureB", biomes[2].texture);
        mat.SetTexture("_TextureA", biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        GetComponent<Renderer>().sharedMaterial = mat;
    }
    private void GenerateSplatMapOnGPU(float[,] heightMap)
    {
        int kernelHandle = splatMapShader.FindKernel("CSMain");

        // Create a RenderTexture with the same dimensions as the height map
        splatMap = new RenderTexture(gridWidthSize, gridDephtSize, 0, RenderTextureFormat.ARGB32);
        splatMap.enableRandomWrite = true;
        splatMap.Create();

        // Convert heightMap to a single-dimensional array for ComputeBuffer
        float[] heightMapArray = new float[gridWidthSize * gridDephtSize];
        Buffer.BlockCopy(heightMap, 0, heightMapArray, 0, heightMapArray.Length * sizeof(float));

        // Create ComputeBuffer for height map and biome thresholds
        using (ComputeBuffer heightMapBuffer = new ComputeBuffer(heightMapArray.Length, sizeof(float)))
        using (ComputeBuffer biomeThresholdBuffer = new ComputeBuffer(biomes.Length, sizeof(float) * 2))
        {
            // Set data for buffers
            heightMapBuffer.SetData(heightMapArray);

            // Create an array for biome thresholds and set data
            Vector2[] biomeThresholds = new Vector2[biomes.Length];
            for (int i = 0; i < biomes.Length; i++)
            {
                biomeThresholds[i] = new Vector2(biomes[i].minHeight, biomes[i].maxHeight);
            }
            biomeThresholdBuffer.SetData(biomeThresholds);

            // Set buffers for the Compute Shader
            splatMapShader.SetBuffer(kernelHandle, "HeightMap", heightMapBuffer);
            splatMapShader.SetBuffer(kernelHandle, "BiomeThresholds", biomeThresholdBuffer);
            splatMapShader.SetTexture(kernelHandle, "Result", splatMap);

            // Dispatch the Compute Shader
            splatMapShader.Dispatch(kernelHandle, gridWidthSize / 8, gridDephtSize / 8, 1); //splatMapShader.Dispatch(kernelHandle, gridWidthSize * 0.125, gridDephtSize * 0.125, 1);
        }

        // The splatMap RenderTexture can now be used as the splat map.
        // You must update AssignMaterial to accept a RenderTexture instead of a Texture2D.
    }


    private void CreateShape()
    {
        int width = gridWidthSize;
        int depth = gridDephtSize;
        vertices = new Vector3[(width + 1) * (depth + 1)];
        triangles = new int[width * depth * 6];
        uvs = new Vector2[(width + 1) * (depth + 1)];
        heightMap = new float[width + 1, depth + 1];

        // Generate the height map
        for (int y = 0; y <= depth; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                heightMap[x, y] = CalculateHeight(x, y);
            }
        }

        int vertexIndex = 0;
        int triangleIndex = 0;

        // Create the vertices and UVs based on the height map
        for (int y = 0; y <= depth; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                vertices[vertexIndex] = new Vector3(x * scaleFactor, heightMap[x, y], y * scaleFactor);
                uvs[vertexIndex] = new Vector2((float)x / width, (float)y / depth);
                vertexIndex++;

                // Skip the last row and column
                if (x < width && y < depth)
                {
                    int vert = y * (width + 1) + x;
                    triangles[triangleIndex++] = vert;
                    triangles[triangleIndex++] = vert + width + 1;
                    triangles[triangleIndex++] = vert + 1;
                    triangles[triangleIndex++] = vert + 1;
                    triangles[triangleIndex++] = vert + width + 1;
                    triangles[triangleIndex++] = vert + width + 2;
                }
            }
        }
    }


    float CalculateHeight(int x, int y)
    {
        float amplitude = this.amplitude;
        float frequency = this.baseFrequency;
        float height = 0;

        for (int o = 0; o < octaves; o++)
        {
            float sampleX = x / (float)gridWidthSize * frequency;
            float sampleY = y / (float)gridDephtSize * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX + 0.5f, sampleY + 0.5f) * 2 - 1;
            height += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;

            // Early exit optimization
            if (amplitude < 0.001f)
                break;
        }
        //if (height>maxHeight) maxHeight = height;
        //if (height<minHeight) minHeight = height;
        

        return height;
    }


    private Texture2D GenerateSplatMap(float[,] heightMap)
    {
        Texture2D splatMap = new Texture2D(gridWidthSize, gridDephtSize);
        for (int y = 0; y < gridDephtSize; y++)
        {
            for (int x = 0; x < gridWidthSize; x++)
            {
                float height = heightMap[x, y];
                Color channelWeight = new Color(0, 0, 0, 0);

                // Here you would determine the blend weights for each texture
                // based on height or other criteria, like slope, noise, etc.
                // For a simple height-based blend, you might do something like this:
                for (int i = 0; i < biomes.Length; i++)
                {
                    if (height >= biomes[i].minHeight && height <= biomes[i].maxHeight)
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

    private Texture2D DetermineTexture(float height)
    {
        foreach (var biome in biomes)
        {
            if (height >= biome.minHeight && height <= biome.maxHeight)
            {
                return biome.texture;
            }
        }
        // If no matching biome is found, return null
        return defaultTexture;
    }
    void OnDestroy()
    {
        // Release the buffer to prevent memory leaks
        if (biomeThresholdBuffer != null)
        {
            biomeThresholdBuffer.Release();
        }
    }

}



//Color DetermineBiomeColor(float height)
//{
//    foreach (var biome in biomes)
//    {
//        if (height >= biome.minHeight && height <= biome.gridDephtSize)
//        {
//           return biome.color;
//        }
//    }
//    return Color.white; // Fallback color if no biome is found
//}

// This example assumes you have a maximum of 4 textures, represented by RGBA channels.








