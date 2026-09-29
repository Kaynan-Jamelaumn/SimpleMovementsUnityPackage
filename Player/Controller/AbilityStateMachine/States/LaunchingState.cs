/// <summary>The ability's actions are running (volleys, dashes, beams). Ends with the slot's Launching phase.</summary>
public class LaunchingState : AbilityState
{
    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate) { }
}
