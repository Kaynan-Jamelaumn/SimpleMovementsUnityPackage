using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class TraitEnhancement
{
    [Header("Trait Enhancement Configuration")]
    public Trait originalTrait;
    public Trait enhancedTrait;

    [Header("Enhancement Type")]
    public TraitEnhancementType enhancementType;

    [Header("Value Modifications")]
    [Tooltip("Multiplier for existing trait effects (1.5 = 50% stronger)")]
    public float effectMultiplier = 1.5f;

    [Tooltip("Additional effects to add to the trait")]
    public List<TraitEffect> additionalEffects = new List<TraitEffect>();

    [Tooltip("Custom description for the enhanced version")]
    [TextArea(2, 3)]
    public string enhancedDescription;
}

public enum TraitEnhancementType
{
    Multiply,      // Multiply existing effects by a factor
    AddEffects,    // Add new effects to existing trait
    Replace,       // Replace with entirely different trait
    Upgrade        // Use a completely different enhanced trait
}

[System.Serializable]
public class SpecialMechanic
{
    [Header("Basic Info")]
    public string mechanicId;
    public string mechanicName = "Special Mechanic";
    [TextArea(2, 3)]
    public string mechanicDescription;

    [Header("Handler Configuration")]
    public SpecialMechanicHandlerBase handlerPrefab;
    public List<SpecialMechanicParameter> parameters = new List<SpecialMechanicParameter>();

    [Header("Activation Settings")]
    public bool activateOnEquip = true;
    public bool deactivateOnUnequip = true;
    public float cooldownDuration = 0f;
}

[System.Serializable]
public class SpecialMechanicParameter
{
    public string parameterName;
    public float value;
    public string description;
}

[System.Serializable]
public class ArmorSetEffect
{
    [Header("Set Bonus Configuration")]
    [Tooltip("Number of pieces required to activate this effect")]
    public int piecesRequired = 2;

    [Tooltip("Name of this set bonus")]
    public string effectName = "Set Bonus";

    [TextArea(2, 4)]
    [Tooltip("Description of what this set bonus does")]
    public string effectDescription = "";

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

    // Constructor to ensure proper initialization
    public ArmorSetEffect()
    {
        piecesRequired = 2;
        effectName = "Set Bonus";
        effectDescription = "";
        traitsToApply = new List<Trait>();
        traitEnhancements = new List<TraitEnhancement>();
        statBonuses = new List<EquippableEffect>();
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

        if (!string.IsNullOrEmpty(effectDescription))
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

    // Check if this effect has any content
    public bool HasEffects()
    {
        return traitsToApply.Count > 0 ||
               traitEnhancements.Count > 0 ||
               statBonuses.Count > 0 ||
               specialMechanics.Count > 0;
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

    // Validate this effect configuration
    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();

        if (piecesRequired < 1)
            issues.Add("Pieces required must be at least 1");

        if (string.IsNullOrEmpty(effectName) || effectName == "Set Bonus")
            issues.Add("Effect name should be more descriptive");

        if (!HasEffects())
            issues.Add("Effect has no actual effects defined");

        // Validate trait enhancements
        foreach (var enhancement in traitEnhancements)
        {
            if (enhancement.originalTrait == null)
            {
                issues.Add("Trait enhancement has no original trait specified");
                continue;
            }

            if (enhancement.enhancementType == TraitEnhancementType.Upgrade && enhancement.enhancedTrait == null)
            {
                issues.Add($"Trait enhancement for {enhancement.originalTrait.Name} is set to Upgrade but no enhanced trait specified");
            }

            if (enhancement.enhancementType == TraitEnhancementType.Multiply && enhancement.effectMultiplier <= 0)
            {
                issues.Add($"Trait enhancement for {enhancement.originalTrait.Name} has invalid multiplier");
            }
        }

        // Validate special mechanics
        foreach (var mechanic in specialMechanics)
        {
            if (string.IsNullOrEmpty(mechanic.mechanicId))
                issues.Add("Special mechanic has no ID specified");

            if (string.IsNullOrEmpty(mechanic.mechanicName))
                issues.Add($"Special mechanic {mechanic.mechanicId} has no name specified");
        }

        return issues;
    }
}