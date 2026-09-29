using UnityEngine;

/// <summary>Abilities are blocked (another ability is busy, or the player is stunned, silenced or dead). Presses are buffered.</summary>
public class UnavailableState : AbilitiesState
{
    public UnavailableState(AbilitiesContext context, AbilitiesStateMachine.EAbilitiesState estate) : base(context, estate) { }

    public override void EnterState() { }
    public override void ExitState() { }
    public override void UpdateState() { }

    public override AbilitiesStateMachine.EAbilitiesState GetNextState()
    {
        if (Available() && CanCast())
            return AbilitiesStateMachine.EAbilitiesState.Available;
        // Remember presses made while blocked: they are cast as soon as abilities are free (input buffer).
        BufferPressedAbilities();
        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}
