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
        if (slot == null)
        {
            Debug.LogError("Cannot set item in null slot");
            return;
        }

        // Use the slot's SetHeldItem method instead of manual transform manipulation
        slot.SetHeldItem(draggedObject);
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

    // Updated SwapItems method 
    private static void SwapItems(InventorySlot slot, InventorySlot lastSlot, GameObject draggedObject)
    {
        if (slot == null || lastSlot == null)
        {
            Debug.LogError("Cannot swap items: one or both slots are null");
            return;
        }

        GameObject currentSlotItem = slot.heldItem;

        // Use the slot's SetHeldItem method instead of manual transform manipulation
        lastSlot.SetHeldItem(currentSlotItem);
        slot.SetHeldItem(draggedObject);
    }

    // Utility methods 
    public static void ReturnItemToLastSlot(GameObject lastItemSlotObject, GameObject draggedObject)
    {
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        if (lastSlot != null)
        {
            lastSlot.SetHeldItem(draggedObject);
        }
        else
        {
            Debug.LogError("LastItemSlotObject does not have InventorySlot component");
            // Fallback to old method if slot component is missing
            draggedObject.transform.SetParent(lastItemSlotObject.transform);
        }
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
