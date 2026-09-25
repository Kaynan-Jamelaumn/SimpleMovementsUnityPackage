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
        // Get the terrain's chunk size (resolution) and number of defined biomes
        int chunkSize = terrainGenerator.ChunkSize;
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;

        // Calculate the number of splatmaps needed (each can store up to 4 biomes in RGBA)
        int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);
        // Calculate the total number of pixels in a single splatmap
        int totalPixels = chunkSize * chunkSize;

        // Precompute a dictionary mapping biome names to their indices for fast lookup
        var biomeIndexMap = BiomeIndexMap(terrainGenerator);

        // Precompute RGBA channel values for each biome index
        var channelValues = new Color32[numBiomes];
        for (int i = 0; i < numBiomes; i++)
        {
            // Assign RGBA values based on biome index modulo 4
            switch (i % 4)
            {
                case 0: channelValues[i] = new Color32(255, 0, 0, 0); break;
                case 1: channelValues[i] = new Color32(0, 255, 0, 0); break;
                case 2: channelValues[i] = new Color32(0, 0, 255, 0); break;
                case 3: channelValues[i] = new Color32(0, 0, 0, 255); break;
            }
        }

        // When enabled, blend up to two biome indices per pixel (with weights summing to 1) near cell
        // borders so texture transitions match the smoothly blended terrain height instead of cutting hard.
        bool useBlending = terrainGenerator.TerrainTextureBasedOnVoronoiPoints && terrainGenerator.UseBiomeBlendedTexturing;

        // Precompute the biome indices for every pixel in the biomeMap (skipped entirely when blending,
        // since the blended path below computes its own per-pixel indices/weights instead).
        int[,] precomputedBiomeIndices = useBlending ? null : new int[chunkSize, chunkSize];

        // Check if texture variations are enabled and we have a valid global offset
        bool useTextureVariations = !useBlending && terrainGenerator.EnableTextureVariations && globalOffset != Vector2.zero;

        if (useBlending)
        {
            // Blended path fills its own index/weight buffers below; nothing to precompute here.
        }
        else if (useTextureVariations)
        {
            // ENHANCED PATH: Apply texture variation logic
            int chunkSeed = Mathf.FloorToInt(globalOffset.x * 0.1f) + Mathf.FloorToInt(globalOffset.y * 0.1f) * 1000;

            Parallel.For(0, chunkSize, y =>
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    Biome biome = biomeMap[x, y];

                    // Find base biome index
                    int baseBiomeIndex = biomeIndexMap[biome.name];

                    // Apply texture variation if biome has multiple textures
                    if (biome.textureVariations != null && biome.textureVariations.Length > 0)
                    {
                        // Create a unique seed for this pixel based on chunk seed and position
                        int pixelSeed = chunkSeed + x * 31 + y * 97;
                        System.Random pixelRandom = new System.Random(pixelSeed);

                        // For future enhancement: texture variation index could be stored here
                        // Currently we just use base biome index
                        precomputedBiomeIndices[x, y] = baseBiomeIndex;
                    }
                    else
                    {
                        precomputedBiomeIndices[x, y] = baseBiomeIndex;
                    }
                }
            });
        }
        else
        {
            // ORIGINAL PATH: Simple biome index lookup without variations
            Parallel.For(0, chunkSize, y =>
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    precomputedBiomeIndices[x, y] = biomeIndexMap[biomeMap[x, y].name];
                }
            });
        }

        int[,,] blendIndices = null;
        float[,,] blendWeights = null;
        int blendSlots = 0;

        if (useBlending)
        {
            SplatBlendData blend = precomputedBlend != null && precomputedBlend.Size == chunkSize && precomputedBlend.Slots == terrainGenerator.SplatTexturesPerPixel
                ? precomputedBlend
                : ComputeBlend(terrainGenerator, worldOrigin, chunkSize, biomeIndexMap);
            blendIndices = blend.Indices;
            blendWeights = blend.Weights;
            blendSlots = blend.Slots;
        }

        // Initialize the array of splatmaps and a shared buffer for pixel data
        Texture2D[] splatMaps = new Texture2D[numSplatMaps];
        Color32[] sharedBuffer = new Color32[totalPixels];

        // Create an empty splatmap texture for each required splatmap
        for (int i = 0; i < numSplatMaps; i++)
        {
            // Create an empty splatmap texture with RGBA32 format
            splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
        }
        // Populate the shared buffer for all splatmaps in parallel
        for (int i = 0; i < numSplatMaps; i++)
        {
            // Clear the shared buffer before processing the current splatmap
            Array.Clear(sharedBuffer, 0, totalPixels); // Reset buffer

            int splatMapIndex = i;

            if (useBlending)
            {
                Parallel.For(0, chunkSize, y =>
                {
                    for (int x = 0; x < chunkSize; x++)
                    {
                        byte r = 0, g = 0, b = 0, a = 0;
                        bool touchesThisMap = false;

                        for (int slot = 0; slot < blendSlots; slot++)
                        {
                            int biomeIndex = blendIndices[x, y, slot];
                            if (biomeIndex < 0 || biomeIndex / 4 != splatMapIndex) continue;

                            touchesThisMap = true;
                            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(blendWeights[x, y, slot]) * 255f);
                            switch (biomeIndex % 4)
                            {
                                case 0: r = value; break;
                                case 1: g = value; break;
                                case 2: b = value; break;
                                case 3: a = value; break;
                            }
                        }

                        if (!touchesThisMap) continue;

                        int pixelIndex = y * chunkSize + x;
                        sharedBuffer[pixelIndex] = new Color32(r, g, b, a);
                    }
                });
            }
            else
            {
                // Parallelize row processing for better performance
                Parallel.For(0, chunkSize, y =>
                {
                    for (int x = 0; x < chunkSize; x++)
                    {
                        // Get the biome index at (x, y)
                        int biomeIndex = precomputedBiomeIndices[x, y];

                        // Check if the biome belongs to the current splatmap
                        if (biomeIndex / 4 != splatMapIndex) continue;

                        // Calculate the linear pixel index and set the corresponding color
                        int pixelIndex = y * chunkSize + x;
                        sharedBuffer[pixelIndex] = channelValues[biomeIndex];
                    }
                });
            }

            // Transfer the buffer's data to the texture and apply changes
            splatMaps[i].SetPixelData(sharedBuffer, 0);
            splatMaps[i].Apply();
        }

        return splatMaps;
    }

    /// <summary>Biome name -> index into <see cref="TerrainGenerator.BiomeDefinitions"/> (a later duplicate name wins).</summary>
    public static Dictionary<string, int> BiomeIndexMap(TerrainGenerator terrainGenerator)
    {
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;
        var biomeIndexMap = new Dictionary<string, int>(numBiomes);
        for (int i = 0; i < numBiomes; i++)
        {
            biomeIndexMap[terrainGenerator.BiomeDefinitions[i].BiomePrefab.name] = i;
        }
        return biomeIndexMap;
    }

    /// <summary>The per-pixel biome blend for a chunk (the same biome layout the terrain was built from).</summary>
    private static SplatBlendData ComputeBlend(TerrainGenerator terrainGenerator, Vector2 worldOrigin, int chunkSize, Dictionary<string, int> biomeIndexMap)
    {
        var data = new SplatBlendData(chunkSize, terrainGenerator.SplatTexturesPerPixel);
        var indices = new SplatBiomeIndex(biomeIndexMap);

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
