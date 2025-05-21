using System.Collections;
using UnityEditor.Playables;
using UnityEngine;

public class CastingState : AbilityState
{
    bool goToNextState = false;
    public CastingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        // enemyEffect singleTargetSelfTarget isFixedPosition isPartialPermanentTargetWhileCasting isPermanentTarget shouldMarkAtCast
        // singleTargetselfTarget ability that follows player afftect only player
        // isFixedPosition ability that follows the player until activated
        // isPartialPermanentTargetWhileCasting follows the player until the end of casting(entering launching)
        // shouldMarkAtCast activate the ability at the first position when casting was activated
        // enemyEffect affect non agressive creature true= no
        Debug.Log("estado casting");
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Casting);
        //if (Context.isPermanentTargetOnCast) Context.AbilityController.StartCoroutine(SetPermanentTargetOnCastRoutine(Context.AbilityController.transform, Context.instantiatedParticle, Context.AbilityStateMachine));
        //else Context.AbilityController.StartCoroutine(SetTargetOnCastRoutine(Context.AbilityController.transform, Context.instantiatedParticle, Context.AbilityStateMachine));

        PlayerAbilityHolder ability = Context.AbilityHolder;
        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.isFixedPosition || ability.abilityEffect.singleTargetSelfTarget)
            Context.AbilityController.StartCoroutine(SetPermanentTargetOnCastRoutine());
        if (ability.abilityEffect.shouldMarkAtCast)
            Context.AbilityController.StartCoroutine(SetTargetOnCastRoutine());
    }

    public override void ExitState()
    {
      
    }

    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (goToNextState) return AbilityStateMachine.EAbilityState.Launching;
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
        goToNextState = true;
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
        goToNextState = true;
        //if (ability.abilityEffect.shouldLaunch)
        //    Context.AbilityController.StartCoroutine(SetBulletLikeTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
        //else if (!ability.abilityEffect.shouldLaunch && ability.abilityEffect.isPermanentTarget)
        //    Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
        //else
        //    Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, abilityStateMachine, attackCast));
    }

}