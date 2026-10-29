using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One work area of the station window (the forge, the anvil, the wheel, the beam...): it draws its work surface from
/// the workshop's real state, turns the pointer on it into hand movements (a hammer blow, a stroke on the stone, a
/// tilt of the crucible) through the hand-skill models (<see cref="CraftHammer"/>, <see cref="CraftStroke"/>,
/// <see cref="CraftPourControl"/>, <see cref="CraftRhythm"/>), and sends what the hands did to the station as requests.
/// Nothing here decides an outcome: the workshop does.
/// </summary>
public abstract class CraftMode
{
    protected CraftStationUI ui;

    public abstract string Title { get; }
    /// <summary>Does the station have what this area needs?</summary>
    public abstract bool Available(CraftStation s);

    /// <summary>Goes to a place of this area by its name ("Grind", "Sew"): the guide's Go button.</summary>
    public virtual void GoTo(string place) { }

    /// <summary>The craft this area's work practises: the player's practice in it steadies the hands here.</summary>
    public virtual CraftDiscipline Discipline => CraftDiscipline.Smithing;
    /// <summary>One line on how the area is worked.</summary>
    public virtual string Hint => "";

    public void Attach(CraftStationUI owner) => ui = owner;

    protected CraftStation Station => ui.Station;
    protected CraftWorkshop Ws => ui.Station.Workshop;
    protected CraftRules Rules => ui.Station.Rules;
    protected CraftPiece Selected => ui.SelectedPiece;
    protected CraftHands Hands => ui.Hands;

    /// <summary>Builds the controls under the work surface (called when the area is opened).</summary>
    public virtual void Build(RectTransform controls) { }
    /// <summary>Draws the work surface.</summary>
    public virtual void Draw(CraftPixels c) { }
    public virtual void Pointer(CraftPointer phase, Vector2 p, bool right, float scroll) { }
    /// <summary>Every frame (unscaled): hammer charge, pour, rhythm.</summary>
    public virtual void Tick(float dt) { }
    /// <summary>Updates the controls' labels (a few times a second).</summary>
    public virtual void Refresh() { }
    /// <summary>The area is closed (another opened, the window shut): finish what the hands were doing.</summary>
    public virtual void Leave() { }

    // ------------------------------------------------------------------ by hand, in 3D
    /// <summary>The 3D work place this area can also be worked at with the hands (<see cref="CraftHandsOn"/>); None if it has none.</summary>
    public virtual CraftHandsOnMode HandsOnMode => CraftHandsOnMode.None;
    /// <summary>What the 3D work place starts with: the tool, the stone, the mould... chosen here.</summary>
    protected virtual void FillHandsOn(ref CraftHandsOnSetup setup) { }

    /// <summary>Takes up this area's work place in 3D (the window steps aside until it is put down).</summary>
    public bool TakeUpByHand()
    {
        if (HandsOnMode == CraftHandsOnMode.None || ui == null)
            return false;
        var setup = new CraftHandsOnSetup { piece = ui.SelectedId };
        FillHandsOn(ref setup);
        Leave(); // what the pointer was doing on the drawing ends here
        return ui.EnterHandsOn(HandsOnMode, setup);
    }

    /// <summary>The button that takes up the work place in 3D.</summary>
    protected Button HandsOnButton(Transform parent, string label, float width = 170f) =>
        Btn(parent, $"{label}  ({StationControls.Key(StationControl.ByHand)})", () => TakeUpByHand(), width, UITheme.ButtonStyle.Primary);

    /// <summary>The end of an area's hint: the key that takes its work place up in 3D.</summary>
    protected static string ByHandHint(string what) => $" {StationControls.K(StationControl.ByHand)}: {what}.";

    // ------------------------------------------------------------------ controls
    protected static RectTransform Row(Transform parent, float height = 34f, float spacing = 8f) => UITheme.Row("Row", parent, height, spacing);

    protected static Button Btn(Transform parent, string label, UnityAction onClick, float width = 0f, UITheme.ButtonStyle style = UITheme.ButtonStyle.Normal, float height = 32f)
    {
        Button b = UITheme.Button(label, parent, label, onClick, style, width, height);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = 10f;
            t.fontSizeMax = 15f;
            t.margin = new Vector4(6f, 0f, 6f, 0f);
        }
        if (width <= 0f) InventoryUIFactory.Size(b, -1f, height, 1f);
        return b;
    }

    protected static TextMeshProUGUI Text(Transform parent, string text, float size = 14f, float width = -1f, Color? color = null)
    {
        TextMeshProUGUI t = UITheme.Label(parent, text, size, color ?? UITheme.Muted);
        InventoryUIFactory.Size(t, width, 30f, width < 0f ? 1f : 0f);
        // A label on a row stays on its row: a long text ends in "..." rather than running out of the window.
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    protected static Slider Slide(Transform row, string label, float min, float max, float value, bool whole = false, float labelWidth = 86f)
    {
        TextMeshProUGUI l = UITheme.Label(row, label, 13f, UITheme.Muted);
        InventoryUIFactory.Size(l, labelWidth, 30f, 0f);
        Slider s = UITheme.BareSlider(label, row, min, max, value, whole);
        InventoryUIFactory.Size(s, -1f, 26f, 1f);
        return s;
    }

    protected static TMP_Dropdown Drop(Transform row, IList<string> options, int value = 0, float width = 0f)
    {
        TMP_Dropdown d = UITheme.BareDropdown("Choice", row);
        d.ClearOptions();
        d.AddOptions(new List<string>(options));
        d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
        InventoryUIFactory.Size(d, width > 0f ? width : -1f, 32f, width > 0f ? 0f : 1f);
        return d;
    }

    protected bool Send(CraftRequest r) => ui.Send(r);

    /// <summary>
    /// A fuel menu: every fuel of the rules with how much of it the player has ("Charcoal (4)", "Firewood (free)",
    /// "Coal (none)"), set to the first one they have. Firewood counts any wood in the bag.
    /// </summary>
    protected TMP_Dropdown FuelDrop(Transform row, List<string> ids, float width)
    {
        ids.Clear();
        foreach (CraftFuel f in Rules.Fuels) ids.Add(f.id);
        return CountedDrop(row, ids, width, "No fuel");
    }

    /// <summary>A menu of supplies (fuels, fibres, threads...) by id, each with how much of it the player has, set to the first one they have.</summary>
    protected TMP_Dropdown CountedDrop(Transform row, List<string> ids, float width, string none = "-")
    {
        var names = CountedNames(ids, out int first);
        return Drop(row, names.Count > 0 ? names : new List<string> { none }, first, width);
    }

    /// <summary>Brings a counted menu up to date (after using some, or the bag changing).</summary>
    protected void RefreshCounts(TMP_Dropdown d, List<string> ids)
    {
        if (d == null || ids.Count == 0 || d.options.Count != ids.Count) return;
        var names = CountedNames(ids, out _);
        bool changed = false;
        for (int i = 0; i < names.Count; i++)
        {
            // Keep a note added after the count (batches on the bench).
            string text = d.options[i].text;
            int cut = text.IndexOf(" + ", System.StringComparison.Ordinal);
            string note = cut >= 0 ? text.Substring(cut) : "";
            if (text != names[i] + note) { d.options[i].text = names[i] + note; changed = true; }
        }
        if (changed) d.RefreshShownValue();
    }

    /// <summary>A fibre menu also says how many batches of each fibre already lie on the station ("Raw wool (none) + 2 on the bench").</summary>
    protected void NoteBatches(TMP_Dropdown d, List<string> ids)
    {
        if (d == null || Station == null || d.options.Count != ids.Count) return;
        bool changed = false;
        for (int i = 0; i < ids.Count; i++)
        {
            int n = Station.FibreBatches(ids[i]).Count;
            string text = d.options[i].text;
            int cut = text.IndexOf(" + ", System.StringComparison.Ordinal);
            if (cut >= 0) text = text.Substring(0, cut);
            if (n > 0) text += $" + {n} on the bench";
            if (text != d.options[i].text) { d.options[i].text = text; changed = true; }
        }
        if (changed) d.RefreshShownValue();
    }

    /// <summary>How much of a supply or fuel the player can use here: -1 = free, 0 = none.</summary>
    protected int Have(string id) => Station != null ? Station.SupplyAvailable(ui.Player, id) : 0;

    /// <summary>The item a supply or fuel is taken from ("Raw Wool"), for telling the player what they lack.</summary>
    protected string ItemOf(string id)
    {
        CraftFuel f = Rules.Fuel(id);
        if (f != null) return string.IsNullOrEmpty(f.item) ? f.Name : f.item;
        CraftSupplyDefinition s = Rules.Supply(id);
        return s != null ? string.IsNullOrEmpty(s.item) ? s.Name : s.item : id;
    }

    private List<string> CountedNames(List<string> ids, out int first)
    {
        first = -1;
        var names = new List<string>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            string name = Rules.Fuel(ids[i])?.Name ?? Rules.Supply(ids[i])?.Name ?? ids[i];
            int have = Have(ids[i]);
            if (have != 0 && first < 0) first = i;
            names.Add($"{name} ({(have < 0 ? "free" : have == 0 ? "none" : have.ToString())})");
        }
        if (first < 0) first = 0;
        return names;
    }

    /// <summary>
    /// A test station: quick buttons stand in for the work of the hands (an instant flux, fold, clay...). At an
    /// ordinary station every such action is done with the hands on the work surface.
    /// </summary>
    protected bool QuickBench => Station != null && Station.Reveals;

    /// <summary>A line of guidance under the controls (what to do now, what the work shows).</summary>
    protected static TextMeshProUGUI InfoRow(RectTransform controls, float height = 48f)
    {
        RectTransform row = UITheme.Row("Info", controls, height, 8f);
        TextMeshProUGUI t = UITheme.Label(row, "", 13f, UITheme.Text);
        InventoryUIFactory.Size(t, -1f, height, 1f);
        FitInside(t);
        return t;
    }

    /// <summary>A text that wraps and shrinks to stay inside its box (and ends in "..." if even that is not enough).</summary>
    protected static void FitInside(TextMeshProUGUI t)
    {
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 9f;
        t.fontSizeMax = 13f;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    protected static CraftRequest R(CraftRequestKind kind, string piece = null) => new CraftRequest(kind, piece);

    // ------------------------------------------------------------------ drawing helpers
    protected static readonly Color Back = new Color(0.06f, 0.06f, 0.08f, 1f);
    protected static readonly Color Wood = new Color(0.36f, 0.24f, 0.14f, 1f);
    protected static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f, 1f);
    protected static readonly Color Water = new Color(0.25f, 0.4f, 0.55f, 0.85f);
    protected static readonly Color Mark = new Color(1f, 0.8f, 0.38f, 0.9f);

    /// <summary>The scale (mm per pixel) that fits a piece in <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    protected static float Fit(CraftPiece p, float width, float height)
    {
        double w = 1;
        foreach (MetalSegment s in p.segments) w = System.Math.Max(w, s.width);
        return Mathf.Max(0.05f, Mathf.Max((float)(p.Length / width), (float)(w / height)));
    }

    /// <summary>Which segment is under pixel x for a piece drawn from <paramref name="x0"/> at <paramref name="mmPerPixel"/>.</summary>
    protected static int SegmentAtPixel(CraftPiece p, float x, float x0, float mmPerPixel)
    {
        double mm = (x - x0) * mmPerPixel, pos = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            pos += p.segments[i].length;
            if (mm <= pos) return i;
        }
        return Mathf.Max(0, p.segments.Count - 1);
    }

    /// <summary>The position (0-1 along the piece) under pixel x.</summary>
    protected static float PositionAtPixel(CraftPiece p, float x, float x0, float mmPerPixel) =>
        Mathf.Clamp01((float)((x - x0) * mmPerPixel / System.Math.Max(1e-6, p.Length)));

    /// <summary>A horizontal bar meter.</summary>
    protected static void Meter(CraftPixels c, float x, float y, float w, float h, float value, Color fill)
    {
        c.Rect(x, y, x + w, y + h, new Color(0f, 0f, 0f, 0.6f));
        c.Rect(x + 1, y + 1, x + 1 + (w - 2) * Mathf.Clamp01(value), y + h - 1, fill);
    }

    protected static bool IsMetal(CraftPiece p) => p != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood);

    // ------------------------------------------------------------------ practice
    protected CraftMasterySettings MasterySettings => Rules?.Settings?.mastery;
    /// <summary>The player's practice in a craft (0 novice - 1 complete mastery).</summary>
    protected double MasteryOf(CraftDiscipline d) => CraftMastery.Of(ui.Known, d, MasterySettings);
    /// <summary>The player's practice in this area's craft.</summary>
    protected double Mastery => MasteryOf(Discipline);

    private readonly List<(Button button, CraftDiscipline craft, CraftRank rank, string label)> practisedButtons = new List<(Button, CraftDiscipline, CraftRank, string)>();

    /// <summary>Is the player practised enough in <paramref name="craft"/> for work that needs <paramref name="rank"/>?</summary>
    protected bool Practised(CraftDiscipline craft, CraftRank rank) => MasteryOf(craft) >= CraftMastery.RankStart(rank, MasterySettings) - 1e-9;

    /// <summary>
    /// A button for work a practised hand does without thinking: it works for a player of <paramref name="rank"/> in
    /// <paramref name="craft"/>, and says which rank it needs otherwise.
    /// </summary>
    protected Button PractisedButton(Transform row, CraftDiscipline craft, CraftRank rank, string label, UnityAction act, float width = 160f)
    {
        Button b = Btn(row, label, () =>
        {
            if (!Practised(craft, rank))
            {
                ui.Message($"Only {CraftText.A(CraftMastery.RankName(rank))} {CraftMastery.RankName(rank).ToLowerInvariant()} {CraftMastery.Crafter(craft)} does that without thinking: do it by hand.");
                return;
            }
            act();
        }, width, UITheme.ButtonStyle.Ghost);
        practisedButtons.Add((b, craft, rank, label));
        RefreshPractice();
        return b;
    }

    /// <summary>
    /// A button for work done by routine (<see cref="CraftRoutine"/>: one action, as well as the player's practice does it;
    /// the station checks the rank and works out how well). <paramref name="piece"/> gives the piece it is done to.
    /// </summary>
    protected Button RoutineButton(Transform row, CraftRoutine routine, string label, System.Func<string> piece, float width = 160f) =>
        PractisedButton(row, CraftMastery.RoutineDiscipline(routine), CraftMastery.RoutineRank(routine), label,
            () => Send(new CraftRequest(CraftRequestKind.Routine, piece?.Invoke()) { i0 = (int)routine }), width);

    /// <summary>The practised-hand buttons as the player's practice now allows (called with every refresh).</summary>
    public void RefreshPractice()
    {
        practisedButtons.RemoveAll(x => x.button == null);
        foreach ((Button b, CraftDiscipline craft, CraftRank rank, string label) in practisedButtons)
        {
            bool can = Practised(craft, rank);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = can ? label : $"{label} ({CraftMastery.RankName(rank)})";
            b.interactable = can;
        }
    }

    // ------------------------------------------------------------------ the shared timing bar
    private static readonly Color ZonePoor = new Color(0.55f, 0.25f, 0.18f, 1f), ZoneGood = new Color(0.75f, 0.6f, 0.2f, 1f),
        ZoneExcellent = new Color(0.35f, 0.7f, 0.3f, 1f), ZonePerfect = new Color(0.75f, 1f, 0.6f, 1f);

    /// <summary>The colour a timing grade is shown in.</summary>
    protected static Color GradeColor(CraftTimingGrade g) => g switch
    {
        CraftTimingGrade.Perfect => ZonePerfect,
        CraftTimingGrade.Excellent => ZoneExcellent,
        CraftTimingGrade.Good => ZoneGood,
        CraftTimingGrade.Poor => ZonePoor,
        _ => new Color(0.9f, 0.25f, 0.2f, 1f),
    };

    /// <summary>
    /// Draws a <see cref="CraftTimingBar"/>: each target's nested zones (Poor, Good, Excellent, Perfect - a target that
    /// gives less is drawn dimmer), its ideal point, the moving indicator and, if there was one, the last hit.
    /// </summary>
    protected static void DrawTimingBar(CraftPixels c, CraftTimingBar bar, float x, float y, float w, float h, CraftTimingHit? last = null)
    {
        if (bar == null) return;
        c.Rect(x - 2, y - 2, x + w + 2, y + h + 2, new Color(0f, 0f, 0f, 0.75f));
        c.Rect(x, y, x + w, y + h, new Color(0.16f, 0.15f, 0.15f, 1f));
        foreach (CraftTimingTarget t in bar.targets)
        {
            float dim = 0.45f + 0.55f * Mathf.Clamp01(t.best);
            for (int z = 0; z < 4; z++)
            {
                var grade = (CraftTimingGrade)(1 + z);
                float half = bar.Zone(grade, t) * w;
                Color col = GradeColor(grade) * dim;
                col.a = 1f;
                float inset = z * h * 0.08f;
                c.Rect(Mathf.Max(x, x + t.ideal * w - half), y + inset, Mathf.Min(x + w, x + t.ideal * w + half), y + h - inset, col);
            }
            c.Rect(x + t.ideal * w - 0.75f, y - 3, x + t.ideal * w + 0.75f, y + h + 3, Color.white);
        }
        if (last.HasValue)
        {
            float lx = x + Mathf.Clamp01(last.Value.position) * w;
            c.Rect(lx - 1.5f, y - 6, lx + 1.5f, y + h + 6, GradeColor(last.Value.grade));
        }
        float ix = x + Mathf.Clamp01(bar.position) * w;
        c.Rect(ix - 2f, y - 8, ix + 2f, y + h + 8, new Color(0.95f, 0.95f, 1f, 1f));
        c.Disc(ix, y + h + 10, 4f, new Color(0.95f, 0.95f, 1f, 1f));
    }

    /// <summary>A hit in words: its grade and score, and the harm a bad one did.</summary>
    protected static string HitWords(CraftTimingHit h, string harm = "damage") =>
        h.damage ? $"{h.Words} - {harm}!" : h.Words;
}
