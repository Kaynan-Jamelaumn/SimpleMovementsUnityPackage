using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OxygenManager : StatusManager
{
    [Header("Oxygen-related fields")]
    [SerializeField] private float underwaterOxygenDrainRate = 5f;
    [SerializeField] private float highAltitudeOxygenDrainRate = 2f;
    [SerializeField] private float poorVentilationOxygenDrainRate = 1f;
    [SerializeField] private float oxygenDepletedHealthDrainRate = 10f;
    [SerializeField] private float oxygenTankRestoreRate = 20f;

    [Header("UI Settings")]
    [SerializeField] private bool showUIWhenNormal = false; // Whether to show UI when oxygen consumption is normal

    [SerializeField] private bool isUnderwater = false;
    [SerializeField] private bool isAtHighAltitude = false;
    [SerializeField] private bool isInPoorVentilation = false;
    [SerializeField] private bool hasOxygenTank = false;
    [SerializeField] private float oxygenTankCapacity = 100f;
    [SerializeField] private float currentOxygenTankAmount = 0f;

    private Coroutine oxygenConsumptionRoutine;
    private Coroutine oxygenRegenerationRoutine;
    private Coroutine oxygenDepletionRoutine;
    private List<CoroutineInfo> oxygenEffectRoutines = new List<CoroutineInfo>();

    // References to other managers
    [SerializeField] private HealthManager healthManager;

    public OxygenEnvironment CurrentEnvironment => GetCurrentEnvironment();
    public bool IsInRestrictedEnvironment => isUnderwater || isAtHighAltitude || isInPoorVentilation;
    public float OxygenTankPercentage => hasOxygenTank ? (currentOxygenTankAmount / oxygenTankCapacity) * 100f : 0f;
    public List<CoroutineInfo> OxygenEffectRoutines { get => oxygenEffectRoutines; set => oxygenEffectRoutines = value; }

    public enum OxygenEnvironment
    {
        Normal,
        Underwater,
        HighAltitude,
        PoorVentilation,
        Critical
    }

    private void Awake()
    {
        healthManager = this.CheckComponent(healthManager, nameof(healthManager));
    }

    private void Start()
    {
        currentValue = maxValue;
        oxygenConsumptionRoutine = StartCoroutine(OxygenConsumptionRoutine());

        // Initially hide UI if in normal environment
        UpdateUIVisibility();
    }

    private void Update()
    {
        UpdateOxygenBar();

        // Check if oxygen is depleted and start health drain
        if (currentValue <= 0 && oxygenDepletionRoutine == null)
        {
            oxygenDepletionRoutine = StartCoroutine(OxygenDepletionRoutine());
        }
        else if (currentValue > 0 && oxygenDepletionRoutine != null)
        {
            StopCoroutine(oxygenDepletionRoutine);
            oxygenDepletionRoutine = null;
        }
    }

    public void UpdateOxygenBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
        UpdateUIVisibility();
    }

    private void UpdateUIVisibility()
    {
        if (uiImage == null) return;

        bool shouldShowUI = showUIWhenNormal || IsInRestrictedEnvironment || currentValue < maxValue;

        // Show/hide the UI based on oxygen consumption state
        if (uiImage.gameObject.activeInHierarchy != shouldShowUI)
        {
            uiImage.gameObject.SetActive(shouldShowUI);
        }
    }

    public OxygenEnvironment GetCurrentEnvironment()
    {
        if (currentValue <= 0)
            return OxygenEnvironment.Critical;
        else if (isUnderwater)
            return OxygenEnvironment.Underwater;
        else if (isAtHighAltitude)
            return OxygenEnvironment.HighAltitude;
        else if (isInPoorVentilation)
            return OxygenEnvironment.PoorVentilation;
        else
            return OxygenEnvironment.Normal;
    }

    public void SetUnderwater(bool underwater)
    {
        isUnderwater = underwater;
        UpdateOxygenConsumption();
    }

    public void SetHighAltitude(bool highAltitude)
    {
        isAtHighAltitude = highAltitude;
        UpdateOxygenConsumption();
    }

    public void SetPoorVentilation(bool poorVentilation)
    {
        isInPoorVentilation = poorVentilation;
        UpdateOxygenConsumption();
    }

    public void SetOxygenTank(bool hasOxygen, float tankAmount = 0f)
    {
        hasOxygenTank = hasOxygen;
        currentOxygenTankAmount = Mathf.Clamp(tankAmount, 0f, oxygenTankCapacity);
    }

    public void UseOxygenTank(float amount)
    {
        if (!hasOxygenTank || currentOxygenTankAmount <= 0) return;

        float oxygenToRestore = Mathf.Min(amount, currentOxygenTankAmount);
        currentOxygenTankAmount -= oxygenToRestore;

        currentValue += oxygenToRestore;
        currentValue = Mathf.Min(maxValue, currentValue);

        if (currentOxygenTankAmount <= 0)
        {
            hasOxygenTank = false;
        }
    }

    private void UpdateOxygenConsumption()
    {
        if (oxygenConsumptionRoutine != null)
        {
            StopCoroutine(oxygenConsumptionRoutine);
        }

        if (oxygenRegenerationRoutine != null)
        {
            StopCoroutine(oxygenRegenerationRoutine);
            oxygenRegenerationRoutine = null;
        }

        if (IsInRestrictedEnvironment)
        {
            oxygenConsumptionRoutine = StartCoroutine(OxygenConsumptionRoutine());
        }
        else
        {
            oxygenRegenerationRoutine = StartCoroutine(OxygenRegenerationRoutine());
        }
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddOxygenEffect(string effectName, float oxygenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(OxygenEffectRoutines, effectName, oxygenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyOxygenEffectRoutine);

    public void RemoveOxygenEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(OxygenEffectRoutines, effectName, effectRoutine);

    public void StopAllOxygenEffects()
        => StopAllEffects(OxygenEffectRoutines);

    public void StopAllOxygenEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(OxygenEffectRoutines, isBuff);

    // Coroutines
    private IEnumerator OxygenConsumptionRoutine()
    {
        while (IsInRestrictedEnvironment && currentValue > 0)
        {
            float drainRate = GetOxygenDrainRate();

            // Check if oxygen tank can be used automatically
            if (hasOxygenTank && currentOxygenTankAmount > 0 && currentValue < maxValue * 0.5f)
            {
                UseOxygenTank(oxygenTankRestoreRate * tickRate);
            }
            else
            {
                currentValue -= RemoveFactor(drainRate);
                currentValue = Mathf.Max(0, currentValue);
            }

            yield return new WaitForSeconds(tickRate);
        }
    }

    private IEnumerator OxygenRegenerationRoutine()
    {
        while (!IsInRestrictedEnvironment && currentValue < maxValue)
        {
            currentValue += incrementValue;
            currentValue = Mathf.Min(maxValue, currentValue);

            yield return new WaitForSeconds(tickRate);
        }
    }

    private IEnumerator OxygenDepletionRoutine()
    {
        while (currentValue <= 0)
        {
            if (healthManager != null)
            {
                healthManager.ConsumeHP(oxygenDepletedHealthDrainRate, true);
            }

            yield return new WaitForSeconds(1f);
        }

        oxygenDepletionRoutine = null;
    }

    private float GetOxygenDrainRate()
    {
        if (isUnderwater)
            return underwaterOxygenDrainRate;
        else if (isAtHighAltitude)
            return highAltitudeOxygenDrainRate;
        else if (isInPoorVentilation)
            return poorVentilationOxygenDrainRate;
        else
            return 0f;
    }

    public IEnumerator ApplyOxygenEffectRoutine(string effectName, float oxygenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            oxygenAmount,
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