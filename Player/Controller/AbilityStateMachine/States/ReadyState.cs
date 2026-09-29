/// <summary>Waiting for input: when triggered, asks the PlayerAbilityController to cast (or to open the preview).</summary>
public class ReadyState : AbilityState
{
    public ReadyState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate) { }

    protected override void OnUpdate()
    {
        PlayerAbilityController controller = Context.AbilityController;
        Context.abilityStillInProgress = controller != null && controller.TargetingSlot == Context.SlotIndex;
        Context.isWaitingForClick = Context.abilityStillInProgress;
        if (!Context.triggered)
            return;
        Context.triggered = false;
        if (controller != null)
            controller.TryCastFromInput(Context.SlotIndex);
    }

    protected override void OnExit()
    {
        Context.triggered = false;
        Context.abilityStillInProgress = false;
        Context.isWaitingForClick = false;
    }
}
