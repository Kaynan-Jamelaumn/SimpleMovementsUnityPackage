using UnityEngine;
/// <summary>
/// Represents the data structure used for creating and updating a mesh.
/// </summary>
public class MeshData
{    
    /// <summary>
     /// The vertices of the mesh, defining its shape.
     /// </summary>
    public Vector3[] vertices;

    /// <summary>
    /// The triangle indices of the mesh, defining how vertices are connected to form faces.
    /// </summary>
    public int[] triangles;

    /// <summary>
    /// The UV coordinates for texture mapping on the mesh.
    /// </summary>
    public Vector2[] uvs;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshData"/> class with the given width and depth.
    /// </summary>
    /// <param name="width">The width of the mesh (number of vertices along the x-axis).</param>
    /// <param name="depth">The depth of the mesh (number of vertices along the z-axis).</param>
    public MeshData(int width, int depth)
    {
        // Initialize vertices, triangles, and UV arrays based on the width and depth of the mesh.

        // Calculate the total number of vertices.
        // Each grid cell requires vertices at its corners, and an additional row and column are added.
        // Formula: (width + 1) * (depth + 1)
        vertices = new Vector3[(width + 1) * (depth + 1)];

        // Calculate the total number of triangle indices.
        // Each grid cell (quad) is split into two triangles, requiring 6 indices.
        // Formula: width * depth * 6
        triangles = new int[width * depth * 6];  // 6 indices per quad (2 triangles).

        // Calculate the total number of UV coordinates, one for each vertex.
        // Formula: (width + 1) * (depth + 1)
        uvs = new Vector2[(width + 1) * (depth + 1)];


    }
    /// <summary>
    /// Updates and creates a Unity Mesh object using the current mesh data.
    /// </summary>
    /// <returns>A new <see cref="Mesh"/> object with the updated data.</returns>
    public Mesh UpdateMesh()
    {
        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Allows foSr larger meshes
        };
        // Optimize the mesh for dynamic updates.
        mesh.MarkDynamic(); // Mark the mesh as dynamic
        mesh.Clear();

        // Assign vertices, triangles, and UVs to the mesh.
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        // Recalculate normals for proper shading.
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
        int lodFactor = lod > 1 ? lod * 2 : 1;

        // Adjust the effective width and depth based on the LOD factor.
        // Ajusta a largura e a profundidade com base no LOD
        int lodWidth = (width - 1) / lodFactor + 1;
        int lodDepth = (depth - 1) / lodFactor + 1;

        // Initialize mesh data with adjusted dimensions.
        MeshData meshData = new MeshData(lodWidth, lodDepth);

        int vertexIndex = 0;
        int triangleIndex = 0;

        // Generate vertices and UV coordinates based on the height map.
        for (int y = 0; y < depth; y += lodFactor)
        {
            for (int x = 0; x < width; x += lodFactor)
            {
                // Assign vertex position and UV coordinates.
                meshData.vertices[vertexIndex] = new Vector3(x * terrainGenerator.ScaleFactor, heightMap[x,y], y * terrainGenerator.ScaleFactor);
                meshData.uvs[vertexIndex] = new Vector2((float)x / width, (float)y / depth);
                vertexIndex++;

                // Add triangles if within bounds of the mesh grid.
                // Verify if we're still in the bounds of the mesh
                if (x < width - lodFactor && y < depth - lodFactor)
                {
                    int vert = (y / lodFactor) * lodWidth + (x / lodFactor);
                    // Create two triangles (as a quad) per iteration

                    // Each quad is divided into two triangles:
                    // Triangle 1: vertices (vert, vert + lodWidth, vert + 1)
                    //First Triangle : vertexes(vert, vert + lodWidth, vert + 1)
                    meshData.triangles[triangleIndex++] = vert;
                    meshData.triangles[triangleIndex++] = vert + lodWidth;
                    meshData.triangles[triangleIndex++] = vert + 1;

                    // Triangle 2: vertices (vert + 1, vert + lodWidth, vert + lodWidth + 1)
                    //Second Triangle 2: vertexes(vert + 1, vert + lodWidth, vert + lodWidth + 1)
                    meshData.triangles[triangleIndex++] = vert + 1;
                    meshData.triangles[triangleIndex++] = vert + lodWidth;
                    meshData.triangles[triangleIndex++] = vert + lodWidth + 1;
                }
            }
        }

        return meshData;
    }
}

