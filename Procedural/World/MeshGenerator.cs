using UnityEngine;
/// <summary>
/// Represents the data structure used for creating and updating a mesh.
/// </summary>
public class MeshData
{
    public int width;
    public int depth;
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    private bool enableDebugging;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshData"/> class with the given width and depth.
    /// </summary>
    /// <param name="width">The width of the mesh (number of vertices along the x-axis).</param>
    /// <param name="depth">The depth of the mesh (number of vertices along the z-axis).</param>
    /// <param name="enableDebugging">Flag to enable or disable debug messages.</param>
    public MeshData(int width, int depth, bool enableDebugging = false)
    {
        this.width = width;
        this.depth = depth;
        this.enableDebugging = enableDebugging;
        vertices = new Vector3[(width + 1) * (depth + 1)];
        triangles = new int[width * depth * 6];
        uvs = new Vector2[(width + 1) * (depth + 1)];

        if (enableDebugging)
            Debug.Log($"MeshData created - Width: {width}, Depth: {depth}, Vertices array: {vertices.Length}, Triangles array: {triangles.Length}");
    }

    public Mesh UpdateMesh()
    {
        if (enableDebugging)
            Debug.Log($"UpdateMesh called - Vertices: {vertices.Length}, Triangles: {triangles.Length}");

        // Validate vertices
        int invalidVertices = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (float.IsNaN(vertices[i].x) || float.IsNaN(vertices[i].y) || float.IsNaN(vertices[i].z) ||
                float.IsInfinity(vertices[i].x) || float.IsInfinity(vertices[i].y) || float.IsInfinity(vertices[i].z))
            {
                Debug.LogError($"Invalid vertex at index {i}: {vertices[i]}");
                vertices[i] = Vector3.zero;
                invalidVertices++;
            }
        }

        if (invalidVertices > 0)
        {
            Debug.LogError($"Found {invalidVertices} invalid vertices!");
        }

        // Validate triangles
        int invalidTriangles = 0;
        for (int i = 0; i < triangles.Length; i++)
        {
            if (triangles[i] < 0 || triangles[i] >= vertices.Length)
            {
                Debug.LogError($"Invalid triangle index at {i}: {triangles[i]} (max should be {vertices.Length - 1})");
                triangles[i] = 0;
                invalidTriangles++;
            }
        }

        if (invalidTriangles > 0)
        {
            Debug.LogError($"Found {invalidTriangles} invalid triangle indices!");
        }

        Mesh mesh = new Mesh();
        mesh.name = "Terrain Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (enableDebugging)
            Debug.Log($"Mesh created - Bounds: {mesh.bounds}, Vertex Count: {mesh.vertexCount}, Triangle Count: {mesh.triangles.Length / 3}");

        return mesh;
    }
}


/// <summary>
/// Provides utilities for generating procedural terrain meshes.
/// </summary>
public static class MeshGenerator
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
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator, float[,] heightMap, int lod = 0, bool enableDebugging = false, Vector2 globalOffset = default)
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