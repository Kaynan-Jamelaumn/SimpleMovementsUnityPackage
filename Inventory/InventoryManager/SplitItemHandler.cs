using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplitItemHandler
{

    public static GameObject FindEmptySlot(GameObject[] slots)
    {
        foreach (var slot in slots)
        {
            var inventorySlot = slot.GetComponent<InventorySlot>();
            if (inventorySlot.heldItem == null && inventorySlot.SlotType == SlotType.Common)
            {
                return slot;
            }
        }
        return null;
    }

    public static void SplitItemIntoNewStack(InventoryManager inventoryManager, InventoryItem pickedItem, GameObject[] slots, GameObject player)
    {
        if (pickedItem.stackCurrent <= 1) return; // No need to split if the stack is 1 or less

        GameObject emptySlot = FindEmptySlot(slots);
        if (emptySlot == null) return;

        int quantityToTransfer = CalculateQuantityToTransfer(pickedItem.stackCurrent);
        List<int> durabilityListToTransfer = TransferDurabilities(pickedItem, quantityToTransfer);

        UpdateOriginalItem(pickedItem, player, quantityToTransfer);

        CreateAndAssignNewItem(inventoryManager, pickedItem, emptySlot, quantityToTransfer, durabilityListToTransfer);
    }

    private static int CalculateQuantityToTransfer(int stackCurrent)
    {
        return stackCurrent / 2;
    }

    private static List<int> TransferDurabilities(InventoryItem pickedItem, int quantity)
    {
        var durabilityListToTransfer = new List<int>();

        for (int i = 0; i < quantity; i++)
        {
            int lastIndex = pickedItem.DurabilityList.Count - 1;
            durabilityListToTransfer.Add(pickedItem.DurabilityList[lastIndex]);
            pickedItem.DurabilityList.RemoveAt(lastIndex);
        }

        return durabilityListToTransfer;
    }

    private static void UpdateOriginalItem(InventoryItem pickedItem, GameObject player, int quantity)
    {
        pickedItem.stackCurrent -= quantity;
        PlayerStatusController playerStatus = player.GetComponent<PlayerStatusController>();
        playerStatus.WeightManager.ConsumeWeight(quantity * pickedItem.itemScriptableObject.Weight);
        pickedItem.totalWeight = pickedItem.stackCurrent * pickedItem.itemScriptableObject.Weight;
    }

    private static void CreateAndAssignNewItem(
        InventoryManager inventoryManager,
        InventoryItem pickedItem,
        GameObject emptySlot,
        int quantity,
        List<int> durabilityListToTransfer)
    {
        GameObject tempGameObject = new GameObject("TempItem");
        ItemPickable newItem = tempGameObject.AddComponent<ItemPickable>();

        newItem.DurabilityList = durabilityListToTransfer;
        newItem.itemScriptableObject = pickedItem.itemScriptableObject;
        newItem.quantity = quantity;

        inventoryManager.InstantiateNewItem(emptySlot, tempGameObject);
        UnityEngine.Object.Destroy(tempGameObject);
    }
}