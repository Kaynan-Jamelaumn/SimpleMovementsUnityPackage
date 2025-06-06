using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    // Core inventory configuration
    [SerializeField] private GameObject[] hotbarSlots = new GameObject[4];
    [SerializeField] private GameObject[] slots = new GameObject[20];
    [SerializeField] private GameObject itemPrefab;

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

    // Properties
    public Transform HandParent => handParent;
    public GameObject[] Slots => slots;
    public GameObject Player => player;
    public bool IsStorageOpened => storageManager.IsStorageOpened;

    private void Awake()
    {
        InitializeComponents();
        InitializeSubSystems();
    }

    private void Start()
    {
        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
    }

    private void Update()
    {
        HandleHotbarInput();
        uiStateManager.UpdateUI();
        dragHandler.UpdateDraggedObjectPosition();
    }

    // Initialization
    private void InitializeComponents()
    {
        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));

        if (cam == null) cam = Camera.main;
        mouse = Mouse.current;
    }

    private void InitializeSubSystems()
    {
        dragHandler = new DragHandler(mouse);
        uiStateManager = new UIStateManager(inventoryParent, equippableInventory, storageParent);
        storageManager = new StorageManager(storageParent, itemPrefab);
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
        HotbarHandler.CheckForHotbarInput(hotbarSlots, handParent);
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
        if (hotbarSlots == null || HotbarHandler.SelectedHotbarSlot >= hotbarSlots.Length)
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

        if (slot != null && IsSlotCompatible(slot, itemType))
        {
            if (slot.heldItem == null)
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

        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
    }

    private bool IsSlotCompatible(InventorySlot slot, ItemType itemType)
    {
        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
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
        ItemPickUpHandler.AddItemToInventory(this, pickedItem, slots, itemPrefab, player);
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

        // Update weight
        var itemComponent = newItem.GetComponent<InventoryItem>();
        var totalWeight = itemComponent.itemScriptableObject.Weight * itemComponent.stackCurrent;
        itemComponent.totalWeight = totalWeight;

        playerStatusController?.WeightManager?.AddWeight(totalWeight);

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
}

