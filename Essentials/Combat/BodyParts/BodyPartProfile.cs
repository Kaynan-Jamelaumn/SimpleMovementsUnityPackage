using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which side of the body a part is on, for hits located by height and side.</summary>
public enum BodySide
{
    /// <summary>The centre: hits anywhere across the body at this height.</summary>
    Any,
    /// <summary>Only hits on the character's left side (beyond Side Offset).</summary>
    Left,
    /// <summary>Only hits on the character's right side (beyond Side Offset).</summary>
    Right,
    /// <summary>Hits on either side (beyond Side Offset): both arms as one part.</summary>
    EitherSide,
}

/// <summary>
/// One region of a body (head, torso, arms, legs, a tail, a weak spot...): where it is, how much damage hits on it deal,
/// which armour pieces protect it, and what hits on it do (stagger, slow, sever). Lives in a <see cref="BodyPartProfile"/>.
/// </summary>
[Serializable]
public class BodyPartDefinition
{
    [Tooltip("Name used everywhere: shields' Covered Body Parts, attacks' Aimed Body Part, hitboxes. E.g. Head, Torso, Arms, Legs, Tail.")]
    public string name = "Torso";
    [Tooltip("Other names for the same part (e.g. Chest and Body for Torso). Shields and attacks may use any of them.")]
    public List<string> aliases = new List<string>();

    [Header("Location (used when no hitbox collider tells the part)")]
    [Tooltip("Height band on the body where hits land on this part: 0 = feet, 1 = top of the head (x = from, y = to).")]
    public Vector2 heightRange = new Vector2(0.45f, 0.83f);
    [Tooltip("Side of the body: Any = the centre; Left / Right / Either Side = only hits that far out from the centre line (arms).")]
    public BodySide side = BodySide.Any;
    [Tooltip("For side parts: how far out from the centre line a hit must be, as a fraction of the body radius.")]
    [Range(0f, 1f)] public float sideOffset = 0.6f;

    [Header("Damage")]
    [Tooltip("Damage multiplier of hits on this part (Head 1.6 = headshots deal 60% more; Legs 0.85).")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Armour pieces that protect this part fully: their Defense counts in full for hits here (other pieces count only by the " +
             "profile's Other Armor Share).")]
    public List<ArmorSlotType> protectedBy = new List<ArmorSlotType>();
    [Tooltip("Natural armour of this part: Defense points added for hits here (a carapace, a thick skull). Negative = a weak spot.")]
    public float bonusDefense = 0f;
    [Tooltip("Natural Magic Resistance points added for hits here.")]
    public float bonusMagicResistance = 0f;

    [Header("What hits here do")]
    [Tooltip("Effects on the character hit on this part: Stagger (stun), Impair Movement (slow / root), Ability Effects (bleed, " +
             "silence...), Sever (dismember). Pick the type with the dropdown; write new ones by deriving from BodyPartEffect.")]
    [SerializeReference, SubclassSelector] public List<BodyPartEffect> effects = new List<BodyPartEffect>();

    [Tooltip("After this part was severed, hits that would land on it go to this part instead (usually Torso).")]
    public string redirectWhenSevered = "Torso";

    /// <summary>Is <paramref name="partName"/> this part's name or one of its aliases (case-insensitive)?</summary>
    public bool Matches(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return false;
        string n = partName.Trim();
        if (string.Equals(name, n, StringComparison.OrdinalIgnoreCase))
            return true;
        if (aliases != null)
            for (int i = 0; i < aliases.Count; i++)
                if (string.Equals(aliases[i], n, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    /// <summary>Is the armour slot one of the pieces protecting this part?</summary>
    public bool IsProtectedBy(ArmorSlotType slot)
    {
        if (protectedBy == null)
            return false;
        for (int i = 0; i < protectedBy.Count; i++)
            if (protectedBy[i] == slot)
                return true;
        return false;
    }

    public bool InHeight(float height01) => height01 >= Mathf.Min(heightRange.x, heightRange.y) - 1e-4f && height01 <= Mathf.Max(heightRange.x, heightRange.y) + 1e-4f;

    /// <summary>Distance (in body heights) from <paramref name="height01"/> to this part's band (0 inside).</summary>
    public float HeightDistance(float height01)
    {
        float lo = Mathf.Min(heightRange.x, heightRange.y), hi = Mathf.Max(heightRange.x, heightRange.y);
        return height01 < lo ? lo - height01 : height01 > hi ? height01 - hi : 0f;
    }

    /// <summary>Does a hit <paramref name="lateral"/> (signed fraction of the radius, + = right) fit this part's side?</summary>
    public bool FitsSide(float lateral)
    {
        switch (side)
        {
            case BodySide.Left: return lateral <= -sideOffset;
            case BodySide.Right: return lateral >= sideOffset;
            case BodySide.EitherSide: return Mathf.Abs(lateral) >= sideOffset;
            default: return true;
        }
    }

    public string Describe()
    {
        var bits = new List<string> { $"×{damageMultiplier:0.##} damage" };
        if (protectedBy != null && protectedBy.Count > 0)
            bits.Add("armour: " + string.Join(", ", protectedBy));
        if (!Mathf.Approximately(bonusDefense, 0f))
            bits.Add($"{bonusDefense:+0;-0} Defense");
        if (effects != null)
            foreach (BodyPartEffect e in effects)
                if (e != null) bits.Add(e.Describe());
        return $"{name}: {string.Join(" · ", bits)}";
    }
}

/// <summary>A hit on a body part, as the part's effects see it.</summary>
public struct BodyPartHit
{
    public BodyPartController controller;
    public CombatEntity target;
    public BodyPartDefinition part;
    public BodyPartController.PartState state;
    /// <summary>The hit as it reached health (amount = damage dealt).</summary>
    public DamageInfo info;
    /// <summary>Damage dealt as a fraction of the target's max health.</summary>
    public float damageFraction;
    /// <summary>Damage this part has taken so far as a fraction of max health.</summary>
    public float partDamageFraction;
    /// <summary>The hit killed the target.</summary>
    public bool killingBlow;
}

/// <summary>
/// Something a hit on a body part does to the character hit: stagger, slow, bleed, sever... Add them to a part's Effects
/// with the type dropdown; write new ones by deriving from this class.
/// </summary>
[Serializable]
public abstract class BodyPartEffect
{
    [Tooltip("Chance (0-1) per qualifying hit.")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("Only hits that remove at least this fraction of the character's max health (0 = any hit; 0.1 = solid hits only).")]
    [Range(0f, 1f)] public float minDamageFraction = 0f;
    [Tooltip("Only on critical hits.")]
    public bool criticalOnly = false;

    public string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Should it apply to this hit (thresholds and chance)?</summary>
    public virtual bool ShouldApply(in BodyPartHit hit) =>
        hit.damageFraction + 1e-5f >= minDamageFraction && (!criticalOnly || hit.info.isCritical) &&
        (chance >= 1f || UnityEngine.Random.value <= chance);

    public abstract void Apply(in BodyPartHit hit);
    public abstract string Describe();

    public virtual void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (chance <= 0f)
            warnings.Add($"{owner}: {MenuName} has 0% chance and never happens.");
    }

    protected string Odds() => chance >= 1f ? "" : $"{chance * 100f:0}%: ";
}

[Serializable, AbilityMenu("Body Part/Stagger", "A short stun: the character flinches, its attack or cast is interrupted. Typical for head hits.", 0)]
public class StaggerBodyPartEffect : BodyPartEffect
{
    [Tooltip("Stagger (stun) duration in seconds.")]
    [Min(0.05f)] public float duration = 0.4f;
    [Tooltip("Seconds before the same character can be staggered by this part again (stops stun-locks).")]
    [Min(0f)] public float cooldown = 2f;

    public override void Apply(in BodyPartHit hit)
    {
        if (hit.target == null || hit.killingBlow || hit.state == null)
            return;
        if (Time.time < hit.state.nextStagger)
            return;
        hit.state.nextStagger = Time.time + cooldown;
        hit.target.ApplyControl(ControlType.Stun, duration, hit.info.source);
    }

    public override string Describe() => $"{Odds()}stagger {duration:0.##}s";
}

[Serializable, AbilityMenu("Body Part/Impair Movement", "Slows (or roots) the character: leg wounds.", 1)]
public class ImpairMovementBodyPartEffect : BodyPartEffect
{
    [Tooltip("Speed removed (0.3 = 30% slower).")]
    [Range(0f, 0.95f)] public float slow = 0.3f;
    [Tooltip("Seconds it lasts.")]
    [Min(0.1f)] public float duration = 2.5f;
    [Tooltip("Root (cannot move at all) instead of slowing.")]
    public bool root = false;

    public override void Apply(in BodyPartHit hit)
    {
        if (hit.target == null || hit.killingBlow)
            return;
        if (root)
            hit.target.ApplyControl(ControlType.Root, duration, hit.info.source);
        else
            hit.target.ApplyControl(ControlType.Slow, duration, hit.info.source, slow);
    }

    public override string Describe() => root ? $"{Odds()}root {duration:0.#}s" : $"{Odds()}{slow * 100f:0}% slower for {duration:0.#}s";
}

[Serializable, AbilityMenu("Body Part/Ability Effects", "Any ability effects (bleed, silence, burn, a debuff...) on the character hit, from its attacker.", 2)]
public class AbilityEffectsBodyPartEffect : BodyPartEffect
{
    [Tooltip("Effects applied to the character hit (as if by its attacker): Damage Over Time for a bleed, Silence for a throat hit...")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    public override void Apply(in BodyPartHit hit)
    {
        if (hit.target == null || !hit.target.IsAlive || effects == null || effects.Count == 0)
            return;
        var settings = new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = effects };
        var cast = new AbilityCastInstance(null, hit.info.source, null, null, null);
        Vector3 point = hit.info.point != Vector3.zero ? hit.info.point : hit.target.Center;
        if (hit.info.source == null)
            cast.SetOrigin(point, point + Vector3.up, Quaternion.identity);
        cast.ApplyHit(settings, hit.target, point, hit.info.direction);
    }

    public override string Describe()
    {
        var names = new List<string>();
        if (effects != null)
            foreach (AbilityEffect e in effects)
                if (e != null) names.Add(AbilityTypeNames.Nice(e.GetType()));
        return $"{Odds()}{(names.Count > 0 ? string.Join(", ", names) : "no effects")}";
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (effects == null || effects.Count == 0)
            warnings.Add($"{owner}: Ability Effects has no effects.");
    }
}

[Serializable, AbilityMenu("Body Part/Sever (dismember)", "Cuts the part off: hides its bones / meshes, spawns the severed piece, optional lasting slow. For gore-enabled games.", 3)]
public class SeverBodyPartEffect : BodyPartEffect
{
    [Tooltip("Sever when the damage this part has taken reaches this fraction of max health (0 = the first qualifying hit).")]
    [Range(0f, 2f)] public float partDamageThreshold = 0.5f;
    [Tooltip("Only sever with the killing blow (dismemberment on death only).")]
    public bool onlyOnKillingBlow = true;
    [Tooltip("Bones collapsed to hide the limb of a skinned mesh, by name (e.g. LeftForeArm). Their children go with them.")]
    public List<string> bones = new List<string>();
    [Tooltip("Renderers turned off, by object name (limbs that are separate meshes).")]
    public List<string> renderers = new List<string>();
    [Tooltip("Spawned at the first bone: the severed piece (give it a Rigidbody so it falls).")]
    public GameObject severedPrefab;
    [Tooltip("Seconds before the severed piece is removed (0 = stays).")]
    [Min(0f)] public float severedLifetime = 20f;
    public GameObject vfx;
    public AudioClip sound;
    [Tooltip("Lasting slow while the character lives on without this part (a lost leg). 0 = none.")]
    [Range(0f, 0.95f)] public float lastingSlow = 0f;
    [Tooltip("The character can no longer attack with weapons (a lost arm, players only).")]
    public bool disablesWeaponAttacks = false;

    public override bool ShouldApply(in BodyPartHit hit)
    {
        if (hit.state == null || hit.state.severed)
            return false;
        if (onlyOnKillingBlow && !hit.killingBlow)
            return false;
        if (hit.partDamageFraction + 1e-5f < partDamageThreshold)
            return false;
        return base.ShouldApply(hit);
    }

    public override void Apply(in BodyPartHit hit)
    {
        if (hit.controller != null)
            hit.controller.Sever(hit.part, this, hit.info);
    }

    public override string Describe() =>
        $"{Odds()}severed at {partDamageThreshold * 100f:0}% damage" + (onlyOnKillingBlow ? " (killing blow)" : "");

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if ((bones == null || bones.Count == 0) && (renderers == null || renderers.Count == 0) && severedPrefab == null)
            warnings.Add($"{owner}: Sever hides nothing (no bones, renderers or severed prefab): only the event is raised.");
    }
}

/// <summary>Which part a hit counts for when it touches several hitboxes at once (one hit is always one part, never several).</summary>
public enum SeveralPartsRule
{
    /// <summary>The part nearest to where the weapon or projectile touched (the first contact).</summary>
    NearestToContact,
    /// <summary>The touched part with the highest damage multiplier (rewards aiming at weak spots).</summary>
    MostVulnerable,
    /// <summary>The touched part with the lowest damage multiplier (forgiving).</summary>
    LeastVulnerable,
}

/// <summary>
/// The body parts of a kind of character: a humanoid (head, torso, arms, legs), a quadruped, a dragon with a weak belly...
/// Assign it to a <see cref="BodyPartController"/> (or to Combat Settings for everyone). Hits find their part from a
/// hitbox collider, or from the hit's height and side on the body; the part scales the damage, decides which armour
/// protects, and runs its effects.
/// </summary>
[CreateAssetMenu(fileName = "BodyPartProfile", menuName = "SimpleMovements/Combat/Body Part Profile")]
public class BodyPartProfile : ScriptableObject
{
    [Tooltip("The body parts. Add as many as the creature has (a tail, wings, a weak spot).")]
    public List<BodyPartDefinition> parts = new List<BodyPartDefinition>();
    [Tooltip("Part used when the location is unknown (no hit point, area damage): usually Torso.")]
    public string defaultPart = "Torso";
    [Tooltip("Share of the armour NOT covering the struck part that still protects (0 = only the covering pieces count; 1 = every " +
             "piece counts everywhere, like without body parts).")]
    [Range(0f, 1f)] public float otherArmorShare = 0.25f;
    [Tooltip("Area damage (explosions, slams, auras) uses the Default Part instead of where it touches.")]
    public bool areaDamageUsesDefaultPart = true;
    [Tooltip("Multiplies every part's damage multiplier (difficulty tuning).")]
    [Min(0f)] public float globalDamageScale = 1f;
    [Tooltip("A hit always counts for ONE part (damage is never applied twice). When it touches several hitboxes at once " +
             "(a blade across the neck and shoulder): the nearest to the contact point, the most or the least vulnerable one.")]
    public SeveralPartsRule whenSeveralParts = SeveralPartsRule.NearestToContact;

    /// <summary>The part named <paramref name="partName"/> (or with that alias), or null.</summary>
    public BodyPartDefinition Find(string partName)
    {
        if (parts == null || string.IsNullOrWhiteSpace(partName))
            return null;
        for (int i = 0; i < parts.Count; i++)
            if (parts[i] != null && parts[i].Matches(partName))
                return parts[i];
        return null;
    }

    /// <summary>The default part (or the first one).</summary>
    public BodyPartDefinition Default
    {
        get
        {
            BodyPartDefinition d = Find(defaultPart);
            if (d != null)
                return d;
            return parts != null && parts.Count > 0 ? parts[0] : null;
        }
    }

    /// <summary>
    /// The part at <paramref name="height01"/> (0 feet - 1 head) and <paramref name="lateral"/> (signed fraction of the
    /// body radius, + = the character's right): side parts first, then centre parts, then the nearest band.
    /// </summary>
    public BodyPartDefinition Locate(float height01, float lateral)
    {
        if (parts == null || parts.Count == 0)
            return null;
        BodyPartDefinition centre = null, sided = null, nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartDefinition p = parts[i];
            if (p == null)
                continue;
            float d = p.HeightDistance(height01);
            if (d <= 0f)
            {
                if (p.side == BodySide.Any) { if (centre == null) centre = p; }
                else if (sided == null && p.FitsSide(lateral)) sided = p;
            }
            if (p.side == BodySide.Any && d < nearestDist)
            {
                nearestDist = d;
                nearest = p;
            }
        }
        return sided ?? centre ?? nearest ?? Default;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (parts == null || parts.Count == 0)
        {
            errors.Add("The profile has no body parts.");
            return;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartDefinition p = parts[i];
            if (p == null) { warnings.Add($"Part #{i + 1} is empty."); continue; }
            if (string.IsNullOrWhiteSpace(p.name)) errors.Add($"Part #{i + 1} has no name.");
            else if (!names.Add(p.name.Trim())) errors.Add($"Two parts are named '{p.name}'.");
            if (p.heightRange.x > p.heightRange.y) warnings.Add($"{p.name}: its height range is reversed (from > to); it is read as {p.heightRange.y:0.##}-{p.heightRange.x:0.##}.");
            if (p.effects != null)
                foreach (BodyPartEffect e in p.effects)
                    e?.Validate(p.name, errors, warnings);
            if (!string.IsNullOrWhiteSpace(p.redirectWhenSevered) && Find(p.redirectWhenSevered) == null)
                warnings.Add($"{p.name}: Redirect When Severed names '{p.redirectWhenSevered}', which is not a part of this profile.");
        }
        if (Find(defaultPart) == null)
            warnings.Add($"Default Part '{defaultPart}' is not a part of this profile (the first part is used).");
        // Every height should belong to some centre part.
        for (float h = 0.02f; h < 1f; h += 0.05f)
        {
            bool covered = false;
            foreach (BodyPartDefinition p in parts)
                if (p != null && p.side == BodySide.Any && p.InHeight(h)) { covered = true; break; }
            if (!covered)
            {
                warnings.Add($"No centre part covers the height {h:0.00} of the body (hits there use the nearest part).");
                break;
            }
        }
    }

    /// <summary>Fills this profile with a humanoid layout (head, torso, arms, legs) and sensible effects.</summary>
    public void FillHumanoid()
    {
        parts = new List<BodyPartDefinition>
        {
            new BodyPartDefinition
            {
                name = "Head", aliases = new List<string> { "Neck", "Face" }, heightRange = new Vector2(0.83f, 1f), damageMultiplier = 1.6f,
                protectedBy = new List<ArmorSlotType> { ArmorSlotType.Helmet },
                effects = new List<BodyPartEffect> { new StaggerBodyPartEffect { chance = 0.35f, minDamageFraction = 0.05f, duration = 0.4f } },
            },
            new BodyPartDefinition
            {
                name = "Torso", aliases = new List<string> { "Chest", "Body", "Back" }, heightRange = new Vector2(0.45f, 0.83f), damageMultiplier = 1f,
                protectedBy = new List<ArmorSlotType> { ArmorSlotType.Chestplate, ArmorSlotType.Shoulders, ArmorSlotType.Cloak, ArmorSlotType.Belt },
            },
            new BodyPartDefinition
            {
                name = "Arms", aliases = new List<string> { "Arm", "Hands", "LeftArm", "RightArm" }, heightRange = new Vector2(0.45f, 0.86f),
                side = BodySide.EitherSide, sideOffset = 0.65f, damageMultiplier = 0.8f,
                protectedBy = new List<ArmorSlotType> { ArmorSlotType.Gloves, ArmorSlotType.Bracers, ArmorSlotType.Shoulders, ArmorSlotType.Shield },
            },
            new BodyPartDefinition
            {
                name = "Legs", aliases = new List<string> { "Leg", "Feet", "Hips" }, heightRange = new Vector2(0f, 0.45f), damageMultiplier = 0.85f,
                protectedBy = new List<ArmorSlotType> { ArmorSlotType.Leggings, ArmorSlotType.Boots },
                effects = new List<BodyPartEffect> { new ImpairMovementBodyPartEffect { chance = 0.3f, minDamageFraction = 0.05f, slow = 0.3f, duration = 2f } },
            },
        };
        defaultPart = "Torso";
        otherArmorShare = 0.25f;
    }

    /// <summary>A humanoid profile created in memory (used when a controller has none assigned).</summary>
    public static BodyPartProfile CreateHumanoid()
    {
        var p = CreateInstance<BodyPartProfile>();
        p.name = "Humanoid (built-in)";
        p.hideFlags = HideFlags.DontSave;
        p.FillHumanoid();
        return p;
    }

    private void Reset() => FillHumanoid();
}
