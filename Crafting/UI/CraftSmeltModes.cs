using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What the three smelting areas share: their own words, labelled gauges and small drawing helpers.</summary>
public abstract class CraftSmeltArea : CraftMode
{
    // The area's own words: what the last thing done came to, or why it could not be done.
    private string saidText = "", saidAbout;
    private float saidUntil;

    /// <summary>
    /// The area's own words, shown with its standing ones: what the last thing done came to, or why it could not be done.
    /// They go once read - after as long as the message row keeps a message (<see cref="CraftWindowRules.MessageSeconds"/>) -
    /// and words about a piece (the lump on the ore bench) as soon as another piece is picked. Setting them says them
    /// about the piece picked now (<see cref="SayAbout"/> names another).
    /// </summary>
    protected string Said
    {
        get => saidText.Length == 0 || Time.unscaledTime > saidUntil || saidAbout != null && saidAbout != ui?.SelectedId ? "" : saidText;
        set => SayAbout(ui?.SelectedId, value);
    }

    /// <summary>Says <paramref name="text"/> about <paramref name="piece"/> (null: about the place - the furnace, the crucible - whatever is picked).</summary>
    protected void SayAbout(string piece, string text)
    {
        saidText = text ?? "";
        saidAbout = string.IsNullOrEmpty(piece) || !SaysAboutThePiece ? null : piece;
        saidUntil = Time.unscaledTime + CraftWindowRules.MessageSeconds(saidText);
    }

    /// <summary>The area's words are about the piece picked (the ore bench's lump), not the place (the furnace, the crucible).</summary>
    protected virtual bool SaysAboutThePiece => false;

    /// <summary>A meter with its name before it: the label from <paramref name="x"/>, the bar to <paramref name="x"/> + <paramref name="w"/>.</summary>
    protected static void Gauge(CraftPixels c, float x, float y, float w, float h, string label, float value, Color fill,
                                params (float from, float to, Color color)[] bands) => Gauge(c, x, y, w, h, label, CraftArt.LabelWidth(label) + 6f, value, fill, bands);

    /// <summary>A meter with its name before it, the bar starting <paramref name="labelWidth"/> after <paramref name="x"/> (bars stacked line up).</summary>
    protected static void Gauge(CraftPixels c, float x, float y, float w, float h, string label, float labelWidth, float value, Color fill,
                                params (float from, float to, Color color)[] bands)
    {
        float lw = labelWidth;
        CraftArt.Label(c, x, y + h * 0.5f - 5f, label, CraftArt.LabelInk);
        Meter(c, x + lw, y, Mathf.Max(8f, w - lw), h, value, fill, bands);
    }

    /// <summary>A ring of dots (what would happen, not what is).</summary>
    protected static void DottedRing(CraftPixels c, float cx, float cy, float r, Color col)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(r * 0.8f), 8, 28);
        for (int k = 0; k < n; k++)
        {
            float a = k * 2f * Mathf.PI / n;
            c.Disc(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 1.1f, col);
        }
    }

    /// <summary>A meter's place kept on the surface (its left edge and bottom), so it never runs off the 720 × 400 drawing.</summary>
    protected static Vector2 OnSurface(float x, float y, float w, float h) =>
        new Vector2(Mathf.Clamp(x, 4f, CraftStationUI.SurfaceWidth - 4f - w), Mathf.Clamp(y, 4f, CraftStationUI.SurfaceHeight - 4f - h));

    /// <summary>The words for a piece's place, when it is not where an area works it.</summary>
    protected static string Where(CraftPiece p) =>
        p.location == CraftLocation.Furnace ? "It is part of the furnace's charge: it smelts there now."
        : p.location == CraftLocation.Melt ? "It is in the crucible: lift it out there with the tongs."
        : $"It is {CraftStationUI.LocationText(p)}: put it on the bench first.";
}

/// <summary>
/// The ore bench: the lump and the stones it has broken into, each drawn where it lies, as big as it is and by how much
/// ore shows in it (a blow's pieces lie round the stone they came from; a stone thrown out leaves the others where they
/// lie). Hold the hammer on a stone and let go over it to crush it (the bar over the stone says what the blow will do as
/// it would land: chip it, or split it in two, three or four - harder blows break it smaller, and powder it; held too
/// long the arm wavers and the bar sinks); pick barren stones out by dragging them onto the waste heap. Swirl it in the
/// pan to wash the dirt and light rock away (steady, wide circles keep the heavy ore; a jerky hand loses it with the mud;
/// a strong flow is quick but carries ore off with the rock), and shake the sieve to sort off the fines - the stones
/// ringed white fall through a thorough shaking of these hands, the dotted ones even a lazy one. What is bound in the
/// mineral stays.
/// <para>Or clean one stone at a time with a brush, dry: pick a brush and scrub back and forth over a stone. The soft
/// brush takes loose dirt, the stiff one caked clay, the wire brush knocks off the weathered rind of rock (and a little
/// ore with it). Dirt shows as brown specks, the rind as a grey band round the stone; each side of a stone remembers
/// what a brush already took from it, as the station counts it (a brush goes over each part once). Slower than washing,
/// but nothing is lost to the water. By hand (3D) the stone is turned in the fingers and brushed all round. Right-click
/// puts down what is in hand: the hammer is lowered, a stone put back; scrubbing, swirling and shaking count as far as
/// they went.</para>
/// </summary>
public sealed class CraftOreMode : CraftSmeltArea
{
    private enum Tool { Hammer, Soft, Stiff, Wire, PickOut }
    private enum Gesture { None, Hammer, Scrub, Pick, Pan, Sieve }
    private static readonly string[] ToolNames = { "Hammer", "Soft brush", "Stiff brush", "Wire brush", "Pick out" };
    /// <summary>Seconds the hammer takes to come up again after a blow: a press sooner waits for it (the station counts blows no faster).</summary>
    private const float Recovery = 0.42f;
    /// <summary>A shake of the sieve counts once the hand has gone this far (px) one way and come back as far.</summary>
    private const float ShakeTravel = 20f;
    /// <summary>The pan's water: a swirl counts inside this radius (px) of its middle.</summary>
    private const float PanWater = 58f;
    /// <summary>The stones on the tray: px a centimetre of their size, the least drawn (to be picked up) and the most.</summary>
    private const float StonePerCm = 4.5f, StoneLeast = 5f, StoneMost = 72f;
    /// <summary>The sectors round a stone a brush remembers having gone over.</summary>
    private const int Sides = 8;

    private readonly CraftHammer hammer = new CraftHammer { fullCharge = 0.7f };
    private Tool tool = Tool.Hammer;
    private readonly List<Button> toolButtons = new List<Button>();
    private TextMeshProUGUI info;
    private float flowValue = 0.5f, meshValue = 1f, meshShownUntil;
    private static readonly Rect Tray = new Rect(20, 20, 520, 360), Pan = new Rect(560, 30, 140, 140), Heap = new Rect(560, 340, 140, 40);
    private static readonly Rect SieveAt = new Rect(560, 200, 140, 120);
    private int hover = -1;
    // The stones as last drawn: where each lies, of which lump, as the bench lay then (a press aims at this drawing).
    private List<Vector3> placed = new List<Vector3>();
    private string placedFor;
    private int placedLayout;
    // Where the stones lay last time (by their places on the bench), so that one thrown out or broken leaves the others be.
    private readonly Dictionary<(double, double), (double, double)> lay = new Dictionary<(double, double), (double, double)>();
    private string layFor;
    private int layStamp = int.MinValue;
    // What the brushes went over, side by side round each stone (by its place on the bench), as the station counts it.
    private sealed class StoneSides
    {
        public readonly CraftBrushCover cover = new CraftBrushCover();
        public int specks;
        public double dirtAtStart;
        public readonly double[] start = new double[Sides];
    }
    private readonly Dictionary<(double, double), StoneSides> sides = new Dictionary<(double, double), StoneSides>();

    // The gesture under way: on which lump, aimed at which stone as the bench lay then.
    private Gesture gesture;
    private string workPiece;
    private int workStone = -1, workLayout;
    private float lastBlowAt = -99f;
    private bool raisePending;
    // Scrubbing a stone: since when, how far.
    private StoneSides scrubbed;
    private float scrubStart, scrubPhase, scrubTravel;
    private Vector2 lastScrub;
    // A stone picked out, held in the hand: where, how big, its colour.
    private Vector2 dragAt;
    private float heldRadius;
    private Color heldColour;
    // The pan: the swirl round its middle - how far it went round, for how long it went on, how even its radius and pace.
    private float panAngle = float.NaN, panTravel, panAdvancing, panLastTime;
    private int panMoves, panN, panWn;
    private double panR, panR2, panW, panW2;
    private Vector2 panAt;
    // The sieve: its runs back and forth.
    private float sieveShift, shakeFrom, shakeExtreme, shakeLastTurn = -1f;
    private int shakeDir, shakeTurns;
    private readonly List<float> shakeGaps = new List<float>();

    public override string Title => "Ore";
    public override bool Available(CraftStation s) => s.Has(CraftTools.OreBench);
    public override string Hint => "Hammer: hold on a stone, let go over it to crush it. Brushes: scrub over a stone. Pan: swirl in the water. Sieve: drag back and forth on it. Pick out: drag a stone onto the waste heap. Right-click puts down." + ByHandHint("brush a stone by hand");
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, tool == Tool.Hammer);
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Brush;
    /// <summary>What the bench says is about the lump picked: another lump picked, it is not said under that one.</summary>
    protected override bool SaysAboutThePiece => true;

    private bool IsBrush => tool == Tool.Soft || tool == Tool.Stiff || tool == Tool.Wire;
    private CraftOreBrush Brush => tool == Tool.Soft ? CraftOreBrush.Soft : tool == Tool.Wire ? CraftOreBrush.Wire : CraftOreBrush.Stiff;

    /// <summary>How much of what a brush goes over the station counts from the window's drawing (the careful pace a practised hand quickens).</summary>
    private float WindowPace
    {
        get
        {
            CraftSimulationSettings s = Rules.Settings;
            return Mathf.Lerp(s.windowPaceNovice, s.windowPaceMaster, (float)Mastery);
        }
    }

    /// <summary>The most thorough shaking these hands can give (the station allows no more): what the white rings show.</summary>
    private float BestShaking => (float)CraftRequestGuard.SieveCap(CraftRequestGuard.FloorOf(Hands.skill));

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.brush = Brush;
        setup.fragment = Mathf.Max(0, hover >= 0 ? hover : workStone);
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        tool = setup.brush == CraftOreBrush.Soft ? Tool.Soft : setup.brush == CraftOreBrush.Wire ? Tool.Wire : Tool.Stiff;
        sides.Clear(); // brushed by hand all round: what each side shows is what the stone has now
        ShowTool();
    }

    public override void Build(RectTransform controls)
    {
        RectTransform r0 = Row(controls, 30f, 4f);
        toolButtons.Clear();
        for (int i = 0; i < ToolNames.Length; i++)
        {
            Tool t = (Tool)i;
            toolButtons.Add(Btn(r0, ToolNames[i], () => PickTool(t), 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        HandsOnButton(r0, "Brush by hand", 150f);
        ShowTool();
        RectTransform r1 = Row(controls);
        Slider flow = Slide(r1, "Water", 0.1f, 1f, flowValue, false, 64f, v => $"{v * 100:0}%");
        flow.onValueChanged.AddListener(v => flowValue = v);
        if (QuickBench)
            Btn(r1, "Rinse", () => Send(new CraftRequest(CraftRequestKind.Wash, ui.SelectedId) { a = flowValue, b = 6f, c = 0.5f }), 90f);
        RectTransform r2 = Row(controls);
        Slider mesh = Slide(r2, "Sieve mesh", 0.3f, 3f, meshValue, false, 64f, v => $"{v:0.0} cm");
        mesh.onValueChanged.AddListener(v => { meshValue = v; meshShownUntil = Time.unscaledTime + 3f; });
        if (QuickBench)
            Btn(r2, "Sieve", () => Send(new CraftRequest(CraftRequestKind.Sieve, ui.SelectedId) { a = meshValue, b = 1f }), 90f);
        info = InfoRow(controls);
    }

    private void PickTool(Tool t)
    {
        EndGesture(true);
        tool = t;
        ShowTool();
    }

    private void ShowTool()
    {
        for (int i = 0; i < toolButtons.Count; i++)
            if (toolButtons[i] != null) UITheme.SetSelected(toolButtons[i], (Tool)i == tool);
    }

    public override void Tick(float dt)
    {
        hammer.Tick(dt);
        if (raisePending && Time.unscaledTime - lastBlowAt >= Recovery)
        {
            raisePending = false;
            if (gesture == Gesture.Hammer) hammer.Raise(); // the hammer is up: the blow charges from now
        }
        LiveDraw = gesture != Gesture.None;
    }

    public override void Leave() => EndGesture(true);

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        bool here = p != null && p.kind == CraftPieceKind.Ore && p.location == CraftLocation.Air;
        c.Rect(Tray.xMin - 4, Tray.yMin - 4, Tray.xMax + 4, Tray.yMax + 4, new Color(0.13f, 0.11f, 0.1f));
        DrawPan(c);
        DrawSieve(c);
        DrawHeap(c);
        hover = -1;
        if (p == null || p.kind != CraftPieceKind.Ore)
        {
            placed = new List<Vector3>();
            placedFor = null;
            CraftArt.Label(c, Tray.center.x - CraftArt.LabelWidth("PICK A LUMP OF ORE") * 0.5f, Tray.center.y - 5f, "PICK A LUMP OF ORE", CraftArt.LabelDim);
            return;
        }
        if (!here)
        {
            placed = new List<Vector3>();
            placedFor = null;
            string where = p.location == CraftLocation.Furnace ? "IN THE FURNACE" : p.location == CraftLocation.Melt ? "IN THE CRUCIBLE" : "NOT ON THE BENCH";
            CraftArt.Label(c, Tray.center.x - CraftArt.LabelWidth(where) * 0.5f, Tray.center.y - 5f, where, CraftArt.LabelDim);
            return;
        }
        Lay(p);
        CraftArt.DrawStones(c, Rules, p, placed);
        DrawDirt(c, p);
        Vector2 h = ui.Hover;
        hover = StoneAt(h);
        // The stones the sieve lets through: ringed white with a thorough shaking of these hands (as far as this shaking
        // went, while it is shaken), dotted with what more there is to gain (a lazy shaking's, or a thorough one's).
        if (gesture == Gesture.Sieve || SieveAt.Contains(h) || Time.unscaledTime < meshShownUntil)
        {
            float solid = gesture == Gesture.Sieve ? Thoroughness() : BestShaking, dotted = gesture == Gesture.Sieve ? BestShaking : 0.001f;
            for (int i = 0; i < placed.Count && i < p.ore.fragments.Count; i++)
            {
                double size = p.ore.fragments[i].size;
                Vector3 f = placed[i];
                if (size < meshValue * CraftWorkshop.SieveReach(solid)) c.Ring(f.x, f.y, f.z + 2.5f, 1.2f, new Color(1f, 1f, 1f, 0.8f));
                if (size < meshValue * CraftWorkshop.SieveReach(dotted)) DottedRing(c, f.x, f.y, f.z + 6f, new Color(1f, 1f, 1f, 0.7f));
            }
        }
        if (p.ore.temperature > 300)
        {
            Color glow = CraftArt.Heat(p.ore.temperature, ui.Dark);
            foreach (Vector3 f in placed) c.Disc(f.x, f.y, f.z, glow);
        }
        // The stone the hammer is raised over (the one struck when it is let go over it), else the one under the hand.
        int aimed = gesture == Gesture.Hammer ? workStone : hover;
        if (aimed >= 0 && aimed < placed.Count && gesture != Gesture.Pick)
        {
            Vector3 f = placed[aimed];
            c.Ring(f.x, f.y, f.z + 3, 2, gesture == Gesture.Hammer && hover != workStone ? Harm : Mark);
            if (gesture == Gesture.Hammer && (hammer.Raised || raisePending))
                DrawBlow(c, p, f);
        }
        if (gesture == Gesture.Scrub && scrubbed != null && workStone >= 0 && workStone < placed.Count)
        {
            Vector3 f = placed[workStone];
            Vector2 at = OverStone(f, 90f, 6f + 4f + CraftArt.LabelHeight);
            Meter(c, at.x, at.y, 90f, 6f, (float)scrubbed.cover.Covered(Brush), new Color(0.6f, 0.85f, 1f), (0.999f, 1f, GoodBand));
            CraftArt.Label(c, at.x, at.y + 9f, "BRUSHED", CraftArt.LabelInk);
        }
        if (gesture == Gesture.Pick && workStone >= 0 && workStone < placed.Count)
        {
            Vector3 f = placed[workStone];
            c.Disc(f.x, f.y, f.z, new Color(0.06f, 0.05f, 0.05f, 0.7f)); // its place, empty
            c.Disc(dragAt.x, dragAt.y, heldRadius, heldColour);
            c.Ring(dragAt.x, dragAt.y, heldRadius, 1f, Heap.Contains(dragAt) ? Good : Mark);
        }
        // The brush in the hand.
        if (IsBrush && Tray.Contains(h))
        {
            float wob = gesture == Gesture.Scrub ? Mathf.Sin(scrubPhase) * 3f : 0f;
            Color bristle = tool == Tool.Soft ? new Color(0.78f, 0.7f, 0.55f) : tool == Tool.Stiff ? new Color(0.25f, 0.2f, 0.15f) : new Color(0.65f, 0.65f, 0.68f);
            c.Rect(h.x - 14 + wob, h.y + 2, h.x + 14 + wob, h.y + 10, new Color(0.45f, 0.3f, 0.17f));
            for (int k = 0; k < 8; k++) c.Line(h.x - 12 + k * 3.4f + wob, h.y + 2, h.x - 12 + k * 3.4f + wob * 1.5f, h.y - 5, bristle, 1f);
            c.Line(h.x + 14 + wob, h.y + 6, h.x + 30 + wob, h.y + 16, new Color(0.45f, 0.3f, 0.17f), 3f);
        }
    }

    /// <summary>
    /// Where the stones lie on the tray (<see cref="CraftOres.Tray"/>): each at its place on the bench, as big as it is,
    /// pushed apart from its neighbours - from where it lay last time, so that only a change moves them. Laid again only
    /// when the lump changes how it lies; the drawing remembers which lump and which layout it shows.
    /// </summary>
    private void Lay(CraftPiece p)
    {
        int stamp = CraftOres.Layout(p);
        if (layFor != p.id)
        {
            lay.Clear();
            sides.Clear();
            layFor = p.id;
            layStamp = int.MinValue;
        }
        if (stamp != layStamp || placed.Count != p.ore.fragments.Count || placedFor != p.id)
        {
            var next = new List<Vector3>(p.ore.fragments.Count);
            foreach ((double x, double y, double r) in CraftOres.Tray(p.ore.fragments, Tray.width, Tray.height, StonePerCm, StoneLeast, StoneMost, lay))
                next.Add(new Vector3(Tray.xMin + (float)x, Tray.yMin + (float)y, (float)r));
            placed = next;
            layStamp = stamp;
            // What the brushes went over is kept only for the stones still lying where they were.
            var keep = new HashSet<(double, double)>();
            foreach (OreFragment f in p.ore.fragments) keep.Add((f.x, f.y));
            var gone = new List<(double, double)>();
            foreach (var k in sides.Keys) if (!keep.Contains(k)) gone.Add(k);
            foreach (var k in gone) sides.Remove(k);
        }
        placedFor = p.id;
        placedLayout = stamp;
    }

    /// <summary>A meter <paramref name="w"/> × <paramref name="h"/> over a stone - under it when there is no room above - kept inside the tray.</summary>
    private static Vector2 OverStone(Vector3 f, float w, float h)
    {
        float y = f.y + f.z + 10f;
        if (y + h > Tray.yMax) y = f.y - f.z - 10f - h;
        return new Vector2(Mathf.Clamp(f.x - w * 0.5f, Tray.xMin, Tray.xMax - w), Mathf.Clamp(y, Tray.yMin, Tray.yMax - h));
    }

    /// <summary>What is left on side <paramref name="i"/> of a stone of what the brushes take there (dirt or rind), as they went over it.</summary>
    private static double LeftOn(CraftBrushCover cover, int i, double[] takes)
    {
        double left = 1;
        for (int b = 0; b < takes.Length; b++) left *= 1 - takes[b] * cover.Done(i, (CraftOreBrush)b);
        return left;
    }

    /// <summary>What is left of what the brushes take (dirt or rind) on a stone they went over evenly, as far as the station counts it.</summary>
    private static double LeftEvenly(OreFragment f, double[] takes)
    {
        double left = 1;
        for (int b = 0; b < takes.Length; b++) left *= 1 - takes[b] * f.Brushed((CraftOreBrush)b);
        return left;
    }

    private static double[] TakesDirt(CraftOreDefinition o) => new[] { CraftOres.BrushOnDirt(CraftOreBrush.Soft, o), CraftOres.BrushOnDirt(CraftOreBrush.Stiff, o), CraftOres.BrushOnDirt(CraftOreBrush.Wire, o) };
    private static readonly double[] TakesRind = { CraftOres.BrushOnRind(CraftOreBrush.Soft), CraftOres.BrushOnRind(CraftOreBrush.Stiff), CraftOres.BrushOnRind(CraftOreBrush.Wire) };

    /// <summary>The specks a stone of this size shows for its dirt before any brush went over it.</summary>
    private static int Specks(OreFragment f, float radius, double dirtLeft) =>
        Mathf.Min(70, Mathf.RoundToInt((float)(f.dirt / Math.Max(1e-6, dirtLeft)) * 300f * Mathf.Clamp01(radius / 30f + 0.25f)));

    /// <summary>
    /// What a brush went over, side by side round <paramref name="f"/>, as the station counts it: kept while the window
    /// and the station agree; started again from the stone itself (gone over evenly, since where is not known) when
    /// they do not - a brush of another hand, or of the 3D work place.
    /// </summary>
    private StoneSides SidesOf(CraftPiece p, OreFragment f, float radius)
    {
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        if (sides.TryGetValue((f.x, f.y), out StoneSides s))
        {
            bool agrees = true;
            for (int b = 0; b < 3 && agrees; b++) agrees = Math.Abs(s.cover.Covered((CraftOreBrush)b) - f.Brushed((CraftOreBrush)b)) < 0.02;
            if (agrees) return s;
        }
        s = new StoneSides();
        var area = new double[Sides];
        var dirt = new double[Sides];
        var rind = new double[Sides];
        for (int i = 0; i < Sides; i++) { area[i] = 1; dirt[i] = f.dirt; rind[i] = f.gangue * f.RindShare; }
        double[] takesDirt = TakesDirt(o);
        s.cover.Reset(area, dirt, rind, takesDirt, TakesRind, new[] { f.brushedSoft, f.brushedStiff, f.brushedWire });
        s.specks = Specks(f, radius, LeftEvenly(f, takesDirt));
        sides[(f.x, f.y)] = s;
        return s;
    }

    /// <summary>
    /// The dirt (brown specks, as much as each stone holds) and the weathered rind (a grey band round it). Each side of a
    /// stone shows what the brushes left there - as the station counts it, at the window's pace - so a side scrubbed stays
    /// clean and one never touched keeps its dirt, while the brush works and after.
    /// </summary>
    private void DrawDirt(CraftPixels c, CraftPiece p)
    {
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        double[] takesDirt = TakesDirt(o);
        var rindColour = new Color(0.46f, 0.43f, 0.38f, 0.95f);
        var dirtColour = new Color(0.3f, 0.21f, 0.12f, 0.95f);
        for (int i = 0; i < placed.Count && i < p.ore.fragments.Count; i++)
        {
            OreFragment f = p.ore.fragments[i];
            Vector3 q = placed[i];
            sides.TryGetValue((f.x, f.y), out StoneSides s);
            double dirtEven = LeftEvenly(f, takesDirt), rindEven = LeftEvenly(f, TakesRind);
            // The rind: a band round the stone, side by side, as thick as the rind left there.
            double rindNow = f.gangue * f.RindShare;
            for (int k = 0; k < Sides; k++)
            {
                float rind = (float)(rindNow * (s != null ? LeftOn(s.cover, k, TakesRind) / Math.Max(1e-6, rindEven) : 1));
                if (rind <= 0.02f) continue;
                float t = Mathf.Clamp(rind * 45f, 1.5f, q.z * 0.4f), rr = q.z - 1f - t * 0.5f;
                for (int seg = 0; seg < 3; seg++)
                {
                    float a0 = (k + seg / 3f) * 2f * Mathf.PI / Sides, a1 = (k + (seg + 1) / 3f) * 2f * Mathf.PI / Sides;
                    c.Line(q.x + Mathf.Cos(a0) * rr, q.y + Mathf.Sin(a0) * rr, q.x + Mathf.Cos(a1) * rr, q.y + Mathf.Sin(a1) * rr, rindColour, t);
                }
            }
            // The dirt: the specks the stone had before any brush, each kept as the side it is on was left.
            int specks = s != null ? s.specks : Specks(f, q.z, dirtEven);
            int seed = CraftArt.StoneSeed(f);
            for (int k = 0; k < specks; k++)
            {
                float a = (float)CraftMath.Noise(seed + k * 97, 3) * 2f * Mathf.PI, r = q.z * 0.85f * Mathf.Sqrt((float)CraftMath.Noise(seed + k * 31, 5));
                int side = Mathf.FloorToInt(a / (2f * Mathf.PI) * Sides) % Sides;
                double left = s != null ? LeftOn(s.cover, side, takesDirt) : dirtEven;
                if (CraftMath.Noise(seed + k, 11) >= left) continue; // brushed off there
                c.Disc(q.x + Mathf.Cos(a) * r, q.y + Mathf.Sin(a) * r, Mathf.Clamp(q.z * 0.06f, 1.2f, 2.6f), dirtColour);
            }
        }
    }

    /// <summary>
    /// Over the stone the hammer is raised above: how hard the blow lands if let go now - as it would land, a tired arm
    /// lighter, and sinking once it is held so long that the arm wavers - over the bands of what it does to this ore: in
    /// the grey it only chips, then it splits in two, three, four (no more pieces than the bench has room for).
    /// </summary>
    private void DrawBlow(CraftPixels c, CraftPiece p, Vector3 f)
    {
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        float hard = 0.4f + (o != null ? o.hardness / 10f : 0.5f);
        int room = CraftOres.MostPieces(p); // the most pieces a blow can split a stone into now
        bool full = room < 2;
        float b2 = full ? 1f : Mathf.Clamp01(0.25f * hard / 1.5f), b3 = room >= 3 ? Mathf.Clamp01(0.7f * hard / 1.5f) : 1f, b4 = room >= 4 ? Mathf.Clamp01(1.2f * hard / 1.5f) : 1f;
        const float w = 96f, h = 8f, named = 40f; // the meter's name after it
        Vector2 at = OverStone(f, w + named, h + 12f);
        float force = (float)hammer.Expected(Hands);
        CraftArt.Label(c, at.x + w + 6f, at.y + h * 0.5f - 5f, "BLOW", CraftArt.LabelInk);
        Meter(c, at.x, at.y, w, h, force, hammer.Wavers ? Harm : new Color(1f, 0.8f, 0.4f),
            (0f, b2, IdleBand), (b2, b3, new Color(0.35f, 0.45f, 0.3f)), (b3, b4, new Color(0.42f, 0.58f, 0.32f)), (b4, 1f, new Color(0.52f, 0.72f, 0.36f)));
        foreach ((float from, string n, int pieces) in new[] { (b2, "2", 2), (b3, "3", 3), (b4, "4", 4) })
            if (room >= pieces && from < 0.97f) CraftArt.Label(c, at.x + 1f + from * (w - 2f) + 2f, at.y + h + 2f, n, CraftArt.LabelInk);
        if (raisePending) CraftArt.Label(c, at.x, at.y + h + 2f, "...", CraftArt.LabelDim);
    }

    private void DrawPan(CraftPixels c)
    {
        Vector2 o = Pan.center;
        c.Disc(o.x, o.y, 66, new Color(0.35f, 0.3f, 0.25f));
        c.Disc(o.x, o.y, PanWater, Water);
        CraftArt.Label(c, Pan.xMin + 2f, Pan.yMax - 2f, "PAN", CraftArt.LabelDim);
        if (gesture != Gesture.Pan)
            return;
        // A ripple where the hand swirls, and how far round and how evenly it has gone.
        float r = Vector2.Distance(panAt, o);
        if (r <= PanWater) c.Ring(o.x, o.y, Mathf.Max(4f, r), 1.5f, new Color(1f, 1f, 1f, 0.45f));
        float turns = Mathf.Abs(panTravel) / (2f * Mathf.PI);
        Gauge(c, Pan.xMin, 18f, Pan.width, 6f, "TURNS", 46f, turns / 3f, new Color(0.6f, 0.85f, 1f), (1f / 3f, 1f, GoodBand));
        Gauge(c, Pan.xMin, 6f, Pan.width, 6f, "EVEN", 46f, PanEvenness(), new Color(0.5f, 0.85f, 0.6f), (0f, 0.45f, HarmBand), (0.45f, 0.7f, FairBand), (0.7f, 1f, GoodBand));
    }

    private void DrawSieve(CraftPixels c)
    {
        Rect s = new Rect(SieveAt.xMin + sieveShift, SieveAt.yMin, SieveAt.width, SieveAt.height);
        c.Rect(s.xMin, s.yMin, s.xMax, s.yMax, Wood);
        c.Rect(s.xMin + 8, s.yMin + 8, s.xMax - 8, s.yMax - 8, new Color(0.08f, 0.07f, 0.06f));
        float gap = Mathf.Lerp(6f, 22f, Mathf.InverseLerp(0.3f, 3f, meshValue)); // a finer mesh, closer wires
        for (float x = s.xMin + 8; x < s.xMax - 8; x += gap) c.Line(x, s.yMin + 8, x, s.yMax - 8, new Color(0.6f, 0.6f, 0.62f, 0.8f), 1f);
        for (float y = s.yMin + 8; y < s.yMax - 8; y += gap) c.Line(s.xMin + 8, y, s.xMax - 8, y, new Color(0.6f, 0.6f, 0.62f, 0.8f), 1f);
        CraftArt.Label(c, SieveAt.xMin + 2f, SieveAt.yMax + 4f, $"SIEVE {meshValue:0.0} CM", CraftArt.LabelDim);
        if (gesture != Gesture.Sieve)
            return;
        // The shakes counted (under the sieve, over the pan's name), and the fines falling just below the sieve - clear of both.
        Gauge(c, SieveAt.xMin, 182f, SieveAt.width, 6f, "SHAKES", Mathf.Clamp01(shakeTurns / 2 / 8f), Mark, (0f, 0.25f, IdleBand));
        for (int i = 0; i < Mathf.Min(20, shakeTurns / 2 * 3); i++)
            c.Disc(s.xMin + 14 + (i * 31 % 112), s.yMin - 2.5f - (i * 7 % 10) * 0.45f, 1.5f, new Color(0.55f, 0.45f, 0.35f));
    }

    private void DrawHeap(CraftPixels c)
    {
        for (int i = 0; i < 14; i++)
            c.Disc(Heap.xMin + 12 + (i * 37 % 116), Heap.yMin + 6 + (i * 13 % 22), 5 + i % 4, new Color(0.42f, 0.4f, 0.37f));
        bool aiming = gesture == Gesture.Pick;
        c.Frame(Heap.xMin, Heap.yMin, Heap.xMax, Heap.yMax, aiming ? (Heap.Contains(dragAt) ? Good : Mark) : new Color(0.3f, 0.28f, 0.26f), aiming ? 2f : 1f);
        CraftArt.Label(c, Heap.xMin + 2f, Heap.yMax + 4f, "WASTE HEAP", CraftArt.LabelDim);
    }

    /// <summary>The stone under <paramref name="at"/> on the tray as last drawn (-1: none).</summary>
    private int StoneAt(Vector2 at)
    {
        if (at.x < 0f) return -1;
        for (int i = 0; i < placed.Count; i++)
            if (Vector2.Distance(at, new Vector2(placed[i].x, placed[i].y)) <= placed[i].z + 2) return i;
        return -1;
    }

    private static bool InWater(Vector2 at) => Vector2.Distance(at, Pan.center) <= PanWater;

    // ------------------------------------------------------------------ the hands
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (phase)
        {
            case CraftPointer.Cancel:
                // Right-click puts down what is in hand: the hammer is lowered, a stone put back; scrubbing, swirling and
                // shaking count as far as they went.
                if (gesture == Gesture.Hammer) SayAbout(workPiece, "The hammer is lowered.");
                else if (gesture == Gesture.Pick) SayAbout(workPiece, "Put back.");
                EndGesture(true);
                return;
            case CraftPointer.Scroll:
                if (gesture == Gesture.None) PickTool((Tool)(((int)tool + (scroll > 0 ? 1 : ToolNames.Length - 1)) % ToolNames.Length));
                return;
            case CraftPointer.Down:
                Begin(pos);
                return;
            case CraftPointer.Drag:
                Continue(pos);
                return;
            case CraftPointer.Up:
                Release(pos);
                return;
        }
    }

    private void Begin(Vector2 pos)
    {
        EndGesture(true); // a release that never came: what was under way is finished first
        CraftPiece p = Selected;
        bool onWork = Tray.Contains(pos) || InWater(pos) || SieveAt.Contains(pos);
        if (p == null || p.kind != CraftPieceKind.Ore)
        {
            if (onWork) Said = "Pick a lump of ore from the list first.";
            return;
        }
        if (p.location != CraftLocation.Air)
        {
            if (onWork) Said = Where(p);
            return;
        }
        if (p.ore.temperature > 120)
        {
            if (onWork) Said = "It is too hot to handle: let it cool on the bench.";
            return;
        }
        workPiece = p.id;
        workLayout = CraftOres.Layout(p);
        if (InWater(pos))
        {
            gesture = Gesture.Pan;
            panAngle = float.NaN;
            panTravel = panAdvancing = 0f;
            panMoves = panN = panWn = 0;
            panR = panR2 = panW = panW2 = 0;
            panLastTime = ui.PointerTime;
            AddPanSample(pos);
            return;
        }
        if (SieveAt.Contains(pos))
        {
            gesture = Gesture.Sieve;
            shakeFrom = shakeExtreme = pos.x;
            shakeDir = shakeTurns = 0;
            shakeLastTurn = -1f;
            shakeGaps.Clear();
            return;
        }
        int stone = StoneAt(pos);
        if (stone < 0)
            return;
        // The stone is aimed at in the drawing the hand saw: if the bench has changed since (another hand's blow, a
        // stone thrown out, the answer to the last one), the drawing is stale and the aim is not taken.
        if (placedFor != p.id || placedLayout != workLayout || stone >= p.ore.fragments.Count)
        {
            SayAbout(p.id, "The stones have just moved: look again and pick the stone anew.");
            workPiece = null;
            return;
        }
        workStone = stone;
        OreFragment f = p.ore.fragments[stone];
        switch (tool)
        {
            case Tool.Hammer:
                gesture = Gesture.Hammer;
                if (Time.unscaledTime - lastBlowAt >= Recovery) hammer.Raise();
                else raisePending = true; // it is still coming up from the last blow
                break;
            case Tool.PickOut:
            {
                gesture = Gesture.Pick;
                dragAt = pos;
                // The stone in the hand looks as it did when it was picked up, whatever the bench does meanwhile.
                CraftOreDefinition o = Rules.Ore(p.ore.ore);
                heldColour = o != null ? Color.Lerp(o.gangueColor, o.color, Mathf.Clamp01((float)(f.mineral / Math.Max(0.02, o.mineralMax))) * 0.75f) : new Color(0.5f, 0.45f, 0.4f);
                heldRadius = placed[stone].z;
                break;
            }
            default:
                if (f.Brushed(Brush) >= 0.999)
                {
                    SayAbout(p.id, CraftOres.BrushedAll(Brush)); // as the station would say: it takes no more off it
                    workStone = -1;
                    workPiece = null;
                    return;
                }
                gesture = Gesture.Scrub;
                scrubbed = SidesOf(p, f, placed[stone].z);
                scrubbed.cover.Begin();
                for (int i = 0; i < Sides; i++) scrubbed.start[i] = scrubbed.cover.Done(i, Brush);
                scrubbed.dirtAtStart = scrubbed.cover.MeanDirt;
                scrubStart = ui.PointerTime;
                scrubPhase = scrubTravel = 0f;
                lastScrub = pos;
                break;
        }
    }

    private void Continue(Vector2 pos)
    {
        switch (gesture)
        {
            case Gesture.Pick:
                dragAt = pos;
                break;
            case Gesture.Scrub:
                ScrubAt(pos);
                break;
            case Gesture.Pan:
                AddPanSample(pos);
                break;
            case Gesture.Sieve:
                ShakeAt(pos.x, ui.PointerTime);
                sieveShift = Mathf.Clamp((pos.x - SieveAt.center.x) * 0.15f, -10f, 10f);
                break;
        }
    }

    private void Release(Vector2 pos)
    {
        switch (gesture)
        {
            case Gesture.Hammer:
                Strike(StoneAt(pos) == workStone); // it comes down on the stone it was raised over, if it is let go over it
                break;
            case Gesture.Pick:
                if (Heap.Contains(pos)) ThrowOut();
                else
                {
                    SayAbout(workPiece, "Put back.");
                    Reset();
                }
                break;
            case Gesture.Scrub:
                SendScrub();
                break;
            case Gesture.Pan:
                SendPan();
                break;
            case Gesture.Sieve:
                SendSieve();
                break;
        }
    }

    /// <summary>
    /// Ends the gesture under way: the hammer lowered and a stone put back (they do nothing unless let go where they
    /// act); scrubbing, swirling and shaking sent as far as they went when <paramref name="keep"/>.
    /// </summary>
    private void EndGesture(bool keep)
    {
        switch (gesture)
        {
            case Gesture.Scrub when keep: SendScrub(); break;
            case Gesture.Pan when keep: SendPan(); break;
            case Gesture.Sieve when keep: SendSieve(); break;
            default: Reset(); break;
        }
    }

    /// <summary>Nothing in hand any more (the counters zeroed before anything is sent).</summary>
    private void Reset()
    {
        gesture = Gesture.None;
        workPiece = null;
        workStone = -1;
        raisePending = false;
        hammer.Lower();
        scrubbed = null;
        sieveShift = 0f;
        shakeTurns = 0;
        panTravel = 0f;
        LiveDraw = false;
    }

    // ---- the hammer
    /// <summary>The hammer comes down on the stone it was raised over - if it was let go over it (<paramref name="overIt"/>); else it is lowered.</summary>
    private void Strike(bool overIt)
    {
        string id = workPiece;
        int layout = workLayout, stone = workStone;
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        if (!hammer.Raised)
        {
            SayAbout(id, "The hammer was still coming up: hold until it is raised, then let go.");
            Reset();
            return;
        }
        if (!overIt || p == null || stone < 0 || stone >= p.ore.fragments.Count)
        {
            SayAbout(id, "Let go over the stone you raised the hammer over (ringed) to strike it: the hammer is lowered.");
            Reset();
            return;
        }
        CraftBlow b = hammer.Release(0.5, 0, Hands);
        lastBlowAt = Time.unscaledTime;
        int before = p.ore.fragments.Count;
        bool full = before >= CraftOres.MaxFragments;
        Reset();
        Send(new CraftRequest(CraftRequestKind.Crush, id) { i0 = stone, a = (float)(b.force * 1.5), i2 = layout }, (ok, why) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(id);
            if (q == null) return;
            int now = q.ore.fragments.Count;
            SayAbout(id, (b.slipped ? "The hammer slipped. " : "") + (now > before ? $"It splits in {now - before + 1}."
                : full ? $"It only chips: the bench holds all the stones it can ({CraftOres.MaxFragments}) - sieve the fines off or pick stones out first."
                : b.rushed > 0.2 ? "It only chips: the hammer was held up so long the arm wavered - let go once the bar is in the band."
                : "It only chips: let the bar fill into the 2, 3 or 4 band to split it."));
        });
    }

    // ---- the brushes
    private void ScrubAt(Vector2 pos)
    {
        if (scrubbed == null || workStone < 0 || workStone >= placed.Count)
            return;
        Vector3 f = placed[workStone];
        float moved = Vector2.Distance(pos, lastScrub);
        lastScrub = pos;
        if (Vector2.Distance(pos, new Vector2(f.x, f.y)) > f.z + 4f || moved < 0.5f)
            return;
        // No faster than a brush can go over a stone (the station counts no more), a tired arm scrubbing less hard; bigger
        // stones take longer to go over. Each side is gone over as the station counts it - a careful share of what the
        // drawing shows (the window's pace) - and once at most in a going-over.
        float pace = WindowPace;
        if (scrubbed.cover.GoingOver(Brush) / pace < CraftRequestGuard.BrushCoverPerSecond * Mathf.Max(0f, ui.PointerTime - scrubStart))
        {
            int side = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(pos.y - f.y, pos.x - f.x), 2f * Mathf.PI) / (2f * Mathf.PI) * Sides) % Sides;
            double room = scrubbed.start[side] + pace - scrubbed.cover.Done(side, Brush);
            if (room > 0)
                scrubbed.cover.Scrub(side, Brush, Math.Min(room, moved / Mathf.Max(8f, f.z * 6f) * (0.45f + 0.55f * (float)Hands.Strength) * pace));
        }
        scrubPhase += moved * 0.3f;
        scrubTravel += moved;
        while (scrubTravel >= 30f)
        {
            scrubTravel -= 30f;
            Hands.Move(tool == Tool.Wire ? 0.35 : 0.2);
        }
    }

    private void SendScrub()
    {
        StoneSides s = scrubbed;
        CraftOreBrush brush = Brush;
        double going = s != null ? s.cover.GoingOver(brush) : 0, covered = s != null ? s.cover.Covered(brush) : 0;
        float seconds = Mathf.Max(0.05f, ui.PointerTime - scrubStart), pace = WindowPace;
        string id = workPiece;
        int stone = workStone, layout = workLayout;
        Reset();
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        if (going <= 0.005 || p == null || stone < 0 || stone >= p.ore.fragments.Count)
            return;
        OreFragment f = p.ore.fragments[stone];
        (double, double) at = (f.x, f.y);
        double dirtBefore = f.mass * f.dirt;
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        // What the station counts of it is the window's pace of what is sent: so much is sent that it counts what the sides show.
        Send(new CraftRequest(CraftRequestKind.BrushOre, id) { i0 = stone, i1 = (int)brush, a = (float)Math.Min(1, going / pace), b = seconds, i2 = layout }, (ok, why) =>
        {
            if (!ok)
            {
                sides.Remove(at); // not counted: the sides start again from the stone
                return;
            }
            CraftPiece q = Ws.Piece(id);
            if (q == null || stone >= q.ore.fragments.Count) return;
            OreFragment g = q.ore.fragments[stone];
            double gone = dirtBefore > 1e-9 ? CraftMath.Clamp01(1 - g.mass * g.dirt / dirtBefore) : 0;
            SayAbout(id, $"The {CraftOres.BrushName(brush)} has been over {covered * 100:0}% of the stone: {gone * 100:0}% of its dirt came off this time"
                + (brush == CraftOreBrush.Wire ? ", and the weathered rind with a little ore." : ".")
                + (covered < 0.995 ? " Go over the rest of it." : "")
                + (clay && brush == CraftOreBrush.Soft ? " The soft brush barely shifts caked clay: the stiff one would." : ""));
        });
    }

    // ---- picking a stone out
    private void ThrowOut()
    {
        string id = workPiece;
        int stone = workStone, layout = workLayout;
        Reset();
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        if (p == null || stone < 0 || stone >= p.ore.fragments.Count)
            return;
        OreFragment f = p.ore.fragments[stone];
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        string looks = Richness(f, o);
        double grams = f.mass;
        bool last = p.ore.fragments.Count == 1;
        Send(new CraftRequest(CraftRequestKind.DiscardFragment, id) { i0 = stone, i2 = layout }, (ok, why) =>
        {
            if (ok) SayAbout(id, $"Thrown on the waste heap: a {CraftText.Mass(grams)} stone, {looks}." + (last ? " Nothing is left of the lump." : ""));
        });
    }

    /// <summary>How rich a stone looks (against the richest its ore comes).</summary>
    private static string Richness(OreFragment f, CraftOreDefinition o)
    {
        double rich = o != null ? f.mineral / Math.Max(0.02, o.mineralMax) : f.mineral;
        return rich > 0.66 ? "rich with ore" : rich > 0.33 ? "with some ore in it" : "barren-looking";
    }

    // ---- the pan
    private void AddPanSample(Vector2 pos)
    {
        float t = ui.PointerTime;
        Vector2 d = pos - Pan.center;
        float r = d.magnitude;
        panAt = pos;
        float dt = Mathf.Clamp(t - panLastTime, 0f, 0.1f);
        panLastTime = t;
        if (r > PanWater || r < 3f)
        {
            panAngle = float.NaN; // out of the water: the swirl takes up again where it comes back
            return;
        }
        // The radius the hands hold wavers with their tremble (tired or unpractised hands swirl less evenly).
        double held = r * (1 + 2 * Hands.Tremble(700 + panN % 97));
        panR += held;
        panR2 += held * held;
        panN++;
        float a = Mathf.Atan2(d.y, d.x);
        if (float.IsNaN(panAngle))
        {
            panAngle = a;
            return;
        }
        float da = Mathf.DeltaAngle(panAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        panAngle = a;
        panTravel += da;
        if (dt > 1e-4f && Mathf.Abs(da) > 1e-4f)
        {
            if (Mathf.Sign(da) == Mathf.Sign(panTravel)) panAdvancing += dt; // the water goes round only while the swirl goes on
            double w = Mathf.Abs(da) / dt;
            panW += w;
            panW2 += w * w;
            panWn++;
        }
        // The arm works the pan: a movement every half turn.
        while (Mathf.Abs(panTravel) >= (panMoves + 1) * Mathf.PI)
        {
            panMoves++;
            Hands.Move(0.15 * flowValue);
        }
    }

    /// <summary>
    /// How well the pan is swirled (0-1): wide circles round the pan (not a jiggle in one spot), an even radius (the
    /// hands' tremble wavers it), an even pace.
    /// </summary>
    private float PanEvenness()
    {
        if (panN < 6)
            return 0f;
        double mean = panR / panN, sd = Math.Sqrt(Math.Max(0, panR2 / panN - mean * mean));
        float radius = Mathf.Clamp01(1f - (float)(sd / Math.Max(1, mean)) * 2.5f);
        float pace = 0.5f;
        if (panWn >= 6)
        {
            double wm = panW / panWn, wsd = Math.Sqrt(Math.Max(0, panW2 / panWn - wm * wm));
            pace = Mathf.Clamp01(1f - (float)(wsd / Math.Max(1e-3, wm)) * 0.8f);
        }
        float wide = (float)CraftMath.Smooth(10, 30, mean);
        return radius * (0.6f + 0.4f * pace) * wide;
    }

    private void SendPan()
    {
        float turns = Mathf.Abs(panTravel) / (2f * Mathf.PI), technique = PanEvenness(), seconds = Mathf.Min(30f, 2f * panAdvancing), water = flowValue;
        string id = workPiece;
        Reset();
        if (turns < 1f)
        {
            SayAbout(id, "Swirl it round: a still pan washes nothing (one whole turn at the least).");
            return;
        }
        CraftPiece p = id != null ? Ws.Piece(id) : null;
        if (p == null || seconds <= 0f)
            return;
        double massBefore = p.ore.Mass, oreBefore = p.ore.MineralMass;
        Send(new CraftRequest(CraftRequestKind.Wash, id) { a = water, b = seconds, c = technique }, (ok, why) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(id);
            double gone = massBefore - (q != null ? q.ore.Mass : 0), ore = oreBefore - (q != null ? q.ore.MineralMass : 0);
            SayAbout(id, (technique > 0.7f ? "Steady circles: the mud washes off cleanly." : technique > 0.45f ? "An uneven swirl: some ore went out with the mud." : "A jerky swirl: ore went out with the mud.")
                + $" {CraftText.Mass(Math.Max(0, gone))} went with the water, {CraftText.Mass(Math.Max(0, ore))} of it ore"
                + (water > 0.75f ? " - a strong flow is quick, but it carries ore off with the rock." : water < 0.3f ? " - a gentle flow is slow, but it keeps the heavy grains." : "."));
        });
    }

    // ---- the sieve
    /// <summary>
    /// A run of the sieve one way: a shake counts when the hand comes back <see cref="ShakeTravel"/> from the furthest it
    /// went and that run was at least as long (a wiggle is not a shake).
    /// </summary>
    private void ShakeAt(float x, float time)
    {
        if (shakeDir == 0)
        {
            if (Mathf.Abs(x - shakeFrom) >= 4f) { shakeDir = x > shakeFrom ? 1 : -1; shakeExtreme = x; }
            return;
        }
        if ((x - shakeExtreme) * shakeDir > 0f)
        {
            shakeExtreme = x;
            return;
        }
        if (Mathf.Abs(x - shakeExtreme) < ShakeTravel)
            return;
        if (Mathf.Abs(shakeExtreme - shakeFrom) >= ShakeTravel)
        {
            shakeTurns++;
            if (shakeLastTurn >= 0f) shakeGaps.Add(time - shakeLastTurn);
            shakeLastTurn = time;
        }
        shakeFrom = shakeExtreme;
        shakeExtreme = x;
        shakeDir = -shakeDir;
    }

    /// <summary>0-1: shakes of an even length of time.</summary>
    private float ShakeRhythm()
    {
        if (shakeGaps.Count < 3) return 0.5f;
        float mean = 0f, var = 0f;
        foreach (float g in shakeGaps) mean += g;
        mean /= shakeGaps.Count;
        foreach (float g in shakeGaps) var += (g - mean) * (g - mean);
        var /= shakeGaps.Count;
        return Mathf.Clamp01(1f - Mathf.Sqrt(var) / Mathf.Max(0.05f, mean) * 1.5f);
    }

    /// <summary>How thorough the shaking so far is (0-1): enough shakes at an even pace - never more than these hands can give (the station allows no more).</summary>
    private float Thoroughness() => Mathf.Min(BestShaking, Mathf.Clamp01(shakeTurns / 2 / 8f) * 0.6f + 0.4f * ShakeRhythm());

    private void SendSieve()
    {
        int shakes = shakeTurns / 2;
        float thorough = Thoroughness(), m = meshValue;
        string id = workPiece;
        Reset();
        if (shakes < 2)
        {
            SayAbout(id, "Shake it back and forth a few times, a hand's width each way: only a shaken sieve sorts.");
            return;
        }
        if (id == null || Ws.Piece(id) == null)
            return;
        Send(new CraftRequest(CraftRequestKind.Sieve, id) { a = m, b = thorough }, (ok, why) =>
        {
            if (!ok) return;
            CraftPiece fines = null;
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated) fines = Ws.Piece(e.Piece) ?? fines;
            SayAbout(id, $"{shakes} shakes{(thorough > 0.8f ? ", even and thorough" : thorough > 0.55f ? "" : ", lazy and uneven")}: "
                + (fines != null ? $"{fines.ore.fragments.Count} stones ({CraftText.Mass(fines.ore.Mass)}) fell through the {m:0.0} cm mesh - they lie on the bench as a lot of their own."
                : "the small stones fell through."));
        });
    }

    // ------------------------------------------------------------------ words
    public override void Refresh()
    {
        if (info == null) return;
        CraftPiece p = Selected;
        string how;
        if (p == null || p.kind != CraftPieceKind.Ore)
            how = "Pick a lump of ore from the list to work it here (put ore from your bag on the station first).";
        else if (p.location != CraftLocation.Air)
            how = Where(p);
        else if (p.ore.temperature > 120)
            how = "It is too hot to handle: let it cool on the bench.";
        else
            how = Doing(p);
        info.text = (Said + " " + how).Trim();
    }

    /// <summary>What the hands are doing now, or how the tool in hand is used (with the stone under the pointer).</summary>
    private string Doing(CraftPiece p)
    {
        switch (gesture)
        {
            case Gesture.Hammer:
                return raisePending ? "The hammer is coming up from the last blow..."
                    : hammer.Wavers ? "<color=#ff8a70>Held too long: the arm wavers and the blow weakens - let go now.</color>"
                    : hover != workStone ? "Raised over the ringed stone: let go over it to strike it (let go elsewhere and the hammer is lowered)."
                    : "Raised: let go over the stone to strike. The bar fills as the blow gathers: grey only chips, the 2, 3 and 4 bands split it into that many.";
            case Gesture.Scrub:
            {
                double covered = scrubbed != null ? scrubbed.cover.Covered(Brush) : 0, dirt = scrubbed != null && scrubbed.dirtAtStart > 1e-9 ? 1 - scrubbed.cover.MeanDirt / scrubbed.dirtAtStart : 0;
                return $"<b>Scrubbing</b> with the {CraftOres.BrushName(Brush)}: {covered * 100:0}% of the stone gone over - about {Math.Max(0, dirt) * 100:0}% of its dirt comes off this time. Go all round it.";
            }
            case Gesture.Pick:
                return "Drag it onto the waste heap (top right) and let go to throw it out; let go anywhere else to put it back.";
            case Gesture.Pan:
                return $"<b>Swirling</b>: {Mathf.Abs(panTravel) / (2f * Mathf.PI):0.0} turns, the circles {PanEvenness() * 100:0}% even (a wide, steady circle at an even pace keeps the heavy ore). Let go to finish.";
            case Gesture.Sieve:
                return $"<b>Shaking</b>: {shakeTurns / 2} shakes - the stones ringed white fall through as it is, the dotted ones with more even shakes (eight, a hand's width each way).";
        }
        string stone = "";
        float best = BestShaking;
        if (hover >= 0 && hover < p.ore.fragments.Count)
        {
            OreFragment f = p.ore.fragments[hover];
            string sieve = f.size < meshValue * CraftWorkshop.SieveReach(best) ? $" - it falls through the {meshValue:0.0} cm sieve"
                : f.size < meshValue ? $" - only just under the {meshValue:0.0} cm mesh: a shaking may leave it in the sieve" : "";
            stone = $" This stone: {f.size:0.0} cm, {Richness(f, Rules.Ore(p.ore.ore))}{(f.dirt > 0.05 ? ", dirty" : "")}{sieve}.";
        }
        int room = CraftOres.MostPieces(p); // the most pieces a blow can split a stone into now
        switch (tool)
        {
            case Tool.Hammer:
                return (room < 2 ? $"The bench holds all the stones it can ({CraftOres.MaxFragments}): blows only chip now - sieve the fines off or pick stones out. "
                    : room < 4 ? $"The bench has room for {room - 1} more stone{(room - 1 == 1 ? "" : "s")}: a blow splits a stone in {room} at the most. " : "")
                    + "<b>Hammer</b>: hold on a stone, let go over it to strike - the bar over it shows what the blow will do. Pan: swirl in the water. Sieve: drag back and forth (the stones ringed white fall through)." + stone;
            case Tool.PickOut:
                return "<b>Pick out</b>: drag a barren stone onto the waste heap (top right) to throw it away." + stone;
            default:
                return $"<b>{ToolNames[(int)tool]}</b>: press on a stone and scrub back and forth all round it. Brown specks are dirt, a grey band the weathered rind; a side brushed stays brushed. Soft: loose dirt. Stiff: caked clay. Wire: the rind (and a little ore)." + stone;
        }
    }
}

/// <summary>
/// The bloomery furnace: charcoal and ore in layers, a draft from the bellows, flux to take the rock into slag. The
/// shaft is drawn in section: the charge, the glow, the slag pooling, the bloom growing at the bottom (or liquid metal,
/// if it runs). Keep it hot but not too hot, tap the slag when it chokes the draft, pull the bloom while it is soft.
/// <para>With the hands: drag the ore - or a casting to melt down, roast or cupel - from the bench up into the mouth of
/// the shaft to charge it, hold on the charcoal heap to shovel fuel in, hold over the mouth to scoop the flux chosen in,
/// hold on the tap hole to let the slag run, drag the bloom (or a bead of metal too small to pour, or a casting put in)
/// out of the shaft with the tongs. The bars: the air the bellows give, the slag (past its tick it chokes the draft).
/// Right-click puts down what is in hand. A test station has quick buttons.</para>
/// </summary>
public sealed class CraftFurnaceMode : CraftSmeltArea
{
    private enum Hold { None, Fuel, Flux, Tap }
    private enum Carry { None, Charge, Bloom, Bead, Piece }

    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private float airPending;
    private TMP_Dropdown fuel, flux;
    private TextMeshProUGUI info;
    private readonly List<string> fuels = new List<string>(), fluxes = new List<string>();
    private static readonly Rect Shaft = new Rect(240, 20, 240, 360), Mouth = new Rect(230, 340, 260, 50), TapHole = new Rect(510, 30, 36, 30),
        Heap = new Rect(20, 200, 160, 70), Bench = new Rect(520, 200, 180, 80), BellowsArea = new Rect(40, 60, 150, 90);
    private static readonly Vector2 BloomAt = new Vector2(360, 64), BeadAt = new Vector2(430, 42);
    private Hold hold;
    private float holdTime;
    private Carry carry;
    private string carried;
    private Vector2 dragAt;
    private bool pumped; // the press that pumped the bellows: its drag and release do nothing more
    private readonly List<(string id, Rect at)> charge = new List<(string id, Rect at)>();

    public override string Title => "Furnace";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Furnace);
    public override string Hint => "Drag ore from the bench into the mouth; hold on the heap to shovel fuel, on the mouth to add flux, on the tap hole to run the slag; pump the bellows; drag the bloom out. Right-click puts down.";

    public override void Build(RectTransform controls)
    {
        fluxes.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (Ws.FurnaceTakes(s)) fluxes.Add(s.id); // only what does something here: a furnace flux, a cupel
        RectTransform r1 = Row(controls);
        if (QuickBench)
            Btn(r1, "Charge the piece", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Furnace }), 140f, UITheme.ButtonStyle.Primary);
        Text(r1, "Fuel", 13f, 34f);
        fuel = FuelDrop(r1, fuels, 150f);
        if (QuickBench)
            Btn(r1, "Add fuel", () => AddFuel(), 90f);
        SettingSlider(r1, "Air vent", 0f, 1f, () => (float)Ws.Furnace.fire.damper, v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v, flag = true }),
            v => $"{v * 100:0}%", false, 64f);
        RectTransform r2 = Row(controls);
        Text(r2, "Flux", 13f, 34f);
        flux = CountedDrop(r2, fluxes, 170f, "No furnace flux");
        if (QuickBench)
        {
            Btn(r2, "Add flux", AddFlux, 90f);
            Btn(r2, "Tap the slag", () => Send(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f }), 110f);
            Btn(r2, "Pull the bloom", () => Send(new CraftRequest(CraftRequestKind.ExtractBloom)), 120f, UITheme.ButtonStyle.Primary);
        }
        RectTransform r3 = Row(controls);
        Text(r3, "Liquid metal is run into a mould in the Crucible area (source: from the furnace).", 13f);
        info = InfoRow(controls);
    }

    private bool AddFuel()
    {
        if (fuels.Count == 0 || fuel == null) return false;
        string id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)];
        string name = Rules.Fuel(id)?.Name ?? id;
        if (Have(id) == 0)
        {
            Said = $"You have no {name}: pick a fuel you have (any wood burns as firewood).";
            hold = Hold.None;
            return false;
        }
        string burnt = BurntAs(id); // any wood burnt as firewood: what of it went in
        return Send(new CraftRequest(CraftRequestKind.AddFuel) { id = id, count = 2, flag = true }, (ok, why) =>
        {
            if (ok) Said = burnt != null ? $"Into the furnace as firewood: {burnt}." : $"Two shovels of {name.ToLowerInvariant()} in.";
            else hold = Hold.None;
        });
    }

    private void AddFlux()
    {
        if (fluxes.Count == 0 || flux == null)
        {
            Said = "There is no furnace flux or cupel to add.";
            return;
        }
        string id = fluxes[Mathf.Clamp(flux.value, 0, fluxes.Count - 1)];
        CraftSupplyDefinition s = Rules.Supply(id);
        if (Have(id) == 0)
        {
            Said = $"You have no {ItemOf(id)}.";
            return;
        }
        Send(new CraftRequest(CraftRequestKind.Flux) { id = id, count = 1, i0 = 0 }, (ok, why) =>
        {
            if (!ok || s == null) return;
            Said = s.kind == CraftSupplyKind.Cupel ? $"A {s.Name.ToLowerInvariant()} set in the hearth: in a strong, hot draft the lead soaks into it and the silver stays."
                : $"A scoop of {s.Name.ToLowerInvariant()} in: {FluxWords(s)}.";
        });
    }

    /// <summary>What a furnace flux does there: limestone's lime takes the silica into the slag; another is what an ore needs to smelt at all.</summary>
    private string FluxWords(CraftSupplyDefinition s)
    {
        foreach (CraftOreDefinition o in Rules.Ores)
            if (o != null && o.needsFlux == s.id) return $"{o.Name.ToLowerInvariant()} gives up its metal only with it";
        return s.id == "limestone" ? "its lime takes the rock's silica into the slag, so the iron stays in the bloom" : "it works in the furnace's slag";
    }

    private static float Seconds(Hold h) => h == Hold.Fuel ? 0.8f : h == Hold.Flux ? 1f : 1.2f;
    private static Rect Place(Hold h) => h == Hold.Fuel ? Heap : h == Hold.Flux ? Mouth : TapHole;

    public override void Tick(float dt)
    {
        pump.Tick(dt);
        float late = BellowsClock.Flush(Time.unscaledTime, ref airPending); // the air of a quick pump, once the bellows have filled
        if (late > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = late, flag = true });
        LiveDraw = hold != Hold.None || carry != Carry.None;
        if (hold == Hold.None)
            return;
        if (!ui.PointerHeld || !Place(hold).Contains(ui.Hover)) { hold = Hold.None; return; }
        if ((holdTime += dt) < Seconds(hold))
            return;
        holdTime = 0f;
        switch (hold)
        {
            case Hold.Fuel:
                if (!AddFuel()) hold = Hold.None; // it keeps shovelling while held
                break;
            case Hold.Flux:
                AddFlux();
                hold = Hold.None;
                break;
            default:
                Send(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f }, (ok, why) => { if (ok) Said = "The slag runs out of the tap hole, glowing: the draft comes back."; });
                hold = Hold.None;
                break;
        }
    }

    public override void Leave()
    {
        hold = Hold.None;
        carry = Carry.None;
        carried = null;
        pumped = false;
        airPending = 0f; // a quick pump's air not yet sent: the bellows are let go
        LiveDraw = false;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftFurnaceState f = Ws.Furnace;
        bool dark = ui.Dark;
        c.Rect(Shaft.xMin - 30, Shaft.yMin, Shaft.xMax + 30, Shaft.yMax, new Color(0.45f, 0.3f, 0.22f));
        c.Rect(Shaft.xMin, Shaft.yMin + 10, Shaft.xMax, Shaft.yMax - 10, new Color(0.08f, 0.07f, 0.07f));
        Color glow = CraftArt.Heat(f.fire.temperature, dark, out float k);
        c.Rect(Shaft.xMin, Shaft.yMin + 40, Shaft.xMax, Shaft.yMin + 200, new Color(glow.r, glow.g, glow.b, k * 0.8f));
        DrawCharge(c, glow, k, dark);
        // The bottom: the slag over the metal, the bloom growing, a cupel with its bead.
        float slag = Mathf.Clamp01((float)(f.slag / Math.Max(1, Rules.Settings.slagCapacity)));
        c.Rect(Shaft.xMin, Shaft.yMin + 22, Shaft.xMax, Shaft.yMin + 22 + slag * 44, new Color(0.25f, 0.18f, 0.12f, 0.9f));
        double metal = f.PoolMass;
        bool liquid = CraftSmelting.PoolLiquid(Rules, f);
        Color molten = Color.Lerp(new Color(1f, 0.6f, 0.2f), Color.white, 0.2f), frozen = new Color(0.45f, 0.43f, 0.42f);
        if (metal >= CraftSmelting.PourMinimum)
            c.Rect(Shaft.xMin + 18, Shaft.yMin + 11, Shaft.xMax - 18, Shaft.yMin + 21, liquid ? molten : frozen);
        if (f.cupel > 0)
        {
            c.Rect(BeadAt.x - 22, BeadAt.y - 9, BeadAt.x + 22, BeadAt.y - 3, new Color(0.82f, 0.8f, 0.74f));
            c.Rect(BeadAt.x - 18, BeadAt.y - 3, BeadAt.x + 18, BeadAt.y - 1, new Color(0.62f, 0.6f, 0.55f));
        }
        if (metal >= CraftSmelting.BeadMinimum && metal < CraftSmelting.PourMinimum && carry != Carry.Bead)
            c.Disc(BeadAt.x, BeadAt.y + BeadRadius(metal) - 1f, BeadRadius(metal), liquid ? molten : new Color(0.85f, 0.85f, 0.88f));
        double bloom = f.BloomMass;
        if (bloom >= 1 && carry != Carry.Bloom)
            c.Disc(BloomAt.x, BloomAt.y, BloomRadius(bloom), Color.Lerp(new Color(0.35f, 0.33f, 0.32f), glow, k * (bloom >= CraftSmelting.BloomMinimum ? 1f : 0.5f)));
        // The tap hole (plugged with clay): crusted dark while the slag is too stiff to run, glowing once it can.
        bool runs = f.fire.temperature >= CraftSmelting.TapHeat && f.slag >= 10;
        c.Rect(Shaft.xMax, Shaft.yMin + 18, TapHole.xMin, Shaft.yMin + 30, new Color(0.2f, 0.12f, 0.08f));
        c.Disc(TapHole.center.x, TapHole.center.y, 12, runs ? new Color(0.95f, 0.5f, 0.18f) : f.fire.temperature >= CraftSmelting.TapHeat ? new Color(0.6f, 0.36f, 0.2f) : new Color(0.3f, 0.26f, 0.22f));
        if (!runs) c.Ring(TapHole.center.x, TapHole.center.y, 12, 2f, new Color(0.2f, 0.17f, 0.15f));
        CraftArt.Label(c, TapHole.xMin - 2f, TapHole.yMax + 4f, "TAP", CraftArt.LabelDim);
        // The charcoal heap, the bench, the bellows.
        for (int i = 0; i < 22; i++)
            c.Disc(Heap.xMin + 12 + (i * 29 % 136), Heap.yMin + 8 + (i * 17 % 40), 7 + i % 3, new Color(0.12f, 0.11f, 0.1f));
        CraftArt.Label(c, Heap.xMin, Heap.yMin - 14f, "FUEL", CraftArt.LabelDim);
        CraftArt.Label(c, Mouth.xMin + 4f, Mouth.yMax - 14f, "MOUTH", CraftArt.LabelDim);
        c.Rect(Bench.xMin, Bench.yMin, Bench.xMax, Bench.yMin + 6, Wood);
        CraftArt.Label(c, Bench.xMin, Bench.yMin - 14f, "BENCH", CraftArt.LabelDim);
        DrawBenchPiece(c, dark);
        DrawCarried(c, f, glow, k, dark);
        if (hold != Hold.None)
        {
            Rect r = Place(hold);
            // Over the place held - the tap hole's under it, where its name is not.
            Vector2 at = hold == Hold.Tap ? new Vector2(r.xMin, r.yMin - 9f) : OnSurface(r.xMin, r.yMax + 3, r.width, 6);
            Meter(c, at.x, at.y, r.width, 6, holdTime / Seconds(hold), Mark);
        }
        c.Polygon(new List<Vector2> { new Vector2(50, 105), new Vector2(170, 75), new Vector2(170, 135) }, new Color(0.45f, 0.3f, 0.18f));
        c.Rect(170, 100, Shaft.xMin - 30, 110, Iron);
        CraftArt.Label(c, 52, 140, "BELLOWS", CraftArt.LabelDim);
        // The bars: the bellows' air; the flame it makes with the vent and the fuel, against what that flame does to the
        // charge (it smelts reducing, roasts clear, cupels and burns a matte's sulfur as a blast); the slag (past its tick
        // it chokes the draft) and the draft itself (choked by the slag and by fine ore packing the charge).
        Gauge(c, 20, 40, 170, 8, "AIR", 46f, (float)f.fire.air, new Color(0.6f, 0.8f, 1f));
        DrawFlame(c, f.fire);
        Gauge(c, 556, 40, 156, 10, "SLAG", 46f, slag, new Color(0.7f, 0.45f, 0.25f), (0f, 0.6f, GoodBand), (0.6f, 1f, HarmBand));
        Gauge(c, 556, 22, 156, 10, "DRAFT", 46f, Draft, new Color(0.75f, 0.85f, 0.95f), (0f, 0.6f, HarmBand), (0.6f, 0.85f, FairBand), (0.85f, 1f, GoodBand));
    }

    /// <summary>How freely the air comes through the charge (1 free - 0 choked): the slag left untapped and fine ore packing the charge choke it.</summary>
    private float Draft => Mathf.Clamp01(1f - (float)CraftSmelting.Choke(Ws));

    /// <summary>
    /// The flame (<see cref="CraftFireState.Oxidizing"/>: the bellows' air through the vent, against the fuel) over what it
    /// does to the charge: SMELT (reducing: ore gives up its metal), ROAST (clear: sulfides roast), CUPEL (an oxidising
    /// blast: lead goes into a cupel, a matte's sulfur burns - and little metal comes).
    /// </summary>
    private static void DrawFlame(CraftPixels c, CraftFireState fire)
    {
        const float x = 20f, y = 18f, w = 185f, h = 8f, named = 46f;
        float roast = (float)CraftSmelting.RoastAir, cupel = (float)CraftSmelting.CupelAir, bar = w - named;
        Gauge(c, x, y, w, h, "FLAME", named, (float)fire.Oxidizing, new Color(1f, 0.85f, 0.55f),
            (0f, roast, GoodBand), (roast, cupel, FairBand), (cupel, 1f, HarmBand));
        // The names of what the flame does: under the bar, and the narrow roasting band's over it.
        CraftArt.Label(c, x + named + 2f, y - 12f, "SMELT", CraftArt.LabelDim);
        CraftArt.Label(c, x + named + bar * roast, y + h + 2f, "ROAST", CraftArt.LabelDim);
        CraftArt.Label(c, x + named + bar * (cupel + 1f) * 0.5f - CraftArt.LabelWidth("CUPEL") * 0.5f, y - 12f, "CUPEL", CraftArt.LabelDim);
    }

    private static float BloomRadius(double grams) => Mathf.Clamp(Mathf.Sqrt((float)grams) * 0.9f, 4f, 30f);
    private static float BeadRadius(double grams) => Mathf.Clamp(Mathf.Pow((float)grams, 1f / 3f) * 2.4f, 2.5f, 7f);

    /// <summary>The charge: each piece in the shaft drawn by what is left of it, in up to three rows; more than they hold are counted.</summary>
    private void DrawCharge(CraftPixels c, Color glow, float k, bool dark)
    {
        charge.Clear();
        int n = 0, more = 0;
        foreach (CraftPiece p in Ws.Pieces)
        {
            if (p.location != CraftLocation.Furnace) continue;
            if (n >= 15) { more++; continue; }
            var slot = new Rect(Shaft.xMin + 10 + n % 5 * 45, Shaft.yMax - 82 - n / 5 * 34, 42, 30);
            n++;
            if (p.kind == CraftPieceKind.Ore)
            {
                CraftOreDefinition o = Rules.Ore(p.ore.ore);
                float r = Mathf.Clamp(Mathf.Sqrt((float)p.ore.Mass) * 0.5f, 5f, 15f);
                c.Disc(slot.center.x, slot.center.y, r, Color.Lerp(o != null ? o.color : new Color(0.5f, 0.4f, 0.35f), glow, k * 0.6f));
            }
            else if (p.id != carried)
            {
                Color m = Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MaxTemperature, dark, out float g), g);
                c.Rect(slot.xMin + 2, slot.center.y - 5, slot.xMax - 2, slot.center.y + 5, m);
                c.Frame(slot.xMin + 2, slot.center.y - 5, slot.xMax - 2, slot.center.y + 5, new Color(0.9f, 0.85f, 0.6f, 0.6f));
                charge.Add((p.id, slot));
            }
        }
        if (more > 0)
            CraftArt.Label(c, Shaft.xMax - CraftArt.LabelWidth($"+{more}") - 6f, Shaft.yMax - 26f, $"+{more}", CraftArt.LabelInk);
    }

    /// <summary>The selected piece on the bench, waiting to be charged (ore, or metal to melt down, roast or cupel).</summary>
    private void DrawBenchPiece(CraftPixels c, bool dark)
    {
        CraftPiece sel = Selected;
        if (sel == null || sel.location != CraftLocation.Air || carry == Carry.Charge)
            return;
        if (sel.kind == CraftPieceKind.Ore)
            DrawLump(c, sel, Bench.center);
        else if (sel.kind == CraftPieceKind.Metal && sel.segments.Count > 0)
        {
            float mm = Fit(sel, 160, 40), len = (float)(sel.Length / mm);
            CraftArt.DrawTop(c, Rules, sel, new Rect(Bench.center.x - len * 0.5f, Bench.yMin + 10, len, 40), mm, null, false, dark);
        }
    }

    private void DrawLump(CraftPixels c, CraftPiece p, Vector2 at)
    {
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        Color col = o != null ? o.color : new Color(0.5f, 0.4f, 0.35f);
        int n = Mathf.Clamp(p.ore.fragments.Count, 1, 7);
        for (int i = 0; i < n; i++)
            c.Disc(at.x + (i % 4 - 1.5f) * 13f, at.y + (i / 4) * 11f - 4f, i == 0 ? 14f : 8f, Color.Lerp(col, o != null ? o.gangueColor : Color.grey, i % 2 * 0.4f));
    }

    private void DrawCarried(CraftPixels c, CraftFurnaceState f, Color glow, float k, bool dark)
    {
        switch (carry)
        {
            case Carry.Charge:
            {
                CraftPiece sel = Ws.Piece(carried);
                if (sel == null) return;
                if (sel.kind == CraftPieceKind.Ore) DrawLump(c, sel, dragAt);
                else c.Rect(dragAt.x - 30, dragAt.y - 6, dragAt.x + 30, dragAt.y + 6, CraftArt.MetalColor(Rules, sel));
                c.Frame(Mouth.xMin, Mouth.yMin, Mouth.xMax, Mouth.yMax, Mouth.Contains(dragAt) ? Good : Mark, 2f);
                break;
            }
            case Carry.Bloom:
                c.Disc(dragAt.x, dragAt.y, Mathf.Min(24f, BloomRadius(f.BloomMass)), Color.Lerp(new Color(0.35f, 0.33f, 0.32f), glow, k));
                break;
            case Carry.Bead:
                c.Disc(dragAt.x, dragAt.y, BeadRadius(f.PoolMass), new Color(0.9f, 0.9f, 0.92f));
                break;
            case Carry.Piece:
            {
                CraftPiece p = Ws.Piece(carried);
                if (p != null)
                    c.Rect(dragAt.x - 20, dragAt.y - 5, dragAt.x + 20, dragAt.y + 5, Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MaxTemperature, dark, out float g), g));
                break;
            }
        }
        if (carry != Carry.None && carry != Carry.Charge)
            c.Line(dragAt.x + 6, dragAt.y + 4, dragAt.x + 40, dragAt.y + 40, Iron, 3f); // the tongs
    }

    // ------------------------------------------------------------------ the hands
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (phase)
        {
            case CraftPointer.Cancel:
                if (carry != Carry.None) Said = "Put back.";
                hold = Hold.None;
                carry = Carry.None;
                carried = null;
                return;
            case CraftPointer.Down:
                Press(pos);
                return;
            case CraftPointer.Drag:
                if (carry != Carry.None) dragAt = pos;
                return;
            case CraftPointer.Up:
                hold = Hold.None;
                if (pumped) { pumped = false; return; }
                LetGo(pos);
                return;
        }
    }

    private void Press(Vector2 pos)
    {
        hold = Hold.None;
        carry = Carry.None;
        carried = null;
        pumped = false;
        CraftFurnaceState f = Ws.Furnace;
        if (BellowsArea.Contains(pos))
        {
            pumped = true; // the pump acts on the press; its drag and release do nothing
            float air = BellowsClock.Pump(Time.unscaledTime, (float)pump.Pump(Hands), ref airPending); // a pump too soon goes with the next
            if (air > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = air, flag = true });
            return;
        }
        if (Heap.Contains(pos))
        {
            if (fuels.Count > 0 && fuel != null && Have(fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)]) == 0) { AddFuel(); return; } // says what is missing
            hold = Hold.Fuel;
            holdTime = 0f;
            return;
        }
        if (TapHole.Contains(pos))
        {
            if (f.fire.temperature < CraftSmelting.TapHeat) { Said = "The tap hole is crusted shut: the slag is too stiff to run until the furnace is hotter."; return; }
            hold = Hold.Tap;
            holdTime = 0f;
            return;
        }
        if (Mouth.Contains(pos))
        {
            hold = Hold.Flux;
            holdTime = 0f;
            return;
        }
        CraftPiece sel = Selected;
        if (Bench.Contains(pos) && sel != null && sel.location == CraftLocation.Air && (sel.kind == CraftPieceKind.Ore || sel.kind == CraftPieceKind.Metal))
        {
            carry = Carry.Charge;
            carried = sel.id;
            dragAt = pos;
            return;
        }
        if (Vector2.Distance(pos, BloomAt) <= Mathf.Max(14f, BloomRadius(f.BloomMass) + 4f) && f.BloomMass >= 1)
        {
            if (f.BloomMass < CraftSmelting.BloomMinimum) { Said = "Only a little iron has gathered at the bottom: it comes out with the bloom once there is enough to grip."; return; }
            carry = Carry.Bloom;
            dragAt = pos;
            return;
        }
        double metal = f.PoolMass;
        if (Vector2.Distance(pos, BeadAt) <= 14f && metal >= CraftSmelting.BeadMinimum && metal < CraftSmelting.PourMinimum)
        {
            carry = Carry.Bead;
            dragAt = pos;
            return;
        }
        foreach ((string id, Rect at) in charge)
            if (at.Contains(pos))
            {
                carry = Carry.Piece;
                carried = id;
                dragAt = pos;
                return;
            }
    }

    private void LetGo(Vector2 pos)
    {
        Carry was = carry;
        string id = carried;
        carry = Carry.None;
        carried = null;
        switch (was)
        {
            case Carry.Charge:
            {
                if (!Mouth.Contains(pos) || id == null) return;
                CraftPiece p = Ws.Piece(id);
                bool ore = p != null && p.kind == CraftPieceKind.Ore;
                Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Furnace }, (ok, why) =>
                {
                    if (ok) Said = ore ? "Charged into the shaft, on a layer of charcoal." : "Into the furnace: it melts down into the bottom (a matte roasts in an oxidising blast; lead carrying silver goes on a cupel).";
                });
                break;
            }
            case Carry.Bloom:
                if (Shaft.Contains(pos)) return;
                Send(new CraftRequest(CraftRequestKind.ExtractBloom), (ok, why) =>
                {
                    if (!ok) return;
                    CraftPiece b = Made();
                    if (b == null) return;
                    Said = b.MaxTemperature > 1000 ? $"The bloom comes out, a glowing spongy lump of {CraftText.Mass(b.Mass(Rules))}: work it at the anvil at welding heat while it is soft, to close it up and drive the slag out."
                        : $"The bloom comes out cold and crumbly ({CraftText.Mass(b.Mass(Rules))}): bring it to welding heat in the forge before you work it.";
                });
                break;
            case Carry.Bead:
                if (Shaft.Contains(pos)) return;
                Send(new CraftRequest(CraftRequestKind.TakeMelt) { flag = true }, (ok, why) =>
                {
                    if (!ok) return;
                    CraftPiece b = Made();
                    if (b != null) Said = $"Lifted out with the tongs: a bead of {CraftText.Mass(b.Mass(Rules))}, hot from the hearth.";
                });
                break;
            case Carry.Piece:
                if (Shaft.Contains(pos) || id == null) return;
                Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok) Said = "Lifted out of the furnace with the tongs."; });
                break;
        }
    }

    /// <summary>Inside an answer: the piece the request made.</summary>
    private CraftPiece Made()
    {
        CraftPiece made = null;
        foreach (CraftEvent e in AnswerEvents)
            if (e.Kind == CraftEventKind.PieceCreated) made = Ws.Piece(e.Piece) ?? made;
        return made;
    }

    // ------------------------------------------------------------------ words
    public override void Refresh()
    {
        RefreshCounts(fuel, fuels);
        RefreshCounts(flux, fluxes);
        if (info == null) return;
        CraftFurnaceState f = Ws.Furnace;
        string how;
        switch (carry)
        {
            case Carry.Charge: how = "Drag it up into the mouth of the shaft (top) and let go to charge it."; break;
            case Carry.None:
                switch (hold)
                {
                    case Hold.Fuel: how = "Shovelling fuel in: keep holding for more."; break;
                    case Hold.Flux: how = "Scooping the flux in..."; break;
                    case Hold.Tap: how = "Breaking the clay plug of the tap hole..."; break;
                    default:
                        how = QuickBench ? "" : "Drag ore - or a casting to melt down, roast or cupel - from the bench (right) into the mouth (top). Hold on the heap for fuel, on the mouth for flux, on the tap hole to run the slag. FLAME: what the air does in the fire - smelt ore in the green, roast sulfides in the amber, cupel in the red. SLAG: past the tick it chokes the DRAFT.";
                        break;
                }
                break;
            default: how = "Drag it out of the shaft and let go to lift it out with the tongs."; break;
        }
        string state = "";
        if (f.BloomMass >= CraftSmelting.BloomMinimum) state += " The bloom is ready: drag it out of the bottom.";
        double metal = f.PoolMass;
        if (metal >= CraftSmelting.BeadMinimum && metal < CraftSmelting.PourMinimum) state += " A bead lies at the bottom: drag it out with the tongs.";
        else if (metal >= CraftSmelting.PourMinimum && !CraftSmelting.PoolLiquid(Rules, f)) state += " The metal at the bottom has frozen: the furnace is too cold to run it.";
        if (f.slag > Rules.Settings.slagCapacity * 0.6) state += f.fire.temperature >= CraftSmelting.TapHeat ? " The slag chokes the draft: tap it." : " The slag chokes the draft, and it is too stiff to run: heat the furnace, then tap it.";
        if (CraftSmelting.FinesInCharge(Ws) > CraftLook.FinesChoke) state += " Fine ore packs the charge and chokes the draft: sieve the fines off and charge lumps.";
        info.text = (Said + " " + how + state).Trim();
    }
}

/// <summary>
/// The crucible: melt metals together (proportions, not names, make the alloy), cover the melt against the air, stir
/// it even, skim the dross, then pour: press on the crucible and lift the pointer to tilt it - a steady, unbroken stream
/// at the right speed fills the mould soundly; too slow it freezes short, too fast it churns and traps air, a broken
/// stream leaves a cold shut; tipped back (or let go) once full, the pouring cup takes the last of the stream.
/// <para>Every step is done with the hands - choose the tool: the <b>tongs</b> (drag a piece from the bench into the
/// crucible, a piece that has not melted yet back out, or a melt that has frozen out as a cake), the <b>cover</b>
/// (sprinkle it over the whole melt), the <b>stirring rod</b> (steady circles in the melt), the <b>skimmer</b> (drag
/// slowly from the middle out over the rim) and the <b>pour</b>. The bars under the mould: how full it is (the tick:
/// where the metal there is runs out), the stream's speed (the green band: neither too slow nor too fast) and how steady
/// it runs. Poured from the furnace, the same pour runs the furnace's metal - only the pour is done there.
/// Right-click tips the crucible back or puts down what is in hand.</para>
/// </summary>
public sealed class CraftCrucibleMode : CraftSmeltArea
{
    private enum Tool { Tongs, Cover, Stir, Skim, Pour }
    private enum Hand { None, Tongs, LiftOut, KnockOut, Sprinkle, Stir, Skim }

    private static readonly string[] ToolNames = { "Tongs", "Cover", "Stirring rod", "Skimmer", "Pour" };
    // The pouring cup's hole: the stream has to land within it.
    private const float MouthRadius = 12f;
    // How far the crucible is carried while pouring: never over the mould itself.
    private const float ShiftMin = -60f, ShiftMax = 136f;
    private static readonly Rect Bench = new Rect(430, 250, 270, 120), Mould = new Rect(450, 24, 250, 120);
    /// <summary>Where the pour's bars start (x): right of the pouring cup, so the stream coming down into it never crosses their names.</summary>
    private const float PourBarsX = 500f;

    private TMP_Dropdown mold, cover, source;
    private TextMeshProUGUI info;
    private readonly List<string> molds = new List<string>(), covers = new List<string>();
    private readonly List<Button> toolButtons = new List<Button>();
    private Tool tool = Tool.Pour;
    private Hand hand;
    private int moldIndex, coverIndex, sourceIndex;
    private bool coverPicked;
    // The pour under way.
    private CraftPourControl pour;
    private bool tippingBack, pourFromFurnace;
    private string pourMold;
    private double pourNeed;
    private float tiltStartY, tilt, startX, shift, drift, reach, landX, pourClock, fillSeconds = 4f;
    private Vector2 mouth;
    private readonly List<Vector2> splashes = new List<Vector2>();
    // What missed or ran over since the last splash drawn (mould-fulls): a splash for each Splash of it, at any frame rate.
    private double splashed;
    private const double Splash = 0.004;
    private Rect crucible = new Rect(150, 40, 160, 160);
    // The hands' other work.
    private readonly CraftCoverage sprinkle = new CraftCoverage();
    private readonly CraftSwirl stir = new CraftSwirl();
    private string lifted;
    private Vector2 dragAt;
    private float skimLast, skimMoving, skimPath;

    public override string Title => ui != null && ui.Station != null && !ui.Station.Has(CraftTools.Crucible) ? "Casting" : "Crucible";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Crucible) || s.Has(CraftTools.Furnace);
    public override string Hint => "Tools: tongs to put metal in (or lift it out), cover, stir, skim; then pour - press on the crucible, carry it until the ring sits in the cup, lift to tip it into the green band, tip back when full. Right-click tips back." + ByHandHint("pour by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Pour;
    /// <summary>A pour under way: leaving would cut it short.</summary>
    public override string Busy => pour != null ? "pouring" : null;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.mold = SelectedMold;
        setup.fromFurnace = FromFurnace;
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        int m = molds.IndexOf(setup.mold);
        if (m >= 0) moldIndex = m;
        if (mold != null && m >= 0) mold.SetValueWithoutNotify(m);
        if (source != null)
            for (int i = 0; i < source.options.Count; i++)
                if (source.options[i].text.Contains("furnace") == setup.fromFurnace) { sourceIndex = i; source.SetValueWithoutNotify(i); break; }
        ShowTool();
    }

    public override void Build(RectTransform controls)
    {
        molds.Clear();
        var mn = new List<string>();
        foreach (CraftMold m in Rules.AllMolds()) { molds.Add(m.id); mn.Add(m.Name); } // the stock ones, then one for each design's part
        covers.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.FluxFor(CraftFluxUse.Cover)) covers.Add(s.id); // only what covers a melt
        RectTransform r0 = Row(controls, 30f, 4f);
        toolButtons.Clear();
        for (int i = 0; i < ToolNames.Length; i++)
        {
            Tool t = (Tool)i;
            toolButtons.Add(Btn(r0, ToolNames[i], () => PickTool(t), 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        RectTransform r1 = Row(controls);
        Text(r1, "Cover", 13f, 46f);
        cover = CountedDrop(r1, covers, 160f, "No cover flux"); // set to the first one the player has
        if (coverPicked && coverIndex < covers.Count) cover.SetValueWithoutNotify(coverIndex);
        else coverIndex = cover.value;
        cover.onValueChanged.AddListener(v => { coverIndex = v; coverPicked = true; });
        if (QuickBench)
        {
            Btn(r1, "Into the crucible", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Melt }), 140f, UITheme.ButtonStyle.Primary);
            Btn(r1, "Lift out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 80f);
            Btn(r1, "Cover it", () => { if (covers.Count > 0) Send(new CraftRequest(CraftRequestKind.Flux) { id = covers[Mathf.Clamp(coverIndex, 0, covers.Count - 1)], count = 1, i0 = 1, a = 1f }); }, 80f);
            Btn(r1, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirMelt) { a = 1f }), 56f);
            Btn(r1, "Skim", () => Send(new CraftRequest(CraftRequestKind.Skim) { a = 0.8f }), 56f);
        }
        RectTransform r2 = Row(controls);
        Text(r2, "Mould", 13f, 50f);
        mold = Drop(r2, mn, Mathf.Clamp(moldIndex, 0, Mathf.Max(0, mn.Count - 1)), 200f);
        mold.onValueChanged.AddListener(v => moldIndex = v);
        var sources = new List<string>();
        if (Station.Has(CraftTools.Crucible)) sources.Add("from the crucible");
        if (Station.Has(CraftTools.Furnace)) sources.Add("from the furnace");
        source = Drop(r2, sources, Mathf.Clamp(sourceIndex, 0, Mathf.Max(0, sources.Count - 1)), 170f);
        source.onValueChanged.AddListener(v => { sourceIndex = v; ShowTool(); });
        HandsOnButton(r2, "Pour by hand", 150f);
        info = InfoRow(controls);
        ShowTool();
    }

    private string SelectedMold => molds.Count > 0 ? molds[Mathf.Clamp(moldIndex, 0, molds.Count - 1)] : null;
    private bool FromFurnace => source != null && source.options.Count > 0 && source.options[Mathf.Clamp(sourceIndex, 0, source.options.Count - 1)].text.Contains("furnace");

    private void PickTool(Tool t)
    {
        if (pour != null)
            return; // the hands are on the crucible
        EndHand(true);
        tool = t;
        ShowTool();
    }

    /// <summary>The tools: from the furnace (or with no crucible here) only the pour - the rest work the crucible's own melt.</summary>
    private void ShowTool()
    {
        bool crucibleWork = !FromFurnace && Station != null && Station.Has(CraftTools.Crucible);
        if (!crucibleWork) tool = Tool.Pour;
        for (int i = 0; i < toolButtons.Count; i++)
        {
            if (toolButtons[i] == null) continue;
            toolButtons[i].gameObject.SetActive(crucibleWork || (Tool)i == Tool.Pour);
            UITheme.SetSelected(toolButtons[i], (Tool)i == tool);
        }
    }

    // ------------------------------------------------------------------ the melt
    /// <summary>The metal there is to pour now: the crucible's melt, or the furnace's.</summary>
    private double MeltMass => FromFurnace ? Ws.Furnace.PoolMass : Ws.Crucible.Mass;
    private double MeltTemperature => FromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature;
    private CraftAlloyProfile MeltProfile => FromFurnace ? CraftSmelting.PoolProfile(Rules, Ws.Furnace) : CraftSmelting.MeltProfile(Rules, Ws.Crucible);

    /// <summary>The metal a mould takes (g) of the metal there is.</summary>
    private double Need(CraftMold m) => CraftSmelting.MoldMass(Rules, m, MeltProfile.Density);

    /// <summary>
    /// Where the stream lands (x, px) from the lip of the crucible carried <paramref name="carried"/> px to the right, when it
    /// reaches as far as a stream of <paramref name="streamReach"/> (0-1) throws: the faster it runs, the further out it falls.
    /// </summary>
    private static float StreamLands(float carried, float streamReach) => 150f + carried + 160f + 18f + 95f * streamReach;

    /// <summary>The melt's surface height above the crucible's floor (px), for <paramref name="mass"/> of metal.</summary>
    private static float Level(double mass) => mass > 1 ? 6f + Mathf.Clamp01((float)(mass / 2500.0)) * 128f : 0f;

    /// <summary>The melt's outline in the upright crucible: narrow at the floor, wider up the sloping walls.</summary>
    private Rect MeltRect(double mass)
    {
        float level = Level(mass), spread = level / 160f * 20f;
        return new Rect(crucible.xMin + 30 - spread, crucible.yMin + 10, crucible.width - 60 + 2 * spread, level);
    }

    /// <summary>
    /// Where the rod and the skimmer work the melt (seen from the side): over the melt, at least the lower part of the
    /// crucible's hollow tall, so a shallow melt can be stirred in circles too - never out over the walls or above.
    /// </summary>
    private Rect WorkZone(double mass)
    {
        Rect m = MeltRect(mass);
        return new Rect(m.xMin, m.yMin, m.width, Mathf.Max(m.height, 70f));
    }

    private bool InMelt(Vector2 p) => pour == null && WorkZone(Ws.Crucible.Mass).Contains(p);

    public override void Tick(float dt)
    {
        LiveDraw = pour != null || hand != Hand.None;
        if (hand == Hand.Sprinkle && ui.PointerHeld)
        {
            Rect m = MeltRect(Ws.Crucible.Mass);
            Vector2 h = ui.Hover;
            if (h.x >= m.xMin && h.x <= m.xMax && crucible.Contains(h))
                sprinkle.Paint(Mathf.Clamp((int)((h.x - m.xMin) / Mathf.Max(1f, m.width) * 8), 0, 7), dt * 1.6f);
        }
        if (pour == null)
            return;
        // The hand on the crucible: up tips it, sideways carries it over the mould; let go (or right-click) and it tips
        // back - the metal already in the air still lands, as the stream dies away.
        if (!ui.PointerHeld) tippingBack = true;
        Vector2 at = ui.Hover;
        pourClock += dt;
        if (tippingBack) tilt = 0f;
        else
        {
            if (at.y >= 0) tilt = Mathf.Clamp01((at.y - tiltStartY) / 160f);
            if (at.x >= 0) shift = Mathf.Clamp(at.x - startX, ShiftMin, ShiftMax);
            // The hands drift slowly (a new waver every third of a second): tired or unpractised hands drift further.
            float wander = (float)Hands.Tremble((int)(pourClock * 3f) + 51) * 650f;
            drift = Mathf.Lerp(drift, wander, Mathf.Min(1f, dt * 3f));
            // How far out the stream reaches: as far as the tilt throws it, then as it slows when eased back. Let go, the
            // crucible tips back with its lip still over the cup: the dying stream falls where it fell.
            reach = Mathf.Max((float)pour.Rate, (float)CraftPourControl.AimOf(tilt));
        }
        landX = StreamLands(shift, reach) + drift;
        bool onTarget = Mathf.Abs(landX - mouth.x) <= MouthRadius;
        double before = pour.Spilled + pour.Overflow;
        pour.Tick(dt, tilt, Hands, fillSeconds, onTarget);
        splashed += pour.Spilled + pour.Overflow - before;
        for (; splashed >= Splash; splashed -= Splash)
            if (splashes.Count < 60)
                splashes.Add(new Vector2(landX + UnityEngine.Random.Range(-14f, 14f), mouth.y + UnityEngine.Random.Range(-30f, 30f)));
        // Done once the stream has stopped: tipped back (let go), the mould full and the crucible upright again, or nothing
        // left to give.
        if (pour.Rate < CraftPourControl.Flowing && (tippingBack || pour.Empty || pour.Full))
            Finish();
    }

    /// <summary>The pour ends: what ran into the mould is cast, what missed it or ran over is lost on the floor.</summary>
    private void Finish()
    {
        if (pour == null)
            return;
        CraftPourControl p = pour;
        pour = null;
        tilt = 0f;
        tippingBack = false;
        LiveDraw = false;
        if (!p.Started && p.Lost <= 1e-6)
            return; // it never ran
        string m = pourMold;
        bool furnace = pourFromFurnace;
        double need = pourNeed;
        int share = p.Poured >= 0.9995 ? 1000 : Mathf.Clamp(Mathf.RoundToInt((float)p.Poured * 1000f), 1, 999);
        int lost = Mathf.Clamp(Mathf.RoundToInt((float)p.Lost * 1000f), 0, 3000);
        var r = new CraftRequest(furnace ? CraftRequestKind.TakeMelt : CraftRequestKind.Pour)
        {
            id = m, a = (float)p.MeanRate, b = (float)p.Steadiness, i0 = p.Interruptions, c = (float)p.SpillShare, d = (float)p.OverflowShare, i1 = share, i2 = lost,
        };
        Send(r, (ok, why) => { if (ok) Said = Verdict(p, need, m, furnace); });
    }

    /// <summary>
    /// Inside the pour's answer: what came of it, in the workshop's own terms (<see cref="CraftSmelting.PourVerdict"/>: short
    /// and why, a cold shut, pores, what went in with the metal, flash, streaks, the cavities filled, the metal lost).
    /// </summary>
    private string Verdict(CraftPourControl p, double need, string moldId, bool fromFurnace)
    {
        CraftPiece cast = null;
        foreach (CraftEvent e in AnswerEvents)
            if (e.Kind == CraftEventKind.Poured || e.Kind == CraftEventKind.PieceCreated) cast = Ws.Piece(e.Piece) ?? cast;
        return CraftSmelting.PourVerdict(Rules, cast, p, need, Rules.Mold(moldId), fromFurnace);
    }

    public override void Leave()
    {
        Finish(); // a pour started is finished: what ran into the mould is cast
        EndHand(true);
    }

    /// <summary>The piece picked changed: a piece held in the tongs is put back; the melt's work (a pour, a cover) goes on.</summary>
    public override void SelectionChanging(string from, string to)
    {
        if (hand == Hand.Tongs || hand == Hand.LiftOut || hand == Hand.KnockOut)
            EndHand(false);
    }

    /// <summary>Ends what the hands hold besides the pour: a cover sprinkled is sent as far as it went (<paramref name="keep"/>); the rest is put down.</summary>
    private void EndHand(bool keep)
    {
        if (hand == Hand.Sprinkle && keep)
            SendCover();
        hand = Hand.None;
        lifted = null;
        stir.Clear();
        sprinkle.Reset(8);
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftCrucibleState cs = Ws.Crucible;
        bool furnace = FromFurnace, dark = ui.Dark;
        crucible = new Rect(150 + (pour != null ? shift : 0f), 40, 160, 160);
        float angle = tilt * 70f;
        var pivot = new Vector2(crucible.xMax, crucible.yMax);
        Vector2 Rot(float x, float y)
        {
            float a = -angle * Mathf.Deg2Rad, dx = x - pivot.x, dy = y - pivot.y;
            return new Vector2(pivot.x + dx * Mathf.Cos(a) - dy * Mathf.Sin(a), pivot.y + dx * Mathf.Sin(a) + dy * Mathf.Cos(a));
        }
        List<Vector2> Box(float x0, float y0, float x1, float y1) => new List<Vector2> { Rot(x0, y0), Rot(x1, y0), Rot(x1, y1), Rot(x0, y1) };
        double temperature = MeltTemperature;
        Color glow = CraftArt.Heat(temperature, dark, out float k);
        // The fire under it (the forge's coals, or the furnace's mouth) while it stands in it.
        if (pour == null)
        {
            Color coals = CraftArt.Heat(furnace ? Ws.Furnace.fire.temperature : Math.Max(Ws.Forge.temperature, Ws.Furnace.fire.temperature), dark, out float g);
            c.Rect(crucible.xMin - 20, 14, crucible.xMax + 20, 36, Color.Lerp(new Color(0.12f, 0.1f, 0.09f), coals, g));
        }
        c.Polygon(new List<Vector2> { Rot(crucible.xMin, crucible.yMax), Rot(crucible.xMin + 24, crucible.yMin), Rot(crucible.xMax - 24, crucible.yMin), Rot(crucible.xMax, crucible.yMax) },
            furnace ? new Color(0.36f, 0.3f, 0.27f) : new Color(0.42f, 0.36f, 0.32f));
        // The melt (falling as it is poured), what floats on it, what has not melted yet.
        double mass = MeltMass, shown = Math.Max(0, mass - (pour != null ? pour.Given * pourNeed : 0));
        CraftAlloyProfile prof = MeltProfile;
        bool solid = temperature < prof.Solidus, liquid = temperature >= prof.Liquidus;
        if (shown > 1)
        {
            Rect m = MeltRect(shown);
            Color col = solid ? new Color(0.45f, 0.43f, 0.42f) : Color.Lerp(new Color(0.4f, 0.38f, 0.36f), glow, liquid ? k : k * 0.6f);
            c.Polygon(Box(m.xMin, m.yMin, m.xMax, m.yMax), col);
            if (!furnace)
            {
                float top = m.yMax;
                if (cs.slag > 5) c.Polygon(Box(m.xMin + 4, top - 7, m.xMax - 4, top - 1), new Color(0.5f, 0.46f, 0.4f, 0.95f));
                if (cs.dross > 5) c.Polygon(Box(m.xMin + 8, top - 5, m.xMax - 8, top), new Color(0.3f, 0.28f, 0.25f, 0.9f));
                if (cs.cover > 0.3) c.Polygon(Box(m.xMin + 2, top, m.xMax - 2, top + 5), new Color(0.7f, 0.75f, 0.8f, 0.6f));
            }
        }
        else if (!furnace && cs.slag + cs.dross > 5)
        {
            // Poured empty: the crust of slag and dross left on its floor, to be scraped out.
            Rect m = MeltRect(400);
            c.Polygon(Box(m.xMin + 6, m.yMin, m.xMax - 6, m.yMin + 5), new Color(0.42f, 0.38f, 0.33f, 0.95f));
        }
        if (!furnace)
        {
            int slot = 0;
            foreach (CraftPiece p in Ws.Pieces)
            {
                if (p.location != CraftLocation.Melt || p.id == lifted || slot >= 6) continue;
                float y0 = crucible.yMin + 14 + slot * 16;
                slot++;
                Color col = p.kind == CraftPieceKind.Ore ? OreColour(p, dark) : Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MaxTemperature, dark, out float g), g);
                c.Polygon(Box(crucible.xMin + 46, y0, crucible.xMax - 46, y0 + 12), col);
            }
        }
        if (hand == Hand.LiftOut || hand == Hand.KnockOut)
        {
            CraftPiece p = hand == Hand.LiftOut ? Ws.Piece(lifted) : null;
            Color col = p == null ? new Color(0.45f, 0.43f, 0.42f) : p.kind == CraftPieceKind.Ore ? OreColour(p, dark) : CraftArt.MetalColor(Rules, p);
            c.Rect(dragAt.x - 30, dragAt.y - 12, dragAt.x + 30, dragAt.y + 12, col);
            c.Line(dragAt.x + 20, dragAt.y + 8, dragAt.x + 60, dragAt.y + 50, Iron, 3f);
        }
        // The bench, with the selected piece on it (the tongs carry it) - out of the way while pouring.
        if (pour == null)
        {
            c.Rect(Bench.xMin, Bench.yMin, Bench.xMax, Bench.yMin + 8, Wood);
            DrawBenchPiece(c, dark);
        }
        DrawTool(c);
        DrawMould(c, glow, pivot);
    }

    private Color OreColour(CraftPiece p, bool dark)
    {
        CraftOreDefinition o = Rules.Ore(p.ore.ore);
        Color col = o != null ? o.color : new Color(0.5f, 0.4f, 0.35f);
        Color heat = CraftArt.Heat(p.ore.temperature, dark, out float g);
        return Color.Lerp(col, heat, g * 0.7f);
    }

    private void DrawBenchPiece(CraftPixels c, bool dark)
    {
        CraftPiece sel = Selected;
        if (sel == null || sel.location != CraftLocation.Air)
            return;
        Vector2 at = hand == Hand.Tongs ? dragAt : new Vector2(Bench.center.x, Bench.yMin + 36);
        if (sel.kind == CraftPieceKind.Ore)
        {
            int n = Mathf.Clamp(sel.ore.fragments.Count, 1, 7);
            for (int i = 0; i < n; i++)
                c.Disc(at.x + (i % 4 - 1.5f) * 13f, at.y + (i / 4) * 11f - 4f, i == 0 ? 14f : 8f, OreColour(sel, dark));
        }
        else if ((sel.kind == CraftPieceKind.Metal || sel.kind == CraftPieceKind.Fasteners) && sel.segments.Count > 0)
        {
            float mm = Fit(sel, 240, 50), len = (float)(sel.Length / mm);
            CraftArt.DrawTop(c, Rules, sel, new Rect(at.x - len * 0.5f, at.y - 25f, len, 50), mm, null, false, dark);
        }
        else
            return;
        if (hand == Hand.Tongs)
            c.Line(at.x + 20, at.y + 8, at.x + 60, at.y + 50, Iron, 3f);
    }

    /// <summary>The tool in the hand over the crucible, and its bar (how much of the melt is covered, how even it is mixed).</summary>
    private void DrawTool(CraftPixels c)
    {
        Vector2 h = ui.Hover;
        if (pour == null && crucible.Contains(h))
            switch (tool)
            {
                case Tool.Stir: c.Line(h.x, h.y, h.x + 30, h.y + 90, new Color(0.5f, 0.45f, 0.4f), 4f); break;
                case Tool.Skim:
                    Vector2 s = hand == Hand.Skim ? dragAt : h;
                    c.Line(s.x - 20, s.y, s.x + 20, s.y, Iron, 5f);
                    c.Line(s.x + 20, s.y, s.x + 60, s.y + 70, Iron, 3f);
                    break;
                case Tool.Cover:
                    if (hand == Hand.Sprinkle) for (int i = 0; i < 10; i++) c.Disc(h.x + (i * 7 % 11 - 5) * 2f, h.y - i * 4f, 1.4f, new Color(0.95f, 0.95f, 0.9f));
                    break;
            }
        if (hand == Hand.Skim && !crucible.Contains(dragAt))
        {
            c.Line(dragAt.x - 20, dragAt.y, dragAt.x + 20, dragAt.y, Iron, 5f);
            c.Line(dragAt.x + 20, dragAt.y, dragAt.x + 60, dragAt.y + 70, Iron, 3f);
        }
        if (hand == Hand.Sprinkle)
            Gauge(c, crucible.xMin, crucible.yMax + 16, crucible.width, 6, "COVER", sprinkle.Mean(), new Color(0.9f, 0.92f, 0.95f), (0.85f, 1f, GoodBand));
        if (hand == Hand.Stir)
            Gauge(c, crucible.xMin, crucible.yMax + 16, crucible.width, 6, "MIXED", (float)Ws.Crucible.mixed, new Color(0.6f, 0.85f, 1f), (0.7f, 1f, GoodBand));
    }

    /// <summary>
    /// The mould in its sand box, seen from above: its cavity has the outline of what it casts (a fastener mould a row of
    /// small cavities that fill in turn), with the mouth (the clay pouring cup) at the left end; the metal runs in there
    /// and creeps along it. Splashes on the sand are what missed. Under it the bars of the pour: how full (the tick:
    /// where the metal there is runs out), the stream's speed (the green band is right), how steady it runs.
    /// </summary>
    private void DrawMould(CraftPixels c, Color glow, Vector2 lip)
    {
        c.Rect(Mould.xMin, Mould.yMin, Mould.xMax, Mould.yMax, new Color(0.5f, 0.42f, 0.3f));
        CraftMold shown = SelectedMold != null ? Rules.Mold(SelectedMold) : null;
        mouth = new Vector2(Mould.xMin + 24f, Mould.center.y);
        bool aimed = pour != null && pour.Rate > CraftPourControl.Flowing && Mathf.Abs(landX - mouth.x) <= MouthRadius;
        Color mark = aimed ? Good : Mark;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        c.Disc(mouth.x, mouth.y, 20f, new Color(0.8f, 0.66f, 0.48f));
        c.Ring(mouth.x, mouth.y, 20f, 2f, new Color(0.55f, 0.43f, 0.3f));
        c.Disc(mouth.x, mouth.y, MouthRadius, new Color(0.06f, 0.05f, 0.04f));
        if (tool == Tool.Pour)
        {
            // A pulsing ring and a guide line up from the cup (under the bench) show where to bring the stream.
            c.Ring(mouth.x, mouth.y, 24f + 3f * pulse, 2f, mark);
            for (float y = Mould.yMax + 50f; y < Bench.yMin - 10f; y += 14f)
                c.Line(mouth.x, y, mouth.x, y + 7f, new Color(mark.r, mark.g, mark.b, 0.45f), 1.5f);
        }
        float full = pour != null ? (float)pour.Poured : 0f;
        Color hot = Color.Lerp(glow, Color.white, 0.2f);
        if (shown != null && shown.fasteners)
            DrawFastenerMould(c, shown, full, hot);
        else if (shown != null)
            DrawShapedMould(c, shown, full, hot);
        foreach (Vector2 sp in splashes) c.Disc(sp.x, sp.y, 2.5f, new Color(glow.r * 0.6f + 0.2f, glow.g * 0.5f + 0.15f, glow.b * 0.4f + 0.1f));
        if (tool == Tool.Pour && shown != null)
        {
            double need = pour != null ? pourNeed : Need(shown);
            float have = need > 1e-6 ? (float)(MeltMass / need) : 0f;
            var bands = new List<(float, float, Color)> { (0.97f, 1f, GoodBand) };
            if (have < 1f) bands.Add((Mathf.Max(0f, have - 0.012f), have + 0.012f, Harm));
            // Right of the cup, clear of the stream that comes down into it from the left.
            Gauge(c, PourBarsX, 150, Mould.xMax - PourBarsX, 8, "FILL", 54f, full, full >= 1f ? Harm : new Color(1f, 0.8f, 0.4f), bands.ToArray());
            Gauge(c, PourBarsX, 164, Mould.xMax - PourBarsX, 8, "FLOW", 54f, pour != null ? (float)pour.Rate : 0f, new Color(1f, 0.75f, 0.35f),
                (0f, (float)CraftSmelting.SlowPour, ColdBand), ((float)CraftSmelting.SlowPour, (float)CraftSmelting.FastPour, GoodBand), ((float)CraftSmelting.FastPour, 1f, HarmBand));
            Gauge(c, PourBarsX, 178, Mould.xMax - PourBarsX, 8, "STEADY", 54f, pour != null ? (float)pour.Steadiness : 0f, new Color(0.5f, 0.85f, 0.6f));
        }
        if (pour == null || pour.Rate <= CraftPourControl.Flowing)
        {
            if (tool == Tool.Pour && (pour == null || !tippingBack))
            {
                // Before the stream runs: where one in the middle of the FLOW bar's green band would land from the lip as it
                // is carried now - carry the crucible right until the ring sits in the cup, then tip it into the green.
                float x = pour == null ? StreamLands(0f, (float)CraftSmelting.GoodPour) : StreamLands(shift, Mathf.Max(reach, (float)CraftSmelting.GoodPour)) + drift;
                bool inCup = Mathf.Abs(x - mouth.x) <= MouthRadius;
                c.Ring(x, mouth.y, 6f, 1.5f, inCup ? new Color(Good.r, Good.g, Good.b, 0.7f) : new Color(1f, 0.8f, 0.4f, 0.6f));
            }
            return;
        }
        // The stream arcs from the lip down to where it lands.
        Vector2 to = new Vector2(landX, mouth.y), mid = new Vector2(lip.x + (landX - lip.x) * 0.7f, lip.y - (lip.y - mouth.y) * 0.25f);
        float wdt = 2f + 8f * (float)pour.Rate;
        c.Line(lip.x, lip.y, mid.x, mid.y, glow, wdt);
        c.Line(mid.x, mid.y, to.x, to.y, glow, wdt);
        bool on = Mathf.Abs(landX - mouth.x) <= MouthRadius;
        c.Ring(landX, mouth.y, 6f, 1.5f, on ? Good : Harm);
    }

    private void DrawShapedMould(CraftPixels c, CraftMold m, float full, Color hot)
    {
        CraftShapeTarget shape = m.profile != null && m.profile.Count >= 2 ? new CraftShapeTarget { profile = m.profile } : null;
        float widest = m.width;
        if (shape != null) foreach (CraftProfilePoint q in m.profile) widest = Mathf.Max(widest, q.width);
        float x0 = mouth.x + 22f, room = Mould.xMax - 10f - x0;
        float k = Mathf.Min(room / Mathf.Max(1f, m.length), (Mould.height - 24f) / Mathf.Max(1f, widest)); // px per mm
        float len = m.length * k;
        c.Rect(mouth.x, mouth.y - 3f, x0, mouth.y + 3f, new Color(0.08f, 0.06f, 0.05f)); // the runner
        if (full > 0f) c.Rect(mouth.x, mouth.y - 2f, x0, mouth.y + 2f, hot);
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n, w = m.width;
            if (shape != null) shape.At(t, out w, out _);
            float hw = Mathf.Max(1.5f, w * k * 0.5f), xa = x0 + len * i / n, xb = x0 + len * (i + 1) / n + 0.6f;
            c.Rect(xa, mouth.y - hw, xb, mouth.y + hw, new Color(0.1f, 0.08f, 0.07f));
            if (full * n > i) c.Rect(xa, mouth.y - hw + 0.8f, xb, mouth.y + hw - 0.8f, hot);
        }
        if (m.eyeAt >= 0f && m.eyeDiameter > 0f)
            c.Disc(x0 + len * m.eyeAt, mouth.y, m.eyeDiameter * k * 0.5f, new Color(0.5f, 0.42f, 0.3f)); // the core that makes the eye
        if (pour != null && pour.Overflow > 0.002)
            c.Rect(x0 - 4f, mouth.y - Mathf.Min(50f, widest * k * 0.5f + 4f + (float)pour.Overflow * 60f), x0 + len + 4f, mouth.y - widest * k * 0.5f - 1f, new Color(hot.r, hot.g, hot.b, 0.5f));
    }

    /// <summary>A fastener mould: a row of small cavities off the runner, filling one after another.</summary>
    private void DrawFastenerMould(CraftPixels c, CraftMold m, float full, Color hot)
    {
        int count = Mathf.Max(1, m.count), cols = Mathf.Min(count, 8), rows = Mathf.CeilToInt(count / (float)cols);
        float x0 = mouth.x + 30f, w = (Mould.xMax - 10f - x0) / cols, h = (Mould.height - 20f) / rows;
        float r = Mathf.Min(w, h) * 0.32f;
        c.Rect(mouth.x, mouth.y - 3f, Mould.xMax - 12f, mouth.y + 3f, new Color(0.08f, 0.06f, 0.05f)); // the runner
        if (full > 0f) c.Rect(mouth.x, mouth.y - 2f, mouth.x + (Mould.xMax - 12f - mouth.x) * Mathf.Min(1f, full * 1.5f), mouth.y + 2f, hot);
        for (int i = 0; i < count; i++)
        {
            float cx = x0 + (i % cols + 0.5f) * w, cy = Mould.yMin + 10f + (i / cols + 0.5f) * h;
            c.Disc(cx, cy, r, new Color(0.1f, 0.08f, 0.07f));
            float fill = Mathf.Clamp01(full * count - i);
            if (fill > 0f) c.Disc(cx, cy, r * Mathf.Sqrt(fill), hot);
        }
    }

    // ------------------------------------------------------------------ the hands
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Cancel)
        {
            // Right-click tips the crucible back (what is in the air still lands) or puts down what is in hand.
            if (pour != null) tippingBack = true;
            else if (hand != Hand.None)
            {
                if (hand != Hand.Sprinkle) Said = "Put down.";
                EndHand(hand == Hand.Sprinkle);
            }
            return;
        }
        if (phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Scroll)
        {
            if (pour == null && hand == Hand.None && !FromFurnace && Station.Has(CraftTools.Crucible))
                PickTool((Tool)(((int)tool + (scroll > 0 ? 1 : ToolNames.Length - 1)) % ToolNames.Length));
            return;
        }
        if (pour != null)
            return; // the pour follows the hand (Tick); letting go tips it back
        switch (tool)
        {
            case Tool.Pour: if (phase == CraftPointer.Down) StartPour(pos); return;
            case Tool.Tongs: Tongs(phase, pos); return;
            case Tool.Cover: Cover(phase, pos); return;
            case Tool.Stir: Stir(phase, pos); return;
            case Tool.Skim: Skim(phase, pos); return;
        }
    }

    private void StartPour(Vector2 pos)
    {
        if (!crucible.Contains(pos))
            return;
        CraftMold m = SelectedMold != null ? Rules.Mold(SelectedMold) : null;
        if (m == null) { Said = "Choose a mould first."; return; }
        bool furnace = FromFurnace;
        if (!Ws.CanPour(furnace, out string why)) { Said = why; return; } // told before the hands start, not after
        pourNeed = Need(m);
        pourMold = m.id;
        pourFromFurnace = furnace;
        pour = new CraftPourControl { Supply = pourNeed > 1e-6 ? MeltMass / pourNeed : 0 };
        // A big mould takes longer to fill than a row of rivets.
        fillSeconds = Mathf.Clamp(1.5f + (float)pourNeed / 400f, 2f, 8f);
        tiltStartY = pos.y;
        startX = pos.x;
        tilt = shift = drift = reach = pourClock = 0f;
        tippingBack = false;
        splashes.Clear();
        splashed = 0;
        Said = "";
    }

    private void Tongs(CraftPointer phase, Vector2 pos)
    {
        CraftPiece sel = Selected;
        switch (phase)
        {
            case CraftPointer.Down:
                EndHand(false);
                if (Bench.Contains(pos) && sel != null && sel.location == CraftLocation.Air && (sel.kind == CraftPieceKind.Metal || sel.kind == CraftPieceKind.Fasteners || sel.kind == CraftPieceKind.Ore))
                {
                    hand = Hand.Tongs;
                    dragAt = pos;
                }
                else if (crucible.Contains(pos))
                {
                    // A piece that has not melted yet: lift it out (the selected one if it is in, else the top one).
                    CraftPiece inside = sel != null && sel.location == CraftLocation.Melt ? sel : null;
                    if (inside == null)
                        foreach (CraftPiece x in Ws.Pieces)
                            if (x.location == CraftLocation.Melt) { inside = x; break; }
                    if (inside != null) { hand = Hand.LiftOut; lifted = inside.id; dragAt = pos; }
                    else if (Ws.Crucible.Mass >= 1 && !Ws.MeltIsLiquid) { hand = Hand.KnockOut; dragAt = pos; } // a frozen melt: knocked out as a cake
                    else if (Ws.Crucible.Mass >= 1) Said = "Molten metal is not lifted out: pour it - or let it freeze and knock the cake out.";
                }
                return;
            case CraftPointer.Drag:
                if (hand != Hand.None) dragAt = pos;
                return;
            case CraftPointer.Up:
            {
                Hand was = hand;
                string id = lifted;
                hand = Hand.None;
                lifted = null;
                if (was == Hand.Tongs && crucible.Contains(pos) && sel != null)
                {
                    bool ore = sel.kind == CraftPieceKind.Ore;
                    Send(new CraftRequest(CraftRequestKind.Move, sel.id) { location = CraftLocation.Melt }, (ok, why) =>
                    {
                        if (ok) Said = ore ? "Into the crucible: once hot, it gives its metal to the melt - lift out the spent rock, skim its slag." : "Into the crucible: it melts when the crucible is hot enough for it.";
                    });
                }
                else if (was == Hand.LiftOut && !crucible.Contains(pos) && id != null)
                    Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok) Said = "Lifted out with the tongs."; });
                else if (was == Hand.KnockOut && !crucible.Contains(pos))
                    Send(new CraftRequest(CraftRequestKind.Pour) { flag = true }, (ok, why) =>
                    {
                        if (!ok) return;
                        CraftPiece cake = null;
                        foreach (CraftEvent e in AnswerEvents)
                            if (e.Kind == CraftEventKind.PieceCreated) cake = Ws.Piece(e.Piece) ?? cake;
                        if (cake != null) Said = $"Knocked out of the crucible: a cake of {CraftLook.MetalName(Rules, cake.Profile(Rules), ui.Known)}, {CraftText.Mass(cake.Mass(Rules))}. Remelt it, or forge it.";
                    });
                return;
            }
        }
    }

    private void Cover(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            EndHand(false);
            if (!crucible.Contains(pos)) return;
            if (covers.Count == 0) { Said = "There is no flux here that covers a melt (borax does)."; return; }
            if (Ws.Crucible.Mass < 1) { Said = "There is no melt to cover yet: melt the metal first."; return; }
            hand = Hand.Sprinkle;
            sprinkle.Reset(8);
        }
        else if (phase == CraftPointer.Up && hand == Hand.Sprinkle)
            EndHand(true);
    }

    private void SendCover()
    {
        float cov = sprinkle.Mean();
        sprinkle.Reset(8);
        hand = Hand.None;
        if (cov <= 0.05f || covers.Count == 0)
            return;
        string id = covers[Mathf.Clamp(coverIndex, 0, covers.Count - 1)];
        if (Have(id) == 0) { Said = $"You have no {ItemOf(id)}."; return; }
        Send(new CraftRequest(CraftRequestKind.Flux) { id = id, count = 1, i0 = 1, a = cov }, (ok, why) =>
        {
            if (!ok) return;
            double now = Ws.Crucible.cover; // as the workshop has it, with what was on it before
            Said = now >= 0.85 ? "A whole cover over the melt: the air is kept off it."
                : $"The melt is {now * 100:0}% covered: the bare part takes up air and dross - sprinkle over the rest of it.";
        });
    }

    private void Stir(CraftPointer phase, Vector2 pos)
    {
        switch (phase)
        {
            case CraftPointer.Down:
                EndHand(false);
                if (!InMelt(pos)) return;
                if (!Ws.MeltIsLiquid) { Said = "The metal is solid: there is nothing to stir until it melts."; return; }
                hand = Hand.Stir;
                stir.Begin(WorkZone(Ws.Crucible.Mass).center); // round the middle of the melt
                stir.Add(pos);
                return;
            case CraftPointer.Drag:
                if (hand != Hand.Stir || !InMelt(pos)) return; // circles round the melt count, not outside it
                stir.Add(pos);
                if (stir.Turns >= 2f)
                {
                    float strength = 0.4f + 0.6f * stir.Steadiness;
                    stir.Spend(2f);
                    Send(new CraftRequest(CraftRequestKind.StirMelt) { a = strength }, (ok, why) =>
                    {
                        if (ok) Said = (strength > 0.8f ? "Wide, steady circles: it mixes evenly." : "An uneven stir: it mixes slowly.") + $" It is {Ws.Crucible.mixed * 100:0}% even now.";
                    });
                }
                return;
            case CraftPointer.Up:
                if (hand == Hand.Stir) EndHand(false);
                return;
        }
    }

    private void Skim(CraftPointer phase, Vector2 pos)
    {
        switch (phase)
        {
            case CraftPointer.Down:
            {
                EndHand(false);
                if (!crucible.Contains(pos)) return;
                CraftCrucibleState cs = Ws.Crucible;
                if (cs.dross + cs.slag < 0.5) { Said = "There is nothing on the melt to skim."; return; }
                if (cs.Mass > CraftSmelting.EmptyCrucible && !Ws.MeltIsLiquid) { Said = "The metal is solid: the dross is stuck in it until it melts."; return; }
                Rect m = WorkZone(cs.Mass);
                if (!m.Contains(pos) || Mathf.Abs(pos.x - m.center.x) > m.width * 0.25f) { Said = "Start in the middle of the melt, then drag slowly out over the rim."; return; }
                hand = Hand.Skim;
                dragAt = pos;
                skimLast = ui.PointerTime;
                skimMoving = skimPath = 0f;
                return;
            }
            case CraftPointer.Drag:
            {
                if (hand != Hand.Skim) return;
                float t = ui.PointerTime;
                skimPath += Vector2.Distance(pos, dragAt);
                skimMoving += Mathf.Clamp(t - skimLast, 0f, 0.1f); // the time it moved - a pause is not care
                skimLast = t;
                dragAt = pos;
                return;
            }
            case CraftPointer.Up:
            {
                if (hand != Hand.Skim) return;
                hand = Hand.None;
                if (crucible.Contains(pos) || skimPath < 50f)
                {
                    Said = "Drag it out over the rim to lift the dross off.";
                    return;
                }
                // A slow, careful draw takes little metal with the dross; a flick takes more.
                float speed = skimPath / Mathf.Max(0.05f, skimMoving);
                float care = 0.3f + 0.7f * Mathf.Clamp01((420f - speed) / 340f);
                CraftCrucibleState cs = Ws.Crucible;
                double metal = cs.Mass, scum = cs.dross + cs.slag;
                bool empty = metal <= CraftSmelting.EmptyCrucible;
                Send(new CraftRequest(CraftRequestKind.Skim) { a = care }, (ok, why) =>
                {
                    if (!ok) return;
                    CraftCrucibleState now = Ws.Crucible;
                    double off = Math.Max(0, scum - now.dross - now.slag);
                    Said = empty ? $"The crust is scraped out of the empty crucible: {CraftText.Mass(off)} of slag and dross. It is clean for the next melt."
                        : (care > 0.8f ? "A slow, careful skim" : care > 0.5f ? "A quick skim" : "A hasty flick")
                        + $": {CraftText.Mass(off)} of dross and slag off, with {CraftText.Mass(Math.Max(0, metal - now.Mass))} of metal.";
                });
                return;
            }
        }
    }

    // ------------------------------------------------------------------ words
    public override void Refresh()
    {
        RefreshCounts(cover, covers);
        if (info == null) return;
        string said = Said, how;
        switch (tool)
        {
            case Tool.Tongs: how = "<b>Tongs</b>: drag the piece from the bench into the crucible; drag one that has not melted out to lift it. A melt that has frozen is knocked out as a cake: drag it out."; break;
            case Tool.Cover: how = "<b>Cover</b>: hold and sweep over the melt to sprinkle the cover across it (the bar: how much of the melt is covered)."; break;
            case Tool.Stir: how = "<b>Stirring rod</b>: hold in the melt and draw wide, steady circles round its middle."; break;
            case Tool.Skim: how = "<b>Skimmer</b>: press in the middle of the melt and drag slowly out over the rim - a slow skim takes little metal with the dross."; break;
            default: how = PourWords(said.Length > 0); break;
        }
        // What the last thing done came to (a pour's verdict) first, while it is fresh; then what to do now.
        info.text = (said + " " + how).Trim();
    }

    /// <summary>
    /// The pour's words: the stream while it runs; else how to pour and what the mould and the melt will give.
    /// <paramref name="brief"/> (a verdict is showing): only what is wrong for the next pour, not how to pour.
    /// </summary>
    private string PourWords(bool brief)
    {
        CraftMold m = SelectedMold != null ? Rules.Mold(SelectedMold) : null;
        if (pour != null)
        {
            string stream = pour.Rate <= CraftPourControl.Flowing ? (pour.Empty ? "The metal has run out." : tippingBack ? "Tipped back." : "Tip it further.")
                : pour.Full ? "<color=#ff8a70>Full: tip it back now - it is running over!</color>"
                : Mathf.Abs(landX - mouth.x) > MouthRadius ? "<color=#ff8a70>Missing the cup: it splashes on the sand!</color>"
                : pour.Rate < CraftSmelting.SlowPour ? "Too slow: it will freeze short - tip it further."
                : pour.Rate > CraftSmelting.FastPour ? "Too fast: it churns and traps air - ease it back."
                : "Into the cup: keep it there, steady.";
            return stream + (pour.Supply < 1 && pour.Given > pour.Supply * 0.8 ? " The crucible is nearly empty." : "");
        }
        string how = brief ? "" : "<b>Pour</b>: press on the crucible and carry it right until the ring sits in the clay cup, then lift the pointer to tip it into the FLOW bar's green band and keep the stream in the cup (green); tip back (or let go) as soon as it is full.";
        if (m == null)
            return how;
        if (!Ws.CanPour(FromFurnace, out string why))
            return $"{how} Not now: {why}".Trim();
        double need = Need(m), have = MeltMass;
        string amount = have < need * 0.98 ? $" The {m.Name.ToLowerInvariant()} takes about {CraftText.Mass(need)}; there is {CraftText.Mass(have)}: it will come out short (the red tick on FILL)."
            : brief ? "" : $" The {m.Name.ToLowerInvariant()} takes about {CraftText.Mass(need)} of the {CraftText.Mass(have)} there is.";
        CraftAlloyProfile prof = MeltProfile;
        string heat = CraftSmelting.Fill(Rules, prof, MeltTemperature - prof.Liquidus) < CraftSmelting.FullFill ? " The melt is sluggish: poured now it freezes before it fills the mould - heat it more." : "";
        string gas = !FromFurnace && Ws.Crucible.gas > CraftSmelting.PoreGas ? " It holds gas: poured now it casts with pores - hold it just above melting, under a cover, until it settles." : "";
        return (how + amount + heat + gas).Trim();
    }
}
