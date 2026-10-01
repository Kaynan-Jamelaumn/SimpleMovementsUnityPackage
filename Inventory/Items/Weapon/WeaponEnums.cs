// Enums shared by weapons, attacks, equipment and the combat stats. They are serialized as numbers in assets:
// only ever add new values at the END of an enum.

/// <summary>What a tool weapon can harvest (collectables require a matching tool).</summary>
public enum ToolType
{
    None,
    Scythe,
    Axe,
    PickAxe,
    Fishingrod,
}

/// <summary>
/// Which attack input was used. Every weapon has one attack per input (Normal is required, the others optional);
/// combos, chains and combo trees are written in terms of these inputs.
/// </summary>
public enum AttackType
{
    /// <summary>Primary attack (the Use Item input by default).</summary>
    Normal,
    /// <summary>Quick attack input.</summary>
    Light,
    /// <summary>Strong attack input.</summary>
    Heavy,
    /// <summary>Special / skill input.</summary>
    Special,
    /// <summary>Secondary attack (right click, alternate fire, shield bash...).</summary>
    Alternate,
}

/// <summary>Weapon family. Used by equipment bonuses ("+15% damage with swords") and conditions.</summary>
public enum WeaponCategory
{
    None,
    Sword,
    Greatsword,
    Axe,
    Mace,
    Hammer,
    Spear,
    Dagger,
    Fist,
    Bow,
    Crossbow,
    Staff,
    Wand,
    Shield,
    Tool,
    Thrown,
    Other,
}

/// <summary>Attribute (from <see cref="CombatStats"/>) that increases a weapon's damage.</summary>
public enum WeaponScaling
{
    None,
    Strength,
    Agility,
    Intelligence,
}

/// <summary>How an attack deals its damage.</summary>
public enum WeaponDamageMode
{
    /// <summary>
    /// The weapon's damage is dealt, unless the attack already deals health damage through its legacy Effects list
    /// (older weapons keep working exactly as configured).
    /// </summary>
    Auto,
    /// <summary>The weapon's damage (min-max x the attack's Damage Multiplier) is always dealt.</summary>
    WeaponDamage,
    /// <summary>Only the attack's effects deal damage (the weapon's min/max damage is ignored).</summary>
    EffectsOnly,
}

/// <summary>How an attack finds what it hits.</summary>
public enum HitDetectionMode
{
    /// <summary>The weapon's Attack Cast when one is configured (older weapons), otherwise the attack's Hit Shape.</summary>
    Auto,
    /// <summary>The attack's Hit Shape, placed in front of the attacker (recommended).</summary>
    HitShape,
    /// <summary>The weapon's Attack Cast around the hand (physics overlap).</summary>
    WeaponCast,
    /// <summary>No direct hit: the attack only runs its behaviours (projectiles, abilities, buffs).</summary>
    None,
    /// <summary>
    /// The weapon itself: a capsule along the held weapon model (the weapon's Blade settings) that follows the hand
    /// through the animation and sweeps the space between frames, so fast swings do not skip targets.
    /// </summary>
    WeaponBlade,
}
