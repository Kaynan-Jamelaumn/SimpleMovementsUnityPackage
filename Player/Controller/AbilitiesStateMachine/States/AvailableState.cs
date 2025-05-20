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

        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

}