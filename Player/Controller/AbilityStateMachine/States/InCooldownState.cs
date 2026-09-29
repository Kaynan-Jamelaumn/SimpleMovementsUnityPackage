/// <summary>Cooling down. Returns to Ready when the slot has a charge again.</summary>
public class InCooldownState : AbilityState
{
    public InCooldownState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate) { }

    /// <summary>0 = ready, 1 = just started (for UI).</summary>
    public float CooldownFraction
    {
        get
        {
            AbilitySlot slot = Context.Slot;
            return slot != null ? slot.CooldownFraction : 0f;
        }
    }
}
