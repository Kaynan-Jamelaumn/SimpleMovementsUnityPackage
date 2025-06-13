using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Equippable", menuName = "Scriptable Objects/Item/Equippable")]
public class EquippableSO : ItemSO
{
    [Header("Equippable Effect")]
    [SerializeField]
    private List<EquippableEffect> effects;

    [Header("Armor Set Information")]
    [SerializeField]
    [Tooltip("Armor set this piece belongs to (if any)")]
    private ArmorSet belongsToArmorSet;

    [SerializeField]
    [Tooltip("Visual indicator when part of an active set")]
    private GameObject setVisualEffect;

    [SerializeField]
    [Tooltip("Material override when set effects are active")]
    private Material setActiveMaterial;

    // Properties
    public List<EquippableEffect> Effects => effects;
    public ArmorSet BelongsToArmorSet => belongsToArmorSet;
    public GameObject SetVisualEffect => setVisualEffect;
    public Material SetActiveMaterial => setActiveMaterial;

    // Armor set related methods
    public bool IsPartOfArmorSet()
    {
        return belongsToArmorSet != null;
    }

    public bool IsPartOfArmorSet(ArmorSet armorSet)
    {
        return belongsToArmorSet == armorSet;
    }

    public string GetSetName()
    {
        return belongsToArmorSet != null ? belongsToArmorSet.SetName : "No Set";
    }

    public bool IsCompatibleWithSet(ArmorSet armorSet)
    {
        if (armorSet == null) return false;
        return armorSet.ContainsPiece(this as ArmorSO);
    }

    // Generate set piece ID dynamically when needed (replaces the removed setPieceId field)
    public virtual string GetSetPieceId()
    {
        if (!IsPartOfArmorSet()) return "";
        return $"{belongsToArmorSet.name}_{name}";
    }

    // Apply or remove equipment stats - UPDATED to remove hardcoded special mechanic handling
    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {
        if (statusController == null)
        {
            Debug.LogError("PlayerStatusController is null when applying equipped stats");
            return;
        }

        // Apply each effect through the proper system
        foreach (EquippableEffect effect in effects)
        {
            if (effect == null) continue;

            // All effects go through the hardcoded stat system since we removed special mechanics from EquippableEffectType
            ApplyStatEffect(effect, shouldApply, statusController);
        }
    }

    // Apply stat effects using hardcoded mappings (for status-modifying traits only)
    private void ApplyStatEffect(EquippableEffect effect, bool shouldApply, PlayerStatusController statusController)
    {
        float amount = effect.amount;
        if (!shouldApply) amount *= -1;

        // Initialize the effect actions dictionary with hardcoded mappings for STATUS effects only
        var effectActions = new Dictionary<EquippableEffectType, System.Action<float>>
        {
            // Core stats
            { EquippableEffectType.MaxWeight, amount => statusController.WeightManager.ModifyMaxWeight(amount) },
            { EquippableEffectType.Speed, amount => statusController.SpeedManager.ModifyBaseSpeed(amount) },
            { EquippableEffectType.MaxStamina, amount => statusController.StaminaManager.ModifyMaxValue(amount) },
            { EquippableEffectType.StaminaRegeneration, amount => statusController.StaminaManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.StaminaHealFactor, amount => statusController.StaminaManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.StaminaDamageFactor, amount => statusController.StaminaManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.MaxHp, amount => statusController.HpManager.ModifyMaxValue(amount) },
            { EquippableEffectType.HpRegeneration, amount => statusController.HpManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.HpHealFactor, amount => statusController.HpManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.HpDamageFactor, amount => statusController.HpManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.MaxMana, amount => statusController.ManaManager.ModifyMaxValue(amount) },
            { EquippableEffectType.ManaRegeneration, amount => statusController.ManaManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.ManaHealFactor, amount => statusController.ManaManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.ManaDamageFactor, amount => statusController.ManaManager.ModifyDecrementFactor(amount) },
            
            // Survival stats
            { EquippableEffectType.MaxHunger, amount => statusController.HungerManager.ModifyMaxValue(amount) },
            { EquippableEffectType.MaxThirst, amount => statusController.ThirstManager.ModifyMaxValue(amount) },
            { EquippableEffectType.MaxSleep, amount => statusController.SleepManager.ModifyMaxValue(amount) },
            { EquippableEffectType.MaxSanity, amount => statusController.SanityManager.ModifyMaxValue(amount) },
            { EquippableEffectType.MaxBodyHeat, amount => statusController.BodyHeatManager.ModifyMaxValue(amount) },
            { EquippableEffectType.MaxOxygen, amount => statusController.OxygenManager.ModifyMaxValue(amount) },
            
            // Survival regeneration
            { EquippableEffectType.HungerRegeneration, amount => statusController.HungerManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.ThirstRegeneration, amount => statusController.ThirstManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.SleepRegeneration, amount => statusController.SleepManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.SanityRegeneration, amount => statusController.SanityManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.BodyHeatRegeneration, amount => statusController.BodyHeatManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.OxygenRegeneration, amount => statusController.OxygenManager.ModifyIncrementValue(amount) },
            
            // Survival factors
            { EquippableEffectType.HungerHealFactor, amount => statusController.HungerManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.ThirstHealFactor, amount => statusController.ThirstManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.SleepHealFactor, amount => statusController.SleepManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.SanityHealFactor, amount => statusController.SanityManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.BodyHeatHealFactor, amount => statusController.BodyHeatManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.OxygenHealFactor, amount => statusController.OxygenManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.HungerDamageFactor, amount => statusController.HungerManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.ThirstDamageFactor, amount => statusController.ThirstManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.SleepDamageFactor, amount => statusController.SleepManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.SanityDamageFactor, amount => statusController.SanityManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.BodyHeatDamageFactor, amount => statusController.BodyHeatManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.OxygenDamageFactor, amount => statusController.OxygenManager.ModifyDecrementFactor(amount) },

            // Speed modifiers
            { EquippableEffectType.SpeedFactor, amount => statusController.SpeedManager.ModifyBaseSpeed(statusController.SpeedManager.BaseSpeed * amount) },
            { EquippableEffectType.SpeedMultiplier, amount => statusController.SpeedManager.ModifyBaseSpeed(statusController.SpeedManager.BaseSpeed * (amount - 1f)) },

            // Combat stats (if you have them in your status controller - add as needed)
            { EquippableEffectType.Strength, amount => Debug.Log($"Strength modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.Agility, amount => Debug.Log($"Agility modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.Intelligence, amount => Debug.Log($"Intelligence modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.Endurance, amount => Debug.Log($"Endurance modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.Defense, amount => Debug.Log($"Defense modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.MagicResistance, amount => Debug.Log($"Magic Resistance modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.CriticalChance, amount => Debug.Log($"Critical Chance modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.CriticalDamage, amount => Debug.Log($"Critical Damage modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.AttackSpeed, amount => Debug.Log($"Attack Speed modified by {amount} - implement when combat system is ready") },
            { EquippableEffectType.CastingSpeed, amount => Debug.Log($"Casting Speed modified by {amount} - implement when combat system is ready") }
        };

        if (effectActions.TryGetValue(effect.effectType, out var action))
        {
            action.Invoke(amount);
        }
        else
        {
            Debug.LogWarning($"Effect type {effect.effectType} is not supported in stat effects system");
        }
    }

    // Validation in editor
    private new void OnValidate()
    {
        // Validate effects
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect == null) continue;

                // No more validation for special mechanics since they're removed from EquippableEffectType
                // All remaining effects are valid status effects
            }
        }

        // Validate set relationship if part of a set
        if (IsPartOfArmorSet() && belongsToArmorSet != null)
        {
            // Check if this piece is actually in the set's piece list
            if (!belongsToArmorSet.ContainsPiece(this as ArmorSO))
            {
                Debug.LogWarning($"Armor piece '{name}' claims to belong to set '{belongsToArmorSet.SetName}' but is not in the set's piece list! Please add this piece to the armor set or remove the set reference.");
            }
        }
    }
}