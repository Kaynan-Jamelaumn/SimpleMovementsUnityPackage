using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure for a chunk's water mesh - see <see cref="MeshGenerator.GenerateWaterMesh"/>. Uses the
/// exact same vertex grid (width/depth/LOD sampling) as <see cref="MeshData"/>, so water vertices always
/// line up with the terrain mesh underneath, and one submesh per water type (ocean, lake, pond, river,
/// plus waterfalls - the steep sheets where a river drops) so each type can get its own material. Only the
/// grid vertices a water quad uses are stored (in grid order); the mesh keeps the bounds of the whole grid.
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

    // The bounds the mesh is given instead of those of its own vertices (see SetBounds).
    private Bounds bounds;
    private bool hasBounds;

    /// <summary>Room for every vertex of a (width + 1) x (depth + 1) grid.</summary>
    public WaterMeshData(int width, int depth) : this((width + 1) * (depth + 1), null)
    {
    }

    /// <summary>Room for <paramref name="vertexCount"/> vertices; <paramref name="submeshQuads"/>: how many quads each submesh will get, when known.</summary>
    public WaterMeshData(int vertexCount, int[] submeshQuads)
    {
        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        colors = new Color[vertexCount];
        flows = new Vector2[vertexCount];
        submeshTriangles = new List<int>[SubmeshCount];
        for (int i = 0; i < SubmeshCount; i++)
            submeshTriangles[i] = submeshQuads != null ? new List<int>(submeshQuads[i] * 6) : new List<int>();
    }

    /// <summary>
    /// The bounds <see cref="BuildMesh"/> gives the mesh. The water mesh used to hold the chunk's whole vertex grid,
    /// dry corners included, and its bounds were that grid's; with only the used vertices stored, the generator
    /// passes the grid's bounds here so the mesh is culled and sorted exactly as before.
    /// </summary>
    public void SetBounds(Bounds value)
    {
        bounds = value;
        hasBounds = true;
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
        if (hasBounds)
            mesh.bounds = bounds;
        else
            mesh.RecalculateBounds();
        return mesh;
    }
}
