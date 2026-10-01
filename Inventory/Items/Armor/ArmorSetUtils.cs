using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Queries about the armor a character wears and its sets, for UI and tools. Everything is read from the
/// <see cref="EquipmentManager"/> (what is really equipped) and the <see cref="ArmorSetManager"/> (which bonuses are
/// really active). They used to scan only the inventory grid, which never contains the equipment slots, so they
/// reported no equipped armor.
/// </summary>
public static class ArmorSetUtils
{
    /// <summary>The equipment manager of the inventory's player (null when there is none).</summary>
    public static EquipmentManager GetEquipment(InventoryManager inventoryManager)
    {
        if (inventoryManager == null)
            return null;
        if (inventoryManager.Equipment != null)
            return inventoryManager.Equipment;
        Component anchor = inventoryManager.Player != null ? (Component)inventoryManager.Player.transform : inventoryManager;
        return EquipmentManager.For(anchor, false);
    }

    // Get all equipped armor sets from inventory
    public static Dictionary<ArmorSet, List<ArmorSO>> GetEquippedArmorSets(InventoryManager inventoryManager)
    {
        var equippedSets = new Dictionary<ArmorSet, List<ArmorSO>>();
        foreach (ArmorSO armor in GetEquippedArmor(inventoryManager))
        {
            ArmorSet set = armor.BelongsToSet;
            if (set == null || !set.ContainsPiece(armor))
                continue;
            if (!equippedSets.TryGetValue(set, out List<ArmorSO> list))
                equippedSets[set] = list = new List<ArmorSO>();
            if (!list.Contains(armor)) // different pieces count once each
                list.Add(armor);
        }
        return equippedSets;
    }

    // Get equipped armor pieces
    public static List<ArmorSO> GetEquippedArmor(InventoryManager inventoryManager)
    {
        EquipmentManager eq = GetEquipment(inventoryManager);
        return eq != null ? eq.GetEquippedArmor() : new List<ArmorSO>();
    }

    // Get missing pieces for a set
    public static List<ArmorSO> GetMissingPieces(this ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        if (armorSet?.SetPieces == null) return new List<ArmorSO>();
        return armorSet.SetPieces.Where(piece => piece != null && (equippedPieces == null || !equippedPieces.Contains(piece))).Distinct().ToList();
    }

    // Check if a piece belongs to any equipped set
    public static bool IsPartOfEquippedSet(ArmorSO armor, Dictionary<ArmorSet, List<ArmorSO>> equippedSets)
    {
        if (armor?.BelongsToSet == null || equippedSets == null) return false;
        return equippedSets.TryGetValue(armor.BelongsToSet, out var list) && list.Contains(armor);
    }

    /// <summary>Active set bonuses: from the armor set manager when there is one (it knows about lingering bonuses).</summary>
    public static List<ArmorSetEffect> GetActiveSetEffects(InventoryManager inventoryManager)
    {
        var activeEffects = new List<ArmorSetEffect>();
        EquipmentManager eq = GetEquipment(inventoryManager);
        ArmorSetManager sets = eq != null ? eq.ArmorSets : null;
        foreach (var kvp in GetEquippedArmorSets(inventoryManager))
        {
            if (sets != null)
                activeEffects.AddRange(sets.GetActiveSetEffects(kvp.Key));
            else
                activeEffects.AddRange(kvp.Key.GetActiveEffects(kvp.Value.Count));
        }
        return activeEffects;
    }

    // Calculate total stat bonuses from all active set effects
    public static Dictionary<EquippableEffectType, float> CalculateSetBonuses(InventoryManager inventoryManager)
    {
        var totalBonuses = new Dictionary<EquippableEffectType, float>();
        foreach (var effect in GetActiveSetEffects(inventoryManager))
        {
            if (effect.statBonuses == null) continue;
            foreach (var bonus in effect.statBonuses)
            {
                if (bonus == null) continue;
                totalBonuses.TryGetValue(bonus.effectType, out float v);
                totalBonuses[bonus.effectType] = v + bonus.amount;
            }
        }
        return totalBonuses;
    }

    // Get formatted summary of equipped armor and sets
    public static string GetEquipmentSummary(InventoryManager inventoryManager) => CreateArmorSummary(inventoryManager);

    // Validate armor set configuration (useful for debugging)
    public static List<string> ValidateArmorSet(ArmorSet armorSet)
    {
        var issues = new List<string>();
        if (armorSet == null)
        {
            issues.Add("ArmorSet is null");
            return issues;
        }
        var warnings = new List<string>();
        armorSet.Validate(issues, warnings);
        issues.AddRange(warnings);
        return issues;
    }

    // Check if a specific armor piece is equipped
    public static bool IsArmorEquipped(InventoryManager inventoryManager, ArmorSO armor)
    {
        EquipmentManager eq = GetEquipment(inventoryManager);
        return eq != null && armor != null && eq.IsEquipped(armor);
    }

    // Get equipped armor piece for a specific slot type
    public static ArmorSO GetEquippedArmorForSlot(InventoryManager inventoryManager, ArmorSlotType slotType)
    {
        EquipmentManager eq = GetEquipment(inventoryManager);
        return eq != null ? eq.GetArmorInSlot(slotType) : null;
    }

    // Calculate total defense from equipped armor
    public static float CalculateTotalDefense(InventoryManager inventoryManager)
    {
        return GetEquippedArmor(inventoryManager).Sum(armor => armor.GetEffectiveDefense());
    }

    // Calculate total magic defense from equipped armor
    public static float CalculateTotalMagicDefense(InventoryManager inventoryManager)
    {
        return GetEquippedArmor(inventoryManager).Sum(armor => armor.GetEffectiveMagicDefense());
    }

    // Get all unique armor sets from equipped armor
    public static List<ArmorSet> GetUniqueEquippedSets(InventoryManager inventoryManager)
    {
        return GetEquippedArmorSets(inventoryManager).Keys.ToList();
    }

    // Get complete armor sets
    public static List<ArmorSet> GetCompleteSets(InventoryManager inventoryManager)
    {
        return GetEquippedArmorSets(inventoryManager)
            .Where(kvp => kvp.Key.IsSetComplete(kvp.Value.Count))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    // Get all active set effects from equipped armor
    public static List<ArmorSetEffect> GetAllActiveSetEffects(InventoryManager inventoryManager) => GetActiveSetEffects(inventoryManager);

    // Create a summary of all equipped armor and set bonuses
    public static string CreateArmorSummary(InventoryManager inventoryManager)
    {
        var sb = new System.Text.StringBuilder("=== Equipped Armor Summary ===\n\n");
        var equippedArmor = GetEquippedArmor(inventoryManager);

        sb.AppendLine($"Total Defense: {equippedArmor.Sum(a => a.GetEffectiveDefense()):F1}");
        sb.AppendLine($"Total Magic Defense: {equippedArmor.Sum(a => a.GetEffectiveMagicDefense()):F1}\n");

        sb.AppendLine("Equipped Pieces:");
        foreach (ArmorSlotType slotType in System.Enum.GetValues(typeof(ArmorSlotType)))
        {
            var inSlot = equippedArmor.Where(a => a.ArmorSlotType == slotType).ToList();
            if (inSlot.Count == 0)
            {
                sb.AppendLine($"  {slotType}: (empty)");
                continue;
            }
            foreach (ArmorSO armor in inSlot)
            {
                string setInfo = armor.IsPartOfSet() ? $" ({armor.BelongsToSet.SetName})" : "";
                sb.AppendLine($"  {slotType}: {armor.Name}{setInfo}");
            }
        }

        var equippedSets = GetEquippedArmorSets(inventoryManager);
        if (equippedSets.Count > 0)
        {
            sb.AppendLine("\nSet Bonuses:");
            foreach (var kvp in equippedSets)
            {
                sb.AppendLine($"  {kvp.Key.SetName} ({kvp.Value.Count}/{kvp.Key.SetPieces.Count}):");
                foreach (string line in kvp.Key.DescribeTiers(kvp.Value.Count))
                    sb.AppendLine("    " + line);
            }
        }

        return sb.ToString();
    }

    // Get armor upgrade recommendations based on equipped sets
    public static List<string> GetUpgradeRecommendations(InventoryManager inventoryManager)
    {
        var recommendations = new List<string>();
        foreach (var kvp in GetEquippedArmorSets(inventoryManager))
        {
            var armorSet = kvp.Key;
            int equippedCount = kvp.Value.Count;
            int nextThreshold = armorSet.GetNextEffectThreshold(equippedCount);
            if (nextThreshold <= 0)
                continue;
            int piecesNeeded = nextThreshold - equippedCount;
            recommendations.Add($"Equip {piecesNeeded} more {armorSet.SetName} piece(s) to unlock the next bonus");
            var missingPieces = armorSet.GetMissingPieces(kvp.Value);
            if (missingPieces.Count > 0)
                recommendations.Add($"  Missing: {string.Join(", ", missingPieces.Select(p => $"{p.Name} ({p.ArmorSlotType})"))}");
        }
        return recommendations;
    }

    // Get set completion percentage for UI
    public static float GetSetCompletionPercentage(ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        if (armorSet == null || equippedPieces == null) return 0f;
        return armorSet.GetCompletionPercentage(equippedPieces.Distinct().Count());
    }

    // Check if a specific effect threshold is met
    public static bool IsEffectThresholdMet(ArmorSet armorSet, int requiredPieces, List<ArmorSO> equippedPieces)
    {
        return equippedPieces != null && equippedPieces.Distinct().Count() >= requiredPieces;
    }

    // Get next milestone for set completion
    public static string GetNextMilestone(ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        int count = equippedPieces != null ? equippedPieces.Distinct().Count() : 0;
        var nextThreshold = armorSet.GetNextEffectThreshold(count);
        if (nextThreshold <= 0) return "Set Complete";

        var nextEffect = armorSet.SetEffects.FirstOrDefault(e => e != null && e.piecesRequired == nextThreshold);
        return nextEffect != null
            ? $"{nextThreshold - count} more pieces for: {nextEffect.effectName}"
            : $"{nextThreshold - count} more pieces for next bonus";
    }

    /// <summary>
    /// Kept for older code: equipped-armor figures, refreshed at most once per second. Reads the equipment manager, which
    /// is cheap, so this is mostly a convenience now.
    /// </summary>
    public class ArmorCache
    {
        private readonly List<ArmorSO> equipped = new List<ArmorSO>();
        private Dictionary<ArmorSet, List<ArmorSO>> equippedSets;
        private float totalDefense;
        private float totalMagicDefense;
        private float lastUpdateTime = -999f;
        private const float CACHE_DURATION = 1f;

        public void UpdateCache(InventoryManager inventoryManager, bool force = false)
        {
            if (!force && Time.time - lastUpdateTime < CACHE_DURATION) return;
            equipped.Clear();
            equipped.AddRange(GetEquippedArmor(inventoryManager));
            equippedSets = GetEquippedArmorSets(inventoryManager);
            totalDefense = equipped.Sum(armor => armor.GetEffectiveDefense());
            totalMagicDefense = equipped.Sum(armor => armor.GetEffectiveMagicDefense());
            lastUpdateTime = Time.time;
        }

        // Several pieces can share a slot type (two rings): the first one is returned.
        public ArmorSO GetArmorForSlot(ArmorSlotType slotType) => equipped.FirstOrDefault(a => a.ArmorSlotType == slotType);

        public float GetTotalDefense() => totalDefense;
        public float GetTotalMagicDefense() => totalMagicDefense;
        public Dictionary<ArmorSet, List<ArmorSO>> GetEquippedSets() => equippedSets ?? new Dictionary<ArmorSet, List<ArmorSO>>();
    }
}
