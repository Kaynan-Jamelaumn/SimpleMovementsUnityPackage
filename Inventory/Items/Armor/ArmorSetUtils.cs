using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ArmorSetUtils
{
    // Get all equipped armor sets from inventory
    public static Dictionary<ArmorSet, List<ArmorSO>> GetEquippedArmorSets(InventoryManager inventoryManager)
    {
        var equippedSets = new Dictionary<ArmorSet, List<ArmorSO>>();

        if (inventoryManager?.Slots == null) return equippedSets;

        var equippedArmor = InventoryUtils.FindAllArmor(inventoryManager.Slots);

        foreach (var armor in equippedArmor)
        {
            var armorSO = armor.itemScriptableObject as ArmorSO;
            if (armorSO?.BelongsToSet != null && armor.isEquipped)
            {
                var armorSet = armorSO.BelongsToSet;
                if (!equippedSets.ContainsKey(armorSet))
                {
                    equippedSets[armorSet] = new List<ArmorSO>();
                }
                equippedSets[armorSet].Add(armorSO);
            }
        }

        return equippedSets;
    }

    // Get equipped armor pieces
    public static List<ArmorSO> GetEquippedArmor(InventoryManager inventoryManager)
    {
        var equippedArmor = new List<ArmorSO>();

        if (inventoryManager?.Slots == null) return equippedArmor;

        var allArmor = InventoryUtils.FindAllArmor(inventoryManager.Slots);

        foreach (var armor in allArmor)
        {
            if (armor.isEquipped && armor.itemScriptableObject is ArmorSO armorSO)
            {
                equippedArmor.Add(armorSO);
            }
        }

        return equippedArmor;
    }

    // Get missing pieces for a set
    public static List<ArmorSO> GetMissingPieces(this ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        if (armorSet?.SetPieces == null) return new List<ArmorSO>();
        return armorSet.SetPieces.Where(piece => piece != null && !equippedPieces.Contains(piece)).ToList();
    }

    // Check if a piece belongs to any equipped set
    public static bool IsPartOfEquippedSet(ArmorSO armor, Dictionary<ArmorSet, List<ArmorSO>> equippedSets)
    {
        if (armor?.BelongsToSet == null) return false;
        return equippedSets.ContainsKey(armor.BelongsToSet) && equippedSets[armor.BelongsToSet].Contains(armor);
    }

    // Get all active set effects from equipped armor
    public static List<ArmorSetEffect> GetActiveSetEffects(InventoryManager inventoryManager)
    {
        var activeEffects = new List<ArmorSetEffect>();
        var equippedSets = GetEquippedArmorSets(inventoryManager);

        foreach (var kvp in equippedSets)
        {
            var armorSet = kvp.Key;
            var equippedCount = kvp.Value.Count;
            activeEffects.AddRange(armorSet.GetActiveEffects(equippedCount));
        }

        return activeEffects;
    }

    // Calculate total stat bonuses from all active set effects
    public static Dictionary<EquippableEffectType, float> CalculateSetBonuses(InventoryManager inventoryManager)
    {
        var totalBonuses = new Dictionary<EquippableEffectType, float>();
        var activeEffects = GetActiveSetEffects(inventoryManager);

        foreach (var effect in activeEffects)
        {
            foreach (var bonus in effect.statBonuses)
            {
                if (totalBonuses.ContainsKey(bonus.effectType))
                    totalBonuses[bonus.effectType] += bonus.amount;
                else
                    totalBonuses[bonus.effectType] = bonus.amount;
            }
        }

        return totalBonuses;
    }

    // Get formatted summary of equipped armor and sets
    public static string GetEquipmentSummary(InventoryManager inventoryManager)
    {
        string summary = "=== EQUIPPED ARMOR ===\n";

        var equippedArmor = InventoryUtils.FindAllArmor(inventoryManager.Slots)
                                         .Where(item => item.isEquipped).ToList();
        var armorBySlot = equippedArmor.GroupBy(item => (item.itemScriptableObject as ArmorSO)?.ArmorSlotType)
                                      .ToDictionary(g => g.Key, g => g.First());

        foreach (ArmorSlotType slotType in System.Enum.GetValues(typeof(ArmorSlotType)))
        {
            if (armorBySlot.TryGetValue(slotType, out var inventoryItem))
            {
                var armor = inventoryItem.itemScriptableObject as ArmorSO;
                string setInfo = armor?.IsPartOfSet() == true ? $" ({armor.BelongsToSet.SetName})" : "";
                summary += $"  {slotType}: {armor.name}{setInfo}\n";
            }
        }

        var equippedSets = GetEquippedArmorSets(inventoryManager);
        if (equippedSets.Count > 0)
        {
            summary += "\nSet Bonuses:\n";
            foreach (var kvp in equippedSets)
            {
                var armorSet = kvp.Key;
                var equippedCount = kvp.Value.Count;
                var activeEffects = armorSet.GetActiveEffects(equippedCount);

                summary += $"  {armorSet.SetName} ({equippedCount}/{armorSet.SetPieces.Count}):\n";

                if (activeEffects.Count > 0)
                {
                    foreach (var effect in activeEffects)
                    {
                        summary += $"    • {effect.effectName}\n";
                    }
                }
                else
                {
                    summary += "    • No active bonuses\n";
                }
            }
        }

        return summary;
    }

    // Validate armor set configuration (useful for debugging)
    public static List<string> ValidateArmorSet(ArmorSet armorSet)
    {
        var issues = new List<string>();

        if (armorSet == null)
        {
            issues.Add("ArmorSet is null");
            return issues;
        }

        // Basic information validation
        if (string.IsNullOrEmpty(armorSet.SetName))
        {
            issues.Add("Set name is empty");
        }

        // Set pieces validation
        if (armorSet.SetPieces == null || armorSet.SetPieces.Count == 0)
        {
            issues.Add("No set pieces defined");
        }
        else
        {
            // Check for null pieces
            var nullPieces = armorSet.SetPieces.Count(p => p == null);
            if (nullPieces > 0)
            {
                issues.Add($"{nullPieces} null armor piece(s) in set");
            }

            // Check for duplicate slot types
            var validPieces = armorSet.SetPieces.Where(p => p != null).ToList();
            var slotTypes = validPieces.Select(p => p.ArmorSlotType).ToList();
            var duplicates = slotTypes.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var duplicate in duplicates)
            {
                issues.Add($"Duplicate slot type: {duplicate}");
            }

            // Check if pieces reference this set
            foreach (var piece in validPieces)
            {
                if (piece.BelongsToSet != armorSet)
                {
                    issues.Add($"Piece '{piece.name}' doesn't reference this set");
                }
            }
        }

        // Set effects validation
        if (armorSet.SetEffects != null)
        {
            for (int i = 0; i < armorSet.SetEffects.Count; i++)
            {
                var effect = armorSet.SetEffects[i];
                if (effect == null)
                {
                    issues.Add($"Set effect at index {i} is null");
                    continue;
                }

                // Validate effect configuration
                var effectIssues = effect.ValidateConfiguration();
                foreach (var effectIssue in effectIssues)
                {
                    issues.Add($"Effect '{effect.effectName}': {effectIssue}");
                }

                // Additional validations
                if (effect.piecesRequired > armorSet.SetPieces.Count)
                {
                    issues.Add($"Effect '{effect.effectName}' requires {effect.piecesRequired} pieces but set only has {armorSet.SetPieces.Count}");
                }

                if (effect.piecesRequired < 1)
                {
                    issues.Add($"Effect '{effect.effectName}' requires less than 1 piece");
                }

                if (!effect.HasEffects())
                {
                    issues.Add($"Effect '{effect.effectName}' has no actual effects defined");
                }

                // Check for duplicate piece requirements
                var duplicateRequirement = armorSet.SetEffects
                    .Where(e => e != effect && e.piecesRequired == effect.piecesRequired)
                    .FirstOrDefault();
                if (duplicateRequirement != null)
                {
                    issues.Add($"Multiple effects require {effect.piecesRequired} pieces: '{effect.effectName}' and '{duplicateRequirement.effectName}'");
                }
            }
        }

        // Configuration validation
        if (armorSet.MinimumPiecesForSet < 1)
        {
            issues.Add("Minimum pieces for set must be at least 1");
        }

        if (armorSet.MaximumSetPieces < armorSet.MinimumPiecesForSet)
        {
            issues.Add("Maximum set pieces cannot be less than minimum pieces");
        }

        if (armorSet.SetPieces.Count > armorSet.MaximumSetPieces)
        {
            issues.Add($"Set has {armorSet.SetPieces.Count} pieces but maximum is {armorSet.MaximumSetPieces}");
        }

        return issues;
    }

    // Check if a specific armor piece is equipped
    public static bool IsArmorEquipped(InventoryManager inventoryManager, ArmorSO armor)
    {
        if (inventoryManager?.Slots == null || armor == null) return false;

        foreach (var slotObj in inventoryManager.Slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject == armor && inventoryItem.isEquipped)
            {
                return true;
            }
        }

        return false;
    }

    // Get equipped armor piece for a specific slot type
    public static ArmorSO GetEquippedArmorForSlot(InventoryManager inventoryManager, ArmorSlotType slotType)
    {
        var equippedArmor = GetEquippedArmor(inventoryManager);
        return equippedArmor.FirstOrDefault(armor => armor.ArmorSlotType == slotType);
    }

    // Calculate total defense from equipped armor
    public static float CalculateTotalDefense(InventoryManager inventoryManager)
    {
        var equippedArmor = GetEquippedArmor(inventoryManager);
        return equippedArmor.Sum(armor => armor.GetEffectiveDefense());
    }

    // Calculate total magic defense from equipped armor
    public static float CalculateTotalMagicDefense(InventoryManager inventoryManager)
    {
        var equippedArmor = GetEquippedArmor(inventoryManager);
        return equippedArmor.Sum(armor => armor.GetEffectiveMagicDefense());
    }

    // Get all unique armor sets from equipped armor
    public static List<ArmorSet> GetUniqueEquippedSets(InventoryManager inventoryManager)
    {
        var equippedSets = GetEquippedArmorSets(inventoryManager);
        return equippedSets.Keys.ToList();
    }

    // Get complete armor sets (where all pieces are equipped)
    public static List<ArmorSet> GetCompleteSets(InventoryManager inventoryManager)
    {
        var result = new List<ArmorSet>();
        var equippedSets = GetEquippedArmorSets(inventoryManager);

        foreach (var kvp in equippedSets)
        {
            var armorSet = kvp.Key;
            var equippedCount = kvp.Value.Count;
            if (armorSet.IsSetComplete(equippedCount))
            {
                result.Add(armorSet);
            }
        }

        return result;
    }

    // Get all active set effects from equipped armor
    public static List<ArmorSetEffect> GetAllActiveSetEffects(InventoryManager inventoryManager)
    {
        var result = new List<ArmorSetEffect>();
        var equippedSets = GetEquippedArmorSets(inventoryManager);

        foreach (var kvp in equippedSets)
        {
            var armorSet = kvp.Key;
            var equippedCount = kvp.Value.Count;
            result.AddRange(armorSet.GetActiveEffects(equippedCount));
        }

        return result;
    }

    // Create a summary of all equipped armor and set bonuses
    public static string CreateArmorSummary(InventoryManager inventoryManager)
    {
        var summary = "=== Equipped Armor Summary ===\n\n";

        var equippedArmor = GetEquippedArmor(inventoryManager);
        var totalDefense = CalculateTotalDefense(inventoryManager);
        var totalMagicDefense = CalculateTotalMagicDefense(inventoryManager);

        summary += $"Total Defense: {totalDefense:F1}\n";
        summary += $"Total Magic Defense: {totalMagicDefense:F1}\n\n";

        summary += "Equipped Pieces:\n";
        var armorBySlot = equippedArmor.ToDictionary(a => a.ArmorSlotType, a => a);
        foreach (ArmorSlotType slotType in System.Enum.GetValues(typeof(ArmorSlotType)))
        {
            if (armorBySlot.TryGetValue(slotType, out ArmorSO armor))
            {
                string setInfo = armor.IsPartOfSet() ? $" ({armor.BelongsToSet.SetName})" : "";
                summary += $"  {slotType}: {armor.name}{setInfo}\n";
            }
            else
            {
                summary += $"  {slotType}: (empty)\n";
            }
        }

        var equippedSets = GetEquippedArmorSets(inventoryManager);
        if (equippedSets.Count > 0)
        {
            summary += "\nActive Set Bonuses:\n";
            foreach (var kvp in equippedSets)
            {
                var armorSet = kvp.Key;
                var equippedCount = kvp.Value.Count;
                var activeEffects = armorSet.GetActiveEffects(equippedCount);

                summary += $"  {armorSet.SetName} ({equippedCount}/{armorSet.SetPieces.Count}):\n";

                if (activeEffects.Count > 0)
                {
                    foreach (var effect in activeEffects)
                    {
                        summary += $"    • {effect.effectName}\n";
                    }
                }
                else
                {
                    int nextThreshold = armorSet.GetNextEffectThreshold(equippedCount);
                    if (nextThreshold > 0)
                    {
                        summary += $"    • Next bonus at {nextThreshold} pieces\n";
                    }
                    else
                    {
                        summary += $"    • No more bonuses available\n";
                    }
                }
            }
        }

        return summary;
    }

    // Get armor upgrade recommendations based on equipped sets
    public static List<string> GetUpgradeRecommendations(InventoryManager inventoryManager)
    {
        var recommendations = new List<string>();
        var equippedSets = GetEquippedArmorSets(inventoryManager);

        foreach (var kvp in equippedSets)
        {
            var armorSet = kvp.Key;
            var equippedCount = kvp.Value.Count;
            var nextThreshold = armorSet.GetNextEffectThreshold(equippedCount);

            if (nextThreshold > 0)
            {
                var missingPieces = armorSet.GetMissingPieces(kvp.Value);
                var piecesNeeded = nextThreshold - equippedCount;

                recommendations.Add($"Equip {piecesNeeded} more {armorSet.SetName} piece(s) to unlock next bonus");

                if (missingPieces.Count > 0)
                {
                    var suggestedSlots = missingPieces.Take(piecesNeeded);
                    recommendations.Add($"  Suggested slots: {string.Join(", ", suggestedSlots)}");
                }
            }
        }

        return recommendations;
    }

    // Helper method to get armor set pieces in inventory
    private static List<ArmorSO> GetArmorSetPiecesInInventory(InventoryManager inventoryManager, ArmorSet armorSet)
    {
        var pieces = new List<ArmorSO>();

        if (inventoryManager?.Slots == null) return pieces;

        foreach (var slot in inventoryManager.Slots)
        {
            var inventorySlot = slot?.GetComponent<InventorySlot>();
            var item = inventorySlot?.heldItem?.GetComponent<InventoryItem>();
            var armorSO = item?.itemScriptableObject as ArmorSO;

            if (armorSO?.BelongsToSet == armorSet)
            {
                pieces.Add(armorSO);
            }
        }

        return pieces;
    }

    // Get set completion percentage for UI
    public static float GetSetCompletionPercentage(ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        if (armorSet?.SetPieces == null || armorSet.SetPieces.Count == 0) return 0f;
        return (float)equippedPieces.Count / armorSet.SetPieces.Count;
    }

    // Check if a specific effect threshold is met
    public static bool IsEffectThresholdMet(ArmorSet armorSet, int requiredPieces, List<ArmorSO> equippedPieces)
    {
        return equippedPieces.Count >= requiredPieces;
    }

    // Get next milestone for set completion
    public static string GetNextMilestone(ArmorSet armorSet, List<ArmorSO> equippedPieces)
    {
        var nextThreshold = armorSet.GetNextEffectThreshold(equippedPieces.Count);
        if (nextThreshold <= 0) return "Set Complete";

        var nextEffect = armorSet.SetEffects.FirstOrDefault(e => e.piecesRequired == nextThreshold);
        if (nextEffect != null)
        {
            return $"{nextThreshold - equippedPieces.Count} more pieces for: {nextEffect.effectName}";
        }

        return $"{nextThreshold - equippedPieces.Count} more pieces for next bonus";
    }

    // Performance helper: Cache equipped armor for multiple queries
    public class ArmorCache
    {
        private Dictionary<ArmorSlotType, ArmorSO> equippedBySlot;
        private Dictionary<ArmorSet, List<ArmorSO>> equippedSets;
        private float totalDefense;
        private float totalMagicDefense;
        private float lastUpdateTime;
        private const float CACHE_DURATION = 1f; // Cache for 1 second

        public void UpdateCache(InventoryManager inventoryManager)
        {
            if (Time.time - lastUpdateTime < CACHE_DURATION) return;

            // Get equipped armor using the static method from ArmorSetUtils
            var equippedArmor = ArmorSetUtils.GetEquippedArmor(inventoryManager);

            // Create dictionary mapping slot types to armor
            equippedBySlot = equippedArmor.ToDictionary(a => a.ArmorSlotType, a => a);

            // Get equipped sets using the static method from ArmorSetUtils
            equippedSets = ArmorSetUtils.GetEquippedArmorSets(inventoryManager);

            // Calculate total defense and magic defense
            totalDefense = equippedArmor.Sum(armor => armor.GetEffectiveDefense());
            totalMagicDefense = equippedArmor.Sum(armor => armor.GetEffectiveMagicDefense());

            lastUpdateTime = Time.time;
        }

        public ArmorSO GetArmorForSlot(ArmorSlotType slotType)
        {
            return equippedBySlot?.TryGetValue(slotType, out ArmorSO armor) == true ? armor : null;
        }

        public float GetTotalDefense() => totalDefense;
        public float GetTotalMagicDefense() => totalMagicDefense;
        public Dictionary<ArmorSet, List<ArmorSO>> GetEquippedSets() => equippedSets ?? new Dictionary<ArmorSet, List<ArmorSO>>();
    }
}