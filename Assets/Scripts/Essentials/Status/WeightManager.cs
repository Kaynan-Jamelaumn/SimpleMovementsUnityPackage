using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeightManager: StatusManager
{
    private const float speedReductionByWeightConstant = 0.28f;

    [Header("Weight-related fields")]
    [SerializeField] private float weight;
    [SerializeField] private float maxWeight = 100;
    [SerializeField] private float incrementFactor = 15;

    [SerializeField] private bool shouldHaveWeight = true;
    [SerializeField] private Image weightImage;

    private Coroutine weightIncreaseEffectRoutine;
    private List<CoroutineInfo> weightIncreaseEffectRoutines = new List<CoroutineInfo>();
    public Coroutine WeightIncreaseEffectRoutine { get => weightIncreaseEffectRoutine; set => weightIncreaseEffectRoutine = value; }
    public List<CoroutineInfo> WeightIncreaseEffectRoutines { get => weightIncreaseEffectRoutines; set => weightIncreaseEffectRoutines = value; }
    public float MaxWeight { get => maxWeight; set => maxWeight = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float Weight { get => weight; set => weight = value; }
    public float SpeedReductionByWeightConstant { get => speedReductionByWeightConstant; }
    public bool ShouldHaveWeight { get => shouldHaveWeight; set => shouldHaveWeight = value; }
    public Image WeightImage { get => weightImage; set => weightImage = value; }
    public void UpdateWeightBar()
    {
        UpdateBar(Weight, MaxWeight, WeightImage);
    }
    public void AddWeight(float amount)
    {
        Weight += amount;// Mathf.Min(Weight + amount, MaxWeight);
    }
    public void ModifyMaxWeight(float amount)
    {
        MaxWeight += amount;
    }
    public void ConsumeWeight(float amount)
    {
        if (Weight <= 0) return;
        Weight -= amount;
    }
    public void AddWeightEffect(string effectName, float weightAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(WeightIncreaseEffectRoutines, effectName, weightAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyWeightIncreaseEffectRoutine);
    }

    public float CalculateSpeedBasedOnWeight(float speed)
    {
        if (!ShouldHaveWeight) return speed;
        float weightPercentage = Weight / MaxWeight;
        float speedReductionByWeight = -SpeedReductionByWeightConstant;

        if (weightPercentage >= 0.82f && weightPercentage <= 1.0f) speedReductionByWeight -= 0.255f;
        else if (Weight > MaxWeight) speedReductionByWeight -= 0.5f;

        float currentSpeedBasedOnWeightCarried = speed * Mathf.Exp(speedReductionByWeight * weightPercentage);
        return currentSpeedBasedOnWeightCarried;
    }

    public IEnumerator ApplyWeightIncreaseEffectRoutine(string effectName, float weightIncreaseAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = weightIncreaseAmount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        float maxWeightOriginalAmount = MaxWeight;

        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                MaxWeight += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                MaxWeight = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        MaxWeight = maxWeightOriginalAmount;
    }
}

