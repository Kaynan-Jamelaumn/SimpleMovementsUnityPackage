using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Specialized handler for armor equipment operations
public static class ArmorEquipmentHandler
{
    // Enhanced armor equipping with set bonus handling
    public static void EquipArmor(InventorySlot slot, InventoryItem armorItem, PlayerStatusController playerStatusController)
    {
        if (slot == null || armorItem?.itemScriptableObject == null) return;

        var armorSO = armorItem.itemScriptableObject as ArmorSO;
        if (armorSO == null) return;

        // Check if slot is compatible
        if (!IsSlotCompatibleWithArmor(slot, armorSO))
        {
            Debug.LogWarning($"Slot {slot.SlotType} is not compatible with armor {armorSO.name} ({armorSO.ArmorSlotType})");
            return;
        }

        // Handle equipment swap if slot is occupied
        if (slot.heldItem != null)
        {
            var currentItem = slot.heldItem.GetComponent<InventoryItem>();
            if (currentItem != null && currentItem.isEquipped)
            {
                UnequipArmor(currentItem, playerStatusController);
            }
        }

        // Equip the new armor
        armorItem.SetEquipped(true);
        armorSO.ApplyEquippedStats(true, playerStatusController);

        // Play audio effect
        PlayArmorEquipSound(armorSO, playerStatusController);

        Debug.Log($"Equipped armor: {armorSO.name} in slot {slot.SlotType}");
    }

    // Enhanced armor unequipping with set bonus handling
    public static void UnequipArmor(InventoryItem armorItem, PlayerStatusController playerStatusController)
    {
        if (armorItem?.itemScriptableObject == null) return;

        var armorSO = armorItem.itemScriptableObject as ArmorSO;
        if (armorSO == null) return;

        // Unequip the armor
        armorItem.SetEquipped(false);
        armorSO.ApplyEquippedStats(false, playerStatusController);

        // Play audio effect
        PlayArmorUnequipSound(armorSO, playerStatusController);

        Debug.Log($"Unequipped armor: {armorSO.name}");
    }

    // Check if a slot is compatible with a specific armor piece
    public static bool IsSlotCompatibleWithArmor(InventorySlot slot, ArmorSO armor)
    {
        if (slot == null || armor == null) return false;

        // Check for exact slot type match
        SlotType requiredSlotType = armor.GetSlotType();
        return slot.SlotType == requiredSlotType || slot.SlotType == SlotType.Common;
    }

    // Find the appropriate slot for an armor piece
    public static InventorySlot FindSlotForArmor(InventoryManager inventoryManager, ArmorSO armor)
    {
        if (inventoryManager?.Slots == null || armor == null) return null;

        SlotType requiredSlotType = armor.GetSlotType();

        // First try to find the exact slot type
        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.SlotType == requiredSlotType)
            {
                return slot;
            }
        }

        // If no specific slot found, try common slots
        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.SlotType == SlotType.Common && slot.heldItem == null)
            {
                return slot;
            }
        }

        return null;
    }

    // Get all equipped armor pieces
    public static List<InventoryItem> GetAllEquippedArmor(InventoryManager inventoryManager)
    {
        var equippedArmor = new List<InventoryItem>();

        if (inventoryManager?.Slots == null) return equippedArmor;

        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject is ArmorSO && inventoryItem.isEquipped)
            {
                equippedArmor.Add(inventoryItem);
            }
        }

        return equippedArmor;
    }

    // Handle armor switching between slots
    public static void SwitchArmor(InventorySlot fromSlot, InventorySlot toSlot, PlayerStatusController playerStatusController)
    {
        if (fromSlot?.heldItem == null || toSlot == null) return;

        var fromItem = fromSlot.heldItem.GetComponent<InventoryItem>();
        var fromArmor = fromItem?.itemScriptableObject as ArmorSO;

        if (fromArmor == null) return;

        // Check compatibility
        if (!IsSlotCompatibleWithArmor(toSlot, fromArmor)) return;

        bool wasEquipped = fromItem.isEquipped;

        // Handle item in target slot
        InventoryItem toItem = null;
        if (toSlot.heldItem != null)
        {
            toItem = toSlot.heldItem.GetComponent<InventoryItem>();
            if (toItem != null && toItem.isEquipped)
            {
                UnequipArmor(toItem, playerStatusController);
            }
        }

        // Move items
        GameObject tempItem = fromSlot.heldItem;
        fromSlot.SetHeldItem(toSlot.heldItem);
        toSlot.SetHeldItem(tempItem);

        // Re-equip if it was equipped before
        if (wasEquipped)
        {
            EquipArmor(toSlot, fromItem, playerStatusController);
        }

        // Re-equip the other item if it was equipped
        if (toItem != null && toItem.isEquipped && fromSlot.heldItem != null)
        {
            var toArmor = toItem.itemScriptableObject as ArmorSO;
            if (toArmor != null && IsSlotCompatibleWithArmor(fromSlot, toArmor))
            {
                EquipArmor(fromSlot, toItem, playerStatusController);
            }
        }
    }

    // Quick equip armor from inventory
    public static bool QuickEquipArmor(InventoryManager inventoryManager, ArmorSO armor, PlayerStatusController playerStatusController)
    {
        var targetSlot = FindSlotForArmor(inventoryManager, armor);
        if (targetSlot == null) return false;

        // Find the armor in inventory
        var armorItem = FindArmorInInventory(inventoryManager, armor);
        if (armorItem == null) return false;

        // Get the slot containing the armor
        var sourceSlot = FindSlotContaining(inventoryManager, armorItem);
        if (sourceSlot == null) return false;

        // Move and equip
        if (targetSlot.heldItem != null)
        {
            // Swap items
            SwitchArmor(sourceSlot, targetSlot, playerStatusController);
        }
        else
        {
            // Move to empty slot
            targetSlot.SetHeldItem(sourceSlot.heldItem);
            sourceSlot.SetHeldItem(null);
            EquipArmor(targetSlot, armorItem, playerStatusController);
        }

        return true;
    }

    // Quick unequip all armor
    public static void UnequipAllArmor(InventoryManager inventoryManager, PlayerStatusController playerStatusController)
    {
        var equippedArmor = GetAllEquippedArmor(inventoryManager);

        foreach (var armorItem in equippedArmor)
        {
            UnequipArmor(armorItem, playerStatusController);
        }

        Debug.Log($"Unequipped {equippedArmor.Count} armor pieces");
    }

    // Optimize armor equipment for maximum set bonuses
    public static void OptimizeArmorSets(InventoryManager inventoryManager, PlayerStatusController playerStatusController)
    {
        // Get all available armor
        var allArmor = GetAllArmorInInventory(inventoryManager);

        // Group by sets
        var armorBySets = allArmor.Where(item => (item.itemScriptableObject as ArmorSO)?.IsPartOfSet() == true)
                                .GroupBy(item => (item.itemScriptableObject as ArmorSO).BelongsToSet)
                                .ToDictionary(g => g.Key, g => g.ToList());

        // Find the set with the most pieces
        var bestSet = armorBySets.OrderByDescending(kvp => kvp.Value.Count).FirstOrDefault();

        if (bestSet.Key != null && bestSet.Value.Count >= bestSet.Key.MinimumPiecesForSet)
        {
            // Unequip current armor
            UnequipAllArmor(inventoryManager, playerStatusController);

            // Equip the best set
            foreach (var armorItem in bestSet.Value)
            {
                var armorSO = armorItem.itemScriptableObject as ArmorSO;
                if (armorSO != null)
                {
                    var targetSlot = FindSlotForArmor(inventoryManager, armorSO);
                    if (targetSlot != null)
                    {
                        var sourceSlot = FindSlotContaining(inventoryManager, armorItem);
                        if (sourceSlot != null && sourceSlot != targetSlot)
                        {
                            targetSlot.SetHeldItem(sourceSlot.heldItem);
                            sourceSlot.SetHeldItem(null);
                        }
                        EquipArmor(targetSlot, armorItem, playerStatusController);
                    }
                }
            }

            Debug.Log($"Optimized equipment for {bestSet.Key.SetName} set ({bestSet.Value.Count} pieces)");
        }
    }

    // Helper methods
    private static InventoryItem FindArmorInInventory(InventoryManager inventoryManager, ArmorSO armor)
    {
        if (inventoryManager?.Slots == null) return null;

        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject == armor)
            {
                return inventoryItem;
            }
        }

        return null;
    }

    private static InventorySlot FindSlotContaining(InventoryManager inventoryManager, InventoryItem item)
    {
        if (inventoryManager?.Slots == null || item == null) return null;

        foreach (var slotObj in inventoryManager.Slots)
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

    private static List<InventoryItem> GetAllArmorInInventory(InventoryManager inventoryManager)
    {
        var allArmor = new List<InventoryItem>();

        if (inventoryManager?.Slots == null) return allArmor;

        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject is ArmorSO)
            {
                allArmor.Add(inventoryItem);
            }
        }

        return allArmor;
    }

    private static void PlayArmorEquipSound(ArmorSO armor, PlayerStatusController playerStatusController)
    {
        if (armor.EquipArmorSound == null) return;

        var audioSource = playerStatusController.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(armor.EquipArmorSound);
        }
    }

    private static void PlayArmorUnequipSound(ArmorSO armor, PlayerStatusController playerStatusController)
    {
        if (armor.UnequipArmorSound == null) return;

        var audioSource = playerStatusController.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(armor.UnequipArmorSound);
        }
    }

    // Validation and debugging methods
    public static bool ValidateArmorEquipment(InventoryManager inventoryManager)
    {
        bool isValid = true;
        var equippedArmor = GetAllEquippedArmor(inventoryManager);

        // Check for duplicate slot types
        var slotTypes = new Dictionary<ArmorSlotType, int>();

        foreach (var armorItem in equippedArmor)
        {
            var armorSO = armorItem.itemScriptableObject as ArmorSO;
            if (armorSO != null)
            {
                if (slotTypes.ContainsKey(armorSO.ArmorSlotType))
                {
                    slotTypes[armorSO.ArmorSlotType]++;
                    Debug.LogWarning($"Multiple {armorSO.ArmorSlotType} pieces equipped");
                    isValid = false;
                }
                else
                {
                    slotTypes[armorSO.ArmorSlotType] = 1;
                }
            }
        }

        return isValid;
    }

    public static string GetEquipmentReport(InventoryManager inventoryManager)
    {
        var report = "=== Armor Equipment Report ===\n";
        var equippedArmor = GetAllEquippedArmor(inventoryManager);

        if (equippedArmor.Count == 0)
        {
            report += "No armor equipped\n";
            return report;
        }

        report += $"Total armor pieces: {equippedArmor.Count}\n\n";

        foreach (var armorItem in equippedArmor)
        {
            var armorSO = armorItem.itemScriptableObject as ArmorSO;
            if (armorSO != null)
            {
                string setInfo = armorSO.IsPartOfSet() ? $" (Set: {armorSO.BelongsToSet.SetName})" : " (No set)";
                report += $"{armorSO.ArmorSlotType}: {armorSO.name}{setInfo}\n";
                report += $"  Defense: {armorSO.DefenseValue}, Magic Defense: {armorSO.MagicDefenseValue}\n";

                if (armorSO.InherentTraits.Count > 0)
                {
                    report += $"  Traits: {string.Join(", ", armorSO.InherentTraits.Where(t => t != null).Select(t => t.Name))}\n";
                }
            }
        }

        return report;
    }


}