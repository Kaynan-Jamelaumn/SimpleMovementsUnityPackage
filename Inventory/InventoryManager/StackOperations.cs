
using UnityEngine;

public static class StackOperations
{
    public static void FillStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, GameObject lastItemSlotObject)
    {
        int itemsToFillStack = slotHeldItem.stackMax - slotHeldItem.stackCurrent;

        if (itemsToFillStack >= draggedItem.stackCurrent)
        {
            FillEntireStack(slotHeldItem, draggedItem);
        }
        else
        {
            FillPartialStack(slotHeldItem, draggedItem, itemsToFillStack, lastItemSlotObject);
        }
    }

    private static void FillEntireStack(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.totalWeight += draggedItem.totalWeight;

        Object.Destroy(draggedItem.gameObject);
    }

    private static void FillPartialStack(InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack, GameObject lastItemSlotObject)
    {
        // Transfer durability items
        for (int j = 0; j < itemsToFillStack; j++)
        {
            if (draggedItem.DurabilityList.Count > 0)
            {
                slotHeldItem.DurabilityList.Add(draggedItem.DurabilityList[^1]);
                draggedItem.DurabilityList.RemoveAt(draggedItem.DurabilityList.Count - 1);
            }
        }

        // Update weights and stacks
        float weightPerItem = draggedItem.itemScriptableObject.Weight;
        slotHeldItem.totalWeight += weightPerItem * itemsToFillStack;
        draggedItem.totalWeight -= weightPerItem * itemsToFillStack;

        slotHeldItem.stackCurrent += itemsToFillStack;
        draggedItem.stackCurrent -= itemsToFillStack;

        // Return remaining items to last slot using new slot system
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        if (lastSlot != null)
        {
            lastSlot.SetHeldItem(draggedItem.gameObject);
        }
        else
        {
            Debug.LogError("LastItemSlotObject does not have InventorySlot component");
            // Fallback to old method
            lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedItem.gameObject);
        }
    }
}