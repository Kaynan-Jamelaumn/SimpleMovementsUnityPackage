using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A product of a living animal that is a material: wool (a sheep's fleece). Every copy keeps its own quality and origin
/// (merchants pay by quality). Named "Wool", the tailor's bench of Crafting takes it as raw wool fibre - a fine fleece as a
/// cleaner, sounder batch (Phase 5: <see cref="ICraftFibreQuality"/>). An asset bound in
/// the Animal Database replaces the item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Animal Product (Material)", menuName = "SimpleMovements/Animals/Animal Product (Material Item)", order = 13)]
public class AnimalProductMaterialSO : MaterialSO, IAnimalItem, IAnimalProductItem, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue, ICraftFibreQuality
{
    [Header("Animals")]
    [Tooltip("The product it is (an id of a species' products in the Animal Database).")]
    [SerializeField] private string productId = "";

    [System.NonSerialized] private string runtimeId;

    public string ProductId => productId;
    public string AnimalKey => AnimalIds.Product(productId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public AnimalProductMaterialSO()
    {
        itemType = ItemType.Material;
        stackMax = 20;
        weight = 1f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(AnimalProductSpec p, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (p == null) return;
        productId = p.id;
        name = p.DisplayName;
        description = string.IsNullOrWhiteSpace(p.description) ? "From an animal of the farm." : p.description;
        itemType = ItemType.Material;
        price = Mathf.Max(1, p.value);
        weight = 1f;
        stackMax = 20;
        SetMaterial(p.tags != null && (p.tags.Contains("fiber") || p.tags.Contains("wool")) ? MaterialKind.Cloth : MaterialKind.Other);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => AnimalProducts.ValueFactor(stack, fromTop);
    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) => AnimalProducts.Tooltip(item, lines);

    /// <summary>
    /// Crafting (Phase 5): a copy's quality, from its code, shapes the batch of raw fibre it makes at a tailor's bench - a
    /// fine fleece comes cleaner and with less short fibre (see <see cref="CraftFibre.Grade"/>); a copy without a code (bought
    /// in) is the supply's typical batch.
    /// </summary>
    public float FibreQuality(int code) => AnimalItemCode.TryProduct(code, out float q, out _) ? q : -1f;

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        AnimalProducts.ItemTooltip(AnimalProducts.Spec(productId), lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(productId)) errors.Add("No product id.");
        else if (AnimalProducts.Spec(productId) == null) warnings.Add($"No species of the Animal Database gives '{productId}'.");
    }
}
