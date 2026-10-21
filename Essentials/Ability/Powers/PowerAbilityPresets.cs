using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ability presets built on <see cref="PowerEffect"/>: Gigantification, Minimization, Shockwave Leap, Mirage, Greater
/// Mirage, Immovable Force, Vanish, Sky Walk and more. Listed in <see cref="AbilityPresets.All"/> (Ability From
/// Preset) and created as assets by Abilities ▸ Generate Power Abilities.
/// </summary>
public static class PowerAbilityPresets
{
    public static readonly AbilityPresets.Preset[] All =
    {
        P("Gigantification", "Grow 80% bigger for 15 s: health, strength, weapon damage and knockback grow with you.", Gigantification),
        P("Minimization", "Shrink to half your size for 15 s: 40% faster and hard to hit, but you take 40% more damage.", Minimization),
        P("Shockwave Leap", "Leap to a point and slam the ground: the shockwave grows with your size.", ShockwaveLeap),
        P("Mirage", "Four idle copies of you appear around you and you vanish for 1.5 s.", Mirage),
        P("Greater Mirage", "Six copies that stand, walk or run around; you swap places with one and vanish for 2 s.", GreaterMirage),
        P("Immovable Force", "8 s immune to stuns, roots, slows, silences, taunts and knockback; 15% less damage.", ImmovableForce),
        P("Vanish", "Invisible for 6 s; hits or attacking reveal you.", Vanish),
        P("Sky Walk", "Toggle: walk on air while you have mana (8 per second).", SkyWalk),
        P("Shadow Step", "Blink forward and stay invisible for 3 s.", ShadowStep),
        P("Phantom Veil", "2.5 s invisible and invulnerable; attacking ends it.", PhantomVeil),
        P("Stone Form", "6 s: immune to control, 50% less damage, rooted in place.", StoneForm),
        P("Titan's Wrath", "Grow 100% for 12 s and become immune to control.", TitansWrath),
        P("Shrink Ray", "A bolt that shrinks an enemy by 40% for 6 s: weaker and takes 30% more damage.", ShrinkRay),
        P("Enlarge Ally", "Makes an ally 50% bigger and tougher for 12 s.", EnlargeAlly),
        P("Cloud Step", "Walk on air for 6 s (free).", CloudStep),
        P("Smoke Bomb", "Vanish in a cloud of smoke that slows enemies inside it.", SmokeBomb),
        P("Feather Form", "Shrink 30%, double jump and glide for 20 s.", FeatherForm),
    };

    private static AbilityPresets.Preset P(string name, string description, System.Func<AbilityDefinition> create) =>
        new AbilityPresets.Preset { name = name, description = description, create = create };

    private static AbilityDefinition New(string name, string description, AbilityTag tags, float castTime, float cooldown, float mana)
    {
        var d = ScriptableObject.CreateInstance<AbilityDefinition>();
        d.name = name;
        d.SetDisplayName(name);
        d.description = description;
        d.tags = tags;
        d.castTime = castTime;
        d.cooldown = cooldown;
        d.manaCost = mana;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.absorption.canBeAbsorbed = false;
        return d;
    }

    /// <summary>Applies <paramref name="effects"/> to the caster.</summary>
    private static void OnSelf(AbilityDefinition d, params AbilityEffect[] effects)
    {
        d.actions.Add(new DirectEffectAction
        {
            applyTo = DirectEffectAction.Recipient.Caster,
            hit = new HitSettings(TargetFilter.Self, effects) { blockedByObstacles = false },
        });
    }

    private static SizeChangePower Giant(float percent) => new SizeChangePower
    {
        sizePercent = percent, healthPerSize = 1f, weaponDamagePerSize = 0.5f, knockbackPerSize = 1f, strengthPerSize = 10f, jumpPerSize = 0.3f,
    };

    // ------------------------------------------------------------------ size
    public static AbilityDefinition Gigantification()
    {
        AbilityDefinition d = New("Gigantification", "You grow 80% bigger: your health grows in proportion, with your strength, weapon damage and knockback.",
            AbilityTag.Buff | AbilityTag.Defensive, 0.6f, 40f, 30f);
        OnSelf(d, new PowerEffect(15f, Giant(80f)) { label = "Gigantification" });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition Minimization()
    {
        AbilityDefinition d = New("Minimization", "You shrink to half your size: 40% faster and harder to hit, but every hit hurts 40% more.",
            AbilityTag.Buff | AbilityTag.Mobility, 0.3f, 30f, 20f);
        OnSelf(d, new PowerEffect(15f, new SizeChangePower
        {
            sizePercent = -50f, healthPerSize = 0f, weaponDamagePerSize = 0f, knockbackPerSize = 0f, strengthPerSize = 0f, jumpPerSize = 0f,
            moveSpeedPercent = 40f, damageTakenPercent = 40f,
        }) { label = "Minimization" });
        return d;
    }

    public static AbilityDefinition TitansWrath()
    {
        AbilityDefinition d = New("Titan's Wrath", "You double in size and nothing can stop you.", AbilityTag.Buff | AbilityTag.Ultimate, 1f, 90f, 50f);
        OnSelf(d, new PowerEffect(12f, Giant(100f), new ControlImmunityPower()) { label = "Titan's Wrath" });
        return d;
    }

    public static AbilityDefinition ShrinkRay()
    {
        AbilityDefinition d = New("Shrink Ray", "A bolt that shrinks an enemy: it deals less damage and takes more.", AbilityTag.Ranged | AbilityTag.Projectile | AbilityTag.Debuff, 0.4f, 14f, 15f);
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 20f;
        d.actions.Add(new ProjectileAction
        {
            speed = 22f,
            maxDistance = 22f,
            radius = 0.3f,
            hit = new HitSettings(TargetFilter.Enemies, new PowerEffect(6f, new SizeChangePower
            {
                sizePercent = -40f, healthPerSize = 0f, weaponDamagePerSize = 0.6f, knockbackPerSize = 1f, strengthPerSize = 10f, jumpPerSize = 0f,
                damageTakenPercent = 30f,
            }) { label = "Shrunk", onlyAffects = TargetFilter.Enemies }),
        });
        return d;
    }

    public static AbilityDefinition EnlargeAlly()
    {
        AbilityDefinition d = New("Enlarge Ally", "Makes an ally bigger and tougher.", AbilityTag.Support | AbilityTag.Buff, 0.8f, 25f, 25f);
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = 15f;
        d.actions.Add(new DirectEffectAction
        {
            applyTo = DirectEffectAction.Recipient.Target,
            hit = new HitSettings(TargetFilter.Allies | TargetFilter.Self, new PowerEffect(12f, Giant(50f)) { label = "Enlarged" }) { blockedByObstacles = false },
        });
        return d;
    }

    public static AbilityDefinition FeatherForm()
    {
        AbilityDefinition d = New("Feather Form", "You shrink, become light, and can double jump and glide.", AbilityTag.Buff | AbilityTag.Mobility, 0.3f, 35f, 20f);
        OnSelf(d, new PowerEffect(20f,
            new SizeChangePower { sizePercent = -30f, healthPerSize = 0f, weaponDamagePerSize = 0f, knockbackPerSize = 0f, strengthPerSize = 0f, jumpPerSize = 0f, moveSpeedPercent = 15f },
            new MovementPower { behaviour = new DoubleJumpTrait() },
            new MovementPower { behaviour = new GlideTrait() }) { label = "Feather Form" });
        return d;
    }

    // ------------------------------------------------------------------ leap
    public static AbilityDefinition ShockwaveLeap()
    {
        AbilityDefinition d = New("Shockwave Leap", "Leap to a point; landing sends a shockwave that knocks enemies away. Bigger characters make bigger shockwaves.",
            AbilityTag.Area | AbilityTag.Mobility | AbilityTag.GapCloser, 0.3f, 12f, 15f);
        d.activeTime = 0.6f;
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 12f;
        d.targeting.aimLock = AbilityAimLock.LockAtCastStart;
        d.actions.Add(new MovementAction
        {
            mode = MovementActionMode.Leap,
            leapDuration = 0.75f,
            arcHeight = 4f,
            stopDistance = 0f,
            landingShape = HitShape.CircleShape(4f),
            landingGrowsWithSize = 1f,
            landingHit = new HitSettings(TargetFilter.Enemies, new DamageEffect { amount = 20f, form = DamageForm.Crush },
                new KnockbackEffect { distance = 3.5f, arcHeight = 1f, direction = DisplacementDirection.AwayFromOrigin }, new StunEffect { duration = 0.5f }),
        });
        d.ai.preferredMinDistance = 5f;
        return d;
    }

    // ------------------------------------------------------------------ illusions
    public static AbilityDefinition Mirage()
    {
        AbilityDefinition d = New("Mirage", "Copies of you appear around you, standing still, and you vanish for a moment. A copy disappears when hit.",
            AbilityTag.Defensive | AbilityTag.Escape, 0.2f, 20f, 25f);
        OnSelf(d, new PowerEffect(6f, new MiragePower { copies = 4, radius = 3f, motion = MirageMotion.Idle, casterInvisibleSeconds = 1.5f }) { label = "Mirage" });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition GreaterMirage()
    {
        AbilityDefinition d = New("Greater Mirage", "Copies of you appear and walk, run or stand around; you swap places with one of them and vanish for a moment.",
            AbilityTag.Defensive | AbilityTag.Escape, 0.2f, 30f, 40f);
        OnSelf(d, new PowerEffect(8f, new MiragePower { copies = 6, radius = 4f, motion = MirageMotion.Mixed, casterInvisibleSeconds = 2f, swapWithACopy = true })
            { label = "Greater Mirage" });
        d.ai.defensive = true;
        return d;
    }

    // ------------------------------------------------------------------ defence and stealth
    public static AbilityDefinition ImmovableForce()
    {
        AbilityDefinition d = New("Immovable Force", "Nothing can stun, root, slow, silence, taunt or push you, and you take 15% less damage.",
            AbilityTag.Defensive | AbilityTag.Buff, 0f, 30f, 20f);
        OnSelf(d, new PowerEffect(8f, new ControlImmunityPower { damageTakenPercent = -15f }) { label = "Immovable Force" });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition StoneForm()
    {
        AbilityDefinition d = New("Stone Form", "You turn to stone: rooted, immune to control and taking half damage.", AbilityTag.Defensive, 0.3f, 40f, 25f);
        OnSelf(d,
            new PowerEffect(6f, new ControlImmunityPower { immuneTo = ControlImmunity.Stun | ControlImmunity.Silence | ControlImmunity.Slow | ControlImmunity.Taunt | ControlImmunity.AllDisplacement, damageTakenPercent = -50f })
                { label = "Stone Form" },
            new RootEffect { duration = 6f, onlyAffects = TargetFilter.Self });
        d.ai.defensive = true;
        d.ai.useBelowOwnHealth = 0.4f;
        return d;
    }

    public static AbilityDefinition Vanish()
    {
        AbilityDefinition d = New("Vanish", "You turn invisible. Enemies lose track of you until you attack or are hit.", AbilityTag.Escape | AbilityTag.Defensive, 0f, 25f, 20f);
        OnSelf(d, new PowerEffect(6f, new InvisibilityPower()) { label = "Vanish" });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition PhantomVeil()
    {
        AbilityDefinition d = New("Phantom Veil", "You fade out of the world: invisible and untouchable for a moment.", AbilityTag.Escape | AbilityTag.Defensive, 0f, 35f, 30f);
        OnSelf(d, new PowerEffect(2.5f, new InvisibilityPower { breakOnDamaged = false }) { label = "Phantom Veil" }, new InvulnerabilityEffect { duration = 2.5f });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition ShadowStep()
    {
        AbilityDefinition d = New("Shadow Step", "Blink forward through the shadows and stay invisible for a moment.", AbilityTag.Mobility | AbilityTag.Escape, 0f, 15f, 15f);
        d.activeTime = 0.1f;
        d.movementWhileCasting = CasterMovementRule.Free;
        d.targeting.mode = AbilityTargetingMode.Direction;
        d.targeting.range = 8f;
        d.targeting.requireLineOfSight = false;
        d.actions.Add(new MovementAction { mode = MovementActionMode.Blink, distance = 8f, respectObstacles = true, invulnerable = true });
        OnSelf(d, new PowerEffect(3f, new InvisibilityPower { graceSeconds = 0.5f }) { label = "Shadow Step" });
        return d;
    }

    public static AbilityDefinition SmokeBomb()
    {
        AbilityDefinition d = New("Smoke Bomb", "A cloud of smoke: you vanish and enemies inside are slowed.", AbilityTag.Escape | AbilityTag.Area | AbilityTag.Control, 0f, 25f, 15f);
        d.actions.Add(new ZoneAction
        {
            shape = HitShape.CircleShape(4f),
            duration = 4f,
            tickInterval = 0.5f,
            hit = new HitSettings(TargetFilter.Enemies, new SlowEffect { slowAmount = 0.4f, duration = 1f }),
        });
        OnSelf(d, new PowerEffect(4f, new InvisibilityPower { breakOnDamaged = false }) { label = "Smoke Bomb" });
        return d;
    }

    // ------------------------------------------------------------------ movement
    public static AbilityDefinition SkyWalk()
    {
        AbilityDefinition d = New("Sky Walk", "Walk on air: you stop falling, Jump rises, Crouch descends. Costs 8 mana per second in the air. Cast again to stop.",
            AbilityTag.Mobility | AbilityTag.Buff, 0f, 1f, 5f);
        d.movementWhileCasting = CasterMovementRule.Free;
        OnSelf(d, new PowerEffect(0f, new MovementPower { behaviour = new SkyWalkTrait { cost = SkyWalkCost.Mana, costPerSecond = 8f } })
            { label = "Sky Walk", toggle = true });
        return d;
    }

    public static AbilityDefinition CloudStep()
    {
        AbilityDefinition d = New("Cloud Step", "Walk on air for a few seconds.", AbilityTag.Mobility, 0f, 20f, 20f);
        d.movementWhileCasting = CasterMovementRule.Free;
        OnSelf(d, new PowerEffect(6f, new MovementPower { behaviour = new SkyWalkTrait { cost = SkyWalkCost.Nothing } }) { label = "Cloud Step" });
        return d;
    }
}
