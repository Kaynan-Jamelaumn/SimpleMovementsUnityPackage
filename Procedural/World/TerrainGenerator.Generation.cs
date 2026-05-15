using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;

// TerrainGenerator, part 3 of 4: generating one chunk's heights and biome map (see TerrainGenerator.cs).
public partial class TerrainGenerator : MonoBehaviour
{
    /// <summary>
    /// Generates terrain data based on the given global offset.
    /// </summary>
    /// <param name="globalOffset">The global offset for the terrain.</param>
    /// <returns>A MapData object containing the height map.</returns>
    private MapData GenerateTerrain(Vector2 globalOffset)
    {
        // Local, not a field: this runs on its own worker thread per chunk (see RequestMapData),
        // and every chunk shares this same TerrainGenerator instance, so a shared field here would
        // race between concurrently-generating chunks.
        float[,] localHeightMap = HeightGenerator.GenerateHeightMap(this, globalOffset, out float[,] erosionDeltaMap, out WaterMapData waterData);
        return new MapData(localHeightMap, null, erosionDeltaMap, waterData);
    }

    /// <summary>
    /// Generates a biome map based on the given global offset and height map.
    /// </summary>
    /// <param name="globalOffset">The global offset for the biome map.</param>
    /// <param name="heightMap">The height map for the terrain.</param>
    /// <returns>A 2D array of Biome objects.</returns>
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap)
    {
        return GenerateBiomeMap(globalOffset, heightMap, false, out _);
    }

    /// <summary>
    /// Generates the biome map and, when <paramref name="computeSplatBlend"/> is set, the chunk's per-pixel
    /// splat blend in the same pass (see <see cref="SplatBlendData"/>) - so that work happens here on the
    /// worker thread instead of on the main thread, and, with ocean or volcanic biomes (where the biome map
    /// is the top entry of the same blend), each blend is looked up once instead of twice.
    /// </summary>
    public Biome[,] GenerateBiomeMap(Vector2 globalOffset, float[,] heightMap, bool computeSplatBlend, out SplatBlendData splatBlend)
    {
        Biome[,] biomeMap = new Biome[ChunkSize, ChunkSize];
        // The same sampler the terrain is built with, so ocean and volcanic biomes land where the terrain has them.
        TerrainHeightSampler sampler = new TerrainHeightSampler(this, EnableWater ? WaterSettings.From(this) : null);

        splatBlend = computeSplatBlend ? new SplatBlendData(ChunkSize, SplatTexturesPerPixel) : null;
        SplatBiomeIndex splatIndices = computeSplatBlend ? new SplatBiomeIndex(SplatMapGenerator.BiomeIndexMap(this)) : null;
        // With ocean/volcanic biomes, a cell's biome is the top of its texture blend (see SampleBiome).
        bool shareBlend = computeSplatBlend && sampler.HasSpecialBiomes;

        for (int y = 0; y < ChunkSize; y++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                float worldX = globalOffset.x + x, worldY = globalOffset.y + y;
                if (shareBlend)
                {
                    List<VoronoiBiomeGenerator.BiomeWeight> blend = sampler.GetTextureBlend(worldX, worldY);
                    biomeMap[x, y] = blend.Count > 0 ? blend[0].Biome : null;
                    SplatMapGenerator.FillBlendSlots(blend, splatIndices, splatBlend, x, y);
                    continue;
                }

                biomeMap[x, y] = sampler.SampleBiome(worldX, worldY);
                if (computeSplatBlend)
                    SplatMapGenerator.FillBlendSlots(sampler.GetTextureBlend(worldX, worldY), splatIndices, splatBlend, x, y);
            }
        }

        return biomeMap;
    }
}
