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
    private Dictionary<string, Coroutine> activeAnimationCoroutines = new Dictionary<string, Coroutine>();
    private float currentMovementX;
    private float currentMovementZ;
    private float movementVelocityX;
    private float movementVelocityZ;

    // Animation layers
    private const int BASE_LAYER = 0;
    private const int ACTION_LAYER = 1;
    private const int UPPER_BODY_LAYER = 2;

    public PlayerAnimationModel Model { get => model; set => model = value; }

    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        movementStateMachine = this.CheckComponent(movementStateMachine, nameof(movementStateMachine));
    }

    private void Start()
    {
        SetupAnimationEvents();
    }

    private void Update()
    {
        ApplyMovementAnimations();
        ApplyStateAnimations();

        if (model.DebugAnimations)
            DebugCurrentAnimations();
    }

    private void SetupAnimationEvents()
    {
        // Setup animation event callbacks
        model.OnAttackStart += () => model.IsInputLocked = true;
        model.OnAttackEnd += () =>
        {
            model.IsAttacking = false;
            model.IsInputLocked = false;
        };

        model.OnDashStart += () => model.IsInputLocked = true;
        model.OnDashEnd += () =>
        {
            model.IsDashing = false;
            model.IsInputLocked = false;
        };

        model.OnRollStart += () => model.IsInputLocked = true;
        model.OnRollEnd += () =>
        {
            model.IsRolling = false;
            model.IsInputLocked = false;
        };
    }

    private void ApplyMovementAnimations()
    {
        // Smooth movement parameter transitions
        if (useAdvancedBlending)
        {
            currentMovementX = Mathf.SmoothDamp(currentMovementX, movementModel.Movement2D.x,
                ref movementVelocityX, smoothTime);
            currentMovementZ = Mathf.SmoothDamp(currentMovementZ, movementModel.Movement2D.y,
                ref movementVelocityZ, smoothTime);
        }
        else
        {
            currentMovementX = movementModel.Movement2D.x;
            currentMovementZ = movementModel.Movement2D.y;
        }

        // Apply movement parameters (only if they exist)
        SetFloatSafe("MovementX", currentMovementX);
        SetFloatSafe("MovementZ", currentMovementZ);

        // Calculate and set movement speed
        float movementSpeed = new Vector2(currentMovementX, currentMovementZ).magnitude;
        SetFloatSafe("MovementSpeed", movementSpeed);
    }

    // Safe parameter setting methods
    private void SetBoolSafe(string paramName, bool value)
    {
        int hash = model.GetAnimationHash(paramName);
        if (hash != -1)
        {
            model.Anim.SetBool(hash, value);
        }
    }

    private void SetFloatSafe(string paramName, float value)
    {
        int hash = model.GetAnimationHash(paramName);
        if (hash != -1)
        {
            model.Anim.SetFloat(hash, value);
        }
    }

    private void SetIntegerSafe(string paramName, int value)
    {
        int hash = model.GetAnimationHash(paramName);
        if (hash != -1)
        {
            model.Anim.SetInteger(hash, value);
        }
    }

    private void SetTriggerSafe(string paramName)
    {
        int hash = model.GetAnimationHash(paramName);
        if (hash != -1)
        {
            model.Anim.SetTrigger(hash);
        }
    }

    private bool GetBoolSafe(string paramName)
    {
        int hash = model.GetAnimationHash(paramName);
        if (hash != -1)
        {
            return model.Anim.GetBool(hash);
        }
        return false;
    }

    private void ApplyStateAnimations()
    {
        if (movementStateMachine?.CurrentState == null) return;

        var currentState = movementStateMachine.CurrentState.StateKey;

        // Apply movement state booleans (only if parameters exist)
        SetBoolSafe("IsWalking", currentState == MovementStateMachine.EMovementState.Walking);
        SetBoolSafe("IsRunning", currentState == MovementStateMachine.EMovementState.Running);
        SetBoolSafe("IsCrouching", currentState == MovementStateMachine.EMovementState.Crouching);
        SetBoolSafe("IsJumping", currentState == MovementStateMachine.EMovementState.Jumping);
        SetBoolSafe("IsRolling", currentState == MovementStateMachine.EMovementState.Rolling || model.IsRolling);
        SetBoolSafe("IsDashing", currentState == MovementStateMachine.EMovementState.Dashing || model.IsDashing);

        // Apply action state booleans
        SetBoolSafe("IsAttacking", model.IsAttacking);
    }

    public void TriggerAnimation(string triggerName, float? lockDuration = null)
    {
        if (string.IsNullOrEmpty(triggerName)) return;

        SetTriggerSafe(triggerName);

        if (lockDuration.HasValue)
        {
            StartCoroutine(LockInputForDuration(lockDuration.Value));
        }

        if (model.DebugAnimations)
            Debug.Log($"Triggered animation: {triggerName}");
    }

    public void TriggerAttackAnimation(string animationTrigger, int comboIndex = 0)
    {
        if (model.IsInputLocked || string.IsNullOrEmpty(animationTrigger)) return;

        // Set combo index if using combo system
        if (comboIndex > 0)
        {
            SetIntegerSafe("AttackCombo", comboIndex);
        }

        // Trigger the attack
        TriggerAnimation(animationTrigger);
        model.IsAttacking = true;
        model.OnAttackStart?.Invoke();

        // Get animation length and setup auto-unlock
        StartCoroutine(HandleAttackAnimation(animationTrigger));
    }

    public void TriggerRollAnimation()
    {
        if (model.IsInputLocked) return;

        TriggerAnimation("RollTrigger");
        model.IsRolling = true;
        model.OnRollStart?.Invoke();

        StartCoroutine(HandleRollAnimation());
    }

    public void TriggerDashAnimation()
    {
        if (model.IsInputLocked) return;

        TriggerAnimation("DashTrigger");
        model.IsDashing = true;
        model.OnDashStart?.Invoke();

        StartCoroutine(HandleDashAnimation());
    }

    private IEnumerator HandleAttackAnimation(string triggerName)
    {
        // Wait a frame for the transition to start
        yield return null;

        // Get the current state info from the action layer
        AnimatorStateInfo stateInfo = model.Anim.GetCurrentAnimatorStateInfo(ACTION_LAYER);

        // Wait for the animation to complete
        yield return new WaitForSeconds(stateInfo.length);

        // End the attack
        EndAttackAnimation();
    }

    private IEnumerator HandleRollAnimation()
    {
        yield return null;
        AnimatorStateInfo stateInfo = model.Anim.GetCurrentAnimatorStateInfo(ACTION_LAYER);
        yield return new WaitForSeconds(stateInfo.length);
        EndRollAnimation();
    }

    private IEnumerator HandleDashAnimation()
    {
        yield return null;
        AnimatorStateInfo stateInfo = model.Anim.GetCurrentAnimatorStateInfo(ACTION_LAYER);
        yield return new WaitForSeconds(stateInfo.length);
        EndDashAnimation();
    }

    private IEnumerator LockInputForDuration(float duration)
    {
        model.IsInputLocked = true;
        yield return new WaitForSeconds(duration);
        model.IsInputLocked = false;
    }

    // Animation end methods (can be called by Animation Events)
    public void EndAttackAnimation()
    {
        model.IsAttacking = false;
        SetBoolSafe("IsAttacking", false);
        model.OnAttackEnd?.Invoke();
    }

    public void EndRollAnimation()
    {
        model.IsRolling = false;
        SetBoolSafe("IsRolling", false);
        model.OnRollEnd?.Invoke();
    }

    public void EndDashAnimation()
    {
        model.IsDashing = false;
        SetBoolSafe("IsDashing", false);
        model.OnDashEnd?.Invoke();
    }

    // Animation event callbacks (called from Unity Animation Events)
    public void OnAttackHit()
    {
        // Handle attack hit logic here
        if (model.DebugAnimations)
            Debug.Log("Attack Hit!");
    }

    public void OnFootstep()
    {
        // Handle footstep sound/effects here
        if (model.DebugAnimations)
            Debug.Log("Footstep!");
    }

    public void OnLanding()
    {
        // Handle landing effects here
        model.OnLandStart?.Invoke();
        if (model.DebugAnimations)
            Debug.Log("Player Landed!");
    }

    // Utility methods
    public bool IsAnimationPlaying(string animationName, int layerIndex = 0)
    {
        AnimatorStateInfo stateInfo = model.Anim.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.IsName(animationName);
    }

    public float GetAnimationProgress(int layerIndex = 0)
    {
        AnimatorStateInfo stateInfo = model.Anim.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.normalizedTime;
    }

    public void SetAnimationSpeed(float speed, int layerIndex = 0)
    {
        model.Anim.SetFloat("AnimationSpeed", speed);
    }

    private void DebugCurrentAnimations()
    {
        if (model.IsAttacking) Debug.Log("Currently Attacking");
        if (model.IsDashing) Debug.Log("Currently Dashing");
        if (model.IsRolling) Debug.Log("Currently Rolling");
        if (model.IsInputLocked) Debug.Log("Input is Locked");
    }

    // Clean up coroutines when disabled
    private void OnDisable()
    {
        StopAllCoroutines();
        activeAnimationCoroutines.Clear();
    }
}