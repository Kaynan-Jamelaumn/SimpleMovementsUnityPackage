using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Carving wood - a grip, a haft, a scabbard core - with the knife at the bench. Choose what it is for (the design and
/// its wooden part): its target length and thickness are dashed in green on the blank. The blank is shown from above
/// (its width) and from the side (its thickness): drag the knife along it where wood should come off - harder strokes
/// cut deeper, a steady straight stroke leaves it smooth, forcing a deep cut splits it along the grain. Round it off
/// once it is near size; saw it to length (drag back and forth across the mark); bore the slot the tang goes into
/// (circle on its end). Rasping and oiling are done in the Grind area.
/// </summary>
public sealed class CraftCarveMode : CraftMode
{
    private enum Cut { Width, Thickness, Round, Bore, Saw }

    private TMP_Dropdown purpose;
    private Slider pressure;
    private TextMeshProUGUI info;
    private readonly List<Button> cutButtons = new List<Button>();
    private readonly List<(string design, string role)> purposes = new List<(string, string)>();
    private Cut cut = Cut.Width;
    private Rect topView, sideView;
    private float x0, mm, len;
    private readonly List<Vector2> path = new List<Vector2>();
    private bool carving, onSide;
    private float strokeStart;
    private readonly CraftSwirl bore = new CraftSwirl();
    private readonly CraftShake saw = new CraftShake();
    private float sawAt = -1f;
    private int boreSeg = -1;
    private float shavings;
    private string said = "";
    private static readonly string[] CutNames = { "Shave the width", "Shave the thickness", "Round it", "Bore the tang slot", "Saw to length" };

    public override string Title => "Carve";
    public override CraftDiscipline Discipline => CraftDiscipline.Woodwork;
    // Wood is carved at a woodshop, or by a smith fitting grips to blades (files beside an anvil) - not at a leather bench.
    public override bool Available(CraftStation s) =>
        s.Has(CraftTools.ChoppingBlock) || s.Has(CraftTools.Sawhorse) || s.Has(CraftTools.FinishingBench) || s.Has(CraftTools.Files) && s.Has(CraftTools.Anvil);
    public override string Hint => "Drag the knife along the blank where wood should come off (top view: width, side view: thickness); round it, saw it to length, bore the tang slot." + ByHandHint("carve it by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Carve;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup) => setup.pressure = pressure.value;

    public override void Build(RectTransform controls)
    {
        purposes.Clear();
        var names = new List<string> { "(what is it for?)" };
        foreach (CraftDesign d in Rules.Designs)
            foreach (CraftComponentSpec c in d.components)
                if (c.kind == CraftPieceKind.Wood)
                {
                    purposes.Add((d.id, c.role));
                    names.Add($"{d.Name}: {c.Name}");
                }
        RectTransform r1 = Row(controls);
        Text(r1, "For", 13f, 30f);
        purpose = Drop(r1, names, 0, 240f);
        purpose.onValueChanged.AddListener(OnPurpose);
        pressure = Slide(r1, "Knife", 0.1f, 1f, 0.5f, false, 44f);
        HandsOnButton(r1, "Carve by hand", 150f);
        RectTransform r2 = Row(controls, 30f, 4f);
        cutButtons.Clear();
        for (int i = 0; i < CutNames.Length; i++)
        {
            Cut c = (Cut)i;
            cutButtons.Add(Btn(r2, CutNames[i], () => { cut = c; ShowCut(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        info = InfoRow(controls);
        ShowCut();
        CraftPiece p = Selected;
        if (p != null && !string.IsNullOrEmpty(p.intentDesign))
        {
            int k = purposes.FindIndex(x => x.design == p.intentDesign && x.role == p.intentRole);
            if (k >= 0) purpose.SetValueWithoutNotify(k + 1);
        }
    }

    private void OnPurpose(int v)
    {
        CraftPiece p = Selected;
        if (p == null || v <= 0 || v > purposes.Count) return;
        Send(new CraftRequest(CraftRequestKind.SetIntent, p.id) { id = purposes[v - 1].design, other = purposes[v - 1].role });
    }

    private void ShowCut()
    {
        for (int i = 0; i < cutButtons.Count; i++) UITheme.SetSelected(cutButtons[i], (Cut)i == cut);
        carving = false;
        bore.Clear();
        saw.Clear();
        sawAt = -1f;
    }

    public override void Leave() => ShowCut();

    public override void Tick(float dt) => shavings = Mathf.Max(0f, shavings - dt * 1.5f);

    // ------------------------------------------------------------------ target
    /// <summary>The target of the part it is for: length range and thickness (both ways).</summary>
    private bool Target(CraftPiece p, out float lengthMin, out float lengthMax, out float thick)
    {
        lengthMin = lengthMax = thick = 0f;
        CraftComponentSpec spec = p != null && !string.IsNullOrEmpty(p.intentDesign) ? Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        if (spec == null || spec.kind != CraftPieceKind.Wood) return false;
        lengthMin = spec.wood.lengthMin;
        lengthMax = spec.wood.lengthMax;
        thick = (spec.wood.diameterMin + spec.wood.diameterMax) * 0.5f;
        return true;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        bool hasTarget = Target(p, out float lmin, out float lmax, out float thick);
        double maxW = 1, maxT = 1;
        foreach (MetalSegment s in p.segments) { maxW = Math.Max(maxW, s.width); maxT = Math.Max(maxT, s.thickness); }
        mm = Mathf.Max(0.1f, Mathf.Max((float)(Math.Max(p.Length, hasTarget ? lmax : 0) / 640.0), (float)(Math.Max(maxW, maxT) / 120.0)));
        len = (float)(p.Length / mm);
        x0 = 40f;
        topView = new Rect(x0, 220, len, 150);
        sideView = new Rect(x0, 40, len, 150);
        CraftWoodDefinition wd = Rules.Wood(p.wood.wood);
        Color wood = wd != null ? wd.color : new Color(0.6f, 0.45f, 0.3f);
        if (p.wood.charred > 0.05) wood = Color.Lerp(wood, Color.black, (float)p.wood.charred);
        DrawView(c, p, topView, true, wood);
        DrawView(c, p, sideView, false, wood);
        // The target: length marks and the thickness as dashed lines, on both views.
        if (hasTarget)
        {
            Color g = new Color(0.45f, 1f, 0.55f, 0.9f);
            foreach (Rect v in new[] { topView, sideView })
            {
                float half = thick * 0.5f / mm, cy = v.center.y;
                for (float x = v.xMin; x < v.xMin + lmax / mm; x += 8f)
                {
                    c.Line(x, cy + half, Mathf.Min(x + 4f, v.xMin + lmax / mm), cy + half, g, 1.5f);
                    c.Line(x, cy - half, Mathf.Min(x + 4f, v.xMin + lmax / mm), cy - half, g, 1.5f);
                }
                c.Line(v.xMin + lmin / mm, cy - half - 14, v.xMin + lmin / mm, cy + half + 14, g, 1.5f);
                c.Line(v.xMin + lmax / mm, cy - half - 14, v.xMin + lmax / mm, cy + half + 14, g, 1.5f);
            }
        }
        // The knife's path while it cuts; shavings curl off.
        for (int i = 1; i < path.Count; i++)
            c.Line(path[i - 1].x, path[i - 1].y, path[i].x, path[i].y, Mark, 2f);
        if (shavings > 0.01f && path.Count > 0)
            for (int i = 0; i < 12; i++)
            {
                Vector2 at = path[path.Count - 1];
                c.Ring(at.x + (i * 23 % 40) - 20, at.y + 10 + (i * 11 % 30) * shavings, 3f + i % 3, 1f, new Color(wood.r * 1.2f, wood.g * 1.15f, wood.b, shavings));
            }
        if (sawAt >= 0f)
        {
            float sx = x0 + sawAt * len;
            c.Line(sx, topView.yMin - 10, sx, topView.yMax + 10, new Color(0.9f, 0.9f, 0.95f, 0.9f), 2f);
            Meter(c, sx - 40, topView.yMax + 12, 80, 6, Mathf.Clamp01(saw.Shakes / 10f), Mark);
        }
        if (cut == Cut.Bore && bore.Active)
            Meter(c, x0, topView.yMax + 12, 120, 6, bore.Turns - Mathf.Floor(bore.Turns), Mark);
    }

    private void DrawView(CraftPixels c, CraftPiece p, Rect view, bool top, Color wood)
    {
        c.Rect(view.xMin - 30, view.yMin - 8, view.xMin + 640 + 10, view.yMax + 8, new Color(0f, 0f, 0f, 0.25f));
        float x = view.xMin, cy = view.center.y;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float l = (float)s.length / mm, h = (float)(top ? s.width : s.thickness) / mm;
            Color col = Color.Lerp(wood, wood * 0.75f, (i % 2) * 0.25f);
            c.Rect(x, cy - h * 0.5f, x + l + 0.6f, cy + h * 0.5f, col);
            // Grain lines.
            for (int g = -2; g <= 2; g++)
                c.Line(x, cy + g * h * 0.18f, x + l, cy + g * h * 0.18f + Mathf.Sin(i * 1.3f + g) * 1.5f, new Color(0f, 0f, 0f, 0.12f), 1f);
            if (top && s.hole > 0.5)
                c.Rect(x, cy - (float)s.hole * 0.5f / mm, x + l, cy + (float)s.hole * 0.5f / mm, new Color(0.05f, 0.04f, 0.03f, 0.9f));
            x += l;
        }
        if (p.wood.split > 0.05)
            c.Line(view.xMin + len * 0.2f, cy + 3, view.xMin + len * (0.2f + 0.6f * (float)p.wood.split), cy - 2, new Color(0.1f, 0.05f, 0.02f), 1.5f);
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        bool overTop = pos.x >= x0 - 8 && pos.x <= x0 + len + 8 && pos.y >= topView.yMin - 8 && pos.y <= topView.yMax + 8;
        bool overSide = pos.x >= x0 - 8 && pos.x <= x0 + len + 8 && pos.y >= sideView.yMin - 8 && pos.y <= sideView.yMax + 8;
        switch (cut)
        {
            case Cut.Saw:
                if (phase == CraftPointer.Down && overTop) { sawAt = Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, len)); saw.Clear(); }
                if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && sawAt >= 0f) saw.Add(pos.y, Time.unscaledTime); // the saw goes back and forth across the blank
                if (phase == CraftPointer.Up && sawAt >= 0f)
                {
                    if (saw.Shakes >= 10)
                    {
                        if (Send(new CraftRequest(CraftRequestKind.Split, p.id) { a = sawAt })) said = "Sawn through: pick the piece you keep.";
                        sawAt = -1f;
                    }
                    else said = $"Keep sawing ({saw.Shakes} of about 10 strokes) - press again on the mark to go on.";
                }
                return;
            case Cut.Bore:
                if (phase == CraftPointer.Down && overTop)
                {
                    boreSeg = Mathf.Clamp(SegmentAtPixel(p, pos.x, x0, mm), 0, p.segments.Count - 1);
                    bore.Begin(pos);
                }
                if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && bore.Active)
                {
                    bore.Add(pos);
                    if (bore.Turns >= 1f)
                    {
                        bore.Spend(1f);
                        if (Send(new CraftRequest(CraftRequestKind.Carve, p.id) { i0 = boreSeg, i1 = boreSeg, i2 = (int)CraftSurface.CarveCut.Bore, a = 1.2f * pressure.value + 0.3f, b = bore.Steadiness }))
                            shavings = 1f;
                    }
                }
                if (phase == CraftPointer.Up) bore.Clear();
                return;
        }
        // A knife stroke along the blank.
        if (phase == CraftPointer.Down && (overTop || overSide))
        {
            carving = true;
            onSide = overSide && !overTop;
            path.Clear();
            strokeStart = Time.unscaledTime;
        }
        if (!carving)
            return;
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
            path.Add(pos);
        if (phase != CraftPointer.Up)
            return;
        carving = false;
        if (path.Count < 3)
        {
            path.Clear();
            return;
        }
        float a = Mathf.Min(path[0].x, path[path.Count - 1].x), b = Mathf.Max(path[0].x, path[path.Count - 1].x);
        foreach (Vector2 q in path) { a = Mathf.Min(a, q.x); b = Mathf.Max(b, q.x); }
        int from = Mathf.Clamp(SegmentAtPixel(p, a, x0, mm), 0, p.segments.Count - 1), to = Mathf.Clamp(SegmentAtPixel(p, b, x0, mm), 0, p.segments.Count - 1);
        // Steady = straight: how far the stroke wandered from its own line, and the hands' tremble.
        float meanY = 0f;
        foreach (Vector2 q in path) meanY += q.y;
        meanY /= path.Count;
        float wander = 0f;
        foreach (Vector2 q in path) wander = Mathf.Max(wander, Mathf.Abs(q.y - meanY));
        float steady = Mathf.Clamp01(1f - wander / 30f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 5f);
        float seconds = Time.unscaledTime - strokeStart;
        // A tired arm presses less; now and then the knife skids and digs in.
        float depth = pressure.value * 1.8f * Mathf.Clamp(seconds / 0.5f, 0.4f, 1.2f) * (0.6f + 0.4f * (float)Hands.Strength);
        bool slipped = Hands.Slips(23);
        if (slipped) { steady *= 0.25f; depth *= 1.4f; }
        Hands.Move(pressure.value * 0.4f);
        // The view decides what comes off: the top view is the width, the side view the thickness (rounding takes both).
        CraftSurface.CarveCut what = cut == Cut.Round ? CraftSurface.CarveCut.Round : onSide ? CraftSurface.CarveCut.Thickness : CraftSurface.CarveCut.Width;
        if (Send(new CraftRequest(CraftRequestKind.Carve, p.id) { i0 = from, i1 = to, i2 = (int)what, a = depth, b = steady }))
        {
            shavings = 1f;
            said = slipped ? "The knife slipped and dug in: your arm is tired." : depth > 1.4f ? "A deep cut: careful, it can split along the grain." : steady > 0.75f ? "A clean, steady shaving." : "A wobbly cut: it leaves the surface rough.";
        }
        path.Clear();
    }

    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Wood)
        {
            info.text = "Put a wooden blank on the bench (from the materials on the left) and pick it.";
            return;
        }
        var sb = new StringBuilder();
        double minW = double.MaxValue, maxW = 0, minT = double.MaxValue, maxT = 0;
        foreach (MetalSegment s in p.segments) { minW = Math.Min(minW, s.width); maxW = Math.Max(maxW, s.width); minT = Math.Min(minT, s.thickness); maxT = Math.Max(maxT, s.thickness); }
        sb.Append($"{p.Length:0} mm long, {minW:0}-{maxW:0} mm wide, {minT:0}-{maxT:0} mm thick");
        if (Target(p, out float lmin, out float lmax, out float thick))
            sb.Append($" - the part wants {lmin:0}-{lmax:0} mm long, about {thick:0} mm thick (dashed green)");
        else
            sb.Append(" - choose what it is for to see its target");
        sb.Append(". ");
        if (p.wood.split > 0.1) sb.Append($"<color=#FF8A70>It has split ({p.wood.split * 100:0}%): it will be weak.</color> ");
        switch (cut)
        {
            case Cut.Saw: sb.Append("Press on the mark where to cut and saw: drag up and down across the blank. "); break;
            case Cut.Bore: sb.Append("Press on the end where the tang goes and draw circles to bore the slot. "); break;
            case Cut.Round: sb.Append("Drag along it to take the corners off: it rounds toward a grip. "); break;
            default: sb.Append("Drag the knife along the top view to narrow it, along the side view to thin it. "); break;
        }
        info.text = (sb + said).Trim();
    }
}
