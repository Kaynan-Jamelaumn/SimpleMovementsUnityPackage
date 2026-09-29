using UnityEngine;

/// <summary>Abilities can be used: forwards pressed inputs to their keys and casts a buffered press when it can start.</summary>
public class AvailableState : AbilitiesState
{
    public AvailableState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState estate) : base(context, estate) { }

    public override void EnterState() { }
    public override void ExitState() { }
    public override void UpdateState() { }

    public override AbilitiesStateMachine.EAbilitiesState GetNextState()
    {
        if (!Available() || !CanCast())
            return AbilitiesStateMachine.EAbilitiesState.Unavailable;
        TriggerPressedAbilities();
        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
