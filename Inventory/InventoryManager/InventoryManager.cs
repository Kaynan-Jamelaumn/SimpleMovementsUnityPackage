using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    // Dynamic slot configuration
    [Header("Dynamic Slot Configuration")]
    [SerializeField] private int numberOfHotBarSlots = 4;
    [SerializeField] private int numberOfInventorySlots = 20;

    // Core inventory configuration
    [SerializeField] private GameObject itemPrefab;

    // Slot Manager
    [Header("Slot Manager")]
    [SerializeField] private SlotManager slotManager;

    // UI References
    [SerializeField] private GameObject inventoryParent;
    [SerializeField] private GameObject equippableInventory;
    [SerializeField] private GameObject storageParent;
    [SerializeField] private Transform handParent;
    [SerializeField] private ItemInfo itemInfo;
    [SerializeField] private Camera cam;

    // Player References
    [SerializeField] private GameObject player;
    [SerializeField] private PlayerStatusController playerStatusController;
    [SerializeField] private WeaponController weaponController;

    // State management
    private DragHandler dragHandler;
    private UIStateManager uiStateManager;
    private StorageManager storageManager;

    // Input
    private Mouse mouse;

    // Previous slot counts for change detection
    private int previousHotbarSlots;
    private int previousInventorySlots;

    // Properties
    public Transform HandParent => handParent;
    public GameObject[] Slots => slotManager?.InventorySlots;
    public GameObject[] HotbarSlots => slotManager?.HotbarSlots;
    public GameObject Player => player;
    public bool IsStorageOpened => storageManager.IsStorageOpened;
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
        InitializeSlotManager();
        HotbarHandler.HotbarItemChanged(slotManager.HotbarSlots, handParent);
    }

    private void Update()
    {
        HandleHotbarInput();
        uiStateManager.UpdateUI();
        dragHandler.UpdateDraggedObjectPosition();

        // Check for slot count changes in editor
        CheckForSlotCountChanges();
    }

    // Initialization
    private void InitializeComponents()
    {
        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));

        if (cam == null) cam = Camera.main;
        mouse = Mouse.current;

        // Validate required UI components
        ValidateUIComponents();
    }

    private void ValidateUIComponents()
    {
        // SlotManager will handle its own validation
        if (slotManager == null)
            Debug.LogError("SlotManager is not assigned in InventoryManager");
    }

    private void InitializeSubSystems()
    {
        dragHandler = new DragHandler(mouse);
        uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
        storageManager = new StorageManager(storageParent, itemPrefab);
    }

    private void InitializeSlotManager()
    {
        if (slotManager == null)
        {
            Debug.LogError("SlotManager is not assigned in InventoryManager!");
            return;
        }

        slotManager.Initialize(playerStatusController, player);

        // Subscribe to slot changes
        slotManager.OnSlotsChanged += OnSlotsChanged;

        // Create initial slots
        slotManager.CreateAllSlots(numberOfHotBarSlots, numberOfInventorySlots);
    }

    private void OnSlotsChanged()
    {
        // Refresh hotbar when slots change
        HotbarHandler.ForceRefresh(slotManager.HotbarSlots, handParent);
    }

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

    // Input handling
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

    // Inventory operations
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

    // Pointer event handling
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

    // Item management
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

    // Item info management
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

    // Item pickup and creation
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
        // Setup transform
        var parentTransform = emptySlot.transform.parent.parent.GetChild(2);
        newItem.transform.SetParent(parentTransform);
        newItem.transform.localScale = Vector3.one;

        // Update weight using InventoryUtils
        var itemComponent = newItem.GetComponent<InventoryItem>();
        var totalWeight = itemComponent.itemScriptableObject.Weight * itemComponent.stackCurrent;
        itemComponent.totalWeight = totalWeight;

        InventoryUtils.UpdatePlayerWeight(player, totalWeight);

        // Assign to slot
        emptySlot.GetComponent<InventorySlot>().SetHeldItem(newItem);
    }

    // Storage management
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

    // Public utility methods
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

    // Manual slot management methods for runtime use
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

    // Slot manager utility methods
    public void SetSlotSizeConstraints(float minSize, float maxSize, float spacing)
    {
        slotManager?.SetSlotSizeConstraints(minSize, maxSize, spacing);
    }

    public void RefreshGridLayout()
    {
        slotManager?.RefreshGridLayout(numberOfInventorySlots);
    }

    // Debug method
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogInventoryState()
    {
        InventoryUtils.LogInventoryState(slotManager.InventorySlots, $"Hotbar: {numberOfHotBarSlots}, Inventory: {numberOfInventorySlots}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (slotManager != null)
        {
            slotManager.OnSlotsChanged -= OnSlotsChanged;
        }
    }
}