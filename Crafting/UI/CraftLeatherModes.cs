using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tannery, in three places, each with only its own tools in the controls:
/// <list type="bullet">
/// <item><b>The beam</b>: the hide cell by cell (flesh, hair, scores and holes, the tanning creeping in, the dye) laid
/// over the sloping fleshing beam, worked with strokes - flesh it, scrape off the hair loosened by lime, stake it supple
/// as it dries, slick it, skive it thinner, rub in oil or wax, brush on dye. The tool is drawn under the pointer, the
/// stroke leaves a trail, and the cells it worked flash so the change can be seen. Pressure is how hard the knife
/// bears; too hard on clean or thin skin scores it.</item>
/// <item><b>The vats</b>: tubs of liquor the hide soaks in - water to soak and clean it, lime to loosen the hair, bark
/// (or alum, or oil) to tan it into leather over hours, acid to pickle or etch metal. Pour from the bucket to fill a vat
/// and to strengthen it (each second of pouring is a dose; too strong too early closes the hide's surface), stir it
/// with the paddle now and then for an even tan, pull the plug to drain it, drag the hide in and out. The hide is drawn
/// soaking in its vat, its colour changing as it tans, with how far the tan has reached the surface and the core. A
/// fire under a vat heats it (hot water hardens leather).</item>
/// <item><b>The frame and the rack</b>: lace the hide in the frame and pull its four sides to stretch it evenly (the
/// lacing goes from slack grey to taut gold to red when overdrawn); hang it on the rack to dry.</item>
/// </list>
/// Above the work, the hide's path (flesh, lime, unhair, tan, stretch and dry, stake, finish) shows what is done and
/// what is next, and its state in words. A test station has quick buttons for all of it.
/// </summary>
public sealed class CraftTanneryMode : CraftMode
{
    private enum Tool { Flesh, Unhair, Stake, Slick, Skive, Oil, Dye }
    private enum Place { Beam, Vats, Frame }
    private enum VatTool { Hide, Bucket, Paddle, Plug }

    private Tool tool = Tool.Flesh;
    private Place place = Place.Beam;
    private VatTool vatTool = VatTool.Hide;
    private TMP_Dropdown agent, supply, vatChoice;
    private Slider pressure, angle, heat, n, e, s, w;
    private TextMeshProUGUI info, path, state;
    private readonly List<string> agents = new List<string>(), supplies = new List<string>();
    private readonly List<Button> placeButtons = new List<Button>(), vatToolButtons = new List<Button>(), toolButtons = new List<Button>();
    private readonly List<RectTransform> beamRows = new List<RectTransform>(), vatRows = new List<RectTransform>(), frameRows = new List<RectTransform>();
    private RectTransform angleRow, supplyRow;
    private readonly CraftStroke stroke = new CraftStroke();
    private readonly CraftSwirl paddle = new CraftSwirl();
    private Rect area = new Rect(40, 24, 640, 330);
    private Rect benchSpot, bucket, frameRect, rackRect;
    private readonly List<Rect> vats = new List<Rect>();
    private readonly List<Rect> plugs = new List<Rect>();
    private int vat;
    private bool dragging, pouring, draining;
    private int dragFromVat = -1, pourVat = -1, handle = -1;
    private Vector2 dragAt;
    private float doses, drainTime;
    private readonly float[] tension = { 0.4f, 0.4f, 0.4f, 0.4f };
    // The stroke being drawn, and the last one's cells flashing to show what it did.
    private readonly List<Vector2> trail = new List<Vector2>(), flash = new List<Vector2>();
    private float flashAt = -9f;
    private string said = "";
    private static readonly string[] ToolNames = { "Flesh it", "Scrape the hair", "Stake it", "Slick it", "Skive it", "Oil / wax", "Dye it" };
    private static readonly string[] PlaceNames = { "The beam", "The vats", "Frame & rack" };
    private static readonly string[] VatToolNames = { "The hide", "Bucket", "Paddle", "Plug" };

    // A leather bench without the tannery's beam, vats and frame: only the finishing (stake, slick, skive, oil, dye).
    private bool BenchOnly => ui != null && ui.Station != null && !ui.Station.Has(CraftTools.FleshingBeam) && !ui.Station.Has(CraftTools.Vats) && !ui.Station.Has(CraftTools.Frame) && !ui.Station.Has(CraftTools.Rack);
    public override string Title => BenchOnly ? "Finish leather" : "Tannery";
    public override CraftDiscipline Discipline => CraftDiscipline.Leatherwork;
    public override bool Available(CraftStation st) => st.Has(CraftTools.FleshingBeam) || st.Has(CraftTools.Vats) || st.Has(CraftTools.Frame) || st.Has(CraftTools.Rack) || st.Has(CraftTools.LeatherBench);

    /// <summary>Is this place at the station (the beam doubles as the bench for finishing)?</summary>
    private bool Here(Place pl) => pl == Place.Beam ? Station.Has(CraftTools.FleshingBeam) || Station.Has(CraftTools.LeatherBench)
        : pl == Place.Vats ? Station.Has(CraftTools.Vats) : Station.Has(CraftTools.Frame) || Station.Has(CraftTools.Rack);

    /// <summary>Can this tool be used here: fleshing and unhairing need the beam; the finishing is done at the beam or a bench.</summary>
    private bool Here(Tool t) => t == Tool.Flesh || t == Tool.Unhair ? Station.Has(CraftTools.FleshingBeam) : (Station.Tools & CraftWorkshop.FinishingTools) != 0;
    public override string Hint => "The path above the work says what the hide needs next. The beam: choose a knife and stroke across the hide. The vats: pour, stir, drain, drag the hide in. Frame & rack: lace it, stretch it, dry it.";

    private static bool FleshSide(Tool t) => t == Tool.Flesh || t == Tool.Skive;

    public override void Build(RectTransform controls)
    {
        beamRows.Clear();
        vatRows.Clear();
        frameRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        if (!Here(place)) place = Here(Place.Beam) ? Place.Beam : Here(Place.Vats) ? Place.Vats : Place.Frame;
        if (!Here(tool)) tool = Here(Tool.Flesh) ? Tool.Flesh : Tool.Stake;
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button pb = Btn(r0, pl == Place.Beam && !Station.Has(CraftTools.FleshingBeam) ? "The bench" : PlaceNames[i], () => { place = pl; Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f);
            pb.gameObject.SetActive(Here(pl));
            placeButtons.Add(pb);
        }
        // The beam: the tools, then how they are held.
        RectTransform b1 = Row(controls, 30f, 3f);
        toolButtons.Clear();
        for (int i = 0; i < ToolNames.Length; i++)
        {
            Tool t = (Tool)i;
            Button tb = Btn(b1, ToolNames[i], () => { tool = t; Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f);
            tb.gameObject.SetActive(Here(t));
            toolButtons.Add(tb);
        }
        beamRows.Add(b1);
        RectTransform b2 = Row(controls);
        pressure = Slide(b2, "Pressure", 0.05f, 1f, 0.5f, false, 64f);
        beamRows.Add(b2);
        angleRow = Row(controls);
        angle = Slide(angleRow, "Knife angle", 0f, 1f, 0.7f, false, 76f);
        Text(angleRow, "flat (gentle) .. steep (bites)", 12f, 170f);
        beamRows.Add(angleRow);
        supplies.Clear();
        var sn = new List<string>();
        foreach (CraftSupplyDefinition x in Rules.Supplies)
            if (x.kind == CraftSupplyKind.Oil || x.kind == CraftSupplyKind.Wax || x.kind == CraftSupplyKind.Dye) { supplies.Add(x.id); sn.Add(x.Name); }
        supplyRow = Row(controls);
        Text(supplyRow, "Rub in", 13f, 50f);
        supply = Drop(supplyRow, sn.Count > 0 ? sn : new List<string> { "-" }, 0, 180f);
        beamRows.Add(supplyRow);
        // The vats: the hand's tool, the agent in the bucket, the fire under the vat chosen.
        RectTransform v1 = Row(controls, 30f, 4f);
        vatToolButtons.Clear();
        for (int i = 0; i < VatToolNames.Length; i++)
        {
            VatTool vt = (VatTool)i;
            vatToolButtons.Add(Btn(v1, VatToolNames[i], () => { vatTool = vt; Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        vatRows.Add(v1);
        RectTransform v2 = Row(controls);
        Text(v2, "In the bucket", 13f, 90f);
        agents.Clear();
        var an = new List<string>();
        foreach (CraftTanningAgent a in Rules.Agents) { agents.Add(a.id); an.Add(a.Name); }
        agent = Drop(v2, an, 0, 160f);
        heat = Slide(v2, "Fire under it", 0f, 1f, 0f, false, 84f);
        heat.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.VatHeat) { i0 = vat, a = v }));
        vatRows.Add(v2);
        if (QuickBench)
        {
            RectTransform r3 = Row(controls);
            var vn = new List<string>();
            for (int i = 0; i < Ws.Vats.Count; i++) vn.Add($"Vat {i + 1}");
            vatChoice = Drop(r3, vn.Count > 0 ? vn : new List<string> { "-" }, 0, 80f);
            vatChoice.onValueChanged.AddListener(v => vat = v);
            Btn(r3, "Fill / +dose", () => { if (agents.Count > 0) Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = vat, id = agents[agent.value], count = 1 }); }, 100f);
            Btn(r3, "Drain", () => Send(new CraftRequest(CraftRequestKind.DrainVat) { i0 = vat }), 60f);
            Btn(r3, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = vat }), 50f);
            Btn(r3, "Into the vat", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Vat, i2 = vat }), 100f);
            vatRows.Add(r3);
            RectTransform r4 = Row(controls);
            Btn(r4, "Hang", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Rack }), 60f);
            Btn(r4, "Frame", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Frame }), 60f);
            Btn(r4, "Take down", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 90f);
            frameRows.Add(r4);
            RectTransform r5 = Row(controls);
            n = Slide(r5, "N", 0f, 1f, 0.4f, false, 18f);
            e = Slide(r5, "E", 0f, 1f, 0.4f, false, 18f);
            s = Slide(r5, "S", 0f, 1f, 0.4f, false, 18f);
            w = Slide(r5, "W", 0f, 1f, 0.4f, false, 18f);
            Btn(r5, "Stretch", () => Send(new CraftRequest(CraftRequestKind.Stretch, ui.SelectedId) { a = n.value, b = e.value, c = s.value, d = w.value }), 90f);
            frameRows.Add(r5);
        }
        // Always: the hide's path and its state, then the guidance.
        RectTransform p1 = Row(controls, 24f);
        path = UITheme.Label(p1, "", 13f, UITheme.Text);
        InventoryUIFactory.Size(path, -1f, 24f, 1f);
        RectTransform p2 = Row(controls, 24f);
        state = UITheme.Label(p2, "", 12f, UITheme.Muted);
        InventoryUIFactory.Size(state, -1f, 24f, 1f);
        info = InfoRow(controls, 40f);
        Show();
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        for (int i = 0; i < vatToolButtons.Count; i++) UITheme.SetSelected(vatToolButtons[i], (VatTool)i == vatTool);
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], (Tool)i == tool);
        foreach (RectTransform r in beamRows) r.gameObject.SetActive(place == Place.Beam);
        foreach (RectTransform r in vatRows) r.gameObject.SetActive(place == Place.Vats);
        foreach (RectTransform r in frameRows) r.gameObject.SetActive(place == Place.Frame);
        if (place == Place.Beam)
        {
            angleRow.gameObject.SetActive(tool == Tool.Flesh || tool == Tool.Unhair);
            supplyRow.gameObject.SetActive(tool == Tool.Oil || tool == Tool.Dye);
        }
        dragging = pouring = draining = false;
        handle = -1;
        paddle.Clear();
        trail.Clear();
    }

    public override void Leave() => Show();

    public override void Tick(float dt)
    {
        if (pouring)
        {
            if (!ui.PointerHeld) FinishPour();
            else if (pourVat >= 0 && pourVat < vats.Count && vats[pourVat].Contains(ui.Hover)) doses += dt;
        }
        if (draining)
        {
            if (!ui.PointerHeld) draining = false;
            else if ((drainTime += dt) >= 1.5f)
            {
                draining = false;
                if (Send(new CraftRequest(CraftRequestKind.DrainVat) { i0 = vat })) said = $"Vat {vat + 1} drained.";
            }
        }
    }

    private void FinishPour()
    {
        pouring = false;
        int count = doses >= 0.4f ? Mathf.Max(1, Mathf.RoundToInt(doses)) : 0;
        if (count > 0 && agents.Count > 0 && pourVat >= 0)
        {
            string id = agents[Mathf.Clamp(agent.value, 0, agents.Count - 1)];
            if (Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = pourVat, id = id, count = count }))
                said = $"{count} dose{(count == 1 ? "" : "s")} of {Rules.Agent(id)?.Name ?? id} into vat {pourVat + 1}.";
        }
        pourVat = -1;
    }

    // ------------------------------------------------------------------ what the hide needs
    private struct HideMeans { public double flesh, hair, loose, tanS, tanC, moisture, stiff, oil, dye, smooth, damage, rot; public int cells; }

    private static HideMeans Means(HideState h)
    {
        var m = new HideMeans();
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            m.cells++;
            m.flesh += c.flesh; m.hair += c.hair; m.loose += c.hairLoose; m.tanS += c.tanS; m.tanC += c.tanC; m.moisture += c.moisture;
            m.stiff += c.stiff; m.oil += c.oil + c.wax; m.dye += c.dye; m.smooth += c.smooth; m.damage += c.damage; m.rot += c.rot;
        }
        if (m.cells == 0) return m;
        double k = 1.0 / m.cells;
        m.flesh *= k; m.hair *= k; m.loose *= k; m.tanS *= k; m.tanC *= k; m.moisture *= k; m.stiff *= k; m.oil *= k; m.dye *= k; m.smooth *= k; m.damage *= k; m.rot *= k;
        return m;
    }

    private static readonly string[] StepNames = { "Flesh", "Lime", "Unhair", "Tan", "Stretch & dry", "Stake", "Finish" };
    private static readonly string[] StepHow =
    {
        "on the beam, Flesh it (flesh side): take the meat and fat off",
        "in a vat of lime for some hours: it loosens the hair",
        "on the beam, Scrape the hair (grain side)",
        "in a vat of bark (or alum, or oil) for hours, strengthened bit by bit and stirred: until the core is tanned",
        "lace it in the frame, stretch it evenly, let it dry (or hang it on the rack)",
        "on the beam, Stake it while it is still a little damp, so it dries supple",
        "oil or wax it, dye it, slick it - as the part wants",
    };

    /// <summary>How far each step is (0-1) for this hide.</summary>
    private static float[] Steps(HideState h, HideMeans m)
    {
        bool tanned = m.tanC > 0.75;
        return new[]
        {
            m.flesh < 0.08 ? 1f : Mathf.Clamp01(1f - (float)m.flesh),
            m.hair < 0.1 ? 1f : Mathf.Clamp01((float)m.loose / 0.7f),
            m.hair < 0.1 ? 1f : Mathf.Clamp01(1f - (float)m.hair),
            tanned ? 1f : Mathf.Clamp01((float)m.tanC / 0.75f),
            h.dried ? 1f : tanned ? Mathf.Clamp01(1f - (float)m.moisture) : 0f,
            h.dried && m.stiff < 0.35 ? 1f : h.dried ? Mathf.Clamp01(1f - (float)m.stiff) : 0f,
            m.oil > 0.2 || m.dye > 0.3 || m.smooth > 0.3 ? 1f : 0f,
        };
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        switch (place)
        {
            case Place.Beam: DrawBeam(c, p); break;
            case Place.Vats: DrawVats(c, p); break;
            default: DrawFrame(c, p); break;
        }
    }

    private static readonly List<Vector2> beamShape = new List<Vector2> { new Vector2(10, 40), new Vector2(710, 70), new Vector2(710, 330), new Vector2(10, 360) };

    private void DrawBeam(CraftPixels c, CraftPiece p)
    {
        if (Station.Has(CraftTools.FleshingBeam))
        {
            // The fleshing beam: a sloping log the hide is thrown over.
            c.Polygon(beamShape, new Color(0.3f, 0.2f, 0.12f));
            for (int k = 0; k < 6; k++) c.Line(10, 80 + k * 50, 710, 100 + k * 45, new Color(0.24f, 0.16f, 0.1f), 2f);
        }
        else
        {
            // The leather bench: a flat board.
            c.Rect(20, 14, 700, 364, new Color(0.42f, 0.33f, 0.24f));
            for (int k = 0; k < 7; k++) c.Line(20, 14 + k * 50, 700, 14 + k * 50, new Color(0.36f, 0.28f, 0.2f), 1f);
        }
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        // Which side is up: the flesh side (pink border) for fleshing and skiving, the grain side (tan border) for the rest.
        Color side = FleshSide(tool) ? new Color(0.86f, 0.55f, 0.5f) : new Color(0.7f, 0.55f, 0.35f);
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        c.Frame(origin.x - 4, origin.y - 4, origin.x + cell * p.hide.width + 4, origin.y + cell * p.hide.height + 4, side, 3f);
        CraftArt.DrawHide(c, Rules, p.hide, area, FleshSide(tool));
        if (p.location == CraftLocation.Vat)
            c.Rect(area.xMin, area.yMin, area.xMax, area.yMax, new Color(0.25f, 0.18f, 0.1f, 0.55f));
        if (p.location == CraftLocation.Frame || p.location == CraftLocation.Rack)
            c.Rect(area.xMin, area.yMin, area.xMax, area.yMax, new Color(0.1f, 0.1f, 0.12f, 0.5f));
        // The last stroke's cells flash, fading.
        float since = Time.unscaledTime - flashAt;
        if (since < 0.9f)
            foreach (Vector2 q in flash)
                c.Disc(q.x, q.y, cell * 1.1f, new Color(1f, 1f, 0.85f, 0.35f * (1f - since / 0.9f)));
        for (int i = 1; i < trail.Count; i++)
            c.Line(trail[i - 1].x, trail[i - 1].y, trail[i].x, trail[i].y, new Color(1f, 0.95f, 0.8f, 0.6f), 2f);
        Vector2 h = ui.Hover;
        if (area.Contains(h)) DrawTool(c, h);
    }

    /// <summary>The tool in the hand, under the pointer.</summary>
    private void DrawTool(CraftPixels c, Vector2 h)
    {
        Color steel = new Color(0.75f, 0.76f, 0.8f), handle = new Color(0.45f, 0.3f, 0.18f);
        float a = angle != null ? angle.value : 0.5f;
        switch (tool)
        {
            case Tool.Flesh:
            case Tool.Unhair:
                // A two-handled beam knife across the stroke, tilted by the knife angle.
                c.Line(h.x - 34, h.y - 6 * a, h.x + 34, h.y + 6 * a, steel, tool == Tool.Flesh ? 4f : 3f);
                c.Line(h.x - 46, h.y - 8 * a, h.x - 34, h.y - 6 * a, handle, 5f);
                c.Line(h.x + 34, h.y + 6 * a, h.x + 46, h.y + 8 * a, handle, 5f);
                break;
            case Tool.Stake:
                c.Rect(h.x - 4, h.y, h.x + 4, h.y + 40, handle);
                c.Disc(h.x, h.y, 8f, steel);
                break;
            case Tool.Slick:
                c.Rect(h.x - 18, h.y - 6, h.x + 18, h.y + 6, new Color(0.7f, 0.85f, 0.9f, 0.85f));
                break;
            case Tool.Skive:
                c.Line(h.x - 20, h.y - 10, h.x + 14, h.y + 6, steel, 3f);
                c.Line(h.x + 14, h.y + 6, h.x + 34, h.y + 16, handle, 5f);
                break;
            default:
            {
                Color col = tool == Tool.Dye ? DyeColour() : new Color(0.85f, 0.7f, 0.35f, 0.8f);
                if (tool == Tool.Dye) { c.Line(h.x, h.y, h.x + 10, h.y + 34, handle, 4f); c.Disc(h.x, h.y, 6f, col); }
                else c.Disc(h.x, h.y, 12f, new Color(0.9f, 0.88f, 0.8f, 0.85f));
                break;
            }
        }
    }

    private Color DyeColour()
    {
        CraftSupplyDefinition d = supplies.Count > 0 ? Rules.Supply(supplies[Mathf.Clamp(supply.value, 0, supplies.Count - 1)]) : null;
        return d != null ? d.color : new Color(0.4f, 0.2f, 0.15f);
    }

    private void DrawBench(CraftPixels c, CraftPiece p)
    {
        benchSpot = new Rect(12, 200, 136, 140);
        if (place == Place.Frame) benchSpot.width = 124f; // clear of the frame's west handle
        c.Rect(benchSpot.xMin, benchSpot.yMin, benchSpot.xMax, benchSpot.yMin + 6, Wood);
        if (p != null && p.kind == CraftPieceKind.Hide && (p.location == CraftLocation.Air || dragging))
        {
            Rect at = dragging ? new Rect(dragAt.x - 60, dragAt.y - 50, 120, 100) : new Rect(benchSpot.xMin + 8, benchSpot.yMin + 10, 120, 110);
            CraftArt.DrawHide(c, Rules, p.hide, at);
        }
    }

    private void DrawVats(CraftPixels c, CraftPiece p)
    {
        vats.Clear();
        plugs.Clear();
        int count = Ws.Vats.Count;
        float width = count > 0 ? Mathf.Min(150f, (540f - 12f * (count - 1)) / count) : 0f;
        for (int i = 0; i < count; i++)
        {
            CraftVatState v = Ws.Vats[i];
            var r = new Rect(168 + i * (width + 12), 60, width, 220);
            vats.Add(r);
            plugs.Add(new Rect(r.xMax - 22, r.yMin + 4, 18, 18));
            if (v.heat > 0.02)
                for (int k = 0; k < 6; k++)
                    c.Line(r.xMin + 10 + k * (r.width - 20) / 5f, r.yMin - 18, r.xMin + 12 + k * (r.width - 20) / 5f + 3 * Mathf.Sin(Time.unscaledTime * 6 + k), r.yMin - 2, new Color(1f, 0.55f, 0.15f, (float)v.heat), 5f);
            c.Rect(r.xMin - 6, r.yMin - 4, r.xMax + 6, r.yMax, Wood);
            // Hoops round the tub.
            c.Rect(r.xMin - 7, r.yMin + 40, r.xMax + 7, r.yMin + 46, Iron);
            c.Rect(r.xMin - 7, r.yMax - 50, r.xMax + 7, r.yMax - 44, Iron);
            c.Rect(r.xMin, r.yMin, r.xMax, r.yMax - 4, new Color(0.1f, 0.08f, 0.06f));
            CraftTanningAgent a = Rules.Agent(v.agent);
            float level = Mathf.Clamp01((float)(v.volume / 60.0));
            if (draining && i == vat) level *= 1f - drainTime / 1.5f;
            float top = r.yMin + (r.height - 8) * level;
            Color liquid = a == null || a.kind == CraftAgentKind.Water ? new Color(0.3f, 0.45f, 0.6f) : a.color;
            liquid = Color.Lerp(new Color(0.3f, 0.45f, 0.6f), liquid, 0.4f + 0.6f * (float)v.strength);
            // A hide soaking in it: drawn as it is (its colour turns as it tans), under the liquor.
            CraftPiece soaking = null;
            foreach (CraftPiece q in Ws.Pieces)
                if (q.kind == CraftPieceKind.Hide && q.location == CraftLocation.Vat && q.vat == i && !(dragging && dragFromVat == i && q == p)) { soaking = q; break; }
            if (soaking != null)
                CraftArt.DrawHide(c, Rules, soaking.hide, new Rect(r.xMin + 14, r.yMin + 14, r.width - 28, Mathf.Max(30f, (r.height - 40) * Mathf.Max(0.35f, level) * 0.85f)));
            if (level > 0.01f)
            {
                c.Rect(r.xMin, r.yMin, r.xMax, top, new Color(liquid.r, liquid.g, liquid.b, soaking != null ? 0.55f : 0.92f));
                c.Rect(r.xMin, top - 3, r.xMax, top, Color.Lerp(liquid, Color.white, 0.3f));
                if (v.stirred > 0.2)
                    for (int k = 0; k < 3; k++)
                        c.Ring(r.center.x, top - 6, 8 + k * 10 + (Time.unscaledTime * 12f) % 10f, 1f, new Color(1f, 1f, 1f, 0.25f * (float)v.stirred));
                if (v.temperature > 45)
                    for (int k = 0; k < 4; k++)
                        c.Line(r.xMin + 14 + k * (r.width - 28) / 3f, top + 4, r.xMin + 18 + k * (r.width - 28) / 3f + 4 * Mathf.Sin(Time.unscaledTime * 2 + k), top + 26, new Color(1f, 1f, 1f, 0.3f), 2f);
                // Strength gauge, on the left inside the tub.
                c.Rect(r.xMin + 4, r.yMin + 26, r.xMin + 10, r.yMax - 12, new Color(0f, 0f, 0f, 0.5f));
                c.Rect(r.xMin + 4, r.yMin + 26, r.xMin + 10, r.yMin + 26 + (r.height - 38) * (float)v.strength, Mark);
            }
            // Under the tub: how far the tan has reached the hide's surface (upper bar) and its core (lower bar).
            if (soaking != null)
            {
                HideMeans m = Means(soaking.hide);
                Meter(c, r.xMin, r.yMin - 34, r.width, 6, (float)m.tanS, new Color(0.75f, 0.5f, 0.3f));
                Meter(c, r.xMin, r.yMin - 44, r.width, 6, (float)m.tanC, new Color(0.55f, 0.32f, 0.18f));
            }
            c.Disc(plugs[i].center.x, plugs[i].center.y, 7f, new Color(0.35f, 0.25f, 0.15f));
            if (i == vat) c.Frame(r.xMin - 8, r.yMin - 6, r.xMax + 8, r.yMax + 2, Mark, 2f);
            if (pouring && pourVat == i)
            {
                Vector2 h = ui.Hover;
                c.Line(h.x, h.y + 40, h.x, top, Color.Lerp(liquid, Color.white, 0.2f), 4f);
                Meter(c, r.xMin, r.yMax + 6, r.width, 6, doses - Mathf.Floor(doses), new Color(0.6f, 0.8f, 1f));
            }
            if (draining && i == vat)
            {
                Meter(c, r.xMin, r.yMax + 6, r.width, 6, drainTime / 1.5f, Mark);
                c.Line(plugs[i].center.x, plugs[i].center.y, plugs[i].center.x + 10, plugs[i].center.y - 40, liquid, 3f);
            }
        }
        // The bucket of the agent chosen, and the paddle.
        bucket = new Rect(20, 40, 110, 110);
        CraftTanningAgent ba = agents.Count > 0 ? Rules.Agent(agents[Mathf.Clamp(agent.value, 0, agents.Count - 1)]) : null;
        c.Polygon(new List<Vector2> { new Vector2(30, 60), new Vector2(120, 60), new Vector2(130, 160), new Vector2(20, 160) }, Iron);
        c.Rect(32, 130, 118, 152, ba != null ? ba.color : new Color(0.3f, 0.45f, 0.6f));
        Vector2 hp = ui.Hover;
        if (vatTool == VatTool.Paddle && VatAt(hp) >= 0) { c.Line(hp.x, hp.y, hp.x + 20, hp.y + 110, Wood, 5f); c.Rect(hp.x - 10, hp.y - 14, hp.x + 10, hp.y + 4, Wood); }
        if (vatTool == VatTool.Bucket && hp.x >= 0 && !pouring) c.Polygon(new List<Vector2> { new Vector2(hp.x - 14, hp.y + 10), new Vector2(hp.x + 14, hp.y + 10), new Vector2(hp.x + 18, hp.y + 40), new Vector2(hp.x - 18, hp.y + 40) }, Iron);
        DrawBench(c, p);
    }

    private void DrawFrame(CraftPixels c, CraftPiece p)
    {
        frameRect = new Rect(196, 30, 316, 340);
        rackRect = new Rect(540, 30, 160, 340);
        c.Frame(frameRect.xMin, frameRect.yMin, frameRect.xMax, frameRect.yMax, Wood, 10f);
        c.Rect(rackRect.xMin, rackRect.yMax - 10, rackRect.xMax, rackRect.yMax, Wood);
        c.Rect(rackRect.xMin, rackRect.yMin, rackRect.xMin + 8, rackRect.yMax, Wood);
        c.Rect(rackRect.xMax - 8, rackRect.yMin, rackRect.xMax, rackRect.yMax, Wood);
        if (p != null && p.kind == CraftPieceKind.Hide)
        {
            if (p.location == CraftLocation.Frame && !dragging)
            {
                var inner = new Rect(frameRect.xMin + 30 - tension[3] * 20, frameRect.yMin + 30 - tension[2] * 20, frameRect.width - 60 + (tension[1] + tension[3]) * 20, frameRect.height - 60 + (tension[0] + tension[2]) * 20);
                CraftArt.DrawHide(c, Rules, p.hide, inner);
                // The lacing to the frame, coloured by how hard each side is drawn: slack grey, taut gold, overdrawn red.
                Color Lace(float t) => t < 0.25f ? new Color(0.55f, 0.55f, 0.55f) : t < 0.75f ? new Color(0.9f, 0.75f, 0.4f) : new Color(1f, 0.4f, 0.3f);
                for (int k = 0; k < 8; k++)
                {
                    float t = (k + 0.5f) / 8f;
                    c.Line(inner.xMin + inner.width * t, inner.yMax, inner.xMin + inner.width * t, frameRect.yMax - 5, Lace(tension[0]), 1f + tension[0]);
                    c.Line(inner.xMin + inner.width * t, inner.yMin, inner.xMin + inner.width * t, frameRect.yMin + 5, Lace(tension[2]), 1f + tension[2]);
                    c.Line(inner.xMax, inner.yMin + inner.height * t, frameRect.xMax - 5, inner.yMin + inner.height * t, Lace(tension[1]), 1f + tension[1]);
                    c.Line(inner.xMin, inner.yMin + inner.height * t, frameRect.xMin + 5, inner.yMin + inner.height * t, Lace(tension[3]), 1f + tension[3]);
                }
                foreach (int k in new[] { 0, 1, 2, 3 })
                {
                    Rect hr = Handle(k);
                    c.Rect(hr.xMin, hr.yMin, hr.xMax, hr.yMax, k == handle ? Mark : new Color(0.7f, 0.6f, 0.45f));
                }
                Meter(c, frameRect.xMin, frameRect.yMin - 20, frameRect.width, 8, 1f - (float)Means(p.hide).moisture, new Color(0.9f, 0.8f, 0.55f));
            }
            else if (p.location == CraftLocation.Rack && !dragging)
            {
                CraftArt.DrawHide(c, Rules, p.hide, new Rect(rackRect.xMin + 12, rackRect.yMin + 20, rackRect.width - 24, rackRect.height - 60));
                Meter(c, rackRect.xMin, rackRect.yMin - 20, rackRect.width, 8, 1f - (float)Means(p.hide).moisture, new Color(0.9f, 0.8f, 0.55f));
            }
        }
        DrawBench(c, p);
    }

    /// <summary>A side's handle on the frame (0 north, 1 east, 2 south, 3 west), pulled out by its tension.</summary>
    private Rect Handle(int k)
    {
        float t = tension[k] * 24f;
        switch (k)
        {
            case 0: return new Rect(frameRect.center.x - 14, frameRect.yMax - 4 + t, 28, 14);
            case 1: return new Rect(frameRect.xMax - 4 + t, frameRect.center.y - 14, 14, 28);
            case 2: return new Rect(frameRect.center.x - 14, frameRect.yMin - 10 - t, 28, 14);
            default: return new Rect(frameRect.xMin - 10 - t, frameRect.center.y - 14, 14, 28);
        }
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (place == Place.Vats) { VatPointer(phase, pos, p); return; }
        if (place == Place.Frame) { FramePointer(phase, pos, p); return; }
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        if (p.location != CraftLocation.Air)
        {
            if (phase == CraftPointer.Down) said = p.location == CraftLocation.Vat ? "It is in a vat: take it out first (the vats)." : "It is on the frame or the rack: take it down first.";
            return;
        }
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        if (phase == CraftPointer.Down) { stroke.Clear(); trail.Clear(); }
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
        {
            stroke.Add(Time.unscaledTime, Mathf.Clamp01((pos.x - origin.x) / Mathf.Max(1f, cell * p.hide.width)), Mathf.Clamp01((pos.y - origin.y) / Mathf.Max(1f, cell * p.hide.height)),
                0, pressure.value, Hands);
            if (trail.Count == 0 || Vector2.Distance(trail[trail.Count - 1], pos) > 3f) trail.Add(pos);
        }
        if (phase != CraftPointer.Up || stroke.Samples < 2)
            return;
        List<float> cells = stroke.Path(p.hide.width, p.hide.height);
        float press = (float)stroke.Pressure;
        CraftRequest r;
        switch (tool)
        {
            case Tool.Flesh: r = new CraftRequest(CraftRequestKind.Scrape, p.id) { points = cells, flag = false, a = press, b = angle.value }; break;
            case Tool.Unhair: r = new CraftRequest(CraftRequestKind.Scrape, p.id) { points = cells, flag = true, a = press, b = angle.value }; break;
            case Tool.Stake: r = new CraftRequest(CraftRequestKind.Stake, p.id) { points = cells, a = press }; break;
            case Tool.Slick: r = new CraftRequest(CraftRequestKind.Slick, p.id) { points = cells, a = press }; break;
            case Tool.Skive: r = new CraftRequest(CraftRequestKind.Skive, p.id) { points = cells, a = press }; break;
            default:
            {
                if (supplies.Count == 0) return;
                string id = supplies[Mathf.Clamp(supply.value, 0, supplies.Count - 1)];
                bool dye = Rules.Supply(id)?.kind == CraftSupplyKind.Dye;
                if (tool == Tool.Dye && !dye || tool == Tool.Oil && dye) { ui.Message(tool == Tool.Dye ? "Choose a dye." : "Choose an oil or a wax."); return; }
                r = new CraftRequest(dye ? CraftRequestKind.Dye : CraftRequestKind.Condition, p.id) { points = cells, id = id, a = press };
                break;
            }
        }
        HideMeans before = Means(p.hide);
        if (Send(r))
        {
            flash.Clear();
            flash.AddRange(trail);
            flashAt = Time.unscaledTime;
            said = StrokeWords(before, Means(p.hide));
        }
        stroke.Clear();
        trail.Clear();
    }

    /// <summary>What the stroke did, read from the hide before and after (offline it answers at once).</summary>
    private string StrokeWords(HideMeans a, HideMeans b)
    {
        if (b.damage > a.damage + 0.002) return "Too hard: the knife scored the hide.";
        switch (tool)
        {
            case Tool.Flesh: return b.flesh < a.flesh - 0.001 ? "Flesh and fat peel away under the knife." : "Nothing much comes off here.";
            case Tool.Unhair: return b.hair < a.hair - 0.001 ? "The hair slides off in clumps." : a.loose < 0.4 ? "The hair holds fast: it has not been limed long enough." : "Little hair left here.";
            case Tool.Stake: return b.stiff < a.stiff - 0.001 ? "Worked over the stake: it softens." : "It is as supple as staking makes it here.";
            case Tool.Slick: return "The grain lies down smooth.";
            case Tool.Skive: return "A thin shaving comes off the flesh side.";
            case Tool.Dye: return "The dye goes on - keep the strokes even.";
            default: return "Worked in: it darkens and softens.";
        }
    }

    private int VatAt(Vector2 pos)
    {
        for (int i = 0; i < vats.Count; i++)
            if (vats[i].Contains(pos)) return i;
        return -1;
    }

    private void VatPointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        int at = VatAt(pos);
        if (phase == CraftPointer.Down && at >= 0) vat = at;
        switch (vatTool)
        {
            case VatTool.Hide:
                if (phase == CraftPointer.Down && p != null && p.kind == CraftPieceKind.Hide)
                {
                    if (p.location == CraftLocation.Air && benchSpot.Contains(pos)) { dragging = true; dragFromVat = -1; }
                    else if (p.location == CraftLocation.Vat && at == p.vat) { dragging = true; dragFromVat = at; }
                    dragAt = pos;
                }
                else if (phase == CraftPointer.Drag) dragAt = pos;
                else if (phase == CraftPointer.Up && dragging)
                {
                    dragging = false;
                    if (dragFromVat < 0 && at >= 0) { if (Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Vat, i2 = at })) said = $"Into vat {at + 1}: it soaks."; }
                    else if (dragFromVat >= 0 && at != dragFromVat) { if (Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air })) said = "Out of the vat, dripping."; }
                    dragFromVat = -1;
                }
                return;
            case VatTool.Bucket:
                if (phase == CraftPointer.Down && at >= 0) { pouring = true; pourVat = at; doses = 0f; }
                else if (phase == CraftPointer.Up && pouring) FinishPour();
                return;
            case VatTool.Paddle:
                if (phase == CraftPointer.Down && at >= 0) paddle.Begin(vats[at].center);
                if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && paddle.Active)
                {
                    paddle.Add(pos);
                    if (paddle.Turns >= 2f)
                    {
                        paddle.Spend(2f);
                        if (Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = vat })) said = $"Vat {vat + 1} stirred: the liquor works in evenly.";
                    }
                }
                if (phase == CraftPointer.Up) paddle.Clear();
                return;
            case VatTool.Plug:
                if (phase == CraftPointer.Down)
                    for (int i = 0; i < plugs.Count; i++)
                        if (plugs[i].Contains(pos) || vats[i].Contains(pos) && pos.y < vats[i].yMin + 40) { vat = i; draining = true; drainTime = 0f; }
                return;
        }
    }

    private void FramePointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        if (phase == CraftPointer.Down)
        {
            if (p.location == CraftLocation.Frame)
                for (int k = 0; k < 4; k++)
                    if (Handle(k).Contains(pos)) { handle = k; return; }
            if (p.location == CraftLocation.Air && benchSpot.Contains(pos) || p.location == CraftLocation.Frame && frameRect.Contains(pos) || p.location == CraftLocation.Rack && rackRect.Contains(pos))
            {
                dragging = true;
                dragAt = pos;
            }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            dragAt = pos;
            if (handle >= 0)
            {
                float d = handle == 0 ? pos.y - frameRect.yMax : handle == 1 ? pos.x - frameRect.xMax : handle == 2 ? frameRect.yMin - pos.y : frameRect.xMin - pos.x;
                tension[handle] = Mathf.Clamp01(d / 100f);
            }
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (handle >= 0)
        {
            handle = -1;
            if (Send(new CraftRequest(CraftRequestKind.Stretch, p.id) { a = tension[0], b = tension[1], c = tension[2], d = tension[3] }))
            {
                float min = Mathf.Min(Mathf.Min(tension[0], tension[1]), Mathf.Min(tension[2], tension[3])), max = Mathf.Max(Mathf.Max(tension[0], tension[1]), Mathf.Max(tension[2], tension[3]));
                said = max > 0.8f ? "Overdrawn: it may tear or dry thin. Ease it off." : max - min > 0.3f ? "Stretched unevenly: pull the other sides to match." : "Stretched evenly.";
            }
            return;
        }
        if (!dragging)
            return;
        dragging = false;
        CraftLocation to = frameRect.Contains(pos) ? CraftLocation.Frame : rackRect.Contains(pos) ? CraftLocation.Rack : CraftLocation.Air;
        if (to != p.location && Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = to }))
            said = to == CraftLocation.Frame ? "Laced in the frame: pull its sides to stretch it." : to == CraftLocation.Rack ? "Hung on the rack to dry." : "Taken down onto the bench.";
    }

    public override void Refresh()
    {
        if (heat != null && vat < Ws.Vats.Count)
            heat.SetValueWithoutNotify((float)Ws.Vats[vat].heat);
        CraftPiece p = Selected;
        if (path != null && state != null)
        {
            if (p != null && p.kind == CraftPieceKind.Hide)
            {
                HideMeans m = Means(p.hide);
                float[] st = Steps(p.hide, m);
                var sb = new System.Text.StringBuilder();
                int next = -1;
                for (int i = 0; i < st.Length; i++)
                {
                    if (i > 0) sb.Append("<color=#6b6f76>  >  </color>");
                    bool done = st[i] >= 0.999f;
                    if (!done && next < 0) next = i;
                    if (done) sb.Append($"<color=#7fd18b>{StepNames[i]}</color>");
                    else if (i == next) sb.Append($"<b><color=#ffcc61>{StepNames[i]}{(st[i] > 0.02f ? $" {st[i] * 100:0}%" : "")}</color></b>");
                    else sb.Append($"<color=#8a8f96>{StepNames[i]}</color>");
                }
                path.text = sb.ToString();
                state.text = $"Flesh left {m.flesh * 100:0}% · hair {m.hair * 100:0}% (loose {m.loose * 100:0}%) · tanned: surface {m.tanS * 100:0}%, core {m.tanC * 100:0}% · wet {m.moisture * 100:0}% · stiff {m.stiff * 100:0}% · oiled {m.oil * 100:0}% · dyed {m.dye * 100:0}%"
                    + (m.damage > 0.02 ? $" · <color=#ff8a70>scored {m.damage * 100:0}%</color>" : "") + (m.rot > 0.05 ? " · <color=#ff8a70>rotting!</color>" : "")
                    + (next >= 0 ? $"\nNext: {StepHow[next]}." : "");
            }
            else
            {
                path.text = "<color=#8a8f96>Flesh  >  Lime  >  Unhair  >  Tan  >  Stretch & dry  >  Stake  >  Finish</color>";
                state.text = "Pick a hide (left) to see what it needs.";
            }
        }
        if (info == null)
            return;
        string how;
        switch (place)
        {
            case Place.Vats:
            {
                string vs = "";
                if (vat < Ws.Vats.Count)
                {
                    CraftVatState v = Ws.Vats[vat];
                    CraftTanningAgent a = Rules.Agent(v.agent);
                    vs = v.volume <= 0.01 ? $"Vat {vat + 1} is empty." : $"Vat {vat + 1}: {a?.Name ?? v.agent}, strength {v.strength * 100:0}% (the gold gauge), {v.temperature:0} °C{(v.stirred > 0.3 ? ", stirred" : "")}.";
                }
                string tip = vatTool == VatTool.Bucket ? "Hold the bucket over a vat to pour (a dose a second)." : vatTool == VatTool.Paddle ? "Hold and circle in a vat to stir it." : vatTool == VatTool.Plug ? "Hold on a vat's plug to drain it." : "Drag the hide from the bench into a vat, or out of it. The bars under a vat: how far the tan has reached the hide's surface (light, upper) and core (dark, lower).";
                how = $"{vs} {tip}";
                break;
            }
            case Place.Frame: how = "Drag the hide from the bench onto the frame (lace it) or the rack (hang it to dry). On the frame, drag its four handles outward: the lacing turns gold when taut, red when overdrawn. The bar above shows how dry it is."; break;
            default:
                how = (FleshSide(tool) ? "<b>Flesh side up</b> (pink border): pink, glistening patches are flesh and fat still on it - stroke them pale. " : "<b>Grain side up</b> (tan border): dark strokes are hair - limed hair scrapes off. ") +
                      "Stroke across the hide with the tool; the cells it worked flash. Darker = wet; the tan creeps in as a browner colour.";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}

/// <summary>
/// The cutting table: lay a pattern on the hide where the leather is good (right-click or scroll turns it; scars and
/// holes under it show in red), click to mark it, then trace its outline with the knife - the line drawn turns green
/// where it follows the outline and red where it wanders; a bar shows how much of the outline is cut. The closer to
/// the line and the steadier, the cleaner the edge. A pattern cannot run off the hide; flaws under it go into the part.
/// </summary>
public sealed class CraftCuttingMode : CraftMode
{
    private TMP_Dropdown pattern;
    private TextMeshProUGUI info;
    private readonly List<string> patterns = new List<string>();
    private int rotation, placedX = -1, placedY = -1;
    private readonly CraftStroke trace = new CraftStroke();
    private readonly List<Vector2> tracePoints = new List<Vector2>();
    private readonly List<Vector2> border = new List<Vector2>();
    private readonly HashSet<int> touched = new HashSet<int>();
    private Rect area = new Rect(30, 20, 660, 360);
    private string said = "";

    public override string Title => "Cut";
    public override CraftDiscipline Discipline => CraftDiscipline.Leatherwork;
    public override bool Available(CraftStation s) => s.Has(CraftTools.LeatherBench);
    public override string Hint => "Click to lay the pattern where the leather is good (right-click or the wheel turns it), then drag the knife round its dashed outline.";

    public override void Build(RectTransform controls)
    {
        patterns.Clear();
        var names = new List<string>();
        foreach (CraftPattern p in Rules.Patterns) { patterns.Add(p.id); names.Add(p.Name); }
        RectTransform r1 = Row(controls);
        Text(r1, "Pattern", 13f, 56f);
        pattern = Drop(r1, names, 0, 220f);
        pattern.onValueChanged.AddListener(_ => Lift());
        Btn(r1, "Turn it", () => rotation = (rotation + 1) % 4, 90f);
        Btn(r1, "Lift the pattern", Lift, 140f, UITheme.ButtonStyle.Ghost);
        info = InfoRow(controls, 40f);
    }

    private void Lift()
    {
        placedX = placedY = -1;
        tracePoints.Clear();
        touched.Clear();
    }

    private CraftPattern Current => patterns.Count > 0 ? Rules.Pattern(patterns[Mathf.Clamp(pattern.value, 0, patterns.Count - 1)]) : null;

    public override void Draw(CraftPixels c)
    {
        // The cutting board.
        c.Rect(area.xMin - 12, area.yMin - 10, area.xMax + 12, area.yMax + 10, new Color(0.42f, 0.33f, 0.24f));
        for (int k = 0; k < 8; k++) c.Line(area.xMin - 12, area.yMin + k * 48, area.xMax + 12, area.yMin + k * 48 + 6, new Color(0.36f, 0.28f, 0.2f), 1f);
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null)
            return;
        CraftArt.DrawHide(c, Rules, p.hide, area);
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        int gx, gy;
        Vector2 h = ui.Hover;
        if (placedX >= 0) { gx = placedX; gy = placedY; }
        else
        {
            if (!area.Contains(h)) return;
            gx = Mathf.FloorToInt((h.x - origin.x) / cell);
            gy = Mathf.FloorToInt((h.y - origin.y) / cell);
        }
        bool fits = CraftLeather.Fits(p.hide, pat, gx, gy, rotation);
        pat.Size(rotation, out int pw, out int ph);
        Color col = placedX >= 0 ? new Color(1f, 1f, 1f, 0.12f) : fits ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(1f, 0.35f, 0.3f, 0.35f);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
                if (pat.Covers(x, y, rotation))
                {
                    float x0 = origin.x + (gx + x) * cell, y0 = origin.y + (gy + y) * cell;
                    c.Rect(x0, y0, x0 + cell, y0 + cell, col);
                    // A scar, a brand or a hole under the pattern goes into the part: shown in red.
                    HideCell hc = p.hide.Cell(gx + x, gy + y);
                    if (hc != null && hc.present && (hc.flaw || hc.damage > 0.3))
                        c.Ring(x0 + cell * 0.5f, y0 + cell * 0.5f, cell * 0.45f, 1.5f, new Color(1f, 0.3f, 0.25f, 0.9f));
                }
        if (placedX >= 0)
        {
            Border(pat, origin, cell);
            // The outline to follow: dashed, the stretches already cut solid.
            for (int i = 0; i < border.Count; i++)
                c.Disc(border[i].x, border[i].y, touched.Contains(i) ? 2.2f : 1.2f, touched.Contains(i) ? new Color(0.5f, 1f, 0.6f, 0.9f) : new Color(1f, 1f, 1f, 0.7f));
            for (int i = 1; i < tracePoints.Count; i++)
            {
                float d = Nearest(tracePoints[i], out _);
                Color line = Color.Lerp(new Color(0.45f, 1f, 0.55f), new Color(1f, 0.35f, 0.3f), Mathf.Clamp01(d / (cell * 1.5f)));
                c.Line(tracePoints[i - 1].x, tracePoints[i - 1].y, tracePoints[i].x, tracePoints[i].y, line, 2f);
            }
            Meter(c, area.xMin, area.yMax + 2, area.width, 6, border.Count > 0 ? touched.Count / (float)border.Count : 0f, new Color(0.5f, 1f, 0.6f));
        }
        // The knife under the pointer.
        if (area.Contains(h))
        {
            c.Line(h.x, h.y, h.x + 12, h.y + 18, new Color(0.8f, 0.82f, 0.86f), 2f);
            c.Line(h.x + 12, h.y + 18, h.x + 22, h.y + 34, new Color(0.45f, 0.3f, 0.18f), 4f);
        }
    }

    /// <summary>The pattern's border cells (their centres in pixels), for following and scoring the knife.</summary>
    private void Border(CraftPattern pat, Vector2 origin, float cell)
    {
        border.Clear();
        pat.Size(rotation, out int pw, out int ph);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                if (!pat.Covers(x, y, rotation)) continue;
                if (pat.Covers(x - 1, y, rotation) && pat.Covers(x + 1, y, rotation) && pat.Covers(x, y - 1, rotation) && pat.Covers(x, y + 1, rotation)) continue;
                border.Add(new Vector2(origin.x + (placedX + x + 0.5f) * cell, origin.y + (placedY + y + 0.5f) * cell));
            }
    }

    private float Nearest(Vector2 t, out int at)
    {
        float best = float.MaxValue;
        at = -1;
        for (int i = 0; i < border.Count; i++)
        {
            float d = Vector2.Distance(t, border[i]);
            if (d < best) { best = d; at = i; }
        }
        return best;
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null)
            return;
        if (phase == CraftPointer.Scroll) { if (placedX < 0) rotation = (rotation + (scroll > 0 ? 1 : 3)) % 4; return; }
        if (right && phase == CraftPointer.Down) { if (placedX < 0) rotation = (rotation + 1) % 4; else Lift(); return; }
        CraftArt.HideLayout(p.hide, area, out Vector2 origin, out float cell);
        if (placedX < 0)
        {
            if (phase != CraftPointer.Down) return;
            int gx = Mathf.FloorToInt((pos.x - origin.x) / cell), gy = Mathf.FloorToInt((pos.y - origin.y) / cell);
            if (!CraftLeather.Fits(p.hide, pat, gx, gy, rotation)) { ui.Message("The pattern does not fit there."); return; }
            placedX = gx;
            placedY = gy;
            tracePoints.Clear();
            touched.Clear();
            said = "Marked. Now drag the knife round the dashed outline, in one go.";
            return;
        }
        if (phase == CraftPointer.Down) { trace.Clear(); tracePoints.Clear(); touched.Clear(); Border(pat, origin, cell); }
        if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
        {
            trace.Add(Time.unscaledTime, pos.x / 720f, pos.y / 400f, 0, 0.5, Hands);
            tracePoints.Add(pos);
            if (Nearest(pos, out int at) < cell && at >= 0) touched.Add(at);
        }
        if (phase == CraftPointer.Up && tracePoints.Count > 6)
        {
            double accuracy = Accuracy(cell);
            if (Send(new CraftRequest(CraftRequestKind.Cut, p.id) { id = pat.id, i0 = placedX, i1 = placedY, i2 = rotation, a = (float)accuracy }))
                said = accuracy > 0.85 ? "A clean cut, right on the line." : accuracy > 0.6 ? "Cut out: the edge wanders a little." : "Cut out, raggedly: the edge will need trimming and burnishing.";
            Lift();
        }
    }

    /// <summary>How well the knife followed the outline: how close each point was to the pattern's border, and how much of the border it went round.</summary>
    private double Accuracy(float cell)
    {
        if (border.Count == 0)
            return 0.5;
        double off = 0;
        foreach (Vector2 t in tracePoints)
            off += Mathf.Clamp01(Nearest(t, out _) / (cell * 1.5f));
        double closeness = 1 - off / tracePoints.Count;
        double coverage = touched.Count / (double)border.Count;
        double steady = Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 4f);
        return Math.Max(0, Math.Min(1, closeness * 0.5 + coverage * 0.4 + steady * 0.1));
    }

    public override void Refresh()
    {
        if (info == null) return;
        string how = placedX < 0
            ? "Move the pattern over the hide: green where it fits, red where it runs off; red rings are scars or holes that would go into the part. Click to mark it."
            : $"Follow the dashed outline with the knife: the line is green on it, red off it. {(border.Count > 0 ? touched.Count * 100 / border.Count : 0)}% of the outline cut. Right-click lifts the pattern.";
        info.text = (how + " " + said).Trim();
    }
}
