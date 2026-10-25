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
    public void Blend(int x, int y, Color c, float cover = 1f)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        float a = Mathf.Clamp01(c.a * cover);
        if (a <= 0f)
            return;
        int i = y * Width + x;
        Color32 d = px[i];
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
        for (int y = iy0; y <= iy1; y++)
        {
            float cy = Mathf.Clamp01(Mathf.Min(y + 1, y1) - Mathf.Max(y, y0));
            for (int x = ix0; x <= ix1; x++)
            {
                float cx = Mathf.Clamp01(Mathf.Min(x + 1, x1) - Mathf.Max(x, x0));
                Blend(x, y, c, cx * cy);
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
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Width - 1, x1); x++)
            {
                float px0 = x + 0.5f - ax, py0 = y + 0.5f - ay;
                float u = len > 1e-5f ? Mathf.Clamp01((px0 * (bx - ax) + py0 * (by - ay)) / (len * len)) : 0f;
                float dx = px0 - u * (bx - ax), dy = py0 - u * (by - ay);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(t * 0.5f - d + 0.5f);
                if (cover > 0f) Blend(x, y, c, cover);
            }
    }

    /// <summary>A filled polygon (even-odd, sampled at pixel centres with 2×2 supersampling for soft edges).</summary>
    public void Polygon(IList<Vector2> pts, Color c)
    {
        if (pts == null || pts.Count < 3)
            return;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (Vector2 p in pts)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int inside = 0;
                for (int s = 0; s < 4; s++)
                {
                    float sx = x + 0.25f + 0.5f * (s & 1), sy = y + 0.25f + 0.5f * (s >> 1);
                    if (Inside(pts, sx, sy)) inside++;
                }
                if (inside > 0) Blend(x, y, c, inside / 4f);
            }
    }

    private static bool Inside(IList<Vector2> pts, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            Vector2 a = pts[i], b = pts[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y + 1e-9f) + a.x)
                inside = !inside;
        }
        return inside;
    }

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
