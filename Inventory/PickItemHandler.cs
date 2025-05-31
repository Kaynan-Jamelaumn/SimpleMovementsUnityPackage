using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemPickupHandler
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
            GameObject emptySlot = FindEmptySlot(slots);

            // Try to stack with existing items first
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var currentSlot = slots[slotIndex]?.GetComponent<InventorySlot>();
                if (currentSlot == null || !IsValidSlotForItem(currentSlot))
                    continue;

                if (currentSlot.heldItem != null)
                {
                    if (TryStackItem(currentSlot, pickedItem, player))
                        return true;
                }
                else if (emptySlot == null)
                {
                    emptySlot = slots[slotIndex];
                }
            }

            // Place in empty slot if no stacking was possible
            return PlaceItemInEmptySlot(inventoryManager, emptySlot, pickedItem);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error adding item to inventory: {e.Message}");
            return false;
        }
    }

    private static bool ValidateParameters(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject player)
    {
        return inventoryManager != null &&
               pickedItem != null &&
               slots != null &&
               slots.Length > 0 &&
               player != null;
    }

    private static GameObject FindEmptySlot(GameObject[] slots)
    {
        if (slots == null) return null;

        foreach (GameObject slot in slots)
        {
            if (slot == null) continue;

            var inventorySlot = slot.GetComponent<InventorySlot>();
            if (inventorySlot?.heldItem == null && inventorySlot.SlotType == SlotType.Common)
                return slot;
        }
        return null;
    }

    private static bool IsValidSlotForItem(InventorySlot slot)
    {
        return slot != null && slot.SlotType == SlotType.Common;
    }

    private static bool TryStackItem(InventorySlot currentSlot, GameObject pickedItem, GameObject player)
    {
        if (currentSlot?.heldItem == null || pickedItem == null)
            return false;

        var currentItem = currentSlot.heldItem.GetComponent<InventoryItem>();
        var pickedItemProperties = pickedItem.GetComponent<ItemPickable>();

        if (currentItem == null || pickedItemProperties?.itemScriptableObject == null)
            return false;

        var pickedItemSO = pickedItemProperties.itemScriptableObject;

        if (pickedItemSO == currentItem.itemScriptableObject)
        {
            if (pickedItemProperties.quantity + currentItem.stackCurrent <= currentItem.stackMax)
            {
                AddToExistingStack(currentSlot, pickedItemProperties, player);
                SafeDestroy(pickedItem);
                return true;
            }
            else
            {
                HandleOverflow(pickedItemProperties, currentItem, currentSlot, player);
                return pickedItemProperties.quantity <= 0;
            }
        }
        return false;
    }

    private static void AddToExistingStack(InventorySlot slot, ItemPickable pickedItem, GameObject player)
    {
        if (slot?.heldItem == null || pickedItem == null) return;

        var currentItem = slot.heldItem.GetComponent<InventoryItem>();
        if (currentItem == null) return;

        currentItem.stackCurrent += pickedItem.quantity;

        // Transfer durability safely
        if (pickedItem.DurabilityList != null && currentItem.DurabilityList != null)
        {
            currentItem.DurabilityList.AddRange(pickedItem.DurabilityList);
        }

        UpdatePlayerWeight(player, pickedItem.itemScriptableObject.Weight * pickedItem.quantity);
    }

    private static void HandleOverflow(ItemPickable pickedItem, InventoryItem currentItem, InventorySlot currentSlot, GameObject player)
    {
        if (pickedItem == null || currentItem == null) return;

        int remainingQuantity = pickedItem.quantity - (currentItem.stackMax - currentItem.stackCurrent);
        int quantityToBeAdded = pickedItem.quantity - remainingQuantity;

        if (quantityToBeAdded > 0)
        {
            TransferDurability(pickedItem, currentItem, quantityToBeAdded);
            currentItem.totalWeight += quantityToBeAdded * pickedItem.itemScriptableObject.Weight;
            UpdateInventoryOnItemPick(currentSlot, quantityToBeAdded, player);
        }

        pickedItem.quantity = remainingQuantity;
    }

    private static void TransferDurability(ItemPickable pickedItem, InventoryItem currentItem, int quantityToBeAdded)
    {
        if (pickedItem?.DurabilityList == null || currentItem?.DurabilityList == null || quantityToBeAdded <= 0)
            return;

        int availableDurability = pickedItem.DurabilityList.Count;
        int actualTransfer = Mathf.Min(quantityToBeAdded, availableDurability);

        if (actualTransfer > 0)
        {
            var durabilityListToTransfer = pickedItem.DurabilityList.GetRange(
                pickedItem.DurabilityList.Count - actualTransfer, actualTransfer);

            pickedItem.DurabilityList.RemoveRange(
                pickedItem.DurabilityList.Count - actualTransfer, actualTransfer);

            currentItem.DurabilityList.AddRange(durabilityListToTransfer);
        }
    }

    private static void UpdateInventoryOnItemPick(InventorySlot slot, int quantityToAdd, GameObject player)
    {
        if (slot?.heldItem == null) return;

        var currentItem = slot.heldItem.GetComponent<InventoryItem>();
        if (currentItem == null) return;

        currentItem.stackCurrent += quantityToAdd;
        UpdatePlayerWeight(player, currentItem.itemScriptableObject.Weight * quantityToAdd);
    }

    private static void UpdatePlayerWeight(GameObject player, float weightToAdd)
    {
        if (player == null) return;

        var playerStatus = player.GetComponent<PlayerStatusController>();
        if (playerStatus?.WeightManager != null)
        {
            playerStatus.WeightManager.AddWeight(weightToAdd);
        }
        else
        {
            Debug.LogWarning("PlayerStatusController or WeightManager not found on player.");
        }
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