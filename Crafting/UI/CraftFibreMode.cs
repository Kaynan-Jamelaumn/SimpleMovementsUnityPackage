using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The fibre bench, in five places, each worked with the hands (fibre is laid on the bench a batch at a time):
/// <list type="bullet">
/// <item><b>Pick</b>: pick the burrs, seeds and shives out of the fibre one by one (click each); a click on clean fibre
/// pulls good fibre out with it. Pick as much as you have patience for, then <i>Done picking</i>.</item>
/// <item><b>Scour</b>: heat the bath (feed the fire: drag the fire gauge), put the fibre in, leave it to soak - stirring
/// a wool fleece in hot water felts it - lift it out, then rinse it (each click on the rinse tub is a rinse) in water near
/// the bath's heat: a hot fleece plunged into cold water felts too. Then hang it to dry.</item>
/// <item><b>Tease</b>: pull each matted lock apart (drag across it), slowly: torn fast, the fibres break.</item>
/// <item><b>Card</b>: drag the top card across the bottom one, always the same way, lightly (the pressure gauge): heavy
/// strokes roll the fibres into knots (neps).</item>
/// <item><b>Comb</b>: draw the fibre off the comb, straight and steady (drag right from the tines): short fibres stay
/// behind as noil, the long ones come out straight - a top for a smooth, strong worsted.</item>
/// </list>
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
    private string said = "", madeFor = "";

    private readonly Rect bench = new Rect(40, 40, 640, 330);

    // Picking.
    private readonly List<Vector2> burrs = new List<Vector2>();
    private int picked, misses, burrsAtStart;

    // Scouring: the bath heats from its fire; the fleece soaks; stirring and the rinse's heat are counted.
    private Rect bath = new Rect(60, 90, 280, 200), rinse = new Rect(420, 90, 240, 200), fireGauge = new Rect(60, 40, 280, 22), rinseGauge = new Rect(420, 40, 240, 22);
    private float fire, water = 18f, rinseTemp = 18f, soakStart = -1f, soakSum, soakN, stir, rinses;
    private bool inBath, soaked, holdingFire, holdingRinse;
    private Vector2 lastStir;
    // One second at the bath is a minute of soaking.
    private const float MinutesPerSecond = 1f;

    // Teasing.
    private readonly List<Vector3> locks = new List<Vector3>(); // x, y, opened (0-1)
    private int dragLock = -1;
    private Vector2 lockFrom;
    private float lockStart, harshSum;
    private int locksDone;

    // Carding and combing.
    private readonly List<Vector2> card = new List<Vector2>();
    private float cardStart;
    private int cardStrokes, lastDir;
    private float pressureSum, evenSum;
    private int pendingStrokes;
    private Rect bottomCard = new Rect(140, 120, 440, 120), comb = new Rect(80, 120, 40, 180);
    private readonly List<Vector2> pass = new List<Vector2>();
    private float passStart;

    public override string Title => "Fibre bench";
    public override CraftDiscipline Discipline => CraftDiscipline.Tailoring;
    public override bool Available(CraftStation s) => s.Has(CraftTools.FibreBench);
    public override string Hint => "Pick the dirt out, scour the grease off, tease the locks open, card or comb the fibres straight - the better prepared, the better the yarn.";

    /// <summary>The batch worked: the one picked in the list, else the first batch lying on the bench.</summary>
    private CraftPiece Batch
    {
        get
        {
            if (Selected != null && Selected.kind == CraftPieceKind.Fibre) return Selected;
            List<CraftPiece> any = Station != null ? Station.FibreBatches() : null;
            return any != null && any.Count > 0 ? any[0] : null;
        }
    }

    public override void Build(RectTransform controls)
    {
        scourRows.Clear(); pickRows.Clear(); teaseRows.Clear();
        RectTransform r0 = Row(controls, 30f, 4f);
        placeButtons.Clear();
        for (int i = 0; i < PlaceNames.Length; i++)
        {
            Place pl = (Place)i;
            placeButtons.Add(Btn(r0, PlaceNames[i], () => { place = pl; Reset(); Show(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
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
        fibre = CountedDrop(r1, fibres, 200f, "No fibre");
        Btn(r1, "Lay a batch on the bench", () =>
        {
            string id = fibres.Count > 0 ? fibres[Mathf.Clamp(fibre.value, 0, fibres.Count - 1)] : null;
            if (id == null) return;
            if (Have(id) == 0) { said = $"You have no {ItemOf(id)}."; return; }
            if (Send(new CraftRequest(CraftRequestKind.PutItem) { id = id, count = 1 })) said = $"A batch of {Rules.Supply(id)?.Name.ToLowerInvariant()} on the bench: pick it (left) to work it.";
        }, 190f);
        if (QuickBench)
        {
            Btn(r1, "Prepare", () =>
            {
                CraftPiece p = Batch;
                if (p == null) return;
                Send(new CraftRequest(CraftRequestKind.CleanFibre, p.id) { a = 0.9f, b = 0.05f });
                Send(new CraftRequest(CraftRequestKind.ScourFibre, p.id) { a = 62f, b = 30f, c = 0.1f, d = 3f, id = detergents.Count > 0 ? detergents[0] : "", i0 = 3 });
            }, 80f);
            Btn(r1, "Card", () => { if (Batch != null) Send(new CraftRequest(CraftRequestKind.CardFibre, Batch.id) { count = 12, a = 0.3f, b = 0.9f }); }, 60f);
        }
        RectTransform s1 = Row(controls);
        Text(s1, "Scour with", 13f, 76f);
        detergent = CountedDrop(s1, detergents, 180f, "Water alone");
        Btn(s1, "Put it in", PutIn, 90f);
        Btn(s1, "Lift it out", LiftOut, 90f);
        Btn(s1, "Hang to dry", HangToDry, 100f, UITheme.ButtonStyle.Primary);
        scourRows.Add(s1);
        RectTransform p1 = Row(controls);
        Btn(p1, "Done picking", DonePicking, 120f);
        RoutineButton(p1, CraftRoutine.PickFibre, "Pick by routine", () => Batch?.id, 160f);
        pickRows.Add(p1);
        RectTransform t1 = Row(controls);
        Btn(t1, "Done teasing", DoneTeasing, 120f);
        RoutineButton(t1, CraftRoutine.TeaseFibre, "Tease by routine", () => Batch?.id, 160f);
        teaseRows.Add(t1);
        info = InfoRow(controls, 44f);
        Show();
    }

    public override void GoTo(string where)
    {
        if (System.Enum.TryParse(where, out Place p)) { place = p; Show(); }
    }

    private void Show()
    {
        for (int i = 0; i < placeButtons.Count; i++) UITheme.SetSelected(placeButtons[i], (Place)i == place);
        foreach (RectTransform r in scourRows) r.gameObject.SetActive(place == Place.Scour);
        foreach (RectTransform r in pickRows) r.gameObject.SetActive(place == Place.Pick);
        foreach (RectTransform r in teaseRows) r.gameObject.SetActive(place == Place.Tease);
    }

    private void Reset()
    {
        madeFor = "";
        card.Clear();
        pass.Clear();
        dragLock = -1;
        holdingFire = holdingRinse = false;
    }

    public override void Leave()
    {
        FlushCard();
        Reset();
    }

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
    }

    /// <summary>The picture of a batch rebuilt when another batch is picked: its burrs, its locks.</summary>
    private void Prepare(CraftPiece p)
    {
        if (p == null || madeFor == p.id + place) return;
        madeFor = p.id + place;
        int seed = CraftMath.Hash(p.id ?? "");
        burrs.Clear();
        int n = Mathf.RoundToInt((float)p.fibre.dirt * 40f);
        for (int i = 0; i < n; i++)
            burrs.Add(new Vector2(bench.xMin + 30 + (float)CraftMath.Noise(seed, i, 51) * (bench.width - 60), bench.yMin + 30 + (float)CraftMath.Noise(seed, i, 52) * (bench.height - 60)));
        burrsAtStart = n;
        picked = misses = 0;
        locks.Clear();
        int m = Mathf.Clamp(Mathf.RoundToInt((float)p.fibre.tangle * 10f), 1, 10);
        for (int i = 0; i < m; i++)
            locks.Add(new Vector3(bench.xMin + 60 + (i % 5) * 120 + (float)CraftMath.Noise(seed, i, 53) * 30, bench.yMin + 90 + (i / 5) * 140, 0f));
        locksDone = 0;
        harshSum = 0f;
    }

    // ------------------------------------------------------------------ drawing
    private Color FibreColour(CraftPiece p)
    {
        Color clean = Rules.Supply(p.fibre.fibre)?.id.Contains("flax") == true || Rules.Supply(p.fibre.fibre)?.id.Contains("hemp") == true ? new Color(0.8f, 0.74f, 0.58f) : new Color(0.94f, 0.92f, 0.86f);
        return Color.Lerp(clean, new Color(0.62f, 0.55f, 0.42f), Mathf.Clamp01((float)(p.fibre.grease * 0.8 + p.fibre.dirt * 0.5)));
    }

    private void Fluff(CraftPixels c, CraftPiece p, Rect r, float aligned)
    {
        Color col = FibreColour(p);
        int seed = CraftMath.Hash(p.id ?? "");
        // The mass of the fibre first - soft, overlapping tufts - then the strands on it.
        int tufts = Mathf.Clamp(Mathf.RoundToInt(r.width * r.height / 1600f), 6, 60);
        for (int i = 0; i < tufts; i++)
        {
            float x = r.xMin + 16f + (float)CraftMath.Noise(seed, i, 68) * (r.width - 32f), y = r.yMin + 14f + (float)CraftMath.Noise(seed, i, 69) * (r.height - 28f);
            float rad = Mathf.Min(r.height * 0.45f, 14f + 16f * (float)CraftMath.Noise(seed, i, 70));
            c.Disc(x, y, rad, new Color(col.r * 0.92f, col.g * 0.92f, col.b * 0.9f, 0.85f));
        }
        for (int i = 0; i < 90; i++)
        {
            float x = r.xMin + (float)CraftMath.Noise(seed, i, 61) * r.width, y = r.yMin + (float)CraftMath.Noise(seed, i, 62) * r.height;
            float a = Mathf.Lerp((float)CraftMath.Noise(seed, i, 63) * Mathf.PI, 0f, aligned);
            float len = 14f + 26f * (float)CraftMath.Noise(seed, i, 64);
            c.Line(x - Mathf.Cos(a) * len, y - Mathf.Sin(a) * len, x + Mathf.Cos(a) * len, y + Mathf.Sin(a) * len, Color.Lerp(col, Color.white, (float)CraftMath.Noise(seed, i, 65) * 0.3f), 2f);
        }
        if (p.fibre.felted > 0.2) c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(col.r * 0.85f, col.g * 0.85f, col.b * 0.8f, Mathf.Clamp01((float)p.fibre.felted)));
        if (p.fibre.wet > 0.2) c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0.3f, 0.4f, 0.55f, 0.25f * (float)p.fibre.wet));
        for (int i = 0; i < Mathf.RoundToInt((float)p.fibre.neps * 40f); i++)
            c.Disc(r.xMin + (float)CraftMath.Noise(seed, i, 66) * r.width, r.yMin + (float)CraftMath.Noise(seed, i, 67) * r.height, 2.2f, col * 0.85f);
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Batch;
        Prepare(p);
        switch (place)
        {
            case Place.Pick:
                c.Rect(bench.xMin - 10, bench.yMin - 10, bench.xMax + 10, bench.yMax + 10, Wood);
                if (p == null) return;
                Fluff(c, p, bench, (float)p.fibre.alignment);
                foreach (Vector2 b in burrs)
                {
                    c.Disc(b.x, b.y, 4f, new Color(0.3f, 0.22f, 0.12f));
                    c.Line(b.x - 5, b.y, b.x + 5, b.y, new Color(0.25f, 0.18f, 0.1f), 1f);
                }
                Meter(c, bench.xMin, bench.yMax + 14, bench.width, 6, burrsAtStart > 0 ? picked / (float)burrsAtStart : 1f, new Color(0.5f, 1f, 0.6f));
                if (ui.Hover.x >= 0) { c.Line(ui.Hover.x, ui.Hover.y, ui.Hover.x + 18, ui.Hover.y + 30, new Color(0.75f, 0.76f, 0.8f), 2f); c.Line(ui.Hover.x + 3, ui.Hover.y, ui.Hover.x + 22, ui.Hover.y + 28, new Color(0.75f, 0.76f, 0.8f), 2f); }
                break;
            case Place.Scour: DrawScour(c, p); break;
            case Place.Tease:
                c.Rect(bench.xMin - 10, bench.yMin - 10, bench.xMax + 10, bench.yMax + 10, Wood);
                if (p == null) return;
                for (int i = 0; i < locks.Count; i++)
                {
                    Vector3 l = locks[i];
                    float spread = 18f + l.z * 50f;
                    Color col = FibreColour(p);
                    for (int k = 0; k < 10; k++)
                        c.Disc(l.x + Mathf.Cos(k * 0.63f) * spread * 0.6f, l.y + Mathf.Sin(k * 0.63f) * spread * 0.4f, 12f - l.z * 5f, l.z > 0.9f ? Color.Lerp(col, Color.white, 0.2f) : col * (0.9f + 0.1f * l.z));
                    if (i == dragLock) c.Line(lockFrom.x, lockFrom.y, ui.Hover.x, ui.Hover.y, new Color(1f, 1f, 1f, 0.5f), 2f);
                }
                break;
            case Place.Card: DrawCard(c, p); break;
            default: DrawComb(c, p); break;
        }
    }

    private void DrawScour(CraftPixels c, CraftPiece p)
    {
        bool thermometer = Station.HasInstrument(CraftInstruments.Thermometer);
        // The bath on its fire, steaming as it heats; the rinse tub beside it.
        c.Rect(bath.xMin - 8, bath.yMin - 30, bath.xMax + 8, bath.yMin, new Color(0.25f, 0.2f, 0.18f));
        if (fire > 0.05f) for (int k = 0; k < 6; k++) c.Disc(bath.xMin + 30 + k * 45, bath.yMin - 14, 6f + fire * 8f, new Color(1f, 0.55f + fire * 0.3f, 0.15f, 0.8f));
        c.Rect(bath.xMin, bath.yMin, bath.xMax, bath.yMax, Iron);
        c.Rect(bath.xMin + 6, bath.yMin + 6, bath.xMax - 6, bath.yMax - 30, Color.Lerp(Water, new Color(0.65f, 0.55f, 0.5f, 0.9f), Mathf.Clamp01((water - 20f) / 80f) * 0.4f));
        if (water > 45f) for (int k = 0; k < Mathf.RoundToInt((water - 45f) / 8f); k++) c.Line(bath.xMin + 30 + k * 40, bath.yMax - 20, bath.xMin + 40 + k * 40, bath.yMax + 20, new Color(1f, 1f, 1f, 0.25f), 3f);
        if (water > 90f) for (int k = 0; k < 8; k++) c.Disc(bath.xMin + 20 + k * 32, bath.yMax - 34, 3f, new Color(1f, 1f, 1f, 0.5f));
        c.Rect(rinse.xMin, rinse.yMin, rinse.xMax, rinse.yMax, Wood);
        c.Rect(rinse.xMin + 6, rinse.yMin + 6, rinse.xMax - 6, rinse.yMax - 30, Water);
        Meter(c, fireGauge.x, fireGauge.y, fireGauge.width, fireGauge.height, fire, new Color(1f, 0.55f, 0.2f));
        Meter(c, rinseGauge.x, rinseGauge.y, rinseGauge.width, rinseGauge.height, Mathf.InverseLerp(5f, 80f, rinseTemp), new Color(0.6f, 0.7f, 1f));
        if (thermometer)
        {
            // A thermometer hangs in each tub.
            c.Rect(bath.xMax - 20, bath.yMin + 20, bath.xMax - 14, bath.yMax - 10, new Color(0.9f, 0.9f, 0.9f));
            c.Rect(bath.xMax - 19, bath.yMin + 20, bath.xMax - 15, bath.yMin + 20 + (bath.height - 30) * Mathf.Clamp01(water / 100f), new Color(0.9f, 0.15f, 0.15f));
            c.Rect(rinse.xMax - 20, rinse.yMin + 20, rinse.xMax - 14, rinse.yMax - 10, new Color(0.9f, 0.9f, 0.9f));
            c.Rect(rinse.xMax - 19, rinse.yMin + 20, rinse.xMax - 15, rinse.yMin + 20 + (rinse.height - 30) * Mathf.Clamp01(rinseTemp / 100f), new Color(0.9f, 0.15f, 0.15f));
        }
        if (p == null) return;
        Rect f = inBath ? new Rect(bath.xMin + 40, bath.yMin + 30, 180, 90) : soaked ? new Rect(rinse.xMin + 30, rinse.yMin + 30, 160, 80) : new Rect(300, 320, 140, 60);
        Fluff(c, p, f, (float)p.fibre.alignment);
        if (inBath) Meter(c, bath.xMin, bath.yMax + 6, bath.width, 6, Mathf.Clamp01((Time.unscaledTime - soakStart) * MinutesPerSecond / 40f), Mark);
        Meter(c, rinse.xMin, rinse.yMax + 6, rinse.width, 6, rinses / 4f, new Color(0.6f, 0.85f, 1f));
    }

    private void DrawCard(CraftPixels c, CraftPiece p)
    {
        // The bottom card on the knee, its teeth, the fibre on it; the top card in the hand.
        c.Rect(bottomCard.xMin, bottomCard.yMin, bottomCard.xMax, bottomCard.yMax, Wood);
        c.Rect(bottomCard.xMin - 80, bottomCard.center.y - 10, bottomCard.xMin, bottomCard.center.y + 10, Wood);
        for (float x = bottomCard.xMin + 6; x < bottomCard.xMax; x += 8) c.Line(x, bottomCard.yMax, x + 3, bottomCard.yMax + 6, Iron, 1f);
        if (p != null) Fluff(c, p, new Rect(bottomCard.xMin + 10, bottomCard.yMax + 4, bottomCard.width - 20, 60), (float)p.fibre.alignment);
        Vector2 h = card.Count > 0 ? card[card.Count - 1] : ui.Hover;
        if (h.x >= 0)
        {
            float press = Pressure(h.y);
            c.Rect(h.x - 160, h.y, h.x + 160, h.y + 40, Wood * 1.1f);
            c.Rect(h.x + 160, h.y + 10, h.x + 230, h.y + 30, Wood);
            for (float x = h.x - 154; x < h.x + 160; x += 8) c.Line(x, h.y, x + 3, h.y - 6, Iron, 1f);
            Meter(c, 30, 380, 260, 8, press, press > 0.6f ? new Color(1f, 0.4f, 0.3f) : press < 0.15f ? new Color(0.6f, 0.85f, 1f) : new Color(0.45f, 0.9f, 0.45f));
        }
        Meter(c, 30, 366, 260, 6, p != null ? (float)p.fibre.alignment : 0f, Mark);
    }

    private float Pressure(float y) => Mathf.Clamp01((bottomCard.yMax + 110f - y) / 100f);

    private void DrawComb(CraftPixels c, CraftPiece p)
    {
        c.Rect(comb.xMin - 30, comb.yMin - 20, comb.xMin, comb.yMax + 20, Wood);
        for (float y = comb.yMin; y < comb.yMax; y += 12) c.Line(comb.xMin, y, comb.xMax, y, Iron, 2f);
        if (p == null) return;
        // The fibre lashed on the comb, the noil left in it, and what has been drawn off.
        Fluff(c, p, new Rect(comb.xMax, comb.yMin, 140, comb.height), (float)p.fibre.alignment);
        for (int k = 0; k < Mathf.RoundToInt((1f - (float)p.fibre.shortFibre) * 6f); k++) c.Disc(comb.xMin + 10, comb.yMin + 12 + k * 28, 4f, new Color(0.7f, 0.65f, 0.55f));
        for (int i = 1; i < pass.Count; i++) c.Line(pass[i - 1].x, pass[i - 1].y, pass[i].x, pass[i].y, new Color(1f, 1f, 1f, 0.5f), 3f);
        Meter(c, 30, 380, 260, 8, (float)p.fibre.alignment, Mark);
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Batch;
        if (place == Place.Scour) { ScourPointer(phase, pos); return; }
        if (p == null) { if (phase == CraftPointer.Down) said = "Lay a batch of fibre on the bench and pick it (left)."; return; }
        switch (place)
        {
            case Place.Pick: PickPointer(phase, pos); break;
            case Place.Tease: TeasePointer(phase, pos); break;
            case Place.Card: CardPointer(phase, pos); break;
            default: CombPointer(phase, pos); break;
        }
    }

    private void PickPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase != CraftPointer.Down) return;
        Vector2 at = pos + new Vector2((float)Hands.Tremble(picked + misses + 131) * 50f, (float)Hands.Tremble(picked + misses + 132) * 50f);
        for (int i = 0; i < burrs.Count; i++)
            if (Vector2.Distance(at, burrs[i]) < 7f)
            {
                burrs.RemoveAt(i);
                picked++;
                Hands.Move(0.02);
                if (burrs.Count == 0) DonePicking();
                return;
            }
        if (bench.Contains(pos)) { misses++; said = "Only good fibre came away with the tweezers."; }
    }

    private void DonePicking()
    {
        CraftPiece p = Batch;
        if (p == null || picked + misses == 0) return;
        float share = burrsAtStart > 0 ? picked / (float)burrsAtStart : 1f, lost = Mathf.Clamp01(misses / Mathf.Max(6f, picked + misses) * 1.5f);
        if (Send(new CraftRequest(CraftRequestKind.CleanFibre, p.id) { a = share, b = lost }))
            said = share > 0.85f ? "Picked clean." : $"Picked over: {Mathf.RoundToInt(share * 100)}% of the debris out.";
        madeFor = "";
    }

    private void ScourPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            if (fireGauge.Contains(pos)) { holdingFire = true; fire = Mathf.Clamp01((pos.x - fireGauge.xMin) / fireGauge.width); return; }
            if (rinseGauge.Contains(pos)) { holdingRinse = true; rinseTemp = Mathf.Lerp(5f, 80f, (pos.x - rinseGauge.xMin) / rinseGauge.width); return; }
            if (inBath && bath.Contains(pos)) { lastStir = pos; return; }
            if (soaked && rinse.Contains(pos))
            {
                rinses++;
                said = Mathf.Abs(rinseTemp - LastBath) > 25f ? "Plunged from the hot bath into cold water: wool can felt like that." : $"Rinsed ({rinses}).";
                return;
            }
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            if (holdingFire) fire = Mathf.Clamp01((pos.x - fireGauge.xMin) / fireGauge.width);
            if (holdingRinse) rinseTemp = Mathf.Lerp(5f, 80f, Mathf.Clamp01((pos.x - rinseGauge.xMin) / rinseGauge.width));
            if (inBath && bath.Contains(pos)) { stir += Vector2.Distance(pos, lastStir) / 2500f; lastStir = pos; }
            return;
        }
        if (phase == CraftPointer.Up) holdingFire = holdingRinse = false;
    }

    private float LastBath => soakN > 0f ? soakSum / soakN : water;

    private void PutIn()
    {
        CraftPiece p = Batch;
        if (p == null) { said = "Pick a batch of fibre (left)."; return; }
        inBath = true;
        soaked = false;
        soakStart = Time.unscaledTime;
        soakSum = soakN = stir = rinses = 0f;
        said = water < 35f ? "Into a cold bath: the grease will not lift. Feed the fire." : "Into the bath. Leave it still to soak.";
    }

    private void LiftOut()
    {
        if (!inBath) return;
        inBath = false;
        soaked = true;
        said = "Lifted out. Now rinse it: set the rinse water near the bath's heat (blue gauge), click the tub for each rinse.";
    }

    private void HangToDry()
    {
        CraftPiece p = Batch;
        if (p == null || !soaked) { said = "Put it in the bath and lift it out first."; return; }
        float minutes = soakN * MinutesPerSecond;
        string det = detergents.Count > 0 && Have(detergents[Mathf.Clamp(detergent.value, 0, detergents.Count - 1)]) != 0 ? detergents[Mathf.Clamp(detergent.value, 0, detergents.Count - 1)] : "";
        float shock = rinses > 0 ? Mathf.Abs(LastBath - rinseTemp) : 0f;
        if (Send(new CraftRequest(CraftRequestKind.ScourFibre, p.id) { a = LastBath, b = minutes, c = Mathf.Clamp01(stir), d = shock, id = det, i0 = Mathf.RoundToInt(rinses) }))
            said = string.IsNullOrEmpty(det) ? "Washed in water alone: the grease stays. Hung up to dry." : "Scoured and hung up to dry.";
        soaked = inBath = false;
        soakN = soakSum = 0f;
        madeFor = "";
    }

    private void TeasePointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down)
        {
            for (int i = 0; i < locks.Count; i++)
                if (locks[i].z < 0.95f && Vector2.Distance(pos, new Vector2(locks[i].x, locks[i].y)) < 40f) { dragLock = i; lockFrom = pos; lockStart = Time.unscaledTime; return; }
            return;
        }
        if (phase != CraftPointer.Up || dragLock < 0) return;
        float dist = Vector2.Distance(pos, lockFrom), speed = dist / Mathf.Max(0.05f, Time.unscaledTime - lockStart);
        Vector3 l = locks[dragLock];
        float harsh = Mathf.Clamp01((speed - 250f) / 600f);
        l.z = Mathf.Clamp01(l.z + Mathf.Clamp01(dist / 120f) * (1f - 0.8f * (float)Batch.fibre.felted));
        locks[dragLock] = l;
        harshSum += harsh;
        if (l.z >= 0.95f) locksDone++;
        said = harsh > 0.5f ? "Torn apart fast: you hear fibres snap." : l.z >= 0.95f ? "Teased open." : "Teased a little looser.";
        dragLock = -1;
        Hands.Move(0.05);
        bool all = true;
        foreach (Vector3 k in locks) all &= k.z >= 0.95f;
        if (all) DoneTeasing();
    }

    private void DoneTeasing()
    {
        CraftPiece p = Batch;
        if (p == null || locks.Count == 0) return;
        float done = 0f;
        foreach (Vector3 k in locks) done += k.z;
        done /= locks.Count;
        if (done < 0.05f) return;
        if (Send(new CraftRequest(CraftRequestKind.OpenFibre, p.id) { a = done, b = harshSum / Mathf.Max(1, locks.Count) }))
            said = done > 0.9f ? "The locks are all open." : "Partly teased.";
        madeFor = "";
    }

    private void CardPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down) { card.Clear(); card.Add(pos); cardStart = Time.unscaledTime; return; }
        if (card.Count == 0) return;
        if (phase == CraftPointer.Drag) { card.Add(pos + new Vector2(0f, (float)Hands.Tremble(card.Count + 141) * 40f)); return; }
        if (phase != CraftPointer.Up) return;
        float dx = card[card.Count - 1].x - card[0].x, press = 0f;
        foreach (Vector2 k in card) press += Pressure(k.y);
        press /= card.Count;
        card.Clear();
        if (Mathf.Abs(dx) < 80f) return;
        int dir = dx > 0 ? 1 : -1;
        float even = (lastDir == 0 || dir == lastDir ? 1f : 0.3f) * Mathf.Clamp01(1f - (float)Hands.Unsteadiness * 3f);
        lastDir = dir;
        pressureSum += press;
        evenSum += even;
        pendingStrokes++;
        cardStrokes++;
        Hands.Move(0.08);
        said = press > 0.6f ? "Pressed hard: the teeth roll the fibre into knots." : even < 0.5f ? "Stroked the other way: it tangles what was combed." : "A light, even stroke.";
        if (pendingStrokes >= 4) FlushCard();
    }

    private void FlushCard()
    {
        CraftPiece p = Batch;
        if (p == null || pendingStrokes == 0) { pendingStrokes = 0; pressureSum = evenSum = 0f; return; }
        Send(new CraftRequest(CraftRequestKind.CardFibre, p.id) { count = pendingStrokes, a = pressureSum / pendingStrokes, b = evenSum / pendingStrokes });
        pendingStrokes = 0;
        pressureSum = evenSum = 0f;
    }

    private void CombPointer(CraftPointer phase, Vector2 pos)
    {
        if (phase == CraftPointer.Down && pos.x < comb.xMax + 150f && pos.x > comb.xMax - 10f) { pass.Clear(); pass.Add(pos); passStart = Time.unscaledTime; return; }
        if (pass.Count == 0) return;
        if (phase == CraftPointer.Drag) { pass.Add(pos + new Vector2(0f, (float)Hands.Tremble(pass.Count + 151) * 50f)); return; }
        if (phase != CraftPointer.Up) return;
        float dx = pass[pass.Count - 1].x - pass[0].x, wobble = 0f;
        for (int i = 1; i < pass.Count; i++) wobble += Mathf.Abs(pass[i].y - pass[i - 1].y);
        float speed = dx / Mathf.Max(0.05f, Time.unscaledTime - passStart);
        pass.Clear();
        if (dx < 150f) { said = "Draw it right off the comb."; return; }
        float trueness = Mathf.Clamp01(1f - wobble / dx * 2f) * Mathf.Clamp01(1f - Mathf.Max(0f, speed - 600f) / 800f);
        Hands.Move(0.1);
        if (Send(new CraftRequest(CraftRequestKind.CombFibre, Batch.id) { count = 1, a = trueness }))
            said = trueness > 0.75f ? "Drawn off straight: the short fibres stay in the comb." : "A crooked pass: it drags the short fibres along.";
    }

    public override void Refresh()
    {
        RefreshCounts(fibre, fibres);
        NoteBatches(fibre, fibres);
        RefreshCounts(detergent, detergents);
        if (info == null) return;
        CraftPiece p = Batch;
        string how;
        if (p == null) how = "<b>Fibre bench</b>: lay a batch of fibre on the bench (above) and pick it in the list (left).";
        else
            switch (place)
            {
                case Place.Pick: how = $"<b>Pick</b>: click each burr and seed to pick it out ({picked} picked, {burrs.Count} left); clicks on clean fibre pull good fibre out. Then Done picking."; break;
                case Place.Scour:
                    how = Station.HasInstrument(CraftInstruments.Thermometer)
                        ? $"<b>Scour</b>: drag the orange gauge to feed the fire - the bath is {water:0} °C (grease lifts from about 45 °C; wool felts if stirred hot). Put it in, let it soak, lift it out, rinse at about the same heat ({rinseTemp:0} °C), hang it to dry."
                        : $"<b>Scour</b>: drag the orange gauge to feed the fire - the bath is {(water > 90 ? "simmering" : water > 55 ? "steaming hot" : water > 35 ? "warm" : "cold")}. Put it in, let it soak still, lift it out, rinse it in water near the bath's heat (blue gauge), hang it to dry.";
                    break;
                case Place.Tease: how = "<b>Tease</b>: drag across each matted lock to pull it open - slowly, or the fibres break."; break;
                case Place.Card: how = p.fibre.wet > 0.5 ? "It is still wet: let it dry before carding." : $"<b>Card</b>: drag the top card across the bottom one, always the same way, lightly (green on the gauge). {cardStrokes} strokes."; break;
                default:
                    CraftSupplyDefinition d = Rules.Supply(p.fibre.fibre);
                    how = d != null && d.staple < 40 ? $"{d.Name} is too short to comb: card it." : "<b>Comb</b>: press by the comb's tines and draw the fibre off to the right, straight and steady. Each pass leaves the short fibre (noil) behind.";
                    break;
            }
        info.text = (how + " " + said).Trim();
    }
}
