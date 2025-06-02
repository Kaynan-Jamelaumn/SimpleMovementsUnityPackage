using System.Collections.Generic;
using System.Linq;
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
public class ClassTraitRestrictions
{
    [Header("Available Traits")]
    [Tooltip("Traits that this class can select from")]
    public List<Trait> availableTraits = new List<Trait>();

    [Header("Forbidden Traits")]
    [Tooltip("Traits that this class cannot select")]
    public List<Trait> forbiddenTraits = new List<Trait>();

    [Header("Exclusive Traits")]
    [Tooltip("Traits that only this class can select")]
    public List<Trait> exclusiveTraits = new List<Trait>();

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
}

[System.Serializable]
public class ClassProgression
{
    [Header("Level Milestones")]
    public List<LevelMilestone> milestones = new List<LevelMilestone>();

    [Header("Skill Trees")]
    public List<SkillTree> skillTrees = new List<SkillTree>();

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
public class SkillTree
{
    public string treeName;
    public TraitType associatedTraitType;
    public List<Trait> skills = new List<Trait>();
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

[CreateAssetMenu(fileName = "New  Player Class", menuName = "Scriptable Objects/Player Class")]
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

    [Header("Trait System")]
    public ClassTraitRestrictions traitRestrictions;
    public List<Trait> startingTraits = new List<Trait>();
    public int traitPoints = 10;

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

    // Trait-related methods
    public bool CanSelectTrait(Trait trait)
    {
        if (trait == null) return false;

        // Check if trait is forbidden
        if (traitRestrictions.forbiddenTraits.Contains(trait))
            return false;

        // Check if trait is available (if list is specified)
        if (traitRestrictions.availableTraits.Count > 0 &&
            !traitRestrictions.availableTraits.Contains(trait))
            return false;

        return true;
    }

    public int GetTraitCost(Trait trait)
    {
        if (trait == null) return 0;

        float baseCost = trait.cost;

        // Apply cost modifiers based on trait type
        if (traitRestrictions.preferredTraitTypes.Contains(trait.type))
        {
            baseCost *= traitRestrictions.preferredCostMultiplier;
        }
        else if (traitRestrictions.difficultTraitTypes.Contains(trait.type))
        {
            baseCost *= traitRestrictions.difficultCostMultiplier;
        }

        return Mathf.RoundToInt(baseCost);
    }

    public List<Trait> GetAvailableTraits()
    {
        var available = new List<Trait>();

        // Add starting traits
        available.AddRange(startingTraits);

        // Add available traits (if specified)
        if (traitRestrictions.availableTraits.Count > 0)
        {
            available.AddRange(traitRestrictions.availableTraits);
        }

        // Add exclusive traits
        available.AddRange(traitRestrictions.exclusiveTraits);

        // Remove forbidden traits
        available.RemoveAll(t => traitRestrictions.forbiddenTraits.Contains(t));

        return available.Distinct().ToList();
    }

    public List<Trait> GetExclusiveTraits()
    {
        return new List<Trait>(traitRestrictions.exclusiveTraits);
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

    // Class strengths and weaknesses analysis
    public Dictionary<string, float> GetStatProfile()
    {
        return new Dictionary<string, float>
        {
            ["Health"] = health,
            ["Stamina"] = stamina,
            ["Mana"] = mana,
            ["Speed"] = speed,
            ["Strength"] = strength,
            ["Agility"] = agility,
            ["Intelligence"] = intelligence,
            ["Endurance"] = endurance,
            ["Defense"] = defense,
            ["Magic Resistance"] = magicResistance
        };
    }

    public List<TraitType> GetStrengths()
    {
        return traitRestrictions.preferredTraitTypes;
    }

    public List<TraitType> GetWeaknesses()
    {
        return traitRestrictions.difficultTraitTypes;
    }

    // Validation
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(GetClassName()) &&
               baseStatGains != null &&
               statMultipliers != null &&
               traitRestrictions != null;
    }

    // Get formatted class description with traits and progression info
    public string GetFormattedDescription()
    {
        string description = classDescription;

        if (traitRestrictions.preferredTraitTypes.Count > 0)
        {
            description += $"\n\nStrengths: {string.Join(", ", traitRestrictions.preferredTraitTypes)}";
        }

        if (traitRestrictions.difficultTraitTypes.Count > 0)
        {
            description += $"\nWeaknesses: {string.Join(", ", traitRestrictions.difficultTraitTypes)}";
        }

        if (startingTraits.Count > 0)
        {
            description += $"\n\nStarting Traits:\n{string.Join("\n", startingTraits.Select(t => $"• {t.Name}"))}";
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

        if (traitRestrictions == null)
            traitRestrictions = new ClassTraitRestrictions();

        if (progression == null)
            progression = new ClassProgression();
    }
}

//using System.Collections.Generic;
//using UnityEngine;
//[System.Serializable]
//public class StatGains
//{
//    public float healthGain;
//    public float staminaGain;
//    public float manaGain;
//    public float speedGain;
//    public float strengthGain;
//    public float agilityGain;
//    public float intelligenceGain;
//    public float enduranceGain;
//}



//public abstract class BasePlayerClass
//{
//    // **Base Stats**
//    [Header("Base Stats")]
//    public float health = 100f;
//    public float stamina = 100f;
//    public float mana = 100f;
//    public float speed = 5f;
//    public float hunger = 100f;
//    public float thirst = 100f;
//    public float weight = 30f;
//    public float sleep = 100f;
//    public float sanity = 100f;
//    public float bodyHeat = 100f;
//    public float oxygen = 100f;

//    [Header("Combat Stats")]
//    public float strength = 10f;
//    public float agility = 10f;
//    public float intelligence = 10f;
//    public float endurance = 10f;
//    public float defense = 5f;
//    public float magicResistance = 5f;

//    [Header("Special Stats")]
//    public float criticalChance = 5f;
//    public float criticalDamage = 150f;
//    public float attackSpeed = 1f;
//    public float castingSpeed = 1f;

//    public List<Trait> startingTraits = new List<Trait>();
//    public int traitPoints = 10; // Points to spend on traits

//    public abstract StatGains GetStatGainsPerLevel();
//    public abstract string GetClassName();
//    public abstract List<GameObject> GetStartingItems();
//    public virtual void InitializeStats() { }
//    /*

//    // **Combat Stats**
//    public float strength;        // Physical strength
//    public float defense;         // Physical damage resistance
//    public float agility;         // Dexterity and evasion
//    public float endurance;       // Ability to withstand physical strain
//    public float armorRating;     // Total protection from physical damage
//    public float vitality;        // Health regeneration over time

//    // **Magical Stats**
//    public float intelligence;    // Magical power and problem-solving
//    public float mana;            // Magical energy for spells
//    public float manaRegeneration; // Rate at which mana regenerates
//    public float spellCastingSpeed; // Speed of casting spells
//    public float magicResistance;  // Resistance to magical attacks

//    // **Mental and Social Stats**
//    public float charisma;        // Social influence, bargaining
//    public float morale;          // Mental state, affects performance
//    public float luck;            // Random chance factors

//    // **Combat Stats**
//    public float criticalHitChance; // Chance of dealing a critical hit
//    public float criticalDamage;    // Multiplier for critical damage
//    public float attackSpeed;       // Speed of physical attacks
//    public float rangedDamage;      // Damage from ranged weapons
//    public float blockChance;       // Chance to block incoming damage

//    // **Environmental Stats**
//    public float fireResistance;    // Resistance to fire-based damage
//    public float coldResistance;    // Resistance to cold-based damage
//    public float poisonResistance;  // Resistance to poison effects
//    public float lightResistance;   // Resistance to light-based effects (holy magic)
//    public float waterResistance;   // Resistance to water-based effects
//    public float earthResistance;   // Resistance to earth-based effects
//    public float thunderResistance; // Resistance to thunder-based effects

//    public float waterAffinity;     // Affinity for water-based spells
//    public float earthAffinity;     // Affinity for earth-based spells
//    public float windAffinity;     // Affinity for wind-based spells
//    public float thunderAffinity;   // Affinity for thunder-based spells
//    public float iceAffinity;       // Affinity for ice-based spells
//    public float fireAffinity;      // Affinity for fire-based spells
//    public float poisonAffinity;    // Affinity for poison-based spells
//    public float lightAffinity;     // Affinity for light-based spells
//    public float darkAffinity;      // Affinity for dark-based spells

//    // **Movement & Stealth Stats**
//    public float stealth;           // Stealth ability for sneaking
//    public float jumpHeight;        // Jumping height
//    public float climbSpeed;        // Climbing ability
//    public float swimmingSpeed;     // Swimming ability

//    // **Other Stats**
//    public float honor;             // Personal honor, affects reputation
//    public float sanity;            // Mental stability, influences behavior
//    public float underwaterBreathing;
//    public float miningSkill;
//    public float foragingSkill;
//    public float woodcutting;
//    public float fishingSkill;

//      // **Combat Maneuvers and Abilities Stats**
//    public float counterattackChance;
//    public float dodgeChance;
//    public float knockbackResistance;
//    public float stunResistance;
//    public float interruptResistance;
//    public float disarmResistance;
//        public float buffDuration;
//    public float debuffDuration;
//    public float summonControl;
//    public float cooldownReduction;
//    public float nightVision;          // Ability to see clearly in dark environments
//    public float temperatureTolerance;

//    */
//}
