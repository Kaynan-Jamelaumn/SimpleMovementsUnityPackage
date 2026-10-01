using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Picks world items (<see cref="ItemPickable"/>) up into the inventory.</summary>
public static class ItemPickUpHandler
{
    /// <summary>
    /// Adds a picked world item: existing stacks first, then empty slots, splitting it into as many stacks as needed
    /// (a pickup bigger than Stack Max used to become one oversized stack). Whatever does not fit stays in the world.
    /// Returns true if at least part of it was picked up.
    /// </summary>
    public static bool AddItemToInventory(InventoryManager inventoryManager, GameObject pickedItem, GameObject[] slots, GameObject itemPrefab, GameObject player)
    {
        if (!InventoryUtils.ValidateInventoryParameters(inventoryManager, pickedItem, slots, player))
            return false;

        try
        {
            var pickable = pickedItem.GetComponent<ItemPickable>();
            if (pickable == null || pickable.itemScriptableObject == null)
            {
                Debug.LogError("[Inventory] The picked object has no ItemPickable with an item assigned.", pickedItem);
                return false;
            }

            int quantity = Mathf.Max(1, pickable.quantity);
            var durabilities = pickable.DurabilityList != null ? new List<int>(pickable.DurabilityList) : new List<int>();
            int remaining = inventoryManager.AddItem(pickable.itemScriptableObject, quantity, durabilities);

            if (remaining <= 0)
            {
                InventoryUtils.SafeDestroy(pickedItem);
                return true;
            }

            // Partly picked: the rest stays on the ground with its durabilities.
            int taken = quantity - remaining;
            pickable.quantity = remaining;
            // The inventory took durabilities from the end of the list: the first ones stay with the rest.
            if (pickable.DurabilityList != null && pickable.DurabilityList.Count > remaining)
                pickable.DurabilityList.RemoveRange(remaining, pickable.DurabilityList.Count - remaining);
            if (taken == 0)
                Debug.Log("[Inventory] No room for this item.");
            return taken > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Inventory] Error adding item to inventory: {e.Message}");
            return false;
        }
    }
}
