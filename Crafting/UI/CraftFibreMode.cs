using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The fibre bench, in five places, each worked with the hands (fibre is laid on the bench a batch at a time):
/// <list type="bullet">
/// <item><b>Pick</b>: pick the burrs, seeds and shives out of the fibre one by one (click each); a click on clean fibre
/// pulls good fibre out with it. What was picked goes to the batch when you are done, leave the place or turn to another
/// batch (<i>Done picking</i> sends it at once).</item>
/// <item><b>Scour</b>: heat the bath (feed the fire: drag the fire gauge), put the fibre in, leave it to soak - stirring
/// a wool fleece in hot water felts it - lift it out, then rinse it (each click on the rinse tub is a rinse) in water near
/// the bath's heat: a hot fleece plunged into cold water felts too. Then hang it to dry.</item>
/// <item><b>Tease</b>: pull each matted lock apart (drag across it), slowly: torn fast, the fibres break.</item>
/// <item><b>Card</b>: drag the top card across the bottom one, always the same way, lightly: the teeth must reach into
/// the fibre to align it (the pressure gauge's green band), but heavy strokes roll the fibres into knots (neps).</item>
/// <item><b>Comb</b>: draw the fibre off the comb, straight and steady along the faint line (press by the tines, drag
/// right): short fibres stay behind as noil, the long ones come out straight - a top for a smooth, strong worsted.</item>
/// </list>
/// A batch in the scouring bath (or the rinse tub) is off the bench - not picked, teased, carded, combed or spun at the
/// wheel - till it is hung up to dry. The right button puts down what the hands hold (a gauge, a card, a lock, the comb):
/// it never sends anything.
/// </summary>
public sealed class CraftFibreMode : CraftMode
{
    private enum Place { Pick, Scour, Tease, Card, Comb }
    private static readonly string[] PlaceNames = { "Pick", "Scour", "Tease", "Card", "Comb" };

    private Place place = Place.Pick;
    private readonly List<Button> placeButtons = new List<Button>();
    private readonly List<RectTransform> scourRows = new List<RectTransform>(), pickRows = new List<RectTransform>(), teaseRows = new List<RectTransform>();
    private TMP_Dropdown fibre, detergent;
    private TextMeshProUGUI info;
    private readonly List<string> fibres = new List<string>(), detergents = new List<string>();
    private string said = "";

    // The bench shows one batch's burrs and locks: the batch they belong to, and the state they were drawn from (redrawn
    // when the batch changes - a routine, another pair of hands, the picks sent).
    private string workedFor = "", madeKey = "";

    private static readonly Rect Bench = new Rect(40, 40, 640, 310);
    // The strip along the top of the surface where the meters and their labels are (the work is drawn below it).
    private const float StripY = 366f;

    // Picking.
    private readonly List<Vector2> burrs = new List<Vector2>();
    private int picked, misses, burrsAtStart;

    // Scouring: the bath heats from its fire; the fleece soaks; stirring and the rinses' heat are counted.
    private static readonly Rect Bath = new Rect(60, 100, 270, 170), Rinse = new Rect(430, 100, 230, 170),
        FireGauge = new Rect(60, 30, 270, 18), RinseGauge = new Rect(430, 30, 230, 18);
    private const float RinseLow = 5f, RinseHigh = 80f;
    private float fire, water = 18f, rinseTemp = 18f, soakSum, soakN, stir, shockMax;
    private int rinses;
    private bool inBath, soaked, holdingFire, holdingRinse, stirring, stirInside;
    private string scourFor = "";
    private Vector2 lastStir;
    // One second at the bath is a minute of soaking (as the station counts it: the soak it grants is the real time the fleece had).
    private const float MinutesPerSecond = (float)CraftFibre.SoakMinutesPerSecond;

    // Teasing: each lock as far as the hands pulled it open; how much of that was already sent, and how harshly (by the opening each pull made).
    private readonly List<Vector3> locks = new List<Vector3>(); // x, y, opened (0-1, as the hands pulled it)
    private int dragLock = -1;
    private Vector2 lockFrom;
    private float lockStart, teaseSent, harshOpen, openSum;

    // Carding: the bottom card on the knee with the fibre (the batt) on its teeth; the top card in the hand.
    private static readonly Rect BottomCard = new Rect(140, 110, 440, 110);
    private const float CardTeeth = 6f, BattTop = 316f, CardHighest = 320f;
    private readonly List<Vector2> card = new List<Vector2>();
    private string cardFor = "";
    private int cardStrokes, lastDir, pendingStrokes;
    private float pressureSum, evenSum, lastStroke = -1f;

    // Combing: a pass is as true as it keeps to the faint line (its mean straying, px, against this: a quarter of it keeps three-quarters true).
    private static readonly Rect Comb = new Rect(80, 120, 40, 180);
    private const float CombTrue = 14f;
    private readonly List<Vector2> pass = new List<Vector2>();
    private float passStart, lastTrue = -1f;
    private int passes;
    private string noilFor = "";
    private double shortAtStart;

    public override string Title => "Fibre bench";
    public override CraftDiscipline Discipline => CraftDiscipline.Tailoring;
    public override bool Available(CraftStation s) => s.Has(CraftTools.FibreBench);
    public override string Hint => "Pick the dirt out, scour the grease off, tease the locks open, card or comb the fibres straight - the better prepared, the better the yarn. Right-click puts down.";

    /// <summary>The batch worked: the one picked in the list, else the first batch lying on the bench (one in the bath is not on it).</summary>
    private CraftPiece Batch
    {
        get
        {
            if (Selected != null && Selected.kind == CraftPieceKind.Fibre) return Selected;
            List<CraftPiece> any = Station != null ? Station.FibreBatches() : null;
            if (any != null)
                foreach (CraftPiece p in any)
                    if (p.id != Bathing) return p;
            return null;
        }
    }

    /// <summary>The fleece soaking or waiting to be rinsed, while there is one.</summary>
    private CraftPiece Scouring => inBath || soaked ? Ws.Piece(scourFor) : null;

    /// <summary>
    /// The batch in the scouring bath or waiting in the rinse tub (null: none): it is off the bench - not picked, teased,
    /// carded, combed or spun - till it is hung up to dry.
    /// </summary>
    public string Bathing => (inBath || soaked) && scourFor.Length > 0 ? scourFor : null;

    /// <summary>What the bench says of a batch in the bath.</summary>
    public const string InTheBath = "It is in the scouring bath: lift it out, rinse it and hang it to dry first (Scour).";

    /// <summary>The batch the bench's hands work now: <see cref="Batch"/>, unless that is in the bath.</summary>
    private CraftPiece OnBench => Batch is CraftPiece p && p.id != Bathing ? p : null;

    public override void Build(RectTransform controls)
    {
        scourRows.Clear(); pickRows.Clear(); teaseRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            placeButtons.Add(Btn(r0, PlaceNames[i], () => GoToPlace(pl), 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        fibres.Clear();
        detergents.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
        {
            if (s.kind == CraftSupplyKind.Fibre) fibres.Add(s.id);
            if (s.kind == CraftSupplyKind.Detergent) detergents.Add(s.id);
        }
        RectTransform r1 = Row(controls);
        Text(r1, "Fibre", 13f, 44f);
        fibre = CountedDrop(r1, fibres, 220f, "No fibre");
        Btn(r1, "Lay a batch on the bench", LayBatch, 200f);
        if (QuickBench)
        {
            Btn(r1, "Prepare", () =>
            {
                CraftPiece p = Batch;
                if (p == null) return;
                string id = p.id, soap = detergents.Count > 0 ? detergents[0] : "";
                Send(new CraftRequest(CraftRequestKind.CleanFibre, id) { a = 0.9f, b = 0.05f }, (ok, _) =>
                {
                    if (ok) Send(new CraftRequest(CraftRequestKind.ScourFibre, id) { a = 62f, b = 30f, c = 0.1f, d = 3f, id = soap, i0 = 3 });
                });
            }, 80f);
            Btn(r1, "Card", () => { if (Batch != null) Send(new CraftRequest(CraftRequestKind.CardFibre, Batch.id) { count = 12, a = 0.3f, b = 0.9f }); }, 60f);
        }
        RectTransform s1 = Row(controls);
        Text(s1, "Scour with", 13f, 76f);
        detergent = CountedDrop(s1, detergents, 180f, "Water alone");
        Btn(s1, "Put it in", PutIn, 90f);
        Btn(s1, "Lift it out", LiftOut, 90f);
        Btn(s1, "Hang to dry", HangToDry, 110f, UITheme.ButtonStyle.Primary);
        scourRows.Add(s1);
        RectTransform p1 = Row(controls);
        Btn(p1, "Done picking", () => DonePicking(true), 120f);
        RoutineButton(p1, CraftRoutine.PickFibre, "Pick by routine", () => OnBench?.id, 160f);
        pickRows.Add(p1);
        RectTransform t1 = Row(controls);
        Btn(t1, "Done teasing", () => DoneTeasing(true), 120f);
        RoutineButton(t1, CraftRoutine.TeaseFibre, "Tease by routine", () => OnBench?.id, 160f);
        teaseRows.Add(t1);
        info = InfoRow(controls, 56f);
        Show();
    }

    /// <summary>A place chosen with its tab (or the guide): the work of the hands in the last one goes to its batch first.</summary>
    private void GoToPlace(Place p)
    {
        if (p != place)
        {
            Finish();
            Abort();
            said = "";
        }
        place = p;
        Show();
    }

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p)) GoToPlace(p);
    }

    /// <summary>Every place is at the fibre bench.</summary>
    public override bool HasPlace(string place) => System.Enum.TryParse(place, out Place _);

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in scourRows) r.gameObject.SetActive(place == Place.Scour);
        foreach (RectTransform r in pickRows) r.gameObject.SetActive(place == Place.Pick);
        foreach (RectTransform r in teaseRows) r.gameObject.SetActive(place == Place.Tease);
    }

    /// <summary>What the hands hold is put down: nothing of it is sent (a gauge let go, a stroke or a pull not made).</summary>
    private void Abort()
    {
        card.Clear();
        pass.Clear();
        dragLock = -1;
        holdingFire = holdingRinse = stirring = false;
    }

    /// <summary>The picks, the teasing and the card strokes made so far go to the batch they were made on.</summary>
    private void Finish()
    {
        DonePicking(false);
        DoneTeasing(false);
        FlushCard();
    }

    public override void Leave()
    {
        Finish();
        Abort();
    }

    public override void SelectionChanging(string from, string to)
    {
        // The work goes to the batch it was done on (it is still there, whatever is picked next); the bench then shows the next.
        Finish();
        Abort();
    }

    public override void SelectionChanged(string to) => said = "";

    /// <summary>A fleece in the bath, or waiting to be rinsed, is lost if it is carried off.</summary>
    public override string Busy => (inBath || soaked) && ui.SelectedId == scourFor && scourFor.Length > 0 ? "scouring the fleece" : null;

    /// <summary>The fleece in the bath (or the rinse tub) waits there when the window closes: it soaks only while the hands are here.</summary>
    public override string Kept => Scouring == null ? null
        : inBath ? "Left as it is for when you come back here: the fleece in the bath - it soaks only while you are at it (working at another station starts afresh)."
        : "Left as it is for when you come back here: the fleece in the rinse tub (working at another station starts afresh).";

    public override void Tick(float dt)
    {
        // The bath warms toward what its fire gives it (and cools when the fire dies down).
        float target = 15f + 85f * fire;
        water += (target - water) * Mathf.Clamp01(dt * 0.12f);
        fire = Mathf.Max(0f, fire - dt * 0.01f);
        if (inBath)
        {
            soakSum += water * dt;
            soakN += dt;
        }
        // The hands moving something (a card, the comb, a lock, a gauge), or a tool under the pointer: drawn every frame.
        LiveDraw = ui.PointerHeld || ui.Hover.x >= 0f && (place == Place.Pick || place == Place.Card);
    }

    // ------------------------------------------------------------------ the batch on the bench
    private static string KeyOf(CraftPiece p) =>
        $"{p.id}:{Mathf.RoundToInt((float)p.fibre.dirt * 40f)}:{Mathf.RoundToInt((float)p.fibre.tangle * 10f)}";

    /// <summary>
    /// The picture of the batch worked: its burrs (from its dirt) and its matted locks (from its tangle). Work done on
    /// another batch goes to that one first; the picture is drawn again when the batch's state changes.
    /// </summary>
    private void Prepare(CraftPiece p)
    {
        if (p == null) return;
        if (p.id != workedFor)
        {
            Finish(); // to the batch it was done on
            workedFor = p.id;
            madeKey = "";
        }
        if (KeyOf(p) == madeKey) return;
        if (picked + misses > 0 || OpenedSoFar() > teaseSent)
            Finish(); // it changed under the hands (a routine, another pair of hands): what was done counts first
        madeKey = KeyOf(p);
        // a new look at what is left in it (the same look for the same batch and state)
        int n = Mathf.RoundToInt((float)p.fibre.dirt * 40f), m = Mathf.Clamp(Mathf.RoundToInt((float)p.fibre.tangle * 10f), 0, 10);
        int seed = CraftMath.Hash(madeKey);
        burrs.Clear();
        for (int i = 0; i < n; i++)
            burrs.Add(new Vector2(Bench.xMin + 30 + (float)CraftMath.Noise(seed, i, 51) * (Bench.width - 60), Bench.yMin + 30 + (float)CraftMath.Noise(seed, i, 52) * (Bench.height - 60)));
        burrsAtStart = n;
        picked = misses = 0;
        locks.Clear();
        for (int i = 0; i < m; i++)
            locks.Add(new Vector3(Bench.xMin + 60 + (i % 5) * 120 + (float)CraftMath.Noise(seed, i, 53) * 30, Bench.yMin + 90 + (i / 5) * 140, 0f));
        teaseSent = harshOpen = openSum = 0f;
        dragLock = -1;
    }

    /// <summary>How far the locks shown were pulled open, on average (0-1).</summary>
    private float OpenedSoFar()
    {
        float done = 0f;
        foreach (Vector3 k in locks) done += k.z;
        return locks.Count > 0 ? done / locks.Count : 0f;
    }

    private void LayBatch()
    {
        string id = fibres.Count > 0 ? fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)] : null;
        if (id == null) return;
        if (Have(id) == 0) { said = $"You have no {ItemOf(id)}."; return; }
        string name = Rules.Supply(id)?.Name.ToLowerInvariant() ?? "fibre";
        Send(new CraftRequest(CraftRequestKind.PutItem) { id = id, count = 1 }, (ok, _) =>
        {
            if (ok) said = $"A batch of {name} lies on the bench: it is worked here (pick it in the list to choose among batches).";
        });
    }

    // ------------------------------------------------------------------ drawing
    private Color FibreColour(CraftPiece p)
    {
        string id = Rules.Supply(p.fibre.fibre)?.id ?? "";
        Color clean = id.Contains("flax") || id.Contains("hemp") ? new Color(0.8f, 0.74f, 0.58f) : new Color(0.94f, 0.92f, 0.86f);
        return Color.Lerp(clean, new Color(0.62f, 0.55f, 0.42f), Mathf.Clamp01((float)(p.fibre.grease * 0.8 + p.fibre.dirt * 0.5)));
    }

    /// <summary>The fibre in <paramref name="r"/>: soft tufts, then the strands on them (lying straighter the better aligned) - all inside the rectangle.</summary>
    private void Fluff(CraftPixels c, CraftPiece p, Rect r, float aligned)
    {
        Color col = FibreColour(p);
        int seed = CraftMath.Hash(p.id ?? "");
        int tufts = Mathf.Clamp(Mathf.RoundToInt(r.width * r.height / 1600f), 6, 60);
        for (int i = 0; i < tufts; i++)
        {
            float rad = Mathf.Min(r.height * 0.45f, 14f + 16f * (float)CraftMath.Noise(seed, i, 70));
            float x = r.xMin + rad + (float)CraftMath.Noise(seed, i, 68) * Mathf.Max(0f, r.width - 2f * rad);
            float y = r.yMin + rad + (float)CraftMath.Noise(seed, i, 69) * Mathf.Max(0f, r.height - 2f * rad);
            c.Disc(x, y, rad, new Color(col.r * 0.92f, col.g * 0.92f, col.b * 0.9f, 0.85f));
        }
        for (int i = 0; i < 90; i++)
        {
            float x = r.xMin + (float)CraftMath.Noise(seed, i, 61) * r.width, y = r.yMin + (float)CraftMath.Noise(seed, i, 62) * r.height;
            float a = Mathf.Lerp((float)CraftMath.Noise(seed, i, 63) * Mathf.PI, 0f, aligned);
            float len = 14f + 26f * (float)CraftMath.Noise(seed, i, 64);
            // keep the strand inside the rectangle
            float dx = Mathf.Cos(a) * len, dy = Mathf.Sin(a) * len;
            float k = 1f;
            if (Mathf.Abs(dx) > 0.01f) k = Mathf.Min(k, Mathf.Min(x - r.xMin, r.xMax - x) / Mathf.Abs(dx));
            if (Mathf.Abs(dy) > 0.01f) k = Mathf.Min(k, Mathf.Min(y - r.yMin, r.yMax - y) / Mathf.Abs(dy));
            k = Mathf.Max(0f, k);
            c.Line(x - dx * k, y - dy * k, x + dx * k, y + dy * k, Color.Lerp(col, Color.white, (float)CraftMath.Noise(seed, i, 65) * 0.3f), 2f);
        }
        if (p.fibre.felted > 0.2) c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(col.r * 0.85f, col.g * 0.85f, col.b * 0.8f, Mathf.Clamp01((float)p.fibre.felted)));
        if (p.fibre.wet > 0.2) c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0.3f, 0.4f, 0.55f, 0.25f * (float)p.fibre.wet));
        for (int i = 0; i < Mathf.RoundToInt((float)p.fibre.neps * 40f); i++)
            c.Disc(r.xMin + 3f + (float)CraftMath.Noise(seed, i, 66) * (r.width - 6f), r.yMin + 3f + (float)CraftMath.Noise(seed, i, 67) * (r.height - 6f), 2.2f, col * 0.85f);
    }

    /// <summary>A labelled meter in the strip along the top: the label at <paramref name="x"/>, the bar after it.</summary>
    private static void Gauge(CraftPixels c, float x, float w, float y, string label, float value, Color fill, params (float, float, Color)[] bands)
    {
        float lw = CraftArt.LabelWidth(label) + 8f;
        CraftArt.Label(c, x, y, label, CraftArt.LabelInk);
        Meter(c, x + lw, y, w - lw, 10f, value, fill, bands);
    }

    /// <summary>
    /// How clean the batch is of the dirt it came with (0-1): what is left in it against the raw fibre's - the burrs
    /// picked and not sent yet counted as the station will count them (<see cref="CraftFibre.PickedOut"/>).
    /// </summary>
    private float Cleanness(CraftPiece p)
    {
        double raw = Rules.Supply(p.fibre.fibre)?.dirt ?? 0;
        if (raw <= 0) return 1f;
        double pending = burrsAtStart > 0 ? picked / (double)burrsAtStart : 0;
        double left = p.fibre.dirt * (1 - CraftFibre.PickedOut(pending, Ws.ToolQuality));
        return Mathf.Clamp01((float)(1 - left / raw));
    }

    public override void Draw(CraftPixels c)
    {
        // the bench works a batch that is on it: one in the bath is at the Scour place
        CraftPiece p = place == Place.Scour ? Batch : OnBench;
        Prepare(p);
        switch (place)
        {
            case Place.Pick:
                c.Rect(Bench.xMin - 10, Bench.yMin - 10, Bench.xMax + 10, Bench.yMax + 10, Wood);
                if (p == null) return;
                Fluff(c, p, Bench, (float)p.fibre.alignment);
                foreach (Vector2 b in burrs)
                {
                    c.Disc(b.x, b.y, 4f, new Color(0.3f, 0.22f, 0.12f));
                    c.Line(b.x - 5, b.y, b.x + 5, b.y, new Color(0.25f, 0.18f, 0.1f), 1f);
                }
                Gauge(c, 40f, 640f, StripY, "CLEAN", Cleanness(p), new Color(0.5f, 1f, 0.6f));
                Vector2 h = ui.Hover;
                if (Bench.Contains(h))
                {
                    // the tweezers, their tips where the pointer is
                    c.Line(h.x, h.y, h.x + 18, h.y - 30, new Color(0.75f, 0.76f, 0.8f), 2f);
                    c.Line(h.x + 3, h.y, h.x + 22, h.y - 28, new Color(0.75f, 0.76f, 0.8f), 2f);
                }
                break;
            case Place.Scour: DrawScour(c, p); break;
            case Place.Tease: DrawTease(c, p); break;
            case Place.Card: DrawCard(c, p); break;
            default: DrawComb(c, p); break;
        }
    }

    private void DrawTease(CraftPixels c, CraftPiece p)
    {
        c.Rect(Bench.xMin - 10, Bench.yMin - 10, Bench.xMax + 10, Bench.yMax + 10, Wood);
        if (p == null) return;
        // A lock opens as far as the fibre lets it (as the station counts it): felted fibre hardly opens however it is
        // pulled, and wet fibre clings.
        float give = (float)CraftFibre.OpenGive(p.fibre);
        Color col = FibreColour(p);
        for (int i = 0; i < locks.Count; i++)
        {
            Vector3 l = locks[i];
            float open = l.z * give;
            float spread = 18f + open * 50f;
            for (int k = 0; k < 10; k++)
                c.Disc(l.x + Mathf.Cos(k * 0.63f) * spread * 0.6f, l.y + Mathf.Sin(k * 0.63f) * spread * 0.4f, 12f - open * 5f, open > 0.9f ? Color.Lerp(col, Color.white, 0.2f) : col * (0.9f + 0.1f * open));
        }
        if (dragLock >= 0 && ui.Hover.x >= 0)
            c.Line(lockFrom.x, lockFrom.y, ui.Hover.x, Mathf.Min(ui.Hover.y, StripY - 8f), new Color(1f, 1f, 1f, 0.5f), 2f);
        Gauge(c, 40f, 640f, StripY, "OPENED", OpenedSoFar() * give, new Color(0.5f, 1f, 0.6f));
    }

    private void DrawScour(CraftPixels c, CraftPiece p)
    {
        bool thermometer = Station.HasInstrument(CraftInstruments.Thermometer);
        CraftPiece f = Scouring ?? p;
        CraftSupplyDefinition d = f != null ? CraftFibre.Def(Rules, f) : null;
        CraftFibre.ScourHeat(d, out double from, out double full);
        // The bath on its fire, steaming as it heats; the rinse tub beside it.
        c.Rect(Bath.xMin - 8, Bath.yMin - 30, Bath.xMax + 8, Bath.yMin, new Color(0.25f, 0.2f, 0.18f));
        if (fire > 0.05f) for (int k = 0; k < 6; k++) c.Disc(Bath.xMin + 30 + k * 42, Bath.yMin - 14, 6f + fire * 8f, new Color(1f, 0.55f + fire * 0.3f, 0.15f, 0.8f));
        c.Rect(Bath.xMin, Bath.yMin, Bath.xMax, Bath.yMax, Iron);
        c.Rect(Bath.xMin + 6, Bath.yMin + 6, Bath.xMax - 6, Bath.yMax - 24, Color.Lerp(Water, new Color(0.65f, 0.55f, 0.5f, 0.9f), Mathf.Clamp01((water - 20f) / 80f) * 0.4f));
        if (water > 45f) for (int k = 0; k < Mathf.Min(6, Mathf.RoundToInt((water - 45f) / 8f)); k++) c.Line(Bath.xMin + 30 + k * 40, Bath.yMax - 18, Bath.xMin + 40 + k * 40, Bath.yMax + 18, new Color(1f, 1f, 1f, 0.25f), 3f);
        if (water > 90f) for (int k = 0; k < 8; k++) c.Disc(Bath.xMin + 20 + k * 32, Bath.yMax - 30, 3f, new Color(1f, 1f, 1f, 0.5f));
        c.Rect(Rinse.xMin, Rinse.yMin, Rinse.xMax, Rinse.yMax, Wood);
        c.Rect(Rinse.xMin + 6, Rinse.yMin + 6, Rinse.xMax - 6, Rinse.yMax - 24, Water);
        // The fire gauge (how hard the fire is fed) and the rinse water's heat, with the band near the bath's heat that does not shock wool.
        CraftArt.Label(c, FireGauge.xMin, FireGauge.yMax + 4, "FIRE", CraftArt.LabelInk);
        Meter(c, FireGauge.x, FireGauge.y, FireGauge.width, FireGauge.height, fire, new Color(1f, 0.55f, 0.2f));
        CraftArt.Label(c, RinseGauge.xMin, RinseGauge.yMax + 4, "RINSE WATER", CraftArt.LabelInk);
        float bathAt = Mathf.InverseLerp(RinseLow, RinseHigh, LastBath), band = (float)CraftFibre.ShockFrom / (RinseHigh - RinseLow);
        bool felts = d != null && d.felts;
        if (felts && (inBath || soaked))
            Meter(c, RinseGauge.x, RinseGauge.y, RinseGauge.width, RinseGauge.height, Mathf.InverseLerp(RinseLow, RinseHigh, rinseTemp), new Color(0.6f, 0.7f, 1f),
                (bathAt - band, bathAt + band, GoodBand));
        else
            Meter(c, RinseGauge.x, RinseGauge.y, RinseGauge.width, RinseGauge.height, Mathf.InverseLerp(RinseLow, RinseHigh, rinseTemp), new Color(0.6f, 0.7f, 1f));
        if (thermometer)
        {
            // A thermometer hangs in each tub, the bath's marked where the grease starts to lift and where it all lifts.
            float t0 = Bath.yMin + 14, t1 = Bath.yMax - 30;
            c.Rect(Bath.xMax - 20, t0, Bath.xMax - 14, t1, new Color(0.9f, 0.9f, 0.9f));
            c.Rect(Bath.xMax - 26, t0 + (t1 - t0) * (float)(full / 100.0) - 1, Bath.xMax - 8, t0 + (t1 - t0) * (float)(full / 100.0) + 1, Good);
            c.Rect(Bath.xMax - 24, t0 + (t1 - t0) * (float)(from / 100.0) - 1, Bath.xMax - 10, t0 + (t1 - t0) * (float)(from / 100.0) + 1, Fair);
            c.Rect(Bath.xMax - 19, t0, Bath.xMax - 15, t0 + (t1 - t0) * Mathf.Clamp01(water / 100f), new Color(0.9f, 0.15f, 0.15f));
            c.Rect(Rinse.xMax - 20, Rinse.yMin + 14, Rinse.xMax - 14, Rinse.yMax - 30, new Color(0.9f, 0.9f, 0.9f));
            c.Rect(Rinse.xMax - 19, Rinse.yMin + 14, Rinse.xMax - 15, Rinse.yMin + 14 + (Rinse.height - 44) * Mathf.Clamp01(rinseTemp / 100f), new Color(0.9f, 0.15f, 0.15f));
            CraftArt.Label(c, FireGauge.xMax - CraftArt.LabelWidth($"BATH {water:0}°C"), FireGauge.yMax + 4, $"BATH {water:0}°C", CraftArt.LabelInk);
            CraftArt.Label(c, RinseGauge.xMax - CraftArt.LabelWidth($"{rinseTemp:0}°C"), RinseGauge.yMax + 4, $"{rinseTemp:0}°C", CraftArt.LabelInk);
        }
        if (f == null) return;
        Rect at = inBath ? new Rect(Bath.xMin + 40, Bath.yMin + 30, 170, 80) : soaked ? new Rect(Rinse.xMin + 30, Rinse.yMin + 30, 150, 80) : new Rect(350, 296, 140, 52);
        Fluff(c, f, at, (float)f.fibre.alignment);
        // How long it has soaked (two-thirds of the grease the heat can lift goes in the first band, nearly all by its end)...
        float tau = (float)CraftFibre.SoakMinutes(d), minutes = soakN * MinutesPerSecond;
        Gauge(c, Bath.xMin, Bath.width, StripY, "SOAK", Mathf.Clamp01(minutes / (3f * tau)), Mark, (2f / 3f, 1f, GoodBand));
        // ...and the soap still in it: the rinses take it out.
        bool det = DetergentId().Length > 0;
        double soapLeft = CraftFibre.SoapAfter(f.fibre.soap, (inBath || soaked) && det, rinses);
        Gauge(c, Rinse.xMin, Rinse.width, StripY, "SOAP", (float)(soapLeft / CraftFibre.SoapPerScour), new Color(0.85f, 0.9f, 1f), (0f, 0.05f, GoodBand));
    }

    private void DrawCard(CraftPixels c, CraftPiece p)
    {
        // The bottom card on the knee: its handle, its teeth, the fibre on them; the top card in the hand.
        c.Rect(BottomCard.xMin, BottomCard.yMin, BottomCard.xMax, BottomCard.yMax, Wood);
        c.Rect(BottomCard.xMin - 80, BottomCard.center.y - 10, BottomCard.xMin, BottomCard.center.y + 10, Wood);
        for (float x = BottomCard.xMin + 6; x < BottomCard.xMax; x += 8) c.Line(x, BottomCard.yMax, x + 3, BottomCard.yMax + CardTeeth, Iron, 1f);
        if (p != null) Fluff(c, p, new Rect(BottomCard.xMin + 10, BottomCard.yMax + CardTeeth, BottomCard.width - 20, BattTop - BottomCard.yMax - CardTeeth), (float)p.fibre.alignment);
        Vector2 h = card.Count > 0 ? card[card.Count - 1] : ui.Hover;
        float press = 0f;
        if (h.x >= 0)
        {
            press = Pressure(h.y);
            // the top card, its teeth down; it goes no lower than the bottom card's teeth and is drawn under the meters
            float y = Mathf.Clamp(h.y, BottomCard.yMax + CardTeeth * 2f, CardHighest);
            c.Rect(h.x - 160, y, h.x + 160, y + 40, Wood * 1.1f);
            c.Rect(h.x + 160, y + 10, h.x + 230, y + 30, Wood);
            for (float x = h.x - 154; x < h.x + 160; x += 8) c.Line(x, y, x + 3, y - CardTeeth, Iron, 1f);
        }
        // The pressure: grey the teeth hardly reach the fibre, green light and right, amber a few neps, red heavy.
        float shown = card.Count > 0 || h.x >= 0 ? press : lastStroke;
        Gauge(c, 30f, 330f, StripY, "PRESS", Mathf.Max(0f, shown), PressColour(shown),
            (0f, (float)CraftFibre.CardLight, IdleBand),
            ((float)CraftFibre.CardLight, (float)CraftFibre.CardNeps, GoodBand),
            ((float)CraftFibre.CardNeps, (float)CraftFibre.CardHard, FairBand),
            ((float)CraftFibre.CardHard, 1f, HarmBand));
        Gauge(c, 390f, 300f, StripY, "ALIGNED", p != null ? (float)p.fibre.alignment : 0f, Mark, (0.5f, 0.65f, GoodBand));
    }

    private static Color PressColour(float press) =>
        press < CraftFibre.CardLight ? new Color(0.6f, 0.85f, 1f) : press <= CraftFibre.CardNeps ? Good : press <= CraftFibre.CardHard ? Fair : Harm;

    /// <summary>How hard the top card presses with its teeth at <paramref name="y"/>: 0 just reaching the fibre on the bottom card, 1 down on its teeth.</summary>
    private static float Pressure(float y) => Mathf.Clamp01((BattTop + CardTeeth - y) / (BattTop - BottomCard.yMax - CardTeeth));

    private void DrawComb(CraftPixels c, CraftPiece p)
    {
        c.Rect(Comb.xMin - 30, Comb.yMin - 20, Comb.xMin, Comb.yMax + 20, Wood);
        for (float y = Comb.yMin; y < Comb.yMax; y += 12) c.Line(Comb.xMin, y, Comb.xMax, y, Iron, 2f);
        if (p != null)
        {
            // The fibre lashed on the comb, the noil the passes have left in it, and the pass being drawn off.
            Fluff(c, p, new Rect(Comb.xMax, Comb.yMin, 140, Comb.height), (float)p.fibre.alignment);
            if (noilFor != p.id) { noilFor = p.id; shortAtStart = p.fibre.shortFibre; }
            int noil = Mathf.Clamp(Mathf.RoundToInt((float)(shortAtStart - p.fibre.shortFibre) * 30f), 0, 6);
            for (int k = 0; k < noil; k++) c.Disc(Comb.xMin + 10, Comb.yMin + 12 + k * 28, 4f, new Color(0.7f, 0.65f, 0.55f));
        }
        for (int i = 1; i < pass.Count; i++) c.Line(pass[i - 1].x, Mathf.Min(pass[i - 1].y, StripY - 8f), pass[i].x, Mathf.Min(pass[i].y, StripY - 8f), new Color(1f, 1f, 1f, 0.5f), 3f);
        if (pass.Count > 0)
        {
            // the straight line the pass should follow
            Vector2 a = pass[0];
            c.Line(a.x, a.y, Mathf.Min(710f, a.x + 280f), a.y, new Color(1f, 1f, 1f, 0.15f), 1f);
        }
        Gauge(c, 30f, 330f, StripY, "LAST PASS", Mathf.Max(0f, lastTrue), lastTrue > 0.75f ? Good : Fair, (0.75f, 1f, GoodBand));
        Gauge(c, 390f, 300f, StripY, "ALIGNED", p != null ? (float)p.fibre.alignment : 0f, Mark, (0.85f, 1f, GoodBand));
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Cancel)
        {
            // the right button puts down what the hands hold - never sends anything
            if (dragLock >= 0 || card.Count > 0 || pass.Count > 0 || holdingFire || holdingRinse || stirring) said = "Put down.";
            Abort();
            return;
        }
        if (phase == CraftPointer.Scroll || phase == CraftPointer.Move) return;
        if (place == Place.Scour) { ScourPointer(phase, pos); return; }
        CraftPiece p = Batch;
        if (p != null && p.id == Bathing) { if (phase == CraftPointer.Down) said = InTheBath; return; } // not on the bench
        if (p == null) { if (phase == CraftPointer.Down) said = "Lay a batch of fibre on the bench first."; return; }
        Prepare(p);
        switch (place)
        {
            case Place.Pick: PickPointer(phase, pos); break;
            case Place.Tease: TeasePointer(phase, pos, p); break;
            case Place.Card: CardPointer(phase, pos, p); break;
            default: CombPointer(phase, pos, p); break;
        }
    }

    private void PickPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase != CraftPointer.Down || !Bench.Contains(pos)) return; // a click picks; its drag and release do nothing
        Vector2 at = pos + new Vector2((float)Hands.Tremble(picked + misses + 131) * 50f, (float)Hands.Tremble(picked + misses + 132) * 50f);
        for (int i = 0; i < burrs.Count; i++)
            if (Vector2.Distance(at, burrs[i]) < 7f)
            {
                burrs.RemoveAt(i);
                picked++;
                Hands.Move(0.02);
                said = burrs.Count > 0 ? $"Picked out ({picked})." : "That was the last you can see: a speck or two always hides deeper in it.";
                if (burrs.Count == 0) DonePicking(true);
                return;
            }
        misses++;
        said = "Only good fibre came away with the tweezers.";
    }

    /// <summary>What was picked goes to the batch it was picked from (<paramref name="told"/>: say so).</summary>
    private void DonePicking(bool told)
    {
        CraftPiece p = Ws?.Piece(workedFor);
        if (picked + misses == 0)
        {
            if (told) said = "Pick some burrs out first.";
            return;
        }
        // the share of the burrs that could be seen that were picked (a miss picks nothing: it only pulls good fibre out)
        int got = picked;
        float share = burrsAtStart > 0 ? picked / (float)burrsAtStart : 0f, lost = Mathf.Clamp01(misses / Mathf.Max(6f, picked + misses) * 1.5f);
        picked = misses = 0; // taken out before it is sent: it never goes twice
        burrsAtStart = burrs.Count; // what is left is all there is to pick now
        if (p == null) { said = "The batch picked over is gone."; return; }
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.CleanFibre, id) { a = share, b = lost }, (ok, _) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(id);
            if (told || place == Place.Pick)
                said = got == 0 ? "Nothing picked: only good fibre came away with the tweezers."
                    : q != null && q.fibre.cleaned ? "Picked clean - a speck or two always stays in it."
                    : $"Picked over: {Mathf.RoundToInt(share * 100)}% of the burrs you could see are out.";
            madeKey = ""; // drawn again from what is left in it
        });
    }

    private void ScourPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            if (FireGauge.Contains(pos)) { holdingFire = true; fire = Mathf.Clamp01((pos.x - FireGauge.xMin) / FireGauge.width); return; }
            if (RinseGauge.Contains(pos)) { holdingRinse = true; rinseTemp = Mathf.Lerp(RinseLow, RinseHigh, (pos.x - RinseGauge.xMin) / RinseGauge.width); return; }
            if (inBath && Bath.Contains(pos)) { stirring = stirInside = true; lastStir = pos; return; }
            if (soaked && Rinse.Contains(pos)) RinseOnce();
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (holdingFire) fire = Mathf.Clamp01((pos.x - FireGauge.xMin) / FireGauge.width);
            if (holdingRinse) rinseTemp = Mathf.Lerp(RinseLow, RinseHigh, Mathf.Clamp01((pos.x - RinseGauge.xMin) / RinseGauge.width));
            if (stirring)
            {
                // stirring only while the hand moves in the bath: from where it was, to where it is
                bool inside = inBath && Bath.Contains(pos);
                if (inside && stirInside) stir += Vector2.Distance(pos, lastStir) / 2500f;
                lastStir = pos;
                stirInside = inside;
            }
            return;
        }
        if (phase == CraftPointer.Up) holdingFire = holdingRinse = stirring = false;
    }

    private float LastBath => soakN > 0f ? soakSum / soakN : water;

    private string DetergentId()
    {
        if (detergents.Count == 0 || detergent == null) return "";
        string id = detergents[Mathf.Clamp(detergent.value, 0, detergents.Count - 1)];
        return Have(id) != 0 ? id : "";
    }

    private void RinseOnce()
    {
        CraftPiece f = Scouring;
        if (f == null) { ScourGone(); return; }
        rinses++;
        float shock = Mathf.Abs(rinseTemp - LastBath);
        shockMax = Mathf.Max(shockMax, shock);
        bool felts = CraftFibre.Def(Rules, f)?.felts ?? false;
        Hands.Move(0.03);
        said = felts && shock > CraftFibre.ShockFelts ? $"Plunged from the {LastBath:0} °C bath into {rinseTemp:0} °C water: wool felts like that. Rinse near the bath's heat (the green band)."
            : felts && shock > CraftFibre.ShockFrom ? $"Rinse {rinses}: the water is far from the bath's heat - the wool shrinks a little."
            : $"Rinse {rinses}.";
    }

    private void ScourGone()
    {
        inBath = soaked = stirring = false;
        soakN = soakSum = stir = shockMax = 0f;
        rinses = 0;
        scourFor = "";
        said = "The fleece in the bath is gone.";
    }

    private void PutIn()
    {
        CraftPiece p = Batch;
        if (p == null) { said = "Lay a batch of fibre on the bench first."; return; }
        if (Scouring != null) { said = "A fleece is in the bath already: lift it out, rinse it and hang it to dry first."; return; }
        scourFor = p.id;
        inBath = true;
        soaked = false;
        soakSum = soakN = stir = shockMax = 0f;
        rinses = 0;
        CraftFibre.ScourHeat(CraftFibre.Def(Rules, p), out double from, out _);
        string soap = DetergentId().Length == 0 ? $" You have no {(detergents.Count > 0 ? ItemOf(detergents[Mathf.Clamp(detergent.value, 0, detergents.Count - 1)]) : "soap")}: water alone lifts little grease." : "";
        said = (water < from ? "Into a cold bath: the grease will not lift. Feed the fire." : "Into the bath. Leave it still to soak.") + soap;
    }

    private void LiftOut()
    {
        if (!inBath) { said = soaked ? "It is out: rinse it, then hang it to dry." : "Put a fleece in the bath first."; return; }
        if (Scouring == null) { ScourGone(); return; }
        inBath = false;
        soaked = true;
        stirring = false;
        said = $"Lifted out after {soakN * MinutesPerSecond:0} minutes. Now rinse it: set the rinse water near the bath's heat (the green band), click the tub for each rinse.";
    }

    private void HangToDry()
    {
        CraftPiece p = Scouring;
        if (!soaked) { said = inBath ? "Lift it out of the bath and rinse it first." : "Put it in the bath and lift it out first."; return; }
        if (p == null) { ScourGone(); return; }
        float minutes = soakN * MinutesPerSecond;
        string det = DetergentId(), id = p.id;
        var r = new CraftRequest(CraftRequestKind.ScourFibre, id) { a = LastBath, b = minutes, c = Mathf.Clamp01(stir), d = rinses > 0 ? shockMax : 0f, id = det, i0 = rinses };
        // the bath is emptied as it goes: sent once
        inBath = soaked = stirring = false;
        float keepN = soakN, keepSum = soakSum, keepStir = stir, keepShock = shockMax;
        int keepRinses = rinses;
        soakN = soakSum = stir = shockMax = 0f;
        rinses = 0;
        Send(r, (ok, why) =>
        {
            if (ok)
            {
                CraftPiece q = Ws.Piece(id);
                said = (LastRecord(id) is string rec && rec.Length > 0 ? rec + ". " : "") + (q != null && q.fibre.felted > 0.3 ? "" : string.IsNullOrEmpty(det) ? "Water alone leaves the grease in it. " : "") + "Hung up to dry.";
                madeKey = "";
                return;
            }
            if (why != CraftAnswerWaits.NoAnswer && Ws.Piece(id) != null && scourFor == id)
            {
                // refused: it is still dripping in the rinse tub - put right what was wrong and hang it again
                soaked = true;
                soakN = keepN; soakSum = keepSum; stir = keepStir; shockMax = keepShock; rinses = keepRinses;
            }
        });
    }

    private void TeasePointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (phase == CraftPointer.Down)
        {
            for (int i = 0; i < locks.Count; i++)
                if (locks[i].z < 0.95f && Vector2.Distance(pos, new Vector2(locks[i].x, locks[i].y)) < 40f) { dragLock = i; lockFrom = pos; lockStart = ui.PointerTime; return; }
            return;
        }
        if (phase != CraftPointer.Up || dragLock < 0 || dragLock >= locks.Count) { if (phase == CraftPointer.Up) dragLock = -1; return; }
        float dist = Vector2.Distance(pos, lockFrom), speed = dist / Mathf.Max(0.05f, ui.PointerTime - lockStart);
        Vector3 l = locks[dragLock];
        float harsh = Mathf.Clamp01((speed - 250f) / 600f);
        // How far the hands pulled it apart: the station knows how much felted fibre gives (it is not counted twice here).
        float opened = Mathf.Clamp01(l.z + Mathf.Clamp01(dist / 120f)) - l.z;
        l.z += opened;
        locks[dragLock] = l;
        harshOpen += harsh * opened;
        openSum += opened;
        bool felted = p.fibre.felted > 0.3, wet = p.fibre.wet > 0.4;
        said = harsh > 0.5f ? "Torn apart fast: you hear fibres snap."
            : wet ? "Wet, it clings: it opens only part of the way - let it dry before teasing it."
            : l.z >= 0.95f ? (felted ? "Pulled as far as it goes: felted fibre hardly opens." : "Teased open.") : "Teased a little looser.";
        dragLock = -1;
        Hands.Move(0.05);
        bool all = true;
        foreach (Vector3 k in locks) all &= k.z >= 0.95f;
        if (all) DoneTeasing(true);
    }

    /// <summary>The teasing done goes to the batch it was done on (<paramref name="told"/>: say so).</summary>
    private void DoneTeasing(bool told)
    {
        CraftPiece p = Ws?.Piece(workedFor);
        float done = OpenedSoFar() - teaseSent; // what was opened since the last teasing went
        if (locks.Count == 0 || done < 0.02f)
        {
            if (told) said = locks.Count == 0 ? "There are no matted locks left to tease." : "Pull a lock open first.";
            return;
        }
        float harsh = openSum > 0f ? harshOpen / openSum : 0f; // how harshly, by the opening each pull made
        teaseSent = OpenedSoFar(); // sent: never twice (the locks stay as they look until the batch is drawn again)
        harshOpen = openSum = 0f;
        if (p == null) { said = "The batch being teased is gone."; return; }
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.OpenFibre, id) { a = done, b = harsh }, (ok, _) =>
        {
            if (!ok) return;
            CraftPiece q = Ws.Piece(id);
            if (told || place == Place.Tease) said = q != null && q.fibre.opened ? "The locks are open." : "Partly teased: some locks are still matted.";
            madeKey = ""; // drawn again from its tangle
        });
    }

    private void CardPointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (phase == CraftPointer.Down)
        {
            if (cardFor != p.id)
            {
                // another batch on the cards: the strokes made go to the last one, and the count starts again
                FlushCard();
                cardFor = p.id;
                cardStrokes = 0;
                lastDir = 0;
            }
            if (p.fibre.wet > 0.5) { said = "It is still wet: let it dry before it is carded."; return; }
            card.Clear();
            card.Add(pos);
            return;
        }
        if (card.Count == 0) return;
        if (phase == CraftPointer.Drag) { card.Add(pos + new Vector2(0f, (float)Hands.Tremble(card.Count + 141) * 40f)); return; }
        if (phase != CraftPointer.Up) return;
        float dx = card[card.Count - 1].x - card[0].x, press = 0f;
        foreach (Vector2 k in card) press += Pressure(k.y);
        press /= card.Count;
        card.Clear();
        if (Mathf.Abs(dx) < 80f) { said = "Draw the top card right across the bottom one."; return; }
        int dir = dx > 0 ? 1 : -1;
        float even = (lastDir == 0 || dir == lastDir ? 1f : 0.3f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
        lastDir = dir;
        lastStroke = press;
        pressureSum += press;
        evenSum += even;
        pendingStrokes++;
        cardStrokes++;
        Hands.Move(0.08);
        said = press > CraftFibre.CardHard ? "Pressed hard: the teeth roll the fibre into knots and break it."
            : press > CraftFibre.CardNeps ? "A little hard: a few knots roll up between the cards."
            : press < CraftFibre.CardTouch ? "The card passed over the fibre without touching it."
            : press < CraftFibre.CardLight ? "Too light: the teeth hardly reach into the fibre."
            : even < 0.5f ? "Stroked the other way: it undoes some of the aligning - keep to one direction." : "A light, even stroke.";
        if (pendingStrokes >= 4) FlushCard();
    }

    /// <summary>The card strokes made go to the batch they were made on.</summary>
    private void FlushCard()
    {
        int n = pendingStrokes;
        float press = n > 0 ? pressureSum / n : 0f, even = n > 0 ? evenSum / n : 0f;
        pendingStrokes = 0; // taken out before they are sent: never twice
        pressureSum = evenSum = 0f;
        CraftPiece p = Ws?.Piece(cardFor);
        if (n == 0 || p == null) return;
        Send(new CraftRequest(CraftRequestKind.CardFibre, p.id) { count = n, a = press, b = even });
    }

    private void CombPointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        if (phase == CraftPointer.Down)
        {
            // by the tines: just right of the comb, within its height
            if (pos.x < Comb.xMax + 150f && pos.x > Comb.xMax - 10f && pos.y > Comb.yMin - 20f && pos.y < Comb.yMax + 20f)
            {
                CraftSupplyDefinition d = CraftFibre.Def(Rules, p);
                if (d != null && d.staple < 40) { said = $"{d.Name} is too short in the staple to comb: card it."; return; }
                if (p.fibre.wet > 0.5) { said = "It is still wet: let it dry before it is combed."; return; }
                pass.Clear();
                pass.Add(pos);
                passStart = ui.PointerTime;
            }
            return;
        }
        if (pass.Count == 0) return;
        if (phase == CraftPointer.Drag) { pass.Add(pos); return; }
        if (phase != CraftPointer.Up) return;
        Vector2 a = pass[0], b = pass[pass.Count - 1];
        float dx = b.x - a.x, seconds = Mathf.Max(0.05f, ui.PointerTime - passStart);
        // How far the hand strayed from the faint line - straight off the comb, level with where it took hold - at every
        // point of the pass (not how many points it had), and the hands' own tremble: one smooth sway over the pass.
        float sway = (float)Hands.Tremble(passes + 151) * 60f, sum = 0f, len = Mathf.Max(1f, dx);
        foreach (Vector2 q in pass)
        {
            float t = Mathf.Clamp01((q.x - a.x) / len);
            float off = q.y - a.y + sway * Mathf.Sin(t * Mathf.PI);
            sum += off * off;
        }
        float rms = Mathf.Sqrt(sum / pass.Count), speed = dx / seconds;
        pass.Clear();
        if (dx < 150f) { said = "Draw it right off the comb."; return; }
        float trueness = Mathf.Clamp01(1f - rms / CombTrue) * Mathf.Clamp01(1f - Mathf.Max(0f, speed - 600f) / 800f);
        lastTrue = trueness;
        passes++;
        Hands.Move(0.1);
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.CombFibre, id) { count = 1, a = trueness }, (ok, _) =>
        {
            if (!ok) return;
            said = trueness > 0.75f ? "Drawn off straight: the short fibres stay in the comb."
                : speed > 600f ? "Snatched off too fast: it drags the short fibres along."
                : "A crooked pass: it drags the short fibres along.";
        });
    }

    public override void Refresh()
    {
        RefreshCounts(fibre, fibres);
        NoteBatches(fibre, fibres);
        RefreshCounts(detergent, detergents);
        if (info == null) return;
        if ((inBath || soaked) && Ws.Piece(scourFor) == null) ScourGone();
        CraftPiece p = Batch;
        string how;
        if (p == null && place != Place.Scour) how = "<b>Fibre bench</b>: lay a batch of fibre on the bench (above); the bench works the batch picked in the list, else the first one lying there.";
        else if (place != Place.Scour && p.id == Bathing) how = InTheBath;
        else
            switch (place)
            {
                case Place.Pick:
                    how = $"<b>Pick</b>: click each burr and seed to pick it out ({picked} picked, {burrs.Count} left; the gauge: how clean it is); a click on clean fibre pulls good fibre out with it. Done picking (or leaving) gives the batch what was picked.";
                    break;
                case Place.Scour:
                    how = ScourHow(Scouring ?? p);
                    break;
                case Place.Tease:
                    how = locks.Count == 0 ? "<b>Tease</b>: it is open - no matted locks left. Card it, or comb a long-stapled fibre." : "<b>Tease</b>: drag across each matted lock to pull it open - slowly, or the fibres break. Felted fibre hardly opens, and wet fibre only part way: let it dry first.";
                    break;
                case Place.Card:
                {
                    int strokes = p.id == cardFor ? cardStrokes : 0;
                    how = p.fibre.wet > 0.5 ? "It is still wet: let it dry before it is carded."
                        : $"<b>Card</b>: drag the top card across the bottom one, always the same way, its teeth just into the fibre: the green band of the gauge (grey: too light to align anything, amber and red: knots and broken fibre). {strokes} stroke{(strokes == 1 ? "" : "s")} on this batch.";
                    break;
                }
                default:
                    CraftSupplyDefinition d = Rules.Supply(p.fibre.fibre);
                    how = d != null && d.staple < 40 ? $"{d.Name} is too short to comb: card it."
                        : "<b>Comb</b>: press by the comb's tines and draw the fibre off to the right, keeping to the faint line level with where you took hold - steady, not too fast. Each pass leaves the short fibre (noil) behind; the left gauge is how true the last pass was.";
                    break;
            }
        // what just happened first, then how the place is worked
        info.text = (said + " " + how).Trim();
    }

    private string ScourHow(CraftPiece f)
    {
        CraftSupplyDefinition d = f != null ? CraftFibre.Def(Rules, f) : null;
        CraftFibre.ScourHeat(d, out double from, out double full);
        bool felts = d != null && d.felts;
        string heat = Station.HasInstrument(CraftInstruments.Thermometer) ? $"{water:0} °C" : water > 90 ? "simmering" : water > 55 ? "steaming hot" : water > 35 ? "warm" : "cold";
        string want = $"the grease starts to lift at {from:0} °C and lifts fully from about {full:0} °C{(felts ? " - leave wool still: stirred hot it felts" : "")}";
        if (inBath)
            return $"<b>Soaking</b> ({soakN * MinutesPerSecond:0} min, the bath {heat}): {want}. Lift it out when the soak gauge reaches its green band.";
        if (soaked)
            return $"<b>Rinse</b> it: set the rinse water near the bath's {LastBath:0} °C{(felts ? " (the green band: further off, hot wool shocked cold felts)" : "")} and click the tub for each rinse - the soap gauge empties - then hang it to dry. {rinses} rinse{(rinses == 1 ? "" : "s")}.";
        return $"<b>Scour</b>: drag the orange gauge to feed the fire - the bath is {heat}; {want}. Put it in, let it soak, lift it out, rinse it, hang it to dry.";
    }
}
