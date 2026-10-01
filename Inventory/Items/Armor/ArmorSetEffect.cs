using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One bonus tier of an <see cref="ArmorSet"/>: active while at least <see cref="piecesRequired"/> different pieces of
/// the set are worn (2/4, 3/4, 4/4...). Tiers are cumulative by default; tiers that share an
/// <see cref="upgradeGroup"/> replace each other instead, so only the highest reached one of the group is active
/// (e.g. "+10% damage" at 2 pieces becomes "+25% damage" at 4).
/// <para>
/// What it does: the classic lists (traits, trait enhancements, stat bonuses, special mechanics) and any
/// <see cref="EquipmentEffect"/> in <see cref="effects"/> (combat stats, resistances, passive behaviours, abilities,
/// on-hit effects...). The <see cref="ArmorSetManager"/> applies and removes them exactly.
/// </para>
/// </summary>
[System.Serializable]
public class ArmorSetEffect
{
    [Header("Set Bonus Configuration")]
    [Tooltip("Number of different pieces of the set that must be worn to activate this bonus")]
    [Min(1)]
    public int piecesRequired = 2;

    [Tooltip("Name of this set bonus")]
    public string effectName = "New Set Bonus";

    [TextArea(2, 4)]
    [Tooltip("Description of what this set bonus does (shown in tooltips and the set UI)")]
    public string effectDescription = "Enter effect description here";

    [Tooltip("Bonuses with the same group name replace each other: only the highest reached one is active (e.g. 'damage' at 2 and 4 pieces). Empty = always adds up with the other tiers.")]
    public string upgradeGroup = "";

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

    [Header("Effects")]
    [Tooltip("Anything else the bonus does: combat stats and resistances, passive behaviours (double jump, life steal...), abilities on keys, on-hit / when-hit effects, conditional effects, visuals.")]
    [SerializeReference, SubclassSelector]
    public List<EquipmentEffect> effects = new List<EquipmentEffect>();

    [Header("Effect Behavior")]
    [Tooltip("Trait strengthening: multiplies with other stacking enhancements of the same trait. Off = only the strongest non-stacking one counts.")]
    public bool canStack = false;

    [Tooltip("When several bonuses strengthen or replace the same trait, the highest priority wins.")]
    public int priority = 0;

    [Tooltip("Seconds the bonus stays active after the pieces that enabled it are removed (0 = removed immediately).")]
    public float persistDuration = 0f;

    [Header("Visual & Audio")]
    [Tooltip("Spawned on the wearer while the bonus is active.")]
    public GameObject setEffectPrefab;
    [Tooltip("Played when the bonus activates.")]
    public AudioClip setActivationSound;
    [Tooltip("Played once on the wearer when the bonus activates.")]
    public ParticleSystem setActivationParticles;

    // Check if this effect should be active
    public bool ShouldBeActive(int equippedPieces)
    {
        return equippedPieces >= piecesRequired && piecesRequired >= 1;
    }

    /// <summary>
    /// Every effect this bonus applies, the classic lists converted to <see cref="EquipmentEffect"/>s. Built fresh when
    /// the bonus activates.
    /// </summary>
    public void CollectEffects(List<EquipmentEffect> into)
    {
        if (traitsToApply != null && traitsToApply.Any(t => t != null))
            into.Add(new GrantTraitsEffect(traitsToApply.Where(t => t != null)));
        if (traitEnhancements != null)
        {
            foreach (TraitEnhancement e in traitEnhancements)
            {
                EnhanceTraitEffect fx = EnhanceTraitEffect.FromLegacy(e, canStack, priority);
                if (fx != null)
                    into.Add(fx);
            }
        }
        if (statBonuses != null && statBonuses.Any(s => s != null))
            into.Add(new ClassicStatsEffect(statBonuses.Where(s => s != null)));
        if (specialMechanics != null)
            foreach (SpecialMechanic m in specialMechanics)
                if (m != null && !string.IsNullOrWhiteSpace(m.mechanicId))
                    into.Add(new SpecialMechanicEffect(m));
        if (effects != null)
            foreach (EquipmentEffect e in effects)
                if (e != null)
                    into.Add(e);
        if (setEffectPrefab != null)
            into.Add(new AttachedVisualEffect(setEffectPrefab));
    }

    // Check if this effect has any actual effects configured
    public bool HasEffects()
    {
        bool hasTraits = traitsToApply != null && traitsToApply.Any(t => t != null);
        bool hasEnhancements = traitEnhancements != null && traitEnhancements.Any(e => e != null && e.originalTrait != null);
        bool hasStatBonuses = statBonuses != null && statBonuses.Any(s => s != null && s.amount != 0);
        bool hasSpecialMechanics = specialMechanics != null && specialMechanics.Any(m => m != null && !string.IsNullOrEmpty(m.mechanicId));
        bool hasEffects = effects != null && effects.Any(e => e != null);

        return hasTraits || hasEnhancements || hasStatBonuses || hasSpecialMechanics || hasEffects;
    }

    /// <summary>One line per thing the bonus does (tooltips).</summary>
    public List<string> DescribeLines()
    {
        var lines = new List<string>();
        var all = new List<EquipmentEffect>();
        CollectEffects(all);
        EquipmentEffect.DescribeAll(all, 1f, lines);
        return lines;
    }

    // Get formatted description including pieces required and all effects
    public string GetFormattedDescription()
    {
        string desc = $"({piecesRequired} pieces) {effectDescription}";
        foreach (string line in DescribeLines())
            desc += $"\n• {line}";
        return desc;
    }

    // Validate this effect configuration - returns list of issues
    public List<string> ValidateConfiguration()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        Validate(errors, warnings);
        errors.AddRange(warnings);
        return errors;
    }

    /// <summary>Errors (the bonus will not work as intended) and warnings (probably a mistake).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        string owner = string.IsNullOrEmpty(effectName) ? "Set bonus" : effectName;
        if (piecesRequired < 1)
            errors.Add($"{owner}: Pieces Required is {piecesRequired}; it must be at least 1.");

        if (string.IsNullOrEmpty(effectName) || effectName == "New Set Bonus" || effectName == "Set Bonus")
            warnings.Add($"{owner}: give the bonus a descriptive name (e.g. 'Warrior's Vigor').");

        if (!HasEffects())
        {
            errors.Add($"{owner}: does nothing. Add at least one Effect, Stat Bonus, Trait, Trait Enhancement or Special Mechanic.");
            return;
        }

        if (traitsToApply != null && traitsToApply.Any(t => t == null))
            warnings.Add($"{owner}: empty slot(s) in 'Traits to Apply'.");

        if (statBonuses != null)
        {
            for (int i = 0; i < statBonuses.Count; i++)
                statBonuses[i]?.Validate($"{owner} ▸ Stat Bonus #{i + 1}", errors, warnings);
            foreach (var dup in statBonuses.Where(s => s != null).GroupBy(s => s.effectType).Where(g => g.Count() > 1))
                warnings.Add($"{owner}: several stat bonuses for {dup.Key}; consider combining them.");
        }

        if (traitEnhancements != null)
        {
            foreach (TraitEnhancement e in traitEnhancements)
            {
                if (e == null)
                    continue;
                if (e.originalTrait == null)
                    errors.Add($"{owner}: a trait enhancement has no original trait.");
                else if (e.enhancementType == TraitEnhancementType.Upgrade && e.enhancedTrait == null)
                    errors.Add($"{owner}: the upgrade of '{e.originalTrait.Name}' has no enhanced trait.");
                else if (e.enhancementType == TraitEnhancementType.Multiply && e.effectMultiplier <= 0)
                    errors.Add($"{owner}: the enhancement of '{e.originalTrait.Name}' has an invalid multiplier ({e.effectMultiplier}).");
            }
        }

        if (specialMechanics != null)
        {
            foreach (SpecialMechanic m in specialMechanics)
            {
                if (m == null)
                    continue;
                if (string.IsNullOrEmpty(m.mechanicId))
                    errors.Add($"{owner}: a special mechanic has no ID (e.g. 'double_jump').");
                else if (specialMechanics.Count(x => x != null && x.mechanicId == m.mechanicId) > 1)
                    warnings.Add($"{owner}: duplicate special mechanic '{m.mechanicId}'.");
                if (string.IsNullOrEmpty(m.mechanicName))
                    warnings.Add($"{owner}: special mechanic '{m.mechanicId}' has no display name.");
            }
        }

        EquipmentEffect.ValidateAll(effects, owner, errors, warnings);

        if (persistDuration < 0f)
            errors.Add($"{owner}: Persist Duration cannot be negative.");
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

        if (effects != null)
        {
            foreach (EquipmentEffect e in effects)
            {
                if (e is GrantTraitsEffect g) affectedTraits.AddRange(g.traits.Where(t => t != null));
                else if (e is EnhanceTraitEffect en)
                {
                    if (en.trait != null) affectedTraits.Add(en.trait);
                    if (en.replacement != null) affectedTraits.Add(en.replacement);
                }
            }
        }

        return affectedTraits.Distinct().ToList();
    }

    // Check if this effect conflicts with another
    public bool ConflictsWith(ArmorSetEffect other)
    {
        if (other == null || canStack) return false;
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
            upgradeGroup = upgradeGroup,
            canStack = canStack,
            priority = priority,
            persistDuration = persistDuration,
            setEffectPrefab = setEffectPrefab,
            setActivationSound = setActivationSound,
            setActivationParticles = setActivationParticles
        };

        clone.traitsToApply = new List<Trait>(traitsToApply ?? new List<Trait>());
        clone.traitEnhancements = new List<TraitEnhancement>(traitEnhancements ?? new List<TraitEnhancement>());
        clone.statBonuses = new List<EquippableEffect>(statBonuses ?? new List<EquippableEffect>());
        clone.specialMechanics = new List<SpecialMechanic>(specialMechanics ?? new List<SpecialMechanic>());
        clone.effects = new List<EquipmentEffect>();
        if (effects != null)
            foreach (EquipmentEffect e in effects)
                clone.effects.Add(e?.Clone());

        return clone;
    }

    // Get debug info
    public string GetDebugInfo()
    {
        var info = $"Effect: {effectName} ({piecesRequired} pieces){(string.IsNullOrEmpty(upgradeGroup) ? "" : $" [group {upgradeGroup}]")}\n";
        info += $"• Valid: {(ValidateConfiguration().Count == 0 ? "Yes" : "No")}\n";
        info += $"• Has Effects: {HasEffects()}\n";
        info += $"• Traits: {traitsToApply?.Count(t => t != null) ?? 0}\n";
        info += $"• Stat Bonuses: {statBonuses?.Count(s => s != null && s.amount != 0) ?? 0}\n";
        info += $"• Enhancements: {traitEnhancements?.Count(e => e != null && e.originalTrait != null) ?? 0}\n";
        info += $"• Special Mechanics: {specialMechanics?.Count(m => m != null && !string.IsNullOrEmpty(m.mechanicId)) ?? 0}\n";
        info += $"• Effects: {effects?.Count(e => e != null) ?? 0}";

        return info;
    }
}
