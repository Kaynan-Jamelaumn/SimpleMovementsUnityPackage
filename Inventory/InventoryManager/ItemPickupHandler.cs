using System;
using UnityEngine;

public static class ItemPickUpHandler
{
    public static bool AddItemToInventory(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject itemPrefab, GameObject player)
    {
        if (!InventoryUtils.ValidateInventoryParameters(inventoryManager, pickedItem, slots, player))
        {
            return false;
        }

        try
        {
            var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
            if (pickedItemProperties?.itemScriptableObject == null)
            {
                Debug.LogError("ItemPickable component or ScriptableObject is missing");
                return false;
            }

            // Maintain original behavior: try stacking with ALL existing items first, then find empty slot
            return TryStackWithAllExistingItems(slots, pickedItem, player) ||
                   PlaceInFirstEmptySlot(inventoryManager, slots, pickedItem);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error adding item to inventory: {e.Message}");
            return false;
        }
    }

    private static bool TryStackWithAllExistingItems(GameObject[] slots, GameObject pickedItem, GameObject player)
    {
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
        if (pickedItemProperties?.itemScriptableObject == null) return false;

        // Go through ALL slots and try to stack with each compatible one
        foreach (GameObject slotObj in slots)
        {
            var slot = slotObj?.GetComponent<InventorySlot>();
            if (!IsValidSlotForStacking(slot)) continue;

            var currentItem = slot.heldItem.GetComponent<InventoryItem>();
            if (!CanItemsStack(currentItem, pickedItemProperties)) continue;

            // Try to stack as much as possible with this slot
            StackWithExistingItem(slot, pickedItem, player);

            // If we've consumed all picked items, we're done
            if (pickedItemProperties.quantity <= 0)
            {
                InventoryUtils.SafeDestroy(pickedItem);
                return true;
            }
        }

        // If we still have items left, return false so it tries to find empty slot
        return false;
    }

    private static bool IsValidSlotForStacking(InventorySlot slot)
    {
        return slot != null && slot.SlotType == SlotType.Common && !InventoryUtils.IsSlotEmpty(slot);
    }

    private static bool PlaceInFirstEmptySlot(InventoryManager inventoryManager, GameObject[] slots, GameObject pickedItem)
    {
        // Use InventoryUtils to find empty slot
        var emptySlots = InventoryUtils.FindEmptySlots(slots);
        if (emptySlots.Count == 0)
        {
            Debug.LogWarning("No empty slot available for item");
            return false;
        }

        return PlaceInEmptySlot(inventoryManager, emptySlots[0], pickedItem);
    }

    private static void StackWithExistingItem(InventorySlot slot, GameObject pickedItem, GameObject player)
    {
        var currentItem = slot.heldItem.GetComponent<InventoryItem>();
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();

        int availableSpace = currentItem.GetAvailableStackSpace();
        if (availableSpace <= 0) return;

        int amountToStack = Mathf.Min(pickedItemProperties.quantity, availableSpace);

        // Transfer durability for the amount we're stacking
        InventoryUtils.TransferDurabilityList(pickedItemProperties.DurabilityList, currentItem.DurabilityList, amountToStack);

        // Update current item stack
        currentItem.AddToStack(amountToStack);

        // Update player weight
        float weightPerItem = pickedItemProperties.itemScriptableObject.Weight;
        InventoryUtils.UpdatePlayerWeight(player, amountToStack * weightPerItem);

        // Reduce picked item quantity
        pickedItemProperties.quantity -= amountToStack;
    }

    private static bool CanItemsStack(InventoryItem currentItem, ItemPickable pickedItemProperties)
    {
        return currentItem?.itemScriptableObject != null &&
               pickedItemProperties?.itemScriptableObject != null &&
               currentItem.itemScriptableObject == pickedItemProperties.itemScriptableObject;
    }

    private static bool PlaceInEmptySlot(InventoryManager inventoryManager, InventorySlot emptySlot, GameObject pickedItem)
    {
        if (emptySlot == null || inventoryManager == null || pickedItem == null)
        {
            Debug.LogWarning("Cannot place item: invalid parameters");
            return false;
        }

        try
        {
            inventoryManager.InstantiateNewItem(emptySlot.gameObject, pickedItem);

            if (pickedItem.scene.IsValid())
            {
                InventoryUtils.SafeDestroy(pickedItem);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error placing item in empty slot: {e.Message}");
            return false;
        }
    }
}