using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One stone of an ore lump in 3D, for the brushes: a lumpy, seeded mesh coloured vertex by vertex - the ore mineral in
/// veins and patches through the loose rock, a dull weathered rind over parts of it, and patches of dirt and caked clay
/// on top - as much of each as the fragment really holds. Brushing scrubs the vertices under the brush: the dirt comes
/// off (as well as the brush can take it), the wire brush knocks the rind off. It measures how much of the surface each
/// brush has gone over, which is what the workshop's <see cref="CraftWorkshop.BrushOre"/> takes.
/// </summary>
public sealed class CraftOreRock
{
    private const int Lat = 16, Lon = 28;

    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Vector3> dirs = new List<Vector3>();
    private readonly List<int> tris = new List<int>();
    private readonly List<Color> colors = new List<Color>();
    private Color[] baseColor = new Color[0];
    private float[] dirt = new float[0], rind = new float[0];
    // How far each brush has gone over each vertex (0-1): a brush can clean only so much, however long it scrubs.
    private readonly float[][] scrubbed = new float[3][];
    private float dirtAtStart, rindAtStart;
    private Color dirtColor, rindColor;

    public Mesh Mesh { get; private set; }
    public float Radius { get; private set; } = 0.06f;
    /// <summary>The fragment this stone shows (its index in the lump when it was built).</summary>
    public int Fragment { get; private set; } = -1;

    /// <summary>Dirt left on it, as a share of what it had when it was put on the bench (0-1).</summary>
    public float DirtLeft => dirtAtStart > 1e-4f ? Mean(dirt) / dirtAtStart : 0f;
    /// <summary>Rind left on it, as a share of what it had (0-1).</summary>
    public float RindLeft => rindAtStart > 1e-4f ? Mean(rind) / rindAtStart : 0f;
    public bool Dirty => Mean(dirt) > 0.05f;
    public bool Rinded => Mean(rind) > 0.05f;

    /// <summary>How much of its surface <paramref name="brush"/> has gone over so far (0-1).</summary>
    public float Covered(CraftOreBrush brush) => scrubbed[(int)brush] != null ? Mean(scrubbed[(int)brush]) : 0f;

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
        Radius = Mathf.Lerp(0.04f, 0.07f, Mathf.Clamp01((float)f.size / 12f));
        float squash = 0.72f + 0.2f * (float)rnd.NextDouble();
        float mineralCut = 1f - Mathf.Clamp01((float)f.mineral) * 1.1f;
        float dirtShare = Mathf.Clamp01((float)f.dirt / 0.2f);
        float rindShare = Mathf.Clamp01((float)(f.gangue * CraftOres.Rind(f.size)) * 3f);

        verts.Clear(); dirs.Clear(); tris.Clear();
        var bc = new List<Color>();
        var dl = new List<float>();
        var rl = new List<float>();
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
                dl.Add(Mathf.Clamp01((Field(mud, d) - (1f - dirtShare)) * 3f));
                rl.Add(Mathf.Clamp01((Field(crust, d) - (1f - rindShare)) * 3f));
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
        dirt = dl.ToArray();
        rind = rl.ToArray();
        for (int k = 0; k < 3; k++) scrubbed[k] = new float[verts.Count];
        dirtAtStart = Mathf.Max(1e-4f, Mean(dirt));
        rindAtStart = Mathf.Max(1e-4f, Mean(rind));
        if (Mesh == null) Mesh = new Mesh { name = "Ore Stone" };
        Mesh.SetVertices(verts);
        Mesh.SetTriangles(tris, 0);
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
        Recolour();
    }

    /// <summary>
    /// The brush drawn along a ray (in the stone's own space) with <paramref name="effort"/> (0-1 this frame): every
    /// vertex facing the ray within <paramref name="reach"/> of it is scrubbed, nearer ones more. Returns how much of
    /// the stone's surface this brush newly went over (a share of the whole stone).
    /// </summary>
    public float Brush(Vector3 from, Vector3 dir, float reach, float effort, CraftOreBrush brush, float onDirt, float onRind)
    {
        if (verts.Count == 0 || effort <= 0f)
            return 0f;
        dir = dir.normalized;
        float[] done = scrubbed[(int)brush];
        float added = 0f;
        for (int i = 0; i < verts.Count; i++)
        {
            if (Vector3.Dot(dirs[i], dir) > -0.15f)
                continue; // the far side of the stone
            Vector3 v = verts[i] - from;
            float along = Vector3.Dot(v, dir);
            float off = (v - dir * along).magnitude;
            if (along <= 0f || off >= reach)
                continue;
            float take = Mathf.Min(1f - done[i], effort * (1f - off / reach));
            if (take <= 0f)
                continue;
            done[i] += take;
            added += take;
            dirt[i] = Mathf.Max(0f, dirt[i] - take * onDirt);
            rind[i] = Mathf.Max(0f, rind[i] - take * onRind);
        }
        if (added > 0f)
            Recolour();
        return added / verts.Count;
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
            Color c = Color.Lerp(baseColor[i], rindColor, rind[i] * 0.85f);
            c = Color.Lerp(c, dirtColor, dirt[i] * 0.92f);
            // Scrubbed clean: a little brighter, as fresh rock is.
            float fresh = Mathf.Max(scrubbed[0] != null ? scrubbed[0][i] : 0f, Mathf.Max(scrubbed[1] != null ? scrubbed[1][i] : 0f, scrubbed[2] != null ? scrubbed[2][i] : 0f));
            c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.12f), fresh * (1f - dirt[i]));
            c.a = 1f;
            colors.Add(c);
        }
        if (Mesh != null) Mesh.SetColors(colors);
    }

    private static float Mean(float[] v)
    {
        if (v == null || v.Length == 0) return 0f;
        float s = 0f;
        foreach (float x in v) s += x;
        return s / v.Length;
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
