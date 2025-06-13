
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TraitEnhancement
{
    [Header("Trait Enhancement Configuration")]
    public Trait originalTrait;
    public Trait enhancedTrait;

    [Header("Enhancement Type")]
    public TraitEnhancementType enhancementType;

    [Header("Value Modifications")]
    [Tooltip("Multiplier for existing trait effects (1.5 = 50% stronger)")]
    public float effectMultiplier = 1.5f;

    [Tooltip("Additional effects to add to the trait")]
    public List<TraitEffect> additionalEffects = new List<TraitEffect>();

    [Tooltip("Custom description for the enhanced version")]
    [TextArea(2, 3)]
    public string enhancedDescription;
}

public enum TraitEnhancementType
{
    Multiply,      // Multiply existing effects by a factor
    AddEffects,    // Add new effects to existing trait
    Replace,       // Replace with entirely different trait
    Upgrade        // Use a completely different enhanced trait
}