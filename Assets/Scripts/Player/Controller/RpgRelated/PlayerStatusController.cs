using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
public class PlayerStatusController : MonoBehaviour
{
    // Models
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private HealthManager hpManager;
    [SerializeField] private FoodManager foodManager;
    [SerializeField] private DrinkManager drinkManager;
    [SerializeField] private WeightManager weightManager;
    [SerializeField] private ExperienceManager xpManager;

    [SerializeField] private PlayerRollModel rollModel;
    [SerializeField] private PlayerDashModel dashModel;

    [SerializeField] private PlayerRollController rollController;
    [SerializeField] private PlayerDashController dashController;

    public ExperienceManager XPManager { get { return xpManager; } }
    public BasePlayerClass PlayerClass { get; private set; }

    public PlayerMovementModel MovementModel
    {
        get { return movementModel; }
    }

    public StaminaManager StaminaManager
    {
        get { return staminaManager; }
    }

    public HealthManager HpManager
    {
        get { return hpManager; }
    }

    public FoodManager FoodManager
    {
        get { return foodManager; }
    }

    public DrinkManager DrinkManager
    {
        get { return drinkManager; }
    }

    public WeightManager WeightManager
    {
        get { return weightManager; }
    }

    public PlayerRollModel RollModel
    {
        get { return rollModel; }
    }

    public PlayerDashModel Dashmodel
    {
        get { return dashModel; }
    }

    public PlayerRollController RollController
    {
        get { return rollController; }
    }

    public PlayerDashController DashController
    {
        get { return dashController; }
    }

    private void Awake()
    {
        // Assign models
        if(!xpManager) xpManager = GetComponent<ExperienceManager>();
        if (!movementModel) movementModel = GetComponent<PlayerMovementModel>();
        if (!staminaManager) staminaManager = GetComponent<StaminaManager>();
        if (!hpManager) hpManager = GetComponent<HealthManager>();
        if (!foodManager) foodManager = GetComponent<FoodManager>();
        if (!drinkManager) drinkManager = GetComponent<DrinkManager>();
        if (!weightManager) weightManager = GetComponent<WeightManager>();
        if (!rollModel) rollModel = GetComponent<PlayerRollModel>();
        if (!dashModel) dashModel = GetComponent<PlayerDashModel>();

    }
    private void Start()
    {
        ValidateAsignments();
        // Initialize player status variables
        xpManager.OnSkillPointGained += HandleSkillPointGained;
        staminaManager.IsRegeneratingStamina = false;
        hpManager.IsRegeneratingHp = false;
        InitializePlayerClass();


    }

    private void Update()
    {
        UpdateStatusBars();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(xpManager, "ExperienceManager is not assigned in xpManager.");
        Assert.IsNotNull(movementModel, "PlayerMovementModel is not assigned in movementModel.");
        Assert.IsNotNull(staminaManager, "StaminaManager is not assigned in staminaManager.");
        Assert.IsNotNull(hpManager, "HealthManager is not assigned in hpManager.");
        Assert.IsNotNull(foodManager, "FoodManager is not assigned in  foodManager.");
        Assert.IsNotNull(drinkManager, "DrinkManager is not assigned in  drinkManager.");
        Assert.IsNotNull(weightManager, "WeightManager is not assigned in  weightManager.");
        Assert.IsNotNull(rollModel, "PlayerRollModel is not assigned in  rollModel.");
        Assert.IsNotNull(dashModel, "PlayerDashModel is not assigned in  dashModel.");
    }
    private void UpdateStatusBars()
    {
        if (staminaManager.StaminaImage) staminaManager.UpdateStaminaBar();
        if (hpManager.HpImage) hpManager.UpdateHpBar();
        if (foodManager.FoodImage) foodManager.UpdateFoodBar();
        if (drinkManager.DrinkImage) drinkManager.UpdateDrinkBar();
        if (weightManager.WeightImage) weightManager.UpdateWeightBar();
    }
    private void OnDestroy()
    {
        // Unsubscribe when the object is destroyed to prevent memory leaks
        if (xpManager != null)
        {
            xpManager.OnSkillPointGained -= HandleSkillPointGained;
        }
    }
    public void SelectPlayerClass(string className)
    {
        BasePlayerClass selectedClass = null;

        switch (className.ToLower())
        {
            case "warrior":
                selectedClass = new WarriorClass();
                break;
            //case "mage":
            //    selectedClass = new MageClass();
            //    break;
                // Add more cases for additional classes
        }

        if (selectedClass != null)
        {
            PlayerClass = selectedClass;
        }
    }
    private void InitializePlayerClass( )
    {
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

        xpManager.SkillPoints--; // Use up a skill point
    }

    // Method to handle the skill point gained event
    private void HandleSkillPointGained()
    {
        Debug.Log("Skill point gained! Time to level up!");
    }

    public void ModifySpeed(float amount)
    {
        movementModel.Speed += amount;
    }

    
    public IEnumerator ApplySpeedEffectRoutine(string effectName, float speedAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = speedAmount;
        float speedOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



        while (Time.time < Time.time + timeBuffEffect)
        {
            if (isProcedural)
            {
               movementModel.Speed += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                movementModel.Speed = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        movementModel.Speed -= speedOriginalAmount;
    }

    public void StopAllDebuffEffects()
    {
        staminaManager.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, false);
        hpManager.StopAllEffectsByType(hpManager.HpEffectRoutines, false);
        foodManager.StopAllEffectsByType(foodManager.FoodEffectRoutines, false);
        drinkManager.StopAllEffectsByType(drinkManager.DrinkEffectRoutines, false);
    }
    public void StopAllBuffEffects()
    {
        staminaManager.StopAllEffectsByType(staminaManager.StaminaEffectRoutines, true);
        hpManager.StopAllEffectsByType(hpManager.HpEffectRoutines, true);
        foodManager.StopAllEffectsByType(foodManager.FoodEffectRoutines, true);
        drinkManager.StopAllEffectsByType(drinkManager.DrinkEffectRoutines, true);
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
