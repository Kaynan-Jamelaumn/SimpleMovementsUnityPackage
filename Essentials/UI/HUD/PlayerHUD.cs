using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's HUD, with one warm, soft look shared by every part:
/// <list type="bullet">
/// <item><b>Status UI</b> (<see cref="HudStatusFrame"/>, top left): portrait with the level, name and class, health /
/// mana / stamina bars that glide and leave a fading trail after a loss, experience, and the needs gauges.</item>
/// <item><b>Abilities UI</b> (<see cref="HudAbilityBar"/>, above the hotbar): icon and key of each ability key, a glow
/// while casting, a cooldown sweep with the seconds left, charges, hover information, click to cast; the active trait
/// beside it.</item>
/// <item>The inventory <b>hotbar</b> and the <b>quickslots</b> restyled and docked with it.</item>
/// </list>
/// <para><b>Building:</b> the inspector's <b>Build Status UI</b> and <b>Build Abilities UI</b> buttons (also
/// <i>Tools ▸ SimpleMovements ▸ Player ▸ Player HUD</i>) build them into your prefab / scene, where you can move,
/// resize, restyle or replace any part; the game then uses what you built. Parts not built are generated when the game
/// starts (Build Missing At Runtime).</para>
/// <para><b>Your own UI:</b> assign your own Status UI / Abilities UI objects, or prefabs (status frame, ability bar,
/// ability slot, trait slot, need gauge) used when building, or a Custom Root to build inside your own panel.</para>
/// Added by the Inventory Manager (Player HUD option) when missing; shown only for the player of this machine.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHUD : MonoBehaviour
{
    [Header("Built UI (Build buttons above, or built when the game starts)")]
    [Tooltip("The HUD's root (a full-screen area in the player's canvas).")]
    public RectTransform hudRoot;
    [Tooltip("The status UI: built by Build Status UI, or your own Hud Status Frame.")]
    public HudStatusFrame statusUI;
    [Tooltip("The abilities UI: built by Build Abilities UI, or your own Hud Ability Bar.")]
    public HudAbilityBar abilityUI;
    [Tooltip("The hover panel (built with the first part).")]
    public HudTooltip tooltipUI;
    [Tooltip("Parts that were not built in the editor are built when the game starts.")]
    public bool buildMissingAtRuntime = true;

    [Header("Parts")]
    [Tooltip("Portrait, name, level, health, mana, stamina and experience.")]
    public bool statusFrame = true;
    [Tooltip("Ring gauges of the other statuses (hunger, thirst, sleep, sanity, body heat, oxygen, weight).")]
    public bool needs = true;
    [Tooltip("The abilities bar above the hotbar.")]
    public bool abilityBar = true;
    [Tooltip("The active trait's skill beside the abilities bar.")]
    public bool activeTrait = true;
    [Tooltip("Restyle the inventory hotbar with the HUD's look (when the game starts).")]
    public bool styleHotbar = true;
    [Tooltip("Restyle the quickslots and put them right of the hotbar.")]
    public bool dockQuickSlots = true;
    [Tooltip("Hide the status bars built by the Inventory UI Builder (the status UI replaces them).")]
    public bool hideOldStatusBars = true;

    [Header("Look")]
    public HudPalette palette = new HudPalette();
    [Tooltip("Size of an ability slot (pixels at 1920x1080).")]
    [Range(36f, 96f)] public float abilitySlotSize = 58f;
    [Tooltip("Size of the active trait slot.")]
    [Range(36f, 110f)] public float traitSlotSize = 66f;
    [Tooltip("Margin from the screen edges.")]
    public Vector2 margin = new Vector2(24f, 18f);
    [Tooltip("Space between the hotbar and the abilities bar.")]
    public float dockSpacing = 10f;

    [Header("Your Own UI (optional, used when building)")]
    [Tooltip("Build the HUD inside this RectTransform (your own canvas or panel). Empty = the player's canvas.")]
    public RectTransform customRoot;
    [Tooltip("Your status UI prefab (a Hud Status Frame with its parts assigned). Empty = the built-in one.")]
    public HudStatusFrame statusFramePrefab;
    [Tooltip("Your abilities bar prefab (a Hud Ability Bar with its parts assigned). Empty = the built-in one.")]
    public HudAbilityBar abilityBarPrefab;
    [Tooltip("Your ability slot prefab (a Hud Ability Slot with its parts assigned): one is made per ability key.")]
    public HudAbilitySlot abilitySlotPrefab;
    [Tooltip("Your active trait slot prefab (a Hud Ability Slot). Empty = the built-in gold one.")]
    public HudAbilitySlot traitSlotPrefab;
    [Tooltip("Your needs gauge prefab (a Hud Need Gauge): one is made per status.")]
    public HudNeedGauge needGaugePrefab;

    // ------------------------------------------------------------------ state
    private GameObject playerRoot;
    private PlayerStatusController status;
    private InventoryManager inventory;
    private AbilitiesStateMachine keys;
    private PlayerAbilityController abilities;
    private TraitManager traits;
    private Transform proxyHolder;
    private HudAbilitySlot tooltipSlot;
    private float tooltipRefresh;
    private bool running;
    private float waitStart, nextOwnerCheck;
    private RectTransform hotbarPanel;
    private bool quickSlotsStyled;

    /// <summary>The player this HUD is for (found from the Inventory Manager / status controller).</summary>
    public GameObject PlayerRoot { get { if (playerRoot == null) FindPlayer(); return playerRoot; } }

    // ------------------------------------------------------------------ building (editor buttons and runtime)
    /// <summary>Builds (or rebuilds) the status UI: the player frame and the needs gauges.</summary>
    public HudStatusFrame BuildStatusUI()
    {
        FindPlayer();
        RemoveStatusUI();
        RectTransform root = EnsureRoot();
        if (statusFramePrefab != null)
        {
            statusUI = Instantiate(statusFramePrefab, root, false);
            statusUI.name = "HUD Status";
        }
        else
            statusUI = HudStatusFrame.Build(root, this, playerRoot, needs);
        EnsureTooltip();
        return statusUI;
    }

    /// <summary>Builds (or rebuilds) the abilities UI: a slot per ability key, and the active trait.</summary>
    public HudAbilityBar BuildAbilityUI()
    {
        FindPlayer();
        RemoveAbilityUI();
        RectTransform root = EnsureRoot();
        if (abilityBarPrefab != null)
        {
            abilityUI = Instantiate(abilityBarPrefab, root, false);
            abilityUI.name = "HUD Abilities";
        }
        else
            abilityUI = HudAbilityBar.Build(root, this, keys, activeTrait);
        EnsureTooltip();
        return abilityUI;
    }

    /// <summary>Builds both.</summary>
    public void BuildAll()
    {
        if (statusFrame || needs) BuildStatusUI();
        if (abilityBar) BuildAbilityUI();
    }

    public void RemoveStatusUI()
    {
        if (statusUI != null) HudStyle.Destroy(statusUI.gameObject);
        statusUI = null;
    }

    public void RemoveAbilityUI()
    {
        if (abilityUI != null) HudStyle.Destroy(abilityUI.gameObject);
        abilityUI = null;
    }

    /// <summary>The HUD's root: Custom Root, else a full-screen area in the player's canvas (made when missing).</summary>
    public RectTransform EnsureRoot()
    {
        if (customRoot != null) return hudRoot = customRoot;
        if (hudRoot != null) return hudRoot;
        FindPlayer();
        Canvas canvas = null;
        if (Application.isPlaying)
            canvas = GameplayUIRoot.CanvasFor(inventory != null ? (Component)inventory : status != null ? status : this);
        else if (playerRoot != null)
        {
            Canvas c = playerRoot.GetComponentInChildren<Canvas>(true);
            canvas = c != null ? c.rootCanvas : null;
            if (canvas == null)
            {
                // A canvas of the player's own, like the inventory's.
                canvas = UITheme.Canvas("HUD Canvas", 10, playerRoot.transform);
            }
        }
        if (canvas == null) canvas = UITheme.Canvas("Player HUD Canvas", 5, null);
        hudRoot = HudStyle.Stretch(HudStyle.Rect("Player HUD", canvas.transform));
        hudRoot.SetAsFirstSibling(); // the inventory, menus and messages draw over it
        return hudRoot;
    }

    private void EnsureTooltip()
    {
        if (tooltipUI != null || hudRoot == null) return;
        tooltipUI = HudTooltip.Create(hudRoot, palette);
        tooltipUI.name = "HUD Tooltip";
    }

    private void FindPlayer()
    {
        if (playerRoot != null) return;
        inventory = GetComponent<InventoryManager>();
        if (inventory == null) inventory = GetComponentInParent<InventoryManager>();
        status = inventory != null && inventory.PlayerStatus != null ? inventory.PlayerStatus : GetComponentInParent<PlayerStatusController>();
        if (status == null) status = GetComponentInChildren<PlayerStatusController>(true);
        playerRoot = status != null ? CharacterProfileApplier.RootOf(status) : inventory != null && inventory.Player != null ? inventory.Player : gameObject;
        if (inventory == null) inventory = playerRoot.GetComponentInChildren<InventoryManager>(true);
        if (status == null) status = playerRoot.GetComponentInChildren<PlayerStatusController>(true);
        keys = playerRoot.GetComponentInChildren<AbilitiesStateMachine>(true);
        abilities = playerRoot.GetComponentInChildren<PlayerAbilityController>(true);
        traits = status != null && status.TraitManager != null ? status.TraitManager : playerRoot.GetComponentInChildren<TraitManager>(true);
    }

    // ------------------------------------------------------------------ runtime
    private void Start()
    {
        FindPlayer();
        waitStart = Time.unscaledTime;
        // Until this machine is known to own the player, a HUD saved in the prefab stays hidden (another player's copy).
        if (hudRoot != null && hudRoot != customRoot) hudRoot.gameObject.SetActive(false);
        TryStart();
    }

    private void Update()
    {
        if (running || Time.unscaledTime < nextOwnerCheck) return;
        nextOwnerCheck = Time.unscaledTime + 0.25f;
        TryStart();
        if (!running && Time.unscaledTime - waitStart > 20f)
            enabled = false; // another player's copy: its HUD is on its own machine
    }

    private void TryStart()
    {
        if (running || playerRoot == null || !GameNet.NetAuthority.IsLocallyControlled(playerRoot)) return;
        running = true;
        RectTransform root = EnsureRoot();
        root.gameObject.SetActive(true);
        if (statusUI == null && buildMissingAtRuntime && (statusFrame || needs)) BuildStatusUI();
        if (abilityUI == null && buildMissingAtRuntime && abilityBar) BuildAbilityUI();
        EnsureTooltip();

        if (proxyHolder == null)
        {
            proxyHolder = new GameObject("HUD Status Proxies").transform;
            proxyHolder.SetParent(transform, false);
        }
        if (hideOldStatusBars && statusUI != null)
            foreach (StatusBarsUI old in playerRoot.GetComponentsInChildren<StatusBarsUI>(true))
                if (old != null) old.gameObject.SetActive(false);
        if (statusUI != null) statusUI.Bind(this, playerRoot, status, proxyHolder);
        if (abilityUI != null) abilityUI.Bind(this, keys, abilities, activeTrait ? traits : null);
        if (styleHotbar) StyleHotbar();
        else FindHotbar();
        InventoryEventSystems.EnsureSingle(); // hover and clicks on the slots
    }

    private void OnDestroy()
    {
        // A HUD made at runtime goes with its player; one saved in the prefab is part of it already.
        if (Application.isPlaying && hudRoot != null && hudRoot != customRoot && !hudRoot.IsChildOf(transform.root))
            Destroy(hudRoot.gameObject);
    }

    private void LateUpdate()
    {
        if (!running) return;
        if (statusUI != null) statusUI.Tick();
        if (abilityUI != null) abilityUI.Tick();
        if (dockQuickSlots) DockQuickSlots();
        PlaceDock();
        if (tooltipSlot != null && tooltipUI != null && Time.unscaledTime >= tooltipRefresh)
        {
            tooltipRefresh = Time.unscaledTime + 0.2f;
            tooltipSlot.TooltipText(out string t, out string b, out string f);
            tooltipUI.Refresh(t, b, f);
        }
    }

    // ------------------------------------------------------------------ hotbar and quickslots
    private void FindHotbar()
    {
        GameObject[] hot = inventory != null ? inventory.HotbarSlots : null;
        if (hot == null || hot.Length == 0 || hot[0] == null) return;
        Transform grid = hot[0].transform.parent;
        hotbarPanel = grid != null && grid.parent is RectTransform p ? p : grid as RectTransform;
    }

    private void StyleHotbar()
    {
        FindHotbar();
        if (hotbarPanel == null) return;
        HudStyle.Restyle(hotbarPanel.GetComponent<Image>(), palette.panel, 14, palette.panelBorder);
        Shadow sh = hotbarPanel.GetComponent<Shadow>();
        if (sh != null && !(sh is Outline)) { sh.effectColor = palette.shadow; sh.effectDistance = new Vector2(0f, -3f); }
        foreach (GameObject slot in inventory.HotbarSlots)
        {
            if (slot == null) continue;
            HudStyle.Restyle(slot.GetComponent<Image>(), palette.slot, 9, palette.slotBorder);
            InventorySlotStyle style = slot.GetComponent<InventorySlotStyle>();
            if (style != null) style.SetBaseColor(palette.slot);
        }
    }

    private void DockQuickSlots()
    {
        if (quickSlotsStyled || inventory == null) return;
        QuickSlotBar bar = inventory.QuickSlots;
        if (bar == null || bar.BarRoot == null || bar.Cells.Count == 0) return; // the bar builds itself at its start
        quickSlotsStyled = true;
        HudStyle.Restyle(bar.BarRoot.GetComponent<Image>(), palette.panel, 14, palette.panelBorder);
        var sh = bar.BarRoot.GetComponent<Shadow>();
        if (sh == null || sh is Outline) sh = bar.BarRoot.gameObject.AddComponent<Shadow>();
        sh.effectColor = palette.shadow;
        sh.effectDistance = new Vector2(0f, -3f);
        foreach (QuickSlotCell c in bar.Cells)
            if (c != null) c.ApplyStyle(palette);
    }

    /// <summary>The abilities bar centred above the hotbar (the trait panel moves with it), the quickslots right of it.</summary>
    private void PlaceDock()
    {
        RectTransform root = hudRoot;
        if (root == null) return;
        bool hasHotbar = hotbarPanel != null && hotbarPanel.gameObject.activeInHierarchy;
        float hotbarTop = margin.y, centre = 0f;
        var corners = new Vector3[4];
        if (hasHotbar)
        {
            hotbarPanel.GetWorldCorners(corners); // 0 bottom left, 2 top right, 3 bottom right
            Vector2 bl = root.InverseTransformPoint(corners[0]), tr = root.InverseTransformPoint(corners[2]);
            Rect r = root.rect;
            hotbarTop = tr.y - r.yMin + dockSpacing;
            centre = (bl.x + tr.x) * 0.5f - r.center.x;
        }
        if (abilityUI != null && abilityUI.dockAboveHotbar)
        {
            var rt = (RectTransform)abilityUI.transform;
            if (rt.parent == root)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(centre, hotbarTop);
            }
        }
        if (dockQuickSlots && quickSlotsStyled && hasHotbar && inventory != null && inventory.QuickSlots != null && inventory.QuickSlots.BarRoot != null)
        {
            RectTransform q = inventory.QuickSlots.BarRoot;
            float scale = root.lossyScale.x > 1e-5f ? root.lossyScale.x : 1f;
            q.pivot = new Vector2(0f, 0f);
            q.position = corners[3] + hotbarPanel.right * (12f * scale); // right of the hotbar, bottoms aligned
        }
    }

    // ------------------------------------------------------------------ tooltip
    public void ShowTooltip(HudAbilitySlot slot)
    {
        if (tooltipUI == null || slot == null) return;
        tooltipSlot = slot;
        slot.TooltipText(out string t, out string b, out string f);
        tooltipUI.Show((RectTransform)slot.transform, t, b, f);
        tooltipRefresh = Time.unscaledTime + 0.2f;
    }

    public void HideTooltip(HudAbilitySlot slot)
    {
        if (tooltipUI == null || slot != tooltipSlot) return;
        tooltipUI.Hide((RectTransform)slot.transform);
        tooltipSlot = null;
    }

    /// <summary>Rebuilds everything at runtime (e.g. after changing the palette in Play Mode).</summary>
    [ContextMenu("Rebuild HUD (Play Mode)")]
    private void RebuildNow()
    {
        if (!Application.isPlaying) { BuildAll(); return; }
        BuildAll();
        running = false;
        TryStart();
    }
}
