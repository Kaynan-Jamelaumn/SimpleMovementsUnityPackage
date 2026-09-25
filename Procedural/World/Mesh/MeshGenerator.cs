using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides utilities for generating procedural terrain meshes.
/// </summary>
public static partial class MeshGenerator
{
    /// <summary>
    /// Generates a terrain mesh from a height map using the specified level of detail (LOD).
    /// </summary>
    /// <param name="terrainGenerator">An instance of the <see cref="TerrainGenerator"/> containing terrain parameters.</param>
    /// <param name="heightMap">A 2D array representing the height values of the terrain.</param>
    /// <param name="lod">The level of detail, where higher values simplify the mesh.</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    /// <param name="globalOffset">Global offset of the chunk for UV variation calculations (only used if texture variations enabled).</param>
    /// <returns>A <see cref="MeshData"/> object containing the generated mesh data.</returns>
    /// <param name="wetness">Optional per-cell ground wetness (same size as the height map); written to the vertex colors' red channel.</param>
    /// <param name="skirtDepth">
    /// Greater than 0 (distance LOD - see <see cref="TerrainGenerator.DistanceLod"/>): hangs a skirt this deep under
    /// every edge, and places the splat map by height map cell so every level of detail maps it identically.
    /// </param>
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator, float[,] heightMap, int lod = 0, bool enableDebugging = false, Vector2 globalOffset = default, float[,] wetness = null, float skirtDepth = 0f)
    {
        // HeightMap is (ChunkSize+1) x (ChunkSize+1) = 242x242
        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        if (enableDebugging)
            Debug.Log($"GenerateTerrainMesh - HeightMap size: {mapWidth}x{mapHeight}, ChunkSize: {terrainGenerator.ChunkSize}, LOD: {lod}, ScaleFactor: {terrainGenerator.ScaleFactor}");

        // LOD factor determines the step size for vertices. Higher LOD skips more vertices.
        int lodFactor = lod > 0 ? lod * 2 : 1;

        // Use the actual heightmap dimensions for mesh generation
        int meshWidth = (mapWidth - 1) / lodFactor;
        int meshHeight = (mapHeight - 1) / lodFactor;

        if (enableDebugging)
            Debug.Log($"Mesh dimensions: {meshWidth}x{meshHeight}, LOD Factor: {lodFactor}");

        MeshData meshData = new MeshData(meshWidth, meshHeight, enableDebugging);
        bool hasWetness = wetness != null && wetness.GetLength(0) == mapWidth && wetness.GetLength(1) == mapHeight;
        if (hasWetness)
            meshData.colors = new Color[meshData.vertices.Length];

        int vertexIndex = 0;
        int triangleIndex = 0;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Calculate UV scale based on world size to maintain consistent texture density
        float uvScale = GetTextureScale(terrainGenerator);

        // Calculate chunk-specific UV variations only if texture variations are enabled
        UVVariationData uvVariation = default;
        bool useVariations = terrainGenerator.EnableTextureVariations && globalOffset != Vector2.zero;

        if (useVariations)
        {
            uvVariation = CalculateUVVariation(terrainGenerator, globalOffset);
        }

        // Generate vertices and UV coordinates based on the height map.
        for (int y = 0; y <= meshHeight; y++)
        {
            for (int x = 0; x <= meshWidth; x++)
            {
                // Map mesh coordinates to heightmap coordinates
                int heightMapX = Mathf.Min(x * lodFactor, mapWidth - 1);
                int heightMapY = Mathf.Min(y * lodFactor, mapHeight - 1);

                // Get height value, check for invalid values
                float heightValue = heightMap[heightMapX, heightMapY];
                if (float.IsNaN(heightValue) || float.IsInfinity(heightValue))
                {
                    if (enableDebugging)
                        Debug.LogWarning($"Invalid height at [{heightMapX},{heightMapY}]: {heightValue}, setting to 0");
                    heightValue = 0f;
                }

                minHeight = Mathf.Min(minHeight, heightValue);
                maxHeight = Mathf.Max(maxHeight, heightValue);

                meshData.vertices[vertexIndex] = new Vector3(
                    heightMapX * terrainGenerator.ScaleFactor,
                    heightValue,
                    heightMapY * terrainGenerator.ScaleFactor
                );

                // Calculate UV coordinates - with or without variations based on toggle
                Vector2 finalUV;

                if (useVariations)
                {
                    // ENHANCED PATH: Apply all texture variation features
                    Vector2 baseUV = new Vector2(
                        (heightMapX * terrainGenerator.ScaleFactor) * uvScale * uvVariation.scaleMultiplier,
                        (heightMapY * terrainGenerator.ScaleFactor) * uvScale * uvVariation.scaleMultiplier
                    );

                    // Apply noise-based UV offset for natural variation
                    if (terrainGenerator.EnableUVNoise)
                    {
                        float worldX = globalOffset.x + heightMapX;
                        float worldY = globalOffset.y + heightMapY;
                        Vector2 noiseOffset = CalculateUVNoiseOffset(worldX, worldY, terrainGenerator.UVNoiseScale, terrainGenerator.UVNoiseStrength);
                        baseUV += noiseOffset;
                    }

                    // Apply chunk-specific UV rotation to reduce repetition
                    if (terrainGenerator.EnableUVRotation)
                    {
                        baseUV = RotateUV(baseUV, uvVariation.rotationAngle, uvVariation.rotationCenter);
                    }

                    finalUV = baseUV;
                }
                else
                {
                    // ORIGINAL PATH: Standard UV calculation without variations
                    finalUV = new Vector2(
                        (heightMapX * terrainGenerator.ScaleFactor) * uvScale,
                        (heightMapY * terrainGenerator.ScaleFactor) * uvScale
                    );
                }

                meshData.uvs[vertexIndex] = finalUV;
                if (hasWetness)
                    meshData.colors[vertexIndex] = new Color(wetness[heightMapX, heightMapY], 0f, 0f, 1f);

                // Dedicated splat-map UV: plain 0-1 across the chunk, deliberately untouched by
                // uvScale/ScaleFactor/variations so it can never wrap/tile within one chunk.
                // With distance LOD, by height map cell (what LOD 0 always gave), so switching levels
                // never shifts the textures.
                meshData.splatUVs[vertexIndex] = skirtDepth > 0f
                    ? new Vector2(heightMapX / (float)(mapWidth - 1), heightMapY / (float)(mapHeight - 1))
                    : new Vector2(
                    (float)x / meshWidth,
                    (float)y / meshHeight
                );

                // Add triangles if within bounds of the mesh grid.
                if (x < meshWidth && y < meshHeight)
                {
                    int topLeft = y * (meshWidth + 1) + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (y + 1) * (meshWidth + 1) + x;
                    int bottomRight = bottomLeft + 1;

                    // Validate indices
                    int maxIndex = (meshWidth + 1) * (meshHeight + 1) - 1;
                    if (bottomRight > maxIndex)
                    {
                        Debug.LogError($"Triangle index out of bounds! bottomRight: {bottomRight}, maxIndex: {maxIndex}");
                        Debug.LogError($"At position x:{x}, y:{y}, meshWidth:{meshWidth}, meshHeight:{meshHeight}");
                    }
                    else
                    {
                        // First Triangle : vertexes(topLeft, bottomLeft, topRight)
                        meshData.triangles[triangleIndex++] = topLeft;
                        meshData.triangles[triangleIndex++] = bottomLeft;
                        meshData.triangles[triangleIndex++] = topRight;

                        // Second Triangle: vertexes(topRight, bottomLeft, bottomRight)
                        meshData.triangles[triangleIndex++] = topRight;
                        meshData.triangles[triangleIndex++] = bottomLeft;
                        meshData.triangles[triangleIndex++] = bottomRight;
                    }
                }

                vertexIndex++;
            }
        }

        if (skirtDepth > 0f)
            meshData.AddSkirt(skirtDepth);

        if (enableDebugging)
        {
            Debug.Log($"Mesh generation complete - Vertices: {vertexIndex}, Triangles: {triangleIndex / 3}, Height range: [{minHeight}, {maxHeight}], UV Scale: {uvScale}");
            if (useVariations)
            {
                Debug.Log($"UV Variation - Rotation: {uvVariation.rotationAngle}, Scale: {uvVariation.scaleMultiplier}");
            }
            Debug.Log($"First vertex: {meshData.vertices[0]}, Last vertex: {meshData.vertices[vertexIndex - 1]}");
        }

        return meshData;
    }

    /// <summary>
    /// How deep a chunk's edge skirts must hang so no crack shows next to a neighbor at any other level of
    /// detail: twice the furthest the terrain near the chunk's edges strays from its coarsest possible mesh
    /// surface (the widest gap two neighbors' edges can have), plus <see cref="TerrainGenerator.LodSkirtDepth"/>.
    /// 0 when distance LOD is off (no skirts).
    /// </summary>
    public static float SkirtDepth(TerrainGenerator terrainGenerator, float[,] heightMap)
    {
        if (!terrainGenerator.DistanceLod)
            return 0f;

        int coarsest = Mathf.Max(terrainGenerator.LevelOfDetail, terrainGenerator.LodMaxLevel);
        int factor = coarsest > 0 ? coarsest * 2 : 1;
        int size = heightMap.GetLength(0);
        int span = size - 2; // cells a coarse mesh covers (0..span)
        float deviation = 0f;
        if (factor > 1 && span >= factor)
        {
            for (int band = 0; band < 3; band++)
            {
                int near = band, far = span - band;
                for (int t = 0; t <= span; t++)
                {
                    deviation = Mathf.Max(deviation, Mathf.Abs(heightMap[near, t] - CoarseSurface(heightMap, near, t, factor)));
                    deviation = Mathf.Max(deviation, Mathf.Abs(heightMap[far, t] - CoarseSurface(heightMap, far, t, factor)));
                    deviation = Mathf.Max(deviation, Mathf.Abs(heightMap[t, near] - CoarseSurface(heightMap, t, near, factor)));
                    deviation = Mathf.Max(deviation, Mathf.Abs(heightMap[t, far] - CoarseSurface(heightMap, t, far, factor)));
                }
            }
        }

        return terrainGenerator.LodSkirtDepth + 2f * deviation;
    }

    /// <summary>
    /// Height of a mesh with vertices every <paramref name="factor"/> cells at a height map cell, triangulated
    /// the way <see cref="GenerateTerrainMesh"/> does (each quad split along its top-right to bottom-left diagonal).
    /// </summary>
    private static float CoarseSurface(float[,] heightMap, int x, int y, int factor)
    {
        int last = heightMap.GetLength(0) - 2;
        int x0 = Mathf.Min((x / factor) * factor, last - factor), y0 = Mathf.Min((y / factor) * factor, last - factor);
        float u = (x - x0) / (float)factor, v = (y - y0) / (float)factor;
        float h00 = heightMap[x0, y0], h10 = heightMap[x0 + factor, y0];
        float h01 = heightMap[x0, y0 + factor], h11 = heightMap[x0 + factor, y0 + factor];
        if (u + v <= 1f)
            return h00 + u * (h10 - h00) + v * (h01 - h00);
        return h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
    }

    /// <summary>
    /// Calculates UV variation parameters for a chunk to reduce texture repetition.
    /// Only called when texture variations are enabled.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing settings.</param>
    /// <param name="globalOffset">Global offset of the chunk.</param>
    /// <returns>UV variation data for the chunk.</returns>
    private static UVVariationData CalculateUVVariation(TerrainGenerator terrainGenerator, Vector2 globalOffset)
    {
        UVVariationData variation = new UVVariationData();

        // Use global offset to create consistent but varied values per chunk
        int chunkSeed = Mathf.FloorToInt(globalOffset.x * 0.1f) + Mathf.FloorToInt(globalOffset.y * 0.1f) * 1000;
        System.Random chunkRandom = new System.Random(chunkSeed);

        // Calculate random rotation angle for this chunk
        if (terrainGenerator.EnableUVRotation)
        {
            variation.rotationAngle = (float)(chunkRandom.NextDouble() * 360.0);
            variation.rotationCenter = new Vector2(0.5f, 0.5f); // Center of UV space
        }

        // Calculate random scale multiplier for this chunk
        if (terrainGenerator.EnableTextureScaleVariation)
        {
            float scaleRange = terrainGenerator.TextureScaleVariationRange;
            variation.scaleMultiplier = 1.0f + ((float)chunkRandom.NextDouble() - 0.5f) * 2.0f * scaleRange;
            variation.scaleMultiplier = Mathf.Clamp(variation.scaleMultiplier, 0.1f, 3.0f);
        }
        else
        {
            variation.scaleMultiplier = 1.0f;
        }

        return variation;
    }

    /// <summary>
    /// Calculates noise-based UV offset for natural texture variation.
    /// </summary>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="noiseScale">Scale of the noise pattern.</param>
    /// <param name="noiseStrength">Strength of the offset.</param>
    /// <returns>UV offset vector.</returns>
    private static Vector2 CalculateUVNoiseOffset(float worldX, float worldY, float noiseScale, float noiseStrength)
    {
        float offsetX = (Mathf.PerlinNoise(worldX * noiseScale, worldY * noiseScale) - 0.5f) * noiseStrength;
        float offsetY = (Mathf.PerlinNoise(worldX * noiseScale + 100f, worldY * noiseScale + 100f) - 0.5f) * noiseStrength;
        return new Vector2(offsetX, offsetY);
    }

    /// <summary>
    /// Rotates UV coordinates around a center point.
    /// </summary>
    /// <param name="uv">Original UV coordinates.</param>
    /// <param name="angleDegrees">Rotation angle in degrees.</param>
    /// <param name="center">Center point for rotation.</param>
    /// <returns>Rotated UV coordinates.</returns>
    private static Vector2 RotateUV(Vector2 uv, float angleDegrees, Vector2 center)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRadians);
        float sin = Mathf.Sin(angleRadians);

        // Translate to origin
        Vector2 translated = uv - center;

        // Rotate
        Vector2 rotated = new Vector2(
            translated.x * cos - translated.y * sin,
            translated.x * sin + translated.y * cos
        );

        // Translate back
        return rotated + center;
    }

    /// <summary>
    /// Calculates the appropriate texture scale based on terrain size to maintain consistent texture density.
    /// Smaller terrains get higher UV scale (more texture repetition), larger terrains get lower UV scale.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing size information.</param>
    /// <returns>UV scale factor for texture coordinates.</returns>
    private static float GetTextureScale(TerrainGenerator terrainGenerator)
    {
        // Base texture scale - adjust this value to control overall texture density
        // Higher values = more texture repetition (smaller texture appearance)
        // Lower values = less texture repetition (larger texture appearance)
        float baseTextureScale = 0.01f;

        // Scale factor based on terrain size to maintain consistent texture density
        // Larger terrains need smaller UV scale to prevent over-repetition
        float sizeScale = (float)TerrainGenerator.MaxChunkSize / (float)terrainGenerator.ChunkSize;

        return baseTextureScale * sizeScale;
    }

    /// <summary>
    /// Data structure for UV variation parameters per chunk.
    /// </summary>
    private struct UVVariationData
    {
        public float rotationAngle;
        public Vector2 rotationCenter;
        public float scaleMultiplier;

        public UVVariationData(float rotationAngle = 0f, Vector2 rotationCenter = default, float scaleMultiplier = 1f)
        {
            this.rotationAngle = rotationAngle;
            this.rotationCenter = rotationCenter;
            this.scaleMultiplier = scaleMultiplier;
        }
    }
}
