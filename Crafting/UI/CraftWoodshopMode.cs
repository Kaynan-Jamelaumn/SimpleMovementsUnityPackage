using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The woodshop, in seven places, each worked with the hands:
/// <list type="bullet">
/// <item><b>Debark</b>: the log on the shaving horse, its end in section beside it. Pull the drawknife along the log
/// (drag right to left) with its edge at the bark's own depth - the thin bright band under the bark: above it only the
/// outer bark comes off, below it the knife gouges the wood. Hold the depth steady; turn the log (wheel or right-click)
/// to reach the bark on its other faces. A green log peels easily; a dry one's bark is tight.</item>
/// <item><b>Split</b>: the log's end. Drag a line across it where the wedge goes - through the heart and along a ray
/// (or a check) splits it in even halves along the grain; off the heart one half is small, across the rays it twists out.
/// Then drive the wedge with the maul (hold and release, full, square blows on the wedge) until it parts.</item>
/// <item><b>Rive</b>: the billet from the side, its grain drawn along it. Set the froe on a grain line at the end, drive
/// it in with the mallet (hold and release), then lever the split along: keep the pointer on the grain line as it
/// wanders - the split follows the fibres if you do, and cuts across them (or runs out of the side) if you do not; rush
/// it and it runs off on its own.</item>
/// <item><b>Saw</b>: mark where to cut on the board, then saw: stroke up and down at the mark and keep the blade on the
/// line - wandering off it leaves the cut crooked and the end torn.</item>
/// <item><b>Steam &amp; bend</b>: drag the piece into the steam box and leave it (an hour for every 25 mm of thickness -
/// the gauge fills), drag it out and bend it at once, while it is hot: drag its free end round the form, steadily. The
/// strain gauge shows how near the outside fibres are to tearing; the strap holds them. It cools fast.</item>
/// <item><b>Dry</b>: lay stickers under the board, evenly; seal its ends with wax (hold on each end); pull the cover
/// over the stack; then drag the board onto the stack. Seasoning too fast - in the sun, unsealed, on uneven stickers -
/// checks and warps it.</item>
/// <item><b>Finish</b>: sand it (coarse, then fine, along the grain: strokes across it leave scratches), then brush on
/// varnish or oil - dip the brush, lay long even strokes; a loaded brush lays it thick (it runs), damp wood clouds it.</item>
/// </list>
/// A test station has quick buttons for all of it.
/// </summary>
public sealed class CraftWoodshopMode : CraftMode
{
    private enum Place { Debark, Split, Rive, Saw, Bend, Dry, Finish }
    private enum FinishTool { Coarse, Medium, Fine, Brush }
    private static readonly string[] PlaceNames = { "Debark", "Split log", "Rive", "Saw", "Steam & bend", "Dry", "Finish" };
    private static readonly float[] Grits = { 0.25f, 0.55f, 0.85f };
    private static readonly string[] FormNames = { "Tight form (6 cm)", "Medium form (12 cm)", "Gentle form (25 cm)" };
    private static readonly float[] FormRadius = { 60f, 120f, 250f };

    private Place place = Place.Debark;
    private FinishTool finishTool = FinishTool.Coarse;
    private readonly List<Button> placeButtons = new List<Button>(), finishButtons = new List<Button>();
    private readonly List<RectTransform> bendRows = new List<RectTransform>(), finishRows = new List<RectTransform>();
    private TMP_Dropdown form, finishSupply;
    private Slider bendAt;
    private Button strapButton;
    private bool strap = true;
    private TextMeshProUGUI info;
    private readonly List<string> finishes = new List<string>();
    private string said = "";

    // Riving.
    private readonly CraftHammer mallet = new CraftHammer { fullCharge = 0.6f };
    private int riveStage, riveLine = -1, malletBlows;
    private float riveFront, riveStray, riveWeight, riveLastX, riveLastT, riveJerk;
    private readonly List<Vector2> splitPath = new List<Vector2>();
    private Rect billet;

    // Sawing.
    private Rect board;
    private float markX = -1f, sawStartX, kerf, wanderSum;
    private int wanderN;
    private bool sawing;
    private readonly CraftShake saw = new CraftShake();

    // Steaming and bending.
    private Rect steamBox, bench, formArea, clamp;
    private Vector2 formCentre;
    private bool dragging, bending;
    private Vector2 dragAt;
    private float bendAngle, bendPeak, lastAngle, lastAngleTime;

    // Drying.
    private Rect trestle, stackArea, coverHandle, leftEnd, rightEnd;
    private readonly List<float> stickers = new List<float>();
    private float sealLeft, sealRight, cover = 0.2f;
    private int holding; // 1 left end, 2 right end, 3 the cover

    // Finishing.
    private readonly List<Vector2> stroke = new List<Vector2>();
    private float strokeStart, load;
    private Rect pot;

    // Debarking: the log on the horse, the face turned up, the strokes on each face of each stretch.
    private Rect logSide, logEnd;
    private int face;
    private const int Faces = 4;
    private readonly Dictionary<long, int> faceStrokes = new Dictionary<long, int>();
    private string debarkFor = "";
    private readonly List<Vector2> knife = new List<Vector2>();
    private float knifeStart;

    // Splitting: the wedge's line across the end, then the maul.
    private readonly CraftHammer maul = new CraftHammer { fullCharge = 0.9f, weight = 1.4f };
    private Vector2 lineA, lineB;
    private bool lining, wedgeSet;
    private float splitWork, splitSolid;
    private int splitBlows;
    private Vector2 endCentre;
    private float endRadius;

    public override string Title => "Woodshop";
    public override CraftDiscipline Discipline => CraftDiscipline.Woodwork;
    public override bool Available(CraftStation s) =>
        s.Has(CraftTools.ChoppingBlock) || s.Has(CraftTools.Sawhorse) || s.Has(CraftTools.SteamBox) || s.Has(CraftTools.DryingStack) || s.Has(CraftTools.FinishingBench) || s.Has(CraftTools.ShavingHorse);
    public override string Hint => "Debark the log, split it, rive along the grain, saw to the line, steam and bend, season in the stack, sand through the grits and seal - each with the hands, on the drawing.";

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

    public override void Build(RectTransform controls)
    {
        bendRows.Clear();
        finishRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        if (!Here(place))
            for (int i = 0; i < PlaceNames.Length; i++)
                if (Here((Place)i)) { place = (Place)i; break; }
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            Button b = Btn(r0, PlaceNames[i], () => { place = pl; Reset(); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f);
            b.gameObject.SetActive(Here(pl));
            placeButtons.Add(b);
        }
        RectTransform b1 = Row(controls);
        form = Drop(b1, FormNames, 1, 190f);
        strapButton = Btn(b1, "Strap on", () => { strap = !strap; Show(); }, 100f);
        bendAt = Slide(b1, "Bend at", 0.1f, 0.9f, 0.5f, false, 56f);
        bendRows.Add(b1);
        RectTransform f1 = Row(controls, 30f, 4f);
        finishButtons.Clear();
        string[] tools = { "Coarse", "Medium", "Fine", "Brush / rub" };
        for (int i = 0; i < tools.Length; i++)
        {
            FinishTool t = (FinishTool)i;
            finishButtons.Add(Btn(f1, tools[i], () => { finishTool = t; Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        finishes.Clear();
        var fn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Varnish || s.kind == CraftSupplyKind.Oil || s.kind == CraftSupplyKind.Wax) { finishes.Add(s.id); fn.Add(s.Name); }
        finishSupply = CountedDrop(f1, finishes, 190f, "No finish");
        finishRows.Add(f1);
        if (QuickBench)
        {
            RectTransform q = Row(controls);
            Btn(q, "Debark", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.DebarkWood, p.id) { i0 = 0, i1 = p.segments.Count - 1, a = 0.45f, b = 0.95f, c = 1f }); }, 70f);
            Btn(q, "Split", () => Send(new CraftRequest(CraftRequestKind.SplitLog, ui.SelectedId) { a = 0.02f, b = 0.95f, c = 0.9f, i0 = 5 }), 50f);
            Btn(q, "Rive", () => Send(new CraftRequest(CraftRequestKind.Rive, ui.SelectedId) { a = 0.05f, b = 0.9f }), 60f);
            Btn(q, "Saw in half", () => Send(new CraftRequest(CraftRequestKind.SawCut, ui.SelectedId) { a = 0.5f, b = 0.05f, c = 0.95f }), 100f);
            Btn(q, "Steam", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Steam }), 70f);
            Btn(q, "Bend 90°", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.BendWood, p.id) { i0 = p.segments.Count / 4, i1 = p.segments.Count * 3 / 4, a = 90f, b = 0.2f, flag = true }); }, 80f);
            Btn(q, "Stack", () => Send(new CraftRequest(CraftRequestKind.StackWood, ui.SelectedId) { a = 1f, b = 1f, c = 1f }), 60f);
            Btn(q, "Sand", () => { CraftPiece p = Selected; if (p != null) Send(new CraftRequest(CraftRequestKind.SandWood, p.id) { i0 = 0, i1 = p.segments.Count - 1, a = 0.9f, b = 10f, c = 1f }); }, 60f);
            Btn(q, "Varnish", () => { CraftPiece p = Selected; if (p != null && finishes.Count > 0) Send(new CraftRequest(CraftRequestKind.Varnish, p.id) { id = finishes[0], i0 = 0, i1 = p.segments.Count - 1, a = 0.9f, b = 0.5f }); }, 70f);
        }
        info = InfoRow(controls, 44f);
        Show();
    }

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p) && Here(p)) { place = p; Show(); }
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        for (int i = 0; i < finishButtons.Count; i++) UITheme.SetSelected(finishButtons[i], (FinishTool)i == finishTool);
        foreach (RectTransform r in bendRows) r.gameObject.SetActive(place == Place.Bend);
        foreach (RectTransform r in finishRows) r.gameObject.SetActive(place == Place.Finish);
        TMP_Text t = strapButton != null ? strapButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = strap ? "Strap on" : "No strap";
        if (strapButton != null) UITheme.SetSelected(strapButton, strap);
    }

    private void Reset()
    {
        riveStage = 0;
        riveLine = -1;
        malletBlows = 0;
        splitPath.Clear();
        markX = -1f;
        sawing = false;
        dragging = bending = false;
        holding = 0;
        stroke.Clear();
        mallet.Lower();
        maul.Lower();
        knife.Clear();
        lining = wedgeSet = false;
        splitWork = splitSolid = 0f;
        splitBlows = 0;
    }

    public override void Leave() => Reset();

    public override void Tick(float dt)
    {
        mallet.Tick(dt);
        maul.Tick(dt);
        // Sealing an end, pulling the cover: held on the drawing.
        if (holding != 0 && !ui.PointerHeld) holding = 0;
        if (holding == 1) sealLeft = Mathf.Min(1f, sealLeft + dt / 1.2f);
        if (holding == 2) sealRight = Mathf.Min(1f, sealRight + dt / 1.2f);
    }

    private CraftPiece WoodPiece => Selected != null && Selected.kind == CraftPieceKind.Wood && Selected.segments.Count > 0 ? Selected : null;

    // ------------------------------------------------------------------ drawing the wood
    private Color WoodColour(CraftPiece p) => Rules.Wood(p.wood.wood)?.color ?? new Color(0.7f, 0.55f, 0.35f);

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

    private void DrawBoard(CraftPixels c, CraftPiece p, Rect r, bool grain = true)
    {
        Color wood = WoodColour(p);
        if (p.wood.moisture > 0.2) wood = Color.Lerp(wood, wood * 0.7f, Mathf.Clamp01(((float)p.wood.moisture - 0.2f) * 6f));
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
                        c.Line(x - 4f, lastY, x, y, k == riveLine && place == Place.Rive ? Mark : Color.Lerp(wood, Color.black, 0.25f), k == riveLine && place == Place.Rive ? 2f : 1f);
                    lastY = y;
                }
            }
        }
        // Checks at the ends and along the faces.
        int checks = Mathf.RoundToInt((float)p.wood.checks * 12f);
        for (int k = 0; k < checks; k++)
        {
            float y = r.yMin + (k * 37 % 100) / 100f * r.height;
            float x = k % 2 == 0 ? r.xMin + 2f : r.xMax - 2f - 14f;
            c.Line(x, y, x + 14f, y + 2f, new Color(0.1f, 0.07f, 0.05f, 0.9f), 1.5f);
        }
        // The finish where it went on: a sheen, blotchy where uneven, a wet gleam while tacky.
        if (p.wood.finish > 0.02)
        {
            int n = p.segments.Count;
            for (int i = 0; i < n; i++)
            {
                float coat = Mathf.Clamp01((float)p.segments[i].coating);
                if (coat < 0.02f) continue;
                float x0 = r.xMin + r.width * i / n, x1 = r.xMin + r.width * (i + 1) / n;
                float blotch = (float)((1 - p.wood.finishEven) * (CraftMath.Noise(CraftMath.Hash(p.id ?? ""), i, 7) - 0.5));
                float a = Mathf.Clamp01(coat * 1.6f + blotch * 0.6f);
                c.Rect(x0, r.yMin, x1 + 0.5f, r.yMax, new Color(0.7f, 0.38f, 0.12f, a * 0.55f));
                c.Rect(x0, r.yMin + r.height * 0.62f, x1 + 0.5f, r.yMin + r.height * 0.68f, new Color(1f, 0.95f, 0.8f, a * 0.35f)); // the gloss
                if (p.wood.finishWet > 0.3) c.Rect(x0, r.yMax - r.height * 0.3f, x1 + 0.5f, r.yMax - r.height * 0.2f, new Color(1f, 1f, 1f, 0.25f * coat));
            }
        }
        c.Frame(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0.1f, 0.07f, 0.05f, 0.8f), 1f);
    }

    /// <summary>Roughness as specks, per segment of the board drawn in <paramref name="r"/>.</summary>
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

    // ------------------------------------------------------------------ debarking
    /// <summary>The depth of the knife's edge (0 skimming the bark's surface - 0.5 at the cambium - 1 deep in the wood) at height y.</summary>
    private float KnifeDepth(float y) => Mathf.Clamp01((logSide.yMax - y) / 36f);

    private int Stretch(CraftPiece p, float x) => Mathf.Clamp(Mathf.FloorToInt((x - logSide.xMin) / logSide.width * p.segments.Count), 0, p.segments.Count - 1);

    private void DrawDebark(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        // The shaving horse: its bench, the jaw holding the log down.
        c.Rect(30, 120, 520, 140, Wood);
        c.Rect(40, 60, 54, 120, Wood);
        c.Rect(496, 60, 510, 120, Wood);
        logSide = new Rect(50, 150, 470, 90);
        logEnd = new Rect(560, 120, 140, 140);
        if (p == null) return;
        if (debarkFor != p.id) { faceStrokes.Clear(); debarkFor = p.id; }
        Color wood = WoodColour(p), bark = Color.Lerp(wood, new Color(0.28f, 0.2f, 0.14f), 0.75f);
        c.Rect(logSide.xMin, logSide.yMin, logSide.xMax, logSide.yMax, wood);
        int n = p.segments.Count;
        for (int i = 0; i < n; i++)
        {
            MetalSegment s = p.segments[i];
            float x0 = logSide.xMin + logSide.width * i / n, x1 = logSide.xMin + logSide.width * (i + 1) / n;
            // The bark on the face turned up: what is left of it, worse where this face was not yet stroked.
            int strokes = faceStrokes.TryGetValue(Key(i, face), out int k) ? k : 0;
            float left = Mathf.Clamp01((float)s.bark * (strokes == 0 ? 1.4f : strokes == 1 ? 0.7f : 0.3f));
            c.Rect(x0, logSide.yMin + 6, x1 + 0.5f, logSide.yMax, new Color(bark.r * 0.8f, bark.g * 0.8f, bark.b * 0.8f, Mathf.Clamp01(left * 1.1f)));
            for (int f = 0; f < 3 && left > 0.2f; f++) c.Line(x0 + 3 + f * 7, logSide.yMin + 14 + f * 21, x0 + 9 + f * 7, logSide.yMin + 30 + f * 21, bark * 0.7f, 1f);
            if (s.rough > 60) c.Disc(x0 + 6, logSide.yMax - 10, 2f, new Color(0.1f, 0.06f, 0.03f, 0.7f));
        }
        if (p.wood.gouged > 0.05)
            for (int g = 0; g < Mathf.RoundToInt((float)p.wood.gouged * 20f); g++)
                c.Line(logSide.xMin + (g * 53 % 97) / 97f * logSide.width, logSide.yMax - 4, logSide.xMin + (g * 53 % 97) / 97f * logSide.width + 18, logSide.yMax - 8, new Color(0.15f, 0.1f, 0.06f), 2f);
        // The bark's thickness and the cambium: the band the knife's edge should run in.
        float cambium = logSide.yMax - 18f;
        c.Rect(logSide.xMax + 4, cambium - 3, logSide.xMax + 14, cambium + 3, new Color(0.95f, 0.9f, 0.6f));
        c.Rect(logSide.xMax + 4, logSide.yMax - 8, logSide.xMax + 14, logSide.yMax, bark);
        // The end, in section: bark ring, the face turned up marked.
        endCentre = logEnd.center;
        endRadius = logEnd.width * 0.45f;
        c.Disc(endCentre.x, endCentre.y, endRadius, bark);
        c.Disc(endCentre.x, endCentre.y, endRadius - 6, Color.Lerp(wood, Color.white, 0.15f));
        for (int r = 1; r < 6; r++) c.Ring(endCentre.x, endCentre.y, (endRadius - 6) * r / 6f, 1f, wood * 0.8f);
        float a = (90f + face * 360f / Faces) * Mathf.Deg2Rad;
        c.Line(endCentre.x, endCentre.y, endCentre.x + Mathf.Cos(a) * endRadius, endCentre.y + Mathf.Sin(a) * endRadius, Mark, 2f);
        // The drawknife: its edge at the pointer, its depth shown on the section.
        Vector2 h = ui.Hover;
        if (h.x >= 0 && logSide.Contains(h) || knife.Count > 0)
        {
            Vector2 at = knife.Count > 0 ? knife[knife.Count - 1] : h;
            float d = KnifeDepth(at.y);
            Color edge = d < 0.3f ? new Color(0.6f, 0.85f, 1f) : d <= 0.62f ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 0.4f, 0.3f);
            c.Rect(at.x - 50, at.y - 2, at.x + 50, at.y + 2, new Color(0.75f, 0.76f, 0.8f));
            c.Rect(at.x - 60, at.y - 4, at.x - 50, at.y + 20, Wood);
            c.Rect(at.x + 50, at.y - 4, at.x + 60, at.y + 20, Wood);
            float rr = (endRadius - 1) - d * 14f;
            c.Ring(endCentre.x, endCentre.y, rr, 1.5f, edge);
            Meter(c, logEnd.xMin, logEnd.yMin - 16, logEnd.width, 6, d, edge);
        }
        for (int i = 1; i < knife.Count; i++) c.Line(knife[i - 1].x, knife[i - 1].y, knife[i].x, knife[i].y, new Color(1f, 1f, 1f, 0.35f), 2f);
        Meter(c, logSide.xMin, logSide.yMin - 30, logSide.width, 6, 1f - (float)CraftWoodwork.BarkLeft(p), new Color(0.9f, 0.8f, 0.55f));
    }

    private static long Key(int stretch, int face) => stretch * 16L + face;

    private void DebarkPointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air) { if (phase == CraftPointer.Down) said = "Pick a log (left) and put it on the horse."; return; }
        if (phase == CraftPointer.Scroll || right && phase == CraftPointer.Down)
        {
            face = (face + (phase == CraftPointer.Scroll && scroll < 0 ? Faces - 1 : 1)) % Faces;
            knife.Clear();
            said = "Turned the log: a new face up.";
            return;
        }
        if (phase == CraftPointer.Down && logSide.Contains(pos))
        {
            if (CraftWoodwork.BarkLeft(p) <= 0.001) { said = "There is no bark left on it."; return; }
            knife.Clear();
            knife.Add(pos + new Vector2(0f, (float)Hands.Tremble(91) * 60f));
            knifeStart = Time.unscaledTime;
            return;
        }
        if (knife.Count == 0) return;
        if (phase == CraftPointer.Drag)
        {
            // The edge follows the hand, trembling: the depth wavers as it does.
            knife.Add(pos + new Vector2(0f, (float)Hands.Tremble(knife.Count + 91) * 60f));
            return;
        }
        if (phase != CraftPointer.Up) return;
        // A pull of the drawknife: right to left along the log.
        float minX = float.MaxValue, maxX = float.MinValue, mean = 0f, sq = 0f;
        foreach (Vector2 k in knife) { minX = Mathf.Min(minX, k.x); maxX = Mathf.Max(maxX, k.x); float d = KnifeDepth(k.y); mean += d; sq += d * d; }
        mean /= knife.Count;
        float sd = Mathf.Sqrt(Mathf.Max(0f, sq / knife.Count - mean * mean));
        bool pulled = knife[0].x > knife[knife.Count - 1].x;
        float seconds = Time.unscaledTime - knifeStart, speed = (maxX - minX) / Mathf.Max(0.05f, seconds);
        knife.Clear();
        if (maxX - minX < 20f) return;
        if (!pulled) { said = "A drawknife is pulled toward you: stroke from right to left."; return; }
        float steady = Mathf.Clamp01(1f - sd * 4f) * Mathf.Clamp01(1f - Mathf.Max(0f, speed - 700f) / 900f);
        int i0 = Stretch(p, minX), i1 = Stretch(p, maxX);
        // A face of a stretch takes two strokes at most: then turn the log.
        int fresh = 0;
        for (int i = i0; i <= i1; i++)
        {
            long key = Key(i, face);
            faceStrokes.TryGetValue(key, out int done);
            if (done < 2) fresh++;
            faceStrokes[key] = done + 1;
        }
        float round = 0.3f * fresh / Mathf.Max(1, i1 - i0 + 1) + 0.02f;
        Hands.Move(0.3);
        if (Send(new CraftRequest(CraftRequestKind.DebarkWood, p.id) { i0 = i0, i1 = i1, a = mean, b = steady, c = round }))
            said = fresh == 0 ? "That face is bare already: turn the log (wheel or right-click)."
                : mean > 0.62f ? "Too deep: the knife digs into the wood under the bark." : mean < 0.3f ? "Too shallow: only the outer bark comes off - set the edge at the bright band."
                : steady < 0.5f ? "The edge wavered: it skipped and dug in by turns." : p.wood.moisture > 0.3 ? "The bark peels off in a long strip." : "The tight, dry bark comes off in chips.";
    }

    // ------------------------------------------------------------------ splitting
    private void DrawSplit(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        c.Rect(200, 40, 520, 90, new Color(0.32f, 0.22f, 0.14f)); // the block
        if (p == null) return;
        Color wood = WoodColour(p), bark = Color.Lerp(wood, new Color(0.28f, 0.2f, 0.14f), 0.75f);
        endCentre = new Vector2(360f, 240f);
        float w = 0f;
        foreach (MetalSegment s in p.segments) w = Mathf.Max(w, (float)s.width);
        endRadius = Mathf.Clamp(w * 0.55f, 40f, 140f);
        int seed = CraftMath.Hash(p.id ?? "");
        // The end: bark ring, growth rings round the heart, rays out from it, a check along one ray.
        c.Disc(endCentre.x, endCentre.y, endRadius, (float)p.wood.bark > 0.3f ? bark : wood * 0.85f);
        c.Disc(endCentre.x, endCentre.y, endRadius - 5, Color.Lerp(wood, Color.white, 0.12f));
        for (int r = 1; r < 9; r++) c.Ring(endCentre.x, endCentre.y, (endRadius - 5) * r / 9f, 1f, wood * 0.82f);
        for (int k = 0; k < 12; k++)
        {
            float a = Ray(seed, k);
            c.Line(endCentre.x, endCentre.y, endCentre.x + Mathf.Cos(a) * (endRadius - 6), endCentre.y + Mathf.Sin(a) * (endRadius - 6), new Color(wood.r * 0.9f, wood.g * 0.85f, wood.b * 0.8f, 0.7f), 1f);
        }
        float check = Ray(seed, seed & 7);
        c.Line(endCentre.x, endCentre.y, endCentre.x + Mathf.Cos(check) * (endRadius - 8), endCentre.y + Mathf.Sin(check) * (endRadius - 8), new Color(0.08f, 0.05f, 0.03f), 2f);
        c.Disc(endCentre.x, endCentre.y, 3f, new Color(0.25f, 0.15f, 0.08f)); // the pith
        // The wedge's line, the crack opening along it as the maul drives it.
        if (lining || wedgeSet)
        {
            c.Line(lineA.x, lineA.y, lineB.x, lineB.y, wedgeSet ? Mark : new Color(1f, 1f, 1f, 0.6f), wedgeSet ? 2f : 1.5f);
            if (wedgeSet)
            {
                Vector2 mid = (lineA + lineB) * 0.5f;
                c.Rect(mid.x - 6, mid.y - 6, mid.x + 6, mid.y + 14, Iron); // the wedge
                Vector2 d = (lineB - lineA).normalized;
                float open = Mathf.Clamp01(splitWork) * endRadius;
                c.Line(mid.x - d.x * open, mid.y - d.y * open, mid.x + d.x * open, mid.y + d.y * open, new Color(0.05f, 0.03f, 0.02f), 3f);
                Meter(c, 30, 380, 260, 8, Mathf.Clamp01(splitWork), new Color(0.6f, 0.85f, 1f));
                if (maul.Raised) Meter(c, 30, 366, 260, 6, (float)maul.Charge, new Color(1f, 0.8f, 0.4f));
            }
        }
    }

    private static float Ray(int seed, int k) => (k * 30f + (seed % 30)) * Mathf.Deg2Rad;

    /// <summary>How far the line runs off the heart (0 through the pith - 1 at the bark), and how near it lies to a ray or the check (1 along one).</summary>
    private void LineQuality(CraftPiece p, out float off, out float along)
    {
        Vector2 d = (lineB - lineA).normalized;
        Vector2 n = new Vector2(-d.y, d.x);
        off = Mathf.Clamp01(Mathf.Abs(Vector2.Dot(endCentre - lineA, n)) / Mathf.Max(1f, endRadius));
        int seed = CraftMath.Hash(p.id ?? "");
        float angle = Mathf.Atan2(d.y, d.x), best = 999f;
        for (int k = 0; k < 12; k++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Ray(seed, k) * Mathf.Rad2Deg));
            diff = Mathf.Min(diff, 180f - diff);
            best = Mathf.Min(best, diff);
        }
        // Off the heart, a line crosses the rays whatever its angle.
        along = Mathf.Clamp01(1f - best / 15f) * (1f - off * 0.8f);
        float checkDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Ray(seed, seed & 7) * Mathf.Rad2Deg));
        if (Mathf.Min(checkDiff, 180f - checkDiff) < 6f && off < 0.15f) along = Mathf.Max(along, 0.95f);
    }

    private void SplitPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air) { if (phase == CraftPointer.Down) said = "Pick a log or a large half (left)."; return; }
        if (!wedgeSet)
        {
            if (phase == CraftPointer.Down && Vector2.Distance(pos, endCentre) <= endRadius + 10f) { lining = true; lineA = lineB = pos; return; }
            if (!lining) return;
            if (phase == CraftPointer.Drag) { lineB = pos; return; }
            if (phase != CraftPointer.Up) return;
            lining = false;
            lineB = pos;
            if (Vector2.Distance(lineA, lineB) < endRadius) { said = "Draw the line right across the end."; return; }
            wedgeSet = true;
            splitWork = splitSolid = 0f;
            splitBlows = 0;
            LineQuality(p, out float off, out float along);
            said = off < 0.12f && along > 0.8f ? "The wedge is set through the heart, along a ray. Now the maul: hold and release, full blows." : off >= 0.12f ? "The wedge is set off the heart: one half will be small. Drive it - or click away from the wedge to draw it again." : "Set across the rays: it may twist out. Drive it - or click away from the wedge to draw it again.";
            return;
        }
        if (phase == CraftPointer.Down)
        {
            if (Vector2.Distance(pos, (lineA + lineB) * 0.5f) > endRadius * 0.6f) { wedgeSet = false; said = "The wedge is lifted out: draw the line again."; return; }
            maul.Raise();
            return;
        }
        if (phase != CraftPointer.Up || !maul.Raised) return;
        Vector2 mid = (lineA + lineB) * 0.5f;
        CraftBlow b = maul.Release(0.5, 0, Hands);
        float square = Mathf.Clamp01(1f - Vector2.Distance(pos, mid) / 30f) * (b.slipped ? 0.2f : 1f);
        float tough = (float)(Rules.Wood(p.wood.wood)?.toughness ?? 0.6);
        float w = 0f;
        foreach (MetalSegment s in p.segments) w = Mathf.Max(w, (float)s.width);
        splitWork += (float)b.force * square * 0.5f / (0.4f + tough) / Mathf.Max(0.6f, w / 200f);
        splitSolid += (float)b.force * square;
        splitBlows++;
        said = b.slipped ? b.Words : square < 0.5f ? "The maul glances off the wedge's edge." : b.force < 0.5f ? "A light blow: the wedge hardly moves." : "The wedge sinks in; the crack runs.";
        if (splitWork < 1f) return;
        LineQuality(p, out float o, out float al);
        float solid = Mathf.Clamp01(splitSolid / splitBlows);
        if (Send(new CraftRequest(CraftRequestKind.SplitLog, p.id) { a = o, b = al, c = solid, i0 = splitBlows }))
            said = o < 0.12f && al > 0.8f ? "It splits open along the heart: two even halves." : o >= 0.12f ? "It splits off the heart: a big half and a small one." : "It splits, twisting out across the grain.";
        wedgeSet = false;
    }

    // ------------------------------------------------------------------ riving
    private void DrawRive(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        // The chopping block it lies on.
        c.Rect(30, 100, 690, 160, new Color(0.32f, 0.22f, 0.14f));
        if (p == null) return;
        billet = new Rect(60, 160, 600, 130);
        DrawBoard(c, p, billet);
        // The split so far, the froe at its front.
        for (int i = 1; i < splitPath.Count; i++)
            c.Line(splitPath[i - 1].x, splitPath[i - 1].y, splitPath[i].x, splitPath[i].y, new Color(0.05f, 0.03f, 0.02f), 3f);
        if (riveStage >= 1 && riveLine >= 0)
        {
            Vector2 f = splitPath.Count > 0 ? splitPath[splitPath.Count - 1] : new Vector2(billet.xMin, GrainY(p, billet, riveLine, 7, billet.xMin));
            c.Rect(f.x - 3, f.y - 30, f.x + 3, f.y + 30, Iron); // the froe's blade
            c.Rect(f.x - 4, f.y + 30, f.x + 4, f.y + 90, Wood); // its handle, up
            if (riveStage == 1)
            {
                Meter(c, billet.xMin, billet.yMax + 50, 120, 8, malletBlows / 2f, Mark);
                if (mallet.Raised) Meter(c, billet.xMin, billet.yMax + 62, 120, 6, (float)mallet.Charge, new Color(1f, 0.8f, 0.4f));
            }
            if (riveStage == 2) Meter(c, billet.xMin, billet.yMax + 50, billet.width, 6, (riveFront - billet.xMin) / billet.width, new Color(0.6f, 0.85f, 1f));
        }
        Vector2 h = ui.Hover;
        if (riveStage == 0 && h.x >= 0 && Mathf.Abs(h.x - billet.xMin) < 40f)
        {
            int k = NearestLine(p, h.y, billet.xMin);
            float y = GrainY(p, billet, k, 7, billet.xMin);
            c.Ring(billet.xMin, y, 8f, 2f, Mark);
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
        if (p == null || p.location != CraftLocation.Air) { if (phase == CraftPointer.Down) said = "Take it onto the block first."; return; }
        switch (riveStage)
        {
            case 0:
                // Set the froe on a grain line at the end of the billet.
                if (phase == CraftPointer.Down && Mathf.Abs(pos.x - billet.xMin) < 40f && pos.y > billet.yMin - 10 && pos.y < billet.yMax + 10)
                {
                    riveLine = NearestLine(p, pos.y, billet.xMin);
                    riveStage = 1;
                    malletBlows = 0;
                    said = "The froe is set on the grain. Drive it in with the mallet: hold to raise it, release to strike (two good blows).";
                }
                return;
            case 1:
                // Drive it in.
                if (phase == CraftPointer.Down) mallet.Raise();
                else if (phase == CraftPointer.Up && mallet.Raised)
                {
                    CraftBlow b = mallet.Release(0.5, 0, Hands);
                    if (b.force > 0.5 && !b.slipped) malletBlows++;
                    said = b.slipped ? b.Words : b.force > 0.5 ? "The froe bites into the end grain." : "Too light a blow.";
                    if (malletBlows >= 2)
                    {
                        riveStage = 2;
                        riveFront = billet.xMin;
                        riveStray = riveWeight = riveJerk = 0f;
                        splitPath.Clear();
                        splitPath.Add(new Vector2(billet.xMin, GrainY(p, billet, riveLine, 7, billet.xMin)));
                        riveLastX = billet.xMin;
                        riveLastT = Time.unscaledTime;
                        said = "It is in. Now lever the split along: hold and move right, keeping on the grain line (it wanders).";
                    }
                }
                return;
            default:
                if (phase != CraftPointer.Down && phase != CraftPointer.Drag)
                    return;
                if (pos.x <= riveFront)
                    return;
                float x = Mathf.Min(pos.x, billet.xMax);
                float now = Time.unscaledTime, dx = x - riveFront, dt = Mathf.Max(0.016f, now - riveLastT);
                float speed = dx / dt; // px/s
                float gy = GrainY(p, billet, riveLine, 7, x);
                float y = pos.y + (float)Hands.Tremble(splitPath.Count + 61) * 80f;
                // Off the line, the split cuts the fibres; where the line has run out of the side, the split runs out with it.
                float off = Mathf.Clamp01(Mathf.Abs(y - gy) / 28f);
                float outside = gy < billet.yMin + 3f || gy > billet.yMax - 3f ? 0.7f : 0f;
                float rushed = Mathf.Clamp01((speed - 450f) / 600f) * 0.5f;
                riveStray += Mathf.Max(off, Mathf.Max(outside, rushed)) * dx;
                riveWeight += dx;
                riveJerk += Mathf.Abs(speed - (riveLastX > billet.xMin ? (riveFront - riveLastX) / dt : speed)) * 0.0002f * dx;
                riveLastX = riveFront;
                riveFront = x;
                riveLastT = now;
                splitPath.Add(new Vector2(x, Mathf.Lerp(gy, y, Mathf.Clamp01(off))));
                if (riveFront >= billet.xMax - 1f)
                {
                    float stray = riveWeight > 0 ? riveStray / riveWeight : 1f;
                    float steady = Mathf.Clamp01(1f - riveJerk / Mathf.Max(1f, riveWeight) * 20f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 4f);
                    if (Send(new CraftRequest(CraftRequestKind.Rive, p.id) { a = stray, b = steady }))
                        said = stray < 0.15f ? "It splits cleanly along the grain: two straight-grained halves." : stray < 0.45f ? "It splits, but the split wandered across the fibres." : "The split ran out of the side: one half is short and tapered.";
                    Hands.Move(0.5);
                    riveStage = 0;
                    riveLine = -1;
                    splitPath.Clear();
                }
                return;
        }
    }

    // ------------------------------------------------------------------ sawing
    private void DrawSaw(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        // Two sawhorses.
        c.Rect(120, 60, 140, 180, Wood);
        c.Rect(560, 60, 580, 180, Wood);
        if (p == null) return;
        float mm = Mathf.Max(0.5f, (float)p.Length / 600f);
        float len = (float)p.Length / mm, wide = Mathf.Clamp((float)p.segments[0].width / mm, 30f, 140f);
        board = new Rect(60, 180, len, wide);
        DrawBoard(c, p, board);
        if (markX >= 0f)
        {
            for (float y = board.yMin - 6; y < board.yMax + 6; y += 6) c.Line(markX, y, markX, y + 3, new Color(0.1f, 0.1f, 0.12f), 1.5f);
            if (sawing)
            {
                float depth = Mathf.Clamp01(kerf) * board.height;
                c.Rect(sawStartX - 1.5f, board.yMax - depth, sawStartX + 1.5f, board.yMax, new Color(0.05f, 0.04f, 0.03f));
                Meter(c, board.xMin, board.yMax + 30, board.width, 6, Mathf.Clamp01(kerf), new Color(0.6f, 0.85f, 1f));
                for (int k = 0; k < Mathf.Min(30, saw.Shakes * 3); k++)
                    c.Disc(markX + (k * 7 % 11 - 5) * 3f, board.yMin - 8 - (k * 5 % 13), 1.2f, Color.Lerp(WoodColour(p), Color.white, 0.3f));
            }
        }
        Vector2 h = ui.Hover;
        if (h.x >= 0 && (board.Contains(h) || sawing))
        {
            // The saw: its blade where the hand is.
            c.Rect(h.x - 2, h.y - 40, h.x + 2, h.y + 60, new Color(0.7f, 0.72f, 0.76f));
            c.Rect(h.x - 8, h.y + 60, h.x + 8, h.y + 90, Wood);
        }
    }

    private void SawPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air) { if (phase == CraftPointer.Down) said = "Lay it on the sawhorses first (take it out)."; return; }
        if (!sawing)
        {
            if (phase == CraftPointer.Down && board.Contains(pos))
            {
                if (markX < 0f || Mathf.Abs(pos.x - markX) > 12f)
                {
                    markX = pos.x;
                    float at = (markX - board.xMin) / Mathf.Max(1f, board.width);
                    said = $"Marked: {at * p.Length:0} mm and {(1 - at) * p.Length:0} mm. Press on the mark and saw - stroke up and down, keep on the line.";
                }
                else
                {
                    sawing = true;
                    sawStartX = pos.x;
                    kerf = 0f;
                    wanderSum = 0f;
                    wanderN = 0;
                    saw.Clear();
                }
            }
            return;
        }
        if (phase == CraftPointer.Drag || phase == CraftPointer.Down)
        {
            saw.Add(pos.y, Time.unscaledTime);
            float x = pos.x + (float)Hands.Tremble(wanderN + 71) * 60f;
            wanderSum += Mathf.Abs(x - markX);
            wanderN++;
            float needed = 6f + (float)p.segments[0].thickness / 5f;
            kerf = saw.Shakes / needed * (0.5f + 0.5f * saw.Rhythm);
            if (wanderN % 15 == 0) Hands.Move(0.3);
            if (kerf >= 1f)
            {
                float wander = Mathf.Clamp01(wanderSum / Mathf.Max(1, wanderN) / 14f);
                float square = 1f - Mathf.Clamp01(Mathf.Abs(x - sawStartX) / 18f);
                float at = Mathf.Clamp01((sawStartX - board.xMin) / Mathf.Max(1f, board.width));
                if (Send(new CraftRequest(CraftRequestKind.SawCut, p.id) { a = at, b = wander, c = square }))
                    said = wander < 0.2f && square > 0.8f ? "Through, square and on the line." : wander < 0.5f ? "Through - the cut wandered a little: the end is ragged." : "Through, well off the line: a torn, crooked end.";
                sawing = false;
                markX = -1f;
            }
        }
        else if (phase == CraftPointer.Up && kerf < 1f)
            said = $"The saw stops {kerf * 100:0}% of the way through: press again on the mark and go on.";
    }

    // ------------------------------------------------------------------ steaming and bending
    private float FormR => FormRadius[Mathf.Clamp(form != null ? form.value : 1, 0, FormRadius.Length - 1)];

    private void DrawBend(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        steamBox = new Rect(30, 190, 220, 130);
        bench = new Rect(30, 60, 220, 60);
        // The steam box, its fire under it, steam leaking from the lid.
        c.Rect(steamBox.xMin, steamBox.yMin, steamBox.xMax, steamBox.yMax, new Color(0.35f, 0.25f, 0.16f));
        c.Rect(steamBox.xMin + 6, steamBox.yMin + 6, steamBox.xMax - 6, steamBox.yMax - 6, new Color(0.12f, 0.1f, 0.09f));
        for (int k = 0; k < 6; k++)
            c.Line(steamBox.xMin + 20 + k * 34, steamBox.yMax, steamBox.xMin + 24 + k * 34 + Mathf.Sin(Time.unscaledTime * 3f + k) * 5f, steamBox.yMax + 24, new Color(1f, 1f, 1f, 0.25f), 3f);
        c.Rect(steamBox.xMin + 60, steamBox.yMin - 20, steamBox.xMax - 60, steamBox.yMin, new Color(1f, 0.5f, 0.15f, 0.7f));
        // The form: a block with its curve, the stop the piece is clamped to.
        float r = Mathf.Clamp(FormR * 0.55f, 40f, 150f);
        formCentre = new Vector2(470f, 200f);
        formArea = new Rect(formCentre.x - r - 40, formCentre.y - r - 40, (r + 40) * 2 + 200, (r + 40) * 2);
        c.Disc(formCentre.x, formCentre.y, r, new Color(0.45f, 0.32f, 0.2f));
        c.Rect(formCentre.x - 4, formCentre.y + r, formCentre.x + 4, formCentre.y + r + 20, Iron);
        if (p == null) return;
        float plastic = (float)CraftWoodwork.Plasticity(p);
        Meter(c, steamBox.xMin, steamBox.yMin - 34, steamBox.width, 8, (float)p.wood.steamed, new Color(0.85f, 0.85f, 0.95f));
        Meter(c, steamBox.xMin, steamBox.yMin - 46, steamBox.width, 6, plastic, new Color(1f, 0.6f, 0.3f));
        Color wood = WoodColour(p);
        float thick = Mathf.Clamp((float)CraftWoodwork.Thickness(p) * 0.6f, 4f, 16f);
        if (p.location == CraftLocation.Steam && !dragging)
            c.Rect(steamBox.xMin + 14, steamBox.center.y - thick * 0.5f, steamBox.xMax - 14, steamBox.center.y + thick * 0.5f, Color.Lerp(wood, new Color(0.3f, 0.2f, 0.12f), 0.3f));
        else if (dragging)
            c.Rect(dragAt.x - 80, dragAt.y - thick * 0.5f, dragAt.x + 80, dragAt.y + thick * 0.5f, wood);
        else if (p.location == CraftLocation.Air)
        {
            // On the form: clamped at the top, the free end following the bend.
            float total = Mathf.Min(360f, (float)p.Length * 0.5f);
            float arc = bendAngle * Mathf.Deg2Rad * (r + thick * 0.5f);
            float rest = Mathf.Max(0f, total - arc);
            var top = new Vector2(formCentre.x, formCentre.y + r + thick * 0.5f);
            c.Rect(top.x - 60, top.y - thick * 0.5f, top.x, top.y + thick * 0.5f, wood); // clamped behind the stop
            Vector2 last = top;
            int steps = Mathf.Max(1, Mathf.CeilToInt(bendAngle / 4f));
            for (int k = 1; k <= steps; k++)
            {
                float a = 90f - bendAngle * k / steps;
                var q = new Vector2(formCentre.x + Mathf.Cos(a * Mathf.Deg2Rad) * (r + thick * 0.5f), formCentre.y + Mathf.Sin(a * Mathf.Deg2Rad) * (r + thick * 0.5f));
                c.Line(last.x, last.y, q.x, q.y, wood, thick);
                last = q;
            }
            float endA = (90f - bendAngle) * Mathf.Deg2Rad;
            var tangent = new Vector2(Mathf.Sin(endA), -Mathf.Cos(endA));
            Vector2 tip = last + tangent * rest;
            c.Line(last.x, last.y, tip.x, tip.y, wood, thick);
            // The strap rides on the outside of the bend, from the clamp to the free end.
            if (strap)
            {
                float rs = r + thick + 2f;
                var sl = new Vector2(top.x - 60, top.y + thick * 0.5f + 2f);
                var s0 = new Vector2(top.x, formCentre.y + rs);
                c.Line(sl.x, sl.y, s0.x, s0.y, Iron, 1.5f);
                for (int k = 1; k <= steps; k++)
                {
                    float a = 90f - bendAngle * k / steps;
                    var q = new Vector2(formCentre.x + Mathf.Cos(a * Mathf.Deg2Rad) * rs, formCentre.y + Mathf.Sin(a * Mathf.Deg2Rad) * rs);
                    c.Line(s0.x, s0.y, q.x, q.y, Iron, 1.5f);
                    s0 = q;
                }
                Vector2 st = s0 + tangent * rest;
                c.Line(s0.x, s0.y, st.x, st.y, Iron, 1.5f);
            }
            clamp = new Rect(top.x - 70f, top.y - 20f, 70f, 40f);
            c.Rect(top.x - 30f, top.y + thick * 0.5f, top.x - 20f, top.y + thick * 0.5f + 14f, Iron); // the clamp
            c.Disc(tip.x, tip.y, 6f, bending ? Mark : new Color(1f, 1f, 1f, 0.4f));
            // The strain: how near the outside fibres are to tearing.
            float tear = (float)CraftWoodwork.BendTear(Rules, p, BendLength(p), Mathf.Max(1f, bendAngle), Speed, strap, out _);
            float strain = bendAngle <= 0f ? 0f : Mathf.Clamp01(1f - Mathf.Exp(-(tear + 0.4f) * 1.2f));
            Meter(c, formArea.xMin, 40, 220, 10, strain, tear > 0f ? new Color(1f, 0.35f, 0.25f) : strain > 0.6f ? Mark : new Color(0.5f, 0.85f, 0.55f));
        }
    }

    private float Speed => Mathf.Clamp01(bendPeak / 90f); // degrees per second, 90 is forcing it

    /// <summary>The length of the piece bent round the form for the angle (its radius × the angle), in mm.</summary>
    private float BendLength(CraftPiece p) => Mathf.Min((float)p.Length * 0.8f, FormR * Mathf.Max(1f, bendAngle) * Mathf.Deg2Rad);

    private void BendPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (p == null) return;
        if (phase == CraftPointer.Down)
        {
            if (p.location == CraftLocation.Air && clamp.Contains(pos) || p.location == CraftLocation.Steam && steamBox.Contains(pos))
            {
                dragging = true;
                dragAt = pos;
                return;
            }
            if (p.location == CraftLocation.Air && formArea.Contains(pos))
            {
                bending = true;
                bendAngle = bendPeak = 0f;
                lastAngle = 0f;
                lastAngleTime = Time.unscaledTime;
            }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (dragging) dragAt = pos;
            if (bending)
            {
                // The free end follows the hand round the form: the angle swept from the top.
                Vector2 d = pos - formCentre;
                float a = Mathf.Clamp(90f - Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f, 170f);
                a = Mathf.Clamp(a + (float)Hands.Tremble(Mathf.RoundToInt(a) + 81) * 30f, 0f, 170f);
                float now = Time.unscaledTime, dt = Mathf.Max(0.016f, now - lastAngleTime);
                if (a > bendAngle) bendPeak = Mathf.Max(bendPeak * 0.97f, (a - lastAngle) / dt);
                bendAngle = Mathf.Max(bendAngle, a);
                lastAngle = a;
                lastAngleTime = now;
            }
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        if (dragging)
        {
            dragging = false;
            if (p.location == CraftLocation.Air && steamBox.Contains(pos) && Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Steam }))
                said = $"Into the steam. About {CraftWoodwork.Thickness(p) / 25.0:0.#} hours for its thickness - the white gauge fills; then bend it at once.";
            else if (p.location == CraftLocation.Steam && !steamBox.Contains(pos) && Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air }))
                said = "Out of the steam, hot and limp: bend it now, before it cools (the orange gauge).";
            return;
        }
        if (bending)
        {
            bending = false;
            if (bendAngle < 3f) { bendAngle = 0f; return; }
            int n = p.segments.Count;
            float lenFrac = BendLength(p) / Mathf.Max(1f, (float)p.Length);
            float centre = bendAt != null ? bendAt.value : 0.5f;
            int i0 = Mathf.Clamp(Mathf.FloorToInt((centre - lenFrac * 0.5f) * n), 0, n - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((centre + lenFrac * 0.5f) * n) - 1, i0, n - 1);
            float tear = (float)CraftWoodwork.BendTear(Rules, p, BendLength(p), bendAngle, Speed, strap, out double kept);
            if (Send(new CraftRequest(CraftRequestKind.BendWood, p.id) { i0 = i0, i1 = i1, a = bendAngle, b = Speed, flag = strap }))
                said = tear > 0f ? (CraftWoodwork.Plasticity(p) < 0.3 ? "Crack! It was too cold and stiff: steam it through first." : Speed > 0.6f ? "Crack! Forced round too fast." : "It creaks and the outside tears: too tight for it.") + $" It keeps {kept:0}°."
                    : $"It takes the bend and springs back a little: {kept:0}° kept. Let it cool and dry on the form.";
            Hands.Move(0.6);
            bendAngle = 0f;
        }
    }

    // ------------------------------------------------------------------ drying
    private void DrawDry(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        trestle = new Rect(30, 220, 330, 30);
        stackArea = new Rect(420, 60, 280, 230);
        // The shed: its roof, the stack's base, and the cover pulled over it as far as the handle.
        c.Rect(stackArea.xMin - 10, stackArea.yMax + 30, stackArea.xMax + 10, stackArea.yMax + 40, Wood);
        c.Rect(stackArea.xMin, stackArea.yMin - 10, stackArea.xMax, stackArea.yMin, new Color(0.25f, 0.25f, 0.27f));
        float coverX = stackArea.xMin + stackArea.width * cover;
        c.Rect(stackArea.xMin, stackArea.yMax + 20, coverX, stackArea.yMax + 30, new Color(0.45f, 0.5f, 0.35f));
        coverHandle = new Rect(coverX - 8, stackArea.yMax + 14, 16, 22);
        c.Rect(coverHandle.xMin, coverHandle.yMin, coverHandle.xMax, coverHandle.yMax, holding == 3 ? Mark : new Color(0.7f, 0.6f, 0.4f));
        // The sun where the cover does not reach.
        if (cover < 0.95f) c.Halo(stackArea.xMax - 20, stackArea.yMax + 70, 30, new Color(1f, 0.9f, 0.5f), 0.6f * (1 - cover));
        // What is in the stack already, with how dry each is.
        int row = 0;
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Wood && q.location == CraftLocation.Stack && q != (dragging ? p : null))
            {
                var r = new Rect(stackArea.xMin + 10, stackArea.yMin + 10 + row * 34, stackArea.width - 20, 18);
                if (r.yMax > stackArea.yMax) break;
                DrawBoard(c, q, r, false);
                for (int k = 0; k < 4; k++) c.Rect(r.xMin + 10 + k * (r.width - 30) / 3f, r.yMin - 6, r.xMin + 18 + k * (r.width - 30) / 3f, r.yMin, new Color(0.25f, 0.18f, 0.1f));
                Meter(c, r.xMin, r.yMax + 2, r.width, 4, 1f - Mathf.Clamp01(((float)q.wood.moisture - (float)CraftWoodwork.Seasoned) / 0.25f), new Color(0.9f, 0.8f, 0.55f));
                if (q == p) c.Frame(r.xMin - 2, r.yMin - 2, r.xMax + 2, r.yMax + 2, Mark, 1.5f);
                row++;
            }
        c.Rect(trestle.xMin, trestle.yMin - 40, trestle.xMin + 10, trestle.yMin, Wood);
        c.Rect(trestle.xMax - 10, trestle.yMin - 40, trestle.xMax, trestle.yMin, Wood);
        if (p == null || p.location == CraftLocation.Stack && !dragging) return;
        // The board on the trestles: the stickers laid under it, its ends being sealed.
        Rect b = dragging ? new Rect(dragAt.x - 120, dragAt.y - 12, 240, 24) : trestle;
        DrawBoard(c, p, b);
        if (!dragging)
        {
            foreach (float s in stickers) c.Rect(s - 4, b.yMin - 8, s + 4, b.yMin, new Color(0.25f, 0.18f, 0.1f));
            leftEnd = new Rect(b.xMin - 6, b.yMin - 6, 22, b.height + 12);
            rightEnd = new Rect(b.xMax - 16, b.yMin - 6, 22, b.height + 12);
            c.Rect(b.xMin, b.yMin, b.xMin + 6, b.yMax, new Color(0.9f, 0.82f, 0.5f, sealLeft));
            c.Rect(b.xMax - 6, b.yMin, b.xMax, b.yMax, new Color(0.9f, 0.82f, 0.5f, sealRight));
            Meter(c, trestle.xMin, trestle.yMin - 60, trestle.width, 6, Evenness(), new Color(0.6f, 0.85f, 1f));
        }
    }

    /// <summary>How evenly the stickers are laid (0-1): enough of them, with even gaps between them and the ends.</summary>
    private float Evenness()
    {
        if (stickers.Count < 2) return 0f;
        var xs = new List<float>(stickers);
        xs.Sort();
        var gaps = new List<float> { xs[0] - trestle.xMin };
        for (int i = 1; i < xs.Count; i++) gaps.Add(xs[i] - xs[i - 1]);
        gaps.Add(trestle.xMax - xs[xs.Count - 1]);
        float mean = 0f;
        foreach (float g in gaps) mean += g;
        mean /= gaps.Count;
        float v = 0f;
        foreach (float g in gaps) v += (g - mean) * (g - mean);
        float cv = Mathf.Sqrt(v / gaps.Count) / Mathf.Max(1f, mean);
        float enough = Mathf.Clamp01((xs.Count - 1) / 3f);
        return Mathf.Clamp01(1f - cv * 1.5f) * enough;
    }

    private void DryPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = WoodPiece;
        if (phase == CraftPointer.Down)
        {
            if (coverHandle.Contains(pos)) { holding = 3; return; }
            if (p == null) return;
            if (p.location == CraftLocation.Stack && stackArea.Contains(pos)) { dragging = true; dragAt = pos; return; }
            if (p.location != CraftLocation.Air) return;
            if (leftEnd.Contains(pos)) { holding = 1; return; }
            if (rightEnd.Contains(pos)) { holding = 2; return; }
            if (trestle.Contains(pos))
            {
                // A click on the board lays a sticker there (a click on one takes it up); a drag carries the board.
                for (int i = 0; i < stickers.Count; i++)
                    if (Mathf.Abs(stickers[i] - pos.x) < 8f) { stickers.RemoveAt(i); return; }
                if (stickers.Count < 8 && pos.y < trestle.center.y) { stickers.Add(pos.x); return; }
                dragging = true;
                dragAt = pos;
            }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (holding == 3) cover = Mathf.Clamp01((pos.x - stackArea.xMin) / stackArea.width);
            if (dragging) dragAt = pos;
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        holding = 0;
        if (!dragging || p == null)
            return;
        dragging = false;
        if (p.location == CraftLocation.Air && stackArea.Contains(pos))
        {
            float even = Evenness(), sealed_ = (sealLeft + sealRight) * 0.5f;
            if (Send(new CraftRequest(CraftRequestKind.StackWood, p.id) { a = even, b = sealed_, c = cover }))
            {
                said = even < 0.5f ? "Stacked on uneven stickers: it will dry unevenly and warp." : sealed_ < 0.5f ? "Stacked - its ends are bare: they will check as it dries." : cover < 0.5f ? "Stacked in the sun: it will dry fast, and check." : "Stacked well: shaded, sealed, on even stickers. Now it seasons slowly.";
                stickers.Clear();
                sealLeft = sealRight = 0f;
            }
        }
        else if (p.location == CraftLocation.Stack && !stackArea.Contains(pos) && Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air }))
            said = p.wood.moisture > 0.18 ? "Taken off the stack still damp." : "Taken off the stack, seasoned.";
    }

    // ------------------------------------------------------------------ sanding and finishing
    private void DrawFinish(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        pot = new Rect(600, 30, 90, 70);
        c.Rect(30, 120, 690, 140, Wood);
        if (finishTool == FinishTool.Brush)
        {
            CraftSupplyDefinition s = finishes.Count > 0 ? Rules.Supply(finishes[Mathf.Clamp(finishSupply.value, 0, finishes.Count - 1)]) : null;
            c.Rect(pot.xMin, pot.yMin, pot.xMax, pot.yMax, Iron);
            c.Rect(pot.xMin + 6, pot.yMax - 18, pot.xMax - 6, pot.yMax - 6, s != null && s.kind == CraftSupplyKind.Oil ? new Color(0.75f, 0.6f, 0.25f) : new Color(0.6f, 0.38f, 0.15f));
            Meter(c, pot.xMin, pot.yMin - 12, pot.width, 6, load, Mark);
        }
        if (p == null) return;
        board = new Rect(40, 150, 640, 110);
        DrawBoard(c, p, board);
        DrawRough(c, p, board);
        for (int i = 1; i < stroke.Count; i++)
            c.Line(stroke[i - 1].x, stroke[i - 1].y, stroke[i].x, stroke[i].y, finishTool == FinishTool.Brush ? new Color(0.8f, 0.55f, 0.25f, 0.6f) : new Color(1f, 1f, 1f, 0.35f), finishTool == FinishTool.Brush ? 8f : 4f);
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
        if (finishTool == FinishTool.Brush && phase == CraftPointer.Down && pot.Contains(pos))
        {
            load = 1f;
            said = "The brush is loaded.";
            return;
        }
        if (p == null) return;
        if (phase == CraftPointer.Down && board.Contains(pos))
        {
            stroke.Clear();
            stroke.Add(pos);
            strokeStart = Time.unscaledTime;
            return;
        }
        if (stroke.Count == 0) return;
        if (phase == CraftPointer.Drag)
        {
            stroke.Add(pos);
            return;
        }
        if (phase != CraftPointer.Up)
            return;
        // What the stroke did: the span along the board, how much along the grain, how many passes, how steady.
        float minX = float.MaxValue, maxX = float.MinValue, along = 0f, across = 0f, wobble = 0f;
        int passes = 1, dir = 0;
        for (int i = 0; i < stroke.Count; i++)
        {
            minX = Mathf.Min(minX, stroke[i].x);
            maxX = Mathf.Max(maxX, stroke[i].x);
            if (i == 0) continue;
            float dx = stroke[i].x - stroke[i - 1].x, dy = stroke[i].y - stroke[i - 1].y;
            along += Mathf.Abs(dx);
            across += Mathf.Abs(dy);
            wobble += Mathf.Abs(dy);
            int s = Math.Sign(dx);
            if (Mathf.Abs(dx) > 2f && s != 0) { if (dir != 0 && s != dir) passes++; dir = s; }
        }
        int n = p.segments.Count;
        int i0 = Mathf.Clamp(Mathf.FloorToInt((minX - board.xMin) / board.width * n), 0, n - 1), i1 = Mathf.Clamp(Mathf.FloorToInt((maxX - board.xMin) / board.width * n), i0, n - 1);
        float alongGrain = along + across > 1f ? along / (along + across) : 1f;
        stroke.Clear();
        if (maxX - minX < 8f) return;
        if (finishTool != FinishTool.Brush)
        {
            float grit = Grits[(int)finishTool];
            bool skipped = p.wood.lastGrit >= 0 && grit - p.wood.lastGrit > 0.4f;
            Hands.Move(0.25);
            if (Send(new CraftRequest(CraftRequestKind.SandWood, p.id) { i0 = i0, i1 = i1, a = grit, b = passes * 1.5f, c = alongGrain }))
                said = alongGrain < 0.7f ? "Across the grain: it leaves scratches. Sand along it." : skipped ? "Straight from coarse to fine: the coarse scratches stay under it. Use the medium paper between."
                    : finishTool == FinishTool.Coarse ? "The coarse paper takes the tool marks off. Then the medium." : finishTool == FinishTool.Medium ? "The medium paper takes out the coarse scratches. Then the fine." : "The fine paper leaves it smooth.";
            return;
        }
        if (finishes.Count == 0) return;
        if (load <= 0.05f) { said = "The brush is dry: dip it in the pot."; return; }
        // A loaded brush on a short stroke lays it thick; a long, straight stroke lays it thin and even.
        float spread = (maxX - minX) / board.width;
        float thick = Mathf.Clamp01(load * (1.1f - 0.8f * spread));
        float even = Mathf.Clamp01(alongGrain * (1f - wobble / Mathf.Max(1f, along) * 2f)) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
        load = Mathf.Max(0f, load - 0.35f - 0.4f * spread);
        string id = finishes[Mathf.Clamp(finishSupply.value, 0, finishes.Count - 1)];
        if (Send(new CraftRequest(CraftRequestKind.Varnish, p.id) { id = id, i0 = i0, i1 = i1, a = even, b = thick }))
            said = p.wood.moisture > 0.2 ? "On damp wood it clouds: season it first." : thick > 0.75f ? "Laid on thick: it runs and sags." : even < 0.5f ? "A patchy, wavering coat." : "A thin, even coat. Let it dry, sand lightly with the fine paper, and lay another.";
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (place)
        {
            case Place.Debark: DebarkPointer(phase, pos, right, scroll); break;
            case Place.Split: SplitPointer(phase, pos); break;
            case Place.Rive: RivePointer(phase, pos); break;
            case Place.Saw: SawPointer(phase, pos); break;
            case Place.Bend: BendPointer(phase, pos); break;
            case Place.Dry: DryPointer(phase, pos); break;
            default: FinishPointer(phase, pos); break;
        }
    }

    public override void Refresh()
    {
        RefreshCounts(finishSupply, finishes);
        if (info == null) return;
        CraftPiece p = WoodPiece;
        string how;
        switch (place)
        {
            case Place.Debark:
                how = p == null ? "<b>Debark</b>: pick a log (left)." : CraftWoodwork.BarkLeft(p) <= 0.001 ? "<b>Debarked</b>: split it, rive it or season it."
                    : $"<b>Debark</b>: pull the drawknife right to left along the log, its edge in the bright band (the cambium, green on the gauge): blue skims, red gouges. Turn the log for its other faces (wheel or right-click). {CraftWoodwork.BarkLeft(p) * 100:0}% of the bark left.";
                break;
            case Place.Split:
                how = !wedgeSet ? "<b>Split</b>: drag a line right across the log's end where the wedge goes - through the heart (the dark dot) and along a ray or the check splits it evenly and straight."
                    : $"Drive the wedge: hold to raise the maul, release on the wedge (full, square blows). {Mathf.Clamp01(splitWork) * 100:0}% split. Click away from the wedge to lift it out.";
                break;
            case Place.Rive:
                how = riveStage == 0 ? "<b>Rive</b>: click on a grain line at the left end of the billet to set the froe on it (a line near the middle, that stays in the wood)."
                    : riveStage == 1 ? $"Drive the froe in with the mallet: hold and release ({malletBlows}/2 good blows)."
                    : "Lever the split along: hold and move right, staying on the highlighted grain line. Not too fast.";
                break;
            case Place.Saw:
                how = markX < 0f ? "<b>Saw</b>: click on the board to mark where to cut." : sawing ? $"Stroke up and down, keep the blade on the mark ({kerf * 100:0}% through)." : "Press on the mark to start sawing (or click elsewhere to mark again).";
                break;
            case Place.Bend:
                how = p == null ? "Pick a piece of wood." : p.location == CraftLocation.Steam ? $"Steaming: {p.wood.steamed * 100:0}% through (white gauge). When it is through, drag it out and bend it at once."
                    : $"<b>Steam &amp; bend</b>: drag it by its clamped end (by the clamp) into the steam box; out and hot (orange gauge), drag its free end round the form steadily. The gauge over the form is the strain: keep it out of the red. {(strap ? "The strap holds the outside." : "No strap: it tears sooner.")}";
                break;
            case Place.Dry:
            {
                // The air seasons wood only as dry as it is itself: damp air leaves it damp.
                double air = CraftWoodwork.Equilibrium(Ws.Humidity);
                bool meter = Station.HasInstrument(CraftInstruments.MoistureMeter);
                string wet = p == null ? "" : meter ? $"the moisture meter reads {p.wood.moisture * 100:0}%" : p.wood.moisture > 0.3 ? "it is heavy and cold with water" : p.wood.moisture > 0.18 ? "it still feels cool and damp" : "it feels dry and light";
                how = p != null && p.location == CraftLocation.Stack ? $"In the stack: {wet}{(meter ? $" (seasoned is about {CraftWoodwork.Seasoned * 100:0}%; this air dries it to about {air * 100:0}%)" : "")}. Drag it off the stack to take it down."
                    : $"<b>Dry</b>: click under the board to lay stickers, evenly (blue gauge); hold on each end to seal it with wax; drag the green cover over the stack; then drag the board onto the stack.{(p != null ? $" Now {wet}." : "")}";
                break;
            }
            default:
            {
                CraftSupplyDefinition s = finishes.Count > 0 ? Rules.Supply(finishes[Mathf.Clamp(finishSupply.value, 0, finishes.Count - 1)]) : null;
                bool wax = s != null && s.kind == CraftSupplyKind.Wax;
                how = finishTool == FinishTool.Brush ? (wax ? "<b>Wax</b>: click the pot to take up wax, then rub long strokes along the board: it sheds water but wears away." : "<b>Seal</b>: click the pot to load the brush, then lay long strokes along the board. Thin coats, dry between them (a resin sealer builds the hardest film; oil soaks in).")
                    : $"<b>Sand</b>: stroke back and forth along the grain (left-right) - coarse, then medium, then fine: each paper only takes out the last one's scratches.{(p != null && p.wood.lastGrit >= 0 ? $" Finest so far: {(p.wood.lastGrit > 0.8 ? "fine" : p.wood.lastGrit > 0.45 ? "medium" : "coarse")}." : "")}";
                break;
            }
        }
        info.text = (how + " " + said).Trim();
    }
}
