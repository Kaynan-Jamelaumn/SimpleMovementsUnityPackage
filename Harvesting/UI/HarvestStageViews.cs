using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the harvesting minigames on the work surface (a <see cref="CraftPixels"/> buffer, origin bottom-left) from
/// their models' real state: the picture of the stage, what to do (the line to follow, the edge to pull, the marks to
/// hold, the arrow, the saw line, the piece to rock, what is left to scrape), how it is going (the cut so far, the
/// peeled hide, cut marks filling, the piece coming out) and a gauge on the right with the zone to stay in - green is
/// right, amber is careful, red is harm.
/// </summary>
public static class HarvestStageViews
{
    public static readonly Color Good = new Color(0.4f, 0.85f, 0.5f), Careful = new Color(1f, 0.75f, 0.3f), Harm = new Color(1f, 0.32f, 0.28f),
        Ink = new Color(0f, 0f, 0f, 0.6f), Paper = new Color(0.94f, 0.92f, 0.86f), Wound = new Color(0.45f, 0.06f, 0.07f), Steel = new Color(0.82f, 0.84f, 0.88f);

    /// <summary>The gauge strip on the right of the surface.</summary>
    private const float GaugeX = 606f, GaugeW = 22f, GaugeY = 40f, GaugeH = 300f;

    /// <summary>Where a path segment is split at its zones' ends (reused every frame).</summary>
    private static readonly List<float> Splits = new List<float>();

    /// <summary>Draws <paramref name="m"/> (its picture, its state, its gauge).</summary>
    public static void Draw(CraftPixels c, HarvestStageModel m, float time)
    {
        if (c == null || m == null)
            return;
        HarvestStageSpec s = m.Spec;
        HarvestPryModel pry = m as HarvestPryModel;
        // Pieces already worked free are gone from their places.
        DrawScene(c, s, pry != null ? pry.Piece?.unit ?? -1 : s.unit, pry != null ? (Func<int, bool>)(u => Freed(pry, u)) : null);
        switch (m)
        {
            case HarvestTraceModel t: Trace(c, t, time); break;
            case HarvestPeelModel p: Peel(c, p, time); break;
            case HarvestSteadyModel st: Steady(c, st, time); break;
            case HarvestPullModel pl: Pull(c, pl, time); break;
            case HarvestSawModel sw: Saw(c, sw, time); break;
            case HarvestPryModel pr: Pry(c, pr, time); break;
            case HarvestScrapeModel sc: Scrape(c, sc, time); break;
        }
        // Progress along the bottom.
        c.Rect(20f, 6f, 580f, 12f, new Color(0f, 0f, 0f, 0.55f));
        c.Rect(21f, 7f, 21f + 558f * Mathf.Clamp01((float)m.Progress), 11f, new Color(0.85f, 0.75f, 0.5f));
    }

    /// <summary>The stage's picture; a lit piece (<paramref name="litUnit"/>) is outlined, pieces <paramref name="gone"/> are not drawn.</summary>
    public static void DrawScene(CraftPixels c, HarvestStageSpec s, int litUnit = -1, Func<int, bool> gone = null)
    {
        foreach (HarvestSceneShape sh in s.scene)
        {
            if (sh.points == null || sh.points.Count < 2) continue;
            if (gone != null && sh.unit >= 0 && gone(sh.unit)) continue;
            Color fill = sh.fill, line = sh.line;
            if (sh.faint)
            {
                fill.a *= 0.7f;
                line.a *= 0.6f;
            }
            if (sh.closed && fill.a > 0.01f && sh.points.Count >= 3)
                c.Polygon(sh.points, fill);
            if (line.a > 0.01f && sh.width > 0f)
            {
                if (sh.closed) c.Outline(sh.points, line, sh.width);
                else
                    for (int i = 1; i < sh.points.Count; i++)
                        c.Line(sh.points[i - 1].x, sh.points[i - 1].y, sh.points[i].x, sh.points[i].y, line, sh.width);
            }
            if (litUnit >= 0 && sh.unit == litUnit)
                c.Outline(sh.points, new Color(1f, 0.92f, 0.55f, 0.95f), 2f);
        }
    }

    /// <summary>Has the piece <paramref name="unit"/> been worked free already?</summary>
    private static bool Freed(HarvestPryModel m, int unit)
    {
        for (int i = 0; i < m.Current && i < m.Spec.pieces.Count; i++)
            if (m.Spec.pieces[i].unit == unit) return true;
        return false;
    }

    // ------------------------------------------------------------------ helpers
    private static void Dashed(CraftPixels c, Vector2 a, Vector2 b, Color col, float t, float dash = 9f, float offset = 0f)
    {
        float len = (b - a).magnitude;
        if (len < 0.5f) return;
        Vector2 d = (b - a) / len;
        for (float s = -offset % (dash * 2f); s < len; s += dash * 2f)
        {
            float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + dash);
            if (s1 > s0) c.Line(a.x + d.x * s0, a.y + d.y * s0, a.x + d.x * s1, a.y + d.y * s1, col, t);
        }
    }

    private static void Polyline(CraftPixels c, IList<Vector2> pts, Color col, float t)
    {
        if (pts == null) return;
        for (int i = 1; i < pts.Count; i++)
            c.Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, col, t);
    }

    /// <summary>A knife tip at <paramref name="p"/> (a small blade leaning away).</summary>
    private static void Knife(CraftPixels c, Vector2 p, Color edge)
    {
        if (p.x < 0f) return;
        c.Polygon(new[] { p, p + new Vector2(10f, 26f), p + new Vector2(16f, 22f) }, Steel);
        c.Line(p.x + 13f, p.y + 24f, p.x + 22f, p.y + 44f, new Color(0.35f, 0.24f, 0.15f), 5f);
        c.Disc(p.x, p.y, 2.2f, edge);
    }

    /// <summary>
    /// A vertical gauge: its value, a band to stay in (<paramref name="bandColor"/>), an optional careful band above it
    /// (amber, up to <paramref name="carefulHigh"/>), harm above that.
    /// </summary>
    private static void Gauge(CraftPixels c, float value, float max, float bandLow, float bandHigh, Color bandColor, float x = GaugeX, float carefulHigh = 0f)
    {
        c.Rect(x - 2f, GaugeY - 2f, x + GaugeW + 2f, GaugeY + GaugeH + 2f, new Color(0f, 0f, 0f, 0.7f));
        c.Rect(x, GaugeY, x + GaugeW, GaugeY + GaugeH, new Color(0.14f, 0.13f, 0.13f));
        float Y(float v) => GaugeY + GaugeH * Mathf.Clamp01(v / Mathf.Max(0.0001f, max));
        if (bandHigh > bandLow)
        {
            c.Rect(x, Y(Mathf.Max(bandHigh, carefulHigh)), x + GaugeW, GaugeY + GaugeH, new Color(Harm.r, Harm.g, Harm.b, 0.35f));
            if (carefulHigh > bandHigh)
                c.Rect(x, Y(bandHigh), x + GaugeW, Y(carefulHigh), new Color(Careful.r, Careful.g, Careful.b, 0.7f));
            c.Rect(x, Y(bandLow), x + GaugeW, Y(bandHigh), new Color(bandColor.r, bandColor.g, bandColor.b, 0.85f));
        }
        float vy = Y(value);
        c.Rect(x - 6f, vy - 1.5f, x + GaugeW + 6f, vy + 1.5f, Color.white);
        c.Polygon(new[] { new Vector2(x - 12f, vy - 6f), new Vector2(x - 4f, vy), new Vector2(x - 12f, vy + 6f) }, Color.white);
    }

    /// <summary>A second, thinner gauge (strain, stress): fills red toward its top.</summary>
    private static void Meter(CraftPixels c, float value, Color col, float x)
    {
        c.Rect(x - 2f, GaugeY - 2f, x + 12f, GaugeY + GaugeH + 2f, new Color(0f, 0f, 0f, 0.7f));
        c.Rect(x, GaugeY, x + 10f, GaugeY + GaugeH, new Color(0.14f, 0.13f, 0.13f));
        c.Rect(x, GaugeY, x + 10f, GaugeY + GaugeH * Mathf.Clamp01(value), col);
    }

    private static Color ZoneColor(HarvestZoneKind k) => k == HarvestZoneKind.Gut ? Harm : k == HarvestZoneKind.Joint ? new Color(1f, 0.55f, 0.2f) : Careful;

    // ------------------------------------------------------------------ Trace
    private static void Trace(CraftPixels c, HarvestTraceModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        for (int pi = 0; pi < s.paths.Count; pi++)
        {
            HarvestPath p = s.paths[pi];
            bool current = pi == m.PathIndex && !m.Done;
            bool done = pi < m.PathIndex || m.Done;
            if (current)
            {
                // The margin: a faint band either side of the line.
                for (int i = 1; i < p.points.Count; i++)
                    c.Line(p.points[i - 1].x, p.points[i - 1].y, p.points[i].x, p.points[i].y, new Color(1f, 1f, 1f, 0.08f), s.tolerance * 2f);
            }
            if (!done)
            {
                // The line to follow, dashed; its zones in their colours (joints only for those who can feel them). A zone
                // often starts or ends part way along a segment: each piece of it is drawn in its own zone's colour.
                float len = p.Length, walked = 0f, phase = current ? time * 18f % 16f : 0f;
                for (int i = 1; i < p.points.Count; i++)
                {
                    Vector2 a = p.points[i - 1], b = p.points[i];
                    float seg = (b - a).magnitude;
                    Splits.Clear();
                    Splits.Add(0f);
                    Splits.Add(seg);
                    if (len > 0f && p.zones != null)
                        foreach (HarvestZone zone in p.zones)
                        {
                            float from = zone.from * len - walked, to = zone.to * len - walked;
                            if (from > 0f && from < seg) Splits.Add(from);
                            if (to > 0f && to < seg) Splits.Add(to);
                        }
                    Splits.Sort();
                    for (int k = 1; k < Splits.Count; k++)
                    {
                        float d0 = Splits[k - 1], d1 = Splits[k];
                        if (d1 - d0 < 0.01f) continue;
                        HarvestZone z = p.ZoneAt(len > 0f ? (walked + (d0 + d1) * 0.5f) / len : 0f);
                        bool show = z != null && (z.kind != HarvestZoneKind.Joint || s.guides);
                        Color col = show ? ZoneColor(z.kind) : (current ? Paper : new Color(Paper.r, Paper.g, Paper.b, 0.45f));
                        Dashed(c, a + (b - a) * (d0 / seg), a + (b - a) * (d1 / seg), col, current ? 2.5f : 1.5f, 8f, phase - walked - d0);
                    }
                    walked += seg;
                }
                // The end: a flag.
                Vector2 e = p.points[p.points.Count - 1];
                c.Line(e.x, e.y, e.x, e.y + 16f, Ink, 2f);
                c.Polygon(new[] { new Vector2(e.x, e.y + 16f), new Vector2(e.x + 11f, e.y + 12f), new Vector2(e.x, e.y + 8f) }, current ? Good : new Color(Good.r, Good.g, Good.b, 0.5f));
            }
            // What the knife cut.
            if (pi < m.Cuts.Count)
                Polyline(c, m.Cuts[pi], Wound, 3f);
            if (current)
            {
                Vector2 at = p.At(m.Reached);
                float pulse = 1f + 0.25f * Mathf.Sin(time * 6f);
                c.Disc(at.x, at.y, 6f * pulse, Good);
                c.Ring(at.x, at.y, 9f * pulse, 1.5f, new Color(1f, 1f, 1f, 0.7f));
            }
        }
        // The band is where the speed is right: green, amber where the line asks for care (it is lower there). Over the
        // gut a red strip beside the gauge says what is at stake.
        float limit = m.Limit;
        Gauge(c, m.Speed, s.speedLimit * 1.6f, 0f, limit, m.Zone != null ? Careful : Good);
        if (m.Zone != null && m.Zone.kind == HarvestZoneKind.Gut)
            c.Rect(GaugeX + GaugeW + 4f, GaugeY, GaugeX + GaugeW + 9f, GaugeY + GaugeH, new Color(Harm.r, Harm.g, Harm.b, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * 6f))));
        if (m.Holding || m.Tip.x >= 0f)
            Knife(c, m.Holding ? m.Tip : m.Pointer, m.Deviation > s.tolerance && m.Holding ? Harm : Good);
    }

    // ------------------------------------------------------------------ Peel
    private static void Peel(CraftPixels c, HarvestPeelModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        int w = Mathf.Max(1, s.gridW), h = Mathf.Max(1, s.gridH);
        float cw = s.area.width / w, ch = s.area.height / h;
        Color under = s.underColor, hide = s.workColor;
        // The flesh under the hide, where there is hide.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = y * w + x;
                byte kind = k < s.cells.Count ? s.cells[k] : (byte)0;
                if (kind == 0) continue;
                float x0 = s.area.x + x * cw, y0 = s.area.yMax - (y + 1) * ch;
                if (m.IsPeeled(x, y))
                {
                    // Off: the flesh shows; the hide is pulled back (drawn lighter at the edge it went to).
                    c.Rect(x0, y0, x0 + cw, y0 + ch, Color.Lerp(under, new Color(0.35f, 0.08f, 0.08f), 0.25f));
                    float harm = m.HarmAt(x, y);
                    if (harm > 0.05f)
                        c.Disc(x0 + cw * 0.5f, y0 + ch * 0.5f, Mathf.Min(cw, ch) * 0.35f * Mathf.Clamp01(harm * 1.5f), Harm);
                }
                else
                {
                    // Still on: the hide, flesh side up; holes from the fight dark.
                    Color col = kind == 3 ? new Color(0.12f, 0.06f, 0.05f) : Color.Lerp(hide, Paper, 0.25f);
                    c.Rect(x0 + 0.5f, y0 + 0.5f, x0 + cw - 0.5f, y0 + ch - 0.5f, col);
                    if (kind == 2) c.Frame(x0 + 1f, y0 + 1f, x0 + cw - 1f, y0 + ch - 1f, new Color(Careful.r, Careful.g, Careful.b, 0.8f), 1.5f);
                }
            }
        // The edges still to separate (the frontier), brightest on the row worked.
        for (int y = 0; y < h; y++)
            foreach (int side in new[] { -1, 1 })
            {
                int cell = m.FrontierCell(y, side);
                if (cell < 0) continue;
                int x = cell % w;
                float x0 = s.area.x + x * cw, y0 = s.area.yMax - (y + 1) * ch;
                float ex = side < 0 ? x0 : x0 + cw;
                bool active = m.Row == y && m.Side == side;
                c.Line(ex, y0, ex, y0 + ch, active ? Color.white : new Color(1f, 1f, 1f, 0.45f), active ? 3f : 1.5f);
            }
        // The pull: from the edge being worked to the hand.
        m.Band(out float low, out float high);
        if (m.Holding && m.Row >= 0)
        {
            int cell = m.FrontierCell(m.Row, m.Side);
            if (cell >= 0)
            {
                int x = cell % w;
                float edge = m.Side < 0 ? s.area.x + x * cw : s.area.x + (x + 1) * cw;
                float ey = s.area.yMax - (m.Row + 0.5f) * ch;
                Color col = m.Tension < low ? Paper : m.Tension <= high ? Good : Harm;
                c.Line(edge, ey, m.Pointer.x, m.Pointer.y, col, 3f);
                c.Disc(m.Pointer.x, m.Pointer.y, 5f, col);
            }
        }
        // The band on a fiddly cell is narrower: shown amber.
        bool fiddly = Math.Abs(low - s.fiddlyLow) < 1e-4f && Math.Abs(high - s.fiddlyHigh) < 1e-4f && s.fiddlyHigh - s.fiddlyLow < s.bandHigh - s.bandLow - 1e-4f;
        Gauge(c, m.Tension, 1.4f, low, high, fiddly ? Careful : Good);
    }

    // ------------------------------------------------------------------ Steady
    private static void Steady(CraftPixels c, HarvestSteadyModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        // What must not be cut: shown with the guides, or when the knife is close.
        for (int i = 0; i < s.hazards.Count; i++)
        {
            HarvestHazard z = s.hazards[i];
            bool near = false;
            if (m.Tip.x >= 0f)
                foreach (Vector2 p in z.shape)
                    if ((p - m.Tip).sqrMagnitude < 40f * 40f) { near = true; break; }
            if (!s.guides && !near && !m.InHazard) continue;
            Color col = z.gut ? Harm : Careful;
            c.Polygon(z.shape, new Color(col.r, col.g, col.b, z.gut ? 0.22f : 0.12f));
            c.Outline(z.shape, new Color(col.r, col.g, col.b, 0.75f), 1.5f);
        }
        for (int i = 0; i < s.targets.Count; i++)
        {
            HarvestTarget t = s.targets[i];
            float done = m.CutOf(i);
            bool active = m.Active == i;
            if (done >= 1f)
            {
                c.Line(t.at.x - 5f, t.at.y - 5f, t.at.x + 5f, t.at.y + 5f, Good, 2.5f);
                c.Line(t.at.x - 5f, t.at.y + 5f, t.at.x + 5f, t.at.y - 5f, Good, 2.5f);
                continue;
            }
            float pulse = active ? 1f : 1f + 0.15f * Mathf.Sin(time * 5f + i);
            c.Ring(t.at.x, t.at.y, t.radius * pulse, 2f, new Color(1f, 0.9f, 0.35f, active ? 1f : 0.8f));
            // How far it is cut: a filling disc.
            if (done > 0f)
                c.Disc(t.at.x, t.at.y, t.radius * Mathf.Sqrt(done) * 0.85f, new Color(1f, 0.85f, 0.3f, 0.75f));
            else
                c.Disc(t.at.x, t.at.y, 2f, new Color(1f, 0.9f, 0.35f));
        }
        if (m.Holding && m.Active >= 0)
        {
            // The hand's wander: from where it aims to where the tip really is.
            c.Line(m.Pointer.x, m.Pointer.y, m.Tip.x, m.Tip.y, new Color(1f, 1f, 1f, 0.35f), 1f);
            c.Ring(m.Pointer.x, m.Pointer.y, 3f, 1f, new Color(1f, 1f, 1f, 0.5f));
        }
        Knife(c, m.Holding ? m.Tip : m.Pointer, m.InHazard ? Harm : Good);
        float off = 0f;
        if (m.Holding && m.Active >= 0)
            off = (m.Tip - s.targets[m.Active].at).magnitude / Mathf.Max(1f, s.targets[m.Active].radius);
        // The gauge: how far off the mark the tip is (stay under the line).
        Gauge(c, off, 2.5f, 0f, 1f, Good);
    }

    // ------------------------------------------------------------------ Pull
    private static void Pull(CraftPixels c, HarvestPullModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        Vector2 a = s.pullFrom, b = s.pullTo, end = m.End;
        // The way out: an arrow; what has come out, solid.
        Dashed(c, a, b, new Color(Paper.r, Paper.g, Paper.b, 0.7f), 2f, 7f, time * 14f);
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        c.Polygon(new[] { b, b - d * 14f + n * 8f, b - d * 14f - n * 8f }, Paper);
        c.Line(a.x, a.y, end.x, end.y, Color.Lerp(s.workColor, Paper, 0.4f), 6f);
        float pulse = m.Holding ? 1f : 1f + 0.2f * Mathf.Sin(time * 5f);
        c.Ring(end.x, end.y, 11f * pulse, 2.5f, m.Holding ? Good : new Color(1f, 0.9f, 0.35f));
        if (m.Holding)
            c.Line(end.x, end.y, m.Pointer.x, m.Pointer.y, new Color(1f, 1f, 1f, 0.5f), 2f);
        m.Band(out float low, out float high);
        Gauge(c, m.Tension, 1.4f, low, high, Good);
        // A catch coming (felt with the technique or the guides): its band shown beside the gauge.
        float next = m.NextCatch;
        if (s.guides && next >= 0f && next - m.Progress < 0.12f)
        {
            m.BandAt(next, out float cl, out float chh);
            float y0 = GaugeY + GaugeH * Mathf.Clamp01(cl / 1.4f), y1 = GaugeY + GaugeH * Mathf.Clamp01(chh / 1.4f);
            c.Rect(GaugeX + GaugeW + 4f, y0, GaugeX + GaugeW + 9f, y1, new Color(Careful.r, Careful.g, Careful.b, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * 8f))));
        }
        Meter(c, m.Strain, Harm, GaugeX + GaugeW + 30f);
    }

    // ------------------------------------------------------------------ Saw
    private static void Saw(CraftPixels c, HarvestSawModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        if (s.paths.Count == 0 || s.paths[0].points.Count < 2)
            return;
        Vector2 a = s.paths[0].points[0], b = s.paths[0].points[s.paths[0].points.Count - 1];
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        // The margin, the line, and the kerf cut so far (deeper and darker as it goes through).
        c.Line(a.x, a.y, b.x, b.y, new Color(1f, 1f, 1f, 0.07f), s.tolerance * 2f);
        Dashed(c, a, b, Paper, 2f, 6f);
        float p = Mathf.Clamp01((float)m.Progress);
        if (p > 0f)
            c.Line(a.x, a.y, b.x, b.y, new Color(0.25f, 0.18f, 0.12f, 0.35f + 0.6f * p), 2f + 3f * p);
        // The beat: a dot swinging along the line at the right pace.
        double beat = m.Beat;
        float u = (float)(beat < 0.5 ? beat * 2 : 2 - beat * 2);
        Vector2 dot = Vector2.Lerp(a + d * 10f, b - d * 10f, u) + n * 18f;
        c.Disc(dot.x, dot.y, 5f, Careful);
        c.Line(a.x + n.x * 18f, a.y + n.y * 18f, b.x + n.x * 18f, b.y + n.y * 18f, new Color(Careful.r, Careful.g, Careful.b, 0.25f), 1f);
        // The saw at the hand.
        if (m.Pointer.x >= 0f)
        {
            Vector2 h = m.Pointer;
            c.Polygon(new[] { h - d * 45f + n * 4f, h + d * 45f + n * 4f, h + d * 45f - n * 7f, h - d * 45f - n * 7f }, new Color(Steel.r, Steel.g, Steel.b, m.Holding ? 0.95f : 0.5f));
            for (int i = -6; i <= 6; i++)
            {
                Vector2 t = h + d * (i * 7f) - n * 7f;
                c.Polygon(new[] { t, t + d * 3.5f - n * 4f, t + d * 7f }, new Color(Steel.r, Steel.g, Steel.b, m.Holding ? 0.95f : 0.5f));
            }
            c.Rect(h.x + d.x * 45f - 6f, h.y + d.y * 45f - 6f, h.x + d.x * 45f + 6f, h.y + d.y * 45f + 6f, new Color(0.4f, 0.27f, 0.16f));
        }
        Gauge(c, p, 1f, 0f, 0f, Good);
        c.Rect(GaugeX, GaugeY, GaugeX + GaugeW, GaugeY + GaugeH * p, new Color(Good.r, Good.g, Good.b, 0.8f));
    }

    // ------------------------------------------------------------------ Pry
    private static void Pry(CraftPixels c, HarvestPryModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        for (int i = 0; i < s.pieces.Count; i++)
        {
            HarvestPieceSpot p = s.pieces[i];
            if (i < m.Current)
            {
                // Free: gone from its place.
                c.Disc(p.at.x, p.at.y, 6f, new Color(0.2f, 0.1f, 0.08f, 0.8f));
                continue;
            }
            if (i == m.Current)
            {
                float pulse = m.Holding ? 1f : 1f + 0.2f * Mathf.Sin(time * 5f);
                c.Ring(p.at.x, p.at.y, 14f * pulse, 2f, new Color(1f, 0.9f, 0.35f));
            }
        }
        HarvestPieceSpot cur = m.Piece;
        if (cur == null)
            return;
        // The rocking guide under the piece: the swing marks (green within, red beyond).
        float y = cur.at.y - 24f;
        float inner = s.tolerance, outer = s.speedLimit;
        c.Line(cur.at.x - outer - 10f, y, cur.at.x + outer + 10f, y, new Color(1f, 1f, 1f, 0.25f), 1.5f);
        foreach (float sgn in new[] { -1f, 1f })
        {
            c.Rect(cur.at.x + sgn * inner - 1.5f, y - 7f, cur.at.x + sgn * inner + 1.5f, y + 7f, Good);
            c.Rect(cur.at.x + sgn * outer - 1.5f, y - 9f, cur.at.x + sgn * outer + 1.5f, y + 9f, Harm);
        }
        float off = Mathf.Clamp(m.Offset, -outer * 1.4f, outer * 1.4f);
        Color hand = Mathf.Abs(off) > outer ? Harm : Mathf.Abs(off) > inner ? Good : Paper;
        c.Disc(cur.at.x + off, y, 5f, hand);
        if (m.Holding)
            c.Line(cur.at.x, cur.at.y, cur.at.x + off, cur.at.y + 26f, new Color(Steel.r, Steel.g, Steel.b, 0.9f), 4f);
        Gauge(c, m.Looseness, 1f, 0f, 0f, Good);
        c.Rect(GaugeX, GaugeY, GaugeX + GaugeW, GaugeY + GaugeH * Mathf.Clamp01(m.Looseness), new Color(Good.r, Good.g, Good.b, 0.8f));
        Meter(c, m.Stress, Harm, GaugeX + GaugeW + 30f);
    }

    // ------------------------------------------------------------------ Scrape
    private static void Scrape(CraftPixels c, HarvestScrapeModel m, float time)
    {
        HarvestStageSpec s = m.Spec;
        int w = Mathf.Max(1, s.gridW), h = Mathf.Max(1, s.gridH);
        float cw = s.area.width / w, ch = s.area.height / h;
        Color tissue = s.tissueColor;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float left = m.TissueAt(x, y);
                if (left < 0f) continue;
                float x0 = s.area.x + x * cw, y0 = s.area.yMax - (y + 1) * ch;
                if (left > 0.05f)
                    c.Rect(x0, y0, x0 + cw, y0 + ch, new Color(tissue.r, tissue.g, tissue.b, Mathf.Clamp01(0.25f + 0.75f * left)));
            }
        if (m.Pointer.x >= 0f)
        {
            Vector2 at = m.Holding && m.Tip.x >= 0f ? m.Tip : m.Pointer;
            float sp = m.Speed;
            Color col = !m.Holding ? Paper : sp > HarvestScrapeModel.BreakSpeed ? Harm :
                sp < HarvestScrapeModel.PressSpeed || sp > HarvestScrapeModel.SkimSpeed ? Careful : Good;
            c.Ring(at.x, at.y, 16f, 2f, col);
        }
        // Green: brisk strokes; amber: so fast they skim; red: so frantic they break it.
        Gauge(c, Mathf.Min(m.Speed, 600f), 600f, HarvestScrapeModel.FullSpeed, HarvestScrapeModel.SkimSpeed, Good, GaugeX, HarvestScrapeModel.BreakSpeed);
        Meter(c, m.Coverage, Good, GaugeX + GaugeW + 30f);
    }
}
