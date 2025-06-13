using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ArmorSetEffect
{
    [Header("Set Bonus Configuration")]
    [Tooltip("Number of pieces required to activate this effect")]
    public int piecesRequired = 2;

    [Tooltip("Name of this set bonus")]
    public string effectName = "New Set Bonus";

    [TextArea(2, 4)]
    [Tooltip("Description of what this set bonus does")]
    public string effectDescription = "Enter effect description here";

    [Header("Trait Effects")]
    [Tooltip("New traits that are applied when this set bonus is active")]
    public List<Trait> traitsToApply = new List<Trait>();

    [Tooltip("Trait enhancements for existing equipped traits")]
    public List<TraitEnhancement> traitEnhancements = new List<TraitEnhancement>();

    [Header("Stat Bonuses")]
    [Tooltip("Direct stat modifications applied by this set bonus")]
    public List<EquippableEffect> statBonuses = new List<EquippableEffect>();

    [Header("Special Mechanics")]
    [Tooltip("Special mechanics activated by this set bonus")]
    public List<SpecialMechanic> specialMechanics = new List<SpecialMechanic>();

    [Header("Effect Behavior")]
    [Tooltip("Whether this effect can stack with other similar effects")]
    public bool canStack = false;

    [Tooltip("Priority level for conflicting effects (higher = more priority)")]
    public int priority = 0;

    [Tooltip("Whether this effect persists after removing armor")]
    public float persistDuration = 0f;

    [Header("Visual & Audio")]
    public GameObject setEffectPrefab;
    public AudioClip setActivationSound;
    public ParticleSystem setActivationParticles;

    // Unity serialization callback - ensures proper initialization
    private void OnAfterDeserialize()
    {
        // Ensure minimum values are set if they're invalid
        if (piecesRequired < 1)
            piecesRequired = 2;

        if (string.IsNullOrEmpty(effectName))
            effectName = "New Set Bonus";

        if (string.IsNullOrEmpty(effectDescription))
            effectDescription = "Enter effect description here";

        // Initialize lists if null
        if (traitsToApply == null)
            traitsToApply = new List<Trait>();
        if (traitEnhancements == null)
            traitEnhancements = new List<TraitEnhancement>();
        if (statBonuses == null)
            statBonuses = new List<EquippableEffect>();
        if (specialMechanics == null)
            specialMechanics = new List<SpecialMechanic>();
    }

    // Check if this effect should be active
    public bool ShouldBeActive(int equippedPieces)
    {
        return equippedPieces >= piecesRequired && piecesRequired >= 1;
    }

    // Get formatted description including pieces required and all effects
    public string GetFormattedDescription()
    {
        string description = $"({piecesRequired} pieces) {effectName}";

        if (!string.IsNullOrEmpty(effectDescription) && effectDescription != "Enter effect description here")
        {
            description += $"\n{effectDescription}";
        }

        // Add trait information
        if (traitsToApply.Count > 0)
        {
            description += "\nNew Traits:";
            foreach (var trait in traitsToApply.Where(t => t != null))
            {
                description += $"\n• {trait.Name}";
            }
        }

        // Add trait enhancement information
        if (traitEnhancements.Count > 0)
        {
            description += "\nTrait Enhancements:";
            foreach (var enhancement in traitEnhancements)
            {
                if (enhancement.originalTrait != null)
                {
                    string enhanceDesc = enhancement.enhancementType switch
                    {
                        TraitEnhancementType.Multiply => $"Enhanced {enhancement.originalTrait.Name} (x{enhancement.effectMultiplier})",
                        TraitEnhancementType.AddEffects => $"Improved {enhancement.originalTrait.Name}",
                        TraitEnhancementType.Replace => $"Upgraded {enhancement.originalTrait.Name}",
                        TraitEnhancementType.Upgrade => $"{enhancement.enhancedTrait?.Name ?? "Enhanced Version"}",
                        _ => enhancement.originalTrait.Name
                    };
                    description += $"\n• {enhanceDesc}";
                }
            }
        }

        // Add stat bonus information
        if (statBonuses.Count > 0)
        {
            description += "\nStat Bonuses:";
            foreach (var bonus in statBonuses)
            {
                description += $"\n• {bonus.GetFormattedDescription()}";
            }
        }

        // Add special mechanics information
        if (specialMechanics.Count > 0)
        {
            description += "\nSpecial Abilities:";
            foreach (var mechanic in specialMechanics)
            {
                description += $"\n• {mechanic.mechanicName}";
            }
        }

        return description;
    }

    // Check if this effect has any content - IMPROVED VERSION
    public bool HasEffects()
    {
        bool hasTraits = traitsToApply != null && traitsToApply.Count > 0 && traitsToApply.Any(t => t != null);
        bool hasEnhancements = traitEnhancements != null && traitEnhancements.Count > 0 && traitEnhancements.Any(e => e.originalTrait != null);
        bool hasStatBonuses = statBonuses != null && statBonuses.Count > 0 && statBonuses.Any(s => s.amount != 0);
        bool hasSpecialMechanics = specialMechanics != null && specialMechanics.Count > 0 && specialMechanics.Any(m => !string.IsNullOrEmpty(m.mechanicId));

        return hasTraits || hasEnhancements || hasStatBonuses || hasSpecialMechanics;
    }

    // Get all traits that would be affected by this effect (for validation)
    public List<Trait> GetAffectedTraits()
    {
        var affectedTraits = new List<Trait>(traitsToApply.Where(t => t != null));

        foreach (var enhancement in traitEnhancements)
        {
            if (enhancement.originalTrait != null)
                affectedTraits.Add(enhancement.originalTrait);
            if (enhancement.enhancedTrait != null)
                affectedTraits.Add(enhancement.enhancedTrait);
        }

        return affectedTraits.Distinct().ToList();
    }

    // Check if this effect conflicts with another effect
    public bool ConflictsWith(ArmorSetEffect other)
    {
        if (other == null) return false;

        // Check for trait conflicts
        var ourTraits = GetAffectedTraits();
        var theirTraits = other.GetAffectedTraits();

        return ourTraits.Any(trait => theirTraits.Contains(trait));
    }

    // Get special mechanic by ID
    public SpecialMechanic GetSpecialMechanic(string mechanicId)
    {
        return specialMechanics.FirstOrDefault(m => m.mechanicId == mechanicId);
    }

    // Validate this effect configuration - ENHANCED VERSION with specific guidance
    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();

        if (piecesRequired < 1)
            issues.Add($"PIECES REQUIRED is {piecesRequired}. Change it to at least 1 (recommended: 2 for partial set, 3-4 for full set)");

        if (string.IsNullOrEmpty(effectName) || effectName == "Set Bonus" || effectName == "New Set Bonus")
            issues.Add($"EFFECT NAME is '{effectName}'. Change it to something descriptive like 'Warrior's Strength' or 'Mage's Focus'");

        if (!HasEffects())
        {
            issues.Add("NO EFFECTS CONFIGURED: You must add at least ONE of the following:\n" +
                      "1. STAT BONUSES: Click the '+' next to 'Stat Bonuses', choose an effect type (like MaxHp), set amount (like 50)\n" +
                      "2. TRAITS TO APPLY: Click the '+' next to 'Traits to Apply', drag a Trait ScriptableObject into the slot\n" +
                      "3. TRAIT ENHANCEMENTS: Enhance existing traits the player has\n" +
                      "4. SPECIAL MECHANICS: Add abilities like double jump or water walking");
        }

        //  validation for each component
        if (traitsToApply != null && traitsToApply.Count > 0)
        {
            int nullTraits = traitsToApply.Count(t => t == null);
            if (nullTraits > 0)
                issues.Add($"TRAITS TO APPLY: {nullTraits} empty slot(s) found. Either drag traits into these slots or remove the empty slots");
        }

        if (statBonuses != null && statBonuses.Count > 0)
        {
            int zeroAmountBonuses = statBonuses.Count(s => s.amount == 0);
            if (zeroAmountBonuses > 0)
                issues.Add($"STAT BONUSES: {zeroAmountBonuses} bonus(es) have 0 amount. Set non-zero values (like 50 for +50 health) or remove them");
        }

        // Validate trait enhancements
        foreach (var enhancement in traitEnhancements)
        {
            if (enhancement.originalTrait == null)
            {
                issues.Add("TRAIT ENHANCEMENT: Missing original trait. Select which trait you want to enhance");
                continue;
            }

            if (enhancement.enhancementType == TraitEnhancementType.Upgrade && enhancement.enhancedTrait == null)
            {
                issues.Add($"TRAIT ENHANCEMENT for '{enhancement.originalTrait.Name}': Set to Upgrade but no enhanced trait selected");
            }

            if (enhancement.enhancementType == TraitEnhancementType.Multiply && enhancement.effectMultiplier <= 0)
            {
                issues.Add($"TRAIT ENHANCEMENT for '{enhancement.originalTrait.Name}': Invalid multiplier {enhancement.effectMultiplier}. Use values like 1.5 for 50% stronger");
            }
        }

        // Validate special mechanics
        foreach (var mechanic in specialMechanics)
        {
            if (string.IsNullOrEmpty(mechanic.mechanicId))
                issues.Add("SPECIAL MECHANIC: Missing Mechanic ID. Enter something like 'double_jump' or 'water_walking'");

            if (string.IsNullOrEmpty(mechanic.mechanicName))
                issues.Add($"SPECIAL MECHANIC '{mechanic.mechanicId}': Missing Mechanic Name. Enter a display name like 'Double Jump'");
        }

        return issues;
    }

    // Get debug info about what this effect contains
    public string GetDebugInfo()
    {
        var info = $"Effect: {effectName} ({piecesRequired} pieces)\n";
        info += $"• Traits to Apply: {(traitsToApply?.Count(t => t != null) ?? 0)}\n";
        info += $"• Trait Enhancements: {(traitEnhancements?.Count(e => e.originalTrait != null) ?? 0)}\n";
        info += $"• Stat Bonuses: {(statBonuses?.Count(s => s.amount != 0) ?? 0)}\n";
        info += $"• Special Mechanics: {(specialMechanics?.Count(m => !string.IsNullOrEmpty(m.mechanicId)) ?? 0)}\n";
        info += $"• Has Effects: {HasEffects()}";
        return info;
    }
}