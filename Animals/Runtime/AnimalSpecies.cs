using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An animal species as an asset: its stages, needs, diet, habitat, looks and breeds (see <see cref="AnimalSpeciesData"/>),
/// and optionally the model its animals are shown with. List it in the Animal Database; one with the id of a built-in
/// species replaces it. Tools ▸ SimpleMovements ▸ Animals ▸ Create Species Assets makes editable copies of the built-in ones.
/// </summary>
[CreateAssetMenu(fileName = "Animal Species", menuName = "SimpleMovements/Animals/Animal Species", order = 1)]
public class AnimalSpecies : ScriptableObject
{
    public AnimalSpeciesData data = new AnimalSpeciesData();

    [Tooltip("The model its animals are shown with (an adult; it is scaled to each animal's size). Empty = a simple body made from its look. " +
             "Use a model, not a Mob prefab: the animal's life is the Animal Director's, the model only shows it.")]
    public GameObject bodyPrefab;

    public string Id => data != null ? data.id : "";

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (data == null)
        {
            errors.Add("No species data.");
            return;
        }
        data.Validate(errors, warnings);
        if (bodyPrefab != null && bodyPrefab.GetComponentInChildren<Mob>(true) != null)
            warnings.Add("Its Body Prefab is a Mob: its AI would fight the Animal Director for the body. Use the model only.");
    }

    /// <summary>
    /// A species made now (Assets ▸ Create, or the inspector's Reset) is of this version: its care is its own, never the
    /// built-in species'. Data Version itself has no such default, so an asset saved before Phase 2 still reads 0.
    /// </summary>
    private void Reset()
    {
        data ??= new AnimalSpeciesData();
        data.dataVersion = AnimalDatabase.SpeciesDataVersion;
    }

    private void OnValidate()
    {
        if (AnimalDatabase.HasInstance) AnimalDatabase.Instance.MarkChanged();
    }
}
