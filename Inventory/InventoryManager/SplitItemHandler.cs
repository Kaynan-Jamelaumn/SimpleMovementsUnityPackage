using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplitItemHandler
{
    public static GameObject FindEmptySlot(GameObject[] slots)
    {
        if (slots == null)
        {
            Debug.LogError("Slots array is null");
            return null;
        }

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            var inventorySlot = slot.GetComponent<InventorySlot>();
            if (inventorySlot?.heldItem == null && inventorySlot.SlotType == SlotType.Common)
            {
                return slot;
            }
        }
        return null;
    }

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
            GameObject emptySlot = FindEmptySlot(slots);
            if (emptySlot == null)
            {
                Debug.LogWarning("No empty slot available for splitting");
                return false;
            }

            int quantityToTransfer = CalculateQuantityToTransfer(pickedItem.stackCurrent);
            List<int> durabilityListToTransfer = TransferDurabilities(pickedItem, quantityToTransfer);

            UpdateOriginalItem(pickedItem, player, quantityToTransfer);
            CreateAndAssignNewItem(inventoryManager, pickedItem, emptySlot, quantityToTransfer, durabilityListToTransfer);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error splitting item: {e.Message}");
            return false;
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

    private static List<int> TransferDurabilities(InventoryItem pickedItem, int quantity)
    {
        var durabilityListToTransfer = new List<int>();

        if (pickedItem?.DurabilityList == null || quantity <= 0)
            return durabilityListToTransfer;

        int availableDurability = pickedItem.DurabilityList.Count;
        int actualTransfer = Mathf.Min(quantity, availableDurability);

        for (int i = 0; i < actualTransfer; i++)
        {
            int lastIndex = pickedItem.DurabilityList.Count - 1;
            if (lastIndex >= 0)
            {
                durabilityListToTransfer.Add(pickedItem.DurabilityList[lastIndex]);
                pickedItem.DurabilityList.RemoveAt(lastIndex);
            }
        }

        return durabilityListToTransfer;
    }

    private static void UpdateOriginalItem(InventoryItem pickedItem, GameObject player, int quantity)
    {
        if (pickedItem == null || player == null || quantity <= 0) return;

        pickedItem.stackCurrent -= quantity;

        var playerStatus = player.GetComponent<PlayerStatusController>();
        if (playerStatus?.WeightManager != null)
        {
            playerStatus.WeightManager.ConsumeWeight(quantity * pickedItem.itemScriptableObject.Weight);
        }

        pickedItem.totalWeight = pickedItem.stackCurrent * pickedItem.itemScriptableObject.Weight;
    }

    private static void CreateAndAssignNewItem(
        InventoryManager inventoryManager,
        InventoryItem pickedItem,
        GameObject emptySlot,
        int quantity,
        List<int> durabilityListToTransfer)
    {
        if (inventoryManager == null || pickedItem?.itemScriptableObject == null || emptySlot == null)
        {
            Debug.LogError("Cannot create new item: invalid parameters");
            return;
        }

        try
        {
            GameObject tempGameObject = new GameObject("TempItem");
            ItemPickable newItem = tempGameObject.AddComponent<ItemPickable>();

            newItem.DurabilityList = durabilityListToTransfer ?? new List<int>();
            newItem.itemScriptableObject = pickedItem.itemScriptableObject;
            newItem.quantity = quantity;

            inventoryManager.InstantiateNewItem(emptySlot, tempGameObject);
            UnityEngine.Object.Destroy(tempGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating new item: {e.Message}");
        }
    }
}