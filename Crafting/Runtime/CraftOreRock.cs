using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One stone of an ore lump in 3D, for the brushes, as big as it is (a pebble drawn larger: <see cref="Magnified"/>, so the
/// brush covers as much of it as it would of the real one): a lumpy, seeded mesh coloured vertex by vertex - the ore mineral in
/// veins and patches through the loose rock, a dull weathered rind over parts of it, and patches of dirt and caked clay
/// on top - as much of each as the fragment really holds now (and what each brush has already gone over, the fragment's
/// <see cref="OreFragment.Brushed"/>). Brushing scrubs the vertices under the brush: the dirt comes off as well as that
/// brush takes it, the wire brush knocks the rind off - each brush going over each patch once, as the workshop's rule
/// says. What a going-over sends (<see cref="GoingOver"/>, then <see cref="NewGoingOver"/>) is counted as the workshop
/// counts it (<see cref="CraftBrushCover"/>: each vertex by the patch of the stone it stands for - the many vertices
/// crowded at the poles count for the little surface they cover - and by what the brush can take there), so the stone
/// shows what the workshop does with it.
/// </summary>
public sealed class CraftOreRock
{
    private const int Lat = 16, Lon = 28;

    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Vector3> dirs = new List<Vector3>();
    private readonly List<int> tris = new List<int>();
    private readonly List<Color> colors = new List<Color>();
    private Color[] baseColor = new Color[0];
    // What each vertex shows and what each brush has done there: the workshop's own accounting.
    private readonly CraftBrushCover cover = new CraftBrushCover();
    private double dirtAtStart, rindAtStart;
    private Color dirtColor, rindColor;

    /// <summary>The smallest and the largest a stone is drawn (m, its radius): a fine is drawn larger than it is.</summary>
    public const float MinRadius = 0.03f, MaxRadius = 0.08f;
    private float realRadius = 0.06f;

    public Mesh Mesh { get; private set; }
    /// <summary>The stone's radius as drawn (m): its real size, within <see cref="MinRadius"/>..<see cref="MaxRadius"/>.</summary>
    public float Radius { get; private set; } = 0.06f;
    /// <summary>How much larger than it really is the stone is drawn (1 for a stone drawn at its size).</summary>
    public float Magnified => Radius / realRadius;
    /// <summary>The fragment this stone shows (its index in the lump when it was built).</summary>
    public int Fragment { get; private set; } = -1;

    /// <summary>
    /// The dirt on it now as a share of what it had when it was put on the bench (0-1, by its mass): what the brushes took
    /// since, as the workshop takes it. The station's dirt share counts the stone lighter too, by all the brushes took off
    /// it (<see cref="CraftOres.BrushTo"/>: what the 3D ore bench's Clean meter shows).
    /// </summary>
    public double DirtSinceBuilt => dirtAtStart > 1e-6 ? cover.MeanDirt / dirtAtStart : 1;
    /// <summary>Rind left on it, as a share of what the stone had before any brush knocked it off (0-1).</summary>
    public float RindLeft => rindAtStart > 1e-6 ? (float)(cover.MeanRind / rindAtStart) : 0f;
    public bool Dirty => cover.MeanDirt > 0.05;
    public bool Rinded => cover.MeanRind > 0.05;

    /// <summary>
    /// A new going-over starts: what was scrubbed so far went to the workshop as one going-over, and what a brush goes over
    /// from here is the next one. The stone stays as it is: a brush that has gone over a patch takes nothing more there.
    /// </summary>
    public void NewGoingOver() => cover.Begin();

    /// <summary>How much more of the stone <paramref name="brush"/> went over in this going-over, as the workshop counts it (0-1): what it sends.</summary>
    public double GoingOver(CraftOreBrush brush) => cover.GoingOver(brush);

    /// <summary>How far <paramref name="brush"/> has gone over the stone, as the workshop counts it (0-1: at 1 it takes no more).</summary>
    public double Covered(CraftOreBrush brush) => cover.Covered(brush);

    /// <summary>Builds the stone for fragment <paramref name="index"/> of <paramref name="lump"/> (the same stone every time).</summary>
    public void Build(CraftRules rules, CraftPiece lump, int index)
    {
        Fragment = index;
        OreFragment f = lump.ore.fragments[index];
        CraftOreDefinition o = rules.Ore(lump.ore.ore);
        Color ore = o != null ? o.color : new Color(0.55f, 0.3f, 0.25f);
        Color rock = o != null ? o.gangueColor : new Color(0.7f, 0.7f, 0.68f);
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        dirtColor = clay ? new Color(0.58f, 0.48f, 0.36f) : new Color(0.36f, 0.27f, 0.18f);
        rindColor = Color.Lerp(rock, new Color(0.42f, 0.38f, 0.32f), 0.6f);
        var rnd = new System.Random(CraftMath.Hash(lump.ore.deposit * 131 + lump.ore.lump, index * 7919 + (int)(f.size * 100)));
        List<(Vector3 at, float r, float h)> Blobs(int n, float rMin, float rMax, float hMin, float hMax)
        {
            var b = new List<(Vector3, float, float)>(n);
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1);
                if (v.sqrMagnitude < 1e-4f) v = Vector3.up;
                b.Add((v.normalized, Mathf.Lerp(rMin, rMax, (float)rnd.NextDouble()), Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble())));
            }
            return b;
        }
        float Field(List<(Vector3 at, float r, float h)> blobs, Vector3 d)
        {
            float sum = 0f;
            foreach ((Vector3 at, float r, float h) in blobs)
            {
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(at, d), -1f, 1f));
                float t = 1f - angle / r;
                if (t > 0f) sum += h * t * t * (3f - 2f * t); // a smooth bump, highest at its centre
            }
            return sum;
        }
        var bumps = Blobs(10, 0.5f, 1.2f, -0.18f, 0.28f);
        var veins = Blobs(4 + (int)(f.mineral * 10), 0.3f, 0.9f, 0.6f, 1.2f);
        var crust = Blobs(6, 0.5f, 1.3f, 0.6f, 1.1f);
        var mud = Blobs(9, 0.4f, 1.1f, 0.6f, 1.2f);
        // As big as it really is (its size is across it), a pebble drawn no smaller than can be brushed and seen.
        realRadius = Mathf.Max(0.0005f, (float)f.size * 0.005f);
        Radius = Mathf.Clamp(realRadius, MinRadius, MaxRadius);
        float squash = 0.72f + 0.2f * (float)rnd.NextDouble();
        float mineralCut = 1f - Mathf.Clamp01((float)f.mineral) * 1.1f;
        // The dirt in its patches, as deep as the stone's dirt is now (a dirtier stone darker, a brushed one lighter); the
        // rind as the rock carries it, less what the brushes have already knocked off.
        float dirtShare = Mathf.Clamp01((float)f.dirt / 0.15f);
        float rindShare = Mathf.Clamp01((float)(f.gangue * f.RindShare) * 3f);
        var takesDirt = new double[3];
        var takesRind = new double[3];
        var brushed = new double[3];
        double rindKept = 1;
        for (int b = 0; b < 3; b++)
        {
            takesDirt[b] = CraftOres.BrushOnDirt((CraftOreBrush)b, o);
            takesRind[b] = CraftOres.BrushOnRind((CraftOreBrush)b);
            brushed[b] = f.Brushed((CraftOreBrush)b);
            rindKept *= 1 - takesRind[b] * brushed[b];
        }

        verts.Clear(); dirs.Clear(); tris.Clear();
        var bc = new List<Color>();
        var dl = new List<double>();
        var rl = new List<double>();
        for (int y = 0; y <= Lat; y++)
        {
            float th = Mathf.PI * y / Lat;
            for (int x = 0; x <= Lon; x++)
            {
                float ph = 2f * Mathf.PI * x / Lon;
                // The seam column shares the first column's shape, so the stone closes without a crack.
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(x == Lon ? 0f : ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(x == Lon ? 0f : ph));
                float r = Radius * (1f + Field(bumps, d));
                verts.Add(new Vector3(d.x * r, d.y * r * squash, d.z * r));
                dirs.Add(new Vector3(d.x, d.y / squash, d.z).normalized);
                float vein = Field(veins, d);
                bc.Add(Color.Lerp(rock, ore, Mathf.Clamp01((vein - mineralCut) * 3f + 0.5f * (float)f.mineral)));
                dl.Add(Mathf.Clamp01((Field(mud, d) - 0.35f) * 2f) * dirtShare);
                rl.Add(Mathf.Clamp01((Field(crust, d) - (1f - rindShare)) * 3f) * rindKept);
            }
        }
        for (int y = 0; y < Lat; y++)
            for (int x = 0; x < Lon; x++)
            {
                int a = y * (Lon + 1) + x, b = a + 1, c = a + Lon + 1, e = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(e); tris.Add(c);
            }
        baseColor = bc.ToArray();
        // Each vertex's share of the surface: its ring's band of latitude, split among the ring's own vertices (the seam
        // column repeats the first and counts nothing).
        var area = new double[verts.Count];
        for (int y = 0; y <= Lat; y++)
        {
            float a = Mathf.PI * Mathf.Max(0f, y - 0.5f) / Lat, b = Mathf.PI * Mathf.Min(Lat, y + 0.5f) / Lat;
            float band = (Mathf.Cos(a) - Mathf.Cos(b)) / Lon;
            for (int x = 0; x <= Lon; x++)
                area[y * (Lon + 1) + x] = x == Lon ? 0f : band;
        }
        cover.Reset(area, dl, rl, takesDirt, takesRind, brushed);
        dirtAtStart = cover.MeanDirt;
        // The rind the stone had before any brush knocked it off: what the words call "left".
        rindAtStart = cover.MeanRind / System.Math.Max(1e-6, rindKept);
        if (Mesh == null) Mesh = new Mesh { name = "Ore Stone" };
        Mesh.SetVertices(verts);
        Mesh.SetTriangles(tris, 0);
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
        Recolour();
    }

    /// <summary>
    /// The brush drawn along a ray (in the stone's own space) with <paramref name="effort"/> (0-1 this frame): every
    /// vertex facing the ray within <paramref name="reach"/> of it is gone over, nearer ones more - each by
    /// <paramref name="brush"/> once at the most. True when it went over anything new.
    /// </summary>
    public bool Brush(Vector3 from, Vector3 dir, float reach, float effort, CraftOreBrush brush)
    {
        if (verts.Count == 0 || effort <= 0f || cover.Count != verts.Count)
            return false;
        dir = dir.normalized;
        bool any = false;
        for (int i = 0; i < verts.Count; i++)
        {
            if (Vector3.Dot(dirs[i], dir) > -0.15f)
                continue; // the far side of the stone
            Vector3 v = verts[i] - from;
            float along = Vector3.Dot(v, dir);
            float off = (v - dir * along).magnitude;
            if (along <= 0f || off >= reach)
                continue;
            if (cover.Scrub(i, brush, effort * (1f - off / reach)) > 0) any = true;
        }
        if (any)
            Recolour();
        return any;
    }

    /// <summary>Where the ray first meets the stone (its own space), if it does: the vertex facing it nearest the ray.</summary>
    public bool Hit(Vector3 from, Vector3 dir, float reach, out Vector3 point, out Vector3 normal)
    {
        point = normal = Vector3.zero;
        dir = dir.normalized;
        float best = reach;
        for (int i = 0; i < verts.Count; i++)
        {
            if (Vector3.Dot(dirs[i], dir) > -0.15f) continue;
            Vector3 v = verts[i] - from;
            float along = Vector3.Dot(v, dir);
            float off = (v - dir * along).magnitude;
            if (along <= 0f || off >= best) continue;
            best = off;
            point = verts[i];
            normal = dirs[i];
        }
        return best < reach;
    }

    private void Recolour()
    {
        colors.Clear();
        for (int i = 0; i < verts.Count; i++)
        {
            float dirt = (float)cover.Dirt(i), rind = (float)cover.Rind(i);
            Color c = Color.Lerp(baseColor[i], rindColor, rind * 0.85f);
            c = Color.Lerp(c, dirtColor, dirt * 0.92f);
            // Scrubbed clean: a little brighter, as fresh rock is.
            float fresh = Mathf.Max((float)cover.Done(i, CraftOreBrush.Soft), Mathf.Max((float)cover.Done(i, CraftOreBrush.Stiff), (float)cover.Done(i, CraftOreBrush.Wire)));
            c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.12f), fresh * (1f - dirt));
            c.a = 1f;
            colors.Add(c);
        }
        if (Mesh != null) Mesh.SetColors(colors);
    }

    /// <summary>A material that shows the vertex colours, lit where the pipeline has one (particle shaders read them).</summary>
    public static Material VertexColourMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) shader = Shader.Find("Particles/Standard Surface");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) { name = "Ore Stone", hideFlags = HideFlags.DontSave, color = Color.white } : null;
    }
}
