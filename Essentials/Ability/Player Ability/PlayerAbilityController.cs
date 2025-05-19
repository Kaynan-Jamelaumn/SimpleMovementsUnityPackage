﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Threading.Tasks;
public class PlayerAbilityController : BaseAbilityController<PlayerAbilityHolder>
{
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private List<PlayerAbilityHolder> abilities;
    [Tooltip("the target has been selected")][SerializeField] public bool isWaitingForClick;
    [Tooltip("the ability is waiting to have a selected target to execute the ability")][SerializeField] public bool abilityStillInProgress;
    public List<PlayerAbilityHolder> Abilities { get { return abilities; }   }


    private void OnDrawGizmos()
    {
        foreach (var ability in abilities)
            if (ability.targetTransform)
                foreach (var attackjhonson in ability.attackCast)
                    attackjhonson.DrawGizmos(ability.targetTransform);
                
            

    }
    private void Awake()
    {
        if (! movementModel) movementModel = GetComponent<PlayerMovementModel>();
        if (!abilitiesStateMachine) abilitiesStateMachine = GetComponent<AbilitiesStateMachine>();
        if (!targetTransform) targetTransform = transform;
        foreach (var ability in abilities)
            if (!ability.targetTransform) ability.targetTransform = transform;
        

    }

    public void CheckAbilities(InputAction inputAction)
    {
        foreach (var ability in abilities)
        {
            if (ability.abilityEffect != null && ability.AbilityActionReference.action == inputAction && ability.abilityState == BaseAbilityHolder.AbilityState.Ready)
            {   
                AttackCast attackCast = ability.attackCast.First();
                if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
                    foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(ability, transform, eachaAttackCast);
                
                else if (ability.abilityEffect.shouldLaunch)
                    _ = SetAbilityActions(ability, transform, attackCast);
                else if (!abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
                {
                    abilityStillInProgress = true;
                    isWaitingForClick = true;
                    StartCoroutine(WaitForClickRoutine(ability, attackCast));
                    
                }
                else if (ability.abilityEffect.isFixedPosition)
                {
                    _ = SetAbilityActions(ability, transform, attackCast);
                }
            }
        }
    }
    public void CheckAbilities2(InputAction inputAction, AbilityStateMachine abilityStateMachine)
    {
        foreach (var ability in abilities)
        {
            if (ability.abilityEffect != null && ability.AbilityActionReference.action == inputAction)
            {
                AttackCast attackCast = ability.attackCast.First();
                if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
                    foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(ability, transform, abilityStateMachine, eachaAttackCast);

                else if (ability.abilityEffect.shouldLaunch)
                    _ = SetAbilityActions(ability, transform, abilityStateMachine, attackCast);
                else if (!abilityStillInProgress && !ability.abilityEffect.isFixedPosition)
                {
                    abilityStillInProgress = true;
                    isWaitingForClick = true;
                    StartCoroutine(WaitForClickRoutine(ability, attackCast));

                }
                else if (ability.abilityEffect.isFixedPosition)
                {
                    _ = SetAbilityActions(ability, transform, abilityStateMachine, attackCast);
                }
            }
        }
    }
    protected override IEnumerator SetBulletLikeTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, AbilityStateMachine abilityStateMachine, AttackCast attackCast = null)
    {
        //ability.abilityState = AbilityHolder.AbilityState.Launching;
        abilityStateMachine.TransitionToState(AbilityStateMachine.EAbilityState.Launching);
        float startTime = Time.time;

        ability.targetTransform = GetTargetTransform(playerTransform);
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
            instantiatedParticle.transform.position = ability.targetTransform.transform.position;
            if (hasFoundTarget == null)
                hasFoundTarget = ability.abilityEffect.CheckContactCollider(ability.targetTransform, attackCast, this.gameObject);
            if (hasFoundTarget)
            {
                if (instantiatedParticle) Destroy(instantiatedParticle);
                targetTransform = ability.targetTransform.transform;
                if (ability.abilityEffect.multiAreaEffect)
                    ApplyAbilityUse(ability, attackCast, instantiatedParticle);
                else ApplyAbilityUse(ability, attackCast, instantiatedParticle, hasFoundTarget);
                yield break;
            }

            yield return null;
        }

        // If it reaches here, it means the ability expired without hitting a target
        // Destroy the particle if it exists
        if (instantiatedParticle) Destroy(instantiatedParticle);
        targetTransform = ability.targetTransform.transform;
        // Apply the ability use
        ApplyAbilityUse(ability, attackCast);
    }
    private void ProcessRayCastAbility(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, attackCast.castSize))
        {
            if (hit.collider != null)
                _ = SetAbilityActions(ability, hit.collider.transform, attackCast);
        }
    }
    protected Vector3 GetMousePosition()
    {
        // get mouse position
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // converting mouse position to wolrd position
        Vector3 worldMousePosition = Camera.main.ScreenToWorldPoint(mousePosition);

        return worldMousePosition;
    }

    public virtual IEnumerator WaitForClickRoutine(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        while (isWaitingForClick)
            yield return null;
        abilityStillInProgress = false;
        isWaitingForClick = false;
        ProcessRayCastAbility(ability, attackCast);

    }




































}
