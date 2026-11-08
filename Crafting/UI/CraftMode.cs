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
/// <para>The pointer means the same in every area (<see cref="CraftPointer"/>, sampled by <see cref="CraftPointerSampler"/>):
/// <b>right-click (<see cref="CraftPointer.Cancel"/>) puts down or aborts what the hands are doing - it never commits,
/// never destroys, never acts as a left click. The wheel turns or adjusts what is in hand. Everything else is a left
/// click or a button.</b> Down, Drag and Up are the left button's alone; a right-click in the middle of a gesture comes
/// as Cancel and the gesture's own Drag and Up still follow (an area that aborted ignores them). Drag and Move samples
/// come by distance (every 2 to 6 pixels of the way), not once a frame: an area that measures speed takes the time of
/// the sample from <see cref="CraftStationUI.PointerTime"/>. There is no drag threshold, so the wobble of a click comes
/// as a few Drag samples: an area that acts on Down (placing a pattern, marking) ignores the drag and release of that
/// same press. The scroll value is one notch (+1 or -1).</para>
/// <para>The selected piece can change under an open area (picked in the list, taken, gone, the window closed):
/// <see cref="SelectionChanging"/> comes first, while the old piece is still selected, then <see cref="SelectionChanged"/>.
/// A request's real outcome comes in the <c>answered</c> callback of <see cref="Send"/> (on a network client a round trip
/// later, with the copy of the workshop already showing it): success words, read-backs of the model and the events the
/// request raised (<see cref="AnswerEvents"/>) go there.</para>
/// </summary>
public abstract class CraftMode
{
    protected CraftStationUI ui;

    public abstract string Title { get; }
    /// <summary>Does the station have what this area needs?</summary>
    public abstract bool Available(CraftStation s);

    /// <summary>Goes to a place of this area by its name ("Grind", "Sew"): the guide's Go button.</summary>
    public virtual void GoTo(string place) { }
    /// <summary>Does this area have the place the guide names (backed by the area's own checks of what the station has)?</summary>
    public virtual bool HasPlace(string place) => false;
    /// <summary>
    /// The place the guide names by its key ("Layout"), as its button reads here ("Lay out"): what the guide line shows,
    /// so the player finds it on the tabs (the key itself is what <see cref="GoTo"/> takes).
    /// </summary>
    public virtual string PlaceTitle(string place) => place;

    /// <summary>
    /// How the arm feels at <paramref name="level"/> (<see cref="CraftHands.Level"/>), in this area's terms: the hand's
    /// work by default; an area whose hands strike blows (a hammer, a maul, a mallet) says so while they do.
    /// </summary>
    public virtual string ArmWords(int level) => CraftHands.FeelingAt(level, false);

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
    /// <summary>
    /// What the pointer did on the work surface (see the convention above). <paramref name="right"/> is true only for
    /// <see cref="CraftPointer.Cancel"/>; <paramref name="scroll"/> is +1 or -1 for <see cref="CraftPointer.Scroll"/>.
    /// </summary>
    public virtual void Pointer(CraftPointer phase, Vector2 p, bool right, float scroll) { }
    /// <summary>Every frame (unscaled): hammer charge, pour, rhythm.</summary>
    public virtual void Tick(float dt) { }
    /// <summary>Updates the controls' labels (a few times a second).</summary>
    public virtual void Refresh() { }
    /// <summary>The area is closed (another opened, the window shut): finish what the hands were doing.</summary>
    public virtual void Leave() { }

    /// <summary>
    /// Before the selected piece changes (picked in the list, taken, gone from the station, the bench cleared, the window
    /// closed): finish the hand work done on <paramref name="from"/> - send what is pending for it, while it is still the
    /// selected piece - and drop every gesture and per-piece state the area holds. The area stays open. When the piece
    /// vanished, nothing is selected any more while this runs (a flush then sends nothing): zero pending counters before
    /// sending them.
    /// </summary>
    public virtual void SelectionChanging(string from, string to) => Leave();
    /// <summary>After the selected piece changed: take up what the new piece shows (a frame's tension, a blank's purpose, a seam list).</summary>
    public virtual void SelectionChanged(string to) { }

    /// <summary>
    /// Can this area work a part already fitted to an assembly (a blade in its hilt, a head on its haft)? The piece list
    /// shows an assembly's fitted parts under it, and they can be picked while such an area is open.
    /// </summary>
    public virtual bool AcceptsFitted(CraftPiece part) => false;

    /// <summary>
    /// Hand work of several presses under way that leaving the area would lose (a weld being set, a chisel job, a saw cut
    /// half made), in words that follow "Finish ..." ("setting the weld"); null when there is none. The work place is not
    /// taken up in 3D while it is set, and leaving the station says it was left unfinished (closing the window too, unless
    /// the area keeps it for when the window is opened here again: <see cref="Kept"/>).
    /// </summary>
    public virtual string Busy => null;

    /// <summary>
    /// What the window says as it closes for hand work of several presses this area keeps (a warp on the loom, a skein
    /// half wound, a fleece in the bath): it is all there again when the window is opened at this station, and only
    /// working at another station (or as another player) starts afresh - so it is told as kept, never as stopped (the
    /// area's <see cref="Busy"/> still keeps the piece it is done on from being taken). A sentence, or null: nothing kept.
    /// </summary>
    public virtual string Kept => null;

    /// <summary>
    /// While true the work surface is drawn every frame (up to 60 times a second: a timing bar, a pour, a raised hammer, a
    /// moving gauge); otherwise 20 times a second.
    /// </summary>
    public bool LiveDraw { get; protected set; }

    // ------------------------------------------------------------------ by hand, in 3D
    /// <summary>The 3D work place this area can also be worked at with the hands (<see cref="CraftHandsOn"/>); None if it has none.</summary>
    public virtual CraftHandsOnMode HandsOnMode => CraftHandsOnMode.None;
    /// <summary>What the 3D work place starts with: the tool, the stone, the mould... chosen here.</summary>
    protected virtual void FillHandsOn(ref CraftHandsOnSetup setup) { }
    /// <summary>
    /// The 3D work place handed the work back: put the area's own controls back from what it ended with (the tool and
    /// face, the stone, the side and angle, the entry, the brush, the cut - <see cref="CraftHandsOn.Setup"/>).
    /// </summary>
    public virtual void TakeBackHandsOn(in CraftHandsOnSetup setup) { }

    /// <summary>
    /// Takes up this area's work place in 3D (the window steps aside until it is put down). Not while the hands are at
    /// work on the drawing (a press under way, or a job of several presses: <see cref="Busy"/>): nothing done there is dropped.
    /// </summary>
    public bool TakeUpByHand()
    {
        if (HandsOnMode == CraftHandsOnMode.None || ui == null)
            return false;
        if (ui.PointerHeld)
        {
            ui.Message("Finish what your hands are doing first.");
            return false;
        }
        string busy = Busy;
        if (!string.IsNullOrEmpty(busy))
        {
            ui.Message($"Finish {busy} first.");
            return false;
        }
        var setup = new CraftHandsOnSetup { piece = ui.SelectedId };
        FillHandsOn(ref setup);
        // Asked before anything here is put down: a work place that cannot be taken up leaves the drawing as it is.
        if (!CraftHandsOn.CanTakeUp(Station, HandsOnMode, setup, out string why))
        {
            ui.Message(why, true);
            return false;
        }
        Leave(); // what is pending on the drawing is sent before the work place is built from the workshop
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

    /// <summary>
    /// A labelled slider on a row; with <paramref name="valueText"/>, its value is shown after it ("650 °C", "35%") and
    /// follows it (<see cref="SetSlider"/> sets it from code).
    /// </summary>
    protected static Slider Slide(Transform row, string label, float min, float max, float value, bool whole = false, float labelWidth = 86f,
                                  System.Func<float, string> valueText = null)
    {
        TextMeshProUGUI l = UITheme.Label(row, label, 13f, UITheme.Muted);
        InventoryUIFactory.Size(l, labelWidth, 30f, 0f);
        Slider s = UITheme.BareSlider(label, row, min, max, value, whole);
        InventoryUIFactory.Size(s, -1f, 26f, 1f);
        if (valueText != null)
        {
            TextMeshProUGUI shown = UITheme.Label(row, "", 12f, UITheme.Text, TextAlignmentOptions.Right);
            InventoryUIFactory.Size(shown, 54f, 30f, 0f);
            shown.textWrappingMode = TextWrappingModes.NoWrap;
            shown.overflowMode = TextOverflowModes.Ellipsis;
            CraftSliderGrip.On(s).ShowValue(shown, valueText);
        }
        return s;
    }

    /// <summary>Sets a slider without telling its listeners (nothing is sent), and updates the value it shows.</summary>
    protected static void SetSlider(Slider s, float value)
    {
        if (s == null)
            return;
        s.SetValueWithoutNotify(value);
        if (s.TryGetComponent(out CraftSliderGrip grip)) grip.Show(s.value);
    }

    /// <summary>
    /// A setting the hand turns (an air vent, a vat's fire, the oven's dial, a depth): <paramref name="write"/> sends the
    /// value when the handle is let go, and at most 5 times a second while it is dragged; it never forces a full window
    /// refresh. The workshop's value (<paramref name="read"/>) is not written back into the slider while it is held, nor
    /// until the workshop shows the value sent (or 1.5 s passed) - so it never jumps back under the hand. Shows its value
    /// (<paramref name="valueText"/>, or two decimals; a whole number for <paramref name="whole"/>).
    /// </summary>
    protected Slider SettingSlider(Transform row, string label, float min, float max, System.Func<float> read, System.Action<float> write,
                                   System.Func<float, string> valueText = null, bool whole = false, float labelWidth = 86f)
    {
        float start = read != null ? read() : min;
        if (valueText == null) valueText = whole ? (System.Func<float, string>)(v => v.ToString("0")) : v => v.ToString("0.00");
        Slider s = Slide(row, label, min, max, start, whole, labelWidth, valueText);
        float tolerance = whole ? 0.55f : Mathf.Max(1e-4f, (max - min) * 0.002f);
        // The workshop's value, while there is one (NaN: nothing to show).
        System.Func<float> current = read == null ? null : (System.Func<float>)(() => Station != null && Station.Workshop != null ? read() : float.NaN);
        CraftSliderGrip.On(s).Setting(current, v => ui.WithoutRefresh(() => write?.Invoke(v)), tolerance);
        return s;
    }

    /// <summary>
    /// Irreversible actions ask first: true on the second click on the same <paramref name="key"/> within 2.5 s; the first
    /// click shows <paramref name="question"/> in the message row ("Click again to throw this stone away.").
    /// </summary>
    protected bool Confirm(string key, string question) => ui.Confirm(key, question);

    protected static TMP_Dropdown Drop(Transform row, IList<string> options, int value = 0, float width = 0f)
    {
        TMP_Dropdown d = UITheme.BareDropdown("Choice", row);
        d.ClearOptions();
        d.AddOptions(new List<string>(options));
        d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
        InventoryUIFactory.Size(d, width > 0f ? width : -1f, 32f, width > 0f ? 0f : 1f);
        return d;
    }

    /// <summary>
    /// Sends a request (see <see cref="CraftStationUI.Send"/>): false when it was refused at once. <paramref name="answered"/>
    /// (ok, reason) comes once with the real outcome - success words, read-backs of the model and work that depends on
    /// this request go there, never in a <c>Send(a) &amp;&amp; Send(b)</c>. On a network client it comes a round trip later,
    /// when the copy of the workshop already shows the result (or as (false, "No answer from the station.") after 10 s or
    /// when the window closes - the outcome unknown, not refused: the station may have done it). A refusal's reason, and
    /// the note on work granted only in part ("Too fast: 4 of those 6 strokes counted."), are shown by Send itself.
    /// Pending hand work (strokes, stitches, beats) is taken out of the area's buffers when it is sent, so it never goes
    /// twice; an area that keeps it for another try keeps it only on a real refusal
    /// (<c>!ok &amp;&amp; reason != CraftAnswerWaits.NoAnswer</c>).
    /// </summary>
    protected bool Send(CraftRequest r, System.Action<bool, string> answered = null) => ui.Send(r, answered);

    /// <summary>
    /// Inside an <c>answered</c> callback: the events the request's own operations raised (a blow's <see cref="CraftEventKind.Struck"/>
    /// with its sound, a solder's flash) - never the ones other work raised meanwhile. Empty anywhere else.
    /// </summary>
    protected IReadOnlyList<CraftEvent> AnswerEvents => ui.AnswerEvents;

    /// <summary>
    /// The latest line the workshop wrote in a piece's record - its own verdict ("Cleaved - it shattered") - for the words
    /// of an answer (read it in the <c>answered</c> callback of <see cref="Send"/>); "" when there is none.
    /// </summary>
    protected string LastRecord(string pieceId)
    {
        CraftPiece p = Station != null && Station.Workshop != null ? Ws.Piece(pieceId) : null;
        return p != null && p.record.Count > 0 ? p.record[p.record.Count - 1].text : "";
    }

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

    /// <summary>A menu of supplies (fuels, fibres, threads, tanning agents...) by id, each with how much of it the player has, set to the first one they have.</summary>
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

    /// <summary>
    /// The wood a shovel of <paramref name="fuelId"/> takes from the bag when any wood is burnt as it ("pine plank
    /// blank": the cheapest goes first), else null - the forge and the furnace say what went on the fire.
    /// </summary>
    protected string BurntAs(string fuelId) => Station != null && !string.IsNullOrEmpty(fuelId) ? Station.BurntAs(ui.Player, fuelId)?.ToLowerInvariant() : null;

    /// <summary>The item a supply, a fuel or a quenchant is taken from ("Raw Wool", "Dragon's Blood"), for telling the player what they lack.</summary>
    protected string ItemOf(string id)
    {
        CraftFuel f = Rules.Fuel(id);
        if (f != null) return string.IsNullOrEmpty(f.item) ? f.Name : f.item;
        CraftSupplyDefinition s = Rules.Supply(id);
        if (s != null) return string.IsNullOrEmpty(s.item) ? s.Name : s.item;
        CraftQuenchant q = Rules.Quenchant(id);
        return q != null ? string.IsNullOrEmpty(q.item) ? q.Name : q.item : id;
    }

    private List<string> CountedNames(List<string> ids, out int first)
    {
        first = -1;
        var names = new List<string>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            string name = Rules.Fuel(ids[i])?.Name ?? Rules.Supply(ids[i])?.Name ?? Rules.Agent(ids[i])?.Name ?? Rules.Quenchant(ids[i])?.Name ?? ids[i];
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
    /// <summary>What the hands have picked or aim at: the chosen tool, part or stretch, the point a blow will fall.</summary>
    protected static readonly Color Mark = new Color(1f, 0.8f, 0.38f, 0.9f);

    // The same colours mean the same in every area. A meter's bands (drawn under its fill, see Meter): where the work goes
    // well (the safe or ideal zone), where it goes only fairly (not yet, or barely), where it does harm (the danger zone),
    // where it is too cold or slow to take, where it does nothing yet. Fills, marks and words that judge: Good, Fair, Harm.
    // What the hands picked is Mark; the design's target is CraftArt.TargetColor; the value is Meter's white tick; heat glows
    // in CraftArt.Heat. A meter's name is written beside it in CraftArt.LabelInk, a place's name or a caption in LabelDim.
    /// <summary>A meter's band where the work goes well: the safe or ideal zone.</summary>
    protected static readonly Color GoodBand = new Color(0.25f, 0.5f, 0.28f, 1f);
    /// <summary>A meter's band where it goes only fairly: it barely takes, or not yet.</summary>
    protected static readonly Color FairBand = new Color(0.5f, 0.4f, 0.16f, 1f);
    /// <summary>A meter's band where harm is done: the danger zone.</summary>
    protected static readonly Color HarmBand = new Color(0.55f, 0.2f, 0.15f, 1f);
    /// <summary>A meter's band where it is too cold or too slow to take (a stream that freezes short, wood not yet hot through).</summary>
    protected static readonly Color ColdBand = new Color(0.25f, 0.35f, 0.5f, 1f);
    /// <summary>A meter's band where the work does nothing yet (a blow that only chips, a shaking too lazy to pass anything).</summary>
    protected static readonly Color IdleBand = new Color(0.3f, 0.3f, 0.32f, 1f);
    /// <summary>A fill, a mark or a word that says it went well (or is ready).</summary>
    protected static readonly Color Good = new Color(0.45f, 0.9f, 0.48f, 1f);
    /// <summary>A fill, a mark or a word that says it went fairly, or is close to harm.</summary>
    protected static readonly Color Fair = new Color(0.95f, 0.72f, 0.3f, 1f);
    /// <summary>A fill, a mark or a word that says harm was done (or will be).</summary>
    protected static readonly Color Harm = new Color(0.92f, 0.34f, 0.26f, 1f);

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

    /// <summary>
    /// A bar meter: <paramref name="value"/> (0-1) along its long side - it stands up and fills from the bottom when
    /// <paramref name="h"/> &gt; <paramref name="w"/>. <paramref name="bands"/> mark targets along it (from - to, 0-1: the safe
    /// zone in <see cref="GoodBand"/>, the danger zone in <see cref="HarmBand"/>), drawn under a slimmer fill, with a white
    /// tick at the value. Every meter is named by a label beside it (<see cref="CraftArt.Label"/>).
    /// </summary>
    protected static void Meter(CraftPixels c, float x, float y, float w, float h, float value, Color fill,
                                params (float from, float to, Color color)[] bands)
    {
        bool up = h > w;
        float v = Mathf.Clamp01(value), len = (up ? h : w) - 2f, thick = (up ? w : h) - 2f;
        c.Rect(x, y, x + w, y + h, new Color(0f, 0f, 0f, 0.6f));
        bool banded = bands != null && bands.Length > 0;
        if (banded)
            foreach ((float from, float to, Color color) b in bands)
            {
                float a = Mathf.Clamp01(Mathf.Min(b.from, b.to)) * len, e = Mathf.Clamp01(Mathf.Max(b.from, b.to)) * len;
                if (up) c.Rect(x + 1, y + 1 + a, x + w - 1, y + 1 + e, b.color);
                else c.Rect(x + 1 + a, y + 1, x + 1 + e, y + h - 1, b.color);
            }
        float inset = banded ? thick / 3f : 0f; // over bands the fill is slimmer, so the bands still show
        if (up) c.Rect(x + 1 + inset, y + 1, x + w - 1 - inset, y + 1 + len * v, fill);
        else c.Rect(x + 1, y + 1 + inset, x + 1 + len * v, y + h - 1 - inset, fill);
        if (!banded)
            return;
        if (up) c.Rect(x - 2, y + 1 + len * v - 1, x + w + 2, y + 1 + len * v + 1, Color.white);
        else c.Rect(x + 1 + len * v - 1, y - 2, x + 1 + len * v + 1, y + h + 2, Color.white);
    }

    protected static bool IsMetal(CraftPiece p) => p != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood);

    /// <summary>
    /// An area's last words about the piece (<paramref name="said"/>) outlive nothing: new words are taken as said where
    /// the piece is now (<paramref name="seen"/>, <paramref name="where"/> keep them); once it has gone somewhere else - from
    /// another area, the 3D work place, another player - what was said where it was is dropped. Call it before showing them.
    /// </summary>
    protected static void Forget(ref string said, ref string seen, ref CraftLocation where, CraftPiece p)
    {
        said ??= "";
        if (!ReferenceEquals(said, seen)) { seen = said; where = p != null ? p.location : CraftLocation.Air; }
        else if (said.Length > 0 && p != null && p.location != where) said = seen = "";
    }

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
