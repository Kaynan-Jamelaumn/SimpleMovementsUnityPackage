using System.Collections.Generic;
using System.Linq;
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

[System.Serializable]
public class ClassRestriction
{
    [Header("Class Restrictions")]
    public List<PlayerClass> allowedClasses = new List<PlayerClass>();
    public List<PlayerClass> prohibitedClasses = new List<PlayerClass>();

    [Header("Level Requirements")]
    public int minimumLevel = 1;
    public int maximumLevel = 100;

    [Header("Stat Requirements")]
    public List<StatRequirement> statRequirements = new List<StatRequirement>();
}

[System.Serializable]
public class StatRequirement
{
    public string statName;
    public float minimumValue;
    public ComparisonType comparisonType = ComparisonType.GreaterThanOrEqual;
}

public enum ComparisonType
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal
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

    [Header("Class & Level Restrictions")]
    public ClassRestriction restrictions = new ClassRestriction();

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

    // Check if trait is available for a specific class
    public bool IsAvailableForClass(PlayerClass playerClass)
    {
        if (playerClass == null) return false;

        // Check allowed classes
        if (restrictions.allowedClasses.Count > 0 && !restrictions.allowedClasses.Contains(playerClass))
            return false;

        // Check prohibited classes
        if (restrictions.prohibitedClasses.Contains(playerClass))
            return false;

        return true;
    }

    // Check if trait is available for a specific level
    public bool IsAvailableForLevel(int level)
    {
        return level >= restrictions.minimumLevel && level <= restrictions.maximumLevel;
    }

    // Check if player meets stat requirements
    public bool MeetsStatRequirements(PlayerStatusController player)
    {
        if (player == null || restrictions.statRequirements.Count == 0) return true;

        foreach (var requirement in restrictions.statRequirements)
        {
            float playerStatValue = GetPlayerStatValue(player, requirement.statName);

            bool meets = requirement.comparisonType switch
            {
                ComparisonType.GreaterThan => playerStatValue > requirement.minimumValue,
                ComparisonType.GreaterThanOrEqual => playerStatValue >= requirement.minimumValue,
                ComparisonType.LessThan => playerStatValue < requirement.minimumValue,
                ComparisonType.LessThanOrEqual => playerStatValue <= requirement.minimumValue,
                ComparisonType.Equal => Mathf.Approximately(playerStatValue, requirement.minimumValue),
                _ => true
            };

            if (!meets) return false;
        }

        return true;
    }

    private float GetPlayerStatValue(PlayerStatusController player, string statName)
    {
        return statName.ToLower() switch
        {
            "health" => player.HpManager.MaxValue,
            "stamina" => player.StaminaManager.MaxValue,
            "mana" => player.ManaManager.MaxValue,
            "speed" => player.SpeedManager.BaseSpeed,
            "level" => player.XPManager.CurrentLevel,
            _ => 0f
        };
    }

    // Get formatted description with requirements
    public string GetFormattedDescription()
    {
        string desc = description;

        if (restrictions.allowedClasses.Count > 0)
        {
            desc += $"\n\nRestricted to: {string.Join(", ", restrictions.allowedClasses.Select(c => c.GetClassName()))}";
        }

        if (restrictions.minimumLevel > 1)
        {
            desc += $"\nMinimum Level: {restrictions.minimumLevel}";
        }

        if (restrictions.statRequirements.Count > 0)
        {
            desc += "\nRequirements:";
            foreach (var req in restrictions.statRequirements)
            {
                desc += $"\n• {req.statName} {GetComparisonSymbol(req.comparisonType)} {req.minimumValue}";
            }
        }

        return desc;
    }

    private string GetComparisonSymbol(ComparisonType type)
    {
        return type switch
        {
            ComparisonType.GreaterThan => ">",
            ComparisonType.GreaterThanOrEqual => "≥",
            ComparisonType.LessThan => "<",
            ComparisonType.LessThanOrEqual => "≤",
            ComparisonType.Equal => "=",
            _ => "?"
        };
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(traitName))
            traitName = name;

        // Ensure restrictions object exists
        if (restrictions == null)
            restrictions = new ClassRestriction();
    }
}