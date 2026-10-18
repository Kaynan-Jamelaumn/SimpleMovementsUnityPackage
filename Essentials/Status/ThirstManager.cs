using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thirst (100 = hydrated, 0 = dehydrated). Drains <b>Decrement Value</b> every <b>Consuming Tick Rate</b> seconds (see
/// <see cref="NeedStatusManager"/>); drinks, potions and abilities fill it. While empty the character loses
/// <see cref="dehydratedHpPenalty"/> health every <see cref="dehydratedHpPenaltyTickRate"/> seconds.
/// </summary>
public class ThirstManager : NeedStatusManager
{
    [Header("Thirst-related fields")]
    [SerializeField, HideInInspector] private float drink;
    [Tooltip("Health lost per dehydration tick while thirst is 0.")]
    [SerializeField] private float dehydratedHpPenalty = 1f;
    [Tooltip("Seconds between two dehydration ticks.")]
    [SerializeField] private float dehydratedHpPenaltyTickRate = 2f;

    private List<CoroutineInfo> drinkEffectRoutines = new List<CoroutineInfo>();
    public List<CoroutineInfo> DrinkEffectRoutines { get => drinkEffectRoutines; set => drinkEffectRoutines = value; }

    /// <summary>Kept for older scripts: thirst no longer uses a consumption coroutine (always null).</summary>
    public Coroutine DrinkConsumptionRoutine { get => null; set { } }
    /// <summary>Kept for older scripts: the dehydration penalty no longer uses a coroutine (always null).</summary>
    public Coroutine DrinkPenaltyRoutine { get => null; set { } }

    protected override float PenaltyDamage => dehydratedHpPenalty;
    protected override float PenaltyInterval => dehydratedHpPenaltyTickRate;

    public void UpdateDrinkBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    /// <summary>Kept for older scripts: the penalty runs by itself while thirst is 0.</summary>
    public void ApplyThirstPenalty() { }

    /// <summary>Removes <paramref name="amount"/> thirst now (changed by Decrement Factor).</summary>
    public void ConsumeDrink(float amount) => Consume(amount);

    public void AddDrinkEffect(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkEffectRoutine);
    public void AddDrinkAddFactorEffect(string effectName, float drinkFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkAddFactorEffectRoutine);
    public void AddDrinkRemoveFactorEffect(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(DrinkEffectRoutines, effectName, drinkRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);

    public void RemoveDrinkEffect(string effectName = null, Coroutine effectRoutine = null) => RemoveEffect(DrinkEffectRoutines, effectName, effectRoutine);
    public void StopAllDrinkEffects() => StopAllEffects(DrinkEffectRoutines);
    public void StopAllDrinkEffectsByType(bool isBuff = true) => StopAllEffectsByType(DrinkEffectRoutines, isBuff);

    public IEnumerator ApplyDrinkEffectRoutine(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            drinkAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => currentValue = Mathf.Clamp(currentValue + (perTick > 0 ? AddFactor(perTick) : -RemoveFactor(-perTick)), 0f, maxValue),
            total => currentValue = Mathf.Clamp(currentValue + (total > 0 ? AddFactor(total) : -RemoveFactor(-total)), 0f, maxValue),
            _ => currentValue = Mathf.Clamp(currentValue, 0f, maxValue)
        );
    }

    public IEnumerator ApplyDrinkAddFactorEffectRoutine(string effectName, float drinkAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        // Added while it lasts and taken away exactly when it ends, is replaced or removed.
        => ModifierEffect(effectName, drinkAddFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => incrementFactor += v);

    public IEnumerator ApplyDrinkRemoveFactorEffectRoutine(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => ModifierEffect(effectName, drinkRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => decrementFactor += v);

    protected override void OnValidate()
    {
        base.OnValidate();
        dehydratedHpPenalty = Mathf.Max(0f, dehydratedHpPenalty);
        dehydratedHpPenaltyTickRate = Mathf.Max(0.1f, dehydratedHpPenaltyTickRate);
    }
}
