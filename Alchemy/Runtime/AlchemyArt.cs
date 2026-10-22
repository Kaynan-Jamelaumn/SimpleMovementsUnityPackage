using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generated icons and models for alchemy items made at runtime (projects without potion or ingredient assets): a
/// vial tinted with the liquid's colour, a leaf, a pile of powder, a crystal, a flask. Cached per shape and colour.
/// </summary>
public static class AlchemyArt
{
    public enum Shape { Vial, Leaf, Powder, Crystal, Flask }

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

    /// <summary>The icon shape of a material kind (herbs are leaves, salts and dusts powder, gems crystals).</summary>
    public static Shape ShapeOf(MaterialKind kind)
    {
        switch (kind)
        {
            case MaterialKind.Herb: return Shape.Leaf;
            case MaterialKind.Stone: case MaterialKind.Ingot: case MaterialKind.Essence: case MaterialKind.Ore: case MaterialKind.Wood: return Shape.Powder;
            case MaterialKind.Cloth: return Shape.Leaf;
            case MaterialKind.Gem: return Shape.Crystal;
            default: return Shape.Flask;
        }
    }

    private static string Key(Shape shape, Color c) => shape + ColorUtility.ToHtmlStringRGB(c);

    // ------------------------------------------------------------------ icons
    /// <summary>A 64×64 icon of <paramref name="shape"/> in <paramref name="color"/>.</summary>
    public static Sprite Icon(Shape shape, Color color)
    {
        string key = Key(shape, color);
        if (icons.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Alchemy Icon " + key,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = Pixel(shape, color, x + 0.5f, y + 0.5f);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        s.name = tex.name;
        s.hideFlags = HideFlags.DontSave;
        icons[key] = s;
        return s;
    }

    private static readonly Color Glass = new Color(0.85f, 0.92f, 1f, 0.35f);
    private static readonly Color Outline = new Color(0.08f, 0.09f, 0.12f, 0.95f);
    private static readonly Color Cork = new Color(0.55f, 0.38f, 0.22f, 1f);

    private static Color Pixel(Shape shape, Color c, float x, float y)
    {
        switch (shape)
        {
            case Shape.Vial: return Bottle(c, x, y, 15f, 25f);
            case Shape.Flask: return Bottle(c, x, y, 19f, 23f);
            case Shape.Leaf:
            {
                // An ellipse along the diagonal with a darker midrib.
                float u = (x - 32f + y - 32f) * 0.7071f, v = (x - 32f - (y - 32f)) * 0.7071f;
                float d = u * u / (26f * 26f) + v * v / (11f * 11f);
                if (d > 1.08f) return Clear;
                if (d > 0.92f) return Outline;
                Color leaf = Color.Lerp(c, c * 0.75f, Mathf.Abs(v) / 11f);
                leaf.a = 1f;
                return Mathf.Abs(v) < 1.2f && Mathf.Abs(u) < 22f ? Shade(c, 0.6f) : leaf;
            }
            case Shape.Powder:
            {
                // A heap: half-ellipse on a base line, with specks.
                float dx = (x - 32f) / 26f, dy = (y - 12f) / 22f;
                if (y < 10f) return Clear;
                float d = dx * dx + dy * dy;
                if (d > 1.06f) return Clear;
                if (d > 0.9f) return Outline;
                int hash = ((int)x * 73856093) ^ ((int)y * 19349663);
                return (hash & 31) == 0 ? Shade(c, 1.35f) : Shade(c, 0.85f + 0.25f * (1f - dy));
            }
            default:
            {
                // A crystal: a tall hexagon split into a light and a dark facet.
                float ax = Mathf.Abs(x - 32f), ay = Mathf.Abs(y - 32f);
                float edge = Mathf.Max(ax / 15f, (ax * 0.5f + ay) / 26f);
                if (edge > 1.06f) return Clear;
                if (edge > 0.92f) return Outline;
                return x < 32f ? Shade(c, 1.2f) : Shade(c, 0.8f);
            }
        }
    }

    private static Color Bottle(Color liquid, float x, float y, float radius, float centreY)
    {
        // Round body, neck, cork; liquid fills the body's lower part; a highlight on the glass.
        float bx = x - 32f, by = y - centreY;
        float body = Mathf.Sqrt(bx * bx + by * by);
        bool inNeck = Mathf.Abs(x - 32f) <= 5.5f && y >= centreY + radius - 4f && y <= centreY + radius + 12f;
        bool inCork = Mathf.Abs(x - 32f) <= 6.5f && y > centreY + radius + 12f && y <= centreY + radius + 18f;
        if (inCork) return Cork;
        if (body > radius + 1.5f && !inNeck) return Clear;
        if (!inNeck && body > radius - 0.5f || inNeck && Mathf.Abs(x - 32f) > 4.5f) return Outline;
        if (!inNeck && y < centreY + radius * 0.35f)
        {
            Color l = Shade(liquid, 0.75f + 0.35f * (1f - body / radius));
            l.a = 1f;
            return bx < -radius * 0.45f && by > -radius * 0.2f ? Color.Lerp(l, Color.white, 0.35f) : l;
        }
        return Glass;
    }

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    private static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

    // ------------------------------------------------------------------ models
    /// <summary>An inactive small model used as the prefab of runtime items (instances are made active when dropped).</summary>
    public static GameObject Template(Shape shape, Color color)
    {
        string key = Key(shape, color);
        if (templates.TryGetValue(key, out GameObject t) && t != null)
            return t;
        if (root == null)
        {
            root = new GameObject("[Alchemy Item Templates]");
            root.SetActive(false);
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(root);
            else
                root.hideFlags = HideFlags.HideAndDontSave;
        }
        PrimitiveType type = shape == Shape.Vial || shape == Shape.Flask ? PrimitiveType.Capsule : shape == Shape.Powder ? PrimitiveType.Sphere : PrimitiveType.Cube;
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "Alchemy " + key;
        go.transform.SetParent(root.transform, false);
        switch (shape)
        {
            case Shape.Vial: go.transform.localScale = new Vector3(0.09f, 0.08f, 0.09f); break;
            case Shape.Flask: go.transform.localScale = new Vector3(0.12f, 0.1f, 0.12f); break;
            case Shape.Leaf: go.transform.localScale = new Vector3(0.14f, 0.01f, 0.07f); break;
            case Shape.Powder: go.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f); break;
            default:
                go.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
                go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                break;
        }
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = MaterialFor(color);
            if (m != null) r.sharedMaterial = m;
        }
        templates[key] = go;
        return go;
    }

    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    /// <summary>A plain lit material of <paramref name="color"/> (cached).</summary>
    public static Material MaterialFor(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (materials.TryGetValue(key, out Material m) && m != null)
            return m;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;
        m = new Material(shader) { name = "Alchemy " + key, hideFlags = HideFlags.DontSave, color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        materials[key] = m;
        return m;
    }
}
