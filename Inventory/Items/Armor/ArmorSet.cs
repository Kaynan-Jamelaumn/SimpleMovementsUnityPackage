using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// An armor set: its pieces and its bonus tiers (<see cref="ArmorSetEffect"/>), e.g. 2/4 pieces: +20 Defense,
/// 3/4: +15% Fire Resistance, 4/4: a trait and an ability. The <see cref="ArmorSetManager"/> counts the different pieces
/// the character wears and turns tiers on and off automatically.
/// </summary>
/// <remarks>
/// This file used to be named ArmorSetSO.cs; Unity needs a ScriptableObject's file to carry its class name, so it was
/// renamed (keeping the same .meta GUID, existing set assets keep working).
/// </remarks>
[CreateAssetMenu(fileName = "New Armor Set", menuName = "Scriptable Objects/Armor Set")]
public class ArmorSet : ScriptableObject
{
    [Header("Set Information")]
    [Tooltip("Name shown in tooltips and the set UI (empty = the asset name).")]
    [SerializeField] private string setName;
    [TextArea(3, 5)]
    [SerializeField] private string setDescription;
    [SerializeField] private Sprite setIcon;
    [Tooltip("Colour used for the set's name in the UI.")]
    [SerializeField] private Color setColor = Color.white;

    [Header("Set Pieces")]
    [Tooltip("The armor pieces of the set. Each piece must also reference this set in its 'Belongs To Armor Set' field.")]
    [SerializeField] private List<ArmorSO> setPieces = new List<ArmorSO>();
    [Tooltip("Slots the set covers (filled from the pieces when empty; informative).")]
    [SerializeField] private List<ArmorSlotType> requiredSlotTypes = new List<ArmorSlotType>();

    [Header("Set Effects")]
    [Tooltip("Bonus tiers. Each activates at its 'Pieces Required'. Tiers with the same Upgrade Group replace each other.")]
    [SerializeField] private List<ArmorSetEffect> setEffects = new List<ArmorSetEffect>();

    [Header("Set Completion")]
    [Tooltip("Pieces needed for the set to count as 'active' in the UI (bonuses use their own Pieces Required).")]
    [SerializeField] private int minimumPiecesForSet = 2;
    [Tooltip("Maximum number of pieces the set can have (validation).")]
    [SerializeField] private int maximumSetPieces = 6;
    [Tooltip("On: the set is complete only when every piece is worn. Off: when the highest bonus tier is reached.")]
    [SerializeField] private bool requiresAllPiecesToComplete = false;

    [Header("Audio & Visual")]
    [Tooltip("Played when the set becomes complete.")]
    [SerializeField] private AudioClip setCompleteSound;
    [Tooltip("Spawned on the wearer while the set is complete.")]
    [SerializeField] private GameObject setCompleteEffect;

    /// <summary>Pieces needed for the highest bonus tier (the number of pieces when there are no tiers).</summary>
    public int RequiredPiecesForFullSet
    {
        get
        {
            int maxRequired = 0;
            if (setEffects != null)
                foreach (var effect in setEffects)
                    if (effect != null && effect.piecesRequired > maxRequired)
                        maxRequired = effect.piecesRequired;
            if (maxRequired > 0)
                return maxRequired;
            int count = setPieces != null ? setPieces.Count(p => p != null) : 0;
            return count > 0 ? count : 1;
        }
    }

    /// <summary>Pieces needed for the set to be complete (all pieces or the highest tier).</summary>
    public int PiecesForCompletion => requiresAllPiecesToComplete ? Mathf.Max(1, setPieces.Count(p => p != null)) : RequiredPiecesForFullSet;

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
        return armorPiece != null && setPieces != null && setPieces.Contains(armorPiece);
    }

    // Add a piece to the set
    public void AddPiece(ArmorSO armorPiece)
    {
        if (armorPiece != null && !ContainsPiece(armorPiece))
            setPieces.Add(armorPiece);
    }

    // Remove a piece from the set
    public void RemovePiece(ArmorSO armorPiece)
    {
        setPieces?.Remove(armorPiece);
    }

    /// <summary>How many DIFFERENT pieces of this set are in <paramref name="worn"/> (two copies of a ring count once).</summary>
    public int CountPieces(IEnumerable<ItemSO> worn)
    {
        if (worn == null || setPieces == null)
            return 0;
        var seen = new HashSet<ArmorSO>();
        foreach (ItemSO item in worn)
            if (item is ArmorSO a && setPieces.Contains(a))
                seen.Add(a);
        return seen.Count;
    }

    /// <summary>
    /// The bonus tiers active with <paramref name="equippedPieces"/> pieces: every reached tier, except that of tiers
    /// sharing an Upgrade Group only the highest reached one counts.
    /// </summary>
    public List<ArmorSetEffect> GetActiveEffects(int equippedPieces)
    {
        var result = new List<ArmorSetEffect>();
        GetActiveEffects(equippedPieces, result);
        return result;
    }

    public void GetActiveEffects(int equippedPieces, List<ArmorSetEffect> into)
    {
        if (setEffects == null)
            return;
        for (int i = 0; i < setEffects.Count; i++)
        {
            ArmorSetEffect e = setEffects[i];
            if (e == null || !e.ShouldBeActive(equippedPieces))
                continue;
            if (!string.IsNullOrEmpty(e.upgradeGroup) && IsSupersededInGroup(e, equippedPieces))
                continue;
            into.Add(e);
        }
    }

    private bool IsSupersededInGroup(ArmorSetEffect e, int equippedPieces)
    {
        for (int j = 0; j < setEffects.Count; j++)
        {
            ArmorSetEffect o = setEffects[j];
            if (o == null || o == e || o.upgradeGroup != e.upgradeGroup || !o.ShouldBeActive(equippedPieces))
                continue;
            // A higher tier of the same group wins; on equal requirements the later one in the list wins.
            if (o.piecesRequired > e.piecesRequired || (o.piecesRequired == e.piecesRequired && j > setEffects.IndexOf(e)))
                return true;
        }
        return false;
    }

    // Get the next effect threshold
    public int GetNextEffectThreshold(int currentPieces)
    {
        return setEffects
            .Where(effect => effect != null && effect.piecesRequired > currentPieces)
            .OrderBy(effect => effect.piecesRequired)
            .FirstOrDefault()?.piecesRequired ?? -1;
    }

    /// <summary>Is the set complete (all pieces, or the highest tier, see <see cref="RequiresAllPiecesToComplete"/>)?</summary>
    public bool IsSetComplete(int equippedPieces) => equippedPieces >= PiecesForCompletion;

    /// <summary>Does the set count as worn (at least Minimum Pieces For Set)?</summary>
    public bool IsSetActive(int equippedPieces) => equippedPieces >= Mathf.Max(1, minimumPiecesForSet);

    // Get completion percentage
    public float GetCompletionPercentage(int equippedPieces)
    {
        int totalRequired = PiecesForCompletion;
        if (totalRequired <= 0) return 0f;
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
                    info += $"\n• {effect.effectName}";
            }

            int nextThreshold = GetNextEffectThreshold(currentPieces);
            if (nextThreshold > 0)
                info += $"\n\nNext bonus at {nextThreshold} pieces";
        }

        return info;
    }

    // Get formatted set information for display
    public string GetFormattedSetInfo(int currentPieces = 0) => GetSetInfo(currentPieces);

    /// <summary>Every tier with its state for tooltips: "(2) Warrior's Vigor: +20 Defense" (active tiers marked).</summary>
    public List<string> DescribeTiers(int currentPieces)
    {
        var lines = new List<string>();
        var active = GetActiveEffects(currentPieces);
        foreach (ArmorSetEffect e in setEffects.Where(x => x != null).OrderBy(x => x.piecesRequired))
        {
            string state = active.Contains(e) ? "✓" : e.ShouldBeActive(currentPieces) ? "↑" : " ";
            string what = string.Join(", ", e.DescribeLines());
            lines.Add($"{state} ({e.piecesRequired}) {e.effectName}{(string.IsNullOrEmpty(what) ? "" : ": " + what)}");
        }
        return lines;
    }

    /// <summary>Configuration problems of the set and its bonuses (inspector, validation window).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (setPieces == null || setPieces.Count == 0)
            errors.Add("The set has no pieces.");
        else
        {
            if (setPieces.Any(p => p == null))
                warnings.Add("'Set Pieces' has empty entries.");
            foreach (var dup in setPieces.Where(p => p != null).GroupBy(p => p).Where(g => g.Count() > 1))
                warnings.Add($"'{dup.Key.name}' is listed {dup.Count()} times (it still counts once).");
            foreach (ArmorSO piece in setPieces.Where(p => p != null))
                if (piece.BelongsToSet != this)
                    errors.Add($"'{piece.name}' is in this set's pieces but its 'Belongs To Armor Set' is {(piece.BelongsToSet != null ? $"'{piece.BelongsToSet.name}'" : "empty")}.");
            if (setPieces.Count > maximumSetPieces)
                warnings.Add($"The set has {setPieces.Count} pieces, more than Maximum Set Pieces ({maximumSetPieces}).");
        }

        if (setEffects == null || setEffects.Count == 0)
            warnings.Add("The set has no bonuses.");
        else
        {
            int pieceCount = setPieces != null ? setPieces.Where(p => p != null).Distinct().Count() : 0;
            for (int i = 0; i < setEffects.Count; i++)
            {
                ArmorSetEffect e = setEffects[i];
                if (e == null)
                {
                    warnings.Add($"Bonus {i + 1} is empty.");
                    continue;
                }
                if (e.piecesRequired > pieceCount)
                    errors.Add($"'{e.effectName}' needs {e.piecesRequired} pieces but the set only has {pieceCount}: it can never activate.");
                e.Validate(errors, warnings);
            }
        }

        if (minimumPiecesForSet < 1)
            errors.Add("Minimum Pieces For Set must be at least 1.");
    }

    // Validation: only safe automatic fixes here (messages are shown by the inspector and the validation window).
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(setName))
            setName = name;
        if (minimumPiecesForSet < 1)
            minimumPiecesForSet = 1;
        if (maximumSetPieces < minimumPiecesForSet)
            maximumSetPieces = minimumPiecesForSet;
        if (setPieces == null)
            setPieces = new List<ArmorSO>();
        if (setEffects == null)
            setEffects = new List<ArmorSetEffect>();

        // Auto-populate required slot types from set pieces
        if (setPieces.Count > 0 && (requiredSlotTypes == null || requiredSlotTypes.Count == 0))
            AutoPopulateSlotTypes();
    }

    // Context menu helpers
    [ContextMenu("Auto-populate Required Slot Types")]
    private void AutoPopulateSlotTypes()
    {
        requiredSlotTypes = setPieces.Where(piece => piece != null)
                                    .Select(piece => piece.ArmorSlotType)
                                    .Distinct()
                                    .ToList();
    }

    [ContextMenu("Validate Set")]
    private void ValidateSetPieces()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        Validate(errors, warnings);
        foreach (string e in errors) Debug.LogError($"[Armor Set '{SetName}'] {e}", this);
        foreach (string w in warnings) Debug.LogWarning($"[Armor Set '{SetName}'] {w}", this);
        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log($"[Armor Set '{SetName}'] No problems found.", this);
    }

    [ContextMenu("Clean Null References")]
    private void CleanNullReferences()
    {
        int removed = setPieces.RemoveAll(piece => piece == null);
        int removedEffects = setEffects != null ? setEffects.RemoveAll(effect => effect == null) : 0;
        if (removed + removedEffects > 0)
            Debug.Log($"Removed {removed} empty piece(s) and {removedEffects} empty bonus(es) from {SetName}", this);
    }
}
