using System.Collections.Generic;
using UnityEngine;

/// <summary>A product of a living animal ("animal:product/egg"): its product's id.</summary>
public interface IAnimalProductItem
{
    string ProductId { get; }
}

/// <summary>
/// A product of a living animal that is eaten or drunk: eggs, milk. Every copy keeps its own quality and origin (one copy
/// per bite: a fine one sates more, merchants pay by quality), and it brews in Alchemy as its spec's ingredient preset
/// ("Egg Yolk", "Goat Milk"). An asset bound in the Animal Database replaces the item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Animal Product (Food)", menuName = "SimpleMovements/Animals/Animal Product (Food Item)", order = 12)]
public class AnimalProductFoodSO : ConsumableSO, IAnimalItem, IAnimalProductItem, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue, IAlchemyIngredient
{
    [Header("Animals")]
    [Tooltip("The product it is (an id of a species' products in the Animal Database).")]
    [SerializeField] private string productId = "";

    [System.NonSerialized] private string runtimeId;

    public string ProductId => productId;
    public string AnimalKey => AnimalIds.Product(productId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => Name;
    public AlchemyIngredientProfile AlchemyProfile => AnimalProducts.AlchemyProfileOf(AnimalProducts.Spec(productId));

    public AnimalProductFoodSO()
    {
        itemType = ItemType.Food;
        stackMax = 20;
        weight = 0.1f;
        OneCopyPerBite();
    }

    private void OnEnable() => OneCopyPerBite();

    private void OneCopyPerBite()
    {
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = int.MaxValue;
        shouldBeDestroyedOn0UsesLeft = true;
    }

    public void Setup(AnimalProductSpec p, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (p == null) return;
        productId = p.id;
        name = p.DisplayName;
        description = string.IsNullOrWhiteSpace(p.description) ? "From an animal of the farm." : p.description;
        itemType = ItemType.Food;
        price = Mathf.Max(1, p.value);
        weight = p.drink ? 1f : 0.1f;
        stackMax = p.drink ? 10 : 20;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
        var effects = new List<ConsumableEffect>();
        if (p.drink)
        {
            effects.Add(new ConsumableEffect { effectType = ConsumableEffectType.Drink, itemType = ConsumableType.Food, effectName = p.DisplayName, amount = p.nutrition * 1.5f, applicationChance = 1f, minimumLevel = 1 });
            effects.Add(new ConsumableEffect { effectType = ConsumableEffectType.Food, itemType = ConsumableType.Food, effectName = p.DisplayName, amount = p.nutrition * 0.5f, applicationChance = 1f, minimumLevel = 1 });
        }
        else effects.Add(new ConsumableEffect { effectType = ConsumableEffectType.Food, itemType = ConsumableType.Food, effectName = p.DisplayName, amount = p.nutrition, applicationChance = 1f, minimumLevel = 1 });
        SetEffects(effects);
        OneCopyPerBite();
    }

    /// <summary>A fine copy sates fully, a poor one half.</summary>
    protected override float EffectScale(InventoryItem instance) =>
        AnimalItemCode.TryProduct(AnimalToolkit.CodeOf(instance, 0), out float q, out _) ? 0.5f + 0.5f * q : 1f;

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => AnimalProducts.ValueFactor(stack, fromTop);
    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) => AnimalProducts.Tooltip(item, lines);

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Food");
        if (Effects != null)
            foreach (ConsumableEffect e in Effects)
                if (e != null) lines.Add(BrewedPotionSO.DescribeEffect(e));
        AnimalProducts.ItemTooltip(AnimalProducts.Spec(productId), lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(productId)) errors.Add("No product id.");
        else if (AnimalProducts.Spec(productId) == null) warnings.Add($"No species of the Animal Database gives '{productId}'.");
    }
}
