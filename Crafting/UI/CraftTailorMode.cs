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
/// <item><b>Needle</b>: thread the needle - bring the thread's end through the eye; the needle trembles in the other
/// hand (more as you tire), and an end that catches on the eye frays (snip it).</item>
/// <item><b>Cut</b>: lay the pattern on the cloth along its grain (the arrows: the warp runs up the bolt), click to
/// pin it, then cut round it with the shears - snip by snip (press and release), each snip from the last.</item>
/// <item><b>Sew</b>: pick the item being made and a seam; stitch along the seam line - each click a stitch, held as
/// long as the thread is pulled (tension). Even stitches, on the line, pulled firm but not puckered.</item>
/// </list>
/// </summary>
public sealed class CraftTailorMode : CraftMode
{
    private enum Place { Spin, Needle, Cut, Sew }
    private static readonly string[] PlaceNames = { "Spin", "Thread the needle", "Cut cloth", "Sew" };

    private Place place = Place.Spin;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> spinRows = new List<RectTransform>(), needleRows = new List<RectTransform>(), cutRows = new List<RectTransform>(), sewRows = new List<RectTransform>();
    private TMP_Dropdown fibre, thread, pattern, seam;
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

    // Sewing.
    private float seamY = 200f, lastStitchX = -1f, pressAt;
    private bool pulling;
    private readonly List<Vector3> stitches = new List<Vector3>(); // x, y, tension
    private float spacingSum, alignSum, tensionSum, tensionSq;
    private int pending;

    public override string Title => "Tailor";
    public override bool Available(CraftStation s) => s.Has(CraftTools.SpinningWheel) || s.Has(CraftTools.TailorTable);
    public override string Hint => "Spin fibre into thread, thread the needle, cut cloth along its grain with the shears, sew the seams - each with the hands.";

    public override void Build(RectTransform controls)
    {
        spinRows.Clear(); needleRows.Clear(); cutRows.Clear(); sewRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        // Only the work this station has the tools for: the wheel spins; the table threads, cuts and sews.
        if (!Here(place)) place = Here(Place.Spin) ? Place.Spin : Place.Needle;
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
        spinRows.Add(s1);
        RectTransform n1 = Row(controls);
        Text(n1, "Thread", 13f, 50f);
        thread = CountedDrop(n1, threads, 200f, "No thread");
        thread.onValueChanged.AddListener(_ => { threaded = false; frays = 0; });
        Btn(n1, "Snip the end", () => { frays = 0; said = "A clean end again."; }, 120f);
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

    private bool Here(Place p) => Station != null && (p == Place.Spin ? Station.Has(CraftTools.SpinningWheel) : Station.Has(CraftTools.TailorTable));

    private string FibreId => fibres.Count > 0 ? fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)] : null;
    private string ThreadId => threads.Count > 0 ? threads[Mathf.Clamp(thread.value, 0, threads.Count - 1)] : null;

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in spinRows) r.gameObject.SetActive(place == Place.Spin);
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
        wheel = Mathf.Max(0f, wheel - dt * 0.35f);
        wheelAngle += wheel * dt * 540f;
        if (!drafting) twistShown = Mathf.MoveTowards(twistShown, 0f, dt * 0.5f);
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        switch (place)
        {
            case Place.Spin: DrawSpin(c); break;
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
        bool right = twistShown >= TwistLow && twistShown <= TwistHigh;
        Meter(c, 30, 350, 260, 8, twistShown, right ? new Color(0.45f, 0.85f, 0.45f) : twistShown < 0.05f ? Iron : new Color(0.95f, 0.45f, 0.3f));
        c.Rect(30 + 260 * TwistLow - 1, 346, 30 + 260 * TwistLow + 1, 362, Color.white);
        c.Rect(30 + 260 * TwistHigh - 1, 346, 30 + 260 * TwistHigh + 1, 362, Color.white);
        if (fibres.Count == 0 || Have(FibreId) == 0)
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
        Meter(c, 30, 380, 200, 6, threadLeft / 40f, Mark);
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
        // The ideal stitch, as a reference by the seam's start.
        c.Line(40, seamY - 40, 40 + IdealGap, seamY - 40, new Color(1f, 1f, 1f, 0.4f), 2f);
        foreach (Vector3 s in stitches)
        {
            Color col = Color.Lerp(new Color(0.95f, 0.9f, 0.7f), new Color(0.35f, 0.22f, 0.12f), s.z);
            c.Line(s.x - 8f, s.y - 6f, s.x + 8f, s.y + 6f, col, 1f + 2f * s.z);
            if (s.z > 0.85f) c.Line(s.x - 10f, s.y - 12f, s.x - 10f, s.y + 12f, new Color(0f, 0f, 0f, 0.25f), 1f); // puckered
        }
        if (pulling) Meter(c, ui.Hover.x - 30f, seamY + 30f, 60f, 6, Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.8f), Mark);
        Vector2 h = ui.Hover;
        if (h.x >= 0) { c.Line(h.x, h.y, h.x + 4f, h.y + 40f, new Color(0.8f, 0.8f, 0.85f), 1.5f); c.Disc(h.x + 4f, h.y + 40f, 2f, Iron); }
        Meter(c, 30, 380, 200, 6, threadLeft / 40f, Mark);
    }

    private const float IdealGap = 22f;

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (place)
        {
            case Place.Spin: SpinPointer(phase, pos); break;
            case Place.Needle: NeedlePointer(phase, pos); break;
            case Place.Cut: CutPointer(phase, pos, right, scroll); break;
            default: SewPointer(phase, pos); break;
        }
    }

    private void SpinPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down && pedal.Contains(pos))
        {
            // A treadle: a steady beat keeps the wheel at speed; stamping fast only jerks it.
            float now = Time.unscaledTime, gap = now - lastPedal;
            lastPedal = now;
            float steady = gap < 0.3f ? 0.3f : gap < 0.5f ? 0.7f : gap < 1.4f ? 1f : 0.7f;
            wheel = Mathf.Min(1f, wheel * 0.6f + 0.45f * steady * (0.6f + 0.4f * (float)Hands.Strength));
            return;
        }
        if (phase == CraftPointer.Down && handArea.Contains(pos))
        {
            string id = FibreId;
            if (id == null || Have(id) == 0) { said = $"You have no {ItemOf(id ?? "fibre")} to spin: bring some (each bobbin uses one)."; return; }
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
        bobbin = Mathf.Min(1f, bobbin + dx / 2400f);
        yarn.Add(Mathf.Lerp(5f, 1.5f, Mathf.Clamp01(noisy / 400f)));
        if (yarn.Count > 40) yarn.RemoveAt(0);
        if (draftN % 20 == 0) Hands.Move(0.15);
        if (bobbin >= 1f)
        {
            float mean = draftSum / draftN, sd = Mathf.Sqrt(Mathf.Max(0f, draftSq / draftN - mean * mean));
            float even = Mathf.Clamp01(1f - sd / Mathf.Max(1f, mean) * 1.4f);
            float tw = twistSum / draftN;
            string id = FibreId;
            if (id != null && Send(new CraftRequest(CraftRequestKind.Spin) { id = id, a = even, b = tw }))
            {
                string got = CraftTextiles.Spun(Rules, Rules.Supply(id), even, tw);
                said = $"A full bobbin: {Rules.Supply(got)?.Name.ToLowerInvariant() ?? "thread"}. " + (tw < 0.3f ? "Under-twisted: it pulls apart. Draft slower or treadle faster." : tw > 0.7f ? "Over-twisted: it kinks. Draft faster or treadle slower." : even < 0.6f ? "Lumpy: draft at an even pace." : "Even and well twisted.");
            }
            bobbin = 0f;
            draftSum = draftSq = draftN = twistSum = 0f;
            yarn.Clear();
            drafting = false;
        }
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
                threadLeft = 40;
                threadId = ThreadId;
                said = $"Threaded with {Rules.Supply(threadId)?.Name.ToLowerInvariant() ?? "thread"}: enough for about 40 stitches.";
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
        float spacing = lastStitchX >= 0f ? Mathf.Clamp01(Mathf.Abs((at.x - lastStitchX) - IdealGap) / IdealGap) : 0f;
        float align = Mathf.Clamp01(Mathf.Abs(at.y - seamY) / 14f);
        stitches.Add(new Vector3(at.x, at.y, tension));
        if (stitches.Count > 60) stitches.RemoveAt(0);
        lastStitchX = at.x;
        spacingSum += spacing; alignSum += align; tensionSum += tension; tensionSq += tension * tension;
        pending++;
        threadLeft--;
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
        Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joint, i0 = (int)CraftJointWork.Punch, count = pending, a = spacingSum / n, b = alignSum / n });
        Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joint, i0 = (int)CraftJointWork.Stitch, count = pending, a = mean, b = Mathf.Sqrt(Mathf.Max(0f, tensionSq / n - mean * mean)) });
        pending = 0;
        spacingSum = alignSum = tensionSum = tensionSq = 0f;
    }

    public override void Refresh()
    {
        RefreshCounts(fibre, fibres);
        RefreshCounts(thread, threads);
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
            case Place.Spin:
                how = fibres.Count == 0 || Have(FibreId) == 0 ? $"<b>Spin</b>: you need {ItemOf(FibreId ?? "fibre")} in your bag (one per bobbin)."
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
                    : !threaded ? "Thread the needle first." : $"<b>Sew</b>: click along the chalk line, left to right - one stitch per click, about as long as the white mark; hold as long as you pull the thread (firm, not puckered). {threadLeft} stitches of thread left.";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}
