using System;
using System.Collections.Generic;

/// <summary>
/// The pots each player has open at a station - a flask of oil, a cake of wax, a jar of dye, a pot of varnish: what is
/// left of the item opened, as a share of one item. There is one pot of each supply, whichever bench draws from it: the
/// beeswax opened to wax a hide waxes a bow stave too, the linseed oil that oiled leather oils a haft. A use comes out of
/// the open pot first; a new item is opened (used up) only when what is left will not cover it
/// (<see cref="CraftLeather.PotsToOpen"/>, in items). The leather's oils, waxes and dyes are measured in what the
/// leather takes in (<see cref="CraftLeather.Uptake"/> over <see cref="CraftSupplyDefinition.cellsPerItem"/>), the
/// woodshop's finishes in strokes of the brush (<see cref="CraftWoodwork.BrushUse"/>).
/// </summary>
public sealed class CraftOpenPots<TPlayer> where TPlayer : class
{
    private readonly Dictionary<(TPlayer player, string supply), double> left = new Dictionary<(TPlayer, string), double>();

    /// <summary>How many pots are open (any player, any supply).</summary>
    public int Count => left.Count;

    /// <summary>What is left in <paramref name="player"/>'s open pot of <paramref name="supply"/> (0-1 of an item; 0 = none open).</summary>
    public double Left(TPlayer player, string supply) => left.TryGetValue((player, supply), out double l) ? l : 0;

    /// <summary>How many new items a use of <paramref name="use"/> items opens: 0 when the open pot still covers it.</summary>
    public int ToOpen(TPlayer player, string supply, double use) => CraftLeather.PotsToOpen(use, Left(player, supply), 1, out _);

    /// <summary>
    /// The use was made: the items it opened (<see cref="ToOpen"/>) go into the pot and <paramref name="use"/> comes out
    /// of it. Returns what is left.
    /// </summary>
    public double Use(TPlayer player, string supply, double use)
    {
        CraftLeather.PotsToOpen(use, Left(player, supply), 1, out double after);
        left[(player, supply)] = after;
        return after;
    }

    /// <summary>The player left: their open pots go with them.</summary>
    public void Forget(TPlayer player)
    {
        var gone = new List<(TPlayer, string)>();
        foreach ((TPlayer player, string supply) k in left.Keys)
            if (ReferenceEquals(k.player, player)) gone.Add(k);
        foreach ((TPlayer, string) k in gone) left.Remove(k);
    }

    /// <summary>The pots of every player <paramref name="gone"/> says has left (a destroyed player object) are dropped.</summary>
    public void Prune(Func<TPlayer, bool> gone)
    {
        if (gone == null)
            return;
        List<(TPlayer, string)> drop = null;
        foreach ((TPlayer player, string supply) k in left.Keys)
            if (gone(k.player)) (drop ??= new List<(TPlayer, string)>()).Add(k);
        if (drop != null)
            foreach ((TPlayer, string) k in drop) left.Remove(k);
    }
}
