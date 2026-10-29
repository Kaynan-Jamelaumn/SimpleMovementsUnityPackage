using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How crafting things are drawn, from their real state: a metal piece from above (its width along its length, the
/// bevels catching the light, the glow of its heat, scale, polish, temper colours, cracks) and from the side (its
/// thickness), a hide cell by cell (flesh, hair, tanning, dye, scores, holes), ore fragments by size and richness,
/// wood, fasteners. The station window draws its work surfaces with these, and the items made at runtime get their
/// icons from them.
/// </summary>
public static class CraftArt
{
    private static readonly Dictionary<string, Sprite> stockIcons = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => stockIcons.Clear();

    public static readonly Color Outline = new Color(0.05f, 0.05f, 0.07f, 0.9f);
    public static readonly Color Scale = new Color(0.12f, 0.11f, 0.11f, 1f);
    public static readonly Color Flesh = new Color(0.86f, 0.62f, 0.56f, 1f);
    public static readonly Color Crack = new Color(0.02f, 0.02f, 0.02f, 1f);

    // ------------------------------------------------------------------ metal
    /// <summary>The colour of a metal surface: the metal, darkened by scale, brightened by polish, tinted by temper colours.</summary>
    public static Color MetalSurface(Color metal, MetalSegment s)
    {
        Color c = metal;
        float polish = Mathf.Clamp01((float)s.polish);
        c = Color.Lerp(c * 0.82f, Color.Lerp(c, Color.white, 0.35f), polish);
        c.a = 1f;
        Color film = CraftMetallurgy.TemperColor(s.temperFilm);
        if (film.a > 0f && polish > 0.3f)
            c = Color.Lerp(c, film, 0.6f * polish);
        float scale = Mathf.Clamp01((float)s.scale * 12f);
        c = Color.Lerp(c, Scale, scale);
        c.a = 1f;
        return c;
    }

    /// <summary>Glow colour and strength of a temperature (dark forges show it earlier).</summary>
    public static Color Heat(double celsius, out float strength)
    {
        Color h = CraftMetallurgy.HeatColor(celsius);
        strength = h.a;
        h.a = 1f;
        return h;
    }

    /// <summary>
    /// A metal piece seen from above in <paramref name="area"/> (pixels): length along x, width along y, each segment
    /// at its own width, the bevels shaded, heat glowing per segment, holes and cracks drawn. Returns the pixel x of
    /// each segment's start (for hit tests). <paramref name="flipped"/>: turned over (the edge on the other side).
    /// </summary>
    public static float[] DrawTop(CraftPixels c, CraftRules rules, CraftPiece p, Rect area, float mmPerPixel, Color? tint = null, bool flipped = false)
    {
        float sg = flipped ? -1f : 1f;
        int n = p.segments.Count;
        var starts = new float[n + 1];
        if (n == 0)
            return starts;
        Color metal = tint ?? MetalColor(rules, p);
        float x = area.xMin, cy = area.center.y;
        for (int i = 0; i < n; i++)
        {
            MetalSegment s = p.segments[i];
            float len = (float)s.length / mmPerPixel, w = (float)s.width / mmPerPixel;
            starts[i] = x;
            float bend = (float)(SumBend(p, i) / mmPerPixel);
            float top = cy + sg * (w * 0.5f + bend), bottom = cy + sg * (-w * 0.5f + bend);
            Color surface = MetalSurface(metal, s);
            if (p.kind == CraftPieceKind.Wood)
            {
                CraftWoodDefinition wd = rules.Wood(p.wood.wood);
                surface = wd != null ? wd.color : new Color(0.6f, 0.45f, 0.3f);
                if (p.wood.charred > 0.05) surface = Color.Lerp(surface, Color.black, (float)p.wood.charred);
            }
            c.Rect(x, bottom, x + len + 0.6f, top, surface);
            // Bevels: a lighter band along the edge(s) as wide as the bevel.
            if (s.Beveled && p.kind == CraftPieceKind.Metal)
            {
                float bw = (float)Math.Max(s.bevelWidthL, s.bevelWidthR) / mmPerPixel;
                Color bevel = Color.Lerp(surface, Color.white, 0.25f + 0.25f * (float)Math.Min(1, s.polish + 0.3));
                c.Rect(x, top - sg * bw, x + len + 0.6f, top, bevel);
                if (p.edges >= 2)
                    c.Rect(x, bottom, x + len + 0.6f, bottom + sg * bw, bevel);
                // A keen edge is a thin bright line; a blunt one shows a flat.
                float keen = (float)CraftEvaluator.Sharpness(s);
                c.Rect(x, top - sg * 1.2f, x + len + 0.6f, top, Color.Lerp(new Color(0.35f, 0.35f, 0.38f), Color.white, keen));
            }
            if (s.fuller > 0.05)
                c.Rect(x, cy + sg * (-w * 0.12f + bend), x + len + 0.6f, cy + sg * (w * 0.12f + bend), Color.Lerp(surface, Color.black, 0.25f));
            // Heat: edge and body glow on their own.
            Color hb = Heat(s.body.temp, out float gb), he = Heat(s.edge.temp, out float ge);
            if (gb > 0f)
                for (int yy = Mathf.FloorToInt(Mathf.Min(bottom, top)); yy <= Mathf.CeilToInt(Mathf.Max(bottom, top)); yy++)
                    for (int xx = Mathf.FloorToInt(x); xx <= Mathf.CeilToInt(x + len); xx++)
                        c.Blend(xx, yy, hb, gb);
            if (ge > gb + 0.05f)
                c.Rect(x, top - sg * Mathf.Max(1.5f, w * 0.2f), x + len + 0.6f, top, new Color(he.r, he.g, he.b, ge));
            if (s.hole > 0.5)
                c.Disc(x + len * 0.5f, cy + sg * bend, (float)s.hole * 0.5f / mmPerPixel, new Color(0.04f, 0.04f, 0.05f, 1f));
            if (s.pit > 0.15)
                for (int k = 0; k < 3; k++)
                    c.Disc(x + len * (0.2f + 0.3f * k), cy + sg * (bend + w * (0.25f - 0.2f * k)), 1f, Scale);
            x += len;
        }
        starts[n] = x;
        // Cracks (the ones that show), with the hairline ones only as faint marks.
        foreach (CraftDefect d in p.defects)
        {
            if (d.kind != CraftDefectKind.Crack && d.kind != CraftDefectKind.ColdShut && d.kind != CraftDefectKind.Delamination || d.at < 0 || d.at >= n) continue;
            if (!d.surface && d.severity < 0.4) continue;
            float mx = (starts[d.at] + starts[d.at + 1]) * 0.5f;
            MetalSegment s = p.segments[d.at];
            float w = (float)s.width / mmPerPixel, bend = (float)(SumBend(p, d.at) / mmPerPixel);
            float depth = Mathf.Clamp01((float)d.severity * 0.5f) * w;
            c.Line(mx, cy + sg * (w * 0.5f + bend), mx + 2f, cy + sg * (w * 0.5f - depth + bend), Crack, d.severity > 0.6 ? 1.6f : 1f);
        }
        // Outline.
        var outline = OutlineTop(p, area, mmPerPixel, sg);
        c.Outline(outline, Outline, 1f);
        return starts;
    }

    private static double SumBend(CraftPiece p, int upTo)
    {
        // The bend of each segment (degrees, shared by length) turns into a sideways offset along the piece.
        double offset = 0, angle = 0;
        for (int i = 0; i <= upTo && i < p.segments.Count; i++)
        {
            angle += p.segments[i].bend;
            offset += Math.Sin(angle * Math.PI / 180) * p.segments[i].length;
        }
        return offset;
    }

    private static List<Vector2> OutlineTop(CraftPiece p, Rect area, float mmPerPixel, float sg = 1f)
    {
        var top = new List<Vector2>();
        var bottom = new List<Vector2>();
        float x = area.xMin, cy = area.center.y;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float len = (float)s.length / mmPerPixel, w = (float)s.width / mmPerPixel, bend = (float)(SumBend(p, i) / mmPerPixel);
            top.Add(new Vector2(x, cy + sg * (w * 0.5f + bend)));
            top.Add(new Vector2(x + len, cy + sg * (w * 0.5f + bend)));
            bottom.Add(new Vector2(x, cy + sg * (-w * 0.5f + bend)));
            bottom.Add(new Vector2(x + len, cy + sg * (-w * 0.5f + bend)));
            x += len;
        }
        bottom.Reverse();
        top.AddRange(bottom);
        return top;
    }

    /// <summary>The piece seen from the side: its thickness along its length (the anvil view), with the glow of its heat.</summary>
    public static void DrawSide(CraftPixels c, CraftRules rules, CraftPiece p, Rect area, float mmPerPixel, float thicknessScale = 1f)
    {
        Color metal = MetalColor(rules, p);
        float x = area.xMin, baseY = area.yMin;
        foreach (MetalSegment s in p.segments)
        {
            float len = (float)s.length / mmPerPixel, t = Mathf.Max(1f, (float)s.thickness / mmPerPixel * thicknessScale);
            float dish = (float)s.dish / mmPerPixel;
            Color surface = MetalSurface(metal, s);
            c.Rect(x, baseY + dish, x + len + 0.6f, baseY + dish + t, surface);
            Color h = Heat(Math.Max(s.body.temp, s.edge.temp), out float g);
            if (g > 0f) c.Rect(x, baseY + dish, x + len + 0.6f, baseY + dish + t, new Color(h.r, h.g, h.b, g));
            x += len;
        }
    }

    // ------------------------------------------------------------------ the cross-section
    /// <summary>
    /// The scale (pixels per mm) that fits the section of <paramref name="s"/> - and the target section, when given -
    /// in <paramref name="r"/>.
    /// </summary>
    public static float SectionScale(MetalSegment s, Rect r, float targetWidth = 0f, float targetThickness = 0f)
    {
        float w = Mathf.Max((float)s.width, targetWidth, 4f), t = Mathf.Max((float)s.thickness, targetThickness, 1f);
        return Mathf.Max(0.5f, Mathf.Min((r.width - 24f) / w, (r.height - 24f) / t));
    }

    /// <summary>
    /// The cross-section of one segment, magnified into <paramref name="r"/> (y up): the width across (the spine on the
    /// left, the edge on the right), the thickness up, the left bevel on the top face and the right bevel underneath.
    /// <paramref name="flip"/> (-1..1, the cosine of a half turn) turns it over about its length - the anvil shows which
    /// face is up. The bevels, the edge's sharpness, a burr, clay and scale are drawn. Returns the centre point.
    /// </summary>
    public static Vector2 DrawSection(CraftPixels c, MetalSegment s, Rect r, Color metal, float scale, float flip = 1f, bool wood = false)
    {
        c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0f, 0f, 0f, 0.45f));
        c.Frame(r.xMin, r.yMin, r.xMax, r.yMax, new Color(1f, 1f, 1f, 0.12f), 1f);
        Vector2 o = r.center;
        float hw = (float)s.width * 0.5f * scale, ht = (float)s.thickness * 0.5f * scale;
        float bl = Mathf.Min((float)s.bevelWidthL * scale, hw * 1.9f), br = Mathf.Min((float)s.bevelWidthR * scale, hw * 1.9f);
        float top = ht - (float)s.bevelDepthL * scale, bottom = -ht + (float)s.bevelDepthR * scale;
        if (top < bottom + 0.5f) { float m = (top + bottom) * 0.5f; top = m + 0.25f; bottom = m - 0.25f; }
        // A half turn about the length: x and y both change sign as flip goes from 1 to -1 (the width foreshortens on the way).
        float fy = flip, fx = flip;
        Vector2 P(float x, float y) => new Vector2(o.x + x * fx, o.y + y * fy);
        var pts = new List<Vector2> { P(-hw, ht), P(hw - bl, ht), P(hw, top), P(hw, bottom), P(hw - br, -ht), P(-hw, -ht) };
        c.Polygon(pts, wood ? metal : Color.Lerp(metal, Color.white, 0.12f));
        if (!wood)
        {
            // The bevels catch the light.
            if (bl > 0.5f) c.Polygon(new List<Vector2> { P(hw - bl, ht), P(hw, top), P(hw - bl * 0.5f, (ht + top) * 0.5f) }, Color.Lerp(metal, Color.white, 0.45f));
            if (br > 0.5f) c.Polygon(new List<Vector2> { P(hw - br, -ht), P(hw, bottom), P(hw - br * 0.5f, (-ht + bottom) * 0.5f) }, Color.Lerp(metal, Color.white, 0.35f));
            if (s.scale > 0.05) c.Outline(pts, new Color(0.18f, 0.16f, 0.15f, Mathf.Clamp01((float)s.scale * 6f)), 2f);
            if (s.clay > 0.05)
            {
                float k = (float)s.clay * scale;
                c.Rect(P(-hw, ht).x - 1, Mathf.Min(P(-hw, ht).y, P(-hw, ht + k).y), P(hw - bl, ht).x + 1, Mathf.Max(P(-hw, ht).y, P(-hw, ht + k).y), new Color(0.62f, 0.52f, 0.4f, 0.9f));
            }
            // The apex: a keen edge is a fine bright point, a blunt one a flat; a burr curls over.
            float keen = (float)CraftEvaluator.Sharpness(s);
            Vector2 tip = P(hw, (top + bottom) * 0.5f);
            c.Disc(tip.x, tip.y, Mathf.Lerp(3f, 1f, keen), Color.Lerp(new Color(0.4f, 0.4f, 0.42f), Color.white, keen));
            if (s.burr > 0.2) c.Line(tip.x, tip.y, tip.x + 5f * fx, tip.y + 4f * fy * (s.honeL >= s.honeR ? -1f : 1f), new Color(0.92f, 0.92f, 0.98f), 1.5f);
        }
        c.Outline(pts, Outline, 1f);
        return o;
    }

    /// <summary>
    /// The target of segment <paramref name="i"/> from the part the piece is meant to be (its intent): the width,
    /// thickness and edge angle the design asks for there. False when the piece has no intent with a shape.
    /// </summary>
    public static bool TargetAt(CraftRules rules, CraftPiece p, int i, out float width, out float thickness, out float edgeAngle, out bool edged)
    {
        width = thickness = edgeAngle = 0f;
        edged = false;
        if (p == null || string.IsNullOrEmpty(p.intentDesign) || i < 0 || i >= p.segments.Count)
            return false;
        CraftComponentSpec spec = rules.Design(p.intentDesign)?.Component(p.intentRole);
        CraftShapeTarget shape = spec?.shape;
        if (shape == null || shape.profile == null || shape.profile.Count < 2)
            return false;
        float t = (float)p.PositionOf(i);
        shape.At(t, out width, out thickness);
        edged = shape.edge != CraftEdgeKind.None && t >= shape.edgeFrom && t <= shape.edgeTo;
        edgeAngle = shape.edgeAngle;
        return width > 0f && thickness > 0f;
    }

    /// <summary>The target section drawn as a dashed outline over the real one (same centre and scale).</summary>
    public static void DrawTargetSection(CraftPixels c, Vector2 o, float scale, float width, float thickness, float edgeAngle, bool edged, bool doubleEdged, float flip = 1f)
    {
        float hw = width * 0.5f * scale, ht = thickness * 0.5f * scale;
        float bevel = edged ? Mathf.Min(hw * 1.8f, ht / Mathf.Tan(Mathf.Max(3f, edgeAngle * 0.5f) * Mathf.Deg2Rad)) : 0f;
        Vector2 P(float x, float y) => new Vector2(o.x + x * flip, o.y + y * flip);
        var pts = new List<Vector2>();
        if (edged)
        {
            pts.Add(P(doubleEdged ? -hw + bevel : -hw, ht));
            pts.Add(P(hw - bevel, ht));
            pts.Add(P(hw, 0f));
            pts.Add(P(hw - bevel, -ht));
            pts.Add(P(doubleEdged ? -hw + bevel : -hw, -ht));
            if (doubleEdged) pts.Add(P(-hw, 0f));
        }
        else
        {
            pts.Add(P(-hw, ht)); pts.Add(P(hw, ht)); pts.Add(P(hw, -ht)); pts.Add(P(-hw, -ht));
        }
        for (int k = 0; k < pts.Count; k++)
            Dashed(c, pts[k], pts[(k + 1) % pts.Count], TargetColor);
    }

    /// <summary>A dashed line (the target shapes are drawn with it).</summary>
    public static void Dashed(CraftPixels c, Vector2 a, Vector2 b, Color col, float t = 1.5f, float dash = 6f)
    {
        int dashes = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / dash));
        for (int d = 0; d < dashes; d += 2)
            c.Line(Mathf.Lerp(a.x, b.x, d / (float)dashes), Mathf.Lerp(a.y, b.y, d / (float)dashes),
                Mathf.Lerp(a.x, b.x, Mathf.Min(1f, (d + 1) / (float)dashes)), Mathf.Lerp(a.y, b.y, Mathf.Min(1f, (d + 1) / (float)dashes)), col, t);
    }

    public static readonly Color TargetColor = new Color(0.45f, 1f, 0.55f, 0.9f);

    /// <summary>The design's shape for the piece (null without one), and how long it should be: the piece's own length
    /// when that is in the design's range, else the nearest end of the range.</summary>
    public static CraftShapeTarget TargetShape(CraftRules rules, CraftPiece p, out float length)
    {
        length = 0f;
        if (p == null || string.IsNullOrEmpty(p.intentDesign))
            return null;
        CraftShapeTarget shape = rules.Design(p.intentDesign)?.Component(p.intentRole)?.shape;
        if (shape == null || shape.profile == null || shape.profile.Count < 2)
            return null;
        length = Mathf.Clamp((float)p.Length, shape.lengthMin, Mathf.Max(shape.lengthMin, shape.lengthMax));
        return shape;
    }

    /// <summary>
    /// The design's shape dashed over the piece, from <paramref name="x0"/>: seen from above (its width, about
    /// <paramref name="centreY"/>) or from the side (its thickness, up from <paramref name="centreY"/>, scaled by
    /// <paramref name="thicknessScale"/>). Ticks under it mark the shortest and longest the design allows.
    /// </summary>
    public static void DrawTargetProfile(CraftPixels c, CraftRules rules, CraftPiece p, float x0, float centreY, float mmPerPixel, bool side, float thicknessScale = 1f)
    {
        CraftShapeTarget shape = TargetShape(rules, p, out float length);
        if (shape == null)
            return;
        const int n = 32;
        Vector2? lastA = null, lastB = null;
        float extent = 0f;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            shape.At(t, out float w, out float th);
            float x = x0 + t * length / mmPerPixel;
            Vector2 a, b;
            if (side) { a = new Vector2(x, centreY + th / mmPerPixel * thicknessScale); b = new Vector2(x, centreY); }
            else { a = new Vector2(x, centreY + w * 0.5f / mmPerPixel); b = new Vector2(x, centreY - w * 0.5f / mmPerPixel); }
            extent = Mathf.Max(extent, Mathf.Abs(a.y - centreY));
            if (lastA.HasValue)
            {
                Dashed(c, lastA.Value, a, TargetColor, 1.2f, 5f);
                if (!side) Dashed(c, lastB.Value, b, TargetColor, 1.2f, 5f);
            }
            if (i == n) Dashed(c, a, b, TargetColor, 1.2f, 4f);
            lastA = a;
            lastB = b;
        }
        float under = side ? centreY - 4f : centreY - extent - 6f;
        foreach (float l in new[] { shape.lengthMin, shape.lengthMax })
        {
            float x = x0 + l / mmPerPixel;
            c.Line(x, under, x, under - 6f, new Color(TargetColor.r, TargetColor.g, TargetColor.b, 0.6f), 1f);
        }
        c.Line(x0 + shape.lengthMin / mmPerPixel, under - 3f, x0 + shape.lengthMax / mmPerPixel, under - 3f, new Color(TargetColor.r, TargetColor.g, TargetColor.b, 0.35f), 1f);
    }

    public static Color MetalColor(CraftRules rules, CraftPiece p)
    {
        if (p.kind == CraftPieceKind.Wood)
        {
            CraftWoodDefinition w = rules.Wood(p.wood.wood);
            return w != null ? w.color : new Color(0.6f, 0.45f, 0.3f);
        }
        CraftAlloyProfile prof = p.Profile(rules);
        Color c = prof != null ? prof.Color : new Color(0.6f, 0.6f, 0.62f);
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------ hide
    /// <summary>The colour of one hide cell (flesh, hair, tanning, dye, moisture, damage).</summary>
    public static Color HideCell(CraftRules rules, HideState h, HideCell cell, bool fleshSide = false)
    {
        if (cell == null || !cell.present)
            return Color.clear;
        CraftHideDefinition d = rules.Hide(h.hide);
        Color skin = d != null ? d.color : new Color(0.62f, 0.5f, 0.42f);
        CraftTanningAgent a = rules.Agent(h.agent);
        Color tan = a != null ? a.color : new Color(0.55f, 0.36f, 0.22f);
        if (d != null && d.textile)
        {
            // Cloth: its own colour, dyed, darker wet, frayed and holed where damaged.
            Color cloth = skin;
            if (cell.dye > 0.01) cloth = Color.Lerp(cloth, new Color((float)cell.dyeR, (float)cell.dyeG, (float)cell.dyeB), Mathf.Clamp01((float)cell.dye));
            cloth *= 1f - 0.25f * Mathf.Clamp01((float)cell.moisture - 0.1f);
            cloth = Color.Lerp(cloth, new Color(0.15f, 0.12f, 0.1f), Mathf.Clamp01((float)cell.damage * 0.8f));
            cloth.a = 1f;
            return cloth;
        }
        if (fleshSide)
        {
            // The underside: pale, suede-like skin, pink and glistening where flesh and fat are still on it.
            Color under = Color.Lerp(Color.Lerp(skin, Color.white, 0.3f), tan, Mathf.Clamp01((float)cell.Tannage));
            under = Color.Lerp(under, Flesh, Mathf.Clamp01((float)cell.flesh * 1.3f));
            under *= 1f - 0.25f * Mathf.Clamp01((float)cell.moisture);
            under = Color.Lerp(under, new Color(0.18f, 0.12f, 0.1f), Mathf.Clamp01((float)cell.damage * 0.8f));
            if (cell.rot > 0.1) under = Color.Lerp(under, new Color(0.35f, 0.4f, 0.25f), Mathf.Clamp01((float)cell.rot));
            under.a = 1f;
            return under;
        }
        Color c = Color.Lerp(skin, tan, Mathf.Clamp01((float)cell.Tannage * 1.2f));
        c = Color.Lerp(c, Flesh, Mathf.Clamp01((float)cell.flesh * 0.8f));
        if (cell.dye > 0.01)
            c = Color.Lerp(c, new Color((float)cell.dyeR, (float)cell.dyeG, (float)cell.dyeB), Mathf.Clamp01((float)cell.dye));
        c = Color.Lerp(c, d != null ? d.hairColor : new Color(0.35f, 0.25f, 0.18f), Mathf.Clamp01((float)cell.hair * 0.9f));
        c *= 1f - 0.3f * Mathf.Clamp01((float)cell.moisture);
        c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.25f), Mathf.Clamp01((float)cell.smooth));
        c = Color.Lerp(c, new Color(0.18f, 0.12f, 0.1f), Mathf.Clamp01((float)cell.damage * 0.8f));
        if (cell.rot > 0.1) c = Color.Lerp(c, new Color(0.35f, 0.4f, 0.25f), Mathf.Clamp01((float)cell.rot));
        c.a = 1f;
        return c;
    }

    /// <summary>A hide cell by cell in <paramref name="area"/> (grain side up, or <paramref name="fleshSide"/> up); returns the cell size in pixels.</summary>
    public static float DrawHide(CraftPixels c, CraftRules rules, HideState h, Rect area, bool fleshSide = false)
    {
        bool textile = rules.Hide(h.hide)?.textile ?? false;
        if (h.width <= 0 || h.height <= 0)
            return 0f;
        float cell = Mathf.Min(area.width / h.width, area.height / h.height);
        float x0 = area.xMin + (area.width - cell * h.width) * 0.5f, y0 = area.yMin + (area.height - cell * h.height) * 0.5f;
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                HideCell hc = h.Cell(x, y);
                if (hc == null || !hc.present) continue;
                c.Rect(x0 + x * cell, y0 + y * cell, x0 + (x + 1) * cell + 0.5f, y0 + (y + 1) * cell + 0.5f, HideCell(rules, h, hc, fleshSide));
                if (hc.damage > 0.35)
                    c.Line(x0 + x * cell + 1, y0 + y * cell + 1, x0 + (x + 1) * cell - 1, y0 + (y + 1) * cell - 1, new Color(0.1f, 0.06f, 0.05f, 0.8f), 1f);
                if (textile)
                {
                    // The weave: the warp runs along the bolt's length (up the drawing), the weft across it.
                    Color thread = new Color(1f, 1f, 1f, 0.12f), weft = new Color(0f, 0f, 0f, 0.1f);
                    for (float k = 0.25f; k < 1f; k += 0.5f)
                    {
                        c.Line(x0 + (x + k) * cell, y0 + y * cell, x0 + (x + k) * cell, y0 + (y + 1) * cell, thread, 1f);
                        c.Line(x0 + x * cell, y0 + (y + k) * cell, x0 + (x + 1) * cell, y0 + (y + k) * cell, weft, 1f);
                    }
                }
                else if (fleshSide)
                {
                    // Fat left on it glistens.
                    if (hc.flesh > 0.35) c.Disc(x0 + (x + 0.35f) * cell, y0 + (y + 0.6f) * cell, Mathf.Max(1f, cell * 0.12f), new Color(1f, 0.92f, 0.85f, 0.7f));
                }
                else if (hc.hair > 0.3)
                    for (int k = 0; k < 3; k++)
                        c.Line(x0 + (x + 0.2f + 0.3f * k) * cell, y0 + y * cell + 1, x0 + (x + 0.3f + 0.3f * k) * cell, y0 + (y + 0.7f) * cell, new Color(0.15f, 0.1f, 0.07f, (float)hc.hair * 0.6f), 1f);
            }
        return cell;
    }

    /// <summary>Where the hide grid sits in <paramref name="area"/> (origin and cell size) - for turning a pointer into cells.</summary>
    public static void HideLayout(HideState h, Rect area, out Vector2 origin, out float cell)
    {
        cell = h.width > 0 && h.height > 0 ? Mathf.Min(area.width / h.width, area.height / h.height) : 1f;
        origin = new Vector2(area.xMin + (area.width - cell * h.width) * 0.5f, area.yMin + (area.height - cell * h.height) * 0.5f);
    }

    // ------------------------------------------------------------------ ore
    /// <summary>Ore fragments as stones sized by mass, coloured by how much ore they hold; returns their centres and radii.</summary>
    public static List<Vector3> DrawOre(CraftPixels c, CraftRules rules, CraftPiece p, Rect area)
    {
        var placed = new List<Vector3>();
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        Color ore = o != null ? o.color : new Color(0.55f, 0.3f, 0.25f);
        Color rock = o != null ? o.gangueColor : new Color(0.7f, 0.7f, 0.68f);
        double total = Math.Max(1, p.ore.Mass);
        int n = p.ore.fragments.Count;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n * area.width / Mathf.Max(1f, area.height))));
        int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
        float cw = area.width / cols, ch = area.height / rows;
        for (int i = 0; i < n; i++)
        {
            OreFragment f = p.ore.fragments[i];
            float share = Mathf.Sqrt((float)(f.mass / total));
            float r = Mathf.Clamp(Mathf.Min(cw, ch) * 0.48f * Mathf.Max(0.25f, share * Mathf.Sqrt(n)), 2f, Mathf.Min(cw, ch) * 0.48f);
            float cx = area.xMin + (i % cols + 0.5f) * cw + (CraftMath.Hash(i, 7) % 7 - 3) * 0.5f;
            float cy = area.yMin + (i / cols + 0.5f) * ch + (CraftMath.Hash(i, 9) % 7 - 3) * 0.5f;
            Color col = Color.Lerp(rock, ore, Mathf.Clamp01((float)f.mineral));
            col = Color.Lerp(col, new Color(0.45f, 0.35f, 0.25f), Mathf.Clamp01((float)f.dirt * 2f));
            if (p.ore.roasted > 0.3) col = Color.Lerp(col, new Color(0.35f, 0.2f, 0.15f), 0.3f);
            c.Disc(cx, cy, r, col);
            c.Ring(cx, cy, r, 1f, Outline);
            if (f.mineral > 0.5)
                c.Disc(cx - r * 0.3f, cy + r * 0.3f, r * 0.25f, Color.Lerp(ore, Color.white, 0.25f));
            placed.Add(new Vector3(cx, cy, r));
        }
        return placed;
    }

    // ------------------------------------------------------------------ icons
    private const int IconSize = 64;

    /// <summary>The icon of a worked piece (drawn from its state).</summary>
    public static Sprite PieceIcon(CraftRules rules, CraftPiece p, List<CraftPiece> parts = null)
    {
        var c = new CraftPixels(IconSize, IconSize);
        c.Clear(Color.clear);
        var area = new Rect(4, 4, IconSize - 8, IconSize - 8);
        switch (p.kind)
        {
            case CraftPieceKind.Ore:
                DrawOre(c, rules, p, area);
                break;
            case CraftPieceKind.Hide:
                DrawHide(c, rules, p.hide, area);
                break;
            case CraftPieceKind.Fasteners:
            {
                Color m = MetalColor(rules, p);
                int n = Mathf.Clamp(p.fasteners.count, 1, 9);
                for (int i = 0; i < n; i++)
                {
                    float x = 14 + (i % 3) * 18, y = 14 + (i / 3) * 18;
                    if (p.fasteners.kind == CraftFastenerKind.Buckle) { c.Ring(32, 32, 16, 5, m); c.Rect(30, 14, 34, 50, m); break; }
                    c.Disc(x, y, 6, m);
                    c.Ring(x, y, 6, 1, Outline);
                }
                break;
            }
            case CraftPieceKind.Fibre:
            {
                Color f = Color.Lerp(new Color(0.93f, 0.9f, 0.82f), new Color(0.6f, 0.52f, 0.4f), (float)(p.fibre.dirt * 0.8 + p.fibre.grease * 0.4));
                for (int i = 0; i < 7; i++)
                    c.Disc(18 + (i * 37 % 28), 18 + (i * 23 % 28), 10, Color.Lerp(f, Color.white, i * 0.04f));
                if (p.fibre.alignment > 0.5)
                    for (int i = 0; i < 6; i++) c.Line(10, 20 + i * 5, 54, 22 + i * 5, f * 0.85f, 1f);
                break;
            }
            case CraftPieceKind.Yarn:
            {
                Color y = Color.Lerp(new Color(0.9f, 0.86f, 0.76f), Color.white, (float)p.yarn.evenness * 0.3f);
                c.Ring(32, 32, 20, 9, y);
                for (int i = 0; i < 8; i++) c.Line(32 + 12 * Mathf.Cos(i * 0.785f), 32 + 12 * Mathf.Sin(i * 0.785f), 32 + 28 * Mathf.Cos(i * 0.785f + 0.3f), 32 + 28 * Mathf.Sin(i * 0.785f + 0.3f), y * 0.8f, 1f);
                break;
            }
            case CraftPieceKind.Gem:
                DrawGem(c, rules, p, area);
                break;
            case CraftPieceKind.Assembly:
            {
                CraftPiece main = parts != null && parts.Count > 0 ? parts[0] : null;
                if (parts != null)
                    foreach (CraftPiece q in parts)
                        if (q.kind == CraftPieceKind.Metal && (main == null || q.Mass(rules) > main.Mass(rules))) main = q;
                if (main != null) Diagonal(c, rules, main);
                c.Ring(52, 12, 6, 2, new Color(1f, 0.8f, 0.38f, 0.9f)); // unfinished mark
                break;
            }
            default:
                Diagonal(c, rules, p);
                break;
        }
        return c.ToSprite($"Craft Piece {p.id}");
    }

    /// <summary>A gemstone, from above: a rough crystal, or its cut outline with the facets' light and dark.</summary>
    public static void DrawGem(CraftPixels c, CraftRules rules, CraftPiece p, Rect area)
    {
        GemState g = p.gem;
        CraftGemDefinition d = rules.Gem(g.gem);
        Color col = d != null ? d.color : new Color(0.7f, 0.7f, 0.75f);
        col.a = 1f;
        float cx = area.center.x, cy = area.center.y, r = Mathf.Min(area.width, area.height) * 0.42f;
        if (g.stage < CraftGemStage.Faceted)
        {
            // the rough: its outline as sawn, and the window marked on it
            var pts = new List<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                double a = i / 24.0 * Math.PI * 2;
                double rr = 0.85 + 0.15 * CraftMath.Noise(g.seed, i, 41);
                double x = Math.Cos(a) * rr, y = Math.Sin(a) * rr;
                foreach (CraftGemPlane b in g.bounds)
                {
                    double over = b.nx * x + b.ny * y - b.offset;
                    if (over > 0) { x -= b.nx * over; y -= b.ny * over; }
                }
                pts.Add(new Vector2(cx + (float)x * r, cy + (float)y * r));
            }
            c.Polygon(pts, col * 0.75f);
            c.Disc(cx - r * 0.25f, cy + r * 0.25f, r * 0.25f, Color.Lerp(col, Color.white, 0.25f));
            if (g.oriented) c.Ring(cx + (float)g.windowX * r, cy + (float)g.windowY * r, (float)CraftLapidary.WindowRadius(g) * r, 1f, new Color(1f, 1f, 1f, 0.6f));
            return;
        }
        CraftGemCut cut = rules.Cut(g.cut);
        float ratio = cut != null ? Mathf.Max(1f, cut.lengthRatio) : 1f;
        bool cab = cut != null && cut.cabochon;
        var outline = new List<Vector2>();
        int n = cut != null && cut.id.Contains("emerald") ? 8 : 16;
        for (int i = 0; i < n; i++)
        {
            double a = (i + 0.5) / n * Math.PI * 2;
            outline.Add(new Vector2(cx + (float)Math.Cos(a) * r / ratio * 1.1f, cy + (float)Math.Sin(a) * r));
        }
        c.Polygon(outline, col * (cab ? 0.85f : 0.7f));
        if (cab)
        {
            c.Disc(cx - r * 0.2f, cy + r * 0.3f, r * 0.35f, Color.Lerp(col, Color.white, 0.35f * (float)g.polish));
            return;
        }
        // facets: alternating light and dark wedges, the table in the middle
        for (int i = 0; i < 8; i++)
        {
            double a0 = i / 8.0 * Math.PI * 2, a1 = (i + 1) / 8.0 * Math.PI * 2;
            var tri = new List<Vector2> { new Vector2(cx, cy), new Vector2(cx + (float)Math.Cos(a0) * r / ratio, cy + (float)Math.Sin(a0) * r), new Vector2(cx + (float)Math.Cos(a1) * r / ratio, cy + (float)Math.Sin(a1) * r) };
            c.Polygon(tri, i % 2 == 0 ? Color.Lerp(col, Color.white, 0.15f + 0.3f * (float)g.polish) : col * 0.6f);
        }
        c.Disc(cx, cy, r * 0.42f / Mathf.Sqrt(ratio), Color.Lerp(col, Color.white, 0.2f + 0.25f * (float)g.polish));
    }

    /// <summary>Draws a long piece corner to corner (rotated by drawing into a temporary canvas and resampling).</summary>
    private static void Diagonal(CraftPixels c, CraftRules rules, CraftPiece p)
    {
        double length = Math.Max(1, p.Length), width = 1;
        foreach (MetalSegment s in p.segments) width = Math.Max(width, s.width);
        bool longPiece = length > width * 2.2;
        int tw = 128, th = 128;
        var tmp = new CraftPixels(tw, th);
        tmp.Clear(Color.clear);
        float mmPerPixel = (float)Math.Max(length / (longPiece ? 170 : 110), width / (longPiece ? 40 : 110));
        float drawLen = (float)(length / mmPerPixel);
        DrawTop(tmp, rules, p, new Rect((tw - drawLen) * 0.5f, 0, drawLen, th), mmPerPixel);
        float angle = longPiece ? 45f * Mathf.Deg2Rad : 0f;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        float scale = tw / (float)IconSize;
        for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
            {
                float u = (x - IconSize * 0.5f) * scale, v = (y - IconSize * 0.5f) * scale;
                float sx = u * cos + v * sin + tw * 0.5f, sy = -u * sin + v * cos + th * 0.5f;
                Color col = tmp.Get(Mathf.FloorToInt(sx), Mathf.FloorToInt(sy));
                if (col.a > 0f) c.Blend(x, y, col);
            }
    }

    /// <summary>The icon of a finished item (its main part's colour on a silhouette of its kind).</summary>
    public static Sprite ItemIcon(CraftRules rules, CraftDesign d, CraftPiece main, Color color)
    {
        var c = new CraftPixels(IconSize, IconSize);
        c.Clear(Color.clear);
        if (main != null && (main.kind == CraftPieceKind.Metal || main.kind == CraftPieceKind.Wood) && d != null && d.IsWeaponLike)
        {
            Diagonal(c, rules, main);
            // The hilt or haft.
            c.Line(8, 8, 20, 20, new Color(0.45f, 0.3f, 0.18f, 1f), 5f);
            c.Line(14, 26, 26, 14, new Color(0.5f, 0.5f, 0.52f, 1f), 3f);
        }
        else if (main != null && main.kind == CraftPieceKind.Hide)
        {
            Color leather = HideCell(rules, main.hide, FirstCell(main.hide));
            Armour(c, d, leather);
        }
        else
            Armour(c, d, color);
        return c.ToSprite($"Crafted {d?.id}");
    }

    private static HideCell FirstCell(HideState h)
    {
        foreach (HideCell cell in h.cells)
            if (cell.present) return cell;
        return null;
    }

    private static void Armour(CraftPixels c, CraftDesign d, Color col)
    {
        col.a = 1f;
        var shape = new List<Vector2>();
        switch (d != null ? d.armorSlot : ArmorSlotType.Chestplate)
        {
            case ArmorSlotType.Helmet: c.Disc(32, 30, 20, col); c.Rect(12, 18, 52, 30, col); break;
            case ArmorSlotType.Shield: c.Disc(32, 32, 26, col); c.Disc(32, 32, 8, Color.Lerp(col, Color.white, 0.4f)); break;
            case ArmorSlotType.Belt: c.Rect(4, 26, 60, 38, col); c.Frame(26, 22, 38, 42, new Color(0.8f, 0.7f, 0.4f), 3); break;
            case ArmorSlotType.Boots: c.Rect(18, 14, 34, 50, col); c.Rect(18, 10, 50, 22, col); break;
            case ArmorSlotType.Gloves: c.Rect(18, 10, 44, 38, col); for (int i = 0; i < 4; i++) c.Rect(18 + i * 7, 38, 23 + i * 7, 54, col); break;
            case ArmorSlotType.Bracers: c.Rect(22, 8, 42, 56, col); break;
            default:
                shape.Add(new Vector2(14, 8)); shape.Add(new Vector2(50, 8)); shape.Add(new Vector2(54, 44)); shape.Add(new Vector2(44, 56));
                shape.Add(new Vector2(20, 56)); shape.Add(new Vector2(10, 44));
                c.Polygon(shape, col);
                break;
        }
    }

    /// <summary>A cached icon for a stock material of a kind and colour.</summary>
    public static Sprite StockIcon(CraftStockKind kind, Color color)
    {
        string key = kind + ColorUtility.ToHtmlStringRGB(color);
        if (stockIcons.TryGetValue(key, out Sprite s) && s != null)
            return s;
        var c = new CraftPixels(IconSize, IconSize);
        c.Clear(Color.clear);
        color.a = 1f;
        switch (kind)
        {
            case CraftStockKind.Metal:
                c.Polygon(new List<Vector2> { new Vector2(8, 20), new Vector2(48, 20), new Vector2(56, 30), new Vector2(16, 30) }, Color.Lerp(color, Color.white, 0.25f));
                c.Rect(8, 12, 48, 20, color);
                c.Polygon(new List<Vector2> { new Vector2(48, 12), new Vector2(56, 22), new Vector2(56, 30), new Vector2(48, 20) }, color * 0.8f);
                break;
            case CraftStockKind.Ore:
                c.Disc(28, 28, 18, color);
                c.Disc(40, 38, 12, Color.Lerp(color, Color.gray, 0.4f));
                c.Disc(22, 22, 5, Color.Lerp(color, Color.white, 0.3f));
                break;
            case CraftStockKind.Hide:
                c.Polygon(new List<Vector2> { new Vector2(10, 14), new Vector2(54, 12), new Vector2(58, 34), new Vector2(50, 54), new Vector2(14, 54), new Vector2(6, 34) }, color);
                break;
            case CraftStockKind.Wood:
                c.Rect(12, 26, 56, 38, color);
                c.Line(14, 30, 54, 31, color * 0.75f, 1f);
                c.Disc(12, 32, 6, Color.Lerp(color, Color.white, 0.2f));
                break;
            case CraftStockKind.Fasteners:
                for (int i = 0; i < 6; i++) c.Disc(16 + (i % 3) * 16, 22 + (i / 3) * 18, 5, color);
                break;
            case CraftStockKind.Fuel:
                for (int i = 0; i < 5; i++) c.Disc(18 + (i % 3) * 14, 22 + (i / 3) * 14, 8, color);
                break;
            case CraftStockKind.Agent:
                c.Rect(18, 10, 46, 46, color);
                c.Rect(24, 46, 40, 54, new Color(0.5f, 0.35f, 0.2f));
                break;
            case CraftStockKind.Log:
                c.Rect(10, 22, 50, 42, color);
                c.Disc(50, 32, 11, color * 0.8f);
                c.Disc(50, 32, 8, new Color(0.85f, 0.72f, 0.5f));
                c.Ring(50, 32, 4, 0.8f, new Color(0.6f, 0.45f, 0.3f));
                break;
            case CraftStockKind.Gem:
                c.Polygon(new List<Vector2> { new Vector2(32, 54), new Vector2(46, 40), new Vector2(42, 14), new Vector2(22, 14), new Vector2(18, 40) }, color * 0.85f);
                c.Polygon(new List<Vector2> { new Vector2(32, 54), new Vector2(46, 40), new Vector2(34, 30) }, Color.Lerp(color, Color.white, 0.35f));
                c.Polygon(new List<Vector2> { new Vector2(22, 14), new Vector2(34, 30), new Vector2(42, 14) }, color * 0.65f);
                break;
            default:
                c.Disc(32, 30, 18, color);
                break;
        }
        s = c.ToSprite("Craft Stock " + key);
        stockIcons[key] = s;
        return s;
    }
}
