using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The weapon in the player's hand and its attacks. The inventory equips the weapon of the selected hotbar slot
/// (<see cref="EquipWeapon(WeaponSO, InventoryItem)"/>); attack inputs call <see cref="BeginAttackInput"/> when pressed
/// and <see cref="ReleaseAttackInput"/> when released (charged attacks), or <see cref="PerformAttack"/> for a tap.
/// <para>
/// Every weapon is data: attacks, chains, charge, combos, status effects and behaviours come from the
/// <see cref="WeaponSO"/>. Parts: <see cref="WeaponManager"/> (equip), <see cref="AttackExecutor"/> (attack
/// timeline and hits), <see cref="ComboSystem"/>, <see cref="VariationSystem"/> (chains), <see cref="InputBufferSystem"/>.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    [Header("Weapon Setup")]
    [Tooltip("The hand (weapon socket). Trails, charge effects and the weapon's Attack Cast use it.")]
    [SerializeField] public GameObject handGameObject;
    [Tooltip("Plays attack animations. Found on the player when empty.")]
    [SerializeField] private PlayerAnimationController animController;

    [Header("Combat Settings")]
    [Tooltip("Seconds an attack input pressed during an attack is remembered (it starts when the attack can be cancelled).")]
    [SerializeField] private float inputBufferTime = 0.3f;
    [SerializeField] private bool enableInputBuffer = true;
    [SerializeField] private bool enableComboSystem = true;
    [Tooltip("Seconds between attacks that still continue a combo when the weapon has no combo tree.")]
    [SerializeField, Min(0.1f)] private float defaultComboWindow = 1.5f;
    [Tooltip("Attacks need (and spend) stamina.")]
    [SerializeField] private bool useStamina = true;

    [Header("Extra Attack Inputs (optional)")]
    [Tooltip("The primary attack comes from the inventory's Use Item input. These inputs trigger the other attacks without wiring events (leave empty if you call OnLightAttack/OnHeavyAttack... from a PlayerInput component instead).")]
    [SerializeField] private InputActionReference lightAttackInput;
    [SerializeField] private InputActionReference heavyAttackInput;
    [SerializeField] private InputActionReference specialAttackInput;
    [SerializeField] private InputActionReference alternateAttackInput;

    [Header("Debug")]
    [Tooltip("Log attacks, hits and combos to the Console.")]
    [SerializeField] private bool debugMode = false;

    // Component References
    private WeaponManager weaponManager;
    private AttackExecutor attackExecutor;
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;
    private AttackAnimationHandler animationHandler;
    private WeaponEffectsManager effectsManager;
    private WeaponStateCoordinator stateCoordinator;

    // Character references (found once)
    private PlayerStatusController status;
    private CombatEntity entity;
    private CombatStats stats;
    private EquipmentManager equipment;
    private AvailabilityStateMachine availability;
    private AudioSource audioSource;
    private readonly HashSet<AttackType> heldInputs = new HashSet<AttackType>();

    // Events
    /// <summary>An attack started (input, attack).</summary>
    public event Action<AttackType, AttackComponent> AttackStarted;
    /// <summary>An attack hit a character (target, damage dealt).</summary>
    public event Action<CombatEntity, float> AttackHit;
    /// <summary>An attack ended (input, attack, interrupted).</summary>
    public event Action<AttackType, AttackComponent, bool> AttackEnded;
    /// <summary>An attack could not start (input, reason: "stamina", "broken").</summary>
    public event Action<AttackType, string> AttackFailed;
    /// <summary>The weapon in hand changed.</summary>
    public event Action WeaponChanged;
    /// <summary>The held weapon's durability reached 0.</summary>
    public event Action<InventoryItem> HeldItemBroke;

    // Properties - Delegating to appropriate components
    public PlayerAnimationController AnimController { get => animController; set => animController = value; }
    public WeaponSO EquippedWeapon => weaponManager?.EquippedWeapon;
    /// <summary>The inventory item of the weapon in hand (durability), or null.</summary>
    public InventoryItem HeldItem => weaponManager?.HeldItem;
    public bool IsAttacking => attackExecutor?.IsAttacking ?? false;
    public bool IsCharging => attackExecutor?.IsCharging ?? false;
    public AttackExecutor.Phase AttackPhase => attackExecutor?.CurrentPhase ?? AttackExecutor.Phase.Idle;
    public AttackAction CurrentAttackAction => attackExecutor?.CurrentAttackAction;
    public AttackVariation CurrentAttackVariation => attackExecutor?.CurrentAttackVariation;
    /// <summary>Charge (0-1) of the attack being charged or performed (combo conditions, UI).</summary>
    public float PendingChargeRatio => attackExecutor?.ChargeRatio ?? 0f;
    public float ChargeHeldTime => attackExecutor?.ChargeHeldTime ?? 0f;
    public ComboSystem Combo => comboSystem;

    public GameObject PlayerObject => status != null ? status.gameObject : gameObject;
    public Transform HandTransform => handGameObject != null ? handGameObject.transform : null;

    /// <summary>
    /// The weapon model held in the hand (a child of the hand created from the weapon's prefab), or null. Weapon Blade
    /// hit detection measures the blade along it.
    /// </summary>
    public Transform HeldModel
    {
        get
        {
            Transform hand = HandTransform;
            if (hand == null) return null;
            GameObject prefab = EquippedWeapon != null ? EquippedWeapon.Prefab : null;
            Transform clone = null;
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform c = hand.GetChild(i);
                if (!c.gameObject.activeInHierarchy) continue;
                if (prefab != null && c.name.StartsWith(prefab.name, System.StringComparison.Ordinal)) return c;
                if (clone == null && c.name.EndsWith("(Clone)", System.StringComparison.Ordinal)) clone = c;
            }
            return clone;
        }
    }
    public CombatEntity Entity
    {
        get
        {
            if (entity == null)
            {
                entity = CombatEntity.Resolve(PlayerObject);
                if (entity == null && status != null)
                    entity = CombatEntity.GetOrAdd(status.gameObject);
            }
            return entity;
        }
    }
    public CombatStats Stats => stats != null ? stats : (stats = CombatStats.For(PlayerObject.transform));
    public EquipmentManager Equipment => equipment != null ? equipment : (equipment = status != null ? EquipmentManager.For(status) : GetComponentInParent<EquipmentManager>());
    public TraitManager Traits => status != null ? status.TraitManager : GetComponentInParent<TraitManager>();
    public SpeedManager SpeedManager => status != null ? status.SpeedManager : null;
    public float DefaultComboWindow => defaultComboWindow;

    // Unity Lifecycle
    private void Awake()
    {
        status = GetComponentInParent<PlayerStatusController>();
        if (status == null)
            status = GetComponentInChildren<PlayerStatusController>();
        if (animController == null)
            animController = GetComponentInParent<PlayerAnimationController>() ?? (status != null ? status.GetComponentInChildren<PlayerAnimationController>() : null);
        availability = PlayerObject.GetComponentInChildren<AvailabilityStateMachine>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && status != null)
            audioSource = status.GetComponent<AudioSource>();
        InitializeComponents();
    }

    private void Start()
    {
        variationSystem.Initialize();
    }

    private void OnEnable()
    {
        EnableInput(lightAttackInput);
        EnableInput(heavyAttackInput);
        EnableInput(specialAttackInput);
        EnableInput(alternateAttackInput);
    }

    private static void EnableInput(InputActionReference r)
    {
        if (r != null && r.action != null)
            r.action.Enable();
    }

    private void Update()
    {
        PollInput(lightAttackInput, AttackType.Light);
        PollInput(heavyAttackInput, AttackType.Heavy);
        PollInput(specialAttackInput, AttackType.Special);
        PollInput(alternateAttackInput, AttackType.Alternate);

        attackExecutor.Tick(Time.deltaTime);
        inputBufferSystem.ProcessInputBuffer();
        variationSystem.UpdateVariationTimers();
        comboSystem.UpdateComboTimer();

        // Stunned or dead mid-attack: the attack is interrupted.
        if (attackExecutor.IsAttacking && !CanActNow())
            attackExecutor.Cancel();
    }

    private void PollInput(InputActionReference r, AttackType type)
    {
        InputAction a = r != null ? r.action : null;
        if (a == null)
            return;
        if (a.WasPressedThisFrame())
            BeginAttackInput(type);
        if (a.WasReleasedThisFrame())
            ReleaseAttackInput(type);
    }

    private void OnDisable()
    {
        attackExecutor?.Cancel();
        heldInputs.Clear();
        animController?.ForceEndAttackAnimation();
    }

    private void OnDestroy()
    {
        weaponManager?.UnequipWeapon();
    }

    private void OnDrawGizmos()
    {
        if (EquippedWeapon?.attackCast != null && handGameObject != null && EquippedWeapon.HasUsableAttackCast)
            EquippedWeapon.attackCast.DrawGizmos(handGameObject.transform);
    }

    private void OnDrawGizmosSelected()
    {
        if (EquippedWeapon == null || handGameObject == null) return;
        DrawRangeGizmos();
        attackExecutor?.DrawDebugInfo(handGameObject.transform);
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

        attackExecutor.SetDependencies(comboSystem, variationSystem, inputBufferSystem, animationHandler, effectsManager);
        comboSystem.SetDependencies(attackExecutor, effectsManager);
        inputBufferSystem.SetDependencies(attackExecutor);
        stateCoordinator.SetDependencies(attackExecutor, comboSystem, variationSystem, inputBufferSystem);
        weaponManager.SetStateCoordinator(stateCoordinator);
    }

    private void OnValidate()
    {
        inputBufferSystem?.Configure(inputBufferTime, enableInputBuffer);
    }

    // ------------------------------------------------------------------ weapon management
    public void EquipWeapon(WeaponSO weaponSO) => weaponManager.EquipWeapon(weaponSO, null);

    /// <summary>Equips a weapon from the inventory (its item carries the durability).</summary>
    public void EquipWeapon(WeaponSO weaponSO, InventoryItem item) => weaponManager.EquipWeapon(weaponSO, item);

    public void UnequipWeapon() => weaponManager.UnequipWeapon();

    // ------------------------------------------------------------------ attacks
    /// <summary>An attack as a tap (charged attacks start uncharged). Kept for older callers.</summary>
    public void PerformAttack(GameObject player, AttackType attackType)
    {
        attackExecutor.BeginInput(player != null ? player : PlayerObject, attackType, false, true);
    }

    /// <summary>The attack input of <paramref name="attackType"/> was pressed (hold it to charge a chargeable attack).</summary>
    public bool BeginAttackInput(AttackType attackType)
    {
        heldInputs.Add(attackType);
        if (EquippedWeapon == null)
            return false;
        if (PlayerAbilityController.IsPointerCapturedFor(PlayerObject))
            return false; // the click belongs to an ability's target preview
        return attackExecutor.BeginInput(PlayerObject, attackType, false, false);
    }

    /// <summary>The attack input of <paramref name="attackType"/> was released.</summary>
    public void ReleaseAttackInput(AttackType attackType)
    {
        heldInputs.Remove(attackType);
        attackExecutor.ReleaseInput(attackType);
    }

    /// <summary>Is the input still held (buffered attacks of a released input are not charged)?</summary>
    public bool IsInputHeld(AttackType attackType) => heldInputs.Contains(attackType);

    /// <summary>Stops a charge without attacking.</summary>
    public void CancelCharge() => attackExecutor.CancelCharge();

    /// <summary>Interrupts the current attack (hit reactions, stuns, scripted events).</summary>
    public void InterruptAttack() => attackExecutor.Cancel();

    // PlayerInput "Invoke Unity Events" hooks
    public void OnLightAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Light);
    public void OnHeavyAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Heavy);
    public void OnSpecialAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Special);
    public void OnAlternateAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Alternate);

    private void HandleCallback(InputAction.CallbackContext value, AttackType type)
    {
        if (value.started) BeginAttackInput(type);
        else if (value.canceled) ReleaseAttackInput(type);
    }

    // ------------------------------------------------------------------ helpers for the attack parts
    internal bool CanActNow()
    {
        if (availability != null && !availability.CanAct())
            return false;
        CombatEntity e = Entity;
        return e == null || (e.IsAlive && !e.IsStunned);
    }

    internal bool HasStamina(float amount)
    {
        if (!useStamina || amount <= 0f || status == null || status.StaminaManager == null)
            return true;
        return status.StaminaManager.CurrentValue >= amount;
    }

    internal void ConsumeStamina(float amount)
    {
        if (useStamina && amount > 0f && status != null && status.StaminaManager != null)
            status.StaminaManager.ConsumeStamina(amount);
    }

    /// <summary>True when the held weapon has durability and it is used up.</summary>
    public bool IsHeldItemBroken
    {
        get
        {
            InventoryItem item = HeldItem;
            return item != null && EquippedWeapon != null && EquippedWeapon.DurabilityReductionPerUse > 0 && item.durability <= 0f;
        }
    }

    internal void ConsumeDurability()
    {
        InventoryItem item = HeldItem;
        WeaponSO weapon = EquippedWeapon;
        if (item == null || weapon == null || weapon.DurabilityReductionPerUse <= 0)
            return;
        item.durability -= weapon.DurabilityReductionPerUse;
        if (item.durability <= 0f)
        {
            item.durability = 0f;
            LogDebug($"{weapon.Name} is broken.");
            HeldItemBroke?.Invoke(item);
        }
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;
        if (audioSource != null)
            audioSource.PlayOneShot(clip);
        else
        {
            AudioSource playerSource = PlayerObject.GetComponent<Player>()?.PlayerAudioSource;
            if (playerSource != null) playerSource.PlayOneShot(clip);
            else AbilityPool.PlaySound(clip, transform.position, 1f);
        }
    }

    internal void RaiseAttackStarted(AttackType input, AttackComponent attack) => AttackStarted?.Invoke(input, attack);
    internal void RaiseAttackHit(CombatEntity target, float damage) => AttackHit?.Invoke(target, damage);
    internal void RaiseAttackEnded(AttackType input, AttackComponent attack, bool interrupted) => AttackEnded?.Invoke(input, attack, interrupted);
    internal void RaiseAttackFailed(AttackType input, string reason) => AttackFailed?.Invoke(input, reason);
    internal void RaiseWeaponChanged() => WeaponChanged?.Invoke();

    // ------------------------------------------------------------------ public API (kept)
    public bool CanAttack() => EquippedWeapon != null && (attackExecutor?.CanStartNext ?? false);

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
        foreach (AttackType attackType in Enum.GetValues(typeof(AttackType)))
            if (EquippedWeapon.HasAction(attackType))
                availableTypes.Add(attackType);
        return availableTypes;
    }

    public string GetAvailableActions() => string.Join(", ", GetAvailableAttackTypes());

    public int GetCurrentVariationIndex(AttackType attackType) => variationSystem.GetCurrentVariationIndex(attackType);

    public List<AttackType> GetCurrentComboSequence() => comboSystem.GetCurrentComboSequence();

    public bool IsInVariantWindow(AttackType attackType) => variationSystem.IsInVariantWindow(attackType);

    public AttackVariation GetCurrentVariation(AttackType attackType) => variationSystem.GetCurrentVariation(attackType);

    // Backward compatibility methods - maintaining original signatures
    public bool CanComboInto(AttackType attackType) => enableComboSystem && attackExecutor.CurrentAttackComponent != null && attackExecutor.CanStartNext;
    public float GetAttackCooldown(AttackType attackType) => GetActionCooldown(attackType);

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
        if (isError) Debug.LogWarning($"[WeaponController] {message}", this);
        else Debug.Log($"[WeaponController] {message}", this);
    }

    // Getters for components (used by other systems)
    public GameObject HandGameObject => handGameObject;
    public PlayerAnimationController GetAnimController() => animController;
    public bool EnableComboSystem => enableComboSystem;
    public bool EnableInputBuffer => enableInputBuffer;
    public float InputBufferTime => inputBufferTime;
    public bool DebugMode => debugMode;
}
