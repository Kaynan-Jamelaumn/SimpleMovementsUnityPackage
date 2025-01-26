using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class ItemPickupHandler
{
    public static void AddItemToInventory(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject itemPrefab, GameObject player)
    {
        GameObject emptySlot = null;

        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            InventorySlot currentSlot = slots[slotIndex].GetComponent<InventorySlot>();
            if (currentSlot.SlotType != SlotType.Common) continue;

            if (currentSlot.heldItem != null)
            {
                InventoryItem currentItem = currentSlot.heldItem.GetComponent<InventoryItem>();
                ItemPickable pickedItemProperties = pickedItem.GetComponent<ItemPickable>();
                ItemSO pickedItemSO = pickedItemProperties.itemScriptableObject;

                if (currentItem != null && pickedItemSO == currentItem.itemScriptableObject)
                {
                    if (pickedItemProperties.quantity + currentItem.stackCurrent <= currentItem.stackMax)
                    {
                        currentItem.totalWeight += pickedItemProperties.quantity * pickedItemSO.Weight;
                        UpdateInventoryOnItemPick(currentSlot, pickedItemProperties.quantity, pickedItemProperties.DurabilityList, player);
                        UnityEngine.Object.Destroy(pickedItem);
                        return;
                    }
                    else
                    {
                        HandleOverflow(pickedItemProperties, currentItem, currentSlot, player);
                        if (pickedItemProperties.quantity <= 0) return;
                    }
                }
            }
            else if (currentSlot.heldItem == null)
            {
                emptySlot = slots[slotIndex];
                break;
            }
        }

        if (emptySlot != null)
        {
            inventoryManager.InstantiateNewItem(emptySlot, pickedItem);
            if (pickedItem.scene.IsValid())
                UnityEngine.Object.Destroy(pickedItem);
        }
    }

    private static void UpdateInventoryOnItemPick(InventorySlot slot, int quantityToAdd, List<int> durabilityList, GameObject player)
    {
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();
        currentItem.stackCurrent += quantityToAdd;

        foreach (int durability in durabilityList)
        {
            currentItem.DurabilityList.Add(durability);
        }

        player.GetComponent<PlayerStatusController>().WeightManager.AddWeight(currentItem.itemScriptableObject.Weight * quantityToAdd);
    }


    private static void HandleOverflow(ItemPickable pickedItem, InventoryItem currentItem, InventorySlot currentSlot, GameObject player)
    {
        int remainingQuantity = pickedItem.quantity - (currentItem.stackMax - currentItem.stackCurrent);
        List<int> durabilityListToTransfer = new List<int>();
        int quantityToBeAdded = pickedItem.quantity - remainingQuantity;

        durabilityListToTransfer.AddRange(pickedItem.DurabilityList.GetRange(pickedItem.DurabilityList.Count - quantityToBeAdded, quantityToBeAdded));
        pickedItem.DurabilityList.RemoveRange(pickedItem.DurabilityList.Count - quantityToBeAdded, quantityToBeAdded);

        currentItem.totalWeight += quantityToBeAdded * pickedItem.itemScriptableObject.Weight;
        UpdateInventoryOnItemPick(currentSlot, quantityToBeAdded, durabilityListToTransfer, player);
        pickedItem.quantity = remainingQuantity;
    }

}

