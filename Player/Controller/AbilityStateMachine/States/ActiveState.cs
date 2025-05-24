using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveState : AbilityState
{
    bool abilityFinishedActivating = false;
    private Coroutine _activeAbilityRoutine;
    public ActiveState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;

    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Active);
        abilityFinishedActivating = false;
        _activeAbilityRoutine = Context.AbilityController.StartCoroutine(ActiveAbilityRoutine(Context.AbilityHolder, Context.instantiatedParticle));
    }
    public override void ExitState() { 
         if (Context.instantiatedParticle != null)
            {
                Object.Destroy(Context.instantiatedParticle);
                Context.instantiatedParticle = null;
            }
         if (_activeAbilityRoutine != null)
        {
                Context.AbilityController.StopCoroutine(_activeAbilityRoutine);
                _activeAbilityRoutine = null;
            }
    }
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
       // Context.instantiatedParticle.transform.position = Context.AbilityController.transform.position;

        yield return new WaitForSeconds(ability.abilityEffect.duration);
        abilityFinishedActivating = true;
    }




}