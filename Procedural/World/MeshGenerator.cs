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

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshData"/> class with the given width and depth.
    /// </summary>
    /// <param name="width">The width of the mesh (number of vertices along the x-axis).</param>
    /// <param name="depth">The depth of the mesh (number of vertices along the z-axis).</param>
    public MeshData(int width, int depth)
    {
        this.width = width;
        this.depth = depth;
        vertices = new Vector3[(width + 1) * (depth + 1)];
        triangles = new int[width * depth * 6];
        uvs = new Vector2[(width + 1) * (depth + 1)];
    }

    public Mesh UpdateMesh()
    {
        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.MarkDynamic();
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
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
    /// <returns>A <see cref="MeshData"/> object containing the generated mesh data.</returns>
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator, float[,] heightMap, int lod = 0)
    {
        int width = terrainGenerator.ChunkSize;
        int depth = terrainGenerator.ChunkSize;

        // LOD factor determines the step size for vertices. Higher LOD skips more vertices.
        // LOD factor é calculado como um múltiplo de 2 para LODs acima de 1, e 1 caso contrário.
        int lodFactor = lod > 0 ? lod * 2 : 1;
        int verticesPerLine = (width - 1) / lodFactor + 1;

        MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);

        int vertexIndex = 0;
        int triangleIndex = 0;

        // Generate vertices and UV coordinates based on the height map.
        for (int y = 0; y < depth; y += lodFactor)
        {
            for (int x = 0; x < width; x += lodFactor)
            {
                // Ensure heightMap access is within bounds
                int clampedX = Mathf.Clamp(x, 0, width - 1);
                int clampedY = Mathf.Clamp(y, 0, depth - 1);

                meshData.vertices[vertexIndex] = new Vector3(x * terrainGenerator.ScaleFactor, heightMap[clampedX, clampedY], y * terrainGenerator.ScaleFactor);

                meshData.uvs[vertexIndex] = new Vector2(
                    (float)x / (width - 1),
                    (float)y / (depth - 1)
                );
                // Add triangles if within bounds of the mesh grid.
                // Verify if we're still in the bounds of the mesh
                if (x < width - lodFactor && y < depth - lodFactor)
                {
                    int currentVertex = vertexIndex;
                    int nextVertex = currentVertex + verticesPerLine;
                    // Create two triangles (as a quad) per iteration

                    // Each quad is divided into two triangles:
                    // Triangle 1: vertices (vert, vert + lodWidth, vert + 1)
                    //First Triangle : vertexes(vert, vert + lodWidth, vert + 1)
                    meshData.triangles[triangleIndex++] = currentVertex;
                    meshData.triangles[triangleIndex++] = nextVertex;
                    meshData.triangles[triangleIndex++] = currentVertex + 1;
                    // Triangle 2: vertices (vert + 1, vert + lodWidth, vert + lodWidth + 1)
                    //Second Triangle 2: vertexes(vert + 1, vert + lodWidth, vert + lodWidth + 1)
                    meshData.triangles[triangleIndex++] = currentVertex + 1;
                    meshData.triangles[triangleIndex++] = nextVertex;
                    meshData.triangles[triangleIndex++] = nextVertex + 1;
                }

                vertexIndex++;
            }
        }

        return meshData;
    }
}
