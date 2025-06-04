using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BodyHeatManager : StatusManager
{
    [Header("Body Heat-related fields")]
    [SerializeField] private float normalTemperature = 50f; // Middle point of the temperature scale
    [SerializeField] private float coldThreshold = 30f;
    [SerializeField] private float hotThreshold = 70f;
    [SerializeField] private float extremeColdThreshold = 10f;
    [SerializeField] private float extremeHotThreshold = 90f;

    [Header("Cold Effects")]
    [SerializeField] private float coldFoodConsumptionMultiplier = 1.5f;
    [SerializeField] private float extremeColdHealthDrain = 2f;

    [Header("Hot Effects")]
    [SerializeField] private float hotThirstConsumptionMultiplier = 1.5f;
    [SerializeField] private float hotStaminaConsumptionMultiplier = 1.3f;
    [SerializeField] private float extremeHotHealthDrain = 2f;

    [Header("UI Settings")]
    [SerializeField] private bool showUIWhenNormal = false; // Whether to show UI when temperature is normal

    [SerializeField] private float environmentalTemperature = 50f; // Current environmental temperature
    [SerializeField] private float temperatureChangeRate = 1f;

    private Coroutine temperatureEffectsRoutine;
    private Coroutine environmentalTemperatureRoutine;
    private List<CoroutineInfo> bodyHeatEffectRoutines = new List<CoroutineInfo>();

    // References to other managers
    [SerializeField] private HungerManager hungerManager;
    [SerializeField] private ThirstManager thirstManager;
    [SerializeField] private SpeedManager speedManager;
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private HealthManager healthManager;

    public TemperatureLevel CurrentTemperatureLevel => GetTemperatureLevel();
    public float EnvironmentalTemperature { get => environmentalTemperature; set => environmentalTemperature = value; }
    public List<CoroutineInfo> BodyHeatEffectRoutines { get => bodyHeatEffectRoutines; set => bodyHeatEffectRoutines = value; }

    public enum TemperatureLevel
    {
        ExtremelyCold,
        Cold,
        Normal,
        Hot,
        ExtremelyHot
    }

    private void Awake()
    {
        speedManager = this.CheckComponent(speedManager, nameof(speedManager));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
        healthManager = this.CheckComponent(healthManager, nameof(healthManager));
        thirstManager = this.CheckComponent(thirstManager, nameof(thirstManager));
        hungerManager = this.CheckComponent(hungerManager, nameof(hungerManager));
    }

    private void Start()
    {
        currentValue = normalTemperature;
        temperatureEffectsRoutine = StartCoroutine(TemperatureEffectsRoutine());
        environmentalTemperatureRoutine = StartCoroutine(EnvironmentalTemperatureRoutine());

        // Initially hide UI if temperature is normal
        UpdateUIVisibility();
    }

    private void Update()
    {
        UpdateBodyHeatBar();
    }

    public void UpdateBodyHeatBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
        UpdateUIVisibility();
    }

    private void UpdateUIVisibility()
    {
        if (uiImage == null) return;

        TemperatureLevel level = GetTemperatureLevel();
        bool shouldShowUI = showUIWhenNormal || level != TemperatureLevel.Normal;

        // Show/hide the UI based on temperature level
        if (uiImage.gameObject.activeInHierarchy != shouldShowUI)
        {
            uiImage.gameObject.SetActive(shouldShowUI);
        }
    }

    public TemperatureLevel GetTemperatureLevel()
    {
        if (currentValue <= extremeColdThreshold)
            return TemperatureLevel.ExtremelyCold;
        else if (currentValue <= coldThreshold)
            return TemperatureLevel.Cold;
        else if (currentValue >= extremeHotThreshold)
            return TemperatureLevel.ExtremelyHot;
        else if (currentValue >= hotThreshold)
            return TemperatureLevel.Hot;
        else
            return TemperatureLevel.Normal;
    }

    public void ModifyBodyHeat(float amount)
    {
        currentValue += amount;
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public void SetEnvironmentalTemperature(float temperature)
    {
        environmentalTemperature = Mathf.Clamp(temperature, 0, maxValue);
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddBodyHeatEffect(string effectName, float heatAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(BodyHeatEffectRoutines, effectName, heatAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyBodyHeatEffectRoutine);

    public void RemoveBodyHeatEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(BodyHeatEffectRoutines, effectName, effectRoutine);

    public void StopAllBodyHeatEffects()
        => StopAllEffects(BodyHeatEffectRoutines);

    public void StopAllBodyHeatEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(BodyHeatEffectRoutines, isBuff);

    // Coroutines
    private IEnumerator EnvironmentalTemperatureRoutine()
    {
        while (true)
        {
            // Gradually adjust body temperature towards environmental temperature
            if (currentValue < environmentalTemperature)
            {
                currentValue += temperatureChangeRate * Time.deltaTime;
            }
            else if (currentValue > environmentalTemperature)
            {
                currentValue -= temperatureChangeRate * Time.deltaTime;
            }

            currentValue = Mathf.Clamp(currentValue, 0, maxValue);
            yield return null; // Update every frame for smooth temperature changes
        }
    }

    private IEnumerator TemperatureEffectsRoutine()
    {
        while (true)
        {
            ApplyTemperatureEffects();
            yield return new WaitForSeconds(1f); // Check effects every second
        }
    }

    private void ApplyTemperatureEffects()
    {
        TemperatureLevel level = GetTemperatureLevel();

        switch (level)
        {
            case TemperatureLevel.Normal:
                RemoveTemperatureEffects();
                break;

            case TemperatureLevel.Cold:
                RemoveTemperatureEffects(); // Clean up first
                ApplyColdEffects();
                break;

            case TemperatureLevel.ExtremelyCold:
                RemoveTemperatureEffects(); // Clean up first
                ApplyColdEffects();
                ApplyExtremeColdEffects();
                break;

            case TemperatureLevel.Hot:
                RemoveTemperatureEffects(); // Clean up first
                ApplyHotEffects();
                break;

            case TemperatureLevel.ExtremelyHot:
                RemoveTemperatureEffects(); // Clean up first
                ApplyHotEffects();
                ApplyExtremeHotEffects();
                break;
        }
    }

    private void RemoveTemperatureEffects()
    {
        // Remove all temperature-related effects when returning to normal
        if (speedManager != null)
        {
            speedManager.RemoveSpeedEffect("ColdSpeed");
        }

        if (hungerManager != null)
        {
            hungerManager.RemoveFoodEffect("ColdHunger");
        }

        if (thirstManager != null)
        {
            thirstManager.RemoveDrinkEffect("HotThirst");
        }

        if (staminaManager != null)
        {
            staminaManager.RemoveStaminaEffect("HotStamina");
        }
    }

    private void ApplyColdEffects()
    {
        // Increase food consumption
        if (hungerManager != null)
        {
            hungerManager.AddFoodRemoveFactorEffect("ColdHunger", coldFoodConsumptionMultiplier * 10f, 2f, 1f, false, false);
        }

        // Apply speed reduction through speed manager
        if (speedManager != null)
        {
            speedManager.AddSpeedFactorEffect("ColdSpeed", -20f, 2f, 1f, false, false);
        }
    }

    private void ApplyExtremeColdEffects()
    {
        // Drain health when extremely cold
        if (healthManager != null)
        {
            healthManager.ConsumeHP(extremeColdHealthDrain * Time.deltaTime, true);
        }
    }

    private void ApplyHotEffects()
    {
        // Increase thirst consumption
        if (thirstManager != null)
        {
            thirstManager.AddDrinkRemoveFactorEffect("HotThirst", hotThirstConsumptionMultiplier * 10f, 2f, 1f, false, false);
        }

        // Increase stamina consumption
        if (staminaManager != null)
        {
            staminaManager.AddStaminaDamageFactorEffect("HotStamina", hotStaminaConsumptionMultiplier * 10f, 2f, 1f, false, false);
        }
    }

    private void ApplyExtremeHotEffects()
    {
        // Drain health when extremely hot
        if (healthManager != null)
        {
            healthManager.ConsumeHP(extremeHotHealthDrain * Time.deltaTime, true);
        }
    }

    public IEnumerator ApplyBodyHeatEffectRoutine(string effectName, float heatAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            heatAmount,
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
            _ => currentValue = Mathf.Clamp(currentValue, 0, maxValue)
        );
    }
}