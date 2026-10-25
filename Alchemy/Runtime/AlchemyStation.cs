using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>What a player asks a station to do (one value type, so a network layer can carry it). Add new kinds at the END.</summary>
public enum AlchemyRequestKind : byte
{
    AddItem, AddWater, SetHeat, Stir, Suspend, Resume, Bottle, CollectDistillate, Empty,
    AddFuel, Bellows, SetDamper, SetStirring, SetBath, Filter, Decant, Skim, CleanVessel,
}

/// <summary>How a station's vessel is heated.</summary>
public enum AlchemyFireControl
{
    /// <summary>A real fire: feed it fuel, work the bellows, open or choke the air vent (the fire minigame).</summary>
    Manual,
    /// <summary>A burner with a valve: a heat slider sets the temperature it drives toward.</summary>
    Regulated,
}

public struct AlchemyRequest
{
    public AlchemyRequestKind kind;
    /// <summary>AddItem: the item (offline / host).</summary>
    public ItemSO item;
    /// <summary>AddItem: the item's Item Database id (network).</summary>
    public string itemId;
    public int count;
    public float value;
    /// <summary>AddItem: the form it goes in as ("coarse", "ground", "powder" when ground with the station's mortar); null = its own.</summary>
    public string form;
    /// <summary>AddItem: how evenly it was ground (0-1).</summary>
    public float evenness;
    /// <summary>AddFuel: the fuel id; Filter: the filter id.</summary>
    public string id;

    public AlchemyRequest(AlchemyRequestKind kind, float value = 0f, int count = 0, ItemSO item = null, string form = null, float evenness = 1f)
    {
        this.kind = kind;
        this.value = value;
        this.count = count;
        this.item = item;
        this.form = form;
        this.evenness = evenness;
        id = null;
        itemId = item != null ? ItemDatabase.Id(item) : null;
    }

    /// <summary>A request about a fuel or a filter (by id).</summary>
    public static AlchemyRequest For(AlchemyRequestKind kind, string id, int count = 1) =>
        new AlchemyRequest(kind, 0f, count) { id = id };
}

/// <summary>
/// The network side of a station (<c>GameNet.NetAlchemyStation</c>): null offline, where the station runs everything
/// itself. The server runs the preparation; a client keeps a copy that follows it and sends what its player does as
/// requests, which the server checks and answers.
/// </summary>
public interface IAlchemyStationNet
{
    /// <summary>True where the station's rules run (the server).</summary>
    bool RunsStationRules { get; }
    /// <summary>Client: sends what the player did to the server.</summary>
    void SendRequest(AlchemyRequest request);
    /// <summary>Client: the player starts (true) or stops using the station (the window opened or closed).</summary>
    void SendTending(bool tending);
    /// <summary>Server: a message for a remote player.</summary>
    void Tell(CombatEntity player, string text);
    /// <summary>Server: opens the station's window on a remote player's screen; false when the player cannot be reached.</summary>
    bool Open(CombatEntity player);
}

/// <summary>
/// An alchemy workplace: a vessel (<see cref="AlchemyEquipment"/>: cauldron, alembic, retort, basin) over a fire,
/// where players put ingredients in, control the heat, stir, and take out what the mixture has become.
/// <list type="bullet">
/// <item><b>Rules</b>: the preparation is an <see cref="AlchemyBrew"/> run by the station's Alchemy Database. It keeps
/// transforming continuously - also while nobody is watching (Run While Away) - in fixed steps, whatever the frame rate.</item>
/// <item><b>Inventory</b>: ingredients come from the player's inventory (or the station's free supplies); bottles go
/// back to it (what does not fit drops at the station). An optional Bottle Item (an empty vial) is used per bottle.</item>
/// <item><b>Knowledge</b>: the player tending the station sees its reactions and learns (<see cref="AlchemyKnowledge"/>).</item>
/// <item><b>Interrupting</b>: Pause stops time for the preparation (its state is kept exactly); turning the fire off is
/// the in-world way - it cools and keeps reacting by the rules. Preparations are saved with the scene's station id and
/// continue where they were after a restart (Persist Preparation).</item>
/// <item><b>Presentation</b> stays out of here: <see cref="AlchemyStationUI"/> (the window) and
/// <see cref="AlchemyStationVisuals"/> (liquid, bubbles, steam, sounds) read the state and listen to <see cref="BrewEvent"/>.</item>
/// <item><b>Network sessions</b>: with a NetworkObject and a <c>NetAlchemyStation</c>, the server runs the preparation
/// and decides everything (ingredients taken, bottles given, what is learnt); every client keeps a copy that follows the
/// server's (<see cref="IsMirror"/>), so the liquid, the steam and the window show the same everywhere. Without one, only
/// the server's (the host's) player can brew here.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Station")]
public class AlchemyStation : MonoBehaviour, IInteractable
{
    [Header("Station")]
    [Tooltip("Name in the prompt and the window (empty = the vessel's name).")]
    [SerializeField] private string stationName = "";
    [Tooltip("The rules. Empty = the project's Alchemy Database (or the built-in alchemy).")]
    [SerializeField] private AlchemyDatabase database;
    [Tooltip("The vessel. Empty = an iron cauldron.")]
    [SerializeField] private AlchemyEquipment equipment;
    [Tooltip("Where the prompt is shown and the range measured from (empty = this object).")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("How close the player must be to use it (metres).")]
    [SerializeField, Min(0.5f)] private float range = 2.5f;

    [Header("What the window shows")]
    [Tooltip("A test bench: the window shows what the brew would become if bottled now with its quality, strength and " +
             "purity, the amounts of what is in the vessel and the reactions by name. Off (a real workshop): only what can be " +
             "seen and smelt - colour, clarity, layers, what floats or has settled, bubbles, steam, smoke - and the result and " +
             "its quality are only known once it is bottled. Free Default Ingredients makes a station a test bench.")]
    [SerializeField] private bool testBench;
    [Tooltip("A thermometer in the vessel: the window shows the temperature in degrees (off = only how hot it looks and feels).")]
    [SerializeField] private bool thermometer = true;

    [Header("Fire")]
    [Tooltip("Manual: a real fire - players feed it fuel, work the bellows and open or choke the air vent, and must keep " +
             "the temperature where they want it. Regulated: a burner with a valve (a heat slider).")]
    [SerializeField] private AlchemyFireControl fireControl = AlchemyFireControl.Manual;
    [Tooltip("Firewood at hand: free firewood for the fire (else players need Firewood items).")]
    [SerializeField] private bool woodpile = true;

    [Header("Tools")]
    [Tooltip("A mortar and pestle: whole ingredients can be ground before they go in (the grinding minigame) - partially, " +
             "finely or to a powder: they give up their substances faster, the unwanted ones too.")]
    [SerializeField] private bool hasMortar = true;
    [Tooltip("A pan of water to stand the vessel in: gentle, even heat (at most ~96 °C), and nothing scorches on the bottom.")]
    [SerializeField] private bool hasWaterBath = true;
    [Tooltip("A tub of ice: cools the contents fast toward 0 °C (preserves what heat destroys; crash-cooling makes powders, not crystals).")]
    [SerializeField] private bool hasIceBath = true;
    [Tooltip("The filters at hand (ids of the database's Filters: strainer, paper, charcoal); empty = all. A filter with a " +
             "medium also uses up that item (Filter Paper, Charcoal).")]
    [SerializeField] private List<string> filters = new List<string>();
    [Tooltip("The vessel keeps a film of residue when emptied: players clean it, or the next preparation starts with it.")]
    [SerializeField] private bool vesselGetsDirty = true;

    [Header("Supplies")]
    [Tooltip("The station has water at hand (a well, a barrel): the window has a Pour Water button.")]
    [SerializeField] private bool waterSource = true;
    [Tooltip("Units poured per press.")]
    [SerializeField, Min(0.5f)] private float waterPerPour = 10f;
    [Tooltip("Temperature of that water (°C).")]
    [SerializeField] private float waterTemperature = 15f;
    [Tooltip("Ingredients the station gives for free (a teaching lab, a test bench).")]
    [SerializeField] private List<ItemSO> freeIngredients = new List<ItemSO>();
    [Tooltip("Testing: every built-in ingredient is a free supply (items made at runtime, single player).")]
    [SerializeField] private bool freeDefaultIngredients;

    [Header("Bottling")]
    [Tooltip("An item used up per bottle filled (an empty vial). Empty = bottles are free.")]
    [SerializeField] private ItemSO bottleItem;
    [Tooltip("Where items that do not fit in the inventory are dropped (empty = in front of the station).")]
    [SerializeField] private Transform dropPoint;

    [Header("Simulation")]
    [Tooltip("The preparation keeps transforming while nobody tends the station (off = time stops for it when the window closes).")]
    [SerializeField] private bool runWhileAway = true;
    [Tooltip("Seed of the preparations' random variable (only reactions with Rate Variance use it).")]
    [SerializeField] private int seed = 1;
    [Tooltip("Each preparation gets a new seed (results then vary a little between identical preparations with Rate Variance).")]
    [SerializeField] private bool newSeedEachPreparation;
    [Tooltip("Seconds between readings of the surroundings (weather, time of day, condition sources).")]
    [SerializeField, Min(0.05f)] private float conditionsInterval = 0.5f;
    [Tooltip("Most simulation steps run in one frame (after a hitch the preparation catches up over several frames).")]
    [SerializeField, Min(1)] private int maxStepsPerFrame = 200;

    [Header("Saving")]
    [Tooltip("Save the preparation in progress (persistentDataPath/Alchemy/Stations) and continue it after a restart.")]
    [SerializeField] private bool persistPreparation = true;
    [Tooltip("Unique id of this station in the project (filled automatically).")]
    [SerializeField] private string stationId = "";

    [Header("Debug")]
    [SerializeField] private bool logEvents;

    private static readonly List<AlchemyStation> all = new List<AlchemyStation>();

    private AlchemyBrew brew;
    private AlchemyRules rules;
    private CombatEntity tender;
    private readonly AlchemyConditionSample conditions = new AlchemyConditionSample();
    private readonly List<IAlchemyConditionSource> sources = new List<IAlchemyConditionSource>();
    private float conditionsTimer, observeTimer;
    private int version;
    private readonly List<SubstanceAmount> residue = new List<SubstanceAmount>();
    private int metaVersion;

    /// <summary>Every station in the loaded scenes.</summary>
    public static IReadOnlyList<AlchemyStation> All => all;

    /// <summary>Raised for every event of the preparation (visuals, sounds, logs).</summary>
    public event Action<AlchemyEvent> BrewEvent;
    /// <summary>Something the player did changed the station (the window redraws).</summary>
    public event Action Changed;

    /// <summary>The preparation was replaced (started, emptied, restored): a network server sends it whole.</summary>
    public event Action BrewReplaced;
    /// <summary>A message for this machine's player at the station (a refusal from the server): the window can show it.</summary>
    public event Action<string> LocalMessage;

    /// <summary>Set by the network layer (null offline).</summary>
    public IAlchemyStationNet Net { get; set; }

    /// <summary>
    /// True where the rules run: offline, or on the server. A client of a network session never runs them: with a
    /// network side it follows the server's preparation, without one it cannot be used from there.
    /// </summary>
    public bool RulesHere => Net != null ? Net.RunsStationRules : !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;
    /// <summary>A network client: the preparation is a copy following the server's.</summary>
    public bool IsMirror { get; private set; }
    /// <summary>Changes when the player brewing here or the residue in the vessel change (the network sends them).</summary>
    public int MetaVersion => metaVersion;

    public AlchemyDatabase Database => database != null ? database : AlchemyDatabase.Instance;
    public AlchemyRules Rules => rules ??= Database.Rules;
    public AlchemyEquipmentProfile EquipmentProfile => equipment != null ? equipment.Profile : DefaultEquipment;
    public string DisplayName => !string.IsNullOrWhiteSpace(stationName) ? stationName : EquipmentProfile.displayName;
    public bool HasWater => waterSource && Rules.IndexOf(AlchemyDefaults.Water) >= 0;
    public bool HasMortar => hasMortar;
    /// <summary>Does the window show results, quality and amounts while brewing (a test bench), or only what is seen?</summary>
    public bool RevealsResults => testBench || freeDefaultIngredients;
    /// <summary>Does the window show the temperature in degrees?</summary>
    public bool HasThermometer => thermometer || RevealsResults;
    public AlchemyFireControl FireControl => fireControl;
    public bool HasManualFire => fireControl == AlchemyFireControl.Manual;
    public bool HasWaterBath => hasWaterBath;
    public bool HasIceBath => hasIceBath;
    /// <summary>The film of residue the vessel kept from its last preparation (empty = clean).</summary>
    public IReadOnlyList<SubstanceAmount> Residue => residue;
    public bool IsDirty => residue.Count > 0;
    public float WaterPerPour => waterPerPour;
    public ItemSO BottleItem => bottleItem;
    /// <summary>The preparation (null when the vessel is empty and nothing was started).</summary>
    public AlchemyBrew Brew => brew;
    /// <summary>The player using the window now.</summary>
    public CombatEntity Tender => tender;
    /// <summary>Changes whenever an operation changed the station.</summary>
    public int Version => version;

    private static AlchemyEquipmentProfile defaultEquipment;
    private static AlchemyEquipmentProfile DefaultEquipment => defaultEquipment ??= AlchemyEquipmentProfile.DefaultCauldron();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => defaultEquipment = null;

    // ------------------------------------------------------------------ lifecycle
    private void Reset()
    {
        stationId = Guid.NewGuid().ToString("N");
    }

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
        {
            sources.Add(gameObject.AddComponent<AlchemySurroundings>());
        }
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        if (persistPreparation && brew == null && RulesHere)
        {
            LoadPreparation();
            LoadResidue();
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (persistPreparation && RulesHere && !IsMirror)
            SavePreparation();
        if (tender != null && RulesHere)
            EndTending(tender);
    }

    private void OnApplicationQuit()
    {
        if (persistPreparation && RulesHere && !IsMirror)
            SavePreparation();
    }

    private void Update()
    {
        if (brew == null || !RulesHere)
            return;
        conditionsTimer += Time.deltaTime;
        if (conditionsTimer >= conditionsInterval)
        {
            conditionsTimer = 0f;
            ApplyConditions();
        }
        if (runWhileAway || tender != null)
            brew.Advance(Time.deltaTime, maxStepsPerFrame);
        if (tender != null)
        {
            observeTimer += Time.deltaTime;
            if (observeTimer >= 1f)
            {
                observeTimer = 0f;
                AlchemyKnowledge.For(tender)?.Observe(brew);
            }
        }
    }

    // ------------------------------------------------------------------ the preparation
    /// <summary>The preparation, started when there is none.</summary>
    public AlchemyBrew EnsureBrew()
    {
        if (brew != null)
            return brew;
        // Started at the database's room temperature; the surroundings are then recorded as the first operations, so the
        // history alone replays the preparation (AlchemyBrew.Replay with the default ambient and the seed).
        int s = newSeedEachPreparation ? Environment.TickCount : seed;
        SetBrew(new AlchemyBrew(Rules, EquipmentProfile, Rules.Settings.defaultAmbient, s));
        ApplyConditions();
        // A dirty vessel: the new preparation starts with what the last one left behind (recorded, so replays see it).
        if (residue.Count > 0)
        {
            foreach (SubstanceAmount r in residue)
                brew.AddSubstance(r.substance, r.amount, brew.Mixture.Ambient, out _, "left in the vessel by the last preparation");
            residue.Clear();
            metaVersion++;
            SaveResidue();
        }
        return brew;
    }

    private void SetBrew(AlchemyBrew b)
    {
        if (brew != null)
            brew.EventRaised -= OnBrewEvent;
        brew = b;
        if (brew != null)
            brew.EventRaised += OnBrewEvent;
        Touch();
        BrewReplaced?.Invoke();
    }

    // ------------------------------------------------------------------ network clients
    /// <summary>Client: the copy of the server's preparation to show (a snapshot arrived; null = the vessel is empty).</summary>
    public void SetMirror(AlchemyBrew copy)
    {
        IsMirror = true;
        SetBrew(copy);
    }

    /// <summary>Client: who brews here and the residue in the vessel, as the server says.</summary>
    public void SetMirrorMeta(CombatEntity using_, IEnumerable<SubstanceAmount> film)
    {
        if (!IsMirror)
            return;
        tender = using_;
        residue.Clear();
        if (film != null)
            foreach (SubstanceAmount r in film)
                if (r != null && Rules.IndexOf(r.substance) >= 0) residue.Add(r);
        Touch();
    }

    /// <summary>Client: the copy moved on (the window redraws).</summary>
    public void MirrorChanged() => Touch();

    /// <summary>Client: the session ended - the copy is dropped (an offline game loads the station's own save again).</summary>
    public void ClearMirror()
    {
        if (!IsMirror)
            return;
        SetBrew(null);
        IsMirror = false;
        tender = null;
        residue.Clear();
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

    private void OnBrewEvent(AlchemyEvent e)
    {
        if (logEvents)
            Debug.Log($"[Alchemy] {DisplayName}: {e}", this);
        BrewEvent?.Invoke(e);
    }

    private void CollectConditions()
    {
        conditions.Clear();
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is Behaviour b && (b == null || !b.isActiveAndEnabled)) continue;
            sources[i]?.Collect(conditions);
        }
    }

    /// <summary>
    /// Reads the surroundings and sets them on the preparation. Values are rounded (ambient to 1 °C, variables to 0.05)
    /// so a drifting humidity does not fill the history: only real changes are recorded.
    /// </summary>
    public void ApplyConditions()
    {
        if (brew == null)
            return;
        CollectConditions();
        if (conditions.Ambient.HasValue)
            brew.SetAmbient(Math.Round(conditions.Ambient.Value));
        foreach (KeyValuePair<string, double> v in conditions.Variables)
            brew.SetVariable(v.Key, Math.Round(v.Value * 20.0) / 20.0, 0.01);
    }

    private void Touch()
    {
        version++;
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ tending
    /// <summary>The player uses the station now (the window opened). On a network client the server is told.</summary>
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
            observeTimer = 0f;
            metaVersion++;
        }
        Touch();
    }

    /// <summary>The player stops using the station (the window closed, they walked away, they left the session).</summary>
    public void EndTending(CombatEntity player)
    {
        if (player == null)
            return;
        if (!RulesHere)
        {
            Net?.SendTending(false);
            return;
        }
        if (player != tender)
            return;
        if (brew != null)
            AlchemyKnowledge.For(player)?.Observe(brew);
        tender = null;
        metaVersion++;
        Touch();
    }

    // ------------------------------------------------------------------ requests
    /// <summary>Asks the station to do something for <paramref name="player"/> (here, or through the network).</summary>
    public bool Request(CombatEntity player, AlchemyRequest request, out string reason)
    {
        reason = "";
        if (!RulesHere)
        {
            if (Net != null)
            {
                Net.SendRequest(request);
                return true;
            }
            reason = "This station is not set up for multiplayer (it needs a NetAlchemyStation): only the host can brew here.";
            return false;
        }
        switch (request.kind)
        {
            case AlchemyRequestKind.AddItem:
                return AddItem(player, request.item != null ? request.item : ItemDatabase.Item(request.itemId), Mathf.Max(1, request.count), out reason,
                    request.form, request.evenness > 0f ? request.evenness : 1f);
            case AlchemyRequestKind.AddWater:
                return AddWater(request.value > 0f ? request.value : waterPerPour, out reason);
            case AlchemyRequestKind.SetHeat:
                if (HasManualFire && request.value > 0f)
                {
                    reason = "This vessel sits on a real fire: feed it fuel and work the bellows.";
                    return false;
                }
                EnsureBrew().SetHeat(request.value);
                Touch();
                return true;
            case AlchemyRequestKind.AddFuel:
                return AddFuel(player, request.id, Mathf.Max(1, request.count), out reason);
            case AlchemyRequestKind.Bellows:
            case AlchemyRequestKind.SetDamper:
                if (!HasManualFire)
                {
                    reason = "This vessel is heated by a burner: use the heat setting.";
                    return false;
                }
                if (request.kind == AlchemyRequestKind.Bellows) EnsureBrew().Bellows(request.value > 0f ? request.value : 1f);
                else EnsureBrew().SetDamper(request.value);
                Touch();
                return true;
            case AlchemyRequestKind.SetStirring:
                if (brew == null || brew.IsEmpty)
                {
                    if (request.value <= 0f) return true;
                    reason = "There is nothing to stir.";
                    return false;
                }
                brew.SetStirring(request.value);
                Touch();
                return true;
            case AlchemyRequestKind.SetBath:
            {
                var bath = (AlchemyBath)Mathf.Clamp(Mathf.RoundToInt(request.value), 0, 2);
                if (bath == AlchemyBath.Water && !hasWaterBath || bath == AlchemyBath.Ice && !hasIceBath)
                {
                    reason = bath == AlchemyBath.Water ? "There is no water bath here." : "There is no ice bath here.";
                    return false;
                }
                EnsureBrew().SetBath(bath);
                Touch();
                return true;
            }
            case AlchemyRequestKind.Filter:
                return Filter(player, request.id, out reason, request.value > 0f ? request.value : 1f);
            case AlchemyRequestKind.Decant:
                return Decant(player, out reason);
            case AlchemyRequestKind.Skim:
                return Skim(player, out reason);
            case AlchemyRequestKind.CleanVessel:
                return CleanVessel(player, out reason);
            case AlchemyRequestKind.Stir:
                if (brew == null || brew.IsEmpty) { reason = "There is nothing to stir."; return false; }
                brew.Stir(request.value > 0f ? request.value : 1f);
                Touch();
                return true;
            case AlchemyRequestKind.Suspend:
                if (brew == null) { reason = "Nothing is being prepared."; return false; }
                brew.Suspend();
                Touch();
                return true;
            case AlchemyRequestKind.Resume:
                if (brew == null) return false;
                brew.Resume();
                Touch();
                return true;
            case AlchemyRequestKind.Bottle:
                return Bottle(player, request.count > 0 ? request.count : int.MaxValue, out reason);
            case AlchemyRequestKind.CollectDistillate:
                return CollectDistillate(player, out reason);
            case AlchemyRequestKind.Empty:
                return Empty(player, out reason);
        }
        return false;
    }

    /// <summary><see cref="Request"/>, telling the player why not.</summary>
    public bool RequestAndTell(CombatEntity player, AlchemyRequest request)
    {
        bool ok = Request(player, request, out string reason);
        if (!ok && !string.IsNullOrEmpty(reason))
            Tell(player, reason);
        return ok;
    }

    // ------------------------------------------------------------------ ingredients
    /// <summary>Is <paramref name="item"/> one of the station's free supplies?</summary>
    public bool IsFreeSupply(ItemSO item)
    {
        if (item == null)
            return false;
        if (freeIngredients != null && freeIngredients.Contains(item))
            return true;
        return freeDefaultIngredients && AlchemyItems.IsRuntime(item) && item is AlchemyIngredientSO;
    }

    /// <summary>The free supplies (the window lists them first).</summary>
    public List<ItemSO> FreeSupplies()
    {
        var list = new List<ItemSO>();
        if (freeIngredients != null)
            foreach (ItemSO i in freeIngredients)
                if (i != null && !list.Contains(i)) list.Add(i);
        if (freeDefaultIngredients)
            foreach (AlchemyIngredientSO i in AlchemyItems.AllRuntimeIngredients())
                if (!list.Exists(x => x.Name == i.Name)) list.Add(i);
        return list;
    }

    /// <summary>The ingredient of <paramref name="item"/> (null = it cannot go into a vessel).</summary>
    public IAlchemyIngredient IngredientOf(ItemSO item) => Database.IngredientOf(item);

    /// <summary>The form <paramref name="item"/> becomes when ground once here (null = there is no mortar or it cannot be ground).</summary>
    public AlchemyIngredientForm GroundForm(ItemSO item) => hasMortar ? Rules.GroundForm(IngredientOf(item)?.AlchemyProfile) : null;

    /// <summary>The levels the mortar can grind <paramref name="item"/> to, coarsest first (empty = it cannot be ground here).</summary>
    public List<AlchemyIngredientForm> GrindLevels(ItemSO item) =>
        hasMortar ? Rules.GrindLevels(IngredientOf(item)?.AlchemyProfile) : new List<AlchemyIngredientForm>();

    /// <summary>
    /// Puts <paramref name="count"/> of <paramref name="item"/> into the vessel (from the player's inventory unless it is a
    /// free supply), in <paramref name="form"/> when given (the station's mortar grinds whole ingredients).
    /// </summary>
    public bool AddItem(CombatEntity player, ItemSO item, int count, out string reason, string form = null, float evenness = 1f)
    {
        reason = "";
        IAlchemyIngredient ingredient = IngredientOf(item);
        if (ingredient == null)
        {
            reason = item != null ? $"{item.Name} does nothing in a vessel." : "Nothing to add.";
            return false;
        }
        if (!string.IsNullOrEmpty(form) && !string.Equals(form, ingredient.AlchemyProfile.form, StringComparison.OrdinalIgnoreCase))
        {
            if (!GrindLevels(item).Exists(f => string.Equals(f.id, form, StringComparison.OrdinalIgnoreCase)))
            {
                reason = hasMortar ? $"{item.Name} cannot be made {Rules.Form(form).Name.ToLowerInvariant()} here." : "There is no mortar here.";
                return false;
            }
        }
        bool free = IsFreeSupply(item);
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (!free)
        {
            int have = inv != null ? inv.GetItemCount(item) : 0;
            count = Mathf.Min(count, have);
            if (count <= 0)
            {
                reason = $"You have no {item.Name}.";
                return false;
            }
        }
        AlchemyBrew b = EnsureBrew();
        int fit = b.HowManyFit(ingredient);
        if (fit <= 0)
        {
            reason = $"The {EquipmentProfile.displayName} is full.";
            return false;
        }
        count = Mathf.Min(count, fit);
        if (!b.AddIngredient(ingredient, count, out reason, form, evenness))
            return false;
        if (!free)
            inv.RemoveItems(item, count);
        if (player != null)
            AlchemyKnowledge.For(player)?.RecordIngredient(ingredient.AlchemyId, ingredient.AlchemyName, count);
        Touch();
        return true;
    }

    public bool AddWater(float units, out string reason)
    {
        reason = "";
        if (!HasWater)
        {
            reason = "There is no water here.";
            return false;
        }
        AlchemyBrew b = EnsureBrew();
        units = Mathf.Min(units, (float)b.FreeCapacity);
        if (units < 0.05f)
        {
            reason = $"The {EquipmentProfile.displayName} is full.";
            return false;
        }
        if (!b.AddSubstance(AlchemyDefaults.Water, units, waterTemperature, out reason))
            return false;
        Touch();
        return true;
    }

    // ------------------------------------------------------------------ the fire
    /// <summary>
    /// How many of <paramref name="fuel"/> the player can put on the fire: -1 = free (the woodpile, a free supply), 0 = none.
    /// A fuel without an item is free with a woodpile, else it needs an item of its name.
    /// </summary>
    public int FuelAvailable(CombatEntity player, AlchemyFuel fuel)
    {
        if (fuel == null || !HasManualFire)
            return 0;
        if (string.IsNullOrEmpty(fuel.item) && woodpile)
            return -1;
        FindSupply(player, string.IsNullOrEmpty(fuel.item) ? fuel.Name : fuel.item, out int count);
        return count;
    }

    /// <summary>Puts <paramref name="count"/> × <paramref name="fuel"/> on the fire (from the player's inventory unless free).</summary>
    public bool AddFuel(CombatEntity player, string fuelId, int count, out string reason)
    {
        reason = "";
        if (!HasManualFire)
        {
            reason = "This vessel is heated by a burner: use the heat setting.";
            return false;
        }
        AlchemyFuel fuel = Rules.Fuel(fuelId);
        if (fuel == null)
        {
            reason = "That does not burn here.";
            return false;
        }
        int have = FuelAvailable(player, fuel);
        if (have == 0)
        {
            reason = $"You have no {fuel.Name}.";
            return false;
        }
        if (have > 0)
            count = Mathf.Min(count, have);
        if (!EnsureBrew().AddFuel(fuel.id, count, out reason))
            return false;
        if (have > 0)
            Use(player, string.IsNullOrEmpty(fuel.item) ? fuel.Name : fuel.item, count);
        Touch();
        return true;
    }

    // ------------------------------------------------------------------ separating
    /// <summary>The filters at hand here.</summary>
    public List<AlchemyFilter> AvailableFilters()
    {
        var list = new List<AlchemyFilter>();
        foreach (AlchemyFilter f in Rules.Filters)
            if (filters == null || filters.Count == 0 || filters.Contains(f.id))
                list.Add(f);
        return list;
    }

    /// <summary>How many times the player can use <paramref name="filter"/>: -1 = as often as they like (reusable or free), 0 = they lack its medium.</summary>
    public int FilterUses(CombatEntity player, AlchemyFilter filter)
    {
        if (filter == null)
            return 0;
        if (string.IsNullOrEmpty(filter.medium))
            return -1;
        FindSupply(player, filter.medium, out int count);
        return count;
    }

    /// <summary>
    /// Pours the liquid through a filter: what it catches (crystals, salts...) is collected; its medium is used up.
    /// <paramref name="care"/> is how well it was poured (the filtering minigame; 1 = carefully).
    /// </summary>
    public bool Filter(CombatEntity player, string filterId, out string reason, float care = 1f)
    {
        reason = "";
        AlchemyFilter f = Rules.Filter(filterId);
        if (f == null || !AvailableFilters().Contains(f))
        {
            reason = "There is no such filter here.";
            return false;
        }
        if (brew == null)
        {
            reason = "There is nothing to filter.";
            return false;
        }
        int uses = FilterUses(player, f);
        if (uses == 0)
        {
            reason = $"The {f.Name.ToLowerInvariant()} needs {f.medium}.";
            return false;
        }
        AlchemyHarvest h = brew.Filter(f.id, care, out reason);
        if (h == null)
            return false;
        if (uses > 0)
            Use(player, f.medium, 1);
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int got = GiveHarvest(inv, h);
        Tell(player, got > 0 ? $"Filtered through the {f.Name.ToLowerInvariant()} and collected {got} item(s) from it." : $"Filtered through the {f.Name.ToLowerInvariant()}.");
        Touch();
        return true;
    }

    /// <summary>Pours off the top layer: bottled when it is a potion, else what it holds is collected (oil, an essential oil).</summary>
    public bool Decant(CombatEntity player, out string reason)
    {
        reason = "";
        if (brew == null)
        {
            reason = "There is nothing to pour off.";
            return false;
        }
        AlchemyHarvest h = brew.Decant(out reason);
        if (h == null)
            return false;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int bottles = 0;
        if (h.Liquid.Outcome != null && h.Liquid.Outcome.producesItems && h.Liquid.Bottles > 0)
        {
            bottles = h.Liquid.Bottles;
            if (bottleItem != null)
                bottles = Mathf.Min(bottles, inv != null ? inv.GetItemCount(bottleItem) : 0);
            ItemSO item = bottles > 0 ? AlchemyItems.PotionItem(Rules, h.Liquid, AllowsRuntimeItems) : null;
            if (item != null)
            {
                if (bottleItem != null && inv != null) inv.RemoveItems(bottleItem, bottles);
                Give(inv, item, bottles, h.Liquid);
            }
            else
                bottles = 0;
        }
        int got = GiveHarvest(inv, h);
        if (player != null && bottles > 0)
            AlchemyKnowledge.For(player)?.RecordResult(brew, h.Liquid, bottles);
        Tell(player, bottles > 0 ? $"Poured off the top layer: {bottles} × {h.Liquid.DisplayName}{QualityText(h.Liquid)}." :
            got > 0 ? $"Poured off the top layer and collected {got} item(s) from it." : "Poured off the top layer.");
        Touch();
        return true;
    }

    /// <summary>Skims off what floats (wax, charcoal, ash): it is collected.</summary>
    public bool Skim(CombatEntity player, out string reason)
    {
        reason = "";
        if (brew == null)
        {
            reason = "There is nothing to skim.";
            return false;
        }
        AlchemyHarvest h = brew.Skim(out reason);
        if (h == null)
            return false;
        int got = GiveHarvest(player != null ? InventoryManager.For(player) : null, h);
        Tell(player, got > 0 ? $"Skimmed the surface and collected {got} item(s)." : "Skimmed the surface.");
        Touch();
        return true;
    }

    /// <summary>Scrubs the empty vessel clean of the last preparation's residue.</summary>
    public bool CleanVessel(CombatEntity player, out string reason)
    {
        reason = "";
        if (brew != null && !brew.IsEmpty)
        {
            reason = "Empty the vessel first.";
            return false;
        }
        if (residue.Count == 0)
        {
            reason = "The vessel is already clean.";
            return false;
        }
        residue.Clear();
        metaVersion++;
        SaveResidue();
        Tell(player, "You scrub the vessel clean.");
        Touch();
        return true;
    }

    /// <summary>An item called <paramref name="name"/> (its name, asset name or alchemy id): among the free supplies (count -1), else in the inventory.</summary>
    private ItemSO FindSupply(CombatEntity player, string name, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (ItemSO s in FreeSupplies())
            if (Named(s, name))
            {
                count = -1;
                return s;
            }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv != null)
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it.itemScriptableObject;
                if (so != null && Named(so, name))
                {
                    count = inv.GetItemCount(so);
                    return so;
                }
            }
        return null;
    }

    private void Use(CombatEntity player, string name, int count)
    {
        ItemSO item = FindSupply(player, name, out int have);
        if (item != null && have > 0)
            InventoryManager.For(player)?.RemoveItems(item, count);
    }

    private static bool Named(ItemSO item, string name) =>
        item != null && (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase) ||
                         item is IAlchemyIngredient a && string.Equals(a.AlchemyId, name, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ taking things out
    /// <summary>How many bottles the player can fill now (liquid, empty vials).</summary>
    public int BottlesPossible(CombatEntity player, out AlchemyResult prediction)
    {
        prediction = brew?.Evaluate();
        if (prediction == null || prediction.Outcome == null || !prediction.Outcome.producesItems)
            return 0;
        int n = prediction.Bottles;
        if (bottleItem != null)
        {
            InventoryManager inv = player != null ? InventoryManager.For(player) : null;
            n = Mathf.Min(n, inv != null ? inv.GetItemCount(bottleItem) : 0);
        }
        return n;
    }

    public bool Bottle(CombatEntity player, int maxBottles, out string reason)
    {
        reason = "";
        if (brew == null || brew.Mixture.LiquidVolume <= Rules.Settings.traceAmount)
        {
            reason = "There is no liquid to bottle.";
            return false;
        }
        int n = Mathf.Min(maxBottles, BottlesPossible(player, out AlchemyResult prediction));
        if (prediction.Outcome == null || !prediction.Outcome.producesItems)
        {
            reason = $"Nothing worth bottling: {prediction.DisplayName.ToLowerInvariant()}.";
            return false;
        }
        if (n <= 0)
        {
            reason = bottleItem != null && prediction.Bottles > 0 ? $"You need an empty {bottleItem.Name} for each bottle." : "Not enough liquid to fill a bottle.";
            return false;
        }
        ItemSO item = AlchemyItems.PotionItem(Rules, prediction, AllowsRuntimeItems);
        if (item == null)
        {
            reason = $"{prediction.DisplayName} has no item asset (Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Potion Items).";
            return false;
        }
        AlchemyResult r = brew.Bottle(n);
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (bottleItem != null && inv != null)
            inv.RemoveItems(bottleItem, r.Bottles);
        Give(inv, item, r.Bottles, r);
        if (player != null)
            AlchemyKnowledge.For(player)?.RecordResult(brew, r, r.Bottles);
        Tell(player, $"Bottled {r.Bottles} × {r.DisplayName}{QualityText(r)}.");
        if (brew.IsEmpty)
            SetBrew(null);
        Touch();
        return true;
    }

    public bool CollectDistillate(CombatEntity player, out string reason)
    {
        reason = "";
        if (brew == null || brew.Mixture.DistillateVolume <= Rules.Settings.traceAmount)
        {
            reason = "Nothing has been distilled.";
            return false;
        }
        AlchemyResult preview = brew.EvaluateDistillate();
        int bottles = preview.Outcome != null && preview.Outcome.producesItems ? preview.Bottles : 0;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (bottleItem != null && bottles > 0)
            bottles = Mathf.Min(bottles, inv != null ? inv.GetItemCount(bottleItem) : 0);
        ItemSO item = bottles > 0 ? AlchemyItems.PotionItem(Rules, preview, AllowsRuntimeItems) : null;
        AlchemyHarvest h = brew.CollectDistillate();
        if (item != null)
        {
            if (bottleItem != null && inv != null) inv.RemoveItems(bottleItem, bottles);
            Give(inv, item, bottles, preview);
        }
        int harvested = GiveHarvest(inv, h);
        if (player != null)
            AlchemyKnowledge.For(player)?.RecordResult(brew, h.Liquid, item != null ? bottles : 0);
        Tell(player, item != null ? $"Collected {bottles} × {h.Liquid.DisplayName}{QualityText(h.Liquid)} from the distillate." :
            harvested > 0 ? "Collected the distillate." : $"The distillate was {h.Liquid.DisplayName.ToLowerInvariant()}: poured away.");
        Touch();
        return true;
    }

    /// <summary>
    /// Empties the vessel: solids at the bottom (crystals, ash...) and the distillate are harvested, the liquid is poured
    /// away (bottle it first). The journal records what it was. The next ingredient starts a new preparation.
    /// </summary>
    public bool Empty(CombatEntity player, out string reason)
    {
        reason = "";
        if (brew == null)
        {
            reason = "The vessel is already empty.";
            return false;
        }
        bool hadLiquid = brew.Mixture.LiquidVolume > Rules.Settings.noticeableAmount;
        AlchemyBrew finished = brew;
        AlchemyHarvest h = brew.Empty();
        if (vesselGetsDirty && h.Leftovers.Count > 0)
        {
            residue.Clear();
            residue.AddRange(h.Leftovers);
            metaVersion++;
            SaveResidue();
        }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int harvested = GiveHarvest(inv, h);
        if (player != null && hadLiquid)
            AlchemyKnowledge.For(player)?.RecordResult(finished, h.Liquid, 0);
        Tell(player, (harvested > 0 ? $"Emptied the vessel and collected {harvested} item(s) from it." : "Emptied the vessel.") +
                     (residue.Count > 0 ? " A film of residue clings to it." : ""));
        SetBrew(null);
        DeletePreparation();
        return true;
    }

    /// <summary>Gives the harvested items (a substance without a Harvest Item gives a matching runtime ingredient offline, else nothing).</summary>
    private int GiveHarvest(InventoryManager inv, AlchemyHarvest h)
    {
        int total = 0;
        foreach (AlchemyHarvestItem i in h.Items)
        {
            ItemSO item = i.Item != null ? i.Item : AlchemyItems.HarvestItem(Rules.Substance(i.Substance), AllowsRuntimeItems);
            if (item == null) continue;
            Give(inv, item, i.Count);
            total += i.Count;
        }
        return total;
    }

    /// <summary>" - quality 72 % (strength 80 %, purity 91 %)": what bottling reveals.</summary>
    public static string QualityText(AlchemyResult r) =>
        r?.Outcome != null && r.Outcome.HasPotency && !r.IsFailure
            ? $" - quality {r.Quality * 100:0}% (strength {r.Potency * 100:0}%, purity {r.Purity * 100:0}%)" : "";

    /// <summary>
    /// Bottles and harvests without an asset get items made at runtime. Their ids say what they are, so every machine of
    /// a network session makes the same item (see <see cref="AlchemyItems"/>).
    /// </summary>
    public bool AllowsRuntimeItems => true;

    /// <summary>
    /// Gives <paramref name="count"/> × <paramref name="item"/> (what does not fit drops at the station). Bottles of
    /// <paramref name="bottled"/> keep its strength and purity (<see cref="BrewedPotionSO.Encode"/>), one value per bottle.
    /// </summary>
    private void Give(InventoryManager inv, ItemSO item, int count, AlchemyResult bottled = null)
    {
        if (item == null || count <= 0)
            return;
        List<int> codes = null;
        if (bottled != null && item is BrewedPotionSO && bottled.Outcome != null && bottled.Outcome.HasPotency)
        {
            codes = new List<int>(count);
            int code = BrewedPotionSO.Encode(bottled.Potency, bottled.Purity);
            for (int i = 0; i < count; i++) codes.Add(code);
        }
        int left = inv != null ? inv.AddItem(item, count, codes) : count;
        if (left > 0)
        {
            Vector3 at = dropPoint != null ? dropPoint.position : transform.position + transform.forward * 1f + Vector3.up * 0.5f;
            if (codes != null && item.Prefab != null)
                ItemHandler.SpawnWorldItem(item, left, codes.GetRange(0, left), at, gameObject);
            else
                ItemDropManager.Spawn(null, new List<ItemStack> { new ItemStack(item, left) }, at, gameObject);
        }
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
    private string SavePath
    {
        get
        {
            string id = string.IsNullOrEmpty(stationId) ? gameObject.scene.name + "_" + name : stationId;
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, "Alchemy", "Stations", id + ".json");
        }
    }

    /// <summary>Writes the preparation in progress to disk (nothing when the vessel is empty).</summary>
    public void SavePreparation()
    {
        if (!Application.isPlaying || IsMirror)
            return;
        try
        {
            if (brew == null || brew.IsEmpty)
            {
                DeletePreparation();
                return;
            }
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, brew.Capture().ToJson());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Alchemy] Could not save the preparation of {DisplayName}: {e.Message}", this);
        }
    }

    private void LoadPreparation()
    {
        if (!Application.isPlaying || IsMirror)
            return;
        try
        {
            string path = SavePath;
            if (!File.Exists(path))
                return;
            AlchemyBrewState state = AlchemyBrewState.FromJson(File.ReadAllText(path));
            if (state != null)
                SetBrew(AlchemyBrew.Restore(Rules, state));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Alchemy] Could not restore the preparation of {DisplayName}: {e.Message}", this);
        }
    }

    private void DeletePreparation()
    {
        try
        {
            string path = SavePath;
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception) { }
    }

    [Serializable]
    private class ResidueSave
    {
        public List<SubstanceAmount> residue = new List<SubstanceAmount>();
    }

    private string ResiduePath => Path.ChangeExtension(SavePath, ".residue.json");

    private void SaveResidue()
    {
        if (!Application.isPlaying || !persistPreparation || IsMirror)
            return;
        try
        {
            string path = ResiduePath;
            if (residue.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new ResidueSave { residue = new List<SubstanceAmount>(residue) }));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Alchemy] Could not save the residue of {DisplayName}: {e.Message}", this);
        }
    }

    private void LoadResidue()
    {
        if (!Application.isPlaying || IsMirror)
            return;
        try
        {
            string path = ResiduePath;
            if (!File.Exists(path))
                return;
            ResidueSave save = JsonUtility.FromJson<ResidueSave>(File.ReadAllText(path));
            residue.Clear();
            if (save?.residue != null)
                foreach (SubstanceAmount r in save.residue)
                    if (r != null && Rules.IndexOf(r.substance) >= 0) residue.Add(r);
            metaVersion++;
        }
        catch (Exception) { }
    }

    /// <summary>Replaces the preparation with a saved one (editor tools, network snapshots, your save system).</summary>
    public void LoadState(AlchemyBrewState state)
    {
        if (!RulesHere)
            return;
        SetBrew(state != null ? AlchemyBrew.Restore(Rules, state) : null);
    }

    // ------------------------------------------------------------------ interaction
    public string InteractionName => DisplayName;
    public string InteractionVerb => "Brew";
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
            reason = "Someone is using it";
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
            AlchemyStationUI.Show(this, player);
            return;
        }
        // A remote player (the server got the interaction from their client): the window opens on their screen.
        if (!RulesHere)
            return;
        if (Net != null && Net.Open(player))
            BeginTending(player);
        else
            Tell(player, "This station is not set up for multiplayer: only the host can brew here.");
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    /// <summary>Configuration problems (the inspector lists them).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (GetComponentInChildren<Collider>() == null)
            errors.Add("No collider on the station or its children: the Player Interactor cannot find it.");
        EquipmentProfile.Validate(errors, warnings);
        if (!waterSource && (freeIngredients == null || freeIngredients.Count == 0) && !freeDefaultIngredients)
            warnings.Add("No water source and no free supplies: players need water or another solvent among their ingredients.");
        if (bottleItem != null && IngredientOf(bottleItem) != null)
            warnings.Add("The bottle item is also an ingredient: players may pour their empty vials in.");
        if (HasManualFire && !woodpile)
            warnings.Add("A manual fire without a woodpile: players need fuel items (Firewood, Charcoal, Pine Resin, Lamp Oil) to light it.");
        foreach (string id in filters)
            if (!string.IsNullOrWhiteSpace(id) && Rules.Filter(id) == null)
                warnings.Add($"Filter '{id}' is not one of the database's filters.");
    }
}
