using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>How a shop window lays its items out.</summary>
public enum MerchantLayoutMode
{
    /// <summary>Like the player's inventory: slots when it uses slots, a grid (items take their Grid Size) when it uses the grid.</summary>
    FollowInventory,
    /// <summary>Always one item per slot.</summary>
    Slots,
    /// <summary>Always a grid where items take their Grid Size.</summary>
    Grid,
}

/// <summary>
/// The shop of an NPC (the "Trade" option): goods with stock and restocking, buying items from the player, prices,
/// categories and the shop window. Add it to an NPC (or use GameObject ▸ SimpleMovements ▸ NPC ▸ Merchant NPC).
/// <para>Every purchase and sale is a <see cref="MerchantTransactionRequest"/> handled by <see cref="Processor"/>
/// (on this machine by default; replace it for a server-authoritative game), so the window never changes the inventory,
/// the wallet or the stock itself.</para>
/// </summary>
[AddComponentMenu("SimpleMovements/NPC/Merchant")]
public class Merchant : NPCBehaviour
{
    [Header("Shop")]
    [Tooltip("Shown at the top of the shop window. Empty = the NPC's name.")]
    [SerializeField] private string shopName;
    [Tooltip("Line under the shop name (\"Finest steel in the valley!\").")]
    [SerializeField] private string shopGreeting = "Take a look at my wares.";
    [Tooltip("Currency of the prices. Empty = the player's default currency (Gold).")]
    [SerializeField] private CurrencyDefinition currency;

    [Header("Goods")]
    [Tooltip("A shared list of goods (optional). The entries below are added to it.")]
    [SerializeField] private MerchantStock stockAsset;
    [Tooltip("What this merchant sells.")]
    [SerializeField] private List<MerchantStockEntry> stock = new List<MerchantStockEntry>();
    [Tooltip("Minutes between restocks (0 = never). Limited goods come back to their maximum (or by their Restock Amount).")]
    [SerializeField, Min(0f)] private float restockEveryMinutes = 10f;
    [Tooltip("Items bought from the player are forgotten at each restock.")]
    [SerializeField] private bool clearSoldItemsOnRestock = true;

    [Header("Buying From The Player")]
    [Tooltip("The player can sell items here (the Sell tab).")]
    [SerializeField] private bool buysItems = true;
    [Tooltip("Only items of these categories (and their subcategories) are bought. Empty = everything that can be sold.")]
    [SerializeField] private List<ItemCategory> buysCategories = new List<ItemCategory>();
    [Tooltip("Items this merchant never buys.")]
    [SerializeField] private List<ItemSO> refusedItems = new List<ItemSO>();
    [Tooltip("Items whose selling price is 0 are refused (\"worthless\").")]
    [SerializeField] private bool refuseWorthlessItems = true;
    [Tooltip("Items bought from the player are put up for sale (the player can buy them back).")]
    [SerializeField] private bool resellBoughtItems = true;
    [Tooltip("How many different bought items are kept for sale (oldest go first).")]
    [SerializeField, Min(0)] private int maxBuybackLines = 12;

    [Header("Pricing")]
    [SerializeField] private MerchantPricing pricing = new MerchantPricing();

    [Header("Categories")]
    [Tooltip("Categories of this shop. Empty = the project's Item Category Database (Resources), else the built-in categories.")]
    [SerializeField] private ItemCategoryDatabase categoryDatabase;
    [Tooltip("Tabs shown, in this order (top categories). Empty = a tab for every category the goods (or the player's sellable items) are in.")]
    [SerializeField] private List<ItemCategory> shownCategories = new List<ItemCategory>();
    [Tooltip("An 'All' tab listing everything.")]
    [SerializeField] private bool showAllTab = true;
    [Tooltip("Subcategory filters under the selected tab (Weapons ▸ Swords, Axes...).")]
    [SerializeField] private bool showSubcategories = true;
    [Tooltip("Shown Categories that have nothing in them still get a tab.")]
    [SerializeField] private bool showEmptyCategories;

    [Header("Window")]
    [Tooltip("Look of the shop window: your prefabs, sprites, fonts, colours and sounds. Empty = the default look.")]
    [SerializeField] private MerchantUISkin skin;
    [Tooltip("Follow Inventory: slots when the player's inventory uses slots, a grid (items take their Grid Size) when it uses the grid inventory.")]
    [SerializeField] private MerchantLayoutMode layout = MerchantLayoutMode.FollowInventory;
    [Tooltip("Columns of the grid layout when buying (0 = as many as the player's grid inventory).")]
    [SerializeField, Min(0)] private int gridColumns;
    [Tooltip("Ask before buying.")]
    [SerializeField] private bool confirmPurchases;
    [Tooltip("Ask before selling.")]
    [SerializeField] private bool confirmSales;
    [Tooltip("Only ask when the total is at least this much (0 = always, when asking is on).")]
    [SerializeField, Min(0)] private int confirmAbove;

    [Header("Events")]
    public UnityEvent onItemBought = new UnityEvent();
    public UnityEvent onItemSold = new UnityEvent();

    /// <summary>Used by every merchant without a processor of its own (the local one by default).</summary>
    public static IMerchantTransactionProcessor GlobalProcessor { get; set; } = LocalMerchantTransactionProcessor.Instance;
    /// <summary>Any merchant completed (or refused) a transaction.</summary>
    public static event Action<Merchant, MerchantTransactionResult> AnyTransaction;
    /// <summary>A transaction of this merchant completed (or was refused).</summary>
    public event Action<MerchantTransactionResult> TransactionCompleted;

    private MerchantInventory inventory;
    private IMerchantTransactionProcessor processor;
    private float nextRestockAt;
    private readonly List<MerchantStockEntry> entryBuffer = new List<MerchantStockEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GlobalProcessor = LocalMerchantTransactionProcessor.Instance;
        AnyTransaction = null;
    }

    // ------------------------------------------------------------------ properties
    public string ShopName => !string.IsNullOrWhiteSpace(shopName) ? shopName : Npc != null ? Npc.DisplayName : name;
    public string ShopGreeting => shopGreeting;
    public bool BuysItems => buysItems;
    public bool ResellsBoughtItems => resellBoughtItems;
    public int MaxBuybackLines => maxBuybackLines;
    public bool RefusesWorthlessItems => refuseWorthlessItems;
    public MerchantPricing Pricing => pricing ?? (pricing = new MerchantPricing());
    public MerchantUISkin Skin => skin;
    public MerchantLayoutMode Layout => layout;
    public int GridColumns => gridColumns;
    public bool ShowAllTab => showAllTab;
    public bool ShowSubcategories => showSubcategories;
    public bool ShowEmptyCategories => showEmptyCategories;
    public IReadOnlyList<ItemCategory> ShownCategories => shownCategories;
    public List<MerchantStockEntry> StockEntries => stock ?? (stock = new List<MerchantStockEntry>());
    public MerchantStock StockAsset => stockAsset;
    public float RestockEveryMinutes => restockEveryMinutes;
    /// <summary>Seconds until the next restock (-1 = never).</summary>
    public float SecondsUntilRestock => restockEveryMinutes > 0f && inventory != null ? Mathf.Max(0f, nextRestockAt - Time.time) : -1f;

    public ItemCategoryDatabase Categories => categoryDatabase != null ? categoryDatabase : ItemCategoryDatabase.Default;

    /// <summary>Handles this merchant's requests (the global one when none is set).</summary>
    public IMerchantTransactionProcessor Processor
    {
        get => processor ?? GlobalProcessor ?? LocalMerchantTransactionProcessor.Instance;
        set => processor = value;
    }

    /// <summary>The goods at runtime (created from the stock entries on first use).</summary>
    public MerchantInventory Inventory
    {
        get
        {
            if (inventory == null)
            {
                inventory = new MerchantInventory();
                inventory.Initialize(AllEntries());
                nextRestockAt = Time.time + restockEveryMinutes * 60f;
            }
            return inventory;
        }
    }

    /// <summary>The stock asset's entries followed by this merchant's own.</summary>
    public List<MerchantStockEntry> AllEntries()
    {
        entryBuffer.Clear();
        if (stockAsset != null)
            entryBuffer.AddRange(stockAsset.Entries);
        entryBuffer.AddRange(StockEntries);
        return entryBuffer;
    }

    // ------------------------------------------------------------------ NPC option
    private void Reset() => optionLabel = "Trade";

    public override string PromptVerb => OptionLabel;

    public override void Begin(NPCInteractionSession session)
    {
        CheckRestock();
        MerchantWindow.Open(this, session);
    }

    public override void End(NPCInteractionSession session) => MerchantWindow.CloseFor(this, session);

    private void Update() => CheckRestock();

    private void CheckRestock()
    {
        if (inventory == null || restockEveryMinutes <= 0f || Time.time < nextRestockAt)
            return;
        Restock();
    }

    /// <summary>Restocks now (limited goods come back; sold items may be cleared).</summary>
    public void Restock()
    {
        Inventory.Restock(clearSoldItemsOnRestock);
        nextRestockAt = Time.time + Mathf.Max(0.1f, restockEveryMinutes) * 60f;
    }

    /// <summary>Starts over from the configured entries (after changing them at runtime).</summary>
    public void RebuildStock()
    {
        Inventory.Initialize(AllEntries());
        nextRestockAt = Time.time + restockEveryMinutes * 60f;
    }

    // ------------------------------------------------------------------ prices and rules
    /// <summary>The currency of the prices for a player with <paramref name="wallet"/>.</summary>
    public CurrencyDefinition CurrencyFor(ICurrencyAccount wallet)
    {
        if (currency != null) return currency;
        if (wallet != null && wallet.DefaultCurrency != null) return wallet.DefaultCurrency;
        return CurrencyDefinition.Default;
    }

    /// <summary>The currency set on the merchant (null = the player's default).</summary>
    public CurrencyDefinition CurrencyOverride => currency;

    /// <summary>Where an item is listed in this shop (a stock entry's Category Override wins).</summary>
    public ItemCategoryMatch CategoryOf(ItemSO item, MerchantStockEntry entry = null)
    {
        ItemCategory over = entry != null ? entry.categoryOverride : null;
        if (over != null)
            return over.IsSubcategory ? new ItemCategoryMatch(over.Root, over) : new ItemCategoryMatch(over, null);
        return Categories.Resolve(item);
    }

    /// <summary>Price the player pays for one item of <paramref name="slot"/>.</summary>
    public int GetBuyPrice(MerchantStockSlot slot, GameObject player = null)
    {
        if (slot == null || slot.Item == null)
            return 0;
        return Pricing.BuyPrice(new MerchantPriceContext(this, slot.Item, true, slot, null, player, CategoryOf(slot.Item, slot.Source)));
    }

    /// <summary>Price the player gets for one item of <paramref name="stack"/> (0 = worthless).</summary>
    public int GetSellPrice(InventoryItem stack, GameObject player = null)
    {
        if (stack == null || stack.itemScriptableObject == null)
            return 0;
        ItemSO item = stack.itemScriptableObject;
        return Pricing.SellPrice(new MerchantPriceContext(this, item, false, null, stack, player, CategoryOf(item)));
    }

    /// <summary>Price for one undamaged <paramref name="item"/> (inspector previews).</summary>
    public int PreviewSellPrice(ItemSO item) =>
        item == null ? 0 : Pricing.SellPrice(new MerchantPriceContext(this, item, false, null, null, null, CategoryOf(item)));

    /// <summary>Price of one <paramref name="entry"/> (inspector previews).</summary>
    public int PreviewBuyPrice(MerchantStockEntry entry)
    {
        if (entry == null || entry.item == null)
            return 0;
        var slot = new MerchantStockSlot(0, 0, entry.item, entry, entry.MaxQuantity, entry.unlimited, false);
        return Pricing.BuyPrice(new MerchantPriceContext(this, entry.item, true, slot, null, null, CategoryOf(entry.item, entry)));
    }

    /// <summary>Does the merchant buy <paramref name="item"/>? <paramref name="reason"/> explains a refusal.</summary>
    public bool WillBuy(ItemSO item, out string reason)
    {
        reason = "";
        if (item == null)
            return false;
        if (!buysItems)
        {
            reason = $"{ShopName} does not buy anything.";
            return false;
        }
        if (!item.CanBeSold)
        {
            reason = $"{item.Name} cannot be sold.";
            return false;
        }
        CurrencyDefinition c = CurrencyFor(null);
        if (c != null && c.BackingItem == item)
        {
            reason = "That is money.";
            return false;
        }
        if (refusedItems != null && refusedItems.Contains(item))
        {
            reason = $"{ShopName} does not want {item.Name}.";
            return false;
        }
        if (buysCategories != null && buysCategories.Count > 0)
        {
            ItemCategoryMatch match = CategoryOf(item);
            bool wanted = false;
            foreach (ItemCategory cat in buysCategories)
                if (cat != null && match.IsIn(cat)) { wanted = true; break; }
            if (!wanted)
            {
                reason = $"{ShopName} does not buy {(match.Leaf != null ? match.Leaf.DisplayName.ToLowerInvariant() : "that")}.";
                return false;
            }
        }
        return true;
    }

    /// <summary>Is <paramref name="top"/> one of the shop's tabs?</summary>
    public bool IsCategoryShown(ItemCategory top)
    {
        if (shownCategories == null || shownCategories.Count == 0)
            return true;
        foreach (ItemCategory c in shownCategories)
            if (c != null && (c == top || (top != null && c.Root == top)))
                return true;
        return false;
    }

    /// <summary>Should a purchase / sale of <paramref name="total"/> be confirmed first?</summary>
    public bool NeedsConfirmation(MerchantTransactionType type, int total)
    {
        bool ask = type == MerchantTransactionType.Buy ? confirmPurchases : confirmSales;
        return ask && total >= confirmAbove;
    }

    // ------------------------------------------------------------------ transactions
    public MerchantTransactionResult QuoteBuy(GameObject player, MerchantStockSlot slot, int quantity) =>
        MerchantTransactions.Quote(MerchantTransactionRequest.Buy(this, player, slot, quantity));

    public MerchantTransactionResult QuoteSell(GameObject player, InventoryItem stack, int quantity) =>
        MerchantTransactions.Quote(MerchantTransactionRequest.Sell(this, player, stack, quantity));

    /// <summary>Sends a request to the processor; <paramref name="onComplete"/> gets the result (at once when local).</summary>
    public void Submit(MerchantTransactionRequest request, Action<MerchantTransactionResult> onComplete) => Processor.Submit(request, onComplete);

    /// <summary>Called by <see cref="MerchantTransactions.Execute"/> after every request.</summary>
    internal void OnTransaction(MerchantTransactionResult result)
    {
        if (result.Succeeded)
        {
            if (result.Request.Type == MerchantTransactionType.Buy) onItemBought?.Invoke();
            else onItemSold?.Invoke();
        }
        TransactionCompleted?.Invoke(result);
        AnyTransaction?.Invoke(this, result);
    }

    // ------------------------------------------------------------------ validation
    public override void Validate(List<string> errors, List<string> warnings)
    {
        var seen = new HashSet<ItemSO>();
        List<MerchantStockEntry> entries = AllEntries();
        if (entries.Count == 0 && !buysItems)
            warnings.Add($"{OptionLabel} (Merchant) sells nothing and buys nothing.");
        else if (entries.Count == 0)
            warnings.Add($"{OptionLabel} (Merchant) has no goods: only the Sell tab is useful.");
        for (int i = 0; i < StockEntries.Count; i++)
        {
            MerchantStockEntry e = StockEntries[i];
            if (e == null) continue;
            e.Validate($"Goods #{i + 1}", errors, warnings);
            if (e.item != null && !seen.Add(e.item))
                warnings.Add($"Goods #{i + 1}: {e.item.Name} is listed more than once.");
        }
        stockAsset?.Validate(errors, warnings);
        Pricing.Validate(errors, warnings);
        if (shownCategories != null)
            foreach (ItemCategory c in shownCategories)
                if (c != null && c.IsSubcategory)
                    warnings.Add($"Shown Categories: '{c.Path}' is a subcategory; its top category '{c.Root.DisplayName}' becomes the tab.");
        if (layout != MerchantLayoutMode.Slots)
            foreach (MerchantStockEntry e in entries)
                if (e?.item != null && gridColumns > 0 && Mathf.Min(e.item.GridSize.x, e.item.GridSize.y) > gridColumns)
                    warnings.Add($"{e.item.Name} ({e.item.GridSize.x}×{e.item.GridSize.y}) is wider than the {gridColumns} grid columns of the shop.");
        if (skin != null)
            skin.Validate(errors, warnings);
    }
}
