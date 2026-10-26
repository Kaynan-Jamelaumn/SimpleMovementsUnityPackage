using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small software canvas: a pixel buffer with filled shapes, lines and blending, uploaded to a texture. The crafting
/// icons are drawn with it, and so is every work surface of the station window (the bar on the anvil, the bevel on the
/// wheel, the hide on the beam, the crucible, the fragments on the ore bench) - drawn from the piece's real state each
/// frame, so what is seen is what is there.
/// </summary>
public sealed class CraftPixels
{
    public readonly int Width, Height;
    private readonly Color32[] px;
    private Texture2D texture;

    public CraftPixels(int width, int height)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        px = new Color32[Width * Height];
    }

    public Color32[] Buffer => px;

    /// <summary>The texture the buffer is uploaded to (created on first use).</summary>
    public Texture2D Texture
    {
        get
        {
            if (texture == null)
            {
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                    name = "Craft Canvas",
                };
            }
            return texture;
        }
    }

    public void Apply()
    {
        Texture.SetPixels32(px);
        Texture.Apply(false, false);
    }

    public void Release()
    {
        if (texture != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
        texture = null;
    }

    // ------------------------------------------------------------------ pixels
    public void Clear(Color c)
    {
        Color32 c32 = c;
        for (int i = 0; i < px.Length; i++) px[i] = c32;
    }

    /// <summary>Blends <paramref name="c"/> (with its alpha × <paramref name="cover"/>) onto a pixel.</summary>
    private static Color32 Solid(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);

    public void Blend(int x, int y, Color c, float cover = 1f)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        float a = Mathf.Clamp01(c.a * cover);
        if (a <= 0f)
            return;
        int i = y * Width + x;
        if (a >= 0.998f)
        {
            px[i] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);
            return;
        }
        Color32 d = px[i];
        if (d.a == 255)
        {
            // Over an opaque pixel (nearly all of them): a plain mix.
            float ia = (1f - a) / 255f;
            px[i] = new Color32((byte)(Mathf.Clamp01(c.r * a + d.r * ia) * 255f), (byte)(Mathf.Clamp01(c.g * a + d.g * ia) * 255f), (byte)(Mathf.Clamp01(c.b * a + d.b * ia) * 255f), 255);
            return;
        }
        float da = d.a / 255f;
        float oa = a + da * (1f - a);
        if (oa <= 1e-5f)
            return;
        float r = (c.r * a + d.r / 255f * da * (1f - a)) / oa;
        float g = (c.g * a + d.g / 255f * da * (1f - a)) / oa;
        float b = (c.b * a + d.b / 255f * da * (1f - a)) / oa;
        px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(oa) * 255f));
    }

    /// <summary>Adds light (glow): additive, clamped.</summary>
    public void Glow(int x, int y, Color c, float amount)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || amount <= 0f)
            return;
        int i = y * Width + x;
        Color32 d = px[i];
        px[i] = new Color32((byte)Mathf.Min(255, d.r + c.r * amount * 255f), (byte)Mathf.Min(255, d.g + c.g * amount * 255f), (byte)Mathf.Min(255, d.b + c.b * amount * 255f),
            (byte)Mathf.Max(d.a, Mathf.Clamp01(amount) * 255f));
    }

    public Color Get(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? Color.clear : (Color)px[y * Width + x];

    // ------------------------------------------------------------------ shapes
    public void Rect(float x0, float y0, float x1, float y1, Color c)
    {
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(Width - 1, Mathf.CeilToInt(x1) - 1);
        int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(Height - 1, Mathf.CeilToInt(y1) - 1);
        bool opaque = c.a >= 0.998f;
        Color32 solid = Solid(c);
        for (int y = iy0; y <= iy1; y++)
        {
            float cy = Mathf.Clamp01(Mathf.Min(y + 1, y1) - Mathf.Max(y, y0));
            int row = y * Width;
            for (int x = ix0; x <= ix1; x++)
            {
                float cx = Mathf.Clamp01(Mathf.Min(x + 1, x1) - Mathf.Max(x, x0));
                if (opaque && cx * cy >= 0.998f) px[row + x] = solid; // the inside of an opaque rectangle: no blending
                else Blend(x, y, c, cx * cy);
            }
        }
    }

    public void Frame(float x0, float y0, float x1, float y1, Color c, float t = 1f)
    {
        Rect(x0, y0, x1, y0 + t, c);
        Rect(x0, y1 - t, x1, y1, c);
        Rect(x0, y0 + t, x0 + t, y1 - t, c);
        Rect(x1 - t, y0 + t, x1, y1 - t, c);
    }

    public void Disc(float cx, float cy, float r, Color c)
    {
        int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
        int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Width - 1, x1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float cover = Mathf.Clamp01(r - d + 0.5f);
                if (cover > 0f) Blend(x, y, c, cover);
            }
    }

    public void Ring(float cx, float cy, float r, float t, Color c)
    {
        int x0 = Mathf.FloorToInt(cx - r - t), x1 = Mathf.CeilToInt(cx + r + t);
        int y0 = Mathf.FloorToInt(cy - r - t), y1 = Mathf.CeilToInt(cy + r + t);
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Width - 1, x1); x++)
            {
                float d = Mathf.Abs(Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r);
                float cover = Mathf.Clamp01(t * 0.5f - d + 0.5f);
                if (cover > 0f) Blend(x, y, c, cover);
            }
    }

    /// <summary>A soft round light (additive).</summary>
    public void Halo(float cx, float cy, float r, Color c, float strength)
    {
        int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r);
        int y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Width - 1, x1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / r;
                if (d < 1f) Glow(x, y, c, strength * (1f - d) * (1f - d));
            }
    }

    public void Line(float ax, float ay, float bx, float by, Color c, float t = 1f)
    {
        float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - t), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + t);
        int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - t), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + t);
        // Only the pixels near the line on each row (a long diagonal's bounding box is mostly empty).
        float nx = len > 1e-5f ? -(by - ay) / len : 0f, ny = len > 1e-5f ? (bx - ax) / len : 1f, reach = t * 0.5f + 1f;
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, y1); y++)
        {
            int xa = Mathf.Max(0, x0), xb = Mathf.Min(Width - 1, x1);
            if (Mathf.Abs(nx) > 1e-3f)
            {
                float py = y + 0.5f - ay, u0 = (-reach - ny * py) / nx, u1 = (reach - ny * py) / nx;
                xa = Mathf.Max(xa, Mathf.FloorToInt(Mathf.Min(u0, u1) + ax - 0.5f) - 1);
                xb = Mathf.Min(xb, Mathf.CeilToInt(Mathf.Max(u0, u1) + ax - 0.5f) + 1);
            }
            for (int x = xa; x <= xb; x++)
            {
                float px0 = x + 0.5f - ax, py0 = y + 0.5f - ay;
                float u = len > 1e-5f ? Mathf.Clamp01((px0 * (bx - ax) + py0 * (by - ay)) / (len * len)) : 0f;
                float dx = px0 - u * (bx - ax), dy = py0 - u * (by - ay);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(t * 0.5f - d + 0.5f);
                if (cover > 0f) Blend(x, y, c, cover);
            }
        }
    }

    /// <summary>
    /// Fills a polygon (any shape, even-odd), anti-aliased: a scanline fill, two sub-rows per pixel row, the spans' ends
    /// covering their pixels by the fraction they reach into them. Cost grows with the rows and edges, not with
    /// rows × columns × edges.
    /// </summary>
    public void Polygon(IList<Vector2> pts, Color c)
    {
        if (pts == null || pts.Count < 3)
            return;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 p in pts)
        {
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
        if (y1 < y0)
            return;
        if (cover == null || cover.Length < Width) cover = new float[Width];
        int n = pts.Count;
        bool opaque = c.a >= 0.998f;
        Color32 solid = Solid(c);
        for (int y = y0; y <= y1; y++)
        {
            int lo = Width, hi = -1;
            for (int sub = 0; sub < 2; sub++)
            {
                float sy = y + 0.25f + 0.5f * sub;
                crossings.Clear();
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    Vector2 a = pts[i], b = pts[j];
                    if ((a.y > sy) != (b.y > sy))
                        crossings.Add(a.x + (b.x - a.x) * (sy - a.y) / (b.y - a.y));
                }
                crossings.Sort();
                for (int k = 0; k + 1 < crossings.Count; k += 2)
                {
                    float xa = Mathf.Max(0f, crossings[k]), xb = Mathf.Min(Width, crossings[k + 1]);
                    if (xb <= xa) continue;
                    int ia = (int)xa, ib = Mathf.Min(Width - 1, (int)xb);
                    lo = Mathf.Min(lo, ia);
                    hi = Mathf.Max(hi, ib);
                    if (ia == ib) { cover[ia] += 0.5f * (xb - xa); continue; }
                    cover[ia] += 0.5f * (ia + 1 - xa);
                    for (int x = ia + 1; x < ib; x++) cover[x] += 0.5f;
                    if (ib < Width) cover[ib] += 0.5f * (xb - ib);
                }
            }
            int row = y * Width;
            for (int x = lo; x <= hi; x++)
            {
                float k = cover[x];
                cover[x] = 0f;
                if (opaque && k >= 0.998f) px[row + x] = solid;
                else if (k > 0.001f) Blend(x, y, c, Mathf.Min(1f, k));
            }
        }
    }

    private float[] cover;
    private readonly List<float> crossings = new List<float>();

    /// <summary>A polygon's outline.</summary>
    public void Outline(IList<Vector2> pts, Color c, float t = 1f)
    {
        if (pts == null || pts.Count < 2)
            return;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            Line(pts[j].x, pts[j].y, pts[i].x, pts[i].y, c, t);
    }

    /// <summary>A sprite of the buffer (icons; the buffer is uploaded first).</summary>
    public Sprite ToSprite(string name)
    {
        Apply();
        Texture.name = name;
        Sprite s = Sprite.Create(Texture, new UnityEngine.Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        texture = null; // the sprite owns it now
        return s;
    }
}
