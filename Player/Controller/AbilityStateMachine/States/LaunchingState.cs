using System.Collections;
using UnityEditor.Playables;
using UnityEngine;

public class LaunchingState : AbilityState
{
    private bool _goToNextState = false;
    private Coroutine _bulletRoutine;
    private Coroutine _trackingTargetRoutine;
    private Coroutine _setAtLaunchRoutine;

    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate)
        : base(context, estate) { }

    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Launching);
        ChooseLaunchRoutine();  // Critical: Entry point for all launch behaviors
    }

    public override void ExitState()   { 
        
        // Stop all active routines to prevent memory leaks
        if (_bulletRoutine != null)
            Context.AbilityController.StopCoroutine(_bulletRoutine);
        if (_trackingTargetRoutine != null)
            Context.AbilityController.StopCoroutine(_trackingTargetRoutine);
        if (_setAtLaunchRoutine != null)
            Context.AbilityController.StopCoroutine(_setAtLaunchRoutine);

        _bulletRoutine = null;
        _trackingTargetRoutine = null;
        _setAtLaunchRoutine = null;

        _goToNextState = false;
}
public override AbilityStateMachine.EAbilityState GetNextState() =>
        _goToNextState ? AbilityStateMachine.EAbilityState.Active : StateKey;



    // Empty overrides preserved for state machine interface compliance
    public override void UpdateState() { }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
    private void TriggerStateTransition() => _goToNextState = true;


    private void ChooseLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;

        // Critical logic branch: Determines projectile tracking behavior
        if (ability.abilityEffect.shouldLaunch)
            _bulletRoutine = Context.AbilityController.StartCoroutine(BulletLikeLaunchRoutine());
        else if (ability.abilityEffect.isFixedPosition)
            _trackingTargetRoutine = Context.AbilityController.StartCoroutine(PermanentTargetLaunchRoutine());
        else
            _setAtLaunchRoutine = Context.AbilityController.StartCoroutine(DelayedLaunchRoutine());
    }

    private IEnumerator PermanentTargetLaunchRoutine()
    {
        // Continuously updates position until launch time expires
        float startTime = Time.time;
        float duration = Context.AbilityHolder.abilityEffect.finalLaunchTime;

        while (Time.time <= startTime + duration)
        {
            SetGizmosAndColliderAndParticlePosition(true);  // Critical: Real-time position tracking
            yield return null;
        }
        FinalizeAbility();
    }

    private IEnumerator DelayedLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;

        // Critical: Only set initial position if not marked at cast
        if (!ability.abilityEffect.shouldMarkAtCast)
            SetGizmosAndColliderAndParticlePosition();

        yield return new WaitForSeconds(ability.abilityEffect.finalLaunchTime);
        FinalizeAbility();
    }

    private IEnumerator BulletLikeLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;
        ability.targetTransform = GetTargetTransform(Context.targetTransform);
        Vector3 direction = CalculateLaunchDirection(ability);
        GameObject hitTarget = null;
        float startTime = Time.time;

        // Critical loop: Manages projectile lifetime and movement
        while (Time.time < startTime + ability.abilityEffect.lifeSpan)
        {
            MoveProjectile(ability, direction);
            hitTarget = CheckCollisions(ability, hitTarget);

            if (hitTarget != null)
            {
                HandleProjectileImpact(ability, hitTarget);
                yield break;  // Exit early on hit
            }
            yield return null;
        }
        HandleProjectileExpiration(ability);  // Time-based expiration
    }

    private Vector3 CalculateLaunchDirection(AbilityHolder ability)
    {
        Vector3 cameraForward = Camera.main.transform.forward;

        // Critical adjustment: Control vertical influence based on ability type
        if (ability.abilityEffect.isGroundFixedPosition)
            cameraForward.y = 0f;  // Lock to horizontal plane
        else
            cameraForward.y *= 0.33f;  // Partial vertical tracking (magic number justified by design)

        return cameraForward.normalized;
    }

    private void MoveProjectile(AbilityHolder ability, Vector3 direction)
    {
        // Applies continuous movement using speed value
        ability.targetTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
        Context.instantiatedParticle.transform.position = ability.targetTransform.position;
    }

    private GameObject CheckCollisions(AbilityHolder ability, GameObject previousHit)
    {
        // Early exit if already found target
        if (previousHit != null) return previousHit;

        return ability.abilityEffect.CheckContactCollider(
            ability.targetTransform,
            Context.attackCast,
            Context.AbilityController.gameObject
        );
    }

    private void HandleProjectileImpact(AbilityHolder ability, GameObject hitTarget)
    {
        CleanupParticle();
        Context.targetTransform = ability.targetTransform;

        // Critical decision: Multi-area vs single-target application
        if (ability.abilityEffect.multiAreaEffect)
            ApplyAbilityUse();
        else
            ApplyAbilityUse(hitTarget);

        TriggerStateTransition();
    }

    private void HandleProjectileExpiration(AbilityHolder ability)
    {
        CleanupParticle();
        Context.targetTransform = ability.targetTransform;
        ApplyAbilityUse();  // Applies effect even if no collision occurred
        TriggerStateTransition();
    }

    private void CleanupParticle()
    {
        if (Context.instantiatedParticle)
            Object.Destroy(Context.instantiatedParticle);
    }


    private void FinalizeAbility()
    {
        TriggerStateTransition();
        ApplyAbilityUse();
    }



    // "legacy"
    public virtual IEnumerator UntilReachesPosition()
    {
        AbilityHolder ability = Context.AbilityHolder;
        float startTime = Time.time;
        if (ability.abilityEffect.shouldLaunch)
        {
            Vector3 startPosition = Context.AbilityController.transform.position;
            Vector3 targetPosition = Context.targetTransform.transform.position;
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            Transform newPlayerTransform = GetTargetTransform(Context.AbilityController.transform);

            while (Time.time < startTime + ability.abilityEffect.finalLaunchTime)
            {
                float distCovered = (Time.time - startTime) * ability.abilityEffect.speed;
                float fracJourney = distCovered / journeyLength;
                newPlayerTransform.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);
                Context.targetTransform = newPlayerTransform.transform;
                Context.instantiatedParticle.transform.position = newPlayerTransform.transform.position;
                yield return null;
            }
            Context.targetTransform = newPlayerTransform.transform;
        }
    }
}