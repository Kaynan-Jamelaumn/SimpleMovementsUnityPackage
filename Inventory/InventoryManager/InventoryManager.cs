using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Enhanced InventoryManager with armor set system integration
public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Dynamic Slot Configuration")]
    [SerializeField, Tooltip("Number of hotbar slots to create dynamically")]
    private int numberOfHotBarSlots = 4;
    [SerializeField, Tooltip("Number of inventory slots to create dynamically")]
    private int numberOfInventorySlots = 20;

    [Header("Core Inventory Configuration")]
    [SerializeField, Tooltip("Prefab used to create inventory items")]
    private GameObject itemPrefab;

    [Header("UI Management Components")]
    [SerializeField, Tooltip("Manages slot creation and layout calculations")]
    private SlotManager slotManager = new SlotManager();
    [SerializeField, Tooltip("Handles layout presets and screen adaptation")]
    private UILayoutManager uiLayoutManager = new UILayoutManager();

    [Header("UI Panel References")]
    [SerializeField, Tooltip("Main inventory panel container")]
    private GameObject inventoryParent;
    [SerializeField, Tooltip("Equipment/gear panel container")]
    private GameObject equippableInventory;
    [SerializeField, Tooltip("Storage interaction panel container")]
    private GameObject storageParent;
    [SerializeField, Tooltip("Transform where held items are instantiated")]
    private Transform handParent;
    [SerializeField, Tooltip("UI component for displaying item information")]
    private ItemInfo itemInfo;
    [SerializeField, Tooltip("Camera reference for world interactions")]
    private Camera cam;

    [Header("Player References")]
    [SerializeField, Tooltip("Player GameObject reference")]
    private GameObject player;
    [SerializeField, Tooltip("Player status and stats controller")]
    private PlayerStatusController playerStatusController;
    [SerializeField, Tooltip("Player weapon handling controller")]
    private WeaponController weaponController;

    [Header("Armor System Integration")]
    [SerializeField, Tooltip("Armor set manager for handling set bonuses")]
    private ArmorSetManager armorSetManager;
    [SerializeField, Tooltip("UI manager for armor set display")]
    private ArmorSetUIManager armorSetUIManager;
    [SerializeField, Tooltip("Enable automatic armor optimization")]
    private bool enableArmorOptimization = true;
    [SerializeField, Tooltip("Show armor set notifications")]
    private bool showArmorSetNotifications = true;

    [Tooltip("Custom effect identifiers for future special mechanics")]
    public List<string> specialEffectIds = new List<string>();

    // Force serialization helper
    [SerializeField, HideInInspector]
    private bool _forceSerialize = true;

    // State management components (runtime only)
    [System.NonSerialized] private DragHandler dragHandler;
    [System.NonSerialized] private UIStateManager uiStateManager;
    [System.NonSerialized] private StorageManager storageManager;
    [System.NonSerialized] private Mouse mouse;

    // Change detection for editor (runtime only)
    [System.NonSerialized] private int previousHotbarSlots;
    [System.NonSerialized] private int previousInventorySlots;

    // Armor system cache for performance
    [System.NonSerialized] private ArmorSetUtils.ArmorCache armorCache;

    // Properties - UI Access
    public Transform HandParent => handParent;
    public GameObject[] Slots => slotManager?.InventorySlots;
    public GameObject[] HotbarSlots => slotManager?.HotbarSlots;
    public GameObject Player => player;
    public bool IsStorageOpened => storageManager?.IsStorageOpened ?? false;
    public SlotManager.LayoutData CurrentLayout => slotManager?.LegacyCurrentLayout;
    public UILayoutManager LayoutManager => uiLayoutManager;
    public ArmorSetManager ArmorSetManager => armorSetManager;
    public ArmorSetUIManager ArmorSetUIManager => armorSetUIManager;

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

    // Unity Lifecycle
    private void Awake()
    {
        // Initialize serialized objects if they're null
        if (slotManager == null) slotManager = new SlotManager();
        if (uiLayoutManager == null) uiLayoutManager = new UILayoutManager();

        InitializeComponents();
        InitializeSubSystems();
        InitializeArmorSystem();

        // Store initial values for change detection
        previousHotbarSlots = numberOfHotBarSlots;
        previousInventorySlots = numberOfInventorySlots;
    }

    private void Start()
    {
        InitializeManagers();
        HotbarHandler.HotbarItemChanged(slotManager.HotbarSlots, handParent);

        // Initialize armor system after all managers are ready
        PostInitializeArmorSystem();
    }

    private void Update()
    {
        HandleHotbarInput();
        uiStateManager?.UpdateUI();
        dragHandler?.UpdateDraggedObjectPosition();
        uiLayoutManager?.Update();
        CheckForSlotCountChanges();

        // Update armor cache periodically for performance
        UpdateArmorCache();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // Enhanced initialization with armor system
    private void InitializeComponents()
    {
        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));
        armorSetManager = this.CheckComponent(armorSetManager, nameof(armorSetManager), false);
        armorSetUIManager = this.CheckComponent(armorSetUIManager, nameof(armorSetUIManager), false);

        if (cam == null) cam = Camera.main;
        mouse = Mouse.current;
        ValidateUIComponents();
    }

    private void ValidateUIComponents()
    {
        if (slotManager == null)
            Debug.LogError("SlotManager is not assigned in InventoryManager");
        if (uiLayoutManager == null)
            Debug.LogError("UILayoutManager is not assigned in InventoryManager");
        if (handParent == null)
            Debug.LogError("HandParent transform is not assigned in InventoryManager");
    }

    private void InitializeSubSystems()
    {
        dragHandler = new DragHandler(mouse);

        if (inventoryParent == null)
        {
            Debug.LogError("InventoryManager: inventoryParent is not assigned!");
            return;
        }

        try
        {
            uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
            storageManager = new StorageManager(storageParent, itemPrefab);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize UI systems: {e.Message}");
        }
    }

    private void InitializeArmorSystem()
    {
        armorCache = new ArmorSetUtils.ArmorCache();

        if (armorSetManager != null)
        {
            // Subscribe to armor set events
            armorSetManager.OnSetCompleted += OnArmorSetCompleted;
            armorSetManager.OnSetBroken += OnArmorSetBroken;
            armorSetManager.OnSetEffectActivated += OnArmorSetEffectActivated;
        }
    }

    private void PostInitializeArmorSystem()
    {
        // Scan for equipped armor after all systems are initialized
        if (armorSetManager != null)
        {
            armorSetManager.ScanEquippedArmor();
        }
    }

    private void InitializeManagers()
    {
        InitializeSlotManager();
        InitializeUILayoutManager();
    }

    private void InitializeSlotManager()
    {
        if (slotManager == null) return;

        slotManager.Initialize(playerStatusController, player);
        slotManager.OnSlotsChanged += OnSlotsChanged;
        slotManager.OnLayoutChanged += OnLayoutChanged;
        slotManager.CreateAllSlots(numberOfHotBarSlots, numberOfInventorySlots);
    }

    private void InitializeUILayoutManager()
    {
        if (uiLayoutManager == null) return;

        uiLayoutManager.Initialize(slotManager);
        uiLayoutManager.OnPresetChanged += OnLayoutPresetChanged;
    }

    private void UnsubscribeFromEvents()
    {
        if (slotManager != null)
        {
            slotManager.OnSlotsChanged -= OnSlotsChanged;
            slotManager.OnLayoutChanged -= OnLayoutChanged;
        }
        if (uiLayoutManager != null)
        {
            uiLayoutManager.OnPresetChanged -= OnLayoutPresetChanged;
        }
        if (armorSetManager != null)
        {
            armorSetManager.OnSetCompleted -= OnArmorSetCompleted;
            armorSetManager.OnSetBroken -= OnArmorSetBroken;
            armorSetManager.OnSetEffectActivated -= OnArmorSetEffectActivated;
        }
    }

    // Enhanced event handlers with armor system integration
    private void OnSlotsChanged()
    {
        HotbarHandler.ForceRefresh(slotManager.HotbarSlots, handParent);

        // Update armor system when slots change
        if (armorSetManager != null)
        {
            armorSetManager.ScanEquippedArmor();
        }
    }

    private void OnLayoutChanged(SlotLayoutCalculator.LayoutData layoutData)
    {
        // Handle layout changes if needed
    }

    private void OnLayoutPresetChanged(UILayoutManager.LayoutPreset preset)
    {
        // Handle preset changes if needed
    }

    // Armor set event handlers
    private void OnArmorSetCompleted(ArmorSet armorSet)
    {
        if (showArmorSetNotifications)
        {
            Debug.Log($"Armor Set Completed: {armorSet.SetName}!");
            // You could show a UI notification here
        }
    }

    private void OnArmorSetBroken(ArmorSet armorSet)
    {
        if (showArmorSetNotifications)
        {
            Debug.Log($"Armor Set Broken: {armorSet.SetName}");
        }
    }

    private void OnArmorSetEffectActivated(ArmorSetEffect effect)
    {
        if (showArmorSetNotifications)
        {
            Debug.Log($"Set Effect Activated: {effect.effectName}");
        }
    }

    // Armor cache management
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

    // Enhanced Input Handling
    public void OnInventory(InputAction.CallbackContext value)
    {
        if (value.started) ToggleInventory();
    }

    // New input method for armor set UI
    public void OnArmorSetUI(InputAction.CallbackContext value)
    {
        if (value.started && armorSetUIManager != null)
        {
            armorSetUIManager.ToggleUI();
        }
    }

    public void OnUseItem(InputAction.CallbackContext value)
    {
        if (!value.started || (dragHandler?.IsDragging ?? false)) return;
        var selectedSlot = GetSelectedHotbarSlot();
        var heldItem = ItemUsageHandler.GetHeldItem(selectedSlot);
        if (heldItem == null || !ItemUsageHandler.HandleCooldown(heldItem)) return;
        ItemUsageHandler.UseHeldItem(player, playerStatusController, weaponController, heldItem);
        ItemUsageHandler.HandleItemDurabilityAndStack(player, handParent, selectedSlot, heldItem);
    }

    private void HandleHotbarInput()
    {
        HotbarHandler.CheckForHotbarInput(slotManager.HotbarSlots, handParent);
    }

    // Enhanced Inventory Operations
    private void ToggleInventory()
    {
        if (uiStateManager?.IsInventoryOpened ?? false)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    private void OpenInventory()
    {
        // Try to reinitialize UIStateManager if it's null
        if (uiStateManager == null && inventoryParent != null)
        {
            try
            {
                uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to reinitialize UIStateManager: {e.Message}");
                return;
            }
        }

        if (inventoryParent == null)
        {
            Debug.LogError("Cannot open inventory - inventoryParent is null!");
            return;
        }

        SetCursorState(CursorLockMode.None, true);
        uiStateManager.SetInventoryOpened(true);
        uiStateManager.UpdateUI();

        // Update armor cache when inventory opens
        UpdateArmorCache();
    }

    private void CloseInventory()
    {
        SetCursorState(CursorLockMode.Locked, false);

        if (uiStateManager != null)
        {
            uiStateManager.SetInventoryOpened(false);
            uiStateManager.UpdateUI();
        }
        else
        {
            // Fallback: directly close the panel if UIStateManager is null
            if (inventoryParent != null) inventoryParent.SetActive(false);
            if (equippableInventory != null) equippableInventory.SetActive(false);
        }

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
        return hotbarSlots[HotbarHandler.SelectedHotbarSlot]?.GetComponent<InventorySlot>();
    }

    // Enhanced Pointer Event Handling with armor awareness
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            HideItemInfo();
            return;
        }
        var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventorySlot>();
        if (itemInfo?.gameObject?.activeSelf ?? false) HideItemInfo();
        else ShowItemInfo(slot);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventorySlot>();
        if (slot?.heldItem != null)
        {
            dragHandler?.StartDragging(slot, eventData.pointerCurrentRaycast.gameObject);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!(dragHandler?.ValidateDragOperation(eventData) ?? false)) return;
        HandleItemDrop(eventData);
        dragHandler?.CleanupDragging();
    }

    // Enhanced Item Management with armor support
    private void HandleItemDrop(PointerEventData eventData)
    {
        var clickedObject = eventData.pointerCurrentRaycast.gameObject;
        var slot = clickedObject?.GetComponent<InventorySlot>();
        var draggedItem = dragHandler?.DraggedObject?.GetComponent<InventoryItem>();
        if (draggedItem == null) return;

        var itemType = draggedItem.itemScriptableObject.ItemType;

        // Check for armor-specific slot compatibility
        if (draggedItem.itemScriptableObject is ArmorSO armor)
        {
            if (slot != null && ArmorEquipmentHandler.IsSlotCompatibleWithArmor(slot, armor))
            {
                if (InventoryUtils.IsSlotEmpty(slot))
                    ItemHandler.PlaceItemInSlot(slot, draggedItem.gameObject, playerStatusController);
                else
                    ItemHandler.SwitchOrFillStack(slot, draggedItem.gameObject, dragHandler.LastItemSlotObject, playerStatusController);
            }
            else if (clickedObject?.name == "DropItem")
            {
                ItemHandler.DropItem(draggedItem.gameObject, dragHandler.LastItemSlotObject, playerStatusController, cam, player);
            }
            else
            {
                ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, draggedItem.gameObject);
            }
        }
        else if (slot != null && InventoryUtils.IsCompatibleSlot(slot, itemType))
        {
            if (InventoryUtils.IsSlotEmpty(slot))
                ItemHandler.PlaceItemInSlot(slot, draggedItem.gameObject, playerStatusController);
            else
                ItemHandler.SwitchOrFillStack(slot, draggedItem.gameObject, dragHandler.LastItemSlotObject, playerStatusController);
        }
        else if (clickedObject?.name == "DropItem")
        {
            ItemHandler.DropItem(draggedItem.gameObject, dragHandler.LastItemSlotObject, playerStatusController, cam, player);
        }
        else
        {
            ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, draggedItem.gameObject);
        }

        HotbarHandler.HotbarItemChanged(slotManager.HotbarSlots, handParent);
    }

    private void ShowItemInfo(InventorySlot slot)
    {
        if (slot?.heldItem != null && itemInfo != null)
            itemInfo.ShowItemInfo(slot.heldItem.GetComponent<InventoryItem>());
        else
            HideItemInfo();
    }

    private void HideItemInfo()
    {
        if (itemInfo?.gameObject != null) itemInfo.gameObject.SetActive(false);
    }

    // Item Pickup and Creation
    public void ItemPicked(GameObject pickedItem)
    {
        ItemPickUpHandler.AddItemToInventory(this, pickedItem, slotManager.InventorySlots, itemPrefab, player);
    }

    public void InstantiateClassItems(List<GameObject> classItems)
    {
        foreach (var item in classItems)
            ItemPicked(item);
    }

    public void InstantiateNewItem(GameObject emptySlot, GameObject pickedItem)
    {
        var newItem = CreateInventoryItem(pickedItem);
        SetupItemInSlot(newItem, emptySlot, pickedItem);
    }

    private GameObject CreateInventoryItem(GameObject pickedItem)
    {
        var newItem = Instantiate(itemPrefab);
        var newItemComponent = newItem.GetComponent<InventoryItem>();
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
        newItemComponent.Initialize(pickedItemProperties);
        return newItem;
    }

    private void SetupItemInSlot(GameObject newItem, GameObject emptySlot, GameObject pickedItem)
    {
        if (newItem == null || emptySlot == null) return;

        var slotComponent = emptySlot.GetComponent<InventorySlot>();
        var itemComponent = newItem.GetComponent<InventoryItem>();
        if (slotComponent == null || itemComponent?.itemScriptableObject == null) return;

        // Calculate weight
        var totalWeight = itemComponent.itemScriptableObject.Weight * itemComponent.stackCurrent;
        itemComponent.totalWeight = totalWeight;

        // Update player weight
        InventoryUtils.UpdatePlayerWeight(player, totalWeight);

        // Use the slot's SetHeldItem method
        slotComponent.SetHeldItem(newItem);

        // If this is armor being placed in an equipment slot, equip it
        if (itemComponent.itemScriptableObject is ArmorSO armorSO &&
            SlotTypeHelper.IsArmorSlot(slotComponent.SlotType) &&
            ArmorEquipmentHandler.IsSlotCompatibleWithArmor(slotComponent, armorSO))
        {
            ArmorEquipmentHandler.EquipArmor(slotComponent, itemComponent, playerStatusController);
        }
    }

    // Storage Management
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
    }

    // Enhanced Public Utility Methods with armor support
    public int GetItemCount(ItemSO itemSO)
    {
        return InventoryUtils.GetItemCount(slotManager.InventorySlots, itemSO);
    }

    public bool HasEnoughSpace(ItemSO itemSO, int requiredQuantity)
    {
        return InventoryUtils.HasEnoughSpace(slotManager.InventorySlots, itemSO, requiredQuantity);
    }

    public bool HasEnoughItems(ItemSO itemSO, int requiredAmount)
    {
        return InventoryUtils.HasEnoughItems(slotManager.InventorySlots, itemSO, requiredAmount);
    }

    public int RemoveItems(ItemSO itemSO, int amountToRemove)
    {
        return InventoryUtils.RemoveItems(slotManager.InventorySlots, itemSO, amountToRemove);
    }

    public float GetTotalInventoryWeight()
    {
        return InventoryUtils.CalculateInventoryWeight(slotManager.InventorySlots);
    }

    // New armor-specific utility methods
    public float GetTotalArmorWeight()
    {
        return InventoryUtils.CalculateArmorWeight(slotManager.InventorySlots);
    }

    public float GetTotalDefense()
    {
        return armorCache?.GetTotalDefense() ?? ArmorSetUtils.CalculateTotalDefense(this);
    }

    public float GetTotalMagicDefense()
    {
        return armorCache?.GetTotalMagicDefense() ?? ArmorSetUtils.CalculateTotalMagicDefense(this);
    }

    public List<ArmorSO> GetEquippedArmor()
    {
        return ArmorSetUtils.GetEquippedArmor(this);
    }

    public Dictionary<ArmorSet, List<ArmorSO>> GetEquippedArmorSets()
    {
        return ArmorSetUtils.GetEquippedArmorSets(this);
    }

    public List<ArmorSet> GetCompleteSets()
    {
        return ArmorSetUtils.GetCompleteSets(this);
    }

    public bool IsArmorEquipped(ArmorSO armor)
    {
        return ArmorSetUtils.IsArmorEquipped(this, armor);
    }

    public ArmorSO GetEquippedArmorForSlot(ArmorSlotType slotType)
    {
        return ArmorSetUtils.GetEquippedArmorForSlot(this, slotType);
    }

    // Armor management methods
    public bool QuickEquipArmor(ArmorSO armor)
    {
        return ArmorEquipmentHandler.QuickEquipArmor(this, armor, playerStatusController);
    }

    public void UnequipAllArmor()
    {
        ArmorEquipmentHandler.UnequipAllArmor(this, playerStatusController);
    }

    public void OptimizeArmorSets()
    {
        if (enableArmorOptimization)
        {
            ArmorEquipmentHandler.OptimizeArmorSets(this, playerStatusController);
        }
    }

    public string GetArmorSummary()
    {
        return ArmorSetUtils.CreateArmorSummary(this);
    }

    public string GetEquipmentReport()
    {
        return ArmorEquipmentHandler.GetEquipmentReport(this);
    }

    // Debug Methods (only in editor)
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogInventoryState()
    {
        InventoryUtils.LogInventoryState(slotManager.InventorySlots, $"Hotbar: {numberOfHotBarSlots}, Inventory: {numberOfInventorySlots}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogLayoutInfo()
    {
        uiLayoutManager?.LogLayoutInfo();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogArmorSetStatus()
    {
        if (armorSetManager != null)
        {
            Debug.Log(armorSetManager.GetSetStatusReport());
        }
        else
        {
            Debug.LogWarning("ArmorSetManager not found!");
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogArmorSummary()
    {
        InventoryUtils.LogArmorSummary(slotManager.InventorySlots);
    }

    // Context menu debug methods
    [ContextMenu("Log Full Inventory State")]
    private void DebugLogFullState()
    {
        LogInventoryState();
        LogArmorSummary();
        LogArmorSetStatus();
    }

    [ContextMenu("Optimize Armor Sets")]
    private void DebugOptimizeArmor()
    {
        OptimizeArmorSets();
    }

    [ContextMenu("Force Armor Scan")]
    private void DebugForceArmorScan()
    {
        armorSetManager?.ScanEquippedArmor();
    }

    [ContextMenu("Validate Armor Equipment")]
    private void DebugValidateArmorEquipment()
    {
        bool isValid = ArmorEquipmentHandler.ValidateArmorEquipment(this);
        Debug.Log($"Armor equipment validation: {(isValid ? "PASSED" : "FAILED")}");
    }


    public void RemoveHotbarSlots(int count)
    {
        NumberOfHotBarSlots = Mathf.Max(1, NumberOfHotBarSlots - count);
    }

    public void AddInventorySlots(int count)
    {
        NumberOfInventorySlots += count;
    }

    public void RemoveInventorySlots(int count)
    {
        NumberOfInventorySlots = Mathf.Max(1, NumberOfInventorySlots - count);
    }

    // UI Layout Management (Delegates to UILayoutManager)
    public void SetLayoutPreset(UILayoutManager.LayoutPreset preset)
    {
        uiLayoutManager?.SetLayoutPreset(preset);
    }

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

    public void SetCustomSpacing(Vector2 spacing)
    {
        uiLayoutManager?.SetCustomSpacing(spacing);
    }

    public void SetCustomPadding(int left, int right, int top, int bottom)
    {
        uiLayoutManager?.SetCustomPadding(left, right, top, bottom);
    }

    public void SetGridColumns(int columns)
    {
        uiLayoutManager?.SetGridColumns(columns);
    }

    public void SetGridRows(int rows)
    {
        uiLayoutManager?.SetGridRows(rows);
    }
    public void ForceAdaptiveGrid()
    {
        uiLayoutManager?.ForceAdaptiveGrid();
    }
    public void AddHotbarSlots(int count)
    {
        NumberOfHotBarSlots += count;
    }
    public float GetPanelUtilization()
    {
        return uiLayoutManager?.GetPanelUtilization() ?? 0f;
    }

}