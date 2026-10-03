using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The player's inventory: hotbar and inventory slots (created at runtime), equipment slots (placed in the UI),
/// drag and drop, dropping into the world, picking up, storage, and the item in hand.
/// <para>
/// Integration: after every change it tells the <see cref="EquipmentManager"/> to sync with the equipment slots (so
/// worn items and set bonuses always match the slots), and equips the weapon of the selected hotbar slot in the
/// <see cref="WeaponController"/>. The Use Item input attacks with that weapon (hold to charge) or uses the item.
/// Missing references are found automatically on the player; the Inventory Manager inspector shows what is missing.
/// </para>
/// </summary>
public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IAmmoSource
{
    [Header("Dynamic Slot Configuration")]
    [SerializeField, Tooltip("Number of hotbar slots to create dynamically")]
    private int numberOfHotBarSlots = 4;
    [SerializeField, Tooltip("Number of inventory slots to create dynamically")]
    private int numberOfInventorySlots = 20;

    [Header("Core Inventory Configuration")]
    [SerializeField, Tooltip("Prefab used to create inventory items (needs an InventoryItem component, an Image for the icon and a Text for the stack count)")]
    private GameObject itemPrefab;

    [Header("UI Management Components")]
    [SerializeField, Tooltip("Manages slot creation and layout calculations")]
    private SlotManager slotManager = new SlotManager();
    [SerializeField, Tooltip("Handles layout presets and screen adaptation")]
    private UILayoutManager uiLayoutManager = new UILayoutManager();

    [Header("UI Panel References")]
    [SerializeField, Tooltip("Main inventory panel container")]
    private GameObject inventoryParent;
    [SerializeField, Tooltip("Equipment/gear panel container (its equipment slots are found automatically)")]
    private GameObject equippableInventory;
    [SerializeField, Tooltip("Storage interaction panel container (child 0: background, child 1: slots)")]
    private GameObject storageParent;
    [SerializeField, Tooltip("Transform where held items are instantiated (the player's hand)")]
    private Transform handParent;
    [SerializeField, Tooltip("UI component for displaying item information")]
    private ItemInfo itemInfo;
    [SerializeField, Tooltip("Camera reference for world interactions (empty = the main camera)")]
    private Camera cam;

    [Header("Player References")]
    [SerializeField, Tooltip("Player GameObject reference")]
    private GameObject player;
    [SerializeField, Tooltip("Player status and stats controller (found on the player when empty)")]
    private PlayerStatusController playerStatusController;
    [SerializeField, Tooltip("Player weapon handling controller (found on the player when empty)")]
    private WeaponController weaponController;

    [Header("Armor System Integration")]
    [SerializeField, Tooltip("Armor set manager for handling set bonuses (found on the player when empty)")]
    private ArmorSetManager armorSetManager;
    [SerializeField, Tooltip("UI manager for armor set display")]
    private ArmorSetUIManager armorSetUIManager;
    [SerializeField, Tooltip("Allow OptimizeArmorSets() to re-equip the most complete set")]
    private bool enableArmorOptimization = true;
    [SerializeField, Tooltip("Log armor set changes to the Console")]
    private bool showArmorSetNotifications = true;

    [Header("Item Panels")]
    [SerializeField, Tooltip("Panel beside the inventory that shows the hovered item (icon, stats, durability, weight...). Created automatically when empty.")]
    private ItemHoverPanel hoverPanel;
    [SerializeField, Tooltip("Show the item panel while the pointer is over a slot.")]
    private bool showHoverPanel = true;
    [SerializeField, Tooltip("Popup opened by right-clicking a stack to split it (half or any amount). Created automatically when empty.")]
    private SplitStackPopup splitPopup;
    [SerializeField, Tooltip("Right-click on a stack opens the split popup. Off = right-click shows the Item Info panel (old behaviour).")]
    private bool rightClickSplits = true;

    [Header("Input")]
    [SerializeField, Tooltip("Actions of the player's input actions that open/close the inventory (the first one found is used). " +
        "The inventory listens to it itself, so no PlayerInput event has to be wired in the inspector.")]
    private string[] inventoryActionNames = { "Inventory", "OpenInventory", "ToggleInventory" };
    [SerializeField, Tooltip("Actions that use / attack with the item in hand (the first one found is used). When none of them exists, " +
        "common names (UseItem, Use, Attack, PrimaryAttack, Fire...) and then any Player action bound to the left mouse button are tried; " +
        "without any, the left mouse button itself is used (Mouse Fallback).")]
    private string[] useItemActionNames = { "UseItem", "Use", "Attack", "PrimaryAttack", "Fire" };
    [SerializeField, Tooltip("The left mouse button uses / attacks with the item in hand when the input actions have no use action.")]
    private bool mouseFallback = true;
    [SerializeField, Tooltip("Actions that open/close the Armor Sets window (the first one found is used). Without one, the window opens " +
        "with the Sets button of the inventory panel.")]
    private string[] armorSetActionNames = { "ArmorSetUI", "ArmorSets", "Sets" };
    [SerializeField, Tooltip("Tab and I open/close the inventory when the input actions have no inventory action.")]
    private bool keyboardFallback = true;

    [Header("Grid Inventory")]
    [Tooltip("Off: the classic inventory (one item per slot) - unchanged. On: a GRID where items take several cells (Item ▸ Grid Size: " +
             "1×1 potion, 1×3 sword, 2×2 helmet...) and can be turned sideways (R, or right click while dragging). The hotbar, " +
             "equipment and storage stay slots. Switching at runtime re-packs the items (see Overflow).")]
    [SerializeField] private bool useGridInventory = false;
    [Tooltip("Size, look and rules of the grid.")]
    [SerializeField] private GridInventorySettings gridSettings = new GridInventorySettings();

    [Header("Hands, Visuals & Quickslots")]
    [Tooltip("Show equipped weapons, shields and armour on the character: weapons in hand while fighting, sheathed when not " +
             "(adds an Equipment Visuals component to the player when missing).")]
    [SerializeField] private bool equipmentVisuals = true;
    [Tooltip("Shields and weapon guards can block (adds a Block Controller to the player when missing).")]
    [SerializeField] private bool shieldBlocking = true;
    [Tooltip("The consumable quickslot bar next to the hotbar (adds a Quick Slot Bar when missing).")]
    [SerializeField] private bool quickSlots = true;
    [Tooltip("Short messages above the hotbar: an item cannot be used, does not fit, no ammo, an off-hand item stowed...")]
    [SerializeField] private bool showFeedbackMessages = true;
    [Tooltip("The message line (created automatically when empty).")]
    [SerializeField] private InventoryFeedback feedback;

    [Tooltip("Custom effect identifiers for future special mechanics")]
    public List<string> specialEffectIds = new List<string>();

    // Force serialization helper
    [SerializeField, HideInInspector]
    private bool _forceSerialize = true;

    // State management components (runtime only)
    [NonSerialized] private DragHandler dragHandler;
    [NonSerialized] private UIStateManager uiStateManager;
    [NonSerialized] private StorageManager storageManager;
    [NonSerialized] private Mouse mouse;

    // Change detection for editor (runtime only)
    [NonSerialized] private int previousHotbarSlots;
    [NonSerialized] private int previousInventorySlots;

    // Armor system cache for performance
    [NonSerialized] private ArmorSetUtils.ArmorCache armorCache;

    // Equipment slots (placed by hand under the equipment panel), cached
    [NonSerialized] private readonly List<InventorySlot> equipmentSlots = new List<InventorySlot>();
    [NonSerialized] private bool equipmentSlotsDirty = true;
    // Inventory + hotbar slots (where items are stored), cached
    [NonSerialized] private GameObject[] storageSlots = Array.Empty<GameObject>();
    [NonSerialized] private bool storageSlotsDirty = true;
    [NonSerialized] private EquipmentManager equipmentManager;
    [NonSerialized] private bool subscribedWeapon;
    [NonSerialized] private bool subscribedSets;
    // Input (own subscription to the player's shared input actions)
    [NonSerialized] private PlayerInput sharedInput;
    [NonSerialized] private InputAction inventoryAction;
    [NonSerialized] private InputAction useItemAction;
    [NonSerialized] private InputAction armorSetAction;
    [NonSerialized] private bool enabledInventoryAction, enabledUseItemAction, enabledArmorSetAction;
    [NonSerialized] private int lastToggleFrame = -1;
    [NonSerialized] private int lastArmorSetToggleFrame = -1;
    [NonSerialized] private int lastUseFrame = -1;
    [NonSerialized] private bool warnedNoWeaponController;
    [NonSerialized] private InputActionPhase lastUsePhase;
    // Hands
    [NonSerialized] private HandState hands;
    [NonSerialized] private InventoryItem stowedOffHand;
    // Companions
    [NonSerialized] private EquipmentVisuals visuals;
    [NonSerialized] private QuickSlotBar quickSlotBar;
    // Grid
    [NonSerialized] private GridInventory grid;
    [NonSerialized] private GridInventoryView gridView;
    [NonSerialized] private bool gridActive;
    [NonSerialized] private bool previousUseGrid;
    [NonSerialized] private int previousGridColumns, previousGridRows;
    [NonSerialized] private CombatInputBinding rotateInput;
    [NonSerialized] private InventoryItem dragItem;
    [NonSerialized] private bool dragFromGrid;
    [NonSerialized] private GridRect dragOrigin;
    [NonSerialized] private bool dragOriginRotated;
    [NonSerialized] private bool dragRotated;
    [NonSerialized] private Vector2Int dragGrab;
    [NonSerialized] private bool dragStyled;
    [NonSerialized] private readonly List<InventoryItem> occupantBuffer = new List<InventoryItem>();

    /// <summary>Raised after items were added, removed or moved (UI refresh, quests, crafting).</summary>
    public event Action InventoryChanged;

    // ------------------------------------------------------------------ properties
    /// <summary>
    /// The equipment slots (helmet, chest, rings...): every slot with a special type under the equipment panel or the
    /// slot manager's equipment parent. Items in them are equipped by the <see cref="EquipmentManager"/>.
    /// </summary>
    public IReadOnlyList<InventorySlot> EquipmentSlots
    {
        get
        {
            if (equipmentSlotsDirty)
                RefreshEquipmentSlots();
            return equipmentSlots;
        }
    }

    /// <summary>Finds the equipment slots again (call after adding or removing equipment slots at runtime).</summary>
    public void RefreshEquipmentSlots()
    {
        equipmentSlotsDirty = false;
        equipmentSlots.Clear();
        CollectEquipmentSlots(slotManager != null ? slotManager.EquipmentSlotsParent : null);
        CollectEquipmentSlots(equippableInventory != null ? equippableInventory.transform : null);
    }

    private void CollectEquipmentSlots(Transform root)
    {
        if (root == null)
            return;
        foreach (InventorySlot slot in root.GetComponentsInChildren<InventorySlot>(true))
        {
            if (slot != null && slot.SlotType != SlotType.Common && !slot.IsHotbarSlot && !equipmentSlots.Contains(slot))
                equipmentSlots.Add(slot);
        }
    }

    /// <summary>Inventory and hotbar slots together (where items are counted, removed and added).</summary>
    public GameObject[] StorageSlots
    {
        get
        {
            if (storageSlotsDirty)
            {
                storageSlotsDirty = false;
                var list = new List<GameObject>();
                if (slotManager?.InventorySlots != null) list.AddRange(slotManager.InventorySlots);
                if (slotManager?.HotbarSlots != null) list.AddRange(slotManager.HotbarSlots);
                list.RemoveAll(s => s == null);
                storageSlots = list.ToArray();
            }
            return storageSlots;
        }
    }

    // Properties - UI Access
    public Transform HandParent => handParent;
    public GameObject[] Slots => slotManager?.InventorySlots;
    public GameObject[] HotbarSlots => slotManager?.HotbarSlots;
    public GameObject Player => player;
    public PlayerStatusController PlayerStatus => playerStatusController;
    public WeaponController WeaponController => weaponController;
    public GameObject ItemPrefab => itemPrefab;
    public bool IsStorageOpened => storageManager?.IsStorageOpened ?? false;
    public bool IsInventoryOpened => uiStateManager?.IsInventoryOpened ?? false;
    public SlotManager.LayoutData CurrentLayout => slotManager?.LegacyCurrentLayout;
    public UILayoutManager LayoutManager => uiLayoutManager;
    public ArmorSetManager ArmorSetManager => armorSetManager;
    public ArmorSetUIManager ArmorSetUIManager => armorSetUIManager;

    /// <summary>The player's equipment manager (applies what equipped items do).</summary>
    public EquipmentManager Equipment
    {
        get
        {
            if (equipmentManager == null && Application.isPlaying)
            {
                Component anchor = playerStatusController != null ? playerStatusController : player != null ? player.transform : null;
                if (anchor != null)
                    equipmentManager = EquipmentManager.For(anchor);
            }
            return equipmentManager;
        }
    }

    /// <summary>The item in the selected hotbar slot, or null.</summary>
    public InventoryItem SelectedHotbarItem => HotbarHandler.GetCurrentHeldItem(HotbarSlots);

    /// <summary>The player's combat entity (null without a player).</summary>
    public CombatEntity PlayerEntity => player != null ? CombatEntity.Resolve(player) : null;

    /// <summary>The OFF HAND slot: the equipment slot of type Shield (shields and one-handed weapons that can be dual wielded).</summary>
    public InventorySlot OffHandSlot
    {
        get
        {
            foreach (InventorySlot s in EquipmentSlots)
                if (s != null && s.SlotType == SlotType.Shield)
                    return s;
            return null;
        }
    }

    /// <summary>The item in the Off Hand slot (active or stowed), or null.</summary>
    public InventoryItem OffHandItem
    {
        get
        {
            InventorySlot s = OffHandSlot;
            return s != null && s.heldItem != null ? s.heldItem.GetComponent<InventoryItem>() : null;
        }
    }

    /// <summary>What the hands hold after the hand rules (two-handed weapons stow the off-hand item).</summary>
    public HandState Hands => hands;
    /// <summary>The off-hand item stowed by a two-handed weapon (null = none).</summary>
    public InventoryItem StowedOffHandItem => stowedOffHand;

    /// <summary>The grid of the inventory (null in slot mode).</summary>
    public GridInventory Grid => gridActive ? grid : null;
    public bool UseGridInventory => useGridInventory;
    public GridInventorySettings GridSettings => gridSettings;
    /// <summary>The number of inventory slots: the grid's cells in grid mode.</summary>
    public int InventorySlotCount => useGridInventory ? gridSettings.CellCount : numberOfInventorySlots;
    public QuickSlotBar QuickSlots => quickSlotBar;
    public EquipmentVisuals Visuals => visuals;
    public bool IsDraggingGridItem => dragItem != null && dragHandler != null && dragHandler.IsDragging;
    /// <summary>Is <paramref name="item"/> the item being dragged?</summary>
    public bool IsBeingDragged(InventoryItem item) => item != null && dragHandler != null && dragHandler.DraggedObject == item.gameObject;

    public int NumberOfHotBarSlots
    {
        get => numberOfHotBarSlots;
        set
        {
            if (value != numberOfHotBarSlots)
            {
                numberOfHotBarSlots = value;
                slotManager?.UpdateHotbarSlots(value);
            }
        }
    }

    public int NumberOfInventorySlots
    {
        get => numberOfInventorySlots;
        set
        {
            if (value != numberOfInventorySlots)
            {
                numberOfInventorySlots = value;
                if (!gridActive)
                    slotManager?.UpdateInventorySlots(value); // the grid's cell count comes from its columns × rows
            }
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private static readonly List<InventoryManager> instances = new List<InventoryManager>();

    /// <summary>
    /// Is the inventory (or a storage) of the player owning <paramref name="anyOnPlayer"/> open? Gameplay inputs that share
    /// keys with the inventory (abilities, for example R also rotating grid items) check this.
    /// </summary>
    public static bool IsOpenFor(Component anyOnPlayer)
    {
        if (anyOnPlayer == null)
            return false;
        Transform root = AbilitiesStateMachine.PlayerRoot(anyOnPlayer);
        for (int i = 0; i < instances.Count; i++)
        {
            InventoryManager m = instances[i];
            if (m == null || !(m.IsInventoryOpened || m.IsStorageOpened))
                continue;
            Transform own = m.player != null ? AbilitiesStateMachine.PlayerRoot(m.player.transform) : m.transform.root;
            if (own == root || (m.player != null && anyOnPlayer.transform.IsChildOf(m.player.transform)))
                return true;
        }
        return false;
    }

    private void Awake()
    {
        if (!instances.Contains(this))
            instances.Add(this);
        InventoryEventSystems.EnsureSingle();
        if (slotManager == null) slotManager = new SlotManager();
        if (uiLayoutManager == null) uiLayoutManager = new UILayoutManager();

        InitializeComponents();
        InitializeSubSystems();
        armorCache = new ArmorSetUtils.ArmorCache();

        previousHotbarSlots = numberOfHotBarSlots;
        previousInventorySlots = numberOfInventorySlots;
    }

    private void Start()
    {
        BindInput();
        InitializeManagers();
        EnsureItemPanels();
        EnsureCompanions();
        SubscribeArmorSets();
        SubscribeWeapon();
        if (useGridInventory)
            ActivateGrid();
        previousUseGrid = useGridInventory;
        previousGridColumns = gridSettings.columns;
        previousGridRows = gridSettings.rows;
        RefreshHand(true);
        NotifyInventoryChanged();
    }

    private void Update()
    {
        HandleKeyboardFallback();
        HandleMouseFallback();
        HandleHotbarInput();
        UpdateHoverPanel();
        uiStateManager?.UpdateUI();
        dragHandler?.UpdateDraggedObjectPosition();
        uiLayoutManager?.Update();
        CheckForSlotCountChanges();
        UpdateArmorCache();
        if (weaponController != null)
            weaponController.InputBlocked = IsInventoryOpened || IsStorageOpened;
        UpdateGridInteraction();
    }

    private void OnDestroy()
    {
        instances.Remove(this);
        InputDeviceTracker.Changed -= RefreshGridHint;
        UnbindInput();
        UnsubscribeFromEvents();
        rotateInput?.Dispose();
        rotateInput = null;
    }

    // ------------------------------------------------------------------ input
    /// <summary>
    /// Listens to the player's Inventory and Use Item actions directly (the same shared input actions movement and
    /// abilities use). Before, the keys only worked when a PlayerInput component was wired to OnInventory/OnUseItem in
    /// the inspector - moving or rebuilding the inventory silently broke them. Inspector wiring still works: a key is
    /// never handled twice in one frame.
    /// </summary>
    private void BindInput()
    {
        if (sharedInput != null)
            return;
        Component owner = player != null ? player.transform : (Component)transform;
        try { sharedInput = SharedPlayerInput.Acquire(owner); }
        catch (Exception e)
        {
            Debug.LogWarning($"[Inventory] Could not read the player's input actions ({e.Message}); Tab / I still open the inventory.", this);
            return;
        }
        SharedPlayerInput.Enable(sharedInput);
        InputActionAsset asset = sharedInput.asset;

        inventoryAction = FindAction(asset, inventoryActionNames);
        if (inventoryAction != null)
        {
            inventoryAction.started += OnInventoryAction;
            if (!inventoryAction.enabled) { inventoryAction.Enable(); enabledInventoryAction = true; }
        }
        useItemAction = FindAction(asset, useItemActionNames) ?? FindAction(asset, FallbackUseNames) ?? FindLeftClickAction(asset);
        if (useItemAction == null)
        {
            var names = new List<string>();
            foreach (InputAction a in asset) names.Add(a.name);
            Debug.LogWarning($"[Inventory] No 'use item / attack' action in the player's input actions (looked for {string.Join(", ", useItemActionNames)}). " +
                             (mouseFallback ? "The left mouse button is used instead. " : "Items and weapons cannot be used: turn on Mouse Fallback or ") +
                             $"Add the action's name to 'Use Item Action Names' on the Inventory Manager. Actions found: {string.Join(", ", names)}.", this);
        }
        else
        {
            Debug.Log($"[Inventory] Use item / attack input: '{useItemAction.actionMap?.name}/{useItemAction.name}'.", this);
        }
        if (useItemAction != null)
        {
            useItemAction.started += OnUseItemAction;
            useItemAction.canceled += OnUseItemAction;
            if (!useItemAction.enabled) { useItemAction.Enable(); enabledUseItemAction = true; }
        }
        armorSetAction = FindAction(asset, armorSetActionNames);
        if (armorSetAction != null)
        {
            armorSetAction.started += OnArmorSetUI;
            if (!armorSetAction.enabled) { armorSetAction.Enable(); enabledArmorSetAction = true; }
        }
    }

    private void UnbindInput()
    {
        if (inventoryAction != null)
        {
            inventoryAction.started -= OnInventoryAction;
            if (enabledInventoryAction) inventoryAction.Disable();
        }
        if (useItemAction != null)
        {
            useItemAction.started -= OnUseItemAction;
            useItemAction.canceled -= OnUseItemAction;
            if (enabledUseItemAction) useItemAction.Disable();
        }
        if (armorSetAction != null)
        {
            armorSetAction.started -= OnArmorSetUI;
            if (enabledArmorSetAction) armorSetAction.Disable();
        }
        inventoryAction = useItemAction = armorSetAction = null;
        enabledInventoryAction = enabledUseItemAction = enabledArmorSetAction = false;
        if (sharedInput != null)
        {
            SharedPlayerInput.Disable(sharedInput);
            SharedPlayerInput.Release(sharedInput);
            sharedInput = null;
        }
    }

    private static readonly string[] FallbackUseNames = { "UseItem", "Use", "Attack", "PrimaryAttack", "Primary", "Fire", "Fire1", "LightAttack" };

    /// <summary>An action of a non-UI map bound to the left mouse button (whatever its name).</summary>
    private static InputAction FindLeftClickAction(InputActionAsset asset)
    {
        if (asset == null) return null;
        foreach (InputActionMap map in asset.actionMaps)
        {
            if (string.Equals(map.name, "UI", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (InputAction a in map.actions)
                foreach (InputBinding b in a.bindings)
                    if (!string.IsNullOrEmpty(b.path) && b.path.IndexOf("<Mouse>/leftButton", StringComparison.OrdinalIgnoreCase) >= 0)
                        return a;
        }
        return null;
    }

    private static InputAction FindAction(InputActionAsset asset, string[] names)
    {
        if (asset == null || names == null)
            return null;
        foreach (string n in names)
        {
            if (string.IsNullOrWhiteSpace(n)) continue;
            InputAction a = asset.FindAction(n, false);
            if (a != null) return a;
        }
        return null;
    }

    private void OnInventoryAction(InputAction.CallbackContext context) => RequestToggleInventory();

    private void OnUseItemAction(InputAction.CallbackContext context) => OnUseItem(context);

    /// <summary>Left mouse button → use / attack, when the input actions have no use action.</summary>
    private void HandleMouseFallback()
    {
        if (!mouseFallback || useItemAction != null)
            return;
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        if (mouse.leftButton.wasPressedThisFrame)
            HandleUsePress(true);
        else if (mouse.leftButton.wasReleasedThisFrame)
            HandleUsePress(false);
    }

    private void HandleKeyboardFallback()
    {
        if (!keyboardFallback || inventoryAction != null)
            return;
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame))
            RequestToggleInventory();
    }

    /// <summary>Toggles once per frame, however many inputs (own action, inspector event, keyboard) reported the key.</summary>
    private void RequestToggleInventory()
    {
        if (lastToggleFrame == Time.frameCount)
            return;
        lastToggleFrame = Time.frameCount;
        ToggleInventory();
    }

    // ------------------------------------------------------------------ initialization
    /// <summary>Finds missing references on the player (status, weapon controller, set manager, camera).</summary>
    private void InitializeComponents()
    {
        if (player == null)
        {
            PlayerStatusController ps = playerStatusController != null ? playerStatusController : GetComponentInParent<PlayerStatusController>();
            if (ps == null)
                ps = transform.root.GetComponentInChildren<PlayerStatusController>(true);
            if (ps == null)
                ps = InventoryUtils.OnlyInstance<PlayerStatusController>(); // never another player's
            if (ps != null)
                player = ps.gameObject;
        }
        if (playerStatusController == null)
            playerStatusController = player != null ? player.GetComponentInChildren<PlayerStatusController>() : GetComponent<PlayerStatusController>();
        if (weaponController == null)
            weaponController = player != null ? player.GetComponentInChildren<WeaponController>() : GetComponent<WeaponController>();
        if (armorSetManager == null && player != null)
            armorSetManager = player.GetComponentInChildren<ArmorSetManager>();
        if (armorSetUIManager == null)
            armorSetUIManager = GetComponentInChildren<ArmorSetUIManager>(true);

        if (playerStatusController == null)
            Debug.LogError("[Inventory] No PlayerStatusController found: assign the Player (or the Player Status Controller) on the Inventory Manager.", this);
        if (weaponController == null)
            Debug.LogWarning("[Inventory] No WeaponController found on the player: weapons in the hotbar cannot attack.", this);

        if (cam == null) cam = Camera.main;
        mouse = Mouse.current;
        ValidateUIComponents();
    }

    private void ValidateUIComponents()
    {
        if (itemPrefab == null)
            Debug.LogError("[Inventory] Item Prefab is not assigned: items cannot be created.", this);
        if (handParent == null)
            Debug.LogWarning("[Inventory] Hand Parent is not assigned: held items are not shown.", this);
    }

    private void InitializeSubSystems()
    {
        dragHandler = new DragHandler(mouse);

        if (inventoryParent == null)
        {
            Debug.LogError("[Inventory] Inventory Parent (the inventory panel) is not assigned!", this);
            return;
        }

        try
        {
            uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
            storageManager = new StorageManager(storageParent, itemPrefab);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Inventory] Failed to initialize UI systems: {e.Message}", this);
        }
    }

    private void InitializeManagers()
    {
        if (slotManager != null)
        {
            slotManager.Initialize(playerStatusController, player);
            slotManager.OnSlotsChanged += OnSlotsChanged;
            slotManager.OnLayoutChanged += OnLayoutChanged;
            slotManager.CreateAllSlots(numberOfHotBarSlots, InventorySlotCount);
        }
        if (uiLayoutManager != null)
        {
            uiLayoutManager.Initialize(slotManager);
            uiLayoutManager.OnPresetChanged += OnLayoutPresetChanged;
        }
    }

    private void SubscribeArmorSets()
    {
        if (armorSetManager == null && Equipment != null)
            armorSetManager = Equipment.ArmorSets;
        if (armorSetManager == null || subscribedSets)
            return;
        armorSetManager.OnSetCompleted += OnArmorSetCompleted;
        armorSetManager.OnSetBroken += OnArmorSetBroken;
        armorSetManager.OnSetEffectActivated += OnArmorSetEffectActivated;
        subscribedSets = true;
    }

    private void SubscribeWeapon()
    {
        if (weaponController == null || subscribedWeapon)
            return;
        weaponController.HeldItemBroke += OnHeldItemBroke;
        weaponController.AttackFailed += OnAttackFailed;
        weaponController.AmmoSource = this;
        subscribedWeapon = true;
    }

    /// <summary>
    /// Adds what the inventory works with when missing: Equipment Visuals and a Block Controller on the player, the
    /// quickslot bar, the message line. Each one has its own option above.
    /// </summary>
    private void EnsureCompanions()
    {
        if (player != null)
        {
            _ = Equipment; // the equipment manager first: the visuals show what it equips
            GameObject host = playerStatusController != null ? playerStatusController.gameObject : player;
            if (equipmentVisuals)
            {
                visuals = host.GetComponentInChildren<EquipmentVisuals>();
                if (visuals == null)
                    visuals = host.AddComponent<EquipmentVisuals>();
                visuals.SetMainHandSocket(handParent);
            }
            if (shieldBlocking && host.GetComponentInChildren<BlockController>() == null)
                host.AddComponent<BlockController>();
        }
        if (quickSlots)
        {
            quickSlotBar = GetComponent<QuickSlotBar>();
            if (quickSlotBar == null && player != null)
                quickSlotBar = player.GetComponentInChildren<QuickSlotBar>();
            if (quickSlotBar == null)
                quickSlotBar = gameObject.AddComponent<QuickSlotBar>();
        }
    }

    /// <summary>Shows a short message above the hotbar (when Show Feedback Messages is on).</summary>
    public void ShowMessage(string message, InventoryFeedback.Kind kind = InventoryFeedback.Kind.Warning)
    {
        if (!showFeedbackMessages || string.IsNullOrWhiteSpace(message))
            return;
        if (feedback == null)
        {
            Transform container = inventoryParent != null && inventoryParent.transform.parent != null ? inventoryParent.transform.parent : transform;
            if (!(container is RectTransform))
            {
                Canvas c = GetComponentInParent<Canvas>();
                if (c == null) return;
                container = c.transform;
            }
            feedback = InventoryFeedback.Create(container);
        }
        feedback.Show(message, kind);
    }

    private void OnAttackFailed(AttackType input, string reason)
    {
        WeaponSO w = input == AttackType.OffHand && weaponController != null && weaponController.OffHandWeapon != null
            ? weaponController.OffHandWeapon : weaponController != null ? weaponController.EquippedWeapon : null;
        string ammo = w != null && w.Ranged != null && w.Ranged.ammo != null && !string.IsNullOrWhiteSpace(w.Ranged.ammo.ammoType) ? w.Ranged.ammo.ammoType : "ammo";
        switch (reason)
        {
            case "ammo": ShowMessage($"No {ammo} left."); break;
            case "empty": ShowMessage("Empty - reload."); break;
            case "reloading": ShowMessage("Reloading...", InventoryFeedback.Kind.Info); break;
            case "broken": ShowMessage(w != null ? $"{w.Name} is broken." : "The weapon is broken."); break;
            case "stamina": ShowMessage("Not enough stamina."); break;
            case "injured": ShowMessage("Too injured to attack."); break;
            case "mana": ShowMessage("Not enough mana."); break;
            case "health": ShowMessage("Not enough health to fire."); break;
            default:
                if (reason != null && reason.StartsWith("item:"))
                    ShowMessage($"No {reason.Substring(5)} left.");
                break;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (slotManager != null)
        {
            slotManager.OnSlotsChanged -= OnSlotsChanged;
            slotManager.OnLayoutChanged -= OnLayoutChanged;
        }
        if (uiLayoutManager != null)
            uiLayoutManager.OnPresetChanged -= OnLayoutPresetChanged;
        if (armorSetManager != null && subscribedSets)
        {
            armorSetManager.OnSetCompleted -= OnArmorSetCompleted;
            armorSetManager.OnSetBroken -= OnArmorSetBroken;
            armorSetManager.OnSetEffectActivated -= OnArmorSetEffectActivated;
        }
        subscribedSets = false;
        if (weaponController != null && subscribedWeapon)
        {
            weaponController.HeldItemBroke -= OnHeldItemBroke;
            weaponController.AttackFailed -= OnAttackFailed;
            if (ReferenceEquals(weaponController.AmmoSource, this))
                weaponController.AmmoSource = null;
        }
        subscribedWeapon = false;
    }

    // ------------------------------------------------------------------ change handling
    private void OnSlotsChanged()
    {
        storageSlotsDirty = true;
        equipmentSlotsDirty = true;
        if (gridActive && grid != null)
        {
            grid.SetCells(slotManager?.InventorySlots);
            gridView?.LayoutCells();
        }
        RefreshHand(true);
        NotifyInventoryChanged();
    }

    private void OnLayoutChanged(SlotLayoutCalculator.LayoutData layoutData) { }

    private void OnLayoutPresetChanged(UILayoutManager.LayoutPreset preset) { }

    /// <summary>
    /// Call after changing slots or items from code: syncs the equipment, the item in hand and the caches, and raises
    /// <see cref="InventoryChanged"/>. Every inventory operation of this class calls it already.
    /// </summary>
    public void NotifyInventoryChanged()
    {
        if (gridActive)
            RepairGrid();
        UpdateHandRules(true);
        EquipmentManager eq = Equipment;
        if (eq != null)
        {
            if (eq.IsInitialized) eq.SyncFromSlots(EquipmentSlots);
            else eq.RequestSync();
        }
        armorCache?.UpdateCache(this, true);
        RefreshHand(false);
        InventoryChanged?.Invoke();
    }

    /// <summary>
    /// Shows the item of the selected hotbar slot in the hand and equips it in the weapon controller when it is a
    /// weapon. <paramref name="forceModel"/> also rebuilds the hand model.
    /// </summary>
    private void RefreshHand(bool forceModel)
    {
        GameObject[] hotbar = slotManager?.HotbarSlots;
        if (hotbar != null)
        {
            if (forceModel) HotbarHandler.ForceRefresh(hotbar, handParent);
            else HotbarHandler.RefreshIfItemChanged(hotbar, handParent);
        }
        SyncHandWeapon();
    }

    private void SyncHandWeapon()
    {
        // Hand rules first: a two-handed weapon stows the off-hand item (its effects come off at the sync).
        if (UpdateHandRules(true))
        {
            EquipmentManager eq = Equipment;
            if (eq != null && eq.IsInitialized)
                eq.SyncFromSlots(EquipmentSlots);
        }
        InventoryItem held = SelectedHotbarItem;
        if (weaponController != null)
        {
            if (held != null && held.itemScriptableObject is WeaponSO weapon)
                weaponController.EquipWeapon(weapon, held);
            else if (weaponController.EquippedWeapon != null)
                weaponController.UnequipWeapon();
            weaponController.SetOffHand(hands.activeOffHand != null ? OffHandItem : null);
        }
        if (visuals != null)
            visuals.SetHands(held, OffHandItem, stowedOffHand != null);
    }

    /// <summary>
    /// Applies the hand rules to the selected hotbar item and the Off Hand slot's item: a two-handed main weapon (or an
    /// item that cannot be held in the off hand) STOWS the off-hand item - it stays in its slot, but its effects,
    /// defense and blocking are off until the rule no longer applies. Returns true when what is stowed changed.
    /// </summary>
    private bool UpdateHandRules(bool announce)
    {
        InventoryItem main = SelectedHotbarItem;
        InventoryItem off = OffHandItem;
        hands = HandRules.Evaluate(main != null ? main.itemScriptableObject : null, off != null ? off.itemScriptableObject : null);
        InventoryItem nowStowed = hands.offHandSuppressed ? off : null;
        if (nowStowed == stowedOffHand)
            return false;
        EquipmentManager eq = Equipment;
        if (stowedOffHand != null && eq != null)
            eq.SetSuppressed(stowedOffHand, false);
        if (nowStowed != null && eq != null)
            eq.SetSuppressed(nowStowed, true);
        stowedOffHand = nowStowed;
        if (announce && nowStowed != null && !string.IsNullOrEmpty(hands.reason))
            ShowMessage(hands.reason, InventoryFeedback.Kind.Info);
        return true;
    }

    /// <summary>The weapon in hand broke: the next one of its stack takes over, or it is destroyed when its item says so.</summary>
    private void OnHeldItemBroke(InventoryItem item)
    {
        if (item == null || item.itemScriptableObject == null)
            return;
        if (item.stackCurrent > 1)
        {
            item.RemoveFromStack(1);
            InventoryUtils.UpdatePlayerWeight(player, -item.itemScriptableObject.Weight);
            if (item.DurabilityList.Count > 0)
            {
                item.durability = item.DurabilityList[item.DurabilityList.Count - 1];
                item.DurabilityList.RemoveAt(item.DurabilityList.Count - 1);
            }
            else
            {
                item.durability = InventoryItem.StartingDurability(item.itemScriptableObject);
            }
            item.RefreshUI();
            NotifyInventoryChanged();
            return;
        }
        if (item.itemScriptableObject.ShouldBeDestroyedOn0UsesLeft)
        {
            InventorySlot slot = FindSlotHolding(item.gameObject);
            if (slot != null)
                slot.heldItem = null;
            InventoryUtils.UpdatePlayerWeight(player, -item.itemScriptableObject.Weight * item.stackCurrent);
            Destroy(item.gameObject);
            RefreshHand(true);
            NotifyInventoryChanged();
        }
    }

    // Armor set event handlers
    private void OnArmorSetCompleted(ArmorSet set, bool isComplete)
    {
        if (showArmorSetNotifications)
            Debug.Log(isComplete ? $"[Inventory] Armor set completed: {set.SetName}" : $"[Inventory] Armor set incomplete: {set.SetName}", this);
    }

    private void OnArmorSetBroken(ArmorSet armorSet)
    {
        if (showArmorSetNotifications)
            Debug.Log($"[Inventory] Armor set broken: {armorSet.SetName}", this);
    }

    private void OnArmorSetEffectActivated(ArmorSetEffect effect)
    {
        if (showArmorSetNotifications)
            Debug.Log($"[Inventory] Set bonus activated: {effect.effectName}", this);
    }

    private void UpdateArmorCache()
    {
        armorCache?.UpdateCache(this);
    }

    // Slot Count Management
    private void CheckForSlotCountChanges()
    {
        if (numberOfHotBarSlots != previousHotbarSlots)
        {
            slotManager?.UpdateHotbarSlots(numberOfHotBarSlots);
            previousHotbarSlots = numberOfHotBarSlots;
        }
        if (numberOfInventorySlots != previousInventorySlots)
        {
            if (!gridActive)
                slotManager?.UpdateInventorySlots(numberOfInventorySlots);
            previousInventorySlots = numberOfInventorySlots;
        }
        // The grid toggle and size changed in the inspector while playing: switch / re-pack safely.
        if (Application.isPlaying && slotManager != null && slotManager.InventorySlots != null)
        {
            if (useGridInventory != previousUseGrid)
            {
                if (!SetGridInventory(useGridInventory))
                    useGridInventory = previousUseGrid; // refused: back to what it is
                previousUseGrid = useGridInventory;
            }
            else if (gridActive && (gridSettings.columns != previousGridColumns || gridSettings.rows != previousGridRows))
            {
                if (!ResizeGrid(gridSettings.columns, gridSettings.rows))
                {
                    gridSettings.columns = previousGridColumns;
                    gridSettings.rows = previousGridRows;
                }
            }
            previousGridColumns = gridSettings.columns;
            previousGridRows = gridSettings.rows;
        }
    }

    // ------------------------------------------------------------------ input
    public void OnInventory(InputAction.CallbackContext value)
    {
        if (value.started) RequestToggleInventory();
    }

    public void OnArmorSetUI(InputAction.CallbackContext value)
    {
        // Once per frame: the own action and an inspector-wired PlayerInput event may both report the key.
        if (!value.started || armorSetUIManager == null || lastArmorSetToggleFrame == Time.frameCount)
            return;
        lastArmorSetToggleFrame = Time.frameCount;
        armorSetUIManager.ToggleUI();
    }

    /// <summary>
    /// The Use Item input (primary click): attacks with the weapon in hand - pressed starts it, holding charges it,
    /// releasing performs a charged attack - or uses the item in hand (potions, food) when pressed.
    /// </summary>
    public void OnUseItem(InputAction.CallbackContext value)
    {
        // The own action subscription and an inspector-wired event may both report the same press: handle it once.
        if (lastUseFrame == Time.frameCount && lastUsePhase == value.phase)
            return;
        lastUseFrame = Time.frameCount;
        lastUsePhase = value.phase;
        if (value.canceled) HandleUsePress(false);
        else if (value.started) HandleUsePress(true);
    }

    private void HandleUsePress(bool pressed)
    {
        if (!pressed)
        {
            weaponController?.ReleaseAttackInput(AttackType.Normal);
            return;
        }
        if ((dragHandler?.IsDragging ?? false) || IsInventoryOpened) return; // clicks belong to the inventory while it is open
        if (PlayerAbilityController.IsPointerCapturedFor(player)) return; // this click confirms/cancels an ability preview
        UseSelectedItem();
    }

    /// <summary>Uses the item of the selected hotbar slot (attacks when it is a weapon). Returns true if something happened.</summary>
    public bool UseSelectedItem()
    {
        var selectedSlot = GetSelectedHotbarSlot();
        var heldItem = selectedSlot != null && selectedSlot.heldItem != null ? selectedSlot.heldItem.GetComponent<InventoryItem>() : null;
        if (heldItem == null || heldItem.itemScriptableObject == null)
            return false;

        if (heldItem.itemScriptableObject is WeaponSO weapon)
        {
            if (weaponController == null)
            {
                if (!warnedNoWeaponController)
                    Debug.LogWarning($"[Inventory] '{weapon.Name}' cannot attack: the Inventory Manager has no Weapon Controller (add one to the player).", this);
                warnedNoWeaponController = true;
                return false;
            }
            if (weaponController.EquippedWeapon != weapon || weaponController.HeldItem != heldItem)
                weaponController.EquipWeapon(weapon, heldItem);
            if (heldItem.IsOnCooldown())
                return false;
            bool started = weaponController.BeginAttackInput(AttackType.Normal);
            if (started && weapon.Cooldown > 0f)
                heldItem.SetCooldown(CombatStats.ItemCooldown(this, weapon.Cooldown));
            return started;
        }

        if (!ItemUsageHandler.HandleCooldown(heldItem, this)) return false;
        bool used = ItemUsageHandler.UseHeldItem(player, playerStatusController, weaponController, heldItem);
        ItemUsageHandler.HandleItemDurabilityAndStack(player, handParent, selectedSlot, heldItem);
        if (used)
            NotifyInventoryChanged();
        return used;
    }

    private void HandleHotbarInput()
    {
        if (HotbarHandler.CheckForHotbarInput(slotManager?.HotbarSlots, handParent))
            SyncHandWeapon();
    }

    // ------------------------------------------------------------------ panels
    private void ToggleInventory()
    {
        if (uiStateManager?.IsInventoryOpened ?? false) CloseInventory();
        else OpenInventory();
    }

    /// <summary>Opens the inventory panel (and frees the cursor).</summary>
    public void OpenInventory()
    {
        if (uiStateManager == null && inventoryParent != null)
        {
            try { uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent); }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] Failed to reinitialize the UI state: {e.Message}", this);
                return;
            }
        }
        if (inventoryParent == null)
        {
            Debug.LogError("[Inventory] Cannot open the inventory: Inventory Parent is not assigned.", this);
            return;
        }

        weaponController?.CancelCharge();
        SetCursorState(CursorLockMode.None, true);
        uiStateManager.SetInventoryOpened(true);
        uiStateManager.UpdateUI();
        armorCache?.UpdateCache(this, true);
    }

    /// <summary>Closes the inventory (and the open storage).</summary>
    public void CloseInventory()
    {
        CancelDrag();
        SetCursorState(CursorLockMode.Locked, false);

        if (uiStateManager != null)
        {
            uiStateManager.SetInventoryOpened(false);
            uiStateManager.UpdateUI();
        }
        else
        {
            if (inventoryParent != null) inventoryParent.SetActive(false);
            if (equippableInventory != null) equippableInventory.SetActive(false);
        }

        HideItemInfo();
        if (hoverPanel != null) hoverPanel.Hide();
        if (splitPopup != null) splitPopup.Close();
        storageManager?.CloseCurrentStorage();
    }

    private void SetCursorState(CursorLockMode lockMode, bool visible)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = visible;
    }

    private InventorySlot GetSelectedHotbarSlot()
    {
        var hotbarSlots = slotManager?.HotbarSlots;
        if (hotbarSlots == null || hotbarSlots.Length == 0 || HotbarHandler.SelectedHotbarSlot >= hotbarSlots.Length)
            return null;
        return hotbarSlots[HotbarHandler.SelectedHotbarSlot].Live()?.GetComponent<InventorySlot>();
    }

    // ------------------------------------------------------------------ pointer (drag and drop)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (splitPopup != null && splitPopup.IsOpen)
            return; // clicks inside the popup are its own; outside ones close it
        if (dragHandler != null && dragHandler.IsDragging)
            return; // the right button rotates a dragged grid item
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            HideItemInfo();
            return;
        }
        var slot = FindSlotUnderPointer(eventData);
        InventoryItem stack = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
        if (rightClickSplits && splitPopup != null && stack != null && stack.stackCurrent > 1)
        {
            HideItemInfo();
            splitPopup.Open(stack, eventData.position, SplitStack);
            return;
        }
        if (itemInfo != null && itemInfo.gameObject.activeSelf) HideItemInfo();
        else if (!showHoverPanel || hoverPanel == null) ShowItemInfo(slot); // the hover panel already shows the item
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || dragHandler == null || dragHandler.IsDragging) return;
        if (splitPopup != null && splitPopup.IsOpen) return;
        var slot = FindSlotUnderPointer(eventData);
        if (slot != null && slot.heldItem != null)
        {
            InventoryItem item = slot.heldItem.GetComponent<InventoryItem>();
            BeginGridDrag(slot, item, eventData.position);
            dragHandler.StartDragging(slot, slot.gameObject);
            if (dragFromGrid)
                StyleDraggedItem();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (dragHandler == null || !dragHandler.IsDragging && dragHandler.DraggedObject == null) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return; // the right button rotates a dragged grid item; only the left button drops
        // Dropped on the grid (even between cells): the grid decides.
        if (gridActive && dragHandler.DraggedObject != null && gridView != null && gridView.CellAtScreen(eventData.position, out int gx, out int gy))
        {
            HandleGridDrop(gx, gy);
            dragHandler.CleanupDragging();
            EndGridDrag();
            NotifyInventoryChanged();
            return;
        }
        if (!dragHandler.ValidateDragOperation(eventData))
        {
            // Released over nothing (or with another button): the item goes back instead of being lost.
            CancelDrag();
            return;
        }
        HandleItemDrop(eventData);
        dragHandler.CleanupDragging();
        EndGridDrag();
        NotifyInventoryChanged();
    }

    /// <summary>Puts a dragged item back into its slot.</summary>
    public void CancelDrag()
    {
        if (dragHandler == null || dragHandler.DraggedObject == null)
            return;
        ReturnDragged(dragHandler.DraggedObject, dragHandler.LastItemSlotObject);
        dragHandler.CleanupDragging();
        EndGridDrag();
    }

    /// <summary>
    /// The dragged item goes back where it came from. In the grid its old place may have been taken meanwhile (a pickup):
    /// then it goes to free space, else to the hotbar, else it is dropped next to the player - never lost.
    /// </summary>
    private void ReturnDragged(GameObject dragged, GameObject lastSlotObject)
    {
        InventorySlot last = lastSlotObject != null ? lastSlotObject.GetComponent<InventorySlot>() : null;
        InventoryItem item = dragged != null ? dragged.GetComponent<InventoryItem>() : null;
        if (gridActive && grid != null && last != null && grid.IsCell(last) && item != null)
        {
            if (dragFromGrid && dragItem == item)
                item.gridRotated = dragOriginRotated;
            grid.TryGetCell(last, out int x, out int y);
            if (grid.PlaceAt(item, x, y, item.gridRotated))
                return;
            RehomeItem(item);
            return;
        }
        ItemHandler.ReturnItemToLastSlot(lastSlotObject, dragged);
    }

    [NonSerialized] private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

    /// <summary>
    /// The slot under the pointer, looking through everything there - not only the topmost object. The inventory's
    /// invisible drop zone covers the screen while the inventory is open, and in the generated UI it was drawn above
    /// the hotbar: items released on a hotbar slot were dropped into the world instead ("the item disappears").
    /// </summary>
    public InventorySlot FindSlotUnderPointer(PointerEventData eventData)
    {
        if (eventData == null)
            return null;
        InventorySlot slot = ResolveSlot(eventData.pointerCurrentRaycast.gameObject);
        if (slot != null || EventSystem.current == null)
            return slot;
        pointerHits.Clear();
        EventSystem.current.RaycastAll(eventData, pointerHits);
        foreach (RaycastResult hit in pointerHits)
        {
            slot = ResolveSlot(hit.gameObject);
            if (slot != null)
                return slot;
            if (!IsDropZone(hit.gameObject))
                return null; // something else (a popup, a panel) is in front of the slots
        }
        return null;
    }

    /// <summary>The slot under the pointer: the slot itself, a child of it, or the item it holds.</summary>
    public InventorySlot ResolveSlot(GameObject hit)
    {
        if (hit == null)
            return null;
        InventorySlot slot = hit.GetComponentInParent<InventorySlot>();
        if (slot != null)
        {
            // A grid cell covered by a bigger item belongs to that item (its top-left cell holds it).
            if (gridActive && grid != null && slot.heldItem == null && grid.IsCell(slot))
            {
                InventorySlot owner = grid.OwnerCell(slot);
                if (owner != null)
                    return owner;
            }
            return slot;
        }
        InventoryItem item = hit.GetComponentInParent<InventoryItem>();
        return item != null ? FindSlotHolding(item.gameObject) : null;
    }

    /// <summary>The slot (inventory, hotbar or equipment) holding an item object, or null.</summary>
    public InventorySlot FindSlotHolding(GameObject itemObject)
    {
        if (itemObject == null) return null;
        foreach (GameObject go in StorageSlots)
            if (go != null && go.TryGetComponent(out InventorySlot s) && s.heldItem == itemObject) return s;
        foreach (InventorySlot s in EquipmentSlots)
            if (s != null && s.heldItem == itemObject) return s;
        return null;
    }

    private void HandleItemDrop(PointerEventData eventData)
    {
        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        GameObject dragged = dragHandler.DraggedObject;
        var draggedItem = dragged != null ? dragged.GetComponent<InventoryItem>() : null;
        if (draggedItem == null || draggedItem.itemScriptableObject == null)
        {
            CancelDrag();
            return;
        }

        // Dropped on a quickslot: the quickslot remembers the item; the item itself goes back (nothing is duplicated).
        QuickSlotCell quick = quickSlotBar != null ? quickSlotBar.CellUnder(eventData) : null;
        if (quick != null)
        {
            quickSlotBar.Assign(quick.Index, draggedItem.itemScriptableObject);
            ReturnDragged(dragged, dragHandler.LastItemSlotObject);
            RefreshHand(true);
            return;
        }

        InventorySlot slot = FindSlotUnderPointer(eventData);
        if (slot != null && slot.gameObject == dragHandler.LastItemSlotObject)
        {
            ReturnDragged(dragged, dragHandler.LastItemSlotObject);
        }
        else if (slot != null && SlotTypeHelper.CanPlace(draggedItem.itemScriptableObject, slot.SlotType))
        {
            if (InventoryUtils.IsSlotEmpty(slot))
                ItemHandler.PlaceItemInSlot(slot, dragged, playerStatusController);
            else if (!SwapIntoGridOrigin(slot, draggedItem))
                ItemHandler.SwitchOrFillStack(slot, dragged, dragHandler.LastItemSlotObject, playerStatusController);
        }
        else if (slot == null && IsDropZone(clickedObject))
        {
            ItemHandler.DropItem(dragged, dragHandler.LastItemSlotObject, playerStatusController, cam, player);
        }
        else
        {
            if (slot != null)
                ShowMessage(SlotTypeHelper.WhyCannotPlace(draggedItem.itemScriptableObject, slot.SlotType));
            ReturnDragged(dragged, dragHandler.LastItemSlotObject);
        }
        RefreshHand(true);
    }

    /// <summary>
    /// The dragged item came from the grid and is dropped on an occupied single slot (hotbar, equipment): the item there
    /// must fit in the grid where the dragged one was. Handled here (true) when that is the case; false = not a grid case.
    /// </summary>
    private bool SwapIntoGridOrigin(InventorySlot target, InventoryItem dragged)
    {
        if (!gridActive || grid == null || !dragFromGrid || target == null || target.heldItem == null)
            return false;
        InventoryItem other = target.heldItem.GetComponent<InventoryItem>();
        if (other == null)
            return false;
        // Same item: stacks merge (the rest goes back to the grid origin).
        if (other.itemScriptableObject == dragged.itemScriptableObject && other.stackMax > 1 && other.stackCurrent < other.stackMax)
            return false;
        InventorySlot origin = dragHandler.LastItemSlotObject != null ? dragHandler.LastItemSlotObject.GetComponent<InventorySlot>() : null;
        if (origin == null || !grid.IsCell(origin))
            return false;
        if (!SlotTypeHelper.CanPlace(other.itemScriptableObject, SlotType.Common))
            return false;
        grid.TryGetCell(origin, out int x, out int y);
        // The other item goes where the dragged one was (upright or turned), else anywhere in the grid.
        bool placed = grid.CanPlace(GridInventory.RectAt(other, x, y, other.gridRotated)) ? MoveToCell(other, target, x, y, other.gridRotated)
            : other.itemScriptableObject.CanRotateInGrid && grid.CanPlace(GridInventory.RectAt(other, x, y, !other.gridRotated)) ? MoveToCell(other, target, x, y, !other.gridRotated)
            : grid.FindSpace(other.itemScriptableObject, out InventorySlot free, out bool rot) && grid.TryGetCell(free, out int fx, out int fy) && MoveToCell(other, target, fx, fy, rot);
        if (!placed)
        {
            ShowMessage($"No room in the grid for {other.itemScriptableObject.Name}.");
            ReturnDragged(dragged.gameObject, dragHandler.LastItemSlotObject);
            return true;
        }
        ItemHandler.PlaceItemInSlot(target, dragged.gameObject, playerStatusController);
        return true;
    }

    private bool MoveToCell(InventoryItem item, InventorySlot from, int x, int y, bool rotated)
    {
        if (from != null && from.heldItem == item.gameObject)
            from.heldItem = null;
        if (grid.PlaceAt(item, x, y, rotated))
            return true;
        if (from != null)
            from.SetHeldItem(item.gameObject);
        return false;
    }

    private static bool IsDropZone(GameObject go)
    {
        if (go == null) return false;
        return go.name == "DropItem" || go.GetComponentInParent<InventoryDropZone>() != null;
    }

    private void ShowItemInfo(InventorySlot slot)
    {
        if (slot.Live()?.heldItem != null && itemInfo != null)
            itemInfo.ShowItemInfo(slot.heldItem.GetComponent<InventoryItem>());
        else
            HideItemInfo();
    }

    // ------------------------------------------------------------------ item panels (hover, split)
    /// <summary>Creates the hover panel and the split popup next to the inventory UI when they are not assigned.</summary>
    private void EnsureItemPanels()
    {
        if (inventoryParent == null)
            return;
        Transform container = inventoryParent.transform.parent != null ? inventoryParent.transform.parent : inventoryParent.transform;
        if (hoverPanel == null && showHoverPanel)
            hoverPanel = ItemHoverPanel.Create(container);
        if (splitPopup == null && rightClickSplits)
            splitPopup = SplitStackPopup.Create(container);
        if (hoverPanel != null) hoverPanel.transform.SetAsLastSibling();
        if (splitPopup != null) splitPopup.transform.SetAsLastSibling();
    }

    private PointerEventData hoverEvent;

    private void UpdateHoverPanel()
    {
        if (hoverPanel == null)
            return;
        bool canShow = showHoverPanel && IsInventoryOpened && EventSystem.current != null && mouseAvailable()
                       && (dragHandler == null || !dragHandler.IsDragging) && (splitPopup == null || !splitPopup.IsOpen);
        if (!canShow)
        {
            hoverPanel.Hide();
            return;
        }
        if (hoverEvent == null || hoverEvent.currentInputModule == null)
            hoverEvent = new PointerEventData(EventSystem.current);
        hoverEvent.position = Mouse.current.position.ReadValue();
        InventorySlot slot = FindSlotUnderPointer(hoverEvent);
        InventoryItem item = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
        if (item != null)
            hoverPanel.Show(item, inventoryParent != null ? (RectTransform)inventoryParent.transform : null);
        else
            hoverPanel.Hide();

        static bool mouseAvailable() => Mouse.current != null;
    }

    /// <summary>Takes <paramref name="amount"/> out of <paramref name="stack"/> into a free slot. False when there is none.</summary>
    public bool SplitStack(InventoryItem stack, int amount)
    {
        if (stack == null || amount < 1 || amount >= stack.stackCurrent)
            return false;
        bool done = SplitItemHandler.SplitItemIntoNewStack(this, stack, StorageSlots, player, amount);
        if (done)
            NotifyInventoryChanged();
        return done;
    }

    private void HideItemInfo()
    {
        if (itemInfo != null && itemInfo.gameObject != null) itemInfo.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ adding items
    /// <summary>Picks up a world item (with an ItemPickable). Returns true if at least part of it was taken.</summary>
    public bool ItemPicked(GameObject pickedItem)
    {
        return ItemPickUpHandler.AddItemToInventory(this, pickedItem, StorageSlots, itemPrefab, player);
    }

    public void InstantiateClassItems(List<GameObject> classItems)
    {
        foreach (var item in classItems)
            if (item != null)
                ItemPicked(item);
    }

    /// <summary>
    /// Adds items from code (rewards, crafting, loot): fills existing stacks first (inventory, then hotbar), then
    /// empty inventory slots, then empty hotbar slots. <paramref name="durabilities"/> gives one durability per unit
    /// (the last one is used first). Returns how many could NOT be added (0 = all).
    /// </summary>
    public int AddItem(ItemSO item, int quantity = 1, IList<int> durabilities = null)
    {
        if (item == null || quantity <= 0)
            return Mathf.Max(0, quantity);
        if (itemPrefab == null)
        {
            Debug.LogError("[Inventory] Cannot add items: Item Prefab is not assigned.", this);
            return quantity;
        }

        var dur = durabilities != null ? new List<int>(durabilities) : new List<int>();
        int remaining = quantity;
        int stackMax = Mathf.Max(1, item.StackMax);

        // 1) existing stacks
        if (stackMax > 1)
        {
            foreach (GameObject go in StorageSlots)
            {
                if (remaining <= 0) break;
                if (go == null || !go.TryGetComponent(out InventorySlot slot) || slot.SlotType != SlotType.Common || slot.heldItem == null) continue;
                InventoryItem existing = slot.heldItem.GetComponent<InventoryItem>();
                if (existing == null || existing.itemScriptableObject != item) continue;
                int space = existing.GetAvailableStackSpace();
                if (space <= 0) continue;
                int n = Mathf.Min(space, remaining);
                InventoryUtils.TransferDurabilityList(dur, existing.DurabilityList, Mathf.Min(n, dur.Count));
                existing.AddToStack(n);
                remaining -= n;
            }
        }

        // 2) empty space: the grid (where the item's cells fit, turned when that helps) or empty slots; then the hotbar
        if (gridActive && grid != null)
        {
            while (remaining > 0 && grid.FindSpace(item, out InventorySlot anchor, out bool rotated))
            {
                int n = Mathf.Min(stackMax, remaining);
                if (CreateStack(anchor, item, n, dur, rotated) == null)
                    break;
                remaining -= n;
            }
        }
        foreach (GameObject go in gridActive ? (HotbarSlots ?? Array.Empty<GameObject>()) : StorageSlots)
        {
            if (remaining <= 0) break;
            if (go == null || !go.TryGetComponent(out InventorySlot slot) || slot.SlotType != SlotType.Common || slot.heldItem != null) continue;
            int n = Mathf.Min(stackMax, remaining);
            if (CreateStack(slot, item, n, dur, false) == null)
                break;
            remaining -= n;
        }

        int added = quantity - remaining;
        if (added > 0)
        {
            InventoryUtils.UpdatePlayerWeight(player, item.Weight * added);
            NotifyInventoryChanged();
        }
        return remaining;
    }

    /// <summary>A new stack in <paramref name="slot"/> (a grid cell: at that place, turned when <paramref name="rotated"/>). Durabilities are taken from <paramref name="durabilityPool"/>.</summary>
    private InventoryItem CreateStack(InventorySlot slot, ItemSO item, int n, List<int> durabilityPool, bool rotated)
    {
        var chunk = new List<int>();
        if (durabilityPool != null)
            InventoryUtils.TransferDurabilityList(durabilityPool, chunk, Mathf.Min(n, durabilityPool.Count));
        GameObject obj = Instantiate(itemPrefab);
        InventoryItem created = obj.GetComponent<InventoryItem>();
        if (created == null)
        {
            Debug.LogError("[Inventory] The Item Prefab has no InventoryItem component.", itemPrefab);
            Destroy(obj);
            return null;
        }
        created.Initialize(item, n, chunk);
        if (gridActive && grid != null && grid.IsCell(slot))
        {
            grid.TryGetCell(slot, out int x, out int y);
            if (!grid.PlaceAt(created, x, y, rotated))
            {
                Destroy(obj); // checked before; never place over another item
                return null;
            }
        }
        else
        {
            slot.SetHeldItem(obj);
        }
        return created;
    }

    /// <summary>
    /// Creates an inventory item in an empty slot (split stacks, scripts). <paramref name="addWeight"/>: the player's
    /// carried weight grows by the item's weight. Returns the item, or null when the slot is not usable. In the grid the
    /// item must fit at that cell (turned if needed).
    /// </summary>
    public InventoryItem CreateItemInSlot(InventorySlot slot, ItemSO item, int quantity, IList<int> durabilities = null, bool addWeight = true)
    {
        if (slot == null || item == null || itemPrefab == null || slot.heldItem != null || !SlotTypeHelper.CanPlace(item, slot.SlotType))
            return null;
        bool rotated = false;
        if (gridActive && grid != null && grid.IsCell(slot))
        {
            grid.TryGetCell(slot, out int x, out int y);
            if (grid.CanPlace(GridInventory.RectAt(item, x, y, false)))
                rotated = false;
            else if (item.CanRotateInGrid && grid.CanPlace(GridInventory.RectAt(item, x, y, true)))
                rotated = true;
            else
                return null;
        }
        InventoryItem created = CreateStack(slot, item, Mathf.Max(1, quantity), durabilities != null ? new List<int>(durabilities) : null, rotated);
        if (created == null)
            return null;
        if (addWeight)
            InventoryUtils.UpdatePlayerWeight(player, item.Weight * created.stackCurrent);
        NotifyInventoryChanged();
        return created;
    }

    /// <summary>Old API: creates the inventory item of a picked world item in an empty slot.</summary>
    public void InstantiateNewItem(GameObject emptySlot, GameObject pickedItem)
    {
        var pickable = pickedItem != null ? pickedItem.GetComponent<ItemPickable>() : null;
        var slot = emptySlot != null ? emptySlot.GetComponent<InventorySlot>() : null;
        if (pickable == null || pickable.itemScriptableObject == null || slot == null || itemPrefab == null)
            return;
        if (gridActive && grid != null && grid.IsCell(slot))
        {
            ItemPicked(pickedItem); // the grid decides where it fits
            return;
        }
        GameObject newItem = Instantiate(itemPrefab);
        var component = newItem.GetComponent<InventoryItem>();
        component.Initialize(pickable);
        slot.SetHeldItem(newItem);
        InventoryUtils.UpdatePlayerWeight(player, component.totalWeight);
        NotifyInventoryChanged();
    }

    // ------------------------------------------------------------------ storage
    public void OpenStorage(Storage storage)
    {
        SetCursorState(CursorLockMode.None, true);
        uiStateManager?.SetStorageOpened(true);
        storageManager?.OpenStorage(storage);
    }

    public void CloseStorage(Storage storage)
    {
        storageManager?.CloseStorage(storage);
        SetCursorState(CursorLockMode.Locked, false);
        uiStateManager?.SetStorageOpened(false);
        NotifyInventoryChanged();
    }

    // ------------------------------------------------------------------ queries (inventory + hotbar)
    public int GetItemCount(ItemSO itemSO) => InventoryUtils.GetItemCount(StorageSlots, itemSO);

    public bool HasEnoughSpace(ItemSO itemSO, int requiredQuantity)
    {
        if (!gridActive || grid == null)
            return InventoryUtils.HasEnoughSpace(StorageSlots, itemSO, requiredQuantity);
        int freeHotbar = 0;
        if (HotbarSlots != null)
            foreach (GameObject go in HotbarSlots)
                if (go != null && go.TryGetComponent(out InventorySlot hs) && hs.heldItem == null) freeHotbar++;
        return grid.CountFit(itemSO, requiredQuantity, StorageSlots, freeHotbar) >= requiredQuantity;
    }

    public bool HasEnoughItems(ItemSO itemSO, int requiredAmount) => InventoryUtils.HasEnoughItems(StorageSlots, itemSO, requiredAmount);

    /// <summary>Removes items (inventory and hotbar); the player's carried weight follows. Returns how many were removed.</summary>
    public int RemoveItems(ItemSO itemSO, int amountToRemove)
    {
        int removed = InventoryUtils.RemoveItems(StorageSlots, itemSO, amountToRemove);
        if (removed > 0)
        {
            InventoryUtils.UpdatePlayerWeight(player, -itemSO.Weight * removed);
            RefreshHand(true);
            NotifyInventoryChanged();
        }
        return removed;
    }

    public float GetTotalInventoryWeight() => InventoryUtils.CalculateInventoryWeight(StorageSlots);

    public float GetTotalArmorWeight() => InventoryUtils.CalculateArmorWeight(StorageSlots);

    public float GetTotalDefense() => armorCache?.GetTotalDefense() ?? ArmorSetUtils.CalculateTotalDefense(this);

    public float GetTotalMagicDefense() => armorCache?.GetTotalMagicDefense() ?? ArmorSetUtils.CalculateTotalMagicDefense(this);

    public List<ArmorSO> GetEquippedArmor() => ArmorSetUtils.GetEquippedArmor(this);

    public Dictionary<ArmorSet, List<ArmorSO>> GetEquippedArmorSets() => ArmorSetUtils.GetEquippedArmorSets(this);

    public List<ArmorSet> GetCompleteSets() => ArmorSetUtils.GetCompleteSets(this);

    public bool IsArmorEquipped(ArmorSO armor) => ArmorSetUtils.IsArmorEquipped(this, armor);

    public ArmorSO GetEquippedArmorForSlot(ArmorSlotType slotType) => ArmorSetUtils.GetEquippedArmorForSlot(this, slotType);

    // Armor management methods
    public bool QuickEquipArmor(ArmorSO armor)
    {
        bool ok = ArmorEquipmentHandler.QuickEquipArmor(this, armor, playerStatusController);
        if (ok) NotifyInventoryChanged();
        return ok;
    }

    public void UnequipAllArmor()
    {
        ArmorEquipmentHandler.UnequipAllArmor(this, playerStatusController);
        NotifyInventoryChanged();
    }

    public void OptimizeArmorSets()
    {
        if (!enableArmorOptimization) return;
        ArmorEquipmentHandler.OptimizeArmorSets(this, playerStatusController);
        NotifyInventoryChanged();
    }

    public string GetArmorSummary() => ArmorSetUtils.CreateArmorSummary(this);

    public string GetEquipmentReport() => ArmorEquipmentHandler.GetEquipmentReport(this);

    // ------------------------------------------------------------------ debug
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogInventoryState()
    {
        InventoryUtils.LogInventoryState(StorageSlots, $"Hotbar: {numberOfHotBarSlots}, Inventory: {numberOfInventorySlots}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogLayoutInfo()
    {
        uiLayoutManager?.LogLayoutInfo();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogArmorSetStatus()
    {
        if (armorSetManager != null) Debug.Log(armorSetManager.GetSetStatusReport(), this);
        else Debug.LogWarning("[Inventory] ArmorSetManager not found!", this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogArmorSummary()
    {
        Debug.Log(ArmorSetUtils.CreateArmorSummary(this), this);
    }

    [ContextMenu("Log Full Inventory State")]
    private void DebugLogFullState()
    {
        LogInventoryState();
        LogArmorSummary();
        LogArmorSetStatus();
        if (Equipment != null) Debug.Log(Equipment.Describe(), this);
    }

    [ContextMenu("Optimize Armor Sets")]
    private void DebugOptimizeArmor() => OptimizeArmorSets();

    [ContextMenu("Force Equipment Sync")]
    private void DebugForceArmorScan() => NotifyInventoryChanged();

    [ContextMenu("Validate Armor Equipment")]
    private void DebugValidateArmorEquipment()
    {
        bool isValid = ArmorEquipmentHandler.ValidateArmorEquipment(this);
        Debug.Log($"[Inventory] Armor equipment validation: {(isValid ? "PASSED" : "FAILED")}", this);
    }

    // ------------------------------------------------------------------ grid inventory
    /// <summary>Turns the grid on for the current inventory slots (they become its cells) and lays it out.</summary>
    private void ActivateGrid()
    {
        GameObject[] cells = slotManager?.InventorySlots;
        if (cells == null || slotManager.InventorySlotsParent == null)
        {
            Debug.LogWarning("[Inventory] Use Grid Inventory is on but the inventory slots are missing (build the inventory UI): the classic slots are used.", this);
            return;
        }
        if (cells.Length < gridSettings.CellCount)
            slotManager.UpdateInventorySlots(gridSettings.CellCount);
        grid = new GridInventory(gridSettings, slotManager.InventorySlots);
        var area = slotManager.InventorySlotsParent as RectTransform;
        if (area == null)
        {
            Debug.LogWarning("[Inventory] The inventory slots' parent is not a UI object: the grid cannot be drawn.", this);
            grid = null;
            return;
        }
        gridView = GridInventoryView.Attach(area, this, grid);
        gridActive = true;
        if (rotateInput == null)
            rotateInput = CombatInputBinding.Create(player != null ? player.transform : transform, null, gridSettings.rotateActionNames,
                gridSettings.rotateFallbackKey, MouseFallback.None, "Rotate item", fallbackMayShare: true); // only read while the inventory is open
        RefreshGridHint();
        InputDeviceTracker.Changed -= RefreshGridHint;
        InputDeviceTracker.Changed += RefreshGridHint;
        RepairGrid();
    }

    private void RefreshGridHint()
    {
        if (gridView != null)
            gridView.SetHint(gridSettings.showRotateHint ? ControlsHint() : "");
    }

    /// <summary>"Drag to move · R or right click while dragging: rotate · R over an item: rotate it" (keys of the device in use).</summary>
    private string ControlsHint()
    {
        bool pad = InputDeviceTracker.Current == InputDeviceTracker.Kind.Gamepad;
        string key = "";
        if (rotateInput != null && rotateInput.HasAction)
            key = InputDeviceTracker.DisplayString(rotateInput.Action);
        else if (!pad && rotateInput != null && rotateInput.HasAny)
            key = gridSettings.rotateFallbackKey.ToString();
        bool rightClick = !pad && gridSettings.rightClickRotatesWhileDragging;
        var parts = new List<string> { "Drag to move" };
        if (!string.IsNullOrEmpty(key))
            parts.Add($"{key}{(rightClick ? " or right click" : "")} while dragging: rotate");
        else if (rightClick)
            parts.Add("Right click while dragging: rotate");
        if (!string.IsNullOrEmpty(key))
            parts.Add($"{key} over an item: rotate it");
        return string.Join("  ·  ", parts);
    }

    /// <summary>
    /// Switches between the classic slots and the grid at runtime. The items in the inventory (not the hotbar) are taken
    /// out, the slots are rebuilt, and the items are put back - in the grid biggest first, turned when that helps. With
    /// Overflow = Refuse nothing changes when something would not fit; with Hotbar Then Drop the rest goes to free hotbar
    /// slots, then is dropped next to the player (logged). Returns false when the switch was refused.
    /// </summary>
    public bool SetGridInventory(bool enable)
    {
        if (enable == gridActive && enable == useGridInventory)
            return true;
        if (slotManager == null || slotManager.InventorySlots == null)
        {
            useGridInventory = enable;
            return true; // not started yet: the slots are built for the mode at Start
        }
        return Repack(enable, gridSettings.columns, gridSettings.rows);
    }

    /// <summary>Changes the grid's columns and rows at runtime (items are re-packed; refused when they would not fit).</summary>
    public bool ResizeGrid(int columns, int rows)
    {
        if (!gridActive)
        {
            gridSettings.columns = columns;
            gridSettings.rows = rows;
            return true;
        }
        return Repack(true, columns, rows);
    }

    private bool Repack(bool toGrid, int columns, int rows)
    {
        CancelDrag();
        storageManager?.CloseCurrentStorage();

        // The items of the bag (the hotbar is not part of the grid).
        var items = new List<InventoryItem>();
        foreach (GameObject go in slotManager.InventorySlots)
        {
            InventorySlot s = go != null ? go.GetComponent<InventorySlot>() : null;
            InventoryItem it = s != null && s.heldItem != null ? s.heldItem.GetComponent<InventoryItem>() : null;
            if (it != null) items.Add(it);
        }

        // Plan first: nothing moves until it is known what fits.
        var plan = new Dictionary<InventoryItem, GridRect>(ReferenceComparer<InventoryItem>.Instance);
        List<InventoryItem> overflow;
        int cellCount = Mathf.Clamp(columns, 2, 30) * Mathf.Clamp(rows, 2, 30);
        if (toGrid)
            overflow = GridInventory.PlanPacking(Mathf.Clamp(columns, 2, 30), Mathf.Clamp(rows, 2, 30), items, true, plan);
        else
            overflow = items.Count > numberOfInventorySlots ? items.GetRange(numberOfInventorySlots, items.Count - numberOfInventorySlots) : new List<InventoryItem>();
        if (overflow.Count > 0 && gridSettings.overflow == GridOverflowPolicy.Refuse)
        {
            string names = string.Join(", ", overflow.ConvertAll(i => i.itemScriptableObject != null ? i.itemScriptableObject.Name : "?"));
            Debug.LogWarning($"[Inventory] Cannot switch to {(toGrid ? $"a {columns}×{rows} grid" : "slots")}: {overflow.Count} item(s) would not fit ({names}). " +
                             "Make room, or set Grid Inventory ▸ Overflow to Hotbar Then Drop.", this);
            ShowMessage($"Not enough room to switch the inventory ({overflow.Count} item(s) would not fit).");
            return false;
        }

        // Take the items out (kept, inactive) and rebuild the slots for the new mode.
        var holder = new GameObject("Inventory Repack (temporary)").transform;
        holder.SetParent(transform, false);
        foreach (InventoryItem it in items)
        {
            InventorySlot s = FindSlotHolding(it.gameObject);
            if (s != null) s.heldItem = null;
            it.transform.SetParent(holder, false);
            it.gameObject.SetActive(false);
        }

        if (gridActive && !toGrid)
        {
            gridView?.Deactivate();
            gridActive = false;
            grid = null;
        }
        gridSettings.columns = Mathf.Clamp(columns, 2, 30);
        gridSettings.rows = Mathf.Clamp(rows, 2, 30);
        useGridInventory = toGrid;
        previousUseGrid = toGrid;
        slotManager.UpdateInventorySlots(toGrid ? cellCount : numberOfInventorySlots);
        storageSlotsDirty = true;
        if (toGrid)
        {
            gridActive = false;
            ActivateGrid();
        }

        // Put the items back.
        foreach (InventoryItem it in items)
        {
            it.gameObject.SetActive(true);
            bool placed = false;
            if (toGrid && grid != null && plan.TryGetValue(it, out GridRect r))
            {
                Vector2Int size = it.itemScriptableObject.GridSize;
                bool rotated = size.x != size.y && r.W == size.y;
                placed = grid.PlaceAt(it, r.X, r.Y, rotated);
            }
            else if (!toGrid)
            {
                it.gridRotated = false;
                foreach (GameObject go in slotManager.InventorySlots)
                    if (go != null && go.TryGetComponent(out InventorySlot s) && s.heldItem == null)
                    {
                        s.SetHeldItem(it.gameObject);
                        placed = true;
                        break;
                    }
            }
            if (!placed)
                RehomeItem(it);
        }
        Destroy(holder.gameObject);
        Debug.Log($"[Inventory] Switched to {(toGrid ? $"the grid ({gridSettings.columns}×{gridSettings.rows})" : "slots")}.", this);
        RefreshHand(true);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// An item that lost its place (a re-pack, a conflict): free grid space, else a free slot of the bag, else a free
    /// hotbar slot, else dropped next to the player (logged) - never lost or duplicated.
    /// </summary>
    private void RehomeItem(InventoryItem it)
    {
        if (it == null)
            return;
        if (gridActive && grid != null && grid.FindSpace(it.itemScriptableObject, out InventorySlot anchor, out bool rot) && grid.TryGetCell(anchor, out int x, out int y)
            && grid.PlaceAt(it, x, y, rot))
            return;
        if (!gridActive && slotManager?.InventorySlots != null)
            foreach (GameObject go in slotManager.InventorySlots)
                if (go != null && go.TryGetComponent(out InventorySlot s) && s.heldItem == null)
                {
                    s.SetHeldItem(it.gameObject);
                    return;
                }
        if (HotbarSlots != null)
            foreach (GameObject go in HotbarSlots)
                if (go != null && go.TryGetComponent(out InventorySlot hs) && hs.heldItem == null)
                {
                    hs.SetHeldItem(it.gameObject);
                    return;
                }
        if (it.itemScriptableObject != null && it.itemScriptableObject.Prefab != null && player != null)
        {
            Vector3 at = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.5f;
            ItemHandler.SpawnWorldItem(it.itemScriptableObject, it.stackCurrent, it.DurabilityList, at, player);
            InventoryUtils.UpdatePlayerWeight(player, -it.itemScriptableObject.Weight * it.stackCurrent);
            Debug.LogWarning($"[Inventory] No room for {it.itemScriptableObject.Name} ×{it.stackCurrent}: dropped next to the player.", this);
            ShowMessage($"No room: {it.itemScriptableObject.Name} was dropped.");
        }
        else
        {
            Debug.LogError($"[Inventory] No room for {(it.itemScriptableObject != null ? it.itemScriptableObject.Name : "an item")} and it cannot be dropped (no prefab): it was kept in a hidden holder.", this);
            it.gameObject.SetActive(false);
            return;
        }
        Destroy(it.gameObject);
    }

    /// <summary>Items that ended up overlapping in the grid (placed by older code into covered cells) are moved to free space.</summary>
    private void RepairGrid()
    {
        if (!gridActive || grid == null)
            return;
        grid.Rebuild();
        if (grid.Conflicts.Count == 0)
            return;
        foreach (InventoryItem it in grid.TakeConflicts())
            RehomeItem(it);
    }

    // ---- dragging in the grid
    private void BeginGridDrag(InventorySlot slot, InventoryItem item, Vector2 pointer)
    {
        dragItem = item;
        dragFromGrid = false;
        dragStyled = false;
        dragRotated = item != null && item.gridRotated;
        dragOriginRotated = dragRotated;
        dragGrab = Vector2Int.zero;
        if (!gridActive || grid == null || item == null || !grid.IsCell(slot))
            return;
        if (!grid.TryGetRect(item, out dragOrigin))
            return;
        dragFromGrid = true;
        dragOriginRotated = item.gridRotated;
        dragRotated = item.gridRotated;
        dragGrab = Vector2Int.zero;
        if (gridView.CellAtScreen(pointer, out int cx, out int cy))
            dragGrab = new Vector2Int(Mathf.Clamp(cx - dragOrigin.X, 0, dragOrigin.W - 1), Mathf.Clamp(cy - dragOrigin.Y, 0, dragOrigin.H - 1));
        gridView.ShowOrigin(dragOrigin);
        dragStyled = true;
    }

    private void EndGridDrag()
    {
        // An item that did not land in the grid gets its own look back (it may have been turned while dragged).
        if (dragItem != null && gridView != null && grid != null && !grid.Model.Contains(dragItem))
            gridView.Restore(dragItem);
        dragItem = null;
        dragFromGrid = false;
        dragStyled = false;
        if (gridView != null)
        {
            gridView.HidePreview();
            gridView.HideOrigin();
        }
    }

    /// <summary>The dragged item's on-screen size, turn and the point it is held by (the grabbed cell stays under the pointer).</summary>
    private void StyleDraggedItem()
    {
        if (gridView == null || dragHandler?.DraggedObject == null || dragItem == null)
            return;
        Vector2Int size = GridInventory.SizeOf(dragItem.itemScriptableObject, dragRotated);
        var rt = (RectTransform)dragItem.transform;
        Vector2 block = gridView.BlockSize(size.x, size.y) * (gridView.Area != null ? gridView.Area.lossyScale.x / Mathf.Max(0.0001f, rt.parent != null ? rt.parent.lossyScale.x : 1f) : 1f);
        rt.sizeDelta = block;
        rt.pivot = new Vector2((dragGrab.x + 0.5f) / size.x, 1f - (dragGrab.y + 0.5f) / size.y);
        gridView.Style(dragItem, dragRotated, block);
    }

    /// <summary>Per frame: placement preview while dragging, rotation input, the hovered item.</summary>
    private void UpdateGridInteraction()
    {
        if (!gridActive || gridView == null || grid == null)
            return;
        bool rotatePressed = rotateInput != null && rotateInput.Pressed && IsInventoryOpened;
        Mouse m = Mouse.current;
        Vector2 pointer = m != null ? m.position.ReadValue() : Vector2.zero;

        if (dragItem != null && dragHandler != null && dragHandler.IsDragging)
        {
            if (rotatePressed || (gridSettings.rightClickRotatesWhileDragging && m != null && m.rightButton.wasPressedThisFrame))
                RotateDragged();
            bool over = gridView.CellAtScreen(pointer, out int cx, out int cy);
            if (!over)
            {
                gridView.HidePreview();
                return;
            }
            if (!dragStyled)
            {
                // An item dragged in from the hotbar / equipment / storage shows its grid size over the grid.
                dragGrab = Vector2Int.zero;
                Vector2Int sz = GridInventory.SizeOf(dragItem.itemScriptableObject, dragRotated);
                dragGrab = new Vector2Int(sz.x / 2, sz.y / 2);
                StyleDraggedItem();
                dragStyled = true;
            }
            GridRect target = DragTarget(cx, cy);
            gridView.ShowPreview(target, PreviewColor(target));
            return;
        }

        // Hover: highlight the item under the pointer; R turns it in place.
        if (!IsInventoryOpened || m == null || !gridView.CellAtScreen(pointer, out int hx, out int hy))
        {
            gridView.HideHover();
            return;
        }
        InventoryItem hovered = grid.Model.At(hx, hy);
        if (hovered != null && grid.TryGetRect(hovered, out GridRect hr))
        {
            gridView.ShowHover(hr);
            if (rotatePressed)
                RotateInPlace(hovered);
        }
        else
        {
            gridView.HideHover();
        }
    }

    private void RotateDragged()
    {
        ItemSO so = dragItem.itemScriptableObject;
        if (so == null || !so.CanRotateInGrid || so.GridSize.x == so.GridSize.y)
        {
            ShowMessage(so != null && !so.CanRotateInGrid ? $"{so.Name} cannot be rotated." : "", InventoryFeedback.Kind.Info);
            return;
        }
        dragRotated = !dragRotated;
        dragGrab = new Vector2Int(dragGrab.y, dragGrab.x); // the held cell turns with the item
        Vector2Int size = GridInventory.SizeOf(so, dragRotated);
        dragGrab = new Vector2Int(Mathf.Clamp(dragGrab.x, 0, size.x - 1), Mathf.Clamp(dragGrab.y, 0, size.y - 1));
        StyleDraggedItem();
    }

    private void RotateInPlace(InventoryItem item)
    {
        ItemSO so = item.itemScriptableObject;
        if (so == null)
            return;
        if (!so.CanRotateInGrid || so.GridSize.x == so.GridSize.y)
        {
            if (!so.CanRotateInGrid) ShowMessage($"{so.Name} cannot be rotated.", InventoryFeedback.Kind.Info);
            return;
        }
        if (grid.TryRotateInPlace(item, out GridRect wanted))
        {
            gridView.LayoutItems();
            NotifyInventoryChanged();
        }
        else
        {
            gridView.FlashInvalid(wanted);
            ShowMessage($"No room to rotate {so.Name} here.");
        }
    }

    /// <summary>Where the dragged item would land with the pointer over cell (cx, cy).</summary>
    private GridRect DragTarget(int cx, int cy)
    {
        Vector2Int size = GridInventory.SizeOf(dragItem.itemScriptableObject, dragRotated);
        Vector2Int grab = new Vector2Int(Mathf.Clamp(dragGrab.x, 0, size.x - 1), Mathf.Clamp(dragGrab.y, 0, size.y - 1));
        return new GridRect(cx - grab.x, cy - grab.y, size.x, size.y);
    }

    private enum DropKind { Place, Merge, Swap, Invalid }

    /// <summary>What dropping the dragged item on <paramref name="target"/> would do (and the item it would stack on / swap with).</summary>
    private DropKind EvaluateDrop(GridRect target, out InventoryItem other)
    {
        other = null;
        GridInventoryModel<InventoryItem> m = grid.Model;
        if (!m.InBounds(target))
            return DropKind.Invalid;
        if (m.CanPlace(target, dragItem))
            return DropKind.Place;
        m.Occupants(target, occupantBuffer, dragItem);
        if (occupantBuffer.Count != 1)
            return DropKind.Invalid;
        other = occupantBuffer[0];
        if (other.itemScriptableObject == dragItem.itemScriptableObject && other.stackMax > 1 && other.stackCurrent < other.stackMax)
            return DropKind.Merge;
        if (!m.CanPlace(target, other))
            return DropKind.Invalid; // other items are in the way too
        return CanSendOtherToOrigin(other, target, out _, out _, out _) ? DropKind.Swap : DropKind.Invalid;
    }

    /// <summary>
    /// For a swap: where the item under the dragged one goes - where the dragged one came from (its grid place, upright or
    /// turned; or its hotbar / equipment slot when that slot accepts it: x = y = -1).
    /// </summary>
    private bool CanSendOtherToOrigin(InventoryItem other, GridRect target, out int x, out int y, out bool rotated)
    {
        x = y = 0;
        rotated = other.gridRotated;
        InventorySlot origin = dragHandler.LastItemSlotObject != null ? dragHandler.LastItemSlotObject.GetComponent<InventorySlot>() : null;
        GridInventoryModel<InventoryItem> m = grid.Model;
        if (origin != null && !grid.IsCell(origin))
        {
            // Dragged in from a single slot: the other item takes that slot if it is allowed there.
            if (origin.heldItem != null || !SlotTypeHelper.CanPlace(other.itemScriptableObject, origin.SlotType))
                return false;
            x = y = -1;
            return true;
        }
        if (!dragFromGrid)
            return false;
        var ignore = new[] { other, dragItem };
        // Upright / turned at the dragged item's old place, not overlapping where the dragged one lands.
        foreach (bool rot in new[] { other.gridRotated, !other.gridRotated })
        {
            if (rot != other.gridRotated && !other.itemScriptableObject.CanRotateInGrid)
                continue;
            GridRect r = GridInventory.RectAt(other, dragOrigin.X, dragOrigin.Y, rot);
            if (!r.Overlaps(target) && m.CanPlace(r, ignore))
            {
                x = r.X; y = r.Y; rotated = rot;
                return true;
            }
        }
        return false;
    }

    private Color PreviewColor(GridRect target)
    {
        switch (EvaluateDrop(target, out _))
        {
            case DropKind.Place: return gridSettings.validColor;
            case DropKind.Merge:
            case DropKind.Swap: return gridSettings.mergeColor;
            default: return gridSettings.invalidColor;
        }
    }

    /// <summary>The dragged item was released over the grid, the pointer on cell (cx, cy).</summary>
    private void HandleGridDrop(int cx, int cy)
    {
        GameObject dragged = dragHandler.DraggedObject;
        InventoryItem item = dragged != null ? dragged.GetComponent<InventoryItem>() : null;
        if (item == null || item.itemScriptableObject == null)
        {
            CancelDrag();
            return;
        }
        if (dragItem != item)
        {
            // Dragged in from the hotbar / equipment / storage: held by its centre, keeping its last turn.
            dragItem = item;
            dragRotated = item.gridRotated;
            dragFromGrid = false;
        }
        GridRect target = DragTarget(cx, cy);
        GameObject last = dragHandler.LastItemSlotObject;
        InventorySlot lastSlot = last != null ? last.GetComponent<InventorySlot>() : null;
        switch (EvaluateDrop(target, out InventoryItem other))
        {
            case DropKind.Place:
                if (grid.PlaceAt(item, target.X, target.Y, dragRotated))
                {
                    ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
                    break;
                }
                goto default;
            case DropKind.Merge:
                // Stacks onto the item there; what does not fit goes back where it came from.
                InventorySlot otherCell = grid.AnchorOf(other);
                if (lastSlot != null)
                    item.gridRotated = dragOriginRotated;
                StackOperations.FillStack(otherCell, other, item, last);
                break;
            case DropKind.Swap:
                if (CanSendOtherToOrigin(other, target, out int ox, out int oy, out bool orot))
                {
                    InventorySlot oc = grid.AnchorOf(other);
                    if (oc != null) oc.heldItem = null;
                    if (ox < 0)
                    {
                        // The other item takes the single slot the dragged one came from.
                        if (grid.PlaceAt(item, target.X, target.Y, dragRotated))
                        {
                            lastSlot.SetHeldItem(other.gameObject);
                            ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
                            break;
                        }
                        if (oc != null) oc.SetHeldItem(other.gameObject);
                        goto default;
                    }
                    if (grid.PlaceAt(item, target.X, target.Y, dragRotated))
                    {
                        if (!grid.PlaceAt(other, ox, oy, orot))
                            RehomeItem(other); // should not happen (checked above): it goes wherever it fits, never lost
                        break;
                    }
                    if (oc != null) oc.SetHeldItem(other.gameObject);
                }
                goto default;
            default:
                gridView.FlashInvalid(target);
                ShowMessage(grid.Model.InBounds(target) ? $"{item.itemScriptableObject.Name} does not fit there." : "It does not fit in the grid there.");
                ReturnDragged(dragged, last);
                break;
        }
        RefreshHand(true);
    }

    // ------------------------------------------------------------------ queries and use (quickslots, ammo)
    /// <summary>How many of <paramref name="item"/> the bag (and the hotbar) holds.</summary>
    public int CountItem(ItemSO item, bool includeHotbar = true)
    {
        if (item == null)
            return 0;
        int n = InventoryUtils.GetItemCount(slotManager?.InventorySlots ?? Array.Empty<GameObject>(), item);
        if (includeHotbar && HotbarSlots != null)
            n += InventoryUtils.GetItemCount(HotbarSlots, item);
        return n;
    }

    /// <summary>The stacks of <paramref name="item"/> in the bag (then the hotbar), in slot order.</summary>
    public List<InventoryItem> FindStacks(ItemSO item, bool includeHotbar = true, List<InventoryItem> into = null)
    {
        into = into ?? new List<InventoryItem>();
        into.Clear();
        if (item == null)
            return into;
        Collect(slotManager?.InventorySlots);
        if (includeHotbar)
            Collect(HotbarSlots);
        return into;

        void Collect(GameObject[] slots)
        {
            if (slots == null) return;
            foreach (GameObject go in slots)
            {
                InventorySlot s = go != null ? go.GetComponent<InventorySlot>() : null;
                InventoryItem it = s != null && s.heldItem != null ? s.heldItem.GetComponent<InventoryItem>() : null;
                if (it != null && it.itemScriptableObject == item && it.stackCurrent > 0)
                    into.Add(it);
            }
        }
    }

    /// <summary>
    /// Uses one <paramref name="item"/> from the inventory without holding it (quickslots): a consumable is applied and one
    /// use is spent from the first ready stack; a throwable is thrown. False with a reason when it cannot be used.
    /// </summary>
    public bool UseItemFromInventory(ItemSO item, bool includeHotbar, out string reason)
    {
        reason = "";
        if (item == null)
        {
            reason = "Nothing to use.";
            return false;
        }
        List<InventoryItem> stacks = FindStacks(item, includeHotbar);
        if (stacks.Count == 0)
        {
            reason = $"No {item.Name} left.";
            return false;
        }
        InventoryItem stack = stacks.Find(s => !s.IsOnCooldown());
        if (stack == null)
        {
            reason = $"{item.Name} is on cooldown ({stacks[0].GetRemainingCooldown():0.0}s).";
            return false;
        }

        // Throwables: thrown from the hand without changing the item held.
        if (item is WeaponSO w && w.Ranged is ThrowMechanic)
        {
            if (weaponController == null)
            {
                reason = "Cannot throw without a Weapon Controller.";
                return false;
            }
            if (!weaponController.QuickThrow(w, stack))
            {
                reason = $"Cannot throw {item.Name} now.";
                return false;
            }
            if (w.Cooldown > 0f)
                stack.SetCooldown(CombatStats.ItemCooldown(this, w.Cooldown));
            return true;
        }

        if (!item.CanUseFromQuickSlot)
        {
            reason = $"{item.Name} cannot be used like this.";
            return false;
        }
        if (!ItemUsageHandler.UseHeldItem(player, playerStatusController, weaponController, stack))
        {
            reason = $"{item.Name} cannot be used now.";
            return false;
        }
        if (item.Cooldown > 0f)
            stack.SetCooldown(CombatStats.ItemCooldown(this, item.Cooldown));
        ItemUsageHandler.ConsumeOneUse(player, stack);
        if (stack.stackCurrent <= 0)
            RemoveEmptiedStack(stack);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>Removes a stack that reached 0 (its slot empties; the hand updates when it was held).</summary>
    private void RemoveEmptiedStack(InventoryItem stack)
    {
        if (stack == null)
            return;
        bool wasHeld = stack == SelectedHotbarItem;
        InventorySlot slot = FindSlotHolding(stack.gameObject);
        if (slot != null)
            slot.heldItem = null;
        Destroy(stack.gameObject);
        if (wasHeld)
            RefreshHand(true);
    }

    /// <summary>The ammo of a type the inventory holds (<paramref name="preferred"/> first). <paramref name="best"/> = what is fired next.</summary>
    public int CountAmmo(string ammoType, AmmoSO preferred, out AmmoSO best)
    {
        best = null;
        if (string.IsNullOrWhiteSpace(ammoType))
            return 0;
        int preferredCount = 0, firstCount = 0;
        foreach (GameObject go in StorageSlots)
        {
            InventorySlot s = go != null ? go.GetComponent<InventorySlot>() : null;
            InventoryItem it = s != null && s.heldItem != null ? s.heldItem.GetComponent<InventoryItem>() : null;
            if (it == null || !(it.itemScriptableObject is AmmoSO a) || !string.Equals(a.AmmoType?.Trim(), ammoType.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            if (a == preferred)
                preferredCount += it.stackCurrent;
            if (best == null)
                best = a;
            if (a == best)
                firstCount += it.stackCurrent;
        }
        if (preferred != null && preferredCount > 0)
        {
            best = preferred;
            return preferredCount;
        }
        return best != null ? firstCount : 0;
    }

    /// <summary>Spends ammo (weight follows). Returns how many were spent.</summary>
    public int ConsumeAmmo(AmmoSO ammo, int amount)
    {
        if (ammo == null || amount <= 0)
            return 0;
        int removed = InventoryUtils.RemoveItems(StorageSlots, ammo, amount);
        if (removed > 0)
        {
            InventoryUtils.UpdatePlayerWeight(player, -ammo.Weight * removed);
            NotifyInventoryChanged();
        }
        return removed;
    }

    /// <summary>Removes units of a stack (a thrown knife): durability and weight follow; an emptied stack is removed.</summary>
    public int ConsumeItem(InventoryItem item, int amount)
    {
        if (item == null || item.itemScriptableObject == null || amount <= 0)
            return 0;
        int n = Mathf.Min(amount, item.stackCurrent);
        for (int i = 0; i < n; i++)
        {
            item.stackCurrent--;
            if (item.DurabilityList.Count > 0)
            {
                item.durability = item.DurabilityList[item.DurabilityList.Count - 1];
                item.DurabilityList.RemoveAt(item.DurabilityList.Count - 1);
            }
            else
            {
                item.durability = InventoryItem.StartingDurability(item.itemScriptableObject);
            }
        }
        item.UpdateTotalWeight();
        item.RefreshUI();
        InventoryUtils.UpdatePlayerWeight(player, -item.itemScriptableObject.Weight * n);
        if (item.stackCurrent <= 0)
            RemoveEmptiedStack(item);
        NotifyInventoryChanged();
        return n;
    }

    /// <summary>
    /// A free slot for a new stack of <paramref name="item"/>: in the grid where it fits (rotation tried), else a free bag
    /// slot, else (or first, with <paramref name="preferHotbar"/>) a free hotbar slot. Null = no room.
    /// </summary>
    public InventorySlot FindFreeSlotFor(ItemSO item, bool preferHotbar, out bool rotated)
    {
        rotated = false;
        InventorySlot hotbar = null;
        if (HotbarSlots != null)
            foreach (GameObject go in HotbarSlots)
                if (go != null && go.TryGetComponent(out InventorySlot hs) && hs.heldItem == null && SlotTypeHelper.CanPlace(item, hs.SlotType))
                {
                    hotbar = hs;
                    break;
                }
        if (preferHotbar && hotbar != null)
            return hotbar;
        if (gridActive && grid != null)
        {
            if (grid.FindSpace(item, out InventorySlot anchor, out rotated))
                return anchor;
        }
        else if (slotManager?.InventorySlots != null)
        {
            foreach (GameObject go in slotManager.InventorySlots)
                if (go != null && go.TryGetComponent(out InventorySlot s) && s.heldItem == null)
                    return s;
        }
        rotated = false;
        return hotbar;
    }

    public InventorySlot FindFreeSlotFor(ItemSO item, bool preferHotbar = false) => FindFreeSlotFor(item, preferHotbar, out _);

    /// <summary>
    /// Moves an existing item (taken off an equipment slot, for example) into the bag: where it fits in the grid, or the
    /// first free slot. It is first removed from the slot holding it. False (nothing changes) when there is no room.
    /// </summary>
    public bool PlaceInBag(InventoryItem item)
    {
        if (item == null || item.itemScriptableObject == null)
            return false;
        InventorySlot from = FindSlotHolding(item.gameObject);
        if (gridActive && grid != null)
        {
            if (from != null && grid.IsCell(from))
                return true; // already in the bag
            if (!grid.FindSpace(item.itemScriptableObject, out InventorySlot anchor, out bool rotated) || !grid.TryGetCell(anchor, out int x, out int y))
                return false;
            if (from != null) from.heldItem = null;
            if (grid.PlaceAt(item, x, y, rotated))
                return true;
            if (from != null) from.SetHeldItem(item.gameObject);
            return false;
        }
        if (slotManager?.InventorySlots == null)
            return false;
        foreach (GameObject go in slotManager.InventorySlots)
            if (go != null && go.TryGetComponent(out InventorySlot s) && s.heldItem == null)
            {
                if (from != null) from.heldItem = null;
                s.SetHeldItem(item.gameObject);
                return true;
            }
        return false;
    }

    // ------------------------------------------------------------------ slots and layout (delegates)
    public void RemoveHotbarSlots(int count) => NumberOfHotBarSlots = Mathf.Max(1, NumberOfHotBarSlots - count);

    public void AddInventorySlots(int count) => NumberOfInventorySlots += count;

    public void RemoveInventorySlots(int count) => NumberOfInventorySlots = Mathf.Max(1, NumberOfInventorySlots - count);

    public void SetLayoutPreset(UILayoutManager.LayoutPreset preset) => uiLayoutManager?.SetLayoutPreset(preset);

    public void ConfigureCustomLayout(
        SlotManager.LayoutMode layoutMode = SlotManager.LayoutMode.Adaptive,
        float minSlotSize = 60f,
        float maxSlotSize = 120f,
        float minSpacing = 5f,
        float maxSpacing = 15f,
        float preferredSpacing = 8f,
        SlotManager.SpaceDistribution spaceDistribution = SlotManager.SpaceDistribution.Balanced,
        SlotManager.GridConstraintMode constraintMode = SlotManager.GridConstraintMode.Adaptive,
        int constraintValue = 5,
        SlotManager.ContentAlignment alignment = SlotManager.ContentAlignment.Center)
    {
        uiLayoutManager?.ConfigureCustomLayout(
            layoutMode, minSlotSize, maxSlotSize, minSpacing, maxSpacing,
            preferredSpacing, spaceDistribution, constraintMode, constraintValue, alignment);
    }

    public void SetCustomSpacing(Vector2 spacing) => uiLayoutManager?.SetCustomSpacing(spacing);

    public void SetCustomPadding(int left, int right, int top, int bottom) => uiLayoutManager?.SetCustomPadding(left, right, top, bottom);

    public void SetGridColumns(int columns) => uiLayoutManager?.SetGridColumns(columns);

    public void SetGridRows(int rows) => uiLayoutManager?.SetGridRows(rows);

    public void ForceAdaptiveGrid() => uiLayoutManager?.ForceAdaptiveGrid();

    public void AddHotbarSlots(int count) => NumberOfHotBarSlots += count;

    public float GetPanelUtilization() => uiLayoutManager?.GetPanelUtilization() ?? 0f;
}
