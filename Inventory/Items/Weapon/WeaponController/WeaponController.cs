using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The weapons in the player's hands and their attacks. The inventory equips the weapon of the selected hotbar slot in
/// the MAIN hand (<see cref="EquipWeapon(WeaponSO, InventoryItem)"/>) and the item of the Off Hand slot in the OFF hand
/// (<see cref="SetOffHand"/>: a second one-handed weapon for dual wielding, or a shield). Attack inputs call
/// <see cref="BeginAttackInput"/> when pressed and <see cref="ReleaseAttackInput"/> when released (charged attacks and
/// bow draws), or <see cref="PerformAttack"/> for a tap; <see cref="AttackType.OffHand"/> attacks with the off-hand weapon.
/// <para>
/// Every weapon is data: attacks, chains, charge, combos, status effects, behaviours, how it is held (one / two hands,
/// dual wielding), blocking (its guard) and shooting (a ranged mechanic: draw, magazine, throw) come from the
/// <see cref="WeaponSO"/>. Parts: <see cref="WeaponManager"/> (equip), <see cref="AttackExecutor"/> (attack timeline,
/// hits, projectiles), a <see cref="ComboSystem"/> and <see cref="VariationSystem"/> per hand, <see cref="InputBufferSystem"/>.
/// </para>
/// <para>
/// Network sessions: the attack rules run only where <see cref="RunsRules"/> is true (offline, the server, the host).
/// A client's own player sends its inputs and aim to the server through <see cref="NetBridge"/>; every client copy
/// shows the attacks the server reports (the Present methods). Only the player of this machine reads its devices and
/// camera. Offline nothing is installed and everything works as before.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    [Header("Weapon Setup")]
    [Tooltip("The hand (weapon socket). Trails, charge effects and the weapon's Attack Cast use it.")]
    [SerializeField] public GameObject handGameObject;
    [Tooltip("The OFF hand (left hand) socket: off-hand weapons are held here, and their trails and hit volumes follow it. " +
             "Empty = found automatically (the humanoid left hand bone).")]
    [SerializeField] public GameObject offHandGameObject;
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
    [Tooltip("Attacks with the OFF-HAND weapon while dual wielding (optional; see Dual Wielding for the other ways).")]
    [SerializeField] private InputActionReference offHandAttackInput;

    [Header("Dual Wielding")]
    [Tooltip("While dual wielding, the Alternate attack input attacks with the off-hand weapon (instead of the main weapon's Alternate attack).")]
    [SerializeField] private bool alternateInputUsesOffHand = true;
    [Tooltip("Actions looked up by name in the player's input actions for the off-hand attack (when Off Hand Attack Input is empty).")]
    [SerializeField] private string[] offHandActionNames = { "OffHandAttack", "LeftAttack", "SecondaryAttack" };
    [Tooltip("While dual wielding without an off-hand input action, the right mouse button attacks with the off-hand weapon.")]
    [SerializeField] private bool rightMouseOffHandFallback = true;

    [Header("Ranged Weapons")]
    [Tooltip("Actions looked up by name in the player's input actions for reloading.")]
    [SerializeField] private string[] reloadActionNames = { "Reload" };
    [Tooltip("Key that reloads when the input actions have no reload action (None = no key). Not used if another action already uses it.")]
    [SerializeField] private Key reloadFallbackKey = Key.R;
    [Tooltip("Camera that aims ranged weapons (the crosshair, or the mouse while the cursor is free). Empty = the main camera.")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Layers the aim ray stops on. Nothing = Combat Settings' character and obstacle layers.")]
    [SerializeField] private LayerMask aimLayers = 0;

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

    // Hands
    private readonly WeaponHand mainHand = new WeaponHand(WeaponHandSide.Main);
    private readonly WeaponHand offHand = new WeaponHand(WeaponHandSide.Off);
    private readonly object offHandEquipKey = new object();
    private WeaponHand conditionHand;
    private Transform autoOffHand;

    // Character references (found once)
    private PlayerStatusController status;
    private CombatEntity entity;
    private CombatStats stats;
    private EquipmentManager equipment;
    private AvailabilityStateMachine availability;
    private AudioSource audioSource;
    private BlockController block;
    private BodyPartController bodyParts;
    private readonly HashSet<AttackType> heldInputs = new HashSet<AttackType>();
    private readonly Dictionary<AttackType, AttackType> pressMapping = new Dictionary<AttackType, AttackType>();

    // Input (named actions and fallbacks)
    private CombatInputBinding offHandBinding, offHandMouse, reloadBinding;
    private bool bindingsCreated;
    private static readonly RaycastHit[] aimHits = new RaycastHit[16];

    // Network (unused offline)
    private readonly HashSet<AttackType> forwardedHeld = new HashSet<AttackType>();
    private readonly List<AttackType> releaseBuffer = new List<AttackType>(4);
    private bool hasRemoteAim;
    private Ray remoteAim;

    // Events
    /// <summary>An attack started (input, attack).</summary>
    public event Action<AttackType, AttackComponent> AttackStarted;
    /// <summary>An attack hit a character (target, damage dealt).</summary>
    public event Action<CombatEntity, float> AttackHit;
    /// <summary>An attack ended (input, attack, interrupted).</summary>
    public event Action<AttackType, AttackComponent, bool> AttackEnded;
    /// <summary>An attack could not start (input, reason: "stamina", "broken", "ammo", "empty", "reloading", "interval").</summary>
    public event Action<AttackType, string> AttackFailed;
    /// <summary>The weapon in hand changed.</summary>
    public event Action WeaponChanged;
    /// <summary>The off-hand item changed (a weapon, a shield, or nothing).</summary>
    public event Action OffHandChanged;
    /// <summary>A held item's durability reached 0 (the main weapon, the off-hand weapon or the shield).</summary>
    public event Action<InventoryItem> HeldItemBroke;
    /// <summary>A ranged weapon fired (hand, number of projectiles).</summary>
    public event Action<WeaponHandSide, int> RangedFired;
    /// <summary>A reload started (true) or finished (false).</summary>
    public event Action<WeaponHandSide, bool> ReloadStateChanged;

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
    /// <summary>The combo state used by combo conditions (the hand being evaluated; the main hand otherwise).</summary>
    public ComboSystem Combo => conditionHand != null ? conditionHand.Combo : comboSystem;
    /// <summary>The weapon combo conditions look at (the hand being evaluated; the main weapon otherwise).</summary>
    public WeaponSO ConditionWeapon => conditionHand != null ? conditionHand.Weapon : EquippedWeapon;

    // Hands
    public WeaponHand MainHand => mainHand;
    public WeaponHand OffHand => offHand;
    public WeaponHand GetHand(WeaponHandSide side) => side == WeaponHandSide.Off ? offHand : mainHand;
    /// <summary>The weapon in the off hand (dual wielding), or null.</summary>
    public WeaponSO OffHandWeapon => offHand.Weapon;
    public InventoryItem OffHandItem => offHand.Item;
    /// <summary>The shield in the off hand (active: not stowed by a two-handed weapon), or null.</summary>
    public ArmorSO OffHandShield { get; private set; }
    public InventoryItem OffHandShieldItem { get; private set; }
    /// <summary>A weapon in each hand.</summary>
    public bool IsDualWielding => EquippedWeapon != null && offHand.Weapon != null;
    /// <summary>The main weapon needs both hands.</summary>
    public bool IsTwoHanding => EquippedWeapon != null && EquippedWeapon.IsTwoHanded;
    /// <summary>The character's block controller (null when it cannot block).</summary>
    public BlockController Block => block != null ? block : (block = PlayerObject.GetComponentInChildren<BlockController>());
    /// <summary>Shows the hands' items on the character (set by <see cref="EquipmentVisuals"/>).</summary>
    public IWeaponVisualHost Visuals { get; set; }
    /// <summary>Ammunition and thrown items (set by the inventory). Null = unlimited.</summary>
    public IAmmoSource AmmoSource { get; set; }
    /// <summary>True while a UI (the inventory) has the input: attack, block and reload inputs are ignored.</summary>
    public bool InputBlocked { get; set; }

    /// <summary>
    /// Network: sends this player's attack, reload and throw intentions to the server. Installed by the network layer on
    /// the owning client's copy only; null offline, on the server and on copies of other players.
    /// </summary>
    public IWeaponNetBridge NetBridge { get; set; }
    /// <summary>
    /// Network: receives what this controller's attacks show (charges, swings, shots, hits, impacts, reloads) where the
    /// rules run, so the network layer can repeat it on clients. Null offline.
    /// </summary>
    public IWeaponPresentationSink PresentationSink { get; set; }
    /// <summary>
    /// Do this controller's attack rules run on this machine? True offline, on the server and on the host. False on a
    /// client's copy of a networked player: inputs are sent to the server and attacks are only shown (see the Present
    /// methods), never resolved here.
    /// </summary>
    public bool RunsRules => GameNet.NetAuthority.RunsRules(PlayerObject);
    /// <summary>Is this the player whose devices and camera are on this machine? Offline: true.</summary>
    public bool IsLocallyControlled => GameNet.NetAuthority.IsLocallyControlled(PlayerObject);
    /// <summary>Are attack inputs of this client's player still held (sent to the server, not yet released)?</summary>
    public bool HasForwardedHeldInputs => forwardedHeld.Count > 0;
    /// <summary>Server: is the aim of a remote owner being followed (see <see cref="SetRemoteAim"/>)?</summary>
    public bool HasRemoteAim => hasRemoteAim;

    /// <summary>
    /// Server: ranged attacks of this player aim along this ray (its owner's camera ray, streamed by the network layer)
    /// instead of a camera of this machine.
    /// </summary>
    public void SetRemoteAim(Vector3 origin, Vector3 direction)
    {
        remoteAim = new Ray(origin, direction);
        hasRemoteAim = true;
    }

    /// <summary>Server: stops following a remote aim.</summary>
    public void ClearRemoteAim() => hasRemoteAim = false;

    public GameObject PlayerObject => status != null ? status.gameObject : gameObject;
    public Transform HandTransform => handGameObject != null ? handGameObject.transform : null;
    /// <summary>The off-hand socket (assigned, else the humanoid left hand, else the main hand).</summary>
    public Transform OffHandTransform
    {
        get
        {
            if (offHandGameObject != null) return offHandGameObject.transform;
            if (autoOffHand == null)
            {
                Animator anim = PlayerObject.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                    autoOffHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            return autoOffHand != null ? autoOffHand : HandTransform;
        }
    }

    /// <summary>Puts the hands' items in the hands now (attacks, blocks); nothing happens without equipment visuals.</summary>
    public void DrawWeapons()
    {
        IWeaponVisualHost host = Visuals;
        if (host is UnityEngine.Object o && o == null)
        {
            Visuals = null;
            return;
        }
        host?.DrawWeapons();
    }

    /// <summary>The socket of a hand.</summary>
    public Transform SocketFor(WeaponHandSide side) => side == WeaponHandSide.Off ? OffHandTransform : HandTransform;

    /// <summary>
    /// The weapon model held in the hand (a child of the hand created from the weapon's prefab), or null. Weapon Blade
    /// hit detection measures the blade along it.
    /// </summary>
    public Transform HeldModel => HeldModelFor(WeaponHandSide.Main);

    /// <summary>The model of a hand's weapon (from the visuals, else a child of the hand made from the weapon's prefab), or null.</summary>
    public Transform HeldModelFor(WeaponHandSide side)
    {
        IWeaponVisualHost host = Visuals;
        if (host is UnityEngine.Object o && o == null)
            host = Visuals = null;
        Transform v = host != null ? host.ModelFor(side) : null;
        if (v != null)
            return v;
        Transform hand = SocketFor(side);
        if (hand == null) return null;
        WeaponSO weapon = GetHand(side).Weapon;
        GameObject prefab = weapon != null ? weapon.Visuals.ModelFor(weapon) : null;
        Transform clone = null;
        for (int i = 0; i < hand.childCount; i++)
        {
            Transform c = hand.GetChild(i);
            if (!c.gameObject.activeInHierarchy) continue;
            if (prefab != null && c.name.StartsWith(prefab.name, StringComparison.Ordinal)) return c;
            if (clone == null && c.name.EndsWith("(Clone)", StringComparison.Ordinal)) clone = c;
        }
        return clone;
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
        offHand.Variations.Initialize();
        bodyParts = BodyPartController.For(PlayerObject.transform);
        if (IsLocallyControlled)
            EnsureInputBindings();
    }

    /// <summary>
    /// Creates the device bindings (off-hand attack, reload) of the player of this machine. Copies of other players in a
    /// network session never get them: they would read this machine's keyboard and mouse.
    /// </summary>
    private void EnsureInputBindings()
    {
        if (bindingsCreated)
            return;
        bindingsCreated = true;
        if (CombatInputBinding.IsPlayer(this))
        {
            offHandBinding = CombatInputBinding.Create(this, offHandAttackInput, offHandActionNames, Key.None, MouseFallback.None, "Off-hand attack");
            if (rightMouseOffHandFallback && !offHandBinding.HasAction)
                offHandMouse = CombatInputBinding.Create(this, null, null, Key.None, MouseFallback.RightButton, "Off-hand attack (mouse)");
            reloadBinding = CombatInputBinding.Create(this, null, reloadActionNames, reloadFallbackKey, MouseFallback.None, "Reload");
        }
    }

    private void OnEnable()
    {
        EnableInput(lightAttackInput);
        EnableInput(heavyAttackInput);
        EnableInput(specialAttackInput);
        EnableInput(alternateAttackInput);
        EnableInput(offHandAttackInput);
    }

    private static void EnableInput(InputActionReference r)
    {
        if (r != null && r.action != null)
            r.action.Enable();
    }

    private void Update()
    {
        // Only the player of this machine reads devices (in a session the copies of other players must not react to
        // this keyboard; the server's copy of a remote player gets its inputs from the network layer).
        if (IsLocallyControlled)
        {
            EnsureInputBindings();
            if (!InputBlocked)
            {
                PollInput(lightAttackInput, AttackType.Light);
                PollInput(heavyAttackInput, AttackType.Heavy);
                PollInput(specialAttackInput, AttackType.Special);
                PollInput(alternateAttackInput, AttackType.Alternate);
                PollOffHandInput();
                if (reloadBinding != null && reloadBinding.Pressed)
                    Reload();
            }
            else
            {
                // Releases still count (a held draw is let go while a window opens).
                ReleaseIfReleased(lightAttackInput, AttackType.Light);
                ReleaseIfReleased(heavyAttackInput, AttackType.Heavy);
                ReleaseIfReleased(specialAttackInput, AttackType.Special);
                ReleaseIfReleased(alternateAttackInput, AttackType.Alternate);
            }
        }

        float dt = Time.deltaTime;
        attackExecutor.Tick(dt);
        if (!RunsRules)
        {
            // A client's copy: reloads, automatic fire, buffers and combos are the server's; only the crosshair
            // bloom of the shots it showed settles here.
            DecayBloom(mainHand, dt);
            DecayBloom(offHand, dt);
            return;
        }

        UpdateRules(dt);
    }

    /// <summary>The per-frame rules: reloads, buffered inputs, automatic fire, chain and combo timers, interruptions.</summary>
    private void UpdateRules(float dt)
    {
        UpdateRanged(mainHand, dt);
        UpdateRanged(offHand, dt);
        inputBufferSystem.ProcessInputBuffer();
        AutomaticFire();
        variationSystem.UpdateVariationTimers();
        offHand.Variations.UpdateVariationTimers();
        comboSystem.UpdateComboTimer();
        offHand.Combo.UpdateComboTimer();

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

    private void ReleaseIfReleased(InputActionReference r, AttackType type)
    {
        InputAction a = r != null ? r.action : null;
        if (a != null && a.WasReleasedThisFrame())
            ReleaseAttackInput(type);
    }

    private void PollOffHandInput()
    {
        if (offHandBinding != null && offHandBinding.HasAction)
        {
            if (offHandBinding.Pressed) BeginAttackInput(AttackType.OffHand);
            if (offHandBinding.Released) ReleaseAttackInput(AttackType.OffHand);
            return;
        }
        if (offHandMouse == null || !offHandMouse.HasAny)
            return;
        if (offHandMouse.Pressed && IsDualWielding && !PlayerAbilityController.IsPointerCapturedFor(PlayerObject))
            BeginAttackInput(AttackType.OffHand);
        if (offHandMouse.Released && heldInputs.Contains(AttackType.OffHand))
            ReleaseAttackInput(AttackType.OffHand);
    }

    private void OnDisable()
    {
        // A client's own player: inputs still held on the server are let go there too.
        if (forwardedHeld.Count > 0 && NetBridge != null && !RunsRules)
        {
            releaseBuffer.Clear();
            releaseBuffer.AddRange(forwardedHeld);
            foreach (AttackType t in releaseBuffer)
                NetBridge.SendAttackInput(this, WeaponInputKind.Release, t);
            releaseBuffer.Clear();
        }
        attackExecutor?.Cancel();
        heldInputs.Clear();
        pressMapping.Clear();
        forwardedHeld.Clear();
        CancelReload(mainHand);
        CancelReload(offHand);
        animController?.ForceEndAttackAnimation();
    }

    private void OnDestroy()
    {
        weaponManager?.UnequipWeapon();
        UnequipOffHandWeapon(false);
        offHandBinding?.Dispose();
        offHandMouse?.Dispose();
        reloadBinding?.Dispose();
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

        mainHand.Combo = comboSystem;
        mainHand.Variations = variationSystem;
        offHand.Combo = new ComboSystem(this, () => offHand.Weapon);
        offHand.Variations = new VariationSystem(this, () => offHand.Weapon);

        attackExecutor.SetDependencies(comboSystem, variationSystem, inputBufferSystem, animationHandler, effectsManager);
        comboSystem.SetDependencies(attackExecutor, effectsManager);
        offHand.Combo.SetDependencies(attackExecutor, effectsManager);
        inputBufferSystem.SetDependencies(attackExecutor);
        stateCoordinator.SetDependencies(attackExecutor, comboSystem, variationSystem, inputBufferSystem);
        weaponManager.SetStateCoordinator(stateCoordinator);
    }

    private void OnValidate()
    {
        inputBufferSystem?.Configure(inputBufferTime, enableInputBuffer);
    }

    /// <summary>Combo conditions look at <paramref name="hand"/> while a combo of that hand is chosen.</summary>
    internal ConditionScope ConditionsFor(WeaponHand hand) => new ConditionScope(this, hand);

    internal readonly struct ConditionScope : IDisposable
    {
        private readonly WeaponController controller;
        private readonly WeaponHand previous;

        public ConditionScope(WeaponController c, WeaponHand hand)
        {
            controller = c;
            previous = c.conditionHand;
            c.conditionHand = hand;
        }

        public void Dispose() => controller.conditionHand = previous;
    }

    // ------------------------------------------------------------------ weapon management
    public void EquipWeapon(WeaponSO weaponSO) => weaponManager.EquipWeapon(weaponSO, null);

    /// <summary>Equips a weapon from the inventory (its item carries the durability).</summary>
    public void EquipWeapon(WeaponSO weaponSO, InventoryItem item) => weaponManager.EquipWeapon(weaponSO, item);

    public void UnequipWeapon() => weaponManager.UnequipWeapon();

    /// <summary>Called by the weapon manager when the main weapon changed.</summary>
    internal void OnMainWeaponChanged(WeaponSO weapon, InventoryItem item)
    {
        if (mainHand.Weapon != weapon || mainHand.Item != item)
        {
            CancelReload(mainHand);
            mainHand.ResetRangedState();
        }
        mainHand.Weapon = weapon;
        mainHand.Item = item;
        InitializeMagazine(mainHand);
        // A two-handed weapon in the main hand: the off-hand weapon cannot stay ready (the inventory stows it too).
        if (weapon != null && weapon.IsTwoHanded && offHand.Weapon != null)
            UnequipOffHandWeapon(true);
    }

    /// <summary>
    /// Sets the ACTIVE off-hand item (after the hand rules: a two-handed main weapon gives null): a weapon (dual wielding),
    /// a shield (blocking), or nothing.
    /// </summary>
    public void SetOffHand(InventoryItem item)
    {
        ItemSO so = item != null ? item.itemScriptableObject : null;
        if (so != null && !HandRules.CanHoldInOffHand(so, out _))
            so = null;
        if (EquippedWeapon != null && EquippedWeapon.IsTwoHanded)
            so = null;

        if (so is WeaponSO weapon)
        {
            bool changed = OffHandShield != null;
            OffHandShield = null;
            OffHandShieldItem = null;
            changed |= EquipOffHandWeapon(weapon, item);
            if (changed) OffHandChanged?.Invoke();
        }
        else if (so is ArmorSO shield)
        {
            bool changed = UnequipOffHandWeapon(true);
            if (OffHandShield != shield || OffHandShieldItem != item)
            {
                OffHandShield = shield;
                OffHandShieldItem = item;
                changed = true;
            }
            if (changed) OffHandChanged?.Invoke();
        }
        else
        {
            bool changed = UnequipOffHandWeapon(true) | OffHandShield != null;
            OffHandShield = null;
            OffHandShieldItem = null;
            if (changed) OffHandChanged?.Invoke();
        }
    }

    private bool EquipOffHandWeapon(WeaponSO weapon, InventoryItem item)
    {
        if (offHand.Weapon == weapon && offHand.Item == item)
            return false;
        if (offHand.Weapon != null)
            UnequipOffHandWeapon(false);
        offHand.Weapon = weapon;
        offHand.Item = item;
        offHand.ResetRangedState();
        InitializeMagazine(offHand);
        offHand.Variations.Reset();
        offHand.Combo.Reset();
        Equipment?.Equip(offHandEquipKey, weapon, "Off Hand");
        PlaySound(weapon.EquipSound);
        LogDebug($"Off hand: {weapon.Name}");
        return true;
    }

    private bool UnequipOffHandWeapon(bool playEffects)
    {
        if (offHand.Weapon == null)
            return false;
        WeaponSO old = offHand.Weapon;
        if (attackExecutor != null && attackExecutor.CurrentHand == offHand)
            attackExecutor.Cancel();
        CancelReload(offHand);
        Equipment?.Unequip(offHandEquipKey);
        offHand.Weapon = null;
        offHand.Item = null;
        offHand.Variations?.Reset();
        offHand.Combo?.Reset();
        if (playEffects)
            PlaySound(old.UnequipSound);
        return true;
    }

    // ------------------------------------------------------------------ attacks
    /// <summary>An attack as a tap (charged attacks start uncharged). Kept for older callers.</summary>
    public void PerformAttack(GameObject player, AttackType attackType)
    {
        if (!RunsRules)
        {
            NetBridge?.SendAttackInput(this, WeaponInputKind.Tap, attackType);
            return;
        }
        if (BodyBlocksAttack(attackType))
        {
            RaiseAttackFailed(attackType, "injured");
            return;
        }
        attackExecutor.BeginInput(player != null ? player : PlayerObject, attackType, false, true);
    }

    /// <summary>The attack input of <paramref name="attackType"/> was pressed (hold it to charge a chargeable attack).</summary>
    /// <remarks>
    /// On a client's copy of a networked player the press is sent to the server, which runs this same method on its copy
    /// (damage, costs and ammo are decided there); the attack is shown here when the server reports it.
    /// </remarks>
    public bool BeginAttackInput(AttackType attackType)
    {
        if (!RunsRules)
            return ForwardPress(attackType);
        AttackType mapped = attackType == AttackType.Alternate && alternateInputUsesOffHand && IsDualWielding ? AttackType.OffHand : attackType;
        pressMapping[attackType] = mapped;
        heldInputs.Add(mapped);
        if (InputBlocked)
            return false;
        if (mapped == AttackType.OffHand ? offHand.Weapon == null && EquippedWeapon == null : EquippedWeapon == null)
            return false;
        if (PlayerAbilityController.IsPointerCapturedFor(PlayerObject))
            return false; // the click belongs to an ability's target preview
        BlockController b = Block;
        if (b != null && b.IsBlocking)
            return b.TryBash(); // attacking with the block up = shield bash
        if ((bodyParts != null && bodyParts.WeaponAttacksDisabled) || BodyBlocksAttack(mapped))
        {
            RaiseAttackFailed(mapped, "injured");
            return false;
        }
        DrawWeapons();
        return attackExecutor.BeginInput(PlayerObject, mapped, false, false);
    }

    /// <summary>
    /// The character's body stops this attack (injuries: both arms broken, the off hand missing for an off-hand attack).
    /// The weapon of a hand that cannot hold it is never equipped in the first place (the inventory's hand rules).
    /// </summary>
    private bool BodyBlocksAttack(AttackType mapped)
    {
        CombatEntity me = Entity;
        if (me == null)
            return false;
        BodyAction needed = mapped == AttackType.OffHand ? BodyAction.WeaponAttacks | BodyAction.OffHand : BodyAction.WeaponAttacks;
        return !me.AllowsAction(needed);
    }

    /// <summary>The attack input of <paramref name="attackType"/> was released.</summary>
    public void ReleaseAttackInput(AttackType attackType)
    {
        if (!RunsRules)
        {
            if (forwardedHeld.Remove(attackType))
                NetBridge?.SendAttackInput(this, WeaponInputKind.Release, attackType);
            return;
        }
        AttackType mapped = pressMapping.TryGetValue(attackType, out AttackType m) ? m : attackType;
        pressMapping.Remove(attackType);
        heldInputs.Remove(mapped);
        attackExecutor.ReleaseInput(mapped);
    }

    /// <summary>
    /// A client's copy of its own player: sends the press to the server after the checks that belong to this machine
    /// (UI focus, an ability preview owning the click, nothing in hand). Returns true when it was sent.
    /// </summary>
    private bool ForwardPress(AttackType attackType)
    {
        IWeaponNetBridge bridge = NetBridge;
        if (bridge == null)
            return false; // a copy of another player: it never attacks from this machine
        forwardedHeld.Add(attackType);
        if (InputBlocked)
            return false;
        AttackType mapped = attackType == AttackType.Alternate && alternateInputUsesOffHand && IsDualWielding ? AttackType.OffHand : attackType;
        if (mapped == AttackType.OffHand ? offHand.Weapon == null && EquippedWeapon == null : EquippedWeapon == null)
            return false;
        if (PlayerAbilityController.IsPointerCapturedFor(PlayerObject))
            return false; // the click belongs to an ability's target preview
        bridge.SendAttackInput(this, WeaponInputKind.Press, attackType);
        return true;
    }

    /// <summary>
    /// Server: a remote owner pressed an attack input. Same as <see cref="BeginAttackInput"/>, plus the item cooldown the
    /// inventory applies to its own player's primary attack (the Use input): a weapon on cooldown does not attack, and a
    /// started attack starts the cooldown.
    /// </summary>
    public bool ServerBeginAttackInput(AttackType attackType)
    {
        if (!RunsRules)
            return false;
        bool primary = attackType == AttackType.Normal;
        InventoryItem item = HeldItem;
        WeaponSO weapon = EquippedWeapon;
        if (primary && item != null && item.IsOnCooldown())
            return false;
        bool started = BeginAttackInput(attackType);
        if (started && primary && item != null && weapon != null && weapon.Cooldown > 0f)
            item.SetCooldown(CombatStats.ItemCooldown(this, weapon.Cooldown));
        return started;
    }

    /// <summary>
    /// Server: a remote owner tapped an attack input (pressed and released at once, see <see cref="PerformAttack"/>),
    /// with the same item cooldown as <see cref="ServerBeginAttackInput"/>.
    /// </summary>
    public bool ServerTapAttack(AttackType attackType)
    {
        if (!RunsRules)
            return false;
        bool primary = attackType == AttackType.Normal;
        InventoryItem item = HeldItem;
        WeaponSO weapon = EquippedWeapon;
        if (primary && item != null && item.IsOnCooldown())
            return false;
        if (BodyBlocksAttack(attackType))
            return false;
        bool started = attackExecutor.BeginInput(PlayerObject, attackType, false, true);
        if (started && primary && item != null && weapon != null && weapon.Cooldown > 0f)
            item.SetCooldown(CombatStats.ItemCooldown(this, weapon.Cooldown));
        return started;
    }

    /// <summary>
    /// Server: drops every attack input of a remote owner that left or lost the character: held inputs and buffered
    /// presses are forgotten and a charge in progress is cancelled (it does not go off as a release would).
    /// </summary>
    public void DropRemoteInputs()
    {
        heldInputs.Clear();
        pressMapping.Clear();
        inputBufferSystem?.Reset();
        attackExecutor?.CancelCharge();
    }

    /// <summary>Is the input still held (buffered attacks of a released input are not charged)?</summary>
    public bool IsInputHeld(AttackType attackType) => heldInputs.Contains(attackType);

    /// <summary>Stops a charge without attacking.</summary>
    public void CancelCharge()
    {
        if (!RunsRules)
        {
            // The charge runs on the server; ask it to drop it (the presentation stops when the server reports it).
            if (NetBridge != null && (attackExecutor.IsCharging || forwardedHeld.Count > 0))
                NetBridge.SendAttackInput(this, WeaponInputKind.CancelCharge, AttackType.Normal);
            return;
        }
        attackExecutor.CancelCharge();
    }

    /// <summary>Interrupts the current attack (hit reactions, stuns, scripted events).</summary>
    public void InterruptAttack() => attackExecutor.Cancel();

    // PlayerInput "Invoke Unity Events" hooks
    public void OnLightAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Light);
    public void OnHeavyAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Heavy);
    public void OnSpecialAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Special);
    public void OnAlternateAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.Alternate);
    public void OnOffHandAttack(InputAction.CallbackContext value) => HandleCallback(value, AttackType.OffHand);
    public void OnReload(InputAction.CallbackContext value)
    {
        if (value.started) Reload();
    }

    private void HandleCallback(InputAction.CallbackContext value, AttackType type)
    {
        if (value.started) BeginAttackInput(type);
        else if (value.canceled) ReleaseAttackInput(type);
    }

    // ------------------------------------------------------------------ ranged
    /// <summary>
    /// Throws one <paramref name="weapon"/> from <paramref name="item"/>'s stack right away (a throwable in a quickslot),
    /// without changing the weapon in hand. Returns false when it cannot be thrown now.
    /// </summary>
    public bool QuickThrow(WeaponSO weapon, InventoryItem item)
    {
        if (!RunsRules)
            return weapon != null && NetBridge != null && NetBridge.SendQuickThrow(this, weapon);
        BlockController b = Block;
        if (b != null && b.IsBlocking)
            b.Lower(true);
        return attackExecutor.QuickFire(weapon, item);
    }

    /// <summary>Rounds loaded in a hand's magazine weapon (0 for other weapons).</summary>
    public int LoadedRounds(WeaponHandSide side = WeaponHandSide.Main)
    {
        WeaponHand h = GetHand(side);
        return h.Ranged != null && h.Ranged.UsesMagazine ? Mathf.Max(0, h.Loaded) : 0;
    }

    /// <summary>Ammo the character carries for a hand's ranged weapon (int.MaxValue = unlimited).</summary>
    public int ReserveAmmo(WeaponHandSide side = WeaponHandSide.Main)
    {
        WeaponHand h = GetHand(side);
        RangedMechanic m = h.Ranged;
        if (m == null || m.ammo == null || !m.ammo.Needed)
            return int.MaxValue;
        return CountAmmo(m.ammo, h.loadedAmmo, out _);
    }

    public bool IsReloading(WeaponHandSide side = WeaponHandSide.Main) => GetHand(side).reloading;

    /// <summary>Starts reloading the main weapon (or the off-hand one when the main one cannot). Returns true if a reload started.</summary>
    public bool Reload()
    {
        if (InputBlocked)
            return false;
        if (!RunsRules)
        {
            // The server reloads (ammo is spent there); the reload shows here when it reports it.
            if (NetBridge == null || (!(mainHand.Ranged is MagazineMechanic) && !(offHand.Ranged is MagazineMechanic)))
                return false;
            NetBridge.SendAttackInput(this, WeaponInputKind.Reload, AttackType.Normal);
            return true;
        }
        return TryStartReload(mainHand) || TryStartReload(offHand);
    }

    /// <summary>
    /// Server: throws one <paramref name="weapon"/> for a client's quick-throw request, from the stack its inventory
    /// (an <see cref="IThrowableSource"/>) names, else from a hand holding that weapon. Returns false when nothing can be
    /// thrown. The stack's item cooldown is checked and started here, as the inventory does for the local player.
    /// </summary>
    public bool ServerQuickThrow(WeaponSO weapon)
    {
        if (weapon == null || !(weapon.Ranged is ThrowMechanic) || !RunsRules)
            return false;
        InventoryItem stack = null;
        IAmmoSource source = AmmoSource;
        if (source is UnityEngine.Object o && o == null)
            source = null;
        if (source is IThrowableSource throwables)
            stack = throwables.FindThrowable(weapon);
        else if (source is InventoryManager inventory)
        {
            // As the quickslots do: the first stack of that weapon not on cooldown (bag, then hotbar).
            List<InventoryItem> stacks = inventory.FindStacks(weapon, true);
            for (int i = 0; i < stacks.Count && stack == null; i++)
                if (stacks[i] != null && !stacks[i].IsOnCooldown())
                    stack = stacks[i];
        }
        if (stack == null && HeldItem != null && EquippedWeapon == weapon)
            stack = HeldItem;
        if (stack == null && OffHandItem != null && OffHandWeapon == weapon)
            stack = OffHandItem;
        if (stack == null || stack.itemScriptableObject != weapon || stack.IsOnCooldown())
            return false;
        if (!QuickThrow(weapon, stack))
            return false;
        if (weapon.Cooldown > 0f)
            stack.SetCooldown(CombatStats.ItemCooldown(this, weapon.Cooldown));
        return true;
    }

    internal int CountAmmo(AmmoRequirement req, AmmoSO preferred, out AmmoSO best)
    {
        best = preferred;
        if (req == null || !req.Needed)
            return int.MaxValue;
        IAmmoSource src = AmmoSource;
        if (src == null || (src is UnityEngine.Object o && o == null))
            return int.MaxValue; // no inventory: unlimited
        return src.CountAmmo(req.ammoType, preferred, out best);
    }

    internal int SpendAmmo(AmmoSO ammo, int amount)
    {
        IAmmoSource src = AmmoSource;
        if (ammo == null || amount <= 0 || src == null || (src is UnityEngine.Object o && o == null))
            return amount;
        return src.ConsumeAmmo(ammo, amount);
    }

    internal void SpendThrownItem(InventoryItem item)
    {
        IAmmoSource src = AmmoSource;
        if (item == null || src == null || (src is UnityEngine.Object o && o == null))
            return;
        src.ConsumeItem(item, 1);
    }

    private void InitializeMagazine(WeaponHand h)
    {
        if (h.Ranged is MagazineMechanic mag && h.Loaded < 0)
            h.Loaded = mag.startsLoaded ? mag.MagazineSize : 0;
    }

    internal bool TryStartReload(WeaponHand h)
    {
        if (!(h.Ranged is MagazineMechanic mag) || h.reloading)
            return false;
        if (h.Loaded >= mag.MagazineSize)
            return false;
        if (!CanActNow() || (attackExecutor.IsAttacking && attackExecutor.CurrentHand == h))
            return false;
        int reserve = CountAmmo(mag.ammo, h.loadedAmmo, out AmmoSO ammo);
        if (reserve < Mathf.Max(1, mag.ammo.perShot))
        {
            PlaySound(mag.emptySound);
            RaiseAttackFailed(h.IsMain ? AttackType.Normal : AttackType.OffHand, "ammo");
            return false;
        }
        h.loadedAmmo = ammo;
        BeginReloadPresentation(h, mag);
        ApplyReloadMovement(h, mag.moveSpeedWhileReloading);
        ReloadStateChanged?.Invoke(h.Side, true);
        PresentationSink?.ReloadShown(this, h.Side, true);
        LogDebug($"Reloading {h.Weapon.Name} ({h.Side})");
        return true;
    }

    /// <summary>The visible part of a reload start (timer for the UI, animation, sound).</summary>
    private void BeginReloadPresentation(WeaponHand h, MagazineMechanic mag)
    {
        h.reloading = true;
        h.reloadDuration = Mathf.Max(0.01f, mag.reloadTime / (Stats != null ? Stats.ReloadSpeedMultiplier : 1f));
        h.reloadEndTime = Time.time + h.reloadDuration;
        if (mag.reloadAnimation != null)
            animController?.PlayAnimation(mag.reloadAnimation);
        PlaySound(mag.reloadSound);
    }

    private void UpdateRanged(WeaponHand h, float dt)
    {
        RangedMechanic m = h.Ranged;
        if (m is MagazineMechanic mag)
        {
            if (h.bloom > 0f)
                h.bloom = Mathf.Max(0f, h.bloom - mag.bloomRecovery * dt);
            if (h.reloading)
            {
                if (!CanActNow())
                {
                    CancelReload(h);
                    return;
                }
                if (Time.time >= h.reloadEndTime)
                    FinishReload(h, mag);
            }
        }
        else if (h.reloading)
        {
            CancelReload(h);
        }
    }

    private void FinishReload(WeaponHand h, MagazineMechanic mag)
    {
        int loaded = Mathf.Max(0, h.Loaded);
        int perShot = Mathf.Max(1, mag.ammo.perShot);
        int wantedRounds = mag.reloadOneAtATime ? 1 : mag.MagazineSize - loaded;
        int rounds = wantedRounds;
        if (mag.ammo.Needed)
        {
            int reserve = CountAmmo(mag.ammo, h.loadedAmmo, out AmmoSO ammo);
            h.loadedAmmo = ammo;
            rounds = Mathf.Min(wantedRounds, reserve / perShot);
            if (rounds > 0)
                rounds = SpendAmmo(ammo, rounds * perShot) / perShot;
        }
        h.Loaded = Mathf.Min(mag.MagazineSize, loaded + Mathf.Max(0, rounds));
        bool more = mag.reloadOneAtATime && h.Loaded < mag.MagazineSize && rounds > 0 && CountAmmo(mag.ammo, h.loadedAmmo, out _) >= perShot;
        if (more)
        {
            h.reloadEndTime = Time.time + h.reloadDuration;
            PlaySound(mag.reloadSound);
            return;
        }
        EndReload(h);
        LogDebug($"Reloaded {h.Weapon.Name}: {h.Loaded}/{mag.MagazineSize}");
    }

    /// <summary>Stops a reload in progress (nothing is loaded; ammo is only spent when a reload completes).</summary>
    public void CancelReload(WeaponHand h)
    {
        if (h == null || !h.reloading)
            return;
        EndReload(h);
    }

    private void EndReload(WeaponHand h)
    {
        h.reloading = false;
        RestoreReloadMovement(h);
        ReloadStateChanged?.Invoke(h.Side, false);
        PresentationSink?.ReloadShown(this, h.Side, false);
    }

    private void ApplyReloadMovement(WeaponHand h, float multiplier)
    {
        SpeedManager sm = SpeedManager;
        if (sm == null || Mathf.Approximately(multiplier, 1f) || h.reloadMoveApplied)
            return;
        float before = sm.Speed;
        sm.ModifySpeed(before * (Mathf.Clamp01(multiplier) - 1f));
        h.reloadSpeedDelta = sm.Speed - before;
        h.reloadMoveApplied = true;
    }

    private void RestoreReloadMovement(WeaponHand h)
    {
        if (!h.reloadMoveApplied)
            return;
        SpeedManager sm = SpeedManager;
        if (sm != null && !Mathf.Approximately(h.reloadSpeedDelta, 0f))
            sm.ModifySpeed(-h.reloadSpeedDelta);
        h.reloadSpeedDelta = 0f;
        h.reloadMoveApplied = false;
    }

    /// <summary>Automatic weapons keep firing while their input is held.</summary>
    private void AutomaticFire()
    {
        if (InputBlocked)
            return;
        TryAutoFire(mainHand, AttackType.Normal);
        TryAutoFire(offHand, AttackType.OffHand);
    }

    private void TryAutoFire(WeaponHand h, AttackType input)
    {
        RangedMechanic m = h.Ranged;
        if (m == null || !m.Automatic || !heldInputs.Contains(input) || attackExecutor.IsCharging || !attackExecutor.CanStartNext)
            return;
        if (Time.time < h.nextFireTime || h.reloading)
            return;
        if (m is MagazineMechanic && h.Loaded <= 0)
            return; // released and pressed again to reload / hear the click
        attackExecutor.BeginInput(PlayerObject, input, true, false);
    }

    /// <summary>
    /// Where a ranged attack aims: what is under the crosshair (or the mouse while the cursor is free), up to
    /// <paramref name="maxDistance"/>; without a camera, straight ahead.
    /// </summary>
    public Vector3 AimPoint(Vector3 from, float maxDistance, bool useCrosshair)
    {
        CombatEntity me = Entity;
        if (useCrosshair && TryGetAimRay(out Ray ray))
        {
            int mask = aimLayers.value != 0 ? aimLayers.value : (CombatSettings.Instance.characterLayers | CombatSettings.Instance.obstacleLayers);
            float camDist = Vector3.Distance(ray.origin, from);
            int n;
            // A remote shooter aimed at where its screen showed the others (no-op offline and for the host's player).
            using (GameNet.LagCompensation.For(me))
                n = ScenePhysics.RaycastNonAlloc(ray, aimHits, maxDistance + camDist, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = ray.GetPoint(maxDistance + camDist);
            Transform root = PlayerObject.transform;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = aimHits[i];
                if (h.collider == null || h.distance >= best || h.collider.transform.IsChildOf(root))
                    continue;
                if (h.distance < camDist * 0.5f)
                    continue; // between the camera and the character
                if (me != null && CombatRules.AreSeparated(me, CombatEntity.Resolve(h.collider)))
                    continue; // a character of another realm scene (City / World overlap): not there for this shooter
                best = h.distance;
                point = h.point;
            }
            return point;
        }
        Vector3 f = me != null ? me.Forward : PlayerObject.transform.forward;
        return from + f * maxDistance;
    }

    /// <summary>
    /// The ray ranged weapons aim along: on the server, the aim its remote owner streams; otherwise the crosshair of this
    /// machine's camera (or the mouse while the cursor is free). False when there is neither (a dedicated server before
    /// the owner's aim arrived, a copy of another player): attacks then aim straight ahead.
    /// </summary>
    public bool TryGetAimRay(out Ray ray)
    {
        if (hasRemoteAim)
        {
            ray = remoteAim;
            return true;
        }
        ray = default;
        if (!IsLocallyControlled)
            return false; // this machine's camera belongs to another player
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
            return false;
        bool cursorFree = Cursor.visible && Cursor.lockState == CursorLockMode.None;
        ray = cursorFree && Mouse.current != null
            ? cam.ScreenPointToRay(Mouse.current.position.ReadValue())
            : cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return true;
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
        // Only where the rules run: on a network client the server spends it and the movement reconcile brings it back.
        if (useStamina && amount > 0f && status != null && status.StaminaManager != null && GameNet.NetAuthority.RunsRules(gameObject))
            status.StaminaManager.ConsumeStamina(amount);
    }

    private ManaManager mana;

    private ManaManager Mana
    {
        get
        {
            if (mana == null)
                mana = status != null && status.ManaManager != null ? status.ManaManager : GetComponentInParent<ManaManager>();
            return mana;
        }
    }

    /// <summary>
    /// Can the shot's Extra Costs be paid (mana, health, stamina, items)? <paramref name="reason"/> = what is missing
    /// ("mana", "health", "stamina", "item:Name").
    /// </summary>
    internal bool CanPayShotCosts(RangedMechanic m, out string reason)
    {
        reason = "";
        if (m == null || m.extraCosts == null)
            return true;
        foreach (ShotCost c in m.extraCosts)
        {
            if (c == null || c.amount <= 0f)
                continue;
            switch (c.resource)
            {
                case ShotResource.Stamina:
                    if (!HasStamina(c.amount)) { reason = "stamina"; return false; }
                    break;
                case ShotResource.Mana:
                    ManaManager mm = Mana;
                    if (mm != null && !mm.HasEnougCurrentValue(c.amount)) { reason = "mana"; return false; }
                    break;
                case ShotResource.Health:
                    CombatEntity e = Entity;
                    if (e != null && e.Health != null && e.Health.CurrentValue <= c.amount) { reason = "health"; return false; }
                    break;
                case ShotResource.Item:
                    if (c.item != null && AmmoSource != null && AmmoSource.CountItem(c.item, true) < Mathf.Max(1, Mathf.RoundToInt(c.amount)))
                    {
                        reason = "item:" + c.item.Name;
                        return false;
                    }
                    break;
            }
        }
        return true;
    }

    /// <summary>Pays the shot's Extra Costs (checked first with <see cref="CanPayShotCosts"/>).</summary>
    internal void PayShotCosts(RangedMechanic m)
    {
        if (m == null || m.extraCosts == null)
            return;
        foreach (ShotCost c in m.extraCosts)
        {
            if (c == null || c.amount <= 0f)
                continue;
            switch (c.resource)
            {
                case ShotResource.Stamina:
                    ConsumeStamina(c.amount);
                    break;
                case ShotResource.Mana:
                    Mana?.ConsumeMana(c.amount);
                    break;
                case ShotResource.Health:
                    CombatEntity e = Entity;
                    if (e != null && e.Health != null)
                        e.Health.ConsumeHP(c.amount, true);
                    break;
                case ShotResource.Item:
                    if (c.item != null && AmmoSource != null)
                        AmmoSource.RemoveItems(c.item, Mathf.Max(1, Mathf.RoundToInt(c.amount)));
                    break;
            }
        }
    }

    /// <summary>Current stamina (float.MaxValue when stamina is not used).</summary>
    public float CurrentStamina => useStamina && status != null && status.StaminaManager != null ? status.StaminaManager.CurrentValue : float.MaxValue;

    /// <summary>True when the held weapon has durability and it is used up.</summary>
    public bool IsHeldItemBroken => IsBroken(HeldItem, EquippedWeapon);

    internal static bool IsBroken(InventoryItem item, ItemSO itemSO) =>
        item != null && itemSO != null && itemSO.DurabilityReductionPerUse > 0 && item.durability <= 0f;

    internal void ConsumeDurability() => ConsumeDurability(HeldItem, EquippedWeapon);

    /// <summary>Wears an item held in a hand (an attack, a blocked hit). Raises <see cref="HeldItemBroke"/> when it breaks.</summary>
    internal void ConsumeDurability(InventoryItem item, ItemSO itemSO, int amount = -1)
    {
        if (item == null || itemSO == null)
            return;
        int loss = amount >= 0 ? amount : itemSO.DurabilityReductionPerUse;
        if (loss <= 0 || item.durability <= 0f)
            return;
        item.durability -= loss;
        if (item.durability <= 0f)
        {
            item.durability = 0f;
            LogDebug($"{itemSO.Name} is broken.");
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
    internal void RaiseAttackFailed(AttackType input, string reason)
    {
        AttackFailed?.Invoke(input, reason);
        PresentationSink?.AttackFailedShown(this, input, reason);
    }
    internal void RaiseWeaponChanged() => WeaponChanged?.Invoke();
    internal void RaiseRangedFired(WeaponHandSide side, int count) => RangedFired?.Invoke(side, count);

    // ------------------------------------------------------------------ network presentation (client copies)
    // A client's copy of a networked player shows what the server's copy does: the network layer calls these with what
    // the server reported. They play animations, trails, sounds and effects only - no damage, costs or ammo - and do
    // nothing where the rules run.

    /// <summary>Network: shows a charge (bow draw) the server began.</summary>
    public void PresentCharge(in WeaponAttackShown attack)
    {
        if (RunsRules)
            return;
        attackExecutor.PresentCharge(GetHand(attack.hand), attack);
    }

    /// <summary>Network: shows an attack the server started (or a quick throw).</summary>
    public void PresentAttack(in WeaponAttackShown attack)
    {
        if (RunsRules)
            return;
        attackExecutor.PresentAttack(GetHand(attack.hand), attack);
    }

    /// <summary>Network: the server cut the attack or charge short.</summary>
    public void PresentAttackStopped(bool interrupted)
    {
        if (RunsRules)
            return;
        attackExecutor.PresentStop(interrupted);
    }

    /// <summary>Network: shows a shot the server fired (projectiles that hit nothing, muzzle flash, sound).</summary>
    public void PresentShot(in WeaponShotShown shot)
    {
        if (RunsRules)
            return;
        WeaponHand h = GetHand(shot.hand);
        if (h.Ranged is MagazineMechanic mag && h.Weapon == shot.weapon)
            h.bloom = Mathf.Min(mag.maxBloom, h.bloom + mag.bloomPerShot); // the crosshair spreads as on the server
        attackExecutor.PresentShot(shot);
    }

    private static void DecayBloom(WeaponHand h, float dt)
    {
        if (h.bloom > 0f && h.Ranged is MagazineMechanic mag)
            h.bloom = Mathf.Max(0f, h.bloom - mag.bloomRecovery * dt);
    }

    /// <summary>Network: shows a hit the server's attack made (particles, hit effect, sound).</summary>
    public void PresentHit(in WeaponHitShown hit)
    {
        if (RunsRules)
            return;
        attackExecutor.PresentHit(hit);
    }

    /// <summary>Network: shows an attack's Impact the server made.</summary>
    public void PresentImpact(in WeaponImpactShown impact)
    {
        if (RunsRules)
            return;
        attackExecutor.PresentImpact(impact);
    }

    /// <summary>Network: a hand started or stopped reloading on the server (animation, sound, the reload UI).</summary>
    public void PresentReload(WeaponHandSide side, bool started)
    {
        if (RunsRules)
            return;
        WeaponHand h = GetHand(side);
        if (h.reloading == started)
            return;
        if (started)
        {
            if (h.Ranged is MagazineMechanic mag)
                BeginReloadPresentation(h, mag);
            else
                h.reloading = true;
        }
        else
        {
            h.reloading = false;
        }
        ReloadStateChanged?.Invoke(side, started);
    }

    /// <summary>Network (the owner): an attack of this player could not start on the server (UI messages, the empty click).</summary>
    public void PresentAttackFailed(AttackType input, string reason)
    {
        if (RunsRules)
            return;
        AttackFailed?.Invoke(input, reason);
        if (string.IsNullOrEmpty(reason) || reason == "reloading" || reason == "stamina" || reason == "broken" || reason == "injured")
            return;
        // Out of ammo or a shot cost missing: the weapon clicks.
        RangedMechanic m = GetHand(input == AttackType.OffHand && offHand.Weapon != null ? WeaponHandSide.Off : WeaponHandSide.Main).Ranged;
        if (m != null)
            PlaySound(m.emptySound);
    }

    /// <summary>Network (the owner): the rounds the server has loaded in a hand's magazine weapon (ammo counters).</summary>
    public void ApplyReplicatedRounds(WeaponHandSide side, int loaded)
    {
        if (RunsRules)
            return;
        WeaponHand h = GetHand(side);
        if (h.Ranged is MagazineMechanic)
            h.Loaded = loaded;
    }

    /// <summary>Network: rounds loaded in a hand's weapon as stored (-1 = not loaded yet), for replication.</summary>
    public int RawLoadedRounds(WeaponHandSide side) => GetHand(side).Loaded;

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
        if (IsDualWielding)
            availableTypes.Add(AttackType.OffHand);
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
    /// <summary>The input behind the off-hand attack (for UI and logs).</summary>
    public string OffHandInputSource => offHandBinding != null && offHandBinding.HasAction ? offHandBinding.Source
        : alternateInputUsesOffHand ? "Alternate attack input" + (offHandMouse != null && offHandMouse.HasAny ? " / right mouse" : "") : "none";
}
