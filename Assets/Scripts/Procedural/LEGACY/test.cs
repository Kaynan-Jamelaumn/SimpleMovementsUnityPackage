using System;
using UnityEngine;

public class SplatMapper
{
    public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
    {
        int chunkSize = terrainGenerator.ChunkSize;
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;
        int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);

        // Initialize the splat maps
        Texture2D[] splatMaps = new Texture2D[numSplatMaps];
        for (int i = 0; i < numSplatMaps; i++)
        {
            splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
        }

        // Iterate over each point in the chunk
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                Biome biome = biomeMap[x, y];
                int biomeIndex = Array.FindIndex(terrainGenerator.BiomeDefinitions, def => def.BiomePrefab.name == biome.name);

                if (biomeIndex >= 0)
                {
                    int splatMapIndex = biomeIndex / 4; // Determine which splat map to use
                    int channelIndex = biomeIndex % 4; // Determine which channel to modify

                    // Get the current color for all splat maps
                    Color[] colors = new Color[numSplatMaps];
                    for (int i = 0; i < numSplatMaps; i++)
                        colors[i] = splatMaps[i].GetPixel(x, y);

                    // Update the correct channel in the correct splat map
                    colors[splatMapIndex][channelIndex] = 1;

                    // Set the updated colors back to the splat maps
                    for (int i = 0; i < numSplatMaps; i++)
                        splatMaps[i].SetPixel(x, y, colors[i]);
                }
            }
        }

        // Apply changes to all splat maps
        foreach (var splatMap in splatMaps)
        {
            splatMap.Apply();
        }

        return splatMaps;
    }
}

//        }