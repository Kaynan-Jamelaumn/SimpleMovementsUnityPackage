using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "Trait Database", menuName = "Scriptable Objects/Trait Database")]
public class TraitDatabase : ScriptableObject
{
    [Header("All Available Traits")]
    [SerializeField] private List<Trait> allTraits = new List<Trait>();

    private static TraitDatabase instance;
    public static TraitDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                // First try Resources folder
                instance = Resources.Load<TraitDatabase>("TraitDatabase");

                // If not found in Resources, search entire project
                if (instance == null)
                {
#if UNITY_EDITOR
                    string[] guids = AssetDatabase.FindAssets("t:TraitDatabase");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        instance = AssetDatabase.LoadAssetAtPath<TraitDatabase>(path);

                        if (instance != null)
                        {
                            Debug.Log($"TraitDatabase found at: {path}. Consider moving it to Resources folder for runtime access.");
                        }
                    }
#endif
                }

                if (instance == null)
                {
                    Debug.LogError("TraitDatabase not found! Please create one using 'Create > Scriptable Objects > Trait Database'");
                }
            }
            return instance;
        }
    }

    // Basic trait retrieval
    public List<Trait> GetAllTraits()
    {
        return new List<Trait>(allTraits);
    }

    public Trait GetTraitByName(string traitName)
    {
        return allTraits.FirstOrDefault(t => t != null && t.Name == traitName);
    }

    // Get traits by type
    public List<Trait> GetTraitsByType(TraitType type)
    {
        return allTraits.Where(t => t != null && t.type == type).ToList();
    }

    // Get traits by rarity
    public List<Trait> GetTraitsByRarity(TraitRarity rarity)
    {
        return allTraits.Where(t => t != null && t.rarity == rarity).ToList();
    }

    // Cost-based queries
    public List<Trait> GetPositiveTraits()
    {
        return allTraits.Where(t => t != null && t.IsPositive).ToList();
    }

    public List<Trait> GetNegativeTraits()
    {
        return allTraits.Where(t => t != null && t.IsNegative).ToList();
    }

    public List<Trait> GetFreeTraits()
    {
        return allTraits.Where(t => t != null && t.IsFree).ToList();
    }

    public List<Trait> GetTraitsByCostRange(int minCost, int maxCost)
    {
        return allTraits.Where(t => t != null && t.cost >= minCost && t.cost <= maxCost).ToList();
    }

    // Search functionality
    public List<Trait> SearchTraits(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm)) return GetAllTraits();

        searchTerm = searchTerm.ToLower();
        return allTraits.Where(t => t != null &&
            (t.Name.ToLower().Contains(searchTerm) ||
             t.description.ToLower().Contains(searchTerm))
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
        bool basicIncompatible = (trait1.incompatibleTraits?.Contains(trait2) ?? false) ||
                                (trait2.incompatibleTraits?.Contains(trait1) ?? false);

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

    // Management methods for editor
    public void AddTrait(Trait trait)
    {
        if (trait != null && !allTraits.Contains(trait))
        {
            allTraits.Add(trait);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public void RemoveTrait(Trait trait)
    {
        if (allTraits.Remove(trait))
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [ContextMenu("Remove Null Traits")]
    public void RemoveNullTraits()
    {
        allTraits.RemoveAll(t => t == null);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Statistics
    public Dictionary<string, int> GetTraitStatistics()
    {
        return new Dictionary<string, int>
        {
            ["Total Traits"] = allTraits.Count,
            ["Combat Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Combat),
            ["Survival Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Survival),
            ["Magic Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Magic),
            ["Social Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Social),
            ["Crafting Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Crafting),
            ["Movement Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Movement),
            ["Mental Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Mental),
            ["Physical Traits"] = allTraits.Count(t => t != null && t.type == TraitType.Physical),
            ["Positive Traits"] = allTraits.Count(t => t != null && t.IsPositive),
            ["Negative Traits"] = allTraits.Count(t => t != null && t.IsNegative),
            ["Free Traits"] = allTraits.Count(t => t != null && t.IsFree),
            ["Common Traits"] = allTraits.Count(t => t != null && t.rarity == TraitRarity.Common),
            ["Uncommon Traits"] = allTraits.Count(t => t != null && t.rarity == TraitRarity.Uncommon),
            ["Rare Traits"] = allTraits.Count(t => t != null && t.rarity == TraitRarity.Rare),
            ["Epic Traits"] = allTraits.Count(t => t != null && t.rarity == TraitRarity.Epic),
            ["Legendary Traits"] = allTraits.Count(t => t != null && t.rarity == TraitRarity.Legendary)
        };
    }

    private void OnValidate()
    {
        // Remove null entries
        if (allTraits != null)
            allTraits.RemoveAll(t => t == null);
    }
}