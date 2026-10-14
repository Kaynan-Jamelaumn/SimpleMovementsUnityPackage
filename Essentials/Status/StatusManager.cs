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
    public float MaxValue
    {
        get => maxValue;
        set
        {
            if (LimitsLocked)
                return; // the server decides the limits of this copy
            maxValue = value;
        }
    }
    public bool IsConsuming { get => isConsuming; set => isConsuming = value; }
    public float StatusIncrementValue => statusIncrementValue;

    /// <summary>Amount restored per regeneration tick, before factors.</summary>
    public float IncrementValue => incrementValue;

    /// <summary>Percent bonus applied to gains (healing received, restores).</summary>
    public float IncrementFactor => incrementFactor;

    /// <summary>Percent bonus applied to losses (damage taken, costs).</summary>
    public float DecrementFactor => decrementFactor;

    public void AddCurrentValue(float amount)
    {
        if (IsReplica)
            return; // the server's value arrives through replication
        currentValue = Mathf.Min(currentValue + ApplyFactor(amount, incrementFactor), maxValue);
    }

    /// <summary>
    /// Sets the value directly (clamped to 0..max), bypassing factors. For the network layer: a client applies the
    /// value the server decided; client-side prediction restores a saved value.
    /// </summary>
    public void SetCurrentValue(float value) => currentValue = Mathf.Clamp(value, 0f, maxValue);
    public virtual bool HasEnougCurrentValue(float amount) => currentValue - amount >= 0;

    public void ModifyMaxValue(float amount)
    {
        if (LimitsLocked)
            return;
        maxValue += amount;
    }

    public void ModifyIncrementValue(float amount)
    {
        if (LimitsLocked)
            return;
        incrementValue += amount;
    }

    public void ModifyIncrementFactor(float amount)
    {
        if (LimitsLocked)
            return;
        incrementFactor += amount;
    }

    public void ModifyDecrementFactor(float amount)
    {
        if (LimitsLocked)
            return;
        decrementFactor += amount;
    }

    public float ApplyFactor(float amount, float factor) => amount * (1.0f + factor / 100.0f);
    protected abstract float AddFactor(float amount);
    protected abstract float RemoveFactor(float amount);
    protected abstract void UpdateStatus();

    // ------------------------------------------------------------------ network replica

    /// <summary>
    /// True on a client's copy of a networked character: the server simulates this status and sends its values, so
    /// this copy runs no regeneration, decay or timed effects, and gameplay writes to it (consume, restore, max value,
    /// factors, effects) are ignored. Only <see cref="ApplyReplicated"/> and <see cref="SetCurrentValue"/> change it.
    /// Always false offline and on the server or host.
    /// </summary>
    public bool IsReplica { get; private set; }

    /// <summary>
    /// True when only the limits (max value, regeneration amount and factors) come from the server while the current
    /// value is still simulated on this machine - the owning client predicts its stamina and the movement reconcile
    /// corrects it. Writes to the limits and new timed effects are ignored; consuming and restoring still work.
    /// </summary>
    public bool HasReplicatedLimits { get; private set; }

    /// <summary>The max value, factors and timed effects are decided by the server (replica or replicated limits).</summary>
    protected bool LimitsLocked => IsReplica || HasReplicatedLimits;

    /// <summary>
    /// Switches replica mode (network layer). On: every running routine and timed effect stops, so nothing changes the
    /// value until the server's next one arrives. Off: the simulation runs on this machine again.
    /// </summary>
    public void SetReplica(bool on)
    {
        if (IsReplica == on)
            return;
        IsReplica = on;
        if (on)
        {
            StopAllCoroutines();
            isRegenerating = false;
            isConsuming = false;
        }
        OnReplicaChanged(on);
    }

    /// <summary>
    /// Takes the limits from the server while the current value stays simulated here (see
    /// <see cref="HasReplicatedLimits"/>). Network layer only.
    /// </summary>
    public void SetReplicatedLimits(bool on) => HasReplicatedLimits = on;

    /// <summary>
    /// Applies the value the server decided: the max first, then the current value, so the clamp never cuts a value
    /// that only fits under the new max. Non-finite values are ignored.
    /// </summary>
    public void ApplyReplicated(float current, float max)
    {
        if (!IsFinite(current) || !IsFinite(max))
            return;
        maxValue = Mathf.Max(0f, max);
        currentValue = ClampReplicated(current);
    }

    /// <summary>
    /// Applies the server's limits (max value, regeneration amount, gain and loss factors) and keeps the current value
    /// within the new max. Used with <see cref="HasReplicatedLimits"/>; non-finite values are ignored.
    /// </summary>
    public void ApplyReplicatedLimits(float max, float increment, float gainFactor, float lossFactor)
    {
        if (!IsFinite(max) || !IsFinite(increment) || !IsFinite(gainFactor) || !IsFinite(lossFactor))
            return;
        maxValue = Mathf.Max(0f, max);
        incrementValue = increment;
        incrementFactor = gainFactor;
        decrementFactor = lossFactor;
        if (currentValue > maxValue)
            currentValue = maxValue;
    }

    /// <summary>How a replicated current value is kept in range (0..max by default).</summary>
    protected virtual float ClampReplicated(float value) => Mathf.Clamp(value, 0f, maxValue);

    /// <summary>
    /// Called after replica mode changed. Subclasses forget routine handles that were stopped when it turned on, and
    /// restart their endless routines when it turns off again after Start.
    /// </summary>
    protected virtual void OnReplicaChanged(bool on) { }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

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
        // Timed effects of a networked copy run on the server only; their result arrives through replication.
        if (LimitsLocked)
            return;
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
            //Debug.LogError("Bar image cannot be null.");
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