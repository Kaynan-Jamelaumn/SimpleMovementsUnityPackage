using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    [SerializeField] private int gridWidthSize = 300; // Number of grid cells
    [SerializeField] private int gridDephtSize = 300; // Number of grid cells
    [SerializeField] private float scaleFactor = 3.0f; // Scale factor for the terrain size
    [SerializeField] private Material terrainMaterial; // Assign a material with a texture in the editor
    [SerializeField] private float lodDistance = 50f; // Distance for LOD switching
    [SerializeField] private float persistence = 1f; // Adjust persistence to control the smoothness of terrain
    [SerializeField] private int octaves = 5; // Octaves for fractal noise
    [SerializeField] private float baseFrequency = 0.4f; // Adjust base frequency to control the scale of terrain features
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
    private void Start()
    {
        mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Allows for larger meshes
        };
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        GenerateTerrainAsync();
    }
    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        //mesh.colors = colors; // Assign colors to the mesh
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private async void GenerateTerrainAsync()
    {
        await Task.Run(() => CreateShape());
        Texture2D splatMap = GenerateSplatMap(heightMap); // Ensure you have a heightMap variable accessible
        AssignMaterial(splatMap);
        UpdateMesh();
    }


    private void CreateShape()
    {
        int width = gridWidthSize;
        int depht = gridDephtSize;
        List<Vector3> vertexList = new List<Vector3>();
        List<int> triangleList = new List<int>();
        List<Vector2> uvList = new List<Vector2>();
        heightMap = new float[width + 1, depht + 1];

        // Generate the height map
        for (int y = 0; y <= depht; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                heightMap[x, y] = CalculateHeight(x, y);
            }
        }

        // Create the vertices based on the height map and assign UV coordinates
        for (int y = 0; y <= depht; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                Vector3 vertex = new Vector3(x * scaleFactor, heightMap[x, y], y * scaleFactor);
                vertexList.Add(vertex);

                // Determine texture for the current vertex
                //Texture2D texture = DetermineTexture(heightMap[x, y]);
                float u = (float)x / width;
                float v = (float)y / depht;
                uvList.Add(new Vector2(u, v));
            }
        }

        // Create the triangles (two triangles per square)
        for (int y = 0; y < depht; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int vert = y * (width + 1) + x;
                triangleList.Add(vert);
                triangleList.Add(vert + width + 1);
                triangleList.Add(vert + 1);

                triangleList.Add(vert + 1);
                triangleList.Add(vert + width + 1);
                triangleList.Add(vert + width + 2);
            }
        }

        vertices = vertexList.ToArray();
        triangles = triangleList.ToArray();
        uvs = uvList.ToArray();
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
            frequency *= 2;

            // Early exit optimization
            if (amplitude < 0.001f)
                break;
        }
        if (height>maxHeight) maxHeight = height;
        if (height<minHeight) minHeight = height;
        

        return height;
    }

    private void AssignMaterial(Texture2D splatMap)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", biomes[0].texture);
        mat.SetTexture("_TextureG", biomes[1].texture);
        mat.SetTexture("_TextureB", biomes[2].texture);
        mat.SetTexture("_TextureA", biomes[3].texture);
        mat.SetTexture("_SplatMap", splatMap);
        GetComponent<Renderer>().sharedMaterial = mat;
    }
    void Update()
    {
        // Check distance for LOD
        float distanceToPlayer = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (distanceToPlayer > lodDistance)
        {
            // Apply LOD adjustments here
        }
        else
        {
            // Apply regular mesh rendering here
        }
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