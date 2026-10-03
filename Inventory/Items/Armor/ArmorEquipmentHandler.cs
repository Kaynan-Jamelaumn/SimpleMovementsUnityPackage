using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Armor operations on the inventory UI: moving pieces into and out of equipment slots, quick-equip, unequip all,
/// wearing the most complete set. It only MOVES items between slots; what an item does while worn is applied by the
/// <see cref="EquipmentManager"/>, which syncs with the equipment slots after every move (so a piece can never be
/// applied twice or stay applied after it left its slot).
/// </summary>
public static class ArmorEquipmentHandler
{
    /// <summary>Tells the character's equipment manager that the equipment slots changed.</summary>
    public static void NotifyEquipmentChanged(PlayerStatusController playerStatusController)
    {
        if (playerStatusController == null)
            return;
        EquipmentManager eq = EquipmentManager.For(playerStatusController);
        if (eq != null && eq.IsInitialized)
            eq.SyncFromInventory();
        else
            eq?.RequestSync();
    }

    /// <summary>Called after an armor piece was placed in <paramref name="slot"/>: syncs the equipment.</summary>
    public static void EquipArmor(InventorySlot slot, InventoryItem armorItem, PlayerStatusController playerStatusController)
    {
        if (slot == null || !(armorItem.Live()?.itemScriptableObject is ArmorSO armorSO))
            return;
        if (!IsSlotCompatibleWithArmor(slot, armorSO))
        {
            Debug.LogWarning($"[Armor] Slot {slot.SlotType} does not fit {armorSO.Name} ({armorSO.ArmorSlotType}).", slot);
            return;
        }
        NotifyEquipmentChanged(playerStatusController);
    }

    /// <summary>Called after an armor piece left its equipment slot: syncs the equipment.</summary>
    public static void UnequipArmor(InventoryItem armorItem, PlayerStatusController playerStatusController)
    {
        if (!(armorItem.Live()?.itemScriptableObject is ArmorSO))
            return;
        NotifyEquipmentChanged(playerStatusController);
    }

    // Check if a slot is compatible with a specific armor piece
    public static bool IsSlotCompatibleWithArmor(InventorySlot slot, ArmorSO armor)
    {
        if (slot == null || armor == null) return false;
        return SlotTypeHelper.CanPlace(armor, slot.SlotType);
    }

    /// <summary>The equipment slot for a piece (an empty one first), or an empty inventory slot, or null.</summary>
    public static InventorySlot FindSlotForArmor(InventoryManager inventoryManager, ArmorSO armor)
    {
        if (inventoryManager == null || armor == null) return null;
        SlotType required = armor.GetSlotType();

        InventorySlot occupied = null;
        foreach (InventorySlot slot in inventoryManager.EquipmentSlots)
        {
            if (slot == null || slot.SlotType != required)
                continue;
            if (slot.heldItem == null)
                return slot;
            if (occupied == null)
                occupied = slot;
        }
        return occupied != null ? occupied : FindEmptyInventorySlot(inventoryManager, armor);
    }

    /// <summary>Inventory items currently worn as armor.</summary>
    public static List<InventoryItem> GetAllEquippedArmor(InventoryManager inventoryManager)
    {
        var result = new List<InventoryItem>();
        EquipmentManager eq = ArmorSetUtils.GetEquipment(inventoryManager);
        if (eq == null) return result;
        foreach (EquipmentManager.Entry e in eq.Entries)
            if (e.Item is ArmorSO && e.InventoryItem != null)
                result.Add(e.InventoryItem);
        return result;
    }

    /// <summary>Swaps the items of two slots (when both fit), then syncs the equipment.</summary>
    public static void SwitchArmor(InventorySlot fromSlot, InventorySlot toSlot, PlayerStatusController playerStatusController)
    {
        if (fromSlot.Live()?.heldItem == null || toSlot == null || fromSlot == toSlot) return;

        var fromItem = fromSlot.heldItem.GetComponent<InventoryItem>();
        var toItem = toSlot.heldItem != null ? toSlot.heldItem.GetComponent<InventoryItem>() : null;
        if (fromItem == null || !SlotTypeHelper.CanPlace(fromItem.itemScriptableObject, toSlot.SlotType))
            return;
        if (toItem != null && !SlotTypeHelper.CanPlace(toItem.itemScriptableObject, fromSlot.SlotType))
            return;

        GameObject moving = fromSlot.heldItem;
        GameObject other = toSlot.heldItem;
        fromSlot.SetHeldItem(other);
        toSlot.SetHeldItem(moving);
        NotifyEquipmentChanged(playerStatusController);
    }

    // Quick equip armor from inventory
    public static bool QuickEquipArmor(InventoryManager inventoryManager, ArmorSO armor, PlayerStatusController playerStatusController)
    {
        var armorItem = FindArmorInInventory(inventoryManager, armor);
        if (armorItem == null) return false;
        var sourceSlot = FindSlotContaining(inventoryManager, armorItem);
        if (sourceSlot == null) return false;

        InventorySlot target = null;
        SlotType required = armor.GetSlotType();
        foreach (InventorySlot slot in inventoryManager.EquipmentSlots)
        {
            if (slot == null || slot.SlotType != required) continue;
            if (slot == sourceSlot) return true; // already worn
            if (target == null || (target.heldItem != null && slot.heldItem == null))
                target = slot;
        }
        if (target == null) return false;

        SwitchArmor(sourceSlot, target, playerStatusController);
        return true;
    }

    /// <summary>Moves every worn armor piece to free inventory slots (pieces that do not fit stay worn).</summary>
    public static void UnequipAllArmor(InventoryManager inventoryManager, PlayerStatusController playerStatusController)
    {
        if (inventoryManager == null) return;
        int moved = 0;
        foreach (InventorySlot slot in inventoryManager.EquipmentSlots)
        {
            if (slot.Live()?.heldItem == null || !(slot.heldItem.GetComponent<InventoryItem>()?.itemScriptableObject is ArmorSO))
                continue;
            // The bag decides where it fits (the grid inventory places it by its size).
            InventoryItem piece = slot.heldItem.GetComponent<InventoryItem>();
            if (!inventoryManager.PlaceInBag(piece))
            {
                Debug.LogWarning("[Armor] No free inventory space to unequip into.");
                break;
            }
            moved++;
        }
        if (moved > 0)
            NotifyEquipmentChanged(playerStatusController);
    }

    /// <summary>Wears the set with the most different pieces in the inventory (if it reaches its minimum).</summary>
    public static void OptimizeArmorSets(InventoryManager inventoryManager, PlayerStatusController playerStatusController)
    {
        var allArmor = GetAllArmorInInventory(inventoryManager);
        var bySet = allArmor
            .Where(item => (item.itemScriptableObject as ArmorSO)?.BelongsToSet != null)
            .GroupBy(item => ((ArmorSO)item.itemScriptableObject).BelongsToSet)
            .Select(g => new { set = g.Key, items = g.GroupBy(i => i.itemScriptableObject).Select(x => x.First()).ToList() })
            .OrderByDescending(x => x.items.Count)
            .FirstOrDefault();

        if (bySet == null || bySet.items.Count < bySet.set.MinimumPiecesForSet)
            return;

        foreach (InventoryItem item in bySet.items)
            QuickEquipArmor(inventoryManager, (ArmorSO)item.itemScriptableObject, playerStatusController);
        Debug.Log($"[Armor] Equipped the {bySet.set.SetName} set ({bySet.items.Count} pieces).");
    }

    // Helper methods
    private static IEnumerable<InventorySlot> AllSlots(InventoryManager inventoryManager)
    {
        if (inventoryManager == null) yield break;
        if (inventoryManager.Slots != null)
            foreach (GameObject go in inventoryManager.Slots)
                if (go != null && go.TryGetComponent(out InventorySlot s)) yield return s;
        if (inventoryManager.HotbarSlots != null)
            foreach (GameObject go in inventoryManager.HotbarSlots)
                if (go != null && go.TryGetComponent(out InventorySlot s)) yield return s;
        foreach (InventorySlot s in inventoryManager.EquipmentSlots)
            if (s != null) yield return s;
    }

    /// <summary>A free place in the bag for <paramref name="item"/> (grid-aware: covered grid cells are not free).</summary>
    private static InventorySlot FindEmptyInventorySlot(InventoryManager inventoryManager, ItemSO item)
    {
        if (inventoryManager == null) return null;
        InventorySlot s = inventoryManager.FindFreeSlotFor(item, false);
        return s != null && !s.IsHotbarSlot ? s : null;
    }

    private static InventoryItem FindArmorInInventory(InventoryManager inventoryManager, ArmorSO armor)
    {
        foreach (InventorySlot slot in AllSlots(inventoryManager))
        {
            var item = slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
            if (item != null && item.itemScriptableObject == armor)
                return item;
        }
        return null;
    }

    private static InventorySlot FindSlotContaining(InventoryManager inventoryManager, InventoryItem item)
    {
        foreach (InventorySlot slot in AllSlots(inventoryManager))
            if (slot.heldItem != null && slot.heldItem.GetComponent<InventoryItem>() == item)
                return slot;
        return null;
    }

    private static List<InventoryItem> GetAllArmorInInventory(InventoryManager inventoryManager)
    {
        var allArmor = new List<InventoryItem>();
        foreach (InventorySlot slot in AllSlots(inventoryManager))
        {
            var item = slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
            if (item.Live()?.itemScriptableObject is ArmorSO)
                allArmor.Add(item);
        }
        return allArmor;
    }

    /// <summary>Warns when two pieces are worn in the same slot type more times than there are slots (should not happen).</summary>
    public static bool ValidateArmorEquipment(InventoryManager inventoryManager)
    {
        bool isValid = true;
        var slotCounts = new Dictionary<SlotType, int>();
        foreach (InventorySlot s in inventoryManager.EquipmentSlots)
            if (s != null) slotCounts[s.SlotType] = (slotCounts.TryGetValue(s.SlotType, out int n) ? n : 0) + 1;

        foreach (var group in GetAllEquippedArmor(inventoryManager).GroupBy(i => ((ArmorSO)i.itemScriptableObject).GetSlotType()))
        {
            slotCounts.TryGetValue(group.Key, out int available);
            if (group.Count() > available)
            {
                Debug.LogWarning($"[Armor] {group.Count()} pieces worn as {group.Key} but only {available} slot(s) exist.");
                isValid = false;
            }
        }
        return isValid;
    }

    public static string GetEquipmentReport(InventoryManager inventoryManager)
    {
        var report = "=== Armor Equipment Report ===\n";
        var equippedArmor = GetAllEquippedArmor(inventoryManager);

        if (equippedArmor.Count == 0)
            return report + "No armor equipped\n";

        report += $"Total armor pieces: {equippedArmor.Count}\n\n";
        foreach (var armorItem in equippedArmor)
        {
            var armorSO = (ArmorSO)armorItem.itemScriptableObject;
            string setInfo = armorSO.IsPartOfSet() ? $" (Set: {armorSO.BelongsToSet.SetName})" : " (No set)";
            report += $"{armorSO.ArmorSlotType}: {armorSO.Name}{setInfo}\n";
            report += $"  Defense: {armorSO.GetEffectiveDefense():0.#}, Magic Defense: {armorSO.GetEffectiveMagicDefense():0.#}\n";
            if (armorSO.InherentTraits.Count > 0)
                report += $"  Traits: {string.Join(", ", armorSO.InherentTraits.Where(t => t != null).Select(t => t.Name))}\n";
        }
        return report;
    }
}
