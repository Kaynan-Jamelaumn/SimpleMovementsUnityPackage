using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static UnityEditor.Timeline.Actions.MenuPriority;


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
    PlayerStatusController playerStatusController;
    WeaponController weaponController;
    PlayerAnimationController animController;
    Storage lastStorage;

    bool isInventoryOpened;

    int selectedHotbarSlot = 0;

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
    void Awake()
    {
        storageParent.SetActive(false);
        inventoryParent.SetActive(false);
        if (player) playerStatusController = player.GetComponent<PlayerStatusController>();
        if (player) weaponController = player.GetComponent<WeaponController>();
        if (player) animController =  player.GetComponent<PlayerAnimationController>();
    }

    void Start()
    {
        ValidateAssignments();
        HotbarItemChanged();
        
    }
    void Update()
    {

        CheckForHotbarInput();

        storageParent.SetActive(isStorageOpened);
        inventoryParent.SetActive(isInventoryOpened);
        equippableInventory.SetActive(isInventoryOpened);

        //Move item
        if (draggedObject != null)
        {
            draggedObject.transform.position = Input.mousePosition;
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
            isInventoryOpened = true;
        }
    }
    public void OnUseItem(InputAction.CallbackContext value)
    {
        // Exit early if the action is not started
        if (!value.started) return;

        // Get the selected inventory slot
        var selectedSlot = hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>();

        // Validate the held item
        var heldItem = GetHeldItem(selectedSlot);
        if (heldItem == null) return;

        // Check and update cooldown
        if (!HandleCooldown(heldItem)) return;

        // Use the item
        UseHeldItem(heldItem);

        // Handle item durability and stack
        HandleItemDurabilityAndStack(selectedSlot, heldItem);
    }

    private InventoryItem GetHeldItem(InventorySlot selectedSlot)
    {
        // Exit if there is no held item or if the held item does not have an InventoryItem component
        return selectedSlot.heldItem?.GetComponent<InventoryItem>();
    }

    private bool HandleCooldown(InventoryItem heldItem)
    {
        // Exit if the item is still on cooldown
        if (Time.time < heldItem.timeSinceLastUse) return false;

        // Update cooldown if applicable
        var cooldown = heldItem.itemScriptableObject.Cooldown;
        if (cooldown != 0)
        {
            heldItem.timeSinceLastUse = Time.time + cooldown;
        }
        return true;
    }

    private void UseHeldItem(InventoryItem heldItem)
    {
        // Use the item based on its type
        if (heldItem.itemScriptableObject is WeaponSO)
        {
            heldItem.itemScriptableObject.UseItem(player, playerStatusController, weaponController);
        }
        else
        {
            heldItem.itemScriptableObject.UseItem(player, playerStatusController);
        }
    }

    private void HandleItemDurabilityAndStack(InventorySlot selectedSlot, InventoryItem heldItem)
    {
        // Check if the item is a consumable or should be destroyed when out of uses
        if (heldItem.itemScriptableObject is ConsumableSO || heldItem.itemScriptableObject.ShouldBeDestroyedOn0UsesLeft)
        {
            ProcessItemDurability(heldItem);

            if (heldItem.stackCurrent <= 0)
            {
                DestroyHeldItem(selectedSlot, heldItem);
            }
        }
    }

    private void ProcessItemDurability(InventoryItem heldItem)
    {
        if (heldItem.DurabilityList.Count > 0 && heldItem.DurabilityList[^1] <= 0)
        {
            DecreaseItemStack(heldItem);
            UpdateItemDurability(heldItem);
        }
    }

    private void DecreaseItemStack(InventoryItem heldItem)
    {
        // Reduce stack count and update player weight
        heldItem.stackCurrent--;
        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(heldItem.itemScriptableObject.Weight);
    }

    private void UpdateItemDurability(InventoryItem heldItem)
    {
        // Update durability and remove the used durability entry
        heldItem.durability = heldItem.DurabilityList[^1];
        heldItem.DurabilityList.RemoveAt(heldItem.DurabilityList.Count - 1);
    }


    private void DestroyHeldItem(InventorySlot selectedSlot, InventoryItem heldItem)
    {
        // Remove the item from the slot and destroy the GameObject
        selectedSlot.heldItem = null;
        Destroy(heldItem.gameObject);

        // Clear all children of the handParent
        for (int i = handParent.childCount - 1; i >= 0; i--)
        {
            Destroy(handParent.GetChild(i).gameObject);
        }
    }
    public void InstantiateClassItems(List<GameObject> classItems)
    {
        foreach (GameObject item in classItems)
            ItemPicked(item);
    }



    private void CheckForHotbarInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            selectedHotbarSlot = 0;
            HotbarItemChanged();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            selectedHotbarSlot = 1;
            HotbarItemChanged();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            selectedHotbarSlot = 2;
            HotbarItemChanged();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            selectedHotbarSlot = 3;
            HotbarItemChanged();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            selectedHotbarSlot = 4;
            HotbarItemChanged();
        }
    }

    private void HotbarItemChanged()
    {
        foreach (GameObject slot in hotbarSlots)
        {
            Vector3 scale;

            if (slot == hotbarSlots[selectedHotbarSlot])
            {
                scale = new Vector3(1.25f, 1.25f, 1.25f);

                // Delete every child inside handparent
                for (int i = 0; i < handParent.childCount; i++)
                {
                    Destroy(handParent.GetChild(i).gameObject);
                }

                if (slot.GetComponent<InventorySlot>().heldItem != null)
                {

                    // Instantiate the prefab of the item in the hand
                    GameObject newItem = Instantiate(hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>().itemScriptableObject.Prefab);


                    //hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>().itemScriptableObject)

                    Rigidbody rb = newItem.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Destroy(rb);
                    }

                    // Remove ItemPickable component
                    ItemPickable itemPickable = newItem.GetComponent<ItemPickable>();
                    if (itemPickable != null)
                    {
                        Destroy(itemPickable);
                    }


                    newItem.transform.parent = handParent;
                    newItem.transform.localPosition = hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>().itemScriptableObject.Position;
                    newItem.transform.localRotation = Quaternion.Euler(hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>().itemScriptableObject.Rotation);
                    newItem.transform.localScale = hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>().itemScriptableObject.Scale;              
                }
            }
            else
            {
                scale = new Vector3(1f, 1f, 1f);
            }

            slot.transform.localScale = scale;
        }
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
            //There is item in the slot - pick it up
            if (slot != null && slot.heldItem != null)
            {
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
                    PlaceItemInSlot(slot);
                }
                else
                {
                    // Switch items or fill stack
                    SwitchOrFillStack(slot);
                }
            }
            else if (clickedObject.name != "DropItem")
            {
                // Return item to last slot
                ReturnItemToLastSlot();
            }
            else
            {
                // Drop item
                DropItem();
            }

            HotbarItemChanged();
            draggedObject = null;
        }
    }

    private void PlaceItemInSlot(InventorySlot slot)
    {
        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));

        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();
        if (slot.SlotType != SlotType.Common && slot.SlotType == (SlotType)draggedItem.itemScriptableObject.ItemType && draggedItem.isEquipped == false)
        {
            draggedItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
            draggedItem.isEquipped = true;
        }
        else if (draggedItem.isEquipped ==true && slot.SlotType == SlotType.Common && slot.SlotType != (SlotType)draggedItem.itemScriptableObject.ItemType)
        {
            draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
            draggedItem.isEquipped = false;
        }
    }

    private void SwitchOrFillStack(InventorySlot slot)
    {
        InventoryItem slotHeldItem = slot.heldItem.GetComponent<InventoryItem>();
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();

        // Switch items if stack is full or different items
        if (slot.heldItem != null && (slotHeldItem.stackCurrent == slotHeldItem.stackMax || slotHeldItem.itemScriptableObject != draggedItem.itemScriptableObject))
        {
            SwitchItems(slot);
        }
        else if (slotHeldItem.stackCurrent < slotHeldItem.stackMax && slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject)
        {
            // Fill stack
            FillStack(slot, slotHeldItem, draggedItem);
        }
    }

    private void SwitchItems(InventorySlot slot)
    {
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();
        InventorySlot lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        InventoryItem currentInventoryItemItem = slot.heldItem.GetComponent<InventoryItem>();
        if (currentInventoryItemItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType && slot.SlotType != SlotType.Common
            || lastSlot.SlotType != SlotType.Common && currentInventoryItemItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType)
        {
            ReturnItemToLastSlot();
            return;
        }
        if (slot.SlotType != SlotType.Common || lastSlot.SlotType != SlotType.Common && draggedItem.itemScriptableObject.ItemType == currentInventoryItemItem.itemScriptableObject.ItemType)
        {
            if (slot.SlotType != SlotType.Common)
            {

            currentInventoryItemItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
            currentInventoryItemItem.isEquipped = false;
            draggedItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
            draggedItem.isEquipped = true;
            }
            else
            {
                currentInventoryItemItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
                currentInventoryItemItem.isEquipped = true;
                draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
                draggedItem.isEquipped = false;
            }

        }

        lastSlot.SetHeldItem(slot.heldItem);
        slot.heldItem.transform.SetParent(slot.transform.parent.parent.GetChild(2));

        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));
    }

    private void FillStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        int itemsToFillStack = slotHeldItem.stackMax - slotHeldItem.stackCurrent;

        if (itemsToFillStack >= draggedItem.stackCurrent)
        {
            // Fill the entire stack
            FillEntireStack(slot, slotHeldItem, draggedItem);
        }
        else
        {
            // Fill part of the stack
            FillPartialStack(slot, slotHeldItem, draggedItem, itemsToFillStack);
        }
    }

    private void FillEntireStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.totalWeight += draggedItem.totalWeight;

        Destroy(draggedObject);
    }

    private void FillPartialStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack)
    {
        for (int j = 0; j < itemsToFillStack; j++)
        {
            slotHeldItem.DurabilityList.Add(draggedItem.DurabilityList[draggedItem.DurabilityList.Count - 1]);
            draggedItem.DurabilityList.RemoveAt(draggedItem.DurabilityList.Count - 1);
        }

        slotHeldItem.totalWeight += draggedItem.itemScriptableObject.Weight * itemsToFillStack;
        draggedItem.totalWeight -= draggedItem.itemScriptableObject.Weight * itemsToFillStack;

        slotHeldItem.stackCurrent += itemsToFillStack;
        draggedItem.stackCurrent -= itemsToFillStack;

        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedObject);
    }

    private void ReturnItemToLastSlot()
    {
        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(lastItemSlotObject.transform);
    }

    private void DropItem()
    {
        InventorySlot lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (lastSlot.SlotType != SlotType.Common)
        {
            draggedItem.isEquipped = false;
            draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
        }

        if (!cam) cam = Camera.main;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 position = ray.GetPoint(3);

        GameObject newItem = Instantiate(draggedItem.itemScriptableObject.Prefab, position, Quaternion.identity);

        ItemPickable itemPickableComponent = newItem.GetComponent<ItemPickable>();
        itemPickableComponent.itemScriptableObject = draggedItem.itemScriptableObject;
        itemPickableComponent.quantity = draggedItem.stackCurrent;
        itemPickableComponent.DurabilityList = draggedItem.DurabilityList;
        itemPickableComponent.InteractionTime = draggedItem.itemScriptableObject.PickUpTime;

        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(itemPickableComponent.itemScriptableObject.Weight * itemPickableComponent.quantity);

        lastSlot.heldItem = null;
        Destroy(draggedObject);
    }

    public void ItemPicked(GameObject pickedItem)
    {
        AddItemToInventory(pickedItem);

    }

    public void SplitItemIntoNewStack(InventoryItem pickedItem)
    {
        GameObject emptySlot = FindEmptySlot();

        if (emptySlot != null)
        {
            // Calculate the quantity to transfer
            if (pickedItem.stackCurrent <= 1)
            {
                return; // No need to split if the stack is 1 or less
            }

            int quantity = pickedItem.stackCurrent / 2;

            // Create a new empty GameObject with InventoryItem component
            GameObject emptyGameObject = new GameObject("EmptyGameObject");
            ItemPickable newItem = emptyGameObject.AddComponent<ItemPickable>();

            // Create a list to store the transferred durability values
            List<int> durabilityListToTransfer = new List<int>();

            // Transfer durability values from the end of the list
            for (int j = 0; j < quantity; j++)
            {
                durabilityListToTransfer.Add(pickedItem.DurabilityList[pickedItem.DurabilityList.Count - 1]);
                pickedItem.DurabilityList.RemoveAt(pickedItem.DurabilityList.Count - 1);
            }


            // Update the quantities and weights for the original and new items
            pickedItem.stackCurrent -= quantity;
            player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(quantity * pickedItem.itemScriptableObject.Weight);
            pickedItem.totalWeight = quantity * pickedItem.itemScriptableObject.Weight;

            // Assign the transferred durability values to the new item
            newItem.DurabilityList = durabilityListToTransfer;

            newItem.itemScriptableObject = pickedItem.itemScriptableObject;
            newItem.quantity = quantity;
            // Instantiate the new item in the empty slot
            InstantiateNewItem(emptySlot, emptyGameObject);
            Destroy(emptyGameObject);
        }
    }



    private GameObject FindEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].GetComponent<InventorySlot>().heldItem == null && slots[i].GetComponent<InventorySlot>().SlotType == SlotType.Common)
            {
                return slots[i];
            }
        }

        return null;
    }
    private void AddItemToInventory(GameObject pickedItem)
    {
        GameObject emptySlot = null;

        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            if (slots[slotIndex].GetComponent<InventorySlot>().SlotType != SlotType.Common) continue;
            InventorySlot currentSlot = slots[slotIndex].GetComponent<InventorySlot>();
            if (currentSlot.heldItem != null)
            {
                InventoryItem currentItem = currentSlot.heldItem.GetComponent<InventoryItem>();
                ItemPickable pickedItemProperties = pickedItem.GetComponentInParent<ItemPickable>();
                ItemSO pickedItemSO = pickedItem.GetComponentInParent<ItemPickable>().itemScriptableObject;

                if (currentItem != null && pickedItemSO == currentItem.itemScriptableObject)
                {
                    if (pickedItemProperties.quantity + currentItem.stackCurrent <= currentItem.stackMax)
                    {
                        currentItem.totalWeight += pickedItemProperties.quantity * pickedItemSO.Weight;
                        UpdateInventoryOnItemPick(currentSlot, pickedItemProperties.quantity, pickedItemProperties.DurabilityList);
                        Destroy(pickedItem);
                        return;
                    }
                    else
                    {
                        int remainingQuantity = pickedItemProperties.quantity - (currentItem.stackMax - currentItem.stackCurrent);
                        List<int> durabilityListToTransfer = new List<int>();
                        int quantityToBeAddedToTheFoundItem = pickedItemProperties.quantity - remainingQuantity;

                        durabilityListToTransfer.AddRange(pickedItemProperties.DurabilityList.GetRange(pickedItemProperties.DurabilityList.Count - quantityToBeAddedToTheFoundItem, quantityToBeAddedToTheFoundItem));
                        pickedItemProperties.DurabilityList.RemoveRange(pickedItemProperties.DurabilityList.Count - quantityToBeAddedToTheFoundItem, quantityToBeAddedToTheFoundItem);

                        currentItem.totalWeight += quantityToBeAddedToTheFoundItem * pickedItemSO.Weight;
                        UpdateInventoryOnItemPick(currentSlot, quantityToBeAddedToTheFoundItem, durabilityListToTransfer);
                        pickedItemProperties.quantity = remainingQuantity;
                        if (remainingQuantity <= 0) return;
                    }
                }
            }
            else if (currentSlot.heldItem == null)
            {
                emptySlot = slots[slotIndex];
                break;
            }
        }

        if (emptySlot != null)
        {
            InstantiateNewItem(emptySlot, pickedItem);
            if (pickedItem.scene.IsValid())
                // It's an instantiated object in the scene
                Destroy(pickedItem); // This will safely destroy the object
            
        }
    }

    private void UpdateInventoryOnItemPick(InventorySlot slot, int quantityToAdd, List<int> durabilityList)
    {
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();
        currentItem.stackCurrent += quantityToAdd;

        foreach (int durability in durabilityList)
        {
            currentItem.DurabilityList.Add(durability);
        }

        player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(currentItem.itemScriptableObject.Weight * quantityToAdd);
    }

    private void InstantiateNewItem(GameObject emptySlot, GameObject pickedItem)
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
        player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(itemWeight);
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

//public void onInventory(InputAction.CallbackContext value)
//{

//    
//    if (!value.started) return;

//    if (isInventoryOpened)
//    {
//        Cursor.lockState = CursorLockMode.Locked;
//        isInventoryOpened = false;
//        isStorageOpened = false;

//        if (lastStorage != null)
//        {
//            CloseStorage(lastStorage);
//        }
//    }
//    else
//    {
//        Cursor.lockState = CursorLockMode.None;
//        isInventoryOpened = true;
//    }
//}
//public void onUseItem(InputAction.CallbackContext value)
//{
//    
//    if (!value.started) return;
//    InventorySlot selectedSlot = hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>();

//    if (selectedSlot.heldItem != null && selectedSlot.heldItem.GetComponent<InventoryItem>() != null)
//    {
//        // Check if there is a held item in the selected hotbar slot
//        InventoryItem heldItem = selectedSlot.heldItem.GetComponent<InventoryItem>();

//        // Ensure heldItem is not null before accessing its properties
//        if (heldItem != null)
//        {
//            heldItem.itemScriptableObject.UseItem();
//            if (heldItem.itemScriptableObject is ConsumableSO)
//            {
//                Debug.Log("ta chovendo ai");
//                heldItem.stackCurrent--;
//                if (heldItem.stackCurrent <= 0)
//                {
//                    selectedSlot.heldItem = null;
//                    Destroy(heldItem.gameObject);
//                }
//            }
//            // This will call the overridden UseItem method in the ItemSO derived classes
//        }
//    }
//}


