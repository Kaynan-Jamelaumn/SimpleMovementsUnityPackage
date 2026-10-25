using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grinding, filing, descaling and polishing: choose the wheel, file, brush or grit and the side (a bevel at an angle,
/// the faces, the edge line, the spine, the tang, the eye), then drag along the piece where it should bite. Dwell
/// grinds deeper; pressing hard on the wheel heats the thin edge (watch the temper colours run) - dip it to cool. A
/// file skates on hardened steel. Coatings and finishes go on here too.
/// </summary>
public sealed class CraftGrindMode : CraftMode
{
    private TMP_Dropdown tool, side, coat;
    private Slider angle, pressure;
    private readonly List<CraftAbrasive> tools = new List<CraftAbrasive>();
    private readonly List<string> coats = new List<string>();
    private readonly CraftStroke stroke = new CraftStroke();
    private float x0, mm, sendTimer, dipTimer = -1;
    private static readonly CraftSurfaceSide[] Sides =
        { CraftSurfaceSide.LeftBevel, CraftSurfaceSide.RightBevel, CraftSurfaceSide.Faces, CraftSurfaceSide.EdgeLine, CraftSurfaceSide.Spine, CraftSurfaceSide.Tang, CraftSurfaceSide.Eye };
    private static readonly string[] SideNames = { "Left bevel", "Right bevel", "Faces", "Edge line", "Spine", "Tang", "Eye" };

    public override string Title => "Grind";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Grinder) || s.Has(CraftTools.Files) || s.Has(CraftTools.Polishing);
    public override string Hint => "Drag along the piece where the tool should work; dwell to take more off. Dip it when it gets hot.";

    public override void Build(RectTransform controls)
    {
        tools.Clear();
        var names = new List<string>();
        foreach (CraftAbrasive a in Rules.Abrasives)
        {
            bool ok = a.kind == CraftAbrasiveKind.Wheel ? Station.Has(CraftTools.Grinder) : a.kind == CraftAbrasiveKind.Polish ? Station.Has(CraftTools.Polishing)
                : a.kind == CraftAbrasiveKind.File || a.kind == CraftAbrasiveKind.Brush ? Station.Has(CraftTools.Files) : false;
            if (!ok) continue;
            tools.Add(a);
            names.Add(a.Name);
        }
        RectTransform r1 = Row(controls);
        tool = Drop(r1, names, 0, 200f);
        side = Drop(r1, SideNames, 0, 140f);
        angle = Slide(r1, "Angle °", 5f, 45f, 15f, true, 64f);
        RectTransform r2 = Row(controls);
        pressure = Slide(r2, "Pressure", 0.05f, 1f, 0.5f);
        Btn(r2, "Dip to cool", Dip, 120f);
        RectTransform r3 = Row(controls);
        coats.Clear();
        var coatNames = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Oil || s.kind == CraftSupplyKind.Wax || s.kind == CraftSupplyKind.Other) { coats.Add(s.id); coatNames.Add(s.Name); }
        coat = Drop(r3, coatNames.Count > 0 ? coatNames : new List<string> { "-" }, 0, 200f);
        Btn(r3, "Coat it", () => { if (coats.Count > 0) Send(new CraftRequest(CraftRequestKind.Coat, ui.SelectedId) { id = coats[coat.value] }); }, 100f);
        Btn(r3, "Etch it (acid vat)", () => ui.Message("Lower it into a vat of acid at the tannery to etch it."), 160f, UITheme.ButtonStyle.Ghost);
    }

    private void Dip()
    {
        CraftPiece p = Selected;
        if (p == null || !Station.Has(CraftTools.QuenchTank)) { ui.Message("There is no water here to dip it in."); return; }
        if (Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Quench, a = 1f }))
            dipTimer = 1.2f;
    }

    public override void Tick(float dt)
    {
        if (dipTimer > 0f)
        {
            dipTimer -= dt;
            if (dipTimer <= 0f)
                Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air });
        }
        if (stroke.Active && ui.PointerHeld)
        {
            sendTimer += dt;
            if (sendTimer >= 0.2f) Flush();
        }
    }

    /// <summary>Sends what the tool did since the last send: the span it covered, how long it dwelt on each segment.</summary>
    private void Flush()
    {
        CraftPiece p = Selected;
        float window = sendTimer;
        sendTimer = 0f;
        if (!IsMetal(p) || stroke.Samples < 1 || tools.Count == 0)
        {
            stroke.Clear();
            return;
        }
        stroke.Span(p.segments.Count, out int from, out int to);
        double moved = Math.Abs(stroke.To - stroke.From) * p.Length;
        double segLength = p.Length / Math.Max(1, p.segments.Count);
        double dwell = window * segLength / Math.Max(segLength, moved);
        CraftAbrasive a = tools[Mathf.Clamp(tool.value, 0, tools.Count - 1)];
        CraftSurfaceSide s = Sides[Mathf.Clamp(side.value, 0, Sides.Length - 1)];
        float press = (float)stroke.Pressure;
        var r = new CraftRequest(CraftRequestKind.Grind, p.id) { i0 = from, i1 = to, i2 = (int)s, id = a.id, a = (float)stroke.Angle, b = press, c = (float)dwell };
        switch (a.kind)
        {
            case CraftAbrasiveKind.File: r.kind = CraftRequestKind.File; break;
            case CraftAbrasiveKind.Brush: r = new CraftRequest(CraftRequestKind.Descale, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
            case CraftAbrasiveKind.Polish: r = new CraftRequest(CraftRequestKind.Polish, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
        }
        Send(r);
        stroke.Clear();
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        mm = Fit(p, 660, 170);
        float len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        CraftArt.DrawTop(c, Rules, p, new Rect(x0, 160, len, 220), mm);
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 40, len, 60), mm, 3f);
        // The wheel (or the file) where the pointer is.
        Vector2 h = ui.Hover;
        if (h.x >= x0 && h.x <= x0 + len)
        {
            bool wheel = tools.Count > 0 && tools[Mathf.Clamp(tool.value, 0, tools.Count - 1)].kind == CraftAbrasiveKind.Wheel;
            if (wheel) c.Ring(h.x, 120, 40, 6, new Color(0.55f, 0.52f, 0.48f, 0.8f));
            else c.Rect(h.x - 30, 140, h.x + 30, 150, new Color(0.5f, 0.5f, 0.55f, 0.8f));
            if (ui.PointerHeld && wheel && p.kind == CraftPieceKind.Metal)
                for (int i = 0; i < 12; i++)
                    c.Line(h.x, 150, h.x + 30 + i * 9, 150 - 20 + (i * 17 % 40), new Color(1f, 0.85f, 0.4f, 0.8f), 1f);
        }
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            c.Rect(0, 0, c.Width, c.Height, new Color(0f, 0f, 0f, 0.45f));
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        float len = (float)(p.Length / mm);
        double x = Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, len));
        // Pressing toward the piece's centre line presses harder.
        double press = pressure.value * (0.7 + 0.6 * Mathf.Clamp01(1f - Mathf.Abs(pos.y - 270f) / 130f));
        if (phase == CraftPointer.Down)
        {
            stroke.Clear();
            sendTimer = 0f;
        }
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
            stroke.Add(Time.unscaledTime, x, 0.5, angle.value, press, Hands);
        if (phase == CraftPointer.Up)
            Flush();
    }
}

/// <summary>
/// The whetstone: the edge seen from above (it glints where it is still dull - a keen edge shows no line of light)
/// and in section where the pointer is (the bevels, the micro-bevel, the burr). Hold one face to the stone and drag
/// strokes across it; the height of the pointer over the stone is the angle you hold - the steadier, the keener.
/// Strokes into the edge cut fast and raise a burr; light trailing strokes and the strop take it off. The sharpest
/// edge is not always the best: a very acute edge on soft metal rolls.
/// </summary>
public sealed class CraftSharpenMode : CraftMode
{
    private TMP_Dropdown stone;
    private Slider pressure;
    private Button leftButton, rightButton;
    private bool leftFace = true;
    private readonly List<CraftAbrasive> stones = new List<CraftAbrasive>();
    private readonly CraftStroke stroke = new CraftStroke();
    private Rect stoneArea;
    private float x0, mm;
    private int from = -1, to = -1, spanStart = -1;
    private TextMeshProUGUI angleText;

    public override string Title => "Sharpen";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Whetstone);
    public override string Hint => "Drag strokes across the stone: the pointer's height is the angle you hold. Work both faces; finish on a fine stone and the strop.";

    public override void Build(RectTransform controls)
    {
        stones.Clear();
        var names = new List<string>();
        foreach (CraftAbrasive a in Rules.AbrasivesOf(CraftAbrasiveKind.Whetstone)) { stones.Add(a); names.Add(a.Name); }
        RectTransform r1 = Row(controls);
        stone = Drop(r1, names, 0, 200f);
        leftButton = Btn(r1, "Left face", () => { leftFace = true; ShowFace(); }, 110f, UITheme.ButtonStyle.Tab);
        rightButton = Btn(r1, "Right face", () => { leftFace = false; ShowFace(); }, 110f, UITheme.ButtonStyle.Tab);
        pressure = Slide(r1, "Pressure", 0.05f, 1f, 0.45f, false, 70f);
        RectTransform r2 = Row(controls);
        Btn(r2, "Whole edge", () => { from = to = -1; }, 120f, UITheme.ButtonStyle.Ghost);
        angleText = Text(r2, "");
        ShowFace();
    }

    private void ShowFace()
    {
        UITheme.SetSelected(leftButton, leftFace);
        UITheme.SetSelected(rightButton, !leftFace);
    }

    private void EdgeSpan(CraftPiece p, out int a, out int b)
    {
        if (from >= 0) { a = Mathf.Min(from, to); b = Mathf.Max(from, to); return; }
        a = p.segments.Count;
        b = -1;
        for (int i = 0; i < p.segments.Count; i++)
            if (p.segments[i].Beveled) { a = Mathf.Min(a, i); b = Mathf.Max(b, i); }
        if (b < 0) { a = 0; b = p.segments.Count - 1; }
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        stoneArea = new Rect(260, 20, 440, 120);
        CraftAbrasive s = stones.Count > 0 ? stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)] : null;
        c.Rect(stoneArea.xMin, stoneArea.yMin, stoneArea.xMax, stoneArea.yMax, s != null && s.apex <= 1.5 ? new Color(0.45f, 0.3f, 0.18f) : Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.75f, 0.72f, 0.68f), s != null ? Mathf.Clamp01(1f - (float)s.finish / 20f) : 0.5f));
        if (!IsMetal(p))
            return;
        mm = Fit(p, 660, 120);
        float len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        CraftArt.DrawTop(c, Rules, p, new Rect(x0, 200, len, 150), mm);
        // Glints where the edge is dull: light comes back off a blunt edge.
        EdgeSpan(p, out int a, out int b);
        double pos = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            float sx = x0 + (float)((pos + g.length * 0.5) / mm);
            pos += g.length;
            if (i < a || i > b) continue;
            float width = (float)(g.width / mm);
            float top = 275 + width * 0.5f;
            if (g.apex > 25) c.Disc(sx, top, Mathf.Clamp((float)g.apex / 40f, 1f, 4f), new Color(1f, 1f, 1f, 0.8f));
            if (g.burr > 0.3) c.Line(sx - 2, top + 2, sx + 2, top + 4, new Color(0.85f, 0.85f, 0.9f, 0.9f), 1f);
        }
        // The section under the pointer.
        Vector2 h = ui.Hover;
        int seg = h.x >= x0 ? SegmentAtPixel(p, h.x, x0, mm) : (a + b) / 2;
        Section(c, p.segments[Mathf.Clamp(seg, 0, p.segments.Count - 1)], new Rect(20, 20, 220, 150));
        if (stoneArea.Contains(h))
        {
            float held = AngleAt(h.y);
            c.Line(h.x - 60 * Mathf.Cos(held * Mathf.Deg2Rad), h.y - 60 * Mathf.Sin(held * Mathf.Deg2Rad), h.x, h.y, Mark, 2f);
        }
    }

    /// <summary>A cross-section of the edge: the two bevels and the apex, magnified.</summary>
    private static void Section(CraftPixels c, MetalSegment s, Rect r)
    {
        c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0f, 0f, 0f, 0.4f));
        float scale = Mathf.Min(r.width / (float)Math.Max(1, Math.Max(s.bevelWidthL, s.bevelWidthR) * 1.6), r.height / (float)Math.Max(0.5, s.thickness * 1.4));
        float cx = r.xMax - 20, cy = r.center.y;
        float t = (float)s.thickness * scale;
        float wl = (float)s.bevelWidthL * scale, wr = (float)s.bevelWidthR * scale;
        float dl = (float)s.bevelDepthL * scale, dr = (float)s.bevelDepthR * scale;
        var pts = new List<Vector2>
        {
            new Vector2(r.xMin + 10, cy + t * 0.5f), new Vector2(cx - wl, cy + t * 0.5f), new Vector2(cx, cy + t * 0.5f - dl),
            new Vector2(cx, cy - t * 0.5f + dr), new Vector2(cx - wr, cy - t * 0.5f), new Vector2(r.xMin + 10, cy - t * 0.5f),
        };
        c.Polygon(pts, new Color(0.65f, 0.66f, 0.7f));
        c.Outline(pts, CraftArt.Outline, 1f);
        if (s.burr > 0.2) c.Line(cx, cy, cx + 6, cy + 4, new Color(0.9f, 0.9f, 0.95f), 1f);
    }

    private float AngleAt(float y) => Mathf.Lerp(5f, 35f, Mathf.Clamp01((y - stoneArea.yMin) / stoneArea.height));

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        if (pos.y > 190)
        {
            // Dragging on the blade picks the stretch of edge to work.
            int seg = SegmentAtPixel(p, pos.x, x0, mm);
            if (phase == CraftPointer.Down) { spanStart = seg; from = to = seg; }
            else if (phase == CraftPointer.Drag && spanStart >= 0) { from = spanStart; to = seg; }
            else if (phase == CraftPointer.Up) spanStart = -1;
            return;
        }
        if (!stoneArea.Contains(pos) && phase != CraftPointer.Up)
            return;
        if (phase == CraftPointer.Down)
            stroke.Clear();
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
        {
            // Pushing the blade away (to the right for the left face, to the left for the right) leads with the edge.
            float along = Mathf.Clamp01((pos.x - stoneArea.xMin) / stoneArea.width);
            stroke.Add(Time.unscaledTime, along, leftFace ? along : 1f - along, AngleAt(pos.y), pressure.value, Hands);
        }
        if (phase == CraftPointer.Up && stroke.Samples > 2 && stones.Count > 0)
        {
            EdgeSpan(p, out int a, out int b);
            CraftAbrasive s = stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)];
            Send(new CraftRequest(CraftRequestKind.Sharpen, p.id)
            {
                i0 = a, i1 = b, i2 = (int)(leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = s.id,
                a = (float)stroke.Angle, b = (float)stroke.Pressure, count = Math.Max(1, stroke.Passes), c = (float)stroke.Wobble, flag = stroke.EdgeLeading,
            });
            angleText.text = $"{stroke.Passes} stroke{(stroke.Passes == 1 ? "" : "s")} at about {stroke.Angle:0}°{(stroke.Wobble > 3 ? ", rocking" : stroke.Wobble > 1.2 ? ", a little unsteady" : ", steady")}" +
                             $"{(stroke.EdgeLeading ? ", edge leading" : ", edge trailing")}.";
            stroke.Clear();
        }
    }
}
