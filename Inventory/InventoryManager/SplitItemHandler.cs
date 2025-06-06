using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplitItemHandler
{
    public static bool SplitItemIntoNewStack(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player)
    {
        if (!ValidateInputs(inventoryManager, pickedItem, slots, player))
        {
            Debug.LogError("Invalid inputs for SplitItemIntoNewStack");
            return false;
        }

        if (pickedItem.stackCurrent <= 1)
        {
            Debug.LogWarning("Cannot split item with stack size of 1 or less");
            return false;
        }

        try
        {
            // Use InventoryUtils to find empty slot
            var emptySlots = InventoryUtils.FindEmptySlots(slots);
            if (emptySlots.Count == 0)
            {
                Debug.LogWarning("No empty slot available for splitting");
                return false;
            }

            InventorySlot emptySlot = emptySlots[0];
            int quantityToTransfer = CalculateQuantityToTransfer(pickedItem.stackCurrent);

            return ExecuteSplit(inventoryManager, pickedItem, emptySlot, quantityToTransfer, player);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error splitting item: {e.Message}");
            return false;
        }
    }

    private static bool ExecuteSplit(InventoryManager inventoryManager, InventoryItem originalItem, InventorySlot targetSlot, int quantityToTransfer, GameObject player)
    {
        // Prepare durability transfer
        List<int> durabilityToTransfer = PrepareDurabilityTransfer(originalItem, quantityToTransfer);

        // Update original item
        UpdateOriginalItem(originalItem, player, quantityToTransfer);

        // Create new item in target slot
        CreateAndAssignNewItem(inventoryManager, originalItem, targetSlot, quantityToTransfer, durabilityToTransfer);

        return true;
    }

    private static List<int> PrepareDurabilityTransfer(InventoryItem originalItem, int quantity)
    {
        var durabilityToTransfer = new List<int>();

        if (originalItem?.DurabilityList == null || quantity <= 0)
            return durabilityToTransfer;

        int availableDurability = originalItem.DurabilityList.Count;
        int actualTransfer = Mathf.Min(quantity, availableDurability);

        // Use InventoryUtils for durability transfer
        InventoryUtils.TransferDurabilityList(originalItem.DurabilityList, durabilityToTransfer, actualTransfer);

        return durabilityToTransfer;
    }

    private static void UpdateOriginalItem(InventoryItem originalItem, GameObject player, int quantity)
    {
        if (originalItem == null || player == null || quantity <= 0) return;

        // Remove from stack
        originalItem.RemoveFromStack(quantity);

        // Update player weight using InventoryUtils
        float weightToRemove = quantity * originalItem.itemScriptableObject.Weight;
        InventoryUtils.UpdatePlayerWeight(player, -weightToRemove);
    }

    private static void CreateAndAssignNewItem(
        InventoryManager inventoryManager,
        InventoryItem originalItem,
        InventorySlot emptySlot,
        int quantity,
        List<int> durabilityList)
    {
        if (inventoryManager == null || originalItem?.itemScriptableObject == null || emptySlot == null)
        {
            Debug.LogError("Cannot create new item: invalid parameters");
            return;
        }

        try
        {
            // Create temporary ItemPickable for the InstantiateNewItem method
            GameObject tempGameObject = new GameObject("TempItem");
            ItemPickable newItem = tempGameObject.AddComponent<ItemPickable>();

            newItem.DurabilityList = durabilityList ?? new List<int>();
            newItem.itemScriptableObject = originalItem.itemScriptableObject;
            newItem.quantity = quantity;

            inventoryManager.InstantiateNewItem(emptySlot.gameObject, tempGameObject);
            InventoryUtils.SafeDestroy(tempGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating new item: {e.Message}");
        }
    }

    private static bool ValidateInputs(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player)
    {
        return inventoryManager != null &&
               pickedItem?.itemScriptableObject != null &&
               slots != null &&
               player != null;
    }

    private static int CalculateQuantityToTransfer(int stackCurrent)
    {
        return Mathf.Max(1, stackCurrent / 2);
    }
}