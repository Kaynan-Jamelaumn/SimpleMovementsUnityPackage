using System.Collections.Generic;

[System.Serializable]
public class ArmorSetTracker
{
    public ArmorSet armorSet;
    public List<ArmorSO> equippedPieces = new List<ArmorSO>();
    public List<ArmorSetEffect> activeEffects = new List<ArmorSetEffect>();

    public int equippedCount => equippedPieces.Count;
    public bool isSetComplete => equippedCount >= GetRequiredPiecesForFullSet();

    private int GetRequiredPiecesForFullSet()
    {
        // Fallback to checking the highest pieces required in effects
        int maxRequired = 0;
        if (armorSet != null && armorSet.SetEffects != null)
        {
            foreach (var effect in armorSet.SetEffects)
            {
                if (effect.piecesRequired > maxRequired)
                    maxRequired = effect.piecesRequired;
            }
        }
        return maxRequired > 0 ? maxRequired : 3; // Default to 3 if no effects defined
    }

    public void AddPiece(ArmorSO piece)
    {
        if (!equippedPieces.Contains(piece))
            equippedPieces.Add(piece);
    }

    public void RemovePiece(ArmorSO piece)
    {
        equippedPieces.Remove(piece);
    }

    public void UpdateActiveEffects()
    {
        activeEffects.Clear();

        foreach (var effect in armorSet.SetEffects)
        {
            if (effect.ShouldBeActive(equippedCount))
            {
                activeEffects.Add(effect);
            }
        }
    }
}