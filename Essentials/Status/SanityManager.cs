using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SanityManager : StatusManager
{
    [Header("Sanity-related fields")]
    [SerializeField] private float lowSanityThreshold = 30f;
    [SerializeField] private float criticalSanityThreshold = 10f;

    [SerializeField] private float lowSanitySpeedPenalty = 0.9f; // 10% speed reduction
    [SerializeField] private float lowSanityStaminaRegenPenalty = 0.8f; // 20% stamina regen reduction
    [SerializeField] private float criticalSanitySpeedPenalty = 0.7f; // 30% speed reduction
    [SerializeField] private float criticalSanityStaminaRegenPenalty = 0.5f; // 50% stamina regen reduction

    [SerializeField] private float sanityRegenRate = 0.1f;
    [SerializeField] private bool shouldRegenerateSanity = true;

    private Coroutine sanityRegenerationRoutine;
    private Coroutine sanityEffectsRoutine;
    private List<CoroutineInfo> sanityEffectRoutines = new List<CoroutineInfo>();

    // References to other managers
    [SerializeField] private SpeedManager speedManager;
    [SerializeField] private StaminaManager staminaManager;

    public SanityLevel CurrentSanityLevel => GetSanityLevel();
    public List<CoroutineInfo> SanityEffectRoutines { get => sanityEffectRoutines; set => sanityEffectRoutines = value; }

    public enum SanityLevel
    {
        Stable,
        Stressed,
        Unstable,
        Critical
    }

    private void Awake()
    {
        speedManager = this.CheckComponent(speedManager, nameof(speedManager));
        staminaManager = this.CheckComponent(staminaManager, nameof(staminaManager));
    }

    private void Start()
    {
        currentValue = maxValue;

        if (shouldRegenerateSanity)
        {
            sanityRegenerationRoutine = StartCoroutine(SanityRegenerationRoutine());
        }

        sanityEffectsRoutine = StartCoroutine(SanityEffectsRoutine());
    }

    private void Update()
    {
        UpdateSanityEffects();
    }

    public void UpdateSanityBar()
    {
        // Sanity doesn't require a UI icon as per requirements
        // But we keep the method for consistency and potential future use
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public SanityLevel GetSanityLevel()
    {
        float percentage = (currentValue / maxValue) * 100f;

        if (percentage > lowSanityThreshold)
            return SanityLevel.Stable;
        else if (percentage > criticalSanityThreshold)
            return SanityLevel.Stressed;
        else if (percentage > 0)
            return SanityLevel.Unstable;
        else
            return SanityLevel.Critical;
    }

    public void ConsumeSanity(float amount)
    {
        if (currentValue <= 0) return;
        currentValue -= RemoveFactor(amount);
        currentValue = Mathf.Max(0, currentValue);
    }

    public void RestoreSanity(float amount)
    {
        currentValue += AddFactor(amount);
        currentValue = Mathf.Min(maxValue, currentValue);
    }

    protected override void UpdateStatus() => UpdateSanityBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddSanityEffect(string effectName, float sanityAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityEffectRoutine);

    public void AddSanityHealFactorEffect(string effectName, float sanityHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityHealFactorEffectRoutine);

    public void AddSanityDamageFactorEffect(string effectName, float sanityDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityDamageFactorEffectRoutine);

    public void RemoveSanityEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(SanityEffectRoutines, effectName, effectRoutine);

    public void StopAllSanityEffects()
        => StopAllEffects(SanityEffectRoutines);

    public void StopAllSanityEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(SanityEffectRoutines, isBuff);

    private void UpdateSanityEffects()
    {
        SanityLevel level = GetSanityLevel();

        // Apply effects based on sanity level
        switch (level)
        {
            case SanityLevel.Stressed:
                ApplyLowSanityEffects();
                break;

            case SanityLevel.Unstable:
                ApplyLowSanityEffects();
                break;

            case SanityLevel.Critical:
                ApplyCriticalSanityEffects();
                break;
        }
    }

    private void ApplyLowSanityEffects()
    {
        // Apply temporary debuffs for low sanity
        if (speedManager != null)
        {
            speedManager.AddSpeedFactorEffect("LowSanity", -10f, 2f, 1f, false, false);
        }

        if (staminaManager != null)
        {
            staminaManager.AddStaminaHealFactorEffect("LowSanity", -20f, 2f, 1f, false, false);
        }
    }

    private void ApplyCriticalSanityEffects()
    {
        // Apply severe debuffs for critical sanity
        if (speedManager != null)
        {
            speedManager.AddSpeedFactorEffect("CriticalSanity", -30f, 2f, 1f, false, false);
        }

        if (staminaManager != null)
        {
            staminaManager.AddStaminaHealFactorEffect("CriticalSanity", -50f, 2f, 1f, false, false);
        }
    }

    // Coroutines
    private IEnumerator SanityRegenerationRoutine()
    {
        while (true)
        {
            if (currentValue < maxValue && GetSanityLevel() == SanityLevel.Stable)
            {
                currentValue += sanityRegenRate;
                currentValue = Mathf.Min(maxValue, currentValue);
            }

            yield return new WaitForSeconds(tickRate);
        }
    }

    private IEnumerator SanityEffectsRoutine()
    {
        while (true)
        {
            // Check for environmental or situational sanity effects
            yield return new WaitForSeconds(2f);
        }
    }

    public IEnumerator ApplySanityEffectRoutine(string effectName, float sanityAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sanityAmount,
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

    public IEnumerator ApplySanityHealFactorEffectRoutine(string effectName, float sanityHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sanityHealFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor += total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplySanityDamageFactorEffectRoutine(string effectName, float sanityDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sanityDamageFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => decrementFactor += perTick,
            total => decrementFactor += total,
            original => decrementFactor -= original
        );
    }
}