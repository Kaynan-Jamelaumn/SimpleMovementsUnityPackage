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
    // Optional per-vertex color: red = ground wetness near water (see WaterMapData.Wetness). Null = no colors.
    public Color[] colors;
    // Skirt vertices (see AddSkirt) come after the (width+1) x (depth+1) grid; each copies the normal of the
    // grid vertex listed here, so the skirt is lit like the ground above it. Null = no skirt.
    public int[] skirtSources;

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

    /// <summary>
    /// Hangs a vertical "skirt" of <paramref name="skirtDepth"/> world units under all four edges of the grid, facing
    /// outward. Where a neighboring chunk uses a different level of detail, the two edges don't line up exactly;
    /// the skirt fills the gap so no crack shows. Appended after the grid, so grid vertex indices are unchanged.
    /// </summary>
    public void AddSkirt(float skirtDepth)
    {
        int gridVertices = (width + 1) * (depth + 1);
        int edgeVertices = 2 * (width + 1) + 2 * (depth + 1);
        int edgeQuads = 2 * width + 2 * depth;

        var skirt = new SkirtBuilder
        {
            Depth = skirtDepth,
            GridVertices = gridVertices,
            RowLength = width + 1,
            Vertices = new Vector3[gridVertices + edgeVertices * 2],
            Sources = new int[edgeVertices * 2],
            Triangles = new int[triangles.Length + edgeQuads * 6],
            Vertex = gridVertices,
            Triangle = triangles.Length,
        };
        skirt.Uvs = new Vector2[skirt.Vertices.Length];
        skirt.SplatUvs = new Vector2[skirt.Vertices.Length];
        skirt.Colors = colors != null ? new Color[skirt.Vertices.Length] : null;
        System.Array.Copy(vertices, skirt.Vertices, gridVertices);
        System.Array.Copy(uvs, skirt.Uvs, gridVertices);
        System.Array.Copy(splatUVs, skirt.SplatUvs, gridVertices);
        if (skirt.Colors != null)
            System.Array.Copy(colors, skirt.Colors, gridVertices);
        System.Array.Copy(triangles, skirt.Triangles, triangles.Length);

        // Each edge is walked so that its skirt faces away from the chunk (clockwise seen from outside).
        skirt.AddEdge(0, 0, 0, 1, depth + 1);          // west,  toward +z
        skirt.AddEdge(width, depth, 0, -1, depth + 1); // east,  toward -z
        skirt.AddEdge(width, 0, -1, 0, width + 1);     // south, toward -x
        skirt.AddEdge(0, depth, 1, 0, width + 1);      // north, toward +x

        vertices = skirt.Vertices;
        uvs = skirt.Uvs;
        splatUVs = skirt.SplatUvs;
        colors = skirt.Colors;
        triangles = skirt.Triangles;
        skirtSources = skirt.Sources;
    }

    private sealed class SkirtBuilder
    {
        public float Depth;
        public int GridVertices, RowLength, Vertex, Triangle;
        public Vector3[] Vertices;
        public Vector2[] Uvs, SplatUvs;
        public Color[] Colors;
        public int[] Triangles, Sources;

        /// <summary>A top and a bottom vertex under each of <paramref name="count"/> grid edge vertices, joined by quads.</summary>
        public void AddEdge(int startX, int startY, int stepX, int stepY, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int source = (startY + stepY * i) * RowLength + startX + stepX * i;
                int top = Vertex++, bottom = Vertex++;
                Vertices[top] = Vertices[source];
                Vertices[bottom] = Vertices[source] - new Vector3(0f, Depth, 0f);
                Uvs[top] = Uvs[bottom] = Uvs[source];
                SplatUvs[top] = SplatUvs[bottom] = SplatUvs[source];
                if (Colors != null)
                    Colors[top] = Colors[bottom] = Colors[source];
                Sources[top - GridVertices] = Sources[bottom - GridVertices] = source;

                if (i == 0)
                    continue;
                int previousTop = top - 2, previousBottom = bottom - 2;
                Triangles[Triangle++] = previousTop;
                Triangles[Triangle++] = previousBottom;
                Triangles[Triangle++] = top;
                Triangles[Triangle++] = top;
                Triangles[Triangle++] = previousBottom;
                Triangles[Triangle++] = bottom;
            }
        }
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
        if (colors != null && colors.Length == vertices.Length)
            mesh.colors = colors;
        mesh.RecalculateNormals();
        if (skirtSources != null)
        {
            Vector3[] normals = mesh.normals;
            int gridVertices = vertices.Length - skirtSources.Length;
            for (int i = 0; i < skirtSources.Length; i++)
                normals[gridVertices + i] = normals[skirtSources[i]];
            mesh.normals = normals;
        }
        mesh.RecalculateBounds();

        if (enableDebugging)
            Debug.Log($"Mesh created - Bounds: {mesh.bounds}, Vertex Count: {mesh.vertexCount}, Triangle Count: {mesh.triangles.Length / 3}");

        return mesh;
    }
}
