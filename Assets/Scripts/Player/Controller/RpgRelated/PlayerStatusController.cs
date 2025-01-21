using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerStatusController : BaseStatusController
{
    [Header("Models")]
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private HealthManager hpManager;
    [SerializeField] private FoodManager foodManager;
    [SerializeField] private DrinkManager drinkManager;
    [SerializeField] private WeightManager weightManager;
    [SerializeField] private ExperienceManager xpManager;

    [Header("Controllers")]
    [SerializeField] private PlayerRollModel rollModel;
    [SerializeField] private PlayerDashModel dashModel;
    [SerializeField] private PlayerRollController rollController;
    [SerializeField] private PlayerDashController dashController;


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

    protected override void Start()
    {
        base.Start();
        xpManager.OnSkillPointGained += HandleSkillPointGained;
        InitializePlayerClass();
        InitializeEffectHandlers();
    }


    private void Update()
    {
        UpdateStatusBars();
    }

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

    private void UpdateStatusBars()
    {
        staminaManager?.UpdateStaminaBar();
        hpManager?.UpdateHpBar();
        foodManager?.UpdateFoodBar();
        drinkManager?.UpdateDrinkBar();
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

        hpManager.MaxHp = PlayerClass.health;
        staminaManager.MaxStamina = PlayerClass.stamina;
        movementModel.Speed = PlayerClass.speed;
        foodManager.MaxFood = PlayerClass.hunger;
        drinkManager.MaxDrink = PlayerClass.thirst;
    }

    public void UpgradeStat(string statType)
    {
        if (xpManager.SkillPoints <= 0)
        {
            Debug.Log("Not enough skill points!");
            return;
        }

        switch (statType.ToLower())
        {
            case "health":
                hpManager.ModifyMaxHp(hpManager.IncrementFactor);
                break;
            case "stamina":
                staminaManager.ModifyMaxStamina(staminaManager.IncrementFactor);
                break;
            case "speed":
                movementModel.Speed += movementModel.SpeedIncrementFactor;
                break;
            case "hunger":
                foodManager.ModifyMaxFood(foodManager.IncrementFactor);
                break;
            case "thirst":
                drinkManager.ModifyMaxDrink(drinkManager.IncrementFactor);
                break;
            case "weight":
                weightManager.ModifyMaxWeight(weightManager.IncrementFactor);
                break;
            default:
                Debug.Log("Invalid stat type!");
                return;
        }

        xpManager.SkillPoints--;
    }

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

    public void StopAllEffects(bool isBuff)
    {
        staminaManager.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, isBuff);
        hpManager.StopAllEffectsByType(hpManager.HpEffectRoutines, isBuff);
        foodManager.StopAllEffectsByType(foodManager.FoodEffectRoutines, isBuff);
        drinkManager.StopAllEffectsByType(drinkManager.DrinkEffectRoutines, isBuff);
    }

    public void ModifySpeed(float amount)
    {
        movementModel.Speed += amount;
    }



    public override void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (EffectHandlers.TryGetValue(effect.effectType, out Action<AttackEffect, float, float, float> handler))
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
