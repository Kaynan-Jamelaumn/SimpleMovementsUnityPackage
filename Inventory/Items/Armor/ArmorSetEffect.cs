using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ArmorSetEffect : ISerializationCallbackReceiver
{
    [Header("Set Bonus Configuration")]
    [Tooltip("Number of pieces required to activate this effect")]
    [Min(1)]
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

    // Serialization callbacks to ensure proper initialization
    public void OnBeforeSerialize()
    {
        // Ensure lists are never null
        if (traitsToApply == null)
            traitsToApply = new List<Trait>();
        if (traitEnhancements == null)
            traitEnhancements = new List<TraitEnhancement>();
        if (statBonuses == null)
            statBonuses = new List<EquippableEffect>();
        if (specialMechanics == null)
            specialMechanics = new List<SpecialMechanic>();
    }

    public void OnAfterDeserialize()
    {
        // Validate and fix data after deserialization
        ValidateAndFixData();
    }

    private void ValidateAndFixData()
    {
        // Ensure minimum values
        if (piecesRequired < 1)
            piecesRequired = 1;

        // Ensure default strings
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

        // Clean up null entries
        traitsToApply.RemoveAll(t => t == null);
        traitEnhancements.RemoveAll(e => e == null || e.originalTrait == null);
        statBonuses.RemoveAll(s => s == null);
        specialMechanics.RemoveAll(m => m == null || string.IsNullOrEmpty(m.mechanicId));
    }

    // Check if this effect should be active
    public bool ShouldBeActive(int equippedPieces)
    {
        return equippedPieces >= piecesRequired && piecesRequired >= 1;
    }

    // Check if this effect has any actual effects configured
    public bool HasEffects()
    {
        bool hasTraits = traitsToApply != null && traitsToApply.Count > 0 && traitsToApply.Any(t => t != null);
        bool hasEnhancements = traitEnhancements != null && traitEnhancements.Count > 0 &&
                              traitEnhancements.Any(e => e != null && e.originalTrait != null);
        bool hasStatBonuses = statBonuses != null && statBonuses.Count > 0 &&
                             statBonuses.Any(s => s != null && s.amount != 0);
        bool hasSpecialMechanics = specialMechanics != null && specialMechanics.Count > 0 &&
                                  specialMechanics.Any(m => m != null && !string.IsNullOrEmpty(m.mechanicId));

        return hasTraits || hasEnhancements || hasStatBonuses || hasSpecialMechanics;
    }

    // Get formatted description including pieces required and all effects
    public string GetFormattedDescription()
    {
        string desc = $"({piecesRequired} pieces) {effectDescription}\n";

        if (statBonuses != null && statBonuses.Count > 0)
        {
            desc += "\nStat Bonuses:";
            foreach (var bonus in statBonuses.Where(b => b != null && b.amount != 0))
            {
                desc += $"\n• {bonus.GetFormattedDescription()}";
            }
        }

        if (traitsToApply != null && traitsToApply.Count > 0)
        {
            desc += "\n\nTraits Applied:";
            foreach (var trait in traitsToApply.Where(t => t != null))
            {
                desc += $"\n• {trait.Name}";
            }
        }

        if (specialMechanics != null && specialMechanics.Count > 0)
        {
            desc += "\n\nSpecial Abilities:";
            foreach (var mechanic in specialMechanics.Where(m => m != null && !string.IsNullOrEmpty(m.mechanicId)))
            {
                desc += $"\n• {mechanic.mechanicName}";
            }
        }

        return desc;
    }

    // Validate this effect configuration - returns list of issues
    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();

        // Validate basic properties
        if (piecesRequired < 1)
            issues.Add($"Pieces required is {piecesRequired}. Must be at least 1.");

        if (string.IsNullOrEmpty(effectName) || effectName == "New Set Bonus" || effectName == "Set Bonus")
            issues.Add("Effect needs a descriptive name (e.g., 'Warrior's Vigor', 'Mage's Focus')");

        if (!HasEffects())
        {
            issues.Add("Effect must have at least ONE of the following:\n" +
                      "• Stat Bonuses (e.g., +50 Health)\n" +
                      "• Traits to Apply\n" +
                      "• Trait Enhancements\n" +
                      "• Special Mechanics");
        }
        else
        {
            // Validate individual components
            ValidateTraits(issues);
            ValidateStatBonuses(issues);
            ValidateTraitEnhancements(issues);
            ValidateSpecialMechanics(issues);
        }

        return issues;
    }

    private void ValidateTraits(List<string> issues)
    {
        if (traitsToApply != null && traitsToApply.Count > 0)
        {
            int nullTraits = traitsToApply.Count(t => t == null);
            if (nullTraits > 0)
                issues.Add($"{nullTraits} empty trait slot(s) in 'Traits to Apply'. Remove or assign traits.");
        }
    }

    private void ValidateStatBonuses(List<string> issues)
    {
        if (statBonuses != null && statBonuses.Count > 0)
        {
            int zeroAmountBonuses = statBonuses.Count(s => s != null && s.amount == 0);
            if (zeroAmountBonuses > 0)
                issues.Add($"{zeroAmountBonuses} stat bonus(es) have 0 amount. Set non-zero values or remove them.");

            // Check for duplicate effect types
            var duplicates = statBonuses
                .Where(s => s != null)
                .GroupBy(s => s.effectType)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var duplicate in duplicates)
            {
                issues.Add($"Multiple stat bonuses for {duplicate}. Consider combining them.");
            }
        }
    }

    private void ValidateTraitEnhancements(List<string> issues)
    {
        if (traitEnhancements != null && traitEnhancements.Count > 0)
        {
            foreach (var enhancement in traitEnhancements.Where(e => e != null))
            {
                if (enhancement.originalTrait == null)
                {
                    issues.Add("Trait enhancement missing original trait. Select which trait to enhance.");
                }
                else if (enhancement.enhancementType == TraitEnhancementType.Upgrade &&
                        enhancement.enhancedTrait == null)
                {
                    issues.Add($"Enhancement for '{enhancement.originalTrait.Name}' set to Upgrade but no enhanced trait selected.");
                }
                else if (enhancement.enhancementType == TraitEnhancementType.Multiply &&
                        enhancement.effectMultiplier <= 0)
                {
                    issues.Add($"Enhancement for '{enhancement.originalTrait.Name}' has invalid multiplier: {enhancement.effectMultiplier}");
                }
            }
        }
    }

    private void ValidateSpecialMechanics(List<string> issues)
    {
        if (specialMechanics != null && specialMechanics.Count > 0)
        {
            foreach (var mechanic in specialMechanics.Where(m => m != null))
            {
                if (string.IsNullOrEmpty(mechanic.mechanicId))
                    issues.Add("Special mechanic missing ID (e.g., 'double_jump', 'water_walking')");

                if (string.IsNullOrEmpty(mechanic.mechanicName))
                    issues.Add($"Special mechanic '{mechanic.mechanicId}' missing display name");

                // Check for duplicate mechanics
                var duplicateCount = specialMechanics.Count(m => m != null && m.mechanicId == mechanic.mechanicId);
                if (duplicateCount > 1)
                    issues.Add($"Duplicate special mechanic: {mechanic.mechanicId}");
            }
        }
    }

    // Get all traits affected by this effect
    public List<Trait> GetAffectedTraits()
    {
        var affectedTraits = new List<Trait>();

        if (traitsToApply != null)
            affectedTraits.AddRange(traitsToApply.Where(t => t != null));

        if (traitEnhancements != null)
        {
            foreach (var enhancement in traitEnhancements.Where(e => e != null))
            {
                if (enhancement.originalTrait != null)
                    affectedTraits.Add(enhancement.originalTrait);
                if (enhancement.enhancedTrait != null)
                    affectedTraits.Add(enhancement.enhancedTrait);
            }
        }

        return affectedTraits.Distinct().ToList();
    }

    // Check if this effect conflicts with another
    public bool ConflictsWith(ArmorSetEffect other)
    {
        if (other == null || canStack) return false;

        // Check for trait conflicts
        var ourTraits = GetAffectedTraits();
        var theirTraits = other.GetAffectedTraits();

        return ourTraits.Any(trait => theirTraits.Contains(trait));
    }

    // Get a special mechanic by ID
    public SpecialMechanic GetSpecialMechanic(string mechanicId)
    {
        return specialMechanics?.FirstOrDefault(m => m != null && m.mechanicId == mechanicId);
    }

    // Create a deep copy of this effect
    public ArmorSetEffect Clone()
    {
        var clone = new ArmorSetEffect
        {
            piecesRequired = piecesRequired,
            effectName = effectName,
            effectDescription = effectDescription,
            canStack = canStack,
            priority = priority,
            persistDuration = persistDuration,
            setEffectPrefab = setEffectPrefab,
            setActivationSound = setActivationSound,
            setActivationParticles = setActivationParticles
        };

        // Clone lists
        clone.traitsToApply = new List<Trait>(traitsToApply);
        clone.traitEnhancements = new List<TraitEnhancement>(traitEnhancements);
        clone.statBonuses = new List<EquippableEffect>(statBonuses);
        clone.specialMechanics = new List<SpecialMechanic>(specialMechanics);

        return clone;
    }

    // Get debug info
    public string GetDebugInfo()
    {
        var info = $"Effect: {effectName} ({piecesRequired} pieces)\n";
        info += $"• Valid: {(ValidateConfiguration().Count == 0 ? "Yes" : "No")}\n";
        info += $"• Has Effects: {HasEffects()}\n";
        info += $"• Traits: {traitsToApply?.Count(t => t != null) ?? 0}\n";
        info += $"• Stat Bonuses: {statBonuses?.Count(s => s != null && s.amount != 0) ?? 0}\n";
        info += $"• Enhancements: {traitEnhancements?.Count(e => e != null && e.originalTrait != null) ?? 0}\n";
        info += $"• Special Mechanics: {specialMechanics?.Count(m => m != null && !string.IsNullOrEmpty(m.mechanicId)) ?? 0}";

        return info;
    }
}