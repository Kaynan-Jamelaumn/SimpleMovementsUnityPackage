using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One item a merchant sells: how many, whether it runs out and is restocked, and its price.</summary>
[Serializable]
public class MerchantStockEntry
{
    [Tooltip("The item sold.")]
    public ItemSO item;
    [Tooltip("Never runs out.")]
    public bool unlimited;
    [Tooltip("How many the merchant has at the start.")]
    [Min(0)] public int quantity = 5;
    [Tooltip("Most the merchant restocks up to (0 = the starting Quantity).")]
    [Min(0)] public int maxQuantity;
    [Tooltip("How many come back at each restock (0 = refilled to the maximum).")]
    [Min(0)] public int restockAmount;
    [Tooltip("Multiplies this item's price at this merchant (1 = normal, 1.5 = 50% dearer). Ignored when Fixed Price is set.")]
    [Min(0f)] public float priceMultiplier = 1f;
    [Tooltip("Exact price per item, whatever the item's value and the merchant's pricing (0 = calculated).")]
    [Min(0)] public int fixedPrice;
    [Tooltip("List the item under this category in this shop instead of its own (optional).")]
    public ItemCategory categoryOverride;

    public MerchantStockEntry() { }

    public MerchantStockEntry(ItemSO item, int quantity, bool unlimited = false)
    {
        this.item = item;
        this.quantity = quantity;
        this.unlimited = unlimited;
    }

    /// <summary>Restock target.</summary>
    public int MaxQuantity => maxQuantity > 0 ? Mathf.Max(maxQuantity, 1) : Mathf.Max(quantity, 1);

    public void Validate(string label, List<string> errors, List<string> warnings)
    {
        if (item == null)
        {
            errors.Add($"{label}: no item.");
            return;
        }
        if (!unlimited && quantity <= 0 && restockAmount <= 0 && maxQuantity <= 0)
            warnings.Add($"{label} ({item.Name}): quantity 0 and nothing to restock - it is always sold out.");
        if (fixedPrice <= 0 && item.Price <= 0f)
            warnings.Add($"{label} ({item.Name}): the item has no Price (value) and no Fixed Price is set - it sells for the minimum price.");
        if (item.Icon == null)
            warnings.Add($"{label} ({item.Name}): the item has no icon.");
    }
}

/// <summary>
/// A reusable list of goods (a blacksmith's stock, a general store). Merchants can share one and add their own entries.
/// </summary>
[CreateAssetMenu(fileName = "Merchant Stock", menuName = "SimpleMovements/NPC/Merchant Stock", order = 1)]
public class MerchantStock : ScriptableObject
{
    [Tooltip("The goods.")]
    [SerializeField] private List<MerchantStockEntry> entries = new List<MerchantStockEntry>();

    public List<MerchantStockEntry> Entries => entries ?? (entries = new List<MerchantStockEntry>());

    public void Validate(List<string> errors, List<string> warnings)
    {
        var seen = new HashSet<ItemSO>();
        for (int i = 0; i < Entries.Count; i++)
        {
            MerchantStockEntry e = Entries[i];
            if (e == null) continue;
            e.Validate($"{name} #{i + 1}", errors, warnings);
            if (e.item != null && !seen.Add(e.item))
                warnings.Add($"{name}: {e.item.Name} is listed more than once (each entry is a separate stack in the shop).");
        }
    }
}

/// <summary>
/// One line of a merchant's goods at runtime: the item, how many are left, and where it came from (a stock entry, or an
/// item the player sold that the merchant now sells back).
/// </summary>
public sealed class MerchantStockSlot
{
    /// <summary>Identifies the slot in transaction requests (stable while the merchant exists).</summary>
    public int Id { get; }
    public ItemSO Item { get; }
    /// <summary>The configured entry (null for items bought from the player).</summary>
    public MerchantStockEntry Source { get; }
    public bool Unlimited { get; }
    /// <summary>An item the player sold (sold back at the normal price; cleared at restock when the merchant says so).</summary>
    public bool IsBuyback { get; }
    public int Quantity { get; internal set; }
    /// <summary>Order in which it was added (keeps the configured order when sorting by "Default").</summary>
    public int Order { get; }

    public bool InStock => Unlimited || Quantity > 0;

    internal MerchantStockSlot(int id, int order, ItemSO item, MerchantStockEntry source, int quantity, bool unlimited, bool buyback)
    {
        Id = id;
        Order = order;
        Item = item;
        Source = source;
        Quantity = Mathf.Max(0, quantity);
        Unlimited = unlimited;
        IsBuyback = buyback;
    }

    /// <summary>How many can be bought now (int.MaxValue when unlimited).</summary>
    public int Available => Unlimited ? int.MaxValue : Quantity;
}

/// <summary>A merchant's goods at runtime: the slots, restocking and items bought from players.</summary>
public sealed class MerchantInventory
{
    private readonly List<MerchantStockSlot> slots = new List<MerchantStockSlot>();
    private int nextId = 1;
    private int nextOrder;

    /// <summary>Raised after quantities or slots changed.</summary>
    public event Action Changed;

    public IReadOnlyList<MerchantStockSlot> Slots => slots;

    /// <summary>Builds the slots from the configured entries (merchant start).</summary>
    public void Initialize(IEnumerable<MerchantStockEntry> entries)
    {
        slots.Clear();
        if (entries != null)
            foreach (MerchantStockEntry e in entries)
                if (e != null && e.item != null)
                    slots.Add(new MerchantStockSlot(nextId++, nextOrder++, e.item, e, e.quantity, e.unlimited, false));
        Changed?.Invoke();
    }

    public MerchantStockSlot Find(int id)
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].Id == id)
                return slots[i];
        return null;
    }

    /// <summary>Takes up to <paramref name="amount"/> from a slot. Returns how many were taken.</summary>
    public int Take(MerchantStockSlot slot, int amount)
    {
        if (slot == null || amount <= 0 || !slots.Contains(slot))
            return 0;
        if (slot.Unlimited)
            return amount;
        int n = Mathf.Min(amount, slot.Quantity);
        slot.Quantity -= n;
        if (slot.Quantity <= 0 && slot.IsBuyback)
            slots.Remove(slot); // a sold-back item that is gone leaves the list
        if (n > 0)
            Changed?.Invoke();
        return n;
    }

    /// <summary>
    /// Items the player sold: added to the slot of the same configured item when there is one (limited stock), else to a
    /// buy-back slot. <paramref name="maxBuybackSlots"/> limits how many different sold items are kept (oldest go first).
    /// </summary>
    public void AddBought(ItemSO item, int amount, int maxBuybackSlots)
    {
        if (item == null || amount <= 0)
            return;
        foreach (MerchantStockSlot s in slots)
            if (s.Item == item && !s.Unlimited)
            {
                s.Quantity += amount;
                Changed?.Invoke();
                return;
            }
        foreach (MerchantStockSlot s in slots)
            if (s.Item == item && s.Unlimited)
                return; // sold anyway
        if (maxBuybackSlots <= 0)
            return;
        int buybacks = 0;
        foreach (MerchantStockSlot s in slots)
            if (s.IsBuyback) buybacks++;
        while (buybacks >= maxBuybackSlots)
        {
            int oldest = slots.FindIndex(s => s.IsBuyback);
            if (oldest < 0) break;
            slots.RemoveAt(oldest);
            buybacks--;
        }
        slots.Add(new MerchantStockSlot(nextId++, nextOrder++, item, null, amount, false, true));
        Changed?.Invoke();
    }

    /// <summary>Configured items come back (to their maximum, or by their Restock Amount); bought-back items may be cleared.</summary>
    public void Restock(bool clearBuyback)
    {
        bool changed = false;
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            MerchantStockSlot s = slots[i];
            if (s.IsBuyback)
            {
                if (clearBuyback)
                {
                    slots.RemoveAt(i);
                    changed = true;
                }
                continue;
            }
            if (s.Unlimited || s.Source == null)
                continue;
            int max = s.Source.MaxQuantity;
            int target = s.Source.restockAmount > 0 ? Mathf.Min(max, s.Quantity + s.Source.restockAmount) : max;
            if (target > s.Quantity)
            {
                s.Quantity = target;
                changed = true;
            }
        }
        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Sets a slot's quantity (loading a save, server updates).</summary>
    public void SetQuantity(MerchantStockSlot slot, int quantity)
    {
        if (slot == null || slot.Unlimited || !slots.Contains(slot))
            return;
        slot.Quantity = Mathf.Max(0, quantity);
        Changed?.Invoke();
    }
}
