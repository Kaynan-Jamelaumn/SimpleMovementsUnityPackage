using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seeds of a plant species. Every copy carries its own genes in its per-copy code (growth, hardiness, yield, quality,
/// potency... and its vigour): seeds saved from a fine plant can make finer plants, and crosses make new varieties. The
/// genes are never shown - only what grows from them. Bought or found seeds (no code) are the species as it is in the
/// wild. Made at runtime for every species (<see cref="AgriItems"/>), or as an asset to give it its own icon and price.
/// </summary>
[CreateAssetMenu(fileName = "Seeds", menuName = "SimpleMovements/Agriculture/Seeds", order = 10)]
public class SeedItemSO : ItemSO, IAgriItem, IRuntimeItem
{
    [Header("Agriculture")]
    [Tooltip("The species (an id of the Agriculture Database: tomato, wheat...).")]
    [SerializeField] private string speciesId = "";

    [System.NonSerialized] private string runtimeId;

    public string SpeciesId => speciesId;
    public string AgriKey => AgriIds.Seed(speciesId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public SeedItemSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 50;
        weight = 0.002f;
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
        name = $"{d.name} Seeds";
        description = d.lifeCycle == PlantLifeCycle.Perennial ? $"Seeds of {d.name.ToLowerInvariant()}, a perennial." : $"Seeds of {d.name.ToLowerInvariant()}.";
        price = d.seedValue;
        weight = 0.002f;
        stackMax = Mathf.Max(1, d.seedStack);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        PlantSpeciesData d = AgriItemInfo.Species(speciesId);
        if (d != null) lines.Add($"Seeds · {AgriText.Category(d.category)}");
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(speciesId)) errors.Add("It names no species (Species Id).");
        else if (AgriItemInfo.Species(speciesId) == null) warnings.Add($"The Agriculture Database has no species '{speciesId}'.");
    }
}
