using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ReadyState : AbilityState
{
    private AbilityStateMachine.EAbilityState _nextStateKey;
    private bool _useStateTransitionFlag = false;

    public ReadyState(AbilityContext context, AbilityStateMachine.EAbilityState estate)
        : base(context, estate) { }

    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Ready);
    }

    public override void UpdateState() { }

    public override void ExitState()
    {
        _useStateTransitionFlag = false; // Reset transition flag when leaving state
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


    public override AbilityStateMachine.EAbilityState GetNextState()
    {
        if (_useStateTransitionFlag) return _nextStateKey;

        // Only trigger ability if we're not already processing one
        if (Context.triggered && !Context.abilityStillInProgress)
        {
            StartAbility();
            Context.triggered = false; // Consume the trigger
        }

        return StateKey;
    }

    private void StartAbility()
    {
        PlayerAbilityHolder ability = Context.AbilityHolder;
        AttackCast primaryCast = ability.attackCast[0];

        // Handle different ability types
        if (ability.abilityEffect.numberOfTargets > 1)
        {
            // Multi-target abilities get their own attack casts
            foreach (var cast in ability.attackCast)
            {
                ExecuteAbility(Context.AbilityController.transform, cast);
            }
        }
        else if (ability.abilityEffect.doesAbilityNeedsConfirmationClickToLaunch)
        {
            // Abilities requiring click confirmation start coroutine
            Context.abilityStillInProgress = true;
            Context.isWaitingForClick = true;
            Context.AbilityController.StartCoroutine(HandleClickConfirmation(ability, primaryCast));
        }
        else
        {
            // Immediate execution for non-interactive abilities
            ExecuteAbility(Context.AbilityController.transform, primaryCast);
        }
    }

    private IEnumerator HandleClickConfirmation(PlayerAbilityHolder ability, AttackCast attackCast)
    {
        Mouse mouse = Mouse.current;
        bool confirmed = false;

        // Input loop with exit conditions
        while (Context.isWaitingForClick && !confirmed)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Left click confirms ability execution
                confirmed = true;
                Debug.Log("Ability confirmed at mouse position");
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                // Right click cancels the ability
                Debug.Log("Ability activation cancelled");
                ResetAbilityState();
                yield break;
            }

            yield return null; // Wait until next frame
        }

        if (confirmed)
        {
            // Find target position through raycasting
            ProcessTargetedAbility(attackCast);
            ResetAbilityState();
        }
    }

    private void ProcessTargetedAbility(AttackCast attackCast)
    {
        // Camera-based targeting system
        if (Camera.main == null) return;

        RaycastHit hit;
        Ray targetingRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Physics check for valid target
        if (Physics.Raycast(targetingRay, out hit, attackCast.castSize))
        {
            ExecuteAbility(hit.transform, attackCast);
        }
    }

    private void  ExecuteAbility(Transform target, AttackCast attackCast)
    {
        // Particle system initialization
        Context.instantiatedParticle = Object.Instantiate(Context.AbilityHolder.abilityEffect.particle);
        Context.instantiatedParticle.transform.position = Context.AbilityController.transform.position;

        // Special case: mark initial cast position
        if (Context.AbilityHolder.abilityEffect.shouldMarkAtCast)
        {
            Context.targetTransform = GetTargetTransform(target);
        }

        // Delegate particle timing to context
        Context.SetParticleDuration(Context.instantiatedParticle, Context.AbilityHolder, attackCast);

        // Determine next state based on cast duration
        _nextStateKey = Context.AbilityHolder.abilityEffect.castDuration > 0
            ? AbilityStateMachine.EAbilityState.Casting
            : AbilityStateMachine.EAbilityState.Launching;

        _useStateTransitionFlag = true; // Flag for state machine transition
    }

    private void ResetAbilityState()
    {
        // Cleanup for cancellation or completion
        Context.abilityStillInProgress = false;
        Context.isWaitingForClick = false;
    }

}