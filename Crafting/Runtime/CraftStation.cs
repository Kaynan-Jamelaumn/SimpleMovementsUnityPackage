using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The network side of a crafting station (<c>GameNet.NetCraftStation</c>; null offline, where the station runs
/// everything itself). The server runs the workshop; a client keeps a copy that follows it and sends what its player
/// does as requests.
/// </summary>
public interface ICraftStationNet
{
    /// <summary>True where the station's rules run (the server).</summary>
    bool RunsStationRules { get; }
    /// <summary>Client: sends what the player did to the server (its <see cref="CraftRequest.seq"/> asks for an answer: <see cref="CraftStation.Answer"/>).</summary>
    void SendRequest(CraftRequest request);
    /// <summary>Client: the player starts (true) or stops working here (the window opened or closed).</summary>
    void SendTending(bool tending);
    /// <summary>Server: a message for a remote player.</summary>
    void Tell(CombatEntity player, string text);
    /// <summary>Server: opens the station's window on a remote player's screen; false when the player cannot be reached.</summary>
    bool Open(CombatEntity player);
}

/// <summary>
/// A smithing, smelting, tanning or leatherworking workplace: a <see cref="CraftWorkshop"/> with the tools the station
/// has (forge, anvil, quench tank, oven, grinder, files, whetstones, ore bench, furnace, crucible, fleshing beam, vats,
/// frame, drying rack, leather bench, assembly bench...) where players bring materials, work them by hand and take out
/// what they made.
/// <list type="bullet">
/// <item><b>Rules</b>: the workshop is run by the station's Crafting Database in fixed steps, whatever the frame rate -
/// also while nobody is there (Run While Away): a bar left in the fire keeps heating, a hide left in the vat keeps
/// tanning.</item>
/// <item><b>Inventory</b>: stock (bars, ore, hides, blanks, fasteners) and worked pieces come from the player's
/// inventory and become pieces of the workshop; pieces go back as worked-piece items, finished assemblies as weapons,
/// tools and armour. Fuel, flux, agents, thread, padding, oil, wax and dye are used up from the inventory (or free).</item>
/// <item><b>Normal or test station</b>: a normal station shows only what can be seen, heard and felt, and its
/// instruments (a thermometer, calipers, a balance, a loupe, a magnet, a test file, an assay kit, a streak plate) only
/// if it has them. A <b>test station</b> has every tool and instrument, free supplies of everything, shows the
/// numbers behind everything and lets time be skipped - for designing content and checking the rules.</item>
/// <item><b>Knowledge</b>: the player working the station learns (<see cref="CraftKnowledge"/>): metals they work
/// often, heat colours they read with a thermometer, what their quenches did, insights from mistakes.</item>
/// <item><b>Presentation</b> stays out of here: <see cref="CraftStationUI"/> (the window and the minigames) and
/// <see cref="CraftStationVisuals"/> (fire glow, sparks, steam, sounds) read the state and listen to <see cref="WorkshopEvent"/>.</item>
/// <item><b>Network sessions</b>: with a NetworkObject and a <c>NetCraftStation</c>, the server runs the workshop and
/// decides everything (materials taken, items given, what is learnt); every client keeps a copy that follows the
/// server's (<see cref="IsMirror"/>) so the fire, the sparks and the work show the same everywhere, and its player's
/// actions go to the server as requests, each answered (<see cref="Answered"/>) once the copy shows what it did. Without
/// one, only the server's (the host's) player can work here.</item>
/// <item><b>Real hands</b>: every request of every player - offline, the host's, a client's - is bounded by that
/// player's <see cref="CraftRequestGuard"/> (no faster than real hands, never steadier than their practice; what the
/// station then refuses is given back), except at a test station, where nobody's hands are and only a player on another
/// machine is held to the pace of requests. Every request's shape is checked at every station.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Craft Station")]
public class CraftStation : MonoBehaviour, IInteractable
{
    [Header("Station")]
    [Tooltip("Name in the prompt and the window.")]
    [SerializeField] private string stationName = "Smithy";
    [Tooltip("The rules. Empty = the project's Crafting Database (or the built-in rules).")]
    [SerializeField] private CraftDatabase database;
    [Tooltip("What the station has.")]
    [SerializeField] private CraftTools tools = CraftTools.Smithy;
    [Tooltip("The designs made here (Crafting Database ids). Empty = every design its tools can make.")]
    [SerializeField] private List<string> designs = new List<string>();
    [Tooltip("Where the prompt is shown and the range measured from (empty = this object).")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("How close the player must be to use it (metres).")]
    [SerializeField, Min(0.5f)] private float range = 3f;

    [Header("Test station")]
    [Tooltip("A test station: every tool and instrument, free supplies of every material, the numbers behind everything " +
             "(temperatures, phases, hardness, composition, the evaluation), items come out already tried, and time can " +
             "be skipped. Off (a real workshop): only what can be seen, heard and felt, the station's own instruments, the " +
             "player's own materials.")]
    [SerializeField] private bool testStation;

    [Header("Instruments")]
    [Tooltip("The measuring tools at hand (a test station has all of them).")]
    [SerializeField] private CraftInstruments instruments = CraftInstruments.Magnet | CraftInstruments.TestFile;

    [Header("Equipment")]
    [Tooltip("What the quench tank holds (a Crafting Database quenchant id).")]
    [SerializeField] private string quenchMedium = "water";
    [Tooltip("Quenchants the tank can be refilled with here (ids; empty = only the one above).")]
    [SerializeField] private List<string> quenchants = new List<string>();
    [Tooltip("How many vats a tannery has.")]
    [SerializeField, Range(0, 6)] private int vats = 3;
    [Tooltip("Free fuel at hand: a woodpile and a charcoal heap (else players need fuel items).")]
    [SerializeField] private bool freeFuel = true;
    [Tooltip("Water at hand (free water for vats).")]
    [SerializeField] private bool waterSource = true;
    [Tooltip("How good the station's tools are (1 ordinary; 0.5 worn and blunt; 1.5 a master's): a sharp drawknife and saw " +
             "cut cleaner, fine cards and combs work faster, a true faceting machine holds its angle, a good lap polishes faster.")]
    [SerializeField, Range(0.3f, 2f)] private float toolQuality = 1f;

    [Header("Supplies")]
    [Tooltip("Items the station gives for free (a teaching smithy).")]
    [SerializeField] private List<ItemSO> freeSupplies = new List<ItemSO>();
    [Tooltip("Where items that do not fit in the inventory are dropped (empty = in front of the station).")]
    [SerializeField] private Transform dropPoint;

    [Header("Simulation")]
    [Tooltip("The workshop goes on while nobody is there (off = time stops for it when the window closes).")]
    [SerializeField] private bool runWhileAway = true;
    [Tooltip("Seed of the workshop's flaws and variations.")]
    [SerializeField] private int seed = 1;
    [Tooltip("Seconds between readings of the surroundings (weather, time of day, condition sources).")]
    [SerializeField, Min(0.05f)] private float conditionsInterval = 0.5f;
    [Tooltip("Most simulation steps run in one frame (after a hitch the workshop catches up over several frames).")]
    [SerializeField, Min(1)] private int maxStepsPerFrame = 300;
    [Tooltip("Operations kept in memory for replays (older ones are forgotten; the state is kept).")]
    [SerializeField, Min(50)] private int historyKept = 600;

    [Header("Saving")]
    [Tooltip("Save the workshop (persistentDataPath/Crafting/Stations) and continue it after a restart.")]
    [SerializeField] private bool persistWorkshop = true;
    [Tooltip("Unique id of this station in the project (filled automatically).")]
    [SerializeField] private string stationId = "";

    [Header("Debug")]
    [SerializeField] private bool logEvents;

    private static readonly List<CraftStation> all = new List<CraftStation>();

    private CraftWorkshop workshop;
    private CraftRules rules;
    private CombatEntity tender;
    private readonly AlchemyConditionSample conditions = new AlchemyConditionSample();
    private readonly List<IAlchemyConditionSource> sources = new List<IAlchemyConditionSource>();
    private float conditionsTimer, readTimer;
    private int version, freeLump;
    private readonly HashSet<string> inspected = new HashSet<string>();
    /// <summary>Items tried while their maker was not in the world: their journal notes it when they take it (the server's own).</summary>
    private readonly HashSet<string> untold = new HashSet<string>();
    private readonly HashSet<string> workedThisVisit = new HashSet<string>();
    private readonly Dictionary<string, int> cracksAtQuench = new Dictionary<string, int>();
    private readonly Dictionary<string, float> heatReadAt = new Dictionary<string, float>();
    private readonly List<string> chargedOres = new List<string>();
    private readonly Dictionary<string, int> chargedDeposits = new Dictionary<string, int>();
    private int metaVersion;
    private readonly Dictionary<CombatEntity, CraftRequestGuard> guards = new Dictionary<CombatEntity, CraftRequestGuard>();
    private readonly Dictionary<CombatEntity, CraftHandsOnMode> byHand = new Dictionary<CombatEntity, CraftHandsOnMode>();
    private readonly Dictionary<CombatEntity, (string text, float at)> told = new Dictionary<CombatEntity, (string, float)>();
    private readonly List<CraftEvent> answerEvents = new List<CraftEvent>();

    /// <summary>Seconds before the same refusal (or note) is told again to a player at a 3D work place; different words are told at once.</summary>
    public const float RepeatInterval = 1.5f;

    /// <summary>Every station in the loaded scenes.</summary>
    public static IReadOnlyList<CraftStation> All => all;

    /// <summary>Every event of the workshop (visuals, sounds, logs).</summary>
    public event Action<CraftEvent> WorkshopEvent;
    /// <summary>Something the player did changed the station (the window redraws).</summary>
    public event Action Changed;
    /// <summary>The workshop was replaced (restored, cleared, loaded): a network server sends it whole.</summary>
    public event Action WorkshopReplaced;
    /// <summary>A message for this machine's player at the station (a refusal from the server, what was taken): the window shows it.</summary>
    public event Action<string> LocalMessage;
    /// <summary>
    /// A network client: the station's answer (ok, and why) to one of this machine's requests - by its
    /// <see cref="CraftRequest.seq"/> - raised once the copy of the workshop shows what the request did. While it is raised
    /// <see cref="AnswerEvents"/> holds the events the request's own operations raised.
    /// </summary>
    public event Action<int, bool, string> Answered;
    /// <summary>A network client: something this machine's player learnt working here (the server's journal said so) - the window shows it.</summary>
    public event Action<string> Learnt;
    /// <summary>
    /// A line for this machine's player's "What happened" from the hands at a 3D work place (<see cref="CraftHandsOn"/>):
    /// what it told them there while the window stood aside - a refusal (<c>refused</c>: dimmed), what a request did. The
    /// window keeps it in its log (the workshop's own events and the station's messages it logs by itself).
    /// </summary>
    public event Action<string, bool> Logged;

    public ICraftStationNet Net { get; set; }
    /// <summary>
    /// True where the rules run: offline, or on the server. A client of a network session never runs them: with a
    /// network side it follows the server's workshop, without one it cannot be worked from there.
    /// </summary>
    public bool RulesHere => Net != null ? Net.RunsStationRules : !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;
    /// <summary>A network client: the workshop is a copy following the server's.</summary>
    public bool IsMirror { get; private set; }
    /// <summary>Changes when the player working here or the items tried change (the network sends them).</summary>
    public int MetaVersion => metaVersion;
    /// <summary>The finished assemblies tried here (their numbers known).</summary>
    public IReadOnlyCollection<string> InspectedIds => inspected;

    public CraftDatabase Database => database != null ? database : CraftDatabase.Instance;
    public CraftRules Rules => rules ??= Database.Rules;
    public string DisplayName => string.IsNullOrWhiteSpace(stationName) ? "Workshop" : stationName;
    public bool IsTestStation => testStation;
    /// <summary>What the station has (a test station has everything).</summary>
    public CraftTools Tools => testStation ? CraftTools.Everything : tools;
    public bool Has(CraftTools t) => (Tools & t) == t;
    /// <summary>The instruments at hand (a test station has all).</summary>
    public CraftInstruments Instruments => testStation ? CraftInstruments.All : instruments;
    public bool HasInstrument(CraftInstruments i) => (Instruments & i) == i;
    /// <summary>Does the window show the numbers behind everything?</summary>
    public bool Reveals => testStation;

    /// <summary>Is <paramref name="d"/> made at this station: in its list of designs (when it has one), and makeable with its tools?</summary>
    public bool Makes(CraftDesign d) =>
        d != null && (designs == null || designs.Count == 0 || designs.Exists(x => string.Equals(x, d.id, StringComparison.OrdinalIgnoreCase)))
        && CraftAssembly.MadeAt(Rules, d, Tools, out _);
    public bool FreeFuel => freeFuel || testStation;
    public CraftWorkshop Workshop => workshop;
    public CombatEntity Tender => tender;
    public int Version => version;
    /// <summary>Was this finished assembly tried (its numbers known)?</summary>
    public bool IsInspected(string assembly) => testStation || inspected.Contains(assembly);
    public IReadOnlyList<string> Quenchants => quenchants.Count > 0 ? quenchants : new List<string> { quenchMedium };
    /// <summary>How good the station's tools are (see the field).</summary>
    public float ToolQuality => testStation ? 1f : toolQuality;

    // ------------------------------------------------------------------ lifecycle
    private void Reset() => stationId = Guid.NewGuid().ToString("N");

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(stationId))
            stationId = Guid.NewGuid().ToString("N");
        rules = null;
    }

    private void Awake()
    {
        GetComponentsInChildren(true, sources);
        if (sources.Count == 0)
            sources.Add(gameObject.AddComponent<AlchemySurroundings>());
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        if (workshop == null && RulesHere)
        {
            if (persistWorkshop) LoadWorkshop();
            if (workshop == null) SetWorkshop(NewWorkshop());
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (persistWorkshop && RulesHere && !IsMirror)
            SaveWorkshop();
        if (tender != null && RulesHere)
            EndTending(tender);
    }

    private void OnApplicationQuit()
    {
        if (persistWorkshop && RulesHere && !IsMirror)
            SaveWorkshop();
    }

    private CraftWorkshop NewWorkshop()
    {
        string id = string.IsNullOrEmpty(stationId) ? null : stationId.Substring(0, Math.Min(12, stationId.Length));
        return new CraftWorkshop(Rules, Tools, Rules.Settings.defaultAmbient, seed, testStation ? Math.Max(3, vats) : vats, quenchMedium, id);
    }

    private void SetWorkshop(CraftWorkshop w)
    {
        if (workshop != null)
            workshop.EventRaised -= OnWorkshopEvent;
        workshop = w;
        if (workshop != null)
            workshop.EventRaised += OnWorkshopEvent;
        if (!IsMirror)
            ApplyConditions();
        Touch();
        WorkshopReplaced?.Invoke();
    }

    /// <summary>The workshop, loaded or made when there is none yet (where the rules run).</summary>
    private CraftWorkshop EnsureWorkshop()
    {
        if (workshop == null)
        {
            if (persistWorkshop) LoadWorkshop();
            if (workshop == null) SetWorkshop(NewWorkshop());
        }
        return workshop;
    }

    // ------------------------------------------------------------------ network clients
    /// <summary>Client: the copy of the server's workshop to show (a snapshot arrived; null = the server has none yet).</summary>
    public void SetMirror(CraftWorkshop copy)
    {
        IsMirror = true;
        SetWorkshop(copy);
    }

    /// <summary>Client: who works here and which items were tried, as the server says.</summary>
    public void SetMirrorMeta(CombatEntity working, IEnumerable<string> tried)
    {
        if (!IsMirror)
            return;
        tender = working;
        inspected.Clear();
        if (tried != null) inspected.UnionWith(tried);
        Touch();
    }

    /// <summary>Client: the copy moved on (the window redraws).</summary>
    public void MirrorChanged() => Touch();

    /// <summary>Client: the session ended - the copy is dropped (an offline game loads the station's own save again).</summary>
    public void ClearMirror()
    {
        if (!IsMirror)
            return;
        SetWorkshop(null);
        IsMirror = false;
        tender = null;
        inspected.Clear();
    }

    /// <summary>Inside <see cref="Answered"/>: the events the answered request's own operations raised on the copy (a blow's ring, a solder's flash).</summary>
    public IReadOnlyList<CraftEvent> AnswerEvents => answerEvents;

    /// <summary>
    /// Client: the server's answer to request <paramref name="seq"/> (the network side calls this once the copy shows its
    /// operations); <paramref name="events"/> = what those operations raised on the copy.
    /// </summary>
    public void Answer(int seq, bool ok, string reason, IList<CraftEvent> events = null)
    {
        answerEvents.Clear();
        if (events != null) answerEvents.AddRange(events);
        try { Answered?.Invoke(seq, ok, reason ?? ""); }
        finally { answerEvents.Clear(); }
    }

    /// <summary>Client: a line of what this machine's player learnt here (from the server's journal): the window shows and keeps it.</summary>
    public void ShowLearnt(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Learnt?.Invoke(text);
    }

    /// <summary>Keeps <paramref name="text"/> in the window's "What happened" for this machine's player (<see cref="Logged"/>), and shows it nowhere else.</summary>
    public void Log(string text, bool refused = false)
    {
        if (!string.IsNullOrEmpty(text))
            Logged?.Invoke(text, refused);
    }

    /// <summary>A message for this machine's player (from the server): in the inventory's message line and the window.</summary>
    public void ShowLocalMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
            {
                InventoryManager.For(players[i])?.ShowMessage(text, InventoryFeedback.Kind.Info);
                break;
            }
        LocalMessage?.Invoke(text);
    }

    private void Update()
    {
        if (workshop == null || !RulesHere)
            return;
        conditionsTimer += Time.deltaTime;
        if (conditionsTimer >= conditionsInterval)
        {
            conditionsTimer = 0f;
            ApplyConditions();
        }
        if (runWhileAway || tender != null)
            workshop.Advance(Time.deltaTime, maxStepsPerFrame);
        if (workshop.History.Count > historyKept * 2)
            workshop.ForgetHistory(historyKept);
        if (tender != null)
        {
            readTimer += Time.deltaTime;
            if (readTimer >= 1f)
            {
                readTimer = 0f;
                ReadInstruments();
            }
        }
    }

    private void OnWorkshopEvent(CraftEvent e)
    {
        if (logEvents)
            Debug.Log($"[Crafting] {DisplayName}: {e.Kind} {e.Piece} {e.Id} {e.Text}", this);
        if (tender != null && RulesHere)
            CraftKnowledge.For(tender)?.Observe(e, workshop.Piece(e.Piece), Instruments);
        WorkshopEvent?.Invoke(e);
    }

    /// <summary>Reads the surroundings into the workshop (rounded, so a drifting humidity does not fill the history).</summary>
    public void ApplyConditions()
    {
        if (workshop == null)
            return;
        conditions.Clear();
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is Behaviour b && (b == null || !b.isActiveAndEnabled)) continue;
            sources[i]?.Collect(conditions);
        }
        if (conditions.Ambient.HasValue && Math.Abs(conditions.Ambient.Value - workshop.Ambient) >= 1)
            workshop.SetAmbient(Math.Round(conditions.Ambient.Value));
        foreach (KeyValuePair<string, double> v in conditions.Variables)
            workshop.SetVariable(v.Key, Math.Round(v.Value * 20.0) / 20.0, 0.01);
        if (Math.Abs(workshop.ToolQuality - ToolQuality) > 0.01)
            workshop.SetVariable("toolQuality", ToolQuality, 0.001);
    }

    /// <summary>A thermometer read while a piece glows teaches what its colour means.</summary>
    private void ReadInstruments()
    {
        if (!HasInstrument(CraftInstruments.Thermometer) || workshop == null)
            return;
        CraftKnowledge k = CraftKnowledge.For(tender);
        if (k == null)
            return;
        foreach (CraftPiece p in workshop.Pieces)
        {
            if (p.kind != CraftPieceKind.Metal) continue;
            double t = p.MaxTemperature;
            string colour = CraftMetallurgy.HeatName(t, false);
            if (string.IsNullOrEmpty(colour) || colour == "black heat") continue;
            if (heatReadAt.TryGetValue(colour, out float at) && Time.time - at < 8f) continue;
            heatReadAt[colour] = Time.time;
            k.ReadHeat(Rules, colour, t);
        }
    }

    private void Touch()
    {
        version++;
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ tending
    /// <summary>The player works here now (the window opened). On a network client the server is told.</summary>
    public void BeginTending(CombatEntity player)
    {
        if (player == null)
            return;
        if (!RulesHere)
        {
            Net?.SendTending(true);
            return;
        }
        if (tender != player)
        {
            tender = player;
            workedThisVisit.Clear();
            metaVersion++;
        }
        // the hands are at the station from now: what fills from nothing (the loom) starts filling
        if (!testStation)
            GuardOf(player).Touch(Time.unscaledTimeAsDouble);
        Touch();
    }

    /// <summary>The player stops working here (the window closed, they walked away, they left the session).</summary>
    public void EndTending(CombatEntity player)
    {
        if (player == null)
            return;
        if (!RulesHere)
        {
            Net?.SendTending(false);
            return;
        }
        byHand.Remove(player); // a 3D work place is put down with the visit
        if (player != tender)
            return;
        tender = null;
        metaVersion++;
        if (persistWorkshop)
            SaveWorkshop();
        Touch();
    }

    // ------------------------------------------------------------------ requests
    /// <summary>
    /// Asks the station to do something for <paramref name="player"/> (here, or through the network). False = refused, with
    /// the reason. True = done (where the rules run) - with a reason when the player's guard granted it only in part - or,
    /// on a network client, sent: the server's answer comes later (<see cref="Answered"/>, for a request with a
    /// <see cref="CraftRequest.seq"/>). Where the rules run, the request's shape is checked first, at every station
    /// (<see cref="CraftRequestGuard.WellFormed(in CraftRequest, out string)"/>); then the player's guard bounds it - not
    /// at a test station, where only a player on another machine is held to the pace of requests - and what the guard
    /// took for work the station then refuses is given back (<see cref="CraftRequestGuard.Refund"/>).
    /// </summary>
    public bool Request(CombatEntity player, CraftRequest r, out string reason)
    {
        reason = "";
        if (!RulesHere)
        {
            if (Net == null)
            {
                reason = "This station is not set up for multiplayer (it needs a NetCraftStation): only the host can work here.";
                return false;
            }
            // Checked here, so that it is never sent at all: an item the server cannot name, a request too big to carry.
            if (r.item != null && string.IsNullOrEmpty(ItemDatabase.Id(r.item)))
            {
                reason = $"{r.item.Name} is not in the Item Database: it cannot be used in a network session.";
                return false;
            }
            CraftRequestGuard.Fit(ref r, out string cut);
            if (!CraftRequestGuard.WellFormed(r, out reason))
                return false;
            Net.SendRequest(r);
            reason = cut;
            return true;
        }
        CraftWorkshop w = EnsureWorkshop();
        // The shape of what reaches the workshop, whoever sent it and wherever (a test station too): a drawn path too long
        // keeps its first part; ids, numbers and lists the workshop cannot take are refused.
        CraftRequestGuard.Fit(ref r, out string trimmed);
        if (!CraftRequestGuard.WellFormed(r, out reason))
            return false;
        string only = testStation ? null : CraftRequestGuard.TestStationOnly(r, w); // (an item nothing has gone into may be put aside anywhere)
        if (only != null)
        {
            reason = only;
            return false;
        }
        // A fold or a stack laid by hand at a real station leaves its seam to the blows that follow (only a test station's
        // quick buttons weld at once).
        if (!testStation && (r.kind == CraftRequestKind.Fold || r.kind == CraftRequestKind.Weld))
            r.flag = true;
        // The craft this is, and how practised the player is at it: a practised hand does more with each action.
        CraftDiscipline? craft = CraftMastery.OfRequest(Rules, w, r);
        double practice = craft.HasValue ? Mastery(player, craft.Value) : GeneralSkill(player);
        // No faster than real hands, never steadier than this practice. A test station's quick buttons send whole bundles,
        // so nobody's hands are checked there - but a player on another machine sends no faster than the pace of requests.
        string note = "";
        CraftRequestGuard guard = null;
        double now = Time.unscaledTimeAsDouble;
        if (player != null && !testStation)
        {
            guard = GuardOf(player);
            guard.Settings = MasterySettings;
            guard.Skill = practice;
            if (!guard.Check(ref r, now, out note, w))
            {
                reason = note;
                return false;
            }
        }
        else if (player != null && !CardTable.IsLocalPlayer(player) && !GuardOf(player).Paced(now, out reason))
            return false;
        if (note.Length == 0)
            note = trimmed; // a stroke too long to take in at once: what counted
        ByPractice(ref r, practice);
        // what it teaches, judged on the piece as it is before (a skein counted again, a stone named again teach nothing)
        double xp = craft.HasValue ? CraftMastery.WorkExperience(r, w.Piece(r.piece)) : 0;
        bool ok;
        switch (r.kind)
        {
            case CraftRequestKind.PutItem:
                // Fibre by its supply id (any item that is that fibre): laid on the fibre bench as a batch.
                ok = r.item == null && string.IsNullOrEmpty(r.itemId) && IsFibre(r.id)
                    ? PutFibre(player, r.id, Math.Max(1, r.count), out reason)
                    : PutItem(player, r.item != null ? r.item : ItemDatabase.Item(r.itemId), Math.Max(1, r.count), out reason, r.location, r.a);
                break;
            case CraftRequestKind.TakePiece: ok = TakePiece(player, r.piece, out reason); break;
            case CraftRequestKind.ScrapPiece: ok = ScrapPiece(player, r.piece, out reason); break;
            case CraftRequestKind.Move: ok = Move(player, r, out reason); break;
            case CraftRequestKind.Turn: ok = w.TurnPiece(r.piece, out reason); break;
            case CraftRequestKind.SetIntent: ok = w.SetIntent(r.piece, r.id, r.other, out reason); break;
            case CraftRequestKind.AddFuel: ok = AddFuel(player, r.id, Math.Max(1, r.count), r.flag, out reason); break;
            case CraftRequestKind.Bellows:
                ok = r.flag ? Has(CraftTools.Furnace) : Has(CraftTools.Forge);
                if (ok) w.Bellows(r.a > 0 ? r.a : 1, r.flag);
                else reason = r.flag ? "There is no furnace here." : "There is no forge here.";
                break;
            case CraftRequestKind.Damper: w.SetDamper(r.a, r.flag); ok = true; break;
            case CraftRequestKind.Oven: ok = w.SetOven(r.a, out reason); break;
            // The ore bench: a stone is named by its place on the bench, and i2 the layout it was aimed at (0 = not checked).
            case CraftRequestKind.Crush: ok = w.CrushOre(r.piece, r.i0, r.a, out reason, r.i2); if (ok) Handled(player, r.piece); break;
            case CraftRequestKind.DiscardFragment: ok = w.DiscardFragment(r.piece, r.i0, out reason, r.i2); break;
            case CraftRequestKind.Wash: ok = w.WashOre(r.piece, r.a, r.b, r.c, out reason); if (ok) Handled(player, r.piece); break;
            case CraftRequestKind.Sieve:
            {
                string fines = w.SieveOre(r.piece, r.a, out reason, r.b > 0 ? r.b : 1);
                ok = reason == "" || fines != null;
                break;
            }
            case CraftRequestKind.Flux: ok = Flux(player, r.id, Math.Max(1, r.count), r.i0, r.piece, out reason, r.a > 0 ? r.a : 1, r.points); break;
            case CraftRequestKind.TapSlag: ok = w.TapSlag(out reason, r.a > 0 ? r.a : 1); break;
            case CraftRequestKind.ExtractBloom:
            {
                string bloom = w.ExtractBloom(out reason);
                ok = bloom != null;
                if (ok) Smelted(player, bloom);
                break;
            }
            case CraftRequestKind.StirMelt: ok = w.StirMelt(r.a > 0 ? r.a : 1, out reason); break;
            case CraftRequestKind.Skim: ok = w.SkimDross(r.a > 0 ? r.a : 1, out reason); break;
            // A pour: i1 the share of the mould it brought, i2 the metal lost on the floor (thousandths of a mould each);
            // flag: knock a frozen melt out as a cake.
            case CraftRequestKind.Pour:
                ok = (r.flag ? w.KnockOut(out reason) : w.Pour(r.id, r.a, r.b, r.i0, out reason, r.c, r.d, r.i1 > 0 ? r.i1 / 1000.0 : 1, r.i2 / 1000.0)) != null;
                break;
            case CraftRequestKind.TakeMelt:
            {
                // flag: lift the last of the metal (a bead too small to pour) off the bottom with the tongs
                string cast = r.flag ? w.LiftBead(out reason) : w.TakeMelt(r.id, r.a, r.b, out reason, r.c, r.d, r.i1 > 0 ? r.i1 / 1000.0 : 1, r.i0, r.i2 / 1000.0);
                ok = cast != null;
                if (ok && cast.Length > 0) Smelted(player, cast);
                break;
            }
            case CraftRequestKind.Strike:
                ok = w.Strike(r.piece, r.a, (CraftStrikeTool)r.i0, (CraftStrikeFace)r.i1, r.b, r.c, out reason);
                if (ok) WorkedMetal(player, r.piece);
                break;
            case CraftRequestKind.Fold: ok = w.Fold(r.piece, out reason, r.b > 0 ? r.b : 1, r.flag); break;
            case CraftRequestKind.Weld: ok = w.Weld(r.piece, r.other, out reason, r.b > 0 ? r.b : 1, r.flag); break;
            case CraftRequestKind.Split:
                // A cut in one go is the quick bench's: at a real station wood is sawn stroke by stroke (SawCut), where the
                // hands' wander and squareness leave their mark on the ends; metal is cut with the hot chisel's blows.
                ok = testStation || w.Piece(r.piece)?.kind != CraftPieceKind.Wood;
                if (ok) ok = w.SplitPiece(r.piece, r.a, out reason) != null;
                else reason = "Wood is sawn through stroke by stroke: mark it and saw it (Woodshop → Saw, or Carve).";
                break;
            case CraftRequestKind.FormFastener: ok = w.FormFastener(r.piece, (CraftFastenerKind)r.i0, out reason) != null; break;
            case CraftRequestKind.Agitate: ok = w.Agitate(r.piece, (CraftQuenchMotion)r.i0, out reason, r.a > 0 ? r.a : -1); break;
            case CraftRequestKind.FillTank: ok = FillTank(player, r.id, out reason); break;
            case CraftRequestKind.Clay:
            {
                // Clay that goes on is taken from the bag; wiping it off costs nothing.
                bool goesOn = w.ClayGoesOn(r.piece, r.i0, r.i1, r.a, r.points);
                string clay = FirstOfKind(CraftSupplyKind.Clay);
                ok = (!goesOn || HaveSupply(player, clay, 1, out reason)) && w.ApplyClay(r.piece, r.i0, r.i1, r.a, out reason, r.points) &&
                     (!goesOn || UseSupply(player, clay, 1, out reason));
                break;
            }
            case CraftRequestKind.Descale: ok = w.Descale(r.piece, r.i0, r.i1, r.id, r.a, r.b * Pace(player, r), out reason); break;
            case CraftRequestKind.Grind:
                ok = w.Grind(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace(player, r), out reason);
                if (ok) WorkedMetal(player, r.piece);
                break;
            case CraftRequestKind.File:
                ok = w.File(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace(player, r), out reason);
                if (ok) WorkedMetal(player, r.piece);
                break;
            case CraftRequestKind.Polish: ok = w.Polish(r.piece, r.i0, r.i1, r.id, r.a, r.b * Pace(player, r), out reason); break;
            case CraftRequestKind.Coat: ok = HaveSupply(player, r.id, 1, out reason) && w.Coat(r.piece, r.id, out reason) && UseSupply(player, r.id, 1, out reason); break;
            // The woodshop.
            case CraftRequestKind.Rive: ok = w.Rive(r.piece, r.a, r.b, out reason, r.c > 0 ? r.c : 0.5) != null; break;
            case CraftRequestKind.SawCut: ok = w.SawCut(r.piece, r.a, r.b, r.c, out reason) != null; break;
            case CraftRequestKind.RipCut: ok = w.RipCut(r.piece, r.a, r.b, r.c, r.d, r.i0 / 1000.0, out reason); break;
            case CraftRequestKind.BendWood: ok = w.BendWood(r.piece, r.i0, r.i1, r.a, r.b, r.flag, out reason, r.c); break;
            case CraftRequestKind.StackWood: ok = w.StackWood(r.piece, r.a, r.b, r.c, out reason); break;
            case CraftRequestKind.StackCover: ok = w.SetStackCover(r.a, out reason); break;
            case CraftRequestKind.SandWood: ok = w.SandWood(r.piece, r.i0, r.i1, r.a, r.b * Pace(player, r) / Math.Max(0.1, Rules.Settings.handWorkPace), r.c, out reason); break;
            case CraftRequestKind.Varnish: ok = Brush(player, r, out reason); break;
            // The woodshop's logs.
            case CraftRequestKind.DebarkWood: ok = w.DebarkWood(r.piece, r.i0, r.i1, r.a, r.b, r.c, out reason); break;
            case CraftRequestKind.SplitLog: ok = w.SplitLog(r.piece, r.a, r.b, r.c, Math.Max(1, r.i0), out reason) != null; break;
            // The tailor's: spinning fibre from the bag (laid on the station as a batch, a bobbin spun from it).
            case CraftRequestKind.Spin: ok = Spin(player, r.id, r.a, r.b, r.c, out reason); break;
            case CraftRequestKind.JoinYarn: ok = w.JoinYarn(r.piece, r.other, out reason); break;
            // The fibre bench and the wheel: fibre into yarn, yarn into thread.
            case CraftRequestKind.CleanFibre: ok = w.CleanFibre(r.piece, r.a, r.b, out reason); break;
            case CraftRequestKind.ScourFibre:
                ok = (string.IsNullOrEmpty(r.id) || HaveSupply(player, r.id, 1, out reason))
                     && w.ScourFibre(r.piece, r.a, r.b, r.c, r.d, r.id, r.i0, out reason)
                     && (string.IsNullOrEmpty(r.id) || UseSupply(player, r.id, 1, out reason));
                break;
            case CraftRequestKind.OpenFibre: ok = w.OpenFibre(r.piece, r.a, r.b, out reason); break;
            case CraftRequestKind.CardFibre: ok = w.CardFibre(r.piece, Math.Max(1, r.count), r.a, r.b, out reason); break;
            case CraftRequestKind.CombFibre: ok = w.CombFibre(r.piece, Math.Max(1, r.count), r.a, out reason); break;
            case CraftRequestKind.SpinFibre: ok = w.SpinFibre(r.piece, r.a, r.b, r.c, Math.Max(1, r.i0), out reason) != null; break;
            case CraftRequestKind.PlyYarn: ok = w.PlyYarn(r.piece, r.other, r.id, r.a, r.b, out reason); break;
            case CraftRequestKind.MeasureYarn: ok = w.MeasureYarn(r.piece, r.a, out reason); break;
            case CraftRequestKind.ReelYarn: ok = ReelYarn(player, r.piece, out reason); break;
            // The tailor's table: measuring the wearer, laying patterns out.
            case CraftRequestKind.LayoutPattern: ok = w.LayoutPattern(r.piece, r.id, r.i0, r.i1, r.i2, r.flag, out reason); break;
            case CraftRequestKind.MeasureFit: ok = w.MeasureFit(r.piece, r.a, r.b, out reason); break;
            case CraftRequestKind.WeaveCloth:
                ok = w.WeaveCloth(r.piece, r.i0, r.a, r.b, r.points != null ? r.points.GetRange(0, Math.Min(r.points.Count, Math.Max(0, r.i1))) : null, out reason) != null;
                break;
            // The lapidary.
            case CraftRequestKind.TestGem: ok = TestGem(r, out reason); break;
            case CraftRequestKind.IdentifyGem: ok = IdentifyGem(player, r.piece, r.id, out reason); break;
            case CraftRequestKind.EvaluateGem:
                ok = HasInstrument(CraftInstruments.Loupe);
                if (ok) ok = w.EvaluateGem(r.piece, r.points, r.a, out reason);
                else reason = "There is no loupe here.";
                break;
            case CraftRequestKind.OrientGem: ok = w.OrientGem(r.piece, r.id, r.a, r.b, r.c, r.d, out reason); break;
            case CraftRequestKind.CleanGem: ok = w.CleanGem(r.piece, r.i0 == 1, r.points, r.a, out reason); break;
            case CraftRequestKind.CleaveGem: ok = w.CleaveGem(r.piece, r.a, r.b, r.c, out reason, r.i0 == 1); break;
            case CraftRequestKind.SawGem: ok = w.SawGem(r.piece, r.a, r.b, r.c, r.d, r.i0 / 1000.0, out reason, r.i1); break;
            case CraftRequestKind.GrindGem: ok = w.GrindGem(r.piece, r.points, r.a, r.b, out reason); break;
            case CraftRequestKind.FacetGem: ok = w.FacetGem(r.piece, r.i0, Math.Max(1, r.count), r.a, r.b, r.c, r.d, out reason, r.i2 / 1000.0, r.i1); break;
            case CraftRequestKind.PolishGem: ok = PolishGem(player, r, out reason); break;
            // The jeweller's bench.
            case CraftRequestKind.FormShank: ok = w.FormShank(r.piece, (CraftJewelPart)r.i0, r.a, r.b, r.c, r.i1, out reason); break;
            case CraftRequestKind.SeatSetting: ok = w.SeatSetting(r.piece, r.other, r.a, r.b, r.c, out reason); break;
            case CraftRequestKind.FinishJewel: ok = w.FinishJewel(r.piece, r.a, r.b, out reason); break;
            case CraftRequestKind.BrushOre:
                // Coverage at the window's careful pace (full by hand in 3D).
                ok = w.BrushOre(r.piece, r.i0, (CraftOreBrush)r.i1, Math.Min(1, r.a * Pace(player, r) / Math.Max(0.1, Rules.Settings.handWorkPace)), out reason, r.i2);
                if (ok) Handled(player, r.piece);
                break;
            case CraftRequestKind.Carve:
                ok = w.Carve(r.piece, r.i0, r.i1, (CraftSurface.CarveCut)r.i2, r.a * Pace(player, r) / Math.Max(0.1, Rules.Settings.handWorkPace), r.b, out reason);
                break;
            case CraftRequestKind.Sharpen: ok = w.Sharpen(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, Math.Max(1, r.count), r.c, r.flag, out reason); break;
            case CraftRequestKind.FillVat: ok = FillVat(player, r.i0, r.id, Math.Max(1, r.count), out reason); break;
            case CraftRequestKind.DrainVat: ok = w.DrainVat(r.i0, out reason); break;
            case CraftRequestKind.VatHeat: ok = w.SetVatHeat(r.i0, r.a, out reason); break;
            case CraftRequestKind.StirVat: ok = w.StirVat(r.i0, out reason, r.a); break;
            case CraftRequestKind.Scrape: ok = w.Scrape(r.piece, r.points, r.flag, r.a, r.b, out reason); break;
            case CraftRequestKind.Stretch: ok = w.Stretch(r.piece, r.a, r.b, r.c, r.d, out reason); break;
            case CraftRequestKind.Stake: ok = w.Stake(r.piece, r.points, r.a, out reason); break;
            case CraftRequestKind.Condition:
            case CraftRequestKind.Dye: ok = FinishLeather(player, r, out reason); break;
            case CraftRequestKind.Slick: ok = w.Slick(r.piece, r.points, r.a, out reason); break;
            case CraftRequestKind.Skive: ok = w.Skive(r.piece, r.points, r.a, out reason); break;
            case CraftRequestKind.Cut: ok = w.CutPattern(r.piece, r.id, r.i0, r.i1, r.i2, r.a, out reason) != null; break;
            case CraftRequestKind.StartAssembly:
                ok = Makes(Rules.Design(r.id));
                if (ok) ok = w.StartAssembly(r.id, out reason) != null;
                else reason = "That is not made here.";
                break;
            case CraftRequestKind.Attach: ok = Attach(player, r, out reason); break;
            case CraftRequestKind.Detach: ok = w.Detach(r.piece, r.id, r.i0, out reason); break;
            case CraftRequestKind.JointWork:
                // the needle's pull (flag) pierces as it draws, and counts its stitches pulled past the cut (i2)
                ok = w.JointWork(r.piece, r.id, (CraftJointWork)r.i0, Math.Max(0, r.count), r.a, r.b, out reason, r.flag, r.c, r.d, r.flag ? r.i2 : -1);
                break;
            case CraftRequestKind.Finish: ok = Finish(player, r.piece, out reason); break;
            case CraftRequestKind.Routine: ok = Routine(player, r, out reason); break;
            case CraftRequestKind.Inspect: ok = Inspect(player, r.piece, out reason); break;
            case CraftRequestKind.InspectItem: ok = InspectItem(player, r.item != null ? r.item : ItemDatabase.Item(r.itemId), out reason); break;
            case CraftRequestKind.Assay: ok = Assay(player, r.piece, out reason); break;
            case CraftRequestKind.Suspend: w.Suspend(); ok = true; break;
            case CraftRequestKind.Resume: w.Resume(); ok = true; break;
            case CraftRequestKind.Wait:
                ok = testStation;
                if (ok) w.RunSteps((int)Math.Round(Math.Min(1800, Math.Max(0, r.a)) / Rules.Settings.stepSeconds));
                else reason = "Time passes as it will: leave it and come back.";
                break;
            case CraftRequestKind.HandsOn:
                // at a 3D work place from now (or no longer): its work claims the full pace of the hands
                ok = player != null;
                if (!ok) reason = "Nobody is working here.";
                else if (r.flag) byHand[player] = Enum.IsDefined(typeof(CraftHandsOnMode), r.i0) ? (CraftHandsOnMode)r.i0 : CraftHandsOnMode.None;
                else byHand.Remove(player);
                break;
            default: ok = false; break;
        }
        if (ok)
        {
            Practice(player, craft, r, xp);
            Touch();
            reason = note; // what the guard let through only in part
        }
        else
            guard?.Refund(); // the station said no: what the guard took for it is given back, so the retry is not held back
        return ok;
    }

    // ------------------------------------------------------------------ the players' hands
    /// <summary>
    /// The bounds on what <paramref name="player"/>'s hands do here (<see cref="CraftRequestGuard"/>): one per player, kept
    /// while they are in the session, the same for a player offline, the host and a network client.
    /// </summary>
    private CraftRequestGuard GuardOf(CombatEntity player)
    {
        if (guards.TryGetValue(player, out CraftRequestGuard g))
            return g;
        List<CombatEntity> gone = null;
        foreach (CombatEntity k in guards.Keys)
            if (k == null) (gone ??= new List<CombatEntity>()).Add(k); // players that are gone
        if (gone != null)
            foreach (CombatEntity k in gone) ForgetPlayer(k);
        g = new CraftRequestGuard(unchecked(player.GetInstanceID() * 7919 + 17), 0);
        guards[player] = g;
        return g;
    }

    /// <summary>The player left the session (a server: their connection went): their guard and their 3D work go with them.</summary>
    public void ForgetPlayer(CombatEntity player)
    {
        if (ReferenceEquals(player, null))
            return;
        guards.Remove(player);
        byHand.Remove(player);
        told.Remove(player);
        pots.Forget(player); // their open pots of oil, wax, dye and varnish go with them
    }

    /// <summary>Is <paramref name="player"/> working at a 3D work place here (the full pace of the hands)?</summary>
    public bool WorkingByHand(CombatEntity player) => player != null && byHand.ContainsKey(player);

    /// <summary>Is <paramref name="player"/> at the 3D work place where <paramref name="kind"/> is worked (<see cref="CraftHandsOn.WorkedAt"/>)?</summary>
    public bool WorkingByHand(CombatEntity player, CraftRequestKind kind) =>
        player != null && byHand.TryGetValue(player, out CraftHandsOnMode at) && CraftHandsOn.WorkedAt(at, kind);

    // ------------------------------------------------------------------ practice
    private CraftMasterySettings MasterySettings => Rules?.Settings?.mastery;

    /// <summary>How practised <paramref name="player"/> is at <paramref name="craft"/> (their journal, kept where the rules run).</summary>
    public double Mastery(CombatEntity player, CraftDiscipline craft) =>
        player != null ? CraftMastery.Of(CraftKnowledge.For(player)?.Data, craft, MasterySettings) : 0;

    private static double GeneralSkill(CombatEntity player) => player != null ? CraftJournal.HandSkill(CraftKnowledge.For(player)?.Data) : 0;

    /// <summary>How steady the player's hands are for this request (their practice in its craft): for the network guard.</summary>
    public double SkillFor(CombatEntity player, in CraftRequest r)
    {
        CraftDiscipline? craft = CraftMastery.OfRequest(Rules, EnsureWorkshop(), r);
        return craft.HasValue ? Mastery(player, craft.Value) : GeneralSkill(player);
    }

    /// <summary>What a practised hand does with one action (see <see cref="CraftMastery.ByPractice"/>).</summary>
    private void ByPractice(ref CraftRequest r, double m) => CraftMastery.ByPractice(ref r, m, MasterySettings);

    /// <summary>Work done teaches its craft (a little at a time: small work comes only so fast) - <paramref name="xp"/>, judged before it was done.</summary>
    private void Practice(CombatEntity player, CraftDiscipline? craft, in CraftRequest r, double xp)
    {
        if (player == null || !craft.HasValue)
            return;
        if (r.kind == CraftRequestKind.IdentifyGem && !(workshop.Piece(r.piece)?.gem.identified ?? false))
            xp *= 0.25; // a wrong name teaches less
        if (xp > 0)
            CraftKnowledge.For(player)?.Practised(craft.Value, xp, true, false, MasterySettings);
    }

    /// <summary>
    /// Work done by routine (<see cref="CraftRoutine"/>): one action, as well as the player's practice does it - only for a
    /// player of the rank it needs.
    /// </summary>
    private bool Routine(CombatEntity player, CraftRequest r, out string reason)
    {
        reason = "";
        var routine = (CraftRoutine)r.i0;
        if (!Enum.IsDefined(typeof(CraftRoutine), routine)) { reason = "There is no such work."; return false; }
        CraftDiscipline craft = CraftMastery.RoutineDiscipline(routine);
        CraftMasterySettings s = MasterySettings;
        double m = Mastery(player, craft);
        if (!CraftMastery.CanRoutine(routine, m, s))
        {
            CraftRank need = CraftMastery.RoutineRank(routine);
            reason = $"Only {CraftText.A(CraftMastery.RankName(need))} {CraftMastery.RankName(need).ToLowerInvariant()} {CraftMastery.Crafter(craft)} can {CraftMastery.RoutineName(routine)} by routine: do it by hand.";
            return false;
        }
        return CraftMastery.DoRoutine(workshop, routine, m, r.piece, player != null ? player.name : "wearer", s, out reason);
    }

    /// <summary>
    /// How much work a second of the hands does here (<see cref="CraftSimulationSettings.handWorkPace"/>): full by
    /// hand in 3D (<c>d</c> = 1 on the request, while the station knows the player is at the 3D work place where that
    /// work is done: <see cref="CraftRequestKind.HandsOn"/>), a careful share of it from the window's drawings - more of
    /// it the more practised the player is (their journal, kept where the rules run).
    /// </summary>
    private double Pace(CombatEntity player, in CraftRequest r)
    {
        CraftSimulationSettings s = Rules.Settings;
        if (r.d >= 0.5f && WorkingByHand(player, r.kind))
            return s.handWorkPace;
        double skill = SkillFor(player, r);
        return s.handWorkPace * (s.windowPaceNovice + (s.windowPaceMaster - s.windowPaceNovice) * CraftMath.Clamp01(skill));
    }

    /// <summary>
    /// <see cref="Request"/>, telling the player why not (or what counted of work granted only in part) - the same words
    /// again only after <see cref="RepeatInterval"/> seconds, different ones at once, so that a hammer swung or bellows
    /// pumped faster than the guard allows at a 3D work place does not flood the screen.
    /// </summary>
    public bool RequestAndTell(CombatEntity player, CraftRequest r)
    {
        bool ok = Request(player, r, out string reason);
        if (!string.IsNullOrEmpty(reason) && !ToldLately(player, reason))
            Tell(player, reason);
        return ok;
    }

    /// <summary>Were these words told to <paramref name="player"/> less than <see cref="RepeatInterval"/> seconds ago? If not, they are now.</summary>
    private bool ToldLately(CombatEntity player, string text)
    {
        if (player == null)
            return false;
        float now = Time.unscaledTime;
        if (told.TryGetValue(player, out (string text, float at) last) && last.text == text && now - last.at < RepeatInterval)
            return true;
        if (told.Count > 16)
            told.Clear(); // players come and go
        told[player] = (text, now);
        return false;
    }

    // ------------------------------------------------------------------ bringing things in
    /// <summary>Can <paramref name="item"/> be put on this station (stock or a worked piece)?</summary>
    public bool Accepts(ItemSO item)
    {
        if (item is CraftPieceItemSO)
            return true;
        // Raw fibre (a fibre stock item, or any item that is a fibre by its name) is laid on a fibre bench or a wheel as a batch.
        bool fibreHere = (Tools & (CraftTools.FibreBench | CraftTools.SpinningWheel)) != 0;
        if (item is CraftStockItemSO s)
            return s.StockKind <= CraftStockKind.Fasteners || s.StockKind == CraftStockKind.Log || s.StockKind == CraftStockKind.Gem
                || s.StockKind == CraftStockKind.Supply && IsFibre(s.CraftId) && fibreHere;
        return fibreHere && FibreOf(item) != null;
    }

    /// <summary>
    /// Puts <paramref name="count"/> of <paramref name="item"/> on the station (from the inventory unless it is a free
    /// supply). Stock becomes new pieces (ore lumps and hides keep what they were); a worked piece comes back exactly as
    /// it was carried off. With <paramref name="into"/> other than Air the piece goes straight there (the fire, the crucible).
    /// </summary>
    public bool PutItem(CombatEntity player, ItemSO item, int count, out string reason, CraftLocation into = CraftLocation.Air, float depth = 0.6f)
    {
        reason = "";
        if (item == null || !Accepts(item))
        {
            reason = item != null ? $"{item.Name} is not worked here." : "Nothing to put down.";
            return false;
        }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        bool free = IsFreeSupply(item);
        int done = 0;
        for (int k = 0; k < count; k++)
        {
            CraftPiece piece;
            List<CraftPiece> parts = null;
            if (!(item is CraftPieceItemSO) && !(item is CraftStockItemSO))
            {
                // A fibre item of the project's own (not a crafting stock item): it becomes a batch of that fibre.
                string fibre = FibreOf(item);
                if (fibre == null) break;
                if (!free && (inv == null || !TakeOne(inv, item, out _))) { if (done == 0) reason = $"You have no {item.Name}."; break; }
                piece = CraftFibre.NewFibre(Rules, fibre);
            }
            else if (item is CraftPieceItemSO pi)
            {
                if (!free && (inv == null || inv.GetItemCount(item) <= 0)) break;
                piece = pi.Piece?.Clone();
                parts = new List<CraftPiece>();
                foreach (CraftPiece c in pi.Components) parts.Add(c.Clone());
                if (piece == null) { reason = "That piece is lost."; break; }
                if (!free) inv.RemoveItems(item, 1);
                CraftItems.Forget(item);
            }
            else
            {
                var stock = (CraftStockItemSO)item;
                int code = 0;
                if (!free)
                {
                    if (!TakeOne(inv, item, out code)) { if (done == 0) reason = $"You have no {item.Name}."; break; }
                }
                else if (stock.StockKind == CraftStockKind.Ore)
                    code = CraftStockItemSO.OreCode(stock.DefaultDeposit, freeLump++);
                else if (stock.StockKind == CraftStockKind.Hide)
                    code = CraftStockItemSO.HideCode(seed * 7919 + freeLump++);
                // Every rough stone is a stone of its own: one bought without a value of its own gets one.
                if (code == 0 && stock.StockKind == CraftStockKind.Gem)
                    code = CraftStockItemSO.HideCode(seed * 7919 + 104729 + freeLump++);
                piece = stock.StockKind == CraftStockKind.Supply ? CraftFibre.NewFibre(Rules, stock.CraftId) : stock.MakePiece(Rules, code);
                if (piece == null) { reason = $"{item.Name} cannot be worked here."; break; }
            }
            string id = workshop.AddPiece(piece, parts);
            done++;
            if (piece.kind == CraftPieceKind.Ore && player != null)
                CraftKnowledge.For(player)?.HandledOre(Rules, piece.ore.ore, false);
            if (into != CraftLocation.Air && id != null && !workshop.MovePiece(id, into, out string why, depth))
                reason = why;
        }
        if (done > 0)
            Touch();
        return done > 0;
    }

    /// <summary>Removes one copy of a stock item, reading the value it kept (ore deposit, hide animal).</summary>
    private static bool TakeOne(InventoryManager inv, ItemSO item, out int code)
    {
        code = 0;
        if (inv == null || inv.GetItemCount(item) <= 0)
            return false;
        List<InventoryItem> stacks = inv.FindStacks(item);
        InventoryItem stack = stacks.Count > 0 ? stacks[stacks.Count - 1] : null;
        code = CraftStockItemSO.CodeOf(stack);
        if (inv.RemoveItems(item, 1) <= 0)
            return false;
        // The value goes with the copy taken.
        if (stack != null && stack.DurabilityList != null && stack.DurabilityList.Count > Math.Max(0, stack.stackCurrent))
            stack.DurabilityList.RemoveAt(stack.DurabilityList.Count - 1);
        return true;
    }

    /// <summary>
    /// Moves a piece to <c>location</c>: into the fire <c>a</c> deep - only segments <c>i0</c> to <c>i1</c> of it when
    /// <c>flag</c> says a stretch was given (without it an <c>i1</c> of 0 means the whole piece, so a stretch of only the
    /// first segment needs the flag) - into vat <c>i2</c>, into the tank at the angle <c>b</c>.
    /// </summary>
    private bool Move(CombatEntity player, CraftRequest r, out string reason)
    {
        CraftPiece p = workshop.Piece(r.piece);
        bool wasQuench = p != null && p.location == CraftLocation.Quench;
        if (r.location == CraftLocation.Quench && p != null)
            cracksAtQuench[p.id] = p.defects.FindAll(d => d.kind == CraftDefectKind.Crack).Count;
        if (r.location == CraftLocation.Furnace && p != null && p.kind == CraftPieceKind.Ore)
        {
            if (!chargedOres.Contains(p.ore.ore)) chargedOres.Add(p.ore.ore);
            chargedDeposits[p.ore.ore] = p.ore.deposit;
        }
        // A stretch of it (the forge's span: segments i0 to i1) when flag says one was given; without the flag an end of 0
        // still means the whole piece (to its last segment), as before.
        int to = r.flag ? r.i1 : r.i1 > 0 ? r.i1 : 9999;
        bool ok = workshop.MovePiece(r.piece, r.location, out reason, r.a > 0 ? r.a : 1, r.i0, to, r.i2, r.b);
        // Lifted out of a hardening quench, the journal learns how it went; a dip to cool it teaches nothing about quenching.
        if (ok && wasQuench && r.location != CraftLocation.Quench && p != null && player != null && p.kind == CraftPieceKind.Metal
            && p.Tag(CraftThermal.QuenchHardeningTag) > 0.5)
            QuenchLearnt(player, p);
        else if (ok && wasQuench && r.location != CraftLocation.Quench && p != null)
            cracksAtQuench.Remove(p.id);
        return ok;
    }

    private void QuenchLearnt(CombatEntity player, CraftPiece p)
    {
        CraftAlloyProfile prof = p.Profile(Rules);
        if (!prof.HardensByQuench)
            return;
        int before = cracksAtQuench.TryGetValue(p.id, out int c) ? c : 0;
        int now = p.defects.FindAll(d => d.kind == CraftDefectKind.Crack).Count;
        double mart = 0;
        foreach (MetalSegment s in p.segments) mart = Math.Max(mart, Math.Max(s.edge.mart, s.body.mart));
        CraftKnowledge.For(player)?.Quenched(prof, workshop.Quench.medium, mart > 0.5, now > before);
        cracksAtQuench.Remove(p.id);
    }

    private void WorkedMetal(CombatEntity player, string id)
    {
        CraftPiece p = workshop.Piece(id);
        if (player == null || p == null || p.kind != CraftPieceKind.Metal || !workedThisVisit.Add(id))
            return;
        CraftKnowledge.For(player)?.Worked(Rules, p.Profile(Rules));
    }

    private void Handled(CombatEntity player, string id)
    {
        CraftPiece p = workshop.Piece(id);
        if (player != null && p != null && p.kind == CraftPieceKind.Ore && workedThisVisit.Add("ore:" + id))
            CraftKnowledge.For(player)?.HandledOre(Rules, p.ore.ore, false);
    }

    private void Smelted(CombatEntity player, string product)
    {
        CraftPiece p = workshop.Piece(product);
        if (player == null || p == null)
            return;
        CraftKnowledge k = CraftKnowledge.For(player);
        string what = CraftLook.MetalName(Rules, p.Profile(Rules), k?.Data);
        foreach (string ore in chargedOres)
            k?.HandledOre(Rules, ore, true, chargedDeposits.TryGetValue(ore, out int dep) ? dep : 0, $"{what}, {CraftText.Mass(p.Mass(Rules))}");
        chargedOres.Clear();
        chargedDeposits.Clear();
    }

    // ------------------------------------------------------------------ taking things out
    /// <summary>Takes a piece off the station into the inventory: a worked-piece item, or the finished item it is.</summary>
    public bool TakePiece(CombatEntity player, string id, out string reason)
    {
        reason = "";
        CraftPiece p = workshop.Piece(id);
        if (p == null)
        {
            reason = "There is no such piece here.";
            return false;
        }
        // Not from inside a working place (taken out first, as the work there needs), nor too hot to carry.
        if (!CraftWorkshop.CanTake(p, out reason))
            return false;
        bool finished = p.kind == CraftPieceKind.Assembly && p.assembly.finished;
        CraftItemReport report = finished ? CraftEvaluator.Item(Rules, workshop, p) : null;
        List<string> procedure = finished ? CraftJournal.Procedure(workshop, p, CraftKnowledge.For(player)?.Data) : null;
        // its maker is whoever finished it (an item finished before makers were kept: whoever takes it)
        string maker = finished && !string.IsNullOrEmpty(p.assembly.maker) ? p.assembly.maker : MakerName(player);
        // tried while its maker was away, and its maker takes it: their journal notes it now
        bool tellMaker = finished && untold.Contains(id) && player != null && MakerName(player) == maker;
        List<CraftPiece> taken = workshop.RemovePiece(id, out reason);
        if (taken == null || taken.Count == 0)
            return false;
        CraftPiece piece = taken[0];
        List<CraftPiece> parts = taken.GetRange(1, taken.Count - 1);
        CraftJournalData known = player != null ? CraftKnowledge.For(player)?.Data : null;
        ItemSO item = finished
            ? CraftItems.Finished(Rules, report, piece, parts, maker, procedure, IsInspected(id), known)
            : CraftItems.PieceItem(Rules, piece, parts, known);
        if (inspected.Remove(id))
            metaVersion++;
        untold.Remove(id);
        if (item == null)
        {
            reason = "It could not be taken.";
            return false;
        }
        if (tellMaker)
            NoteMade(player, report, procedure);
        Give(player, item, 1);
        if (finished && player != null)
            Tell(player, $"Took {item.Name}.");
        Touch();
        return true;
    }

    /// <summary>
    /// Throws a piece away (scrap): at a test station anything simply goes; at any other station only an item nothing has
    /// gone into yet is put aside (<see cref="CraftWorkshop.PutAside"/>) - the rest is taken, not thrown away.
    /// </summary>
    public bool ScrapPiece(CombatEntity player, string id, out string reason)
    {
        bool ok = testStation ? workshop.RemovePiece(id, out reason) != null : workshop.PutAside(id, out reason);
        if (!ok)
            return false;
        Touch();
        return true;
    }

    private static string MakerName(CombatEntity player)
    {
        string n = player != null ? PartySystem.NameOf(player) : "";
        return string.IsNullOrWhiteSpace(n) ? "" : n.Trim();
    }

    /// <summary>Gives items (what does not fit drops at the station).</summary>
    private void Give(CombatEntity player, ItemSO item, int count)
    {
        if (item == null || count <= 0)
            return;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int left = inv != null ? inv.AddItem(item, count) : count;
        if (left > 0)
        {
            Vector3 at = dropPoint != null ? dropPoint.position : transform.position + transform.forward * 1f + Vector3.up * 0.5f;
            ItemDropManager.Spawn(null, new List<ItemStack> { new ItemStack(item, left) }, at, gameObject);
        }
    }

    // ------------------------------------------------------------------ supplies
    /// <summary>Is <paramref name="item"/> free here (the station's free supplies, or anything at a test station)?</summary>
    public bool IsFreeSupply(ItemSO item)
    {
        if (item == null)
            return false;
        if (freeSupplies != null && freeSupplies.Contains(item))
            return true;
        return testStation && CraftItems.IsRuntime(item) && item is CraftStockItemSO;
    }

    /// <summary>The free supplies (the window lists them first): the station's own, and every stock material at a test station.</summary>
    public List<ItemSO> FreeSupplies()
    {
        var list = new List<ItemSO>();
        if (freeSupplies != null)
            foreach (ItemSO i in freeSupplies)
                if (i != null && !list.Contains(i)) list.Add(i);
        if (testStation)
            foreach (CraftStockItemSO s in CraftItems.AllStock(Rules, Tools))
                if (!list.Contains(s)) list.Add(s);
        return list;
    }

    /// <summary>
    /// How many of a supply (a fuel, flux, agent, thread, oil... by its id) the player can use: -1 = free, 0 = none.
    /// A supply's item is found by the stock item's id, or by the definition's item name.
    /// </summary>
    public int SupplyAvailable(CombatEntity player, string id)
    {
        FindSupply(player, id, out int count);
        return count;
    }

    private string ItemNameOf(string id)
    {
        CraftFuel f = Rules.Fuel(id);
        if (f != null) return string.IsNullOrEmpty(f.item) ? null : f.item;
        CraftSupplyDefinition s = Rules.Supply(id);
        if (s != null) return string.IsNullOrEmpty(s.item) ? s.Name : s.item;
        CraftTanningAgent a = Rules.Agent(id);
        if (a != null) return string.IsNullOrEmpty(a.item) ? null : a.item;
        CraftQuenchant q = Rules.Quenchant(id);
        if (q != null) return string.IsNullOrEmpty(q.item) ? null : q.item;
        return id;
    }

    private ItemSO FindSupply(CombatEntity player, string id, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(id))
            return null;
        // Free: fuel at a woodpile, water, an agent with no item.
        if (Rules.Fuel(id) != null && FreeFuel) { count = -1; return null; }
        CraftTanningAgent agent = Rules.Agent(id);
        if (agent != null && (agent.kind == CraftAgentKind.Water && waterSource || string.IsNullOrEmpty(agent.item))) { count = -1; return null; }
        // A quenchant with no item (water from the well) fills the tank for nothing.
        CraftQuenchant quenchant = agent == null && Rules.Supply(id) == null ? Rules.Quenchant(id) : null;
        if (quenchant != null && string.IsNullOrEmpty(quenchant.item)) { count = -1; return null; }
        string itemName = ItemNameOf(id);
        foreach (ItemSO s in FreeSupplies())
            if (Matches(s, id, itemName)) { count = -1; return s; }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv != null)
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it.itemScriptableObject;
                if (so != null && Matches(so, id, itemName))
                {
                    count = inv.GetItemCount(so);
                    return so;
                }
            }
        // Firewood: any plank, log or wooden blank in the bag burns - what is named as firewood first, then the cheapest
        // (CraftFuel.BurnOrder), never a dear blank while a cheap one is left.
        if (inv != null && Rules.Fuel(id) is CraftFuel fuel && fuel.burnsAnyWood)
        {
            ItemSO best = null;
            double first = double.MaxValue;
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it.itemScriptableObject;
                if (so == null || so == best || !IsWood(so)) continue;
                double order = CraftFuel.BurnOrder(so.Name, so.Price);
                if (order < first)
                {
                    first = order;
                    best = so;
                }
            }
            if (best != null)
            {
                count = inv.GetItemCount(best);
                return best;
            }
        }
        return null;
    }

    /// <summary>
    /// The item a fire that burns any wood takes for <paramref name="fuelId"/> when the player has none of the fuel's own
    /// (a plank burnt as firewood: "Pine plank blank"), so the window can say what goes on the fire; null when the fuel's
    /// own item, or free fuel, is used.
    /// </summary>
    public string BurntAs(CombatEntity player, string fuelId)
    {
        CraftFuel fuel = Rules.Fuel(fuelId);
        if (fuel == null || !fuel.burnsAnyWood)
            return null;
        ItemSO item = FindSupply(player, fuelId, out int count);
        return item != null && count > 0 && !Matches(item, fuelId, ItemNameOf(fuelId)) ? item.Name : null;
    }

    private static readonly string[] WoodWords = { "wood", "firewood", "plank", "planks", "log", "logs", "timber", "lumber", "billet", "branch", "branches", "stick", "sticks", "kindling", "board", "boards", "offcut", "offcuts", "twig", "twigs" };

    /// <summary>Is it plain wood that burns: a wooden blank, or a material named as wood or after a wood (never a tool, weapon or garment)?</summary>
    private bool IsWood(ItemSO item)
    {
        if (item is CraftStockItemSO stock)
            return stock.StockKind == CraftStockKind.Wood;
        if (!(item is MaterialSO))
            return false;
        string name = item.Name ?? ((UnityEngine.Object)item).name ?? "";
        foreach (string word in name.Split(new[] { ' ', '_', '-', '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string w in WoodWords)
                if (string.Equals(word, w, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (CraftWoodDefinition d in Rules.Woods)
                if (string.Equals(word, d.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(word, d.id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private bool Matches(ItemSO item, string id, string itemName)
    {
        if (item is CraftStockItemSO s)
            return string.Equals(s.CraftId, id, StringComparison.OrdinalIgnoreCase) || s.StockKind == CraftStockKind.Supply && SameWords(s.Name, itemName);
        if (item is CraftPieceItemSO)
            return false;
        // An item of the project's own: by its name or asset name, against the supply's item name, its name or its id,
        // ignoring case, punctuation and word order ("Raw Wool", "raw_wool", "Wool, raw").
        CraftSupplyDefinition def = Rules.Supply(id);
        string a = item.Name, b = ((UnityEngine.Object)item).name;
        foreach (string want in new[] { itemName, def?.displayName, def?.id, id })
            if (!string.IsNullOrEmpty(want) && (SameWords(a, want) || SameWords(b, want)))
                return true;
        // a fibre by its plain name ("Wool", "Fleece", "Raw flax")
        if (def != null && def.kind == CraftSupplyKind.Fibre)
        {
            string stem = def.id.Split('_')[0];
            if (string.Equals(FibreWord(a), stem, StringComparison.OrdinalIgnoreCase) || string.Equals(FibreWord(b), stem, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Do two names have the same words (any case, order or punctuation)?</summary>
    public static bool SameWords(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        string[] Split(string x)
        {
            string[] w = x.ToLowerInvariant().Split(new[] { ' ', '_', '-', '(', ')', ',', '.', '\'', '/' }, StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(w, StringComparer.Ordinal);
            return w;
        }
        string[] x1 = Split(a), x2 = Split(b);
        if (x1.Length != x2.Length) return false;
        for (int i = 0; i < x1.Length; i++)
            if (x1[i] != x2[i]) return false;
        return true;
    }

    /// <summary>The fibre an item is (a Fibre supply id), or null: by its supply name, or plainly ("Wool", "Fleece", "Raw flax", "Cotton bale").</summary>
    public string FibreOf(ItemSO item)
    {
        if (item == null) return null;
        if (item is CraftStockItemSO s)
            return s.StockKind == CraftStockKind.Supply && IsFibre(s.CraftId) ? s.CraftId : null;
        if (item is CraftPieceItemSO) return null;
        foreach (CraftSupplyDefinition d in Rules.Supplies)
            if (d.kind == CraftSupplyKind.Fibre && Matches(item, d.id, ItemNameOf(d.id)))
                return d.id;
        return null;
    }

    private static readonly string[] RawWords = { "raw", "fibre", "fiber", "floss", "unspun", "loose", "bale", "bundle", "of", "a", "tow", "hank", "lock", "locks", "greasy", "boll", "bolls" };

    /// <summary>The one word of a name that is not just "raw" or the like (fleece counts as wool), or null.</summary>
    private static string FibreWord(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string found = null;
        foreach (string w in name.ToLowerInvariant().Split(new[] { ' ', '_', '-', '(', ')', ',', '.', '\'', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Array.IndexOf(RawWords, w) >= 0) continue;
            if (found != null) return null;
            found = w == "fleece" ? "wool" : w;
        }
        return found;
    }

    /// <summary>Batches of a fibre (or of any fibre, <paramref name="fibre"/> null) lying on the station, ready to work or spin.</summary>
    public List<CraftPiece> FibreBatches(string fibre = null)
    {
        var list = new List<CraftPiece>();
        if (workshop == null) return list;
        foreach (CraftPiece p in workshop.Pieces)
            if (p.kind == CraftPieceKind.Fibre && p.location == CraftLocation.Air && (fibre == null || string.Equals(p.fibre.fibre, fibre, StringComparison.OrdinalIgnoreCase)))
                list.Add(p);
        return list;
    }

    private bool HaveSupply(CombatEntity player, string id, int count, out string reason)
    {
        reason = "";
        FindSupply(player, id, out int have);
        if (have < 0 || have >= count)
            return true;
        reason = have > 0 ? $"You have {have} {ItemNameOf(id) ?? id}: {count} are needed." : $"You have no {ItemNameOf(id) ?? id}.";
        return false;
    }

    private bool UseSupply(CombatEntity player, string id, int count, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(id))
            return true;
        ItemSO item = FindSupply(player, id, out int have);
        if (have < 0)
            return true;
        if (have < count || item == null)
        {
            reason = $"You have no {ItemNameOf(id) ?? id}.";
            return false;
        }
        InventoryManager.For(player)?.RemoveItems(item, count);
        return true;
    }

    private string FirstOfKind(CraftSupplyKind kind) => Rules.FirstSupply(kind)?.id;

    /// <summary>
    /// The pots each player has open here - a flask of oil, a cake of wax, a jar of dye, a pot of varnish: how much of an
    /// item is left in each. One pot of each supply, whichever bench draws from it (the beeswax that waxed a hide waxes a
    /// bow stave too); a use draws from it, and the next item is opened (used up) only when it will not cover the use.
    /// </summary>
    private readonly CraftOpenPots<CombatEntity> pots = new CraftOpenPots<CombatEntity>();

    /// <summary>The open pot of <paramref name="supply"/>: players who are gone have taken theirs with them.</summary>
    private double PotLeft(CombatEntity player, string supply)
    {
        pots.Prune(who => !ReferenceEquals(who, null) && who == null);
        return pots.Left(player, supply);
    }

    /// <summary>
    /// A stroke of the brush (or of a wax cake) at the finishing bench: the workshop lays it, paid from the player's open
    /// pot of that finish (<see cref="pots"/>, shared with the leather's oils and waxes) - <see cref="CraftWoodwork.BrushUse"/>
    /// of an item a stroke, a new item opened when the pot will not cover it. A free finish costs nothing.
    /// </summary>
    private bool Brush(CombatEntity player, CraftRequest r, out string reason)
    {
        reason = "";
        CraftPiece p = workshop.Piece(r.piece);
        int n = p != null ? p.segments.Count : 0;
        double share = n > 0 ? (Math.Min(n - 1, Math.Max(r.i0, r.i1)) - Math.Max(0, Math.Min(r.i0, r.i1)) + 1) / (double)n : 1;
        double use = CraftWoodwork.BrushUse(share, r.b);
        FindSupply(player, r.id, out int have);
        double left = PotLeft(player, r.id);
        int open = have < 0 ? 0 : pots.ToOpen(player, r.id, use);
        if (have >= 0 && open > have)
        {
            string item = ItemNameOf(r.id) ?? r.id;
            reason = left > 0.02 ? $"What is left of the {item} you opened will not cover that stroke, and you have no more: make a shorter one." : $"You have no {item}.";
            return false;
        }
        if (!workshop.Varnish(r.piece, r.id, r.i0, r.i1, r.a, r.b, out reason))
            return false;
        if (have < 0)
            return true; // free here
        if (open > 0 && !UseSupply(player, r.id, open, out reason))
            return false;
        pots.Use(player, r.id, use);
        return true;
    }

    public bool AddFuel(CombatEntity player, string fuelId, int count, bool furnace, out string reason)
    {
        reason = "";
        CraftFuel fuel = Rules.Fuel(fuelId);
        if (fuel == null)
        {
            reason = "That does not burn here.";
            return false;
        }
        FindSupply(player, fuelId, out int have);
        if (have == 0)
        {
            reason = fuel.burnsAnyWood
                ? $"You have no {fuel.Name}: any wood burns as it (planks, logs, offcuts, wooden blanks)."
                : $"You have no {ItemNameOf(fuelId) ?? fuel.Name}.{FuelHint(player, fuelId)}";
            return false;
        }
        if (have > 0) count = Math.Min(count, have);
        if (!workshop.AddFuel(fuel.id, count, out reason, furnace))
            return false;
        if (have > 0) UseSupply(player, fuelId, count, out _);
        return true;
    }

    /// <summary>" You do have Firewood." - the fuels the player could put on instead.</summary>
    private string FuelHint(CombatEntity player, string except)
    {
        var have = new List<string>();
        foreach (CraftFuel f in Rules.Fuels)
            if (f.id != except && SupplyAvailable(player, f.id) != 0) have.Add(f.Name);
        return have.Count > 0 ? $" You do have {string.Join(", ", have)}." : " The fire takes charcoal, coal, coke, or firewood (any wood: planks, logs, offcuts).";
    }

    private bool Flux(CombatEntity player, string id, int count, int where, string piece, out string reason, double coverage = 1, List<float> perSegment = null)
    {
        if (!HaveSupply(player, id, count, out reason))
            return false;
        // On a piece (2): sprinkled stretch by stretch where the hand covered it.
        if (where >= 2 ? !workshop.FluxPiece(id, count, piece, out reason, coverage, perSegment) : !workshop.AddFlux(id, count, where, out reason, piece, coverage))
            return false;
        return UseSupply(player, id, count, out reason);
    }

    /// <summary>Fills the quench tank with one of the station's quenchants (<see cref="Quenchants"/>), its item used up (water is free).</summary>
    private bool FillTank(CombatEntity player, string quenchant, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.QuenchTank))
        {
            reason = "There is no quench tank here.";
            return false;
        }
        bool offered = false;
        foreach (string q in Quenchants)
            offered |= string.Equals(q, quenchant, StringComparison.OrdinalIgnoreCase);
        if (!offered || Rules.Quenchant(quenchant) == null)
        {
            reason = "The tank is not filled with that here.";
            return false;
        }
        string id = Rules.Quenchant(quenchant).id;
        return HaveSupply(player, id, 1, out reason) && workshop.FillTank(id, out reason) && UseSupply(player, id, 1, out reason);
    }

    private bool FillVat(CombatEntity player, int vat, string agent, int doses, out string reason)
    {
        if (!HaveSupply(player, agent, doses, out reason))
            return false;
        if (!workshop.FillVat(vat, agent, doses, out reason))
            return false;
        if (player != null) CraftKnowledge.For(player)?.UsedAgent(agent);
        return UseSupply(player, agent, doses, out reason);
    }

    /// <summary>
    /// Oil, wax or dye worked into leather: what the leather takes up (<see cref="CraftLeather.Uptake"/>, in cell-loads; one
    /// item covers <see cref="CraftSupplyDefinition.cellsPerItem"/> cells) comes out of the player's open pot of it
    /// (<see cref="pots"/>, shared with the finishing bench's oils and waxes), a new item opened whenever that will not
    /// cover it - so a careful stroke and a scribble over the whole hide cost by what they cover, and a stroke that takes
    /// nothing in costs nothing.
    /// </summary>
    private bool FinishLeather(CombatEntity player, CraftRequest r, out string reason)
    {
        bool dye = r.kind == CraftRequestKind.Dye;
        CraftSupplyDefinition s = Rules.Supply(r.id);
        CraftPiece p = workshop.Piece(r.piece);
        if (s == null || p == null || p.kind != CraftPieceKind.Hide)
            return dye ? workshop.Dye(r.piece, r.id, r.points, r.a, out reason) : workshop.Condition(r.piece, r.id, r.points, r.a, out reason); // it says why not
        double use = CraftLeather.Uptake(p.hide, s, r.points, r.a) / Math.Max(0.1, s.cellsPerItem); // in items
        FindSupply(player, s.id, out int have);
        double left = PotLeft(player, s.id);
        int open = have < 0 ? 0 : pots.ToOpen(player, s.id, use);
        if (have >= 0 && open > have)
        {
            string item = ItemNameOf(s.id) ?? s.Name;
            reason = have > 0 ? $"That stroke would take {open} {item}: you have {have}. Work it in a little at a time."
                : left > 0.02 ? $"What is left of the {item} you opened will not cover that stroke, and you have no more: make a shorter one."
                : $"You have no {item}.";
            return false;
        }
        if (!(dye ? workshop.Dye(r.piece, r.id, r.points, r.a, out reason) : workshop.Condition(r.piece, r.id, r.points, r.a, out reason)))
            return false;
        if (have < 0)
            return true; // free here
        if (open > 0 && !UseSupply(player, s.id, open, out reason))
            return false;
        pots.Use(player, s.id, use);
        return true;
    }

    private bool Attach(CombatEntity player, CraftRequest r, out string reason)
    {
        CraftPiece asm = workshop.Piece(r.piece);
        CraftDesign design = asm != null && asm.kind == CraftPieceKind.Assembly ? Rules.Design(asm.assembly.design) : null;
        CraftComponentSpec spec = design?.Component(r.id);
        if (spec != null && spec.supply)
        {
            // Thread and padding come from the inventory: the one chosen (r.other), else the first of its kind the player has.
            CraftSupplyDefinition s = Rules.Supply(r.other);
            if (s != null && s.kind != spec.supplyKind || s == null && !string.IsNullOrEmpty(r.other))
            {
                reason = $"That is not {spec.Name.ToLowerInvariant()}.";
                return false;
            }
            s ??= SupplyOfKind(player, spec.supplyKind);
            // never more than the item still takes (what is added is not taken back out)
            int room = CraftAssembly.SupplyRoom(design, asm, spec.role);
            if (room <= 0)
                return workshop.Attach(r.piece, r.id, 0, null, out reason, 1, s?.strength ?? 0, s?.id); // it says why not
            int n = Math.Min(Math.Max(1, r.count), room);
            if (s != null && !HaveSupply(player, s.id, n, out reason))
                return false;
            // named, so the item weighs and is worth what went into it
            if (!workshop.Attach(r.piece, r.id, 0, null, out reason, n, s?.strength ?? 0, s?.id))
                return false;
            return s == null || UseSupply(player, s.id, n, out reason);
        }
        return workshop.Attach(r.piece, r.id, r.i0, r.other, out reason);
    }

    /// <summary>
    /// Spins <paramref name="fibre"/> from the bag: one item of it is laid on the station as a batch, as it comes (raw:
    /// greasy, dirty, tangled), and a bobbin of yarn (<see cref="CraftFibre.BobbinGrams"/> of it) is spun from the batch
    /// with the hands' draft (<paramref name="evenness"/>, <paramref name="twist"/>, <paramref name="fineness"/>) - the same
    /// as spinning a batch from the fibre bench (<see cref="CraftFibre.Spin"/>); the rest of the batch stays on the bench
    /// for the next bobbins. In one request, so a network client never waits for the batch to appear before it spins.
    /// </summary>
    private bool Spin(CombatEntity player, string fibre, double evenness, double twist, double fineness, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.SpinningWheel)) { reason = "There is no spinning wheel here."; return false; }
        CraftSupplyDefinition f = Rules.Supply(fibre);
        if (f == null || f.kind != CraftSupplyKind.Fibre) { reason = "That cannot be spun."; return false; }
        if ((f.spinsInto == null || f.spinsInto.Count == 0) && string.IsNullOrEmpty(f.weavesInto)) { reason = "Nothing spins from it."; return false; } // its yarn would reel into no thread and weave into no cloth
        CraftPiece batch = CraftFibre.NewFibre(Rules, f.id);
        if (batch == null) { reason = "That cannot be spun."; return false; }
        if (!HaveSupply(player, f.id, 1, out reason) || !UseSupply(player, f.id, 1, out reason))
            return false;
        string id = workshop.AddPiece(batch);
        // The batch stays on the bench whatever happens to the spinning (nothing taken from the bag is lost).
        return workshop.SpinFibre(id, evenness, twist, fineness, CraftFibre.BobbinGrams, out reason) != null;
    }

    // ------------------------------------------------------------------ fibre, yarn and gems
    private bool IsFibre(string id) => Rules.Supply(id)?.kind == CraftSupplyKind.Fibre;

    /// <summary>Lays <paramref name="count"/> items of a fibre on the fibre bench, each a batch of its own.</summary>
    private bool PutFibre(CombatEntity player, string fibre, int count, out string reason)
    {
        reason = "";
        if ((Tools & (CraftTools.FibreBench | CraftTools.SpinningWheel)) == 0) { reason = "There is no fibre bench or spinning wheel here."; return false; }
        int done = 0;
        for (int k = 0; k < count; k++)
        {
            if (!HaveSupply(player, fibre, 1, out reason) || !UseSupply(player, fibre, 1, out reason))
                break;
            CraftPiece p = CraftFibre.NewFibre(Rules, fibre);
            if (p == null) { reason = "That is not a fibre."; break; }
            workshop.AddPiece(p);
            done++;
        }
        if (done > 0) reason = "";
        return done > 0;
    }

    /// <summary>
    /// Reels a measured skein off as thread: one thread item for every <see cref="CraftFibre.MetresPerThread"/> metres
    /// counted (never more than is really there), coarse, even or fine as the yarn is good.
    /// </summary>
    private bool ReelYarn(CombatEntity player, string id, out string reason)
    {
        reason = "";
        CraftPiece p = workshop.Piece(id);
        if (p == null || p.kind != CraftPieceKind.Yarn) { reason = "That is not yarn."; return false; }
        if (!p.yarn.measured) { reason = "Wind it into a skein and count it first: its length must be known."; return false; }
        int n = CraftFibre.ThreadCount(p);
        string thread = CraftFibre.ThreadOf(Rules, p);
        if (n < 1 || string.IsNullOrEmpty(thread)) { reason = $"There is not enough of it to reel ({CraftFibre.MetresPerThread:0} m a skein)."; return false; }
        if (workshop.RemovePiece(id, out reason) == null)
            return false;
        Give(player, FindSupply(player, thread, out _) ?? CraftItems.Stock(Rules, CraftStockKind.Supply, thread), n);
        Tell(player, $"Reeled into {n} × {Rules.Supply(thread)?.Name.ToLowerInvariant() ?? thread}.");
        return true;
    }

    /// <summary>The instrument each gem test needs.</summary>
    public static CraftInstruments InstrumentFor(CraftGemTest t) => t switch
    {
        CraftGemTest.Loupe => CraftInstruments.Loupe,
        CraftGemTest.Scratch => CraftInstruments.HardnessPoints,
        CraftGemTest.Refractometer => CraftInstruments.Refractometer,
        CraftGemTest.Gravity => CraftInstruments.Scale,
        CraftGemTest.Dichroscope => CraftInstruments.Dichroscope,
        _ => CraftInstruments.Loupe,
    };

    /// <summary>An instrument in words ("set of hardness points", "balance"): the station window lists them so, and refusals name the one missing.</summary>
    public static string InstrumentName(CraftInstruments i) => i switch
    {
        CraftInstruments.Thermometer => "pyrometer",
        CraftInstruments.Calipers => "calipers",
        CraftInstruments.Scale => "balance", // weighed in air and in water, it reads a stone's specific gravity too
        CraftInstruments.Loupe => "loupe",
        CraftInstruments.Magnet => "magnet",
        CraftInstruments.TestFile => "test file",
        CraftInstruments.Assay => "assay kit",
        CraftInstruments.StreakPlate => "streak plate",
        CraftInstruments.Refractometer => "refractometer",
        CraftInstruments.HardnessPoints => "set of hardness points",
        CraftInstruments.MoistureMeter => "moisture meter",
        CraftInstruments.Dichroscope => "dichroscope",
        _ => i.ToString().ToLowerInvariant(),
    };

    private bool TestGem(CraftRequest r, out string reason)
    {
        var test = (CraftGemTest)r.i0;
        if (!HasInstrument(InstrumentFor(test)))
        {
            reason = $"There is no {InstrumentName(InstrumentFor(test))} here.";
            return false;
        }
        return workshop.TestGem(r.piece, test, r.a, r.b, out reason);
    }

    private bool IdentifyGem(CombatEntity player, string id, string gem, out string reason)
    {
        if (!workshop.IdentifyGem(id, gem, out reason))
            return false;
        CraftPiece p = workshop.Piece(id);
        if (player != null && p != null && p.gem.identified)
            CraftKnowledge.For(player)?.IdentifiedGem(Rules, p.gem.gem);
        return true;
    }

    /// <summary>
    /// Polishes a stone: the compound comes from the inventory - one charges the lap for the stone, the first time it is
    /// polished with it; another compound (one the player has, or it is refused) charges the lap afresh
    /// (<see cref="GemState.compound"/>). Passes with the compound the lap is charged with need nothing more.
    /// </summary>
    private bool PolishGem(CombatEntity player, CraftRequest r, out string reason)
    {
        CraftPiece p = workshop.Piece(r.piece);
        bool charge = p != null && p.kind == CraftPieceKind.Gem && CraftLapidary.ChargesLap(p.gem, r.id);
        if (charge && !HaveSupply(player, r.id, 1, out reason))
            return false;
        if (!workshop.PolishGem(r.piece, r.id, Math.Max(1, r.count), r.a, r.b, out reason, r.points))
            return false;
        return !charge || UseSupply(player, r.id, 1, out reason);
    }

    // ------------------------------------------------------------------ finishing and testing
    private bool Finish(CombatEntity player, string id, out string reason)
    {
        CraftPiece asm = workshop.Piece(id);
        CraftDesign design = asm != null && asm.kind == CraftPieceKind.Assembly ? Rules.Design(asm.assembly.design) : null;
        CraftDiscipline craft = CraftMastery.OfDesign(design);
        // the finisher's practice goes into it - a master's finishing touches - and they are its maker
        if (!workshop.Finish(id, out reason, Mastery(player, craft), MakerName(player)))
            return false;
        if (player != null && asm != null)
        {
            CraftItemReport report = CraftEvaluator.Item(Rules, workshop, asm);
            CraftKnowledge k = CraftKnowledge.For(player);
            // each one of a design already made teaches less (the journal's note of the design)
            int made = k != null && design != null ? CraftJournal.DesignNote(k.Data, design.id)?.timesMade ?? 0 : 0;
            k?.Practised(craft, CraftMastery.ItemExperience(design, report.Quality, MasterySettings, made), false, true, MasterySettings);
            // How good it is shows when it is tried (here, or later from the bag): only then does the journal note it. A
            // jewel is buffed and looked over as it is finished (the jeweller's Inspect & finish); a test station shows all.
            if (testStation || design?.kind == CraftDesignKind.Jewelry)
                Tried(player, asm, report);
        }
        return true;
    }

    /// <summary>
    /// A finished item on the bench is tried, the first time: its numbers are known from now on (here, and on the item
    /// when it is taken), and its maker's journal notes it as made - by the name they know it by - wherever they are in
    /// the world; whoever tries it only learns what it is. A maker away hears of it when they take it.
    /// </summary>
    private void Tried(CombatEntity player, CraftPiece asm, CraftItemReport report)
    {
        if (!inspected.Add(asm.id))
            return;
        metaVersion++;
        CombatEntity maker = MakerOf(asm.assembly.maker, player);
        CraftKnowledge k = maker != null ? CraftKnowledge.For(maker) : null;
        if (k == null)
        {
            untold.Add(asm.id);
            return;
        }
        NoteMade(maker, report, CraftJournal.Procedure(workshop, asm, k.Data));
    }

    /// <summary>The journal of <paramref name="maker"/> notes the item as made, by the name they know it by.</summary>
    private void NoteMade(CombatEntity maker, CraftItemReport report, List<string> procedure)
    {
        CraftKnowledge k = maker != null ? CraftKnowledge.For(maker) : null;
        if (k == null || report == null)
            return;
        report.Name = CraftEvaluator.KnownName(Rules, report, k.Data);
        k.Made(report, procedure);
    }

    /// <summary>
    /// The player who made an item (<paramref name="maker"/>: their name; empty for an item whose maker was never kept - then
    /// whoever is at it, <paramref name="at"/>), when they are in the world; else null.
    /// </summary>
    private static CombatEntity MakerOf(string maker, CombatEntity at)
    {
        if (string.IsNullOrEmpty(maker) || at != null && MakerName(at) == maker)
            return at;
        foreach (CombatEntity p in CombatEntity.Players)
            if (p != null && MakerName(p) == maker)
                return p;
        return null;
    }

    /// <summary>The first supply of a kind the player has (or that is free here), else the first of its kind.</summary>
    private CraftSupplyDefinition SupplyOfKind(CombatEntity player, CraftSupplyKind kind)
    {
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == kind && SupplyAvailable(player, s.id) != 0) return s;
        return Rules.FirstSupply(kind);
    }

    /// <summary>
    /// The final inspection of a finished item: it is flexed, struck, its edge tried, its straps pulled - and what it
    /// is shows (its numbers are then known, here and on the item).
    /// </summary>
    private bool Inspect(CombatEntity player, string id, out string reason)
    {
        reason = "";
        CraftPiece asm = workshop.Piece(id);
        if (asm == null || asm.kind != CraftPieceKind.Assembly || !asm.assembly.finished)
        {
            reason = "Only a finished item can be tried.";
            return false;
        }
        CraftItemReport report = CraftEvaluator.Item(Rules, workshop, asm);
        CraftJournalData known = player != null ? CraftKnowledge.For(player)?.Data : null;
        string name = CraftEvaluator.KnownName(Rules, report, known);
        Tried(player, asm, report);
        Tell(player, $"{name}: {report.Tier} work.");
        return true;
    }

    /// <summary>
    /// Tries a finished item the player carries in the bag (the final inspection, made later): its numbers show on it from
    /// now on, on every machine, and its maker's journal notes it as made (the maker in the world; whoever else tries it only
    /// learns what it is). At a bench where such things are made (or a test station).
    /// </summary>
    private bool InspectItem(CombatEntity player, ItemSO item, out string reason)
    {
        reason = "";
        CraftedItemRecord rec = (item as ICraftedItem)?.Record;
        if (rec == null)
        {
            reason = "Only a finished item can be tried.";
            return false;
        }
        CraftDesign d = Rules.Design(rec.designId);
        if (d == null || !testStation && !Makes(d))
        {
            reason = "It is not tried here: take it to a bench where such things are made.";
            return false;
        }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null || inv.GetItemCount(item) <= 0)
        {
            reason = "It is not in your bag.";
            return false;
        }
        if (rec.inspected)
        {
            reason = "It has been tried: what it is shows on it.";
            return false;
        }
        CraftItems.Try((ICraftedItem)item);
        // its maker's journal notes it - wherever they are in the world - from what the item remembers (its name as it was
        // known when it was taken): someone else's work tried here only shows what it is
        CombatEntity maker = MakerOf(rec.maker, player);
        CraftKnowledge k = maker != null ? CraftKnowledge.For(maker) : null;
        if (k != null)
        {
            var report = new CraftItemReport { Design = d, Name = rec.name, Quality = rec.quality, Tier = rec.tier, Assembly = rec.assembly };
            report.Notes.AddRange(rec.notes);
            k.Made(report, rec.procedure);
        }
        Tell(player, $"{rec.name}: {rec.tier} work.");
        return true;
    }

    /// <summary>The assay kit: names the metal (or ore) and shows what it is made of.</summary>
    private bool Assay(CombatEntity player, string id, out string reason)
    {
        reason = "";
        if (!HasInstrument(CraftInstruments.Assay))
        {
            reason = "There is no assay kit here.";
            return false;
        }
        CraftPiece p = workshop.Piece(id);
        if (p == null)
        {
            reason = "There is nothing to assay.";
            return false;
        }
        CraftKnowledge k = player != null ? CraftKnowledge.For(player) : null;
        if (p.kind == CraftPieceKind.Ore)
        {
            if (k != null)
            {
                CraftOreNote n = CraftJournal.OreNote(k.Data, p.ore.ore, true);
                bool newly = !n.identified;
                n.identified = true;
                k.MarkDirty();
                if (newly) Tell(player, $"The assay names it: {Rules.Ore(p.ore.ore)?.Name}.");
            }
            return true;
        }
        if (p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Fasteners)
        {
            reason = "Only metal and ore can be assayed.";
            return false;
        }
        k?.Assayed(Rules, p.Profile(Rules));
        return true;
    }

    /// <summary>What the assay shows of a piece (its elements, the main ones first).</summary>
    public List<string> AssayLines(string id, CraftJournalData known)
    {
        var lines = new List<string>();
        CraftPiece p = workshop?.Piece(id);
        if (p == null)
            return lines;
        if (p.kind == CraftPieceKind.Ore)
        {
            CraftOreDefinition o = Rules.Ore(p.ore.ore);
            lines.Add($"{o?.Name ?? p.ore.ore}: about {p.ore.MineralMass / Math.Max(1, p.ore.Mass) * 100:0}% ore in the rock.");
            foreach (CraftOreYield y in o != null ? o.yields : new List<CraftOreYield>())
                lines.Add($"{Rules.ElementName(y.element)}: about {CraftOres.MetalContent(Rules, p, y.element) * 100:0.#}%");
            return lines;
        }
        double total = 0;
        foreach (CraftAmount a in p.composition) total += a.grams;
        var parts = new List<CraftAmount>(p.composition);
        parts.Sort((x, y) => y.grams.CompareTo(x.grams));
        foreach (CraftAmount a in parts)
            if (total > 0 && a.grams / total > 0.0005)
                lines.Add($"{Rules.ElementName(a.element)}: {a.grams / total * 100:0.##}%");
        return lines;
    }

    // ------------------------------------------------------------------ messages
    public void Tell(CombatEntity player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text))
            return;
        if (CardTable.IsLocalPlayer(player))
        {
            InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
            LocalMessage?.Invoke(text);
        }
        else if (Net != null)
            Net.Tell(player, text);
        else
            InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info); // a session's inventory forwards it to its owner
    }

    // ------------------------------------------------------------------ saving
    [Serializable]
    private class StationSave
    {
        public int version = 1;
        public CraftWorkshopState workshop;
        public List<string> inspected = new List<string>();
        public List<string> untold = new List<string>();
        public int freeLump;
    }

    private string SavePath
    {
        get
        {
            string id = string.IsNullOrEmpty(stationId) ? gameObject.scene.name + "_" + name : stationId;
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, "Crafting", "Stations", id + ".json");
        }
    }

    /// <summary>Writes the workshop to disk (its state; the history is not needed to continue).</summary>
    public void SaveWorkshop()
    {
        if (!Application.isPlaying || workshop == null || IsMirror)
            return;
        try
        {
            CraftWorkshopState s = workshop.Capture(false);
            var save = new StationSave { workshop = s, inspected = new List<string>(inspected), untold = new List<string>(untold), freeLump = freeLump };
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(save));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not save the workshop of {DisplayName}: {e.Message}", this);
        }
    }

    private void LoadWorkshop()
    {
        if (!Application.isPlaying || IsMirror)
            return;
        try
        {
            string path = SavePath;
            if (!File.Exists(path))
                return;
            StationSave save = JsonUtility.FromJson<StationSave>(File.ReadAllText(path));
            if (save?.workshop == null)
                return;
            CraftWorkshop w = CraftWorkshop.Restore(Rules, save.workshop);
            if (w.Tools != Tools)
                w = CraftWorkshop.Restore(Rules, Retool(save.workshop));
            SetWorkshop(w);
            inspected.Clear();
            if (save.inspected != null) inspected.UnionWith(save.inspected);
            untold.Clear();
            if (save.untold != null) untold.UnionWith(save.untold);
            freeLump = save.freeLump;
            metaVersion++;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not restore the workshop of {DisplayName}: {e.Message}", this);
        }
    }

    private CraftWorkshopState Retool(CraftWorkshopState s)
    {
        s.tools = (int)Tools;
        return s;
    }

    /// <summary>Replaces the workshop with a saved one (editor tools, network snapshots, your save system).</summary>
    public void LoadState(CraftWorkshopState state)
    {
        if (!RulesHere)
            return;
        SetWorkshop(state != null ? CraftWorkshop.Restore(Rules, state) : NewWorkshop());
    }

    /// <summary>Empties the workshop (test stations, debugging).</summary>
    public void ClearWorkshop()
    {
        if (!RulesHere)
            return;
        inspected.Clear();
        untold.Clear();
        metaVersion++;
        SetWorkshop(NewWorkshop());
    }

    // ------------------------------------------------------------------ interaction
    public string InteractionName => DisplayName;
    public string InteractionVerb => "Work";
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRange => range;
    public int InteractionPriority => 5;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        CombatEntity player = PlayerOf(interactor);
        if (player == null)
            return false;
        if (tender != null && tender != player && tender.isActiveAndEnabled)
        {
            reason = "Someone is working here";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = PlayerOf(interactor);
        if (player == null)
            return;
        if (CardTable.IsLocalPlayer(player))
        {
            CraftStationUI.Show(this, player);
            return;
        }
        // A remote player (the server got the interaction from their client): the window opens on their screen.
        if (!RulesHere)
            return;
        if (Net != null && Net.Open(player))
            BeginTending(player);
        else
            Tell(player, "This station is not set up for multiplayer: only the host can work here.");
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    /// <summary>Configuration problems (the inspector lists them).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (GetComponentInChildren<Collider>() == null)
            errors.Add("No collider on the station or its children: the Player Interactor cannot find it.");
        if (Tools == CraftTools.None)
            errors.Add("The station has no tools.");
        if (Has(CraftTools.QuenchTank) && Rules.Quenchant(quenchMedium) == null)
            errors.Add($"Unknown quenchant '{quenchMedium}'.");
        foreach (string q in quenchants)
            if (Rules.Quenchant(q) == null) errors.Add($"Unknown quenchant '{q}'.");
        if (Has(CraftTools.QuenchTank))
        {
            // the tank's quenchants all harsh (water, brine): a blade of hardening steel cracks in every one of them
            bool gentle = false;
            foreach (string q in Quenchants)
                gentle |= Rules.Quenchant(q) != null && CraftThermal.CrackHarshness(Rules.Quenchant(q)) < CraftThermal.HarshQuench;
            if (!gentle)
                warnings.Add("The quench tank can only be filled with harsh quenchants (water, brine): a blade of hardening steel cracks in them. Add 'oil' to the Quenchants.");
        }
        if (Has(CraftTools.Vats) && vats <= 0)
            warnings.Add("A tannery without vats: hides cannot be limed or tanned.");
        if (Has(CraftTools.Forge) && !Has(CraftTools.Anvil))
            warnings.Add("A forge without an anvil: metal can be heated but not forged.");
        CraftStationVisuals look = GetComponentInChildren<CraftStationVisuals>();
        if (look != null)
        {
            var unmodelled = new List<CraftHandsOnMode>();
            look.UnmodelledPlaces(unmodelled);
            foreach (CraftHandsOnMode place in unmodelled)
                warnings.Add($"Craft Station Visuals: the point of the {place} work place is set, but no 'Worked by hand' model for it - the 3D work place is built there among your own models (what stands right there is hidden by where it stands). Set its model to hide exactly that.");
        }
        if ((Has(CraftTools.Forge) || Has(CraftTools.Furnace)) && !FreeFuel)
            warnings.Add("No free fuel: players need fuel items (Charcoal, Coal, Firewood) to light the fire.");
        foreach (string e in Rules.CompileErrors) errors.Add("Rules: " + e);
    }
}
