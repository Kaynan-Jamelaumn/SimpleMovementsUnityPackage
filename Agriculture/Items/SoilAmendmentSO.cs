using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A material for the soil or the plants, one portion per copy: sand, clay, gravel, peat, compost, ash, limestone,
/// sulfur, manure, bone meal, fertilizers (mineral, alchemical, liquid, slow-release, fish, herbal), magical compost,
/// mulch, pesticides, fungicide, soil cleanser, ladybirds, slug bait... Its numbers (<see cref="data"/>) say what one
/// portion does to one square metre. An asset listed in the Agriculture Database replaces the built-in material of its
/// id (or adds a new one).
/// </summary>
[CreateAssetMenu(fileName = "Soil Material", menuName = "SimpleMovements/Agriculture/Soil Material", order = 14)]
public class SoilAmendmentSO : ItemSO, IAgriItem, IRuntimeItem
{
    [Header("Agriculture")]
    public SoilAmendmentData data = new SoilAmendmentData();

    [System.NonSerialized] private string runtimeId;

    public string AgriKey => AgriIds.Amendment(data != null ? data.id : "");
    public string RuntimeId => runtimeId;

    public SoilAmendmentSO()
    {
        itemType = ItemType.Material;
        stackMax = 20;
        weight = 0.5f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public override bool ShowsDurability => false;

    public void Setup(SoilAmendmentData d, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (d == null) return;
        data = d.Clone();
        Refresh();
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    /// <summary>Takes its name, description, price and weight from its data.</summary>
    public void Refresh()
    {
        if (data == null) return;
        name = data.name;
        description = data.description;
        price = data.value;
        weight = data.portionKg;
        stackMax = data.kind == AmendmentKind.Beneficial ? 5 : 20;
    }

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        if (data == null) return;
        lines.Add(data.kind == AmendmentKind.Treatment ? "For plants: use it with Treat" : data.kind == AmendmentKind.Beneficial ? "Living helpers" : $"For the soil · one portion of {data.portionKg:0.##} kg");
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (data == null) { errors.Add("No material data."); return; }
        var e = new List<string>();
        var w = new List<string>();
        data.Validate(e, w);
        errors.AddRange(e);
        warnings.AddRange(w);
    }
}
