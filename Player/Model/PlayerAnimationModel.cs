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

    public void InitializeAnimationHashes()
    {
        animationHashes.Clear();

        // Only cache parameters that actually exist in the Animator Controller
        if (anim == null)
        {
            Debug.LogError("Animator is null in PlayerAnimationModel!");
            return;
        }

        if (anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning("No RuntimeAnimatorController assigned to Animator!");
            return;
        }

        string[] parameterNames = {
        // Movement parameters
        "IsWalking", "IsRunning", "IsCrouching", "IsDashing",
        "IsRolling", "IsJumping", "IsAttacking", "IsFalling",
        "MovementX", "MovementZ", "MovementSpeed", "AirTime",
        
        // Combat parameters
        "AttackCombo", "AttackTrigger", "LightAttackTrigger",
        "HeavyAttackTrigger", "ChargedAttackTrigger", "SpecialAttackTrigger",
        
        // Action parameters
        "RollTrigger", "DashTrigger", "JumpTrigger", "LandTrigger",
        "HitTrigger", "DeathTrigger",
        
        // State parameters
        "IsInputLocked", "IsBlocking", "IsStunned"
    };

        int cachedCount = 0;
        foreach (string paramName in parameterNames)
        {
            if (HasParameter(paramName))
            {
                animationHashes[paramName] = Animator.StringToHash(paramName);
                cachedCount++;
            }
        }

        if (debugAnimations)
            Debug.Log($"Cached {cachedCount} animation parameters out of {parameterNames.Length} total parameters");
    }
    private bool HasParameter(string paramName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;

        try
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == paramName)
                    return true;
            }
        }
        catch (System.Exception e)
        {
            if (debugAnimations)
                Debug.LogWarning($"Error checking parameter '{paramName}': {e.Message}");
        }

        return false;
    }

    public int GetAnimationHash(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return -1;

        if (animationHashes.TryGetValue(parameterName, out int hash))
        {
            return hash;
        }

        // Try to add the parameter if it exists but wasn't cached
        if (HasParameter(parameterName))
        {
            hash = Animator.StringToHash(parameterName);
            animationHashes[parameterName] = hash;

            if (debugAnimations)
                Debug.Log($"Late-cached animation parameter: {parameterName}");

            return hash;
        }

        if (debugAnimations)
            Debug.LogWarning($"Animation parameter '{parameterName}' not found in Animator Controller!");

        return -1;
    }

    public bool SetParameterSafe(string parameterName, object value)
    {
        int hash = GetAnimationHash(parameterName);
        if (hash == -1) return false;

        try
        {
            // Get the parameter type
            AnimatorControllerParameter param = null;
            foreach (var p in anim.parameters)
            {
                if (p.name == parameterName)
                {
                    param = p;
                    break;
                }
            }

            if (param == null) return false;

            // Set based on parameter type
            switch (param.type)
            {
                case AnimatorControllerParameterType.Bool:
                    if (value is bool boolVal)
                        anim.SetBool(hash, boolVal);
                    else
                        return false;
                    break;

                case AnimatorControllerParameterType.Float:
                    if (value is float floatVal)
                        anim.SetFloat(hash, floatVal);
                    else if (value is int intVal)
                        anim.SetFloat(hash, intVal);
                    else
                        return false;
                    break;

                case AnimatorControllerParameterType.Int:
                    if (value is int integerVal)
                        anim.SetInteger(hash, integerVal);
                    else if (value is float floatAsInt)
                        anim.SetInteger(hash, Mathf.RoundToInt(floatAsInt));
                    else
                        return false;
                    break;

                case AnimatorControllerParameterType.Trigger:
                    anim.SetTrigger(hash);
                    break;

                default:
                    return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            if (debugAnimations)
                Debug.LogError($"Error setting parameter '{parameterName}': {e.Message}");
            return false;
        }
    }

    // NEW METHOD - Get parameter value safely
    public T GetParameterSafe<T>(string parameterName, T defaultValue = default(T))
    {
        int hash = GetAnimationHash(parameterName);
        if (hash == -1) return defaultValue;

        try
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)anim.GetBool(hash);
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)anim.GetFloat(hash);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)anim.GetInteger(hash);
            }
        }
        catch (System.Exception e)
        {
            if (debugAnimations)
                Debug.LogError($"Error getting parameter '{parameterName}': {e.Message}");
        }

        return defaultValue;
    }

    // NEW METHOD - Validate animation state
    public bool ValidateAnimationState()
    {
        if (anim == null)
        {
            Debug.LogError("Animator component is missing!");
            return false;
        }

        if (anim.runtimeAnimatorController == null)
        {
            Debug.LogError("RuntimeAnimatorController is missing!");
            return false;
        }

        bool isValid = true;

        // Check if critical parameters exist
        string[] criticalParams = { "IsAttacking", "MovementX", "MovementZ" };
        foreach (string param in criticalParams)
        {
            if (!HasAnimationParameter(param))
            {
                Debug.LogWarning($"Critical animation parameter '{param}' is missing!");
                isValid = false;
            }
        }

        return isValid;
    }

    // NEW METHOD - Reset all animation parameters to default
    public void ResetAllParameters()
    {
        if (anim == null) return;

        try
        {
            foreach (var param in anim.parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        anim.SetBool(param.name, false);
                        break;
                    case AnimatorControllerParameterType.Float:
                        anim.SetFloat(param.name, 0f);
                        break;
                    case AnimatorControllerParameterType.Int:
                        anim.SetInteger(param.name, 0);
                        break;
                        // Note: Triggers reset automatically after being consumed
                }
            }

            // Reset internal state
            isAttacking = false;
            isDashing = false;
            isRolling = false;
            isInputLocked = false;

            if (debugAnimations)
                Debug.Log("All animation parameters reset to default values");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error resetting animation parameters: {e.Message}");
        }
    }
    public bool HasAnimationParameter(string parameterName)
    {
        return animationHashes.ContainsKey(parameterName);
    }


    public bool GetAnimationBool(string parameterName)
    {
        return GetParameterSafe<bool>(parameterName, false);
    }

    public float GetAnimationFloat(string parameterName)
    {
        return GetParameterSafe<float>(parameterName, 0f);
    }

    // NEW METHOD - Get animation integer
    public int GetAnimationInteger(string parameterName)
    {
        return GetParameterSafe<int>(parameterName, 0);
    }

    public bool IsAttacking
    {
        get => isAttacking;
        set
        {
            if (isAttacking != value)
            {
                isAttacking = value;
                if (debugAnimations)
                    Debug.Log($"IsAttacking changed to: {value}");
            }
        }
    }

    public bool IsDashing
    {
        get => isDashing;
        set
        {
            if (isDashing != value)
            {
                isDashing = value;
                if (debugAnimations)
                    Debug.Log($"IsDashing changed to: {value}");
            }
        }
    }

    public bool IsRolling
    {
        get => isRolling;
        set
        {
            if (isRolling != value)
            {
                isRolling = value;
                if (debugAnimations)
                    Debug.Log($"IsRolling changed to: {value}");
            }
        }
    }

    public bool IsInputLocked
    {
        get => isInputLocked;
        set
        {
            if (isInputLocked != value)
            {
                isInputLocked = value;
                if (debugAnimations)
                    Debug.Log($"IsInputLocked changed to: {value}");
            }
        }
    }

    public float TransitionSpeed { get => transitionSpeed; set => transitionSpeed = value; }
    public bool DebugAnimations { get => debugAnimations; set => debugAnimations = value; }
    public Animator Anim { get => anim; set => anim = value; }

}