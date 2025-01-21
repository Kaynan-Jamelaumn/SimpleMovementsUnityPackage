using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumerates the different types of effects that a consumable can apply.
/// </summary>
public enum ConsumableEffectType
{
    Hp,                    // Affects health points.
    Stamina,               // Affects stamina.
    Food,                  // Affects food consumption or hunger level.
    Drink,                 // Affects drink consumption or thirst level.
    Weight,                // Affects the player's weight capacity.
    Speed,                 // Affects movement speed.
    HpRegeneration,        // Regenerates health points over time.
    StaminaRegeneration,   // Regenerates stamina over time.
    HpHealFactor,          // Boosts healing effects on health points.
    StaminaHealFactor,     // Boosts healing effects on stamina.
    HpDamageFactor,        // Increases damage received to health.
    StaminaDamageFactor    // Increases stamina drain or damage.
}

/// <summary>
/// Enumerates the types of consumable items available.
/// </summary>
public enum ConsumableType
{
    Potion,  // Consumable is a potion.
    Food     // Consumable is food.
}

/// <summary>
/// Defines the properties and behaviors of a consumable effect.
/// </summary>
[System.Serializable]
public class ConsumableEffect
{
    // Basic Effect Information
    [Header("Effect Information")]
    public ConsumableEffectType effectType;  // Type of effect (e.g., Hp, Stamina).
    public ConsumableType itemType;          // Type of consumable item (e.g., Potion, Food).
    public string effectName;                // Name of the effect.
    public float amount;                     // Magnitude of the effect.

    // Effect Timers
    [Header("Effect Timing")]
    [Tooltip("The duration of the effect.")]
    public float timeBuffEffect;             // Duration of the effect.
    [Tooltip("How much time for the effect to tick again.")]
    public float tickCooldown;               // Time interval for periodic effects.
    [Tooltip("The amount will be divided by the time.")]
    public bool isProcedural;                // Whether the effect applies incrementally.
    [Tooltip("If the same effect is being applied, can it stack or only reset the time?")]
    public bool isStackable;                 // Whether the effect can stack.

    // Randomization
    [Header("Random Effect Values")]
    public bool randomAmount;                // Whether to randomize the effect amount.
    public bool randomTimeBuffEffect;        // Whether to randomize the duration.
    public bool randomTickCooldown;          // Whether to randomize the tick cooldown.
    public float minAmount;                  // Minimum effect amount.
    public float maxAmount;                  // Maximum effect amount.
    public float minTimeBuffEffect;          // Minimum duration.
    public float maxTimeBuffEffect;          // Maximum duration.
    public float minTickCooldown;            // Minimum tick cooldown.
    public float maxTickCooldown;            // Maximum tick cooldown.
}

/// <summary>
/// Scriptable Object that represents a consumable item and its associated effects.
/// </summary>

[CreateAssetMenu(fileName = "Consumable", menuName = "Scriptable Objects/Item/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Buff")]
    [SerializeField]
    private List<ConsumableEffect> effects;

    /// <summary>Gets the list of effects associated with this consumable.</summary>
    public List<ConsumableEffect> Effects => effects;

    /// <summary>Initializes a new instance of the <see cref="ConsumableSO"/> class with default settings.</summary>
    public ConsumableSO()
    {
        durabilityReductionPerUse = 1;
    }

    // Dictionary to map consumable effect types to their corresponding actions
    private Dictionary<ConsumableEffectType, Action<ConsumableEffect, PlayerStatusController>> effectActions;

    // Lazy initialization of effect actions
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
    /// <summary>
    /// Uses the consumable item and applies all associated effects to the player.
    /// </summary>
    /// <param name="playerObject">The player game object.</param>
    /// <param name="statusController">The player's status controller. Can be null if not required.</param>
    public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
    {
        base.UseItem(playerObject, statusController);

        // Iterate through each effect and attempt to apply it using the corresponding action.
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

    /// <summary>
    /// Initializes the dictionary that maps <see cref="ConsumableEffectType"/> to their corresponding actions.
    /// </summary>
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

    /// <summary>
    /// Creates a handler to manage the immediate and procedural effects of a consumable.
    /// </summary>
    /// <param name="getDirectAction">A function that retrieves the action for an immediate effect.</param>
    /// <param name="getEffectAction">A function that retrieves the action for a procedural effect.</param>
    /// <returns>An action to handle the consumable effect.</returns>
    private Action<ConsumableEffect, PlayerStatusController> CreateHandler(
        Func<PlayerStatusController, Action<float>> getDirectAction,
        Func<PlayerStatusController, Action<string, float, float, float, bool, bool>> getEffectAction)
    {
        return (effect, controller) =>
        {
            var directAction = getDirectAction?.Invoke(controller);
            var effectAction = getEffectAction?.Invoke(controller);

            // Determine whether to apply an immediate or procedural effect.
            if (effect.timeBuffEffect == 0)
            {
                ApplyEffect(effect, controller, directAction, null); // Immediate effect only.
            }
            else
            {
                ApplyEffect(effect, controller, null, effectAction); // Procedural effect only.
            }
        };
    }

    /// <summary>
    /// Applies a consumable effect to the player.
    /// </summary>
    /// <param name="effect">The consumable effect to be applied.</param>
    /// <param name="statusController">The player's status controller.</param>
    /// <param name="applyImmediateEffect">The action to apply an immediate effect.</param>
    /// <param name="applyBuffEffect">The action to apply a procedural (buff) effect.</param>
    private void ApplyEffect(ConsumableEffect effect, PlayerStatusController statusController,
        Action<float> applyImmediateEffect, Action<string, float, float, float, bool, bool> applyBuffEffect)
    {
        // Calculate the amount of the effect, applying randomization if enabled.
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);

        // Apply the immediate effect if provided.
        if (applyImmediateEffect != null)
        {
            applyImmediateEffect(amount);
        }

        // Apply the procedural (buff) effect if provided.
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