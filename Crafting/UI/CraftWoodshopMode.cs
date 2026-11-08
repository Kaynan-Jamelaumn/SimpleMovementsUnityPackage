using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The woodshop, in seven places, each worked with the hands on the drawing:
/// <list type="bullet">
/// <item><b>Debark</b>: the log on the shaving horse, its end in section beside it. Pull the drawknife along the log
/// (drag right to left) with its edge on the bright line under the bark: the DEPTH gauge shows where it peels (green),
/// only skims (grey) or gouges the wood (red) - the same depths the workshop uses, green wood peeling deeper than dry.
/// Two pulls bare a face; turn the log (the wheel, or Turn the log) to reach the bark on its other faces. A green log
/// peels in one round of the faces, a dry one's tight bark takes two.</item>
/// <item><b>Split</b>: the piece's end, standing on the block as it really is - a round log, a half, a quarter, a billet.
/// Drag a line right across it where the wedge goes - across the side the wedges part (a half's from its flat face out):
/// the halves are as even as the line divides the end, and a line through the heart along a ray (or the check) splits
/// straight; one that misses the heart crosses the rays. Then drive the wedge with the maul (hold and let go, full, square
/// blows on the wedge) until it parts. The wedges take a piece of 60 mm or more, 40 mm thick or more; smaller is riven,
/// a thin board ripped.</item>
/// <item><b>Rive</b>: the billet from the side at its real size, its grain drawn along it. Set the froe on a grain line at
/// the end (where it is set is where it parts: off the middle the halves are uneven and the split runs out), drive it
/// in with the mallet (two good blows), then lever the split along from its end: keep the pointer on the grain line as
/// it wanders, at an even pace.</item>
/// <item><b>Saw</b>: the board from above. Across: click to mark where to cut (it is cut exactly there), press on the
/// mark and stroke up and down across the board, long even strokes, keeping on the line. A taper: click where the line
/// starts and where it ends (its width there), then saw along it - both edges are ripped to it.</item>
/// <item><b>Steam &amp; bend</b>: drag the piece by its clamped end into the steam box: it heats through (HEAT), then soaks
/// (SOAK). Drag it out and bend it at once - it stays soft (SOFT) only a minute or so: drag its free end round the form,
/// at an even pace. The STRAIN gauge is how hard the form strains its outside fibres at the pull's fastest pace so far -
/// what the bend is judged by - with the pace now as a thin tick (past the tick they tear; right-click lets it spring back
/// unbent); the strap holds them. The part says how much bend it wants.</item>
/// <item><b>Dry</b>: lay stickers under the board (one under each end, the rest evenly between - four or more), hold on
/// each end to seal it with wax (a seal stays with the piece), pull the cover over the stack (it shades every board in it),
/// then drag the board onto the stack. Seasoning too fast - in the sun, unsealed, on uneven stickers - checks and warps
/// it; thick stock seasons slowly (rive it first).</item>
/// <item><b>Finish</b>: sand it through the grits along the grain - each paper only clears the scratches of the one before
/// (the strip under the board shows, stretch by stretch, where the paper in hand bites and where its scratches are too deep
/// for it; the SURFACE gauge how rough it is on the whole) - then brush on a varnish (a film), rub in oil (it soaks into
/// the grain) or wax: thin, even coats on dry wood, each left to dry at its own pace. A pot opened lays about two
/// full-length coats.</item>
/// </list>
/// Right-click puts down what the hands hold: the knife lifts, the maul and the mallet come down without striking, the
/// saw stops in its kerf, a bend springs back, a carried board goes back. Work begun on a piece - a wedge half driven,
/// the froe in, a kerf, stickers and seals - stays with that piece while another place, area or piece is picked. A test
/// station has quick buttons for all of it.
/// </summary>
public sealed class CraftWoodshopMode : CraftMode
{
    private enum Place { Debark, Split, Rive, Saw, Bend, Dry, Finish }
    private enum FinishTool { Coarse, Medium, Fine, Brush }
    private enum Hold { None, SealLeft, SealRight, Cover }
    private static readonly string[] PlaceNames = { "Debark", "Split log", "Rive", "Saw", "Steam & bend", "Dry", "Finish" };
    private static readonly float[] Grits = { 0.25f, 0.55f, 0.85f };
    private static readonly string[] GritNames = { "coarse", "medium", "fine" };
    private static readonly string[] FormNames = { "Tight form (6 cm)", "Medium form (12 cm)", "Gentle form (25 cm)", "Snath form (60 cm)" };
    private static readonly float[] FormRadius = { 60f, 120f, 250f, 600f };

    /// <summary>
    /// The hand work under way on one piece, kept with it (by its id) when another piece, place or area is picked: the
    /// faces drawn on the horse, the wedge and how far it is driven, the froe, the saw's mark and kerf, the stickers and
    /// the sealed ends. Cleared when the work is done or the piece is gone - or cut since (elsewhere, by other hands):
    /// marks kept as shares of its length, and faces kept by its stretches, no longer fall where they were made.
    /// </summary>
    private sealed class PieceWork
    {
        /// <summary>The piece's length (mm) and stretches when the work was begun: the marks and faces are of that piece.</summary>
        public double length;
        public int stretches;
        public readonly Dictionary<long, int> faceStrokes = new Dictionary<long, int>();
        public int face;
        public bool wedgeSet, splitSent;
        public Vector2 lineA, lineB;
        public float splitWork, splitSolid;
        public int splitBlows;
        public int riveLine = -1, riveStage, malletBlows;
        public float riveFront, riveStray, riveWeight, riveJerk;
        public bool riveSent;
        public readonly List<Vector2> splitPath = new List<Vector2>();
        public float mark = -1f, ripA = -1f, ripB = -1f, ripWa, ripWb;
        public CraftSawStroke saw = new CraftSawStroke();
        public bool sawStarted, sawSent;
        /// <summary>The saw under way is a rip along the taper (else a cut across at the mark).</summary>
        public bool sawRip;
        public readonly List<float> stickers = new List<float>();
        public float sealLeft, sealRight;
    }

    private Place place = Place.Debark;
    private FinishTool finishTool = FinishTool.Coarse;
    private bool ripMode;
    private readonly List<Button> placeButtons = new List<Button>(), finishButtons = new List<Button>(), sawButtons = new List<Button>();
    private readonly Dictionary<Place, RectTransform> placeRows = new Dictionary<Place, RectTransform>();
    private TMP_Dropdown form, finishSupply;
    private Slider bendFrom;
    private Button strapButton;
    private TextMeshProUGUI faceText;
    private bool strap = true;
    private TextMeshProUGUI info;
    private readonly List<string> finishes = new List<string>();
    private string said = "";
    private readonly Dictionary<string, PieceWork> works = new Dictionary<string, PieceWork>();
    /// <summary>The press under way acted when it went down (a mark, a sticker, the froe set): its drag and release do nothing.</summary>
    private bool ignorePress;

    public override string Title => "Woodshop";
    public override CraftDiscipline Discipline => CraftDiscipline.Woodwork;
    public override bool Available(CraftStation s) =>
        s.Has(CraftTools.ChoppingBlock) || s.Has(CraftTools.Sawhorse) || s.Has(CraftTools.SteamBox) || s.Has(CraftTools.DryingStack) || s.Has(CraftTools.FinishingBench) || s.Has(CraftTools.ShavingHorse);
    public override string Hint => "Pick a place: debark the log, split it, rive along the grain, saw to the line, steam and bend, season in the stack, sand and seal - each with the hands, on the drawing. Right-click puts down.";
    // the maul on the wedge and the mallet on the froe strike blows; the drawknife, the saw and the papers are strokes
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, place == Place.Split || place == Place.Rive);

    /// <summary>Does the station have the tools for a place?</summary>
    private bool Here(Place p)
    {
        if (Station == null) return false;
        switch (p)
        {
            case Place.Debark: return Station.Has(CraftTools.ShavingHorse);
            case Place.Split: case Place.Rive: return Station.Has(CraftTools.ChoppingBlock);
            case Place.Saw: return Station.Has(CraftTools.Sawhorse) || Station.Has(CraftTools.Files);
            case Place.Bend: return Station.Has(CraftTools.SteamBox);
            case Place.Dry: return Station.Has(CraftTools.DryingStack);
            default: return Station.Has(CraftTools.FinishingBench);
        }
    }

    // ------------------------------------------------------------------ building
    public override void Build(RectTransform controls)
    {
        placeRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        if (!Here(place))
            for (int i = 0; i < PlaceNames.Length; i++)
                if (Here((Place)i)) { place = (Place)i; break; }
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(r0, PlaceNames[i], () => GoToPlace(pl), 0f, UITheme.ButtonStyle.Tab, 28f);
            b.gameObject.SetActive(Here(pl));
            placeButtons.Add(b);
        }

        RectTransform debark = Row(controls);
        Btn(debark, "Turn the log", () => TurnLog(1), 130f);
        faceText = Text(debark, "", 13f);
        placeRows[Place.Debark] = debark;

        RectTransform split = Row(controls);
        Btn(split, "Lift the wedge out", LiftWedge, 170f);
        Text(split, "A press on the end draws the wedge's line; then hold and let go on the wedge to drive it.", 12f);
        placeRows[Place.Split] = split;

        RectTransform rive = Row(controls);
        Btn(rive, "Lift the froe out", LiftFroe, 160f);
        Text(rive, "Set the froe on a grain line at the left end; two mallet blows; then lever from the split's end.", 12f);
        placeRows[Place.Rive] = rive;

        RectTransform saw = Row(controls, 30f, 4f);
        sawButtons.Clear();
        sawButtons.Add(Btn(saw, "Across", () => SetRip(false), 110f, UITheme.ButtonStyle.Tab, 28f));
        sawButtons.Add(Btn(saw, "A taper (rip)", () => SetRip(true), 130f, UITheme.ButtonStyle.Tab, 28f));
        Btn(saw, "Clear the marks", ClearMarks, 140f, UITheme.ButtonStyle.Ghost, 28f);
        placeRows[Place.Saw] = saw;

        RectTransform bend = Row(controls);
        form = Drop(bend, FormNames, 1, 180f);
        strapButton = Btn(bend, "Strap on", () => { strap = !strap; Show(); }, 96f);
        bendFrom = Slide(bend, "Bend from", 0.1f, 0.9f, 0.5f, false, 70f, v => BendFromText(v));
        placeRows[Place.Bend] = bend;

        RectTransform dry = Row(controls);
        Btn(dry, "Take up the stickers", () => { PieceWork k = Current; if (k != null) { k.stickers.Clear(); said = "The stickers are taken up."; } }, 180f);
        Text(dry, "Stickers: under each end and evenly between. Seal: hold an end. Cover: drag its handle.", 12f);
        placeRows[Place.Dry] = dry;

        RectTransform fin = Row(controls, 30f, 4f);
        finishButtons.Clear();
        string[] tools = { "Coarse", "Medium", "Fine", "Brush / rub" };
        for (int i = 0; i < tools.Length; i++)
        {
            FinishTool t = (FinishTool)i;
            finishButtons.Add(Btn(fin, tools[i], () => { finishTool = t; EndGestures(false); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        finishes.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Varnish || s.kind == CraftSupplyKind.Oil || s.kind == CraftSupplyKind.Wax) finishes.Add(s.id);
        finishSupply = CountedDrop(fin, finishes, 236f, "No finish");
        finishSupply.onValueChanged.AddListener(_ => { if (load > 0f) { load = 0f; said = "A different finish: dip the brush afresh."; } });
        placeRows[Place.Finish] = fin;

        if (QuickBench)
        {
            RectTransform q = Row(controls);
            Btn(q, "Debark", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.DebarkWood, p.id) { i0 = 0, i1 = p.segments.Count - 1, a = 0.45f, b = 0.95f, c = 1f }); }, 70f);
            Btn(q, "Split", () => Send(new CraftRequest(CraftRequestKind.SplitLog, ui.SelectedId) { a = 0.02f, b = 0.95f, c = 0.9f, i0 = 5 }), 50f);
            Btn(q, "Rive", () => Send(new CraftRequest(CraftRequestKind.Rive, ui.SelectedId) { a = 0.05f, b = 0.9f, c = 0.5f }), 60f);
            Btn(q, "Saw in half", () => Send(new CraftRequest(CraftRequestKind.SawCut, ui.SelectedId) { a = 0.5f, b = 0.05f, c = 0.95f }), 100f);
            Btn(q, "Steam", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Steam }), 70f);
            Btn(q, "Bend 90°", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.BendWood, p.id) { i0 = p.segments.Count / 4, i1 = p.segments.Count * 3 / 4, a = 90f, b = 0.2f, c = FormR, flag = true }); }, 80f);
            Btn(q, "Stack", () => Send(new CraftRequest(CraftRequestKind.StackWood, ui.SelectedId) { a = 1f, b = 1f, c = 1f }), 60f);
            Btn(q, "Sand", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.SandWood, p.id) { i0 = 0, i1 = p.segments.Count - 1, a = 0.9f, b = 10f, c = 1f }); }, 60f);
            Btn(q, "Varnish", () => { CraftPiece p = Selected; if (p != null && finishes.Count > 0) Send(new CraftRequest(CraftRequestKind.Varnish, p.id) { id = finishes[0], i0 = 0, i1 = p.segments.Count - 1, a = 0.9f, b = 0.5f }); }, 70f);
        }
        info = InfoRow(controls, 48f);
        Show();
    }

    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Place p) && Here(p)) GoToPlace(p);
    }

    public override bool HasPlace(string place) => Enum.TryParse(place, out Place p) && Here(p);

    public override string PlaceTitle(string place) => Enum.TryParse(place, out Place p) ? PlaceNames[(int)p] : place;

    /// <summary>Another place: the hands put down what they hold there; the work begun on the piece stays with it.</summary>
    private void GoToPlace(Place p)
    {
        if (p == place) return;
        EndGestures(false);
        place = p;
        said = "";
        Show();
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        for (int i = 0; i < finishButtons.Count; i++) UITheme.SetSelected(finishButtons[i], (FinishTool)i == finishTool);
        for (int i = 0; i < sawButtons.Count; i++) UITheme.SetSelected(sawButtons[i], (i == 1) == ripMode);
        foreach (KeyValuePair<Place, RectTransform> kv in placeRows)
            if (kv.Value != null) kv.Value.gameObject.SetActive(kv.Key == place);
        TMP_Text t = strapButton != null ? strapButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = strap ? "Strap on" : "No strap";
        if (strapButton != null) UITheme.SetSelected(strapButton, strap);
    }

    // ------------------------------------------------------------------ the piece, its work
    private CraftPiece WoodPiece => Selected != null && Selected.kind == CraftPieceKind.Wood && Selected.segments.Count > 0 ? Selected : null;

    private PieceWork Work(CraftPiece p)
    {
        if (p == null) return null;
        if (works.TryGetValue(p.id, out PieceWork k) && Math.Abs(k.length - p.Length) <= 0.5 && k.stretches == p.segments.Count)
            return k;
        if (k != null && (k.sawSent || k.splitSent || k.riveSent))
            return k; // its own cut on the way: the answer clears the work
        if (k != null && (k.mark >= 0f || k.ripA >= 0f || k.sawStarted || k.stickers.Count > 0 || k.faceStrokes.Count > 0 || k.wedgeSet || k.riveStage > 0))
            said = "It has been cut since the work here was begun on it: the marks and the work are gone - begin again.";
        // a fresh start: the ends it has sealed are the piece's own (the workshop keeps a seal however often it is stacked)
        works[p.id] = k = new PieceWork { length = p.Length, stretches = p.segments.Count };
        k.sealLeft = k.sealRight = Mathf.Clamp01((float)p.wood.endsSealed);
        return k;
    }

    /// <summary>The work under way on the selected piece (null with no wooden piece picked).</summary>
    private PieceWork Current => Work(WoodPiece);

    /// <summary>Where a piece that is not on the bench is, and where it is taken out.</summary>
    private static string OutOfPlace(CraftPiece p)
    {
        switch (p.location)
        {
            case CraftLocation.Steam: return "It is in the steam box: drag it out at Steam & bend.";
            case CraftLocation.Stack: return "It is seasoning in the stack: drag it off the stack at Dry.";
            case CraftLocation.Assembled: return "It is fitted to an item: it is worked where it is fitted.";
            default: return $"It is {CraftStationUI.LocationText(p)}: take it out first.";
        }
    }

    public override string Busy
    {
        get
        {
            PieceWork k = Current;
            if (k == null) return null;
            if (k.wedgeSet && k.splitWork > 0.001f) return "splitting it";
            if (k.riveStage == 2 || k.riveStage == 1 && k.malletBlows > 0) return "riving it";
            if (k.sawStarted && k.saw.Progress > 0 && k.saw.Progress < 1) return k.sawRip ? "ripping the taper" : "sawing it through";
            return null;
        }
    }

    // ------------------------------------------------------------------ leaving, the piece changing
    public override void Leave() => EndGestures(true);

    public override void SelectionChanging(string from, string to)
    {
        EndGestures(true); // a pull, a pass, a coat under way goes to the piece it was made on
        said = "";
    }

    public override void SelectionChanged(string to)
    {
        SetSlider(bendFrom, bendFrom != null ? bendFrom.value : 0.5f); // "Bend from" in the millimetres of the piece now picked
        // a piece no longer on the station takes its work with it
        if (Station == null || Station.Workshop == null || works.Count == 0) return;
        var gone = new List<string>();
        foreach (string id in works.Keys)
            if (Ws.Piece(id) == null) gone.Add(id);
        foreach (string id in gone) works.Remove(id);
        // the saw goes the way the cut begun on this piece goes (across, or along its taper)
        PieceWork k = Current;
        if (k != null && k.sawStarted && k.saw.Progress > 0 && k.sawRip != ripMode)
        {
            ripMode = k.sawRip;
            Show();
        }
    }

    /// <summary>
    /// The hands put down what they hold: a pull of the drawknife, a pass of the paper or a coat of the brush already
    /// made is sent (when <paramref name="finish"/>) - the rest of a gesture (a raised maul or mallet, the saw, a lever, a
    /// bend, a carried board) is put down as it is, nothing lost of the piece's work.
    /// </summary>
    private void EndGestures(bool finish)
    {
        if (finish)
        {
            if (knife.Count > 0) Pull(WoodPiece);
            if (sanding.Active) EndSanding(WoodPiece);
            if (brushing.Count > 0) EndBrush(WoodPiece);
        }
        knife.Clear();
        sanding.Clear();
        sandPoints.Clear();
        sentPasses = 0;
        brushing.Clear();
        lining = false;
        maul.Lower();
        mallet.Lower();
        levering = false;
        if (sawing) StopSaw();
        dragging = false;
        bending = false;
        if (holding == Hold.Cover) { coverPending = true; coverSentAt = Time.unscaledTime; } // it stays where the hand left it
        holding = Hold.None;
        coverHeld = false;
        ignorePress = ui != null && ui.PointerHeld; // the rest of a press under way does nothing
        LiveDraw = false;
    }

    public override void Tick(float dt)
    {
        mallet.Tick(dt);
        maul.Tick(dt);
        if (holding != Hold.None && !ui.PointerHeld)
        {
            if (holding == Hold.Cover) { coverPending = true; coverSentAt = Time.unscaledTime; }
            holding = Hold.None;
            coverHeld = false;
        }
        PieceWork k = Current;
        if (k != null && holding == Hold.SealLeft) k.sealLeft = Mathf.Min(1f, k.sealLeft + dt / 1.2f);
        if (k != null && holding == Hold.SealRight) k.sealRight = Mathf.Min(1f, k.sealRight + dt / 1.2f);
        if (coverPending && Station != null && Station.Workshop != null &&
            (Ws.StackCover.HasValue && Mathf.Abs((float)Ws.StackCover.Value - coverValue) < 0.01f || Time.unscaledTime - coverSentAt > 1.5f))
            coverPending = false;
        LiveDraw = knife.Count > 0 || lining || maul.Raised || mallet.Raised || levering || sawing || dragging || bending || holding != Hold.None
                   || sanding.Active || brushing.Count > 0 || place == Place.Bend && WoodPiece != null && WoodPiece.wood.steamed > 0;
    }

    // ------------------------------------------------------------------ drawing the wood
    private Color WoodColour(CraftPiece p)
    {
        Color wood = Rules.Wood(p.wood.wood)?.color ?? new Color(0.7f, 0.55f, 0.35f);
        if (p.wood.moisture > 0.2) wood = Color.Lerp(wood, wood * 0.7f, Mathf.Clamp01(((float)p.wood.moisture - 0.2f) * 6f));
        if (p.wood.charred > 0.05) wood = Color.Lerp(wood, Color.black, (float)p.wood.charred);
        wood.a = 1f;
        return wood;
    }

    private static Color Bark(Color wood) => Color.Lerp(wood, new Color(0.28f, 0.2f, 0.14f), 0.75f);

    /// <summary>The grain line <paramref name="k"/> of <paramref name="lines"/> across a board drawn in <paramref name="r"/>, at x: it wanders, and slopes out of the side as much as the grain runs out.</summary>
    private static float GrainY(CraftPiece p, Rect r, int k, int lines, float x)
    {
        int seed = CraftMath.Hash(p.id ?? "");
        float t = (x - r.xMin) / Mathf.Max(1f, r.width);
        float baseY = r.yMin + (k + 0.5f) / lines * r.height;
        float wave = Mathf.Sin(t * (5f + k % 3) + (seed % 100) * 0.1f + k) * r.height * 0.06f + Mathf.Sin(t * 13f + k * 2.1f) * r.height * 0.02f;
        float slope = (float)p.wood.runout * 1.6f * ((seed >> 3) % 2 == 0 ? 1f : -1f);
        return baseY + wave + slope * (t - 0.5f) * r.height;
    }

    /// <summary>A board in a rectangle: its wood, grain, checks and whatever finish is on it, stretch by stretch.</summary>
    private void DrawBoard(CraftPixels c, CraftPiece p, Rect r, bool grain = true, int highlightLine = -1)
    {
        Color wood = WoodColour(p);
        c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, wood);
        if (grain)
        {
            const int lines = 7;
            for (int k = 0; k < lines; k++)
            {
                float lastY = GrainY(p, r, k, lines, r.xMin);
                for (float x = r.xMin + 4f; x <= r.xMax; x += 4f)
                {
                    float y = GrainY(p, r, k, lines, x);
                    if (y > r.yMin && y < r.yMax && lastY > r.yMin && lastY < r.yMax)
                        c.Line(x - 4f, lastY, x, y, k == highlightLine ? Mark : Color.Lerp(wood, Color.black, 0.25f), k == highlightLine ? 2f : 1f);
                    lastY = y;
                }
            }
        }
        DrawChecks(c, p, r);
        DrawCoats(c, p, r);
        c.Frame(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0.1f, 0.07f, 0.05f, 0.8f), 1f);
    }

    private static void DrawChecks(CraftPixels c, CraftPiece p, Rect r)
    {
        int checks = Mathf.RoundToInt((float)p.wood.checks * 12f);
        for (int k = 0; k < checks; k++)
        {
            float y = r.yMin + (k * 37 % 100) / 100f * r.height;
            float x = k % 2 == 0 ? r.xMin + 2f : r.xMax - 2f - 14f;
            c.Line(x, y, x + 14f, y + 2f, new Color(0.1f, 0.07f, 0.05f, 0.9f), 1.5f);
        }
    }

    /// <summary>The finish where it went on, stretch by stretch: a film's sheen (blotchy where uneven, a wet gleam while tacky), oil darkening the grain, a wax's soft shine.</summary>
    private static void DrawCoats(CraftPixels c, CraftPiece p, Rect r)
    {
        int n = p.segments.Count;
        WoodState w = p.wood;
        for (int i = 0; i < n; i++)
        {
            float coat = Mathf.Clamp01((float)p.segments[i].coating);
            if (coat < 0.02f) continue;
            float x0 = r.xMin + r.width * i / n, x1 = r.xMin + r.width * (i + 1) / n;
            if (w.finish > 0.02)
            {
                float blotch = (float)((1 - w.finishEven) * (CraftMath.Noise(CraftMath.Hash(p.id ?? ""), i, 7) - 0.5));
                float a = Mathf.Clamp01(coat * 1.6f + blotch * 0.6f);
                c.Rect(x0, r.yMin, x1 + 0.5f, r.yMax, new Color(0.7f, 0.38f, 0.12f, a * 0.5f));
                c.Rect(x0, r.yMin + r.height * 0.62f, x1 + 0.5f, r.yMin + r.height * 0.68f, new Color(1f, 0.95f, 0.8f, a * 0.35f)); // the gloss
            }
            else if (w.oil > 0.02)
                c.Rect(x0, r.yMin, x1 + 0.5f, r.yMax, new Color(0.45f, 0.26f, 0.08f, Mathf.Clamp01(coat) * 0.35f)); // oil darkens the grain
            if (w.wax > 0.02)
                c.Rect(x0, r.yMin + r.height * 0.55f, x1 + 0.5f, r.yMin + r.height * 0.75f, new Color(1f, 0.95f, 0.75f, Mathf.Clamp01(coat) * 0.25f)); // a soft shine
            if (w.finishWet > 0.3) c.Rect(x0, r.yMax - r.height * 0.3f, x1 + 0.5f, r.yMax - r.height * 0.2f, new Color(1f, 1f, 1f, 0.25f * coat));
        }
    }

    /// <summary>Roughness as specks, per stretch of the board drawn in <paramref name="r"/>.</summary>
    private static void DrawRough(CraftPixels c, CraftPiece p, Rect r)
    {
        int n = p.segments.Count;
        for (int i = 0; i < n; i++)
        {
            float x0 = r.xMin + r.width * i / n, w = r.width / n;
            int specks = Mathf.Clamp(Mathf.RoundToInt((float)p.segments[i].rough / 4f), 0, 24);
            for (int k = 0; k < specks; k++)
                c.Disc(x0 + (k * 29 % 97) / 97f * w, r.yMin + (k * 53 % 89) / 89f * r.height, 1f, new Color(0.15f, 0.1f, 0.06f, 0.55f));
        }
    }

    /// <summary>A labelled meter: the label in the drawing's letters at <paramref name="x"/>, the meter from <paramref name="x"/> + <paramref name="labelWidth"/>.</summary>
    private static void Gauge(CraftPixels c, string label, float x, float y, float w, float h, float value, Color fill, float labelWidth = 64f,
                              params (float from, float to, Color color)[] bands)
    {
        CraftArt.Label(c, x, y + h * 0.5f - 5f, label, CraftArt.LabelInk);
        Meter(c, x + labelWidth, y, w - labelWidth, h, value, fill, bands);
    }

    /// <summary>
    /// How hard a maul or mallet would come down if let go now (0-1: 1 a full blow; the green band on its gauge is a blow
    /// that drives): as the blow would land (<see cref="CraftHammer.Expected"/>) - a tired arm's lighter, one held up until
    /// the arm wavers weaker.
    /// </summary>
    private float Raised(CraftHammer h) => (float)(h.Expected(Hands) / (0.8 + 0.2 * h.weight));

    public override void Draw(CraftPixels c)
    {
        switch (place)
        {
            case Place.Debark: DrawDebark(c); break;
            case Place.Split: DrawSplit(c); break;
            case Place.Rive: DrawRive(c); break;
            case Place.Saw: DrawSaw(c); break;
            case Place.Bend: DrawBend(c); break;
            case Place.Dry: DrawDry(c); break;
            default: DrawFinish(c); break;
        }
    }

    // ================================================================== debarking
    private Rect logSide, logEnd;
    private readonly List<Vector2> knife = new List<Vector2>();
    private float knifeStart, knifeLast;

    /// <summary>The depth of the knife's edge (0 the bark's surface - 1 deep in the wood) at height y over the log.</summary>
    private float KnifeDepth(float y) => Mathf.Clamp01((logSide.yMax - y) / 36f);
    private float DepthY(float depth) => logSide.yMax - 36f * depth;
    private int Stretch(CraftPiece p, float x) => Mathf.Clamp(Mathf.FloorToInt((x - logSide.xMin) / logSide.width * p.segments.Count), 0, p.segments.Count - 1);
    private static long Key(int stretch, int face) => stretch * 16L + face;

    /// <summary>The bark showing on one face of a stretch: the stretch's own (the workshop's), worse on a face not yet pulled, none once it is bare.</summary>
    private static float FaceBark(CraftPiece p, PieceWork k, int i, int face)
    {
        float bark = (float)p.segments[i].bark;
        if (bark < 0.05f) return 0f;
        int strokes = k.faceStrokes.TryGetValue(Key(i, face), out int s) ? s : 0;
        return Mathf.Clamp01(bark * (strokes == 0 ? 1.3f : strokes == 1 ? 0.7f : 0.25f));
    }

    private void DrawDebark(CraftPixels c)
    {
        // The shaving horse: its bench and legs.
        c.Rect(30, 96, 520, 116, Wood);
        c.Rect(44, 30, 58, 96, Wood);
        c.Rect(492, 30, 506, 96, Wood);
        logSide = new Rect(50, 120, 470, 90);
        logEnd = new Rect(566, 104, 136, 136);
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air)
            return;
        PieceWork k = Work(p);
        Color wood = WoodColour(p), bark = Bark(wood);
        float gouge = (float)CraftWoodwork.GougeDepth(p);
        c.Rect(logSide.xMin, logSide.yMin, logSide.xMax, logSide.yMax, wood);
        int n = p.segments.Count;
        for (int i = 0; i < n; i++)
        {
            MetalSegment s = p.segments[i];
            float x0 = logSide.xMin + logSide.width * i / n, x1 = logSide.xMin + logSide.width * (i + 1) / n;
            // the bark on the face turned up, over the top of the log down to the line where it lifts
            float left = FaceBark(p, k, i, k.face);
            c.Rect(x0, DepthY((float)CraftWoodwork.PeelDepth), x1 + 0.5f, logSide.yMax, new Color(bark.r, bark.g, bark.b, left));
            c.Rect(x0, logSide.yMin + 4, x1 + 0.5f, DepthY((float)CraftWoodwork.PeelDepth), new Color(bark.r * 0.8f, bark.g * 0.8f, bark.b * 0.8f, left * 0.55f));
            for (int f = 0; f < 3 && left > 0.2f; f++) c.Line(x0 + 3 + f * 7, logSide.yMin + 14 + f * 18, x0 + 9 + f * 7, logSide.yMin + 28 + f * 18, bark * 0.7f, 1f);
            if (s.rough > 60) c.Disc(x0 + 6, logSide.yMin + 8, 2f, new Color(0.1f, 0.06f, 0.03f, 0.7f));
        }
        // the cambium: the bright line where the bark lifts, and the line past which the knife gouges
        c.Rect(logSide.xMin, DepthY((float)CraftWoodwork.PeelDepth) - 1f, logSide.xMax, DepthY((float)CraftWoodwork.PeelDepth) + 1f, new Color(0.95f, 0.9f, 0.6f, 0.85f));
        for (float x = logSide.xMin; x < logSide.xMax; x += 10f) c.Line(x, DepthY(gouge), x + 5f, DepthY(gouge), new Color(1f, 0.45f, 0.35f, 0.6f), 1f);
        if (p.wood.gouged > 0.05)
            for (int g = 0; g < Mathf.RoundToInt((float)p.wood.gouged * 20f); g++)
            {
                float gx = logSide.xMin + (g * 53 % 97) / 97f * logSide.width;
                c.Line(gx, logSide.yMax - 4, gx + 18, logSide.yMax - 8, new Color(0.15f, 0.1f, 0.06f), 2f);
            }
        // DEPTH: where the edge skims, peels and gouges - the workshop's own depths
        float gx0 = 526f, gx1 = 538f;
        c.Rect(gx0 - 1, DepthY(1f) - 1, gx1 + 1, DepthY(0f) + 1, new Color(0f, 0f, 0f, 0.6f));
        c.Rect(gx0, DepthY((float)CraftWoodwork.PeelDepth), gx1, DepthY(0f), IdleBand); // it skims
        c.Rect(gx0, DepthY(gouge), gx1, DepthY((float)CraftWoodwork.PeelDepth), GoodBand); // it peels
        c.Rect(gx0, DepthY(1f), gx1, DepthY(gouge), HarmBand); // it gouges
        CraftArt.Label(c, 516f, DepthY(0f) + 6f, "DEPTH", CraftArt.LabelInk);
        // The end, in section: each face's bark round it, the face turned up at the top.
        Vector2 ec = logEnd.center;
        float er = logEnd.width * 0.45f;
        c.Disc(ec.x, ec.y, er, Color.Lerp(wood, Color.white, 0.15f));
        for (int r = 1; r < 6; r++) c.Ring(ec.x, ec.y, (er - 6) * r / 6f, 1f, wood * 0.8f);
        for (int f = 0; f < CraftWoodwork.LogFaces; f++)
        {
            float mean = 0f;
            for (int i = 0; i < n; i++) mean += FaceBark(p, k, i, f);
            mean /= Mathf.Max(1, n);
            float centre = 90f + (f - k.face) * 360f / CraftWoodwork.LogFaces;
            for (float a = centre - 44f; a <= centre + 44f; a += 4f)
            {
                float rad = a * Mathf.Deg2Rad;
                c.Disc(ec.x + Mathf.Cos(rad) * (er - 3f), ec.y + Mathf.Sin(rad) * (er - 3f), 3.2f, new Color(bark.r, bark.g, bark.b, 0.25f + 0.75f * mean));
            }
        }
        c.Line(ec.x, ec.y + er + 2f, ec.x, ec.y + er + 12f, Mark, 2f);
        CraftArt.Label(c, ec.x - CraftArt.LabelWidth($"FACE {k.face + 1}/{CraftWoodwork.LogFaces}") * 0.5f, logEnd.yMin - 14f, $"FACE {k.face + 1}/{CraftWoodwork.LogFaces}", CraftArt.LabelInk);
        // The drawknife: its edge at the pointer, its depth on the section and the gauge.
        Vector2 h = ui.Hover;
        bool over = h.x >= 0 && h.x >= logSide.xMin - 20 && h.x <= logSide.xMax + 20 && h.y >= logSide.yMin && h.y <= logSide.yMax + 30;
        if (over || knife.Count > 0)
        {
            Vector2 at = knife.Count > 0 ? knife[knife.Count - 1] : h;
            float d = KnifeDepth(at.y);
            Color edge = d < CraftWoodwork.PeelDepth ? CraftArt.LabelDim : d <= gouge ? Good : Harm;
            c.Rect(at.x - 50, at.y - 2, at.x + 50, at.y + 2, new Color(0.75f, 0.76f, 0.8f));
            c.Rect(at.x - 60, at.y - 4, at.x - 50, at.y + 20, Wood);
            c.Rect(at.x + 50, at.y - 4, at.x + 60, at.y + 20, Wood);
            c.Ring(ec.x, ec.y, Mathf.Max(4f, (er - 1) - d * 14f), 1.5f, edge);
            c.Rect(gx0 - 4, DepthY(d) - 1, gx1 + 4, DepthY(d) + 1, Color.white);
        }
        for (int i = 1; i < knife.Count; i++) c.Line(knife[i - 1].x, knife[i - 1].y, knife[i].x, knife[i].y, new Color(1f, 1f, 1f, 0.35f), 2f);
        Gauge(c, "BARK", 50f, 290f, 470f, 8f, (float)CraftWoodwork.BarkLeft(p), bark * 1.6f, 44f);
    }

    private void TurnLog(int dir)
    {
        PieceWork k = Current;
        if (k == null) { said = "Pick a log (on the left) to put it on the horse."; return; }
        k.face = (k.face + dir + CraftWoodwork.LogFaces) % CraftWoodwork.LogFaces;
        knife.Clear();
        said = $"Turned the log: face {k.face + 1} of {CraftWoodwork.LogFaces} is up.";
    }

    private void DebarkPointer(CraftPointer phase, Vector2 pos, float scroll)
    {
        CraftPiece p = WoodPiece;
        if (phase == CraftPointer.Scroll)
        {
            // the log turns on the horse only with the knife off it: the pull under way is not thrown away
            if (knife.Count > 0) { said = "Finish the pull first: the log turns once the drawknife is off it."; return; }
            TurnLog(scroll < 0 ? -1 : 1);
            return;
        }
        if (phase == CraftPointer.Down)
        {
            if (p == null) { said = "Pick a log (on the left) to put it on the horse."; return; }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            if (pos.x < logSide.xMin - 20 || pos.x > logSide.xMax + 20 || pos.y < logSide.yMin || pos.y > logSide.yMax + 30)
                return;
            if (CraftWoodwork.BarkLeft(p) <= 0.001) { said = "There is no bark left on it: split it, rive it or season it."; ignorePress = true; return; }
            knife.Clear();
            knife.Add(pos + new Vector2(0f, (float)Hands.Tremble(91) * 60f));
            knifeStart = knifeLast = ui.PointerTime;
            return;
        }
        if (knife.Count == 0) return;
        if (phase == CraftPointer.Drag)
        {
            // the edge follows the hand, trembling: the depth wavers as it does
            knife.Add(pos + new Vector2(0f, (float)Hands.Tremble(knife.Count + 91) * 60f));
            knifeLast = ui.PointerTime;
            return;
        }
        if (phase == CraftPointer.Up) Pull(p);
    }

    /// <summary>A pull of the drawknife (right to left along the log) as far as it went: sent to the log it was made on.</summary>
    private void Pull(CraftPiece p)
    {
        var pts = new List<Vector2>(knife);
        knife.Clear();
        if (p == null || p.location != CraftLocation.Air || pts.Count < 2)
            return;
        float minX = float.MaxValue, maxX = float.MinValue, mean = 0f, sq = 0f;
        foreach (Vector2 q in pts) { minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); float d = KnifeDepth(q.y); mean += d; sq += d * d; }
        mean /= pts.Count;
        if (maxX - minX < 20f) return; // a dab, not a pull
        if (pts[0].x <= pts[pts.Count - 1].x) { said = "A drawknife is pulled toward you: stroke from right to left."; return; }
        float sd = Mathf.Sqrt(Mathf.Max(0f, sq / pts.Count - mean * mean));
        float seconds = Mathf.Max(0.05f, knifeLast - knifeStart), speed = (maxX - minX) / seconds;
        float steady = Mathf.Clamp01(1f - sd * 4f) * Mathf.Clamp01(1f - Mathf.Max(0f, speed - 700f) / 900f);
        PieceWork k = Work(p);
        int i0 = Stretch(p, minX), i1 = Stretch(p, maxX);
        // a stretch pulled twice on every face with bark still on it (tight, dry bark): another round starts afresh
        for (int i = i0; i <= i1; i++)
        {
            bool round = true;
            for (int f = 0; f < CraftWoodwork.LogFaces && round; f++) round = k.faceStrokes.TryGetValue(Key(i, f), out int s) && s >= 2;
            if (round && p.segments[i].bark > 0.05)
                for (int f = 0; f < CraftWoodwork.LogFaces; f++) k.faceStrokes.Remove(Key(i, f));
        }
        int fresh = 0;
        for (int i = i0; i <= i1; i++)
        {
            long key = Key(i, k.face);
            k.faceStrokes.TryGetValue(key, out int done);
            if (done < 2 && p.segments[i].bark > 0.05) fresh++;
            k.faceStrokes[key] = done + 1;
        }
        Hands.Move(0.3);
        if (fresh == 0) { said = "That face is bare already: turn the log (the wheel, or Turn the log)."; return; }
        float round01 = (float)CraftWoodwork.DebarkPerStroke * fresh / Mathf.Max(1, i1 - i0 + 1);
        float gouge = (float)CraftWoodwork.GougeDepth(p);
        bool green = p.wood.moisture > 0.3;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.DebarkWood, id) { i0 = i0, i1 = i1, a = mean, b = steady, c = round01 }, (ok, why) =>
        {
            if (!ok) return;
            said = mean > gouge ? "Too deep: the knife digs into the wood under the bark (red)."
                : mean < CraftWoodwork.PeelDepth ? "Too shallow: only the outer bark comes off - set the edge on the bright line (green)."
                : steady < 0.5f ? "The edge wavered: it skipped and dug in by turns."
                : green ? "The bark peels off in a long strip." : "The tight, dry bark comes off in chips.";
            CraftPiece q = Ws.Piece(id);
            if (q != null && CraftWoodwork.BarkLeft(q) < 0.05) ui.Message("The last of the bark comes away: split it, rive it or season it.");
        });
    }

    // ================================================================== splitting
    private readonly CraftHammer maul = new CraftHammer { fullCharge = 0.9f, weight = 1.4f };
    private bool lining;
    private Vector2 lineA, lineB;
    // The end as it is drawn: its shape (0 round, 1 a half, 2 a quarter, 3 a billet), its box, the heart (pith) and the radius.
    private int sectionShape;
    private Rect sectionBox;
    private Vector2 pith;
    private float pithRadius;

    private static int Count(CraftPiece p, CraftOpKind kind)
    {
        int n = 0;
        foreach (CraftRecordEntry e in p.record) if (e.kind == kind) n += Math.Max(1, e.count);
        return n;
    }

    /// <summary>The end of the piece as it really is, standing on the block: a round log, a half, a quarter, or a billet.</summary>
    private void Section(CraftPiece p)
    {
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        w /= p.segments.Count;
        t /= p.segments.Count;
        float k = Mathf.Min(2.4f, 250f / Mathf.Max(1f, (float)Math.Max(w, t)));
        float W = (float)w * k, T = (float)t * k;
        int splits = Count(p, CraftOpKind.SplitLog), rives = Count(p, CraftOpKind.Rive);
        float ratio = T / Mathf.Max(1f, W);
        sectionShape = p.wood.log ? 0 : rives == 0 && splits == 1 && ratio > 1.7f && ratio < 2.3f ? 1 : rives == 0 && splits == 2 && ratio > 0.8f && ratio < 1.25f ? 2 : 3;
        if (sectionShape == 0) W = T = Mathf.Max(W, T);
        sectionBox = new Rect(360f - W * 0.5f, 72f, W, T);
        switch (sectionShape)
        {
            case 0: pith = sectionBox.center; pithRadius = W * 0.5f; break;
            case 1: pith = new Vector2(sectionBox.xMin, sectionBox.center.y); pithRadius = T * 0.5f; break;
            case 2: pith = new Vector2(sectionBox.xMin, sectionBox.yMin); pithRadius = Mathf.Max(W, T); break;
            default: pith = new Vector2(sectionBox.xMin - W * 0.6f, sectionBox.yMin - T * 0.5f); pithRadius = 0f; break;
        }
    }

    private bool InSection(Vector2 q)
    {
        switch (sectionShape)
        {
            case 0: return (q - pith).sqrMagnitude <= pithRadius * pithRadius;
            case 1: return q.x >= pith.x && (q - pith).sqrMagnitude <= pithRadius * pithRadius;
            case 2: return q.x >= pith.x && q.y >= pith.y && (q - pith).sqrMagnitude <= pithRadius * pithRadius;
            default: return sectionBox.Contains(q);
        }
    }

    private static float Ray(int seed, int k) => (k * 30f + (seed % 30)) * Mathf.Deg2Rad;

    /// <summary>Why the wedges do not take it, with the places that do: the froe, and for a board too thin for a wedge the rip saw.</summary>
    private string NotForTheWedges(CraftPiece p, string why) =>
        $"{why.TrimEnd('.')} ({(CraftWoodwork.CrossSize(p) >= CraftWoodwork.MinSplitWidth && Here(Place.Saw) ? "Rive, or Saw: A taper" : "Rive")}).";

    /// <summary>
    /// How a wedge line from <paramref name="a"/> to <paramref name="b"/> parts the end: <paramref name="off"/> = how unevenly
    /// it divides it (0 in two equal halves - 1: the smaller part at 12 %; the share the workshop gives each half), and
    /// <paramref name="along"/> = how true it runs to a ray of the wood or its check (0 across - 1 along). The rays run out
    /// from the heart: on an end that shows it (a log, a half, a quarter) a line that misses the heart crosses them
    /// whatever its angle - <paramref name="heart"/> (1 through it - 0 well off it) counts in. False when it does not
    /// cross the end.
    /// </summary>
    private bool LineQuality(CraftPiece p, Vector2 a, Vector2 b, out float off, out float along, out float heart)
    {
        off = 1f;
        along = 0f;
        heart = 1f;
        Vector2 d = b - a;
        if (d.sqrMagnitude < 4f) return false;
        int one = 0, other = 0;
        const int grid = 28;
        for (int ix = 0; ix < grid; ix++)
            for (int iy = 0; iy < grid; iy++)
            {
                var q = new Vector2(sectionBox.xMin + (ix + 0.5f) / grid * sectionBox.width, sectionBox.yMin + (iy + 0.5f) / grid * sectionBox.height);
                if (!InSection(q)) continue;
                float cross = d.x * (q.y - a.y) - d.y * (q.x - a.x);
                if (cross >= 0f) one++; else other++;
            }
        int all = one + other;
        if (all == 0 || one == 0 || other == 0) return false;
        float smaller = Mathf.Min(one, other) / (float)all;
        off = Mathf.Clamp01((0.5f - smaller) / (float)CraftWoodwork.SplitShare);
        int seed = CraftMath.Hash(p.id ?? "");
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, best = 999f;
        for (int k = 0; k < 12; k++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(angle, Ray(seed, k) * Mathf.Rad2Deg));
            best = Mathf.Min(best, Mathf.Min(diff, 180f - diff));
        }
        if (sectionShape <= 2)
        {
            // how far the line passes from the heart: within a few pixels it goes through it
            float miss = Mathf.Abs(d.x * (pith.y - a.y) - d.y * (pith.x - a.x)) / d.magnitude;
            heart = Mathf.Clamp01(1f - (miss - 6f) / 24f);
        }
        along = Mathf.Clamp01(1f - best / 15f) * (1f - off * 0.8f) * heart;
        if (p.wood.log)
        {
            float checkDiff = Mathf.Abs(Mathf.DeltaAngle(angle, Ray(seed, seed & 7) * Mathf.Rad2Deg));
            if (Mathf.Min(checkDiff, 180f - checkDiff) < 6f && off < CraftWoodwork.SplitEven && heart > 0.75f) along = Mathf.Max(along, 0.95f);
        }
        return true;
    }

    /// <summary>Draws a ray of the wood from the heart outward, only where it is inside the end.</summary>
    private void DrawRay(CraftPixels c, float angle, Color col, float thick)
    {
        float reach = sectionShape == 3 ? Vector2.Distance(pith, new Vector2(sectionBox.xMax, sectionBox.yMax)) : pithRadius - 4f;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 last = pith;
        bool inside = InSection(last);
        for (float r = 3f; r <= reach; r += 3f)
        {
            Vector2 q = pith + dir * r;
            bool now = InSection(q);
            if (now && inside) c.Line(last.x, last.y, q.x, q.y, col, thick);
            last = q;
            inside = now;
        }
    }

    private void DrawSplit(CraftPixels c)
    {
        c.Rect(170, 20, 550, 70, new Color(0.32f, 0.22f, 0.14f)); // the block
        c.Rect(170, 66, 550, 70, new Color(0.42f, 0.3f, 0.2f));
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air)
            return;
        PieceWork k = Work(p);
        Section(p);
        Color wood = WoodColour(p), bark = Bark(wood);
        int seed = CraftMath.Hash(p.id ?? "");
        Color face = Color.Lerp(wood, Color.white, 0.12f), rings = wood * 0.82f;
        rings.a = 1f;
        switch (sectionShape)
        {
            case 0:
                c.Disc(pith.x, pith.y, pithRadius, CraftWoodwork.BarkLeft(p) > 0.3 ? bark : wood * 0.85f);
                c.Disc(pith.x, pith.y, pithRadius - 5, face);
                for (int r = 1; r < 9; r++) c.Ring(pith.x, pith.y, (pithRadius - 5) * r / 9f, 1f, rings);
                break;
            default:
                // a half, a quarter or a billet: the part of the round it is, the growth rings round the heart where it lies
                var pts = new List<Vector2>();
                if (sectionShape == 1) { for (float a = -90f; a <= 90f; a += 6f) pts.Add(pith + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * pithRadius); }
                else if (sectionShape == 2) { pts.Add(pith); for (float a = 0f; a <= 90f; a += 5f) pts.Add(pith + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * pithRadius); }
                else { pts.Add(new Vector2(sectionBox.xMin, sectionBox.yMin)); pts.Add(new Vector2(sectionBox.xMax, sectionBox.yMin)); pts.Add(new Vector2(sectionBox.xMax, sectionBox.yMax)); pts.Add(new Vector2(sectionBox.xMin, sectionBox.yMax)); }
                c.Polygon(pts, face);
                float far = Vector2.Distance(pith, new Vector2(sectionBox.xMax, sectionBox.yMax));
                for (float r = 10f; r < far; r += 12f)
                    for (float a = 0f; a < 360f; a += 4f)
                    {
                        var q = pith + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                        if (InSection(q)) c.Rect(q.x - 0.5f, q.y - 0.5f, q.x + 0.5f, q.y + 0.5f, rings);
                    }
                c.Outline(pts, new Color(0.1f, 0.07f, 0.05f, 0.8f), 1f);
                break;
        }
        for (int r = 0; r < 12; r++) DrawRay(c, Ray(seed, r), new Color(wood.r * 0.9f, wood.g * 0.85f, wood.b * 0.8f, 0.7f), 1f);
        if (p.wood.log) DrawRay(c, Ray(seed, seed & 7), new Color(0.08f, 0.05f, 0.03f), 2f); // the check
        if (InSection(pith) || sectionShape == 1 || sectionShape == 2) c.Disc(pith.x, pith.y, 3f, new Color(0.25f, 0.15f, 0.08f)); // the heart
        // The wedge's line, the crack opening along it as the maul drives it.
        if (lining) c.Line(lineA.x, lineA.y, lineB.x, lineB.y, new Color(1f, 1f, 1f, 0.6f), 1.5f);
        if (k.wedgeSet)
        {
            c.Line(k.lineA.x, k.lineA.y, k.lineB.x, k.lineB.y, Mark, 2f);
            Vector2 mid = (k.lineA + k.lineB) * 0.5f, d = (k.lineB - k.lineA).normalized;
            float open = Mathf.Clamp01(k.splitWork) * Vector2.Distance(k.lineA, k.lineB) * 0.5f;
            c.Line(mid.x - d.x * open, mid.y - d.y * open, mid.x + d.x * open, mid.y + d.y * open, new Color(0.05f, 0.03f, 0.02f), 3f);
            c.Rect(mid.x - 6, mid.y - 6, mid.x + 6, mid.y + 14, Iron); // the wedge
            Gauge(c, "SPLIT", 24f, 372f, 300f, 8f, Mathf.Clamp01(k.splitWork), new Color(0.6f, 0.85f, 1f), 50f);
            if (maul.Raised) Gauge(c, "MAUL", 24f, 356f, 300f, 6f, Raised(maul), new Color(1f, 0.8f, 0.4f), 50f, (0.75f, 1f, GoodBand));
        }
        Vector2 h = ui.Hover;
        if (k.wedgeSet && maul.Raised && h.x >= 0f)
        {
            // the maul's head over the wedge where the hand is
            c.Rect(h.x - 16, h.y + 10, h.x + 16, h.y + 28, Iron);
            c.Rect(h.x - 3, h.y + 28, h.x + 3, Mathf.Min(398f, h.y + 70), Wood);
        }
    }

    private void SplitPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        PieceWork k = Current;
        if (phase == CraftPointer.Down)
        {
            if (p == null) { said = "Pick a log or a large half (on the left) to stand it on the block."; return; }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            if (!k.wedgeSet)
            {
                if (!sectionBox.Contains(pos) && Vector2.Distance(pos, sectionBox.center) > Mathf.Max(sectionBox.width, sectionBox.height) * 0.5f + 14f)
                    return;
                if (!CraftWoodwork.SplitsWithWedges(p, out string why)) { said = NotForTheWedges(p, why); ignorePress = true; return; }
                lining = true;
                lineA = lineB = pos;
                return;
            }
            if (k.splitSent) { said = "Waiting for it to part..."; ignorePress = true; return; }
            maul.Raise();
            return;
        }
        if (lining)
        {
            if (phase == CraftPointer.Drag) { lineB = pos; return; }
            if (phase != CraftPointer.Up) return;
            lining = false;
            lineB = pos;
            if (!LineQuality(p, lineA, lineB, out float off, out float along, out float heart) || Vector2.Distance(lineA, lineB) < Mathf.Min(sectionBox.width, sectionBox.height) * 0.5f)
            {
                said = "Draw the line right across the end.";
                return;
            }
            // the wedges part the larger side: a line along it (a slab) is not a split they make
            if (!CraftWoodwork.WedgeLineAcross(p, lineB.x - lineA.x, lineB.y - lineA.y, out string across))
            {
                said = across;
                return;
            }
            k.wedgeSet = true;
            k.lineA = lineA;
            k.lineB = lineB;
            k.splitWork = k.splitSolid = 0f;
            k.splitBlows = 0;
            float trueTo = (float)CraftWoodwork.SplitAlongOf(along, CraftWoodwork.Cleave(Rules, p));
            float share = (float)CraftWoodwork.SplitShareOf(off);
            said = (off < CraftWoodwork.SplitEven && trueTo > CraftWoodwork.SplitAlong ? "The wedge is set through the heart, along a ray: even, straight halves."
                    : off >= CraftWoodwork.SplitEven ? $"The wedge is set off the heart: the halves will be about {share * 100:0} and {(1 - share) * 100:0} %."
                    : heart < 0.5f ? (trueTo < CraftWoodwork.TwistsOut ? "The line misses the heart: it runs across the rays, and the split will twist out." : "The line passes off the heart: the split may wander.")
                    : trueTo < CraftWoodwork.TwistsOut ? "Set across the rays: it will twist out." : "Set a little across the rays: the split may wander.")
                   + " Now the maul: hold, and let go on the wedge.";
            return;
        }
        if (phase != CraftPointer.Up || !maul.Raised || k == null || !k.wedgeSet) return;
        Vector2 mid = (k.lineA + k.lineB) * 0.5f + new Vector2(0f, 10f);
        CraftBlow b = maul.Release(0.5, 0, Hands);
        float square = Mathf.Clamp01(1f - Vector2.Distance(pos, mid) / 30f) * (b.slipped ? 0.2f : 1f);
        float tough = (float)(Rules.Wood(p.wood.wood)?.toughness ?? 0.6);
        float cleave = (float)CraftWoodwork.Cleave(Rules, p);
        float size = (float)CraftWoodwork.CrossSize(p);
        // a heavier, tougher piece takes more; interlocked grain fights the wedge
        k.splitWork += (float)b.force * square * 0.5f / (0.4f + tough) / Mathf.Max(0.6f, size / 200f) * (0.6f + 0.4f * cleave);
        k.splitSolid += (float)b.force * square;
        k.splitBlows++;
        said = b.slipped ? b.Words : square < 0.5f ? "The maul glances off the wedge's edge." : b.force < 0.5f ? "A light blow: the wedge hardly moves." : "The wedge sinks in; the crack runs.";
        if (k.splitWork < 1f) return;
        LineQuality(p, k.lineA, k.lineB, out float o, out float al, out _);
        float solid = Mathf.Clamp01(k.splitSolid / k.splitBlows);
        string id = p.id;
        k.splitSent = true;
        Send(new CraftRequest(CraftRequestKind.SplitLog, id) { a = o, b = al, c = solid, i0 = k.splitBlows }, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer) { k.splitSent = false; return; } // refused: the wedge is still in, another blow sends it again
            works.Remove(id);
            if (ok) ui.Message(CraftText.Capital(LastRecord(id)) + ".");
        });
    }

    private void LiftWedge()
    {
        PieceWork k = Current;
        if (k == null || !k.wedgeSet) { said = "There is no wedge in it."; return; }
        if (k.splitWork > 0.05f && !Confirm("wedge", "Click again to knock the wedge out: the split so far closes up.")) return;
        k.wedgeSet = false;
        k.splitWork = k.splitSolid = 0f;
        k.splitBlows = 0;
        maul.Lower();
        said = "The wedge is out: draw its line again.";
    }

    // ================================================================== riving
    private readonly CraftHammer mallet = new CraftHammer { fullCharge = 0.6f };
    private Rect billet;
    private bool levering;
    private Vector2 leverLast;
    private float leverT, leverEma;

    /// <summary>The dimension the froe parts (mm): the larger across.</summary>
    private static float RiveDepth(CraftPiece p)
    {
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        return (float)(Math.Max(w, t) / Math.Max(1, p.segments.Count));
    }

    private void DrawRive(CraftPixels c)
    {
        c.Rect(30, 80, 690, 130, new Color(0.32f, 0.22f, 0.14f)); // the block it lies on
        c.Rect(30, 126, 690, 130, new Color(0.42f, 0.3f, 0.2f));
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air)
            return;
        PieceWork k = Work(p);
        float h = Mathf.Clamp(RiveDepth(p) * 2.2f, 24f, 170f);
        billet = new Rect(60, 132, 600, h);
        DrawBoard(c, p, billet, true, k.riveStage > 0 ? k.riveLine : -1);
        for (int i = 1; i < k.splitPath.Count; i++)
            c.Line(k.splitPath[i - 1].x, k.splitPath[i - 1].y, k.splitPath[i].x, k.splitPath[i].y, new Color(0.05f, 0.03f, 0.02f), 3f);
        if (k.riveStage >= 1 && k.riveLine >= 0)
        {
            Vector2 f = k.splitPath.Count > 0 ? k.splitPath[k.splitPath.Count - 1] : new Vector2(billet.xMin, GrainY(p, billet, k.riveLine, 7, billet.xMin));
            c.Rect(f.x - 3, f.y - 24, f.x + 3, f.y + 24, Iron); // the froe's blade
            c.Rect(f.x - 4, f.y + 24, f.x + 4, Mathf.Min(396f, f.y + 80), Wood); // its handle, up
            if (k.riveStage == 1)
            {
                Gauge(c, "BLOWS", 30f, 50f, 260f, 8f, k.malletBlows / 2f, Mark, 58f);
                if (mallet.Raised) Gauge(c, "MALLET", 30f, 34f, 260f, 6f, Raised(mallet), new Color(1f, 0.8f, 0.4f), 58f, (0.5f, 1f, GoodBand));
            }
            else
            {
                Gauge(c, "SPLIT", 30f, 50f, 660f, 8f, (k.riveFront - billet.xMin) / billet.width, new Color(0.6f, 0.85f, 1f), 58f);
                Gauge(c, "PACE", 30f, 34f, 300f, 6f, Mathf.Clamp01(leverEma / 900f), Mathf.Clamp01((leverEma - 450f) / 600f) > 0f ? Harm : Good, 58f, (0f, 0.5f, GoodBand), (0.5f, 1f, HarmBand));
            }
        }
        Vector2 hv = ui.Hover;
        if (k.riveStage == 0 && hv.x >= 0 && Mathf.Abs(hv.x - billet.xMin) < 40f && hv.y > billet.yMin - 10 && hv.y < billet.yMax + 10)
        {
            int line = NearestLine(p, hv.y, billet.xMin);
            c.Ring(billet.xMin, GrainY(p, billet, line, 7, billet.xMin), 8f, 2f, Mark);
        }
    }

    private int NearestLine(CraftPiece p, float y, float x)
    {
        int best = 0;
        float d = float.MaxValue;
        for (int k = 0; k < 7; k++)
        {
            float gy = GrainY(p, billet, k, 7, x);
            if (Mathf.Abs(gy - y) < d) { d = Mathf.Abs(gy - y); best = k; }
        }
        return best;
    }

    private void RivePointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        PieceWork k = Current;
        if (phase == CraftPointer.Down && (p == null || p.location != CraftLocation.Air))
        {
            said = p == null ? "Pick a billet (on the left) to lay it on the block." : OutOfPlace(p);
            return;
        }
        if (p == null || k == null) return;
        switch (k.riveStage)
        {
            case 0:
                // Set the froe on a grain line at the end of the billet.
                if (phase != CraftPointer.Down || Mathf.Abs(pos.x - billet.xMin) >= 40f || pos.y < billet.yMin - 10 || pos.y > billet.yMax + 10)
                    return;
                ignorePress = true;
                if (!CraftWoodwork.RivesWithFroe(p, out string why)) { said = why.StartsWith("Split") ? $"{why.TrimEnd('.')} (Split log)." : why; return; }
                k.riveLine = NearestLine(p, pos.y, billet.xMin);
                k.riveStage = 1;
                k.malletBlows = 0;
                float share = (k.riveLine + 0.5f) / 7f;
                said = (Mathf.Abs(share - 0.5f) < 0.1f ? "The froe is set on the middle line." : $"The froe is set off the middle: the halves will be about {share * 100:0} and {(1 - share) * 100:0} %, and the split runs out more.")
                       + " Drive it in with the mallet: hold, and let go - two good blows.";
                return;
            case 1:
                if (phase == CraftPointer.Down) mallet.Raise();
                else if (phase == CraftPointer.Up && mallet.Raised)
                {
                    CraftBlow b = mallet.Release(0.5, 0, Hands);
                    if (b.force > 0.5 && !b.slipped) k.malletBlows++;
                    said = b.slipped ? b.Words : b.force > 0.5 ? "The froe bites into the end grain." : "Too light a blow.";
                    if (k.malletBlows >= 2)
                    {
                        k.riveStage = 2;
                        k.riveFront = billet.xMin;
                        k.riveStray = k.riveWeight = k.riveJerk = 0f;
                        k.splitPath.Clear();
                        k.splitPath.Add(new Vector2(billet.xMin, GrainY(p, billet, k.riveLine, 7, billet.xMin)));
                        said = "It is in. Now lever the split along from its end: hold and move right, keeping on the highlighted grain line, at an even pace.";
                    }
                }
                return;
            default:
                if (k.riveSent) return;
                if (phase == CraftPointer.Down)
                {
                    float fy = k.splitPath.Count > 0 ? k.splitPath[k.splitPath.Count - 1].y : GrainY(p, billet, k.riveLine, 7, k.riveFront);
                    if (Mathf.Abs(pos.x - k.riveFront) > 25f || Mathf.Abs(pos.y - fy) > 40f)
                    {
                        said = "Lever from the end of the split, where the froe is.";
                        ignorePress = true;
                        return;
                    }
                    levering = true;
                    leverLast = pos;
                    leverT = ui.PointerTime;
                    leverEma = 0f;
                    return;
                }
                if (phase == CraftPointer.Up) { levering = false; return; }
                if (phase != CraftPointer.Drag || !levering) return;
                Lever(p, k, pos);
                return;
        }
    }

    /// <summary>
    /// The split levered on to the hand at <paramref name="pos"/>: the way from the last sample is followed in 4 px steps
    /// (a jump is judged all along it, against the grain under it), at the hand's pace smoothed over a fifth of a second.
    /// </summary>
    private void Lever(CraftPiece p, PieceWork k, Vector2 pos)
    {
        float now = ui.PointerTime, dt = Mathf.Max(1e-3f, now - leverT);
        float x1 = Mathf.Min(pos.x, billet.xMax);
        if (x1 > k.riveFront)
        {
            float dx = x1 - k.riveFront;
            float before = leverEma;
            leverEma += (dx / dt - leverEma) * (1f - Mathf.Exp(-dt / 0.2f));
            float rushed = Mathf.Clamp01((leverEma - 450f) / 600f) * 0.5f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dx / 4f));
            float span = pos.x - leverLast.x;
            for (int s = 1; s <= steps; s++)
            {
                float sx = k.riveFront + dx * s / steps;
                float along = Mathf.Abs(span) > 1e-3f ? Mathf.Clamp01((sx - leverLast.x) / span) : 1f;
                float y = Mathf.Lerp(leverLast.y, pos.y, along) + (float)Hands.Tremble(k.splitPath.Count + 61) * 80f;
                float gy = GrainY(p, billet, k.riveLine, 7, sx);
                // off the line the split cuts the fibres; where the line runs out of the side, the split runs out with it
                float offLine = Mathf.Clamp01(Mathf.Abs(y - gy) / 28f);
                float outside = gy < billet.yMin + 3f || gy > billet.yMax - 3f ? 0.7f : 0f;
                k.riveStray += Mathf.Max(offLine, Mathf.Max(outside, rushed)) * (dx / steps);
                k.riveWeight += dx / steps;
                k.splitPath.Add(new Vector2(sx, Mathf.Lerp(gy, y, offLine)));
            }
            k.riveJerk += Mathf.Abs(leverEma - before) * 0.0008f * dx; // a stop and a start, a lurch: the pace jumping
            k.riveFront = x1;
        }
        leverLast = pos;
        leverT = now;
        if (k.riveFront < billet.xMax - 1f) return;
        levering = false;
        float stray = k.riveWeight > 0 ? k.riveStray / k.riveWeight : 1f;
        float steady = Mathf.Clamp01(1f - k.riveJerk / Mathf.Max(1f, k.riveWeight) * 20f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 4f);
        Hands.Move(0.5);
        string id = p.id;
        k.riveSent = true;
        Send(new CraftRequest(CraftRequestKind.Rive, id) { a = stray, b = steady, c = (k.riveLine + 0.5f) / 7f }, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer) { k.riveSent = false; return; }
            works.Remove(id);
            if (ok) ui.Message(CraftText.Capital(LastRecord(id)) + (steady < 0.5f ? ": the jerky levering bruised its faces." : "."));
        });
    }

    private void LiftFroe()
    {
        PieceWork k = Current;
        if (k == null || k.riveStage == 0) { said = "The froe is not set."; return; }
        if ((k.riveStage == 2 || k.malletBlows > 0) && !Confirm("froe", "Click again to pull the froe out: the split so far closes up.")) return;
        k.riveStage = 0;
        k.riveLine = -1;
        k.malletBlows = 0;
        k.splitPath.Clear();
        mallet.Lower();
        levering = false;
        said = "The froe is out.";
    }

    // ================================================================== sawing
    private float boardX0 = 60f, boardLen = 600f, boardCY = 210f, xs = 1f, ys = 1f;
    private bool sawing;
    private float pressX, pressOff;
    /// <summary>The rip saw was set on the taper's lower line (the drawing mirrors it onto the upper one).</summary>
    private bool ripLower;

    /// <summary>The board laid on the sawhorses: its length over 600 px, its width (to its own scale) about the middle.</summary>
    private void BoardGeometry(CraftPiece p)
    {
        double maxW = 1;
        foreach (MetalSegment s in p.segments) maxW = Math.Max(maxW, s.width);
        xs = Mathf.Max(0.05f, (float)p.Length / boardLen);
        ys = Mathf.Max(0.25f, (float)maxW / 150f);
    }

    private float XAt(float t) => boardX0 + t * boardLen;
    private float TAt(float x) => Mathf.Clamp01((x - boardX0) / boardLen);
    private float HalfAt(CraftPiece p, float x) => (float)p.segments[Mathf.Clamp(p.SegmentAt(TAt(x)), 0, p.segments.Count - 1)].width * 0.5f / ys;

    /// <summary>The upper line of a marked taper at x (pixels), and its ends.</summary>
    private float RipY(PieceWork k, float x) => boardCY + (float)CraftWoodwork.RipLineAt(k.ripA, k.ripWa, k.ripB, k.ripWb, TAt(x)) * 0.5f / ys;

    private void DrawPlank(CraftPixels c, CraftPiece p)
    {
        Color wood = WoodColour(p);
        double pos = 0;
        foreach (MetalSegment s in p.segments)
        {
            float x0 = boardX0 + (float)(pos / xs), x1 = boardX0 + (float)((pos + s.length) / xs);
            float hw = (float)s.width * 0.5f / ys;
            c.Rect(x0, boardCY - hw, x1 + 0.6f, boardCY + hw, wood);
            for (int g = -2; g <= 2; g++)
                c.Line(x0, boardCY + g * hw * 0.35f, x1, boardCY + g * hw * 0.35f + Mathf.Sin((float)pos * 0.01f + g) * 1.2f, new Color(0f, 0f, 0f, 0.12f), 1f);
            if (s.rough > 50) c.Disc(x0 + 4, boardCY + hw - 4, 1.5f, new Color(0.15f, 0.1f, 0.06f, 0.6f));
            pos += s.length;
        }
    }

    private void DrawSaw(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        // Two sawhorses.
        c.Rect(116, 30, 136, 120, Wood);
        c.Rect(584, 30, 604, 120, Wood);
        c.Rect(100, 116, 152, 124, Wood);
        c.Rect(568, 116, 620, 124, Wood);
        if (p == null || p.location != CraftLocation.Air)
            return;
        PieceWork k = Work(p);
        BoardGeometry(p);
        DrawPlank(c, p);
        double widest = 1;
        foreach (MetalSegment s in p.segments) widest = Math.Max(widest, s.width);
        float top = boardCY + (float)widest * 0.5f / ys; // the board's far edge as drawn: the lengths are written above it
        if (!ripMode && k.mark >= 0f)
        {
            float mx = XAt(k.mark);
            float hw = HalfAt(p, mx);
            for (float y = boardCY - hw - 8; y < boardCY + hw + 8; y += 6) c.Line(mx, y, mx, y + 3, new Color(0.1f, 0.1f, 0.12f), 1.5f);
            string near = $"{k.mark * p.Length:0} MM", far = $"{(1 - k.mark) * p.Length:0} MM";
            float nearX = Mathf.Max(boardX0, mx - 10 - CraftArt.LabelWidth(near)), farX = Mathf.Min(boardX0 + boardLen - CraftArt.LabelWidth(far), mx + 10);
            Apart(nearX, CraftArt.LabelWidth(near), farX, CraftArt.LabelWidth(far), top + 10, out float nearY, out float farY);
            CraftArt.Label(c, nearX, nearY, near, CraftArt.LabelInk);
            CraftArt.Label(c, farX, farY, far, CraftArt.LabelInk);
            if (k.sawStarted && !k.sawRip)
            {
                float bx = mx + (float)k.saw.Blade;
                float depth = (float)k.saw.Progress * hw * 2f;
                c.Rect(bx - 1.5f, boardCY + hw - depth, bx + 1.5f, boardCY + hw, new Color(0.05f, 0.04f, 0.03f));
                Gauge(c, "SAW", 60f, 372f, 600f, 8f, (float)k.saw.Progress, new Color(0.6f, 0.85f, 1f), 40f);
                for (int s = 0; s < Mathf.Min(30, k.saw.Strokes * 2); s++)
                    c.Disc(bx + (s * 7 % 11 - 5) * 3f, boardCY - hw - 6 - (s * 5 % 13), 1.2f, Color.Lerp(WoodColour(p), Color.white, 0.3f));
            }
        }
        if (ripMode)
        {
            Color line = new Color(0.1f, 0.1f, 0.12f);
            float waX = 0f, waY = 0f;
            string wa = $"{k.ripWa:0} MM";
            if (k.ripA >= 0f)
            {
                float ax = XAt(k.ripA), ay = boardCY + k.ripWa * 0.5f / ys;
                c.Disc(ax, ay, 3f, Mark);
                c.Disc(ax, boardCY - k.ripWa * 0.5f / ys, 3f, Mark);
                waX = Mathf.Clamp(ax - CraftArt.LabelWidth(wa) * 0.5f, 4f, 716f - CraftArt.LabelWidth(wa));
                waY = Mathf.Max(ay, top) + 10;
                if (k.ripB < 0f) CraftArt.Label(c, waX, waY, wa, CraftArt.LabelInk);
            }
            if (k.ripA >= 0f && k.ripB >= 0f)
            {
                float x0 = XAt(Mathf.Min(k.ripA, k.ripB)), x1 = XAt(Mathf.Max(k.ripA, k.ripB));
                for (float x = x0; x < x1; x += 8f)
                {
                    float xe = Mathf.Min(x + 4f, x1);
                    c.Line(x, RipY(k, x), xe, RipY(k, xe), line, 1.5f);
                    c.Line(x, 2 * boardCY - RipY(k, x), xe, 2 * boardCY - RipY(k, xe), line, 1.5f);
                }
                string wb = $"{k.ripWb:0} MM";
                float bx = XAt(k.ripB), wbX = Mathf.Clamp(bx - CraftArt.LabelWidth(wb) * 0.5f, 4f, 716f - CraftArt.LabelWidth(wb)), wbY = Mathf.Max(RipY(k, bx), top) + 10;
                // the two widths, the left one first, a row apart when they would fall on each other
                bool aFirst = waX <= wbX;
                Apart(aFirst ? waX : wbX, CraftArt.LabelWidth(aFirst ? wa : wb), aFirst ? wbX : waX, CraftArt.LabelWidth(aFirst ? wb : wa), Mathf.Max(waY, wbY), out float y0, out float y1);
                CraftArt.Label(c, waX, aFirst ? y0 : y1, wa, CraftArt.LabelInk);
                CraftArt.Label(c, wbX, aFirst ? y1 : y0, wb, CraftArt.LabelInk);
                if (k.sawStarted && k.sawRip)
                {
                    float dir = k.ripB >= k.ripA ? 1f : -1f, xa = XAt(k.ripA), reach = (float)k.saw.Progress * Mathf.Abs(x1 - x0);
                    for (float x = 0f; x < reach; x += 2f)
                    {
                        float px = xa + dir * x;
                        c.Rect(px - 1f, RipY(k, px) - 1.5f, px + 1f, RipY(k, px) + 1.5f, new Color(0.05f, 0.04f, 0.03f));
                        c.Rect(px - 1f, 2 * boardCY - RipY(k, px) - 1.5f, px + 1f, 2 * boardCY - RipY(k, px) + 1.5f, new Color(0.05f, 0.04f, 0.03f));
                    }
                    Gauge(c, "RIP", 60f, 372f, 600f, 8f, (float)k.saw.Progress, new Color(0.6f, 0.85f, 1f), 40f);
                }
            }
        }
        Vector2 h = ui.Hover;
        if (h.x >= 0 && h.x > boardX0 - 20 && h.x < boardX0 + boardLen + 20)
        {
            if (!ripMode)
            {
                // the saw: its blade upright where the hand is
                c.Rect(h.x - 2, h.y - 40, h.x + 2, h.y + 60, new Color(0.7f, 0.72f, 0.76f));
                c.Rect(h.x - 8, h.y + 60, h.x + 8, h.y + 90, Wood);
            }
            else
            {
                // a rip saw lies along the line
                c.Rect(h.x - 60, h.y - 2, h.x + 40, h.y + 2, new Color(0.7f, 0.72f, 0.76f));
                c.Rect(h.x + 40, h.y - 8, h.x + 70, h.y + 8, Wood);
            }
        }
    }

    /// <summary>
    /// Two labels on one row, the first (at <paramref name="x0"/>, <paramref name="w0"/> wide) left of the second: where
    /// they would fall on each other, the shorter goes a row up.
    /// </summary>
    private static void Apart(float x0, float w0, float x1, float w1, float y, out float y0, out float y1)
    {
        y0 = y1 = y;
        if (x0 + w0 + 6f <= x1) return;
        if (w0 <= w1) y0 += 12f; else y1 += 12f;
    }

    private void SetRip(bool rip)
    {
        if (rip == ripMode) return;
        PieceWork k = Current;
        if (k != null && k.sawStarted && k.saw.Progress > 0 && k.sawRip != rip)
        {
            said = rip ? "Finish the cut begun across the board first (or clear the marks)." : "Finish ripping the taper begun first (or clear the marks).";
            return;
        }
        if (sawing) StopSaw();
        if (k != null && k.sawStarted && k.sawRip != rip) { k.saw = new CraftSawStroke(); k.sawStarted = false; }
        ripMode = rip;
        said = "";
        Show();
    }

    private void ClearMarks()
    {
        PieceWork k = Current;
        if (k == null) return;
        if (k.sawStarted && k.saw.Progress > 0.05 && !Confirm("marks", "Click again to give up the cut begun at the mark.")) return;
        if (sawing) StopSaw();
        k.mark = k.ripA = k.ripB = -1f;
        k.saw = new CraftSawStroke();
        k.sawStarted = false;
        said = ripMode ? "Click where the taper's line starts on the board, then where it ends." : "Click on the board to mark where to cut.";
    }

    private void StopSaw()
    {
        sawing = false;
        PieceWork k = Current;
        k?.saw.Stop();
    }

    /// <summary>Strokes a cut takes: across the section at the mark (a wide board longer than a stick), and along a rip as long as it is (300 mm counts as one cut across).</summary>
    private double SawStrokes(CraftPiece p, PieceWork k)
    {
        if (!ripMode)
        {
            MetalSegment s = p.segments[Mathf.Clamp(p.SegmentAt(k.mark), 0, p.segments.Count - 1)];
            return CraftSawStroke.StrokesFor(s.width, s.thickness);
        }
        double t = 0;
        foreach (MetalSegment s in p.segments) t = Math.Max(t, s.thickness);
        double length = Math.Abs(k.ripB - k.ripA) * p.Length;
        return CraftSawStroke.StrokesFor(t) * Math.Max(1, length / 300);
    }

    private void SawPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        PieceWork k = Current;
        if (phase == CraftPointer.Down)
        {
            if (p == null) { said = "Pick a board (on the left) to lay it on the sawhorses."; return; }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            if (k.sawSent) { said = "Waiting for the cut..."; ignorePress = true; return; }
            if (pos.x < boardX0 - 10 || pos.x > boardX0 + boardLen + 10) return;
            if (ripMode) RipDown(p, k, pos);
            else CrossDown(p, k, pos);
            return;
        }
        if (!sawing) return;
        if (phase == CraftPointer.Up) { StopSaw(); if (k != null && k.saw.Progress < 1) said = $"The saw rests {k.saw.Progress * 100:0}% of the way: press on the line again to go on."; return; }
        if (phase != CraftPointer.Drag || k == null) return;
        if (ripMode) k.saw.Add((ripLower ? 2 * boardCY - pos.y : pos.y) - RipY(k, pos.x) - pressOff, pos.x, ui.PointerTime, Hands);
        else k.saw.Add(pos.x - pressX, pos.y, ui.PointerTime, Hands);
        if (k.saw.Progress >= 1) FinishCut(p, k);
    }

    private void CrossDown(CraftPiece p, PieceWork k, Vector2 pos)
    {
        float hw = HalfAt(p, pos.x);
        if (Mathf.Abs(pos.y - boardCY) > hw + 14f) return;
        if (k.mark >= 0f && Mathf.Abs(pos.x - XAt(k.mark)) <= 12f)
        {
            // the blade is set on the mark: where the hand pressed is only where the stroke starts from
            sawing = true;
            pressX = pos.x;
            if (k.sawStarted) k.saw.Resume();
            else { k.saw.Begin(SawStrokes(p, k)); k.sawStarted = true; k.sawRip = ripMode; }
            return;
        }
        ignorePress = true;
        if (k.sawStarted && k.saw.Progress > 0) { said = "Finish the cut begun at the mark (press on it), or clear the marks."; return; }
        float at = TAt(pos.x);
        if (!CraftWoodwork.CanSawAt(p, at, out string why)) { said = why; return; }
        k.mark = at;
        k.saw = new CraftSawStroke();
        k.sawStarted = false;
        said = $"Marked: {at * p.Length:0} mm and {(1 - at) * p.Length:0} mm. Press on the mark and saw: long strokes up and down across the board, keeping on the line.";
    }

    private void RipDown(CraftPiece p, PieceWork k, Vector2 pos)
    {
        if (k.ripA >= 0f && k.ripB >= 0f)
        {
            float lo = XAt(Mathf.Min(k.ripA, k.ripB)), hi = XAt(Mathf.Max(k.ripA, k.ripB));
            if (pos.x >= lo - 8 && pos.x <= hi + 8 && Mathf.Abs(Mathf.Abs(pos.y - boardCY) - Mathf.Abs(RipY(k, pos.x) - boardCY)) <= 14f)
            {
                sawing = true;
                ripLower = pos.y < boardCY; // the lower edge: the same line, mirrored
                pressOff = (ripLower ? 2 * boardCY - pos.y : pos.y) - RipY(k, pos.x);
                if (k.sawStarted) k.saw.Resume();
                else { k.saw.Begin(SawStrokes(p, k)); k.sawStarted = true; k.sawRip = ripMode; }
                return;
            }
            ignorePress = true;
            said = k.sawStarted ? "Press on the line to go on sawing along it." : "Press on the line to saw along it - or clear the marks to mark another.";
            return;
        }
        ignorePress = true;
        float t = TAt(pos.x), width = Mathf.Max(3f, Mathf.Abs(pos.y - boardCY) * 2f * ys);
        if (k.ripA < 0f)
        {
            k.ripA = t;
            k.ripWa = width;
            said = $"The line starts {t * p.Length:0} mm along, {width:0} mm wide: click where it ends.";
            return;
        }
        if (!CraftWoodwork.RipNarrows(p, k.ripA, t, k.ripWa, width, out string why)) { said = why; return; }
        k.ripB = t;
        k.ripWb = width;
        k.saw = new CraftSawStroke();
        k.sawStarted = false;
        said = $"A taper from {k.ripWa:0} to {k.ripWb:0} mm: press on the line and saw along it - long strokes back and forth, keeping on it. Both edges are ripped to it.";
    }

    private void FinishCut(CraftPiece p, PieceWork k)
    {
        sawing = false;
        k.saw.Stop();
        k.sawSent = true;
        float wander = (float)k.saw.Wander, square = (float)k.saw.Square;
        string id = p.id;
        Hands.Move(0.3);
        CraftRequest r = ripMode
            ? new CraftRequest(CraftRequestKind.RipCut, id) { a = k.ripA, b = k.ripB, c = k.ripWa, d = k.ripWb, i0 = Mathf.RoundToInt(wander * 1000f) }
            : new CraftRequest(CraftRequestKind.SawCut, id) { a = k.mark, b = wander, c = square };
        bool rip = ripMode;
        Send(r, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer) { k.sawSent = false; return; } // still in its kerf: a stroke more sends it again
            works.Remove(id);
            if (!ok) return;
            if (rip) ui.Message(wander < CraftWoodwork.SawSquare ? "Ripped along the line: a clean taper." : wander < 0.5f ? "Ripped - the saw wandered a little: a wavy, rough edge." : "Ripped well off the line: a ragged edge.");
            else ui.Message(wander < CraftWoodwork.SawSquare && square >= 0.8f ? "Through, square and on the line." : wander < 0.5f ? "Through - the cut wandered a little: the end is ragged." : "Through, well off the line: a torn, crooked end.");
        });
    }

    // ================================================================== steaming and bending
    private float FormR => FormRadius[Mathf.Clamp(form != null ? form.value : 1, 0, FormRadius.Length - 1)];
    private float From => bendFrom != null ? bendFrom.value : 0.5f;
    private Rect steamBox;
    private Vector2 formC, clampAt, tip;
    private float bendScale = 1f, formPx = 40f, thickPx = 6f;
    private readonly List<Vector2> freeLine = new List<Vector2>(), heldLine = new List<Vector2>();
    private bool dragging, bending;
    private Vector2 dragAt;
    private float sweep0, sweep, sweepEma, sweepPeak, sweepT;

    private string BendFromText(float v)
    {
        CraftPiece p = WoodPiece;
        return p != null ? $"{v * p.Length:0} mm" : $"{v * 100:0}%";
    }

    /// <summary>The bearing (degrees) of a point round the form's centre.</summary>
    private float Bearing(Vector2 q) => Mathf.Atan2(q.y - formC.y, q.x - formC.x) * Mathf.Rad2Deg;

    /// <summary>The most it can be bent: as much of the free end as there is to wrap round the form.</summary>
    private float MaxSweep(CraftPiece p) => Mathf.Min(170f, (float)((1 - From) * p.Length / FormR) * Mathf.Rad2Deg);

    /// <summary>
    /// Lays out the piece clamped on the form at its "bend from" point, to one scale with the form (so it wraps it truly):
    /// the part behind the clamp to the left, the free part to the right wrapped round the form by <paramref name="wrap"/>
    /// degrees and then on along its own curve (the bend it already keeps).
    /// </summary>
    private void LayOut(CraftPiece p, float wrap)
    {
        float L = (float)p.Length, behind = From * L;
        bendScale = Mathf.Max(Mathf.Max(behind / 170f, (L - behind) / 220f), Mathf.Max(FormR / 110f, 0.5f));
        // A piece that already keeps bends can curl out of the room: drawn smaller until all of it is in (right of the steam box, under the gauges).
        for (int tries = 0; ; tries++)
        {
            Trace(p, wrap);
            if (tries == 4) break;
            float fit = 1f;
            foreach (List<Vector2> line in new[] { freeLine, heldLine })
                foreach (Vector2 q in line)
                {
                    if (q.x < 244f) fit = Mathf.Min(fit, (clampAt.x - 244f) / Mathf.Max(1f, clampAt.x - q.x));
                    if (q.x > 712f) fit = Mathf.Min(fit, (712f - clampAt.x) / Mathf.Max(1f, q.x - clampAt.x));
                    if (q.y < 10f) fit = Mathf.Min(fit, (clampAt.y - 10f) / Mathf.Max(1f, clampAt.y - q.y));
                    if (q.y > 332f) fit = Mathf.Min(fit, (332f - clampAt.y) / Mathf.Max(1f, q.y - clampAt.y));
                }
            if (fit >= 1f) break;
            bendScale /= Mathf.Max(0.2f, fit * 0.97f);
        }
    }

    private void Trace(CraftPiece p, float wrap)
    {
        float L = (float)p.Length, behind = From * L, free = L - behind;
        formPx = FormR / bendScale;
        thickPx = Mathf.Clamp((float)CraftWoodwork.Thickness(p) / bendScale, 3f, 14f);
        formC = new Vector2(420f, 230f - formPx * 0.5f);
        clampAt = new Vector2(formC.x, formC.y + formPx + thickPx * 0.5f);
        float rr = formPx + thickPx * 0.5f, wrapMm = wrap * Mathf.Deg2Rad * FormR, step = 3f;
        freeLine.Clear();
        freeLine.Add(clampAt);
        Vector2 at = clampAt;
        float dir = 0f; // degrees: 0 pointing right
        for (float mm = 0f; mm < free; mm += step * bendScale)
        {
            float s = Mathf.Min(step * bendScale, free - mm);
            if (mm < wrapMm)
            {
                // round the form
                float a = (90f - (mm + s) / FormR * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                at = formC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                dir = -(mm + s) / FormR * Mathf.Rad2Deg;
            }
            else
            {
                int i = Mathf.Clamp(p.SegmentAt((behind + mm) / L), 0, p.segments.Count - 1);
                dir -= (float)(p.segments[i].bend / Math.Max(1, p.segments[i].length)) * s;
                at += new Vector2(Mathf.Cos(dir * Mathf.Deg2Rad), Mathf.Sin(dir * Mathf.Deg2Rad)) * (s / bendScale);
            }
            freeLine.Add(at);
        }
        tip = at;
        heldLine.Clear();
        heldLine.Add(clampAt);
        at = clampAt;
        dir = 180f;
        for (float mm = 0f; mm < behind; mm += step * bendScale)
        {
            float s = Mathf.Min(step * bendScale, behind - mm);
            int i = Mathf.Clamp(p.SegmentAt((behind - mm) / L), 0, p.segments.Count - 1);
            dir += (float)(p.segments[i].bend / Math.Max(1, p.segments[i].length)) * s;
            at += new Vector2(Mathf.Cos(dir * Mathf.Deg2Rad), Mathf.Sin(dir * Mathf.Deg2Rad)) * (s / bendScale);
            heldLine.Add(at);
        }
    }

    private static float Near(List<Vector2> line, Vector2 q)
    {
        float best = float.MaxValue;
        for (int i = 1; i < line.Count; i++)
        {
            Vector2 a = line[i - 1], b = line[i], d = b - a;
            float t = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(q - a, d) / d.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(q, a + d * t));
        }
        return best;
    }

    private void DrawBend(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        steamBox = new Rect(20, 150, 210, 110);
        // The steam box, its fire under it, steam leaking from the lid.
        c.Rect(steamBox.xMin, steamBox.yMin, steamBox.xMax, steamBox.yMax, new Color(0.35f, 0.25f, 0.16f));
        c.Rect(steamBox.xMin + 6, steamBox.yMin + 6, steamBox.xMax - 6, steamBox.yMax - 6, new Color(0.12f, 0.1f, 0.09f));
        for (int k = 0; k < 6; k++)
            c.Line(steamBox.xMin + 18 + k * 34, steamBox.yMax, steamBox.xMin + 22 + k * 34 + Mathf.Sin(Time.unscaledTime * 3f + k) * 5f, steamBox.yMax + 22, new Color(1f, 1f, 1f, 0.25f), 3f);
        c.Rect(steamBox.xMin + 60, steamBox.yMin - 18, steamBox.xMax - 60, steamBox.yMin, new Color(1f, 0.5f, 0.15f, 0.7f));
        if (p == null)
            return;
        LayOut(p, bending ? sweep : 0f);
        // The form: a block with its curve, the stop the piece is clamped to.
        c.Disc(formC.x, formC.y, formPx, new Color(0.45f, 0.32f, 0.2f));
        c.Rect(formC.x - formPx - 6, formC.y - formPx - 10, formC.x + formPx + 6, formC.y - formPx - 4, Wood); // the bench under it
        Color wood = WoodColour(p);
        // HEAT, SOAK and SOFT: it heats through (it soaks once it is past the tick), softens through, and sets again out of the steam.
        float heat = Mathf.Clamp01(((float)CraftWoodwork.MeanTemperature(p) - 20f) / 80f), soakAt = ((float)CraftWoodwork.SoakHeat - 20f) / 80f;
        Gauge(c, "HEAT", 20f, 372f, 210f, 8f, heat, new Color(1f, 0.55f, 0.25f), 46f, (0f, soakAt, ColdBand), (soakAt, 1f, GoodBand));
        Gauge(c, "SOAK", 20f, 356f, 210f, 8f, (float)p.wood.steamed, new Color(0.85f, 0.85f, 0.95f), 46f);
        Gauge(c, "SOFT", 20f, 340f, 210f, 8f, (float)CraftWoodwork.Plasticity(p), new Color(1f, 0.6f, 0.3f), 46f);
        if (p.location == CraftLocation.Steam && !dragging)
            c.Rect(steamBox.xMin + 14, steamBox.center.y - thickPx * 0.5f, steamBox.xMax - 14, steamBox.center.y + thickPx * 0.5f, Color.Lerp(wood, new Color(0.3f, 0.2f, 0.12f), 0.3f));
        else if (dragging)
            c.Rect(dragAt.x - 80, dragAt.y - thickPx * 0.5f, dragAt.x + 80, dragAt.y + thickPx * 0.5f, wood);
        else if (p.location == CraftLocation.Air)
        {
            for (int i = 1; i < heldLine.Count; i++) c.Line(heldLine[i - 1].x, heldLine[i - 1].y, heldLine[i].x, heldLine[i].y, wood, thickPx);
            for (int i = 1; i < freeLine.Count; i++) c.Line(freeLine[i - 1].x, freeLine[i - 1].y, freeLine[i].x, freeLine[i].y, wood, thickPx);
            // where the bend starts: the clamp on it there
            c.Rect(clampAt.x - 12f, clampAt.y + thickPx * 0.5f, clampAt.x - 4f, clampAt.y + thickPx * 0.5f + 14f, Iron);
            c.Rect(clampAt.x - 14f, clampAt.y - thickPx * 0.5f - 4f, clampAt.x - 2f, clampAt.y - thickPx * 0.5f, Iron);
            if (strap)
                for (int i = 1; i < freeLine.Count; i++)
                {
                    Vector2 a = freeLine[i - 1], b = freeLine[i], n = new Vector2(-(b - a).y, (b - a).x).normalized * (thickPx * 0.5f + 2f);
                    c.Line(a.x - n.x, a.y - n.y, b.x - n.x, b.y - n.y, Iron, 1.5f);
                }
            c.Disc(tip.x, tip.y, 6f, bending ? Mark : new Color(1f, 1f, 1f, 0.4f));
            // STRAIN: how hard this form strains the outside fibres, against what they take (past the tick, they tear) - at
            // the pace the bend is judged by: the fastest of the pull so far (a yank has strained them, whatever the hand
            // does after it); the pace the hand has now is the thin tick
            float strain = BendStrainNow(p, true);
            Gauge(c, "STRAIN", 270f, 372f, 430f, 8f, strain / 1.5f, strain >= 1f ? Harm : strain >= 0.8f ? Fair : Good, 56f,
                (0f, 0.8f / 1.5f, GoodBand), (0.8f / 1.5f, 1f / 1.5f, FairBand), (1f / 1.5f, 1f, HarmBand));
            if (bending)
            {
                float now = 327f + 372f * Mathf.Clamp01(BendStrainNow(p, false) / 1.5f); // on the meter as Meter lays it out
                c.Rect(now - 0.5f, 368f, now + 0.5f, 384f, new Color(1f, 1f, 1f, 0.55f));
                string deg = $"{sweep:0}°";
                CraftArt.Label(c, Mathf.Clamp(tip.x + 10f, 4f, 716f - CraftArt.LabelWidth(deg)), Mathf.Clamp(tip.y + 6f, 4f, 386f), deg, CraftArt.LabelInk);
            }
        }
    }

    /// <summary>
    /// The strain of a bend over the form (1 = the limit): at the pace it is judged by (<paramref name="judged"/>: the
    /// fastest of the pull so far, as <see cref="Bend"/> sends it), or at the hand's pace now; before a pull, an even one.
    /// </summary>
    private float BendStrainNow(CraftPiece p, bool judged) =>
        (float)CraftWoodwork.BendStrain(Rules, p, FormR, bending ? Mathf.Clamp01((judged ? sweepPeak : sweepEma) / 90f) : 0.1f, strap);

    private void BendPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (phase == CraftPointer.Down)
        {
            if (p == null) { said = "Pick a piece of wood (on the left)."; return; }
            if (p.location == CraftLocation.Steam)
            {
                if (steamBox.Contains(pos)) { dragging = true; dragAt = pos; }
                return;
            }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            if (Vector2.Distance(pos, tip) <= 24f || Near(freeLine, pos) <= 14f)
            {
                bending = true;
                sweep0 = Bearing(pos);
                sweep = sweepEma = sweepPeak = 0f;
                sweepT = ui.PointerTime;
                return;
            }
            if (Near(heldLine, pos) <= 14f) { dragging = true; dragAt = pos; }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (dragging) dragAt = pos;
            if (bending)
            {
                // the free end swept round the form, from where it was taken (clockwise: down round the form)
                float now = ui.PointerTime, dt = Mathf.Max(1e-3f, now - sweepT);
                float next = Mathf.Clamp(Mathf.DeltaAngle(Bearing(pos), sweep0), 0f, MaxSweep(p));
                float rate = Mathf.Max(0f, next - sweep) / dt;
                sweepEma += (rate - sweepEma) * (1f - Mathf.Exp(-dt / 0.3f));
                bool torn = BendStrainNow(p, true) >= 1f;
                sweepPeak = Mathf.Max(sweepPeak, sweepEma);
                sweep = next;
                sweepT = now;
                if (!torn && BendStrainNow(p, true) >= 1f)
                    said = "Too fast: the fibres on the outside are strained past what they take - let go and it cracks. Right-click lets it spring back unbent.";
            }
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (dragging)
        {
            dragging = false;
            if (p.location == CraftLocation.Air && steamBox.Contains(pos))
            {
                Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Steam }, (ok, why) =>
                {
                    if (ok) said = "Into the steam: it heats through first (HEAT), then it soaks (SOAK) - thicker wood takes longer. Then bend it at once.";
                });
            }
            else if (p.location == CraftLocation.Steam && !steamBox.Contains(pos))
            {
                string id = p.id;
                Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) =>
                {
                    CraftPiece q = Ws.Piece(id);
                    if (ok && q != null)
                        said = q.wood.steamed >= 0.99 ? $"Out of the steam, hot and limp: bend it now - it stays soft about {CraftWoodwork.CoolSeconds(q):0} s (SOFT)."
                            : "Out of the steam before it soaked through (SOAK): it is still stiff inside.";
                });
            }
            return;
        }
        if (bending) Bend(p);
    }

    private void Bend(CraftPiece p)
    {
        bending = false;
        if (p == null || sweep < 3f) { sweep = 0f; return; }
        // the hands' tremble moves where the end is let go, drifting a quarter-second at a time
        float angle = Mathf.Clamp(sweep + (float)Hands.Tremble(81 + Mathf.FloorToInt(sweepT * 4f)) * 30f, 1f, MaxSweep(p));
        float speed = Mathf.Clamp01(sweepPeak / 90f);
        double L = p.Length, arc = angle * Mathf.Deg2Rad * FormR;
        int i0 = Mathf.Clamp(p.SegmentAt(From + 1e-4), 0, p.segments.Count - 1);
        int i1 = Mathf.Clamp(p.SegmentAt(Math.Min(1, From + arc / L) - 1e-4), i0, p.segments.Count - 1);
        string id = p.id;
        CraftComponentSpec part = !string.IsNullOrEmpty(p.intentDesign) ? Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        Hands.Move(0.6);
        sweep = 0f;
        Send(new CraftRequest(CraftRequestKind.BendWood, id) { i0 = i0, i1 = i1, a = angle, b = speed, c = FormR, flag = strap }, (ok, why) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(id);
            if (q == null) return;
            double has = 0;
            foreach (MetalSegment s in q.segments) has += s.bend;
            string record = LastRecord(id);
            bool cracked = record.Contains("cracked");
            string verdict = cracked
                ? (CraftWoodwork.Plasticity(q) < 0.3 ? "Crack! It was too cold and stiff: steam it through and bend it at once." : speed > 0.6f ? "Crack! Forced round too fast." : "It creaks and the outside tears: too tight a form for it (or no strap).")
                : "It takes the bend and springs back a little.";
            string want = part != null && part.shape != null ? (Math.Abs(part.shape.curvature) < 1 ? $" The {part.Name.ToLowerInvariant()} should stay straight." : $" The {part.Name.ToLowerInvariant()} wants {part.shape.curvature:0}°.") : "";
            ui.Message($"{verdict} It has {has:0}° now.{want}");
        });
    }

    // ================================================================== drying
    private Rect trestle, stackArea, coverHandle, leftEnd, rightEnd, stripRect;
    private Hold holding;
    private bool coverHeld, coverPending;
    private float coverValue = 0.2f, coverSentAt;

    /// <summary>The cover as the stack has it (or as the hand holds it, until the stack shows it).</summary>
    private float Cover => coverHeld || coverPending ? coverValue : Station != null && Station.Workshop != null && Ws.StackCover.HasValue ? (float)Ws.StackCover.Value : 0.2f;

    /// <summary>How well the stickers are laid (0-1): the workshop's rule - four or more, one under each end, even gaps between.</summary>
    private static float Evenness(List<float> stickers) => (float)CraftWoodwork.StickerEvenness(stickers);

    /// <summary>How well an end is sealed: as the hands have waxed it, and never less than the piece's own seal (the workshop keeps it).</summary>
    private static float Seal(PieceWork k, CraftPiece p, bool left) => Mathf.Max(left ? k.sealLeft : k.sealRight, Mathf.Clamp01((float)p.wood.endsSealed));

    private void DrawDry(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        trestle = new Rect(40, 190, 320, 24);
        stripRect = new Rect(40, 168, 320, 34);
        stackArea = new Rect(420, 70, 280, 216);
        // The shed: the stack's base, its roof, and the cover pulled over it as far as the handle.
        c.Rect(stackArea.xMin - 10, stackArea.yMin - 6, stackArea.xMax + 10, stackArea.yMin, Wood); // the base the stack stands on
        c.Rect(stackArea.xMin - 6, stackArea.yMax + 6, stackArea.xMax + 6, stackArea.yMax + 14, new Color(0.25f, 0.25f, 0.27f));
        float cover = Cover;
        float coverX = stackArea.xMin + stackArea.width * cover;
        c.Rect(stackArea.xMin, stackArea.yMax + 14, coverX, stackArea.yMax + 24, new Color(0.45f, 0.5f, 0.35f));
        coverHandle = new Rect(coverX - 8, stackArea.yMax + 8, 16, 22);
        c.Rect(coverHandle.xMin, coverHandle.yMin, coverHandle.xMax, coverHandle.yMax, holding == Hold.Cover ? Mark : new Color(0.7f, 0.6f, 0.4f));
        // The sun where the cover does not reach.
        if (cover < 0.95f) c.Halo(stackArea.xMax - 26, stackArea.yMax + 66, 26, new Color(1f, 0.9f, 0.5f), 0.6f * (1 - cover));
        // What is in the stack already, with how dry each is (DRY: the tick is dry enough for a fit).
        int row = 0;
        float dryTick = (float)((CraftWoodwork.Green - CraftWoodwork.DryEnough) / (CraftWoodwork.Green - CraftWoodwork.Seasoned));
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Wood && q.location == CraftLocation.Stack && !(dragging && q == p))
            {
                // stacked from the bottom up, each board on its stickers
                var r = new Rect(stackArea.xMin + 10, stackArea.yMin + 6 + row * 36, stackArea.width - 20, 16);
                if (r.yMax + 14 > stackArea.yMax) break;
                DrawBoard(c, q, r, false);
                for (int k = 0; k < 4; k++) c.Rect(r.xMin + 10 + k * (r.width - 30) / 3f, r.yMin - 6, r.xMin + 18 + k * (r.width - 30) / 3f, r.yMin, new Color(0.25f, 0.18f, 0.1f));
                float dryness = Mathf.Clamp01((float)((CraftWoodwork.Green - q.wood.moisture) / (CraftWoodwork.Green - CraftWoodwork.Seasoned)));
                Gauge(c, "DRY", r.xMin, r.yMax + 4, r.width, 6, dryness, new Color(0.9f, 0.8f, 0.55f), 30f, (dryTick, 1f, GoodBand));
                if (q == p) c.Frame(r.xMin - 2, r.yMin - 2, r.xMax + 2, r.yMax + 2, Mark, 1.5f);
                row++;
            }
        // The trestles.
        c.Rect(trestle.xMin, trestle.yMin - 60, trestle.xMin + 10, trestle.yMin - 10, Wood);
        c.Rect(trestle.xMax - 10, trestle.yMin - 60, trestle.xMax, trestle.yMin - 10, Wood);
        c.Rect(trestle.xMin - 6, trestle.yMin - 16, trestle.xMax + 6, trestle.yMin - 10, Wood);
        if (p == null || p.location == CraftLocation.Stack && !dragging || p.location != CraftLocation.Air && p.location != CraftLocation.Stack)
            return;
        PieceWork w = Work(p);
        // The board on the trestles: the stickers laid under it, its ends being sealed (or carried).
        Rect b = dragging ? new Rect(dragAt.x - 120, dragAt.y - 12, 240, 24) : trestle;
        DrawBoard(c, p, b);
        if (dragging)
            return;
        foreach (float s in w.stickers)
        {
            float sx = trestle.xMin + s * trestle.width;
            c.Rect(sx - 4, trestle.yMin - 10, sx + 4, trestle.yMin, new Color(0.25f, 0.18f, 0.1f));
        }
        leftEnd = new Rect(b.xMin - 8, b.yMin - 6, 22, b.height + 12);
        rightEnd = new Rect(b.xMax - 14, b.yMin - 6, 22, b.height + 12);
        float sealL = Seal(w, p, true), sealR = Seal(w, p, false);
        c.Rect(b.xMin, b.yMin, b.xMin + 6, b.yMax, new Color(0.9f, 0.82f, 0.5f, sealL));
        c.Rect(b.xMax - 6, b.yMin, b.xMax, b.yMax, new Color(0.9f, 0.82f, 0.5f, sealR));
        float even = Evenness(w.stickers), sealed_ = (sealL + sealR) * 0.5f;
        Gauge(c, "STICKERS", 24f, 372f, 340f, 8f, even, new Color(0.6f, 0.85f, 1f), 74f, (0f, (float)CraftWoodwork.EvenStickers, HarmBand), ((float)CraftWoodwork.EvenStickers, 1f, GoodBand));
        Gauge(c, "SEALED", 24f, 356f, 340f, 8f, sealed_, new Color(0.9f, 0.82f, 0.5f), 74f, (0f, (float)CraftWoodwork.WellSealed, HarmBand), ((float)CraftWoodwork.WellSealed, 1f, GoodBand));
        Gauge(c, "COVER", 24f, 340f, 340f, 8f, Cover, new Color(0.45f, 0.5f, 0.35f), 74f, (0f, (float)CraftWoodwork.WellShaded, HarmBand), ((float)CraftWoodwork.WellShaded, 1f, GoodBand));
    }

    /// <summary>The board on the trestles was let go over the stack: stacked with its stickers, its seals and the cover as it is; the words name every shortfall.</summary>
    private void StackIt(CraftPiece p)
    {
        PieceWork w = Work(p);
        float even = Evenness(w.stickers), sealed_ = (Seal(w, p, true) + Seal(w, p, false)) * 0.5f, cover = Cover;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.StackWood, id) { a = even, b = sealed_, c = cover }, (ok, why) =>
        {
            if (!ok) return;
            works.Remove(id); // stacked: the stickers and the seals went with it
            var wrong = new List<string>();
            if (even < CraftWoodwork.EvenStickers) wrong.Add("on too few or uneven stickers it will warp (one under each end, the rest evenly between: four or more)");
            if (sealed_ < CraftWoodwork.WellSealed) wrong.Add("its ends are not sealed: they will check");
            if (cover < CraftWoodwork.WellShaded) wrong.Add("in the sun it dries too fast and checks (pull the cover over)");
            ui.Message(wrong.Count == 0 ? "Stacked well: shaded, sealed, on even stickers. Now it seasons slowly." : "Stacked - but " + string.Join("; ", wrong) + ".");
        });
    }

    private void DryPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        PieceWork w = Current;
        if (phase == CraftPointer.Down)
        {
            if (coverHandle.Contains(pos)) { coverValue = Cover; holding = Hold.Cover; coverHeld = true; return; }
            if (p == null) { said = "Pick a board (on the left) to lay it on the trestles."; return; }
            if (p.location == CraftLocation.Stack)
            {
                if (stackArea.Contains(pos)) { dragging = true; dragAt = pos; }
                return;
            }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            // under the board a sticker (one under each end too), on the board's ends the wax
            bool under = stripRect.Contains(pos) && pos.y < trestle.yMin;
            if (!under && leftEnd.Contains(pos)) { holding = Hold.SealLeft; return; }
            if (!under && rightEnd.Contains(pos)) { holding = Hold.SealRight; return; }
            if (under)
            {
                // a sticker laid under the board where it is clicked (a click on one takes it up)
                ignorePress = true;
                float at = Mathf.Clamp01((pos.x - trestle.xMin) / trestle.width);
                for (int i = 0; i < w.stickers.Count; i++)
                    if (Mathf.Abs(w.stickers[i] - at) * trestle.width < 8f) { w.stickers.RemoveAt(i); return; }
                if (w.stickers.Count < 8) w.stickers.Add(at);
                else said = "Eight stickers are plenty.";
                return;
            }
            if (trestle.Contains(pos)) { dragging = true; dragAt = pos; }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (holding == Hold.Cover)
            {
                float v = Mathf.Clamp01((pos.x - stackArea.xMin) / stackArea.width);
                if (Mathf.Abs(v - coverValue) > 0.002f)
                {
                    coverValue = v;
                    // a setting: only its latest value goes, without a full refresh of the window for each step of the drag
                    ui.WithoutRefresh(() => Send(new CraftRequest(CraftRequestKind.StackCover) { a = v }));
                }
            }
            if (dragging) dragAt = pos;
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (holding == Hold.Cover) { coverPending = true; coverSentAt = Time.unscaledTime; }
        holding = Hold.None;
        coverHeld = false;
        if (!dragging || p == null)
            return;
        dragging = false;
        if (p.location == CraftLocation.Air && stackArea.Contains(pos))
            StackIt(p);
        else if (p.location == CraftLocation.Stack && !stackArea.Contains(pos))
        {
            string id = p.id;
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) =>
            {
                CraftPiece q = Ws.Piece(id);
                if (ok && q != null)
                    ui.Message(q.wood.moisture > CraftWoodwork.DryEnough ? "Taken off the stack still damp: it will shrink loose in a fit." : "Taken off the stack, seasoned.");
            });
        }
    }

    // ================================================================== sanding and finishing
    private Rect board, pot;
    private float load;
    private readonly CraftStroke sanding = new CraftStroke(0.12);
    private readonly List<Vector2> sandPoints = new List<Vector2>(), brushing = new List<Vector2>();
    private int sentPasses;

    private CraftSupplyDefinition FinishSupply => finishes.Count > 0 && finishSupply != null ? Rules.Supply(finishes[Mathf.Clamp(finishSupply.value, 0, finishes.Count - 1)]) : null;

    /// <summary>The roughness (0-60 on the gauge) a paper of <paramref name="grit"/> bites fully below; none for the coarsest.</summary>
    private static float BitesBelow(float grit) => grit <= CraftWoodwork.CoarsestGrit + 1e-4 ? 999f : (float)(CraftWoodwork.SandFloor(grit - CraftWoodwork.GritStep) + CraftWoodwork.GritReach);

    private void DrawFinish(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        pot = new Rect(614, 280, 76, 64);
        c.Rect(30, 112, 690, 132, Wood);
        board = new Rect(40, 140, 640, 110);
        if (finishTool == FinishTool.Brush)
        {
            CraftSupplyDefinition s = FinishSupply;
            Color stuff = s != null && s.color != Color.white ? s.color : new Color(0.6f, 0.38f, 0.15f);
            stuff.a = 1f;
            c.Rect(pot.xMin, pot.yMin, pot.xMax, pot.yMax, Iron);
            c.Rect(pot.xMin + 6, pot.yMax - 18, pot.xMax - 6, pot.yMax - 6, stuff);
            Gauge(c, "BRUSH", 560f, 352f, 130f, 6f, load, Mark, 48f);
        }
        if (p == null || p.location != CraftLocation.Air)
            return;
        DrawBoard(c, p, board);
        DrawRough(c, p, board);
        if (finishTool != FinishTool.Brush)
        {
            // SURFACE: how rough it is on the whole (smooth to the left), where the paper in hand bites (green) and hardly
            // does (grey); the strip under the board says it stretch by stretch - the paper is judged where it goes:
            // green where it bites, grey where the scratches there are deeper than it takes out, bright where it has done
            // all it can
            double rough = 0, l = 0;
            foreach (MetalSegment s in p.segments) { rough += s.rough * s.length; l += s.length; }
            rough /= Math.Max(1e-6, l);
            float grit = Grits[(int)finishTool], floor = (float)CraftWoodwork.SandFloor(grit) / 60f, bites = Mathf.Min(1f, BitesBelow(grit) / 60f);
            Gauge(c, "SURFACE", 30f, 372f, 470f, 8f, (float)rough / 60f, new Color(0.85f, 0.75f, 0.55f), 70f, (floor, bites, GoodBand), (bites, 1f, IdleBand));
            int n = p.segments.Count;
            for (int i = 0; i < n; i++)
            {
                double r = p.segments[i].rough;
                Color band = CraftWoodwork.SkipsGrit(r, grit) ? IdleBand : r <= CraftWoodwork.SandFloor(grit) + 1.5 ? Good : GoodBand;
                CraftArt.Span(c, board.xMin + board.width * i / n, board.yMin - 6f, board.xMin + board.width * (i + 1) / n, board.yMin - 2f, band);
            }
            for (int g = 0; g < Grits.Length; g++)
            {
                float gx = 100f + (float)CraftWoodwork.SandFloor(Grits[g]) / 60f * 400f;
                c.Line(gx, 366f, gx, 371f, CraftArt.LabelDim, 1f);
                CraftArt.Label(c, gx - 3f, 354f, GritNames[g].Substring(0, 1), CraftArt.LabelDim);
            }
        }
        for (int i = 1; i < sandPoints.Count; i++) c.Line(sandPoints[i - 1].x, sandPoints[i - 1].y, sandPoints[i].x, sandPoints[i].y, new Color(1f, 1f, 1f, 0.35f), 4f);
        for (int i = 1; i < brushing.Count; i++) c.Line(brushing[i - 1].x, brushing[i - 1].y, brushing[i].x, brushing[i].y, new Color(0.8f, 0.55f, 0.25f, 0.6f), 8f);
        Vector2 h = ui.Hover;
        if (h.x >= 0)
        {
            if (finishTool == FinishTool.Brush) { c.Rect(h.x - 10, h.y - 4, h.x + 10, h.y + 4, new Color(0.3f, 0.22f, 0.15f)); c.Line(h.x, h.y + 4, h.x + 10, h.y + 40, Wood, 4f); }
            else c.Rect(h.x - 22, h.y - 14, h.x + 22, h.y + 14, finishTool == FinishTool.Coarse ? new Color(0.62f, 0.5f, 0.35f) : finishTool == FinishTool.Medium ? new Color(0.74f, 0.64f, 0.48f) : new Color(0.85f, 0.78f, 0.62f));
        }
    }

    private void FinishPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (phase == CraftPointer.Down)
        {
            if (finishTool == FinishTool.Brush && pot.Contains(pos))
            {
                ignorePress = true;
                CraftSupplyDefinition s = FinishSupply;
                if (s == null) { said = "There is no finish here."; return; }
                load = 1f;
                said = (s.kind == CraftSupplyKind.Wax ? "You take up wax on the cloth." : "The brush is loaded.")
                       + (Have(s.id) == 0 ? $" You have no {ItemOf(s.id)}: only what is left in a pot you opened will do." : "");
                return;
            }
            if (p == null) { said = "Pick a piece of wood (on the left) to lay it on the bench."; return; }
            if (p.location != CraftLocation.Air) { said = OutOfPlace(p); return; }
            if (!board.Contains(pos)) return;
            if (finishTool == FinishTool.Brush)
            {
                brushing.Clear();
                brushing.Add(pos);
            }
            else
            {
                sanding.Clear();
                sandPoints.Clear();
                sentPasses = 0;
                SandSample(pos);
            }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (brushing.Count > 0) brushing.Add(pos);
            else if (sanding.Active)
            {
                SandSample(pos);
                while (sentPasses < sanding.PassRuns.Count) SendPass(p, sanding.PassRuns[sentPasses++]);
            }
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (brushing.Count > 0) EndBrush(p);
        else if (sanding.Active) EndSanding(p);
    }

    private void SandSample(Vector2 pos)
    {
        sandPoints.Add(pos);
        sanding.Add(ui.PointerTime, (pos.x - board.xMin) / Mathf.Max(1f, board.width), (pos.y - board.yMin) / Mathf.Max(1f, board.height), 0, 0.3, Hands);
    }

    /// <summary>The pass under way, if it is one, and the stroke is over.</summary>
    private void EndSanding(CraftPiece p)
    {
        while (sentPasses < sanding.PassRuns.Count) SendPass(p, sanding.PassRuns[sentPasses++]);
        if (sanding.Current(out CraftPass last)) SendPass(p, last);
        else if (sentPasses == 0 && sandPoints.Count > 2) said = "Longer strokes: back and forth along the board, a hand's length each way.";
        sanding.Clear();
        sandPoints.Clear();
        sentPasses = 0;
    }

    /// <summary>One pass of the paper along the board: its stretch, how much along the grain - sent as it is made.</summary>
    private void SendPass(CraftPiece p, CraftPass pass)
    {
        if (p == null || p.location != CraftLocation.Air) return;
        int n = p.segments.Count;
        int i0 = Mathf.Clamp(Mathf.FloorToInt((float)Math.Min(pass.from, pass.to) * n), 0, n - 1), i1 = Mathf.Clamp(Mathf.FloorToInt((float)Math.Max(pass.from, pass.to) * n), i0, n - 1);
        float along = 0f, across = 0f;
        for (int i = pass.first + 1; i <= pass.last && i < sandPoints.Count; i++)
        {
            along += Mathf.Abs(sandPoints[i].x - sandPoints[i - 1].x);
            across += Mathf.Abs(sandPoints[i].y - sandPoints[i - 1].y);
        }
        float alongGrain = along + across > 1f ? along / (along + across) : 1f;
        float grit = Grits[(int)finishTool];
        string skips = SkippedWhere(p, i0, i1, grit);
        string name = GritNames[(int)finishTool], coarser = finishTool > FinishTool.Coarse ? GritNames[(int)finishTool - 1] : "coarse";
        Send(new CraftRequest(CraftRequestKind.SandWood, p.id) { i0 = i0, i1 = i1, a = grit, b = 1.5f, c = alongGrain }, (ok, why) =>
        {
            if (!ok) return;
            said = alongGrain < 0.7f ? "Across the grain: it leaves scratches. Sand along it (left and right)."
                : skips == "" ? (finishTool == FinishTool.Coarse ? "The coarse paper takes the tool marks off. Then the medium."
                    : finishTool == FinishTool.Medium ? "The medium paper takes out the coarse scratches. Then the fine." : "The fine paper leaves it smooth.")
                : skips == "all" ? $"The {name} paper hardly bites: the scratches it meets are deeper than it takes out (grey under the board). Go back to the {coarser} paper."
                : $"{CraftText.Capital(skips)} the {name} paper hardly bites (grey under the board): take the {coarser} paper there first. Elsewhere it does its work.";
        });
    }

    /// <summary>
    /// Where along stretches <paramref name="i0"/>-<paramref name="i1"/> a paper of <paramref name="grit"/> hardly bites
    /// (<see cref="CraftWoodwork.SkipsGrit(double, double)"/>, stretch by stretch): "" nowhere, "all" all of them, else in
    /// words - "at the left end", "at the right end", "from 120 to 180 mm".
    /// </summary>
    private static string SkippedWhere(CraftPiece p, int i0, int i1, float grit)
    {
        int first = -1, last = -1, count = 0, n = p.segments.Count, a = Math.Max(0, i0), b = Math.Min(n - 1, i1);
        for (int i = a; i <= b; i++)
            if (CraftWoodwork.SkipsGrit(p.segments[i].rough, grit)) { if (first < 0) first = i; last = i; count++; }
        if (first < 0) return "";
        if (count == b - a + 1) return "all";
        bool together = count == last - first + 1;
        if (together && first == 0) return "at the left end";
        if (together && last == n - 1) return "at the right end";
        double from = 0;
        for (int i = 0; i < first; i++) from += p.segments[i].length;
        double to = from;
        for (int i = first; i <= last; i++) to += p.segments[i].length;
        return $"from {from:0} to {to:0} mm";
    }

    /// <summary>A stroke of the brush (or of the wax cloth) as it was laid: how even, how thick - sent with what it lays.</summary>
    private void EndBrush(CraftPiece p)
    {
        var pts = new List<Vector2>(brushing);
        brushing.Clear();
        if (p == null || p.location != CraftLocation.Air || pts.Count < 2 || finishes.Count == 0) return;
        float minX = float.MaxValue, maxX = float.MinValue, along = 0f, wobble = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            minX = Mathf.Min(minX, pts[i].x);
            maxX = Mathf.Max(maxX, pts[i].x);
            if (i == 0) continue;
            along += Mathf.Abs(pts[i].x - pts[i - 1].x);
            wobble += Mathf.Abs(pts[i].y - pts[i - 1].y);
        }
        if (maxX - minX < 8f) return;
        CraftSupplyDefinition s = FinishSupply;
        if (s == null) return;
        bool wax = s.kind == CraftSupplyKind.Wax;
        if (load <= 0.05f) { said = wax ? "The cloth is dry: take up wax from the pot." : "The brush is dry: dip it in the pot."; return; }
        int n = p.segments.Count;
        int i0 = Mathf.Clamp(Mathf.FloorToInt((minX - board.xMin) / board.width * n), 0, n - 1), i1 = Mathf.Clamp(Mathf.FloorToInt((maxX - board.xMin) / board.width * n), i0, n - 1);
        float alongGrain = along + wobble > 1f ? along / (along + wobble) : 1f;
        // A loaded brush on a short stroke lays it thick; a long, straight stroke lays it thin and even.
        float spread = (maxX - minX) / board.width;
        float thick = Mathf.Clamp01(load * (1.1f - 0.8f * spread));
        float even = Mathf.Clamp01(alongGrain * (1f - wobble / Mathf.Max(1f, along) * 2f)) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
        load = Mathf.Max(0f, load - 0.35f - 0.4f * spread);
        bool damp = p.wood.moisture > CraftWoodwork.DryEnough, overWet = p.wood.finishWet > 0.4 && !wax;
        float dryS = (float)((s.dryingHours > 0 ? s.dryingHours : CraftWoodwork.DefaultDryingHours) / Math.Max(1e-3, Rules.Settings.processHoursPerSecond));
        Hands.Move(0.2);
        Send(new CraftRequest(CraftRequestKind.Varnish, p.id) { id = s.id, i0 = i0, i1 = i1, a = even, b = thick }, (ok, why) =>
        {
            if (!ok) return;
            if (wax) said = damp ? "Wax on damp wood smears." : thick > 0.75f ? "Laid on thick: the wax smears instead of working in." : even < 0.5f ? "Patchy: rub it in with long, even strokes." : "Rubbed in thin and even: a soft sheen that sheds water.";
            else if (s.kind == CraftSupplyKind.Oil) said = damp ? "On damp wood little of the oil soaks in: season it first." : $"It soaks into the grain (no film). It dries in about {dryS:0} s; a smooth surface takes it best.";
            else said = damp ? "On damp wood it clouds: season it first." : overWet ? "Over a wet coat it wrinkles: let the last one dry first." : thick > 0.75f ? "Laid on thick: it runs and sags." : even < 0.5f ? "A patchy, wavering coat." : $"A thin, even coat. Let it dry (about {dryS:0} s), sand lightly with the fine paper, and lay another.";
        });
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Cancel)
        {
            Cancel();
            return;
        }
        if (phase == CraftPointer.Down) ignorePress = false;
        if (ignorePress)
        {
            if (phase == CraftPointer.Up) ignorePress = false;
            return;
        }
        switch (place)
        {
            case Place.Debark: DebarkPointer(phase, pos, scroll); break;
            case Place.Split: if (phase != CraftPointer.Scroll) SplitPointer(phase, pos); break;
            case Place.Rive: if (phase != CraftPointer.Scroll) RivePointer(phase, pos); break;
            case Place.Saw: if (phase != CraftPointer.Scroll) SawPointer(phase, pos); break;
            case Place.Bend: if (phase != CraftPointer.Scroll) BendPointer(phase, pos); break;
            case Place.Dry: if (phase != CraftPointer.Scroll) DryPointer(phase, pos); break;
            default: if (phase != CraftPointer.Scroll) FinishPointer(phase, pos); break;
        }
    }

    /// <summary>Right-click: the hands put down what they hold, as it is - nothing is sent, nothing of the piece's work is lost.</summary>
    private void Cancel()
    {
        bool any = knife.Count > 0 || lining || maul.Raised || mallet.Raised || levering || sawing || dragging || bending || holding != Hold.None || sanding.Active || brushing.Count > 0;
        if (!any) return;
        if (knife.Count > 0) said = "The drawknife is lifted: that pull did nothing.";
        else if (maul.Raised || mallet.Raised) said = "You lower it without striking.";
        else if (sawing) said = "The saw rests in its kerf.";
        else if (bending) said = "You let it spring back: nothing bent.";
        else if (dragging) said = "Put back where it was.";
        else if (sanding.Active) said = sentPasses > 0 ? "The paper is lifted (the passes made count)." : "The paper is lifted.";
        else if (brushing.Count > 0) said = "The brush is lifted: that stroke laid nothing.";
        knife.Clear();
        brushing.Clear();
        sanding.Clear();
        sandPoints.Clear();
        sentPasses = 0;
        lining = false;
        maul.Lower();
        mallet.Lower();
        levering = false;
        if (sawing) StopSaw();
        dragging = false;
        bending = false;
        sweep = 0f;
        if (holding == Hold.Cover) { coverPending = true; coverSentAt = Time.unscaledTime; }
        holding = Hold.None;
        coverHeld = false;
        ignorePress = true; // the rest of the press does nothing
    }

    // ------------------------------------------------------------------ the words
    public override void Refresh()
    {
        RefreshCounts(finishSupply, finishes);
        if (bendFrom != null) SetSlider(bendFrom, bendFrom.value); // its readout in this piece's millimetres (the piece may have been cut)
        PieceWork cur = Current;
        if (faceText != null) faceText.text = cur != null ? $"Face {cur.face + 1} of {CraftWoodwork.LogFaces} up - the wheel turns the log too." : "The wheel turns the log.";
        if (info == null) return;
        CraftPiece p = WoodPiece;
        string how;
        if (p != null && p.location != CraftLocation.Air && !(place == Place.Bend && p.location == CraftLocation.Steam) && !(place == Place.Dry && p.location == CraftLocation.Stack))
            how = OutOfPlace(p);
        else
            switch (place)
            {
                case Place.Debark:
                    how = p == null ? "<b>Debark</b>: pick a log (on the left)." : CraftWoodwork.BarkLeft(p) <= 0.001 ? "<b>Debarked</b>: split it, rive it or season it."
                        : $"<b>Debark</b>: pull the drawknife right to left along the log, its edge on the bright line (green on DEPTH: grey skims, red gouges{(p.wood.moisture > 0.3 ? "" : " - a dry log gouges sooner")}). Two pulls bare a face; turn the log for the next. {CraftWoodwork.BarkLeft(p) * 100:0}% of the bark left.";
                    break;
                case Place.Split:
                    how = p == null ? "<b>Split</b>: pick a log or a large half (on the left)."
                        : !CraftWoodwork.SplitsWithWedges(p, out string small) ? $"<b>Split</b>: {NotForTheWedges(p, small)}"
                        : cur != null && cur.wedgeSet ? $"Drive the wedge: hold the maul, let go on the wedge - full, square blows. {Mathf.Clamp01(cur.splitWork) * 100:0}% split. (Lift the wedge out to draw its line again.)"
                        : $"<b>Split</b> ({CraftWoodwork.CrossSize(p):0} mm across): drag a line right across the end where the wedge goes{(CraftWoodwork.WedgeLineAcross(p, 1, 0, out _) && CraftWoodwork.WedgeLineAcross(p, 0, 1, out _) ? "" : $", across its {CraftWoodwork.CrossSize(p):0} mm side")}. The halves are as even as the line divides the end; through the heart and along a ray (or the check) it splits straight.";
                    break;
                case Place.Rive:
                    how = p == null ? "<b>Rive</b>: pick a billet (on the left)."
                        : cur == null || cur.riveStage == 0 ? (!CraftWoodwork.RivesWithFroe(p, out string no) ? $"<b>Rive</b>: {no}" : $"<b>Rive</b> ({RiveDepth(p):0} mm across): click a grain line at the left end to set the froe - where it is set is where it parts (the middle line halves it).")
                        : cur.riveStage == 1 ? $"Drive the froe in with the mallet: hold, and let go ({cur.malletBlows}/2 good blows)."
                        : "Lever the split along from its end: hold and move right on the highlighted grain line, at an even pace (PACE in the green).";
                    break;
                case Place.Saw:
                    how = p == null ? "<b>Saw</b>: pick a board (on the left)."
                        : ripMode ? (cur == null || cur.ripA < 0f ? $"<b>A taper</b> ({p.Length:0} mm long, up to {CraftWoodwork.CrossSize(p):0} mm wide): click where the line starts on the board (its height there is the width it leaves), then where it ends."
                            : cur.ripB < 0f ? "Click where the taper's line ends."
                            : $"Saw along the line: long strokes back and forth, keeping on it ({cur.saw.Progress * 100:0}% done).")
                        : cur == null || cur.mark < 0f ? $"<b>Saw across</b> ({p.Length:0} mm long): click on the board to mark where to cut - it is cut exactly at the mark."
                        : $"Press on the mark and saw: long strokes up and down across the board, keeping the blade on the line ({cur.saw.Progress * 100:0}% through).";
                    break;
                case Place.Bend:
                    how = p == null ? "<b>Steam & bend</b>: pick a piece of wood (on the left)." : BendText(p);
                    break;
                case Place.Dry:
                {
                    // The air seasons wood only as dry as it is itself: damp air leaves it damp.
                    double air = CraftWoodwork.Equilibrium(Ws.Humidity);
                    bool meter = Station.HasInstrument(CraftInstruments.MoistureMeter);
                    string wet = p == null ? "" : meter ? $"the moisture meter reads {p.wood.moisture * 100:0}%" : p.wood.moisture > 0.3 ? "it is heavy and cold with water" : p.wood.moisture > CraftWoodwork.DryEnough ? "it still feels cool and damp" : "it feels dry and light";
                    how = p != null && p.location == CraftLocation.Stack
                        ? $"In the stack: {wet}{(meter ? $" (dry enough for a fit under {CraftWoodwork.DryEnough * 100:0}%; this air dries it to about {air * 100:0}%)" : "")}. Drag it off the stack to take it down."
                        : $"<b>Dry</b>: click under the board to lay stickers - one under each end, the rest evenly between (four or more: STICKERS past the tick); hold on each end to seal it; drag the cover's handle over the stack; then drag the board onto the stack.{Thick(p)}{Seals(p)}{(p != null ? $" Now {wet}." : "")}";
                    break;
                }
                default:
                {
                    CraftSupplyDefinition s = FinishSupply;
                    if (finishTool == FinishTool.Brush)
                        how = s == null ? "There is no finish here."
                            : s.kind == CraftSupplyKind.Wax ? $"<b>Wax</b>: click the pot to take up {s.Name.ToLowerInvariant()}, then rub long, thin strokes along the board: it sheds water but wears away."
                            : s.kind == CraftSupplyKind.Oil ? $"<b>Oil</b>: click the pot to load the brush, then lay long strokes along the board: {s.Name.ToLowerInvariant()} soaks into the grain (no film) and must dry between coats."
                            : $"<b>Varnish</b>: click the pot to load the brush, then lay long, thin strokes along the board: {s.Name.ToLowerInvariant()} builds a film; let each coat dry. A pot lays about two full-length coats.";
                    else
                        how = $"<b>Sand</b> ({GritNames[(int)finishTool]}): stroke back and forth along the grain (left-right), a hand's length each way - coarse, then medium, then fine: each paper clears only the scratches of the one before. Under the board: green where this paper bites, grey where it is too fine for the scratches there, bright where it has done all it can.";
                    break;
                }
            }
        info.text = (how + " " + said).Trim();
    }

    /// <summary>
    /// Stock thicker than the stack seasons at its pace: it takes longer - and a half, a quarter or a log (twice that
    /// thickness or more) is better split or rived first, its billets seasoning at the stack's pace.
    /// </summary>
    private static string Thick(CraftPiece p)
    {
        if (p == null) return "";
        double pace = CraftWoodwork.Seasoning(p);
        return pace < 0.95 ? $" {CraftWoodwork.Thickness(p):0} mm thick, it seasons {1 / pace:0.#} times as slowly{(pace <= 0.5 ? ": split or rive it first" : "")}." : "";
    }

    /// <summary>Its ends sealed before (the wax stays on through the stack and off it; a fresh saw cut is bare).</summary>
    private static string Seals(CraftPiece p) =>
        p == null || p.wood.endsSealed < 0.05 ? "" : p.wood.endsSealed >= CraftWoodwork.WellSealed ? " Its ends are still sealed from before." : " Its ends are partly sealed from before (a fresh cut is bare).";

    private string BendText(CraftPiece p)
    {
        if (p.location == CraftLocation.Steam)
            return CraftWoodwork.MeanTemperature(p) < CraftWoodwork.SoakHeat
                ? $"Heating through ({CraftWoodwork.MeanTemperature(p):0} °C): it soaks once it is hot (HEAT past the tick)."
                : p.wood.steamed < 1 ? $"Soaking: {p.wood.steamed * 100:0}% through (SOAK). When it is through, drag it out and bend it at once."
                : "Steamed through: drag it out and bend it at once (and do not leave it in too long: it goes soft and mushy).";
        double has = 0;
        foreach (MetalSegment s in p.segments) has += s.bend;
        CraftComponentSpec part = !string.IsNullOrEmpty(p.intentDesign) ? Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        string want = part == null || part.shape == null ? $"It has {has:0}° of bend."
            : Math.Abs(part.shape.curvature) < 1 ? $"The {part.Name.ToLowerInvariant()} should stay straight{(Math.Abs(has) > 2 ? $" (it has {has:0}°)" : "")}."
            : $"The {part.Name.ToLowerInvariant()} wants {part.shape.curvature:0}°: it has {has:0}° (it springs back: bend a little past).";
        double soft = CraftWoodwork.Plasticity(p);
        string state = soft > 0.3 ? $"Soft for about {Math.Max(0, p.wood.steamed * CraftWoodwork.CoolSeconds(p)):0} s more: drag the free end round the form, at an even pace." : "It is stiff: drag it by its clamped end into the steam box first.";
        return $"<b>Steam & bend</b>: {state} STRAIN past the tick tears it (a gentler form, the strap, more steam) - it keeps the pull's fastest pace, the thin tick is the pace now. {want}";
    }
}
