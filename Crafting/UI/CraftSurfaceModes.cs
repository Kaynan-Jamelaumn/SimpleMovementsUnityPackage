using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grinding, filing, descaling and polishing. The blade lies under the wheel seen from above; above it, magnified, the
/// cross-section where the pointer is - what the metal really looks like there, the design's target shape as a dashed
/// green outline, and the gold line where the wheel bites for the side chosen. Pick a side (the pictures on the right,
/// or the list), hold on the blade where it should be ground and stay to take more off: the section changes as you
/// work, what you just ground shines, and the strip along the blade says what is still to do (yellow), done (green) or
/// gone too far (red). Pressing hard on the wheel heats the thin edge - dip it before the temper runs. A file skates on
/// hardened steel. Coatings go on here too.
/// </summary>
public sealed class CraftGrindMode : CraftMode
{
    private TMP_Dropdown tool, side, coat;
    private Slider angle, pressure;
    private TextMeshProUGUI info;
    private readonly List<CraftAbrasive> tools = new List<CraftAbrasive>();
    private readonly List<string> coats = new List<string>();
    private readonly CraftStroke stroke = new CraftStroke();
    private float x0, mm, len, sendTimer, dipTimer = -1;
    private float[] fresh = new float[0];
    private int hoverSeg;
    private Rect topView, sectionBox;
    private readonly Rect[] sideIcons = new Rect[7];
    private static readonly CraftSurfaceSide[] Sides =
        { CraftSurfaceSide.LeftBevel, CraftSurfaceSide.RightBevel, CraftSurfaceSide.Faces, CraftSurfaceSide.EdgeLine, CraftSurfaceSide.Spine, CraftSurfaceSide.Tang, CraftSurfaceSide.Eye };
    private static readonly string[] SideNames = { "Left bevel", "Right bevel", "Faces", "Edge line", "Spine", "Tang", "Eye" };
    private static readonly string[] SideHelp =
    {
        "the top face along the edge, held at the angle: the first bevel",
        "the face underneath along the edge: the second bevel (turn it to the wheel the other way)",
        "both broad faces, flat on the wheel: the blade gets thinner and cleaner",
        "straight into the edge: blunts it to a flat line - takes out chips, or takes the edge back",
        "the back of the blade: it gets narrower",
        "the tang: narrower and thinner, to fit the grip",
        "inside the eye or slot: only a file reaches there",
    };

    public override string Title => "Grind";
    public override CraftDiscipline Discipline => Selected != null && Selected.kind == CraftPieceKind.Wood ? CraftDiscipline.Woodwork : CraftDiscipline.Smithing;
    // Metal surface work: a wheel, polishing, or files where metal is worked (not the files of a leather bench).
    public override bool Available(CraftStation s) =>
        s.Has(CraftTools.Grinder) || s.Has(CraftTools.Polishing) || s.Has(CraftTools.Files) && (s.Has(CraftTools.Anvil) || s.Has(CraftTools.Forge));
    public override string Hint => "Pick a side (pictures on the right), hold on the blade where the wheel should bite - the magnified section shows what it does. Dip it when it gets hot." + ByHandHint("hold it to the wheel by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Grind;

    private CraftSurfaceSide CurrentSide => Sides[Mathf.Clamp(side != null ? side.value : 0, 0, Sides.Length - 1)];
    private CraftAbrasive CurrentTool => tools.Count > 0 ? tools[Mathf.Clamp(tool.value, 0, tools.Count - 1)] : null;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.abrasive = CurrentTool?.id;
        setup.side = CurrentSide;
        setup.angle = angle.value;
        setup.pressure = pressure.value;
    }

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
        HandsOnButton(r2, "At the wheel by hand", 190f);
        RectTransform r3 = Row(controls);
        coats.Clear();
        var coatNames = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Oil || s.kind == CraftSupplyKind.Wax || s.kind == CraftSupplyKind.Other) { coats.Add(s.id); coatNames.Add(s.Name); }
        coat = Drop(r3, coatNames.Count > 0 ? coatNames : new List<string> { "-" }, 0, 200f);
        Btn(r3, "Coat it", () => { if (coats.Count > 0) Send(new CraftRequest(CraftRequestKind.Coat, ui.SelectedId) { id = coats[coat.value] }); }, 100f);
        Btn(r3, "Etch it (acid vat)", () => ui.Message("Lower it into a vat of acid at the tannery to etch it."), 160f, UITheme.ButtonStyle.Ghost);
        RectTransform r4 = Row(controls, 52f);
        info = Text(r4, "", 13f);
        InventoryUIFactory.Size(info, -1f, 52f, 1f);
        FitInside(info);
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
        for (int i = 0; i < fresh.Length; i++)
            fresh[i] = Mathf.Max(0f, fresh[i] - dt * 0.7f);
    }

    /// <summary>Sends what the tool did since the last send: the span it covered, how long it dwelt on each segment.</summary>
    private void Flush()
    {
        CraftPiece p = Selected;
        float window = sendTimer;
        sendTimer = 0f;
        CraftAbrasive a = CurrentTool;
        if (!IsMetal(p) || stroke.Samples < 1 || a == null)
        {
            stroke.Clear();
            return;
        }
        stroke.Span(p.segments.Count, out int from, out int to);
        double moved = Math.Abs(stroke.To - stroke.From) * p.Length;
        double segLength = p.Length / Math.Max(1, p.segments.Count);
        double dwell = window * segLength / Math.Max(segLength, moved);
        float press = (float)stroke.Pressure;
        var r = new CraftRequest(CraftRequestKind.Grind, p.id) { i0 = from, i1 = to, i2 = (int)CurrentSide, id = a.id, a = (float)stroke.Angle, b = press, c = (float)dwell };
        switch (a.kind)
        {
            case CraftAbrasiveKind.File: r.kind = CraftRequestKind.File; break;
            case CraftAbrasiveKind.Brush: r = new CraftRequest(CraftRequestKind.Descale, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
            case CraftAbrasiveKind.Polish: r = new CraftRequest(CraftRequestKind.Polish, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
        }
        if (Send(r))
        {
            if (fresh.Length != p.segments.Count) fresh = new float[p.segments.Count];
            for (int i = Mathf.Max(0, from); i <= Mathf.Min(fresh.Length - 1, to); i++) fresh[i] = 1f;
        }
        stroke.Clear();
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        sectionBox = new Rect(12, 234, 360, 158);
        DrawSideIcons(c);
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        if (fresh.Length != p.segments.Count) fresh = new float[p.segments.Count];
        mm = Fit(p, 660, 140);
        len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        topView = new Rect(x0, 26, len, 150);
        float[] starts = CraftArt.DrawTop(c, Rules, p, topView, mm);
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 200, len, 26), mm, 3f);
        float cy = topView.center.y;
        // What was just ground shines; the target width is dotted in green; the strip says what is left to do.
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float a = starts[i], b = starts[i + 1], w = (float)s.width / mm;
            if (fresh[i] > 0.01f)
                c.Rect(a, cy - w * 0.5f, b, cy + w * 0.5f, new Color(1f, 0.97f, 0.85f, 0.45f * fresh[i]));
            bool target = CraftArt.TargetAt(Rules, p, i, out float tw, out float tt, out float ta, out bool edged);
            if (target)
            {
                float hw = tw * 0.5f / mm;
                if (i % 2 == 0) { c.Rect(a, cy + hw - 0.6f, b, cy + hw + 0.6f, new Color(0.45f, 1f, 0.55f, 0.8f)); c.Rect(a, cy - hw - 0.6f, b, cy - hw + 0.6f, new Color(0.45f, 1f, 0.55f, 0.8f)); }
            }
            c.Rect(a + 0.5f, 182, b - 0.5f, 192, StateColor(p, i, s, target, tw, tt, ta, edged));
        }
        // The segment under the pointer: its section, the target, where the wheel bites.
        Vector2 h = ui.Hover;
        if (h.x >= x0 && h.x <= x0 + len && h.y >= 20 && h.y <= 230)
            hoverSeg = Mathf.Clamp(SegmentAtPixel(p, h.x, x0, mm), 0, p.segments.Count - 1);
        hoverSeg = Mathf.Clamp(hoverSeg, 0, p.segments.Count - 1);
        c.Frame(starts[hoverSeg], topView.yMin - 2, starts[hoverSeg + 1], topView.yMax + 2, new Color(1f, 1f, 1f, 0.35f), 1f);
        MetalSegment seg = p.segments[hoverSeg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, hoverSeg, out float ttw, out float ttt, out float tta, out bool tedged);
        float scale = CraftArt.SectionScale(seg, sectionBox, hasTarget ? ttw : 0f, hasTarget ? ttt : 0f);
        Vector2 o = CraftArt.DrawSection(c, seg, sectionBox, CraftArt.MetalColor(Rules, p), scale, 1f, p.kind == CraftPieceKind.Wood);
        if (hasTarget) CraftArt.DrawTargetSection(c, o, scale, ttw, ttt, tta, tedged, p.edges >= 2);
        DrawContact(c, o, scale, seg, CurrentSide, angle.value, ui.PointerHeld && stroke.Active);
        // The wheel (or the file) where the pointer is, with sparks while it bites.
        if (h.x >= x0 && h.x <= x0 + len && topView.Contains(h))
        {
            bool wheel = CurrentTool != null && CurrentTool.kind == CraftAbrasiveKind.Wheel;
            if (wheel) c.Ring(h.x, cy + 70, 30, 5, new Color(0.62f, 0.6f, 0.56f, 0.85f));
            else c.Rect(h.x - 30, cy + 50, h.x + 30, cy + 58, new Color(0.5f, 0.5f, 0.55f, 0.85f));
            if (ui.PointerHeld && wheel && p.kind == CraftPieceKind.Metal)
                for (int i = 0; i < 14; i++)
                {
                    float t = Time.unscaledTime * 9f + i * 1.7f;
                    c.Line(h.x, cy + 40, h.x + 40 + (i * 13 % 60), cy + 20 + Mathf.Sin(t) * 30f, new Color(1f, 0.85f, 0.4f, 0.75f), 1f);
                }
        }
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            c.Rect(0, 0, c.Width, c.Height, new Color(0f, 0f, 0f, 0.45f));
    }

    /// <summary>The strip along the blade: grey no target, yellow still to grind (brighter = closer), green done, red too far.</summary>
    private static Color StateColor(CraftPiece p, int i, MetalSegment s, bool target, float tw, float tt, float ta, bool edged)
    {
        if (!target)
            return s.Beveled ? new Color(0.75f, 0.75f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.33f, 0.8f);
        if (s.width < tw * 0.88f || s.thickness < tt * 0.85f)
            return new Color(0.95f, 0.35f, 0.3f, 0.95f); // ground away more than the design has
        if (!edged)
        {
            float off = Mathf.Clamp01(Mathf.Max((float)(s.width - tw) / Mathf.Max(5f, tw), (float)(s.thickness - tt) / Mathf.Max(1f, tt)) * 4f);
            return off < 0.15f ? new Color(0.45f, 0.95f, 0.5f, 0.95f) : Color.Lerp(new Color(1f, 0.9f, 0.4f), new Color(0.55f, 0.45f, 0.2f), off);
        }
        float want = tt * 0.5f / Mathf.Tan(Mathf.Max(3f, ta * 0.5f) * Mathf.Deg2Rad);
        float done = Mathf.Clamp01((float)Math.Min(s.bevelWidthL, p.edges >= 2 ? s.bevelWidthR : s.bevelWidthL) / Mathf.Max(0.5f, want));
        if (p.edges < 2) done = Mathf.Clamp01((float)(s.bevelWidthL + s.bevelWidthR) * 0.5f / Mathf.Max(0.5f, want));
        bool keenEnough = s.EdgeThickness < 0.6;
        return done > 0.85f && keenEnough ? new Color(0.45f, 0.95f, 0.5f, 0.95f) : Color.Lerp(new Color(0.45f, 0.38f, 0.2f), new Color(1f, 0.88f, 0.35f), done);
    }

    /// <summary>The gold line where the tool meets the metal in the section, for the side chosen.</summary>
    private static void DrawContact(CraftPixels c, Vector2 o, float scale, MetalSegment s, CraftSurfaceSide side, float angle, bool biting)
    {
        float hw = (float)s.width * 0.5f * scale, ht = (float)s.thickness * 0.5f * scale;
        float mid = (ht - (float)s.bevelDepthL * scale + (-ht + (float)s.bevelDepthR * scale)) * 0.5f;
        Color gold = biting ? new Color(1f, 0.9f, 0.45f, 1f) : Mark;
        float thick = biting ? 3.5f : 2.5f, L = Mathf.Min(150f, hw * 1.4f + 30f);
        float ca = Mathf.Cos(angle * Mathf.Deg2Rad), sa = Mathf.Sin(angle * Mathf.Deg2Rad);
        switch (side)
        {
            case CraftSurfaceSide.LeftBevel:
                c.Line(o.x + hw + 2, o.y + mid + 2, o.x + hw + 2 - L * ca, o.y + mid + 2 + L * sa, gold, thick);
                break;
            case CraftSurfaceSide.RightBevel:
                c.Line(o.x + hw + 2, o.y + mid - 2, o.x + hw + 2 - L * ca, o.y + mid - 2 - L * sa, gold, thick);
                break;
            case CraftSurfaceSide.Faces:
                c.Line(o.x - hw, o.y + ht + 3, o.x + hw, o.y + ht + 3, gold, thick);
                c.Line(o.x - hw, o.y - ht - 3, o.x + hw, o.y - ht - 3, gold, thick);
                break;
            case CraftSurfaceSide.EdgeLine:
                c.Line(o.x + hw + 3, o.y - ht - 6, o.x + hw + 3, o.y + ht + 6, gold, thick);
                break;
            case CraftSurfaceSide.Spine:
                c.Line(o.x - hw - 3, o.y - ht - 6, o.x - hw - 3, o.y + ht + 6, gold, thick);
                break;
            case CraftSurfaceSide.Tang:
                c.Line(o.x - hw - 3, o.y - ht - 6, o.x - hw - 3, o.y + ht + 6, gold, thick * 0.7f);
                c.Line(o.x + hw + 3, o.y - ht - 6, o.x + hw + 3, o.y + ht + 6, gold, thick * 0.7f);
                c.Line(o.x - hw, o.y + ht + 3, o.x + hw, o.y + ht + 3, gold, thick * 0.7f);
                break;
            default:
                c.Ring(o.x, o.y, Mathf.Max(4f, Mathf.Min(hw, ht) * 0.6f), 2f, gold);
                break;
        }
    }

    /// <summary>The seven sides as small sections, the gold line on each where the tool works; click one to choose it.</summary>
    private void DrawSideIcons(CraftPixels c)
    {
        var dummy = new MetalSegment { width = 30, thickness = 6, bevelDepthL = 2.2, bevelWidthL = 8, bevelDepthR = 2.2, bevelWidthR = 8, apex = 30 };
        for (int i = 0; i < 7; i++)
        {
            float x = 384 + (i % 4) * 82, y = i < 4 ? 316 : 240;
            sideIcons[i] = new Rect(x, y, 76, 72);
            bool on = Sides[i] == CurrentSide;
            float scale = CraftArt.SectionScale(dummy, new Rect(x, y, 76, 60));
            Vector2 o = CraftArt.DrawSection(c, dummy, new Rect(x, y + 12, 76, 60), new Color(0.6f, 0.6f, 0.64f), scale * 0.8f);
            DrawContact(c, o, scale * 0.8f, dummy, Sides[i], 18f, false);
            c.Frame(x, y, x + 76, y + 72, on ? new Color(1f, 0.8f, 0.38f, 1f) : new Color(1f, 1f, 1f, 0.15f), on ? 2.5f : 1f);
            // A dot per side under the icon (1-7, the side's number) so the list and the pictures match.
            for (int k = 0; k <= i; k++)
                c.Disc(x + 6 + k * 6, y + 6, 1.8f, on ? Mark : new Color(1f, 1f, 1f, 0.4f));
        }
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Down)
            for (int i = 0; i < sideIcons.Length; i++)
                if (sideIcons[i].Contains(pos))
                {
                    side.value = i;
                    return;
                }
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        if (phase == CraftPointer.Down && !(pos.x >= x0 - 6 && pos.x <= x0 + len + 6 && pos.y >= 18 && pos.y <= 232))
            return; // only on the blade
        double x = Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, len));
        // Pressing toward the blade's centre line presses harder.
        double press = pressure.value * (0.7 + 0.6 * Mathf.Clamp01(1f - Mathf.Abs(pos.y - topView.center.y) / 75f));
        if (phase == CraftPointer.Down)
        {
            stroke.Clear();
            sendTimer = 0f;
        }
        if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && (stroke.Active || phase == CraftPointer.Down))
            stroke.Add(Time.unscaledTime, x, 0.5, angle.value, press, Hands);
        if (phase == CraftPointer.Up && stroke.Active)
            Flush();
    }

    // ------------------------------------------------------------------ what it says
    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        int si = Mathf.Clamp(side.value, 0, Sides.Length - 1);
        var sb = new StringBuilder();
        sb.Append($"<b>{SideNames[si]}</b>: {SideHelp[si]}. ");
        if (!IsMetal(p))
        {
            info.text = sb.Append("Pick a blade (or a part) from the pieces on the left.").ToString();
            return;
        }
        int i = Mathf.Clamp(hoverSeg, 0, p.segments.Count - 1);
        MetalSegment s = p.segments[i];
        sb.Append($"Here (segment {i + 1} of {p.segments.Count}): ");
        if (s.Beveled) sb.Append($"bevels {s.bevelWidthL:0.#} / {s.bevelWidthR:0.#} mm at {s.AngleL:0}° / {s.AngleR:0}°, edge {s.EdgeThickness:0.##} mm thick");
        else sb.Append($"{s.width:0.#} × {s.thickness:0.#} mm, no bevel yet");
        if (CraftArt.TargetAt(Rules, p, i, out float tw, out float tt, out float ta, out bool edged))
            sb.Append($" - the design wants {tw:0.#} × {tt:0.#} mm{(edged ? $", an edge of {ta:0}° ({ta * 0.5f:0}° each side)" : "")} (dashed green)");
        sb.Append(". ");
        double hot = p.MaxTemperature;
        if (hot > 200) sb.Append("<color=#FF8A70>It is getting hot: dip it before the temper runs.</color>");
        else if (!p.segments.Exists(x => x.Beveled) && edged) sb.Append("Start with the left bevel along the edge, then the right.");
        info.text = sb.ToString();
    }
}

/// <summary>
/// The whetstone. Above: the section where the pointer is (the bevels, the apex, the burr curling over) with the
/// design's edge as a dashed green outline, and a gauge of the angle you hold against the angle the design wants.
/// Below: the stone - drag strokes across it; the pointer's height over the stone is the angle you hold, the steadier
/// the keener. The strip along the edge goes from red (dull: it glints) to green (keen); the two thin bars under the
/// blade are how far each face has been honed. Raise a burr on one face, switch faces, take the burr off with light
/// trailing strokes or the strop, then go to a finer stone. A very acute edge on soft metal rolls.
/// </summary>
public sealed class CraftSharpenMode : CraftMode
{
    private TMP_Dropdown stone;
    private Slider pressure;
    private Button leftButton, rightButton;
    private TextMeshProUGUI angleText, info;
    private bool leftFace = true;
    private readonly List<CraftAbrasive> stones = new List<CraftAbrasive>();
    private readonly CraftStroke stroke = new CraftStroke();
    private Rect stoneArea, sectionBox, gauge;
    private float x0, mm, heldAngle = 15f, lastAngle = -1f;
    private int from = -1, to = -1, spanStart = -1, hoverSeg;

    public override string Title => "Sharpen";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Whetstone);
    public override string Hint => "Drag strokes across the stone (bottom): the pointer's height is the angle you hold. Raise a burr, switch faces, strop it, go finer." + ByHandHint("on the stone by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Sharpen;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.abrasive = stones.Count > 0 ? stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)].id : null;
        setup.side = leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel;
        setup.pressure = pressure.value;
    }

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
        HandsOnButton(r2, "On the stone by hand", 190f);
        angleText = Text(r2, "");
        RectTransform r3 = Row(controls, 52f);
        info = Text(r3, "", 13f);
        InventoryUIFactory.Size(info, -1f, 52f, 1f);
        FitInside(info);
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

    private CraftAbrasive CurrentStone => stones.Count > 0 ? stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)] : null;

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        stoneArea = new Rect(140, 10, 440, 78);
        sectionBox = new Rect(12, 236, 330, 156);
        gauge = new Rect(356, 236, 352, 156);
        CraftAbrasive s = CurrentStone;
        Color stoneColor = s != null && s.apex <= 1.5 ? new Color(0.45f, 0.3f, 0.18f) : Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.75f, 0.72f, 0.68f), s != null ? Mathf.Clamp01(1f - (float)s.finish / 20f) : 0.5f);
        c.Rect(stoneArea.xMin - 6, stoneArea.yMin - 4, stoneArea.xMax + 6, stoneArea.yMax + 4, Wood);
        c.Rect(stoneArea.xMin, stoneArea.yMin, stoneArea.xMax, stoneArea.yMax, stoneColor);
        // The height marks on the stone: 5° at the bottom, 35° at the top.
        for (int k = 0; k <= 6; k++)
        {
            float y = stoneArea.yMin + stoneArea.height * k / 6f;
            c.Line(stoneArea.xMin, y, stoneArea.xMin + 12, y, new Color(1f, 1f, 1f, 0.35f), 1f);
        }
        if (!IsMetal(p))
            return;
        mm = Fit(p, 660, 110);
        float len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        float[] starts = CraftArt.DrawTop(c, Rules, p, new Rect(x0, 112, len, 104), mm);
        EdgeSpan(p, out int a, out int b);
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            float sx = starts[i], ex = starts[i + 1];
            bool inSpan = i >= a && i <= b;
            float keen = (float)CraftEvaluator.Sharpness(g);
            // Sharpness strip along the edge: red dull, yellow, green keen.
            Color col = keen > 0.75f ? new Color(0.45f, 0.95f, 0.5f) : keen > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(0.45f, 0.95f, 0.5f), (keen - 0.5f) * 4f) : Color.Lerp(new Color(0.95f, 0.35f, 0.3f), new Color(1f, 0.85f, 0.35f), keen * 2f);
            c.Rect(sx + 0.5f, 222, ex - 0.5f, 232, inSpan ? col : new Color(col.r, col.g, col.b, 0.3f));
            if (g.burr > 0.3) c.Rect(sx + 0.5f, 232, ex - 0.5f, 235, new Color(0.92f, 0.92f, 0.98f, 0.9f));
            // How far each face has been honed.
            c.Rect(sx + 0.5f, 102, sx + 0.5f + (ex - sx - 1f) * Mathf.Clamp01((float)g.honeL), 106, new Color(0.55f, 0.75f, 1f, inSpan ? 0.95f : 0.35f));
            c.Rect(sx + 0.5f, 95, sx + 0.5f + (ex - sx - 1f) * Mathf.Clamp01((float)g.honeR), 99, new Color(1f, 0.6f, 0.85f, inSpan ? 0.95f : 0.35f));
        }
        if (from >= 0)
            c.Frame(starts[Mathf.Clamp(a, 0, p.segments.Count - 1)], 108, starts[Mathf.Clamp(b + 1, 1, p.segments.Count)], 236, Mark, 1.5f);
        // The section where the pointer is (else the middle of the edge).
        Vector2 h = ui.Hover;
        hoverSeg = h.x >= x0 && h.x <= x0 + len && h.y > 92 ? Mathf.Clamp(SegmentAtPixel(p, h.x, x0, mm), 0, p.segments.Count - 1) : Mathf.Clamp((a + b) / 2, 0, p.segments.Count - 1);
        MetalSegment seg = p.segments[hoverSeg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, hoverSeg, out float tw, out float tt, out float ta, out bool edged);
        // Magnify the edge: the section's own width is drawn only near the apex.
        var edgeOnly = new MetalSegment
        {
            width = Math.Max(Math.Max(seg.bevelWidthL, seg.bevelWidthR) * 1.6, 4), thickness = seg.thickness, bevelDepthL = seg.bevelDepthL, bevelDepthR = seg.bevelDepthR,
            bevelWidthL = seg.bevelWidthL, bevelWidthR = seg.bevelWidthR, apex = seg.apex, burr = seg.burr, chip = seg.chip, honeL = seg.honeL, honeR = seg.honeR,
        };
        float scale = CraftArt.SectionScale(edgeOnly, sectionBox);
        Vector2 o = CraftArt.DrawSection(c, edgeOnly, sectionBox, CraftArt.MetalColor(Rules, p), scale);
        if (hasTarget && edged)
            CraftArt.DrawTargetSection(c, o, scale, (float)edgeOnly.width, (float)seg.thickness, ta, true, false);
        DrawGauge(c, hasTarget && edged ? ta * 0.5f : -1f);
        // The blade on the stone while stroking: its spine and edge at the angle held.
        if (stoneArea.Contains(h))
        {
            heldAngle = AngleAt(h.y);
            float dir = leftFace ? 1f : -1f;
            c.Line(h.x - 70 * Mathf.Cos(heldAngle * Mathf.Deg2Rad) * dir, h.y - 4 + 70 * Mathf.Sin(heldAngle * Mathf.Deg2Rad), h.x, h.y - 4, Mark, 3f);
            c.Disc(h.x, h.y - 4, 2.5f, Color.white);
        }
    }

    /// <summary>The angle you hold (gold) against the design's (green) - the stone flat, the blade leaning on it.</summary>
    private void DrawGauge(CraftPixels c, float targetAngle)
    {
        c.Rect(gauge.xMin, gauge.yMin, gauge.xMax, gauge.yMax, new Color(0f, 0f, 0f, 0.45f));
        float bx = gauge.xMin + 40, by = gauge.yMin + 30, L = gauge.width - 70;
        c.Rect(bx - 20, by - 14, bx + L + 10, by, new Color(0.45f, 0.43f, 0.4f));
        for (int k = 5; k <= 35; k += 5)
        {
            float r = k * Mathf.Deg2Rad;
            c.Line(bx + (L - 8) * Mathf.Cos(r), by + (L - 8) * Mathf.Sin(r), bx + L * Mathf.Cos(r), by + L * Mathf.Sin(r), new Color(1f, 1f, 1f, 0.3f), 1f);
        }
        if (targetAngle > 0f)
        {
            float r = targetAngle * Mathf.Deg2Rad;
            c.Line(bx, by, bx + L * Mathf.Cos(r), by + L * Mathf.Sin(r), new Color(0.45f, 1f, 0.55f, 0.9f), 2f);
        }
        float shown = stoneArea.Contains(ui.Hover) ? heldAngle : lastAngle > 0f ? lastAngle : heldAngle;
        float hr = shown * Mathf.Deg2Rad;
        c.Line(bx, by, bx + L * Mathf.Cos(hr), by + L * Mathf.Sin(hr), Mark, 4f);
    }

    private float AngleAt(float y) => Mathf.Lerp(5f, 35f, Mathf.Clamp01((y - stoneArea.yMin) / stoneArea.height));

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        if (pos.y > 92 && pos.y < 236)
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
            CraftAbrasive s = CurrentStone;
            Send(new CraftRequest(CraftRequestKind.Sharpen, p.id)
            {
                i0 = a, i1 = b, i2 = (int)(leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = s.id,
                a = (float)stroke.Angle, b = (float)stroke.Pressure, count = Math.Max(1, stroke.Passes), c = (float)stroke.Wobble, flag = stroke.EdgeLeading,
            });
            lastAngle = (float)stroke.Angle;
            angleText.text = $"{stroke.Passes} stroke{(stroke.Passes == 1 ? "" : "s")} at about {stroke.Angle:0}°{(stroke.Wobble > 3 ? ", rocking" : stroke.Wobble > 1.2 ? ", a little unsteady" : ", steady")}" +
                             $"{(stroke.EdgeLeading ? ", edge leading" : ", edge trailing")}.";
            stroke.Clear();
        }
    }

    /// <summary>Where the edge stands and what to do next.</summary>
    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        if (!IsMetal(p) || p.kind != CraftPieceKind.Metal)
        {
            info.text = "Pick a blade from the pieces on the left. Strokes on the stone (bottom) sharpen the stretch of edge framed in gold (drag on the blade to choose it).";
            return;
        }
        EdgeSpan(p, out int a, out int b);
        double hl = 0, hr = 0, burr = 0, keen = 0, apex = 0;
        int n = 0;
        for (int i = a; i <= b && i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            hl += Math.Min(1, g.honeL); hr += Math.Min(1, g.honeR); burr = Math.Max(burr, g.burr);
            keen += CraftEvaluator.Sharpness(g); apex += g.apex; n++;
        }
        n = Math.Max(1, n);
        hl /= n; hr /= n; keen /= n; apex /= n;
        CraftAbrasive s = CurrentStone;
        var sb = new StringBuilder();
        sb.Append($"<color=#8CB8FF>Left face {hl * 100:0}%</color> · <color=#FF99D9>right face {hr * 100:0}%</color> · edge {(keen > 0.75 ? "keen" : keen > 0.5 ? "sharp" : keen > 0.3 ? "dull" : "blunt")} (~{apex:0} µm). ");
        if (!p.segments.Exists(x => x.Beveled))
            sb.Append("<b>Grind the bevels first</b> (Grind area): a stone only hones an edge that is already there.");
        else if (leftFace && hl < 0.95)
            sb.Append("Keep stroking the <b>left face</b> at a steady angle until a burr curls over (the white line).");
        else if (!leftFace && hr < 0.95)
            sb.Append("Keep stroking the <b>right face</b> at the same angle until a burr curls over.");
        else if (burr > 0.3 && (hl < 0.95 || hr < 0.95))
            sb.Append($"A burr has rolled over: now work the <b>{(hl < 0.95 ? "left" : "right")} face</b>.");
        else if (burr > 0.2)
            sb.Append("Take the burr off: <b>light strokes with the edge trailing</b> (pull the blade toward the spine), or the strop.");
        else if (s != null && apex > s.apex * 1.6)
            sb.Append("Both faces honed: a few more steady, light strokes on each face bring it to this stone's best.");
        else
            sb.Append(s != null && s.apex > 2 ? "As keen as this stone makes it: go to a <b>finer stone</b>, then the strop." : "Finished: as keen as it gets.");
        info.text = sb.ToString();
    }
}
