using System.Collections.Generic;
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
    // Dedicated splat-map UV: spans exactly [0,1] once across the whole chunk, completely
    // independent of texture density/tiling. `uvs` above is deliberately stretched past [0,1]
    // (see GetTextureScale) so the detail texture repeats several times per chunk - sampling
    // the splat map with THAT uv wraps it the same number of times, tiling the entire biome
    // layout within a single chunk instead of showing it once. This second channel is what
    // the shader now uses to sample the splat map correctly.
    public Vector2[] splatUVs;

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
        splatUVs = new Vector2[(width + 1) * (depth + 1)];

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
        mesh.uv2 = splatUVs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (enableDebugging)
            Debug.Log($"Mesh created - Bounds: {mesh.bounds}, Vertex Count: {mesh.vertexCount}, Triangle Count: {mesh.triangles.Length / 3}");

        return mesh;
    }
}

/// <summary>
/// Data structure for a chunk's water mesh - see <see cref="MeshGenerator.GenerateWaterMesh"/>. Uses the
/// exact same vertex grid (width/depth/LOD sampling) as <see cref="MeshData"/>, so water vertices always
/// line up with the terrain mesh underneath, and one submesh per water type (ocean, lake, pond, river,
/// plus waterfalls - the steep sheets where a river drops) so each type can get its own material. Vertices of dry grid points no quad uses stay in the buffer
/// unreferenced - a little memory, in exchange for trivially correct indexing.
/// </summary>
public class WaterMeshData
{
    public const int SubmeshCount = 5;

    public Vector3[] vertices;
    public Vector2[] uvs;
    public Color[] colors;
    public readonly List<int>[] submeshTriangles;

    public WaterMeshData(int width, int depth)
    {
        int count = (width + 1) * (depth + 1);
        vertices = new Vector3[count];
        uvs = new Vector2[count];
        colors = new Color[count];
        submeshTriangles = new List<int>[SubmeshCount];
        for (int i = 0; i < SubmeshCount; i++)
            submeshTriangles[i] = new List<int>();
    }

    /// <summary>Submesh (and material slot) of a water type: 0 = ocean, 1 = lake, 2 = pond, 3 = river, 4 = waterfall.</summary>
    public static int SubmeshIndex(WaterBodyType type)
    {
        return Mathf.Clamp((int)type - 1, 0, SubmeshCount - 1);
    }

    public static WaterBodyType SubmeshType(int index)
    {
        return (WaterBodyType)(index + 1);
    }

    public Mesh BuildMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Water Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.subMeshCount = SubmeshCount;
        for (int i = 0; i < SubmeshCount; i++)
            mesh.SetTriangles(submeshTriangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
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

                // Dedicated splat-map UV: plain 0-1 across the chunk, deliberately untouched by
                // uvScale/ScaleFactor/variations so it can never wrap/tile within one chunk.
                meshData.splatUVs[vertexIndex] = new Vector2(
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

    /// <summary>
    /// Generates a chunk's water mesh from its water map (see <see cref="WaterGenerator"/>), using the same
    /// grid/LOD sampling as <see cref="GenerateTerrainMesh"/> so water vertices line up with the terrain
    /// mesh underneath.
    ///
    /// A quad is drawn as soon as ANY of its corners is wet. Its dry corners sit at the lower of the
    /// nearby water body's level (<see cref="WaterMapData.ShoreLevel"/>) and the terrain height. Rims,
    /// banks and coasts are kept above the water, so normally the flat water just slides under the land
    /// and the visible shoreline is exactly where terrain crosses the water surface - no gap between water
    /// and shore. If terrain were ever lower, the water meets it instead of floating. Quads past this
    /// chunk's own extent (the neighbor draws those too) are skipped, so transparent water is never
    /// blended twice along a chunk border.
    /// </summary>
    /// <param name="terrainGenerator">Terrain generator, for ScaleFactor.</param>
    /// <param name="heightMap">The chunk's terrain height map.</param>
    /// <param name="water">The chunk's water map. Null returns null.</param>
    /// <param name="lod">Must match the LOD the chunk's terrain mesh was built with, so shorelines line up.</param>
    /// <param name="globalOffset">The chunk's world offset, for world-space UVs that tile seamlessly across chunks.</param>
    /// <returns>Water mesh data, or null if this chunk has no water at all.</returns>
    public static WaterMeshData GenerateWaterMesh(TerrainGenerator terrainGenerator, float[,] heightMap, WaterMapData water, int lod = 0, Vector2 globalOffset = default)
    {
        if (water == null)
            return null;

        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        int lodFactor = lod > 0 ? lod * 2 : 1;
        int meshWidth = (mapWidth - 1) / lodFactor;
        int meshHeight = (mapHeight - 1) / lodFactor;

        // Chunks are spaced (map size - 2) cells apart, so the last height map column/row is the next
        // chunk's first: quads reaching past it belong to the neighbor.
        int lastOwnedX = mapWidth - 2;
        int lastOwnedY = mapHeight - 2;

        WaterMeshData meshData = new WaterMeshData(meshWidth, meshHeight);
        // A quad whose wet corners differ in water height by more than this is a waterfall sheet (steeper
        // than ~50 degrees at this vertex spacing); gentler rapids stay river.
        float waterfallRise = 1.2f * lodFactor;
        float scaleFactor = terrainGenerator.ScaleFactor;
        const float uvScale = 1f / 20f;
        bool anyQuad = false;

        int vertexIndex = 0;
        for (int y = 0; y <= meshHeight; y++)
        {
            for (int x = 0; x <= meshWidth; x++)
            {
                int heightMapX = Mathf.Min(x * lodFactor, mapWidth - 1);
                int heightMapY = Mathf.Min(y * lodFactor, mapHeight - 1);
                float terrain = heightMap[heightMapX, heightMapY];

                float waterHeight;
                if (water.IsWet(heightMapX, heightMapY))
                {
                    waterHeight = water.Surface[heightMapX, heightMapY];
                }
                else
                {
                    float shore = water.ShoreLevel[heightMapX, heightMapY];
                    waterHeight = float.IsNaN(shore) ? terrain : Mathf.Min(shore, terrain);
                }

                meshData.vertices[vertexIndex] = new Vector3(heightMapX * scaleFactor, waterHeight, heightMapY * scaleFactor);
                meshData.uvs[vertexIndex] = new Vector2((globalOffset.x + heightMapX) * uvScale, (globalOffset.y + heightMapY) * uvScale);
                // For custom water shaders: r = water depth (0-1 over 10 units, 0 at the shoreline, e.g. for
                // foam/fade), g = water type (0.25 ocean, 0.5 lake, 0.75 pond, 1 river, 0 dry).
                float depth = Mathf.Clamp01((waterHeight - terrain) / 10f);
                meshData.colors[vertexIndex] = new Color(depth, (int)water.Type[heightMapX, heightMapY] / 4f, 0f, 1f);

                if (x < meshWidth && y < meshHeight)
                {
                    int nextHeightMapX = Mathf.Min((x + 1) * lodFactor, mapWidth - 1);
                    int nextHeightMapY = Mathf.Min((y + 1) * lodFactor, mapHeight - 1);

                    if (nextHeightMapX <= lastOwnedX && nextHeightMapY <= lastOwnedY)
                    {
                        WaterBodyType type = QuadWaterType(
                            water.Type[heightMapX, heightMapY],
                            water.Type[nextHeightMapX, heightMapY],
                            water.Type[heightMapX, nextHeightMapY],
                            water.Type[nextHeightMapX, nextHeightMapY]);

                        if (type != WaterBodyType.None)
                        {
                            if (IsWaterfall(water, heightMapX, heightMapY, nextHeightMapX, nextHeightMapY, waterfallRise))
                                type = WaterBodyType.Waterfall;

                            int topLeft = y * (meshWidth + 1) + x;
                            int topRight = topLeft + 1;
                            int bottomLeft = (y + 1) * (meshWidth + 1) + x;
                            int bottomRight = bottomLeft + 1;

                            // Same triangulation as the terrain mesh, so the water and terrain surfaces are
                            // compared triangle-for-triangle and cross along clean straight shorelines.
                            List<int> triangles = meshData.submeshTriangles[WaterMeshData.SubmeshIndex(type)];
                            triangles.Add(topLeft);
                            triangles.Add(bottomLeft);
                            triangles.Add(topRight);
                            triangles.Add(topRight);
                            triangles.Add(bottomLeft);
                            triangles.Add(bottomRight);
                            anyQuad = true;
                        }
                    }
                }

                vertexIndex++;
            }
        }

        return anyQuad ? meshData : null;
    }

    /// <summary>True when the quad's wet corners span more than <paramref name="rise"/> in water height.</summary>
    private static bool IsWaterfall(WaterMapData water, int x0, int y0, int x1, int y1, float rise)
    {
        float min = float.MaxValue, max = float.MinValue;
        Consider(water, x0, y0, ref min, ref max);
        Consider(water, x1, y0, ref min, ref max);
        Consider(water, x0, y1, ref min, ref max);
        Consider(water, x1, y1, ref min, ref max);
        return max - min > rise;
    }

    private static void Consider(WaterMapData water, int x, int y, ref float min, ref float max)
    {
        if (!water.IsWet(x, y))
            return;
        float surface = water.Surface[x, y];
        if (surface < min) min = surface;
        if (surface > max) max = surface;
    }

    /// <summary>
    /// Which type a quad renders as when its corners touch different water bodies: the lowest enum value
    /// wins (ocean, lake, pond, river), so e.g. a river mouth quad is drawn as the body it flows into.
    /// </summary>
    private static WaterBodyType QuadWaterType(WaterBodyType a, WaterBodyType b, WaterBodyType c, WaterBodyType d)
    {
        WaterBodyType best = WaterBodyType.None;
        best = PreferWaterType(best, a);
        best = PreferWaterType(best, b);
        best = PreferWaterType(best, c);
        best = PreferWaterType(best, d);
        return best;
    }

    private static WaterBodyType PreferWaterType(WaterBodyType current, WaterBodyType candidate)
    {
        if (candidate == WaterBodyType.None)
            return current;
        if (current == WaterBodyType.None || candidate < current)
            return candidate;
        return current;
    }
}
