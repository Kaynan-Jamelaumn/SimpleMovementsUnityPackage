using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class ReadyState : AbilityState
{
    AbilityStateMachine.EAbilityState stateMasterKey;
    bool useStateMasterKey = false;
    public ReadyState(AbilityContext context, AbilityStateMachine.EAbilityState estate) : base(context, estate)
    {
        AbilityContext Context = context;
    }
    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Ready);
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
    }
    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (useStateMasterKey)
        {
            useStateMasterKey = false;

            return stateMasterKey;

        }

        if ( Context.triggered)
            StartAbility(); 
        
        if (Context.abilityStartedActivating) return AbilityStateMachine.EAbilityState.Active;
            return StateKey;

    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }





    public void StartAbility()
    {
        // enemyEffect singleTargetSelfTarget isFixedPosition isPartialPermanentTargetWhileCasting isPermanentTarget shouldMarkAtCast
        // singleTargetselfTarget ability that follows player afftect only player
        // isFixedPosition ability that follows the player until activated
        // isPartialPermanentTargetWhileCasting follows the player until the end of casting(entering launching)
        // shouldMarkAtCast activate the ability at the first position when casting was activated
        // enemyEffect affect non agressive creature true= no
        // doesAbilityNeedsConfirmationClickToLaunch 
        PlayerAbilityHolder ability = Context.AbilityHolder;
        AttackCast attackCast = ability.attackCast[0];
        Context.triggered = false;

        if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
            foreach (var eachaAttackCast in ability.attackCast) _ = SetAbilityActions(Context.AbilityController.transform, eachaAttackCast);
        else if (ability.abilityEffect.shouldLaunch)
            _ = SetAbilityActions(Context.AbilityController.transform, attackCast);
        else if (!Context.abilityStillInProgress && ability.abilityEffect.doesAbilityNeedsConfirmationClickToLaunch)
        {
            Debug.Log("B");
            Context.abilityStillInProgress = true;
            Context.isWaitingForClick = true;
            Context.AbilityController.StartCoroutine(WaitForClickRoutine(ability, attackCast));

        }
        else
        {
            Debug.Log("A");
            _ = SetAbilityActions(Context.AbilityController.transform, attackCast);
        }
    }


    public virtual IEnumerator WaitForClickRoutine(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        // Get the Mouse device from the new Input System
        Mouse mouse = Mouse.current;
        bool clicked = false;

        while (Context.isWaitingForClick && !clicked)
        {
            // Check for left mouse button click (button 0)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Debug.Log("Left mouse button clicked - proceeding with ability");
                clicked = true;
            }
            // Check for right mouse button click (button 1)
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                Debug.Log("Right mouse button clicked - canceling wait");
                Context.isWaitingForClick = false;
                Context.abilityStillInProgress = false;
                yield break; // Exit the coroutine without processing the ability
            }

            yield return null;
        }

        if (clicked)
        {
            Debug.Log("Processing ability after click confirmation");
            Context.abilityStillInProgress = false;
            Context.isWaitingForClick = false;
            ProcessRayCastAbility(ability, attackCast);
        }
    }



    //public virtual IEnumerator WaitForClickRoutine(PlayerAbilityHolder ability, AttackCast attackCast)
    //{
    //    while (Context.isWaitingForClick)
    //        yield return null;
    //    Debug.Log("C");
    //    Context.abilityStillInProgress = false;
    //    Context.isWaitingForClick = false;
    //    ProcessRayCastAbility(ability, attackCast);

    //}



    private void ProcessRayCastAbility(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, attackCast.castSize))
        {
            if (hit.collider != null)
                _ = SetAbilityActions(hit.collider.transform, attackCast);
        }
    }





    protected virtual async Task SetAbilityActions(Transform abilityTargetTransform, AttackCast attackCast = null)
    {
        PlayerAbilityHolder ability = Context.AbilityHolder;
        Transform targetedTransform = abilityTargetTransform;
        GameObject instantiatedParticle = UnityEngine.Object.Instantiate(ability.abilityEffect.particle);

        Debug.Log(instantiatedParticle.gameObject.name);


        if (ability.abilityEffect.shouldMarkAtCast)
        {
            ability.targetTransform = GetTargetTransform(targetedTransform);
            Context.targetTransform = ability.targetTransform;
        }
        await Context.SetParticleDuration(instantiatedParticle, ability, attackCast);
        instantiatedParticle.transform.position = Context.AbilityController.transform.position;

        if (ability.abilityEffect.castDuration != 0)
        {
            stateMasterKey = AbilityStateMachine.EAbilityState.Casting;
            useStateMasterKey = true;
            Debug.LogError("3");
        }
        else
        {
            Debug.LogError("2");
            stateMasterKey = AbilityStateMachine.EAbilityState.Launching;
            useStateMasterKey = true;
        }
    }

}