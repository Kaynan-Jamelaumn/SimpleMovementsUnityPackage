using System.Collections.Generic;
using UnityEngine;

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
    // Second UV channel: water flow direction x speed (world X/Z) - see WaterMapData.FlowX.
    public Vector2[] flows;
    public readonly List<int>[] submeshTriangles;

    public WaterMeshData(int width, int depth)
    {
        int count = (width + 1) * (depth + 1);
        vertices = new Vector3[count];
        uvs = new Vector2[count];
        colors = new Color[count];
        flows = new Vector2[count];
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
        mesh.uv2 = flows;
        mesh.subMeshCount = SubmeshCount;
        for (int i = 0; i < SubmeshCount; i++)
            mesh.SetTriangles(submeshTriangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
