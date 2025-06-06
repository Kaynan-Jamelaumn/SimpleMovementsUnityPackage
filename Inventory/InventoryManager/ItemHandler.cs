using UnityEngine.InputSystem;
using UnityEngine;

public static class ItemHandler
{
    // Main item placement method
    public static void PlaceItemInSlot(InventorySlot slot, GameObject draggedObject, PlayerStatusController playerStatusController)
    {
        SetItemInSlot(slot, draggedObject);
        HandleItemEquipping(slot, draggedObject.GetComponent<InventoryItem>(), playerStatusController);
    }

    // Core slot operations
    private static void SetItemInSlot(InventorySlot slot, GameObject draggedObject)
    {
        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));
    }

    // Equipment handling
    private static void HandleItemEquipping(InventorySlot slot, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        if (IsEquippable(slot, draggedItem))
        {
            EquipItem(draggedItem, playerStatusController);
        }
        else if (ShouldUnequip(slot, draggedItem))
        {
            UnequipItem(draggedItem, playerStatusController);
        }
    }

    private static bool IsEquippable(InventorySlot slot, InventoryItem item)
    {
        return slot.SlotType != SlotType.Common &&
               slot.SlotType == (SlotType)item.itemScriptableObject.ItemType &&
               !item.isEquipped;
    }

    private static bool ShouldUnequip(InventorySlot slot, InventoryItem item)
    {
        return item.isEquipped &&
               slot.SlotType == SlotType.Common &&
               slot.SlotType != (SlotType)item.itemScriptableObject.ItemType;
    }

    private static void EquipItem(InventoryItem item, PlayerStatusController playerStatusController)
    {
        item.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
        item.isEquipped = true;
    }

    private static void UnequipItem(InventoryItem item, PlayerStatusController playerStatusController)
    {
        item.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
        item.isEquipped = false;
    }

    // Stack operations
    public static void SwitchOrFillStack(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        var slotHeldItem = slot.heldItem.GetComponent<InventoryItem>();
        var draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (ShouldSwitchItems(slotHeldItem, draggedItem))
        {
            SwitchItems(slot, draggedObject, lastItemSlotObject, playerStatusController);
        }
        else if (CanStackItems(slotHeldItem, draggedItem))
        {
            StackOperations.FillStack(slot, slotHeldItem, draggedItem, lastItemSlotObject);
        }
    }

    private static bool ShouldSwitchItems(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        return slotHeldItem.stackCurrent == slotHeldItem.stackMax ||
               slotHeldItem.itemScriptableObject != draggedItem.itemScriptableObject;
    }

    private static bool CanStackItems(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        return slotHeldItem.stackCurrent < slotHeldItem.stackMax &&
               slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject;
    }

    // Item switching logic
    public static void SwitchItems(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        var draggedItem = draggedObject.GetComponent<InventoryItem>();
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        var currentItem = slot.heldItem.GetComponent<InventoryItem>();

        if (!CanSwitchItems(slot, lastSlot, currentItem, draggedItem))
        {
            ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            return;
        }

        UpdateEquippedStates(slot, lastSlot, currentItem, draggedItem, playerStatusController);
        SwapItems(slot, lastSlot, draggedObject);
    }

    private static bool CanSwitchItems(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem)
    {
        return !((currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType && slot.SlotType != SlotType.Common) ||
                (lastSlot.SlotType != SlotType.Common && currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType));
    }

    private static void UpdateEquippedStates(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        if (slot.SlotType != SlotType.Common)
        {
            UnequipItem(currentItem, playerStatusController);
            EquipItem(draggedItem, playerStatusController);
        }
        else if (lastSlot.SlotType != SlotType.Common)
        {
            EquipItem(currentItem, playerStatusController);
            UnequipItem(draggedItem, playerStatusController);
        }
    }

    private static void SwapItems(InventorySlot slot, InventorySlot lastSlot, GameObject draggedObject)
    {
        lastSlot.SetHeldItem(slot.heldItem);
        slot.heldItem.transform.SetParent(lastSlot.transform.parent.parent.GetChild(2));

        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));
    }

    // Utility methods
    public static void ReturnItemToLastSlot(GameObject lastItemSlotObject, GameObject draggedObject)
    {
        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(lastItemSlotObject.transform);
    }

    // Item dropping
    public static void DropItem(GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController, Camera cam, GameObject player)
    {
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        var draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (lastSlot.SlotType != SlotType.Common)
        {
            UnequipItem(draggedItem, playerStatusController);
        }

        Vector3 dropPosition = GetDropPosition(cam);
        CreateDroppedItem(draggedItem, dropPosition, player);

        lastSlot.heldItem = null;
        Object.Destroy(draggedObject);
    }

    private static Vector3 GetDropPosition(Camera cam)
    {
        if (!cam) cam = Camera.main;

        Vector2 mousePosition = Vector2.zero;
        if (Mouse.current != null)
        {
            mousePosition = Mouse.current.position.ReadValue();
        }

        Ray ray = cam.ScreenPointToRay(mousePosition);
        return ray.GetPoint(3);
    }

    private static void CreateDroppedItem(InventoryItem draggedItem, Vector3 position, GameObject player)
    {
        GameObject newItem = Object.Instantiate(draggedItem.itemScriptableObject.Prefab, position, Quaternion.identity);

        var itemPickableComponent = newItem.GetComponent<ItemPickable>();
        itemPickableComponent.itemScriptableObject = draggedItem.itemScriptableObject;
        itemPickableComponent.quantity = draggedItem.stackCurrent;
        itemPickableComponent.DurabilityList = draggedItem.DurabilityList;
        itemPickableComponent.InteractionTime = draggedItem.itemScriptableObject.PickUpTime;

        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(
            itemPickableComponent.itemScriptableObject.Weight * itemPickableComponent.quantity);
    }
}

// Separate class for stack operations to improve organization
public static class StackOperations
{
    public static void FillStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, GameObject lastItemSlotObject)
    {
        int itemsToFillStack = slotHeldItem.stackMax - slotHeldItem.stackCurrent;

        if (itemsToFillStack >= draggedItem.stackCurrent)
        {
            FillEntireStack(slotHeldItem, draggedItem);
        }
        else
        {
            FillPartialStack(slotHeldItem, draggedItem, itemsToFillStack, lastItemSlotObject);
        }
    }

    private static void FillEntireStack(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.totalWeight += draggedItem.totalWeight;

        Object.Destroy(draggedItem.gameObject);
    }

    private static void FillPartialStack(InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack, GameObject lastItemSlotObject)
    {
        // Transfer durability items
        for (int j = 0; j < itemsToFillStack; j++)
        {
            if (draggedItem.DurabilityList.Count > 0)
            {
                slotHeldItem.DurabilityList.Add(draggedItem.DurabilityList[^1]);
                draggedItem.DurabilityList.RemoveAt(draggedItem.DurabilityList.Count - 1);
            }
        }

        // Update weights and stacks
        float weightPerItem = draggedItem.itemScriptableObject.Weight;
        slotHeldItem.totalWeight += weightPerItem * itemsToFillStack;
        draggedItem.totalWeight -= weightPerItem * itemsToFillStack;

        slotHeldItem.stackCurrent += itemsToFillStack;
        draggedItem.stackCurrent -= itemsToFillStack;

        // Return remaining items to last slot
        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedItem.gameObject);
    }
}