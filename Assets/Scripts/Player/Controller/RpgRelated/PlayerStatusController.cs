using System.Collections;
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
        xpManager = GetComponentOrLogError(ref xpManager, "ExperienceManager");
        movementModel = GetComponentOrLogError(ref movementModel, "PlayerMovementModel");
        staminaManager = GetComponentOrLogError(ref staminaManager, "StaminaManager");
        hpManager = GetComponentOrLogError(ref hpManager, "HealthManager");
        foodManager = GetComponentOrLogError(ref foodManager, "FoodManager");
        drinkManager = GetComponentOrLogError(ref drinkManager, "DrinkManager");
        weightManager = GetComponentOrLogError(ref weightManager, "WeightManager");
        rollModel = GetComponentOrLogError(ref rollModel, "PlayerRollModel");
        dashModel = GetComponentOrLogError(ref dashModel, "PlayerDashModel");
    }

    protected override void Start()
    {
        base.Start();
        xpManager.OnSkillPointGained += HandleSkillPointGained;
        InitializePlayerClass();
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
        switch (effect.effectType)
        {
            case AttackEffectType.Stamina:
                if (timeBuffEffect == 0) StaminaManager.AddStamina(amount);
                else StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Hp:
                if (timeBuffEffect == 0) HpManager.AddHp(amount);
                else HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Food:
                if (timeBuffEffect == 0) FoodManager.AddFood(amount);
                else FoodManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Drink:
                if (timeBuffEffect == 0) DrinkManager.AddDrink(amount);
                else DrinkManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Weight:
                if (timeBuffEffect == 0) WeightManager.AddWeight(amount);
                else WeightManager.AddWeightEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                HpManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                HpManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaHealFactor:
                StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaDamageFactor:
                StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaRegeneration:
                StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                HpManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
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
