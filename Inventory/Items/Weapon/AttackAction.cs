using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The attack a weapon performs for one input (Normal, Light, Heavy, Special, Alternate). Pressing the same input
/// again within <see cref="variantTime"/> plays its <see cref="variations"/> in order (a chained 1-2-3 string), then
/// starts over. Everything about the attack itself (timing, damage, hit area, on-hit effects, charging, behaviours)
/// lives in <see cref="AttackComponent"/>.
/// </summary>
[System.Serializable]
public class AttackAction : AttackComponent
{
    [Header("Action Configuration")]
    [Tooltip("The input this action answers to (set automatically from the weapon slot it is in).")]
    public AttackType actionType;
    [Tooltip("Name shown in tooltips and debug output.")]
    public string actionName;
    [Tooltip("Seconds after this attack during which pressing the same input again plays the next variation (the chain resets afterwards).")]
    public float variantTime = 1.0f;

    [Header("Trait Requirements")]
    [Tooltip("The player or the weapon must have one of these traits to perform this action")]
    public List<Trait> requiredTraits = new List<Trait>();
    [Tooltip("Action is enhanced if these traits are present")]
    public List<Trait> enhancementTraits = new List<Trait>();

    [Header("Trait Enhancement Modifiers")]
    [Tooltip("Attack speed while enhanced (1.2 = 20% faster). Applied per attack; the asset never changes.")]
    [SerializeField] private float enhancedAnimationSpeedMultiplier = 1.0f;
    [Tooltip("Stamina cost while enhanced (0.8 = 20% cheaper).")]
    [SerializeField] private float enhancedStaminaCostMultiplier = 1.0f;
    [Tooltip("Damage while enhanced (1.25 = 25% more).")]
    [SerializeField] private float enhancedDamageMultiplier = 1.0f;
    [Tooltip("Extra classic effects while enhanced.")]
    [SerializeField] private List<AttackActionEffect> enhancementEffects = new List<AttackActionEffect>();

    [Header("Variations")]
    [Tooltip("Next attacks of the chain for the same input (played in order when the input is pressed again in time).")]
    public List<AttackVariation> variations = new List<AttackVariation>();

    public float EnhancedAnimationSpeedMultiplier => enhancedAnimationSpeedMultiplier;
    public float EnhancedStaminaCostMultiplier => enhancedStaminaCostMultiplier;
    public float EnhancedDamageMultiplier => enhancedDamageMultiplier;
    public List<AttackActionEffect> EnhancementEffects => enhancementEffects ?? (enhancementEffects = new List<AttackActionEffect>());

    public override string DisplayName => string.IsNullOrEmpty(actionName) ? actionType.ToString() : actionName;

    public AttackVariation GetVariation(int variationIndex)
    {
        if (variations == null || variations.Count == 0)
            return null;
        return variations[((variationIndex % variations.Count) + variations.Count) % variations.Count];
    }

    public int GetVariationCount() => variations?.Count ?? 0;

    // Trait-related methods - use references instead of names
    public bool CanPerformWithTraits(TraitManager playerTraits, WeaponSO weapon)
    {
        if (requiredTraits == null || requiredTraits.Count == 0) return true;

        bool anyAssigned = false;
        foreach (var requiredTrait in requiredTraits)
        {
            if (requiredTrait == null) continue;
            anyAssigned = true;
            if (playerTraits != null && playerTraits.HasTrait(requiredTrait))
                return true;
            if (weapon != null && weapon.HasTrait(requiredTrait))
                return true;
        }
        // Only empty entries: nothing is actually required.
        return !anyAssigned;
    }

    public bool HasEnhancementTrait(TraitManager playerTraits, WeaponSO weapon, out Trait foundTrait)
    {
        foundTrait = null;
        if (enhancementTraits == null) return false;

        foreach (var enhancementTrait in enhancementTraits)
        {
            if (enhancementTrait == null) continue;
            if ((playerTraits != null && playerTraits.HasTrait(enhancementTrait)) || (weapon != null && weapon.HasTrait(enhancementTrait)))
            {
                foundTrait = enhancementTrait;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Old API: used to multiply this action's speed and stamina cost and append effects to it PERMANENTLY (the
    /// weapon asset got stronger with every attack). Enhancements are now applied per attack by the weapon controller
    /// without touching the asset, so this does nothing.
    /// </summary>
    [System.Obsolete("Enhancements are applied per attack by the WeaponController (the asset is no longer modified).")]
    public void ApplyTraitEnhancements(TraitManager playerTraits, WeaponSO weapon) { }

    /// <summary>Stamina cost with the old string-based trait modifiers ("staminacost" consumption rates) of the player.</summary>
    public float GetModifiedStaminaCost(TraitManager playerTraits, WeaponSO weapon)
    {
        float modifiedCost = staminaCost;
        if (playerTraits == null)
            return modifiedCost;
        IReadOnlyList<Trait> traits = playerTraits.Traits;
        for (int i = 0; i < traits.Count; i++)
        {
            Trait trait = traits[i];
            if (trait == null || trait.effects == null) continue;
            foreach (var effect in trait.effects)
                if (effect != null && effect.effectType == TraitEffectType.ConsumptionRate && string.Equals(effect.targetStat, "staminacost", System.StringComparison.OrdinalIgnoreCase))
                    modifiedCost *= effect.value;
        }
        return modifiedCost;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (variantTime < 0f)
            errors.Add($"{owner}: Variant Time cannot be negative.");
        if (variations != null && variations.Count > 0 && variantTime <= 0f)
            warnings.Add($"{owner}: has variations but Variant Time is 0, so they never play.");
        if (variations != null)
            for (int i = 0; i < variations.Count; i++)
            {
                if (variations[i] == null) continue;
                variations[i].Validate($"{owner} ▸ Variation {i + 1}", errors, warnings);
            }
        if (requiredTraits != null && requiredTraits.Contains(null))
            warnings.Add($"{owner}: 'Required Traits' has empty entries.");
    }

    // Debug helper
    public string GetRequiredTraitsString()
    {
        if (requiredTraits == null || requiredTraits.Count == 0) return "None";
        var traitNames = new List<string>();
        foreach (var trait in requiredTraits)
            if (trait != null) traitNames.Add(trait.Name);
        return string.Join(", ", traitNames);
    }

    public string GetEnhancementTraitsString()
    {
        if (enhancementTraits == null || enhancementTraits.Count == 0) return "None";
        var traitNames = new List<string>();
        foreach (var trait in enhancementTraits)
            if (trait != null) traitNames.Add(trait.Name);
        return string.Join(", ", traitNames);
    }
}
