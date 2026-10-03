/// <summary>
/// Element of a hit. Used by weapons, abilities, armor resistances and elemental reactions
/// (<see cref="ElementalSystem"/>). The values are serialized as numbers: only add new elements at the end.
/// </summary>
public enum ElementType
{
    None,
    Fire,
    Ice,
    Lightning,
    Poison,
    Holy,
    Dark,
    Wind,
    Earth,
    Water
}

/// <summary>
/// How a hit is resisted. Physical damage is reduced by the target's Defense, Magical damage by its Magic Resistance
/// (see <see cref="CombatStats"/>), True damage by neither. Serialized as numbers: only add new types at the end.
/// </summary>
public enum DamageType
{
    Physical,
    Magical,
    True
}

/// <summary>
/// Scales damage before a character receives it: armor, resistances, damage-reduction buffs... Register one with
/// <see cref="CombatEntity.AddDamageTakenModifier"/>. Modifiers run in registration order, after the entity's
/// <see cref="CombatEntity.damageTakenMultiplier"/> and before the health manager's damage factor.
/// </summary>
/// <summary>What caused threat (threat meters, network sync, debugging).</summary>
public enum ThreatKind
{
    Damage,
    Healing,
    Control,
    Taunt,
    Detection,
    Other,
}

/// <summary>
/// Changes the damage a character DEALS (registered on the attacker's <see cref="CombatEntity"/>): elemental damage
/// bonuses and other outgoing modifiers. Runs before the target's interceptors and damage-taken modifiers.
/// </summary>
public interface IDamageDealtModifier
{
    float ModifyDamageDealt(in DamageInfo info, float amount);
}

public interface IDamageTakenModifier
{
    /// <summary>
    /// Returns the damage after this modifier. <paramref name="amount"/> already includes the earlier modifiers;
    /// <paramref name="info"/> tells who hit, with what type and element. Return 0 to ignore the hit completely.
    /// </summary>
    float ModifyDamageTaken(in DamageInfo info, float amount);
}

/// <summary>
/// Defense that depends on where a hit lands: only the armour covering the struck body part protects fully
/// (implemented by <see cref="BodyPartController"/>, read by <see cref="CombatStats"/>).
/// </summary>
public interface ILocationalDefense
{
    /// <summary>
    /// The Defense (or Magic Resistance) points that protect against <paramref name="info"/>; <paramref name="total"/> is
    /// the character's full value (innate + every armour piece + buffs).
    /// </summary>
    float DefenseAgainst(in DamageInfo info, CombatStatType stat, float total);
}

/// <summary>Short words for tooltips: "fire ", "magical ", or "" for plain physical damage.</summary>
public static class DamageWords
{
    public static string Describe(DamageType type, ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return "fire ";
            case ElementType.Ice: return "ice ";
            case ElementType.Lightning: return "lightning ";
            case ElementType.Poison: return "poison ";
            case ElementType.Holy: return "holy ";
            case ElementType.Dark: return "dark ";
            case ElementType.Wind: return "wind ";
            case ElementType.Earth: return "earth ";
            case ElementType.Water: return "water ";
        }
        switch (type)
        {
            case DamageType.Magical: return "magical ";
            case DamageType.True: return "true ";
            default: return "";
        }
    }
}
