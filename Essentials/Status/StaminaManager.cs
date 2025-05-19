using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaminaManager : StatusManager
{
    [Header("Stamina-related fields")]
    private Coroutine staminaRegenerationRoutine;

    private List<CoroutineInfo> staminaEffectRoutines = new List<CoroutineInfo>();
    public Coroutine StaminaRegenerationRoutine { get => staminaRegenerationRoutine; set => staminaRegenerationRoutine = value; }
    public List<CoroutineInfo> StaminaEffectRoutines { get => staminaEffectRoutines; set => staminaEffectRoutines = value; }
    private void Start()
    {
        currentValue = maxValue;
    }
    private void Update()
    {
        if (!isRegenerating && currentValue < maxValue && !isConsuming)
        {
            isRegenerating = true;
            StaminaRegenerationRoutine = StartCoroutine(RegenerateStaminaRoutine());
        }

    }
    public void UpdateStaminaBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }
    public void ConsumeStamina(float amount)
    {
        if (currentValue <= 0) return;
        currentValue -= RemoveFactor(amount);
    }
    public override bool HasEnougCurrentValue(float amount) => currentValue - amount >= 1;

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Method to signal the regeneration coroutine to stop
    public void StopStaminaRegeneration() => isConsuming = true;


    public void AddStaminaEffect(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(StaminaEffectRoutines, effectName, staminaAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaEffectRoutine);
    public void AddStaminaHealFactorEffect(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(StaminaEffectRoutines, effectName, staminaHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaHealFactorEffectRoutine);
    public void AddStaminaDamageFactorEffect(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(StaminaEffectRoutines, effectName, staminaDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaDamageFactorEffectRoutine);
    public void AddStaminaRegenEffect(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(StaminaEffectRoutines, effectName, staminaRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaRegenEffectRoutine);
    public void RemoveStaminaEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(StaminaEffectRoutines, effectName, effectRoutine);
    }

    public void StopAllStaminaEffects()
    {
        StopAllEffects(StaminaEffectRoutines);
    }

    public void StopAllStaminaEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(StaminaEffectRoutines, isBuff);
    }

    public IEnumerator RegenerateStaminaRoutine()
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

    // Coroutine to consume stamina over time
    public IEnumerator ConsumeStaminaRoutine(float amount = 0, float staminaTickConsuption = 0.5f)
    {
        // Signal the regeneration coroutine to stop
        StopStaminaRegeneration();
        while (true) {
            currentValue -= amount;
            if (currentValue - amount < 1)
            {
                isConsuming = false;
                yield break;
            }
            yield return new WaitForSeconds(staminaTickConsuption);
        }
    }
    public IEnumerator ApplyStaminaEffectRoutine(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            staminaAmount,
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
    public IEnumerator ApplyStaminaHealFactorEffectRoutine(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            staminaHealFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor = total,
            original => incrementFactor -= original
        );
    }
    public IEnumerator ApplyStaminaDamageFactorEffectRoutine(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            staminaDamageFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            // Note: Original code uses incrementFactor in procedural case and decrementFactor in total case
            perTick => incrementFactor += perTick,
            total => decrementFactor = total,
            original => decrementFactor -= original
        );
    }
    public IEnumerator ApplyStaminaRegenEffectRoutine(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            staminaRegenAmount,
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