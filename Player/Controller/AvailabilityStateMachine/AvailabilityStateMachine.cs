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
    Slowed = 1 << 3
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

    private void Update()
    {
        UpdateStatusEffects();
        DetermineCurrentState();

        // Handle state transitions and updates (copied from base StateManager logic)
        EAvailabilityState nextStateKey = CurrentState.GetNextState();

        if (!IsTransitioningState && nextStateKey.Equals(CurrentState.StateKey))
        {
            CurrentState.UpdateState();
        }
        else if (!IsTransitioningState)
        {
            TransitionToState(nextStateKey);
        }
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
        EAvailabilityState targetState = EAvailabilityState.UnnafectedState;

        // Priority: Death > Stunned > Ready
        // Note: Silence alone does NOT change the state - player stays in Ready
        if (HasStatusEffect(EStatusEffect.Stunned))
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
                Debug.Log($"Extended {effect} duration to {duration}s");
            }
        }
        else
        {
            activeStatusEffects[effect] = new StatusEffectData(effect, duration);
            currentStatusFlags |= effect;
            OnStatusEffectAdded?.Invoke(effect);
            Debug.Log($"Applied {effect} for {duration}s");
        }
    }

    public void RemoveStatusEffect(EStatusEffect effect)
    {
        if (activeStatusEffects.ContainsKey(effect))
        {
            activeStatusEffects.Remove(effect);
            currentStatusFlags &= ~effect;
            OnStatusEffectRemoved?.Invoke(effect);
            Debug.Log($"Removed {effect}");
        }
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

    public void Kill()
    {
        // Clear all status effects and go to death state
        activeStatusEffects.Clear();
        currentStatusFlags = EStatusEffect.None;
        TransitionToState(EAvailabilityState.Death);
    }

    public void Revive()
    {
        TransitionToState(EAvailabilityState.UnnafectedState);
    }
}