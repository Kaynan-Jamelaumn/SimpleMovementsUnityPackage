using System;
using System.Collections.Generic;
using UnityEngine;

public enum ConsumableEffectType
{
    // Core status effects
    Hp,
    Stamina,
    Food,
    Drink,
    Weight,
    Speed,
    HpRegeneration,
    StaminaRegeneration,
    HpHealFactor,
    StaminaHealFactor,
    HpDamageFactor,
    StaminaDamageFactor,

    // Speed modifiers
    SpeedFactor,
    SpeedMultiplier,

    // Extended status effects
    Sleep,
    SleepFactor,
    Sanity,
    SanityHealFactor,
    SanityDamageFactor,
    Mana,
    ManaRegeneration,
    ManaHealFactor,
    ManaDamageFactor,
    BodyHeat,
    BodyHeatFactor,
    Oxygen,
    OxygenFactor,

    // Max value modifiers
    MaxHp,
    MaxStamina,
    MaxMana,
    MaxHunger,
    MaxThirst,
    MaxWeight,
    MaxSleep,
    MaxSanity,
    MaxBodyHeat,
    MaxOxygen,

    // Survival regeneration
    HungerRegeneration,
    ThirstRegeneration,
    SleepRegeneration,
    SanityRegeneration,
    BodyHeatRegeneration,
    OxygenRegeneration,

    // Survival factors
    HungerHealFactor,
    ThirstHealFactor,
    SleepHealFactor,
    BodyHeatHealFactor,
    OxygenHealFactor,
    HungerDamageFactor,
    ThirstDamageFactor,
    SleepDamageFactor, 
    BodyHeatDamageFactor,
    OxygenDamageFactor
}

public enum ConsumableType
{
    Potion,
    Food
}

[System.Serializable]
public class ConsumableEffect
{
    [Header("Effect Information")]
    public ConsumableEffectType effectType;
    public ConsumableType itemType;
    public string effectName;
    public float amount;

    [Header("Effect Timing")]
    [Tooltip("The duration of the effect.")]
    public float timeBuffEffect;
    [Tooltip("How much time for the effect to tick again.")]
    public float tickCooldown;
    [Tooltip("The amount will be divided by the time.")]
    public bool isProcedural;
    [Tooltip("If the same effect is being applied, can it stack or only reset the time?")]
    public bool isStackable;

    [Header("Random Effect Values")]
    public bool randomAmount;
    public bool randomTimeBuffEffect;
    public bool randomTickCooldown;
    public float minAmount;
    public float maxAmount;
    public float minTimeBuffEffect;
    public float maxTimeBuffEffect;
    public float minTickCooldown;
    public float maxTickCooldown;

    [Header("Application Conditions")]
    [Tooltip("Probability of applying the effect (0-1)")]
    [Range(0f, 1f)]
    public float applicationChance = 1f;

    [Tooltip("Minimum player level required for this effect")]
    public int minimumLevel = 1;

    public float GetRandomizedAmount()
    {
        return randomAmount ? UnityEngine.Random.Range(minAmount, maxAmount) : amount;
    }

    public float GetRandomizedDuration()
    {
        return randomTimeBuffEffect ? UnityEngine.Random.Range(minTimeBuffEffect, maxTimeBuffEffect) : timeBuffEffect;
    }

    public float GetRandomizedTickCooldown()
    {
        return randomTickCooldown ? UnityEngine.Random.Range(minTickCooldown, maxTickCooldown) : tickCooldown;
    }

    public bool ShouldApplyEffect(int playerLevel = 1)
    {
        return playerLevel >= minimumLevel && UnityEngine.Random.value <= applicationChance;
    }
}

[CreateAssetMenu(fileName = "Consumable", menuName = "Scriptable Objects/Item/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Effects")]
    [SerializeField]
    private List<ConsumableEffect> effects;

    [Header("Consumable Settings")]
    [Tooltip("Play sound effect when consumed")]
    public bool playSoundOnUse = true;

    [Tooltip("Show visual effect when consumed")]
    public bool showVisualEffectOnUse = true;

    [Tooltip("Delay before effects are applied (in seconds)")]
    public float effectDelay = 0f;

    public List<ConsumableEffect> Effects => effects;

    private static readonly Dictionary<ConsumableEffectType, Func<PlayerStatusController, bool>> StatusManagerChecks =
        new Dictionary<ConsumableEffectType, Func<PlayerStatusController, bool>>
        {
            { ConsumableEffectType.Hp, controller => controller.HpManager != null },
            { ConsumableEffectType.Stamina, controller => controller.StaminaManager != null },
            { ConsumableEffectType.Food, controller => controller.HungerManager != null },
            { ConsumableEffectType.Drink, controller => controller.ThirstManager != null },
            { ConsumableEffectType.Weight, controller => controller.WeightManager != null },
            { ConsumableEffectType.Speed, controller => controller.SpeedManager != null },
            { ConsumableEffectType.Sleep, controller => controller.SleepManager != null },
            { ConsumableEffectType.Sanity, controller => controller.SanityManager != null },
            { ConsumableEffectType.Mana, controller => controller.ManaManager != null },
            { ConsumableEffectType.BodyHeat, controller => controller.BodyHeatManager != null },
            { ConsumableEffectType.Oxygen, controller => controller.OxygenManager != null }
        };

    public ConsumableSO()
    {
        durabilityReductionPerUse = 1;
    }

    public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
    {
        base.UseItem(playerObject, statusController);

        if (statusController == null)
        {
            Debug.LogWarning("PlayerStatusController is null. Cannot apply consumable effects.");
            return;
        }

        if (effectDelay > 0f)
        {
            statusController.StartCoroutine(ApplyEffectsWithDelay(statusController, effectDelay));
        }
        else
        {
            ApplyAllEffects(statusController);
        }
    }

    private System.Collections.IEnumerator ApplyEffectsWithDelay(PlayerStatusController statusController, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        ApplyAllEffects(statusController);
    }

    private void ApplyAllEffects(PlayerStatusController statusController)
    {
        int playerLevel = statusController.XPManager?.CurrentLevel ?? 1;

        foreach (var effect in effects)
        {
            if (!effect.ShouldApplyEffect(playerLevel))
                continue;

            if (!IsStatusManagerAvailable(effect.effectType, statusController))
            {
                Debug.LogWarning($"Status manager for effect type {effect.effectType} is not available.");
                continue;
            }

            ApplyEffect(effect, statusController);
        }
    }

    private bool IsStatusManagerAvailable(ConsumableEffectType effectType, PlayerStatusController statusController)
    {
        return !StatusManagerChecks.TryGetValue(effectType, out var check) || check(statusController);
    }

    private void ApplyEffect(ConsumableEffect effect, PlayerStatusController statusController)
    {
        float amount = effect.GetRandomizedAmount();
        float duration = effect.GetRandomizedDuration();
        float cooldown = effect.GetRandomizedTickCooldown();

        try
        {
            switch (effect.effectType)
            {
                // Core status effects
                case ConsumableEffectType.Hp:
                    ApplyStatusEffect(effect, statusController.HpManager.AddCurrentValue,
                        statusController.HpManager.AddHpEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.Stamina:
                    ApplyStatusEffect(effect, statusController.StaminaManager.AddCurrentValue,
                        statusController.StaminaManager.AddStaminaEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.Food:
                    ApplyStatusEffect(effect, statusController.HungerManager.AddCurrentValue,
                        statusController.HungerManager.AddFoodEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.Drink:
                    ApplyStatusEffect(effect, statusController.ThirstManager.AddCurrentValue,
                        statusController.ThirstManager.AddDrinkEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.Weight:
                    statusController.WeightManager.AddWeightEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.Speed:
                    ApplyStatusEffect(effect, statusController.SpeedManager.ModifySpeed,
                        statusController.SpeedManager.AddSpeedEffect, amount, duration, cooldown);
                    break;

                // Regeneration effects
                case ConsumableEffectType.HpRegeneration:
                    statusController.HpManager.AddHpRegenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.StaminaRegeneration:
                    statusController.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ManaRegeneration:
                    statusController.ManaManager.AddManaRegenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.HungerRegeneration:
                    statusController.HungerManager.AddFoodEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ThirstRegeneration:
                    statusController.ThirstManager.AddDrinkEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SleepRegeneration:
                    statusController.SleepManager.AddSleepEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SanityRegeneration:
                    statusController.SanityManager.AddSanityEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.BodyHeatRegeneration:
                    statusController.BodyHeatManager.AddBodyHeatEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.OxygenRegeneration:
                    statusController.OxygenManager.AddOxygenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                // Heal factor effects
                case ConsumableEffectType.HpHealFactor:
                    statusController.HpManager.AddHpHealFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.StaminaHealFactor:
                    statusController.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ManaHealFactor:
                    statusController.ManaManager.AddManaHealFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.HungerHealFactor:
                    statusController.HungerManager.AddFoodEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ThirstHealFactor:
                    statusController.ThirstManager.AddDrinkEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SleepHealFactor:
                    statusController.SleepManager.AddSleepEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SanityHealFactor:
                    statusController.SanityManager.AddSanityHealFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.BodyHeatHealFactor:
                    statusController.BodyHeatManager.AddBodyHeatEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.OxygenHealFactor:
                    statusController.OxygenManager.AddOxygenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                // Damage factor effects
                case ConsumableEffectType.HpDamageFactor:
                    statusController.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.StaminaDamageFactor:
                    statusController.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ManaDamageFactor:
                    statusController.ManaManager.AddManaDamageFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.HungerDamageFactor:
                    statusController.HungerManager.AddFoodEffect(effect.effectName, -amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.ThirstDamageFactor:
                    statusController.ThirstManager.AddDrinkEffect(effect.effectName, -amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SleepDamageFactor:
                    statusController.SleepManager.AddSleepEffect(effect.effectName, -amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SanityDamageFactor:
                    statusController.SanityManager.AddSanityDamageFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.BodyHeatDamageFactor:
                    statusController.BodyHeatManager.AddBodyHeatEffect(effect.effectName, -amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.OxygenDamageFactor:
                    statusController.OxygenManager.AddOxygenEffect(effect.effectName, -amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                // Extended status effects
                case ConsumableEffectType.Sleep:
                    ApplyStatusEffect(effect, statusController.SleepManager.AddCurrentValue,
                        statusController.SleepManager.AddSleepEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.SleepFactor:
                    statusController.SleepManager.AddSleepEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.Sanity:
                    ApplyStatusEffect(effect, statusController.SanityManager.AddCurrentValue,
                        statusController.SanityManager.AddSanityEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.Mana:
                    ApplyStatusEffect(effect, statusController.ManaManager.AddCurrentValue,
                        statusController.ManaManager.AddManaEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.BodyHeat:
                    ApplyStatusEffect(effect, (amt) => statusController.BodyHeatManager.ModifyBodyHeat(amt),
                        statusController.BodyHeatManager.AddBodyHeatEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.BodyHeatFactor:
                    statusController.BodyHeatManager.AddBodyHeatEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.Oxygen:
                    ApplyStatusEffect(effect, statusController.OxygenManager.AddCurrentValue,
                        statusController.OxygenManager.AddOxygenEffect, amount, duration, cooldown);
                    break;

                case ConsumableEffectType.OxygenFactor:
                    statusController.OxygenManager.AddOxygenEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                // Speed modifiers
                case ConsumableEffectType.SpeedFactor:
                    statusController.SpeedManager.AddSpeedFactorEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                case ConsumableEffectType.SpeedMultiplier:
                    statusController.SpeedManager.AddSpeedMultiplierEffect(effect.effectName, amount, duration, cooldown,
                        effect.isProcedural, effect.isStackable);
                    break;

                // Max value modifiers
                case ConsumableEffectType.MaxHp:
                    statusController.HpManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxStamina:
                    statusController.StaminaManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxMana:
                    statusController.ManaManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxHunger:
                    statusController.HungerManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxThirst:
                    statusController.ThirstManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxWeight:
                    statusController.WeightManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxSleep:
                    statusController.SleepManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxSanity:
                    statusController.SanityManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxBodyHeat:
                    statusController.BodyHeatManager.ModifyMaxValue(amount);
                    break;

                case ConsumableEffectType.MaxOxygen:
                    statusController.OxygenManager.ModifyMaxValue(amount);
                    break;

                default:
                    Debug.LogWarning($"Effect type {effect.effectType} is not implemented in the consumable system.");
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error applying consumable effect {effect.effectType}: {ex.Message}");
        }
    }

    private void ApplyStatusEffect(ConsumableEffect effect, Action<float> immediateAction,
        Action<string, float, float, float, bool, bool> timedAction, float amount, float duration, float cooldown)
    {
        if (duration <= 0)
        {
            immediateAction?.Invoke(amount);
        }
        else
        {
            timedAction?.Invoke(effect.effectName, amount, duration, cooldown, effect.isProcedural, effect.isStackable);
        }
    }

    public bool HasEffect(ConsumableEffectType effectType)
    {
        return effects.Exists(effect => effect.effectType == effectType);
    }

    public List<ConsumableEffect> GetEffectsOfType(ConsumableEffectType effectType)
    {
        return effects.FindAll(effect => effect.effectType == effectType);
    }

    public float GetTotalEffectAmount(ConsumableEffectType effectType)
    {
        float total = 0f;
        foreach (var effect in effects)
        {
            if (effect.effectType == effectType)
            {
                total += effect.GetRandomizedAmount();
            }
        }
        return total;
    }

    public string GetEffectDescription()
    {
        if (effects.Count == 0)
            return "No effects";

        var descriptions = new List<string>();
        foreach (var effect in effects)
        {
            string sign = effect.amount >= 0 ? "+" : "";
            descriptions.Add($"{sign}{effect.amount} {effect.effectType}");
        }

        return string.Join(", ", descriptions);
    }
}