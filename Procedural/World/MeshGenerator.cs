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
    /// <returns>A <see cref="MeshData"/> object containing the generated mesh data.</returns>
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator, float[,] heightMap, int lod = 0, bool enableDebugging = false)
    {
        // HeightMap is (ChunkSize+1) x (ChunkSize+1) = 242x242
        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        if (enableDebugging)
            Debug.Log($"GenerateTerrainMesh - HeightMap size: {mapWidth}x{mapHeight}, ChunkSize: {terrainGenerator.ChunkSize}, LOD: {lod}, ScaleFactor: {terrainGenerator.ScaleFactor}");

        // LOD factor determines the step size for vertices. Higher LOD skips more vertices.
        // LOD factor é calculado como um múltiplo de 2 para LODs acima de 1, e 1 caso contrário.
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

                meshData.uvs[vertexIndex] = new Vector2(
                    (float)x / meshWidth,
                    (float)y / meshHeight
                );

                // Add triangles if within bounds of the mesh grid.
                // Verify if we're still in the bounds of the mesh
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
            Debug.Log($"Mesh generation complete - Vertices: {vertexIndex}, Triangles: {triangleIndex / 3}, Height range: [{minHeight}, {maxHeight}]");
            Debug.Log($"First vertex: {meshData.vertices[0]}, Last vertex: {meshData.vertices[vertexIndex - 1]}");
        }

        return meshData;
    }
}