using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lapidary's bench and the faceting machine, in ten places, from the rough to a cut stone:
/// <list type="bullet">
/// <item><b>Clean</b>: scrub the matrix and dirt off the rough (drag over the crust): the brush is slow but harmless, the
/// pick quick - worked on bare stone it scratches it, and a brittle stone handled roughly chips. Crust left on blurs
/// every test and hides what lies beneath it.</item>
/// <item><b>Test</b>: the gem tests, each with the hands. The loupe; a hardness point drawn lightly across the stone (does
/// it scratch?); the refractometer (hold the stone still on the prism while the shadow edge settles); the hydrostatic
/// balance (read it when the swinging needle comes to rest); the dichroscope (turn it slowly round the stone).</item>
/// <item><b>Identify</b>: compare the readings with the gem book (the tested values of every stone) and name it.</item>
/// <item><b>Evaluate</b>: search it with the loupe: move it over the stone and focus through its depth (the wheel) -
/// what lies in focus under the loupe shows. What you find is what the worth is judged by.</item>
/// <item><b>Orient</b>: place the finished stone in the rough (drag the circle; the wheel sizes it) clear of what you
/// found, choose the cut, and tilt the table to the crystal: across the axis for the best colour, off any cleavage.</item>
/// <item><b>Split</b>: draw the line, choose the technique (along the cleavage, or - a stone without one - nicked and
/// snapped), then strike on the timing bar: the indicator is the blow's force, 100% on the white mark parts it cleanly;
/// off it the face steps, far off it shatters.</item>
/// <item><b>Saw</b>: draw the line, then make each stroke of the trim saw on the timing bar: a narrow target gives
/// most, a wide one is safer and gives less, a miss jumps the blade (chips and lost weight).</item>
/// <item><b>Grind</b>: the stone turns against the wheel; hold to press it (higher in the panel presses harder) where
/// it stands outside the gold outline, and ease off before the wheel cuts into it. Steady pressure grinds a true
/// outline; ground freehand on the lap, the outline comes out lopsided.</item>
/// <item><b>Facet</b>: tier by tier: set the quill's angle on the protractor (drag round the dial or the wheel), then cut
/// each facet on the timing bar - accurate hits cut symmetrical facets at the angle, poor ones uneven facets, a miss
/// botches one (recut the tier).</item>
/// <item><b>Polish</b>: the compound on the lap; hold and move over the stone so every region comes up bright -
/// lightly (high on the stone) and not too fast, keep the heat gauge out of the red, and stop where it is bright (past
/// it the edges round). Polish never mends a bad cut.</item>
/// </list>
/// </summary>
public sealed class CraftLapidaryMode : CraftMode
{
    private enum Place { Clean, Test, Identify, Evaluate, Orient, Split, Saw, Grind, Facet, Polish }
    private static readonly string[] PlaceNames = { "Clean", "Test", "Identify", "Evaluate", "Orient", "Split", "Saw", "Grind", "Facet", "Polish" };
    private static readonly string[] TestNames = { "Loupe", "Hardness point", "Refractometer", "Balance (in water)", "Dichroscope" };
    private static readonly float[] Points = { 5f, 6f, 6.5f, 7f, 7.5f, 8f, 9f, 9.5f };

    private Place place = Place.Clean;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> cleanRows = new List<RectTransform>(), evalRows = new List<RectTransform>(), testRows = new List<RectTransform>(), idRows = new List<RectTransform>(),
        orientRows = new List<RectTransform>(), splitRows = new List<RectTransform>(), sawRows = new List<RectTransform>(), grindRows = new List<RectTransform>(),
        facetRows = new List<RectTransform>(), polishRows = new List<RectTransform>();
    private TMP_Dropdown test, point, gemName, cutName, technique, cleanTool, compound;
    private Slider tableTilt, speed, sawPressure;
    private readonly List<Button> dripButtons = new List<Button>();
    private TextMeshProUGUI info, book;
    private readonly List<string> gems = new List<string>(), cuts = new List<string>(), compounds = new List<string>();
    private string said = "";

    // The stone, from above.
    private static readonly Vector2 Centre = new Vector2(230f, 210f);
    private const float StoneRadius = 150f;
    private Vector2 ToPx(double x, double y) => new Vector2(Centre.x + (float)x * StoneRadius, Centre.y + (float)y * StoneRadius);
    private Vector2 ToPlan(Vector2 p) => (p - Centre) / StoneRadius;

    // Cleaning: the work put into each sector of the crust, and how controlled it was.
    private readonly float[] scrub = new float[CraftLapidary.CrustSectors];
    private float scrubTotal, controlSum, controlN, lastScrubTime;
    private Vector2 lastScrub;
    private bool scrubbing;

    // Testing.
    private bool holding;
    private float holdStart, moved, needle, needleV, sweep, lastAngle;
    private Vector2 lastPos;
    private readonly Rect instrument = new Rect(430, 60, 260, 300);

    // Evaluating: the loupe's focus and the looks taken.
    private float focus = 0.5f;
    private readonly List<float> looks = new List<float>();
    private Vector2 lastLook;
    private float lastLookTime, steadySum;
    private int steadyN;

    // Orienting.
    private Vector2 window;
    private float windowR = 0.6f;
    private bool draggingWindow;
    private string orientFor = "";

    // Splitting and sawing: the line, and the timing bar its blow or its strokes are judged on.
    private Vector2 lineA, lineB;
    private bool lining, lineSet, drip = true;
    private CraftTimingBar bar;
    private CraftTimingHit? lastHit;
    private readonly List<CraftTimingHit> hits = new List<CraftTimingHit>();
    private int sawStrokes;
    private string barFor = "";
    private static readonly Rect BarRect = new Rect(410, 330, 290, 22);

    // Grinding: the stone turning against the wheel.
    private float turn, pressure, grindTick;
    private bool pressing;
    private readonly List<float> touches = new List<float>(), pressures = new List<float>();
    private static readonly Vector2 Wheel = new Vector2(384f, 210f); // where the stone meets the wheel
    private const float WheelR = 70f;
    private static readonly Rect PressPanel = new Rect(540, 60, 160, 300);
    private const float TurnPerSecond = 80f;

    // Faceting: the quill's angle, then each facet cut on the timing bar.
    private float quill = 40f;
    private int tier = -1;
    private readonly Rect lap = new Rect(430, 40, 160, 130), indexWheel = new Rect(600, 50, 110, 110);
    // The protractor: its pivot and arc (0° straight down the mast - 90° flat).
    private static readonly Vector2 Pivot = new Vector2(450f, 180f);
    private const float Arc = 130f;
    private static float QuillAt(Vector2 pos) => Mathf.Clamp(Mathf.Atan2(pos.x - Pivot.x, pos.y - Pivot.y) * Mathf.Rad2Deg, 0f, 90f);
    private static bool OnDial(Vector2 pos) => Vector2.Distance(pos, Pivot) < Arc + 15f && pos.x >= Pivot.x - 10f && pos.y >= Pivot.y - 10f;
    private string facetFor = "";

    // Polishing.
    private float heat, pressSum, speedSum, polishHeld;
    private int passes;
    private readonly float[] regionTime = new float[CraftLapidary.PolishRegions];

    public override string Title => "Lapidary";
    public override CraftDiscipline Discipline => CraftDiscipline.Jewellery;
    public override bool Available(CraftStation s) => s.Has(CraftTools.GemBench) || s.Has(CraftTools.FacetingMachine);
    public override string Hint => "Clean the rough, test and name it, search it under the loupe, plan the cut, split or saw it, grind it to its outline, cut each tier of facets, polish - each with the hands.";

    private CraftPiece Stone => Selected != null && Selected.kind == CraftPieceKind.Gem ? Selected : null;

    private bool Here(Place p)
    {
        if (Station == null) return false;
        switch (p)
        {
            case Place.Facet: return Station.Has(CraftTools.FacetingMachine);
            case Place.Polish: return Station.Has(CraftTools.FacetingMachine) || Station.Has(CraftTools.Polishing);
            case Place.Grind: return Station.Has(CraftTools.GemBench) || Station.Has(CraftTools.FacetingMachine);
            case Place.Clean: case Place.Test: case Place.Identify: case Place.Evaluate: return Station.Has(CraftTools.GemBench) || Station.Has(CraftTools.JewelersBench);
            default: return Station.Has(CraftTools.GemBench);
        }
    }

    public override void Build(RectTransform controls)
    {
        cleanRows.Clear(); evalRows.Clear(); testRows.Clear(); idRows.Clear(); orientRows.Clear(); splitRows.Clear(); sawRows.Clear(); grindRows.Clear(); facetRows.Clear(); polishRows.Clear();
        dripButtons.Clear();
        RectTransform r0 = Row(controls, 30f, 3f);
        placeButtons.Clear();
        if (!Here(place))
            for (int i = 0; i < PlaceNames.Length; i++)
                if (Here((Place)i)) { place = (Place)i; break; }
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(r0, PlaceNames[i], () => { FlushAll(); place = pl; ResetHands(); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f);
            b.gameObject.SetActive(Here(pl));
            placeButtons.Add(b);
        }
        RectTransform k1 = Row(controls);
        Text(k1, "Tool", 13f, 36f);
        cleanTool = Drop(k1, new List<string> { "Stiff brush and water", "Steel pick" }, 0, 210f);
        RoutineButton(k1, CraftRoutine.CleanGem, "Clean by routine", () => Stone?.id, 170f);
        cleanRows.Add(k1);
        RectTransform e1 = Row(controls);
        RoutineButton(e1, CraftRoutine.SearchGem, "Search it by routine", () => Stone?.id, 190f);
        evalRows.Add(e1);

        RectTransform t1 = Row(controls);
        Text(t1, "Test", 13f, 36f);
        test = Drop(t1, TestNames, 0, 190f);
        test.onValueChanged.AddListener(_ => ResetHands());
        var pts = new List<string>();
        foreach (float f in Points) pts.Add($"Point {f:0.#}");
        point = Drop(t1, pts, 3, 110f);
        Btn(t1, "Look (loupe)", () => DoTest(CraftGemTest.Loupe, 1f), 110f);
        testRows.Add(t1);

        gems.Clear();
        var gn = new List<string>();
        foreach (CraftGemDefinition g in Rules.Gems) { gems.Add(g.id); gn.Add(g.Name); }
        RectTransform i1 = Row(controls);
        Text(i1, "It is", 13f, 36f);
        gemName = Drop(i1, gn.Count > 0 ? gn : new List<string> { "-" }, 0, 200f);
        Btn(i1, "Name it", () =>
        {
            CraftPiece p = Stone;
            if (p == null || gems.Count == 0) return;
            string id = gems[Mathf.Clamp(gemName.value, 0, gems.Count - 1)];
            if (Send(new CraftRequest(CraftRequestKind.IdentifyGem, p.id) { id = id })) said = $"Named it {Rules.Gem(id)?.Name.ToLowerInvariant()}.";
        }, 90f, UITheme.ButtonStyle.Primary);
        idRows.Add(i1);
        RectTransform i2 = UITheme.Row("Book", controls, 118f, 8f);
        book = UITheme.Label(i2, "", 11f, UITheme.Muted);
        InventoryUIFactory.Size(book, -1f, 118f, 1f);
        book.textWrappingMode = TextWrappingModes.Normal;
        book.overflowMode = TextOverflowModes.Truncate;
        idRows.Add(i2);

        cuts.Clear();
        var cn = new List<string>();
        foreach (CraftGemCut c in Rules.Cuts) { cuts.Add(c.id); cn.Add(c.Name); }
        RectTransform o1 = Row(controls);
        Text(o1, "Cut", 13f, 30f);
        cutName = Drop(o1, cn.Count > 0 ? cn : new List<string> { "-" }, 0, 180f);
        tableTilt = Slide(o1, "Table tilt", 0f, 90f, 0f, true, 66f);
        Btn(o1, "Mark it out", MarkOut, 110f, UITheme.ButtonStyle.Primary);
        orientRows.Add(o1);

        RectTransform s1 = Row(controls);
        Text(s1, "Technique", 13f, 70f);
        technique = Drop(s1, new List<string> { "Along the cleavage (chisel)", "Nick and snap" }, 0, 220f);
        technique.onValueChanged.AddListener(_ => { barFor = ""; });
        Btn(s1, "New line", () => { lineSet = lining = false; barFor = ""; }, 90f);
        splitRows.Add(s1);

        RectTransform w1 = Row(controls);
        dripButtons.Add(Btn(w1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        sawPressure = Slide(w1, "Feed pressure", 0f, 1f, 0.35f, false, 92f);
        sawPressure.onValueChanged.AddListener(_ => { barFor = ""; });
        Btn(w1, "New line", () => { lineSet = lining = false; barFor = ""; hits.Clear(); sawStrokes = 0; }, 90f);
        sawRows.Add(w1);

        RectTransform g1 = Row(controls);
        dripButtons.Add(Btn(g1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        grindRows.Add(g1);

        RectTransform f1 = Row(controls);
        Btn(f1, "Next tier", NextTier, 100f);
        Btn(f1, "Recut this tier", RecutTier, 130f);
        dripButtons.Add(Btn(f1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        facetRows.Add(f1);

        compounds.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Compound) compounds.Add(s.id);
        RectTransform p1 = Row(controls);
        Text(p1, "Compound", 13f, 70f);
        compound = CountedDrop(p1, compounds, 200f, "No compound");
        speed = Slide(p1, "Lap speed", 0f, 1f, 0.5f, false, 70f);
        polishRows.Add(p1);
        if (QuickBench)
        {
            RectTransform q = Row(controls);
            Btn(q, "Clean all", () =>
            {
                CraftPiece p = Stone;
                if (p == null) return;
                var all = new List<float>();
                for (int k = 0; k < CraftLapidary.CrustSectors; k++) all.Add(k < p.gem.crust.Count ? p.gem.crust[k] / 0.35f + 0.05f : 0f);
                Send(new CraftRequest(CraftRequestKind.CleanGem, p.id) { points = all, a = 1f });
            }, 80f);
            Btn(q, "All tests", () => { foreach (CraftGemTest t in Enum.GetValues(typeof(CraftGemTest))) DoTest(t, 1f); }, 80f);
            Btn(q, "Search all", () =>
            {
                CraftPiece p = Stone;
                if (p == null) return;
                var all = new List<float>();
                for (float z = 0.05f; z < 1f; z += 0.15f)
                    for (float y = -0.9f; y <= 0.9f; y += 0.2f)
                        for (float x = -0.9f; x <= 0.9f; x += 0.2f) { all.Add(x); all.Add(y); all.Add(z); }
                for (int k = 0; k < all.Count; k += 510)
                {
                    var part = all.GetRange(k, Math.Min(510, all.Count - k));
                    if (part.Count % 2 != 0) part.Add(0f);
                    Send(new CraftRequest(CraftRequestKind.EvaluateGem, p.id) { points = part, a = 1f });
                }
            }, 80f);
            Btn(q, "Grind ideal", () =>
            {
                CraftPiece p = Stone;
                if (p == null || !p.gem.oriented) return;
                for (int pass = 0; pass < 40 && CraftLapidary.Excess(p.gem) > 0.1; pass++)
                {
                    List<float> prof = p.gem.profile.Count == CraftLapidary.ProfileRadii ? p.gem.profile : CraftLapidary.Outline(p.gem);
                    var t = new List<float>();
                    for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                        if (prof[k] > CraftLapidary.TargetRadius(p.gem, k) + 0.04) { t.Add(k * 360f / CraftLapidary.ProfileRadii); t.Add(0.5f); }
                    if (t.Count == 0 || !Send(new CraftRequest(CraftRequestKind.GrindGem, p.id) { points = t, a = 1f, b = 1f })) break;
                }
            }, 90f);
            Btn(q, "Cut ideal", () =>
            {
                CraftPiece p = Stone;
                CraftGemCut cut = p != null ? Rules.Cut(p.gem.cut) : null;
                CraftGemDefinition d = p != null ? Rules.Gem(p.gem.gem) : null;
                if (cut == null || d == null) return;
                for (int t = 0; t < cut.tiers.Count; t++)
                    Send(new CraftRequest(CraftRequestKind.FacetGem, p.id) { i0 = t, count = cut.tiers[t].facets, a = (float)CraftLapidary.IdealAngle(cut.tiers[t], d.refractiveIndex), b = 0f, c = 0f, d = 1f });
            }, 80f);
        }
        info = InfoRow(controls, 44f);
        Show();
    }

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p) && Here(p)) { FlushAll(); place = p; ResetHands(); Show(); }
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in cleanRows) r.gameObject.SetActive(place == Place.Clean);
        foreach (RectTransform r in evalRows) r.gameObject.SetActive(place == Place.Evaluate);
        foreach (RectTransform r in testRows) r.gameObject.SetActive(place == Place.Test);
        foreach (RectTransform r in idRows) r.gameObject.SetActive(place == Place.Identify);
        foreach (RectTransform r in orientRows) r.gameObject.SetActive(place == Place.Orient);
        foreach (RectTransform r in splitRows) r.gameObject.SetActive(place == Place.Split);
        foreach (RectTransform r in sawRows) r.gameObject.SetActive(place == Place.Saw);
        foreach (RectTransform r in grindRows) r.gameObject.SetActive(place == Place.Grind);
        foreach (RectTransform r in facetRows) r.gameObject.SetActive(place == Place.Facet);
        foreach (RectTransform r in polishRows) r.gameObject.SetActive(place == Place.Polish);
        foreach (Button b in dripButtons)
        {
            TMP_Text t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = drip ? "Drip on" : "Drip off";
            if (b != null) UITheme.SetSelected(b, drip);
        }
    }

    private void ResetHands()
    {
        holding = scrubbing = pressing = lining = draggingWindow = false;
        lineSet = false;
        barFor = "";
        hits.Clear();
        sawStrokes = 0;
        lastHit = null;
    }

    private void FlushAll()
    {
        FlushScrub();
        FlushLooks();
        FlushGrind();
        FlushPolish();
    }

    public override void Leave()
    {
        FlushAll();
        ResetHands();
    }

    public override void Tick(float dt)
    {
        // The balance's needle swings and settles; the lap's heat rises while pressed and falls off.
        needleV += (-needle * 9f - needleV * 0.9f) * dt;
        needle += needleV * dt;
        if ((place == Place.Split || place == Place.Saw || place == Place.Facet) && bar != null) bar.Tick(dt);
        if (place == Place.Clean && scrubbing && Time.unscaledTime - lastScrubTime > 0.6f) FlushScrub();
        if (place == Place.Grind && Stone != null)
        {
            turn = (turn + dt * TurnPerSecond) % 360f;
            if (pressing && ui.PointerHeld)
            {
                pressure = Mathf.Clamp01((ui.Hover.y - PressPanel.yMin) / PressPanel.height);
                grindTick += dt;
                while (grindTick >= 0.05f)
                {
                    grindTick -= 0.05f;
                    // the part of the stone at the wheel (the stone's right side, screen angle 0)
                    float at = ((0f - turn) % 360f + 360f) % 360f + (float)Hands.Tremble(touches.Count + 231) * 120f;
                    touches.Add((at % 360f + 360f) % 360f);
                    touches.Add(pressure * 0.5f);
                    pressures.Add(pressure);
                    heat = Mathf.Min(1.2f, heat + 0.05f * pressure * (drip ? 0.15f : 1f));
                }
                if (touches.Count >= 20) FlushGrind();
            }
            else heat = Mathf.Max(0f, heat - dt * 0.4f);
        }
        if (place == Place.Polish)
        {
            if (holding && ui.PointerHeld)
            {
                Vector2 h = ui.Hover;
                float press = Mathf.Clamp01((Centre.y + StoneRadius - h.y) / (2f * StoneRadius));
                heat = Mathf.Min(1.2f, heat + dt * press * speed.value * 1.1f);
                polishHeld += dt;
                pressSum += press * dt;
                speedSum += speed.value * dt;
                int k = RegionAt(h);
                if (k >= 0) regionTime[k] += dt;
                while (polishHeld >= 0.5f) { polishHeld -= 0.5f; passes++; }
                if (passes >= 4) FlushPolish();
            }
            else heat = Mathf.Max(0f, heat - dt * 0.4f);
        }
    }

    // ------------------------------------------------------------------ the stone, from above
    private void DrawStone(CraftPixels c, CraftPiece p, bool showFound, bool showAll)
    {
        GemState g = p.gem;
        CraftGemDefinition d = Rules.Gem(g.gem);
        Color col = d != null ? d.color : new Color(0.8f, 0.8f, 0.85f);
        col.a = 1f;
        var pts = new List<Vector2>();
        for (int i = 0; i < 48; i++)
        {
            double a = i / 48.0 * Math.PI * 2, x = Math.Cos(a), y = Math.Sin(a);
            foreach (CraftGemPlane b in g.bounds)
            {
                double over = b.nx * x + b.ny * y - b.offset;
                if (over > 0) { x -= b.nx * over; y -= b.ny * over; }
            }
            pts.Add(ToPx(x, y));
        }
        float tone = 0.55f + 0.45f * (float)g.tone;
        c.Polygon(pts, Color.Lerp(col * tone, new Color(0.5f, 0.5f, 0.5f), d != null && d.opaque ? 0.1f : 0.25f));
        c.Outline(pts, col * 0.6f, 2f);
        if (g.stage >= CraftGemStage.Faceted)
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Line(Centre.x, Centre.y, Centre.x + Mathf.Cos(a) * StoneRadius * 0.95f, Centre.y + Mathf.Sin(a) * StoneRadius * 0.95f, new Color(1f, 1f, 1f, 0.15f + 0.3f * (float)g.polish), 1f);
            }
        // The cleavage's trace, once looked at under the loupe.
        if ((g.looked || showAll) && d != null && d.cleavage >= CraftCleavage.Good)
        {
            float a = (float)g.cleaveTrace * Mathf.Deg2Rad;
            for (int k = -2; k <= 2; k++)
            {
                Vector2 n = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * k * 22f;
                c.Line(Centre.x + n.x - Mathf.Cos(a) * StoneRadius * 0.8f, Centre.y + n.y - Mathf.Sin(a) * StoneRadius * 0.8f, Centre.x + n.x + Mathf.Cos(a) * StoneRadius * 0.8f, Centre.y + n.y + Mathf.Sin(a) * StoneRadius * 0.8f, new Color(1f, 1f, 1f, 0.18f), 1f);
            }
        }
        foreach (CraftInclusion i in g.inclusions)
        {
            if (!(showAll || showFound && i.found)) continue;
            Vector2 at = ToPx(i.x, i.y);
            float r = Mathf.Max(2f, i.size * StoneRadius);
            switch (i.kind)
            {
                case CraftInclusionKind.Feather: c.Line(at.x - r, at.y - r * 0.4f, at.x + r, at.y + r * 0.4f, new Color(1f, 1f, 1f, 0.8f), 1.5f); break;
                case CraftInclusionKind.Cloud: c.Disc(at.x, at.y, r, new Color(1f, 1f, 1f, 0.35f)); break;
                case CraftInclusionKind.Needle: c.Line(at.x - r, at.y + r, at.x + r, at.y - r, new Color(0.95f, 0.9f, 0.7f, 0.8f), 1f); break;
                default: c.Disc(at.x, at.y, r * 0.6f, new Color(0.1f, 0.1f, 0.1f, 0.9f)); break;
            }
            if (i.found) c.Ring(at.x, at.y, r + 3f, 1f, new Color(1f, 0.35f, 0.3f, 0.8f));
        }
        if (place != Place.Clean) DrawCrust(c, g, null);
        if (g.chips > 0.05) c.Disc(pts[7].x, pts[7].y, 4f + (float)g.chips * 10f, Back);
    }

    /// <summary>The matrix and dirt still on the rough: a brown crust round its skin, thicker where there is more.</summary>
    private void DrawCrust(CraftPixels c, GemState g, float[] pending)
    {
        if (g.crust == null || g.crust.Count == 0) return;
        int n = g.crust.Count;
        for (int k = 0; k < n; k++)
        {
            float amount = g.crust[k] - (pending != null && k < pending.Length ? pending[k] * 0.35f * (float)Station.ToolQuality : 0f);
            if (amount <= 0.02f) continue;
            float a0 = k * Mathf.PI * 2f / n, a1 = (k + 1) * Mathf.PI * 2f / n, inner = 1f - 0.45f * Mathf.Clamp01(amount);
            var wedge = new List<Vector2>();
            for (int s = 0; s <= 4; s++) { float a = Mathf.Lerp(a0, a1, s / 4f); wedge.Add(Clip(g, Mathf.Cos(a), Mathf.Sin(a), 1f)); }
            for (int s = 4; s >= 0; s--) { float a = Mathf.Lerp(a0, a1, s / 4f); wedge.Add(Clip(g, Mathf.Cos(a), Mathf.Sin(a), inner)); }
            c.Polygon(wedge, new Color(0.38f, 0.28f, 0.18f, 0.35f + 0.6f * Mathf.Clamp01(amount)));
        }
    }

    /// <summary>A point at <paramref name="r"/> along a direction of the plan, pulled inside any sawn face.</summary>
    private Vector2 Clip(GemState g, double x, double y, double r)
    {
        x *= r; y *= r;
        foreach (CraftGemPlane b in g.bounds)
        {
            double over = b.nx * x + b.ny * y - b.offset;
            if (over > 0) { x -= b.nx * over; y -= b.ny * over; }
        }
        return ToPx(x, y);
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Stone;
        c.Rect(Centre.x - StoneRadius - 20, Centre.y - StoneRadius - 20, Centre.x + StoneRadius + 20, Centre.y + StoneRadius + 20, new Color(0.14f, 0.13f, 0.12f));
        if (p == null) return;
        switch (place)
        {
            case Place.Clean: DrawClean(c, p); break;
            case Place.Test: DrawStone(c, p, true, Station.Reveals); DrawInstrument(c, p); break;
            case Place.Identify: DrawStone(c, p, true, Station.Reveals); break;
            case Place.Evaluate: DrawEvaluate(c, p); break;
            case Place.Orient: DrawOrient(c, p); break;
            case Place.Split: case Place.Saw: DrawCut(c, p); break;
            case Place.Grind: DrawGrind(c, p); break;
            case Place.Facet: DrawFacet(c, p); break;
            default: DrawPolish(c, p); break;
        }
    }

    // ------------------------------------------------------------------ cleaning
    private bool Pick => cleanTool != null && cleanTool.value == 1;

    private void DrawClean(CraftPixels c, CraftPiece p)
    {
        DrawStone(c, p, true, Station.Reveals);
        DrawCrust(c, p.gem, scrub);
        // The tool under the hand while scrubbing.
        Vector2 h = ui.Hover;
        if (h.x >= 0 && Vector2.Distance(h, Centre) < StoneRadius + 10f)
        {
            if (Pick) c.Line(h.x, h.y, h.x + 26, h.y + 40, new Color(0.75f, 0.76f, 0.8f), 2f);
            else { c.Rect(h.x - 14, h.y - 4, h.x + 14, h.y + 4, Wood); for (int k = -12; k <= 12; k += 4) c.Line(h.x + k, h.y - 4, h.x + k, h.y - 10, new Color(0.85f, 0.8f, 0.6f), 1f); }
        }
        // How clean it is, and how steady the hand has been.
        double dirt = CraftLapidary.Dirt(p.gem);
        c.Rect(430, 60, 690, 300, new Color(0.2f, 0.19f, 0.18f));
        Meter(c, 440, 270, 240, 10, 1f - (float)dirt, new Color(0.6f, 0.85f, 1f));
        Meter(c, 440, 250, 240, 6, controlN > 0 ? controlSum / controlN : 1f, new Color(0.45f, 0.9f, 0.45f));
        for (int k = 0; k < p.gem.crust.Count; k++)
        {
            float x0 = 440 + k * 15f;
            Meter(c, x0, 80, 12, 150, Mathf.Clamp01(p.gem.crust[k]), new Color(0.55f, 0.4f, 0.25f));
        }
    }

    private void CleanPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (p == null) return;
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) < StoneRadius + 10f) { scrubbing = true; lastScrub = pos; lastScrubTime = Time.unscaledTime; return; }
        if (phase == CraftPointer.Up) { FlushScrub(); scrubbing = false; return; }
        if (!scrubbing || phase != CraftPointer.Drag) return;
        float now = Time.unscaledTime, dist = Vector2.Distance(pos, lastScrub);
        if (dist < 2f) return;
        // Work goes into the sector of the outline under the tool; a frantic hand is not a controlled one.
        Vector2 plan = ToPlan(pos + new Vector2((float)Hands.Tremble(controlN > 0 ? (int)controlN + 241 : 241) * 40f, 0f));
        float a = Mathf.Atan2(plan.y, plan.x);
        if (a < 0) a += Mathf.PI * 2f;
        int k = Mathf.FloorToInt(a / (Mathf.PI * 2f) * scrub.Length) % scrub.Length;
        float work = dist / (StoneRadius * (Pick ? 2.2f : 1.4f));
        scrub[k] += work;
        scrubTotal += work;
        float speedPx = dist / Mathf.Max(0.016f, now - lastScrubTime);
        controlSum += Mathf.Clamp01(1.2f - speedPx / 1400f);
        controlN++;
        lastScrub = pos;
        lastScrubTime = now;
        Hands.Move(0.03);
        if (scrubTotal > 1.5f) FlushScrub();
    }

    private void FlushScrub()
    {
        CraftPiece p = Stone;
        if (p == null || scrubTotal <= 0f) { Array.Clear(scrub, 0, scrub.Length); scrubTotal = 0f; return; }
        var work = new List<float>(scrub);
        float control = controlN > 0 ? controlSum / controlN : 1f;
        double before = p.gem.scratches;
        if (Send(new CraftRequest(CraftRequestKind.CleanGem, p.id) { points = work, a = control, i0 = Pick ? 1 : 0 }))
            said = p.gem.scratches > before + 0.01 ? "The pick is marking the bare stone: ease off there." : CraftLapidary.Dirt(p.gem) < 0.05 ? "Clean to the bare stone." : "Scrubbing.";
        Array.Clear(scrub, 0, scrub.Length);
        scrubTotal = 0f;
        controlSum = controlN = 0f;
    }

    // ------------------------------------------------------------------ testing
    private CraftGemTest CurrentTest => (CraftGemTest)Mathf.Clamp(test != null ? test.value : 0, 0, TestNames.Length - 1);

    private void DrawInstrument(CraftPixels c, CraftPiece p)
    {
        c.Rect(instrument.xMin, instrument.yMin, instrument.xMax, instrument.yMax, new Color(0.2f, 0.19f, 0.18f));
        switch (CurrentTest)
        {
            case CraftGemTest.Scratch:
                // The point held to the stone: drag it lightly across.
                if (holding) c.Line(lastPos.x - 30, lastPos.y + 40, lastPos.x, lastPos.y, new Color(0.75f, 0.76f, 0.8f), 3f);
                break;
            case CraftGemTest.Refractometer:
            {
                // The prism with its scale; the shadow edge sharpens while the stone is held still.
                c.Rect(instrument.xMin + 40, instrument.yMin + 60, instrument.xMax - 40, instrument.yMax - 40, new Color(0.85f, 0.88f, 0.9f));
                float still = holding ? Mathf.Clamp01((Time.unscaledTime - holdStart) / 2f) * Mathf.Clamp01(1f - moved / 60f) : 0f;
                float edge = instrument.yMin + 60 + (instrument.height - 100) * 0.5f;
                c.Rect(instrument.xMin + 40, instrument.yMin + 60, instrument.xMax - 40, edge - (1f - still) * 30f, new Color(0.25f, 0.27f, 0.3f));
                for (int k = 0; k < 10; k++) c.Line(instrument.xMin + 40, instrument.yMin + 70 + k * 20, instrument.xMin + 60, instrument.yMin + 70 + k * 20, Iron, 1f);
                Meter(c, instrument.xMin, instrument.yMin - 14, instrument.width, 6, still, new Color(0.45f, 0.9f, 0.45f));
                break;
            }
            case CraftGemTest.Gravity:
            {
                // The balance's needle, swinging until it settles.
                Vector2 pivot = new Vector2(instrument.center.x, instrument.yMin + 60);
                float a = Mathf.Clamp(needle, -1f, 1f) * 40f * Mathf.Deg2Rad;
                c.Line(pivot.x, pivot.y, pivot.x + Mathf.Sin(a) * 200f, pivot.y + Mathf.Cos(a) * 200f, Iron, 3f);
                c.Rect(pivot.x - 2, pivot.y + 200, pivot.x + 2, pivot.y + 230, Mark);
                c.Disc(pivot.x, pivot.y, 6f, Iron);
                break;
            }
            case CraftGemTest.Dichroscope:
            {
                // The dichroscope's two windows: the colours differ as it is turned (for a pleochroic stone).
                CraftGemDefinition d = Rules.Gem(p.gem.gem);
                Color col = d != null ? d.color : Color.white;
                float pleo = d != null ? d.pleochroism : 0f;
                float turn = lastAngle;
                Color a = Color.Lerp(col, Color.Lerp(col, new Color(col.b, col.r, col.g), pleo), 0.5f + 0.5f * Mathf.Sin(turn * 2f));
                Color b = Color.Lerp(col, Color.Lerp(col, new Color(col.g, col.b, col.r), pleo), 0.5f - 0.5f * Mathf.Sin(turn * 2f));
                c.Ring(instrument.center.x, instrument.center.y, 100f, 10f, Iron);
                c.Rect(instrument.center.x - 60, instrument.center.y - 30, instrument.center.x - 4, instrument.center.y + 30, a);
                c.Rect(instrument.center.x + 4, instrument.center.y - 30, instrument.center.x + 60, instrument.center.y + 30, b);
                Meter(c, instrument.xMin, instrument.yMin - 14, instrument.width, 6, Mathf.Clamp01(sweep / 180f), new Color(0.6f, 0.85f, 1f));
                break;
            }
        }
    }

    private void DoTest(CraftGemTest t, float care)
    {
        CraftPiece p = Stone;
        if (p == null) { said = "Pick a stone (left)."; return; }
        if (!Station.HasInstrument(CraftStation.InstrumentFor(t))) { said = $"There is no {CraftStation.InstrumentFor(t).ToString().ToLowerInvariant()} here."; return; }
        float pt = Points[Mathf.Clamp(point.value, 0, Points.Length - 1)];
        if (Send(new CraftRequest(CraftRequestKind.TestGem, p.id) { i0 = (int)t, a = care, b = pt }))
            said = care < 0.5f ? "A hurried reading: it may be off." : "Read.";
    }

    private void TestPointer(CraftPointer phase, Vector2 pos)
    {
        CraftGemTest t = CurrentTest;
        if (t == CraftGemTest.Loupe) { if (phase == CraftPointer.Down) DoTest(t, 1f); return; }
        if (t == CraftGemTest.Gravity)
        {
            // Weigh: a click sets the needle swinging; the next, when it is still, reads it.
            if (phase != CraftPointer.Down || !instrument.Contains(pos)) return;
            if (!holding) { holding = true; needle = 0.9f; needleV = 0f; said = "The needle swings: read it when it rests."; return; }
            holding = false;
            float care = Mathf.Clamp01(1f - (Mathf.Abs(needle) + Mathf.Abs(needleV) * 0.3f) * 2f);
            DoTest(t, care);
            return;
        }
        Vector2 at = pos + new Vector2((float)Hands.Tremble(moved > 0 ? 191 : 192) * 40f, (float)Hands.Tremble(193) * 40f);
        if (phase == CraftPointer.Down)
        {
            bool onStone = Vector2.Distance(pos, Centre) < StoneRadius;
            if (t == CraftGemTest.Scratch && !onStone) return;
            if ((t == CraftGemTest.Refractometer || t == CraftGemTest.Dichroscope) && !instrument.Contains(pos) && !onStone) return;
            holding = true;
            holdStart = Time.unscaledTime;
            moved = sweep = 0f;
            lastPos = at;
            lastAngle = Mathf.Atan2(pos.y - instrument.center.y, pos.x - instrument.center.x);
            return;
        }
        if (!holding) return;
        if (phase == CraftPointer.Drag)
        {
            moved += Vector2.Distance(at, lastPos);
            if (t == CraftGemTest.Dichroscope)
            {
                float a = Mathf.Atan2(pos.y - instrument.center.y, pos.x - instrument.center.x);
                sweep += Mathf.Abs(Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg));
                lastAngle = a;
            }
            lastPos = at;
            return;
        }
        if (phase != CraftPointer.Up) return;
        holding = false;
        float held = Time.unscaledTime - holdStart;
        switch (t)
        {
            case CraftGemTest.Scratch:
                // A short, light stroke: dragged hard and long it gouges the stone.
                if (moved < 8f) { said = "Draw the point across the stone."; return; }
                DoTest(t, Mathf.Clamp01(1f - Mathf.Max(0f, moved - 40f) / 120f));
                break;
            case CraftGemTest.Refractometer:
                if (held < 0.6f) { said = "Hold the stone still on the prism until the edge settles."; return; }
                DoTest(t, Mathf.Clamp01(held / 2f) * Mathf.Clamp01(1f - moved / 60f));
                break;
            case CraftGemTest.Dichroscope:
                if (sweep < 45f) { said = "Turn the dichroscope round the stone (drag round the ring)."; return; }
                DoTest(t, Mathf.Clamp01(sweep / 180f) * Mathf.Clamp01(1f - Mathf.Max(0f, sweep / Mathf.Max(0.1f, held) - 200f) / 300f));
                break;
        }
    }

    // ------------------------------------------------------------------ evaluating
    private void DrawEvaluate(CraftPixels c, CraftPiece p)
    {
        DrawStone(c, p, true, Station.Reveals);
        Vector2 h = ui.Hover;
        float lr = 34f * Mathf.Min(1.3f, Station.ToolQuality);
        if (h.x >= 0 && Vector2.Distance(h, Centre) < StoneRadius + 10f)
        {
            // Under the loupe: what lies at the depth in focus shows sharp, near it a blur.
            foreach (CraftInclusion i in p.gem.inclusions)
            {
                Vector2 at = ToPx(i.x, i.y);
                if (Vector2.Distance(at, h) > lr) continue;
                float off = Mathf.Abs(i.depth - focus);
                if (off < 0.12f + i.size) c.Disc(at.x, at.y, Mathf.Max(2f, i.size * StoneRadius * 0.8f), i.kind == CraftInclusionKind.Feather ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.08f, 0.08f, 0.08f, 0.9f));
                else if (off < 0.3f) c.Disc(at.x, at.y, Mathf.Max(3f, i.size * StoneRadius * 1.6f), new Color(1f, 1f, 1f, 0.15f));
            }
            c.Ring(h.x, h.y, lr, 3f, Iron);
        }
        // The focus through the stone's depth (the wheel), and how much has been searched.
        Meter(c, 430, 360, 260, 8, focus, new Color(0.6f, 0.85f, 1f));
        Meter(c, 430, 346, 260, 6, Mathf.Clamp01(looks.Count / 3f / 300f), Mark);
    }

    private void EvaluatePointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Stone;
        if (phase == CraftPointer.Scroll) { focus = Mathf.Clamp01(focus + (scroll > 0 ? 0.05f : -0.05f)); return; }
        if (p == null) return;
        if (phase == CraftPointer.Up) { FlushLooks(); return; }
        if (phase != CraftPointer.Down && phase != CraftPointer.Drag && phase != CraftPointer.Move) return;
        if (Vector2.Distance(pos, Centre) > StoneRadius) return;
        float now = Time.unscaledTime;
        if (Vector2.Distance(pos, lastLook) < 10f && now - lastLookTime < 0.25f) return;
        // A look: where the loupe was, at the focus, as steady as the hand held it.
        float speed = Vector2.Distance(pos, lastLook) / Mathf.Max(0.016f, now - lastLookTime);
        Vector2 plan = ToPlan(pos + new Vector2((float)Hands.Tremble(looks.Count + 201) * 40f, (float)Hands.Tremble(looks.Count + 202) * 40f));
        looks.Add(plan.x); looks.Add(plan.y); looks.Add(focus);
        steadySum += Mathf.Clamp01(1f - speed / 900f);
        steadyN++;
        lastLook = pos;
        lastLookTime = now;
        if (looks.Count >= 510) FlushLooks();
    }

    private void FlushLooks()
    {
        CraftPiece p = Stone;
        if (p == null || looks.Count < 3) { looks.Clear(); steadySum = 0f; steadyN = 0; return; }
        var pts = new List<float>(looks);
        if (pts.Count % 2 != 0) pts.Add(0f);
        if (Send(new CraftRequest(CraftRequestKind.EvaluateGem, p.id) { points = pts, a = steadyN > 0 ? steadySum / steadyN : 0.5f }))
            said = "Searched.";
        looks.Clear();
        steadySum = 0f;
        steadyN = 0;
    }

    // ------------------------------------------------------------------ orienting
    private void DrawOrient(CraftPixels c, CraftPiece p)
    {
        if (orientFor != p.id)
        {
            orientFor = p.id;
            window = p.gem.oriented ? new Vector2((float)p.gem.windowX, (float)p.gem.windowY) : Vector2.zero;
            windowR = p.gem.oriented ? (float)p.gem.windowR : 0.6f;
            if (p.gem.oriented) tableTilt.SetValueWithoutNotify((float)p.gem.tableAngle);
        }
        DrawStone(c, p, true, Station.Reveals);
        Vector2 w = ToPx(window.x, window.y);
        c.Ring(w.x, w.y, windowR * StoneRadius, 2f, Mark);
        // The table's tilt against what the dichroscope showed of the axis (and the cleavage, if it was seen).
        Rect gauge = new Rect(430, 60, 260, 120);
        c.Rect(gauge.xMin, gauge.yMin, gauge.xMax, gauge.yMax, new Color(0.2f, 0.19f, 0.18f));
        float tilt = tableTilt != null ? tableTilt.value : 0f;
        c.Line(gauge.xMin + 20, gauge.center.y, gauge.xMin + 20 + 200 * Mathf.Cos(tilt * Mathf.Deg2Rad), gauge.center.y + 50 * Mathf.Sin(tilt * Mathf.Deg2Rad), Mark, 3f);
        if (p.gem.readAxis >= 0)
        {
            float a = (float)p.gem.readAxis * Mathf.Deg2Rad;
            c.Line(gauge.xMin + 20, gauge.center.y, gauge.xMin + 20 + 200 * Mathf.Cos(a), gauge.center.y + 50 * Mathf.Sin(a), new Color(0.6f, 0.85f, 1f), 1.5f);
        }
        CraftGemDefinition d = Rules.Gem(p.gem.gem);
        if (p.gem.looked && d != null && d.cleavage >= CraftCleavage.Good)
        {
            float a = (float)p.gem.cleaveTilt * Mathf.Deg2Rad;
            c.Line(gauge.xMin + 20, gauge.center.y, gauge.xMin + 20 + 200 * Mathf.Cos(a), gauge.center.y + 50 * Mathf.Sin(a), new Color(1f, 0.4f, 0.3f), 1.5f);
        }
        // What the cut would give, judged on what was found.
        CraftGemGrade g = Preview(p);
        if (g != null) Meter(c, 430, 200, 260, 8, Mathf.Clamp01((float)g.Quality), new Color(0.45f, 0.9f, 0.45f));
    }

    private CraftGemGrade Preview(CraftPiece p)
    {
        if (cuts.Count == 0) return null;
        CraftPiece copy = p.Clone();
        copy.gem.oriented = true;
        copy.gem.cut = cuts[Mathf.Clamp(cutName.value, 0, cuts.Count - 1)];
        copy.gem.windowX = window.x;
        copy.gem.windowY = window.y;
        copy.gem.windowR = windowR;
        copy.gem.tableAngle = tableTilt.value;
        return CraftLapidary.Grade(Rules, copy, !Station.Reveals);
    }

    private void OrientPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        if (phase == CraftPointer.Scroll) { windowR = Mathf.Clamp(windowR + (scroll > 0 ? 0.03f : -0.03f), 0.15f, 1f); return; }
        if (phase == CraftPointer.Down && Vector2.Distance(pos, ToPx(window.x, window.y)) < windowR * StoneRadius) { draggingWindow = true; return; }
        if (phase == CraftPointer.Drag && draggingWindow) { window = ToPlan(pos); if (window.magnitude > 0.9f) window = window.normalized * 0.9f; return; }
        if (phase == CraftPointer.Up) draggingWindow = false;
    }

    private void MarkOut()
    {
        CraftPiece p = Stone;
        if (p == null || cuts.Count == 0) return;
        string cut = cuts[Mathf.Clamp(cutName.value, 0, cuts.Count - 1)];
        if (Send(new CraftRequest(CraftRequestKind.OrientGem, p.id) { id = cut, a = tableTilt.value, b = window.x, c = window.y, d = windowR }))
            said = $"Marked out for {CraftText.A(Rules.Cut(cut)?.Name ?? "cut")} {Rules.Cut(cut)?.Name.ToLowerInvariant()}.";
    }

    // ------------------------------------------------------------------ splitting and sawing
    private bool Snap => technique != null && technique.value == 1;
    private CraftGemDefinition GemOf(CraftPiece p) => p != null ? Rules.Gem(p.gem.gem) : null;
    private int SawStrokesNeeded => Mathf.RoundToInt(Mathf.Lerp(8f, 4f, sawPressure != null ? sawPressure.value : 0.35f));

    /// <summary>The timing bar for what is being done now (rebuilt when the stone, the place or the settings change).</summary>
    private CraftTimingBar BarFor(CraftPiece p)
    {
        string key = $"{p.id}:{place}:{(Snap ? 1 : 0)}:{(sawPressure != null ? sawPressure.value : 0f):0.00}:{tier}";
        if (bar != null && barFor == key) return bar;
        barFor = key;
        CraftGemDefinition d = GemOf(p);
        if (place == Place.Split)
            // the very zones the blow is judged on
            bar = CraftLapidary.SplitBar(Rules, d, Snap);
        else
        {
            bar = new CraftTimingBar(Rules.Settings.timing) { difficulty = (float)CraftLapidary.Difficulty(d), skill = (float)Hands.skill };
            if (place == Place.Saw)
            {
                // pressing the feed harder: fewer strokes, but a faster, less forgiving blade
                bar.difficulty *= 1f + 0.4f * (sawPressure != null ? sawPressure.value : 0.35f);
                bar.NewTargets(p.gem.seed, 300 + sawStrokes, true);
            }
            else bar.NewTargets(p.gem.seed, 500 + tier * 16 + hits.Count);
        }
        return bar;
    }

    private void DrawCut(CraftPixels c, CraftPiece p)
    {
        DrawStone(c, p, true, Station.Reveals);
        if (p.gem.oriented)
        {
            Vector2 w = ToPx(p.gem.windowX, p.gem.windowY);
            c.Ring(w.x, w.y, (float)CraftLapidary.WindowRadius(p.gem) * StoneRadius, 1.5f, new Color(1f, 0.8f, 0.38f, 0.6f));
        }
        if (lining || lineSet)
        {
            c.Line(lineA.x, lineA.y, lineB.x, lineB.y, lineSet ? Mark : new Color(1f, 1f, 1f, 0.6f), lineSet ? 2f : 1f);
            if (place == Place.Saw && lineSet)
            {
                // the blade as far along the line as the strokes have taken it
                Vector2 at = Vector2.Lerp(lineA, lineB, Mathf.Clamp01(sawStrokes / (float)SawStrokesNeeded));
                c.Disc(at.x, at.y, 8f, new Color(0.75f, 0.76f, 0.8f));
                c.Line(lineA.x, lineA.y, at.x, at.y, new Color(0.15f, 0.15f, 0.15f), 3f);
                Meter(c, BarRect.xMin, BarRect.yMax + 26, BarRect.width, 6, sawStrokes / (float)SawStrokesNeeded, new Color(0.6f, 0.85f, 1f));
            }
        }
        if (lineSet)
        {
            DrawTimingBar(c, BarFor(p), BarRect.xMin, BarRect.yMin, BarRect.width, BarRect.height, lastHit);
            // each stroke so far, as it was judged
            for (int k = 0; k < hits.Count; k++)
                c.Rect(BarRect.xMin + k * 18f, BarRect.yMin - 30, BarRect.xMin + k * 18f + 14f, BarRect.yMin - 16, GradeColor(hits[k].grade));
            if (place == Place.Split)
            {
                // the force scale under the bar: light to hard
                c.Line(BarRect.xMin, BarRect.yMin - 6, BarRect.xMax, BarRect.yMin - 6, new Color(1f, 1f, 1f, 0.3f), 1f);
            }
        }
        if (!drip && place == Place.Saw) c.Halo(Centre.x, Centre.y, StoneRadius * 0.5f, new Color(1f, 0.5f, 0.2f), lineSet ? 0.25f : 0f);
    }

    /// <summary>The line in the plan: its direction (°) and its signed distance from the centre.</summary>
    private void LineOf(out float angle, out float offset)
    {
        Vector2 a = ToPlan(lineA), b = ToPlan(lineB), d = (b - a).normalized;
        angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float r = angle * Mathf.Deg2Rad;
        offset = Mathf.Sin(r) * a.x - Mathf.Cos(r) * a.y;
    }

    private void CutPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (p == null) return;
        if (!lineSet)
        {
            if (phase == CraftPointer.Down) { lining = true; lineA = lineB = pos; return; }
            if (!lining) return;
            if (phase == CraftPointer.Drag) { lineB = pos; return; }
            if (phase != CraftPointer.Up) return;
            lining = false;
            if (Vector2.Distance(lineA, lineB) < StoneRadius * 0.8f) { said = "Draw the line right across the stone."; return; }
            lineSet = true;
            hits.Clear();
            sawStrokes = 0;
            lastHit = null;
            barFor = "";
            said = place == Place.Split
                ? (Snap ? "Nicked along the line. Strike when the marker is on the white line: that is the blow that snaps it." : "The chisel is set on the line. Strike when the marker is on the white line: the force that parts it.")
                : $"Now saw: click for each stroke when the marker is on a target - {SawStrokesNeeded} strokes. The narrow target gives most, the wide one is safer.";
            return;
        }
        if (phase != CraftPointer.Down) return;
        CraftTimingBar b = BarFor(p);
        CraftTimingHit hit = b.Hit(Hands);
        lastHit = hit;
        if (place == Place.Split)
        {
            LineOf(out float angle, out float offset);
            if (Send(new CraftRequest(CraftRequestKind.CleaveGem, p.id) { a = angle, b = offset, c = Mathf.Clamp01(hit.position), i0 = Snap ? 1 : 0 }))
                said = hit.position < 0.3f ? "Too light: it rang, but did not part." : HitWords(hit, hit.position > b.targets[0].ideal ? "too hard, it shattered" : "too weak, it crushed");
            lineSet = false;
            return;
        }
        // a stroke of the saw
        hits.Add(hit);
        sawStrokes++;
        said = $"Stroke {sawStrokes}/{SawStrokesNeeded}: {HitWords(hit, "the blade jumped")}";
        b.NewTargets(p.gem.seed, 300 + sawStrokes, true);
        Hands.Move(0.08);
        if (sawStrokes < SawStrokesNeeded) return;
        LineOf(out float ang, out float ofs);
        float sum = 0f;
        int misses = 0;
        foreach (CraftTimingHit h in hits) { sum += h.score; if (h.damage) misses++; }
        float steady = sum / hits.Count;
        if (Send(new CraftRequest(CraftRequestKind.SawGem, p.id) { a = ang, b = ofs, c = steady, d = drip ? 1f : 0f, i0 = Mathf.RoundToInt(sawPressure.value * 1000f), i1 = misses }))
            said = $"Sawn through - {steady * 100:0}% true" + (misses > 0 ? $", the blade jumped {misses} time{(misses == 1 ? "" : "s")}." : ".");
        lineSet = false;
        hits.Clear();
        sawStrokes = 0;
    }

    // ------------------------------------------------------------------ grinding
    private List<float> ProfileOf(GemState g) => g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);

    /// <summary>A point of the stone at plan angle <paramref name="t"/> (rad) and radius <paramref name="r"/>, turned on the dop and drawn round <paramref name="o"/>.</summary>
    private Vector2 Turned(Vector2 o, float t, float r) => new Vector2(o.x + Mathf.Cos(t + turn * Mathf.Deg2Rad) * r * StoneRadius, o.y + Mathf.Sin(t + turn * Mathf.Deg2Rad) * r * StoneRadius);

    /// <summary>Where the dop holds the stone: its edge at the wheel (against it while pressed, just off it otherwise).</summary>
    private Vector2 DopAt(List<float> prof)
    {
        float at = ((0f - turn) % 360f + 360f) % 360f / 360f * prof.Count;
        int k0 = Mathf.FloorToInt(at) % prof.Count;
        float edge = Mathf.Lerp(prof[k0], prof[(k0 + 1) % prof.Count], at - Mathf.Floor(at));
        return new Vector2(Wheel.x - edge * StoneRadius - (pressing ? 0f : 10f), Centre.y);
    }

    private void DrawGrind(CraftPixels c, CraftPiece p)
    {
        GemState g = p.gem;
        CraftGemDefinition d = GemOf(p);
        Color col = d != null ? d.color : Color.white;
        col.a = 1f;
        if (!g.oriented) { DrawStone(c, p, true, Station.Reveals); return; }
        List<float> prof = ProfileOf(g);
        int n = CraftLapidary.ProfileRadii;
        Vector2 dop = DopAt(prof);
        var stone = new List<Vector2>();
        var target = new List<Vector2>();
        for (int k = 0; k < n; k++)
        {
            float t = k * Mathf.PI * 2f / n;
            stone.Add(Turned(dop, t, prof[k]));
            target.Add(Turned(dop, t, (float)CraftLapidary.TargetRadius(g, k)));
        }
        c.Rect(dop.x - 5f, dop.y - 5f, dop.x + 5f, dop.y + 5f, Iron); // the dop
        c.Polygon(stone, col * (0.55f + 0.45f * (float)g.tone));
        c.Outline(stone, col * 0.6f, 2f);
        c.Disc(dop.x, dop.y, 4f, Iron);
        // The planned outline (gold); red where the wheel has gone into it.
        c.Outline(target, new Color(1f, 0.8f, 0.38f, 0.9f), 1.5f);
        for (int k = 0; k < n; k++)
        {
            float tr = (float)CraftLapidary.TargetRadius(g, k);
            if (prof[k] < tr - 0.01f)
            {
                float t = k * Mathf.PI * 2f / n;
                Vector2 a = Turned(dop, t, prof[k]), b = Turned(dop, t, tr);
                c.Line(a.x, a.y, b.x, b.y, new Color(1f, 0.3f, 0.25f), 3f);
            }
        }
        // The wheel (spinning: the streaks move), the drip, the pressure panel.
        c.Disc(Wheel.x + WheelR, Wheel.y, WheelR, new Color(0.42f, 0.42f, 0.45f));
        for (int k = 0; k < 6; k++)
        {
            float a = Time.unscaledTime * 6f + k * Mathf.PI / 3f;
            c.Line(Wheel.x + WheelR, Wheel.y, Wheel.x + WheelR + Mathf.Cos(a) * WheelR * 0.9f, Wheel.y + Mathf.Sin(a) * WheelR * 0.9f, new Color(0.55f, 0.55f, 0.6f), 1f);
        }
        if (drip) c.Line(Wheel.x, Wheel.y + WheelR + 20f, Wheel.x, Wheel.y + 8f, Water, 2f);
        else c.Halo(Wheel.x, Wheel.y, 30f, new Color(1f, 0.5f, 0.2f), pressing ? 0.4f : 0f);
        if (pressing) c.Halo(Wheel.x, Wheel.y, 14f, new Color(1f, 0.85f, 0.5f), 0.5f + 0.5f * pressure);
        c.Rect(PressPanel.xMin, PressPanel.yMin, PressPanel.xMax, PressPanel.yMax, new Color(0.2f, 0.19f, 0.18f));
        c.Rect(PressPanel.xMin + 4, PressPanel.yMin + 4, PressPanel.xMin + 24, PressPanel.yMin + 4 + (PressPanel.height - 8) * (pressing ? pressure : 0f), pressure > 0.75f ? new Color(1f, 0.4f, 0.3f) : new Color(0.45f, 0.9f, 0.45f));
        Meter(c, PressPanel.xMin + 34, PressPanel.yMax - 20, PressPanel.width - 44, 8, 1f - (float)CraftLapidary.Excess(g), new Color(0.6f, 0.85f, 1f));
        Meter(c, PressPanel.xMin + 34, PressPanel.yMax - 36, PressPanel.width - 44, 6, Mathf.Clamp01(heat), heat > 0.7f ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.8f, 0.4f));
    }

    private void GrindPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (p == null) return;
        if (!p.gem.oriented) { if (phase == CraftPointer.Down) said = "Mark the stone out first (Orient): the wheel needs an outline to grind to."; return; }
        if (phase == CraftPointer.Down && (PressPanel.Contains(pos) || Vector2.Distance(pos, Wheel + new Vector2(WheelR, 0f)) < WheelR)) { pressing = true; grindTick = 0f; return; }
        if (phase == CraftPointer.Up && pressing) { pressing = false; FlushGrind(); }
    }

    private void FlushGrind()
    {
        CraftPiece p = Stone;
        if (p == null || touches.Count < 2) { touches.Clear(); pressures.Clear(); return; }
        // steady pressure grinds a true outline
        float mean = 0f, sq = 0f;
        foreach (float v in pressures) mean += v;
        mean /= Mathf.Max(1, pressures.Count);
        foreach (float v in pressures) sq += (v - mean) * (v - mean);
        float steady = Mathf.Clamp01(1f - Mathf.Sqrt(sq / Mathf.Max(1, pressures.Count)) * 3f);
        double over = p.gem.overground;
        if (Send(new CraftRequest(CraftRequestKind.GrindGem, p.id) { points = new List<float>(touches), a = drip ? 1f : 0f, b = steady }))
            said = p.gem.overground > over + 0.005 ? "The wheel is cutting into the planned stone: ease off there." : CraftLapidary.Excess(p.gem) <= 0.15 ? "Ground to its outline: facet it." : "Grinding.";
        touches.Clear();
        pressures.Clear();
    }

    // ------------------------------------------------------------------ faceting
    private CraftGemCut CutOf(CraftPiece p) => Rules.Cut(p.gem.cut);

    /// <summary>Facets cut on the bar for a tier (each hit cuts its share of the tier's facets).</summary>
    private int HitsFor(CraftCutTier t) => CraftMastery.FacetHits(t.facets, Mastery, MasterySettings);

    private void NextTier()
    {
        CraftPiece p = Stone;
        CraftGemCut cut = p != null ? CutOf(p) : null;
        if (cut == null) { said = "Mark the stone out first (Orient)."; return; }
        tier = Mathf.Min(p.gem.facets.Count, cut.tiers.Count - 1);
        hits.Clear();
        lastHit = null;
        barFor = "";
        said = $"{cut.tiers[tier].name}: set the angle, then click on the lap to cut each facet when the marker is on the target.";
    }

    private void RecutTier()
    {
        CraftPiece p = Stone;
        CraftGemCut cut = p != null ? CutOf(p) : null;
        if (cut == null || p.gem.facets.Count == 0) return;
        if (tier < 0) tier = p.gem.facets.Count - 1;
        hits.Clear();
        lastHit = null;
        barFor = "";
        said = $"Recutting the {cut.tiers[tier].name.ToLowerInvariant()}: it costs a little weight.";
    }

    private void DrawFacet(CraftPixels c, CraftPiece p)
    {
        if (facetFor != p.id) { facetFor = p.id; tier = -1; hits.Clear(); }
        CraftGemCut cut = CutOf(p);
        // The stone in profile on its dop: the crown above, the pavilion below; the tiers cut so far shaded.
        Vector2 o = new Vector2(230f, 220f);
        CraftGemDefinition d = Rules.Gem(p.gem.gem);
        Color col = d != null ? d.color : Color.white;
        col.a = 1f;
        c.Rect(o.x - 4, o.y - 190, o.x + 4, o.y - 110, Iron); // the dop
        var outline = new List<Vector2> { new Vector2(o.x - 110, o.y), new Vector2(o.x - 60, o.y + 50), new Vector2(o.x + 60, o.y + 50), new Vector2(o.x + 110, o.y), new Vector2(o.x, o.y - 110) };
        c.Polygon(outline, col * 0.6f);
        if (cut != null)
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                CraftFacetRecord f = p.gem.facets.Find(x => x.tier == t);
                if (f == null) continue;
                bool crown = cut.tiers[t].kind == CraftCutTierKind.Crown || cut.tiers[t].kind == CraftCutTierKind.Table || cut.tiers[t].kind == CraftCutTierKind.Dome;
                Color fc = f.misses > 0 ? new Color(1f, 0.4f, 0.3f, 0.6f) : f.asymmetry > 0.35 ? new Color(1f, 0.8f, 0.4f, 0.5f) : new Color(1f, 1f, 1f, 0.35f);
                c.Line(o.x - 100 + t * 8, crown ? o.y + 10 : o.y - 10, o.x + 100 - t * 8, crown ? o.y + 10 : o.y - 10, fc, 2f);
            }
        // The lap spinning.
        c.Rect(lap.xMin, lap.yMin, lap.xMax, lap.yMax, new Color(0.3f, 0.3f, 0.32f));
        c.Ring(lap.center.x, lap.center.y, 50f, 4f, new Color(0.5f, 0.5f, 0.55f));
        // The index wheel: a stop lit for every facet cut so far in this tier.
        int needed = cut != null && tier >= 0 ? HitsFor(cut.tiers[tier]) : 8;
        c.Disc(indexWheel.center.x, indexWheel.center.y, 50f, Iron);
        for (int k = 0; k < needed; k++)
        {
            float a = k * Mathf.PI * 2f / needed;
            Color sc = k < hits.Count ? GradeColor(hits[k].grade) : k == hits.Count ? Mark : new Color(0.6f, 0.6f, 0.62f);
            c.Disc(indexWheel.center.x + Mathf.Cos(a) * 42f, indexWheel.center.y + Mathf.Sin(a) * 42f, 4f, sc);
        }
        // The protractor: a quarter arc with a tick every 10°, the quill's angle on it.
        for (int k = 0; k < 90; k += 3)
        {
            float a0 = k * Mathf.Deg2Rad, a1 = (k + 3) * Mathf.Deg2Rad;
            c.Line(Pivot.x + Mathf.Sin(a0) * Arc, Pivot.y + Mathf.Cos(a0) * Arc, Pivot.x + Mathf.Sin(a1) * Arc, Pivot.y + Mathf.Cos(a1) * Arc, new Color(0.6f, 0.6f, 0.62f), 2f);
        }
        for (int k = 0; k <= 90; k += 10)
        {
            float a = k * Mathf.Deg2Rad;
            c.Line(Pivot.x + Mathf.Sin(a) * (Arc - 10f), Pivot.y + Mathf.Cos(a) * (Arc - 10f), Pivot.x + Mathf.Sin(a) * Arc, Pivot.y + Mathf.Cos(a) * Arc, new Color(0.8f, 0.8f, 0.82f), 1.5f);
        }
        float qa = quill * Mathf.Deg2Rad;
        c.Line(Pivot.x, Pivot.y, Pivot.x + Mathf.Sin(qa) * Arc, Pivot.y + Mathf.Cos(qa) * Arc, Mark, 3f);
        c.Disc(Pivot.x, Pivot.y, 5f, Iron);
        if (cut != null && tier >= 0)
            DrawTimingBar(c, BarFor(p), BarRect.xMin, BarRect.yMin, BarRect.width, BarRect.height, lastHit);
        if (!drip) c.Halo(o.x, o.y, 40f, new Color(1f, 0.5f, 0.2f), tier >= 0 && hits.Count > 0 ? 0.4f : 0f);
    }

    private void FacetPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Stone;
        CraftGemCut cut = p != null ? CutOf(p) : null;
        if (phase == CraftPointer.Scroll) { quill = Mathf.Clamp(quill + (scroll > 0 ? 0.5f : -0.5f), 0f, 90f); return; }
        if (cut == null) { if (phase == CraftPointer.Down) said = "Mark the stone out first (Orient)."; return; }
        if (tier < 0) NextTier();
        if (phase == CraftPointer.Down && OnDial(pos) && !lap.Contains(pos)) { quill = QuillAt(pos); holding = true; return; }
        if (phase == CraftPointer.Drag && holding) { quill = QuillAt(pos); return; }
        if (phase == CraftPointer.Up) { holding = false; return; }
        if (phase != CraftPointer.Down || !(lap.Contains(pos) || BarRect.Contains(pos) || indexWheel.Contains(pos))) return;
        // One facet ground on the lap: judged on the bar.
        CraftTimingBar b = BarFor(p);
        CraftTimingHit hit = b.Hit(Hands);
        lastHit = hit;
        hits.Add(hit);
        CraftCutTier ct = cut.tiers[tier];
        int needed = HitsFor(ct);
        said = $"Facet {hits.Count}/{needed}: {HitWords(hit, "it cut through its neighbour")}";
        b.NewTargets(p.gem.seed, 500 + tier * 16 + hits.Count);
        Hands.Move(0.05);
        if (hits.Count < needed) return;
        // The tier as its facets came: their angle and meet off as far as the hits were, their unevenness, the botched ones.
        float angle = 0f, index = 0f, meet = 0f, mean = 0f, sq = 0f;
        int misses = 0;
        foreach (CraftTimingHit h in hits)
        {
            float off = Mathf.Clamp(h.offset, -1.5f, 1.5f);
            angle += off * 2.5f;
            index += (1f - h.score) * 0.5f;
            meet += Mathf.Clamp(off * 0.8f, -1f, 1f);
            mean += h.score;
            if (h.damage) misses++;
        }
        int nh = hits.Count;
        angle /= nh; index /= nh; meet /= nh; mean /= nh;
        foreach (CraftTimingHit h in hits) sq += (h.score - mean) * (h.score - mean);
        float asym = Mathf.Clamp01(Mathf.Sqrt(sq / nh) * 2.5f + (1f - mean) * 0.3f);
        if (Send(new CraftRequest(CraftRequestKind.FacetGem, p.id) { i0 = tier, count = ct.facets, a = quill + angle, b = index, c = meet, d = drip ? 1f : 0f, i1 = misses, i2 = Mathf.RoundToInt(asym * 1000f) }))
            said = $"{ct.name} cut - {mean * 100:0}% true" + (misses > 0 ? $", {misses} botched: recut the tier." : asym > 0.35f ? ", unevenly." : ".");
        tier = -1;
        hits.Clear();
    }

    // ------------------------------------------------------------------ polishing
    /// <summary>The polish region (3 × 3 over the stone) under a point, or -1 off the stone.</summary>
    private static int RegionAt(Vector2 pos)
    {
        if (Vector2.Distance(pos, Centre) > StoneRadius) return -1;
        int col = Mathf.Clamp(Mathf.FloorToInt((pos.x - (Centre.x - StoneRadius)) / (2f * StoneRadius / 3f)), 0, 2);
        int row = Mathf.Clamp(Mathf.FloorToInt((pos.y - (Centre.y - StoneRadius)) / (2f * StoneRadius / 3f)), 0, 2);
        return row * 3 + col;
    }

    private void DrawPolish(CraftPixels c, CraftPiece p)
    {
        DrawStone(c, p, true, Station.Reveals);
        GemState g = p.gem;
        // How bright each region has come (dull dark, bright white, past it orange: the edges rounding).
        float cell = 2f * StoneRadius / 3f;
        for (int k = 0; k < CraftLapidary.PolishRegions; k++)
        {
            float v = k < g.polishRegions.Count ? g.polishRegions[k] : (float)(-Math.Log(Math.Max(1e-4, 1 - g.polish)) / 1.6);
            float shine = 1f - Mathf.Exp(-1.6f * v);
            float x0 = Centre.x - StoneRadius + (k % 3) * cell, y0 = Centre.y - StoneRadius + (k / 3) * cell;
            Color tint = v > CraftLapidary.OverPolish ? new Color(1f, 0.55f, 0.2f, 0.35f) : shine < 0.6f ? new Color(0f, 0f, 0f, 0.35f * (1f - shine)) : new Color(1f, 1f, 1f, 0.25f * shine);
            c.Rect(x0 + 2, y0 + 2, x0 + cell - 2, y0 + cell - 2, tint);
        }
        c.Rect(430, 60, 690, 300, new Color(0.3f, 0.3f, 0.32f));
        c.Ring(560, 180, 100f, 3f, new Color(0.6f, 0.55f, 0.5f));
        Meter(c, 430, 360, 260, 8, Mathf.Clamp01(heat), heat > 0.7f ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.8f, 0.4f));
        Meter(c, 430, 346, 260, 6, (float)g.polish, new Color(0.6f, 0.85f, 1f));
        if (holding && ui.Hover.x >= 0) c.Ring(ui.Hover.x, ui.Hover.y, 10f, 2f, Mark);
    }

    private void PolishPointer(CraftPointer phase, Vector2 pos)
    {
        if (Stone == null) return;
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) < StoneRadius) { holding = true; polishHeld = 0f; return; }
        if (phase == CraftPointer.Up && holding) { holding = false; FlushPolish(); }
    }

    private void FlushPolish()
    {
        CraftPiece p = Stone;
        if (p == null || passes == 0) { passes = 0; pressSum = speedSum = 0f; Array.Clear(regionTime, 0, regionTime.Length); return; }
        string id = compounds.Count > 0 ? compounds[Mathf.Clamp(compound.value, 0, compounds.Count - 1)] : null;
        float t = Mathf.Max(0.01f, passes * 0.5f);
        var where = new List<float>(regionTime) { 0f }; // 9 regions (and an even count)
        if (id != null && Send(new CraftRequest(CraftRequestKind.PolishGem, p.id) { id = id, count = passes, a = Mathf.Clamp01(pressSum / t * 1.2f + heat * 0.3f), b = speedSum / t, points = where }))
        {
            int dull = 0, over = 0;
            foreach (float v in p.gem.polishRegions) { if (v < 0.6f) dull++; if (v > CraftLapidary.OverPolish) over++; }
            said = heat > 0.7f ? "It is getting hot on the lap: lighter, slower." : over > 0 ? "Polished past bright there: the edges round." : dull > 0 ? $"Polishing - {dull} region{(dull == 1 ? "" : "s")} still dull." : "Polished bright all over.";
        }
        passes = 0;
        pressSum = speedSum = 0f;
        Array.Clear(regionTime, 0, regionTime.Length);
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (Stone == null && phase == CraftPointer.Down) { said = "Pick a stone (left): put a rough stone on the bench first."; return; }
        switch (place)
        {
            case Place.Clean: CleanPointer(phase, pos); break;
            case Place.Test: TestPointer(phase, pos); break;
            case Place.Evaluate: EvaluatePointer(phase, pos, scroll); break;
            case Place.Orient: OrientPointer(phase, pos, scroll); break;
            case Place.Split: case Place.Saw: CutPointer(phase, pos); break;
            case Place.Grind: GrindPointer(phase, pos); break;
            case Place.Facet: FacetPointer(phase, pos, scroll); break;
            case Place.Polish: PolishPointer(phase, pos); break;
        }
    }

    /// <summary>The gem book: what a gem tester's tables say of every stone (sorted by refractive index).</summary>
    private string Book(CraftPiece p)
    {
        var list = new List<CraftGemDefinition>(Rules.Gems);
        list.Sort((a, b) => a.refractiveIndex.CompareTo(b.refractiveIndex));
        var sb = new System.Text.StringBuilder("<b>Gem book</b> - RI · SG · hardness · pleochroism\n");
        foreach (CraftGemDefinition g in list)
            sb.Append($"{g.Name}: {g.refractiveIndex:0.00} · {g.specificGravity:0.00} · {g.hardness:0.#} · {(g.pleochroism > 0.4 ? "strong" : g.pleochroism > 0.12 ? "weak" : "none")}{(g.opaque ? " · opaque" : "")}{(g.cleavage >= CraftCleavage.Good ? " · cleaves" : "")}\n");
        return sb.ToString();
    }

    public override void Refresh()
    {
        RefreshCounts(compound, compounds);
        if (info == null) return;
        CraftPiece p = Stone;
        if (book != null && place == Place.Identify) book.text = Book(p);
        string how;
        if (p == null) how = "<b>Lapidary</b>: put a rough stone on the bench (from your bag) and pick it (left).";
        else
        {
            GemState g = p.gem;
            CraftGemCut cut = CutOf(p);
            CraftGemDefinition d = GemOf(p);
            switch (place)
            {
                case Place.Clean:
                    how = g.stage >= CraftGemStage.Faceted ? "It is cut: there is nothing on it to clean off."
                        : $"<b>Clean</b>: hold and scrub over the brown crust - the brush is slow but harmless, the pick quick but it marks bare stone (and chips a brittle one worked roughly). Dirt {CraftLapidary.Dirt(g) * 100:0}%; crust left on blurs the tests and hides inclusions.";
                    break;
                case Place.Test:
                    how = CurrentTest == CraftGemTest.Scratch ? "<b>Hardness</b>: pick a point and draw it lightly and briefly across the stone: if it is scratched, the point is harder."
                        : CurrentTest == CraftGemTest.Refractometer ? "<b>Refractometer</b>: press and hold the stone still on the prism (the panel) until the green gauge fills - the shadow edge then reads true."
                        : CurrentTest == CraftGemTest.Gravity ? "<b>Balance</b>: click the panel to weigh it in water - the needle swings; click again when it has come to rest."
                        : CurrentTest == CraftGemTest.Dichroscope ? "<b>Dichroscope</b>: press and drag slowly round the ring: do the two windows change colour as it turns?"
                        : "<b>Loupe</b>: click the stone (or Look) to look at it closely.";
                    how += $" {Readings(g)}";
                    if (CraftLapidary.Dirt(g) > 0.25) how += " (It is still crusted: the readings will be off - clean it.)";
                    break;
                case Place.Identify:
                    how = $"<b>Identify</b>: {Readings(g)} Compare with the book and name it.";
                    break;
                case Place.Evaluate:
                    how = $"<b>Evaluate</b>: move the loupe over the stone while holding, and turn the wheel to focus through it (blue gauge = depth). Searched: {looks.Count / 3} looks.{(g.evaluated ? $" Judged worth about {g.estimate:0} as it is - found {g.inclusions.FindAll(i => i.found).Count} inclusions." : "")}";
                    break;
                case Place.Orient:
                {
                    CraftGemGrade pr = Preview(p);
                    how = $"<b>Orient</b>: drag the gold circle (the finished stone) clear of the flaws, the wheel sizes it; tilt the table (gold line) across the axis (blue line, from the dichroscope), away from the cleavage (red). Then Mark it out.{(pr != null ? $" Expected about {pr.Yield:0.00} ct, worth {pr.Value:0}." : "")}";
                    break;
                }
                case Place.Split:
                    how = d == null ? "" : Snap && !CraftLapidary.Snappable(d) ? $"{d.Name} cannot be nicked and snapped: {(d.cleavage >= CraftCleavage.Good ? "split it along its cleavage" : "it is too tough - saw it")}."
                        : !Snap && d.cleavage == CraftCleavage.None ? $"{d.Name} has no cleavage: {(CraftLapidary.Snappable(d) ? "nick and snap it, or saw it" : "saw it")}."
                        : Snap ? "<b>Nick and snap</b>: draw the line, then strike when the marker is on the white line. It never breaks clean: the further off the blow, the more is lost, and far off it cracks."
                        : "<b>Cleave</b>: draw the line along the cleavage (the faint lines) - off the plane it shatters - then strike when the marker is on the white line: the force that parts it.";
                    break;
                case Place.Saw:
                    how = $"<b>Saw</b>: draw the line across the rough outside the gold circle, then click for each stroke when the marker is on a target ({sawStrokes}/{SawStrokesNeeded}). Keep the drip on. {CraftLapidary.Excess(g) * 100:0}% of the rough is still outside the stone ({(CraftLapidary.Excess(g) <= CraftLapidary.PreformExcess ? "little enough to grind" : $"saw it below {CraftLapidary.PreformExcess * 100:0}%")}).";
                    break;
                case Place.Grind:
                    how = !g.oriented ? "Mark the stone out first (Orient)." : g.stage >= CraftGemStage.Faceted || g.facets.Count > 0 ? "It is already being faceted."
                        : $"<b>Grind</b>: the stone turns against the wheel. Hold in the panel to press - higher presses harder - while the part outside the gold outline passes the wheel, and ease off before it cuts in (red). Keep the pressure steady. Still to take off: {CraftLapidary.Excess(g) * 100:0}% (facet below 15%).{(g.shapeError > 0.15 ? " The outline is going lopsided." : "")}";
                    break;
                case Place.Facet:
                {
                    string chart = "";
                    double ri = g.readRI >= 0 ? g.readRI : g.identified || CraftJournal.KnowsGem(ui.Known, g.gem) ? Rules.Gem(g.gem)?.refractiveIndex ?? -1 : -1;
                    if (ri > 0) chart = $" The chart for RI {ri:0.00}: pavilion {CraftLapidary.IdealPavilion(ri):0.#}°, crown {CraftLapidary.IdealCrown(ri):0.#}°.";
                    how = cut == null ? "Mark the stone out first (Orient)." : g.stage >= CraftGemStage.Faceted && tier < 0 ? "Every tier is cut: polish it (or recut a tier)."
                        : $"<b>Facet</b> - {cut.tiers[Mathf.Clamp(tier >= 0 ? tier : g.facets.Count, 0, cut.tiers.Count - 1)].name} ({g.facets.Count}/{cut.tiers.Count}): set the quill to {quill:0.0}° (drag the dial or the wheel), then click the lap for each facet when the marker is on the target - 100% is a true facet, off it uneven, a miss botches it.{chart}";
                    break;
                }
                default:
                    how = g.stage < CraftGemStage.Faceted ? "Cut every tier first." : $"<b>Polish</b>: hold and move over the stone so every region comes up bright - higher presses lighter - and keep the heat (orange) out of the red. Polish {g.polish * 100:0}%. It will not mend a bad cut.";
                    break;
            }
        }
        info.text = (how + " " + said).Trim();
    }

    private string Readings(GemState g)
    {
        var parts = new List<string>();
        if (g.hardLo > 1 || g.hardHi < 10) parts.Add($"hardness {g.hardLo:0.#}-{g.hardHi:0.#}");
        if (g.readRI >= 0) parts.Add($"RI {g.readRI:0.00}");
        if (g.readSG >= 0) parts.Add($"SG {g.readSG:0.00}");
        if (g.readPleo >= 0) parts.Add(g.readPleo > 0.4 ? "strongly pleochroic" : g.readPleo > 0.12 ? "weakly pleochroic" : "not pleochroic");
        return parts.Count == 0 ? "No readings yet." : "Readings: " + string.Join(", ", parts) + ".";
    }
}
