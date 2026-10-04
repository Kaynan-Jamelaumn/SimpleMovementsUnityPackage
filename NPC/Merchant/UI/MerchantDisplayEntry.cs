using System.Collections.Generic;
using UnityEngine;

/// <summary>One item as the shop window shows it: a line of the merchant's goods (buying) or a stack of the player's (selling).</summary>
public sealed class MerchantDisplayEntry
{
    public MerchantTransactionType Mode;
    public ItemSO Item;
    /// <summary>Buying: the shop line.</summary>
    public MerchantStockSlot Slot;
    /// <summary>Selling: the player's stack.</summary>
    public InventoryItem Stack;
    /// <summary>Stock left (buying) or units in the stack (selling).</summary>
    public int Quantity;
    public bool Unlimited;
    public int UnitPrice;
    /// <summary>Buying: the player can pay for one.</summary>
    public bool Affordable;
    /// <summary>Can be bought / sold now (in stock, the merchant buys it).</summary>
    public bool Available;
    /// <summary>Why it cannot be bought / sold ("Sold out", "Not wanted").</summary>
    public string Reason = "";
    public ItemCategoryMatch Category;
    /// <summary>Kept on screen but greyed (filtered out of a mirrored inventory).</summary>
    public bool Dimmed;
    /// <summary>Selling from the hotbar.</summary>
    public bool FromHotbar;
    /// <summary>A fixed place in a grid (a mirror of the player's grid inventory).</summary>
    public bool HasPlacement;
    public GridRect Placement;
    public bool Rotated;
    /// <summary>An empty slot of a mirrored inventory (no item).</summary>
    public bool IsEmptySlot;

    /// <summary>What identifies the selection across refreshes.</summary>
    public object Key => Slot != null ? (object)Slot : Stack;

    public void Clear()
    {
        Item = null;
        Slot = null;
        Stack = null;
        Quantity = 0;
        Unlimited = false;
        UnitPrice = 0;
        Affordable = Available = false;
        Reason = "";
        Category = default;
        Dimmed = FromHotbar = HasPlacement = Rotated = IsEmptySlot = false;
        Placement = default;
    }
}

/// <summary>
/// Places items in a grid the way the grid inventory would: in order, each at the first place it fits row by row,
/// turned sideways when it is allowed and helps. Rows grow as needed. Uses <see cref="GridInventoryModel{TItem}"/>, so
/// the shop follows exactly the grid inventory's rules.
/// </summary>
public static class MerchantGridPacker
{
    /// <summary>
    /// Fills <paramref name="rects"/> and <paramref name="rotated"/> (one per size, in order) and returns the rows used.
    /// Items wider than the grid that cannot turn are clamped to its width.
    /// </summary>
    public static int Pack(IReadOnlyList<Vector2Int> sizes, IReadOnlyList<bool> canRotate, int columns, bool allowRotate,
        List<GridRect> rects, List<bool> rotated)
    {
        rects.Clear();
        rotated.Clear();
        columns = Mathf.Max(1, columns);
        int bound = 1;
        for (int i = 0; i < sizes.Count; i++)
            bound += Mathf.Max(sizes[i].x, sizes[i].y);
        var model = new GridInventoryModel<object>(columns, Mathf.Max(1, bound), ReferenceComparer<object>.Instance);
        int rows = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            Vector2Int s = new Vector2Int(Mathf.Max(1, sizes[i].x), Mathf.Max(1, sizes[i].y));
            bool turn = canRotate != null && i < canRotate.Count && canRotate[i];
            bool forcedTurn = false;
            if (s.x > columns && turn && s.y <= columns)
            {
                s = new Vector2Int(s.y, s.x); // only fits sideways
                forcedTurn = true;
            }
            if (s.x > columns)
                s.x = columns;
            if (!model.FindSpace(s.x, s.y, allowRotate && turn && !forcedTurn && s.y <= columns, out GridRect r, out bool rot))
            {
                r = new GridRect(0, rows, s.x, s.y); // cannot happen with the row bound; stay safe
                rot = false;
            }
            model.Place(new object(), r);
            rects.Add(r);
            rotated.Add(forcedTurn ? !rot : rot);
            rows = Mathf.Max(rows, r.Bottom);
        }
        return Mathf.Max(1, rows);
    }
}
