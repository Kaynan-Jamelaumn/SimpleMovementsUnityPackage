using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Data shared by the player's abilities availability states (<see cref="AbilitiesStateMachine"/>): the ability keys,
/// whether any of them blocks the others (one subscription for both states), the input of each key and the press
/// waiting in the input buffer.
/// </summary>
public class AbilitiesContext
{
    private readonly PlayerInput playerInput;
    private readonly PlayerAnimationModel animationModel;
    private readonly PlayerAbilityController abilityController;
    private readonly AvailabilityStateMachine availabilityStateMachine;
    private readonly List<AbilityAction> abilityAction;

    public AbilitiesContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController, AvailabilityStateMachine availabilityStateMachine, List<AbilityAction> abilityAction)
    {
        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.availabilityStateMachine = availabilityStateMachine;
        this.abilityAction = abilityAction;
    }

    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public AvailabilityStateMachine AvailabilityState => availabilityStateMachine;
    public List<AbilityAction> AbilityAction => abilityAction;

    // ------------------------------------------------------------------ availability of the keys
    private readonly List<AbilityStateMachine> watched = new List<AbilityStateMachine>();

    /// <summary>False while one of the keys is in a phase that blocks the others (its ability's Blocks Other Abilities).</summary>
    public bool KeysAvailable { get; private set; } = true;

    /// <summary>(Re)subscribes to every key's availability. Called when the key list changes.</summary>
    public void WatchKeys()
    {
        for (int i = 0; i < watched.Count; i++)
        {
            if (watched[i] != null)
                watched[i].Context.AvailabilityChanged -= OnKeyAvailabilityChanged;
        }
        watched.Clear();
        if (abilityAction != null)
        {
            for (int i = 0; i < abilityAction.Count; i++)
            {
                AbilityStateMachine m = abilityAction[i] != null ? abilityAction[i].AbilityStateMachine : null;
                if (m == null || watched.Contains(m))
                    continue;
                m.Context.AvailabilityChanged += OnKeyAvailabilityChanged;
                watched.Add(m);
            }
        }
        RecalculateAvailability();
    }

    private void OnKeyAvailabilityChanged(bool value) => RecalculateAvailability();

    public void RecalculateAvailability()
    {
        bool value = true;
        for (int i = 0; i < watched.Count; i++)
        {
            if (watched[i] != null && !watched[i].Available())
            {
                value = false;
                break;
            }
        }
        KeysAvailable = value;
    }

    /// <summary>Stops listening to the keys (the machine is being destroyed).</summary>
    public void Dispose()
    {
        for (int i = 0; i < watched.Count; i++)
        {
            if (watched[i] != null)
                watched[i].Context.AvailabilityChanged -= OnKeyAvailabilityChanged;
        }
        watched.Clear();
    }

    // ------------------------------------------------------------------ input of each key
    private InputAction[] defaultActions;

    /// <summary>
    /// The input of key <paramref name="index"/>: its Input Action Reference, or - when empty - the action named
    /// "Ability1".."AbilityN" (by position) in the player's input actions.
    /// </summary>
    public InputAction ResolveInput(int index)
    {
        if (abilityAction == null || index < 0 || index >= abilityAction.Count)
            return null;
        AbilityAction a = abilityAction[index];
        if (a != null && a.abilityActionReference != null && a.abilityActionReference.action != null)
            return a.abilityActionReference.action;
        if (playerInput == null || playerInput.asset == null)
            return null;
        if (defaultActions == null || defaultActions.Length <= index)
            System.Array.Resize(ref defaultActions, Mathf.Max(index + 1, 8));
        if (defaultActions[index] == null)
            defaultActions[index] = playerInput.asset.FindAction(AbilitiesStateMachine.DefaultActionName(index));
        return defaultActions[index];
    }

    // ------------------------------------------------------------------ input buffer
    /// <summary>Seconds a press made slightly too early is remembered (0 = off).</summary>
    public float InputBufferTime { get; set; }

    /// <summary>Key waiting in the input buffer (-1 = none).</summary>
    public int BufferedKey { get; private set; } = -1;

    /// <summary>Time.time until which <see cref="BufferedKey"/> is still cast.</summary>
    public float BufferedUntil { get; private set; }

    /// <summary>Remembers a press of key <paramref name="index"/> (the latest press replaces an older one).</summary>
    public void BufferPress(int index)
    {
        if (InputBufferTime <= 0f)
            return;
        BufferedKey = index;
        BufferedUntil = Time.time + InputBufferTime;
    }

    public void ClearBuffer() => BufferedKey = -1;
}
