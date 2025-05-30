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
    [Tooltip("inventory UI parent from everything for when I open the inventory or close")][SerializeField] GameObject inventoryParent;
    [Tooltip("equippable inventory UI to be equipped when Inventory opened or closed")][SerializeField] GameObject equippableInventory;
    [SerializeField] GameObject storageParent;
    [Tooltip("the player hand transform for when the item to be spawned when equipped in hotbar")][SerializeField] Transform handParent;
    [SerializeField] public GameObject itemPrefab;
    [SerializeField] Camera cam;

    [Tooltip("the item info box")][SerializeField] ItemInfo itemInfo;

    GameObject draggedObject;
    GameObject lastItemSlotObject;
    [SerializeField] GameObject player;
    [SerializeField] PlayerStatusController playerStatusController;
    [SerializeField] WeaponController weaponController;
    [SerializeField] PlayerAnimationController animController;
    Storage lastStorage;

    bool isInventoryOpened;
    bool isDragging;

    // Add reference to Mouse for new Input System
    private Mouse mouse;

    public Transform HandParent
    {
        get => handParent;
        set => handParent = value;
    }
    public GameObject InventoryParent
    {
        get => inventoryParent;
        set => inventoryParent = value;
    }
    public GameObject StorageParent
    {
        get => storageParent;
        set => storageParent = value;
    }
    public GameObject[] Slots { get => slots; }
    public GameObject Player { get => player; }

    void Awake()
    {
        storageParent.SetActive(false);
        inventoryParent.SetActive(false);
        if (Player && !playerStatusController) playerStatusController = Player.GetComponent<PlayerStatusController>();
        if (Player && !weaponController) weaponController = Player.GetComponent<WeaponController>();
        if (Player && !animController) animController = Player.GetComponent<PlayerAnimationController>();

        // Initialize mouse reference
        mouse = Mouse.current;
    }

    void Start()
    {
        ValidateAssignments();
        HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
    }

    void Update()
    {
        HotbarHandler.CheckForHotbarInput(hotbarSlots, handParent);

        storageParent.SetActive(isStorageOpened);
        inventoryParent.SetActive(isInventoryOpened);
        equippableInventory.SetActive(isInventoryOpened);

  
        if (draggedObject != null && mouse != null)
        {
            draggedObject.transform.position = mouse.position.ReadValue();
        }
    }

    private void ValidateAssignments()
    {
        Assert.IsNotNull(playerStatusController, "PlayerStatusController is not assigned in playerStatusController.");
    }

    public void OnInventory(InputAction.CallbackContext value)
    {
        if (!value.started) return;

        if (isInventoryOpened)
        {
            Cursor.lockState = CursorLockMode.Locked;
            isInventoryOpened = false;
            isStorageOpened = false;

            if (lastStorage != null)
                CloseStorage(lastStorage);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isInventoryOpened = true;
        }
    }

    public void OnUseItem(InputAction.CallbackContext value)
    {
        if (!value.started || isDragging) return; // Prevent OnUseItem while dragging

        Debug.Log("OnUseItem called");

        var selectedSlot = hotbarSlots[HotbarHandler.SelectedHotbarSlot].GetComponent<InventorySlot>();
        if (selectedSlot == null)
        {
            Debug.LogWarning("Selected slot is null or does not have an InventorySlot component.");
            return;
        }

        var heldItem = ItemUsageHandler.GetHeldItem(selectedSlot);

        if (heldItem == null) return;
        Debug.Log($"Using item: {heldItem.itemScriptableObject.Name}");
        if (!ItemUsageHandler.HandleCooldown(heldItem)) return;

        ItemUsageHandler.UseHeldItem(Player, playerStatusController, weaponController, heldItem);

        ItemUsageHandler.HandleItemDurabilityAndStack(Player, handParent, selectedSlot, heldItem);
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

        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        InventorySlot slot = clickedObject.GetComponent<InventorySlot>();

        if (itemInfo.gameObject.activeSelf)
        {
            HideItemInfo();
            return;
        }

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
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
            InventorySlot slot = clickedObject.GetComponent<InventorySlot>();
            // There is an item in the slot - pick it up
            if (slot != null && slot.heldItem != null)
            {
                isDragging = true; // Start dragging
                draggedObject = slot.heldItem;
                slot.heldItem = null;
                lastItemSlotObject = clickedObject;
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (draggedObject != null && eventData.pointerCurrentRaycast.gameObject != null && eventData.button == PointerEventData.InputButton.Left)
        {
            GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
            InventorySlot slot = clickedObject.GetComponent<InventorySlot>();

            // Get the ItemType of the held item
            ItemType itemType = draggedObject.GetComponent<InventoryItem>().itemScriptableObject.ItemType;

            // Check if the slot is common or matches the itemType
            if (slot != null && (slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType))
            {
                // Place item if slot is empty
                if (slot.heldItem == null)
                {
                    ItemHandler.PlaceItemInSlot(slot, draggedObject, playerStatusController);
                }
                else
                {
                    // Switch items or fill stack
                    ItemHandler.SwitchOrFillStack(slot, draggedObject, lastItemSlotObject, playerStatusController);
                }
            }
            else if (clickedObject.name != "DropItem")
            {
                // Return item to last slot
                ItemHandler.ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            }
            else
            {
                // Drop item
                DropItem();
            }

            HotbarHandler.HotbarItemChanged(hotbarSlots, handParent);
            draggedObject = null;
        }

        isDragging = false; // Stop dragging
    }

    private void DropItem()
    {
        ItemHandler.DropItem(draggedObject, lastItemSlotObject, playerStatusController, cam, player);
    }

    public void ItemPicked(GameObject pickedItem)
    {
        ItemPickupHandler.AddItemToInventory(this, pickedItem, Slots, itemPrefab, Player);
    }

    public void InstantiateNewItem(GameObject emptySlot, GameObject pickedItem)
    {
        GameObject newItem = Instantiate(itemPrefab);
        InventoryItem newItemItem = newItem.GetComponent<InventoryItem>();

        ItemPickable pickedItemProperties = pickedItem.GetComponent<ItemPickable>();

        // Assign the item scriptable object and its properties
        ItemSO pickedItemSO = pickedItemProperties.itemScriptableObject;
        newItemItem.itemScriptableObject = pickedItemSO;

        // Set the new item's ,itemstack and the durability(Stack) properties
        newItemItem.stackCurrent = pickedItemProperties.quantity;
        newItemItem.stackMax = pickedItemSO.StackMax;

        newItemItem.durability = pickedItemProperties.DurabilityList[pickedItemProperties.DurabilityList.Count - 1];
        newItemItem.DurabilityList = pickedItemProperties.DurabilityList;

        //pickedItemSO.PlayerObject = player.gameObject;
        //newItemItem.itemScriptableObject.statusController = player.GetComponent<PlayerStatusController>();
        // Set the new item's parent,
        Transform parentTransform = emptySlot.transform.parent.parent.GetChild(2);
        newItem.transform.SetParent(parentTransform);

        // Update player weight and set the new item in the empty slot
        float itemWeight = newItemItem.itemScriptableObject.Weight * newItemItem.stackCurrent;
        newItemItem.totalWeight = itemWeight;
        Player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(itemWeight);
        emptySlot.GetComponent<InventorySlot>().SetHeldItem(newItem);

        // Set the new item's scale
        newItem.transform.localScale = new Vector3(1, 1, 1);
    }

    public void OpenStorage(Storage storage)
    {
        lastStorage = storage;

        Cursor.lockState = CursorLockMode.None;
        isStorageOpened = true;

        //Set all slots to inactive
        for (int i = 0; i < storageParent.transform.GetChild(1).childCount; i++)
        {
            storageParent.transform.GetChild(1).GetChild(i).gameObject.SetActive(false);
        }

        //Set some of the slots to active
        for (int i = 0; i < storage.size; i++)
        {
            storageParent.transform.GetChild(1).GetChild(i).gameObject.SetActive(true);
        }
        //Set background size and position
        float sizeY = (float)Mathf.CeilToInt(storage.size / 4f) / 4;
        storageParent.transform.GetChild(0).localScale = new Vector2(1, sizeY);

        float posY = (1 - sizeY) * 230;
        storageParent.transform.GetChild(0).localPosition = new Vector2(-615, 130 + posY);

        //Destroy all items
        for (int i = 0; i < storageParent.transform.GetChild(2).childCount; i++)
        {
            Destroy(storageParent.transform.GetChild(2).GetChild(i).gameObject);
        }

        int index = 0;
        foreach (StorageItem storageItem in storage.items)
        {
            if (storageItem.itemScriptableObject != null)
            {
                GameObject newItem = Instantiate(itemPrefab);
                InventoryItem item = newItem.GetComponent<InventoryItem>();
                item.itemScriptableObject = storageItem.itemScriptableObject;
                item.stackCurrent = storageItem.currentStack;

                Transform slot = storageParent.transform.GetChild(1).GetChild(index);
                newItem.transform.SetParent(slot.parent.parent.GetChild(2));
                slot.GetComponent<InventorySlot>().SetHeldItem(newItem);
                newItem.transform.localScale = new Vector3(1, 1, 1);
            }
            index++;
        }
    }

    public void CloseStorage(Storage storage)
    {
        lastStorage = null;
        Cursor.lockState = CursorLockMode.Locked;
        isStorageOpened = false;

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