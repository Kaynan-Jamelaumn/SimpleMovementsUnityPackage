using UnityEngine;

public class InCooldownState : AbilityState
{
    public InCooldownState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.InCooldown);
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
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