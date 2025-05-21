using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveState : AbilityState
{
    bool abilityFinishedActivating = false;
    public ActiveState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;

    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Active);
        abilityFinishedActivating = false;
        Context.AbilityController.StartCoroutine(ActiveAbilityRoutine(Context.AbilityHolder, Context.instantiatedParticle));
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        GetNextState();
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (abilityFinishedActivating) return AbilityStateMachine.EAbilityState.InCooldown;
        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
    public virtual IEnumerator ActiveAbilityRoutine(AbilityHolder ability, GameObject instantiatedParticle = null)
    {
        yield return new WaitForSeconds(ability.abilityEffect.duration);
        abilityFinishedActivating = true;
        if (instantiatedParticle) Object.Destroy(instantiatedParticle);
    }




}