using UnityEngine;

public class LaunchingState : AbilityState
{
    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Launching);
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

}