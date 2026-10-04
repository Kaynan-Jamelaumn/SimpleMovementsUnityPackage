using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item with no use of its own that is not a material: keys, junk and valuables to sell, trophies, quest items.
/// Merchants list it under Miscellaneous. Quest items are never sold (<see cref="ItemSO.CanBeSold"/> is turned off).
/// </summary>
[CreateAssetMenu(fileName = "Misc Item", menuName = "SimpleMovements/Items/Miscellaneous", order = 7)]
public class MiscItemSO : ItemSO
{
    [Header("Miscellaneous")]
    [Tooltip("A quest item: it cannot be sold (Can Be Sold is turned off) and the tooltip says so.")]
    [SerializeField] private bool questItem;
    [Tooltip("Optional line shown in the tooltip (\"Opens the crypt door\", \"Worth a lot to a collector\").")]
    [SerializeField] private string tooltipNote;

    public bool IsQuestItem => questItem;
    public string TooltipNote => tooltipNote;

    public MiscItemSO()
    {
        itemType = ItemType.Miscellaneous;
    }

    public override void AppendTooltip(List<string> lines)
    {
        if (questItem)
            lines.Add("Quest item");
        if (!string.IsNullOrWhiteSpace(tooltipNote))
            lines.Add(tooltipNote);
        base.AppendTooltip(lines);
    }

    protected override void OnValidate()
    {
        if (questItem)
            canBeSold = false;
        base.OnValidate();
    }
}
