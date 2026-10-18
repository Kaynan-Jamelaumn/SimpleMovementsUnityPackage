using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public abstract class StatusManager : MonoBehaviour
{

    [Tooltip("The value right now (0 .. Max Value).")]
    [SerializeField] protected float currentValue;
    [Tooltip("Highest value.")]
    [SerializeField] protected float maxValue = 100;
    [Tooltip("REGENERATION tick: seconds between two gains. Each tick adds Increment Value (changed by Increment Factor). " +
             "E.g. Tick Rate 0.5 + Increment Value 1 = 2 points per second.")]
    [SerializeField] protected float tickRate = 0.5f;
    [Tooltip("CONSUMPTION tick: seconds between two losses of a status that drains by itself (hunger, thirst, rest). Each tick " +
             "removes Decrement Value (changed by Decrement Factor). E.g. Consuming Tick Rate 5 + Decrement Value 1 = 1 point every 5 s " +
             "(12 per minute).")]
    [SerializeField] protected float consumingTickRate = 0.5f;
    [SerializeField] protected float maxValueModifier = 15;

    [Tooltip("Points gained per regeneration tick (see Tick Rate), before Increment Factor.")]
    [SerializeField] protected float incrementValue = 1f; // The amount to increment the value by each tick
    [Tooltip("Points lost per consumption tick (see Consuming Tick Rate), before Decrement Factor. The AMOUNT of each loss.")]
    [SerializeField] protected float decrementValue = 1f; // The amount to decrese the value by each tick when it's a decresable stats like hunger
    [Tooltip("PERCENT bonus on every gain (regeneration, food, potions, heals): +50 = gains are 50% bigger, -50 = half. 0 = no change. " +
             "It is a percentage, not a multiplier: 1 means +1%.")]
    [SerializeField] protected float incrementFactor = 0f; // The factor by whic h will influence the increment in the value
    [Tooltip("PERCENT bonus on every loss (drain ticks, damage, costs): +50 = losses are 50% bigger, -50 = half. 0 = no change. " +
             "It is a percentage, not a multiplier: 1 means +1%. Decrement Value says HOW MUCH is lost per tick; this scales it.")]
    [SerializeField] protected float decrementFactor = 0f; // The factor by which will influence the  decrement in the value
    [SerializeField] protected float statusIncrementValue = 1f; // The factor by which the status will increase
    [Tooltip("The image this status fills (legacy bar). The Player HUD assigns its own; leave empty otherwise.")]
    [SerializeField] protected Image uiImage;


    [Tooltip("The status drains by itself every Consuming Tick Rate seconds (hunger, thirst).")]
    [SerializeField] protected bool shouldConsume; // Should the status be decreasing by each tick
    [Tooltip("Read only: the status is losing value right now.")]
    [SerializeField] protected bool isConsuming;
    [Tooltip("Read only: the status is regenerating right now.")]
    [SerializeField] protected bool isRegenerating;

    protected List<CoroutineInfo> effectRoutines = new List<CoroutineInfo>();

    public float CurrentValue => currentValue;

    /// <summary>The image this status fills (its UI bar). The Player HUD gives each status its own.</summary>
    public Image UIImage { get => uiImage; set => uiImage = value; }
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

    /// <summary>Points lost per consumption tick, before <see cref="DecrementFactor"/>.</summary>
    public float DecrementValue => decrementValue;

    /// <summary>Seconds between two regeneration ticks.</summary>
    public float TickRate => tickRate;

    /// <summary>Seconds between two consumption ticks.</summary>
    public float ConsumingTickRate => consumingTickRate;

    /// <summary>Does the status drain by itself (hunger, thirst)?</summary>
    public bool ShouldConsume { get => shouldConsume; set => shouldConsume = value; }

    /// <summary>The value as a fraction of the max (0-1).</summary>
    public float Fraction => maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) : 0f;

    /// <summary>Is the status regenerating right now?</summary>
    public bool IsRegenerating => isRegenerating;

    // ------------------------------------------------------------------ drain rate multipliers
    private readonly Dictionary<object, float> rateMultipliers = new Dictionary<object, float>();
    private float rateMultiplier = 1f;

    /// <summary>
    /// How fast the status drains by itself right now: the product of every multiplier set with
    /// <see cref="SetRateMultiplier"/> (cold makes you hungry faster, sleeping slows thirst...). 1 = normal.
    /// </summary>
    public float RateMultiplier => rateMultiplier;

    /// <summary>
    /// Sets one named multiplier of the drain speed (1 = no change, 1.5 = 50% faster, 0 = stops it). The same
    /// <paramref name="key"/> replaces its previous value; <see cref="ClearRateMultiplier"/> removes it. Never stacks by
    /// accident: each source owns one key.
    /// </summary>
    public void SetRateMultiplier(object key, float multiplier)
    {
        if (key == null)
            return;
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            multiplier = 1f;
        multiplier = Mathf.Max(0f, multiplier);
        if (Mathf.Approximately(multiplier, 1f))
        {
            ClearRateMultiplier(key);
            return;
        }
        rateMultipliers[key] = multiplier;
        RecomputeRate();
    }

    /// <summary>Removes the drain multiplier of <paramref name="key"/>.</summary>
    public void ClearRateMultiplier(object key)
    {
        if (key != null && rateMultipliers.Remove(key))
            RecomputeRate();
    }

    private void RecomputeRate()
    {
        float m = 1f;
        foreach (float v in rateMultipliers.Values)
            m *= v;
        rateMultiplier = m;
    }

    /// <summary>
    /// Sets the value to <paramref name="fraction"/> (0-1) of the max, without factors (a respawn, a full rest).
    /// Ignored on a client's copy (the server's value arrives through replication).
    /// </summary>
    public virtual void ResetForRespawn(float fraction)
    {
        if (IsReplica)
            return;
        float f = float.IsNaN(fraction) ? 1f : Mathf.Clamp01(fraction);
        currentValue = maxValue * f;
        isConsuming = false;
    }

    public virtual void AddCurrentValue(float amount)
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
            effectReverts.Clear();
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

    // Undo actions of running timed effects (factor and regeneration changes), so an effect that is replaced, removed or
    // stopped early is reverted exactly. Before, a replaced effect never ran its end and left its change forever.
    private readonly Dictionary<CoroutineInfo, Action> effectReverts = new Dictionary<CoroutineInfo, Action>();
    private Action pendingEffectRevert;

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

        pendingEffectRevert = null;
        IEnumerator routine = effectRoutineFunc(effectName, amount, duration, tickInterval, isProcedural, isStackable);
        Action revert = pendingEffectRevert;
        pendingEffectRevert = null;
        var info = new CoroutineInfo
        {
            effectName = effectName,
            isBuff = amount > 0
        };
        effectList.Add(info);
        if (revert != null)
            effectReverts[info] = revert;
        info.coroutine = StartCoroutine(Tracked(effectList, info, routine));
    }

    // Runs an effect and forgets it when it ends by itself (its own end already reverted it).
    private IEnumerator Tracked(List<CoroutineInfo> list, CoroutineInfo info, IEnumerator routine)
    {
        while (routine.MoveNext())
            yield return routine.Current;
        effectReverts.Remove(info);
        list.Remove(info);
    }

    /// <summary>
    /// For effect routines that change a field for a while (a factor, a regeneration amount): call it while building the
    /// routine (before returning it). It adds the amount, tracks exactly what was added, and takes it away when the effect
    /// ends, is replaced, removed or stopped.
    /// </summary>
    protected IEnumerator ModifierEffect(string effectName, float amount, float duration, float tickInterval, bool isProcedural,
        bool isStackable, Action<float> add)
    {
        float applied = 0f;
        Action revert = () =>
        {
            if (applied != 0f) add(-applied);
            applied = 0f;
        };
        pendingEffectRevert = revert;
        return ApplyEffectRoutine(effectName, amount, duration, tickInterval, isProcedural, isStackable,
            perTick => { add(perTick); applied += perTick; },
            total => { add(total); applied += total; },
            _ => revert());
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
        foreach (var effect in effectList.ToList())
            StopAndRemoveEffect(effectList, effect);
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
        if (effect.coroutine != null)
            StopCoroutine(effect.coroutine);
        effectList.Remove(effect);
        if (effectReverts.TryGetValue(effect, out Action revert))
        {
            effectReverts.Remove(effect);
            revert?.Invoke();
        }
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