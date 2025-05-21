using UnityEngine;

public class AvailableState : AbilitiesState
{
    public AvailableState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState estate) : base(context, estate)
    {
        AbilitiesContext Context = context;
    }
    public override void EnterState()
    {
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        GetNextState();

    }
    public override AbilitiesStateMachine.EAbilitiesState GetNextState()
    {

        if (!Available()) return AbilitiesStateMachine.EAbilitiesState.Unavailable;
        if (TriggeredAbility1()) Context.AbilityAction[0].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility2()) Context.AbilityAction[1].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility3()) Context.AbilityAction[2].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility4()) Context.AbilityAction[3].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility5()) Context.AbilityAction[4].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility6()) Context.AbilityAction[5].AbilityStateMachine.Context.triggered = true;
        if (TriggeredAbility7()) Context.AbilityAction[6].AbilityStateMachine.Context.triggered = true;
        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

}