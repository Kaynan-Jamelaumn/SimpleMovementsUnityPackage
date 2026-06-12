using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// SplatMapGenerator, part 2 of 2: blended splat maps from the biome blend (see SplatMapGenerator.cs).
public static partial class SplatMapGenerator
{
    /// <param name="worldOrigin">
    /// The chunk's true global offset, always required (regardless of texture variations) when biome-blended
    /// texturing is enabled, so per-pixel world positions can be resolved for smooth border blending.
    /// </param>
    /// <param name="globalOffset">Global offset for the chunk. Only used for texture variations when enabled.</param>
    /// <param name="precomputedBlend">
    /// The chunk's per-pixel biome blend when it was already computed off the main thread (see
    /// <see cref="TerrainGenerator.GenerateBiomeMap(Vector2, float[,], bool, out SplatBlendData)"/>); null to compute it here.
    /// </param>
    public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Vector2 worldOrigin, Vector2 globalOffset = default, SplatBlendData precomputedBlend = null)
    {
        return CreateSplatTextures(GenerateSplatPixels(terrainGenerator, biomeMap, worldOrigin, precomputedBlend), terrainGenerator.ChunkSize);
    }

    /// <summary>
    /// The pixels of a chunk's splat maps, without creating any texture - so this can run on a worker thread (see
    /// <see cref="TextureGenerator.CreateChunkMaterial"/>, which makes the textures from them). One RGBA array per
    /// splat map; splat map i, channel c holds the weight of biome i * 4 + c (in <see cref="TerrainGenerator.BiomeDefinitions"/>
    /// order). With biome-blended texturing a pixel mixes up to <see cref="TerrainGenerator.SplatTexturesPerPixel"/>
    /// biomes near borders, otherwise it is fully its biome.
    /// </summary>
    /// <param name="worldOrigin">The chunk's global offset (needed to blend across biome borders).</param>
    /// <param name="precomputedBlend">The chunk's per-pixel biome blend if already computed (see <see cref="TerrainGenerator.GenerateBiomeMap(Vector2, float[,], bool, out SplatBlendData)"/>); null to compute it here.</param>
    public static Color32[][] GenerateSplatPixels(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Vector2 worldOrigin, SplatBlendData precomputedBlend = null)
    {
        int chunkSize = terrainGenerator.ChunkSize;
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;
        // Each splat map stores up to 4 biomes in RGBA.
        int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);
        int totalPixels = chunkSize * chunkSize;
        var biomeIndexMap = BiomeIndexMap(terrainGenerator);

        var pixels = new Color32[numSplatMaps][];
        for (int i = 0; i < numSplatMaps; i++)
            pixels[i] = new Color32[totalPixels];

        // When enabled, blend up to SplatTexturesPerPixel biomes per pixel (weights summing to 1) near cell
        // borders so texture transitions match the smoothly blended terrain height instead of cutting hard.
        bool useBlending = terrainGenerator.TerrainTextureBasedOnVoronoiPoints && terrainGenerator.UseBiomeBlendedTexturing;
        if (useBlending)
        {
            SplatBlendData blend = precomputedBlend != null && precomputedBlend.Size == chunkSize && precomputedBlend.Slots == terrainGenerator.SplatTexturesPerPixel
                ? precomputedBlend
                : ComputeBlend(terrainGenerator, worldOrigin, chunkSize, biomeIndexMap);
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int pixelIndex = y * chunkSize + x;
                    for (int slot = 0; slot < blend.Slots; slot++)
                    {
                        int biomeIndex = blend.Indices[x, y, slot];
                        if (biomeIndex < 0)
                            continue;
                        byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(blend.Weights[x, y, slot]) * 255f);
                        Color32 pixel = pixels[biomeIndex / 4][pixelIndex];
                        switch (biomeIndex % 4)
                        {
                            case 0: pixel.r = value; break;
                            case 1: pixel.g = value; break;
                            case 2: pixel.b = value; break;
                            case 3: pixel.a = value; break;
                        }
                        pixels[biomeIndex / 4][pixelIndex] = pixel;
                    }
                }
            }
            return pixels;
        }

        // One biome per pixel: full weight in its channel. (Texture variations don't change the splat maps.)
        var indices = new SplatBiomeIndex(biomeIndexMap, terrainGenerator.SplatIndexByBiome);
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int biomeIndex = indices[biomeMap[x, y]];
                Color32 pixel = new Color32(0, 0, 0, 0);
                switch (biomeIndex % 4)
                {
                    case 0: pixel.r = 255; break;
                    case 1: pixel.g = 255; break;
                    case 2: pixel.b = 255; break;
                    case 3: pixel.a = 255; break;
                }
                pixels[biomeIndex / 4][y * chunkSize + x] = pixel;
            }
        }
        return pixels;
    }

    /// <summary>Splat map textures from <see cref="GenerateSplatPixels"/> (main thread only).</summary>
    public static Texture2D[] CreateSplatTextures(Color32[][] pixels, int size)
    {
        var splatMaps = new Texture2D[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            splatMaps[i] = new Texture2D(size, size, TextureFormat.RGBA32, false);
            splatMaps[i].SetPixelData(pixels[i], 0);
            splatMaps[i].Apply();
        }
        return splatMaps;
    }

    /// <summary>
    /// Biome name -> index into <see cref="TerrainGenerator.BiomeDefinitions"/> (a later duplicate name wins). The copy
    /// the generator prepared on the main thread when there is one (reading asset names is main-thread work).
    /// </summary>
    public static Dictionary<string, int> BiomeIndexMap(TerrainGenerator terrainGenerator)
    {
        return terrainGenerator.SplatIndexByName ?? ComputeBiomeIndexMap(terrainGenerator);
    }

    /// <summary>As <see cref="BiomeIndexMap"/>, always computed now (main thread).</summary>
    public static Dictionary<string, int> ComputeBiomeIndexMap(TerrainGenerator terrainGenerator)
    {
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;
        var biomeIndexMap = new Dictionary<string, int>(numBiomes);
        for (int i = 0; i < numBiomes; i++)
        {
            // An empty slot keeps its index (the others' indices don't move) but maps no name.
            BiomeInstance instance = terrainGenerator.BiomeDefinitions[i];
            if (instance != null && instance.BiomePrefab != null)
                biomeIndexMap[instance.BiomePrefab.name] = i;
        }
        return biomeIndexMap;
    }

    /// <summary>The per-pixel biome blend for a chunk (the same biome layout the terrain was built from).</summary>
    private static SplatBlendData ComputeBlend(TerrainGenerator terrainGenerator, Vector2 worldOrigin, int chunkSize, Dictionary<string, int> biomeIndexMap)
    {
        var data = new SplatBlendData(chunkSize, terrainGenerator.SplatTexturesPerPixel);
        var indices = new SplatBiomeIndex(biomeIndexMap, terrainGenerator.SplatIndexByBiome);

        // Same biome layout the terrain was built from, including ocean biomes along the coast and a
        // volcanic biome over volcanoes (see TerrainHeightSampler.GetTextureBlend).
        TerrainHeightSampler sampler = new TerrainHeightSampler(terrainGenerator,
            terrainGenerator.EnableWater ? WaterSettings.From(terrainGenerator) : null);

        Parallel.For(0, chunkSize, y =>
        {
            for (int x = 0; x < chunkSize; x++)
                FillBlendSlots(sampler.GetTextureBlend(worldOrigin.x + x, worldOrigin.y + y), indices, data, x, y);
        });
        return data;
    }

    /// <summary>One pixel of <see cref="SplatBlendData"/> from its biome blend.</summary>
    public static void FillBlendSlots(List<VoronoiBiomeGenerator.BiomeWeight> blend, SplatBiomeIndex indices, SplatBlendData data, int x, int y)
    {
        // Blend entries come sorted by weight; where more biomes meet than there are splat slots, the
        // strongest ones are kept and renormalized to still sum to 1.
        int slots = data.Slots;
        float slotTotal = 0f;
        for (int slot = 0; slot < slots && slot < blend.Count; slot++)
            slotTotal += blend[slot].Weight;

        for (int slot = 0; slot < slots; slot++)
        {
            if (slot < blend.Count)
            {
                data.Indices[x, y, slot] = indices[blend[slot].Biome];
                data.Weights[x, y, slot] = slotTotal > 0f ? blend[slot].Weight / slotTotal : 0f;
            }
            else
            {
                data.Indices[x, y, slot] = -1;
                data.Weights[x, y, slot] = 0f;
            }
        }
    }
}
