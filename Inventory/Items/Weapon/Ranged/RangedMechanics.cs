using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a weapon's projectiles fly and land.</summary>
[Serializable]
public class ProjectileSettings
{
    [Tooltip("The projectile's model (arrow, bolt, tracer) - its forward axis points along the flight. Empty: the ammo's model, " +
             "for thrown weapons the weapon's own prefab, else Combat Settings' default projectile.")]
    public GameObject prefab;
    [Tooltip("Collision radius (metres).")]
    [Min(0.005f)] public float radius = 0.04f;
    [Tooltip("Downward acceleration (m/s²): arrows ~9.8, bolts ~5, bullets 0.")]
    [Min(0f)] public float gravity = 9.81f;
    [Tooltip("Flight distance before it disappears (metres).")]
    [Min(1f)] public float maxDistance = 90f;
    [Tooltip("Characters it passes through before stopping (0 = stops at the first).")]
    [Min(0)] public int pierce = 0;
    [Tooltip("Damage kept after each character it passed through.")]
    [Range(0f, 1f)] public float pierceFalloff = 0.7f;
    [Tooltip("Damage multiplier at Max Distance (falls off linearly with the distance flown). 1 = no falloff; firearms ~0.5.")]
    [Range(0f, 1f)] public float damageAtMaxDistance = 1f;
    [Tooltip("Stick into what it hits (arrows, knives) instead of vanishing.")]
    public bool stickInSurfaces = true;
    [Tooltip("Seconds a stuck projectile stays visible.")]
    [Min(0f)] public float stuckLifetime = 12f;
    [Tooltip("Chance (0-1) that a stuck projectile can be picked up again: its ammo item, or the thrown weapon itself.")]
    [Range(0f, 1f)] public float recoverChance = 0.5f;
    [Tooltip("Explosion radius when it stops (fire bombs, grenades). 0 = only what it hits directly.")]
    [Min(0f)] public float explosionRadius = 0f;
    [Tooltip("Explosion damage as a fraction of the shot's damage (at the centre; half at the edge).")]
    [Range(0f, 2f)] public float explosionDamage = 0.75f;
    [Tooltip("Seconds before it explodes on its own (a grenade's fuse; it bounces until then). 0 = on impact.")]
    [Min(0f)] public float fuseTime = 0f;
    [Tooltip("Face the flight direction every frame (arrows). Off for tumbling thrown objects.")]
    public bool alignToVelocity = true;
    [Tooltip("Spin around its side axis while flying (degrees per second): thrown axes, knives.")]
    public float spin = 0f;
    [Tooltip("Spawned where it hits.")]
    public GameObject impactVfx;
    public AudioClip impactSound;

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (fuseTime > 0f && explosionRadius <= 0f)
            warnings.Add($"{owner}: a fuse time without an explosion radius only delays the projectile vanishing.");
        if (recoverChance > 0f && !stickInSurfaces)
            warnings.Add($"{owner}: Recover Chance needs Stick In Surfaces (only stuck projectiles can be picked up).");
    }
}

/// <summary>Which ammo a ranged weapon uses.</summary>
[Serializable]
public class AmmoRequirement
{
    [Tooltip("Ammo items (Item ▸ Ammo) whose Ammo Type matches are fired: Arrow, Bolt, Bullet, Shell, Dart... Empty = no ammo " +
             "needed (a wand, an enchanted bow).")]
    public string ammoType = "Arrow";
    [Tooltip("Ammo spent per shot.")]
    [Min(0)] public int perShot = 1;
    [Tooltip("Never spends ammo (debug, special weapons). Ammo items still change the shot when the character has some.")]
    public bool infinite = false;

    public bool Needed => !infinite && perShot > 0 && !string.IsNullOrWhiteSpace(ammoType);

    public bool Accepts(AmmoSO ammo) =>
        ammo != null && !string.IsNullOrWhiteSpace(ammoType) && string.Equals(ammo.AmmoType?.Trim(), ammoType.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>How a bow (or any drawn weapon) reacts to a tap and to holding. Serialized as numbers: append only.</summary>
public enum DrawStyle
{
    /// <summary>Hold to draw; a release before the minimum draw is cancelled (or a weak shot). No quick shots.</summary>
    HoldToDraw,
    /// <summary>A tap fires a quick shot; holding draws a stronger shot, up to a full draw.</summary>
    QuickOrDrawn,
    /// <summary>Fires on press, no drawing (quick shots only; optionally repeating while held).</summary>
    QuickOnly,
}

/// <summary>What a shot can cost besides ammo.</summary>
public enum ShotResource
{
    Stamina,
    Mana,
    Health,
    /// <summary>Units of any item in the inventory (a mana crystal, gunpowder, a spell scroll...).</summary>
    Item,
}

/// <summary>One cost of a shot: stamina, mana, health or units of an item. Without enough of it the weapon cannot fire.</summary>
[Serializable]
public class ShotCost
{
    [Tooltip("What the shot spends.")]
    public ShotResource resource = ShotResource.Mana;
    [Tooltip("How much per shot (units for an item).")]
    [Min(0f)] public float amount = 5f;
    [Tooltip("The item spent (Resource = Item).")]
    public ItemSO item;

    public string Describe() => resource == ShotResource.Item
        ? $"{Mathf.Max(1, Mathf.RoundToInt(amount))} × {(item != null ? item.Name : "(no item)")} per shot"
        : $"{amount:0.#} {resource.ToString().ToLowerInvariant()} per shot";
}

/// <summary>What one shot is like (computed by a mechanic from the draw / charge).</summary>
public struct RangedShot
{
    /// <summary>Multiplies the weapon damage.</summary>
    public float damageMultiplier;
    /// <summary>Launch speed (m/s).</summary>
    public float velocity;
    /// <summary>Inaccuracy: random deviation inside a cone of this half-angle (degrees).</summary>
    public float spread;
    /// <summary>Projectiles per shot (shotgun pellets).</summary>
    public int projectiles;
    /// <summary>Extra upward angle (degrees): throws are lobbed.</summary>
    public float loft;
    /// <summary>Stamina spent by the shot (on top of the attack's cost).</summary>
    public float stamina;
}

/// <summary>Input to <see cref="RangedMechanic.Shot"/>.</summary>
public struct RangedShotInput
{
    /// <summary>Seconds the attack input was held (the draw).</summary>
    public float heldSeconds;
    /// <summary>The attack's charge ratio (0-1).</summary>
    public float chargeRatio;
    /// <summary>Extra spread from firing in a row (firearm bloom).</summary>
    public float bloom;
}

/// <summary>
/// How a ranged weapon shoots: a bow's draw and release, a crossbow's or firearm's magazine and reload, a throw. Set on the
/// weapon (Ranged Weapon); the weapon's attacks keep their timing, animation, stamina, on-hit effects and behaviours, and
/// the attacks listed in Firing Attacks fire a projectile at the moment they strike. New mechanics: derive from this class.
/// </summary>
[Serializable]
public abstract class RangedMechanic
{
    [Tooltip("Attacks (by input) that fire a projectile; the others stay melee (e.g. a bow bash on Alternate).")]
    public List<AttackType> firingAttacks = new List<AttackType> { AttackType.Normal, AttackType.Heavy };
    [Tooltip("How the projectile flies and lands.")]
    public ProjectileSettings projectile = new ProjectileSettings();
    [Tooltip("The ammo it uses.")]
    public AmmoRequirement ammo = new AmmoRequirement();
    [Tooltip("A child of the weapon model where projectiles start (e.g. Muzzle, ArrowRest). Not found = the weapon hand.")]
    public string muzzleMarker = "Muzzle";
    [Tooltip("How far the crosshair looks for what it aims at (metres).")]
    [Min(1f)] public float aimDistance = 120f;
    [Tooltip("Projectiles fly toward the crosshair (players). Off = straight ahead of the character.")]
    public bool aimAtCrosshair = true;
    [Tooltip("Played when a shot fires.")]
    public AudioClip fireSound;
    [Tooltip("Spawned at the muzzle when a shot fires.")]
    public GameObject muzzleVfx;
    [Tooltip("Played when a shot cannot fire (no ammo, empty magazine).")]
    public AudioClip emptySound;
    [Tooltip("Other things each shot spends, besides ammo and the mechanic's stamina: mana (a magic bow), health (a blood " +
             "weapon), more stamina, or units of an item (a mana crystal, gunpowder). Without enough, the weapon cannot fire. " +
             "For a weapon that needs no ammo at all, leave Ammo ▸ Ammo Type empty.")]
    public List<ShotCost> extraCosts = new List<ShotCost>();

    public string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Does an attack of this action type fire?</summary>
    public bool Fires(AttackType actionType) => firingAttacks == null || firingAttacks.Count == 0 || firingAttacks.Contains(actionType);

    /// <summary>Holds rounds that must be reloaded (crossbows, firearms).</summary>
    public virtual bool UsesMagazine => false;
    public virtual int MagazineSize => 0;
    /// <summary>The weapon item itself is thrown (one unit leaves the stack).</summary>
    public virtual bool ConsumesWeaponItem => false;
    /// <summary>Keeps firing while the input is held.</summary>
    public virtual bool Automatic => false;
    /// <summary>Minimum seconds between two shots.</summary>
    public virtual float FireInterval => 0f;

    /// <summary>The charge (draw, throw power) of firing attacks; null = the attack's own Charge settings.</summary>
    public virtual ChargeSettings OverrideCharge(ChargeSettings attackCharge) => null;

    /// <summary>The shot for this draw / charge.</summary>
    public abstract RangedShot Shot(in RangedShotInput input);

    public virtual void Describe(List<string> lines)
    {
        if (ammo != null && ammo.Needed)
            lines.Add($"Uses {ammo.ammoType} ammo ({ammo.perShot} per shot)");
        else
            lines.Add("No ammo needed");
        if (extraCosts != null)
            foreach (ShotCost c in extraCosts)
                if (c != null && c.amount > 0f)
                    lines.Add("Costs " + c.Describe());
    }

    public virtual void Validate(string owner, List<string> errors, List<string> warnings)
    {
        projectile?.Validate(owner, errors, warnings);
        if (firingAttacks != null && firingAttacks.Count == 0)
            warnings.Add($"{owner}: Firing Attacks is empty: every attack fires.");
        if (extraCosts != null)
            foreach (ShotCost c in extraCosts)
                if (c != null && c.resource == ShotResource.Item && c.item == null && c.amount > 0f)
                    errors.Add($"{owner}: an Extra Cost spends an Item but no item is set.");
    }
}

[Serializable, AbilityMenu("Ranged/Bow (draw and release)", "Hold to draw (strength builds over time), release to loose. Weak draws deal less damage, fly slower and spread more; holding a full draw too long makes the aim shake.", 0)]
public class DrawMechanic : RangedMechanic
{
    [Header("Draw")]
    [Tooltip("Hold To Draw: hold to draw, release to loose (no quick shots). Quick Or Drawn: a tap looses a quick shot, " +
             "holding draws a stronger one. Quick Only: fires on press, no drawing (rapid shots).")]
    public DrawStyle style = DrawStyle.HoldToDraw;
    [Tooltip("Seconds to reach a full draw.")]
    [Min(0.05f)] public float drawTime = 1f;
    [Tooltip("Least draw (0-1 of a full draw) that looses an arrow.")]
    [Range(0f, 1f)] public float minEffectiveDraw = 0.3f;
    [Tooltip("Releasing below the minimum still looses a weak arrow (off = the shot is cancelled and nothing is spent).")]
    public bool releaseBelowMinimum = false;
    [Tooltip("How strength grows with the draw (x = draw 0-1, y = strength 0-1).")]
    public AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Quick Shot (Quick Or Drawn, Quick Only)")]
    [Tooltip("Quick Or Drawn: released before this many seconds = a quick shot; held longer = a drawn shot.")]
    [Min(0f)] public float quickShotTime = 0.2f;
    [Tooltip("Damage multiplier of a quick shot.")]
    [Min(0f)] public float quickDamage = 0.6f;
    [Tooltip("Speed of a quick shot (m/s): faster flies farther.")]
    [Min(0.1f)] public float quickVelocity = 30f;
    [Tooltip("Spread of a quick shot (degrees).")]
    [Range(0f, 30f)] public float quickSpread = 3f;
    [Tooltip("Least seconds between two shots (Quick Or Drawn, Quick Only).")]
    [Min(0f)] public float quickFireInterval = 0.35f;
    [Tooltip("Quick Only: keep shooting while the input is held.")]
    public bool repeatWhileHeld = false;

    [Header("Strength")]
    [Tooltip("Damage multiplier at the weakest draw (Quick Or Drawn: a drawn shot grows from the quick shot instead).")]
    [Min(0f)] public float minDamage = 0.35f;
    [Tooltip("Damage multiplier at a full draw.")]
    [Min(0f)] public float maxDamage = 1.5f;
    [Tooltip("Arrow speed at the weakest draw (m/s).")]
    [Min(0.1f)] public float minVelocity = 14f;
    [Tooltip("Arrow speed at a full draw (m/s).")]
    [Min(0.1f)] public float maxVelocity = 55f;

    [Header("Accuracy")]
    [Tooltip("Spread (degrees) at the weakest draw.")]
    [Range(0f, 30f)] public float spreadAtMinDraw = 6f;
    [Tooltip("Spread (degrees) at a full draw.")]
    [Range(0f, 30f)] public float spreadAtFullDraw = 0.4f;
    [Tooltip("Seconds a full draw can be held steady; after that the aim shakes more and more.")]
    [Min(0f)] public float steadyHoldTime = 3f;
    [Tooltip("Extra spread per second of holding past the steady time (degrees).")]
    [Min(0f)] public float shakePerSecond = 2f;
    [Tooltip("Most extra spread from shaking (degrees).")]
    [Range(0f, 30f)] public float maxShake = 6f;

    [Header("Cost and Movement")]
    [Tooltip("Stamina per arrow loosed.")]
    [Min(0f)] public float staminaPerShot = 3f;
    [Tooltip("Stamina per second while holding a draw.")]
    [Min(0f)] public float staminaWhileDrawing = 1.5f;
    [Tooltip("Movement speed while drawing.")]
    [Range(0f, 1f)] public float moveSpeedWhileDrawing = 0.45f;
    [Tooltip("Animation while drawing (empty = the attack's charge animation).")]
    public AnimationClip drawAnimation;
    [Tooltip("Played when the draw starts.")]
    public AudioClip drawSound;

    public DrawMechanic()
    {
        ammo.ammoType = "Arrow";
        projectile.gravity = 9.81f;
        projectile.recoverChance = 0.5f;
        muzzleMarker = "ArrowRest";
    }

    public override float FireInterval => style == DrawStyle.HoldToDraw ? 0f : quickFireInterval;
    public override bool Automatic => style == DrawStyle.QuickOnly && repeatWhileHeld;

    public override ChargeSettings OverrideCharge(ChargeSettings attackCharge) => style == DrawStyle.QuickOnly
        ? new ChargeSettings { enabled = false } // fires on press
        : new ChargeSettings
        {
            enabled = true,
            minChargeTime = style == DrawStyle.QuickOrDrawn ? 0f : drawTime * minEffectiveDraw,
            maxChargeTime = Mathf.Max(drawTime * minEffectiveDraw + 0.05f, drawTime),
            damageAtFullCharge = 1f, // the draw decides the damage (Shot)
            areaAtFullCharge = 1f,
            earlyRelease = style == DrawStyle.QuickOrDrawn || releaseBelowMinimum ? ChargeSettings.EarlyRelease.NormalAttack : ChargeSettings.EarlyRelease.Cancel,
            autoReleaseAtFull = false,
            staminaPerSecond = staminaWhileDrawing,
            moveSpeedWhileCharging = moveSpeedWhileDrawing,
            chargeAnimation = drawAnimation != null ? drawAnimation : attackCharge?.chargeAnimation,
            chargingVfx = attackCharge?.chargingVfx,
            fullChargeVfx = attackCharge?.fullChargeVfx,
            fullChargeSound = attackCharge?.fullChargeSound,
        };

    /// <summary>The draw (0-1) after holding <paramref name="seconds"/>.</summary>
    public float Draw(float seconds) => Mathf.Clamp01(seconds / Mathf.Max(0.05f, drawTime));

    /// <summary>Is a release after <paramref name="heldSeconds"/> a quick shot?</summary>
    public bool IsQuickShot(float heldSeconds) =>
        style == DrawStyle.QuickOnly || (style == DrawStyle.QuickOrDrawn && heldSeconds < quickShotTime);

    public override RangedShot Shot(in RangedShotInput input)
    {
        if (IsQuickShot(input.heldSeconds))
            return new RangedShot
            {
                damageMultiplier = quickDamage,
                velocity = quickVelocity,
                spread = quickSpread,
                projectiles = 1,
                stamina = staminaPerShot,
            };
        // Quick Or Drawn: a drawn shot grows from the quick shot (at Quick Shot Time) to the full draw.
        bool fromQuick = style == DrawStyle.QuickOrDrawn;
        float draw = fromQuick
            ? Mathf.Clamp01((input.heldSeconds - quickShotTime) / Mathf.Max(0.05f, drawTime - quickShotTime))
            : Draw(input.heldSeconds);
        float strength = Mathf.Clamp01(strengthCurve != null && strengthCurve.length > 0 ? strengthCurve.Evaluate(draw) : draw);
        float lowDamage = fromQuick ? quickDamage : minDamage;
        float lowVelocity = fromQuick ? quickVelocity : minVelocity;
        float spread = Mathf.Lerp(fromQuick ? quickSpread : spreadAtMinDraw, spreadAtFullDraw, draw);
        float overHold = input.heldSeconds - drawTime - steadyHoldTime;
        if (overHold > 0f)
            spread += Mathf.Min(maxShake, overHold * shakePerSecond);
        return new RangedShot
        {
            damageMultiplier = Mathf.Lerp(lowDamage, maxDamage, strength),
            velocity = Mathf.Lerp(lowVelocity, maxVelocity, strength),
            spread = spread,
            projectiles = 1,
            stamina = staminaPerShot,
        };
    }

    public override void Describe(List<string> lines)
    {
        switch (style)
        {
            case DrawStyle.QuickOnly:
                lines.Add($"Quick shots: ×{quickDamage:0.##} damage, {quickVelocity:0} m/s, every {quickFireInterval:0.##}s" + (repeatWhileHeld ? " while held" : ""));
                break;
            case DrawStyle.QuickOrDrawn:
                lines.Add($"Tap: quick shot (×{quickDamage:0.##}, {quickVelocity:0} m/s). Hold {drawTime:0.##}s: full draw (×{maxDamage:0.##}, {maxVelocity:0} m/s)");
                break;
            default:
                lines.Add($"Bow: draw {drawTime:0.##}s (min {minEffectiveDraw * 100f:0}%), damage ×{minDamage:0.##}-{maxDamage:0.##}, {minVelocity:0}-{maxVelocity:0} m/s");
                break;
        }
        base.Describe(lines);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (maxDamage < minDamage) warnings.Add($"{owner}: Max Damage is below Min Damage (a full draw deals less).");
        if (maxVelocity < minVelocity) warnings.Add($"{owner}: Max Velocity is below Min Velocity.");
    }
}

[Serializable, AbilityMenu("Ranged/Magazine (crossbow, firearm)", "Fires loaded rounds; reload when empty. Crossbows: magazine 1, slow reload. Pistols, rifles: bigger magazines, fire interval, automatic fire, pellets, bloom.", 1)]
public class MagazineMechanic : RangedMechanic
{
    [Header("Magazine")]
    [Tooltip("Rounds loaded at once (crossbow 1, pistol 8, rifle 30).")]
    [Min(1)] public int magazineSize = 1;
    [Tooltip("Seconds to reload.")]
    [Min(0f)] public float reloadTime = 1.6f;
    [Tooltip("Reload one round at a time (shotgun shells, a revolver): each Reload Time loads one; firing interrupts.")]
    public bool reloadOneAtATime = false;
    [Tooltip("Reload by itself when firing with an empty magazine (if there is ammo).")]
    public bool autoReloadWhenEmpty = true;
    [Tooltip("A newly obtained weapon is loaded.")]
    public bool startsLoaded = true;
    [Tooltip("Movement speed while reloading.")]
    [Range(0f, 1f)] public float moveSpeedWhileReloading = 0.6f;
    [Tooltip("Played on the character while reloading.")]
    public AnimationClip reloadAnimation;
    public AudioClip reloadSound;

    [Header("Firing")]
    [Tooltip("Least seconds between two shots.")]
    [Min(0.01f)] public float fireInterval = 0.25f;
    [Tooltip("Keeps firing while the input is held.")]
    public bool automatic = false;
    [Tooltip("Damage multiplier of each projectile.")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Projectile speed (m/s).")]
    [Min(0.1f)] public float velocity = 70f;
    [Tooltip("Projectiles per shot (shotgun pellets, each with its own spread).")]
    [Min(1)] public int projectilesPerShot = 1;
    [Tooltip("Base spread (degrees).")]
    [Range(0f, 30f)] public float spread = 0.6f;
    [Tooltip("Extra spread added by each shot fired in a row (recoil bloom, degrees).")]
    [Range(0f, 10f)] public float bloomPerShot = 0f;
    [Tooltip("Bloom recovered per second when not firing (degrees).")]
    [Min(0f)] public float bloomRecovery = 6f;
    [Tooltip("Most bloom (degrees).")]
    [Range(0f, 30f)] public float maxBloom = 6f;
    [Tooltip("Stamina per shot (heavy crossbows).")]
    [Min(0f)] public float staminaPerShot = 0f;

    public MagazineMechanic()
    {
        ammo.ammoType = "Bolt";
        projectile.gravity = 4f;
        projectile.recoverChance = 0.4f;
    }

    public override bool UsesMagazine => true;
    public override int MagazineSize => Mathf.Max(1, magazineSize);
    public override bool Automatic => automatic;
    public override float FireInterval => fireInterval;

    public override RangedShot Shot(in RangedShotInput input) => new RangedShot
    {
        damageMultiplier = damageMultiplier,
        velocity = velocity,
        spread = spread + Mathf.Min(maxBloom, input.bloom),
        projectiles = Mathf.Max(1, projectilesPerShot),
        stamina = staminaPerShot,
    };

    public override void Describe(List<string> lines)
    {
        lines.Add($"Magazine {magazineSize}, reload {reloadTime:0.##}s, {1f / Mathf.Max(0.01f, fireInterval):0.#} shots/s" +
                  (automatic ? " (automatic)" : "") + (projectilesPerShot > 1 ? $", {projectilesPerShot} pellets" : ""));
        base.Describe(lines);
    }
}

[Serializable, AbilityMenu("Ranged/Throw (knives, javelins, grenades)", "The weapon itself is thrown (one leaves the stack): hold to throw harder. Recover it where it lands.", 2)]
public class ThrowMechanic : RangedMechanic
{
    [Tooltip("Hold the input to throw harder (off = always the full throw).")]
    public bool chargeable = true;
    [Tooltip("Seconds of holding for the strongest throw.")]
    [Min(0.05f)] public float fullThrowTime = 0.8f;
    [Tooltip("Speed of the weakest / strongest throw (m/s).")]
    [Min(0.1f)] public float minVelocity = 10f;
    [Min(0.1f)] public float maxVelocity = 24f;
    [Tooltip("Damage multiplier of the weakest / strongest throw.")]
    [Min(0f)] public float minDamage = 0.7f;
    [Min(0f)] public float maxDamage = 1.3f;
    [Tooltip("Upward angle added to the aim (degrees): throws are lobbed a little.")]
    [Range(0f, 45f)] public float loft = 6f;
    [Tooltip("Spread (degrees).")]
    [Range(0f, 30f)] public float spread = 1.5f;
    [Tooltip("One unit of the weapon's stack is thrown (off = the weapon stays in hand, e.g. a sling throwing stones as ammo).")]
    public bool consumesItem = true;
    [Tooltip("Stamina per throw.")]
    [Min(0f)] public float staminaPerThrow = 4f;

    public ThrowMechanic()
    {
        ammo.ammoType = "";
        projectile.gravity = 9.81f;
        projectile.recoverChance = 1f;
        projectile.spin = 720f;
        projectile.alignToVelocity = false;
        firingAttacks = new List<AttackType> { AttackType.Normal };
    }

    public override bool ConsumesWeaponItem => consumesItem;

    public override ChargeSettings OverrideCharge(ChargeSettings attackCharge) => !chargeable ? null : new ChargeSettings
    {
        enabled = true,
        minChargeTime = 0f,
        maxChargeTime = fullThrowTime,
        damageAtFullCharge = 1f,
        areaAtFullCharge = 1f,
        earlyRelease = ChargeSettings.EarlyRelease.NormalAttack,
        staminaPerSecond = 0f,
        moveSpeedWhileCharging = attackCharge != null && attackCharge.enabled ? attackCharge.moveSpeedWhileCharging : 0.7f,
        chargeAnimation = attackCharge?.chargeAnimation,
        chargingVfx = attackCharge?.chargingVfx,
        fullChargeVfx = attackCharge?.fullChargeVfx,
        fullChargeSound = attackCharge?.fullChargeSound,
    };

    public override RangedShot Shot(in RangedShotInput input)
    {
        float t = chargeable ? Mathf.Clamp01(input.heldSeconds / Mathf.Max(0.05f, fullThrowTime)) : 1f;
        return new RangedShot
        {
            damageMultiplier = Mathf.Lerp(minDamage, maxDamage, t),
            velocity = Mathf.Lerp(minVelocity, maxVelocity, t),
            spread = spread,
            projectiles = 1,
            loft = loft,
            stamina = staminaPerThrow,
        };
    }

    public override void Describe(List<string> lines)
    {
        lines.Add($"Thrown: {minVelocity:0}-{maxVelocity:0} m/s" + (chargeable ? $" (hold {fullThrowTime:0.##}s for full power)" : "") +
                  (consumesItem ? ", the weapon leaves the stack" : ""));
        base.Describe(lines);
    }
}
