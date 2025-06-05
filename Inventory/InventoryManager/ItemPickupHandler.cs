using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemPickUpHandler
{
    public static bool AddItemToInventory(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject itemPrefab, GameObject player)
    {
        if (!ValidateParameters(inventoryManager, pickedItem, slots, player))
        {
            Debug.LogError("Invalid parameters for AddItemToInventory");
            return false;
        }

        try
        {
            // Try to stack with existing items first, then find empty slot
            return TryStackWithExistingItems(slots, pickedItem, player) ||
                   PlaceInEmptySlot(inventoryManager, slots, pickedItem);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error adding item to inventory: {e.Message}");
            return false;
        }
    }

    private static bool ValidateParameters(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject player)
    {
        return inventoryManager != null && pickedItem != null && slots != null && slots.Length > 0 && player != null;
    }

    private static bool TryStackWithExistingItems(GameObject[] slots, GameObject pickedItem, GameObject player)
    {
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
        if (pickedItemProperties?.itemScriptableObject == null) return false;

        foreach (GameObject slot in slots)
        {
            var inventorySlot = slot?.GetComponent<InventorySlot>();
            if (!IsValidSlotForStacking(inventorySlot)) continue;

            if (TryStackItem(inventorySlot, pickedItem, player))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsValidSlotForStacking(InventorySlot slot)
    {
        return slot != null && slot.SlotType == SlotType.Common && slot.heldItem != null;
    }

    private static bool TryStackItem(InventorySlot currentSlot, GameObject pickedItem, GameObject player)
    {
        var currentItem = currentSlot.heldItem.GetComponent<InventoryItem>();
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();

        if (!CanItemsStack(currentItem, pickedItemProperties)) return false;

        int availableSpace = currentItem.stackMax - currentItem.stackCurrent;

        if (pickedItemProperties.quantity <= availableSpace)
        {
            // All items can be stacked
            AddToExistingStack(currentSlot, pickedItemProperties, player);
            SafeDestroy(pickedItem);
            return true;
        }
        else
        {
            // Partial stacking
            HandlePartialStacking(pickedItemProperties, currentItem, currentSlot, player, availableSpace);
            return pickedItemProperties.quantity <= 0; // Return true if all items were consumed
        }
    }

    private static bool CanItemsStack(InventoryItem currentItem, ItemPickable pickedItemProperties)
    {
        return currentItem?.itemScriptableObject != null &&
               pickedItemProperties?.itemScriptableObject != null &&
               currentItem.itemScriptableObject == pickedItemProperties.itemScriptableObject;
    }

    private static void AddToExistingStack(InventorySlot slot, ItemPickable pickedItem, GameObject player)
    {
        var currentItem = slot.heldItem.GetComponent<InventoryItem>();

        currentItem.stackCurrent += pickedItem.quantity;

        // Transfer durability safely
        if (pickedItem.DurabilityList != null && currentItem.DurabilityList != null)
        {
            currentItem.DurabilityList.AddRange(pickedItem.DurabilityList);
        }

        UpdatePlayerWeight(player, pickedItem.itemScriptableObject.Weight * pickedItem.quantity);
    }

    private static void HandlePartialStacking(ItemPickable pickedItem, InventoryItem currentItem,
        InventorySlot currentSlot, GameObject player, int availableSpace)
    {
        if (availableSpace > 0)
        {
            TransferDurability(pickedItem, currentItem, availableSpace);
            currentItem.stackCurrent = currentItem.stackMax;
            currentItem.totalWeight = currentItem.stackMax * pickedItem.itemScriptableObject.Weight;
            UpdatePlayerWeight(player, availableSpace * pickedItem.itemScriptableObject.Weight);
        }

        pickedItem.quantity -= availableSpace;
    }

    private static void TransferDurability(ItemPickable pickedItem, InventoryItem currentItem, int quantityToTransfer)
    {
        if (pickedItem?.DurabilityList == null || currentItem?.DurabilityList == null || quantityToTransfer <= 0)
            return;

        int availableDurability = pickedItem.DurabilityList.Count;
        int actualTransfer = Mathf.Min(quantityToTransfer, availableDurability);

        if (actualTransfer > 0)
        {
            // Transfer from the end of the list
            int startIndex = pickedItem.DurabilityList.Count - actualTransfer;
            var durabilityToTransfer = pickedItem.DurabilityList.GetRange(startIndex, actualTransfer);

            pickedItem.DurabilityList.RemoveRange(startIndex, actualTransfer);
            currentItem.DurabilityList.AddRange(durabilityToTransfer);
        }
    }

    private static void UpdatePlayerWeight(GameObject player, float weightToAdd)
    {
        var playerStatus = player?.GetComponent<PlayerStatusController>();
        if (playerStatus?.WeightManager != null)
        {
            playerStatus.WeightManager.AddWeight(weightToAdd);
        }
        else
        {
            Debug.LogWarning("PlayerStatusController or WeightManager not found on player.");
        }
    }

    private static bool PlaceInEmptySlot(InventoryManager inventoryManager, GameObject[] slots, GameObject pickedItem)
    {
        GameObject emptySlot = FindEmptySlot(slots);

        if (emptySlot == null)
        {
            Debug.LogWarning("No empty slot available for item");
            return false;
        }

        return PlaceItemInEmptySlot(inventoryManager, emptySlot, pickedItem);
    }

    private static GameObject FindEmptySlot(GameObject[] slots)
    {
        foreach (GameObject slot in slots)
        {
            var inventorySlot = slot?.GetComponent<InventorySlot>();
            if (inventorySlot?.heldItem == null && inventorySlot.SlotType == SlotType.Common)
                return slot;
        }
        return null;
    }

    private static bool PlaceItemInEmptySlot(InventoryManager inventoryManager, GameObject emptySlot, GameObject pickedItem)
    {
        if (emptySlot == null || inventoryManager == null || pickedItem == null)
        {
            Debug.LogWarning("Cannot place item: empty slot, inventory manager, or picked item is null.");
            return false;
        }

        try
        {
            inventoryManager.InstantiateNewItem(emptySlot, pickedItem);

            if (pickedItem.scene.IsValid())
            {
                SafeDestroy(pickedItem);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error placing item in empty slot: {e.Message}");
            return false;
        }
    }

    private static void SafeDestroy(GameObject obj)
    {
        if (obj != null)
        {
            UnityEngine.Object.Destroy(obj);
        }
    }
}