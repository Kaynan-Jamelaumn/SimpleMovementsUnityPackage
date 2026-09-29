/// <summary>Follow-through / recovery of the ability. Ends with the slot's Active phase.</summary>
public class ActiveState : AbilityState
{
    public ActiveState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate) { }
}
