using System.Collections.Generic;
using UnityEngine;

/// <summary>What a crafting material is made of. Serialized as numbers in assets: only add new values at the END.</summary>
public enum MaterialKind
{
    Other,
    Ore,
    Ingot,
    Wood,
    Stone,
    Herb,
    Cloth,
    Leather,
    Gem,
    MonsterPart,
    Essence,
}

/// <summary>
/// A crafting resource (ore, ingot, wood, herb, cloth, leather, gem...): a plain stackable item with no use of its own.
/// Merchants list it under Materials (by its <see cref="MaterialKind"/>); recipes and quests count it with
/// <see cref="InventoryManager.GetItemCount"/> and take it with <see cref="InventoryManager.RemoveItems"/>.
/// </summary>
[CreateAssetMenu(fileName = "Material", menuName = "SimpleMovements/Items/Material", order = 6)]
public class MaterialSO : ItemSO
{
    [Header("Material")]
    [Tooltip("What the material is (Ore, Wood, Herb...). Shops use it for the Materials subcategories.")]
    [SerializeField] private MaterialKind materialKind = MaterialKind.Other;
    [Tooltip("Quality tier (1 = common). Shown in the tooltip; recipes and your scripts can read it.")]
    [SerializeField, Min(1)] private int tier = 1;

    public MaterialKind Kind => materialKind;
    public int Tier => tier;

    public MaterialSO()
    {
        itemType = ItemType.Material;
        stackMax = 64;
    }

    public override void AppendTooltip(List<string> lines)
    {
        string kind = materialKind == MaterialKind.MonsterPart ? "Monster part" : materialKind.ToString();
        lines.Add(materialKind == MaterialKind.Other ? "Crafting material" : $"Crafting material · {kind}");
        if (tier > 1)
            lines.Add($"Tier {tier}");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (stackMax <= 1)
            warnings.Add("Materials usually stack (Stack Max 1 means one per slot).");
    }

    protected override void OnValidate()
    {
        if (itemType != ItemType.Material)
            itemType = ItemType.Material;
        base.OnValidate();
    }
}
