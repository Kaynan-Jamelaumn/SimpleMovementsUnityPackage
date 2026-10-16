using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meshes for the placeholder hair, beards and eyebrows of <see cref="CharacterAppearance"/>: shells that wrap the
/// head (a sphere of radius 0.5 in "head units", centred on the head) between a top and a bottom line, with a
/// thickness, instead of scaled spheres and cubes. Angles in degrees: theta around the head (0 = the face, 90 = the
/// right ear, 180 = the back), phi down from the top of the head (0) to under the chin (180). Built once and cached.
/// </summary>
internal static class AppearanceMeshes
{
    private static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    private static Vector3 Dir(float thetaDeg, float phiDeg)
    {
        float t = thetaDeg * Mathf.Deg2Rad, p = phiDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(p) * Mathf.Sin(t), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Cos(t));
    }

    private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    /// <summary>
    /// A shell over the head between <paramref name="top"/>(theta) and <paramref name="bottom"/>(theta), from
    /// <paramref name="thetaMin"/> to <paramref name="thetaMax"/>, <paramref name="thickness"/> thick (inwards), with an
    /// optional <paramref name="bump"/>(theta, phi) added to the radius (curls, a fuller chin).
    /// </summary>
    public static Mesh Shell(string key, float thetaMin, float thetaMax, Func<float, float> top, Func<float, float> bottom,
        float radius, float thickness, Func<float, float, float> bump = null, int segTheta = 48, int segPhi = 18)
    {
        if (cache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;
        var v = new List<Vector3>();
        var tris = new List<int>();
        int cols = segTheta + 1, rows = segPhi + 1;
        // Outer surface, then inner surface (same grid).
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < cols; i++)
            {
                float t = Mathf.Lerp(thetaMin, thetaMax, i / (float)segTheta);
                float pTop = top(t), pBottom = Mathf.Max(pTop + 0.5f, bottom(t));
                for (int j = 0; j < rows; j++)
                {
                    float p = Mathf.Lerp(pTop, pBottom, j / (float)segPhi);
                    float r = radius + (bump != null ? bump(t, p) : 0f);
                    // The edges thin out so the shell does not end in a hard step.
                    float edge = Mathf.Min(Smooth(0f, 0.18f, j / (float)segPhi), Smooth(1f, 0.82f, j / (float)segPhi));
                    float th = thickness * Mathf.Lerp(0.25f, 1f, edge);
                    v.Add(Dir(t, p) * (layer == 0 ? r : r - th));
                }
            }
        int inner = cols * rows;
        int Idx(int i, int j, int l) => l * inner + i * rows + j;
        for (int i = 0; i < segTheta; i++)
            for (int j = 0; j < segPhi; j++)
            {
                Quad(v, tris, Idx(i, j, 0), Idx(i + 1, j, 0), Idx(i, j + 1, 0), Idx(i + 1, j + 1, 0), true);
                Quad(v, tris, Idx(i, j, 1), Idx(i + 1, j, 1), Idx(i, j + 1, 1), Idx(i + 1, j + 1, 1), false);
            }
        // Rims close the thickness along the four borders.
        bool closed = thetaMax - thetaMin >= 359.9f;
        for (int i = 0; i < segTheta; i++)
        {
            Rim(v, tris, Idx(i, 0, 0), Idx(i + 1, 0, 0), Idx(i, 0, 1), Idx(i + 1, 0, 1), Idx(i, 1, 0));
            Rim(v, tris, Idx(i, segPhi, 0), Idx(i + 1, segPhi, 0), Idx(i, segPhi, 1), Idx(i + 1, segPhi, 1), Idx(i, segPhi - 1, 0));
        }
        if (!closed)
            for (int j = 0; j < segPhi; j++)
            {
                Rim(v, tris, Idx(0, j, 0), Idx(0, j + 1, 0), Idx(0, j, 1), Idx(0, j + 1, 1), Idx(1, j, 0));
                Rim(v, tris, Idx(segTheta, j, 0), Idx(segTheta, j + 1, 0), Idx(segTheta, j, 1), Idx(segTheta, j + 1, 1), Idx(segTheta - 1, j, 0));
            }
        return Finish(key, v, tris);
    }

    /// <summary>
    /// Hair or a beard hanging down: from the line <paramref name="top"/>(theta) on the head, straight down to
    /// <paramref name="bottomY"/> (head units), widening by <paramref name="flare"/>.
    /// </summary>
    public static Mesh Curtain(string key, float thetaMin, float thetaMax, Func<float, float> top, float radius, float bottomY,
        float flare, float thickness, int segTheta = 36, int segDown = 12)
    {
        if (cache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;
        var v = new List<Vector3>();
        var tris = new List<int>();
        int cols = segTheta + 1, rows = segDown + 1;
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < cols; i++)
            {
                float t = Mathf.Lerp(thetaMin, thetaMax, i / (float)segTheta);
                Vector3 start = Dir(t, top(t)) * radius;
                var flat = new Vector3(start.x, 0f, start.z);
                for (int j = 0; j < rows; j++)
                {
                    float k = j / (float)segDown;
                    Vector3 pos = new Vector3(start.x, Mathf.Lerp(start.y, bottomY, k), start.z) + flat.normalized * (flare * k);
                    // Thinner and narrower at the tips.
                    float th = thickness * Mathf.Lerp(1f, 0.35f, k);
                    if (layer == 1) pos -= (flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.back) * th;
                    v.Add(pos);
                }
            }
        int inner = cols * rows;
        int Idx(int i, int j, int l) => l * inner + i * rows + j;
        for (int i = 0; i < segTheta; i++)
            for (int j = 0; j < segDown; j++)
            {
                QuadAway(v, tris, Idx(i, j, 0), Idx(i + 1, j, 0), Idx(i, j + 1, 0), Idx(i + 1, j + 1, 0), Idx(i, j, 1));
                QuadAway(v, tris, Idx(i, j, 1), Idx(i + 1, j, 1), Idx(i, j + 1, 1), Idx(i + 1, j + 1, 1), Idx(i, j, 0));
            }
        for (int i = 0; i < segTheta; i++)
            Rim(v, tris, Idx(i, segDown, 0), Idx(i + 1, segDown, 0), Idx(i, segDown, 1), Idx(i + 1, segDown, 1), Idx(i, segDown - 1, 0));
        for (int j = 0; j < segDown; j++)
        {
            Rim(v, tris, Idx(0, j, 0), Idx(0, j + 1, 0), Idx(0, j, 1), Idx(0, j + 1, 1), Idx(1, j, 0));
            Rim(v, tris, Idx(segTheta, j, 0), Idx(segTheta, j + 1, 0), Idx(segTheta, j, 1), Idx(segTheta, j + 1, 1), Idx(segTheta - 1, j, 0));
        }
        return Finish(key, v, tris);
    }

    // ------------------------------------------------------------------ triangles facing the right way
    // Unity draws a triangle's front where cross(b - a, c - a) points: each triangle is turned to face its wanted side.
    private static void Tri(List<Vector3> v, List<int> tris, int a, int b, int c, Vector3 wanted)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, wanted) < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    // Outer quads face away from the centre, inner quads towards it.
    private static void Quad(List<Vector3> v, List<int> tris, int a, int b, int c, int d, bool outward)
    {
        Vector3 centre = (v[a] + v[b] + v[c] + v[d]) * 0.25f;
        Vector3 wanted = outward ? centre : -centre;
        Tri(v, tris, a, b, c, wanted);
        Tri(v, tris, b, d, c, wanted);
    }

    // Faces away from the opposite layer (curtains have no single centre).
    private static void QuadAway(List<Vector3> v, List<int> tris, int a, int b, int c, int d, int other)
    {
        Vector3 wanted = v[a] - v[other];
        if (wanted.sqrMagnitude < 1e-10f) wanted = v[a];
        Tri(v, tris, a, b, c, wanted);
        Tri(v, tris, b, d, c, wanted);
    }

    // A border strip, facing away from the shell (from the neighbouring row <paramref name="inside"/>).
    private static void Rim(List<Vector3> v, List<int> tris, int a, int b, int c, int d, int inside)
    {
        Vector3 wanted = (v[a] + v[b]) * 0.5f - v[inside];
        if (wanted.sqrMagnitude < 1e-10f) return;
        Tri(v, tris, a, b, c, wanted);
        Tri(v, tris, b, d, c, wanted);
    }

    private static Mesh Finish(string key, List<Vector3> v, List<int> tris)
    {
        var m = new Mesh { name = "Appearance " + key, hideFlags = HideFlags.DontSave };
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        cache[key] = m;
        return m;
    }

    // ------------------------------------------------------------------ the shapes
    /// <summary>Where hair starts on the head: high on the forehead, above the ears, low on the nape.</summary>
    public static float Hairline(float t, float frontPhi = 58f, float backPhi = 112f)
    {
        float a = Mathf.Abs(Mathf.DeltaAngle(0f, t));
        float line = a < 90f ? Mathf.Lerp(frontPhi, 100f, a / 90f) : Mathf.Lerp(100f, backPhi, (a - 90f) / 90f);
        line -= 14f * Mathf.Exp(-Mathf.Pow((a - 92f) / 13f, 2f)); // around the ears
        return line;
    }

    public static Mesh HairCap(string variant, float thickness, float lower = 0f, Func<float, float, float> bump = null) =>
        Shell("hair-" + variant, -180f, 180f, t => 0f, t => Hairline(t) + lower, 0.505f, thickness, bump);

    public static Mesh HairBack(string variant, float bottomY, float flare) =>
        Curtain("hairback-" + variant, 70f, 290f, t => Hairline(t) - 6f, 0.53f, bottomY, flare, 0.05f);

    /// <summary>Beard line on the face: under the nose in front, up to the sideburns.</summary>
    private static float BeardTop(float t) => Mathf.Lerp(110f, 80f, Mathf.Pow(Mathf.Clamp01(Mathf.Abs(t) / 100f), 1.4f));

    private static float BeardBottom(float t) => Mathf.Lerp(172f, 138f, Mathf.Pow(Mathf.Clamp01(Mathf.Abs(t) / 100f), 2f));

    public static Mesh Beard(string variant, float thickness, float chin) =>
        Shell("beard-" + variant, -100f, 100f, BeardTop, BeardBottom, 0.5f, thickness,
            (t, p) => chin * Smooth(135f, 172f, p) * Mathf.Cos(t * Mathf.Deg2Rad), 40, 16);

    public static Mesh Goatee() =>
        Shell("goatee", -32f, 32f, t => 126f + Mathf.Abs(t) * 0.25f, t => 176f - Mathf.Abs(t) * 0.4f, 0.5f, 0.055f,
            (t, p) => 0.035f * Smooth(140f, 172f, p), 16, 10);

    public static Mesh Moustache(bool thick) =>
        Shell(thick ? "moustache-thick" : "moustache", -44f, 44f, t => 103f + Mathf.Abs(t) * 0.12f, t => (thick ? 115f : 112f) + Mathf.Abs(t) * 0.18f,
            0.505f, thick ? 0.05f : 0.035f, null, 20, 4);

    public static Mesh BeardHang(string variant, float bottomY) =>
        Curtain("beardhang-" + variant, -55f, 55f, t => BeardBottom(t) - 8f, 0.5f, bottomY, 0.06f, 0.06f, 20, 10);

    /// <summary>
    /// The skin of the head (placeholder body): radius 0.49 (just under the hair and beard shells), narrowing to the jaw
    /// and a longer chin, fuller at the back of the skull.
    /// </summary>
    public static Mesh HeadSkin()
    {
        const string key = "head-skin";
        if (cache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;
        const int segT = 40, segP = 24;
        var v = new List<Vector3>();
        var tris = new List<int>();
        for (int j = 0; j <= segP; j++)
        {
            float p = 180f * j / segP;
            for (int i = 0; i < segT; i++)
            {
                float t = -180f + 360f * i / segT;
                Vector3 q = Dir(t, p) * 0.49f;
                float jaw = Smooth(100f, 178f, p);
                q.x *= Mathf.Lerp(0.95f, 0.7f, jaw);
                if (q.z < 0f) q.z *= Mathf.Lerp(1.04f, 0.85f, jaw);
                q.y -= 0.04f * jaw;
                v.Add(q);
            }
        }
        for (int j = 0; j < segP; j++)
            for (int i = 0; i < segT; i++)
            {
                int a = j * segT + i, b = j * segT + (i + 1) % segT, c = (j + 1) * segT + i, d = (j + 1) * segT + (i + 1) % segT;
                Quad(v, tris, a, b, c, d, true);
            }
        return Finish(key, v, tris);
    }

    /// <summary>One eyebrow (right side; the left one is mirrored by negative theta).</summary>
    public static Mesh Eyebrow(int style, bool left)
    {
        float a = 8f, b = 42f;
        Func<float, float> top, bottom;
        float th = 0.022f;
        switch (style)
        {
            case 1: top = t => 66f; bottom = t => 77f; th = 0.032f; break;                                  // thick
            case 2: top = t => 72f; bottom = t => 75f; th = 0.016f; break;                                  // thin
            case 3: top = t => 70f - 5f * Mathf.Sin(Mathf.PI * (Mathf.Abs(t) - a) / (b - a)); bottom = t => 75f - 4f * Mathf.Sin(Mathf.PI * (Mathf.Abs(t) - a) / (b - a)); break; // arched
            case 4: top = t => 72f; bottom = t => 77f; break;                                               // straight
            default: top = t => 70f - 2f * Mathf.Sin(Mathf.PI * (Mathf.Abs(t) - a) / (b - a)); bottom = t => 76f - Mathf.Abs(t) * 0.04f; break; // natural
        }
        return left
            ? Shell($"brow-{style}-l", -b, -a, top, bottom, 0.502f, th, null, 10, 3)
            : Shell($"brow-{style}-r", a, b, top, bottom, 0.502f, th, null, 10, 3);
    }
}
