using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerStatusController : BaseStatusController
{
    [Header("Core References")]
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private PlayerRollModel rollModel;
    [SerializeField] private PlayerDashModel dashModel;

    [Header("Status Managers")]
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private HealthManager hpManager;
    [SerializeField] private HungerManager hungerManager;
    [SerializeField] private ThirstManager thirstManager;
    [SerializeField] private WeightManager weightManager;
    [SerializeField] private SpeedManager speedManager;
    [SerializeField] private SleepManager sleepManager;
    [SerializeField] private SanityManager sanityManager;
    [SerializeField] private ManaManager manaManager;
    [SerializeField] private BodyHeatManager bodyHeatManager;
    [SerializeField] private OxygenManager oxygenManager;

    [Header("System Managers")]
    [SerializeField] private ExperienceManager xpManager;
    [SerializeField] private TraitManager traitManager;

    [Header("Player Class")]
    [SerializeField] private PlayerClass currentPlayerClass;

    [Header("Starting Setup")]
    [SerializeField] private bool autoApplyClassStats = true;
    [SerializeField] private bool autoApplyStartingTraits = true;

    // Effect handlers for attack effects
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

    // Events
    public event Action<PlayerClass> OnPlayerClassChanged;
    public event Action OnStatsInitialized;

    // Properties
    public PlayerClass CurrentPlayerClass => currentPlayerClass;
    public ExperienceManager XPManager => xpManager;
    public TraitManager TraitManager => traitManager;
    public PlayerMovementModel MovementModel => movementModel;
    public StaminaManager StaminaManager => staminaManager;
    public HealthManager HpManager => hpManager;
    public HungerManager HungerManager => hungerManager;
    public ThirstManager ThirstManager => thirstManager;
    public WeightManager WeightManager => weightManager;
    public SpeedManager SpeedManager => speedManager;
    public SleepManager SleepManager => sleepManager;
    public SanityManager SanityManager => sanityManager;
    public ManaManager ManaManager => manaManager;
    public BodyHeatManager BodyHeatManager => bodyHeatManager;
    public OxygenManager OxygenManager => oxygenManager;
    public PlayerRollModel RollModel => rollModel;
    public PlayerDashModel DashModel => dashModel;

    private void Awake()
    {
        // Cache components
        CacheComponents();

        // Initialize trait manager if not assigned
        if (traitManager == null)
            traitManager = GetComponent<TraitManager>();
    }

    private void Start()
    {
        // Subscribe to XP manager events
        if (xpManager != null)
        {
            xpManager.OnLevelUp += HandleLevelUp;
            xpManager.OnStatUpgraded += HandleStatUpgraded;
        }

        // Apply player class if assigned
        if (currentPlayerClass != null)
        {
            ApplyPlayerClass(currentPlayerClass);
        }

        InitializeEffectHandlers();
        OnStatsInitialized?.Invoke();
    }

    private void Update()
    {
        UpdateStatusBars();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (xpManager != null)
        {
            xpManager.OnLevelUp -= HandleLevelUp;
            xpManager.OnStatUpgraded -= HandleStatUpgraded;
        }
    }

    // Cache all required components
    private void CacheComponents()
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
        sleepManager = this.CheckComponent(sleepManager, nameof(sleepManager));
        sanityManager = this.CheckComponent(sanityManager, nameof(sanityManager));
        manaManager = this.CheckComponent(manaManager, nameof(manaManager));
        bodyHeatManager = this.CheckComponent(bodyHeatManager, nameof(bodyHeatManager));
        oxygenManager = this.CheckComponent(oxygenManager, nameof(oxygenManager));
    }

    // Update all status bars - only update if the UI is active
    private void UpdateStatusBars()
    {
        // Always update core status bars
        staminaManager?.UpdateStaminaBar();
        hpManager?.UpdateHpBar();
        hungerManager?.UpdateFoodBar();
        thirstManager?.UpdateDrinkBar();
        weightManager?.UpdateWeightBar();
        manaManager?.UpdateManaBar();

        // Conditional status bars update themselves and manage their own visibility
        sleepManager?.UpdateSleepBar();
        bodyHeatManager?.UpdateBodyHeatBar();
        oxygenManager?.UpdateOxygenBar();

        // Sanity doesn't have a UI bar by design, but we still call update for internal state management
        sanityManager?.UpdateSanityBar();
    }

    // Set player class with reference
    public void SetPlayerClass(PlayerClass playerClass)
    {
        if (playerClass == null)
        {
            Debug.LogWarning("Attempted to set null player class!");
            return;
        }

        currentPlayerClass = playerClass;

        if (autoApplyClassStats)
        {
            ApplyPlayerClass(playerClass);
        }

        // Set class in XP manager
        if (xpManager != null)
        {
            xpManager.SetPlayerClass(playerClass);
        }

        OnPlayerClassChanged?.Invoke(playerClass);
        Debug.Log($"Player class set to: {playerClass.GetClassName()}");
    }

    // Apply player class stats and traits
    private void ApplyPlayerClass(PlayerClass playerClass)
    {
        if (playerClass == null) return;

        // Apply base stats
        hpManager.MaxValue = playerClass.health;
        staminaManager.MaxValue = playerClass.stamina;
        manaManager.MaxValue = playerClass.mana;
        speedManager.BaseSpeed = playerClass.speed;
        hungerManager.MaxValue = playerClass.hunger;
        thirstManager.MaxValue = playerClass.thirst;
        weightManager.MaxValue = playerClass.weight;
        sleepManager.MaxValue = playerClass.sleep;
        sanityManager.MaxValue = playerClass.sanity;
        bodyHeatManager.MaxValue = playerClass.bodyHeat;
        oxygenManager.MaxValue = playerClass.oxygen;

        // Apply starting traits if enabled
        if (autoApplyStartingTraits && traitManager != null)
        {
            foreach (var trait in playerClass.GetStartingTraits())
            {
                if (trait != null)
                {
                    traitManager.AddTrait(trait, true); // Skip cost for starting traits
                }
            }

            // Add trait points from class
            traitManager.AddTraitPoints(playerClass.traitPoints);
        }

        Debug.Log($"Applied {playerClass.GetClassName()} class stats and traits");
    }

    // Handle level up event
    private void HandleLevelUp(int newLevel)
    {
        Debug.Log($"Player reached level {newLevel}!");
        // You can add level-up effects here (visual effects, sounds, etc.)
    }

    // Handle stat upgrade event
    private void HandleStatUpgraded(string statName)
    {
        Debug.Log($"Player upgraded {statName}!");
        // You can add upgrade effects here
    }

    // Manually upgrade a stat using XP system
    public bool UpgradeStat(string statType)
    {
        if (xpManager == null)
        {
            Debug.LogError("No XP Manager found!");
            return false;
        }

        return xpManager.UpgradeStat(statType);
    }

    // Get available stats for upgrading
    public List<string> GetUpgradeableStats()
    {
        if (xpManager == null) return new List<string>();
        return xpManager.GetUpgradeableStats();
    }

    // Get stat upgrade preview
    public string GetStatUpgradePreview(string statType)
    {
        if (xpManager == null) return "N/A";
        return xpManager.GetStatUpgradePreview(statType);
    }

    // Stop all effects by type
    public void StopAllEffects(bool isBuff)
    {
        staminaManager?.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, isBuff);
        hpManager?.StopAllEffectsByType(hpManager.HpEffectRoutines, isBuff);
        hungerManager?.StopAllEffectsByType(hungerManager.FoodEffectRoutines, isBuff);
        thirstManager?.StopAllEffectsByType(thirstManager.DrinkEffectRoutines, isBuff);
        speedManager?.StopAllEffectsByType(speedManager.SpeedEffectRoutines, isBuff);
        sleepManager?.StopAllEffectsByType(sleepManager.SleepEffectRoutines, isBuff);
        sanityManager?.StopAllEffectsByType(sanityManager.SanityEffectRoutines, isBuff);
        manaManager?.StopAllEffectsByType(manaManager.ManaEffectRoutines, isBuff);
        bodyHeatManager?.StopAllEffectsByType(bodyHeatManager.BodyHeatEffectRoutines, isBuff);
        oxygenManager?.StopAllEffectsByType(oxygenManager.OxygenEffectRoutines, isBuff);
    }

    // Apply attack effects
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

    // Initialize effect handlers
    private void InitializeEffectHandlers()
    {
        effectHandlers = new Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>>
        {
            { AttackEffectType.Stamina, CreateHandler(staminaManager.AddCurrentValue, staminaManager.AddStaminaEffect) },
            { AttackEffectType.Hp, CreateHandler(hpManager.AddCurrentValue, hpManager.AddHpEffect) },
            { AttackEffectType.Food, CreateHandler(hungerManager.AddCurrentValue, hungerManager.AddFoodEffect) },
            { AttackEffectType.Drink, CreateHandler(thirstManager.AddCurrentValue, thirstManager.AddDrinkEffect) },
            { AttackEffectType.Weight, (effect, amount, time, cooldown) => weightManager.AddWeightEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Speed, CreateSpeedHandler(speedManager.ModifySpeed, speedManager.AddSpeedEffect) },
            { AttackEffectType.HpHealFactor, (effect, amount, time, cooldown) => hpManager.AddHpHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.HpDamageFactor, (effect, amount, time, cooldown) => hpManager.AddHpDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaHealFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaDamageFactor, (effect, amount, time, cooldown) => staminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.StaminaRegeneration, (effect, amount, time, cooldown) => staminaManager.AddStaminaRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.HpRegeneration, (effect, amount, time, cooldown) => hpManager.AddHpRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.SpeedFactor, (effect, amount, time, cooldown) => speedManager.AddSpeedFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.SpeedMultiplier, (effect, amount, time, cooldown) => speedManager.AddSpeedMultiplierEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Sleep, CreateHandler(sleepManager.AddCurrentValue, sleepManager.AddSleepEffect) },
            { AttackEffectType.SleepFactor, (effect, amount, time, cooldown) => sleepManager.AddSleepEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Sanity, CreateHandler(sanityManager.AddCurrentValue, sanityManager.AddSanityEffect) },
            { AttackEffectType.SanityHealFactor, (effect, amount, time, cooldown) => sanityManager.AddSanityHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.SanityDamageFactor, (effect, amount, time, cooldown) => sanityManager.AddSanityDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Mana, CreateHandler(manaManager.AddCurrentValue, manaManager.AddManaEffect) },
            { AttackEffectType.ManaRegeneration, (effect, amount, time, cooldown) => manaManager.AddManaRegenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.ManaHealFactor, (effect, amount, time, cooldown) => manaManager.AddManaHealFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.ManaDamageFactor, (effect, amount, time, cooldown) => manaManager.AddManaDamageFactorEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.BodyHeat, CreateHandler(bodyHeatManager.ModifyBodyHeat, bodyHeatManager.AddBodyHeatEffect) },
            { AttackEffectType.BodyHeatFactor, (effect, amount, time, cooldown) => bodyHeatManager.AddBodyHeatEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },
            { AttackEffectType.Oxygen, CreateHandler((amount) => oxygenManager.AddCurrentValue(amount), oxygenManager.AddOxygenEffect) },
            { AttackEffectType.OxygenFactor, (effect, amount, time, cooldown) => oxygenManager.AddOxygenEffect(effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) }
        };
    }

    // Helper methods for creating effect handlers
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

    // Environmental and state management methods
    public void StartSleeping() => sleepManager?.StartSleeping();
    public void StopSleeping() => sleepManager?.StopSleeping();
    public void SetEnvironmentalTemperature(float temperature) => bodyHeatManager?.SetEnvironmentalTemperature(temperature);
    public void SetUnderwater(bool underwater) => oxygenManager?.SetUnderwater(underwater);
    public void SetHighAltitude(bool highAltitude) => oxygenManager?.SetHighAltitude(highAltitude);
    public void SetPoorVentilation(bool poorVentilation) => oxygenManager?.SetPoorVentilation(poorVentilation);
    public void SetOxygenTank(bool hasOxygen, float tankAmount = 0f) => oxygenManager?.SetOxygenTank(hasOxygen, tankAmount);

    // Status level getters
    public SleepManager.SleepinessLevel GetSleepinessLevel() => sleepManager?.CurrentSleepinessLevel ?? SleepManager.SleepinessLevel.Rested;
    public SanityManager.SanityLevel GetSanityLevel() => sanityManager?.CurrentSanityLevel ?? SanityManager.SanityLevel.Stable;
    public BodyHeatManager.TemperatureLevel GetTemperatureLevel() => bodyHeatManager?.CurrentTemperatureLevel ?? BodyHeatManager.TemperatureLevel.Normal;
    public OxygenManager.OxygenEnvironment GetOxygenEnvironment() => oxygenManager?.CurrentEnvironment ?? OxygenManager.OxygenEnvironment.Normal;

    // Status monitoring methods - useful for UI and other systems
    public bool IsPlayerSleepy() => sleepManager?.CurrentSleepinessLevel != SleepManager.SleepinessLevel.Rested;
    public bool IsPlayerTemperatureAffected() => bodyHeatManager?.CurrentTemperatureLevel != BodyHeatManager.TemperatureLevel.Normal;
    public bool IsPlayerConsumingOxygen() => oxygenManager?.IsInRestrictedEnvironment ?? false;
    public bool IsPlayerSanityAffected() => sanityManager?.CurrentSanityLevel != SanityManager.SanityLevel.Stable;

    // Force update UI visibility - useful when you want to manually show/hide conditional UIs
    public void ForceUpdateConditionalUIs()
    {
        sleepManager?.UpdateSleepBar();
        bodyHeatManager?.UpdateBodyHeatBar();
        oxygenManager?.UpdateOxygenBar();
    }
}