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

    // Dragging State
    private GameObject draggedObject;
    private GameObject lastItemSlotObject;
    private bool isDragging;

    // Inventory State
    private bool isInventoryOpened;
    private bool isStorageOpened;
    private Storage lastStorage;

    // Input
    private Mouse mouse;

    // Cached UI states for performance
    private bool lastInventoryState;
    private bool lastStorageState;

    // Properties
    public Transform HandParent => handParent;
    public GameObject[] Slots => slots;
    public GameObject Player => player;
    public bool IsStorageOpened { get => isStorageOpened; set => isStorageOpened = value; }

    private void Awake()
    {
        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));

        if (cam == null) cam = Camera.main;

        InitializeUI();
        mouse = Mouse.current;
    }

    private void Start()
    {
        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
        CacheUIStates();
    }

    private void Update()
    {
        HandleHotbarInput();
        HandleUIStateChanges();
        UpdateDraggedObjectPosition();
    }


    // UI Management
    private void InitializeUI()
    {
        SetUIState(storageParent, false);
        SetUIState(inventoryParent, false);
        SetUIState(equippableInventory, false);
    }

    private void CacheUIStates()
    {
        lastInventoryState = isInventoryOpened;
        lastStorageState = isStorageOpened;
    }

    private void SetUIState(GameObject uiElement, bool active)
    {
        if (uiElement != null) uiElement.SetActive(active);
    }

    private void HandleUIStateChanges()
    {
        if (lastStorageState != isStorageOpened)
        {
            SetUIState(storageParent, isStorageOpened);
            lastStorageState = isStorageOpened;
        }

        if (lastInventoryState != isInventoryOpened)
        {
            SetUIState(inventoryParent, isInventoryOpened);
            SetUIState(equippableInventory, isInventoryOpened);
            lastInventoryState = isInventoryOpened;
        }
    }

    // Input Handling
    private void HandleHotbarInput()
    {
        HotbarHandler.CheckForHotbarInput(hotbarSlots, handParent);
    }

    private void UpdateDraggedObjectPosition()
    {
        if (draggedObject != null && mouse != null)
        {
            draggedObject.transform.position = mouse.position.ReadValue();
        }
    }

    public void OnInventory(InputAction.CallbackContext value)
    {
        if (value.started) ToggleInventory();
    }

    public void OnUseItem(InputAction.CallbackContext value)
    {
        if (!value.started || isDragging) return;

        var selectedSlot = GetSelectedHotbarSlot();
        var heldItem = ItemUsageHandler.GetHeldItem(selectedSlot);

        if (heldItem == null || !ItemUsageHandler.HandleCooldown(heldItem)) return;

        ItemUsageHandler.UseHeldItem(player, playerStatusController, weaponController, heldItem);
        ItemUsageHandler.HandleItemDurabilityAndStack(player, handParent, selectedSlot, heldItem);
    }

    // Inventory Operations
    private void ToggleInventory()
    {
        if (isInventoryOpened) CloseInventory();
        else OpenInventory();
    }

    private void OpenInventory()
    {
        SetCursorState(CursorLockMode.None, true);
        isInventoryOpened = true;
    }

    private void CloseInventory()
    {
        SetCursorState(CursorLockMode.Locked, false);
        isInventoryOpened = false;
        isStorageOpened = false;

        if (lastStorage != null) CloseStorage(lastStorage);
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
        if (slot?.heldItem != null) StartDragging(slot, eventData.pointerCurrentRaycast.gameObject);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!ValidateDragOperation(eventData)) return;

        HandleItemDrop(eventData);
        CleanupDragging();
    }

    // Dragging Operations
    private bool ValidateDragOperation(PointerEventData eventData)
    {
        if (draggedObject == null || eventData.pointerCurrentRaycast.gameObject == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            isDragging = false;
            return false;
        }
        return true;
    }

    private void StartDragging(InventorySlot slot, GameObject clickedObject)
    {
        isDragging = true;
        draggedObject = slot.heldItem;
        slot.heldItem = null;
        lastItemSlotObject = clickedObject;
    }

    private void HandleItemDrop(PointerEventData eventData)
    {
        var clickedObject = eventData.pointerCurrentRaycast.gameObject;
        var slot = clickedObject?.GetComponent<InventorySlot>();
        var itemType = draggedObject.GetComponent<InventoryItem>().itemScriptableObject.ItemType;

        if (slot != null && IsSlotCompatible(slot, itemType))
        {
            if (slot.heldItem == null)
                ItemHandler.PlaceItemInSlot(slot, draggedObject, playerStatusController);
            else
                ItemHandler.SwitchOrFillStack(slot, draggedObject, lastItemSlotObject, playerStatusController);
        }
        else if (clickedObject?.name == "DropItem")
        {
            ItemHandler.DropItem(draggedObject, lastItemSlotObject, playerStatusController, cam, player);
        }
        else
        {
            ItemHandler.ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
        }

        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
    }

    private void CleanupDragging()
    {
        draggedObject = null;
        isDragging = false;
    }

    private bool IsSlotCompatible(InventorySlot slot, ItemType itemType)
    {
        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
    }

    // Item Info Management
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

    // Item Management
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

        SetupInventoryItemData(newItemComponent, pickedItemProperties);
        return newItem;
    }

    private void SetupInventoryItemData(InventoryItem newItem, ItemPickable pickedItem)
    {
        var itemSO = pickedItem.itemScriptableObject;
        newItem.itemScriptableObject = itemSO;
        newItem.stackCurrent = pickedItem.quantity;
        newItem.stackMax = itemSO.StackMax;
        newItem.DurabilityList = pickedItem.DurabilityList;

        if (pickedItem.DurabilityList?.Count > 0)
            newItem.durability = pickedItem.DurabilityList[pickedItem.DurabilityList.Count - 1];
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

    // Storage Management
    public void OpenStorage(Storage storage)
    {
        lastStorage = storage;
        SetCursorState(CursorLockMode.None, true);
        isStorageOpened = true;

        ConfigureStorageUI(storage);
        PopulateStorageItems(storage);
    }

    public void CloseStorage(Storage storage)
    {
        SaveStorageItems(storage);
        lastStorage = null;
        SetCursorState(CursorLockMode.Locked, false);
        isStorageOpened = false;
    }

    private void ConfigureStorageUI(Storage storage)
    {
        SetupStorageSlots(storage);
        SetupStorageBackground(storage);
    }

    private void SetupStorageSlots(Storage storage)
    {
        var slotsParent = storageParent.transform.GetChild(1);

        // Deactivate all slots
        for (int i = 0; i < slotsParent.childCount; i++)
            slotsParent.GetChild(i).gameObject.SetActive(false);

        // Activate required slots
        for (int i = 0; i < storage.size; i++)
            slotsParent.GetChild(i).gameObject.SetActive(true);
    }

    private void SetupStorageBackground(Storage storage)
    {
        var backgroundTransform = storageParent.transform.GetChild(0);
        float sizeY = (float)Mathf.CeilToInt(storage.size / 4f) / 4;
        float posY = (1 - sizeY) * 230;

        backgroundTransform.localScale = new Vector2(1, sizeY);
        backgroundTransform.localPosition = new Vector2(-615, 130 + posY);
    }

    private void PopulateStorageItems(Storage storage)
    {
        ClearExistingStorageItems();

        for (int i = 0; i < storage.items.Count; i++)
        {
            var storageItem = storage.items[i];
            if (storageItem?.itemScriptableObject != null)
                CreateStorageItem(storageItem, i);
        }
    }

    private void ClearExistingStorageItems()
    {
        var itemsParent = storageParent.transform.GetChild(2);
        for (int i = itemsParent.childCount - 1; i >= 0; i--)
            Destroy(itemsParent.GetChild(i).gameObject);
    }

    private void CreateStorageItem(StorageItem storageItem, int index)
    {
        var newItem = Instantiate(itemPrefab);
        var itemComponent = newItem.GetComponent<InventoryItem>();

        itemComponent.itemScriptableObject = storageItem.itemScriptableObject;
        itemComponent.stackCurrent = storageItem.currentStack;

        var slot = storageParent.transform.GetChild(1).GetChild(index);
        newItem.transform.SetParent(slot.parent.parent.GetChild(2));
        newItem.transform.localScale = Vector3.one;

        slot.GetComponent<InventorySlot>().SetHeldItem(newItem);
    }

    private void SaveStorageItems(Storage storage)
    {
        var slotsParent = storageParent.transform.GetChild(1);
        storage.items.Clear();

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            var slot = slotsParent.GetChild(i);
            var slotComponent = slot.GetComponent<InventorySlot>();

            if (slot.gameObject.activeInHierarchy && slotComponent?.heldItem != null)
            {
                var inventoryItem = slotComponent.heldItem.GetComponent<InventoryItem>();
                storage.items.Add(new StorageItem(inventoryItem.stackCurrent, inventoryItem.itemScriptableObject));
            }
            else
            {
                storage.items.Add(new StorageItem(0, null));
            }
        }
    }
}