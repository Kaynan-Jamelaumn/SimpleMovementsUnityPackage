using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttackAction : IAttackComponent
{
    [Header("Action Configuration")]
    public AttackType actionType;
    public string actionName;

    [Header("Animation & Timing")]
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] public float animationSpeed = 1.0f;
    [SerializeField] private float startupFrames = 0.1f;
    [SerializeField] private float activeFrames = 0.2f;
    [SerializeField] private float recoveryFrames = 0.3f;
    public float variantTime = 1.0f;

    [Header("Combat Properties")]
    public float staminaCost;
    [SerializeField] private bool lockMovement = false;
    [SerializeField] private float movementSpeedMultiplier = 1.0f;
    [SerializeField] private Vector3 forwardMovement = Vector3.zero;

    [Header("Trait Requirements")]
    [Tooltip("Player or weapon must have one of these traits to perform this action")]
    public List<Trait> requiredTraits = new List<Trait>();
    [Tooltip("Action is enhanced if these traits are present")]
    public List<Trait> enhancementTraits = new List<Trait>();

    [Header("Trait Enhancement Modifiers")]
    [Tooltip("Applied when enhancement traits are present")]
    [SerializeField] private float enhancedAnimationSpeedMultiplier = 1.0f;
    [SerializeField] private float enhancedStaminaCostMultiplier = 1.0f;
    [SerializeField] private float enhancedDamageMultiplier = 1.0f;
    [SerializeField] private List<AttackActionEffect> enhancementEffects = new List<AttackActionEffect>();

    [Header("Effects")]
    [SerializeField] private List<AttackActionEffect> effects = new List<AttackActionEffect>();

    [Header("Audio & Visual")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private ParticleSystem attackParticles;
    [SerializeField] private GameObject trailEffect;

    [Header("Variations")]
    [Tooltip("Additional variations of this action type")]
    public List<AttackVariation> variations = new List<AttackVariation>();

    // IAttackComponent implementation
    public float StartupFrames => startupFrames;
    public float ActiveFrames => activeFrames;
    public float RecoveryFrames => recoveryFrames;
    public float AnimationSpeed => animationSpeed;
    public float StaminaCost => staminaCost;  // Added this
    public bool LockMovement => lockMovement;
    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public Vector3 ForwardMovement => forwardMovement;
    public AnimationClip AnimationClip => animationClip;
    public AudioClip AttackSound => attackSound;
    public ParticleSystem AttackParticles => attackParticles;
    public GameObject TrailEffect => trailEffect;
    public List<AttackActionEffect> Effects => effects;

    public float GetTotalDuration()
    {
        return (startupFrames + activeFrames + recoveryFrames) / animationSpeed;
    }

    public bool IsInActiveFrames(float normalizedTime)
    {
        float startTime = startupFrames / GetTotalDuration();
        float endTime = (startupFrames + activeFrames) / GetTotalDuration();
        return normalizedTime >= startTime && normalizedTime <= endTime;
    }

    public bool IsInRecoveryFrames(float normalizedTime)
    {
        float recoveryStart = (startupFrames + activeFrames) / GetTotalDuration();
        return normalizedTime >= recoveryStart;
    }

    public AttackVariation GetVariation(int variationIndex)
    {
        if (variations == null || variations.Count == 0)
            return null;
        return variations[variationIndex % variations.Count];
    }

    public int GetVariationCount() => variations?.Count ?? 0;

    // Trait-related methods - Fixed to use references instead of names
    public bool CanPerformWithTraits(TraitManager playerTraits, WeaponSO weapon)
    {
        // If no traits required, action is always available
        if (requiredTraits.Count == 0) return true;

        foreach (var requiredTrait in requiredTraits)
        {
            if (requiredTrait == null) continue;

            // Check player traits
            if (playerTraits != null && playerTraits.HasTrait(requiredTrait))
                return true;

            // Check weapon traits
            if (weapon != null && weapon.HasTrait(requiredTrait))
                return true;
        }

        // If we get here, no required traits were found
        return false;
    }

    public bool HasEnhancementTrait(TraitManager playerTraits, WeaponSO weapon, out Trait foundTrait)
    {
        foundTrait = null;

        foreach (var enhancementTrait in enhancementTraits)
        {
            if (enhancementTrait == null) continue;

            // Check player traits
            if (playerTraits != null && playerTraits.HasTrait(enhancementTrait))
            {
                foundTrait = enhancementTrait;
                return true;
            }

            // Check weapon traits
            if (weapon != null && weapon.HasTrait(enhancementTrait))
            {
                foundTrait = enhancementTrait;
                return true;
            }
        }

        return false;
    }

    public void ApplyTraitEnhancements(TraitManager playerTraits, WeaponSO weapon)
    {
        if (HasEnhancementTrait(playerTraits, weapon, out Trait enhancementTrait))
        {
            // Apply the configured enhancement modifiers
            animationSpeed *= enhancedAnimationSpeedMultiplier;
            staminaCost *= enhancedStaminaCostMultiplier;

            // Add enhancement effects
            foreach (var enhancementEffect in enhancementEffects)
            {
                if (enhancementEffect != null)
                {
                    // Clone the effect to avoid modifying the original
                    var clonedEffect = new AttackActionEffect
                    {
                        effectType = enhancementEffect.effectType,
                        amount = enhancementEffect.amount * enhancedDamageMultiplier,
                        effectName = enhancementEffect.effectName + "_Enhanced",
                        enemyEffect = enhancementEffect.enemyEffect,
                        isProcedural = enhancementEffect.isProcedural,
                        probabilityToApply = enhancementEffect.probabilityToApply,
                        criticalChance = enhancementEffect.criticalChance,
                        criticalDamageMultiplier = enhancementEffect.criticalDamageMultiplier
                    };
                    effects.Add(clonedEffect);
                }
            }

            // Apply trait-specific effects from the trait itself
            ApplyTraitSpecificEffects(enhancementTrait);

            Debug.Log($"Applied {enhancementTrait.Name} enhancement to {actionName}");
        }
    }

    private void ApplyTraitSpecificEffects(Trait trait)
    {
        if (trait == null) return;

        // Apply effects from the trait's own effect list
        foreach (var traitEffect in trait.effects)
        {
            if (traitEffect == null) continue;

            // Apply animation speed modifiers
            if (traitEffect.targetStat.ToLower() == "attackspeed" && traitEffect.effectType == TraitEffectType.StatMultiplier)
            {
                animationSpeed *= traitEffect.value;
            }

            // Apply stamina cost modifiers
            if (traitEffect.targetStat.ToLower() == "staminacost" && traitEffect.effectType == TraitEffectType.ConsumptionRate)
            {
                staminaCost *= traitEffect.value;
            }

            // Apply damage modifiers
            if (traitEffect.targetStat.ToLower() == "damage" && traitEffect.effectType == TraitEffectType.StatMultiplier)
            {
                // Increase damage of all damage effects
                foreach (var effect in effects)
                {
                    if (effect.effectType == AttackEffectType.Hp)
                    {
                        effect.amount *= traitEffect.value;
                    }
                }
            }
        }
    }

    public float GetModifiedStaminaCost(TraitManager playerTraits, WeaponSO weapon)
    {
        float modifiedCost = staminaCost;

        // Check player traits for stamina modifiers
        if (playerTraits != null)
        {
            foreach (var trait in playerTraits.ActiveTraits)
            {
                if (trait == null) continue;

                foreach (var effect in trait.effects)
                {
                    if (effect.targetStat.ToLower() == "staminacost" && effect.effectType == TraitEffectType.ConsumptionRate)
                    {
                        modifiedCost *= effect.value;
                    }
                }
            }
        }

        // Weapon trait modifications are handled in WeaponSO.CalculateTraitModifiedStaminaCost

        return modifiedCost;
    }

    // Debug helper
    public string GetRequiredTraitsString()
    {
        if (requiredTraits.Count == 0) return "None";
        var traitNames = new List<string>();
        foreach (var trait in requiredTraits)
        {
            if (trait != null) traitNames.Add(trait.Name);
        }
        return string.Join(", ", traitNames);
    }

    public string GetEnhancementTraitsString()
    {
        if (enhancementTraits.Count == 0) return "None";
        var traitNames = new List<string>();
        foreach (var trait in enhancementTraits)
        {
            if (trait != null) traitNames.Add(trait.Name);
        }
        return string.Join(", ", traitNames);
    }
}