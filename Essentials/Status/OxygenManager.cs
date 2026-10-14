using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OxygenManager : StatusManager
{
    [Header("Oxygen-related fields")]
    [Tooltip("Oxygen lost per tick while the head is under water.")]
    [SerializeField] private float underwaterOxygenDrainRate = 5f;
    [Tooltip("Oxygen lost per tick at high altitude.")]
    [SerializeField] private float highAltitudeOxygenDrainRate = 2f;
    [Tooltip("Oxygen lost per tick in a poorly ventilated place.")]
    [SerializeField] private float poorVentilationOxygenDrainRate = 1f;
    [Tooltip("Oxygen given back per tick by an oxygen tank.")]
    [SerializeField] private float oxygenTankRestoreRate = 20f;

    [Header("Suffocation (out of breath)")]
    [Tooltip("Health lost every Suffocation Interval while out of oxygen (or a percentage of max health, see below). 0 = no damage.")]
    [SerializeField, Min(0f)] private float oxygenDepletedHealthDrainRate = 10f;
    [Tooltip("Seconds between two suffocation hits.")]
    [SerializeField, Min(0.1f)] private float suffocationInterval = 1f;
    [Tooltip("Seconds at zero oxygen before the first hit (a last moment to reach the surface). 0 = at once.")]
    [SerializeField, Min(0f)] private float suffocationGraceSeconds = 0f;
    [Tooltip("The damage is a percentage of the max health instead of points (10 = 10% of max health per hit).")]
    [SerializeField] private bool suffocationPercentOfMaxHealth = false;
    [Tooltip("Extra damage added to every next hit while still out of breath (it gets worse the longer it lasts). 0 = constant.")]
    [SerializeField, Min(0f)] private float suffocationDamageIncrease = 0f;
    [Tooltip("Most damage one hit can do once it has grown (0 = no limit).")]
    [SerializeField, Min(0f)] private float suffocationMaxDamage = 0f;

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
    private bool started;

    // References to other managers
    [SerializeField] private HealthManager healthManager;

    public OxygenEnvironment CurrentEnvironment => GetCurrentEnvironment();
    public bool IsInRestrictedEnvironment => isUnderwater || isAtHighAltitude || isInPoorVentilation;
    public float OxygenTankPercentage => hasOxygenTank ? (currentOxygenTankAmount / oxygenTankCapacity) * 100f : 0f;
    public bool IsUnderwater => isUnderwater;
    public bool IsAtHighAltitude => isAtHighAltitude;
    public bool IsInPoorVentilation => isInPoorVentilation;
    public bool HasOxygenTank => hasOxygenTank;
    /// <summary>Oxygen left in the tank (0 without one).</summary>
    public float OxygenTankAmount => currentOxygenTankAmount;
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
        started = true;
        // A client's copy keeps the server's value and runs no routines (the server simulates breathing).
        if (!IsReplica)
        {
            currentValue = maxValue;
            oxygenConsumptionRoutine = StartCoroutine(OxygenConsumptionRoutine());
        }

        // Initially hide UI if in normal environment
        UpdateUIVisibility();
    }

    protected override void OnReplicaChanged(bool on)
    {
        oxygenConsumptionRoutine = null;
        oxygenRegenerationRoutine = null;
        oxygenDepletionRoutine = null;
        if (!on && started)
            UpdateOxygenConsumption();
    }

    /// <summary>
    /// Client copies: shows the server's environment (underwater, altitude, ventilation) and oxygen tank, which the bar
    /// visibility and the tank display read. Ignored where the rules run.
    /// </summary>
    public void ApplyReplicatedEnvironment(bool underwater, bool highAltitude, bool poorVentilation, bool hasTank, float tankAmount)
    {
        if (!IsReplica)
            return;
        isUnderwater = underwater;
        isAtHighAltitude = highAltitude;
        isInPoorVentilation = poorVentilation;
        hasOxygenTank = hasTank;
        currentOxygenTankAmount = float.IsNaN(tankAmount) || float.IsInfinity(tankAmount) ? 0f : Mathf.Clamp(tankAmount, 0f, oxygenTankCapacity);
    }

    private void Update()
    {
        UpdateOxygenBar();
        if (IsReplica)
            return; // suffocation damage is dealt on the server

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
        if (IsReplica) return; // the server decides the environment of a networked character
        isUnderwater = underwater;
        UpdateOxygenConsumption();
    }

    public void SetHighAltitude(bool highAltitude)
    {
        if (IsReplica) return;
        isAtHighAltitude = highAltitude;
        UpdateOxygenConsumption();
    }

    public void SetPoorVentilation(bool poorVentilation)
    {
        if (IsReplica) return;
        isInPoorVentilation = poorVentilation;
        UpdateOxygenConsumption();
    }

    public void SetOxygenTank(bool hasOxygen, float tankAmount = 0f)
    {
        if (IsReplica) return;
        hasOxygenTank = hasOxygen;
        currentOxygenTankAmount = Mathf.Clamp(tankAmount, 0f, oxygenTankCapacity);
    }

    public void UseOxygenTank(float amount)
    {
        if (IsReplica) return;
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
        if (IsReplica)
            return;
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
        if (suffocationGraceSeconds > 0f)
        {
            float until = Time.time + suffocationGraceSeconds;
            while (currentValue <= 0 && Time.time < until)
                yield return null;
        }
        int hits = 0;
        while (currentValue <= 0)
        {
            float damage = SuffocationDamage(hits++);
            if (healthManager != null && damage > 0f)
                healthManager.ConsumeHP(damage, true);
            yield return new WaitForSeconds(Mathf.Max(0.1f, suffocationInterval));
        }

        oxygenDepletionRoutine = null;
    }

    /// <summary>The damage of the <paramref name="hit"/>-th suffocation hit (0 = the first).</summary>
    public float SuffocationDamage(int hit)
    {
        float damage = oxygenDepletedHealthDrainRate + suffocationDamageIncrease * Mathf.Max(0, hit);
        if (suffocationMaxDamage > 0f && suffocationDamageIncrease > 0f)
            damage = Mathf.Min(damage, Mathf.Max(oxygenDepletedHealthDrainRate, suffocationMaxDamage));
        if (suffocationPercentOfMaxHealth)
            damage = healthManager != null ? healthManager.MaxValue * damage / 100f : 0f;
        return Mathf.Max(0f, damage);
    }

    /// <summary>Sets the suffocation damage (per hit, points or percent of max health) and the seconds between hits.</summary>
    public void SetSuffocation(float damagePerHit, float intervalSeconds = 1f, bool percentOfMaxHealth = false)
    {
        oxygenDepletedHealthDrainRate = Mathf.Max(0f, damagePerHit);
        suffocationInterval = Mathf.Max(0.1f, intervalSeconds);
        suffocationPercentOfMaxHealth = percentOfMaxHealth;
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