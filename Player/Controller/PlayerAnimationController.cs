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

    // Animation state tracking
    private readonly Dictionary<string, Coroutine> activeAnimationCoroutines = new Dictionary<string, Coroutine>();
    private Vector2 currentMovement, movementVelocity;

    // Attack animation control
    private AttackAnimationState attackAnimState = new AttackAnimationState();
    private Coroutine attackAnimationCoroutine;

    // Animation layers
    private const int BASE_LAYER = 0;
    private const int ACTION_LAYER = 1;
    private const int UPPER_LAYER = 2;

    // Properties
    public PlayerAnimationModel Model { get => model; set => model = value; }
    public bool IsAttackAnimationLocked() => attackAnimState.IsLocked;

    // Nested Classes
    private class AttackAnimationState
    {
        public AnimationClip CurrentAnimation;
        public float Speed = 1.0f;
        public float Duration = 0f;
        public bool IsLocked = false;
        public bool IsWeaponManaged = false;

        public void Reset()
        {
            CurrentAnimation = null;
            Speed = 1.0f;
            Duration = 0f;
            IsLocked = false;
            IsWeaponManaged = false;
        }
    }

    // Unity Lifecycle
    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        movementStateMachine = this.CheckComponent(movementStateMachine, nameof(movementStateMachine));
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

    // Animation Events Setup
    private void SetupAnimationEvents()
    {
        SetupActionEvents(model.OnAttackStart, model.OnAttackEnd, () =>
        {
            if (!attackAnimState.IsWeaponManaged && !attackAnimState.IsLocked)
            {
                model.IsAttacking = false;
                model.IsInputLocked = false;
            }
        });

        SetupActionEvents(model.OnDashStart, model.OnDashEnd, () => ResetActionState(false, true));
        SetupActionEvents(model.OnRollStart, model.OnRollEnd, () => ResetActionState(true, false));
    }

    private void SetupActionEvents(Action startAction, Action endAction, Action endLogic)
    {
        startAction += () => model.IsInputLocked = true;
        endAction += endLogic;
    }

    private void ResetActionState(bool resetRoll, bool resetDash)
    {
        if (resetRoll) model.IsRolling = false;
        if (resetDash) model.IsDashing = false;
        model.IsInputLocked = false;
    }

    // Movement Animation
    private void ApplyMovementAnimations()
    {
        var movement = movementModel.Movement2D;

        if (useAdvancedBlending)
        {
            currentMovement.x = Mathf.SmoothDamp(currentMovement.x, movement.x, ref movementVelocity.x, smoothTime);
            currentMovement.y = Mathf.SmoothDamp(currentMovement.y, movement.y, ref movementVelocity.y, smoothTime);
        }
        else
        {
            currentMovement = movement;
        }

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["MovementX"] = currentMovement.x,
            ["MovementZ"] = currentMovement.y,
            ["MovementSpeed"] = currentMovement.magnitude
        });
    }

    private void ApplyStateAnimations()
    {
        if (movementStateMachine?.CurrentState == null) return;

        var state = movementStateMachine.CurrentState.StateKey;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsWalking"] = state == MovementStateMachine.EMovementState.Walking,
            ["IsRunning"] = state == MovementStateMachine.EMovementState.Running,
            ["IsCrouching"] = state == MovementStateMachine.EMovementState.Crouching,
            ["IsJumping"] = state == MovementStateMachine.EMovementState.Jumping,
            ["IsRolling"] = state == MovementStateMachine.EMovementState.Rolling || model.IsRolling,
            ["IsDashing"] = state == MovementStateMachine.EMovementState.Dashing || model.IsDashing,
            ["IsAttacking"] = model.IsAttacking,
            ["IsInputLocked"] = model.IsInputLocked
        });
    }

    // Attack Animation System
    public void PlayAttackAnimationWithDuration(AnimationClip animationClip, float weaponAttackDuration, float animationSpeed = 1.0f)
    {
        if (!ValidateAnimationClip(animationClip)) return;

        LogAttackAnimationSetup(animationClip, weaponAttackDuration, animationSpeed);

        StopAttackAnimation();

        attackAnimState.CurrentAnimation = animationClip;
        attackAnimState.Speed = animationSpeed;
        attackAnimState.Duration = weaponAttackDuration;
        attackAnimState.IsLocked = true;
        attackAnimState.IsWeaponManaged = true;

        PrepareAttackState();
        attackAnimationCoroutine = StartCoroutine(PlayAttackAnimationCoroutine(animationClip, weaponAttackDuration, animationSpeed));
    }

    public void TriggerAttackAnimationWithDuration(string animationTrigger, float weaponDuration, int comboIndex = 0)
    {
        if (attackAnimState.IsLocked)
        {
            if (model.DebugAnimations) Debug.Log("Attack animation is locked, ignoring trigger request");
            return;
        }

        if (string.IsNullOrEmpty(animationTrigger))
        {
            if (model.DebugAnimations) Debug.LogWarning("Animation trigger is null or empty");
            return;
        }

        if (model.DebugAnimations)
            Debug.Log($"TriggerAttackAnimationWithDuration: {animationTrigger} with duration: {weaponDuration}s, combo: {comboIndex}");

        StopAttackAnimation();

        attackAnimState.Duration = weaponDuration;
        attackAnimState.IsLocked = true;
        attackAnimState.IsWeaponManaged = true;

        if (comboIndex > 0) model.SetParameterSafe("AttackCombo", comboIndex);

        PrepareAttackState();
        TriggerAnimation(animationTrigger);

        attackAnimationCoroutine = StartCoroutine(ControlAttackDuration(weaponDuration));
    }

    // Animation Control Coroutines
    private IEnumerator PlayAttackAnimationCoroutine(AnimationClip clip, float weaponDuration, float speed)
    {
        float animationNaturalDuration = clip.length;
        bool needsLooping = weaponDuration > (animationNaturalDuration * 1.1f);
        float adjustedSpeed = speed;

        if (weaponDuration < animationNaturalDuration * 0.9f)
        {
            adjustedSpeed = (animationNaturalDuration / weaponDuration) * speed;
            needsLooping = false;
        }

        model.Anim.speed = adjustedSpeed;
        bool animationStarted = TryPlayAnimation(clip);

        yield return null;

        float elapsedTime = 0f;
        float adjustedAnimDuration = animationNaturalDuration / adjustedSpeed;

        while (elapsedTime < weaponDuration && attackAnimState.IsLocked)
        {
            elapsedTime += Time.deltaTime;

            if (needsLooping && animationStarted)
            {
                yield return HandleAnimationLooping(clip, elapsedTime, adjustedAnimDuration, weaponDuration);
            }

            if (ShouldCheckAnimationState(elapsedTime))
            {
                CheckAndMaintainAttackAnimation(elapsedTime, weaponDuration);
            }

            yield return null;
        }

        CompleteAttackAnimation();
    }

    private IEnumerator ControlAttackDuration(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration && attackAnimState.IsLocked)
        {
            elapsedTime += Time.deltaTime;
            MaintainAttackState();
            yield return null;
        }

        CompleteAttackAnimation();
    }

    // Animation Playback
    private bool TryPlayAnimation(AnimationClip clip)
    {
        try
        {
            for (int layer = 0; layer < model.Anim.layerCount; layer++)
            {
                if (model.Anim.HasState(layer, Animator.StringToHash(clip.name)))
                {
                    model.Anim.Play(clip.name, layer, 0f);
                    if (model.DebugAnimations) Debug.Log($"✓ Playing animation: {clip.name} on layer {layer}");
                    return true;
                }
            }

            return TryTriggerFallback();
        }
        catch (Exception e)
        {
            if (model.DebugAnimations) Debug.LogError($"Failed to play animation: {e.Message}");
            return TryTriggerFallback();
        }
    }

    private bool TryTriggerFallback()
    {
        string[] triggers = { "AttackTrigger", "LightAttackTrigger", "HeavyAttackTrigger", "NormalAttackTrigger", "BasicAttackTrigger" };

        foreach (string trigger in triggers)
        {
            if (model.HasAnimationParameter(trigger))
            {
                model.SetParameterSafe(trigger, true);
                if (model.DebugAnimations) Debug.Log($"✓ Triggered: {trigger}");
                return true;
            }
        }

        if (model.HasAnimationParameter("IsAttacking"))
        {
            model.SetParameterSafe("IsAttacking", true);
            return true;
        }

        return false;
    }

    // State Management
    private void PrepareAttackState()
    {
        model.IsAttacking = true;
        model.IsInputLocked = true;
        model.SetParameterSafe("IsAttacking", true);
        model.OnAttackStart?.Invoke();
    }

    private void CompleteAttackAnimation()
    {
        if (model.DebugAnimations) Debug.Log("Completing attack animation");

        model.Anim.speed = 1.0f;
        attackAnimState.Reset();

        model.IsAttacking = false;
        model.IsInputLocked = false;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsAttacking"] = false,
            ["AttackCombo"] = 0
        });

        attackAnimationCoroutine = null;
        model.OnAttackEnd?.Invoke();
    }

    private void ResetAttackAnimation()
    {
        if (attackAnimationCoroutine != null)
        {
            StopCoroutine(attackAnimationCoroutine);
            attackAnimationCoroutine = null;
        }
        attackAnimState.Reset();
    }

    // Public API
    public void ForceEndAttackAnimation()
    {
        if (model.DebugAnimations) Debug.Log("Force ending attack animation");

        StopAttackAnimation();
        model.Anim.speed = 1.0f;
        attackAnimState.Reset();

        model.IsAttacking = false;
        model.IsInputLocked = false;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsAttacking"] = false,
            ["AttackCombo"] = 0
        });

        model.OnAttackEnd?.Invoke();
    }

    public void PlayAnimation(AnimationClip animationClip, int layerIndex = BASE_LAYER, float crossfadeTime = 0.1f)
    {
        if (animationClip == null) return;

        if (HasAnimationState(animationClip.name, layerIndex))
        {
            model.Anim.CrossFade(animationClip.name, crossfadeTime, layerIndex);
        }
        else
        {
            model.Anim.PlayInFixedTime(animationClip.name, layerIndex);
        }
    }

    public void TriggerAnimation(string triggerName, float? lockDuration = null)
    {
        if (string.IsNullOrEmpty(triggerName)) return;

        if (attackAnimState.IsLocked && triggerName.Contains("Attack"))
        {
            if (model.DebugAnimations) Debug.Log($"Ignoring {triggerName} - attack locked");
            return;
        }

        model.SetParameterSafe(triggerName, true);

        if (lockDuration.HasValue)
            StartCoroutine(LockInputForDuration(lockDuration.Value));
    }

    public void TriggerRollAnimation() => TriggerActionAnimation("RollTrigger", () => model.IsRolling = true, model.OnRollStart, HandleRollAnimation);
    public void TriggerDashAnimation() => TriggerActionAnimation("DashTrigger", () => model.IsDashing = true, model.OnDashStart, HandleDashAnimation);

    public void ResetAnimationState()
    {
        StopAttackAnimation();

        model.IsAttacking = false;
        model.IsInputLocked = false;
        model.IsRolling = false;
        model.IsDashing = false;

        attackAnimState.Reset();

        SetAnimationParameters(new Dictionary<string, object>
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
    private void SetAnimationParameters(Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
            model.SetParameterSafe(kvp.Key, kvp.Value);
    }

    private void StopAttackAnimation()
    {
        if (attackAnimationCoroutine != null)
        {
            StopCoroutine(attackAnimationCoroutine);
            attackAnimationCoroutine = null;
        }
    }

    private bool ValidateAnimationClip(AnimationClip clip)
    {
        if (clip == null)
        {
            if (model.DebugAnimations) Debug.LogError("AnimationClip is null!");
            return false;
        }
        return true;
    }

    private void LogAttackAnimationSetup(AnimationClip clip, float weaponDuration, float speed)
    {
        if (!model.DebugAnimations) return;

        Debug.Log($"=== ATTACK ANIMATION SETUP ===\n" +
                  $"Animation: {clip.name}\n" +
                  $"Length: {clip.length}s\n" +
                  $"Weapon Duration: {weaponDuration}s\n" +
                  $"Speed: {speed}\n" +
                  $"Will Loop: {weaponDuration > clip.length * 1.1f}");
    }

    private bool ShouldCheckAnimationState(float elapsedTime)
    {
        return elapsedTime % 0.2f < Time.deltaTime;
    }

    private void CheckAndMaintainAttackAnimation(float elapsedTime, float duration)
    {
        bool isPlaying = IsAnyAttackAnimationPlaying();

        if (!isPlaying && elapsedTime < duration - 0.2f)
        {
            if (model.DebugAnimations) Debug.Log("No attack animation detected, using fallback");
            TryTriggerFallback();
        }
    }

    private void MaintainAttackState()
    {
        if (!model.IsAttacking && attackAnimState.IsLocked)
        {
            model.IsAttacking = true;
            model.SetParameterSafe("IsAttacking", true);
        }
    }

    private bool IsAnyAttackAnimationPlaying()
    {
        for (int layer = 0; layer < model.Anim.layerCount; layer++)
        {
            var stateInfo = model.Anim.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsTag("Attack") || IsAttackStateName(stateInfo))
                return true;
        }
        return model.GetAnimationBool("IsAttacking");
    }

    private bool IsAttackStateName(AnimatorStateInfo stateInfo)
    {
        string[] attackStates = { "Attack", "LightAttack", "HeavyAttack", "NormalAttack", "BasicAttack" };
        return Array.Exists(attackStates, state => stateInfo.IsName(state));
    }

    private IEnumerator HandleAnimationLooping(AnimationClip clip, float elapsedTime, float animDuration, float totalDuration)
    {
        // Implementation depends on specific looping requirements
        yield return null;
    }

    private bool HasAnimationState(string stateName, int layerIndex)
    {
        if (model.Anim == null || model.Anim.runtimeAnimatorController == null) return false;
        if (layerIndex >= model.Anim.layerCount) return false;

        foreach (var clip in model.Anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == stateName) return true;
        }
        return false;
    }

    private void TriggerActionAnimation(string trigger, Action setState, Action onStart, Func<IEnumerator> handler)
    {
        if (model.IsInputLocked) return;

        TriggerAnimation(trigger);
        setState?.Invoke();
        onStart?.Invoke();
        StartCoroutine(handler());
    }

    private IEnumerator HandleRollAnimation()
    {
        yield return HandleActionAnimation(() =>
        {
            model.IsRolling = false;
            model.SetParameterSafe("IsRolling", false);
            model.OnRollEnd?.Invoke();
        });
    }

    private IEnumerator HandleDashAnimation()
    {
        yield return HandleActionAnimation(() =>
        {
            model.IsDashing = false;
            model.SetParameterSafe("IsDashing", false);
            model.OnDashEnd?.Invoke();
        });
    }

    private IEnumerator HandleActionAnimation(Action endAction)
    {
        yield return null;
        var stateInfo = model.Anim.GetCurrentAnimatorStateInfo(ACTION_LAYER);
        yield return new WaitForSeconds(stateInfo.length);
        endAction?.Invoke();
    }

    private IEnumerator LockInputForDuration(float duration)
    {
        model.IsInputLocked = true;
        yield return new WaitForSeconds(duration);
        model.IsInputLocked = false;
    }

    private void DebugCurrentAnimations()
    {
        var states = new List<string>();
        if (model.IsAttacking) states.Add("Attacking");
        if (attackAnimState.IsLocked) states.Add("Locked");
        if (attackAnimState.IsWeaponManaged) states.Add("WeaponManaged");
        if (model.IsDashing) states.Add("Dashing");
        if (model.IsRolling) states.Add("Rolling");
        if (model.IsInputLocked) states.Add("InputLocked");

        if (states.Count > 0) Debug.Log($"[AnimController] States: {string.Join(", ", states)}");
    }

    // Animation Event Callbacks
    public void OnAttackHit() => DebugLog("Attack Hit!");
    public void OnFootstep() => DebugLog("Footstep!");
    public void OnLanding()
    {
        model.OnLandStart?.Invoke();
        DebugLog("Player Landed!");
    }

    private void DebugLog(string message)
    {
        if (model.DebugAnimations) Debug.Log($"[AnimController] {message}");
    }

    // Utility Methods
    public bool IsAnimationPlaying(string animationName, int layerIndex = 0) =>
        model.Anim.GetCurrentAnimatorStateInfo(layerIndex).IsName(animationName);

    public float GetAnimationProgress(int layerIndex = 0) =>
        model.Anim.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;

    public void SetAnimationSpeed(float speed, int layerIndex = 0) => model.Anim.speed = speed;

    // Backward Compatibility
    public void PlayAttackAnimation(AnimationClip animationClip, float animationSpeed = 1.0f)
    {
        if (animationClip != null)
            PlayAttackAnimationWithDuration(animationClip, animationClip.length / animationSpeed, animationSpeed);
    }

    public void TriggerAttackAnimation(string animationTrigger, int comboIndex = 0)
    {
        TriggerAttackAnimationWithDuration(animationTrigger, 1.0f, comboIndex);
    }

    public void EndAttackAnimation()
    {
        if (attackAnimState.IsLocked)
        {
            if (model.DebugAnimations) Debug.Log("EndAttackAnimation called but animation is locked");
            return;
        }

        if (!attackAnimState.IsWeaponManaged)
        {
            CompleteAttackAnimation();
        }
    }

    public void EndRollAnimation() => ResetActionState(true, false);
    public void EndDashAnimation() => ResetActionState(false, true);
}