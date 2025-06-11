using System.Collections.Generic;
using System.Linq;
using UnityEngine;





[CreateAssetMenu(fileName = "New Armor Set", menuName = "Scriptable Objects/Armor Set")]
public class ArmorSet : ScriptableObject
{
    [Header("Set Information")]
    [SerializeField] private string setName;
    [TextArea(3, 5)]
    [SerializeField] private string setDescription;
    [SerializeField] private Sprite setIcon;
    [SerializeField] private Color setColor = Color.white;

    [Header("Set Pieces")]
    [SerializeField] private List<ArmorSO> setPieces = new List<ArmorSO>();
    [SerializeField] private List<ArmorSlotType> requiredSlotTypes = new List<ArmorSlotType>();

    [Header("Set Effects")]
    [SerializeField] private List<ArmorSetEffect> setEffects = new List<ArmorSetEffect>();

    [Header("Set Completion")]
    [SerializeField] private int minimumPiecesForSet = 2;
    [SerializeField] private int maximumSetPieces = 6;
    [SerializeField] private bool requiresAllPiecesToComplete = false;

    [Header("Audio & Visual")]
    [SerializeField] private AudioClip setCompleteSound;
    [SerializeField] private GameObject setCompleteEffect;

    // Properties
    public string SetName => string.IsNullOrEmpty(setName) ? name : setName;
    public string SetDescription => setDescription;
    public Sprite SetIcon => setIcon;
    public Color SetColor => setColor;
    public List<ArmorSO> SetPieces => setPieces;
    public List<ArmorSlotType> RequiredSlotTypes => requiredSlotTypes;
    public List<ArmorSetEffect> SetEffects => setEffects;
    public int MinimumPiecesForSet => minimumPiecesForSet;
    public int MaximumSetPieces => maximumSetPieces;
    public bool RequiresAllPiecesToComplete => requiresAllPiecesToComplete;
    public AudioClip SetCompleteSound => setCompleteSound;
    public GameObject SetCompleteEffect => setCompleteEffect;

    // Check if this set contains a specific armor piece
    public bool ContainsPiece(ArmorSO armorPiece)
    {
        return setPieces != null && setPieces.Contains(armorPiece);
    }

    // Add a piece to the set
    public void AddPiece(ArmorSO armorPiece)
    {
        if (armorPiece != null && !ContainsPiece(armorPiece))
        {
            setPieces.Add(armorPiece);
        }
    }

    // Remove a piece from the set
    public void RemovePiece(ArmorSO armorPiece)
    {
        if (setPieces != null)
        {
            setPieces.Remove(armorPiece);
        }
    }

    // Get all effects that should be active for a given number of pieces
    public List<ArmorSetEffect> GetActiveEffects(int equippedPieces)
    {
        return setEffects.Where(effect => effect.ShouldBeActive(equippedPieces)).ToList();
    }

    // Get the next effect threshold
    public int GetNextEffectThreshold(int currentPieces)
    {
        return setEffects
            .Where(effect => effect.piecesRequired > currentPieces)
            .OrderBy(effect => effect.piecesRequired)
            .FirstOrDefault()?.piecesRequired ?? -1;
    }

    // Check if the set is complete
    public bool IsSetComplete(int equippedPieces)
    {
        if (requiresAllPiecesToComplete)
        {
            return equippedPieces >= setPieces.Count;
        }
        return equippedPieces >= minimumPiecesForSet;
    }

    // Get completion percentage
    public float GetCompletionPercentage(int equippedPieces)
    {
        int totalRequired = requiresAllPiecesToComplete ? setPieces.Count : minimumPiecesForSet;
        if (totalRequired == 0) return 0f;
        return Mathf.Clamp01((float)equippedPieces / totalRequired);
    }

    // Get formatted set information for display
    public string GetSetInfo(int currentPieces = 0)
    {
        string info = $"{SetName}\n{SetDescription}";

        if (currentPieces > 0)
        {
            info += $"\n\nEquipped: {currentPieces}/{setPieces.Count} pieces";

            var activeEffects = GetActiveEffects(currentPieces);
            if (activeEffects.Count > 0)
            {
                info += "\n\nActive Effects:";
                foreach (var effect in activeEffects)
                {
                    info += $"\n• {effect.effectName}";
                }
            }

            int nextThreshold = GetNextEffectThreshold(currentPieces);
            if (nextThreshold > 0)
            {
                info += $"\n\nNext bonus at {nextThreshold} pieces";
            }
        }

        return info;
    }

    // Get formatted set information for display
    public string GetFormattedSetInfo(int currentPieces = 0)
    {
        string info = $"{SetName}\n{SetDescription}";

        if (currentPieces > 0)
        {
            info += $"\n\nEquipped: {currentPieces}/{setPieces.Count} pieces";

            var activeEffects = GetActiveEffects(currentPieces);
            if (activeEffects.Count > 0)
            {
                info += "\n\nActive Effects:";
                foreach (var effect in activeEffects)
                {
                    info += $"\n• {effect.effectName}";
                }
            }

            int nextThreshold = GetNextEffectThreshold(currentPieces);
            if (nextThreshold > 0)
            {
                info += $"\n\nNext bonus at {nextThreshold} pieces";
            }
        }

        return info;
    }

    // Get missing slot types for this set
    public List<ArmorSlotType> GetMissingSlotTypes(List<ArmorSO> equippedPieces)
    {
        var equippedSlotTypes = equippedPieces.Where(p => p != null).Select(p => p.ArmorSlotType).ToList();
        return setPieces.Where(p => p != null)
                       .Select(p => p.ArmorSlotType)
                       .Except(equippedSlotTypes)
                       .ToList();
    }

    // Get pieces by slot type
    public ArmorSO GetPieceBySlotType(ArmorSlotType slotType)
    {
        return setPieces.FirstOrDefault(piece => piece != null && piece.ArmorSlotType == slotType);
    }

    // Validation
    private void OnValidate()
    {
        // Auto-set set name
        if (string.IsNullOrEmpty(setName))
        {
            setName = name;
        }

        // Validate minimum pieces
        if (minimumPiecesForSet < 1)
        {
            minimumPiecesForSet = 1;
        }

        // Validate maximum pieces
        if (maximumSetPieces < minimumPiecesForSet)
        {
            maximumSetPieces = minimumPiecesForSet;
        }

        // Ensure set pieces don't exceed maximum
        if (setPieces.Count > maximumSetPieces)
        {
            Debug.LogWarning($"Set {setName} has more pieces ({setPieces.Count}) than maximum ({maximumSetPieces})");
        }

        // Validate set effects
        if (setEffects != null)
        {
            for (int i = setEffects.Count - 1; i >= 0; i--)
            {
                var effect = setEffects[i];
                if (effect == null)
                {
                    Debug.LogWarning($"Removing null effect at index {i} in set {setName}");
                    setEffects.RemoveAt(i);
                    continue;
                }

                if (effect.piecesRequired > setPieces.Count)
                {
                    Debug.LogWarning($"Set effect '{effect.effectName}' requires {effect.piecesRequired} pieces but set only has {setPieces.Count}");
                }

                if (effect.piecesRequired < 1)
                {
                    Debug.LogWarning($"Set effect '{effect.effectName}' requires less than 1 piece");
                }

                if (!effect.HasEffects())
                {
                    Debug.LogWarning($"Set effect '{effect.effectName}' has no actual effects defined");
                }
            }
        }

        // Auto-populate required slot types from set pieces
        if (setPieces.Count > 0 && requiredSlotTypes.Count == 0)
        {
            requiredSlotTypes = setPieces.Where(piece => piece != null)
                                        .Select(piece => piece.ArmorSlotType)
                                        .Distinct()
                                        .ToList();
        }

        // Clean up null pieces
        if (setPieces != null)
        {
            setPieces.RemoveAll(piece => piece == null);
        }
    }

    // Editor helper methods
    [ContextMenu("Auto-populate Required Slot Types")]
    private void AutoPopulateSlotTypes()
    {
        requiredSlotTypes = setPieces.Where(piece => piece != null)
                                    .Select(piece => piece.ArmorSlotType)
                                    .Distinct()
                                    .ToList();
    }

    [ContextMenu("Validate Set Pieces")]
    private void ValidateSetPieces()
    {
        foreach (var piece in setPieces)
        {
            if (piece != null && piece.BelongsToSet != this)
            {
                Debug.LogWarning($"Armor piece {piece.name} belongs to set but doesn't reference this set");
            }
        }
    }

    [ContextMenu("Clean Null References")]
    private void CleanNullReferences()
    {
        int originalCount = setPieces.Count;
        setPieces.RemoveAll(piece => piece == null);

        int removedCount = originalCount - setPieces.Count;
        if (removedCount > 0)
        {
            Debug.Log($"Removed {removedCount} null armor piece references from {setName}");
        }

        // Also clean effects
        if (setEffects != null)
        {
            int originalEffectCount = setEffects.Count;
            setEffects.RemoveAll(effect => effect == null);

            int removedEffectCount = originalEffectCount - setEffects.Count;
            if (removedEffectCount > 0)
            {
                Debug.Log($"Removed {removedEffectCount} null effect references from {setName}");
            }
        }
    }
}





//// Legacy compatibility class - redirects to ArmorSet
//[CreateAssetMenu(fileName = "New Armor Set SO", menuName = "Scriptable Objects/Legacy/Armor Set SO")]
//public class ArmorSetSO : ScriptableObject
//{
//    [Header("Legacy ArmorSetSO")]
//    [SerializeField] private string setName;
//    [SerializeField] private List<EquippableSO> setItems = new List<EquippableSO>();
//    [SerializeField] private List<ArmorSetEffect> setEffects = new List<ArmorSetEffect>();

//    // Properties for compatibility
//    public string GetSetName() => string.IsNullOrEmpty(setName) ? name : setName;
//    public List<EquippableSO> GetSetItems() => setItems ?? new List<EquippableSO>();
//    public List<ArmorSetEffect> GetSetEffects() => setEffects ?? new List<ArmorSetEffect>();

//    // Compatibility methods
//    public bool ContainsItem(EquippableSO item)
//    {
//        return setItems != null && setItems.Contains(item);
//    }

//    public int GetSetPieceCount()
//    {
//        return setItems?.Count ?? 0;
//    }

//    public bool IsSetComplete(int equippedCount)
//    {
//        return equippedCount >= GetSetPieceCount();
//    }

//    public List<ArmorSetEffect> GetActiveEffects(int equippedCount)
//    {
//        if (setEffects == null) return new List<ArmorSetEffect>();
//        return setEffects.Where(effect => effect.ShouldBeActive(equippedCount)).ToList();
//    }

//    // Migration helper to convert to new ArmorSet
//    public ArmorSet ConvertToArmorSet()
//    {
//        var newArmorSet = CreateInstance<ArmorSet>();
//        newArmorSet.name = GetSetName();

//        // Convert EquippableSO items to ArmorSO items
//        var armorPieces = new List<ArmorSO>();
//        foreach (var item in setItems)
//        {
//            if (item is ArmorSO armor)
//            {
//                armorPieces.Add(armor);
//            }
//        }

//        // Note: Futurely  need to set the properties on the new ArmorSet

//        return newArmorSet;
//    }

//    private void OnValidate()
//    {
//        if (string.IsNullOrEmpty(setName))
//            setName = name;

//        // Remove null items
//        if (setItems != null)
//            setItems.RemoveAll(item => item == null);

//        // Warn about deprecated class
//        Debug.LogWarning($"ArmorSetSO '{setName}' is deprecated. Please migrate to ArmorSet for full functionality.");
//    }
//}