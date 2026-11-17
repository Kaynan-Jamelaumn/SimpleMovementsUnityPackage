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

    // Every parameter of the controller the Animator has now, with its type. Read once per controller: asking the
    // Animator for its parameters builds a new array each time, which used to happen dozens of times a frame.
    private readonly Dictionary<string, AnimatorControllerParameterType> parameterTypes =
        new Dictionary<string, AnimatorControllerParameterType>();
    private RuntimeAnimatorController scannedController;
    private int scannedCount;
    private int countCheckedFrame = -1;

    private static readonly string[] KnownParameterNames =
    {
        // Movement parameters
        "IsWalking", "IsRunning", "IsCrouching", "IsDashing",
        "IsRolling", "IsJumping", "IsAttacking", "IsFalling",
        "MovementX", "MovementZ", "MovementSpeed", "AirTime",
        "IsSwimming", "IsUnderwater", "SwimVertical", "SwimPitch", "SwimSpeed",
        // Injuries (how the legs let the player move)
        "LocomotionMode", "IsLimping", "LimpAmount", "LimpSide", "IsHopping", "HopCycle", "IsCrawling", "IsLegless",
        "MissingLegs", "MissingArms", "IsSitting", "SeatValue",

        // Combat parameters
        "AttackCombo", "AttackTrigger", "LightAttackTrigger",
        "HeavyAttackTrigger", "ChargedAttackTrigger", "SpecialAttackTrigger",

        // Action parameters
        "RollTrigger", "DashTrigger", "JumpTrigger", "LandTrigger",
        "HitTrigger", "DeathTrigger", "UseItemTrigger",

        // State parameters
        "IsInputLocked", "IsBlocking", "IsStunned", "IsGrounded", "IsDead", "IsSleeping", "IsUnconscious", "IsTreating"
    };

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

    /// <summary>
    /// Reads the Animator's parameters again (call it after giving the Animator another controller; it is also done
    /// by itself when a parameter is asked for and the controller is not the one last read).
    /// </summary>
    public void InitializeAnimationHashes()
    {
        animationHashes.Clear();
        parameterTypes.Clear();
        scannedController = null;
        scannedCount = 0;

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

        ScanParameters();
        int cachedCount = CacheKnownNames();

        if (debugAnimations)
            Debug.Log($"Cached {cachedCount} animation parameters out of {KnownParameterNames.Length} total parameters");
    }

    /// <summary>The parameters this system drives that the controller has: known from the start (their hashes kept).</summary>
    private int CacheKnownNames()
    {
        int cachedCount = 0;
        foreach (string paramName in KnownParameterNames)
        {
            if (parameterTypes.ContainsKey(paramName))
            {
                animationHashes[paramName] = Animator.StringToHash(paramName);
                cachedCount++;
            }
        }
        return cachedCount;
    }

    /// <summary>Lists the controller's parameters with their types (the one place that asks the Animator for them).</summary>
    private void ScanParameters()
    {
        parameterTypes.Clear();
        scannedController = anim != null ? anim.runtimeAnimatorController : null;
        scannedCount = 0;
        if (anim == null || scannedController == null)
            return;
        try
        {
            AnimatorControllerParameter[] all = anim.parameters;
            scannedCount = all.Length;
            for (int i = 0; i < all.Length; i++)
                parameterTypes[all[i].name] = all[i].type;
        }
        catch (System.Exception e)
        {
            if (debugAnimations)
                Debug.LogWarning($"Error reading the Animator's parameters: {e.Message}");
        }
    }

    private bool HasParameter(string paramName)
    {
        if (anim == null) return false;
        RuntimeAnimatorController controller = anim.runtimeAnimatorController;
        if (controller == null)
        {
            // No controller (any more): nothing can be set, whatever was read from the last one.
            if (parameterTypes.Count > 0 || scannedController != null)
            {
                parameterTypes.Clear();
                scannedController = null;
                scannedCount = 0;
            }
            return false;
        }

        // Another controller than the one last read (swapped without InitializeAnimationHashes): start again from it.
        if (!ReferenceEquals(controller, scannedController))
            InitializeAnimationHashes();

        if (parameterTypes.ContainsKey(paramName))
            return true;

        // Not there. The list may have been read before the Animator was ready, or a parameter was added while
        // playing in the editor: the count tells (one cheap check a frame, no array built).
        if (countCheckedFrame != Time.frameCount)
        {
            countCheckedFrame = Time.frameCount;
            if (anim.parameterCount != scannedCount)
            {
                ScanParameters();
                CacheKnownNames();
                return parameterTypes.ContainsKey(paramName);
            }
        }
        return false;
    }

    public int GetAnimationHash(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return -1;

        // (also notices a controller that was swapped, before a hash of the old one is handed out)
        bool exists = HasParameter(parameterName);

        if (animationHashes.TryGetValue(parameterName, out int hash))
        {
            return hash;
        }

        // Try to add the parameter if it exists but wasn't cached
        if (exists)
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

    /// <summary>The hash and the type of a parameter the Animator has. False when it has none of that name.</summary>
    private bool TryGetParameter(string parameterName, out int hash, out AnimatorControllerParameterType type)
    {
        type = default;
        hash = GetAnimationHash(parameterName);
        return hash != -1 && anim != null && parameterTypes.TryGetValue(parameterName, out type);
    }

    // The three typed versions do exactly what the object version below does for a bool, a float and an int, without
    // boxing the value: a call with a plain value (true, 0.5f, 2) picks them by itself.

    /// <summary>Sets a Bool parameter (a Trigger of that name is set, whatever the value). False when it could not.</summary>
    public bool SetParameterSafe(string parameterName, bool value)
    {
        if (!TryGetParameter(parameterName, out int hash, out AnimatorControllerParameterType type)) return false;
        switch (type)
        {
            case AnimatorControllerParameterType.Bool:
                anim.SetBool(hash, value);
                return true;
            case AnimatorControllerParameterType.Trigger:
                anim.SetTrigger(hash);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Sets a Float parameter (an Int one gets the rounded value, a Trigger is set). False when it could not.</summary>
    public bool SetParameterSafe(string parameterName, float value)
    {
        if (!TryGetParameter(parameterName, out int hash, out AnimatorControllerParameterType type)) return false;
        switch (type)
        {
            case AnimatorControllerParameterType.Float:
                anim.SetFloat(hash, value);
                return true;
            case AnimatorControllerParameterType.Int:
                anim.SetInteger(hash, Mathf.RoundToInt(value));
                return true;
            case AnimatorControllerParameterType.Trigger:
                anim.SetTrigger(hash);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Sets an Int parameter (a Float one gets the value, a Trigger is set). False when it could not.</summary>
    public bool SetParameterSafe(string parameterName, int value)
    {
        if (!TryGetParameter(parameterName, out int hash, out AnimatorControllerParameterType type)) return false;
        switch (type)
        {
            case AnimatorControllerParameterType.Int:
                anim.SetInteger(hash, value);
                return true;
            case AnimatorControllerParameterType.Float:
                anim.SetFloat(hash, value);
                return true;
            case AnimatorControllerParameterType.Trigger:
                anim.SetTrigger(hash);
                return true;
            default:
                return false;
        }
    }

    public bool SetParameterSafe(string parameterName, object value)
    {
        if (!TryGetParameter(parameterName, out int hash, out AnimatorControllerParameterType type)) return false;

        try
        {
            // Set based on parameter type
            switch (type)
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