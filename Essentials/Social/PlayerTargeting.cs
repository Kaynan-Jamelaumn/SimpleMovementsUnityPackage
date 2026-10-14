using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Selecting another player (or a mob) as this machine's player's target: click on them (the mouse, or the middle of
/// the screen while the cursor is locked), or cycle through the players in front with the Cycle key. The target is
/// highlighted with a ring under its feet (red = can fight you, green = your party, blue = an ally, grey = neutral)
/// and shown in the corner by the <see cref="TargetFrame"/>, whose menu invites to a party, trades, challenges to a
/// duel or inspects. The Inspect key inspects the target (<see cref="PlayerInspection"/>).
/// <para>It runs on the machine of a player only (never on a dedicated server) and is created by itself with the
/// default settings; put one in a scene that stays loaded (Tools ▸ SimpleMovements ▸ Social ▸ Create Player Targeting)
/// to change them. All keys are rebindable in Key Bindings (Select Target, Cycle Target, Inspect Target, Target Menu).</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Player Targeting")]
public class PlayerTargeting : MonoBehaviour
{
    [Header("Selecting")]
    [Tooltip("Controls that select what is under the mouse (or the crosshair while the cursor is locked).")]
    [SerializeField] private string[] selectControls = { "<Mouse>/leftButton" };
    [Tooltip("Controls that select the next player in front of you.")]
    [SerializeField] private string[] cycleControls = { "<Keyboard>/tab" };
    [Tooltip("Controls that inspect the current target.")]
    [SerializeField] private string[] inspectControls = { "<Keyboard>/u" };
    [Tooltip("Controls that open the target's menu (and free the cursor for it).")]
    [SerializeField] private string[] menuControls = { "<Keyboard>/g" };
    [Tooltip("Mobs can be targeted too (their frame shows name and health only).")]
    public bool targetMobs = false;
    [Tooltip("Farthest a click selects (meters).")]
    [Min(1f)] public float selectDistance = 60f;
    [Tooltip("Farthest the Cycle key looks for players (meters).")]
    [Min(1f)] public float cycleDistance = 40f;
    [Tooltip("The target is dropped when it gets farther than this (meters).")]
    [Min(1f)] public float keepDistance = 90f;
    [Tooltip("Clicking on nothing (the ground, the sky) clears the target.")]
    public bool clearOnEmptyClick = false;
    [Tooltip("Drop the target when it dies.")]
    public bool clearOnDeath = true;
    [Tooltip("Layers a click can hit (characters and what may stand in front of them).")]
    public LayerMask selectLayers = ~0;

    [Header("Highlight")]
    [Tooltip("Draw a ring under the target.")]
    public bool showRing = true;
    [Tooltip("Optional: your own highlight object (a decal, an outline) placed on the target instead of the ring.")]
    public GameObject highlightPrefab;
    public Color enemyColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    public Color partyColor = new Color(0.3f, 1f, 0.45f, 0.95f);
    public Color allyColor = new Color(0.35f, 0.65f, 1f, 0.95f);
    public Color neutralColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
    [Min(0.01f)] public float ringWidth = 0.06f;
    [Tooltip("Material of the ring (an unlit material that shows vertex colours). Empty = URP Particles/Unlit, else Sprites/Default.")]
    public Material ringMaterial;

    [Header("Interface")]
    [Tooltip("Create the target frame, the party frame, the inspect window, the nameplates and highlights of other " +
             "players and the dungeon portal panel (each one only when the scene has none of its own).")]
    public bool createInterface = true;

    /// <summary>When true (the default), a Player Targeting is created by itself when a game starts with a player.</summary>
    public static bool AutoCreate = true;

    /// <summary>This machine's targeting (null on a dedicated server).</summary>
    public static PlayerTargeting Instance { get; private set; }

    /// <summary>The target changed (null = none).</summary>
    public event Action<CombatEntity> TargetChanged;

    /// <summary>The Target Menu key was pressed (the target frame opens its menu).</summary>
    public event Action MenuRequested;

    /// <summary>A dungeon portal was clicked (the portal panel shows its free player slots), or deselected (null).</summary>
    public event Action<Portal> PortalSelected;

    private CombatEntity target;
    private Portal selectedPortal;
    private GlobalInputBinding selectInput, cycleInput, inspectInput, menuInput;
    private LineRenderer ring;
    private GameObject customHighlight;
    private bool autoCreated;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly List<CombatEntity> candidates = new List<CombatEntity>();

    /// <summary>The current target, or null.</summary>
    public CombatEntity Target => target;

    /// <summary>The dungeon portal last clicked, or null (see <see cref="PortalInfoPanel"/>).</summary>
    public Portal SelectedPortal => selectedPortal;

    /// <summary>Selects a dungeon portal (null deselects it).</summary>
    public void SelectPortal(Portal portal)
    {
        if (portal != null && !portal.LeadsToDungeon)
            portal = null;
        if (ReferenceEquals(portal, selectedPortal))
            return;
        selectedPortal = portal;
        try { PortalSelected?.Invoke(portal); }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    /// <summary>This machine's player.</summary>
    public static CombatEntity Me => PlayerLocator.Local;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        AutoCreate = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (!AutoCreate || Application.isBatchMode || Instance != null)
            return;
        var go = new GameObject("Player Targeting");
        DontDestroyOnLoad(go);
        PlayerTargeting t = go.AddComponent<PlayerTargeting>();
        t.autoCreated = true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A configured one (placed in a scene) replaces the automatic one.
            if (Instance.autoCreated)
                Destroy(Instance.gameObject);
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        selectInput = GlobalInputBinding.Create(new[] { "SelectTarget" }, selectControls, "Select Target", true, true, this);
        cycleInput = GlobalInputBinding.Create(new[] { "CycleTarget" }, cycleControls, "Cycle Target", true, true, this);
        inspectInput = GlobalInputBinding.Create(new[] { "InspectTarget" }, inspectControls, "Inspect Target", true, true, this);
        menuInput = GlobalInputBinding.Create(new[] { "TargetMenu" }, menuControls, "Target Menu", true, true, this);
        if (createInterface)
        {
            if (GetComponent<TargetFrame>() == null) gameObject.AddComponent<TargetFrame>();
            if (GetComponent<PartyFrame>() == null) gameObject.AddComponent<PartyFrame>();
            if (GetComponent<InspectWindow>() == null) gameObject.AddComponent<InspectWindow>();
            if (PlayerNameplates.Instance == null) gameObject.AddComponent<PlayerNameplates>();
            if (PlayerHighlights.Instance == null) gameObject.AddComponent<PlayerHighlights>();
            if (PortalInfoPanel.Instance == null) gameObject.AddComponent<PortalInfoPanel>();
        }
    }

    private void OnDestroy()
    {
        selectInput?.Dispose();
        cycleInput?.Dispose();
        inspectInput?.Dispose();
        menuInput?.Dispose();
        if (ring != null) Destroy(ring.gameObject);
        if (customHighlight != null) Destroy(customHighlight);
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------ target

    /// <summary>Makes <paramref name="e"/> the target (null clears it).</summary>
    public void SetTarget(CombatEntity e)
    {
        if (e != null && (e == Me || !Targetable(e)))
            return;
        if (ReferenceEquals(e, target))
            return;
        target = e;
        if (customHighlight != null)
            Destroy(customHighlight);
        if (target != null && highlightPrefab != null)
        {
            customHighlight = Instantiate(highlightPrefab, target.transform);
            customHighlight.transform.localPosition = Vector3.zero;
        }
        try { TargetChanged?.Invoke(target); }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    public void ClearTarget() => SetTarget(null);

    /// <summary>Can <paramref name="e"/> be targeted by this machine's player?</summary>
    public bool Targetable(CombatEntity e)
    {
        if (e == null || !e.isActiveAndEnabled)
            return false;
        if (e.Kind == CombatEntity.EntityKind.Player)
            return Me == null || !CombatRules.AreSeparated(Me, e);
        return targetMobs && e.Kind == CombatEntity.EntityKind.Mob && (Me == null || !CombatRules.AreSeparated(Me, e));
    }

    /// <summary>How <paramref name="e"/> relates to this machine's player (the ring and frame colours).</summary>
    public static CombatRelation RelationOf(CombatEntity e) => Me != null && e != null ? CombatRelations.Get(Me, e) : CombatRelation.Neutral;

    public Color ColorOf(CombatEntity e)
    {
        switch (RelationOf(e))
        {
            case CombatRelation.Enemy: return enemyColor;
            case CombatRelation.Party: return partyColor;
            case CombatRelation.Ally: return allyColor;
            default: return neutralColor;
        }
    }

    // ------------------------------------------------------------------ every frame

    private void Update()
    {
        CombatEntity me = Me;
        if (me == null || GameNet.NetAuthority.IsHeadless)
        {
            if (!ReferenceEquals(target, null))
                ClearTarget();
            UpdateRing();
            return;
        }
        // A destroyed target (disconnected, despawned) compares equal to null but is still referenced: dropped too.
        if (!ReferenceEquals(target, null) && (target == null || !Targetable(target) || (clearOnDeath && target.IsDead) ||
                               Vector3.Distance(me.Position, target.Position) > keepDistance))
            ClearTarget();

        if (selectInput != null && selectInput.Pressed && !PointerOverUI())
            SelectUnderPointer(me);
        if (cycleInput != null && cycleInput.Pressed)
            Cycle(me);
        if (inspectInput != null && inspectInput.Pressed)
        {
            if (target != null && target.Kind == CombatEntity.EntityKind.Player)
                PlayerActions.Inspect(me, target);
            else
                PlayerActions.Say("Select a player first (click on them or press " + KeyLabel(cycleInput) + ").");
        }
        if (menuInput != null && menuInput.Pressed && target != null)
            MenuRequested?.Invoke();
        UpdateRing();
    }

    private static bool PointerOverUI()
    {
        EventSystem es = EventSystem.current;
        return es != null && Cursor.lockState != CursorLockMode.Locked && es.IsPointerOverGameObject();
    }

    private void SelectUnderPointer(CombatEntity me)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector2 screen;
        if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
            screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        else
            screen = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screen);
        PhysicsScene physics = ScenePhysics.Of(me.gameObject);
        int n = physics.Raycast(ray.origin, ray.direction, hits, selectDistance, selectLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, 0, n, HitComparer.Instance);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null)
                continue;
            CombatEntity e = CombatEntity.Resolve(c);
            if (e == me || (e != null && CombatRules.ControllingPlayer(e) == me && e.Kind != CombatEntity.EntityKind.Player))
                continue; // your own body, weapons and summons
            if (e != null && Targetable(e))
            {
                SetTarget(e);
                return;
            }
            // A dungeon portal: its panel shows how many players it can still take.
            Portal portal = e == null ? c.GetComponentInParent<Portal>() : null;
            if (portal != null && portal.LeadsToDungeon && portal.IsOpen)
            {
                SelectPortal(portal);
                return;
            }
            if (!c.isTrigger)
                break; // a wall or the ground in front
        }
        if (clearOnEmptyClick)
            ClearTarget();
    }

    /// <summary>The next player (or mob) in front, ordered by how close to the middle of the view they are.</summary>
    private void Cycle(CombatEntity me)
    {
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : me.transform.forward;
        forward.y = 0f;
        candidates.Clear();
        IReadOnlyList<CombatEntity> pool = targetMobs ? CombatEntity.All : CombatEntity.Players;
        for (int i = 0; i < pool.Count; i++)
        {
            CombatEntity e = pool[i];
            if (e == null || e == me || e.IsDead || !Targetable(e))
                continue;
            Vector3 to = e.Position - me.Position;
            if (to.magnitude > cycleDistance)
                continue;
            to.y = 0f;
            if (to.sqrMagnitude > 1f && forward.sqrMagnitude > 0.01f && Vector3.Angle(forward, to) > 80f)
                continue;
            candidates.Add(e);
        }
        if (candidates.Count == 0)
        {
            PlayerActions.Say("Nobody to target nearby.");
            return;
        }
        candidates.Sort((a, b) => Score(me, forward, a).CompareTo(Score(me, forward, b)));
        int index = target != null ? candidates.IndexOf(target) : -1;
        SetTarget(candidates[(index + 1) % candidates.Count]);
    }

    private static float Score(CombatEntity me, Vector3 forward, CombatEntity e)
    {
        Vector3 to = e.Position - me.Position;
        float d = to.magnitude;
        to.y = 0f;
        float angle = to.sqrMagnitude > 0.01f && forward.sqrMagnitude > 0.01f ? Vector3.Angle(forward, to) : 0f;
        return angle / 10f + d / 10f;
    }

    // ------------------------------------------------------------------ highlight

    private void UpdateRing()
    {
        bool show = showRing && highlightPrefab == null && target != null;
        if (!show)
        {
            if (ring != null && ring.enabled)
                ring.enabled = false;
            return;
        }
        if (ring == null)
            ring = CreateRing();
        ring.enabled = true;
        float radius = Mathf.Max(0.4f, target.Radius * 1.4f);
        Vector3 center = target.Position + Vector3.up * 0.06f;
        float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
        int count = ring.positionCount;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * pulse);
        }
        Color c = ColorOf(target);
        ring.startColor = ring.endColor = c;
        ring.widthMultiplier = ringWidth;
    }

    private LineRenderer CreateRing()
    {
        var go = new GameObject("Target Ring");
        DontDestroyOnLoad(go);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 48;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 2;
        if (ringMaterial != null)
            lr.sharedMaterial = ringMaterial;
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
                lr.sharedMaterial = new Material(shader) { name = "Target Ring" };
        }
        return lr;
    }

    private static string KeyLabel(GlobalInputBinding b)
    {
        InputAction a = b != null ? b.ReadAction : null;
        return a != null ? a.GetBindingDisplayString() : "the Cycle Target key";
    }

    private sealed class HitComparer : IComparer<RaycastHit>
    {
        public static readonly HitComparer Instance = new HitComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
