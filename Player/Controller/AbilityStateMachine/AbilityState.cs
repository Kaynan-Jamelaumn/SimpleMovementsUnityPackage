using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AbilityStateMachine;

// enemyEffect singleTargetSelfTarget isFixedPosition isPartialPermanentTargetWhileCasting isPermanentTarget shouldMarkAtCast
// singleTargetselfTarget ability that follows player afftect only player
// isFixedPosition ability that follows the player until activated
// isPartialPermanentTargetWhileCasting follows the player until the end of casting(entering launching)
// shouldMarkAtCast activate the ability at the first position when casting was activated
// enemyEffect affect non agressive creature true= no

public abstract class AbilityState : BaseState<AbilityStateMachine.EAbilityState>
{
    protected AbilityContext Context;


    public AbilityState(AbilityContext context, AbilityStateMachine.EAbilityState stateKey) : base(stateKey)
    {
        Context = context;
    }

    public bool Available()
    {
        return Context.cachedAvailability;
    }

    protected void RecalculateAvailability(EAbilityState stateKey)
    {
        bool isAvailable;
        if (Context.AbilityHolder == null || Context.AbilityHolder.abilityEffect == null)
        {
            isAvailable = true;
            Context.SetCachedAvailability(isAvailable);
            return;
        }

        if (Context.AbilityHolder.abilityEffect.StateAvailabilityDict.TryGetValue(stateKey, out bool availability))
            isAvailable = availability;
        else
            isAvailable = true;

        Context.SetCachedAvailability(isAvailable);
    }

    // Use cached transform instead of creating new GameObjects
    public virtual Transform GetTargetTransform(Transform playerTransform)
    {
        return Context.GetCachedPlayerTransform(playerTransform);
    }

    public virtual void SetGizmosAndColliderAndParticlePosition(bool isPermanent = false)
    {
        AbilityHolder ability = Context.AbilityHolder;
        Transform playerTransform = Context.AbilityController.transform;

        if (isPermanent)
        {
            ability.targetTransform = playerTransform;
            Context.targetTransform = playerTransform;
        }
        else
        {
            ability.targetTransform = GetTargetTransform(playerTransform);
            Context.targetTransform = ability.targetTransform;
        }

        // Update particle position if it exists
        if (Context.instantiatedParticle != null)
            Context.instantiatedParticle.transform.position = Context.targetTransform.position;
    }

    public virtual void ApplyAbilityUse(GameObject affectedTarget = null)
    {
        AbilityHolder ability = Context.AbilityHolder;

        foreach (var effect in ability.abilityEffect.effects)
        {
            if (effect.attackCast == null)
                effect.attackCast = new List<AttackCast> { Context.attackCast };

            if (effect.enemyEffect == false)
            {
                if (ability.abilityEffect.casterReceivesBeneffitsBuffsEvenFromFarAway)
                    ability.abilityEffect.Use(Context.AbilityController.gameObject, effect);
                else
                    ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast);
            }
            else
            {
                if (ability.abilityEffect.multiAreaEffect)
                {
                    if (ability.abilityEffect.casterReceivePenalties)
                        ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, false, null, Context.AbilityController.gameObject);
                    else if (affectedTarget)
                        ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, affectedTarget);
                    else
                        ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, false);
                }
                else
                {
                    if (ability.abilityEffect.casterReceivePenalties)
                        ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, true, null, Context.AbilityController.gameObject);
                    else
                        ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, true);
                }
            }
        }

        ability.activeTime = Time.time;
        Context.targetTransform = Context.AbilityController.transform;
    }

}