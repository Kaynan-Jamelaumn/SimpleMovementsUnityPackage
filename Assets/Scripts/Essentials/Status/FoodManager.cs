using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class FoodManager : StatusManager
{
    private HealthManager healthManager;


    [Header("Hunger-related fields")]
    [SerializeField] private float food;
    [SerializeField] private float maxFood = 100;
    [SerializeField] private float incrementFactor = 15;
    [SerializeField] private float foodConsumption = 1;
    [SerializeField] private float foodTickConsumption = 0.5f;
    [SerializeField] private float hpPenaltyWhileHungry = 1f;
    [SerializeField] private float hpTickPenaltyCooldownWhileHungry = 1f;
    [SerializeField] private float foodFactor;
    [SerializeField] private float foodRemoveFactor;
    [SerializeField] private bool shouldConsumeFood = true;
    [SerializeField] private bool isRegeneratingFood;
    [SerializeField] private bool isConsumingFood;
    [SerializeField] private Image foodImage;
    private Coroutine foodConsumptionRoutine;
    private Coroutine foodPenaltyRoutine;
    //private Coroutine foodFactorRoutine;
    //private Coroutine foodRemoveFactorRoutine;
    private List<CoroutineInfo> foodEffectRoutines = new List<CoroutineInfo>();

    public Coroutine FoodConsumptionRoutine { get => foodConsumptionRoutine; set => foodConsumptionRoutine = value; }
    public Coroutine FoodPenaltyRoutine { get => foodPenaltyRoutine; set => foodPenaltyRoutine = value; }
    public List<CoroutineInfo> FoodEffectRoutines { get => foodEffectRoutines; set => foodEffectRoutines = value; }
    public float Food { get => food; set => food = value; }
    public float MaxFood { get => maxFood; set => maxFood = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float FoodConsumption { get => foodConsumption; set => foodConsumption = value; }
    public float FoodTickConsumption { get => foodTickConsumption; set => foodTickConsumption = value; }
    public float HpPenaltyWhileHungry { get => hpPenaltyWhileHungry; set => hpPenaltyWhileHungry = value; }
    public float HpTickPenaltyCooldownWhileHungry { get => hpTickPenaltyCooldownWhileHungry; set => hpTickPenaltyCooldownWhileHungry = value; }
    public float FoodFactor { get => foodFactor; set => foodFactor = value; }
    public float FoodRemoveFactorProperty { get => foodRemoveFactor; set => foodRemoveFactor = value; }
    public bool IsRegeneratingFood { get => isRegeneratingFood; set => isRegeneratingFood = value; }
    public bool ShouldConsumeFood { get => shouldConsumeFood; set => shouldConsumeFood = value; }
    public bool IsConsumingFood { get => isConsumingFood; set => isConsumingFood = value; }
    public Image FoodImage { get => foodImage; set => foodImage = value; }

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();   
    }
    private void Start()
    {
        Food = MaxFood;
        ValidateAssignments();
    }

    private void Update()
    {
        if (Food <= 0 && FoodPenaltyRoutine == null)
        {
            ApplyHungerPenalty();
        }
        FoodConsumptionRoutine = ShouldConsumeFood && !IsConsumingFood ? StartCoroutine(ConsumeFoodRoutine()) : null;

    }
    private void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HealthManager is not assigned in healthManager.");
    }
    public void UpdateFoodBar()
    {
        UpdateBar(Food, MaxFood, FoodImage);
        Food = Mathf.Clamp(Food, 0, MaxFood);
    }

    public void ApplyHungerPenalty()
    {
        FoodPenaltyRoutine = StartCoroutine(HungerPenalty());
    }

    public void AddFood(float amount)
    {
        Food = Mathf.Min(Food + FoodAddFactor(amount), MaxFood);
    }

    public bool HasEnoughFood(float amount)
    {
        return Food - amount >= 0;
    }
    public void ConsumeFood(float amount)
    {
        if (Food <= 0) return;
        Food -= FoodRemoveFactor(amount);
    }
    public void ModifyMaxFood(float amount)
    {
        MaxFood += amount;
    }
    public void ModifyFoodFactor(float amount)
    {
        FoodFactor += amount;
    }
    public void ModifyFoodRemoveFactor(float amount)
    {
        foodRemoveFactor += amount;
    }
    private float FoodAddFactor(float amount)
    {
        return ApplyFactor(amount, FoodFactor);
    }
    private float FoodRemoveFactor(float amount)
    {
        return ApplyFactor(amount, foodRemoveFactor);
    }
    public void AddFoodEffect(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(FoodEffectRoutines, effectName, foodAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodEffectRoutine);
    }
    public void AddFoodAddFactorEffect(string effectName, float foodFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(FoodEffectRoutines, effectName, foodFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodAddFactorEffectRoutine);
    }
    public void AddFoodRemoveFactorEffect(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(FoodEffectRoutines, effectName, foodRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodRemoveFactorEffectRoutine);
    }

    public void RemoveFoodEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(FoodEffectRoutines, effectName, effectRoutine);
    }

    public void StopAllFoodEffects()
    {
        StopAllEffects(FoodEffectRoutines);
    }
    public void StopAllFoodEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(FoodEffectRoutines, isBuff);
    }
    public IEnumerator ConsumeFoodRoutine()
    {
        if (IsRegeneratingFood)
        {
            IsRegeneratingFood = false;
            yield break;
        }
        IsConsumingFood = true;

        //consume food while it's not below 0
        while (Food >= 0)
        {
            Food -= FoodConsumption;

            if (!IsConsumingFood)
            {
                IsConsumingFood = true;
                break;
            }
            yield return new WaitForSeconds(FoodTickConsumption);
        }
    }

    public IEnumerator HungerPenalty()
    {
        while (Food <= 0)
        {
            healthManager.ConsumeHP(HpPenaltyWhileHungry, true);
            yield return new WaitForSeconds(HpTickPenaltyCooldownWhileHungry);
        }

        FoodPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
    }

    public IEnumerator ApplyFoodEffectRoutine(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = foodAmount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            float newAmount = amount;
            if (newAmount > 0) newAmount = FoodAddFactor(newAmount);
            else newAmount = FoodRemoveFactor(newAmount);
            Food += newAmount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        Food = Mathf.Min(Food, MaxFood);
    }
    public IEnumerator ApplyFoodAddFactorEffectRoutine(string effectName, float foodAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = foodAddFactorAmount;
        float foodAddFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                FoodFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                FoodFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        FoodFactor -= foodAddFactorOriginalAmount;
    }
    public IEnumerator ApplyFoodRemoveFactorEffectRoutine(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = foodRemoveFactorAmount;
        float foodRemoveFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time <  startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                foodRemoveFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                foodRemoveFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        foodRemoveFactor -= foodRemoveFactorOriginalAmount;
    }
}
