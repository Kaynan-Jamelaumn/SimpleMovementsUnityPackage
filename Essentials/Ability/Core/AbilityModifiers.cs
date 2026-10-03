using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Changes applied on top of an <see cref="AbilityDefinition"/> without touching the asset: an absorbed variant
/// ("weaker, 1 projectile instead of 3"), a level bonus, a mob elite buff... Multipliers multiply, deltas add, and
/// overrides replace. Stacking several sets combines them the same way.
/// </summary>
[Serializable]
public class AbilityModifierSet
{
    [Tooltip("Optional name shown in tooltips (e.g. 'Weakened', 'Empowered').")]
    public string label = "";

    [Header("Power")]
    [Tooltip("Multiplies all damage (direct, over time and legacy HP damage effects). 1 = unchanged.")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Multiplies healing done by the ability.")]
    [Min(0f)] public float healMultiplier = 1f;
    [Tooltip("Multiplies crowd-control durations (stun, silence, root, slow, taunt).")]
    [Min(0f)] public float controlMultiplier = 1f;
    [Tooltip("Multiplies knockback and pull distances.")]
    [Min(0f)] public float displacementMultiplier = 1f;

    [Header("Timing")]
    [Tooltip("Multiplies the cooldown. 0.8 = 20% shorter.")]
    [Min(0f)] public float cooldownMultiplier = 1f;
    [Tooltip("Multiplies the cast (wind-up) time.")]
    [Min(0f)] public float castTimeMultiplier = 1f;
    [Tooltip("Multiplies lingering durations: zones, beams, barriers, summon lifetime.")]
    [Min(0f)] public float durationMultiplier = 1f;

    [Header("Reach")]
    [Tooltip("Multiplies targeting range and projectile travel distance.")]
    [Min(0f)] public float rangeMultiplier = 1f;
    [Tooltip("Multiplies the size of every hit shape (radius, width, length).")]
    [Min(0f)] public float areaMultiplier = 1f;
    [Tooltip("Multiplies projectile speed.")]
    [Min(0f)] public float projectileSpeedMultiplier = 1f;

    [Header("Counts")]
    [Tooltip("Added to the number of projectiles per volley (can be negative; at least 1 remains).")]
    public int projectileCountDelta = 0;
    [Tooltip("Replaces the number of projectiles per volley. -1 = keep the ability's number. E.g. a mob fires 3 bullets, the absorbed copy fires 1.")]
    public int projectileCountOverride = -1;
    [Tooltip("Added to the number of volleys (can be negative; at least 1 remains).")]
    public int volleyCountDelta = 0;
    [Tooltip("Added to how many targets a projectile passes through.")]
    public int pierceDelta = 0;
    [Tooltip("Added to the number of summoned creatures.")]
    public int summonCountDelta = 0;
    [Tooltip("Added to the number of charges.")]
    public int extraCharges = 0;

    [Header("Cost")]
    [Tooltip("Multiplies mana, stamina and health costs.")]
    [Min(0f)] public float costMultiplier = 1f;
    [Tooltip("Multiplies only the mana cost (on top of Cost Multiplier). Mana Cost Reduction stats use it. 0 counts as 1.")]
    [Min(0f)] public float manaCostMultiplier = 1f;

    public static AbilityModifierSet CreateIdentity() => new AbilityModifierSet();

    public AbilityModifierSet Clone() => (AbilityModifierSet)MemberwiseClone();

    /// <summary>True if this set changes nothing.</summary>
    public bool IsIdentity =>
        Mathf.Approximately(damageMultiplier, 1f) && Mathf.Approximately(healMultiplier, 1f) &&
        Mathf.Approximately(controlMultiplier, 1f) && Mathf.Approximately(displacementMultiplier, 1f) &&
        Mathf.Approximately(cooldownMultiplier, 1f) && Mathf.Approximately(castTimeMultiplier, 1f) &&
        Mathf.Approximately(durationMultiplier, 1f) && Mathf.Approximately(rangeMultiplier, 1f) &&
        Mathf.Approximately(areaMultiplier, 1f) && Mathf.Approximately(projectileSpeedMultiplier, 1f) &&
        Mathf.Approximately(costMultiplier, 1f) && Mathf.Approximately(manaCostMultiplier, 1f) && projectileCountDelta == 0 && projectileCountOverride < 0 &&
        volleyCountDelta == 0 && pierceDelta == 0 && summonCountDelta == 0 && extraCharges == 0;

    /// <summary>Returns a new set equal to this one followed by <paramref name="other"/>.</summary>
    public AbilityModifierSet CombinedWith(AbilityModifierSet other)
    {
        AbilityModifierSet r = Clone();
        if (other == null)
            return r;
        r.label = string.IsNullOrEmpty(other.label) ? label : (string.IsNullOrEmpty(label) ? other.label : label + ", " + other.label);
        r.damageMultiplier *= other.damageMultiplier;
        r.healMultiplier *= other.healMultiplier;
        r.controlMultiplier *= other.controlMultiplier;
        r.displacementMultiplier *= other.displacementMultiplier;
        r.cooldownMultiplier *= other.cooldownMultiplier;
        r.castTimeMultiplier *= other.castTimeMultiplier;
        r.durationMultiplier *= other.durationMultiplier;
        r.rangeMultiplier *= other.rangeMultiplier;
        r.areaMultiplier *= other.areaMultiplier;
        r.projectileSpeedMultiplier *= other.projectileSpeedMultiplier;
        r.costMultiplier *= other.costMultiplier;
        r.projectileCountDelta += other.projectileCountDelta;
        if (other.projectileCountOverride >= 0)
            r.projectileCountOverride = other.projectileCountOverride;
        r.volleyCountDelta += other.volleyCountDelta;
        r.pierceDelta += other.pierceDelta;
        r.summonCountDelta += other.summonCountDelta;
        r.extraCharges += other.extraCharges;
        return r;
    }

    /// <summary>Short description of the differences, e.g. "Damage x0.7, Cooldown x1.2, 1 projectile".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        void Mul(string n, float v) { if (!Mathf.Approximately(v, 1f)) parts.Add($"{n} x{v:0.##}"); }
        Mul("Damage", damageMultiplier);
        Mul("Healing", healMultiplier);
        Mul("Control", controlMultiplier);
        Mul("Knockback", displacementMultiplier);
        Mul("Cooldown", cooldownMultiplier);
        Mul("Cast time", castTimeMultiplier);
        Mul("Duration", durationMultiplier);
        Mul("Range", rangeMultiplier);
        Mul("Area", areaMultiplier);
        Mul("Projectile speed", projectileSpeedMultiplier);
        Mul("Cost", costMultiplier);
        if (projectileCountOverride >= 0) parts.Add($"{projectileCountOverride} projectile{(projectileCountOverride == 1 ? "" : "s")}");
        if (projectileCountDelta != 0) parts.Add($"{projectileCountDelta:+#;-#} projectiles");
        if (volleyCountDelta != 0) parts.Add($"{volleyCountDelta:+#;-#} volleys");
        if (pierceDelta != 0) parts.Add($"{pierceDelta:+#;-#} pierce");
        if (summonCountDelta != 0) parts.Add($"{summonCountDelta:+#;-#} summons");
        if (extraCharges != 0) parts.Add($"{extraCharges:+#;-#} charges");
        return parts.Count == 0 ? "Unchanged" : string.Join(", ", parts);
    }
}

/// <summary>
/// The resolved numbers an ability is cast with (definition values x modifiers), computed once per cast so
/// actions and effects read plain fields.
/// </summary>
public struct AbilityStats
{
    public float damage, heal, control, displacement, cooldown, castTime, duration, range, area, projectileSpeed, cost, manaCost;
    public int projectileCountDelta, projectileCountOverride, volleyCountDelta, pierceDelta, summonCountDelta, extraCharges;

    public static AbilityStats Identity => new AbilityStats
    {
        damage = 1f,
        heal = 1f,
        control = 1f,
        displacement = 1f,
        cooldown = 1f,
        castTime = 1f,
        duration = 1f,
        range = 1f,
        area = 1f,
        projectileSpeed = 1f,
        cost = 1f,
        manaCost = 1f,
        projectileCountOverride = -1,
    };

    public static AbilityStats From(AbilityModifierSet a, AbilityModifierSet b = null)
    {
        AbilityStats s = Identity;
        s.Apply(a);
        s.Apply(b);
        return s;
    }

    public void Apply(AbilityModifierSet m)
    {
        if (m == null)
            return;
        damage *= m.damageMultiplier;
        heal *= m.healMultiplier;
        control *= m.controlMultiplier;
        displacement *= m.displacementMultiplier;
        cooldown *= m.cooldownMultiplier;
        castTime *= m.castTimeMultiplier;
        duration *= m.durationMultiplier;
        range *= m.rangeMultiplier;
        area *= m.areaMultiplier;
        projectileSpeed *= m.projectileSpeedMultiplier;
        cost *= m.costMultiplier;
        manaCost *= m.manaCostMultiplier > 0f ? m.manaCostMultiplier : 1f; // 0 = unset (older data); use Cost Multiplier 0 for free
        projectileCountDelta += m.projectileCountDelta;
        if (m.projectileCountOverride >= 0)
            projectileCountOverride = m.projectileCountOverride;
        volleyCountDelta += m.volleyCountDelta;
        pierceDelta += m.pierceDelta;
        summonCountDelta += m.summonCountDelta;
        extraCharges += m.extraCharges;
    }

    /// <summary>Projectiles per volley after modifiers (at least 1).</summary>
    public int ProjectileCount(int baseCount)
    {
        int n = projectileCountOverride >= 0 ? projectileCountOverride : baseCount;
        return Mathf.Max(1, n + projectileCountDelta);
    }

    public int VolleyCount(int baseCount) => Mathf.Max(1, baseCount + volleyCountDelta);
    public int Pierce(int basePierce) => Mathf.Max(0, basePierce + pierceDelta);
    public int SummonCount(int baseCount) => Mathf.Max(1, baseCount + summonCountDelta);
}

/// <summary>A possible absorbed form of an ability.</summary>
[Serializable]
public class AbilityVariant
{
    [Tooltip("Name shown to the player, e.g. 'Lesser Firebolt'. Empty = the ability's name with the tier.")]
    public string name = "";
    [Tooltip("How this copy compares to the original (for UI and statistics).")]
    public AbsorbTier tier = AbsorbTier.Same;
    [Tooltip("Relative chance of this variant among the variants of the ability (2 = twice as likely as 1).")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("What changes in this copy.")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();

    public AbilityVariant Clone()
    {
        var v = (AbilityVariant)MemberwiseClone();
        v.modifiers = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
        return v;
    }
}

/// <summary>Whether and how an ability can be absorbed by the player when its owner is killed.</summary>
[Serializable]
public class AbsorptionRules
{
    [Tooltip("The player can absorb this ability from a mob that has it.")]
    public bool canBeAbsorbed = true;

    [Tooltip("Chance (0-1) that a kill grants this ability, used only by casters WITHOUT an absorption table. Mobs ignore it: their MobAbilityController decides with one chance per kill and a share per ability (Absorption section).")]
    [Range(0f, 1f)] public float absorbChance = 0.2f;

    [Tooltip("Changes every absorbed copy gets before the variant, e.g. Projectile Count Override = 1 so the player always fires one bullet.")]
    public AbilityModifierSet absorbedBaseModifiers = new AbilityModifierSet();

    [Tooltip("ON: pick from the global variants (Weaker / Same / Stronger / Altered) in the Absorption Settings. OFF: use only the Custom Variants below.")]
    public bool useDefaultVariants = true;

    [Tooltip("Variants specific to this ability. When not empty they are added to (or with Use Default Variants off, replace) the global ones.")]
    public List<AbilityVariant> customVariants = new List<AbilityVariant>();

    [Tooltip("Optional: absorbing this ability grants a DIFFERENT ability instead (e.g. the mob's 'Triple Shot' becomes the player's 'Quick Shot'). Empty = the same ability.")]
    public AbilityDefinition absorbedAs;
}
