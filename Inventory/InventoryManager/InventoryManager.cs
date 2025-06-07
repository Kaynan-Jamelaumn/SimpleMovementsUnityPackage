using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    private SlotManager slotManager;
    [SerializeField, Tooltip("Handles layout presets and screen adaptation")]
    private UILayoutManager uiLayoutManager;

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

    // State management components
    private DragHandler dragHandler;
    private UIStateManager uiStateManager;
    private StorageManager storageManager;

    // Input reference
    private Mouse mouse;

    // Change detection for editor
    private int previousHotbarSlots;
    private int previousInventorySlots;

    // Properties - UI Access
    public Transform HandParent => handParent;
    public GameObject[] Slots => slotManager?.InventorySlots;
    public GameObject[] HotbarSlots => slotManager?.HotbarSlots;
    public GameObject Player => player;
    public bool IsStorageOpened => storageManager.IsStorageOpened;
    public SlotManager.LayoutData CurrentLayout => slotManager?.LegacyCurrentLayout; // Use legacy property for compatibility
    public UILayoutManager LayoutManager => uiLayoutManager;

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
        InitializeComponents();
        InitializeSubSystems();
        // Store initial values for change detection
        previousHotbarSlots = numberOfHotBarSlots;
        previousInventorySlots = numberOfInventorySlots;
    }

    private void Start()
    {
        InitializeManagers();
        HotbarHandler.HotbarItemChanged(slotManager.HotbarSlots, handParent);
    }

    private void Update()
    {
        HandleHotbarInput();
        uiStateManager.UpdateUI();
        dragHandler.UpdateDraggedObjectPosition();
        uiLayoutManager.Update();
        // Check for slot count changes in editor
        CheckForSlotCountChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // Initialization
    private void InitializeComponents()
    {
        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));
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
        if (itemInfo == null)
            Debug.LogWarning("ItemInfo component is not assigned in InventoryManager");
    }

    private void InitializeSubSystems()
    {
        dragHandler = new DragHandler(mouse);
        uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
        storageManager = new StorageManager(storageParent, itemPrefab);
    }

    private void InitializeManagers()
    {
        InitializeSlotManager();
        InitializeUILayoutManager();
    }

    private void InitializeSlotManager()
    {
        if (slotManager == null)
        {
            Debug.LogError("SlotManager is not assigned in InventoryManager!");
            return;
        }

        slotManager.Initialize(playerStatusController, player);
        slotManager.OnSlotsChanged += OnSlotsChanged;

        // Subscribe to layout changed events - handle both new and legacy format
        slotManager.OnLayoutChanged += OnLayoutChanged;

        slotManager.CreateAllSlots(numberOfHotBarSlots, numberOfInventorySlots);
    }

    private void InitializeUILayoutManager()
    {
        if (uiLayoutManager == null)
        {
            Debug.LogError("UILayoutManager is not assigned in InventoryManager!");
            return;
        }
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
    }

    // Event Handlers
    private void OnSlotsChanged()
    {
        HotbarHandler.ForceRefresh(slotManager.HotbarSlots, handParent);
    }

    private void OnLayoutChanged(SlotLayoutCalculator.LayoutData layoutData)
    {
        // Handle layout changes if needed
        //Debug.Log($"Layout changed: {layoutData.columns}x{layoutData.rows}, Utilization: {layoutData.panelUtilization:P1}");
    }

    private void OnLayoutPresetChanged(UILayoutManager.LayoutPreset preset)
    {
        Debug.Log($"Layout preset changed to: {preset}");
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

    // Runtime slot management methods
    public void AddHotbarSlots(int count)
    {
        NumberOfHotBarSlots += count;
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

    public bool IsLayoutOptimal()
    {
        return uiLayoutManager?.IsLayoutOptimal() ?? false;
    }

    public float GetPanelUtilization()
    {
        return uiLayoutManager?.GetPanelUtilization() ?? 0f;
    }

    public Vector2 GetRecommendedPanelSize()
    {
        return uiLayoutManager?.GetRecommendedPanelSize(numberOfInventorySlots) ?? Vector2.zero;
    }

    public string GetLayoutSummary()
    {
        return uiLayoutManager?.GetLayoutSummary() ?? "No layout data available";
    }

    // Input Handling
    public void OnInventory(InputAction.CallbackContext value)
    {
        if (value.started) ToggleInventory();
    }

    public void OnUseItem(InputAction.CallbackContext value)
    {
        if (!value.started || dragHandler.IsDragging) return;
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

    // Inventory Operations
    private void ToggleInventory()
    {
        if (uiStateManager.IsInventoryOpened) CloseInventory();
        else OpenInventory();
    }

    private void OpenInventory()
    {
        SetCursorState(CursorLockMode.None, true);
        uiStateManager.SetInventoryOpened(true);
    }

    private void CloseInventory()
    {
        SetCursorState(CursorLockMode.Locked, false);
        uiStateManager.SetInventoryOpened(false);
        storageManager.CloseCurrentStorage();
    }

    private void SetCursorState(CursorLockMode lockMode, bool visible)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = visible;
    }

    private InventorySlot GetSelectedHotbarSlot()
    {
        var hotbarSlots = slotManager.HotbarSlots;
        if (hotbarSlots == null || hotbarSlots.Length == 0 || HotbarHandler.SelectedHotbarSlot >= hotbarSlots.Length)
            return null;
        return hotbarSlots[HotbarHandler.SelectedHotbarSlot]?.GetComponent<InventorySlot>();
    }

    // Pointer Event Handling
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            HideItemInfo();
            return;
        }
        var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventorySlot>();
        if (itemInfo.gameObject.activeSelf) HideItemInfo();
        else ShowItemInfo(slot);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventorySlot>();
        if (slot?.heldItem != null)
        {
            dragHandler.StartDragging(slot, eventData.pointerCurrentRaycast.gameObject);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragHandler.ValidateDragOperation(eventData)) return;
        HandleItemDrop(eventData);
        dragHandler.CleanupDragging();
    }

    // Item Management
    private void HandleItemDrop(PointerEventData eventData)
    {
        var clickedObject = eventData.pointerCurrentRaycast.gameObject;
        var slot = clickedObject?.GetComponent<InventorySlot>();
        var draggedItem = dragHandler.DraggedObject.GetComponent<InventoryItem>();
        var itemType = draggedItem.itemScriptableObject.ItemType;
        if (slot != null && InventoryUtils.IsCompatibleSlot(slot, itemType))
        {
            if (InventoryUtils.IsSlotEmpty(slot))
                ItemHandler.PlaceItemInSlot(slot, dragHandler.DraggedObject, playerStatusController);
            else
                ItemHandler.SwitchOrFillStack(slot, dragHandler.DraggedObject, dragHandler.LastItemSlotObject, playerStatusController);
        }
        else if (clickedObject?.name == "DropItem")
        {
            ItemHandler.DropItem(dragHandler.DraggedObject, dragHandler.LastItemSlotObject, playerStatusController, cam, player);
        }
        else
        {
            ItemHandler.ReturnItemToLastSlot(dragHandler.LastItemSlotObject, dragHandler.DraggedObject);
        }
        HotbarHandler.HotbarItemChanged(slotManager.HotbarSlots, handParent);
    }

    private void ShowItemInfo(InventorySlot slot)
    {
        if (slot?.heldItem != null)
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
        if (newItem == null || emptySlot == null)
        {
            Debug.LogError("Cannot setup item: newItem or emptySlot is null");
            return;
        }
        var slotComponent = emptySlot.GetComponent<InventorySlot>();
        if (slotComponent == null)
        {
            Debug.LogError("EmptySlot does not have InventorySlot component");
            return;
        }
        var itemComponent = newItem.GetComponent<InventoryItem>();
        if (itemComponent?.itemScriptableObject == null)
        {
            Debug.LogError("NewItem does not have valid InventoryItem component");
            return;
        }
        // Calculate weight
        var totalWeight = itemComponent.itemScriptableObject.Weight * itemComponent.stackCurrent;
        itemComponent.totalWeight = totalWeight;
        // Update player weight
        InventoryUtils.UpdatePlayerWeight(player, totalWeight);
        // Use the slot's SetHeldItem method
        slotComponent.SetHeldItem(newItem);
    }

    // Storage Management
    public void OpenStorage(Storage storage)
    {
        SetCursorState(CursorLockMode.None, true);
        uiStateManager.SetStorageOpened(true);
        storageManager.OpenStorage(storage);
    }

    public void CloseStorage(Storage storage)
    {
        storageManager.CloseStorage(storage);
        SetCursorState(CursorLockMode.Locked, false);
        uiStateManager.SetStorageOpened(false);
    }

    // Public Utility Methods
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

    // Debug Methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogInventoryState()
    {
        InventoryUtils.LogInventoryState(slotManager.InventorySlots, $"Hotbar: {numberOfHotBarSlots}, Inventory: {numberOfInventorySlots}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogLayoutInfo()
    {
        uiLayoutManager?.LogLayoutInfo();
        Debug.Log($"Current Layout Summary: {GetLayoutSummary()}");
        Debug.Log($"Panel Utilization: {GetPanelUtilization():P1}");
        Debug.Log($"Layout Optimal: {IsLayoutOptimal()}");
    }
}