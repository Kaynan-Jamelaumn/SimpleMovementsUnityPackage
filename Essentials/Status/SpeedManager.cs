using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpeedManager : MonoBehaviour
{
    [Header("Speed Configuration")]
    [SerializeField] protected float currentSpeed;
    [SerializeField] protected float baseSpeed = 10f;
    [SerializeField] protected float speedWhileRunningMultiplier = 2f;
    [SerializeField] protected float speedWhileCrouchingMultiplier = 0.5f;
    [SerializeField] protected float incrementFactor = 15f;
    [SerializeField] protected float decrementFactor = 15f;
    [SerializeField] protected float statusIncrementValue = 1f;

    private List<CoroutineInfo> speedEffectRoutines = new List<CoroutineInfo>();

    // Properties
    public float Speed => currentSpeed;
    public float BaseSpeed { get => baseSpeed; set => baseSpeed = value; }
    public float SpeedWhileRunningMultiplier { get => speedWhileRunningMultiplier; set => speedWhileRunningMultiplier = value; }
    public float SpeedWhileCrouchingMultiplier { get => speedWhileCrouchingMultiplier; set => speedWhileCrouchingMultiplier = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float DecrementFactor { get => decrementFactor; set => decrementFactor = value; }
    public float StatusIncrementValue => statusIncrementValue;
    public List<CoroutineInfo> SpeedEffectRoutines { get => speedEffectRoutines; set => speedEffectRoutines = value; }

    private void Start()
    {
        // Initialize current speed to base speed
        currentSpeed = baseSpeed;
    }

    // Core speed methods
    public void ModifySpeed(float amount)
    {
        currentSpeed = Mathf.Max(0, currentSpeed + amount);
    }

    public void ModifyBaseSpeed(float amount)
    {
        baseSpeed = Mathf.Max(0, baseSpeed + amount);
        currentSpeed = Mathf.Max(0, currentSpeed + amount); // Also adjust current speed
    }

    public void SetSpeed(float newSpeed)
    {
        currentSpeed = Mathf.Max(0, newSpeed);
    }

    public void ResetToBaseSpeed()
    {
        currentSpeed = baseSpeed;
    }

    public float GetSpeedWithMultiplier(float multiplier)
    {
        return currentSpeed * multiplier;
    }

    // Factor application methods
    public void ModifyIncrementFactor(float amount) => incrementFactor += amount;
    public void ModifyDecrementFactor(float amount) => decrementFactor += amount;
    public float ApplyFactor(float amount, float factor) => amount * (1.0f + factor / 100.0f);
    protected float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect system methods
    public void AddSpeedEffect(string effectName, float speedAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SpeedEffectRoutines, effectName, speedAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySpeedEffectRoutine);

    public void AddSpeedFactorEffect(string effectName, float speedFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SpeedEffectRoutines, effectName, speedFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySpeedFactorEffectRoutine);

    public void AddSpeedMultiplierEffect(string effectName, float multiplierAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SpeedEffectRoutines, effectName, multiplierAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySpeedMultiplierEffectRoutine);

    public void RemoveSpeedEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(SpeedEffectRoutines, effectName, effectRoutine);

    public void StopAllSpeedEffects()
        => StopAllEffects(SpeedEffectRoutines);

    public void StopAllSpeedEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(SpeedEffectRoutines, isBuff);

    // Effect management methods (copied from StatusManager pattern)
    protected void AddEffect(
        List<CoroutineInfo> effectList,
        string effectName,
        float amount,
        float duration,
        float tickInterval,
        bool isProcedural = false,
        bool isStackable = false,
        Func<string, float, float, float, bool, bool, IEnumerator> effectRoutineFunc = null
    )
    {
        if (effectRoutineFunc == null)
        {
            Debug.LogError("Effect routine function cannot be null.");
            return;
        }

        if (!isStackable)
        {
            var existingEffect = effectList.FirstOrDefault(e => e.effectName == effectName);
            if (existingEffect != null)
            {
                StopAndRemoveEffect(effectList, existingEffect);
            }
        }

        Coroutine effectCoroutine = StartCoroutine(effectRoutineFunc(effectName, amount, duration, tickInterval, isProcedural, isStackable));
        effectList.Add(new CoroutineInfo
        {
            coroutine = effectCoroutine,
            effectName = effectName,
            isBuff = amount > 0
        });
    }

    protected void RemoveEffect(List<CoroutineInfo> effectList, string effectName = null, Coroutine effectRoutine = null)
    {
        var effectToRemove = effectList.FirstOrDefault(e =>
            (effectName != null && e.effectName == effectName) ||
            (effectRoutine != null && e.coroutine == effectRoutine));

        if (effectToRemove != null)
        {
            StopAndRemoveEffect(effectList, effectToRemove);
        }
    }

    protected void StopAllEffects(List<CoroutineInfo> effectList)
    {
        foreach (var effect in effectList)
        {
            StopCoroutine(effect.coroutine);
        }
        effectList.Clear();
    }

    public void StopAllEffectsByType(List<CoroutineInfo> effectList, bool isBuff)
    {
        var effectsToStop = effectList.Where(e => e.isBuff == isBuff).ToList();
        foreach (var effect in effectsToStop)
        {
            StopAndRemoveEffect(effectList, effect);
        }
    }

    private void StopAndRemoveEffect(List<CoroutineInfo> effectList, CoroutineInfo effect)
    {
        StopCoroutine(effect.coroutine);
        effectList.Remove(effect);
    }

    // Effect routine implementations
    protected IEnumerator ApplyEffectRoutine(
        string effectName,
        float originalAmount,
        float duration,
        float tickInterval,
        bool isProcedural,
        bool isStackable,
        Action<float> applyPerTick,
        Action<float> applyTotal,
        Action<float> finalAction
    )
    {
        float adjustedAmount = isProcedural ? originalAmount / (duration / tickInterval) : originalAmount;
        float startTime = Time.time;

        if (isProcedural)
        {
            while (Time.time < startTime + duration)
            {
                applyPerTick?.Invoke(adjustedAmount);
                yield return new WaitForSeconds(tickInterval);
            }
        }
        else
        {
            applyTotal?.Invoke(adjustedAmount);
            yield return new WaitForSeconds(duration);
        }

        finalAction?.Invoke(originalAmount);
    }

    public IEnumerator ApplySpeedEffectRoutine(string effectName, float speedAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            speedAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick =>
            {
                float newAmount = perTick > 0 ? AddFactor(perTick) : RemoveFactor(perTick);
                currentSpeed = Mathf.Max(0, currentSpeed + newAmount);
            },
            total =>
            {
                float newAmount = total > 0 ? AddFactor(total) : RemoveFactor(total);
                currentSpeed = Mathf.Max(0, currentSpeed + newAmount);
            },
            _ => currentSpeed = Mathf.Max(0, currentSpeed)
        );
    }

    public IEnumerator ApplySpeedFactorEffectRoutine(string effectName, float speedFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            speedFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor += total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplySpeedMultiplierEffectRoutine(string effectName, float multiplierAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            multiplierAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => speedWhileRunningMultiplier += perTick,
            total => speedWhileRunningMultiplier += total,
            original => speedWhileRunningMultiplier -= original
        );
    }
}