using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Measures placeable prefabs (see <see cref="PrefabShape"/>) once each, on the main thread - reading
/// renderers, meshes and colliders is only allowed there. Works on prefab assets that were never
/// instantiated: it uses each part's mesh or collider box and its transform relative to the root, not
/// Renderer.bounds (which is empty for prefab assets).
/// </summary>
public static class PrefabShapeCache
{
    /// <summary>The base is the part of the meshes within this share of the object's height from its bottom.</summary>
    private const float BaseSlice = 0.1f;

    private static readonly Dictionary<GameObject, PrefabShape> Shapes = new Dictionary<GameObject, PrefabShape>();
    private static readonly List<Vector3> Vertices = new List<Vector3>();

    public static void Clear()
    {
        Shapes.Clear();
    }

    /// <summary>The measured shape of a prefab (main thread only). <see cref="PrefabShape.Unknown"/> for null.</summary>
    public static PrefabShape Get(GameObject prefab)
    {
        if (prefab == null)
            return PrefabShape.Unknown();

        if (Shapes.TryGetValue(prefab, out PrefabShape cached))
            return cached;

        PrefabShape shape = Measure(prefab);
        Shapes[prefab] = shape;
        return shape;
    }

    private static PrefabShape Measure(GameObject prefab)
    {
        var shape = new PrefabShape { RootScale = prefab.transform.localScale };
        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;

        bool hasRenderer = false;
        Vector3 rMin = Vector3.zero, rMax = Vector3.zero;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null || !IsActive(filter.transform, prefab.transform))
                continue;
            Encapsulate(ref hasRenderer, ref rMin, ref rMax, toRoot * filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
        }
        foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh == null || !IsActive(skinned.transform, prefab.transform))
                continue;
            Encapsulate(ref hasRenderer, ref rMin, ref rMax, toRoot * skinned.transform.localToWorldMatrix, skinned.sharedMesh.bounds);
        }
        shape.HasRenderers = hasRenderer;
        shape.RendererMin = rMin;
        shape.RendererMax = rMax;
        if (hasRenderer)
            MeasureBase(prefab, toRoot, shape);

        bool hasCollider = false;
        Vector3 cMin = Vector3.zero, cMax = Vector3.zero;
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger || !IsActive(collider.transform, prefab.transform))
                continue;
            Matrix4x4 matrix = toRoot * collider.transform.localToWorldMatrix;
            if (collider is BoxCollider)
            {
                var box = (BoxCollider)collider;
                Encapsulate(ref hasCollider, ref cMin, ref cMax, matrix, new Bounds(box.center, box.size));
            }
            else if (collider is SphereCollider)
            {
                var sphere = (SphereCollider)collider;
                Encapsulate(ref hasCollider, ref cMin, ref cMax, matrix, new Bounds(sphere.center, Vector3.one * (2f * sphere.radius)));
            }
            else if (collider is CapsuleCollider)
            {
                var capsule = (CapsuleCollider)collider;
                float diameter = 2f * capsule.radius, length = Mathf.Max(capsule.height, diameter);
                int axis = Mathf.Clamp(capsule.direction, 0, 2);
                var size = new Vector3(axis == 0 ? length : diameter, axis == 1 ? length : diameter, axis == 2 ? length : diameter);
                Encapsulate(ref hasCollider, ref cMin, ref cMax, matrix, new Bounds(capsule.center, size));
            }
            else if (collider is MeshCollider && ((MeshCollider)collider).sharedMesh != null)
            {
                Encapsulate(ref hasCollider, ref cMin, ref cMax, matrix, ((MeshCollider)collider).sharedMesh.bounds);
            }
        }
        shape.HasColliders = hasCollider;
        shape.ColliderMin = cMin;
        shape.ColliderMax = cMax;
        return shape;
    }

    /// <summary>
    /// The box around the mesh vertices near the bottom of the object (root space). Skipped - leaving the other
    /// boxes to stand in for it - when a mesh can't be read (Read/Write disabled in its import settings).
    /// </summary>
    private static void MeasureBase(GameObject prefab, Matrix4x4 toRoot, PrefabShape shape)
    {
        var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null && IsActive(filter.transform, prefab.transform))
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(filter.sharedMesh, toRoot * filter.transform.localToWorldMatrix));
        }
        foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh != null && IsActive(skinned.transform, prefab.transform))
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(skinned.sharedMesh, toRoot * skinned.transform.localToWorldMatrix));
        }
        foreach (KeyValuePair<Mesh, Matrix4x4> part in parts)
        {
            if (!part.Key.isReadable)
                return;
        }

        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (KeyValuePair<Mesh, Matrix4x4> part in parts)
        {
            part.Key.GetVertices(Vertices);
            foreach (Vector3 vertex in Vertices)
            {
                float y = part.Value.MultiplyPoint3x4(vertex).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        }
        if (float.IsInfinity(bottom))
            return;

        float sliceTop = bottom + Mathf.Max(0.001f, (top - bottom) * BaseSlice);
        bool has = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        foreach (KeyValuePair<Mesh, Matrix4x4> part in parts)
        {
            part.Key.GetVertices(Vertices);
            foreach (Vector3 vertex in Vertices)
            {
                Vector3 p = part.Value.MultiplyPoint3x4(vertex);
                if (p.y > sliceTop)
                    continue;
                if (!has)
                {
                    min = max = p;
                    has = true;
                }
                else
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
        }
        Vertices.Clear();
        if (!has)
            return;
        shape.HasBase = true;
        shape.BaseMin = new Vector3(min.x, bottom, min.z);
        shape.BaseMax = new Vector3(max.x, sliceTop, max.z);
    }

    /// <summary>
    /// Whether a part of the prefab is switched on (it and every parent up to the root): switched-off parts don't
    /// show when the object is created, so they don't count toward its size. (Prefab assets aren't in a scene, so
    /// activeInHierarchy can't tell.)
    /// </summary>
    private static bool IsActive(Transform part, Transform root)
    {
        for (Transform t = part; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf && t != root)
                return false;
            if (t == root)
                break;
        }
        return true;
    }

    /// <summary>Grows a box (root space) by the 8 corners of a local box transformed by <paramref name="matrix"/>.</summary>
    private static void Encapsulate(ref bool has, ref Vector3 min, ref Vector3 max, Matrix4x4 matrix, Bounds local)
    {
        Vector3 c = local.center, e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                c.x + ((i & 1) != 0 ? e.x : -e.x),
                c.y + ((i & 2) != 0 ? e.y : -e.y),
                c.z + ((i & 4) != 0 ? e.z : -e.z));
            Vector3 p = matrix.MultiplyPoint3x4(corner);
            if (!has)
            {
                min = max = p;
                has = true;
            }
            else
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
    }
}
