using System;
using System.Collections.Generic;
using UnityEngine;

// Shared vocabulary of the injury system: what kind of body part something is, which actions an injury can stop, and
// the interface the rest of the game asks ("may this character sprint?", "which hands can hold something?").
// Every enum here is serialized as numbers in assets: only add new values at the END.

/// <summary>What kind of body part an <see cref="InjuryBodyPart"/> is (rules, filters and default effects use it).</summary>
public enum BodyPartKind
{
    Head,
    Torso,
    Arm,
    Hand,
    Leg,
    Foot,
    /// <summary>Anything else (a tail, wings): no built-in meaning.</summary>
    Other,
    Neck,
    Groin,
}

/// <summary>The side of the body a part is on.</summary>
public enum BodyPartSide
{
    Center,
    Left,
    Right,
}

/// <summary>
/// Actions an injured or missing body part can stop. Body parts list which of them they stop from which impairment
/// (<see cref="BodyActionRule"/>), and injury stages can stop some directly. Serialized as numbers: append only.
/// </summary>
[Flags]
public enum BodyAction
{
    None = 0,
    Sprint = 1 << 0,
    Jump = 1 << 1,
    Dash = 1 << 2,
    Roll = 1 << 3,
    Crouch = 1 << 4,
    /// <summary>Every weapon attack (both hands).</summary>
    WeaponAttacks = 1 << 5,
    /// <summary>Holding a two-handed weapon (needs both hands as well).</summary>
    TwoHandedWeapons = 1 << 6,
    /// <summary>Holding or using anything in the main hand.</summary>
    MainHand = 1 << 7,
    /// <summary>Holding anything in the off hand (shields, a second weapon).</summary>
    OffHand = 1 << 8,
    /// <summary>
    /// The hand on the same side as the body part (an arm or a hand): read as Main Hand or Off Hand from
    /// <see cref="InjurySettings.mainHandSide"/>, so one rule works for both arms.
    /// </summary>
    HandOnThisSide = 1 << 9,
    /// <summary>Raising a shield or a weapon guard.</summary>
    Block = 1 << 10,
    /// <summary>Casting abilities.</summary>
    CastAbilities = 1 << 11,
    /// <summary>Using items (potions, food, medical items).</summary>
    UseItems = 1 << 12,
}

/// <summary>How a hit was resisted, as a filter (several may be chosen).</summary>
[Flags]
public enum InjuryDamageKinds
{
    None = 0,
    Physical = 1 << 0,
    Magical = 1 << 1,
    True = 1 << 2,
    All = Physical | Magical | True,
}

/// <summary>How a hit arrived, as a filter (several may be chosen).</summary>
[Flags]
public enum InjuryDeliveries
{
    None = 0,
    /// <summary>Hits that do not say how they arrived (older weapons, scripts).</summary>
    Unknown = 1 << 0,
    Melee = 1 << 1,
    Projectile = 1 << 2,
    Area = 1 << 3,
    All = Unknown | Melee | Projectile | Area,
}

/// <summary>What happens when a part's condition reaches 0.</summary>
public enum ZeroConditionRule
{
    /// <summary>Nothing more (the part simply works as badly as it can).</summary>
    Nothing,
    /// <summary>The part is lost (only parts that can be lost).</summary>
    LoseLimb,
    /// <summary>The character is stunned for a moment (a blow to the head).</summary>
    Stun,
    /// <summary>The character dies (true damage through the normal death rules: unconsciousness still applies).</summary>
    Kill,
}

/// <summary>Where a treatment came from.</summary>
public enum TreatmentSource
{
    /// <summary>A medical item from the inventory (bandage, splint, suture kit...).</summary>
    Item,
    /// <summary>An ability (a healing spell, a racial regeneration).</summary>
    Ability,
    /// <summary>Healing over time.</summary>
    Natural,
    /// <summary>A healer in a city (placeholder: see <see cref="CityInjuryTreatment"/>).</summary>
    City,
    /// <summary>Your own scripts.</summary>
    Script,
}

/// <summary>Which hands can hold and use things right now (see <see cref="IBodyActionLimits.Hands"/>).</summary>
public struct HandCapability
{
    public bool mainHand;
    public bool offHand;
    public bool twoHanded;
    /// <summary>Why the main hand cannot be used (empty when it can).</summary>
    public string mainReason;
    /// <summary>Why the off hand cannot be used.</summary>
    public string offReason;
    /// <summary>Why two-handed weapons cannot be held.</summary>
    public string twoHandedReason;

    /// <summary>Both hands free (no injury system, or no injuries).</summary>
    public static HandCapability Full => new HandCapability { mainHand = true, offHand = true, twoHanded = true };

    /// <summary>At least one hand can hold something.</summary>
    public bool AnyHand => mainHand || offHand;

    public bool IsFull => mainHand && offHand && twoHanded;
}

/// <summary>
/// What a character's body allows right now. The injury system implements it (<see cref="InjuryController"/>) and
/// registers it on the character's <see cref="CombatEntity.BodyLimits"/>; movement, weapons, hands, items and
/// abilities ask it. Write your own (a curse that stops jumping) and assign it the same way.
/// </summary>
public interface IBodyActionLimits
{
    /// <summary>May the character do <paramref name="action"/>? (several flags = all of them).</summary>
    bool Allows(BodyAction action);

    /// <summary>Why <paramref name="action"/> is not allowed ("Left leg broken"); empty when it is.</summary>
    string ReasonFor(BodyAction action);

    /// <summary>Which hands can hold things.</summary>
    HandCapability Hands { get; }
}

/// <summary>How the character gets around with the legs it has (see <see cref="BodyLocomotion"/>).</summary>
public enum LocomotionMode
{
    /// <summary>Walks and runs normally.</summary>
    Normal,
    /// <summary>A hurt leg or foot (or a missing foot): walks with a limp.</summary>
    Limping,
    /// <summary>One leg cannot carry weight (lost or useless): hops on the other, in bursts.</summary>
    Hopping,
    /// <summary>No leg can carry weight: drags itself along with the arms.</summary>
    Crawling,
}

/// <summary>
/// How the character's legs let it move right now: the mode (limp, hop, crawl), how fast compared to a healthy body,
/// and what the animator and the colliders need (limp amount and side, hop rhythm, body height). Made by the
/// <see cref="InjuryController"/> from the legs' state; movement reads it through <see cref="CombatEntity.Locomotion"/>.
/// </summary>
public struct BodyLocomotion
{
    public LocomotionMode mode;
    /// <summary>How badly it limps, 0-1 (Limping; 1 also while hopping or crawling).</summary>
    public float limp;
    /// <summary>The bad leg: -1 = left, +1 = right, 0 = none (or both).</summary>
    public float limpSide;
    /// <summary>Legs that cannot carry weight (lost, or too hurt).</summary>
    public int unusableLegs;
    /// <summary>Legs that are missing.</summary>
    public int missingLegs;
    /// <summary>Feet that are missing (the leg is still there: it walks on the stump).</summary>
    public int missingFeet;
    /// <summary>Arms that are missing (from the upper arm or the forearm), for one-armed animations.</summary>
    public int missingArms;
    /// <summary>Walking hurts (a bad limp, a stump): the strain consequence runs while moving.</summary>
    public bool painful;
    /// <summary>Speed on top of the character's stats (1 = unchanged). Hopping averages to it over a hop.</summary>
    public float speedMultiplier;
    /// <summary>Seconds of one hop (Hopping).</summary>
    public float hopPeriod;
    /// <summary>Share of a hop spent moving (the rest is the landing).</summary>
    public float hopMoveShare;
    /// <summary>Height of the body's collider compared to standing (1 = standing; ~0.5 with no legs).</summary>
    public float heightFactor;
    /// <summary>Why it moves like this ("Left leg is missing").</summary>
    public string reason;

    /// <summary>A healthy body.</summary>
    public static BodyLocomotion Normal => new BodyLocomotion { mode = LocomotionMode.Normal, speedMultiplier = 1f, heightFactor = 1f, hopPeriod = 0.6f, hopMoveShare = 1f, reason = "" };

    public bool IsNormal => mode == LocomotionMode.Normal;

    /// <summary>
    /// The speed multiplier at <paramref name="time"/> (the movement clock): steady, except hopping, which moves in a
    /// burst at the start of each hop and stands still on landing (the average is <see cref="speedMultiplier"/>).
    /// </summary>
    public float SpeedAt(float time)
    {
        float s = Mathf.Max(0f, speedMultiplier);
        if (mode != LocomotionMode.Hopping || hopPeriod <= 0.05f || hopMoveShare >= 0.999f || hopMoveShare <= 0.05f)
            return s;
        float phase = HopPhase(time);
        if (phase >= hopMoveShare)
            return 0f;
        // A half sine over the moving share: averages to s over the whole hop.
        return s / hopMoveShare * Mathf.Sin(phase / hopMoveShare * Mathf.PI) * (Mathf.PI * 0.5f);
    }

    /// <summary>Where in the current hop the body is, 0-1 (for the animator's Hop Cycle).</summary>
    public float HopPhase(float time) => hopPeriod > 0.05f ? Mathf.Repeat(time, hopPeriod) / hopPeriod : 0f;

    public string Describe()
    {
        switch (mode)
        {
            case LocomotionMode.Limping: return $"Limping ({limp * 100f:0}%)";
            case LocomotionMode.Hopping: return "Hopping on one leg";
            case LocomotionMode.Crawling: return "Crawling";
            default: return "Walking normally";
        }
    }
}

/// <summary>
/// Optional companion of <see cref="IBodyActionLimits"/>: how the body moves (limp, hop, crawl). The injury system
/// implements it; <see cref="CombatEntity.Locomotion"/> reads it.
/// </summary>
public interface IBodyLocomotion
{
    BodyLocomotion Locomotion { get; }
}

/// <summary>Names and descriptions of the <see cref="BodyAction"/> flags (inspectors, tooltips).</summary>
public static class BodyActions
{
    /// <summary>The single flags, in order.</summary>
    public static readonly BodyAction[] All =
    {
        BodyAction.Sprint, BodyAction.Jump, BodyAction.Dash, BodyAction.Roll, BodyAction.Crouch, BodyAction.WeaponAttacks,
        BodyAction.TwoHandedWeapons, BodyAction.MainHand, BodyAction.OffHand, BodyAction.HandOnThisSide, BodyAction.Block,
        BodyAction.CastAbilities, BodyAction.UseItems,
    };

    /// <summary>"Cannot sprint", "Cannot hold two-handed weapons"...</summary>
    public static string Describe(BodyAction a)
    {
        switch (a)
        {
            case BodyAction.Sprint: return "Cannot sprint";
            case BodyAction.Jump: return "Cannot jump";
            case BodyAction.Dash: return "Cannot dash";
            case BodyAction.Roll: return "Cannot roll";
            case BodyAction.Crouch: return "Cannot crouch";
            case BodyAction.WeaponAttacks: return "Cannot attack with weapons";
            case BodyAction.TwoHandedWeapons: return "Cannot hold two-handed weapons";
            case BodyAction.MainHand: return "Main hand unusable";
            case BodyAction.OffHand: return "Off hand unusable";
            case BodyAction.HandOnThisSide: return "This hand is unusable";
            case BodyAction.Block: return "Cannot block";
            case BodyAction.CastAbilities: return "Cannot cast abilities";
            case BodyAction.UseItems: return "Cannot use items";
            default: return a.ToString();
        }
    }

    /// <summary>Every flag set in <paramref name="actions"/>, described ("Cannot sprint, Cannot jump").</summary>
    public static string DescribeAll(BodyAction actions)
    {
        if (actions == BodyAction.None)
            return "nothing";
        var parts = new List<string>();
        foreach (BodyAction a in All)
            if ((actions & a) != 0)
                parts.Add(Describe(a));
        return string.Join(", ", parts);
    }

    /// <summary>
    /// <see cref="BodyAction.HandOnThisSide"/> read for a part on <paramref name="side"/>: Main Hand on the main-hand
    /// side, Off Hand on the other (a centre part keeps nothing of it).
    /// </summary>
    public static BodyAction ResolveSide(BodyAction actions, BodyPartSide side, BodyPartSide mainHandSide)
    {
        if ((actions & BodyAction.HandOnThisSide) == 0)
            return actions;
        actions &= ~BodyAction.HandOnThisSide;
        if (side == BodyPartSide.Center)
            return actions;
        return actions | (side == mainHandSide ? BodyAction.MainHand : BodyAction.OffHand);
    }

    /// <summary>The kinds of a hit as filter flags.</summary>
    public static InjuryDamageKinds KindOf(DamageType type)
    {
        switch (type)
        {
            case DamageType.Magical: return InjuryDamageKinds.Magical;
            case DamageType.True: return InjuryDamageKinds.True;
            default: return InjuryDamageKinds.Physical;
        }
    }

    /// <summary>The delivery of a hit as filter flags (periodic damage never matches).</summary>
    public static InjuryDeliveries DeliveryOf(DamageDelivery d)
    {
        switch (d)
        {
            case DamageDelivery.Melee: return InjuryDeliveries.Melee;
            case DamageDelivery.Projectile: return InjuryDeliveries.Projectile;
            case DamageDelivery.Area: return InjuryDeliveries.Area;
            case DamageDelivery.Periodic: return InjuryDeliveries.None;
            default: return InjuryDeliveries.Unknown;
        }
    }
}
