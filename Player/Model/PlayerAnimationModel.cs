using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationModel : MonoBehaviour
{
    [Header("Animation Components")]
    [SerializeField] private Animator anim;

    [Header("Animation Settings")]
    [SerializeField] private float transitionSpeed = 0.1f;
    [SerializeField] private bool debugAnimations = false;

    // Animation parameter hashes - cached for performance
    private readonly Dictionary<string, int> animationHashes = new Dictionary<string, int>();

    // Animation state tracking
    private bool isAttacking = false;
    private bool isDashing = false;
    private bool isRolling = false;
    private bool isInputLocked = false;

    // Animation events
    public System.Action OnAttackStart;
    public System.Action OnAttackEnd;
    public System.Action OnDashStart;
    public System.Action OnDashEnd;
    public System.Action OnRollStart;
    public System.Action OnRollEnd;
    public System.Action OnJumpStart;
    public System.Action OnLandStart;

    private void Awake()
    {
        anim = this.CheckComponent(anim, nameof(anim), isCritical: true, searchChildren: true);
        InitializeAnimationHashes();
    }

    private void InitializeAnimationHashes()
    {
        // Only cache parameters that actually exist in the Animator Controller
        // This prevents errors when parameters don't exist
        if (anim == null) return;

        string[] parameterNames = {
            "IsWalking", "IsRunning", "IsCrouching", "IsDashing",
            "IsRolling", "IsJumping", "IsAttacking", "IsFalling",
            "MovementX", "MovementZ", "MovementSpeed", "AirTime",
            "AttackCombo", "AttackTrigger", "RollTrigger", "DashTrigger",
            "JumpTrigger", "LandTrigger", "HitTrigger", "DeathTrigger"
        };

        foreach (string paramName in parameterNames)
        {
            if (HasParameter(paramName))
            {
                animationHashes[paramName] = Animator.StringToHash(paramName);
            }
        }
    }

    private bool HasParameter(string paramName)
    {
        if (anim == null) return false;

        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    public int GetAnimationHash(string parameterName)
    {
        if (animationHashes.TryGetValue(parameterName, out int hash))
        {
            return hash;
        }

        if (debugAnimations)
            Debug.LogWarning($"Animation parameter '{parameterName}' not found in hash cache or doesn't exist in Animator Controller!");

        // Return -1 to indicate parameter doesn't exist
        return -1;
    }
    public bool HasAnimationParameter(string parameterName)
    {
        return animationHashes.ContainsKey(parameterName);
    }


    public bool GetAnimationBool(string parameterName)
    {
        if (HasAnimationParameter(parameterName))
        {
            int hash = GetAnimationHash(parameterName);
            return anim.GetBool(hash);
        }
        return false;
    }

    public bool IsAttacking { get => isAttacking; set => isAttacking = value; }
    public bool IsDashing { get => isDashing; set => isDashing = value; }
    public bool IsRolling { get => isRolling; set => isRolling = value; }
    public bool IsInputLocked { get => isInputLocked; set => isInputLocked = value; }
    public float TransitionSpeed { get => transitionSpeed; set => transitionSpeed = value; }
    public bool DebugAnimations { get => debugAnimations; set => debugAnimations = value; }
    public Animator Anim { get => anim; set => anim = value; }
}