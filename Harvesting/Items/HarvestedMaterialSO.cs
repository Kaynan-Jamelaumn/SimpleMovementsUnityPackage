using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A piece harvested from a creature: a claw, a fang, a horn, a bone, a tendon, scales, feathers, a gland, a plain hide...
/// Every copy keeps its own quality and marks (in its copy's durability list, like a potion's strength): a stack of
/// claws remembers which are pristine and which cracked - in the fight or while being worked loose - and merchants pay
/// for each by its quality. Pieces with alchemy substances go into alchemy vessels.
/// <para>Made at runtime for every material of every creature (<see cref="HarvestItems"/>), or as an asset (Tools ▸
/// SimpleMovements ▸ Harvesting ▸ Create Items) to give it its own icon, model and price.</para>
/// </summary>
[CreateAssetMenu(fileName = "Harvested Material", menuName = "SimpleMovements/Harvesting/Harvested Material", order = 2)]
public class HarvestedMaterialSO : MaterialSO, IHarvestedItem, IItemInstanceTooltip, IItemInstanceValue, IRuntimeItem, IAlchemyIngredient
{
    [Header("Harvesting")]
    [Tooltip("The creature it comes from (an anatomy id of the Harvest Database: bear, wolf...).")]
    [SerializeField] private string anatomyId = "";
    [Tooltip("The material of that creature it is (claws, fangs, femur...).")]
    [SerializeField] private string materialId = "";

    [System.NonSerialized] private string runtimeId;
    [System.NonSerialized] private AlchemyIngredientProfile alchemy;

    public string AnatomyId => anatomyId;
    public string MaterialId => materialId;

    /// <summary>The id of an item made at runtime ("harvest:bear/claws"); null for an asset (the Item Database names it).</summary>
    public string RuntimeId => runtimeId;

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => Name;
    public AlchemyIngredientProfile AlchemyProfile =>
        alchemy ??= HarvestedItemInfo.AlchemyProfile(HarvestedItemInfo.Material(this, out _)) ?? new AlchemyIngredientProfile();

    public override bool ShowsDurability => false;

    public HarvestedMaterialSO()
    {
        stackMax = 20;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    /// <summary>Fills the item from a creature's material (name, description, kind, price, weight, icon, model).</summary>
    public void Setup(HarvestAnatomyData a, HarvestMaterial m, Sprite itemIcon, GameObject model, string id = null)
    {
        anatomyId = a != null ? a.id : "";
        materialId = m != null ? m.id : "";
        runtimeId = id;
        alchemy = null;
        if (m == null)
            return;
        name = m.Name;
        description = string.IsNullOrWhiteSpace(m.description) ? $"From {(a != null ? HarvestText.Capital(a.Name.ToLowerInvariant()) : "a creature")}." : m.description;
        SetMaterial(HarvestedItemInfo.MaterialKindOf(m.kind), m.tier);
        price = m.value;
        weight = m.weight;
        stackMax = m.kind == HarvestMaterialKind.Hide && !m.countable ? 1 : m.countable ? 20 : 30;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
        SetGridSize(m.kind == HarvestMaterialKind.Hide && !m.countable ? new Vector2Int(2, 2) : m.Shape == "longbone" || m.Shape == "antler" || m.Shape == "horn" ? new Vector2Int(1, 2) : Vector2Int.one);
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => HarvestedItemInfo.ValueFactor(stack, fromTop);

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) => HarvestedItemInfo.InstanceTooltip(item, lines);

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        HarvestedItemInfo.ItemTooltip(this, lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(anatomyId) || string.IsNullOrWhiteSpace(materialId))
            warnings.Add("It names no creature material (Anatomy Id, Material Id): its pieces show no origin.");
        else if (HarvestedItemInfo.Material(this, out _) == null)
            warnings.Add($"The Harvest Database has no material '{materialId}' on a creature '{anatomyId}'.");
    }
}
