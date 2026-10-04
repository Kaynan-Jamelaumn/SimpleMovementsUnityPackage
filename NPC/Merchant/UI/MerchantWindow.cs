using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shop window: Buy and Sell tabs, category tabs and subcategory filters (from the item categories), search, sort and
/// filters, the items, the selected item's details with a quantity and the Buy / Sell button, the player's money, and
/// confirmation of purchases. It lays the items out like the player's inventory - slots, or a grid where items take
/// their Grid Size - and when selling shows the player's bag exactly as their inventory shows it.
/// <para>It changes nothing itself: purchases and sales go to the merchant as requests, and the window refreshes when the
/// stock, the inventory or the wallet change (from anywhere). Built at runtime from the merchant's skin, or from your
/// prefab (Skin ▸ Window Prefab) with the fields below wired - every field is optional.</para>
/// </summary>
[DisallowMultipleComponent]
public class MerchantWindow : NPCWindow
{
    public enum SortMode
    {
        Default,
        Name,
        PriceLowToHigh,
        PriceHighToLow,
        Category,
    }

    [Header("Header")]
    [SerializeField] private Image portrait;
    [SerializeField] private GameObject portraitFrame;
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI greetingText;
    [SerializeField] private Button closeButton;

    [Header("Wallet")]
    [SerializeField] private TextMeshProUGUI walletText;
    [SerializeField] private Image walletIcon;

    [Header("Buy / Sell")]
    [SerializeField] private MerchantTabButton buyTab;
    [SerializeField] private MerchantTabButton sellTab;

    [Header("Categories")]
    [Tooltip("Parent of the category tabs (generated).")]
    [SerializeField] private RectTransform categoryTabs;
    [Tooltip("Parent of the subcategory tabs (generated). Hidden when the selected tab has none.")]
    [SerializeField] private RectTransform subcategoryTabs;
    [Tooltip("Hidden together with the subcategory tabs (their row). Empty = the tabs' parent itself.")]
    [SerializeField] private GameObject subcategoryRow;

    [Header("Search, Sort, Filters")]
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private Button sortButton;
    [SerializeField] private TextMeshProUGUI sortLabel;
    [SerializeField] private MerchantTabButton affordableFilter;
    [SerializeField] private MerchantTabButton inStockFilter;

    [Header("Items")]
    [SerializeField] private ScrollRect itemsScroll;
    [Tooltip("Parent of the items (anchored to the top of the scroll view; its height is set by the window).")]
    [SerializeField] private RectTransform itemsContent;
    [SerializeField] private TextMeshProUGUI emptyText;

    [Header("Details, Feedback, Confirmation")]
    [SerializeField] private MerchantDetailsPanel details;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private MerchantConfirmDialog confirmDialog;

    [Header("Templates (optional, used before generated ones)")]
    [SerializeField] private MerchantItemEntryUI slotEntryTemplate;
    [SerializeField] private MerchantItemEntryUI gridEntryTemplate;
    [SerializeField] private MerchantTabButton tabTemplate;

    private const float Padding = 8f;
    private const float SectionLabelHeight = 22f;
    private const float SectionGap = 14f;
    private const int MaxQuantity = 999;

    // Current
    private Merchant merchant;
    private MerchantUISkin skin;
    private MerchantTransactionType mode = MerchantTransactionType.Buy;
    private ItemCategory topFilter;
    private ItemCategory subFilter;
    private string search = "";
    private SortMode sort = SortMode.Default;
    private bool affordableOnly;
    private bool inStockOnly;
    private object selectedKey;
    private int quantity = 1;
    private bool pending;
    private int requestToken;
    private bool dirty;
    private bool lastUseGrid;
    private float lastViewportWidth = -1f;
    private float statusHideAt;
    private bool wired;
    private InventoryManager subscribedInventory;
    private CurrencyWallet subscribedWallet;
    private MerchantInventory subscribedStock;
    private AudioSource audioSource;

    // Entries and pools
    private readonly List<MerchantDisplayEntry> entries = new List<MerchantDisplayEntry>();
    private readonly List<MerchantDisplayEntry> entryPool = new List<MerchantDisplayEntry>();
    private readonly List<MerchantDisplayEntry> visible = new List<MerchantDisplayEntry>();
    private readonly List<MerchantDisplayEntry> hotbarVisible = new List<MerchantDisplayEntry>();
    private readonly List<MerchantItemEntryUI> slotViews = new List<MerchantItemEntryUI>();
    private readonly List<MerchantItemEntryUI> gridViews = new List<MerchantItemEntryUI>();
    private readonly List<Image> cellViews = new List<Image>();
    private readonly List<TextMeshProUGUI> labelViews = new List<TextMeshProUGUI>();
    private int slotViewsUsed, gridViewsUsed, cellViewsUsed, labelViewsUsed;
    private readonly List<MerchantTabButton> topTabs = new List<MerchantTabButton>();
    private readonly List<MerchantTabButton> subTabs = new List<MerchantTabButton>();
    private readonly List<ItemCategory> topList = new List<ItemCategory>();
    private readonly List<ItemCategory> subList = new List<ItemCategory>();
    private readonly List<ItemCategory> shownTopList = new List<ItemCategory>();
    private readonly List<ItemCategory> shownSubList = new List<ItemCategory>();
    private readonly Dictionary<ItemCategory, int> counts = new Dictionary<ItemCategory, int>();
    private readonly List<Vector2Int> packSizes = new List<Vector2Int>();
    private readonly List<bool> packRotatable = new List<bool>();
    private readonly List<GridRect> packRects = new List<GridRect>();
    private readonly List<bool> packRotated = new List<bool>();

    public Merchant Merchant => merchant;
    public MerchantTransactionType Mode => mode;
    public override string PanelName => merchant != null ? $"Shop: {merchant.ShopName}" : "Shop";
    private GameObject Player => Session?.Player;
    private InventoryManager PlayerInventory => Session?.Inventory;
    private CurrencyWallet Wallet => Session?.Wallet;
    private CurrencyDefinition Currency => merchant != null ? merchant.CurrencyFor(Wallet) : CurrencyDefinition.Default;

    // ------------------------------------------------------------------ opening and closing
    /// <summary>Opens the shop of <paramref name="merchant"/> for <paramref name="session"/> (on the player's canvas).</summary>
    public static MerchantWindow Open(Merchant merchant, NPCInteractionSession session)
    {
        MerchantUISkin skin = merchant.Skin != null ? merchant.Skin : MerchantUISkin.Default;
        MerchantWindow w = NPCWindowCache.Get(session.Canvas, skin.windowPrefab, skin, parent => MerchantUIBuilder.BuildWindow(parent, skin));
        w.Show(merchant, session, skin);
        return w;
    }

    /// <summary>Closes the shop window shown for <paramref name="session"/>.</summary>
    public static void CloseFor(Merchant merchant, NPCInteractionSession session)
    {
        if (merchant == null || session == null)
            return;
        MerchantUISkin skin = merchant.Skin != null ? merchant.Skin : MerchantUISkin.Default;
        MerchantWindow w = NPCWindowCache.TryGet(session.Canvas, skin.windowPrefab, skin);
        if (w != null && ReferenceEquals(w.Session, session))
        {
            w.PlaySound(skin.closeSound);
            w.Unbind();
        }
    }

    private void Show(Merchant shop, NPCInteractionSession session, MerchantUISkin look)
    {
        merchant = shop;
        skin = look != null ? look : MerchantUISkin.Default;
        Bind(session);
        Wire();
        Subscribe();
        bool hasGoods = merchant.Inventory.Slots.Count > 0;
        mode = !hasGoods && merchant.BuysItems ? MerchantTransactionType.Sell : MerchantTransactionType.Buy;
        ResetFilters();
        FillHeader();
        FitToCanvas();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform); // real sizes before the items are placed
        SetStatus("", false);
        if (confirmDialog != null) confirmDialog.Close();
        PlaySound(skin.openSound);
        Refresh();
    }

    protected override void OnUnbound()
    {
        Unsubscribe();
        if (confirmDialog != null) confirmDialog.Close();
        selectedKey = null;
        pending = false;
        requestToken++; // a late answer for this session is ignored
    }

    /// <summary>Close button / Escape: closes a confirmation first, else the shop (back to the NPC's menu or the end).</summary>
    public override void RequestClose()
    {
        if (confirmDialog != null && confirmDialog.IsOpen)
        {
            confirmDialog.Close();
            return;
        }
        NPCInteractionSession s = Session;
        if (s != null && merchant != null)
            s.BehaviourFinished(merchant);
        if (Session == s && s != null)
            base.RequestClose();
    }

    private void Wire()
    {
        if (wired)
            return;
        wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
        if (buyTab != null) buyTab.Setup("Buy", null, () => SetMode(MerchantTransactionType.Buy));
        if (sellTab != null) sellTab.Setup("Sell", null, () => SetMode(MerchantTransactionType.Sell));
        if (searchField != null) searchField.onValueChanged.AddListener(t => { search = t ?? ""; MarkDirty(); });
        if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
        if (affordableFilter != null) affordableFilter.Setup("Affordable", null, () => { affordableOnly = !affordableOnly; Click(); MarkDirty(); });
        if (inStockFilter != null) inStockFilter.Setup("In stock", null, () => { inStockOnly = !inStockOnly; Click(); MarkDirty(); });
        if (details != null) details.Wire(this);
        if (slotEntryTemplate != null) slotEntryTemplate.gameObject.SetActive(false);
        if (gridEntryTemplate != null) gridEntryTemplate.gameObject.SetActive(false);
        if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);
    }

    private void Subscribe()
    {
        Unsubscribe();
        subscribedStock = merchant.Inventory;
        subscribedStock.Changed += MarkDirty;
        subscribedInventory = PlayerInventory;
        if (subscribedInventory != null)
            subscribedInventory.InventoryChanged += MarkDirty;
        subscribedWallet = Wallet;
        if (subscribedWallet != null)
            subscribedWallet.BalanceChanged += OnBalanceChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedStock != null) subscribedStock.Changed -= MarkDirty;
        if (subscribedInventory != null) subscribedInventory.InventoryChanged -= MarkDirty;
        if (subscribedWallet != null) subscribedWallet.BalanceChanged -= OnBalanceChanged;
        subscribedStock = null;
        subscribedInventory = null;
        subscribedWallet = null;
    }

    private void OnBalanceChanged(CurrencyDefinition c, int before, int after) => MarkDirty();

    public void MarkDirty() => dirty = true;

    // ------------------------------------------------------------------ per frame
    protected override void Update()
    {
        base.Update();
        if (Session == null)
            return;
        if (statusText != null && statusHideAt > 0f && Time.unscaledTime > statusHideAt)
        {
            statusHideAt = 0f;
            statusText.text = "";
        }
    }

    private void LateUpdate()
    {
        if (Session == null || merchant == null)
            return;
        if (!merchant.isActiveAndEnabled)
        {
            RequestClose();
            return;
        }
        float width = ViewportWidth();
        if (Mathf.Abs(width - lastViewportWidth) > 0.5f || UseGridLayout() != lastUseGrid)
            dirty = true;
        if (dirty)
            Refresh();
    }

    // ------------------------------------------------------------------ state changes
    private void SetMode(MerchantTransactionType newMode)
    {
        if (newMode == MerchantTransactionType.Sell && !merchant.BuysItems)
            return;
        Click();
        if (mode == newMode)
            return;
        mode = newMode;
        ResetFilters();
        Refresh();
    }

    private void ResetFilters()
    {
        topFilter = null;
        subFilter = null;
        selectedKey = null;
        quantity = 1;
        affordableOnly = inStockOnly = false;
        sort = SortMode.Default;
        search = "";
        if (searchField != null) searchField.SetTextWithoutNotify("");
        if (itemsScroll != null) itemsScroll.verticalNormalizedPosition = 1f;
    }

    private void CycleSort()
    {
        Click();
        sort = (SortMode)(((int)sort + 1) % Enum.GetValues(typeof(SortMode)).Length);
        MarkDirty();
    }

    private void SelectTop(ItemCategory top)
    {
        Click();
        topFilter = top;
        subFilter = null;
        if (itemsScroll != null) itemsScroll.verticalNormalizedPosition = 1f;
        MarkDirty();
    }

    private void SelectSub(ItemCategory sub)
    {
        Click();
        subFilter = sub;
        MarkDirty();
    }

    /// <summary>An item was clicked: select it; double click or right click trades one.</summary>
    public void OnEntryClicked(MerchantDisplayEntry entry, PointerEventData.InputButton button, int clickCount)
    {
        if (entry == null || entry.Item == null || pending)
            return;
        bool quick = button == PointerEventData.InputButton.Right || (button == PointerEventData.InputButton.Left && clickCount >= 2);
        if (!ReferenceEquals(selectedKey, entry.Key))
        {
            selectedKey = entry.Key;
            quantity = 1;
        }
        Click();
        if (quick)
        {
            quantity = 1;
            Trade(entry, 1, false);
        }
        UpdateSelectionVisuals();
        RefreshDetails();
    }

    public void StepQuantity(int delta) => SetQuantity(quantity + delta);

    public void SetQuantity(int value)
    {
        MerchantDisplayEntry e = SelectedEntry();
        quantity = e != null ? Mathf.Clamp(value, 1, MaxFor(e)) : 1;
        RefreshDetails();
    }

    /// <summary>As many as possible: what is in stock and affordable and fits in the bag (buying), the whole stack (selling).</summary>
    public void SetMaxQuantity()
    {
        MerchantDisplayEntry e = SelectedEntry();
        if (e == null)
            return;
        int max = MaxFor(e);
        if (e.Mode == MerchantTransactionType.Buy && PlayerInventory != null && max > 1 && !PlayerInventory.HasEnoughSpace(e.Item, max))
        {
            // The most that fits (space only shrinks as the quantity grows).
            int lo = 1, hi = max;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (PlayerInventory.HasEnoughSpace(e.Item, mid)) lo = mid;
                else hi = mid - 1;
            }
            max = lo;
        }
        quantity = max;
        RefreshDetails();
    }

    /// <summary>The Buy / Sell button.</summary>
    public void ConfirmSelected()
    {
        MerchantDisplayEntry e = SelectedEntry();
        if (e != null)
            Trade(e, quantity, false);
    }

    // ------------------------------------------------------------------ transactions
    private void Trade(MerchantDisplayEntry e, int count, bool confirmed)
    {
        if (pending || e == null || merchant == null || Player == null)
            return;
        MerchantTransactionResult quote = Quote(e, count);
        if (!quote.Succeeded)
        {
            SetStatus(quote.Message, false);
            PlaySound(skin.errorSound);
            RefreshDetails();
            return;
        }
        if (!confirmed && confirmDialog != null && merchant.NeedsConfirmation(e.Mode, quote.TotalPrice))
        {
            string what = count > 1 ? $"{count} × {e.Item.Name}" : e.Item.Name;
            string verb = e.Mode == MerchantTransactionType.Buy ? "Buy" : "Sell";
            object key = e.Key;
            MerchantTransactionType kind = e.Mode;
            // Entries are reused by every refresh: find the item again when the player confirms.
            confirmDialog.Show($"{verb} {what} for {Currency.Format(quote.TotalPrice)}?", verb, () =>
            {
                MerchantDisplayEntry fresh = FindEntry(key);
                if (fresh != null && fresh.Mode == kind)
                    Trade(fresh, count, true);
            });
            return;
        }
        MerchantTransactionRequest request = e.Mode == MerchantTransactionType.Buy
            ? MerchantTransactionRequest.Buy(merchant, Player, e.Slot, count, quote.TotalPrice)
            : MerchantTransactionRequest.Sell(merchant, Player, e.Stack, count, quote.TotalPrice);
        pending = true;
        int token = ++requestToken;
        RefreshDetails();
        merchant.Submit(request, result => OnResult(token, result));
    }

    private MerchantTransactionResult Quote(MerchantDisplayEntry e, int count)
    {
        return e.Mode == MerchantTransactionType.Buy ? merchant.QuoteBuy(Player, e.Slot, count) : merchant.QuoteSell(Player, e.Stack, count);
    }

    private void OnResult(int token, MerchantTransactionResult result)
    {
        if (this == null || token != requestToken)
            return; // the window was closed or reused meanwhile
        pending = false;
        if (Session == null)
            return;
        SetStatus(result.Message, result.Succeeded);
        if (result.Succeeded)
        {
            PlaySound(result.Request.Type == MerchantTransactionType.Buy ? skin.buySound : skin.sellSound);
            quantity = 1;
        }
        else
        {
            PlaySound(skin.errorSound);
        }
        Refresh();
    }

    // ------------------------------------------------------------------ refresh
    /// <summary>Rebuilds everything from the stock, the inventory and the wallet.</summary>
    public void Refresh()
    {
        dirty = false;
        if (merchant == null || Session == null)
            return;
        lastUseGrid = UseGridLayout();
        lastViewportWidth = ViewportWidth();
        BuildEntries();
        UpdateTabs();
        FilterAndSort();
        Layout();
        UpdateSelectionVisuals();
        RefreshDetails();
        UpdateWallet();
        UpdateControls();
    }

    private void BuildEntries()
    {
        foreach (MerchantDisplayEntry e in entries)
        {
            e.Clear();
            entryPool.Add(e);
        }
        entries.Clear();
        if (mode == MerchantTransactionType.Buy)
            BuildBuyEntries();
        else
            BuildSellEntries();
    }

    private MerchantDisplayEntry Rent()
    {
        MerchantDisplayEntry e;
        if (entryPool.Count > 0)
        {
            e = entryPool[entryPool.Count - 1];
            entryPool.RemoveAt(entryPool.Count - 1);
        }
        else
        {
            e = new MerchantDisplayEntry();
        }
        entries.Add(e);
        return e;
    }

    private void BuildBuyEntries()
    {
        CurrencyDefinition currency = Currency;
        int balance = Wallet != null ? Wallet.GetBalance(currency) : 0;
        foreach (MerchantStockSlot slot in merchant.Inventory.Slots)
        {
            if (slot == null || slot.Item == null)
                continue;
            ItemCategoryMatch cat = merchant.CategoryOf(slot.Item, slot.Source);
            if (!merchant.IsCategoryShown(cat.Top))
                continue;
            MerchantDisplayEntry e = Rent();
            e.Mode = MerchantTransactionType.Buy;
            e.Item = slot.Item;
            e.Slot = slot;
            e.Quantity = slot.Quantity;
            e.Unlimited = slot.Unlimited;
            e.UnitPrice = merchant.GetBuyPrice(slot, Player);
            e.Affordable = balance >= e.UnitPrice;
            e.Available = slot.InStock;
            e.Reason = !slot.InStock ? "Sold out" : !e.Affordable ? $"Not enough {currency.DisplayName}" : "";
            e.Category = cat;
        }
    }

    private void BuildSellEntries()
    {
        InventoryManager inv = PlayerInventory;
        if (inv == null)
            return;
        bool mirror = skin.mirrorInventoryWhenSelling;
        GridInventory grid = inv.Grid;
        if (mirror && UseGridLayout() && grid != null)
        {
            foreach (KeyValuePair<InventoryItem, GridRect> kv in grid.Model.Placements)
            {
                MerchantDisplayEntry e = AddStack(kv.Key, false);
                if (e == null) continue;
                e.HasPlacement = true;
                e.Placement = kv.Value;
                e.Rotated = kv.Key.gridRotated;
            }
        }
        else
        {
            AddSlots(inv.Slots, false, mirror);
        }
        AddSlots(inv.HotbarSlots, true, mirror);
    }

    private void AddSlots(GameObject[] slots, bool hotbar, bool includeEmpty)
    {
        if (slots == null)
            return;
        foreach (GameObject go in slots)
        {
            InventorySlot s = go != null ? go.GetComponent<InventorySlot>() : null;
            if (s == null)
                continue;
            InventoryItem stack = s.heldItem != null ? s.heldItem.GetComponent<InventoryItem>() : null;
            if (stack != null && stack.itemScriptableObject != null)
            {
                AddStack(stack, hotbar);
            }
            else if (includeEmpty)
            {
                MerchantDisplayEntry e = Rent();
                e.Mode = MerchantTransactionType.Sell;
                e.IsEmptySlot = true;
                e.FromHotbar = hotbar;
            }
        }
    }

    private MerchantDisplayEntry AddStack(InventoryItem stack, bool hotbar)
    {
        if (stack == null || stack.itemScriptableObject == null)
            return null;
        bool willBuy = merchant.WillBuy(stack.itemScriptableObject, out string reason);
        int price = merchant.GetSellPrice(stack, Player);
        if (willBuy && price <= 0 && merchant.RefusesWorthlessItems)
        {
            willBuy = false;
            reason = "Worthless here";
        }
        if (!willBuy && !skin.mirrorInventoryWhenSelling)
            return null; // the list shows only what can be sold
        MerchantDisplayEntry e = Rent();
        e.Mode = MerchantTransactionType.Sell;
        e.Item = stack.itemScriptableObject;
        e.Stack = stack;
        e.Quantity = stack.stackCurrent;
        e.UnitPrice = price;
        e.Affordable = true;
        e.Available = willBuy;
        e.Reason = reason;
        e.Category = merchant.CategoryOf(stack.itemScriptableObject);
        e.FromHotbar = hotbar;
        return e;
    }

    // ------------------------------------------------------------------ tabs
    private void UpdateTabs()
    {
        // The categories that have something (buying: every line; selling: what the merchant buys).
        counts.Clear();
        topList.Clear();
        int total = 0;
        foreach (MerchantDisplayEntry e in entries)
        {
            if (e.Item == null || (mode == MerchantTransactionType.Sell && !e.Available))
                continue;
            ItemCategory top = e.Category.Top;
            if (top == null)
                continue;
            total++;
            counts[top] = counts.TryGetValue(top, out int n) ? n + 1 : 1;
            if (!topList.Contains(top))
                topList.Add(top);
            ItemCategory sub = e.Category.Sub;
            if (sub != null)
                counts[sub] = counts.TryGetValue(sub, out int m) ? m + 1 : 1;
        }
        if (merchant.ShowEmptyCategories)
            foreach (ItemCategory c in merchant.ShownCategories)
                if (c != null && !topList.Contains(c.Root))
                    topList.Add(c.Root);
        SortTops(topList);
        bool allTab = merchant.ShowAllTab || topList.Count == 0;
        if (topFilter != null && !topList.Contains(topFilter))
            topFilter = null;
        if (topFilter == null && !allTab && topList.Count > 0)
            topFilter = topList[0];

        // Top tabs (rebuilt only when the set changes).
        var wanted = new List<ItemCategory>();
        if (allTab) wanted.Add(null);
        wanted.AddRange(topList);
        if (!SameList(wanted, shownTopList))
        {
            shownTopList.Clear();
            shownTopList.AddRange(wanted);
            RebuildTabs(categoryTabs, topTabs, shownTopList, true);
        }
        for (int i = 0; i < topTabs.Count && i < shownTopList.Count; i++)
        {
            ItemCategory c = shownTopList[i];
            int n = c == null ? total : counts.TryGetValue(c, out int k) ? k : 0;
            topTabs[i].SetText($"{(c == null ? "All" : c.DisplayName)}  <size=75%><color=#9aa>{n}</color></size>");
            topTabs[i].SetSelected(ReferenceEquals(c, topFilter), skin);
        }

        // Subcategory tabs of the selected tab.
        subList.Clear();
        if (merchant.ShowSubcategories && topFilter != null)
        {
            foreach (MerchantDisplayEntry e in entries)
            {
                if (e.Item == null || e.Category.Top != topFilter || (mode == MerchantTransactionType.Sell && !e.Available))
                    continue;
                ItemCategory sub = e.Category.Sub;
                if (sub != null && !subList.Contains(sub))
                    subList.Add(sub);
            }
            subList.Sort(ItemCategoryDatabase.Compare);
        }
        if (subFilter != null && !subList.Contains(subFilter))
            subFilter = null;
        var wantedSubs = new List<ItemCategory>();
        if (subList.Count > 0)
        {
            wantedSubs.Add(null);
            wantedSubs.AddRange(subList);
        }
        if (!SameList(wantedSubs, shownSubList))
        {
            shownSubList.Clear();
            shownSubList.AddRange(wantedSubs);
            RebuildTabs(subcategoryTabs, subTabs, shownSubList, false);
        }
        for (int i = 0; i < subTabs.Count && i < shownSubList.Count; i++)
        {
            ItemCategory c = shownSubList[i];
            int n = c == null ? (counts.TryGetValue(topFilter, out int t) ? t : 0) : counts.TryGetValue(c, out int k) ? k : 0;
            subTabs[i].SetText($"{(c == null ? $"All {topFilter?.DisplayName}" : c.DisplayName)}  <size=75%><color=#9aa>{n}</color></size>");
            subTabs[i].SetSelected(ReferenceEquals(c, subFilter), skin);
        }
        GameObject row = subcategoryRow != null ? subcategoryRow : subcategoryTabs != null ? subcategoryTabs.gameObject : null;
        if (row != null)
            row.SetActive(shownSubList.Count > 0);
    }

    private void SortTops(List<ItemCategory> list)
    {
        IReadOnlyList<ItemCategory> order = merchant.ShownCategories;
        if (order != null && order.Count > 0)
        {
            list.Sort((a, b) =>
            {
                int ia = IndexOfRoot(order, a), ib = IndexOfRoot(order, b);
                return ia != ib ? ia.CompareTo(ib) : ItemCategoryDatabase.Compare(a, b);
            });
            return;
        }
        list.Sort(ItemCategoryDatabase.Compare);
    }

    private static int IndexOfRoot(IReadOnlyList<ItemCategory> order, ItemCategory c)
    {
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].Root == c)
                return i;
        return int.MaxValue;
    }

    private static bool SameList(List<ItemCategory> a, List<ItemCategory> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    private void RebuildTabs(RectTransform parent, List<MerchantTabButton> tabs, List<ItemCategory> cats, bool top)
    {
        if (parent == null)
            return;
        while (tabs.Count < cats.Count)
            tabs.Add(CreateTab(parent));
        for (int i = 0; i < tabs.Count; i++)
        {
            bool used = i < cats.Count;
            tabs[i].gameObject.SetActive(used);
            if (!used) continue;
            ItemCategory c = cats[i];
            tabs[i].Tag = c;
            if (top) tabs[i].Setup(c == null ? "All" : c.DisplayName, c != null ? c.Icon : null, () => SelectTop(c));
            else tabs[i].Setup(c == null ? "All" : c.DisplayName, c != null ? c.Icon : null, () => SelectSub(c));
        }
        LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    private MerchantTabButton CreateTab(RectTransform parent)
    {
        MerchantTabButton t;
        if (skin.tabPrefab != null) t = Instantiate(skin.tabPrefab, parent, false);
        else if (tabTemplate != null) t = Instantiate(tabTemplate, parent, false);
        else t = MerchantUIBuilder.CreateTab(parent, skin, "Tab", false);
        t.gameObject.SetActive(true);
        return t;
    }

    // ------------------------------------------------------------------ filter and sort
    private bool Matches(MerchantDisplayEntry e)
    {
        if (e.IsEmptySlot || e.Item == null)
            return true;
        if (topFilter != null && e.Category.Top != topFilter)
            return false;
        if (subFilter != null && !e.Category.IsIn(subFilter))
            return false;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            bool hit = e.Item.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       e.Category.Path.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hit) return false;
        }
        if (mode == MerchantTransactionType.Buy)
        {
            if (affordableOnly && !e.Affordable) return false;
            if (inStockOnly && (e.Slot == null || !e.Slot.InStock)) return false;
        }
        return true;
    }

    private bool IsMirror => mode == MerchantTransactionType.Sell && skin.mirrorInventoryWhenSelling;

    private void FilterAndSort()
    {
        visible.Clear();
        hotbarVisible.Clear();
        bool mirror = IsMirror;
        foreach (MerchantDisplayEntry e in entries)
        {
            bool match = Matches(e);
            if (mirror)
            {
                e.Dimmed = !match; // a mirror keeps every item where it is
                (e.FromHotbar ? hotbarVisible : visible).Add(e);
            }
            else if (match)
            {
                (mode == MerchantTransactionType.Sell && e.FromHotbar ? hotbarVisible : visible).Add(e);
            }
        }
        if (mirror)
            return;
        Comparison<MerchantDisplayEntry> cmp = Comparer();
        if (cmp != null)
        {
            StableSort(visible, cmp);
            StableSort(hotbarVisible, cmp);
        }
    }

    private Comparison<MerchantDisplayEntry> Comparer()
    {
        switch (sort)
        {
            case SortMode.Name: return (a, b) => string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
            case SortMode.PriceLowToHigh: return (a, b) => a.UnitPrice.CompareTo(b.UnitPrice);
            case SortMode.PriceHighToLow: return (a, b) => b.UnitPrice.CompareTo(a.UnitPrice);
            case SortMode.Category:
                return (a, b) =>
                {
                    int c = ItemCategoryDatabase.Compare(a.Category.Top, b.Category.Top);
                    if (c == 0) c = ItemCategoryDatabase.Compare(a.Category.Sub, b.Category.Sub);
                    return c != 0 ? c : string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
                };
            default:
                if (mode == MerchantTransactionType.Buy)
                    return (a, b) => (a.Slot != null ? a.Slot.Order : 0).CompareTo(b.Slot != null ? b.Slot.Order : 0);
                return null; // selling: the inventory's order
        }
    }

    private static void StableSort(List<MerchantDisplayEntry> list, Comparison<MerchantDisplayEntry> cmp)
    {
        // Insertion sort: stable and allocation-free (shop lists are short).
        for (int i = 1; i < list.Count; i++)
        {
            MerchantDisplayEntry x = list[i];
            int j = i - 1;
            while (j >= 0 && cmp(list[j], x) > 0)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = x;
        }
    }

    // ------------------------------------------------------------------ layout
    /// <summary>Grid layout when the merchant says so, or when the player's inventory is a grid (Follow Inventory).</summary>
    private bool UseGridLayout()
    {
        if (merchant == null)
            return false;
        switch (merchant.Layout)
        {
            case MerchantLayoutMode.Grid: return true;
            case MerchantLayoutMode.Slots: return false;
            default:
                InventoryManager inv = PlayerInventory;
                return inv != null && inv.UseGridInventory;
        }
    }

    private GridInventorySettings GridSettings => PlayerInventory != null ? PlayerInventory.GridSettings : null;

    private float ViewportWidth()
    {
        RectTransform vp = itemsScroll != null && itemsScroll.viewport != null ? itemsScroll.viewport : itemsContent != null ? itemsContent.parent as RectTransform : null;
        return vp != null ? vp.rect.width : 600f;
    }

    private void Layout()
    {
        if (itemsContent == null)
            return;
        slotViewsUsed = gridViewsUsed = cellViewsUsed = labelViewsUsed = 0;
        float width = Mathf.Max(64f, ViewportWidth());
        bool grid = UseGridLayout();
        float y = Padding;
        bool sell = mode == MerchantTransactionType.Sell;
        bool hotbarSection = sell && hotbarVisible.Count > 0;

        if (grid)
        {
            GridInventorySettings gs = GridSettings;
            InventoryManager inv = PlayerInventory;
            bool mirrorGrid = IsMirror && inv != null && inv.Grid != null;
            int columns = mirrorGrid ? inv.Grid.Columns : merchant.GridColumns > 0 ? merchant.GridColumns : gs != null ? gs.columns : 10;
            float cell = CellSize(width, columns);
            if (sell && hotbarSection)
                y = SectionLabel("Bag", y, width);
            if (mirrorGrid)
                y = GridSectionMirror(visible, inv.Grid.Columns, inv.Grid.Rows, cell, y, width, gs);
            else
                y = GridSectionPacked(visible, columns, cell, y, width, gs);
            if (hotbarSection)
            {
                y = SectionLabel("Hotbar", y + SectionGap - skin.spacing, width);
                y = SlotSection(hotbarVisible, cell, y, width);
            }
        }
        else
        {
            if (sell && hotbarSection)
                y = SectionLabel("Bag", y, width);
            y = SlotSection(visible, skin.slotSize, y, width);
            if (hotbarSection)
            {
                y = SectionLabel("Hotbar", y + SectionGap - skin.spacing, width);
                y = SlotSection(hotbarVisible, skin.slotSize, y, width);
            }
        }

        HideUnused(slotViews, slotViewsUsed);
        HideUnused(gridViews, gridViewsUsed);
        HideUnused(cellViews, cellViewsUsed);
        HideUnused(labelViews, labelViewsUsed);
        itemsContent.anchorMin = new Vector2(0f, 1f);
        itemsContent.anchorMax = new Vector2(1f, 1f);
        itemsContent.pivot = new Vector2(0.5f, 1f);
        itemsContent.sizeDelta = new Vector2(0f, y + Padding);

        if (emptyText != null)
        {
            bool nothing = CountItems(visible) + CountItems(hotbarVisible) == 0;
            emptyText.gameObject.SetActive(nothing);
            if (nothing)
                emptyText.text = EmptyMessage();
        }
    }

    private static int CountItems(List<MerchantDisplayEntry> list)
    {
        int n = 0;
        foreach (MerchantDisplayEntry e in list)
            if (e.Item != null && !e.Dimmed && (e.Mode == MerchantTransactionType.Buy || e.Available)) n++;
        return n;
    }

    private string EmptyMessage()
    {
        bool filtered = topFilter != null || subFilter != null || !string.IsNullOrWhiteSpace(search) || affordableOnly || inStockOnly;
        if (mode == MerchantTransactionType.Buy)
            return filtered ? "No goods match." : $"{merchant.ShopName} has nothing for sale right now.";
        if (PlayerInventory == null)
            return "You have no inventory.";
        return filtered ? "None of your items match." : $"You have nothing {merchant.ShopName} wants to buy.";
    }

    /// <summary>The grid cell size: the skin's, else the player's grid cell, made smaller when the columns do not fit.</summary>
    private float CellSize(float width, int columns)
    {
        GridInventorySettings gs = GridSettings;
        float preferred = skin.gridCellSize > 0f ? skin.gridCellSize : gs != null && gs.cellSize > 0f ? gs.cellSize : 56f;
        float fit = (width - Padding * 2f - (columns - 1) * skin.spacing) / Mathf.Max(1, columns);
        return Mathf.Max(20f, Mathf.Floor(Mathf.Min(preferred, fit)));
    }

    private float SectionLabel(string text, float y, float width)
    {
        TextMeshProUGUI label = RentLabel();
        label.text = text;
        RectTransform rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Padding, -y);
        rt.sizeDelta = new Vector2(width - Padding * 2f, SectionLabelHeight);
        return y + SectionLabelHeight;
    }

    private float SlotSection(List<MerchantDisplayEntry> list, float size, float y, float width)
    {
        if (list.Count == 0)
            return y;
        float step = size + skin.spacing;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width - Padding * 2f + skin.spacing) / step));
        CurrencyDefinition currency = Currency;
        for (int i = 0; i < list.Count; i++)
        {
            MerchantItemEntryUI view = RentSlotView();
            RectTransform rt = view.Rect;
            Place(rt, new Vector2(Padding + (i % columns) * step, -(y + (i / columns) * step)), new Vector2(size, size));
            view.Bind(this, list[i], IsSelected(list[i]), false, skin, currency, GridSettings);
        }
        int rows = (list.Count + columns - 1) / columns;
        return y + rows * step - skin.spacing;
    }

    private readonly List<MerchantDisplayEntry> packed = new List<MerchantDisplayEntry>();

    private float GridSectionPacked(List<MerchantDisplayEntry> list, int columns, float cell, float y, float width, GridInventorySettings gs)
    {
        packed.Clear();
        packSizes.Clear();
        packRotatable.Clear();
        foreach (MerchantDisplayEntry e in list)
        {
            if (e.Item == null)
                continue; // empty slots have no place in a packed grid
            packed.Add(e);
            packSizes.Add(e.Item.GridSize);
            packRotatable.Add(e.Item.CanRotateInGrid);
        }
        bool autoRotate = gs == null || gs.autoRotateToFit;
        int rows = packed.Count > 0 ? MerchantGridPacker.Pack(packSizes, packRotatable, columns, autoRotate, packRects, packRotated) : 0;
        for (int i = 0; i < packed.Count; i++)
        {
            packed[i].Placement = packRects[i];
            packed[i].Rotated = packRotated[i];
        }
        return DrawGrid(packed, columns, rows, cell, y, width, gs);
    }

    private float GridSectionMirror(List<MerchantDisplayEntry> list, int columns, int rows, float cell, float y, float width, GridInventorySettings gs)
    {
        return DrawGrid(list, columns, rows, cell, y, width, gs);
    }

    /// <summary>Draws the cells of a columns × rows grid and every entry over its Placement.</summary>
    private float DrawGrid(List<MerchantDisplayEntry> list, int columns, int rows, float cell, float y, float width, GridInventorySettings gs)
    {
        if (rows <= 0)
            return y;
        float step = cell + skin.spacing;
        float gridWidth = columns * step - skin.spacing;
        float x0 = Padding + Mathf.Max(0f, (width - Padding * 2f - gridWidth) * 0.5f);
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < columns; gx++)
            {
                Image c = RentCell();
                Place(c.rectTransform, new Vector2(x0 + gx * step, -(y + gy * step)), new Vector2(cell, cell));
            }
        CurrencyDefinition currency = Currency;
        foreach (MerchantDisplayEntry e in list)
        {
            if (e.Item == null)
                continue;
            GridRect r = e.Placement;
            MerchantItemEntryUI view = RentGridView();
            Vector2 size = new Vector2(r.W * cell + (r.W - 1) * skin.spacing, r.H * cell + (r.H - 1) * skin.spacing);
            Place(view.Rect, new Vector2(x0 + r.X * step, -(y + r.Y * step)), size);
            view.Bind(this, e, IsSelected(e), true, skin, currency, gs);
        }
        return y + rows * step - skin.spacing;
    }

    private static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private MerchantItemEntryUI RentSlotView()
    {
        if (slotViewsUsed < slotViews.Count)
        {
            MerchantItemEntryUI v = slotViews[slotViewsUsed++];
            if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
            v.transform.SetAsLastSibling();
            return v;
        }
        MerchantItemEntryUI made = skin.slotEntryPrefab != null ? Instantiate(skin.slotEntryPrefab, itemsContent, false)
            : slotEntryTemplate != null ? Instantiate(slotEntryTemplate, itemsContent, false)
            : MerchantUIBuilder.CreateEntry(itemsContent, skin, false);
        made.gameObject.SetActive(true);
        slotViews.Add(made);
        slotViewsUsed++;
        return made;
    }

    private MerchantItemEntryUI RentGridView()
    {
        if (gridViewsUsed < gridViews.Count)
        {
            MerchantItemEntryUI v = gridViews[gridViewsUsed++];
            if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
            v.transform.SetAsLastSibling();
            return v;
        }
        MerchantItemEntryUI made = skin.gridEntryPrefab != null ? Instantiate(skin.gridEntryPrefab, itemsContent, false)
            : gridEntryTemplate != null ? Instantiate(gridEntryTemplate, itemsContent, false)
            : MerchantUIBuilder.CreateEntry(itemsContent, skin, true);
        made.gameObject.SetActive(true);
        gridViews.Add(made);
        gridViewsUsed++;
        return made;
    }

    private Image RentCell()
    {
        Image c;
        if (cellViewsUsed < cellViews.Count)
        {
            c = cellViews[cellViewsUsed++];
            if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
        }
        else
        {
            c = InventoryUIFactory.Panel("Cell", itemsContent, skin.slotColor);
            c.raycastTarget = false;
            cellViews.Add(c);
            cellViewsUsed++;
        }
        c.color = skin.slotColor;
        if (skin.slotBackground != null)
        {
            c.sprite = skin.slotBackground;
            c.type = skin.backgroundImageType;
        }
        c.transform.SetAsFirstSibling(); // cells under the items
        return c;
    }

    private TextMeshProUGUI RentLabel()
    {
        if (labelViewsUsed < labelViews.Count)
        {
            TextMeshProUGUI l = labelViews[labelViewsUsed++];
            if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
            return l;
        }
        TextMeshProUGUI made = MerchantUIBuilder.Text("Section", itemsContent, "", 14f, skin.mutedTextColor, skin, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        labelViews.Add(made);
        labelViewsUsed++;
        return made;
    }

    private static void HideUnused<T>(List<T> pool, int used) where T : Component
    {
        for (int i = used; i < pool.Count; i++)
            if (pool[i] != null && pool[i].gameObject.activeSelf)
                pool[i].gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ selection and details
    private bool IsSelected(MerchantDisplayEntry e) => e != null && e.Item != null && selectedKey != null && ReferenceEquals(e.Key, selectedKey);

    private MerchantDisplayEntry FindEntry(object key)
    {
        if (key == null || (key is UnityEngine.Object o && o == null))
            return null;
        foreach (MerchantDisplayEntry e in entries)
            if (e.Item != null && ReferenceEquals(e.Key, key))
                return e;
        return null;
    }

    private MerchantDisplayEntry SelectedEntry()
    {
        if (selectedKey == null)
            return null;
        if (selectedKey is UnityEngine.Object o && o == null)
        {
            selectedKey = null; // the stack was sold or used up
            return null;
        }
        foreach (MerchantDisplayEntry e in entries)
            if (e.Item != null && ReferenceEquals(e.Key, selectedKey))
                return e;
        return null;
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < slotViewsUsed; i++)
            slotViews[i].SetSelected(IsSelected(slotViews[i].Entry));
        for (int i = 0; i < gridViewsUsed; i++)
            gridViews[i].SetSelected(IsSelected(gridViews[i].Entry));
    }

    private int MaxFor(MerchantDisplayEntry e)
    {
        if (e == null || e.Item == null)
            return 1;
        if (e.Mode == MerchantTransactionType.Sell)
            return Mathf.Max(1, e.Quantity);
        int max = e.Unlimited ? MaxQuantity : Mathf.Min(MaxQuantity, e.Quantity);
        if (e.UnitPrice > 0 && Wallet != null)
            max = Mathf.Min(max, Wallet.GetBalance(Currency) / e.UnitPrice);
        return Mathf.Max(1, max);
    }

    /// <summary>Redraws the details of the selected item (quantity, quote, button).</summary>
    public void RefreshDetails()
    {
        if (details == null)
            return;
        MerchantDisplayEntry e = SelectedEntry();
        if (e == null)
        {
            details.ShowEmpty(mode == MerchantTransactionType.Buy ? "Select an item to see its details and buy it." : "Select one of your items to sell it.");
            return;
        }
        quantity = Mathf.Clamp(quantity, 1, MaxFor(e));
        MerchantTransactionResult quote = Quote(e, quantity);
        details.Show(e, quote, quantity, MaxFor(e), pending, Currency, skin);
    }

    // ------------------------------------------------------------------ header, wallet, controls
    private void FillHeader()
    {
        NPC npc = merchant.Npc;
        if (shopNameText != null) shopNameText.text = merchant.ShopName;
        if (greetingText != null)
        {
            string line = !string.IsNullOrWhiteSpace(merchant.ShopGreeting) ? merchant.ShopGreeting : npc != null ? npc.Title : "";
            greetingText.text = line ?? "";
            greetingText.gameObject.SetActive(!string.IsNullOrWhiteSpace(line));
        }
        if (portrait != null)
        {
            Sprite p = npc != null ? npc.Portrait : null;
            portrait.sprite = p;
            (portraitFrame != null ? portraitFrame : portrait.gameObject).SetActive(p != null);
        }
        if (sellTab != null) sellTab.gameObject.SetActive(merchant.BuysItems);
        if (buyTab != null) buyTab.gameObject.SetActive(true);
    }

    private void UpdateWallet()
    {
        CurrencyDefinition c = Currency;
        int balance = Wallet != null ? Wallet.GetBalance(c) : 0;
        if (walletText != null)
        {
            walletText.text = c.Format(balance);
            walletText.color = c.Color;
        }
        if (walletIcon != null)
        {
            Sprite icon = skin.currencyIcon != null ? skin.currencyIcon : c.Icon;
            walletIcon.sprite = icon;
            walletIcon.gameObject.SetActive(icon != null);
        }
    }

    private void UpdateControls()
    {
        if (buyTab != null) buyTab.SetSelected(mode == MerchantTransactionType.Buy, skin);
        if (sellTab != null) sellTab.SetSelected(mode == MerchantTransactionType.Sell, skin);
        bool buying = mode == MerchantTransactionType.Buy;
        if (affordableFilter != null)
        {
            affordableFilter.gameObject.SetActive(buying);
            affordableFilter.SetSelected(affordableOnly, skin);
        }
        if (inStockFilter != null)
        {
            inStockFilter.gameObject.SetActive(buying);
            inStockFilter.SetSelected(inStockOnly, skin);
        }
        if (sortButton != null)
        {
            sortButton.interactable = !IsMirror; // a mirror of the inventory keeps its order
            if (sortLabel != null)
                sortLabel.text = IsMirror ? "Sort: as in bag" : $"Sort: {SortName(sort)}";
        }
    }

    private static string SortName(SortMode s)
    {
        switch (s)
        {
            case SortMode.Name: return "Name";
            case SortMode.PriceLowToHigh: return "Price ▲";
            case SortMode.PriceHighToLow: return "Price ▼";
            case SortMode.Category: return "Category";
            default: return "Default";
        }
    }

    private void SetStatus(string text, bool success)
    {
        if (statusText == null)
        {
            if (!string.IsNullOrEmpty(text))
                PlayerInventory?.ShowMessage(text, success ? InventoryFeedback.Kind.Info : InventoryFeedback.Kind.Warning);
            return;
        }
        statusText.text = text ?? "";
        statusText.color = success ? skin.successColor : skin.errorColor;
        statusHideAt = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + 4f;
    }

    /// <summary>The window keeps its skin size, shrunk to fit small screens.</summary>
    private void FitToCanvas()
    {
        var rt = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        Vector2 size = skin.windowPrefab != null ? rt.sizeDelta : skin.windowSize;
        if (skin.windowPrefab == null)
            rt.sizeDelta = size;
        Vector2 room = canvasRect.rect.size - new Vector2(40f, 40f);
        float scale = Mathf.Min(1f, room.x / Mathf.Max(1f, size.x), room.y / Mathf.Max(1f, size.y));
        rt.localScale = Vector3.one * Mathf.Max(0.5f, scale);
    }

    private void Click() => PlaySound(skin != null ? skin.clickSound : null);

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || skin == null)
            return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(clip, skin.volume);
    }

    /// <summary>Wires a generated window's parts (used by <see cref="MerchantUIBuilder"/>).</summary>
    public void Configure(Image portraitImage, GameObject portraitHolder, TextMeshProUGUI shopName, TextMeshProUGUI greeting, Button close,
        TextMeshProUGUI wallet, Image walletImage, MerchantTabButton buy, MerchantTabButton sell, RectTransform tabs, RectTransform subTabsParent,
        GameObject subRow, TMP_InputField searchInput, Button sortBtn, TextMeshProUGUI sortText, MerchantTabButton affordable, MerchantTabButton inStock,
        ScrollRect scroll, RectTransform content, TextMeshProUGUI empty, MerchantDetailsPanel detailsPanel, TextMeshProUGUI status, MerchantConfirmDialog confirm)
    {
        portrait = portraitImage;
        portraitFrame = portraitHolder;
        shopNameText = shopName;
        greetingText = greeting;
        closeButton = close;
        walletText = wallet;
        walletIcon = walletImage;
        buyTab = buy;
        sellTab = sell;
        categoryTabs = tabs;
        subcategoryTabs = subTabsParent;
        subcategoryRow = subRow;
        searchField = searchInput;
        sortButton = sortBtn;
        sortLabel = sortText;
        affordableFilter = affordable;
        inStockFilter = inStock;
        itemsScroll = scroll;
        itemsContent = content;
        emptyText = empty;
        details = detailsPanel;
        statusText = status;
        confirmDialog = confirm;
    }
}
