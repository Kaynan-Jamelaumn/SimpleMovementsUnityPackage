using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produce that is not eaten: herbs, flowers, roots and berries for medicine, alchemy and magic. Every copy keeps its own
/// quality and potency (how it was grown and when it was picked); merchants pay by them; with an alchemy ingredient
/// named in its species' produce, it goes into vessels.
/// </summary>
[CreateAssetMenu(fileName = "Produce", menuName = "SimpleMovements/Agriculture/Produce (Material)", order = 11)]
public class ProduceMaterialSO : MaterialSO, IAgriProduce, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue, IAlchemyIngredient
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

    public ProduceMaterialSO()
    {
        stackMax = 30;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(PlantSpeciesData d, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (d == null) return;
        speciesId = d.id;
        ProduceSpec p = d.Produce;
        name = p.name;
        description = string.IsNullOrWhiteSpace(p.description) ? $"From {d.name.ToLowerInvariant()}." : p.description;
        SetMaterial(MaterialKind.Herb, d.category == PlantCategory.Magical || d.category == PlantCategory.Rare ? 3 : d.category == PlantCategory.Alchemical ? 2 : 1);
        price = p.value;
        weight = p.weight;
        stackMax = 30;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => AgriItemInfo.ValueFactor(stack, fromTop);
    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) => AgriItemInfo.ProduceTooltip(item, AgriItemInfo.Species(speciesId), lines);

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        PlantSpeciesData d = AgriItemInfo.Species(speciesId);
        if (d != null) lines.Add($"{AgriText.Category(d.category)} · from {d.name.ToLowerInvariant()}");
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (AgriItemInfo.Species(speciesId) == null) warnings.Add($"The Agriculture Database has no species '{speciesId}'.");
    }
}
