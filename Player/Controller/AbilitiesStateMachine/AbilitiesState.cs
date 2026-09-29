using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base of the player's abilities availability states. Availability is false while any key is in a phase that blocks
/// the others; pressed inputs are forwarded to their keys (any number of keys). A press that comes slightly too
/// early (another ability still busy, global cooldown, the end of its own cooldown) waits in the input buffer and is
/// cast as soon as the ability can start.
/// </summary>
public abstract class AbilitiesState : BaseState<AbilitiesStateMachine.EAbilitiesState>
{
    protected AbilitiesContext Context;

    public AbilitiesState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState stateKey) : base(stateKey)
    {
        Context = context;
    }

    /// <summary>False while one of the keys blocks the others (shared by both states, updated by events).</summary>
    protected bool Available() => Context.KeysAvailable;

    /// <summary>Kept for compatibility with states written for the old version (same as <see cref="Available"/>).</summary>
    protected bool _cachedAvailability => Context.KeysAvailable;

    /// <summary>Can the player use abilities at all (not stunned, silenced or dead)?</summary>
    protected bool CanCast()
    {
        AvailabilityStateMachine a = Context.AvailabilityState;
        return a == null || a.CanCastSpells();
    }

    private static bool IsKeyReady(AbilityStateMachine machine) =>
        machine.CurrentState != null && machine.CurrentState.StateKey == AbilityStateMachine.EAbilityState.Ready && machine.HasAbility;

    /// <summary>True if the input of key <paramref name="index"/> was pressed while its key is ready.</summary>
    protected bool IsAbilityTriggered(int index)
    {
        List<AbilityAction> actions = Context.AbilityAction;
        if (actions == null || index < 0 || index >= actions.Count)
            return false;
        AbilityStateMachine machine = actions[index] != null ? actions[index].AbilityStateMachine : null;
        if (machine == null)
            return false;
        InputAction action = Context.ResolveInput(index);
        return action != null && action.triggered && IsKeyReady(machine);
    }

    /// <summary>The input of key <paramref name="index"/> (see <see cref="AbilitiesContext.ResolveInput"/>).</summary>
    protected InputAction ResolveInput(AbilityAction a, int index) => Context.ResolveInput(index);

    /// <summary>
    /// Forwards every pressed ability input to its key. Presses that come slightly too early wait in the input buffer;
    /// presses that fail for another reason (not enough mana, silenced...) are forwarded so the failure is reported.
    /// </summary>
    protected void TriggerPressedAbilities()
    {
        List<AbilityAction> actions = Context.AbilityAction;
        if (actions == null)
            return;
        PlayerAbilityController controller = Context.AbilityController;
        bool buffering = Context.InputBufferTime > 0f;

        for (int i = 0; i < actions.Count; i++)
        {
            AbilityAction a = actions[i];
            AbilityStateMachine machine = a != null ? a.AbilityStateMachine : null;
            if (machine == null || !machine.HasAbility)
                continue;
            InputAction action = Context.ResolveInput(i);
            if (action == null)
                continue;
            bool pressed = action.triggered;
            bool held = !pressed && a.holdToRepeat && action.IsPressed();
            if (!pressed && !held)
                continue;

            bool keyReady = IsKeyReady(machine);
            CastFailReason reason = keyReady
                ? (controller != null ? controller.CheckSlotReady(machine.SlotIndex) : CastFailReason.None)
                : CastFailReason.Busy;

            if (held)
            {
                // Holding casts again whenever the ability can start; it never reports failures or fills the buffer.
                if (keyReady && reason == CastFailReason.None)
                    machine.Context.triggered = true;
                continue;
            }

            if (keyReady && (reason == CastFailReason.None || !buffering || !AbilityCaster.IsTimingReason(reason)))
            {
                // Cast now, or fail now with the reason (not enough mana, silenced...), exactly as before.
                machine.Context.triggered = true;
                Context.ClearBuffer();
            }
            else if (buffering)
            {
                Context.BufferPress(i);
            }
        }

        DeliverBufferedPress();
    }

    /// <summary>Casts the press waiting in the input buffer as soon as its ability can start (drops it when it expires).</summary>
    protected void DeliverBufferedPress()
    {
        int i = Context.BufferedKey;
        if (i < 0)
            return;
        List<AbilityAction> actions = Context.AbilityAction;
        if (actions == null || i >= actions.Count || Time.time > Context.BufferedUntil)
        {
            Context.ClearBuffer();
            return;
        }
        AbilityStateMachine machine = actions[i] != null ? actions[i].AbilityStateMachine : null;
        if (machine == null)
        {
            Context.ClearBuffer();
            return;
        }
        if (!IsKeyReady(machine))
            return;
        PlayerAbilityController controller = Context.AbilityController;
        if (controller != null && controller.CheckSlotReady(machine.SlotIndex) != CastFailReason.None)
            return;
        machine.Context.triggered = true;
        Context.ClearBuffer();
    }

    /// <summary>While abilities are blocked, presses are only remembered (cast when abilities are free again).</summary>
    protected void BufferPressedAbilities()
    {
        if (Context.InputBufferTime <= 0f)
            return;
        List<AbilityAction> actions = Context.AbilityAction;
        if (actions == null)
            return;
        for (int i = 0; i < actions.Count; i++)
        {
            AbilityStateMachine machine = actions[i] != null ? actions[i].AbilityStateMachine : null;
            if (machine == null || !machine.HasAbility)
                continue;
            InputAction action = Context.ResolveInput(i);
            if (action != null && action.triggered)
                Context.BufferPress(i);
        }
    }

    // Kept for compatibility (safe with fewer than 7 keys).
    protected bool TriggeredAbility1() => IsAbilityTriggered(0);
    protected bool TriggeredAbility2() => IsAbilityTriggered(1);
    protected bool TriggeredAbility3() => IsAbilityTriggered(2);
    protected bool TriggeredAbility4() => IsAbilityTriggered(3);
    protected bool TriggeredAbility5() => IsAbilityTriggered(4);
    protected bool TriggeredAbility6() => IsAbilityTriggered(5);
    protected bool TriggeredAbility7() => IsAbilityTriggered(6);
}
