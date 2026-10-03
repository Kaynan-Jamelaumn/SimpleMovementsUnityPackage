using System;
using System.Collections.Generic;

/// <summary>A rectangle of cells in a grid inventory: top-left cell (X, Y) and size (W × H).</summary>
[Serializable]
public struct GridRect : IEquatable<GridRect>
{
    public int X, Y, W, H;

    public GridRect(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public int Right => X + W;
    public int Bottom => Y + H;
    public int Area => W * H;

    public bool Overlaps(GridRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
    public bool Contains(int cx, int cy) => cx >= X && cx < Right && cy >= Y && cy < Bottom;
    /// <summary>The same rectangle turned sideways (same top-left cell).</summary>
    public GridRect Rotated => new GridRect(X, Y, H, W);

    public bool Equals(GridRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
    public override bool Equals(object obj) => obj is GridRect r && Equals(r);
    public override int GetHashCode() => ((X * 397 ^ Y) * 397 ^ W) * 397 ^ H;
    public override string ToString() => $"({X},{Y}) {W}x{H}";
}

/// <summary>
/// The logic of a grid inventory, independent of Unity: which item occupies which cells. It is the single source of
/// truth for placement rules - in bounds, no overlap - and offers first-fit search (with rotation), moves and swaps.
/// <typeparamref name="TItem"/> is whatever identifies an item (the inventory uses its inventory items).
/// </summary>
public sealed class GridInventoryModel<TItem> where TItem : class
{
    private readonly TItem[] cells;
    private readonly Dictionary<TItem, GridRect> placed;

    public int Width { get; }
    public int Height { get; }
    public int Count => placed.Count;
    public IEnumerable<KeyValuePair<TItem, GridRect>> Placements => placed;

    public GridInventoryModel(int width, int height, IEqualityComparer<TItem> comparer = null)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "A grid needs at least 1×1 cells.");
        Width = width;
        Height = height;
        cells = new TItem[width * height];
        placed = new Dictionary<TItem, GridRect>(comparer ?? EqualityComparer<TItem>.Default);
    }

    /// <summary>A copy (to try placements without changing this one).</summary>
    public GridInventoryModel<TItem> Clone()
    {
        var c = new GridInventoryModel<TItem>(Width, Height);
        foreach (KeyValuePair<TItem, GridRect> kv in placed)
            c.Place(kv.Key, kv.Value);
        return c;
    }

    public bool InBounds(GridRect r) => r.W >= 1 && r.H >= 1 && r.X >= 0 && r.Y >= 0 && r.Right <= Width && r.Bottom <= Height;

    /// <summary>The item covering a cell (null = free or outside).</summary>
    public TItem At(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? cells[y * Width + x] : null;

    public bool TryGet(TItem item, out GridRect rect) => placed.TryGetValue(item, out rect);
    public bool Contains(TItem item) => item != null && placed.ContainsKey(item);

    /// <summary>Is <paramref name="r"/> inside the grid and free (the items in <paramref name="ignore"/> do not count)?</summary>
    public bool CanPlace(GridRect r, TItem ignore = null)
    {
        if (!InBounds(r))
            return false;
        for (int y = r.Y; y < r.Bottom; y++)
            for (int x = r.X; x < r.Right; x++)
            {
                TItem o = cells[y * Width + x];
                if (o != null && !ReferenceEquals(o, ignore))
                    return false;
            }
        return true;
    }

    /// <summary>As <see cref="CanPlace(GridRect, TItem)"/>, ignoring several items.</summary>
    public bool CanPlace(GridRect r, ICollection<TItem> ignore)
    {
        if (!InBounds(r))
            return false;
        for (int y = r.Y; y < r.Bottom; y++)
            for (int x = r.X; x < r.Right; x++)
            {
                TItem o = cells[y * Width + x];
                if (o != null && (ignore == null || !ignore.Contains(o)))
                    return false;
            }
        return true;
    }

    /// <summary>The distinct items overlapping <paramref name="r"/> (clipped to the grid), except <paramref name="ignore"/>.</summary>
    public void Occupants(GridRect r, List<TItem> into, TItem ignore = null)
    {
        into.Clear();
        int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y);
        int x1 = Math.Min(Width, r.Right), y1 = Math.Min(Height, r.Bottom);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                TItem o = cells[y * Width + x];
                if (o != null && !ReferenceEquals(o, ignore) && !into.Contains(o))
                    into.Add(o);
            }
    }

    /// <summary>Puts an item at <paramref name="r"/> (moving it if it was placed). False (nothing changes) when it does not fit.</summary>
    public bool Place(TItem item, GridRect r)
    {
        if (item == null || !CanPlace(r, item))
            return false;
        Remove(item);
        placed[item] = r;
        Fill(r, item);
        return true;
    }

    public bool Remove(TItem item)
    {
        if (item == null || !placed.TryGetValue(item, out GridRect r))
            return false;
        placed.Remove(item);
        Fill(r, null);
        return true;
    }

    public void Clear()
    {
        Array.Clear(cells, 0, cells.Length);
        placed.Clear();
    }

    /// <summary>Moves a placed item to <paramref name="to"/> when that is valid (its own cells do not block it).</summary>
    public bool Move(TItem item, GridRect to) => placed.ContainsKey(item) && Place(item, to);

    /// <summary>
    /// First free place for a <paramref name="w"/>×<paramref name="h"/> item, scanning rows top to bottom, left to right;
    /// with <paramref name="allowRotate"/> the sideways orientation is tried where the upright one does not fit.
    /// </summary>
    public bool FindSpace(int w, int h, bool allowRotate, out GridRect rect, out bool rotated, TItem ignore = null)
    {
        rotated = false;
        rect = default;
        if (w < 1 || h < 1)
            return false;
        bool tryRotated = allowRotate && w != h;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var r = new GridRect(x, y, w, h);
                if (CanPlace(r, ignore))
                {
                    rect = r;
                    return true;
                }
                if (tryRotated)
                {
                    GridRect rr = r.Rotated;
                    if (CanPlace(rr, ignore))
                    {
                        rect = rr;
                        rotated = true;
                        return true;
                    }
                }
            }
        return false;
    }

    /// <summary>Free cells.</summary>
    public int FreeCells
    {
        get
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == null) n++;
            return n;
        }
    }

    /// <summary>
    /// Swaps two items: <paramref name="a"/> to <paramref name="aTo"/> and <paramref name="b"/> to <paramref name="bTo"/>,
    /// only when both fit at once. Nothing changes otherwise.
    /// </summary>
    public bool Swap(TItem a, GridRect aTo, TItem b, GridRect bTo)
    {
        if (a == null || b == null || ReferenceEquals(a, b) || aTo.Overlaps(bTo))
            return false;
        var ignore = new[] { a, b };
        if (!CanPlace(aTo, ignore) || !CanPlace(bTo, ignore))
            return false;
        Remove(a);
        Remove(b);
        placed[a] = aTo;
        Fill(aTo, a);
        placed[b] = bTo;
        Fill(bTo, b);
        return true;
    }

    /// <summary>Do the cells agree with the placements (no overlaps, nothing outside)? For tests and validation.</summary>
    public bool IsConsistent(out string problem)
    {
        var seen = new TItem[cells.Length];
        foreach (KeyValuePair<TItem, GridRect> kv in placed)
        {
            GridRect r = kv.Value;
            if (!InBounds(r))
            {
                problem = $"{kv.Key} is outside the grid at {r}.";
                return false;
            }
            for (int y = r.Y; y < r.Bottom; y++)
                for (int x = r.X; x < r.Right; x++)
                {
                    int i = y * Width + x;
                    if (seen[i] != null)
                    {
                        problem = $"{kv.Key} overlaps {seen[i]} at ({x},{y}).";
                        return false;
                    }
                    seen[i] = kv.Key;
                    if (!ReferenceEquals(cells[i], kv.Key))
                    {
                        problem = $"Cell ({x},{y}) does not record {kv.Key}.";
                        return false;
                    }
                }
        }
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null && seen[i] == null)
            {
                problem = $"Cell ({i % Width},{i / Width}) records an item that is not placed.";
                return false;
            }
        problem = null;
        return true;
    }

    private void Fill(GridRect r, TItem value)
    {
        for (int y = r.Y; y < r.Bottom; y++)
            for (int x = r.X; x < r.Right; x++)
                cells[y * Width + x] = value;
    }
}
