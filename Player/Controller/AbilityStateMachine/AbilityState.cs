using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VersionControl;
using UnityEngine;
using System.Threading.Tasks;
using static AbilityStateMachine;
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


    public virtual Transform GetTargetTransform(Transform playerTransform)
    {
        Transform newPlayerTransform = new GameObject("PlayerLastTransform").transform;
        if (Context.oldTransform == null) Context.oldTransform = newPlayerTransform;
        else
        {
            UnityEngine.Object.Destroy(Context.oldTransform.gameObject);
            Context.oldTransform = newPlayerTransform;
        }
        newPlayerTransform.position = playerTransform.position;
        newPlayerTransform.rotation = playerTransform.rotation;
        return newPlayerTransform;
    }




    public virtual void SetGizmosAndColliderAndParticlePosition(bool isPermanent = false)
    {

        AbilityHolder ability = Context.AbilityHolder;
        Transform playerTransform = Context.AbilityController.transform;
        if (!ability.abilityEffect.isFixedPosition)
        {
            if (isPermanent)
            {
                ability.targetTransform = playerTransform; // player transform
                Context.targetTransform = playerTransform;
            }
            else
            {
                ability.targetTransform = GetTargetTransform(playerTransform);
                Context.targetTransform = GetTargetTransform(playerTransform);
            }
            if (Context.instantiatedParticle)
                Context.instantiatedParticle.transform.position = Context.targetTransform.position;
        }

    }





    public virtual void ApplyAbilityUse(GameObject affectedTarget = null)
    {
        AbilityHolder ability = Context.AbilityHolder;

        foreach (var effect in ability.abilityEffect.effects)
        {

            if (effect.attackCast == null) effect.attackCast = new List<AttackCast> { Context.attackCast };
            if (effect.enemyEffect == false)
            {
                if (ability.abilityEffect.isSelfTargetOrCasterReceivesBeneffitsBuffsEvenFromFarAway) ability.abilityEffect.Use(Context.AbilityController.gameObject, effect);
                else ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast);

            }

            else
            {
                if (ability.abilityEffect.multiAreaEffect)
                {
                    if (ability.abilityEffect.casterIsImune) ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, false, null, Context.AbilityController.gameObject);
                    else if (affectedTarget) ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, affectedTarget);
                    else ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, false);
                }
                else
                {
                    if (ability.abilityEffect.casterIsImune) ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, true, null, Context.AbilityController.gameObject);
                    else ability.abilityEffect.Use(Context.targetTransform, effect, effect.attackCast, true);
                }

            }

        }
        ability.activeTime = Time.time;
        Context.targetTransform = Context.AbilityController.transform;
    }




}

