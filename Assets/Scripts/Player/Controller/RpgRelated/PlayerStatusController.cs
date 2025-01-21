using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Manages the player's status, including health, stamina, food, drink, and experience.
/// Provides access to various models and controllers for player actions and attributes.
/// </summary>
public class PlayerStatusController : BaseStatusController
{
    [Header("Models")]
    [Tooltip("Handles player movement mechanics.")]
    [SerializeField] private PlayerMovementModel movementModel;

    [Tooltip("Manages the player's stamina.")]
    [SerializeField] private StaminaManager staminaManager;

    [Tooltip("Manages the player's health.")]
    [SerializeField] private HealthManager hpManager;

    [Tooltip("Tracks the player's food consumption and hunger levels.")]
    [SerializeField] private FoodManager foodManager;

    [Tooltip("Tracks the player's drink consumption and thirst levels.")]
    [SerializeField] private DrinkManager drinkManager;

    [Tooltip("Tracks and manages the player's carried weight.")]
    [SerializeField] private WeightManager weightManager;

    [Tooltip("Manages player experience and skill points.")]
    [SerializeField] private ExperienceManager xpManager;

    [Header("Controllers")]
    [Tooltip("Handles player rolling mechanics.")]
    [SerializeField] private PlayerRollModel rollModel;

    [Tooltip("Handles player dashing mechanics.")]
    [SerializeField] private PlayerDashModel dashModel;

    [Tooltip("Controller for player rolling actions.")]
    [SerializeField] private PlayerRollController rollController;

    [Tooltip("Controller for player dashing actions.")]
    [SerializeField] private PlayerDashController dashController;

    /// <summary>
    /// Dictionary mapping attack effect types to their corresponding handlers.
    /// </summary>
    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> effectHandlers;

    /// <summary>
    /// Lazy-initialized dictionary of effect handlers.
    /// </summary>
    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> EffectHandlers
    {
        get
        {
            if (effectHandlers == null)
            {
                InitializeEffectHandlers();
            }
            return effectHandlers;
        }
    }

    /// <summary>
    /// Dictionary mapping stat upgrade keys to their corresponding handlers.
    /// </summary>
    private Dictionary<string, Action> upgradeStatHandlers;

    /// <summary>
    /// Lazy-initialized dictionary of upgrade stat handlers.
    /// </summary>
    private Dictionary<string, Action> UpgradeStatHandlers
    {
        get
        {
            if (upgradeStatHandlers == null)
            {
                InitializeUpgradeStatHandlers();
            }
            return upgradeStatHandlers;
        }
    }

    public BasePlayerClass PlayerClass { get; private set; }

    public ExperienceManager XPManager => xpManager;
    public PlayerMovementModel MovementModel => movementModel;
    public StaminaManager StaminaManager => staminaManager;
    public HealthManager HpManager => hpManager;
    public FoodManager FoodManager => foodManager;
    public DrinkManager DrinkManager => drinkManager;
    public WeightManager WeightManager => weightManager;
    public PlayerRollModel RollModel => rollModel;
    public PlayerDashModel DashModel => dashModel;
    public PlayerRollController RollController => rollController;
    public PlayerDashController DashController => dashController;


    /// <summary>
    /// Caches references to various components required by the controller.
    /// </summary>
    protected override void CacheComponents()
    {
        CacheManager(ref xpManager, "ExperienceManager");
        CacheManager(ref movementModel, "PlayerMovementModel");
        CacheManager(ref staminaManager, "StaminaManager");
        CacheManager(ref hpManager, "HealthManager");
        CacheManager(ref foodManager, "FoodManager");
        CacheManager(ref drinkManager, "DrinkManager");
        CacheManager(ref weightManager, "WeightManager");

        rollModel = GetComponentOrLogError(ref rollModel, "PlayerRollModel");
        dashModel = GetComponentOrLogError(ref dashModel, "PlayerDashModel");
    }

    /// <summary>
    /// Initializes the player status controller and subscribes to necessary events.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        xpManager.OnSkillPointGained += HandleSkillPointGained;
        InitializePlayerClass();
        InitializeEffectHandlers();
    }

    /// <summary>
    /// Updates the player status each frame.
    /// </summary>
    private void Update()
    {
        UpdateStatusBars();
    }

    /// <summary>
    /// Validates that all required assignments are properly set.
    /// </summary>
    public override void ValidateAssignments()
    {
        Assert.IsNotNull(xpManager, "ExperienceManager is not assigned.");
        Assert.IsNotNull(movementModel, "PlayerMovementModel is not assigned.");
        Assert.IsNotNull(staminaManager, "StaminaManager is not assigned.");
        Assert.IsNotNull(hpManager, "HealthManager is not assigned.");
        Assert.IsNotNull(foodManager, "FoodManager is not assigned.");
        Assert.IsNotNull(drinkManager, "DrinkManager is not assigned.");
        Assert.IsNotNull(weightManager, "WeightManager is not assigned.");
        Assert.IsNotNull(rollModel, "PlayerRollModel is not assigned.");
        Assert.IsNotNull(dashModel, "PlayerDashModel is not assigned.");
    }

    /// <summary>
    /// Updates various status bars such as stamina, health, and hunger.
    /// </summary>
    private void UpdateStatusBars()
    {
        staminaManager?.UpdateStaminaBar();
        hpManager?.UpdateHpBar();
        foodManager?.UpdateFoodBar();
        drinkManager?.UpdateDrinkBar();
        weightManager?.UpdateWeightBar();
    }
    /// <summary>
    /// Handles cleanup operations upon destruction of the object.
    /// </summary>
    private void OnDestroy()
    {
        // Unsubscribe from the OnSkillPointGained event to prevent memory leaks.
        if (xpManager != null)
        {
            xpManager.OnSkillPointGained -= HandleSkillPointGained;
        }
    }

    /// <summary>
    /// Selects the player's class based on the provided class name.
    /// </summary>
    /// <param name="className">The name of the class to assign to the player.</param>
    public void SelectPlayerClass(string className)
    {
        PlayerClass = className.ToLower() switch
        {
            "warrior" => new WarriorClass(),
            _ => null
        };

        if (PlayerClass == null)
        {
            Debug.LogWarning($"Invalid class name: {className}");
        }
    }

    /// <summary>
    /// Initializes the player's class and updates related attributes.
    /// </summary>
    private void InitializePlayerClass()
    {
        if (PlayerClass == null)
        {
            Debug.LogWarning("PlayerClass is not initialized.");
            return;
        }

        // Set initial values for player attributes based on their class.
        hpManager.MaxHp = PlayerClass.health;
        staminaManager.MaxStamina = PlayerClass.stamina;
        movementModel.Speed = PlayerClass.speed;
        foodManager.MaxFood = PlayerClass.hunger;
        drinkManager.MaxDrink = PlayerClass.thirst;
    }

    /// <summary>
    /// Upgrades a specific player stat if skill points are available.
    /// </summary>
    /// <param name="statType">The type of stat to upgrade.</param>
    public void UpgradeStat(string statType)
    {
        if (xpManager.SkillPoints <= 0)
        {
            Debug.Log("Not enough skill points!");
            return;
        }

        // Check if the stat type has a corresponding upgrade handler.
        if (upgradeStatHandlers.TryGetValue(statType.ToLower(), out var action))
        {
            action();
            xpManager.SkillPoints--;
        }
        else
        {
            Debug.Log("Invalid stat type!");
        }
    }

    /// <summary>
    /// Initializes handlers for upgrading player stats.
    /// </summary>
    private void InitializeUpgradeStatHandlers()
    {
        upgradeStatHandlers = new Dictionary<string, Action>
    {
        { "health", () => hpManager.ModifyMaxHp(hpManager.IncrementFactor) },
        { "stamina", () => staminaManager.ModifyMaxStamina(staminaManager.IncrementFactor) },
        { "speed", () => movementModel.Speed += movementModel.SpeedIncrementFactor },
        { "hunger", () => foodManager.ModifyMaxFood(foodManager.IncrementFactor) },
        { "thirst", () => drinkManager.ModifyMaxDrink(drinkManager.IncrementFactor) },
        { "weight", () => weightManager.ModifyMaxWeight(weightManager.IncrementFactor) }
    };
    }

    /// <summary>
    /// Handles the event when a skill point is gained.
    /// </summary>
    private void HandleSkillPointGained()
    {
        Debug.Log("Skill point gained! Time to level up!");
    }


    public IEnumerator ApplySpeedEffectRoutine(string effectName, float speedAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float elapsedTime = 0f;
        float increment = isProcedural ? speedAmount / (timeBuffEffect / tickCooldown) : speedAmount;

        while (elapsedTime < timeBuffEffect)
        {
            if (isProcedural)
            {
                movementModel.Speed += increment;
                yield return new WaitForSeconds(tickCooldown);
                elapsedTime += tickCooldown;
            }
            else
            {
                movementModel.Speed = speedAmount;
                yield return new WaitForSeconds(timeBuffEffect);
                break;
            }
        }

        movementModel.Speed -= speedAmount;
    }


    public void ModifySpeed(float amount)
    {
        movementModel.Speed += amount;
    }

    /// <summary>
    /// Stops all active effects on the player, optionally filtering by type.
    /// </summary>
    /// <param name="isBuff">Indicates if only buff effects should be stopped.</param>
    public void StopAllEffects(bool isBuff)
    {
        staminaManager.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, isBuff);
        hpManager.StopAllEffectsByType(hpManager.HpEffectRoutines, isBuff);
        foodManager.StopAllEffectsByType(foodManager.FoodEffectRoutines, isBuff);
        drinkManager.StopAllEffectsByType(drinkManager.DrinkEffectRoutines, isBuff);
    }

    /// <summary>
    /// Applies an attack effect to the player.
    /// </summary>
    /// <param name="effect">The attack effect to apply.</param>
    /// <param name="amount">The value associated with the effect.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">The interval for applying procedural effects.</param>
    public override void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (EffectHandlers.TryGetValue(effect.effectType, out var handler))
        {
            handler.Invoke(effect, amount, timeBuffEffect, tickCooldown);
        }
        else
        {
            Debug.LogWarning($"Unhandled effect type: {effect.effectType}");
        }
    }

    /// <summary>
    /// Initializes handlers for different attack effects.
    /// </summary>
    private void InitializeEffectHandlers()
    {
        effectHandlers = new Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>>
    {
        { AttackEffectType.Stamina, CreateHandler(staminaManager.AddStamina, staminaManager.AddStaminaEffect) },
        { AttackEffectType.Hp, CreateHandler(hpManager.AddHp, hpManager.AddHpEffect) },
        { AttackEffectType.Food, CreateHandler(foodManager.AddFood, foodManager.AddFoodEffect) },
        { AttackEffectType.Drink, CreateHandler(drinkManager.AddDrink, drinkManager.AddDrinkEffect) },
        { AttackEffectType.Weight, (effect, amount, time, cooldown) => weightManager.AddWeightEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.HpHealFactor, (effect, amount, time, cooldown) => hpManager.AddHpHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.HpDamageFactor, (effect, amount, time, cooldown) => hpManager.AddHpDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.StaminaHealFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.StaminaDamageFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.StaminaRegeneration, (effect, amount, time, cooldown) => staminaManager.AddStaminaRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
        { AttackEffectType.HpRegeneration, (effect, amount, time, cooldown) => hpManager.AddHpRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) }
    };
    }

    /// <summary>
    /// Handles the application of a specific effect to the player.
    /// </summary>
    /// <param name="directAction">The direct action to apply if no duration is specified.</param>
    /// <param name="effectAction">The effect action to apply over time.</param>
    /// <param name="effect">The effect details.</param>
    /// <param name="amount">The value associated with the effect.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">The interval for applying procedural effects.</param>
    private void HandleEffect(
        Action<float> directAction,
        Action<string, float, float, float, bool, bool> effectAction,
        AttackEffect effect,
        float amount,
        float timeBuffEffect,
        float tickCooldown)
    {
        if (timeBuffEffect == 0)
        {
            directAction?.Invoke(amount);
        }
        else
        {
            effectAction?.Invoke(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
        }
    }

    /// <summary>
    /// Creates a handler for a specific effect type.
    /// </summary>
    /// <param name="directAction">The direct action to apply if no duration is specified.</param>
    /// <param name="effectAction">The effect action to apply over time.</param>
    /// <returns>An action handler for the specified effect type.</returns>
    private Action<AttackEffect, float, float, float> CreateHandler(
        Action<float> directAction,
        Action<string, float, float, float, bool, bool> effectAction)
    {
        return (effect, amount, time, cooldown) => HandleEffect(directAction, effectAction, effect, amount, time, cooldown);
    }

}







//public void RemoveHpEffect(string effectName = null, Coroutine effectRoutine = null)
//    {
//        // Remove effect based on effectName
//        if (effectName != null)
//        {
//            CoroutineInfo effectToRemove = model.StaminaEffectRoutines.Find(e => e.effectName == effectName);

//            if (effectToRemove != null)
//            {
//                StopCoroutine(effectToRemove.coroutine);
//                model.StaminaEffectRoutines.Remove(effectToRemove);
//            }
//        }

//        // Remove effect based on effectRoutine
//        if (effectRoutine != null)
//        {
//            CoroutineInfo effectToRemove = model.StaminaEffectRoutines.Find(e => e.coroutine == effectRoutine);

//            if (effectToRemove != null)
//            {
//                StopCoroutine(effectToRemove.coroutine);
//                model.StaminaEffectRoutines.Remove(effectToRemove);
//            }
//        }
//    }
