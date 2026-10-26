using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Models of what is made at a crafting station, built from what it really is: a bar or a blade from its segments
/// (length, width, thickness, bevels, bend), a hide or a cut strap from its cells, a haft from its blank, a lump of ore,
/// a handful of rivets - and a finished weapon from its parts in place (pommel, grip, guard, blade; a head on a haft).
/// <para>They are the world prefab of the runtime items that have none, so a piece dropped on the ground, and a crafted
/// weapon whose template has no model of its own, look like what was forged - on every machine, since each builds the
/// model from the same definition. Built once per item and kept under an inactive holder (instances are made active
/// where they are used).</para>
/// </summary>
public static class CraftModels
{
    private static GameObject root;
    private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        root = null;
        cache.Clear();
        materials.Clear();
    }

    private static Transform Root
    {
        get
        {
            if (root == null)
            {
                root = new GameObject("[Crafting Models]");
                root.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.DontDestroyOnLoad(root);
                else
                    root.hideFlags = HideFlags.HideAndDontSave;
            }
            return root.transform;
        }
    }

    // ------------------------------------------------------------------ what each item looks like
    /// <summary>The model of a worked piece (with an unfinished assembly's parts in place), centred on its middle.</summary>
    public static GameObject ForPiece(CraftRules rules, string key, CraftPiece piece, IReadOnlyList<CraftPiece> parts)
    {
        if (rules == null || piece == null)
            return null;
        if (cache.TryGetValue(key, out GameObject cached) && cached != null)
            return cached;
        GameObject go = New(piece.kind == CraftPieceKind.Assembly ? "Assembly" : piece.kind.ToString());
        if (piece.kind == CraftPieceKind.Assembly)
            Assemble(rules, rules.Design(piece.assembly.design), parts, go.transform);
        else
            Part(rules, piece, go.transform, Vector3.zero, Quaternion.identity, true);
        return Keep(key, go);
    }

    /// <summary>
    /// The model of a finished weapon, tool or piece of armour from its record's parts: weapons with the grip at the
    /// origin and the blade or head along +Y, armour as its plates and straps side by side.
    /// </summary>
    public static GameObject ForCrafted(CraftRules rules, CraftedItemRecord r)
    {
        if (rules == null || r == null)
            return null;
        string key = CraftedTooltip.IdPrefix + r.id;
        if (cache.TryGetValue(key, out GameObject cached) && cached != null)
            return cached;
        var parts = new List<CraftPiece>();
        foreach (string j in r.componentJson)
        {
            if (string.IsNullOrEmpty(j)) continue;
            CraftPiece p = JsonUtility.FromJson<CraftPiece>(j);
            if (p != null) parts.Add(p);
        }
        if (parts.Count == 0)
            return null;
        GameObject go = New(string.IsNullOrEmpty(r.name) ? "Crafted" : r.name);
        Assemble(rules, rules.Design(r.designId), parts, go.transform);
        return Keep(key, go);
    }

    /// <summary>The model of a stock item (a bar, a lump, a rolled hide, a blank, a bag of fuel...).</summary>
    public static GameObject ForStock(CraftStockItemSO item, CraftRules rules)
    {
        if (item == null)
            return null;
        string key = "stock:" + item.StockKind + ":" + (string.IsNullOrEmpty(item.CraftId) ? item.Name : item.CraftId);
        if (cache.TryGetValue(key, out GameObject cached) && cached != null)
            return cached;
        GameObject go = New(item.Name);
        Color c = Average(item.Icon);
        switch (item.StockKind)
        {
            case CraftStockKind.Metal:
                Box(go.transform, Vector3.zero, new Vector3(item.Size.y, item.Size.x, item.Size.z) * 0.001f, c, true, 0.4f);
                break;
            case CraftStockKind.Ore:
                Lump(go.transform, Vector3.zero, 0.07f, c);
                break;
            case CraftStockKind.Hide:
                Roll(go.transform, 0.09f, 0.5f, c);
                break;
            case CraftStockKind.Wood:
                Box(go.transform, Vector3.zero, new Vector3(0.04f, 0.45f, 0.04f), c, false, 0.2f);
                break;
            case CraftStockKind.Fasteners:
                for (int i = 0; i < 5; i++)
                    Box(go.transform, new Vector3((i - 2) * 0.012f, 0, (i % 2) * 0.01f), new Vector3(0.006f, 0.02f, 0.006f), c, true, 0.5f);
                break;
            default:
                Box(go.transform, Vector3.zero, new Vector3(0.18f, 0.22f, 0.12f), new Color(0.55f, 0.45f, 0.32f), false, 0.1f); // a sack
                break;
        }
        return Keep(key, go);
    }

    /// <summary>Forgets a model (its item is gone).</summary>
    public static void Forget(string key)
    {
        if (key != null && cache.TryGetValue(key, out GameObject go))
        {
            cache.Remove(key);
            if (go != null) Destroy(go);
        }
    }

    // ------------------------------------------------------------------ assembling
    private static void Assemble(CraftRules rules, CraftDesign d, IReadOnlyList<CraftPiece> parts, Transform into)
    {
        if (parts == null || parts.Count == 0)
            return;
        CraftComponentSpec Spec(CraftPiece p) => d?.Component(p.intentRole);
        CraftComponentFunction Fn(CraftPiece p) => Spec(p)?.function ?? CraftComponentFunction.Edge;
        bool weapon = d == null || d.IsWeaponLike;
        if (!weapon)
        {
            // Armour and shields: plates, backings and straps side by side, leather behind metal.
            float x = 0;
            foreach (CraftPiece p in parts)
            {
                Vector3 size = SizeOf(rules, p);
                Part(rules, p, into, new Vector3(x + size.x * 0.5f, 0, Fn(p) == CraftComponentFunction.Protection ? 0f : 0.004f), Quaternion.identity, true);
                x += size.x + 0.01f;
            }
            foreach (Transform t in into)
                t.localPosition -= new Vector3(x * 0.5f, 0, 0);
            return;
        }
        CraftPiece grip = null, main = null, guard = null, pommel = null;
        foreach (CraftPiece p in parts)
        {
            switch (Fn(p))
            {
                case CraftComponentFunction.Grip: if (grip == null || Length(p) > Length(grip)) grip = p; break;
                case CraftComponentFunction.Edge:
                case CraftComponentFunction.Striking: if (main == null || p.Mass(rules) > main.Mass(rules)) main = p; break;
                case CraftComponentFunction.Guard: if (guard == null) guard = p; break;
                case CraftComponentFunction.Counterweight: if (pommel == null) pommel = p; break;
            }
        }
        float gripLength = grip != null ? Length(grip) : 0.1f;
        bool hafted = grip != null && grip.kind == CraftPieceKind.Wood && gripLength > 0.3f;
        if (hafted)
        {
            // An axe, a hammer, a mace, a spear: the haft up from the hand, the head at its top.
            Part(rules, grip, into, Vector3.zero, Quaternion.identity, false);
            if (main != null)
            {
                bool spear = d != null && d.weaponCategory == WeaponCategory.Spear;
                if (spear)
                    Part(rules, main, into, new Vector3(0, gripLength, 0), Quaternion.identity, false);
                else
                {
                    float w = SizeOf(rules, main).x;
                    Part(rules, main, into, new Vector3(-Length(main) * 0.3f, gripLength - w * 0.5f, 0), Quaternion.Euler(0, 0, -90), false);
                }
            }
        }
        else
        {
            // A sword, a dagger, a knife: pommel, grip, guard, blade (its tang inside the grip).
            if (grip != null)
                Part(rules, grip, into, new Vector3(0, -gripLength, 0), Quaternion.identity, false);
            if (pommel != null)
                Part(rules, pommel, into, new Vector3(0, -gripLength - Length(pommel), 0), Quaternion.identity, false);
            if (guard != null)
            {
                float gl = Length(guard);
                Part(rules, guard, into, new Vector3(-gl * 0.5f, 0, 0), Quaternion.Euler(0, 0, -90), false);
            }
            if (main != null)
            {
                float tang = d?.Component(main.intentRole)?.shape?.tangTo ?? 0f;
                Part(rules, main, into, new Vector3(0, -Length(main) * tang, 0), Quaternion.identity, false);
            }
        }
        foreach (CraftPiece p in parts)
            if (p != grip && p != main && p != guard && p != pommel && p.kind != CraftPieceKind.Fasteners && Fn(p) == CraftComponentFunction.Strap)
                Part(rules, p, into, new Vector3(0, -gripLength * 0.5f, 0.02f), Quaternion.identity, true);
    }

    /// <summary>One piece as a child of <paramref name="into"/>: along +Y from <paramref name="at"/> (or centred there).</summary>
    private static void Part(CraftRules rules, CraftPiece p, Transform into, Vector3 at, Quaternion turn, bool centred)
    {
        switch (p.kind)
        {
            case CraftPieceKind.Metal:
            case CraftPieceKind.Wood:
            {
                if (p.segments.Count == 0)
                    break;
                Mesh mesh = BarMesh(p, centred);
                bool metal = p.kind == CraftPieceKind.Metal;
                double polish = 0;
                foreach (MetalSegment s in p.segments) polish += s.polish;
                polish /= p.segments.Count;
                Color c = CraftArt.MetalColor(rules, p);
                if (metal && p.segments.Count > 0)
                {
                    double scale = 0;
                    foreach (MetalSegment s in p.segments) scale += s.scale;
                    c = Color.Lerp(c, new Color(0.16f, 0.15f, 0.15f), Mathf.Clamp01((float)(scale / p.segments.Count) * 4f));
                }
                Child(into, p.id, mesh, c, metal, metal ? 0.2f + 0.7f * (float)polish : 0.15f, at, turn);
                break;
            }
            case CraftPieceKind.Hide:
                if (p.hide != null && p.hide.cells.Count > 0)
                    Child(into, p.id, HideMesh(rules, p.hide, centred, out Color c), c, false, 0.25f, at, turn);
                break;
            case CraftPieceKind.Ore:
                Lump(into, at, Mathf.Clamp(Mathf.Pow((float)Math.Max(1, p.Mass(rules)) / 3.5f, 1f / 3f) * 0.0062f, 0.02f, 0.2f),
                    rules.Ore(p.ore.ore)?.color ?? new Color(0.4f, 0.35f, 0.3f));
                break;
            case CraftPieceKind.Fasteners:
                for (int i = 0; i < 4; i++)
                    Box(into, at + new Vector3((i - 1.5f) * 0.012f, 0, 0), new Vector3(0.006f, 0.016f, 0.006f), CraftArt.MetalColor(rules, p), true, 0.5f);
                break;
        }
    }

    private static float Length(CraftPiece p) => (float)(p.Length * 0.001);

    private static Vector3 SizeOf(CraftRules rules, CraftPiece p)
    {
        if (p.kind == CraftPieceKind.Hide && p.hide != null)
        {
            float cell = rules.Settings.cellSize * 0.01f;
            return new Vector3(p.hide.width * cell, p.hide.height * cell, 0.004f);
        }
        double w = 0;
        foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
        return new Vector3((float)(w * 0.001), Length(p), 0.01f);
    }

    // ------------------------------------------------------------------ meshes
    /// <summary>
    /// A bar or blade: a ring of six points per segment boundary - the spine, the two faces and the bevels each face
    /// takes down to the edge - following the bend, joined face by face (hard edges) and capped. Millimetres become
    /// metres; the length runs along +Y, the width along X (the edge toward +X), the thickness along Z.
    /// </summary>
    private static Mesh BarMesh(CraftPiece p, bool centred, bool perSegment = false)
    {
        List<MetalSegment> segs = p.segments;
        int n = segs.Count;
        var rings = new List<Vector3[]>(n + 1);
        Vector2 pos = Vector2.zero, dir = Vector2.up;
        for (int k = 0; k <= n; k++)
        {
            MetalSegment a = segs[Math.Max(0, k - 1)], b = segs[Math.Min(n - 1, k)];
            float hw = (float)(a.width + b.width) * 0.00025f;
            float ht = (float)(a.thickness + b.thickness) * 0.00025f;
            float bl = Mathf.Min((float)(a.bevelWidthL + b.bevelWidthL) * 0.0005f, hw * 1.9f);
            float br = Mathf.Min((float)(a.bevelWidthR + b.bevelWidthR) * 0.0005f, hw * 1.9f);
            float top = ht - (float)(a.bevelDepthL + b.bevelDepthL) * 0.0005f;
            float bottom = -ht + (float)(a.bevelDepthR + b.bevelDepthR) * 0.0005f;
            if (top < bottom + 0.0002f)
            {
                float mid = (top + bottom) * 0.5f;
                top = mid + 0.0001f;
                bottom = mid - 0.0001f;
            }
            Vector2 across = new Vector2(dir.y, -dir.x);
            Vector3 P(float x, float z)
            {
                Vector2 q = pos + across * x;
                return new Vector3(q.x, q.y, z);
            }
            rings.Add(new[] { P(-hw, ht), P(hw - bl, ht), P(hw, top), P(hw, bottom), P(hw - br, -ht), P(-hw, -ht) });
            if (k < n)
            {
                pos += dir * (float)(segs[k].length * 0.001);
                float bend = (float)segs[k].bend * Mathf.Deg2Rad;
                dir = new Vector2(dir.x * Mathf.Cos(bend) - dir.y * Mathf.Sin(bend), dir.x * Mathf.Sin(bend) + dir.y * Mathf.Cos(bend));
            }
        }
        var v = new List<Vector3>();
        var tris = new List<List<int>>();
        for (int k = 0; k < n; k++)
        {
            if (perSegment || k == 0) tris.Add(new List<int>());
            List<int> tri = tris[tris.Count - 1];
            Vector3[] r0 = rings[k], r1 = rings[k + 1];
            Vector3 axis = (Center(r0) + Center(r1)) * 0.5f;
            for (int s = 0; s < r0.Length; s++)
            {
                int s2 = (s + 1) % r0.Length;
                Quad(v, tri, r0[s], r1[s], r1[s2], r0[s2], axis);
            }
        }
        Cap(v, tris[0], rings[0], Center(rings[Math.Min(1, n)]));
        Cap(v, tris[tris.Count - 1], rings[n], Center(rings[Math.Max(0, n - 1)]));
        if (centred)
        {
            Vector3 mid = (Center(rings[0]) + Center(rings[n])) * 0.5f;
            for (int i = 0; i < v.Count; i++) v[i] -= mid;
        }
        if (tris.Count == 1)
            return Build("Bar", v, tris[0]);
        var mesh = new Mesh { name = "Bar (by segment)", hideFlags = HideFlags.DontSave };
        if (v.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.subMeshCount = tris.Count;
        for (int i = 0; i < tris.Count; i++)
            mesh.SetTriangles(tris[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ for the hands-on work places
    /// <summary>
    /// A metal or wooden piece as it is now, one sub-mesh per segment (so each can glow at its own heat), along +Y from
    /// its start (or centred). Null when it has no segments.
    /// </summary>
    public static Mesh PieceMesh(CraftPiece p, bool centred) => p != null && p.segments.Count > 0 ? BarMesh(p, centred, true) : null;

    /// <summary>A shared material of <paramref name="c"/> that glows with <paramref name="glow"/> at <paramref name="strength"/> (0-1): hot metal.</summary>
    public static Material Glowing(Color c, bool metal, float smooth, Color glow, float strength)
    {
        strength = Mathf.Clamp01(strength);
        if (strength < 0.02f)
            return MaterialFor(c, metal, smooth);
        int key = Mathf.RoundToInt(c.r * 15) | Mathf.RoundToInt(c.g * 15) << 4 | Mathf.RoundToInt(c.b * 15) << 8 | Mathf.RoundToInt(glow.r * 15) << 12
                  | Mathf.RoundToInt(glow.g * 15) << 16 | Mathf.RoundToInt(glow.b * 15) << 20 | Mathf.RoundToInt(strength * 15) << 24 | 1 << 30 | (metal ? int.MinValue : 0);
        if (materials.TryGetValue(key, out Material m) && m != null)
            return m;
        m = MaterialFor(c, metal, smooth);
        if (m == null)
            return null;
        m = new Material(m) { name = "Hot Metal", hideFlags = HideFlags.DontSave };
        Color hot = Color.Lerp(c, glow, strength);
        m.color = hot;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hot);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow * (strength * strength * 3f));
        materials[key] = m;
        return m;
    }

    /// <summary>A shared plain material (props of the work places).</summary>
    public static Material Plain(Color c, bool metal = false, float smooth = 0.3f) => MaterialFor(c, metal, smooth);

    /// <summary>A hide or a cut part: its cells as a sheet of its thickness, with sides where a neighbour is missing.</summary>
    private static Mesh HideMesh(CraftRules rules, HideState h, bool centred, out Color color)
    {
        float cell = rules.Settings.cellSize * 0.01f;
        var v = new List<Vector3>();
        var tri = new List<int>();
        Color sum = Color.clear;
        int count = 0;
        bool Present(int x, int y) => x >= 0 && y >= 0 && x < h.width && y < h.height && y * h.width + x < h.cells.Count && h.cells[y * h.width + x].present;
        Vector3 offset = centred ? new Vector3(-h.width * cell * 0.5f, -h.height * cell * 0.5f, 0) : Vector3.zero;
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                if (!Present(x, y)) continue;
                HideCell c = h.cells[y * h.width + x];
                sum += CraftArt.HideCell(rules, h, c);
                count++;
                float t = Mathf.Max(0.001f, (float)c.thickness * 0.001f) * 0.5f;
                Vector3 a = offset + new Vector3(x * cell, y * cell, 0), b = a + new Vector3(cell, 0, 0), d = a + new Vector3(0, cell, 0), e = a + new Vector3(cell, cell, 0);
                Vector3 up = new Vector3(0, 0, t), mid = (a + e) * 0.5f;
                Quad(v, tri, a + up, d + up, e + up, b + up, mid);
                Quad(v, tri, a - up, b - up, e - up, d - up, mid);
                if (!Present(x - 1, y)) Quad(v, tri, a - up, d - up, d + up, a + up, mid);
                if (!Present(x + 1, y)) Quad(v, tri, b + up, e + up, e - up, b - up, mid);
                if (!Present(x, y - 1)) Quad(v, tri, a + up, b + up, b - up, a - up, mid);
                if (!Present(x, y + 1)) Quad(v, tri, d - up, e - up, e + up, d + up, mid);
            }
        color = count > 0 ? sum / count : new Color(0.55f, 0.4f, 0.3f);
        color.a = 1f;
        return Build("Hide", v, tri);
    }

    /// <summary>A quad facing away from <paramref name="inside"/> (Unity's front faces wind clockwise).</summary>
    private static void Quad(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + c) * 0.5f - inside) >= 0f;
        if (outward)
        {
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }
        else
        {
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 1);
            tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
        }
    }

    /// <summary>A fan closing <paramref name="ring"/>, facing away from <paramref name="inside"/>.</summary>
    private static void Cap(List<Vector3> v, List<int> tri, Vector3[] ring, Vector3 inside)
    {
        int i = v.Count;
        Vector3 c0 = Center(ring);
        v.Add(c0);
        foreach (Vector3 p in ring) v.Add(p);
        for (int s = 0; s < ring.Length; s++)
        {
            int a = i + 1 + s, b = i + 1 + (s + 1) % ring.Length;
            bool outward = Vector3.Dot(Vector3.Cross(v[a] - c0, v[b] - c0), c0 - inside) >= 0f;
            if (outward) { tri.Add(i); tri.Add(a); tri.Add(b); }
            else { tri.Add(i); tri.Add(b); tri.Add(a); }
        }
    }

    private static Vector3 Center(Vector3[] ring)
    {
        Vector3 c = Vector3.zero;
        foreach (Vector3 p in ring) c += p;
        return c / Mathf.Max(1, ring.Length);
    }

    private static Mesh Build(string name, List<Vector3> v, List<int> tri)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        if (v.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ objects
    private static GameObject New(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Root, false);
        return go;
    }

    private static GameObject Keep(string key, GameObject go)
    {
        if (go.transform.childCount == 0)
        {
            Destroy(go);
            return null;
        }
        cache[key] = go;
        return go;
    }

    private static void Child(Transform into, string name, Mesh mesh, Color color, bool metal, float smooth, Vector3 at, Quaternion turn)
    {
        var go = new GameObject(string.IsNullOrEmpty(name) ? "Part" : name);
        go.transform.SetParent(into, false);
        go.transform.localPosition = at;
        go.transform.localRotation = turn;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFor(color, metal, smooth);
    }

    private static void Box(Transform into, Vector3 at, Vector3 size, Color color, bool metal, float smooth) =>
        Primitive(into, PrimitiveType.Cube, at, size, color, metal, smooth);

    private static void Lump(Transform into, Vector3 at, float radius, Color color) =>
        Primitive(into, PrimitiveType.Sphere, at, new Vector3(radius * 2.2f, radius * 1.6f, radius * 1.9f), color, false, 0.25f);

    private static void Roll(Transform into, float radius, float length, Color color)
    {
        GameObject go = Primitive(into, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2, length * 0.5f, radius * 2), color, false, 0.2f);
        go.transform.localRotation = Quaternion.Euler(0, 0, 90);
    }

    private static GameObject Primitive(Transform into, PrimitiveType type, Vector3 at, Vector3 size, Color color, bool metal, float smooth)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Collider col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        go.transform.SetParent(into, false);
        go.transform.localPosition = at;
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = MaterialFor(color, metal, smooth);
        return go;
    }

    private static Material MaterialFor(Color c, bool metal, float smooth)
    {
        int key = Mathf.RoundToInt(c.r * 31) | Mathf.RoundToInt(c.g * 31) << 5 | Mathf.RoundToInt(c.b * 31) << 10 | (metal ? 1 << 15 : 0)
                  | Mathf.RoundToInt(Mathf.Clamp01(smooth) * 7) << 16;
        if (materials.TryGetValue(key, out Material m) && m != null)
            return m;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;
        m = new Material(shader) { name = metal ? "Forged Metal" : "Crafted Material", hideFlags = HideFlags.DontSave, color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal ? 0.85f : 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        materials[key] = m;
        return m;
    }

    private static Color Average(Sprite s)
    {
        Texture2D t = s != null ? s.texture : null;
        if (t == null || !t.isReadable)
            return new Color(0.5f, 0.5f, 0.52f);
        Color sum = Color.clear;
        int n = 0;
        Color[] px = t.GetPixels();
        for (int i = 0; i < px.Length; i += 7)
        {
            if (px[i].a < 0.5f) continue;
            sum += px[i];
            n++;
        }
        Color c = n > 0 ? sum / n : new Color(0.5f, 0.5f, 0.52f);
        c.a = 1f;
        return c;
    }

    private static void Destroy(GameObject go)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }
}
