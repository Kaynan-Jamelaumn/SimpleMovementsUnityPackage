using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ComboCondition
{
    public enum ConditionType
    {
        InputTiming,
        StaminaThreshold,
        HealthThreshold,
        TraitRequired,
        WeaponTraitRequired,
        StatusEffect,
        ComboCount,
        ManaThreshold,
        ElementalCharge
    }

    public ConditionType type;
    public float threshold;

    [Header("Trait References")]
    [Tooltip("Required trait for TraitRequired or WeaponTraitRequired conditions")]
    public Trait requiredTrait;

    [Header("Status Effect")]
    [Tooltip("Required status effect name for StatusEffect condition")]
    public string requiredStatusEffect;

    public bool inverse = false;

    public bool Evaluate(PlayerStatusController player, GameObject target, int comboCount, WeaponController weaponController)
    {
        bool result = false;

        switch (type)
        {
            case ConditionType.StaminaThreshold:
                result = player.StaminaManager.CurrentValue >= threshold;
                break;

            case ConditionType.HealthThreshold:
                result = (player.HpManager.CurrentValue / player.HpManager.MaxValue) >= threshold;
                break;

            case ConditionType.ManaThreshold:
                result = player.ManaManager.CurrentValue >= threshold;
                break;

            case ConditionType.TraitRequired:
                // Check if player has the required trait
                if (requiredTrait != null)
                {
                    var traitManager = player.GetComponent<TraitManager>();
                    result = traitManager != null && traitManager.HasTrait(requiredTrait);
                }
                break;

            case ConditionType.WeaponTraitRequired:
                // Check if weapon has the required trait
                if (requiredTrait != null && weaponController.EquippedWeapon != null)
                {
                    result = weaponController.EquippedWeapon.HasTrait(requiredTrait);
                }
                break;

            case ConditionType.ComboCount:
                result = comboCount >= threshold;
                break;

            case ConditionType.StatusEffect:
                // Check if target has status effect
                if (target != null && !string.IsNullOrEmpty(requiredStatusEffect))
                {
                    var statusController = target.GetComponent<BaseStatusController>();
                    // Note: You'll need to implement HasStatusEffect method in BaseStatusController
                    // result = statusController?.HasStatusEffect(requiredStatusEffect) ?? false;

                    // For now, return false as placeholder
                    result = false;
                }
                break;

            case ConditionType.InputTiming:
                // This would need timing window implementation
                // For now, always true as placeholder
                result = true;
                break;

            case ConditionType.ElementalCharge:
                // This would need elemental charge tracking
                // For now, check if threshold is met (placeholder)
                result = threshold <= 1f;
                break;
        }

        return inverse ? !result : result;
    }
}

[System.Serializable]
public class ComboBranch
{
    [Header("Branch Information")]
    public string branchName;
    public AttackType triggerInput;
    public List<ComboCondition> conditions = new List<ComboCondition>();

    [Header("Branch Action")]
    public AttackAction branchAction;
    public bool isFinisher = false;
    public bool resetsCombo = false;

    [Header("Rewards")]
    public float damageBonus = 0f;
    public int experienceBonus = 0;
    public List<AttackEffect> bonusEffects = new List<AttackEffect>();

    [Header("Trait Modifiers")]
    [Tooltip("These traits will modify the branch execution")]
    public List<Trait> branchModifierTraits = new List<Trait>();
    [Tooltip("Damage multiplier when modifier traits are present")]
    public float traitDamageMultiplier = 1.0f;
    [Tooltip("Experience multiplier when modifier traits are present")]
    public float traitExperienceMultiplier = 1.0f;

    [Header("Visual")]
    public ParticleSystem branchParticles;
    public AudioClip branchSound;

    public bool CanExecute(PlayerStatusController player, GameObject target, int comboCount, WeaponController weaponController)
    {
        foreach (var condition in conditions)
        {
            if (!condition.Evaluate(player, target, comboCount, weaponController))
                return false;
        }
        return true;
    }

    public float GetModifiedDamageBonus(TraitManager playerTraits, WeaponSO weapon)
    {
        float modifiedBonus = damageBonus;

        foreach (var modifierTrait in branchModifierTraits)
        {
            if (modifierTrait == null) continue;

            bool hasTrait = false;

            // Check player traits
            if (playerTraits != null && playerTraits.HasTrait(modifierTrait))
                hasTrait = true;

            // Check weapon traits
            if (!hasTrait && weapon != null && weapon.HasTrait(modifierTrait))
                hasTrait = true;

            if (hasTrait)
            {
                modifiedBonus *= traitDamageMultiplier;
            }
        }

        return modifiedBonus;
    }

    public int GetModifiedExperienceBonus(TraitManager playerTraits, WeaponSO weapon)
    {
        float modifiedBonus = experienceBonus;

        foreach (var modifierTrait in branchModifierTraits)
        {
            if (modifierTrait == null) continue;

            bool hasTrait = false;

            // Check player traits
            if (playerTraits != null && playerTraits.HasTrait(modifierTrait))
                hasTrait = true;

            // Check weapon traits
            if (!hasTrait && weapon != null && weapon.HasTrait(modifierTrait))
                hasTrait = true;

            if (hasTrait)
            {
                modifiedBonus *= traitExperienceMultiplier;
            }
        }

        return Mathf.RoundToInt(modifiedBonus);
    }
}