using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

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
    [SerializeField] private HungerManager hungerManager;

    [Tooltip("Tracks the player's drink consumption and thirst levels.")]
    [SerializeField] private ThirstManager thirstManager;

    [Tooltip("Tracks and manages the player's carried weight.")]
    [SerializeField] private WeightManager weightManager;

    [Tooltip("Manages player experience and skill points.")]
    [SerializeField] private ExperienceManager xpManager;

    [Tooltip("Manages the player's speed.")]
    [SerializeField] private SpeedManager speedManager;

    [Header("Controllers")]
    [Tooltip("Handles player rolling mechanics.")]
    [SerializeField] private PlayerRollModel rollModel;

    [Tooltip("Handles player dashing mechanics.")]
    [SerializeField] private PlayerDashModel dashModel;

    [Tooltip("Controller for player rolling actions.")]
    //[SerializeField] private PlayerRollController rollController;

    //[Tooltip("Controller for player dashing actions.")]
    //[SerializeField] private PlayerDashController dashController;

    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> effectHandlers;

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

    private Dictionary<string, Action> upgradeStatHandlers;

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
    public HungerManager HungerManager => hungerManager;
    public ThirstManager ThirstManager => thirstManager;
    public WeightManager WeightManager => weightManager;
    public SpeedManager SpeedManager => speedManager;
    public PlayerRollModel RollModel => rollModel;
    public PlayerDashModel DashModel => dashModel;
    //public PlayerRollController RollController => rollController;
    //public PlayerDashController DashController => dashController;

    protected void Awake()
    {
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
        hpManager = this.CheckComponent(hpManager, nameof(hpManager));
        hungerManager = this.CheckComponent(hungerManager, nameof(hungerManager));
        thirstManager = this.CheckComponent(thirstManager, nameof(thirstManager));
        weightManager = this.CheckComponent(weightManager, nameof(weightManager));
        xpManager = this.CheckComponent(xpManager, nameof(xpManager));
        speedManager = this.CheckComponent(speedManager, nameof(speedManager));
        rollModel = this.CheckComponent(rollModel, nameof(rollModel));
        dashModel = this.CheckComponent(dashModel, nameof(dashModel));
        //rollController = this.CheckComponent(rollController, nameof(rollController));
        //dashController = this.CheckComponent(dashController, nameof(dashController));
    }

    void Start()
    {
        xpManager.OnSkillPointGained += HandleSkillPointGained;
        InitializePlayerClass();
        InitializeEffectHandlers();
    }
    
    private void Update()
    {
        UpdateStatusBars();
    }

    private void UpdateStatusBars()
    {
        staminaManager?.UpdateStaminaBar();
        hpManager?.UpdateHpBar();
        hungerManager?.UpdateFoodBar();
        thirstManager?.UpdateDrinkBar();
        weightManager?.UpdateWeightBar();
    }

    private void OnDestroy()
    {
        if (xpManager != null)
        {
            xpManager.OnSkillPointGained -= HandleSkillPointGained;
        }
    }

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

    private void InitializePlayerClass()
    {
        if (PlayerClass == null)
        {
            Debug.LogWarning("PlayerClass is not initialized.");
            return;
        }

        hpManager.MaxValue = PlayerClass.health;
        staminaManager.MaxValue = PlayerClass.stamina;
        speedManager.BaseSpeed = PlayerClass.speed; // Now using SpeedManager
        hungerManager.MaxValue = PlayerClass.hunger;
        thirstManager.MaxValue = PlayerClass.thirst;
    }

    public void UpgradeStat(string statType)
    {
        if (xpManager.SkillPoints <= 0)
        {
            Debug.Log("Not enough skill points!");
            return;
        }

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

    private void InitializeUpgradeStatHandlers()
    {
        upgradeStatHandlers = new Dictionary<string, Action>
        {
            { "health", () => hpManager.ModifyMaxValue(hpManager.StatusIncrementValue) },
            { "stamina", () => staminaManager.ModifyMaxValue(staminaManager.StatusIncrementValue) },
            { "speed", () => speedManager.ModifyBaseSpeed(speedManager.StatusIncrementValue) }, // Now using SpeedManager
            { "hunger", () => hungerManager.ModifyMaxValue(hungerManager.StatusIncrementValue) },
            { "thirst", () => thirstManager.ModifyMaxValue(thirstManager.StatusIncrementValue) },
            { "weight", () => weightManager.ModifyMaxValue(weightManager.StatusIncrementValue) }
        };
    }

    private void HandleSkillPointGained()
    {
        Debug.Log("Skill point gained! Time to level up!");
    }

    // Removed ApplySpeedEffectRoutine as it's now handled by SpeedManager
    public void ModifySpeed(float amount)
    {
        speedManager.ModifySpeed(amount);
    }

    public void StopAllEffects(bool isBuff)
    {
        staminaManager.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, isBuff);
        hpManager.StopAllEffectsByType(hpManager.HpEffectRoutines, isBuff);
        hungerManager.StopAllEffectsByType(hungerManager.FoodEffectRoutines, isBuff);
        thirstManager.StopAllEffectsByType(thirstManager.DrinkEffectRoutines, isBuff);
        speedManager.StopAllEffectsByType(speedManager.SpeedEffectRoutines, isBuff);
    }

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

    private void InitializeEffectHandlers()
    {
        effectHandlers = new Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>>
        {
            { AttackEffectType.Stamina, CreateHandler(staminaManager.AddCurrentValue, staminaManager.AddStaminaEffect) },
            { AttackEffectType.Hp, CreateHandler(hpManager.AddCurrentValue, hpManager.AddHpEffect) },
            { AttackEffectType.Food, CreateHandler(hungerManager.AddCurrentValue, hungerManager.AddFoodEffect) },
            { AttackEffectType.Drink, CreateHandler(thirstManager.AddCurrentValue, thirstManager.AddDrinkEffect) },
            { AttackEffectType.Weight, (effect, amount, time, cooldown) => weightManager.AddWeightEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Speed, CreateSpeedHandler(speedManager.ModifySpeed, speedManager.AddSpeedEffect) }, // Custom handler for speed
            { AttackEffectType.HpHealFactor, (effect, amount, time, cooldown) => hpManager.AddHpHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.HpDamageFactor, (effect, amount, time, cooldown) => hpManager.AddHpDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaHealFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaDamageFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaRegeneration, (effect, amount, time, cooldown) => staminaManager.AddStaminaRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.HpRegeneration, (effect, amount, time, cooldown) => hpManager.AddHpRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.SpeedFactor, (effect, amount, time, cooldown) => speedManager.AddSpeedFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) }, // Added Speed factor effect
            { AttackEffectType.SpeedMultiplier, (effect, amount, time, cooldown) => speedManager.AddSpeedMultiplierEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) } // Added Speed multiplier effect
        };
    }

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

    private Action<AttackEffect, float, float, float> CreateHandler(
        Action<float> directAction,
        Action<string, float, float, float, bool, bool> effectAction)
    {
        return (effect, amount, time, cooldown) => HandleEffect(directAction, effectAction, effect, amount, time, cooldown);
    }


    private Action<AttackEffect, float, float, float> CreateSpeedHandler(
    Action<float> directAction,
    Action<string, float, float, float, bool, bool> effectAction)
    {
        return (effect, amount, time, cooldown) => HandleSpeedEffect(directAction, effectAction, effect, amount, time, cooldown);
    }

    private void HandleSpeedEffect(
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
}