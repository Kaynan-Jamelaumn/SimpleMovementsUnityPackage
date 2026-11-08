using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a section's area is drawn.</summary>
public enum SoilSectionShape
{
    /// <summary>Any outline (points on the field's ground plane).</summary>
    Polygon,
    /// <summary>A rectangle (centre, size, angle).</summary>
    Rectangle,
    /// <summary>A circle (centre, radius).</summary>
    Circle,
    /// <summary>Only the cells painted with the section's brush.</summary>
    Painted,
}

/// <summary>
/// A part of a field whose soil differs: "the corner by the wall is sour and wet", "the strip along the path is stony",
/// "this bed was dug with compost". It has a shape (or painted cells, or both), soft edges, and the soil it changes
/// (only the properties ticked in <see cref="soil"/>). Later sections lie over earlier ones.
/// </summary>
[Serializable]
public class SoilSection
{
    public string name = "Section";
    /// <summary>Its id in the layout (the editor's brush and selection follow it through undo and reordering).</summary>
    [HideInInspector] public int uid;
    [Tooltip("Colour of the section in the editor.")]
    public Color color = new Color(0.9f, 0.6f, 0.2f, 1f);
    public bool enabled = true;
    public SoilSectionShape shape = SoilSectionShape.Rectangle;
    [Tooltip("Polygon outline, field-local metres (X, Z).")]
    public List<Vector2> points = new List<Vector2>();
    [Tooltip("Centre of the rectangle or circle, field-local metres (X, Z).")]
    public Vector2 center;
    public Vector2 size = new Vector2(2f, 2f);
    [Tooltip("Rotation of the rectangle, degrees.")]
    public float angle;
    [Min(0.05f)] public float radius = 1.5f;
    [Tooltip("Width of the soft edge where this soil blends into the soil around it, metres.")]
    [Min(0f)] public float feather = 0.3f;
    [Tooltip("What this section changes.")]
    public SoilOverride soil = new SoilOverride();

    [Header("Water")]
    [Tooltip("Irrigated: channels keep this part at a moisture.")]
    public bool irrigated;
    [Range(0.05f, 1f)] public float irrigationMoisture = 0.65f;
    public WaterSourceKind irrigationWater = WaterSourceKind.River;
    [Tooltip("Mulch lying here from the start (0-1).")]
    [Range(0f, 1f)] public float mulch;

    [TextArea(1, 4)] public string note = "";

    /// <summary>How much of this section's soil a point gets from its shape (0-1, soft edges); painted cells are separate.</summary>
    public float Weight(Vector2 p)
    {
        float d;
        switch (shape)
        {
            case SoilSectionShape.Polygon:
                if (points == null || points.Count < 3) return 0f;
                d = AgriMath.SignedDistance(points, p);
                break;
            case SoilSectionShape.Rectangle:
            {
                float a = -angle * Mathf.Deg2Rad;
                Vector2 q = p - center;
                q = new Vector2(q.x * Mathf.Cos(a) - q.y * Mathf.Sin(a), q.x * Mathf.Sin(a) + q.y * Mathf.Cos(a));
                Vector2 e = new Vector2(Mathf.Abs(q.x) - size.x * 0.5f, Mathf.Abs(q.y) - size.y * 0.5f);
                d = new Vector2(Mathf.Max(e.x, 0f), Mathf.Max(e.y, 0f)).magnitude + Mathf.Min(Mathf.Max(e.x, e.y), 0f);
                break;
            }
            case SoilSectionShape.Circle:
                d = (p - center).magnitude - radius;
                break;
            default:
                return 0f;
        }
        if (feather <= 1e-4f) return d <= 0f ? 1f : 0f;
        return 1f - AgriMath.Smooth(-feather * 0.5f, feather * 0.5f, d);
    }

    /// <summary>The outline of the shape (for drawing): the polygon, the rectangle's corners or a circle of points.</summary>
    public List<Vector2> Outline(int circleSegments = 40)
    {
        var o = new List<Vector2>();
        switch (shape)
        {
            case SoilSectionShape.Polygon:
                if (points != null) o.AddRange(points);
                break;
            case SoilSectionShape.Rectangle:
            {
                float a = angle * Mathf.Deg2Rad;
                Vector2 ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size.x * 0.5f, ay = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * size.y * 0.5f;
                o.Add(center - ax - ay);
                o.Add(center + ax - ay);
                o.Add(center + ax + ay);
                o.Add(center - ax + ay);
                break;
            }
            case SoilSectionShape.Circle:
                for (int i = 0; i < circleSegments; i++)
                {
                    float t = i * Mathf.PI * 2f / circleSegments;
                    o.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
                }
                break;
        }
        return o;
    }

    public SoilSection Clone()
    {
        var s = (SoilSection)MemberwiseClone();
        s.points = points != null ? new List<Vector2>(points) : new List<Vector2>();
        s.soil = soil != null ? soil.Clone() : new SoilOverride();
        return s;
    }
}

/// <summary>
/// Everything a designer says about a field's soil: its outline, the size of its cells, the base soil, the sections over
/// it, the cells painted by hand, and the natural patchiness. <see cref="Bake"/> turns it into a <see cref="SoilGrid"/>
/// (the same way every time: the editor's preview is exactly what the game starts with).
/// </summary>
[Serializable]
public class SoilLayout
{
    /// <summary>Painted value of a cell kept to the base soil (no section applies there).</summary>
    public const byte PaintBase = 255;
    public const int MaxSide = 1024;
    public const int MaxCells = 1 << 18;

    [Tooltip("The field's outline, field-local metres (X, Z).")]
    public List<Vector2> outline = new List<Vector2> { new Vector2(-4f, -3f), new Vector2(4f, -3f), new Vector2(4f, 3f), new Vector2(-4f, 3f) };
    [Tooltip("Size of a soil cell, metres. Smaller cells follow sections and roots more closely and cost more memory.")]
    [Range(0.1f, 2f)] public float cellSize = 0.25f;
    [Tooltip("The soil everywhere no section changes it.")]
    public SoilProfile baseSoil = SoilProfile.Preset(SoilType.OrganicRich);
    [Tooltip("Parts of the field with a different soil. Later sections lie over earlier ones.")]
    public List<SoilSection> sections = new List<SoilSection>();
    [Tooltip("Natural patchiness (0 = perfectly even): no soil is the same a metre further on.")]
    [Range(0f, 1f)] public float variation = 0.35f;
    [Tooltip("Size of the patches, metres.")]
    [Min(0.5f)] public float variationScale = 3f;
    public int seed = 12345;
    /// <summary>Where the patchiness is read, so moving the layout's origin (the inspector's Centre) does not move it in the world.</summary>
    [HideInInspector] public Vector2 patchOffset;

    // The painted cells: section index + 1 (0 = not painted, 255 = base soil), on the grid the layout had when painted.
    [HideInInspector] public byte[] painted = new byte[0];
    [HideInInspector] public int paintedWidth, paintedHeight;
    [HideInInspector] public Vector2 paintedOrigin;
    [HideInInspector] public float paintedCell;

    // ---------------------------------------------------------------- geometry
    public Rect Bounds()
    {
        if (outline == null || outline.Count == 0) return new Rect(0f, 0f, 1f, 1f);
        Vector2 min = outline[0], max = outline[0];
        foreach (Vector2 p in outline)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>The cell size actually used (grown when the field would have more than <see cref="MaxCells"/> cells).</summary>
    public float EffectiveCellSize()
    {
        Rect b = Bounds();
        float cell = Mathf.Clamp(cellSize, 0.1f, 2f);
        float area = Mathf.Max(0.01f, b.width * b.height);
        if (area / (cell * cell) > MaxCells) cell = Mathf.Ceil(Mathf.Sqrt(area / MaxCells) * 20f) / 20f;
        if (b.width / cell > MaxSide) cell = b.width / MaxSide;
        if (b.height / cell > MaxSide) cell = b.height / MaxSide;
        return cell;
    }

    public void Geometry(out int width, out int height, out float cell, out Vector2 origin)
    {
        Rect b = Bounds();
        cell = EffectiveCellSize();
        origin = new Vector2(Mathf.Floor(b.xMin / cell) * cell, Mathf.Floor(b.yMin / cell) * cell);
        width = Mathf.Clamp(Mathf.CeilToInt((b.xMax - origin.x) / cell - 1e-4f), 1, MaxSide);
        height = Mathf.Clamp(Mathf.CeilToInt((b.yMax - origin.y) / cell - 1e-4f), 1, MaxSide);
    }

    /// <summary>A new grid with this layout's geometry (cells inside the outline marked), not yet baked.</summary>
    public SoilGrid CreateGrid()
    {
        Geometry(out int w, out int h, out float cell, out Vector2 origin);
        var g = new SoilGrid(w, h, cell, origin);
        for (int i = 0; i < g.Count; i++)
            g.SetInside(i, AgriMath.InPolygon(outline, g.Center(i)));
        return g;
    }

    public float Area() => AgriMath.Area(outline);

    // ---------------------------------------------------------------- sections
    /// <summary>
    /// An id no section of this layout has: random, so an id is never handed out again (a removed or undone section's
    /// id stays dead, and a brush or selection holding it erases or selects nothing) nor shared with another field.
    /// </summary>
    public int NewSectionUid()
    {
        for (;;)
        {
            int id = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF;
            if (id != 0 && IndexOfUid(id) < 0) return id;
        }
    }

    /// <summary>The index of the section with <paramref name="uid"/>, or -1.</summary>
    public int IndexOfUid(int uid)
    {
        if (uid == 0 || sections == null) return -1;
        for (int i = 0; i < sections.Count; i++)
            if (sections[i] != null && sections[i].uid == uid) return i;
        return -1;
    }

    /// <summary>Gives every section a unique id (sections made by code, or copied). Returns whether one changed.</summary>
    public bool EnsureSectionUids()
    {
        if (sections == null) return false;
        var seen = new HashSet<int>();
        bool changed = false;
        foreach (SoilSection s in sections)
        {
            if (s == null) continue;
            if (s.uid > 0 && seen.Add(s.uid)) continue;
            int id;
            do id = NewSectionUid(); while (seen.Contains(id));
            s.uid = id;
            seen.Add(id);
            changed = true;
        }
        return changed;
    }

    // ---------------------------------------------------------------- moving the origin
    /// <summary>The offset, in whole cells, that puts the middle of the outline at the field's origin.</summary>
    public Vector2 CentreOffset()
    {
        Rect b = Bounds();
        float step = EffectiveCellSize();
        return new Vector2(Mathf.Round(b.center.x / step) * step, Mathf.Round(b.center.y / step) * step);
    }

    /// <summary>
    /// Moves everything - the outline, the sections, the paint and the patchiness - by -<paramref name="offset"/> in the
    /// field's space: with the object moved by +<paramref name="offset"/>, nothing moves in the world (with a whole-cell
    /// offset the baked soil is exactly the same).
    /// </summary>
    public void Shift(Vector2 offset)
    {
        for (int i = 0; i < outline.Count; i++) outline[i] -= offset;
        if (sections != null)
            foreach (SoilSection s in sections)
            {
                if (s == null) continue;
                s.center -= offset;
                if (s.points != null)
                    for (int i = 0; i < s.points.Count; i++) s.points[i] -= offset;
            }
        paintedOrigin -= offset;
        patchOffset += offset;
    }

    // ---------------------------------------------------------------- painting
    /// <summary>
    /// Makes the painted map match the current geometry, keeping what was painted (by position). Only painting calls it:
    /// baking and previews read the map through <see cref="PaintFor"/>, so trying another cell size or dragging the
    /// outline about never coarsens or loses paint.
    /// </summary>
    public void EnsurePaintMap()
    {
        Geometry(out int w, out int h, out float cell, out Vector2 origin);
        if (PaintFits(w, h, cell, origin))
            return;
        painted = Resample(w, h, cell, origin) ?? new byte[w * h];
        paintedWidth = w;
        paintedHeight = h;
        paintedOrigin = origin;
        paintedCell = cell;
    }

    /// <summary>
    /// The paint of a grid's cells: the stored map when it was painted on that geometry, else a copy taken by position
    /// (the stored map is left as it is). Null when nothing is painted.
    /// </summary>
    public byte[] PaintFor(SoilGrid g)
    {
        if (g == null) return null;
        return PaintFits(g.Width, g.Height, g.CellSize, g.Origin) ? painted : Resample(g.Width, g.Height, g.CellSize, g.Origin);
    }

    private bool HasPaint => painted != null && painted.Length > 0 && painted.Length == paintedWidth * paintedHeight && paintedCell > 0f;

    private bool PaintFits(int w, int h, float cell, Vector2 origin) =>
        HasPaint && painted.Length == w * h && paintedWidth == w && paintedHeight == h &&
        Mathf.Approximately(paintedCell, cell) && (paintedOrigin - origin).sqrMagnitude < 1e-8f;

    /// <summary>The stored paint taken by position onto another geometry (null when nothing is painted).</summary>
    private byte[] Resample(int w, int h, float cell, Vector2 origin)
    {
        if (!HasPaint) return null;
        var map = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = origin + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
                int ox = Mathf.FloorToInt((p.x - paintedOrigin.x) / paintedCell), oy = Mathf.FloorToInt((p.y - paintedOrigin.y) / paintedCell);
                if (ox >= 0 && oy >= 0 && ox < paintedWidth && oy < paintedHeight)
                    map[y * w + x] = painted[oy * paintedWidth + ox];
            }
        return map;
    }

    /// <summary>What a cell of the current geometry is painted with (0 = nothing).</summary>
    public byte PaintedAt(int index) => painted != null && index >= 0 && index < painted.Length && painted.Length == paintedWidth * paintedHeight ? painted[index] : (byte)0;

    /// <summary>Paints a disc (section index + 1, 255 for the base soil, 0 to erase). Returns whether a cell changed.</summary>
    public bool Paint(Vector2 local, float radius, byte value)
    {
        EnsurePaintMap();
        bool changed = false;
        float r2 = radius * radius;
        int rr = Mathf.CeilToInt(radius / paintedCell) + 1;
        int cx = Mathf.FloorToInt((local.x - paintedOrigin.x) / paintedCell), cy = Mathf.FloorToInt((local.y - paintedOrigin.y) / paintedCell);
        for (int y = cy - rr; y <= cy + rr; y++)
            for (int x = cx - rr; x <= cx + rr; x++)
            {
                if (x < 0 || y < 0 || x >= paintedWidth || y >= paintedHeight) continue;
                Vector2 c = paintedOrigin + new Vector2((x + 0.5f) * paintedCell, (y + 0.5f) * paintedCell);
                if ((c - local).sqrMagnitude > r2) continue;
                int i = y * paintedWidth + x;
                if (painted[i] == value) continue;
                painted[i] = value;
                changed = true;
            }
        return changed;
    }

    /// <summary>After removing section <paramref name="index"/>: its painted cells are cleared, later sections' move down.</summary>
    public void SectionRemoved(int index)
    {
        if (painted == null) return;
        int id = index + 1;
        for (int i = 0; i < painted.Length; i++)
        {
            byte v = painted[i];
            if (v == 0 || v == PaintBase) continue;
            if (v == id) painted[i] = 0;
            else if (v > id) painted[i] = (byte)(v - 1);
        }
    }

    /// <summary>After swapping sections <paramref name="a"/> and <paramref name="b"/>: their painted cells swap too.</summary>
    public void SectionsSwapped(int a, int b)
    {
        if (painted == null) return;
        byte ia = (byte)(a + 1), ib = (byte)(b + 1);
        for (int i = 0; i < painted.Length; i++)
        {
            if (painted[i] == ia) painted[i] = ib;
            else if (painted[i] == ib) painted[i] = ia;
        }
    }

    public void ClearPaint(int sectionIndex = -1)
    {
        if (painted == null) return;
        for (int i = 0; i < painted.Length; i++)
            if (sectionIndex < 0 || painted[i] == sectionIndex + 1)
                painted[i] = 0;
    }

    // ---------------------------------------------------------------- baking
    /// <summary>
    /// Fills a grid made by <see cref="CreateGrid"/>: for every cell inside the outline, the base soil, then each section
    /// in order blended in by its weight there (painted cells count fully, cells painted "base" take no section), then
    /// the natural patchiness. Also the irrigation and mulch of the sections, and each cell's slow reserve from its
    /// organic matter. The grid's clock starts at <paramref name="now"/>.
    /// </summary>
    public void Bake(SoilGrid g, double now = 0.0)
    {
        if (g == null) return;
        byte[] paintMap = PaintFor(g);
        float[] v = g.Values;
        var baseValues = new float[SoilChannels.Authored];
        for (int c = 0; c < SoilChannels.Authored; c++)
            baseValues[c] = baseSoil != null ? baseSoil.Get((SoilChannel)c) : 0f;
        SoilType baseType = baseSoil != null ? baseSoil.type : SoilType.OrganicRich;
        float varScale = Mathf.Max(0.5f, variationScale);
        int count = sections?.Count ?? 0;

        for (int i = 0; i < g.Count; i++)
        {
            int o = i * SoilGrid.Stride;
            Array.Clear(v, o, SoilGrid.Stride);
            g.Last[i] = now;
            g.Marks[i * 3] = g.RainTotal;
            g.Marks[i * 3 + 1] = g.EvapTotal;
            g.Marks[i * 3 + 2] = g.WarmTotal;
            g.Holes[i] = 0;
            g.IrrigationTarget[i] = 0;
            g.IrrigationWater[i] = 0;
            for (int k = 0; k < SoilGrid.PendingSlots; k++)
            {
                g.PendingItem[i * SoilGrid.PendingSlots + k] = 0;
                g.PendingAmount[i * SoilGrid.PendingSlots + k] = 0f;
            }
            if (!g.Inside[i]) continue;

            Vector2 p = g.Center(i);
            for (int c = 0; c < SoilChannels.Authored; c++)
                v[o + c] = baseValues[c];
            SoilType type = baseType;
            float mulch = 0f;
            byte paint = paintMap != null ? paintMap[i] : (byte)0;

            for (int s = 0; s < count; s++)
            {
                SoilSection sec = sections[s];
                if (sec == null || !sec.enabled) continue;
                float w = paint == PaintBase ? 0f : Mathf.Max(sec.Weight(p), paint == s + 1 ? 1f : 0f);
                if (w <= 0f) continue;
                SoilOverride so = sec.soil;
                if (so != null && so.values != null)
                {
                    for (int c = 0; c < SoilChannels.Authored; c++)
                        if (so.Overrides((SoilChannel)c))
                            v[o + c] += (so.values.Get((SoilChannel)c) - v[o + c]) * w;
                    if (so.overrideType && w >= 0.5f) type = so.type;
                }
                if (sec.irrigated && w >= 0.5f)
                {
                    g.IrrigationTarget[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(sec.irrigationMoisture * 255f), 1, 255);
                    g.IrrigationWater[i] = (byte)sec.irrigationWater;
                }
                if (sec.mulch > 0f) mulch += (sec.mulch - mulch) * w;
            }

            if (variation > 0f)
                Vary(v, o, (p + patchOffset) / varScale, variation, seed);

            for (int c = 0; c < SoilChannels.Authored; c++)
                v[o + c] = SoilChannels.Clamp((SoilChannel)c, v[o + c]);
            g.Types[i] = (byte)type;
            // The reserves organic matter keeps feeding while it rots (what it holds at the pace it rots; see SoilGrid.Advance).
            float om = SoilGrid.Labile(v[o + (int)SoilChannel.OrganicMatter]) * SoilGrid.RotFactor(v[o + (int)SoilChannel.Ph]);
            v[o + (int)SoilChannel.SlowNitrogen] = om * SoilGrid.SlowNitrogenPerOm;
            v[o + (int)SoilChannel.SlowPhosphorus] = om * SoilGrid.SlowPhosphorusPerOm;
            v[o + (int)SoilChannel.SlowPotassium] = om * SoilGrid.SlowPotassiumPerOm;
            v[o + (int)SoilChannel.Mulch] = AgriMath.Clamp01(mulch);
        }
        g.Version++;
    }

    /// <summary>The natural patchiness: each property drifts on its own pattern (not all together).</summary>
    private static void Vary(float[] v, int o, Vector2 q, float amount, int seed)
    {
        float N(int salt) => AgriMath.Patchy(seed + salt * 7919, q.x + salt * 3.17f, q.y - salt * 1.91f);
        v[o + (int)SoilChannel.Ph] += N(1) * 0.35f * amount;
        for (int c = (int)SoilChannel.Nitrogen; c <= (int)SoilChannel.Molybdenum; c++)
            v[o + c] *= 1f + N(2 + c) * 0.3f * amount;
        v[o + (int)SoilChannel.OrganicMatter] *= 1f + N(20) * 0.25f * amount;
        v[o + (int)SoilChannel.Salinity] *= 1f + N(21) * 0.3f * amount;
        v[o + (int)SoilChannel.WaterRetention] += N(22) * 0.06f * amount;
        v[o + (int)SoilChannel.Drainage] += N(23) * 0.06f * amount;
        v[o + (int)SoilChannel.Compaction] += N(24) * 0.08f * amount;
        v[o + (int)SoilChannel.Moisture] += N(25) * 0.08f * amount;
        v[o + (int)SoilChannel.MagicEssence] *= 1f + N(26) * 0.3f * amount;
        v[o + (int)SoilChannel.Pathogens] *= 1f + N(27) * 0.4f * amount;
        if (v[o + (int)SoilChannel.Stones] > 0.02f)
            v[o + (int)SoilChannel.Stones] += N(28) * 0.12f * amount;
    }

    /// <summary>A signature of everything that shapes the baked soil (a saved field from another layout is not reused).</summary>
    public int Hash()
    {
        unchecked
        {
            Geometry(out int w, out int h, out float cell, out Vector2 origin);
            int hsh = (int)AgriMath.Hash(w, h, Mathf.RoundToInt(cell * 1000f), seed);
            hsh = hsh * 31 + Mathf.RoundToInt(origin.x * 100f) * 7 + Mathf.RoundToInt(origin.y * 100f);
            if (outline != null)
                foreach (Vector2 p in outline)
                    hsh = hsh * 31 + Mathf.RoundToInt(p.x * 100f) * 3 + Mathf.RoundToInt(p.y * 100f);
            hsh = hsh * 31 + Mathf.RoundToInt(variation * 1000f) + Mathf.RoundToInt(variationScale * 100f);
            hsh = hsh * 31 + Mathf.RoundToInt(patchOffset.x * 100f) * 7 + Mathf.RoundToInt(patchOffset.y * 100f);
            if (baseSoil != null)
            {
                hsh = hsh * 31 + (int)baseSoil.type;
                for (int c = 0; c < SoilChannels.Authored; c++)
                    hsh = hsh * 31 + Mathf.RoundToInt(baseSoil.Get((SoilChannel)c) * 100f);
            }
            if (sections != null)
                foreach (SoilSection s in sections)
                {
                    if (s == null) continue;
                    hsh = hsh * 31 + (s.enabled ? 1 : 0) + (int)s.shape * 3 + Mathf.RoundToInt(s.feather * 100f);
                    hsh = hsh * 31 + Mathf.RoundToInt(s.center.x * 100f) + Mathf.RoundToInt(s.center.y * 1000f);
                    hsh = hsh * 31 + Mathf.RoundToInt(s.size.x * 100f) + Mathf.RoundToInt(s.size.y * 1000f) + Mathf.RoundToInt(s.angle * 10f) + Mathf.RoundToInt(s.radius * 100f);
                    if (s.points != null)
                        foreach (Vector2 p in s.points)
                            hsh = hsh * 31 + Mathf.RoundToInt(p.x * 100f) * 3 + Mathf.RoundToInt(p.y * 100f);
                    if (s.soil != null)
                    {
                        hsh = hsh * 31 + s.soil.mask + (s.soil.overrideType ? (int)s.soil.type + 1 : 0) * 977;
                        if (s.soil.values != null)
                            for (int c = 0; c < SoilChannels.Authored; c++)
                                if (s.soil.Overrides((SoilChannel)c))
                                    hsh = hsh * 31 + Mathf.RoundToInt(s.soil.values.Get((SoilChannel)c) * 100f);
                    }
                    hsh = hsh * 31 + (s.irrigated ? Mathf.RoundToInt(s.irrigationMoisture * 100f) + (int)s.irrigationWater * 1000 : 0) + Mathf.RoundToInt(s.mulch * 100f);
                }
            if (painted != null)
                for (int i = 0; i < painted.Length; i++)
                    if (painted[i] != 0) hsh = hsh * 31 + i * 7 + painted[i];
            return hsh;
        }
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (outline == null || outline.Count < 3) errors.Add("The outline needs at least 3 points.");
        else if (Area() < 0.25f) errors.Add("The outline is too small (under a quarter of a square metre).");
        Rect b = Bounds();
        float cell = EffectiveCellSize();
        if (cell > cellSize + 1e-3f) warnings.Add($"The field is large for its cell size: cells of {cell:0.##} m are used instead of {cellSize:0.##} m.");
        if (baseSoil == null) errors.Add("No base soil.");
        if (sections != null)
            for (int i = 0; i < sections.Count; i++)
            {
                SoilSection s = sections[i];
                if (s == null) { warnings.Add($"Section {i + 1} is empty."); continue; }
                string n = string.IsNullOrWhiteSpace(s.name) ? $"Section {i + 1}" : s.name;
                if (s.shape == SoilSectionShape.Polygon && (s.points == null || s.points.Count < 3)) errors.Add($"{n}: a polygon needs at least 3 points.");
                if (s.soil == null || (s.soil.IsEmpty && !s.irrigated && s.mulch <= 0f)) warnings.Add($"{n}: it changes nothing (tick the properties it sets).");
                if (s.shape != SoilSectionShape.Painted)
                {
                    List<Vector2> o = s.Outline(16);
                    bool any = false;
                    foreach (Vector2 p in o)
                        if (b.Contains(p)) { any = true; break; }
                    if (!any && !b.Contains(s.center)) warnings.Add($"{n}: it lies outside the field.");
                }
            }
        if (sections != null && sections.Count > 250) errors.Add("At most 250 sections.");
    }
}
