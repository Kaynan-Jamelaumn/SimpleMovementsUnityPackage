using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A filled outline in a UI rect (a body part of the Health tab's figure): drawn as a polygon, and only the inside of
/// the outline receives the pointer, so neighbouring parts never steal each other's clicks.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class BodyShapeGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [Tooltip("The outline in the rect's own 0-1 space (x to the right, y up), in order around it.")]
    [SerializeField] private Vector2[] points = new Vector2[0];

    /// <summary>The outline (0-1 of the rect, y up).</summary>
    public Vector2[] Points => points;

    /// <summary>Sets the outline (0-1 of the rect, y up, in order around it).</summary>
    public void SetShape(Vector2[] normalized)
    {
        points = normalized ?? new Vector2[0];
        SetVerticesDirty();
    }

    /// <summary>A plain rectangle (parts the figure has no shape for).</summary>
    public void SetRectangle() => SetShape(new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });

    /// <summary>
    /// Places this graphic on a figure box at the outline <paramref name="shape"/> (the <see cref="BodyPartLayout"/>
    /// space: 0-1 across, 0-1 down from the top): its rect becomes the outline's bounds.
    /// </summary>
    public void PlaceOnFigure(Vector2[] shape)
    {
        Rect b = BodyPartLayout.Bounds(shape);
        RectTransform rt = rectTransform;
        rt.anchorMin = new Vector2(b.xMin, 1f - b.yMax);
        rt.anchorMax = new Vector2(b.xMax, 1f - b.yMin);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var local = new Vector2[shape.Length];
        for (int i = 0; i < shape.Length; i++)
            local[i] = new Vector2(b.width > 0f ? (shape[i].x - b.xMin) / b.width : 0.5f, b.height > 0f ? 1f - (shape[i].y - b.yMin) / b.height : 0.5f);
        SetShape(local);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3)
            return;
        Rect r = GetPixelAdjustedRect();
        Vector2 centre = Vector2.zero;
        foreach (Vector2 p in points)
            centre += p;
        centre /= points.Length;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = new Vector3(r.x + centre.x * r.width, r.y + centre.y * r.height);
        vh.AddVert(v);
        for (int i = 0; i < points.Length; i++)
        {
            v.position = new Vector3(r.x + points[i].x * r.width, r.y + points[i].y * r.height);
            vh.AddVert(v);
        }
        // A fan from the centre (the outlines are star-shaped around it).
        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Length);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (points == null || points.Length < 3)
            return true;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
            return false;
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f)
            return false;
        return BodyPartLayout.Contains(points, new Vector2((local.x - r.x) / r.width, (local.y - r.y) / r.height));
    }
}
