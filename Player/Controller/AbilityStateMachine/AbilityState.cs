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
            Context.oldTransform = Context.oldTransform;
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
        if (!ability.abilityEffect.isFixedPosition)
        {
            if (isPermanent)
            {
                ability.targetTransform = Context.targetTransform; // player transform
                Context.targetTransform = Context.targetTransform; ;
            }
            else
            {
                ability.targetTransform = GetTargetTransform(Context.targetTransform);
                Context.targetTransform = GetTargetTransform(Context.targetTransform);
            }
            if (Context.instantiatedParticle)
                Context.instantiatedParticle.transform.position = Context.targetTransform.position;
        }

    }





    public virtual async  System.Threading.Tasks.Task SetParticleDuration()
    {
        AbilityHolder ability = Context.AbilityHolder;
        ParticleSystem particleSystem = Context.instantiatedParticle.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule mainModule = particleSystem.main;

        // Set start delay and duration before starting the particle system
        mainModule.startDelay = 0;

        float duration;
        if (ability.abilityEffect.shouldLaunch)
            duration = ability.abilityEffect.lifeSpan;

        else if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast)
            duration = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;

        else duration = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;

        mainModule.duration = duration;
        mainModule.startLifetime = duration;
        // Set sub-particle system durations
        float subParticleDuration = ability.abilityEffect.shouldLaunch ? ability.abilityEffect.lifeSpan :
            ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast ?
            ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration : ability.abilityEffect.duration;

        foreach (var particle in particleSystem.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule mainModuleSubParticle = particle.main;
            mainModuleSubParticle.startDelay = ability.abilityEffect.finalLaunchTime;
            mainModuleSubParticle.duration = subParticleDuration;
            mainModuleSubParticle.startLifetime = subParticleDuration - ability.abilityEffect.finalLaunchTime;
            if (ability.abilityEffect.subParticleShouldChangeSize)
            {
                if (Context.attackCast.castType == AttackCast.CastType.Box)
                {
                    if (mainModuleSubParticle.startSizeX.constant < Context.attackCast.boxSize.x && mainModuleSubParticle.startSizeZ.constant < Context.attackCast.boxSize.z && mainModuleSubParticle.startSizeY.constant < Context.attackCast.boxSize.y)
                        ChangeParticleSize(mainModuleSubParticle, Context.attackCast);
                }
                else if (mainModuleSubParticle.startSizeX.constant < Context.attackCast.castSize && mainModuleSubParticle.startSizeZ.constant < Context.attackCast.castSize && mainModuleSubParticle.startSizeY.constant < Context.attackCast.castSize)
                    ChangeParticleSize(mainModuleSubParticle, Context.attackCast);
            }
        }
        if (ability.abilityEffect.particleShouldChangeSize) ChangeParticleSize(mainModule, Context.attackCast);
        particleSystem.Play();


    }
#pragma warning restore CS1998
    private void ChangeParticleSize(ParticleSystem.MainModule particle, AttackCast attackCast = null)
    {
        if (attackCast != null)
        {
            float sizeX, sizeY, sizeZ;
            if (attackCast.castType == AttackCast.CastType.Sphere)
                sizeX = sizeY = sizeZ = attackCast.castSize;

            else
            {
                sizeX = attackCast.boxSize.x;
                sizeY = attackCast.boxSize.y;
                sizeZ = attackCast.boxSize.z;
            }

            particle.startSizeX = sizeX;
            particle.startSizeY = sizeY;
            particle.startSizeZ = sizeZ;
        }
    }

    public virtual void ApplyAbilityUse(GameObject affectedTarget = null)
    {
        AbilityHolder ability = Context.AbilityHolder;

        //ability.abilityEffect.Use();
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
        Context.abilityStartedActivating = true;//ability.abilityState = AbilityHolder.AbilityState.Active;
        ability.activeTime = Time.time;
        Context.targetTransform = Context.AbilityController.transform;
        //Context.AbilityController.StartCoroutine(ActiveAbilityRoutine(ability, instantiatedParticle));
    }




}

