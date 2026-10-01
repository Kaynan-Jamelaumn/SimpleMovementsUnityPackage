using System;

/// <summary>What kind of ability this is. Used by the AI to pick abilities and by UI/filters. Several can be combined.</summary>
[Flags]
public enum AbilityTag
{
    None = 0,
    Melee = 1 << 0,
    Ranged = 1 << 1,
    Area = 1 << 2,
    Projectile = 1 << 3,
    Control = 1 << 4,
    Mobility = 1 << 5,
    Summon = 1 << 6,
    Defensive = 1 << 7,
    Support = 1 << 8,
    Buff = 1 << 9,
    Debuff = 1 << 10,
    GapCloser = 1 << 11,
    Escape = 1 << 12,
    Ultimate = 1 << 13,
}

/// <summary>How an ability chooses where it goes.</summary>
public enum AbilityTargetingMode
{
    /// <summary>Centred on the caster (self buffs, novas, stomps).</summary>
    Self,
    /// <summary>Needs a target character (the mob's current target, or what the player aims at).</summary>
    Unit,
    /// <summary>A position on the ground within range (meteors, ground surges, cages).</summary>
    Point,
    /// <summary>A direction from the caster (projectiles, cones, beams, charges).</summary>
    Direction,
}

/// <summary>When the aim of an ability stops following its target.</summary>
public enum AbilityAimLock
{
    /// <summary>Aim is fixed when the cast starts (the telegraph stays where it was placed - dodgeable).</summary>
    LockAtCastStart,
    /// <summary>Aim follows the target while casting and is fixed on release (limited by Aim Turn Rate).</summary>
    TrackUntilRelease,
    /// <summary>The ability stays attached to the caster (auras, spins, frontal cleaves that turn with the caster).</summary>
    FollowCaster,
}

/// <summary>Who an ability affects, relative to the caster.</summary>
[Flags]
public enum TargetFilter
{
    /// <summary>Nobody.</summary>
    None = 0,
    /// <summary>The caster / attacker itself.</summary>
    Self = 1 << 0,
    /// <summary>Characters of the same team (CombatEntity.Team).</summary>
    Allies = 1 << 1,
    /// <summary>Hostile characters. For players: every character of another team.</summary>
    Enemies = 1 << 2,
    /// <summary>Neither ally nor enemy (a mob another mob is not hostile to).</summary>
    Neutral = 1 << 3,
    /// <summary>Everyone except the caster.</summary>
    AllButSelf = Allies | Enemies | Neutral,
    /// <summary>Everyone, the caster included (the same as Unity's "Everything").</summary>
    All = Self | Allies | Enemies | Neutral,
    /// <summary>
    /// Members of the caster's party only (a party heal that skips other allies). Party members are also Allies, so
    /// with Allies on this bit changes nothing.
    /// </summary>
    Party = 1 << 4,
}

/// <summary>How two combatants relate (see <see cref="CombatRelations"/>).</summary>
public enum CombatRelation
{
    Self,
    Ally,
    Enemy,
    Neutral,
    /// <summary>In the same party (a closer kind of ally: filters with Allies include party members).</summary>
    Party,
}

/// <summary>
/// Phases of an ability slot. The order and names match the player's AbilityStateMachine.EAbilityState so the two
/// map one to one.
/// </summary>
public enum AbilityPhase
{
    Ready,
    Casting,
    Launching,
    Active,
    InCooldown,
}

/// <summary>Phases during which an ability blocks the owner's other abilities (player availability rules).</summary>
[Flags]
public enum AbilityPhaseMask
{
    None = 0,
    Casting = 1 << 0,
    Launching = 1 << 1,
    Active = 1 << 2,
}

/// <summary>Why a cast stopped before finishing.</summary>
public enum CastInterruptReason
{
    Manual,
    Damage,
    Stun,
    Silence,
    Death,
    TargetLost,
    Replaced,
    Disabled,
}

/// <summary>Why a cast could not start.</summary>
public enum CastFailReason
{
    None,
    NoAbility,
    Disabled,
    OnCooldown,
    Busy,
    Stunned,
    Silenced,
    Dead,
    NotEnoughResource,
    NoTarget,
    OutOfRange,
    TooClose,
    NoLineOfSight,
}

/// <summary>What the caster may do while casting.</summary>
public enum CasterMovementRule
{
    /// <summary>The caster stands still while casting (most telegraphed attacks).</summary>
    Stop,
    /// <summary>The caster moves slower while casting (see Cast Move Speed Multiplier).</summary>
    Slowed,
    /// <summary>The caster moves freely (instant cast, mobile casters).</summary>
    Free,
}

/// <summary>Where a cast action is placed.</summary>
public enum ActionAnchor
{
    /// <summary>At the caster, facing the aim direction.</summary>
    Caster,
    /// <summary>At the aimed point (ground target / target position when the cast was released).</summary>
    AimPoint,
    /// <summary>On the target character's current position (falls back to the aim point).</summary>
    TargetUnit,
}

/// <summary>Crowd-control kinds applied by effects.</summary>
public enum ControlType
{
    Stun,
    Silence,
    Root,
    Slow,
    Taunt,
}

/// <summary>How an absorbed copy compares to the original.</summary>
public enum AbsorbTier
{
    Weaker,
    Same,
    Stronger,
    Altered,
}

/// <summary>How several projectiles of one launch are arranged.</summary>
public enum ProjectilePattern
{
    /// <summary>Fanned out evenly across Spread Angle.</summary>
    Spread,
    /// <summary>All around the caster (360 degrees).</summary>
    Ring,
    /// <summary>Side by side, all flying the same direction (Spacing apart).</summary>
    Parallel,
    /// <summary>Random directions inside Spread Angle.</summary>
    Random,
    /// <summary>Falling from above onto random points around the aim point (meteor / arrow rain).</summary>
    Rain,
}

/// <summary>Shape of a ground surge.</summary>
public enum SurgePattern
{
    /// <summary>Eruptions travelling forward in a straight line.</summary>
    Line,
    /// <summary>A shockwave ring expanding from the origin.</summary>
    Ring,
    /// <summary>Several lines spread evenly around the origin (Lines).</summary>
    Star,
    /// <summary>A line that turns toward the target as it travels.</summary>
    Seek,
}

/// <summary>Layout of barrier segments.</summary>
public enum BarrierLayout
{
    /// <summary>A straight wall across the aim direction, centred on the anchor.</summary>
    Wall,
    /// <summary>A curved wall (part of a circle) facing the caster.</summary>
    Arc,
    /// <summary>A closed ring around the anchor (traps whoever is inside).</summary>
    Cage,
}

/// <summary>How a movement action moves the caster.</summary>
public enum MovementActionMode
{
    /// <summary>Dash to the target, stopping Stop Distance before it.</summary>
    DashToTarget,
    /// <summary>Dash Distance along the aim direction.</summary>
    DashAlongAim,
    /// <summary>Jump in an arc to the aimed point.</summary>
    Leap,
    /// <summary>Teleport to the aimed point (or Distance along the aim).</summary>
    Blink,
    /// <summary>Jump back, away from the target.</summary>
    Retreat,
    /// <summary>Dash through the aim direction hitting everything on the way.</summary>
    Charge,
}

/// <summary>Direction of a knockback.</summary>
public enum DisplacementDirection
{
    /// <summary>Away from where the hit came from (explosion centre / caster).</summary>
    AwayFromOrigin,
    /// <summary>Along the ability's aim direction.</summary>
    AlongAim,
    /// <summary>Straight up.</summary>
    Up,
}

/// <summary>Where a pull drags the target.</summary>
public enum PullAnchor
{
    /// <summary>Toward the caster.</summary>
    Caster,
    /// <summary>Toward the centre of the hit (area centre / impact point).</summary>
    HitOrigin,
}
