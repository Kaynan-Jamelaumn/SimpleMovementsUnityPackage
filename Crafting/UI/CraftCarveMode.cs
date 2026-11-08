using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Carving wood - a grip, a haft, a scabbard core - with the knife at the bench (it needs the files and knives:
/// <see cref="CraftTools.Files"/>). Choose what it is for (the design and its wooden part): the shape the part is
/// judged by - its length range and its width and thickness along it - is dashed in green over the blank, seen from
/// above (its width) and from the side (its thickness), each view to its own scale; the section under the pointer is
/// drawn beside them, what it has against what it wants.
/// <para>The tab says what the knife takes: <b>Shave the width</b> works on the view from above, <b>Shave the
/// thickness</b> on the view from the side (the other view is dimmed), <b>Round it</b> on either - drag the knife along
/// where wood should come off. The knife's setting (the wheel turns it too) and the stroke's pace set how deep it bites:
/// past the depth the workshop splits it at (the info says both), or with a shaky stroke, it splits along the grain.
/// <b>Bore the slot</b>: press near the end the tang goes into and draw circles from there, round and even (they are
/// counted round their own middle, wherever they are drawn; uneven ones split the wood): each turn bores the slot wider
/// and deeper from that end than the slot already goes (bored here or at the bench), toward the size the tang needs
/// (drawn on the end). <b>Saw to length</b>: click where to cut,
/// press on the mark and saw - long strokes up and down across it, keeping on the line (the woodshop's saw, its rule:
/// the strokes it takes through that thickness, how far off the line it ran, how square). A haft's end that goes into
/// a head's eye is marked, with the eye's size.</para>
/// <para>A knife stroke is steady as far as it keeps to its own line (a straight stroke down a taper is steady). Right-click
/// lifts the knife (that stroke takes nothing) or the saw (the kerf stays; set back on the mark, it goes on in its kerf). A
/// mark made on a piece cut since (elsewhere) is gone. Sand it, oil, wax or varnish it at the woodshop's Finish.</para>
/// </summary>
public sealed class CraftCarveMode : CraftMode
{
    private enum Cut { Width, Thickness, Round, Bore, Saw }
    private static readonly string[] CutNames = { "Shave the width", "Shave the thickness", "Round it", "Bore the slot", "Saw to length" };

    /// <summary>
    /// The work under way on one piece (by its id), kept while another piece or area is picked: the saw's mark and kerf,
    /// the end the slot is bored from. Dropped once the piece has been cut since (elsewhere, by other hands): the mark is a
    /// share of the length it had.
    /// </summary>
    private sealed class PieceWork
    {
        /// <summary>The piece's length (mm) and stretches when the work was begun.</summary>
        public double length;
        public int stretches;
        public float mark = -1f;
        public readonly CraftSawStroke saw = new CraftSawStroke();
        public bool sawStarted, sawSent;
        /// <summary>The end the slot is bored from (-1 none yet, 0 the near end, 1 the far end), and the turns of the bit sent and not yet answered.</summary>
        public int boreEnd = -1, boreAhead;
    }

    private TMP_Dropdown purpose;
    private Slider pressure;
    private TextMeshProUGUI info;
    private readonly List<Button> cutButtons = new List<Button>();
    private readonly List<(string design, string role)> purposes = new List<(string, string)>();
    private readonly Dictionary<string, PieceWork> works = new Dictionary<string, PieceWork>();
    private Cut cut = Cut.Width;
    private bool intentPending;

    // the drawing: where the views are and their scales (mm per pixel along, across from above, across from the side)
    private const float X0 = 40f, ViewLength = 520f;
    private Rect topView = new Rect(X0, 220, ViewLength, 150), sideView = new Rect(X0, 40, ViewLength, 150);
    private readonly Rect inset = new Rect(592, 152, 116, 116);
    private float mmX = 1f, mmTop = 1f, mmSide = 1f;

    // the hands
    private readonly List<Vector2> path = new List<Vector2>();
    private bool carving, boring, sawing, ignorePress;
    private float strokeStart, strokeEnd, pressX;
    /// <summary>The circles the hand draws turning the bit, counted round their own middle wherever they are drawn.</summary>
    private readonly CraftHandCircles bore = new CraftHandCircles();
    private float shavings;
    private Vector2 shavingsAt;
    private string said = "";

    public override string Title => "Carve";
    public override CraftDiscipline Discipline => CraftDiscipline.Woodwork;
    // Every knife stroke and the bench saw need the knives and files: the woodshop's carving bench, or a smith's files.
    public override bool Available(CraftStation s) => s.Has(CraftTools.Files);
    public override string Hint => "Shave it to the dashed shape (the tab says width, thickness or round), bore the slot from the end, saw it to length. Right-click lifts the tool." + ByHandHint("carve it by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Carve;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.pressure = pressure != null ? pressure.value : 0.5f;
        setup.cut = (int)cut; // the saw included: it comes back as it was unless a cut is picked by hand
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        if (setup.cut >= 0 && setup.cut <= (int)Cut.Saw) cut = (Cut)setup.cut;
        if (setup.pressure > 0f) SetSlider(pressure, Mathf.Clamp(setup.pressure, 0.1f, 1f));
        ShowCut();
    }

    // ------------------------------------------------------------------ building
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
        purpose = Drop(r1, names, 0, 250f);
        purpose.onValueChanged.AddListener(OnPurpose);
        pressure = Slide(r1, "Knife", 0.1f, 1f, 0.5f, false, 44f, v => $"{v * 100:0}%");
        HandsOnButton(r1, "Carve by hand", 150f);
        RectTransform r2 = Row(controls, 30f, 4f);
        cutButtons.Clear();
        for (int i = 0; i < CutNames.Length; i++)
        {
            Cut c = (Cut)i;
            cutButtons.Add(Btn(r2, CutNames[i], () => PickCut(c), 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        info = InfoRow(controls);
        ShowCut();
        SyncPurpose();
    }

    /// <summary>Goes to a cut by the guide's name for it ("Saw": saw to length).</summary>
    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Cut c)) PickCut(c);
    }

    public override bool HasPlace(string place) => Enum.TryParse(place, out Cut _);

    public override string PlaceTitle(string place) => Enum.TryParse(place, out Cut c) ? CutNames[(int)c] : place;

    private void PickCut(Cut c)
    {
        if (c == cut) return;
        EndGestures(true);
        cut = c;
        said = "";
        ShowCut();
    }

    private void ShowCut()
    {
        for (int i = 0; i < cutButtons.Count; i++) UITheme.SetSelected(cutButtons[i], (Cut)i == cut);
    }

    /// <summary>The menu shows what the piece picked is for (nothing is sent).</summary>
    private void SyncPurpose()
    {
        if (purpose == null) return;
        CraftPiece p = WoodPiece;
        int k = p != null && !string.IsNullOrEmpty(p.intentDesign) ? purposes.FindIndex(x => x.design == p.intentDesign && x.role == p.intentRole) : -1;
        if (purpose.value != k + 1) purpose.SetValueWithoutNotify(k + 1);
    }

    private void OnPurpose(int v)
    {
        CraftPiece p = WoodPiece;
        if (p == null)
        {
            said = "Pick a wooden blank first.";
            SyncPurpose();
            return;
        }
        bool some = v > 0 && v <= purposes.Count;
        intentPending = true;
        // "(what is it for?)" clears it: the blank is for nothing yet
        Send(new CraftRequest(CraftRequestKind.SetIntent, p.id) { id = some ? purposes[v - 1].design : "", other = some ? purposes[v - 1].role : "" }, (ok, why) =>
        {
            intentPending = false;
            if (!ok) SyncPurpose();
        });
    }

    // ------------------------------------------------------------------ the piece, its work, leaving
    /// <summary>Where wood that is not lying on the bench is taken out to carve it here (the steam box, the drying stack: the Woodshop's).</summary>
    private static string OffTheBench(CraftPiece p) =>
        p.location == CraftLocation.Steam ? "It is in the steam box: drag it out at the Woodshop (Steam & bend) to carve it here."
        : p.location == CraftLocation.Stack ? "It is seasoning in the stack: drag it off at the Woodshop (Dry) to carve it here."
        : $"It is {CraftStationUI.LocationText(p)}: take it out onto the bench to carve it.";

    private CraftPiece WoodPiece => Selected != null && Selected.kind == CraftPieceKind.Wood && Selected.segments.Count > 0 ? Selected : null;

    private PieceWork Work(CraftPiece p)
    {
        if (p == null) return null;
        if (works.TryGetValue(p.id, out PieceWork k) && Math.Abs(k.length - p.Length) <= 0.5 && k.stretches == p.segments.Count)
            return k;
        if (k != null && k.sawSent)
            return k; // its own cut on the way: the answer clears the work
        if (k != null && (k.mark >= 0f || k.sawStarted))
            said = "It has been cut since it was marked: mark it again.";
        works[p.id] = k = new PieceWork { length = p.Length, stretches = p.segments.Count };
        return k;
    }

    public override string Busy
    {
        get
        {
            CraftPiece p = WoodPiece;
            PieceWork k = p != null && works.ContainsKey(p.id) ? Work(p) : null; // (a cut made since drops it)
            return k != null && k.sawStarted && k.saw.Progress > 0 && k.saw.Progress < 1 ? "sawing it to length" : null;
        }
    }

    public override void Leave() => EndGestures(true);

    public override void SelectionChanging(string from, string to)
    {
        EndGestures(true); // a knife stroke under way goes to the piece it was made on
        said = "";
    }

    public override void SelectionChanged(string to)
    {
        SyncPurpose();
        if (Station == null || Station.Workshop == null || works.Count == 0) return;
        var gone = new List<string>();
        foreach (string id in works.Keys)
            if (Ws.Piece(id) == null) gone.Add(id);
        foreach (string id in gone) works.Remove(id);
    }

    /// <summary>The hands put down what they hold: a knife stroke already drawn is made (when <paramref name="finish"/>); the saw rests in its kerf; a part-turn of the bit is lost.</summary>
    private void EndGestures(bool finish)
    {
        if (finish && carving) Stroke(WoodPiece);
        carving = false;
        path.Clear();
        boring = false;
        bore.Clear();
        if (sawing) StopSaw();
        ignorePress = ui != null && ui.PointerHeld;
        LiveDraw = false;
    }

    public override void Tick(float dt)
    {
        shavings = Mathf.Max(0f, shavings - dt * 1.4f);
        LiveDraw = carving || boring || sawing || shavings > 0f;
    }

    // ------------------------------------------------------------------ the target
    /// <summary>The part's spec, when the piece is for one.</summary>
    private CraftComponentSpec Spec(CraftPiece p)
    {
        CraftComponentSpec spec = p != null && !string.IsNullOrEmpty(p.intentDesign) ? Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        return spec != null && spec.kind == CraftPieceKind.Wood ? spec : null;
    }

    /// <summary>
    /// The width and thickness the part wants at <paramref name="t"/> (0-1 along it) - from the shape it is judged by,
    /// else from the wooden part's thickness range - and the length it should be.
    /// </summary>
    private bool TargetAt(CraftPiece p, float t, out float width, out float thickness, out float lengthMin, out float lengthMax)
    {
        width = thickness = lengthMin = lengthMax = 0f;
        CraftComponentSpec spec = Spec(p);
        if (spec == null) return false;
        CraftShapeTarget shape = spec.shape;
        if (shape != null && shape.profile != null && shape.profile.Count >= 2)
        {
            shape.At(Mathf.Clamp01(t), out width, out thickness);
            lengthMin = shape.lengthMin;
            lengthMax = Mathf.Max(shape.lengthMin, shape.lengthMax);
            return true;
        }
        width = thickness = (spec.wood.diameterMin + spec.wood.diameterMax) * 0.5f;
        lengthMin = spec.wood.lengthMin;
        lengthMax = Mathf.Max(spec.wood.lengthMin, spec.wood.lengthMax);
        return true;
    }

    /// <summary>The length the target is drawn to: the piece's own when that is in the range, else the nearer end of it.</summary>
    private static float TargetLength(CraftPiece p, float lengthMin, float lengthMax) => Mathf.Clamp((float)p.Length, lengthMin, lengthMax);

    /// <summary>Lays the views out to the piece and its target: one length scale for both, a width scale for the view from above and a thickness scale for the view from the side.</summary>
    private void Layout(CraftPiece p)
    {
        double maxW = 1, maxT = 1;
        foreach (MetalSegment s in p.segments) { maxW = Math.Max(maxW, s.width); maxT = Math.Max(maxT, s.thickness); }
        float longest = (float)p.Length;
        if (TargetAt(p, 0f, out _, out _, out _, out float lmax))
        {
            longest = Mathf.Max(longest, lmax);
            for (int i = 0; i <= 16; i++)
            {
                TargetAt(p, i / 16f, out float tw, out float tt, out _, out _);
                maxW = Math.Max(maxW, tw);
                maxT = Math.Max(maxT, tt);
            }
        }
        mmX = Mathf.Max(0.1f, longest / ViewLength);
        mmTop = Mathf.Max(0.05f, (float)maxW / 120f);
        mmSide = Mathf.Max(0.05f, (float)maxT / 120f);
    }

    private float XOf(double mm) => X0 + (float)(mm / mmX);
    private float MmAt(float x) => (x - X0) * mmX;

    /// <summary>The segment under pixel x (by the piece's real lengths).</summary>
    private static int SegmentAtX(CraftPiece p, float mmAlong)
    {
        double l = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            l += p.segments[i].length;
            if (mmAlong <= l) return i;
        }
        return p.segments.Count - 1;
    }

    // ------------------------------------------------------------------ drawing
    private static readonly Color Dimmed = new Color(0.04f, 0.04f, 0.05f, 0.6f);

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = WoodPiece;
        if (p == null || p.location != CraftLocation.Air)
            return; // the bench is empty: the info row says where the piece is
        Layout(p);
        Color wood = CraftArt.MetalColor(Rules, p);
        if (p.wood.charred > 0.05) wood = Color.Lerp(wood, Color.black, (float)p.wood.charred);
        wood.a = 1f;
        DrawView(c, p, topView, true, wood);
        DrawView(c, p, sideView, false, wood);
        DrawTarget(c, p, topView, true);
        DrawTarget(c, p, sideView, false);
        CraftArt.Label(c, X0, topView.yMax + 6f, "FROM ABOVE: WIDTH", cut == Cut.Thickness ? CraftArt.LabelDim : CraftArt.LabelInk);
        CraftArt.Label(c, X0, sideView.yMax + 6f, "FROM THE SIDE: THICKNESS", cut == Cut.Width ? CraftArt.LabelDim : CraftArt.LabelInk);
        // The tab decides: the view it does not cut is dimmed.
        if (cut == Cut.Width) c.Rect(sideView.xMin - 4, sideView.yMin, sideView.xMax + 4, sideView.yMax, Dimmed);
        if (cut == Cut.Bore) c.Rect(sideView.xMin - 4, sideView.yMin, sideView.xMax + 4, sideView.yMax, new Color(0f, 0f, 0f, 0.3f)); // the bit goes in from the end, seen from above
        if (cut == Cut.Thickness) c.Rect(topView.xMin - 4, topView.yMin, topView.xMax + 4, topView.yMax, Dimmed);
        PieceWork k = Work(p);
        // A haft's end that goes into a head's eye.
        if (CraftWoodwork.HeadEye(Ws, p, out double eye, out _))
        {
            float ex = XOf(p.Length);
            MetalSegment end = p.segments[p.segments.Count - 1];
            float ex0 = XOf(p.Length - end.length);
            c.Frame(ex0, topView.center.y - (float)end.width * 0.5f / mmTop - 4f, ex, topView.center.y + (float)end.width * 0.5f / mmTop + 4f, Mark, 1.5f);
            string label = $"EYE END {eye:0} MM";
            CraftArt.Label(c, Mathf.Min(ex - CraftArt.LabelWidth(label), topView.xMax - CraftArt.LabelWidth(label)), topView.yMin + 4f, label, Mark);
        }
        // The saw's mark and its kerf, on both views.
        if (k.mark >= 0f)
        {
            float mx = XOf(k.mark * p.Length);
            foreach (Rect v in new[] { topView, sideView })
                for (float y = v.yMin + 6; y < v.yMax - 6; y += 6) c.Line(mx, y, mx, y + 3, new Color(0.95f, 0.95f, 1f, 0.85f), 1.5f);
            if (k.sawStarted)
            {
                float bx = mx + (float)k.saw.Blade;
                int i = SegmentAtX(p, k.mark * (float)p.Length);
                float hw = (float)p.segments[i].width * 0.5f / mmTop;
                float depth = (float)k.saw.Progress * hw * 2f;
                c.Rect(bx - 1.5f, topView.center.y + hw - depth, bx + 1.5f, topView.center.y + hw, new Color(0.05f, 0.04f, 0.03f));
                Gauge(c, "SAW", X0, 10f, 300f, 8f, (float)k.saw.Progress, new Color(0.6f, 0.85f, 1f));
            }
        }
        // The knife's path while it cuts; the shavings curl off where it last cut.
        for (int i = 1; i < path.Count; i++)
            c.Line(path[i - 1].x, path[i - 1].y, path[i].x, path[i].y, Mark, 2f);
        if (shavings > 0.01f)
            for (int i = 0; i < 10; i++)
            {
                float sx = Mathf.Clamp(shavingsAt.x + (i * 23 % 40) - 20, 6f, 714f);
                float sy = Mathf.Clamp(shavingsAt.y + 8f + (i * 11 % 26) * (1.2f - shavings), 6f, 394f);
                c.Ring(sx, sy, 2.5f + i % 3, 1f, new Color(Mathf.Min(1f, wood.r * 1.25f), Mathf.Min(1f, wood.g * 1.2f), wood.b, Mathf.Clamp01(shavings)));
            }
        if (boring)
        {
            Gauge(c, "TURN", X0, 10f, 300f, 8f, (float)(bore.Turns - Math.Floor(bore.Turns)), Mark);
            // How evenly the bit is turned - each turn is sent with it: red where it tears the grain and splits the wood.
            float lw = CraftArt.LabelWidth("STEADY") + 6f;
            CraftArt.Label(c, 360f, 9f, "STEADY", CraftArt.LabelInk);
            Meter(c, 360f + lw, 10f, 200f - lw, 8f, bore.Turned >= Math.PI ? (float)bore.Steadiness : 0f, new Color(0.5f, 0.85f, 0.6f),
                (0f, (float)CraftWoodwork.CarveSplitSteady, HarmBand), ((float)CraftWoodwork.CleanStroke, 1f, GoodBand));
        }
        DrawSection(c, p, k);
    }

    private void DrawView(CraftPixels c, CraftPiece p, Rect view, bool top, Color wood)
    {
        c.Rect(view.xMin - 6, view.yMin, view.xMax + 6, view.yMax, new Color(0f, 0f, 0f, 0.25f));
        float cy = view.center.y, scale = top ? mmTop : mmSide;
        double at = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float x = XOf(at), x1 = XOf(at + s.length), h = (float)(top ? s.width : s.thickness) / scale;
            Color col = Color.Lerp(wood, wood * 0.82f, (i % 2) * 0.5f);
            col.a = 1f;
            c.Rect(x, cy - h * 0.5f, x1 + 0.6f, cy + h * 0.5f, col);
            // the grain along it
            for (int g = -2; g <= 2; g++)
                c.Line(x, cy + g * h * 0.18f, x1, cy + g * h * 0.18f + Mathf.Sin(i * 1.3f + g) * 1.5f, new Color(0f, 0f, 0f, 0.12f), 1f);
            // the bark where it is still on
            if (s.bark > 0.1)
            {
                Color bark = new Color(0.3f, 0.21f, 0.14f, Mathf.Clamp01((float)s.bark));
                CraftArt.Span(c, x, cy + h * 0.5f - 3f, x1, cy + h * 0.5f, bark);
                CraftArt.Span(c, x, cy - h * 0.5f, x1, cy - h * 0.5f + 3f, bark);
            }
            if (top && s.hole > 0.5)
            {
                float hh = Mathf.Min(h * 0.5f, (float)s.hole * 0.5f / scale);
                CraftArt.Span(c, x, cy - hh, x1, cy + hh, new Color(0.05f, 0.04f, 0.03f, 0.9f));
            }
            at += s.length;
        }
        if (p.wood.split > 0.05)
            c.Line(XOf(p.Length * 0.2), cy + 3, XOf(p.Length * (0.2 + 0.6 * p.wood.split)), cy - 2, new Color(0.1f, 0.05f, 0.02f), 1.5f);
    }

    /// <summary>The part's shape dashed over a view, to the view's own scale, with ticks under it at the shortest and longest it may be.</summary>
    private void DrawTarget(CraftPixels c, CraftPiece p, Rect view, bool top)
    {
        if (!TargetAt(p, 0f, out _, out _, out float lmin, out float lmax))
            return;
        float length = TargetLength(p, lmin, lmax), cy = view.center.y, scale = top ? mmTop : mmSide;
        const int n = 32;
        Vector2 lastA = default, lastB = default;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            TargetAt(p, t, out float w, out float th, out _, out _);
            float half = (top ? w : th) * 0.5f / scale, x = XOf(t * length);
            var a = new Vector2(x, cy + half);
            var b = new Vector2(x, cy - half);
            if (i > 0)
            {
                CraftArt.Dashed(c, lastA, a, CraftArt.TargetColor, 1.2f, 5f);
                CraftArt.Dashed(c, lastB, b, CraftArt.TargetColor, 1.2f, 5f);
            }
            if (i == 0 || i == n) CraftArt.Dashed(c, a, b, CraftArt.TargetColor, 1.2f, 4f);
            lastA = a;
            lastB = b;
        }
        Color tick = new Color(CraftArt.TargetColor.r, CraftArt.TargetColor.g, CraftArt.TargetColor.b, 0.7f);
        float under = view.yMin - 3f;
        c.Line(XOf(lmin), under, XOf(lmin), under - 5f, tick, 1.5f);
        c.Line(XOf(lmax), under, XOf(lmax), under - 5f, tick, 1.5f);
        c.Line(XOf(lmin), under - 2.5f, XOf(lmax), under - 2.5f, new Color(tick.r, tick.g, tick.b, 0.4f), 1f);
    }

    /// <summary>
    /// The section beside the views: under the pointer, what the piece has (solid) against what the part wants there
    /// (dashed) - or, boring, the end the slot goes into, with the slot the tang needs; on a haft's eye end, the eye.
    /// </summary>
    private void DrawSection(CraftPixels c, CraftPiece p, PieceWork k)
    {
        c.Rect(inset.xMin - 4, inset.yMin - 4, inset.xMax + 4, inset.yMax + 4, new Color(0f, 0f, 0f, 0.35f));
        Color wood = CraftArt.MetalColor(Rules, p);
        wood.a = 1f;
        int i;
        string title;
        bool boreView = cut == Cut.Bore;
        if (boreView)
        {
            i = (k.boreEnd >= 0 ? k.boreEnd : BoreEndUnder(p, ui.Hover)) == 1 ? p.segments.Count - 1 : 0;
            title = "THE END";
        }
        else
        {
            Vector2 h = ui.Hover;
            float along = h.x >= 0f ? MmAt(h.x) : (float)p.Length * 0.5f;
            if (along < 0f || along > p.Length) along = (float)p.Length * 0.5f;
            i = SegmentAtX(p, along);
            title = $"AT {p.PositionOf(i) * p.Length:0} MM";
        }
        MetalSegment s = p.segments[i];
        bool wants = TargetAt(p, (float)p.PositionOf(i), out float tw, out float tt, out _, out _);
        float biggest = Mathf.Max(Mathf.Max((float)s.width, (float)s.thickness), wants ? Mathf.Max(tw, tt) : 0f);
        double need = 0, eye = 0;
        bool slot = CraftWoodwork.TangSlot(Ws, p, out need, out bool measured);
        bool eyeEnd = !boreView && i == p.segments.Count - 1 && CraftWoodwork.HeadEye(Ws, p, out eye, out _);
        if (boreView && slot) biggest = Mathf.Max(biggest, (float)need);
        if (eyeEnd) biggest = Mathf.Max(biggest, (float)eye);
        float k2 = 104f / Mathf.Max(1f, biggest);
        Vector2 cc = inset.center;
        float hw = (float)s.width * 0.5f * k2, ht = (float)s.thickness * 0.5f * k2;
        c.Rect(cc.x - hw, cc.y - ht, cc.x + hw, cc.y + ht, wood);
        c.Frame(cc.x - hw, cc.y - ht, cc.x + hw, cc.y + ht, new Color(0.1f, 0.07f, 0.05f, 0.9f), 1f);
        if (s.hole > 0.5) c.Disc(cc.x, cc.y, (float)s.hole * 0.5f * k2, new Color(0.05f, 0.04f, 0.03f));
        string has = $"{s.width:0} × {s.thickness:0} MM", want = "";
        if (boreView)
        {
            if (slot)
            {
                DashedCircle(c, cc, (float)need * 0.5f * k2);
                has = $"SLOT {s.hole:0} MM";
                want = $"{(measured ? "TANG" : "WANTS")} {need:0} MM";
            }
        }
        else if (eyeEnd)
        {
            DashedCircle(c, cc, (float)eye * 0.5f * k2);
            want = $"EYE {eye:0} MM";
        }
        else if (wants)
        {
            Vector2 a = new Vector2(cc.x - tw * 0.5f * k2, cc.y - tt * 0.5f * k2), b = new Vector2(cc.x + tw * 0.5f * k2, cc.y + tt * 0.5f * k2);
            CraftArt.Dashed(c, a, new Vector2(b.x, a.y), CraftArt.TargetColor, 1.2f, 4f);
            CraftArt.Dashed(c, new Vector2(b.x, a.y), b, CraftArt.TargetColor, 1.2f, 4f);
            CraftArt.Dashed(c, b, new Vector2(a.x, b.y), CraftArt.TargetColor, 1.2f, 4f);
            CraftArt.Dashed(c, new Vector2(a.x, b.y), a, CraftArt.TargetColor, 1.2f, 4f);
            want = $"WANTS {tw:0} × {tt:0}";
        }
        CraftArt.Label(c, inset.xMin, inset.yMax + 8f, title, CraftArt.LabelInk);
        CraftArt.Label(c, inset.xMin, inset.yMin - 18f, has, CraftArt.LabelInk);
        if (want.Length > 0) CraftArt.Label(c, inset.xMin, inset.yMin - 32f, want, CraftArt.TargetColor);
    }

    private static void DashedCircle(CraftPixels c, Vector2 cc, float r)
    {
        for (int a = 0; a < 360; a += 20)
        {
            float a0 = a * Mathf.Deg2Rad, a1 = (a + 10) * Mathf.Deg2Rad;
            c.Line(cc.x + Mathf.Cos(a0) * r, cc.y + Mathf.Sin(a0) * r, cc.x + Mathf.Cos(a1) * r, cc.y + Mathf.Sin(a1) * r, CraftArt.TargetColor, 1.5f);
        }
    }

    /// <summary>A labelled meter: the label in the drawing's letters, the meter after it.</summary>
    private static void Gauge(CraftPixels c, string label, float x, float y, float w, float h, float value, Color fill)
    {
        CraftArt.Label(c, x, y + h * 0.5f - 5f, label, CraftArt.LabelInk);
        Meter(c, x + 40f, y, w - 40f, h, value, fill);
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
        if (phase == CraftPointer.Scroll)
        {
            // the wheel sets the knife: how hard it is pressed into the wood
            if (pressure != null && cut != Cut.Saw)
            {
                SetSlider(pressure, Mathf.Clamp(pressure.value + (scroll > 0 ? 0.05f : -0.05f), 0.1f, 1f));
                said = $"The knife: {pressure.value * 100:0}% - it bites about {BiteDepth(pressure.value, 0.5f):0.0} mm in a half-second stroke.";
            }
            return;
        }
        if (phase == CraftPointer.Down) ignorePress = false;
        if (ignorePress)
        {
            if (phase == CraftPointer.Up) ignorePress = false;
            return;
        }
        CraftPiece p = WoodPiece;
        if (phase == CraftPointer.Down)
        {
            if (p == null) { said = "Put a wooden blank on the bench (from the materials on the left) and pick it."; ignorePress = true; return; }
            if (p.location != CraftLocation.Air) { said = OffTheBench(p); ignorePress = true; return; }
        }
        if (p == null) return;
        switch (cut)
        {
            case Cut.Saw: SawPointer(p, phase, pos); return;
            case Cut.Bore: BorePointer(p, phase, pos); return;
            default: KnifePointer(p, phase, pos); return;
        }
    }

    private bool Over(Rect v, Vector2 pos) => pos.x >= v.xMin - 8 && pos.x <= v.xMax + 8 && pos.y >= v.yMin && pos.y <= v.yMax;

    /// <summary>Right-click: the knife is lifted (that stroke takes nothing), the bit is drawn out, the saw rests in its kerf.</summary>
    private void Cancel()
    {
        if (!carving && !boring && !sawing) return;
        said = carving ? "The knife is lifted: that stroke took nothing." : boring ? "The bit is drawn out." : "The saw rests in its kerf.";
        carving = false;
        path.Clear();
        boring = false;
        bore.Clear();
        if (sawing) StopSaw();
        ignorePress = true;
    }

    // ---- the knife
    /// <summary>The depth (mm) the knife at <paramref name="press"/> takes in a stroke of <paramref name="seconds"/>, at the window's careful pace for these hands - what the workshop gets.</summary>
    private float BiteDepth(float press, float seconds)
    {
        CraftSimulationSettings s = Rules.Settings;
        float pace = s.windowPaceNovice + (s.windowPaceMaster - s.windowPaceNovice) * Mathf.Clamp01((float)Hands.skill);
        return press * 1.8f * Mathf.Clamp(seconds / 0.5f, 0.4f, 1.2f) * (0.6f + 0.4f * (float)Hands.Strength) * pace;
    }

    private void KnifePointer(CraftPiece p, CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            bool top = Over(topView, pos), side = Over(sideView, pos);
            if (!top && !side) return;
            if (cut == Cut.Width && !top || cut == Cut.Thickness && !side)
            {
                said = cut == Cut.Width ? "Shave the width works on the view from above (or pick Shave the thickness)." : "Shave the thickness works on the view from the side (or pick Shave the width).";
                ignorePress = true;
                return;
            }
            carving = true;
            path.Clear();
            path.Add(pos);
            strokeStart = strokeEnd = ui.PointerTime;
            return;
        }
        if (!carving) return;
        if (phase == CraftPointer.Drag)
        {
            path.Add(pos);
            strokeEnd = ui.PointerTime;
            return;
        }
        if (phase == CraftPointer.Up) Stroke(p);
    }

    /// <summary>The knife's stroke as far as it went: sent to the piece it was drawn on.</summary>
    private void Stroke(CraftPiece p)
    {
        var pts = new List<Vector2>(path);
        float seconds = Mathf.Max(0f, strokeEnd - strokeStart);
        carving = false;
        path.Clear();
        if (p == null || p.location != CraftLocation.Air || pts.Count < 3)
            return;
        float a = float.MaxValue, b = float.MinValue;
        foreach (Vector2 q in pts) { a = Mathf.Min(a, q.x); b = Mathf.Max(b, q.x); }
        if (b - a < 6f) return; // a dab, not a stroke
        int from = SegmentAtX(p, MmAt(a)), to = SegmentAtX(p, MmAt(b));
        if (MmAt(a) > p.Length) { said = "That is past the end of the blank."; return; }
        // Steady = straight: how far the stroke wandered from its own line (a straight stroke down a taper is steady), and
        // the hands' tremble.
        float steady = (float)CraftWoodwork.StrokeSteady(pts, 30, Hands.Unsteadiness);
        // A slow stroke bites deeper than a flick; a tired arm presses less; now and then the knife skids and digs in.
        float press = pressure != null ? pressure.value : 0.5f;
        float depth = press * 1.8f * Mathf.Clamp(seconds / 0.5f, 0.4f, 1.2f) * (0.6f + 0.4f * (float)Hands.Strength);
        bool slipped = Hands.Slips(23);
        if (slipped) { steady *= 0.25f; depth *= 1.4f; }
        Hands.Move(press * 0.4f);
        CraftSimulationSettings s = Rules.Settings;
        float bite = depth * (s.windowPaceNovice + (s.windowPaceMaster - s.windowPaceNovice) * Mathf.Clamp01((float)Hands.skill)); // what the workshop takes
        CraftSurface.CarveCut what = cut == Cut.Round ? CraftSurface.CarveCut.Round : cut == Cut.Thickness ? CraftSurface.CarveCut.Thickness : CraftSurface.CarveCut.Width;
        string id = p.id;
        double splitBefore = p.wood.split;
        Vector2 last = pts[pts.Count - 1];
        Send(new CraftRequest(CraftRequestKind.Carve, id) { i0 = from, i1 = to, i2 = (int)what, a = depth, b = steady }, (ok, why) =>
        {
            if (!ok) return;
            shavings = 1f;
            shavingsAt = last;
            CraftPiece q = Ws.Piece(id);
            bool split = q != null && q.wood.split > splitBefore + 1e-4;
            if (slipped) said = "The knife slipped and dug in: your arm is tired." + (split ? " It split along the grain." : "");
            else if (split) said = bite > CraftWoodwork.CarveSplitDepth ? $"Too deep a cut ({bite:0.0} mm): it split along the grain." : "A shaky stroke: the knife tore along the grain and split it.";
            else if (bite > CraftWoodwork.CarveSplitDepth * 0.8) said = $"A deep cut ({bite:0.0} mm), near what splits it: ease the knife.";
            else said = steady >= (float)CraftWoodwork.CleanStroke ? $"A clean, steady shaving ({bite:0.0} mm)." : $"A wobbly cut ({bite:0.0} mm): it leaves the surface rough.";
        });
    }

    // ---- the bore
    /// <summary>The end nearer to the pointer (0 the near end, 1 the far end): the slot goes in from an end.</summary>
    private int BoreEndUnder(CraftPiece p, Vector2 pos) => pos.x >= 0f && MmAt(pos.x) > p.Length * 0.5 ? 1 : 0;

    private void BorePointer(CraftPiece p, CraftPointer phase, Vector2 pos)
    {
        PieceWork k = Work(p);
        if (phase == CraftPointer.Down)
        {
            if (!Over(topView, pos)) return;
            if (!CraftWoodwork.TangSlot(Ws, p, out double need, out _))
            {
                said = string.IsNullOrEmpty(p.intentDesign) ? "Choose what it is for first: the slot is the size of the tang it goes on." : "This part goes on no tang: there is no slot to bore.";
                ignorePress = true;
                return;
            }
            k.boreEnd = BoreEndUnder(p, pos); // the slot in the piece from this end (the bench's, or this window's) says how deep it goes
            boring = true;
            bore.Begin(pos.x, pos.y); // the circles are counted round their own middle: the hand circles from where it pressed
            return;
        }
        if (!boring) return;
        if (phase == CraftPointer.Drag)
        {
            bore.Add(pos.x, pos.y);
            if (bore.Turns < 1) return;
            bore.Spend(1);
            // Each turn bores the slot wider, and a stretch deeper than it goes in the piece now (up to three quarters of
            // the length) - past the turns still on their way, which have not bored theirs yet.
            if (!CraftWoodwork.BoreSpan(p, k.boreEnd, out int i0, out int i1, k.boreAhead)) return;
            float press = pressure != null ? pressure.value : 0.5f;
            string id = p.id;
            Hands.Move(0.2);
            k.boreAhead++;
            Send(new CraftRequest(CraftRequestKind.Carve, id) { i0 = i0, i1 = i1, i2 = (int)CraftSurface.CarveCut.Bore, a = 1.2f * press + 0.3f, b = (float)bore.Steadiness }, (ok, why) =>
            {
                k.boreAhead = Math.Max(0, k.boreAhead - 1);
                if (!ok) return;
                shavings = 0.7f;
                shavingsAt = new Vector2(XOf(k.boreEnd == 1 ? p.Length : 0), topView.center.y);
                CraftPiece q = Ws.Piece(id);
                if (q != null && CraftWoodwork.TangSlot(Ws, q, out double need, out bool measured))
                {
                    double hole = CraftAssembly.Eye(q);
                    said = hole + 0.5 >= need ? $"The slot takes the {(measured ? "blade's tang" : "tang")} now ({hole:0} of {need:0} mm): bore no more." : $"The slot is {hole:0} mm; the tang needs {need:0} mm.";
                }
            });
            return;
        }
        if (phase == CraftPointer.Up)
        {
            boring = false;
            bore.Clear();
        }
    }

    // ---- the saw
    private void StopSaw()
    {
        sawing = false;
        CraftPiece p = WoodPiece;
        if (p != null && works.TryGetValue(p.id, out PieceWork k)) k.saw.Stop();
    }

    private void SawPointer(CraftPiece p, CraftPointer phase, Vector2 pos)
    {
        PieceWork k = Work(p);
        if (phase == CraftPointer.Down)
        {
            if (!Over(topView, pos) && !Over(sideView, pos)) return;
            if (k.sawSent) { said = "Waiting for the cut..."; ignorePress = true; return; }
            if (k.mark >= 0f && Mathf.Abs(pos.x - XOf(k.mark * p.Length)) <= 12f)
            {
                // the blade is set on the mark: where the hand pressed is only where its strokes start from
                sawing = true;
                pressX = pos.x;
                if (k.sawStarted) k.saw.Resume();
                else
                {
                    MetalSegment s = p.segments[SegmentAtX(p, k.mark * (float)p.Length)];
                    k.saw.Begin(CraftSawStroke.StrokesFor(s.width, s.thickness)); // across the section: a wide board takes longer than a stick
                    k.sawStarted = true;
                }
                return;
            }
            ignorePress = true;
            if (k.sawStarted && k.saw.Progress > 0)
            {
                if (!Confirm("carve-mark", "Click again to give up the cut begun at the mark and mark here.")) return;
            }
            float at = Mathf.Clamp01(MmAt(pos.x) / Mathf.Max(1f, (float)p.Length));
            if (!CraftWoodwork.CanSawAt(p, at, out string why)) { said = why; return; }
            k.mark = at;
            k.saw.Stop();
            k.sawStarted = false;
            said = $"Marked: {at * p.Length:0} mm and {(1 - at) * p.Length:0} mm. Press on the mark and saw: long strokes up and down, keeping on the line.";
            return;
        }
        if (!sawing) return;
        if (phase == CraftPointer.Up)
        {
            StopSaw();
            if (k.saw.Progress < 1) said = $"The saw rests {k.saw.Progress * 100:0}% through: press on the mark again to go on.";
            return;
        }
        if (phase != CraftPointer.Drag) return;
        k.saw.Add(pos.x - pressX, pos.y, ui.PointerTime, Hands);
        if (k.saw.Progress < 1) return;
        sawing = false;
        k.saw.Stop();
        k.sawSent = true;
        float wander = (float)k.saw.Wander, square = (float)k.saw.Square, mark = k.mark;
        string id = p.id;
        Hands.Move(0.3);
        Send(new CraftRequest(CraftRequestKind.SawCut, id) { a = mark, b = wander, c = square }, (ok, why) =>
        {
            if (!ok && why != CraftAnswerWaits.NoAnswer) { k.sawSent = false; return; } // still in its kerf: a stroke more sends it again
            works.Remove(id);
            if (ok) ui.Message(wander < CraftWoodwork.SawSquare && square >= 0.8f ? "Sawn through, square and on the line: pick the piece you keep."
                : wander < 0.5f ? "Sawn through - the cut wandered a little: the end is ragged." : "Sawn through, well off the line: a torn, crooked end.");
        });
    }

    // ------------------------------------------------------------------ the words
    public override void Refresh()
    {
        if (!intentPending && purpose != null && !purpose.IsExpanded) SyncPurpose(); // what another hand (or the 3D bench) set
        if (info == null)
            return;
        CraftPiece p = WoodPiece;
        if (p == null)
        {
            info.text = ("Put a wooden blank on the bench (from the materials on the left) and pick it. " + said).Trim();
            return;
        }
        if (p.location != CraftLocation.Air)
        {
            info.text = OffTheBench(p);
            return;
        }
        var sb = new StringBuilder();
        double minW = double.MaxValue, maxW = 0, minT = double.MaxValue, maxT = 0;
        foreach (MetalSegment s in p.segments) { minW = Math.Min(minW, s.width); maxW = Math.Max(maxW, s.width); minT = Math.Min(minT, s.thickness); maxT = Math.Max(maxT, s.thickness); }
        sb.Append($"{p.Length:0} mm long, {minW:0}-{maxW:0} wide, {minT:0}-{maxT:0} thick");
        CraftComponentSpec spec = Spec(p);
        if (spec != null && TargetAt(p, 0f, out _, out _, out float lmin, out float lmax))
            sb.Append($"; the {spec.Name.ToLowerInvariant()} wants {lmin:0}-{lmax:0} mm (dashed green; the section beside shows it under the pointer)");
        else
            sb.Append(" - choose what it is for to see the shape it is judged by");
        sb.Append(". ");
        if (p.wood.split > 0.1) sb.Append($"<color=#FF8A70>It has split ({p.wood.split * 100:0}%): it will be weak.</color> ");
        float press = pressure != null ? pressure.value : 0.5f;
        switch (cut)
        {
            case Cut.Saw:
                PieceWork k = Work(p);
                sb.Append(k.mark < 0f ? "Click where to cut. " : $"Press on the mark and saw ({k.saw.Progress * 100:0}% through). ");
                break;
            case Cut.Bore:
                if (CraftWoodwork.TangSlot(Ws, p, out double need, out bool measured))
                    sb.Append($"Press near the end the tang goes into and draw circles from there, round and even: the slot is {CraftAssembly.Eye(p):0} mm, the {(measured ? "blade's tang" : "tang")} needs {need:0}. ");
                else sb.Append(string.IsNullOrEmpty(p.intentDesign) ? "Choose what it is for: the slot is the size of its tang. " : "This part goes on no tang. ");
                break;
            default:
                sb.Append($"{(cut == Cut.Round ? "Drag along either view to take the corners off" : cut == Cut.Width ? "Drag the knife along the view from above to narrow it" : "Drag the knife along the view from the side to thin it")}: "
                          + $"it bites about {BiteDepth(press, 0.5f):0.0} mm in a half-second stroke (the wheel sets the knife); past {CraftWoodwork.CarveSplitDepth:0.0} mm, or shaky, it splits. ");
                break;
        }
        info.text = (sb + said).Trim();
    }
}
