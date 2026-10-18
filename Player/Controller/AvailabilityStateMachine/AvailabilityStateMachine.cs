using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine;

[System.Flags]
public enum EStatusEffect
{
    None = 0,
    Silenced = 1 << 0,
    Stunned = 1 << 1,
    // Add more status effects as needed
    Poisoned = 1 << 2,
    Slowed = 1 << 3,
    /// <summary>Asleep or unconscious: cannot move or act (like a stun), but it is not crowd control.</summary>
    Incapacitated = 1 << 4
}


[System.Serializable]
public class StatusEffectData
{
    public EStatusEffect effect;
    public float remainingDuration;
    public float originalDuration;

    public StatusEffectData(EStatusEffect effect, float duration)
    {
        this.effect = effect;
        this.remainingDuration = duration;
        this.originalDuration = duration;
    }
}

public class AvailabilityStateMachine : StateManager<EAvailabilityState>
{

    // Status effects system
    private Dictionary<EStatusEffect, StatusEffectData> activeStatusEffects = new Dictionary<EStatusEffect, StatusEffectData>();
    private EStatusEffect currentStatusFlags = EStatusEffect.None;

    // Events
    public event System.Action<EStatusEffect> OnStatusEffectAdded;
    public event System.Action<EStatusEffect> OnStatusEffectRemoved;
    public event System.Action<EAvailabilityState, EAvailabilityState> OnStateChanged;

    private AvailabilityContext context;

    [Tooltip("Log applied/removed status effects to the Console.")]
    [SerializeField] private bool debugLog = false;

    private void Awake()
    {

        context = new AvailabilityContext();
        InitializeStates();
    }

    private void InitializeStates()
    {
        States.Add(EAvailabilityState.UnnafectedState, new UnnafectedState(context));
        States.Add(EAvailabilityState.Stunned, new StunnedState(context));
        States.Add(EAvailabilityState.Death, new DeathState(context));

        CurrentState = States[EAvailabilityState.UnnafectedState];
    }

    protected override void Update()
    {
        UpdateStatusEffects();
        DetermineCurrentState();
        base.Update();
    }

    private void UpdateStatusEffects()
    {
        var effectsToRemove = new List<EStatusEffect>();

        foreach (var kvp in activeStatusEffects)
        {
            kvp.Value.remainingDuration -= Time.deltaTime;

            if (kvp.Value.remainingDuration <= 0)
            {
                effectsToRemove.Add(kvp.Key);
            }
        }

        // Remove expired effects
        foreach (var effect in effectsToRemove)
        {
            RemoveStatusEffect(effect);
        }
    }

    private void DetermineCurrentState()
    {
        // Death is permanent until Revive() (previously the next frame brought the player back to life).
        if (CurrentState.StateKey == EAvailabilityState.Death)
            return;

        EAvailabilityState targetState = EAvailabilityState.UnnafectedState;

        // Priority: Death > Stunned > Ready
        // Note: Silence alone does NOT change the state - player stays in Ready
        if (HasStatusEffect(EStatusEffect.Stunned) || HasStatusEffect(EStatusEffect.Incapacitated))
        {
            targetState = EAvailabilityState.Stunned;
        }
        // Player remains in Ready state when only silenced

        // Transition if needed
        if (CurrentState.StateKey != targetState)
        {
            var previousState = CurrentState.StateKey;
            TransitionToState(targetState);
            OnStateChanged?.Invoke(previousState, targetState);
        }
    }

    // Public "API" for managing status effects
    public void ApplyStatusEffect(EStatusEffect effect, float duration)
    {
        if (activeStatusEffects.ContainsKey(effect))
        {
            // If new duration is longer, overwrite
            if (duration > activeStatusEffects[effect].remainingDuration)
            {
                activeStatusEffects[effect].remainingDuration = duration;
                activeStatusEffects[effect].originalDuration = duration;
                if (debugLog) Debug.Log($"Extended {effect} duration to {duration}s");
            }
        }
        else
        {
            activeStatusEffects[effect] = new StatusEffectData(effect, duration);
            currentStatusFlags |= effect;
            OnStatusEffectAdded?.Invoke(effect);
            if (debugLog) Debug.Log($"Applied {effect} for {duration}s");
        }
    }

    public void RemoveStatusEffect(EStatusEffect effect)
    {
        if (activeStatusEffects.ContainsKey(effect))
        {
            activeStatusEffects.Remove(effect);
            currentStatusFlags &= ~effect;
            OnStatusEffectRemoved?.Invoke(effect);
            if (debugLog) Debug.Log($"Removed {effect}");
        }
    }

    /// <summary>
    /// Sets exactly how long <paramref name="effect"/> still lasts: adds it, shortens or lengthens it, or removes it when
    /// <paramref name="remaining"/> is 0 or less. Used to show the server's stun / silence on a client's copy.
    /// </summary>
    public void SetStatusEffectRemaining(EStatusEffect effect, float remaining)
    {
        if (float.IsNaN(remaining) || remaining <= 0f)
        {
            RemoveStatusEffect(effect);
            return;
        }
        if (activeStatusEffects.TryGetValue(effect, out StatusEffectData data))
        {
            data.remainingDuration = remaining;
            data.originalDuration = Mathf.Max(data.originalDuration, remaining);
            return;
        }
        ApplyStatusEffect(effect, remaining);
    }

    public bool HasStatusEffect(EStatusEffect effect)
    {
        return (currentStatusFlags & effect) != 0;
    }

    public float GetStatusEffectRemainingTime(EStatusEffect effect)
    {
        return activeStatusEffects.ContainsKey(effect) ? activeStatusEffects[effect].remainingDuration : 0f;
    }

    public EStatusEffect GetCurrentStatusFlags()
    {
        return currentStatusFlags;
    }

    // Convenience methods
    public bool CanMove()
    {
        // Only stunned or death prevents movement
        return CurrentState.StateKey != EAvailabilityState.Stunned &&
               CurrentState.StateKey != EAvailabilityState.Death;
    }

    public bool CanCastSpells()
    {
        // Silenced, stunned, or death prevents spell casting
        return !HasStatusEffect(EStatusEffect.Silenced) &&
               CurrentState.StateKey != EAvailabilityState.Stunned &&
               CurrentState.StateKey != EAvailabilityState.Death;
    }

    public bool CanAct()
    {
        // Can perform basic actions unless stunned or dead
        return CurrentState.StateKey != EAvailabilityState.Stunned &&
               CurrentState.StateKey != EAvailabilityState.Death;
    }

    public bool CanPerformBasicActions()
    {
        // Same as CanAct - basic actions like movement, attacking (but not spells)
        return CanAct();
    }

    // Public methods to apply specific effects
    public void ApplyStun(float duration)
    {
        ApplyStatusEffect(EStatusEffect.Stunned, duration);
    }

    public void ApplySilence(float duration)
    {
        ApplyStatusEffect(EStatusEffect.Silenced, duration);
    }

    private readonly HashSet<object> incapacitatedBy = new HashSet<object>();

    /// <summary>Is the character asleep or unconscious (cannot move or act)?</summary>
    public bool IsIncapacitated => incapacitatedBy.Count > 0;

    /// <summary>
    /// Sleep and unconsciousness: while any <paramref name="key"/> holds it, the character cannot move or act (the Stunned
    /// state), without counting as stunned for crowd control. Each system owns its key.
    /// </summary>
    public void SetIncapacitated(object key, bool on)
    {
        if (key == null)
            return;
        if (on) incapacitatedBy.Add(key);
        else incapacitatedBy.Remove(key);
        if (incapacitatedBy.Count > 0)
        {
            if (!activeStatusEffects.ContainsKey(EStatusEffect.Incapacitated))
                ApplyStatusEffect(EStatusEffect.Incapacitated, float.MaxValue);
        }
        else
        {
            RemoveStatusEffect(EStatusEffect.Incapacitated);
        }
    }

    public void Kill()
    {
        // Clear all status effects and go to death state
        incapacitatedBy.Clear();
        activeStatusEffects.Clear();
        currentStatusFlags = EStatusEffect.None;
        var previous = CurrentState != null ? CurrentState.StateKey : EAvailabilityState.UnnafectedState;
        if (previous == EAvailabilityState.Death)
            return;
        TransitionToState(EAvailabilityState.Death);
        OnStateChanged?.Invoke(previous, EAvailabilityState.Death);
    }

    public void Revive()
    {
        incapacitatedBy.Clear();
        activeStatusEffects.Clear();
        currentStatusFlags = EStatusEffect.None;
        var previous = CurrentState != null ? CurrentState.StateKey : EAvailabilityState.Death;
        TransitionToState(EAvailabilityState.UnnafectedState);
        OnStateChanged?.Invoke(previous, EAvailabilityState.UnnafectedState);
    }
}