using UnityEngine;

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public MeshData(int width, int depth)
    {
        vertices = new Vector3[(width + 1) * (depth + 1)];
        triangles = new int[width * depth * 6];
        uvs = new Vector2[(width + 1) * (depth + 1)];


    }

    public Mesh UpdateMesh()
    {
        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Allows foSr larger meshes
        };
        mesh.MarkDynamic(); // Mark the mesh as dynamic
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }

}


public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(TerrainGenerator terrainGenerator, float[,] heightMap, int lod = 0)
    {
        int width = terrainGenerator.GridWidthSize;
        int depth = terrainGenerator.GridDepthSize;

        // LOD factor é calculado como um múltiplo de 2 para LODs acima de 1, e 1 caso contrário.
        int lodFactor = lod > 1 ? lod * 2 : 1;

        // Ajusta a largura e a profundidade com base no LOD
        int lodWidth = (width - 1) / lodFactor + 1;
        int lodDepth = (depth - 1) / lodFactor + 1;

        MeshData meshData = new MeshData(lodWidth, lodDepth);

        int vertexIndex = 0;
        int triangleIndex = 0;

        // Create the vertices and UVs based on the height map
        for (int y = 0; y < depth; y += lodFactor)
        {
            for (int x = 0; x < width; x += lodFactor)
            {
                meshData.vertices[vertexIndex] = new Vector3(x * terrainGenerator.ScaleFactor, heightMap[x,y], y * terrainGenerator.ScaleFactor);
                meshData.uvs[vertexIndex] = new Vector2((float)x / width, (float)y / depth);
                vertexIndex++;

                // Verify if we're still in the bounds of the mesh
                if (x < width - lodFactor && y < depth - lodFactor)
                {
                    int vert = (y / lodFactor) * lodWidth + (x / lodFactor);
                    // Create two triangles (as a quad) per iteration


                    //First Triangle : vertexes(vert, vert + lodWidth, vert + 1)
                    meshData.triangles[triangleIndex++] = vert;
                    meshData.triangles[triangleIndex++] = vert + lodWidth;
                    meshData.triangles[triangleIndex++] = vert + 1;
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

