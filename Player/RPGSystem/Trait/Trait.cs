using System.Collections.Generic;
using UnityEngine;

public enum TraitType
{
    Combat,
    Survival,
    Magic,
    Social,
    Crafting,
    Movement,
    Mental,
    Physical
}

public enum TraitEffectType
{
    StatMultiplier,
    StatAddition,
    RegenerationRate,
    ConsumptionRate,
    ResistanceBonus,
    SkillBonus,
    Special
}

public enum TraitRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>Whether a trait only changes stats, gives something the player uses, or both.</summary>
public enum TraitKind
{
    /// <summary>Only passive stat changes (always on).</summary>
    Passive,
    /// <summary>Gives something the player uses: a movement skill, an ability on a key.</summary>
    Active,
    /// <summary>Passive changes and something the player uses.</summary>
    Hybrid,
}

/// <summary>
/// OLD string-based effect ("health" x1.2, "damage" +5...). Still supported: the Trait Manager maps the common ones
/// (health, stamina, mana, speed, defense, healing, jump, weight, cooldown) and the weapon system reads "damage",
/// "attack", "element_*", "lifesteal", "aoe" and "*on_hit*" itself. New traits should use Modifiers instead.
/// </summary>
[System.Serializable]
public class TraitEffect
{
    [Tooltip("StatMultiplier: value 1.2 = +20%. StatAddition: +value. RegenerationRate: +value per tick. ConsumptionRate: +value% stamina use. ResistanceBonus: value% less damage.")]
    public TraitEffectType effectType;
    [Tooltip("Amount (see Effect Type for how it is read).")]
    public float value;
    [Tooltip("Stat name: health, stamina, mana, speed, defense, healing, jump, weight, cooldown - or weapon keys: damage, attack, element_fire, lifesteal, aoe...")]
    public string targetStat;

    [Space]
    [Header("Description")]
    [TextArea(2, 3)]
    public string effectDescription;
}

/// <summary>
/// A character trait chosen when the character is created (or granted by armor, quests...). It can have:
/// <list type="bullet">
/// <item><b>Modifiers</b> - passive stat changes: +20% max health, -15% damage taken, +10% move speed...</item>
/// <item><b>Behaviours</b> - active or triggered parts: double jump, wall climb, glide, an ability on a key (barrier),
/// second wind, life steal, modifiers while at low health...</item>
/// </list>
/// Positive traits cost trait points; negative traits (drawbacks) have a negative cost and give points back.
/// Create with Assets ▸ Create ▸ Scriptable Objects ▸ Trait (or Trait From Preset...).
/// </summary>
[CreateAssetMenu(fileName = "New Trait", menuName = "Scriptable Objects/Trait")]
public class Trait : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Name shown to the player. Empty = the asset name.")]
    public string traitName;
    [Tooltip("Description shown to the player. The modifiers and behaviours are listed after it automatically.")]
    [TextArea(3, 5)]
    public string description;

    [Header("Cost & Type")]
    [Tooltip("Trait points it costs when picked. Positive = a benefit that costs points. Negative = a drawback that GIVES points back. 0 = free.")]
    public int cost;
    [Tooltip("Category used to group traits in menus.")]
    public TraitType type;
    [Tooltip("Rarity (for UI colour and random rolls).")]
    public TraitRarity rarity = TraitRarity.Common;
    [Tooltip("Can be picked on the character creation screen. Turn off for traits only granted by armor, quests or scripts.")]
    public bool availableAtCreation = true;

    [Header("Passive Modifiers (always on)")]
    [Tooltip("Stat changes while the character has the trait: +20% Max Health, -15% Damage Taken, +10% Move Speed...")]
    public List<TraitModifier> modifiers = new List<TraitModifier>();

    [Header("Active & Triggered Behaviours")]
    [Tooltip("Things the trait lets the character DO or reacts with: double jump, wall climb, glide, an ability on a key (barrier), second wind, life steal, modifiers while at low health...")]
    [SerializeReference, SubclassSelector] public List<TraitBehaviour> behaviours = new List<TraitBehaviour>();

    [Header("Legacy Effects (old string-based)")]
    [Tooltip("Old effects. Still applied (common stats) and read by weapons (damage, element_*, lifesteal...). Prefer Modifiers for new traits.")]
    public List<TraitEffect> effects = new List<TraitEffect>();

    [Header("Dependencies")]
    [Tooltip("Cannot be taken together with any of these.")]
    public List<Trait> incompatibleTraits = new List<Trait>();
    [Tooltip("The character must already have ALL of these.")]
    public List<Trait> requiredTraits = new List<Trait>();
    [Tooltip("Only one trait of such a group can be taken (e.g. Tall / Short).")]
    public List<Trait> mutuallyExclusiveTraits = new List<Trait>();

    [Header("Visual")]
    public Sprite icon;
    [Tooltip("Colour of the trait in the UI.")]
    public Color traitColor = Color.white;

    [Header("Audio")]
    [Tooltip("Played when the trait is gained.")]
    public AudioClip acquisitionSound;

    // Properties
    public bool IsPositive => cost > 0;
    public bool IsNegative => cost < 0;
    public bool IsFree => cost == 0;
    public string Name => string.IsNullOrEmpty(traitName) ? name : traitName;

    /// <summary>Passive, Active (something the player uses) or Hybrid.</summary>
    public TraitKind Kind
    {
        get
        {
            bool active = false;
            foreach (TraitBehaviour b in behaviours)
                if (b != null && b.IsActive) { active = true; break; }
            bool passive = modifiers.Count > 0 || effects.Count > 0;
            if (!active)
                foreach (TraitBehaviour b in behaviours)
                    if (b != null && !b.IsActive) { passive = true; break; }
            return active ? (passive ? TraitKind.Hybrid : TraitKind.Active) : TraitKind.Passive;
        }
    }

    /// <summary>Description plus a bullet list of every modifier, behaviour and legacy effect.</summary>
    public string GetFormattedDescription()
    {
        string desc = description;
        var lines = new List<string>();
        foreach (TraitModifier m in modifiers)
            if (m != null) lines.Add(m.Describe());
        foreach (TraitBehaviour b in behaviours)
            if (b != null) lines.Add(b.Describe());
        foreach (TraitEffect effect in effects)
        {
            if (effect == null) continue;
            lines.Add(!string.IsNullOrEmpty(effect.effectDescription) ? effect.effectDescription : $"{effect.effectType}: {effect.value} to {effect.targetStat}");
        }
        if (lines.Count > 0)
            desc += (string.IsNullOrEmpty(desc) ? "" : "\n\n") + "Effects:\n• " + string.Join("\n• ", lines);
        return desc;
    }

    /// <summary>Setup problems (shown by the inspector and Validate All).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (modifiers.Count == 0 && behaviours.Count == 0 && effects.Count == 0)
            warnings.Add($"{Name}: does nothing (no modifiers, behaviours or effects).");
        int good = 0, bad = 0;
        foreach (TraitModifier m in modifiers)
        {
            if (m == null) continue;
            if (Mathf.Approximately(m.value, 0f))
                warnings.Add($"{Name}: a '{TraitStats.Get(m.stat).name}' modifier has value 0.");
            if (m.IsBeneficial) good++; else bad++;
        }
        if (cost > 0 && good == 0 && bad > 0 && behaviours.Count == 0)
            warnings.Add($"{Name}: every modifier is a drawback but the trait COSTS points. Drawbacks usually have a negative cost (they give points).");
        if (cost < 0 && bad == 0 && good > 0)
            warnings.Add($"{Name}: every modifier is a benefit but the trait GIVES points (negative cost).");
        for (int i = 0; i < behaviours.Count; i++)
        {
            if (behaviours[i] == null)
                errors.Add($"{Name}: behaviour {i} is empty (pick a type or remove it).");
            else
                behaviours[i].Validate(errors, warnings);
        }
        foreach (TraitEffect e in effects)
        {
            if (e == null) continue;
            if (!TraitStats.TryMapLegacy(e, out _, out _, out _) && !TraitStats.IsWeaponLegacyKey(e.targetStat))
                warnings.Add($"{Name}: legacy effect '{e.targetStat}' ({e.effectType}) is not applied by anything.");
        }
        if (requiredTraits.Contains(this) || incompatibleTraits.Contains(this) || mutuallyExclusiveTraits.Contains(this))
            errors.Add($"{Name}: lists itself as required, incompatible or mutually exclusive.");
        foreach (Trait r in requiredTraits)
            if (r != null && (incompatibleTraits.Contains(r) || mutuallyExclusiveTraits.Contains(r)))
                errors.Add($"{Name}: '{r.Name}' is both required and incompatible - the trait can never be taken.");
        foreach (Trait r in requiredTraits)
            if (r != null && r.requiredTraits.Contains(this))
                errors.Add($"{Name}: '{r.Name}' and this trait require each other - neither can be taken first.");
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(traitName))
            traitName = name;
    }
}
