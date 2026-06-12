using UnityEngine;

/// <summary>
/// The measured size of a placeable prefab, in its root's local space: the box around its renderers, the box
/// around its colliders and the box around its base (the part resting on the ground). Measured once on the
/// main thread (see <see cref="PrefabShapeCache"/>) and then read by object placement on worker threads,
/// which is why it is plain data.
/// </summary>
public sealed class PrefabShape
{
    /// <summary>Box around the renderers (root local space, before the root's own scale).</summary>
    public Vector3 RendererMin, RendererMax;
    /// <summary>Box around the colliders (root local space, before the root's own scale).</summary>
    public Vector3 ColliderMin, ColliderMax;
    /// <summary>
    /// Box around the base: the mesh vertices in the bottom tenth of the object's height (a tree's trunk rather
    /// than its canopy). Only measured when every mesh has Read/Write enabled (see <see cref="HasBase"/>).
    /// </summary>
    public Vector3 BaseMin, BaseMax;
    public bool HasRenderers, HasColliders, HasBase;
    /// <summary>The prefab root's own scale, applied (times the placement scale) when spawning.</summary>
    public Vector3 RootScale = Vector3.one;

    /// <summary>A shape with no measurements: the pivot is treated as a point on the ground.</summary>
    public static PrefabShape Unknown()
    {
        return new PrefabShape();
    }

    /// <summary>
    /// The footprint box for a footprint source, falling back when that kind of box is missing (Automatic: the
    /// base, then the colliders, then the renderers). False when there is no box at all.
    /// </summary>
    public bool TryGetBox(FootprintSource source, out Vector3 min, out Vector3 max)
    {
        switch (source)
        {
            case FootprintSource.Automatic:
                if (HasBase)
                {
                    min = BaseMin;
                    max = BaseMax;
                    return true;
                }
                if (HasColliders)
                {
                    min = ColliderMin;
                    max = ColliderMax;
                    return true;
                }
                break;
            case FootprintSource.Colliders:
                if (HasColliders)
                {
                    min = ColliderMin;
                    max = ColliderMax;
                    return true;
                }
                break;
        }
        return TryGetSizeBox(out min, out max);
    }

    /// <summary>The box around the whole object: its renderers, or its colliders when it has no renderers.</summary>
    public bool TryGetSizeBox(out Vector3 min, out Vector3 max)
    {
        if (HasRenderers)
        {
            min = RendererMin;
            max = RendererMax;
            return true;
        }
        if (HasColliders)
        {
            min = ColliderMin;
            max = ColliderMax;
            return true;
        }
        min = max = Vector3.zero;
        return false;
    }
}
