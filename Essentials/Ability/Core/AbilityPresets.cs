using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ready-made ability setups: a starting point for designers (Assets > Create > Scriptable Objects > Ability > Ability From Preset, or Apply Preset in the ability inspector) and the mob's
/// automatic basic attack. Each method returns a new, unsaved <see cref="AbilityDefinition"/>.
/// </summary>
public static class AbilityPresets
{
    public struct Preset
    {
        public string name;
        public string description;
        public Func<AbilityDefinition> create;
    }

    /// <summary>Every preset, for editor menus.</summary>
    public static readonly Preset[] All =
    {
        new Preset { name = "Melee Cleave (Cone)", description = "Wind-up and a frontal cone swing.", create = () => MeleeCleave(12f, 3f, 110f) },
        new Preset { name = "Bite (Direct)", description = "Short bite on the target.", create = () => BasicMelee(8f, 2f, 1.2f) },
        new Preset { name = "Stun Slam (Circle)", description = "Jumps up and slams the ground around itself, stunning.", create = StunSlam },
        new Preset { name = "Firebolt (Projectile)", description = "A single fast projectile.", create = Firebolt },
        new Preset { name = "Triple Shot (Spread)", description = "Three projectiles in a fan. Absorbed copies can fire just one.", create = TripleShot },
        new Preset { name = "Bomb (Lobbed)", description = "A lobbed explosive landing on the target.", create = LobbedBomb },
        new Preset { name = "Meteor Rain", description = "Projectiles falling around the target area.", create = MeteorRain },
        new Preset { name = "Ground Surge (Line)", description = "A line of spikes travelling toward the target.", create = GroundSurgeLine },
        new Preset { name = "Shockwave (Ring)", description = "An expanding ring: jump it or run out of it.", create = Shockwave },
        new Preset { name = "Fissure Star", description = "Five fissures erupting around the caster.", create = FissureStar },
        new Preset { name = "Hook (Pull)", description = "A projectile that drags the target to the caster.", create = HookPull },
        new Preset { name = "Vortex (Pull to Centre)", description = "Pulls everyone near the target into one spot.", create = Vortex },
        new Preset { name = "Stone Wall", description = "A wall between the caster and the target.", create = StoneWall },
        new Preset { name = "Cage", description = "A ring of blocks trapping the target.", create = Cage },
        new Preset { name = "Poison Pool (Zone)", description = "A lingering area that damages and slows.", create = PoisonPool },
        new Preset { name = "Fire Breath (Beam)", description = "A channelled cone-like beam that follows the target slowly.", create = FireBreath },
        new Preset { name = "Leap Slam", description = "Leaps onto the target and slams on landing.", create = LeapSlam },
        new Preset { name = "Charge", description = "Rushes forward, knocking back everyone on the way.", create = Charge },
        new Preset { name = "Disengage (Retreat)", description = "Jumps away from the target (escape).", create = Disengage },
        new Preset { name = "Heal Self", description = "Heals the caster over time.", create = HealSelf },
        new Preset { name = "Summon Minions", description = "Summons creatures (assign the prefab).", create = () => Summon(null, 2) },
        new Preset { name = "Guardian Barrier (Self)", description = "A ring of stone around the caster plus a moment of invulnerability.", create = GuardianBarrier },
        new Preset { name = "Blink", description = "Teleports a short distance along the aim.", create = Blink },
        new Preset { name = "War Cry (Silence Nova)", description = "Silences and slows enemies around the caster.", create = WarCry },
    };

    private static AbilityDefinition New(string name, AbilityTag tags)
    {
        var d = ScriptableObject.CreateInstance<AbilityDefinition>();
        d.name = name;
        d.SetDisplayName(name);
        d.tags = tags;
        return d;
    }

    /// <summary>Marks a preset as a runtime-only definition (not an asset).</summary>
    public static AbilityDefinition AsRuntime(AbilityDefinition d, string id)
    {
        d.InitializeRuntime(id, d.DisplayName);
        return d;
    }

    // ------------------------------------------------------------------ melee
    /// <summary>A mob's plain melee attack: short wind-up and a small frontal cone.</summary>
    public static AbilityDefinition BasicMelee(float damage, float reach, float cooldown, float windUp = 0.35f)
    {
        AbilityDefinition d = New("Basic Attack", AbilityTag.Melee);
        d.castTime = windUp;
        d.activeTime = 0.25f;
        d.cooldown = cooldown;
        d.movementWhileCasting = CasterMovementRule.Stop;
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = reach;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 240f;
        d.targeting.leadTarget = 0.2f;
        d.actions.Add(new AreaHitAction
        {
            shape = new HitShape { type = HitShapeType.Cone, radius = reach + 0.6f, angle = 80f, height = 3f, baseOffset = -1f },
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = damage, variance = 0.15f }) { maxTargets = 1 },
        });
        d.presentation.castTrigger = "Attack";
        d.presentation.showTelegraph = false;
        d.presentation.noiseRadius = 8f;
        d.ai.priority = 0.6f;
        d.absorption.canBeAbsorbed = false;
        return d;
    }

    public static AbilityDefinition MeleeCleave(float damage, float radius, float angle)
    {
        AbilityDefinition d = New("Cleave", AbilityTag.Melee | AbilityTag.Area);
        d.castTime = 0.6f;
        d.activeTime = 0.35f;
        d.cooldown = 4f;
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = radius;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 120f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.ConeShape(radius, angle),
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = damage }, new KnockbackEffect { distance = 1.5f, arcHeight = 0f, disableTarget = false }),
        });
        d.presentation.castTrigger = "Attack";
        return d;
    }

    public static AbilityDefinition StunSlam()
    {
        AbilityDefinition d = New("Stun Slam", AbilityTag.Melee | AbilityTag.Area | AbilityTag.Control);
        d.castTime = 1f;
        d.activeTime = 0.6f;
        d.cooldown = 9f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.targeting.range = 4f;
        d.interruptedByDamage = true;
        d.interruptDamageThreshold = 0.08f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.CircleShape(4f),
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 15f }, new StunEffect { duration = 1.5f }) { edgeMultiplier = 0.6f },
        });
        d.ai.priority = 1.4f;
        d.ai.minTargets = 1;
        return d;
    }

    // ------------------------------------------------------------------ projectiles
    public static AbilityDefinition Firebolt()
    {
        AbilityDefinition d = New("Firebolt", AbilityTag.Ranged | AbilityTag.Projectile);
        d.castTime = 0.5f;
        d.cooldown = 2.5f;
        d.manaCost = 8f;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 22f;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 200f;
        d.targeting.leadTarget = 0.7f;
        d.movementWhileCasting = CasterMovementRule.Slowed;
        d.actions.Add(new ProjectileAction
        {
            speed = 24f,
            maxDistance = 25f,
            radius = 0.3f,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 14f }, new DamageOverTimeEffect { damagePerTick = 2f, duration = 3f, chance = 0.35f }),
        });
        d.ai.requireTargetInArea = false;
        return d;
    }

    public static AbilityDefinition TripleShot()
    {
        AbilityDefinition d = Firebolt();
        d.name = "Triple Shot";
        d.SetDisplayName("Triple Shot");
        d.cooldown = 4f;
        d.targeting.leadTarget = 0.4f;
        var p = d.FindAction<ProjectileAction>();
        p.count = 3;
        p.pattern = ProjectilePattern.Spread;
        p.spreadAngle = 24f;
        p.hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 8f });
        d.absorption.absorbedBaseModifiers = new AbilityModifierSet { label = "Single", projectileCountOverride = 1, damageMultiplier = 1.5f };
        return d;
    }

    public static AbilityDefinition LobbedBomb()
    {
        AbilityDefinition d = New("Bomb", AbilityTag.Ranged | AbilityTag.Area | AbilityTag.Projectile);
        d.castTime = 0.8f;
        d.cooldown = 6f;
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 16f;
        d.targeting.minRange = 3f;
        d.targeting.leadTarget = 0.6f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new ProjectileAction
        {
            lob = true,
            arcHeight = 4f,
            speed = 12f,
            maxDistance = 20f,
            radius = 0.35f,
            explosionRadius = 3f,
            explodeAtEnd = true,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 18f }, new KnockbackEffect { distance = 3f }) { edgeMultiplier = 0.5f },
        });
        return d;
    }

    public static AbilityDefinition MeteorRain()
    {
        AbilityDefinition d = New("Meteor Rain", AbilityTag.Ranged | AbilityTag.Area | AbilityTag.Ultimate);
        d.castTime = 1.4f;
        d.cooldown = 16f;
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 20f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new ProjectileAction
        {
            pattern = ProjectilePattern.Rain,
            count = 4,
            volleys = 3,
            volleyInterval = 0.35f,
            reaimEachVolley = false,
            rainHeight = 14f,
            rainRadius = 4f,
            speed = 20f,
            radius = 0.5f,
            explosionRadius = 2f,
            explodeAtEnd = true,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 10f }),
        });
        d.ai.priority = 1.5f;
        d.ai.extraAICooldown = 6f;
        return d;
    }

    // ------------------------------------------------------------------ ground
    public static AbilityDefinition GroundSurgeLine()
    {
        AbilityDefinition d = New("Ground Surge", AbilityTag.Ranged | AbilityTag.Area);
        d.castTime = 0.9f;
        d.cooldown = 7f;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 14f;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 90f;
        d.actions.Add(new GroundSurgeAction { pattern = SurgePattern.Line, distance = 14f, speed = 14f, step = 1.4f, eruptionRadius = 1.1f });
        return d;
    }

    public static AbilityDefinition Shockwave()
    {
        AbilityDefinition d = New("Shockwave", AbilityTag.Area | AbilityTag.Control);
        d.castTime = 1.1f;
        d.cooldown = 10f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.targeting.range = 10f;
        d.actions.Add(new GroundSurgeAction
        {
            pattern = SurgePattern.Ring,
            distance = 10f,
            speed = 9f,
            step = 1f,
            eruptionRadius = 1.2f,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 12f }, new KnockbackEffect { distance = 3f, arcHeight = 1f }),
        });
        d.ai.minTargets = 1;
        return d;
    }

    public static AbilityDefinition FissureStar()
    {
        AbilityDefinition d = New("Fissure Star", AbilityTag.Area);
        d.castTime = 1f;
        d.cooldown = 9f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.targeting.range = 9f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new GroundSurgeAction { pattern = SurgePattern.Star, lines = 5, distance = 9f, speed = 12f, step = 1.3f, eruptionRadius = 1f });
        return d;
    }

    public static AbilityDefinition PoisonPool()
    {
        AbilityDefinition d = New("Poison Pool", AbilityTag.Ranged | AbilityTag.Area | AbilityTag.Debuff);
        d.castTime = 0.7f;
        d.cooldown = 10f;
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 14f;
        d.targeting.leadTarget = 0.5f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new ZoneAction
        {
            anchor = ActionAnchor.AimPoint,
            shape = HitShape.CircleShape(3f),
            duration = 6f,
            tickInterval = 0.5f,
            growTime = 0.4f,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 3f, variance = 0f }, new SlowEffect { slowAmount = 0.35f, duration = 1f }) { blockedByObstacles = false },
        });
        return d;
    }

    public static AbilityDefinition FireBreath()
    {
        AbilityDefinition d = New("Fire Breath", AbilityTag.Ranged | AbilityTag.Area);
        d.castTime = 0.8f;
        d.cooldown = 12f;
        d.movementWhileCasting = CasterMovementRule.Stop;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 10f;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 120f;
        d.actions.Add(new BeamAction
        {
            length = 10f,
            width = 2f,
            duration = 2.5f,
            tickInterval = 0.25f,
            turnRate = 35f,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 4f, variance = 0f }, new DamageOverTimeEffect { damagePerTick = 1f, duration = 2f }),
        });
        return d;
    }

    // ------------------------------------------------------------------ control
    public static AbilityDefinition HookPull()
    {
        AbilityDefinition d = New("Hook", AbilityTag.Ranged | AbilityTag.Projectile | AbilityTag.Control | AbilityTag.GapCloser);
        d.castTime = 0.6f;
        d.cooldown = 9f;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 14f;
        d.targeting.minRange = 4f;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 150f;
        d.targeting.leadTarget = 0.6f;
        d.actions.Add(new ProjectileAction
        {
            speed = 30f,
            maxDistance = 15f,
            radius = 0.35f,
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 5f }, new PullEffect { maxDistance = 14f, stopDistance = 1f, duration = 0.35f }),
        });
        d.ai.preferredMinDistance = 5f;
        return d;
    }

    public static AbilityDefinition Vortex()
    {
        AbilityDefinition d = New("Vortex", AbilityTag.Ranged | AbilityTag.Area | AbilityTag.Control);
        d.castTime = 1f;
        d.cooldown = 12f;
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 15f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new AreaHitAction
        {
            anchor = ActionAnchor.AimPoint,
            shape = HitShape.CircleShape(5f),
            hit = new HitSettings(TargetFilter.Enemies, new PullEffect { anchorTo = PullAnchor.HitOrigin, maxDistance = 5f, stopDistance = 0.3f, duration = 0.4f }) { blockedByObstacles = false },
        });
        d.actions.Add(new AreaHitAction
        {
            anchor = ActionAnchor.AimPoint,
            delay = 0.45f,
            shape = HitShape.CircleShape(2f),
            hit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 16f }, new StunEffect { duration = 1f }),
        });
        return d;
    }

    public static AbilityDefinition StoneWall()
    {
        AbilityDefinition d = New("Stone Wall", AbilityTag.Defensive | AbilityTag.Control);
        d.castTime = 0.5f;
        d.cooldown = 14f;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 8f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new BarrierAction { anchor = ActionAnchor.Caster, layout = BarrierLayout.Wall, length = 7f, forwardOffset = 3f, duration = 6f });
        d.ai.defensive = true;
        d.ai.avoidAllyOverlap = true;
        return d;
    }

    public static AbilityDefinition Cage()
    {
        AbilityDefinition d = New("Cage", AbilityTag.Ranged | AbilityTag.Control);
        d.castTime = 1.2f;
        d.cooldown = 18f;
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = 14f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new BarrierAction { anchor = ActionAnchor.AimPoint, layout = BarrierLayout.Cage, radius = 2.5f, duration = 4f, segmentWidth = 0.9f });
        d.ai.avoidAllyOverlap = true;
        d.ai.extraAICooldown = 6f;
        return d;
    }

    public static AbilityDefinition WarCry()
    {
        AbilityDefinition d = New("War Cry", AbilityTag.Area | AbilityTag.Control | AbilityTag.Debuff);
        d.castTime = 0.5f;
        d.cooldown = 15f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.targeting.range = 7f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.CircleShape(7f),
            hit = new HitSettings(TargetFilter.Enemies, new SilenceEffect { duration = 2.5f }, new SlowEffect { slowAmount = 0.3f, duration = 3f }) { blockedByObstacles = true },
        });
        return d;
    }

    // ------------------------------------------------------------------ movement
    public static AbilityDefinition LeapSlam()
    {
        AbilityDefinition d = New("Leap Slam", AbilityTag.Melee | AbilityTag.Area | AbilityTag.GapCloser | AbilityTag.Mobility);
        d.castTime = 0.5f;
        d.activeTime = 0.5f;
        d.cooldown = 10f;
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = 12f;
        d.targeting.minRange = 4f;
        d.targeting.leadTarget = 0.6f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new MovementAction
        {
            mode = MovementActionMode.Leap,
            leapDuration = 0.7f,
            arcHeight = 3f,
            stopDistance = 0.5f,
            landingShape = HitShape.CircleShape(3f),
            landingHit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 16f }, new KnockbackEffect { distance = 2.5f }),
        });
        d.ai.preferredMinDistance = 5f;
        return d;
    }

    public static AbilityDefinition Charge()
    {
        AbilityDefinition d = New("Charge", AbilityTag.Melee | AbilityTag.GapCloser | AbilityTag.Mobility);
        d.castTime = 0.8f;
        d.activeTime = 0.6f;
        d.cooldown = 9f;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 12f;
        d.targeting.minRange = 3f;
        d.targeting.aimLock = AbilityAimLock.TrackUntilRelease;
        d.targeting.aimTurnRate = 90f;
        d.targeting.leadTarget = 0.3f;
        d.actions.Add(new MovementAction
        {
            mode = MovementActionMode.Charge,
            distance = 12f,
            speed = 22f,
            pathWidth = 1.8f,
            pathHit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 14f }, new KnockbackEffect { distance = 3.5f, arcHeight = 0.8f, direction = DisplacementDirection.AwayFromOrigin }),
        });
        d.ai.preferredMinDistance = 4f;
        return d;
    }

    public static AbilityDefinition Disengage()
    {
        AbilityDefinition d = New("Disengage", AbilityTag.Mobility | AbilityTag.Escape | AbilityTag.Defensive);
        d.castTime = 0f;
        d.activeTime = 0.2f;
        d.cooldown = 8f;
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = 6f;
        d.actions.Add(new MovementAction { mode = MovementActionMode.Retreat, distance = 6f, speed = 16f, arcHeight = 0.6f, invulnerable = true });
        d.ai.defensive = true;
        return d;
    }

    /// <summary>Teleports a short distance along the aim (a player trait ability).</summary>
    public static AbilityDefinition Blink()
    {
        AbilityDefinition d = New("Blink", AbilityTag.Mobility | AbilityTag.Escape);
        d.castTime = 0f;
        d.activeTime = 0.1f;
        d.cooldown = 6f;
        d.movementWhileCasting = CasterMovementRule.Free;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 8f;
        d.targeting.requireLineOfSight = false;
        d.actions.Add(new MovementAction { mode = MovementActionMode.Blink, distance = 8f, respectObstacles = true, invulnerable = true });
        d.absorption.canBeAbsorbed = false;
        return d;
    }

    /// <summary>A ring of stone rising around the caster, plus a moment of invulnerability (a player trait ability).</summary>
    public static AbilityDefinition GuardianBarrier()
    {
        AbilityDefinition d = New("Guardian Barrier", AbilityTag.Defensive | AbilityTag.Support);
        d.description = "Raises a ring of stone around you that blocks enemies and projectiles.";
        d.castTime = 0.3f;
        d.cooldown = 25f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.actions.Add(new BarrierAction { anchor = ActionAnchor.Caster, layout = BarrierLayout.Cage, radius = 2.2f, duration = 5f, segmentWidth = 0.9f, pushOutOverlapping = true });
        d.actions.Add(new DirectEffectAction
        {
            applyTo = DirectEffectAction.Recipient.Caster,
            hit = new HitSettings(TargetFilter.Self, new InvulnerabilityEffect { duration = 1f }) { blockedByObstacles = false },
        });
        d.absorption.canBeAbsorbed = false;
        d.ai.defensive = true;
        return d;
    }

    // ------------------------------------------------------------------ support
    public static AbilityDefinition HealSelf()
    {
        AbilityDefinition d = New("Heal Self", AbilityTag.Support | AbilityTag.Defensive);
        d.castTime = 1f;
        d.cooldown = 20f;
        d.interruptedByDamage = true;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.actions.Add(new DirectEffectAction
        {
            applyTo = DirectEffectAction.Recipient.Caster,
            hit = new HitSettings(TargetFilter.Self, new HealEffect { amount = 30f, duration = 3f }) { blockedByObstacles = false },
        });
        d.ai.defensive = true;
        d.ai.useBelowOwnHealth = 0.45f;
        return d;
    }

    public static AbilityDefinition Summon(GameObject prefab, int count)
    {
        AbilityDefinition d = New("Summon Minions", AbilityTag.Summon);
        d.castTime = 1.2f;
        d.cooldown = 25f;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.interruptedByDamage = true;
        d.actions.Add(new SummonAction { prefab = prefab, count = count, lifetime = 25f, maxAlive = 4 });
        d.ai.opener = true;
        d.ai.priority = 1.2f;
        d.absorption.canBeAbsorbed = false;
        return d;
    }
}
