using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Provides methods for generating splat maps based on biome data, height maps, and other criteria.
/// </summary>
public static partial class SplatMapGenerator
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
    /// <param name="globalOffset">
    /// Global offset for the chunk. Only used for texture variations when enabled.
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
