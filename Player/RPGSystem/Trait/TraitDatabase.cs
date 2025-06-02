using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Enhanced Trait Database", menuName = "Scriptable Objects /Trait Database")]
public class TraitDatabase : ScriptableObject
{
    [Header("All Available Traits")]
    [SerializeField] private List<Trait> allTraits = new List<Trait>();

    [Header("Organized by Type")]
    [SerializeField] private List<Trait> combatTraits = new List<Trait>();
    [SerializeField] private List<Trait> survivalTraits = new List<Trait>();
    [SerializeField] private List<Trait> magicTraits = new List<Trait>();
    [SerializeField] private List<Trait> socialTraits = new List<Trait>();
    [SerializeField] private List<Trait> craftingTraits = new List<Trait>();
    [SerializeField] private List<Trait> movementTraits = new List<Trait>();
    [SerializeField] private List<Trait> mentalTraits = new List<Trait>();
    [SerializeField] private List<Trait> physicalTraits = new List<Trait>();

    [Header("Organized by Rarity")]
    [SerializeField] private List<Trait> commonTraits = new List<Trait>();
    [SerializeField] private List<Trait> uncommonTraits = new List<Trait>();
    [SerializeField] private List<Trait> rareTraits = new List<Trait>();
    [SerializeField] private List<Trait> epicTraits = new List<Trait>();
    [SerializeField] private List<Trait> legendaryTraits = new List<Trait>();

    [Header("Class-Specific Collections")]
    [SerializeField] private List<ClassTraitCollection> classSpecificTraits = new List<ClassTraitCollection>();

    // Singleton pattern for easy access
    private static TraitDatabase instance;
    public static TraitDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<TraitDatabase>("TraitDatabase");
                if (instance == null)
                {
                    Debug.LogError("TraitDatabase not found in Resources folder! Please create one.");
                }
            }
            return instance;
        }
    }

    [System.Serializable]
    public class ClassTraitCollection
    {
        public PlayerClass playerClass;
        public List<Trait> availableTraits = new List<Trait>();
        public List<Trait> startingTraits = new List<Trait>();
        public List<Trait> exclusiveTraits = new List<Trait>();
    }

    // Basic trait retrieval
    public Trait GetTrait(Trait traitReference)
    {
        return traitReference;
    }

    public Trait GetTraitByName(string traitName)
    {
        return allTraits.FirstOrDefault(t => t.Name == traitName);
    }

    public List<Trait> GetAllTraits()
    {
        return new List<Trait>(allTraits);
    }

    // Get traits by type
    public List<Trait> GetTraitsByType(TraitType type)
    {
        return type switch
        {
            TraitType.Combat => new List<Trait>(combatTraits),
            TraitType.Survival => new List<Trait>(survivalTraits),
            TraitType.Magic => new List<Trait>(magicTraits),
            TraitType.Social => new List<Trait>(socialTraits),
            TraitType.Crafting => new List<Trait>(craftingTraits),
            TraitType.Movement => new List<Trait>(movementTraits),
            TraitType.Mental => new List<Trait>(mentalTraits),
            TraitType.Physical => new List<Trait>(physicalTraits),
            _ => allTraits.Where(t => t.type == type).ToList()
        };
    }

    // Get traits by rarity
    public List<Trait> GetTraitsByRarity(TraitRarity rarity)
    {
        return rarity switch
        {
            TraitRarity.Common => new List<Trait>(commonTraits),
            TraitRarity.Uncommon => new List<Trait>(uncommonTraits),
            TraitRarity.Rare => new List<Trait>(rareTraits),
            TraitRarity.Epic => new List<Trait>(epicTraits),
            TraitRarity.Legendary => new List<Trait>(legendaryTraits),
            _ => allTraits.Where(t => t.rarity == rarity).ToList()
        };
    }

    // Class-specific trait queries
    public List<Trait> GetTraitsForClass(PlayerClass playerClass)
    {
        if (playerClass == null) return new List<Trait>();

        var collection = classSpecificTraits.FirstOrDefault(c => c.playerClass == playerClass);
        if (collection != null)
            return new List<Trait>(collection.availableTraits);

        // Fallback: filter all traits by class availability
        return allTraits.Where(t => t.IsAvailableForClass(playerClass)).ToList();
    }

    public List<Trait> GetStartingTraitsForClass(PlayerClass playerClass)
    {
        if (playerClass == null) return new List<Trait>();

        var collection = classSpecificTraits.FirstOrDefault(c => c.playerClass == playerClass);
        return collection?.startingTraits ?? new List<Trait>();
    }

    public List<Trait> GetExclusiveTraitsForClass(PlayerClass playerClass)
    {
        if (playerClass == null) return new List<Trait>();

        var collection = classSpecificTraits.FirstOrDefault(c => c.playerClass == playerClass);
        return collection?.exclusiveTraits ?? new List<Trait>();
    }

    // Advanced filtering
    public List<Trait> GetTraitsForClassAndType(PlayerClass playerClass, TraitType type)
    {
        return GetTraitsForClass(playerClass).Where(t => t.type == type).ToList();
    }

    public List<Trait> GetTraitsForClassAndRarity(PlayerClass playerClass, TraitRarity rarity)
    {
        return GetTraitsForClass(playerClass).Where(t => t.rarity == rarity).ToList();
    }

    public List<Trait> GetTraitsForLevel(int level)
    {
        return allTraits.Where(t => t.IsAvailableForLevel(level)).ToList();
    }

    public List<Trait> GetTraitsForClassAndLevel(PlayerClass playerClass, int level)
    {
        return GetTraitsForClass(playerClass).Where(t => t.IsAvailableForLevel(level)).ToList();
    }

    // Cost and availability queries
    public List<Trait> GetPositiveTraits()
    {
        return allTraits.Where(t => t.IsPositive).ToList();
    }

    public List<Trait> GetNegativeTraits()
    {
        return allTraits.Where(t => t.IsNegative).ToList();
    }

    public List<Trait> GetFreeTraits()
    {
        return allTraits.Where(t => t.IsFree).ToList();
    }

    public List<Trait> GetTraitsByCostRange(int minCost, int maxCost)
    {
        return allTraits.Where(t => t.cost >= minCost && t.cost <= maxCost).ToList();
    }

    // Search functionality
    public List<Trait> SearchTraits(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm)) return GetAllTraits();

        searchTerm = searchTerm.ToLower();
        return allTraits.Where(t =>
            t.Name.ToLower().Contains(searchTerm) ||
            t.description.ToLower().Contains(searchTerm) ||
            t.effects.Any(e => e.targetStat.ToLower().Contains(searchTerm) ||
                              e.effectDescription.ToLower().Contains(searchTerm))
        ).ToList();
    }

    public List<Trait> SearchTraitsForClass(PlayerClass playerClass, string searchTerm)
    {
        var classTraits = GetTraitsForClass(playerClass);
        if (string.IsNullOrEmpty(searchTerm)) return classTraits;

        searchTerm = searchTerm.ToLower();
        return classTraits.Where(t =>
            t.Name.ToLower().Contains(searchTerm) ||
            t.description.ToLower().Contains(searchTerm)
        ).ToList();
    }

    // Validation methods
    public bool IsValidTrait(Trait trait)
    {
        return trait != null && allTraits.Contains(trait);
    }

    public bool AreTraitsCompatible(Trait trait1, Trait trait2)
    {
        if (trait1 == null || trait2 == null) return false;

        // Check basic incompatibility
        bool basicIncompatible = trait1.incompatibleTraits.Contains(trait2) ||
                                trait2.incompatibleTraits.Contains(trait1);

        // Check mutual exclusivity
        bool mutuallyExclusive = (trait1.mutuallyExclusiveTraits?.Contains(trait2) ?? false) ||
                               (trait2.mutuallyExclusiveTraits?.Contains(trait1) ?? false);

        return !basicIncompatible && !mutuallyExclusive;
    }

    public bool HasRequiredTraits(Trait trait, List<Trait> activeTraits)
    {
        if (trait == null || trait.requiredTraits == null || trait.requiredTraits.Count == 0)
            return true;

        return trait.requiredTraits.All(required => activeTraits.Contains(required));
    }

    // Management methods
    public void AddClassTraitCollection(PlayerClass playerClass)
    {
        if (playerClass == null) return;

        var existing = classSpecificTraits.FirstOrDefault(c => c.playerClass == playerClass);
        if (existing == null)
        {
            classSpecificTraits.Add(new ClassTraitCollection { playerClass = playerClass });
        }
    }

    public void RemoveClassTraitCollection(PlayerClass playerClass)
    {
        classSpecificTraits.RemoveAll(c => c.playerClass == playerClass);
    }

    // Editor utility methods
    [ContextMenu("Organize All Traits")]
    private void OrganizeAllTraits()
    {
        OrganizeTraitsByType();
        OrganizeTraitsByRarity();
        UpdateClassSpecificCollections();
    }

    [ContextMenu("Organize Traits by Type")]
    private void OrganizeTraitsByType()
    {
        combatTraits.Clear();
        survivalTraits.Clear();
        magicTraits.Clear();
        socialTraits.Clear();
        craftingTraits.Clear();
        movementTraits.Clear();
        mentalTraits.Clear();
        physicalTraits.Clear();

        foreach (var trait in allTraits)
        {
            if (trait == null) continue;

            switch (trait.type)
            {
                case TraitType.Combat: combatTraits.Add(trait); break;
                case TraitType.Survival: survivalTraits.Add(trait); break;
                case TraitType.Magic: magicTraits.Add(trait); break;
                case TraitType.Social: socialTraits.Add(trait); break;
                case TraitType.Crafting: craftingTraits.Add(trait); break;
                case TraitType.Movement: movementTraits.Add(trait); break;
                case TraitType.Mental: mentalTraits.Add(trait); break;
                case TraitType.Physical: physicalTraits.Add(trait); break;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Organize Traits by Rarity")]
    private void OrganizeTraitsByRarity()
    {
        commonTraits.Clear();
        uncommonTraits.Clear();
        rareTraits.Clear();
        epicTraits.Clear();
        legendaryTraits.Clear();

        foreach (var trait in allTraits)
        {
            if (trait == null) continue;

            switch (trait.rarity)
            {
                case TraitRarity.Common: commonTraits.Add(trait); break;
                case TraitRarity.Uncommon: uncommonTraits.Add(trait); break;
                case TraitRarity.Rare: rareTraits.Add(trait); break;
                case TraitRarity.Epic: epicTraits.Add(trait); break;
                case TraitRarity.Legendary: legendaryTraits.Add(trait); break;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Update Class-Specific Collections")]
    private void UpdateClassSpecificCollections()
    {
        foreach (var collection in classSpecificTraits)
        {
            if (collection.playerClass == null) continue;

            // Update available traits based on class restrictions
            collection.availableTraits = allTraits
                .Where(t => t.IsAvailableForClass(collection.playerClass))
                .ToList();

            // Update exclusive traits (traits that are ONLY available to this class)
            collection.exclusiveTraits = allTraits
                .Where(t => t.restrictions.allowedClasses.Count == 1 &&
                           t.restrictions.allowedClasses.Contains(collection.playerClass))
                .ToList();
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Remove Null Traits")]
    private void RemoveNullTraits()
    {
        allTraits.RemoveAll(t => t == null);

        // Clean up organized lists
        combatTraits.RemoveAll(t => t == null);
        survivalTraits.RemoveAll(t => t == null);
        magicTraits.RemoveAll(t => t == null);
        socialTraits.RemoveAll(t => t == null);
        craftingTraits.RemoveAll(t => t == null);
        movementTraits.RemoveAll(t => t == null);
        mentalTraits.RemoveAll(t => t == null);
        physicalTraits.RemoveAll(t => t == null);

        commonTraits.RemoveAll(t => t == null);
        uncommonTraits.RemoveAll(t => t == null);
        rareTraits.RemoveAll(t => t == null);
        epicTraits.RemoveAll(t => t == null);
        legendaryTraits.RemoveAll(t => t == null);

        // Clean up class collections
        foreach (var collection in classSpecificTraits)
        {
            collection.availableTraits.RemoveAll(t => t == null);
            collection.startingTraits.RemoveAll(t => t == null);
            collection.exclusiveTraits.RemoveAll(t => t == null);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Validation and statistics
    public Dictionary<string, int> GetTraitStatistics()
    {
        return new Dictionary<string, int>
        {
            ["Total Traits"] = allTraits.Count,
            ["Combat Traits"] = combatTraits.Count,
            ["Survival Traits"] = survivalTraits.Count,
            ["Magic Traits"] = magicTraits.Count,
            ["Social Traits"] = socialTraits.Count,
            ["Crafting Traits"] = craftingTraits.Count,
            ["Movement Traits"] = movementTraits.Count,
            ["Mental Traits"] = mentalTraits.Count,
            ["Physical Traits"] = physicalTraits.Count,
            ["Positive Traits"] = allTraits.Count(t => t.IsPositive),
            ["Negative Traits"] = allTraits.Count(t => t.IsNegative),
            ["Free Traits"] = allTraits.Count(t => t.IsFree),
            ["Common Traits"] = commonTraits.Count,
            ["Uncommon Traits"] = uncommonTraits.Count,
            ["Rare Traits"] = rareTraits.Count,
            ["Epic Traits"] = epicTraits.Count,
            ["Legendary Traits"] = legendaryTraits.Count
        };
    }

    private void OnValidate()
    {
        // Remove null entries
        if (allTraits != null)
            allTraits.RemoveAll(t => t == null);
    }
}