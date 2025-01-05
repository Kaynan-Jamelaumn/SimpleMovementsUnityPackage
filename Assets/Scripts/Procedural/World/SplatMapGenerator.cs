using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Represents data for a splat map, including its RenderTexture and Texture2D representation.
/// </summary>
public class SplatMapData
{
    private RenderTexture splatMap;
    private Texture2D splatMap2D;
    private ComputeBuffer biomeThresholdBuffer;

    /// <summary>
    /// Gets or sets the RenderTexture representation of the splat map.
    /// </summary>
    public RenderTexture SplatMap { get => splatMap; set => splatMap = value; }

    /// <summary>
    /// Gets or sets the Texture2D representation of the splat map.
    /// </summary>
    public Texture2D SplatMap2D { get => splatMap2D; set => splatMap2D = value; }
}

/// <summary>
/// Provides methods for generating splat maps based on biome data, height maps, and other criteria.
/// </summary>
public static class SplatMapGenerator
{
    /// <summary>
    /// Generates a splat map based on biome data using a single-threaded approach.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing biome definitions and chunk size.</param>
    /// <param name="biomeMap">A 2D array of biomes representing the terrain.</param>
    /// <param name="splatMap">The Texture2D to store the generated splat map.</param>
    /// <returns>The generated splat map as a Texture2D.</returns>
    public static Texture2D GenerateSplatMapOutsideMainThread(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Texture2D splatMap)
    {
        Color[] colorMap = new Color[terrainGenerator.ChunkSize * terrainGenerator.ChunkSize];

        for (int y = 0; y < terrainGenerator.ChunkSize; y++)
        {
            for (int x = 0; x < terrainGenerator.ChunkSize; x++)
            {
                Biome biome = biomeMap[x, y];
                Color channelWeight = new Color(0, 0, 0, 0);

                for (int i = 0; i < terrainGenerator.BiomeDefinitions.Length; i++)
                {
                    if (terrainGenerator.BiomeDefinitions[i].BiomePrefab.name == biome.name)
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
    /// <summary>
    /// Generates multiple splatmaps for a terrain based on the biome distribution map.
    /// Each splatmap encodes up to four biome channels (RGBA) in a single texture.
    /// This function is highly optimized for large-scale terrain generation.
    /// </summary>
    /// <param name="terrainGenerator">
    /// The TerrainGenerator instance providing terrain parameters and biome definitions.
    /// </param>
    /// <param name="biomeMap">
    /// A 2D array representing the biome distribution for the terrain.
    /// Each element corresponds to a biome at a specific (x, y) position on the terrain.
    /// </param>
    /// <returns>
    /// An array of Texture2D splatmaps. Each texture contains up to four biome channels,
    /// represented in the RGBA components.
    /// </returns>
    /// <remarks>
    /// ### Function Logic and Optimizations:
    ///
    /// 1. **Setup and Initialization**:
    ///    - Determine the number of splatmaps needed (`numSplatMaps`) based on the number of biomes.
    ///    - Precompute biome indices for efficiency, avoiding repeated lookups during pixel processing.
    ///
    /// 2. **Biome Index Mapping**:
    ///    - A dictionary maps biome names to their corresponding indices (`biomeIndexMap`), reducing redundant calculations.
    ///    - This ensures quick access to biome indices for biome assignments.
    ///
    /// 3. **Precompute Channel Values**:
    ///    - For each biome, precompute RGBA channel values (`channelValues`) based on the biome index modulo 4.
    ///    - This eliminates repetitive branching logic during the main loop, improving performance.
    ///
    /// 4. **Shared Buffer Reuse**:
    ///    - A single shared buffer (`sharedBuffer`) is used for all splatmaps, cleared and reused in each iteration.
    ///    - This reduces memory allocations and garbage collection overhead.
    ///    // - **What is the Shared Buffer?**:
    ///   - The `sharedBuffer` is a temporary array of `Color32` used to store pixel data for a single splatmap.
    ///   - Its size matches the total number of pixels in the terrain chunk (`chunkSize * chunkSize`).
    ///   
    /// /// - **Why Reuse It?**:
    ///   - Memory Efficiency: Instead of allocating a new buffer for each splatmap, the same buffer is cleared and reused.
    ///   - Performance: Reduces memory allocation and garbage collection overhead during texture generation.
    ///
    /// - **How Is It Used?**:
    ///   - Each iteration clears the buffer (`Array.Clear`) before populating it with the pixel data for the current splatmap.
    ///   - After processing, the buffer is transferred to the texture using `SetPixelData`.
    ///
    /// 5. **Parallelized Pixel Buffer Population**:
    ///    - The pixel processing loop is parallelized using `Parallel.For`, leveraging multiple CPU cores.
    ///    - Only pixels relevant to the current splatmap are updated, skipping unnecessary ones.
    ///      - Each splatmap processes only the pixels associated with its assigned biome channels.
    ///
    /// 6. **Efficient Texture Updates**:
    ///    - Use `SetPixelData` to transfer the populated buffer to the texture in bulk.
    ///    - The `Apply` method finalizes the updates to synchronize the texture with the GPU.
    ///
    /// ### Key Concepts:
    ///
    /// - **Color32 vs. Color**:
    ///   - `Color32` is used for memory efficiency and faster texture manipulation as it operates with bytes.
    ///   - This avoids unnecessary precision overhead (`float`) of `Color`.
    ///   /// - **What is `Color32`?**:
    ///   - A lightweight structure using 8-bit components (0–255) for red, green, blue, and alpha.
    ///   - Memory-efficient: Each color requires only 4 bytes (vs. 16 bytes with `Color`).
    ///   - Example: `Color32(255, 0, 0, 0)` represents pure red with no green, blue, or alpha.
    /// - **What is `Color`?**:
    ///   - A structure using floating-point components (0.0–1.0) for higher precision.
    ///   - Example: `Color(1.0f, 0.0f, 0.0f, 0.0f)` also represents pure red.
    /// - **Why Use `Color32`?**:
    ///   - For terrain splatmaps, precision beyond 8-bit is unnecessary since RGBA values are discrete and encoded.
    ///   - `Color32` is faster and uses less memory, making it ideal for large-scale operations like splatmap generation.
    ///
    /// - **Texture Manipulation**:
    ///   - `SetPixelData`: Efficiently updates a block of pixels using pre-filled arrays.
    ///   - `Apply`: Finalizes the texture changes and updates the GPU representation.
    ///   ///   - **`SetPixel`**:
    ///     - Updates individual pixels one by one.
    ///     - Slower for large textures due to frequent CPU-GPU communication.
    ///   - **`SetPixelData`**:
    ///     - Transfers an entire array of pixel data to the texture in bulk.
    ///     - Much faster for large textures as it minimizes overhead and avoids per-pixel updates.
    ///   - **Why Use `SetPixelData`?**:
    ///     - Bulk operations are more efficient for high-resolution textures.
    ///     - The `sharedBuffer` provides all pixel data for the splatmap in one go.
    ///
    /// - **Parallel Processing**:
    ///   - The independent nature of splatmap channels allows for safe and efficient parallelization.
    ///   - This significantly reduces processing time for high-resolution terrain generation.
    ///
    /// ### Performance Notes:
    ///
    /// - The function minimizes memory usage by reusing a shared buffer.
    /// - It avoids redundant calculations by precomputing and caching data.
    /// - Parallel processing ensures scalability for large terrain sizes.
    ///
    /// ### Limitations:
    ///
    /// - This function assumes that biome definitions and indices remain constant during execution.
    /// - It is optimized for CPU-based texture generation; further optimizations may include GPU-based implementations.
    ///
    /// </remarks>


    public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
    {
        // Get the terrain's chunk size (resolution) and number of defined biomes
        int chunkSize = terrainGenerator.ChunkSize;
        int numBiomes = terrainGenerator.BiomeDefinitions.Length;

        // Calculate the number of splatmaps needed (each can store up to 4 biomes in RGBA)
        int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);
        // Calculate the total number of pixels in a single splatmap
        int totalPixels = chunkSize * chunkSize;

        // Precompute a dictionary mapping biome names to their indices for fast lookup
        var biomeIndexMap = new Dictionary<string, int>(numBiomes);
        for (int i = 0; i < numBiomes; i++)
        {
            biomeIndexMap[terrainGenerator.BiomeDefinitions[i].BiomePrefab.name] = i;
        }

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

        // Precompute the biome indices for every pixel in the biomeMap
        int[,] precomputedBiomeIndices = new int[chunkSize, chunkSize];
        Parallel.For(0, chunkSize, y =>
        {
            for (int x = 0; x < chunkSize; x++)
            {
                // Map the biome name at (x, y) to its index
                precomputedBiomeIndices[x, y] = biomeIndexMap[biomeMap[x, y].name];
            }
        });

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

            // Parallelize row processing for better performance
            Parallel.For(0, chunkSize, y =>
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    // Get the biome index at (x, y)
                    int biomeIndex = precomputedBiomeIndices[x, y];

                    // Check if the biome belongs to the current splatmap
                    if (biomeIndex / 4 != i) continue;

                    // Calculate the linear pixel index and set the corresponding color
                    int pixelIndex = y * chunkSize + x;
                    sharedBuffer[pixelIndex] = channelValues[biomeIndex];
                }
            });

            // Transfer the buffer's data to the texture and apply changes 
            splatMaps[i].SetPixelData(sharedBuffer, 0);
            splatMaps[i].Apply();
        }

        return splatMaps;
    }








    /// <summary>
    /// Generates a splat map optimized for up to 4 texture channels based on biome data.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing biome definitions and chunk size.</param>
    /// <param name="biomeMap">A 2D array of biomes representing the terrain.</param>
    /// <param name="splatMap">The Texture2D to store the generated splat map.</param>
    /// <returns>The generated splat map as a Texture2D.</returns>
    public static Texture2D GenerateSplatMapOutsideMainThread4Textures(TerrainGenerator terrainGenerator, Biome[,] biomeMap, Texture2D splatMap)
    {
        Color[] colorMap = new Color[terrainGenerator.ChunkSize * terrainGenerator.ChunkSize];

        for (int y = 0; y < terrainGenerator.ChunkSize; y++)
        {
            for (int x = 0; x < terrainGenerator.ChunkSize; x++)
            {
                Biome biome = biomeMap[x, y];
                Color channelWeight = new Color(0, 0, 0, 0);

                for (int i = 0; i < terrainGenerator.BiomeDefinitions.Length; i++)
                {
                    if (terrainGenerator.BiomeDefinitions[i].BiomePrefab.name == biome.name)
                    {
                        channelWeight[i] = 1; // Assign full weight to the corresponding channel
                        break;
                    }
                }

                colorMap[y * terrainGenerator.ChunkSize + x] = channelWeight;
            }
        }

        splatMap.SetPixels(colorMap); // Set all pixels at once
        splatMap.Apply(); // Apply all SetPixel changes
        return splatMap;
    }
    /// <summary>
    /// Generates a splat map based on height data, blending textures according to height ranges.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing biome definitions, chunk size, and height range.</param>
    /// <param name="heightMap">A 2D array of height values for the terrain.</param>
    /// <param name="splatMap">The Texture2D to store the generated splat map.</param>
    /// <returns>The generated splat map as a Texture2D.</returns>
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
                for (int i = 0; i < terrainGenerator.BiomeDefinitions.Length; i++)
                {
                    if (scaledHeight >= terrainGenerator.BiomeDefinitions[i].BiomePrefab.minHeight && scaledHeight <= terrainGenerator.BiomeDefinitions[i].BiomePrefab.maxHeight)
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




//public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
//{
//    int chunkSize = terrainGenerator.ChunkSize;
//    int numBiomes = terrainGenerator.BiomeDefinitions.Length;
//    int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);

//    Texture2D[] splatMaps = new Texture2D[numSplatMaps];
//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);

//        // Fill with transparent black initially
//        Color[] colors = new Color[chunkSize * chunkSize];
//        for (int j = 0; j < colors.Length; j++)
//        {
//            colors[j] = new Color(0, 0, 0, 0);
//        }
//        splatMaps[i].SetPixels(colors);
//    }

//    for (int y = 0; y < chunkSize; y++)
//    {
//        for (int x = 0; x < chunkSize; x++)
//        {
//            Biome biome = biomeMap[x, y];
//            int biomeIndex = Array.FindIndex(terrainGenerator.BiomeDefinitions, def => def.BiomePrefab.name == biome.name);

//            if (biomeIndex >= 0)
//            {
//                int splatMapIndex = biomeIndex / 4;
//                int channelIndex = biomeIndex % 4;

//                Color currentColor = splatMaps[splatMapIndex].GetPixel(x, y);
//                currentColor[channelIndex] = 1.0f;
//                splatMaps[splatMapIndex].SetPixel(x, y, currentColor);
//            }
//        }
//    }

//    foreach (var splatMap in splatMaps)
//    {
//        splatMap.Apply();
//    }

//    return splatMaps;
//}



//public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
//{
//    int chunkSize = terrainGenerator.ChunkSize;
//    int numBiomes = terrainGenerator.BiomeDefinitions.Length;
//    int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);

//    // Precompute biome index lookup
//    var biomeIndexMap = new Dictionary<string, int>();
//    for (int i = 0; i < numBiomes; i++)
//    {
//        biomeIndexMap[terrainGenerator.BiomeDefinitions[i].BiomePrefab.name] = i;
//    }

//    // Initialize splatmaps
//    Texture2D[] splatMaps = new Texture2D[numSplatMaps];
//    Color[] initialColors = new Color[chunkSize * chunkSize];
//    for (int j = 0; j < initialColors.Length; j++)
//    {
//        initialColors[j] = new Color(0, 0, 0, 0);
//    }

//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
//        splatMaps[i].SetPixels(initialColors);
//    }

//    // Process biome map
//    Color[][] splatMapColors = new Color[numSplatMaps][];
//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMapColors[i] = new Color[chunkSize * chunkSize];
//        Array.Copy(initialColors, splatMapColors[i], initialColors.Length);
//    }

//    for (int y = 0; y < chunkSize; y++)
//    {
//        for (int x = 0; x < chunkSize; x++)
//        {
//            Biome biome = biomeMap[x, y];
//            if (biomeIndexMap.TryGetValue(biome.name, out int biomeIndex))
//            {
//                int splatMapIndex = biomeIndex / 4;
//                int channelIndex = biomeIndex % 4;

//                int pixelIndex = y * chunkSize + x;
//                splatMapColors[splatMapIndex][pixelIndex][channelIndex] = 1.0f;
//            }
//        }
//    }

//    // Apply changes to splatmaps
//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i].SetPixels(splatMapColors[i]);
//        splatMaps[i].Apply();
//    }

//    return splatMaps;
//}


//public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
//{
//    int chunkSize = terrainGenerator.ChunkSize;
//    int numBiomes = terrainGenerator.BiomeDefinitions.Length;
//    int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);

//    // Precompute biome index lookup
//    var biomeIndexMap = new Dictionary<string, int>();
//    for (int i = 0; i < numBiomes; i++)
//    {
//        biomeIndexMap[terrainGenerator.BiomeDefinitions[i].BiomePrefab.name] = i;
//    }

//    // Initialize splatmaps
//    Texture2D[] splatMaps = new Texture2D[numSplatMaps];
//    Color[][] splatMapBuffers = new Color[numSplatMaps][];

//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
//        splatMapBuffers[i] = new Color[chunkSize * chunkSize];
//    }

//    // Process biome map and populate splatmap buffers
//    for (int y = 0; y < chunkSize; y++)
//    {
//        for (int x = 0; x < chunkSize; x++)
//        {
//            Biome biome = biomeMap[x, y];
//            if (biomeIndexMap.TryGetValue(biome.name, out int biomeIndex))
//            {
//                int splatMapIndex = biomeIndex / 4;
//                int channelIndex = biomeIndex % 4;

//                int pixelIndex = y * chunkSize + x;
//                ref Color pixelColor = ref splatMapBuffers[splatMapIndex][pixelIndex];

//                switch (channelIndex)
//                {
//                    case 0: pixelColor.r = 1.0f; break;
//                    case 1: pixelColor.g = 1.0f; break;
//                    case 2: pixelColor.b = 1.0f; break;
//                    case 3: pixelColor.a = 1.0f; break;
//                }
//            }
//        }
//    }

//    // Apply the buffers to the textures
//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i].SetPixels(splatMapBuffers[i]);
//        splatMaps[i].Apply();
//    }

//    return splatMaps;
//}


//public static Texture2D[] GenerateSplatMaps(TerrainGenerator terrainGenerator, Biome[,] biomeMap)
//{
//    int chunkSize = terrainGenerator.ChunkSize;
//    int numBiomes = terrainGenerator.BiomeDefinitions.Length;
//    int numSplatMaps = Mathf.CeilToInt(numBiomes / 4f);
//    int totalPixels = chunkSize * chunkSize;

//    // Precompute biome index lookup
//    var biomeIndexMap = new Dictionary<string, int>(numBiomes);
//    for (int i = 0; i < numBiomes; i++)
//    {
//        biomeIndexMap[terrainGenerator.BiomeDefinitions[i].BiomePrefab.name] = i;
//    }

//    // Precompute biome indices for the map
//    int[,] precomputedBiomeIndices = new int[chunkSize, chunkSize];
//    for (int y = 0; y < chunkSize; y++)
//    {
//        for (int x = 0; x < chunkSize; x++)
//        {
//            precomputedBiomeIndices[x, y] = biomeIndexMap[biomeMap[x, y].name];
//        }
//    }

//    // Initialize splatmaps and their pixel buffers
//    Texture2D[] splatMaps = new Texture2D[numSplatMaps];
//    Color32[] sharedBuffer = new Color32[totalPixels];

//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        splatMaps[i] = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
//    }

//    // Populate pixel buffers (parallelized)
//    for (int i = 0; i < numSplatMaps; i++)
//    {
//        Array.Clear(sharedBuffer, 0, totalPixels); // Reset buffer

//        Parallel.For(0, chunkSize, y =>
//        {
//            for (int x = 0; x < chunkSize; x++)
//            {
//                int biomeIndex = precomputedBiomeIndices[x, y];
//                if (biomeIndex / 4 != i) continue;

//                int channelIndex = biomeIndex % 4;
//                int pixelIndex = y * chunkSize + x;

//                switch (channelIndex)
//                {
//                    case 0: sharedBuffer[pixelIndex].r = 255; break;
//                    case 1: sharedBuffer[pixelIndex].g = 255; break;
//                    case 2: sharedBuffer[pixelIndex].b = 255; break;
//                    case 3: sharedBuffer[pixelIndex].a = 255; break;
//                }
//            }
//        });

//        splatMaps[i].SetPixelData(sharedBuffer, 0);
//        splatMaps[i].Apply();
//    }

//    return splatMaps;
//}