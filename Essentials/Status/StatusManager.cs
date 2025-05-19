using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public abstract class StatusManager : MonoBehaviour
{

    [SerializeField] protected float currentValue;
    [SerializeField] protected float maxValue = 100;
    [SerializeField] protected float tickRate = 0.5f;
    [SerializeField] protected float consumingTickRate = 0.5f;
    [SerializeField] protected float maxValueModifier = 15;

    [SerializeField] protected float incrementValue = 1f; // The amount to increment the value by each tick
    [SerializeField] protected float decrementValue = 1f; // The amount to decrese the value by each tick when it's a decresable stats like hunger
    [SerializeField] protected float incrementFactor = 1f; // The factor by whic h will influence the increment in the value
    [SerializeField] protected float decrementFactor = 1f; // The factor by which will influence the  decrement in the value
    [SerializeField] protected float statusIncrementValue = 1f; // The factor by which the status will increase
    [SerializeField] protected Image uiImage;


    [SerializeField] protected bool shouldConsume; // Should the status be decreasing by each tick
    [SerializeField] protected bool isConsuming;
    [SerializeField] protected bool isRegenerating;

    protected List<CoroutineInfo> effectRoutines = new List<CoroutineInfo>();

    public float CurrentValue => currentValue;
    public float MaxValue { get => maxValue; set => maxValue = value; }
    public bool IsConsuming { get => isConsuming; set => isConsuming = value; }
    public float StatusIncrementValue => statusIncrementValue;

    public void AddCurrentValue(float amount) => currentValue = Mathf.Min(currentValue + ApplyFactor(amount, incrementFactor), maxValue);
    public virtual bool HasEnougCurrentValue(float amount) => currentValue - amount >= 0;
    public void ModifyMaxValue(float amount) => maxValue += amount;
    public void ModifyIncrementValue(float amount) => incrementValue += amount;
    public void ModifyIncrementFactor(float amount) => incrementFactor += amount;
    public void ModifyDecrementFactor(float amount) => decrementFactor += amount;
    public float ApplyFactor(float amount, float factor) => amount * (1.0f + factor / 100.0f);
    protected abstract float AddFactor(float amount);
    protected abstract float RemoveFactor(float amount);
    protected abstract void UpdateStatus();
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
    protected void UpdateBar()
    {
        if (uiImage == null) return;
        uiImage.fillAmount = Mathf.Clamp01(currentValue / maxValue);
    }


    protected void UpdateBar(float value, float maxValue, UnityEngine.UI.Image barImage)
    {
        if (barImage == null)
        {
            Debug.LogError("Bar image cannot be null.");
            return;
        }

        barImage.fillAmount = Mathf.Clamp01(value / maxValue);
    }


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
}