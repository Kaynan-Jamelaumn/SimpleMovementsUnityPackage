
// Base class for special mechanic handlers
using System.Collections.Generic;
using UnityEngine;

public abstract class SpecialMechanicHandlerBase : MonoBehaviour, ISpecialMechanicHandler
{
    [Header("Supported Mechanics")]
    [SerializeField] protected List<string> supportedMechanics = new List<string>();

    [Header("Active Mechanics")]
    [SerializeField] protected Dictionary<string, bool> activeMechanics = new Dictionary<string, bool>();

    protected PlayerStatusController playerController;
    protected bool isInitialized = false;

    protected virtual void Awake()
    {
        playerController = GetComponent<PlayerStatusController>();
        InitializeMechanics();
    }

    protected virtual void InitializeMechanics()
    {
        isInitialized = true;
    }

    public virtual bool CanHandleMechanic(string mechanicId)
    {
        return supportedMechanics.Contains(mechanicId);
    }

    public abstract void ApplyMechanic(SpecialMechanic mechanic, bool enable);

    public virtual void UpdateMechanic(SpecialMechanic mechanic, Dictionary<string, float> parameters)
    {
        // Override in derived classes for dynamic updates
    }

    public virtual List<string> GetSupportedMechanics()
    {
        return new List<string>(supportedMechanics);
    }

    protected float GetParameterValue(SpecialMechanic mechanic, string parameterName, float defaultValue = 0f)
    {
        var parameter = mechanic.parameters?.Find(p => p.parameterName == parameterName);
        return parameter?.value ?? defaultValue;
    }

    protected void SetMechanicActive(string mechanicId, bool active)
    {
        activeMechanics[mechanicId] = active;
    }

    protected bool IsMechanicActive(string mechanicId)
    {
        return activeMechanics.TryGetValue(mechanicId, out bool active) && active;
    }
}










//using System.Collections.Generic;
//using UnityEngine;

//// Interface for components that can handle special mechanics
//public interface ISpecialMechanicHandler
//{
//    bool CanHandleMechanic(string mechanicId);
//    void ApplyMechanic(SpecialMechanic mechanic, bool enable);
//    void UpdateMechanic(SpecialMechanic mechanic, Dictionary<string, float> parameters);
//    List<string> GetSupportedMechanics();
//}

//// Base class for special mechanic handlers
//public abstract class SpecialMechanicHandlerBase : MonoBehaviour, ISpecialMechanicHandler
//{
//    [Header("Supported Mechanics")]
//    [SerializeField] protected List<string> supportedMechanics = new List<string>();

//    [Header("Active Mechanics")]
//    [SerializeField] protected Dictionary<string, bool> activeMechanics = new Dictionary<string, bool>();

//    protected PlayerStatusController playerController;
//    protected bool isInitialized = false;

//    protected virtual void Awake()
//    {
//        playerController = GetComponent<PlayerStatusController>();
//        InitializeMechanics();
//    }

//    protected virtual void InitializeMechanics()
//    {
//        isInitialized = true;
//    }

//    public virtual bool CanHandleMechanic(string mechanicId)
//    {
//        return supportedMechanics.Contains(mechanicId);
//    }

//    public abstract void ApplyMechanic(SpecialMechanic mechanic, bool enable);

//    public virtual void UpdateMechanic(SpecialMechanic mechanic, Dictionary<string, float> parameters)
//    {
//        // Override in derived classes for dynamic updates
//    }

//    public virtual List<string> GetSupportedMechanics()
//    {
//        return new List<string>(supportedMechanics);
//    }

//    protected float GetParameterValue(SpecialMechanic mechanic, string parameterName, float defaultValue = 0f)
//    {
//        var parameter = mechanic.parameters?.Find(p => p.parameterName == parameterName);
//        return parameter?.value ?? defaultValue;
//    }

//    protected void SetMechanicActive(string mechanicId, bool active)
//    {
//        activeMechanics[mechanicId] = active;
//    }

//    protected bool IsMechanicActive(string mechanicId)
//    {
//        return activeMechanics.TryGetValue(mechanicId, out bool active) && active;
//    }
//}

//// Movement-related special mechanics handler
//public class PlayerMovementController : SpecialMechanicHandlerBase
//{
//    [Header("Movement Mechanics")]
//    [SerializeField] private bool waterWalkingEnabled = false;
//    [SerializeField] private float gravityMultiplier = 1f;
//    [SerializeField] private float originalGravity = -9.81f;
//    [SerializeField] private LayerMask waterLayers = -1;

//    [Header("Components")]
//    [SerializeField] private Rigidbody playerRigidbody;
//    [SerializeField] private Collider playerCollider;

//    protected override void InitializeMechanics()
//    {
//        base.InitializeMechanics();

//        supportedMechanics.AddRange(new[]
//        {
//            "water_walking",
//            "gravity_reduction",
//            "enhanced_movement",
//            "phase_walking"
//        });

//        if (playerRigidbody == null)
//            playerRigidbody = GetComponent<Rigidbody>();

//        if (playerCollider == null)
//            playerCollider = GetComponent<Collider>();

//        // Store original gravity
//        if (Physics.gravity.y != 0)
//            originalGravity = Physics.gravity.y;
//    }

//    public override void ApplyMechanic(SpecialMechanic mechanic, bool enable)
//    {
//        if (!CanHandleMechanic(mechanic.mechanicId)) return;

//        switch (mechanic.mechanicId.ToLower())
//        {
//            case "water_walking":
//                ApplyWaterWalking(enable);
//                break;

//            case "gravity_reduction":
//                float reduction = GetParameterValue(mechanic, "reduction", 0.5f);
//                ApplyGravityReduction(reduction, enable);
//                break;

//            case "enhanced_movement":
//                float speedBoost = GetParameterValue(mechanic, "speed_boost", 1.5f);
//                ApplyMovementBoost(speedBoost, enable);
//                break;

//            case "phase_walking":
//                ApplyPhaseWalking(enable);
//                break;
//        }

//        SetMechanicActive(mechanic.mechanicId, enable);
//    }

//    private void ApplyWaterWalking(bool enable)
//    {
//        waterWalkingEnabled = enable;

//        if (enable)
//        {
//            // Add logic to detect water surfaces and prevent falling through
//            StartCoroutine(WaterWalkingCoroutine());
//        }

//        Debug.Log($"Water walking {(enable ? "enabled" : "disabled")}");
//    }

//    private void ApplyGravityReduction(float reduction, bool enable)
//    {
//        if (enable)
//        {
//            gravityMultiplier = 1f - Mathf.Clamp01(reduction);
//            Physics.gravity = new Vector3(Physics.gravity.x, originalGravity * gravityMultiplier, Physics.gravity.z);
//        }
//        else
//        {
//            gravityMultiplier = 1f;
//            Physics.gravity = new Vector3(Physics.gravity.x, originalGravity, Physics.gravity.z);
//        }

//        Debug.Log($"Gravity {(enable ? $"reduced to {gravityMultiplier * 100:F0}%" : "restored")}");
//    }

//    private void ApplyMovementBoost(float speedBoost, bool enable)
//    {
//        if (playerController?.SpeedManager != null)
//        {
//            float modifier = enable ? speedBoost - 1f : -(speedBoost - 1f);
//            playerController.SpeedManager.ModifyBaseSpeed(playerController.SpeedManager.BaseSpeed * modifier);
//        }

//        Debug.Log($"Movement boost {(enable ? $"applied ({speedBoost:F1}x)" : "removed")}");
//    }

//    private void ApplyPhaseWalking(bool enable)
//    {
//        if (playerCollider != null)
//        {
//            playerCollider.isTrigger = enable;
//        }

//        Debug.Log($"Phase walking {(enable ? "enabled" : "disabled")}");
//    }

//    private System.Collections.IEnumerator WaterWalkingCoroutine()
//    {
//        while (waterWalkingEnabled)
//        {
//            if (IsPlayerOnWater())
//            {
//                // Prevent player from falling through water
//                PreventWaterFall();
//            }

//            yield return new WaitForFixedUpdate();
//        }
//    }

//    private bool IsPlayerOnWater()
//    {
//        // Raycast down to check for water surfaces
//        var ray = new Ray(transform.position, Vector3.down);
//        return Physics.Raycast(ray, 2f, waterLayers);
//    }

//    private void PreventWaterFall()
//    {
//        if (playerRigidbody != null && playerRigidbody.velocity.y < 0)
//        {
//            // Stop downward movement when on water
//            var velocity = playerRigidbody.velocity;
//            velocity.y = Mathf.Max(velocity.y, 0);
//            playerRigidbody.velocity = velocity;
//        }
//    }

//    public void SetWaterWalking(bool enable)
//    {
//        waterWalkingEnabled = enable;
//        SetMechanicActive("water_walking", enable);
//    }

//    public void SetGravityMultiplier(float multiplier)
//    {
//        gravityMultiplier = multiplier;
//        Physics.gravity = new Vector3(Physics.gravity.x, originalGravity * gravityMultiplier, Physics.gravity.z);
//        SetMechanicActive("gravity_reduction", multiplier != 1f);
//    }
//}

//// Jump-related special mechanics handler
//public class PlayerJumpController : SpecialMechanicHandlerBase
//{
//    [Header("Jump Mechanics")]
//    [SerializeField] private bool doubleJumpEnabled = false;
//    [SerializeField] private int maxJumps = 1;
//    [SerializeField] private int currentJumps = 0;
//    [SerializeField] private float jumpForce = 10f;
//    [SerializeField] private float doubleJumpForceMultiplier = 0.8f;

//    [Header("Components")]
//    [SerializeField] private Rigidbody playerRigidbody;
//    [SerializeField] private GroundChecker groundChecker;

//    protected override void InitializeMechanics()
//    {
//        base.InitializeMechanics();

//        supportedMechanics.AddRange(new[]
//        {
//            "double_jump",
//            "enhanced_jump",
//            "infinite_jump",
//            "wall_jump"
//        });

//        if (playerRigidbody == null)
//            playerRigidbody = GetComponent<Rigidbody>();

//        if (groundChecker == null)
//            groundChecker = GetComponent<GroundChecker>();
//    }

//    public override void ApplyMechanic(SpecialMechanic mechanic, bool enable)
//    {
//        if (!CanHandleMechanic(mechanic.mechanicId)) return;

//        switch (mechanic.mechanicId.ToLower())
//        {
//            case "double_jump":
//                ApplyDoubleJump(enable);
//                break;

//            case "enhanced_jump":
//                float jumpBoost = GetParameterValue(mechanic, "jump_boost", 1.5f);
//                ApplyJumpBoost(jumpBoost, enable);
//                break;

//            case "infinite_jump":
//                ApplyInfiniteJump(enable);
//                break;

//            case "wall_jump":
//                ApplyWallJump(enable);
//                break;
//        }

//        SetMechanicActive(mechanic.mechanicId, enable);
//    }

//    private void ApplyDoubleJump(bool enable)
//    {
//        doubleJumpEnabled = enable;
//        maxJumps = enable ? 2 : 1;

//        Debug.Log($"Double jump {(enable ? "enabled" : "disabled")}");
//    }

//    private void ApplyJumpBoost(float jumpBoost, bool enable)
//    {
//        if (enable)
//        {
//            jumpForce *= jumpBoost;
//        }
//        else
//        {
//            jumpForce /= jumpBoost;
//        }

//        Debug.Log($"Jump boost {(enable ? $"applied ({jumpBoost:F1}x)" : "removed")}");
//    }

//    private void ApplyInfiniteJump(bool enable)
//    {
//        maxJumps = enable ? int.MaxValue : 1;

//        Debug.Log($"Infinite jump {(enable ? "enabled" : "disabled")}");
//    }

//    private void ApplyWallJump(bool enable)
//    {
//        // Wall jump logic would be implemented here
//        Debug.Log($"Wall jump {(enable ? "enabled" : "disabled")}");
//    }

//    public void SetDoubleJump(bool enable)
//    {
//        doubleJumpEnabled = enable;
//        maxJumps = enable ? 2 : 1;
//        SetMechanicActive("double_jump", enable);
//    }

//    public bool CanJump()
//    {
//        return currentJumps < maxJumps && (groundChecker?.IsGrounded() == true || currentJumps < maxJumps);
//    }

//    public void PerformJump()
//    {
//        if (!CanJump() || playerRigidbody == null) return;

//        float currentJumpForce = jumpForce;

//        // Reduce force for additional jumps
//        if (currentJumps > 0)
//        {
//            currentJumpForce *= doubleJumpForceMultiplier;
//        }

//        playerRigidbody.AddForce(Vector3.up * currentJumpForce, ForceMode.Impulse);
//        currentJumps++;
//    }

//    public void ResetJumps()
//    {
//        currentJumps = 0;
//    }

//    private void Update()
//    {
//        // Reset jumps when grounded
//        if (groundChecker?.IsGrounded() == true && currentJumps > 0)
//        {
//            ResetJumps();
//        }
//    }
//}

//// Environment interaction special mechanics handler
//public class EnvironmentInteractionController : SpecialMechanicHandlerBase
//{
//    [Header("Environment Mechanics")]
//    [SerializeField] private bool immunityEnabled = false;
//    [SerializeField] private List<string> immunityTypes = new List<string>();

//    [Header("Enhanced Interaction")]
//    [SerializeField] private float interactionRange = 1f;
//    [SerializeField] private bool enhancedInteractionEnabled = false;

//    protected override void InitializeMechanics()
//    {
//        base.InitializeMechanics();

//        supportedMechanics.AddRange(new[]
//        {
//            "environment_immunity",
//            "enhanced_interaction",
//            "temperature_resistance",
//            "poison_immunity"
//        });
//    }

//    public override void ApplyMechanic(SpecialMechanic mechanic, bool enable)
//    {
//        if (!CanHandleMechanic(mechanic.mechanicId)) return;

//        switch (mechanic.mechanicId.ToLower())
//        {
//            case "environment_immunity":
//                string immunityType = mechanic.parameters?.Find(p => p.parameterName == "immunity_type")?.value.ToString() ?? "all";
//                ApplyEnvironmentImmunity(immunityType, enable);
//                break;

//            case "enhanced_interaction":
//                float rangeMultiplier = GetParameterValue(mechanic, "range_multiplier", 2f);
//                ApplyEnhancedInteraction(rangeMultiplier, enable);
//                break;

//            case "temperature_resistance":
//                ApplyTemperatureResistance(enable);
//                break;

//            case "poison_immunity":
//                ApplyPoisonImmunity(enable);
//                break;
//        }

//        SetMechanicActive(mechanic.mechanicId, enable);
//    }

//    private void ApplyEnvironmentImmunity(string immunityType, bool enable)
//    {
//        if (enable)
//        {
//            if (!immunityTypes.Contains(immunityType))
//                immunityTypes.Add(immunityType);
//        }
//        else
//        {
//            immunityTypes.Remove(immunityType);
//        }

//        immunityEnabled = immunityTypes.Count > 0;

//        Debug.Log($"Environment immunity for {immunityType} {(enable ? "enabled" : "disabled")}");
//    }

//    private void ApplyEnhancedInteraction(float rangeMultiplier, bool enable)
//    {
//        if (enable)
//        {
//            interactionRange *= rangeMultiplier;
//        }
//        else
//        {
//            interactionRange /= rangeMultiplier;
//        }

//        enhancedInteractionEnabled = enable;

//        Debug.Log($"Enhanced interaction {(enable ? $"enabled (range: {interactionRange:F1})" : "disabled")}");
//    }

//    private void ApplyTemperatureResistance(bool enable)
//    {
//        if (playerController?.BodyHeatManager != null)
//        {
//            // Apply temperature resistance logic
//            // This would modify how temperature affects the player
//        }

//        Debug.Log($"Temperature resistance {(enable ? "enabled" : "disabled")}");
//    }

//    private void ApplyPoisonImmunity(bool enable)
//    {
//        // Apply poison immunity logic
//        // This would prevent poison effects from applying

//        Debug.Log($"Poison immunity {(enable ? "enabled" : "disabled")}");
//    }

//    public bool HasImmunity(string immunityType)
//    {
//        return immunityEnabled && (immunityTypes.Contains("all") || immunityTypes.Contains(immunityType));
//    }

//    public float GetInteractionRange()
//    {
//        return interactionRange;
//    }
//}

//// Simple ground checker utility
//public class GroundChecker : MonoBehaviour
//{
//    [Header("Ground Check")]
//    [SerializeField] private LayerMask groundMask = -1;
//    [SerializeField] private float checkDistance = 0.1f;
//    [SerializeField] private Vector3 checkOffset = Vector3.zero;

//    public bool IsGrounded()
//    {
//        Vector3 checkPosition = transform.position + checkOffset;
//        return Physics.Raycast(checkPosition, Vector3.down, checkDistance, groundMask);
//    }

//    private void OnDrawGizmosSelected()
//    {
//        Vector3 checkPosition = transform.position + checkOffset;
//        Gizmos.color = IsGrounded() ? Color.green : Color.red;
//        Gizmos.DrawRay(checkPosition, Vector3.down * checkDistance);
//    }
//}