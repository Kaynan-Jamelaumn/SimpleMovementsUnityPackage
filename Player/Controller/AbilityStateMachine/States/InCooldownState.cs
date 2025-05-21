using System.Collections;
using UnityEngine;

public class InCooldownState : AbilityState
{
    bool cooldownFinished = false;
    public InCooldownState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.InCooldown);
        Context.AbilityController.StartCoroutine(CooldownAbilityRoutine(Context.AbilityHolder));
    }
    public override void ExitState() { cooldownFinished = false; }
    public override void UpdateState()
    {
        GetNextState();
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (cooldownFinished) return AbilityStateMachine.EAbilityState.Ready;   
        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

    public virtual IEnumerator CooldownAbilityRoutine(AbilityHolder ability)
    {
        yield return new WaitForSeconds(ability.abilityEffect.coolDown);
        cooldownFinished = true;
    }

}