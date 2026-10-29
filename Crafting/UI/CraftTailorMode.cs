using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tailor's, in four places, each worked with the hands:
/// <list type="bullet">
/// <item><b>Spin</b>: treadle the wheel in a steady rhythm (click the pedal) and draft the fibre out of the hand (hold
/// and draw the hand away from the wheel, steadily). Draft too fast for the wheel and the yarn is under-twisted and
/// weak; too slow and it kinks; an uneven draft makes it lumpy. A full bobbin is a hank of thread - coarse, even or
/// fine by how it was spun.</item>
/// <item><b>Spin a prepared batch</b>: pick a batch from the fibre bench (left) and spin it the same way: what the
/// batch is - scoured, carded, combed - shows in the yarn as much as the hands do. Set the draft (thick or fine).</item>
/// <item><b>Ply</b>: two bobbins of yarn twisted together the other way: treadle, and feed the strands back at a pace
/// that keeps the ply twist in its band (it should balance the singles' twist), evenly, so both strands share the load.</item>
/// <item><b>Skein &amp; reel</b>: turn the niddy-noddy in the hand (hold and circle round it): each turn winds a run of
/// wraps off the bobbin (1.5 m a wrap) and they are counted as they go - turned too fast, some slip past the count. When
/// the bobbin is empty the skein is tied off at what was counted; reel it off as thread. An apprentice tailor winds and
/// counts it by routine.</item>
/// <item><b>Weave</b> (at a loom): tension the warp (hold on the warp beam and let go with the needle in the green band:
/// an uneven warp puckers the cloth), then throw the shuttle and beat each row in with the reed on the timing bar - 100%
/// on the target packs the row true, off it the row is uneven, a bad miss skips a pick (a flaw in the cloth). Cut it off
/// the loom: the cloth goes on to be laid out, cut and sewn.</item>
/// <item><b>Measure</b>: lay the tape round the wearer - from each mark drag the tape to its end mark and let go on it.
/// The next item started is made to these measurements (a careless measure fits worse than none).</item>
/// <item><b>Lay out</b>: chalk every pattern on the cloth before cutting - patterns cannot overlap; the gauge shows how
/// much of the cloth is used; on the grain is green. Cutting along a chalked line is cleaner.</item>
/// <item><b>Needle</b>: thread the needle - bring the thread's end through the eye; the needle trembles in the other
/// hand (more as you tire), and an end that catches on the eye frays (snip it).</item>
/// <item><b>Cut</b>: lay the pattern on the cloth along its grain (the arrows: the warp runs up the bolt), click to
/// pin it, then cut round it with the shears - snip by snip (press and release), each snip from the last.</item>
/// <item><b>Sew</b>: pick the item being made and a seam; stitch along the seam line - each click a run of stitches
/// (one for a novice, two from journeyman, three for a master tailor), held as long as the thread is pulled (tension).
/// Even stitches, on the line, pulled firm but not puckered. A practised hand's needleful lasts longer.</item>
/// </list>
/// </summary>
public sealed class CraftTailorMode : CraftMode
{
    private enum Place { Spin, Ply, Skein, Weave, Fit, Layout, Needle, Cut, Sew }
    private static readonly string[] PlaceNames = { "Spin", "Ply", "Skein & reel", "Weave", "Measure", "Lay out", "Thread the needle", "Cut cloth", "Sew" };

    private Place place = Place.Spin;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> spinRows = new List<RectTransform>(), needleRows = new List<RectTransform>(), cutRows = new List<RectTransform>(), sewRows = new List<RectTransform>();
    private readonly List<RectTransform> plyRows = new List<RectTransform>(), skeinRows = new List<RectTransform>(), layoutRows = new List<RectTransform>(), weaveRows = new List<RectTransform>(),
        fitRows = new List<RectTransform>();
    private TMP_Dropdown fibre, thread, pattern, seam, strandA, strandB;
    private Slider draft;
    private readonly List<string> yarns = new List<string>();
    private string yarnsKey = "";

    // Plying: the singles' twist the ply twist should balance.
    private float singlesTwist = 0.5f;

    // The niddy-noddy, turned in the hand: each turn winds a run of wraps off the bobbin; wraps wound and wraps counted.
    private static readonly Vector2 Niddy = new Vector2(330f, 200f);
    private int wraps, counted;
    private bool windingSkein;
    private float lastWindAngle, windTurn, niddyAngle, turnStartedAt;
    private string skeinFor = "";

    // Measuring: the wearer's landmarks, the tape's run from each start mark to its end mark.
    private static readonly Vector4[] Marks = { new Vector4(300, 300, 420, 300), new Vector4(310, 240, 410, 240), new Vector4(300, 190, 420, 190), new Vector4(420, 300, 470, 170) };
    private static readonly string[] MarkNames = { "the shoulders", "the chest", "the waist", "the arm" };
    private int markAt;
    private bool taping;
    private float tapeErr;
    private Vector2 tapeEnd;
    private TextMeshProUGUI info;
    private readonly List<string> fibres = new List<string>(), threads = new List<string>(), patterns = new List<string>(), seams = new List<string>();
    private string seamsFor = "";
    private string said = "";

    // Spinning.
    private Vector2 wheelCentre, orifice;
    private Rect pedal, handArea;
    private float wheel, wheelAngle, lastPedal = -9f, bobbin, draftSum, draftSq, draftN, twistSum, twistShown;
    // Drafting at the wheel's pace: the twist is the wheel's turns per length drawn (0.5 = just right).
    private const float TwistPace = 260f, TwistLow = 0.3f, TwistHigh = 0.7f;
    // The wheel's momentum: how much a treadle adds, how fast it slows, how fast it turns at full speed; and how much
    // drafting fills a bobbin (pixels of hand travel - some twenty draws).
    private const float TreadleKick = 0.32f, WheelDrag = 0.045f, WheelDragAtSpeed = 0.055f, WheelTurnsPerSecond = 0.9f, BobbinPixels = 6500f;
    private bool drafting;
    private Vector2 lastHand;
    private float lastHandTime;
    private readonly List<float> yarn = new List<float>();

    // The needle (it stays threaded for some stitches).
    private bool threaded;
    private int threadLeft, frays;
    private string threadId = "";
    private Vector2 threadEnd, lastEnd;
    private Rect eye;

    // Cutting.
    private int rotation, placedX = -1, placedY = -1;
    private readonly List<Vector2> trace = new List<Vector2>(), border = new List<Vector2>();
    private readonly HashSet<int> touched = new HashSet<int>();
    private readonly List<float> snips = new List<float>();
    private Vector2 lastSnip;
    private Rect bolt = new Rect(30, 20, 660, 360);

    // Weaving: the warp's tension, then each row beaten in on the timing bar.
    private Slider weaveWidth, beatDensity;
    private CraftTimingBar beatBar;
    private CraftTimingHit? lastBeat;
    private readonly List<CraftTimingHit> beats = new List<CraftTimingHit>();
    private float warpTension = -1f, tensionNeedle;
    private bool winding;
    private string weaveFor = "";
    private static readonly Rect Loom = new Rect(110, 50, 400, 320), WarpBeam = new Rect(540, 60, 150, 300), BeatBar = new Rect(110, 14, 400, 18);
    // The warp's best tension (the needle's green band), and how far off it an even warp can be.
    private const float TensionIdeal = 0.62f, TensionBand = 0.08f;

    // Sewing.
    private float seamY = 200f, lastStitchX = -1f, pressAt;
    private bool pulling;
    private readonly List<Vector3> stitches = new List<Vector3>(); // x, y, tension
    private float spacingSum, alignSum, tensionSum, tensionSq;
    private int pending;

    public override string Title => "Tailor";
    public override CraftDiscipline Discipline => CraftDiscipline.Tailoring;
    public override bool Available(CraftStation s) => s.Has(CraftTools.SpinningWheel) || s.Has(CraftTools.TailorTable) || s.Has(CraftTools.Loom);
    public override string Hint => "Spin fibre into yarn, weave it into cloth, thread the needle, cut cloth along its grain with the shears, sew the seams - each with the hands.";

    public override void Build(RectTransform controls)
    {
        spinRows.Clear(); needleRows.Clear(); cutRows.Clear(); sewRows.Clear(); plyRows.Clear(); skeinRows.Clear(); layoutRows.Clear(); weaveRows.Clear(); fitRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        // Only the work this station has the tools for: the wheel spins; the table threads, cuts and sews.
        if (!Here(place)) place = Here(Place.Spin) ? Place.Spin : Here(Place.Weave) && !Here(Place.Needle) ? Place.Weave : Place.Needle;
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(r0, PlaceNames[i], () => { place = pl; Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f);
            b.gameObject.SetActive(Here(pl));
            placeButtons.Add(b);
        }
        fibres.Clear(); threads.Clear(); patterns.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
        {
            if (s.kind == CraftSupplyKind.Fibre) fibres.Add(s.id);
            if (s.kind == CraftSupplyKind.Thread) threads.Add(s.id);
        }
        RectTransform s1 = Row(controls);
        Text(s1, "Fibre", 13f, 44f);
        fibre = CountedDrop(s1, fibres, 200f, "No fibre");
        fibre.onValueChanged.AddListener(_ => { bobbin = 0f; draftSum = draftSq = draftN = twistSum = 0f; yarn.Clear(); drafting = false; });
        draft = Slide(s1, "Draft thick-fine", 0f, 1f, 0.5f, false, 100f);
        spinRows.Add(s1);
        RectTransform y1 = Row(controls);
        Text(y1, "Bobbins", 13f, 60f);
        strandA = Drop(y1, new List<string> { "-" }, 0, 230f);
        strandB = Drop(y1, new List<string> { "-" }, 0, 230f);
        plyRows.Add(y1);
        RectTransform k1 = Row(controls);
        Btn(k1, "Tie off the skein", TieOff, 140f);
        Btn(k1, "Reel off as thread", () => { if (YarnPiece != null && Send(new CraftRequest(CraftRequestKind.ReelYarn, YarnPiece.id))) said = "Reeled off."; }, 150f, UITheme.ButtonStyle.Primary);
        RoutineButton(k1, CraftRoutine.WindSkein, "Wind & count by routine", () => YarnPiece?.id, 200f);
        skeinRows.Add(k1);
        RectTransform m1 = Row(controls);
        RoutineButton(m1, CraftRoutine.MeasureWearer, "Measure by routine", () => Assembly?.id, 180f);
        fitRows.Add(m1);
        RectTransform v1 = Row(controls);
        weaveWidth = Slide(v1, "Width", 2f, 16f, 6f, true, 44f);
        weaveWidth.onValueChanged.AddListener(_ => { if (beats.Count == 0) weaveFor = ""; });
        beatDensity = Slide(v1, "Beat open-packed", 0f, 1f, 0.5f, false, 110f);
        Btn(v1, "Cut it off the loom", FinishWeave, 150f, UITheme.ButtonStyle.Primary);
        weaveRows.Add(v1);
        RectTransform l1 = Row(controls);
        Text(l1, "Pattern", 13f, 56f);
        TMP_Dropdown lp = Drop(l1, PatternNames(), 0, 200f);
        lp.onValueChanged.AddListener(v => { if (pattern != null) pattern.SetValueWithoutNotify(v); });
        Btn(l1, "Turn it", () => rotation = (rotation + 1) % 4, 80f);
        Btn(l1, "Rub out the chalk", () => { if (Selected != null) Send(new CraftRequest(CraftRequestKind.LayoutPattern, Selected.id) { flag = true }); }, 140f, UITheme.ButtonStyle.Ghost);
        layoutRows.Add(l1);
        RectTransform n1 = Row(controls);
        Text(n1, "Thread", 13f, 50f);
        thread = CountedDrop(n1, threads, 200f, "No thread");
        thread.onValueChanged.AddListener(_ => { threaded = false; frays = 0; });
        Btn(n1, "Snip the end", () => { frays = 0; said = "A clean end again."; }, 120f);
        PractisedButton(n1, CraftDiscipline.Tailoring, CraftRank.Apprentice, "Thread it (practised)", ThreadByRoutine, 170f);
        needleRows.Add(n1);
        RectTransform c1 = Row(controls);
        var pn = new List<string>();
        foreach (CraftPattern p in Rules.Patterns) { patterns.Add(p.id); pn.Add(p.Name); }
        Text(c1, "Pattern", 13f, 56f);
        pattern = Drop(c1, pn, 0, 200f);
        pattern.onValueChanged.AddListener(_ => Lift());
        Btn(c1, "Turn it", () => { if (placedX < 0) rotation = (rotation + 1) % 4; }, 80f);
        Btn(c1, "Unpin", Lift, 80f, UITheme.ButtonStyle.Ghost);
        cutRows.Add(c1);
        RectTransform w1 = Row(controls);
        Text(w1, "Seam", 13f, 44f);
        seam = Drop(w1, new List<string> { "-" }, 0, 260f);
        sewRows.Add(w1);
        info = InfoRow(controls, 44f);
        Show();
    }

    private bool Here(Place p) => Station != null && (p == Place.Spin || p == Place.Ply ? Station.Has(CraftTools.SpinningWheel)
        : p == Place.Weave ? Station.Has(CraftTools.Loom)
        : p == Place.Skein ? Station.Has(CraftTools.SpinningWheel) || Station.Has(CraftTools.TailorTable) : Station.Has(CraftTools.TailorTable));

    private List<string> PatternNames()
    {
        var names = new List<string>();
        foreach (CraftPattern p in Rules.Patterns) names.Add(p.Name);
        return names;
    }

    /// <summary>A batch of fibre picked to spin (from the fibre bench), or null: spin from the bag.</summary>
    /// <summary>
    /// The batch of fibre to spin: the one picked, else one of the chosen fibre lying on the station, else (with none of
    /// that fibre in the bag) any batch on the station. Null: spin from the bag.
    /// </summary>
    private CraftPiece Batch
    {
        get
        {
            if (Selected != null && Selected.kind == CraftPieceKind.Fibre) return Selected;
            if (Station == null) return null;
            List<CraftPiece> mine = Station.FibreBatches(FibreId);
            if (mine.Count > 0) return mine[0];
            if (FibreId == null || Have(FibreId) == 0)
            {
                List<CraftPiece> any = Station.FibreBatches();
                if (any.Count > 0) return any[0];
            }
            return null;
        }
    }
    private CraftPiece YarnPiece => Selected != null && Selected.kind == CraftPieceKind.Yarn ? Selected : null;
    private string Strand(TMP_Dropdown d) => d != null && yarns.Count > 0 ? yarns[Mathf.Clamp(d.value, 0, yarns.Count - 1)] : null;

    private string FibreId => fibres.Count > 0 ? fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)] : null;
    private string ThreadId => threads.Count > 0 ? threads[Mathf.Clamp(thread.value, 0, threads.Count - 1)] : null;

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p) && Here(p)) { FlushStitches(); place = p; Show(); }
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in spinRows) r.gameObject.SetActive(place == Place.Spin);
        foreach (RectTransform r in plyRows) r.gameObject.SetActive(place == Place.Ply);
        foreach (RectTransform r in skeinRows) r.gameObject.SetActive(place == Place.Skein);
        foreach (RectTransform r in layoutRows) r.gameObject.SetActive(place == Place.Layout);
        foreach (RectTransform r in weaveRows) r.gameObject.SetActive(place == Place.Weave);
        foreach (RectTransform r in fitRows) r.gameObject.SetActive(place == Place.Fit);
        foreach (RectTransform r in needleRows) r.gameObject.SetActive(place == Place.Needle);
        foreach (RectTransform r in cutRows) r.gameObject.SetActive(place == Place.Cut);
        foreach (RectTransform r in sewRows) r.gameObject.SetActive(place == Place.Sew);
        drafting = pulling = false;
    }

    public override void Leave()
    {
        FlushStitches();
        Show();
    }

    public override void Tick(float dt)
    {
        // The wheel runs down unless it is treadled.
        // A heavy wheel is a flywheel: it keeps turning a long while after the last treadle (from full speed it takes
        // some ten seconds to stop), slowing gently - faster at speed, where the drive band and the flyer drag most.
        wheel = Mathf.Max(0f, wheel - dt * (WheelDrag + WheelDragAtSpeed * wheel));
        wheelAngle += wheel * dt * WheelTurnsPerSecond * 360f;
        if (!drafting) twistShown = Mathf.MoveTowards(twistShown, 0f, dt * 0.5f);
        if (place == Place.Weave)
        {
            beatBar?.Tick(dt);
            // Winding the warp beam: the tension climbs faster the tighter it gets (and trembles in the hand).
            if (winding && ui.PointerHeld) tensionNeedle = Mathf.Min(1.05f, tensionNeedle + dt * (0.18f + 0.45f * tensionNeedle) + (float)Hands.Tremble(beats.Count + 251) * dt * 3f);
        }
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        switch (place)
        {
            case Place.Spin: case Place.Ply: DrawSpin(c); break;
            case Place.Skein: DrawSkein(c); break;
            case Place.Weave: DrawWeave(c); break;
            case Place.Fit: DrawFit(c); break;
            case Place.Layout: DrawLayout(c); break;
            case Place.Needle: DrawNeedle(c); break;
            case Place.Cut: DrawCut(c); break;
            default: DrawSew(c); break;
        }
    }

    private void DrawSpin(CraftPixels c)
    {
        wheelCentre = new Vector2(170f, 222f);
        orifice = new Vector2(330f, 250f);
        pedal = new Rect(100, 30, 140, 30);
        handArea = new Rect(330, 100, 380, 280);
        // The wheel and its spokes turning, the drive band, the flyer, the pedal.
        c.Ring(wheelCentre.x, wheelCentre.y, 110f, 6f, Wood);
        for (int k = 0; k < 8; k++)
        {
            float a = (wheelAngle + k * 45f) * Mathf.Deg2Rad;
            c.Line(wheelCentre.x, wheelCentre.y, wheelCentre.x + Mathf.Cos(a) * 106f, wheelCentre.y + Mathf.Sin(a) * 106f, Wood, 2f);
        }
        c.Line(wheelCentre.x, wheelCentre.y + 110f, orifice.x - 20f, orifice.y + 10f, new Color(0.8f, 0.75f, 0.6f), 1f);
        c.Rect(orifice.x - 40f, orifice.y - 10f, orifice.x, orifice.y + 10f, Wood);
        c.Disc(orifice.x - 20f, orifice.y, 14f * (0.5f + 0.5f * bobbin), new Color(0.85f, 0.8f, 0.68f)); // yarn on the bobbin
        c.Rect(pedal.xMin, pedal.yMin, pedal.xMax, pedal.yMax, Time.unscaledTime - lastPedal < 0.15f ? Mark : Wood);
        c.Line(pedal.center.x, pedal.yMax, wheelCentre.x, wheelCentre.y - 20f, Iron, 2f);
        Meter(c, 30, 380, 260, 8, wheel, new Color(0.6f, 0.85f, 1f));
        Meter(c, 30, 366, 260, 6, bobbin, Mark);
        // The twist going in now: the band between the ticks is right; left of it pulls apart, right of it kinks.
        float lo = place == Place.Ply ? singlesTwist * 0.8f - 0.12f : TwistLow, hi = place == Place.Ply ? singlesTwist * 0.8f + 0.12f : TwistHigh;
        bool right = twistShown >= lo && twistShown <= hi;
        Meter(c, 30, 350, 260, 8, twistShown, right ? new Color(0.45f, 0.85f, 0.45f) : twistShown < 0.05f ? Iron : new Color(0.95f, 0.45f, 0.3f));
        c.Rect(30 + 260 * lo - 1, 346, 30 + 260 * lo + 1, 362, Color.white);
        c.Rect(30 + 260 * hi - 1, 346, 30 + 260 * hi + 1, 362, Color.white);
        if (place == Place.Ply)
        {
            // Two strands from the lazy kate, twisting together into one.
            Vector2 hd = drafting ? lastHand : new Vector2(orifice.x + 60f, orifice.y);
            c.Line(hd.x, hd.y, 690, 120, new Color(0.9f, 0.86f, 0.75f), 2f);
            c.Line(hd.x, hd.y, 690, 90, new Color(0.85f, 0.8f, 0.7f), 2f);
            c.Rect(670, 70, 710, 140, Wood);
        }
        else if (Batch == null && (fibres.Count == 0 || Have(FibreId) == 0))
            c.Rect(handArea.xMin, handArea.yMin, handArea.xMax, handArea.yMax, new Color(0.5f, 0.1f, 0.1f, 0.18f)); // nothing in the hand
        // The yarn from the orifice to the hand: thick where the draft lagged, thin where it ran ahead.
        Vector2 hand = drafting ? lastHand : new Vector2(orifice.x + 60f, orifice.y);
        int n = yarn.Count;
        for (int i = 0; i < n; i++)
        {
            float t0 = i / (float)n, t1 = (i + 1) / (float)n;
            Vector2 a = Vector2.Lerp(orifice, hand, t0), b = Vector2.Lerp(orifice, hand, t1);
            c.Line(a.x, a.y, b.x, b.y, new Color(0.9f, 0.86f, 0.75f), Mathf.Clamp(yarn[i], 1f, 6f));
        }
        if (n == 0) c.Line(orifice.x, orifice.y, hand.x, hand.y, new Color(0.9f, 0.86f, 0.75f), 2f);
        // The fibre in the hand.
        if (place == Place.Ply) return;
        CraftSupplyDefinition f = fibres.Count > 0 ? Rules.Supply(fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)]) : null;
        for (int k = 0; k < 14; k++)
            c.Disc(hand.x + 10f + (k * 7 % 23), hand.y - 10f + (k * 11 % 21), 5f, f != null && f.id.Contains("flax") ? new Color(0.78f, 0.72f, 0.55f) : new Color(0.92f, 0.9f, 0.85f));
    }

    private void DrawNeedle(CraftPixels c)
    {
        // The needle held up in the other hand: it trembles, more as the arm tires.
        float t = Time.unscaledTime, amp = 2f + (float)Hands.Unsteadiness * 160f;
        float ex = 400f + Mathf.Sin(t * 5.3f) * amp * 0.6f, ey = 230f + Mathf.Sin(t * 3.7f + 1f) * amp;
        eye = new Rect(ex - 5f, ey - 26f, 10f, 52f);
        c.Rect(ex - 9f, ey - 230f, ex + 9f, ey + 60f, new Color(0.75f, 0.76f, 0.8f));
        c.Rect(ex - 4f, ey - 24f, ex + 4f, ey + 24f, new Color(0.06f, 0.06f, 0.07f)); // the eye
        Vector2 h = ui.Hover;
        if (h.x < 0) return;
        // The thread's end follows the hand, with its tremble.
        threadEnd = h + new Vector2((float)Hands.Tremble(Mathf.RoundToInt(t * 10f) + 91) * 120f, (float)Hands.Tremble(Mathf.RoundToInt(t * 10f) + 92) * 120f);
        CraftSupplyDefinition s = threads.Count > 0 ? Rules.Supply(threads[Mathf.Clamp(thread.value, 0, threads.Count - 1)]) : null;
        Color col = s != null && s.id.Contains("wool") ? new Color(0.9f, 0.88f, 0.8f) : new Color(0.85f, 0.82f, 0.7f);
        c.Line(threadEnd.x - 200f, threadEnd.y - 40f, threadEnd.x, threadEnd.y, col, 2f);
        c.Disc(threadEnd.x, threadEnd.y, 1.5f + frays * 1.2f, col);
        if (threaded) c.Line(ex, ey, ex + 160f, ey - 60f, col, 2f);
        Meter(c, 30, 380, 200, 6, threadLeft / (float)Needleful, Mark);
    }

    private CraftPattern Current => patterns.Count > 0 ? Rules.Pattern(patterns[Mathf.Clamp(pattern.value, 0, patterns.Count - 1)]) : null;

    private void DrawCut(CraftPixels c)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        c.Rect(bolt.xMin - 10, bolt.yMin - 10, bolt.xMax + 10, bolt.yMax + 10, new Color(0.42f, 0.33f, 0.24f));
        if (p == null || p.kind != CraftPieceKind.Hide || !CraftTextiles.IsCloth(Rules, p) || pat == null) return;
        CraftArt.DrawHide(c, Rules, p.hide, bolt);
        CraftArt.HideLayout(p.hide, bolt, out Vector2 origin, out float cell);
        DrawChalk(c, p, origin, cell);
        // The cloth's grain: the warp runs up the bolt.
        Arrow(c, new Vector2(bolt.xMax - 20f, bolt.yMin + 40f), new Vector2(bolt.xMax - 20f, bolt.yMin + 120f), new Color(1f, 1f, 1f, 0.6f));
        int gx, gy;
        Vector2 h = ui.Hover;
        if (placedX >= 0) { gx = placedX; gy = placedY; }
        else
        {
            if (!bolt.Contains(h)) return;
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
                    HideCell hc = p.hide.Cell(gx + x, gy + y);
                    if (hc != null && hc.present && (hc.flaw || hc.damage > 0.3))
                        c.Ring(x0 + cell * 0.5f, y0 + cell * 0.5f, cell * 0.45f, 1.5f, new Color(1f, 0.3f, 0.25f, 0.9f));
                }
        // The pattern's own grain line: along the bolt's (green) or across it (amber).
        Vector2 mid = new Vector2(origin.x + (gx + pw * 0.5f) * cell, origin.y + (gy + ph * 0.5f) * cell);
        bool along = rotation % 2 == 0;
        Vector2 dir = along ? new Vector2(0f, cell * Mathf.Min(ph, 4) * 0.4f) : new Vector2(cell * Mathf.Min(pw, 4) * 0.4f, 0f);
        Arrow(c, mid - dir, mid + dir, along ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.75f, 0.3f));
        if (placedX >= 0)
        {
            Border(pat, origin, cell);
            for (int i = 0; i < border.Count; i++)
                c.Disc(border[i].x, border[i].y, touched.Contains(i) ? 2.2f : 1.2f, touched.Contains(i) ? new Color(0.5f, 1f, 0.6f, 0.9f) : new Color(1f, 1f, 1f, 0.7f));
            for (int i = 1; i < trace.Count; i++)
                c.Line(trace[i - 1].x, trace[i - 1].y, trace[i].x, trace[i].y, Color.Lerp(new Color(0.45f, 1f, 0.55f), new Color(1f, 0.35f, 0.3f), Mathf.Clamp01(Nearest(trace[i], out _) / (cell * 1.5f))), 2f);
            Meter(c, bolt.xMin, bolt.yMax + 2, bolt.width, 6, border.Count > 0 ? touched.Count / (float)border.Count : 0f, new Color(0.5f, 1f, 0.6f));
        }
        if (bolt.Contains(h))
        {
            // The shears, open or closing.
            float open = ui.PointerHeld ? 4f : 14f;
            c.Line(h.x, h.y, h.x + 30f, h.y + open, new Color(0.75f, 0.76f, 0.8f), 2f);
            c.Line(h.x, h.y, h.x + 30f, h.y - open, new Color(0.75f, 0.76f, 0.8f), 2f);
            c.Ring(h.x + 36f, h.y + open + 4f, 5f, 1.5f, Iron);
            c.Ring(h.x + 36f, h.y - open - 4f, 5f, 1.5f, Iron);
        }
    }

    private static void Arrow(CraftPixels c, Vector2 a, Vector2 b, Color col)
    {
        c.Line(a.x, a.y, b.x, b.y, col, 2f);
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        c.Line(b.x, b.y, b.x - d.x * 8f + n.x * 5f, b.y - d.y * 8f + n.y * 5f, col, 2f);
        c.Line(b.x, b.y, b.x - d.x * 8f - n.x * 5f, b.y - d.y * 8f - n.y * 5f, col, 2f);
    }

    private void DrawSew(CraftPixels c)
    {
        CraftPiece a = Selected;
        // Two edges of cloth laid together, the seam line chalked along them.
        Color cloth = new Color(0.8f, 0.77f, 0.68f);
        if (a != null && a.kind == CraftPieceKind.Assembly)
            foreach (AssemblyPart part in a.assembly.parts)
            {
                CraftPiece q = Ws.Piece(part.piece);
                if (q != null && q.kind == CraftPieceKind.Hide) { cloth = Rules.Hide(q.hide.hide)?.color ?? cloth; break; }
            }
        c.Rect(30, 130, 690, 200, cloth);
        c.Rect(30, 200, 690, 270, Color.Lerp(cloth, Color.black, 0.12f));
        for (float x = 40; x < 680; x += 10) c.Line(x, seamY, x + 5, seamY, new Color(1f, 1f, 1f, 0.6f), 1f);
        // The ideal run (a stitch, or a practised hand's run of them), as a reference by the seam's start.
        c.Line(40, seamY - 40, 40 + IdealGap * StitchesPerClick, seamY - 40, new Color(1f, 1f, 1f, 0.4f), 2f);
        foreach (Vector3 s in stitches)
        {
            Color col = Color.Lerp(new Color(0.95f, 0.9f, 0.7f), new Color(0.35f, 0.22f, 0.12f), s.z);
            c.Line(s.x - 8f, s.y - 6f, s.x + 8f, s.y + 6f, col, 1f + 2f * s.z);
            if (s.z > 0.85f) c.Line(s.x - 10f, s.y - 12f, s.x - 10f, s.y + 12f, new Color(0f, 0f, 0f, 0.25f), 1f); // puckered
        }
        if (pulling) Meter(c, ui.Hover.x - 30f, seamY + 30f, 60f, 6, Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.8f), Mark);
        Vector2 h = ui.Hover;
        if (h.x >= 0) { c.Line(h.x, h.y, h.x + 4f, h.y + 40f, new Color(0.8f, 0.8f, 0.85f), 1.5f); c.Disc(h.x + 4f, h.y + 40f, 2f, Iron); }
        Meter(c, 30, 380, 200, 6, threadLeft / (float)Needleful, Mark);
    }

    private const float IdealGap = 22f;

    /// <summary>Stitches each click lays (one; a run of two from journeyman, three for a master).</summary>
    private int StitchesPerClick => CraftMastery.StitchesPerAction(Mastery, MasterySettings);

    // ------------------------------------------------------------------ weaving
    /// <summary>The yarn on the loom: the bobbin picked, else the first on the bench.</summary>
    private CraftPiece WeaveYarn
    {
        get
        {
            if (YarnPiece != null) return YarnPiece;
            if (Ws == null) return null;
            foreach (CraftPiece q in Ws.Pieces) if (q.kind == CraftPieceKind.Yarn && q.location == CraftLocation.Air) return q;
            return null;
        }
    }

    /// <summary>A steady number from an id (the targets' places follow from it, the same every time).</summary>
    private static int Salt(string id)
    {
        int h = 17;
        foreach (char ch in id ?? "") h = unchecked(h * 31 + ch);
        return h;
    }

    private int RowsAvailable(CraftPiece y) => y == null ? 0 : Mathf.Min(30, Mathf.FloorToInt((float)(y.yarn.length / (CraftFibre.MetresPerCell * Mathf.Max(2f, weaveWidth.value)))));

    /// <summary>Rows thrown and beaten in with each beat (a practised weaver's rhythm carries two or three).</summary>
    private int RowsPerBeat => CraftMastery.RowsPerBeat(Mastery, MasterySettings);

    /// <summary>Rows woven so far.</summary>
    private int RowsWoven => beats.Count * RowsPerBeat;

    private CraftTimingBar BeatBarFor(CraftPiece y)
    {
        if (beatBar != null && weaveFor == y.id) return beatBar;
        weaveFor = y.id;
        // a lumpy yarn is harder to beat in evenly
        beatBar = new CraftTimingBar(Rules.Settings.timing) { difficulty = 1.25f - 0.5f * (float)CraftFibre.Quality(y.yarn), skill = (float)Hands.skill };
        beatBar.NewTargets(Salt(y.id), beats.Count);
        return beatBar;
    }

    private void DrawWeave(CraftPixels c)
    {
        CraftPiece y = WeaveYarn;
        // The loom: its frame, the warp threads, the cloth woven so far (each row as it was beaten), the reed.
        c.Rect(Loom.xMin, Loom.yMin, Loom.xMax, Loom.yMax, new Color(0.12f, 0.11f, 0.1f));
        c.Frame(Loom.xMin, Loom.yMin, Loom.xMax, Loom.yMax, Wood, 8f);
        int width = Mathf.RoundToInt(weaveWidth != null ? weaveWidth.value : 6f);
        float clothW = Mathf.Min(Loom.width - 40f, width * 22f), x0 = Loom.center.x - clothW / 2f, rowH = (Loom.height - 60f) / 30f;
        Color yc = y != null ? Rules.Supply(y.yarn.fibre)?.color ?? new Color(0.85f, 0.82f, 0.75f) : new Color(0.85f, 0.82f, 0.75f);
        yc.a = 1f;
        for (int k = 0; k <= width * 4; k++)
        {
            float x = x0 + k * clothW / (width * 4);
            c.Line(x, Loom.yMin + 20f, x, Loom.yMax - 20f, warpTension < 0f ? new Color(1f, 1f, 1f, 0.25f) : new Color(yc.r, yc.g, yc.b, 0.7f), 1f);
        }
        int per = RowsPerBeat;
        for (int r = 0; r < beats.Count * per && r < 30; r++)
        {
            CraftTimingHit bt = beats[r / per];
            float yb = Loom.yMin + 24f + r * rowH;
            Color rc = bt.damage ? new Color(0.9f, 0.3f, 0.25f) : Color.Lerp(yc * 0.6f, yc, bt.score);
            rc.a = 1f;
            // an uneven row sits askew
            float skew = (1f - bt.score) * 4f * Mathf.Sign(bt.offset);
            c.Line(x0, yb, x0 + clothW, yb + skew, rc, rowH * 0.9f);
        }
        float reedY = Loom.yMin + 30f + Mathf.Min(30, beats.Count * per) * rowH;
        c.Rect(x0 - 10f, reedY, x0 + clothW + 10f, reedY + 6f, Wood);
        // The warp beam and the tension needle, its green band.
        c.Rect(WarpBeam.xMin, WarpBeam.yMin, WarpBeam.xMax, WarpBeam.yMax, new Color(0.2f, 0.19f, 0.18f));
        float g0 = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * (TensionIdeal - TensionBand), g1 = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * (TensionIdeal + TensionBand);
        c.Rect(WarpBeam.xMin + 10f, g0, WarpBeam.xMin + 40f, g1, new Color(0.35f, 0.7f, 0.3f));
        float shown = winding ? tensionNeedle : Mathf.Max(0f, warpTension);
        float ny = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * Mathf.Clamp01(shown);
        c.Rect(WarpBeam.xMin + 6f, ny - 2f, WarpBeam.xMin + 60f, ny + 2f, shown > TensionIdeal + TensionBand * 2f ? new Color(1f, 0.4f, 0.3f) : Mark);
        c.Disc(WarpBeam.center.x + 30f, WarpBeam.center.y, 26f, Wood);
        c.Ring(WarpBeam.center.x + 30f, WarpBeam.center.y, 26f, 3f, Iron);
        if (y != null && warpTension >= 0f)
            DrawTimingBar(c, BeatBarFor(y), BeatBar.xMin, BeatBar.yMin, BeatBar.width, BeatBar.height, lastBeat);
    }

    private void WeavePointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece y = WeaveYarn;
        if (y == null) { if (phase == CraftPointer.Down) said = "There is no yarn to weave: spin some first."; return; }
        if (CraftFibre.ClothOf(Rules, y) == null) { if (phase == CraftPointer.Down) said = "Nothing is woven from that yarn."; return; }
        if (phase == CraftPointer.Down && WarpBeam.Contains(pos))
        {
            if (beats.Count > 0) { said = "The warp is already on: weave, or cut the cloth off first."; return; }
            winding = true;
            tensionNeedle = 0f;
            return;
        }
        if (phase == CraftPointer.Up && winding)
        {
            winding = false;
            if (tensionNeedle < 0.2f) { said = "Wind the warp tighter: hold longer on the beam."; return; }
            warpTension = tensionNeedle;
            float off = Mathf.Abs(warpTension - TensionIdeal);
            said = off <= TensionBand ? "The warp is evenly tensioned." : warpTension > TensionIdeal ? "Wound too tight: the warp will pucker the cloth." : "Too slack: the warp sags unevenly.";
            Hands.Move(0.1);
            return;
        }
        if (phase != CraftPointer.Down || !(Loom.Contains(pos) || BeatBar.Contains(pos))) return;
        if (warpTension < 0f) { said = "Tension the warp first: hold on the warp beam (right)."; return; }
        int rows = RowsAvailable(y);
        if (RowsWoven >= rows) { said = "The yarn is all in: cut the cloth off the loom."; return; }
        // A throw of the shuttle and a beat of the reed, judged on the bar.
        CraftTimingBar b = BeatBarFor(y);
        CraftTimingHit hit = b.Hit(Hands);
        beats.Add(hit);
        lastBeat = hit;
        b.NewTargets(Salt(y.id), beats.Count);
        said = RowsPerBeat > 1 ? $"Rows {Mathf.Min(rows, RowsWoven - RowsPerBeat + 1)}-{Mathf.Min(rows, RowsWoven)}: {HitWords(hit, "a skipped pick")}" : $"Row {beats.Count}: {HitWords(hit, "a skipped pick")}";
        if (RowsWoven >= rows) FinishWeave();
    }

    private void FinishWeave()
    {
        CraftPiece y = WeaveYarn;
        if (y == null || beats.Count == 0) { said = "Weave some rows first."; return; }
        var pts = new List<float>();
        float sum = 0f;
        foreach (CraftTimingHit h in beats) { pts.Add(h.score); sum += h.score; }
        int rows = beats.Count; // beats: the station counts the rows each carried
        int shownRows = Mathf.Min(RowsAvailable(y), RowsWoven);
        if (pts.Count % 2 != 0) pts.Add(0f);
        float even = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(warpTension - TensionIdeal) - TensionBand * 0.5f) * 3f);
        if (Send(new CraftRequest(CraftRequestKind.WeaveCloth, y.id) { i0 = Mathf.RoundToInt(weaveWidth.value), i1 = rows, a = even, b = beatDensity.value, points = pts }))
            said = $"Cut off the loom: {Mathf.RoundToInt(weaveWidth.value)} × {shownRows} of cloth, beaten {sum / rows * 100:0}% true. Lay it out and cut it at the tailor's table.";
        beats.Clear();
        lastBeat = null;
        warpTension = -1f;
        weaveFor = "";
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (place)
        {
            case Place.Spin: case Place.Ply: SpinPointer(phase, pos); break;
            case Place.Skein: SkeinPointer(phase, pos); break;
            case Place.Weave: WeavePointer(phase, pos); break;
            case Place.Fit: FitPointer(phase, pos); break;
            case Place.Layout: LayoutPointer(phase, pos, right, scroll); break;
            case Place.Needle: NeedlePointer(phase, pos); break;
            case Place.Cut: CutPointer(phase, pos, right, scroll); break;
            default: SewPointer(phase, pos); break;
        }
    }

    private void SpinPointer(CraftPointer phase, Vector2 pos)
    {
        bool plying = place == Place.Ply;
        if (phase == CraftPointer.Down && pedal.Contains(pos))
        {
            // A treadle: a steady beat keeps the wheel at speed; stamping fast only jerks it.
            float now = Time.unscaledTime, gap = now - lastPedal;
            lastPedal = now;
            // Each treadle adds momentum: a steady beat (about once a second) brings it up to speed in a few strokes and
            // keeps it there; stamping fast only jerks the wheel.
            float steady = gap < 0.3f ? 0.25f : gap < 0.5f ? 0.65f : gap < 1.8f ? 1f : 0.8f;
            wheel = Mathf.Min(1f, wheel + (1f - wheel) * TreadleKick * steady * (0.6f + 0.4f * (float)Hands.Strength));
            if (gap < 0.3f) wheel *= 0.95f;
            return;
        }
        if (phase == CraftPointer.Down && handArea.Contains(pos))
        {
            string id = FibreId;
            if (plying)
            {
                CraftPiece a = Ws.Piece(Strand(strandA)), b = Ws.Piece(Strand(strandB));
                if (a == null || b == null || a == b) { said = "Pick two bobbins of yarn to ply (spin them first)."; return; }
                singlesTwist = (float)(a.yarn.twist + b.yarn.twist) * 0.5f;
            }
            else if (Batch == null && (id == null || Have(id) == 0)) { said = $"You have no {ItemOf(id ?? "fibre")} to spin: bring some (each bobbin uses one), or pick a batch from the fibre bench."; return; }
            if (Batch != null && Batch.fibre.wet > 0.3) { said = "The batch is still wet: let it dry."; return; }
            if (wheel < 0.15f) { said = "Treadle the wheel first: the twist comes from the wheel turning."; return; }
            drafting = true;
            lastHand = pos;
            lastHandTime = Time.unscaledTime;
            return;
        }
        if (!drafting) return;
        if (phase == CraftPointer.Up) { drafting = false; return; }
        if (phase != CraftPointer.Drag) return;
        float t = Time.unscaledTime, dt = Mathf.Max(0.016f, t - lastHandTime);
        float dx = pos.x - lastHand.x;
        lastHand = pos;
        lastHandTime = t;
        if (dx < -2f)
        {
            // Back toward the wheel: the spun length winds on.
            yarn.Clear();
            return;
        }
        if (dx <= 0.5f) return; // the hand held still: the twist builds up (it shows next time the hand moves)
        // Drafting out: the twist is the wheel's turns per length drawn.
        float speed = dx / dt; // px/s
        float twist = Mathf.Clamp01(0.5f * wheel * TwistPace / Mathf.Max(20f, speed) / 0.7f);
        twistShown = Mathf.Lerp(twistShown, twist, 0.25f);
        float noisy = speed * (1f + (float)Hands.Tremble((int)(draftN + 101)) * 6f);
        draftSum += noisy; draftSq += noisy * noisy; draftN++;
        twistSum += twist;
        bobbin = Mathf.Min(1f, bobbin + dx / BobbinPixels);
        yarn.Add(Mathf.Lerp(5f, 1.5f, Mathf.Clamp01(noisy / 400f)));
        if (yarn.Count > 40) yarn.RemoveAt(0);
        if (draftN % 20 == 0) Hands.Move(0.15);
        if (bobbin >= 1f)
        {
            float mean = draftSum / draftN, sd = Mathf.Sqrt(Mathf.Max(0f, draftSq / draftN - mean * mean));
            float even = Mathf.Clamp01(1f - sd / Mathf.Max(1f, mean) * 1.4f);
            float tw = twistSum / draftN;
            string id = FibreId;
            CraftPiece batch = Batch;
            if (plying)
            {
                if (Send(new CraftRequest(CraftRequestKind.PlyYarn, Strand(strandA)) { other = Strand(strandB), a = tw, b = even }))
                    said = Mathf.Abs(tw - singlesTwist * 0.8f) < 0.12f ? "Plied: the yarn hangs straight, balanced." : tw < singlesTwist * 0.8f ? "Under-plied: it untwists itself." : "Over-plied: it kinks back on itself.";
                yarnsKey = "";
            }
            else if (batch != null)
            {
                if (Send(new CraftRequest(CraftRequestKind.SpinFibre, batch.id) { a = even, b = tw, c = draft != null ? draft.value : 0.5f, i0 = 30 }))
                    said = "A full bobbin of yarn from the batch. " + (tw < 0.3f ? "Under-twisted: it pulls apart." : tw > 0.7f ? "Over-twisted: it kinks." : even < 0.6f ? "Lumpy: draft at an even pace." : "Even and well twisted.") + " Ply it, or wind it into a skein.";
                yarnsKey = "";
            }
            else if (id != null && Send(new CraftRequest(CraftRequestKind.Spin) { id = id, a = even, b = tw }))
            {
                string got = CraftTextiles.Spun(Rules, Rules.Supply(id), even, tw, CraftTextiles.RawPrep(Rules, id));
                said = $"A full bobbin: {Rules.Supply(got)?.Name.ToLowerInvariant() ?? "thread"}. " + (tw < 0.3f ? "Under-twisted: it pulls apart. Draft slower or treadle faster." : tw > 0.7f ? "Over-twisted: it kinks. Draft faster or treadle slower." : even < 0.6f ? "Lumpy: draft at an even pace." : "Even and well twisted.");
            }
            bobbin = 0f;
            draftSum = draftSq = draftN = twistSum = 0f;
            yarn.Clear();
            drafting = false;
        }
    }

    /// <summary>Stitches a needleful lasts (a practised hand wastes less of it).</summary>
    private int Needleful => CraftMastery.NeedlefulStitches(Mastery);

    /// <summary>An apprentice threads a needle without thinking.</summary>
    private void ThreadByRoutine()
    {
        if (threads.Count == 0 || Have(ThreadId) == 0) { said = $"You have no {ItemOf(ThreadId ?? "thread")}: spin some at the wheel, or bring some."; return; }
        threaded = true;
        threadLeft = Needleful;
        threadId = ThreadId;
        frays = 0;
        said = $"Threaded without a thought: enough for about {Needleful} stitches.";
    }

    private void NeedlePointer(CraftPointer phase, Vector2 pos)
    {
        if (threaded || threads.Count == 0) return;
        if (phase == CraftPointer.Down)
        {
            if (Have(ThreadId) == 0) { said = $"You have no {ItemOf(ThreadId)}: spin some at the wheel, or bring some."; lastEnd = new Vector2(-999f, -999f); return; }
            lastEnd = threadEnd;
            return;
        }
        if (lastEnd.x < -900f) return;
        if (phase != CraftPointer.Drag) return;
        Vector2 end = threadEnd;
        // Through the eye: crossing its middle from left to right, inside its height; catching its edge frays the end.
        float gapHalf = Mathf.Max(2f, 6f - frays * 1.2f);
        if (lastEnd.x < eye.center.x && end.x >= eye.center.x)
        {
            float y = Mathf.Lerp(lastEnd.y, end.y, (eye.center.x - lastEnd.x) / Mathf.Max(0.01f, end.x - lastEnd.x));
            float inside = eye.height * 0.5f - Mathf.Abs(y - eye.center.y);
            if (inside > gapHalf)
            {
                threaded = true;
                threadLeft = Needleful;
                threadId = ThreadId;
                said = $"Threaded with {Rules.Supply(threadId)?.Name.ToLowerInvariant() ?? "thread"}: enough for about {Needleful} stitches.";
            }
            else if (inside > -8f)
            {
                frays++;
                said = frays >= 3 ? "The end is frayed fat: snip it." : "It catches on the eye and frays.";
            }
        }
        lastEnd = end;
    }

    private void Lift()
    {
        placedX = placedY = -1;
        trace.Clear();
        touched.Clear();
        snips.Clear();
    }

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

    private void CutPointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || !CraftTextiles.IsCloth(Rules, p) || pat == null) { if (phase == CraftPointer.Down) said = "Pick a length of cloth (left)."; return; }
        if (phase == CraftPointer.Scroll) { if (placedX < 0) rotation = (rotation + (scroll > 0 ? 1 : 3)) % 4; return; }
        CraftArt.HideLayout(p.hide, bolt, out Vector2 origin, out float cell);
        if (right && phase == CraftPointer.Down)
        {
            if (placedX < 0) { rotation = (rotation + 1) % 4; return; }
            FinishCut(p, pat, cell);
            return;
        }
        if (placedX < 0)
        {
            if (phase != CraftPointer.Down) return;
            int gx = Mathf.FloorToInt((pos.x - origin.x) / cell), gy = Mathf.FloorToInt((pos.y - origin.y) / cell);
            if (!CraftLeather.Fits(p.hide, pat, gx, gy, rotation)) { ui.Message("The pattern does not fit there."); return; }
            placedX = gx;
            placedY = gy;
            trace.Clear();
            touched.Clear();
            snips.Clear();
            Border(pat, origin, cell);
            said = rotation % 2 == 0 ? "Pinned on the grain. Now cut round it: snip, snip - press and release, each snip from the last." : "Pinned across the grain: it will stretch and pucker. Cut round it with the shears (or unpin and turn it).";
            return;
        }
        // A snip: from the last snip to where the shears close.
        if (phase != CraftPointer.Up) return;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(trace.Count + 111) * 50f, (float)Hands.Tremble(trace.Count + 112) * 50f);
        if (trace.Count == 0) { trace.Add(at); lastSnip = at; return; }
        float len = Vector2.Distance(at, lastSnip);
        if (len > cell * 3f) { said = "Too long a snip: the shears would wander. Shorter snips."; return; }
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
        for (int k = 1; k <= steps; k++)
        {
            Vector2 q = Vector2.Lerp(lastSnip, at, k / (float)steps);
            trace.Add(q);
            if (Nearest(q, out int i) < cell && i >= 0) touched.Add(i);
        }
        snips.Add(len);
        lastSnip = at;
        Hands.Move(0.1);
        if (border.Count > 0 && touched.Count >= border.Count * 0.95f)
            FinishCut(p, pat, cell);
    }

    private void FinishCut(CraftPiece p, CraftPattern pat, float cell)
    {
        if (trace.Count < 6) { Lift(); return; }
        double off = 0;
        foreach (Vector2 t in trace) off += Mathf.Clamp01(Nearest(t, out _) / (cell * 1.5f));
        double closeness = 1 - off / trace.Count, coverage = border.Count > 0 ? touched.Count / (double)border.Count : 0;
        float mean = 0f, sq = 0f;
        foreach (float s in snips) { mean += s; sq += s * s; }
        mean /= Mathf.Max(1, snips.Count);
        float choppy = snips.Count > 1 ? Mathf.Sqrt(Mathf.Max(0f, sq / snips.Count - mean * mean)) / Mathf.Max(1f, mean) : 0.5f;
        double accuracy = Math.Max(0, Math.Min(1, closeness * 0.5 + coverage * 0.4 + (1 - Mathf.Clamp01(choppy)) * 0.1));
        if (Send(new CraftRequest(CraftRequestKind.Cut, p.id) { id = pat.id, i0 = placedX, i1 = placedY, i2 = rotation, a = (float)accuracy }))
            said = accuracy > 0.85 ? "A clean edge, right on the line." : accuracy > 0.6 ? "Cut out: the edge wanders a little." : "Cut out raggedly: it will fray.";
        Lift();
    }

    // ------------------------------------------------------------------ the niddy-noddy
    private int WrapsOn(CraftPiece y) => Mathf.Max(1, Mathf.CeilToInt((float)(y.yarn.length / CraftFibre.MetresPerWrap)));

    /// <summary>Wraps each turn of the niddy-noddy winds: the whole bobbin in about eight turns.</summary>
    private int WrapsPerTurn(CraftPiece y) => Mathf.Max(1, Mathf.CeilToInt(WrapsOn(y) / 8f));

    private static float AngleAt(Vector2 p) => Mathf.Atan2(p.y - Niddy.y, p.x - Niddy.x) * Mathf.Rad2Deg;

    private void DrawSkein(CraftPixels c)
    {
        CraftPiece y = YarnPiece;
        // The circle to turn it along, and the niddy-noddy turning in the hand: a shaft, a cross-arm at each end (the far
        // one at right angles, so seen end-on); the yarn wraps from arm to arm.
        c.Ring(Niddy.x, Niddy.y, 150f, 1.5f, new Color(1f, 1f, 1f, windingSkein ? 0.3f : 0.12f));
        float a = niddyAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
        Vector2 endA = Niddy - dir * 120f, endB = Niddy + dir * 120f;
        if (y != null && skeinFor != y.id) { skeinFor = y.id; wraps = counted = 0; }
        int shown = Mathf.Min(wraps, 60);
        for (int k = 0; k < shown; k++)
        {
            Vector2 o = side * (k * 0.5f - 15f);
            c.Line(endA.x + o.x, endA.y + o.y, endB.x - o.x, endB.y - o.y, new Color(0.9f, 0.86f, 0.76f, 0.6f), 1f);
        }
        c.Line(endA.x, endA.y, endB.x, endB.y, Wood, 6f);
        c.Line(endA.x - side.x * 45f, endA.y - side.y * 45f, endA.x + side.x * 45f, endA.y + side.y * 45f, Wood, 6f);
        c.Disc(endB.x, endB.y, 9f, Wood);
        c.Disc(Niddy.x, Niddy.y, 5f, Iron);
        if (y == null) return;
        float onBobbin = Mathf.Clamp01(1f - wraps * (float)CraftFibre.MetresPerWrap / Mathf.Max(1f, (float)y.yarn.length));
        c.Disc(640, 330, 10 + 30 * onBobbin, new Color(0.9f, 0.86f, 0.76f));
        c.Ring(640, 330, 10, 2f, Wood);
        Meter(c, 30, 380, 260, 8, 1f - onBobbin, Mark);
        // each turn's pace: steady (green) counts true, rushed (red) loses count
        if (windingSkein) Meter(c, 30, 366, 260, 6, Mathf.Clamp01(windTurn / 360f), Time.unscaledTime - turnStartedAt < 0.5f ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.6f, 0.85f, 1f));
    }

    private void SkeinPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece y = YarnPiece;
        if (y == null) { if (phase == CraftPointer.Down) said = "Pick a bobbin of yarn (left)."; return; }
        if (skeinFor != y.id) { skeinFor = y.id; wraps = counted = 0; }
        if (phase == CraftPointer.Down)
        {
            float d = Vector2.Distance(pos, Niddy);
            if (d < 40f || d > 260f) { said = "Turn the niddy-noddy: hold and circle round it."; return; }
            if (wraps * CraftFibre.MetresPerWrap >= y.yarn.length) { said = "The bobbin is empty: tie off the skein."; return; }
            windingSkein = true;
            lastWindAngle = AngleAt(pos);
            windTurn = 0f;
            turnStartedAt = Time.unscaledTime;
            return;
        }
        if (phase == CraftPointer.Up) { windingSkein = false; return; }
        if (!windingSkein || phase != CraftPointer.Drag) return;
        float ang = AngleAt(pos), delta = Mathf.DeltaAngle(lastWindAngle, ang);
        lastWindAngle = ang;
        niddyAngle += delta;
        windTurn += Mathf.Abs(delta);
        if (windTurn < 360f) return;
        windTurn -= 360f;
        // A turn: a run of wraps goes on, counted as they go - turned too fast (or when the hand slips) some slip past the count.
        float now = Time.unscaledTime, took = now - turnStartedAt;
        turnStartedAt = now;
        int add = Mathf.Min(WrapsPerTurn(y), WrapsOn(y) - wraps);
        if (add <= 0) return;
        bool rushed = took < 0.5f || Hands.Slips(171 + wraps, 0);
        int lost = rushed ? Mathf.Clamp(Mathf.RoundToInt(add * Mathf.Clamp01((0.5f - took) / 0.5f + 0.15f)), 1, add) : 0;
        wraps += add;
        counted += add - lost;
        Hands.Move(0.04);
        said = rushed ? $"Too quick: you lost count - {counted} wraps counted." : $"{counted} wraps counted.";
        if (wraps * CraftFibre.MetresPerWrap >= y.yarn.length)
        {
            windingSkein = false;
            TieOff();
        }
    }

    private void TieOff()
    {
        CraftPiece y = YarnPiece;
        if (y == null || wraps == 0) { said = "Wind the yarn on the niddy-noddy first."; return; }
        if (Send(new CraftRequest(CraftRequestKind.MeasureYarn, y.id) { a = (float)(counted * CraftFibre.MetresPerWrap) }))
            said = $"Tied off: you counted {counted} wraps - {counted * CraftFibre.MetresPerWrap:0} m. Reel it off as thread.";
        wraps = counted = 0;
        niddyAngle = 0f;
    }

    // ------------------------------------------------------------------ measuring the wearer
    private void DrawFit(CraftPixels c)
    {
        // The wearer: head, shoulders, body, an arm, legs; the tape between the marks.
        Color skin = new Color(0.55f, 0.45f, 0.38f);
        c.Disc(360, 350, 26, skin);
        c.Rect(300, 170, 420, 310, skin);
        c.Rect(420, 170, 450, 300, skin);
        c.Rect(310, 40, 350, 170, skin);
        c.Rect(370, 40, 410, 170, skin);
        for (int i = 0; i < Marks.Length; i++)
        {
            Vector4 m = Marks[i];
            Color col = i < markAt ? new Color(0.45f, 0.9f, 0.45f) : i == markAt ? Mark : new Color(1f, 1f, 1f, 0.4f);
            c.Disc(m.x, m.y, 4f, col);
            c.Ring(m.z, m.w, 6f, 1.5f, col);
        }
        if (taping && markAt < Marks.Length)
        {
            Vector4 m = Marks[markAt];
            c.Line(m.x, m.y, tapeEnd.x, tapeEnd.y, new Color(0.95f, 0.9f, 0.55f), 4f);
            for (float t = 0; t < 1f; t += 0.08f) c.Disc(Mathf.Lerp(m.x, tapeEnd.x, t), Mathf.Lerp(m.y, tapeEnd.y, t), 1f, Iron);
        }
        Meter(c, 30, 380, 260, 8, markAt / (float)Marks.Length, Mark);
    }

    private void FitPointer(CraftPointer phase, Vector2 pos)
    {
        if (markAt >= Marks.Length) { if (phase == CraftPointer.Down) { markAt = 0; tapeErr = 0f; said = "Measuring again from the start."; } return; }
        Vector4 m = Marks[markAt];
        if (phase == CraftPointer.Down && Vector2.Distance(pos, new Vector2(m.x, m.y)) < 20f) { taping = true; tapeEnd = pos; return; }
        if (!taping) return;
        // The tape follows the hand, with its tremble; it must be let go right on the end mark.
        tapeEnd = pos + new Vector2((float)Hands.Tremble(markAt * 7 + 181) * 60f, (float)Hands.Tremble(markAt * 7 + 182) * 60f);
        if (phase != CraftPointer.Up) return;
        taping = false;
        float err = Vector2.Distance(tapeEnd, new Vector2(m.z, m.w));
        tapeErr += err;
        markAt++;
        said = err < 4f ? $"Measured {MarkNames[markAt - 1]} exactly." : err < 12f ? $"Measured {MarkNames[markAt - 1]}, a little off." : $"The tape slipped measuring {MarkNames[markAt - 1]}.";
        Hands.Move(0.02);
        if (markAt < Marks.Length) return;
        float accuracy = Mathf.Clamp01(1f - tapeErr / Marks.Length / 25f);
        // The wearer's own size (their chest, mm), as the tape read it.
        float chest = (float)CraftTextiles.ChestOf(ui.Player != null ? ui.Player.name : "wearer") + (1f - accuracy) * 60f;
        CraftPiece a = Selected != null && Selected.kind == CraftPieceKind.Assembly ? Selected : null;
        if (Send(new CraftRequest(CraftRequestKind.MeasureFit, a?.id) { a = accuracy, b = chest }))
            said = (a != null ? "The item being made takes these measurements. " : "The next item started is made to these measurements. ") + (accuracy > 0.85f ? "A careful measure." : accuracy > 0.6f ? "A rough measure." : "A careless measure: it may fit worse than none.");
    }

    // ------------------------------------------------------------------ laying out
    private void DrawChalk(CraftPixels c, CraftPiece p, Vector2 origin, float cell)
    {
        foreach (CraftLayoutPlacement l in p.hide.layout)
        {
            CraftPattern lp = Rules.Pattern(l.pattern);
            if (lp == null) continue;
            lp.Size(l.rotation, out int w, out int h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!lp.Covers(x, y, l.rotation)) continue;
                    float x0 = origin.x + (l.x + x) * cell, y0 = origin.y + (l.y + y) * cell;
                    c.Rect(x0, y0, x0 + cell, y0 + cell, l.rotation % 2 == 0 ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 0.8f, 0.4f, 0.18f));
                    if (!lp.Covers(x - 1, y, l.rotation)) c.Rect(x0, y0, x0 + 1.5f, y0 + cell, Color.white);
                    if (!lp.Covers(x + 1, y, l.rotation)) c.Rect(x0 + cell - 1.5f, y0, x0 + cell, y0 + cell, Color.white);
                    if (!lp.Covers(x, y - 1, l.rotation)) c.Rect(x0, y0, x0 + cell, y0 + 1.5f, Color.white);
                    if (!lp.Covers(x, y + 1, l.rotation)) c.Rect(x0, y0 + cell - 1.5f, x0 + cell, y0 + cell, Color.white);
                }
        }
    }

    private void DrawLayout(CraftPixels c)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        c.Rect(bolt.xMin - 10, bolt.yMin - 10, bolt.xMax + 10, bolt.yMax + 10, new Color(0.42f, 0.33f, 0.24f));
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null) return;
        CraftArt.DrawHide(c, Rules, p.hide, bolt);
        CraftArt.HideLayout(p.hide, bolt, out Vector2 origin, out float cell);
        DrawChalk(c, p, origin, cell);
        Arrow(c, new Vector2(bolt.xMax - 20f, bolt.yMin + 40f), new Vector2(bolt.xMax - 20f, bolt.yMin + 120f), new Color(1f, 1f, 1f, 0.6f));
        Vector2 h = ui.Hover;
        if (bolt.Contains(h))
        {
            int gx = Mathf.FloorToInt((h.x - origin.x) / cell), gy = Mathf.FloorToInt((h.y - origin.y) / cell);
            bool fits = CraftLeather.Fits(p.hide, pat, gx, gy, rotation) && CraftTextiles.Overlaps(Rules, p.hide, pat, gx, gy, rotation) < 0;
            pat.Size(rotation, out int pw, out int ph);
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                    if (pat.Covers(x, y, rotation))
                    {
                        float x0 = origin.x + (gx + x) * cell, y0 = origin.y + (gy + y) * cell;
                        c.Rect(x0, y0, x0 + cell, y0 + cell, fits ? rotation % 2 == 0 ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(1f, 0.75f, 0.3f, 0.35f) : new Color(1f, 0.35f, 0.3f, 0.35f));
                    }
        }
        Meter(c, bolt.xMin, bolt.yMax + 2, bolt.width, 6, (float)CraftTextiles.Utilization(Rules, p.hide), new Color(0.6f, 0.85f, 1f));
    }

    private void LayoutPointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        CraftPattern pat = Current;
        if (p == null || p.kind != CraftPieceKind.Hide || pat == null) { if (phase == CraftPointer.Down) said = "Pick a length of cloth (or a hide) on the left."; return; }
        if (phase == CraftPointer.Scroll || right && phase == CraftPointer.Down) { rotation = (rotation + (scroll < 0 ? 3 : 1)) % 4; return; }
        if (phase != CraftPointer.Down || !bolt.Contains(pos)) return;
        CraftArt.HideLayout(p.hide, bolt, out Vector2 origin, out float cell);
        int gx = Mathf.FloorToInt((pos.x - origin.x) / cell), gy = Mathf.FloorToInt((pos.y - origin.y) / cell);
        if (Send(new CraftRequest(CraftRequestKind.LayoutPattern, p.id) { id = pat.id, i0 = gx, i1 = gy, i2 = rotation }))
            said = rotation % 2 == 0 ? "Chalked on the grain." : "Chalked across the grain: it will hang and wear worse.";
    }

    private CraftPiece Assembly => Selected != null && Selected.kind == CraftPieceKind.Assembly ? Selected : null;


    private void SewPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece a = Assembly;
        if (a == null) { if (phase == CraftPointer.Down) said = "Pick the item being made (start it at the Assembly area)."; return; }
        if (seams.Count == 0) { if (phase == CraftPointer.Down) said = "It has no seams to sew."; return; }
        if (!threaded) { if (phase == CraftPointer.Down) said = "Thread the needle first."; return; }
        if (phase == CraftPointer.Down)
        {
            if (pos.y < seamY - 60f || pos.y > seamY + 60f) return;
            if (lastStitchX >= 0f && pos.x <= lastStitchX + 2f) { said = "Sew along the seam, left to right."; return; }
            pulling = true;
            pressAt = Time.unscaledTime;
            return;
        }
        if (phase != CraftPointer.Up || !pulling) return;
        pulling = false;
        float tension = Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.8f) * (0.6f + 0.4f * (float)Hands.Strength);
        Vector2 at = pos + new Vector2((float)Hands.Tremble(stitches.Count + 121) * 60f, (float)Hands.Tremble(stitches.Count + 122) * 60f);
        // A run of stitches between the last and this one (a practised hand lays two or three with one pull).
        int per = StitchesPerClick;
        float gap = IdealGap * per;
        float spacing = lastStitchX >= 0f ? Mathf.Clamp01(Mathf.Abs((at.x - lastStitchX) - gap) / gap) : 0f;
        float align = Mathf.Clamp01(Mathf.Abs(at.y - seamY) / 14f);
        float fromX = lastStitchX >= 0f ? lastStitchX : at.x - gap;
        for (int k = 1; k <= per; k++)
            stitches.Add(new Vector3(Mathf.Lerp(fromX, at.x, k / (float)per), Mathf.Lerp(seamY, at.y, k / (float)per), tension));
        while (stitches.Count > 60) stitches.RemoveAt(0);
        lastStitchX = at.x;
        spacingSum += spacing; alignSum += align; tensionSum += tension; tensionSq += tension * tension;
        pending++;
        threadLeft = Mathf.Max(0, threadLeft - per);
        Hands.Move(0.1);
        if (pending >= 6) FlushStitches();
        if (threadLeft <= 0)
        {
            FlushStitches();
            threaded = false;
            said = "The thread runs out: thread the needle again.";
        }
        if (lastStitchX > 670f) { lastStitchX = -1f; stitches.Clear(); said = "To the end of the seam: carry on from the start of the next stretch."; }
    }

    /// <summary>The stitches so far go to the item: each one pierces (spacing, on the line) and pulls (tension).</summary>
    private void FlushStitches()
    {
        CraftPiece a = Assembly;
        if (a == null || pending <= 0 || seams.Count == 0) { pending = 0; spacingSum = alignSum = tensionSum = tensionSq = 0f; return; }
        string joint = seams[Mathf.Clamp(seam.value, 0, seams.Count - 1)];
        // The design's thread goes in with the first stitches (the thread on the needle).
        CraftDesign d = Rules.Design(a.assembly.design);
        if (d != null)
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == CraftSupplyKind.Thread && a.assembly.Supply(c.role) < c.supplyCount)
                    Send(new CraftRequest(CraftRequestKind.Attach, a.id) { id = c.role, count = Mathf.Max(1, c.supplyCount - (int)a.assembly.Supply(c.role)), other = threadId });
        float n = pending, mean = tensionSum / n;
        // i1 = 1: the needle - each pull lays as many stitches (and pierces as many holes) as the tailor's practice allows
        Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joint, i0 = (int)CraftJointWork.Punch, count = pending, i1 = 1, a = spacingSum / n, b = alignSum / n });
        Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joint, i0 = (int)CraftJointWork.Stitch, count = pending, i1 = 1, a = mean, b = Mathf.Sqrt(Mathf.Max(0f, tensionSq / n - mean * mean)) });
        pending = 0;
        spacingSum = alignSum = tensionSum = tensionSq = 0f;
    }

    public override void Refresh()
    {
        RefreshCounts(fibre, fibres);
        NoteBatches(fibre, fibres);
        RefreshCounts(thread, threads);
        // The bobbins of yarn on the bench, for plying.
        var ys = new List<string>();
        foreach (CraftPiece q in Ws.Pieces) if (q.kind == CraftPieceKind.Yarn && q.location == CraftLocation.Air) ys.Add(q.id);
        string yk = string.Join(",", ys);
        if (yk != yarnsKey && strandA != null)
        {
            yarnsKey = yk;
            yarns.Clear();
            yarns.AddRange(ys);
            var names = new List<string>();
            foreach (string id in ys) { CraftPiece q = Ws.Piece(id); names.Add($"{CraftLook.Name(Rules, q, null)} ({q.yarn.length:0} m)"); }
            if (names.Count == 0) names.Add("-");
            strandA.ClearOptions(); strandA.AddOptions(names); strandA.SetValueWithoutNotify(0);
            strandB.ClearOptions(); strandB.AddOptions(names); strandB.SetValueWithoutNotify(Mathf.Min(1, names.Count - 1));
        }
        // The seams of the item picked (they change with it).
        CraftPiece a = Assembly;
        string key = a != null ? a.id + a.assembly.design : "";
        if (key != seamsFor && seam != null)
        {
            seamsFor = key;
            seams.Clear();
            var names = new List<string>();
            CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
            if (d != null)
                foreach (CraftJointSpec j in d.joints)
                    if (j.kind == CraftJointKind.Stitch) { seams.Add(j.id); names.Add(j.Name); }
            seam.ClearOptions();
            seam.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
            stitches.Clear();
            lastStitchX = -1f;
        }
        if (info == null) return;
        string how;
        switch (place)
        {
            case Place.Ply:
                how = yarns.Count < 2 ? "<b>Ply</b>: spin two bobbins of yarn from batches of prepared fibre first."
                    : "<b>Ply</b>: treadle (pedal), then hold in the hand area and feed the two strands back to the right at an even pace: keep the ply twist between the ticks - it balances the singles' twist.";
                break;
            case Place.Skein:
                how = YarnPiece == null ? "<b>Skein</b>: pick a bobbin of yarn (left)." : YarnPiece.yarn.measured ? $"<b>A counted skein</b> of {YarnPiece.yarn.measuredLength:0} m: reel it off as thread ({CraftFibre.ThreadCount(YarnPiece)} × {CraftFibre.MetresPerThread:0} m)."
                    : $"<b>Skein</b>: hold and circle round the niddy-noddy to turn it - each turn winds {WrapsPerTurn(YarnPiece)} wraps of {CraftFibre.MetresPerWrap} m (counted {counted}). About half a second a turn: faster and some slip past the count. It ties off when the bobbin is empty.";
                break;
            case Place.Weave:
            {
                CraftPiece y = WeaveYarn;
                CraftHideDefinition cloth = y != null ? CraftFibre.ClothOf(Rules, y) : null;
                how = y == null ? "<b>Weave</b>: spin a bobbin of yarn first (or pick one, left)."
                    : cloth == null ? "Nothing is woven from that yarn."
                    : warpTension < 0f ? $"<b>Weave {cloth.Name.ToLowerInvariant()}</b>: first tension the warp - hold on the warp beam (right) and let go with the needle in the green band."
                    : $"<b>Weave {cloth.Name.ToLowerInvariant()}</b> {(int)weaveWidth.value} wide: click the loom to throw the shuttle and beat {(RowsPerBeat == 1 ? "the row" : $"{RowsPerBeat} rows")} in when the marker is on the target ({Mathf.Min(RowsWoven, RowsAvailable(y))}/{RowsAvailable(y)} rows). {(lastBeat.HasValue ? HitWords(lastBeat.Value, "a skipped pick") : "")} Cut it off the loom when done.";
                break;
            }
            case Place.Fit:
                how = markAt >= Marks.Length ? "Measured. Click to measure again." : $"<b>Measure</b>: press on the gold dot and drag the tape to its ring - let go right on it ({MarkNames[markAt]}). An apprentice tailor measures by routine.";
                break;
            case Place.Layout:
                how = "<b>Lay out</b>: click to chalk the pattern on the cloth (green: on the grain; the wheel or right-click turns it); patterns may not overlap. Plan them all before cutting: the blue gauge is the cloth used. Cut them at the Cut cloth place, on the chalk.";
                break;
            case Place.Spin:
                how = Batch != null ? $"<b>Spin the batch</b> ({Batch.fibre.grams:0} g left): pedal to keep the wheel turning, hold in the hand area and draw to the right at an even pace; keep the twist between the ticks. The draft slider sets how fine."
                    : fibres.Count == 0 || Have(FibreId) == 0 ? $"<b>Spin</b>: you need {ItemOf(FibreId ?? "fibre")} in your bag (one per bobbin), or a batch from the fibre bench."
                    : "<b>Spin</b>: click the pedal about once a second to keep the wheel turning (blue); hold in the hand area and draw to the right at an even pace (left to wind on). Keep the twist (green/red bar) between the ticks: too fast and it pulls apart, too slow and it kinks. Gold = the bobbin.";
                break;
            case Place.Needle:
                how = threaded ? $"<b>Threaded</b> ({threadLeft} stitches left)."
                    : threads.Count == 0 || Have(ThreadId) == 0 ? $"<b>Thread the needle</b>: you need {ItemOf(ThreadId ?? "thread")} in your bag - spin it at the wheel, or bring some."
                    : "<b>Thread the needle</b>: hold and bring the thread's end through the eye, left to right, in the middle of it. It trembles - wait for a still moment.";
                break;
            case Place.Cut:
                how = placedX < 0 ? "<b>Cut cloth</b>: move the pattern over the cloth - the green arrow means on the grain (the white arrow), amber across it; right-click or the wheel turns it; click to pin it."
                    : $"Cut round it: press and release for each snip, from the last snip along the line ({(border.Count > 0 ? touched.Count * 100 / border.Count : 0)}% round). Right-click to finish.";
                break;
            default:
                how = a == null ? "<b>Sew</b>: pick the item being made (start it at the Assembly area and put its cloth parts in)."
                    : !threaded ? "Thread the needle first." : $"<b>Sew</b>: click along the chalk line, left to right - {(StitchesPerClick == 1 ? "one stitch" : $"a run of {StitchesPerClick} stitches")} per click, about as long as the white mark; hold as long as you pull the thread (firm, not puckered). {threadLeft} stitches of thread left.";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}
