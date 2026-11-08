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
/// <item>Saved where the rules run (offline, the server) under persistentDataPath/Agriculture/Fields, by scene and field
/// id: soil, plants and weather history. Changing the layout in the editor starts the soil afresh (plants are kept).</item>
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
        EnsureState();
        EnsureTrigger();
        if (view == null)
        {
            var go = new GameObject("Soil Field View");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            view = go.AddComponent<SoilFieldView>();
            view.Bind(this);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        FarmDirector.Unregister(this);
        SaveIfDirty(true);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(fieldId)) fieldId = Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    /// <summary>Makes the field's soil from its layout (and its save) the first time it is needed.</summary>
    public void EnsureState()
    {
        if (state != null) return;
        layout ??= new SoilLayout();
        SoilGrid g = layout.CreateGrid();
        layout.Bake(g, Application.isPlaying ? FarmClock.Now : 0.0);
        state = new FieldState(g, AgricultureDatabase.Instance, AgriMath.Hash(FieldId));
        Rect b = layout.Bounds();
        boundingRadius = Mathf.Max(b.width, b.height) * 0.5f + 0.5f;
        coversDirty = true;
        if (Application.isPlaying && RulesHere && saveToDisk && Settings.saveFields) Load();
        // Made on a network client: not this machine's own field (its save was not loaded) - it is made again, and its
        // save loaded, once the rules run here; until then it is never saved.
        if (Application.isPlaying && !RulesHere) reloadWhenRulesHere = true;
        soilSeen = state.Grid.Version;
        plantsSeen = state.PlantVersion;
    }

    /// <summary>Starts the field afresh from its layout (no plants, the designed soil). Editor tools and tests.</summary>
    public void Rebuild()
    {
        state = null;
        heights = null;
        EnsureState();
        view?.Rebuild();
        Changed?.Invoke();
    }

    private IAgriClimateSource MakeClimate()
    {
        if (useWorldWeather && Settings.useWorldWeather && WeatherSystem.Instance != null && WeatherSystem.Instance.gameObject.scene == gameObject.scene)
            return new WorldWeatherSource(this);
        return new CityClimateSource(Settings);
    }

    /// <summary>The Farm Director calls this every frame: covers, weather, saving, change notices.</summary>
    public void Tick(double now)
    {
        if (reloadWhenRulesHere && RulesHere)
        {
            // Back from a network session: this machine's own field (and its save) again.
            reloadWhenRulesHere = false;
            Rebuild();
        }
        EnsureState();
        if (coversDirty) ApplyCovers();
        if (RulesHere && !Mirror)
        {
            if (state.EnvTime < 0.0 || now - state.EnvTime >= Settings.environmentStepHours)
            {
                if (WeatherSystem.Instance == null && climate is WorldWeatherSource) climate = null;
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
        float limit = Settings.deadPlantDays * 24f;
        for (int i = state.Plants.Count - 1; i >= 0; i--)
        {
            PlantState p = state.Plants[i];
            if (p != null && !p.Alive && p.deadHours > limit) state.Remove(p);
        }
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
        EnsureState();
        FarmResult result;
        if (Mirror) result = FarmResult.Fail("This field follows the server.");
        else if (player == null) result = FarmResult.Fail("Nobody to do it.");
        else if ((Local(player.transform.position) - request.spot).magnitude > Settings.workRange + 0.75f || !PlantInReach(request.plant, player))
            result = FarmResult.Fail("That is out of reach.");
        else
        {
            AgriKnowledge k = AgriKnowledge.For(player);
            FarmWorker worker = AgriToolkit.WorkerFor(player, k, request.kind == FarmActionKind.Sow ? AgriIds.Seed(request.item ?? "") : null);
            result = FarmActions.Perform(state, request, worker, FarmClock.Now, Climate);
            if (result != null && result.ok) AgriToolkit.Apply(player, k, worker, result, this);
            dirty = true;
        }
        // The answer names its request: a client matches it to what it is waiting for.
        if (result != null) result.seq = request.seq;
        Answered?.Invoke(player, result);
        if (!CardTable.IsLocalPlayer(player)) Net?.SendResult(player, result);
        return result;
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
    public float InteractionRange => boundingRadius + Settings.workRange + extraReach;
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
    private const int SaveVersion = 2;

    public string SavePath
    {
        get
        {
            string scene = gameObject.scene.IsValid() ? gameObject.scene.name : "Scene";
            string file = SMAssetsEditorSafe(scene + "_" + FieldId);
            return Path.Combine(Application.persistentDataPath, "Agriculture", "Fields", file + ".bin");
        }
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

    public void Save()
    {
        // Never what a network client was shown: only this machine's own field.
        if (state == null || Mirror || reloadWhenRulesHere) return;
        saveTimer = 0f;
        dirty = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            using (FileStream file = File.Create(SavePath))
            using (var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Fastest))
            using (var w = new BinaryWriter(zip))
                Write(w, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not save field '{DisplayName}' ({e.Message}).", this);
        }
    }

    private void Load()
    {
        string path = SavePath;
        if (!File.Exists(path)) return;
        try
        {
            using (FileStream file = File.OpenRead(path))
            using (var zip = new GZipStream(file, CompressionMode.Decompress))
            using (var r = new BinaryReader(zip))
                Read(r);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not read the save of field '{DisplayName}' - it starts afresh ({e.Message}).", this);
            SoilGrid g = layout.CreateGrid();
            layout.Bake(g, FarmClock.Now);
            state = new FieldState(g, AgricultureDatabase.Instance, AgriMath.Hash(FieldId));
        }
    }

    /// <summary>Everything the field is (soil, plants, weather history), for saves and tests.</summary>
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
        state.Grid.Write(w);
        w.Write(JsonUtility.ToJson(state.Export()));
    }

    private void Read(BinaryReader r)
    {
        if (r.ReadInt32() != SaveMagic) throw new InvalidDataException("not a field save");
        int version = r.ReadInt32();
        if (version != SaveVersion) throw new InvalidDataException("an older save");
        int hash = r.ReadInt32();
        int count = r.ReadInt32();
        var ids = new string[count];
        for (int i = 0; i < count; i++) ids[i] = r.ReadString();
        double env = r.ReadDouble();
        float soilT = r.ReadSingle(), wet = r.ReadSingle(), today = r.ReadSingle(), yesterday = r.ReadSingle();
        int lightDay = r.ReadInt32();
        bool sameLayout = hash == layout.Hash();
        if (sameLayout)
        {
            if (!state.Grid.Read(r)) throw new InvalidDataException("the soil does not fit");
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
        }
        else
        {
            // The layout changed in the editor: the soil starts afresh; skip the saved one.
            var discard = new SoilGrid(state.Grid.Width, state.Grid.Height, state.Grid.CellSize, state.Grid.Origin);
            try { discard.Read(r); } catch (Exception) { }
            Debug.Log($"Agriculture: the layout of field '{DisplayName}' changed - its soil starts afresh, its plants are kept.", this);
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
        view?.Rebuild();
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
            if (File.Exists(SavePath)) File.Delete(SavePath);
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
