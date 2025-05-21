using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Threading.Tasks;
public class PlayerAbilityController : MonoBehaviour// BaseAbilityController<PlayerAbilityHolder>
{
    //    [SerializeField] private PlayerMovementModel movementModel;
    //    [SerializeField] private List<PlayerAbilityHolder> abilities;
    //    [Tooltip("the target has been selected")][SerializeField] public bool isWaitingForClick;
    //    [Tooltip("the ability is waiting to have a selected target to execute the ability")][SerializeField] public bool abilityStillInProgress;
    //    public List<PlayerAbilityHolder> Abilities { get { return abilities; } }


    //    private void OnDrawGizmos()
    //    {
    //        foreach (var ability in abilities)
    //            if (ability.targetTransform)
    //                foreach (var attackjhonson in ability.attackCast)
    //                    attackjhonson.DrawGizmos(ability.targetTransform);



    //    }
    //    private void Awake()
    //    {
    //        if (!movementModel) movementModel = GetComponent<PlayerMovementModel>();
    //        if (!abilitiesStateMachine) abilitiesStateMachine = GetComponent<AbilitiesStateMachine>();
    //        if (!targetTransform) targetTransform = transform;
    //        foreach (var ability in abilities)
    //            if (!ability.targetTransform) ability.targetTransform = transform;


    //    }
    //    public PlayerAbilityHolder FindAbilityByInputAction(InputAction inputAction)
    //    {
    //        return abilities.FirstOrDefault(ability =>
    //        ability.AbilityActionReference != null &&
    //        ability.AbilityActionReference.action == inputAction);
    //    }

    //    public void CheckAbilitiesNew(InputAction inputAction)
    //    {
    //        if (abilitiesStateMachine.CurrentState.StateKey != AbilitiesStateMachine.EAbilitiesState.Available) return;
    //        //PlayerAbilityHolder ability = FindAbilityByInputAction(inputAction);
    //        AbilityAction abilityAction = abilitiesStateMachine.FindAbilityActionByInputAction(inputAction);
    //        if (abilityAction.AbilityStateMachine.CurrentState.StateKey != AbilityStateMachine.EAbilityState.Ready) return;
    //        PlayerAbilityHolder ability = abilityAction.AbilityStateMachine.AbilityHolder;

    //        AttackCast attackCast = ability.attackCast.First();

    //        if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
    //            foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(ability, transform, eachaAttackCast);
    //        else if (ability.abilityEffect.shouldLaunch)
    //            _ = SetAbilityActions(ability, transform, attackCast);
    //        else if (!abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
    //        {
    //            abilityStillInProgress = true;
    //            isWaitingForClick = true;
    //            StartCoroutine(WaitForClickRoutine(ability, attackCast));

    //        }
    //        else if (ability.abilityEffect.isFixedPosition)
    //        {
    //            _ = SetAbilityActions(ability, transform, attackCast);
    //        }


    //    }






    public virtual async Task SetParticleDuration(GameObject instantiatedParticle, AbilityHolder ability, AttackCast attackCast = null)
    {
        ParticleSystem particleSystem = instantiatedParticle.GetComponent<ParticleSystem>();
        if (particleSystem.isPlaying)
        {
            particleSystem.Stop(true);
        }
        ParticleSystem.MainModule mainModule = particleSystem.main;

       // Set start delay and duration before starting the particle system
        mainModule.startDelay = 0;

        float duration;
        if (ability.abilityEffect.shouldLaunch)
            duration = ability.abilityEffect.lifeSpan;

        else if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast)
            duration = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;

        else duration = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
        mainModule.startDelay = ability.abilityEffect.castDuration;


        mainModule.duration = duration;
        mainModule.startLifetime = duration;
      //  Set sub-particle system durations
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
                if (attackCast.castType == AttackCast.CastType.Box)
                {
                    if (mainModuleSubParticle.startSizeX.constant < attackCast.boxSize.x && mainModuleSubParticle.startSizeZ.constant < attackCast.boxSize.z && mainModuleSubParticle.startSizeY.constant < attackCast.boxSize.y)
                        ChangeParticleSize(mainModuleSubParticle, attackCast);
                }
                else if (mainModuleSubParticle.startSizeX.constant < attackCast.castSize && mainModuleSubParticle.startSizeZ.constant < attackCast.castSize && mainModuleSubParticle.startSizeY.constant < attackCast.castSize)
                    ChangeParticleSize(mainModuleSubParticle, attackCast);
            }
        }
        if (ability.abilityEffect.particleShouldChangeSize) ChangeParticleSize(mainModule, attackCast);
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










    //    public void CheckAbilities(InputAction inputAction)
    //    {
    //        foreach (var ability in abilities)
    //        {
    //            if (ability.abilityEffect != null && ability.AbilityActionReference.action == inputAction && ability.abilityState == BaseAbilityHolder.AbilityState.Ready)
    //            {
    //                AttackCast attackCast = ability.attackCast.First();
    //                if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
    //                    foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(ability, transform, eachaAttackCast);

    //                else if (ability.abilityEffect.shouldLaunch)
    //                    _ = SetAbilityActions(ability, transform, attackCast);
    //                else if (!abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
    //                {
    //                    abilityStillInProgress = true;
    //                    isWaitingForClick = true;
    //                    StartCoroutine(WaitForClickRoutine(ability, attackCast));

    //                }
    //                else if (ability.abilityEffect.isFixedPosition)
    //                {
    //                    _ = SetAbilityActions(ability, transform, attackCast);
    //                }
    //            }
    //        }
    //    }
    //    public void CheckAbilities2(InputAction inputAction, AbilityStateMachine abilityStateMachine)
    //    {
    //        foreach (var ability in abilities)
    //        {
    //            if (ability.abilityEffect != null && ability.AbilityActionReference.action == inputAction)
    //            {
    //                AttackCast attackCast = ability.attackCast.First();
    //                if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
    //                    foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(ability, transform, abilityStateMachine, eachaAttackCast);

    //                else if (ability.abilityEffect.shouldLaunch)
    //                    _ = SetAbilityActions(ability, transform, abilityStateMachine, attackCast);
    //                else if (!abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
    //                {
    //                    abilityStillInProgress = true;
    //                    isWaitingForClick = true;
    //                    StartCoroutine(WaitForClickRoutine(ability, attackCast));

    //                }
    //                else if (ability.abilityEffect.isFixedPosition)
    //                {
    //                    _ = SetAbilityActions(ability, transform, abilityStateMachine, attackCast);
    //                }
    //            }
    //        }
    //    }
    //    protected override IEnumerator SetBulletLikeTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AbilityStateMachine abilityStateMachine, AttackCast attackCast = null)
    //    {
    //        //ability.abilityState = AbilityHolder.AbilityState.Launching;
    //        abilityStateMachine.TransitionToState(AbilityStateMachine.EAbilityState.Launching);
    //        float startTime = Time.time;

    //        ability.targetTransform = GetTargetTransform(playerTransform);
    //        // Calculate the direction from player to mouse position
    //        Vector3 cameraForward = Camera.main.transform.forward;
    //        if (ability.abilityEffect.isGroundFixedPosition) cameraForward.y = 0f; // Ignore vertical component
    //        else cameraForward.y *= 0.33f;
    //        Vector3 direction = cameraForward.normalized;
    //        ability.targetTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
    //        GameObject hasFoundTarget = null;
    //        while (Time.time < startTime + ability.abilityEffect.lifeSpan)
    //        {
    //            // Update the target position based on the object's transform
    //            ability.targetTransform.position += direction * ability.abilityEffect.speed * Time.deltaTime;
    //            instantiatedParticle.transform.position = ability.targetTransform.transform.position;
    //            if (hasFoundTarget == null)
    //                hasFoundTarget = ability.abilityEffect.CheckContactCollider(ability.targetTransform, attackCast, this.gameObject);
    //            if (hasFoundTarget)
    //            {
    //                if (instantiatedParticle) Destroy(instantiatedParticle);
    //                targetTransform = ability.targetTransform.transform;
    //                if (ability.abilityEffect.multiAreaEffect)
    //                    ApplyAbilityUse(ability, attackCast, instantiatedParticle);
    //                else ApplyAbilityUse(ability, attackCast, instantiatedParticle, hasFoundTarget);
    //                yield break;
    //            }

    //            yield return null;
    //        }

    //        // If it reaches here, it means the ability expired without hitting a target
    //        // Destroy the particle if it exists
    //        if (instantiatedParticle) Destroy(instantiatedParticle);
    //        targetTransform = ability.targetTransform.transform;
    //        // Apply the ability use
    //        ApplyAbilityUse(ability, attackCast);
    //    }
    //    private void ProcessRayCastAbility(PlayerAbilityHolder ability, AttackCast attackCast)
    //    {
    //        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

    //        if (Physics.Raycast(ray, out RaycastHit hit, attackCast.castSize))
    //        {
    //            if (hit.collider != null)
    //                _ = SetAbilityActions(ability, hit.collider.transform, attackCast);
    //        }
    //    }
    //    protected Vector3 GetMousePosition()
    //    {
    //        // get mouse position
    //        Vector2 mousePosition = Mouse.current.position.ReadValue();

    //        // converting mouse position to wolrd position
    //        Vector3 worldMousePosition = Camera.main.ScreenToWorldPoint(mousePosition);

    //        return worldMousePosition;
    //    }

    //    public virtual IEnumerator WaitForClickRoutine(PlayerAbilityHolder ability, AttackCast attackCast)
    //    {
    //        while (isWaitingForClick)
    //            yield return null;
    //        abilityStillInProgress = false;
    //        isWaitingForClick = false;
    //        ProcessRayCastAbility(ability, attackCast);

    //    }




}
