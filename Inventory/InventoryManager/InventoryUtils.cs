using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Centralized utility class for inventory operationsusing UnityEngine;
public static class InventoryUtils
{
    //  slot compatibility checking for armor
    public static bool IsCompatibleSlot(InventorySlot slot, ItemType itemType)
    {
        if (slot == null) return false;

        // Handle armor compatibility
        if (itemType == ItemType.Armor || SlotTypeHelper.ItemTypeToSlotType(itemType) != SlotType.Common)
        {
            var requiredSlotType = SlotTypeHelper.ItemTypeToSlotType(itemType);
            return slot.SlotType == SlotType.Common || slot.SlotType == requiredSlotType;
        }

        return slot.SlotType == SlotType.Common || slot.SlotType == (SlotType)itemType;
    }

    // compatibility check for armor pieces
    public static bool IsCompatibleSlotForArmor(InventorySlot slot, ArmorSO armor)
    {
        if (slot == null || armor == null) return false;

        var requiredSlotType = armor.GetSlotType();
        return slot.SlotType == SlotType.Common || slot.SlotType == requiredSlotType;
    }

    // Find slots compatible with specific armor pieces
    public static List<InventorySlot> FindCompatibleSlotsForArmor(GameObject[] slots, ArmorSO armor)
    {
        var compatibleSlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (IsCompatibleSlotForArmor(slot, armor))
            {
                compatibleSlots.Add(slot);
            }
        }

        return compatibleSlots;
    }

    // Find equipped armor slots
    public static List<InventorySlot> FindEquippedArmorSlots(GameObject[] slots)
    {
        var equippedSlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject is ArmorSO && inventoryItem.isEquipped)
            {
                equippedSlots.Add(slot);
            }
        }

        return equippedSlots;
    }

    // Find all armor pieces in inventory
    public static List<InventoryItem> FindAllArmor(GameObject[] slots)
    {
        var armorItems = new List<InventoryItem>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject is ArmorSO)
            {
                armorItems.Add(inventoryItem);
            }
        }

        return armorItems;
    }

    // Find armor pieces belonging to specific sets
    public static Dictionary<ArmorSet, List<InventoryItem>> FindArmorBySet(GameObject[] slots)
    {
        var armorBySet = new Dictionary<ArmorSet, List<InventoryItem>>();
        var allArmor = FindAllArmor(slots);

        foreach (var armor in allArmor)
        {
            var armorSO = armor.itemScriptableObject as ArmorSO;
            if (armorSO?.IsPartOfSet() == true)
            {
                var armorSet = armorSO.BelongsToSet;
                if (!armorBySet.ContainsKey(armorSet))
                {
                    armorBySet[armorSet] = new List<InventoryItem>();
                }
                armorBySet[armorSet].Add(armor);
            }
        }

        return armorBySet;
    }

    // Find best available slot for armor piece
    public static InventorySlot FindBestSlotForArmor(GameObject[] slots, ArmorSO armor)
    {
        var compatibleSlots = FindCompatibleSlotsForArmor(slots, armor);

        // Prefer exact slot type match
        var exactMatch = compatibleSlots.FirstOrDefault(s => s.SlotType == armor.GetSlotType());
        if (exactMatch != null && exactMatch.heldItem == null)
        {
            return exactMatch;
        }

        // Then prefer empty compatible slots
        var emptySlot = compatibleSlots.FirstOrDefault(s => s.heldItem == null);
        if (emptySlot != null)
        {
            return emptySlot;
        }

        // Finally, return any compatible slot
        return compatibleSlots.FirstOrDefault();
    }

    // Slot validation with armor support
    public static bool IsValidDropTarget(InventorySlot slot, ItemType itemType)
    {
        return slot != null && IsCompatibleSlot(slot, itemType);
    }

    public static bool IsValidArmorDropTarget(InventorySlot slot, ArmorSO armor)
    {
        return slot != null && IsCompatibleSlotForArmor(slot, armor);
    }

    public static bool IsValidSlotIndex(GameObject[] slots, int index)
    {
        return slots != null && index >= 0 && index < slots.Length && slots[index] != null;
    }

    //  empty slot finding with armor slot prioritization
    public static List<InventorySlot> FindEmptySlots(GameObject[] slots, SlotType requiredType = SlotType.Common, bool prioritizeArmorSlots = false)
    {
        var emptySlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null)
            {
                if (requiredType == SlotType.Common || slot.SlotType == requiredType)
                {
                    emptySlots.Add(slot);
                }
            }
        }

        // Prioritize armor slots if requested
        if (prioritizeArmorSlots)
        {
            return emptySlots.OrderByDescending(s => SlotTypeHelper.IsArmorSlot(s.SlotType)).ToList();
        }

        return emptySlots;
    }

    // Find empty armor-specific slots
    public static List<InventorySlot> FindEmptyArmorSlots(GameObject[] slots)
    {
        var emptyArmorSlots = new List<InventorySlot>();

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null && SlotTypeHelper.IsArmorSlot(slot.SlotType))
            {
                emptyArmorSlots.Add(slot);
            }
        }

        return emptyArmorSlots;
    }

    // Find slots with stackable items (excluding armor)
    public static List<InventorySlot> FindStackableSlots(GameObject[] slots, ItemSO itemSO)
    {
        var stackableSlots = new List<InventorySlot>();

        // Armor cannot be stacked
        if (itemSO is ArmorSO) return stackableSlots;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var heldItem = slot?.heldItem?.GetComponent<InventoryItem>();

            if (heldItem != null &&
                heldItem.itemScriptableObject == itemSO &&
                heldItem.stackCurrent < heldItem.stackMax &&
                !(heldItem.itemScriptableObject is ArmorSO)) // Ensure no armor stacking
            {
                stackableSlots.Add(slot);
            }
        }

        return stackableSlots;
    }

    //  first available slot finding with armor awareness
    public static InventorySlot FindFirstAvailableSlot(GameObject[] slots, ItemSO itemSO)
    {
        // Handle armor pieces specially
        if (itemSO is ArmorSO armor)
        {
            return FindBestSlotForArmor(slots, armor);
        }

        // First try to find stackable slots
        var stackableSlots = FindStackableSlots(slots, itemSO);
        if (stackableSlots.Count > 0)
            return stackableSlots[0];

        // Then find empty slots
        var emptySlots = FindEmptySlots(slots);
        return emptySlots.Count > 0 ? emptySlots[0] : null;
    }

    //  weight management with armor consideration
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

    // Calculate total inventory weight including armor
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

    // Calculate armor-specific weight
    public static float CalculateArmorWeight(GameObject[] slots)
    {
        float armorWeight = 0f;
        var armorItems = FindAllArmor(slots);

        foreach (var armor in armorItems)
        {
            armorWeight += armor.totalWeight;
        }

        return armorWeight;
    }

    //  item counting with armor support
    public static int GetItemCount(GameObject[] slots, ItemSO itemSO)
    {
        int count = 0;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject == itemSO)
            {
                // Armor pieces count as 1 regardless of stack
                if (itemSO is ArmorSO)
                    count += 1;
                else
                    count += item.stackCurrent;
            }
        }

        return count;
    }

    // Count equipped armor pieces
    public static int GetEquippedArmorCount(GameObject[] slots)
    {
        return FindEquippedArmorSlots(slots).Count;
    }

    // Count armor pieces by slot type
    public static Dictionary<ArmorSlotType, int> GetArmorCountBySlotType(GameObject[] slots)
    {
        var countBySlot = new Dictionary<ArmorSlotType, int>();
        var allArmor = FindAllArmor(slots);

        foreach (var armor in allArmor)
        {
            var armorSO = armor.itemScriptableObject as ArmorSO;
            if (armorSO != null)
            {
                if (countBySlot.ContainsKey(armorSO.ArmorSlotType))
                    countBySlot[armorSO.ArmorSlotType]++;
                else
                    countBySlot[armorSO.ArmorSlotType] = 1;
            }
        }

        return countBySlot;
    }

    //  space availability checking
    public static int GetAvailableSpace(GameObject[] slots, ItemSO itemSO)
    {
        // Armor pieces need empty compatible slots
        if (itemSO is ArmorSO armor)
        {
            var compatibleSlots = FindCompatibleSlotsForArmor(slots, armor);
            return compatibleSlots.Count(slot => slot.heldItem == null);
        }

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

    // Check space for armor specifically
    public static bool HasSpaceForArmor(GameObject[] slots, ArmorSO armor)
    {
        var compatibleSlots = FindCompatibleSlotsForArmor(slots, armor);
        return compatibleSlots.Any(slot => slot.heldItem == null);
    }

    //  item removal with armor support
    public static int RemoveItems(GameObject[] slots, ItemSO itemSO, int amountToRemove)
    {
        // Armor pieces are removed individually
        if (itemSO is ArmorSO)
        {
            int removedCount = 0;
            var armorItems = FindAllArmor(slots);

            foreach (var armor in armorItems)
            {
                if (armor.itemScriptableObject == itemSO && removedCount < amountToRemove)
                {
                    var slot = FindSlotContaining(slots, armor);
                    if (slot != null)
                    {
                        // Unequip if equipped
                        if (armor.isEquipped)
                        {
                            var playerController = Object.FindFirstObjectByType<PlayerStatusController>();
                            if (playerController != null)
                            {
                                ArmorEquipmentHandler.UnequipArmor(armor, playerController);
                            }
                        }

                        slot.heldItem = null;
                        SafeDestroy(armor.gameObject);
                        removedCount++;
                    }
                }
            }

            return removedCount;
        }

        // Handle non-armor items with original logic
        int removedCount_NonArmor = 0;

        foreach (var slotObj in slots)
        {
            if (slotObj == null || removedCount_NonArmor >= amountToRemove) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject == itemSO)
            {
                int removeFromThisStack = Mathf.Min(item.stackCurrent, amountToRemove - removedCount_NonArmor);

                if (item.RemoveFromStack(removeFromThisStack))
                {
                    removedCount_NonArmor += removeFromThisStack;

                    // Destroy item if stack is empty
                    if (item.IsEmpty())
                    {
                        slot.heldItem = null;
                        SafeDestroy(item.gameObject);
                    }
                }
            }
        }

        return removedCount_NonArmor;
    }

    // Helper method to find slot containing specific item
    private static InventorySlot FindSlotContaining(GameObject[] slots, InventoryItem item)
    {
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem?.GetComponent<InventoryItem>() == item)
            {
                return slot;
            }
        }

        return null;
    }

    //  inventory state utilities
    public static int CountItemsOfType(GameObject[] slots, ItemType itemType)
    {
        int count = 0;
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject?.ItemType == itemType)
            {
                // Count armor pieces as 1 each
                if (itemType == ItemType.Armor || itemType == ItemType.Helmet ||
                    itemType == ItemType.Boots || itemType == ItemType.Gloves ||
                    itemType == ItemType.Shield)
                    count += 1;
                else
                    count += item.stackCurrent;
            }
        }
        return count;
    }

    // Get all items of specific type with armor awareness
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

    //  batch operations
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
                int countToAdd = (item.itemScriptableObject is ArmorSO) ? 1 : item.stackCurrent;

                if (summary.ContainsKey(item.itemScriptableObject))
                    summary[item.itemScriptableObject] += countToAdd;
                else
                    summary[item.itemScriptableObject] = countToAdd;
            }
        }

        return summary;
    }

    //  armor-specific summary
    public static Dictionary<ArmorSet, int> GetArmorSetSummary(GameObject[] slots)
    {
        var summary = new Dictionary<ArmorSet, int>();
        var armorBySet = FindArmorBySet(slots);

        foreach (var kvp in armorBySet)
        {
            summary[kvp.Key] = kvp.Value.Count;
        }

        return summary;
    }

    // Performance utilities with armor optimization
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

    //  debug helpers with armor support
    public static void LogInventoryState(GameObject[] slots, string context = "")
    {
        Debug.Log($"=== Inventory State {context} ===");

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i]?.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item != null)
            {
                string itemName = item.itemScriptableObject.Name;
                string itemInfo = (item.itemScriptableObject is ArmorSO armor)
                    ? $" (Armor: {armor.ArmorSlotType}, Equipped: {item.isEquipped})"
                    : $" x{item.stackCurrent}";

                Debug.Log($"Slot {i} ({slot.SlotType}): {itemName}{itemInfo}");
            }
            else
            {
                Debug.Log($"Slot {i} ({slot?.SlotType}): Empty");
            }
        }
    }

    public static void LogArmorSummary(GameObject[] slots)
    {
        var armorItems = FindAllArmor(slots);
        var equippedArmor = armorItems.Where(a => a.isEquipped).ToList();
        var armorBySet = FindArmorBySet(slots);

        Debug.Log("=== Armor Summary ===");
        Debug.Log($"Total armor pieces: {armorItems.Count}");
        Debug.Log($"Equipped armor pieces: {equippedArmor.Count}");

        if (armorBySet.Count > 0)
        {
            Debug.Log("Armor sets:");
            foreach (var kvp in armorBySet)
            {
                var equippedInSet = kvp.Value.Count(a => a.isEquipped);
                Debug.Log($"  {kvp.Key.SetName}: {equippedInSet}/{kvp.Value.Count} equipped");
            }
        }

        Debug.Log($"Total armor weight: {CalculateArmorWeight(slots):F2}");
    }

    //  quick access with armor support
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

    public static bool TryGetEquippedArmor(GameObject[] slots, ArmorSlotType slotType, out InventoryItem armor)
    {
        armor = null;
        var equippedArmor = FindEquippedArmorSlots(slots);

        foreach (var slot in equippedArmor)
        {
            var item = slot.heldItem.GetComponent<InventoryItem>();
            var armorSO = item?.itemScriptableObject as ArmorSO;

            if (armorSO?.ArmorSlotType == slotType)
            {
                armor = item;
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

    // Safe operations
    public static void SafeDestroy(GameObject obj)
    {
        if (obj != null) Object.Destroy(obj);
    }

    //  slot utilities
    public static bool IsSlotEmpty(InventorySlot slot)
    {
        return slot?.heldItem == null;
    }

    public static bool SlotHasItem(InventorySlot slot, ItemSO itemSO)
    {
        var item = slot?.heldItem?.GetComponent<InventoryItem>();
        return item?.itemScriptableObject == itemSO;
    }

    public static bool SlotHasArmor(InventorySlot slot, ArmorSO armor)
    {
        var item = slot?.heldItem?.GetComponent<InventoryItem>();
        return item?.itemScriptableObject == armor;
    }

    //  validation helpers
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

    public static bool ValidateArmorEquipment(GameObject[] slots, ArmorSO armor)
    {
        if (armor == null)
        {
            Debug.LogError("Armor is null");
            return false;
        }

        var compatibleSlots = FindCompatibleSlotsForArmor(slots, armor);
        if (compatibleSlots.Count == 0)
        {
            Debug.LogError($"No compatible slots found for armor {armor.name} ({armor.ArmorSlotType})");
            return false;
        }

        return true;
    }

    // Check if there's enough space for items
    public static bool HasEnoughSpace(GameObject[] slots, ItemSO itemSO, int requiredQuantity)
    {
        if (itemSO == null || requiredQuantity <= 0) return true;

        int availableSpace = GetAvailableSpace(slots, itemSO);
        return availableSpace >= requiredQuantity;
    }

    // Check if inventory has enough items
    public static bool HasEnoughItems(GameObject[] slots, ItemSO itemSO, int requiredAmount)
    {
        if (itemSO == null || requiredAmount <= 0) return true;

        int currentCount = GetItemCount(slots, itemSO);
        return currentCount >= requiredAmount;
    }

    // Transfer durability between lists
    public static void TransferDurabilityList(List<int> sourceList, List<int> targetList, int amount)
    {
        if (sourceList == null || targetList == null || amount <= 0) return;

        int transferCount = Mathf.Min(amount, sourceList.Count);

        for (int i = 0; i < transferCount; i++)
        {
            if (sourceList.Count > 0)
            {
                int lastIndex = sourceList.Count - 1;
                targetList.Add(sourceList[lastIndex]);
                sourceList.RemoveAt(lastIndex);
            }
        }
    }

    // Find first available slot that can hold the item
    public static InventorySlot FindFirstAvailableSlotForItem(GameObject[] slots, ItemSO itemSO, int quantity = 1)
    {
        if (itemSO == null) return null;

        // For armor, find appropriate slot
        if (itemSO is ArmorSO armor)
        {
            return FindBestSlotForArmor(slots, armor);
        }

        // For stackable items, try existing stacks first
        var stackableSlots = FindStackableSlots(slots, itemSO);
        foreach (var slot in stackableSlots)
        {
            var item = slot.heldItem.GetComponent<InventoryItem>();
            if (item != null && (item.stackMax - item.stackCurrent) >= quantity)
            {
                return slot;
            }
        }

        // Then try empty slots
        var emptySlots = FindEmptySlots(slots);
        return emptySlots.FirstOrDefault();
    }

    // Get weight per item for any ItemSO
    public static float GetItemWeight(ItemSO itemSO, int quantity = 1)
    {
        return itemSO?.Weight * quantity ?? 0f;
    }

    // Check if slot can hold specific quantity of item
    public static bool CanSlotHoldQuantity(InventorySlot slot, ItemSO itemSO, int quantity)
    {
        if (slot == null || itemSO == null || quantity <= 0) return false;

        // Empty slot
        if (slot.heldItem == null)
        {
            return IsCompatibleSlot(slot, itemSO.ItemType) && quantity <= itemSO.StackMax;
        }

        // Slot with item
        var item = slot.heldItem.GetComponent<InventoryItem>();
        if (item?.itemScriptableObject == itemSO)
        {
            return (item.stackCurrent + quantity) <= item.stackMax;
        }

        return false;
    }

    // Get total stack space available for item type
    public static int GetTotalAvailableStackSpace(GameObject[] slots, ItemSO itemSO)
    {
        if (itemSO == null) return 0;

        int totalSpace = 0;

        // Count space in existing stacks
        var stackableSlots = FindStackableSlots(slots, itemSO);
        foreach (var slot in stackableSlots)
        {
            var item = slot.heldItem.GetComponent<InventoryItem>();
            if (item != null)
            {
                totalSpace += (item.stackMax - item.stackCurrent);
            }
        }

        // Count empty compatible slots
        var emptySlots = FindEmptySlots(slots);
        foreach (var slot in emptySlots)
        {
            if (IsCompatibleSlot(slot, itemSO.ItemType))
            {
                totalSpace += itemSO.StackMax;
            }
        }

        return totalSpace;
    }

    // Validate item placement in slot
    public static bool ValidateItemPlacement(InventorySlot slot, ItemSO itemSO, int quantity = 1)
    {
        if (slot == null || itemSO == null) return false;

        // Check slot type compatibility
        if (!IsCompatibleSlot(slot, itemSO.ItemType)) return false;

        // Check if slot can hold the quantity
        return CanSlotHoldQuantity(slot, itemSO, quantity);
    }

    // Get detailed inventory report
    public static string GetDetailedInventoryReport(GameObject[] slots)
    {
        var report = "=== Detailed Inventory Report ===\n";
        var summary = GetInventorySummary(slots);

        report += $"Total unique items: {summary.Count}\n";
        report += $"Total inventory weight: {CalculateInventoryWeight(slots):F2}\n";
        report += $"Total armor weight: {CalculateArmorWeight(slots):F2}\n\n";

        report += "Item breakdown:\n";
        foreach (var kvp in summary.OrderBy(x => x.Key.ItemType).ThenBy(x => x.Key.Name))
        {
            string itemType = kvp.Key.ItemType.ToString();
            string itemName = kvp.Key.Name;
            int count = kvp.Value;
            float weight = kvp.Key.Weight * count;

            report += $"  {itemType}: {itemName} x{count} (Weight: {weight:F2})\n";
        }

        // Armor set summary
        var armorBySet = FindArmorBySet(slots);
        if (armorBySet.Count > 0)
        {
            report += "\nArmor Sets:\n";
            foreach (var kvp in armorBySet)
            {
                var equippedCount = kvp.Value.Count(a => a.isEquipped);
                report += $"  {kvp.Key.SetName}: {equippedCount}/{kvp.Value.Count} equipped\n";
            }
        }

        return report;
    }

    // Clean up inventory by removing empty items
    public static void CleanupInventory(GameObject[] slots)
    {
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var item = slot.heldItem.GetComponent<InventoryItem>();
            if (item != null && item.IsEmpty())
            {
                slot.heldItem = null;
                SafeDestroy(item.gameObject);
            }
        }
    }

    // Defragment inventory by consolidating stacks
    public static void DefragmentInventory(GameObject[] slots)
    {
        var itemGroups = new Dictionary<ItemSO, List<InventoryItem>>();

        // Group items by type
        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            var item = slot?.heldItem?.GetComponent<InventoryItem>();

            if (item?.itemScriptableObject != null && !(item.itemScriptableObject is ArmorSO))
            {
                if (!itemGroups.ContainsKey(item.itemScriptableObject))
                {
                    itemGroups[item.itemScriptableObject] = new List<InventoryItem>();
                }
                itemGroups[item.itemScriptableObject].Add(item);
            }
        }

        // Consolidate stacks for each item type
        foreach (var kvp in itemGroups)
        {
            var items = kvp.Value.OrderByDescending(i => i.stackCurrent).ToList();

            for (int i = 0; i < items.Count - 1; i++)
            {
                var sourceItem = items[i];
                if (sourceItem.stackCurrent >= sourceItem.stackMax) continue;

                for (int j = i + 1; j < items.Count; j++)
                {
                    var targetItem = items[j];
                    if (targetItem.stackCurrent <= 0) continue;

                    int spaceAvailable = sourceItem.stackMax - sourceItem.stackCurrent;
                    int amountToTransfer = Mathf.Min(spaceAvailable, targetItem.stackCurrent);

                    if (amountToTransfer > 0)
                    {
                        // Transfer items
                        sourceItem.AddToStack(amountToTransfer);
                        targetItem.RemoveFromStack(amountToTransfer);

                        // Transfer durability
                        TransferDurabilityList(targetItem.DurabilityList, sourceItem.DurabilityList, amountToTransfer);

                        // If target item is empty, remove it
                        if (targetItem.IsEmpty())
                        {
                            var targetSlot = FindSlotContaining(slots, targetItem);
                            if (targetSlot != null)
                            {
                                targetSlot.heldItem = null;
                                SafeDestroy(targetItem.gameObject);
                            }
                        }

                        spaceAvailable -= amountToTransfer;
                        if (spaceAvailable <= 0) break;
                    }
                }
            }
        }
    }


}