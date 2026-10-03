using System.Collections.Generic;
using UnityEngine;

/// <summary>What an archetype is. One data model serves races, classes and anything else that defines a character.</summary>
public enum ArchetypeKind
{
    Race,
    Class,
    /// <summary>Backgrounds, origins, professions, subclasses, bloodlines...</summary>
    Background,
    Other,
}

/// <summary>Makes some traits cheaper or dearer for an archetype (an Elf pays less for Magic traits).</summary>
[System.Serializable]
public class TraitAffinity
{
    [Tooltip("One trait. Empty = every trait of the Type below.")]
    public Trait trait;
    [Tooltip("Trait type affected when no trait is set.")]
    public TraitType type = TraitType.Magic;
    [Tooltip("Cost multiplier (0.75 = 25% cheaper, 1.5 = 50% dearer). Drawbacks (negative costs) are not changed.")]
    [Range(0f, 3f)] public float costMultiplier = 0.75f;

    public bool Matches(Trait t) => t != null && (trait != null ? trait == t : t.type == type);

    public string Describe() =>
        $"{(trait != null ? trait.Name : type + " traits")} cost {(costMultiplier < 1f ? $"{(1f - costMultiplier) * 100f:0}% less" : $"{(costMultiplier - 1f) * 100f:0}% more")}";
}

/// <summary>
/// A race, class or background: everything that makes one kind of character differ. Base combat stats and resistances,
/// growth per level, attribute scaling (e.g. "+0.3% crit per Agility"), character stats (max health, move speed...),
/// passives and abilities (the same effects as equipment: abilities on a key, on-hit / when-hit effects, auras,
/// conditional bonuses, trait strength changes), height, and which traits it can take and how much they cost.
/// <para>A character combines several (a race + a class + a background) through <see cref="CharacterIdentity"/>; every
/// part adds to the same stat systems as equipment and traits, so nothing conflicts and removing one is exact.</para>
/// Create with Assets ▸ Create ▸ SimpleMovements ▸ Character ▸ Character Archetype (Race / Class).
/// </summary>
[CreateAssetMenu(fileName = "New Archetype", menuName = "SimpleMovements/Character/Character Archetype (Race - Class)", order = 1)]
public class CharacterArchetype : ScriptableObject
{
    [Header("Identity")]
    public ArchetypeKind kind = ArchetypeKind.Race;
    [Tooltip("Name shown to the player. Empty = the asset name.")]
    public string displayName;
    [TextArea(3, 6)] public string description;
    public Sprite icon;
    public Color color = Color.white;
    [Tooltip("Can be picked on the character creation screen.")]
    public bool availableAtCreation = true;

    [Header("Body")]
    [Tooltip("This archetype decides the character's height (races). Off = it does not change height.")]
    public bool setsHeight = true;
    [Tooltip("Heights the player can pick at creation (metres).")]
    public Vector2 heightRange = new Vector2(1.6f, 1.95f);
    [Tooltip("Height used when none was picked (metres).")]
    public float defaultHeight = 1.78f;

    [Header("Base Combat Stats")]
    [Tooltip("Combat stats of every character of this archetype: Strength, Defense, Critical Chance, Armor Penetration, Height, Threat...")]
    public List<CombatStatModifier> combatStats = new List<CombatStatModifier>();
    [Tooltip("Elemental resistances (negative = weakness).")]
    public List<ElementalResistance> resistances = new List<ElementalResistance>();
    [Tooltip("Attribute scaling: points of one stat give another (+0.3 Critical Chance per Agility, +0.5 Defense per Endurance). " +
             "Added to the general rules of the Combat Stats component.")]
    public List<StatScalingRule> scalingRules = new List<StatScalingRule>();
    [Tooltip("Character stats (players): max health / stamina / mana, regeneration, move speed, damage taken, healing received...")]
    public List<TraitModifier> characterStats = new List<TraitModifier>();

    [Header("Growth Per Level")]
    [Tooltip("Combat stats gained every level after the first (×(level-1)): +2 Strength, +0.5 Defense per level...")]
    public List<CombatStatModifier> combatStatsPerLevel = new List<CombatStatModifier>();
    [Tooltip("Character stats gained every level after the first: +10 Max Health per level...")]
    public List<TraitModifier> characterStatsPerLevel = new List<TraitModifier>();

    [Header("Passives & Abilities")]
    [Tooltip("Unique mechanics, exactly like equipment effects: an ability on a key (racial ability), on-hit / when-hit effects, " +
             "pulses and auras, conditional bonuses (at night, at low health), granted traits, stronger / weaker traits (Enhance Trait)...")]
    [SerializeReference, SubclassSelector] public List<EquipmentEffect> effects = new List<EquipmentEffect>();

    [Header("Traits")]
    [Tooltip("Traits every character of this archetype has (free, cannot be removed by the player).")]
    public List<Trait> innateTraits = new List<Trait>();
    [Tooltip("Extra traits offered at character creation (e.g. race traits). Mark a trait as exclusive with the trait's own " +
             "'Only For' list.")]
    public List<Trait> offeredTraits = new List<Trait>();
    [Tooltip("Traits characters of this archetype can never take.")]
    public List<Trait> forbiddenTraits = new List<Trait>();
    [Tooltip("Traits (or trait types) that are cheaper or dearer for this archetype.")]
    public List<TraitAffinity> traitAffinities = new List<TraitAffinity>();
    [Tooltip("Trait points added to (or removed from) the character's budget at creation.")]
    public int bonusTraitPoints = 0;

    [Header("Combinations")]
    [Tooltip("Archetypes that cannot be combined with this one (a race that cannot be a Paladin).")]
    public List<CharacterArchetype> incompatibleWith = new List<CharacterArchetype>();

    public string Name => string.IsNullOrEmpty(displayName) ? name : displayName;

    public float ClampHeight(float metres) => Mathf.Clamp(metres, Mathf.Min(heightRange.x, heightRange.y), Mathf.Max(heightRange.x, heightRange.y));

    public bool IsCompatibleWith(CharacterArchetype other) =>
        other == null || other == this || (!incompatibleWith.Contains(other) && !other.incompatibleWith.Contains(this));

    /// <summary>Cost multiplier this archetype applies to a trait (1 = unchanged).</summary>
    public float TraitCostMultiplier(Trait trait)
    {
        float m = 1f;
        Trait specific = null;
        foreach (TraitAffinity a in traitAffinities)
        {
            if (a == null || !a.Matches(trait))
                continue;
            // A rule for the trait itself wins over a rule for its type.
            if (a.trait != null) { specific = a.trait; m = a.costMultiplier; }
            else if (specific == null) m *= a.costMultiplier;
        }
        return m;
    }

    /// <summary>Description plus a bullet list of what it changes (creation screen, tooltips).</summary>
    public string GetFormattedDescription()
    {
        var lines = new List<string>();
        foreach (CombatStatModifier m in combatStats) if (m != null) lines.Add(m.Describe());
        foreach (ElementalResistance r in resistances) if (r != null) lines.Add(r.Describe());
        foreach (TraitModifier m in characterStats) if (m != null) lines.Add(m.Describe());
        foreach (StatScalingRule r in scalingRules) if (r != null) lines.Add(r.Describe());
        foreach (CombatStatModifier m in combatStatsPerLevel) if (m != null) lines.Add(m.Describe() + " per level");
        foreach (TraitModifier m in characterStatsPerLevel) if (m != null) lines.Add(m.Describe() + " per level");
        foreach (EquipmentEffect e in effects) if (e != null) lines.Add(e.Describe());
        foreach (Trait t in innateTraits) if (t != null) lines.Add("Trait: " + t.Name);
        foreach (TraitAffinity a in traitAffinities) if (a != null) lines.Add(a.Describe());
        if (bonusTraitPoints != 0) lines.Add($"{bonusTraitPoints:+0;-0} trait points");
        string d = description ?? "";
        if (lines.Count > 0)
            d += (string.IsNullOrEmpty(d) ? "" : "\n\n") + "• " + string.Join("\n• ", lines);
        return d;
    }

    /// <summary>Setup problems (inspector and Validate All).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null) errors.Add($"{Name}: effect {i} is empty (pick a type or remove it).");
            else effects[i].Validate(Name, errors, warnings);
        }
        if (setsHeight && (heightRange.x <= 0f || heightRange.y <= 0f))
            errors.Add($"{Name}: height range must be above 0.");
        if (setsHeight && (defaultHeight < Mathf.Min(heightRange.x, heightRange.y) || defaultHeight > Mathf.Max(heightRange.x, heightRange.y)))
            warnings.Add($"{Name}: the default height is outside the height range.");
        foreach (Trait t in innateTraits)
            if (t != null && forbiddenTraits.Contains(t)) errors.Add($"{Name}: '{t.Name}' is both innate and forbidden.");
        foreach (Trait t in offeredTraits)
            if (t != null && forbiddenTraits.Contains(t)) errors.Add($"{Name}: '{t.Name}' is both offered and forbidden.");
        if (incompatibleWith.Contains(this))
            errors.Add($"{Name}: is incompatible with itself.");
        if (kind == ArchetypeKind.Class && setsHeight)
            warnings.Add($"{Name}: a Class archetype sets the height; usually only races do (untick Sets Height).");
        if (kind == ArchetypeKind.Race && !setsHeight)
            warnings.Add($"{Name}: this race does not set the height, so the height slider is hidden when it is picked.");
        if (combatStats.Count == 0 && resistances.Count == 0 && scalingRules.Count == 0 && characterStats.Count == 0 &&
            combatStatsPerLevel.Count == 0 && characterStatsPerLevel.Count == 0 && effects.Count == 0 && innateTraits.Count == 0 &&
            traitAffinities.Count == 0 && bonusTraitPoints == 0 && offeredTraits.Count == 0 && forbiddenTraits.Count == 0)
            warnings.Add($"{Name}: changes nothing yet (no stats, growth, passives, traits or trait rules).");
        if (innateTraits.FindAll(t => t != null && t.HasActiveSkill).Count > 1)
            warnings.Add($"{Name}: more than one innate trait is an active trait.");
        foreach (Trait t in offeredTraits)
            if (t != null && t.onlyFor != null && t.onlyFor.Exists(x => x != null) && !t.onlyFor.Contains(this))
                warnings.Add($"{Name}: offers '{t.Name}', but that trait's Only For list does not include this archetype, so it is never shown.");
        foreach (TraitAffinity aff in traitAffinities)
        {
            if (aff == null) continue;
            if (aff.costMultiplier <= 0f)
                warnings.Add($"{Name}: {aff.Describe()} - a multiplier of 0 makes those traits free.");
        }
        foreach (StatScalingRule r in scalingRules)
            if (r != null && r.from == r.to) errors.Add($"{Name}: a scaling rule gives {r.from} from itself.");
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = name;
    }
}
