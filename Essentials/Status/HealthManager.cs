using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthManager : StatusManager
{
    [Header("Health-related fields")]
    [SerializeField] private float healthRegenCooldownDuration = 15;
    private float healthRegenCooldownTimer = -15;

    private Coroutine hpRegenerationRoutine;
    private List<CoroutineInfo> hpEffectRoutines = new List<CoroutineInfo>();
    public List<CoroutineInfo> HpEffectRoutines { get => hpEffectRoutines; set => hpEffectRoutines = value; }

    private void Start()
    {
        currentValue = maxValue;
    }

    private void Update()
    {
        if (!isRegenerating && currentValue < maxValue &&  Time.time - healthRegenCooldownTimer > healthRegenCooldownDuration)
        {
            healthRegenCooldownTimer = Time.time;
            isRegenerating = true;
            hpRegenerationRoutine = StartCoroutine(RegenerateHpRoutine());
        }

    }
    public void UpdateHpBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }
    public void ConsumeHP(float amount, bool scapeBounds= false)
    {
        if (currentValue <= 0) return;
        healthRegenCooldownTimer = Time.time;
        if (scapeBounds) currentValue -= amount;
        else currentValue -= RemoveFactor(amount);
        isConsuming = true;
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    public void AddHpEffect(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false) 
        => AddEffect(HpEffectRoutines, effectName, hpAmount,timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpEffectRoutine);

    public void AddHpHealFactorEffect(string effectName, float hpHealFactorAmount, float timeBuffEffect,float tickCooldown, bool isProcedural = false,  bool isStackable = false)
        => AddEffect(HpEffectRoutines, effectName, hpHealFactorAmount, timeBuffEffect,tickCooldown, isProcedural, isStackable, ApplyHpHealFactorEffectRoutine);
    public void AddHpDamageFactorEffect(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(HpEffectRoutines, effectName, hpDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpDamageFactorEffectRoutine);
    public void AddHpRegenEffect(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false) 
        => AddEffect(HpEffectRoutines, effectName, hpRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpRegenEffectRoutine);

    public void RemoveHpEffect(string effectName = null, Coroutine effectRoutine = null)
     => RemoveEffect(HpEffectRoutines, effectName, effectRoutine);

    public void StopAllHpEffects()
        => StopAllEffects(HpEffectRoutines);

    public void StopAllHpEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(HpEffectRoutines, isBuff);

    public void StopHpRegeneration()
        => isConsuming = true;
    public IEnumerator RegenerateHpRoutine()
    {
        // Check if stamina consumption is active, exit coroutine if true
        if (isConsuming)
        {
            isRegenerating = false;
            yield break; // Exit the coroutine
        }

        isRegenerating = true;

        // Regenerate stamina while it is below the maximum
        while (currentValue < maxValue)
        {
            currentValue += incrementValue;

            // Check the flag and exit the coroutine if needed
            if (isConsuming)
            {
                isConsuming = false;
                isRegenerating = false;
                yield break;
            }

            yield return new WaitForSeconds(tickRate); // Delay between regen updates
        }

        isRegenerating = false;
    }

    // Example refactored coroutines
    public IEnumerator ApplyHpEffectRoutine(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            hpAmount,
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
            _ => currentValue = Mathf.Min(currentValue, maxValue)
        );
    }

    public IEnumerator ApplyHpHealFactorEffectRoutine(string effectName, float hpHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            hpHealFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor += total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplyHpDamageFactorEffectRoutine(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            hpDamageFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => decrementFactor += perTick,
            total => decrementFactor = total,
            original => decrementFactor -= original
        );
    }

    public IEnumerator ApplyHpRegenEffectRoutine(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            hpRegenAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementValue += perTick,
            total => incrementValue = total,
            original => incrementValue -= original
        );
    }
}
