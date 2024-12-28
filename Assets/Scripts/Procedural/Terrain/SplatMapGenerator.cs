using System;
using UnityEngine;
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
    public static Texture2D GenerateSplatMapOutsideMainThread(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Texture2D splatMap)
    {
        Color[] colorMap = new Color[terrainGenerator.ChunkSize * terrainGenerator.ChunkSize];

        for (int y = 0; y < terrainGenerator.ChunkSize; y++)
        {
            for (int x = 0; x < terrainGenerator.ChunkSize; x++)
            {
                Biome biome = biomeMap[x, y];
                Color channelWeight = new Color(0, 0, 0, 0);

                for (int i = 0; i < terrainGenerator.Biomes.Length; i++)
                {
                    if (terrainGenerator.Biomes[i].name == biome.name)
                    {
                        channelWeight[i % 4] = 1; // Assign weight for the first 4 channels
                        break;
                    }
                }

                colorMap[y * terrainGenerator.ChunkSize + x] = channelWeight;
            }
        }

        splatMap.SetPixels(colorMap);
        splatMap.Apply();
        return splatMap;
    }
    //public static Texture2D GenerateSplatMapOutsideMainThread4Textures(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Texture2D splatMap)
    //{
    //    Color[] colorMap = new Color[terrainGenerator.ChunkSize * terrainGenerator.ChunkSize];

    //    for (int y = 0; y < terrainGenerator.ChunkSize; y++)
    //    {
    //        for (int x = 0; x < terrainGenerator.ChunkSize; x++)
    //        {
    //            Biome biome = biomeMap[x, y];
    //            Color channelWeight = new Color(0, 0, 0, 0);

    //            for (int i = 0; i < terrainGenerator.Biomes.Length; i++)
    //            {
    //                if (terrainGenerator.Biomes[i].name == biome.name)
    //                {
    //                    channelWeight[i] = 1; // Assign full weight to the corresponding channel
    //                    break;
    //                }
    //            }

    //            colorMap[y * terrainGenerator.ChunkSize + x] = channelWeight;
    //        }
    //    }

    //    splatMap.SetPixels(colorMap); // Set all pixels at once
    //    splatMap.Apply(); // Apply all SetPixel changes
    //    return splatMap;
    //}
    public static Texture2D GenerateSplatMapBasedOnHeight(TerrainGenerator terrainGenerator, float[,] heightMap, Texture2D splatMap)
    {

        Color[] colorMap = new Color[terrainGenerator.ChunkSize * terrainGenerator.ChunkSize];

        for (int y = 0; y < terrainGenerator.ChunkSize; y++)
        {
            for (int x = 0; x < terrainGenerator.ChunkSize; x++)
            {
                float height = heightMap[x, y];
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

                colorMap[y * terrainGenerator.ChunkSize + x] = channelWeight;
            }
        }

        splatMap.SetPixels(colorMap); // Set all pixels at once
        splatMap.Apply(); // Apply all SetPixel changes
        return splatMap;
    }

}
