using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The assembly bench. Start an item of a design this station makes, fit each part into its place - the right kind, cut
/// to its pattern, cool, and it must go on: a guard's slot over the tang at the shoulder, a grip and a pommel along it,
/// with the tang coming through them and enough left to peen; a haft into the head's eye - then make each joint by hand,
/// as its kind is made:
/// <list type="bullet">
/// <item>holes on the beat: the pricking iron through leather (four a beat), a punch for a rivet's hole (one a beat), the
/// needle through cloth (it pierces as it sews); off the beat the holes wander;</item>
/// <item>stitches and turns on the beat, pulled as hard as the Pull says (a stitch is best at 60%, a wrap at 65%; past 90%
/// a stitch cuts the leather), the hands' tremble and strength in it; off the beat the tension is uneven;</item>
/// <item>the hammer - hold to raise it, let go to strike: a rivet is set tight by a blow in its green band (light through
/// leather, firmer through wood, firmest metal on metal; too hard crushes the leather, splits the wood, cracks a hard
/// rivet struck cold), a tang peened over its pommel into the good band (past it the head cracks), a guard's slot closed
/// onto the tang, a wedge driven home (past it the haft splits).</item>
/// </list>
/// The bench works on one item: the one picked in the list (a part of it picked works on it too; a loose piece picked is
/// offered for the place it fits). Each beat and each blow goes to the station at once and its answer shows; the work moves
/// on only when a step can go no further (the holes all punched: to stitching them; the joint made: to the next one).
/// Finish it, then try it - here, or later from the bag at a bench where such things are made - to know what it is.
/// </summary>
public sealed class CraftAssemblyMode : CraftMode
{
    // ------------------------------------------------------------------ the surface (pixels, y up)
    private const float SurfaceW = CraftStationUI.SurfaceWidth;
    private const float TitleY = 380f, PartsTop = 370f, PartsBottom = 256f;
    private const float JointsHeaderY = 241f, RowsTop = 236f, JointRow = 16f;
    private const int JointRows = 6;
    private const float WorkTop = 134f;
    private const float PlanJointsX = 380f; // nothing on the bench: the design's joints listed from here, its parts to the left
    /// <summary>The rhythm's track: the beats come from the right, a second of time is this many pixels, and they are answered on the line.</summary>
    private const float TrackX0 = 24f, TrackX1 = 696f, HitX = 360f, PixelsPerSecond = 240f, TrackY = 66f, TrackH = 22f;
    /// <summary>The hammer's blow meter and the joint's progress beside it.</summary>
    private const float BlowX = 24f, BlowW = 400f, BlowY = 66f, BlowH = 22f, GaugeX = 450f, GaugeW = 246f;
    /// <summary>Seconds after a blow before the hammer can be raised again (as the station's guard has it).</summary>
    private const float Recovery = 0.42f;
    /// <summary>Where the Pull starts (and starts again for work with another best pull): the hands set it, then keep it as they tire.</summary>
    private const float PullStart = 0.4f;
    private const float WordsSeconds = 5f, PromptSeconds = 6f;

    /// <summary>A way of working a joint: the work, the tool (the request's <c>i1</c>: 0 a punch or none, 1 the needle, 2 the pricking iron and awl), and its name.</summary>
    private struct Step
    {
        public CraftJointWork work;
        public int tool;
        public string label;
        public bool Hammer => work == CraftJointWork.Rivet || work == CraftJointWork.Peen || work == CraftJointWork.Wedge;
        public bool Rhythm => work == CraftJointWork.Punch || work == CraftJointWork.Stitch || work == CraftJointWork.Wrap;
        public bool Pulls => work == CraftJointWork.Stitch || work == CraftJointWork.Wrap;
        public bool Same(Step o) => work == o.work && tool == o.tool;
    }

    /// <summary>How a joint stands, as the surface shows it (worked out a few times a second).</summary>
    private struct JointView
    {
        public string id, name, status;
        public float progress, quality;
        public bool applies, jewel;
        /// <summary>A good band on the progress bar (0-1) and where it is overdone from (0: none).</summary>
        public float bandFrom, bandTo, over;
    }

    // ------------------------------------------------------------------ the controls
    private TMP_Dropdown category, group, design, slot, part, joint, work, bag;
    private Slider pull;
    private Button fitButton, offButton, finishButton, tryButton, bagButton;
    private TextMeshProUGUI info;

    private readonly List<CraftDesign> makeable = new List<CraftDesign>();
    private readonly List<string> categories = new List<string>(), groups = new List<string>(), designs = new List<string>();
    private readonly List<(string role, int index)> slots = new List<(string role, int index)>();
    /// <summary>The part menu's choices: piece ids, or supply ids for a supply's place.</summary>
    private readonly List<string> parts = new List<string>();
    private readonly List<string> joints = new List<string>();
    private readonly List<Step> steps = new List<Step>();
    private readonly List<ItemSO> bagItems = new List<ItemSO>();
    private readonly List<JointView> views = new List<JointView>();

    /// <summary>The item the bench works on (an assembly's piece id).</summary>
    private string target;
    private string menuSig, bagSig;
    /// <summary>The info row speaks of the place picked (else of the joint picked).</summary>
    private bool slotFocus;

    private readonly CraftRhythm rhythm = new CraftRhythm();
    private readonly CraftHammer hammer = new CraftHammer { fullCharge = 0.6f };
    private float lastBlow = -99f;
    /// <summary>The press under way began in the work area (its release strikes).</summary>
    private bool pressInWork;
    private bool rhythmShown;
    /// <summary>The last beat answered: where its beat was against the line, and when; how hard the hands really pulled it.</summary>
    private double lastOffset;
    private float lastAnswerAt = -99f, lastError = -1f, lastPull = -1f;
    /// <summary>The best pull of the work the Pull was last set for (0: none yet).</summary>
    private double pullFor;
    /// <summary>The last blow struck: how hard it landed (a share of a full blow), against which ideal, and when.</summary>
    private float lastForce = -1f, lastIdeal, lastBlowShown = -99f;
    private string words = "", prompt = "";
    private Color wordsColor = Color.white;
    private float wordsAt = -99f, promptAt = -99f;

    public override string Title => "Assemble";
    public override CraftDiscipline Discipline => TargetDesign != null ? CraftMastery.OfDesign(TargetDesign) : CraftDiscipline.Smithing;
    public override bool Available(CraftStation s) => s.Has(CraftTools.AssemblyBench) || s.Has(CraftTools.LeatherBench);
    public override string Hint => "Fit the parts, then work each joint under the item: click on the beat to punch, stitch or wind (the wheel sets the pull); hold and let go to strike. Right-click lowers the hammer.";
    // the hammer's blows (rivets, peening, a wedge), or the needle, the awl and the wrap
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, CurrentStep?.Hammer ?? false);

    // ------------------------------------------------------------------ the item worked on
    private CraftPiece Target
    {
        get
        {
            CraftPiece p = target != null && Station != null && Station.Workshop != null ? Ws.Piece(target) : null;
            return p != null && p.kind == CraftPieceKind.Assembly ? p : null;
        }
    }

    private CraftDesign TargetDesign
    {
        get
        {
            CraftPiece a = Target;
            return a != null ? Rules.Design(a.assembly.design) : null;
        }
    }

    /// <summary>The item a fitted part is in (null: it is not fitted).</summary>
    private CraftPiece OwnerOf(CraftPiece part)
    {
        if (part == null || part.location != CraftLocation.Assembled || Station?.Workshop == null)
            return null;
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Assembly)
                foreach (AssemblyPart ap in q.assembly.parts)
                    if (ap.piece == part.id) return q;
        return null;
    }

    /// <summary>
    /// The item the bench works on: the one picked in the list, or the one a picked part is in; else it keeps the one it
    /// had - and with none, the newest still being made (else the newest finished).
    /// </summary>
    private void ResolveTarget()
    {
        if (Station?.Workshop == null)
            return;
        CraftPiece s = Selected;
        if (s != null && s.kind == CraftPieceKind.Assembly)
        {
            SetTarget(s.id);
            return;
        }
        CraftPiece owner = OwnerOf(s);
        if (owner != null)
        {
            SetTarget(owner.id);
            return;
        }
        if (Target != null)
            return;
        CraftPiece best = null;
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Assembly && q.location != CraftLocation.Assembled && (best == null || !q.assembly.finished || best.assembly.finished))
                best = q;
        SetTarget(best?.id);
    }

    private void SetTarget(string id)
    {
        if (id == target && (id == null || Target != null))
            return;
        target = id;
        rhythm.Clear();
        hammer.Lower();
        pressInWork = false;
        menuSig = null;
        if (slot == null)
            return;
        Fill(true);
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        PickSlot(FirstEmptySlot(a, d, -1));
        int j = FirstJointToWork(a, d);
        if (j >= 0) PickJoint(j);
        slotFocus = a != null && !a.assembly.finished && FirstEmptySlot(a, d, -1) >= 0 && RequiredMissing(a, d);
        ComputeViews(a, d);
    }

    public override bool AcceptsFitted(CraftPiece part) => OwnerOf(part) != null;

    public override void SelectionChanging(string from, string to) => Leave();

    public override void SelectionChanged(string to)
    {
        if (Station?.Workshop == null || slot == null)
            return;
        CraftPiece p = to != null ? Ws.Piece(to) : null;
        if (p == null)
            return;
        if (p.kind == CraftPieceKind.Assembly)
        {
            SetTarget(p.id);
            return;
        }
        CraftPiece owner = OwnerOf(p);
        if (owner != null)
        {
            // a part of an item: work on that item, at the part's place
            SetTarget(owner.id);
            foreach (AssemblyPart ap in owner.assembly.parts)
                if (ap.piece == p.id) PickSlot(slots.IndexOf((ap.role, ap.index)));
            slotFocus = true;
            return;
        }
        // a loose piece: the item stays; the piece is offered for the first open place that takes it (else the first open
        // place of its kind, where the info row says why it will not go)
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        if (a == null || d == null || a.assembly.finished)
            return;
        int ofKind = -1, takes = -1;
        for (int k = 0; k < slots.Count && takes < 0; k++)
        {
            CraftComponentSpec c = d.Component(slots[k].role);
            if (c == null || c.supply || c.kind != p.kind) continue;
            if (c.kind != CraftPieceKind.Fasteners && Ws.Piece(a.assembly.PieceIn(c.role, slots[k].index)) != null) continue;
            if (ofKind < 0) ofKind = k;
            if (PartWhy(a, d, c, slots[k].index, p) == null) takes = k;
        }
        int pick = takes >= 0 ? takes : ofKind;
        if (pick < 0)
            return;
        PickSlot(pick);
        int i = parts.IndexOf(p.id);
        if (i >= 0) SetValue(part, i);
        slotFocus = true;
        RefreshInfo();
    }

    // ------------------------------------------------------------------ the controls
    public override void Build(RectTransform controls)
    {
        // Only what this station can make (its tools, and its list of designs when it has one), sorted into categories.
        makeable.Clear();
        categories.Clear();
        foreach (CraftDesign d in Rules.Designs)
            if (Station.Makes(d))
            {
                makeable.Add(d);
                CraftDesignMenu.Of(d, out string c, out _);
                if (!categories.Contains(c)) categories.Add(c);
            }
        categories.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

        RectTransform r0 = Row(controls);
        Text(r0, "Make", 13f, 40f);
        category = Fitting(Drop(r0, categories.Count > 0 ? categories : new List<string> { "Nothing is made here" }, 0, 118f));
        group = Fitting(Drop(r0, new List<string> { "All" }, 0, 160f));
        design = Fitting(Drop(r0, new List<string> { "-" }, 0, 0f));
        Btn(r0, "Start", StartItem, 72f, UITheme.ButtonStyle.Primary);
        category.onValueChanged.AddListener(_ => { group.SetValueWithoutNotify(0); FillGroups(); });
        group.onValueChanged.AddListener(_ => { design.SetValueWithoutNotify(0); FillDesigns(); });
        FillGroups();

        RectTransform r1 = Row(controls);
        Text(r1, "Place", 13f, 40f);
        slot = Fitting(Drop(r1, new List<string> { "-" }, 0, 220f));
        part = Fitting(Drop(r1, new List<string> { "-" }, 0, 0f));
        fitButton = Btn(r1, "Fit it", FitPart, 72f, UITheme.ButtonStyle.Primary);
        offButton = Btn(r1, "Take off", TakeOff, 84f, UITheme.ButtonStyle.Ghost);
        slot.onValueChanged.AddListener(_ => { slotFocus = true; FillParts(Target, true); RefreshInfo(); });
        part.onValueChanged.AddListener(_ => { slotFocus = true; RefreshInfo(); });

        RectTransform r2 = Row(controls);
        Text(r2, "Joint", 13f, 40f);
        joint = Fitting(Drop(r2, new List<string> { "-" }, 0, 250f));
        work = Fitting(Drop(r2, new List<string> { "-" }, 0, 170f));
        pull = Slide(r2, "Pull", 0f, 1f, PullStart, false, 34f, v => $"{v * 100f:0}%");
        joint.onValueChanged.AddListener(_ => { slotFocus = false; JointPicked(); RefreshInfo(); });
        work.onValueChanged.AddListener(_ => { slotFocus = false; StepPicked(); RefreshInfo(); });

        RectTransform r3 = Row(controls);
        finishButton = Btn(r3, "Finish", FinishItem, 84f, UITheme.ButtonStyle.Primary);
        tryButton = Btn(r3, "Try it", TryItem, 84f);
        Text(r3, "Untried, in your bag:", 13f, 150f);
        bag = Fitting(Drop(r3, new List<string> { "nothing untried" }, 0, 0f));
        bagButton = Btn(r3, "Try that", TryFromBag, 90f, UITheme.ButtonStyle.Ghost);
        info = InfoRow(controls, 48f);

        // the item it worked on before (the menus are new): a selected item, or a part of one, wins
        menuSig = bagSig = null;
        string had = target;
        target = null;
        if (had != null && Ws.Piece(had)?.kind == CraftPieceKind.Assembly) SetTarget(had);
        ResolveTarget();
        Fill(true);
        FillBag(true);
        Refresh();
    }

    private static int Rank(string c)
    {
        int i = Array.IndexOf(CraftDesignMenu.Order, c);
        return i >= 0 ? i : 100;
    }

    /// <summary>A menu whose words shrink to fit it (a long design or joint name stays whole).</summary>
    private static TMP_Dropdown Fitting(TMP_Dropdown d)
    {
        foreach (TMP_Text t in new[] { d.captionText, d.itemText })
        {
            if (t == null) continue;
            t.enableAutoSizing = true;
            t.fontSizeMin = 9f;
            t.fontSizeMax = 14f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }
        return d;
    }

    private static void SetOptions(TMP_Dropdown dd, List<string> options, int value)
    {
        dd.ClearOptions();
        dd.AddOptions(options.Count > 0 ? options : new List<string> { "-" });
        dd.SetValueWithoutNotify(Mathf.Clamp(value, 0, Math.Max(0, options.Count - 1)));
        dd.RefreshShownValue();
    }

    private static void SetValue(TMP_Dropdown dd, int value)
    {
        if (dd == null || value < 0 || value >= dd.options.Count) return;
        dd.SetValueWithoutNotify(value);
        dd.RefreshShownValue();
    }

    /// <summary>Updates the options' words in place (a count, a state) without touching the choice.</summary>
    private static void Relabel(TMP_Dropdown dd, List<string> texts)
    {
        if (dd == null || dd.options.Count != texts.Count)
            return;
        bool changed = false;
        for (int i = 0; i < texts.Count; i++)
            if (dd.options[i].text != texts[i])
            {
                dd.options[i].text = texts[i];
                changed = true;
            }
        if (changed) dd.RefreshShownValue();
    }

    private string CategoryShown => categories.Count > 0 ? categories[Mathf.Clamp(category.value, 0, categories.Count - 1)] : null;

    /// <summary>The groups of the category picked ("All" first), then its designs.</summary>
    private void FillGroups()
    {
        groups.Clear();
        groups.Add("All");
        string cat = CategoryShown;
        foreach (CraftDesign d in makeable)
        {
            CraftDesignMenu.Of(d, out string c, out string g);
            if (c == cat && !groups.Contains(g)) groups.Add(g);
        }
        SetOptions(group, groups, group.value);
        FillDesigns();
    }

    /// <summary>The designs of the group picked, with what they are where that helps ("Kettle hat · Head").</summary>
    private void FillDesigns()
    {
        designs.Clear();
        var names = new List<string>();
        string cat = CategoryShown;
        string grp = groups.Count > 0 ? groups[Mathf.Clamp(group.value, 0, groups.Count - 1)] : "All";
        foreach (CraftDesign d in makeable)
        {
            CraftDesignMenu.Of(d, out string c, out string g);
            if (c != cat || grp != "All" && g != grp) continue;
            designs.Add(d.id);
            names.Add(d.kind == CraftDesignKind.Armor && cat == CraftDesignMenu.Armour ? $"{d.Name} · {CraftDesignMenu.Slot(d.armorSlot)}"
                : grp == "All" ? $"{d.Name} · {g}" : d.Name);
        }
        if (designs.Count == 0) names.Add("Nothing is made here");
        SetOptions(design, names, design.value);
    }

    private CraftDesign DesignPicked => designs.Count > 0 ? Rules.Design(designs[Mathf.Clamp(design.value, 0, designs.Count - 1)]) : null;

    // ------------------------------------------------------------------ the menus of the item
    private (string role, int index) CurrentSlot => slots.Count > 0 ? slots[Mathf.Clamp(slot.value, 0, slots.Count - 1)] : (null, 0);
    private string CurrentPart => parts.Count > 0 ? parts[Mathf.Clamp(part.value, 0, parts.Count - 1)] : null;
    private string CurrentJoint => joints.Count > 0 ? joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)] : null;
    private Step? CurrentStep => steps.Count > 0 ? steps[Mathf.Clamp(work.value, 0, steps.Count - 1)] : (Step?)null;
    private CraftComponentSpec SlotSpec(CraftDesign d) => d != null && CurrentSlot.role != null ? d.Component(CurrentSlot.role) : null;
    private CraftJointSpec JointSpec(CraftDesign d) => d != null && CurrentJoint != null ? d.Joint(CurrentJoint) : null;

    /// <summary>Rebuilds the item's menus when what is on the bench changed (choices kept by what they name, not their place).</summary>
    private void Fill(bool force)
    {
        if (Station?.Workshop == null || slot == null)
            return;
        CraftPiece a = Target;
        string sig = Signature(a);
        if (!force && sig == menuSig)
            return;
        menuSig = sig;
        FillSlots(a);
        FillParts(a, false);
        FillJoints(a);
        FillSteps(a, false);
    }

    private string Signature(CraftPiece a)
    {
        var sb = new StringBuilder();
        if (a != null)
        {
            sb.Append(a.id).Append(a.assembly.finished ? 'F' : 'u');
            foreach (AssemblyPart ap in a.assembly.parts) sb.Append(ap.role).Append(ap.index).Append(ap.piece);
            foreach (CraftVariable v in a.assembly.supplies) sb.Append(v.name).Append(v.value.ToString("0.##"));
            CraftDesign d = Rules.Design(a.assembly.design);
            if (d != null)
                foreach (CraftJointSpec js in d.joints)
                {
                    // a step opens or closes as the joint goes: its holes, a pin set, the peened pommel
                    JointState j = a.assembly.Joint(js.id);
                    if (j != null) sb.Append(j.holes > 0 ? 'h' : '-').Append(j.peen > 0 ? 'p' : '-');
                }
        }
        sb.Append('|');
        // the loose pieces - and, for the place picked, why each would not go on (a part cools, a hole is drifted wider)
        CraftDesign pd = a != null ? Rules.Design(a.assembly.design) : null;
        CraftComponentSpec place = SlotSpec(pd);
        foreach (CraftPiece q in Ws.Pieces)
        {
            if (q.location != CraftLocation.Air || q.kind == CraftPieceKind.Assembly) continue;
            sb.Append(q.id);
            if (place != null && !place.supply && q.kind == place.kind)
                sb.Append(':').Append(PartWhy(a, pd, place, CurrentSlot.index, q) is string why ? ShortWhy(why) : "");
            sb.Append(',');
        }
        return sb.ToString();
    }

    private void FillSlots(CraftPiece a)
    {
        (string role, int index) was = CurrentSlot;
        slots.Clear();
        var names = new List<string>();
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        int keep = 0;
        if (d != null)
            foreach (CraftComponentSpec c in d.components)
            {
                int count = c.supply ? 1 : Math.Max(1, c.count);
                for (int i = 0; i < count; i++)
                {
                    if (c.role == was.role && i == was.index) keep = slots.Count;
                    slots.Add((c.role, i));
                    names.Add(SlotLabel(a, c, i));
                }
            }
        if (names.Count == 0) names.Add(a == null ? "No item on the bench" : "-");
        SetOptions(slot, names, keep);
    }

    private string SlotLabel(CraftPiece a, CraftComponentSpec c, int i)
    {
        string name = c.Name + (!c.supply && c.count > 1 ? $" {i + 1}" : "");
        if (c.supply)
            return $"{name}: {Math.Floor(a.assembly.Supply(c.role) + 1e-6):0} of {Math.Max(1, c.supplyCount)} added";
        CraftPiece p = Ws.Piece(a.assembly.PieceIn(c.role, i));
        if (p != null)
            return c.kind == CraftPieceKind.Fasteners ? $"{name}: {SetWords(p)}" : $"{name}: fitted";
        return c.optional ? $"{name} (optional)" : $"{name}: empty";
    }

    /// <summary>A set of fasteners on the item in words: its buckles, or its rivets set and those left to set ("3 set, 7 left").</summary>
    private static string SetWords(CraftPiece set) =>
        set.fasteners.kind == CraftFastenerKind.Buckle || set.fasteners.used <= 0 ? $"{set.fasteners.count} in the set" : $"{set.fasteners.used} set, {set.fasteners.Left} left";

    /// <summary>
    /// The part menu for the place picked: the loose pieces of its kind - those that would go on first, the rest saying why
    /// not - or, for a supply's place, the supplies of its kind with how many the player has.
    /// </summary>
    private void FillParts(CraftPiece a, bool newPlace)
    {
        string was = CurrentPart;
        parts.Clear();
        var names = new List<string>();
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        CraftComponentSpec c = SlotSpec(d);
        int keep = -1;
        if (c != null && c.supply)
        {
            foreach (CraftSupplyDefinition s in Rules.Supplies)
            {
                if (s.kind != c.supplyKind) continue;
                int have = Have(s.id);
                if (keep < 0 && have != 0) keep = parts.Count;
                parts.Add(s.id);
                names.Add(SupplyLabel(s));
            }
        }
        else if (c != null)
        {
            var fits = new List<(string id, string label)>();
            var not = new List<(string id, string label)>();
            int index = CurrentSlot.index;
            foreach (CraftPiece q in Ws.Pieces)
            {
                if (q.location != CraftLocation.Air || q.kind == CraftPieceKind.Assembly || q.kind != c.kind) continue;
                if (c.kind == CraftPieceKind.Fasteners && (c.fastener == CraftFastenerKind.Buckle) != (q.fasteners.kind == CraftFastenerKind.Buckle)) continue;
                string why = PartWhy(a, d, c, index, q);
                string label = PieceLabel(q);
                if (why == null) fits.Add((q.id, label));
                else not.Add((q.id, $"{label} - {ShortWhy(why)}"));
            }
            foreach ((string id, string label) x in fits) { parts.Add(x.id); names.Add(x.label); }
            foreach ((string id, string label) x in not) { parts.Add(x.id); names.Add(x.label); }
        }
        if (!newPlace && was != null && parts.Contains(was)) keep = parts.IndexOf(was);
        if (parts.Count == 0)
            names.Add(c == null ? "-" : c.supply ? "No such supply" : $"Nothing here for the {c.Name.ToLowerInvariant()}");
        SetOptions(part, names, Math.Max(0, keep));
    }

    private string SupplyLabel(CraftSupplyDefinition s)
    {
        int have = Have(s.id);
        return $"{s.Name} ({(have < 0 ? "free" : have == 0 ? "none" : have.ToString())}) · strength {s.strength:0.##}";
    }

    private string PieceLabel(CraftPiece q)
    {
        string name = CraftLook.Name(Rules, q, ui.Known);
        return q.kind == CraftPieceKind.Fasteners ? $"{name} ({q.fasteners.count})"
            : q.kind == CraftPieceKind.Metal || q.kind == CraftPieceKind.Wood ? $"{name} ({CraftText.Length(q.Length)})" : name;
    }

    /// <summary>The first words of a refusal, for a menu ("It will not go on").</summary>
    private static string ShortWhy(string why)
    {
        string s = why;
        int cut = s.IndexOf(':');
        if (cut > 0) s = s.Substring(0, cut);
        s = s.TrimEnd('.');
        return s.Length > 34 ? s.Substring(0, 33) + "..." : s;
    }

    /// <summary>
    /// Why <paramref name="q"/> would not go in that place (null: it would) - the workshop's own checks as the window can
    /// read them (the station has the last word).
    /// </summary>
    private string PartWhy(CraftPiece a, CraftDesign d, CraftComponentSpec c, int index, CraftPiece q)
    {
        if (a == null || d == null || c == null || q == null)
            return "There is nothing to fit.";
        if (a.assembly.finished)
            return "It is already finished.";
        if (q.kind != c.kind)
            return $"The {c.Name.ToLowerInvariant()} is not that.";
        if (c.kind == CraftPieceKind.Hide)
        {
            bool cloth = CraftTextiles.IsCloth(Rules, q);
            CraftFabric fabric = c.leather?.fabric ?? CraftFabric.Leather;
            if (fabric == CraftFabric.Leather && cloth) return $"The {c.Name.ToLowerInvariant()} is leather, not cloth.";
            if (fabric == CraftFabric.Cloth && !cloth) return $"The {c.Name.ToLowerInvariant()} is cut from cloth.";
            if (!string.IsNullOrEmpty(c.leather?.pattern) && q.hide.pattern != c.leather.pattern)
                return $"That was not cut with the {Rules.Pattern(c.leather.pattern)?.Name.ToLowerInvariant() ?? c.leather.pattern} pattern.";
        }
        if (c.kind == CraftPieceKind.Fasteners && q.fasteners.Left < 1)
            return "The set is empty.";
        CraftPiece there = Ws.Piece(a.assembly.PieceIn(c.role, index));
        bool adding = there != null && c.kind == CraftPieceKind.Fasteners;
        if (there != null && !adding)
            return $"The {c.Name.ToLowerInvariant()} is already in place.";
        if (adding && there.fasteners.kind != q.fasteners.kind)
            return $"The set fitted is {there.fasteners.kind.ToString().ToLowerInvariant()}s.";
        if (q.MaxTemperature > 200 && c.kind != CraftPieceKind.Fasteners)
            foreach (CraftComponentSpec o in d.components)
                if (o.kind == CraftPieceKind.Hide || o.kind == CraftPieceKind.Wood)
                    return "It is too hot: it would scorch the leather and wood.";
        if (!CraftJewelry.PartFits(Ws, a, d, c, q, out string jewel))
            return jewel;
        return adding ? null : CraftAssembly.WhyNotOn(Ws, a, d, c, index, q);
    }

    private void FillJoints(CraftPiece a)
    {
        string was = CurrentJoint;
        joints.Clear();
        var names = new List<string>();
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d != null)
            foreach (CraftJointSpec js in d.joints)
            {
                joints.Add(js.id);
                names.Add(JointLabel(a, d, js));
            }
        int keep = was != null ? joints.IndexOf(was) : -1;
        if (keep < 0) keep = Math.Max(0, FirstJointToWork(a, d));
        if (names.Count == 0) names.Add(a == null ? "No item on the bench" : "No joints: its parts are all it needs");
        SetOptions(joint, names, keep);
    }

    private string JointLabel(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        if (!CraftAssembly.JointApplies(Ws, a, d, js))
            return $"{js.Name} ({(OptionalLeftOff(d, js, a) ? "optional: no part" : "its parts are not in")})";
        if (CraftJewelry.IsJewelJoint(js.kind))
            return $"{js.Name} (jeweller's work)";
        JointState j = a.assembly.Joint(js.id);
        if (j == null)
            return js.Name;
        JointView v = default;
        Progress(a, d, js, j, ref v);
        return $"{js.Name}: {v.status}";
    }

    /// <summary>Is a part the joint joins missing, and optional (a grip wrap left off: the joint is not made)?</summary>
    private bool OptionalLeftOff(CraftDesign d, CraftJointSpec js, CraftPiece a)
    {
        foreach (string role in new[] { js.roleA, js.roleB })
        {
            CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
            if (c != null && !c.supply && c.optional && Ws.Piece(a.assembly.PieceIn(role)) == null) return true;
        }
        return false;
    }

    /// <summary>The ways of working the joint picked; the first one with work left in it is picked when <paramref name="first"/>.</summary>
    private void FillSteps(CraftPiece a, bool first)
    {
        Step? was = CurrentStep;
        steps.Clear();
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        CraftJointSpec js = JointSpec(d);
        if (js != null) steps.AddRange(StepsOf(a, d, js));
        var names = new List<string>();
        int keep = -1;
        for (int i = 0; i < steps.Count; i++)
        {
            names.Add(steps[i].label);
            if (!first && was.HasValue && steps[i].Same(was.Value)) keep = i;
        }
        if (keep < 0) keep = Math.Max(0, FirstDoableStep(a, d, js));
        if (names.Count == 0)
            names.Add(js == null ? "-" : CraftJewelry.IsJewelJoint(js.kind) ? "At the Jeweller area" : js.kind == CraftJointKind.Fit ? "Nothing to work" : "-");
        SetOptions(work, names, keep);
    }

    private List<Step> StepsOf(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        var list = new List<Step>();
        if (a == null || d == null || js == null || CraftJewelry.IsJewelJoint(js.kind))
            return list;
        bool cloth = CraftTextiles.ClothSeam(Ws, a, js); // a seam of cloth: the needle sews it, piercing as it goes
        void Sew()
        {
            if (cloth)
                list.Add(new Step { work = CraftJointWork.Stitch, tool = 1, label = "Sew (needle)" });
            else
            {
                list.Add(new Step { work = CraftJointWork.Punch, tool = 2, label = "Punch (pricking iron)" });
                list.Add(new Step { work = CraftJointWork.Stitch, tool = 2, label = "Stitch (awl)" });
            }
        }
        void Set(string label)
        {
            if (CraftAssembly.NeedsHoles(Ws, a, d, js))
                list.Add(new Step { work = CraftJointWork.Punch, tool = 0, label = "Punch a hole (punch)" });
            list.Add(new Step { work = CraftJointWork.Rivet, tool = 0, label = label });
        }
        switch (js.kind)
        {
            case CraftJointKind.Stitch: Sew(); break;
            case CraftJointKind.Rivet:
                Set(CraftAssembly.RivetPart(d, js)?.fastener == CraftFastenerKind.Stud ? "Set a stud (hammer)" : "Set a rivet (hammer)");
                break;
            case CraftJointKind.Buckle:
                if (CraftAssembly.RivetPart(d, js) != null) Set("Rivet it on (hammer)");
                else if (CraftAssembly.ThreadPart(d) != null) Sew();
                break;
            case CraftJointKind.Fit:
                if (Ws.Piece(a.assembly.PieceIn(js.roleB))?.kind == CraftPieceKind.Metal)
                    list.Add(new Step { work = CraftJointWork.Peen, label = "Close the slot (hammer)" });
                break;
            case CraftJointKind.Peen: list.Add(new Step { work = CraftJointWork.Peen, label = "Peen the tang (hammer)" }); break;
            case CraftJointKind.Wrap: list.Add(new Step { work = CraftJointWork.Wrap, label = "Wind it on" }); break;
            case CraftJointKind.Wedge: list.Add(new Step { work = CraftJointWork.Wedge, label = "Drive the wedge (hammer)" }); break;
            case CraftJointKind.Socket:
                // the head driven home first; then its pins, when fitted, through the socket and the shaft as they sit
                list.Add(new Step { work = CraftJointWork.Wedge, label = "Drive it on (hammer)" });
                if (CraftAssembly.RivetsOf(Ws, a, d, js) != null) Set(CraftAssembly.RivetsWanted(js, d) > 1 ? "Rivet it (hammer)" : "Pin it (hammer)");
                break;
        }
        return list;
    }

    // ------------------------------------------------------------------ what can be done now
    /// <summary>
    /// Why this step cannot be worked now (null: it can) - the workshop's own refusals, read here before the hands try: the
    /// item finished, a part not in, the padding not laid in, then the joint's own (<see cref="CraftAssembly.WhyNot"/>).
    /// </summary>
    private string Check(CraftPiece a, CraftDesign d, CraftJointSpec js, Step st)
    {
        if (a == null || d == null)
            return "Start an item first: choose a design above and press Start.";
        if (js == null)
            return "Pick a joint to work.";
        if (a.assembly.finished)
            return "It is already finished.";
        foreach (string role in new[] { js.roleA, js.roleB })
        {
            CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
            if (c == null) continue;
            if (!c.supply && a.assembly.PieceIn(role) == null)
                return $"Put the {c.Name.ToLowerInvariant()} in place first.";
            // quilting goes through the batting: it is laid in before the seam is sewn
            if (c.supply && c.supplyKind != CraftSupplyKind.Thread && (st.work == CraftJointWork.Punch || st.work == CraftJointWork.Stitch)
                && a.assembly.Supply(role) < Math.Max(1, c.supplyCount))
                return $"Lay the {c.Name.ToLowerInvariant()} in first: pick it as the place, and Fit it.";
        }
        int n = 1;
        string why = CraftAssembly.WhyNot(Ws, a, d, js, st.work, ref n);
        if (why != null && st.work == CraftJointWork.Stitch && st.tool == 1 && NeedlePierces(a, d, js))
            return null; // the needle makes its own hole as it sews
        return why;
    }

    /// <summary>The needle's next pull must pierce a hole first: thread in, the seam not sewn all along, no hole waiting, holes still to make.</summary>
    private bool NeedlePierces(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        JointState j = a.assembly.Joint(js.id);
        CraftComponentSpec thread = CraftAssembly.ThreadPart(d);
        if (j == null || thread == null || a.assembly.Supply(thread.role) <= 0)
            return false;
        int wantStitches = js.kind == CraftJointKind.Buckle ? CraftAssembly.StitchesPerBuckle * Math.Max(1, js.count) : Math.Max(1, js.count);
        return j.holes <= j.stitches && j.stitches < wantStitches && j.holes < CraftAssembly.HolesWanted(d, js);
    }

    /// <summary>The first step that has work left in it (else the first that can still go on, else the first).</summary>
    private int FirstDoableStep(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        if (a == null || d == null || js == null)
            return -1;
        for (int i = 0; i < steps.Count; i++)
            if (!StepSpent(a, d, js, steps[i])) return i;
        for (int i = 0; i < steps.Count; i++)
            if (Check(a, d, js, steps[i]) == null) return i;
        return steps.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Has a step done its part of the joint: it can go no further, or - a socketed head driven home with its pins still to
    /// set - what it is for is done (it could still be driven on, but the pins come next)?
    /// </summary>
    private bool StepSpent(CraftPiece a, CraftDesign d, CraftJointSpec js, Step st)
    {
        if (Check(a, d, js, st) != null)
            return true;
        JointState j = a.assembly.Joint(js.id);
        return st.work == CraftJointWork.Wedge && js.kind == CraftJointKind.Socket && j != null && j.wedge >= CraftAssembly.WedgeGoodLow
               && CraftAssembly.PinsWanted(Ws, a, d, js) > j.rivets;
    }

    /// <summary>Is the joint made as well as work makes it (a socket's fitted pins set too)?</summary>
    private bool Complete(CraftPiece a, CraftDesign d, CraftJointSpec js, JointState j) =>
        CraftAssembly.JointComplete(js, j, CraftAssembly.PinsWanted(Ws, a, d, js));

    /// <summary>Does the joint want work the bench can do now (it applies, it is not the jeweller's, not made as well as work makes it, and a step of it can go on)?</summary>
    private bool WantsWork(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        if (!CraftAssembly.JointApplies(Ws, a, d, js) || CraftJewelry.IsJewelJoint(js.kind))
            return false;
        JointState j = a.assembly.Joint(js.id);
        if (j == null || Complete(a, d, js, j))
            return false;
        foreach (Step st in StepsOf(a, d, js))
            if (Check(a, d, js, st) == null) return true;
        return false;
    }

    /// <summary>The joint to work first: the first that wants work, else the first not yet made, else the first.</summary>
    private int FirstJointToWork(CraftPiece a, CraftDesign d)
    {
        if (a == null || d == null || d.joints.Count == 0)
            return -1;
        for (int i = 0; i < d.joints.Count; i++)
            if (WantsWork(a, d, d.joints[i])) return i;
        for (int i = 0; i < d.joints.Count; i++)
        {
            JointState j = a.assembly.Joint(d.joints[i].id);
            if (CraftAssembly.JointApplies(Ws, a, d, d.joints[i]) && j != null && !j.done) return i;
        }
        return 0;
    }

    /// <summary>The next joint after <paramref name="from"/> that wants work (-1: none).</summary>
    private int NextJoint(CraftPiece a, CraftDesign d, string from)
    {
        int at = joints.IndexOf(from);
        for (int k = 1; k <= joints.Count; k++)
        {
            int i = (at + k) % joints.Count;
            CraftJointSpec js = d.Joint(joints[i]);
            if (js != null && js.id != from && WantsWork(a, d, js)) return i;
        }
        return -1;
    }

    /// <summary>The first place still empty after <paramref name="after"/> (a required one before an optional one; -1: none).</summary>
    private int FirstEmptySlot(CraftPiece a, CraftDesign d, int after)
    {
        if (a == null || d == null)
            return -1;
        int optional = -1;
        for (int pass = 0; pass < 2; pass++)
            for (int k = 0; k < slots.Count; k++)
            {
                int i = (after + 1 + k) % Math.Max(1, slots.Count);
                CraftComponentSpec c = d.Component(slots[i].role);
                if (c == null) continue;
                bool empty = c.supply ? CraftAssembly.SupplyRoom(d, a, c.role) > 0 : Ws.Piece(a.assembly.PieceIn(c.role, slots[i].index)) == null;
                if (!empty) continue;
                if (!c.optional) return i;
                if (optional < 0) optional = i;
            }
        return optional;
    }

    /// <summary>Is a required part (in any of its places) or supply still missing?</summary>
    private bool RequiredMissing(CraftPiece a, CraftDesign d)
    {
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional) continue;
            if (c.supply)
            {
                if (CraftAssembly.SupplyRoom(d, a, c.role) > 0) return true;
                continue;
            }
            for (int i = 0; i < Math.Max(1, c.count); i++)
                if (Ws.Piece(a.assembly.PieceIn(c.role, i)) == null) return true;
        }
        return false;
    }

    private void PickSlot(int k)
    {
        if (k < 0 || k >= slots.Count)
            return;
        SetValue(slot, k);
        FillParts(Target, true);
    }

    private void PickJoint(int k)
    {
        if (k < 0 || k >= joints.Count)
            return;
        SetValue(joint, k);
        JointPicked();
    }

    private void JointPicked()
    {
        FillSteps(Target, true);
        StepPicked();
    }

    private void StepPicked()
    {
        rhythm.Clear();
        hammer.Lower();
        pressInWork = false;
        // work with another best pull (a wrap after a seam) starts from the Pull's start again: the hands set it afresh
        Step? st = CurrentStep;
        if (st.HasValue && st.Value.Pulls && pull != null)
        {
            double ideal = BestPull(st.Value);
            if (pullFor > 0 && Math.Abs(ideal - pullFor) > 1e-6)
            {
                SetSlider(pull, PullStart);
                lastPull = -1f;
            }
            pullFor = ideal;
        }
    }

    /// <summary>The pull a step's work is best at: a stitch's, a wrap's.</summary>
    private static double BestPull(Step st) => st.work == CraftJointWork.Wrap ? CraftAssembly.WrapTensionIdeal : CraftAssembly.StitchTensionIdeal;

    // ------------------------------------------------------------------ starting, fitting, finishing, trying
    private void StartItem()
    {
        CraftDesign d = DesignPicked;
        if (d == null)
        {
            Prompt("Nothing is made at this station.");
            return;
        }
        // An empty one of it is on the bench already: work on that rather than start another.
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Assembly && q.location == CraftLocation.Air && q.assembly.design == d.id && !q.assembly.finished && Untouched(q))
            {
                ui.Select(q.id);
                Prompt($"An empty {d.Name.ToLowerInvariant()} is on the bench already: fit its parts.");
                return;
            }
        var before = new HashSet<string>();
        foreach (CraftPiece q in Ws.Pieces)
            if (q.kind == CraftPieceKind.Assembly) before.Add(q.id);
        string id = d.id;
        Send(new CraftRequest(CraftRequestKind.StartAssembly) { id = id }, (ok, _) =>
        {
            if (!ok || Station?.Workshop == null)
                return;
            foreach (CraftPiece q in Ws.Pieces)
                if (q.kind == CraftPieceKind.Assembly && !before.Contains(q.id) && q.assembly.design == id)
                {
                    ui.Select(q.id);
                    ui.Message($"Started {CraftText.A(d.Name)} {d.Name.ToLowerInvariant()}: fit its parts, then make its joints.");
                    return;
                }
        });
    }

    /// <summary>Nothing fitted, added or worked yet.</summary>
    private static bool Untouched(CraftPiece q)
    {
        if (q.assembly.parts.Count > 0)
            return false;
        foreach (CraftVariable v in q.assembly.supplies)
            if (v.value > 0) return false;
        foreach (JointState j in q.assembly.joints)
            if (j.holes > 0 || j.stitches > 0 || j.rivets > 0 || j.turns > 0 || j.peen > 0 || j.wedge > 0) return false;
        return true;
    }

    private void FitPart()
    {
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        slotFocus = true;
        if (a == null || d == null)
        {
            Prompt("Start an item first: choose a design above and press Start.");
            return;
        }
        CraftComponentSpec c = SlotSpec(d);
        if (c == null)
            return;
        if (a.assembly.finished)
        {
            Prompt("It is already finished.");
            return;
        }
        string asm = a.id;
        (string role, int index) s = CurrentSlot;
        if (c.supply)
        {
            int room = CraftAssembly.SupplyRoom(d, a, c.role);
            CraftSupplyDefinition sup = Rules.Supply(CurrentPart);
            if (room <= 0) { Prompt($"It has all the {c.Name.ToLowerInvariant()} it needs: what is added stays in."); return; }
            if (sup == null) { Prompt($"There is nothing to add as the {c.Name.ToLowerInvariant()}."); return; }
            int have = Have(sup.id);
            if (have == 0) { Prompt($"You have no {ItemOf(sup.id)}."); return; }
            if (have > 0 && have < room) { Prompt($"You have {have} {ItemOf(sup.id)}: {room} are needed."); return; }
            string supName = sup.Name.ToLowerInvariant(), what = c.Name.ToLowerInvariant();
            Send(new CraftRequest(CraftRequestKind.Attach, asm) { id = c.role, other = sup.id, count = room }, (ok, _) =>
            {
                if (ok) ui.Message($"{CraftText.Capital(supName)} added: the {what} is in.");
                AfterFitting(asm);
            });
            return;
        }
        CraftPiece q = Ws.Piece(CurrentPart);
        if (q == null)
        {
            Prompt($"Nothing on the station will do for the {c.Name.ToLowerInvariant()}.");
            return;
        }
        string why = PartWhy(a, d, c, s.index, q);
        if (why != null)
        {
            Prompt(why);
            return;
        }
        CraftPiece there = Ws.Piece(a.assembly.PieceIn(c.role, s.index));
        int had = there != null ? there.fasteners.Left : 0, adds = q.fasteners.Left;
        string name = c.Name.ToLowerInvariant();
        Send(new CraftRequest(CraftRequestKind.Attach, asm) { id = c.role, i0 = s.index, other = q.id }, (ok, _) =>
        {
            if (!ok)
                return;
            CraftPiece now = Ws.Piece(asm);
            CraftDesign nd = now != null ? Rules.Design(now.assembly.design) : null;
            if (there != null)
                ui.Message($"{adds} more in the set of {name}: {had + adds} to set now.");
            else
                ui.Message($"The {name} is on{FittedWords(now, nd, c)}.{SolderWarning(now, nd, c)}");
            AfterFitting(asm);
        });
    }

    /// <summary>After a part goes in: the place picked moves on to the next one still empty, or - all in - to the joints.</summary>
    private void AfterFitting(string asm)
    {
        CraftPiece a = Target;
        if (a == null || a.id != asm)
            return;
        Fill(true);
        CraftDesign d = TargetDesign;
        int next = FirstEmptySlot(a, d, slot.value);
        if (next >= 0 && RequiredMissing(a, d))
        {
            PickSlot(next);
            return;
        }
        int j = FirstJointToWork(a, d);
        if (j >= 0)
        {
            PickJoint(j);
            slotFocus = false;
        }
    }

    /// <summary>How a part sits once on: its gap on the tang, the haft in the eye.</summary>
    private string FittedWords(CraftPiece a, CraftDesign d, CraftComponentSpec c)
    {
        if (a == null || d == null)
            return "";
        foreach (CraftJointSpec js in d.joints)
        {
            if (js.roleB != c.role && js.roleA != c.role) continue;
            JointState j = a.assembly.Joint(js.id);
            if (j == null || !CraftAssembly.JointApplies(Ws, a, d, js)) continue;
            if (js.kind == CraftJointKind.Fit)
                return $": {GapWords(j.gap)}";
            if (js.kind == CraftJointKind.Wedge || js.kind == CraftJointKind.Socket)
                return ": " + CraftAssembly.HaftFitWords(js, j.gap); // what the wedge will take up, and what nothing will
        }
        return "";
    }

    /// <summary>A stone going into jewellery not yet soldered: the torch's heat will reach it (a warning, not a refusal).</summary>
    private string SolderWarning(CraftPiece a, CraftDesign d, CraftComponentSpec c)
    {
        if (a == null || d == null || c == null || c.kind != CraftPieceKind.Gem || d.kind != CraftDesignKind.Jewelry)
            return "";
        foreach (CraftJointSpec js in d.joints)
            if (js.kind == CraftJointKind.Solder && CraftAssembly.JointApplies(Ws, a, d, js) && !(a.assembly.Joint(js.id)?.done ?? false))
                return " Solder it first: the torch's heat reaches a stone already in the piece.";
        return "";
    }

    private void TakeOff()
    {
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        slotFocus = true;
        CraftComponentSpec c = SlotSpec(d);
        if (a == null || c == null)
            return;
        if (a.assembly.finished) { Prompt("It is already finished: its parts stay as they were judged."); return; }
        if (c.supply) { Prompt($"The {c.Name.ToLowerInvariant()} is in it: what was added cannot be taken back out."); return; }
        (string role, int index) s = CurrentSlot;
        if (Ws.Piece(a.assembly.PieceIn(c.role, s.index)) == null) { Prompt("Nothing is fitted there."); return; }
        string held = CraftAssembly.HeldOn(Ws, a, d, c);
        if (held != null) { Prompt(held); return; }
        // the joints it is in are undone with it, for good: asked first, as any work thrown away
        string undone = UndoneWords(a, d, c, Ws.Piece(a.assembly.PieceIn(c.role, s.index)));
        if (undone != null && !Confirm($"takeoff:{a.id}:{c.role}:{s.index}", $"Click Take off again: {undone}."))
            return;
        string name = c.Name.ToLowerInvariant();
        Send(new CraftRequest(CraftRequestKind.Detach, a.id) { id = c.role, i0 = s.index }, (ok, _) =>
        {
            if (ok) ui.Message($"The {name} is off: a piece of its own again (the joints it was in start over).");
        });
    }

    /// <summary>
    /// What taking a part (<paramref name="part"/>) off undoes (null: nothing - no joint it is in has work in it): the rivets
    /// drilled out (also every one set from a set of rivets taken off), the stitches cut, the peen, the wedge, the wrap, the
    /// solder and the setting, the leather or cloth marked where it was worked - as <see cref="CraftWorkshop.Detach"/>
    /// undoes them.
    /// </summary>
    private string UndoneWords(CraftPiece a, CraftDesign d, CraftComponentSpec c, CraftPiece part)
    {
        int rivets = 0, stitches = 0, holes = 0, turns = 0;
        bool peen = false, wedge = false, jewel = false;
        foreach (CraftJointSpec js in d.joints)
        {
            JointState j = a.assembly.Joint(js.id);
            if (j == null) continue;
            bool named = js.roleA == c.role || js.roleB == c.role;
            if (!named)
            {
                // a set of rivets: what was set from it is drilled out wherever it went
                if (c.kind == CraftPieceKind.Fasteners && j.rivets > 0 && CraftAssembly.RivetPart(d, js)?.role == c.role) rivets += j.rivets;
                continue;
            }
            rivets += j.rivets;
            stitches += j.stitches;
            holes += j.holes;
            turns += j.turns;
            peen |= j.peen > 0 || j.peenClosed > 0;
            wedge |= j.wedge > 0;
            jewel |= j.solder > 0 || j.pushes > 0;
        }
        var what = new List<string>();
        if (rivets > 0) what.Add($"{rivets} rivet{(rivets == 1 ? "" : "s")} drilled out and lost");
        if (stitches > 0) what.Add($"{stitches} stitch{(stitches == 1 ? "" : "es")} cut");
        else if (holes > 0 && rivets == 0) what.Add($"{holes} hole{(holes == 1 ? "" : "s")} punched for nothing");
        if (turns > 0) what.Add("the wrap unwound");
        if (peen) what.Add("the peening undone");
        if (wedge) what.Add("the head knocked off its haft");
        if (jewel) what.Add("the solder broken, the setting opened");
        if (what.Count > 0 && part != null && part.kind == CraftPieceKind.Hide)
            what.Add($"the {(CraftTextiles.IsCloth(Rules, part) ? "cloth" : "leather")} marked where it was worked");
        return what.Count == 0 ? null : "its joints are undone - " + string.Join(", ", what);
    }

    private void FinishItem()
    {
        CraftPiece a = Target;
        if (a == null) { Prompt("Start an item first: choose a design above and press Start."); return; }
        if (a.assembly.finished) { Prompt("It is already finished."); return; }
        List<string> missing = CraftAssembly.Missing(Ws, a);
        if (missing.Count > 0)
        {
            Prompt("Still to do: " + string.Join(" ", missing));
            return;
        }
        Send(new CraftRequest(CraftRequestKind.Finish, a.id), (ok, _) =>
        {
            if (!ok)
                return;
            // the rivets or studs left over in its set came off it: they lie on the bench
            string spare = "";
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated && Station?.Workshop != null && Ws.Piece(e.Piece) is CraftPiece set && set.kind == CraftPieceKind.Fasteners)
                    spare += $" The {set.fasteners.count} {set.fasteners.kind.ToString().ToLowerInvariant()}{(set.fasteners.count == 1 ? "" : "s")} left over are on the bench.";
            ui.Message("Finished. Try it to see what it is worth - or take it, and try it later from your bag at a bench where such things are made." + spare);
        });
    }

    private void TryItem()
    {
        CraftPiece a = Target;
        if (a == null || !a.assembly.finished) { Prompt("Only a finished item can be tried: finish it first."); return; }
        if (Station.IsInspected(a.id)) { Prompt("It has been tried: what it is shows on the right."); return; }
        Send(new CraftRequest(CraftRequestKind.Inspect, a.id));
    }

    private void TryFromBag()
    {
        if (bagItems.Count == 0) { Prompt("You carry nothing untried of what is made here: an item is tried at a bench where such things are made."); return; }
        ItemSO so = bagItems[Mathf.Clamp(bag.value, 0, bagItems.Count - 1)];
        Send(new CraftRequest(CraftRequestKind.InspectItem) { item = so }, (ok, _) => FillBag(true));
    }

    /// <summary>
    /// The finished items in the bag not yet tried that this bench can try - such things are made here (their numbers show
    /// once they are); the others wait for a bench where they are made.
    /// </summary>
    private void FillBag(bool force)
    {
        if (bag == null)
            return;
        bagItems.Clear();
        var names = new List<string>();
        int elsewhere = 0;
        InventoryManager inv = ui.Player != null ? InventoryManager.For(ui.Player) : null;
        if (inv != null)
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it?.itemScriptableObject;
                CraftedItemRecord rec = (so as ICraftedItem)?.Record;
                if (rec == null || rec.inspected || bagItems.Contains(so)) continue;
                // the station's own rule (InspectItem): a design it makes, or anything at a test station
                CraftDesign rd = Rules.Design(rec.designId);
                if (rd == null || !Station.IsTestStation && !Station.Makes(rd))
                {
                    elsewhere++;
                    continue;
                }
                bagItems.Add(so);
                names.Add(so.Name);
            }
        string sig = string.Join("|", names) + "|" + elsewhere;
        if (!force && sig == bagSig)
            return;
        bagSig = sig;
        SetOptions(bag, names.Count > 0 ? names : new List<string> { elsewhere > 0 ? "none made here" : "nothing untried" }, bag.value);
    }

    // ------------------------------------------------------------------ the hands at work
    public override void Tick(float dt)
    {
        rhythm.Tick(dt);
        hammer.Tick(dt);
        LiveDraw = hammer.Raised || rhythmShown || Time.unscaledTime - lastBlow < 0.5f;
    }

    public override void Leave()
    {
        hammer.Lower();
        pressInWork = false;
        LiveDraw = false;
    }

    public override void Pointer(CraftPointer phase, Vector2 p, bool right, float scroll)
    {
        if (phase == CraftPointer.Cancel)
        {
            // the right button puts the hammer down: no blow
            if (hammer.Raised) Say("You lower the hammer: no blow.", CraftArt.LabelDim);
            hammer.Lower();
            pressInWork = false;
            return;
        }
        if (phase == CraftPointer.Scroll)
        {
            Step? st = CurrentStep;
            if (st.HasValue && st.Value.Pulls && pull != null)
                SetSlider(pull, Mathf.Clamp01(pull.value + 0.05f * Mathf.Sign(scroll)));
            return;
        }
        if (phase == CraftPointer.Down)
        {
            pressInWork = false;
            if (p.y >= PartsBottom && p.y <= PartsTop) { ClickPart(p); return; }
            if (p.y >= RowsTop - JointRows * JointRow && p.y <= JointsHeaderY + 10f) { ClickJoint(p); return; }
            if (p.y < WorkTop)
            {
                pressInWork = true;
                WorkDown();
            }
            return;
        }
        if (phase == CraftPointer.Up && pressInWork)
        {
            pressInWork = false;
            WorkUp();
        }
        // Drag and Move: nothing - a beat is answered on its press, a blow struck on its release
    }

    private void ClickPart(Vector2 p)
    {
        CraftDesign d = TargetDesign;
        if (d == null || d.components.Count == 0)
            return;
        int n = d.components.Count;
        float gap = 6f, w = (SurfaceW - 24f - gap * (n - 1)) / n;
        int i = Mathf.Clamp(Mathf.FloorToInt((p.x - 12f) / (w + gap)), 0, n - 1);
        CraftComponentSpec c = d.components[i];
        int count = c.supply ? 1 : Math.Max(1, c.count), shown = Math.Min(count, 4);
        float x = 12f + i * (w + gap);
        int index = count > 1 ? Mathf.Clamp(Mathf.FloorToInt((p.x - x - 4f) / Mathf.Max(1f, (w - 8f) / shown)), 0, shown - 1) : 0;
        // a place past the four drawn: the first of them still empty
        if (count > shown && index == shown - 1)
            for (int k = shown - 1; k < count; k++)
                if (Target != null && Ws.Piece(Target.assembly.PieceIn(c.role, k)) == null) { index = k; break; }
        int at = slots.IndexOf((c.role, index));
        if (at < 0) at = slots.IndexOf((c.role, 0));
        PickSlot(at);
        slotFocus = true;
        RefreshInfo();
    }

    private void ClickJoint(Vector2 p)
    {
        if (views.Count == 0)
            return;
        int first = FirstRowShown();
        int row = Mathf.FloorToInt((RowsTop - p.y) / JointRow);
        if (row < 0 || row >= JointRows)
            return;
        int k = first + row;
        if (k < 0 || k >= joints.Count)
            return;
        PickJoint(k);
        slotFocus = false;
        RefreshInfo();
    }

    private int FirstRowShown() => Mathf.Clamp(joint.value - JointRows + 1, 0, Math.Max(0, joints.Count - JointRows));

    private void WorkDown()
    {
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        CraftJointSpec js = JointSpec(d);
        Step? st = CurrentStep;
        slotFocus = false;
        if (a == null || d == null) { Prompt("Start an item first: choose a design above and press Start."); return; }
        if (js == null || !st.HasValue)
        {
            Prompt(js != null && CraftJewelry.IsJewelJoint(js.kind) ? $"{js.Name}: jeweller's work, at the Jeweller area."
                : js != null && js.kind == CraftJointKind.Fit ? $"{js.Name}: the part fits as it was made - there is nothing to work."
                : "Pick a joint to work.");
            return;
        }
        string why = Check(a, d, js, st.Value);
        if (why != null)
        {
            Prompt(why);
            return;
        }
        if (st.Value.Hammer)
        {
            if (Time.unscaledTime - lastBlow < Recovery)
            {
                Say("The hammer is still coming up.", CraftArt.LabelDim);
                pressInWork = false;
                return;
            }
            hammer.Raise();
            LiveDraw = true;
            return;
        }
        Beat(a, d, js, st.Value);
    }

    /// <summary>A beat answered: this beat's hole, stitch or turn goes to the station now, as well as the hands timed and pulled it.</summary>
    private void Beat(CraftPiece a, CraftDesign d, CraftJointSpec js, Step st)
    {
        double before = rhythm.Pull * rhythm.Count;
        double err = rhythm.Answer(pull.value, Hands);
        if (err < 0)
        {
            Say("That beat is answered: wait for the next one.", CraftArt.LabelDim);
            return;
        }
        lastError = (float)err;
        lastOffset = rhythm.LastOffset;
        lastAnswerAt = Time.unscaledTime;
        // the pull the hands really gave this beat: the Pull, its tremble, less what a tired arm lacks (a punch pulls nothing)
        float tension = (float)CraftMath.Clamp01(rhythm.Pull * rhythm.Count - before), e = (float)err;
        lastPull = st.Pulls ? tension : -1f;
        string asm = a.id, jid = js.id;
        bool leather = !CraftTextiles.ClothSeam(Ws, a, js);
        var chain = new List<CraftRequest>();
        switch (st.work)
        {
            case CraftJointWork.Punch:
                chain.Add(Joint(asm, jid, CraftJointWork.Punch, st.tool, e, e * 0.7f));
                break;
            case CraftJointWork.Stitch:
            {
                CraftRequest stitch = Joint(asm, jid, CraftJointWork.Stitch, st.tool, tension, e);
                if (st.tool == 1)
                {
                    // the needle pierces the hole it needs as it sews - in the same request, so the stitch never waits on
                    // the pierce's answer (a window closed or the item taken meanwhile would lose it); a pull past the cut cuts
                    stitch.flag = true;
                    stitch.c = e;
                    stitch.d = e * 0.7f;
                    stitch.i2 = tension > CraftAssembly.StitchTensionCuts ? 1 : 0;
                }
                chain.Add(stitch);
                break;
            }
            case CraftJointWork.Wrap:
                chain.Add(Joint(asm, jid, CraftJointWork.Wrap, 0, tension, e));
                break;
        }
        string timing = TimingWords(err, rhythm.LastOffset);
        string pulled = st.work == CraftJointWork.Stitch ? StitchWords(tension, leather) : st.work == CraftJointWork.Wrap ? WrapWords(tension) : "";
        Color col = GradeColor(GradeOf(err));
        SendChain(chain, 0, ok =>
        {
            if (!ok)
                return;
            Say($"{timing}{pulled}: {ProgressWords(asm, jid, st)}.", col);
            AdvanceWork(asm, jid, st);
        });
    }

    private static CraftRequest Joint(string asm, string joint, CraftJointWork w, int tool, float a, float b) =>
        new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)w, count = 1, i1 = tool, a = a, b = b };

    /// <summary>Sends the requests one after another, each once the one before it was done (a refusal stops the rest: Send shows why).</summary>
    private void SendChain(List<CraftRequest> chain, int i, Action<bool> done)
    {
        if (i >= chain.Count)
        {
            done?.Invoke(chain.Count > 0);
            return;
        }
        Send(chain[i], (ok, _) =>
        {
            if (ok) SendChain(chain, i + 1, done);
            else done?.Invoke(false);
        });
    }

    /// <summary>The hammer comes down: its blow goes to the station, and what it did comes back in words.</summary>
    private void WorkUp()
    {
        if (!hammer.Raised)
            return;
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        CraftJointSpec js = JointSpec(d);
        Step? st = CurrentStep;
        string why = st.HasValue ? Check(a, d, js, st.Value) : "Pick a joint to work.";
        if (why != null || !st.Value.Hammer)
        {
            hammer.Lower();
            if (why != null) Prompt(why);
            return;
        }
        CraftBlow b = hammer.Release(0.5, 0, Hands);
        lastBlow = Time.unscaledTime;
        double ideal = CraftAssembly.IdealCharge(Ws, a, js, st.Value.work);
        double r = b.force / Math.Max(1e-3, ideal);
        // where it landed against the bands stays on the meter a moment
        lastForce = (float)b.force;
        lastIdeal = (float)ideal;
        lastBlowShown = Time.unscaledTime;
        string asm = a.id, jid = js.id;
        Step used = st.Value;
        string slip = b.slipped ? "The hammer glanced off. " : "";
        Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = jid, i0 = (int)used.work, count = 1, a = (float)(b.force * 4) }, (ok, _) =>
        {
            if (!ok)
                return;
            string said = "";
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.Joined && e.Id == jid && !string.IsNullOrEmpty(e.Text)) said = e.Text;
                else if (e.Kind == CraftEventKind.Note && e.Piece == asm && !string.IsNullOrEmpty(e.Text)) said = e.Text;
            Say(slip + (said.Length > 0 ? said : CraftAssembly.BlowWords(r) + "."), BlowColor(r));
            AdvanceWork(asm, jid, used);
        });
    }

    /// <summary>
    /// The work moves on by itself only when the step can go no further: the holes all punched, to stitching them (and back
    /// to punching when every hole is stitched and the seam is not yet sewn along); the joint made as well as the work
    /// makes it, to the next joint that wants work. Read from the state the answer brought, never before it.
    /// </summary>
    private void AdvanceWork(string asm, string jid, Step used)
    {
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        if (a == null || d == null || a.id != asm || CurrentJoint != jid)
            return; // the hands have moved on themselves
        Step? cur = CurrentStep;
        CraftJointSpec js = d.Joint(jid);
        JointState j = a.assembly.Joint(jid);
        if (js == null || j == null || !cur.HasValue || !cur.Value.Same(used))
            return;
        // a fit is never "complete": it is done with once its slot closes no further
        bool complete = Complete(a, d, js, j) || js.kind == CraftJointKind.Fit && Check(a, d, js, used) != null;
        if (!complete && !StepSpent(a, d, js, used))
            return; // it carries on
        if (!complete)
        {
            int k = FirstDoableStep(a, d, js);
            if (k >= 0 && !steps[k].Same(used) && Check(a, d, js, steps[k]) == null)
            {
                SetValue(work, k);
                StepPicked();
                ui.Message(StepTurn(js, used, steps[k]));
            }
            return;
        }
        int next = NextJoint(a, d, jid);
        if (next >= 0)
        {
            CraftJointSpec n = d.Joint(joints[next]);
            PickJoint(next);
            ui.Message($"{js.Name}: done. On to the {n.Name.ToLowerInvariant()}.");
        }
        else
            ui.Message(CraftAssembly.Missing(Ws, a).Count == 0 ? $"{js.Name}: done. Everything is made: Finish it." : $"{js.Name}: done.");
    }

    private static string StepTurn(CraftJointSpec js, Step from, Step to)
    {
        if (from.work == CraftJointWork.Punch && to.work == CraftJointWork.Stitch) return "The holes are punched: now stitch them, on the beat.";
        if (from.work == CraftJointWork.Punch && to.work == CraftJointWork.Rivet)
            return js.kind == CraftJointKind.Socket ? "The hole is through the socket and the shaft: now pin it with the hammer." : "The holes are punched: now set the rivets with the hammer.";
        if (from.work == CraftJointWork.Stitch && to.work == CraftJointWork.Punch) return "Every hole is stitched: punch more.";
        if (from.work == CraftJointWork.Rivet && to.work == CraftJointWork.Punch) return "Each hole has its rivet: punch the next.";
        if (from.work == CraftJointWork.Wedge && to.work == CraftJointWork.Punch) return "The head is home on the shaft: now punch the pin's hole through the socket and the shaft.";
        return $"{js.Name}: now {to.label.ToLowerInvariant()}.";
    }

    // ------------------------------------------------------------------ words
    private void Say(string text, Color col)
    {
        words = text ?? "";
        wordsColor = col;
        wordsAt = Time.unscaledTime;
    }

    /// <summary>Why something cannot be done now: in the info row (and on the surface), never as a message.</summary>
    private void Prompt(string text)
    {
        prompt = text ?? "";
        promptAt = Time.unscaledTime;
        Say(prompt, Fair);
        RefreshInfo();
    }

    private static CraftTimingGrade GradeOf(double err) =>
        err >= 1 ? CraftTimingGrade.Miss : err > 0.6 ? CraftTimingGrade.Poor : err > 0.3 ? CraftTimingGrade.Good : err > 0.12 ? CraftTimingGrade.Excellent : CraftTimingGrade.Perfect;

    /// <summary>A beat's timing in words: on it, or how far early or late (<paramref name="offset"/>: seconds, negative = before the beat).</summary>
    private static string TimingWords(double err, double offset)
    {
        string side = offset < 0 ? "early" : "late";
        return err >= 1 ? "Off the beat" : err > 0.6 ? $"Well {side}" : err > 0.3 ? $"A little {side}" : "On the beat";
    }

    private static string StitchWords(float tension, bool leather) =>
        tension > CraftAssembly.StitchTensionCuts ? (leather ? ", pulled too hard - it cuts the leather" : ", pulled too hard - it puckers the cloth")
        : tension < CraftAssembly.StitchTensionIdeal - 0.2 ? ", a loose stitch" : "";

    private static string WrapWords(float tension) =>
        tension > CraftAssembly.WrapTensionIdeal + 0.25 ? ", wound too tight" : tension < CraftAssembly.WrapTensionIdeal - 0.2 ? ", wound slack" : "";

    private static Color BlowColor(double r) =>
        r < CraftAssembly.TooLight || r > CraftAssembly.TooHard ? Harm : r < CraftAssembly.GoodLow || r > CraftAssembly.GoodHigh ? Fair : Good;

    /// <summary>Where the joint stands after a beat ("holes 8/16", "sewn 6/16", "10/10 turns").</summary>
    private string ProgressWords(string asm, string jid, Step st)
    {
        CraftPiece a = Ws.Piece(asm);
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        CraftJointSpec js = d?.Joint(jid);
        JointState j = a?.assembly.Joint(jid);
        if (js == null || j == null)
            return "done";
        int holes = CraftAssembly.HolesWanted(d, js);
        int stitches = js.kind == CraftJointKind.Buckle ? CraftAssembly.StitchesPerBuckle * Math.Max(1, js.count) : Math.Max(1, js.count);
        switch (st.work)
        {
            case CraftJointWork.Punch: return $"holes {j.holes}/{holes}";
            case CraftJointWork.Stitch: return $"sewn {j.stitches}/{stitches}";
            case CraftJointWork.Wrap: return $"{j.turns}/{Math.Max(1, js.count)} turns";
            default: return "";
        }
    }

    /// <summary>A fit's gap: measured with calipers (or at a test station), else as the hand feels it.</summary>
    private string GapWords(double gap)
    {
        if (gap < 0) return "too tight";
        if (Station.Reveals || Station.HasInstrument(CraftInstruments.Calipers)) return $"gap {gap:0.0} mm";
        return gap <= 0.05 ? "tight" : gap <= 0.3 ? "snug" : gap <= 0.8 ? "a little play" : gap <= 1.5 ? "loose" : "it rattles";
    }

    // ------------------------------------------------------------------ the joints as they stand
    /// <summary>A joint's progress, its good band and its state in a few words.</summary>
    private void Progress(CraftPiece a, CraftDesign d, CraftJointSpec js, JointState j, ref JointView v)
    {
        switch (js.kind)
        {
            case CraftJointKind.Fit:
                v.progress = (float)Math.Exp(-Math.Max(0, j.gap) / 0.8) * (j.gap < 0 ? 0 : 1);
                v.status = GapWords(j.gap) + (j.peenClosed > 0 ? ", closed on it" : "");
                break;
            case CraftJointKind.Peen:
                v.progress = (float)(j.peen / 2);
                v.bandFrom = (float)(CraftAssembly.PeenGoodLow / 2);
                v.bandTo = v.over = (float)(CraftAssembly.PeenGoodHigh / 2);
                v.status = j.peen <= 0 ? "not peened" : $"peened {j.peen:0.0} (good 1.0-1.4)";
                break;
            case CraftJointKind.Wedge:
            case CraftJointKind.Socket:
            {
                // driven home between the bands (past them the haft splits); a socket's pins once it is home
                bool socket = js.kind == CraftJointKind.Socket;
                v.progress = (float)(j.wedge / 2);
                v.bandFrom = (float)(CraftAssembly.WedgeGoodLow / 2);
                v.bandTo = v.over = (float)(CraftAssembly.WedgeGoodHigh / 2);
                v.status = j.wedge <= 0 ? socket ? "not on" : "not driven" : $"driven {j.wedge:0.0} (home {CraftAssembly.WedgeGoodLow:0.0}-{CraftAssembly.WedgeGoodHigh:0.0})";
                int pins = socket ? CraftAssembly.PinsWanted(Ws, a, d, js) : 0;
                if (pins > 0) v.status += $", pinned {j.rivets}/{pins}";
                if (j.wedge > 0 && CraftAssembly.PlayLeft(js, j) > CraftAssembly.PlayFree) v.status += socket ? ", loose on the shaft" : ", the head rattles";
                break;
            }
            case CraftJointKind.Rivet:
            {
                int want = CraftAssembly.RivetsWanted(js, d);
                v.progress = j.rivets / (float)want;
                v.status = $"{j.rivets}/{want} set" + (CraftAssembly.NeedsHoles(Ws, a, d, js) ? $", {j.holes} holes" : "");
                break;
            }
            case CraftJointKind.Buckle:
                if (CraftAssembly.RivetPart(d, js) != null)
                {
                    v.progress = j.rivets / (float)Math.Max(1, js.count);
                    v.status = $"{j.rivets}/{Math.Max(1, js.count)} riveted, {j.holes} holes";
                }
                else
                {
                    int want = CraftAssembly.StitchesPerBuckle * Math.Max(1, js.count);
                    v.progress = j.stitches / (float)want;
                    v.status = $"holes {j.holes}, sewn {j.stitches}/{want}";
                }
                break;
            case CraftJointKind.Stitch:
                v.progress = j.stitches / (float)Math.Max(1, js.count);
                v.status = $"holes {j.holes}, sewn {j.stitches}/{Math.Max(1, js.count)}";
                break;
            case CraftJointKind.Wrap:
                v.progress = j.turns / (float)Math.Max(1, js.count);
                v.status = $"{j.turns}/{Math.Max(1, js.count)} turns";
                break;
            default:
                v.progress = j.done ? 1f : 0f;
                v.status = j.done ? "done (jeweller)" : "jeweller's work";
                break;
        }
        if (j.damage > 0.05) v.status += ", damaged";
        v.progress = Mathf.Clamp01(v.progress);
    }

    private static bool Started(CraftJointSpec js, JointState j) =>
        js.kind == CraftJointKind.Fit || j.holes > 0 || j.stitches > 0 || j.rivets > 0 || j.turns > 0 || j.peen > 0 || j.wedge > 0 || j.solder > 0 || j.pushes > 0;

    /// <summary>
    /// Every joint as it stands: how far along (the bar's length), how well the work done on it so far is made (its colour:
    /// the evaluator's own rating of the work done, what is still to do left out), in words.
    /// </summary>
    private void ComputeViews(CraftPiece a, CraftDesign d)
    {
        views.Clear();
        if (a == null || d == null)
            return;
        var byRole = new Dictionary<string, List<CraftPiece>>();
        foreach (CraftComponentSpec c in d.components)
        {
            var l = new List<CraftPiece>();
            for (int i = 0; i < Math.Max(1, c.count); i++)
            {
                CraftPiece p = Ws.Piece(a.assembly.PieceIn(c.role, i));
                if (p != null) l.Add(p);
            }
            if (l.Count > 0) byRole[c.role] = l;
        }
        foreach (CraftJointSpec js in d.joints)
        {
            JointState j = a.assembly.Joint(js.id);
            var v = new JointView { id = js.id, name = js.Name, quality = -1f, applies = CraftAssembly.JointApplies(Ws, a, d, js), jewel = CraftJewelry.IsJewelJoint(js.kind) };
            if (j != null)
            {
                Progress(a, d, js, j, ref v);
                if (v.applies && Started(js, j))
                    v.quality = (float)CraftMath.Clamp01(CraftEvaluator.JointQuality(Rules, js, j, d, byRole, a.assembly, soFar: true));
            }
            if (!v.applies) v.status = OptionalLeftOff(d, js, a) ? "optional: no part" : "its parts are not in";
            views.Add(v);
        }
    }

    // ------------------------------------------------------------------ refreshing
    public override void Refresh()
    {
        if (info == null || Station?.Workshop == null)
            return;
        if (Target == null || Selected != null && (Selected.kind == CraftPieceKind.Assembly ? Selected.id != target : OwnerOf(Selected) is CraftPiece o && o.id != target))
            ResolveTarget();
        Fill(false);
        CraftPiece a = Target;
        CraftDesign d = TargetDesign;
        if (a != null && d != null)
        {
            var sl = new List<string>();
            foreach ((string role, int index) s in slots)
            {
                CraftComponentSpec c = d.Component(s.role);
                sl.Add(c != null ? SlotLabel(a, c, s.index) : s.role);
            }
            Relabel(slot, sl);
            var jl = new List<string>();
            foreach (string id in joints)
            {
                CraftJointSpec js = d.Joint(id);
                jl.Add(js != null ? JointLabel(a, d, js) : id);
            }
            Relabel(joint, jl);
            CraftComponentSpec spec = SlotSpec(d);
            if (spec != null && spec.supply)
            {
                var pl = new List<string>();
                foreach (string id in parts)
                {
                    CraftSupplyDefinition s = Rules.Supply(id);
                    pl.Add(s != null ? SupplyLabel(s) : id);
                }
                Relabel(part, pl);
            }
        }
        ComputeViews(a, d);
        FillBag(false);
        bool open = a != null && !a.assembly.finished;
        CraftComponentSpec cs = SlotSpec(d);
        fitButton.interactable = open && cs != null && parts.Count > 0;
        offButton.interactable = open && cs != null && !cs.supply && Ws.Piece(a.assembly.PieceIn(cs.role, CurrentSlot.index)) != null;
        finishButton.interactable = open;
        tryButton.interactable = a != null && a.assembly.finished && !Station.IsInspected(a.id);
        bagButton.interactable = bagItems.Count > 0;
        Step? st = CurrentStep;
        pull.interactable = st.HasValue && st.Value.Pulls;
        CraftJointSpec cj = JointSpec(d);
        rhythmShown = open && st.HasValue && st.Value.Rhythm && Check(a, d, cj, st.Value) == null;
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        if (info == null || Station?.Workshop == null)
            return;
        info.text = InfoText(Target, TargetDesign);
    }

    private string InfoText(CraftPiece a, CraftDesign d)
    {
        string head = prompt.Length > 0 && Time.unscaledTime - promptAt < PromptSeconds ? $"<color=#ffb070>{prompt}</color>  " : "";
        if (a == null || d == null)
        {
            if (makeable.Count == 0)
                return head + "Nothing is made at this station: its tools make no design.";
            CraftDesign p = DesignPicked;
            return head + (p != null ? $"{p.Name}: {p.description} Press Start to begin one - its parts are made at the other areas (or brought), then fitted and joined here." : "Choose what to make above and press Start.");
        }
        if (a.assembly.finished)
        {
            if (!Station.IsInspected(a.id))
                return head + "Finished. Try it to see what it is worth - or take it, and try it later from your bag at a bench where such things are made.";
            CraftItemReport r = CraftEvaluator.Item(Rules, Ws, a);
            return head + $"Finished and tried: {CraftEvaluator.KnownName(Rules, r, ui.Known)}, {r.Tier.ToLowerInvariant()} work (what it is shows on the right). Take it with Take.";
        }
        return head + (slotFocus ? SlotInfo(a, d) : JointInfo(a, d));
    }

    private string SlotInfo(CraftPiece a, CraftDesign d)
    {
        CraftComponentSpec c = SlotSpec(d);
        if (c == null)
            return MissingLine(a);
        (string role, int index) s = CurrentSlot;
        if (c.supply)
        {
            int room = CraftAssembly.SupplyRoom(d, a, c.role);
            CraftSupplyDefinition sup = Rules.Supply(CurrentPart);
            string text = room <= 0 ? $"{c.Name}: all of it is in (what is added stays in)." : $"{c.Name}: {room} to add - Fit it adds them from your bag.";
            if (c.supplyKind == CraftSupplyKind.Thread)
                text += " " + ThreadAdvice(a, d, sup?.strength ?? 0, sup?.Name);
            else if (sup != null)
                text += $" {sup.Name}: strength {sup.strength:0.##}.";
            return text;
        }
        CraftPiece there = Ws.Piece(a.assembly.PieceIn(c.role, s.index));
        if (there != null && c.kind != CraftPieceKind.Fasteners)
        {
            string held = CraftAssembly.HeldOn(Ws, a, d, c);
            return $"{c.Name}: {CraftLook.Name(Rules, there, ui.Known)} is on{FittedWords(a, d, c)}. {held ?? "Take it off to change it."}{TangLine(a, d, c)}";
        }
        CraftPiece cand = Ws.Piece(CurrentPart);
        string fasteners = there != null ? $" {CraftText.Capital(SetWords(there))}: another set adds to it." : "";
        if (cand == null)
            return $"{c.Name}{(c.optional ? " (optional: it can be left off)" : "")}: nothing on the station will do. {WhereMade(c)}{fasteners}";
        string why = PartWhy(a, d, c, s.index, cand);
        string verdict = why == null ? $"{c.Name}: {CraftLook.Name(Rules, cand, ui.Known)} will go on - Fit it." : $"{c.Name}: {why}";
        return verdict + fasteners + SolderWarning(a, d, c) + TangLine(a, d, c);
    }

    /// <summary>Where a missing part comes from.</summary>
    private string WhereMade(CraftComponentSpec c)
    {
        switch (c.kind)
        {
            case CraftPieceKind.Hide:
                return c.leather != null && !string.IsNullOrEmpty(c.leather.pattern) ? $"Cut it with the {Rules.Pattern(c.leather.pattern)?.Name.ToLowerInvariant() ?? c.leather.pattern} pattern." : "Cut it from leather.";
            case CraftPieceKind.Wood: return "Carve it from wood.";
            case CraftPieceKind.Fasteners: return c.fastener == CraftFastenerKind.Buckle ? "Buckles are cast or bought." : "Rivets and studs are made at the anvil, cast, or bought.";
            case CraftPieceKind.Gem: return "Cut and polish a stone at the lapidary's.";
            default: return "Forge or cast it.";
        }
    }

    /// <summary>A hilted item: the tang against what the hilt takes of it.</summary>
    private string TangLine(CraftPiece a, CraftDesign d, CraftComponentSpec c)
    {
        if (c == null || c.function != CraftComponentFunction.Edge && c.function != CraftComponentFunction.Guard && c.function != CraftComponentFunction.Grip && c.function != CraftComponentFunction.Counterweight)
            return "";
        if (!CraftAssembly.TangReport(Ws, a, out double tang, out double need))
            return "";
        string measure = Station.Reveals || Station.HasInstrument(CraftInstruments.Calipers) ? $"{tang:0} mm" : $"about {Math.Round(tang / 5) * 5:0} mm";
        return need > 0 ? $" The tang ({measure}) runs through {need:0} mm of grip and pommel, and {CraftAssembly.PeenAllowance:0} mm more is peened over." : $" The tang is {measure} long.";
    }

    /// <summary>The thread the seams want (the thickest leather or cloth sewn), against the thread chosen or added.</summary>
    private string ThreadAdvice(CraftPiece a, CraftDesign d, double strength, string name)
    {
        double thick = 0;
        bool cloth = false;
        foreach (CraftJointSpec js in d.joints)
            if (js.kind == CraftJointKind.Stitch || js.kind == CraftJointKind.Buckle && CraftAssembly.RivetPart(d, js) == null)
            {
                double t = CraftAssembly.SeamThickness(Ws, a, js, out bool isCloth);
                if (t <= thick) continue;
                thick = t;
                cloth = isCloth;
            }
        if (thick <= 0)
            return "";
        double need = CraftTextiles.ThreadNeed(thick);
        string s = $"The {(cloth ? "cloth" : "leather")} is {thick:0.#} mm: it wants thread of strength {need:0.##} or more";
        if (strength <= 0)
            return s + ".";
        string what = string.IsNullOrEmpty(name) ? "the thread" : name.ToLowerInvariant();
        return s + (strength >= need - 1e-6 ? $" - {what} ({strength:0.##}) suits it." : $" - {what} ({strength:0.##}) is weak for it: the seams will be weak.");
    }

    private string JointInfo(CraftPiece a, CraftDesign d)
    {
        CraftJointSpec js = JointSpec(d);
        if (js == null)
            return MissingLine(a);
        JointState j = a.assembly.Joint(js.id);
        if (j == null)
            return MissingLine(a);
        if (!CraftAssembly.JointApplies(Ws, a, d, js))
            return OptionalLeftOff(d, js, a) ? $"{js.Name}: its part is left off - it is optional, so there is nothing to make (fit one to make it). {MissingLine(a)}"
                : $"{js.Name}: fit its parts first. {MissingLine(a)}";
        if (CraftJewelry.IsJewelJoint(js.kind))
            return $"{js.Name}: jeweller's work - at the Jeweller area{(j.done ? " (done)" : "")}. {MissingLine(a)}";
        Step? st = CurrentStep;
        if (!st.HasValue)
            return js.kind == CraftJointKind.Fit ? $"{js.Name}: {GapWords(j.gap)} - it fits as it was made; there is nothing to work. {MissingLine(a)}" : MissingLine(a);
        string why = Check(a, d, js, st.Value);
        string how = HowTo(a, d, js, j, st.Value);
        return why != null ? $"{why} {how}" : how;
    }

    /// <summary>The rules of the work picked, and where the joint stands.</summary>
    private string HowTo(CraftPiece a, CraftDesign d, CraftJointSpec js, JointState j, Step st)
    {
        int holes = CraftAssembly.HolesWanted(d, js);
        int stitches = js.kind == CraftJointKind.Buckle ? CraftAssembly.StitchesPerBuckle * Math.Max(1, js.count) : Math.Max(1, js.count);
        int per = CraftMastery.StitchesPerAction(Mastery, MasterySettings);
        string pct = $"{(pull != null ? pull.value : 0.6f) * 100f:0}%";
        CraftComponentSpec thread = CraftAssembly.ThreadPart(d);
        double strength = thread != null ? a.assembly.Supply(thread.role + CraftWorkshop.StrengthSuffix) : 0;
        switch (st.work)
        {
            case CraftJointWork.Punch:
                return st.tool == 2
                    ? $"Punch on the beat: click under the joints as a beat crosses the line - the pricking iron makes {CraftMastery.HolesPerBeat} holes a beat; off the beat they wander. Holes {j.holes}/{holes}."
                    : $"Punch on the beat: one hole a beat for each rivet; off the beat it wanders. Holes {j.holes}/{holes}.";
            case CraftJointWork.Stitch:
                return (st.tool == 1
                           ? $"Sew on the beat: the needle pierces and draws {per} stitch{(per == 1 ? "" : "es")} a beat. Pull {pct}: best {CraftAssembly.StitchTensionIdeal * 100:0}%, over {CraftAssembly.StitchTensionCuts * 100:0}% puckers the cloth."
                           : $"Stitch on the beat: {per} stitch{(per == 1 ? "" : "es")} a beat of the awl. Pull {pct}: best {CraftAssembly.StitchTensionIdeal * 100:0}%, over {CraftAssembly.StitchTensionCuts * 100:0}% cuts the leather.")
                       + $" Sewn {j.stitches}/{stitches}. " + ThreadAdvice(a, d, strength, null);
            case CraftJointWork.Wrap:
                return $"Wind on the beat. Pull {pct}: best {CraftAssembly.WrapTensionIdeal * 100:0}% - slack slips, too tight bites in. Turns {j.turns}/{Math.Max(1, js.count)}.";
            case CraftJointWork.Rivet:
            {
                CraftPiece set = CraftAssembly.RivetsOf(Ws, a, d, js);
                string harm = HarmWords(a, d, js);
                return $"Hold to raise the hammer, let go to strike: a blow in the green band sets it tight{harm}. Set {j.rivets}/{CraftAssembly.RivetsWanted(js, d)}, {(set != null ? set.fasteners.Left : 0)} left in the set.";
            }
            case CraftJointWork.Peen:
                return js.kind == CraftJointKind.Fit
                    ? $"Close the slot onto the tang with lighter blows in the green band: {GapWords(j.gap)}; it closes only so far ({j.peenClosed:0.0} of {CraftAssembly.PeenCloseBudget:0.0} mm). A blow out of the band batters it, too hard a one dents the guard; a hard guard struck cold cracks."
                    : $"Peen the tang's end over the pommel with firm blows in the green band: spread {j.peen:0.0}, good from {CraftAssembly.PeenGoodLow:0.0} to {CraftAssembly.PeenGoodHigh:0.0} - past it the head cracks; a blow out of the band peens it ragged, too hard a one bends the tang.{TangLine(a, d, d.Component(js.roleA))}";
            case CraftJointWork.Wedge:
                return js.kind == CraftJointKind.Socket
                    ? $"Drive the head onto the shaft with blows in the green band: driven {j.wedge:0.0}, home from {CraftAssembly.WedgeGoodLow:0.0} to {CraftAssembly.WedgeGoodHigh:0.0} - past it the shaft splits; a blow too hard splits it at once.{(CraftAssembly.PinsWanted(Ws, a, d, js) > 0 ? " Once it is home, pin it." : "")}"
                    : $"Drive the wedge with blows in the green band: driven {j.wedge:0.0}, home from {CraftAssembly.WedgeGoodLow:0.0} to {CraftAssembly.WedgeGoodHigh:0.0} - past it the haft splits; a blow too hard splits it at once, a light one bounces off.";
            default:
                return "";
        }
    }

    /// <summary>What too hard a blow does to this joint.</summary>
    private string HarmWords(CraftPiece a, CraftDesign d, CraftJointSpec js)
    {
        bool leather = false, wood = false;
        foreach (string role in new[] { js.roleA, js.roleB })
        {
            CraftPiece p = string.IsNullOrEmpty(role) ? null : Ws.Piece(a.assembly.PieceIn(role));
            leather |= p != null && p.kind == CraftPieceKind.Hide;
            wood |= p != null && p.kind == CraftPieceKind.Wood;
        }
        return leather ? " - light through leather: too hard crushes it" : wood ? " - firmer through wood: too hard splits it" : " - metal on metal takes a firm blow; a hard rivet struck cold cracks";
    }

    /// <summary>What is still to do before it can be finished (or that it can be).</summary>
    private string MissingLine(CraftPiece a)
    {
        List<string> missing = CraftAssembly.Missing(Ws, a);
        return missing.Count == 0 ? "Everything is made: press Finish." : "Still to do: " + string.Join(" ", missing);
    }

    // ------------------------------------------------------------------ the surface
    public override void Draw(CraftPixels c)
    {
        if (Station?.Workshop == null)
            return;
        CraftPiece a = Target;
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (a == null || d == null)
        {
            DrawPlan(c);
            return;
        }
        string state = a.assembly.finished ? Station.IsInspected(a.id) ? "FINISHED AND TRIED" : "FINISHED - NOT YET TRIED" : "BEING MADE";
        CraftArt.Label(c, 12, TitleY, CraftArt.LabelFit($"{d.Name.ToUpperInvariant()} - {state}", SurfaceW - 24f), CraftArt.LabelInk);
        DrawParts(c, a, d);
        DrawJoints(c);
        c.Rect(8, WorkTop, SurfaceW - 8, WorkTop + 1, new Color(1f, 1f, 1f, 0.12f));
        DrawWork(c, a, d);
    }

    /// <summary>Nothing on the bench: what the design picked takes.</summary>
    private void DrawPlan(CraftPixels c)
    {
        CraftArt.Label(c, 16, TitleY, "NOTHING ON THE BENCH", CraftArt.LabelInk);
        CraftDesign p = DesignPicked;
        if (p == null)
        {
            CraftArt.Label(c, 16, TitleY - 22, makeable.Count == 0 ? "NOTHING IS MADE AT THIS STATION" : "CHOOSE A DESIGN ABOVE AND PRESS START", CraftArt.LabelDim);
            return;
        }
        CraftArt.Label(c, 16, TitleY - 22, CraftArt.LabelFit($"{CraftText.A(p.Name).ToUpperInvariant()} {p.Name.ToUpperInvariant()} TAKES:", SurfaceW - 32f), CraftArt.LabelDim);
        float y = TitleY - 46;
        foreach (CraftComponentSpec cs in p.components)
        {
            if (y < 150) break;
            string n = cs.supply ? $"{cs.Name} ×{Math.Max(1, cs.supplyCount)}" : cs.kind == CraftPieceKind.Fasteners ? $"{cs.Name} ×{cs.fastenerCount}" : cs.count > 1 ? $"{cs.Name} ×{cs.count}" : cs.Name;
            CraftArt.Label(c, 28, y, CraftArt.LabelFit(n.ToUpperInvariant() + (cs.optional ? " (OPTIONAL)" : ""), PlanJointsX - 12f - 28f), CraftArt.LabelInk);
            y -= 16;
        }
        y = TitleY - 46;
        foreach (CraftJointSpec js in p.joints)
        {
            if (y < 150) break;
            CraftArt.Label(c, PlanJointsX, y, CraftArt.LabelFit(js.Name.ToUpperInvariant(), SurfaceW - 16f - PlanJointsX), CraftArt.LabelDim);
            y -= 16;
        }
        CraftArt.Label(c, 16, 40, "PRESS START: THEN FIT ITS PARTS AND MAKE ITS JOINTS", CraftArt.LabelDim);
    }

    private void DrawParts(CraftPixels c, CraftPiece a, CraftDesign d)
    {
        int n = d.components.Count;
        if (n == 0)
            return;
        float gap = 6f, w = (SurfaceW - 24f - gap * (n - 1)) / n;
        (string role, int index) sel = CurrentSlot;
        float fit = w - 8f;
        for (int i = 0; i < n; i++)
        {
            CraftComponentSpec cs = d.components[i];
            float x = 12f + i * (w + gap);
            int count = cs.supply ? 1 : Math.Max(1, cs.count), have = 0;
            for (int k = 0; k < count && !cs.supply; k++)
                if (Ws.Piece(a.assembly.PieceIn(cs.role, k)) != null) have++;
            bool full = cs.supply ? CraftAssembly.SupplyRoom(d, a, cs.role) <= 0 : have >= count;
            Color back = full ? new Color(0.12f, 0.19f, 0.14f, 1f) : have > 0 ? new Color(0.18f, 0.17f, 0.12f, 1f) : new Color(0.11f, 0.11f, 0.14f, 1f);
            c.Rect(x, PartsBottom, x + w, PartsTop, back);
            bool picked = sel.role == cs.role;
            c.Frame(x, PartsBottom, x + w, PartsTop, picked ? Mark : new Color(1f, 1f, 1f, 0.18f), picked ? 2f : 1f);
            List<string> lines = CraftArt.LabelWrap(cs.Name.ToUpperInvariant(), fit, 2);
            for (int l = 0; l < lines.Count; l++)
                CraftArt.Label(c, x + 4, PartsTop - 14 - l * 13, lines[l], picked ? Mark : CraftArt.LabelInk);
            var area = new Rect(x + 4, PartsBottom + 18, w - 8, PartsTop - 18 - 13 * lines.Count - (PartsBottom + 18));
            if (cs.supply)
                DrawSupply(c, a, d, cs, area);
            else
                DrawPlaces(c, a, cs, count, area, picked ? sel.index : -1);
            string foot = cs.supply ? $"ADDED {Math.Floor(a.assembly.Supply(cs.role) + 1e-6):0}/{Math.Max(1, cs.supplyCount)}"
                : cs.kind == CraftPieceKind.Fasteners ? (have > 0 && Ws.Piece(a.assembly.PieceIn(cs.role)) is CraftPiece fs ? SetWords(fs).ToUpperInvariant() : cs.optional ? "OPTIONAL" : "EMPTY")
                : count > 1 ? $"{have}/{count} FITTED" : have > 0 ? "FITTED" : cs.optional ? "OPTIONAL" : "EMPTY";
            CraftArt.Label(c, x + 4, PartsBottom + 4, CraftArt.LabelFit(foot, fit), full ? Good : cs.optional && have == 0 ? CraftArt.LabelDim : Fair);
        }
    }

    /// <summary>The places of a part (up to four drawn side by side), each with its piece or empty.</summary>
    private void DrawPlaces(CraftPixels c, CraftPiece a, CraftComponentSpec cs, int count, Rect area, int picked)
    {
        if (area.width < 4 || area.height < 4)
            return;
        if (cs.kind == CraftPieceKind.Fasteners)
        {
            CraftPiece set = Ws.Piece(a.assembly.PieceIn(cs.role));
            DrawFasteners(c, set, cs, area);
            return;
        }
        int shown = Math.Min(count, 4);
        float w = area.width / shown;
        for (int k = 0; k < shown; k++)
        {
            var r = new Rect(area.x + k * w + 1, area.y, w - 2, area.height);
            CraftPiece p = Ws.Piece(a.assembly.PieceIn(cs.role, k));
            if (count > 1 && k == picked) c.Frame(r.xMin - 1, r.yMin - 1, r.xMax + 1, r.yMax + 1, new Color(1f, 0.8f, 0.38f, 0.6f), 1f);
            if (p == null)
            {
                c.Frame(r.xMin + 2, r.yMin + 2, r.xMax - 2, r.yMax - 2, new Color(1f, 1f, 1f, 0.1f), 1f);
                continue;
            }
            DrawPiece(c, p, r);
        }
        if (count > shown)
            CraftArt.Label(c, area.xMax - CraftArt.LabelWidth($"+{count - shown}"), area.yMax - 10, $"+{count - shown}", CraftArt.LabelDim);
    }

    private void DrawPiece(CraftPixels c, CraftPiece p, Rect r)
    {
        switch (p.kind)
        {
            case CraftPieceKind.Hide:
                CraftArt.DrawHide(c, Rules, p.hide, r);
                break;
            case CraftPieceKind.Gem:
                CraftArt.DrawGem(c, Rules, p, r);
                break;
            case CraftPieceKind.Assembly:
                CraftArt.Label(c, r.x + 2, r.center.y - 5, "ITEM", CraftArt.LabelDim);
                break;
            default:
            {
                if (p.segments.Count == 0)
                    break;
                float mmp = Fit(p, r.width, r.height);
                float len = (float)(p.Length / mmp);
                CraftArt.DrawTop(c, Rules, p, new Rect(r.x + (r.width - len) * 0.5f, r.y, len, r.height), mmp);
                break;
            }
        }
    }

    /// <summary>
    /// A set of fasteners: a head for each (up to what fits) - those set into the item first, peened over and ringed in
    /// green, then those still to set, then rings for any the design wants that the set lacks.
    /// </summary>
    private void DrawFasteners(CraftPixels c, CraftPiece set, CraftComponentSpec cs, Rect area)
    {
        int want = Math.Max(1, cs.fastenerCount), have = set != null ? set.fasteners.count : 0, used = set != null ? Math.Min(have, set.fasteners.used) : 0;
        Color metal = set != null ? CraftArt.MetalColor(Rules, set) : new Color(0.5f, 0.5f, 0.5f, 1f);
        bool buckle = cs.fastener == CraftFastenerKind.Buckle;
        float step = buckle ? 22f : 11f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt(area.width / step)), rows = Mathf.Max(1, Mathf.FloorToInt(area.height / step));
        int shown = Math.Min(Math.Max(have, want), perRow * rows);
        for (int k = 0; k < shown; k++)
        {
            float cx = area.x + step * 0.5f + k % perRow * step, cy = area.yMax - step * 0.5f - k / perRow * step;
            if (k < have)
            {
                if (buckle)
                {
                    c.Frame(cx - 8, cy - 6, cx + 8, cy + 6, metal, 2f);
                    c.Rect(cx - 1, cy - 6, cx + 1, cy + 6, metal);
                }
                else if (k < used)
                {
                    // set into the item: its head peened over
                    c.Disc(cx, cy, 4f, Good);
                    c.Disc(cx, cy, 3f, metal);
                    c.Disc(cx, cy, 1.2f, new Color(0f, 0f, 0f, 0.45f));
                }
                else
                    c.Disc(cx, cy, 3.5f, metal * new Color(0.8f, 0.8f, 0.8f, 1f));
            }
            else
                c.Ring(cx, cy, buckle ? 6f : 3.5f, 1f, new Color(1f, 1f, 1f, 0.15f));
        }
    }

    private void DrawSupply(CraftPixels c, CraftPiece a, CraftDesign d, CraftComponentSpec cs, Rect area)
    {
        float frac = (float)CraftMath.Clamp01(a.assembly.Supply(cs.role) / Math.Max(1, cs.supplyCount));
        float y = area.y + area.height * 0.5f - 6f;
        c.Rect(area.x, y, area.xMax, y + 12f, new Color(0f, 0f, 0f, 0.5f));
        c.Rect(area.x + 1, y + 1, area.x + 1 + (area.width - 2) * frac, y + 11f, cs.supplyKind == CraftSupplyKind.Thread ? new Color(0.85f, 0.8f, 0.65f, 1f) : new Color(0.7f, 0.62f, 0.5f, 1f));
        double strength = a.assembly.Supply(cs.role + CraftWorkshop.StrengthSuffix);
        if (strength > 0)
            CraftArt.Label(c, area.x, y - 16, CraftArt.LabelFit($"STRENGTH {strength:0.##}", area.width), CraftArt.LabelDim);
    }

    private void DrawJoints(CraftPixels c)
    {
        CraftArt.Label(c, 12, JointsHeaderY, views.Count == 0 ? "NO JOINTS: ITS PARTS ARE ALL IT NEEDS" : "JOINTS", CraftArt.LabelDim);
        if (views.Count == 0)
            return;
        int first = FirstRowShown(), sel = joint.value;
        for (int i = 0; i < JointRows && first + i < views.Count; i++)
        {
            JointView v = views[first + i];
            float yb = RowsTop - (i + 1) * JointRow, yt = yb + JointRow - 2;
            if (first + i == sel)
            {
                c.Rect(8, yb, SurfaceW - 8, yt, new Color(1f, 0.8f, 0.38f, 0.1f));
                c.Frame(8, yb, SurfaceW - 8, yt, Mark, 1f);
            }
            Color ink = !v.applies ? CraftArt.LabelDim : first + i == sel ? Mark : CraftArt.LabelInk;
            CraftArt.Label(c, 14, yb + 3, CraftArt.LabelFit(v.name.ToUpperInvariant(), 262f - 8f - 14f), ink);
            Color fill = v.quality < 0 ? new Color(0.5f, 0.5f, 0.55f, 1f) : v.quality >= 0.75f ? Good : v.quality >= 0.45f ? Fair : Harm;
            if (v.applies && !v.jewel)
            {
                if (v.bandTo > 0)
                    Meter(c, 262, yb + 3, 200, 9, v.progress, fill, (v.bandFrom, v.bandTo, GoodBand), (v.over, 1f, HarmBand));
                else
                    Meter(c, 262, yb + 3, 200, 9, v.progress, fill);
            }
            CraftArt.Label(c, 470, yb + 3, CraftArt.LabelFit(v.status.ToUpperInvariant(), SurfaceW - 12f - 470f), v.quality < 0 ? CraftArt.LabelDim : fill);
        }
        if (views.Count > JointRows)
            CraftArt.Label(c, SurfaceW - 12 - CraftArt.LabelWidth($"{first + 1}-{Math.Min(views.Count, first + JointRows)} OF {views.Count}"), JointsHeaderY,
                $"{first + 1}-{Math.Min(views.Count, first + JointRows)} OF {views.Count}", CraftArt.LabelDim);
    }

    /// <summary>The work area under the joints: the rhythm's track or the hammer's meter for the work picked, and the last result.</summary>
    private void DrawWork(CraftPixels c, CraftPiece a, CraftDesign d)
    {
        CraftJointSpec js = JointSpec(d);
        Step? st = CurrentStep;
        JointState j = js != null ? a.assembly.Joint(js.id) : null;
        string why = a.assembly.finished ? "IT IS FINISHED: TRY IT, OR TAKE IT" : js == null ? "PICK A JOINT TO WORK"
            : !st.HasValue ? CraftJewelry.IsJewelJoint(js.kind) ? "JEWELLER'S WORK: AT THE JEWELLER AREA" : "NOTHING TO WORK ON THIS JOINT"
            : Check(a, d, js, st.Value)?.ToUpperInvariant();
        bool live = why == null && st.HasValue && j != null;
        string head = why ?? (st.Value.Hammer ? $"{st.Value.label.ToUpperInvariant()}: HOLD HERE TO RAISE THE HAMMER, LET GO TO STRIKE" : $"{st.Value.label.ToUpperInvariant()}: CLICK HERE AS A BEAT CROSSES THE LINE");
        List<string> lines = CraftArt.LabelWrap(head, SurfaceW - 32f, 2);
        for (int l = 0; l < lines.Count; l++)
            CraftArt.Label(c, 16, WorkTop - 16 - l * 13, lines[l], why == null ? CraftArt.LabelInk : Fair);
        if (live)
        {
            if (st.Value.Hammer) DrawHammer(c, a, d, js, j, st.Value);
            else DrawRhythm(c, a, d, js, j, st.Value);
        }
        if (words.Length > 0 && Time.unscaledTime - wordsAt < WordsSeconds)
        {
            List<string> w = CraftArt.LabelWrap(words.ToUpperInvariant(), SurfaceW - 32f, 2);
            for (int l = 0; l < w.Count; l++)
                CraftArt.Label(c, 16, 18 - l * 13, w[l], wordsColor);
        }
    }

    private void DrawRhythm(CraftPixels c, CraftPiece a, CraftDesign d, CraftJointSpec js, JointState j, Step st)
    {
        float y0 = TrackY, y1 = TrackY + TrackH;
        c.Rect(TrackX0, y0, TrackX1, y1, new Color(0.14f, 0.13f, 0.13f, 1f));
        double interval = rhythm.interval, off = rhythm.Offset;
        float zone = (float)(rhythm.window * PixelsPerSecond), core = zone * 0.3f;
        for (int k = -3; k <= 4; k++)
        {
            // the beats come from the right and cross the line at their time
            float x = HitX + (float)((k * interval - off) * PixelsPerSecond);
            if (x + zone < TrackX0 || x - zone > TrackX1) continue;
            c.Rect(Mathf.Max(TrackX0, x - zone), y0 + 3, Mathf.Min(TrackX1, x + zone), y1 - 3, FairBand);
            c.Rect(Mathf.Max(TrackX0, x - core), y0 + 1, Mathf.Min(TrackX1, x + core), y1 - 1, GoodBand);
            if (x >= TrackX0 && x <= TrackX1)
                c.Rect(x - 1, y0 - 3, x + 1, y1 + 3, k == 0 && rhythm.Answered ? GradeColor(GradeOf(Math.Max(0, lastError))) : Color.white);
        }
        c.Rect(HitX - 1.5f, y0 - 8, HitX + 1.5f, y1 + 8, Mark);
        // where the last beat was when it was answered (to the right: early, to the left: late)
        if (lastError >= 0 && Time.unscaledTime - lastAnswerAt < 1.5f)
        {
            float lx = Mathf.Clamp(HitX - (float)(lastOffset * PixelsPerSecond), TrackX0, TrackX1);
            c.Disc(lx, y0 - 9, 4f, GradeColor(GradeOf(lastError)));
        }
        CraftArt.Label(c, HitX - 12 - CraftArt.LabelWidth("LATE"), y1 + 6, "LATE", CraftArt.LabelDim);
        CraftArt.Label(c, HitX + 14, y1 + 6, "EARLY", CraftArt.LabelDim);
        // how hard the hands pull (stitches, turns), against the best and where it cuts
        if (st.Pulls)
        {
            float ideal = (float)BestPull(st);
            float cuts = st.work == CraftJointWork.Wrap ? Mathf.Min(1f, ideal + 0.25f) : (float)CraftAssembly.StitchTensionCuts;
            Meter(c, TrackX0, 38, 240, 12, pull.value, new Color(0.85f, 0.85f, 0.9f, 1f), (ideal - 0.1f, ideal + 0.1f, GoodBand), (cuts, 1f, HarmBand));
            // what the last beat really pulled (the tremble in it, a tired arm's less): a tick to steer the Pull by
            bool recent = lastPull >= 0f && Time.unscaledTime - lastAnswerAt < 1.5f;
            if (recent)
            {
                float px = TrackX0 + 1 + 238f * Mathf.Clamp01(lastPull);
                bool good = Mathf.Abs(lastPull - ideal) <= 0.1f, harm = lastPull > cuts;
                c.Rect(px - 1.5f, 34, px + 1.5f, 54, harm ? Harm : good ? Good : Fair);
            }
            CraftArt.Label(c, TrackX0 + 250, 39, recent ? $"PULL {pull.value * 100f:0}%, PULLED {lastPull * 100f:0}% (BEST {ideal * 100f:0}%)" : $"PULL {pull.value * 100f:0}% (BEST {ideal * 100f:0}%)", CraftArt.LabelInk);
        }
        string progress = ProgressWords(a.id, js.id, st).ToUpperInvariant();
        if (progress.Length > 0)
            CraftArt.Label(c, TrackX1 - CraftArt.LabelWidth(progress), 39, progress, CraftArt.LabelInk);
    }

    private void DrawHammer(CraftPixels c, CraftPiece a, CraftDesign d, CraftJointSpec js, JointState j, Step st)
    {
        float ideal = (float)CraftAssembly.IdealCharge(Ws, a, js, st.work);
        float tl = ideal * (float)CraftAssembly.TooLight, gl = ideal * (float)CraftAssembly.GoodLow, gh = ideal * (float)CraftAssembly.GoodHigh, th = ideal * (float)CraftAssembly.TooHard;
        float charge = (float)hammer.Expected(Hands); // the blow as it would land: a tired arm's lighter, one held too long weaker
        Meter(c, BlowX, BlowY, BlowW, BlowH, charge, new Color(0.95f, 0.95f, 1f, 1f),
            (0f, tl, HarmBand), (tl, gl, FairBand), (gl, gh, GoodBand), (gh, th, FairBand), (th, 1f, HarmBand));
        // the last blow struck: where it landed against the bands (a tired arm's lands lighter than it was charged)
        bool landed = lastForce >= 0f && Time.unscaledTime - lastBlowShown < 1.5f && Mathf.Abs(lastIdeal - ideal) < 1e-4f;
        if (landed)
        {
            float bx = BlowX + 1 + (BlowW - 2) * Mathf.Clamp01(lastForce);
            c.Rect(bx - 2, BlowY - 4, bx + 2, BlowY + BlowH + 4, BlowColor(lastForce / Mathf.Max(1e-3f, ideal)));
        }
        CraftArt.Label(c, BlowX, BlowY + BlowH + 6, landed ? $"BLOW - THE LAST: {CraftAssembly.BlowWords(lastForce / Mathf.Max(1e-3f, ideal)).ToUpperInvariant()}" : "BLOW", CraftArt.LabelInk);
        BandLabel(c, "LIGHT", 0f, gl);
        BandLabel(c, "GOOD", gl, gh);
        if (th < 0.97f) BandLabel(c, "HARD", th, 1f);
        float since = Time.unscaledTime - lastBlow;
        string state = hammer.Raised ? "RAISED: LET GO TO STRIKE - RIGHT-CLICK LOWERS IT" : since < Recovery ? "THE HAMMER IS COMING UP" : "HOLD TO RAISE THE HAMMER";
        CraftArt.Label(c, BlowX, 40, state, hammer.Raised ? Mark : CraftArt.LabelDim);
        // the joint's own gauge
        string label;
        switch (st.work)
        {
            case CraftJointWork.Rivet:
            {
                int want = CraftAssembly.RivetsWanted(js, d);
                Meter(c, GaugeX, BlowY, GaugeW, BlowH, j.rivets / (float)want, j.rivets == 0 ? CraftArt.LabelDim : j.rivetQuality >= 0.75 ? Good : j.rivetQuality >= 0.45 ? Fair : Harm);
                CraftPiece set = CraftAssembly.RivetsOf(Ws, a, d, js);
                label = $"SET {j.rivets}/{want}, {(set != null ? set.fasteners.Left : 0)} LEFT";
                break;
            }
            case CraftJointWork.Peen when js.kind == CraftJointKind.Fit:
                Meter(c, GaugeX, BlowY, GaugeW, BlowH, (float)(j.peenClosed / CraftAssembly.PeenCloseBudget), new Color(0.7f, 0.75f, 0.85f, 1f));
                label = $"CLOSED: {GapWords(j.gap).ToUpperInvariant()}";
                break;
            case CraftJointWork.Peen:
                Meter(c, GaugeX, BlowY, GaugeW, BlowH, (float)(j.peen / 2), Good,
                    ((float)(CraftAssembly.PeenGoodLow / 2), (float)(CraftAssembly.PeenGoodHigh / 2), GoodBand), ((float)(CraftAssembly.PeenGoodHigh / 2), 1f, HarmBand));
                label = $"PEENED {j.peen:0.0} (GOOD 1.0-1.4)";
                break;
            default:
            {
                // a wedge or a socketed head: home between the bands, split past them (one rule for both)
                Meter(c, GaugeX, BlowY, GaugeW, BlowH, (float)(j.wedge / 2), Good,
                    ((float)(CraftAssembly.WedgeGoodLow / 2), (float)(CraftAssembly.WedgeGoodHigh / 2), GoodBand), ((float)(CraftAssembly.WedgeGoodHigh / 2), 1f, HarmBand));
                label = $"DRIVEN {j.wedge:0.0} (HOME {CraftAssembly.WedgeGoodLow:0.0}-{CraftAssembly.WedgeGoodHigh:0.0})";
                break;
            }
        }
        CraftArt.Label(c, GaugeX, BlowY + BlowH + 6, CraftArt.LabelFit(label, GaugeW), CraftArt.LabelInk);
    }

    /// <summary>A word under the blow meter, centred on a band (from - to: share of a full blow).</summary>
    private static void BandLabel(CraftPixels c, string text, float from, float to)
    {
        float mid = BlowX + 1 + (BlowW - 2) * (Mathf.Clamp01(from) + Mathf.Clamp01(to)) * 0.5f;
        float x = Mathf.Clamp(mid - CraftArt.LabelWidth(text) * 0.5f, BlowX, BlowX + BlowW - CraftArt.LabelWidth(text));
        CraftArt.Label(c, x, BlowY - 14, text, CraftArt.LabelDim);
    }
}
