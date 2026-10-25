using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A weapon or tool made at a crafting station: a copy of its design's template weapon (attacks, animations, hit
/// volumes, sounds) whose numbers - damage, criticals, knockback, speed, tool power, element, durability, weight,
/// price - come from the evaluation of what was actually made. Made at runtime (see <see cref="CraftItems"/>).
/// </summary>
public class ForgedWeaponSO : WeaponSO, ICraftedItem, IRuntimeItem, IItemInstanceTooltip
{
    [SerializeField] private CraftedItemRecord record = new CraftedItemRecord();

    public CraftedItemRecord Record => record;
    public string RuntimeId => CraftedTooltip.IdPrefix + record.id;

    /// <summary>The fields only a subclass can set (name, look, weight, durability, tool type).</summary>
    public void SetIdentity(CraftedItemRecord r, string displayName, string itemDescription, float kilograms, float value, int maxUses, Sprite itemIcon,
        ToolType tool, Vector2Int grid)
    {
        record = r ?? new CraftedItemRecord();
        ((UnityEngine.Object)this).name = displayName;
        name = displayName;
        description = itemDescription;
        weight = Mathf.Max(0.05f, kilograms);
        price = Mathf.Max(0f, value);
        maxDurability = Mathf.Max(1, maxUses);
        durability = maxDurability;
        durabilityReductionPerUse = 1;
        shouldBeDestroyedOn0UsesLeft = false;
        stackMax = 1;
        toolType = tool;
        if (itemIcon != null) icon = itemIcon;
        SetGridSize(grid);
    }

    /// <summary>The record changed where the item was made (it was tried): the numbers and lines it shows follow.</summary>
    public void ReplaceRecord(CraftedItemRecord r)
    {
        if (r != null && r.id == record.id)
            record = r;
    }

    /// <summary>A model built from the parts, used when the design's template has none (on the ground, in the hand).</summary>
    public void SetModelIfNone(GameObject model)
    {
        if (prefab == null && model != null) prefab = model;
    }

    public override void AppendTooltip(List<string> lines)
    {
        CraftedTooltip.Append(record, lines);
        if (record.inspected)
            base.AppendTooltip(lines);
    }

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) { }
}
