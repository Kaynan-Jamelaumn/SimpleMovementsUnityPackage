using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lapidary's bench and the faceting machine, in ten places, from the rough to a cut stone (each place only where
/// the station has its tools: a jeweller's bench with the gem kit cleans, tests, names and searches stones, and its
/// polishing wheel polishes them):
/// <list type="bullet">
/// <item><b>Clean</b>: scrub the matrix and dirt off the rough (drag over the crust): the brush is slow but harmless, the
/// pick quick - worked on bare stone it scratches it, and a brittle stone handled roughly chips. Crust left on blurs
/// every test and hides what lies beneath it (the lap grinds off what is left when the stone is cut).</item>
/// <item><b>Test</b>: the gem tests, each with the hands. The loupe; a hardness point drawn lightly across the stone (a
/// point that scratches it is harder than it: the readings bracket its own hardness); the refractometer (hold the stone
/// still on the prism while the shadow edge settles); the hydrostatic balance (read it when the swinging needle comes to
/// rest); the dichroscope (turn it slowly round the stone).</item>
/// <item><b>Identify</b>: compare the readings with the gem book (every stone, two columns) and name it - once: the name
/// sticks, right or wrong.</item>
/// <item><b>Evaluate</b>: search it with the loupe: hold and move it over the stone and focus through its depth (the
/// wheel) - what shows sharp under the loupe is found (a steadier hand reaches further; a thick crust hides what lies under
/// it). The meter shows how much of the stone, at every depth, the loupe has truly gone over: only where its ring passed,
/// at the depths in focus, and nothing under a thick crust (<see cref="CraftLapidary.MarkSearched"/>).</item>
/// <item><b>Orient</b>: place the finished stone in the rough (drag the planned outline; the wheel sizes it) clear of what
/// you found, choose the cut, and tilt the table to the crystal: lay the gold line (the table) on the blue line (the axis,
/// from the dichroscope) - the table then lies across the axis, for the best colour - and keep it 8° or more off the red
/// line (the cleavage, once the loupe has shown it). The yield and worth are judged on what you know of the stone.</item>
/// <item><b>Split</b>: draw the line (it is cut where it is drawn, whichever way; a line that misses what is left of the
/// stone shows red and is not taken), then strike on the timing bar: a stone with a good or perfect cleavage parts on its
/// plane when the line lies within a few degrees of its faint cleavage lines (off it, it shatters); one without a
/// cleavage worth following is nicked and snapped (never clean); the indicator is the blow's force, 100% on the white
/// mark.</item>
/// <item><b>Saw</b>: draw the line, then make each stroke of the trim saw on the timing bar: a narrow target gives most, a
/// wide one is safer and gives less, a miss jumps the blade (chips and lost weight); a hard feed (past the red mark) chips
/// a brittle stone.</item>
/// <item><b>Grind</b>: the stone turns against the wheel; hold in the panel to press it (higher presses harder, as at the
/// smith's wheel: the PRESS gauge rises with the hand) where it stands outside the gold outline, and ease off before the
/// wheel cuts into it. Steady pressure grinds a true outline; pressed hard without the drip, the stone heats - the HEATED
/// gauge is the heat it has taken, which never goes out of it, and a sensitive one cracks at its red.</item>
/// <item><b>Facet</b>: tier by tier: pick the tier (the menu shows each one's state - a tier already cut is recut, at a
/// little weight), set the quill's angle on the protractor (drag round the dial or the wheel; the chart's angle for the
/// tier shows once the stone's refractive index is read or known), then cut each facet on the timing bar - accurate hits
/// cut symmetrical facets at the angle, poor ones uneven facets, a miss botches one.</item>
/// <item><b>Polish</b>: the compound on the lap (one from the bag charges it for the stone; another compound charges it
/// afresh); hold and move over the stone so every region comes up bright - only the time on the stone polishes. The
/// pressure is the hand's own, set with the wheel (or on the PRESS gauge) wherever the stone is held: from the green the
/// lap polishes at its full rate, in the red (pressure × lap speed) the stone heats; a table laid on a cleavage flakes
/// above its own mark. Polished on past bright, the edges round. Neither polish nor heat mends a bad cut.</item>
/// </list>
/// Right-click puts down what the hands hold - the line is rubbed out, a cut's strokes or a tier's hits abandoned, the
/// stone lifted off the wheel or the lap, the instrument put down - and never strikes, cuts, names or reads. The wheel
/// turns the quill, focuses the loupe, sizes the planned stone and sets the lap's pressure.
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
    private TMP_Dropdown test, point, gemName, cutName, technique, cleanTool, compound, tierPick;
    private Slider tableTilt, speed, sawPressure;
    private Button lookButton, nameButton, markButton;
    private readonly List<Button> dripButtons = new List<Button>();
    private TextMeshProUGUI info, bookHead, bookLeft, bookRight;
    private RectTransform bookColumns;
    private readonly List<string> gems = new List<string>(), cuts = new List<string>(), compounds = new List<string>();
    private string said = "";

    // The stone, from above.
    private static readonly Vector2 Centre = new Vector2(230f, 210f);
    private const float StoneRadius = 150f;
    private Vector2 ToPx(double x, double y) => new Vector2(Centre.x + (float)x * StoneRadius, Centre.y + (float)y * StoneRadius);
    private Vector2 ToPlan(Vector2 p) => (p - Centre) / StoneRadius;
    /// <summary>The cut stone drawn from above (from Faceted on): its planned outline filling the view, the long half-axis this many pixels.</summary>
    private const float CutRadius = 135f;

    // Cleaning: the work put into each sector of the crust, and how controlled it was (weighed by the way the tool went).
    private readonly float[] scrub = new float[CraftLapidary.CrustSectors];
    private float scrubTotal, controlSum, controlWeight, lastScrubTime;
    private Vector2 lastScrub;
    private bool scrubbing;
    private string scrubFor;

    // Testing.
    private bool holding;
    private float holdStart, moved, needle, needleV, sweep, lastAngle;
    private Vector2 lastPos;
    private static readonly Rect Instrument = new Rect(430, 50, 260, 300);

    // Evaluating: the loupe's focus, the looks of this stroke, and how much of the stone has been searched - what the
    // strokes sent went over (at the steadiness each was sent with), and this stroke so far (at its steadiness so far).
    private float focus = 0.5f;
    private readonly List<float> looks = new List<float>();
    private Vector2 lastLook;
    private float lastLookTime, steadySum;
    private int steadyN;
    private string looksFor;
    private readonly bool[] searched = new bool[CraftLapidary.SearchCells * CraftLapidary.SearchCells * CraftLapidary.SearchDepths];
    private readonly bool[] strokeSearched = new bool[CraftLapidary.SearchCells * CraftLapidary.SearchCells * CraftLapidary.SearchDepths];
    private string searchedFor = "";

    // Orienting.
    private Vector2 window, dragFrom;
    private float windowR = 0.6f;
    private bool draggingWindow;
    private string orientFor = "", previewKey = "";
    private CraftGemGrade previewGrade;
    private GemState previewGem;

    // Splitting and sawing: the line, and the timing bar its blow or its strokes are judged on.
    private Vector2 lineA, lineB;
    private bool lining, lineSet, drip = true;
    private string lineFor;
    private CraftTimingBar bar;
    private CraftTimingHit? lastHit;
    private readonly List<CraftTimingHit> sawHits = new List<CraftTimingHit>();
    private int sawStrokes;
    private string barFor = "";
    private static readonly Rect BarRect = new Rect(410, 330, 290, 22);

    // Grinding: the stone turning against the wheel.
    private float turn, pressure, grindTick, pendingHeat;
    private bool pressing;
    private string grindFor;
    private readonly List<float> touches = new List<float>(), pressures = new List<float>();
    private static readonly Vector2 Wheel = new Vector2(384f, 210f); // where the stone meets the wheel
    private const float WheelR = 70f;
    private static readonly Rect PressPanel = new Rect(536, 60, 176, 290);
    private const float TurnPerSecond = 80f;

    // Faceting: the tier, the quill's angle, then each facet cut on the timing bar.
    private float quill = 40f;
    private int tier = -1;
    private readonly List<CraftTimingHit> facetHits = new List<CraftTimingHit>();
    private string facetFor = "", tiersKey = "";
    private CraftRequest? retryFacet;
    private bool facetSending, dialHeld;
    private static readonly Rect Lap = new Rect(430, 40, 160, 130), IndexWheel = new Rect(600, 50, 110, 110);
    // The protractor: its pivot and arc (0° straight up the mast - 90° flat).
    private static readonly Vector2 Pivot = new Vector2(450f, 180f);
    private const float Arc = 130f;
    private static float QuillAt(Vector2 pos) => Mathf.Clamp(Mathf.Atan2(pos.x - Pivot.x, pos.y - Pivot.y) * Mathf.Rad2Deg, 0f, 90f);
    private static bool OnDial(Vector2 pos) => Vector2.Distance(pos, Pivot) < Arc + 15f && pos.x >= Pivot.x - 10f && pos.y >= Pivot.y - 10f;

    // Polishing: a pass is half a second on the lap; each is sent as it is made. The hand's pressure is its own (the wheel,
    // or the PRESS gauge), wherever on the stone it is held; a pass's pressure and speed are its means over its time on the
    // stone (what the station judges it by).
    private const float PassSeconds = (float)CraftLapidary.PassSeconds;
    private float polishHeld, onStoneHeld, pressSum, speedSum, lapPressure = (float)CraftLapidary.PolishFullRate;
    private bool polishing, pressHeld;
    private string polishFor;
    private readonly float[] regionTime = new float[CraftLapidary.PolishRegions];
    /// <summary>The PRESS gauge on the polish panel (a click or a drag on it sets the pressure; higher presses harder).</summary>
    private static readonly Rect PressGauge = new Rect(446, 60, 18, 280);

    public override string Title => "Lapidary";
    public override CraftDiscipline Discipline => CraftDiscipline.Jewellery;
    public override bool Available(CraftStation s) => s.Has(CraftTools.GemBench) || s.Has(CraftTools.FacetingMachine) || s.Has(CraftTools.JewelersBench);
    public override string Hint => "Clean the rough, test and name it, search it under the loupe, plan the cut, split or saw it, grind it to its outline, cut each tier of facets, polish - each with the hands. Right-click puts down.";

    private CraftPiece Stone => Selected != null && Selected.kind == CraftPieceKind.Gem ? Selected : null;
    private CraftGemDefinition GemOf(CraftPiece p) => p != null ? Rules.Gem(p.gem.gem) : null;
    private CraftGemCut CutOf(CraftPiece p) => p != null ? Rules.Cut(p.gem.cut) : null;
    /// <summary>Does the player know what this stone is (named rightly, or a gem they know)?</summary>
    private bool Known(CraftPiece p) => p != null && (p.gem.identified || CraftJournal.KnowsGem(ui.Known, p.gem.gem) || Station.Reveals);

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

    // ------------------------------------------------------------------ the controls
    public override void Build(RectTransform controls)
    {
        cleanRows.Clear(); evalRows.Clear(); testRows.Clear(); idRows.Clear(); orientRows.Clear(); splitRows.Clear(); sawRows.Clear(); grindRows.Clear(); facetRows.Clear(); polishRows.Clear();
        dripButtons.Clear();
        tiersKey = "";
        RectTransform r0 = Row(controls, 30f, 3f);
        placeButtons.Clear();
        if (!Here(place))
            for (int i = 0; i < PlaceNames.Length; i++)
                if (Here((Place)i)) { place = (Place)i; break; }
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(r0, PlaceNames[i], () => GoPlace(pl), 0f, UITheme.ButtonStyle.Tab, 28f);
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
        test.onValueChanged.AddListener(_ => { ResetHands(); Show(); });
        var pts = new List<string>();
        foreach (float f in Points) pts.Add($"Point {f:0.#}");
        point = Drop(t1, pts, 3, 110f);
        lookButton = Btn(t1, "Look (loupe)", () => DoTest(CraftGemTest.Loupe, 1f), 110f);
        testRows.Add(t1);

        gems.Clear();
        var gn = new List<string>();
        foreach (CraftGemDefinition g in Rules.Gems) { gems.Add(g.id); gn.Add(g.Name); }
        RectTransform i1 = Row(controls);
        Text(i1, "It is", 13f, 36f);
        gemName = Drop(i1, gn.Count > 0 ? gn : new List<string> { "-" }, 0, 200f);
        nameButton = Btn(i1, "Name it", NameIt, 90f, UITheme.ButtonStyle.Primary);
        idRows.Add(i1);
        // The gem book: two columns side by side, in a list that scrolls when there are more stones than fit.
        RectTransform content = UITheme.ScrollList("Book", controls, 118f, out ScrollRect _, null, 4, 2f);
        bookHead = UITheme.Label(content, "", 11f, UITheme.Text);
        InventoryUIFactory.Size(bookHead, -1f, 14f, 1f);
        bookColumns = UITheme.Row("Columns", content, 100f, 12f);
        bookLeft = UITheme.Label(bookColumns, "", 11f, UITheme.Muted);
        InventoryUIFactory.Size(bookLeft, -1f, 100f, 1f);
        bookRight = UITheme.Label(bookColumns, "", 11f, UITheme.Muted);
        InventoryUIFactory.Size(bookRight, -1f, 100f, 1f);
        foreach (TextMeshProUGUI t in new[] { bookHead, bookLeft, bookRight })
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.alignment = TextAlignmentOptions.TopLeft;
        }
        idRows.Add((RectTransform)content.parent.parent);

        cuts.Clear();
        var cn = new List<string>();
        foreach (CraftGemCut c in Rules.Cuts) { cuts.Add(c.id); cn.Add(c.Name); }
        RectTransform o1 = Row(controls);
        Text(o1, "Cut", 13f, 30f);
        cutName = Drop(o1, cn.Count > 0 ? cn : new List<string> { "-" }, 0, 170f);
        tableTilt = Slide(o1, "Table tilt", 0f, 90f, 0f, true, 66f, v => $"{v:0}°");
        markButton = Btn(o1, "Mark it out", MarkOut, 110f, UITheme.ButtonStyle.Primary);
        orientRows.Add(o1);

        RectTransform s1 = Row(controls);
        Text(s1, "Technique", 13f, 70f);
        technique = Drop(s1, new List<string> { "Along the cleavage (chisel)", "Nick and snap" }, 0, 220f);
        technique.onValueChanged.AddListener(_ => { barFor = ""; });
        Btn(s1, "New line", () => { DropLine(); said = "Draw the line across the stone."; }, 90f);
        splitRows.Add(s1);

        RectTransform w1 = Row(controls);
        dripButtons.Add(Btn(w1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        sawPressure = Slide(w1, "Feed pressure", 0f, 1f, 0.35f, false, 92f, v => $"{v * 100:0}%");
        sawPressure.onValueChanged.AddListener(_ => { barFor = ""; });
        Btn(w1, "New line", () => { DropLine(); said = "Draw the line across the rough."; }, 90f);
        sawRows.Add(w1);

        RectTransform g1 = Row(controls);
        dripButtons.Add(Btn(g1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        grindRows.Add(g1);

        RectTransform f1 = Row(controls);
        Text(f1, "Tier", 13f, 32f);
        tierPick = Drop(f1, new List<string> { "-" }, 0, 330f);
        tierPick.onValueChanged.AddListener(v => PickTier(v));
        dripButtons.Add(Btn(f1, "Drip on", () => { drip = !drip; Show(); }, 90f));
        facetRows.Add(f1);

        compounds.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Compound) compounds.Add(s.id);
        RectTransform p1 = Row(controls);
        Text(p1, "Compound", 13f, 70f);
        compound = CountedDrop(p1, compounds, 200f, "No compound");
        speed = Slide(p1, "Lap speed", 0f, 1f, 0.5f, false, 70f, v => $"{v * 100:0}%");
        polishRows.Add(p1);

        if (QuickBench)
        {
            // A test station's quick buttons: each step waits for the answer of the one before (on a network client the
            // copy of the workshop changes only when it comes).
            RectTransform q = Row(controls);
            Btn(q, "Clean all", () =>
            {
                CraftPiece p = Stone;
                if (p == null) return;
                var all = new List<float>();
                for (int k = 0; k < CraftLapidary.CrustSectors; k++) all.Add(k < p.gem.crust.Count ? Mathf.Min(3f, p.gem.crust[k] / 0.35f + 0.05f) : 0f);
                Send(new CraftRequest(CraftRequestKind.CleanGem, p.id) { points = all, a = 1f });
            }, 80f);
            Btn(q, "All tests", () => QuickTest(Stone?.id, 0), 80f);
            Btn(q, "Search all", () => QuickSearch(Stone?.id, 0), 80f);
            Btn(q, "Grind ideal", () => QuickGrind(Stone?.id, 0), 90f);
            Btn(q, "Cut ideal", () => QuickFacet(Stone?.id), 80f);
        }
        info = InfoRow(controls, 44f);
        Show();
    }

    private void QuickTest(string id, int t)
    {
        if (id == null || t >= TestNames.Length || Ws.Piece(id) == null) return;
        Send(new CraftRequest(CraftRequestKind.TestGem, id) { i0 = t, a = 1f, b = Points[Mathf.Clamp(point.value, 0, Points.Length - 1)] }, (ok, why) => QuickTest(id, t + 1));
    }

    private void QuickSearch(string id, int from)
    {
        if (id == null || Ws.Piece(id) == null) return;
        var all = new List<float>();
        for (float z = 0.05f; z < 1f; z += 0.15f)
            for (float y = -0.9f; y <= 0.9f; y += 0.2f)
                for (float x = -0.9f; x <= 0.9f; x += 0.2f) { all.Add(x); all.Add(y); all.Add(z); }
        if (from >= all.Count) return;
        var part = all.GetRange(from, Math.Min(510, all.Count - from));
        Send(new CraftRequest(CraftRequestKind.EvaluateGem, id) { points = part, a = 1f }, (ok, why) => { if (ok) QuickSearch(id, from + 510); });
    }

    private void QuickGrind(string id, int pass)
    {
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        if (p == null || !p.gem.oriented || pass >= 40 || CraftLapidary.Excess(p.gem) <= 0.1) return;
        List<float> prof = ProfileOf(p.gem);
        var t = new List<float>();
        for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
            if (prof[k] > CraftLapidary.TargetRadius(p.gem, k) + 0.04) { t.Add(k * 360f / CraftLapidary.ProfileRadii); t.Add(0.5f); }
        if (t.Count > 0)
            Send(new CraftRequest(CraftRequestKind.GrindGem, id) { points = t, a = 1f, b = 1f }, (ok, why) => { if (ok) QuickGrind(id, pass + 1); });
    }

    private void QuickFacet(string id)
    {
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        CraftGemCut cut = CutOf(p);
        CraftGemDefinition d = GemOf(p);
        if (cut == null || d == null || p.gem.facets.Count >= cut.tiers.Count) return;
        int t = p.gem.facets.Count;
        Send(new CraftRequest(CraftRequestKind.FacetGem, id) { i0 = t, count = cut.tiers[t].facets, a = (float)CraftLapidary.IdealAngle(cut.tiers[t], d.refractiveIndex), b = 0f, c = 0f, d = 1f },
            (ok, why) => { if (ok) QuickFacet(id); });
    }

    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Place p) && Here(p)) GoPlace(p);
    }

    public override bool HasPlace(string place) => Enum.TryParse(place, out Place p) && Here(p);

    private void GoPlace(Place p)
    {
        FlushAll();
        ResetHands();
        place = p;
        said = "";
        if (p == Place.Split) PickTechnique(Stone);
        Show();
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
        if (point != null) point.gameObject.SetActive(CurrentTest == CraftGemTest.Scratch);
        if (lookButton != null) lookButton.gameObject.SetActive(CurrentTest == CraftGemTest.Loupe);
        foreach (Button b in dripButtons)
        {
            TMP_Text t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = drip ? "Drip on" : "Drip off";
            if (b != null) UITheme.SetSelected(b, drip);
        }
    }

    // ------------------------------------------------------------------ the hands, and the piece changing
    /// <summary>Puts down what the hands hold now (the line, a cut's strokes, a tier's hits, the stone on the wheel or the lap, an instrument).</summary>
    private void ResetHands()
    {
        holding = scrubbing = pressing = lining = draggingWindow = dialHeld = polishing = pressHeld = false;
        DropLine();
        facetHits.Clear();
        lastHit = null;
        LiveDraw = false;
    }

    /// <summary>The line and the strokes or blow made along it: rubbed out.</summary>
    private void DropLine()
    {
        lineSet = lining = false;
        lineFor = null;
        barFor = "";
        sawHits.Clear();
        sawStrokes = 0;
        lastHit = null;
    }

    /// <summary>Sends what the hands did that is not sent yet (scrubbing, looks, touches on the wheel, a pass on the lap), each to the stone it was done on.</summary>
    private void FlushAll()
    {
        FlushScrub();
        FlushLooks();
        FlushGrind();
        FinishPass();
    }

    public override void Leave()
    {
        FlushAll();
        ResetHands();
    }

    /// <summary>The stone picked is changing: what was done on it is sent to it, everything held for it is put down.</summary>
    public override void SelectionChanging(string from, string to)
    {
        FlushAll();
        ResetHands();
        orientFor = facetFor = previewKey = tiersKey = "";
        previewGrade = null;
        retryFacet = null;
        facetSending = false;
        tier = -1;
        said = "";
    }

    public override void SelectionChanged(string to)
    {
        if (place == Place.Split) PickTechnique(Stone);
        Show();
    }

    /// <summary>Hand work of several presses under way that leaving would lose.</summary>
    public override string Busy => sawStrokes > 0 ? "sawing through the stone" : facetHits.Count > 0 ? "cutting the tier of facets" : null;

    /// <summary>Words for the stone picked now - an answer that comes after another was picked says nothing of it.</summary>
    private void SayFor(string id, string text)
    {
        if (ui != null && ui.SelectedId == id) said = text ?? "";
    }

    public override void Tick(float dt)
    {
        // The balance's needle swings and settles.
        needleV += (-needle * 9f - needleV * 0.9f) * dt;
        needle += needleV * dt;
        CraftPiece p = Stone;
        bool barShown = (place == Place.Split || place == Place.Saw) && lineSet || place == Place.Facet && FacetBarShown(p);
        if (barShown && bar != null) bar.Tick(dt);
        if (place == Place.Clean && scrubbing && Time.unscaledTime - lastScrubTime > 0.6f) FlushScrub();
        if (place == Place.Grind && p != null && p.gem.oriented)
        {
            turn = (turn + dt * TurnPerSecond) % 360f;
            if (pressing && ui.PointerHeld)
            {
                // higher in the panel presses harder, as at the smith's wheel (off the surface, the hand keeps its last pressure)
                if (ui.Hover.x >= 0) pressure = PressInPanel(ui.Hover.y);
                grindTick += dt;
                CraftGemDefinition d = GemOf(p);
                while (grindTick >= 0.05f)
                {
                    grindTick -= 0.05f;
                    // the part of the stone at the wheel (the stone's right side, screen angle 0)
                    float at = ((0f - turn) % 360f + 360f) % 360f + (float)Hands.Tremble(touches.Count + 231) * 120f;
                    touches.Add((at % 360f + 360f) % 360f);
                    touches.Add(pressure * 0.5f);
                    pressures.Add(pressure);
                    pendingHeat += (float)CraftLapidary.GrindHeat(pressure * 0.5f, drip ? 1 : 0, d != null && d.heatSensitive);
                }
                if (touches.Count >= 40) FlushGrind();
            }
            else if (pressing)
            {
                pressing = false;
                FlushGrind();
            }
        }
        if (place == Place.Polish && polishing)
        {
            if (!ui.PointerHeld) FinishPass();
            else if (p != null)
            {
                // the pass's time; what it spent on the stone (where, how hard, how fast) is what it polishes with
                polishHeld += dt;
                int k = RegionAt(p.gem, ui.Hover);
                if (k >= 0)
                {
                    regionTime[k] += dt;
                    onStoneHeld += dt;
                    pressSum += lapPressure * dt;
                    speedSum += (speed != null ? speed.value : 0.5f) * dt;
                }
                if (polishHeld >= PassSeconds) SendPass();
            }
        }
        bool hover = ui.Hover.x >= 0;
        LiveDraw = barShown || place == Place.Grind && p != null && p.gem.oriented || polishing || pressHeld || scrubbing || draggingWindow || dialHeld
                   || place == Place.Test && (holding || Mathf.Abs(needle) > 0.01f || Mathf.Abs(needleV) > 0.01f)
                   || place == Place.Evaluate && hover || place == Place.Polish && hover;
    }

    // ------------------------------------------------------------------ the stone, from above
    private static Color StoneColour(CraftGemDefinition d, GemState g)
    {
        Color col = d != null ? d.color : new Color(0.8f, 0.8f, 0.85f);
        col.a = 1f;
        float tone = 0.55f + 0.45f * (float)g.tone;
        Color c = Color.Lerp(col * tone, new Color(0.5f, 0.5f, 0.5f), d != null && d.opaque ? 0.1f : 0.25f);
        c.a = 1f;
        return c;
    }

    private void DrawStone(CraftPixels c, CraftPiece p, bool showFound, bool showAll)
    {
        GemState g = p.gem;
        if (g.stage >= CraftGemStage.Faceted && g.oriented)
        {
            DrawCutStone(c, p, showFound, showAll);
            return;
        }
        CraftGemDefinition d = GemOf(p);
        Color col = StoneColour(d, g);
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
        c.Polygon(pts, col);
        c.Outline(pts, col * 0.6f, 2f);
        // The cleavage's trace, once looked at under the loupe (only a cleavage worth following shows its faint lines).
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
            if (showAll || showFound && i.found) DrawInclusion(c, i, ToPx(i.x, i.y), Mathf.Max(2f, i.size * StoneRadius));
        if (place != Place.Clean) DrawCrust(c, g, null);
        if (g.chips > 0.05) c.Disc(pts[7].x, pts[7].y, 4f + (float)g.chips * 10f, Back);
    }

    private static void DrawInclusion(CraftPixels c, CraftInclusion i, Vector2 at, float r)
    {
        switch (i.kind)
        {
            case CraftInclusionKind.Feather: c.Line(at.x - r, at.y - r * 0.4f, at.x + r, at.y + r * 0.4f, new Color(1f, 1f, 1f, 0.8f), 1.5f); break;
            case CraftInclusionKind.Cloud: c.Disc(at.x, at.y, r, new Color(1f, 1f, 1f, 0.35f)); break;
            case CraftInclusionKind.Needle: c.Line(at.x - r, at.y + r, at.x + r, at.y - r, new Color(0.95f, 0.9f, 0.7f, 0.8f), 1f); break;
            default: c.Disc(at.x, at.y, r * 0.6f, new Color(0.1f, 0.1f, 0.1f, 0.9f)); break;
        }
        if (i.found) c.Ring(at.x, at.y, r + 3f, 1f, new Color(1f, 0.35f, 0.3f, 0.8f));
    }

    /// <summary>The cut stone's outline as drawn: the planned outline (an ellipse as long as the cut) filling the view.</summary>
    private static Rect CutBox(GemState g)
    {
        float h = CutRadius / Mathf.Max(1f, (float)g.cutLength);
        return new Rect(Centre.x - CutRadius, Centre.y - h, 2f * CutRadius, 2f * h);
    }

    /// <summary>A point of the plan on the cut stone's drawing.</summary>
    private static Vector2 CutPx(GemState g, double x, double y)
    {
        float a = Mathf.Max(0.05f, (float)CraftLapidary.WindowRadius(g));
        return new Vector2(Centre.x + (float)(x - g.windowX) / a * CutRadius, Centre.y + (float)(y - g.windowY) / a * CutRadius);
    }

    private static List<Vector2> Ellipse(Vector2 o, float rx, float ry, int n, float start = 0f)
    {
        var pts = new List<Vector2>(n);
        for (int k = 0; k < n; k++)
        {
            float a = start + k * Mathf.PI * 2f / n;
            pts.Add(new Vector2(o.x + Mathf.Cos(a) * rx, o.y + Mathf.Sin(a) * ry));
        }
        return pts;
    }

    /// <summary>A cut stone from above: a faceted one's table and facets, a cabochon's dome, as bright as it is polished.</summary>
    private void DrawCutStone(CraftPixels c, CraftPiece p, bool showFound, bool showAll)
    {
        GemState g = p.gem;
        CraftGemDefinition d = GemOf(p);
        CraftGemCut cut = CutOf(p);
        Color col = StoneColour(d, g);
        Rect box = CutBox(g);
        float rx = box.width / 2f, ry = box.height / 2f, shine = 0.12f + 0.45f * (float)g.polish;
        List<Vector2> outline = Ellipse(Centre, rx, ry, 48);
        c.Polygon(outline, col);
        if (cut != null && cut.cabochon)
        {
            // the dome: lighter toward its crown, a highlight where the light falls
            for (int k = 1; k <= 4; k++)
                c.Polygon(Ellipse(Centre + new Vector2(-rx * 0.06f * k, ry * 0.06f * k), rx * (1f - 0.2f * k), ry * (1f - 0.2f * k), 32), new Color(1f, 1f, 1f, 0.05f + 0.06f * shine * k));
        }
        else
        {
            // the table, and the facets running from its corners to the girdle
            List<Vector2> table = Ellipse(Centre, rx * 0.5f, ry * 0.5f, 8, Mathf.PI / 8f);
            Color line = new Color(1f, 1f, 1f, shine);
            c.Polygon(table, new Color(1f, 1f, 1f, 0.06f + 0.12f * (float)g.polish));
            c.Outline(table, line, 1.5f);
            for (int k = 0; k < 8; k++)
            {
                Vector2 rim = outline[(k * 6 + 3) % 48];
                c.Line(table[k].x, table[k].y, rim.x, rim.y, line, 1f);
                Vector2 mid = outline[(k * 6) % 48];
                c.Line(table[k].x, table[k].y, mid.x, mid.y, new Color(1f, 1f, 1f, shine * 0.6f), 1f);
            }
        }
        c.Outline(outline, col * 0.6f, 2f);
        foreach (CraftInclusion i in g.inclusions)
            if (showAll || showFound && i.found) DrawInclusion(c, i, CutPx(g, i.x, i.y), Mathf.Max(2f, i.size * CutRadius));
        if (g.chips > 0.05) c.Disc(outline[7].x, outline[7].y, 3f + (float)g.chips * 8f, Back);
    }

    /// <summary>The matrix and dirt still on the rough: a brown crust round its skin, thicker where there is more (<paramref name="pending"/>: the scrubbing not sent yet, as the workshop will take it off).</summary>
    private void DrawCrust(CraftPixels c, GemState g, float[] pending)
    {
        if (g.crust == null || g.crust.Count == 0 || g.stage >= CraftGemStage.Faceted) return;
        int n = g.crust.Count;
        float control = controlWeight > 0f ? controlSum / controlWeight : 1f;
        for (int k = 0; k < n; k++)
        {
            float amount = g.crust[k] - (pending != null && k < pending.Length ? (float)CraftLapidary.CrustRemoval(pending[k], Pick, Station.ToolQuality, control) : 0f);
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
        if (p == null)
        {
            CraftArt.Label(c, Centre.x - CraftArt.LabelWidth("PICK A STONE") / 2f, Centre.y, "PICK A STONE", CraftArt.LabelDim);
            return;
        }
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
        // The tool under the hand.
        Vector2 h = ui.Hover;
        if (h.x >= 0 && Vector2.Distance(h, Centre) < StoneRadius + 10f)
        {
            if (Pick) c.Line(h.x, h.y, h.x + 26, h.y + 40, new Color(0.75f, 0.76f, 0.8f), 2f);
            else { c.Rect(h.x - 14, h.y - 4, h.x + 14, h.y + 4, Wood); for (int k = -12; k <= 12; k += 4) c.Line(h.x + k, h.y - 4, h.x + k, h.y - 10, new Color(0.85f, 0.8f, 0.6f), 1f); }
        }
        // The crust round the stone, sector by sector (as the outline goes round, from its right side), how clean it is, how steady the hand.
        c.Rect(430, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, 440, 352, "CRUST ROUND THE STONE", CraftArt.LabelInk);
        int n = p.gem.stage >= CraftGemStage.Faceted ? 0 : p.gem.crust.Count;
        float control = controlWeight > 0f ? controlSum / controlWeight : 1f;
        for (int k = 0; k < n; k++)
        {
            float x0 = 442 + k * 15f;
            float left = p.gem.crust[k] - (float)CraftLapidary.CrustRemoval(scrub[k], Pick, Station.ToolQuality, control);
            Meter(c, x0, 190, 12, 150, Mathf.Clamp01(left), new Color(0.55f, 0.4f, 0.25f), (0f, 0.15f, GoodBand)); // clean once it is down in the band
        }
        double dirt = CraftLapidary.Dirt(p.gem);
        CraftArt.Label(c, 440, 166, $"CLEAN {(1 - dirt) * 100:0}%", CraftArt.LabelInk);
        Meter(c, 440, 148, 240, 12, 1f - (float)dirt, new Color(0.6f, 0.85f, 1f), (0.85f, 1f, GoodBand));
        CraftArt.Label(c, 440, 120, "STEADY HAND", CraftArt.LabelInk);
        Meter(c, 440, 102, 240, 8, control, new Color(0.45f, 0.9f, 0.45f));
        CraftArt.Label(c, 440, 70, Pick ? "PICK: QUICK, MARKS BARE STONE" : "BRUSH: SLOW, NEVER HARMS", CraftArt.LabelDim);
    }

    private void CleanPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (phase == CraftPointer.Up || phase == CraftPointer.Cancel)
        {
            // let go, or lifted off: what was scrubbed is scrubbed
            if (scrubbing) FlushScrub();
            scrubbing = false;
            return;
        }
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) < StoneRadius + 10f)
        {
            if (p.gem.stage >= CraftGemStage.Faceted) { said = "It is cut: the lap took its skin - there is nothing on it to clean off."; return; }
            scrubbing = true;
            scrubFor = p.id;
            lastScrub = pos;
            lastScrubTime = ui.PointerTime;
            return;
        }
        if (!scrubbing || phase != CraftPointer.Drag) return;
        float now = ui.PointerTime, dist = Vector2.Distance(pos, lastScrub);
        if (dist < 2f) return;
        // Work goes into the sector of the outline under the tool - only while the tool is on that sector's crust (where it is
        // drawn: from its inner edge out to the skin), not over the clean middle of the stone or off it; a frantic hand is
        // not a controlled one.
        Vector2 plan = ToPlan(pos + new Vector2((float)Hands.Tremble(controlWeight > 0 ? (int)controlWeight + 241 : 241) * 40f, 0f));
        float a = Mathf.Atan2(plan.y, plan.x);
        if (a < 0) a += Mathf.PI * 2f;
        int k = Mathf.FloorToInt(a / (Mathf.PI * 2f) * scrub.Length) % scrub.Length;
        float speedPx = dist / Mathf.Max(0.001f, now - lastScrubTime);
        lastScrub = pos;
        lastScrubTime = now;
        Hands.Move(0.003 * dist); // the arm tires with the way the tool goes, however it is sampled
        if (!OnCrust(p.gem, k, plan)) return;
        float work = dist / (StoneRadius * (Pick ? 2.2f : 1.4f));
        scrub[k] += work;
        scrubTotal += work;
        controlSum += Mathf.Clamp01(1.2f - speedPx / 1400f) * dist;
        controlWeight += dist;
        if (scrubTotal > 1.5f) FlushScrub();
    }

    /// <summary>
    /// Is the tool at <paramref name="plan"/> on the crust of sector <paramref name="k"/> - between the crust's inner edge
    /// (as thick as the crust left there, the scrubbing not yet sent taken off) and the skin, as <see cref="DrawCrust"/>
    /// draws it, give or take the tool's own width? A bare sector's skin still takes the tool (the pick marks it).
    /// </summary>
    private bool OnCrust(GemState g, int k, Vector2 plan)
    {
        if (g.crust == null || k < 0 || k >= g.crust.Count) return false;
        float control = controlWeight > 0f ? controlSum / controlWeight : 1f;
        float left = Mathf.Clamp01(g.crust[k] - (float)CraftLapidary.CrustRemoval(scrub[k], Pick, Station.ToolQuality, control));
        float t = Mathf.Atan2(plan.y, plan.x), r = plan.magnitude, reach = 14f / StoneRadius; // the brush's half-width
        float skin = ToPlan(Clip(g, Mathf.Cos(t), Mathf.Sin(t), 1f)).magnitude, inner = ToPlan(Clip(g, Mathf.Cos(t), Mathf.Sin(t), 1f - 0.45f * left)).magnitude;
        return r >= inner - reach && r <= skin + reach;
    }

    private void FlushScrub()
    {
        CraftPiece p = scrubFor != null ? Ws.Piece(scrubFor) : null;
        var work = new List<float>(scrub);
        float total = scrubTotal, control = controlWeight > 0f ? controlSum / controlWeight : 1f;
        bool pick = Pick;
        // taken out of the hands' buffers before it is sent: it never goes twice
        Array.Clear(scrub, 0, scrub.Length);
        scrubTotal = controlSum = controlWeight = 0f;
        if (p == null || total <= 0f || p.gem.stage >= CraftGemStage.Faceted) return;
        string id = p.id;
        double before = p.gem.scratches;
        Send(new CraftRequest(CraftRequestKind.CleanGem, id) { points = work, a = control, i0 = pick ? 1 : 0 }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(id) : null;
            if (q == null) return;
            SayFor(id, q.gem.scratches > before + 0.01 ? "The pick is marking the bare stone: ease off there." : CraftLapidary.Dirt(q.gem) < 0.05 ? "Clean to the bare stone." : $"{LastRecord(id)}.");
        });
    }

    // ------------------------------------------------------------------ testing
    private CraftGemTest CurrentTest => (CraftGemTest)Mathf.Clamp(test != null ? test.value : 0, 0, TestNames.Length - 1);

    private void DrawInstrument(CraftPixels c, CraftPiece p)
    {
        c.Rect(Instrument.xMin, Instrument.yMin, Instrument.xMax, Instrument.yMax, new Color(0.2f, 0.19f, 0.18f));
        switch (CurrentTest)
        {
            case CraftGemTest.Loupe:
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 18, "LOUPE: CLICK THE STONE", CraftArt.LabelInk);
                break;
            case CraftGemTest.Scratch:
            {
                // The point held to the stone: drag it lightly across.
                float pt = Points[Mathf.Clamp(point != null ? point.value : 0, 0, Points.Length - 1)];
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 18, $"HARDNESS POINT {pt:0.#}", CraftArt.LabelInk);
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 36, "DRAW IT LIGHTLY ACROSS", CraftArt.LabelDim);
                if (holding) c.Line(lastPos.x - 30, lastPos.y + 40, lastPos.x, lastPos.y, new Color(0.75f, 0.76f, 0.8f), 3f);
                Meter(c, Instrument.xMin + 10, Instrument.yMin + 20, Instrument.width - 20, 8, Mathf.Clamp01(moved / 160f), new Color(0.6f, 0.85f, 1f), (8f / 160f, 40f / 160f, GoodBand), (40f / 160f, 1f, HarmBand));
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMin + 34, "STROKE: SHORT AND LIGHT", CraftArt.LabelInk);
                break;
            }
            case CraftGemTest.Refractometer:
            {
                // The prism with its scale; the shadow edge sharpens while the stone is held still.
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 18, "REFRACTOMETER", CraftArt.LabelInk);
                c.Rect(Instrument.xMin + 40, Instrument.yMin + 40, Instrument.xMax - 40, Instrument.yMax - 60, new Color(0.85f, 0.88f, 0.9f));
                float still = holding ? Mathf.Clamp01((Time.unscaledTime - holdStart) / 2f) * Mathf.Clamp01(1f - moved / 60f) : 0f;
                float edge = Instrument.yMin + 40 + (Instrument.height - 100) * 0.5f;
                c.Rect(Instrument.xMin + 40, Instrument.yMin + 40, Instrument.xMax - 40, edge - (1f - still) * 30f, new Color(0.25f, 0.27f, 0.3f));
                for (int k = 0; k < 10; k++) c.Line(Instrument.xMin + 40, Instrument.yMin + 50 + k * 19, Instrument.xMin + 60, Instrument.yMin + 50 + k * 19, Iron, 1f);
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMin + 24, "HELD STILL", CraftArt.LabelInk);
                Meter(c, Instrument.xMin + 10, Instrument.yMin + 8, Instrument.width - 20, 8, still, new Color(0.45f, 0.9f, 0.45f), (0.3f, 1f, GoodBand));
                break;
            }
            case CraftGemTest.Gravity:
            {
                // The balance's needle, swinging until it settles.
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 18, "BALANCE: CLICK TO WEIGH", CraftArt.LabelInk);
                Vector2 pivot = new Vector2(Instrument.center.x, Instrument.yMax - 50);
                float a = Mathf.Clamp(needle, -1f, 1f) * 40f * Mathf.Deg2Rad;
                c.Line(pivot.x, pivot.y, pivot.x + Mathf.Sin(a) * 190f, pivot.y - Mathf.Cos(a) * 190f, Iron, 3f);
                c.Rect(pivot.x - 2, pivot.y - 225, pivot.x + 2, pivot.y - 195, Mark); // rest: the mark under the needle
                c.Disc(pivot.x, pivot.y, 6f, Iron);
                float rest = Mathf.Clamp01(1f - (Mathf.Abs(needle) + Mathf.Abs(needleV) * 0.3f) * 2f);
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMin + 24, holding ? "AT REST" : "", CraftArt.LabelInk);
                if (holding) Meter(c, Instrument.xMin + 10, Instrument.yMin + 8, Instrument.width - 20, 8, rest, new Color(0.45f, 0.9f, 0.45f), (0.5f, 1f, GoodBand));
                break;
            }
            case CraftGemTest.Dichroscope:
            {
                // The dichroscope's two windows: the colours differ as it is turned (for a pleochroic stone).
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMax - 18, "DICHROSCOPE: TURN IT", CraftArt.LabelInk);
                CraftGemDefinition d = GemOf(p);
                Color col = d != null ? d.color : Color.white;
                col.a = 1f;
                float pleo = d != null ? d.pleochroism : 0f;
                float turnA = lastAngle;
                Color a = Color.Lerp(col, Color.Lerp(col, new Color(col.b, col.r, col.g), pleo), 0.5f + 0.5f * Mathf.Sin(turnA * 2f));
                Color b = Color.Lerp(col, Color.Lerp(col, new Color(col.g, col.b, col.r), pleo), 0.5f - 0.5f * Mathf.Sin(turnA * 2f));
                c.Ring(Instrument.center.x, Instrument.center.y, 100f, 10f, Iron);
                c.Rect(Instrument.center.x - 60, Instrument.center.y - 30, Instrument.center.x - 4, Instrument.center.y + 30, a);
                c.Rect(Instrument.center.x + 4, Instrument.center.y - 30, Instrument.center.x + 60, Instrument.center.y + 30, b);
                CraftArt.Label(c, Instrument.xMin + 10, Instrument.yMin + 24, "TURNED", CraftArt.LabelInk);
                Meter(c, Instrument.xMin + 10, Instrument.yMin + 8, Instrument.width - 20, 8, Mathf.Clamp01(sweep / 180f), new Color(0.6f, 0.85f, 1f), (0.25f, 1f, GoodBand));
                break;
            }
        }
    }

    private void DoTest(CraftGemTest t, float care)
    {
        CraftPiece p = Stone;
        if (p == null) { said = "Pick a stone (left)."; return; }
        CraftInstruments needs = CraftStation.InstrumentFor(t);
        if (!Station.HasInstrument(needs)) { said = $"There is no {CraftStation.InstrumentName(needs)} here."; return; }
        float pt = Points[Mathf.Clamp(point != null ? point.value : 0, 0, Points.Length - 1)];
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.TestGem, id) { i0 = (int)t, a = care, b = pt }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(id) : null;
            if (q == null) return;
            GemState g = q.gem;
            string read = t switch
            {
                CraftGemTest.Refractometer => $"The shadow edge reads {g.readRI:0.00}{(g.stage < CraftGemStage.Faceted ? " (a spot reading on the rough)" : "")}",
                CraftGemTest.Gravity => $"The balance reads a specific gravity of {g.readSG:0.00}",
                CraftGemTest.Dichroscope => g.readPleo > 0.4 ? "The two windows differ strongly as it turns" : g.readPleo > 0.12 ? "The two windows differ a little as it turns" : "The two windows stay alike as it turns",
                _ => LastRecord(id),
            };
            SayFor(id, $"{read}.{(care < 0.5f ? " A hurried reading: it may be off." : "")}");
        });
    }

    private void TestPointer(CraftPointer phase, Vector2 pos)
    {
        CraftGemTest t = CurrentTest;
        if (phase == CraftPointer.Cancel)
        {
            if (holding) said = t == CraftGemTest.Gravity ? "The balance is stilled: nothing read." : "Put down: nothing read.";
            holding = false;
            if (t == CraftGemTest.Gravity) needle = needleV = 0f;
            return;
        }
        if (t == CraftGemTest.Loupe) { if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) < StoneRadius + 10f) DoTest(t, 1f); return; }
        if (t == CraftGemTest.Gravity)
        {
            // Weigh: a click sets the needle swinging; the next, when it is still, reads it.
            if (phase != CraftPointer.Down || !Instrument.Contains(pos)) return;
            if (!holding) { holding = true; needle = 0.9f; needleV = 0f; said = "The needle swings: click again to read it when it rests on the mark."; return; }
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
            if ((t == CraftGemTest.Refractometer || t == CraftGemTest.Dichroscope) && !Instrument.Contains(pos) && !onStone) return;
            holding = true;
            holdStart = Time.unscaledTime;
            moved = sweep = 0f;
            lastPos = at;
            lastAngle = Mathf.Atan2(pos.y - Instrument.center.y, pos.x - Instrument.center.x);
            return;
        }
        if (!holding) return;
        if (phase == CraftPointer.Drag)
        {
            moved += Vector2.Distance(at, lastPos);
            if (t == CraftGemTest.Dichroscope)
            {
                float a = Mathf.Atan2(pos.y - Instrument.center.y, pos.x - Instrument.center.x);
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

    // ------------------------------------------------------------------ identifying
    private void NameIt()
    {
        CraftPiece p = Stone;
        if (p == null || gems.Count == 0) { said = "Pick a stone (left)."; return; }
        if (!string.IsNullOrEmpty(p.gem.identifiedAs)) { said = $"You named it {Rules.Gem(p.gem.identifiedAs)?.Name.ToLowerInvariant() ?? p.gem.identifiedAs}: the name sticks."; return; }
        string gem = gems[Mathf.Clamp(gemName.value, 0, gems.Count - 1)];
        string name = Rules.Gem(gem)?.Name.ToLowerInvariant() ?? gem;
        if (CraftLapidary.Tests(p.gem) < 2) { said = "Test it first: look at it, scratch it, read it - at least two tests."; return; }
        if (!Confirm($"name:{p.id}:{gem}", $"Click Name it again to name it {name}: the name sticks, right or wrong.")) return;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.IdentifyGem, id) { id = gem }, (ok, why) => { if (ok) SayFor(id, $"Named it {name}."); });
    }

    /// <summary>The gem book: what a gem tester's tables say of every stone (sorted by refractive index), in two columns.</summary>
    private void FillBook()
    {
        if (!string.IsNullOrEmpty(bookLeft.text)) return; // the book does not change during a visit
        var list = new List<CraftGemDefinition>(Rules.Gems);
        list.Sort((a, b) => a.refractiveIndex.CompareTo(b.refractiveIndex));
        var left = new System.Text.StringBuilder();
        var right = new System.Text.StringBuilder();
        int half = (list.Count + 1) / 2;
        for (int i = 0; i < list.Count; i++)
        {
            CraftGemDefinition g = list[i];
            string line = $"{g.Name}: {g.refractiveIndex:0.00} · {g.specificGravity:0.00} · {g.hardness:0.##} · {(g.pleochroism > 0.4 ? "strong" : g.pleochroism > 0.12 ? "weak" : "none")}{(g.opaque ? " · opaque" : "")}{(g.cleavage >= CraftCleavage.Good ? " · cleaves" : "")}";
            (i < half ? left : right).Append(line).Append('\n');
        }
        bookHead.text = "<b>Gem book</b> - refractive index · specific gravity · hardness · pleochroism";
        bookLeft.text = left.ToString();
        bookRight.text = right.ToString();
        float height = Mathf.Max(1, half) * 13.2f + 4f;
        InventoryUIFactory.Size(bookColumns, -1f, height);
        InventoryUIFactory.Size(bookLeft, -1f, height, 1f);
        InventoryUIFactory.Size(bookRight, -1f, height, 1f);
    }

    // ------------------------------------------------------------------ evaluating
    /// <summary>How steady the loupe is held in this stroke so far (1 still) - never steadier than these hands: what the station will take.</summary>
    private float SteadyNow => Mathf.Min(steadyN > 0 ? steadySum / steadyN : 1f, 1f - 4f * (float)CraftRequestGuard.FloorOf(Hands.skill));

    /// <summary>Where the next look of the loupe lands: the hand's place, shaken by its tremble as the look sent will be.</summary>
    private Vector2 Shaken(Vector2 pos) => pos + new Vector2((float)Hands.Tremble(looks.Count + 201) * 40f, (float)Hands.Tremble(looks.Count + 202) * 40f);

    private void DrawEvaluate(CraftPixels c, CraftPiece p)
    {
        GemState g = p.gem;
        bool cut = g.stage >= CraftGemStage.Faceted && g.oriented;
        DrawStone(c, p, true, Station.Reveals);
        Vector2 h = ui.Hover;
        float scale = cut ? CutRadius / Mathf.Max(0.05f, (float)CraftLapidary.WindowRadius(g)) : StoneRadius;
        float reach = (float)CraftLapidary.LoupeReach(SteadyNow, Station.ToolQuality);
        if (h.x >= 0 && Vector2.Distance(h, Centre) < (cut ? CutRadius : StoneRadius) + 10f)
        {
            // the loupe where the next look lands (the hand's tremble moves it a little), at the reach it will have
            Vector2 at0 = Shaken(h);
            Vector2 plan = cut ? new Vector2((at0.x - Centre.x) / scale + (float)g.windowX, (at0.y - Centre.y) / scale + (float)g.windowY) : ToPlan(at0);
            // Under the loupe: what the next look finds shows sharp; the rest near it, blurred.
            foreach (CraftInclusion i in g.inclusions)
            {
                Vector2 at = cut ? CutPx(g, i.x, i.y) : ToPx(i.x, i.y);
                if (Vector2.Distance(at, at0) > (reach + i.size) * scale) continue;
                if (CraftLapidary.Seen(g, i, plan.x, plan.y, focus, reach))
                    c.Disc(at.x, at.y, Mathf.Max(2f, i.size * scale * 0.8f), i.kind == CraftInclusionKind.Feather ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.08f, 0.08f, 0.08f, 0.9f));
                else if (Mathf.Abs(i.depth - focus) < 0.3f)
                    c.Disc(at.x, at.y, Mathf.Max(3f, i.size * scale * 1.6f), new Color(1f, 1f, 1f, 0.15f));
            }
            // a thick crust under the loupe is opaque
            if (!cut)
            {
                double crust = CraftLapidary.CrustAt(g, plan.x, plan.y);
                if (crust > 0.2) c.Disc(at0.x, at0.y, reach * scale, new Color(0.38f, 0.28f, 0.18f, Mathf.Clamp01((float)crust) * 0.6f));
            }
            c.Ring(at0.x, at0.y, reach * scale, 3f, Iron);
        }
        // The focus through the stone's depth (the wheel), and how much of it has been searched.
        c.Rect(430, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, 440, 352, "FOCUS: WHEEL", CraftArt.LabelInk);
        CraftArt.Label(c, 470, 330, "TABLE", CraftArt.LabelDim);
        CraftArt.Label(c, 470, 64, "CULET", CraftArt.LabelDim);
        Meter(c, 446, 60, 16, 280, 1f - focus, new Color(0.6f, 0.85f, 1f));
        float covered = Coverage(p);
        CraftArt.Label(c, 540, 330, "SEARCHED", CraftArt.LabelInk);
        CraftArt.Label(c, 540, 312, $"{covered * 100:0}%", CraftArt.LabelInk);
        Meter(c, 560, 60, 16, 240, covered, Mark, (0.7f, 1f, GoodBand));
        CraftArt.Label(c, 600, 290, "LOUPE", CraftArt.LabelInk);
        CraftArt.Label(c, 600, 272, "REACH", CraftArt.LabelInk);
        Meter(c, 610, 60, 16, 200, Mathf.Clamp01((reach - 0.08f) / 0.18f), new Color(0.45f, 0.9f, 0.45f));
    }

    /// <summary>
    /// The share of the stone - every place and every step of its depth - the loupe has truly gone over in this visit (the
    /// strokes sent, and this one so far): only where it finds what lies there (<see cref="CraftLapidary.MarkSearched"/>).
    /// </summary>
    private float Coverage(CraftPiece p) => searchedFor != p.id ? 0f : (float)CraftLapidary.SearchedShare(p.gem, searched, strokeSearched);

    /// <summary>The search kept belongs to this stone (another stone picked starts it afresh).</summary>
    private void SearchFor(CraftPiece p)
    {
        if (searchedFor == p.id) return;
        Array.Clear(searched, 0, searched.Length);
        Array.Clear(strokeSearched, 0, strokeSearched.Length);
        searchedFor = p.id;
    }

    private void EvaluatePointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Stone;
        if (phase == CraftPointer.Scroll) { focus = Mathf.Clamp01(focus + (scroll > 0 ? -0.06f : 0.06f)); return; }
        if (phase == CraftPointer.Up || phase == CraftPointer.Cancel) { FlushLooks(); return; } // the stroke ends: its looks go with its steadiness
        if (phase != CraftPointer.Down && phase != CraftPointer.Drag) return; // only while held: hovering looks at nothing
        GemState g = p.gem;
        bool cut = g.stage >= CraftGemStage.Faceted && g.oriented;
        if (Vector2.Distance(pos, Centre) > (cut ? CutRadius : StoneRadius)) return;
        if (phase == CraftPointer.Down || looksFor != p.id)
        {
            // a stroke begins (or, begun off the stone, comes onto it)
            if (!Station.HasInstrument(CraftInstruments.Loupe)) { if (phase == CraftPointer.Down) said = "There is no loupe here."; return; }
            FlushLooks();
            looksFor = p.id;
            lastLook = pos;
            lastLookTime = ui.PointerTime - 1f;
        }
        float now = ui.PointerTime;
        if (Vector2.Distance(pos, lastLook) < 10f && now - lastLookTime < 0.25f) return;
        // A look: where the loupe was, at the focus, as steady as the hand held it.
        float speedPx = phase == CraftPointer.Down ? 0f : Vector2.Distance(pos, lastLook) / Mathf.Max(0.001f, now - lastLookTime);
        float scale = cut ? CutRadius / Mathf.Max(0.05f, (float)CraftLapidary.WindowRadius(g)) : StoneRadius;
        Vector2 shaken = Shaken(pos);
        Vector2 plan = cut ? new Vector2((shaken.x - Centre.x) / scale + (float)g.windowX, (shaken.y - Centre.y) / scale + (float)g.windowY) : ToPlan(shaken);
        looks.Add(plan.x); looks.Add(plan.y); looks.Add(focus);
        steadySum += Mathf.Clamp01(1f - speedPx / 900f);
        steadyN++;
        // what this stroke has gone over so far, at its steadiness so far (counted for good when the stroke is sent)
        SearchFor(p);
        CraftLapidary.MarkSearched(g, strokeSearched, plan.x, plan.y, focus, CraftLapidary.LoupeReach(SteadyNow, Station.ToolQuality));
        lastLook = pos;
        lastLookTime = now;
        if (looks.Count >= 510) FlushLooks();
    }

    private void FlushLooks()
    {
        CraftPiece p = looksFor != null ? Ws.Piece(looksFor) : null;
        var pts = new List<float>(looks); // x, y and the focus of each look
        float steady = SteadyNow;
        looks.Clear();
        steadySum = 0f;
        steadyN = 0;
        Array.Clear(strokeSearched, 0, strokeSearched.Length);
        if (p == null || pts.Count < 3) return;
        // the stroke's looks are searched as the station takes them: each at the steadiness the stroke is sent with
        SearchFor(p);
        double reach = CraftLapidary.LoupeReach(steady, Station.ToolQuality);
        for (int k = 0; k + 2 < pts.Count; k += 3) CraftLapidary.MarkSearched(p.gem, searched, pts[k], pts[k + 1], pts[k + 2], reach);
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.EvaluateGem, id) { points = pts, a = steady }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(id) : null;
            if (q != null) SayFor(id, $"{LastRecord(id)}. Judged worth about {q.gem.estimate:0} as it is.");
        });
    }

    // ------------------------------------------------------------------ orienting
    private string CutChosen => cuts.Count > 0 && cutName != null ? cuts[Mathf.Clamp(cutName.value, 0, cuts.Count - 1)] : null;

    private void DrawOrient(CraftPixels c, CraftPiece p)
    {
        if (orientFor != p.id)
        {
            orientFor = p.id;
            window = p.gem.oriented ? new Vector2((float)p.gem.windowX, (float)p.gem.windowY) : Vector2.zero;
            windowR = p.gem.oriented ? (float)p.gem.windowR : 0.6f;
            if (p.gem.oriented)
            {
                SetSlider(tableTilt, (float)p.gem.tableAngle);
                int ci = cuts.IndexOf(p.gem.cut);
                if (ci >= 0 && cutName != null) cutName.SetValueWithoutNotify(ci);
            }
        }
        DrawStone(c, p, true, Station.Reveals);
        // The finished stone where it would sit: its outline as long as the cut, as big as the rough and its faces allow;
        // a faint ring for the size asked, where the rough cuts it smaller.
        CraftGemGrade grade = Preview(p);
        Vector2 w = ToPx(window.x, window.y);
        if (previewGem != null)
        {
            var outline = new List<Vector2>();
            for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
            {
                float t = k * Mathf.PI * 2f / CraftLapidary.ProfileRadii, r = (float)CraftLapidary.TargetRadius(previewGem, k) * StoneRadius;
                outline.Add(new Vector2(w.x + Mathf.Cos(t) * r, w.y + Mathf.Sin(t) * r));
            }
            c.Outline(outline, Mark, 2f);
            if (CraftLapidary.WindowRadius(previewGem) < windowR - 0.01f) c.Ring(w.x, w.y, windowR * StoneRadius, 1f, new Color(1f, 0.8f, 0.38f, 0.3f));
        }
        // The table's tilt (gold) against the axis the dichroscope showed (blue) and the cleavage the loupe showed (red).
        Rect gauge = new Rect(430, 230, 260, 140);
        c.Rect(gauge.xMin, gauge.yMin, gauge.xMax, gauge.yMax, new Color(0.2f, 0.19f, 0.18f));
        Vector2 o = new Vector2(gauge.xMin + 20, gauge.yMax - 30);
        void Ray(float deg, Color col, float width) => c.Line(o.x, o.y, o.x + 200 * Mathf.Cos(deg * Mathf.Deg2Rad), o.y - 90 * Mathf.Sin(deg * Mathf.Deg2Rad), col, width);
        float tilt = tableTilt != null ? tableTilt.value : 0f;
        Color blue = new Color(0.6f, 0.85f, 1f), red = Harm;
        CraftArt.Label(c, gauge.xMin + 8, gauge.yMax - 14, "TABLE", Mark);
        if (p.gem.readAxis >= 0)
        {
            Ray((float)p.gem.readAxis, blue, 1.5f);
            CraftArt.Label(c, gauge.xMin + 64, gauge.yMax - 14, "AXIS", blue);
        }
        CraftGemDefinition d = GemOf(p);
        if ((p.gem.looked || Station.Reveals) && d != null && d.cleavage >= CraftCleavage.Good)
        {
            // within 8° of the cleavage the table flakes: the red wedge
            float cl = (float)p.gem.cleaveTilt;
            Ray(cl - 8f, new Color(red.r, red.g, red.b, 0.35f), 1f);
            Ray(cl + 8f, new Color(red.r, red.g, red.b, 0.35f), 1f);
            Ray(cl, red, 1.5f);
            CraftArt.Label(c, gauge.xMin + 110, gauge.yMax - 14, "CLEAVAGE", red);
        }
        Ray(tilt, Mark, 3f);
        CraftArt.Label(c, gauge.xMin + 8, gauge.yMin + 8, "GOLD ON BLUE, OFF RED", CraftArt.LabelDim);
        // What the cut would give, judged on what is known of the stone.
        c.Rect(430, 60, 690, 210, new Color(0.2f, 0.19f, 0.18f));
        if (grade != null)
        {
            CraftArt.Label(c, 440, 190, "EXPECTED", CraftArt.LabelInk);
            CraftArt.Label(c, 440, 170, $"{grade.Yield:0.00} CT", CraftArt.LabelInk);
            CraftArt.Label(c, 440, 150, $"WORTH {grade.Value:0}", CraftArt.LabelInk);
            CraftArt.Label(c, 440, 98, "QUALITY", CraftArt.LabelInk);
            Meter(c, 440, 80, 240, 10, Mathf.Clamp01((float)grade.Quality), new Color(0.45f, 0.9f, 0.45f));
        }
    }

    /// <summary>
    /// What the stone would be, cut this way: judged on what the player knows of it - the axis as the dichroscope read it
    /// (unread, the colour is not judged), the cleavage only once the loupe has shown it, the inclusions found - and
    /// remembered until something changes.
    /// </summary>
    private CraftGemGrade Preview(CraftPiece p)
    {
        string cut = CutChosen;
        if (cut == null) return null;
        float tilt = tableTilt != null ? tableTilt.value : 0f;
        string key = $"{p.id}:{p.record.Count}:{cut}:{window.x:0.000}:{window.y:0.000}:{windowR:0.000}:{tilt:0}";
        if (key == previewKey) return previewGrade;
        previewKey = key;
        CraftPiece copy = p.Clone();
        GemState g = copy.gem;
        g.oriented = true;
        g.cut = cut;
        g.cutLength = Math.Max(1, Rules.Cut(cut)?.lengthRatio ?? 1);
        g.windowX = window.x;
        g.windowY = window.y;
        g.windowR = windowR;
        g.tableAngle = tilt;
        g.axis = p.gem.readAxis >= 0 ? p.gem.readAxis : tilt;
        if (!(p.gem.looked || Station.Reveals)) g.cleaveTilt = tilt + 45;
        previewGem = g;
        previewGrade = CraftLapidary.Grade(Rules, copy, !Station.Reveals);
        return previewGrade;
    }

    private void OrientPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        if (phase == CraftPointer.Scroll) { windowR = Mathf.Clamp(windowR + (scroll > 0 ? 0.02f : -0.02f), 0.15f, 1f); return; }
        if (phase == CraftPointer.Cancel) { if (draggingWindow) window = dragFrom; draggingWindow = false; return; } // put back where it was
        if (phase == CraftPointer.Down && Vector2.Distance(pos, ToPx(window.x, window.y)) < Mathf.Max(20f, windowR * StoneRadius)) { draggingWindow = true; dragFrom = window; return; }
        if (phase == CraftPointer.Drag && draggingWindow) { window = ToPlan(pos); if (window.magnitude > 0.9f) window = window.normalized * 0.9f; return; }
        if (phase == CraftPointer.Up) draggingWindow = false;
    }

    private void MarkOut()
    {
        CraftPiece p = Stone;
        string cut = CutChosen;
        if (p == null || cut == null) { said = "Pick a stone (left)."; return; }
        if (p.gem.profile.Count == CraftLapidary.ProfileRadii) { said = "It is ground to its planned outline: the plan is fixed."; return; }
        if (p.gem.facets.Count > 0 || p.gem.stage >= CraftGemStage.Faceted) { said = "It is already being cut: the plan is fixed."; return; }
        string id = p.id, name = Rules.Cut(cut)?.Name ?? "cut";
        Send(new CraftRequest(CraftRequestKind.OrientGem, id) { id = cut, a = tableTilt.value, b = window.x, c = window.y, d = windowR }, (ok, why) =>
        {
            if (ok) SayFor(id, $"Marked out for {CraftText.A(name)} {name.ToLowerInvariant()}: {LastRecord(id).ToLowerInvariant()}.");
        });
    }

    // ------------------------------------------------------------------ splitting and sawing
    private bool Snap => technique != null && technique.value == 1;
    private int SawStrokesNeeded => Mathf.RoundToInt(Mathf.Lerp(8f, 4f, sawPressure != null ? sawPressure.value : 0.35f));

    /// <summary>Does the player know whether the stone cleaves (the loupe showed its cleavage lines, or the stone is known)?</summary>
    private bool CleavageKnown(CraftPiece p) => p != null && (p.gem.looked || Known(p));

    /// <summary>On the Split place, the technique the stone's known cleavage calls for.</summary>
    private void PickTechnique(CraftPiece p)
    {
        CraftGemDefinition d = GemOf(p);
        if (technique == null || d == null || !CleavageKnown(p)) return;
        technique.SetValueWithoutNotify(d.cleavage >= CraftCleavage.Good ? 0 : 1);
        barFor = "";
    }

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
            else bar.NewTargets(p.gem.seed, 500 + tier * 16 + facetHits.Count);
        }
        return bar;
    }

    private void DrawCut(CraftPixels c, CraftPiece p)
    {
        DrawStone(c, p, true, Station.Reveals);
        if (p.gem.oriented)
        {
            // the planned stone: the line must leave it whole
            Vector2 w = ToPx(p.gem.windowX, p.gem.windowY);
            var outline = new List<Vector2>();
            for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
            {
                float t = k * Mathf.PI * 2f / CraftLapidary.ProfileRadii, r = (float)CraftLapidary.TargetRadius(p.gem, k) * StoneRadius;
                outline.Add(new Vector2(w.x + Mathf.Cos(t) * r, w.y + Mathf.Sin(t) * r));
            }
            c.Outline(outline, new Color(1f, 0.8f, 0.38f, 0.6f), 1.5f);
        }
        c.Rect(410, 40, 700, 300, new Color(0.2f, 0.19f, 0.18f));
        if (lining || lineSet)
        {
            // a line being drawn shows red while it would miss what is left of the stone (it is not taken)
            bool misses = lining && Vector2.Distance(lineA, lineB) >= StoneRadius * 0.8f && !LineCutsStone(p);
            c.Line(lineA.x, lineA.y, lineB.x, lineB.y, lineSet ? Mark : misses ? Harm : new Color(1f, 1f, 1f, 0.6f), lineSet ? 2f : 1f);
            if (place == Place.Saw && lineSet)
            {
                // the blade as far along the line as the strokes have taken it
                Vector2 at = Vector2.Lerp(lineA, lineB, Mathf.Clamp01(sawStrokes / (float)SawStrokesNeeded));
                c.Disc(at.x, at.y, 8f, new Color(0.75f, 0.76f, 0.8f));
                c.Line(lineA.x, lineA.y, at.x, at.y, new Color(0.15f, 0.15f, 0.15f), 3f);
            }
        }
        if (place == Place.Saw)
        {
            // the feed: past the red mark a brittle stone chips at the end of the cut
            float feed = sawPressure != null ? sawPressure.value : 0.35f;
            CraftArt.Label(c, 420, 280, $"FEED {feed * 100:0}%", CraftArt.LabelInk);
            Meter(c, 420, 262, 270, 10, feed, new Color(0.6f, 0.85f, 1f), ((float)CraftLapidary.SawChipPressure, 1f, HarmBand));
            CraftArt.Label(c, 420, 240, "RED: A BRITTLE STONE CHIPS", CraftArt.LabelDim);
            CraftArt.Label(c, 420, 210, drip ? "DRIP ON: COOL" : "DRIP OFF: IT HEATS", drip ? CraftArt.LabelDim : Fair);
        }
        if (lineSet)
        {
            DrawTimingBar(c, BarFor(p), BarRect.xMin, BarRect.yMin, BarRect.width, BarRect.height, lastHit);
            if (place == Place.Split)
            {
                CraftArt.Label(c, BarRect.xMin, 372, "LIGHT", CraftArt.LabelDim);
                CraftArt.Label(c, BarRect.xMax - CraftArt.LabelWidth("HARD"), 372, "HARD", CraftArt.LabelDim);
                CraftArt.Label(c, BarRect.xMin, 305, Snap ? "THE SNAP: STRIKE ON WHITE" : "THE BLOW: STRIKE ON WHITE", CraftArt.LabelInk);
            }
            else
            {
                // each stroke so far, as it was judged, and how far through the cut
                CraftArt.Label(c, BarRect.xMin, 305, $"STROKE {sawStrokes + 1}/{SawStrokesNeeded}", CraftArt.LabelInk);
                for (int k = 0; k < sawHits.Count; k++)
                    c.Rect(BarRect.xMin + 120 + k * 18f, 304, BarRect.xMin + 120 + k * 18f + 14f, 316, GradeColor(sawHits[k].grade));
            }
        }
        else CraftArt.Label(c, 420, 305, "DRAW THE LINE ACROSS", CraftArt.LabelInk);
        if (!drip && place == Place.Saw) c.Halo(Centre.x, Centre.y, StoneRadius * 0.5f, new Color(1f, 0.5f, 0.2f), lineSet ? 0.25f : 0f);
    }

    /// <summary>The line in the plan: its direction (°) and its signed distance from the centre - where it was drawn, whichever way (<see cref="CraftLapidary.LineThrough"/>).</summary>
    private void LineOf(out float angle, out float offset)
    {
        Vector2 a = ToPlan(lineA), b = ToPlan(lineB);
        CraftLapidary.LineThrough(a.x, a.y, b.x, b.y, out double an, out double of);
        angle = (float)an;
        offset = (float)of;
    }

    /// <summary>Does the line drawn run through what is left of the stone (the workshop refuses one that misses it)?</summary>
    private bool LineCutsStone(CraftPiece p)
    {
        LineOf(out float angle, out float offset);
        return Mathf.Abs(offset) < 0.97f && CraftLapidary.LineCuts(p.gem, angle, offset);
    }

    private void CutPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (phase == CraftPointer.Cancel)
        {
            if (sawStrokes > 0) said = "The blade is lifted out: that cut is abandoned.";
            else if (lineSet || lining) said = "The line is rubbed out.";
            DropLine();
            return;
        }
        if (p.gem.stage >= CraftGemStage.Faceted || p.gem.facets.Count > 0) { if (phase == CraftPointer.Down) said = "It is already being faceted: it is cut on the lap now."; return; }
        if (lineSet && lineFor != p.id) DropLine();
        if (!lineSet)
        {
            if (phase == CraftPointer.Down) { lining = true; lineFor = p.id; lineA = lineB = pos; return; }
            if (!lining) return;
            if (phase == CraftPointer.Drag) { lineB = pos; return; }
            if (phase != CraftPointer.Up) return;
            lining = false;
            if (Vector2.Distance(lineA, lineB) < StoneRadius * 0.8f) { said = "Draw the line right across the stone."; return; }
            if (!LineCutsStone(p)) { said = "That line misses the stone: draw it across what is left of it."; return; }
            lineSet = true;
            sawHits.Clear();
            sawStrokes = 0;
            lastHit = null;
            barFor = "";
            said = place == Place.Split
                ? (Snap ? "Nicked along the line. Strike when the marker is on the white line: that is the blow that snaps it." : "The chisel is set on the line. Strike when the marker is on the white line: the force that parts it.")
                : $"Now saw: click for each stroke when the marker is on a target - {SawStrokesNeeded} strokes. The narrow target gives most, the wide one is safer.";
            return;
        }
        if (phase != CraftPointer.Down) return; // the strike acts on the press: its drag and release do nothing
        CraftTimingBar b = BarFor(p);
        CraftTimingHit hit = b.Hit(Hands);
        lastHit = hit;
        string id = p.id;
        if (place == Place.Split)
        {
            LineOf(out float angle, out float offset);
            DropLine();
            if (hit.position < 0.3f) { said = "Too light: it rang, but did not part."; }
            Send(new CraftRequest(CraftRequestKind.CleaveGem, id) { a = angle, b = offset, c = Mathf.Clamp01(hit.position), i0 = Snap ? 1 : 0 }, (ok, why) =>
            {
                if (!ok) return;
                bool shattered = false;
                foreach (CraftEvent e in AnswerEvents) if (e.Id == "shattered") shattered = true;
                string verdict = LastRecord(id);
                SayFor(id, $"{verdict}.");
                if (shattered) ui.Message($"{verdict}: {(Snap ? "a crack ran in from the break" : "off its plane, or struck too hard, it shattered")}.");
            });
            lastHit = hit;
            return;
        }
        // a stroke of the saw
        sawHits.Add(hit);
        sawStrokes++;
        said = $"Stroke {sawStrokes}/{SawStrokesNeeded}: {HitWords(hit, "the blade jumped")}";
        b.NewTargets(p.gem.seed, 300 + sawStrokes, true);
        Hands.Move(0.08);
        if (sawStrokes < SawStrokesNeeded) return;
        LineOf(out float ang, out float ofs);
        float sum = 0f;
        int misses = 0;
        foreach (CraftTimingHit h in sawHits) { sum += h.score; if (h.damage) misses++; }
        float steady = sum / sawHits.Count;
        float feed = sawPressure != null ? sawPressure.value : 0.35f;
        bool cool = drip;
        DropLine();
        Send(new CraftRequest(CraftRequestKind.SawGem, id) { a = ang, b = ofs, c = steady, d = cool ? 1f : 0f, i0 = Mathf.RoundToInt(feed * 1000f), i1 = misses }, (ok, why) =>
        {
            if (ok) SayFor(id, $"{LastRecord(id)}{(misses > 0 ? $": the blade jumped {misses} time{(misses == 1 ? "" : "s")}" : "")}.");
        });
    }

    // ------------------------------------------------------------------ grinding
    private List<float> ProfileOf(GemState g) => g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);

    /// <summary>The scale (pixels to the rough's radius) the grind view draws at: the stone's widest reach always fits.</summary>
    private static float GrindScale(List<float> prof)
    {
        float most = 0.1f;
        foreach (float r in prof) most = Mathf.Max(most, r);
        return Mathf.Min(StoneRadius, 165f / most);
    }

    /// <summary>A point of the stone at plan angle <paramref name="t"/> (rad) and radius <paramref name="r"/>, turned on the dop and drawn round <paramref name="o"/>.</summary>
    private Vector2 Turned(Vector2 o, float t, float r, float scale) => new Vector2(o.x + Mathf.Cos(t + turn * Mathf.Deg2Rad) * r * scale, o.y + Mathf.Sin(t + turn * Mathf.Deg2Rad) * r * scale);

    /// <summary>Where the dop holds the stone: its edge at the wheel (against it while pressed, just off it otherwise).</summary>
    private Vector2 DopAt(List<float> prof, float scale)
    {
        float at = ((0f - turn) % 360f + 360f) % 360f / 360f * prof.Count;
        int k0 = Mathf.FloorToInt(at) % prof.Count;
        float edge = Mathf.Lerp(prof[k0], prof[(k0 + 1) % prof.Count], at - Mathf.Floor(at));
        return new Vector2(Wheel.x - edge * scale - (pressing ? 0f : 10f), Centre.y);
    }

    private void DrawGrind(CraftPixels c, CraftPiece p)
    {
        GemState g = p.gem;
        CraftGemDefinition d = GemOf(p);
        if (!g.oriented) { DrawStone(c, p, true, Station.Reveals); return; }
        List<float> prof = ProfileOf(g);
        int n = CraftLapidary.ProfileRadii;
        float scale = GrindScale(prof);
        Vector2 dop = DopAt(prof, scale);
        var stone = new List<Vector2>();
        var target = new List<Vector2>();
        for (int k = 0; k < n; k++)
        {
            float t = k * Mathf.PI * 2f / n;
            stone.Add(Turned(dop, t, prof[k], scale));
            target.Add(Turned(dop, t, (float)CraftLapidary.TargetRadius(g, k), scale));
        }
        Color col = StoneColour(d, g);
        c.Polygon(stone, col);
        c.Outline(stone, col * 0.6f, 2f);
        c.Disc(dop.x, dop.y, 5f, Iron); // the dop
        // The planned outline (gold); red where the wheel has gone into it.
        c.Outline(target, new Color(1f, 0.8f, 0.38f, 0.9f), 1.5f);
        for (int k = 0; k < n; k++)
        {
            float tr = (float)CraftLapidary.TargetRadius(g, k);
            if (prof[k] < tr - 0.01f)
            {
                float t = k * Mathf.PI * 2f / n;
                Vector2 a = Turned(dop, t, prof[k], scale), b = Turned(dop, t, tr, scale);
                c.Line(a.x, a.y, b.x, b.y, Harm, 3f);
            }
        }
        // The wheel (spinning: the streaks move), the drip.
        c.Disc(Wheel.x + WheelR, Wheel.y, WheelR, new Color(0.42f, 0.42f, 0.45f));
        for (int k = 0; k < 6; k++)
        {
            float a = Time.unscaledTime * 6f + k * Mathf.PI / 3f;
            c.Line(Wheel.x + WheelR, Wheel.y, Wheel.x + WheelR + Mathf.Cos(a) * WheelR * 0.9f, Wheel.y + Mathf.Sin(a) * WheelR * 0.9f, new Color(0.55f, 0.55f, 0.6f), 1f);
        }
        if (drip) c.Line(Wheel.x, Wheel.y + WheelR + 20f, Wheel.x, Wheel.y + 8f, Water, 2f);
        else c.Halo(Wheel.x, Wheel.y, 30f, new Color(1f, 0.5f, 0.2f), pressing ? 0.4f : 0f);
        if (pressing) c.Halo(Wheel.x, Wheel.y, 14f, new Color(1f, 0.85f, 0.5f), 0.5f + 0.5f * pressure);
        // The panel held to press: hard at its top, light at its bottom (the words down its left side), as at the smith's
        // wheel - the PRESS gauge rises with the hand; the heat the stone has taken (it never goes out of it), what is left
        // to grind - each meter named over it, the names apart.
        c.Rect(PressPanel.xMin, PressPanel.yMin, PressPanel.xMax, PressPanel.yMax, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, PressPanel.xMin + 6, PressPanel.yMax - 30, "HARD", CraftArt.LabelDim);
        CraftArt.Label(c, PressPanel.xMin + 6, PressPanel.yMin + 6, "LIGHT", CraftArt.LabelDim);
        void Column(float cx, string name, float value, Color fill, params (float, float, Color)[] bands)
        {
            CraftArt.Label(c, cx - CraftArt.LabelWidth(name) * 0.5f, PressPanel.yMax - 14, name, CraftArt.LabelInk);
            Meter(c, cx - 7, PressPanel.yMin + 22, 14, PressPanel.height - 44, value, fill, bands);
        }
        float shown = pressing ? pressure : ui.Hover.x >= PressPanel.xMin && PressPanel.Contains(ui.Hover) ? PressInPanel(ui.Hover.y) : 0f;
        float safe = drip ? 1f : (float)(CraftLapidary.GrindSafeTouch / 0.5);
        Column(PressPanel.xMin + 66, "PRESS", shown, new Color(0.45f, 0.9f, 0.45f), (safe, 1f, drip ? IdleBand : FairBand)); // without the drip, harder heats it
        float heat = Mathf.Clamp01((float)g.heat + pendingHeat);
        // the heat taken (a sensitive stone cracks at the red); what heats it now is the PRESS gauge's amber, without the drip
        Column(PressPanel.xMin + 112, "HEATED", heat, heat >= CraftLapidary.GrindCrackHeat ? Harm : new Color(1f, 0.8f, 0.4f),
            d != null && d.heatSensitive ? ((float)CraftLapidary.GrindCrackHeat, 1f, HarmBand) : (0f, 0f, HarmBand));
        Column(PressPanel.xMin + 154, "LEFT", (float)CraftLapidary.Excess(g), new Color(0.6f, 0.85f, 1f), (0f, 0.15f, GoodBand));
    }

    /// <summary>The pressure the hand puts on the wheel at <paramref name="y"/> in the press panel: higher presses harder (as at the smith's wheel).</summary>
    private static float PressInPanel(float y) => Mathf.Clamp01((y - PressPanel.yMin) / PressPanel.height);

    private void GrindPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = Stone;
        if (phase == CraftPointer.Up || phase == CraftPointer.Cancel)
        {
            // let go, or lifted off the wheel: what was ground is ground
            if (pressing) { pressing = false; FlushGrind(); }
            return;
        }
        if (phase != CraftPointer.Down) return;
        if (!p.gem.oriented) { said = "Mark the stone out first (Orient): the wheel needs an outline to grind to."; return; }
        if (p.gem.stage >= CraftGemStage.Faceted || p.gem.facets.Count > 0) { said = "It is already being faceted: the lap takes it from here."; return; }
        if (p.gem.profile.Count == 0 && CraftLapidary.Excess(p.gem) > CraftLapidary.GrindableExcess) { said = $"Saw the bulk of the rough off first: more than {CraftLapidary.GrindableExcess * 100:0}% lies outside the planned stone."; return; }
        if (PressPanel.Contains(pos) || Vector2.Distance(pos, Wheel + new Vector2(WheelR, 0f)) < WheelR)
        {
            pressing = true;
            grindFor = p.id;
            grindTick = 0f;
        }
    }

    private void FlushGrind()
    {
        CraftPiece p = grindFor != null ? Ws.Piece(grindFor) : null;
        var pts = new List<float>(touches);
        // steady pressure grinds a true outline
        float mean = 0f, sq = 0f;
        foreach (float v in pressures) mean += v;
        mean /= Mathf.Max(1, pressures.Count);
        foreach (float v in pressures) sq += (v - mean) * (v - mean);
        float steady = Mathf.Clamp01(1f - Mathf.Sqrt(sq / Mathf.Max(1, pressures.Count)) * 3f);
        touches.Clear();
        pressures.Clear();
        pendingHeat = 0f;
        if (p == null || pts.Count < 2) return;
        string id = p.id;
        double over = p.gem.overground, heat = p.gem.heat;
        Send(new CraftRequest(CraftRequestKind.GrindGem, id) { points = pts, a = drip ? 1f : 0f, b = steady }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(id) : null;
            if (q == null) return;
            double excess = CraftLapidary.Excess(q.gem);
            string words = q.gem.overground > over + 0.005 ? "The wheel is cutting into the planned stone: ease off there."
                : q.gem.heat >= CraftLapidary.GrindCrackHeat && heat < CraftLapidary.GrindCrackHeat ? "It got hot against the dry wheel - drip on, or press lighter."
                : excess <= 0.15 ? "Ground to its outline: facet it." : $"Grinding - {excess * 100:0}% still to take off.";
            SayFor(id, words);
        });
    }

    // ------------------------------------------------------------------ faceting
    /// <summary>Facets cut on the bar for a tier (each hit cuts its share of the tier's facets).</summary>
    private int HitsFor(CraftCutTier t) => CraftMastery.FacetHits(t.facets, Mastery, MasterySettings);

    /// <summary>The refractive index the player has for the cutting chart: read on the refractometer, or the stone's own once it is known; -1 none.</summary>
    private double ChartRI(CraftPiece p)
    {
        if (p.gem.readRI >= 0) return p.gem.readRI;
        return Known(p) ? GemOf(p)?.refractiveIndex ?? -1 : -1;
    }

    /// <summary>Is the bar shown: a tier chosen of a stone that can still be cut?</summary>
    private bool FacetBarShown(CraftPiece p)
    {
        CraftGemCut cut = CutOf(p);
        return p != null && cut != null && tier >= 0 && tier < cut.tiers.Count && p.gem.stage < CraftGemStage.Polished;
    }

    /// <summary>A tier's state in words, for the menu.</summary>
    private static string TierState(GemState g, int t, int next)
    {
        CraftFacetRecord f = g.facets.Find(x => x.tier == t);
        if (f == null) return t == next ? "next" : "to cut";
        if (f.misses > 0) return $"{f.misses} botched: recut it";
        if (f.asymmetry > 0.35) return "uneven";
        if (Math.Abs(f.angleError) > 3) return "off its angle";
        return Math.Abs(f.angleError) <= 1 && Math.Abs(f.meet) < 0.25 ? "cut true" : "cut";
    }

    /// <summary>The tier menu as the stone now stands (rebuilt when its tiers change), on the next tier to cut unless one was picked.</summary>
    private void RefreshTiers(CraftPiece p)
    {
        if (tierPick == null) return;
        CraftGemCut cut = CutOf(p);
        string key = p == null || cut == null ? "" : $"{p.id}:{cut.id}:{p.gem.facets.Count}:{string.Join(",", p.gem.facets.ConvertAll(f => $"{f.tier}/{f.misses}/{f.asymmetry:0.00}/{f.angleError:0.0}"))}";
        if (key == tiersKey) return;
        bool sameStone = tiersKey.Length > 0 && key.Length > 0 && tiersKey.Split(':')[0] == key.Split(':')[0];
        tiersKey = key;
        var names = new List<string>();
        if (cut != null)
        {
            int next = Mathf.Min(p.gem.facets.Count, cut.tiers.Count - 1);
            for (int t = 0; t < cut.tiers.Count; t++) names.Add($"{t + 1}. {cut.tiers[t].name} - {TierState(p.gem, t, p.gem.facets.Count)}");
            if (!sameStone || tier < 0 || tier >= cut.tiers.Count) tier = p.gem.facets.Count < cut.tiers.Count ? next : -1;
        }
        else tier = -1;
        // every tier cut: the menu says so, and picking a tier there recuts it
        if (cut != null && tier < 0) names.Add("Every tier is cut - pick one to recut it");
        tierPick.ClearOptions();
        tierPick.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        tierPick.SetValueWithoutNotify(tier >= 0 ? tier : Mathf.Max(0, names.Count - 1));
    }

    private void PickTier(int value)
    {
        CraftPiece p = Stone;
        CraftGemCut cut = CutOf(p);
        if (cut == null) return;
        if (value >= cut.tiers.Count) { tier = -1; facetHits.Clear(); retryFacet = null; return; }
        tier = Mathf.Clamp(value, 0, cut.tiers.Count - 1);
        facetHits.Clear();
        retryFacet = null;
        lastHit = null;
        barFor = "";
        said = p.gem.facets.Exists(f => f.tier == tier) ? $"The {cut.tiers[tier].name.ToLowerInvariant()} is cut: cutting it again recuts it (a little weight lost)." : "";
    }

    private void DrawFacet(CraftPixels c, CraftPiece p)
    {
        if (facetFor != p.id) { facetFor = p.id; facetHits.Clear(); retryFacet = null; }
        RefreshTiers(p);
        CraftGemCut cut = CutOf(p);
        CraftGemDefinition d = GemOf(p);
        DrawProfile(c, p, cut, d);
        // The lap spinning.
        c.Rect(Lap.xMin, Lap.yMin, Lap.xMax, Lap.yMax, new Color(0.3f, 0.3f, 0.32f));
        c.Ring(Lap.center.x, Lap.center.y, 50f, 4f, new Color(0.5f, 0.5f, 0.55f));
        for (int k = 0; k < 4; k++)
        {
            float a = Time.unscaledTime * 5f + k * Mathf.PI / 2f;
            c.Line(Lap.center.x, Lap.center.y, Lap.center.x + Mathf.Cos(a) * 46f, Lap.center.y + Mathf.Sin(a) * 46f, new Color(0.45f, 0.45f, 0.5f), 1f);
        }
        CraftArt.Label(c, Lap.xMin + 6, Lap.yMin + 6, "LAP: CLICK TO CUT", CraftArt.LabelInk);
        // The index wheel: a stop lit for every facet cut so far in this tier.
        bool shown = FacetBarShown(p);
        int needed = shown ? HitsFor(cut.tiers[tier]) : 8;
        c.Disc(IndexWheel.center.x, IndexWheel.center.y, 50f, Iron);
        for (int k = 0; k < needed; k++)
        {
            float a = k * Mathf.PI * 2f / needed;
            Color sc = k < facetHits.Count ? GradeColor(facetHits[k].grade) : k == facetHits.Count ? Mark : new Color(0.6f, 0.6f, 0.62f);
            c.Disc(IndexWheel.center.x + Mathf.Cos(a) * 42f, IndexWheel.center.y + Mathf.Sin(a) * 42f, 4f, sc);
        }
        CraftArt.Label(c, IndexWheel.center.x - CraftArt.LabelWidth("INDEX") / 2f, IndexWheel.center.y - 5, "INDEX", CraftArt.LabelDim);
        // The protractor: a quarter arc with a tick every 10°, the chart's angle for the tier, the quill's angle on it.
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
        if (shown && ChartAngle(p, cut.tiers[tier], out double chart))
        {
            float a = (float)chart * Mathf.Deg2Rad;
            c.Line(Pivot.x + Mathf.Sin(a) * (Arc - 22f), Pivot.y + Mathf.Cos(a) * (Arc - 22f), Pivot.x + Mathf.Sin(a) * (Arc + 6f), Pivot.y + Mathf.Cos(a) * (Arc + 6f), CraftArt.TargetColor, 3f);
            CraftArt.Label(c, 600, 300, $"CHART {chart:0.0}°", CraftArt.TargetColor);
        }
        float qa = quill * Mathf.Deg2Rad;
        c.Line(Pivot.x, Pivot.y, Pivot.x + Mathf.Sin(qa) * Arc, Pivot.y + Mathf.Cos(qa) * Arc, Mark, 3f);
        c.Disc(Pivot.x, Pivot.y, 5f, Iron);
        CraftArt.Label(c, 600, 282, $"QUILL {quill:0.0}°", Mark);
        if (shown)
        {
            DrawTimingBar(c, BarFor(p), BarRect.xMin, BarRect.yMin, BarRect.width, BarRect.height, lastHit);
            CraftArt.Label(c, 600, 264, $"FACET {Mathf.Min(facetHits.Count + 1, needed)}/{needed}", CraftArt.LabelInk);
        }
        if (!drip) c.Halo(230f, 200f, 40f, new Color(1f, 0.5f, 0.2f), facetHits.Count > 0 ? 0.4f : 0f);
    }

    /// <summary>The angle the chart gives a tier: the pavilion's and crown's need the stone's refractive index; the girdle, table, base and dome have their own.</summary>
    private bool ChartAngle(CraftPiece p, CraftCutTier t, out double angle)
    {
        angle = 0;
        double ri = ChartRI(p);
        bool needsRI = t.kind == CraftCutTierKind.Pavilion || t.kind == CraftCutTierKind.Crown;
        if (needsRI && ri <= 0) return false;
        angle = CraftLapidary.IdealAngle(t, needsRI ? ri : 1.5);
        return true;
    }

    /// <summary>
    /// The stone in profile on its dop: the crown above the girdle, the pavilion below, each at the angle it was cut at
    /// (its chart angle, dim, until it is cut); a cabochon's base and dome.
    /// </summary>
    private void DrawProfile(CraftPixels c, CraftPiece p, CraftGemCut cut, CraftGemDefinition d)
    {
        Vector2 o = new Vector2(220f, 200f);
        const float W = 110f;
        Color col = StoneColour(d, p.gem), dim = new Color(1f, 1f, 1f, 0.25f);
        c.Rect(o.x - 4, 30, o.x + 4, o.y - 100, Iron); // the dop
        if (cut == null)
        {
            CraftArt.Label(c, o.x - CraftArt.LabelWidth("MARK IT OUT FIRST") / 2f, o.y, "MARK IT OUT FIRST", CraftArt.LabelDim);
            return;
        }
        double ri = d != null ? d.refractiveIndex : 1.54;
        float Angle(CraftCutTierKind kind, double fallback)
        {
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                if (cut.tiers[t].kind != kind) continue;
                CraftFacetRecord f = p.gem.facets.Find(x => x.tier == t);
                return (float)(f != null ? CraftLapidary.IdealAngle(cut.tiers[t], ri) + f.angleError : fallback);
            }
            return (float)fallback;
        }
        bool Cut(CraftCutTierKind kind) => p.gem.facets.Exists(f => f.tier < cut.tiers.Count && cut.tiers[f.tier].kind == kind);
        bool Botched(CraftCutTierKind kind) => p.gem.facets.Exists(f => f.tier < cut.tiers.Count && cut.tiers[f.tier].kind == kind && f.misses > 0);
        if (cut.cabochon)
        {
            float dome = Mathf.Clamp(Angle(CraftCutTierKind.Dome, 38), 10f, 70f);
            float h = W * Mathf.Tan(dome * Mathf.Deg2Rad) * 0.6f;
            var arc = new List<Vector2> { new Vector2(o.x - W, o.y) };
            for (int k = 0; k <= 24; k++) { float a = Mathf.PI * k / 24f; arc.Add(new Vector2(o.x - Mathf.Cos(a) * W, o.y + Mathf.Sin(a) * h)); }
            if (Cut(CraftCutTierKind.Dome)) c.Polygon(arc, col); else c.Outline(arc, dim, 1.5f);
            c.Line(o.x - W, o.y, o.x + W, o.y, Cut(CraftCutTierKind.Base) ? col * 1.2f : dim, 3f);
            CraftArt.Label(c, o.x + W + 6, o.y + 10, "DOME", CraftArt.LabelDim);
            return;
        }
        float pav = Mathf.Clamp(Angle(CraftCutTierKind.Pavilion, CraftLapidary.IdealPavilion(ri)), 20f, 60f);
        float crown = Mathf.Clamp(Angle(CraftCutTierKind.Crown, CraftLapidary.IdealCrown(ri)), 15f, 55f);
        float culet = o.y - W * Mathf.Tan(pav * Mathf.Deg2Rad), table = 0.55f * W, top = o.y + (W - table) * Mathf.Tan(crown * Mathf.Deg2Rad);
        culet = Mathf.Max(culet, 40f);
        var pavilion = new List<Vector2> { new Vector2(o.x - W, o.y), new Vector2(o.x + W, o.y), new Vector2(o.x, culet) };
        var crownPts = new List<Vector2> { new Vector2(o.x - W, o.y), new Vector2(o.x - table, top), new Vector2(o.x + table, top), new Vector2(o.x + W, o.y) };
        Color red = Harm;
        if (Cut(CraftCutTierKind.Pavilion)) { c.Polygon(pavilion, col * 0.85f); c.Outline(pavilion, Botched(CraftCutTierKind.Pavilion) ? red : col * 0.6f, 2f); }
        else c.Outline(pavilion, dim, 1.5f);
        if (Cut(CraftCutTierKind.Crown)) { c.Polygon(crownPts, col); c.Outline(crownPts, Botched(CraftCutTierKind.Crown) ? red : col * 0.6f, 2f); }
        else c.Outline(crownPts, dim, 1.5f);
        // the facet lines of the tiers cut: from the girdle to the culet, from the girdle to the table
        for (int k = -2; k <= 2; k++)
        {
            if (Cut(CraftCutTierKind.Pavilion)) c.Line(o.x + k * W / 3f, o.y, o.x, culet, new Color(1f, 1f, 1f, 0.2f), 1f);
            if (Cut(CraftCutTierKind.Crown)) c.Line(o.x + k * W / 2.6f, o.y, o.x + k * table / 2.6f, top, new Color(1f, 1f, 1f, 0.2f), 1f);
        }
        c.Line(o.x - W - 2, o.y, o.x + W + 2, o.y, Cut(CraftCutTierKind.Girdle) ? new Color(1f, 1f, 1f, 0.6f) : dim, 2f);
        if (Cut(CraftCutTierKind.Table)) c.Line(o.x - table, top, o.x + table, top, new Color(1f, 1f, 1f, 0.7f), 2f);
        CraftArt.Label(c, o.x + W + 6, o.y + 14, $"CROWN {crown:0}°", CraftArt.LabelDim);
        CraftArt.Label(c, o.x + W + 6, o.y - 24, $"PAVILION {pav:0}°", CraftArt.LabelDim);
    }

    private void FacetPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Stone;
        CraftGemCut cut = CutOf(p);
        if (phase == CraftPointer.Scroll) { quill = Mathf.Clamp(quill + (scroll > 0 ? 0.5f : -0.5f), 0f, 90f); return; }
        if (phase == CraftPointer.Cancel)
        {
            if (facetHits.Count > 0) said = "Lifted off the lap: the facets of this tier are abandoned.";
            facetHits.Clear();
            lastHit = null;
            dialHeld = false;
            return;
        }
        if (phase == CraftPointer.Up) { dialHeld = false; return; }
        if (phase == CraftPointer.Drag) { if (dialHeld) quill = QuillAt(pos); return; }
        if (phase != CraftPointer.Down) return;
        if (cut == null) { said = "Mark the stone out first (Orient)."; return; }
        if (OnDial(pos) && !Lap.Contains(pos)) { quill = QuillAt(pos); dialHeld = true; return; }
        if (!(Lap.Contains(pos) || BarRect.Contains(pos) || IndexWheel.Contains(pos))) return;
        if (facetSending) { said = "The lap is still cutting the last tier."; return; }
        if (p.gem.stage >= CraftGemStage.Polished) { said = "It is polished: the lap would only spoil it."; return; }
        if (tier < 0 || tier >= cut.tiers.Count) { said = "Every tier is cut: polish it - or pick a tier in the menu to recut it."; return; }
        string id = p.id;
        if (retryFacet.HasValue && retryFacet.Value.piece == id && retryFacet.Value.i0 == tier) { SendTier(retryFacet.Value, cut.tiers[tier]); return; }
        if (facetHits.Count == 0)
        {
            // before the first facet: would the station cut this tier at all?
            if (!Ws.CanFacet(id, tier, out string why)) { said = why; return; }
            if (p.gem.facets.Exists(f => f.tier == tier) && !Confirm($"recut:{id}:{tier}", $"Click the lap again to recut the {cut.tiers[tier].name.ToLowerInvariant()}: it costs about 3% of the stone's weight."))
                return;
        }
        // One facet ground on the lap: judged on the bar.
        CraftTimingBar b = BarFor(p);
        CraftTimingHit hit = b.Hit(Hands);
        lastHit = hit;
        facetHits.Add(hit);
        CraftCutTier ct = cut.tiers[tier];
        int needed = HitsFor(ct);
        said = $"Facet {facetHits.Count}/{needed}: {HitWords(hit, "it cut through its neighbour")}";
        b.NewTargets(p.gem.seed, 500 + tier * 16 + facetHits.Count);
        Hands.Move(0.05);
        if (facetHits.Count < needed) return;
        // The tier as its facets came: their angle and meet off as far as the hits were, their unevenness (facets cut
        // steep then shallow are uneven, however well they average), the botched ones - the model's own rule.
        CraftLapidary.TierFromHits(facetHits, out double angle, out double index, out double meet, out double asym, out int misses);
        facetHits.Clear(); // taken out of the hands' buffer: sent once, kept for another try only if the station refuses it
        SendTier(new CraftRequest(CraftRequestKind.FacetGem, id) { i0 = tier, count = ct.facets, a = quill + (float)angle, b = (float)index, c = (float)meet, d = drip ? 1f : 0f, i1 = misses, i2 = Mathf.RoundToInt((float)asym * 1000f) }, ct);
    }

    /// <summary>Sends a tier of facets; the next tier is chosen in its answer (on a network client the copy shows the cut only then).</summary>
    private void SendTier(CraftRequest r, CraftCutTier ct)
    {
        string id = r.piece;
        int t = r.i0;
        retryFacet = null;
        facetSending = true;
        Send(r, (ok, why) =>
        {
            facetSending = false;
            if (!ok)
            {
                // refused by the station: the hits are kept, a click on the lap sends them again (an unknown outcome is not retried)
                if (why != CraftAnswerWaits.NoAnswer && Ws.Piece(id) != null) { retryFacet = r; SayFor(id, $"The {ct.name.ToLowerInvariant()} was not cut: click the lap to send it again once that is put right."); }
                return;
            }
            CraftPiece q = Ws.Piece(id);
            if (q == null) return;
            string record = LastRecord(id);
            if (ui.SelectedId == id)
            {
                tiersKey = ""; // the menu shows the tier's state, and moves on to the next tier
                tier = -1;
                RefreshTiers(q);
                CraftGemCut cut = CutOf(q);
                said = $"{record}." + (q.gem.stage >= CraftGemStage.Faceted ? " Every tier is cut: polish it." : cut != null && tier >= 0 ? $" Next: the {cut.tiers[tier].name.ToLowerInvariant()}." : "");
            }
        });
    }

    // ------------------------------------------------------------------ polishing
    /// <summary>The pressure a point of the PRESS gauge stands for (its foot light, its top hard).</summary>
    private static float PressOnGauge(float y) => Mathf.Clamp01((y - PressGauge.yMin - 1f) / (PressGauge.height - 2f));

    /// <summary>The compound picked for the lap, or null when there is none.</summary>
    private string CompoundChosen => compounds.Count > 0 && compound != null ? compounds[Mathf.Clamp(compound.value, 0, compounds.Count - 1)] : null;

    /// <summary>The polish region (3 × 3 over the cut stone, row 0 at the top) under a point, or -1 off the stone.</summary>
    private static int RegionAt(GemState g, Vector2 pos)
    {
        Rect box = CutBox(g);
        Vector2 d = pos - box.center;
        if (pos.x < 0 || d.x * d.x / (box.width * box.width / 4f) + d.y * d.y / (box.height * box.height / 4f) > 1f) return -1;
        int col = Mathf.Clamp(Mathf.FloorToInt((pos.x - box.xMin) / (box.width / 3f)), 0, 2);
        int row = Mathf.Clamp(Mathf.FloorToInt((box.yMax - pos.y) / (box.height / 3f)), 0, 2);
        return row * 3 + col;
    }

    private void DrawPolish(CraftPixels c, CraftPiece p)
    {
        GemState g = p.gem;
        if (g.stage < CraftGemStage.Faceted || !g.oriented) { DrawStone(c, p, true, Station.Reveals); return; }
        DrawCutStone(c, p, true, Station.Reveals);
        // How bright each region has come (dull dark, bright white, past it orange: the edges rounding).
        Rect box = CutBox(g);
        float cw = box.width / 3f, chh = box.height / 3f;
        for (int k = 0; k < CraftLapidary.PolishRegions; k++)
        {
            float v = k < g.polishRegions.Count ? g.polishRegions[k] : (float)(-Math.Log(Math.Max(1e-4, 1 - g.polish)) / 1.6);
            float shine = 1f - Mathf.Exp(-1.6f * v);
            float x0 = box.xMin + (k % 3) * cw, y1 = box.yMax - (k / 3) * chh;
            Color tint = v > CraftLapidary.OverPolish ? new Color(1f, 0.55f, 0.2f, 0.35f) : v < CraftLapidary.DullPolish ? new Color(0f, 0f, 0f, 0.4f * (1f - shine)) : new Color(1f, 1f, 1f, 0.25f * shine);
            c.Rect(x0 + 2, y1 - chh + 2, x0 + cw - 2, y1 - 2, tint);
        }
        Vector2 h = ui.Hover;
        if (polishing && h.x >= 0) c.Ring(h.x, h.y, 10f, 2f, Mark);
        // The hand's pressure (set with the wheel or on the gauge, wherever the stone is held: higher presses harder), the
        // heat it makes at this lap speed, the shine - as the pass is sent; a thin mark, this pass's own so far.
        c.Rect(430, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftGemDefinition d = GemOf(p);
        float lap = speed != null ? speed.value : 0.5f;
        float press = lapPressure;
        float hot = Mathf.Min(1f, (float)CraftLapidary.PolishHeatLimit / Mathf.Max(0.01f, lap));
        bool flakes = CraftLapidary.TableOnCleavage(d, g);
        float flake = (float)CraftLapidary.FlakePressure;
        float top = flakes ? Mathf.Min(hot, flake) : hot, full = (float)CraftLapidary.PolishFullRate;
        // green: the full rate - or, where the table flakes (or a fast lap heats) short of it, as hard as it safely takes
        bool safeOnly = top <= full;
        CraftArt.Label(c, 440, 352, "PRESS", CraftArt.LabelInk);
        Meter(c, PressGauge.x, PressGauge.y, PressGauge.width, PressGauge.height, press, new Color(0.9f, 0.9f, 0.95f),
            (safeOnly ? Mathf.Max(0f, top - 0.1f) : full, top, GoodBand), (top, 1f, HarmBand));
        CraftArt.Label(c, 500, 352, "HEAT", CraftArt.LabelInk);
        Meter(c, 506, 60, 18, 280, press * lap, new Color(1f, 0.8f, 0.4f), ((float)CraftLapidary.PolishHeatLimit, 1f, HarmBand));
        if (polishing && onStoneHeld > 0f)
        {
            // this pass so far (its means over its time on the stone: what the station judges it by)
            float meanPress = pressSum / onStoneHeld, meanHeat = meanPress * speedSum / onStoneHeld;
            c.Rect(PressGauge.xMax + 2, PressGauge.y + 1 + (PressGauge.height - 2) * Mathf.Clamp01(meanPress) - 1, PressGauge.xMax + 8, PressGauge.y + 1 + (PressGauge.height - 2) * Mathf.Clamp01(meanPress) + 1, Mark);
            c.Rect(526, 61 + 278 * Mathf.Clamp01(meanHeat) - 1, 532, 61 + 278 * Mathf.Clamp01(meanHeat) + 1, Mark);
        }
        CraftArt.Label(c, 440, 44, $"{press * 100:0}%", CraftArt.LabelInk);
        CraftArt.Label(c, 556, 352, "SHINE", CraftArt.LabelInk);
        Meter(c, 562, 60, 18, 280, (float)g.polish, new Color(0.6f, 0.85f, 1f), (0.7f, 1f, GoodBand));
        CraftArt.Label(c, 600, 330, "GREEN:", CraftArt.LabelDim);
        CraftArt.Label(c, 600, 316, safeOnly ? "SAFE" : "FULL RATE", CraftArt.LabelDim);
        CraftArt.Label(c, 600, 196, "PRESS: WHEEL", CraftArt.LabelDim);
        CraftArt.Label(c, 600, 182, "OR GAUGE", CraftArt.LabelDim);
        CraftArt.Label(c, 600, 290, "RED:", CraftArt.LabelDim);
        CraftArt.Label(c, 600, 276, flakes && flake < hot ? "IT FLAKES" : "IT HEATS", CraftArt.LabelDim);
        if (flakes) CraftArt.Label(c, 600, 240, "TABLE ON", Fair);
        if (flakes) CraftArt.Label(c, 600, 226, "CLEAVAGE", Fair);
    }

    private void PolishPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Stone;
        // the wheel sets the hand's pressure (in hand or not), higher harder; a press on the PRESS gauge sets it too
        if (phase == CraftPointer.Scroll) { lapPressure = Mathf.Clamp01(Mathf.Round((lapPressure + (scroll > 0 ? 0.05f : -0.05f)) * 20f) / 20f); return; }
        if (pressHeld)
        {
            if (phase == CraftPointer.Drag) lapPressure = PressOnGauge(pos.y);
            else if (phase == CraftPointer.Up || phase == CraftPointer.Cancel) pressHeld = false;
            return;
        }
        if (phase == CraftPointer.Up || phase == CraftPointer.Cancel) { FinishPass(); return; } // lifted off the lap: the pass so far counts
        if (phase != CraftPointer.Down) return;
        if (p.gem.stage < CraftGemStage.Faceted || !p.gem.oriented) { said = "Cut every tier first."; return; }
        if (new Rect(PressGauge.x - 8, PressGauge.y - 4, PressGauge.width + 16, PressGauge.height + 8).Contains(pos))
        {
            lapPressure = PressOnGauge(pos.y);
            pressHeld = true;
            return;
        }
        if (RegionAt(p.gem, pos) < 0) return;
        string cid = CompoundChosen;
        if (cid == null) { said = "There is no polishing compound."; return; }
        // a compound not on the lap yet charges it from the bag: none in the bag, none on the lap
        if (CraftLapidary.ChargesLap(p.gem, cid) && Have(cid) == 0) { said = $"You have no {ItemOf(cid)} to charge the lap with."; return; }
        polishing = true;
        polishFor = p.id;
        polishHeld = onStoneHeld = pressSum = speedSum = 0f;
        Array.Clear(regionTime, 0, regionTime.Length);
    }

    /// <summary>The stone comes off the lap: a pass under way counts if it was half done (its own means over its time on the stone), else nothing.</summary>
    private void FinishPass()
    {
        if (polishing && polishHeld >= PassSeconds * 0.5f) SendPass();
        polishing = false;
        polishHeld = onStoneHeld = pressSum = speedSum = 0f;
        Array.Clear(regionTime, 0, regionTime.Length);
    }

    /// <summary>
    /// One pass of the lap, sent as it is made: where on the stone it went and for how long (the seconds on each region: only
    /// time on the stone polishes), and the true mean pressure and lap speed over that time.
    /// </summary>
    private void SendPass()
    {
        CraftPiece p = polishFor != null ? Ws.Piece(polishFor) : null;
        float onStone = onStoneHeld, press = onStone > 0f ? pressSum / onStone : 0f, lap = onStone > 0f ? speedSum / onStone : 0f;
        var where = new List<float>(regionTime); // one a region
        polishHeld = onStoneHeld = pressSum = speedSum = 0f;
        Array.Clear(regionTime, 0, regionTime.Length);
        string id = CompoundChosen;
        if (p == null || id == null || onStone <= 0f) return;
        string stone = p.id;
        double heat = p.gem.heat, chips = p.gem.chips;
        CraftGemDefinition d = GemOf(p);
        bool soft = CraftLapidary.CompoundFit(Rules.Supply(id), d) < CraftLapidary.SoftCompound;
        Send(new CraftRequest(CraftRequestKind.PolishGem, stone) { id = id, count = 1, a = Mathf.Clamp01(press), b = Mathf.Clamp01(lap), points = where }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(stone) : null;
            if (q == null) return;
            int dull = 0, over = 0;
            foreach (float v in q.gem.polishRegions) { if (v < CraftLapidary.DullPolish) dull++; if (v > CraftLapidary.OverPolish) over++; }
            string words = soft ? "The lap slides over it: this compound is softer than the stone - a harder one polishes it."
                : q.gem.chips > chips + 1e-6 ? "A flake lifts off the table: it lies on the cleavage - polish it lighter."
                : q.gem.heat > heat + 1e-6 ? "It is getting hot on the lap: lighter, or slow the lap."
                : over > 0 ? "Polished past bright there: the edges round."
                : dull > 0 ? $"Polishing - {dull} region{(dull == 1 ? "" : "s")} still dull." : "Polished bright all over.";
            SayFor(stone, words);
        });
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (Stone == null)
        {
            if (phase == CraftPointer.Down) said = Selected != null ? "That is not a stone: pick a stone (left)." : "Pick a stone (left): put a rough stone on the bench first.";
            return;
        }
        switch (place)
        {
            case Place.Clean: CleanPointer(phase, pos); break;
            case Place.Test: TestPointer(phase, pos); break;
            case Place.Evaluate: EvaluatePointer(phase, pos, scroll); break;
            case Place.Orient: OrientPointer(phase, pos, scroll); break;
            case Place.Split: case Place.Saw: CutPointer(phase, pos); break;
            case Place.Grind: GrindPointer(phase, pos); break;
            case Place.Facet: FacetPointer(phase, pos, scroll); break;
            case Place.Polish: PolishPointer(phase, pos, scroll); break;
        }
    }

    // ------------------------------------------------------------------ the words
    public override void Refresh()
    {
        RefreshCounts(compound, compounds);
        if (info == null) return;
        CraftPiece p = Stone;
        if (place == Place.Identify && bookLeft != null) FillBook();
        if (place == Place.Facet) RefreshTiers(p);
        if (nameButton != null) nameButton.interactable = p != null && string.IsNullOrEmpty(p.gem.identifiedAs);
        if (markButton != null) markButton.interactable = p != null && p.gem.profile.Count == 0 && p.gem.facets.Count == 0;
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
                    how = g.stage >= CraftGemStage.Faceted ? "It is cut: the lap took its skin - there is nothing on it to clean off."
                        : $"<b>Clean</b>: hold and scrub over the brown crust - the brush is slow but harmless, the pick quick but it marks bare stone (and chips a brittle one worked roughly). Dirt {CraftLapidary.Dirt(g) * 100:0}%: crust left on blurs the tests and hides inclusions.";
                    break;
                case Place.Test:
                    how = CurrentTest == CraftGemTest.Scratch ? "<b>Hardness</b>: pick a point and draw it lightly and briefly across the stone: a point that scratches it is harder than it."
                        : CurrentTest == CraftGemTest.Refractometer ? "<b>Refractometer</b>: press and hold the stone still on the prism until the green gauge fills - the shadow edge then reads true."
                        : CurrentTest == CraftGemTest.Gravity ? "<b>Balance</b>: click the panel to weigh it in water - the needle swings; click again when it rests on the mark."
                        : CurrentTest == CraftGemTest.Dichroscope ? "<b>Dichroscope</b>: press and drag slowly round the ring: do the two windows change colour as it turns?"
                        : "<b>Loupe</b>: click the stone (or Look) to look at it closely.";
                    how += $" {Readings(g)}";
                    if (CraftLapidary.Dirt(g) > 0.25) how += " (It is still crusted: the readings will be off - clean it.)";
                    break;
                case Place.Identify:
                    how = !string.IsNullOrEmpty(g.identifiedAs) ? $"<b>Identify</b>: you named it {Rules.Gem(g.identifiedAs)?.Name.ToLowerInvariant() ?? g.identifiedAs}: the name sticks. {Readings(g)}"
                        : $"<b>Identify</b>: {Readings(g)} Compare them with the book and name it - once: the name sticks.";
                    break;
                case Place.Evaluate:
                    how = $"<b>Evaluate</b>: hold and move the loupe over the stone, the wheel focuses through its depth: what shows sharp is found - a steady hand reaches further. SEARCHED fills only where the loupe's ring has passed, at each depth in focus (the table, the middle, the culet) - what it has gone over, the loupe has found.{(g.evaluated ? $" Judged worth about {g.estimate:0} as it is - {g.inclusions.FindAll(i => i.found).Count} inclusions found." : "")}";
                    break;
                case Place.Orient:
                {
                    CraftGemGrade pr = Preview(p);
                    how = g.profile.Count > 0 || g.facets.Count > 0 ? "<b>Orient</b>: it is ground (or cut) to its planned outline: the plan is fixed."
                        : $"<b>Orient</b>: drag the gold outline (the finished stone) clear of the flaws, the wheel sizes it. Lay the gold line (the table) on the blue one (the axis): the table then lies across it - and 8° or more off the red (the cleavage). Then Mark it out.{(pr != null ? $" Expected about {pr.Yield:0.00} ct, worth {pr.Value:0}." : "")}";
                    break;
                }
                case Place.Split:
                    how = d == null ? "" : g.facets.Count > 0 || g.stage >= CraftGemStage.Faceted ? "It is already being faceted."
                        : !CleavageKnown(p) ? "<b>Split</b>: look at it under the loupe first - a stone that cleaves shows faint cleavage lines. Then draw the line and strike on the bar (the indicator is the blow's force: on the white line)."
                        : d.cleavage >= CraftCleavage.Good
                            ? (Snap ? "It cleaves: split it along its plane (Along the cleavage)." : $"<b>Cleave</b>: draw the line along the faint cleavage lines - within {CraftLapidary.CleaveTolerance(d):0.#}° of them, or it shatters - then strike when the marker is on the white line: the force that parts it.")
                            : (!Snap ? "It has no cleavage worth following: nick and snap it - or saw it if it is too tough to snap."
                                : "<b>Nick and snap</b>: draw the line, then strike when the marker is on the white line. It never breaks clean: the further off the blow, the more is lost, and far off it cracks.");
                    break;
                case Place.Saw:
                {
                    double ex = CraftLapidary.Excess(g);
                    how = g.facets.Count > 0 || g.stage >= CraftGemStage.Faceted ? "It is already being faceted."
                        : $"<b>Saw</b>: draw the line across the rough outside the gold outline, then click for each stroke when the marker is on a target ({sawStrokes}/{SawStrokesNeeded}). Feed past the red mark chips a brittle stone. {ex * 100:0}% of the rough lies outside the planned stone ({(ex <= CraftLapidary.PreformExcess ? "it can be faceted, better ground first" : ex <= CraftLapidary.GrindableExcess ? $"grind it now, or saw more off" : $"saw it below {CraftLapidary.GrindableExcess * 100:0}% to grind it")}).";
                    break;
                }
                case Place.Grind:
                    how = !g.oriented ? "Mark the stone out first (Orient)." : g.stage >= CraftGemStage.Faceted || g.facets.Count > 0 ? "It is already being faceted."
                        : g.profile.Count == 0 && CraftLapidary.Excess(g) > CraftLapidary.GrindableExcess ? $"Saw the bulk of the rough off first: the wheel takes on no more than {CraftLapidary.GrindableExcess * 100:0}% outside the planned stone."
                        : $"<b>Grind</b>: the stone turns against the wheel. Hold in the panel to press - higher presses harder - while the part outside the gold outline passes the wheel, and ease off before it cuts in (red). Steady pressure grinds true; dry, a hard press (the PRESS gauge's amber) heats it, and the heat it takes stays in it (HEATED{(d != null && d.heatSensitive ? ": at the red it cracks" : "")}). Still to take off: {CraftLapidary.Excess(g) * 100:0}% (facet below 15%).{(g.shapeError > 0.15 ? " The outline is going lopsided." : "")}";
                    break;
                case Place.Facet:
                    how = FacetText(p, cut);
                    break;
                default:
                {
                    string cid = CompoundChosen;
                    bool charged = cid != null && !CraftLapidary.ChargesLap(g, cid);
                    how = g.stage < CraftGemStage.Faceted ? "Cut every tier first."
                        : $"<b>Polish</b>: hold and move over the stone so every region comes up bright (only the time on the stone polishes). The wheel sets the hand's pressure, wherever it is held (or press the PRESS gauge): green is the full rate, red heats it{(CraftLapidary.TableOnCleavage(d, g) ? $" - and the table lies on the cleavage: past {CraftLapidary.FlakePressure * 100:0}% it flakes" : "")}. {(cid == null ? "" : charged ? "The lap is charged with this compound. " : "A jar of this compound charges the lap. ")}Polish {g.polish * 100:0}%. It will not mend a bad cut.";
                    break;
                }
            }
        }
        info.text = (how + " " + said).Trim();
    }

    private string FacetText(CraftPiece p, CraftGemCut cut)
    {
        GemState g = p.gem;
        if (cut == null) return "Mark the stone out first (Orient).";
        if (g.stage >= CraftGemStage.Polished) return "It is polished: its facets are done.";
        if (tier < 0 || tier >= cut.tiers.Count) return "Every tier is cut: polish it - or pick a tier in the menu to recut it.";
        if (facetHits.Count == 0 && !Ws.CanFacet(p.id, tier, out string why)) return $"<b>Facet</b>: {why}";
        CraftCutTier t = cut.tiers[tier];
        string chart;
        if (ChartAngle(p, t, out double angle))
            chart = $"{t.name}: {angle:0.0}° on the chart (green) - the quill is at {quill:0.0}°.";
        else
        {
            // no refractive index yet: the tier's angle relative to its kind's main tier
            string kind = t.kind == CraftCutTierKind.Pavilion ? "pavilion" : "crown";
            chart = $"{t.name}: the chart needs the stone's refractive index (the refractometer)" + (Math.Abs(t.angleOffset) > 0.01f ? $" - it is {(t.angleOffset > 0 ? "+" : "")}{t.angleOffset:0.#}° on the {kind}'s mains." : ".");
        }
        bool recut = g.facets.Exists(f => f.tier == tier);
        return $"<b>Facet</b> {g.facets.Count}/{cut.tiers.Count} tiers - {chart} Set the quill (drag the dial or the wheel), then click the lap for each facet when the marker is on the target.{(recut ? " Cutting it again recuts it." : "")}";
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
