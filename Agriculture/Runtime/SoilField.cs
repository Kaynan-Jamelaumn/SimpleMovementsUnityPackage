using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>The network side of a field (a NetSoilField on the same object, when Fish-Net is in the project).</summary>
public interface ISoilFieldNet
{
    bool RunsRules { get; }
    /// <summary>Client: asks the server to do something.</summary>
    void SendRequest(FarmRequest request);
    /// <summary>Server: opens the field window on the player's screen.</summary>
    bool Open(CombatEntity player);
    /// <summary>Server: what came of a player's request.</summary>
    void SendResult(CombatEntity player, FarmResult result);
    /// <summary>Server: the player's skills.</summary>
    void SendSkills(CombatEntity player, string json);
    /// <summary>Server: something changed that clients should see.</summary>
    void Changed();
    void Tell(CombatEntity player, string text);
}

/// <summary>
/// A piece of land that is soil: put it on the ground of the city (or anywhere) and shape it in the Scene view. Its
/// <see cref="layout"/> is what a designer says about it - the outline, the base soil, the sections whose soil differs
/// (rectangles, circles, polygons, painted cells, with soft edges), the natural patchiness. In play it becomes a living
/// field: every cell's soil changes with weather and work, plants grow in it, players work it through the field window.
/// <list type="bullet">
/// <item>Cheap: soil cells are only worked out when touched; plants are data drawn in batches (no GameObject per
/// plant), updated about once a game hour by the <see cref="FarmDirector"/> within a per-frame budget.</item>
/// <item>Saved where the rules run (offline, the server) with the world that is open (Worlds/&lt;world&gt;/Agriculture/Fields,
/// see <see cref="FarmClock.SaveFolder"/>), by scene and field id: soil, covers, plants and weather. A save is written
/// whole (aside, then swapped in, the previous copy kept as a backup), and one that cannot be read is never written over.
/// Changing the layout in the editor starts the soil afresh (plants are kept).</item>
/// <item>Interactable: a trigger over the field (Ignore Raycast layer) makes it a target of the Player Interactor.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Agriculture/Soil Field")]
public class SoilField : MonoBehaviour, IInteractable
{
    [Tooltip("Identifies the field's save file and its network messages. Made automatically; keep it unique.")]
    [SerializeField] private string fieldId = "";
    [Tooltip("Its name (the window's title, the notebook).")]
    [SerializeField] private string displayName = "Garden";
    [Tooltip("The soil: outline, cell size, base soil, sections, painted cells, patchiness.")]
    public SoilLayout layout = new SoilLayout();

    [Header("Weather")]
    [Tooltip("In a scene with a Weather System, use its weather; otherwise (the city) the Agriculture Database's climate.")]
    public bool useWorldWeather = true;

    [Header("Look")]
    [Tooltip("Draw the soil over the ground (its colour shows type, wetness, working, what lies on it).")]
    public bool showSoil = true;
    [Tooltip("Metres from the camera within which the field is drawn.")]
    [Min(1f)] public float drawDistance = 90f;
    [Tooltip("Metres within which fruit, flowers and pests are drawn.")]
    [Min(1f)] public float detailDistance = 30f;
    [Tooltip("Height of the soil above the ground below it, metres.")]
    [Min(0f)] public float groundOffset = 0.02f;
    [Tooltip("Follow the ground below (raycast when the field starts); off = flat at the field's height.")]
    public bool followGround = true;
    [Tooltip("What counts as ground for Follow Ground.")]
    public LayerMask groundLayers = ~0;
    public bool plantShadows = true;

    [Header("Interaction")]
    public bool interactable = true;
    public int interactionPriority = 3;
    [Tooltip("Metres beyond the outline a player can stand and still work the field.")]
    [Min(0f)] public float extraReach = 0.6f;

    [Header("Saving")]
    [Tooltip("Keep this field's soil and plants on disk (needs Save Fields in the Agriculture settings).")]
    public bool saveToDisk = true;

    private FieldState state;
    private IAgriClimateSource climate;
    private SoilFieldView view;
    private Transform interactionPoint;
    private bool coversDirty = true, dirty;
    /// <summary>Set when a network session ended on this client: the state is the client's until the rules run here again.</summary>
    private bool reloadWhenRulesHere;
    private float saveTimer;
    private int soilSeen = -1, plantsSeen = -1;
    private float[] heights;
    private int heightW, heightH;
    private float heightStep;
    private Vector2 heightOrigin;
    private float boundingRadius;
    // Sleeping (where the rules run): nobody near - saved, not simulated, perhaps freed from memory.
    private bool asleep, waking, started;
    private FieldCatchUp catchUp;
    private double sleptAt = -1.0;
    /// <summary>The world the garden was read from (<see cref="FarmClock.World"/>) and its folder (null: not kept): saved only there.</summary>
    private int stateWorld = -1;
    private string stateFolder;
    /// <summary>No copy of the save could be read: it is never written over, and the garden refuses work.</summary>
    private bool unreadable;
    /// <summary>Real seconds before a save that failed is tried again.</summary>
    private float retryAt;

    /// <summary>
    /// Asked before every piece of work on any field and for its prompt (offline, the server, and a client's prompt):
    /// the field, the player and the work - a refusal ("Not your garden.") or null when they may. Null hook: everyone may.
    /// Housing installs it for gardens that belong to a rented plot.
    /// </summary>
    public static Func<SoilField, CombatEntity, FarmActionKind, string> AccessCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AccessCheck = null;

    public ISoilFieldNet Net { get; set; }
    /// <summary>A network client's copy, filled by the server's snapshots (no rules run here).</summary>
    public bool Mirror { get; private set; }
    /// <summary>Raised when the soil or a plant changed visibly.</summary>
    public event Action Changed;
    /// <summary>Where the rules run: a player's request was done (or refused). On a client: the server's answer arrived.</summary>
    public event Action<CombatEntity, FarmResult> Answered;

    public string FieldId
    {
        get
        {
            if (string.IsNullOrEmpty(fieldId)) fieldId = Guid.NewGuid().ToString("N").Substring(0, 12);
            return fieldId;
        }
    }

    public string DisplayName { get => string.IsNullOrWhiteSpace(displayName) ? "Field" : displayName; set => displayName = value; }
    public AgriSettings Settings => AgricultureDatabase.Instance.Settings;
    public bool RulesHere => Net != null ? Net.RunsRules : !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;
    public FieldState State
    {
        get
        {
            EnsureState();
            return state;
        }
    }

    public IAgriClimateSource Climate => climate ??= MakeClimate();
    public SoilFieldView View => view;

    /// <summary>Sleeping: nobody near (or hidden). Saved, not simulated, not drawn; brought up to date when it wakes.</summary>
    public bool Asleep => asleep;
    /// <summary>Being brought up to date (waking, after a jump of the clock, or in the background while asleep).</summary>
    public bool CatchingUp => catchUp != null;
    /// <summary>Its soil and plants are in memory.</summary>
    public bool Loaded => state != null;
    /// <summary>Simulated now: awake, up to date, its rules here.</summary>
    public bool Simulated => !asleep && catchUp == null && state != null && RulesHere && !Mirror;
    /// <summary>The game hour the garden has lived to (-1 when not known: never saved).</summary>
    public double LivedTo => state != null ? state.EnvTime : sleptAt;
    /// <summary>The Farm Director's note of when a player was last near (real seconds).</summary>
    internal float LastNearAt { get; set; }
    /// <summary>The catch-up under way ends in waking (someone is near), not in sleeping again.</summary>
    internal bool Waking => waking && catchUp != null;
    /// <summary>The Farm Director's note of how far the nearest player was at its last look (metres; large: nobody near).</summary>
    internal float NearestPlayerSeen { get; set; } = float.MaxValue;
    /// <summary>No copy of its save could be read: left as it is (never written over), it refuses work until it is mended.</summary>
    public bool Unreadable => unreadable;

    /// <summary>The middle of the field's box in the world, on the field's plane (no ground sampling).</summary>
    public Vector3 CentreWorld
    {
        get
        {
            Rect r = layout != null ? layout.Bounds() : default;
            return transform.position + transform.rotation * new Vector3(r.center.x, 0f, r.center.y);
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private void Reset()
    {
        fieldId = Guid.NewGuid().ToString("N").Substring(0, 12);
        layout = new SoilLayout();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        FarmDirector.Register(this);
        LastNearAt = Time.unscaledTime;
        EnsureTrigger();
        if (!RulesHere) return; // a network client: drawn once the server's picture comes (when its player is near)
        if (!started)
        {
            started = true;
            if (Settings.sleepWhenAlone && CanUnload)
            {
                // It starts asleep (nothing loaded): woken - loaded and brought up to date - when a player comes near.
                asleep = true;
                sleptAt = PeekLivedTo();
                return;
            }
        }
        // Asleep (hidden with its realm, or nobody was near): it stays asleep until a player comes near - the Farm Director
        // wakes the nearest gardens first, a few at a time - not every garden of the realm at once in this frame.
        if (asleep) return;
        EnsureState();
        EnsureView();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        // Hidden (offline: the city while the player is away) or going away: it sleeps - saved, and brought up to date,
        // with the weather that fell meanwhile, when it is back.
        if (RulesHere && !Mirror) FallAsleep();
        else SaveIfDirty(true);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        FarmDirector.Unregister(this);
        catchUp = null;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(fieldId)) fieldId = Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    /// <summary>
    /// Makes the field's soil from its layout (and its save) the first time it is needed. An awake garden read from a
    /// save that lags the clock is brought up to date in lockstep before it is simulated (see <see cref="CatchingUp"/>).
    /// </summary>
    public void EnsureState()
    {
        if (state != null) return;
        layout ??= new SoilLayout();
        state = Fresh();
        Rect b = layout.Bounds();
        boundingRadius = Mathf.Max(b.width, b.height) * 0.5f + 0.5f;
        outerRadius = 0f;
        coversDirty = true;
        unreadable = false;
        stateWorld = -1;
        stateFolder = null;
        if (Application.isPlaying && RulesHere && saveToDisk && Settings.saveFields)
        {
            // The open world's garden: it is saved back into that world only.
            stateWorld = FarmClock.World;
            stateFolder = FarmClock.SaveFolder;
            if (stateFolder != null) Load();
        }
        // Made on a network client: not this machine's own field (its save was not loaded) - it is made again, and its
        // save loaded, once the rules run here; until then it is never saved.
        if (Application.isPlaying && !RulesHere) reloadWhenRulesHere = true;
        soilSeen = state.Grid.Version;
        plantsSeen = state.PlantVersion;
        // Read awake from a save that lags the clock (another world opened, the field was made again, a garden that never
        // sleeps came into play): it catches up as after a jump of the clock, weather and plants step by step - not the
        // whole gap in one plain tick, all its rain at once and then each plant through it. A sleeping one does as it wakes.
        CatchUpAfterJump();
    }

    /// <summary>The field as its layout makes it (the designed soil, no plants).</summary>
    private FieldState Fresh()
    {
        SoilGrid g = layout.CreateGrid();
        layout.Bake(g, Application.isPlaying ? FarmClock.Now : 0.0);
        return new FieldState(g, AgricultureDatabase.Instance, AgriMath.Hash(FieldId));
    }

    /// <summary>Makes the field again from its layout and its save (without one: the designed soil, no plants). Editor tools and tests.</summary>
    public void Rebuild()
    {
        state = null;
        heights = null;
        catchUp = null;
        waking = false;
        EnsureState();
        if (!asleep)
        {
            view?.Rebuild();
            EnsureView();
        }
        Changed?.Invoke();
    }

    private IAgriClimateSource MakeClimate() => WorldWeatherHere ? new WorldWeatherSource(this) : new CityClimateSource(Settings);

    /// <summary>Does the field follow the world's weather now (its scene's Weather System is here)?</summary>
    private bool WorldWeatherHere =>
        useWorldWeather && Settings.useWorldWeather && WeatherSystem.Instance != null && WeatherSystem.Instance.gameObject.scene == gameObject.scene;

    /// <summary>
    /// The climate was chosen before the scene's Weather System was enabled (a field read while its scene loads), or that
    /// Weather System is gone: it is chosen again at its next use.
    /// </summary>
    private bool ClimateStale => climate is WorldWeatherSource ? WeatherSystem.Instance == null : climate is CityClimateSource && WorldWeatherHere;

    /// <summary>The Farm Director calls this every frame: covers, weather, saving, change notices.</summary>
    public void Tick(double now)
    {
        if (reloadWhenRulesHere && RulesHere)
        {
            // Back from a network session: this machine's own field (and its save) again.
            reloadWhenRulesHere = false;
            Rebuild();
        }
        if (state == null && !RulesHere) return; // a network client's garden nobody of this machine is near
        if (asleep || catchUp != null) return; // sleeping, or being brought up to date (the Farm Director runs that)
        if (state == null && RulesHere && !Mirror && Settings.sleepWhenAlone && CanUnload)
        {
            // The rules run here again with nothing loaded (a session ended, another world opened): it starts asleep, as
            // at the start - woken when a player comes near, not every garden loaded in this frame.
            asleep = true;
            sleptAt = PeekLivedTo();
            return;
        }
        EnsureState();
        if (catchUp != null) return; // just read from a save that lags the clock: it catches up first
        if (coversDirty) ApplyCovers();
        if (RulesHere && !Mirror)
        {
            if (state.EnvTime < 0.0 || now - state.EnvTime >= Settings.environmentStepHours)
            {
                if (ClimateStale) climate = null;
                state.TickEnvironment(now, Climate);
            }
            if (dirty && saveToDisk && Settings.saveFields)
            {
                saveTimer += Time.unscaledDeltaTime;
                if (saveTimer > Settings.autosaveMinutes * 60f) SaveIfDirty(false);
            }
        }
        if (state.Grid.Version != soilSeen || state.PlantVersion != plantsSeen)
        {
            soilSeen = state.Grid.Version;
            plantsSeen = state.PlantVersion;
            dirty = true;
            Changed?.Invoke();
            if (RulesHere) Net?.Changed();
        }
    }

    /// <summary>The end of a pass over the plants: the dead that have lain long enough are gone.</summary>
    public void Sweep(double now)
    {
        if (state == null || Mirror) return;
        state.SweepDead(Settings.deadPlantDays * 24f);
    }

    // ------------------------------------------------------------------ sleeping and waking
    /// <summary>Its garden is kept on disk in the open world (so it may be freed from memory once saved).</summary>
    private bool CanUnload => saveToDisk && Settings.saveFields && !unreadable && FarmClock.SaveFolder != null;

    /// <summary>
    /// Nobody near (or hidden): the garden is brought up to the present when it is nearly there, saved, and no longer
    /// simulated or drawn; with Unload When Asleep it is also freed from memory - only once its save is written (a save
    /// that failed keeps it in memory, and it is tried again). Its life goes on - it is caught up, hour by hour with the
    /// weather that fell, when it wakes, and in the background when it lags too far behind. Only where the rules run.
    /// </summary>
    public void FallAsleep()
    {
        if (!Application.isPlaying || !RulesHere || Mirror || reloadWhenRulesHere) return;
        if (asleep)
        {
            waking = false; // a wake under way (hidden again before it ended) goes on in the background instead
            SettleAsleep(true);
            return;
        }
        asleep = true;
        waking = false; // a catch-up under way goes on in the background, then the garden is saved and freed
        FarmWindow.Close(this);
        DropView();
        if (state == null) return;
        if (catchUp != null)
        {
            // What it is now is saved at once (the catch-up goes on from there; it saves again when it ends).
            if (CanUnload) Save();
            return;
        }
        CoversForCatchUp();
        if (FarmClock.Now - state.EnvTime <= Settings.maxStepHours) BringUpToDate(FarmClock.Now);
        if (CanUnload && Save() && Settings.unloadWhenAsleep) Unload();
    }

    /// <summary>
    /// A sleeping garden still in memory: saved if it changed (a save that failed is tried again after a while) and freed
    /// with Unload When Asleep. The Farm Director calls it now and then; <paramref name="now"/>: without waiting for a retry.
    /// </summary>
    internal void SettleAsleep(bool now = false)
    {
        if (!asleep || catchUp != null || state == null || !Application.isPlaying || !RulesHere || Mirror || reloadWhenRulesHere) return;
        if (!now && Time.unscaledTime < retryAt) return;
        if (dirty && CanUnload && !Save()) return;
        if (Settings.unloadWhenAsleep) Unload();
    }

    /// <summary>
    /// A player came near (or it is shown again): loaded, brought up to date - at once for a few hours, over frames for
    /// longer (it refuses work meanwhile: "The garden is waking up.") - then simulated and drawn. Only where the rules run.
    /// </summary>
    public void WakeUp()
    {
        if (!Application.isPlaying || !RulesHere || Mirror || !isActiveAndEnabled) return; // a hidden field sleeps on
        if (!asleep && catchUp == null) return;
        EnsureState();
        if (catchUp != null)
        {
            waking = true; // the background work under way becomes the waking
            return;
        }
        CoversForCatchUp();
        if (state.EnvTime >= 0.0 && FarmClock.Now - state.EnvTime > Settings.maxStepHours)
        {
            catchUp = NewCatchUp();
            waking = true;
            return;
        }
        BringUpToDate(FarmClock.Now);
        FinishWaking();
    }

    private void FinishWaking()
    {
        asleep = false;
        waking = false;
        catchUp = null;
        coversDirty = true;
        LastNearAt = Time.unscaledTime;
        EnsureView();
        // The change notice goes out at the next tick: the window, the view and the clients' pictures are refreshed.
        soilSeen = -1;
        plantsSeen = -1;
    }

    /// <summary>The Farm Director works on the catch-up under way; true when it finished (or there was none).</summary>
    internal bool RunCatchUp(Func<bool> outOfTime)
    {
        if (catchUp == null) return true;
        if (state == null || !ReferenceEquals(catchUp.Field, state))
        {
            // The field was made again meanwhile (rebuilt, reloaded): start over from what it is now.
            catchUp = state != null ? NewCatchUp() : null;
            if (catchUp == null) return true;
        }
        if (catchUp.WeatherSteps == 0)
        {
            // Not begun yet: the covers over it, and its weather, as they are now - a structure or the scene's Weather
            // System shown with it may have been enabled after it was read (a scene that loads a garden awake).
            CoversForCatchUp();
            if (ClimateStale)
            {
                climate = null;
                catchUp = NewCatchUp();
            }
        }
        double now = FarmClock.Now;
        catchUp.Target = now;
        catchUp.LiveNow = now;
        if (!catchUp.Run(outOfTime)) return false;
        catchUp = null;
        dirty = true;
        if (waking && isActiveAndEnabled) FinishWaking();
        else
        {
            // Nobody waits for it (or it was hidden meanwhile): it sleeps on, saved, and freed once saved.
            waking = false;
            asleep = true;
            if (CanUnload && Save() && Settings.unloadWhenAsleep) Unload();
        }
        return true;
    }

    /// <summary>The Farm Director: a sleeping garden that lags behind is brought up to date in the background.</summary>
    internal void BeginBackgroundCatchUp()
    {
        if (!asleep || catchUp != null || !RulesHere || Mirror || reloadWhenRulesHere) return;
        EnsureState();
        if (state.EnvTime < 0.0) return;
        CoversForCatchUp();
        catchUp = NewCatchUp();
        waking = false;
    }

    /// <summary>
    /// The clock is ahead of an awake garden by more than a step - it jumped (sleep, a skip: the Farm Director calls
    /// this), or the garden was just read from a save that lags it: it catches up in lockstep, not plant by plant.
    /// </summary>
    internal void CatchUpAfterJump()
    {
        if (!Application.isPlaying || asleep || catchUp != null || state == null || !RulesHere || Mirror || state.EnvTime < 0.0) return;
        if (FarmClock.Now - state.EnvTime <= Settings.maxStepHours) return;
        CoversForCatchUp();
        catchUp = NewCatchUp();
        waking = true;
    }

    private FieldCatchUp NewCatchUp() => new FieldCatchUp(state, Climate, new CityClimateSource(Settings), FarmClock.Now);

    /// <summary>
    /// Before a catch-up: the covers of the structures over the field, when they are here (the field is shown, so are they).
    /// A field hidden with its realm keeps the covers it was saved with - its greenhouse is hidden with it, not gone.
    /// </summary>
    private void CoversForCatchUp()
    {
        if (state != null && coversDirty && isActiveAndEnabled) ApplyCovers();
    }

    /// <summary>Brings the field to <paramref name="target"/> at once (a gap of a few hours).</summary>
    private void BringUpToDate(double target)
    {
        if (state == null || state.EnvTime < 0.0 || target <= state.EnvTime) return;
        new FieldCatchUp(state, Climate, new CityClimateSource(Settings), target).Run();
    }

    /// <summary>Frees the soil and plants from memory - only when they are saved (a field not saved stays in memory).</summary>
    private void Unload()
    {
        if (state == null || !CanUnload || reloadWhenRulesHere || dirty) return;
        sleptAt = state.EnvTime;
        state = null;
        heights = null;
    }

    /// <summary>The game hour a saved garden had lived to, read from the start of its save (-1 without one).</summary>
    private double PeekLivedTo()
    {
        string path = SavePath;
        if (path == null) return -1.0;
        foreach (string copy in FarmClock.CopiesOf(path))
        {
            try
            {
                if (!File.Exists(copy)) continue;
                using (FileStream file = File.OpenRead(copy))
                using (var zip = new GZipStream(file, CompressionMode.Decompress))
                using (var r = new BinaryReader(zip))
                {
                    int version;
                    if (r.ReadInt32() != SaveMagic || (version = r.ReadInt32()) < 2 || version > SaveVersion) continue;
                    r.ReadInt32();
                    int count = r.ReadInt32();
                    if (count < 0 || count > 100000) continue;
                    for (int i = 0; i < count; i++) r.ReadString();
                    return r.ReadDouble();
                }
            }
            catch (Exception)
            {
                // the next copy
            }
        }
        return -1.0;
    }

    /// <summary>
    /// Another world opened (a session started or ended): what the garden held was the old world's (saved into it as it
    /// closed). The new world's garden is read when it is needed - at once for an awake garden (brought up to the new
    /// world's clock in lockstep first when its save lags it), when it wakes for a sleeping one; on a network client, the
    /// server's picture comes instead.
    /// </summary>
    internal void ChangeWorld()
    {
        if (Mirror) return; // the server's picture on a client: the network side gives the field back (ClearMirror)
        bool had = state != null;
        catchUp = null;
        waking = false;
        state = null;
        heights = null;
        dirty = false;
        saveTimer = 0f;
        unreadable = false;
        stateWorld = -1;
        stateFolder = null;
        sleptAt = -1.0;
        if (!Application.isPlaying) return;
        if (had) FarmWindow.Close(this);
        if (!RulesHere)
        {
            // A network client: the server's picture is drawn when its player is near; a garden never sleeps here.
            DropView();
            asleep = false;
            return;
        }
        reloadWhenRulesHere = false;
        if (asleep)
        {
            sleptAt = PeekLivedTo();
            return;
        }
        if (!had && view == null) return; // nothing of it was in use: it starts asleep at its next tick
        EnsureState();
        view?.Rebuild();
        EnsureView();
        soilSeen = -1;
        plantsSeen = -1;
        Changed?.Invoke();
    }

    /// <summary>The world the garden was read from is written one last time before it closes: the garden is saved into it.</summary>
    internal void SaveForClosingWorld()
    {
        if (state == null || Mirror || reloadWhenRulesHere || stateFolder == null) return;
        if (!dirty && catchUp == null && state.Grid.Version == soilSeen && state.PlantVersion == plantsSeen) return; // saved as it is
        Save();
    }

    private void EnsureView()
    {
        // Drawn only where someone looks: never on a dedicated server.
        if (view != null || !Application.isPlaying || !GameNet.NetAuthority.HasLocalPlayer || !isActiveAndEnabled || asleep) return;
        var go = new GameObject("Soil Field View");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        view = go.AddComponent<SoilFieldView>();
        view.Bind(this);
    }

    private void DropView()
    {
        if (view == null) return;
        Destroy(view.gameObject);
        view = null;
    }

    /// <summary>
    /// Picks the ripe produce of every plant - for someone leaving the garden (a tenant whose lease ended: their crop is
    /// theirs, not the next tenant's). The field is brought up to the present first (loaded if it slept), then each
    /// mature, ripe or overripe piece (and each plant whose whole crop is ready) is harvested as a careful worker with a
    /// sickle and a shovel would. Where the rules run; the items picked (agricultural ids with their per-copy codes). The
    /// garden is saved before they are given (they are not picked twice after a crash); a garden whose save cannot be read
    /// gives nothing.
    /// </summary>
    public List<FarmItemChange> PickRipe(string forName)
    {
        var items = new List<FarmItemChange>();
        if (!Application.isPlaying || !RulesHere || Mirror || reloadWhenRulesHere) return items;
        EnsureState();
        if (unreadable) return items;
        CoversForCatchUp();
        if (catchUp != null && !ReferenceEquals(catchUp.Field, state)) catchUp = null; // the field was made again meanwhile
        if (catchUp != null)
        {
            catchUp.Target = FarmClock.Now;
            catchUp.LiveNow = FarmClock.Now;
            catchUp.Run();
            catchUp = null;
            if (waking && isActiveAndEnabled) FinishWaking();
            waking = false;
        }
        else if (state.EnvTime >= 0.0 && FarmClock.Now > state.EnvTime)
            new FieldCatchUp(state, Climate, new CityClimateSource(Settings), FarmClock.Now).Run();
        var worker = new FarmWorker { key = 0, name = forName ?? "" };
        worker.tools.Add(new FarmToolProfile { id = "agri:tool/sickle", kind = FarmToolKind.Sickle, quality = 0.6f, speed = 1f, edge = 1f });
        worker.tools.Add(new FarmToolProfile { id = "agri:tool/shovel", kind = FarmToolKind.Shovel, quality = 0.6f, speed = 1f, edge = 1f });
        double now = FarmClock.Now;
        foreach (PlantState p in state.Plants.ToArray())
        {
            if (p == null || !p.Alive || !p.Emerged) continue;
            p.data ??= state.Catalog?.Species(p.species);
            if (p.data == null) continue;
            ProduceSpec pr = p.data.Produce;
            if (PlantSim.OnePiece(pr) || (pr.wholePlant && pr.part == ProducePart.Grain))
            {
                bool ready = p.stage == PlantStage.HarvestReady || p.stage == PlantStage.Overripe ||
                             (p.fruits.Count > 0 && p.fruits[0].stage >= ProduceStage.Mature && p.fruits[0].stage != ProduceStage.Rotten);
                if (ready) Collect(FarmActions.Perform(state, new FarmRequest { kind = FarmActionKind.Harvest, spot = p.position, plant = p.id }, worker, now, Climate), items);
                continue;
            }
            foreach (FruitState fr in p.fruits.ToArray())
                if (fr.stage >= ProduceStage.Mature && fr.stage != ProduceStage.Rotten)
                    Collect(FarmActions.Perform(state, new FarmRequest { kind = FarmActionKind.Harvest, spot = p.position, plant = p.id, piece = fr.id }, worker, now, Climate), items);
        }
        dirty = true;
        // Saved now, awake or not: the picked crop is the leaving tenant's from this moment.
        if (CanUnload && Save() && asleep && Settings.unloadWhenAsleep) Unload();
        return items;
    }

    private static void Collect(FarmResult r, List<FarmItemChange> into)
    {
        if (r == null || !r.ok || r.give == null) return;
        foreach (FarmItemChange g in r.give)
            if (g != null && g.count > 0) into.Add(g);
    }

    /// <summary>
    /// A network client stops watching the garden (nobody of this machine near it): its picture and its drawing are
    /// freed. A field this machine made before the session (its own) is saved first.
    /// </summary>
    public void ForgetPicture()
    {
        if (RulesHere) return;
        if (!Mirror && !reloadWhenRulesHere && state != null) Save();
        FarmWindow.Close(this);
        DropView();
        state = null;
        heights = null;
        reloadWhenRulesHere = true;
    }

    // ------------------------------------------------------------------ geometry
    /// <summary>Field-local ground-plane coordinates (X, Z) of a world point.</summary>
    public Vector2 Local(Vector3 world)
    {
        Vector3 l = Quaternion.Inverse(transform.rotation) * (world - transform.position);
        return new Vector2(l.x, l.z);
    }

    /// <summary>The world point on the soil at local (X, Z).</summary>
    public Vector3 World(Vector2 local, float lift = 0f) =>
        transform.position + transform.rotation * new Vector3(local.x, GroundHeight(local) + groundOffset + lift, local.y);

    /// <summary>How far a world point is from the field's outline (0 inside).</summary>
    public float DistanceFromOutline(Vector3 world)
    {
        Vector2 p = Local(world);
        return AgriMath.InPolygon(layout.outline, p) ? 0f : AgriMath.DistanceToOutline(layout.outline, p);
    }

    public bool Contains(Vector3 world) => AgriMath.InPolygon(layout.outline, Local(world));

    /// <summary>The field's box in world space.</summary>
    public Bounds WorldBounds()
    {
        Rect r = layout.Bounds();
        var b = new Bounds(World(r.center), Vector3.zero);
        b.Encapsulate(transform.position + transform.rotation * new Vector3(r.xMin, -1f, r.yMin));
        b.Encapsulate(transform.position + transform.rotation * new Vector3(r.xMax, 3f, r.yMax));
        b.Encapsulate(transform.position + transform.rotation * new Vector3(r.xMin, 3f, r.yMax));
        b.Encapsulate(transform.position + transform.rotation * new Vector3(r.xMax, -1f, r.yMin));
        return b;
    }

    public bool Touches(GrowingStructure s) => s != null && WorldBounds().Intersects(s.WorldBounds);

    /// <summary>Height of the ground under a local point, relative to the field (0 without Follow Ground).</summary>
    public float GroundHeight(Vector2 local)
    {
        if (!followGround || !Application.isPlaying) return 0f;
        if (heights == null) SampleGround();
        if (heights == null || heights.Length == 0) return 0f;
        float fx = (local.x - heightOrigin.x) / heightStep, fy = (local.y - heightOrigin.y) / heightStep;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, heightW - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, heightH - 1);
        int x1 = Mathf.Min(x0 + 1, heightW - 1), y1 = Mathf.Min(y0 + 1, heightH - 1);
        float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
        float a = Mathf.Lerp(heights[y0 * heightW + x0], heights[y0 * heightW + x1], tx);
        float b = Mathf.Lerp(heights[y1 * heightW + x0], heights[y1 * heightW + x1], tx);
        return Mathf.Lerp(a, b, ty);
    }

    /// <summary>Raycasts the ground under the field on a one-metre grid (once; again after a move).</summary>
    public void SampleGround()
    {
        Rect r = layout.Bounds();
        heightStep = 1f;
        heightOrigin = new Vector2(r.xMin, r.yMin);
        heightW = Mathf.Max(2, Mathf.CeilToInt(r.width / heightStep) + 2);
        heightH = Mathf.Max(2, Mathf.CeilToInt(r.height / heightStep) + 2);
        heights = new float[heightW * heightH];
        PhysicsScene physics = gameObject.scene.IsValid() ? gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        Vector3 up = transform.rotation * Vector3.up;
        for (int y = 0; y < heightH; y++)
            for (int x = 0; x < heightW; x++)
            {
                Vector2 l = heightOrigin + new Vector2(x * heightStep, y * heightStep);
                Vector3 top = transform.position + transform.rotation * new Vector3(l.x, 0f, l.y) + up * 20f;
                float h = 0f;
                if (physics.Raycast(top, -up, out RaycastHit hit, 60f, groundLayers, QueryTriggerInteraction.Ignore))
                    h = Vector3.Dot(hit.point - transform.position, up);
                heights[y * heightW + x] = Mathf.Clamp(h, -20f, 20f);
            }
    }

    // ------------------------------------------------------------------ covers (greenhouses, shade)
    public void MarkCoversDirty() => coversDirty = true;

    private void ApplyCovers()
    {
        coversDirty = false;
        if (state == null) return;
        SoilGrid g = state.Grid;
        int n = g.Count;
        for (int i = 0; i < n; i++)
        {
            g.CoverRain[i] = 1f;
            g.CoverEvap[i] = 1f;
            g.CoverLight[i] = 1f;
            g.CoverWarmth[i] = 0f;
            g.CoverFrost[i] = 0f;
        }
        IReadOnlyList<GrowingStructure> list = FarmDirector.Structures;
        for (int k = 0; k < list.Count; k++)
        {
            GrowingStructure s = list[k];
            if (s == null || !s.isActiveAndEnabled || !Touches(s)) continue;
            for (int i = 0; i < n; i++)
            {
                if (!g.Inside[i]) continue;
                if (!s.Contains(World(g.Center(i), 0.3f))) continue;
                g.CoverRain[i] *= s.rain;
                g.CoverEvap[i] *= s.evaporation;
                g.CoverLight[i] *= s.light;
                g.CoverWarmth[i] += s.warmth;
                g.CoverFrost[i] = Mathf.Max(g.CoverFrost[i], s.frostProtection);
            }
        }
        g.Version++;
    }

    // ------------------------------------------------------------------ work
    /// <summary>
    /// A player asks to do something. Where the rules run it is done now and the result returned (the items are given
    /// and used, the skills taught); on a network client the request goes to the server and null is returned - the
    /// answer comes through <see cref="Answered"/>.
    /// </summary>
    public FarmResult Perform(FarmRequest request, CombatEntity player)
    {
        if (player == null) return FarmResult.Fail("Nobody to do it.");
        if (!RulesHere)
        {
            if (Net == null) return FarmResult.Fail("This field is not set up for multiplayer (it needs a NetSoilField).");
            Net.SendRequest(request);
            return null;
        }
        return PerformHere(request, player);
    }

    /// <summary>Does a request where the rules run (offline, or the server for any player).</summary>
    public FarmResult PerformHere(FarmRequest request, CombatEntity player)
    {
        FarmResult result;
        string denied;
        if (Mirror) result = FarmResult.Fail("This field follows the server.");
        else if (player == null) result = FarmResult.Fail("Nobody to do it.");
        else if ((denied = Denied(player, request.kind)) != null) result = FarmResult.Fail(denied);
        // The spot is checked before anything is loaded: a request from afar never wakes a sleeping garden.
        else if ((Local(player.transform.position) - request.spot).magnitude > Settings.workRange + 0.75f) result = FarmResult.Fail("That is out of reach.");
        else if (unreadable) result = FarmResult.Fail(UnreadableText);
        else if (asleep || catchUp != null)
        {
            WakeUp();
            result = FarmResult.Fail("The garden is waking up.");
        }
        else
        {
            EnsureState();
            if (catchUp != null) result = FarmResult.Fail("The garden is waking up."); // just read from a save that lags the clock
            else if (unreadable || !PlantInReach(request.plant, player))
                result = FarmResult.Fail(unreadable ? UnreadableText : "That is out of reach.");
            else
            {
                AgriKnowledge k = AgriKnowledge.For(player);
                FarmWorker worker = AgriToolkit.WorkerFor(player, k, request.kind == FarmActionKind.Sow ? AgriIds.Seed(request.item ?? "") : null);
                result = FarmActions.Perform(state, request, worker, FarmClock.Now, Climate);
                if (result != null && result.ok) AgriToolkit.Apply(player, k, worker, result, this);
                dirty = true;
            }
        }
        // The answer names its request: a client matches it to what it is waiting for.
        if (result != null) result.seq = request.seq;
        Answered?.Invoke(player, result);
        if (!CardTable.IsLocalPlayer(player)) Net?.SendResult(player, result);
        return result;
    }

    private const string UnreadableText = "This garden's save cannot be read: it is left as it is.";

    /// <summary>The access hook's refusal for this player and work, or null (no hook: anyone may).</summary>
    public string Denied(CombatEntity player, FarmActionKind kind)
    {
        Func<SoilField, CombatEntity, FarmActionKind, string> check = AccessCheck;
        if (check == null) return null;
        try
        {
            return check(this, player, kind);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return "You cannot work this soil now.";
        }
    }

    /// <summary>
    /// A plant named by id is there and within the player's reach (its own crown counts): a request may name any plant
    /// of the field, so the spot alone does not prove the hands reach it.
    /// </summary>
    private bool PlantInReach(int plantId, CombatEntity player)
    {
        if (plantId <= 0) return true;
        PlantState p = state.Find(plantId);
        if (p == null) return true; // the action itself says it is gone
        p.data ??= state.Catalog?.Species(p.species);
        float crown = p.data != null ? p.data.Look.width * 0.5f * Mathf.Max(0.2f, p.size) : 0.5f;
        return (Local(player.transform.position) - p.position).magnitude <= Settings.workRange + 0.75f + crown;
    }

    /// <summary>Client: the server's answer to a request arrived.</summary>
    public void ReceiveAnswer(CombatEntity player, FarmResult result) => Answered?.Invoke(player, result);

    public void Tell(CombatEntity player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text)) return;
        if (CardTable.IsLocalPlayer(player) || Net == null)
            InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        else
            Net.Tell(player, text);
    }

    // ------------------------------------------------------------------ interaction
    private void EnsureTrigger()
    {
        if (!interactable || transform.Find("Soil Field Trigger") != null) return;
        Rect r = layout.Bounds();
        var go = new GameObject("Soil Field Trigger") { layer = 2 }; // Ignore Raycast: only the interaction prompt sees it
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(r.center.x, 0.5f, r.center.y);
        box.size = new Vector3(Mathf.Max(0.5f, r.width), 1.4f, Mathf.Max(0.5f, r.height));
        var point = new GameObject("Interaction Point");
        point.transform.SetParent(transform, false);
        point.transform.localPosition = new Vector3(r.center.x, 0.8f, r.center.y);
        interactionPoint = point.transform;
    }

    public string InteractionName => DisplayName;
    public string InteractionVerb => "Tend";
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRange => Radius + Settings.workRange + extraReach;

    /// <summary>Half the size of the field's box, metres (from the layout: known while the field sleeps).</summary>
    public float Radius
    {
        get
        {
            if (boundingRadius <= 0f && layout != null)
            {
                Rect b = layout.Bounds();
                boundingRadius = Mathf.Max(b.width, b.height) * 0.5f + 0.5f;
            }
            return boundingRadius;
        }
    }
    /// <summary>From the middle of the field's box to its farthest corner, metres (no point of the field is farther).</summary>
    internal float OuterRadius
    {
        get
        {
            if (outerRadius <= 0f && layout != null)
            {
                Rect b = layout.Bounds();
                outerRadius = Mathf.Sqrt(b.width * b.width + b.height * b.height) * 0.5f + 0.5f;
            }
            return outerRadius;
        }
    }
    private float outerRadius;

    public int InteractionPriority => interactionPriority;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!interactable) return false;
        CombatEntity player = PlayerOf(interactor);
        if (player == null) return false;
        if (DistanceFromOutline(player.transform.position) > Settings.workRange + extraReach)
        {
            reason = "Too far from the soil";
            return false;
        }
        string denied = Denied(player, FarmActionKind.Inspect);
        if (denied != null)
        {
            reason = denied;
            return false;
        }
        if (unreadable)
        {
            reason = UnreadableText;
            return false;
        }
        if (RulesHere && !Mirror && (asleep || catchUp != null))
        {
            WakeUp();
            reason = "The garden is waking up.";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = PlayerOf(interactor);
        if (player == null) return;
        if (CardTable.IsLocalPlayer(player))
        {
            FarmWindow.Show(this, player);
            return;
        }
        if (!RulesHere) return;
        if (Net != null && Net.Open(player)) Net.SendSkills(player, AgriKnowledge.For(player).Export());
        else Tell(player, "This field is not set up for multiplayer: only the host can work it.");
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    // ------------------------------------------------------------------ saving
    private const int SaveMagic = 0x31464741; // "AGF1"
    /// <summary>3: the weather of the last hour is kept, and the soil is kept in one piece (skipped whole when the layout changed). 2: still read.</summary>
    private const int SaveVersion = 3;

    /// <summary>
    /// The field's save in the open world: Worlds/&lt;world&gt;/Agriculture/Fields/&lt;scene&gt;_&lt;field id&gt;.bin (null where nothing
    /// is kept here: a network client, a world open in another window of the game). Its backup is "&lt;file&gt;.bak".
    /// </summary>
    public string SavePath => PathIn(FarmClock.SaveFolder);

    private string PathIn(string folder)
    {
        if (folder == null) return null;
        string scene = gameObject.scene.IsValid() ? gameObject.scene.name : "Scene";
        return Path.Combine(folder, "Fields", SMAssetsEditorSafe(scene + "_" + FieldId) + ".bin");
    }

    private static string SMAssetsEditorSafe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    public void SaveIfDirty(bool force)
    {
        if (!dirty || state == null || Mirror || reloadWhenRulesHere || !Application.isPlaying || !RulesHere || !saveToDisk || !Settings.saveFields) return;
        if (!force && saveTimer < Settings.autosaveMinutes * 60f) return;
        Save();
    }

    /// <summary>
    /// Writes the field's save whole - aside first, then swapped in, the previous copy kept as the backup - so a failed or
    /// torn write never costs the save there was. Only this machine's own field, into the world it was read from, and never
    /// over a save that could not be read. False when nothing was written: the field then stays in memory, still to be
    /// saved (it is tried again).
    /// </summary>
    public bool Save()
    {
        // Never what a network client was shown: only this machine's own field.
        if (state == null || Mirror || reloadWhenRulesHere || unreadable || !saveToDisk || !Settings.saveFields) return false;
        if (stateFolder == null || stateWorld != FarmClock.World) return false; // not kept, or another world's
        saveTimer = 0f;
        try
        {
            byte[] data;
            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
                using (var w = new BinaryWriter(zip))
                    Write(w, true);
                data = ms.ToArray();
            }
            FarmClock.WriteWhole(PathIn(stateFolder), data);
            dirty = false;
            return true;
        }
        catch (Exception e)
        {
            dirty = true;
            retryAt = Time.unscaledTime + 30f;
            Debug.LogWarning($"Agriculture: could not save field '{DisplayName}' ({e.Message}). It stays in memory and is tried again.", this);
            return false;
        }
    }

    /// <summary>
    /// Reads the field's save into the fresh state: the save itself, else a write that was not swapped in, else the backup
    /// (a save that cannot be read is then set aside, kept for whoever wants to look at it). When no copy can be read, the
    /// garden is left as its layout makes it, refuses work and is never saved - nothing is written over the files.
    /// </summary>
    private void Load()
    {
        string path = PathIn(stateFolder);
        string[] copies = FarmClock.CopiesOf(path);
        bool any = false;
        string problem = null;
        for (int k = 0; k < copies.Length; k++)
        {
            if (!File.Exists(copies[k])) continue;
            any = true;
            try
            {
                using (FileStream file = File.OpenRead(copies[k]))
                using (var zip = new GZipStream(file, CompressionMode.Decompress))
                using (var r = new BinaryReader(zip))
                    Read(r);
            }
            catch (Exception e)
            {
                problem ??= e.Message;
                state = Fresh(); // nothing of a copy that could not be read is kept
                dirty = false;
                continue;
            }
            if (k > 0 && File.Exists(path))
            {
                string aside = path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try
                {
                    File.Move(path, aside);
                    Debug.LogWarning($"Agriculture: the save of field '{DisplayName}' could not be read ({problem}): its {(k == 1 ? "last write" : "backup")} was used, and the unreadable file is kept as '{Path.GetFileName(aside)}'.", this);
                }
                catch (Exception e)
                {
                    unreadable = true;
                    Debug.LogError($"Agriculture: the save of field '{DisplayName}' could not be read ({problem}) nor set aside ({e.Message}): the garden refuses work and is not saved until it is mended ({path}).", this);
                }
            }
            return;
        }
        if (!any) return; // never saved: it starts from its layout
        unreadable = true;
        Debug.LogError($"Agriculture: no copy of the save of field '{DisplayName}' can be read ({problem}): the garden is left as its layout makes it, refuses work and is not saved, so nothing is written over it ({path}).", this);
    }

    /// <summary>Everything the field is (soil, covers, plants, weather), for saves and tests.</summary>
    public void Write(BinaryWriter w, bool full)
    {
        w.Write(SaveMagic);
        w.Write(SaveVersion);
        w.Write(layout.Hash());
        IReadOnlyList<SoilAmendmentData> amendments = AgricultureDatabase.Instance.AllAmendments;
        w.Write(amendments.Count);
        foreach (SoilAmendmentData a in amendments) w.Write(a.id ?? "");
        w.Write(state.EnvTime);
        w.Write(state.SoilTemperature);
        w.Write(state.WetLeaves);
        w.Write(state.TodayLight);
        w.Write(state.YesterdayLight);
        w.Write(state.LightDay);
        AgriWeather we = state.Weather;
        w.Write(we.temperature);
        w.Write(we.humidity);
        w.Write(we.rain);
        w.Write(we.clouds);
        w.Write(we.wind);
        w.Write(we.light);
        w.Write(we.day);
        w.Write(we.storm);
        // The soil in one piece, so a save whose layout changed can skip it whole.
        using (var ms = new MemoryStream())
        {
            using (var gw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) state.Grid.Write(gw);
            w.Write((int)ms.Length);
            w.Write(ms.GetBuffer(), 0, (int)ms.Length);
        }
        w.Write(JsonUtility.ToJson(state.Export()));
    }

    private void Read(BinaryReader r)
    {
        if (r.ReadInt32() != SaveMagic) throw new InvalidDataException("not a field save");
        int version = r.ReadInt32();
        if (version < 2 || version > SaveVersion) throw new InvalidDataException($"a save of version {version}");
        int hash = r.ReadInt32();
        int count = r.ReadInt32();
        if (count < 0 || count > 100000) throw new InvalidDataException("broken materials");
        var ids = new string[count];
        for (int i = 0; i < count; i++) ids[i] = r.ReadString();
        double env = r.ReadDouble();
        float soilT = r.ReadSingle(), wet = r.ReadSingle(), today = r.ReadSingle(), yesterday = r.ReadSingle();
        int lightDay = r.ReadInt32();
        AgriWeather weather = AgriEnvironment.Climate(Settings, Math.Max(0.0, env)); // a save of version 2: the climate's
        byte[] soil = null;
        if (version >= 3)
        {
            weather = new AgriWeather
            {
                temperature = r.ReadSingle(), humidity = r.ReadSingle(), rain = r.ReadSingle(), clouds = r.ReadSingle(),
                wind = r.ReadSingle(), light = r.ReadSingle(), day = r.ReadBoolean(), storm = r.ReadBoolean(),
            };
            int length = r.ReadInt32();
            if (length < 0 || length > 256 * 1024 * 1024) throw new InvalidDataException("broken soil");
            soil = r.ReadBytes(length);
            if (soil.Length != length) throw new EndOfStreamException("the soil was cut short");
        }
        bool sameLayout = hash == layout.Hash();
        if (sameLayout)
        {
            bool fits;
            if (soil != null)
                using (var sr = new BinaryReader(new MemoryStream(soil))) fits = state.Grid.Read(sr);
            else fits = state.Grid.Read(r);
            if (!fits) throw new InvalidDataException("the soil does not fit");
            // Materials lying on top are kept by index: map the saved ones to this database's.
            var remap = new ushort[count + 1];
            for (int i = 0; i < count; i++) remap[i + 1] = (ushort)(state.Catalog.AmendmentIndex(ids[i]) + 1);
            ushort[] pending = state.Grid.PendingItem;
            for (int i = 0; i < pending.Length; i++)
                if (pending[i] != 0) pending[i] = pending[i] <= count ? remap[pending[i]] : (ushort)0;
            state.EnvTime = env;
            state.SoilTemperature = soilT;
            state.WetLeaves = wet;
            state.TodayLight = today;
            state.YesterdayLight = yesterday;
            state.LightDay = lightDay;
            state.Weather = weather; // the instruments read it before the next weather step
        }
        else
        {
            // The layout changed in the editor: the soil starts afresh; the saved one is skipped.
            Debug.Log($"Agriculture: the layout of field '{DisplayName}' changed - its soil starts afresh, its plants are kept.", this);
            if (soil == null)
            {
                // A save of version 2 keeps its soil cell by cell: it can be skipped only when the cells are the same.
                try
                {
                    var discard = new SoilGrid(state.Grid.Width, state.Grid.Height, state.Grid.CellSize, state.Grid.Origin);
                    if (!discard.Read(r)) throw new InvalidDataException("another size");
                    state.Import(JsonUtility.FromJson<PlantStateList>(r.ReadString()));
                }
                catch (Exception)
                {
                    state = Fresh();
                    Debug.Log($"Agriculture: the older save of field '{DisplayName}' does not fit its new layout - its plants start afresh too.", this);
                }
                dirty = true;
                return;
            }
        }
        string json = r.ReadString();
        PlantStateList list = JsonUtility.FromJson<PlantStateList>(json);
        state.Import(list);
        dirty = !sameLayout;
    }

    // ------------------------------------------------------------------ network snapshots (what clients see)
    private const int VisualVersion = 2;
    private const int MaxVisualPlants = 65536;

    /// <summary>
    /// Client: the session ended - the server's picture is dropped and the field is its own again. It is made afresh
    /// (and its save loaded) once the rules run here: never saved over by what the client was shown.
    /// </summary>
    public void ClearMirror()
    {
        if (!Mirror) return;
        Mirror = false;
        reloadWhenRulesHere = true;
        state = null;
        heights = null;
        // Drawn again once the rules run here (the field made afresh from this machine's own save).
        DropView();
        Changed?.Invoke();
    }

    /// <summary>
    /// What a client needs to draw the field and read its plants: the soil as it looks (<paramref name="soil"/>) and every
    /// plant as it looks (<paramref name="plants"/>) - either or both, so a plant that changed does not resend the soil.
    /// No hidden numbers.
    /// </summary>
    public byte[] ExportVisual(bool soil = true, bool plants = true)
    {
        EnsureState();
        using (var ms = new MemoryStream())
        {
            using (var w = new BinaryWriter(ms))
            {
                SoilGrid g = state.Grid;
                w.Write(VisualVersion);
                w.Write((byte)((soil ? 1 : 0) | (plants ? 2 : 0)));
                w.Write(FarmClock.Now);
                w.Write(g.Width);
                w.Write(g.Height);
                for (int i = 0; soil && i < g.Count; i++)
                {
                    if (!g.Inside[i]) continue;
                    int o = i * SoilGrid.Stride;
                    w.Write(g.Types[i]);
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Moisture]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Tilth]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Stones]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Mulch]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Compaction]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.WaterRetention]));
                    w.Write(Byte(g.Values[o + (int)SoilChannel.Salinity] / 10f));
                    w.Write(g.Holes[i]);
                    int top = g.PendingTop(i, out float amount);
                    w.Write((ushort)(top + 1));
                    w.Write(Byte(amount / 10f));
                }
                if (!plants) return ms.ToArray();
                w.Write(state.Plants.Count);
                foreach (PlantState p in state.Plants)
                {
                    w.Write(p.id);
                    w.Write(p.species ?? "");
                    w.Write(p.position.x);
                    w.Write(p.position.y);
                    w.Write((byte)p.stage);
                    w.Write(Byte(p.progress));
                    w.Write(Byte(p.size / 1.5f));
                    w.Write(Byte(p.health));
                    w.Write(Byte(p.stress));
                    w.Write(p.planted);
                    w.Write(p.deadHours);
                    int flags = (p.dormant ? 1 : 0) | (p.supported ? 2 : 0) | (p.bolted ? 4 : 0) | (p.genes.rare ? 8 : 0) | (p.covered ? 16 : 0);
                    w.Write((byte)flags);
                    for (int k = 0; k < PlantState.SymptomCount; k++) w.Write(Byte(p.symptoms[k]));
                    for (int k = 0; k < 8; k++) w.Write(Byte(p.pests[k]));
                    for (int k = 0; k < 6; k++) w.Write(Byte(p.diseases[k]));
                    w.Write(Byte(p.canopy));
                    w.Write(Byte(p.deadWood));
                    w.Write(Byte(p.wounds));
                    w.Write(p.label ?? "");
                    w.Write((short)Mathf.Min(p.fruits.Count, short.MaxValue));
                    foreach (FruitState f in p.fruits)
                    {
                        w.Write(f.id);
                        w.Write((byte)f.stage);
                        w.Write(Byte(f.progress));
                        w.Write(Byte(f.size));
                        w.Write(Byte(f.slot));
                        w.Write((byte)(f.pollinated ? 1 : 0));
                    }
                }
            }
            return ms.ToArray();
        }
    }

    /// <summary>A client takes the server's picture, whole or in part (it becomes a mirror: no rules run on it).</summary>
    public bool ImportVisual(byte[] data)
    {
        EnsureState();
        if (data == null || data.Length < 21) return false;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadInt32() != VisualVersion) return false;
                int parts = r.ReadByte();
                double now = r.ReadDouble();
                SoilGrid g = state.Grid;
                if (r.ReadInt32() != g.Width || r.ReadInt32() != g.Height) return false;
                Mirror = true;
                if (Application.isPlaying) FarmClock.FromServer(now);
                EnsureView();
                for (int i = 0; (parts & 1) != 0 && i < g.Count; i++)
                {
                    if (!g.Inside[i]) continue;
                    int o = i * SoilGrid.Stride;
                    g.Types[i] = r.ReadByte();
                    g.Values[o + (int)SoilChannel.Moisture] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.Tilth] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.Stones] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.Mulch] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.Compaction] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.WaterRetention] = r.ReadByte() / 255f;
                    g.Values[o + (int)SoilChannel.Salinity] = r.ReadByte() / 255f * 10f;
                    g.Holes[i] = r.ReadByte();
                    ushort top = r.ReadUInt16();
                    float amount = r.ReadByte() / 255f * 10f;
                    g.PendingItem[i * SoilGrid.PendingSlots] = top;
                    g.PendingAmount[i * SoilGrid.PendingSlots] = amount;
                    g.PendingItem[i * SoilGrid.PendingSlots + 1] = 0;
                }
                if ((parts & 1) != 0) g.Version++;
                if ((parts & 2) == 0) return true;
                int count = r.ReadInt32();
                if (count < 0 || count > MaxVisualPlants) return false;
                var plants = new PlantStateList();
                for (int n = 0; n < count; n++)
                {
                    var p = new PlantState
                    {
                        id = r.ReadInt32(), species = r.ReadString(), position = new Vector2(r.ReadSingle(), r.ReadSingle()), stage = (PlantStage)r.ReadByte(),
                        progress = r.ReadByte() / 255f, size = r.ReadByte() / 255f * 1.5f, health = r.ReadByte() / 255f, stress = r.ReadByte() / 255f,
                        planted = r.ReadDouble(), deadHours = r.ReadSingle(),
                    };
                    p.last = now;
                    int flags = r.ReadByte();
                    p.dormant = (flags & 1) != 0;
                    p.supported = (flags & 2) != 0;
                    p.bolted = (flags & 4) != 0;
                    p.genes.rare = (flags & 8) != 0;
                    p.covered = (flags & 16) != 0;
                    for (int k = 0; k < PlantState.SymptomCount; k++) p.symptoms[k] = r.ReadByte() / 255f;
                    for (int k = 0; k < 8; k++) p.pests[k] = r.ReadByte() / 255f;
                    for (int k = 0; k < 6; k++) p.diseases[k] = r.ReadByte() / 255f;
                    p.canopy = r.ReadByte() / 255f;
                    p.deadWood = r.ReadByte() / 255f;
                    p.wounds = r.ReadByte() / 255f;
                    p.label = r.ReadString();
                    int fruits = r.ReadInt16();
                    if (fruits < 0) return false;
                    for (int k = 0; k < fruits; k++)
                        p.fruits.Add(new FruitState { id = r.ReadInt32(), stage = (ProduceStage)r.ReadByte(), progress = r.ReadByte() / 255f, size = r.ReadByte() / 255f, slot = r.ReadByte() / 255f, pollinated = r.ReadByte() != 0 });
                    plants.plants.Add(p);
                }
                state.Import(plants);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: a broken field snapshot ({e.Message}).", this);
            return false;
        }
    }

    private static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

    // ------------------------------------------------------------------ debug (Play mode)
    [ContextMenu("Agriculture/Give The Local Player A Starter Kit")]
    private void GiveStarterKit()
    {
        CombatEntity me = HubWorld.HubWorldDirector.LocalPlayer;
        if (me == null)
            foreach (CombatEntity e in CombatEntity.Players)
                if (CardTable.IsLocalPlayer(e)) { me = e; break; }
        if (me == null || !Application.isPlaying)
        {
            Debug.LogWarning("Agriculture: no local player (enter Play mode with a player).", this);
            return;
        }
        AgriToolkit.GiveStarterKit(me);
    }

    [ContextMenu("Agriculture/Skip 6 Hours")]
    private void Skip6() => FarmClock.Skip(6.0);

    [ContextMenu("Agriculture/Skip 1 Day")]
    private void Skip24() => FarmClock.Skip(24.0);

    [ContextMenu("Agriculture/Skip 1 Week")]
    private void SkipWeek() => FarmClock.Skip(24.0 * 7.0);

    [ContextMenu("Agriculture/Forget The Saved Field")]
    private void ForgetSave()
    {
        try
        {
            string path = SavePath;
            if (path != null)
                foreach (string copy in FarmClock.CopiesOf(path))
                    if (File.Exists(copy)) File.Delete(copy);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.Message, this);
        }
        if (Application.isPlaying) Rebuild();
    }

    // ------------------------------------------------------------------ gizmos
    private void OnDrawGizmos()
    {
        if (layout?.outline == null || layout.outline.Count < 2) return;
        Gizmos.color = new Color(0.55f, 0.35f, 0.15f, 0.9f);
        Matrix4x4 m = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        for (int i = 0, j = layout.outline.Count - 1; i < layout.outline.Count; j = i++)
        {
            Vector2 a = layout.outline[j], b = layout.outline[i];
            Gizmos.DrawLine(m.MultiplyPoint3x4(new Vector3(a.x, 0.03f, a.y)), m.MultiplyPoint3x4(new Vector3(b.x, 0.03f, b.y)));
        }
    }
}

/// <summary>A live Weather System's weather over a field (it only knows now; seasons are added on top).</summary>
public sealed class WorldWeatherSource : IAgriClimateSource
{
    private readonly SoilField field;
    private AgriWeather cached;
    private float cachedAt = -10f;

    public WorldWeatherSource(SoilField field) { this.field = field; }

    public bool Historic => false;

    public AgriWeather WeatherAt(double time)
    {
        if (Time.unscaledTime - cachedAt < 2f) return cached;
        cachedAt = Time.unscaledTime;
        WeatherSystem ws = WeatherSystem.Instance;
        AgriSettings s = AgricultureDatabase.Instance.Settings;
        if (ws == null || field == null)
        {
            cached = AgriEnvironment.Climate(s, time);
            return cached;
        }
        WeatherState w = ws.WeatherAt(field.transform.position);
        var date = new AgriDate(time, s);
        float hour = WorldTime.HasClock ? WorldTime.Hour : date.Hour;
        float sunrise = WorldTime.HasClock ? WorldTime.SunriseHour : date.Sunrise, sunset = WorldTime.HasClock ? WorldTime.SunsetHour : date.Sunset;
        float clouds = Mathf.Clamp01(w.Clouds + 0.3f * w.Fog + 0.2f * w.Dust);
        cached = new AgriWeather
        {
            temperature = w.Temperature + (s.seasonsOnWorldWeather ? AgriEnvironment.SeasonOffset(date.Season) : 0f),
            humidity = Mathf.Clamp01(w.Humidity),
            rain = (w.Rain * 8f + w.Snow * 2f + w.Hail * 4f) * (w.IsStormy ? 1.5f : 1f),
            clouds = clouds,
            wind = w.WindSpeed,
            light = AgriEnvironment.Sunlight(hour, sunrise, sunset, clouds),
            storm = w.IsStormy,
        };
        cached.day = cached.light > 0f;
        return cached;
    }
}
