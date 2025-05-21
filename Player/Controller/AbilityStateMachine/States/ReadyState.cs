using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ReadyState : AbilityState
{
    AbilityStateMachine.EAbilityState stateMasterKey;
    bool useStateMasterKey = false;
    public ReadyState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Ready);
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        GetNextState();
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (useStateMasterKey)
        {
            useStateMasterKey = false;
            return stateMasterKey;

        }
        if ( Context.triggered)
            StartAbility(); 
        
        if (Context.abilityStartedActivating) return AbilityStateMachine.EAbilityState.Active;
            return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }





    public void StartAbility()
    {
        PlayerAbilityHolder ability = Context.AbilityHolder;

        AttackCast attackCast = ability.attackCast.FirstOrDefault();

        if (attackCast == null)
        {
            // Handle the case where there are no elements
            // Maybe return early or throw a more specific exception
            throw new InvalidOperationException("No attack casts available");
        }

        if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
            foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(Context.AbilityController.transform, eachaAttackCast);
        else if (ability.abilityEffect.shouldLaunch)
            _ = SetAbilityActions(Context.AbilityController.transform, attackCast);
        else if (!Context.abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
        {
            Context.abilityStillInProgress = true;
            Context. isWaitingForClick = true;
            Context.AbilityController.StartCoroutine(WaitForClickRoutine(ability, attackCast));

        }
        else if (ability.abilityEffect.isFixedPosition)
        {
            _ = SetAbilityActions(Context.AbilityController.transform, attackCast);
        }
    }





    public virtual IEnumerator WaitForClickRoutine(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        while (Context.isWaitingForClick)
            yield return null;
        Context.abilityStillInProgress = false;
        Context.isWaitingForClick = false;
        ProcessRayCastAbility(ability, attackCast);

    }



    private void ProcessRayCastAbility(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, attackCast.castSize))
        {
            if (hit.collider != null)
                _ = SetAbilityActions(hit.collider.transform, attackCast);
        }
    }





    protected virtual async Task SetAbilityActions(Transform abilityTargetTransform, AttackCast attackCast = null)
    {
        PlayerAbilityHolder ability = Context.AbilityHolder;
        Transform targetedTransform = abilityTargetTransform;
        GameObject instantiatedParticle = UnityEngine.Object.Instantiate(ability.particle);
        if (!ability.abilityEffect.isFixedPosition && ability.abilityEffect.shouldMarkAtCast)
        {
            ability.targetTransform = GetTargetTransform(targetedTransform);
            Context.targetTransform = ability.targetTransform;
        }
        await Context.AbilityController.SetParticleDuration(instantiatedParticle, ability, attackCast);
        instantiatedParticle.transform.position = Context.targetTransform.position;

        if (ability.abilityEffect.castDuration != 0)
        {
            stateMasterKey = AbilityStateMachine.EAbilityState.Casting;
            useStateMasterKey = true;
            GetNextState();
        }
        else
        {
            stateMasterKey = AbilityStateMachine.EAbilityState.Launching;
            useStateMasterKey = true;
            GetNextState();
        }
    }

}