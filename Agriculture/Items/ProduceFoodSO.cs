using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produce that is eaten: fruit, vegetables, grain. Every copy keeps its own quality (one copy per bite); a fine one
/// sates fully, a poor one less, merchants pay by quality.
/// </summary>
[CreateAssetMenu(fileName = "Produce Food", menuName = "SimpleMovements/Agriculture/Produce (Food)", order = 12)]
public class ProduceFoodSO : ConsumableSO, IAgriProduce, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue, IAlchemyIngredient
{
    [Header("Agriculture")]
    [SerializeField] private string speciesId = "";

    [System.NonSerialized] private string runtimeId;

    public string SpeciesId => speciesId;
    public string AgriKey => AgriIds.Produce(speciesId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => Name;
    public AlchemyIngredientProfile AlchemyProfile => AgriItems.AlchemyProfileOf(AgriItemInfo.Species(speciesId));

    public ProduceFoodSO()
    {
        itemType = ItemType.Food;
        stackMax = 20;
        weight = 0.2f;
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

    public void Setup(PlantSpeciesData d, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (d == null) return;
        speciesId = d.id;
        ProduceSpec p = d.Produce;
        name = p.name;
        description = string.IsNullOrWhiteSpace(p.description) ? $"From {d.name.ToLowerInvariant()}." : p.description;
        itemType = ItemType.Food;
        price = p.value;
        weight = p.weight;
        stackMax = 20;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
        SetEffects(new List<ConsumableEffect>
        {
            new ConsumableEffect
            {
                effectType = ConsumableEffectType.Food, itemType = ConsumableType.Food, effectName = p.name, amount = Mathf.Max(0f, p.nutrition),
                applicationChance = 1f, minimumLevel = 1,
            },
        });
        OneCopyPerBite();
    }

    /// <summary>A fine piece sates fully, a poor one less.</summary>
    protected override float EffectScale(InventoryItem instance)
    {
        int code = AgriItemInfo.CodeOf(instance);
        return AgriItemCode.TryProduce(code, out float q, out _, out _, out _) ? 0.5f + 0.5f * q : 1f;
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => AgriItemInfo.ValueFactor(stack, fromTop);
    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) => AgriItemInfo.ProduceTooltip(item, AgriItemInfo.Species(speciesId), lines);

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Food");
        if (Effects != null)
            foreach (ConsumableEffect e in Effects)
                if (e != null) lines.Add(BrewedPotionSO.DescribeEffect(e));
        PlantSpeciesData d = AgriItemInfo.Species(speciesId);
        if (d != null) lines.Add($"From {d.name.ToLowerInvariant()}");
    }
}
