using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attack chains per input: pressing an input again within its action's Variant Time plays the next variation
/// (base action, variation 1, variation 2... then the base action again).
/// </summary>
public class VariationSystem
{
    private readonly WeaponController controller;
    private readonly System.Func<WeaponSO> weaponSource;
    private readonly Dictionary<AttackType, VariationState> variationStates = new Dictionary<AttackType, VariationState>();

    public VariationSystem(WeaponController controller) : this(controller, null) { }

    /// <summary>Chains of the weapon <paramref name="weapon"/> returns (the off hand has its own); null = the main weapon.</summary>
    public VariationSystem(WeaponController controller, System.Func<WeaponSO> weapon)
    {
        this.controller = controller;
        weaponSource = weapon;
    }

    /// <summary>The weapon whose chains this state follows.</summary>
    public WeaponSO Weapon => weaponSource != null ? weaponSource() : controller.EquippedWeapon;

    public void Initialize()
    {
        foreach (AttackType attackType in System.Enum.GetValues(typeof(AttackType)))
            if (!variationStates.ContainsKey(attackType))
                variationStates[attackType] = new VariationState();
    }

    private VariationState State(AttackType t)
    {
        if (!variationStates.TryGetValue(t, out VariationState s))
            variationStates[t] = s = new VariationState();
        return s;
    }

    /// <summary>
    /// The action for an input and the variation to play (null = the base action). Step 0 of the chain is the base
    /// action, step k the k-th variation. (It used to skip the first variation.)
    /// </summary>
    public (AttackAction action, AttackVariation variation) GetAttackActionWithVariation(AttackType attackType)
    {
        WeaponSO w = Weapon;
        AttackAction baseAction = w != null ? w.GetAction(attackType) : null;
        if (baseAction == null) return (null, null);

        VariationState state = State(attackType);
        int count = baseAction.GetVariationCount();
        if (count == 0 || !state.IsWithinVariantTime(baseAction.variantTime))
        {
            state.Reset();
            return (baseAction, null);
        }
        int step = state.NextStep % (count + 1);
        return (baseAction, step == 0 ? null : baseAction.variations[step - 1]);
    }

    /// <summary>Advances the chain after an attack of this input started.</summary>
    public void UpdateVariationState(AttackType attackType, AttackAction action)
    {
        VariationState state = State(attackType);
        int count = action != null ? action.GetVariationCount() : 0;
        bool chaining = count > 0 && state.IsWithinVariantTime(action.variantTime);
        int played = chaining ? state.NextStep % (count + 1) : 0;
        state.NextStep = count > 0 ? (played + 1) % (count + 1) : 0;
        state.UpdateExecution();
    }

    public void UpdateVariationTimers()
    {
        WeaponSO weapon = Weapon;
        foreach (var kvp in variationStates)
        {
            var state = kvp.Value;
            var action = weapon != null ? weapon.GetAction(kvp.Key) : null;
            if (state.IsInVariantWindow && (action == null || !state.IsWithinVariantTime(action.variantTime)))
                state.Reset();
        }
    }

    /// <summary>The step of the chain the next press plays (0 = base action).</summary>
    public int GetCurrentVariationIndex(AttackType attackType) => variationStates.TryGetValue(attackType, out var state) ? state.NextStep : 0;

    public bool IsInVariantWindow(AttackType attackType)
    {
        var action = Weapon?.GetAction(attackType);
        return action != null && variationStates.TryGetValue(attackType, out var s) && s.IsWithinVariantTime(action.variantTime);
    }

    /// <summary>The variation the next press plays (null = the base action).</summary>
    public AttackVariation GetCurrentVariation(AttackType attackType)
    {
        var action = Weapon?.GetAction(attackType);
        int step = GetCurrentVariationIndex(attackType);
        return action == null || step <= 0 || step > action.GetVariationCount() ? null : action.variations[step - 1];
    }

    public void Reset()
    {
        foreach (var state in variationStates.Values)
            state.Reset();
    }

    private class VariationState
    {
        public int NextStep;
        public float LastExecutionTime = -999f;
        public bool IsInVariantWindow;

        public void Reset()
        {
            NextStep = 0;
            LastExecutionTime = -999f;
            IsInVariantWindow = false;
        }

        public bool IsWithinVariantTime(float variantTime) => Time.time - LastExecutionTime <= variantTime;

        public void UpdateExecution()
        {
            LastExecutionTime = Time.time;
            IsInVariantWindow = true;
        }
    }
}
