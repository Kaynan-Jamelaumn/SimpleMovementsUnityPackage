using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A moment of an attack's timeline at which a behaviour runs.</summary>
public enum AttackMoment
{
    /// <summary>When the attack starts (after charging, if it was charged).</summary>
    Start,
    /// <summary>When the active (hitting) phase begins.</summary>
    ActiveStart,
    /// <summary>Every time the attack hits a character.</summary>
    EachHit,
    /// <summary>When the active phase ends.</summary>
    ActiveEnd,
    /// <summary>When the attack is over.</summary>
    End,
    /// <summary>When charging begins (charged attacks).</summary>
    ChargeStart,
    /// <summary>When the charge becomes full (charged attacks).</summary>
    FullCharge,
    /// <summary>When the attack's Impact happens (the weapon strikes the ground: a slam).</summary>
    Impact,
}

/// <summary>Everything a behaviour knows about the attack it belongs to. Created by the weapon controller for each attack.</summary>
public sealed class AttackContext
{
    public WeaponController Controller;
    public WeaponSO Weapon;
    public AttackComponent Attack;
    /// <summary>The input that started it.</summary>
    public AttackType Input;
    public CombatEntity Attacker;
    public GameObject AttackerObject;
    /// <summary>The weapon hand (null when the controller has none).</summary>
    public Transform Hand;
    /// <summary>0 = not charged, 1 = fully charged.</summary>
    public float ChargeRatio;
    /// <summary>Damage multiplier of this attack (combo, charge, branch bonuses...), weapon damage aside.</summary>
    public float DamageMultiplier = 1f;
    /// <summary>Scale of hit areas (charge).</summary>
    public float AreaMultiplier = 1f;
    /// <summary>Characters hit so far.</summary>
    public int HitCount;
    /// <summary>The last character hit (EachHit behaviours: the one just hit).</summary>
    public CombatEntity LastHitTarget;
    public Vector3 LastHitPoint;
    /// <summary>Where the Impact happened (Impact behaviours), and the surface normal there.</summary>
    public Vector3 ImpactPoint;
    public Vector3 ImpactNormal = Vector3.up;
    /// <summary>Deals this attack's weapon damage times a factor to a character (returns the damage dealt, 0 if not hit).</summary>
    public Func<CombatEntity, float, float> DealWeaponDamage;

    public Vector3 Origin => Attacker != null ? Attacker.BasePosition : AttackerObject != null ? AttackerObject.transform.position : Vector3.zero;

    public Vector3 Forward
    {
        get
        {
            Vector3 f = Attacker != null ? Attacker.Forward : AttackerObject != null ? AttackerObject.transform.forward : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }
    }

    /// <summary>Ability modifiers matching this attack's strength (for abilities cast by it).</summary>
    public AbilityModifierSet AbilityModifiers(bool scaleDamage) => !scaleDamage || Mathf.Approximately(DamageMultiplier, 1f) && Mathf.Approximately(AreaMultiplier, 1f)
        ? null
        : new AbilityModifierSet { label = "Weapon attack", damageMultiplier = DamageMultiplier, areaMultiplier = AreaMultiplier };
}

/// <summary>
/// Something extra an attack does at a moment of its timeline (<see cref="AttackMoment"/>): cast an ability, lunge,
/// buff the attacker, hit an area, spawn an effect... Add them to an attack's Behaviours with the type dropdown; write
/// new ones by deriving from this class.
/// </summary>
[Serializable]
public abstract class AttackBehaviour
{
    [Tooltip("When it runs.")]
    public AttackMoment when = AttackMoment.ActiveStart;
    [Tooltip("Chance (0-1) that it runs.")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("Only when the attack was charged at least this much (0 = always, 1 = only at full charge).")]
    [Range(0f, 1f)] public float minCharge = 0f;

    public string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Should it run now (chance and charge checked)?</summary>
    public bool ShouldRun(AttackContext ctx) =>
        ctx.ChargeRatio + 1e-4f >= minCharge && (chance >= 1f || UnityEngine.Random.value <= chance);

    public abstract void Execute(AttackContext ctx);
    public abstract string Describe();

    public virtual void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (chance <= 0f)
            warnings.Add($"{owner}: {MenuName} has 0% chance and never runs.");
    }

    protected static string When(AttackMoment m)
    {
        switch (m)
        {
            case AttackMoment.Start: return "on swing";
            case AttackMoment.ActiveStart: return "when it strikes";
            case AttackMoment.EachHit: return "on each hit";
            case AttackMoment.ActiveEnd: return "after the strike";
            case AttackMoment.End: return "at the end";
            case AttackMoment.ChargeStart: return "when charging starts";
            default: return "at full charge";
        }
    }
}

[Serializable, AbilityMenu("Ability/Cast Ability", "Casts an ability (a projectile, a shockwave, a beam...) as part of the attack. Its damage follows the attack's strength when 'Scale With Attack' is on.", 0)]
public class CastAbilityBehaviour : AttackBehaviour
{
    public enum Aim
    {
        /// <summary>Straight ahead of the attacker.</summary>
        Forward,
        /// <summary>At the character just hit (Each Hit) or the nearest enemy ahead.</summary>
        Target,
        /// <summary>At the attacker's feet.</summary>
        Self,
    }

    [Tooltip("The ability cast (an Ability Definition asset).")]
    public AbilityDefinition ability;
    [Tooltip("Where it is aimed.")]
    public Aim aim = Aim.Forward;
    [Tooltip("Aim distance in front of the attacker for Forward aim (metres).")]
    [Min(0f)] public float forwardDistance = 3f;
    [Tooltip("The ability's damage and area follow the attack's multipliers (combo, charge).")]
    public bool scaleWithAttack = true;

    public override string Describe() => ability != null ? $"Casts {ability.DisplayName} {When(when)}" : "Cast Ability (none)";

    public override void Execute(AttackContext ctx)
    {
        if (ability == null)
            return;
        CombatEntity target = null;
        Vector3 dir = ctx.Forward;
        Vector3 point = ctx.Origin + dir * forwardDistance;
        if (aim == Aim.Self)
        {
            point = ctx.Origin;
        }
        else if (aim == Aim.Target)
        {
            target = ctx.LastHitTarget != null && ctx.LastHitTarget.IsAlive ? ctx.LastHitTarget
                : CombatQuery.BestInCone(ctx.Origin, dir, 60f, Mathf.Max(forwardDistance, 8f), ctx.Attacker, TargetFilter.Enemies);
            if (target != null)
            {
                point = target.BasePosition;
                dir = CombatQuery.FlatDirection(ctx.Origin, point, dir);
            }
        }
        AbilityCaster.ExecuteInstant(ability, ctx.Attacker, point, dir, target, ctx.AbilityModifiers(scaleWithAttack));
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (ability == null)
            errors.Add($"{owner}: Cast Ability has no ability.");
    }
}

[Serializable, AbilityMenu("Movement/Lunge", "Moves the attacker forward (or back) during the attack: a lunging stab, a leaping slam, a backstep.", 0)]
public class LungeBehaviour : AttackBehaviour
{
    [Tooltip("Distance (metres). Negative = backwards.")]
    public float distance = 2f;
    [Tooltip("Seconds the movement takes.")]
    [Min(0.02f)] public float duration = 0.2f;
    [Tooltip("Jump height of the movement (0 = along the ground).")]
    [Min(0f)] public float arcHeight = 0f;

    public LungeBehaviour() { when = AttackMoment.Start; }

    public override string Describe() => $"{(distance >= 0f ? "Lunges" : "Steps back")} {Mathf.Abs(distance):0.#} m {When(when)}";

    public override void Execute(AttackContext ctx)
    {
        if (ctx.Attacker == null || Mathf.Approximately(distance, 0f))
            return;
        ForcedMovement fm = ForcedMovement.GetOrAdd(ctx.Attacker);
        if (fm != null && !fm.IsActive)
            fm.Begin(ctx.Forward * distance, duration, arcHeight, true);
    }
}

[Serializable, AbilityMenu("Buff/Effects On Self", "Applies effects to the attacker: heal, shield, speed boost, invulnerability... (the same effects abilities use).", 0)]
public class SelfEffectsBehaviour : AttackBehaviour
{
    [Tooltip("Applied to the attacker.")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    public SelfEffectsBehaviour() { when = AttackMoment.Start; }

    public override string Describe() => $"{EquipmentProcs.DescribeEffects(effects)} to self {When(when)}";

    public override void Execute(AttackContext ctx)
    {
        if (ctx.Attacker == null || effects == null || effects.Count == 0)
            return;
        var hit = new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = effects };
        EquipmentProcs.Apply(ctx.Attacker, ctx.Attacker, hit, null, 1f, null, null);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (effects == null || effects.Count == 0)
            errors.Add($"{owner}: Effects On Self has no effects.");
    }
}

[Serializable, AbilityMenu("Hit/Area Burst", "Hits everything in an area at one moment - a ground slam shockwave, a spin, an explosion at the hit point - with a part of the weapon damage and its own effects.", 0)]
public class AreaBurstBehaviour : AttackBehaviour
{
    public enum Center
    {
        /// <summary>The attacker (shape placed in front of it).</summary>
        Attacker,
        /// <summary>The character just hit (Each Hit).</summary>
        HitTarget,
    }

    [Tooltip("Where the area is.")]
    public Center center = Center.Attacker;
    [Tooltip("The area.")]
    public HitShape shape = HitShape.CircleShape(3f);
    [Tooltip("Who is hit, relative to the attacker.")]
    public TargetFilter filter = TargetFilter.Enemies;
    [Tooltip("Part of the attack's weapon damage dealt to each character (0 = none, only the effects).")]
    [Min(0f)] public float weaponDamageFraction = 0.5f;
    [Tooltip("Applied to each character in the area.")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();
    [Tooltip("The character hit directly is not hit again by the burst.")]
    public bool skipDirectTarget = true;
    public GameObject vfx;
    public AudioClip sound;

    private static readonly List<CombatEntity> buffer = new List<CombatEntity>(16);

    public override string Describe() => $"Area burst {When(when)}: {weaponDamageFraction * 100f:0}% damage in {shape.Describe(1f)}";

    public override void Execute(AttackContext ctx)
    {
        CombatEntity anchor = center == Center.HitTarget ? ctx.LastHitTarget : null;
        if (center == Center.HitTarget && anchor == null)
            return;
        Vector3 pos = anchor != null ? anchor.BasePosition : ctx.Origin;
        Quaternion rot = Quaternion.LookRotation(ctx.Forward, Vector3.up);
        ResolvedShape resolved = ResolvedShape.Resolve(shape, pos, rot, ctx.AreaMultiplier);
        CombatQuery.Overlap(resolved, buffer);
        var hit = effects != null && effects.Count > 0 ? new HitSettings { filter = filter, blockedByObstacles = false, effects = effects } : null;
        for (int i = 0; i < buffer.Count; i++)
        {
            CombatEntity e = buffer[i];
            if (e == null || e == ctx.Attacker || !e.IsAlive)
                continue;
            if (skipDirectTarget && e == ctx.LastHitTarget && center == Center.HitTarget)
                continue;
            if (ctx.Attacker != null && !CombatRelations.Passes(filter, CombatRelations.Get(ctx.Attacker, e)))
                continue;
            if (weaponDamageFraction > 0f)
                ctx.DealWeaponDamage?.Invoke(e, weaponDamageFraction);
            if (hit != null)
                EquipmentProcs.Apply(ctx.Attacker, e, hit, null, ctx.DamageMultiplier, null, null);
        }
        buffer.Clear();
        if (vfx != null)
            AbilityPool.PlayVfx(vfx, pos, rot, 0f, ctx.AreaMultiplier);
        if (sound != null)
            AbilityPool.PlaySound(sound, pos, 1f);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (weaponDamageFraction <= 0f && (effects == null || effects.Count == 0))
            warnings.Add($"{owner}: Area Burst deals no damage and has no effects.");
        if (shape != null)
            shape.Validate(owner + " ▸ Area Burst", warnings);
    }
}

[Serializable, AbilityMenu("Visual/Spawn Effect", "Spawns a prefab (slash trail, sparks, shockwave visual) and plays a sound at a moment of the attack.", 0)]
public class SpawnEffectBehaviour : AttackBehaviour
{
    public enum Where { Hand, AttackerFeet, InFront, HitPoint }

    public GameObject prefab;
    public AudioClip sound;
    public Where where = Where.InFront;
    [Tooltip("In Front: distance ahead of the attacker.")]
    public float distance = 1.5f;
    [Tooltip("Seconds before it is removed (0 = from its particle systems).")]
    [Min(0f)] public float lifetime = 0f;
    [Tooltip("Follows the hand/attacker while it lasts.")]
    public bool follow = false;

    public override string Describe() => "";

    public override void Execute(AttackContext ctx)
    {
        Vector3 pos;
        Transform followT = null;
        switch (where)
        {
            case Where.Hand:
                pos = ctx.Hand != null ? ctx.Hand.position : ctx.Origin + Vector3.up;
                followT = ctx.Hand;
                break;
            case Where.AttackerFeet:
                pos = ctx.Origin;
                followT = ctx.Attacker != null ? ctx.Attacker.transform : null;
                break;
            case Where.HitPoint:
                if (ctx.LastHitTarget == null) return;
                pos = ctx.LastHitPoint;
                break;
            default:
                pos = ctx.Origin + ctx.Forward * distance + Vector3.up;
                break;
        }
        Quaternion rot = Quaternion.LookRotation(ctx.Forward, Vector3.up);
        if (prefab != null)
            AbilityPool.PlayVfx(prefab, pos, rot, lifetime, 1f, follow ? followT : null);
        if (sound != null)
            AbilityPool.PlaySound(sound, pos, 1f);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (prefab == null && sound == null)
            warnings.Add($"{owner}: Spawn Effect has no prefab or sound.");
    }
}

[Serializable, AbilityMenu("Defense/Invulnerability", "Makes the attacker invulnerable for a moment (a parry, a spin through enemies, an unstoppable finisher).", 0)]
public class InvulnerabilityBehaviour : AttackBehaviour
{
    [Tooltip("Seconds of invulnerability.")]
    [Min(0.02f)] public float duration = 0.3f;

    public InvulnerabilityBehaviour() { when = AttackMoment.Start; }

    public override string Describe() => $"Invulnerable for {duration:0.##}s {When(when)}";

    public override void Execute(AttackContext ctx) => ctx.Attacker?.SetInvulnerable(duration);
}
