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
public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
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
                slotManager?.UpdateInventorySlots(value);
            }
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
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
        SubscribeArmorSets();
        SubscribeWeapon();
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
    }

    private void OnDestroy()
    {
        UnbindInput();
        UnsubscribeFromEvents();
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
            slotManager.CreateAllSlots(numberOfHotBarSlots, numberOfInventorySlots);
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
        subscribedWeapon = true;
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
            weaponController.HeldItemBroke -= OnHeldItemBroke;
        subscribedWeapon = false;
    }

    // ------------------------------------------------------------------ change handling
    private void OnSlotsChanged()
    {
        storageSlotsDirty = true;
        equipmentSlotsDirty = true;
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
        if (weaponController == null)
            return;
        InventoryItem held = SelectedHotbarItem;
        if (held != null && held.itemScriptableObject is WeaponSO weapon)
            weaponController.EquipWeapon(weapon, held);
        else if (weaponController.EquippedWeapon != null)
            weaponController.UnequipWeapon();
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
            slotManager?.UpdateInventorySlots(numberOfInventorySlots);
            previousInventorySlots = numberOfInventorySlots;
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
                heldItem.SetCooldown(weapon.Cooldown);
            return started;
        }

        if (!ItemUsageHandler.HandleCooldown(heldItem)) return false;
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
            dragHandler.StartDragging(slot, slot.gameObject);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (dragHandler == null || !dragHandler.IsDragging && dragHandler.DraggedObject == null) return;
        if (!dragHandler.ValidateDragOperation(eventData))
        {
            // Released over nothing (or with another button): the item goes back instead of being lost.
            CancelDrag();
            return;
        }
        HandleItemDrop(eventData);
        dragHandler.CleanupDragging();
        NotifyInventoryChanged();
    }

    /// <summary>Puts a dragged item back into its slot.</summary>
    public void CancelDrag()
    {
        if (dragHandler == null || dragHandler.DraggedObject == null)
            return;
        ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, dragHandler.DraggedObject);
        dragHandler.CleanupDragging();
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
            return slot;
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

        InventorySlot slot = FindSlotUnderPointer(eventData);
        if (slot != null && slot.gameObject == dragHandler.LastItemSlotObject)
        {
            ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, dragged);
        }
        else if (slot != null && SlotTypeHelper.CanPlace(draggedItem.itemScriptableObject, slot.SlotType))
        {
            if (InventoryUtils.IsSlotEmpty(slot))
                ItemHandler.PlaceItemInSlot(slot, dragged, playerStatusController);
            else
                ItemHandler.SwitchOrFillStack(slot, dragged, dragHandler.LastItemSlotObject, playerStatusController);
        }
        else if (slot == null && IsDropZone(clickedObject))
        {
            ItemHandler.DropItem(dragged, dragHandler.LastItemSlotObject, playerStatusController, cam, player);
        }
        else
        {
            ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, dragged);
        }
        RefreshHand(true);
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

        // 2) empty slots (inventory before hotbar)
        foreach (GameObject go in StorageSlots)
        {
            if (remaining <= 0) break;
            if (go == null || !go.TryGetComponent(out InventorySlot slot) || slot.SlotType != SlotType.Common || slot.heldItem != null) continue;
            int n = Mathf.Min(stackMax, remaining);
            var chunk = new List<int>();
            InventoryUtils.TransferDurabilityList(dur, chunk, Mathf.Min(n, dur.Count));
            GameObject obj = Instantiate(itemPrefab);
            InventoryItem created = obj.GetComponent<InventoryItem>();
            if (created == null)
            {
                Debug.LogError("[Inventory] The Item Prefab has no InventoryItem component.", itemPrefab);
                Destroy(obj);
                break;
            }
            created.Initialize(item, n, chunk);
            slot.SetHeldItem(obj);
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

    /// <summary>
    /// Creates an inventory item in an empty slot (split stacks, scripts). <paramref name="addWeight"/>: the player's
    /// carried weight grows by the item's weight. Returns the item, or null when the slot is not usable.
    /// </summary>
    public InventoryItem CreateItemInSlot(InventorySlot slot, ItemSO item, int quantity, IList<int> durabilities = null, bool addWeight = true)
    {
        if (slot == null || item == null || itemPrefab == null || slot.heldItem != null || !SlotTypeHelper.CanPlace(item, slot.SlotType))
            return null;
        GameObject obj = Instantiate(itemPrefab);
        InventoryItem created = obj.GetComponent<InventoryItem>();
        if (created == null)
        {
            Debug.LogError("[Inventory] The Item Prefab has no InventoryItem component.", itemPrefab);
            Destroy(obj);
            return null;
        }
        created.Initialize(item, quantity, durabilities);
        slot.SetHeldItem(obj);
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

    public bool HasEnoughSpace(ItemSO itemSO, int requiredQuantity) => InventoryUtils.HasEnoughSpace(StorageSlots, itemSO, requiredQuantity);

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
