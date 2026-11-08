using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The look of agriculture, made from code: low-poly meshes for plants, fruit, pests and stakes; a palette of
/// instancing-enabled materials (colours rounded to 6 steps per channel, so thousands of plants share a few dozen draw
/// calls); the soil overlay material; item icons drawn on a small canvas; simple models for items on the ground.
/// Placeholders by design: give species your own models and items your own icons when you have them.
/// </summary>
public static class PlantArt
{
    private static Mesh cylinder, sphere, cone, box, disc, blade;
    private static readonly Dictionary<int, Material> palette = new Dictionary<int, Material>();
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
    private static GameObject templateRoot;
    private static Shader litShader;
    private static bool shaderSearched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        palette.Clear();
        icons.Clear();
        templates.Clear();
        templateRoot = null;
        shaderSearched = false;
    }

    // ---------------------------------------------------------------- materials
    public static Shader Lit
    {
        get
        {
            if (!shaderSearched)
            {
                shaderSearched = true;
                litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                if (litShader == null) litShader = Shader.Find("Sprites/Default");
            }
            return litShader;
        }
    }

    /// <summary>Palette key of a colour (6 steps per channel).</summary>
    public static int Key(Color c) => Mathf.Clamp(Mathf.RoundToInt(c.r * 5f), 0, 5) * 36 + Mathf.Clamp(Mathf.RoundToInt(c.g * 5f), 0, 5) * 6 + Mathf.Clamp(Mathf.RoundToInt(c.b * 5f), 0, 5);

    /// <summary>The shared, instancing-enabled material of a palette key.</summary>
    public static Material Palette(int key)
    {
        if (palette.TryGetValue(key, out Material m) && m != null)
            return m;
        Shader shader = Lit;
        if (shader == null) return null;
        var c = new Color(key / 36 / 5f, key / 6 % 6 / 5f, key % 6 / 5f);
        m = new Material(shader) { name = $"Plant {key}", hideFlags = HideFlags.DontSave, color = c, enableInstancing = true };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        palette[key] = m;
        return m;
    }

    public static Material Palette(Color c) => Palette(Key(c));

    /// <summary>A new material for a field's soil (its texture is the field's cells).</summary>
    public static Material SoilMaterial(Texture texture)
    {
        Shader shader = Lit;
        if (shader == null) return null;
        var m = new Material(shader) { name = "Soil Field", hideFlags = HideFlags.DontSave, color = Color.white };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        m.mainTexture = texture;
        return m;
    }

    // ---------------------------------------------------------------- meshes (base at y = 0, height 1, width 1)
    public static Mesh Cylinder => cylinder != null ? cylinder : cylinder = MakeCylinder(7, 0.5f, 0.5f, "Plant Stem");
    public static Mesh Cone => cone != null ? cone : cone = MakeCylinder(7, 0.5f, 0.02f, "Plant Cone");
    public static Mesh Sphere => sphere != null ? sphere : sphere = MakeSphere(8, 6);
    public static Mesh Box => box != null ? box : box = MakeBox();
    public static Mesh Disc => disc != null ? disc : disc = MakeCylinder(10, 0.5f, 0.5f, "Plant Disc");
    /// <summary>A tall, thin, closed leaf blade (grain, grass, root-crop tufts).</summary>
    public static Mesh Blade => blade != null ? blade : blade = MakeCylinder(4, 0.5f, 0.05f, "Plant Blade");

    private static Mesh MakeCylinder(int sides, float bottom, float top, string name)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * bottom);
            v.Add(dir * top + Vector3.up);
            Vector3 normal = (dir + Vector3.up * (bottom - top)).normalized;
            n.Add(normal);
            n.Add(normal);
        }
        for (int i = 0; i < sides; i++)
        {
            int a = i * 2;
            t.Add(a); t.Add(a + 1); t.Add(a + 2);
            t.Add(a + 1); t.Add(a + 3); t.Add(a + 2);
        }
        int capTop = v.Count;
        v.Add(Vector3.up);
        n.Add(Vector3.up);
        int capBottom = v.Count;
        v.Add(Vector3.zero);
        n.Add(Vector3.down);
        int ring = v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * top + Vector3.up);
            n.Add(Vector3.up);
            v.Add(dir * bottom);
            n.Add(Vector3.down);
        }
        for (int i = 0; i < sides; i++)
        {
            int a = ring + i * 2;
            t.Add(capTop); t.Add(a + 2); t.Add(a);
            t.Add(capBottom); t.Add(a + 1); t.Add(a + 3);
        }
        var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    private static Mesh MakeSphere(int lon, int lat)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int y = 0; y <= lat; y++)
        {
            float b = Mathf.PI * y / lat;
            for (int x = 0; x <= lon; x++)
            {
                float a = Mathf.PI * 2f * x / lon;
                v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), -Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * 0.5f + new Vector3(0f, 0.5f, 0f));
            }
        }
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int a = y * (lon + 1) + x, b = a + lon + 1;
                t.Add(a); t.Add(b); t.Add(a + 1);
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
        var m = new Mesh { name = "Plant Sphere", hideFlags = HideFlags.DontSave };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh MakeBox()
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 d in dirs)
        {
            Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(d, u);
            int start = v.Count;
            Vector3 c = d * 0.5f + new Vector3(0f, 0.5f, 0f);
            v.Add(c - u * 0.5f - w * 0.5f);
            v.Add(c - u * 0.5f + w * 0.5f);
            v.Add(c + u * 0.5f + w * 0.5f);
            v.Add(c + u * 0.5f - w * 0.5f);
            for (int i = 0; i < 4; i++) n.Add(d);
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }
        var m = new Mesh { name = "Plant Box", hideFlags = HideFlags.DontSave };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    // ---------------------------------------------------------------- icons
    public static Sprite SeedIcon(PlantSpeciesData d) => Icon("seed:" + (d?.id ?? ""), c =>
    {
        Color pouch = new Color(0.72f, 0.6f, 0.42f);
        c.Polygon(new[] { new Vector2(16, 6), new Vector2(48, 6), new Vector2(54, 40), new Vector2(42, 50), new Vector2(22, 50), new Vector2(10, 40) }, pouch);
        c.Rect(22, 48, 42, 54, new Color(0.55f, 0.45f, 0.3f));
        Color seed = d != null ? Color.Lerp(d.Look.flower, new Color(0.35f, 0.25f, 0.15f), 0.5f) : new Color(0.4f, 0.3f, 0.2f);
        c.Disc(26, 24, 4, seed);
        c.Disc(36, 20, 4, seed);
        c.Disc(32, 31, 4, seed);
        if (d != null) c.Disc(32, 41, 5, d.Look.leaf);
    });

    public static Sprite ProduceIcon(PlantSpeciesData d) => Icon("produce:" + (d?.id ?? ""), c =>
    {
        if (d == null) { c.Disc(32, 30, 18, Color.gray); return; }
        ProduceSpec p = d.Produce;
        Color col = p.colorRipe;
        switch (p.part)
        {
            case ProducePart.Leaf:
                c.Polygon(new[] { new Vector2(32, 6), new Vector2(50, 28), new Vector2(40, 52), new Vector2(32, 58), new Vector2(24, 52), new Vector2(14, 28) }, d.Look.leaf);
                c.Line(32, 8, 32, 54, Color.Lerp(d.Look.leaf, Color.white, 0.3f), 2f);
                break;
            case ProducePart.Root:
                c.Polygon(new[] { new Vector2(22, 46), new Vector2(42, 46), new Vector2(36, 10), new Vector2(32, 4), new Vector2(28, 10) }, col);
                c.Line(28, 48, 22, 60, d.Look.leaf, 3f);
                c.Line(32, 48, 32, 61, d.Look.leaf, 3f);
                c.Line(36, 48, 42, 60, d.Look.leaf, 3f);
                break;
            case ProducePart.Flower:
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    c.Disc(32 + Mathf.Cos(a) * 11f, 32 + Mathf.Sin(a) * 11f, 9, col);
                }
                c.Disc(32, 32, 7, d.Look.flower == col ? new Color(1f, 0.8f, 0.2f) : d.Look.flower);
                break;
            case ProducePart.Grain:
                c.Line(32, 4, 32, 58, new Color(0.7f, 0.6f, 0.3f), 2f);
                for (int i = 0; i < 6; i++)
                {
                    c.Disc(27, 30 + i * 4.5f, 3.5f, col);
                    c.Disc(37, 30 + i * 4.5f, 3.5f, col);
                }
                break;
            default:
                c.Disc(32, 28, 18, col);
                c.Disc(26, 34, 5, Color.Lerp(col, Color.white, 0.35f));
                c.Line(32, 44, 34, 54, d.Look.stem, 3f);
                c.Polygon(new[] { new Vector2(34, 50), new Vector2(46, 56), new Vector2(38, 46) }, d.Look.leaf);
                break;
        }
        if (d.Look.glow.a > 0.05f) c.Ring(32, 32, 28, 2f, new Color(d.Look.glow.r, d.Look.glow.g, d.Look.glow.b, 0.6f));
    });

    public static Sprite ToolIcon(FarmToolKind k) => Icon("tool:" + k, c =>
    {
        Color wood = new Color(0.55f, 0.4f, 0.25f), iron = new Color(0.62f, 0.64f, 0.68f);
        switch (k)
        {
            case FarmToolKind.Hoe:
                c.Line(14, 6, 46, 50, wood, 3f);
                c.Polygon(new[] { new Vector2(40, 50), new Vector2(58, 52), new Vector2(56, 42), new Vector2(44, 44) }, iron);
                break;
            case FarmToolKind.Shovel:
                c.Line(32, 22, 32, 60, wood, 3f);
                c.Polygon(new[] { new Vector2(22, 24), new Vector2(42, 24), new Vector2(40, 8), new Vector2(32, 2), new Vector2(24, 8) }, iron);
                break;
            case FarmToolKind.Trowel:
                c.Line(32, 34, 32, 58, wood, 4f);
                c.Polygon(new[] { new Vector2(24, 34), new Vector2(40, 34), new Vector2(32, 6) }, iron);
                break;
            case FarmToolKind.Dibber:
                c.Polygon(new[] { new Vector2(28, 58), new Vector2(36, 58), new Vector2(34, 14), new Vector2(32, 6), new Vector2(30, 14) }, wood);
                break;
            case FarmToolKind.PruningShears:
                c.Line(20, 8, 40, 40, iron, 3f);
                c.Line(44, 8, 24, 40, iron, 3f);
                c.Line(40, 40, 50, 58, new Color(0.7f, 0.2f, 0.15f), 4f);
                c.Line(24, 40, 14, 58, new Color(0.7f, 0.2f, 0.15f), 4f);
                break;
            case FarmToolKind.Sickle:
                c.Ring(32, 34, 20, 4f, iron);
                c.Line(32, 4, 32, 18, wood, 4f);
                break;
            case FarmToolKind.PollinationBrush:
                c.Line(32, 4, 32, 40, wood, 3f);
                c.Disc(32, 48, 8, new Color(0.95f, 0.85f, 0.5f));
                break;
            case FarmToolKind.Tweezers:
                c.Line(28, 6, 31, 58, iron, 2f);
                c.Line(36, 6, 33, 58, iron, 2f);
                break;
            default:
                c.Line(24, 4, 24, 60, wood, 4f);
                c.Line(34, 4, 34, 60, wood, 4f);
                c.Line(44, 4, 44, 60, wood, 4f);
                c.Line(18, 40, 50, 40, new Color(0.85f, 0.8f, 0.6f), 2f);
                break;
        }
    });

    public static Sprite InstrumentIcon(InstrumentKind k) => Icon("instr:" + k, c =>
    {
        Color brass = new Color(0.78f, 0.62f, 0.3f), glass = new Color(0.65f, 0.85f, 0.95f, 0.9f);
        switch (k)
        {
            case InstrumentKind.BotanicalHandbook:
                c.Rect(14, 8, 50, 56, new Color(0.35f, 0.5f, 0.3f));
                c.Rect(18, 12, 46, 52, new Color(0.9f, 0.86f, 0.75f));
                c.Disc(32, 34, 8, new Color(0.3f, 0.6f, 0.25f));
                break;
            case InstrumentKind.SoilAnalysisKit:
                c.Rect(8, 10, 56, 40, new Color(0.45f, 0.32f, 0.2f));
                c.Rect(14, 24, 22, 50, glass); c.Rect(28, 24, 36, 50, new Color(0.9f, 0.4f, 0.4f, 0.9f)); c.Rect(42, 24, 50, 50, new Color(0.4f, 0.8f, 0.4f, 0.9f));
                break;
            case InstrumentKind.AdvancedAnalyzer:
                c.Rect(10, 8, 54, 34, brass);
                c.Disc(32, 44, 14, brass);
                c.Disc(32, 44, 9, new Color(0.7f, 0.5f, 0.95f));
                break;
            case InstrumentKind.DepthGauge:
                c.Rect(28, 4, 36, 60, new Color(0.65f, 0.5f, 0.3f));
                for (int i = 0; i < 6; i++) c.Rect(36, 8 + i * 9, 42, 10 + i * 9, new Color(0.2f, 0.15f, 0.1f));
                break;
            case InstrumentKind.Magnifier:
                c.Ring(28, 36, 16, 4f, brass);
                c.Disc(28, 36, 12, glass);
                c.Line(40, 24, 54, 6, new Color(0.4f, 0.28f, 0.18f), 5f);
                break;
            default:
                c.Line(32, 4, 32, 34, brass, 3f);
                c.Disc(32, 42, 15, brass);
                c.Disc(32, 42, 11, new Color(0.95f, 0.93f, 0.85f));
                float a = ((int)k * 0.9f) % 3f - 1.5f;
                c.Line(32, 42, 32 + Mathf.Sin(a) * 9f, 42 + Mathf.Cos(a) * 9f, new Color(0.7f, 0.15f, 0.1f), 2f);
                break;
        }
    });

    public static Sprite AmendmentIcon(SoilAmendmentData a) => Icon("amend:" + (a?.id ?? ""), c =>
    {
        Color col = a != null ? a.color : new Color(0.4f, 0.3f, 0.2f);
        if (a != null && (a.liquid || a.kind == AmendmentKind.Treatment))
        {
            c.Polygon(new[] { new Vector2(18, 6), new Vector2(46, 6), new Vector2(48, 40), new Vector2(38, 48), new Vector2(38, 58), new Vector2(26, 58), new Vector2(26, 48), new Vector2(16, 40) }, new Color(0.7f, 0.82f, 0.85f, 0.9f));
            c.Polygon(new[] { new Vector2(19, 8), new Vector2(45, 8), new Vector2(46, 30), new Vector2(18, 30) }, col);
            return;
        }
        if (a != null && a.kind == AmendmentKind.Beneficial)
        {
            c.Rect(10, 10, 54, 46, new Color(0.6f, 0.45f, 0.3f));
            c.Disc(24, 30, 7, col); c.Disc(40, 26, 7, col); c.Disc(32, 38, 6, col);
            return;
        }
        c.Polygon(new[] { new Vector2(12, 6), new Vector2(52, 6), new Vector2(56, 44), new Vector2(46, 54), new Vector2(18, 54), new Vector2(8, 44) }, new Color(0.62f, 0.52f, 0.36f));
        c.Polygon(new[] { new Vector2(16, 40), new Vector2(48, 40), new Vector2(44, 54), new Vector2(20, 54) }, col);
    });

    public static Sprite ContainerIcon(ContainerPreset p) => Icon("water:" + (p?.id ?? ""), c =>
    {
        Color metal = new Color(0.5f, 0.6f, 0.55f);
        if (p != null && p.capacity >= 8f)
        {
            c.Polygon(new[] { new Vector2(14, 6), new Vector2(50, 6), new Vector2(56, 46), new Vector2(8, 46) }, new Color(0.55f, 0.42f, 0.28f));
            c.Ring(32, 46, 22, 2f, metal);
            return;
        }
        if (p != null && p.capacity < 2f)
        {
            c.Disc(32, 24, 16, new Color(0.65f, 0.85f, 0.95f, 0.9f));
            c.Rect(28, 38, 36, 56, new Color(0.65f, 0.85f, 0.95f, 0.9f));
            return;
        }
        c.Rect(14, 8, 44, 40, metal);
        c.Line(44, 30, 58, 46, metal, 4f);
        c.Disc(58, 46, 4, metal);
        c.Ring(24, 44, 8, 3f, metal);
    });

    private static Sprite Icon(string key, System.Action<CraftPixels> draw)
    {
        if (icons.TryGetValue(key, out Sprite s) && s != null)
            return s;
        var c = new CraftPixels(64, 64);
        c.Clear(new Color(0f, 0f, 0f, 0f));
        draw(c);
        s = c.ToSprite("Agri " + key);
        icons[key] = s;
        return s;
    }

    // ---------------------------------------------------------------- dropped-item models
    /// <summary>A simple model for an item lying on the ground (inactive template; the item system instantiates it).</summary>
    public static GameObject Template(string key, PrimitiveType shape, Color color, Vector3 scale)
    {
        if (templates.TryGetValue(key, out GameObject t) && t != null)
            return t;
        if (templateRoot == null)
        {
            templateRoot = new GameObject("[Agriculture Item Templates]") { hideFlags = HideFlags.DontSave };
            templateRoot.SetActive(false);
            if (Application.isPlaying) Object.DontDestroyOnLoad(templateRoot);
        }
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = "Agri " + key;
        go.transform.SetParent(templateRoot.transform, false);
        go.transform.localScale = scale;
        Material m = AlchemyArt.MaterialFor(color);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && m != null) r.sharedMaterial = m;
        templates[key] = go;
        return go;
    }
}
