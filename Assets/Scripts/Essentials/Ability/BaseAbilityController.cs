using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
public abstract class BaseAbilityController<T> : MonoBehaviour where T : AbilityHolder
{
    [SerializeField] protected Transform targetTransform;
    protected Transform oldTransform;
    protected virtual async Task SetAbilityActions(T ability, Transform abilityTargetTransform, AttackCast attackCast = null)
    {
        Transform targetedTransform = abilityTargetTransform;
        GameObject instantiatedParticle = Instantiate(ability.particle);
        if (!ability.abilityEffect.isFixedPosition && ability.abilityEffect.shouldMarkAtCast)
        {
            ability.targetTransform = GetTargetTransform(targetedTransform);
            targetTransform = ability.targetTransform;
        }
        await SetParticleDuration(instantiatedParticle, ability, attackCast);
        instantiatedParticle.transform.position = targetTransform.position;

        if (ability.abilityEffect.castDuration != 0)
        {
            ability.abilityState = AbilityHolder.AbilityState.Casting;
            if (ability.abilityEffect.isPartialPermanentTargetWhileCasting)
                StartCoroutine(SetPermanentTargetOnCastRoutine(ability, targetedTransform, instantiatedParticle, attackCast));
            else
                StartCoroutine(SetTargetOnCastRoutine(ability, targetedTransform, instantiatedParticle, attackCast));
        }
        else
        {
            if (ability.abilityEffect.isPermanentTarget)
                StartCoroutine(SetPermanentTargetLaunchRoutine(ability, targetedTransform, instantiatedParticle, attackCast));
            else
                StartCoroutine(DelayedSetTargetLaunchRoutine(ability, targetedTransform, instantiatedParticle, attackCast));
        }
    }


#pragma warning disable CS1998
    public virtual async Task SetParticleDuration(GameObject instantiatedParticle, AbilityHolder ability, AttackCast attackCast = null)
    {
        ParticleSystem particleSystem = instantiatedParticle.GetComponent<ParticleSystem>();
        //if (particleSystem.isPlaying)
        //{
        //    particleSystem.Stop(true);
        //}
        ParticleSystem.MainModule mainModule = particleSystem.main;

        // Set start delay and duration before starting the particle system
        mainModule.startDelay = 0;

        float duration;
        if (ability.abilityEffect.shouldLaunch)
            duration = ability.abilityEffect.lifeSpan;
        
        else if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast)
           duration = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
        
        else duration = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
        //mainModule.startDelay = ability.abilityEffect.castDuration;


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
            mainModuleSubParticle.startLifetime = subParticleDuration -ability.abilityEffect.finalLaunchTime;
            if (ability.abilityEffect.subParticleShouldChangeSize)
            {
                if (attackCast.castType == AttackCast.CastType.Box)
                {
                    if (mainModuleSubParticle.startSizeX.constant < attackCast.boxSize.x && mainModuleSubParticle.startSizeZ.constant < attackCast.boxSize.z && mainModuleSubParticle.startSizeY.constant < attackCast.boxSize.y)
                         ChangeParticleSize(mainModuleSubParticle, attackCast);
                }
                else if (mainModuleSubParticle.startSizeX.constant < attackCast.castSize && mainModuleSubParticle.startSizeZ.constant < attackCast.castSize && mainModuleSubParticle.startSizeY.constant < attackCast.castSize)
                     ChangeParticleSize(mainModuleSubParticle, attackCast);
            }
        }
        if (ability.abilityEffect.particleShouldChangeSize)  ChangeParticleSize(mainModule, attackCast);
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

    public virtual void SetGizmosAndColliderAndParticlePosition(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, bool isPermanent = false)
    {
        if (!ability.abilityEffect.isFixedPosition)
        {
            if (isPermanent)
            {
                ability.targetTransform = playerTransform;
                targetTransform = playerTransform;
            }
            else
            {
                ability.targetTransform = GetTargetTransform(playerTransform);
                targetTransform = GetTargetTransform(playerTransform);
            }
            if (instantiatedParticle)
            instantiatedParticle.transform.position = targetTransform.position;
        }

    }

    public virtual Transform GetTargetTransform(Transform playerTransform)
    {
        Transform newPlayerTransform = new GameObject("PlayerLastTransform").transform;
        if (oldTransform == null) oldTransform = newPlayerTransform;
        else
        {
             Destroy(oldTransform.gameObject);
            oldTransform = newPlayerTransform;
        }
        newPlayerTransform.position = playerTransform.position;
        newPlayerTransform.rotation = playerTransform.rotation;
        return newPlayerTransform;
    }

    public virtual IEnumerator CooldownAbilityRoutine(AbilityHolder ability)
    {
        yield return new WaitForSeconds(ability.abilityEffect.coolDown);
        ability.abilityState = AbilityHolder.AbilityState.Ready;
    }

    public virtual IEnumerator ActiveAbilityRoutine(AbilityHolder ability, GameObject instantiatedParticle = null)
    {
        yield return new WaitForSeconds(ability.abilityEffect.duration);
        ability.abilityState = AbilityHolder.AbilityState.InCooldown;
        StartCoroutine(CooldownAbilityRoutine(ability));
        if (instantiatedParticle) Destroy(instantiatedParticle);
    }

    public virtual IEnumerator SetPermanentTargetOnCastRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {
        ability.abilityState = AbilityHolder.AbilityState.Casting;
        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.castDuration)
        {
            SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, true);
            yield return null;
        }

        SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, false);
        if (ability.abilityEffect.isPermanentTarget)
            StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, attackCast));
        else
            StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, attackCast));
    }

    public virtual IEnumerator SetTargetOnCastRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {
        ability.abilityState = AbilityHolder.AbilityState.Casting;
        if (ability.abilityEffect.shouldMarkAtCast == true) SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle);


        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.castDuration)
            yield return null;
        if (ability.abilityEffect.shouldLaunch) 
            StartCoroutine(SetBulletLikeTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, attackCast));
        else if (! ability.abilityEffect.shouldLaunch && ability.abilityEffect.isPermanentTarget)
            StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, attackCast));
        else
            StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle, attackCast));
    }

    public virtual IEnumerator UntilReachesPosition(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {
        ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;
        if (ability.abilityEffect.shouldLaunch)
        {
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = playerTransform.transform.position;//ability.abilityEffect.targetTransform.position;
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            Transform newPlayerTransform = GetTargetTransform(transform);
            while (Time.time < startTime + ability.abilityEffect.finalLaunchTime)
            {
                float distCovered = (Time.time - startTime) * ability.abilityEffect.speed;
                float fracJourney = distCovered / journeyLength;
                newPlayerTransform.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);
                //instantiatedParticle.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);

                targetTransform = newPlayerTransform.transform;
                instantiatedParticle.transform.position = newPlayerTransform.transform.position;
                yield return null;
            }
            targetTransform = newPlayerTransform.transform;

        }
    }

    protected virtual IEnumerator SetBulletLikeTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {

        ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = playerTransform.transform.position;//ability.abilityEffect.targetTransform.position;
        Transform newPlayerTransform = GetTargetTransform(transform);

        Vector3 direction = (targetPosition - startPosition).normalized;
        newPlayerTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
        GameObject hasFoundTarget = null;
        while (Time.time < startTime + ability.abilityEffect.lifeSpan)
        {
            newPlayerTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
            targetTransform = newPlayerTransform.transform;
            instantiatedParticle.transform.position = newPlayerTransform.transform.position;
            if (hasFoundTarget == null)
                hasFoundTarget = ability.abilityEffect.CheckContactCollider(targetTransform, attackCast, this.gameObject);
            if (hasFoundTarget)
            {
                if (instantiatedParticle) Destroy(instantiatedParticle);
                targetTransform = newPlayerTransform.transform;
                if (ability.abilityEffect.multiAreaEffect)
                    ApplyAbilityUse(ability, attackCast, instantiatedParticle);
                else ApplyAbilityUse(ability, attackCast, instantiatedParticle, hasFoundTarget);
                yield break;
            }
            yield return null;
        }
        if (instantiatedParticle) Destroy(instantiatedParticle);
        targetTransform = newPlayerTransform.transform;
        ApplyAbilityUse(ability, attackCast, instantiatedParticle);
    }

    public virtual IEnumerator SetPermanentTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {
        ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.finalLaunchTime)
            {
                SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, true);
                yield return null;
            }

        ApplyAbilityUse(ability, attackCast, instantiatedParticle);
    }

    public virtual IEnumerator DelayedSetTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AttackCast attackCast = null)
    {
        if (!ability.abilityEffect.shouldMarkAtCast) SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle);
        ability.abilityState = AbilityHolder.AbilityState.Launching;
        yield return new WaitForSeconds(ability.abilityEffect.finalLaunchTime);
        ApplyAbilityUse(ability, attackCast, instantiatedParticle);
    }
    public virtual void ApplyAbilityUse(AbilityHolder ability, AttackCast attackCast = null, GameObject instantiatedParticle = null, GameObject affectedTarget = null)
    {
        //ability.abilityEffect.Use();
        foreach (var effect in ability.abilityEffect.effects)
        {

            if (effect.attackCast == null) effect.attackCast = new List<AttackCast> { attackCast };
            if (effect.enemyEffect == false)
            {
                if(ability.abilityEffect.isSelfTargetOrCasterReceivesBeneffitsBuffsEvenFromFarAway) ability.abilityEffect.Use(this.gameObject, effect);
                else ability.abilityEffect.Use(targetTransform,effect, effect.attackCast);

            }

            else
            {
                if (ability.abilityEffect.multiAreaEffect)
                {
                    if (ability.abilityEffect.casterIsImune) ability.abilityEffect.Use(targetTransform, effect, effect.attackCast, false, null, this.gameObject);
                    else if (affectedTarget) ability.abilityEffect.Use(targetTransform, effect, effect.attackCast, affectedTarget);
                    else ability.abilityEffect.Use(targetTransform, effect, effect.attackCast, false);
                }
                else
                {
                    if (ability.abilityEffect.casterIsImune) ability.abilityEffect.Use(targetTransform, effect, effect.attackCast, true, null, this.gameObject);
                    else ability.abilityEffect.Use(targetTransform, effect, effect.attackCast, true);
                }

            }
            
        }
        ability.abilityState = AbilityHolder.AbilityState.Active;
        ability.activeTime = Time.time;
        targetTransform = transform;
        StartCoroutine(ActiveAbilityRoutine(ability, instantiatedParticle));
    }

}
