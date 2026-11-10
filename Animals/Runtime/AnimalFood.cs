using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A food for animals as an asset (see <see cref="AnimalFoodData"/>): what a portion fills, waters and feeds, what else it
/// does, and the item ids that are it. List it in the Animal Database; one with the id of a built-in food replaces it.
/// What the food is to each species is that species' diet.
/// </summary>
[CreateAssetMenu(fileName = "Animal Food", menuName = "SimpleMovements/Animals/Animal Food", order = 2)]
public class AnimalFood : ScriptableObject
{
    public AnimalFoodData data = new AnimalFoodData();

    public string Id => data != null ? data.id : "";

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (data == null)
        {
            errors.Add("No food data.");
            return;
        }
        data.Validate(errors, warnings);
    }

    private void OnValidate()
    {
        if (AnimalDatabase.HasInstance) AnimalDatabase.Instance.MarkChanged();
    }
}
