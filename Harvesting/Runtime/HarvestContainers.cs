using System;
using UnityEngine;

/// <summary>
/// The containers some materials must be taken into (<see cref="HarvestMaterial.container"/>): blood and venom need an
/// empty vial, bile a jar. The player's bag must hold one per piece when the work starts; one is used up for each piece
/// given. The container is an item of the Item Database, named by its id or its name.
/// </summary>
public static class HarvestContainers
{
    /// <summary>The container item named <paramref name="nameOrId"/> (an Item Database id, else an item's name), or null.</summary>
    public static ItemSO Find(string nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId))
            return null;
        string key = nameOrId.Trim();
        ItemSO item = ItemDatabase.Item(key);
        if (item != null)
            return item;
        ItemDatabase db = ItemDatabase.Instance;
        if (db == null || db.items == null)
            return null;
        foreach (ItemDatabase.Entry e in db.items)
            if (e.item != null && (string.Equals(e.item.Name, key, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(((UnityEngine.Object)e.item).name, key, StringComparison.OrdinalIgnoreCase)))
                return e.item;
        return null;
    }

    /// <summary>How many containers for <paramref name="m"/> the player carries (int.MaxValue when it needs none).</summary>
    public static int Count(CombatEntity player, HarvestMaterial m)
    {
        if (m == null || string.IsNullOrWhiteSpace(m.container))
            return int.MaxValue;
        ItemSO item = Find(m.container);
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        return item != null && inv != null ? inv.CountItem(item) : 0;
    }

    /// <summary>
    /// Can the player hold <paramref name="pieces"/> pieces of <paramref name="m"/>? False with why ("You need an empty vial
    /// to hold the blood - you have none.").
    /// </summary>
    public static bool Has(CombatEntity player, HarvestMaterial m, int pieces, out string reason)
    {
        reason = "";
        if (m == null || string.IsNullOrWhiteSpace(m.container))
            return true;
        int need = Mathf.Max(1, pieces);
        int have = Count(player, m);
        if (have >= need)
            return true;
        string what = m.container.Trim().ToLowerInvariant();
        string article = "aeiou".IndexOf(what.Length > 0 ? what[0] : 'x') >= 0 ? "an" : "a";
        reason = need == 1 || have == 0
            ? $"You need {article} {what} to hold the {m.Name.ToLowerInvariant()}{(have == 0 ? "" : $" ({have} carried)")}."
            : $"You need {need} × {what} to hold every piece (you carry {have}).";
        return false;
    }

    /// <summary>Uses up <paramref name="pieces"/> containers of <paramref name="m"/> from the player's bag (as they are filled).</summary>
    public static void Use(CombatEntity player, HarvestMaterial m, int pieces)
    {
        if (m == null || string.IsNullOrWhiteSpace(m.container) || pieces <= 0)
            return;
        ItemSO item = Find(m.container);
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (item != null && inv != null)
            inv.RemoveItems(item, pieces);
    }
}
