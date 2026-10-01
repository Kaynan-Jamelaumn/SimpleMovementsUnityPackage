using System.Collections.Generic;

/// <summary>
/// Inspector/debug view of one worn armor set: the pieces worn and the bonus tiers active. Filled by the
/// <see cref="ArmorSetManager"/> after every equipment change (it is not the source of truth).
/// </summary>
[System.Serializable]
public class ArmorSetTracker
{
    public ArmorSet armorSet;
    public List<ArmorSO> equippedPieces = new List<ArmorSO>();
    public List<ArmorSetEffect> activeEffects = new List<ArmorSetEffect>();

    /// <summary>Different pieces worn.</summary>
    public int equippedCount => equippedPieces.Count;
    public bool isSetComplete => armorSet != null && armorSet.IsSetComplete(equippedCount);

    public void AddPiece(ArmorSO piece)
    {
        if (piece != null && !equippedPieces.Contains(piece))
            equippedPieces.Add(piece);
    }

    public void RemovePiece(ArmorSO piece)
    {
        equippedPieces.Remove(piece);
    }

    /// <summary>Recomputes the tiers reached with the pieces listed (Upgrade Groups respected).</summary>
    public void UpdateActiveEffects()
    {
        activeEffects.Clear();
        if (armorSet != null)
            armorSet.GetActiveEffects(equippedCount, activeEffects);
    }
}
