﻿using System.Collections;
using UnityEditor.Playables;
using UnityEngine;

public class CastingState : AbilityState
{
    private bool _goToNextState = false;
    private Coroutine _dynamicTrackingRoutine;
    private Coroutine _staticMarkRoutine;


    public CastingState(AbilityContext context, AbilityStateMachine.EAbilityState estate)
        : base(context, estate) { }

    public override void EnterState()
    {
        // Validate state transition permissions
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Casting);

        PlayerAbilityHolder ability = Context.AbilityHolder;

        // Check if the ability requires dynamic target tracking (e.g., follows the caster)
        bool needsDynamicTracking =
            ability.abilityEffect.isPartialPermanentTargetWhileCasting ||
            ability.abilityEffect.isFixedPosition ||
            ability.abilityEffect.singleTargetSelfTarget;

        if (needsDynamicTracking)
            StartDynamicTrackingCoroutine();

        // Handle static position marking (e.g., AoE placed at cast start)
        if (ability.abilityEffect.shouldMarkAtCast)
            StartStaticMarkCoroutine();
    }

    public override void ExitState()
    {
        // Stop all active routines to prevent memory leaks
        if (_dynamicTrackingRoutine != null)
            Context.AbilityController.StopCoroutine(_dynamicTrackingRoutine);
        if (_staticMarkRoutine != null)
            Context.AbilityController.StopCoroutine(_staticMarkRoutine);

        _dynamicTrackingRoutine = null;
        _staticMarkRoutine = null;

        _goToNextState = false;
    }


    public override AbilityStateMachine.EAbilityState GetNextState() =>
        _goToNextState ? AbilityStateMachine.EAbilityState.Launching : StateKey;

    public override void LateUpdateState() { }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void UpdateState() { }

    private void TriggerStateTransition() => _goToNextState = true;

    // Starts coroutine to track dynamic targets (e.g., abilities that follow the caster)
    private void StartDynamicTrackingCoroutine() =>
        _dynamicTrackingRoutine = Context.AbilityController.StartCoroutine(DynamicTrackingRoutine());

    // Starts coroutine to mark a static position at cast start (e.g., ground-targeted abilities)
    private void StartStaticMarkCoroutine() =>
        _staticMarkRoutine = Context.AbilityController.StartCoroutine(StaticMarkRoutine());

    // Updates visuals continuously during casting (e.g., projectile spawners)
    private IEnumerator DynamicTrackingRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;
        float castDuration = ability.abilityEffect.castDuration;
        float startTime = Time.time;

        // Update target position every frame during casting
        while (Time.time <= startTime + castDuration)
        {
            SetGizmosAndColliderAndParticlePosition(true);
            yield return null;
        }

        // Lock final position and trigger state transition
        SetGizmosAndColliderAndParticlePosition(false);
        TriggerStateTransition();
    }

    // Marks a fixed position at cast start (e.g., delayed AoE)
    private IEnumerator StaticMarkRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;

        // Set initial position once
        if (ability.abilityEffect.shouldMarkAtCast)
            SetGizmosAndColliderAndParticlePosition();

        // Wait for cast duration without updates
        yield return WaitForSeconds(ability.abilityEffect.castDuration);
        TriggerStateTransition();
    }

    // Reusable wait-for-duration logic
    private IEnumerator WaitForSeconds(float duration)
    {
        float startTime = Time.time;
        while (Time.time < startTime + duration)
            yield return null;
    }
}