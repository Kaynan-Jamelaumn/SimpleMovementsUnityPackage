using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackExecutor
{
    private WeaponController controller;
    private AttackState attackState = new AttackState();
    private Coroutine currentAttackCoroutine;
    private readonly HashSet<Collider> hitTargets = new HashSet<Collider>();

    // Dependencies
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;
    private AttackAnimationHandler animationHandler;
    private WeaponEffectsManager effectsManager;

    // Properties
    public bool IsAttacking => attackState.IsActive || (controller.GetAnimController()?.Model?.IsAttacking ?? false);
    public AttackAction CurrentAttackAction => attackState.Action;
    public AttackVariation CurrentAttackVariation => attackState.Variation;
    public IAttackComponent CurrentAttackComponent => attackState.CurrentComponent;

    public AttackExecutor(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetDependencies(ComboSystem comboSystem, VariationSystem variationSystem,
        InputBufferSystem inputBufferSystem, AttackAnimationHandler animationHandler,
        WeaponEffectsManager effectsManager)
    {
        this.comboSystem = comboSystem;
        this.variationSystem = variationSystem;
        this.inputBufferSystem = inputBufferSystem;
        this.animationHandler = animationHandler;
        this.effectsManager = effectsManager;
    }

    public void PerformAttack(GameObject player, AttackType attackType)
    {
        controller.LogDebug($"PerformAttack called with type: {attackType}");

        if (!CanPerformAttack())
        {
            controller.LogDebug("Attack preconditions failed", true);
            return;
        }

        if (attackState.IsActive && controller.EnableInputBuffer)
        {
            controller.LogDebug("Currently attacking, buffering input");
            inputBufferSystem.BufferInput(attackType, player);
            return;
        }

        ExecuteAttack(player, attackType);
    }

    public void ExecuteAttack(GameObject player, AttackType attackType)
    {
        controller.LogDebug($"ExecuteAttack: {attackType}");

        // Check if combo should be triggered
        if (controller.EnableComboSystem && controller.EquippedWeapon.HasCombos() &&
            comboSystem.TryExecuteCombo(player, attackType))
        {
            return;
        }

        // Get the appropriate action and variation
        var (action, variation) = variationSystem.GetAttackActionWithVariation(attackType);
        if (action == null)
        {
            controller.LogDebug($"No action available for type: {attackType}", true);
            return;
        }

        // Update variation state
        variationSystem.UpdateVariationState(attackType, action);

        // Update combo sequence
        comboSystem.UpdateComboSequence(attackType);

        // Start the attack
        StartAttack(player, action, variation);
    }

    public void StartAttack(GameObject player, AttackAction action, AttackVariation variation = null, ComboSequence combo = null)
    {
        string attackName = variation?.variationName ?? action.actionName;
        controller.LogDebug($"Starting attack: {attackName}");

        attackState.IsActive = true;
        attackState.SetAttackComponent(action, variation);
        attackState.AnimationTime = 0f;
        attackState.IsComboFinisher = combo != null;

        var component = attackState.CurrentComponent;
        ApplyMovementRestrictions(player, component);
        animationHandler.TriggerAttackAnimation(component);

        if (component.ForwardMovement != Vector3.zero)
            controller.StartCoroutine(ApplyForwardMovement(player, component));

        StartAttackCoroutine(player, component);
    }

    private IEnumerator HandleAttackExecution(GameObject player, IAttackComponent component)
    {
        float totalDuration = component.GetTotalDuration();
        float elapsedTime = 0f;

        controller.LogDebug($"Starting attack execution. Duration: {totalDuration}s");

        yield return null; // Wait one frame
        yield return WaitForAnimationLock();

        hitTargets.Clear();

        while (elapsedTime < totalDuration && attackState.IsActive)
        {
            elapsedTime += Time.deltaTime;
            attackState.AnimationTime = elapsedTime / totalDuration;

            if (component.IsInActiveFrames(attackState.AnimationTime))
            {
                PerformCollisionDetection(player, component);
            }

            yield return null;
        }

        EndAttack();
    }

    private void PerformCollisionDetection(GameObject player, IAttackComponent component)
    {
        if (controller.EquippedWeapon?.attackCast == null) return;

        var colliders = controller.EquippedWeapon.attackCast.DetectObjects(controller.HandGameObject.transform);
        foreach (var collider in colliders)
        {
            if (IsValidTarget(collider, player) && !hitTargets.Contains(collider))
            {
                ProcessHit(collider, player, component);
                hitTargets.Add(collider);
            }
        }
    }

    private bool IsValidTarget(Collider collider, GameObject playerObject)
    {
        return collider != null && collider.gameObject != playerObject;
    }

    private void ProcessHit(Collider collider, GameObject player, IAttackComponent component)
    {
        controller.EquippedWeapon.ApplyEffectsToTarget(collider.gameObject, player, component);
        effectsManager.PlayHitEffects(component, collider.transform.position);
        controller.LogDebug($"Hit target: {collider.name}");
    }

    private bool CanPerformAttack()
    {
        return controller.EquippedWeapon != null && controller.GetAnimController() != null && !attackState.IsActive;
    }

    private void ApplyMovementRestrictions(GameObject player, IAttackComponent component)
    {
        var playerMovement = player.GetComponent<PlayerMovement>();
        if (playerMovement == null) return;

        controller.LogDebug(component.LockMovement ? "Locking player movement" :
                $"Setting movement speed multiplier: {component.MovementSpeedMultiplier}");
        // Implement movement restrictions based on your PlayerMovement component
    }

    private IEnumerator ApplyForwardMovement(GameObject player, IAttackComponent component)
    {
        var rigidbody = player.GetComponent<Rigidbody>();
        if (rigidbody == null) yield break;

        float duration = component.StartupFrames + component.ActiveFrames;
        float elapsed = 0f;
        Vector3 frameMovement = component.ForwardMovement * Time.fixedDeltaTime / duration;

        while (elapsed < duration)
        {
            rigidbody.MovePosition(rigidbody.position + player.transform.forward * frameMovement.magnitude);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private void StartAttackCoroutine(GameObject player, IAttackComponent component)
    {
        if (currentAttackCoroutine != null) controller.StopCoroutine(currentAttackCoroutine);
        currentAttackCoroutine = controller.StartCoroutine(HandleAttackExecution(player, component));
    }

    private IEnumerator WaitForAnimationLock()
    {
        float lockWaitTime = 0f;
        var animController = controller.GetAnimController();
        while (lockWaitTime < 0.1f && !animController.IsAttackAnimationLocked())
        {
            lockWaitTime += Time.deltaTime;
            yield return null;
        }

        controller.LogDebug(animController.IsAttackAnimationLocked() ?
            "Attack animation successfully locked" :
            "Warning: Attack animation may not be properly locked");
    }

    public void ResetAttackState()
    {
        attackState.Reset();
        hitTargets.Clear();
    }

    public void EndAttack()
    {
        controller.LogDebug("Ending attack");
        ResetAttackState();
        currentAttackCoroutine = null;
    }

    public void CleanupAllCoroutines()
    {
        if (currentAttackCoroutine != null)
        {
            controller.StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }
    }

    public void DrawDebugInfo(Transform handTransform)
    {
        if (attackState.CurrentComponent == null) return;

        Gizmos.color = Color.green;
        Vector3 labelPos = handTransform.position + Vector3.up * 2f;

#if UNITY_EDITOR
        string attackName = (attackState.CurrentComponent as AttackVariation)?.variationName ??
                           (attackState.CurrentComponent as AttackAction)?.actionName ?? "Unknown";
        string info = $"Attack: {attackName}\n" +
                     $"Progress: {attackState.AnimationTime:F2}\n" +
                     $"Weapon: {(attackState.IsActive ? "ACTIVE" : "IDLE")}\n" +
                     $"Animation: {(controller.GetAnimController()?.IsAttackAnimationLocked() == true ? "LOCKED" : "UNLOCKED")}\n" +
                     $"Model: {(controller.GetAnimController()?.Model?.IsAttacking == true ? "ATTACKING" : "IDLE")}\n" +
                     $"Variation: {(attackState.Variation != null ? "YES" : "NO")}";

        UnityEditor.Handles.Label(labelPos, info);
#endif
    }

    // Nested Classes
    private class AttackState
    {
        public bool IsActive;
        public AttackAction Action;
        public AttackVariation Variation;
        public IAttackComponent CurrentComponent; // Unified reference
        public float AnimationTime;
        public bool IsComboFinisher;

        public void Reset()
        {
            IsActive = false;
            Action = null;
            Variation = null;
            CurrentComponent = null;
            AnimationTime = 0f;
            IsComboFinisher = false;
        }

        public void SetAttackComponent(AttackAction action, AttackVariation variation = null)
        {
            Action = action;
            Variation = variation;
            CurrentComponent = variation ?? (IAttackComponent)action;
        }
    }
}