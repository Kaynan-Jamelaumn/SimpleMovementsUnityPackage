using UnityEngine;
using System.Collections.Generic;

// Utility class to reduce redundancy across inventory system
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

    // Weight management helpers
    public static void UpdatePlayerWeight(GameObject player, float weightChange)
    {
        var statusController = player?.GetComponent<PlayerStatusController>();
        var weightManager = statusController?.WeightManager;

        if (weightManager != null)
        {
            if (weightChange > 0)
                weightManager.AddWeight(weightChange);
            else
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
            toList.Add(fromList[lastIndex]);
            fromList.RemoveAt(lastIndex);
        }
    }

    // Safe GameObject destruction
    public static void SafeDestroy(GameObject obj)
    {
        if (obj != null)
            Object.Destroy(obj);
    }

    // UI state management
    public static void SetUIActive(GameObject uiElement, bool active)
    {
        if (uiElement != null && uiElement.activeSelf != active)
            uiElement.SetActive(active);
    }

    // Transform utilities
    public static void SetupItemTransform(Transform itemTransform, Transform parentTransform, Vector3 localPosition, Vector3 localScale)
    {
        if (itemTransform == null || parentTransform == null) return;

        itemTransform.SetParent(parentTransform);
        itemTransform.localPosition = localPosition;
        itemTransform.localScale = localScale;
        itemTransform.localRotation = Quaternion.identity;
    }

    // Inventory validation
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

    // Get available inventory space for specific item
    public static int GetAvailableSpace(GameObject[] slots, ItemSO itemSO)
    {
        int availableSpace = 0;

        // Count space in existing stacks
        foreach (var slot in FindStackableSlots(slots, itemSO))
        {
            var item = slot.heldItem.GetComponent<InventoryItem>();
            availableSpace += item.GetAvailableStackSpace();
        }

        // Count empty slots
        int emptySlots = FindEmptySlots(slots).Count;
        availableSpace += emptySlots * itemSO.StackMax;

        return availableSpace;
    }

    // Debug helpers
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