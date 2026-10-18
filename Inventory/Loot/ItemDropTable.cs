using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A whole-number range (min and max included).</summary>
[Serializable]
public struct DropAmount
{
    public int min;
    public int max;

    public DropAmount(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    public int Roll(System.Random rng) => rng.Next(Mathf.Min(min, max), Mathf.Max(min, max) + 1);
    public override string ToString() => min == max ? min.ToString() : $"{min}-{max}";
}

/// <summary>One item a table can drop: the item, how likely, how many, and from which tier.</summary>
[Serializable]
public class ItemDropEntry
{
    [Tooltip("The item dropped.")]
    public ItemSO item;
    [Tooltip("Relative chance among the pool's entries when the table rolls (ignored for Guaranteed drops).")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Chance (0-1) it really drops once picked (a guaranteed entry with 0.5 drops half the time).")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("How many (a stack; split into several pickups above the item's stack size).")]
    public DropAmount quantity = new DropAmount(1, 1);
    [Tooltip("Extra quantity per tier above the entry's Min Tier (0.5 = one more every two tiers).")]
    [Min(0f)] public float quantityPerTier;
    [Tooltip("Lowest tier of the source (chest tier, dungeon depth, mob tier) at which it can drop.")]
    [Min(0)] public int minTier;
    [Tooltip("Highest tier at which it can drop (-1 = no limit).")]
    public int maxTier = -1;

    public bool AllowedAt(int tier) => item != null && tier >= minTier && (maxTier < 0 || tier <= maxTier);

    public string Describe() =>
        item == null ? "(no item)" : $"{item.Name} x{quantity}{(chance < 0.999f ? $" ({chance * 100f:0}%)" : "")}" +
                                     (minTier > 0 || maxTier >= 0 ? $" [tier {minTier}{(maxTier >= 0 ? "-" + maxTier : "+")}]" : "");
}

/// <summary>Another table rolled as one entry of this table's pool (shared loot groups: "common materials", "rare gems").</summary>
[Serializable]
public class ItemDropGroup
{
    [Tooltip("The table rolled when this group is picked.")]
    public ItemDropTable table;
    [Tooltip("Relative chance among the pool's entries.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Lowest tier at which the group can be picked.")]
    [Min(0)] public int minTier;
    [Tooltip("Highest tier (-1 = no limit).")]
    public int maxTier = -1;

    public bool AllowedAt(int tier) => table != null && tier >= minTier && (maxTier < 0 || tier <= maxTier);
}

/// <summary>An item and a quantity (one roll's result).</summary>
public struct ItemStack
{
    public ItemSO item;
    public int quantity;

    public ItemStack(ItemSO item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public override string ToString() => item != null ? $"{item.Name} x{quantity}" : "(none)";
}

/// <summary>
/// What a chest, an urn, a barrel, a mob or a boss can drop: items that always drop (<see cref="guaranteed"/>), a weighted
/// <see cref="pool"/> rolled <see cref="rolls"/> times (more at higher tiers), and groups (other tables) for shared loot.
/// Every entry has its chance, quantity and tier range. Create one with Create ▸ SimpleMovements ▸ Items ▸ Item Drop Table,
/// register it in the <see cref="ItemDropManager"/>, and assign it to a dungeon loot / prop entry, a chest, a breakable
/// or a mob's <see cref="ItemDropOnDeath"/>. The inspector simulates rolls.
/// </summary>
[CreateAssetMenu(fileName = "ItemDropTable", menuName = "SimpleMovements/Items/Item Drop Table", order = 20)]
public class ItemDropTable : ScriptableObject
{
    [Tooltip("Name in the Item Drop Manager and the logs (empty = the asset's name).")]
    public string displayName = "";
    [TextArea(1, 3)] public string notes = "";

    [Header("Always")]
    [Tooltip("Dropped every time (each with its own chance, quantity and tiers).")]
    public List<ItemDropEntry> guaranteed = new List<ItemDropEntry>();

    [Header("Random pool")]
    [Tooltip("How many times the pool is rolled.")]
    public DropAmount rolls = new DropAmount(1, 2);
    [Tooltip("Extra rolls per tier of the source (0.5 = one more roll every two tiers; the fraction is a chance).")]
    [Min(0f)] public float extraRollsPerTier = 0.5f;
    [Tooltip("Chance (0-1) that a roll gives nothing (an empty urn).")]
    [Range(0f, 1f)] public float nothingChance = 0.2f;
    [Tooltip("The same entry cannot be picked twice in one drop.")]
    public bool uniquePicks = true;
    [Tooltip("The items of the pool (weighted).")]
    public List<ItemDropEntry> pool = new List<ItemDropEntry>();
    [Tooltip("Other tables rolled as entries of the pool (weighted with the items).")]
    public List<ItemDropGroup> groups = new List<ItemDropGroup>();

    public string Name => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>
    /// Rolls the table for a source of <paramref name="tier"/> and adds the result to <paramref name="into"/> (same items
    /// merged). <paramref name="chanceMultiplier"/> scales every chance (the manager's drop rate, elites);
    /// <paramref name="quantityMultiplier"/> every quantity.
    /// </summary>
    public void Roll(int tier, System.Random rng, List<ItemStack> into, float chanceMultiplier = 1f, float quantityMultiplier = 1f, int depth = 0)
    {
        if (into == null || rng == null || depth > 8)
            return;   // a group that contains itself
        tier = Mathf.Max(0, tier);
        foreach (ItemDropEntry d in guaranteed)
            Give(d, tier, rng, into, chanceMultiplier, quantityMultiplier);

        int count = Mathf.Max(0, rolls.Roll(rng));
        float extra = extraRollsPerTier * tier;
        count += Mathf.FloorToInt(extra);
        if (rng.NextDouble() < extra - Mathf.Floor(extra)) count++;

        var picked = new HashSet<object>();
        for (int r = 0; r < count; r++)
        {
            if (rng.NextDouble() < nothingChance)
                continue;
            object pick = Pick(tier, rng, uniquePicks ? picked : null);
            if (pick == null)
                break;   // nothing left to pick
            picked.Add(pick);
            if (pick is ItemDropEntry d)
                Give(d, tier, rng, into, chanceMultiplier, quantityMultiplier);
            else if (pick is ItemDropGroup g)
                g.table.Roll(tier, rng, into, chanceMultiplier, quantityMultiplier, depth + 1);
        }
    }

    private object Pick(int tier, System.Random rng, HashSet<object> exclude)
    {
        float total = 0f;
        foreach (ItemDropEntry d in pool)
            if (d != null && d.AllowedAt(tier) && d.weight > 0f && (exclude == null || !exclude.Contains(d))) total += d.weight;
        foreach (ItemDropGroup g in groups)
            if (g != null && g.AllowedAt(tier) && g.weight > 0f && (exclude == null || !exclude.Contains(g))) total += g.weight;
        if (total <= 0f)
            return null;
        double x = rng.NextDouble() * total;
        foreach (ItemDropEntry d in pool)
        {
            if (d == null || !d.AllowedAt(tier) || d.weight <= 0f || (exclude != null && exclude.Contains(d))) continue;
            if ((x -= d.weight) <= 0) return d;
        }
        foreach (ItemDropGroup g in groups)
        {
            if (g == null || !g.AllowedAt(tier) || g.weight <= 0f || (exclude != null && exclude.Contains(g))) continue;
            if ((x -= g.weight) <= 0) return g;
        }
        return null;
    }

    private static void Give(ItemDropEntry d, int tier, System.Random rng, List<ItemStack> into, float chanceMultiplier, float quantityMultiplier)
    {
        if (d == null || !d.AllowedAt(tier))
            return;
        if (ItemDropManager.Instance.IsBlocked(d.item))
            return;
        if (rng.NextDouble() >= Mathf.Clamp01(d.chance * chanceMultiplier))
            return;
        float qty = d.quantity.Roll(rng) + d.quantityPerTier * Mathf.Max(0, tier - d.minTier);
        int n = Mathf.Max(1, Mathf.RoundToInt(qty * Mathf.Max(0f, quantityMultiplier)));
        for (int i = 0; i < into.Count; i++)
        {
            if (into[i].item == d.item)
            {
                into[i] = new ItemStack(d.item, into[i].quantity + n);
                return;
            }
        }
        into.Add(new ItemStack(d.item, n));
    }

    /// <summary>Every item this table (and its groups) can drop.</summary>
    public void CollectItems(HashSet<ItemSO> into, int depth = 0)
    {
        if (into == null || depth > 8) return;
        foreach (ItemDropEntry d in guaranteed) if (d?.item != null) into.Add(d.item);
        foreach (ItemDropEntry d in pool) if (d?.item != null) into.Add(d.item);
        foreach (ItemDropGroup g in groups) if (g?.table != null) g.table.CollectItems(into, depth + 1);
    }

    /// <summary>Setup problems (empty items, a group containing this table).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        foreach (ItemDropEntry d in guaranteed)
            if (d != null && d.item == null) warnings.Add("A guaranteed entry has no item.");
        foreach (ItemDropEntry d in pool)
            if (d != null && d.item == null) warnings.Add("A pool entry has no item.");
        foreach (ItemDropEntry d in pool)
            if (d?.item != null && d.item.Prefab == null) errors.Add($"'{d.item.Name}' has no prefab: it cannot be dropped in the world.");
        foreach (ItemDropEntry d in guaranteed)
            if (d?.item != null && d.item.Prefab == null) errors.Add($"'{d.item.Name}' has no prefab: it cannot be dropped in the world.");
        foreach (ItemDropGroup g in groups)
            if (g?.table == this) errors.Add("A group uses this same table (it would roll forever).");
        if (guaranteed.Count == 0 && pool.Count == 0 && groups.Count == 0)
            warnings.Add("The table is empty: it never drops anything.");
    }
}
