using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tannery: the hide cell by cell on the beam (flesh, hair, scores and holes, the tanning creeping in, the dye),
/// worked with strokes - flesh it, scrape off the hair loosened by lime, stake it supple as it dries, slick it, skive
/// it thinner, rub in oil or wax, brush on dye. Pressure is how hard the knife bears; too hard on clean or thin skin
/// scores it. The vats soak, lime and tan (strength and heat matter; too strong too early closes the surface), the
/// frame stretches it evenly, the rack dries it.
/// </summary>
public sealed class CraftTanneryMode : CraftMode
{
    private enum Tool { Flesh, Unhair, Stake, Slick, Skive, Oil, Dye }

    private Tool tool = Tool.Flesh;
    private TMP_Dropdown toolDrop, agent, supply, vatChoice;
    private Slider pressure, angle, heat, n, e, s, w;
    private readonly List<string> agents = new List<string>(), supplies = new List<string>();
    private readonly CraftStroke stroke = new CraftStroke();
    private Rect area = new Rect(20, 20, 680, 360);
    private static readonly string[] ToolNames = { "Flesh it", "Scrape the hair", "Stake it", "Slick it", "Skive it", "Oil / wax it", "Dye it" };

    public override string Title => "Tannery";
    public override bool Available(CraftStation st) => st.Has(CraftTools.FleshingBeam) || st.Has(CraftTools.Vats) || st.Has(CraftTools.Frame) || st.Has(CraftTools.Rack);
    public override string Hint => "Choose a tool and stroke across the hide. Vats: fill, heat and put the hide in; the frame stretches it, the rack dries it.";

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        toolDrop = Drop(r1, ToolNames, (int)tool, 170f);
        toolDrop.onValueChanged.AddListener(v => tool = (Tool)v);
        pressure = Slide(r1, "Pressure", 0.05f, 1f, 0.5f, false, 64f);
        angle = Slide(r1, "Knife angle", 0f, 1f, 0.7f, false, 76f);
        supplies.Clear();
        var sn = new List<string>();
        foreach (CraftSupplyDefinition x in Rules.Supplies)
            if (x.kind == CraftSupplyKind.Oil || x.kind == CraftSupplyKind.Wax || x.kind == CraftSupplyKind.Dye) { supplies.Add(x.id); sn.Add(x.Name); }
        supply = Drop(r1, sn.Count > 0 ? sn : new List<string> { "-" }, 0, 140f);
        RectTransform r2 = Row(controls);
        var vn = new List<string>();
        for (int i = 0; i < Ws.Vats.Count; i++) vn.Add($"Vat {i + 1}");
        vatChoice = Drop(r2, vn.Count > 0 ? vn : new List<string> { "-" }, 0, 90f);
        agents.Clear();
        var an = new List<string>();
        foreach (CraftTanningAgent a in Rules.Agents) { agents.Add(a.id); an.Add(a.Name); }
        agent = Drop(r2, an, 0, 150f);
        Btn(r2, "Fill / +dose", () => { if (agents.Count > 0) Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = vatChoice.value, id = agents[agent.value], count = 1 }); }, 110f);
        Btn(r2, "Drain", () => Send(new CraftRequest(CraftRequestKind.DrainVat) { i0 = vatChoice.value }), 70f);
        Btn(r2, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = vatChoice.value }), 60f);
        heat = Slide(r2, "Heat", 0f, 1f, 0f, false, 40f);
        heat.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.VatHeat) { i0 = vatChoice.value, a = v }));
        RectTransform r3 = Row(controls);
        Btn(r3, "Into the vat", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Vat, i2 = vatChoice.value }), 120f, UITheme.ButtonStyle.Primary);
        Btn(r3, "Hang to dry", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Rack }), 120f);
        Btn(r3, "Lace in the frame", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Frame }), 150f);
        Btn(r3, "Take it down", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 120f);
        RectTransform r4 = Row(controls);
        n = Slide(r4, "N", 0f, 1f, 0.4f, false, 18f);
        e = Slide(r4, "E", 0f, 1f, 0.4f, false, 18f);
        s = Slide(r4, "S", 0f, 1f, 0.4f, false, 18f);
        w = Slide(r4, "W", 0f, 1f, 0.4f, false, 18f);
        Btn(r4, "Stretch", () => Send(new CraftRequest(CraftRequestKind.Stretch, ui.SelectedId) { a = n.value, b = e.value, c = s.value, d = w.value }), 90f);
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        CraftArt.DrawHide(c, Rules, p.hide, area);
        if (p.location == CraftLocation.Vat)
            c.Rect(area.xMin, area.yMin, area.xMax, area.yMax, new Color(0.25f, 0.18f, 0.1f, 0.45f));
        if (p.location == CraftLocation.Frame)
            c.Frame(area.xMin, area.yMin, area.xMax, area.yMax, Wood, 6f);
        Vector2 h = ui.Hover;
        if (area.Contains(h))
            c.Ring(h.x, h.y, 10, 1.5f, Mark);
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        if (phase == CraftPointer.Down)
            stroke.Clear();
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
            stroke.Add(Time.unscaledTime, Mathf.Clamp01((pos.x - origin.x) / Mathf.Max(1f, cell * p.hide.width)), Mathf.Clamp01((pos.y - origin.y) / Mathf.Max(1f, cell * p.hide.height)),
                0, pressure.value, Hands);
        if (phase != CraftPointer.Up || stroke.Samples < 2)
            return;
        List<float> path = stroke.Path(p.hide.width, p.hide.height);
        float press = (float)stroke.Pressure;
        CraftRequest r;
        switch (tool)
        {
            case Tool.Flesh: r = new CraftRequest(CraftRequestKind.Scrape, p.id) { points = path, flag = false, a = press, b = angle.value }; break;
            case Tool.Unhair: r = new CraftRequest(CraftRequestKind.Scrape, p.id) { points = path, flag = true, a = press, b = angle.value }; break;
            case Tool.Stake: r = new CraftRequest(CraftRequestKind.Stake, p.id) { points = path, a = press }; break;
            case Tool.Slick: r = new CraftRequest(CraftRequestKind.Slick, p.id) { points = path, a = press }; break;
            case Tool.Skive: r = new CraftRequest(CraftRequestKind.Skive, p.id) { points = path, a = press }; break;
            default:
            {
                if (supplies.Count == 0) return;
                string id = supplies[Mathf.Clamp(supply.value, 0, supplies.Count - 1)];
                bool dye = Rules.Supply(id)?.kind == CraftSupplyKind.Dye;
                if (tool == Tool.Dye && !dye || tool == Tool.Oil && dye) { ui.Message(tool == Tool.Dye ? "Choose a dye." : "Choose an oil or a wax."); return; }
                r = new CraftRequest(dye ? CraftRequestKind.Dye : CraftRequestKind.Condition, p.id) { points = path, id = id, a = press };
                break;
            }
        }
        Send(r);
        stroke.Clear();
    }

    public override void Refresh()
    {
        if (heat != null && vatChoice != null && vatChoice.value < Ws.Vats.Count)
            heat.SetValueWithoutNotify((float)Ws.Vats[vatChoice.value].heat);
    }
}

/// <summary>
/// The cutting table: lay a pattern on the hide where the leather is good (right-click or scroll turns it), click to
/// mark it, then trace its outline with the knife - the closer to the line and the steadier, the cleaner the edge.
/// A pattern cannot run off the hide; scars and holes under it go into the part.
/// </summary>
public sealed class CraftCuttingMode : CraftMode
{
    private TMP_Dropdown pattern;
    private readonly List<string> patterns = new List<string>();
    private int rotation, placedX = -1, placedY = -1;
    private readonly CraftStroke trace = new CraftStroke();
    private readonly List<Vector2> tracePoints = new List<Vector2>();
    private Rect area = new Rect(20, 20, 680, 360);

    public override string Title => "Cut";
    public override bool Available(CraftStation s) => s.Has(CraftTools.LeatherBench);
    public override string Hint => "Click to lay the pattern (right-click turns it), then drag the knife around its outline.";

    public override void Build(RectTransform controls)
    {
        patterns.Clear();
        var names = new List<string>();
        foreach (CraftPattern p in Rules.Patterns) { patterns.Add(p.id); names.Add(p.Name); }
        RectTransform r1 = Row(controls);
        pattern = Drop(r1, names, 0, 220f);
        pattern.onValueChanged.AddListener(_ => placedX = placedY = -1);
        Btn(r1, "Turn it", () => rotation = (rotation + 1) % 4, 90f);
        Btn(r1, "Lift the pattern", () => placedX = placedY = -1, 140f, UITheme.ButtonStyle.Ghost);
    }

    private CraftPattern Current => patterns.Count > 0 ? Rules.Pattern(patterns[Mathf.Clamp(pattern.value, 0, patterns.Count - 1)]) : null;

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null)
            return;
        CraftArt.DrawHide(c, Rules, p.hide, area);
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        int gx, gy;
        if (placedX >= 0) { gx = placedX; gy = placedY; }
        else
        {
            Vector2 h = ui.Hover;
            if (!area.Contains(h)) return;
            gx = Mathf.FloorToInt((h.x - origin.x) / cell);
            gy = Mathf.FloorToInt((h.y - origin.y) / cell);
        }
        bool fits = CraftLeather.Fits(p.hide, pat, gx, gy, rotation);
        pat.Size(rotation, out int pw, out int ph);
        Color col = fits ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(1f, 0.35f, 0.3f, 0.35f);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
                if (pat.Covers(x, y, rotation))
                    c.Rect(origin.x + (gx + x) * cell, origin.y + (gy + y) * cell, origin.x + (gx + x + 1) * cell, origin.y + (gy + y + 1) * cell, col);
        for (int i = 1; i < tracePoints.Count; i++)
            c.Line(tracePoints[i - 1].x, tracePoints[i - 1].y, tracePoints[i].x, tracePoints[i].y, Mark, 1.5f);
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null)
            return;
        if (phase == CraftPointer.Scroll) { rotation = (rotation + (scroll > 0 ? 1 : 3)) % 4; return; }
        if (right && phase == CraftPointer.Down) { rotation = (rotation + 1) % 4; return; }
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        if (placedX < 0)
        {
            if (phase != CraftPointer.Down) return;
            int gx = Mathf.FloorToInt((pos.x - origin.x) / cell), gy = Mathf.FloorToInt((pos.y - origin.y) / cell);
            if (!CraftLeather.Fits(p.hide, pat, gx, gy, rotation)) { ui.Message("The pattern does not fit there."); return; }
            placedX = gx;
            placedY = gy;
            tracePoints.Clear();
            return;
        }
        if (phase == CraftPointer.Down) { trace.Clear(); tracePoints.Clear(); }
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
        {
            trace.Add(Time.unscaledTime, pos.x / 720f, pos.y / 400f, 0, 0.5, Hands);
            tracePoints.Add(pos);
        }
        if (phase == CraftPointer.Up && tracePoints.Count > 6)
        {
            double accuracy = Accuracy(p, pat, origin, cell);
            Send(new CraftRequest(CraftRequestKind.Cut, p.id) { id = pat.id, i0 = placedX, i1 = placedY, i2 = rotation, a = (float)accuracy });
            placedX = placedY = -1;
            tracePoints.Clear();
        }
    }

    /// <summary>How well the knife followed the outline: how close each point was to the pattern's border, and how much of the border it went round.</summary>
    private double Accuracy(CraftPiece p, CraftPattern pat, Vector2 origin, float cell)
    {
        pat.Size(rotation, out int pw, out int ph);
        var border = new List<Vector2>();
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                if (!pat.Covers(x, y, rotation)) continue;
                if (pat.Covers(x - 1, y, rotation) && pat.Covers(x + 1, y, rotation) && pat.Covers(x, y - 1, rotation) && pat.Covers(x, y + 1, rotation)) continue;
                border.Add(new Vector2(origin.x + (placedX + x + 0.5f) * cell, origin.y + (placedY + y + 0.5f) * cell));
            }
        if (border.Count == 0)
            return 0.5;
        double off = 0;
        var touched = new HashSet<int>();
        foreach (Vector2 t in tracePoints)
        {
            float best = float.MaxValue;
            int at = 0;
            for (int i = 0; i < border.Count; i++)
            {
                float d = Vector2.Distance(t, border[i]);
                if (d < best) { best = d; at = i; }
            }
            off += Mathf.Clamp01(best / (cell * 1.5f));
            if (best < cell) touched.Add(at);
        }
        double closeness = 1 - off / tracePoints.Count;
        double coverage = touched.Count / (double)border.Count;
        double steady = Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 4f);
        return Math.Max(0, Math.Min(1, closeness * 0.5 + coverage * 0.4 + steady * 0.1));
    }
}
