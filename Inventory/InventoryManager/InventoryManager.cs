using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [HideInInspector] public bool isStorageOpened;

    [SerializeField] GameObject[] hotbarSlots = new GameObject[4];
    [SerializeField] GameObject[] slots = new GameObject[20];

    [Tooltip("Inventory UI parent for when I open the inventory or close")]
    [SerializeField] GameObject inventoryParent;

    [Tooltip("Equippable inventory UI to be equipped when Inventory opened or closed")]
    [SerializeField] GameObject equippableInventory;

    [SerializeField] GameObject storageParent;

    [Tooltip("The player hand transform for when the item to be spawned when equipped in hotbar")]
    [SerializeField] Transform handParent;

    [SerializeField] public GameObject itemPrefab;
    [SerializeField] Camera cam;

    [Tooltip("The item info box")]
    [SerializeField] ItemInfo itemInfo;

    // Dragging state
    GameObject draggedObject;
    GameObject lastItemSlotObject;
    bool isDragging;

    // Player references - cached for performance
    [SerializeField] GameObject player;
    [SerializeField] PlayerStatusController playerStatusController;
    [SerializeField] WeaponController weaponController;
    [SerializeField] PlayerAnimationController animController;

    // Inventory state
    bool isInventoryOpened;
    Storage lastStorage;

    // Input references
    private Mouse mouse;

    // Cached UI state to avoid redundant updates
    private bool lastInventoryState;
    private bool lastStorageState;

    // Properties
    public Transform HandParent { get => handParent; set => handParent = value; }
    public GameObject InventoryParent { get => inventoryParent; set => inventoryParent = value; }
    public GameObject StorageParent { get => storageParent; set => storageParent = value; }
    public GameObject[] Slots { get => slots; }
    public GameObject Player { get => player; }

    void Awake()
    {
        InitializeUI();

        playerStatusController = this.CheckComponent(playerStatusController, nameof(playerStatusController));
        weaponController = this.CheckComponent(weaponController, nameof(weaponController));
        animController = this.CheckComponent(animController, nameof(animController));

        mouse = Mouse.current;
    }

    void Start()
    {

        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);

        // Initialize cached states
        lastInventoryState = isInventoryOpened;
        lastStorageState = isStorageOpened;
    }

    void Update()
    {
        HandleHotbarInput();
        HandleUIStateChanges();
        HandleDraggedObjectPosition();
    }

    private void InitializeUI()
    {
        storageParent.SetActive(false);
        inventoryParent.SetActive(false);
    }



    private void HandleHotbarInput()
    {
        HotbarHandler.CheckForHotbarInput(hotbarSlots, handParent);
    }

    private void HandleUIStateChanges()
    {
        // Only update UI when state actually changes to avoid redundant operations
        if (lastStorageState != isStorageOpened)
        {
            storageParent.SetActive(isStorageOpened);
            lastStorageState = isStorageOpened;
        }

        if (lastInventoryState != isInventoryOpened)
        {
            inventoryParent.SetActive(isInventoryOpened);
            equippableInventory.SetActive(isInventoryOpened);
            lastInventoryState = isInventoryOpened;
        }
    }

    private void HandleDraggedObjectPosition()
    {
        if (draggedObject != null && mouse != null)
        {
            draggedObject.transform.position = mouse.position.ReadValue();
        }
    }


    public void OnInventory(InputAction.CallbackContext value)
    {
        if (!value.started) return;

        ToggleInventory();
    }

    private void ToggleInventory()
    {
        if (isInventoryOpened)
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isInventoryOpened = true;
    }

    private void CloseInventory()
    {
        Cursor.lockState = CursorLockMode.Locked;
        isInventoryOpened = false;
        isStorageOpened = false;

        if (lastStorage != null)
            CloseStorage(lastStorage);
    }

    public void OnUseItem(InputAction.CallbackContext value)
    {
        if (!value.started || isDragging) return; // Prevent OnUseItem while dragging

        Debug.Log("OnUseItem called");

        var selectedSlot = GetSelectedHotbarSlot();
        if (selectedSlot == null) return;

        var heldItem = ItemUsageHandler.GetHeldItem(selectedSlot);
        if (heldItem == null) return;

        Debug.Log($"Using item: {heldItem.itemScriptableObject.Name}");

        if (!ItemUsageHandler.HandleCooldown(heldItem)) return;

        ItemUsageHandler.UseHeldItem(Player, playerStatusController, weaponController, heldItem);
        ItemUsageHandler.HandleItemDurabilityAndStack(Player, handParent, selectedSlot, heldItem);
    }

    private InventorySlot GetSelectedHotbarSlot()
    {
        var selectedSlot = hotbarSlots[HotbarHandler.SelectedHotbarSlot].GetComponent<InventorySlot>();
        if (selectedSlot == null)
        {
            Debug.LogWarning("Selected slot is null or does not have an InventorySlot component.");
        }
        return selectedSlot;
    }

    public void InstantiateClassItems(List<GameObject> classItems)
    {
        foreach (GameObject item in classItems)
            ItemPicked(item);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            HideItemInfo();
            return;
        }

        if (itemInfo.gameObject.activeSelf)
        {
            HideItemInfo();
            return;
        }

        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        InventorySlot slot = clickedObject.GetComponent<InventorySlot>();
        ShowItemInfo(slot);
    }

    private void ShowItemInfo(InventorySlot slot)
    {
        if (slot != null && slot.heldItem != null)
        {
            itemInfo.ShowItemInfo(slot.heldItem.GetComponent<InventoryItem>());
        }
        else
        {
            HideItemInfo();
        }
    }

    private void HideItemInfo()
    {
        itemInfo.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        InventorySlot slot = clickedObject.GetComponent<InventorySlot>();

        // There is an item in the slot - pick it up
        if (slot != null && slot.heldItem != null)
        {
            StartDragging(slot, clickedObject);
        }
    }

    private void StartDragging(InventorySlot slot, GameObject clickedObject)
    {
        isDragging = true;
        draggedObject = slot.heldItem;
        slot.heldItem = null;
        lastItemSlotObject = clickedObject;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (draggedObject == null || eventData.pointerCurrentRaycast.gameObject == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            isDragging = false;
            return;
        }

        HandleItemDrop(eventData);
        CleanupDragging();
    }

    private void HandleItemDrop(PointerEventData eventData)
    {
        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        InventorySlot slot = clickedObject.GetComponent<InventorySlot>();

        // Get the ItemType of the held item
        ItemType itemType = draggedObject.GetComponent<InventoryItem>().itemScriptableObject.ItemType;

        // Check if the slot is compatible
        if (slot != null && IsSlotCompatible(slot, itemType))
        {
            if (slot.heldItem == null)
            {
                ItemHandler.PlaceItemInSlot(slot, draggedObject, playerStatusController);
            }
            else
            {
                ItemHandler.SwitchOrFillStack(slot, draggedObject, lastItemSlotObject, playerStatusController);
            }
        }
        else if (clickedObject.name == "DropItem")
        {
            DropItem();
        }
        else
        {
            ItemHandler.ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
        }

        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
    }

    private bool IsSlotCompatible(InventorySlot slot, ItemType itemType)
    {
        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
    }

    private void CleanupDragging()
    {
        draggedObject = null;
        isDragging = false;
    }

    private void DropItem()
    {
        ItemHandler.DropItem(draggedObject, lastItemSlotObject, playerStatusController, cam, player);
    }

    public void ItemPicked(GameObject pickedItem)
    {
        ItemPickUpHandler.AddItemToInventory(this, pickedItem, Slots, itemPrefab, Player);
    }

    public void InstantiateNewItem(GameObject emptySlot, GameObject pickedItem)
    {
        GameObject newItem = Instantiate(itemPrefab);
        InventoryItem newItemComponent = newItem.GetComponent<InventoryItem>();
        ItemPickable pickedItemProperties = pickedItem.GetComponent<ItemPickable>();

        SetupNewInventoryItem(newItemComponent, pickedItemProperties);
        SetupItemTransform(newItem, emptySlot);
        UpdatePlayerWeight(newItemComponent);
        AssignItemToSlot(emptySlot, newItem);
    }

    private void SetupNewInventoryItem(InventoryItem newItemComponent, ItemPickable pickedItemProperties)
    {
        ItemSO pickedItemSO = pickedItemProperties.itemScriptableObject;
        newItemComponent.itemScriptableObject = pickedItemSO;
        newItemComponent.stackCurrent = pickedItemProperties.quantity;
        newItemComponent.stackMax = pickedItemSO.StackMax;
        newItemComponent.durability = pickedItemProperties.DurabilityList[pickedItemProperties.DurabilityList.Count - 1];
        newItemComponent.DurabilityList = pickedItemProperties.DurabilityList;
    }

    private void SetupItemTransform(GameObject newItem, GameObject emptySlot)
    {
        Transform parentTransform = emptySlot.transform.parent.parent.GetChild(2);
        newItem.transform.SetParent(parentTransform);
        newItem.transform.localScale = Vector3.one;
    }

    private void UpdatePlayerWeight(InventoryItem newItemComponent)
    {
        float itemWeight = newItemComponent.itemScriptableObject.Weight * newItemComponent.stackCurrent;
        newItemComponent.totalWeight = itemWeight;
        Player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(itemWeight);
    }

    private void AssignItemToSlot(GameObject emptySlot, GameObject newItem)
    {
        emptySlot.GetComponent<InventorySlot>().SetHeldItem(newItem);
    }

    public void OpenStorage(Storage storage)
    {
        lastStorage = storage;
        Cursor.lockState = CursorLockMode.None;
        isStorageOpened = true;

        SetupStorageSlots(storage);
        SetupStorageBackground(storage);
        PopulateStorageItems(storage);
    }

    private void SetupStorageSlots(Storage storage)
    {
        Transform slotsParent = storageParent.transform.GetChild(1);

        // Set all slots to inactive
        for (int i = 0; i < slotsParent.childCount; i++)
        {
            slotsParent.GetChild(i).gameObject.SetActive(false);
        }

        // Set some of the slots to active
        for (int i = 0; i < storage.size; i++)
        {
            slotsParent.GetChild(i).gameObject.SetActive(true);
        }
    }

    private void SetupStorageBackground(Storage storage)
    {
        float sizeY = (float)Mathf.CeilToInt(storage.size / 4f) / 4;
        storageParent.transform.GetChild(0).localScale = new Vector2(1, sizeY);

        float posY = (1 - sizeY) * 230;
        storageParent.transform.GetChild(0).localPosition = new Vector2(-615, 130 + posY);
    }

    private void PopulateStorageItems(Storage storage)
    {
        // Destroy all existing items
        Transform itemsParent = storageParent.transform.GetChild(2);
        for (int i = 0; i < itemsParent.childCount; i++)
        {
            Destroy(itemsParent.GetChild(i).gameObject);
        }

        // Create new items
        int index = 0;
        foreach (StorageItem storageItem in storage.items)
        {
            if (storageItem.itemScriptableObject != null)
            {
                CreateStorageItem(storageItem, index);
            }
            index++;
        }
    }

    private void CreateStorageItem(StorageItem storageItem, int index)
    {
        GameObject newItem = Instantiate(itemPrefab);
        InventoryItem item = newItem.GetComponent<InventoryItem>();
        item.itemScriptableObject = storageItem.itemScriptableObject;
        item.stackCurrent = storageItem.currentStack;

        Transform slot = storageParent.transform.GetChild(1).GetChild(index);
        newItem.transform.SetParent(slot.parent.parent.GetChild(2));
        slot.GetComponent<InventorySlot>().SetHeldItem(newItem);
        newItem.transform.localScale = Vector3.one;
    }

    public void CloseStorage(Storage storage)
    {
        lastStorage = null;
        Cursor.lockState = CursorLockMode.Locked;
        isStorageOpened = false;

        SaveStorageItems(storage);
    }

    private void SaveStorageItems(Storage storage)
    {
        Transform slotsParent = storageParent.transform.GetChild(1);
        storage.items.Clear();

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            Transform slot = slotsParent.GetChild(i);

            if (slot.gameObject.activeInHierarchy && slot.GetComponent<InventorySlot>().heldItem != null)
            {
                InventoryItem inventoryItem = slot.GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>();
                storage.items.Add(new StorageItem(inventoryItem.stackCurrent, inventoryItem.itemScriptableObject));
            }
            else
            {
                storage.items.Add(new StorageItem(0, null));
            }
        }
    }
}