using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The simple art of animal husbandry, made at runtime: item icons (feed sacks and bales, crates, care tools), dropped-item
/// models, and the placeholder body of an animal whose species has no model (a body, a head and legs from its look and
/// coat, built once per species and shape and copied).
/// </summary>
public static class AnimalArt
{
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
    private static GameObject templateRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        icons.Clear();
        templates.Clear();
        templateRoot = null;
    }

    // ---------------------------------------------------------------- icons
    public static Color FeedColour(AnimalFoodData f)
    {
        if (f == null) return new Color(0.7f, 0.6f, 0.4f);
        if (f.HasTag("forage")) return f.HasTag("dry") ? new Color(0.82f, 0.74f, 0.42f) : new Color(0.42f, 0.66f, 0.32f);
        if (f.HasTag("poisonous")) return new Color(0.42f, 0.3f, 0.5f);
        if (f.HasTag("spoiled")) return new Color(0.42f, 0.44f, 0.3f);
        if (f.HasTag("insect")) return new Color(0.62f, 0.46f, 0.28f);
        if (f.HasTag("milk")) return new Color(0.95f, 0.94f, 0.88f);
        return new Color(0.86f, 0.72f, 0.46f);
    }

    public static Sprite FeedIcon(AnimalFoodData f) => Icon("feed:" + (f?.id ?? ""), c =>
    {
        Color col = FeedColour(f);
        if (f != null && f.HasTag("forage"))
        {
            // A bale or a bundle.
            c.Rect(10, 14, 54, 46, col);
            c.Frame(10, 14, 54, 46, Shade(col, 0.7f), 2f);
            c.Line(10, 24, 54, 24, Shade(col, 0.6f), 2f);
            c.Line(10, 36, 54, 36, Shade(col, 0.6f), 2f);
        }
        else
        {
            // A sack.
            Color sack = new Color(0.78f, 0.68f, 0.5f);
            c.Polygon(new[] { new Vector2(16, 6), new Vector2(48, 6), new Vector2(54, 40), new Vector2(42, 52), new Vector2(22, 52), new Vector2(10, 40) }, sack);
            c.Disc(32, 30, 10, col);
            c.Line(22, 52, 42, 52, Shade(sack, 0.7f), 3f);
        }
    });

    public static Sprite CrateIcon(AnimalSpeciesData s, AnimalBreedData b) => Icon("crate:" + (s?.id ?? "") + "/" + (b?.id ?? ""), c =>
    {
        Color wood = new Color(0.62f, 0.46f, 0.28f);
        Color coat = b != null && b.coats.Count > 0 ? b.coats[0] : s != null ? s.look.coat : Color.white;
        c.Disc(32, 34, 14, coat);
        c.Disc(42, 42, 7, coat);
        c.Rect(8, 8, 56, 28, wood);
        c.Frame(8, 8, 56, 28, Shade(wood, 0.7f), 2f);
        for (int x = 16; x <= 48; x += 8) c.Line(x, 28, x, 54, Shade(wood, 0.8f), 2f);
        c.Line(8, 54, 56, 54, Shade(wood, 0.8f), 3f);
    });

    // A tool's colours: wood, bristles, rubber, steel.
    private static readonly Color Wood = new Color(0.62f, 0.46f, 0.28f);
    private static readonly Color Bristle = new Color(0.86f, 0.78f, 0.58f);
    private static readonly Color Rubber = new Color(0.26f, 0.25f, 0.28f);
    private static readonly Color Steel = new Color(0.72f, 0.75f, 0.8f);

    /// <summary>The main colour of a care tool (its dropped model).</summary>
    public static Color ToolColour(AnimalToolData t)
    {
        if (t == null) return Wood;
        if (t.HasTag("curry_comb")) return Rubber;
        if (t.HasTag("hoof_pick")) return Steel;
        if (t.HasTag("bedding")) return new Color(0.88f, 0.8f, 0.45f);
        if (t.HasTag("soap")) return new Color(0.92f, 0.9f, 0.78f);
        return Wood;
    }

    /// <summary>The model of a care tool lying on the ground (a brush or a comb a small block, a pick or a fork a long handle).</summary>
    public static GameObject ToolModel(AnimalToolData t)
    {
        string id = t != null ? t.id : "";
        bool longHandle = t != null && (t.HasTag("fork") || t.HasTag("shovel"));
        bool pick = t != null && t.HasTag("hoof_pick");
        if (longHandle) return Template("tool:" + id, PrimitiveType.Cylinder, ToolColour(t), new Vector3(0.05f, 0.6f, 0.05f));
        if (pick) return Template("tool:" + id, PrimitiveType.Cylinder, ToolColour(t), new Vector3(0.04f, 0.12f, 0.04f));
        return Template("tool:" + id, PrimitiveType.Cube, ToolColour(t), new Vector3(0.22f, 0.07f, 0.1f));
    }

    /// <summary>The icon of a care tool by its id (a soft brush, a curry comb, a hoof pick, a pitchfork; any other: a simple tool).</summary>
    public static Sprite ToolIcon(string toolId) => Icon("tool:" + (toolId ?? ""), c =>
    {
        switch (toolId)
        {
            case "soft_brush":
                // A wooden back with a fringe of bristles.
                c.Rect(10, 30, 54, 42, Wood);
                c.Frame(10, 30, 54, 42, Shade(Wood, 0.7f), 2f);
                for (int x = 13; x <= 51; x += 4) c.Line(x, 30, x, 16, Bristle, 2f);
                c.Line(12, 16, 52, 16, Shade(Bristle, 0.8f), 2f);
                break;
            case "curry_comb":
                // A round rubber comb with rings of teeth and a strap across its back.
                c.Disc(32, 30, 20, Rubber);
                c.Ring(32, 30, 14, 2f, Shade(Rubber, 1.6f));
                c.Ring(32, 30, 8, 2f, Shade(Rubber, 1.6f));
                c.Rect(14, 44, 50, 52, new Color(0.55f, 0.32f, 0.2f));
                c.Frame(14, 44, 50, 52, Shade(new Color(0.55f, 0.32f, 0.2f), 0.7f), 2f);
                break;
            case "hoof_pick":
                // A wooden grip and a hooked steel pick.
                c.Line(14, 12, 34, 38, Wood, 6f);
                c.Line(34, 38, 44, 52, Steel, 3f);
                c.Line(44, 52, 52, 46, Steel, 3f);
                c.Line(52, 46, 50, 40, Steel, 2f);
                break;
            case "shears":
                // Two crossed blades and their spring loop.
                c.Line(14, 54, 46, 14, Steel, 4f);
                c.Line(18, 14, 50, 54, Steel, 4f);
                c.Ring(32, 34, 5, 2f, Shade(Steel, 0.7f));
                c.Ring(14, 56, 6, 2f, Wood);
                c.Ring(50, 56, 6, 2f, Wood);
                break;
            case "milking_pail":
                // A wooden pail with its bail.
                c.Rect(16, 12, 48, 44, Wood);
                c.Frame(16, 12, 48, 44, Shade(Wood, 0.65f), 2f);
                c.Line(16, 22, 48, 22, Steel, 2f);
                c.Line(16, 36, 48, 36, Steel, 2f);
                c.Ring(32, 44, 14, 2f, Steel);
                break;
            case "pitchfork":
                // A long handle and three tines.
                c.Line(32, 4, 32, 40, Wood, 4f);
                c.Line(20, 40, 44, 40, Steel, 3f);
                c.Line(20, 40, 20, 60, Steel, 2.5f);
                c.Line(32, 40, 32, 60, Steel, 2.5f);
                c.Line(44, 40, 44, 60, Steel, 2.5f);
                break;
            default:
                // A handle and a head.
                c.Line(16, 10, 40, 40, Wood, 5f);
                c.Rect(34, 38, 54, 54, Steel);
                c.Frame(34, 38, 54, 54, Shade(Steel, 0.7f), 2f);
                break;
        }
    });

    /// <summary>The colour of a product (an egg's shell, milk, a fleece).</summary>
    public static Color ProductColour(AnimalProductSpec p)
    {
        if (p == null) return new Color(0.9f, 0.88f, 0.8f);
        if (p.tags != null && p.tags.Contains("egg")) return p.id.Contains("duck") ? new Color(0.82f, 0.9f, 0.86f) : new Color(0.93f, 0.82f, 0.66f);
        if (p.tags != null && p.tags.Contains("milk")) return new Color(0.97f, 0.97f, 0.94f);
        if (p.tags != null && p.tags.Contains("fleece")) return new Color(0.92f, 0.9f, 0.82f);
        return new Color(0.9f, 0.88f, 0.8f);
    }

    /// <summary>The icon of a product: an egg, a jug of milk, a fleece; anything else a small bundle of its colour.</summary>
    public static Sprite ProductIcon(AnimalProductSpec p) => Icon("product:" + (p?.id ?? ""), c =>
    {
        Color col = ProductColour(p);
        if (p != null && p.tags != null && p.tags.Contains("egg"))
        {
            c.Disc(32, 30, 16, col);
            c.Disc(32, 40, 12, col);
            c.Disc(27, 40, 4, Shade(col, 1.12f));
        }
        else if (p != null && p.tags != null && p.tags.Contains("milk"))
        {
            c.Rect(18, 10, 42, 46, Shade(new Color(0.55f, 0.42f, 0.3f), 1f));
            c.Rect(21, 13, 39, 40, col);
            c.Ring(46, 30, 8, 3f, new Color(0.55f, 0.42f, 0.3f));
        }
        else
        {
            c.Disc(22, 30, 12, col);
            c.Disc(40, 30, 12, col);
            c.Disc(31, 40, 13, Shade(col, 1.05f));
            c.Disc(31, 22, 11, Shade(col, 0.92f));
        }
    });

    /// <summary>The model of a product lying on the ground (an egg, a jug, a fleece).</summary>
    public static GameObject ProductModel(AnimalProductSpec p)
    {
        string id = p != null ? p.id : "";
        if (p != null && p.tags != null && p.tags.Contains("egg")) return Template("product:" + id, PrimitiveType.Sphere, ProductColour(p), new Vector3(0.07f, 0.09f, 0.07f));
        if (p != null && p.tags != null && p.tags.Contains("milk")) return Template("product:" + id, PrimitiveType.Cylinder, ProductColour(p), new Vector3(0.12f, 0.12f, 0.12f));
        return Template("product:" + id, PrimitiveType.Sphere, ProductColour(p), new Vector3(0.35f, 0.18f, 0.3f));
    }

    private static Sprite Icon(string key, System.Action<CraftPixels> draw)
    {
        if (icons.TryGetValue(key, out Sprite s) && s != null) return s;
        var c = new CraftPixels(64, 64);
        c.Clear(new Color(0f, 0f, 0f, 0f));
        draw(c);
        s = c.ToSprite("Animal " + key);
        icons[key] = s;
        return s;
    }

    // ---------------------------------------------------------------- models
    private static Transform Root
    {
        get
        {
            if (templateRoot == null)
            {
                templateRoot = new GameObject("[Animal Templates]") { hideFlags = HideFlags.DontSave };
                templateRoot.SetActive(false);
                if (Application.isPlaying) Object.DontDestroyOnLoad(templateRoot);
            }
            return templateRoot.transform;
        }
    }

    /// <summary>A simple model for an item lying on the ground (inactive template; the item system instantiates it).</summary>
    public static GameObject Template(string key, PrimitiveType shape, Color color, Vector3 scale)
    {
        if (templates.TryGetValue(key, out GameObject t) && t != null) return t;
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = "Animal " + key;
        go.transform.SetParent(Root, false);
        go.transform.localScale = scale;
        Paint(go, color);
        templates[key] = go;
        return go;
    }

    /// <summary>
    /// A new placeholder body for an adult of a species (1 unit = its real size: the body scales it): a body, a head, legs,
    /// in its coat. No colliders (nothing to trip on; the pen's trigger takes the clicks).
    /// </summary>
    public static GameObject MakeBody(AnimalSpeciesData s, Color coat, Color accent)
    {
        AnimalLookSpec look = s?.look ?? new AnimalLookSpec();
        var root = new GameObject("Body");
        float L = look.length, H = look.height;
        switch (look.shape)
        {
            case AnimalLookSpec.Shape.Bird:
                Part(root, PrimitiveType.Sphere, new Vector3(0f, H * 0.55f, 0f), new Vector3(L * 0.75f, H * 0.6f, L), coat);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, H * 0.92f, L * 0.42f), Vector3.one * H * 0.32f, coat);
                Part(root, PrimitiveType.Cube, new Vector3(0f, H * 0.9f, L * 0.62f), new Vector3(H * 0.08f, H * 0.06f, H * 0.14f), new Color(0.95f, 0.7f, 0.2f));
                Part(root, PrimitiveType.Cylinder, new Vector3(-L * 0.12f, H * 0.14f, 0f), new Vector3(0.04f, H * 0.14f, 0.04f), new Color(0.9f, 0.68f, 0.25f));
                Part(root, PrimitiveType.Cylinder, new Vector3(L * 0.12f, H * 0.14f, 0f), new Vector3(0.04f, H * 0.14f, 0.04f), new Color(0.9f, 0.68f, 0.25f));
                break;
            case AnimalLookSpec.Shape.Small:
                Part(root, PrimitiveType.Sphere, new Vector3(0f, H * 0.5f, 0f), new Vector3(L * 0.6f, H, L), coat);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, H * 0.85f, L * 0.45f), Vector3.one * H * 0.6f, coat);
                Part(root, PrimitiveType.Capsule, new Vector3(-H * 0.12f, H * 1.25f, L * 0.42f), new Vector3(H * 0.12f, H * 0.35f, H * 0.08f), coat);
                Part(root, PrimitiveType.Capsule, new Vector3(H * 0.12f, H * 1.25f, L * 0.42f), new Vector3(H * 0.12f, H * 0.35f, H * 0.08f), coat);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, H * 0.55f, -L * 0.5f), Vector3.one * H * 0.25f, accent);
                break;
            default:
            {
                float leg = H * 0.45f;
                Part(root, PrimitiveType.Capsule, new Vector3(0f, leg + H * 0.25f, 0f), new Vector3(H * 0.55f, L * 0.42f, H * 0.5f), coat, Quaternion.Euler(90f, 0f, 0f));
                Part(root, PrimitiveType.Cube, new Vector3(0f, H * 0.85f, L * 0.5f), new Vector3(H * 0.3f, H * 0.3f, L * 0.22f), coat);
                Part(root, PrimitiveType.Cube, new Vector3(0f, H * 0.78f, L * 0.62f), new Vector3(H * 0.22f, H * 0.16f, L * 0.08f), accent);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * H * 0.18f;
                    float z = (i < 2 ? 1f : -1f) * L * 0.3f;
                    Part(root, PrimitiveType.Cylinder, new Vector3(x, leg * 0.5f, z), new Vector3(H * 0.1f, leg * 0.5f, H * 0.1f), Shade(coat, 0.85f));
                }
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, H * 0.6f, -L * 0.48f), new Vector3(0.04f, H * 0.2f, 0.04f), Shade(coat, 0.8f), Quaternion.Euler(30f, 0f, 0f));
                break;
            }
        }
        return root;
    }

    /// <summary>A coloured part (no collider) added to a model: a ribbon on a body (Phase 4), the parts of a placeholder.</summary>
    public static void AddPart(GameObject root, PrimitiveType type, Vector3 at, Vector3 scale, Color color) => Part(root, type, at, scale, color);

    private static void Part(GameObject root, PrimitiveType type, Vector3 at, Vector3 scale, Color color, Quaternion? rotation = null)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        Collider col = p.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }
        p.transform.SetParent(root.transform, false);
        p.transform.localPosition = at;
        p.transform.localRotation = rotation ?? Quaternion.identity;
        p.transform.localScale = scale;
        Paint(p, color);
    }

    private static void Paint(GameObject go, Color color)
    {
        Material m = AlchemyArt.MaterialFor(color);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && m != null) r.sharedMaterial = m;
    }

    /// <summary>A darker (k &lt; 1) or lighter shade of a colour, fully opaque.</summary>
    public static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

    /// <summary>The coat of an animal: its breed's coat by its genes, else the species' look.</summary>
    public static Color CoatOf(AnimalSpeciesData s, AnimalBreedData b, int coat)
    {
        if (b != null && b.coats != null && b.coats.Count > 0) return b.coats[Mathf.Abs(coat) % b.coats.Count];
        return s != null ? s.look.coat : new Color(0.6f, 0.5f, 0.4f);
    }
}
