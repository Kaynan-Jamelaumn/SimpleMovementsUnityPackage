using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The ability of one player ability slot (one per AbilityStateMachine). Set <see cref="ability"/> (new system); the
/// old Ability Effect field still works and is converted automatically when Ability is empty.
/// </summary>
[System.Serializable]
public class PlayerAbilityHolder : AbilityHolder
{
    [Tooltip("Optional: the input of this slot, for reference. The input that actually casts is the one in the AbilitiesStateMachine's binding list (the inspector can copy it there).")]
    [SerializeField] private InputActionReference abilityActionReference;
    public InputActionReference AbilityActionReference { get => abilityActionReference; }

    [Tooltip("The ability in this slot. Empty = the legacy Ability Effect below is converted automatically. Absorbed abilities are written here at runtime.")]
    public AbilityDefinition ability;

    [Tooltip("Changes applied on top of the ability (an absorbed weaker copy, upgrades).")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();

    /// <summary>The ability to use: <see cref="ability"/>, or the converted legacy ability effect.</summary>
    public AbilityDefinition ResolveAbility()
    {
        if (ability != null)
            return ability;
        return abilityEffect != null ? LegacyAbilityConverter.Convert(this, false) : null;
    }
}
