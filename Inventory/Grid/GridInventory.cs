using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The grid inventory of an <see cref="InventoryManager"/> (Use Grid Inventory on). Its inventory slots are the CELLS,
/// row by row; an item is held by the cell of its top-left corner and covers W × H cells (its Grid Size, swapped when
/// rotated). The other cells it covers stay empty slots.
/// <para>
/// The slots remain the only truth: the occupancy (<see cref="GridInventoryModel{TItem}"/>) is rebuilt from them for
/// every decision, so nothing visual can create, lose or duplicate an item, and every placement - adding, dragging,
/// rotating, splitting, swapping - is checked against bounds and overlaps first.
/// </para>
/// </summary>
public sealed class GridInventory
{
    private readonly GridInventoryModel<InventoryItem> model;
    private readonly Dictionary<InventorySlot, int> indexOf = new Dictionary<InventorySlot, int>(ReferenceComparer<InventorySlot>.Instance);
    private readonly List<InventoryItem> conflicts = new List<InventoryItem>();
    private InventorySlot[] cells = new InventorySlot[0];

    public GridInventorySettings Settings { get; }
    public int Columns { get; }
    public int Rows { get; }
    /// <summary>Items found overlapping or outside the grid at the last rebuild (placed by older code); see <see cref="TakeConflicts"/>.</summary>
    public IReadOnlyList<InventoryItem> Conflicts => conflicts;

    public GridInventory(GridInventorySettings settings, GameObject[] cellObjects)
    {
        Settings = settings ?? new GridInventorySettings();
        Settings.Clamp();
        Columns = Settings.columns;
        Rows = Settings.rows;
        model = new GridInventoryModel<InventoryItem>(Columns, Rows, ReferenceComparer<InventoryItem>.Instance);
        SetCells(cellObjects);
    }

    /// <summary>Uses these slots as the cells (row by row). Extra slots are ignored; missing ones leave holes.</summary>
    public void SetCells(GameObject[] cellObjects)
    {
        int n = Columns * Rows;
        cells = new InventorySlot[n];
        indexOf.Clear();
        if (cellObjects == null)
            return;
        for (int i = 0; i < n && i < cellObjects.Length; i++)
        {
            InventorySlot s = cellObjects[i] != null ? cellObjects[i].GetComponent<InventorySlot>() : null;
            cells[i] = s;
            if (s != null)
                indexOf[s] = i;
        }
    }

    /// <summary>The occupancy, rebuilt from the slots now.</summary>
    public GridInventoryModel<InventoryItem> Model
    {
        get
        {
            Rebuild();
            return model;
        }
    }

    /// <summary>Rebuilds the occupancy from what the cells hold. Items that overlap or stick out are listed in <see cref="Conflicts"/>.</summary>
    public void Rebuild()
    {
        model.Clear();
        conflicts.Clear();
        for (int i = 0; i < cells.Length; i++)
        {
            InventorySlot s = cells[i];
            if (s == null || s.heldItem == null)
                continue;
            InventoryItem it = s.heldItem.GetComponent<InventoryItem>();
            if (it == null || it.itemScriptableObject == null)
                continue;
            GridRect r = RectAt(it, i % Columns, i / Columns, it.gridRotated);
            if (!model.Place(it, r))
                conflicts.Add(it);
        }
    }

    /// <summary>Removes and returns the conflicting items from their cells (the caller re-places them).</summary>
    public List<InventoryItem> TakeConflicts()
    {
        Rebuild();
        var list = new List<InventoryItem>(conflicts);
        foreach (InventoryItem it in list)
        {
            InventorySlot s = AnchorOf(it);
            if (s != null && s.heldItem == it.gameObject)
                s.heldItem = null;
        }
        conflicts.Clear();
        return list;
    }

    // ------------------------------------------------------------------ cells
    public bool IsCell(InventorySlot slot) => slot != null && indexOf.ContainsKey(slot);

    public bool TryGetCell(InventorySlot slot, out int x, out int y)
    {
        if (slot != null && indexOf.TryGetValue(slot, out int i))
        {
            x = i % Columns;
            y = i / Columns;
            return true;
        }
        x = y = -1;
        return false;
    }

    public InventorySlot CellAt(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows ? cells[y * Columns + x] : null;

    /// <summary>The cell holding <paramref name="item"/> (its top-left), or null.</summary>
    public InventorySlot AnchorOf(InventoryItem item)
    {
        if (item == null)
            return null;
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null && cells[i].heldItem == item.gameObject)
                return cells[i];
        return null;
    }

    /// <summary>The cell holding the item that covers <paramref name="cell"/> (itself when it holds one), or null.</summary>
    public InventorySlot OwnerCell(InventorySlot cell)
    {
        if (!TryGetCell(cell, out int x, out int y))
            return null;
        if (cell.heldItem != null)
            return cell;
        InventoryItem covering = Model.At(x, y);
        return covering != null ? AnchorOf(covering) : null;
    }

    // ------------------------------------------------------------------ sizes
    /// <summary>Cells an item takes (rotated = width and height swapped).</summary>
    public static Vector2Int SizeOf(ItemSO item, bool rotated)
    {
        Vector2Int s = item != null ? item.GridSize : Vector2Int.one;
        return rotated ? new Vector2Int(s.y, s.x) : s;
    }

    public static GridRect RectAt(InventoryItem item, int x, int y, bool rotated)
    {
        Vector2Int s = SizeOf(item != null ? item.itemScriptableObject : null, rotated);
        return new GridRect(x, y, s.x, s.y);
    }

    public static GridRect RectAt(ItemSO item, int x, int y, bool rotated)
    {
        Vector2Int s = SizeOf(item, rotated);
        return new GridRect(x, y, s.x, s.y);
    }

    /// <summary>Where a placed item is (false when it is not in the grid).</summary>
    public bool TryGetRect(InventoryItem item, out GridRect rect) => Model.TryGet(item, out rect);

    // ------------------------------------------------------------------ placement
    /// <summary>Is the rectangle inside the grid and free (<paramref name="ignore"/> does not count)?</summary>
    public bool CanPlace(GridRect r, InventoryItem ignore = null) => Model.CanPlace(r, ignore);

    /// <summary>A free place for a new stack of <paramref name="item"/> (rotation tried when allowed).</summary>
    public bool FindSpace(ItemSO item, out InventorySlot anchor, out bool rotated, InventoryItem ignore = null)
    {
        anchor = null;
        rotated = false;
        if (item == null)
            return false;
        Vector2Int s = item.GridSize;
        bool allowRotate = item.CanRotateInGrid && Settings.autoRotateToFit;
        if (!Model.FindSpace(s.x, s.y, allowRotate, out GridRect r, out rotated, ignore))
            return false;
        anchor = CellAt(r.X, r.Y);
        return anchor != null;
    }

    /// <summary>Puts an inventory item at (x, y): checks bounds and overlaps first. Returns false (nothing changes) if it does not fit.</summary>
    public bool PlaceAt(InventoryItem item, int x, int y, bool rotated)
    {
        if (item == null)
            return false;
        InventorySlot cell = CellAt(x, y);
        if (cell == null)
            return false;
        GridRect r = RectAt(item, x, y, rotated);
        if (!Model.CanPlace(r, item))
            return false;
        InventorySlot old = AnchorOf(item);
        if (old != null && old != cell)
            old.heldItem = null;
        item.gridRotated = rotated;
        cell.SetHeldItem(item.gameObject);
        return true;
    }

    /// <summary>Turns a placed item sideways where it is, if the turned shape fits. Returns false otherwise.</summary>
    public bool TryRotateInPlace(InventoryItem item, out GridRect wanted)
    {
        wanted = default;
        if (item == null || item.itemScriptableObject == null || !item.itemScriptableObject.CanRotateInGrid)
            return false;
        if (!Model.TryGet(item, out GridRect r))
            return false;
        wanted = r.Rotated;
        if (r.W == r.H || !model.CanPlace(wanted, item))
            return false;
        item.gridRotated = !item.gridRotated;
        return true;
    }

    /// <summary>
    /// How many of <paramref name="item"/> (up to <paramref name="quantity"/>) fit: room in existing stacks of the given
    /// slots, then new stacks placed in a copy of the grid, then <paramref name="freeExtraSlots"/> single slots (the hotbar).
    /// </summary>
    public int CountFit(ItemSO item, int quantity, IEnumerable<GameObject> stackSlots, int freeExtraSlots)
    {
        if (item == null || quantity <= 0)
            return 0;
        int fit = 0;
        int stackMax = Mathf.Max(1, item.StackMax);
        if (stackMax > 1 && stackSlots != null)
        {
            foreach (GameObject go in stackSlots)
            {
                if (fit >= quantity) break;
                if (go == null || !go.TryGetComponent(out InventorySlot s) || s.heldItem == null) continue;
                InventoryItem it = s.heldItem.GetComponent<InventoryItem>();
                if (it != null && it.itemScriptableObject == item)
                    fit += it.GetAvailableStackSpace();
            }
        }
        if (fit >= quantity)
            return quantity;
        // Try the new stacks in a copy of the grid (placeholders stand for the stacks that do not exist yet).
        var copy = new GridInventoryModel<object>(Columns, Rows, ReferenceComparer<object>.Instance);
        foreach (KeyValuePair<InventoryItem, GridRect> kv in Model.Placements)
            copy.Place(kv.Key, kv.Value);
        Vector2Int size = item.GridSize;
        bool allowRotate = item.CanRotateInGrid && Settings.autoRotateToFit;
        while (fit < quantity && copy.FindSpace(size.x, size.y, allowRotate, out GridRect r, out _))
        {
            copy.Place(new object(), r);
            fit += stackMax;
        }
        fit += freeExtraSlots * stackMax;
        return Mathf.Min(quantity, fit);
    }

    /// <summary>
    /// Plans where items would go in an EMPTY grid of this size (mode switching): biggest first, rotation allowed.
    /// Returns the items that would not fit.
    /// </summary>
    public static List<InventoryItem> PlanPacking(int columns, int rows, IList<InventoryItem> items, bool allowRotate,
        Dictionary<InventoryItem, GridRect> plan)
    {
        var left = new List<InventoryItem>();
        var m = new GridInventoryModel<InventoryItem>(columns, rows, ReferenceComparer<InventoryItem>.Instance);
        var sorted = new List<InventoryItem>(items);
        sorted.Sort((a, b) => Area(b).CompareTo(Area(a)));
        foreach (InventoryItem it in sorted)
        {
            if (it == null || it.itemScriptableObject == null)
                continue;
            Vector2Int s = it.itemScriptableObject.GridSize;
            if (it.gridRotated)
                s = new Vector2Int(s.y, s.x);
            bool rot = allowRotate && it.itemScriptableObject.CanRotateInGrid;
            if (m.FindSpace(s.x, s.y, rot, out GridRect r, out _))
            {
                m.Place(it, r);
                plan[it] = r;
            }
            else
            {
                left.Add(it);
            }
        }
        return left;
    }

    private static int Area(InventoryItem it)
    {
        if (it == null || it.itemScriptableObject == null) return 0;
        Vector2Int s = it.itemScriptableObject.GridSize;
        return s.x * s.y;
    }
}
