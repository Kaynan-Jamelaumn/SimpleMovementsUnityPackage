using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hunger (100 = fed, 0 = starving). Drains <b>Decrement Value</b> every <b>Consuming Tick Rate</b> seconds (see
/// <see cref="NeedStatusManager"/>); food, potions and abilities fill it. While empty the character loses
/// <see cref="starvingHpPenalty"/> health every <see cref="starvingHpPenaltyTickRate"/> seconds.
/// </summary>
public class HungerManager : NeedStatusManager
{
    [Header("Hunger-related fields")]
    [Tooltip("Health lost per starvation tick while hunger is 0.")]
    [SerializeField] private float starvingHpPenalty = 1f;
    [Tooltip("Seconds between two starvation ticks.")]
    [SerializeField] private float starvingHpPenaltyTickRate = 2f;

    private List<CoroutineInfo> foodEffectRoutines = new List<CoroutineInfo>();
    public List<CoroutineInfo> FoodEffectRoutines { get => foodEffectRoutines; set => foodEffectRoutines = value; }

    /// <summary>Kept for older scripts: hunger no longer uses a consumption coroutine (always null).</summary>
    public Coroutine FoodConsumptionRoutine { get => null; set { } }
    /// <summary>Kept for older scripts: the starvation penalty no longer uses a coroutine (always null).</summary>
    public Coroutine FoodPenaltyRoutine { get => null; set { } }

    protected override float PenaltyDamage => starvingHpPenalty;
    protected override float PenaltyInterval => starvingHpPenaltyTickRate;

    public void UpdateFoodBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    /// <summary>Kept for older scripts: the penalty runs by itself while hunger is 0.</summary>
    public void ApplyHungerPenalty() { }

    /// <summary>Removes <paramref name="amount"/> hunger now (changed by Decrement Factor).</summary>
    public void ConsumeFood(float amount) => Consume(amount);

    public void AddFoodEffect(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodEffectRoutine);
    public void AddFoodAddFactorEffect(string effectName, float foodFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodAddFactorEffectRoutine);
    public void AddFoodRemoveFactorEffect(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(FoodEffectRoutines, effectName, foodRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodRemoveFactorEffectRoutine);

    public void RemoveFoodEffect(string effectName = null, Coroutine effectRoutine = null) => RemoveEffect(FoodEffectRoutines, effectName, effectRoutine);
    public void StopAllFoodEffects() => StopAllEffects(FoodEffectRoutines);
    public void StopAllFoodEffectsByType(bool isBuff = true) => StopAllEffectsByType(FoodEffectRoutines, isBuff);

    public IEnumerator ApplyFoodEffectRoutine(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            foodAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => currentValue = Mathf.Clamp(currentValue + (perTick > 0 ? AddFactor(perTick) : -RemoveFactor(-perTick)), 0f, maxValue),
            total => currentValue = Mathf.Clamp(currentValue + (total > 0 ? AddFactor(total) : -RemoveFactor(-total)), 0f, maxValue),
            _ => currentValue = Mathf.Clamp(currentValue, 0f, maxValue)
        );
    }

    public IEnumerator ApplyFoodAddFactorEffectRoutine(string effectName, float foodAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        // Added while it lasts and taken away exactly when it ends, is replaced or removed.
        => ModifierEffect(effectName, foodAddFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => incrementFactor += v);

    public IEnumerator ApplyFoodRemoveFactorEffectRoutine(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => ModifierEffect(effectName, foodRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => decrementFactor += v);

    protected override void OnValidate()
    {
        base.OnValidate();
        starvingHpPenalty = Mathf.Max(0f, starvingHpPenalty);
        starvingHpPenaltyTickRate = Mathf.Max(0.1f, starvingHpPenaltyTickRate);
    }
}
