using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts the goods of animal husbandry in a merchant's shop (add it next to a <see cref="Merchant"/>): the care tools
/// (soft brush, curry comb, hoof pick, pitchfork) and the tools collecting takes (shears, a milking pail), the supplies
/// (straw, soap), the feeds, a crate of each species' first breed, and medicine - a standard bottle of every potion a
/// medicine names (the veterinary ones too) and the plain remedies (charcoal, wormwood, vinegar, lavender) - plus, when
/// asked, what animals give (eggs, milk, wool: bought in, so of middling quality and no known origin), and any item ids
/// listed in Extra.
/// <para>The animal items are made at runtime, so no scene or asset can list them: they are added to the merchant's own
/// goods as the scene wakes, before the shop is first opened, in the same order on every machine (a network client maps
/// the server's lines onto them). What the merchant lists already is not listed twice.</para>
/// </summary>
[RequireComponent(typeof(Merchant))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-60)]
[AddComponentMenu("SimpleMovements/Animals/Animal Goods (Merchant)")]
public class AnimalMerchantStock : MonoBehaviour
{
    [Header("What it sells")]
    [Tooltip("The tools of animal husbandry: the brushes, the hoof pick, the pitchfork (brushing, hoof care and mucking out need them), the shears and the milking pail (shearing and milking need them).")]
    public bool tools = true;
    [Tooltip("The supplies care uses: straw for bedding, soap for washing.")]
    public bool supplies = true;
    [Tooltip("Every feed (hay, grain, the feeds of each kind of animal).")]
    public bool feed = true;
    [Tooltip("A crate of each species' first breed.")]
    public bool livestock = true;
    [Tooltip("Medicine: a standard bottle of every potion a medicine names (the veterinary ones too) and the plain remedies (charcoal, wormwood, vinegar, lavender).")]
    public bool medicine = true;
    [Tooltip("What animals give: eggs, milk, wool (a grocer's, a tailor's). Bought in, they are of middling quality and of no known origin; the farm's own are better.")]
    public bool products;
    [Tooltip("Item ids sold as well: any Item Database id, an animal id (animal:feed/hay), an ingredient (ingredient:sage_leaves) or potion:<outcome> for a standard bottle.")]
    public List<string> extra = new List<string>();

    [Header("How many")]
    [Tooltip("The goods never run out.")]
    public bool unlimited = true;
    [Tooltip("When they do run out: how many of each the merchant has, and restocks to.")]
    [Min(1)] public int quantity = 20;

    private bool added;

    private void Awake() => AddGoods();

    /// <summary>
    /// Adds the goods to the merchant's own (once): every machine does the same, from the same content, before the shop
    /// is first opened (it is rebuilt from its entries after).
    /// </summary>
    public void AddGoods()
    {
        if (added) return;
        added = true;
        Merchant merchant = GetComponent<Merchant>();
        if (merchant == null) return;
        List<ItemSO> goods;
        try
        {
            goods = Goods(tools, supplies, feed, livestock, medicine, extra, products);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return;
        }
        var listed = new HashSet<ItemSO>();
        foreach (MerchantStockEntry e in merchant.AllEntries())
            if (e != null && e.item != null) listed.Add(e.item);
        int count = 0;
        foreach (ItemSO item in goods)
        {
            if (item == null || !listed.Add(item)) continue;
            merchant.StockEntries.Add(new MerchantStockEntry(item, Mathf.Max(1, quantity), unlimited));
            count++;
        }
        if (count > 0) merchant.RebuildStock();
    }

    /// <summary>
    /// The goods, in a fixed order (the same on every machine): tools, supplies, feed, livestock, medicine, products, then
    /// the extra ids. Ids that resolve to no item are left out (warned about once each, for the extra ones).
    /// </summary>
    public static List<ItemSO> Goods(bool tools, bool supplies, bool feed, bool livestock, bool medicine, IList<string> extra, bool products = false)
    {
        var list = new List<ItemSO>();
        AnimalDatabase db = AnimalDatabase.Instance;
        if (tools || supplies)
            foreach (AnimalToolData t in db.AllTools)
            {
                if (t == null) continue;
                if (!t.consumed)
                {
                    // Animal husbandry's own tools (another system's - the farm's shovel - is sold by its own merchants).
                    if (tools && t.IsItem(AnimalIds.Tool(t.id))) Add(list, AnimalItems.Tool(t.id));
                }
                else if (supplies) Add(list, AnimalToolkit.FirstItemOf(t.items));
            }
        if (feed) list.AddRange(AnimalItems.All(true, false, false));
        if (livestock)
            foreach (AnimalSpeciesData s in db.AllSpecies)
                if (s != null) Add(list, AnimalItems.Livestock(s.id, s.BreedOrDefault(null)?.id ?? ""));
        if (medicine)
        {
            // The veterinary outcomes must be in Alchemy before their bottles can be made.
            AnimalAlchemy.EnsureInstalled();
            foreach (AnimalTreatmentData tr in db.AllTreatments)
                if (tr != null) Add(list, AnimalToolkit.FirstItemOf(tr.items));
        }
        if (products)
            foreach (ItemSO item in AnimalItems.All(false, false, false, true)) Add(list, item);
        if (extra != null)
            foreach (string id in extra)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                ItemSO item = AnimalToolkit.ItemOfKey(id);
                if (item == null)
                {
                    if (unknown.Add(id.Trim())) Debug.LogWarning($"Animals: a merchant's extra goods name '{id.Trim()}', which is no item.");
                    continue;
                }
                Add(list, item);
            }
        return list;
    }

    private static readonly HashSet<string> unknown = new HashSet<string>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => unknown.Clear();

    private static void Add(List<ItemSO> list, ItemSO item)
    {
        if (item != null && !list.Contains(item)) list.Add(item);
    }

    private void Reset()
    {
        tools = supplies = feed = livestock = medicine = true;
        products = false;
        unlimited = true;
        quantity = 20;
    }
}
