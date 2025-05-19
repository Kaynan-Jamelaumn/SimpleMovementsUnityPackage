using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeightManager : StatusManager
{
    private const float SpeedReductionFactor = 0.28f;

    [Header("Weight-related fields")]

    [SerializeField] private bool shouldHaveWeight = true;

    private Coroutine weightIncreaseEffectRoutine;
    private readonly List<CoroutineInfo> weightIncreaseEffectRoutines = new List<CoroutineInfo>();
    public List<CoroutineInfo> WeightIncreaseEffectRoutines => weightIncreaseEffectRoutines;
    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);
    public void UpdateWeightBar()
    {
        if (uiImage == null)
        {
            Debug.LogWarning("WeightImage is not assigned.");
            return;
        }

        UpdateBar(currentValue, maxValue, uiImage);
    }

    public void AddWeight(float amount)
    {
        currentValue += amount;
        UpdateWeightBar();
    }

    public void ModifyMaxWeight(float amount)
    {
        maxValue += amount;
        UpdateWeightBar();
    }

    public void ConsumeWeight(float amount)
    {
        if (currentValue > 0)
        {
            currentValue -= amount;
            UpdateWeightBar();
        }
    }

    public void AddWeightEffect(string effectName, float weightAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(WeightIncreaseEffectRoutines, effectName, weightAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyWeightIncreaseEffectRoutine);
    }

    public float CalculateSpeedBasedOnWeight(float baseSpeed)
    {
        if (!shouldHaveWeight) return baseSpeed;

        float weightPercentage = currentValue / maxValue;
        float speedFactor = -SpeedReductionFactor;

        if (weightPercentage >= 0.82f && weightPercentage <= 1.0f)
            speedFactor -= 0.255f;
        else if (currentValue > maxValue)
            speedFactor -= 0.5f;

        return baseSpeed * Mathf.Exp(speedFactor * weightPercentage);
    }

    public IEnumerator ApplyWeightIncreaseEffectRoutine(string effectName, float weightIncreaseAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float increment = isProcedural ? weightIncreaseAmount / (timeBuffEffect / tickCooldown) : weightIncreaseAmount;
        float originalMaxWeight = maxValue;

        return ApplyEffectRoutine(effectName, weightIncreaseAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable,
            perTick => {
                maxValue += perTick;
                UpdateWeightBar();
            },
            total => {
                maxValue = total;
                UpdateWeightBar();
            },
            _ => {
                maxValue = originalMaxWeight;
                UpdateWeightBar();
            }
        );
    }
}