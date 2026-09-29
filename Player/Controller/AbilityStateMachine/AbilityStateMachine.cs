using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player ability key (listed in the AbilitiesStateMachine). It registers its slot with the
/// <see cref="PlayerAbilityController"/>, casts it when its input is pressed, and its states mirror the slot's
/// phase. The ability comes from the Ability Holder (new Ability field, or the legacy Ability Effect).
/// <para>For UI (hotbars, tooltips): <see cref="KeyLabel"/>, <see cref="DisplayName"/>, <see cref="Icon"/>,
/// <see cref="CooldownRemaining"/>, <see cref="Charges"/> and the <c>StateChanged</c> event; <see cref="CastNow"/>
/// casts it from a button, <see cref="SetAbility"/> swaps its ability at runtime.</para>
/// </summary>
[DefaultExecutionOrder(-5)]
public class AbilityStateMachine : StateManager<AbilityStateMachine.EAbilityState>
{
    public enum EAbilityState
    {
        Ready,
        Casting,
        Launching,
        Active,
        InCooldown
    }

    [Tooltip("The player's PlayerAbilityController. This ability key gets its live entry (cooldown, charges) there when the game starts. Empty = found on this object or a parent.")]
    [SerializeField] private PlayerAbilityController abilityController;
    [Tooltip("The player's animation model (cast animations). Empty = found in the children or parents.")]
    [SerializeField] private PlayerAnimationModel animationModel;
    [Tooltip("What this ability key casts: an Ability Definition (new system) or the legacy Ability Effect, plus Modifiers. Absorbed abilities replace it at runtime.")]
    [SerializeField] private PlayerAbilityHolder abilityHolder;

    /// <summary>
    /// The player's input actions. Shared by every key of the player (one copy per player instead of one per key);
    /// assign your own before Awake to use a different one.
    /// </summary>
    public PlayerInput playerInput;
    private bool sharedInput;
    private AbilityContext context;
    private AbilitiesStateMachine keys;

    /// <summary>Shared state of this slot (created on first access, so other components can subscribe early).</summary>
    public AbilityContext Context
    {
        get
        {
            EnsureContext();
            return context;
        }
    }

    public PlayerAbilityHolder AbilityHolder { get => abilityHolder; set => abilityHolder = value; }
    public PlayerAbilityController AbilityController => abilityController;

    /// <summary>Index of this machine's slot in the PlayerAbilityController.</summary>
    public int SlotIndex { get; private set; } = -1;

    public AbilitySlot Slot => abilityController != null ? abilityController.GetSlot(SlotIndex) : null;
    public bool HasAbility => Slot != null && Slot.ability != null;

    public bool Available() => Context.cachedAvailability;

    // ------------------------------------------------------------------ key information (UI)
    /// <summary>Position of this key in the AbilitiesStateMachine list (-1 = no input casts it).</summary>
    public int KeyIndex { get; private set; } = -1;

    /// <summary>The AbilitiesStateMachine that lists this key (null if none).</summary>
    public AbilitiesStateMachine Keys => keys;

    /// <summary>The input that casts this key (its reference, or the AbilityN fallback). Null if it has none.</summary>
    public InputAction InputAction => keys != null && KeyIndex >= 0 ? keys.GetKeyInput(KeyIndex) : null;

    /// <summary>The key as the player sees it ("Q", "Mouse 4", "Right Shoulder"...), for hotbars. Empty if none.</summary>
    public string KeyLabel
    {
        get
        {
            InputAction a = InputAction;
            return a != null ? a.GetBindingDisplayString() : "";
        }
    }

    /// <summary>Name of what this key casts (absorbed variants included).</summary>
    public string DisplayName
    {
        get
        {
            AbilitySlot s = Slot;
            if (s != null)
                return s.DisplayName;
            return abilityHolder != null && abilityHolder.ability != null ? abilityHolder.ability.DisplayName : "(empty)";
        }
    }

    /// <summary>Icon of the ability on this key (null if none).</summary>
    public Sprite Icon
    {
        get
        {
            AbilitySlot s = Slot;
            AbilityDefinition def = s != null ? s.ability : abilityHolder != null ? abilityHolder.ability : null;
            return def != null ? def.icon : null;
        }
    }

    /// <summary>Seconds until the key can be used again (0 = ready).</summary>
    public float CooldownRemaining => Slot != null ? Slot.CooldownRemaining : 0f;

    /// <summary>0 = ready, 1 = the cooldown just started (for UI fill).</summary>
    public float CooldownFraction => Slot != null ? Slot.CooldownFraction : 0f;

    public int Charges => Slot != null ? Slot.Charges : 0;
    public int MaxCharges => Slot != null ? Slot.MaxCharges : 0;

    /// <summary>Why the key could not start right now (None = it could, if the aim allows).</summary>
    public CastFailReason ReadyReason => abilityController != null ? abilityController.CheckSlotReady(SlotIndex) : CastFailReason.Disabled;

    /// <summary>Casts this key as if its input was pressed (UI buttons, touch controls). Returns true if something started.</summary>
    public bool CastNow() => abilityController != null && SlotIndex >= 0 && abilityController.TryCastFromInput(SlotIndex);

    /// <summary>
    /// Puts another ability on this key at runtime (skill trees, loadouts). Its cooldown starts fresh. Before the
    /// game starts it only changes the Ability Holder.
    /// </summary>
    public void SetAbility(AbilityDefinition ability, AbilityModifierSet modifiers = null)
    {
        if (abilityController != null && SlotIndex >= 0)
        {
            abilityController.SetSlot(SlotIndex, ability, modifiers);
            return;
        }
        if (abilityHolder == null)
            abilityHolder = new PlayerAbilityHolder();
        abilityHolder.ability = ability;
        abilityHolder.modifiers = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
    }

    /// <summary>Called by the AbilitiesStateMachine: which key this machine is (-1 / null = none).</summary>
    internal void BindKey(AbilitiesStateMachine owner, int index)
    {
        keys = owner;
        KeyIndex = owner != null ? index : -1;
    }

    // ------------------------------------------------------------------ setup
    /// <summary>Fills empty references from this object, its children and parents (inspector button).</summary>
    public void AutoAssignReferences()
    {
        if (abilityController == null) abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController == null) abilityController = GetComponentInParent<PlayerAbilityController>();
        if (animationModel == null) animationModel = GetComponentInChildren<PlayerAnimationModel>();
        if (animationModel == null) animationModel = GetComponentInParent<PlayerAnimationModel>();
        if (abilityHolder == null) abilityHolder = new PlayerAbilityHolder();
    }

    private void EnsureContext()
    {
        if (context != null)
            return;
        if (abilityController == null) abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController == null) abilityController = GetComponentInParent<PlayerAbilityController>();
        if (animationModel == null) animationModel = GetComponentInChildren<PlayerAnimationModel>();
        if (animationModel == null) animationModel = GetComponentInParent<PlayerAnimationModel>();
        if (abilityHolder == null) abilityHolder = new PlayerAbilityHolder();
        if (playerInput == null && Application.isPlaying)
        {
            // One copy of the input actions per player, shared by all its keys.
            playerInput = SharedPlayerInput.Acquire(this);
            sharedInput = true;
        }
        context = new AbilityContext(playerInput, animationModel, abilityController, abilityHolder) { Machine = this };
    }

    private void Awake()
    {
        EnsureContext();
        if (abilityController == null)
        {
            Debug.LogError($"[{name}] AbilityStateMachine needs a PlayerAbilityController on the player.", this);
        }
        else
        {
            SlotIndex = abilityController.RegisterStateMachine(this);
            abilityController.SlotChanged += OnSlotChanged;
        }
        InitializeStates();
    }

    private void OnEnable()
    {
        EnsureContext();
        SharedPlayerInput.Enable(playerInput);
    }

    private void OnDisable()
    {
        SharedPlayerInput.Disable(playerInput);
    }

    private void OnDestroy()
    {
        if (abilityController != null)
            abilityController.SlotChanged -= OnSlotChanged;
        context?.Dispose();
        if (sharedInput)
        {
            SharedPlayerInput.Release(playerInput);
            playerInput = null;
            sharedInput = false;
        }
    }

    private void InitializeStates()
    {
        States.Add(EAbilityState.Ready, new ReadyState(context, EAbilityState.Ready));
        States.Add(EAbilityState.Casting, new CastingState(context, EAbilityState.Casting));
        States.Add(EAbilityState.Launching, new LaunchingState(context, EAbilityState.Launching));
        States.Add(EAbilityState.Active, new ActiveState(context, EAbilityState.Active));
        States.Add(EAbilityState.InCooldown, new InCooldownState(context, EAbilityState.InCooldown));
        CurrentState = States[EAbilityState.Ready];
    }

    /// <summary>The slot's ability changed (absorbed, replaced): show it in the holder and refresh availability.</summary>
    private void OnSlotChanged(int index)
    {
        if (index != SlotIndex)
            return;
        AbilitySlot slot = Slot;
        if (slot != null && abilityHolder != null)
        {
            abilityHolder.ability = slot.ability;
            abilityHolder.modifiers = slot.modifiers != null ? slot.modifiers.Clone() : new AbilityModifierSet();
        }
        if (CurrentState is AbilityState)
        {
            AbilityDefinition def = slot != null ? slot.ability : null;
            context.SetCachedAvailability(def == null || !def.BlocksOthersDuring((AbilityPhase)(int)CurrentState.StateKey));
        }
    }
}
