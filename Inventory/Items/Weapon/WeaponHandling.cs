using System;
using UnityEngine;

/// <summary>How many hands a weapon needs. Serialized as numbers: only add new values at the end.</summary>
public enum WeaponGrip
{
    /// <summary>From the weapon category: greatswords, hammers, spears, bows, crossbows and staves are two-handed.</summary>
    Auto,
    /// <summary>Leaves the other hand free for a shield, a second weapon or another off-hand item.</summary>
    OneHanded,
    /// <summary>Needs both hands: while it is held, the off-hand item is stowed and does nothing.</summary>
    TwoHanded,
}

/// <summary>Whether a weapon may be held in the off hand (dual wielding). Serialized as numbers: append only.</summary>
public enum OffHandUse
{
    /// <summary>One-handed swords, daggers, axes, maces, fists, wands, tools and thrown weapons can; the rest cannot.</summary>
    Auto,
    /// <summary>Can be held in the off hand next to a one-handed main weapon.</summary>
    Allowed,
    /// <summary>Main hand only.</summary>
    NotAllowed,
}

/// <summary>Which hand an attack or an item belongs to.</summary>
public enum WeaponHandSide
{
    Main,
    Off,
}

/// <summary>
/// How a weapon is held: one or two hands, whether it can be dual wielded, and what it does from the off hand. Pure
/// data on the <see cref="WeaponSO"/>; the rules are applied by <see cref="HandRules"/>.
/// </summary>
[Serializable]
public class WeaponHandling
{
    [Tooltip("Auto: from the Weapon Category (greatswords, hammers, spears, bows, crossbows and staves are two-handed). " +
             "Two-Handed: while held, the off-hand item (shield, second weapon) is stowed and inactive.")]
    public WeaponGrip grip = WeaponGrip.Auto;

    [Tooltip("Can it be held in the OFF hand (the Off Hand equipment slot) next to a one-handed main weapon? Auto: one-handed " +
             "swords, daggers, axes, maces, fists, wands, tools and thrown weapons can. Two-handed weapons never can.")]
    public OffHandUse offHand = OffHandUse.Auto;

    [Tooltip("When it is in the off hand, the off-hand input performs this attack of THIS weapon (with its own chain, charge, " +
             "on-hit effects, behaviours and combos).")]
    public AttackType offHandAttack = AttackType.Normal;

    [Tooltip("Damage multiplier of its attacks while it is in the off hand (e.g. 0.8 for a weaker off hand).")]
    [Min(0f)] public float offHandDamageMultiplier = 1f;

    [Tooltip("Stamina cost multiplier of its attacks while it is in the off hand.")]
    [Min(0f)] public float offHandStaminaMultiplier = 1f;

    /// <summary>Is the weapon two-handed (its grip, or its category when the grip is Auto)?</summary>
    public bool IsTwoHanded(WeaponCategory category)
    {
        switch (grip)
        {
            case WeaponGrip.OneHanded: return false;
            case WeaponGrip.TwoHanded: return true;
            default: return HandRules.TwoHandedByDefault(category);
        }
    }

    /// <summary>Can the weapon be held in the off hand?</summary>
    public bool CanBeOffHand(WeaponCategory category)
    {
        if (IsTwoHanded(category))
            return false;
        switch (offHand)
        {
            case OffHandUse.Allowed: return true;
            case OffHandUse.NotAllowed: return false;
            default: return HandRules.DualWieldableByDefault(category);
        }
    }

    public string Describe(WeaponCategory category)
    {
        if (IsTwoHanded(category))
            return "Two-handed";
        return CanBeOffHand(category) ? "One-handed · can be dual wielded" : "One-handed · main hand only";
    }
}

/// <summary>What the hands hold after the rules were applied.</summary>
public struct HandState
{
    /// <summary>The weapon of the main hand (null = no weapon).</summary>
    public WeaponSO mainWeapon;
    /// <summary>The off-hand item that is active (null = none, or stowed by a two-handed weapon).</summary>
    public ItemSO activeOffHand;
    /// <summary>The off-hand item is present but stowed (a two-handed weapon is held).</summary>
    public bool offHandSuppressed;
    /// <summary>Why the off-hand item is stowed or refused (empty when everything is valid).</summary>
    public string reason;

    public bool IsTwoHanded => mainWeapon != null && mainWeapon.IsTwoHanded;
    public bool IsDualWielding => mainWeapon != null && activeOffHand is WeaponSO;
    public WeaponSO OffHandWeapon => activeOffHand as WeaponSO;
    public ArmorSO OffHandShield => activeOffHand as ArmorSO;
}

/// <summary>
/// The rules of the hands, in one place: what may be held in the off hand, and which combinations are valid.
/// <list type="bullet">
/// <item>Main hand: the item of the selected hotbar slot.</item>
/// <item>Off hand: the item of the Off Hand equipment slot (the old Shield slot): a shield, or a weapon that can be dual
/// wielded.</item>
/// <item>A two-handed main weapon stows the off-hand item: its effects, defense and blocking are off until the
/// two-handed weapon leaves the hand (nothing is moved or lost).</item>
/// </list>
/// </summary>
public static class HandRules
{
    /// <summary>Categories that need both hands when the weapon's grip is Auto.</summary>
    public static bool TwoHandedByDefault(WeaponCategory c) =>
        c == WeaponCategory.Greatsword || c == WeaponCategory.Hammer || c == WeaponCategory.Spear || c == WeaponCategory.Bow ||
        c == WeaponCategory.Crossbow || c == WeaponCategory.Staff;

    /// <summary>Categories that can be dual wielded when the weapon's off-hand use is Auto.</summary>
    public static bool DualWieldableByDefault(WeaponCategory c) =>
        c == WeaponCategory.Sword || c == WeaponCategory.Dagger || c == WeaponCategory.Axe || c == WeaponCategory.Mace ||
        c == WeaponCategory.Fist || c == WeaponCategory.Wand || c == WeaponCategory.Tool || c == WeaponCategory.Thrown ||
        c == WeaponCategory.Shield;

    /// <summary>Is the item a shield (armor worn in the Shield / Off Hand slot)?</summary>
    public static bool IsShield(ItemSO item) => item is ArmorSO a && a.ArmorSlotType == ArmorSlotType.Shield;

    /// <summary>Can <paramref name="item"/> be held in the off hand? <paramref name="reason"/> explains a refusal.</summary>
    public static bool CanHoldInOffHand(ItemSO item, out string reason)
    {
        reason = "";
        if (item == null)
            return true;
        if (IsShield(item))
            return true;
        if (item is WeaponSO w)
        {
            if (w.IsTwoHanded)
            {
                reason = $"{w.Name} is two-handed: it cannot be held in the off hand.";
                return false;
            }
            if (!w.CanBeOffHand)
            {
                reason = $"{w.Name} cannot be dual wielded (main hand only).";
                return false;
            }
            return true;
        }
        reason = $"{item.Name} cannot be held in the off hand (only shields and one-handed weapons that can be dual wielded).";
        return false;
    }

    /// <summary>Applies the rules to what the hands hold.</summary>
    public static HandState Evaluate(ItemSO mainHand, ItemSO offHand)
    {
        var s = new HandState { mainWeapon = mainHand as WeaponSO };
        if (offHand == null)
            return s;
        if (!CanHoldInOffHand(offHand, out string why))
        {
            s.offHandSuppressed = true;
            s.reason = why;
            return s;
        }
        if (s.mainWeapon != null && s.mainWeapon.IsTwoHanded)
        {
            s.offHandSuppressed = true;
            s.reason = $"{s.mainWeapon.Name} needs both hands: {offHand.Name} is stowed.";
            return s;
        }
        s.activeOffHand = offHand;
        return s;
    }
}
