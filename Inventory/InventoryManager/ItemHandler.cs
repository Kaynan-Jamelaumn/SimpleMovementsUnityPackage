using UnityEngine.InputSystem;
using UnityEngine;

public static class ItemHandler
{
    // Main item placement method - enhanced for armor set system
    public static void PlaceItemInSlot(InventorySlot slot, GameObject draggedObject, PlayerStatusController playerStatusController)
    {
        SetItemInSlot(slot, draggedObject);

        var draggedItem = draggedObject.GetComponent<InventoryItem>();
        if (draggedItem?.itemScriptableObject is ArmorSO armorSO)
        {
            // Use specialized armor equipment handler
            ArmorEquipmentHandler.EquipArmor(slot, draggedItem, playerStatusController);
            // Note: ArmorSO.ApplyEquippedStats will automatically notify the ArmorSetManager
        }
        else
        {
            // Use existing equipment handling for non-armor items
            HandleItemEquipping(slot, draggedItem, playerStatusController);
        }
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

    // Equipment handling - enhanced for armor detection
    private static void HandleItemEquipping(InventorySlot slot, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        // Handle armor separately
        if (draggedItem?.itemScriptableObject is ArmorSO)
        {
            // Armor handling is done in PlaceItemInSlot
            return;
        }

        // Handle non-armor equipment
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
        if (item?.itemScriptableObject is ArmorSO) return false; // Armor handled separately

        return slot.SlotType != SlotType.Common &&
               slot.SlotType == (SlotType)item.itemScriptableObject.ItemType &&
               !item.isEquipped;
    }

    private static bool ShouldUnequip(InventorySlot slot, InventoryItem item)
    {
        if (item?.itemScriptableObject is ArmorSO) return false; // Armor handled separately

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

    // Stack operations - enhanced for armor compatibility
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
        // Armor cannot be stacked
        if (slotHeldItem.itemScriptableObject is ArmorSO || draggedItem.itemScriptableObject is ArmorSO)
            return false;

        return slotHeldItem.stackCurrent < slotHeldItem.stackMax &&
               slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject;
    }

    // Item switching logic - enhanced for armor handling
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

        // Handle armor equipment states before switching
        HandleArmorSwitching(slot, lastSlot, currentItem, draggedItem, playerStatusController);

        // Handle non-armor equipment states
        UpdateEquippedStates(slot, lastSlot, currentItem, draggedItem, playerStatusController);

        // Perform the actual item swap
        SwapItems(slot, lastSlot, draggedObject);
    }

    private static void HandleArmorSwitching(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        var currentArmor = currentItem?.itemScriptableObject as ArmorSO;
        var draggedArmor = draggedItem?.itemScriptableObject as ArmorSO;

        // Unequip current armor if it's equipped
        if (currentArmor != null && currentItem.isEquipped)
        {
            ArmorEquipmentHandler.UnequipArmor(currentItem, playerStatusController);
        }

        // Unequip dragged armor if it's equipped
        if (draggedArmor != null && draggedItem.isEquipped)
        {
            ArmorEquipmentHandler.UnequipArmor(draggedItem, playerStatusController);
        }

        // Re-equip in new slots if appropriate
        if (currentArmor != null && ArmorEquipmentHandler.IsSlotCompatibleWithArmor(lastSlot, currentArmor))
        {
            // Will be equipped after swap in SwapItems completion
        }

        if (draggedArmor != null && ArmorEquipmentHandler.IsSlotCompatibleWithArmor(slot, draggedArmor))
        {
            // Will be equipped after swap in SwapItems completion
        }
    }

    private static bool CanSwitchItems(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem)
    {
        // Enhanced compatibility check for armor
        var currentArmor = currentItem?.itemScriptableObject as ArmorSO;
        var draggedArmor = draggedItem?.itemScriptableObject as ArmorSO;

        // Check armor slot compatibility
        if (currentArmor != null && !ArmorEquipmentHandler.IsSlotCompatibleWithArmor(lastSlot, currentArmor))
        {
            return false;
        }

        if (draggedArmor != null && !ArmorEquipmentHandler.IsSlotCompatibleWithArmor(slot, draggedArmor))
        {
            return false;
        }

        // Original compatibility check for non-armor items
        return !((currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType && slot.SlotType != SlotType.Common) ||
                (lastSlot.SlotType != SlotType.Common && currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType));
    }

    private static void UpdateEquippedStates(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        // Skip armor items as they're handled separately
        if (currentItem?.itemScriptableObject is ArmorSO || draggedItem?.itemScriptableObject is ArmorSO)
            return;

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

    // Updated SwapItems method with post-swap armor equipping
    private static void SwapItems(InventorySlot slot, InventorySlot lastSlot, GameObject draggedObject)
    {
        if (slot == null || lastSlot == null)
        {
            Debug.LogError("Cannot swap items: one or both slots are null");
            return;
        }

        GameObject currentSlotItem = slot.heldItem;
        var playerStatusController = Object.FindFirstObjectByType<PlayerStatusController>();

        // Use the slot's SetHeldItem method instead of manual transform manipulation
        lastSlot.SetHeldItem(currentSlotItem);
        slot.SetHeldItem(draggedObject);

        // Handle post-swap armor equipping
        if (playerStatusController != null)
        {
            // Check if items should be equipped in their new slots
            var draggedItem = draggedObject?.GetComponent<InventoryItem>();
            var currentItem = currentSlotItem?.GetComponent<InventoryItem>();

            if (draggedItem?.itemScriptableObject is ArmorSO draggedArmor &&
                ArmorEquipmentHandler.IsSlotCompatibleWithArmor(slot, draggedArmor) &&
                SlotTypeHelper.IsArmorSlot(slot.SlotType))
            {
                ArmorEquipmentHandler.EquipArmor(slot, draggedItem, playerStatusController);
            }

            if (currentItem?.itemScriptableObject is ArmorSO currentArmor &&
                ArmorEquipmentHandler.IsSlotCompatibleWithArmor(lastSlot, currentArmor) &&
                SlotTypeHelper.IsArmorSlot(lastSlot.SlotType))
            {
                ArmorEquipmentHandler.EquipArmor(lastSlot, currentItem, playerStatusController);
            }
        }
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

    // Item dropping - enhanced for armor
    public static void DropItem(GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController, Camera cam, GameObject player)
    {
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        var draggedItem = draggedObject.GetComponent<InventoryItem>();

        // Handle armor unequipping
        if (draggedItem?.itemScriptableObject is ArmorSO)
        {
            if (draggedItem.isEquipped)
            {
                ArmorEquipmentHandler.UnequipArmor(draggedItem, playerStatusController);
            }
        }
        else if (lastSlot.SlotType != SlotType.Common)
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