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
    private static readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();
    /// <summary>The meshes built here for the models kept (a bar's, a hide's): freed with their model - never Unity's own primitives.</summary>
    private static readonly HashSet<Mesh> built = new HashSet<Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        root = null;
        cache.Clear();
        materials.Clear();
        built.Clear();
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
            {
                // the blank at its size (a 2.2 m ash stave, a linden plank); an old asset without one as a stick
                Vector3 s = CraftStockItemSO.UnsetWoodSize(item.Size) ? new Vector3(450f, 40f, 40f) : item.Size;
                Box(go.transform, Vector3.zero, new Vector3(s.y, s.x, s.z) * 0.001f, c, false, 0.2f);
                break;
            }
            case CraftStockKind.Log:
                Roll(go.transform, item.Size.y > 0 ? item.Size.y * 0.0005f : 0.12f, item.Size.x > 0 ? item.Size.x * 0.001f : 0.9f, c);
                break;
            case CraftStockKind.Gem:
                Lump(go.transform, Vector3.zero, 0.02f, c);
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

    /// <summary>
    /// Forgets a model (its item is gone): the object and the meshes built for it (a piece's bar or hide - the shared
    /// materials and Unity's own primitive meshes stay).
    /// </summary>
    public static void Forget(string key)
    {
        if (key != null && cache.TryGetValue(key, out GameObject go))
        {
            cache.Remove(key);
            if (go == null)
                return;
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && built.Remove(f.sharedMesh))
                    Destroy(f.sharedMesh);
            Destroy(go);
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
            case CraftPieceKind.Fibre:
                Lump(into, at, 0.06f, Color.Lerp(new Color(0.93f, 0.9f, 0.82f), new Color(0.6f, 0.52f, 0.4f), (float)p.fibre.dirt));
                break;
            case CraftPieceKind.Yarn:
                Roll(into, 0.03f, 0.08f, new Color(0.9f, 0.86f, 0.76f));
                break;
            case CraftPieceKind.Gem:
                Lump(into, at, Mathf.Clamp(Mathf.Pow((float)Math.Max(0.05, p.gem.carats), 1f / 3f) * 0.004f, 0.003f, 0.03f), rules.Gem(p.gem.gem)?.color ?? Color.white);
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
    /// A bar or blade: a ring of points per segment boundary - the spine, the two faces (each with the fuller's groove down
    /// its middle, flat where there is none) and the bevels each face takes down to the edge - following the bend and
    /// lifted by the dish, joined face by face (hard edges) and capped; a cut half made notches both faces across it, as
    /// deep as it has gone. A double-edged blade has its bevels mirrored onto the other edge (meeting in the middle at
    /// most), as the section shows them. What is not metal goes into one more sub-mesh after the metal's
    /// (<see cref="MarkedMesh"/>): an eye drifted through it (a dark disc on each face) and the cracks that show (dark
    /// lines in from the edge), as the drawings mark them. Millimetres become metres; the length runs along +Y, the width
    /// along X (the edge toward +X), the thickness along Z (the left face toward +Z).
    /// </summary>
    private static Mesh BarMesh(CraftPiece p, bool centred, bool perSegment = false)
    {
        List<MetalSegment> segs = p.segments;
        int n = segs.Count;
        bool both = p.edges >= 2;
        // The rings along the length (each boundary, and three more across a cut half made), the segment each ring's strip
        // to the next belongs to, and each segment's middle and heading (where its marks go).
        var rings = new List<Vector3[]>(n + 1);
        var owner = new List<int>(n + 1);
        var mids = new Vector2[n];
        var heads = new Vector2[n];
        Vector2 pos = Vector2.zero, dir = Vector2.up;
        for (int k = 0; k <= n; k++)
        {
            rings.Add(Ring(segs[Math.Max(0, k - 1)], segs[Math.Min(n - 1, k)], both, pos, dir, 0f));
            if (k == n)
                break;
            MetalSegment s = segs[k];
            float len = (float)(s.length * 0.001);
            owner.Add(k);
            mids[k] = pos + dir * len * 0.5f;
            heads[k] = dir;
            float cut = Mathf.Clamp((float)s.cut, 0f, 0.95f);
            if (cut > 0.01f)
            {
                // A V across both faces at the middle, half the cut from each, about as wide as it is deep.
                float half = Mathf.Clamp((float)(s.thickness * cut) / Mathf.Max(0.1f, (float)s.length), 0.05f, 0.3f);
                rings.Add(Ring(s, s, both, pos + dir * len * (0.5f - half), dir, 0f));
                owner.Add(k);
                rings.Add(Ring(s, s, both, mids[k], dir, cut));
                owner.Add(k);
                rings.Add(Ring(s, s, both, pos + dir * len * (0.5f + half), dir, 0f));
                owner.Add(k);
            }
            pos += dir * len;
            float bend = (float)s.bend * Mathf.Deg2Rad;
            dir = new Vector2(dir.x * Mathf.Cos(bend) - dir.y * Mathf.Sin(bend), dir.x * Mathf.Sin(bend) + dir.y * Mathf.Cos(bend));
        }
        var v = new List<Vector3>();
        var tris = new List<List<int>>();
        for (int k = 0; k < (perSegment ? n : 1); k++) tris.Add(new List<int>());
        // Every ring runs the same way round, so one face that is surely outside (the edge's flat, outermost on +X) tells the
        // winding for them all - the fuller's groove is not convex, and a test against the middle would turn its walls inside out.
        const int e0 = 6, e1 = 7;
        bool outward = Vector3.Dot(Vector3.Cross(rings[1][e0] - rings[0][e0], rings[1][e1] - rings[0][e0]),
                                   (rings[0][e0] + rings[1][e1]) * 0.5f - (Center(rings[0]) + Center(rings[1])) * 0.5f) >= 0f;
        for (int i = 0; i + 1 < rings.Count; i++)
        {
            List<int> tri = tris[perSegment ? owner[i] : 0];
            Vector3[] r0 = rings[i], r1 = rings[i + 1];
            for (int s = 0; s < r0.Length; s++)
            {
                int s2 = (s + 1) % r0.Length;
                Quad(v, tri, r0[s], r1[s], r1[s2], r0[s2], outward);
            }
        }
        Cap(v, tris[0], rings[0], Center(rings[1]));
        Cap(v, tris[tris.Count - 1], rings[rings.Count - 1], Center(rings[rings.Count - 2]));
        // The marks: an eye (a dark disc on each face), the cracks that show (from the edge in, on each face) - laid on the
        // section the mesh has at the segment's middle, where its rings meet halfway.
        var marks = new List<int>();
        for (int k = 0; k < n; k++)
        {
            MetalSegment s = segs[k];
            if (s.hole <= 0.5)
                continue;
            float r = Mathf.Min((float)s.hole * 0.0005f, (float)Mid(segs, k, g => g.width) * 0.00045f);
            float ht = (float)Mid(segs, k, g => g.thickness) * 0.0005f, dish = (float)Mid(segs, k, g => g.dish) * 0.001f;
            Disc(v, marks, mids[k], heads[k], r, ht + dish + 0.0002f, 1f);
            Disc(v, marks, mids[k], heads[k], r, -ht + dish - 0.0002f, -1f);
        }
        foreach (CraftDefect d in p.defects)
            if (Shows(d, n))
                CrackMark(v, marks, segs, d.at, both, mids[d.at], heads[d.at], d.severity);
        if (marks.Count > 0)
            tris.Add(marks);
        if (centred)
        {
            Vector3 mid = (Center(rings[0]) + Center(rings[rings.Count - 1])) * 0.5f;
            for (int i = 0; i < v.Count; i++) v[i] -= mid;
        }
        var mesh = new Mesh { name = perSegment ? "Bar (by segment)" : "Bar", hideFlags = HideFlags.DontSave };
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

    /// <summary>
    /// One ring of a bar's section at <paramref name="at"/> (heading <paramref name="along"/>), from segment
    /// <paramref name="a"/>'s values and <paramref name="b"/>'s (their mean at a boundary): the top face from the back to
    /// the edge (with the groove's four points), the edge, the bottom face back (its groove), and on a double-edged blade
    /// the back edge. <paramref name="notch"/> (0-1) takes that share of the thickness off, half from each face. Always the
    /// same points in the same order (a groove of no depth lies flat in its face), so rings join point to point.
    /// </summary>
    private static Vector3[] Ring(MetalSegment a, MetalSegment b, bool both, Vector2 at, Vector2 along, float notch)
    {
        float hw = (float)(a.width + b.width) * 0.00025f;
        float ht = (float)(a.thickness + b.thickness) * 0.00025f;
        float cap = both ? hw : hw * 1.9f;
        float bl = Mathf.Min((float)(a.bevelWidthL + b.bevelWidthL) * 0.0005f, cap);
        float br = Mathf.Min((float)(a.bevelWidthR + b.bevelWidthR) * 0.0005f, cap);
        float top = ht - (float)(a.bevelDepthL + b.bevelDepthL) * 0.0005f;
        float bottom = -ht + (float)(a.bevelDepthR + b.bevelDepthR) * 0.0005f;
        // The fuller: its depth is both faces' together, half in each, down the middle of the flat between the bevels.
        float groove = Mathf.Min((float)(a.fuller + b.fuller) * 0.00025f, ht * 0.45f);
        if (notch > 0f)
        {
            ht *= 1f - notch;
            top = Mathf.Min(top, ht);
            bottom = Mathf.Max(bottom, -ht);
            groove = Mathf.Min(groove, ht * 0.45f);
        }
        if (top < bottom + 0.0002f)
        {
            float mid = (top + bottom) * 0.5f;
            top = mid + 0.0001f;
            bottom = mid - 0.0001f;
        }
        float dish = (float)(a.dish + b.dish) * 0.0005f;
        float backTop = both ? -hw + bl : -hw, backBottom = both ? -hw + br : -hw;
        Groove(backTop, hw - bl, hw, groove, out float t0, out float t1, out float tq, out float td);
        Groove(backBottom, hw - br, hw, groove, out float b0, out float b1, out float bq, out float bd);
        Vector2 across = new Vector2(along.y, -along.x);
        Vector3 P(float x, float z)
        {
            Vector2 q = at + across * x;
            return new Vector3(q.x, q.y, z + dish);
        }
        var ring = new List<Vector3>(16)
        {
            P(backTop, ht), P(t0, ht), P(t0 + tq, ht - td), P(t1 - tq, ht - td), P(t1, ht), P(hw - bl, ht), P(hw, top),
            P(hw, bottom), P(hw - br, -ht), P(b1, -ht), P(b1 - bq, -ht + bd), P(b0 + bq, -ht + bd), P(b0, -ht), P(backBottom, -ht),
        };
        if (both)
        {
            ring.Add(P(-hw, bottom));
            ring.Add(P(-hw, top));
        }
        return ring.ToArray();
    }

    /// <summary>
    /// Where a face's groove lies on its flat from <paramref name="back"/> to <paramref name="shoulder"/>: about a quarter of
    /// the width about the middle, inside the flat; a flat too narrow (or no fuller) gets a groove of no depth at its middle.
    /// </summary>
    private static void Groove(float back, float shoulder, float hw, float depth, out float x0, out float x1, out float slope, out float deep)
    {
        float margin = hw * 0.05f, g = hw * 0.24f;
        x0 = Mathf.Max(-g, back + margin);
        x1 = Mathf.Min(g, shoulder - margin);
        deep = depth;
        if (x1 - x0 < hw * 0.08f || depth <= 1e-6f)
        {
            x0 = x1 = (back + shoulder) * 0.5f;
            deep = 0f;
        }
        slope = (x1 - x0) * 0.25f;
    }

    /// <summary>
    /// A value of the section the mesh has at the middle of segment <paramref name="k"/>: halfway between its two rings,
    /// each the mean of the segment and its neighbour.
    /// </summary>
    private static double Mid(List<MetalSegment> segs, int k, Func<MetalSegment, double> of) =>
        0.25 * of(segs[Math.Max(0, k - 1)]) + 0.5 * of(segs[k]) + 0.25 * of(segs[Math.Min(segs.Count - 1, k + 1)]);

    /// <summary>A crack as the top view draws it: one that shows (on the surface, or deep), on a segment there is.</summary>
    private static bool Shows(CraftDefect d, int segments) =>
        (d.kind == CraftDefectKind.Crack || d.kind == CraftDefectKind.ColdShut || d.kind == CraftDefectKind.Delamination)
        && d.at >= 0 && d.at < segments && (d.surface || d.severity >= 0.4);

    /// <summary>A dark disc of <paramref name="r"/> lying at height <paramref name="z"/>, facing <paramref name="facing"/> (+1 up the thickness, -1 down).</summary>
    private static void Disc(List<Vector3> v, List<int> tri, Vector2 at, Vector2 along, float r, float z, float facing)
    {
        Vector2 across = new Vector2(along.y, -along.x);
        int c = v.Count;
        v.Add(new Vector3(at.x, at.y, z));
        const int sides = 12;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            Vector2 q = at + across * (Mathf.Cos(a) * r) + along * (Mathf.Sin(a) * r);
            v.Add(new Vector3(q.x, q.y, z));
        }
        for (int i = 0; i < sides; i++)
            Tri(tri, v, c, c + 1 + i, c + 1 + (i + 1) % sides, new Vector3(0f, 0f, facing));
    }

    /// <summary>
    /// A crack in from the edge over both faces, as far across the width as the top view draws it (half its severity of
    /// the width, all of it at the most): down the bevel, then along the flat, a hair off the surface.
    /// </summary>
    private static void CrackMark(List<Vector3> v, List<int> tri, List<MetalSegment> segs, int k, bool both, Vector2 at, Vector2 along, double severity)
    {
        float hw = (float)Mid(segs, k, g => g.width) * 0.0005f, ht = (float)Mid(segs, k, g => g.thickness) * 0.0005f;
        float dish = (float)Mid(segs, k, g => g.dish) * 0.001f;
        float cap = both ? hw : hw * 1.9f;
        float bl = Mathf.Min((float)Mid(segs, k, g => g.bevelWidthL) * 0.001f, cap), br = Mathf.Min((float)Mid(segs, k, g => g.bevelWidthR) * 0.001f, cap);
        float top = ht - (float)Mid(segs, k, g => g.bevelDepthL) * 0.001f, bottom = -ht + (float)Mid(segs, k, g => g.bevelDepthR) * 0.001f;
        float depth = Mathf.Clamp01((float)severity * 0.5f) * hw * 2f, wide = severity > 0.6 ? 0.0014f : 0.0008f;
        Vector2 across = new Vector2(along.y, -along.x);
        for (int face = 0; face < 2; face++)
        {
            float sg = face == 0 ? 1f : -1f, bevel = face == 0 ? bl : br, apex = face == 0 ? top : bottom;
            var path = new List<Vector2> { new Vector2(hw, apex) };
            if (bevel > 1e-5f && depth > 1e-5f)
            {
                float f = Mathf.Min(1f, depth / bevel);
                path.Add(new Vector2(hw - bevel * f, apex + (sg * ht - apex) * f));
            }
            if (depth > bevel) path.Add(new Vector2(hw - depth, sg * ht));
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 p0 = path[i], p1 = path[i + 1];
                // The face's outward side in the section (up on the top face, down underneath).
                Vector2 outSide = new Vector2((p1.y - p0.y) * sg, -(p1.x - p0.x) * sg).normalized;
                Vector3 W(Vector2 q, float l)
                {
                    Vector2 xy = at + across * (q.x + outSide.x * 0.0002f) + along * l;
                    return new Vector3(xy.x, xy.y, q.y + outSide.y * 0.0002f + dish);
                }
                int c = v.Count;
                v.Add(W(p0, -wide * 0.5f));
                v.Add(W(p0, wide * 0.5f));
                v.Add(W(p1, wide * 0.5f));
                v.Add(W(p1, -wide * 0.5f));
                var facing = new Vector3(across.x * outSide.x, across.y * outSide.x, outSide.y);
                Tri(tri, v, c, c + 1, c + 2, facing);
                Tri(tri, v, c, c + 2, c + 3, facing);
            }
        }
    }

    /// <summary>The triangle <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/> (vertex indices) wound to face <paramref name="facing"/>.</summary>
    private static void Tri(List<int> tri, List<Vector3> v, int a, int b, int c, Vector3 facing)
    {
        bool front = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) >= 0f;
        tri.Add(a);
        tri.Add(front ? b : c);
        tri.Add(front ? c : b);
    }

    /// <summary>
    /// Has <paramref name="mesh"/> - with <paramref name="metalSubMeshes"/> sub-meshes of metal (one per segment for
    /// <see cref="PieceMesh"/>, else one) - a last one of marks (an eye, cracks), to be given <see cref="Marks"/>?
    /// </summary>
    public static bool MarkedMesh(Mesh mesh, int metalSubMeshes) => mesh != null && mesh.subMeshCount > metalSubMeshes;

    /// <summary>The dark material of a piece's marks (an eye, a crack).</summary>
    public static Material Marks => MaterialFor(new Color(0.03f, 0.03f, 0.035f), false, 0.1f);

    /// <summary>
    /// A number that changes when what <see cref="PieceMesh"/> builds would: the segments' shape (length, width,
    /// thickness, bevels, bend, dish, fuller, cut, eye), the edges, and the cracks that show.
    /// </summary>
    public static uint ShapeKey(CraftPiece p)
    {
        var h = new CraftChecksum();
        h.Add(p.segments.Count);
        h.Add(p.edges);
        foreach (MetalSegment s in p.segments)
        {
            h.Add(s.length, 0.2);
            h.Add(s.width, 0.2);
            h.Add(s.thickness, 0.1);
            h.Add(s.bevelDepthL, 0.05);
            h.Add(s.bevelDepthR, 0.05);
            h.Add(s.bevelWidthL, 0.1);
            h.Add(s.bevelWidthR, 0.1);
            h.Add(s.bend, 0.5);
            h.Add(s.dish, 0.2);
            h.Add(s.fuller, 0.1);
            h.Add(s.cut, 0.02);
            h.Add(s.hole, 0.2);
        }
        foreach (CraftDefect d in p.defects)
            if (Shows(d, p.segments.Count))
            {
                h.Add(d.at);
                h.Add(d.severity, 0.05);
            }
        return h.Value;
    }

    // ------------------------------------------------------------------ for the hands-on work places
    /// <summary>
    /// A metal or wooden piece as it is now, one sub-mesh per segment (so each can glow at its own heat), along +Y from
    /// its start (or centred). Null when it has no segments.
    /// </summary>
    public static Mesh PieceMesh(CraftPiece p, bool centred) => p != null && p.segments.Count > 0 ? BarMesh(p, centred, true) : null;

    /// <summary>
    /// A shared material of <paramref name="c"/> that glows with <paramref name="glow"/> at <paramref name="strength"/>
    /// (0-1): hot metal - as smooth as it is polished (kept apart by its smoothness too).
    /// </summary>
    public static Material Glowing(Color c, bool metal, float smooth, Color glow, float strength)
    {
        strength = Mathf.Clamp01(strength);
        if (strength < 0.02f)
            return MaterialFor(c, metal, smooth);
        long key = Mathf.RoundToInt(c.r * 15) | Mathf.RoundToInt(c.g * 15) << 4 | Mathf.RoundToInt(c.b * 15) << 8 | Mathf.RoundToInt(glow.r * 15) << 12
                   | Mathf.RoundToInt(glow.g * 15) << 16 | Mathf.RoundToInt(glow.b * 15) << 20 | Mathf.RoundToInt(strength * 15) << 24
                   | (long)Mathf.RoundToInt(Mathf.Clamp01(smooth) * 7) << 28 | (metal ? 1L << 31 : 0L) | 1L << 32;
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
    private static void Quad(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside) =>
        Quad(v, tri, a, b, c, d, Vector3.Dot(Vector3.Cross(b - a, c - a), (a + c) * 0.5f - inside) >= 0f);

    /// <summary>A quad wound as given: <paramref name="outward"/> = a, b, c is its front (Unity's front faces wind clockwise).</summary>
    private static void Quad(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool outward)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
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
        if (mesh != null) built.Add(mesh);
        var go = new GameObject(string.IsNullOrEmpty(name) ? "Part" : name);
        go.transform.SetParent(into, false);
        go.transform.localPosition = at;
        go.transform.localRotation = turn;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = MarkedMesh(mesh, 1) ? new[] { MaterialFor(color, metal, smooth), Marks } : new[] { MaterialFor(color, metal, smooth) };
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
        long key = Mathf.RoundToInt(c.r * 31) | Mathf.RoundToInt(c.g * 31) << 5 | Mathf.RoundToInt(c.b * 31) << 10 | (metal ? 1 << 15 : 0)
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

    private static void Destroy(UnityEngine.Object o)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(o);
        else UnityEngine.Object.DestroyImmediate(o);
    }
}
