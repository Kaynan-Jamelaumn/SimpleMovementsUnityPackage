using UnityEngine;

/// <summary>Moves quantity between two stacks of the same item (durability and weight follow the items).</summary>
public static class StackOperations
{
    public static void FillStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, GameObject lastItemSlotObject)
    {
        int itemsToFillStack = slotHeldItem.stackMax - slotHeldItem.stackCurrent;

        if (itemsToFillStack >= draggedItem.stackCurrent)
            FillEntireStack(slotHeldItem, draggedItem);
        else
            FillPartialStack(slotHeldItem, draggedItem, itemsToFillStack, lastItemSlotObject);
    }

    private static void FillEntireStack(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.UpdateTotalWeight();
        slotHeldItem.RefreshUI();

        Object.Destroy(draggedItem.gameObject);
    }

    private static void FillPartialStack(InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack, GameObject lastItemSlotObject)
    {
        // Durability entries travel with the items
        for (int j = 0; j < itemsToFillStack && draggedItem.DurabilityList.Count > 0; j++)
        {
            slotHeldItem.DurabilityList.Add(draggedItem.DurabilityList[^1]);
            draggedItem.DurabilityList.RemoveAt(draggedItem.DurabilityList.Count - 1);
        }

        slotHeldItem.stackCurrent += itemsToFillStack;
        draggedItem.stackCurrent -= itemsToFillStack;
        slotHeldItem.UpdateTotalWeight();
        draggedItem.UpdateTotalWeight();
        slotHeldItem.RefreshUI();
        draggedItem.RefreshUI();

        // The rest goes back to where it came from
        var lastSlot = lastItemSlotObject != null ? lastItemSlotObject.GetComponent<InventorySlot>() : null;
        if (lastSlot != null)
            lastSlot.SetHeldItem(draggedItem.gameObject);
        else
            Debug.LogError("[Inventory] The item's previous slot has no InventorySlot component.");
    }
}
