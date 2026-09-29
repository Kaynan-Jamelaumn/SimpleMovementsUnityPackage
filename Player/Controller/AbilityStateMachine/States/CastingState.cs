/// <summary>Wind-up of the ability (telegraph shown). Ends when the PlayerAbilityController releases it.</summary>
public class CastingState : AbilityState
{
    public CastingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate) { }
}
