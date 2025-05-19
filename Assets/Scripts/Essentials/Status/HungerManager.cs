using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class HungerManager : StatusManager
{
    private HealthManager healthManager;


    [Header("Hunger-related fields")]
    [SerializeField] private float starvingHpPenalty = 1f;
    [SerializeField] private float starvingHpPenaltyTickRate = 1f;
    private Coroutine foodConsumptionRoutine;
    private Coroutine foodPenaltyRoutine;

    private List<CoroutineInfo> foodEffectRoutines = new List<CoroutineInfo>();
    public Coroutine FoodConsumptionRoutine { get => foodConsumptionRoutine; set => foodConsumptionRoutine = value; }
    public Coroutine FoodPenaltyRoutine { get => foodPenaltyRoutine; set => foodPenaltyRoutine = value; }
    public List<CoroutineInfo> FoodEffectRoutines { get => foodEffectRoutines; set => foodEffectRoutines = value; }

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();   
    }
    private void Start()
    {
        currentValue = maxValue;
        ValidateAssignments();
    }

    private void Update()
    {
        if (currentValue <= 0 && FoodPenaltyRoutine == null)
        {
            ApplyHungerPenalty();
        }
        FoodConsumptionRoutine = shouldConsume && !isConsuming ? StartCoroutine(ConsumeFoodRoutine()) : null;

    }
    private void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HealthManager is not assigned in healthManager.");
    }
    public void UpdateFoodBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public void ApplyHungerPenalty()
    {
        FoodPenaltyRoutine = StartCoroutine(HungerPenalty());
    }
    public void ConsumeFood(float amount)
    {
        if (currentValue <= 0) return;
        currentValue -= RemoveFactor(amount);
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);
    public void AddFoodEffect(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodEffectRoutine);
    public void AddFoodAddFactorEffect(string effectName, float foodFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodAddFactorEffectRoutine);
    public void AddFoodRemoveFactorEffect(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodRemoveFactorEffectRoutine);

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
        if (isRegenerating)
        {
            isRegenerating = false;
            yield break;
        }
        isRegenerating = true;

        //consume food while it's not below 0
        while (currentValue >= 0)
        {
            currentValue -= decrementValue;

            if (!isRegenerating)
            {
                isRegenerating = true;
                break;
            }
            yield return new WaitForSeconds(tickRate);
        }
    }

    public IEnumerator HungerPenalty()
    {
        while (currentValue <= 0)
        {
            healthManager.ConsumeHP(starvingHpPenalty, true);
            yield return new WaitForSeconds(starvingHpPenalty);
        }

        FoodPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
    }
    public IEnumerator ApplyFoodEffectRoutine(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            foodAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick =>
            {
                float newAmount = perTick > 0 ? AddFactor(perTick) : RemoveFactor(perTick);
                currentValue += newAmount;
            },
            total =>
            {
                float newAmount = total > 0 ? AddFactor(total) : RemoveFactor(total);
                currentValue += newAmount;
            },
            _ => currentValue = Mathf.Min(currentValue, maxValue)
        );
    }

    public IEnumerator ApplyFoodAddFactorEffectRoutine(string effectName, float foodAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            foodAddFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor = total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplyFoodRemoveFactorEffectRoutine(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            foodRemoveFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => decrementFactor += perTick,
            total => decrementFactor = total,
            original => decrementFactor -= original
        );
    }
}
