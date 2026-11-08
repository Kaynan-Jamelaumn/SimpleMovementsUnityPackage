using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tailor's, in nine places, each worked with the hands (the yarn's at the wheel and the loom, the cloth's at the
/// tailor's table):
/// <list type="bullet">
/// <item><b>Spin</b>: treadle the wheel in a steady rhythm (the quick-action key - the foot keeps it turning while the
/// hand drafts - or a click on the pedal) and draft the fibre out of the hand (hold and draw the hand away from the
/// wheel, steadily). Draft too fast for the wheel and the yarn is under-twisted and weak; too slow (or held still) and
/// it kinks; a little off right it reels a coarser thread; an uneven draft makes it lumpy. A full bobbin is a bobbin of
/// yarn on the station: from a batch picked (or the first of the chosen fibre lying there, not one in the fibre bench's
/// bath), else from the bag (one fibre item is laid on the station as a batch and the bobbin spun from it). Set the
/// draft: finer spins more metres from the same fibre (and takes longer), weaves a thinner cloth, and wants
/// well-prepared fibre.</item>
/// <item><b>Ply</b>: two bobbins of yarn twisted together the other way: treadle, and feed the strands back at a pace
/// that keeps the ply twist in its band (it balances the singles' twist), evenly, so both strands share the load.</item>
/// <item><b>Skein &amp; reel</b>: turn the niddy-noddy in the hand (hold and circle round it): each turn winds a run of
/// wraps off the bobbin (1.5 m a wrap) and they are counted as they go - turned too fast, some slip past the count. When
/// the bobbin is empty the skein is tied off at what was counted; reel it off as thread. An apprentice tailor winds and
/// counts it by routine.</item>
/// <item><b>Weave</b> (at a loom): wind other bobbins of the same yarn onto it for a longer length, set the width,
/// tension the warp (hold on the warp beam and let go with the needle in the green band: an uneven warp puckers the
/// cloth), then throw the shuttle and beat each row in with the reed on the timing bar - 100% on the target packs the
/// row true, off it the row lies looser, a bad miss skips a pick (a flaw in the cloth); the cloth is as sound as its
/// yarn. While the warp is on, its yarn is not plied, wound or reeled. Cut it off the loom: the cloth goes on to be
/// laid out, cut and sewn.</item>
/// <item><b>Measure</b>: lay the tape round the wearer - from each mark drag the tape to its end mark and let go on it
/// (its end sways with the hand's tremble). The garment picked, or the next one started, is made to these measurements
/// (a careless measure fits worse than none).</item>
/// <item><b>Lay out</b>: chalk the patterns on the cloth before cutting - patterns cannot overlap; the gauge shows how
/// much of the cloth is used; on the grain is green. Cutting on a chalked line is cleaner.</item>
/// <item><b>Needle</b>: thread the needle - bring the thread's end through the eye; the needle trembles in the other
/// hand (more as you tire), and an end that catches on the eye frays (snip it).</item>
/// <item><b>Cut</b>: lay the pattern on the cloth along its grain (the arrows: the warp runs up the bolt; near its own
/// chalk it takes the chalk's place and turn), click to pin it, then cut round its outline with the shears - snip by
/// snip (press and release), each snip from the last. The part comes free when the whole outline is cut (or cut it free
/// from nine-tenths round: the rest tears). A part already cut is not cut again: it is fitted into its item.</item>
/// <item><b>Sew</b>: pick the item being made and a seam; stitch along the seam line - each click a run of stitches
/// (one for a novice, two from journeyman, three for a master tailor), held as long as the thread is pulled (tension,
/// its band on the gauge). Even stitches, on the line, pulled firm but not cutting. A practised hand's needleful lasts
/// longer.</item>
/// </list>
/// The right button puts down what the hands hold (the draft, the tape, a snip, a pull): it never sends or destroys
/// anything; the wheel turns a pattern.
/// </summary>
public sealed class CraftTailorMode : CraftMode
{
    private enum Place { Spin, Ply, Skein, Weave, Fit, Layout, Needle, Cut, Sew }
    private static readonly string[] PlaceNames = { "Spin", "Ply", "Skein & reel", "Weave", "Measure", "Lay out", "Thread needle", "Cut cloth", "Sew" };

    private Place place = Place.Spin;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> tabRows = new List<RectTransform>();
    private readonly Dictionary<Place, List<RectTransform>> placeRows = new Dictionary<Place, List<RectTransform>>();
    private TextMeshProUGUI info;
    private string said = "";

    // The strip along the top of the surface where the meters and their labels are (the work is drawn below it).
    private const float StripY = 366f;

    public override string Title => "Tailor";
    public override CraftDiscipline Discipline => CraftDiscipline.Tailoring;
    public override bool Available(CraftStation s) => s.Has(CraftTools.SpinningWheel) || s.Has(CraftTools.TailorTable) || s.Has(CraftTools.Loom);
    public override string Hint => "Spin fibre into yarn, weave it into cloth, thread the needle, cut cloth along its grain with the shears, sew the seams - each with the hands. Right-click puts down.";

    // ------------------------------------------------------------------ building
    public override void Build(RectTransform controls)
    {
        placeRows.Clear();
        tabRows.Clear();
        placeButtons.Clear();
        // Only the work this station has the tools for: the wheel spins; the loom weaves; the table measures, cuts and sews.
        if (!Here(place)) place = FirstPlace();
        // The places in two rows: the yarn's (the wheel and the loom), and the cloth's (the table).
        RectTransform yarnTabs = Row(controls, 28f, 4f), clothTabs = Row(controls, 28f, 4f);
        Text(yarnTabs, "Yarn", 12f, 40f);
        Text(clothTabs, "Cloth", 12f, 40f);
        tabRows.Add(yarnTabs);
        tabRows.Add(clothTabs);
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(i < (int)Place.Fit ? yarnTabs : clothTabs, PlaceNames[i], () => GoToPlace(pl), 0f, UITheme.ButtonStyle.Tab, 26f);
            b.gameObject.SetActive(Here(pl));
            placeButtons.Add(b);
        }
        yarnTabs.gameObject.SetActive(Here(Place.Spin) || Here(Place.Ply) || Here(Place.Skein) || Here(Place.Weave));
        clothTabs.gameObject.SetActive(Here(Place.Fit));

        fibres.Clear(); threads.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
        {
            if (s.kind == CraftSupplyKind.Fibre) fibres.Add(s.id);
            if (s.kind == CraftSupplyKind.Thread) threads.Add(s.id);
        }
        // Spin: the fibre (a batch on the station first, else from the bag) and the draft.
        RectTransform s1 = PlaceRow(controls, Place.Spin);
        Text(s1, "Fibre", 13f, 44f);
        fibre = CountedDrop(s1, fibres, 210f, "No fibre");
        draft = Slide(s1, "Draft thick-fine", 0f, 1f, 0.5f, false, 104f, DraftText);
        // Ply: the two bobbins.
        RectTransform y1 = PlaceRow(controls, Place.Ply);
        Text(y1, "Bobbins", 13f, 60f);
        strandA = Drop(y1, new List<string> { "-" }, 0, 250f);
        strandB = Drop(y1, new List<string> { "-" }, 0, 250f);
        strandA.onValueChanged.AddListener(_ => StrandsChanged());
        strandB.onValueChanged.AddListener(_ => StrandsChanged());
        // Skein & reel.
        RectTransform k1 = PlaceRow(controls, Place.Skein);
        Btn(k1, "Tie off the skein", TieOff, 140f);
        Btn(k1, "Reel off as thread", Reel, 150f, UITheme.ButtonStyle.Primary);
        RoutineButton(k1, CraftRoutine.WindSkein, "Wind & count by routine", () => YarnPiece?.id, 200f);
        // Weave: the width, the beat, cutting off; and other bobbins to wind on.
        RectTransform v1 = PlaceRow(controls, Place.Weave);
        weaveWidth = Slide(v1, "Width", CraftFibre.MinWidth, CraftFibre.MaxWidth, 6f, true, 44f, v => $"{v:0} wide");
        beatDensity = Slide(v1, "Beat open-packed", 0f, 1f, 0.5f, false, 110f, v => v < 0.35f ? "open" : v > 0.65f ? "packed" : "even");
        cutOffButton = Btn(v1, "Cut it off the loom", FinishWeave, 150f, UITheme.ButtonStyle.Primary);
        RectTransform v2 = PlaceRow(controls, Place.Weave);
        Text(v2, "Wind on", 13f, 60f);
        windOn = Drop(v2, new List<string> { "-" }, 0, 300f);
        windOnButton = Btn(v2, "Wind it on", JoinYarn, 120f);
        // Measure.
        RectTransform m1 = PlaceRow(controls, Place.Fit);
        RoutineButton(m1, CraftRoutine.MeasureWearer, "Measure by routine", () => Assembly?.id, 180f);
        // Lay out: the pattern (shared with Cut), its turn, rubbing the chalk out.
        RectTransform l1 = PlaceRow(controls, Place.Layout);
        Text(l1, "Pattern", 13f, 56f);
        layoutPattern = Drop(l1, new List<string> { "-" }, 0, 250f);
        layoutPattern.onValueChanged.AddListener(v => PatternPicked(v));
        Btn(l1, "Turn it", () => { layoutRotation = (layoutRotation + 1) % 4; said = layoutRotation % 2 == 0 ? "Along the grain." : "Across the grain."; }, 80f);
        Btn(l1, "Rub out the chalk", RubOut, 140f, UITheme.ButtonStyle.Ghost);
        // Thread the needle.
        RectTransform n1 = PlaceRow(controls, Place.Needle);
        Text(n1, "Thread", 13f, 50f);
        thread = CountedDrop(n1, threads, 210f, "No thread");
        thread.onValueChanged.AddListener(_ => { threaded = false; frays = 0; });
        Btn(n1, "Snip the end", () => { frays = 0; said = "A clean end again."; }, 120f);
        PractisedButton(n1, CraftDiscipline.Tailoring, CraftRank.Apprentice, "Thread it (practised)", ThreadByRoutine, 170f);
        // Cut cloth: the pattern, its turn, cutting it free, unpinning.
        RectTransform c1 = PlaceRow(controls, Place.Cut);
        Text(c1, "Pattern", 13f, 56f);
        cutPattern = Drop(c1, new List<string> { "-" }, 0, 230f);
        cutPattern.onValueChanged.AddListener(v => PatternPicked(v));
        Btn(c1, "Turn it", TurnCut, 76f);
        cutFreeButton = Btn(c1, "Cut it free", () => CutFree(), 110f, UITheme.ButtonStyle.Primary);
        Btn(c1, "Unpin", Unpin, 76f, UITheme.ButtonStyle.Ghost);
        // Sew: the seam.
        RectTransform w1 = PlaceRow(controls, Place.Sew);
        Text(w1, "Seam", 13f, 44f);
        seam = Drop(w1, new List<string> { "-" }, 0, 320f);
        seam.onValueChanged.AddListener(_ => SeamPicked());
        info = InfoRow(controls, 56f);
        patternKey = seamsFor = yarnsKey = windOnKey = "";
        Show();
    }

    private RectTransform PlaceRow(RectTransform controls, Place p)
    {
        RectTransform r = Row(controls);
        if (!placeRows.TryGetValue(p, out List<RectTransform> list)) placeRows[p] = list = new List<RectTransform>();
        list.Add(r);
        return r;
    }

    private bool Here(Place p) => Station != null && (p == Place.Spin || p == Place.Ply ? Station.Has(CraftTools.SpinningWheel)
        : p == Place.Weave ? Station.Has(CraftTools.Loom)
        : p == Place.Skein ? Station.Has(CraftTools.SpinningWheel) || Station.Has(CraftTools.TailorTable) : Station.Has(CraftTools.TailorTable));

    private Place FirstPlace()
    {
        foreach (Place p in (Place[])Enum.GetValues(typeof(Place)))
            if (Here(p)) return p;
        return Place.Spin;
    }

    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Place p) && Here(p)) GoToPlace(p);
    }

    public override bool HasPlace(string place) => Enum.TryParse(place, out Place p) && Here(p);

    public override string PlaceTitle(string place) => Enum.TryParse(place, out Place p) ? PlaceNames[(int)p] : place;

    /// <summary>A place chosen with its tab (or the guide): the hands put down what they held, the stitches go to their seam.</summary>
    private void GoToPlace(Place p)
    {
        if (p != place)
        {
            FlushStitches();
            PutDown();
            said = "";
        }
        place = p;
        Show();
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (KeyValuePair<Place, List<RectTransform>> kv in placeRows)
            foreach (RectTransform r in kv.Value) r.gameObject.SetActive(kv.Key == place);
    }

    /// <summary>What the hands hold is put down - nothing of it is sent: the draft, a pull of the thread, the tape, a snip, a turn of the niddy-noddy, the warp beam.</summary>
    private void PutDown()
    {
        drafting = pulling = taping = windingSkein = winding = snipping = placing = false;
        lastEnd = new Vector2(-999f, -999f);
    }

    public override void Leave()
    {
        FlushStitches();
        PutDown();
    }

    public override void SelectionChanging(string from, string to)
    {
        // The stitches go to the seam they were sewn on; everything else the hands did here is kept with what it was done
        // to (a bobbin with its batch or strands, the loom with its yarn, a skein with its bobbin, a cut with its cloth).
        FlushStitches();
        PutDown();
    }

    public override void SelectionChanged(string to) => said = "";

    /// <summary>Work of several presses on the piece picked, which carrying it off would lose.</summary>
    public override string Busy
    {
        get
        {
            string id = ui.SelectedId;
            if (string.IsNullOrEmpty(id)) return null;
            if (id == warpYarn && beats.Count > 0) return "weaving the cloth on the loom";
            if (cut != null && cut.cloth == id && cut.snips.Count > 0) return $"cutting out the {cut.Name.ToLowerInvariant()}";
            if (skeins.TryGetValue(id, out Skein k) && k.wraps > 0) return "winding the skein";
            if (spinBobbins.TryGetValue(id, out WheelBobbin s) && s.metres > 0f) return "spinning the bobbin";
            if (plyBobbin.metres > 0f && Array.IndexOf(plyBobbin.key.Split('|'), id) >= 0) return "plying it";
            return null;
        }
    }

    /// <summary>
    /// The hand work the window keeps when it closes - the warp on the loom and its rows, a skein half wound, a bobbin half
    /// filled, a cut under way: all there again when it is opened at this station (working elsewhere starts afresh).
    /// </summary>
    public override string Kept
    {
        get
        {
            var kept = new List<string>();
            if (warpTension >= 0f && Ws?.Piece(warpYarn) != null)
                kept.Add(beats.Count > 0 ? $"the warp on the loom, {Mathf.Min(RowsWoven, CraftFibre.MaxRows)} row{(RowsWoven == 1 ? "" : "s")} woven" : "the warp on the loom");
            foreach (Skein k in skeins.Values)
                if (k.wraps > 0) { kept.Add("a skein half wound on the niddy-noddy"); break; }
            foreach (WheelBobbin b in spinBobbins.Values)
                if (b.metres > 0f && !b.waiting) { kept.Add("a bobbin half spun on the wheel"); break; }
            if (plyBobbin.metres > 0f && !plyBobbin.waiting) kept.Add("a bobbin half plied");
            if (cut != null && cut.snips.Count > 0 && !cut.sent) kept.Add($"the {cut.Name.ToLowerInvariant()} half cut out");
            return kept.Count == 0 ? null : $"Left as it is for when you come back here: {string.Join(", ", kept)} (working at another station starts afresh).";
        }
    }

    public override void Tick(float dt)
    {
        // The wheel runs down unless it is treadled. A heavy wheel is a flywheel: it keeps turning a long while after the
        // last treadle (from full speed it takes some ten seconds to stop), slowing gently - faster at speed, where the
        // drive band and the flyer drag most.
        wheel = Mathf.Max(0f, wheel - dt * (WheelDrag + WheelDragAtSpeed * wheel));
        wheelAngle += wheel * dt * WheelTurnsPerSecond * 360f;
        // the foot on the pedal (the quick-action key) while the hand drafts with the pointer
        if ((place == Place.Spin || place == Place.Ply) && StationControls.Pressed(StationControl.Quick)) Treadle(Time.unscaledTime);
        if (!drafting) twistShown = Mathf.MoveTowards(twistShown, 0f, dt * 0.5f);
        if (place == Place.Weave)
        {
            beatBar?.Tick(dt);
            // Winding the warp beam: the tension climbs faster the tighter it gets (and trembles in the hand).
            if (winding && ui.PointerHeld) tensionNeedle = Mathf.Min(1.05f, tensionNeedle + dt * (0.18f + 0.45f * tensionNeedle) + (float)Hands.Tremble(beats.Count + 251) * dt * 3f);
        }
        // Drawn every frame while something moves: the wheel, the timing bar, the trembling needle, the hands at work.
        LiveDraw = ui.PointerHeld
                   || (place == Place.Spin || place == Place.Ply) && wheel > 0.01f
                   || place == Place.Weave && (warpTension >= 0f || winding)
                   || place == Place.Needle && !threaded
                   || ui.Hover.x >= 0f && (place == Place.Layout || place == Place.Cut || place == Place.Sew || place == Place.Fit); // drawn under the pointer
    }

    // ------------------------------------------------------------------ the pieces
    private readonly List<string> fibres = new List<string>(), threads = new List<string>();
    private TMP_Dropdown fibre, thread;
    private CraftPiece YarnPiece => Selected != null && Selected.kind == CraftPieceKind.Yarn ? Selected : null;
    private CraftPiece Assembly => Selected != null && Selected.kind == CraftPieceKind.Assembly ? Selected : null;
    private string FibreId => fibres.Count > 0 && fibre != null ? fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)] : null;
    private string ThreadId => threads.Count > 0 && thread != null ? threads[Mathf.Clamp(thread.value, 0, threads.Count - 1)] : null;

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
            string bath = Bathing; // a batch in the fibre bench's bath is not spun meanwhile
            foreach (CraftPiece p in Station.FibreBatches(FibreId))
                if (p.id != bath) return p;
            if (FibreId == null || Have(FibreId) == 0)
                foreach (CraftPiece p in Station.FibreBatches())
                    if (p.id != bath) return p;
            return null;
        }
    }

    /// <summary>The batch in the fibre bench's scouring bath or rinse tub (null: none): it is not spun till it is hung up to dry.</summary>
    private string Bathing => ui.Area<CraftFibreMode>()?.Bathing;

    // ================================================================== the wheel: spinning and plying
    private Vector2 wheelCentre = new Vector2(170f, 212f), orifice = new Vector2(330f, 240f);
    private static readonly Rect Pedal = new Rect(100, 24, 140, 30), HandArea = new Rect(330, 90, 380, 260), Kate = new Rect(650, 16, 56, 62);
    private float wheel, wheelAngle, lastPedal = -9f, twistShown;
    // Drafting at the wheel's pace: the twist is the wheel's turns per length drawn (0.5 = just right). Within TwistTrue of
    // it the yarn keeps nine-tenths of its quality (CraftTextiles.TwistTrue: the green band, "well twisted"); out to
    // TwistLow / TwistHigh it is a little under- or over-twisted and reels a coarser thread (amber); beyond, it pulls
    // apart or kinks (red).
    private const float TwistPace = 260f, TwistLow = 0.3f, TwistHigh = 0.7f, TwistTrue = (float)CraftTextiles.TwistTrue;
    // The wheel's momentum: how much a treadle adds, how fast it slows, how fast it turns at full speed.
    private const float TreadleKick = 0.32f, WheelDrag = 0.045f, WheelDragAtSpeed = 0.055f, WheelTurnsPerSecond = 0.9f;
    // How much drafting a metre of yarn takes (pixels of hand travel: a 30 g bobbin of a middling draft, some 130 m, is
    // twenty-odd draws), plying a metre half that; the draft is judged over windows of this many seconds.
    private const float DraftPixelsPerMetre = 50f, PlyPixelsPerMetre = 25f, DraftWindow = 0.05f, TireEvery = 1f / 3f;
    private bool drafting, windowOpen;
    private Vector2 lastHand;
    private float windowX, windowAt, draftTime;
    private Slider draft;
    private TMP_Dropdown strandA, strandB;
    private readonly List<string> yarns = new List<string>();
    private string yarnsKey = "";

    /// <summary>A bobbin filling at the wheel: the yarn drafted onto it so far, how, and what it is spun (or plied) from.</summary>
    private sealed class WheelBobbin
    {
        public string key = "";
        /// <summary>Metres of yarn on it, and the draft it was spun at (spinning).</summary>
        public float metres, draft = -1f;
        /// <summary>The twist put in: the wheel's turns against the length drawn (their ratio is the yarn's twist).</summary>
        public float turns, length;
        /// <summary>The draft's pace, window by window, each by the length it drew (the yarn's evenness along its length).</summary>
        public float speedSum, speedSq, weight;
        public int windows;
        /// <summary>Full, its request sent and not answered yet.</summary>
        public bool waiting;
        /// <summary>The yarn between the orifice and the hand: thick where the draft lagged, thin where it ran ahead.</summary>
        public readonly List<float> drawn = new List<float>();

        public float Twist => length > 1f ? Mathf.Clamp01(turns / length) : 0.5f;

        public float Evenness
        {
            get
            {
                if (windows < 2 || weight <= 0f) return 0.5f;
                float mean = speedSum / weight, sd = Mathf.Sqrt(Mathf.Max(0f, speedSq / weight - mean * mean));
                return Mathf.Clamp01(1f - sd / Mathf.Max(1f, mean) * 1.4f);
            }
        }

        public void Clear()
        {
            metres = turns = length = speedSum = speedSq = weight = 0f;
            draft = -1f;
            windows = 0;
            waiting = false;
            drawn.Clear();
        }
    }

    private readonly Dictionary<string, WheelBobbin> spinBobbins = new Dictionary<string, WheelBobbin>();
    private readonly WheelBobbin plyBobbin = new WheelBobbin();

    /// <summary>What is being spun: the batch's id, or "bag:" and the fibre.</summary>
    private string SpinKey => Batch?.id ?? (FibreId != null ? "bag:" + FibreId : "");

    /// <summary>The bobbin being spun now (each batch keeps its own half-filled bobbin while another is spun).</summary>
    private WheelBobbin SpinBobbin
    {
        get
        {
            string key = SpinKey;
            if (!spinBobbins.TryGetValue(key, out WheelBobbin b))
                spinBobbins[key] = b = new WheelBobbin { key = key };
            CraftPiece batch = Batch;
            // a bobbin begun from the bag goes on from the batch the bag's fibre was laid in as
            if (batch != null && b.metres <= 0f && spinBobbins.TryGetValue("bag:" + batch.fibre.fibre, out WheelBobbin bag) && bag.metres > 0f && !bag.waiting)
            {
                spinBobbins.Remove(bag.key);
                spinBobbins[key] = bag;
                bag.key = key;
                b = bag;
            }
            return b;
        }
    }

    private WheelBobbin Bobbin => place == Place.Ply ? plyBobbin : SpinBobbin;
    private string Strand(TMP_Dropdown d) => d != null && yarns.Count > 0 ? yarns[Mathf.Clamp(d.value, 0, yarns.Count - 1)] : null;
    private string PlyKey => $"{Strand(strandA)}|{Strand(strandB)}";

    private void StrandsChanged()
    {
        // a full bobbin on its way to the station keeps the two it was plied from (its answer clears it, or says it is lost)
        if (plyBobbin.waiting) return;
        if (plyBobbin.metres > 0f)
            said = "Other bobbins on the kate: the plying starts again.";
        plyBobbin.Clear();
        plyBobbin.key = PlyKey;
    }

    /// <summary>The draft slider's reading: the metres a bobbin of the fibre now spun gives at that draft.</summary>
    private string DraftText(float v)
    {
        SpinSource(out double grams, out double prep);
        return $"{CraftFibre.MetresSpun(grams, v, prep):0} m";
    }

    /// <summary>The grams the next bobbin spins and how well prepared they are (a raw fibre item from the bag, or the batch).</summary>
    private void SpinSource(out double grams, out double prep)
    {
        CraftPiece batch = Station != null && Station.Workshop != null ? Batch : null;
        grams = batch != null ? Math.Min(CraftFibre.BobbinGrams, batch.fibre.grams) : CraftFibre.BobbinGrams;
        prep = batch != null ? CraftFibre.Prep(batch.fibre) : FibreId != null ? CraftTextiles.RawPrep(Rules, FibreId) : 0.3;
    }

    /// <summary>The metres a full bobbin holds: spun, the bobbin's fibre at its draft; plied, the shorter strand's (of the two it is plied from).</summary>
    private float BobbinMetres(WheelBobbin b)
    {
        if (place == Place.Ply)
        {
            string[] pair = b.key.Split('|');
            CraftPiece a = pair.Length == 2 ? Ws.Piece(pair[0]) : null, c = pair.Length == 2 ? Ws.Piece(pair[1]) : null;
            if (a == null || c == null) { a = Ws.Piece(Strand(strandA)); c = Ws.Piece(Strand(strandB)); }
            return a != null && c != null ? (float)(Math.Min(a.yarn.length, c.yarn.length) * 0.95) : 1f;
        }
        SpinSource(out double grams, out double prep);
        return Mathf.Max(1f, (float)CraftFibre.MetresSpun(grams, b.draft >= 0f ? b.draft : draft.value, prep));
    }

    private float Fill(WheelBobbin b) => Mathf.Clamp01(b.metres / BobbinMetres(b));

    private void DrawSpin(CraftPixels c)
    {
        bool plying = place == Place.Ply;
        WheelBobbin b = Bobbin;
        // The wheel and its spokes turning, the drive band, the flyer and its bobbin, the pedal.
        c.Ring(wheelCentre.x, wheelCentre.y, 110f, 6f, Wood);
        for (int k = 0; k < 8; k++)
        {
            float a = (wheelAngle + k * 45f) * Mathf.Deg2Rad;
            c.Line(wheelCentre.x, wheelCentre.y, wheelCentre.x + Mathf.Cos(a) * 106f, wheelCentre.y + Mathf.Sin(a) * 106f, Wood, 2f);
        }
        c.Line(wheelCentre.x, wheelCentre.y + 110f, orifice.x - 20f, orifice.y + 10f, new Color(0.8f, 0.75f, 0.6f), 1f);
        c.Rect(orifice.x - 40f, orifice.y - 10f, orifice.x, orifice.y + 10f, Wood);
        c.Disc(orifice.x - 20f, orifice.y, 8f + 8f * Fill(b), new Color(0.85f, 0.8f, 0.68f)); // the yarn on the bobbin
        c.Rect(Pedal.xMin, Pedal.yMin, Pedal.xMax, Pedal.yMax, Time.unscaledTime - lastPedal < 0.15f ? Mark : Wood);
        c.Line(Pedal.center.x, Pedal.yMax, wheelCentre.x, wheelCentre.y - 20f, Iron, 2f);
        // Where the hand drafts.
        c.Frame(HandArea.xMin, HandArea.yMin, HandArea.xMax, HandArea.yMax, new Color(1f, 1f, 1f, 0.08f), 1f);
        bool nothing = !plying && Batch == null && (fibres.Count == 0 || Have(FibreId) == 0);
        if (nothing) c.Rect(HandArea.xMin, HandArea.yMin, HandArea.xMax, HandArea.yMax, new Color(0.5f, 0.1f, 0.1f, 0.18f)); // nothing in the hand
        Vector2 hand = drafting ? new Vector2(Mathf.Clamp(lastHand.x, HandArea.xMin + 8f, HandArea.xMax - 40f), Mathf.Clamp(lastHand.y, HandArea.yMin + 16f, HandArea.yMax - 18f)) : new Vector2(orifice.x + 60f, orifice.y);
        if (plying)
        {
            // Two strands from the lazy kate, twisting together into one.
            c.Rect(Kate.xMin, Kate.yMin, Kate.xMax, Kate.yMax, Wood);
            c.Disc(Kate.xMin + 16f, Kate.yMax - 14f, 10f, new Color(0.9f, 0.86f, 0.75f));
            c.Disc(Kate.xMax - 16f, Kate.yMax - 14f, 10f, new Color(0.85f, 0.8f, 0.7f));
            c.Line(hand.x, hand.y, Kate.xMin + 16f, Kate.yMax - 14f, new Color(0.9f, 0.86f, 0.75f), 2f);
            c.Line(hand.x, hand.y, Kate.xMax - 16f, Kate.yMax - 14f, new Color(0.85f, 0.8f, 0.7f), 2f);
        }
        // The yarn from the orifice to the hand: thick where the draft lagged, thin where it ran ahead.
        int n = b.drawn.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = Vector2.Lerp(orifice, hand, i / (float)n), e = Vector2.Lerp(orifice, hand, (i + 1) / (float)n);
            c.Line(a.x, a.y, e.x, e.y, new Color(0.9f, 0.86f, 0.75f), Mathf.Clamp(b.drawn[i], 1f, 6f));
        }
        if (n == 0) c.Line(orifice.x, orifice.y, hand.x, hand.y, new Color(0.9f, 0.86f, 0.75f), 2f);
        if (!plying && !nothing)
        {
            CraftSupplyDefinition f = Batch != null ? Rules.Supply(Batch.fibre.fibre) : Rules.Supply(FibreId);
            Color col = f != null && (f.id.Contains("flax") || f.id.Contains("hemp")) ? new Color(0.78f, 0.72f, 0.55f) : new Color(0.92f, 0.9f, 0.85f);
            for (int k = 0; k < 14; k++)
                c.Disc(Mathf.Min(hand.x + 10f + (k * 7 % 23), HandArea.xMax - 6f), hand.y - 10f + (k * 11 % 21), 5f, col);
        }
        // The meters: the wheel's speed (too slow to draft below the red), the bobbin, the twist going in now.
        Gauge(c, 30f, 640f, StripY + 14f, "WHEEL", SpinLabels, wheel, new Color(0.6f, 0.85f, 1f), (0f, 0.15f, HarmBand));
        Gauge(c, 30f, 640f, StripY, "BOBBIN", SpinLabels, Fill(b), Mark);
        if (plying)
        {
            // the ply's band: the twist that balances the singles' (it hangs straight)
            float bal = (float)(CraftFibre.PlyBalanceAt * SinglesTwist()), lo = bal - (float)CraftFibre.PlyBand, hi = bal + (float)CraftFibre.PlyBand;
            bool right = twistShown >= lo && twistShown <= hi;
            Gauge(c, 30f, 640f, StripY - 14f, "TWIST", SpinLabels, twistShown, right ? Good : twistShown < 0.05f ? Iron : Harm,
                (0f, lo, HarmBand), (lo, hi, GoodBand), (hi, 1f, HarmBand));
        }
        else
        {
            // well twisted in the green; a little under or over in the amber (a coarser thread); pulls apart or kinks in the red
            float tLo = 0.5f - TwistTrue, tHi = 0.5f + TwistTrue;
            Color now = twistShown < 0.05f ? Iron : twistShown >= tLo && twistShown <= tHi ? Good : twistShown >= TwistLow && twistShown <= TwistHigh ? Fair : Harm;
            Gauge(c, 30f, 640f, StripY - 14f, "TWIST", SpinLabels, twistShown, now,
                (0f, TwistLow, HarmBand), (TwistLow, tLo, FairBand), (tLo, tHi, GoodBand), (tHi, TwistHigh, FairBand), (TwistHigh, 1f, HarmBand));
        }
        // the bobbin's own twist so far (what it is spun at, the yarn sent): a gold mark on the TWIST meter
        if (b.length > 1f && b.metres > 0f)
        {
            float x0 = 30f + SpinLabels + 1f, len = 640f - SpinLabels - 2f, at = x0 + len * b.Twist;
            c.Rect(at - 1.5f, StripY - 14f - 3f, at + 1.5f, StripY - 14f + 1f, Mark);
            c.Rect(at - 1.5f, StripY - 14f + 9f, at + 1.5f, StripY - 14f + 13f, Mark);
        }
    }

    /// <summary>The words for a bobbin spun at twist <paramref name="tw"/> and evenness <paramref name="even"/>, keyed to the TWIST meter's bands.</summary>
    private static string SpunWords(float tw, float even) =>
        tw < TwistLow ? "Under-twisted: it pulls apart - draft slower or treadle faster."
        : tw > TwistHigh ? "Over-twisted: it kinks - draft faster, and do not hold still."
        : tw < 0.5f - TwistTrue ? "A little under-twisted: it reels a coarser thread - draft a little slower."
        : tw > 0.5f + TwistTrue ? "A little over-twisted: it reels a coarser thread - draft a little faster."
        : even < 0.6f ? "Lumpy: draft at an even pace." : "Even and well twisted.";

    /// <summary>The singles' twist the ply should balance (the two bobbins on the kate).</summary>
    private float SinglesTwist()
    {
        CraftPiece a = Ws.Piece(Strand(strandA)), b = Ws.Piece(Strand(strandB));
        return a != null && b != null ? (float)CraftFibre.SinglesTwist(new List<CraftPiece> { a, b }) : 0.5f;
    }

    /// <summary>
    /// A treadle (a click on the pedal, or the quick-action key - the foot keeps the wheel going while the hand drafts):
    /// a steady beat (about once a second) brings it up to speed in a few strokes and keeps it there; stamping fast only
    /// jerks the wheel.
    /// </summary>
    private void Treadle(float now)
    {
        float gap = now - lastPedal;
        lastPedal = now;
        float steady = gap < 0.3f ? 0.25f : gap < 0.5f ? 0.65f : gap < 1.8f ? 1f : 0.8f;
        wheel = Mathf.Min(1f, wheel + (1f - wheel) * TreadleKick * steady * (0.6f + 0.4f * (float)Hands.Strength));
        if (gap < 0.3f) wheel *= 0.95f;
    }

    /// <summary>Is <paramref name="yarnId"/> the warp on the loom now (it is woven from: not plied, wound or reeled meanwhile)?</summary>
    private bool OnLoom(string yarnId) => warpTension >= 0f && !string.IsNullOrEmpty(yarnId) && yarnId == warpYarn;

    private const string WarpOnLoom = "It is the warp on the loom: cut the cloth off first (Weave).";

    private void SpinPointer(CraftPointer phase, Vector2 pos)
    {
        bool plying = place == Place.Ply;
        if (phase == CraftPointer.Down && Pedal.Contains(pos))
        {
            Treadle(ui.PointerTime);
            return;
        }
        if (phase == CraftPointer.Down && HandArea.Contains(pos))
        {
            WheelBobbin b = Bobbin;
            if (b.waiting) { said = "The full bobbin is coming off the wheel."; return; }
            if (plying)
            {
                CraftPiece a = Ws.Piece(Strand(strandA)), c = Ws.Piece(Strand(strandB));
                if (a == null || c == null || a == c) { said = "Pick two different bobbins of yarn to ply (spin them first)."; return; }
                if (OnLoom(a.id) || OnLoom(c.id)) { said = WarpOnLoom; return; }
                if (!string.Equals(a.yarn.fibre, c.yarn.fibre, StringComparison.OrdinalIgnoreCase)) { said = "Ply bobbins spun from the same fibre."; return; }
                if (a.yarn.length < 1 || c.yarn.length < 1) { said = "There is too little yarn on one bobbin to ply."; return; }
                if (b.key != PlyKey) { b.Clear(); b.key = PlyKey; }
            }
            else
            {
                CraftPiece batch = Batch;
                if (batch == null && (FibreId == null || Have(FibreId) == 0)) { said = $"You have no {ItemOf(FibreId ?? "fibre")} to spin: bring some, or lay a batch on the fibre bench."; return; }
                if (batch != null && batch.id == Bathing) { said = "That batch is in the scouring bath at the fibre bench: lift it out, rinse it and hang it to dry first."; return; }
                if (batch != null && batch.fibre.wet > 0.3) { said = "The batch is still wet: let it dry."; return; }
            }
            if (Fill(b) >= 1f) { SendBobbin(b); return; } // a full bobbin that did not come off yet: winding it off again
            if (wheel < 0.15f) { said = "Treadle the wheel first: the twist comes from the wheel turning."; return; }
            drafting = true;
            windowOpen = false; // the clock starts with the hand's first movement
            lastHand = pos;
            return;
        }
        if (!drafting) return;
        if (phase == CraftPointer.Up) { drafting = false; return; }
        if (phase != CraftPointer.Drag) return;
        WheelBobbin bb = Bobbin;
        float t = ui.PointerTime;
        if (!windowOpen) { windowOpen = true; windowX = pos.x; windowAt = t; lastHand = pos; return; }
        bool back = pos.x < lastHand.x - 2f;
        lastHand = pos;
        if (back)
        {
            // Back toward the wheel: the spun length winds on (no new yarn, no twist added meanwhile).
            bb.drawn.Clear();
            windowX = pos.x;
            windowAt = t;
            return;
        }
        float dt = t - windowAt, dx = pos.x - windowX;
        if (dt < DraftWindow || dx < 0.5f) return; // the hand held still: the twist builds up, and shows when it moves on
        windowX = pos.x;
        windowAt = t;
        Draft(bb, dx, dt);
    }

    /// <summary>A window of drafting: <paramref name="dx"/> pixels drawn out in <paramref name="dt"/> seconds of the wheel turning.</summary>
    private void Draft(WheelBobbin b, float dx, float dt)
    {
        bool plying = place == Place.Ply;
        float speed = dx / dt; // px/s
        float rate = 0.5f * wheel * TwistPace / 0.7f; // the twist the wheel puts in, per pixel drawn a second
        twistShown = Mathf.Lerp(twistShown, Mathf.Clamp01(rate / Mathf.Max(20f, speed)), 0.35f);
        b.turns += rate * dt;
        b.length += dx;
        float noisy = speed * (1f + (float)Hands.Tremble(b.windows + 101) * 6f);
        b.speedSum += noisy * dx;
        b.speedSq += noisy * noisy * dx;
        b.weight += dx;
        b.windows++;
        if (!plying && b.draft < 0f) b.draft = draft.value; // a bobbin is spun at one draft
        b.metres += dx / (plying ? PlyPixelsPerMetre : DraftPixelsPerMetre);
        b.drawn.Add(Mathf.Lerp(5f, 1.5f, Mathf.Clamp01(noisy / 400f)));
        if (b.drawn.Count > 40) b.drawn.RemoveAt(0);
        // the arm tires with the time it drafts
        draftTime += Mathf.Min(dt, 0.25f);
        while (draftTime >= TireEvery)
        {
            draftTime -= TireEvery;
            Hands.Move(0.15);
        }
        if (Fill(b) >= 1f)
        {
            drafting = false;
            SendBobbin(b);
        }
    }

    /// <summary>A full bobbin comes off the wheel: spun (from the batch, or a batch laid from the bag) or plied.</summary>
    private void SendBobbin(WheelBobbin b)
    {
        if (b.waiting) return;
        float even = b.Evenness, tw = b.Twist, dr = b.draft >= 0f ? b.draft : draft.value;
        string words = SpunWords(tw, even);
        if (b == plyBobbin)
        {
            // the two bobbins the yarn was plied from (the kate as it was when the plying began)
            string[] pair = b.key.Split('|');
            CraftPiece a = pair.Length == 2 ? Ws.Piece(pair[0]) : null, c = pair.Length == 2 ? Ws.Piece(pair[1]) : null;
            if (a == null || c == null) { b.Clear(); b.key = PlyKey; said = "A bobbin on the kate is gone: the plying is lost."; return; }
            float balanceAt = (float)(CraftFibre.PlyBalanceAt * CraftFibre.SinglesTwist(new List<CraftPiece> { a, c }));
            b.waiting = true;
            Send(new CraftRequest(CraftRequestKind.PlyYarn, a.id) { other = c.id, a = tw, b = even }, (ok, why) =>
            {
                if (ok || why == CraftAnswerWaits.NoAnswer) { b.Clear(); b.key = PlyKey; }
                else if (b.key != PlyKey)
                {
                    // refused, and other bobbins went on the kate meanwhile: what was plied from the last two is lost
                    b.Clear();
                    b.key = PlyKey;
                    said = "Other bobbins are on the kate now: the plying of the last two is lost - ply these.";
                    return;
                }
                else b.waiting = false;
                if (!ok) { if (why != CraftAnswerWaits.NoAnswer) said = "The bobbin stays full on the wheel: draw again to wind it off once that is put right."; return; }
                yarnsKey = "";
                double balance = CraftFibre.PlyBalance(tw, balanceAt / CraftFibre.PlyBalanceAt);
                said = (balance > CraftFibre.PlyStraight ? "Plied: the yarn hangs straight, balanced."
                    : tw < balanceAt ? "Under-plied: it untwists itself (feed it back slower)." : "Over-plied: it kinks back on itself (feed it back faster).")
                    + (even < 0.6f ? " The strands went in unevenly: feed them at a steadier pace." : "");
            });
            return;
        }
        // Spun from the batch the bobbin was begun on - or from the bag, which lays a fibre item on the bench as a batch.
        string key = b.key;
        bool fromBag = key.StartsWith("bag:", StringComparison.Ordinal);
        CraftPiece batch = fromBag ? null : Ws.Piece(key);
        if (!fromBag && (batch == null || batch.kind != CraftPieceKind.Fibre)) { b.Clear(); said = "The batch being spun is gone: the bobbin is lost."; return; }
        var r = batch != null
            ? new CraftRequest(CraftRequestKind.SpinFibre, batch.id) { a = even, b = tw, c = dr, i0 = CraftFibre.BobbinGrams }
            : new CraftRequest(CraftRequestKind.Spin) { id = key.Substring(4), a = even, b = tw, c = dr };
        b.waiting = true;
        Send(r, (ok, why) =>
        {
            if (ok || why == CraftAnswerWaits.NoAnswer) b.Clear(); else b.waiting = false;
            if (!ok) { if (why != CraftAnswerWaits.NoAnswer) said = "The bobbin stays full on the wheel: draw again to wind it off once that is put right."; return; }
            yarnsKey = "";
            CraftPiece y = null;
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated && Ws.Piece(e.Piece)?.kind == CraftPieceKind.Yarn) y = Ws.Piece(e.Piece);
            string got = y != null ? $"A full bobbin: {y.yarn.length:0} m of {CraftLook.Name(Rules, y, ui.Known).ToLowerInvariant()}" : "A full bobbin of yarn";
            string bag = r.kind == CraftRequestKind.Spin ? $" (a {Rules.Supply(r.id)?.Name.ToLowerInvariant() ?? "fibre"} from the bag, laid on the bench as a batch: the rest of it spins next)" : "";
            // the yarn as the station made it: an even draft of poorly prepared fibre still spins lumpy
            if (y != null && Mathf.Abs(tw - 0.5f) <= TwistTrue && even >= 0.6f && y.yarn.evenness < 0.55)
                words = y.yarn.prep < 0.5 ? "Lumpy for all the even draft: the fibre was not prepared (pick, scour, tease and card it at the fibre bench)." : "Lumpy: drafted too fine for how the fibre was prepared.";
            said = $"{got}{bag}. {words} Ply it, wind it into a skein, or weave it.";
        });
    }

    // ================================================================== the niddy-noddy
    private static readonly Vector2 Niddy = new Vector2(330f, 196f);
    /// <summary>A skein being wound: the wraps on the niddy-noddy, those counted, and the bobbin's length when winding began (plied or woven since, it starts again).</summary>
    private sealed class Skein { public int wraps, counted; public double length; }
    private readonly Dictionary<string, Skein> skeins = new Dictionary<string, Skein>();
    private bool windingSkein;
    private float lastWindAngle, windTurn, niddyAngle, turnStartedAt, rushedAt = -9f;

    private Skein SkeinOf(CraftPiece y)
    {
        if (!skeins.TryGetValue(y.id, out Skein k)) skeins[y.id] = k = new Skein();
        return k;
    }

    private static int WrapsOn(CraftPiece y) => Mathf.Max(1, Mathf.CeilToInt((float)(y.yarn.length / CraftFibre.MetresPerWrap)));

    /// <summary>Wraps each turn of the niddy-noddy winds: the whole bobbin in about eight turns.</summary>
    private static int WrapsPerTurn(CraftPiece y) => Mathf.Max(1, Mathf.CeilToInt(WrapsOn(y) / 8f));

    private static float AngleAt(Vector2 p) => Mathf.Atan2(p.y - Niddy.y, p.x - Niddy.x) * Mathf.Rad2Deg;

    private void DrawSkein(CraftPixels c)
    {
        CraftPiece y = YarnPiece;
        Skein k = y != null ? SkeinOf(y) : null;
        // The circle to turn it along, and the niddy-noddy turning in the hand: a shaft, a cross-arm at each end (the far
        // one at right angles, so seen end-on); the yarn wraps from arm to arm.
        c.Ring(Niddy.x, Niddy.y, 150f, 1.5f, new Color(1f, 1f, 1f, windingSkein ? 0.3f : 0.12f));
        float a = niddyAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
        Vector2 endA = Niddy - dir * 120f, endB = Niddy + dir * 120f;
        int shown = k != null ? Mathf.Min(k.wraps, 60) : 0;
        for (int i = 0; i < shown; i++)
        {
            Vector2 o = side * (i * 0.5f - 15f);
            c.Line(endA.x + o.x, endA.y + o.y, endB.x - o.x, endB.y - o.y, new Color(0.9f, 0.86f, 0.76f, 0.6f), 1f);
        }
        c.Line(endA.x, endA.y, endB.x, endB.y, Wood, 6f);
        c.Line(endA.x - side.x * 45f, endA.y - side.y * 45f, endA.x + side.x * 45f, endA.y + side.y * 45f, Wood, 6f);
        c.Disc(endB.x, endB.y, 9f, Wood);
        c.Disc(Niddy.x, Niddy.y, 5f, Iron);
        if (y == null) return;
        float wound = Mathf.Clamp01(k.wraps * (float)CraftFibre.MetresPerWrap / Mathf.Max(1f, (float)y.yarn.length));
        c.Disc(640, 300, 10 + 30 * (1f - wound), new Color(0.9f, 0.86f, 0.76f));
        c.Ring(640, 300, 10, 2f, Wood);
        Gauge(c, 30f, 330f, StripY, "WOUND", y.yarn.measured && k.wraps == 0 ? 1f : wound, Mark);
        // The turn under way: amber while it is too quick to count (under half a second), green once it may finish; red
        // for a moment after a rushed turn lost some of the count.
        float now = Time.unscaledTime;
        Color pace = now - rushedAt < 0.4f ? Harm : now - turnStartedAt < 0.5f ? Fair : Good;
        Gauge(c, 390f, 300f, StripY, "TURN", windingSkein ? Mathf.Clamp01(Mathf.Abs(windTurn) / 360f) : 0f, pace);
        string counted = y.yarn.measured && k.wraps == 0 ? $"COUNTED {y.yarn.measuredLength:0} M" : $"COUNTED {k.counted}";
        CraftArt.Label(c, 690f - CraftArt.LabelWidth(counted), StripY - 16f, counted, CraftArt.LabelInk);
    }

    private void SkeinPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece y = YarnPiece;
        if (y == null) { if (phase == CraftPointer.Down) said = "Pick a bobbin of yarn (left)."; return; }
        Skein k = SkeinOf(y);
        if (phase == CraftPointer.Down)
        {
            float d = Vector2.Distance(pos, Niddy);
            if (d < 40f || d > 260f) { said = "Turn the niddy-noddy: hold and circle round it."; return; }
            if (OnLoom(y.id)) { said = WarpOnLoom; return; }
            if (k.wraps * CraftFibre.MetresPerWrap >= y.yarn.length) { said = "The bobbin is empty: tie off the skein."; return; }
            if (k.wraps == 0) k.length = y.yarn.length;
            windingSkein = true;
            lastWindAngle = AngleAt(pos);
            windTurn = 0f;
            turnStartedAt = ui.PointerTime;
            return;
        }
        if (phase == CraftPointer.Up) { windingSkein = false; return; }
        if (!windingSkein || phase != CraftPointer.Drag) return;
        float ang = AngleAt(pos), delta = Mathf.DeltaAngle(lastWindAngle, ang);
        lastWindAngle = ang;
        niddyAngle += delta;
        // Turned one way round: back and forth winds nothing.
        windTurn += delta;
        if (Mathf.Abs(windTurn) < 360f) return;
        windTurn -= Mathf.Sign(windTurn) * 360f;
        // A turn: a run of wraps goes on, counted as they go - turned too fast (or when the hand slips) some slip past the count.
        float now = ui.PointerTime, took = now - turnStartedAt;
        turnStartedAt = now;
        int add = Mathf.Min(WrapsPerTurn(y), WrapsOn(y) - k.wraps);
        if (add <= 0) return;
        bool quick = took < 0.5f, slipped = !quick && Hands.Slips(171 + k.wraps, 0), rushed = quick || slipped;
        int lost = rushed ? Mathf.Clamp(Mathf.RoundToInt(add * Mathf.Clamp01((0.5f - took) / 0.5f + 0.15f)), 1, add) : 0;
        if (rushed) rushedAt = Time.unscaledTime;
        k.wraps += add;
        k.counted += add - lost;
        Hands.Move(0.04);
        // said by its cause: a turn too quick to count, or a tired hand that slipped on a slow one
        said = quick ? $"Too quick: you lost count - {k.counted} wraps counted."
            : slipped ? $"Your tired hand slipped: you lost count - {k.counted} wraps counted. Rest a moment."
            : $"{k.counted} wraps counted.";
        if (k.wraps * CraftFibre.MetresPerWrap >= y.yarn.length)
        {
            windingSkein = false;
            TieOff();
        }
    }

    /// <summary>The skein is tied off at what was counted, once the whole bobbin is wound on.</summary>
    private void TieOff()
    {
        CraftPiece y = YarnPiece;
        if (y == null) { said = "Pick a bobbin of yarn (left)."; return; }
        if (OnLoom(y.id)) { said = WarpOnLoom; return; }
        Skein k = SkeinOf(y);
        if (k.wraps == 0) { said = y.yarn.measured ? "It is tied off already: reel it off as thread." : "Wind the yarn on the niddy-noddy first."; return; }
        double left = y.yarn.length - k.wraps * CraftFibre.MetresPerWrap;
        if (left > 0) { said = $"Wind the rest on first: {left:0} m are still on the bobbin."; return; }
        int counted = k.counted;
        string id = y.id;
        Send(new CraftRequest(CraftRequestKind.MeasureYarn, id) { a = (float)(counted * CraftFibre.MetresPerWrap) }, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer) return; // still wound: tie it off again
            skeins.Remove(id);
            niddyAngle = 0f;
            if (ok) said = $"Tied off: you counted {counted} wraps - {counted * CraftFibre.MetresPerWrap:0} m. Reel it off as thread.";
        });
    }

    private void Reel()
    {
        CraftPiece y = YarnPiece;
        if (y == null) { said = "Pick a skein of yarn (left)."; return; }
        if (OnLoom(y.id)) { said = WarpOnLoom; return; }
        if (!y.yarn.measured) { said = "Wind it into a skein and count it first: its length must be known before it is reeled."; return; }
        Send(new CraftRequest(CraftRequestKind.ReelYarn, y.id)); // the station says what it reeled into
    }

    // ================================================================== the loom
    private static readonly Rect Loom = new Rect(110, 50, 400, 300), WarpBeam = new Rect(540, 60, 150, 290), BeatBar = new Rect(110, 14, 400, 18);
    // The warp's best tension (the needle's green band), and how far off it an even warp can be.
    private const float TensionIdeal = 0.62f, TensionBand = 0.08f;
    private Slider weaveWidth, beatDensity;
    private TMP_Dropdown windOn;
    private Button windOnButton, cutOffButton;
    private readonly List<string> windOnIds = new List<string>();
    private string windOnKey = "";
    private CraftTimingBar beatBar;
    private CraftTimingHit? lastBeat;
    private readonly List<CraftTimingHit> beats = new List<CraftTimingHit>();
    private float warpTension = -1f, tensionNeedle;
    private bool winding, cuttingOff;
    // The warp on the loom: the yarn it is wound from, and its width (fixed until the cloth is cut off).
    private string warpYarn = "", weaveFor = "";
    private int warpWidth;

    /// <summary>The yarn on the loom: the warp's, once it is on; before that the bobbin picked, else the first on the station.</summary>
    private CraftPiece WeaveYarn
    {
        get
        {
            if (warpTension >= 0f) return Ws.Piece(warpYarn);
            if (YarnPiece != null) return YarnPiece;
            foreach (CraftPiece q in Ws.Pieces) if (q.kind == CraftPieceKind.Yarn && q.location == CraftLocation.Air) return q;
            return null;
        }
    }

    private int Width => warpTension >= 0f ? warpWidth : Mathf.RoundToInt(weaveWidth != null ? weaveWidth.value : 6f);

    /// <summary>A steady number from an id (the targets' places follow from it, the same every time).</summary>
    private static int Salt(string id)
    {
        int h = 17;
        foreach (char ch in id ?? "") h = unchecked(h * 31 + ch);
        return h;
    }

    private int RowsAvailable(CraftPiece y) => y == null ? 0 : CraftFibre.RowsFor(y.yarn.length, Width);

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

    /// <summary>The warp comes off the loom (the cloth was cut off, or there was none): the width can be set again.</summary>
    private void WarpOff()
    {
        beats.Clear();
        lastBeat = null;
        warpTension = -1f;
        warpYarn = "";
        weaveFor = "";
        beatBar = null;
        cuttingOff = false;
    }

    private void DrawWeave(CraftPixels c)
    {
        CraftPiece y = WeaveYarn;
        if (warpTension >= 0f && y == null && !cuttingOff) { WarpOff(); said = "The yarn on the loom is gone."; }
        // The loom: its frame, the warp threads, the cloth woven so far (each row as it was beaten), the reed.
        c.Rect(Loom.xMin, Loom.yMin, Loom.xMax, Loom.yMax, new Color(0.12f, 0.11f, 0.1f));
        c.Frame(Loom.xMin, Loom.yMin, Loom.xMax, Loom.yMax, Wood, 8f);
        int width = Width;
        float clothW = Mathf.Min(Loom.width - 40f, width * 22f), x0 = Loom.center.x - clothW / 2f, rowH = (Loom.height - 60f) / CraftFibre.MaxRows;
        Color yc = y != null ? Rules.Supply(y.yarn.fibre)?.color ?? new Color(0.85f, 0.82f, 0.75f) : new Color(0.85f, 0.82f, 0.75f);
        yc.a = 1f;
        for (int k = 0; k <= width * 4; k++)
        {
            float x = x0 + k * clothW / (width * 4);
            c.Line(x, Loom.yMin + 20f, x, Loom.yMax - 20f, warpTension < 0f ? new Color(1f, 1f, 1f, 0.25f) : new Color(yc.r, yc.g, yc.b, 0.7f), 1f);
        }
        int per = RowsPerBeat;
        for (int r = 0; r < beats.Count * per && r < CraftFibre.MaxRows; r++)
        {
            CraftTimingHit bt = beats[r / per];
            float yb = Loom.yMin + 24f + r * rowH;
            Color rc = bt.damage ? Harm : Color.Lerp(yc * 0.6f, yc, bt.score);
            rc.a = 1f;
            // an uneven row sits askew
            float skew = (1f - bt.score) * 4f * Mathf.Sign(bt.offset);
            c.Line(x0, yb, x0 + clothW, yb + skew, rc, rowH * 0.9f);
        }
        // How far the yarn goes: a mark where the last row it allows would lie.
        int rows = RowsAvailable(y);
        if (rows > 0) c.Rect(x0 - 14f, Loom.yMin + 24f + rows * rowH - 1f, x0 - 4f, Loom.yMin + 24f + rows * rowH + 1f, Mark);
        float reedY = Loom.yMin + 30f + Mathf.Min(CraftFibre.MaxRows, beats.Count * per) * rowH;
        c.Rect(x0 - 10f, reedY, x0 + clothW + 10f, reedY + 6f, Wood);
        // The warp beam and the tension needle, its green band.
        c.Rect(WarpBeam.xMin, WarpBeam.yMin, WarpBeam.xMax, WarpBeam.yMax, new Color(0.2f, 0.19f, 0.18f));
        float g0 = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * (TensionIdeal - TensionBand), g1 = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * (TensionIdeal + TensionBand);
        c.Rect(WarpBeam.xMin + 10f, g0, WarpBeam.xMin + 40f, g1, GoodBand);
        float shown = winding ? tensionNeedle : Mathf.Max(0f, warpTension);
        float ny = WarpBeam.yMin + 10f + (WarpBeam.height - 20f) * Mathf.Clamp01(shown);
        c.Rect(WarpBeam.xMin + 6f, ny - 2f, WarpBeam.xMin + 60f, ny + 2f, shown > TensionIdeal + TensionBand * 2f ? Harm : Mark);
        c.Disc(WarpBeam.center.x + 30f, WarpBeam.center.y, 26f, Wood);
        c.Ring(WarpBeam.center.x + 30f, WarpBeam.center.y, 26f, 3f, Iron);
        CraftArt.Label(c, WarpBeam.xMin + 72f, WarpBeam.yMax - 18f, "TENSION", CraftArt.LabelInk); // the needle's name, clear of it
        CraftArt.Label(c, WarpBeam.xMin, StripY, "WARP BEAM", CraftArt.LabelDim);
        string count = y != null ? $"ROWS {Mathf.Min(RowsWoven, rows)}/{rows}" : "NO YARN";
        CraftArt.Label(c, Loom.xMin, StripY, count, CraftArt.LabelInk);
        if (y != null && warpTension >= 0f)
        {
            CraftArt.Label(c, 30f, BeatBar.yMin + 4f, "BEAT", CraftArt.LabelInk);
            DrawTimingBar(c, BeatBarFor(y), BeatBar.xMin, BeatBar.yMin, BeatBar.width, BeatBar.height, lastBeat);
        }
    }

    private void WeavePointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece y = WeaveYarn;
        if (phase == CraftPointer.Up && winding)
        {
            winding = false;
            if (y == null) return;
            if (tensionNeedle < 0.2f) { said = "Wind the warp tighter: hold longer on the beam."; return; }
            if (RowsAvailable(y) < 1) { said = TooLittle(y); return; }
            warpTension = tensionNeedle;
            warpYarn = y.id;
            warpWidth = Mathf.RoundToInt(weaveWidth.value);
            float off = Mathf.Abs(warpTension - TensionIdeal);
            said = off <= TensionBand ? "The warp is evenly tensioned." : warpTension > TensionIdeal ? "Wound too tight: the warp will pucker the cloth." : "Too slack: the warp sags unevenly.";
            Hands.Move(0.1);
            return;
        }
        if (phase != CraftPointer.Down) return;
        if (y == null) { said = "There is no yarn to weave: spin some first."; return; }
        if (CraftFibre.ClothOf(Rules, y) == null) { said = "Nothing is woven from that yarn."; return; }
        if (cuttingOff) { said = "The cloth is being cut off the loom."; return; }
        if (WarpBeam.Contains(pos))
        {
            if (warpTension >= 0f) { said = beats.Count > 0 ? "The warp is on: weave, or cut the cloth off first." : "The warp is on: weave, or take it off (Cut it off the loom)."; return; }
            if (RowsAvailable(y) < 1) { said = TooLittle(y); return; }
            winding = true;
            tensionNeedle = 0f;
            return;
        }
        if (!(Loom.Contains(pos) || BeatBar.Contains(pos))) return;
        if (warpTension < 0f) { said = "Tension the warp first: hold on the warp beam (right of the loom)."; return; }
        int rows = RowsAvailable(y);
        if (RowsWoven >= rows) { said = rows < 1 ? TooLittle(y) : "The yarn is all in: cut the cloth off the loom."; return; }
        // A throw of the shuttle and a beat of the reed, judged on the bar.
        CraftTimingBar b = BeatBarFor(y);
        CraftTimingHit hit = b.Hit(Hands);
        beats.Add(hit);
        lastBeat = hit;
        b.NewTargets(Salt(y.id), beats.Count);
        said = RowsPerBeat > 1 ? $"Rows {Mathf.Min(rows, RowsWoven - RowsPerBeat + 1)}-{Mathf.Min(rows, RowsWoven)}: {HitWords(hit, "a skipped pick")}" : $"Row {beats.Count}: {HitWords(hit, "a skipped pick")}";
        if (RowsWoven >= rows) FinishWeave();
    }

    private string TooLittle(CraftPiece y) =>
        $"Too little yarn for a row {Width} wide: a row takes {CraftFibre.MetresPerCell * Width:0} m and the bobbin has {y.yarn.length:0} m. Narrow the warp, or wind another bobbin on.";

    /// <summary>The cloth is cut off the loom (the rows beaten go as one length); with nothing woven the warp is just taken off.</summary>
    private void FinishWeave()
    {
        if (cuttingOff) return;
        if (warpTension < 0f) { said = "There is no warp on the loom: tension one on the warp beam first."; return; }
        CraftPiece y = Ws.Piece(warpYarn);
        if (y == null) { WarpOff(); said = "The yarn on the loom is gone."; return; }
        if (beats.Count == 0) { WarpOff(); said = "The warp is taken off the loom: nothing was woven. The width can be set again."; return; }
        // each beat as true as these hands beat (the station keeps no truer: CraftRequestGuard.TrueCap)
        var pts = new List<float>();
        float sum = 0f, truest = Truest;
        foreach (CraftTimingHit h in beats) { float s = Mathf.Min(h.score, truest); pts.Add(s); sum += s; }
        int sent = beats.Count; // beats: the station counts the rows each carried
        float even = Mathf.Min(Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(warpTension - TensionIdeal) - TensionBand * 0.5f) * 3f), truest);
        int width = warpWidth;
        float trueness = sum / sent;
        string name = CraftFibre.ClothOf(Rules, y)?.Name.ToLowerInvariant() ?? "cloth";
        cuttingOff = true; // the rows go once: they stay on the loom only if the station refuses them
        Send(new CraftRequest(CraftRequestKind.WeaveCloth, y.id) { i0 = width, i1 = sent, a = even, b = beatDensity.value, points = pts }, (ok, why) =>
        {
            cuttingOff = false;
            if (!ok && why != CraftAnswerWaits.NoAnswer) { said = "The rows stay on the loom: cut it off again in a moment."; return; } // put it right and cut it off again
            CraftPiece cloth = null;
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated && Ws.Piece(e.Piece)?.kind == CraftPieceKind.Hide) cloth = Ws.Piece(e.Piece);
            WarpOff();
            if (!ok) return;
            int flaws = 0;
            if (cloth != null) foreach (HideCell cell in cloth.hide.cells) if (cell.flaw) flaws++;
            // how true the rows were beaten, as the cloth's own record keeps it (the rows the yarn reached)
            CraftRecordEntry woven = cloth?.record.FindLast(e => e.kind == CraftOpKind.WeaveCloth);
            double kept = woven != null ? woven.min : trueness;
            string weak = woven != null && woven.text.Contains(" of a weak") ? ", of a weak yarn: it weaves a weak cloth" : "";
            said = cloth != null
                ? $"Cut off the loom: {cloth.hide.width} × {cloth.hide.height} of {name}, beaten {kept * 100:0}% true{(flaws > 0 ? $", {flaws} flawed patch{(flaws == 1 ? "" : "es")}" : "")}{weak}. Lay it out and cut it at the tailor's table."
                : $"Cut off the loom: a length of {name}.";
        });
    }

    /// <summary>Another bobbin of the same yarn is wound onto the one for the loom: a longer length.</summary>
    private void JoinYarn()
    {
        CraftPiece y = WeaveYarn;
        if (warpTension >= 0f) { said = "The warp is on the loom: wind all the yarn on before tensioning it."; return; }
        if (y == null) { said = "Pick the bobbin to weave (left), then the bobbin to wind onto it."; return; }
        string from = windOnIds.Count > 0 ? windOnIds[Mathf.Clamp(windOn.value, 0, windOnIds.Count - 1)] : null;
        if (from == null) { said = "There is no other bobbin of this yarn to wind on: spin another (same fibre, same plies)."; return; }
        if (!Ws.CanJoinYarn(y.id, from, out string why)) { said = why; return; }
        string onto = y.id;
        double before = y.yarn.length;
        Send(new CraftRequest(CraftRequestKind.JoinYarn, onto) { other = from }, (ok, _) =>
        {
            if (!ok) return;
            windOnKey = "";
            CraftPiece q = Ws.Piece(onto);
            if (q != null) said = $"Wound on: {before:0} m became {q.yarn.length:0} m - {CraftFibre.RowsFor(q.yarn.length, Width)} rows at {Width} wide.";
        });
    }

    // ================================================================== measuring the wearer
    // The wearer's landmarks: the tape's run from each start mark to its end mark.
    private static readonly Vector4[] Marks = { new Vector4(300, 280, 420, 280), new Vector4(310, 220, 410, 220), new Vector4(300, 170, 420, 170), new Vector4(420, 280, 435, 150) };
    private static readonly string[] MarkNames = { "the shoulders", "the chest", "the waist", "the arm" };
    // How far off the end mark (px) a mark is still careful, and still rough: what the station's fit makes of the measure.
    private static readonly float CarefulPx = (float)((1 - CraftTextiles.FitCareful) * 25), RoughPx = (float)((1 - CraftTextiles.FitWorthIt) * 25);
    private readonly float[] markErr = new float[4];
    private int markAt;
    private bool taping;
    private Vector2 tapeHand;

    private void DrawFit(CraftPixels c)
    {
        // The wearer: head, shoulders, body, an arm, legs; the tape between the marks.
        Color skin = new Color(0.55f, 0.45f, 0.38f);
        c.Disc(360, 322, 24, skin);
        c.Rect(300, 150, 420, 290, skin);
        c.Rect(420, 150, 450, 280, skin);
        c.Rect(310, 20, 350, 150, skin);
        c.Rect(370, 20, 410, 150, skin);
        for (int i = 0; i < Marks.Length; i++)
        {
            Vector4 m = Marks[i];
            // measured marks show how true they were: green careful, amber rough, red slipped
            Color col = i < markAt ? MarkColour(markErr[i]) : i == markAt ? Mark : new Color(1f, 1f, 1f, 0.4f);
            c.Disc(m.x, m.y, 4f, col);
            c.Ring(m.z, m.w, 6f, 1.5f, col);
        }
        if (taping && markAt < Marks.Length)
        {
            Vector4 m = Marks[markAt];
            Vector2 now = TapeEnd(Time.unscaledTime), end = new Vector2(now.x, Mathf.Min(now.y, StripY - 8f));
            c.Line(m.x, m.y, end.x, end.y, new Color(0.95f, 0.9f, 0.55f), 4f);
            for (float t = 0; t < 1f; t += 0.08f) c.Disc(Mathf.Lerp(m.x, end.x, t), Mathf.Lerp(m.y, end.y, t), 1f, Iron);
        }
        Gauge(c, 30f, 330f, StripY, "MARKS", markAt / (float)Marks.Length, Mark);
        if (markAt > 0)
        {
            float acc = Accuracy(markAt);
            // the measure so far: careless (fits worse than none) in red, rough in amber, careful in green
            Gauge(c, 390f, 300f, StripY, "ACCURACY", acc, MarkColour(MeanErr(markAt)),
                (0f, (float)CraftTextiles.FitWorthIt, HarmBand),
                ((float)CraftTextiles.FitWorthIt, (float)CraftTextiles.FitCareful, FairBand),
                ((float)CraftTextiles.FitCareful, 1f, GoodBand));
        }
    }

    private static Color MarkColour(float err) => err < CarefulPx ? Good : err < RoughPx ? Fair : Harm;

    private float MeanErr(int n)
    {
        float s = 0f;
        for (int i = 0; i < n; i++) s += markErr[i];
        return n > 0 ? s / n : 0f;
    }

    /// <summary>How accurate the measure of the first <paramref name="n"/> marks is - never truer than these hands (what the station keeps).</summary>
    private float Accuracy(int n) => Mathf.Min(Mathf.Clamp01(1f - MeanErr(n) / 25f), Truest);

    /// <summary>
    /// Where the tape's end is at <paramref name="t"/>: where the hand holds it, swaying with the hand's tremble - a
    /// fresh, practised hand barely, a tired one by more than a careful measure allows (let go as it crosses the ring).
    /// What is drawn is what is measured.
    /// </summary>
    private Vector2 TapeEnd(float t)
    {
        float amp = (float)Hands.Unsteadiness * TapeSway, s = markAt * 1.7f;
        return tapeHand + amp * new Vector2(0.7f * Mathf.Sin(t * 4.3f + s) + 0.3f * Mathf.Sin(t * 9.1f + 2f * s),
                                           0.7f * Mathf.Sin(t * 3.4f + 1.3f + s) + 0.3f * Mathf.Sin(t * 7.7f + s));
    }

    /// <summary>How far (px) the tape's end sways for each unit of the hands' unsteadiness: a fresh novice's ±5, a spent arm's ±23.</summary>
    private const float TapeSway = 200f;

    private void FitPointer(CraftPointer phase, Vector2 pos)
    {
        if (markAt >= Marks.Length) { if (phase == CraftPointer.Down) { markAt = 0; said = "Measuring again from the start."; } return; }
        Vector4 m = Marks[markAt];
        if (phase == CraftPointer.Down)
        {
            if (Vector2.Distance(pos, new Vector2(m.x, m.y)) < 20f) { taping = true; tapeHand = pos; }
            else said = $"Press on the gold dot to start the tape round {MarkNames[markAt]}.";
            return;
        }
        if (!taping) return;
        // The tape follows the hand (tapeHand: where it holds the end), swaying with its tremble; it must be let go right on the end mark.
        tapeHand = pos;
        if (phase != CraftPointer.Up) return;
        taping = false;
        float err = Vector2.Distance(TapeEnd(ui.PointerTime), new Vector2(m.z, m.w));
        markErr[markAt] = err;
        markAt++;
        said = err < CarefulPx ? $"Measured {MarkNames[markAt - 1]} true." : err < RoughPx ? $"Measured {MarkNames[markAt - 1]}, a little off." : $"The tape slipped measuring {MarkNames[markAt - 1]}.";
        Hands.Move(0.02);
        if (markAt < Marks.Length) return;
        float accuracy = Accuracy(Marks.Length);
        // The wearer's own size (their chest, mm), as the tape read it.
        float chest = (float)CraftTextiles.ChestOf(ui.Player != null ? ui.Player.name : "wearer") + (1f - accuracy) * 60f;
        CraftPiece a = Assembly;
        CraftDesign made = a != null ? Rules.Design(a.assembly.design) : null;
        Send(new CraftRequest(CraftRequestKind.MeasureFit, a?.id) { a = accuracy, b = chest }, (ok, _) =>
        {
            if (!ok) return;
            double fit = CraftTextiles.FitOf(true, accuracy);
            // the workshop puts a chest's measure only into a garment (CraftJewelry.Takes): for anything else it waits
            string name = made?.Name.ToLowerInvariant() ?? "";
            string into = made != null && CraftJewelry.Takes(made, false) ? $"The {name} takes these measurements: "
                : made != null ? $"{CraftText.Capital(CraftText.A(name))} {name} is not made to measure: these measurements wait for the next garment started - "
                : "The next garment started is made to these measurements: ";
            said = into
                   + (accuracy > CraftTextiles.FitCareful ? "a careful measure - made to measure" : accuracy >= CraftTextiles.FitWorthIt ? "a rough measure" : "a careless measure - it fits worse than none")
                   + $" (fit ×{fit:0.00}; unmeasured ×{CraftTextiles.FitUnmeasured:0.00}).";
        });
    }

    /// <summary>The truest these hands lay the tape, beat a row or tension a warp: the station keeps no truer (<see cref="CraftRequestGuard.TrueCap"/>), so the gauges and words show that.</summary>
    private float Truest => (float)CraftRequestGuard.TrueCap(CraftRequestGuard.FloorOf(Hands.skill));

    // ================================================================== patterns: laying out and cutting
    private static readonly Rect Bolt = new Rect(30, 20, 660, 330);
    private TMP_Dropdown layoutPattern, cutPattern;
    private Button cutFreeButton;
    private readonly List<string> patternIds = new List<string>();
    private string patternKey = "", patternId = "";
    private int layoutRotation, cutRotation;

    private CraftPattern Current => Rules.Pattern(patternId);

    /// <summary>
    /// The patterns the tailor's table offers: first those the items being made on the station still want (how many),
    /// then every other pattern cloth is cut with. Leather's own patterns are cut at the leather bench.
    /// </summary>
    private void RefreshPatterns()
    {
        List<(string pattern, int count, string item)> wanted = CraftTextiles.PatternsWanted(Ws);
        var ids = new List<string>();
        var names = new List<string>();
        var wantedCount = new Dictionary<string, int>();
        foreach ((string pat, int count, string _) in wanted)
            wantedCount[pat] = (wantedCount.TryGetValue(pat, out int n) ? n : 0) + count;
        foreach ((string pat, int _, string _) in wanted)
            if (!ids.Contains(pat) && Rules.Pattern(pat) != null) ids.Add(pat);
        int firstOther = ids.Count;
        foreach (CraftPattern p in Rules.Patterns)
            if (!ids.Contains(p.id) && CraftTextiles.ClothPattern(Rules, p.id)) ids.Add(p.id);
        if (ids.Count == 0) foreach (CraftPattern p in Rules.Patterns) ids.Add(p.id);
        string key = string.Join(",", ids) + "#" + string.Join(",", wantedCount.Values);
        if (key == patternKey) return;
        patternKey = key;
        patternIds.Clear();
        patternIds.AddRange(ids);
        for (int i = 0; i < ids.Count; i++)
        {
            string name = Rules.Pattern(ids[i])?.Name ?? ids[i];
            names.Add(i < firstOther && wantedCount.TryGetValue(ids[i], out int n) ? $"{name} ({n} wanted)" : name);
        }
        if (patternIds.Count > 0 && !patternIds.Contains(patternId)) patternId = patternIds[0];
        foreach (TMP_Dropdown d in new[] { layoutPattern, cutPattern })
        {
            if (d == null) continue;
            d.ClearOptions();
            d.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        }
        ShowPattern();
    }

    /// <summary>Is a pattern pinned on the length of cloth picked (its menu then shows that pattern, and is locked till it is unpinned)?</summary>
    private bool PinnedHere => cut != null && Cloth != null && cut.cloth == Cloth.id;

    /// <summary>Both menus show the pattern in hand (at Cut, the one pinned on the cloth picked - another length takes the pattern in hand).</summary>
    private void ShowPattern()
    {
        int i = Mathf.Max(0, patternIds.IndexOf(patternId));
        if (layoutPattern != null && layoutPattern.value != i) layoutPattern.SetValueWithoutNotify(i);
        if (cutPattern != null)
        {
            bool here = PinnedHere;
            int pinned = here ? patternIds.IndexOf(cut.pattern) : -1, shown = pinned >= 0 ? pinned : i;
            if (cutPattern.value != shown) cutPattern.SetValueWithoutNotify(shown);
            if (cutPattern.interactable == here) cutPattern.interactable = !here;
        }
    }

    private void PatternPicked(int v)
    {
        if (PinnedHere && place == Place.Cut) { ShowPattern(); said = "Unpin the pattern on the cloth to choose another."; return; }
        if (v >= 0 && v < patternIds.Count) patternId = patternIds[v];
        ShowPattern();
    }

    /// <summary>A length of cloth picked - a whole one, not a part cut from it (the tailor's table lays out and cuts cloth; leather is the leather bench's).</summary>
    private CraftPiece Cloth => Selected != null && CraftTextiles.IsCloth(Rules, Selected) && string.IsNullOrEmpty(Selected.hide.pattern) ? Selected : null;

    /// <summary>Why the piece picked is not a length of cloth to be <paramref name="done"/> ("laid out", "cut").</summary>
    private string PickCloth(string done)
    {
        CraftPiece s = Selected;
        if (s != null && CraftTextiles.IsCloth(Rules, s))
            return $"It is already cut to a pattern ({Rules.Pattern(s.hide.pattern)?.Name.ToLowerInvariant() ?? "a part"}): fit it into its item at the Assembly area. Parts are {done} from a whole length of cloth.";
        if (s != null && s.kind == CraftPieceKind.Hide)
            return $"Leather is {done} at the leather bench: pick a length of cloth.";
        return "Pick a length of cloth (left).";
    }

    /// <summary>The cloth on the board, the arrow of its grain (the warp runs up the bolt).</summary>
    private void DrawBolt(CraftPixels c, CraftPiece p)
    {
        c.Rect(Bolt.xMin - 10, Bolt.yMin - 10, Bolt.xMax + 10, Bolt.yMax + 10, new Color(0.42f, 0.33f, 0.24f));
        if (p == null) return;
        CraftArt.DrawHide(c, Rules, p.hide, Bolt);
        CraftArt.DrawChalk(c, Rules, p.hide, Bolt);
        // the warp runs up the bolt: its arrow on the board's rim
        Arrow(c, new Vector2(Bolt.xMax + 5f, Bolt.yMin + 20f), new Vector2(Bolt.xMax + 5f, Bolt.yMin + 100f), new Color(1f, 1f, 1f, 0.7f));
    }

    private static void Arrow(CraftPixels c, Vector2 a, Vector2 b, Color col)
    {
        c.Line(a.x, a.y, b.x, b.y, col, 2f);
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        c.Line(b.x, b.y, b.x - d.x * 8f + n.x * 5f, b.y - d.y * 8f + n.y * 5f, col, 2f);
        c.Line(b.x, b.y, b.x - d.x * 8f - n.x * 5f, b.y - d.y * 8f - n.y * 5f, col, 2f);
    }

    /// <summary>A pattern laid on the cloth: on the grain green, across it amber, red where it will not go; its own grain arrow; flaws under it ringed.</summary>
    private void DrawLaid(CraftPixels c, CraftPiece p, CraftPattern pat, int gx, int gy, int rotation, bool fits, bool pinned)
    {
        Color fill = pinned ? new Color(1f, 1f, 1f, 0.12f) : !fits ? new Color(1f, 0.35f, 0.3f, 0.35f) : rotation % 2 == 0 ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(1f, 0.75f, 0.3f, 0.35f);
        CraftArt.DrawPattern(c, p.hide, Bolt, pat, gx, gy, rotation, fill);
        CraftArt.HideLayout(p.hide, Bolt, out _, out float cell);
        pat.Size(rotation, out int pw, out int ph);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                if (!pat.Covers(x, y, rotation)) continue;
                HideCell hc = p.hide.Cell(gx + x, gy + y);
                if (hc != null && hc.present && (hc.flaw || hc.damage > 0.3))
                {
                    Rect cr = CraftArt.HideCellRect(p.hide, Bolt, gx + x, gy + y);
                    c.Ring(cr.center.x, cr.center.y, cell * 0.45f, 1.5f, Harm);
                }
            }
        // The pattern's own grain line: along the bolt's (green) or across it (amber).
        Vector2 mid = CraftArt.HidePoint(p.hide, Bolt, gx + pw * 0.5f, gy + ph * 0.5f);
        bool along = rotation % 2 == 0;
        Vector2 dir = along ? new Vector2(0f, cell * Mathf.Min(ph, 4) * 0.4f) : new Vector2(cell * Mathf.Min(pw, 4) * 0.4f, 0f);
        Arrow(c, mid - dir, mid + dir, along ? Good : Fair);
    }

    private void DrawLayout(CraftPixels c)
    {
        CraftPiece p = Cloth;
        CraftPattern pat = Current;
        DrawBolt(c, p);
        if (p == null || pat == null) return;
        Vector2 h = ui.Hover;
        if (Bolt.Contains(h))
        {
            Vector2Int under = CraftArt.HideCellAt(p.hide, Bolt, h); // the pattern's top-left cell (row 0 at the top)
            bool fits = CraftLeather.Fits(p.hide, pat, under.x, under.y, layoutRotation) && CraftTextiles.Overlaps(Rules, p.hide, pat, under.x, under.y, layoutRotation) < 0;
            DrawLaid(c, p, pat, under.x, under.y, layoutRotation, fits, false);
        }
        Gauge(c, Bolt.xMin, Bolt.width, StripY, "CLOTH USED", (float)CraftTextiles.Utilization(Rules, p.hide), new Color(0.6f, 0.85f, 1f));
    }

    private void LayoutPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Cloth;
        CraftPattern pat = Current;
        if (phase == CraftPointer.Scroll) { layoutRotation = (layoutRotation + (scroll < 0 ? 3 : 1)) % 4; return; }
        if (phase != CraftPointer.Down) return; // a click chalks; its drag and release do nothing
        if (p == null) { said = PickCloth("laid out"); return; }
        if (pat == null || !Bolt.Contains(pos)) return;
        Vector2Int under = CraftArt.HideCellAt(p.hide, Bolt, pos); // the pattern's top-left cell (row 0 at the top)
        int gx = under.x, gy = under.y, rot = layoutRotation;
        if (!CraftLeather.Fits(p.hide, pat, gx, gy, rot)) { said = "The pattern runs off the cloth there."; return; }
        int other = CraftTextiles.Overlaps(Rules, p.hide, pat, gx, gy, rot);
        if (other >= 0) { said = $"It overlaps the {Rules.Pattern(p.hide.layout[other].pattern)?.Name.ToLowerInvariant() ?? "pattern"} chalked there."; return; }
        Send(new CraftRequest(CraftRequestKind.LayoutPattern, p.id) { id = pat.id, i0 = gx, i1 = gy, i2 = rot }, (ok, _) =>
        {
            if (ok) said = rot % 2 == 0 ? "Chalked on the grain." : "Chalked across the grain: it will hang and wear worse.";
        });
    }

    private void RubOut()
    {
        CraftPiece p = Cloth;
        if (p == null || p.hide.layout.Count == 0) { said = "There is no chalk on it to rub out."; return; }
        if (!Confirm("rubout:" + p.id, "Click again to rub out all the chalk on this cloth.")) return;
        Send(new CraftRequest(CraftRequestKind.LayoutPattern, p.id) { flag = true }, (ok, _) => { if (ok) said = "The chalk is rubbed out."; });
    }

    // ---- cutting: the pattern pinned on the cloth, and the shears' snips round its outline
    /// <summary>A pattern pinned on a length of cloth and the cut under way round its outline (cell edges, the chalk's line).</summary>
    private sealed class PinnedCut
    {
        public string cloth, pattern, name;
        public int x, y, rotation, chalk = -1, sides;
        public List<List<Vector2>> outline;
        public bool[] touched;
        public int touchedCount;
        public readonly List<Vector2> trace = new List<Vector2>();
        public readonly List<float> snips = new List<float>();
        public float closeSum;
        public int closeN;
        public Vector2 last;
        public bool sent;
        public string Name => name;
        public float Coverage => sides > 0 ? touchedCount / (float)sides : 0f;
        public float Closeness => closeN > 0 ? closeSum / closeN : 0f;
    }

    private PinnedCut cut;
    private bool placing, snipping;
    /// <summary>A side of the outline counts as cut when the shears pass within this much of a cell of its middle (under half, so one line down a strip cuts one side).</summary>
    private const float TouchCells = 0.35f;
    /// <summary>Cut it free from this much of the outline round: the rest tears (each hundredth left uncut costs 2.5% of the cut's cleanness).</summary>
    private const float FreeFrom = 0.9f, TornCost = 2.5f;

    private void DrawCut(CraftPixels c)
    {
        CraftPiece p = Cloth;
        DrawBolt(c, p);
        if (p == null) return;
        Vector2 h = ui.Hover;
        CraftArt.HideLayout(p.hide, Bolt, out _, out float cell);
        if (cut != null && cut.cloth == p.id)
        {
            CraftPattern pat = Rules.Pattern(cut.pattern);
            if (pat != null)
            {
                DrawLaid(c, p, pat, cut.x, cut.y, cut.rotation, true, true);
                // The outline to follow, dashed; the sides already cut solid; the snips coloured by how close they kept.
                CraftArt.DrawOutline(c, cut.outline, new Color(1f, 1f, 1f, 0.8f), 1.5f, true);
                int k = 0;
                foreach (List<Vector2> loop in cut.outline)
                    for (int i = 0; i < loop.Count; i++, k++)
                        if (cut.touched[k])
                        {
                            Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
                            c.Line(a.x, a.y, b.x, b.y, Good, 2f);
                        }
                for (int i = 1; i < cut.trace.Count; i++)
                {
                    float d = CraftArt.NearestOnOutline(cut.outline, cut.trace[i], out _, out _, out _);
                    c.Line(cut.trace[i - 1].x, cut.trace[i - 1].y, cut.trace[i].x, cut.trace[i].y, Color.Lerp(Good, Harm, Mathf.Clamp01(d / (cell * 0.5f))), 2f);
                }
                if (cut.trace.Count > 0) c.Ring(cut.last.x, cut.last.y, 4f, 1.5f, Mark); // where the next snip starts
            }
            Gauge(c, Bolt.xMin, Bolt.width, StripY, "CUT ROUND", cut.Coverage, new Color(0.5f, 1f, 0.6f), (FreeFrom, 1f, GoodBand));
        }
        else
        {
            CraftPattern pat = Current;
            if (pat != null && Bolt.Contains(h))
            {
                Snap(p, pat, CraftArt.HideCellAt(p.hide, Bolt, h), cutRotation, out int gx, out int gy, out int rot, out int chalk);
                bool fits = CraftLeather.Fits(p.hide, pat, gx, gy, rot);
                DrawLaid(c, p, pat, gx, gy, rot, fits, false);
                if (fits && CraftTextiles.Overlaps(Rules, p.hide, pat, gx, gy, rot, chalk) >= 0)
                    CraftArt.DrawOutline(c, CraftArt.PatternOutline(p.hide, Bolt, pat, gx, gy, rot), Fair, 2f); // it cuts through other chalk
                else if (chalk >= 0)
                    CraftArt.DrawOutline(c, CraftArt.PatternOutline(p.hide, Bolt, pat, gx, gy, rot), Good, 2f); // on the chalk
            }
        }
        if (Bolt.Contains(h))
        {
            // The shears, open or closing (kept under the meters along the top).
            float open = Mathf.Clamp(Mathf.Min(snipping ? 4f : 14f, StripY - 13f - h.y), 0f, 14f);
            c.Line(h.x, h.y, h.x + 30f, h.y + open, new Color(0.75f, 0.76f, 0.8f), 2f);
            c.Line(h.x, h.y, h.x + 30f, h.y - open, new Color(0.75f, 0.76f, 0.8f), 2f);
            c.Ring(h.x + 36f, h.y + open + 4f, 5f, 1.5f, Iron);
            c.Ring(h.x + 36f, h.y - open - 4f, 5f, 1.5f, Iron);
        }
    }

    /// <summary>
    /// Where a pattern laid with its top-left cell at <paramref name="at"/> goes: onto the chalk of the same pattern within
    /// a cell of it, turned as that chalk is (<paramref name="chalk"/> its placement; -1 for none) - however it was turned
    /// at Lay out - else where it was laid, at the turn in hand (<paramref name="rotation"/>).
    /// </summary>
    private void Snap(CraftPiece p, CraftPattern pat, Vector2Int at, int rotation, out int gx, out int gy, out int rot, out int chalk)
    {
        gx = at.x;
        gy = at.y;
        rot = ((rotation % 4) + 4) % 4;
        chalk = -1;
        int best = int.MaxValue;
        List<CraftLayoutPlacement> layout = p.hide.layout;
        for (int i = 0; i < layout.Count; i++)
        {
            CraftLayoutPlacement l = layout[i];
            int dx = Mathf.Abs(l.x - at.x), dy = Mathf.Abs(l.y - at.y);
            if (l.pattern != pat.id || dx > 1 || dy > 1 || dx + dy >= best) continue;
            best = dx + dy;
            gx = l.x;
            gy = l.y;
            rot = ((l.rotation % 4) + 4) % 4;
            chalk = i;
        }
    }

    private void TurnCut()
    {
        if (PinnedHere) { said = "Unpin it to turn it."; return; }
        cutRotation = (cutRotation + 1) % 4;
        said = cutRotation % 2 == 0 ? "Along the grain." : "Across the grain: the part will be weaker.";
    }

    private void Unpin()
    {
        if (cut == null) { said = "Nothing is pinned."; return; }
        if (cut.snips.Count > 0 && !Confirm("unpin", "Click Unpin again: the snips so far are lost.")) return;
        cut = null;
        placing = snipping = false;
        ShowPattern();
        said = "Unpinned.";
    }

    private void CutPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = Cloth;
        if (phase == CraftPointer.Scroll) { if (!PinnedHere) cutRotation = (cutRotation + (scroll > 0 ? 1 : 3)) % 4; else said = "Unpin it to turn it."; return; }
        if (p == null)
        {
            if (phase == CraftPointer.Down) said = PickCloth("cut");
            return;
        }
        if (cut != null && cut.cloth != p.id && phase == CraftPointer.Down && cut.snips.Count > 0 && !Confirm("repin", "Click again to pin a pattern here: the cut started on the other length is dropped."))
            return;
        if (cut == null || cut.cloth != p.id)
        {
            if (phase != CraftPointer.Down) return;
            CraftPattern pat = Current;
            if (pat == null || !Bolt.Contains(pos)) return;
            // onto its own chalk (turned as it was chalked), else where it lies at the turn in hand
            Snap(p, pat, CraftArt.HideCellAt(p.hide, Bolt, pos), cutRotation, out int gx, out int gy, out int rot, out int chalk);
            if (!CraftLeather.Fits(p.hide, pat, gx, gy, rot)) { said = "The pattern runs off the cloth there (or over a hole)."; return; }
            cut = new PinnedCut { cloth = p.id, pattern = pat.id, name = pat.Name, x = gx, y = gy, rotation = rot, chalk = chalk };
            cut.outline = CraftArt.PatternOutline(p.hide, Bolt, pat, gx, gy, rot);
            foreach (List<Vector2> loop in cut.outline) cut.sides += loop.Count;
            cut.touched = new bool[cut.sides];
            placing = true; // the press that pinned it: its drag and release do not cut
            ShowPattern();
            int other = CraftTextiles.Overlaps(Rules, p.hide, pat, gx, gy, rot, chalk);
            said = (chalk >= 0 ? "Pinned on its chalk (a cut on the chalk is cleaner). " : "")
                   + (other >= 0 ? $"It cuts through the chalk of the {Rules.Pattern(p.hide.layout[other].pattern)?.Name.ToLowerInvariant() ?? "pattern"}: that chalk is lost. " : "")
                   + (rot % 2 == 0 ? "Pinned on the grain. Now cut round it: snip, snip - press and release, each snip from the last." : "Pinned across the grain: it will stretch and wear worse. Cut round it with the shears (or unpin it and turn it).");
            return;
        }
        if (cut.sent) return;
        if (phase == CraftPointer.Down) { placing = false; snipping = true; return; }
        if (placing) { if (phase == CraftPointer.Up) placing = false; return; } // the wobble of the pinning click is not a cut
        if (phase != CraftPointer.Up || !snipping) return;
        snipping = false;
        // A snip: from the last snip to where the shears close (the hand trembles).
        CraftArt.HideLayout(p.hide, Bolt, out _, out float cell);
        Vector2 at = pos + new Vector2((float)Hands.Tremble(cut.trace.Count + 111) * 50f, (float)Hands.Tremble(cut.trace.Count + 112) * 50f);
        if (cut.trace.Count == 0) { cut.trace.Add(at); cut.last = at; said = "The shears are in: snip along the dashed line from here."; return; }
        float len = Vector2.Distance(at, cut.last);
        if (len < 1f) return;
        if (len > cell * 3f) { said = "Too long a snip: the shears would wander. Shorter snips, from the gold ring."; return; }
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
        for (int k = 1; k <= steps; k++)
        {
            Vector2 q = Vector2.Lerp(cut.last, at, k / (float)steps);
            cut.trace.Add(q);
            float d = CraftArt.NearestOnOutline(cut.outline, q, out _, out _, out _);
            cut.closeSum += 1f - (float)CraftMath.Smooth(0.05, 0.6, d / cell); // on the line 1, half a cell off nearly 0
            cut.closeN++;
        }
        // the sides of the outline this snip passed along (near their middles)
        int s = 0;
        foreach (List<Vector2> loop in cut.outline)
            for (int i = 0; i < loop.Count; i++, s++)
            {
                if (cut.touched[s]) continue;
                Vector2 mid = (loop[i] + loop[(i + 1) % loop.Count]) * 0.5f;
                if (DistanceToSegment(mid, cut.last, at) < TouchCells * cell) { cut.touched[s] = true; cut.touchedCount++; }
            }
        cut.snips.Add(len);
        cut.last = at;
        Hands.Move(0.1);
        if (cut.touchedCount >= cut.sides) CutFree(true);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>How clean the cut is (0-1): how close the shears kept to the line, how even the snips, and how much of the outline was cut rather than torn.</summary>
    private static double Accuracy(PinnedCut k)
    {
        float mean = 0f, sq = 0f;
        foreach (float s in k.snips) { mean += s; sq += s * s; }
        mean /= Mathf.Max(1, k.snips.Count);
        float choppy = k.snips.Count > 1 ? Mathf.Sqrt(Mathf.Max(0f, sq / k.snips.Count - mean * mean)) / Mathf.Max(1f, mean) : 0.5f;
        double torn = 1 - Math.Min(1, TornCost * (1 - k.Coverage));
        return Math.Max(0, Math.Min(1, (0.85 * k.Closeness + 0.15 * (1 - Mathf.Clamp01(choppy))) * torn));
    }

    /// <summary>The part comes off the cloth: by itself once the whole outline is cut, or cut free from nine-tenths round (the rest tears).</summary>
    private void CutFree(bool whole = false)
    {
        CraftPiece p = Cloth;
        if (cut == null || p == null || cut.cloth != p.id) { said = "Pin a pattern on the cloth and cut round it first."; return; }
        if (cut.sent) return;
        if (!whole && cut.Coverage < FreeFrom) { said = $"Cut further round first: {cut.Coverage * 100:0}% of the outline is cut ({FreeFrom * 100:0}% at least; what is left tears free)."; return; }
        // never cleaner than these hands cut: the station holds every cut to it (CraftRequestGuard.CutCap), so the words say what it records
        double accuracy = Math.Min(CraftRequestGuard.CutCap(CraftRequestGuard.FloorOf(Hands.skill)), Accuracy(cut));
        PinnedCut k = cut;
        cut.sent = true; // sent once: the pinned cut stays only if the station refuses it
        Send(new CraftRequest(CraftRequestKind.Cut, p.id) { id = k.pattern, i0 = k.x, i1 = k.y, i2 = k.rotation, a = (float)accuracy }, (ok, why) =>
        {
            k.sent = false;
            if (!ok && why != CraftAnswerWaits.NoAnswer) return; // still pinned: cut it free again
            if (cut == k) cut = null;
            ShowPattern();
            if (!ok) return;
            string torn = k.Coverage < 1f ? $" ({(1f - k.Coverage) * 100:0}% of the edge tore free)" : "";
            bool onChalk = false;
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated && LastRecord(e.Piece).Contains("along the chalk")) onChalk = true;
            said = (accuracy > 0.85 ? "A clean edge, right on the line" : accuracy > 0.6 ? "Cut out: the edge wanders a little" : "Cut out raggedly: it will fray")
                   + torn + (onChalk ? ", along the chalk" : "") + $": {k.name.ToLowerInvariant()}{(k.rotation % 2 == 0 ? "" : ", across the grain")}.";
        });
    }

    // ================================================================== the needle
    private bool threaded;
    private int threadLeft, frays;
    private string threadId = "";
    private Vector2 threadEnd, lastEnd = new Vector2(-999f, -999f);
    private Rect eye;

    /// <summary>Stitches a needleful lasts (a practised hand wastes less of it).</summary>
    private int Needleful => CraftMastery.NeedlefulStitches(Mastery);

    /// <summary>The item picked already holds all its thread: the needle is threaded from it, whatever is in the bag.</summary>
    private bool ItemThreaded(out string thread)
    {
        thread = null;
        CraftPiece a = Assembly;
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d == null) return false;
        bool any = false;
        foreach (CraftComponentSpec c in d.components)
            if (c.supply && c.supplyKind == CraftSupplyKind.Thread)
            {
                if (a.assembly.Supply(c.role) < Math.Max(1, c.supplyCount)) return false;
                any = true;
            }
        if (any) thread = "the item's thread";
        return any;
    }

    /// <summary>
    /// The thread against what the seams of the item picked go through (their thickest cloth or leather: a heavier cloth
    /// wants a stronger thread, <see cref="CraftTextiles.ThreadNeed"/>): " Coarse wool yarn (0.6) is weak for its wool
    /// cloth (it wants 0.92): its seams will be weak." - "" with no item picked.
    /// </summary>
    private string ThreadFor()
    {
        CraftPiece a = Assembly;
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d == null)
            return "";
        double thick = 0;
        string what = "";
        foreach (CraftJointSpec js in d.joints)
        {
            if (js.kind != CraftJointKind.Stitch && !(js.kind == CraftJointKind.Buckle && CraftAssembly.RivetPart(d, js) == null)) continue;
            // the parts fitted, else what the design cuts them from
            double t = CraftAssembly.SeamThickness(Ws, a, js);
            foreach (string role in new[] { js.roleA, js.roleB })
            {
                CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
                if (t <= 0 && c != null && c.kind == CraftPieceKind.Hide && c.leather != null) t = Math.Max(t, (c.leather.thicknessMin + c.leather.thicknessMax) * 0.5);
            }
            if (t <= thick) continue;
            thick = t;
            CraftPiece part = null;
            foreach (string role in new[] { js.roleA, js.roleB })
                if (part == null && !string.IsNullOrEmpty(role)) part = Ws.Piece(a.assembly.PieceIn(role));
            what = part != null && part.kind == CraftPieceKind.Hide ? Rules.Hide(part.hide.hide)?.Name.ToLowerInvariant() ?? "" : CraftTextiles.ClothSeam(Ws, a, js) ? "cloth" : "leather";
        }
        if (thick <= 0)
            return "";
        // the thread it is sewn with: its own once all of it is in, else the one chosen from the bag
        double strength = 0;
        string name;
        if (ItemThreaded(out _))
        {
            CraftComponentSpec t = CraftAssembly.ThreadPart(d);
            strength = t != null ? a.assembly.Supply(t.role + CraftWorkshop.StrengthSuffix) : 0;
            name = "Its thread";
        }
        else
        {
            CraftSupplyDefinition s = Rules.Supply(ThreadId);
            strength = s?.strength ?? 0;
            name = s?.Name ?? "";
        }
        if (strength <= 0 || name.Length == 0)
            return "";
        double need = CraftTextiles.ThreadNeed(thick);
        if (CraftTextiles.ThreadSuits(thick, strength) >= CraftEvaluator.ThreadSuitsWell)
            return $" {name} ({strength:0.##}) suits the {what} of the {d.Name.ToLowerInvariant()} (it wants {need:0.##}).";
        return strength < need ? $" {name} ({strength:0.##}) is weak for the {what} of the {d.Name.ToLowerInvariant()} (it wants {need:0.##}): its seams will be weak."
            : $" {name} ({strength:0.##}) is too heavy for the {what} of the {d.Name.ToLowerInvariant()}: it will pucker it.";
    }

    /// <summary>Can the needle be threaded now: from the item's own thread, or with the thread chosen from the bag?</summary>
    private bool CanThread(out string with)
    {
        if (ItemThreaded(out with)) return true;
        with = Rules.Supply(ThreadId)?.Name.ToLowerInvariant() ?? "thread";
        return threads.Count > 0 && Have(ThreadId) != 0;
    }

    private void Threaded(string with)
    {
        threaded = true;
        threadLeft = Needleful;
        threadId = ThreadId;
        frays = 0;
        said = $"Threaded with {with}: enough for about {Needleful} stitches.";
    }

    /// <summary>An apprentice threads a needle without thinking.</summary>
    private void ThreadByRoutine()
    {
        if (!CanThread(out string with)) { said = $"You have no {ItemOf(ThreadId ?? "thread")}: spin some at the wheel, or bring some."; return; }
        Threaded(with);
        said = "Without a thought: " + said;
    }

    private void DrawNeedle(CraftPixels c)
    {
        // The needle held up in the other hand: it trembles, more as the arm tires.
        float t = Time.unscaledTime, amp = 2f + (float)Hands.Unsteadiness * 160f;
        float ex = 400f + Mathf.Sin(t * 5.3f) * amp * 0.6f, ey = 200f + Mathf.Sin(t * 3.7f + 1f) * amp;
        eye = new Rect(ex - 5f, ey - 26f, 10f, 52f);
        c.Rect(ex - 9f, ey - 180f, ex + 9f, ey + 60f, new Color(0.75f, 0.76f, 0.8f));
        c.Rect(ex - 4f, ey - 24f, ex + 4f, ey + 24f, new Color(0.06f, 0.06f, 0.07f)); // the eye
        CraftSupplyDefinition s = Rules.Supply(ThreadId);
        Color col = s != null && s.id.Contains("wool") ? new Color(0.9f, 0.88f, 0.8f) : new Color(0.85f, 0.82f, 0.7f);
        if (threaded) c.Line(ex, ey, ex + 160f, ey - 60f, col, 2f);
        Gauge(c, 30f, 330f, StripY, "THREAD", threaded ? threadLeft / (float)Needleful : 0f, Mark);
        Vector2 h = ui.Hover;
        if (h.x < 0 || threaded) return;
        // The thread's end follows the hand, with its tremble; fatter the more it has frayed.
        threadEnd = new Vector2(h.x, Mathf.Min(h.y, StripY - 10f)) + new Vector2((float)Hands.Tremble(Mathf.RoundToInt(t * 10f) + 91) * 120f, (float)Hands.Tremble(Mathf.RoundToInt(t * 10f) + 92) * 120f);
        c.Line(threadEnd.x - 200f, threadEnd.y - 40f, threadEnd.x, threadEnd.y, col, 2f);
        c.Disc(threadEnd.x, threadEnd.y, 1.5f + frays * 1.2f, col);
    }

    private void NeedlePointer(CraftPointer phase, Vector2 pos)
    {
        if (threaded) { if (phase == CraftPointer.Down) said = $"It is threaded ({threadLeft} stitches left): sew."; return; }
        if (phase == CraftPointer.Down)
        {
            if (!CanThread(out _)) { said = $"You have no {ItemOf(ThreadId ?? "thread")}: spin some at the wheel, or bring some."; lastEnd = new Vector2(-999f, -999f); return; }
            lastEnd = threadEnd;
            return;
        }
        if (lastEnd.x < -900f || phase != CraftPointer.Drag) { if (phase == CraftPointer.Up) lastEnd = new Vector2(-999f, -999f); return; }
        Vector2 end = threadEnd;
        // Through the eye: crossing its middle from left to right, inside its height; a frayed end is fatter and needs more
        // room; catching the eye's edge frays it.
        float gapHalf = 6f + frays * 1.2f;
        if (lastEnd.x < eye.center.x && end.x >= eye.center.x)
        {
            float y = Mathf.Lerp(lastEnd.y, end.y, (eye.center.x - lastEnd.x) / Mathf.Max(0.01f, end.x - lastEnd.x));
            float inside = eye.height * 0.5f - Mathf.Abs(y - eye.center.y);
            if (inside > gapHalf)
            {
                CanThread(out string with);
                Threaded(with);
            }
            else if (inside > -8f)
            {
                frays++;
                said = frays >= 3 ? "The end is frayed fat: snip it." : "It catches on the eye and frays.";
            }
        }
        lastEnd = end;
    }

    // ================================================================== sewing
    private const float SeamY = 200f, IdealGap = 22f;
    private TMP_Dropdown seam;
    private readonly List<string> seams = new List<string>();
    private string seamsFor = "";
    private float lastStitchX = -1f, pressAt, lastPull = -1f;
    private bool pulling;
    private readonly List<Vector3> stitches = new List<Vector3>(); // x, y, tension
    private float spacingSum, alignSum, tensionSum, tensionSq;
    // the pulls waiting to go, and how many of them were pulled past the cut (each of those cuts the cloth, as the PULL gauge showed)
    private int pending, pendingCut;
    // The seam the stitches waiting to go were sewn on; the items whose thread is on its way in.
    private string pendingAsm = "", pendingJoint = "";
    private readonly HashSet<string> attaching = new HashSet<string>();

    /// <summary>Stitches each click lays (one; a run of two from journeyman, three for a master).</summary>
    private int StitchesPerClick => CraftMastery.StitchesPerAction(Mastery, MasterySettings);

    private string SeamId => seams.Count > 0 && seam != null ? seams[Mathf.Clamp(seam.value, 0, seams.Count - 1)] : null;

    /// <summary>The tension a pull held this long puts on the stitch (the arm's strength in it): what the gauge shows is what is sent.</summary>
    private float PullTension(float held) => Mathf.Clamp01(held / 0.8f) * (0.6f + 0.4f * (float)Hands.Strength);

    private void SeamPicked()
    {
        // the stitches waiting go to the seam they were sewn on; the new seam starts at its beginning
        FlushStitches();
        stitches.Clear();
        lastStitchX = -1f;
        said = "";
    }

    /// <summary>Why the seam picked cannot take stitches now (null: it can).</summary>
    private string SeamProblem(CraftPiece a, CraftJointSpec j)
    {
        CraftDesign d = Rules.Design(a.assembly.design);
        if (a.assembly.finished) return "It is finished: only taking it is left.";
        if (d == null || j == null) return "It has no seams to sew.";
        foreach (string role in new[] { j.roleA, j.roleB })
        {
            CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
            if (c == null) continue;
            if (!c.supply && Ws.Piece(a.assembly.PieceIn(role)) == null) return $"Fit the {c.Name.ToLowerInvariant()} first (at the Assembly area).";
            if (c.supply && c.supplyKind != CraftSupplyKind.Thread && a.assembly.Supply(role) < Math.Max(1, c.supplyCount)) return $"Lay the {c.Name.ToLowerInvariant()} in first (add it at the Assembly area).";
        }
        JointState js = a.assembly.Joint(j.id);
        int waiting = pendingAsm == a.id && pendingJoint == j.id ? pending * StitchesPerClick : 0;
        if ((js != null ? js.stitches : 0) + waiting >= Math.Max(1, j.count)) return $"The {j.Name.ToLowerInvariant()} is sewn all along: pick another seam.";
        // its thread goes in with the first stitches: the bag must hold what the item takes
        foreach (CraftComponentSpec c in d.components)
            if (c.supply && c.supplyKind == CraftSupplyKind.Thread && a.assembly.Supply(c.role) < c.supplyCount)
            {
                int need = c.supplyCount - (int)a.assembly.Supply(c.role), have = Have(threadId);
                if (have >= 0 && have < need)
                    return $"The {d.Name.ToLowerInvariant()} takes {need} {ItemOf(threadId)}: you have {have}.";
            }
        return null;
    }

    private void DrawSew(CraftPixels c)
    {
        CraftPiece a = Assembly;
        // Two edges of cloth laid together, the seam line chalked along them.
        Color cloth = new Color(0.8f, 0.77f, 0.68f);
        if (a != null)
            foreach (AssemblyPart part in a.assembly.parts)
            {
                CraftPiece q = Ws.Piece(part.piece);
                if (q != null && q.kind == CraftPieceKind.Hide) { cloth = Rules.Hide(q.hide.hide)?.color ?? cloth; break; }
            }
        c.Rect(30, 130, 690, 200, cloth);
        c.Rect(30, 200, 690, 270, Color.Lerp(cloth, Color.black, 0.12f));
        for (float x = 40; x < 680; x += 10) c.Line(x, SeamY, x + 5, SeamY, new Color(1f, 1f, 1f, 0.6f), 1f);
        // The ideal run (a stitch, or a practised hand's run of them), as a reference by the seam's start.
        c.Line(40, SeamY - 40, 40 + IdealGap * StitchesPerClick, SeamY - 40, new Color(1f, 1f, 1f, 0.4f), 2f);
        foreach (Vector3 s in stitches)
        {
            Color col = Color.Lerp(new Color(0.95f, 0.9f, 0.7f), new Color(0.35f, 0.22f, 0.12f), s.z);
            c.Line(s.x - 8f, s.y - 6f, s.x + 8f, s.y + 6f, col, 1f + 2f * s.z);
            if (s.z > CraftAssembly.StitchTensionCuts) c.Line(s.x - 10f, s.y - 12f, s.x - 10f, s.y + 12f, new Color(0f, 0f, 0f, 0.25f), 1f); // pulled so hard it puckers
        }
        Vector2 h = ui.Hover;
        if (h.x >= 0) { c.Line(h.x, h.y, h.x + 4f, h.y - 40f, new Color(0.8f, 0.8f, 0.85f), 1.5f); c.Disc(h.x + 4f, h.y - 40f, 2f, Iron); }
        // The thread left on the needle; the pull now (or the last one) against the seam's best tension and where it cuts;
        // how far the seam is sewn (it can be finished from four-fifths, it is at full strength sewn all along).
        Gauge(c, 30f, 200f, StripY, "THREAD", threaded ? threadLeft / (float)Needleful : 0f, Mark);
        float pull = pulling ? PullTension(Time.unscaledTime - pressAt) : lastPull;
        float ideal = (float)CraftAssembly.StitchTensionIdeal, cuts = (float)CraftAssembly.StitchTensionCuts;
        Gauge(c, 250f, 220f, StripY, "PULL", Mathf.Max(0f, pull), pull > cuts ? Harm : Mathf.Abs(pull - ideal) <= 0.1f ? Good : Fair,
            (ideal - 0.1f, ideal + 0.1f, GoodBand), (cuts, 1f, HarmBand));
        CraftJointSpec j = a != null ? Rules.Design(a.assembly.design)?.Joint(SeamId) : null;
        JointState js = j != null ? a.assembly.Joint(j.id) : null;
        if (j != null)
            Gauge(c, 490f, 200f, StripY, "SEAM", js != null ? js.stitches / (float)Math.Max(1, j.count) : 0f, new Color(0.5f, 1f, 0.6f), (0.8f, 1f, GoodBand));
    }

    private void SewPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece a = Assembly;
        if (phase == CraftPointer.Down)
        {
            if (a == null) { said = "Pick the item being made (start it at the Assembly area)."; return; }
            CraftJointSpec j = Rules.Design(a.assembly.design)?.Joint(SeamId);
            if (j == null) { said = "It has no seams to sew."; return; }
            if (!threaded) { said = "Thread the needle first."; return; }
            string problem = SeamProblem(a, j);
            if (problem != null) { said = problem; return; }
            if (pos.y < SeamY - 60f || pos.y > SeamY + 60f) return;
            if (lastStitchX >= 0f && pos.x <= lastStitchX + 2f) { said = "Sew along the seam, left to right."; return; }
            pulling = true;
            pressAt = ui.PointerTime;
            return;
        }
        if (phase != CraftPointer.Up || !pulling) return;
        pulling = false;
        if (a == null) return;
        float tension = PullTension(ui.PointerTime - pressAt);
        lastPull = tension;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(stitches.Count + 121) * 60f, (float)Hands.Tremble(stitches.Count + 122) * 60f);
        // A run of stitches between the last and this one (a practised hand lays two or three with one pull).
        int per = StitchesPerClick;
        float gap = IdealGap * per;
        float spacing = lastStitchX >= 0f ? Mathf.Clamp01(Mathf.Abs((at.x - lastStitchX) - gap) / gap) : 0f;
        float align = Mathf.Clamp01(Mathf.Abs(at.y - SeamY) / 14f);
        float fromX = lastStitchX >= 0f ? lastStitchX : at.x - gap;
        for (int k = 1; k <= per; k++)
            stitches.Add(new Vector3(Mathf.Lerp(fromX, at.x, k / (float)per), Mathf.Lerp(SeamY, at.y, k / (float)per), tension));
        while (stitches.Count > 60) stitches.RemoveAt(0);
        lastStitchX = at.x;
        if (pending == 0) { pendingAsm = a.id; pendingJoint = SeamId; } // the seam these stitches are sewn on
        spacingSum += spacing; alignSum += align; tensionSum += tension; tensionSq += tension * tension;
        pending++;
        if (tension > CraftAssembly.StitchTensionCuts) pendingCut++; // this pull cuts the cloth (the red band): the station counts it so
        threadLeft = Mathf.Max(0, threadLeft - per);
        Hands.Move(0.1);
        said = tension > CraftAssembly.StitchTensionCuts ? "Pulled too hard: it cuts into the cloth and puckers." : tension < CraftAssembly.StitchTensionIdeal - 0.2f ? "A loose stitch: pull a little longer." : "";
        if (pending >= 6) FlushStitches();
        if (threadLeft <= 0)
        {
            FlushStitches();
            threaded = false;
            said = "The thread runs out: thread the needle again.";
        }
        if (lastStitchX > 670f) { lastStitchX = -1f; stitches.Clear(); said = "To the end of the seam: carry on from the start of the next stretch."; }
    }

    /// <summary>
    /// The stitches waiting go to the seam they were sewn on, all at once - the item's thread first (with its first
    /// stitches), then the pulls, one request: the needle pierces the holes they need as it draws them. Nothing waits
    /// for an answer before it goes, so the stitches reach the station whatever comes next (the window closing, the item
    /// taken): the station runs one player's requests in the order they were sent.
    /// </summary>
    private void FlushStitches()
    {
        int n = pending, cut = pendingCut;
        float spacing = n > 0 ? spacingSum / n : 0f, align = n > 0 ? alignSum / n : 0f, mean = n > 0 ? tensionSum / n : 0f;
        float spread = n > 0 ? Mathf.Sqrt(Mathf.Max(0f, tensionSq / n - mean * mean)) : 0f;
        string asm = pendingAsm, joint = pendingJoint, with = threadId;
        // taken out before they are sent: they never go twice
        pending = pendingCut = 0;
        spacingSum = alignSum = tensionSum = tensionSq = 0f;
        pendingAsm = pendingJoint = "";
        if (n <= 0 || string.IsNullOrEmpty(asm) || string.IsNullOrEmpty(joint)) return;
        CraftPiece a = Ws?.Piece(asm);
        if (a == null) { said = "The item being sewn is gone: those stitches are lost."; return; }
        CraftDesign d = Rules.Design(a.assembly.design);
        if (d != null)
            foreach (CraftComponentSpec c in d.components)
            {
                string key = asm + "/" + c.role;
                if (!c.supply || c.supplyKind != CraftSupplyKind.Thread || a.assembly.Supply(c.role) >= c.supplyCount || !attaching.Add(key)) continue;
                var thread = new CraftRequest(CraftRequestKind.Attach, asm) { id = c.role, count = Mathf.Max(1, c.supplyCount - (int)a.assembly.Supply(c.role)), other = with };
                if (!Send(thread, (ok, _) => attaching.Remove(key)))
                {
                    said = "The thread did not go into it: those stitches are lost."; // refused at once (the reason is shown)
                    return;
                }
            }
        // i1 = 1: the needle - each pull lays as many stitches (and pierces as many holes, flag) as the tailor's practice
        // allows; i2: the pulls past the cut, each of which cut the cloth (whatever the pull the run averaged)
        var pull = new CraftRequest(CraftRequestKind.JointWork, asm)
        {
            id = joint, i0 = (int)CraftJointWork.Stitch, count = n, i1 = 1, flag = true, i2 = cut, a = mean, b = spread, c = spacing, d = align,
        };
        Send(pull, (ok, _) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(asm);
            CraftJointSpec j = q != null ? Rules.Design(q.assembly.design)?.Joint(joint) : null;
            JointState js = j != null ? q.assembly.Joint(joint) : null;
            if (js == null) return;
            int done = Math.Max(1, (int)Math.Ceiling(j.count * 0.8));
            if (js.stitches >= j.count) said = $"The {j.Name.ToLowerInvariant()} is sewn all along ({js.stitches}/{j.count}).";
            else if (js.stitches >= done && js.stitches - n * StitchesPerClick < done) said = $"The {j.Name.ToLowerInvariant()} holds now ({js.stitches}/{j.count}): sewn all along it is stronger.";
        });
    }

    // ================================================================== the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Cancel)
        {
            Cancel();
            return;
        }
        if (phase == CraftPointer.Move) return;
        if (phase == CraftPointer.Scroll && place != Place.Layout && place != Place.Cut) return;
        switch (place)
        {
            case Place.Spin: case Place.Ply: SpinPointer(phase, pos); break;
            case Place.Skein: SkeinPointer(phase, pos); break;
            case Place.Weave: WeavePointer(phase, pos); break;
            case Place.Fit: FitPointer(phase, pos); break;
            case Place.Layout: LayoutPointer(phase, pos, scroll); break;
            case Place.Needle: NeedlePointer(phase, pos); break;
            case Place.Cut: CutPointer(phase, pos, scroll); break;
            default: SewPointer(phase, pos); break;
        }
    }

    /// <summary>The right button: what the hands hold is put down - never committed, never destroyed.</summary>
    private void Cancel()
    {
        string what = drafting ? "You let go of the fibre: the yarn drawn so far is on the bobbin."
            : pulling ? "The pull let go: no stitch."
            : taping ? "The tape let go: that mark is not measured."
            : windingSkein ? "The niddy-noddy is still: the wraps wound so far are kept."
            : winding ? "The warp beam let go: the warp is not on."
            : snipping ? "The shears open again: no snip."
            : lastEnd.x > -900f && place == Place.Needle ? "The thread drawn back."
            : place == Place.Cut && PinnedHere && cut.snips.Count == 0 && !cut.sent ? "Unpinned."
            : place == Place.Cut && PinnedHere && cut.snips.Count > 0 ? "The cut is started: Unpin (twice) to start it over."
            : "";
        if (place == Place.Cut && PinnedHere && cut.snips.Count == 0 && !cut.sent && !snipping) { cut = null; ShowPattern(); }
        PutDown();
        if (what.Length > 0) said = what;
    }

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

    /// <summary>A labelled meter: the label at <paramref name="x"/>, the bar after it, <paramref name="w"/> wide in all.</summary>
    private static void Gauge(CraftPixels c, float x, float w, float y, string label, float value, Color fill, params (float, float, Color)[] bands) =>
        Gauge(c, x, w, y, label, CraftArt.LabelWidth(label) + 8f, value, fill, bands);

    /// <summary>The wheel's meters, stacked: their bars start this far after their names, so they line up.</summary>
    private const float SpinLabels = 56f;

    /// <summary>A labelled meter whose bar starts <paramref name="labelWidth"/> after <paramref name="x"/> (stacked bars line up).</summary>
    private static void Gauge(CraftPixels c, float x, float w, float y, string label, float labelWidth, float value, Color fill, params (float, float, Color)[] bands)
    {
        CraftArt.Label(c, x, y, label, CraftArt.LabelInk);
        Meter(c, x + labelWidth, y, w - labelWidth, 10f, value, fill, bands);
    }

    // ================================================================== the controls and the words
    public override void Refresh()
    {
        RefreshCounts(fibre, fibres);
        NoteBatches(fibre, fibres);
        RefreshCounts(thread, threads);
        RefreshYarns();
        RefreshPatterns();
        RefreshSeams();
        RefreshWindOn();
        // what was kept for pieces that are gone goes with them; a skein whose bobbin changed under it (plied, woven, wound on) starts again
        if (cut != null && Ws.Piece(cut.cloth) == null && !cut.sent) cut = null;
        ShowPattern(); // the pattern menus as the cloth picked has them (pinned there, or the pattern in hand)
        List<string> gone = null;
        foreach (KeyValuePair<string, Skein> kv in skeins)
        {
            CraftPiece y = Ws.Piece(kv.Key);
            if (y != null && (kv.Value.wraps == 0 || Math.Abs(y.yarn.length - kv.Value.length) < 0.5)) continue;
            (gone ??= new List<string>()).Add(kv.Key);
            if (y != null && y.id == ui.SelectedId && place == Place.Skein) said = "The bobbin changed since you began winding it: the skein starts again.";
        }
        foreach (string id in spinBobbins.Keys) if (!id.StartsWith("bag:", StringComparison.Ordinal) && Ws.Piece(id) == null && !spinBobbins[id].waiting) (gone ??= new List<string>()).Add(id);
        if (gone != null) foreach (string id in gone) { skeins.Remove(id); spinBobbins.Remove(id); }
        if (draft != null)
        {
            // a bobbin is spun at one draft: the slider holds still while one is on the wheel
            WheelBobbin b = place == Place.Spin ? SpinBobbin : null;
            bool started = b != null && b.metres > 0f && b.draft >= 0f;
            if (started && Mathf.Abs(draft.value - b.draft) > 1e-4f) SetSlider(draft, b.draft);
            draft.interactable = !started;
        }
        if (draft != null) SetSlider(draft, draft.value); // its reading follows the fibre now spun
        if (weaveWidth != null)
        {
            // the warp's width is fixed while it is on the loom
            if (warpTension >= 0f && Mathf.RoundToInt(weaveWidth.value) != warpWidth) SetSlider(weaveWidth, warpWidth);
            weaveWidth.interactable = warpTension < 0f && !cuttingOff;
        }
        if (cutOffButton != null) cutOffButton.interactable = warpTension >= 0f && !cuttingOff;
        if (windOnButton != null) windOnButton.interactable = warpTension < 0f && windOnIds.Count > 0;
        if (cutFreeButton != null) cutFreeButton.interactable = PinnedHere && !cut.sent;
        if (info == null) return;
        // what just happened first (a bobbin's verdict, a refusal's why), then how the place is worked
        info.text = (said + " " + How()).Trim();
    }

    /// <summary>The bobbins of yarn on the station, for plying (their lengths change as they are woven and plied).</summary>
    private void RefreshYarns()
    {
        var ys = new List<string>();
        var names = new List<string>();
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Yarn && q.location == CraftLocation.Air && !OnLoom(q.id)) // the warp on the loom is not plied
            {
                ys.Add(q.id);
                names.Add($"{CraftLook.Name(Rules, q, ui.Known)} ({q.yarn.length:0} m)");
            }
        string yk = string.Join(",", names) + "#" + string.Join(",", ys);
        if (yk == yarnsKey || strandA == null) return;
        string a = Strand(strandA), b = Strand(strandB);
        yarnsKey = yk;
        yarns.Clear();
        yarns.AddRange(ys);
        if (names.Count == 0) names.Add("-");
        strandA.ClearOptions(); strandA.AddOptions(names);
        strandB.ClearOptions(); strandB.AddOptions(names);
        int ia = yarns.IndexOf(a), ib = yarns.IndexOf(b);
        strandA.SetValueWithoutNotify(ia >= 0 ? ia : 0);
        strandB.SetValueWithoutNotify(ib >= 0 ? ib : Mathf.Min(1, names.Count - 1));
        if (PlyKey != plyBobbin.key) StrandsChanged();
    }

    /// <summary>The other bobbins of the loom's yarn that could be wound onto it (same fibre, same plies).</summary>
    private void RefreshWindOn()
    {
        if (windOn == null) return;
        CraftPiece y = place == Place.Weave ? WeaveYarn : null;
        var ids = new List<string>();
        var names = new List<string>();
        if (y != null)
            foreach (CraftPiece q in Ws.Pieces)
                if (q != y && q.kind == CraftPieceKind.Yarn && Ws.CanJoinYarn(y.id, q.id, out _))
                {
                    ids.Add(q.id);
                    names.Add($"{CraftLook.Name(Rules, q, ui.Known)} ({q.yarn.length:0} m)");
                }
        string key = (y?.id ?? "") + "#" + string.Join(",", names);
        if (key == windOnKey) return;
        string was = windOnIds.Count > 0 ? windOnIds[Mathf.Clamp(windOn.value, 0, windOnIds.Count - 1)] : null;
        windOnKey = key;
        windOnIds.Clear();
        windOnIds.AddRange(ids);
        windOn.ClearOptions();
        windOn.AddOptions(names.Count > 0 ? names : new List<string> { y == null ? "-" : "no other bobbin of this yarn" });
        windOn.SetValueWithoutNotify(Mathf.Max(0, ids.IndexOf(was)));
    }

    /// <summary>The seams of the item picked, each with how far it is sewn (picked anew when another item is picked).</summary>
    private void RefreshSeams()
    {
        if (seam == null) return;
        CraftPiece a = Assembly;
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        var ids = new List<string>();
        var names = new List<string>();
        if (d != null)
            foreach (CraftJointSpec j in d.joints)
                if (j.kind == CraftJointKind.Stitch)
                {
                    JointState js = a.assembly.Joint(j.id);
                    int s = js != null ? js.stitches : 0;
                    ids.Add(j.id);
                    names.Add($"{j.Name} {s}/{j.count}{(s >= j.count ? " - sewn" : "")}");
                }
        string item = a != null ? a.id + a.assembly.design : "";
        string key = item + "#" + string.Join(",", names);
        if (key == seamsFor) return;
        bool other = !seamsFor.StartsWith(item + "#", StringComparison.Ordinal) || item.Length == 0;
        if (other)
        {
            FlushStitches(); // to the seam they were sewn on, before the list changes under them
            stitches.Clear();
            lastStitchX = -1f;
        }
        string was = SeamId;
        seamsFor = key;
        seams.Clear();
        seams.AddRange(ids);
        seam.ClearOptions();
        seam.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        int keep = ids.IndexOf(was);
        if (other || keep < 0)
        {
            // another item: the first seam that can be sewn and is not sewn all along
            keep = 0;
            for (int i = 0; i < ids.Count; i++)
                if (SeamProblem(a, d.Joint(ids[i])) == null) { keep = i; break; }
        }
        seam.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, ids.Count - 1)));
    }

    private string How()
    {
        switch (place)
        {
            case Place.Spin:
            {
                CraftPiece batch = Batch;
                WheelBobbin b = SpinBobbin;
                SpinSource(out double grams, out double prep);
                string from = batch != null ? $"the batch of {Rules.Supply(batch.fibre.fibre)?.Name.ToLowerInvariant() ?? "fibre"} ({batch.fibre.grams:0} g left)"
                    : FibreId != null && Have(FibreId) != 0 ? $"{ItemOf(FibreId)} from the bag (laid on the bench as a batch)" : null;
                if (from == null) return $"<b>Spin</b>: you need {ItemOf(FibreId ?? "fibre")} in your bag, or a batch on the fibre bench.";
                if (b.waiting) return "<b>A full bobbin</b> is coming off the wheel.";
                if (Fill(b) >= 1f) return "<b>A full bobbin</b> is on the wheel: draw on the fibre again to wind it off.";
                // what the preparation leaves of an even draft (and what drafting fine costs poorly prepared fibre)
                double keeps = CraftFibre.DraftEvenness(prep, b.draft >= 0f ? b.draft : draft.value), fineCost = 1 - keeps / CraftFibre.DraftEvenness(prep, 0);
                bool started = b.metres > 0f;
                string note = prep < 0.5 ? (started ? " Unprepared fibre: it spins lumpy however even the draft." : $" Unprepared fibre spins lumpy however even the draft (it keeps {keeps * 100:0}% of it at this draft): prepare it at the fibre bench, or draft it thicker.")
                    : fineCost > 0.08 ? " Too fine a draft for how it was prepared: it will be lumpy." : "";
                // the foot keeps the wheel turning while the hand drafts (the key, or a click on the pedal between draws)
                string foot = $"treadle with {StationControls.K(StationControl.Quick)} (or click the pedal) about once a second";
                if (started)
                    return $"<b>Spinning</b> {from}, the bobbin {Fill(b) * 100:0}% full: {foot} as you draw right at an even pace; the TWIST in the green (the gold mark: the bobbin's twist so far).{note}";
                return $"<b>Spin</b> {from}: {foot}, and keep on as you draft (WHEEL); hold in the hand area and draw right at an even pace, back left to wind on. Keep the TWIST green: amber reels a coarser thread, red pulls apart (drawn too fast) or kinks (too slow, or held still). Draft: finer gives more metres from {grams:0} g but takes longer and weaves a thinner cloth.{note}";
            }
            case Place.Ply:
                return yarns.Count < 2 ? (warpTension >= 0f ? "<b>Ply</b>: it takes two bobbins of yarn besides the warp on the loom." : "<b>Ply</b>: spin two bobbins of yarn first.")
                    : plyBobbin.waiting ? "<b>A full bobbin</b> of plied yarn is coming off the wheel."
                    : $"<b>Ply</b> the two bobbins: treadle with {StationControls.K(StationControl.Quick)} (or click the pedal) as you hold in the hand area and feed the strands right at an even pace, the TWIST in its green band ({CraftFibre.PlyBalanceAt * 100:0}% of the singles' twist, the other way: it hangs straight). The ply goes on the shorter bobbin; the other keeps its rest.";
            case Place.Skein:
            {
                CraftPiece y = YarnPiece;
                if (y == null) return "<b>Skein</b>: pick a bobbin of yarn (left).";
                if (OnLoom(y.id)) return $"<b>Skein</b>: {WarpOnLoom}";
                Skein k = SkeinOf(y);
                if (y.yarn.measured && k.wraps == 0)
                    return $"<b>A counted skein</b> of {y.yarn.measuredLength:0} m: reel it off as thread ({CraftFibre.ThreadCount(y)} × {CraftFibre.MetresPerThread:0} m), or wind it again to count it again.";
                return $"<b>Skein</b>: hold and circle round the niddy-noddy, one way round: each turn winds {WrapsPerTurn(y)} wraps of {CraftFibre.MetresPerWrap} m ({k.counted} counted, {Math.Max(0, y.yarn.length - k.wraps * CraftFibre.MetresPerWrap):0} m to go). The TURN gauge is amber while a turn is too quick to count: finish it once it is green. It ties off when the bobbin is empty.";
            }
            case Place.Weave:
            {
                CraftPiece y = WeaveYarn;
                CraftHideDefinition cloth = y != null ? CraftFibre.ClothOf(Rules, y) : null;
                if (y == null) return "<b>Weave</b>: spin a bobbin of yarn first (or pick one, left).";
                if (cloth == null) return "Nothing is woven from that yarn.";
                int rows = RowsAvailable(y);
                if (warpTension < 0f)
                    return rows < 1 ? $"<b>Weave {cloth.Name.ToLowerInvariant()}</b>: {TooLittle(y)}"
                        : $"<b>Weave {cloth.Name.ToLowerInvariant()}</b>: {y.yarn.length:0} m of yarn make {rows} row{(rows == 1 ? "" : "s")} {Width} wide ({CraftFibre.MetresPerCell * Width:0} m a row, {CraftFibre.MaxRows} at most). Wind more bobbins of this yarn on for a longer length; then hold on the warp beam and let go with the needle in the green band.";
                return $"<b>Weaving</b> {warpWidth} wide: click the loom to beat {(RowsPerBeat == 1 ? "a row" : $"{RowsPerBeat} rows")} in when the marker is on the target ({Mathf.Min(RowsWoven, rows)}/{rows} rows); a bad miss skips a pick. Cut it off the loom when done.";
            }
            case Place.Fit:
                return markAt >= Marks.Length ? "Measured. Click to measure again."
                    : $"<b>Measure</b> {MarkNames[markAt]}: press on the gold dot and drag the tape to its ring - its end sways with your hand (more as the arm tires): let go as it crosses the ring. Within {CarefulPx:0} px of the ring is careful; past {RoughPx:0} px it fits worse than no measure at all.";
            case Place.Layout:
                return Cloth == null ? $"<b>Lay out</b>: {PickCloth("laid out")}"
                    : "<b>Lay out</b>: click to chalk the pattern (green on the grain, amber across it; the wheel or Turn it turns it); patterns may not overlap. Those the items here still want come first in the menu. A cut on the chalk is cleaner.";
            case Place.Needle:
            {
                if (threaded) return $"<b>Threaded</b> ({threadLeft} stitches left).{ThreadFor()}";
                bool item = ItemThreaded(out _);
                if (!item && (threads.Count == 0 || Have(ThreadId) == 0)) return $"<b>Thread the needle</b>: you need {ItemOf(ThreadId ?? "thread")} in your bag - spin it at the wheel, or bring some.";
                return $"<b>Thread the needle</b>{(item ? " with the item's own thread" : "")}: hold and bring the thread's end through the eye, left to right, in the middle of it. It trembles - wait for a still moment. A frayed end is fatter: snip it.{ThreadFor()}";
            }
            case Place.Cut:
            {
                if (Cloth == null) return $"<b>Cut cloth</b>: {PickCloth("cut")}";
                if (cut == null || cut.cloth != Cloth.id)
                    return "<b>Cut cloth</b>: move the pattern over the cloth - green on the grain, amber across it, red where it will not go; it snaps onto its own chalk (cleaner). The wheel or Turn it turns it; click to pin it.";
                return $"<b>Cut round it</b>: press and release for each snip, from the gold ring along the dashed line (three cells a snip at most). {cut.Coverage * 100:0}% cut: it comes free once cut all round, or Cut it free from {FreeFrom * 100:0}% (the rest tears). Unpin starts over.";
            }
            default:
            {
                CraftPiece a = Assembly;
                if (a == null) return "<b>Sew</b>: pick the item being made (start it at the Assembly area and fit its cloth parts).";
                CraftJointSpec j = Rules.Design(a.assembly.design)?.Joint(SeamId);
                if (j == null) return "It has no seams to sew.";
                string problem = SeamProblem(a, j);
                if (problem != null) return $"<b>Sew</b>: {problem}";
                if (!threaded) return "<b>Sew</b>: thread the needle first.";
                JointState js = a.assembly.Joint(j.id);
                int s = js != null ? js.stitches : 0, done = Math.Max(1, (int)Math.Ceiling(j.count * 0.8));
                return $"<b>Sew the {j.Name.ToLowerInvariant()}</b> ({s}/{j.count}: it holds from {done}, strongest sewn all along): click along the chalk line, left to right, {(StitchesPerClick == 1 ? "a stitch" : $"a run of {StitchesPerClick} stitches")} as long as the white mark a click; hold to pull - PULL in the green, never the red.";
            }
        }
    }
}
