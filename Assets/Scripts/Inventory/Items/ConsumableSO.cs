using System;
using System.Collections.Generic;
using UnityEngine;

public enum ConsumableEffectType
{
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
}
public enum ConsumableType
{
   Potion,
   Food
}


[System.Serializable]
public class ConsumableEffect
{
    // Basic Effect Information
    [Header("Effect Information")]
    public ConsumableEffectType effectType;
    public ConsumableType itemType;
    public string effectName;
    public float amount;

    // Effect Timers
    [Header("Effect Timing")]
    [Tooltip("The duration of the effect")]
    public float timeBuffEffect;
    [Tooltip("How much time for the effect to tick again")]
    public float tickCooldown;
    [Tooltip("The amount will be divided by the time")]
    public bool isProcedural;
    [Tooltip("If the same effect is being applied, can it stack or only reset the time?")]
    public bool isStackable;

    // Randomization
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
}


[CreateAssetMenu(fileName = "Consumable", menuName = "Scriptable Objects/Item/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Buff")]
    [SerializeField] private List<ConsumableEffect> effects;

    public List<ConsumableEffect> Effects
    {
        get { return effects; }
    }
    public ConsumableSO()
    {
        durabilityReductionPerUse = 1;
    }


    private Dictionary<ConsumableEffectType, Action<ConsumableEffect, PlayerStatusController>> effectActions;
    private Dictionary<ConsumableEffectType, Action<ConsumableEffect, PlayerStatusController>> EffectActions
    {
        get
        {
            if (effectActions == null)
            {
                InitializeEffectActions();
            }
            return effectActions;
        }
    }


    public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
    {
        base.UseItem(playerObject, statusController);

        foreach (var effect in effects)
        {
            if (EffectActions.TryGetValue(effect.effectType, out var action))
            {
                action.Invoke(effect, statusController);
            }
            else
            {
                Debug.LogWarning($"Effect type {effect.effectType} is not supported.");
            }
        }
    }



    private void InitializeEffectActions()
    {
        effectActions = new Dictionary<ConsumableEffectType, Action<ConsumableEffect, PlayerStatusController>>
    {
        { ConsumableEffectType.Hp, CreateHandler((controller) => controller.HpManager.AddHp, (controller) => controller.HpManager.AddHpEffect) },
        { ConsumableEffectType.Stamina, CreateHandler((controller) => controller.StaminaManager.AddStamina, (controller) => controller.StaminaManager.AddStaminaEffect) },
        { ConsumableEffectType.Food, CreateHandler((controller) => controller.FoodManager.AddFood, (controller) => controller.FoodManager.AddFoodEffect) },
        { ConsumableEffectType.Drink, CreateHandler((controller) => controller.DrinkManager.AddDrink, (controller) => controller.DrinkManager.AddDrinkEffect) },
        { ConsumableEffectType.Weight, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.WeightManager.AddWeightEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.HpHealFactor, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.HpManager.AddHpHealFactorEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.HpDamageFactor, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.StaminaHealFactor, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.StaminaDamageFactor, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.StaminaRegeneration, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) },
        { ConsumableEffectType.HpRegeneration, (effect, controller) => ApplyEffect(effect, controller,
            (amount) => controller.HpManager.AddHpRegenEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable), null) }
    };
    }

    private Action<ConsumableEffect, PlayerStatusController> CreateHandler(
        Func<PlayerStatusController, Action<float>> getDirectAction,
        Func<PlayerStatusController, Action<string, float, float, float, bool, bool>> getEffectAction)
    {
        return (effect, controller) =>
        {
            var directAction = getDirectAction?.Invoke(controller);
            var effectAction = getEffectAction?.Invoke(controller);

            // If the effect has a buff effect time, use ApplyEffect with both handlers
            if (effect.timeBuffEffect == 0)
            {
                ApplyEffect(effect, controller, directAction, null); // Only immediate effect handler
            }
            else
            {
                ApplyEffect(effect, controller, null, effectAction); // Only buff effect handler
            }
        };
    }

    private void ApplyEffect(ConsumableEffect effect, PlayerStatusController statusController,
        Action<float> applyImmediateEffect, Action<string, float, float, float, bool, bool> applyBuffEffect)
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);

        // Apply the immediate effect if provided
        if (applyImmediateEffect != null)
        {
            applyImmediateEffect(amount);
        }

        // Apply the buff effect if provided
        if (applyBuffEffect != null)
        {
            applyBuffEffect(effect.effectName, amount, effect.timeBuffEffect, effect.tickCooldown, effect.isProcedural, effect.isStackable);
        }
    }





}

/*








public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
{
    base.UseItem(playerObject, statusController);

    //foreach (var effect in effects)
    //{
    //    ApplyEffect(effect, statusController);

    //}

    // Ensure effect actions are initialized
    if (effectActions == null)
    {
        InitializeEffectActions();
    }

    foreach (var effect in effects)
    {
        if (effectActions.TryGetValue(effect.effectType, out var action))
        {
            action.Invoke(effect, statusController);
        }
        else
        {
            Debug.LogWarning($"Effect type {effect.effectType} is not supported.");
        }
    }
}

private void ApplyEffect(ConsumableEffect effect, PlayerStatusController statusController)
{
    float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
    float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
    float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

    switch (effect.effectType)
    {
        case ConsumableEffectType.Stamina:
            if (effect.timeBuffEffect == 0) statusController.StaminaManager.AddStamina(effect.amount);
            else statusController.StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.Hp:
            if (effect.timeBuffEffect == 0) statusController.HpManager.AddHp(effect.amount);
            else statusController.HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.Food:
            if (effect.timeBuffEffect == 0) statusController.FoodManager.AddFood(effect.amount);
            else statusController.FoodManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.Drink:
            if (effect.timeBuffEffect == 0) statusController.DrinkManager.AddDrink(effect.amount);
            else statusController.DrinkManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.Weight:
            statusController.WeightManager.AddWeightEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.HpHealFactor:
            statusController.HpManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.HpDamageFactor:
            statusController.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.StaminaHealFactor:
            statusController.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.StaminaDamageFactor:
            statusController.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.StaminaRegeneration:
            statusController.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
        case ConsumableEffectType.HpRegeneration:
            statusController.HpManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
            break;
    }*/