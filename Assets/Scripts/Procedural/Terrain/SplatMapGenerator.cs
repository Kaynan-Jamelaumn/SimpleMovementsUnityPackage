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
    public static Texture2D GenerateSplatMap(TerrainGenerator terrainGenerator, float[,] heightMap)
    {

        Texture2D splatMap = new Texture2D(terrainGenerator.GridWidthSize, terrainGenerator.GridDepthSize);
        for (int y = 0; y < terrainGenerator.GridDepthSize; y++)
        {
            for (int x = 0; x < terrainGenerator.GridWidthSize; x++)
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

                // Set the pixel values on the splat map
                splatMap.SetPixel(x, y, channelWeight);
            }
        }
        splatMap.Apply(); // Apply all SetPixel changes
        return splatMap;
    }
    public static Texture2D GenerateSplatMapOutsideMainThread(TerrainGenerator terrainGenerator, float[,] heightMap, Texture2D splatMap)
    {
        for (int y = 0; y < terrainGenerator.GridDepthSize; y++)
        {
            for (int x = 0; x < terrainGenerator.GridWidthSize; x++)
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

                // Set the pixel values on the splat map
                splatMap.SetPixel(x, y, channelWeight);
            }
        }
        splatMap.Apply(); // Apply all SetPixel changes
        return splatMap;
    }
    public static void GenerateSplatMapOnGPU(SingleTerrainGenerator terrainGenerator, SplatMapData splatMapData, float[,] heightMap)
    {
        int kernelHandle = terrainGenerator.SplatMapShader.FindKernel("CSMain");

        // Create a RenderTexture with the same dimensions as the height map
        splatMapData.SplatMap = new RenderTexture(terrainGenerator.GridWidthSize, terrainGenerator.GridDepthSize, 0, RenderTextureFormat.ARGB32);
        splatMapData.SplatMap.enableRandomWrite = true;
        splatMapData.SplatMap.Create();

        // Convert heightMap to a single-dimensional array for ComputeBuffer
        float[] heightMapArray = new float[terrainGenerator.GridWidthSize * terrainGenerator.GridDepthSize];
        Buffer.BlockCopy(heightMap, 0, heightMapArray, 0, heightMapArray.Length * sizeof(float));

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
