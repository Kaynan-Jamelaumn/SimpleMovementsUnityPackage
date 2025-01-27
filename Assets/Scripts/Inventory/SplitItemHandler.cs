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
        GameObject emptySlot = FindEmptySlot(slots);

        if (emptySlot != null)
        {
            // Calculate the quantity to transfer
            if (pickedItem.stackCurrent <= 1)
            {
                return; // No need to split if the stack is 1 or less
            }

            int quantity = pickedItem.stackCurrent / 2;

            // Create a new empty GameObject with InventoryItem component
            GameObject emptyGameObject = new GameObject("EmptyGameObject");
            ItemPickable newItem = emptyGameObject.AddComponent<ItemPickable>();

            // Create a list to store the transferred durability values
            List<int> durabilityListToTransfer = new List<int>();

            // Transfer durability values from the end of the list
            for (int j = 0; j < quantity; j++)
            {
                durabilityListToTransfer.Add(pickedItem.DurabilityList[pickedItem.DurabilityList.Count - 1]);
                pickedItem.DurabilityList.RemoveAt(pickedItem.DurabilityList.Count - 1);
            }


            // Update the quantities and weights for the original and new items
            pickedItem.stackCurrent -= quantity;
            player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(quantity * pickedItem.itemScriptableObject.Weight);
            pickedItem.totalWeight = quantity * pickedItem.itemScriptableObject.Weight;

            // Assign the transferred durability values to the new item
            newItem.DurabilityList = durabilityListToTransfer;

            newItem.itemScriptableObject = pickedItem.itemScriptableObject;
            newItem.quantity = quantity;
            // Instantiate the new item in the empty slot
            inventoryManager.InstantiateNewItem(emptySlot, emptyGameObject);
            UnityEngine.Object.Destroy(emptyGameObject);
        }
    }




}
