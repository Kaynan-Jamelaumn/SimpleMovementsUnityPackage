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

[System.Serializable]
public class TraitEffect
{
    public TraitEffectType effectType;
    public float value;
    public string targetStat;

    [Space]
    [Header("Description")]
    [TextArea(2, 3)]
    public string effectDescription;
}

[CreateAssetMenu(fileName = "New Trait", menuName = "Scriptable Objects/Trait")]
public class Trait : ScriptableObject
{
    [Header("Basic Info")]
    public string traitName;
    [TextArea(3, 5)]
    public string description;

    [Header("Cost & Type")]
    public int cost;
    public TraitType type;
    public TraitRarity rarity = TraitRarity.Common;

    [Header("Effects")]
    public List<TraitEffect> effects = new List<TraitEffect>();

    [Header("Dependencies")]
    public List<Trait> incompatibleTraits = new List<Trait>();
    public List<Trait> requiredTraits = new List<Trait>();
    public List<Trait> mutuallyExclusiveTraits = new List<Trait>();

    [Header("Visual")]
    public Sprite icon;
    public Color traitColor = Color.white;

    [Header("Audio")]
    public AudioClip acquisitionSound;

    // Properties
    public bool IsPositive => cost > 0;
    public bool IsNegative => cost < 0;
    public bool IsFree => cost == 0;
    public string Name => string.IsNullOrEmpty(traitName) ? name : traitName;

    // Get formatted description
    public string GetFormattedDescription()
    {
        string desc = description;

        if (effects.Count > 0)
        {
            desc += "\n\nEffects:";
            foreach (var effect in effects)
            {
                if (!string.IsNullOrEmpty(effect.effectDescription))
                {
                    desc += $"\n• {effect.effectDescription}";
                }
                else
                {
                    desc += $"\n• {effect.effectType}: {effect.value} to {effect.targetStat}";
                }
            }
        }

        return desc;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(traitName))
            traitName = name;
    }
}