using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generated icons and models for harvested items made at runtime (materials without item assets): the material's own
/// shape - the same outlines the harvesting window draws (a claw, a fang, a femur, a heart, a liver...) - in its colour,
/// shaded and outlined; and a small tinted model to drop in the world. Cached per material.
/// </summary>
public static class HarvestArt
{
    private const int Size = 64;
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
    private static GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        icons.Clear();
        templates.Clear();
        root = null;
    }

    /// <summary>
    /// Forgets the icons and models made so far (they are made again on demand); outside Play Mode they are destroyed.
    /// For tests and tools that change the creatures between uses.
    /// </summary>
    public static void ClearCaches()
    {
        if (!Application.isPlaying)
        {
            foreach (Sprite s in icons.Values)
                if (s != null)
                {
                    Texture2D t = s.texture;
                    UnityEngine.Object.DestroyImmediate(s);
                    if (t != null) UnityEngine.Object.DestroyImmediate(t);
                }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        ResetStatics();
    }

    private static string Key(HarvestAnatomyData a, HarvestMaterial m) => (a != null ? a.id : "") + "/" + (m != null ? m.id : "") + ColorUtility.ToHtmlStringRGB(m != null ? m.color : Color.gray);

    // ------------------------------------------------------------------ shapes
    /// <summary>The outlines of a material's icon in a unit box (y up), with the colour of each.</summary>
    public static List<(Vector2[] poly, Color color)> Shapes(HarvestAnatomyData a, HarvestMaterial m)
    {
        var list = new List<(Vector2[], Color)>();
        Color c = m != null ? m.color : Color.gray;
        string shape = m != null ? m.Shape : "cut";
        switch (shape)
        {
            case "hide":
            case "membrane":
            {
                if (m != null && m.kind == HarvestMaterialKind.Feathers)
                {
                    list.Add((HarvestDiagram.P(0.5f, 0.97f, 0.66f, 0.7f, 0.62f, 0.3f, 0.5f, 0.12f, 0.4f, 0.32f, 0.36f, 0.7f), c));
                    list.Add((HarvestDiagram.P(0.49f, 0.95f, 0.51f, 0.95f, 0.52f, 0.03f, 0.48f, 0.03f), Shade(c, 0.55f)));
                    break;
                }
                if (shape == "membrane")
                {
                    list.Add((HarvestDiagram.P(0.06f, 0.82f, 0.5f, 0.96f, 0.94f, 0.82f, 0.82f, 0.42f, 0.66f, 0.56f, 0.5f, 0.3f, 0.34f, 0.56f, 0.18f, 0.42f), c));
                    break;
                }
                // A pelt laid flat: the body, the legs spread, the head.
                HarvestBodyPlan plan = a != null ? a.plan : HarvestBodyPlan.Quadruped;
                if (plan == HarvestBodyPlan.Serpent)
                {
                    list.Add((Band(0.12f), c));
                    break;
                }
                list.Add((HarvestDiagram.P(0.3f, 0.82f, 0.08f, 0.94f, 0.04f, 0.84f, 0.22f, 0.7f, 0.24f, 0.3f, 0.06f, 0.16f, 0.1f, 0.06f, 0.3f, 0.18f,
                    0.44f, 0.12f, 0.5f, 0.02f, 0.56f, 0.12f, 0.7f, 0.18f, 0.9f, 0.06f, 0.94f, 0.16f, 0.76f, 0.3f, 0.78f, 0.7f, 0.96f, 0.84f, 0.92f, 0.94f,
                    0.7f, 0.82f, 0.58f, 0.9f, 0.5f, 0.98f, 0.42f, 0.9f), c));
                break;
            }
            case "scale":
            case "plate":
                list.Add((Fit(HarvestDiagram.Piece(shape)[0], 0.1f), c));
                break;
            case "cut":
                list.Add((HarvestDiagram.Blob(new Vector2(0.5f, 0.5f), new Vector2(0.42f, 0.32f), 7), Shade(Color.Lerp(c, Color.white, 0.25f), 1f)));
                list.Add((HarvestDiagram.Blob(new Vector2(0.5f, 0.5f), new Vector2(0.36f, 0.26f), 7), c));
                list.Add((HarvestDiagram.Ellipse(0.62f, 0.56f, 0.07f, 0.07f, 12), new Color(0.93f, 0.9f, 0.8f)));
                break;
            case "fat":
                list.Add((HarvestDiagram.P(0.12f, 0.3f, 0.86f, 0.24f, 0.9f, 0.62f, 0.16f, 0.72f), c));
                break;
            case "tendon":
            case "sinew":
                list.Add((HarvestDiagram.Limb(new Vector2(0.12f, 0.2f), new Vector2(0.52f, 0.62f), new Vector2(0.88f, 0.84f), 0.05f), c));
                break;
            case "heart":
            case "lungs":
            case "liver":
            case "kidney":
            case "stomach":
            case "intestines":
            case "bladder":
            case "gall":
            case "spleen":
            case "gland":
            case "venom":
            case "organ":
            {
                string o = shape == "organ" ? "liver" : shape == "venom" ? "gland" : shape;
                Vector2 half = o == "gland" || o == "gall" || o == "bladder" || o == "kidney" ? new Vector2(0.3f, 0.34f) : new Vector2(0.4f, 0.36f);
                if (m != null && m.kind == HarvestMaterialKind.Essence)
                {
                    list.Add((HarvestDiagram.P(0.5f, 0.96f, 0.76f, 0.72f, 0.7f, 0.2f, 0.5f, 0.04f, 0.3f, 0.2f, 0.24f, 0.72f), c));
                    list.Add((HarvestDiagram.P(0.5f, 0.96f, 0.5f, 0.04f, 0.3f, 0.2f, 0.24f, 0.72f), Shade(c, 1.25f)));
                    break;
                }
                foreach (Vector2[] p in HarvestDiagram.OrganOutline(o, new Vector2(0.5f, 0.5f), half, 3))
                    list.Add((p, c));
                break;
            }
            case "paw":
                list.Add((HarvestDiagram.Limb(new Vector2(0.5f, 0.96f), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.38f), 0.12f), c));
                list.Add((HarvestDiagram.Ellipse(0.5f, 0.24f, 0.22f, 0.18f, 16), c));
                break;
            default:
                foreach (Vector2[] p in HarvestDiagram.Piece(shape))
                    list.Add((Fit(p, 0.08f), c));
                break;
        }
        return list;
    }

    private static Vector2[] Band(float width)
    {
        var l = new List<Vector2>();
        var r = new List<Vector2>();
        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f;
            Vector2 p = new Vector2(0.1f + 0.8f * t, 0.5f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f));
            l.Add(p + new Vector2(0f, width * (0.4f + 0.6f * t)));
            r.Add(p - new Vector2(0f, width * (0.4f + 0.6f * t)));
        }
        r.Reverse();
        l.AddRange(r);
        return l.ToArray();
    }

    /// <summary>A piece's outline scaled into the icon with a margin, its aspect kept.</summary>
    private static Vector2[] Fit(Vector2[] poly, float margin)
    {
        Rect b = HarvestDiagram.Bounds(new[] { poly });
        float s = (1f - margin * 2f) / Mathf.Max(0.01f, Mathf.Max(b.width, b.height));
        var o = new Vector2[poly.Length];
        Vector2 c = b.center;
        for (int i = 0; i < poly.Length; i++)
            o[i] = new Vector2(0.5f + (poly[i].x - c.x) * s, 0.5f + (poly[i].y - c.y) * s);
        return o;
    }

    // ------------------------------------------------------------------ icons
    /// <summary>A 64×64 icon of the material (its shape in its colour, shaded, outlined).</summary>
    public static Sprite Icon(HarvestAnatomyData a, HarvestMaterial m)
    {
        string key = Key(a, m);
        if (icons.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;
        List<(Vector2[] poly, Color color)> shapes = Shapes(a, m);
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "Harvest Icon " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[Size * Size];
        // 2×2 samples a pixel: smooth edges.
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color sum = Clear;
                for (int s = 0; s < 4; s++)
                {
                    var p = new Vector2((x + 0.25f + 0.5f * (s & 1)) / Size, (y + 0.25f + 0.5f * (s >> 1)) / Size);
                    sum += Sample(shapes, p);
                }
                px[y * Size + x] = sum * 0.25f;
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = tex.name;
        sprite.hideFlags = HideFlags.DontSave;
        icons[key] = sprite;
        return sprite;
    }

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    private static readonly Color Outline = new Color(0.08f, 0.07f, 0.06f, 0.95f);

    private static Color Sample(List<(Vector2[] poly, Color color)> shapes, Vector2 p)
    {
        // The topmost shape under the point; its edge is outlined, its body shaded lighter toward the top left.
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            Vector2[] poly = shapes[i].poly;
            if (!HarvestDiagram.Contains(poly, p))
                continue;
            float edge = float.MaxValue;
            for (int k = 0; k < poly.Length; k++)
                edge = Mathf.Min(edge, HarvestDiagram.SegmentDistance(p, poly[k], poly[(k + 1) % poly.Length], out _));
            if (edge < 1.6f / Size)
                return Outline;
            float light = 0.82f + 0.36f * Mathf.Clamp01((p.y - p.x) * 0.5f + 0.5f);
            return Shade(shapes[i].color, light);
        }
        // Just outside an edge: the outline too (it stays visible on light shapes).
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            Vector2[] poly = shapes[i].poly;
            for (int k = 0; k < poly.Length; k++)
                if (HarvestDiagram.SegmentDistance(p, poly[k], poly[(k + 1) % poly.Length], out _) < 1.1f / Size)
                    return Outline;
        }
        return Clear;
    }

    private static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

    // ------------------------------------------------------------------ models
    /// <summary>An inactive small tinted model, the prefab of a runtime item (instances are made active when dropped).</summary>
    public static GameObject Template(HarvestAnatomyData a, HarvestMaterial m)
    {
        string key = Key(a, m);
        if (templates.TryGetValue(key, out GameObject t) && t != null)
            return t;
        if (root == null)
        {
            root = new GameObject("[Harvest Item Templates]");
            root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(root);
            else root.hideFlags = HideFlags.HideAndDontSave;
        }
        string shape = m != null ? m.Shape : "cut";
        PrimitiveType type;
        Vector3 scale;
        switch (shape)
        {
            case "hide": case "membrane": type = PrimitiveType.Cube; scale = new Vector3(0.6f, 0.03f, 0.45f); break;
            case "longbone": case "spine": case "tendon": case "sinew": case "horn": case "tusk": case "antler":
                type = PrimitiveType.Capsule; scale = new Vector3(0.06f, 0.16f, 0.06f); break;
            case "skull": case "pelvis": type = PrimitiveType.Sphere; scale = new Vector3(0.22f, 0.16f, 0.26f); break;
            case "ribs": case "jaw": case "fat": case "cut": type = PrimitiveType.Cube; scale = new Vector3(0.22f, 0.07f, 0.16f); break;
            case "claw": case "talon": case "fang": case "tooth": case "beak": case "paw": type = PrimitiveType.Capsule; scale = new Vector3(0.035f, 0.05f, 0.035f); break;
            case "scale": case "plate": type = PrimitiveType.Cube; scale = new Vector3(0.12f, 0.015f, 0.12f); break;
            default: type = PrimitiveType.Sphere; scale = new Vector3(0.12f, 0.1f, 0.12f); break;
        }
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "Harvest " + key;
        go.transform.SetParent(root.transform, false);
        go.transform.localScale = scale;
        if (type == PrimitiveType.Capsule)
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = AlchemyArt.MaterialFor(m != null ? m.color : Color.gray);
            if (mat != null) r.sharedMaterial = mat;
        }
        templates[key] = go;
        return go;
    }
}
