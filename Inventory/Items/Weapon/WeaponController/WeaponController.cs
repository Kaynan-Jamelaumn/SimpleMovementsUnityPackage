using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [Header("Weapon Setup")]
    [SerializeField] public GameObject handGameObject;
    [SerializeField] private PlayerAnimationController animController;

    [Header("Combat Settings")]
    [SerializeField] private float inputBufferTime = 0.3f;
    [SerializeField] private bool enableInputBuffer = true;
    [SerializeField] private bool enableComboSystem = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Component References
    private WeaponManager weaponManager;
    private AttackExecutor attackExecutor;
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;
    private AttackAnimationHandler animationHandler;
    private WeaponEffectsManager effectsManager;
    private WeaponStateCoordinator stateCoordinator;

    // Properties - Delegating to appropriate components
    public PlayerAnimationController AnimController { get => animController; set => animController = value; }
    public WeaponSO EquippedWeapon => weaponManager?.EquippedWeapon;
    public bool IsAttacking => attackExecutor?.IsAttacking ?? false;
    public AttackAction CurrentAttackAction => attackExecutor?.CurrentAttackAction;
    public AttackVariation CurrentAttackVariation => attackExecutor?.CurrentAttackVariation;

    // Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        LogDebug($"WeaponController initialized. AnimController found: {animController != null}");
        variationSystem.Initialize();
    }

    private void Update()
    {
        variationSystem.UpdateVariationTimers();
        inputBufferSystem.ProcessInputBuffer();
        comboSystem.UpdateComboTimer();
    }

    private void OnDisable()
    {
        attackExecutor.CleanupAllCoroutines();
        attackExecutor.ResetAttackState();
        animController?.ForceEndAttackAnimation();
        LogDebug("WeaponController disabled - all states reset");
    }

    private void OnDrawGizmos()
    {
        if (EquippedWeapon?.attackCast != null && handGameObject != null)
        {
            EquippedWeapon.attackCast.DrawGizmos(handGameObject.transform);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (EquippedWeapon == null || handGameObject == null) return;
        DrawRangeGizmos();
        attackExecutor.DrawDebugInfo(handGameObject.transform);
    }

    // Initialization
    private void InitializeComponents()
    {
        weaponManager = new WeaponManager(this);
        attackExecutor = new AttackExecutor(this);
        comboSystem = new ComboSystem(this);
        variationSystem = new VariationSystem(this);
        inputBufferSystem = new InputBufferSystem(this, inputBufferTime, enableInputBuffer);
        animationHandler = new AttackAnimationHandler(this);
        effectsManager = new WeaponEffectsManager(this);
        stateCoordinator = new WeaponStateCoordinator(this);

        // Setup cross-references
        attackExecutor.SetDependencies(comboSystem, variationSystem, inputBufferSystem, animationHandler, effectsManager);
        comboSystem.SetDependencies(attackExecutor, effectsManager);
        inputBufferSystem.SetDependencies(attackExecutor);
        stateCoordinator.SetDependencies(attackExecutor, comboSystem, variationSystem, inputBufferSystem);
        weaponManager.SetStateCoordinator(stateCoordinator);
    }

    // Weapon Management - Delegated to WeaponManager
    public void EquipWeapon(WeaponSO weaponSO)
    {
        weaponManager.EquipWeapon(weaponSO);
    }

    public void UnequipWeapon()
    {
        weaponManager.UnequipWeapon();
    }

    // Attack Execution - Delegated to AttackExecutor
    public void PerformAttack(GameObject player, AttackType attackType)
    {
        attackExecutor.PerformAttack(player, attackType);
    }

    // Public API - Maintaining all original method names
    public bool CanAttack() => EquippedWeapon != null && !IsAttacking;

    public bool HasAction(AttackType attackType) => EquippedWeapon?.HasAction(attackType) ?? false;

    public float GetActionCooldown(AttackType attackType)
    {
        var action = EquippedWeapon?.GetAction(attackType);
        return action?.GetTotalDuration() ?? 0f;
    }

    public List<AttackType> GetAvailableAttackTypes()
    {
        var availableTypes = new List<AttackType>();

        if (EquippedWeapon == null) return availableTypes;

        foreach (AttackType attackType in System.Enum.GetValues(typeof(AttackType)))
        {
            if (EquippedWeapon.HasAction(attackType))
            {
                availableTypes.Add(attackType);
            }
        }

        return availableTypes;
    }

    public string GetAvailableActions()
    {
        var types = GetAvailableAttackTypes();
        return string.Join(", ", types);
    }

    public int GetCurrentVariationIndex(AttackType attackType) => variationSystem.GetCurrentVariationIndex(attackType);

    public List<AttackType> GetCurrentComboSequence() => comboSystem.GetCurrentComboSequence();

    public bool IsInVariantWindow(AttackType attackType) => variationSystem.IsInVariantWindow(attackType);

    public AttackVariation GetCurrentVariation(AttackType attackType) => variationSystem.GetCurrentVariation(attackType);

    // Backward compatibility methods - maintaining original signatures
    public bool CanComboInto(AttackType attackType) => enableComboSystem && attackExecutor.CurrentAttackComponent != null;
    public float GetAttackCooldown(AttackType attackType) => GetActionCooldown(attackType);

    // Helper Methods
    private void DrawRangeGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(handGameObject.transform.position, EquippedWeapon.MaxRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(handGameObject.transform.position, EquippedWeapon.MinRange);
    }

    public void LogDebug(string message, bool isError = false)
    {
        if (!debugMode) return;

        if (isError) Debug.LogError($"[WeaponController] {message}");
    }

    // Getters for components (used by other systems)
    public GameObject HandGameObject => handGameObject;
    public PlayerAnimationController GetAnimController() => animController;
    public bool EnableComboSystem => enableComboSystem;
    public bool EnableInputBuffer => enableInputBuffer;
    public float InputBufferTime => inputBufferTime;
    public bool DebugMode => debugMode;
}