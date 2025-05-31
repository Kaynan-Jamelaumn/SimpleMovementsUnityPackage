using System.Collections.Generic;
using UnityEngine;

public class VariationSystem
{
    private WeaponController controller;
    private readonly Dictionary<AttackType, VariationState> variationStates = new Dictionary<AttackType, VariationState>();

    public VariationSystem(WeaponController controller)
    {
        this.controller = controller;
    }

    public void Initialize()
    {
        foreach (AttackType attackType in System.Enum.GetValues(typeof(AttackType)))
        {
            variationStates[attackType] = new VariationState();
        }
    }

    public (AttackAction action, AttackVariation variation) GetAttackActionWithVariation(AttackType attackType)
    {
        var baseAction = controller.EquippedWeapon.GetAction(attackType);
        if (baseAction == null) return (null, null);

        var variationState = variationStates[attackType];

        // Check if we're within variant time window
        if (variationState.IsWithinVariantTime(baseAction.variantTime))
        {
            // Get the next variation
            var variation = controller.EquippedWeapon.GetActionVariation(attackType, variationState.CurrentVariationIndex);
            if (variation != null)
            {
                controller.LogDebug($"Using variation {variationState.CurrentVariationIndex} for {attackType}");
                return (baseAction, variation);
            }
        }

        // Reset to first variation (base action)
        variationState.Reset();
        controller.LogDebug($"Using base action for {attackType}");
        return (baseAction, null);
    }

    public void UpdateVariationState(AttackType attackType, AttackAction action)
    {
        var variationState = variationStates[attackType];
        variationState.UpdateExecution();

        // Increment to next variation for future use
        int maxVariations = action.GetVariationCount();
        if (maxVariations > 0)
        {
            variationState.CurrentVariationIndex = (variationState.CurrentVariationIndex + 1) % (maxVariations + 1);
        }
        else
        {
            variationState.CurrentVariationIndex = 0;
        }
    }

    public void UpdateVariationTimers()
    {
        foreach (var kvp in variationStates)
        {
            var state = kvp.Value;
            var action = controller.EquippedWeapon?.GetAction(kvp.Key);

            if (action != null && state.IsInVariantWindow && !state.IsWithinVariantTime(action.variantTime))
            {
                state.Reset();
                controller.LogDebug($"Variation timer expired for {kvp.Key}");
            }
        }
    }

    public int GetCurrentVariationIndex(AttackType attackType)
    {
        return variationStates.TryGetValue(attackType, out var state) ? state.CurrentVariationIndex : 0;
    }

    public bool IsInVariantWindow(AttackType attackType)
    {
        var action = controller.EquippedWeapon?.GetAction(attackType);
        var state = variationStates.TryGetValue(attackType, out var s) ? s : null;

        return action != null && state != null && state.IsWithinVariantTime(action.variantTime);
    }

    public AttackVariation GetCurrentVariation(AttackType attackType)
    {
        return controller.EquippedWeapon?.GetActionVariation(attackType, GetCurrentVariationIndex(attackType));
    }

    public void Reset()
    {
        foreach (var state in variationStates.Values)
        {
            state.Reset();
        }
    }

    // Nested Classes
    private class VariationState
    {
        public int CurrentVariationIndex;
        public float LastExecutionTime;
        public bool IsInVariantWindow;

        public void Reset()
        {
            CurrentVariationIndex = 0;
            LastExecutionTime = 0f;
            IsInVariantWindow = false;
        }

        public bool IsWithinVariantTime(float variantTime)
        {
            return Time.time - LastExecutionTime <= variantTime;
        }

        public void UpdateExecution()
        {
            LastExecutionTime = Time.time;
            IsInVariantWindow = true;
        }
    }
}