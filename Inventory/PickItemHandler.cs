using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemPickupHandler
{
    public static void AddItemToInventory(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject itemPrefab, GameObject player)
    {
        GameObject emptySlot = FindEmptySlot(slots);

        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            InventorySlot currentSlot = slots[slotIndex].GetComponent<InventorySlot>();

            if (!IsValidSlotForItem(currentSlot))
                continue;

            if (currentSlot.heldItem != null)
            {
                if (TryStackItem(currentSlot, pickedItem, player))
                    return;
            }
            else if (emptySlot == null)
            {
                emptySlot = slots[slotIndex];
            }
        }

        PlaceItemInEmptySlot(inventoryManager, emptySlot, pickedItem);
    }

    private static GameObject FindEmptySlot(GameObject[] slots)
    {
        foreach (GameObject slot in slots)
        {
            InventorySlot inventorySlot = slot.GetComponent<InventorySlot>();
            if (inventorySlot.heldItem == null && inventorySlot.SlotType == SlotType.Common)
                return slot;
        }
        return null;
    }

    private static bool IsValidSlotForItem(InventorySlot slot)
    {
        return slot.SlotType == SlotType.Common;
    }

    private static bool TryStackItem(InventorySlot currentSlot, GameObject pickedItem, GameObject player)
    {
        InventoryItem currentItem = currentSlot.heldItem.GetComponent<InventoryItem>();
        ItemPickable pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
        ItemSO pickedItemSO = pickedItemProperties.itemScriptableObject;

        if (currentItem != null && pickedItemSO == currentItem.itemScriptableObject)
        {
            if (pickedItemProperties.quantity + currentItem.stackCurrent <= currentItem.stackMax)
            {
                AddToExistingStack(currentSlot, pickedItemProperties, player);
                UnityEngine.Object.Destroy(pickedItem);
                return true;
            }
            else
            {
                HandleOverflow(pickedItemProperties, currentItem, currentSlot, player);
                if (pickedItemProperties.quantity <= 0)
                    return true;
            }
        }
        return false;
    }

    private static void AddToExistingStack(InventorySlot slot, ItemPickable pickedItem, GameObject player)
    {
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();
        currentItem.stackCurrent += pickedItem.quantity;

        foreach (int durability in pickedItem.DurabilityList)
        {
            currentItem.DurabilityList.Add(durability);
        }

        UpdatePlayerWeight(player, pickedItem.itemScriptableObject.Weight * pickedItem.quantity);
    }

    private static void HandleOverflow(ItemPickable pickedItem, InventoryItem currentItem, InventorySlot currentSlot, GameObject player)
    {
        int remainingQuantity = pickedItem.quantity - (currentItem.stackMax - currentItem.stackCurrent);
        int quantityToBeAdded = pickedItem.quantity - remainingQuantity;

        TransferDurability(pickedItem, currentItem, quantityToBeAdded);

        currentItem.totalWeight += quantityToBeAdded * pickedItem.itemScriptableObject.Weight;
        UpdateInventoryOnItemPick(currentSlot, quantityToBeAdded, player);
        pickedItem.quantity = remainingQuantity;
    }

    private static void TransferDurability(ItemPickable pickedItem, InventoryItem currentItem, int quantityToBeAdded)
    {
        List<int> durabilityListToTransfer = pickedItem.DurabilityList.GetRange(pickedItem.DurabilityList.Count - quantityToBeAdded, quantityToBeAdded);
        pickedItem.DurabilityList.RemoveRange(pickedItem.DurabilityList.Count - quantityToBeAdded, quantityToBeAdded);

        currentItem.DurabilityList.AddRange(durabilityListToTransfer);
    }

    private static void UpdateInventoryOnItemPick(InventorySlot slot, int quantityToAdd, GameObject player)
    {
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();
        currentItem.stackCurrent += quantityToAdd;

        UpdatePlayerWeight(player, currentItem.itemScriptableObject.Weight * quantityToAdd);
    }

    private static void UpdatePlayerWeight(GameObject player, float weightToAdd)
    {
        player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(weightToAdd);
    }

    private static void PlaceItemInEmptySlot(InventoryManager inventoryManager, GameObject emptySlot, GameObject pickedItem)
    {
        if (emptySlot != null)
        {
            inventoryManager.InstantiateNewItem(emptySlot, pickedItem);
            if (pickedItem.scene.IsValid())
            {
                UnityEngine.Object.Destroy(pickedItem);
            }
        }
    }
}
