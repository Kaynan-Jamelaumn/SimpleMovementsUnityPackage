using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ground warning drawn with line renderers: the outline of an ability's hit area plus a "charging" outline that
/// grows from the origin as the cast progresses. Pooled; follows the terrain when Combat Settings has ground layers.
/// Create with <see cref="Show"/>, update with <see cref="SetShape"/>/<see cref="SetProgress"/>, and give back with
/// <see cref="Release"/>.
/// </summary>
public sealed class AbilityTelegraph
{
    private static readonly Stack<AbilityTelegraph> pool = new Stack<AbilityTelegraph>(16);
    private static readonly List<Vector3> outer = new List<Vector3>(80);
    private static readonly List<Vector3> inner = new List<Vector3>(80);
    private static readonly List<Vector3> scaled = new List<Vector3>(80);
    private static Transform root;

    private GameObject go;
    private LineRenderer outline;
    private LineRenderer innerOutline;
    private LineRenderer fill;
    private ResolvedShape shape;
    private Color color;
    private float progress;
    private bool active;
    private Vector3 lastOrigin;
    private Quaternion lastRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool.Clear();
        root = null;
    }

    /// <summary>Shows a telegraph for <paramref name="shape"/> (null when running without a runtime).</summary>
    public static AbilityTelegraph Show(in ResolvedShape shape, Color color)
    {
        if (!Application.isPlaying)
            return null;
        AbilityTelegraph t = null;
        while (pool.Count > 0 && (t == null || t.go == null))
            t = pool.Pop();
        if (t == null || t.go == null)
            t = new AbilityTelegraph();
        t.Activate(shape, color);
        return t;
    }

    private AbilityTelegraph()
    {
        if (root == null)
        {
            AbilityRuntime rt = AbilityRuntime.Instance;
            var r = new GameObject("[Telegraphs]");
            if (rt != null)
                r.transform.SetParent(rt.transform, false);
            root = r.transform;
        }
        go = new GameObject("Telegraph");
        go.transform.SetParent(root, false);
        outline = CreateLine("Outline", false);
        innerOutline = CreateLine("Inner", false);
        fill = CreateLine("Progress", false);
    }

    private LineRenderer CreateLine(string lineName, bool loop)
    {
        var child = new GameObject(lineName);
        child.transform.SetParent(go.transform, false);
        var lr = child.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.TransformZ;
        child.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        lr.sharedMaterial = CombatSettings.Instance.TelegraphMaterial;
        return lr;
    }

    private void Activate(in ResolvedShape s, Color c)
    {
        active = true;
        go.SetActive(true);
        color = c;
        progress = 0f;
        float w = CombatSettings.Instance.telegraphLineWidth;
        outline.widthMultiplier = w;
        innerOutline.widthMultiplier = w;
        fill.widthMultiplier = w * 0.75f;
        ApplyColors();
        shape = s;
        Rebuild();
    }

    private void ApplyColors()
    {
        outline.startColor = outline.endColor = color;
        innerOutline.startColor = innerOutline.endColor = color;
        Color faded = color;
        faded.a *= 0.55f;
        fill.startColor = fill.endColor = faded;
    }

    /// <summary>Moves/reshapes the telegraph (cheap if nothing changed).</summary>
    public void SetShape(in ResolvedShape s)
    {
        if (!active)
            return;
        bool moved = (s.origin - lastOrigin).sqrMagnitude > 0.0025f || Quaternion.Angle(s.rotation, lastRotation) > 0.5f;
        shape = s;
        if (moved)
            Rebuild();
    }

    /// <summary>0-1: the inner outline grows from the origin to the full shape.</summary>
    public void SetProgress(float t)
    {
        if (!active)
            return;
        t = Mathf.Clamp01(t);
        if (Mathf.Abs(t - progress) < 0.01f)
            return;
        progress = t;
        RebuildProgress();
    }

    public void SetColor(Color c)
    {
        color = c;
        if (active)
            ApplyColors();
    }

    /// <summary>Hides the telegraph and returns it to the pool.</summary>
    public void Release()
    {
        if (!active)
            return;
        active = false;
        if (go != null)
        {
            go.SetActive(false);
            pool.Push(this);
        }
    }

    private void Rebuild()
    {
        lastOrigin = shape.origin;
        lastRotation = shape.rotation;
        shape.GetOutline(outer, inner, 40, 0.06f);
        ConformToGround(outer);
        ConformToGround(inner);
        Assign(outline, outer);
        Assign(innerOutline, inner);
        RebuildProgress();
    }

    private void RebuildProgress()
    {
        scaled.Clear();
        if (progress <= 0.01f || outer.Count == 0)
        {
            fill.positionCount = 0;
            return;
        }
        Vector3 o = shape.origin;
        for (int i = 0; i < outer.Count; i++)
        {
            Vector3 p = outer[i];
            Vector3 flat = new Vector3(p.x - o.x, 0f, p.z - o.z) * progress;
            scaled.Add(new Vector3(o.x + flat.x, Mathf.Lerp(o.y, p.y, progress) + 0.01f, o.z + flat.z));
        }
        Assign(fill, scaled);
    }

    private static void Assign(LineRenderer lr, List<Vector3> points)
    {
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            lr.SetPosition(i, points[i]);
    }

    /// <summary>Drops outline points onto the ground so telegraphs follow slopes (only near the shape's height).</summary>
    private static void ConformToGround(List<Vector3> points)
    {
        if (points.Count == 0 || CombatSettings.Instance.groundLayers.value == 0)
            return;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            if (CombatQuery.GroundHeight(p, out float y, 2f, 4f))
                points[i] = new Vector3(p.x, y + 0.06f, p.z);
        }
    }
}
