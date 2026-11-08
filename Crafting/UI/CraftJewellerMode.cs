using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The jeweller's bench, in six places (the piece itself is started and put together at the Assembly area):
/// <list type="bullet">
/// <item><b>Size</b>: for a ring, try the ring sizer's rings on the wearer's finger until one fits: the size goes to the
/// ring being made (or waits for the next one started), and its band is formed to it. Only a ring is sized to a finger;
/// the fewer the tries, the truer the measure (the sizer's own steps do not count against it).</item>
/// <item><b>Form</b>: bend the wire or strip round the mandrel - tap all the way round (press and release on the metal
/// round the rim: the TAP gauge is the blow as it would land, green where it bends the metal), evenly, then tap the seam
/// closed at the top. A band (to the finger), a bezel or a prong head (to the stone's girdle), a bail or a chain. Only the
/// length the part needs is used: the rest goes back on the bench.</item>
/// <item><b>Seat</b>: cut the seat for the stone with the burr in a setting - loose, or already fitted to the piece (pick
/// the piece) - hold it in the middle, steady (LEVEL: the hand's drift by the second), and let go when the seat (gold) lies
/// in the green ring round the stone's girdle: too small it will not go in, too wide it never sits tight. Every seat starts
/// a little under its stone's girdle. A setting formed far off the stone's size is refused.</item>
/// <item><b>Solder</b>: flux the seam (brush over it), then play the torch on the metal (hold): it glows; let go once the
/// solder takes (the heat gauge's green, lower the better it is fluxed) - short of it nothing joins, past it the metal pits; a gaping seam or thin flux
/// never takes. A stone already in the piece feels the heat: solder before setting.</item>
/// <item><b>Set</b>: push the bezel (or each prong) over the stone - one side, then the opposite, then a quarter round -
/// firmly, but past the red mark a brittle stone chips. Each push counts as it is made. The stone is set once the SECURE
/// gauge reaches its green (<see cref="CraftJewelry.SetSecure"/>): the inspection and the finished piece judge it by the same
/// line; a seat or collar too wide for it never gets there.</item>
/// <item><b>Finish</b>: buff it on the wheel (hold: only the time on the wheel counts), steam it clean, then inspect it under
/// the loupe and finish it (a pendant's cord is added here too). A buff the wheel has not had the time for is not all
/// taken: the piece waits, unfinished, to be buffed a little longer.</item>
/// </list>
/// Right-click puts the tool down - the mallet, the burr, the torch, the pusher, the piece off the wheel - and never taps,
/// cuts, heats or pushes.
/// </summary>
public sealed class CraftJewellerMode : CraftMode
{
    private enum Place { Size, Form, Seat, Solder, Set, Finish }
    private static readonly string[] PlaceNames = { "Size", "Form", "Seat", "Solder", "Set", "Finish" };
    private static readonly string[] PartNames = { "Band", "Bezel", "Prong head", "Bail", "Chain" };
    private static readonly CraftJewelPart[] Parts = { CraftJewelPart.Band, CraftJewelPart.Bezel, CraftJewelPart.Prongs, CraftJewelPart.Bail, CraftJewelPart.Chain };
    private static readonly int[] ProngCounts = { 3, 4, 5, 6, 8 };
    /// <summary>Each part's sizes on the slider (mm inside): a ring's band to a circlet's, a bezel's, a chain's length.</summary>
    private static readonly (float min, float max)[] SizeRange = { (10f, 200f), (2f, 30f), (2f, 30f), (2f, 30f), (100f, 600f) };

    private Place place = Place.Form;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> formRows = new List<RectTransform>(), seatRows = new List<RectTransform>(), jointRows = new List<RectTransform>(), finishRows = new List<RectTransform>(),
        sizeRows = new List<RectTransform>();
    private TMP_Dropdown part, prongs, stone, joint, solder;
    private Slider size;
    private Button sizeTo, cordButton;
    private TextMeshProUGUI info;
    private readonly List<string> stones = new List<string>(), joints = new List<string>(), solders = new List<string>();
    private string stonesKey = "", jointsKey = "";
    private string said = "";

    private static readonly Vector2 Centre = new Vector2(250f, 200f);

    // Sizing: the finger's size is the wearer's own; each ring tried says too tight, too loose or right.
    private int tries;
    private float tried = -1f;
    private bool fits;

    // Forming: taps round the rim (eight sectors) and on the seam; a size for each part.
    private readonly int[] sectors = new int[8];
    private float seam = 1f;
    private int seamOver, taps;
    private float pressAt;
    private bool pressing;
    private string formFor;
    private readonly float[] partSize = { 18f, 6f, 6f, 6f, 150f };

    // Seating: the burr growing the seat (from a little under the girdle, at its own pace), and how far the hand drifted
    // while it cut (judged by the second, at any frame rate).
    private bool burring;
    private float seatMm, burrStart, wander;
    private Vector2 burrLast;
    private string burrSetting, burrStone;
    /// <summary>How far under the stone's girdle (mm) a seat starts: every stone's seat takes the burr as long to cut.</summary>
    private const float BurrStartUnder = 1.2f, BurrMmPerSecond = 0.35f;

    // Soldering: flux cover, the heat.
    private readonly HashSet<int> fluxed = new HashSet<int>();
    private float heat;
    private bool torching;
    private string heatFor = "";

    // Setting: pushes round the stone (each sent as it is made).
    private float lastPush = -999f, pushStart;
    private int pushCount;
    private bool pushing;
    private string pushFor = "";

    // Finishing: each piece's own buffing and steaming (kept until it is inspected).
    private readonly Dictionary<string, float> buffed = new Dictionary<string, float>(), steamed = new Dictionary<string, float>();
    private bool buffing, steaming;
    private string finishFor;
    private const float SteamFull = 1.5f;

    public override string Title => "Jeweller";
    public override CraftDiscipline Discipline => CraftDiscipline.Jewellery;
    public override bool Available(CraftStation s) => s.Has(CraftTools.JewelersBench);
    public override string Hint => "Size the finger, form the band and the setting round the mandrel, cut the seat to the stone, solder, set the stone, buff and inspect - each with the hands. Right-click puts down.";
    // the mallet's taps round the mandrel are blows; the burr, the torch and the pusher are the hand's other work
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, place == Place.Form);

    private CraftPiece MetalPiece => Selected != null && Selected.kind == CraftPieceKind.Metal ? Selected : null;
    private CraftPiece Assembly => Selected != null && Selected.kind == CraftPieceKind.Assembly ? Selected : null;
    private CraftPiece StoneChosen => stones.Count > 0 && stone != null ? Ws.Piece(stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)]) : null;
    private string JointId => joints.Count > 0 && joint != null ? joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)] : null;
    private CraftJewelPart PartChosen => Parts[Mathf.Clamp(part != null ? part.value : 0, 0, Parts.Length - 1)];
    private int ProngsChosen => ProngCounts[Mathf.Clamp(prongs != null ? prongs.value : 1, 0, ProngCounts.Length - 1)];
    /// <summary>The steadiest these hands can be: the window shows what the station will take (see <see cref="CraftRequestGuard.FloorOf"/>).</summary>
    private float Floor => (float)CraftRequestGuard.FloorOf(Hands.skill);

    /// <summary>The wearer's finger (inner diameter, mm): their own, fixed.</summary>
    private float Finger => (float)CraftJewelry.FingerOf(ui.Player != null ? ui.Player.name : "wearer");

    // ------------------------------------------------------------------ the controls
    public override void Build(RectTransform controls)
    {
        formRows.Clear(); seatRows.Clear(); jointRows.Clear(); finishRows.Clear(); sizeRows.Clear();
        stonesKey = jointsKey = "";
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            placeButtons.Add(Btn(r0, PlaceNames[i], () => GoPlace(pl), 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        RectTransform z1 = Row(controls);
        RoutineButton(z1, CraftRoutine.SizeFinger, "Size by routine", () => RingBeingMade()?.id, 170f);
        sizeRows.Add(z1);

        RectTransform f1 = Row(controls);
        part = Drop(f1, PartNames, 0, 120f);
        part.onValueChanged.AddListener(_ => { ShowSize(); Show(); });
        size = Slide(f1, "Inside", 0f, 1f, 0.5f, false, 46f, v => $"{SizeOf(v):0.0} mm");
        size.onValueChanged.AddListener(v => partSize[(int)PartIndex] = SizeOf(v));
        prongs = Drop(f1, new List<string> { "3 prongs", "4 prongs", "5 prongs", "6 prongs", "8 prongs" }, 1, 96f);
        Btn(f1, "Done", FormDone, 70f, UITheme.ButtonStyle.Primary);
        formRows.Add(f1);

        RectTransform s1 = Row(controls);
        Text(s1, "Stone", 13f, 40f);
        stone = Drop(s1, new List<string> { "-" }, 0, 250f);
        sizeTo = Btn(s1, "Size to the stone", SizeToChosen, 150f);
        stone.onValueChanged.AddListener(_ =>
        {
            // a setting is formed to the stone chosen
            CraftPiece g = StoneChosen;
            if (g != null && g.gem.girdle > 0) { partSize[1] = partSize[2] = (float)g.gem.girdle; ShowSize(); }
        });
        seatRows.Add(s1);
        formRows.Add(s1);
        double finger = MeasuredFinger();
        if (finger > 0) partSize[0] = (float)finger; // the band, to the finger measured

        RectTransform j1 = Row(controls);
        Text(j1, "Joint", 13f, 40f);
        joint = Drop(j1, new List<string> { "-" }, 0, 230f);
        joint.onValueChanged.AddListener(_ =>
        {
            // another joint: the tools are put down, its flux and its pushes are its own
            ResetHands();
            heatFor = pushFor = "";
            lastPush = -999f;
            pushCount = 0;
        });
        solders.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies) if (s.kind == CraftSupplyKind.Solder) solders.Add(s.id);
        solder = CountedDrop(j1, solders, 170f, "No solder");
        jointRows.Add(j1);

        RectTransform k1 = Row(controls);
        Text(k1, "Wheel: buff (hold). Jet: steam clean.", 12f, 250f, UITheme.Muted);
        cordButton = Btn(k1, "Add the cord", AddCord, 120f);
        Btn(k1, "Inspect & finish", InspectAndFinish, 150f, UITheme.ButtonStyle.Primary);
        finishRows.Add(k1);

        if (QuickBench)
        {
            // A test station's quick buttons (each request waits for the answer of the one it depends on).
            RectTransform q = Row(controls);
            Btn(q, "Form", () => { if (MetalPiece != null) Send(new CraftRequest(CraftRequestKind.FormShank, MetalPiece.id) { i0 = (int)PartChosen, a = partSize[(int)PartIndex], b = 0.95f, c = 0.02f, i1 = ProngsChosen }); }, 60f);
            Btn(q, "Seat", () =>
            {
                CraftPiece s = SettingToSeat(out _, out _), g = StoneChosen;
                if (s != null && g != null) Send(new CraftRequest(CraftRequestKind.SeatSetting, s.id) { other = g.id, a = (float)g.gem.girdle + 0.02f, b = 0.95f, c = 0.9f });
            }, 60f);
            Btn(q, "Solder", () =>
            {
                CraftPiece a = Assembly;
                string j = JointId;
                if (a != null && j != null) WithSolder(a, () => Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = j, i0 = (int)CraftJointWork.Solder, count = 1, a = 1f, b = 0.9f }));
            }, 70f);
            // enough firm, even pushes to set it (CraftJewelry.SetSecure) on a bezel or a claw head alike
            Btn(q, "Set", () => { if (Assembly != null && JointId != null) Send(new CraftRequest(CraftRequestKind.JointWork, Assembly.id) { id = JointId, i0 = (int)CraftJointWork.Push, count = 24, a = 0.6f, b = 0.95f }); }, 50f);
        }
        info = InfoRow(controls, 44f);
        ShowSize();
        Show();
    }

    public override void GoTo(string where)
    {
        if (Enum.TryParse(where, out Place p)) GoPlace(p);
    }

    /// <summary>Every place is at the jeweller's bench.</summary>
    public override bool HasPlace(string place) => Enum.TryParse(place, out Place _);

    private void GoPlace(Place p)
    {
        ResetHands();
        place = p;
        said = "";
        jointsKey = "";
        Show();
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in formRows) r.gameObject.SetActive(place == Place.Form);
        foreach (RectTransform r in seatRows) r.gameObject.SetActive(place == Place.Seat || place == Place.Form);
        foreach (RectTransform r in jointRows) r.gameObject.SetActive(place == Place.Solder || place == Place.Set);
        foreach (RectTransform r in finishRows) r.gameObject.SetActive(place == Place.Finish);
        foreach (RectTransform r in sizeRows) r.gameObject.SetActive(place == Place.Size);
        if (solder != null) solder.gameObject.SetActive(place == Place.Solder);
        if (prongs != null) prongs.gameObject.SetActive(PartChosen == CraftJewelPart.Prongs);
        if (size != null)
        {
            // a bail has no size to choose: its label, slider and value go together
            Transform row = size.transform.parent;
            int at = size.transform.GetSiblingIndex();
            for (int k = Mathf.Max(0, at - 1); k <= Mathf.Min(row.childCount - 1, at + 1); k++) row.GetChild(k).gameObject.SetActive(PartChosen != CraftJewelPart.Bail);
        }
        if (sizeTo != null)
        {
            sizeTo.gameObject.SetActive(place == Place.Form && PartChosen != CraftJewelPart.Bail && PartChosen != CraftJewelPart.Chain);
            TMP_Text t = sizeTo.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = PartChosen == CraftJewelPart.Band ? "Size to the finger" : "Size to the stone";
        }
    }

    // ------------------------------------------------------------------ the sizes
    private int PartIndex => Mathf.Clamp(part != null ? part.value : 0, 0, Parts.Length - 1);

    /// <summary>The slider's place (0-1) as a size (mm): even steps of the size's proportion, so a ring's band is set as finely as a circlet's.</summary>
    private float SizeOf(float v)
    {
        (float min, float max) = SizeRange[PartIndex];
        return min * Mathf.Pow(max / min, Mathf.Clamp01(v));
    }

    private float SliderOf(float mm)
    {
        (float min, float max) = SizeRange[PartIndex];
        return Mathf.Clamp01(Mathf.Log(Mathf.Clamp(mm, min, max) / min) / Mathf.Log(max / min));
    }

    /// <summary>The slider shows the chosen part's own size.</summary>
    private void ShowSize() => SetSlider(size, SliderOf(partSize[PartIndex]));

    /// <summary>A ring being made (the piece picked, if it is one).</summary>
    private CraftPiece RingBeingMade()
    {
        CraftPiece a = Assembly;
        return a != null && !a.assembly.finished && CraftJewelry.Takes(Rules.Design(a.assembly.design), true) ? a : null;
    }

    /// <summary>The finger measured: the ring's own, or the one waiting for the next ring; 0 when none.</summary>
    private double MeasuredFinger()
    {
        CraftPiece ring = RingBeingMade();
        if (ring != null && ring.assembly.measured && CraftJewelry.IsFinger(ring.assembly.fitSize)) return ring.assembly.fitSize;
        return Ws != null && Ws.WaitingMeasurement(true, out _, out double s) ? s : 0;
    }

    private void SizeToChosen()
    {
        if (PartChosen == CraftJewelPart.Band)
        {
            double finger = MeasuredFinger();
            if (finger <= 0) { said = "No finger measured yet: size it first (Size)."; return; }
            partSize[0] = (float)finger;
            said = $"The band set to the finger measured: {finger:0.00} mm.";
        }
        else
        {
            CraftPiece g = StoneChosen;
            if (g == null || g.gem.girdle <= 0) { said = "Choose a cut stone (above) to size the setting to."; return; }
            partSize[PartIndex] = (float)g.gem.girdle;
            said = $"Set to the stone's girdle: {g.gem.girdle:0.00} mm.";
        }
        ShowSize();
    }

    // ------------------------------------------------------------------ the hands, and the piece changing
    private void ResetHands()
    {
        if (taps > 0 || pressing) DropForming();
        pressing = burring = torching = pushing = buffing = steaming = false;
        LiveDraw = false;
    }

    /// <summary>The mandrel work on the piece: let go of (nothing was formed).</summary>
    private void DropForming()
    {
        Array.Clear(sectors, 0, sectors.Length);
        seam = 1f;
        seamOver = taps = 0;
        pressing = false;
        formFor = null;
    }

    public override void Leave() => ResetHands();

    /// <summary>The piece picked is changing: the tools are put down, and what belonged to the piece is dropped (each piece's buffing is kept with it).</summary>
    public override void SelectionChanging(string from, string to)
    {
        ResetHands();
        DropForming();
        tries = 0;
        tried = -1f;
        fits = false;
        heatFor = pushFor = "";
        fluxed.Clear();
        heat = 0f;
        lastPush = -999f;
        pushCount = 0;
        said = "";
    }

    public override void SelectionChanged(string to)
    {
        CraftPiece p = to != null && Station != null && Station.Workshop != null ? Ws.Piece(to) : null;
        // a cut stone picked: the one to seat a setting to
        if (p != null && p.kind == CraftPieceKind.Gem && stone != null)
        {
            RefreshStones();
            int at = stones.IndexOf(p.id);
            if (at >= 0) stone.SetValueWithoutNotify(at);
        }
        // a ring picked: its band is formed to the finger measured for it
        double finger = MeasuredFinger();
        if (finger > 0) partSize[0] = (float)finger;
        CraftPiece g = StoneChosen;
        if (g != null && g.gem.girdle > 0) partSize[1] = partSize[2] = (float)g.gem.girdle;
        jointsKey = "";
        ShowSize();
        Show();
    }

    /// <summary>Several taps on the mandrel, not done yet: leaving would lose them.</summary>
    public override string Busy => taps > 0 ? "forming it on the mandrel" : null;

    /// <summary>A setting already fitted to its piece can be picked here: its seat is cut in place (Seat).</summary>
    public override bool AcceptsFitted(CraftPiece part) =>
        part != null && part.kind == CraftPieceKind.Metal && (CraftJewelry.PartOf(part) == CraftJewelPart.Bezel || CraftJewelry.PartOf(part) == CraftJewelPart.Prongs);

    private void SayFor(string id, string text)
    {
        if (ui != null && ui.SelectedId == id) said = text ?? "";
    }

    public override void Tick(float dt)
    {
        if (torching && ui.PointerHeld) heat += dt * 0.28f / Mathf.Max(0.6f, Station.ToolQuality < 1f ? 1.2f : 1f);
        else heat = Mathf.Max(0f, heat - dt * 0.35f);
        if (burring && ui.PointerHeld)
        {
            seatMm += dt * BurrMmPerSecond;
            Vector2 h = ui.Hover;
            if (h.x >= 0) { wander += Vector2.Distance(h, burrLast); burrLast = h; }
        }
        else if (burring) burring = false; // the press ended off the surface: nothing cut
        if (buffing && ui.PointerHeld && finishFor != null)
        {
            buffed.TryGetValue(finishFor, out float b);
            buffed[finishFor] = Mathf.Min(1f, b + dt * (float)CraftJewelry.BuffPerSecond * Mathf.Min(1.2f, Station.ToolQuality));
        }
        if (steaming && ui.PointerHeld && finishFor != null)
        {
            steamed.TryGetValue(finishFor, out float s);
            steamed[finishFor] = Mathf.Min(SteamFull, s + dt);
        }
        LiveDraw = pressing || burring || torching || heat > 0f || pushing || buffing || steaming;
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

    private const float SizerY = 330f;
    private static float SizerX(int i) => 70f + i * 48f;
    private static float SizerMm(int i) => 14f + i * 0.75f;

    private static List<Vector2> Ellipse(Vector2 o, float rx, float ry, int n)
    {
        var pts = new List<Vector2>(n);
        for (int k = 0; k < n; k++)
        {
            float a = k * Mathf.PI * 2f / n;
            pts.Add(new Vector2(o.x + Mathf.Cos(a) * rx, o.y + Mathf.Sin(a) * ry));
        }
        return pts;
    }

    private void DrawSize(CraftPixels c)
    {
        // The ring sizer: a stick of rings, smallest first, each marked with its size; the finger above.
        c.Rect(SizerX(0) - 20, SizerY - 4, SizerX(12) + 20, SizerY + 4, Iron);
        for (int i = 0; i < 13; i++)
        {
            bool chosen = Mathf.Abs(SizerMm(i) - tried) < 0.01f;
            c.Ring(SizerX(i), SizerY, 8f + SizerMm(i) * 0.6f, 2f, chosen ? Mark : new Color(0.75f, 0.76f, 0.8f));
            if (i % 2 == 0) CraftArt.Label(c, SizerX(i) - CraftArt.LabelWidth($"{SizerMm(i):0}") / 2f, SizerY - 36, $"{SizerMm(i):0}", CraftArt.LabelDim);
        }
        CraftArt.Label(c, 40, 370, "RING SIZER: CLICK A RING TO TRY IT", CraftArt.LabelInk);
        // The finger (its own width), and the ring tried on it: stuck at the tip if tight, snug at the base, hanging loose.
        Color skin = new Color(0.55f, 0.45f, 0.38f);
        float half = Finger * 1.6f;
        c.Rect(330 - half, 40, 330 + half, 220, skin);
        c.Disc(330, 220, half, skin);
        CraftArt.Label(c, 330 + half + 10, 200, "FINGER", CraftArt.LabelDim);
        if (tried > 0)
        {
            float r = tried * 1.6f, y = fits ? 90f : tried < Finger ? 228f : 70f;
            Color col = fits ? Good : Harm;
            List<Vector2> ring = Ellipse(new Vector2(330 + (fits || tried < Finger ? 0 : 6), y), r + 2f, (r + 2f) * 0.35f, 40);
            c.Outline(ring, col, 4f);
            string word = fits ? "IT FITS" : tried < Finger ? "TOO TIGHT" : "TOO LOOSE";
            CraftArt.Label(c, 470, 160, $"TRIED {tried:0.00} MM", CraftArt.LabelInk);
            CraftArt.Label(c, 470, 140, word, col);
        }
        CraftPiece ring1 = RingBeingMade();
        if (ring1 != null && ring1.assembly.measured) CraftArt.Label(c, 470, 110, $"RING SIZED: {ring1.assembly.fitSize:0.00} MM", CraftArt.LabelDim);
    }

    private void DrawForm(CraftPixels c)
    {
        CraftPiece p = MetalPiece;
        float mm = PartChosen == CraftJewelPart.Bail ? 6f : partSize[PartIndex];
        // The mandrel from above, the metal bent round it as far as it has been tapped; the seam at the top.
        float r = MandrelPx;
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
        CraftArt.Label(c, Centre.x - CraftArt.LabelWidth("SEAM") / 2f, Mathf.Min(370f, Centre.y + r + 26f), "SEAM", CraftArt.LabelDim);
        // The tap's force, how round it is, how closed the seam - as the station will take them from these hands.
        c.Rect(450, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, 460, 350, PartChosen == CraftJewelPart.Bail ? "A BAIL" : $"{PartNames[PartIndex].ToUpperInvariant()} {mm:0.0} MM INSIDE", CraftArt.LabelInk);
        CraftArt.Label(c, 460, 320, "TAP", CraftArt.LabelInk);
        // the tap's force as it would land (a tired arm taps lighter): green from where a tap bends the metal
        Meter(c, 460, 302, 220, 10, pressing ? TapForce() : 0f, Mark, (TapBends, 1f, GoodBand));
        CraftArt.Label(c, 460, 280, "LIGHT TAPS DO NOT BEND IT", CraftArt.LabelDim);
        float round = Mathf.Min(Roundness(), 1f - 4f * Floor);
        CraftArt.Label(c, 460, 250, $"ROUND {round * 100:0}%", CraftArt.LabelInk);
        Meter(c, 460, 232, 220, 10, round, new Color(0.6f, 0.85f, 1f), (0.6f, 1f, GoodBand));
        float gapMm = GapMm;
        CraftArt.Label(c, 460, 205, $"SEAM GAP {gapMm:0.00} MM", CraftArt.LabelInk);
        Meter(c, 460, 187, 220, 10, Mathf.Clamp01(1f - gapMm / 0.8f), new Color(0.45f, 0.9f, 0.45f), (1f - 0.1f / 0.8f, 1f, GoodBand));
        CraftArt.Label(c, 460, 165, "CLOSED: IT TAKES THE SOLDER", CraftArt.LabelDim);
        if (p != null)
        {
            double t = 0;
            foreach (MetalSegment s in p.segments) t = Math.Max(t, Math.Min(s.width, s.thickness));
            double need = CraftJewelry.LengthNeeded(PartChosen, mm, t, ProngsChosen);
            CraftArt.Label(c, 460, 120, $"NEEDS {need:0} MM", CraftArt.LabelInk);
            CraftArt.Label(c, 460, 100, $"OF {p.Length:0} MM", need > p.Length ? Harm : CraftArt.LabelDim);
        }
    }

    /// <summary>A tap's force from which it bends the metal round the mandrel (lighter, it only rings).</summary>
    private const float TapBends = 0.25f;

    /// <summary>The force the mallet lands with if the hand lets go now: how long it was raised (full at half a second), as strong as the arm still is.</summary>
    private float TapForce() => Mathf.Clamp01((Time.unscaledTime - pressAt) / 0.5f) * (float)Hands.Strength;

    /// <summary>The radius (px) the mandrel is drawn at for the part being formed (the metal bends round it, up to 22 px out while unbent).</summary>
    private float MandrelPx => Mathf.Clamp((PartChosen == CraftJewelPart.Bail ? 6f : partSize[PartIndex]) * 4f, 30f, 145f);

    /// <summary>Is a point on the metal round the mandrel (where a tap lands on it), not off on the bench or the gauges?</summary>
    private bool OnTheMetal(Vector2 at)
    {
        float d = Vector2.Distance(at, Centre), r = MandrelPx;
        return d > r - 18f && d < r + 40f;
    }

    /// <summary>The seam's gap as the station takes it (mm): never closer than these hands can close it.</summary>
    private float GapMm => Mathf.Max(Mathf.Max(0f, seam) * 0.8f, 2f * Floor);

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

    /// <summary>
    /// The setting the Seat place cuts: the bezel or prong head picked, or - the piece picked - the setting fitted to it
    /// (the seat is cut in place); <paramref name="why"/> says what is missing.
    /// </summary>
    private CraftPiece SettingToSeat(out bool fitted, out string why)
    {
        fitted = false;
        why = "Pick the bezel or prong head - or the piece it is fitted to (left) - and the stone (above).";
        CraftPiece p = Selected;
        if (p == null) return null;
        if (p.kind == CraftPieceKind.Metal)
        {
            CraftJewelPart jp = CraftJewelry.PartOf(p);
            if (jp == CraftJewelPart.Bezel || jp == CraftJewelPart.Prongs) return p;
            why = jp == CraftJewelPart.None ? "Form it into a bezel or a prong head first (Form)." : "A seat is cut in a bezel or a prong head.";
            return null;
        }
        if (p.kind == CraftPieceKind.Assembly)
        {
            CraftDesign d = Rules.Design(p.assembly.design);
            if (d != null)
                foreach (CraftComponentSpec c in d.components)
                {
                    if (c.function != CraftComponentFunction.Setting) continue;
                    CraftPiece s = Ws.Piece(p.assembly.PieceIn(c.role));
                    if (s != null) { fitted = true; return s; }
                    why = $"Fit its {c.Name.ToLowerInvariant()} first (Assembly), or pick the loose one.";
                }
        }
        return null;
    }

    /// <summary>Pixels per millimetre of the seat drawing: the largest of collar, seat and girdle fills about 165 px of radius.</summary>
    private static float SeatScale(float largest) => Mathf.Min(48f, 165f / Mathf.Max(1f, largest * 0.5f + 0.6f));

    private void DrawSeat(CraftPixels c)
    {
        CraftPiece p = SettingToSeat(out bool fitted, out _), g = StoneChosen;
        Color m = MetalColour(p);
        float collar = p != null ? (float)p.Tag(CraftJewelry.SizeTag) : 6f;
        float girdle = g != null ? (float)g.gem.girdle : 6f;
        float had = p != null ? (float)p.Tag(CraftJewelry.SeatTag) : 0f;
        float seat = Mathf.Max(had, burring ? seatMm : 0f);
        float k = SeatScale(Mathf.Max(collar, Mathf.Max(seat, girdle)));
        // the setting's wall (a strip about half a millimetre thick), the snug band round the girdle, the seat cut, the girdle
        c.Ring(Centre.x, Centre.y, (collar * 0.5f + 0.25f) * k, 0.5f * k, m);
        Color snug = new Color(Good.r, Good.g, Good.b, 0.45f);
        float inner = (girdle - (float)CraftJewelry.SeatTooSmall) * 0.5f * k, outer = (girdle + (float)CraftJewelry.SeatSnug) * 0.5f * k;
        c.Ring(Centre.x, Centre.y, (inner + outer) * 0.5f, Mathf.Max(2f, outer - inner), snug);
        if (seat > 0) c.Ring(Centre.x, Centre.y, seat * 0.5f * k, 2f, Mark);
        c.Ring(Centre.x, Centre.y, girdle * 0.5f * k, 1.5f, Color.white);
        c.Disc(Centre.x, Centre.y, 6f, burring ? new Color(Good.r, Good.g, Good.b, 0.6f) : new Color(1f, 1f, 1f, 0.3f));
        if (burring && ui.Hover.x >= 0) c.Disc(ui.Hover.x, ui.Hover.y, 4f, Iron);
        // the numbers, and how the burr is going
        c.Rect(450, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, 460, 350, fitted ? "SEAT IN THE FITTED SETTING" : "SEAT FOR THE STONE", CraftArt.LabelInk);
        CraftArt.Label(c, 460, 320, $"COLLAR {collar:0.00} MM", m);
        CraftArt.Label(c, 460, 300, g != null ? $"GIRDLE {girdle:0.00} MM" : "NO STONE CHOSEN", Color.white);
        CraftArt.Label(c, 460, 280, $"SEAT {seat:0.00} MM", Mark);
        CraftArt.Label(c, 460, 260, "GREEN RING: SNUG", Good);
        if (p != null && g != null)
        {
            string problem = CraftJewelry.CollarProblem(p, g);
            if (problem != null) CraftArt.Label(c, 460, 230, CraftJewelry.CollarSlack(p, g) > 0 ? "COLLAR TOO WIDE" : "COLLAR TOO SMALL", Harm);
            else if (seat > 0) CraftArt.Label(c, 460, 230, SeatWord(seat - girdle), SeatColour(seat - girdle));
        }
        float clean = burring ? Mathf.Min(BurrClean(), 1f - 3f * Floor) : 0f, level = burring ? Mathf.Min(BurrLevel(), 1f - 4f * Floor) : 0f;
        CraftArt.Label(c, 460, 190, "LEVEL (HOLD STILL)", CraftArt.LabelInk);
        Meter(c, 460, 172, 220, 10, level, new Color(0.6f, 0.85f, 1f), (0.6f, 1f, GoodBand));
        CraftArt.Label(c, 460, 140, "CLEAN (NOT TOO LONG)", CraftArt.LabelInk);
        Meter(c, 460, 122, 220, 10, clean, new Color(0.45f, 0.9f, 0.45f), (0.5f, 1f, GoodBand));
    }

    // the seat's words, as the record's (CraftJewelry.SeatWords): too wide where the stone can never be set in it
    private static string SeatWord(double err) { double f = CraftJewelry.SeatFitOf(err); return err < -CraftJewelry.SeatTooSmall ? "STILL TOO SMALL" : f >= 0.95 ? "SNUG" : f >= CraftJewelry.SeatHolds ? "A LITTLE WIDE" : "TOO WIDE"; }
    private static Color SeatColour(double err) { double f = CraftJewelry.SeatFitOf(err); return err < -CraftJewelry.SeatTooSmall ? CraftArt.LabelDim : f >= 0.95 ? Good : f >= CraftJewelry.SeatHolds ? Mark : Harm; }

    /// <summary>How level the burr was held: the hand's drift by the second (180 px a second loses it all - at any frame rate), and its tremble.</summary>
    private float BurrLevel() => Mathf.Clamp01(1f - wander / Mathf.Max(0.2f, Time.unscaledTime - burrStart) / 180f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
    private float BurrClean() => Mathf.Clamp01(1f - Mathf.Max(0f, Time.unscaledTime - burrStart - 6f) / 6f);

    /// <summary>The setting and the stone of the setting joint chosen (Set).</summary>
    private CraftPiece SettingOf(CraftPiece asm, out CraftPiece gem)
    {
        gem = null;
        if (asm == null || JointId == null) return null;
        CraftJointSpec j = Rules.Design(asm.assembly.design)?.Joint(JointId);
        if (j == null) return null;
        CraftJewelry.SettingParts(Ws, asm, j, out CraftPiece setting, out gem);
        return setting;
    }

    /// <summary>The stone already in a piece, if any.</summary>
    private CraftPiece StoneIn(CraftPiece asm)
    {
        if (asm == null) return null;
        foreach (AssemblyPart ap in asm.assembly.parts)
        {
            CraftPiece q = Ws.Piece(ap.piece);
            if (q != null && q.kind == CraftPieceKind.Gem) return q;
        }
        return null;
    }

    /// <summary>Are all the piece's solder joints done (the stone may go in)?</summary>
    private bool Soldered(CraftPiece asm)
    {
        CraftDesign d = asm != null ? Rules.Design(asm.assembly.design) : null;
        if (d == null) return true;
        foreach (CraftJointSpec j in d.joints)
            if (j.kind == CraftJointKind.Solder && CraftAssembly.JointApplies(Ws, asm, d, j) && !(asm.assembly.Joint(j.id)?.done ?? false)) return false;
        return true;
    }

    // the seam on the soldering block: the two parts either side of a gap as wide as the parts' seams left it
    private const float BlockY0 = 120f, BlockY1 = 200f, SeamX = 250f;
    /// <summary>The heat (°C) the solder chosen flows at, for the colour the parts glow (the torch's heat is kept as a share of it).</summary>
    private float SolderGlowC => (solders.Count > 0 && solder != null ? Rules.Supply(solders[Mathf.Clamp(solder.value, 0, solders.Count - 1)]) : null)?.FlowTemperature ?? 760f;
    private float SeamGapPx(double gap) => 4f + (float)gap * 40f;

    private void DrawSolder(CraftPixels c)
    {
        CraftPiece a = Assembly;
        if (a == null) { CraftArt.Label(c, 60, 200, "PICK THE PIECE BEING MADE", CraftArt.LabelDim); return; }
        CraftJointSpec spec = JointId != null ? Rules.Design(a.assembly.design)?.Joint(JointId) : null;
        if (heatFor != a.id + JointId) { heatFor = a.id + JointId; fluxed.Clear(); heat = 0f; }
        double gap = spec != null ? CraftJewelry.JointGap(Ws, a, spec) : 0;
        float half = SeamGapPx(gap) * 0.5f;
        // The two parts on the soldering block, the seam between them, fluxed where it was brushed.
        c.Rect(60, 60, 440, BlockY0, new Color(0.45f, 0.42f, 0.38f));
        Color m = MetalColour(null);
        foreach (AssemblyPart ap in a.assembly.parts) { CraftPiece q = Ws.Piece(ap.piece); if (q != null && q.kind == CraftPieceKind.Metal) { m = MetalColour(q); break; } }
        // It glows as hot metal does at the forge (CraftArt.Heat), the solder flowing at its own flow point (SolderGlowC: silver
        // solder a little below gold's): it runs from 85% of its flow, fully at it - a cherry red; past 112% the metal pits.
        Color hot = CraftArt.Heat(heat * SolderGlowC, ui.Dark, out float hk);
        Color glow = Color.Lerp(m, hot, hk);
        c.Rect(80, BlockY0, SeamX - half, BlockY1, glow);
        c.Rect(SeamX + half, BlockY0, 420, BlockY1, glow);
        for (int k = 0; k < 10; k++)
            c.Rect(SeamX - half - 6, BlockY0 + k * 8, SeamX + half + 6, BlockY0 + 8 + k * 8, fluxed.Contains(k) ? new Color(0.9f, 0.9f, 0.85f, 0.8f) : new Color(0.1f, 0.1f, 0.1f, 0.6f));
        float run = (float)CraftMath.Smooth(0.85, 1.0, heat);
        if (run > 0f && heat < 1.3f) c.Rect(SeamX - half - 2, BlockY0, SeamX + half + 2, BlockY1, new Color(1f, 1f, 0.85f, run)); // the solder runs
        if (heat > 1.12f) for (int k = 0; k < 6; k++) c.Disc(90 + k * 55, 160, 3f, new Color(0.2f, 0.15f, 0.12f));
        CraftArt.Label(c, SeamX - CraftArt.LabelWidth("SEAM") / 2f, BlockY1 + 8, "SEAM", CraftArt.LabelDim);
        CraftArt.Label(c, 80, BlockY0 - 22, gap > 0.1 ? $"GAP {gap:0.00} MM: CLOSE IT ON THE MANDREL" : $"GAP {gap:0.00} MM", gap > 0.1 ? Fair : CraftArt.LabelDim);
        // A stone already in the piece feels the heat.
        CraftPiece g = StoneIn(a);
        if (g != null)
        {
            CraftGemDefinition d = Rules.Gem(g.gem.gem);
            Color sc = d != null ? d.color : Color.white;
            sc.a = 1f;
            c.Disc(340, 240, 18f, heat > 0.6f ? Color.Lerp(sc, new Color(1f, 0.3f, 0.2f), Mathf.Clamp01((heat - 0.6f) * 2f)) : sc);
            CraftArt.Label(c, 230, 270, "THE STONE IS IN THE PIECE", Fair);
        }
        if (torching && ui.Hover.x >= 0) c.Halo(ui.Hover.x, ui.Hover.y, 40f, new Color(0.5f, 0.6f, 1f), 0.6f);
        // The heat against the solder's flow, the flux against what the joint needs to take.
        c.Rect(450, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        CraftArt.Label(c, 460, 350, "HEAT (TORCH: HOLD)", CraftArt.LabelInk);
        const float top = 1.3f;
        float flux = fluxed.Count / 10f;
        // from the heat the solder starts to run (dim) to the heat this flux and this seam take at (green), to the pitting (red)
        float takes = TakingHeat(flux, gap);
        if (takes > 0f)
            Meter(c, 460, 330, 220, 12, heat / top, new Color(1f, 0.6f, 0.3f), (0.85f / top, takes / top, FairBand), (takes / top, 1.12f / top, GoodBand), (1.12f / top, 1f, HarmBand));
        else
            Meter(c, 460, 330, 220, 12, heat / top, new Color(1f, 0.6f, 0.3f), (0.85f / top, 1.12f / top, FairBand), (1.12f / top, 1f, HarmBand));
        CraftArt.Label(c, 460, 312, takes > 0f ? "GREEN: IT TAKES  RED: IT PITS" : "MORE FLUX: IT CANNOT TAKE YET", takes > 0f ? CraftArt.LabelDim : Fair);
        double reach = CraftJewelry.FluxReach(1, gap);
        float least = reach < 0.5 ? 1f : Mathf.Clamp01((float)((0.5 / Math.Exp(-gap / 0.35) - 0.35) / 0.65));
        CraftArt.Label(c, 460, 270, "FLUX (BRUSH THE SEAM)", CraftArt.LabelInk);
        Meter(c, 460, 250, 220, 12, flux, new Color(0.9f, 0.9f, 0.85f), (least, 1f, reach < 0.5 ? HarmBand : GoodBand));
        CraftArt.Label(c, 460, 232, reach < 0.5 ? "SEAM GAPES: CANNOT TAKE" : "GREEN: ENOUGH TO TAKE", reach < 0.5 ? Fair : CraftArt.LabelDim);
        JointState j = JointId != null ? a.assembly.Joint(JointId) : null;
        if (j != null)
        {
            CraftArt.Label(c, 460, 190, "JOINT", CraftArt.LabelInk);
            Meter(c, 460, 170, 220, 12, (float)j.solder, new Color(0.9f, 0.8f, 0.4f), (0.5f, 1f, GoodBand));
            CraftArt.Label(c, 460, 152, j.solder >= 0.5 ? "IT HAS TAKEN" : "NOT JOINED YET", j.solder >= 0.5 ? Good : CraftArt.LabelDim);
        }
    }

    /// <summary>The least heat at which a joint this fluxed and this gapped takes (its flow reaching half, <see cref="CraftJewelry.SolderFlow"/>); 0 when it never can.</summary>
    private static float TakingHeat(float flux, double gap)
    {
        if (CraftJewelry.SolderFlow(1.0, flux, gap) < 0.5) return 0f;
        double lo = 0.85, hi = 1.0;
        for (int i = 0; i < 20; i++)
        {
            double mid = (lo + hi) * 0.5;
            if (CraftJewelry.SolderFlow(mid, flux, gap) >= 0.5) hi = mid; else lo = mid;
        }
        return (float)hi;
    }

    private void DrawSet(CraftPixels c)
    {
        CraftPiece a = Assembly;
        CraftPiece setting = SettingOf(a, out CraftPiece gem);
        if (setting == null) { CraftArt.Label(c, 60, 200, a == null ? "PICK THE PIECE BEING MADE" : "FIT THE SETTING AND THE STONE", CraftArt.LabelDim); return; }
        Color m = MetalColour(setting);
        CraftGemDefinition d = gem != null ? Rules.Gem(gem.gem.gem) : null;
        Color sc = d != null ? d.color : new Color(0.8f, 0.8f, 0.85f);
        sc.a = 1f;
        JointState j = a.assembly.Joint(JointId);
        float held = j != null ? (float)Math.Min(1, j.security / 0.85) : 0f;
        c.Disc(Centre.x, Centre.y, 110f, sc * 0.8f);
        c.Disc(Centre.x - 30, Centre.y + 30, 30f, Color.Lerp(sc, Color.white, 0.4f));
        bool prongHead = CraftJewelry.PartOf(setting) == CraftJewelPart.Prongs;
        if (prongHead)
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
        // where the next push should land
        if (lastPush > -900f && ui.Hover.x >= 0)
        {
            float ideal = IdealPush(setting) * Mathf.Deg2Rad;
            c.Disc(Centre.x + Mathf.Cos(ideal) * 130f, Centre.y + Mathf.Sin(ideal) * 130f, 5f, new Color(Good.r, Good.g, Good.b, 0.7f));
        }
        c.Rect(450, 40, 690, 370, new Color(0.2f, 0.19f, 0.18f));
        bool brittle = d != null && d.toughness < CraftJewelry.ChipToughness;
        CraftArt.Label(c, 460, 350, "PUSH (HOLD, LET GO)", CraftArt.LabelInk);
        float press = pushing ? Mathf.Clamp01((Time.unscaledTime - pushStart) / 0.6f) * (float)Hands.Strength : 0f;
        Meter(c, 460, 330, 220, 12, press, Mark, (0.4f, (float)CraftJewelry.ChipPress, GoodBand), ((float)CraftJewelry.ChipPress, 1f, HarmBand));
        CraftArt.Label(c, 460, 312, brittle ? "RED: THIS STONE CHIPS" : "RED: A BRITTLE STONE CHIPS", brittle ? Fair : CraftArt.LabelDim);
        CraftJointSpec spec = Rules.Design(a.assembly.design)?.Joint(JointId);
        int needed = spec != null && spec.kind == CraftJointKind.ProngSet ? Math.Max(1, spec.count) : 0;
        CraftArt.Label(c, 460, 280, "SECURE", CraftArt.LabelInk);
        // green from where the stone is set: the line the inspection and the finished piece judge it by too
        float setAt = (float)(CraftJewelry.SetSecure / 0.85);
        Meter(c, 460, 260, 220, 12, held, new Color(0.45f, 0.9f, 0.45f), (setAt, 1f, GoodBand));
        CraftArt.Label(c, 460, 242, "GREEN: SET", CraftArt.LabelDim);
        if (setting != null && gem != null)
        {
            double mostSecure = CraftJewelry.MostSecure(setting, gem), most = mostSecure / 0.85;
            // short of the green the seat (or the collar) can never hold it: a new setting
            if (mostSecure < CraftJewelry.SetSecure) CraftArt.Label(c, 560, 242, "IT CANNOT HOLD", Harm);
            else if (most < 0.99) CraftArt.Label(c, 560, 242, $"AT MOST {Math.Min(1, most) * 100:0}%", Fair);
        }
        CraftArt.Label(c, 460, 210, needed > 0 ? $"PUSHES {j?.pushes ?? 0}/{needed}" : $"PUSHES {j?.pushes ?? 0}", CraftArt.LabelInk);
        float tilt = j != null ? (float)j.tilt : 0f;
        CraftArt.Label(c, 460, 180, "TILT", CraftArt.LabelInk);
        Meter(c, 460, 160, 220, 12, tilt, new Color(1f, 0.8f, 0.4f), (0.25f, 1f, HarmBand));
        CraftArt.Label(c, 460, 142, "OPPOSITE SIDES IN TURN", CraftArt.LabelDim);
    }

    private void DrawFinish(CraftPixels c)
    {
        CraftPiece a = Assembly;
        // The buffing wheel, and the piece's shine as it is buffed; the steam jet.
        c.Disc(560, 200, 90f, new Color(0.85f, 0.8f, 0.7f));
        c.Ring(560, 200, 90f, 3f, new Color(0.6f, 0.55f, 0.5f));
        for (int k = 0; k < 6 && buffing; k++)
        {
            float ang = Time.unscaledTime * 8f + k * Mathf.PI / 3f;
            c.Line(560, 200, 560 + Mathf.Cos(ang) * 80f, 200 + Mathf.Sin(ang) * 80f, new Color(0.7f, 0.65f, 0.55f), 1f);
        }
        CraftArt.Label(c, 560 - CraftArt.LabelWidth("BUFFING WHEEL") / 2f, 96, "BUFFING WHEEL", CraftArt.LabelDim);
        c.Rect(560, 310, 600, 370, Iron);
        CraftArt.Label(c, 610, 340, "STEAM", CraftArt.LabelDim);
        if (steaming) c.Halo(580, 290, 40f, new Color(1f, 1f, 1f), 0.5f);
        if (a == null) { CraftArt.Label(c, 60, 200, "PICK THE PIECE BEING MADE", CraftArt.LabelDim); return; }
        Color m = Color.gray;
        foreach (AssemblyPart ap in a.assembly.parts) { CraftPiece q = Ws.Piece(ap.piece); if (q != null && q.kind == CraftPieceKind.Metal) { m = MetalColour(q); break; } }
        // as the station will take it: never brighter than these hands buff
        float buff = Mathf.Min(Buffed(a.id), 1f - 3f * Floor), steam = Steamed(a.id);
        float shine = Mathf.Max(buff, (float)a.assembly.finish);
        c.Ring(Centre.x - 20, Centre.y, 90f, 18f, Color.Lerp(m * 0.65f, Color.Lerp(m, Color.white, 0.35f), shine));
        CraftArt.Label(c, 60, 360, $"BUFFED {shine * 100:0}%", CraftArt.LabelInk);
        Meter(c, 60, 340, 300, 10, shine, Mark, (0.83f, 1f, GoodBand));
        CraftArt.Label(c, 60, 52, $"STEAMED {steam / SteamFull * 100:0}%", CraftArt.LabelInk);
        Meter(c, 60, 34, 300, 8, Mathf.Clamp01(steam / SteamFull), new Color(0.6f, 0.85f, 1f));
    }

    private float Buffed(string id) => id != null && buffed.TryGetValue(id, out float b) ? b : 0f;
    private float Steamed(string id) => id != null && steamed.TryGetValue(id, out float s) ? s : 0f;

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
                // Finding it in few tries is a careful measure; many tries, a muddled one (never truer than these hands). The
                // ring that fits is the sizer's nearest to the finger: how near its steps come to it is the sizer's, not the
                // jeweller's, so it does not count against the measure (the band is formed to the ring tried).
                float accuracy = Mathf.Min(Mathf.Clamp01(1.1f - tries * 0.05f), 1f - 4f * Floor);
                CraftPiece ring = RingBeingMade();
                float size1 = tried;
                Send(new CraftRequest(CraftRequestKind.MeasureFit, ring?.id) { a = accuracy, b = size1 }, (ok, why) =>
                {
                    if (!ok) return;
                    partSize[0] = size1;
                    if (PartChosen == CraftJewelPart.Band) ShowSize();
                    said = $"It fits snugly: {size1:0.00} mm - {(ring != null ? "the ring takes it" : "it waits for the next ring started")}; form the band to it.";
                });
                tries = 0;
            }
            return;
        }
    }

    private void FormPointer(CraftPointer phase, Vector2 pos)
    {
        CraftPiece p = MetalPiece;
        if (phase == CraftPointer.Cancel) { if (pressing) said = "The mallet is lifted: no tap."; pressing = false; return; }
        if (p == null) { if (phase == CraftPointer.Down) said = "Pick a length of wire or bezel strip (left)."; return; }
        if (phase == CraftPointer.Down)
        {
            if (!OnTheMetal(pos)) { said = "Tap the metal round the mandrel."; return; } // a click on the bench or the gauges taps nothing
            if (taps > 0 && formFor != p.id) DropForming();
            if (taps == 0 && !Ws.CanFormShank(p.id, PartChosen, partSize[PartIndex], ProngsChosen, out string why)) { said = why; return; } // before any tapping
            pressing = true;
            formFor = p.id;
            pressAt = Time.unscaledTime;
            return;
        }
        if (phase != CraftPointer.Up || !pressing) return;
        pressing = false;
        // the mallet comes down where the hand lets go: off the metal, it taps nothing
        if (!OnTheMetal(pos)) { said = "Tap the metal round the mandrel: that came down off it."; return; }
        float force = TapForce();
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
        sectors[s] += force > TapBends ? 1 : 0;
        said = force <= TapBends ? "Too light a tap." : sectors[s] > 3 ? "Tapped there too often: it flattens." : "It bends round the mandrel.";
    }

    private void FormDone()
    {
        CraftPiece p = MetalPiece;
        if (p == null) { said = "Pick a length of wire or bezel strip (left)."; return; }
        if (taps == 0 || formFor != p.id) { said = "Tap it round the mandrel first."; return; }
        CraftJewelPart jp = PartChosen;
        float round = Mathf.Min(Roundness(), 1f - 4f * Floor), gapMm = GapMm, mm = jp == CraftJewelPart.Bail ? 6f : partSize[PartIndex];
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.FormShank, id) { i0 = (int)jp, a = mm, b = round, c = gapMm, i1 = ProngsChosen }, (ok, why) =>
        {
            if (!ok) return; // refused: the taps are kept (the reason says what to change)
            if (formFor == id) DropForming();
            SayFor(id, $"{LastRecord(id)}. {(round < 0.6f ? "Out of round." : gapMm > 0.3f ? "The seam gapes." : "True, the seam closed.")}");
        });
    }

    private void SeatPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Cancel) { if (burring) said = "The burr is lifted: nothing cut."; burring = false; return; }
        CraftPiece p = SettingToSeat(out bool fitted, out string why), g = StoneChosen;
        if (phase == CraftPointer.Down)
        {
            if (p == null || g == null) { said = p == null ? why : "Choose the stone (above): the seat is cut to its girdle."; return; }
            string problem = CraftJewelry.CollarProblem(p, g);
            if (problem != null) { said = problem; return; }
            if (Vector2.Distance(pos, Centre) > 30f) { said = "Hold the burr in the middle of the setting."; return; }
            burring = true;
            burrSetting = p.id;
            burrStone = g.id;
            burrStart = Time.unscaledTime;
            float had = (float)p.Tag(CraftJewelry.SeatTag);
            // a new seat starts a little under the girdle (the same way to go for a small stone or a large one); a seat
            // already cut goes on from where it is
            seatMm = had > 0 ? had : Mathf.Max((float)g.gem.girdle * 0.8f, (float)g.gem.girdle - BurrStartUnder);
            wander = 0f;
            burrLast = pos;
            return;
        }
        if (phase != CraftPointer.Up || !burring) return;
        burring = false;
        if (p == null || g == null || p.id != burrSetting || g.id != burrStone) return;
        float level = Mathf.Min(BurrLevel(), 1f - 4f * Floor), clean = Mathf.Min(BurrClean(), 1f - 3f * Floor);
        string setting = p.id, picked = ui.SelectedId;
        float cut = seatMm;
        double girdle = g.gem.girdle;
        Send(new CraftRequest(CraftRequestKind.SeatSetting, setting) { other = g.id, a = cut, b = level, c = clean }, (ok, why2) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(setting);
            double err = (q != null ? q.Tag(CraftJewelry.SeatTag) : cut) - girdle;
            SayFor(picked, $"{LastRecord(setting)}{(err > CraftJewelry.SeatSnug && CraftJewelry.SeatFitOf(err) >= CraftJewelry.SeatHolds ? " (it holds less tightly)" : "")}.{(clean < 0.5f ? " The burr ran on: the seat is ragged." : "")}");
        });
    }

    /// <summary>
    /// Puts the solder the piece needs in it (from the bag), then does <paramref name="then"/> - in the answer, so nothing is
    /// tried without it. Says what is missing, and does nothing, when there is no solder.
    /// </summary>
    private void WithSolder(CraftPiece a, Action then)
    {
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d == null) return;
        string id = solders.Count > 0 ? solders[Mathf.Clamp(solder.value, 0, solders.Count - 1)] : null;
        foreach (CraftComponentSpec c in d.components)
        {
            if (!c.supply || c.supplyKind != CraftSupplyKind.Solder || a.assembly.Supply(c.role) >= c.supplyCount) continue;
            int n = Mathf.Max(1, c.supplyCount - (int)a.assembly.Supply(c.role));
            Send(new CraftRequest(CraftRequestKind.Attach, a.id) { id = c.role, count = n, other = id }, (ok, why) => { if (ok) then(); });
            return;
        }
        then();
    }

    /// <summary>Is there solder for the piece: in it already, or in the bag (the one chosen)?</summary>
    private bool SolderReady(CraftPiece a, out string missing)
    {
        missing = "";
        CraftDesign d = Rules.Design(a.assembly.design);
        string id = solders.Count > 0 ? solders[Mathf.Clamp(solder.value, 0, solders.Count - 1)] : null;
        if (d == null) return true;
        foreach (CraftComponentSpec c in d.components)
        {
            if (!c.supply || c.supplyKind != CraftSupplyKind.Solder || a.assembly.Supply(c.role) >= c.supplyCount) continue;
            if (id == null) { missing = "There is no solder to use here."; return false; }
            if (Have(id) == 0) { missing = $"You have no {ItemOf(id)}: bring some (the solder goes into the joint)."; return false; }
        }
        return true;
    }

    private void SolderPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Cancel) { if (torching) said = "The torch is lifted: the joint is not tried."; torching = false; return; }
        CraftPiece a = Assembly;
        if (a == null || JointId == null) { if (phase == CraftPointer.Down) said = "Pick the piece being made (start it at the Assembly area) and a solder joint."; return; }
        CraftJointSpec j = Rules.Design(a.assembly.design)?.Joint(JointId);
        if (j == null || j.kind != CraftJointKind.Solder) { if (phase == CraftPointer.Down) said = "That joint is not soldered."; return; }
        // On the seam: a brush stroke of flux; on the metal, the torch.
        float half = SeamGapPx(CraftJewelry.JointGap(Ws, a, j)) * 0.5f + 6f;
        if (pos.x > SeamX - half && pos.x < SeamX + half && pos.y > BlockY0 - 4 && pos.y < BlockY1 + 4 && (phase == CraftPointer.Down || phase == CraftPointer.Drag) && !torching)
        {
            fluxed.Add(Mathf.Clamp(Mathf.FloorToInt((pos.y - BlockY0) / 8f), 0, 9));
            return;
        }
        if (phase == CraftPointer.Down && pos.y > BlockY0 - 10 && pos.y < BlockY1 + 10 && pos.x > 70 && pos.x < 430)
        {
            if (!SolderReady(a, out string missing)) { said = missing; return; } // before any heating
            torching = true;
            if (StoneIn(a) != null && !Soldered(a)) said = "The stone is in the piece: the torch's heat reaches it. Solder before setting - take it out first if you can.";
            return;
        }
        if (phase != CraftPointer.Up || !torching) return;
        torching = false;
        float peak = heat, flux = fluxed.Count / 10f;
        string asm = a.id, jid = JointId;
        CraftPiece stone = StoneIn(a);
        double stoneHeat = stone != null ? stone.gem.heat : 0;
        // the solder goes in first; the joint is tried in its answer (never without it)
        WithSolder(a, () => Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = jid, i0 = (int)CraftJointWork.Solder, count = 1, a = peak, b = flux }, (ok, why) =>
        {
            CraftPiece q = ok ? Ws.Piece(asm) : null;
            JointState js = q?.assembly.Joint(jid);
            if (js == null) return;
            double gap = CraftJewelry.JointGap(Ws, q, j);
            string words = js.solder >= 0.5 ? (peak > 1.12f ? "The joint has taken - but overheated: the solder pitted, the metal is firescaled." : "The solder ran along the seam: the joint has taken.")
                : peak < 0.85f ? "Not hot enough: the solder balled up and did not run - heat it longer."
                : CraftJewelry.FluxReach(flux, gap) < 0.5 ? (gap > 0.1 && CraftJewelry.FluxReach(1, gap) < 0.5 ? $"It will not take: the seam gapes ({gap:0.00} mm) - close it on the mandrel." : "It did not take: the flux is too thin - brush the whole seam.")
                : "It did not take: short of its flow - heat it to the green.";
            SayFor(asm, words);
            if (stone != null && Ws.Piece(stone.id) != null && Ws.Piece(stone.id).gem.heat > stoneHeat + 1e-6)
                ui.Message("The torch's heat reached the stone in its setting: it cracked. Solder before setting the stone.");
        }));
    }

    /// <summary>Where the next push should land: across from the last, then a quarter round - on a prong head, at the prong nearest that.</summary>
    private float IdealPush(CraftPiece setting)
    {
        float ideal = pushCount % 2 == 1 ? lastPush + 180f : lastPush + 90f;
        if (CraftJewelry.PartOf(setting) == CraftJewelPart.Prongs)
        {
            int n = Math.Max(3, (int)setting.Tag(CraftJewelry.ProngTag));
            float step = 360f / n;
            ideal = Mathf.Round(ideal / step) * step;
        }
        return (ideal % 360f + 360f) % 360f;
    }

    private void SetPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Cancel) { if (pushing) said = "The pusher is lifted: no push."; pushing = false; return; }
        CraftPiece a = Assembly;
        CraftPiece setting = SettingOf(a, out CraftPiece gem);
        if (setting == null || gem == null) { if (phase == CraftPointer.Down) said = a == null ? "Pick the piece (left)." : "Fit the setting and the stone in it first (Assembly), and choose the setting joint."; return; }
        string key = a.id + JointId;
        if (pushFor != key) { pushFor = key; lastPush = -999f; pushCount = 0; }
        if (phase == CraftPointer.Down && Vector2.Distance(pos, Centre) > 90f && Vector2.Distance(pos, Centre) < 140f) { pushing = true; pushStart = Time.unscaledTime; return; }
        if (phase != CraftPointer.Up || !pushing) return;
        pushing = false;
        float press = Mathf.Clamp01((Time.unscaledTime - pushStart) / 0.6f) * (float)Hands.Strength;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(pushCount + 241) * 40f, (float)Hands.Tremble(pushCount + 242) * 40f);
        float ang = (Mathf.Atan2(at.y - Centre.y, at.x - Centre.x) * Mathf.Rad2Deg + 360f) % 360f;
        // Prongs: a push between the prongs only bends the air.
        if (CraftJewelry.PartOf(setting) == CraftJewelPart.Prongs)
        {
            int n = Math.Max(3, (int)setting.Tag(CraftJewelry.ProngTag));
            float step = 360f / n, off = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / step) * step));
            if (off > 15f) { said = "That was between the prongs."; return; }
        }
        // Opposite sides in turn: each push should land across from the last, then a quarter round for the next pair.
        float even = lastPush < -900f ? 1f : Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(ang, IdealPush(setting))) / 90f);
        even = Mathf.Min(even, 1f - 4f * Floor);
        lastPush = ang;
        pushCount++;
        Hands.Move(0.08);
        string asm = a.id, jid = JointId;
        double chips = gem.gem.chips;
        string gemId = gem.id, settingId = setting.id;
        said = press > CraftJewelry.ChipPress ? "Pushed hard - careful of the stone." : even < 0.5f ? "Push the opposite side next, or the stone tilts." : "Pushed over.";
        // each push counts as it is made: nothing waits to be sent when the place or the piece changes
        Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = jid, i0 = (int)CraftJointWork.Push, count = 1, a = press, b = even }, (ok, why) =>
        {
            CraftPiece g2 = ok ? Ws.Piece(gemId) : null;
            if (g2 != null && g2.gem.chips > chips + 1e-6) ui.Message("Pushed too hard: the stone chipped under the setting.");
            JointState js = ok ? Ws.Piece(asm)?.assembly.Joint(jid) : null;
            CraftJointSpec spec = Rules.Design(Ws.Piece(asm)?.assembly.design ?? "")?.Joint(jid);
            if (js == null || spec == null || ui.SelectedId != asm) return;
            if (CraftAssembly.JointDone(spec, js)) said = "The stone is set: it holds.";
            else if (g2 != null && Ws.Piece(settingId) is CraftPiece sp && CraftJewelry.MostSecure(sp, g2) < CraftJewelry.SetSecure)
                said = "It will never hold in this setting: the seat (or the collar) is too wide for the stone - start a new setting.";
        });
    }

    private void FinishPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            CraftPiece a = Assembly;
            if (a == null) { said = "Pick the piece (left)."; return; }
            if (Vector2.Distance(pos, new Vector2(560, 200)) < 95f) { buffing = true; finishFor = a.id; return; }
            if (pos.x > 540 && pos.x < 620 && pos.y > 260 && pos.y < 375) { steaming = true; finishFor = a.id; return; }
            return;
        }
        if (phase == CraftPointer.Up || phase == CraftPointer.Cancel) buffing = steaming = false; // what was buffed stays buffed
    }

    /// <summary>A cord (a thread no joint takes) the design still wants, and the role it goes in; null when none.</summary>
    private CraftComponentSpec CordWanted(CraftPiece a)
    {
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d == null) return null;
        foreach (CraftComponentSpec c in d.components)
        {
            if (!c.supply || c.supplyKind != CraftSupplyKind.Thread || c.optional || a.assembly.Supply(c.role) >= Math.Max(1, c.supplyCount)) continue;
            bool joined = false;
            foreach (CraftJointSpec j in d.joints) if (j.roleA == c.role || j.roleB == c.role) joined = true;
            if (!joined) return c;
        }
        return null;
    }

    private void AddCord()
    {
        CraftPiece a = Assembly;
        CraftComponentSpec cord = CordWanted(a);
        if (cord == null) { said = a == null ? "Pick the piece (left)." : "It has its cord."; return; }
        string thread = null;
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Thread && Have(s.id) != 0) { thread = s.id; break; }
        if (thread == null) { said = $"You have no thread or cord for its {cord.Name.ToLowerInvariant()}."; return; }
        string id = a.id, name = cord.Name.ToLowerInvariant(), item = ItemOf(thread);
        int n = Math.Max(1, cord.supplyCount - (int)a.assembly.Supply(cord.role));
        Send(new CraftRequest(CraftRequestKind.Attach, id) { id = cord.role, count = n, other = thread }, (ok, why) => { if (ok) SayFor(id, $"The {name} is threaded on ({item})."); });
    }

    private void InspectAndFinish()
    {
        CraftPiece a = Assembly;
        if (a == null) { said = "Pick the piece (left)."; return; }
        // what still stands between it and the inspection, before anything is sent
        var missing = CraftAssembly.Missing(Ws, a);
        missing.RemoveAll(m => m.StartsWith("Buff", StringComparison.Ordinal));
        if (missing.Count > 0) { said = $"Not yet: {string.Join(" ", missing)}"; return; }
        string id = a.id;
        float buff = Buffed(id), clean = Mathf.Clamp01(Steamed(id) / SteamFull);
        if (buff <= 0.05f && a.assembly.finish <= 0) { said = "Buff it on the wheel first."; return; }
        Send(new CraftRequest(CraftRequestKind.FinishJewel, id) { a = Mathf.Min(buff, 1f - 3f * Floor), b = clean }, (ok, why) =>
        {
            if (!ok) return; // refused: the buffing is kept for another try
            if (!string.IsNullOrEmpty(why))
            {
                // only part of the buff counted (the wheel had not had the time): the buffing is kept and the piece is not
                // finished - the next Inspect & finish sends it again, and the piece takes the brighter of the two
                SayFor(id, "The wheel has not brought it that bright yet: buff it a little longer, then Inspect & finish.");
                return;
            }
            // all of it counted: it is in the piece now
            buffed.Remove(id);
            steamed.Remove(id);
            string inspected = LastRecord(id);
            Send(new CraftRequest(CraftRequestKind.Finish, id), (done, why2) => SayFor(id, done ? $"{inspected}. Finished." : $"{inspected}."));
        });
    }

    // ------------------------------------------------------------------ the words
    private void RefreshStones()
    {
        // Stones that can be seated to (cut, on the bench), the one picked kept.
        var gs = new List<string>();
        foreach (CraftPiece q in Ws.Pieces) if (q.kind == CraftPieceKind.Gem && q.location == CraftLocation.Air && q.gem.stage >= CraftGemStage.Faceted && q.gem.girdle > 0) gs.Add(q.id);
        string key = string.Join(",", gs);
        if (key == stonesKey || stone == null) return;
        string was = StoneChosen?.id;
        stonesKey = key;
        stones.Clear();
        stones.AddRange(gs);
        var names = new List<string>();
        foreach (string id in gs) { CraftPiece q = Ws.Piece(id); names.Add($"{CraftLook.Name(Rules, q, ui.Known)} {q.gem.carats:0.00} ct, {q.gem.girdle:0.00} mm"); }
        stone.ClearOptions();
        stone.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
        int at = was != null ? stones.IndexOf(was) : -1;
        stone.SetValueWithoutNotify(Mathf.Max(0, at));
    }

    public override void Refresh()
    {
        RefreshCounts(solder, solders);
        RefreshStones();
        CraftPiece a = Assembly;
        string jk = a != null ? a.id + place + a.assembly.parts.Count : "";
        if (jk != jointsKey && joint != null)
        {
            string was = JointId;
            jointsKey = jk;
            joints.Clear();
            var names = new List<string>();
            CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
            if (d != null)
                foreach (CraftJointSpec j in d.joints)
                    if (place == Place.Solder ? j.kind == CraftJointKind.Solder : j.kind == CraftJointKind.BezelSet || j.kind == CraftJointKind.ProngSet) { joints.Add(j.id); names.Add(j.Name); }
            joint.ClearOptions();
            joint.AddOptions(names.Count > 0 ? names : new List<string> { "-" });
            int at = was != null ? joints.IndexOf(was) : -1;
            joint.SetValueWithoutNotify(Mathf.Max(0, at));
        }
        if (cordButton != null) cordButton.gameObject.SetActive(place == Place.Finish && CordWanted(a) != null);
        if (info == null) return;
        string how;
        switch (place)
        {
            case Place.Size:
            {
                CraftPiece ring = RingBeingMade();
                how = a != null && ring == null ? $"<b>Size</b>: only a ring is sized to a finger - {CraftText.A(Rules.Design(a.assembly.design)?.Name ?? "piece")} {Rules.Design(a.assembly.design)?.Name.ToLowerInvariant() ?? "piece"} fits as it is."
                    : $"<b>Size</b> the finger for a ring: click the sizer's rings to try them until one fits (the fewer tries, the truer the size). {(ring != null ? "The ring picked takes the size." : "The size waits for the next ring started.")}";
                break;
            }
            case Place.Form:
            {
                CraftPiece p = MetalPiece;
                CraftJewelPart jp = PartChosen;
                string sizeWords = jp == CraftJewelPart.Band ? $"{partSize[0]:0.0} mm inside (to the finger{(MeasuredFinger() > 0 ? $" measured: {MeasuredFinger():0.00}" : ": size it first")})"
                    : jp == CraftJewelPart.Bail ? "a bail" : jp == CraftJewelPart.Chain ? $"{partSize[4]:0} mm" : $"{partSize[PartIndex]:0.00} mm inside (to the stone's girdle{(StoneChosen != null ? $": {StoneChosen.gem.girdle:0.00}" : ": choose the stone")})";
                how = p == null ? "<b>Form</b>: pick a length of wire or bezel strip (left)."
                    : $"<b>Form</b> {CraftText.A(PartNames[PartIndex])} {PartNames[PartIndex].ToLowerInvariant()}, {sizeWords}: press and release on the metal round the rim to tap it round (each part once or twice; the TAP gauge's green bends it), then tap the seam at the top closed; Done. Only the length it needs is used.";
                break;
            }
            case Place.Seat:
            {
                CraftPiece s = SettingToSeat(out bool fitted, out string why);
                how = s == null ? $"<b>Seat</b>: {why}"
                    : $"<b>Seat</b>{(fitted ? " the fitted setting" : "")}: choose the stone (above); hold the burr in the middle, still, and let go when the gold seat lies in the green ring - wider it never sits tight, smaller it will not go in.";
                break;
            }
            case Place.Solder:
            {
                bool stoneIn = a != null && StoneIn(a) != null && !Soldered(a);
                how = a == null ? "<b>Solder</b>: pick the piece being made (start it at the Assembly area)."
                    : $"<b>Solder</b>: brush flux along the seam (drag over it) into the green, then hold the torch on the metal and let go once the solder takes (heat in the green - the better fluxed, the sooner): short of it nothing joins, past it the metal pits.{(stoneIn ? " The stone is in the piece: the heat reaches it." : "")}";
                break;
            }
            case Place.Set:
            {
                CraftJointSpec spec = a != null && JointId != null ? Rules.Design(a.assembly.design)?.Joint(JointId) : null;
                JointState js = a != null && JointId != null ? a.assembly.Joint(JointId) : null;
                string count = spec != null && spec.kind == CraftJointKind.ProngSet ? $" Pushes {js?.pushes ?? 0}/{spec.count}: every claw." : "";
                how = a == null ? "<b>Set</b>: pick the piece (left)."
                    : $"<b>Set</b>: hold and let go on the bezel (or a prong) to push it over the stone - then the opposite side, then a quarter round (the green dot). Firm, not past the red. It is set once SECURE is in its green.{count}";
                break;
            }
            default:
                how = a == null ? "<b>Finish</b>: pick the piece (left)."
                    : $"<b>Finish</b>: hold the piece on the buffing wheel (only the time on it counts), hold it in the steam to clean it, then Inspect & finish.{(CordWanted(a) != null ? $" Its {CordWanted(a).Name.ToLowerInvariant()} is still to be added." : "")}";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}
