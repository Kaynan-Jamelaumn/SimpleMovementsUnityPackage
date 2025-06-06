using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Centralized utility class for inventory operations - cleaner and more maintainable
public static class InventoryUtils
{
    // Slot compatibility checking
    public static bool IsCompatibleSlot(InventorySlot slot, ItemType itemType)
    {
        if (slot == null) return false;
        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
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

    // Enhanced: Find first available slot (stackable or empty)
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

    // Enhanced: Find empty slot (GameObject version for backward compatibility)
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

    // Enhanced: Get available space for specific item
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

    // Enhanced: Check if enough space exists
    public static bool HasEnoughSpace(GameObject[] slots, ItemSO itemSO, int requiredQuantity)
    {
        return GetAvailableSpace(slots, itemSO) >= requiredQuantity;
    }

    // Enhanced: Check if enough items exist
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
                        Object.Destroy(item.gameObject);
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

    // Enhanced: Durability utilities
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

    // Enhanced: Slot utilities
    public static bool IsSlotEmpty(InventorySlot slot)
    {
        return slot?.heldItem == null;
    }

    public static bool SlotHasItem(InventorySlot slot, ItemSO itemSO)
    {
        var item = slot?.heldItem?.GetComponent<InventoryItem>();
        return item?.itemScriptableObject == itemSO;
    }

    // Enhanced: Validation helpers
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

    // Enhanced: Debug helpers
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
}