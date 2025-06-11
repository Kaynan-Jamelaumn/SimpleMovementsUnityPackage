using System.Collections.Generic;
using System.Linq;



[System.Serializable]
public class ArmorSetTracker
{
    public ArmorSet armorSet;
    public List<ArmorSO> equippedPieces = new List<ArmorSO>();
    public List<ArmorSetEffect> activeEffects = new List<ArmorSetEffect>();
    public Dictionary<string, object> activeMechanics = new Dictionary<string, object>();
    public int equippedCount => equippedPieces.Count;
    public bool isSetComplete => armorSet != null && armorSet.IsSetComplete(equippedCount);

    public ArmorSetTracker(ArmorSet set)
    {
        armorSet = set;
        equippedPieces = new List<ArmorSO>();
        activeEffects = new List<ArmorSetEffect>();
        activeMechanics = new Dictionary<string, object>();
    }

    public void AddPiece(ArmorSO piece)
    {
        if (piece != null && !equippedPieces.Contains(piece))
        {
            equippedPieces.Add(piece);
        }
    }

    public void RemovePiece(ArmorSO piece)
    {
        equippedPieces.Remove(piece);
    }

    public void UpdateActiveEffects()
    {
        if (armorSet == null) return;

        var previousEffects = new List<ArmorSetEffect>(activeEffects);
        activeEffects = armorSet.GetActiveEffects(equippedCount);

        // Track which effects were added or removed
        var addedEffects = activeEffects.Except(previousEffects).ToList();
        var removedEffects = previousEffects.Except(activeEffects).ToList();
    }

    public bool HasPiece(ArmorSO piece)
    {
        return equippedPieces.Contains(piece);
    }

    public bool HasMechanic(string mechanicId)
    {
        return activeMechanics.ContainsKey(mechanicId);
    }

    public void AddMechanic(string mechanicId, object mechanicData)
    {
        activeMechanics[mechanicId] = mechanicData;
    }

    public void RemoveMechanic(string mechanicId)
    {
        activeMechanics.Remove(mechanicId);
    }
}