using System.Collections;
using UnityEditor.Playables;
using UnityEngine;

public class LaunchingState : AbilityState
{
    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Launching);
        //if (Context.shouldHaveDelayedLaunchTime) Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine());
        //else Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine());


        AbilityHolder ability = Context.AbilityHolder;
        if (Context.isPermanentTargetOnCast)
        {
            if (ability.abilityEffect.isPermanentTarget)
                Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine());
            else
                Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine());

        }
        else
        {
            if (ability.abilityEffect.shouldLaunch)
                Context.AbilityController.StartCoroutine(SetBulletLikeTargetLaunchRoutine());
            else if (!ability.abilityEffect.shouldLaunch && ability.abilityEffect.isPermanentTarget)
                Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine());
            else
                Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine());
        }

        if (ability.abilityEffect.isPermanentTarget)
            Context.AbilityController.StartCoroutine(SetPermanentTargetLaunchRoutine());
        else
            Context.AbilityController.StartCoroutine(DelayedSetTargetLaunchRoutine());


    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        GetNextState();

    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (Context.abilityStartedActivating) return AbilityStateMachine.EAbilityState.Active;
        return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


    public virtual IEnumerator SetPermanentTargetLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;


        //ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;
        while (Time.time <= startTime + ability.abilityEffect.finalLaunchTime)
        {
            SetGizmosAndColliderAndParticlePosition(true);
            yield return null;
        }

        ApplyAbilityUse();
    }






    public virtual IEnumerator DelayedSetTargetLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;

        if (!ability.abilityEffect.shouldMarkAtCast) SetGizmosAndColliderAndParticlePosition();
        ability.abilityState = AbilityHolder.AbilityState.Launching;
        yield return new WaitForSeconds(ability.abilityEffect.finalLaunchTime);
        ApplyAbilityUse();
    }












    protected IEnumerator SetBulletLikeTargetLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;



        //ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;

        ability.targetTransform = GetTargetTransform(Context.targetTransform);
        // Calculate the direction from player to mouse position
        Vector3 cameraForward = Camera.main.transform.forward;
        if (ability.abilityEffect.isGroundFixedPosition) cameraForward.y = 0f; // Ignore vertical component
        else cameraForward.y *= 0.33f;
        Vector3 direction = cameraForward.normalized;
        ability.targetTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
        GameObject hasFoundTarget = null;
        while (Time.time < startTime + ability.abilityEffect.lifeSpan)
        {
            // Update the target position based on the object's transform
            ability.targetTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
            Context.instantiatedParticle.transform.position = ability.targetTransform.transform.position;
            if (hasFoundTarget == null)
                hasFoundTarget = ability.abilityEffect.CheckContactCollider(ability.targetTransform, Context.attackCast, Context.AbilityController.gameObject);
            if (hasFoundTarget)
            {
                if (Context.instantiatedParticle) Object.Destroy(Context.instantiatedParticle);
                Context.targetTransform = ability.targetTransform.transform;
                if (ability.abilityEffect.multiAreaEffect)
                    ApplyAbilityUse();
                else ApplyAbilityUse(hasFoundTarget);
                yield break;
            }

            yield return null;
        }

        // If it reaches here, it means the ability expired without hitting a target
        // Destroy the particle if it exists
        if (Context.instantiatedParticle) Object.Destroy(Context.instantiatedParticle);
        Context.targetTransform = ability.targetTransform.transform;
        // Apply the ability use
        ApplyAbilityUse();
    }





    public virtual IEnumerator UntilReachesPosition()
    {
        AbilityHolder ability = Context.AbilityHolder;
        //ability.abilityState = AbilityHolder.AbilityState.Launching;
        float startTime = Time.time;
        if (ability.abilityEffect.shouldLaunch)
        {
            Vector3 startPosition = Context.AbilityController.transform.position;
            Vector3 targetPosition = Context.targetTransform.transform.position;//ability.abilityEffect.targetTransform.position; must be player transform
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            Transform newPlayerTransform = GetTargetTransform(Context.AbilityController.transform);
            while (Time.time < startTime + ability.abilityEffect.finalLaunchTime)
            {
                float distCovered = (Time.time - startTime) * ability.abilityEffect.speed;
                float fracJourney = distCovered / journeyLength;
                newPlayerTransform.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);
                //instantiatedParticle.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);

                Context.targetTransform = newPlayerTransform.transform;
                Context.instantiatedParticle.transform.position = newPlayerTransform.transform.position;
                yield return null;
            }
            Context.targetTransform = newPlayerTransform.transform;

        }
    }
}