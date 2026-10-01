using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Hold-to-charge settings of an attack (a bow draw, a charged heavy swing).</summary>
[Serializable]
public class ChargeSettings
{
    [Tooltip("Hold the attack input to charge; the attack happens when it is released (or when fully charged, see below).")]
    public bool enabled = false;
    [Tooltip("Seconds of holding before the attack counts as charged at all. Releasing earlier does what 'Early Release' says.")]
    [Min(0f)] public float minChargeTime = 0.25f;
    [Tooltip("Seconds of holding for a full charge.")]
    [Min(0.05f)] public float maxChargeTime = 1.2f;
    [Tooltip("Damage multiplier at full charge (at the minimum charge it is 1; in between it grows linearly).")]
    [Min(1f)] public float damageAtFullCharge = 2f;
    [Tooltip("Hit area scale at full charge (1 = unchanged).")]
    [Min(0.1f)] public float areaAtFullCharge = 1f;
    [Tooltip("What happens when the input is released before the minimum charge time.")]
    public EarlyRelease earlyRelease = EarlyRelease.NormalAttack;
    [Tooltip("Attack by itself when fully charged (off = keep holding until released).")]
    public bool autoReleaseAtFull = false;
    [Tooltip("Stamina spent per second while charging (on top of the attack's cost).")]
    [Min(0f)] public float staminaPerSecond = 0f;
    [Tooltip("Movement speed multiplier while charging.")]
    [Range(0f, 1f)] public float moveSpeedWhileCharging = 0.5f;
    [Tooltip("Animation played (looped by the animator) while charging.")]
    public AnimationClip chargeAnimation;
    [Tooltip("Effect attached to the weapon hand while charging.")]
    public GameObject chargingVfx;
    [Tooltip("Effect and sound when the charge is full.")]
    public GameObject fullChargeVfx;
    public AudioClip fullChargeSound;

    public enum EarlyRelease
    {
        /// <summary>A normal (uncharged) attack.</summary>
        NormalAttack,
        /// <summary>Nothing happens (the stamina of the attack is not spent).</summary>
        Cancel,
    }

    /// <summary>0 below the minimum charge, 1 at full charge.</summary>
    public float Ratio(float heldSeconds)
    {
        if (heldSeconds < minChargeTime)
            return 0f;
        float span = Mathf.Max(0.01f, maxChargeTime - minChargeTime);
        return Mathf.Clamp01((heldSeconds - minChargeTime) / span);
    }

    public float DamageMultiplier(float ratio) => Mathf.Lerp(1f, damageAtFullCharge, Mathf.Clamp01(ratio));
    public float AreaMultiplier(float ratio) => Mathf.Lerp(1f, areaAtFullCharge, Mathf.Clamp01(ratio));
}

/// <summary>
/// A second, separate hit of an attack where the weapon strikes the ground (a hammer slam, a greatsword smash): an
/// area of its own shape around the impact point. Characters inside it are hit whether or not they touch the weapon.
/// </summary>
[Serializable]
public class ImpactSettings
{
    public enum Trigger
    {
        /// <summary>When the weapon (its blade tip, or the hand) reaches the ground during the active phase. No contact = no impact.</summary>
        GroundContact,
        /// <summary>At ground contact, or at the end of the active phase if the weapon never touched the ground (reliable).</summary>
        GroundContactOrActiveEnd,
        /// <summary>When the active phase starts, on the ground below the weapon.</summary>
        ActiveStart,
        /// <summary>When the active phase ends, on the ground below the weapon.</summary>
        ActiveEnd,
    }

    [Tooltip("This attack has an impact (a ground slam) in addition to its normal hit.")]
    public bool enabled = false;
    [Tooltip("When the impact happens.\n• Ground Contact: when the weapon reaches the ground.\n• Ground Contact Or Active End: the same, " +
             "or at the end of the active phase if it never touched the ground.\n• Active Start / Active End: at that moment, on the ground below the weapon.")]
    public Trigger trigger = Trigger.GroundContactOrActiveEnd;
    [Tooltip("PHYSICS: layers the weapon can strike as ground (terrain, floors, rocks). Nothing = Combat Settings ▸ Ground Layers. " +
             "Characters are never ground.")]
    public LayerMask groundLayers = 0;
    [Tooltip("Which hit volume of the weapon must reach the ground (e.g. Head). Empty = the attack's first volume (or the weapon's first).")]
    public string contactVolume = "";
    [Tooltip("How close (metres) the weapon must come to the ground to count as touching it.")]
    [Min(0.01f)] public float contactTolerance = 0.15f;

    [Tooltip("The impact area, centred on the impact point and facing where the attacker looks. Characters inside it are " +
             "hit - they do not need to touch the weapon or the ground. Charge scales it.")]
    public HitShape area = HitShape.CircleShape(3f);
    [Tooltip("Who the impact can hit, relative to the attacker (see the buttons' tooltips).")]
    public TargetFilter filter = TargetFilter.Enemies;
    [Tooltip("Optional: friendly fire of the impact and which kinds / factions it can reach.")]
    public TargetRules rules = new TargetRules();
    [Tooltip("Characters hit at most (closest first). 0 = no limit.")]
    [Min(0)] public int maxTargets = 0;
    [Tooltip("Characters the swing already hit can be hit again by the impact.")]
    public bool alsoHitsSwingTargets = true;

    [Tooltip("Weapon damage dealt by the impact, as a fraction of this attack's damage (1 = the same as the swing, 0 = only the effects below).")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Strength at the edge of the area compared to the centre (1 = the same everywhere, 0.5 = half at the edge). Scales damage and effects.")]
    [Range(0f, 1f)] public float edgeMultiplier = 0.5f;
    [Tooltip("Applied to each character hit by the impact: knock-up, stun, slow, burn...")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    [Tooltip("Spawned at the impact point (dust, cracks, shockwave).")]
    public GameObject vfx;
    [Tooltip("Played at the impact point.")]
    public AudioClip sound;

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (!enabled) return;
        if (area == null) { errors.Add($"{owner}: the Impact has no area."); return; }
        var e = new List<string>();
        area.Validate(owner + " ▸ Impact Area", e);
        warnings.AddRange(e);
        if (filter == TargetFilter.None)
            errors.Add($"{owner}: the Impact's filter is empty, it can hit nobody.");
        if (damageMultiplier <= 0f && (effects == null || effects.Count == 0))
            warnings.Add($"{owner}: the Impact deals no damage and has no effects.");
    }
}

/// <summary>
/// What an attack (an <see cref="AttackAction"/> or one of its <see cref="AttackVariation"/>s) is: animation and
/// timing (startup, active and recovery phases), cost, movement, damage, how it finds what it hits, what it does to
/// what it hits (status effects), charging, and extra <see cref="AttackBehaviour"/>s (cast an ability, lunge, spawn a
/// projectile...). All of it is data: new attacks never need weapon-specific code.
/// </summary>
[Serializable]
public abstract class AttackComponent : IAttackComponent
{
    [Header("Animation & Timing")]
    [Tooltip("Animation played for the attack. Empty = the animator trigger of the attack type (AttackTrigger, HeavyAttackTrigger...).")]
    [SerializeField] protected AnimationClip animationClip;
    [Tooltip("Playback speed of the attack (2 = twice as fast). Attack speed bonuses multiply it.")]
    [SerializeField] public float animationSpeed = 1.0f;
    [Tooltip("Seconds before the attack can hit (wind-up).")]
    [SerializeField] protected float startupFrames = 0.1f;
    [Tooltip("Seconds during which the attack hits.")]
    [SerializeField] protected float activeFrames = 0.2f;
    [Tooltip("Seconds after the hit before the character is free again.")]
    [SerializeField] protected float recoveryFrames = 0.3f;
    [Tooltip("How far into the recovery a buffered next attack (a combo) may start: 0 = right after the hit, 1 = only when the recovery is over.")]
    [Range(0f, 1f)] public float cancelPoint = 1f;

    [Header("Combat Properties")]
    [Tooltip("Stamina spent when the attack starts.")]
    public float staminaCost;
    [Tooltip("The character cannot move during the attack.")]
    [SerializeField] protected bool lockMovement = false;
    [Tooltip("Movement speed multiplier during the attack (when movement is not locked).")]
    [SerializeField] protected float movementSpeedMultiplier = 1.0f;
    [Tooltip("Distance moved during startup + active (z = forward, x = right), e.g. (0, 0, 1.5) for a lunging stab.")]
    [SerializeField] protected Vector3 forwardMovement = Vector3.zero;

    [Header("Damage")]
    [Tooltip("Auto: the weapon's damage, unless the classic Effects below already deal health damage. Weapon Damage: always the weapon's damage. Effects Only: only the effects.")]
    public WeaponDamageMode damageMode = WeaponDamageMode.Auto;
    [Tooltip("Multiplies the weapon's damage for this attack (a heavy attack 1.8, a quick jab 0.6).")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Physical is reduced by Defense, Magical by Magic Resistance, True by neither.")]
    public DamageType damageType = DamageType.Physical;
    [Tooltip("Element of this attack. None = the weapon's element.")]
    public ElementType elementOverride = ElementType.None;
    [Tooltip("Extra critical chance (0-1) for this attack.")]
    [Range(0f, 1f)] public float criticalChanceBonus = 0f;
    [Tooltip("Multiplies the weapon's knockback for this attack (0 = none).")]
    [Min(0f)] public float knockbackMultiplier = 1f;

    [Header("Hit Detection")]
    [Tooltip("How the attack finds what it hits.\n• Hit Shape (recommended): the area below, in front of the character.\n" +
             "• Weapon Cast: the weapon's old Attack Cast around the hand.\n• Auto: Weapon Cast when the weapon's Attack Cast has Target " +
             "Layers, otherwise the Hit Shape.\n• None: no direct hit, only the behaviours (a projectile, an ability).")]
    public HitDetectionMode hitDetection = HitDetectionMode.Auto;
    [Tooltip("The area this attack hits: placed at the attacker's feet, facing where they look, checked every frame of the Active phase. " +
             "This IS the attack's reach (a Cone of Radius 2.2 reaches 2.2 m). Used when Hit Detection is Hit Shape, or Auto without an Attack Cast.")]
    public HitShape hitShape = HitShape.ConeShape(2.2f, 110f);
    [Tooltip("Who can be hit, relative to the attacker (teams come from the Combat Entity: same team = Allies).\n• Self: the attacker.\n" +
             "• Allies: same team.\n• Enemies: hostile characters. For the player every character of another team is an enemy (mobs, animals).\n" +
             "• Neutral: neither (e.g. a mob another mob is not hostile to).")]
    public TargetFilter hitFilter = TargetFilter.Enemies;
    [Tooltip("Weapon Blade: which of the weapon's hit volumes this attack uses, by name (Head, Handle, Shield...). Empty = all of them.")]
    public List<string> bladeVolumes = new List<string>();
    [Tooltip("Optional: friendly fire of this attack (Game Rule / Never / Always harms party members and allies) and which " +
             "kinds of characters / factions it can reach. The defaults change nothing.")]
    public TargetRules targetRules = new TargetRules();
    [Tooltip("Maximum characters hit per attack (closest first). 0 = no limit.")]
    [Min(0)] public int maxTargets = 0;
    [Tooltip("Seconds before the same target can be hit again during the active phase (multi-hit attacks). 0 = once per attack.")]
    [Min(0f)] public float rehitInterval = 0f;

    [Header("Impact (ground slam)")]
    [Tooltip("A second hit where the weapon strikes the ground: its own area around the impact point, damage and effects.")]
    public ImpactSettings impact = new ImpactSettings();

    [Header("On Hit")]
    [Tooltip("Applied to each character hit: burn, poison, slow, stun, knockback, life steal (Recipient = Caster)... The same effects abilities use.")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> onHitEffects = new List<AbilityEffect>();
    [Tooltip("Spawned on each character hit.")]
    public GameObject hitVfx;
    [Tooltip("Played on each hit that lands (the Attack Sound plays at the swing).")]
    public AudioClip hitSound;

    [Header("Charge")]
    public ChargeSettings charge = new ChargeSettings();

    [Header("Behaviours")]
    [Tooltip("Extra things the attack does at a moment of its timeline: cast an ability, lunge, fire a projectile, buff the attacker, spawn an effect...")]
    [SerializeReference, SubclassSelector] public List<AttackBehaviour> behaviours = new List<AttackBehaviour>();

    [Header("Effects")]
    [Tooltip("Classic status effects (Hp, Speed, Stamina... amounts). Negative Hp on enemies is damage. Kept for older weapons; On Hit Effects are more flexible.")]
    [SerializeField] protected List<AttackActionEffect> effects = new List<AttackActionEffect>();

    [Header("Audio & Visual")]
    [Tooltip("Played when the attack starts, the swing (empty = the weapon's Attack Sound). The sound of a hit landing is the Hit Sound.")]
    [SerializeField] protected AudioClip attackSound;
    [Tooltip("Spawned where the attack hits.")]
    [SerializeField] protected ParticleSystem attackParticles;
    [Tooltip("Attached to the weapon hand during the attack.")]
    [SerializeField] protected GameObject trailEffect;

    // IAttackComponent implementation
    public float StartupFrames => startupFrames;
    public float ActiveFrames => activeFrames;
    public float RecoveryFrames => recoveryFrames;
    public float AnimationSpeed => animationSpeed;
    public float StaminaCost => staminaCost;
    public bool LockMovement => lockMovement;
    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public Vector3 ForwardMovement => forwardMovement;
    public AnimationClip AnimationClip => animationClip;
    public AudioClip AttackSound => attackSound;
    public ParticleSystem AttackParticles => attackParticles;
    public GameObject TrailEffect => trailEffect;
    public List<AttackActionEffect> Effects => effects ?? (effects = new List<AttackActionEffect>());

    /// <summary>Name shown in debug output and tooltips.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Sets the timeline (seconds at speed 1) and playback speed (templates, tools, scripts building attacks).</summary>
    public void SetTiming(float startup, float active, float recovery, float speed = 1f)
    {
        startupFrames = Mathf.Max(0f, startup);
        activeFrames = Mathf.Max(0f, active);
        recoveryFrames = Mathf.Max(0f, recovery);
        animationSpeed = Mathf.Max(0.01f, speed);
    }

    /// <summary>Sets how the attacker moves during the attack.</summary>
    public void SetMovement(bool lockMove, float speedMultiplier, Vector3 forward)
    {
        lockMovement = lockMove;
        movementSpeedMultiplier = speedMultiplier;
        forwardMovement = forward;
    }

    /// <summary>Sets the animation, sound, hit particles and trail.</summary>
    public void SetPresentation(AnimationClip clip, AudioClip sound = null, ParticleSystem particles = null, GameObject trail = null)
    {
        animationClip = clip;
        attackSound = sound;
        attackParticles = particles;
        trailEffect = trail;
    }

    public float GetTotalDuration()
    {
        return (startupFrames + activeFrames + recoveryFrames) / Mathf.Max(0.01f, animationSpeed);
    }

    public bool IsInActiveFrames(float normalizedTime)
    {
        float total = Mathf.Max(0.0001f, startupFrames + activeFrames + recoveryFrames);
        return normalizedTime >= startupFrames / total && normalizedTime <= (startupFrames + activeFrames) / total;
    }

    public bool IsInRecoveryFrames(float normalizedTime)
    {
        float total = Mathf.Max(0.0001f, startupFrames + activeFrames + recoveryFrames);
        return normalizedTime >= (startupFrames + activeFrames) / total;
    }

    /// <summary>True when the classic Effects deal health damage to enemies (Auto damage mode then uses them only).</summary>
    public bool EffectsDealDamage()
    {
        if (effects == null)
            return false;
        foreach (AttackActionEffect e in effects)
            if (e != null && e.enemyEffect && e.effectType == AttackEffectType.Hp && (e.amount < 0f || (e.randomAmount && e.minAmount < 0f)))
                return true;
        return false;
    }

    /// <summary>Does this attack deal the weapon's damage?</summary>
    public bool DealsWeaponDamage =>
        damageMode == WeaponDamageMode.WeaponDamage || (damageMode == WeaponDamageMode.Auto && !EffectsDealDamage());

    /// <summary>Tooltip lines: timing, cost, damage and what it does on hit.</summary>
    public virtual void Describe(List<string> lines)
    {
        lines.Add($"{DisplayName}: {GetTotalDuration():0.##}s" + (staminaCost > 0f ? $", {staminaCost:0.#} stamina" : ""));
        if (DealsWeaponDamage && !Mathf.Approximately(damageMultiplier, 1f))
            lines.Add($"  {damageMultiplier * 100f:0}% weapon damage");
        if (charge != null && charge.enabled)
            lines.Add($"  Hold to charge (up to x{charge.damageAtFullCharge:0.#} damage in {charge.maxChargeTime:0.#}s)");
        string onHit = EquipmentProcs.DescribeEffects(onHitEffects);
        if (!string.IsNullOrEmpty(onHit))
            lines.Add($"  On hit: {onHit}");
        if (behaviours != null)
            foreach (AttackBehaviour b in behaviours)
                if (b != null) lines.Add($"  {b.Describe()}");
    }

    /// <summary>Configuration problems (inspector and validation window).</summary>
    public virtual void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (animationSpeed <= 0f)
            errors.Add($"{owner}: Animation Speed must be above 0.");
        if (startupFrames < 0f || activeFrames < 0f || recoveryFrames < 0f)
            errors.Add($"{owner}: timings cannot be negative.");
        if (activeFrames <= 0f && (behaviours == null || behaviours.Count == 0))
            warnings.Add($"{owner}: Active Frames is 0, so the attack never hits.");
        if (hitShape != null && (hitDetection == HitDetectionMode.HitShape || hitDetection == HitDetectionMode.Auto))
        {
            var e = new List<string>();
            hitShape.Validate(owner + " ▸ Hit Shape", e);
            warnings.AddRange(e);
        }
        if (hitFilter == TargetFilter.None && hitDetection != HitDetectionMode.None)
            errors.Add($"{owner}: Hit Filter is empty, the attack can hit nobody.");
        impact?.Validate(owner, errors, warnings);
        if (charge != null && charge.enabled && charge.maxChargeTime < charge.minChargeTime)
            errors.Add($"{owner}: the full charge time is shorter than the minimum charge time.");
        if (onHitEffects != null)
            foreach (AbilityEffect e in onHitEffects)
            {
                if (e == null) warnings.Add($"{owner}: an On Hit Effect has no type.");
                else e.Validate(owner, errors, warnings);
            }
        if (behaviours != null)
            foreach (AttackBehaviour b in behaviours)
            {
                if (b == null) warnings.Add($"{owner}: a behaviour has no type.");
                else b.Validate(owner, errors, warnings);
            }
    }
}

/// <summary>Where one hit volume is this frame (world space).</summary>
public struct VolumePose
{
    /// <summary>Capsule: one end. Sphere / Box: the centre.</summary>
    public Vector3 a;
    /// <summary>Capsule: the other end (= a for spheres and boxes).</summary>
    public Vector3 b;
    /// <summary>Box orientation.</summary>
    public Quaternion rotation;

    public static VolumePose Lerp(in VolumePose from, in VolumePose to, float t) => new VolumePose
    {
        a = Vector3.Lerp(from.a, to.a, t),
        b = Vector3.Lerp(from.b, to.b, t),
        rotation = Quaternion.Slerp(from.rotation, to.rotation, t),
    };
}

/// <summary>
/// One part of a weapon that hits (Weapon Blade hit detection): a capsule along the model (a blade, a handle, a spear
/// shaft), a sphere (a mace or flail head, a pommel) or a box (an axe or hammer head, a shield). It follows the model
/// in the hand through the animation and is swept between frames. Placed with marker children of the model (exact on
/// any model) or with values relative to the model's pivot (the grip).
/// </summary>
[Serializable]
public class WeaponBlade
{
    public enum Axis { X, Y, Z, NegativeX, NegativeY, NegativeZ }
    public enum VolumeShape { Capsule, Sphere, Box }

    [Tooltip("Name attacks use to pick this volume (Blade, Head, Handle, Spike, Pommel, Shield...).")]
    public string name = "Blade";
    [Tooltip("Capsule: a long part (blade, shaft, handle). Sphere: a round part (mace head, flail ball, pommel). Box: a block (axe or hammer head, shield).")]
    public VolumeShape shape = VolumeShape.Capsule;

    [Tooltip("Capsule: direction of the part in the model (handle → tip). Look at the model's gizmo in the prefab: most swords and hammers point along Y or Z.")]
    public Axis axis = Axis.Y;
    [Tooltip("Capsule: where the part starts, in metres from the model's pivot (the grip) along the axis.")]
    public float start = 0.1f;
    [Tooltip("Capsule: where it ends (the tip), in metres from the pivot along the axis.")]
    public float end = 1f;
    [Tooltip("Capsule / Sphere: thickness (radius, metres). Charge scales it.")]
    [Min(0.01f)] public float radius = 0.2f;

    [Tooltip("Sphere / Box: centre in the model's local axes, in metres from the pivot (the grip).")]
    public Vector3 center = new Vector3(0f, 0.9f, 0f);
    [Tooltip("Box: size in metres (x, y, z of the box). Charge scales it.")]
    public Vector3 size = new Vector3(0.3f, 0.25f, 0.45f);
    [Tooltip("Box: rotation relative to the model (degrees).")]
    public Vector3 rotation = Vector3.zero;

    [Tooltip("Optional: an empty child of the weapon model. Capsule: marks the start. Sphere / Box: marks the centre (a box also takes its rotation). Found by name; when it exists it replaces the values above.")]
    public string startMarker = "BladeStart";
    [Tooltip("Optional, capsules: an empty child marking the tip.")]
    public string endMarker = "BladeEnd";

    public Vector3 AxisVector
    {
        get
        {
            switch (axis)
            {
                case Axis.X: return Vector3.right;
                case Axis.Z: return Vector3.forward;
                case Axis.NegativeX: return Vector3.left;
                case Axis.NegativeY: return Vector3.down;
                case Axis.NegativeZ: return Vector3.back;
                default: return Vector3.up;
            }
        }
    }

    /// <summary>Where the volume is now on <paramref name="model"/> (with its markers, when found).</summary>
    public VolumePose GetPose(Transform model, Transform startMark, Transform endMark)
    {
        var pose = new VolumePose { rotation = model.rotation };
        switch (shape)
        {
            case VolumeShape.Sphere:
                pose.a = pose.b = startMark != null ? startMark.position : model.position + model.rotation * center;
                break;
            case VolumeShape.Box:
                if (startMark != null)
                {
                    pose.a = pose.b = startMark.position;
                    pose.rotation = startMark.rotation;
                }
                else
                {
                    pose.a = pose.b = model.position + model.rotation * center;
                    pose.rotation = model.rotation * Quaternion.Euler(rotation);
                }
                break;
            default:
                GetSegment(model, startMark, endMark, out pose.a, out pose.b);
                break;
        }
        return pose;
    }

    /// <summary>World segment of a capsule volume for a model (and its markers, when found).</summary>
    public void GetSegment(Transform model, Transform startMark, Transform endMark, out Vector3 a, out Vector3 b)
    {
        if (startMark != null && endMark != null)
        {
            a = startMark.position;
            b = endMark.position;
            return;
        }
        Vector3 dir = model.rotation * AxisVector;
        if (endMark != null)
        {
            b = endMark.position;
            Vector3 d = b - model.position;
            if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            a = model.position + dir * start;
            return;
        }
        a = model.position + dir * start;
        b = model.position + dir * end;
    }

    /// <summary>Box half extents with a scale (charge).</summary>
    public Vector3 HalfExtents(float scale) => size * (0.5f * Mathf.Max(0.01f, scale));

    /// <summary>The lowest point of the volume (what touches the ground first).</summary>
    public Vector3 LowestPoint(in VolumePose pose, float scale)
    {
        switch (shape)
        {
            case VolumeShape.Sphere:
                return pose.a + Vector3.down * radius * scale;
            case VolumeShape.Box:
            {
                Vector3 h = HalfExtents(scale);
                Vector3 up = Quaternion.Inverse(pose.rotation) * Vector3.up;
                float drop = Mathf.Abs(up.x) * h.x + Mathf.Abs(up.y) * h.y + Mathf.Abs(up.z) * h.z;
                return pose.a + Vector3.down * drop;
            }
            default:
                return (pose.a.y < pose.b.y ? pose.a : pose.b) + Vector3.down * radius * scale;
        }
    }

    /// <summary>
    /// Does the volume touch a character's body (a vertical capsule from <paramref name="bodyBottom"/> to
    /// <paramref name="bodyTop"/> of radius <paramref name="bodyRadius"/>)? <paramref name="point"/> = where.
    /// </summary>
    public bool Touches(in VolumePose pose, float scale, Vector3 bodyBottom, Vector3 bodyTop, float bodyRadius, out Vector3 point)
    {
        switch (shape)
        {
            case VolumeShape.Sphere:
            {
                Vector3 onBody = ClosestOnSegment(bodyBottom, bodyTop, pose.a);
                float reach = radius * scale + bodyRadius;
                point = pose.a + Vector3.ClampMagnitude(onBody - pose.a, radius * scale);
                return (onBody - pose.a).sqrMagnitude <= reach * reach;
            }
            case VolumeShape.Box:
            {
                // Closest box point to a few points along the body (exact enough for character-sized bodies).
                Vector3 h = HalfExtents(scale);
                Quaternion inv = Quaternion.Inverse(pose.rotation);
                float best = float.MaxValue;
                point = pose.a;
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 p = Vector3.Lerp(bodyBottom, bodyTop, i / 4f);
                    Vector3 local = inv * (p - pose.a);
                    Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -h.x, h.x), Mathf.Clamp(local.y, -h.y, h.y), Mathf.Clamp(local.z, -h.z, h.z));
                    float d = (local - clamped).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        point = pose.a + pose.rotation * clamped;
                    }
                }
                return best <= bodyRadius * bodyRadius;
            }
            default:
            {
                ClosestPoints(pose.a, pose.b, bodyBottom, bodyTop, out Vector3 onBlade, out Vector3 onBody);
                float reach = radius * scale + bodyRadius;
                point = onBlade;
                return (onBlade - onBody).sqrMagnitude <= reach * reach;
            }
        }
    }

    /// <summary>Colliders inside the volume (props, trees, rocks). Returns the count written to <paramref name="buffer"/>.</summary>
    public int Overlap(in VolumePose pose, float scale, Collider[] buffer)
    {
        switch (shape)
        {
            case VolumeShape.Sphere:
                return Physics.OverlapSphereNonAlloc(pose.a, radius * scale, buffer, ~0, QueryTriggerInteraction.Collide);
            case VolumeShape.Box:
                return Physics.OverlapBoxNonAlloc(pose.a, HalfExtents(scale), buffer, pose.rotation, ~0, QueryTriggerInteraction.Collide);
            default:
                return Physics.OverlapCapsuleNonAlloc(pose.a, pose.b, radius * scale, buffer, ~0, QueryTriggerInteraction.Collide);
        }
    }

    /// <summary>Largest distance from the grip the volume reaches (for range estimates).</summary>
    public float Reach(float scale = 1f)
    {
        switch (shape)
        {
            case VolumeShape.Sphere: return center.magnitude + radius * scale;
            case VolumeShape.Box: return center.magnitude + HalfExtents(scale).magnitude;
            default: return Mathf.Max(Mathf.Abs(start), Mathf.Abs(end)) + radius * scale;
        }
    }

    public static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 1e-8f) return a;
        return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
    }

    /// <summary>Closest points between segments p1-q1 and p2-q2.</summary>
    public static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, rr = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, rr);
        float s, t;
        if (a <= 1e-6f && e <= 1e-6f) { c1 = p1; c2 = p2; return; }
        if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, rr);
            if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }

    /// <summary>A descendant of <paramref name="root"/> with this name, or null.</summary>
    public static Transform FindMarker(Transform root, string markerName)
    {
        if (root == null || string.IsNullOrEmpty(markerName)) return null;
        if (root.name == markerName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = FindMarker(root.GetChild(i), markerName);
            if (t != null) return t;
        }
        return null;
    }

    public string Describe()
    {
        string n = string.IsNullOrEmpty(name) ? "" : name + ": ";
        switch (shape)
        {
            case VolumeShape.Sphere: return $"{n}sphere r{radius:0.##} m";
            case VolumeShape.Box: return $"{n}box {size.x:0.##}×{size.y:0.##}×{size.z:0.##} m";
            default: return $"{n}{Mathf.Abs(end - start):0.##} m capsule, r{radius:0.##} m";
        }
    }
}
