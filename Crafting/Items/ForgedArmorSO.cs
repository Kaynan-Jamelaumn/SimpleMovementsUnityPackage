using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Armour or a shield made at a crafting station: a copy of its design's template (model, slot visuals, sounds) whose
/// defence, magic defence, resistances, durability, weight and price come from what was actually made.
/// </summary>
public class ForgedArmorSO : ArmorSO, ICraftedItem, IRuntimeItem, IItemInstanceTooltip
{
    [SerializeField] private CraftedItemRecord record = new CraftedItemRecord();

    public CraftedItemRecord Record => record;
    public string RuntimeId => CraftedTooltip.IdPrefix + record.id;

    public void SetIdentity(CraftedItemRecord r, string displayName, string itemDescription, float kilograms, float value, int maxUses, Sprite itemIcon, Vector2Int grid)
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
