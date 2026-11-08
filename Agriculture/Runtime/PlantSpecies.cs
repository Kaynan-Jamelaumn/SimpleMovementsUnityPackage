using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A plant species as an asset: everything about how it lives, what it needs at each stage and what it gives (see
/// <see cref="PlantSpeciesData"/>). List it in the Agriculture Database; one with the id of a built-in species replaces
/// it. Tools ▸ SimpleMovements ▸ Agriculture ▸ Create Species Assets makes editable copies of the built-in ones.
/// </summary>
[CreateAssetMenu(fileName = "Plant Species", menuName = "SimpleMovements/Agriculture/Plant Species", order = 1)]
public class PlantSpecies : ScriptableObject
{
    public PlantSpeciesData data = new PlantSpeciesData();

    public string Id => data != null ? data.id : "";

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (data == null)
        {
            errors.Add("No species data.");
            return;
        }
        data.Validate(errors, warnings);
    }

    private void OnValidate()
    {
        if (AgricultureDatabase.HasInstance) AgricultureDatabase.Instance.MarkChanged();
    }
}
