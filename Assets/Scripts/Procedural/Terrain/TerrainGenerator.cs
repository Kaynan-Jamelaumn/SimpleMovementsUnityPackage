using UnityEngine;

[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(TerrainCollider))] // Add this line to require a Terrain Collider component
public class TerrainGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale =  20f;

    public int octaves = 4;
    [Range(0, 1)]
    public float persistence = 0.5f;
    public float lacunarity = 2.0f;

    public int seed;
    public Vector2 offset;

    private void Start()
    {
        GenerateTerrain();
        if (seed == 0) RandomizeSeed(); 

    }

    public void RandomizeSeed()
    {
        seed = Random.Range(0, int.MaxValue);
        offset = new Vector2(Random.Range(0, int.MaxValue), Random.Range(0, int.MaxValue));
    }

    void GenerateTerrain()
    {
        Terrain terrain = GetComponent<Terrain>();
        terrain.terrainData = GenerateTerrainData();

        // Add collider to the terrain
        TerrainCollider terrainCollider = gameObject.GetComponent<TerrainCollider>();
        terrainCollider.terrainData = terrain.terrainData;
    }

    TerrainData GenerateTerrainData()
    {
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = width + 1,
            size = new Vector3(width, 50, height)
        };

        terrainData.SetHeights(0, 0, GenerateHeights());
        return terrainData;
    }

    float[,] GenerateHeights()
    {
        float[,] heights = new float[width, height];
        Vector2[] octaveOffsets = GenerateOctaveOffsets();

        float maxPossibleHeight = CalculateMaxPossibleHeight();
        float halfWidth = width / 2f;
        float halfHeight = height / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heights[x, y] = CalculateNormalizedHeight(x, y, octaveOffsets, halfWidth, halfHeight, maxPossibleHeight);
            }
        }
        return heights;
    }

    Vector2[] GenerateOctaveOffsets()
    {
        Vector2[] octaveOffsets = new Vector2[octaves];
        System.Random prng = new System.Random(seed);

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }
        return octaveOffsets;
    }

    float CalculateMaxPossibleHeight()
    {
        float amplitude = 1;
        float maxPossibleHeight = 0;

        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amplitude;
            amplitude *= persistence;
        }
        return maxPossibleHeight;
    }

    float CalculateNormalizedHeight(int x, int y, Vector2[] octaveOffsets, float halfWidth, float halfHeight, float maxPossibleHeight)
    {
        float noiseHeight = CalculateNoiseHeight(x, y, octaveOffsets, halfWidth, halfHeight);
        return (noiseHeight + 1) / (2 * maxPossibleHeight);
    }

    float CalculateNoiseHeight(int x, int y, Vector2[] octaveOffsets, float halfWidth, float halfHeight)
    {
        float amplitude = 1;
        float frequency = 1;
        float noiseHeight = 0;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = (x - halfWidth) / scale * frequency + octaveOffsets[i].x;
            float sampleY = (y - halfHeight) / scale * frequency + octaveOffsets[i].y;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return noiseHeight;
    }
}
