using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class ThirstManager : StatusManager
{
    private HealthManager healthManager;
    [Header("Thirst-related fields")]
    [SerializeField] private float drink;
    [SerializeField] private float dehydratedHpPenalty = 1f;
    [SerializeField] private float dehydratedHpPenaltyTickRate = 1f;
    private Coroutine drinkConsumptionRoutine;
    private Coroutine drinkPenaltyRoutine;
    //private Coroutine drinkFactorRoutine;
    //private Coroutine drinkRemoveFactorRoutine;
    private List<CoroutineInfo> drinkEffectRoutines = new List<CoroutineInfo>();
    public Coroutine DrinkConsumptionRoutine { get => drinkConsumptionRoutine; set => drinkConsumptionRoutine = value; }
    public Coroutine DrinkPenaltyRoutine { get => drinkPenaltyRoutine; set => drinkPenaltyRoutine = value; }
    public List<CoroutineInfo> DrinkEffectRoutines { get => drinkEffectRoutines; set => drinkEffectRoutines = value; }

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        currentValue = maxValue;
    }

    private void Update()
    {
        if (currentValue <= 0 && DrinkPenaltyRoutine == null)
        {
            ApplyThirstPenalty();
        }
        DrinkConsumptionRoutine = shouldConsume && !isConsuming ? StartCoroutine(ConsumeDrinkRoutine()) : null;

    }

    public void UpdateDrinkBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }
    public void ApplyThirstPenalty() => DrinkPenaltyRoutine = StartCoroutine(ThirstPenalty());

    public void ConsumeDrink(float amount)
    {
        if (currentValue <= 0) return;
        currentValue -= RemoveFactor(amount);
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);
    public void AddDrinkEffect(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkEffectRoutine);

    public void AddDrinkAddFactorEffect(string effectName, float drinkFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkAddFactorEffectRoutine);

    public void AddDrinkRemoveFactorEffect(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);

    public void RemoveDrinkEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(DrinkEffectRoutines, effectName, effectRoutine);
    }
    public void StopAllDrinkEffects()
    {
        StopAllEffects(DrinkEffectRoutines);
    }
    public void StopAllDrinkEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(DrinkEffectRoutines, isBuff);
    }
    public IEnumerator ConsumeDrinkRoutine()
    {
        if (isRegenerating)
        {
            isRegenerating = false;
            yield break;
        }
        isConsuming = true;

        while (currentValue >= 0)
        {
            currentValue -= decrementValue;

            if (!isConsuming)
            {
                isConsuming = true;
                break;
            }
            yield return new WaitForSeconds(tickRate);
        }
    }

    public IEnumerator ThirstPenalty()
    {
        while (currentValue <= 0)
        {
            healthManager.ConsumeHP(dehydratedHpPenalty, true);
            yield return new WaitForSeconds(dehydratedHpPenaltyTickRate);
        }

        DrinkPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
    }

    public IEnumerator ApplyDrinkEffectRoutine(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            drinkAmount,
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

    public IEnumerator ApplyDrinkAddFactorEffectRoutine(string effectName, float drinkAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            drinkAddFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor = total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplyDrinkRemoveFactorEffectRoutine(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            drinkRemoveFactorAmount,
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
