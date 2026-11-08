using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tannery, in three places, each with only its own tools in the controls:
/// <list type="bullet">
/// <item><b>The beam</b>: the hide cell by cell (flesh, hair, scores and holes, the tanning creeping in, the dye) laid
/// over the sloping fleshing beam, worked with strokes - flesh it, scrape off the hair loosened by lime, stake it, slick
/// it, skive it thinner, rub in oil or wax, brush on dye. The tool is drawn under the pointer, the stroke leaves a trail,
/// and the cells it really works flash. The gauge under the hide shows the pressure against what the cells under the
/// tool can bear (the knife turns red past it; the skiving knife's line is one pass's); the knife's angle is drawn
/// beside it (steep bites, flat is gentle). The wheel eases or presses the tool as it goes: each stretch of a stroke is
/// worked at the pressure it was drawn with.</item>
/// <item><b>The vats</b>: tubs of liquor the hide soaks in - water to soak it (and, hot, to harden tanned leather), lime
/// to loosen the hair, bark (or alum, or oil) to tan it into leather over hours, vinegar to pickle and etch metal. Pour
/// from the bucket (a dose a second; a vat holding another liquor refuses it before anything is poured), stir with the
/// paddle, hold the plug to drain, drag the piece in, between and out. Each tub has its STRENGTH gauge (red past the
/// line where a raw core's outside closes - case hardening - while the core is raw) and a thermometer with the band the
/// liquor works in; under it, bars for what the work in it is doing (the tan's surface and core, the hair loosening,
/// the hardening, the etch). A fire under a vat heats it.</item>
/// <item><b>Frame and rack</b>: lace the hide in the frame and pull its four handles - each slides along its PULL gauge
/// of what that edge can take (grey slack: it puckers as it dries - every side must hold; green taut: it dries flat;
/// amber overdrawn: the edge thins; red: it tears) - or hang it on the rack, four side by side (it puckers); stake it as
/// it dries, while the dryness bar is in its gold band, and slick, oil or dye it where it hangs (a hide too narrow in its
/// place on the rack is worked close up, over the frame).</item>
/// </list>
/// Under the work, the hide's path (flesh, lime, unhair, tan, stretch, stake and dry, finish) shows what is done and
/// what is next, here or where it must be taken, and its state in words. A test station has quick buttons for all of it.
/// </summary>
public sealed class CraftTanneryMode : CraftMode
{
    // The guide names the places: keep these names.
    private enum Place { Beam, Vats, Frame }
    private enum Tool { Flesh, Unhair, Stake, Slick, Skive, Oil, Dye, Hands }
    private enum VatTool { Hands, Bucket, Paddle, Plug }

    private static readonly Tool[] BeamTools = { Tool.Flesh, Tool.Unhair, Tool.Stake, Tool.Slick, Tool.Skive, Tool.Oil, Tool.Dye };
    private static readonly Tool[] FrameTools = { Tool.Hands, Tool.Stake, Tool.Slick, Tool.Oil, Tool.Dye };
    private static readonly string[] VatToolNames = { "Hands", "Bucket", "Paddle", "Plug" };
    /// <summary>The longest stretch of a stroke sent at once (samples); a longer one goes on in the next request.</summary>
    private const int MaxSegment = 200;
    /// <summary>How far a frame handle slides out (pixels) for the most that side may be pulled.</summary>
    private const float TravelPx = 50f;
    /// <summary>Seconds a hand holds the plug to drain a vat.</summary>
    private const float DrainSeconds = 1.5f;

    private Place place = Place.Beam;
    private Tool beamTool = Tool.Flesh, frameTool = Tool.Hands;
    private VatTool vatTool = VatTool.Hands;
    private Tool InHand => place == Place.Frame ? frameTool : beamTool;
    private bool Strokes => place == Place.Beam || place == Place.Frame && frameTool != Tool.Hands;

    // ------------------------------------------------------------------ the controls
    private TMP_Dropdown agentDrop, oilDrop, dyeDrop, vatChoice;
    private Slider pressure, angle, heat, n, e, s, w;
    private TextMeshProUGUI info, path, state, pressLabel, heatLabel;
    private RectTransform beamRow, frameRow, workRow, angleGroup, oilGroup, dyeGroup, vatRow, vatSetRow, vatQuickRow, frameQuickRow;
    private readonly List<string> agents = new List<string>(), oils = new List<string>(), dyes = new List<string>();
    private readonly List<(Button button, Place place)> placeButtons = new List<(Button, Place)>();
    private readonly List<(Button button, Tool tool)> beamButtons = new List<(Button, Tool)>(), frameButtons = new List<(Button, Tool)>();
    private readonly List<Button> vatButtons = new List<Button>();

    // ------------------------------------------------------------------ what the hands are doing
    // A stroke over the hide: the piece and where it is drawn, the stretch of it not sent yet (cells, row 0 at the top).
    private bool stroking;
    private string strokePiece;
    private Tool strokeTool;
    private Rect strokeArea;
    private readonly List<float> segment = new List<float>();
    private Vector2 lastPoint, pendingTail;
    private bool tailWaiting;
    private readonly CraftStroke stroke = new CraftStroke(); // the hand's tremble on the pressure, the arm tiring by time
    private readonly List<Vector2> trail = new List<Vector2>();
    // The cells the last stroke worked, flashing.
    private readonly List<int> flash = new List<int>();
    private string flashPiece;
    private float flashAt = -9f;
    // The vats: the tub chosen, a piece dragged, the bucket pouring, the plug held, the paddle.
    private Rect area = new Rect(40, 28, 640, 326);
    private Rect benchSpot = new Rect(12, 200, 116, 140);
    private readonly List<Rect> vats = new List<Rect>();
    private readonly List<Rect> plugs = new List<Rect>();
    private int vat;
    private bool dragging, pouring, draining, ignorePress;
    private int dragFromVat = -1, pourVat = -1, pourMax = -1, handle = -1;
    private Vector2 dragAt;
    private float pourTime, drainTime, grab;
    private readonly CraftSwirl paddle = new CraftSwirl();
    private int stirVat = -1;
    // The frame: each side's pull as the hide holds it (or as the hand pulls it now), and a stretch on its way.
    private readonly float[] tension = new float[4];
    private bool stretchPending;
    private string said = "";

    private static readonly Rect FrameRect = new Rect(200, 70, 300, 250);
    private Rect RackRect => Station.Has(CraftTools.Frame) ? new Rect(574, 70, 130, 250) : FrameRect;

    // A leather bench without the tannery's beam, vats and frame: only the finishing (stake, slick, skive, oil, dye).
    private bool BenchOnly => ui != null && ui.Station != null && !ui.Station.Has(CraftTools.FleshingBeam) && !ui.Station.Has(CraftTools.Vats) && !ui.Station.Has(CraftTools.Frame) && !ui.Station.Has(CraftTools.Rack);
    public override string Title => BenchOnly ? "Finish leather" : "Tannery";
    public override CraftDiscipline Discipline => CraftDiscipline.Leatherwork;
    public override bool Available(CraftStation st) => st.Has(CraftTools.FleshingBeam) || st.Has(CraftTools.Vats) || st.Has(CraftTools.Frame) || st.Has(CraftTools.Rack) || st.Has(CraftTools.LeatherBench);

    /// <summary>Is this place at the station (the beam doubles as the bench for finishing)?</summary>
    private bool Here(Place pl) => pl == Place.Beam ? Station.Has(CraftTools.FleshingBeam) || Station.Has(CraftTools.LeatherBench)
        : pl == Place.Vats ? Station.Has(CraftTools.Vats) : Station.Has(CraftTools.Frame) || Station.Has(CraftTools.Rack);

    /// <summary>Can this tool be used here: fleshing and unhairing need the beam; the finishing is done at the beam or a bench.</summary>
    private bool Here(Tool t) => t == Tool.Hands || (t == Tool.Flesh || t == Tool.Unhair ? Station.Has(CraftTools.FleshingBeam) : (Station.Tools & CraftWorkshop.FinishingTools) != 0);

    /// <summary>Goes to a place by the guide's name for it ("Beam", "Vats", "Frame").</summary>
    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Place p) && Here(p)) GoToPlace(p);
    }

    public override bool HasPlace(string place) => Enum.TryParse(place, out Place p) && Here(p);

    public override string PlaceTitle(string place) => Enum.TryParse(place, out Place p) ? PlaceName(p) : place;

    public override string Hint => "The path under the work says what the hide needs next. Beam: stroke the hide with a tool (the wheel eases or presses it as you go). Vats: pour, stir, drain, drag pieces in. Frame & rack: lace, pull, hang, stake as it dries. Right-click puts down.";

    private static bool FleshSide(Tool t) => t == Tool.Flesh || t == Tool.Skive;

    private static string ToolName(Tool t) => t switch
    {
        Tool.Flesh => "Flesh it", Tool.Unhair => "Scrape the hair", Tool.Stake => "Stake it", Tool.Slick => "Slick it",
        Tool.Skive => "Skive it", Tool.Oil => "Oil / wax", Tool.Dye => "Dye it", _ => "Hands",
    };

    private string PlaceName(Place p) => p == Place.Beam ? (Station.Has(CraftTools.FleshingBeam) ? "The beam" : "The bench")
        : p == Place.Vats ? "The vats"
        : Station.Has(CraftTools.Frame) && Station.Has(CraftTools.Rack) ? "Frame & rack" : Station.Has(CraftTools.Frame) ? "The frame" : "The rack";

    /// <summary>How far a tool works round its line (cells): exactly as the workshop works it.</summary>
    private static double Reach(Tool t) => t switch
    {
        Tool.Flesh => CraftLeather.ScrapeReach, Tool.Unhair => CraftLeather.ScrapeReach, Tool.Stake => CraftLeather.StakeReach,
        Tool.Slick => CraftLeather.SlickReach, Tool.Skive => CraftLeather.SkiveReach, Tool.Oil => CraftLeather.OilReach, _ => CraftLeather.DyeReach,
    };

    // ------------------------------------------------------------------ building
    public override void Build(RectTransform controls)
    {
        placeButtons.Clear();
        beamButtons.Clear();
        frameButtons.Clear();
        vatButtons.Clear();
        if (!Here(place)) place = Here(Place.Beam) ? Place.Beam : Here(Place.Vats) ? Place.Vats : Place.Frame;
        if (!Here(beamTool)) beamTool = Here(Tool.Flesh) ? Tool.Flesh : Tool.Stake;
        if (!Here(frameTool)) frameTool = Tool.Hands;
        RectTransform r0 = Row(controls, 30f, 4f);
        foreach (Place pl in new[] { Place.Beam, Place.Vats, Place.Frame })
        {
            Place p = pl;
            Button b = Btn(r0, PlaceName(p), () => GoToPlace(p), 0f, UITheme.ButtonStyle.Tab, 28f);
            b.gameObject.SetActive(Here(p));
            placeButtons.Add((b, p));
        }
        // The beam's tools; the frame's (the hands lace, pull and hang; the finishing tools work the hide where it hangs).
        beamRow = Row(controls, 30f, 3f);
        foreach (Tool t in BeamTools)
        {
            Tool tt = t;
            beamButtons.Add((Btn(beamRow, ToolName(t), () => Choose(tt), 0f, UITheme.ButtonStyle.Tab, 28f), t));
        }
        frameRow = Row(controls, 30f, 3f);
        foreach (Tool t in FrameTools)
        {
            Tool tt = t;
            frameButtons.Add((Btn(frameRow, t == Tool.Hands ? "Lace, pull, hang" : ToolName(t), () => Choose(tt), 0f, UITheme.ButtonStyle.Tab, 28f), t));
        }
        // How the tool is held: the pressure (the depth, skiving; the wheel turns it too), the knife's angle, what is rubbed in.
        workRow = Row(controls, 34f, 8f);
        RectTransform press = Group(workRow);
        pressLabel = Text(press, "Pressure", 13f, 62f);
        // the skiving knife's depth in what one pass takes off at most (CraftLeather.SkivePass); the rest by how hard
        pressure = Slide(press, "", 0.05f, 1f, 0.35f, false, 0f, v => InHand == Tool.Skive ? $"{v * CraftLeather.SkivePass:0.00} mm" : $"{v * 100:0}%");
        angleGroup = Group(workRow);
        angle = Slide(angleGroup, "Knife angle", 0f, 1f, 0.7f, false, 74f, v => v < 0.35f ? "flat" : v > 0.75f ? "steep" : "middling");
        oils.Clear();
        dyes.Clear();
        foreach (CraftSupplyDefinition x in Rules.Supplies)
        {
            if (x.kind == CraftSupplyKind.Oil || x.kind == CraftSupplyKind.Wax) oils.Add(x.id);
            else if (x.kind == CraftSupplyKind.Dye) dyes.Add(x.id);
        }
        oilGroup = Group(workRow);
        Text(oilGroup, "Rub in", 13f, 44f);
        oilDrop = CountedDrop(oilGroup, oils, 210f, "No oil or wax");
        dyeGroup = Group(workRow);
        Text(dyeGroup, "Dye", 13f, 30f);
        dyeDrop = CountedDrop(dyeGroup, dyes, 210f, "No dye");
        // The vats: the hand's tool, the agent in the bucket, the fire under the vat chosen.
        vatRow = Row(controls, 30f, 4f);
        for (int i = 0; i < VatToolNames.Length; i++)
        {
            VatTool vt = (VatTool)i;
            vatButtons.Add(Btn(vatRow, VatToolNames[i], () => { vatTool = vt; StopGestures(); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        vatSetRow = Row(controls, 34f, 8f);
        Text(vatSetRow, "In the bucket", 13f, 86f);
        agents.Clear();
        foreach (CraftTanningAgent a in Rules.Agents) agents.Add(a.id);
        agentDrop = CountedDrop(vatSetRow, agents, 200f, "No agent");
        heatLabel = Text(vatSetRow, "Fire under vat 1", 13f, 104f);
        heat = SettingSlider(vatSetRow, "", 0f, 1f, () => vat >= 0 && vat < Ws.Vats.Count ? (float)Ws.Vats[vat].heat : 0f,
            v => Send(new CraftRequest(CraftRequestKind.VatHeat) { i0 = vat, a = v }), FireWords, false, 0f);
        if (QuickBench)
        {
            vatQuickRow = Row(controls, 34f, 8f);
            var vn = new List<string>();
            for (int i = 0; i < Ws.Vats.Count; i++) vn.Add($"Vat {i + 1}");
            vatChoice = Drop(vatQuickRow, vn.Count > 0 ? vn : new List<string> { "-" }, 0, 80f);
            vatChoice.onValueChanged.AddListener(v => vat = v);
            Btn(vatQuickRow, "Fill / +dose", () => { if (agents.Count > 0) Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = vat, id = AgentId, count = 1 }); }, 100f);
            Btn(vatQuickRow, "Drain", () => Send(new CraftRequest(CraftRequestKind.DrainVat) { i0 = vat }), 60f);
            Btn(vatQuickRow, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = vat }), 50f);
            Btn(vatQuickRow, "Into the vat", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Vat, i2 = vat }), 100f);
            frameQuickRow = Row(controls, 34f, 6f);
            Btn(frameQuickRow, "Hang", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Rack }), 54f);
            Btn(frameQuickRow, "Frame", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Frame }), 58f);
            Btn(frameQuickRow, "Take down", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 84f);
            n = Slide(frameQuickRow, "N", 0f, 1f, 0.4f, false, 14f);
            e = Slide(frameQuickRow, "E", 0f, 1f, 0.4f, false, 14f);
            s = Slide(frameQuickRow, "S", 0f, 1f, 0.4f, false, 14f);
            w = Slide(frameQuickRow, "W", 0f, 1f, 0.4f, false, 14f);
            Btn(frameQuickRow, "Stretch", () => Send(new CraftRequest(CraftRequestKind.Stretch, ui.SelectedId) { a = n.value, b = e.value, c = s.value, d = w.value }), 74f);
        }
        // Always: the hide's path and what is next, its state in numbers, then how the place is worked.
        RectTransform p1 = Row(controls, 30f);
        path = UITheme.Label(p1, "", 13f, UITheme.Text);
        InventoryUIFactory.Size(path, -1f, 30f, 1f);
        FitInside(path);
        RectTransform p2 = Row(controls, 22f);
        state = UITheme.Label(p2, "", 12f, UITheme.Muted);
        InventoryUIFactory.Size(state, -1f, 22f, 1f);
        FitInside(state);
        state.fontSizeMax = 12f;
        info = InfoRow(controls, 40f);
        LoadTension(Selected);
        Show();
        ShowTools(Selected);
    }

    /// <summary>A part of a row that is shown or hidden as one (a slider with its label and value).</summary>
    private static RectTransform Group(RectTransform row)
    {
        RectTransform g = UITheme.Row("Group", row, 30f, 6f);
        InventoryUIFactory.Size(g, -1f, 30f, 1f);
        return g;
    }

    private void Choose(Tool t)
    {
        StopGestures();
        if (place == Place.Frame) frameTool = t;
        else beamTool = t;
        Show();
    }

    private void GoToPlace(Place p)
    {
        StopGestures();
        place = p;
        said = "";
        Show();
    }

    private void Show()
    {
        foreach ((Button b, Place p) in placeButtons) UITheme.SetSelected(b, p == place);
        foreach ((Button b, Tool t) in beamButtons) UITheme.SetSelected(b, t == beamTool);
        foreach ((Button b, Tool t) in frameButtons) UITheme.SetSelected(b, t == frameTool);
        for (int i = 0; i < vatButtons.Count; i++) UITheme.SetSelected(vatButtons[i], (VatTool)i == vatTool);
        if (beamRow == null)
            return;
        beamRow.gameObject.SetActive(place == Place.Beam);
        frameRow.gameObject.SetActive(place == Place.Frame);
        workRow.gameObject.SetActive(Strokes);
        Tool tool = InHand;
        angleGroup.gameObject.SetActive(tool == Tool.Flesh);
        oilGroup.gameObject.SetActive(tool == Tool.Oil);
        dyeGroup.gameObject.SetActive(tool == Tool.Dye);
        pressLabel.text = tool == Tool.Skive ? "Depth" : "Pressure";
        if (pressure != null) SetSlider(pressure, pressure.value); // its readout in the tool's own terms (mm a pass for the skiving knife)
        vatRow.gameObject.SetActive(place == Place.Vats);
        vatSetRow.gameObject.SetActive(place == Place.Vats);
        if (vatQuickRow != null) vatQuickRow.gameObject.SetActive(place == Place.Vats);
        if (frameQuickRow != null) frameQuickRow.gameObject.SetActive(place == Place.Frame && frameTool == Tool.Hands);
    }

    /// <summary>The tools for the piece in hand: cloth takes only oil, wax and dye here; a tool the station lacks is not shown.</summary>
    private void ShowTools(CraftPiece p)
    {
        bool cloth = CraftTextiles.IsCloth(Rules, p);
        bool Fits(Tool t) => Here(t) && (!cloth || t == Tool.Oil || t == Tool.Dye || t == Tool.Hands);
        foreach ((Button b, Tool t) in beamButtons)
            if (b.gameObject.activeSelf != Fits(t)) b.gameObject.SetActive(Fits(t));
        foreach ((Button b, Tool t) in frameButtons)
            if (b.gameObject.activeSelf != Fits(t)) b.gameObject.SetActive(Fits(t));
        bool changed = false;
        if (!Fits(beamTool)) { beamTool = Fits(Tool.Flesh) ? Tool.Flesh : Fits(Tool.Stake) ? Tool.Stake : Tool.Dye; changed = true; }
        if (!Fits(frameTool)) { frameTool = Tool.Hands; changed = true; }
        if (changed) { StopGestures(); Show(); }
    }

    // ------------------------------------------------------------------ leaving, the piece changing
    /// <summary>The hands stop: a stroke on the hide is sent as far as it went, a pour pours what was poured, the rest is put down.</summary>
    private void StopGestures()
    {
        if (stroking) FlushSegment(false);
        stroking = false;
        segment.Clear();
        stroke.Clear();
        tailWaiting = false;
        trail.Clear();
        if (pouring) FinishPour();
        dragging = draining = false;
        dragFromVat = -1;
        if (handle >= 0)
        {
            handle = -1;
            LoadTension(Selected); // let go without pulling: it springs back to what it held
        }
        paddle.Clear();
        stirVat = -1;
        ignorePress = false;
        LiveDraw = false;
    }

    public override void Leave() => StopGestures();

    public override void SelectionChanging(string from, string to)
    {
        StopGestures(); // a stroke under way lands on the piece it was made on (it is still the one picked)
        flash.Clear();
        flashPiece = null;
        stretchPending = false;
        said = "";
    }

    public override void SelectionChanged(string to) => LoadTension(to != null ? Ws.Piece(to) : null);

    /// <summary>The frame's handles as the hide holds them (all slack when it is not in the frame).</summary>
    private void LoadTension(CraftPiece p)
    {
        HideState h = p != null && p.kind == CraftPieceKind.Hide && p.location == CraftLocation.Frame ? p.hide : null;
        tension[0] = h != null ? (float)h.tensionN : 0f;
        tension[1] = h != null ? (float)h.tensionE : 0f;
        tension[2] = h != null ? (float)h.tensionS : 0f;
        tension[3] = h != null ? (float)h.tensionW : 0f;
    }

    // ------------------------------------------------------------------ every frame
    public override void Tick(float dt)
    {
        if (pouring)
        {
            if (!ui.PointerHeld) FinishPour();
            else if (OverTub(pourVat, ui.Hover))
            {
                pourTime += dt;
                int full = PourCap(pourVat);
                float cap = pourMax >= 0 ? Mathf.Min(pourMax, full) : full;
                if (pourTime >= cap + 0.999f)
                {
                    pourTime = cap + 0.999f;
                    said = PourEnough(pourVat);
                }
            }
        }
        if (draining)
        {
            if (!ui.PointerHeld) draining = false;
            else if ((drainTime += dt) >= DrainSeconds)
            {
                draining = false;
                int at = vat;
                Send(new CraftRequest(CraftRequestKind.DrainVat) { i0 = at }, (ok, why) => { if (ok) said = $"Vat {at + 1} drained: the plug is back in."; });
            }
        }
        Vector2 h = ui.Hover;
        LiveDraw = stroking || dragging || handle >= 0 || pouring || draining || paddle.Active || Time.unscaledTime - flashAt < 0.9f
                   || h.x >= 0 && (Strokes || place == Place.Vats && vatTool != VatTool.Hands);
    }

    // ------------------------------------------------------------------ what the hide needs
    private struct HideMeans
    {
        public double flesh, hair, loose, tanS, tanC, coreLeast, moisture, wettest, stiff, hard, oil, wax, dye, smooth, slick, grain, damage, scar, rot, thick, thin, thickest;
        public int cells;
    }

    private static HideMeans Means(HideState h)
    {
        var m = new HideMeans { coreLeast = 1, thin = double.MaxValue };
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            m.cells++;
            m.flesh += c.flesh; m.hair += c.hair; m.loose += c.hairLoose; m.tanS += c.tanS; m.tanC += c.tanC; m.moisture += c.moisture;
            m.stiff += c.stiff; m.hard += c.hard; m.oil += c.oil; m.wax += c.wax; m.dye += c.dye; m.smooth += c.smooth; m.slick += c.smooth * c.grain; m.grain += c.grain;
            m.damage += c.damage; m.scar += Math.Min(c.damage, c.scar); m.rot += c.rot;
            double t = CraftLeather.CellThickness(c);
            m.thick += t;
            m.thin = Math.Min(m.thin, t);
            m.thickest = Math.Max(m.thickest, t);
            m.coreLeast = Math.Min(m.coreLeast, c.tanC);
            m.wettest = Math.Max(m.wettest, c.moisture);
        }
        if (m.cells == 0) { m.thin = 0; return m; }
        double k = 1.0 / m.cells;
        m.flesh *= k; m.hair *= k; m.loose *= k; m.tanS *= k; m.tanC *= k; m.moisture *= k; m.stiff *= k; m.hard *= k; m.oil *= k; m.wax *= k; m.dye *= k;
        m.smooth *= k; m.slick *= k; m.grain *= k; m.damage *= k; m.scar *= k; m.rot *= k; m.thick *= k;
        return m;
    }

    private enum Step { Flesh, Lime, Unhair, Tan, Dry, Finish }
    private static readonly string[] StepNames = { "Flesh", "Lime", "Unhair", "Tan", "Stretch, stake & dry", "Finish" };

    /// <summary>
    /// How far each step is (0-1) for this hide, by the thresholds the guide reads it with too: flesh under 8%, hair
    /// under 10% (<see cref="CraftLeather.UnhairedBelow"/>, or loosened 70%: limed; the hair left by the evaluator's own
    /// measure, <see cref="CraftLeather.HairLeft(CraftHideDefinition, double)"/>), the core tanned 75%, dried with
    /// stiffness under 35%, a finish on it.
    /// </summary>
    private float[] Steps(CraftPiece p, HideMeans m)
    {
        HideState h = p.hide;
        bool tanned = m.tanC > 0.75;
        float drying = h.dried ? (m.stiff < 0.35 ? 1f : 0.9f)
            : tanned ? 0.9f * Mathf.Clamp01((float)((0.75 - m.moisture) / (0.75 - CraftLeather.DryAt))) : 0f;
        float finish = Mathf.Clamp01(Mathf.Max((float)((m.oil + m.wax) / 0.2), Mathf.Max((float)(m.dye / 0.3), (float)(m.slick / 0.3))));
        return new[]
        {
            m.flesh < 0.08 ? 1f : Mathf.Clamp01(1f - (float)m.flesh),
            m.hair < CraftLeather.UnhairedBelow || m.loose >= 0.7 ? 1f : Mathf.Clamp01((float)m.loose / 0.7f),
            1f - (float)CraftLeather.HairLeft(Rules.Hide(h.hide), m.hair),
            tanned ? 1f : Mathf.Clamp01((float)m.tanC / 0.75f),
            drying,
            finish,
        };
    }

    private bool Stakes => (Station.Tools & CraftWorkshop.FinishingTools) != 0;

    /// <summary>Does the station have what step <paramref name="st"/> needs now (a dried, stiff hide: something to stake it on)?</summary>
    private bool CanDo(Step st, CraftPiece p) => st switch
    {
        Step.Flesh => Station.Has(CraftTools.FleshingBeam),
        Step.Unhair => Station.Has(CraftTools.FleshingBeam),
        Step.Lime => Station.Has(CraftTools.Vats),
        Step.Tan => Station.Has(CraftTools.Vats),
        Step.Dry => !p.hide.dried || Stakes, // it dries anywhere (on the bench it puckers)
        _ => Stakes,
    };

    /// <summary>What to do for step <paramref name="st"/>, said for this station and this hide (or where to take it); one short line.</summary>
    private string StepHow(Step st, CraftPiece p, HideMeans m)
    {
        // Soaking already where the step is done: how far it has to go, read on the bars under its tub.
        CraftVatState vat = p.location == CraftLocation.Vat && p.vat >= 0 && p.vat < Ws.Vats.Count ? Ws.Vats[p.vat] : null;
        CraftTanningAgent soak = vat != null && vat.volume > 0.01 ? Rules.Agent(vat.agent) : null;
        bool liquor = soak != null && soak.kind != CraftAgentKind.Water && soak.kind != CraftAgentKind.Lime && soak.kind != CraftAgentKind.Acid;
        if (st == Step.Lime && soak?.kind == CraftAgentKind.Lime)
            return $"it is liming in vat {p.vat + 1}: lift it out once LOOSE passes 70% (past 95% the grain spoils)";
        if (st == Step.Tan && liquor)
            return $"it is tanning in vat {p.vat + 1}: strengthen and stir it until CORE passes 75%";
        string how = StepWork(st, p, m);
        if (!CanDo(st, p))
            return how;
        // Elsewhere: first out of where it is.
        if (p.location == CraftLocation.Vat)
            return $"lift it out of vat {p.vat + 1}, then {how}";
        if ((p.location == CraftLocation.Frame || p.location == CraftLocation.Rack) && st != Step.Dry && st != Step.Finish)
            return $"take it down onto the bench, then {how}";
        return how;
    }

    /// <summary>The work of step <paramref name="st"/> at this station (or where to take the hide), wherever it is now.</summary>
    private string StepWork(Step st, CraftPiece p, HideMeans m)
    {
        bool frame = Station.Has(CraftTools.Frame), rack = Station.Has(CraftTools.Rack);
        switch (st)
        {
            case Step.Flesh:
                return CanDo(st, p) ? "on the beam, Flesh it (flesh side up): keep the pressure in the gauge's green" : "flesh it on a fleshing beam: there is none here";
            case Step.Lime:
                return CanDo(st, p) ? "lime it in a vat for some hours to loosen the hair (about 30 °C is fastest; hotter is no faster)" : "lime it in a tannery's vats: there are none here";
            case Step.Unhair:
                return CanDo(st, p) ? "on the beam, Scrape the hair (grain side up), gently: hard scuffs the grain" : "scrape the hair off on a fleshing beam: there is none here";
            case Step.Tan:
                return CanDo(st, p) ? "tan it in a vat: start weak, strengthen and stir it until the core is tanned" : "tan it in a tannery's vats: there are none here";
            case Step.Dry:
                if (p.hide.dried)
                    return !Stakes ? "it dried stiff: stake it on a fleshing beam or a leather bench"
                        // bark-tanned grain cracks staked dry: oil softens it and does it no harm (as the guide says)
                        : CraftLeather.CracksStakedDry(Rules, p.hide) ? "it dried stiff: Oil it supple, dyed first if it is to be (staked dry, bark grain cracks)"
                        : "it dried stiff: Stake it supple (dry, it softens slowly)";
                if (p.location != CraftLocation.Vat && m.moisture <= CraftLeather.StakeWet && m.moisture > CraftLeather.DryAt)
                    return Stakes ? "stake it now, while damp, and again as it dries until it is dry (staked dry, bark grain cracks)"
                        : "it is drying: stake it while damp, on a fleshing beam or a leather bench";
                if (p.location == CraftLocation.Frame)
                    return "it is drying in the frame: keep every side in the green; stake it once the DRY bar reaches its gold band";
                if (p.location == CraftLocation.Rack)
                    return "it is drying on the rack; stake it once the DRY bar reaches its gold band";
                return frame ? "lace it in the frame, pull each side into the green, let it dry; stake it as it dries"
                    : rack ? "hang it on the rack to dry (it puckers a little); stake it as it dries"
                    : "let it dry (unstretched it puckers); stake it as it dries";
            default:
                return CanDo(st, p) ? "dye it first (oil and wax keep dye out), then oil or wax it, then slick it" : "finish it on a fleshing beam or a leather bench";
        }
    }

    private void RefreshPath(CraftPiece p)
    {
        if (path == null || state == null)
            return;
        if (p == null || p.kind != CraftPieceKind.Hide)
        {
            path.text = "<color=#8a8f96>Flesh  >  Lime  >  Unhair  >  Tan  >  Stretch, stake & dry  >  Finish</color>\n<color=#8a8f96>Pick a hide on the left to see what it needs next.</color>";
            bool metal = p != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners);
            state.text = p == null ? "" : !metal ? "That is not a hide."
                : Station.Has(CraftTools.Vats) ? "Metal: at the vats, lower it into a vinegar pickle - the scale lifts, then bright metal etches."
                : "Metal is pickled and etched in a tannery's vats.";
            return;
        }
        HideMeans m = Means(p.hide);
        if (CraftTextiles.IsCloth(Rules, p))
        {
            string cut = !string.IsNullOrEmpty(p.hide.pattern) ? "fit it into an item and sew its seams (Assemble)"
                : Station.Has(CraftTools.TailorTable) ? "cut it at the tailor's table (Tailor → Cut cloth)"
                : Station.Has(CraftTools.LeatherBench) ? "cut it on the cutting table (Cut), along its grain" : "take it to a tailor's table to cut it";
            path.text = "<color=#8a8f96>Cloth: nothing to flesh, tan or dry - it is cut and sewn.</color>\n" +
                        $"Next: dye or oil it here if it wants it, then {cut}.";
            state.text = $"dyed {m.dye * 100:0}% · oiled {m.oil * 100:0}% · waxed {m.wax * 100:0}% · {Weight(m.thick)}";
            return;
        }
        float[] st = Steps(p, m);
        var sb = new System.Text.StringBuilder();
        int next = -1;
        for (int i = 0; i < st.Length; i++)
        {
            if (i > 0) sb.Append("<color=#6b6f76>  >  </color>");
            bool done = st[i] >= 0.999f;
            if (!done && next < 0) next = i;
            if (done) sb.Append($"<color=#7fd18b>{StepNames[i]}</color>");
            else if (i == next) sb.Append(CanDo((Step)i, p) ? $"<b><color=#ffcc61>{StepNames[i]}{(st[i] > 0.02f ? $" {st[i] * 100:0}%" : "")}</color></b>" : $"<b><color=#b08a6a>{StepNames[i]} (not here)</color></b>");
            else sb.Append($"<color=#8a8f96>{StepNames[i]}</color>");
        }
        string nextText;
        if (next >= 0)
            nextText = CanDo((Step)next, p) ? $"Next: {StepHow((Step)next, p, m)}." : $"<color=#a0a4aa>Next: {StepHow((Step)next, p, m)}.</color>";
        else if (!string.IsNullOrEmpty(p.hide.pattern))
            nextText = "Next: fit it into an item (Assemble) - slick its edges burnished first.";
        else
        {
            string down = p.location == CraftLocation.Frame || p.location == CraftLocation.Rack ? "take it down onto the bench (drag it off), then "
                : p.location == CraftLocation.Vat ? "lift it out of the vat, then " : "";
            nextText = Station.Has(CraftTools.LeatherBench) ? $"Next: {down}cut its patterns (Cut)." : $"Next: {down}take it to a leather bench to cut its patterns.";
        }
        path.text = sb.Append('\n').Append(nextText).ToString();
        // the work's damage by what did it (the scars it was born with apart), and rot only while it rots
        DamageBy(p, out double knife, out double torn, out double other);
        double rotting = CraftLeather.Rotting(Ws, p, out bool spoiling);
        state.text = $"flesh {m.flesh * 100:0}% · hair {m.hair * 100:0}% (loose {m.loose * 100:0}%) · tan {m.tanS * 100:0}%, core {m.tanC * 100:0}% · wet {m.moisture * 100:0}% · stiff {m.stiff * 100:0}% · grain {m.grain * 100:0}% · oil {m.oil * 100:0}%, wax {m.wax * 100:0}% · dye {m.dye * 100:0}% · {Weight(m.thick)}"
                     + (m.scar > 0.005 ? $" · scars {m.scar * 100:0}%" : "")
                     + (knife > 0.005 ? $" · <color=#ff8a70>knife {knife * 100:0}%</color>" : "")
                     + (torn > 0.005 ? $" · <color=#ff8a70>torn {torn * 100:0}%</color>" : "")
                     + (other > 0.005 ? $" · <color=#ff8a70>damage {other * 100:0}%</color>" : "")
                     + (spoiling ? " · <color=#ff8a70>rotting!</color>" : rotting >= CraftLeather.SmellsFrom ? " · <color=#ffb070>it starts to smell: lime or tan it</color>" : "");
    }

    /// <summary>
    /// The hide's damage (a share of it, the scars it was born with apart) by what did it (<see cref="CraftLeather.WorkMarks"/>):
    /// the knife (its scores and skived-through holes), the frame (its tears), and the rest - rot, a burning pickle,
    /// cooking, a cut's ragged edge.
    /// </summary>
    private static void DamageBy(CraftPiece p, out double knife, out double torn, out double other)
    {
        knife = torn = other = 0;
        HideState h = p.hide;
        int[] by = CraftLeather.WorkMarks(p);
        int n = 0;
        for (int i = 0; i < h.cells.Count; i++)
        {
            HideCell c = h.cells[i];
            if (!c.present) continue;
            n++;
            double worked = CraftLeather.Worked(c);
            if (by[i] == CraftLeather.ByKnife) knife += worked;
            else if (by[i] == CraftLeather.ByFrame) torn += worked;
            else other += worked;
        }
        if (n == 0) return;
        knife /= n;
        torn /= n;
        other /= n;
    }

    /// <summary>How thick leather feels (with calipers, also in mm).</summary>
    private string Weight(double mm)
    {
        string word = mm < 1.5 ? "glove-thin" : mm < 2.5 ? "garment weight" : mm < 4 ? "belt weight" : mm < 6 ? "sole-thick" : "armour-thick";
        return Station.HasInstrument(CraftInstruments.Calipers) ? $"{word} ({mm:0.0} mm)" : word;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        switch (place)
        {
            case Place.Beam: DrawBeam(c, p); break;
            case Place.Vats: DrawVats(c, p); break;
            default: DrawFramePlace(c, p); break;
        }
    }

    private static readonly List<Vector2> beamShape = new List<Vector2> { new Vector2(10, 40), new Vector2(710, 70), new Vector2(710, 330), new Vector2(10, 360) };

    /// <summary>The rectangle a hide's grid takes when drawn in <paramref name="where"/>.</summary>
    private static Rect GridRect(HideState h, Rect where)
    {
        CraftArt.HideLayout(h, where, out Vector2 origin, out float cell);
        return new Rect(origin.x, origin.y, cell * h.width, cell * h.height);
    }

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
            c.Rect(20, 24, 700, 364, new Color(0.42f, 0.33f, 0.24f));
            for (int k = 0; k < 7; k++) c.Line(20, 24 + k * 50, 700, 24 + k * 50, new Color(0.36f, 0.28f, 0.2f), 1f);
        }
        Tool t = beamTool;
        if (p == null || p.kind != CraftPieceKind.Hide)
        {
            DrawPressGauge(c, t, 1f, false);
            return;
        }
        // Which side is up: the flesh side (pink border) for fleshing and skiving, the grain side (tan border) for the rest.
        Color side = FleshSide(t) ? new Color(0.86f, 0.55f, 0.5f) : new Color(0.7f, 0.55f, 0.35f);
        Rect grid = GridRect(p.hide, area);
        c.Frame(grid.xMin - 4, grid.yMin - 4, grid.xMax + 4, grid.yMax + 4, side, 3f);
        CraftArt.DrawHide(c, Rules, p.hide, area, FleshSide(t));
        if (t == Tool.Stake) StakeTint(c, p.hide, area);
        DrawFlash(c, p, area);
        DrawTrail(c);
        if (p.location != CraftLocation.Air)
        {
            // It is not on the beam: drawn dimmed, with where it is.
            c.Rect(area.xMin, area.yMin, area.xMax, area.yMax, new Color(0.08f, 0.07f, 0.06f, 0.6f));
            string where = p.location == CraftLocation.Vat ? $"IN VAT {p.vat + 1}" : p.location == CraftLocation.Frame ? "IN THE FRAME" : p.location == CraftLocation.Rack ? "ON THE RACK" : "ELSEWHERE";
            CraftArt.Label(c, area.center.x - CraftArt.LabelWidth(where) * 0.5f, area.center.y, where, CraftArt.LabelInk);
        }
        Vector2 h = ui.Hover;
        bool over = h.x >= 0 && grid.Contains(h) && p.location == CraftLocation.Air;
        float safe = SafeLevel(p, area, h, t, over);
        if (over) DrawTool(c, h, t, pressure != null && pressure.value > safe + 1e-3f);
        DrawPressGauge(c, t, safe, over);
        if (t == Tool.Flesh) DrawAngleInset(c);
    }

    /// <summary>With the staking blade in hand: where the hide is too wet (blue) or too dry (red) to stake well.</summary>
    private void StakeTint(CraftPixels c, HideState h, Rect where)
    {
        CraftTanningAgent a = Rules.Agent(h.agent);
        bool bark = a != null && a.kind == CraftAgentKind.Vegetable;
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                HideCell hc = h.Cell(x, y);
                if (hc == null || !hc.present) continue;
                Color tint = hc.moisture > CraftLeather.StakeWet ? new Color(0.3f, 0.55f, 1f, 0.32f)
                    : hc.moisture <= CraftLeather.StakeDry ? (bark ? new Color(1f, 0.3f, 0.25f, 0.32f) : new Color(1f, 0.6f, 0.3f, 0.22f)) : Color.clear;
                if (tint.a <= 0f) continue;
                Rect r = CraftArt.HideCellRect(h, where, x, y);
                c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, tint);
            }
    }

    /// <summary>The cells the last stroke worked, fading (where the hide it was made on is drawn).</summary>
    private void DrawFlash(CraftPixels c, CraftPiece p, Rect where)
    {
        float since = Time.unscaledTime - flashAt;
        if (since >= 0.9f || p == null || p.id != flashPiece)
            return;
        Color col = new Color(1f, 1f, 0.85f, 0.4f * (1f - since / 0.9f));
        foreach (int i in flash)
        {
            if (i < 0 || p.hide.width <= 0) continue;
            Rect r = CraftArt.HideCellRect(p.hide, where, i % p.hide.width, i / p.hide.width);
            c.Rect(r.xMin + 1, r.yMin + 1, r.xMax - 1, r.yMax - 1, col);
        }
    }

    private void DrawTrail(CraftPixels c)
    {
        for (int i = 1; i < trail.Count; i++)
            c.Line(trail[i - 1].x, trail[i - 1].y, trail[i].x, trail[i].y, new Color(1f, 0.95f, 0.8f, 0.6f), 2f);
    }

    /// <summary>
    /// The most the tool in hand can bear on the cells it would work at <paramref name="at"/> (0-1 of the pressure; 1 =
    /// any): the fleshing knife by the clean or thin cells and its angle, the hair knife by the grain (where unlimed hair
    /// holds: none, <see cref="CraftLeather.HairHolds"/>), the skiving knife by how thin the thinnest cell may go before
    /// one pass holes it (<see cref="CraftLeather.SkivePass"/>).
    /// </summary>
    private float SafeLevel(CraftPiece p, Rect where, Vector2 at, Tool t, bool over)
    {
        if (!over || p == null || t != Tool.Flesh && t != Tool.Unhair && t != Tool.Skive)
            return 1f;
        HideState h = p.hide;
        Vector2 v = CraftArt.HideCells(h, where, at);
        double reach = Reach(t), safe = 1;
        bool unlimed = false;
        for (int y = Mathf.FloorToInt(v.y - (float)reach); y <= Mathf.FloorToInt(v.y + (float)reach); y++)
            for (int x = Mathf.FloorToInt(v.x - (float)reach); x <= Mathf.FloorToInt(v.x + (float)reach); x++)
            {
                HideCell c = h.Cell(x, y);
                if (c == null || !c.present || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), v) > reach) continue;
                if (t == Tool.Flesh && CraftLeather.Scorable(c)) safe = Math.Min(safe, CraftLeather.SafeFleshingPressure(c, angle.value));
                else if (t == Tool.Unhair) { safe = Math.Min(safe, CraftLeather.ScuffAbove); unlimed |= CraftLeather.HairHolds(c); }
                else if (t == Tool.Skive) safe = Math.Min(safe, (c.thickness - CraftLeather.SkivedThrough) / CraftLeather.SkivePass);
            }
        return unlimed ? 0f : Mathf.Clamp01((float)safe);
    }

    /// <summary>The gauge under the hide: the pressure (the depth, skiving) against what the cells under the tool can bear.</summary>
    private void DrawPressGauge(CraftPixels c, Tool t, float safe, bool over)
    {
        string label = t == Tool.Skive ? "DEPTH" : "PRESS";
        CraftArt.Label(c, 44, 5, label, CraftArt.LabelInk);
        float v = pressure != null ? pressure.value : 0.35f;
        bool banded = over && (t == Tool.Flesh || t == Tool.Unhair || t == Tool.Skive);
        if (banded)
            Meter(c, 92, 4, 260, 12, v, new Color(0.95f, 0.9f, 0.75f), (0f, safe, GoodBand), (safe, 1f, HarmBand));
        else
            Meter(c, 92, 4, 260, 12, v, new Color(0.95f, 0.9f, 0.75f));
    }

    /// <summary>The fleshing knife's edge against the hide, as steep as the angle slider holds it.</summary>
    private void DrawAngleInset(CraftPixels c)
    {
        float a = angle != null ? angle.value : 0.7f;
        CraftArt.Label(c, 372, 5, "EDGE", CraftArt.LabelInk);
        c.Rect(410, 2, 474, 20, new Color(0f, 0f, 0f, 0.55f));
        c.Line(414, 5, 470, 5, new Color(0.86f, 0.62f, 0.56f), 2f); // the hide's surface
        float deg = (10f + 40f * a) * Mathf.Deg2Rad;
        c.Line(420, 5, 420 + 18f * Mathf.Cos(deg), 5 + 18f * Mathf.Sin(deg), new Color(0.8f, 0.82f, 0.86f), 2f);
    }

    /// <summary>The tool in the hand, under the pointer (the knife red when it bears harder than the cells can take).</summary>
    private void DrawTool(CraftPixels c, Vector2 h, Tool t, bool tooHard)
    {
        Color steel = tooHard ? Harm : new Color(0.75f, 0.76f, 0.8f), wood = new Color(0.45f, 0.3f, 0.18f);
        float a = angle != null ? angle.value : 0.5f;
        switch (t)
        {
            case Tool.Flesh:
            case Tool.Unhair:
                // A two-handled beam knife across the stroke.
                c.Line(h.x - 34, h.y - 6 * a, h.x + 34, h.y + 6 * a, steel, t == Tool.Flesh ? 4f : 3f);
                c.Line(h.x - 46, h.y - 8 * a, h.x - 34, h.y - 6 * a, wood, 5f);
                c.Line(h.x + 34, h.y + 6 * a, h.x + 46, h.y + 8 * a, wood, 5f);
                break;
            case Tool.Stake:
                c.Rect(h.x - 4, h.y, h.x + 4, h.y + 40, wood);
                c.Disc(h.x, h.y, 8f, steel);
                break;
            case Tool.Slick:
                c.Rect(h.x - 18, h.y - 6, h.x + 18, h.y + 6, new Color(0.7f, 0.85f, 0.9f, 0.85f));
                break;
            case Tool.Skive:
                c.Line(h.x - 20, h.y - 10, h.x + 14, h.y + 6, steel, 3f);
                c.Line(h.x + 14, h.y + 6, h.x + 34, h.y + 16, wood, 5f);
                break;
            case Tool.Dye:
            {
                CraftSupplyDefinition d = Chosen(Tool.Dye);
                c.Line(h.x, h.y, h.x + 10, h.y + 34, wood, 4f);
                c.Disc(h.x, h.y, 6f, d != null ? d.color : new Color(0.4f, 0.2f, 0.15f));
                break;
            }
            default:
                c.Disc(h.x, h.y, 12f, new Color(0.9f, 0.88f, 0.8f, 0.85f));
                break;
        }
    }

    // ------------------------------------------------------------------ the vats, drawn
    private CraftPiece SoakingIn(int i)
    {
        foreach (CraftPiece q in Ws.Pieces)
            if (q.location == CraftLocation.Vat && q.vat == i && (q.kind == CraftPieceKind.Hide || q.kind == CraftPieceKind.Metal || q.kind == CraftPieceKind.Fasteners)) return q;
        return null;
    }

    private static Color LiquidColour(CraftTanningAgent a, double strength)
    {
        Color water = new Color(0.3f, 0.45f, 0.6f);
        Color own = a != null ? a.color : water;
        return a == null || a.kind == CraftAgentKind.Water ? water : Color.Lerp(water, own, 0.4f + 0.6f * (float)strength);
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
            CraftTanningAgent a = Rules.Agent(v.agent);
            if (v.heat > 0.02)
                for (int k = 0; k < 6; k++)
                    c.Line(r.xMin + 10 + k * (r.width - 20) / 5f, r.yMin - 18, r.xMin + 12 + k * (r.width - 20) / 5f + 3 * Mathf.Sin(Time.unscaledTime * 6 + k), r.yMin - 2, new Color(1f, 0.55f, 0.15f, (float)v.heat), 5f);
            c.Rect(r.xMin - 6, r.yMin - 4, r.xMax + 6, r.yMax, Wood);
            // Hoops round the tub.
            c.Rect(r.xMin - 7, r.yMin + 40, r.xMax + 7, r.yMin + 46, Iron);
            c.Rect(r.xMin - 7, r.yMax - 50, r.xMax + 7, r.yMax - 44, Iron);
            c.Rect(r.xMin, r.yMin, r.xMax, r.yMax - 4, new Color(0.1f, 0.08f, 0.06f));
            float level = Mathf.Clamp01((float)(v.volume / 60.0));
            if (draining && i == vat) level *= 1f - drainTime / DrainSeconds;
            float top = r.yMin + (r.height - 8) * level;
            Color liquid = LiquidColour(a, v.strength);
            // What soaks in it: drawn as it is (a hide's colour turns as it tans), under the liquor.
            CraftPiece soaking = SoakingIn(i);
            if (soaking != null && dragging && dragFromVat == i && p != null && soaking.id == p.id) soaking = null;
            if (soaking != null)
            {
                var spot = new Rect(r.xMin + 14, r.yMin + 14, r.width - 28, Mathf.Max(30f, (r.height - 40) * Mathf.Max(0.35f, level) * 0.85f));
                if (soaking.kind == CraftPieceKind.Hide) CraftArt.DrawHide(c, Rules, soaking.hide, spot);
                else CraftArt.DrawTop(c, Rules, soaking, new Rect(spot.xMin, spot.center.y - 20, spot.width, 40), Fit(soaking, spot.width, 40), null, false, ui.Dark);
            }
            if (level > 0.01f)
            {
                c.Rect(r.xMin, r.yMin, r.xMax, top, new Color(liquid.r, liquid.g, liquid.b, soaking != null ? 0.55f : 0.92f));
                c.Rect(r.xMin, top - 3, r.xMax, top, Color.Lerp(liquid, Color.white, 0.3f));
                if (v.stirred > 0.2)
                    for (int k = 0; k < 3; k++)
                        c.Ring(r.center.x, top - 6, 8 + k * 10 + (Time.unscaledTime * 12f) % 10f, 1f, new Color(1f, 1f, 1f, 0.25f * (float)v.stirred));
                if (v.temperature > 45)
                    for (int k = 0; k < 4; k++)
                        c.Line(r.xMin + 18 + k * (r.width - 36) / 3f, top + 4, r.xMin + 22 + k * (r.width - 36) / 3f + 4 * Mathf.Sin(Time.unscaledTime * 2 + k), top + 26, new Color(1f, 1f, 1f, 0.3f), 2f);
                StrengthGauge(c, r, v, a, soaking);
                Thermometer(c, r, v, a, soaking);
                // the gauges' names over the tub, clear of the steam off a hot liquor (spelt out where the tub is wide enough)
                CraftArt.Label(c, r.xMin, 300, r.width >= 100f ? "STRENGTH" : "STR", CraftArt.LabelInk);
                CraftArt.Label(c, r.xMax - CraftArt.LabelWidth("HEAT"), 300, "HEAT", CraftArt.LabelInk);
            }
            if (soaking != null) WorkBars(c, r, a, soaking);
            c.Disc(plugs[i].center.x, plugs[i].center.y, 7f, vatTool == VatTool.Plug && plugs[i].Contains(ui.Hover) ? Mark : new Color(0.35f, 0.25f, 0.15f));
            // its number, low in the tub (what the line under the vats and the fire's slider call it)
            string number = r.width >= 110f ? $"VAT {i + 1}" : $"{i + 1}";
            CraftArt.Label(c, r.center.x - CraftArt.LabelWidth(number) * 0.5f, r.yMin + 3, number, i == vat ? CraftArt.LabelInk : CraftArt.LabelDim);
            if (i == vat) c.Frame(r.xMin - 8, r.yMin - 6, r.xMax + 8, r.yMax + 2, Mark, 2f);
            if (draining && i == vat)
            {
                Meter(c, r.xMin, 316, r.width, 8, drainTime / DrainSeconds, Mark);
                CraftArt.Label(c, r.center.x - CraftArt.LabelWidth("DRAIN") * 0.5f, 328, "DRAIN", CraftArt.LabelInk);
                c.Line(plugs[i].center.x, plugs[i].center.y, r.xMax + 6, r.yMin + 4, liquid, 3f); // out through the plug hole
            }
        }
        // The bucket of the agent chosen (on its stand when it is not in hand, tipped over a tub while it pours), and the paddle.
        CraftTanningAgent ba = Rules.Agent(AgentId);
        Vector2 hp = ui.Hover;
        bool inHand = vatTool == VatTool.Bucket && hp.x >= 0;
        if (!inHand)
        {
            c.Polygon(new List<Vector2> { new Vector2(30, 60), new Vector2(120, 60), new Vector2(130, 160), new Vector2(20, 160) }, Iron);
            c.Rect(32, 130, 118, 152, LiquidColour(ba, 1));
        }
        else if (pouring && pourVat >= 0 && pourVat < vats.Count)
            DrawPour(c, pourVat);
        else
        {
            // in the hand, under the pointer (kept on the surface)
            float bx = Mathf.Clamp(hp.x, 20f, 700f), by = Mathf.Min(hp.y, 358f);
            c.Polygon(new List<Vector2> { new Vector2(bx - 14, by + 10), new Vector2(bx + 14, by + 10), new Vector2(bx + 18, by + 40), new Vector2(bx - 18, by + 40) }, Iron);
            c.Rect(bx - 15, by + 32, bx + 15, by + 38, LiquidColour(ba, 1));
        }
        if (vatTool == VatTool.Paddle && VatAt(hp) >= 0) { c.Line(hp.x, hp.y, hp.x + 20, hp.y + 110, Wood, 5f); c.Rect(hp.x - 10, hp.y - 14, hp.x + 10, hp.y + 4, Wood); }
        DrawBench(c, p, true);
    }

    /// <summary>
    /// The bucket tipped over tub <paramref name="i"/> - held above the tubs' labels, its stream falling clear of them into
    /// the liquor - and over it the doses poured so far: one tick a dose, as many as this pour may take.
    /// </summary>
    private void DrawPour(CraftPixels c, int i)
    {
        Rect r = vats[i];
        CraftVatState v = Ws.Vats[i];
        float top = r.yMin + (r.height - 8) * Mathf.Clamp01((float)(v.volume / 60.0));
        Vector2 hv = ui.Hover;
        float bx = Mathf.Clamp(hv.x, r.xMin + 40f, Mathf.Max(r.xMin + 40f, r.xMax - 20f)), by = Mathf.Clamp(hv.y, 302f, 322f);
        Color pourCol = Color.Lerp(LiquidColour(Rules.Agent(AgentId), 1), Color.white, 0.2f);
        if (OverTub(i, hv)) c.Line(bx - 14, by + 10, bx - 14, top, pourCol, 4f);
        c.Polygon(new List<Vector2> { new Vector2(bx - 16, by + 10), new Vector2(bx + 12, by + 22), new Vector2(bx + 4, by + 50), new Vector2(bx - 24, by + 38) }, Iron);
        int cap = Mathf.Max(1, pourMax >= 0 ? Mathf.Min(pourMax, PourCap(i)) : PourCap(i));
        int doses = Mathf.FloorToInt(pourTime);
        var ticks = new (float, float, Color)[cap];
        for (int k = 0; k < cap; k++) ticks[k] = ((k + 0.9f) / cap, (k + 1f) / cap, new Color(0f, 0f, 0f, 0.9f));
        float mx = Mathf.Clamp(bx - 34f, 4f, 716f - 68f);
        Meter(c, mx, by + 54, 68, 8, pourTime / cap, new Color(0.6f, 0.8f, 1f), ticks);
        string words = doses == 1 ? "1 DOSE" : $"{doses} DOSES";
        float w = CraftArt.LabelWidth(words);
        CraftArt.Label(c, Mathf.Clamp(mx + 34f - w * 0.5f, 4f, 716f - w), by + 66, words, CraftArt.LabelInk);
    }

    /// <summary>
    /// How strong the liquor is (gold), with the line past which it closes a raw hide's outside (case hardening) - red
    /// past it only while there is a core to close: no hide in it yet, or one whose least-tanned core is still under
    /// <see cref="CraftLeather.CaseHardensBelow"/> (once the core is tanned, a strong liquor only tans it faster).
    /// </summary>
    private static void StrengthGauge(CraftPixels c, Rect r, CraftVatState v, CraftTanningAgent a, CraftPiece soaking)
    {
        bool tans = a != null && a.kind != CraftAgentKind.Water && a.kind != CraftAgentKind.Lime && a.kind != CraftAgentKind.Acid;
        bool rawCore = soaking == null || soaking.kind != CraftPieceKind.Hide || Means(soaking.hide).coreLeast < CraftLeather.CaseHardensBelow;
        if (tans && rawCore)
            Meter(c, r.xMin + 4, r.yMin + 26, 7, r.height - 38, (float)v.strength, Mark, (0f, a.caseHardeningAbove, GoodBand), (a.caseHardeningAbove, 1f, HarmBand));
        else if (tans)
            Meter(c, r.xMin + 4, r.yMin + 26, 7, r.height - 38, (float)v.strength, Mark, (0f, 1f, GoodBand));
        else
            Meter(c, r.xMin + 4, r.yMin + 26, 7, r.height - 38, (float)v.strength, Mark);
    }

    /// <summary>Lime loosens the hair fastest from about this warmth (°C, <see cref="CraftLeather.LimeWarmth"/>'s top) and no faster past it; up to the second it is well clear of cooking a raw hide.</summary>
    private const float LimeBest = 30f, LimeSafe = 40f;

    private static readonly Color HeatGood = GoodBand, HeatHardens = new Color(0.75f, 0.6f, 0.2f, 1f),
        HeatHarms = new Color(0.75f, 0.38f, 0.12f, 1f), HeatShrinks = HarmBand;

    /// <summary>
    /// The liquor's heat (0-100 °C) against the bands the workshop applies: green where it works best (lime from about
    /// 30 °C, where it is at its fastest; a tanning liquor from its best warmth to its most), gold where hot water hardens
    /// tanned leather (55 °C to the hide's shrink temperature), orange where a tanning liquor harms a half-tanned hide (past
    /// its most), red past the soaking hide's shrink temperature (60 °C raw), where it shrinks - and a raw one cooks.
    /// </summary>
    private void Thermometer(CraftPixels c, Rect r, CraftVatState v, CraftTanningAgent a, CraftPiece soaking)
    {
        float shrinkT = soaking != null && soaking.kind == CraftPieceKind.Hide ? (float)soaking.hide.shrinkTemperature : 60f;
        var bands = new List<(float, float, Color)>();
        if (a == null || a.kind == CraftAgentKind.Water)
        {
            // only tanned leather hardens (a raw hide just soaks - and cooks past 60 °C)
            bool tanned = soaking == null || soaking.kind == CraftPieceKind.Hide && CraftLeather.MeanTannage(soaking.hide) > 0.5;
            if (tanned) bands.Add(((float)CraftLeather.HardensFrom / 100f, shrinkT / 100f, HeatHardens));
        }
        else if (a.kind == CraftAgentKind.Lime)
            bands.Add((LimeBest / 100f, Mathf.Min(LimeSafe, shrinkT) / 100f, HeatGood));
        else if (a.kind != CraftAgentKind.Acid)
        {
            bool raw = soaking == null || soaking.kind != CraftPieceKind.Hide || CraftLeather.MeanTannage(soaking.hide) < 0.6;
            bands.Add((a.bestTemperature / 100f, Mathf.Min(a.maxTemperature, shrinkT) / 100f, HeatGood));
            if (raw && a.maxTemperature < shrinkT) bands.Add((a.maxTemperature / 100f, shrinkT / 100f, HeatHarms));
        }
        bands.Add((shrinkT / 100f, 1f, HeatShrinks));
        Meter(c, r.xMax - 11, r.yMin + 26, 7, r.height - 38, (float)v.temperature / 100f, new Color(0.95f, 0.95f, 0.95f), bands.ToArray());
    }

    /// <summary>Under a tub, what the work in it is doing - by what it holds.</summary>
    private void WorkBars(CraftPixels c, Rect r, CraftTanningAgent a, CraftPiece q)
    {
        Color good = GoodBand, bad = HarmBand;
        float x = r.xMin + 52, len = r.width - 52; // the names fit before the bars (SHRUNK is the longest)
        void Bar(float y, string label, float value, Color fill, params (float, float, Color)[] bands)
        {
            CraftArt.Label(c, r.xMin, y, label, CraftArt.LabelInk);
            Meter(c, x, y, len, 9, value, fill, bands);
        }
        if (q.kind != CraftPieceKind.Hide)
        {
            // metal in a pickle: the scale it still carries, then how deep it is etched
            double scale = 0, l = 0;
            foreach (MetalSegment seg in q.segments) { scale += seg.scale * seg.length; l += seg.length; }
            Bar(26, "SCALE", Mathf.Clamp01((float)(l > 0 ? scale / l / 0.1 : 0)), new Color(0.45f, 0.35f, 0.3f));
            Bar(12, "ETCH", Mathf.Clamp01((float)q.Tag("etched")), new Color(0.8f, 0.82f, 0.86f));
            return;
        }
        HideMeans m = Means(q.hide);
        switch (a?.kind ?? CraftAgentKind.Water)
        {
            case CraftAgentKind.Lime:
                Bar(26, "LOOSE", (float)m.loose, new Color(0.88f, 0.88f, 0.82f), (0.7f, 0.95f, good), (0.95f, 1f, bad));
                Bar(12, "GRAIN", (float)m.grain, new Color(0.7f, 0.55f, 0.35f));
                break;
            case CraftAgentKind.Water:
                // hot water: how hard a tanned hide has set (cuir bouilli), and how far it has shrunk (past a quarter: ruined)
                Bar(26, "HARD", (float)Math.Min(1, m.hard), new Color(0.75f, 0.6f, 0.35f));
                Bar(12, "SHRUNK", Mathf.Clamp01((float)MeanShrink(q.hide) / 0.4f), new Color(0.85f, 0.4f, 0.3f), (0.25f, 1f, bad));
                break;
            case CraftAgentKind.Acid:
                Bar(26, "BURN", (float)Math.Max(0, m.damage - m.scar), new Color(0.85f, 0.4f, 0.3f));
                break;
            default:
                Bar(26, "TAN", (float)m.tanS, new Color(0.75f, 0.5f, 0.3f), (0.75f, 1f, good));
                Bar(12, "CORE", (float)m.tanC, new Color(0.55f, 0.32f, 0.18f), (0.75f, 1f, good));
                // the least tanned place of the core (what the inspector reads), a mark on the bar
                c.Rect(x + 1 + (len - 2) * Mathf.Clamp01((float)m.coreLeast) - 1, 10, x + 1 + (len - 2) * Mathf.Clamp01((float)m.coreLeast) + 1, 23, Mark);
                break;
        }
    }

    private static double MeanShrink(HideState h)
    {
        double t = 0;
        int k = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.shrink; k++; }
        return k > 0 ? t / k : 0;
    }

    /// <summary>The bench at the left: the piece picked, if it lies there (or the piece dragged, under the hand).</summary>
    private void DrawBench(CraftPixels c, CraftPiece p, bool metalToo)
    {
        c.Rect(benchSpot.xMin, benchSpot.yMin, benchSpot.xMax, benchSpot.yMin + 6, Wood);
        CraftArt.Label(c, benchSpot.xMin, benchSpot.yMin - 14, "BENCH", CraftArt.LabelDim);
        if (p == null || !(p.location == CraftLocation.Air || dragging))
            return;
        Rect at = dragging ? new Rect(dragAt.x - 52, dragAt.y - 45, 104, 90) : new Rect(benchSpot.xMin + 6, benchSpot.yMin + 10, 104, 110);
        if (p.kind == CraftPieceKind.Hide)
            CraftArt.DrawHide(c, Rules, p.hide, at);
        else if (metalToo && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners) && p.segments.Count > 0)
            CraftArt.DrawTop(c, Rules, p, new Rect(at.xMin, at.center.y - 16, at.width, 32), Fit(p, at.width, 32), null, false, ui.Dark);
    }

    // ------------------------------------------------------------------ the frame and the rack, drawn
    /// <summary>How hard each side can be pulled before it tears (the selected hide's weakest edge cells).</summary>
    private double Limit(CraftPiece p, int side) => p != null && p.kind == CraftPieceKind.Hide ? CraftLeather.EdgeLimit(Rules, p.hide, side) : 1;

    /// <summary>The most a handle pulls at full travel: a little past what the side can take (never past 1).</summary>
    private float MaxPull(CraftPiece p, int side) => Mathf.Clamp((float)(1.2 * Limit(p, side)), 0.05f, 1f);

    private float HandleTravel(CraftPiece p, int k) => Mathf.Clamp(tension[k] / MaxPull(p, k) * TravelPx, 0f, TravelPx);

    /// <summary>A side's handle on the frame (0 north - the top -, 1 east, 2 south, 3 west), slid out by its pull.</summary>
    private Rect HandleRect(CraftPiece p, int k)
    {
        float t = HandleTravel(p, k);
        Rect f = FrameRect;
        switch (k)
        {
            case 0: return new Rect(f.center.x - 14, f.yMax + 4 + t, 28, 14);
            case 1: return new Rect(f.xMax + 4 + t, f.center.y - 14, 14, 28);
            case 2: return new Rect(f.center.x - 14, f.yMin - 18 - t, 28, 14);
            default: return new Rect(f.xMin - 18 - t, f.center.y - 14, 14, 28);
        }
    }

    /// <summary>How far past the frame's edge the pointer is, along side <paramref name="k"/>'s pull.</summary>
    private static float Travel(int k, Vector2 pos)
    {
        Rect f = FrameRect;
        return k == 0 ? pos.y - (f.yMax + 4) : k == 1 ? pos.x - (f.xMax + 4) : k == 2 ? f.yMin - 4 - pos.y : f.xMin - 4 - pos.x;
    }

    /// <summary>The hide inside the frame: it spreads toward the frame as it is pulled.</summary>
    private Rect FrameInner(CraftPiece p)
    {
        Rect f = FrameRect;
        float mN = 36 - 18 * HandleTravel(p, 0) / TravelPx, mE = 36 - 18 * HandleTravel(p, 1) / TravelPx;
        float mS = 36 - 18 * HandleTravel(p, 2) / TravelPx, mW = 36 - 18 * HandleTravel(p, 3) / TravelPx;
        return new Rect(f.xMin + mW, f.yMin + mS, f.width - mW - mE, f.height - mS - mN);
    }

    private static Color PullColour(double rel) => rel < CraftLeather.FlatFrom ? new Color(0.55f, 0.55f, 0.55f)
        : rel <= CraftLeather.OverdrawnFrom ? Good : rel <= 1 ? Fair : Harm;

    /// <summary>The hides laced in the frame, or those on the rack, in the station's order.</summary>
    private List<CraftPiece> HidesAt(CraftLocation at)
    {
        var list = new List<CraftPiece>();
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Hide && q.location == at) list.Add(q);
        return list;
    }

    /// <summary>
    /// The hides on the rack as the window hangs them, each in its place: as many as the rack takes
    /// (<see cref="CraftLeather.RackHides"/>) - an old save's extra ones wait, except the picked one, which takes the last place.
    /// </summary>
    private List<CraftPiece> RackShown(CraftPiece picked)
    {
        List<CraftPiece> hung = HidesAt(CraftLocation.Rack);
        if (hung.Count <= CraftLeather.RackHides)
            return hung;
        List<CraftPiece> shown = hung.GetRange(0, CraftLeather.RackHides);
        if (picked != null && hung.IndexOf(picked) >= CraftLeather.RackHides) shown[CraftLeather.RackHides - 1] = picked;
        return shown;
    }

    /// <summary>Where a hide hangs on the rack: side by side, as many as hang there (<see cref="RackShown"/>).</summary>
    private Rect RackSlot(List<CraftPiece> shown, int i)
    {
        Rect r = RackRect;
        int k = Mathf.Max(1, Mathf.Min(CraftLeather.RackHides, shown.Count));
        float wEach = (r.width - 16) / k;
        return new Rect(r.xMin + 8 + (i % CraftLeather.RackHides) * wEach + 2, r.yMin + 20, wEach - 4, r.height - 50);
    }

    /// <summary>A hide drawn with cells smaller than this (pixels) in its place on the rack is worked in a close-up instead.</summary>
    private const float CloseUpCell = 10f;

    /// <summary>The close-up of a hide on the rack being worked: over the frame's middle, under a strip for its name.</summary>
    private static Rect CloseUpRect => new Rect(FrameRect.xMin + 14, FrameRect.yMin + 14, FrameRect.width - 28, FrameRect.height - 42);

    /// <summary>The close-up's panel, its name strip included: a press in it works the hide shown close up.</summary>
    private static Rect CloseUpPanel => new Rect(CloseUpRect.xMin - 4, CloseUpRect.yMin - 4, CloseUpRect.width + 8, CloseUpRect.height + 22);

    /// <summary>
    /// Where the picked hide hangs on the rack (<paramref name="slot"/>, false when it does not hang where the window shows
    /// it), and whether a stroke tool works it in a close-up (<see cref="CloseUpRect"/>): when its place is too narrow to
    /// stroke (cells under <see cref="CloseUpCell"/>), as it is beside a frame or with several hides hanging.
    /// </summary>
    private bool RackPlace(CraftPiece p, out Rect slot, out bool closeUp)
    {
        slot = default;
        closeUp = false;
        if (p == null || p.kind != CraftPieceKind.Hide || p.location != CraftLocation.Rack || !Station.Has(CraftTools.Rack))
            return false;
        List<CraftPiece> shown = RackShown(p);
        int i = shown.IndexOf(p);
        if (i < 0)
            return false;
        slot = RackSlot(shown, i);
        CraftArt.HideLayout(p.hide, slot, out _, out float cell);
        closeUp = place == Place.Frame && frameTool != Tool.Hands && cell < CloseUpCell;
        return true;
    }

    private void DrawFramePlace(CraftPixels c, CraftPiece p)
    {
        bool hasFrame = Station.Has(CraftTools.Frame), hasRack = Station.Has(CraftTools.Rack);
        bool closeUp = RackPlace(p, out _, out bool zoom) && zoom && !dragging; // the picked rack hide worked over the frame
        DrawBench(c, p, false);
        CraftPiece framed = null;
        if (hasFrame)
        {
            Rect f = FrameRect;
            List<CraftPiece> inFrame = HidesAt(CraftLocation.Frame);
            framed = p != null && inFrame.Contains(p) ? p : inFrame.Count > 0 ? inFrame[0] : null;
            if (framed != null && dragging && framed == p) framed = null;
            c.Frame(f.xMin, f.yMin, f.xMax, f.yMax, Wood, 10f);
            CraftArt.Label(c, f.xMin, f.yMax + 8, "FRAME", CraftArt.LabelDim);
            bool mine = framed != null && framed == p;
            if (framed != null)
            {
                Rect inner = mine ? FrameInner(p) : new Rect(f.xMin + 36, f.yMin + 36, f.width - 72, f.height - 72);
                CraftArt.DrawHide(c, Rules, framed.hide, inner);
                if (mine && frameTool == Tool.Stake) StakeTint(c, framed.hide, inner);
                DrawFlash(c, framed, inner);
                // The lacing to the frame, by how hard each side is pulled for what its edge can take.
                for (int k = 0; k < 4; k++)
                {
                    double rel = mine ? tension[k] / Math.Max(1e-3, Limit(p, k)) : 0;
                    Color lace = mine ? PullColour(rel) : new Color(0.5f, 0.5f, 0.5f);
                    float thick = 1f + Mathf.Clamp01((float)rel);
                    for (int j = 0; j < 8; j++)
                    {
                        float t = (j + 0.5f) / 8f;
                        if (k == 0) c.Line(inner.xMin + inner.width * t, inner.yMax, inner.xMin + inner.width * t, f.yMax - 5, lace, thick);
                        else if (k == 2) c.Line(inner.xMin + inner.width * t, inner.yMin, inner.xMin + inner.width * t, f.yMin + 5, lace, thick);
                        else if (k == 1) c.Line(inner.xMax, inner.yMin + inner.height * t, f.xMax - 5, inner.yMin + inner.height * t, lace, thick);
                        else c.Line(inner.xMin, inner.yMin + inner.height * t, f.xMin + 5, inner.yMin + inner.height * t, lace, thick);
                    }
                }
                if (mine)
                {
                    for (int k = 0; k < 4; k++) DrawPullSlot(c, p, k);
                    DrawDryness(c, framed, new Rect(f.xMin + 4, f.yMax + 22, 124, 8));
                }
                else if (!closeUp) // a press on it picks it, whatever is in hand
                    CraftArt.Label(c, f.center.x - CraftArt.LabelWidth("CLICK TO PICK IT") * 0.5f, f.yMin - 20, "CLICK TO PICK IT", CraftArt.LabelInk);
                if (inFrame.Count > 1)
                    CraftArt.Label(c, f.xMax - 60, f.yMax + 8, $"+{inFrame.Count - 1} MORE", CraftArt.LabelDim);
            }
        }
        if (hasRack)
        {
            Rect r = RackRect;
            c.Rect(r.xMin, r.yMax - 10, r.xMax, r.yMax, Wood);
            c.Rect(r.xMin, r.yMin, r.xMin + 8, r.yMax, Wood);
            c.Rect(r.xMax - 8, r.yMin, r.xMax, r.yMax, Wood);
            CraftArt.Label(c, r.xMin, r.yMax + 8, "RACK", CraftArt.LabelDim);
            List<CraftPiece> hung = HidesAt(CraftLocation.Rack), shown = RackShown(p);
            for (int i = 0; i < shown.Count; i++)
            {
                if (dragging && shown[i] == p) continue;
                Rect slot = RackSlot(shown, i);
                CraftArt.DrawHide(c, Rules, shown[i].hide, slot);
                if (shown[i] == p)
                {
                    c.Frame(slot.xMin - 2, slot.yMin - 2, slot.xMax + 2, slot.yMax + 2, Mark, 2f);
                    if (!closeUp)
                    {
                        if (frameTool == Tool.Stake) StakeTint(c, p.hide, slot);
                        DrawFlash(c, p, slot);
                    }
                    DrawDryness(c, p, new Rect(r.xMin + 4, r.yMax + 22, Mathf.Min(124f, r.width - 8), 8));
                }
            }
            if (hung.Count > shown.Count) CraftArt.Label(c, r.xMax - 60, r.yMax + 8, $"+{hung.Count - shown.Count} MORE", CraftArt.LabelDim);
        }
        if (closeUp)
        {
            // the picked rack hide, close up where it can be worked (its place on the rack outlined): over the frame
            Rect z = CloseUpRect;
            Rect panel = CloseUpPanel;
            c.Rect(panel.xMin, panel.yMin, panel.xMax, panel.yMax, new Color(0.1f, 0.08f, 0.06f, 0.95f));
            c.Frame(panel.xMin, panel.yMin, panel.xMax, panel.yMax, Mark, 2f);
            CraftArt.DrawHide(c, Rules, p.hide, z);
            if (frameTool == Tool.Stake) StakeTint(c, p.hide, z);
            DrawFlash(c, p, z);
            CraftArt.Label(c, z.xMin + 2, z.yMax + 4, "ON THE RACK, CLOSE UP", CraftArt.LabelInk);
        }
        DrawTrail(c);
        Vector2 h = ui.Hover;
        if (frameTool != Tool.Hands && h.x >= 0 && p != null && StrokeArea(p, out Rect where) && GridRect(p.hide, where).Contains(h))
            DrawTool(c, h, frameTool, false);
    }

    /// <summary>A handle's slot: the gauge of that side's pull (grey slack, green taut, amber overdrawn, red tears) with the handle on it.</summary>
    private void DrawPullSlot(CraftPixels c, CraftPiece p, int k)
    {
        double lim = Math.Max(1e-3, Limit(p, k));
        float max = MaxPull(p, k);
        // the band edges, in shares of the handle's travel
        float flat = Mathf.Clamp01((float)(CraftLeather.FlatFrom * lim / max)), over = Mathf.Clamp01((float)(CraftLeather.OverdrawnFrom * lim / max)), tear = Mathf.Clamp01((float)(lim / max));
        var bands = new (float, float, Color)[]
        {
            (0f, flat, IdleBand), (flat, over, GoodBand),
            (over, tear, FairBand), (tear, 1f, HarmBand),
        };
        // Each slot runs outward from the frame along the handle's travel (its middle at no pull, the far end at the most);
        // the handle sits on it at its pull.
        Rect hr = HandleRect(p, k);
        Vector2 toward = k == 0 ? Vector2.up : k == 1 ? Vector2.right : k == 2 ? Vector2.down : Vector2.left;
        PullGauge(c, hr.center - toward * HandleTravel(p, k), toward, bands); // from the handle's middle with the side slack
        // its name beside the gauge's far half, clear of the handle's whole travel, of FRAME and DRY and of the rack
        Rect f = FrameRect;
        Vector2 at = k == 0 ? new Vector2(f.center.x + 18, f.yMax + 30) : k == 2 ? new Vector2(f.center.x + 18, f.yMin - 40)
            : k == 1 ? new Vector2(f.xMax + 21, f.center.y + 17) : new Vector2(f.xMin - 51, f.center.y + 17);
        CraftArt.Label(c, at.x, at.y, "PULL", CraftArt.LabelDim);
        double rel = tension[k] / lim;
        c.Rect(hr.xMin, hr.yMin, hr.xMax, hr.yMax, k == handle ? Mark : new Color(0.7f, 0.6f, 0.45f));
        c.Frame(hr.xMin, hr.yMin, hr.xMax, hr.yMax, PullColour(rel), 2f);
    }

    /// <summary>
    /// A handle's gauge: its bands laid along the handle's travel from <paramref name="start"/> (no pull) for
    /// <see cref="TravelPx"/> in <paramref name="toward"/> (outward from the frame), 6 pixels wide - the handle on it is the value.
    /// </summary>
    private static void PullGauge(CraftPixels c, Vector2 start, Vector2 toward, (float from, float to, Color color)[] bands)
    {
        Vector2 end = start + toward * TravelPx, side = new Vector2(Mathf.Abs(toward.y), Mathf.Abs(toward.x)) * 3f;
        c.Rect(Mathf.Min(start.x, end.x) - side.x - 1, Mathf.Min(start.y, end.y) - side.y - 1, Mathf.Max(start.x, end.x) + side.x + 1, Mathf.Max(start.y, end.y) + side.y + 1, new Color(0f, 0f, 0f, 0.6f));
        foreach ((float from, float to, Color color) b in bands)
        {
            Vector2 a = start + toward * (TravelPx * Mathf.Clamp01(b.from)), e = start + toward * (TravelPx * Mathf.Clamp01(b.to));
            if ((e - a).sqrMagnitude < 0.01f) continue;
            c.Rect(Mathf.Min(a.x, e.x) - side.x, Mathf.Min(a.y, e.y) - side.y, Mathf.Max(a.x, e.x) + side.x, Mathf.Max(a.y, e.y) + side.y, b.color);
        }
    }

    /// <summary>How dry it is (the mean; a mark at the wettest place), with the gold band to stake it in and the line where it is dry.</summary>
    private static void DrawDryness(CraftPixels c, CraftPiece p, Rect r)
    {
        HideMeans m = Means(p.hide);
        float lo = 1f - (float)CraftLeather.StakeWet, hi = 1f - (float)CraftLeather.StakeDry;
        CraftArt.Label(c, r.xMin, r.yMin - 1, "DRY", CraftArt.LabelInk);
        Meter(c, r.xMin + 30, r.yMin, r.width - 30, r.height, 1f - (float)m.moisture, new Color(0.9f, 0.8f, 0.55f),
            (lo, hi, new Color(0.7f, 0.58f, 0.2f, 1f)));
        float wet = 1f - (float)m.wettest, len = r.width - 32;
        c.Rect(r.xMin + 31 + len * wet - 1, r.yMin - 3, r.xMin + 31 + len * wet + 1, r.yMax + 3, new Color(0.4f, 0.65f, 1f));
        float dry = 1f - (float)CraftLeather.DryAt;
        c.Rect(r.xMin + 31 + len * dry - 0.5f, r.yMin, r.xMin + 31 + len * dry + 0.5f, r.yMax, Color.black);
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Scroll)
        {
            // the wheel turns the pressure of the tool in hand; mid-stroke, the stretch drawn so far goes at the pressure it
            // was drawn with, and the stroke goes on at the new one
            if (Strokes && pressure != null)
            {
                if (stroking) FlushSegment(true);
                pressure.value = Mathf.Clamp(pressure.value + 0.05f * Mathf.Sign(scroll), pressure.minValue, pressure.maxValue);
            }
            return;
        }
        if (phase == CraftPointer.Down) ignorePress = false;
        if (ignorePress && phase != CraftPointer.Cancel)
        {
            if (phase == CraftPointer.Up) ignorePress = false;
            return;
        }
        CraftPiece p = Selected;
        if (place == Place.Vats) VatPointer(phase, pos, p);
        else if (place == Place.Frame && frameTool == Tool.Hands) FramePointer(phase, pos, p);
        else StrokePointer(phase, pos, p);
    }

    /// <summary>Where the selected hide is drawn for the tool in hand to stroke it (and false, with why not, if it cannot be).</summary>
    private bool StrokeArea(CraftPiece p, out Rect where)
    {
        where = area;
        if (p == null || p.kind != CraftPieceKind.Hide)
            return false;
        if (place == Place.Beam)
            return p.location == CraftLocation.Air;
        if (p.location == CraftLocation.Frame && Station.Has(CraftTools.Frame))
        {
            where = FrameInner(p);
            return true;
        }
        if (p.location == CraftLocation.Rack && RackPlace(p, out Rect slot, out bool closeUp))
        {
            where = closeUp ? CloseUpRect : slot; // a narrow place on the rack is worked close up
            return true;
        }
        return false;
    }

    /// <summary>Why the tool in hand cannot work the selected piece where it is (null: it can).</summary>
    private string CannotStroke(CraftPiece p, Tool t)
    {
        if (p == null) return "Pick a hide on the left first.";
        if (p.kind != CraftPieceKind.Hide) return "That is not a hide.";
        if (!Here(t)) return "There is no fleshing beam here for that.";
        if (CraftTextiles.IsCloth(Rules, p) && t != Tool.Oil && t != Tool.Dye) return "It is cloth: only oil, wax or dye are worked into it here.";
        if (place == Place.Beam)
            switch (p.location)
            {
                case CraftLocation.Air: break;
                case CraftLocation.Vat: return $"It is soaking in vat {p.vat + 1}: lift it out first (the vats).";
                case CraftLocation.Frame:
                case CraftLocation.Rack:
                    return t == Tool.Flesh || t == Tool.Unhair || t == Tool.Skive ? $"It is {(p.location == CraftLocation.Frame ? "in the frame" : "on the rack")}: take it down onto the bench to flesh, scrape or skive it."
                        : $"It is {(p.location == CraftLocation.Frame ? "in the frame" : "on the rack")}: stake, slick, oil or dye it there ({PlaceName(Place.Frame)}).";
                default: return "Take it out first.";
            }
        else if (p.location != CraftLocation.Frame && p.location != CraftLocation.Rack)
            return "Lace it in the frame or hang it on the rack first (Lace, pull, hang) - or work it at the beam.";
        if ((t == Tool.Oil || t == Tool.Dye) && Chosen(t) == null)
            return t == Tool.Dye ? "There is no dye to choose." : "There is no oil or wax to choose.";
        return null; // with none in the bag, what is left in a pot already opened may still do: the station says
    }

    /// <summary>For oil, wax and dye: what one item covers, and a word when the bag has none.</summary>
    private string SupplyNote(Tool t)
    {
        CraftSupplyDefinition sup = Chosen(t);
        if (sup == null)
            return "";
        string note = $" One {ItemOf(sup.id)} covers about {sup.cellsPerItem:0} cells: a stroke uses what the leather takes in.";
        return Have(sup.id) == 0 ? note + $" <color=#ffb070>You have no {ItemOf(sup.id)}: only what is left in one you opened.</color>" : note;
    }

    private CraftSupplyDefinition Chosen(Tool t)
    {
        List<string> ids = t == Tool.Dye ? dyes : oils;
        TMP_Dropdown d = t == Tool.Dye ? dyeDrop : oilDrop;
        return ids.Count > 0 && d != null ? Rules.Supply(ids[Mathf.Clamp(d.value, 0, ids.Count - 1)]) : null;
    }

    private void StrokePointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (phase == CraftPointer.Cancel)
        {
            // the tool comes off the hide: the stroke under way is put down, not sent
            if (stroking)
            {
                stroking = false;
                segment.Clear();
                stroke.Clear();
                tailWaiting = false;
                trail.Clear();
                said = "Lifted off: that stroke was not finished.";
                ignorePress = true;
            }
            return;
        }
        if (phase == CraftPointer.Down)
        {
            Tool t = InHand;
            // another hide on the rack or in the frame: pick it (the label over the frame says so)
            if (place == Place.Frame && (PickOnRack(pos, p) || PickInFrame(pos, p)))
                return;
            string why = CannotStroke(p, t);
            if (why != null || !StrokeArea(p, out Rect where))
            {
                said = why ?? "";
                ignorePress = true;
                return;
            }
            if (!GridRect(p.hide, where).Contains(pos))
            {
                ignorePress = true;
                return;
            }
            stroking = true;
            strokePiece = p.id;
            strokeTool = t;
            strokeArea = where;
            stroke.Clear();
            segment.Clear();
            tailWaiting = false;
            trail.Clear();
            AddSample(pos);
            return;
        }
        if (!stroking)
            return;
        if (phase == CraftPointer.Drag)
            AddSample(pos);
        else if (phase == CraftPointer.Up)
        {
            AddSample(pos);
            FlushSegment(false);
            stroking = false;
            trail.Clear();
        }
    }

    /// <summary>One sample of the stroke: on the hide it goes into the stretch being drawn; off it, that stretch ends there.</summary>
    private void AddSample(Vector2 pos)
    {
        CraftPiece p = Ws.Piece(strokePiece);
        if (p == null || p.kind != CraftPieceKind.Hide)
        {
            stroking = false;
            return;
        }
        HideState h = p.hide;
        Vector2 v = CraftArt.HideCells(h, strokeArea, pos);
        bool on = h.Cell(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y))?.present == true;
        if (!on)
        {
            // the tool left the hide: what was drawn on it is a stroke of its own (the next starts where it comes back)
            if (segment.Count > 0) FlushSegment(false);
            return;
        }
        stroke.Add(ui.PointerTime, v.x / Mathf.Max(1, h.width), v.y / Mathf.Max(1, h.height), 0, pressure.value, Hands);
        if (segment.Count == 0 || Vector2.Distance(lastPoint, v) >= 0.2f)
        {
            segment.Add(v.x);
            segment.Add(v.y);
            lastPoint = v;
            tailWaiting = false;
        }
        else
        {
            pendingTail = v;
            tailWaiting = true;
        }
        if (trail.Count == 0 || Vector2.Distance(trail[trail.Count - 1], pos) > 3f) trail.Add(pos);
        if (segment.Count >= MaxSegment * 2) FlushSegment(true);
    }

    /// <summary>Sends the stretch of the stroke drawn so far (taken out of the buffer first); <paramref name="goOn"/>: the stroke goes on from its end.</summary>
    private void FlushSegment(bool goOn)
    {
        if (tailWaiting)
        {
            segment.Add(pendingTail.x);
            segment.Add(pendingTail.y);
            lastPoint = pendingTail;
            tailWaiting = false;
        }
        var points = new List<float>(segment);
        float press = (float)stroke.Pressure;
        segment.Clear();
        stroke.Clear();
        if (goOn)
        {
            segment.Add(lastPoint.x);
            segment.Add(lastPoint.y);
        }
        if (points.Count >= 4 && strokePiece != null)
            SendStroke(strokePiece, strokeTool, points, press);
    }

    private void SendStroke(string pieceId, Tool t, List<float> points, float press)
    {
        CraftPiece p = Ws.Piece(pieceId);
        if (p == null)
            return;
        // the cells this stroke works, as the workshop will work them: they flash now, and are judged in the answer
        var before = new List<(int i, HideCell cell)>();
        CraftLeather.StrokeCells(p.hide, points, Reach(t), (i, c, wt) => before.Add((i, c.Clone())));
        flash.Clear();
        foreach ((int i, HideCell _) in before) flash.Add(i);
        flashPiece = pieceId;
        flashAt = Time.unscaledTime;
        CraftSupplyDefinition sup = t == Tool.Oil || t == Tool.Dye ? Chosen(t) : null;
        CraftRequest r;
        switch (t)
        {
            case Tool.Flesh: r = new CraftRequest(CraftRequestKind.Scrape, pieceId) { points = points, flag = false, a = press, b = angle.value }; break;
            case Tool.Unhair: r = new CraftRequest(CraftRequestKind.Scrape, pieceId) { points = points, flag = true, a = press, b = 0.5f }; break;
            case Tool.Stake: r = new CraftRequest(CraftRequestKind.Stake, pieceId) { points = points, a = press }; break;
            case Tool.Slick: r = new CraftRequest(CraftRequestKind.Slick, pieceId) { points = points, a = press }; break;
            case Tool.Skive: r = new CraftRequest(CraftRequestKind.Skive, pieceId) { points = points, a = press }; break;
            default:
                if (sup == null) return;
                r = new CraftRequest(t == Tool.Dye ? CraftRequestKind.Dye : CraftRequestKind.Condition, pieceId) { points = points, id = sup.id, a = press };
                break;
        }
        float edge0 = (float)p.hide.edgeFinish;
        Send(r, (ok, why) =>
        {
            if (!ok)
            {
                if (flashPiece == pieceId) flash.Clear();
                return;
            }
            CraftPiece q = Ws.Piece(pieceId);
            if (q == null || ui.SelectedId != pieceId)
                return;
            string words = StrokeWords(t, q, before, sup, edge0, out bool harm);
            if (string.IsNullOrEmpty(words))
                return;
            if (harm) ui.Message(words);
            else said = words;
        });
    }

    /// <summary>What a stroke did, read from the very cells it worked, before and after (in the answer: the workshop's own result).</summary>
    private string StrokeWords(Tool t, CraftPiece p, List<(int i, HideCell cell)> before, CraftSupplyDefinition sup, float edge0, out bool harm)
    {
        harm = false;
        HideState h = p.hide;
        double k = 0, flesh0 = 0, flesh1 = 0, hair0 = 0, hair1 = 0, loose = 0, grain0 = 0, grain1 = 0, stiff0 = 0, stiff1 = 0, smooth0 = 0, smooth1 = 0, greasy = 0, dye0 = 0, dye1 = 0, moist = 0, rise = 0;
        bool holed = false;
        foreach ((int i, HideCell b) in before)
        {
            if (i < 0 || i >= h.cells.Count) continue;
            HideCell a = h.cells[i];
            if (!a.present) continue;
            k++;
            flesh0 += b.flesh; flesh1 += a.flesh; hair0 += b.hair; hair1 += a.hair; loose += b.hairLoose;
            grain0 += b.grain; grain1 += a.grain; stiff0 += b.stiff; stiff1 += a.stiff; smooth0 += b.smooth; smooth1 += a.smooth;
            greasy += b.oil + b.wax; dye0 += b.dye; dye1 += a.dye; moist += b.moisture;
            rise = Math.Max(rise, a.damage - b.damage);
            holed |= a.thickness < 0.6 && a.damage > b.damage;
        }
        if (k == 0)
            return "";
        switch (t)
        {
            case Tool.Flesh:
                if (rise > 0.005) { harm = true; return "Too hard: the knife bit into clean or thin skin - keep the pressure in the gauge's green (a flatter knife is gentler)."; }
                return flesh1 < flesh0 - 0.001 * k ? "Flesh and fat peel away under the knife." : "Nothing much comes off here: it is clean.";
            case Tool.Unhair:
                if (grain1 < grain0 - 0.002 * k)
                {
                    harm = true;
                    if (hair0 / k <= CraftLeather.HairyFrom) return "No hair here to scrape: pressing that hard only scuffs the bare grain - ease off.";
                    return loose / k < CraftLeather.LooseFrom + 0.1 ? "The hair barely moves and the grain scuffs: it has not been limed long enough."
                        : "The hair comes away, but pressing that hard scuffs the grain: ease off.";
                }
                return hair1 < hair0 - 0.001 * k ? "The hair slides off in clumps." : "Little hair left here.";
            case Tool.Stake:
            {
                double m = moist / k;
                if (grain1 < grain0 - 0.002 * k) { harm = true; return "Dry: the grain cracks under the stake - stake bark leather while it is still damp."; }
                if (m > CraftLeather.StakeWet) return "Too wet to stake: it stiffens again as it dries - stake it as it dries.";
                if (m <= CraftLeather.StakeDry) return stiff1 < stiff0 - 0.001 * k ? "Dry: it softens only a little now - stake leather while it is damp." : "Dry: staking hardly softens it now.";
                return stiff1 < stiff0 - 0.001 * k ? "Worked over the stake: it softens." : "It is as supple as staking makes it here.";
            }
            case Tool.Slick:
                if (hair0 / k >= CraftLeather.UnhairedBelow) return "The hair keeps the slicker off the grain: scrape the hair off first.";
                if (smooth1 <= smooth0 + 0.001 * k) return "The grain will not slick any further here (or it is too damaged to).";
                return h.edgeFinish > edge0 + 0.005 ? "The grain lies down smooth and the edges burnish." : !string.IsNullOrEmpty(h.pattern) ? "The grain lies down smooth (run along the edges to burnish them)." : "The grain lies down smooth.";
            case Tool.Skive:
                if (holed) { harm = true; return "Skived through: a hole!"; }
                return "A thin shaving comes off the flesh side.";
            case Tool.Dye:
                if (dye1 < dye0 + 0.02 * k && greasy / k > 0.2) return "The oil and wax keep the dye out: dye before oiling.";
                if (hair0 / k >= CraftLeather.UnhairedBelow) return "The hair keeps much of the dye off the grain: scrape the hair off first.";
                return dye1 > dye0 + 0.001 * k ? "The dye goes on - keep the strokes even." : "It will take no more dye here.";
            default:
                return sup != null && sup.kind == CraftSupplyKind.Wax ? "The wax goes on: it sheds water and stiffens a little." : "Worked in: it darkens and softens.";
        }
    }

    /// <summary>A press on another hide hanging on the rack picks it (the rest of the press does nothing) - not under the close-up of the one being worked.</summary>
    private bool PickOnRack(Vector2 pos, CraftPiece p)
    {
        if (!Station.Has(CraftTools.Rack) || RackPlace(p, out _, out bool closeUp) && closeUp && CloseUpPanel.Contains(pos))
            return false;
        List<CraftPiece> shown = RackShown(p);
        for (int i = 0; i < shown.Count; i++)
            if (shown[i] != p && RackSlot(shown, i).Contains(pos))
            {
                ui.Select(shown[i].id);
                said = "Picked the hide on the rack.";
                ignorePress = true;
                return true;
            }
        return false;
    }

    /// <summary>
    /// A press on the frame holding another hide picks that hide (the rest of the press does nothing) - unless the picked
    /// rack hide is worked close up over it.
    /// </summary>
    private bool PickInFrame(Vector2 pos, CraftPiece p)
    {
        if (!Station.Has(CraftTools.Frame) || !FrameRect.Contains(pos) || RackPlace(p, out _, out bool closeUp) && closeUp)
            return false;
        foreach (CraftPiece q in HidesAt(CraftLocation.Frame))
            if (q != p)
            {
                ui.Select(q.id);
                said = "Picked the hide in the frame.";
                ignorePress = true;
                return true;
            }
        return false;
    }

    // ------------------------------------------------------------------ the vats, by hand
    private int VatAt(Vector2 pos)
    {
        for (int i = 0; i < vats.Count; i++)
            if (vats[i].Contains(pos)) return i;
        return -1;
    }

    /// <summary>Is the pointer over tub <paramref name="i"/>'s column (the bucket pours anywhere above it)?</summary>
    private bool OverTub(int i, Vector2 pos) => i >= 0 && i < vats.Count && pos.x >= vats[i].xMin - 6 && pos.x <= vats[i].xMax + 6 && pos.y >= vats[i].yMin;

    private string AgentId => agents.Count > 0 && agentDrop != null ? agents[Mathf.Clamp(agentDrop.value, 0, agents.Count - 1)] : null;

    private string AgentItem() => Rules.Agent(AgentId) is CraftTanningAgent a && !string.IsNullOrEmpty(a.item) ? a.item : Rules.Agent(AgentId)?.Name ?? "agent";

    /// <summary>
    /// The most doses one pour gives, by the workshop's rule: an agent, what brings its liquor to full strength; water, one
    /// to fill an empty vat (or one of water), a few to thin a liquor.
    /// </summary>
    private int PourCap(int i)
    {
        CraftTanningAgent a = Rules.Agent(AgentId);
        CraftVatState v = i >= 0 && i < Ws.Vats.Count ? Ws.Vats[i] : null;
        bool empty = v == null || v.volume <= 0.01 || string.IsNullOrEmpty(v.agent);
        if (a == null || a.kind == CraftAgentKind.Water || a.dose <= 0)
            return empty || Rules.Agent(v.agent)?.kind == CraftAgentKind.Water ? 1 : 4;
        bool same = !empty && v.agent == a.id;
        return Mathf.Clamp(Mathf.CeilToInt((float)((1 - (same ? v.strength : 0)) / a.dose - 1e-4)), 1, 8);
    }

    /// <summary>What the bucket says once it has poured all it may into tub <paramref name="i"/>.</summary>
    private string PourEnough(int i)
    {
        CraftTanningAgent a = Rules.Agent(AgentId);
        if (pourMax >= 0 && pourMax < PourCap(i)) return $"That is all the {AgentItem()} you have.";
        if (a == null || a.kind == CraftAgentKind.Water || a.dose <= 0)
            return PourCap(i) == 1 ? "That fills the vat with water." : "That is as weak as water makes it.";
        return "That is enough for a full-strength liquor.";
    }

    /// <summary>What the fire brings the vat to (the workshop's rule): in the vat panel's words, in °C with a thermometer.</summary>
    private string FireWords(float v)
    {
        if (v <= 0.02f) return "out";
        double t = Ws.Ambient + v * (100 - Ws.Ambient) * 0.95;
        if (Station.HasInstrument(CraftInstruments.Thermometer)) return CraftText.Celsius(t);
        string word = CraftLook.VatHeat(t);
        return word == "simmering" ? "simmers" : word; // the readout is short
    }

    /// <summary>Can this piece go into a vat (a hide; metal into a pickle)? Null: yes; else why not.</summary>
    private string NotSoakable(CraftPiece p)
    {
        if (p == null) return "Pick a hide on the left first (or a metal piece, for the pickle).";
        if (p.kind == CraftPieceKind.Hide) return CraftTextiles.IsCloth(Rules, p) ? "Cloth does not soak in the vats." : null;
        return p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners ? null : "Only hides soak in the vats (and metal in a vinegar pickle).";
    }

    private void VatPointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        int at = VatAt(pos);
        if (phase == CraftPointer.Down && at >= 0) vat = at;
        switch (vatTool)
        {
            case VatTool.Hands:
                if (phase == CraftPointer.Cancel) { if (dragging) { dragging = false; dragFromVat = -1; ignorePress = true; } return; }
                if (phase == CraftPointer.Down)
                {
                    CraftPiece inTub = at >= 0 ? SoakingIn(at) : null;
                    if (inTub != null && (p == null || inTub.id != p.id))
                    {
                        ui.Select(inTub.id);
                        said = $"Picked what soaks in vat {at + 1}: drag it out, or into another vat.";
                        ignorePress = true;
                        return;
                    }
                    string why = NotSoakable(p);
                    if (why != null) { said = why; ignorePress = true; return; }
                    if (p.location == CraftLocation.Air && benchSpot.Contains(pos)) { dragging = true; dragFromVat = -1; }
                    else if (p.location == CraftLocation.Vat && at == p.vat) { dragging = true; dragFromVat = at; }
                    else
                    {
                        said = p.location == CraftLocation.Air ? "Press on it where it lies on the bench (left) and drag it into a vat."
                            : p.location == CraftLocation.Vat ? $"It soaks in vat {p.vat + 1}: press on that tub and drag it out, or into another."
                            : $"It is {CraftStationUI.LocationText(p)}: take it down onto the bench first.";
                        ignorePress = true;
                    }
                    dragAt = pos;
                }
                else if (phase == CraftPointer.Drag) dragAt = pos;
                else if (phase == CraftPointer.Up && dragging)
                {
                    dragging = false;
                    string id = p.id;
                    bool metal = p.kind != CraftPieceKind.Hide;
                    if (at >= 0 && at != dragFromVat)
                    {
                        int into = at;
                        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Vat, i2 = into }, (ok, why) =>
                        {
                            if (ok) said = metal ? $"Into the pickle in vat {into + 1}: the scale lifts, then the bright metal etches." : $"Into vat {into + 1}: it soaks.";
                        });
                    }
                    else if (dragFromVat >= 0 && at < 0)
                        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok) said = "Out of the vat, dripping, onto the bench."; });
                    dragFromVat = -1;
                }
                return;
            case VatTool.Bucket:
                if (phase == CraftPointer.Cancel) { if (pouring) { pouring = false; pourVat = -1; pourTime = 0f; said = "The bucket is tipped back: nothing poured."; ignorePress = true; } return; }
                if (phase == CraftPointer.Down)
                {
                    int column = -1;
                    for (int i = 0; i < vats.Count; i++) if (OverTub(i, pos)) column = i;
                    if (column < 0 || AgentId == null) return;
                    // a vat holding another liquor takes none of it: said before anything is poured
                    if (!Ws.CanFillVat(column, AgentId, out string refused)) { said = refused; ignorePress = true; return; }
                    int have = Have(AgentId);
                    if (have == 0) { said = $"You have no {AgentItem()}."; ignorePress = true; return; }
                    vat = column;
                    pouring = true;
                    pourVat = column;
                    pourTime = 0f;
                    pourMax = have;
                }
                else if (phase == CraftPointer.Up && pouring) FinishPour();
                return;
            case VatTool.Paddle:
                if (phase == CraftPointer.Cancel || phase == CraftPointer.Up) { paddle.Clear(); stirVat = -1; return; }
                if (phase == CraftPointer.Down && at >= 0)
                {
                    paddle.Begin(vats[at].center);
                    stirVat = at;
                }
                if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && paddle.Active)
                {
                    paddle.Add(pos);
                    if (paddle.Turns >= 2f)
                    {
                        // two turns stir it, as evenly as the circles were steady
                        float steady = paddle.Steadiness;
                        int into = stirVat;
                        paddle.Begin(vats[into].center);
                        Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = into, a = Mathf.Max(0.01f, steady) }, (ok, why) =>
                        {
                            if (ok) said = steady > 0.7f ? $"Vat {into + 1} stirred: the liquor works in evenly." : $"Vat {into + 1} stirred, unevenly: circle steadily, at one distance from the middle.";
                        });
                    }
                }
                return;
            case VatTool.Plug:
                if (phase == CraftPointer.Cancel) { if (draining) { draining = false; said = "Let go of the plug: the vat stays full."; ignorePress = true; } return; }
                if (phase == CraftPointer.Down)
                    for (int i = 0; i < plugs.Count; i++)
                        if (plugs[i].Contains(pos) || vats[i].Contains(pos) && pos.y < vats[i].yMin + 40) { vat = i; draining = true; drainTime = 0f; }
                return;
        }
    }

    private void FinishPour()
    {
        pouring = false;
        int count = Mathf.FloorToInt(pourTime);
        if (pourMax >= 0) count = Mathf.Min(count, pourMax);
        string id = AgentId;
        int into = pourVat;
        if (count >= 1 && id != null && into >= 0)
        {
            string name = Rules.Agent(id)?.Name ?? id;
            Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = into, id = id, count = count }, (ok, why) =>
            {
                if (ok) said = $"{count} dose{(count == 1 ? "" : "s")} of {name.ToLowerInvariant()} into vat {into + 1}.";
            });
        }
        else if (pourTime > 0.15f)
            said = "Hold the bucket over the vat longer: a dose takes a second of pouring.";
        pourVat = -1;
        pourTime = 0f;
    }

    // ------------------------------------------------------------------ the frame and the rack, by hand
    private void FramePointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (phase == CraftPointer.Cancel)
        {
            if (handle >= 0) { handle = -1; LoadTension(p); said = "Let go of the handle: it is as it was."; ignorePress = true; }
            else if (dragging) { dragging = false; ignorePress = true; }
            return;
        }
        if (phase == CraftPointer.Down)
        {
            // another hide in the frame or on the rack: pick it
            if (PickOnRack(pos, p) || PickInFrame(pos, p))
                return;
            if (p == null || p.kind != CraftPieceKind.Hide) { said = "Pick a hide on the left first."; ignorePress = true; return; }
            if (CraftTextiles.IsCloth(Rules, p)) { said = "Cloth is not laced or hung here."; ignorePress = true; return; }
            if (p.location == CraftLocation.Frame && Station.Has(CraftTools.Frame))
                for (int k = 0; k < 4; k++)
                    if (HandleRect(p, k).Contains(pos))
                    {
                        handle = k;
                        grab = Travel(k, pos) - HandleTravel(p, k); // where on the handle it was taken: it does not jump
                        return;
                    }
            bool onIt = p.location == CraftLocation.Air && benchSpot.Contains(pos)
                        || p.location == CraftLocation.Frame && FrameRect.Contains(pos)
                        || p.location == CraftLocation.Rack && StrokeArea(p, out Rect slot) && slot.Contains(pos);
            if (onIt) { dragging = true; dragAt = pos; }
            else if (p.location == CraftLocation.Vat) { said = $"It is soaking in vat {p.vat + 1}: lift it out first (the vats)."; ignorePress = true; }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            dragAt = pos;
            if (handle >= 0)
                tension[handle] = Mathf.Clamp(Travel(handle, pos) - grab, 0f, TravelPx) / TravelPx * MaxPull(p, handle);
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (handle >= 0)
        {
            handle = -1;
            SendStretch(p);
            return;
        }
        if (!dragging)
            return;
        dragging = false;
        CraftLocation to = Station.Has(CraftTools.Frame) && FrameRect.Contains(pos) ? CraftLocation.Frame
            : Station.Has(CraftTools.Rack) && RackRect.Contains(pos) ? CraftLocation.Rack
            : benchSpot.Contains(pos) || pos.x < FrameRect.xMin - 20 ? CraftLocation.Air : p.location;
        if (to == p.location)
            return;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = to }, (ok, why) =>
        {
            if (!ok) return;
            if (ui.SelectedId == id) LoadTension(Ws.Piece(id));
            said = to == CraftLocation.Frame ? "Laced in the frame, slack: pull each handle into the green, evenly."
                : to == CraftLocation.Rack ? "Hung on the rack to dry: it will pucker a little as it shrinks." : "Taken down onto the bench.";
        });
    }

    /// <summary>How torn a hide is (its tears, however often a torn place tore again).</summary>
    private static double Torn(CraftPiece p)
    {
        double s = 0;
        foreach (CraftDefect d in p.defects) if (d.kind == CraftDefectKind.Tear) s += d.severity;
        return s;
    }

    /// <summary>The frame's four sides as the hands now pull them, sent when a handle is let go; the words are the workshop's verdict.</summary>
    private void SendStretch(CraftPiece p)
    {
        if (p == null)
            return;
        string id = p.id;
        double tears = Torn(p);
        stretchPending = true;
        Send(new CraftRequest(CraftRequestKind.Stretch, id) { a = tension[0], b = tension[1], c = tension[2], d = tension[3] }, (ok, why) =>
        {
            stretchPending = false;
            CraftPiece q = Ws.Piece(id);
            if (ui.SelectedId == id) LoadTension(q);
            if (!ok || q == null || ui.SelectedId != id)
                return;
            double[] pull = CraftLeather.Pulls(Rules, q.hide);
            double least = Math.Min(Math.Min(pull[0], pull[1]), Math.Min(pull[2], pull[3])), most = Math.Max(Math.Max(pull[0], pull[1]), Math.Max(pull[2], pull[3]));
            if (Torn(q) > tears + 1e-6) ui.Message("It tore at the edge: that side was pulled past what the hide can take. Ease it back into the green.");
            else if (most > CraftLeather.OverdrawnFrom) said = "Overdrawn: that edge will thin as it dries - ease it back into the green.";
            else if (CraftLeather.Unevenness(pull) > CraftLeather.UnevenFrom) said = "Uneven: opposite sides pull differently - it will dry out of square.";
            else if (least >= CraftLeather.FlatFrom) said = "Taut and even: it will dry flat.";
            else said = "Slack on some side: it will pucker as it dries - pull every side into the green.";
        });
    }

    // ------------------------------------------------------------------ refreshing
    public override void Refresh()
    {
        CraftPiece p = Selected;
        ShowTools(p);
        RefreshCounts(oilDrop, oils);
        RefreshCounts(dyeDrop, dyes);
        RefreshCounts(agentDrop, agents);
        if (handle < 0 && !stretchPending) LoadTension(p);
        if (vat >= Ws.Vats.Count) vat = 0;
        if (heatLabel != null) heatLabel.text = $"Fire under vat {vat + 1}";
        RefreshPath(p);
        if (info == null)
            return;
        string how;
        switch (place)
        {
            case Place.Vats: how = VatInfo(p); break;
            case Place.Frame: how = FrameInfo(p); break;
            default: how = BeamInfo(p); break;
        }
        info.text = (how + " " + said).Trim();
    }

    private string BeamInfo(CraftPiece p)
    {
        Tool t = beamTool;
        string why = CannotStroke(p, t);
        if (why != null && (p == null || p.kind != CraftPieceKind.Hide || p.location != CraftLocation.Air || CraftTextiles.IsCloth(Rules, p)))
            return why;
        switch (t)
        {
            case Tool.Flesh: return "<b>Flesh side up</b>: pink, glistening patches are flesh and fat - stroke them pale. Keep the pressure in the gauge's green (ease it with the wheel as you go): past it the knife scores clean or thin skin (steep bites sooner, flat is gentler but slower).";
            case Tool.Unhair: return "<b>Grain side up</b>: limed hair scrapes off; unlimed hair holds and the knife scuffs the grain. Past the gauge's green it scuffs the grain too. Left on, the hair spoils the leather: it takes neither slicker nor dye.";
            case Tool.Stake: return "Stroke it over the stake to soften it - best while damp. <color=#7fa6ff>Blue</color>: too wet (it stiffens again); <color=#ff8a70>red</color>: too dry (bark grain cracks).";
            case Tool.Slick: return "Slick the grain smooth and sealed; on a cut part, run along its edges to burnish them." + HairyNote(p, "slicker");
            case Tool.Skive: return "<b>Flesh side up</b>: each stroke shaves it thinner (the depth: what one pass takes off at most). Past the gauge's green one pass goes through; going back over a place in the same stroke cuts up to twice as deep.";
            case Tool.Dye: return (why ?? "Brush the dye on, in even strokes: the item made of it takes its colour. Dye before oiling: oil and wax keep dye out." + SupplyNote(t)) + OiledNote(p) + HairyNote(p, "dye");
            default: return why ?? "Rub it in: an oil makes leather supple, a wax sheds water (and stiffens it a little)." + SupplyNote(t);
        }
    }

    /// <summary>Dyeing an oiled or waxed hide: the dye will be patchy.</summary>
    private static string OiledNote(CraftPiece p)
    {
        if (p == null || p.kind != CraftPieceKind.Hide) return "";
        HideMeans m = Means(p.hide);
        return m.oil + m.wax > 0.2 ? " <color=#ff8a70>It is oiled or waxed: the dye will be patchy.</color>" : "";
    }

    private string VatInfo(CraftPiece p)
    {
        string vs = "";
        string warn = "";
        if (vat < Ws.Vats.Count)
        {
            CraftVatState v = Ws.Vats[vat];
            vs = $"Vat {vat + 1}: {CraftLook.Vat(Rules, v, Station.HasInstrument(CraftInstruments.Thermometer), Station.Reveals)}";
            CraftPiece q = SoakingIn(vat);
            CraftTanningAgent a = Rules.Agent(v.agent);
            if (q != null && q.kind == CraftPieceKind.Hide && v.volume > 0.01)
            {
                HideMeans m = Means(q.hide);
                bool rotting = CraftLeather.Rotting(Ws, q, out _) > 0;
                if (v.temperature > q.hide.shrinkTemperature) warn = " <color=#ff8a70>Too hot: the hide is shrinking (a raw one cooks) - put the fire out.</color>";
                else if (a != null && a.kind != CraftAgentKind.Water && a.kind != CraftAgentKind.Lime && a.kind != CraftAgentKind.Acid)
                {
                    if (v.temperature > a.maxTemperature && CraftLeather.MeanTannage(q.hide) < 0.6) warn = " <color=#ff8a70>Too hot for a half-tanned hide: it is being harmed.</color>";
                    else if (v.strength > a.caseHardeningAbove && m.coreLeast < CraftLeather.CaseHardensBelow) warn = " <color=#ff8a70>Too strong for this raw hide: its outside closes (case hardening) - start weak, strengthen it as it tans.</color>";
                    else if (rotting) warn = " <color=#ff8a70>Too weak to keep the raw hide from rotting: strengthen the liquor.</color>";
                }
                else if (a != null && a.kind == CraftAgentKind.Lime && m.loose > 0.95) warn = " <color=#ff8a70>Over-limed: the grain is going spongy - take it out.</color>";
                else if (rotting) warn = " <color=#ff8a70>Plain water does not keep a raw hide: it is rotting - lime it or tan it.</color>";
            }
        }
        string tip = vatTool == VatTool.Bucket ? "Hold the bucket over a vat: each second pours a dose (into plain water it makes the liquor)."
            : vatTool == VatTool.Paddle ? "Hold and circle in a vat: every two turns stir it - steady circles stir it best."
            : vatTool == VatTool.Plug ? "Hold on a vat's plug a moment to drain it (what soaks in it comes out onto the bench)."
            : NotSoakable(p) ?? "Drag the piece from the bench into a vat, between vats or out; click what soaks in a tub to pick it.";
        const string gauges = " STRENGTH: red past where a raw core's outside closes - no harm once the core is tanned. HEAT: green works best, gold hardens tanned leather, orange harms a half-tanned hide, red shrinks it.";
        return $"{vs}{warn} {tip}{gauges}".Trim();
    }

    private string FrameInfo(CraftPiece p)
    {
        if (frameTool != Tool.Hands)
        {
            string why = CannotStroke(p, frameTool);
            if (why != null) return why;
            string where = RackPlace(p, out _, out bool closeUp) && closeUp ? "where it hangs (close up, over the frame)" : "where it hangs";
            return frameTool == Tool.Stake ? $"Stake it {where}, while the dryness bar is in its gold band. <color=#7fa6ff>Blue</color>: too wet; <color=#ff8a70>red</color>: too dry (bark grain cracks)."
                : frameTool == Tool.Slick ? $"Slick the grain smooth {where}." + HairyNote(p, "slicker")
                : frameTool == Tool.Dye ? $"Brush the dye on {where} (dye before oiling)." + SupplyNote(frameTool) + OiledNote(p) + HairyNote(p, "dye")
                : $"Rub the oil or wax in {where}." + SupplyNote(frameTool);
        }
        if (p == null || p.kind != CraftPieceKind.Hide) return "Pick a hide on the left first.";
        if (p.location == CraftLocation.Vat) return $"It is soaking in vat {p.vat + 1}: lift it out first (the vats).";
        if (p.location == CraftLocation.Frame)
            return "Drag each handle outward along its PULL gauge, what that edge can take - <color=#9aa0a6>grey</color> slack (it puckers as it dries), <color=#7fd18b>green</color> taut (it dries flat), <color=#ffb070>amber</color> overdrawn (the edge thins), <color=#ff8a70>red</color> it tears. Every side in the green, opposite sides alike.";
        if (p.location == CraftLocation.Rack)
            return "It hangs on the rack, drying (it puckers a little). Stake it as it dries - the gold band on the dryness bar. Drag it back to the bench to take it down.";
        if (p.location != CraftLocation.Air) return $"It is {CraftStationUI.LocationText(p)}: bring it to the bench first.";
        return Station.Has(CraftTools.Frame) ? "Drag the hide from the bench onto the frame (laced in slack) or the rack (it hangs to dry)." : "Drag the hide from the bench onto the rack to dry it.";
    }

    /// <summary>Slicking or dyeing a hide with its hair still on: the hair keeps the <paramref name="what"/> off the grain.</summary>
    private string HairyNote(CraftPiece p, string what) =>
        p != null && p.kind == CraftPieceKind.Hide && CraftLeather.HairLeft(Rules, p.hide) > 0
            ? $" <color=#ff8a70>Its hair is still on: it keeps the {what} off the grain - scrape it off first.</color>" : "";
}

/// <summary>
/// The cutting table: lay a pattern on the hide where the leather is good (the wheel turns it; the footprint is centred on
/// the pointer, snaps onto a pattern chalked there; red rings are scars and holes, amber ones the work's own marks - knife
/// scores, tears, rot - and all would go into the part; *Show thickness* shades the hide by how thick it is for the
/// pattern), click to mark it, then cut round its true outline with the knife - as many presses as it takes, the knife
/// wandering as the hands tremble; the line is cut only where the moving blade runs along it (solid green as far as it
/// is), and the blade's track is green on the line, red where it wandered. The part comes out by itself once cut all
/// round (or *Cut it out* from 90%: the rest tears). The closer to the line, the cleaner the edge - never cleaner than
/// these hands cut, a little cleaner along the pattern's chalk; the words on it are the part's own. Cloth is cut at the tailor's
/// table - at a leather bench with none, here, its pattern's grain shown as an arrow (amber across the bolt's: weaker); a
/// part already cut is fitted, not cut again. *Chalk it* marks a pattern on the hide to cut later.
/// </summary>
public sealed class CraftCuttingMode : CraftMode
{
    /// <summary>Where the knife runs on the line: full closeness within this (pixels), none past <see cref="ZeroPx"/>.</summary>
    private const float FullPx = 3f, ZeroPx = 14f;
    /// <summary>The line is cut where the blade runs past it this close (pixels; never past 0.45 of a cell): a point of it every so far along each side.</summary>
    private const float TouchPx = 10f;
    /// <summary>How far the blade wanders from the hand per unit of the hands' unsteadiness (pixels), and over how much travel it sways.</summary>
    private const float WanderPx = 110f, WanderStep = 40f;
    /// <summary>The part can be cut out from this share of its outline round; each hundredth left uncut tears and costs this much of the cut's cleanness.</summary>
    private const float FreeFrom = 0.9f, TornCost = 2.5f;

    /// <summary>A pattern marked on a hide and the cut made round it so far.</summary>
    private sealed class Marking
    {
        public string piece, pattern;
        public int x, y, rotation;
        public List<List<Vector2>> outline;
        // Points along the outline, at most a blade's touch apart on every side (sideFirst: each side's first point, in the
        // outline's order, and one more entry at the end): the line is cut as far as the moving blade ran past them.
        public Vector2[] points;
        public int[] sideFirst;
        public bool[] passed;
        public int passedCount;
        public readonly List<List<Vector3>> knife = new List<List<Vector3>>(); // where the blade ran, press by press (x, y, how far off the line)
        public double closeSum, lenSum;
        public float travelled;
        public bool sent;
        /// <summary>How far round the outline it is cut (the share of the line the blade ran along).</summary>
        public float Coverage => points != null && points.Length > 0 ? passedCount / (float)points.Length : 0f;
        public float Closeness => lenSum > 0 ? (float)(closeSum / lenSum) : 0f;
        public bool Started => passedCount > 0 || knife.Count > 0;
    }

    private TMP_Dropdown pattern;
    private TextMeshProUGUI info;
    private Button thickButton;
    // The patterns listed - leather's, or cloth's at a leather bench with no tailor's table - and what the designs want of
    // each when cut from that: the thickness range and what it makes.
    private List<string> patterns = new List<string>();
    private Dictionary<string, (float min, float max, string uses)> wants = new Dictionary<string, (float, float, string)>();
    private readonly List<string> leatherPatterns = new List<string>(), clothPatterns = new List<string>();
    private readonly Dictionary<string, (float min, float max, string uses)> leatherWants = new Dictionary<string, (float, float, string)>(),
        clothWants = new Dictionary<string, (float, float, string)>();
    private bool listingCloth;
    private readonly Dictionary<string, Marking> kept = new Dictionary<string, Marking>(); // cuts begun on hides not picked now
    private Marking mark;
    private int rotation;
    private bool placing, cutting, aborted, showThickness;
    private Vector2 lastPointer;
    private float worked; // pixels cut since the arm last moved (it tires a little as it cuts)
    private Rect area = new Rect(30, 24, 660, 350);
    private string said = "";

    public override string Title => "Cut";
    /// <summary>Leatherwork - or, for cloth cut here, the tailor's craft (what the station counts the cut as, and caps it by).</summary>
    public override CraftDiscipline Discipline => ui != null && Station != null && ClothHere(Selected) ? CraftDiscipline.Tailoring : CraftDiscipline.Leatherwork;
    public override bool Available(CraftStation s) => s.Has(CraftTools.LeatherBench);
    public override string Hint => "Click to lay the pattern where the leather is good (the wheel turns it), then cut round its dashed outline with the knife: it comes out once cut all round. Right-click lifts the knife.";

    /// <summary>Cloth is cut at the tailor's table; at a leather bench with none, here (along its grain).</summary>
    private bool ClothHere(CraftPiece p) => CraftTextiles.IsCloth(Rules, p) && !Station.Has(CraftTools.TailorTable);

    public override string Busy => mark != null && mark.Started && !mark.sent ? $"cutting out the {Rules.Pattern(mark.pattern)?.Name.ToLowerInvariant() ?? "pattern"}" : null;

    /// <summary>The knife follows the pointer smoothly while it is over the board, and the cut while it is made.</summary>
    public override void Tick(float dt) => LiveDraw = cutting || ui.Hover.x >= 0;

    public override void Build(RectTransform controls)
    {
        Wants();
        listingCloth = ClothHere(Selected);
        patterns = listingCloth ? clothPatterns : leatherPatterns;
        wants = listingCloth ? clothWants : leatherWants;
        RectTransform r1 = Row(controls);
        Text(r1, "Pattern", 13f, 56f);
        pattern = Drop(r1, PatternNames(), 0, 280f);
        pattern.onValueChanged.AddListener(PatternChosen);
        Btn(r1, "Turn it", Turn, 90f);
        Btn(r1, "Lift the pattern", Lift, 140f, UITheme.ButtonStyle.Ghost);
        RectTransform r2 = Row(controls);
        Btn(r2, "Chalk it", Chalk, 100f);
        Btn(r2, "Rub out the chalk", RubOut, 150f, UITheme.ButtonStyle.Ghost);
        thickButton = Btn(r2, "Show thickness", () => { showThickness = !showThickness; UITheme.SetSelected(thickButton, showThickness); }, 140f);
        UITheme.SetSelected(thickButton, showThickness);
        Btn(r2, "Cut it out", () => CutOut(false), 120f, UITheme.ButtonStyle.Primary);
        info = InfoRow(controls, 56f);
    }

    /// <summary>
    /// Which patterns the designs cut from leather and which from cloth (a part of either fabric counts for both), with the
    /// thickness each wants and what it makes; a pattern no design uses is listed for both.
    /// </summary>
    private void Wants()
    {
        leatherWants.Clear();
        clothWants.Clear();
        var uses = new Dictionary<(string, bool), List<string>>();
        void Want(Dictionary<string, (float min, float max, string uses)> into, bool cloth, CraftDesign d, CraftLeatherSpec l)
        {
            float lo = l.thicknessMin, hi = l.thicknessMax;
            if (into.TryGetValue(l.pattern, out var was)) { lo = Mathf.Min(lo, was.min); hi = Mathf.Max(hi, was.max); }
            into[l.pattern] = (lo, hi, "");
            if (!uses.TryGetValue((l.pattern, cloth), out List<string> list)) uses[(l.pattern, cloth)] = list = new List<string>();
            string what = d.Name.ToLowerInvariant();
            if (!list.Contains(what) && list.Count < 3) list.Add(what);
        }
        foreach (CraftDesign d in Rules.Designs)
            foreach (CraftComponentSpec c in d.components)
            {
                if (c.supply || c.kind != CraftPieceKind.Hide || c.leather == null || string.IsNullOrEmpty(c.leather.pattern)) continue;
                if (c.leather.fabric != CraftFabric.Cloth) Want(leatherWants, false, d, c.leather);
                if (c.leather.fabric != CraftFabric.Leather) Want(clothWants, true, d, c.leather);
            }
        foreach (KeyValuePair<(string, bool), List<string>> kv in uses)
        {
            Dictionary<string, (float min, float max, string uses)> into = kv.Key.Item2 ? clothWants : leatherWants;
            (float min, float max, string _) w = into[kv.Key.Item1];
            into[kv.Key.Item1] = (w.min, w.max, string.Join(", ", kv.Value));
        }
        leatherPatterns.Clear();
        clothPatterns.Clear();
        foreach (CraftPattern p in Rules.Patterns)
        {
            bool unused = !leatherWants.ContainsKey(p.id) && !clothWants.ContainsKey(p.id);
            if (unused || leatherWants.ContainsKey(p.id)) leatherPatterns.Add(p.id);
            if (unused || clothWants.ContainsKey(p.id)) clothPatterns.Add(p.id);
        }
    }

    private List<string> PatternNames()
    {
        var names = new List<string>();
        foreach (string id in patterns)
        {
            CraftPattern p = Rules.Pattern(id);
            names.Add(wants.TryGetValue(id, out var wnt) ? $"{p.Name} · {wnt.min:0.#}-{wnt.max:0.#} mm" : p.Name);
        }
        return names.Count > 0 ? names : new List<string> { "-" };
    }

    /// <summary>Lists cloth's patterns (or leather's) when a piece of the other fabric is picked.</summary>
    private void Relist(bool cloth)
    {
        if (cloth == listingCloth || pattern == null)
            return;
        listingCloth = cloth;
        patterns = cloth ? clothPatterns : leatherPatterns;
        wants = cloth ? clothWants : leatherWants;
        pattern.ClearOptions();
        pattern.AddOptions(PatternNames());
        pattern.SetValueWithoutNotify(0);
        pattern.RefreshShownValue();
    }

    private CraftPattern Current => patterns.Count > 0 && pattern != null ? Rules.Pattern(patterns[Mathf.Clamp(pattern.value, 0, patterns.Count - 1)]) : null;

    // ------------------------------------------------------------------ the marking
    private void Turn()
    {
        if (mark != null) { said = "Lift the pattern to turn it."; return; }
        rotation = (rotation + 1) % 4;
    }

    private void PatternChosen(int v)
    {
        if (mark == null)
            return;
        int at = patterns.IndexOf(mark.pattern);
        if (at >= 0) pattern.SetValueWithoutNotify(at);
        said = "Lift the pattern to choose another.";
    }

    /// <summary>Lifts the pattern off the hide - asked twice once the knife has started round it (that cut is lost).</summary>
    private void Lift()
    {
        if (mark == null)
            return;
        if (mark.sent) { said = "The cut is on its way to the station."; return; }
        if (mark.Started && !Confirm("lift", "Click Lift the pattern again: the cut made round it so far is lost."))
            return;
        Forget();
        said = "The pattern is lifted.";
    }

    private void Forget()
    {
        if (mark != null) kept.Remove(mark.piece);
        mark = null;
        placing = cutting = false;
    }

    private void Mark(CraftPiece p, CraftPattern pat, int gx, int gy)
    {
        var m = new Marking { piece = p.id, pattern = pat.id, x = gx, y = gy, rotation = rotation };
        m.outline = CraftArt.PatternOutline(p.hide, area, pat, gx, gy, rotation);
        // the line to cut, as points at most the blade's touch apart: a dab cuts only where it lands, a run along it all of it
        CraftArt.HideLayout(p.hide, area, out _, out float cell);
        float touch = Touch(cell);
        var points = new List<Vector2>();
        var first = new List<int>();
        foreach (List<Vector2> loop in m.outline)
            for (int k = 0; k < loop.Count; k++)
            {
                Vector2 a = loop[k], b = loop[(k + 1) % loop.Count];
                int n = Mathf.Max(3, Mathf.CeilToInt(Vector2.Distance(a, b) / touch));
                first.Add(points.Count);
                for (int j = 0; j < n; j++) points.Add(Vector2.Lerp(a, b, (j + 0.5f) / n));
            }
        first.Add(points.Count);
        m.points = points.ToArray();
        m.sideFirst = first.ToArray();
        m.passed = new bool[m.points.Length];
        mark = m;
        said = "Marked. Now cut round the dashed outline with the knife.";
    }

    /// <summary>Where the pattern would lie with the pointer at <paramref name="pos"/>: centred on it, or on a matching chalk mark within a cell.</summary>
    private void Placement(HideState h, CraftPattern pat, Vector2 pos, out int gx, out int gy, out bool onChalk)
    {
        pat.Size(rotation, out int pw, out int ph);
        Vector2 v = CraftArt.HideCells(h, area, pos);
        gx = Mathf.FloorToInt(v.x - pw * 0.5f + 0.5f);
        gy = Mathf.FloorToInt(v.y - ph * 0.5f + 0.5f);
        onChalk = false;
        foreach (CraftLayoutPlacement l in h.layout)
            if (l.pattern == pat.id && l.rotation == rotation && Mathf.Abs(l.x - gx) <= 1 && Mathf.Abs(l.y - gy) <= 1)
            {
                gx = l.x;
                gy = l.y;
                onChalk = true;
                return;
            }
    }

    /// <summary>How near the moving blade must pass a point of the line to cut it there (pixels; never past 0.45 of a cell).</summary>
    private static float Touch(float cell) => Mathf.Min(TouchPx, cell * 0.45f);

    /// <summary>Would a cut there go through a pattern chalked on the hide (other than the one it lies on)? That chalk is lost.</summary>
    private bool CutsChalk(HideState h, CraftPattern pat, int gx, int gy, int rot) =>
        // one rule with the cloth table's: any chalk but the very placement it is laid on (CraftTextiles.Overlaps)
        CraftTextiles.Overlaps(Rules, h, pat, gx, gy, rot, CraftLeather.ChalkAt(h, pat.id, gx, gy, rot)) >= 0;

    /// <summary>Why the selected piece cannot be cut here (null: it can).</summary>
    private string CannotCut(CraftPiece p)
    {
        if (p == null) return "Pick a hide or a piece of leather on the left.";
        if (p.kind != CraftPieceKind.Hide) return "That is not leather: pick a hide on the left.";
        if (CraftTextiles.IsCloth(Rules, p) && Station.Has(CraftTools.TailorTable))
            return "Cloth is cut at the tailor's table (Tailor → Cut cloth), where its grain is shown and the shears are.";
        if (!string.IsNullOrEmpty(p.hide.pattern)) return "It is already cut to a pattern: fit it into an item (Assemble). Cut parts from a whole hide.";
        if (p.location != CraftLocation.Air) return $"It is {CraftStationUI.LocationText(p)}: take it out first (the Tannery).";
        return null;
    }

    // ------------------------------------------------------------------ leaving, the piece changing
    public override void Leave()
    {
        cutting = false; // the knife is lifted; the marking stays on its hide
        placing = false;
        aborted = false;
    }

    public override void SelectionChanging(string from, string to)
    {
        Leave();
        // a cut begun on that hide waits for it; a marking with no cut yet is lifted
        if (mark != null && mark.Started && !mark.sent) kept[mark.piece] = mark;
        mark = null;
        said = "";
    }

    public override void SelectionChanged(string to)
    {
        CraftPiece picked = to != null ? Ws.Piece(to) : null;
        if (picked != null && picked.kind == CraftPieceKind.Hide) Relist(ClothHere(picked)); // cloth's patterns for cloth
        if (to == null || !kept.TryGetValue(to, out Marking m))
            return;
        kept.Remove(to);
        CraftPiece p = Ws.Piece(to);
        CraftPattern pat = Rules.Pattern(m.pattern);
        if (p == null || pat == null || CannotCut(p) != null || !CraftLeather.Fits(p.hide, pat, m.x, m.y, m.rotation))
            return;
        mark = m;
        rotation = m.rotation;
        int at = patterns.IndexOf(m.pattern);
        if (at >= 0) pattern.SetValueWithoutNotify(at);
        said = "Your cut round it so far is still there.";
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        // The cutting board.
        c.Rect(area.xMin - 12, area.yMin - 10, area.xMax + 12, area.yMax + 10, new Color(0.42f, 0.33f, 0.24f));
        for (int k = 0; k < 8; k++) c.Line(area.xMin - 12, area.yMin + k * 48, area.xMax + 12, area.yMin + k * 48 + 6, new Color(0.36f, 0.28f, 0.2f), 1f);
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        CraftArt.DrawHide(c, Rules, p.hide, area);
        if (CannotCut(p) != null)
        {
            c.Rect(area.xMin, area.yMin, area.xMax, area.yMax, new Color(0.08f, 0.07f, 0.06f, 0.55f));
            return;
        }
        CraftPattern pat = mark != null ? Rules.Pattern(mark.pattern) : Current;
        if (showThickness) ShadeThickness(c, p.hide, pat);
        CraftArt.DrawChalk(c, Rules, p.hide, area);
        CraftArt.HideLayout(p.hide, area, out _, out float cell);
        if (pat == null)
            return;
        Vector2 h = ui.Hover;
        if (mark != null)
        {
            CraftArt.DrawPattern(c, p.hide, area, pat, mark.x, mark.y, mark.rotation, new Color(1f, 1f, 1f, 0.1f));
            // The line to follow: dashed, solid green as far as it is cut; where the blade ran, green on the line, red off it.
            CraftArt.DrawOutline(c, mark.outline, new Color(1f, 1f, 1f, 0.8f), 1.5f, true);
            int s = 0;
            foreach (List<Vector2> loop in mark.outline)
                for (int k = 0; k < loop.Count; k++, s++)
                {
                    Vector2 a = loop[k], b = loop[(k + 1) % loop.Count];
                    int from = mark.sideFirst[s], n = mark.sideFirst[s + 1] - from;
                    for (int j = 0; j < n; j++)
                    {
                        if (!mark.passed[from + j]) continue;
                        Vector2 u = Vector2.Lerp(a, b, j / (float)n), v = Vector2.Lerp(a, b, (j + 1) / (float)n);
                        c.Line(u.x, u.y, v.x, v.y, Good, 2.5f);
                    }
                }
            foreach (List<Vector3> run in mark.knife)
                for (int i = 1; i < run.Count; i++)
                {
                    Color line = Color.Lerp(Good, Harm, Mathf.InverseLerp(FullPx, ZeroPx, run[i].z));
                    c.Line(run[i - 1].x, run[i - 1].y, run[i].x, run[i].y, line, 2f);
                }
            CraftArt.Label(c, 8, 387, "CUT ROUND", CraftArt.LabelInk);
            Meter(c, 84, 386, 620, 10, mark.Coverage, new Color(0.5f, 1f, 0.6f), (FreeFrom, 1f, GoodBand));
            if (CraftTextiles.IsCloth(Rules, p)) GrainArrow(c, p.hide, pat, mark.x, mark.y, mark.rotation);
        }
        else if (area.Contains(h))
        {
            Placement(p.hide, pat, h, out int gx, out int gy, out bool onChalk);
            bool fits = CraftLeather.Fits(p.hide, pat, gx, gy, rotation);
            CraftArt.DrawPattern(c, p.hide, area, pat, gx, gy, rotation, fits ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(1f, 0.35f, 0.3f, 0.35f));
            pat.Size(rotation, out int pw, out int ph);
            // What goes into the part from under the pattern: a scar, a brand or a hole in red; the work's own marks - a
            // knife's score, a tear, a skived-through spot, rot - in amber.
            int[] marked = CraftLeather.WorkMarks(p, WorkMarkFrom);
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    if (!pat.Covers(x, y, rotation)) continue;
                    HideCell hc = p.hide.Cell(gx + x, gy + y);
                    if (hc == null || !hc.present) continue;
                    bool scar = Scar(hc), byWork = !scar && marked[(gy + y) * p.hide.width + gx + x] != 0;
                    if (!scar && !byWork) continue;
                    Rect cr = CraftArt.HideCellRect(p.hide, area, gx + x, gy + y);
                    c.Ring(cr.center.x, cr.center.y, cell * 0.45f, 1.5f, scar ? Harm : Fair);
                }
            if (CraftTextiles.IsCloth(Rules, p)) GrainArrow(c, p.hide, pat, gx, gy, rotation);
            if (fits)
            {
                // its outline: green on its own chalk, amber where the cut would go through other chalk
                Color line = onChalk ? Good : CutsChalk(p.hide, pat, gx, gy, rotation) ? Fair : new Color(1f, 1f, 1f, 0.6f);
                CraftArt.DrawOutline(c, CraftArt.PatternOutline(p.hide, area, pat, gx, gy, rotation), line, 1.5f, !onChalk);
            }
        }
        // The knife under the pointer: its blade where it really cuts (the hands sway it).
        if (area.Contains(h))
        {
            Vector2 tip = cutting && mark != null ? h + Wander(mark.travelled) : h;
            c.Line(tip.x, tip.y, tip.x + 12, tip.y + 18, new Color(0.8f, 0.82f, 0.86f), 2f);
            c.Line(tip.x + 12, tip.y + 18, tip.x + 22, tip.y + 34, new Color(0.45f, 0.3f, 0.18f), 4f);
        }
    }

    /// <summary>
    /// Cloth: the pattern's grain as an arrow on it - green along the cloth's (up the bolt, unturned or turned half round),
    /// amber across it (a part cut across the grain stretches and is weaker).
    /// </summary>
    private void GrainArrow(CraftPixels c, HideState h, CraftPattern pat, int gx, int gy, int rot)
    {
        pat.Size(rot, out int pw, out int ph);
        Vector2 a = CraftArt.HidePoint(h, area, gx + pw * 0.5f, gy + ph * 0.5f);
        bool along = ((rot % 4) + 4) % 4 % 2 == 0;
        Color col = along ? Good : Fair;
        Vector2 d = along ? new Vector2(0, 1) : new Vector2(1, 0);
        CraftArt.HideLayout(h, area, out _, out float cell);
        float len = Mathf.Min(cell * 0.8f * (along ? ph : pw), 60f);
        Vector2 tip = a + d * len * 0.5f, tail = a - d * len * 0.5f, side = new Vector2(-d.y, d.x) * 6f;
        c.Line(tail.x, tail.y, tip.x, tip.y, col, 2.5f);
        c.Line(tip.x, tip.y, tip.x - d.x * 8 + side.x, tip.y - d.y * 8 + side.y, col, 2.5f);
        c.Line(tip.x, tip.y, tip.x - d.x * 8 - side.x, tip.y - d.y * 8 - side.y, col, 2.5f);
    }

    /// <summary>The hide shaded by thickness for the pattern: green where it suits it, blue thinner, amber thicker (grey by thickness when no design says).</summary>
    private void ShadeThickness(CraftPixels c, HideState h, CraftPattern pat)
    {
        bool known = pat != null && wants.TryGetValue(pat.id, out _);
        (float min, float max, string _) want = known ? wants[pat.id] : (0f, 0f, "");
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                HideCell hc = h.Cell(x, y);
                if (hc == null || !hc.present) continue;
                float t = (float)CraftLeather.CellThickness(hc);
                Color col = !known ? new Color(1f, 1f, 1f, Mathf.Clamp01(t / 8f) * 0.5f)
                    : t < want.min ? new Color(0.3f, 0.5f, 1f, 0.45f) : t > want.max ? new Color(1f, 0.6f, 0.2f, 0.45f) : new Color(0.3f, 0.9f, 0.4f, 0.35f);
                Rect r = CraftArt.HideCellRect(h, area, x, y);
                c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, col);
            }
    }

    // ------------------------------------------------------------------ the pointer
    /// <summary>How far the blade has swayed from the hand after <paramref name="travelled"/> pixels of cutting (smoothly; more for shaky, tired hands).</summary>
    private Vector2 Wander(float travelled)
    {
        float k = travelled / WanderStep;
        int i = Mathf.FloorToInt(k);
        float f = k - i;
        f = f * f * (3f - 2f * f);
        Vector2 a = Sway(i), b = Sway(i + 1);
        return Vector2.Lerp(a, b, f) * (float)(Hands.Unsteadiness * WanderPx);
    }

    private Vector2 Sway(int i) => new Vector2((float)(CraftMath.Noise(Hands.seed, i, 401) * 2 - 1), (float)(CraftMath.Noise(Hands.seed, i, 402) * 2 - 1));

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Scroll)
        {
            if (mark == null) rotation = (rotation + (scroll > 0 ? 1 : 3)) % 4;
            else said = "Lift the pattern to turn it.";
            return;
        }
        if (phase == CraftPointer.Cancel)
        {
            // the knife comes up: what it cut stays cut, the rest of this press does nothing
            if (cutting) { cutting = false; aborted = true; said = "The knife is lifted: press on the line again to go on."; }
            return;
        }
        if (phase == CraftPointer.Down) { aborted = false; placing = false; }
        if (aborted)
        {
            if (phase == CraftPointer.Up) aborted = false;
            return;
        }
        CraftPiece p = Selected;
        string why = CannotCut(p);
        if (why != null)
        {
            if (phase == CraftPointer.Down) said = why;
            return;
        }
        if (mark == null)
        {
            CraftPattern pat = Current;
            if (phase != CraftPointer.Down || pat == null)
                return;
            Placement(p.hide, pat, pos, out int gx, out int gy, out _);
            if (!CraftLeather.Fits(p.hide, pat, gx, gy, rotation)) { said = "The pattern does not fit there: it runs off the hide (or over a hole)."; return; }
            Mark(p, pat, gx, gy);
            placing = true; // this press marked it: its drag and release do not cut
            return;
        }
        if (placing)
        {
            if (phase == CraftPointer.Up) placing = false;
            return;
        }
        if (mark.sent)
            return; // waiting for the station
        if (phase == CraftPointer.Down)
        {
            cutting = true;
            mark.knife.Add(new List<Vector3>());
            lastPointer = pos;
            CutAt(pos, true);
            return;
        }
        if (!cutting)
            return;
        CutAt(pos, false);
        if (phase == CraftPointer.Up) cutting = false;
    }

    /// <summary>The blade at the hand (swayed by its tremble): how close it runs to the line, and how far along it it cuts.</summary>
    private void CutAt(Vector2 pos, bool first)
    {
        CraftPiece p = Ws.Piece(mark.piece);
        if (p == null)
            return;
        CraftArt.HideLayout(p.hide, area, out _, out float cell);
        float step = first ? 0f : Vector2.Distance(lastPointer, pos);
        mark.travelled += step;
        lastPointer = pos;
        // the arm tires a little as it cuts (by the length cut, whatever the sampling)
        worked += step;
        while (worked >= 60f)
        {
            worked -= 60f;
            Hands.Move(0.1);
        }
        Vector2 at = pos + Wander(mark.travelled);
        float d = CraftArt.NearestOnOutline(mark.outline, at, out _, out _, out _);
        List<Vector3> run = mark.knife[mark.knife.Count - 1];
        if (run.Count > 0)
        {
            Vector3 last = run[run.Count - 1];
            float len = Vector2.Distance(new Vector2(last.x, last.y), at);
            mark.closeSum += len * (1 - CraftMath.Smooth(FullPx, ZeroPx, d));
            mark.lenSum += len;
            if (run.Count >= 2000)
            {
                // a very long run goes on in a new one (drawn the same)
                run = new List<Vector3> { last };
                mark.knife.Add(run);
            }
        }
        run.Add(new Vector3(at.x, at.y, d));
        // The moving blade cuts the line where it runs past it (a press landing cuts nothing): the whole outline is cut
        // only by running the knife all along it, never by a dab here and there.
        float touch = Touch(cell);
        if (step > 0f)
            for (int k = 0; k < mark.points.Length; k++)
                if (!mark.passed[k] && Vector2.Distance(mark.points[k], at) <= touch)
                {
                    mark.passed[k] = true;
                    mark.passedCount++;
                }
        if (mark.passedCount >= mark.points.Length && mark.points.Length > 0)
        {
            // cut all round: the part comes out
            cutting = false;
            aborted = true;
            CutOut(true);
        }
    }

    /// <summary>How clean the cut is: how close the blade ran to the line, less what tore where it was not cut; never cleaner than these hands cut.</summary>
    private double Accuracy(Marking m)
    {
        double torn = 1 - Math.Min(1, TornCost * (1 - m.Coverage));
        return Math.Max(0, Math.Min(CraftRequestGuard.CutCap(CraftRequestGuard.FloorOf(Hands.skill)), m.Closeness * torn));
    }

    /// <summary>How clean the part comes out as cut now: the cut's own cleanness - along the pattern chalked there, the workshop's <see cref="CraftLeather.ChalkedCut"/> cleaner.</summary>
    private double Expected(Marking m)
    {
        CraftPiece p = Ws.Piece(m.piece);
        bool chalked = p != null && CraftLeather.ChalkAt(p.hide, m.pattern, m.x, m.y, m.rotation) >= 0;
        return Math.Min(1, Accuracy(m) + (chalked ? CraftLeather.ChalkedCut : 0));
    }

    private void CutOut(bool whole)
    {
        if (mark == null) { said = "Mark the pattern on the hide first, then cut round it."; return; }
        if (mark.sent) return;
        if (!whole && mark.Coverage < FreeFrom)
        {
            said = $"Cut further round first: {mark.Coverage * 100:0}% of the outline is cut (from {FreeFrom * 100:0}% it can come out; the rest tears free).";
            return;
        }
        Marking m = mark;
        double accuracy = Accuracy(m);
        float coverage = m.Coverage;
        string name = Rules.Pattern(m.pattern)?.Name ?? "part";
        m.sent = true;
        Send(new CraftRequest(CraftRequestKind.Cut, m.piece) { id = m.pattern, i0 = m.x, i1 = m.y, i2 = m.rotation, a = (float)accuracy }, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer)
            {
                m.sent = false; // a real refusal: the marking and the cut stay, to finish once it is put right
                if (mark != m) kept[m.piece] = m; // (another piece was picked meanwhile: it waits for its hide)
                return;
            }
            if (mark == m) Forget();
            else kept.Remove(m.piece);
            if (!ok)
                return;
            string part = null;
            foreach (CraftEvent e in AnswerEvents) if (e.Kind == CraftEventKind.Cut) part = e.Piece;
            bool chalk = part != null && LastRecord(part).Contains("along the chalk");
            // in the part's own words: as cleanly as the station cut it (along the chalk, cleaner than the knife alone)
            double q = part != null && Ws.Piece(part) != null ? Ws.Piece(part).hide.cutQuality : accuracy;
            string clean = q > CraftLeather.CleanCut ? "A clean cut, right on the line" : q > CraftLeather.RaggedCut ? "Cut out: the edge wanders a little" : "Cut out, raggedly: burnish its edges well";
            string torn = coverage < 0.999f ? $" ({(1 - coverage) * 100:0}% of the edge tore free)" : "";
            ui.Message($"{clean}{(chalk ? ", along the chalk" : "")}{torn}: {CraftText.A(name)} {name.ToLowerInvariant()}.");
        });
    }

    private void Chalk()
    {
        CraftPiece p = Selected;
        string why = CannotCut(p);
        if (why != null) { said = why; return; }
        if (mark == null) { said = "Mark where it goes first (click the hide), then chalk it."; return; }
        if (mark.Started) { said = "The knife has started round it: cut it out, or lift it."; return; }
        Marking m = mark;
        Send(new CraftRequest(CraftRequestKind.LayoutPattern, m.piece) { id = m.pattern, i0 = m.x, i1 = m.y, i2 = m.rotation }, (ok, reason) =>
        {
            if (!ok) return;
            if (mark == m) Forget();
            said = "Chalked: cut along the chalk later and the edge comes cleaner. Lay the next one.";
        });
    }

    private void RubOut()
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Hide || p.hide.layout.Count == 0) { said = "There is no chalk on it."; return; }
        if (!Confirm("rub", "Click Rub out the chalk again: every pattern chalked on it is rubbed off."))
            return;
        Send(new CraftRequest(CraftRequestKind.LayoutPattern, p.id) { flag = true }, (ok, why) => { if (ok) said = "The chalk is rubbed off."; });
    }

    // ------------------------------------------------------------------ refreshing
    public override void Refresh()
    {
        // a cut kept for a hide no longer here goes with it; a marking whose leather went (cut away elsewhere) is lifted
        var gone = new List<string>();
        foreach (string id in kept.Keys) if (Ws.Piece(id) == null) gone.Add(id);
        foreach (string id in gone) kept.Remove(id);
        CraftPiece p = Selected;
        if (mark != null && !mark.sent)
        {
            CraftPattern mp = Rules.Pattern(mark.pattern);
            if (p == null || p.id != mark.piece || mp == null || !CraftLeather.Fits(p.hide, mp, mark.x, mark.y, mark.rotation))
            {
                Forget();
                said = "The leather under the pattern is gone: mark it again.";
            }
        }
        if (mark == null && p != null && p.kind == CraftPieceKind.Hide) Relist(ClothHere(p));
        if (info == null)
            return;
        string why = CannotCut(p);
        string how;
        if (why != null)
            how = why;
        else if (mark == null)
        {
            CraftPattern pat = Current;
            string under = "";
            Vector2 h = ui.Hover;
            if (pat != null && area.Contains(h))
            {
                Placement(p.hide, pat, h, out int gx, out int gy, out bool onChalk);
                if (CraftLeather.Fits(p.hide, pat, gx, gy, rotation))
                    under = " " + Under(p, pat, gx, gy, rotation) + (onChalk ? " On its chalk: the cut comes cleaner." : CutsChalk(p.hide, pat, gx, gy, rotation) ? " <color=#ffb070>It would cut through chalk laid there.</color>" : "");
            }
            string use = pat != null && wants.TryGetValue(pat.id, out var wnt) && wnt.uses.Length > 0 ? $"{pat.Name}: for {wnt.uses}. " : "";
            how = ClothHere(p)
                ? $"{use}Move it over the cloth (green fits, red runs off; red rings are flaws, amber ones damage - both go into the part) - its arrow is its grain: green along the bolt, amber across it (weaker) - and click to mark it.{under}"
                : $"{use}Move it over the hide (green fits, red runs off; red rings are scars and holes, amber ones the work's marks - knife scores, tears, rot - and all go into the part) and click to mark it.{under}";
        }
        else
        {
            CraftPattern mp = Rules.Pattern(mark.pattern);
            bool chalked = CraftLeather.ChalkAt(p.hide, mark.pattern, mark.x, mark.y, mark.rotation) >= 0;
            string state = mark.sent ? "Cutting it out..." : $"{mark.Coverage * 100:0}% cut round, {mark.Closeness * 100:0}% on the line: as cut now it comes out {Expected(mark) * 100:0}% clean{(chalked ? " (along its chalk)" : "")}.";
            how = $"Cut round the dashed outline: press on it and draw the knife all along it (lift and press again to go on; right-click lifts the knife) - the line is cut only where the blade runs. It comes out once cut all round, or Cut it out from {FreeFrom * 100:0}%. {state} {Under(p, mp, mark.x, mark.y, mark.rotation)}";
        }
        if (showThickness && why == null)
        {
            CraftPattern pat = mark != null ? Rules.Pattern(mark.pattern) : Current;
            how += pat != null && wants.TryGetValue(pat.id, out var wnt) ? $" Shaded: <color=#7fd18b>green</color> suits the {pat.Name.ToLowerInvariant()} ({wnt.min:0.#}-{wnt.max:0.#} mm), <color=#7fa6ff>blue</color> thinner, <color=#ffb070>amber</color> thicker." : " Shaded: lighter is thicker.";
        }
        info.text = (how + " " + said).Trim();
    }

    /// <summary>A scar, a brand or a hole: what the table rings in red.</summary>
    private static bool Scar(HideCell c) => c.flaw || c.damage > 0.3;

    /// <summary>The table rings in amber a cell the work damaged past this (rot, a burn) - and every cell a knife scored or the frame tore.</summary>
    private const double WorkMarkFrom = 0.02;

    /// <summary>The leather under the pattern: how thick (with calipers in mm), how tanned and dried, its scars and the work's marks - and whether that suits what the pattern makes.</summary>
    private string Under(CraftPiece piece, CraftPattern pat, int gx, int gy, int rot)
    {
        if (pat == null || piece == null)
            return "";
        HideState h = piece.hide;
        int[] marked = CraftLeather.WorkMarks(piece, WorkMarkFrom);
        pat.Size(rot, out int pw, out int ph);
        double sum = 0, lo = double.MaxValue, hi = 0, tan = 0, core = 1, hair = 0;
        int k = 0, scars = 0, scores = 0, tears = 0, spoilt = 0;
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                if (!pat.Covers(x, y, rot)) continue;
                HideCell c = h.Cell(gx + x, gy + y);
                if (c == null || !c.present) continue;
                double t = CraftLeather.CellThickness(c);
                sum += t; lo = Math.Min(lo, t); hi = Math.Max(hi, t);
                tan += c.Tannage;
                core = Math.Min(core, c.tanC);
                hair += c.hair;
                int by = marked[(gy + y) * h.width + gx + x];
                if (Scar(c)) scars++;
                else if (by == CraftLeather.ByKnife) scores++;
                else if (by == CraftLeather.ByFrame) tears++;
                else if (by == CraftLeather.ByOther) spoilt++;
                k++;
            }
        if (k == 0)
            return "";
        double mean = sum / k;
        string weight = mean < 1.5 ? "glove-thin" : mean < 2.5 ? "garment weight" : mean < 4 ? "belt weight" : mean < 6 ? "sole-thick" : "armour-thick";
        if (Station.HasInstrument(CraftInstruments.Calipers)) weight += $" ({mean:0.0} mm, {lo:0.0}-{hi:0.0})";
        bool cloth = Rules.Hide(h.hide)?.textile ?? false;
        string tanned = cloth ? (((rot % 4) + 4) % 4 % 2 == 0 ? "along the grain" : "<color=#ffb070>across the grain: it stretches and is weaker</color>")
            : tan / k < 0.3 ? "<color=#ff8a70>raw: tan it first</color>" : core < 0.4 ? "<color=#ffb070>the core is raw</color>" : "tanned through";
        string dry = cloth || h.dried ? "" : ", <color=#ffb070>not dried yet</color>";
        // the hair left on it, by the evaluator's own measure (the part would be marked down for it)
        string hairy = !cloth && CraftLeather.HairLeft(Rules.Hide(h.hide), hair / k) > CraftEvaluator.HairNoted ? ", <color=#ffb070>its hair still on</color>" : "";
        string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
        string flaws = (scars > 0 ? $", {Count(scars, "scar", "scars")}" : "")
                       + (scores > 0 ? $", <color=#ffb070>{Count(scores, "knife score", "knife scores")}</color>" : "")
                       + (tears > 0 ? $", <color=#ffb070>{Count(tears, "tear", "tears")}</color>" : "")
                       + (spoilt > 0 ? $", <color=#ffb070>{Count(spoilt, "damaged spot", "damaged spots")}</color>" : "");
        string suits = "";
        if (wants.TryGetValue(pat.id, out var wnt))
            suits = mean < wnt.min - 0.05 ? $" <color=#ffb070>Too thin for a {pat.Name.ToLowerInvariant()} (wants {wnt.min:0.#}-{wnt.max:0.#} mm).</color>"
                : mean > wnt.max + 0.05 ? $" <color=#ffb070>Too thick for a {pat.Name.ToLowerInvariant()} (wants {wnt.min:0.#}-{wnt.max:0.#} mm).</color>" : " The right weight for it.";
        return $"Under it: {weight}, {tanned}{dry}{hairy}{flaws}.{suits}";
    }
}
