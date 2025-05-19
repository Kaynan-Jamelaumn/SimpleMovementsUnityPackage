using UnityEngine;

public class UnavailableState : AbilitiesState
{
    public UnavailableState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState estate) : base(context, estate)
    {
        AbilitiesContext Context = context;
    }
    public override void EnterState()
    {
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        //GetNextState();
    }
    public override AbilitiesStateMachine.EAbilitiesState GetNextState()
    {

        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }

}