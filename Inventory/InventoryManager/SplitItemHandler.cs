using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplitItemHandler
{
    /// <summary>Splits half of the stack into a free slot.</summary>
    public static bool SplitItemIntoNewStack(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player)
        => SplitItemIntoNewStack(inventoryManager, pickedItem, slots, player, -1);

    /// <summary>
    /// Moves <paramref name="amount"/> items of the stack (with their durabilities) into a free slot - preferably in the
    /// same part of the inventory (hotbar or bag). <paramref name="amount"/> below 1 = half.
    /// </summary>
    public static bool SplitItemIntoNewStack(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player, int amount)
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
            // A free place in the same part of the inventory (hotbar or bag) - the grid inventory checks the item's size.
            InventorySlot source = inventoryManager.FindSlotHolding(pickedItem.gameObject);
            InventorySlot emptySlot = inventoryManager.FindFreeSlotFor(pickedItem.itemScriptableObject, source != null && source.IsHotbarSlot);
            if (emptySlot == null)
            {
                Debug.LogWarning("No empty slot available for splitting");
                inventoryManager.ShowMessage("No room to split the stack.");
                return false;
            }
            int quantityToTransfer = amount >= 1
                ? Mathf.Clamp(amount, 1, pickedItem.stackCurrent - 1)
                : CalculateQuantityToTransfer(pickedItem.stackCurrent);

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
        if (CreateAndAssignNewItem(inventoryManager, originalItem, targetSlot, quantityToTransfer, durabilityToTransfer))
            return true;

        // It could not be created there: everything goes back to the original stack (nothing is lost).
        originalItem.AddToStack(quantityToTransfer);
        originalItem.DurabilityList.AddRange(durabilityToTransfer);
        InventoryUtils.UpdatePlayerWeight(player, quantityToTransfer * originalItem.itemScriptableObject.Weight);
        originalItem.RefreshUI();
        return false;
    }

    private static List<int> PrepareDurabilityTransfer(InventoryItem originalItem, int quantity)
    {
        var durabilityToTransfer = new List<int>();

        if (originalItem.Live()?.DurabilityList == null || quantity <= 0)
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

    private static bool CreateAndAssignNewItem(
        InventoryManager inventoryManager,
        InventoryItem originalItem,
        InventorySlot emptySlot,
        int quantity,
        List<int> durabilityList)
    {
        if (inventoryManager == null || originalItem.Live()?.itemScriptableObject == null || emptySlot == null)
        {
            Debug.LogError("Cannot create new item: invalid parameters");
            return false;
        }

        return inventoryManager.CreateItemInSlot(emptySlot, originalItem.itemScriptableObject, quantity, durabilityList, true) != null;
    }

    private static bool ValidateInputs(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player)
    {
        return inventoryManager != null &&
               pickedItem.Live()?.itemScriptableObject != null &&
               slots != null &&
               player != null;
    }

    private static int CalculateQuantityToTransfer(int stackCurrent)
    {
        return Mathf.Max(1, stackCurrent / 2);
    }
}