using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class StatGains
{
    [Header("Base Stat Gains")]
    public float healthGain = 5f;
    public float staminaGain = 5f;
    public float manaGain = 5f;
    public float speedGain = 0.1f;

    [Header("Combat Stat Gains")]
    public float strengthGain = 1f;
    public float agilityGain = 1f;
    public float intelligenceGain = 1f;
    public float enduranceGain = 1f;

    [Header("Other Stat Gains")]
    public float hungerGain = 2f;
    public float thirstGain = 2f;
    public float sleepGain = 2f;
    public float sanityGain = 2f;
    public float weightGain = 1f;
}

[System.Serializable]
public class StatMultipliers
{
    [Header("Stat Upgrade Multipliers")]
    [Tooltip("Multiplier when player chooses to upgrade this stat")]
    public float healthMultiplier = 1f;
    public float staminaMultiplier = 1f;
    public float manaMultiplier = 1f;
    public float speedMultiplier = 1f;
    public float strengthMultiplier = 1f;
    public float agilityMultiplier = 1f;
    public float intelligenceMultiplier = 1f;
    public float enduranceMultiplier = 1f;

    // Get multiplier for a specific stat
    public float GetMultiplier(string statName)
    {
        return statName.ToLower() switch
        {
            "health" => healthMultiplier,
            "stamina" => staminaMultiplier,
            "mana" => manaMultiplier,
            "speed" => speedMultiplier,
            "strength" => strengthMultiplier,
            "agility" => agilityMultiplier,
            "intelligence" => intelligenceMultiplier,
            "endurance" => enduranceMultiplier,
            _ => 1f
        };
    }
}

[System.Serializable]
public class ClassProgression
{
    [Header("Level Milestones")]
    public List<LevelMilestone> milestones = new List<LevelMilestone>();

    [Header("Passive Abilities")]
    public List<PassiveAbility> passiveAbilities = new List<PassiveAbility>();
}

[System.Serializable]
public class LevelMilestone
{
    public int level;
    public string milestoneTitle;
    [TextArea(2, 3)]
    public string description;
    public List<Trait> unlockedTraits = new List<Trait>();
    public List<PassiveAbility> unlockedAbilities = new List<PassiveAbility>();
    public int bonusTraitPoints = 0;
    public int bonusStatPoints = 0;
}

[System.Serializable]
public class PassiveAbility
{
    public string abilityName;
    [TextArea(2, 3)]
    public string description;
    public Sprite icon;
    public List<TraitEffect> effects = new List<TraitEffect>();
}

[CreateAssetMenu(fileName = "New Player Class", menuName = "Scriptable Objects/Player Class")]
public class PlayerClass : ScriptableObject
{
    [Header("Basic Info")]
    public string className;
    [TextArea(3, 5)]
    public string classDescription;
    [TextArea(2, 4)]
    public string classLore;
    public Sprite classIcon;
    public Color classColor = Color.white;

    [Header("Base Stats")]
    public float health = 100f;
    public float stamina = 100f;
    public float mana = 100f;
    public float speed = 5f;
    public float hunger = 100f;
    public float thirst = 100f;
    public float weight = 30f;
    public float sleep = 100f;
    public float sanity = 100f;
    public float bodyHeat = 100f;
    public float oxygen = 100f;

    [Header("Combat Stats")]
    public float strength = 10f;
    public float agility = 10f;
    public float intelligence = 10f;
    public float endurance = 10f;
    public float defense = 5f;
    public float magicResistance = 5f;

    [Header("Special Stats")]
    public float criticalChance = 5f;
    public float criticalDamage = 150f;
    public float attackSpeed = 1f;
    public float castingSpeed = 1f;

    [Header("Leveling")]
    public StatGains baseStatGains;
    public StatMultipliers statMultipliers;

    [Header("Trait System - SINGLE SOURCE OF TRUTH")]
    [Tooltip("Traits this class can select during character creation")]
    public List<Trait> availableTraits = new List<Trait>();
    [Tooltip("Traits that ONLY this class can use")]
    public List<Trait> exclusiveTraits = new List<Trait>();
    [Tooltip("Traits this class starts with for free")]
    public List<Trait> startingTraits = new List<Trait>();
    public int traitPoints = 10;

    [Header("Trait Type Preferences")]
    [Tooltip("Trait types this class has affinity for (reduced cost)")]
    public List<TraitType> preferredTraitTypes = new List<TraitType>();
    [Tooltip("Trait types this class has difficulty with (increased cost)")]
    public List<TraitType> difficultTraitTypes = new List<TraitType>();

    [Header("Cost Modifiers")]
    [Range(0.1f, 2f)]
    [Tooltip("Cost multiplier for preferred trait types")]
    public float preferredCostMultiplier = 0.8f;
    [Range(1f, 3f)]
    [Tooltip("Cost multiplier for difficult trait types")]
    public float difficultCostMultiplier = 1.5f;

    [Header("Class Progression")]
    public ClassProgression progression;

    [Header("Starting Equipment")]
    public List<GameObject> startingItems = new List<GameObject>();
    public List<string> startingSkills = new List<string>();

    [Header("Class Relationships")]
    [Tooltip("Classes that can multiclass with this one")]
    public List<PlayerClass> compatibleClasses = new List<PlayerClass>();
    [Tooltip("Classes that cannot multiclass with this one")]
    public List<PlayerClass> incompatibleClasses = new List<PlayerClass>();

    [Header("Audio & Visual")]
    public AudioClip classSelectionSound;
    public GameObject classVisualEffect;

    // Properties
    public string GetClassName() => string.IsNullOrEmpty(className) ? name : className;

    // TRAIT METHODS - SINGLE SOURCE OF TRUTH
    public List<Trait> GetAllAvailableTraits()
    {
        var allTraits = new List<Trait>(availableTraits);
        allTraits.AddRange(exclusiveTraits);
        allTraits.AddRange(startingTraits);
        return allTraits.Distinct().Where(t => t != null).ToList();
    }

    public List<Trait> GetSelectableTraits()
    {
        // Traits that can be selected during character creation (excluding starting traits)
        var selectable = new List<Trait>(availableTraits);
        selectable.AddRange(exclusiveTraits);
        return selectable.Distinct().Where(t => t != null && !startingTraits.Contains(t)).ToList();
    }

    public List<Trait> GetExclusiveTraits()
    {
        return exclusiveTraits.Where(t => t != null).ToList();
    }

    public List<Trait> GetStartingTraits()
    {
        return startingTraits.Where(t => t != null).ToList();
    }

    public bool CanSelectTrait(Trait trait)
    {
        if (trait == null) return false;

        // Can select if it's in available traits or exclusive traits, but not if it's already a starting trait
        return (availableTraits.Contains(trait) || exclusiveTraits.Contains(trait)) && !startingTraits.Contains(trait);
    }

    public int GetTraitCost(Trait trait)
    {
        if (trait == null) return 0;

        float baseCost = trait.cost;

        // Apply cost modifiers based on trait type
        if (preferredTraitTypes.Contains(trait.type))
        {
            baseCost *= preferredCostMultiplier;
        }
        else if (difficultTraitTypes.Contains(trait.type))
        {
            baseCost *= difficultCostMultiplier;
        }

        return Mathf.RoundToInt(baseCost);
    }

    // Helper methods for editor
    [ContextMenu("Add All Combat Traits")]
    public void AddAllCombatTraits()
    {
        if (TraitDatabase.Instance != null)
        {
            var combatTraits = TraitDatabase.Instance.GetTraitsByType(TraitType.Combat);
            foreach (var trait in combatTraits)
            {
                if (!availableTraits.Contains(trait))
                    availableTraits.Add(trait);
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [ContextMenu("Add All Survival Traits")]
    public void AddAllSurvivalTraits()
    {
        if (TraitDatabase.Instance != null)
        {
            var survivalTraits = TraitDatabase.Instance.GetTraitsByType(TraitType.Survival);
            foreach (var trait in survivalTraits)
            {
                if (!availableTraits.Contains(trait))
                    availableTraits.Add(trait);
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [ContextMenu("Add All Magic Traits")]
    public void AddAllMagicTraits()
    {
        if (TraitDatabase.Instance != null)
        {
            var magicTraits = TraitDatabase.Instance.GetTraitsByType(TraitType.Magic);
            foreach (var trait in magicTraits)
            {
                if (!availableTraits.Contains(trait))
                    availableTraits.Add(trait);
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [ContextMenu("Clear All Trait Lists")]
    public void ClearAllTraitLists()
    {
        availableTraits.Clear();
        exclusiveTraits.Clear();
        startingTraits.Clear();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void AddAvailableTrait(Trait trait)
    {
        if (trait != null && !availableTraits.Contains(trait))
        {
            availableTraits.Add(trait);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public void RemoveAvailableTrait(Trait trait)
    {
        if (availableTraits.Remove(trait))
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public void AddExclusiveTrait(Trait trait)
    {
        if (trait != null && !exclusiveTraits.Contains(trait))
        {
            exclusiveTraits.Add(trait);
            // Remove from available traits if it's there (exclusive traits don't need to be in both lists)
            availableTraits.Remove(trait);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public void AddStartingTrait(Trait trait)
    {
        if (trait != null && !startingTraits.Contains(trait))
        {
            startingTraits.Add(trait);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    // Progression methods
    public LevelMilestone GetMilestoneForLevel(int level)
    {
        return progression.milestones.FirstOrDefault(m => m.level == level);
    }

    public List<LevelMilestone> GetMilestonesUpToLevel(int level)
    {
        return progression.milestones.Where(m => m.level <= level).ToList();
    }

    public List<Trait> GetUnlockedTraitsForLevel(int level)
    {
        var unlockedTraits = new List<Trait>();
        var milestones = GetMilestonesUpToLevel(level);

        foreach (var milestone in milestones)
        {
            unlockedTraits.AddRange(milestone.unlockedTraits);
        }

        return unlockedTraits.Distinct().ToList();
    }

    public List<PassiveAbility> GetUnlockedAbilitiesForLevel(int level)
    {
        var unlockedAbilities = new List<PassiveAbility>();
        var milestones = GetMilestonesUpToLevel(level);

        foreach (var milestone in milestones)
        {
            unlockedAbilities.AddRange(milestone.unlockedAbilities);
        }

        // Add base passive abilities
        unlockedAbilities.AddRange(progression.passiveAbilities);

        return unlockedAbilities.Distinct().ToList();
    }

    // Stat gain methods
    public StatGains GetStatGainsPerLevel() => baseStatGains;

    public float GetStatUpgradeAmount(string statName)
    {
        var baseGain = GetBaseStatGain(statName);
        var multiplier = statMultipliers.GetMultiplier(statName);
        return baseGain * multiplier;
    }

    private float GetBaseStatGain(string statName)
    {
        return statName.ToLower() switch
        {
            "health" => baseStatGains.healthGain,
            "stamina" => baseStatGains.staminaGain,
            "mana" => baseStatGains.manaGain,
            "speed" => baseStatGains.speedGain,
            "strength" => baseStatGains.strengthGain,
            "agility" => baseStatGains.agilityGain,
            "intelligence" => baseStatGains.intelligenceGain,
            "endurance" => baseStatGains.enduranceGain,
            "hunger" => baseStatGains.hungerGain,
            "thirst" => baseStatGains.thirstGain,
            "sleep" => baseStatGains.sleepGain,
            "sanity" => baseStatGains.sanityGain,
            "weight" => baseStatGains.weightGain,
            _ => 1f
        };
    }

    // Equipment and skills
    public List<GameObject> GetStartingItems() => new List<GameObject>(startingItems);
    public List<string> GetStartingSkills() => new List<string>(startingSkills);

    // Class compatibility
    public bool IsCompatibleWith(PlayerClass otherClass)
    {
        if (otherClass == null) return false;
        if (incompatibleClasses.Contains(otherClass)) return false;
        if (compatibleClasses.Count > 0 && !compatibleClasses.Contains(otherClass)) return false;
        return true;
    }

    // Get formatted class description with traits and progression info
    public string GetFormattedDescription()
    {
        string description = classDescription;

        if (preferredTraitTypes.Count > 0)
        {
            description += $"\n\nStrengths: {string.Join(", ", preferredTraitTypes)}";
        }

        if (difficultTraitTypes.Count > 0)
        {
            description += $"\nWeaknesses: {string.Join(", ", difficultTraitTypes)}";
        }

        if (startingTraits.Count > 0)
        {
            description += $"\n\nStarting Traits:\n{string.Join("\n", startingTraits.Where(t => t != null).Select(t => $"• {t.Name}"))}";
        }

        if (exclusiveTraits.Count > 0)
        {
            description += $"\n\nExclusive Traits:\n{string.Join("\n", exclusiveTraits.Where(t => t != null).Select(t => $"• {t.Name}"))}";
        }

        if (progression.milestones.Count > 0)
        {
            description += $"\n\nKey Milestones:";
            foreach (var milestone in progression.milestones.Take(3))
            {
                description += $"\nLevel {milestone.level}: {milestone.milestoneTitle}";
            }
        }

        return description;
    }

    private void OnValidate()
    {
        // Auto-set className to asset name if empty
        if (string.IsNullOrEmpty(className))
            className = name;

        // Initialize components if null
        if (baseStatGains == null)
            baseStatGains = new StatGains();

        if (statMultipliers == null)
            statMultipliers = new StatMultipliers();

        if (progression == null)
            progression = new ClassProgression();

        // Only remove null traits in build, not in editor (so you can assign them)
#if !UNITY_EDITOR
        if (availableTraits != null)
            availableTraits.RemoveAll(t => t == null);
        if (exclusiveTraits != null)
            exclusiveTraits.RemoveAll(t => t == null);
        if (startingTraits != null)
            startingTraits.RemoveAll(t => t == null);
#endif

        // Helpful validation in editor
#if UNITY_EDITOR
        if (availableTraits.Count == 0 && exclusiveTraits.Count == 0)
        {
            Debug.LogWarning($"PlayerClass '{className}' has no available traits! Right-click to add traits by type, or manually assign them in the inspector.");
        }
#endif
    }
}

