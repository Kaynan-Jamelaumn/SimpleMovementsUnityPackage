using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Centralized utility class for inventory operations
public static class InventoryUtils
{
    // Slot compatibility checking
    public static bool IsCompatibleSlot(InventorySlot slot, ItemType itemType)
    {
        if (slot == null) return false;
        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
    }

    // slot validation
    public static bool IsValidDropTarget(InventorySlot slot, ItemType itemType)
    {
        return slot != null && IsCompatibleSlot(slot, itemType);
    }

    public static bool IsValidSlotIndex(GameObject[] slots, int index)
    {
        return slots != null && index >= 0 && index < slots.Length && slots[index] != null;
    }

    // Find empty slots in inventory
    public static List<InventorySlot> FindEmptySlots(GameObject[] slots, SlotType requiredType = SlotType.Common)
    {
        var emptySlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null && (requiredType == SlotType.Common || slot.SlotType == requiredType))
            {
                emptySlots.Add(slot);
            }
        }

        return emptySlots;
    }

    // Find slots with stackable items
    public static List<InventorySlot> FindStackableSlots(GameObject[] slots, ItemSO itemSO)
    {
        var stackableSlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var heldItem = slot?.heldItem?.GetComponent<InventoryItem>();

            if (heldItem != null &&
                heldItem.itemScriptableObject == itemSO &&
                heldItem.stackCurrent < heldItem.stackMax)
            {
                stackableSlots.Add(slot);
            }
        }

        return stackableSlots;
    }

    //  Find first available slot (stackable or empty)
    public static InventorySlot FindFirstAvailableSlot(GameObject[] slots, ItemSO itemSO)
    {
        // First try to find stackable slots
        var stackableSlots = FindStackableSlots(slots, itemSO);
        if (stackableSlots.Count > 0)
            return stackableSlots[0];

        // Then find empty slots
        var emptySlots = FindEmptySlots(slots);
        return emptySlots.Count > 0 ? emptySlots[0] : null;
    }

    //  Find empty slot (GameObject version for backward compatibility)
    public static GameObject FindEmptySlot(GameObject[] slots)
    {
        var emptySlot = FindEmptySlots(slots);
        return emptySlot.Count > 0 ? emptySlot[0].gameObject : null;
    }

    // Weight management helpers
    public static void UpdatePlayerWeight(GameObject player, float weightChange)
    {
        var statusController = player?.GetComponent<PlayerStatusController>();
        var weightManager = statusController?.WeightManager;

        if (weightManager != null)
        {
            if (weightChange > 0)
                weightManager.AddWeight(weightChange);
            else if (weightChange < 0)
                weightManager.ConsumeWeight(-weightChange);
        }
        else
        {
            Debug.LogWarning("WeightManager not found on player");
        }
    }

    // Calculate total inventory weight
    public static float CalculateInventoryWeight(GameObject[] slots)
    {
        float totalWeight = 0f;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item != null)
                totalWeight += item.totalWeight;
        }

        return totalWeight;
    }

    // Get inventory item count
    public static int GetItemCount(GameObject[] slots, ItemSO itemSO)
    {
        int count = 0;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject == itemSO)
                count += item.stackCurrent;
        }

        return count;
    }

    //  Get available space for specific item
    public static int GetAvailableSpace(GameObject[] slots, ItemSO itemSO)
    {
        int availableSpace = 0;

        // Count space in existing stacks
        var stackableSlots = FindStackableSlots(slots, itemSO);
        foreach (var slot in stackableSlots)
        {
            var item = slot.heldItem.GetComponent<InventoryItem>();
            availableSpace += (item.stackMax - item.stackCurrent);
        }

        // Count empty slots
        var emptySlots = FindEmptySlots(slots);
        availableSpace += emptySlots.Count * itemSO.StackMax;

        return availableSpace;
    }

    //  Check if enough space exists
    public static bool HasEnoughSpace(GameObject[] slots, ItemSO itemSO, int requiredQuantity)
    {
        return GetAvailableSpace(slots, itemSO) >= requiredQuantity;
    }

    // Check if enough items exist
    public static bool HasEnoughItems(GameObject[] slots, ItemSO itemSO, int requiredAmount)
    {
        return GetItemCount(slots, itemSO) >= requiredAmount;
    }

    // Remove items from inventory
    public static int RemoveItems(GameObject[] slots, ItemSO itemSO, int amountToRemove)
    {
        int removedCount = 0;

        foreach (var slotObj in slots)
        {
            if (slotObj == null || removedCount >= amountToRemove) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject == itemSO)
            {
                int removeFromThisStack = Mathf.Min(item.stackCurrent, amountToRemove - removedCount);

                if (item.RemoveFromStack(removeFromThisStack))
                {
                    removedCount += removeFromThisStack;

                    // Destroy item if stack is empty
                    if (item.IsEmpty())
                    {
                        slot.heldItem = null;
                        SafeDestroy(item.gameObject);
                    }
                }
            }
        }

        return removedCount;
    }

    // Durability management
    public static void TransferDurabilityList(List<int> fromList, List<int> toList, int count)
    {
        if (fromList == null || toList == null || count <= 0) return;

        int transferCount = Mathf.Min(count, fromList.Count);

        for (int i = 0; i < transferCount; i++)
        {
            int lastIndex = fromList.Count - 1;
            if (lastIndex >= 0)
            {
                toList.Add(fromList[lastIndex]);
                fromList.RemoveAt(lastIndex);
            }
        }
    }

    //  Durability utilities
    public static int GetAverageDurability(List<int> durabilityList)
    {
        if (durabilityList == null || durabilityList.Count == 0) return 0;
        return Mathf.RoundToInt((float)durabilityList.Average());
    }

    public static int GetLowestDurability(List<int> durabilityList)
    {
        if (durabilityList == null || durabilityList.Count == 0) return 0;
        return durabilityList.Min();
    }

    public static int GetHighestDurability(List<int> durabilityList)
    {
        if (durabilityList == null || durabilityList.Count == 0) return 0;
        return durabilityList.Max();
    }

    // Safe operations
    public static void SafeDestroy(GameObject obj)
    {
        if (obj != null) Object.Destroy(obj);
    }

    //  Slot utilities
    public static bool IsSlotEmpty(InventorySlot slot)
    {
        return slot?.heldItem == null;
    }

    public static bool SlotHasItem(InventorySlot slot, ItemSO itemSO)
    {
        var item = slot?.heldItem?.GetComponent<InventoryItem>();
        return item?.itemScriptableObject == itemSO;
    }

    //  Validation helpers
    public static bool ValidateInventoryParameters(InventoryManager inventoryManager, GameObject item, GameObject[] slots, GameObject player)
    {
        if (inventoryManager == null)
        {
            Debug.LogError("InventoryManager is null");
            return false;
        }

        if (item == null)
        {
            Debug.LogError("Item is null");
            return false;
        }

        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("Slots array is null or empty");
            return false;
        }

        if (player == null)
        {
            Debug.LogError("Player is null");
            return false;
        }

        return true;
    }

    public static bool ValidateItemCreationParameters(GameObject emptySlot, GameObject pickedItem, GameObject itemPrefab)
    {
        if (emptySlot == null)
        {
            Debug.LogError("Empty slot is null");
            return false;
        }

        if (pickedItem == null)
        {
            Debug.LogError("Picked item is null");
            return false;
        }

        if (itemPrefab == null)
        {
            Debug.LogError("Item prefab is null");
            return false;
        }

        var pickedItemComponent = pickedItem.GetComponent<ItemPickable>();
        if (pickedItemComponent?.itemScriptableObject == null)
        {
            Debug.LogError("ItemPickable component or ScriptableObject is missing");
            return false;
        }

        return true;
    }




    // Inventory state utilities
    public static int CountItemsOfType(GameObject[] slots, ItemType itemType)
    {
        int count = 0;
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject?.ItemType == itemType)
                count += item.stackCurrent;
        }
        return count;
    }

    public static List<InventoryItem> GetAllItemsOfType(GameObject[] slots, ItemType itemType)
    {
        var items = new List<InventoryItem>();
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject?.ItemType == itemType)
                items.Add(item);
        }
        return items;
    }

    // Performance utilities
    public static void ClearEmptySlots(GameObject[] slots)
    {
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item != null && item.IsEmpty())
            {
                slot.heldItem = null;
                SafeDestroy(item.gameObject);
            }
        }
    }

    // Batch operations
    public static Dictionary<ItemSO, int> GetInventorySummary(GameObject[] slots)
    {
        var summary = new Dictionary<ItemSO, int>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject != null)
            {
                if (summary.ContainsKey(item.itemScriptableObject))
                    summary[item.itemScriptableObject] += item.stackCurrent;
                else
                    summary[item.itemScriptableObject] = item.stackCurrent;
            }
        }

        return summary;
    }

    //  Debug helpers
    public static void LogInventoryState(GameObject[] slots, string context = "")
    {
        Debug.Log($"=== Inventory State {context} ===");

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i]?.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item != null)
            {
                Debug.Log($"Slot {i}: {item.itemScriptableObject.Name} x{item.stackCurrent}");
            }
            else
            {
                Debug.Log($"Slot {i}: Empty");
            }
        }
    }

    public static void LogInventorySummary(GameObject[] slots)
    {
        var summary = GetInventorySummary(slots);
        Debug.Log("=== Inventory Summary ===");

        foreach (var kvp in summary)
        {
            Debug.Log($"{kvp.Key.Name}: {kvp.Value}");
        }

        Debug.Log($"Total unique items: {summary.Count}");
        Debug.Log($"Total weight: {CalculateInventoryWeight(slots):F2}");
    }

    // Quick access methods for common operations
    public static bool TryGetFirstItemOfType(GameObject[] slots, ItemType itemType, out InventoryItem item)
    {
        item = null;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var slotItem = slot?.heldItem?.GetComponent<InventoryItem>();

            if (slotItem?.itemScriptableObject?.ItemType == itemType)
            {
                item = slotItem;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetItemBySO(GameObject[] slots, ItemSO itemSO, out InventoryItem item)
    {
        item = null;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var slotItem = slot?.heldItem?.GetComponent<InventoryItem>();

            if (slotItem?.itemScriptableObject == itemSO)
            {
                item = slotItem;
                return true;
            }
        }

        return false;
    }
}