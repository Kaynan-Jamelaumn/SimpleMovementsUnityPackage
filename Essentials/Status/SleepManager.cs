using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SleepManager : StatusManager
{
    [Header("Sleep-related fields")]
    [SerializeField] private float sleepDeprivationThreshold = 30f;
    [SerializeField] private float moderateSleepinessThreshold = 60f;
    [SerializeField] private float extremeSleepinessThreshold = 85f;

    [SerializeField] private float sleepDeprivationSpeedPenalty = 0.8f; // 20% speed reduction
    [SerializeField] private float moderateStaminaRegenPenalty = 0.7f; // 30% stamina regen reduction
    [SerializeField] private float moderateSanityDrainRate = 1f;
    [SerializeField] private float extremeSanityDrainRate = 3f;
    [SerializeField] private float extremeCollapseThreshold = 95f;
    [SerializeField] private float forcedNapDuration = 30f;
    [SerializeField] private float forcedNapRecoveryRate = 0.5f; // Poor sleep quality

    [SerializeField] private bool isSleeping = false;
    [SerializeField] private bool hasCollapsed = false;

    private Coroutine sleepConsumptionRoutine;
    private Coroutine sleepEffectsRoutine;
    private Coroutine forcedNapRoutine;
    private List<CoroutineInfo> sleepEffectRoutines = new List<CoroutineInfo>();

    // References to other managers
    [SerializeField] private SpeedManager speedManager;
    [SerializeField] private StaminaManager staminaManager;
    [SerializeField] private SanityManager sanityManager;

    public bool IsSleeping => isSleeping;
    public bool HasCollapsed => hasCollapsed;
    public SleepinessLevel CurrentSleepinessLevel => GetSleepinessLevel();
    public List<CoroutineInfo> SleepEffectRoutines { get => sleepEffectRoutines; set => sleepEffectRoutines = value; }

    public enum SleepinessLevel
    {
        Rested,
        SlightlySleepy,
        ModeratelySleepy,
        ExtremelySleepy
    }

    private void Awake()
    {
        speedManager = this.CheckComponent(speedManager, nameof(speedManager));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
        sanityManager = this.CheckComponent(sanityManager, nameof(sanityManager));

    }

    private void Start()
    {
        currentValue = maxValue;
        sleepConsumptionRoutine = StartCoroutine(SleepConsumptionRoutine());
        sleepEffectsRoutine = StartCoroutine(SleepEffectsRoutine());
    }

    private void Update()
    {
        UpdateSleepBar();

        // Check for forced collapse
        if (currentValue >= extremeCollapseThreshold && !hasCollapsed && !isSleeping)
        {
            ForceCollapse();
        }
    }

    public void UpdateSleepBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public SleepinessLevel GetSleepinessLevel()
    {
        if (currentValue < sleepDeprivationThreshold)
            return SleepinessLevel.Rested;
        else if (currentValue < moderateSleepinessThreshold)
            return SleepinessLevel.SlightlySleepy;
        else if (currentValue < extremeSleepinessThreshold)
            return SleepinessLevel.ModeratelySleepy;
        else
            return SleepinessLevel.ExtremelySleepy;
    }

    public void StartSleeping()
    {
        if (isSleeping) return;

        isSleeping = true;
        isConsuming = false;

        if (sleepConsumptionRoutine != null)
            StopCoroutine(sleepConsumptionRoutine);

        StartCoroutine(SleepRecoveryRoutine());
    }

    public void StopSleeping()
    {
        if (!isSleeping) return;

        isSleeping = false;
        isConsuming = true;
        hasCollapsed = false;

        sleepConsumptionRoutine = StartCoroutine(SleepConsumptionRoutine());
    }

    private void ForceCollapse()
    {
        hasCollapsed = true;
        forcedNapRoutine = StartCoroutine(ForcedNapRoutine());
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddSleepEffect(string effectName, float sleepAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SleepEffectRoutines, effectName, sleepAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySleepEffectRoutine);

    public void RemoveSleepEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(SleepEffectRoutines, effectName, effectRoutine);

    public void StopAllSleepEffects()
        => StopAllEffects(SleepEffectRoutines);

    public void StopAllSleepEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(SleepEffectRoutines, isBuff);

    // Coroutines
    private IEnumerator SleepConsumptionRoutine()
    {
        while (!isSleeping)
        {
            currentValue += decrementValue;
            yield return new WaitForSeconds(tickRate);
        }
    }

    private IEnumerator SleepRecoveryRoutine()
    {
        while (isSleeping && currentValue > 0)
        {
            currentValue -= incrementValue;
            yield return new WaitForSeconds(tickRate);
        }

        if (currentValue <= 0)
        {
            StopSleeping();
        }
    }

    private IEnumerator ForcedNapRoutine()
    {
        isSleeping = true;
        isConsuming = false;

        float napTimer = 0f;
        while (napTimer < forcedNapDuration)
        {
            currentValue -= incrementValue * forcedNapRecoveryRate;
            napTimer += tickRate;
            yield return new WaitForSeconds(tickRate);
        }

        StopSleeping();
    }

    private IEnumerator SleepEffectsRoutine()
    {
        while (true)
        {
            ApplySleepEffects();
            yield return new WaitForSeconds(1f); // Check effects every second
        }
    }

    private void ApplySleepEffects()
    {
        SleepinessLevel level = GetSleepinessLevel();

        switch (level)
        {
            case SleepinessLevel.SlightlySleepy:
                ApplySpeedPenalty();
                break;

            case SleepinessLevel.ModeratelySleepy:
                ApplySpeedPenalty();
                ApplyStaminaRegenPenalty();
                ApplySanityDrain(moderateSanityDrainRate);
                break;

            case SleepinessLevel.ExtremelySleepy:
                ApplySpeedPenalty();
                ApplyStaminaRegenPenalty();
                ApplySanityDrain(extremeSanityDrainRate);
                break;
        }
    }

    private void ApplySpeedPenalty()
    {
        if (speedManager != null)
        {
            // This would need to be implemented as a temporary modifier
            // For now, we'll add a speed debuff effect
            speedManager.AddSpeedFactorEffect("SleepDeprivation", -20f, 2f, 1f, false, false);
        }
    }

    private void ApplyStaminaRegenPenalty()
    {
        if (staminaManager != null)
        {
            staminaManager.AddStaminaHealFactorEffect("SleepStaminaPenalty", -30f, 2f, 1f, false, false);
        }
    }

    private void ApplySanityDrain(float drainRate)
    {
        if (sanityManager != null)
        {
            sanityManager.ConsumeSanity(drainRate * Time.deltaTime);
        }
    }

    public IEnumerator ApplySleepEffectRoutine(string effectName, float sleepAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sleepAmount,
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