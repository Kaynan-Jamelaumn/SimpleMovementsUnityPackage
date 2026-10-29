using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The jeweller's bench, in six places (the piece itself is started and put together at the Assembly area):
/// <list type="bullet">
/// <item><b>Size</b>: try the ring sizer's rings on the wearer's finger until one fits: the size goes to the next piece
/// started, and the band is formed to it.</item>
/// <item><b>Form</b>: bend the wire or strip round the mandrel - tap all the way round (click round the rim), evenly,
/// then tap the seam closed at the top. A band, a bezel, a prong head, a bail or a chain.</item>
/// <item><b>Seat</b>: cut the seat for the stone with the burr: hold it in the setting's middle, steady, and let go when
/// the seat (gold) just meets the stone's girdle (white): too small it will not go in, too wide it rattles.</item>
/// <item><b>Solder</b>: flux the joint (brush over it), then play the torch on it (hold): the metal glows; let go the
/// moment the solder flashes along the seam. Keep heating and it pits - and a stone already set feels it.</item>
/// <item><b>Set</b>: push the bezel (or each prong) over the stone - one side, then the opposite, then a quarter round -
/// firmly but not so hard a brittle stone chips.</item>
/// <item><b>Finish</b>: buff it on the wheel (hold, lightly), steam it clean, then inspect it under the loupe and finish it.</item>
/// </list>
/// </summary>
public sealed class CraftJewellerMode : CraftMode
{
    private enum Place { Size, Form, Seat, Solder, Set, Finish }
    private static readonly string[] PlaceNames = { "Size", "Form", "Seat", "Solder", "Set", "Finish" };
    private static readonly string[] PartNames = { "Band", "Bezel", "Prong head", "Bail", "Chain" };
    private static readonly CraftJewelPart[] Parts = { CraftJewelPart.Band, CraftJewelPart.Bezel, CraftJewelPart.Prongs, CraftJewelPart.Bail, CraftJewelPart.Chain };

    private Place place = Place.Form;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> formRows = new List<RectTransform>(), seatRows = new List<RectTransform>(), jointRows = new List<RectTransform>(), finishRows = new List<RectTransform>(),
        sizeRows = new List<RectTransform>();
    private TMP_Dropdown part, prongs, stone, joint, solder;
    private Slider size;
    private TextMeshProUGUI info;
    private readonly List<string> stones = new List<string>(), joints = new List<string>(), solders = new List<string>();
    private string stonesKey = "", jointsKey = "";
    private string said = "";

    private static readonly Vector2 Centre = new Vector2(250f, 200f);

    // Sizing: the finger's size is the wearer's own; each ring tried says too tight, too loose or right.
    private int tries;
    private float tried = -1f;
    private bool fits;

    // Forming: taps round the rim (eight sectors) and on the seam.
    private readonly int[] sectors = new int[8];
    private float seam = 1f;
    private int seamOver, taps;
    private float pressAt;
    private bool pressing;

    // Seating: the burr growing the seat.
    private bool burring;
    private float seatMm, burrStart, wander;
    private int wanderN;
    private Vector2 burrLast;

    // Soldering: flux cover, the heat.
    private readonly HashSet<int> fluxed = new HashSet<int>();
    private float heat;
    private bool torching;
    private string heatFor = "";

    // Setting: pushes round the stone.
    private float lastPush = -999f, pushPress, pushStart, evenSum, pressSum;
    private int pushes, pending;
    private bool pushing;

    // Finishing.
    private float buff, cleanHeld;
    private bool buffing, steaming;

    public override string Title => "Jeweller";
    public override CraftDiscipline Discipline => CraftDiscipline.Jewellery;
    public override bool Available(CraftStation s) => s.Has(CraftTools.JewelersBench);
    public override string Hint => "Size the finger, form the band and the setting round the mandrel, cut the seat to the stone, solder, set the stone, buff and inspect - each with the hands.";

    private CraftPiece MetalPiece => Selected != null && Selected.kind == CraftPieceKind.Metal ? Selected : null;
    private CraftPiece Assembly => Selected != null && Selected.kind == CraftPieceKind.Assembly ? Selected : null;
    private CraftPiece StoneChosen => stones.Count > 0 && stone != null ? Ws.Piece(stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)]) : null;
    private string JointId => joints.Count > 0 && joint != null ? joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)] : null;

    /// <summary>The wearer's finger (inner diameter, mm): their own, fixed.</summary>
    private float Finger => (float)CraftJewelry.FingerOf(ui.Player != null ? ui.Player.name : "wearer");

    public override void Build(RectTransform controls)
    {
        formRows.Clear(); seatRows.Clear(); jointRows.Clear(); finishRows.Clear(); sizeRows.Clear();
        stonesKey = jointsKey = "";
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            placeButtons.Add(Btn(r0, PlaceNames[i], () => { place = pl; ResetHands(); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        RectTransform z1 = Row(controls);
        RoutineButton(z1, CraftRoutine.SizeFinger, "Size by routine", () => Assembly?.id, 170f);
        sizeRows.Add(z1);
        RectTransform f1 = Row(controls);
        part = Drop(f1, PartNames, 0, 130f);
        double measured = Ws != null ? Ws.Variable("fitSize") : 0;
        size = Slide(f1, "Inside (mm)", 2f, 200f, measured > 5 && measured < 40 ? (float)measured : 18f, false, 80f);
        prongs = Drop(f1, new List<string> { "3 prongs", "4 prongs", "5 prongs", "6 prongs", "8 prongs" }, 1, 100f);
        Btn(f1, "Done", FormDone, 70f, UITheme.ButtonStyle.Primary);
        formRows.Add(f1);
        RectTransform s1 = Row(controls);
        Text(s1, "Stone", 13f, 40f);
        stone = Drop(s1, new List<string> { "-" }, 0, 230f);
        Btn(s1, "Size to the stone", () => { CraftPiece g = StoneChosen; if (g != null && g.gem.girdle > 0) size.value = (float)g.gem.girdle; }, 140f);
        seatRows.Add(s1);
        formRows.Add(s1);
        RectTransform j1 = Row(controls);
        Text(j1, "Joint", 13f, 40f);
        joint = Drop(j1, new List<string> { "-" }, 0, 230f);
        solders.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies) if (s.kind == CraftSupplyKind.Solder) solders.Add(s.id);
        solder = CountedDrop(j1, solders, 170f, "No solder");
        jointRows.Add(j1);
        RectTransform k1 = Row(controls);
        Text(k1, "Hold on the wheel to buff, on the steam jet to clean.", 12f, 330f, UITheme.Muted);
        Btn(k1, "Inspect & finish", InspectAndFinish, 150f, UITheme.ButtonStyle.Primary);
        finishRows.Add(k1);
        if (QuickBench)
        {
            RectTransform q = Row(controls);
            Btn(q, "Form", () => { if (MetalPiece != null) Send(new CraftRequest(CraftRequestKind.FormShank, MetalPiece.id) { i0 = (int)Parts[part.value], a = size.value, b = 0.95f, c = 0.02f, i1 = 4 }); }, 60f);
            Btn(q, "Seat", () => { CraftPiece g = StoneChosen; if (MetalPiece != null && g != null) Send(new CraftRequest(CraftRequestKind.SeatSetting, MetalPiece.id) { other = g.id, a = (float)g.gem.girdle + 0.02f, b = 0.95f, c = 0.9f }); }, 60f);
            Btn(q, "Solder", () => { if (Assembly != null && JointId != null) { AttachSolder(); Send(new CraftRequest(CraftRequestKind.JointWork, Assembly.id) { id = JointId, i0 = (int)CraftJointWork.Solder, count = 1, a = 1f, b = 0.9f }); } }, 70f);
            Btn(q, "Set", () => { if (Assembly != null && JointId != null) Send(new CraftRequest(CraftRequestKind.JointWork, Assembly.id) { id = JointId, i0 = (int)CraftJointWork.Push, count = 12, a = 0.6f, b = 0.95f }); }, 50f);
        }
        info = InfoRow(controls, 44f);
        Show();
    }

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p)) { place = p; ResetHands(); Show(); }
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in formRows) r.gameObject.SetActive(place == Place.Form);
        foreach (RectTransform r in seatRows) r.gameObject.SetActive(place == Place.Seat || place == Place.Form);
        foreach (RectTransform r in jointRows) r.gameObject.SetActive(place == Place.Solder || place == Place.Set);
        foreach (RectTransform r in finishRows) r.gameObject.SetActive(place == Place.Finish);
        foreach (RectTransform r in sizeRows) r.gameObject.SetActive(place == Place.Size);
    }

    private void ResetHands()
    {
        pressing = burring = torching = pushing = buffing = steaming = false;
        jointsKey = "";
    }

    public override void Leave()
    {
        FlushPushes();
        ResetHands();
    }

    public override void Tick(float dt)
    {
        if (torching && ui.PointerHeld) heat += dt * 0.28f / Mathf.Max(0.6f, Station.ToolQuality < 1f ? 1.2f : 1f);
        else heat = Mathf.Max(0f, heat - dt * 0.35f);
        if (burring && ui.PointerHeld)
        {
            seatMm += dt * 0.35f;
            Vector2 h = ui.Hover;
            wander += Vector2.Distance(h, burrLast);
            wanderN++;
            burrLast = h;
        }
        if (buffing && ui.PointerHeld)
        {
            float press = Mathf.Clamp01((Centre.y + 120f - ui.Hover.y) / 240f);
            buff = Mathf.Min(1f, buff + dt * 0.12f * (press > 0.65f ? 0.6f : 1f) * Mathf.Min(1.2f, Station.ToolQuality));
        }
        if (steaming && ui.PointerHeld) cleanHeld = Mathf.Min(1.5f, cleanHeld + dt);
    }

    // ------------------------------------------------------------------ drawing
    private Color MetalColour(CraftPiece p) => p != null ? CraftArt.MetalColor(Rules, p) : new Color(0.85f, 0.75f, 0.4f);

    public override void Draw(CraftPixels c)
    {
        c.Rect(20, 20, 700, 380, new Color(0.17f, 0.15f, 0.13f));
        switch (place)
        {
            case Place.Size: DrawSize(c); break;
            case Place.Form: DrawForm(c); break;
            case Place.Seat: DrawSeat(c); break;
            case Place.Solder: DrawSolder(c); break;
            case Place.Set: DrawSet(c); break;
            default: DrawFinish(c); break;
        }
    }

    private const float SizerY = 300f;
    private static float SizerX(int i) => 70f + i * 48f;
    private static float SizerMm(int i) => 14f + i * 0.75f;

    private void DrawSize(CraftPixels c)
    {
        // The ring sizer: a stick of rings, smallest first; the finger beneath.
        for (int i = 0; i < 13; i++)
            c.Ring(SizerX(i), SizerY, 8f + SizerMm(i) * 0.6f, 2f, Mathf.Abs(SizerMm(i) - tried) < 0.01f ? Mark : new Color(0.75f, 0.76f, 0.8f));
        c.Rect(300, 40, 360, 200, new Color(0.55f, 0.45f, 0.38f));
        c.Disc(330, 200, 30f, new Color(0.55f, 0.45f, 0.38f));
        if (tried > 0) c.Ring(330, 140, Finger * 1.6f, 4f, fits ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 0.45f, 0.3f));
    }

    private void DrawForm(CraftPixels c)
    {
        CraftPiece p = MetalPiece;
        // The mandrel from above, the metal bent round it as far as it has been tapped; the seam at the top.
        float r = Mathf.Clamp(size.value * 4f, 30f, 150f);
        c.Disc(Centre.x, Centre.y, r - 6f, new Color(0.35f, 0.35f, 0.37f));
        Color m = MetalColour(p);
        for (int s = 0; s < 8; s++)
        {
            float a0 = s * Mathf.PI / 4f, a1 = (s + 1) * Mathf.PI / 4f;
            float bent = Mathf.Clamp01(sectors[s] / 2f);
            float rr = r + (1f - bent) * 22f + (sectors[s] > 3 ? -3f : 0f);
            for (float a = a0; a < a1; a += 0.05f)
                c.Line(Centre.x + Mathf.Cos(a) * rr, Centre.y + Mathf.Sin(a) * rr, Centre.x + Mathf.Cos(a + 0.05f) * rr, Centre.y + Mathf.Sin(a + 0.05f) * rr, m, 5f);
        }
        // The seam's gap.
        float gap = Mathf.Max(0f, seam) * 18f;
        c.Rect(Centre.x - gap * 0.5f, Centre.y + r - 8f, Centre.x + gap * 0.5f, Centre.y + r + 8f, new Color(0.17f, 0.15f, 0.13f));
        if (seamOver > 0) c.Rect(Centre.x - 4f, Centre.y + r - 10f, Centre.x + 4f, Centre.y + r + 10f, m * 0.7f);
        if (pressing) Meter(c, 470, 360, 200, 8, Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.5f), Mark);
        Meter(c, 470, 346, 200, 6, Roundness(), new Color(0.6f, 0.85f, 1f));
    }

    private float Roundness()
    {
        float mean = 0f;
        foreach (int s in sectors) mean += s;
        mean /= sectors.Length;
        if (mean <= 0f) return 0f;
        float v = 0f, under = 0f;
        foreach (int s in sectors) { v += (s - mean) * (s - mean); if (s == 0) under++; }
        float cv = Mathf.Sqrt(v / sectors.Length) / mean;
        return Mathf.Clamp01((1f - cv * 0.8f) * (1f - under / sectors.Length) * Mathf.Pow(0.9f, seamOver));
    }

    private const float PxPerMm = 20f;

    private void DrawSeat(CraftPixels c)
    {
        CraftPiece p = MetalPiece, g = StoneChosen;
        Color m = MetalColour(p);
        float inner = p != null ? (float)p.Tag(CraftJewelry.SizeTag) : 6f;
        float girdle = g != null ? (float)g.gem.girdle : 6f;
        c.Ring(Centre.x, Centre.y, inner * PxPerMm * 0.5f + 10f, 10f, m);
        float seat = Mathf.Max((float)(p != null ? p.Tag(CraftJewelry.SeatTag) : 0), burring ? seatMm : 0f);
        if (seat > 0) c.Ring(Centre.x, Centre.y, seat * PxPerMm * 0.5f, 2f, Mark);
        c.Ring(Centre.x, Centre.y, girdle * PxPerMm * 0.5f, 1.5f, Color.white);
        c.Disc(Centre.x, Centre.y, 6f, burring ? new Color(0.45f, 0.9f, 0.45f, 0.6f) : new Color(1f, 1f, 1f, 0.3f));
        if (burring) c.Disc(ui.Hover.x, ui.Hover.y, 4f, Iron);
    }

    private CraftPiece SettingOf(CraftPiece asm, out CraftPiece gem)
    {
        gem = null;
        if (asm == null || JointId == null) return null;
        CraftJointSpec j = Rules.Design(asm.assembly.design)?.Joint(JointId);
        if (j == null) return null;
        CraftJewelry.SettingParts(Ws, asm, j, out CraftPiece setting, out gem);
        return setting;
    }

    private void DrawSolder(CraftPixels c)
    {
        CraftPiece a = Assembly;
        if (a == null) return;
        if (heatFor != a.id + JointId) { heatFor = a.id + JointId; fluxed.Clear(); heat = 0f; }
        // The two parts on the soldering block, the seam between them, fluxed where it was brushed.
        c.Rect(60, 60, 440, 120, new Color(0.45f, 0.42f, 0.38f));
        Color m = MetalColour(null);
        foreach (AssemblyPart part in a.assembly.parts) { CraftPiece q = Ws.Piece(part.piece); if (q != null && q.kind == CraftPieceKind.Metal) { m = MetalColour(q); break; } }
        // Heat colours: dull red, cherry, orange - the solder flashes bright at its flow.
        Color glow = heat < 0.5f ? m : Color.Lerp(m, heat < 0.85f ? new Color(0.7f, 0.15f, 0.08f) : heat < 1.12f ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.8f, 0.6f), Mathf.Clamp01((heat - 0.5f) * 2f));
        c.Rect(80, 120, 240, 200, glow);
        c.Rect(260, 120, 420, 200, glow);
        for (int k = 0; k < 10; k++)
            c.Rect(240, 120 + k * 8, 260, 128 + k * 8, fluxed.Contains(k) ? new Color(0.9f, 0.9f, 0.85f, 0.8f) : new Color(0.1f, 0.1f, 0.1f));
        if (heat >= 0.95f && heat < 1.3f) c.Rect(246, 120, 254, 200, new Color(1f, 1f, 0.85f)); // the solder runs
        if (heat > 1.12f) for (int k = 0; k < 6; k++) c.Disc(90 + k * 55, 160, 3f, new Color(0.2f, 0.15f, 0.12f));
        if (torching) c.Halo(ui.Hover.x, ui.Hover.y, 40f, new Color(0.5f, 0.6f, 1f), 0.6f);
        Meter(c, 470, 360, 200, 8, Mathf.Clamp01(fluxed.Count / 10f), new Color(0.9f, 0.9f, 0.85f));
    }

    private void DrawSet(CraftPixels c)
    {
        CraftPiece a = Assembly;
        CraftPiece setting = SettingOf(a, out CraftPiece gem);
        if (setting == null) return;
        Color m = MetalColour(setting);
        CraftGemDefinition d = gem != null ? Rules.Gem(gem.gem.gem) : null;
        Color sc = d != null ? d.color : new Color(0.8f, 0.8f, 0.85f);
        sc.a = 1f;
        JointState j = a.assembly.Joint(JointId);
        float held = j != null ? (float)Math.Min(1, j.security / 0.85) : 0f;
        c.Disc(Centre.x, Centre.y, 110f, sc * 0.8f);
        c.Disc(Centre.x - 30, Centre.y + 30, 30f, Color.Lerp(sc, Color.white, 0.4f));
        if (CraftJewelry.PartOf(setting) == CraftJewelPart.Prongs)
        {
            int n = Math.Max(3, (int)setting.Tag(CraftJewelry.ProngTag));
            for (int k = 0; k < n; k++)
            {
                float ang = k * Mathf.PI * 2f / n;
                float r = 120f - held * 14f;
                c.Disc(Centre.x + Mathf.Cos(ang) * r, Centre.y + Mathf.Sin(ang) * r, 9f, m);
            }
        }
        else c.Ring(Centre.x, Centre.y, 116f - held * 6f, 10f + held * 4f, m);
        if (pushing) Meter(c, 470, 360, 200, 8, Mathf.Clamp01((Time.unscaledTime - pushStart) / 0.6f), Mark);
        Meter(c, 470, 346, 200, 6, held, new Color(0.45f, 0.9f, 0.45f));
    }

    private void DrawFinish(CraftPixels c)
    {
        CraftPiece a = Assembly;
        // The buffing wheel, and the piece's shine as it is buffed; the steam jet.
        c.Disc(560, 220, 90f, new Color(0.85f, 0.8f, 0.7f));
        c.Ring(560, 220, 90f, 3f, new Color(0.6f, 0.55f, 0.5f));
        c.Rect(560, 40, 600, 100, Iron);
        if (steaming) c.Halo(580, 120, 40f, new Color(1f, 1f, 1f), 0.5f);
        if (a == null) return;
        Color m = Color.gray;
        foreach (AssemblyPart part in a.assembly.parts) { CraftPiece q = Ws.Piece(part.piece); if (q != null && q.kind == CraftPieceKind.Metal) { m = MetalColour(q); break; } }
        float shine = Mathf.Max(buff, (float)a.assembly.finish);
        c.Ring(Centre.x, Centre.y, 90f, 18f, Color.Lerp(m * 0.65f, Color.Lerp(m, Color.white, 0.35f), shine));
        Meter(c, 60, 360, 300, 8, shine, Mark);
        Meter(c, 60, 346, 300, 6, Mathf.Clamp01(cleanHeld / 1.5f), new Color(0.6f, 0.85f, 1f));
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (place)
        {
            case Place.Size: SizePointer(phase, pos); break;
            case Place.Form: FormPointer(phase, pos); break;
            case Place.Seat: SeatPointer(phase, pos); break;
            case Place.Solder: SolderPointer(phase, pos); break;
            case Place.Set: SetPointer(phase, pos); break;
            default: FinishPointer(phase, pos); break;
        }
    }

    private void SizePointer(CraftPointer phase, Vector2 pos)
    {
        if (phase != CraftPointer.Down) return;
        for (int i = 0; i < 13; i++)
        {
            if (Vector2.Distance(pos, new Vector2(SizerX(i), SizerY)) > 20f) continue;
            tries++;
            tried = SizerMm(i);
            float off = tried - Finger;
            fits = Mathf.Abs(off) < 0.4f;
            said = off < -0.4f ? "Too tight: it will not go over the knuckle." : off > 0.4f ? "Too loose: it spins on the finger." : "It fits snugly.";
            if (fits)
            {
                // Finding it in few tries is a careful measure; many tries, a muddled one.
                float accuracy = Mathf.Clamp01(1f - Mathf.Abs(off) / 0.8f) * Mathf.Clamp01(1.1f - tries * 0.05f);
                CraftPiece asm = Assembly;
                if (Send(new CraftRequest(CraftRequestKind.MeasureFit, asm?.id) { a = accuracy, b = tried }))
                    said += $" Size {tried:0.00} mm: form the band to it.";
                size.SetValueWithoutNotify(tried);
                tries = 0;
            }
            return;
        }
    }

    private void FormPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = MetalPiece;
        if (p == null) { if (phase == CraftPointer.Down) said = "Pick a length of wire or strip (left)."; return; }
        if (phase == CraftPointer.Down) { pressing = true; pressAt = Time.unscaledTime; return; }
        if (phase != CraftPointer.Up || !pressing) return;
        pressing = false;
        float force = Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.5f) * (float)Hands.Strength;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(taps + 231) * 40f, (float)Hands.Tremble(taps + 232) * 40f);
        float a = Mathf.Atan2(at.y - Centre.y, at.x - Centre.x);
        float deg = (a * Mathf.Rad2Deg + 360f) % 360f;
        taps++;
        Hands.Move(0.05);
        // The seam at the top: taps there close the gap; past closed they overlap the ends.
        if (Mathf.Abs(Mathf.DeltaAngle(deg, 90f)) < 18f && Roundness() > 0.4f)
        {
            if (seam > 0f) { seam -= 0.12f + 0.2f * force; said = seam <= 0.05f ? "The seam is closed." : "The ends come together."; }
            else { seamOver++; said = "Struck the closed seam: the ends ride over each other."; }
            return;
        }
        int s = Mathf.Clamp(Mathf.FloorToInt(deg / 45f), 0, 7);
        sectors[s] += force > 0.25f ? 1 : 0;
        said = force <= 0.25f ? "Too light a tap." : sectors[s] > 3 ? "Tapped there too often: it flattens." : "It bends round the mandrel.";
    }

    private void FormDone()
    {
        CraftPiece p = MetalPiece;
        if (p == null) return;
        if (taps == 0) { said = "Tap it round the mandrel first."; return; }
        CraftJewelPart jp = Parts[Mathf.Clamp(part.value, 0, Parts.Length - 1)];
        int[] prongCounts = { 3, 4, 5, 6, 8 };
        float gapMm = Mathf.Max(0f, seam) * 0.8f;
        if (Send(new CraftRequest(CraftRequestKind.FormShank, p.id) { i0 = (int)jp, a = size.value, b = Roundness(), c = gapMm, i1 = prongCounts[Mathf.Clamp(prongs.value, 0, 4)] }))
            said = Roundness() < 0.6f ? "Formed - out of round." : gapMm > 0.3f ? "Formed - the seam gapes." : "Formed true, the seam closed.";
        Array.Clear(sectors, 0, sectors.Length);
        seam = 1f;
        seamOver = taps = 0;
    }

    private void SeatPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = MetalPiece, g = StoneChosen;
        if (p == null || g == null) { if (phase == CraftPointer.Down) said = "Pick the bezel or prong head (left) and the stone (above)."; return; }
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) < 20f)
        {
            burring = true;
            burrStart = Time.unscaledTime;
            float had = (float)p.Tag(CraftJewelry.SeatTag);
            seatMm = had > 0 ? had : (float)g.gem.girdle * 0.8f;
            wander = 0f;
            wanderN = 0;
            burrLast = pos;
            return;
        }
        if (phase != CraftPointer.Up || !burring) return;
        burring = false;
        float level = Mathf.Clamp01(1f - wander / Mathf.Max(1, wanderN) / 3f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
        float clean = Mathf.Clamp01(1f - Mathf.Max(0f, Time.unscaledTime - burrStart - 6f) / 6f);
        float err = seatMm - (float)g.gem.girdle;
        if (Send(new CraftRequest(CraftRequestKind.SeatSetting, p.id) { other = g.id, a = seatMm, b = level, c = clean }))
            said = err < -0.15f ? "The seat is still too small for the stone." : err > 0.3f ? "Cut too wide: the stone will rattle in it." : "The seat is cut to the stone's girdle.";
    }

    private void AttachSolder()
    {
        CraftPiece a = Assembly;
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d == null) return;
        string id = solders.Count > 0 ? solders[Mathf.Clamp(solder.value, 0, solders.Count - 1)] : null;
        foreach (CraftComponentSpec c in d.components)
            if (c.supply && c.supplyKind == CraftSupplyKind.Solder && a.assembly.Supply(c.role) < c.supplyCount)
                Send(new CraftRequest(CraftRequestKind.Attach, a.id) { id = c.role, count = Mathf.Max(1, c.supplyCount - (int)a.assembly.Supply(c.role)), other = id });
    }

    private void SolderPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece a = Assembly;
        if (a == null || JointId == null) { if (phase == CraftPointer.Down) said = "Pick the piece being made (start it at the Assembly area) and a solder joint."; return; }
        CraftJointSpec j = Rules.Design(a.assembly.design)?.Joint(JointId);
        if (j == null || j.kind != CraftJointKind.Solder) { if (phase == CraftPointer.Down) said = "That joint is not soldered."; return; }
        // On the seam: a brush stroke of flux; on the metal, the torch.
        if (pos.x > 236 && pos.x < 264 && pos.y > 116 && pos.y < 204 && (phase == CraftPointer.Down || phase == CraftPointer.Drag) && !torching)
        {
            fluxed.Add(Mathf.Clamp(Mathf.FloorToInt((pos.y - 120f) / 8f), 0, 9));
            return;
        }
        if (phase == CraftPointer.Down && pos.y > 110 && pos.y < 210 && pos.x > 70 && pos.x < 430) { torching = true; return; }
        if (phase != CraftPointer.Up || !torching) return;
        torching = false;
        AttachSolder();
        float flux = fluxed.Count / 10f;
        if (Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = JointId, i0 = (int)CraftJointWork.Solder, count = 1, a = heat, b = flux }))
            said = heat < 0.9f ? "Not hot enough: the solder balled up and did not run." : heat > 1.12f ? "Overheated: the solder pitted, the metal is firescaled." : flux < 0.6f ? "It flowed, but only where it was fluxed." : "The solder flashed along the seam.";
    }

    private void SetPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece a = Assembly;
        CraftPiece setting = SettingOf(a, out CraftPiece gem);
        if (setting == null || gem == null) { if (phase == CraftPointer.Down) said = "Pick the piece (left), and a setting joint with its stone in place."; return; }
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) > 90f && Vector2.Distance(pos, Centre) < 140f) { pushing = true; pushStart = Time.unscaledTime; return; }
        if (phase != CraftPointer.Up || !pushing) return;
        pushing = false;
        float press = Mathf.Clamp01((Time.unscaledTime - pushStart) / 0.6f) * (float)Hands.Strength;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(pushes + 241) * 40f, (float)Hands.Tremble(pushes + 242) * 40f);
        float ang = (Mathf.Atan2(at.y - Centre.y, at.x - Centre.x) * Mathf.Rad2Deg + 360f) % 360f;
        // Prongs: a push between the prongs only bends the air.
        if (CraftJewelry.PartOf(setting) == CraftJewelPart.Prongs)
        {
            int n = Math.Max(3, (int)setting.Tag(CraftJewelry.ProngTag));
            float step = 360f / n, off = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / step) * step));
            if (off > 15f) { said = "That was between the prongs."; return; }
        }
        // Opposite sides in turn: each push should land across from the last, then a quarter round for the next pair.
        float ideal = lastPush < -900f ? ang : pushes % 2 == 1 ? lastPush + 180f : lastPush + 90f;
        float even = lastPush < -900f ? 1f : Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(ang, ideal)) / 90f);
        lastPush = ang;
        pushes++;
        pending++;
        evenSum += even;
        pressSum += press;
        Hands.Move(0.08);
        said = press > 0.85f ? "Pushed hard - careful of the stone." : even < 0.5f ? "Push the opposite side next, or the stone tilts." : "Pushed over.";
        if (pending >= 4) FlushPushes();
    }

    private void FlushPushes()
    {
        CraftPiece a = Assembly;
        if (a == null || pending == 0 || JointId == null) { pending = 0; evenSum = pressSum = 0f; return; }
        Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = JointId, i0 = (int)CraftJointWork.Push, count = pending, a = pressSum / pending, b = evenSum / pending });
        pending = 0;
        evenSum = pressSum = 0f;
    }

    private void FinishPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            if (Vector2.Distance(pos, new Vector2(560, 220)) < 95f) { buffing = true; return; }
            if (pos.x > 540 && pos.x < 620 && pos.y > 30 && pos.y < 140) { steaming = true; return; }
            return;
        }
        if (phase == CraftPointer.Up) buffing = steaming = false;
    }

    private void InspectAndFinish()
    {
        CraftPiece a = Assembly;
        if (a == null) { said = "Pick the piece (left)."; return; }
        if (buff <= 0.05f && a.assembly.finish <= 0) { said = "Buff it on the wheel first."; return; }
        if (Send(new CraftRequest(CraftRequestKind.FinishJewel, a.id) { a = buff, b = Mathf.Clamp01(cleanHeld / 1.5f) }) && Send(new CraftRequest(CraftRequestKind.Finish, a.id)))
            said = "Inspected and finished.";
        buff = cleanHeld = 0f;
    }

    public override void Refresh()
    {
        RefreshCounts(solder, solders);
        // Stones that can be set (cut and polished) and the joints of the piece picked.
        var gs = new List<string>();
        foreach (CraftPiece q in Ws.Pieces) if (q.kind == CraftPieceKind.Gem && q.location == CraftLocation.Air && q.gem.stage >= CraftGemStage.Faceted) gs.Add(q.id);
        string key = string.Join(",", gs);
        if (key != stonesKey && stone != null)
        {
            stonesKey = key;
            stones.Clear();
            stones.AddRange(gs);
            var names = new List<string>();
            foreach (string id in gs) { CraftPiece q = Ws.Piece(id); names.Add($"{CraftLook.Name(Rules, q, ui.Known)} {q.gem.carats:0.00} ct"); }
            stone.ClearOptions();
            stone.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        }
        CraftPiece a = Assembly;
        string jk = a != null ? a.id + place : "";
        if (jk != jointsKey && joint != null)
        {
            jointsKey = jk;
            joints.Clear();
            var names = new List<string>();
            CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
            if (d != null)
                foreach (CraftJointSpec j in d.joints)
                    if (place == Place.Solder ? j.kind == CraftJointKind.Solder : j.kind == CraftJointKind.BezelSet || j.kind == CraftJointKind.ProngSet) { joints.Add(j.id); names.Add(j.Name); }
            joint.ClearOptions();
            joint.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        }
        if (info == null) return;
        string how;
        switch (place)
        {
            case Place.Size: how = "<b>Size</b>: click the rings of the sizer to try them on the finger until one fits (the fewer tries, the truer the size)."; break;
            case Place.Form:
                how = MetalPiece == null ? "<b>Form</b>: pick a length of wire or bezel strip (left)." : $"<b>Form</b> a {PartNames[Mathf.Clamp(part.value, 0, 4)].ToLowerInvariant()} of {size.value:0.0} mm inside: hold and release to tap round the rim (each part of it once or twice), then tap the seam at the top closed. Done when it is round (blue gauge).";
                break;
            case Place.Seat: how = "<b>Seat</b>: pick the setting (left) and the stone (above); hold the burr in the setting's middle, still, and let go when the gold seat just meets the white girdle."; break;
            case Place.Solder: how = a == null ? "<b>Solder</b>: pick the piece being made (start it at the Assembly area)." : "<b>Solder</b>: brush flux along the seam (drag over it), then hold the torch on the metal and let go the moment the solder flashes bright along the seam."; break;
            case Place.Set: how = a == null ? "<b>Set</b>: pick the piece (left)." : "<b>Set</b>: hold and release on the bezel (or a prong) to push it over the stone - then the opposite side, then a quarter round. Firm, not hard."; break;
            default: how = a == null ? "<b>Finish</b>: pick the piece (left)." : "<b>Finish</b>: hold the piece on the buffing wheel (lightly), hold it in the steam to clean it, then Inspect & finish."; break;
        }
        info.text = (how + " " + said).Trim();
    }
}
