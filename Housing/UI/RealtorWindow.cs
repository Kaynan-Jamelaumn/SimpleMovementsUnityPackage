using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The realtor's window (built from code, one per canvas), in four tabs:
/// <list type="bullet">
/// <item><b>Houses</b>: the houses this realtor lets, nearest first - their sizes, what each asks a day and for a week or
/// a month, whether it is free, its last prices, where it is (<i>Show the way</i> marks it in the world) - and an offer
/// for the chosen one, added to the player's order.</item>
/// <item><b>My order</b>: the order being made (it starts as the order the player placed last - a change that waits for
/// the order notice, else the order that stands - and is kept until placed): its houses in order of preference, their
/// offers and terms, the deposit it needs against the one held, whether placing it takes effect at once or after the
/// order notice, what is under way (a change waiting, a withdrawal), Place and Withdraw.</item>
/// <item><b>My home</b> and <b>Account</b>: the shared pages (<see cref="HousingHomePage"/>, <see cref="HousingAccountPage"/>).</item>
/// </list>
/// It changes nothing itself: everything is asked of the housing office (<see cref="HousingClient"/>), one request at a
/// time, and shown as the answers and views come back. A view that brings nothing new for a part of the window leaves it
/// as it is (the houses' cards are kept and only their texts change), so what is typed, hovered or selected stays; while
/// the housing office cannot answer, the window says why instead of showing an empty city.
/// </summary>
[DisallowMultipleComponent]
public class RealtorWindow : NPCWindow, IHousingHost
{
    private enum Tab { Houses, Order, Home, Account }

    /// <summary>A house of the order being made: the house, the term (<see cref="LeaseTerm"/>), the offer a day (thousandths).</summary>
    private sealed class DraftItem
    {
        public string house = "";
        public int term;
        public long offer;
    }

    /// <summary>A house's card in the list (kept between views: only its texts change).</summary>
    private sealed class HouseCard
    {
        public RectTransform root;
        public Button button;
        public TextMeshProUGUI name, where, sizes, asks, state;
    }

    private Realtor realtor;
    private CombatEntity player;
    private RealtorView view;
    /// <summary>Why the housing office cannot answer (empty: it can); there is no view meanwhile.</summary>
    private string closed = "";
    private bool viewDirty;
    private Tab tab = Tab.Houses;
    private HousingWindowCore core;
    private float nextFit;

    // The houses.
    private string selectedHouse = "";
    private string offerFor = "";
    private bool offerTouched;
    private int addTerm;
    private readonly Dictionary<string, HouseCard> houseCards = new Dictionary<string, HouseCard>();
    private readonly HashSet<string> cardsShown = new HashSet<string>();
    private readonly List<string> cardsGone = new List<string>();
    private readonly HousingLines lines = new HousingLines();

    // The order being made: kept until it is placed (also while the window is closed), for the player it was made by.
    private readonly List<DraftItem> draft = new List<DraftItem>();
    private bool draftEdited;
    private CombatEntity draftOwner;
    private int chosenItem = -1;
    private string editFor = "";
    private int editTerm;

    // UI
    private TextMeshProUGUI title, subtitle, purse;
    private Button housesTab, orderTab, homeTab, accountTab;
    private GameObject housesPage, orderPage;
    private RectTransform houseList, details, orderList;
    private HousingSection detailsSection, orderSection;
    private TMP_InputField offerInput, editInput;
    private TextMeshProUGUI houseNote, offerHint, offerCurrency, editName, editCurrency, depositText, mayOrderText, underWayText, draftText, clearingText;
    private Button weekButton, monthButton, addButton, wayButton, editWeek, editMonth, saveButton, placeButton, withdrawButton, undoButton;
    private HousingHomePage home;
    private HousingAccountPage account;

    public override string PanelName => Session != null && Session.Npc != null ? "Realtor: " + Session.Npc.DisplayName : "Realtor";

    CombatEntity IHousingHost.Player => player;
    bool IHousingHost.AtDesk => true;
    void IHousingHost.Send(HousingRequest request) => core.Send(request);
    void IHousingHost.Ask(string text, string yes, Action confirmed, bool danger, Action cancelled) => core.Ask(text, yes, confirmed, danger, cancelled);
    void IHousingHost.Say(string text, bool ok) => core.Say(text, ok);

    // ------------------------------------------------------------------ opening and closing
    /// <summary>Opens the houses of <paramref name="realtor"/> for <paramref name="session"/> (on the player's canvas).</summary>
    public static RealtorWindow Open(Realtor realtor, NPCInteractionSession session)
    {
        if (realtor == null || session == null) return null;
        RealtorWindow w = NPCWindowCache.Get<RealtorWindow>(session.Canvas, null, null, Create);
        w.Show(realtor, session);
        return w;
    }

    /// <summary>Closes the realtor window shown for <paramref name="session"/>.</summary>
    public static void CloseFor(Realtor realtor, NPCInteractionSession session)
    {
        if (session == null) return;
        RealtorWindow w = NPCWindowCache.TryGet<RealtorWindow>(session.Canvas, null, null);
        if (w != null && ReferenceEquals(w.Session, session)) w.Unbind();
    }

    private static RealtorWindow Create(Transform parent)
    {
        Image win = UITheme.Window("Realtor Window", parent, HousingUI.WindowSize);
        RealtorWindow w = win.gameObject.AddComponent<RealtorWindow>();
        w.Build(win.transform);
        return w;
    }

    private void Show(Realtor r, NPCInteractionSession session)
    {
        Bind(session); // a window still showing another session lets it go first (OnUnbound)
        if (!ReferenceEquals(Session, session)) return;
        realtor = r;
        player = CombatEntity.Resolve(session.Player);
        view = null;
        closed = "";
        viewDirty = false;
        if (draftOwner != player)
        {
            draft.Clear();
            draftEdited = false;
            draftOwner = player;
        }
        chosenItem = -1;
        editFor = "";
        selectedHouse = "";
        offerFor = "";
        offerTouched = false;
        offerInput.SetTextWithoutNotify("");
        editInput.SetTextWithoutNotify("");
        home.Reset();
        account.Reset();
        HousingClient.RealtorViewArrived -= OnView;
        HousingClient.RealtorViewArrived += OnView;
        HousingUI.FitToCanvas((RectTransform)transform, HousingUI.WindowSize);
        SetTab(Tab.Houses);
        core.Begin(player); // asks for the view (where the rules run it comes at once)
        if (player == null) core.Say("Nobody to rent a house for.", false);
        else if (draftEdited && draft.Count > 0) core.Tell("The order you were making is kept: it is not placed yet (My order).");
    }

    protected override void OnUnbound()
    {
        HousingClient.RealtorViewArrived -= OnView;
        if (core != null) core.End();
        view = null;
        closed = "";
        viewDirty = false;
        realtor = null;
        player = null;
    }

    /// <summary>Close button / Escape: closes a question first, else the window (back to the NPC's menu, or the end).</summary>
    public override void RequestClose()
    {
        if (core != null && core.Asking)
        {
            core.CloseConfirm();
            return;
        }
        NPCInteractionSession s = Session;
        if (s != null && realtor != null) s.BehaviourFinished(realtor);
        if (s == null || ReferenceEquals(Session, s)) base.RequestClose();
    }

    protected override void Update()
    {
        base.Update();
        if (Session == null) return;
        if (realtor == null || !realtor.isActiveAndEnabled)
        {
            RequestClose();
            return;
        }
        core.Tick();
        if (viewDirty && !HousingUI.PointerHeld())
        {
            viewDirty = false;
            Render();
        }
        if (Time.unscaledTime >= nextFit)
        {
            nextFit = Time.unscaledTime + 1f;
            HousingUI.FitToCanvas((RectTransform)transform, HousingUI.WindowSize);
        }
    }

    protected override void OnDestroy()
    {
        HousingClient.RealtorViewArrived -= OnView;
        if (core != null) core.End();
        base.OnDestroy();
    }

    private void OnView(RealtorView v)
    {
        if (Session == null || v == null || v.own) return; // the player's own housing window's view is not this one's
        core.Viewed();
        viewDirty = true;
        if (!string.IsNullOrEmpty(v.closed))
        {
            // The housing office cannot answer: nothing in the view is real (the order being made is kept).
            closed = HousingUI.Clean(v.closed).Trim();
            if (closed.Length == 0) closed = "The housing office is closed.";
            view = null;
            return;
        }
        closed = "";
        view = HousingUI.Fix(v);
        if (!draftEdited) LoadDraft();
    }

    /// <summary>What the window says while it has no view: why the housing office does not answer, or that it is asked.</summary>
    private string Waiting => closed.Length > 0 ? closed : "Asking the realtor...";

    private void OnAnswered(HousingRequestKind kind, HousingReply reply)
    {
        home.Answered(kind, reply);
        account.Answered(kind, reply);
        orderSection.Forget();
        if (reply == null || !reply.ok) return;
        if (kind == HousingRequestKind.PlaceOrder || kind == HousingRequestKind.CancelOrder)
        {
            // The order that stands is the one to show again (the next view brings it).
            draftEdited = false;
            chosenItem = -1;
            editFor = "";
        }
    }

    // ------------------------------------------------------------------ building
    private void Build(Transform w)
    {
        HousingUI.Header(w, RequestClose, out title, out subtitle, out purse);
        RectTransform tabs = UITheme.Area("Tabs", w, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -142f), new Vector2(-24f, -98f));
        InventoryUIFactory.Horizontal(tabs.gameObject, 8f);
        housesTab = UITheme.Button("Houses Tab", tabs, "Houses", () => SetTab(Tab.Houses), UITheme.ButtonStyle.Tab, 190f, 42f);
        orderTab = UITheme.Button("Order Tab", tabs, "My order", () => SetTab(Tab.Order), UITheme.ButtonStyle.Tab, 190f, 42f);
        homeTab = UITheme.Button("Home Tab", tabs, "My home", () => SetTab(Tab.Home), UITheme.ButtonStyle.Tab, 190f, 42f);
        accountTab = UITheme.Button("Account Tab", tabs, "Account", () => SetTab(Tab.Account), UITheme.ButtonStyle.Tab, 190f, 42f);

        RectTransform body = UITheme.Area("Body", w, Vector2.zero, Vector2.one, new Vector2(24f, 58f), new Vector2(-24f, -150f));
        housesPage = BuildHouses(body);
        orderPage = BuildOrder(body);
        home = new HousingHomePage(this, UITheme.Area("My Home", body, Vector2.zero, Vector2.one));
        account = new HousingAccountPage(this, UITheme.Area("Account", body, Vector2.zero, Vector2.one), false);

        TextMeshProUGUI status = HousingUI.StatusLine(w);
        HousingConfirm confirm = HousingUI.Confirm(w);
        core = new HousingWindowCore(status, confirm) { Answered = OnAnswered, HasView = () => view != null };
        SetTab(Tab.Houses);
    }

    private GameObject BuildHouses(Transform body)
    {
        RectTransform page = UITheme.Area("Houses", body, Vector2.zero, Vector2.one);
        Image left = HousingUI.Panel(page, "House List", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(500f, 0f));
        houseList = HousingUI.FillList(left.transform, "Houses", 8, 6f);
        houseNote = HousingUI.Note(houseList, ""); // first in the list; the houses' cards follow it

        Image right = HousingUI.Panel(page, "House", Vector2.zero, Vector2.one, new Vector2(516f, 0f), Vector2.zero);
        details = UITheme.ScrollList("Details", right.transform, 0f, out _, new Color(0f, 0f, 0f, 0f), 14, 6f);
        detailsSection = new HousingSection(details);
        UITheme.Place((RectTransform)details.parent.parent, Vector2.zero, Vector2.one, new Vector2(4f, 156f), new Vector2(-4f, -4f));

        // The offer for the chosen house (kept while the details are rebuilt).
        RectTransform foot = UITheme.Area("Offer", right.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 6f), new Vector2(-16f, 152f));
        InventoryUIFactory.Vertical(foot.gameObject, 0, 8f, false);
        RectTransform r1 = UITheme.Row("Offer Row", foot, 40f, 8f);
        InventoryUIFactory.Size(HousingUI.Text(r1, "Your offer a day", 15f), 140f, 40f, 0f);
        offerInput = OfferField("Offer", r1);
        InventoryUIFactory.Size(offerInput, 130f, 40f, 0f);
        offerInput.onValueChanged.AddListener(_ =>
        {
            offerTouched = true;
            ShowOfferHint();
        });
        offerCurrency = HousingUI.Text(r1, "", 15f, UITheme.Muted);
        InventoryUIFactory.Size(offerCurrency, 36f, 40f, 0f);
        UITheme.Spacer(r1);
        HousingUI.TermButtons(r1, out weekButton, out monthButton, () => SetAddTerm((int)LeaseTerm.Week), () => SetAddTerm((int)LeaseTerm.Month));
        RectTransform r2 = UITheme.Row("Offer Buttons", foot, 44f, 8f);
        addButton = UITheme.Button("Add", r2, "Add to my order", AddToOrder, UITheme.ButtonStyle.Primary, 0f, 44f);
        wayButton = UITheme.Button("Way", r2, "Show the way", ShowTheWay, UITheme.ButtonStyle.Normal, 190f, 44f);
        offerHint = HousingUI.Text(foot, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(offerHint, -1f, 38f);
        return page.gameObject;
    }

    private GameObject BuildOrder(Transform body)
    {
        RectTransform page = UITheme.Area("Order", body, Vector2.zero, Vector2.one);
        Image left = HousingUI.Panel(page, "Order List", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 0f));
        orderList = HousingUI.FillList(left.transform, "Order", 8, 6f);
        orderSection = new HousingSection(orderList);

        Image right = HousingUI.Panel(page, "Order Side", Vector2.zero, Vector2.one, new Vector2(616f, 0f), Vector2.zero);
        RectTransform col = UITheme.Column("Column", right.transform, 16, 6f);
        InventoryUIFactory.Stretch(col);
        UITheme.Header(col, "The chosen house");
        editName = HousingUI.Text(col, "", 16f, UITheme.Text, FontStyles.Bold);
        RectTransform r1 = UITheme.Row("Edit Row", col, 40f, 8f);
        InventoryUIFactory.Size(HousingUI.Text(r1, "Offer a day", 15f), 100f, 40f, 0f);
        editInput = OfferField("Edit Offer", r1);
        InventoryUIFactory.Size(editInput, 110f, 40f, 0f);
        editCurrency = HousingUI.Text(r1, "", 15f, UITheme.Muted);
        InventoryUIFactory.Size(editCurrency, 30f, 40f, 0f);
        UITheme.Spacer(r1);
        HousingUI.TermButtons(r1, out editWeek, out editMonth, () => SetEditTerm((int)LeaseTerm.Week), () => SetEditTerm((int)LeaseTerm.Month));
        saveButton = UITheme.Button("Save", col, "Save this change", SaveItem, UITheme.ButtonStyle.Normal, 0f, 40f);
        UITheme.Header(col, "Deposit");
        depositText = HousingUI.Text(col, "", 15f);
        mayOrderText = HousingUI.Text(col, "", 15f, UITheme.Bad);
        underWayText = HousingUI.Text(col, "", 15f, UITheme.Gold);
        draftText = HousingUI.Text(col, "", 15f, UITheme.Gold);
        RectTransform r2 = UITheme.Row("Order Buttons", col, 44f, 8f);
        r2.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        placeButton = UITheme.Button("Place", r2, "Place order", Place, UITheme.ButtonStyle.Primary, 0f, 44f);
        withdrawButton = UITheme.Button("Withdraw", r2, "Withdraw order", Withdraw, UITheme.ButtonStyle.Danger, 0f, 44f);
        undoButton = UITheme.Button("Undo", col, "Undo my changes", Undo, UITheme.ButtonStyle.Normal, 0f, 38f);
        clearingText = HousingUI.Text(col, "", 14f, UITheme.Muted);
        return page.gameObject;
    }

    /// <summary>
    /// An offer a day: digits, and '.' or ',' (either may be the decimal mark) whatever the system's language - a text
    /// field's own decimal check would drop the one the language does not use ("12.50" read as 1250).
    /// </summary>
    private static TMP_InputField OfferField(string name, Transform row)
    {
        TMP_InputField f = UITheme.Input(name, row, "12.50", 16, 40f);
        f.contentType = TMP_InputField.ContentType.DecimalNumber;
        f.onValidateInput = HousingUI.NumberChar;
        return f;
    }

    // ------------------------------------------------------------------ showing
    private string Currency => view != null ? view.account.currency : "";

    private string Money(long amount) => HousingUI.Money(amount, Currency);

    private string PerDay(long milli) => HousingUI.PerDay(milli, Currency);

    private string Dur(double days) => view != null ? HousingUI.Duration(days, view.secondsPerDay, view.gameTime) : "";

    private int Days(int term) => HousingUI.TermDays(view, term);

    private static bool Allows(ListingView l, int term) => l == null || (term == (int)LeaseTerm.Month ? l.monthly : l.weekly);

    private static int FirstTerm(ListingView l) => l == null || l.weekly ? (int)LeaseTerm.Week : (int)LeaseTerm.Month;

    private void SetTab(Tab t)
    {
        tab = t;
        housesPage.SetActive(t == Tab.Houses);
        orderPage.SetActive(t == Tab.Order);
        home.Root.SetActive(t == Tab.Home);
        account.Root.SetActive(t == Tab.Account);
        UITheme.SetSelected(housesTab, t == Tab.Houses);
        UITheme.SetSelected(orderTab, t == Tab.Order);
        UITheme.SetSelected(homeTab, t == Tab.Home);
        UITheme.SetSelected(accountTab, t == Tab.Account);
        Render();
    }

    private void Render()
    {
        try
        {
            RenderHeader();
            switch (tab)
            {
                case Tab.Houses: RenderHouses(); break;
                case Tab.Order: RenderOrder(); break;
                case Tab.Home: home.Show(view, closed.Length > 0 ? closed : null); break;
                default:
                    account.Show(view != null ? view.account : null, view, view != null ? view.secondsPerDay : 86400.0, view != null && view.gameTime,
                        closed.Length > 0 ? closed : null);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    private void RenderHeader()
    {
        string npc = Session != null && Session.Npc != null ? Session.Npc.DisplayName : view != null && view.realtorName.Length > 0 ? view.realtorName : "Realtor";
        HousingUI.Set(title, npc);
        HousingUI.Set(subtitle, view == null ? Waiting : "Houses to let - next clearing in " + Dur(view.nextClearingIn));
        HousingUI.Set(purse, view == null ? "" : "Purse: " + Money(view.account.wallet));
        HousingUI.SetLabel(orderTab, draft.Count > 0 ? $"My order ({draft.Count})" : "My order");
        int home = view != null ? view.leases.Count : 0;
        HousingUI.SetLabel(homeTab, home > 0 ? $"My home ({home})" : "My home");
        int waiting = view != null ? view.account.notices.Count + view.account.held.Count : 0;
        HousingUI.SetLabel(accountTab, waiting > 0 ? $"Account ({waiting})" : "Account");
    }

    // ------------------------------------------------------------------ the houses
    private ListingView SelectedListing()
    {
        if (view == null) return null;
        ListingView l = HousingUI.Listing(view, selectedHouse);
        if (l == null || l.elsewhere)
        {
            // The first of this realtor's houses (those of the order another realtor lets are not in its list).
            l = view.listings.Find(x => !x.elsewhere);
            if (l != null) selectedHouse = l.id;
        }
        return l;
    }

    private void RenderHouses()
    {
        HousingUI.Set(houseNote, view == null ? Waiting : !view.listings.Exists(x => !x.elsewhere) ? "No houses are let here." : "", true);
        ListingView selected = SelectedListing();
        cardsShown.Clear();
        int index = 1; // after the note
        if (view != null)
        {
            foreach (ListingView l in view.listings)
            {
                if (l.elsewhere || !cardsShown.Add(l.id)) continue; // in the player's order, but another realtor lets it
                if (!houseCards.TryGetValue(l.id, out HouseCard c) || c.root == null)
                {
                    c = MakeCard(l.id);
                    houseCards[l.id] = c;
                }
                HousingUI.Set(c.name, l.name.Length > 0 ? l.name : "A house");
                HousingUI.Set(c.where, (l.district.Trim().Length > 0 ? l.district.Trim() + ", " : "") + HousingUI.Way(l));
                HousingUI.Set(c.sizes, HousingUI.Sizes(l));
                HousingUI.Set(c.asks, "Asks " + PerDay(l.askingMilli));
                string state = State(l, out Color color);
                HousingUI.Set(c.state, state, color);
                UITheme.SetSelected(c.button, l.id == selectedHouse);
                if (c.root.GetSiblingIndex() != index) c.root.SetSiblingIndex(index);
                index++;
            }
        }
        // The cards of houses no longer listed go.
        cardsGone.Clear();
        foreach (KeyValuePair<string, HouseCard> kv in houseCards)
            if (!cardsShown.Contains(kv.Key)) cardsGone.Add(kv.Key);
        foreach (string id in cardsGone)
        {
            HouseCard c = houseCards[id];
            houseCards.Remove(id);
            if (c.root == null) continue;
            c.root.gameObject.SetActive(false);
            Destroy(c.root.gameObject);
        }
        RenderDetails(selected);
    }

    private HouseCard MakeCard(string id)
    {
        var c = new HouseCard();
        c.root = HousingUI.Named(HousingUI.ClickCard(houseList, () => SelectHouse(id), out c.button), "House " + id);
        RectTransform top = HousingUI.Line(c.root, 8f, 24f);
        c.name = HousingUI.Flex(HousingUI.Text(top, "", 17f, UITheme.Text, FontStyles.Bold));
        c.where = HousingUI.Text(top, "", 13f, UITheme.Muted, FontStyles.Normal, TextAlignmentOptions.Right);
        c.sizes = HousingUI.Text(c.root, "", 14f);
        RectTransform bottom = HousingUI.Line(c.root, 8f, 22f);
        c.asks = HousingUI.Text(bottom, "", 14f, UITheme.Gold);
        c.state = HousingUI.Flex(HousingUI.Text(bottom, "", 14f, UITheme.Muted, FontStyles.Normal, TextAlignmentOptions.Right));
        return c;
    }

    private void SelectHouse(string id)
    {
        if (HousingUI.Listing(view, id) == null) return;
        selectedHouse = id;
        foreach (KeyValuePair<string, HouseCard> kv in houseCards)
            if (kv.Value.button != null) UITheme.SetSelected(kv.Value.button, kv.Key == id);
        try
        {
            RenderDetails(SelectedListing());
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    private int DraftIndex(string house) => draft.FindIndex(d => d.house == house);

    /// <summary>
    /// "Free - first clearing in 5 h", "Let to Bob - its term ends in 3 days", "Let to Bob - comes free in 2 days, offered a
    /// day later" (its tenant does not renew), "Yours" (and its place in the order).
    /// </summary>
    private string State(ListingView l, out Color color)
    {
        int rank = DraftIndex(l.id) + 1;
        string tail = rank > 0 ? $" - choice {rank} in your order" : "";
        if (l.mine)
        {
            color = UITheme.Gold;
            return "Yours" + tail;
        }
        if (l.vacant)
        {
            color = UITheme.Good;
            return (l.firstClearingIn > 0.0005 ? "Free - first clearing in " + Dur(l.firstClearingIn) : "Free") + tail;
        }
        string who = l.tenant.Trim().Length > 0 ? "Let to " + l.tenant.Trim() : "Let";
        if (l.comesFree)
        {
            color = UITheme.Accent;
            return who + " - comes free in " + Dur(l.endsIn) + ", offered a day later" + tail;
        }
        color = UITheme.Muted;
        return who + " - its term ends in " + Dur(l.endsIn) + tail;
    }

    /// <summary>The least offer a house takes (what it asks, when the server does not say).</summary>
    private static long Least(ListingView l) => l.minOfferMilli > 0 ? l.minOfferMilli : l.askingMilli;

    private string AskText(ListingView l)
    {
        var parts = new List<string>();
        if (l.weekly) parts.Add($"{Money(HousingUI.Total(l.askingMilli, view.weekDays))} a week ({HousingUI.Plural(view.weekDays, "day", "days")})");
        if (l.monthly) parts.Add($"{Money(HousingUI.Total(l.askingMilli, view.monthDays))} a month ({HousingUI.Plural(view.monthDays, "day", "days")})");
        return $"Asks {PerDay(l.askingMilli)}: {string.Join(", ", parts)}.";
    }

    /// <summary>The chosen house's details (made again only when what they say changed), and the offer under them.</summary>
    private void RenderDetails(ListingView l)
    {
        lines.Clear();
        if (l == null) lines.Note(view == null ? Waiting : "Choose a house.");
        else DetailLines(l);
        detailsSection.Show(lines.Signature(), lines.Build);
        ShowOffer(l);
    }

    private void DetailLines(ListingView l)
    {
        lines.Text(l.name.Length > 0 ? l.name : "A house", 24f, UITheme.Text, FontStyles.Bold);
        string way = HousingUI.Way(l);
        lines.Text((l.district.Trim().Length > 0 ? l.district.Trim() + " - " : "") + (way == "right here" ? "right here" : way + " of here"), 14f, UITheme.Muted);
        lines.Text(State(l, out Color c), 16f, c);
        if (l.comesFree && !l.mine)
            lines.Text("Its tenant does not renew: it comes free when its term ends, and is offered to everyone a day later. Name it in your order to be ready.", 14f, UITheme.Muted);
        if (l.description.Trim().Length > 0) lines.Text(l.description.Trim(), 15f);

        lines.Header("The house and its land");
        lines.Text($"House: {HousingUI.SquareMetres(l.houseArea)} of floor on {HousingUI.Plural(l.floors, "floor", "floors")} ({HousingUI.SquareMetres(l.houseFootprint)} on the ground).", 15f);
        lines.Text("Land: " + HousingUI.SquareMetres(l.landArea) + (l.gardenArea > 0.5f ? ", with " + HousingUI.SquareMetres(l.gardenArea) + " of garden." : ", no garden."), 15f);
        if (l.zones.Trim().Length > 0) lines.Text("Also: " + l.zones.Trim() + ".", 15f);

        lines.Header("Rent");
        lines.Text(l.weekly && l.monthly ? "Let by the week or by the month." : l.weekly ? "Let by the week only." : "Let by the month only.", 15f);
        lines.Text(AskText(l), 15f, UITheme.Gold);
        lines.Text($"Its sizes are worth {PerDay(l.baseMilli)} (its base rent); what it asks follows the offers made for it.", 14f, UITheme.Muted);
        if (!l.vacant && !l.mine && !l.comesFree && Least(l) < l.askingMilli)
            lines.Text($"While it is let, offers start at {PerDay(Least(l))}: what others offered for it does not make yours dearer.", 14f, UITheme.Muted);
        if (l.takeAtMilli > 0 && !l.mine)
            lines.Text($"Its renewal is fixed: at its lease end, in {Dur(l.endsIn)}, an offer of at least {PerDay(l.takeAtMilli)} takes it from its tenant (the best such offer wins).", 14f, UITheme.Accent);
        int i = DraftIndex(l.id);
        if (i >= 0)
            lines.Text($"In your order (choice {i + 1}): {PerDay(draft[i].offer)} by the {HousingUI.TermName(draft[i].term)}.", 15f, UITheme.Accent);
        else if (l.myRank > 0 && l.myOfferMilli > 0)
            lines.Text($"Your order that stands offers {PerDay(l.myOfferMilli)} by the {HousingUI.TermName(l.myTerm)} (choice {l.myRank}).", 15f, UITheme.Accent);

        lines.Header("Last prices paid");
        int n = Math.Min(l.salesMilli.Count, l.salesAgo.Count);
        if (n == 0) lines.Note("None yet.");
        for (int k = 0; k < n; k++)
            lines.Text($"{PerDay(l.salesMilli[k])}, {HousingUI.Ago(l.salesAgo[k], view.secondsPerDay, view.gameTime)}", 15f);
    }

    /// <summary>The offer under the details: filled for the chosen house (its offer in the order, else what it asks).</summary>
    private void ShowOffer(ListingView l)
    {
        bool can = l != null && !l.mine && player != null;
        offerInput.interactable = can;
        addButton.interactable = can;
        wayButton.interactable = l != null;
        HousingUI.Set(offerCurrency, Currency);
        if (l == null)
        {
            offerFor = "";
            HousingUI.ShowTerms(weekButton, monthButton, true, true, addTerm, false);
            HousingUI.Set(offerHint, "");
            return;
        }
        int i = DraftIndex(l.id);
        if (l.id != offerFor || !offerTouched)
        {
            if (l.id != offerFor) addTerm = i >= 0 ? draft[i].term : FirstTerm(l);
            offerFor = l.id;
            offerTouched = false;
            offerInput.SetTextWithoutNotify(HousingUI.Rate(i >= 0 ? draft[i].offer : HousingUI.UpToCent(Least(l)), false));
        }
        if (!Allows(l, addTerm)) addTerm = FirstTerm(l);
        HousingUI.ShowTerms(weekButton, monthButton, l.weekly, l.monthly, addTerm, can);
        HousingUI.SetLabel(addButton, i >= 0 ? "Change my offer" : "Add to my order");
        ShowOfferHint();
    }

    private void ShowOfferHint()
    {
        ListingView l = HousingUI.Listing(view, offerFor);
        if (l == null)
        {
            HousingUI.Set(offerHint, "");
            return;
        }
        if (l.mine)
        {
            HousingUI.Set(offerHint, "You rent it: see My home.");
            return;
        }
        if (!HousingUI.ParseRate(offerInput.text, out long offer, out string problem))
        {
            HousingUI.Set(offerHint, problem);
            return;
        }
        if (offer < Least(l))
        {
            HousingUI.Set(offerHint, $"It takes offers of at least {PerDay(Least(l))}.");
            return;
        }
        HousingUI.Set(offerHint, $"At that offer a {HousingUI.TermName(addTerm)} costs {Money(HousingUI.Total(offer, Days(addTerm)))}. " +
            "Your order holds its largest total as a deposit; you pay only for the house you win.");
    }

    private void SetAddTerm(int term)
    {
        ListingView l = HousingUI.Listing(view, offerFor);
        if (l != null && !Allows(l, term)) return;
        addTerm = term;
        HousingUI.ShowTerms(weekButton, monthButton, l == null || l.weekly, l == null || l.monthly, addTerm, l != null && !l.mine);
        ShowOfferHint();
    }

    private void AddToOrder()
    {
        ListingView l = SelectedListing();
        if (view == null || l == null) return;
        if (l.mine)
        {
            core.Say("You rent it already.", false);
            return;
        }
        if (!HousingUI.ParseRate(offerInput.text, out long offer, out string problem))
        {
            core.Say(problem, false);
            return;
        }
        if (offer < Least(l))
        {
            core.Say($"{l.name} takes offers of at least {PerDay(Least(l))}.", false);
            return;
        }
        if (!Allows(l, addTerm))
        {
            core.Say($"{l.name} is not let by the {HousingUI.TermName(addTerm)}.", false);
            return;
        }
        int i = DraftIndex(l.id);
        if (i < 0 && draft.Count >= view.maxOrderItems)
        {
            core.Say($"An order names at most {view.maxOrderItems} houses: remove one first (My order).", false);
            return;
        }
        if (i < 0)
        {
            draft.Add(new DraftItem { house = l.id, term = addTerm, offer = offer });
            core.Say($"{l.name} is choice {draft.Count} in your order. Place the order in My order.", true);
        }
        else
        {
            draft[i].term = addTerm;
            draft[i].offer = offer;
            core.Say($"Your offer for {l.name} is changed. Place the order in My order.", true);
        }
        draftEdited = true;
        offerTouched = false;
        RenderHeader();
        RenderHouses();
    }

    private void ShowTheWay()
    {
        ListingView l = SelectedListing();
        if (l == null) return;
        Property p = HousingDirector.FindProperty(l.id);
        if (p == null)
        {
            core.Say($"{l.name} is too far away to show.", false);
            return;
        }
        if (player != null && p.Contains(player.transform.position))
        {
            core.Say($"You are on the land of {l.name}.", true);
            return;
        }
        HousingMarker.Show(p, player);
        string way = HousingUI.Way(l);
        core.Tell(way == "right here" ? $"Look for the light over {l.name}, right here." : $"Look for the light over {l.name}: {way} of here.");
    }

    // ------------------------------------------------------------------ the order being made
    /// <summary>The order as the player placed it last: the change that waits for the order notice, else the order that stands.</summary>
    private void Placed(out List<string> houses, out List<int> terms, out List<long> offers)
    {
        bool waiting = HousingUI.ChangeWaiting(view);
        houses = waiting ? view.orderPendingHouses : view.orderHouses;
        terms = waiting ? view.orderPendingTerms : view.orderTerms;
        offers = waiting ? view.orderPendingOffersMilli : view.orderOffersMilli;
    }

    private void LoadDraft()
    {
        draft.Clear();
        if (view == null) return;
        Placed(out List<string> houses, out List<int> terms, out List<long> offers);
        int n = Math.Min(houses.Count, Math.Min(terms.Count, offers.Count));
        for (int i = 0; i < n; i++)
            draft.Add(new DraftItem { house = houses[i] ?? "", term = terms[i], offer = offers[i] });
        if (chosenItem >= draft.Count) chosenItem = -1;
    }

    private bool DraftDiffers()
    {
        if (view == null) return draft.Count > 0;
        Placed(out List<string> houses, out List<int> terms, out List<long> offers);
        int n = Math.Min(houses.Count, Math.Min(terms.Count, offers.Count));
        if (n != draft.Count) return true;
        for (int i = 0; i < n; i++)
            if (draft[i].house != houses[i] || draft[i].term != terms[i] || draft[i].offer != offers[i]) return true;
        return false;
    }

    /// <summary>The deposit an order holds: the largest of its houses' totals for their terms (the books' rule).</summary>
    private long DraftEscrow()
    {
        long e = 0;
        foreach (DraftItem d in draft) e = Math.Max(e, HousingUI.Total(d.offer, Days(d.term)));
        return e;
    }

    /// <summary>Does the draft keep every house of <paramref name="houses"/>, at its term, for no less (only adds or offers more)?</summary>
    private bool OnlyAdds(List<string> houses, List<int> terms, List<long> offers)
    {
        int n = Math.Min(houses.Count, Math.Min(terms.Count, offers.Count));
        for (int i = 0; i < n; i++)
        {
            DraftItem d = draft.Find(x => x.house == houses[i]);
            if (d == null || d.term != terms[i] || d.offer < offers[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Placing the draft takes something back from the order that stands (a house dropped, an offer lowered, a term
    /// changed): it takes effect only after the order notice, and the order stands as it is until then.
    /// </summary>
    private bool TakesBack() => view != null && view.orderHouses.Count > 0 && !OnlyAdds(view.orderHouses, view.orderTerms, view.orderOffersMilli);

    /// <summary>A change that takes something back keeps the time of the change already waiting (it takes back nothing more).</summary>
    private bool KeepsWaitingTime() =>
        HousingUI.ChangeWaiting(view) && OnlyAdds(view.orderPendingHouses, view.orderPendingTerms, view.orderPendingOffersMilli);

    /// <summary>
    /// What placing the draft takes from the purse (less than 0: gives back), as the books reckon it: a change that takes
    /// something back holds the larger deposit until it takes effect.
    /// </summary>
    private long DraftDelta()
    {
        long escrow = DraftEscrow();
        long held = view != null ? view.orderEscrow : 0;
        return TakesBack() ? Math.Max(held, escrow) - held : escrow - held;
    }

    /// <summary>What keeps an item from being placed (blocking), or a remark on it; null when it is fine.</summary>
    private string Problem(DraftItem d, out bool blocking)
    {
        blocking = false;
        ListingView l = HousingUI.Listing(view, d.house);
        if (l == null)
        {
            // A house of the order that stands is always in the view while its place is loaded: its place is not open
            // now, and the books cannot take an order naming it.
            if (view.orderHouses.Contains(d.house))
            {
                blocking = true;
                return "Its place is not open now: remove it to change your order (until you do, your order stands as it is).";
            }
            return "Another realtor lets it: its price and terms are checked when you place the order.";
        }
        blocking = true;
        if (l.mine) return "You rent it already.";
        if (!Allows(l, d.term)) return $"It is not let by the {HousingUI.TermName(d.term)}.";
        if (d.offer < Least(l)) return $"It takes offers of at least {PerDay(Least(l))} now: raise your offer.";
        blocking = false;
        if (d.offer > l.askingMilli * (double)view.confirmAboveAsking) return $"Your offer is over {HousingUI.Number(view.confirmAboveAsking)} times what it asks.";
        if (l.elsewhere) return "Another realtor lets it (it is in your order all the same).";
        return null;
    }

    /// <summary>The order's list (made again only when what it shows changed), then the side.</summary>
    private void RenderOrder()
    {
        if (chosenItem >= draft.Count) chosenItem = -1;
        var sig = new HousingSig().Add(view == null ? Waiting : "").Add(draft.Count).Add(chosenItem);
        if (view != null)
        {
            sig.Add(view.maxOrderItems).Add(Currency);
            foreach (DraftItem d in draft)
            {
                string problem = Problem(d, out bool blocking);
                sig.Add(HousingUI.HouseName(view, d.house)).Add(d.term).Add(d.offer).Add(Days(d.term)).Add(problem).Add(blocking);
            }
        }
        orderSection.Show(sig.ToString(), BuildOrderList);
        RenderOrderSide();
    }

    private void BuildOrderList(RectTransform list)
    {
        if (view == null) HousingUI.Note(list, Waiting);
        else if (draft.Count == 0)
            HousingUI.Note(list, $"Your order is empty. Choose houses in Houses and add them: up to {view.maxOrderItems}, the one you want most first.");
        for (int i = 0; i < draft.Count; i++)
        {
            int index = i;
            DraftItem d = draft[i];
            // Named after its house: Up, Down and Remove stay with it as the list changes (never another house's).
            RectTransform row = HousingUI.Named(HousingUI.Line(list, 6f, 56f), "Choice " + d.house);
            RectTransform card = HousingUI.ClickCard(row, () => ChooseItem(index), out Button b);
            HousingUI.Flex(card);
            HousingUI.Text(card, $"{i + 1}. {HousingUI.HouseName(view, d.house)}", 16f, UITheme.Text, FontStyles.Bold);
            HousingUI.Text(card, $"{PerDay(d.offer)} by the {HousingUI.TermName(d.term)}: {Money(HousingUI.Total(d.offer, Days(d.term)))}", 14f);
            bool blocking = false;
            string problem = view != null ? Problem(d, out blocking) : null;
            if (problem != null) HousingUI.Text(card, problem, 13f, blocking ? UITheme.Bad : UITheme.Muted);
            UITheme.SetSelected(b, index == chosenItem);
            Button up = HousingUI.Small(row, "Up", () => MoveItem(index, -1), 62f);
            up.interactable = index > 0;
            Button down = HousingUI.Small(row, "Down", () => MoveItem(index, 1), 74f);
            down.interactable = index < draft.Count - 1;
            HousingUI.Small(row, "Remove", () => RemoveItem(index), 92f);
        }
    }

    private void RenderOrderSide()
    {
        DraftItem d = chosenItem >= 0 && chosenItem < draft.Count ? draft[chosenItem] : null;
        ListingView l = d != null ? HousingUI.Listing(view, d.house) : null;
        if (d == null)
        {
            editFor = "";
            HousingUI.Set(editName, draft.Count > 0 ? "Choose a house in your order to change its offer or term." : "");
            editInput.SetTextWithoutNotify("");
        }
        else
        {
            HousingUI.Set(editName, $"{chosenItem + 1}. {HousingUI.HouseName(view, d.house)}");
            if (editFor != d.house)
            {
                editFor = d.house;
                editTerm = d.term;
                editInput.SetTextWithoutNotify(HousingUI.Rate(d.offer, false));
            }
        }
        editInput.interactable = d != null;
        saveButton.interactable = d != null;
        HousingUI.Set(editCurrency, Currency);
        HousingUI.ShowTerms(editWeek, editMonth, l == null || l.weekly, l == null || l.monthly, editTerm, d != null);

        if (view == null)
        {
            HousingUI.Set(depositText, "");
            HousingUI.Set(mayOrderText, "", true);
            HousingUI.Set(underWayText, "", true);
            HousingUI.Set(draftText, "", true);
            HousingUI.Set(clearingText, "");
            placeButton.interactable = withdrawButton.interactable = undoButton.interactable = false;
            return;
        }
        long escrow = DraftEscrow();
        long held = view.orderEscrow;
        bool differs = DraftDiffers();
        bool standing = view.orderHouses.Count > 0;
        bool withdrawing = HousingUI.Withdrawing(view);
        bool later = TakesBack();
        string notice = Dur(view.orderNotice);
        string s;
        if (draft.Count == 0)
            s = standing ? $"Your list is empty. You hold {Money(held)} as the deposit of the order that stands: withdraw it to get that back."
                : "An order holds the largest total of its houses as a deposit, and gives back what is left over.";
        else
        {
            s = $"It needs a deposit of {Money(escrow)} (its largest total). You hold {Money(held)} now.";
            if (differs || withdrawing)
            {
                long delta = DraftDelta();
                s += delta > 0 ? $" Placing it takes {Money(delta)} from your purse." : delta < 0 ? $" Placing it gives {Money(-delta)} back." : " Placing it costs nothing more.";
                if (later && escrow < held) s += " What is left over comes back when the change takes effect.";
            }
        }
        HousingUI.Set(depositText, s);
        HousingUI.Set(mayOrderText, view.mayOrder.Length > 0 ? "You cannot order now: " + view.mayOrder : "", true);
        HousingUI.Set(underWayText, HousingUI.OrderUnderWay(view), true);
        string said;
        if (differs && draft.Count > 0)
            said = "Your changes are not placed yet. " + (!later ? "Placed, they take effect at once."
                : KeepsWaitingTime() ? $"Placed, they take effect with the change that waits, in {Dur(view.orderPendingIn)}."
                : $"They take something back: placed, they take effect in about {notice}, and until then your order stands as it is.");
        else if (differs) said = "";
        else if (withdrawing && draft.Count > 0) said = "Place it to call the withdrawal off.";
        else if (HousingUI.ChangeWaiting(view)) said = "This is the change you placed: it waits for the order notice.";
        else said = standing ? "Your order stands as it is." : "";
        HousingUI.Set(draftText, said, true);
        placeButton.interactable = view.mayOrder.Length == 0 && draft.Count > 0 && (differs || withdrawing) && player != null;
        withdrawButton.interactable = standing && !withdrawing && player != null;
        undoButton.interactable = differs;
        HousingUI.Set(clearingText, $"Next clearing in {Dur(view.nextClearingIn)}. You win at most one house: the first in your list that yours is the best offer for.");
    }

    private void ChooseItem(int index)
    {
        if (index < 0 || index >= draft.Count) return;
        chosenItem = index;
        editFor = "";
        RenderOrder();
    }

    private void SetEditTerm(int term)
    {
        DraftItem d = chosenItem >= 0 && chosenItem < draft.Count ? draft[chosenItem] : null;
        ListingView l = d != null ? HousingUI.Listing(view, d.house) : null;
        if (d == null || (l != null && !Allows(l, term))) return;
        editTerm = term;
        HousingUI.ShowTerms(editWeek, editMonth, l == null || l.weekly, l == null || l.monthly, editTerm, true);
    }

    private void SaveItem()
    {
        if (chosenItem < 0 || chosenItem >= draft.Count) return;
        DraftItem d = draft[chosenItem];
        ListingView l = HousingUI.Listing(view, d.house);
        if (!HousingUI.ParseRate(editInput.text, out long offer, out string problem))
        {
            core.Say(problem, false);
            return;
        }
        if (l != null && offer < Least(l))
        {
            core.Say($"{l.name} takes offers of at least {PerDay(Least(l))}.", false);
            return;
        }
        if (l != null && !Allows(l, editTerm))
        {
            core.Say($"{l.name} is not let by the {HousingUI.TermName(editTerm)}.", false);
            return;
        }
        d.offer = offer;
        d.term = editTerm;
        draftEdited = true;
        core.Say("Changed: it is not placed yet.", true);
        RenderOrder();
    }

    private void MoveItem(int index, int by)
    {
        int to = index + by;
        if (index < 0 || index >= draft.Count || to < 0 || to >= draft.Count) return;
        DraftItem d = draft[index];
        draft[index] = draft[to];
        draft[to] = d;
        if (chosenItem == index) chosenItem = to;
        else if (chosenItem == to) chosenItem = index;
        draftEdited = true;
        RenderOrder();
    }

    private void RemoveItem(int index)
    {
        if (index < 0 || index >= draft.Count) return;
        draft.RemoveAt(index);
        if (chosenItem == index)
        {
            chosenItem = -1;
            editFor = "";
        }
        else if (chosenItem > index)
        {
            chosenItem--;
        }
        draftEdited = true;
        RenderHeader();
        RenderOrder();
    }

    private void Undo()
    {
        draftEdited = false;
        LoadDraft();
        chosenItem = -1;
        editFor = "";
        core.Tell("Your order is as you placed it again.");
        Render();
    }

    private void Place()
    {
        if (view == null) return;
        if (view.mayOrder.Length > 0)
        {
            core.Say(view.mayOrder, false);
            return;
        }
        if (draft.Count == 0)
        {
            core.Say("Add a house to your order first.", false);
            return;
        }
        foreach (DraftItem d in draft)
        {
            string problem = Problem(d, out bool blocking);
            if (blocking)
            {
                core.Say($"{HousingUI.HouseName(view, d.house)}: {problem}", false);
                return;
            }
        }
        var houses = new List<string>();
        var terms = new List<int>();
        var offers = new List<long>();
        foreach (DraftItem d in draft)
        {
            houses.Add(d.house);
            terms.Add(d.term);
            offers.Add(d.offer);
        }
        long escrow = DraftEscrow();
        long delta = DraftDelta();
        bool later = TakesBack();
        long holds = view.orderEscrow + delta;
        string money = delta > 0 ? $" It takes {Money(delta)} from your purse now." : delta < 0 ? $" {Money(-delta)} of the deposit you hold comes back now." : "";
        string rule = $"Your order is sealed: nobody sees it. At each clearing (every {Dur(view.clearingEvery)}) every house offered goes to the best offer for it; " +
            "you win at most one house - the first in your list among those you are best for - and pay what you offered for the term. " +
            "Offers are binding: adding a house or offering more takes effect at once; anything that takes something back waits the order notice. ";
        if (later)
            rule += (KeepsWaitingTime() ? $"This change takes effect with the change that waits, in {Dur(view.orderPendingIn)}"
                : $"This change takes something back: it takes effect in about {Dur(view.orderNotice)}") +
                " - until then your order stands as it is, and may still win a house (and pays for it if it does). ";
        else if (HousingUI.ChangeWaiting(view)) rule += "This takes effect at once, in place of the change that waits. ";
        if (HousingUI.Withdrawing(view)) rule += "It calls off the withdrawal of your order. ";
        rule += $"It holds {Money(holds)} as a deposit; what is left over is given back" + (later && escrow < holds ? " when the change takes effect." : ".") + money;
        string high = HighOffers();
        core.Ask(rule, "Place order", () =>
        {
            if (high != null) core.Ask(high, "Place it anyway", () => SendOrder(houses, terms, offers, delta), true);
            else SendOrder(houses, terms, offers, delta);
        });
    }

    /// <summary>The second question: offers above Confirm Above Asking times what their house asks.</summary>
    private string HighOffers()
    {
        var names = new List<string>();
        foreach (DraftItem d in draft)
        {
            ListingView l = HousingUI.Listing(view, d.house);
            if (l != null && d.offer > l.askingMilli * (double)view.confirmAboveAsking)
                names.Add($"{HousingUI.HouseName(view, d.house)} ({PerDay(d.offer)}; it asks {PerDay(l.askingMilli)})");
        }
        if (names.Count == 0) return null;
        return $"You offer more than {HousingUI.Number(view.confirmAboveAsking)} times what it asks for {string.Join(", ", names)}. " +
            "If you win, you pay all you offered. Place it anyway?";
    }

    private void SendOrder(List<string> houses, List<int> terms, List<long> offers, long delta)
    {
        core.Send(new HousingRequest
        {
            kind = (byte)HousingRequestKind.PlaceOrder, houses = houses, terms = terms, offersMilli = offers, expected = delta,
        });
    }

    private void Withdraw()
    {
        if (view == null || view.orderHouses.Count == 0) return;
        if (HousingUI.Withdrawing(view))
        {
            core.Say(HousingUI.OrderUnderWay(view), false);
            return;
        }
        core.Ask(HousingUI.WithdrawQuestion(view), "Withdraw", () => core.Send(new HousingRequest { kind = (byte)HousingRequestKind.CancelOrder }), true);
    }
}
