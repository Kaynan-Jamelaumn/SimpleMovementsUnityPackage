using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's ability keys: each entry pairs an input with an AbilityStateMachine (what it casts). While available,
/// a pressed input casts its key; nothing can be cast while an ability blocks the others or the player is stunned,
/// silenced or dead. Presses made slightly too early are remembered for <see cref="InputBufferTime"/> seconds.
/// Keys can be added, removed and rebound at runtime (<see cref="AddKey"/>, <see cref="RemoveKey"/>, <see cref="SetKeyInput"/>).
/// </summary>
public class AbilitiesStateMachine : StateManager<AbilitiesStateMachine.EAbilitiesState>
{
    AbilitiesContext context;

    public enum EAbilitiesState
    {
        Available,
        Unavailable,
    }

    [Tooltip("The player's PlayerAbilityController. Empty = found on this object or a parent.")]
    [SerializeField] private PlayerAbilityController abilityController;
    [Tooltip("The player's animation model. Empty = found in the children.")]
    [SerializeField] private PlayerAnimationModel animationModel;
    [Tooltip("Blocks abilities while the player is stunned, silenced or dead. Empty = found on this object or a parent.")]
    [SerializeField] private AvailabilityStateMachine availabilityStateMachine;

    [Tooltip("The player's ABILITY KEYS: each line = an input + the AbilityStateMachine that stores the ability it casts. Easiest to edit in the PlayerAbilityController inspector ('Ability Keys').")]
    [SerializeField] private List<AbilityAction> abilityAction = new List<AbilityAction>();

    [Header("Input")]
    [Tooltip("A key pressed up to this many seconds before its ability can start (another ability still busy, the global cooldown, the last moment of its own cooldown) is remembered and cast as soon as it can. Presses that fail for other reasons (not enough mana, silenced) are reported at once. 0 = early presses are ignored (the old behaviour).")]
    [SerializeField, Range(0f, 0.5f)] private float inputBufferTime = 0.15f;

    private PlayerInput playerInput;

    public IReadOnlyList<AbilityAction> AbilityActions => abilityAction;

    /// <summary>Seconds an early press is remembered (0 = off). Can be changed at runtime (e.g. from the options menu).</summary>
    public float InputBufferTime
    {
        get => inputBufferTime;
        set
        {
            inputBufferTime = Mathf.Max(0f, value);
            if (context != null)
                context.InputBufferTime = inputBufferTime;
        }
    }

    /// <summary>The key waiting in the input buffer (-1 = none).</summary>
    public int BufferedKey => context != null ? context.BufferedKey : -1;

    /// <summary>The player's shared input actions (the same copy the ability keys and traits read).</summary>
    public PlayerInput InputActions => playerInput;

    /// <summary>Fills empty references from this object, its children and parents (inspector button).</summary>
    public void AutoAssignReferences()
    {
        if (abilityController == null) abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController == null) abilityController = GetComponentInParent<PlayerAbilityController>();
        if (animationModel == null) animationModel = GetComponentInChildren<PlayerAnimationModel>();
        if (availabilityStateMachine == null) availabilityStateMachine = GetComponent<AvailabilityStateMachine>();
        if (availabilityStateMachine == null) availabilityStateMachine = GetComponentInParent<AvailabilityStateMachine>();
    }

    /// <summary>
    /// Adds a binding for every AbilityStateMachine under the player that is not bound yet (its input is taken from
    /// its Ability Holder when set). Returns how many were added.
    /// </summary>
    public int AddMissingBindings()
    {
        Transform root = PlayerRoot(this);
        int added = 0;
        foreach (AbilityStateMachine m in root.GetComponentsInChildren<AbilityStateMachine>(true))
        {
            if (abilityAction.Exists(a => a != null && a.AbilityStateMachine == m))
                continue;
            InputActionReference input = m.AbilityHolder != null ? m.AbilityHolder.AbilityActionReference : null;
            // Fill an empty binding first, otherwise add one.
            AbilityAction empty = abilityAction.Find(a => a != null && a.AbilityStateMachine == null);
            if (empty != null)
            {
                empty.AbilityStateMachine = m;
                if (empty.abilityActionReference == null)
                    empty.abilityActionReference = input;
            }
            else
            {
                abilityAction.Add(new AbilityAction(input, m));
            }
            added++;
        }
        if (added > 0 && Application.isPlaying)
            BindKeys();
        return added;
    }

    /// <summary>
    /// The player's root object: the one with the PlayerStatusController (so a player placed inside another scene object
    /// does not pick up other characters' slots), else the top of the hierarchy.
    /// </summary>
    public static Transform PlayerRoot(Component c)
    {
        PlayerStatusController status = c.GetComponentInParent<PlayerStatusController>();
        return status != null ? status.transform : c.transform.root;
    }

    public AvailabilityStateMachine Availability => availabilityStateMachine;
    public PlayerAbilityController AbilityController => abilityController;

    private void Awake()
    {
        if (abilityController == null) abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController == null) abilityController = GetComponentInParent<PlayerAbilityController>();
        if (animationModel == null) animationModel = GetComponentInChildren<PlayerAnimationModel>();
        if (availabilityStateMachine == null) availabilityStateMachine = GetComponent<AvailabilityStateMachine>();
        if (availabilityStateMachine == null) availabilityStateMachine = GetComponentInParent<AvailabilityStateMachine>();
        if (abilityController == null)
            Debug.LogError($"[{name}] AbilitiesStateMachine needs a PlayerAbilityController on the player.", this);

        abilityAction.RemoveAll(a => a == null);
        for (int i = 0; i < abilityAction.Count; i++)
        {
            if (abilityAction[i].AbilityStateMachine == null)
                Debug.LogWarning($"[{name}] ability binding {i} has no AbilityStateMachine.", this);
        }

        playerInput = SharedPlayerInput.Acquire(this);
        context = new AbilitiesContext(playerInput, animationModel, abilityController, availabilityStateMachine, abilityAction)
        {
            InputBufferTime = inputBufferTime,
        };
        BindKeys();
        InitializeStates();
    }

    private void OnEnable()
    {
        SharedPlayerInput.Enable(playerInput);
        // Actions referenced from an Input Actions asset are not enabled by the generated class: enable them here.
        foreach (AbilityAction a in abilityAction)
            EnableReference(a != null ? a.abilityActionReference : null);
    }

    private void OnDisable()
    {
        SharedPlayerInput.Disable(playerInput);
        context?.ClearBuffer();
    }

    private void OnDestroy()
    {
        context?.Dispose();
        SharedPlayerInput.Release(playerInput);
        playerInput = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (context != null)
            context.InputBufferTime = inputBufferTime;
    }
#endif

    private static void EnableReference(InputActionReference reference)
    {
        if (reference != null && reference.action != null)
            reference.action.Enable();
    }

    /// <summary>The action used when binding <paramref name="index"/> has no input: "Ability1", "Ability2"...</summary>
    public static string DefaultActionName(int index) => "Ability" + (index + 1);

    private void InitializeStates()
    {
        States.Add(EAbilitiesState.Available, new AvailableState(context, EAbilitiesState.Available));
        States.Add(EAbilitiesState.Unavailable, new UnavailableState(context, EAbilitiesState.Unavailable));
        CurrentState = States[EAbilitiesState.Available];
    }

    /// <summary>Tells every AbilityStateMachine which key it is and listens to their availability.</summary>
    private void BindKeys()
    {
        for (int i = 0; i < abilityAction.Count; i++)
        {
            AbilityStateMachine m = abilityAction[i] != null ? abilityAction[i].AbilityStateMachine : null;
            if (m != null)
                m.BindKey(this, i);
        }
        context?.WatchKeys();
    }

    public AbilityAction FindAbilityActionByInputAction(InputAction inputAction)
    {
        return abilityAction.FirstOrDefault(action =>
            action != null &&
            action.abilityActionReference != null &&
            action.abilityActionReference.action == inputAction);
    }

    // ------------------------------------------------------------------ runtime key API
    /// <summary>Number of ability keys.</summary>
    public int KeyCount => abilityAction.Count;

    /// <summary>The AbilityStateMachine of key <paramref name="index"/> (null if none).</summary>
    public AbilityStateMachine GetKey(int index) =>
        index >= 0 && index < abilityAction.Count && abilityAction[index] != null ? abilityAction[index].AbilityStateMachine : null;

    /// <summary>Position of <paramref name="machine"/> in the key list (-1 if it is not a key).</summary>
    public int IndexOfKey(AbilityStateMachine machine)
    {
        if (machine == null)
            return -1;
        for (int i = 0; i < abilityAction.Count; i++)
        {
            if (abilityAction[i] != null && abilityAction[i].AbilityStateMachine == machine)
                return i;
        }
        return -1;
    }

    /// <summary>The input that casts key <paramref name="index"/> (its reference, or the AbilityN fallback).</summary>
    public InputAction GetKeyInput(int index)
    {
        if (context != null)
            return context.ResolveInput(index);
        AbilityAction a = index >= 0 && index < abilityAction.Count ? abilityAction[index] : null;
        return a != null && a.abilityActionReference != null ? a.abilityActionReference.action : null;
    }

    /// <summary>
    /// Adds an ability key at runtime (a skill unlocked mid-game). The machine is usually a new child with an
    /// AbilityStateMachine. Returns the key index (the existing one if it is already a key).
    /// </summary>
    public int AddKey(AbilityStateMachine machine, InputActionReference input = null, bool holdToRepeat = false)
    {
        if (machine == null)
            return -1;
        int existing = IndexOfKey(machine);
        if (existing >= 0)
        {
            if (input != null)
                SetKeyInput(existing, input);
            return existing;
        }
        abilityAction.Add(new AbilityAction(input, machine) { holdToRepeat = holdToRepeat });
        if (isActiveAndEnabled)
            EnableReference(input);
        BindKeys();
        return abilityAction.Count - 1;
    }

    /// <summary>Removes the key of <paramref name="machine"/> (the machine and its ability are kept). Returns true if it was a key.</summary>
    public bool RemoveKey(AbilityStateMachine machine)
    {
        int index = IndexOfKey(machine);
        if (index < 0)
            return false;
        abilityAction.RemoveAt(index);
        machine.BindKey(null, -1);
        context?.ClearBuffer();
        BindKeys();
        return true;
    }

    /// <summary>Changes the input of key <paramref name="index"/> at runtime (null = the AbilityN fallback).</summary>
    public void SetKeyInput(int index, InputActionReference input)
    {
        if (index < 0 || index >= abilityAction.Count || abilityAction[index] == null)
            return;
        abilityAction[index].abilityActionReference = input;
        if (isActiveAndEnabled)
            EnableReference(input);
    }
}
