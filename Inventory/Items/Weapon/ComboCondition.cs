using System.Collections.Generic;
using UnityEngine;

/// <summary>A condition a combo branch needs to be taken (see <see cref="ComboBranch"/>).</summary>
[System.Serializable]
public class ComboCondition
{
    /// <summary>Serialized as numbers: only add new types at the END.</summary>
    public enum ConditionType
    {
        /// <summary>The input came at most Threshold seconds after the previous attack started (a quick follow-up).</summary>
        InputTiming,
        /// <summary>Current stamina is at least Threshold.</summary>
        StaminaThreshold,
        /// <summary>Health fraction (0-1) is at least Threshold.</summary>
        HealthThreshold,
        /// <summary>The player has Required Trait.</summary>
        TraitRequired,
        /// <summary>The weapon has Required Trait.</summary>
        WeaponTraitRequired,
        /// <summary>The target has a status: stunned, silenced, rooted, slowed, taunted, bleeding/dot (any damage over time) or an element name (fire, poison...).</summary>
        StatusEffect,
        /// <summary>At least Threshold attacks in the current combo.</summary>
        ComboCount,
        /// <summary>Current mana is at least Threshold.</summary>
        ManaThreshold,
        /// <summary>At least Threshold hits in a row with the weapon's element in this combo.</summary>
        ElementalCharge,
        // Added later
        /// <summary>The previous attack of the combo used Required Input.</summary>
        PreviousAttack,
        /// <summary>The target's health fraction (0-1) is below Threshold (execute finishers).</summary>
        TargetHealthBelow,
        /// <summary>The attack was charged at least Threshold (0-1).</summary>
        ChargedAttack,
        /// <summary>The player is in the air.</summary>
        InAir,
    }

    [Tooltip("What is checked. Hover the options' names in the docs for details; Threshold is read according to the type.")]
    public ConditionType type;
    [Tooltip("Number used by the condition: seconds (Input Timing), amounts (Stamina, Mana), fractions 0-1 (Health, Target Health, Charged Attack) or counts (Combo Count, Elemental Charge).")]
    public float threshold;

    [Header("Trait References")]
    [Tooltip("Required trait for TraitRequired or WeaponTraitRequired conditions")]
    public Trait requiredTrait;

    [Header("Status Effect")]
    [Tooltip("Status Effect condition: stunned, silenced, rooted, slowed, taunted, dot (any damage over time) or an element (fire, poison, ice...).")]
    public string requiredStatusEffect;

    [Header("Previous Attack")]
    [Tooltip("Previous Attack condition: the input of the previous attack in the combo.")]
    public AttackType requiredInput = AttackType.Normal;

    [Tooltip("Reverse the result (e.g. 'NOT in the air').")]
    public bool inverse = false;

    public bool Evaluate(PlayerStatusController player, GameObject target, int comboCount, WeaponController weaponController)
    {
        bool result = false;
        ComboSystem combo = weaponController != null ? weaponController.Combo : null;

        switch (type)
        {
            case ConditionType.StaminaThreshold:
                result = player != null && player.StaminaManager != null && player.StaminaManager.CurrentValue >= threshold;
                break;

            case ConditionType.HealthThreshold:
                result = player != null && player.HpManager != null && player.HpManager.MaxValue > 0f &&
                         player.HpManager.CurrentValue / player.HpManager.MaxValue >= threshold;
                break;

            case ConditionType.ManaThreshold:
                result = player != null && player.ManaManager != null && player.ManaManager.CurrentValue >= threshold;
                break;

            case ConditionType.TraitRequired:
                if (requiredTrait != null && player != null)
                {
                    TraitManager tm = player.TraitManager != null ? player.TraitManager : player.GetComponent<TraitManager>();
                    result = tm != null && tm.HasTrait(requiredTrait);
                }
                break;

            case ConditionType.WeaponTraitRequired:
                result = requiredTrait != null && weaponController != null && weaponController.ConditionWeapon != null &&
                         weaponController.ConditionWeapon.HasTrait(requiredTrait);
                break;

            case ConditionType.ComboCount:
                result = comboCount >= threshold;
                break;

            case ConditionType.StatusEffect:
                result = TargetHasStatus(target, requiredStatusEffect);
                break;

            case ConditionType.InputTiming:
                // A quick follow-up: pressed within Threshold seconds of the previous attack (0 = any time in the combo).
                result = combo != null && combo.PreviousInput.HasValue &&
                         (threshold <= 0f || combo.TimeSinceLastAttack <= threshold);
                break;

            case ConditionType.ElementalCharge:
                result = combo != null && combo.ElementalHitStreak >= Mathf.Max(1f, threshold);
                break;

            case ConditionType.PreviousAttack:
                result = combo != null && combo.PreviousInput.HasValue && combo.PreviousInput.Value == requiredInput;
                break;

            case ConditionType.TargetHealthBelow:
                {
                    CombatEntity e = target != null ? CombatEntity.Resolve(target) : null;
                    result = e != null && e.IsAlive && e.HealthRatio < threshold;
                    break;
                }

            case ConditionType.ChargedAttack:
                result = weaponController != null && weaponController.PendingChargeRatio >= Mathf.Max(0.01f, threshold);
                break;

            case ConditionType.InAir:
                {
                    CharacterController cc = player != null ? player.GetComponentInChildren<CharacterController>() : null;
                    result = cc != null && !cc.isGrounded;
                    break;
                }
        }

        return inverse ? !result : result;
    }

    /// <summary>Does the target currently have the named status (see <see cref="requiredStatusEffect"/>)?</summary>
    public static bool TargetHasStatus(GameObject target, string status)
    {
        if (target == null || string.IsNullOrWhiteSpace(status))
            return false;
        CombatEntity e = CombatEntity.Resolve(target);
        if (e == null)
            return false;
        string s = status.Trim().ToLowerInvariant();
        switch (s)
        {
            case "stun": case "stunned": return e.IsStunned;
            case "silence": case "silenced": return e.IsSilenced;
            case "root": case "rooted": return e.IsRooted;
            case "slow": case "slowed": return e.IsSlowed;
            case "taunt": case "taunted": return e.TauntedBy != null;
            case "dot": case "bleed": case "bleeding": case "damageovertime": return PeriodicEffectRunner.CountOn(e, true) > 0;
            case "burning": s = "fire"; break;
            case "poisoned": s = "poison"; break;
            case "frozen": case "chilled": s = "ice"; break;
        }
        if (System.Enum.TryParse(s, true, out ElementType element) && element != ElementType.None)
            return PeriodicEffectRunner.CountOn(e, true, element) > 0;
        return false;
    }

    public string Describe()
    {
        string text;
        switch (type)
        {
            case ConditionType.InputTiming: text = threshold > 0f ? $"within {threshold:0.##}s of the previous attack" : "during a combo"; break;
            case ConditionType.StaminaThreshold: text = $"stamina ≥ {threshold:0}"; break;
            case ConditionType.HealthThreshold: text = $"health ≥ {threshold * 100f:0}%"; break;
            case ConditionType.TraitRequired: text = $"has {(requiredTrait != null ? requiredTrait.Name : "?")}"; break;
            case ConditionType.WeaponTraitRequired: text = $"weapon has {(requiredTrait != null ? requiredTrait.Name : "?")}"; break;
            case ConditionType.StatusEffect: text = $"target is {requiredStatusEffect}"; break;
            case ConditionType.ComboCount: text = $"combo ≥ {threshold:0} hits"; break;
            case ConditionType.ManaThreshold: text = $"mana ≥ {threshold:0}"; break;
            case ConditionType.ElementalCharge: text = $"{threshold:0} elemental hits in a row"; break;
            case ConditionType.PreviousAttack: text = $"after a {requiredInput} attack"; break;
            case ConditionType.TargetHealthBelow: text = $"target health < {threshold * 100f:0}%"; break;
            case ConditionType.ChargedAttack: text = $"charged ≥ {threshold * 100f:0}%"; break;
            default: text = "in the air"; break;
        }
        return inverse ? "NOT " + text : text;
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if ((type == ConditionType.TraitRequired || type == ConditionType.WeaponTraitRequired) && requiredTrait == null)
            errors.Add($"{owner}: '{type}' needs a Required Trait.");
        if (type == ConditionType.StatusEffect && string.IsNullOrWhiteSpace(requiredStatusEffect))
            errors.Add($"{owner}: 'Status Effect' needs a status name (stunned, slowed, fire...).");
        if ((type == ConditionType.HealthThreshold || type == ConditionType.TargetHealthBelow || type == ConditionType.ChargedAttack) && (threshold < 0f || threshold > 1f))
            warnings.Add($"{owner}: '{type}' reads Threshold as a fraction between 0 and 1 (it is {threshold}).");
    }
}

/// <summary>
/// One branch of a <see cref="ComboTree"/>: the attack performed for an input when its conditions hold (e.g. Heavy
/// after two Normal attacks while the target is stunned: a finisher).
/// </summary>
[System.Serializable]
public class ComboBranch
{
    [Header("Branch Information")]
    public string branchName;
    [Tooltip("The input that takes this branch.")]
    public AttackType triggerInput;
    [Tooltip("All must hold for the branch to be taken.")]
    public List<ComboCondition> conditions = new List<ComboCondition>();
    [Tooltip("When several branches can be taken, the highest priority wins (then finishers last, then the biggest damage bonus).")]
    public int priority = 0;

    [Header("Branch Action")]
    [Tooltip("The attack performed.")]
    public AttackAction branchAction;
    [Tooltip("Ends the combo after this attack (with its rewards).")]
    public bool isFinisher = false;
    [Tooltip("Clears the combo after this attack.")]
    public bool resetsCombo = false;

    [Header("Rewards")]
    [Tooltip("Extra damage for this attack: 0.5 = +50%.")]
    public float damageBonus = 0f;
    [Tooltip("Experience given when the branch ends the combo (finisher).")]
    public int experienceBonus = 0;
    [Tooltip("Classic effects applied to the player when the branch is taken (a heal, a speed buff...).")]
    public List<AttackEffect> bonusEffects = new List<AttackEffect>();

    [Header("Trait Modifiers")]
    [Tooltip("These traits will modify the branch execution")]
    public List<Trait> branchModifierTraits = new List<Trait>();
    [Tooltip("Damage bonus multiplier for each modifier trait present")]
    public float traitDamageMultiplier = 1.0f;
    [Tooltip("Experience multiplier for each modifier trait present")]
    public float traitExperienceMultiplier = 1.0f;

    [Header("Visual")]
    public ParticleSystem branchParticles;
    public AudioClip branchSound;

    public bool CanExecute(PlayerStatusController player, GameObject target, int comboCount, WeaponController weaponController)
    {
        if (branchAction == null)
            return false;
        foreach (var condition in conditions)
        {
            if (condition != null && !condition.Evaluate(player, target, comboCount, weaponController))
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
            bool hasTrait = (playerTraits != null && playerTraits.HasTrait(modifierTrait)) || (weapon != null && weapon.HasTrait(modifierTrait));
            if (hasTrait)
                modifiedBonus *= traitDamageMultiplier;
        }
        return modifiedBonus;
    }

    public int GetModifiedExperienceBonus(TraitManager playerTraits, WeaponSO weapon)
    {
        float modifiedBonus = experienceBonus;
        foreach (var modifierTrait in branchModifierTraits)
        {
            if (modifierTrait == null) continue;
            bool hasTrait = (playerTraits != null && playerTraits.HasTrait(modifierTrait)) || (weapon != null && weapon.HasTrait(modifierTrait));
            if (hasTrait)
                modifiedBonus *= traitExperienceMultiplier;
        }
        return Mathf.RoundToInt(modifiedBonus);
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        string me = $"{owner} ▸ {(string.IsNullOrEmpty(branchName) ? "branch" : branchName)}";
        if (branchAction == null)
            errors.Add($"{me}: no Branch Action.");
        else
            branchAction.Validate(me, errors, warnings);
        if (conditions != null)
            foreach (ComboCondition c in conditions)
                c?.Validate(me, errors, warnings);
    }
}
