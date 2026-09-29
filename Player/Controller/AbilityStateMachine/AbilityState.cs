using UnityEngine;
using static AbilityStateMachine;

/// <summary>
/// Base of a player ability slot state. The PlayerAbilityController runs the ability; these states mirror the
/// slot's phase (Ready, Casting, Launching, Active, InCooldown) so existing listeners, UI and the abilities
/// availability machine keep working.
/// </summary>
public abstract class AbilityState : BaseState<EAbilityState>
{
    protected AbilityContext Context;

    public AbilityState(AbilityContext context, EAbilityState stateKey) : base(stateKey)
    {
        Context = context;
    }

    public bool Available() => Context.cachedAvailability;

    /// <summary>Other abilities are blocked while this ability is in a phase listed in its Blocks Other Abilities.</summary>
    protected void RecalculateAvailability(EAbilityState stateKey)
    {
        AbilitySlot slot = Context.Slot;
        AbilityDefinition def = slot != null ? slot.ability : null;
        Context.SetCachedAvailability(def == null || !def.BlocksOthersDuring((AbilityPhase)(int)stateKey));
    }

    public override void EnterState()
    {
        RecalculateAvailability(StateKey);
        OnEnter();
    }

    public override void ExitState() => OnExit();

    public override void UpdateState() => OnUpdate();

    /// <summary>Follows the slot's phase (the enums have the same order).</summary>
    public override EAbilityState GetNextState()
    {
        AbilitySlot slot = Context.Slot;
        return slot != null ? (EAbilityState)(int)slot.Phase : StateKey;
    }

    protected virtual void OnEnter() { }
    protected virtual void OnExit() { }
    protected virtual void OnUpdate() { }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
