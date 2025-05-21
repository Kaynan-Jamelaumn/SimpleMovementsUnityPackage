using System.Collections;
using UnityEditor.Playables;
using UnityEngine;

public class CastingState : AbilityState
{
    public CastingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Casting);
        //if (Context.isPermanentTargetOnCast) Context.AbilityController.StartCoroutine(SetPermanentTargetOnCastRoutine(Context.AbilityController.transform, Context.instantiatedParticle, Context.AbilityStateMachine));
        //else Context.AbilityController.StartCoroutine(SetTargetOnCastRoutine(Context.AbilityController.transform, Context.instantiatedParticle, Context.AbilityStateMachine));

        PlayerAbilityHolder ability = Context.AbilityHolder;
        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting)
            Context.AbilityController.StartCoroutine(SetPermanentTargetOnCastRoutine());
        else
            Context.AbilityController.StartCoroutine(SetTargetOnCastRoutine());
    }

    public override void ExitState()
    {
      
    }

    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (Context.abilityStartedActivating) return AbilityStateMachine.EAbilityState.Active;
        return StateKey;

    }

    public override void LateUpdateState()
    {
    }

    public override void OnTriggerEnter(Collider other)
    {
    }

    public override void OnTriggerExit(Collider other)
    {
    }

    public override void OnTriggerStay(Collider other)
    {
    }

    public override void UpdateState()
    {
        GetNextState();
    }



    public virtual IEnumerator SetPermanentTargetOnCastRoutine()
    {
        //ability.abilityState = AbilityHolder.AbilityState.Casting;
        //abilityStateMachine.TransitionToState(AbilityStateMachine.EAbilityState.Casting);

        AbilityHolder ability = Context.AbilityHolder;
        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.castDuration)
        {
            SetGizmosAndColliderAndParticlePosition(true);
            yield return null;
        }

        SetGizmosAndColliderAndParticlePosition(false);
        //if (ability.abilityEffect.isPermanentTarget)
        //    Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
        //else
        //    Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
    }



    public virtual IEnumerator SetTargetOnCastRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;
        //ability.abilityState = AbilityHolder.AbilityState.Casting;
        //abilityStateMachine.TransitionToState(AbilityStateMachine.EAbilityState.Casting);
        if (ability.abilityEffect.shouldMarkAtCast == true) SetGizmosAndColliderAndParticlePosition();


        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.castDuration)
            yield return null;
        //if (ability.abilityEffect.shouldLaunch)
        //    Context.AbilityController.StartCoroutine(SetBulletLikeTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
        //else if (!ability.abilityEffect.shouldLaunch && ability.abilityEffect.isPermanentTarget)
        //    Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
        //else
        //    Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
    }

}