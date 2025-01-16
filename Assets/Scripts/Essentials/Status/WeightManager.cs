using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeightManager : StatusManager
{
    private const float SpeedReductionFactor = 0.28f;

    [Header("Weight-related fields")]
    [SerializeField] private float weight = 0f;
    [SerializeField] private float maxWeight = 100f;
    [SerializeField] private float incrementFactor = 15f;
    [SerializeField] private bool shouldHaveWeight = true;
    [SerializeField] private Image weightImage;

    private Coroutine weightIncreaseEffectRoutine;
    private readonly List<CoroutineInfo> weightIncreaseEffectRoutines = new List<CoroutineInfo>();

    public float MaxWeight
    {
        get => maxWeight;
        set => maxWeight = Mathf.Max(0f, value);
    }

    public float IncrementFactor
    {
        get => incrementFactor;
        set => incrementFactor = Mathf.Max(0f, value);
    }

    public float Weight
    {
        get => weight;
        set => weight = Mathf.Clamp(value, 0f, MaxWeight);
    }

    public bool ShouldHaveWeight
    {
        get => shouldHaveWeight;
        set => shouldHaveWeight = value;
    }

    public Image WeightImage
    {
        get => weightImage;
        set => weightImage = value;
    }

    public List<CoroutineInfo> WeightIncreaseEffectRoutines => weightIncreaseEffectRoutines;

    /// <summary>
    /// Updates the weight bar UI.
    /// </summary>
    public void UpdateWeightBar()
    {
        if (WeightImage == null)
        {
            Debug.LogWarning("WeightImage is not assigned.");
            return;
        }

        UpdateBar(Weight, MaxWeight, WeightImage);
    }

    /// <summary>
    /// Adds weight to the current total.
    /// </summary>
    /// <param name="amount">The amount of weight to add.</param>
    public void AddWeight(float amount)
    {
        Weight += amount;
        UpdateWeightBar();
    }

    /// <summary>
    /// Modifies the maximum weight capacity.
    /// </summary>
    /// <param name="amount">The amount to add to the maximum weight.</param>
    public void ModifyMaxWeight(float amount)
    {
        MaxWeight += amount;
        UpdateWeightBar();
    }

    /// <summary>
    /// Consumes weight, reducing the current total.
    /// </summary>
    /// <param name="amount">The amount of weight to consume.</param>
    public void ConsumeWeight(float amount)
    {
        if (Weight > 0)
        {
            Weight -= amount;
            UpdateWeightBar();
        }
    }

    /// <summary>
    /// Adds a weight-related effect.
    /// </summary>
    public void AddWeightEffect(string effectName, float weightAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(WeightIncreaseEffectRoutines, effectName, weightAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyWeightIncreaseEffectRoutine);
    }

    /// <summary>
    /// Calculates movement speed based on current weight.
    /// </summary>
    /// <param name="baseSpeed">The base movement speed.</param>
    /// <returns>The adjusted speed based on weight.</returns>
    public float CalculateSpeedBasedOnWeight(float baseSpeed)
    {
        if (!ShouldHaveWeight) return baseSpeed;

        float weightPercentage = Weight / MaxWeight;
        float speedFactor = -SpeedReductionFactor;

        if (weightPercentage >= 0.82f && weightPercentage <= 1.0f)
            speedFactor -= 0.255f;
        else if (Weight > MaxWeight)
            speedFactor -= 0.5f;

        return baseSpeed * Mathf.Exp(speedFactor * weightPercentage);
    }

    /// <summary>
    /// Applies a weight-increase effect over time.
    /// </summary>
    public IEnumerator ApplyWeightIncreaseEffectRoutine(string effectName, float weightIncreaseAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float increment = isProcedural ? weightIncreaseAmount / (timeBuffEffect / tickCooldown) : weightIncreaseAmount;
        float originalMaxWeight = MaxWeight;

        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                MaxWeight += increment;
                UpdateWeightBar();
                yield return new WaitForSeconds(tickCooldown);
            }
            else
            {
                MaxWeight = increment;
                UpdateWeightBar();
                yield break; // Non-procedural effects are applied instantly.
            }
        }

        // Restore original max weight
        MaxWeight = originalMaxWeight;
        UpdateWeightBar();
    }
}
