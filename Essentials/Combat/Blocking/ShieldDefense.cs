using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a shield (or a weapon guard) does while it is raised: which arc and body parts it protects, how much damage
/// it removes, what each blocked hit costs, when the guard breaks, the parry window and the shield bash. Pure data on
/// the item (<see cref="ArmorSO"/> shields, <see cref="WeaponSO"/> guards); a <see cref="BlockController"/> uses it.
/// </summary>
[Serializable]
public class ShieldDefense
{
    [Tooltip("Can this item block? Shields: on. Weapons: a guard with the weapon itself (greatsword, staff), off by default.")]
    public bool enabled = true;

    [Header("Coverage")]
    [Tooltip("Width of the protected arc in front of the defender (degrees). Buckler ~90, round shield ~120, kite ~140, tower ~170. " +
             "Hits from outside the arc (flanks, behind) are not blocked.")]
    [Range(10f, 360f)] public float coverageAngle = 120f;
    [Tooltip("Body parts the shield protects, by name (Head, Torso, Arms, Legs). Empty = every part. A small buckler leaves the " +
             "legs open; a tower shield covers everything.")]
    public List<string> coveredBodyParts = new List<string>();
    [Tooltip("Also blocks projectiles (arrows, bolts, thrown weapons, spell projectiles), not only melee hits.")]
    public bool blocksProjectiles = true;
    [Tooltip("Also blocks area damage (explosions, slams) that comes from inside the arc.")]
    public bool blocksAreaDamage = false;

    [Header("Damage Reduction (while the block is up)")]
    [Tooltip("Fraction of Physical damage removed (1 = fully negated, 0.6 = 60% less).")]
    [Range(0f, 1f)] public float physicalReduction = 1f;
    [Tooltip("Fraction of Magical damage removed.")]
    [Range(0f, 1f)] public float magicalReduction = 0.4f;
    [Tooltip("Extra fraction removed from elemental hits (fire, ice...) on top of the type's reduction (e.g. a fire-warded shield).")]
    [Range(0f, 1f)] public float elementalReduction = 0f;

    [Header("Stamina")]
    [Tooltip("Stamina spent per point of damage the shield stopped (heavy hits cost more).")]
    [Min(0f)] public float staminaPerDamage = 0.6f;
    [Tooltip("Stamina spent per blocked hit, however small.")]
    [Min(0f)] public float staminaPerBlock = 4f;
    [Tooltip("Stamina drained per second while the block is held (0 = none).")]
    [Min(0f)] public float staminaPerSecond = 0f;

    [Header("Guard Break")]
    [Tooltip("When a blocked hit costs more stamina than is left, the guard breaks: the part the stamina could not pay goes " +
             "through, and the defender is staggered.")]
    public bool guardBreakOnNoStamina = true;
    [Tooltip("A single hit stronger than this (damage before the block) breaks the guard at once (0 = only stamina breaks it).")]
    [Min(0f)] public float guardBreakDamage = 0f;
    [Tooltip("Stagger (stun) on the defender when the guard breaks (seconds).")]
    [Min(0f)] public float guardBreakStun = 1f;
    [Tooltip("Seconds before the block can be raised again after a guard break.")]
    [Min(0f)] public float guardBreakCooldown = 1.5f;

    [Header("Parry")]
    [Tooltip("Seconds after raising the block during which a MELEE hit is parried: no damage, and the attacker is staggered " +
             "(0 = this item cannot parry).")]
    [Min(0f)] public float parryWindow = 0.15f;
    [Tooltip("Stagger (stun) on the attacker of a parried hit (seconds).")]
    [Min(0f)] public float parryStun = 0.8f;
    [Tooltip("Projectiles can be parried too (deflected).")]
    public bool parryProjectiles = false;

    [Header("Handling")]
    [Tooltip("Seconds to raise the block before it protects (big shields are slower).")]
    [Min(0f)] public float raiseTime = 0.12f;
    [Tooltip("Movement speed while blocking (1 = normal speed).")]
    [Range(0f, 1f)] public float moveSpeedWhileBlocking = 0.5f;
    [Tooltip("Knockback received from blocked hits (0 = holds its ground, 1 = full knockback).")]
    [Range(0f, 1f)] public float knockbackWhileBlocking = 0.25f;
    [Tooltip("Durability the item loses per blocked hit (0 = none). A shield at 0 durability stops blocking (or breaks, by its item settings).")]
    [Min(0)] public int durabilityPerBlock = 1;

    [Header("Shield Bash (attack input while blocking)")]
    [Tooltip("Pressing attack while blocking bashes with the shield: a short push that can stagger.")]
    public bool bashEnabled = true;
    [Tooltip("Flat damage of the bash (Physical).")]
    [Min(0f)] public float bashDamage = 4f;
    [Tooltip("Reach of the bash in front of the defender (metres).")]
    [Min(0.2f)] public float bashRange = 1.8f;
    [Tooltip("Width of the bash arc (degrees).")]
    [Range(10f, 360f)] public float bashAngle = 90f;
    [Tooltip("Knockback distance (metres).")]
    [Min(0f)] public float bashKnockback = 2f;
    [Tooltip("Stagger (stun) on the characters hit (seconds).")]
    [Min(0f)] public float bashStun = 0.5f;
    [Tooltip("Stamina spent per bash.")]
    [Min(0f)] public float bashStamina = 10f;
    [Tooltip("Seconds between bashes.")]
    [Min(0.1f)] public float bashCooldown = 1.2f;

    [Header("Feedback")]
    public AudioClip blockSound;
    public AudioClip parrySound;
    public AudioClip guardBreakSound;
    public AudioClip bashSound;
    [Tooltip("Spawned where a hit is blocked (sparks, splinters).")]
    public GameObject blockVfx;
    [Tooltip("Spawned where a hit is parried.")]
    public GameObject parryVfx;

    // ------------------------------------------------------------------ rules
    /// <summary>Is a hit coming from <paramref name="incomingDirection"/> (the way the hit travels) inside the arc of a defender facing <paramref name="forward"/>?</summary>
    public bool Covers(Vector3 forward, Vector3 incomingDirection)
    {
        if (coverageAngle >= 359f)
            return true;
        Vector3 toAttacker = -incomingDirection;
        toAttacker.y = 0f;
        forward.y = 0f;
        if (toAttacker.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
            return true; // straight above / below: the shield is up
        return Vector3.Angle(forward, toAttacker) <= coverageAngle * 0.5f;
    }

    /// <summary>Does the shield protect this body part (null = unknown: protected)?</summary>
    public bool CoversPart(BodyPartDefinition part)
    {
        if (part == null || coveredBodyParts == null || coveredBodyParts.Count == 0)
            return true;
        for (int i = 0; i < coveredBodyParts.Count; i++)
            if (part.Matches(coveredBodyParts[i]))
                return true;
        return false;
    }

    /// <summary>Fraction of a hit of this type and element the block removes.</summary>
    public float Reduction(DamageType type, ElementType element)
    {
        float r;
        switch (type)
        {
            case DamageType.Physical: r = physicalReduction; break;
            case DamageType.Magical: r = magicalReduction; break;
            default: return 0f; // true damage cannot be blocked
        }
        if (element != ElementType.None && elementalReduction > 0f)
            r = 1f - (1f - r) * (1f - elementalReduction);
        return Mathf.Clamp01(r);
    }

    public void Describe(List<string> lines)
    {
        if (!enabled)
            return;
        lines.Add($"Blocks {physicalReduction * 100f:0}% physical / {magicalReduction * 100f:0}% magical in a {coverageAngle:0}° arc" +
                  (coveredBodyParts != null && coveredBodyParts.Count > 0 ? $" ({string.Join(", ", coveredBodyParts)})" : ""));
        if (parryWindow > 0f)
            lines.Add($"Parry: raise it {parryWindow:0.##}s before a hit to stagger the attacker");
        if (bashEnabled)
            lines.Add($"Bash: attack while blocking ({bashDamage:0.#} damage, {bashStun:0.#}s stagger)");
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (!enabled)
            return;
        if (physicalReduction <= 0f && magicalReduction <= 0f)
            warnings.Add($"{owner}: blocking removes no damage (Physical and Magical Reduction are 0).");
        if (staminaPerDamage <= 0f && staminaPerBlock <= 0f && guardBreakDamage <= 0f)
            warnings.Add($"{owner}: blocking costs nothing and the guard never breaks (an unbreakable wall).");
        if (parryWindow > 0.6f)
            warnings.Add($"{owner}: a parry window over 0.6 s makes almost every block a parry.");
    }

    // ------------------------------------------------------------------ presets
    /// <summary>Small, fast, narrow; great parries; the legs stay open.</summary>
    public static ShieldDefense Buckler() => new ShieldDefense
    {
        coverageAngle = 90f,
        coveredBodyParts = new List<string> { "Head", "Torso", "Arms" },
        physicalReduction = 0.7f,
        magicalReduction = 0.2f,
        staminaPerDamage = 0.8f,
        staminaPerBlock = 4f,
        raiseTime = 0.06f,
        moveSpeedWhileBlocking = 0.8f,
        knockbackWhileBlocking = 0.6f,
        parryWindow = 0.25f,
        parryStun = 1f,
        bashDamage = 3f,
        bashStun = 0.4f,
        guardBreakStun = 0.8f,
    };

    /// <summary>The all-rounder.</summary>
    public static ShieldDefense Round() => new ShieldDefense
    {
        coverageAngle = 120f,
        physicalReduction = 0.9f,
        magicalReduction = 0.35f,
        staminaPerDamage = 0.6f,
        staminaPerBlock = 4f,
        raiseTime = 0.1f,
        moveSpeedWhileBlocking = 0.6f,
        knockbackWhileBlocking = 0.35f,
        parryWindow = 0.15f,
    };

    /// <summary>Wide and solid; covers everything but the feet.</summary>
    public static ShieldDefense Kite() => new ShieldDefense
    {
        coverageAngle = 140f,
        physicalReduction = 1f,
        magicalReduction = 0.45f,
        staminaPerDamage = 0.5f,
        staminaPerBlock = 5f,
        raiseTime = 0.14f,
        moveSpeedWhileBlocking = 0.5f,
        knockbackWhileBlocking = 0.2f,
        parryWindow = 0.12f,
    };

    /// <summary>A wall: everything is covered, little stamina per hit, but slow and no parry.</summary>
    public static ShieldDefense Tower() => new ShieldDefense
    {
        coverageAngle = 170f,
        physicalReduction = 1f,
        magicalReduction = 0.6f,
        staminaPerDamage = 0.35f,
        staminaPerBlock = 6f,
        raiseTime = 0.25f,
        moveSpeedWhileBlocking = 0.3f,
        knockbackWhileBlocking = 0f,
        parryWindow = 0f,
        blocksAreaDamage = true,
        bashDamage = 6f,
        bashKnockback = 3f,
        bashStun = 0.8f,
        bashStamina = 15f,
        guardBreakStun = 1.4f,
    };

    /// <summary>Guarding with a weapon (greatsword, staff, sword): partial reduction, costly, quick parries, no bash.</summary>
    public static ShieldDefense WeaponGuard() => new ShieldDefense
    {
        enabled = true,
        coverageAngle = 100f,
        physicalReduction = 0.6f,
        magicalReduction = 0f,
        staminaPerDamage = 1f,
        staminaPerBlock = 6f,
        raiseTime = 0.08f,
        moveSpeedWhileBlocking = 0.55f,
        knockbackWhileBlocking = 0.5f,
        parryWindow = 0.2f,
        parryStun = 0.9f,
        blocksProjectiles = false,
        bashEnabled = false,
        durabilityPerBlock = 1,
    };
}
