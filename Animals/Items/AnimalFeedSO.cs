using System.Collections.Generic;
using UnityEngine;

/// <summary>An item of animal husbandry: its id in the animal rules ("animal:feed/hay", "animal:livestock/cow/jersey").</summary>
public interface IAnimalItem
{
    string AnimalKey { get; }
}

/// <summary>
/// Feed made for animals - hay, grass, grain, feeds, treats - one copy per item, each with its own quality (a fresh
/// bale feeds better than a mouldy one). What it does to an animal is its food's data (<see cref="AnimalFoodData"/>) and
/// that animal's diet; players do not eat it. An asset bound in the Animal Database replaces the item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Animal Feed", menuName = "SimpleMovements/Animals/Animal Feed (Item)", order = 10)]
public class AnimalFeedSO : ItemSO, IAnimalItem, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue
{
    [Header("Animals")]
    [Tooltip("The food it is (an id of the Animal Database's foods).")]
    [SerializeField] private string foodId = "";

    [System.NonSerialized] private string runtimeId;

    public string FoodId => foodId;
    public string AnimalKey => AnimalIds.Feed(foodId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public AnimalFeedSO()
    {
        itemType = ItemType.Material;
        stackMax = 20;
        weight = 1f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(AnimalFoodData f, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (f == null) return;
        foodId = f.id;
        name = f.DisplayName;
        description = string.IsNullOrWhiteSpace(f.description) ? "Food for animals." : f.description;
        itemType = ItemType.Material;
        price = Mathf.Max(1f, Mathf.Round(f.portionsPerItem * (0.4f + f.satiety * 0.05f)));
        weight = Mathf.Clamp(f.portionsPerItem * 0.25f, 0.2f, 6f);
        stackMax = f.portionsPerItem >= 6 ? 10 : 20;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) =>
        AnimalItemCode.ValueFactor(AnimalToolkit.QualityOfCopy(stack, fromTop));

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        if (item == null || lines == null) return;
        float q = AnimalToolkit.QualityOfCopy(item, 0);
        lines.Add($"Quality: {AgriItemCode.Grade(q)} ({Mathf.RoundToInt(q * 100f)}%)");
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Animal feed");
        AnimalFoodData f = AnimalDatabase.Instance.Food(foodId);
        if (f != null)
        {
            lines.Add($"{f.portionsPerItem} portion{(f.portionsPerItem == 1 ? "" : "s")}");
            if (f.freshHours > 0f) lines.Add($"Keeps fresh about {Mathf.Max(1, Mathf.RoundToInt(f.freshHours / 24f))} day(s) in a feeder");
        }
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(foodId)) errors.Add("No food id.");
        else if (AnimalDatabase.Instance.Food(foodId) == null) warnings.Add($"No food '{foodId}' in the Animal Database.");
    }
}
