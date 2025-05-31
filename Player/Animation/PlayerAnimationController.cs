using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerAnimationModel model;
    [SerializeField] private PlayerMovementModel movementModel;
    [SerializeField] private MovementStateMachine movementStateMachine;

    [Header("Animation Settings")]
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private bool useAdvancedBlending = true;

    // Component managers
    private MovementAnimationHandler movementHandler;
    private AttackAnimationManager attackManager;
    private ActionAnimationHandler actionHandler;
    private AnimationEventHandler eventHandler;
    private AnimationUtilityHelper utilityHelper;

    // Animation state tracking
    private readonly Dictionary<string, Coroutine> activeAnimationCoroutines = new Dictionary<string, Coroutine>();

    // Properties - maintaining exact same public interface
    public PlayerAnimationModel Model { get => model; set => model = value; }
    public bool IsAttackAnimationLocked() => attackManager?.IsAttackAnimationLocked() ?? false;

    // Unity Lifecycle
    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        movementStateMachine = this.CheckComponent(movementStateMachine, nameof(movementStateMachine));

        InitializeComponents();
    }

    private void Start() => SetupAnimationEvents();

    private void Update()
    {
        ApplyMovementAnimations();
        ApplyStateAnimations();
        if (model.DebugAnimations) DebugCurrentAnimations();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        activeAnimationCoroutines.Clear();
        ResetAttackAnimation();

        if (model != null)
        {
            model.IsAttacking = false;
            model.IsInputLocked = false;
        }
    }

    // Component Initialization
    private void InitializeComponents()
    {
        // Initialize handlers
        movementHandler = new MovementAnimationHandler(model, movementModel, movementStateMachine, smoothTime, useAdvancedBlending);

        // Initialize MonoBehaviour components
        attackManager = gameObject.AddComponent<AttackAnimationManager>();
        attackManager.Initialize(model);

        actionHandler = gameObject.AddComponent<ActionAnimationHandler>();
        actionHandler.Initialize(model);

        eventHandler = new AnimationEventHandler(model);
        utilityHelper = new AnimationUtilityHelper(model);
    }

    // Animation Events Setup
    private void SetupAnimationEvents() => eventHandler.SetupAnimationEvents();

    // Movement Animation - delegate to handler
    private void ApplyMovementAnimations() => movementHandler.ApplyMovementAnimations();
    private void ApplyStateAnimations() => movementHandler.ApplyStateAnimations();

    // Attack Animation System - maintaining exact same public interface
    public void PlayAttackAnimationWithDuration(AnimationClip animationClip, float weaponAttackDuration, float animationSpeed = 1.0f)
        => attackManager.PlayAttackAnimationWithDuration(animationClip, weaponAttackDuration, animationSpeed);

    public void TriggerAttackAnimationWithDuration(string animationTrigger, float weaponDuration, int comboIndex = 0)
        => attackManager.TriggerAttackAnimationWithDuration(animationTrigger, weaponDuration, comboIndex);

    public void ForceEndAttackAnimation() => attackManager.ForceEndAttackAnimation();

    // Public API - maintaining exact same interface
    public void PlayAnimation(AnimationClip animationClip, int layerIndex = 0, float crossfadeTime = 0.1f)
        => utilityHelper.PlayAnimation(animationClip, layerIndex, crossfadeTime);

    public void TriggerAnimation(string triggerName, float? lockDuration = null)
        => utilityHelper.TriggerAnimation(triggerName, lockDuration, this);

    public void TriggerRollAnimation() => actionHandler.TriggerRollAnimation();
    public void TriggerDashAnimation() => actionHandler.TriggerDashAnimation();

    public void ResetAnimationState()
    {
        StopAttackAnimation();

        model.IsAttacking = false;
        model.IsInputLocked = false;
        model.IsRolling = false;
        model.IsDashing = false;

        attackManager.ResetAttackAnimation();

        utilityHelper.SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsAttacking"] = false,
            ["IsInputLocked"] = false,
            ["IsRolling"] = false,
            ["IsDashing"] = false,
            ["AttackCombo"] = 0
        });

        model.Anim.speed = 1.0f;
    }

    // Helper Methods
    private void StopAttackAnimation() => attackManager?.ResetAttackAnimation();
    private void ResetAttackAnimation() => attackManager?.ResetAttackAnimation();

    // Animation Event Callbacks - delegate to event handler
    public void OnAttackHit() => eventHandler.OnAttackHit();
    public void OnFootstep() => eventHandler.OnFootstep();
    public void OnLanding() => eventHandler.OnLanding();

    // Utility Methods - delegate to utility helper
    public bool IsAnimationPlaying(string animationName, int layerIndex = 0)
        => utilityHelper.IsAnimationPlaying(animationName, layerIndex);

    public float GetAnimationProgress(int layerIndex = 0)
        => utilityHelper.GetAnimationProgress(layerIndex);

    public void SetAnimationSpeed(float speed, int layerIndex = 0)
        => utilityHelper.SetAnimationSpeed(speed, layerIndex);

    private void DebugCurrentAnimations() => utilityHelper.DebugCurrentAnimations();

    // Backward Compatibility - maintaining exact same public interface
    public void PlayAttackAnimation(AnimationClip animationClip, float animationSpeed = 1.0f)
        => attackManager.PlayAttackAnimation(animationClip, animationSpeed);

    public void TriggerAttackAnimation(string animationTrigger, int comboIndex = 0)
        => attackManager.TriggerAttackAnimation(animationTrigger, comboIndex);

    public void EndAttackAnimation() => attackManager.EndAttackAnimation();
    public void EndRollAnimation() => actionHandler.EndRollAnimation();
    public void EndDashAnimation() => actionHandler.EndDashAnimation();
}