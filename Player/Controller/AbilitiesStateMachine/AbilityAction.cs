using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One ability key of the player (a line of the <see cref="AbilitiesStateMachine"/> list): the input that casts it
/// and the <see cref="global::AbilityStateMachine"/> that stores what it casts.
/// </summary>
[System.Serializable]
public class AbilityAction
{
    [SerializeField] private AbilityStateMachine abilityStateMachine;
    public InputActionReference abilityActionReference;

    [Tooltip("Keep casting while the key is held: the ability starts again every time it is ready. Off = one cast per press.")]
    public bool holdToRepeat;

    public AbilityStateMachine AbilityStateMachine { get => abilityStateMachine; set => abilityStateMachine = value; }

    public AbilityAction(InputActionReference abilityActionReference, AbilityStateMachine a)
    {
        this.abilityStateMachine = a;
        this.abilityActionReference = abilityActionReference;
    }
}
