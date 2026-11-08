using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "My home": each house the player rents - when its term ends, its rent, whether it renews by itself and how well it
/// is protected against higher offers, the renewal fixed a day before the term ends (and what is still to be deposited
/// for it), who they let in and letting in those next to them - then the places they may use, and in those they manage
/// (let in with the right to let others in) who they let in and letting in those next to them. Shown by the realtor's
/// window and by the player's own housing window (<see cref="MyHousingWindow"/>); it only asks, through its window. It is
/// made again only when what it shows changed: a view that brings nothing new leaves it as it is (a slider held, a
/// button selected with the keyboard or a gamepad).
/// </summary>
internal sealed class HousingHomePage
{
    private readonly IHousingHost host;
    private readonly RectTransform list;
    private readonly HousingSection section;
    // What the player set and has not applied yet (by property): kept while the page is made again.
    private readonly Dictionary<string, float> protection = new Dictionary<string, float>();
    private readonly Dictionary<string, bool> manage = new Dictionary<string, bool>();
    private readonly List<string> stale = new List<string>();
    private RealtorView view;
    private string note = "";

    public GameObject Root { get; }

    public HousingHomePage(IHousingHost host, RectTransform area)
    {
        this.host = host;
        Root = area.gameObject;
        Image bg = UITheme.Panel("My Home", area, UITheme.Card, UITheme.Radius);
        InventoryUIFactory.Stretch(bg.rectTransform);
        list = HousingUI.FillList(bg.transform, "Leases", 10, 10f);
        section = new HousingSection(list);
    }

    /// <summary>Shows <paramref name="v"/> (null: nothing came yet), with <paramref name="message"/> above it.</summary>
    public void Show(RealtorView v, string message = null)
    {
        view = v;
        note = message ?? "";
        Render();
    }

    /// <summary>Forgets what the player set and did not apply (the window closed).</summary>
    public void Reset()
    {
        protection.Clear();
        manage.Clear();
        view = null;
        note = "";
        section.Forget();
    }

    public void Answered(HousingRequestKind kind, HousingReply reply)
    {
        if (reply != null && reply.ok && kind == HousingRequestKind.SetRenewal) protection.Clear();
        // Made again with the next view, whatever it brings: a toggle the player turned shows the books' state again.
        section.Forget();
    }

    private void Render()
    {
        RealtorView v = view;
        if (v != null)
        {
            // What was set for a lease no longer held is forgotten.
            stale.Clear();
            foreach (string id in protection.Keys)
                if (!v.leases.Exists(l => l.propertyId == id)) stale.Add(id);
            foreach (string id in stale) protection.Remove(id);
        }
        section.Show(Signature(v), Build);
    }

    /// <summary>All the page shows, in one string (what the player set and did not apply is kept apart).</summary>
    private string Signature(RealtorView v)
    {
        var s = new HousingSig().Add(note).Add(v != null).Add(host.AtDesk);
        if (v == null) return s.ToString();
        double spd = v.secondsPerDay;
        bool game = v.gameTime;
        s.Add(v.account.currency).Add(v.account.balance).Add(v.defaultProtection).Add(v.maxProtection).Add(v.protectionMargin)
            .Add(v.protectionPremium).Add(v.weekDays).Add(v.monthDays);
        foreach (LeaseView l in v.leases)
            s.Add(l.propertyId).Add(l.name).Add(HousingUI.Duration(l.endsIn, spd, game)).Add(l.term).Add(l.rateMilli).Add(l.autoRenew)
                .Add(l.protection).Add(l.noticed).Add(l.renewRateMilli).Add(l.renewTotal).Add(l.reserved).Add(l.movingOut).Add(l.grants);
        s.Add(v.nearby).Add(HousingUI.PlacesSignature(v.account, v.nearby));
        return s.ToString();
    }

    private void Build(RectTransform root)
    {
        if (note.Length > 0) HousingUI.Note(root, note);
        RealtorView v = view;
        if (v == null)
        {
            if (note.Length == 0) HousingUI.Note(root, "Asking the housing office...");
            return;
        }
        if (v.leases.Count == 0)
            HousingUI.Note(root, "You rent no house. A realtor lets them: name the houses you want in an order, and at a clearing each house offered goes to the best offer for it.");
        foreach (LeaseView l in v.leases)
        {
            try
            {
                Lease(v, l);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        // Where the player manages a place (and is not its tenant): who they let in there, and letting in those next to them.
        HousingUI.Places(root, v.account, v.nearby, (p, n, a) => HousingUI.LetIn(host, p, n, a), (p, g) => HousingUI.LetOut(host, p, g));
    }

    private void Lease(RealtorView v, LeaseView l)
    {
        string cur = v.account.currency;
        string id = l.propertyId;
        string name = l.name.Length > 0 ? l.name : "Your house";
        double spd = v.secondsPerDay;
        bool game = v.gameTime;
        RectTransform card = HousingUI.Named(HousingUI.Card(list, 16, 8f), "Lease " + id);
        HousingUI.Text(card, name, 22f, UITheme.Gold, FontStyles.Bold);
        HousingUI.Text(card, $"This term ends in {HousingUI.Duration(l.endsIn, spd, game)}. Let by the {HousingUI.TermName(l.term)} at {HousingUI.PerDay(l.rateMilli, cur)}.", 16f);

        if (l.movingOut)
        {
            HousingUI.Text(card, "You are moving: this lease ends with its term.", 16f, UITheme.Bad);
        }
        else
        {
            Toggle renew = UITheme.Toggle(card, "Renew automatically", l.autoRenew);
            // Once the renewal is fixed (a day before the term ends) it may only be stopped, not started.
            renew.interactable = !(l.noticed && !l.autoRenew);
            renew.onValueChanged.AddListener(on => SetRenewal(v, l, renew, on));
            if (l.noticed) Fixed(v, l, card);
            else if (l.autoRenew) Protection(v, l, card);
            else HousingUI.Text(card, "It will not be renewed: it ends with this term.", 15f, UITheme.Bad);
        }

        HousingUI.SubHeader(card, "Who you let in");
        HousingUI.Grants(card, l.grants, g => host.Send(HousingUI.RevokeRequest(id, "", g)));

        HousingUI.SubHeader(card, "Let in");
        if (v.nearby.Count > 0)
        {
            bool on = manage.TryGetValue(id, out bool m) && m;
            Toggle t = UITheme.Toggle(card, "They may let others in", on);
            t.onValueChanged.AddListener(x => manage[id] = x);
        }
        HousingUI.Nearby(card, v.nearby,
            ("Friend", n => Grant(l, n, HousingAccess.Friend)),
            ("Garden only", n => Grant(l, n, HousingAccess.Garden)),
            ("Visitor", n => Grant(l, n, HousingAccess.Enter)));
    }

    /// <summary>A day before the term ends the renewal is fixed: its price, what is set aside, what is still missing.</summary>
    private void Fixed(RealtorView v, LeaseView l, RectTransform card)
    {
        string cur = v.account.currency;
        if (!l.autoRenew || l.renewTotal <= 0)
        {
            HousingUI.Text(card, "It will not be renewed: it ends with this term.", 15f, UITheme.Bad);
            return;
        }
        HousingUI.Text(card, $"Renews at the end of this term for {HousingUI.Money(l.renewTotal, cur)}: {HousingUI.Money(l.reserved, cur)} set aside.", 16f);
        float p = Mathf.Clamp(l.protection > 0f ? l.protection : v.defaultProtection, 1f, v.maxProtection);
        HousingUI.Text(card, $"The renewal is fixed (protection {HousingUI.Times(p)}). {ProtectionWords(v, l, p)}", 14f, UITheme.Muted);
        long missing = l.renewTotal - l.reserved;
        if (missing <= 0)
        {
            HousingUI.Text(card, "It is paid: nothing more is needed.", 15f, UITheme.Good);
            return;
        }
        // At the clearing the rest is taken from the rent account first.
        long fromAccount = Math.Min(Math.Max(0L, v.account.balance), missing);
        long toDeposit = missing - fromAccount;
        if (fromAccount > 0) HousingUI.Text(card, $"{HousingUI.Money(fromAccount, cur)} more comes from your rent account at the clearing.", 15f);
        if (toDeposit <= 0) return;
        RectTransform row = HousingUI.Line(card);
        HousingUI.Flex(HousingUI.Text(row, $"Deposit {HousingUI.Money(toDeposit, cur)} more, or the lease ends with this term.", 15f, UITheme.Bad));
        Button b = HousingUI.Small(row, "Deposit " + HousingUI.Money(toDeposit, cur), () =>
            host.Send(new HousingRequest { kind = (byte)HousingRequestKind.Deposit, amount = toDeposit }), 200f, UITheme.ButtonStyle.Primary);
        b.interactable = host.AtDesk;
        if (!host.AtDesk) HousingUI.Note(card, "Deposits are made at a realtor or an innkeeper.");
    }

    /// <summary>The protection slider (1 to the most, by tenths), what it means in money, and Apply.</summary>
    private void Protection(RealtorView v, LeaseView l, RectTransform card)
    {
        string id = l.propertyId;
        float current = Mathf.Clamp(l.protection > 0f ? l.protection : v.defaultProtection, 1f, v.maxProtection);
        float shown = protection.TryGetValue(id, out float p) ? p : current;
        if (Mathf.Abs(shown - current) < 0.001f) protection.Remove(id);
        Slider slider = UITheme.Slider(card, "Protection", 1f, v.maxProtection, shown, false, out TextMeshProUGUI value);
        TextMeshProUGUI meaning = HousingUI.Text(card, "", 15f);
        TextMeshProUGUI next = HousingUI.Text(card, "", 14f, UITheme.Muted);
        RectTransform row = HousingUI.Line(card);
        HousingUI.Flex(HousingUI.Text(row, "A higher protection keeps the house from higher offers, for a little more rent.", 14f, UITheme.Muted));
        Button apply = HousingUI.Small(row, "Apply", null, 110f, UITheme.ButtonStyle.Primary);
        void UpdateTexts(float x)
        {
            value.text = HousingUI.Times(x);
            meaning.text = HousingUI.Clean(ProtectionWords(v, l, x));
            next.text = HousingUI.Clean(NextTerm(v, l, x));
            apply.interactable = Mathf.Abs(x - current) > 0.001f;
        }
        slider.onValueChanged.AddListener(x =>
        {
            float snapped = Mathf.Clamp(Mathf.Round(x * 10f) / 10f, 1f, v.maxProtection);
            if (Mathf.Abs(snapped - x) > 1e-4f) slider.SetValueWithoutNotify(snapped);
            if (Mathf.Abs(snapped - current) < 0.001f) protection.Remove(id);
            else protection[id] = snapped;
            UpdateTexts(snapped);
        });
        apply.onClick.AddListener(() =>
        {
            float x = Mathf.Clamp(Mathf.Round(slider.value * 10f) / 10f, 1f, v.maxProtection);
            host.Send(new HousingRequest { kind = (byte)HousingRequestKind.SetRenewal, placeId = id, flag = true, protection = x });
        });
        UpdateTexts(shown);
    }

    /// <summary>"Someone must offer more than 27.17 g a day to take it; it costs you 0.62 g a day more." (the books' own sums).</summary>
    private static string ProtectionWords(RealtorView v, LeaseView l, float p)
    {
        string cur = v.account.currency;
        long rate = Math.Max(1L, l.renewRateMilli > 0 ? l.renewRateMilli : l.rateMilli);
        long held = HousingTenure.ProtectionMilli(rate, p);
        long beat = HousingTenure.BeatMilli(held, v.protectionMargin);
        long premium = (long)Math.Ceiling(HousingTenure.PremiumMilli(rate, held, v.protectionPremium) - 1e-9);
        string cost = premium > 0 ? $"it costs you {HousingUI.PerDay(premium, cur)} more" : "it costs you nothing more";
        return $"Someone must offer more than {HousingUI.PerDay(beat, cur)} to take it; {cost}.";
    }

    private static string NextTerm(RealtorView v, LeaseView l, float p)
    {
        string cur = v.account.currency;
        long rate = Math.Max(1L, l.renewRateMilli > 0 ? l.renewRateMilli : l.rateMilli);
        long held = HousingTenure.ProtectionMilli(rate, p);
        int days = HousingUI.TermDays(v, l.term);
        long total = HousingTenure.RenewTotal(rate, held, days, v.protectionPremium);
        return $"The next {HousingUI.TermName(l.term)} would cost about {HousingUI.Money(total, cur)} (rent now {HousingUI.PerDay(rate, cur)}), paid from your rent account.";
    }

    private void SetRenewal(RealtorView v, LeaseView l, Toggle toggle, bool on)
    {
        float p = Mathf.Clamp(l.protection > 0f ? l.protection : v.defaultProtection, 1f, v.maxProtection);
        var r = new HousingRequest { kind = (byte)HousingRequestKind.SetRenewal, placeId = l.propertyId, flag = on, protection = p };
        if (on)
        {
            host.Send(r);
            return;
        }
        string name = l.name.Length > 0 ? l.name : "your house";
        host.Ask($"Stop renewing {name}? Your lease ends with this term (in {HousingUI.Duration(l.endsIn, v.secondsPerDay, v.gameTime)}) and the house comes free: it is offered to everyone a day later.",
            "Stop renewing", () => host.Send(r), true, () =>
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(true);
            });
    }

    private void Grant(LeaseView l, NearbyView n, HousingAccess access)
    {
        string id = l.propertyId;
        if (manage.TryGetValue(id, out bool m) && m) access |= HousingAccess.Manage;
        HousingRequest r = HousingUI.GrantRequest(id, "", n, access);
        if (access == HousingAccess.Enter)
        {
            host.Send(r);
            return;
        }
        string name = l.name.Length > 0 ? l.name : "your house";
        host.Ask($"Let {HousingUI.Who(n.name, n.fingerprint)} into {name}? They may {HousingService.Describe(access)}.", "Let them in", () => host.Send(r));
    }
}

/// <summary>
/// "Account": the rent account (what is in it, what is set aside for renewals, the most it may hold) and the purse,
/// deposits and withdrawals, money owed to the player, their messages, what is kept for them, their order (where the
/// window has no order tab: with a change waiting for the order notice, or its withdrawal under way) and each district's
/// usual rents. Deposits, withdrawals and claims need a realtor or an innkeeper (the page says so elsewhere). The figures
/// are updated in place and the lists made again only when what they show changed, so what is typed stays.
/// </summary>
internal sealed class HousingAccountPage
{
    private readonly IHousingHost host;
    private readonly bool showOrder;
    private readonly TextMeshProUGUI balanceText, reservedText, capText, walletText, roomText, deskText;
    private readonly TMP_InputField amount;
    private readonly Button depositButton, withdrawButton;
    private readonly HousingSection section;
    private AccountView account;
    private RealtorView realtor;
    private double secondsPerDay = 86400.0;
    private bool gameTime;
    private string waiting = "";

    public GameObject Root { get; }

    public HousingAccountPage(IHousingHost host, RectTransform area, bool showOrder)
    {
        this.host = host;
        this.showOrder = showOrder;
        Root = area.gameObject;

        Image top = HousingUI.Panel(area, "Rent Account", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -184f), new Vector2(0f, 0f));
        RectTransform col = UITheme.Column("Column", top.transform, 16, 8f);
        InventoryUIFactory.Stretch(col);
        RectTransform stats = UITheme.Row("Figures", col, 56f, 12f);
        stats.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        balanceText = Figure(stats, "Rent account");
        reservedText = Figure(stats, "Set aside");
        capText = Figure(stats, "It may hold");
        walletText = Figure(stats, "Your purse");
        RectTransform row = UITheme.Row("Amount", col, 44f, 10f);
        amount = AmountField(row, 200f, 42f);
        depositButton = UITheme.Button("Deposit", row, "Deposit", () => Move(host, amount, true), UITheme.ButtonStyle.Primary, 150f, 42f);
        withdrawButton = UITheme.Button("Withdraw", row, "Withdraw", () => Move(host, amount, false), UITheme.ButtonStyle.Normal, 150f, 42f);
        deskText = HousingUI.Flex(HousingUI.Text(row, "", 14f, UITheme.Muted));
        roomText = HousingUI.Text(col, "", 14f, UITheme.Muted);

        Image rest = HousingUI.Panel(area, "Account Lists", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -196f));
        section = new HousingSection(HousingUI.FillList(rest.transform, "Account", 10, 10f));
    }

    private static TextMeshProUGUI Figure(Transform row, string label)
    {
        RectTransform c = UITheme.Column(label, row, 0, 2f);
        HousingUI.Text(c, label, 13f, UITheme.Muted);
        TextMeshProUGUI v = HousingUI.Text(c, "-", 21f, UITheme.Text, FontStyles.Bold);
        v.textWrappingMode = TextWrappingModes.NoWrap;
        v.overflowMode = TextOverflowModes.Ellipsis;
        return v;
    }

    /// <summary>
    /// A field for an amount of money: digits, and '.', ',' or spaces between them, whatever the system's language (read
    /// by <see cref="HousingUI.ParseAmount(string, out long, out string)"/>, which refuses what it cannot read for sure).
    /// </summary>
    internal static TMP_InputField AmountField(Transform row, float width, float height)
    {
        TMP_InputField f = UITheme.Input("Amount", row, "Amount", 14, height);
        f.contentType = TMP_InputField.ContentType.DecimalNumber;
        f.onValidateInput = HousingUI.NumberChar;
        InventoryUIFactory.Size(f, width, height, 0f);
        return f;
    }

    /// <summary>Deposits (or withdraws) the amount typed in <paramref name="field"/>, at a realtor or an innkeeper.</summary>
    internal static void Move(IHousingHost host, TMP_InputField field, bool deposit)
    {
        if (host == null) return;
        if (!host.AtDesk)
        {
            host.Say(deposit ? "Deposit at a realtor or an innkeeper." : "Withdraw at a realtor or an innkeeper.", false);
            return;
        }
        if (!HousingUI.ParseAmount(field != null ? field.text : "", out long a, out string problem))
        {
            host.Say(problem, false);
            return;
        }
        host.Send(new HousingRequest { kind = (byte)(deposit ? HousingRequestKind.Deposit : HousingRequestKind.Withdraw), amount = a });
    }

    /// <summary>
    /// Shows the account (and, from a realtor's view, the order and the districts); <paramref name="waitingNote"/> is said
    /// while there is no account to show (why the housing office does not answer).
    /// </summary>
    public void Show(AccountView a, RealtorView v, double spd, bool game, string waitingNote = null)
    {
        account = a;
        realtor = v;
        secondsPerDay = spd;
        gameTime = game;
        waiting = waitingNote ?? "";
        Render();
    }

    public void Reset()
    {
        account = null;
        realtor = null;
        waiting = "";
        if (amount != null) amount.SetTextWithoutNotify("");
        section.Forget();
    }

    public void Answered(HousingRequestKind kind, HousingReply reply)
    {
        if (reply != null && reply.ok && (kind == HousingRequestKind.Deposit || kind == HousingRequestKind.Withdraw) && amount != null)
            amount.SetTextWithoutNotify("");
        section.Forget();
    }

    private void Render()
    {
        AccountView a = account;
        bool desk = host.AtDesk;
        depositButton.interactable = desk && a != null;
        withdrawButton.interactable = desk && a != null && a.balance > 0;
        HousingUI.Set(deskText, desk ? "" : "Deposits and withdrawals are made at a realtor or an innkeeper.");
        if (a == null)
        {
            HousingUI.Set(balanceText, "-");
            HousingUI.Set(reservedText, "-");
            HousingUI.Set(capText, "-");
            HousingUI.Set(walletText, "-");
            HousingUI.Set(roomText, "");
        }
        else
        {
            string cur = a.currency;
            HousingUI.Set(balanceText, HousingUI.Money(a.balance, cur));
            HousingUI.Set(reservedText, HousingUI.Money(a.reserved, cur));
            HousingUI.Set(capText, HousingUI.Money(a.cap, cur));
            HousingUI.Set(walletText, HousingUI.Money(a.wallet, cur));
            long room = Math.Max(0L, a.cap - a.balance - a.reserved);
            HousingUI.Set(roomText, a.cap <= 0
                ? "A rent account is for the rent of a house you have: renewals are paid from it."
                : $"Renewals are paid from it. You may deposit {HousingUI.Money(room, cur)} more (it holds a few terms of rent at most).");
        }
        section.Show(Signature(), Build);
    }

    /// <summary>What the lists show, in one string.</summary>
    private string Signature()
    {
        AccountView a = account;
        bool desk = host.AtDesk;
        var s = new HousingSig().Add(waiting).Add(a != null).Add(desk);
        if (a != null)
            s.Add(a.currency).Add(a.owed).Add(HousingUI.MessagesSignature(a, secondsPerDay, gameTime)).Add(HousingUI.HeldSignature(a, secondsPerDay, gameTime, desk));
        RealtorView v = realtor;
        if (v == null) return s.ToString();
        if (showOrder)
        {
            s.Add(HousingUI.Duration(v.nextClearingIn, v.secondsPerDay, v.gameTime)).Add(v.orderEscrow).Add(HousingUI.OrderUnderWay(v))
                .Add(HousingUI.Duration(v.orderNotice, v.secondsPerDay, v.gameTime));
            Items(s, v, v.orderHouses, v.orderTerms, v.orderOffersMilli);
            Items(s, v, v.orderPendingHouses, v.orderPendingTerms, v.orderPendingOffersMilli);
        }
        int n = Math.Min(v.districts.Count, v.districtLevels.Count);
        for (int i = 0; i < n; i++) s.Add(v.districts[i]).Add(HousingUI.Times(v.districtLevels[i]));
        return s.ToString();
    }

    private static void Items(HousingSig s, RealtorView v, List<string> houses, List<int> terms, List<long> offers)
    {
        int n = Math.Min(houses.Count, Math.Min(terms.Count, offers.Count));
        s.Add(n);
        for (int i = 0; i < n; i++) s.Add(HousingUI.HouseName(v, houses[i])).Add(terms[i]).Add(offers[i]);
    }

    private void Build(RectTransform list)
    {
        AccountView a = account;
        if (a == null)
        {
            HousingUI.Note(list, waiting.Length > 0 ? waiting : "Asking the housing office...");
            return;
        }
        bool desk = host.AtDesk;
        string cur = a.currency;
        if (a.owed > 0)
        {
            RectTransform owed = HousingUI.Card(list);
            HousingUI.SectionHeader(owed, "Owed to you");
            HousingUI.Text(owed, $"{HousingUI.Money(a.owed, cur)} is owed to you (deposits given back): it goes into your purse when it has room.", 15f);
        }
        HousingUI.Messages(list, a, secondsPerDay, gameTime, () => host.Send(new HousingRequest { kind = (byte)HousingRequestKind.ReadNotices }));
        HousingUI.Held(list, a, secondsPerDay, gameTime, Claim, desk);
        if (showOrder && realtor != null) Order(list, realtor);
        if (realtor != null) Districts(list, realtor);
    }

    private void Claim(HeldView h)
    {
        if (!host.AtDesk)
        {
            host.Say("Claim it at a realtor or an innkeeper.", false);
            return;
        }
        HousingUI.Claim(host, h, account != null ? account.currency : "");
    }

    /// <summary>
    /// The order that stands (the window has no order tab): its houses, its deposit, a change waiting for the order
    /// notice or its withdrawal under way, and Withdraw.
    /// </summary>
    private void Order(Transform list, RealtorView v)
    {
        RectTransform card = HousingUI.Named(HousingUI.Card(list), "Your Order");
        int n = Math.Min(v.orderHouses.Count, Math.Min(v.orderTerms.Count, v.orderOffersMilli.Count));
        string cur = v.account.currency;
        bool withdrawing = HousingUI.Withdrawing(v);
        HousingUI.SectionHeader(card, "Your order", n > 0 ? "Withdraw" : null, () =>
            host.Ask(HousingUI.WithdrawQuestion(v), "Withdraw", () => host.Send(new HousingRequest { kind = (byte)HousingRequestKind.CancelOrder }), true),
            !withdrawing);
        if (n == 0)
        {
            HousingUI.Note(card, "You have no order. A realtor takes orders for houses.");
            return;
        }
        for (int i = 0; i < n; i++)
            HousingUI.Text(card, $"{i + 1}. {HousingUI.HouseName(v, v.orderHouses[i])} - {HousingUI.PerDay(v.orderOffersMilli[i], cur)} by the {HousingUI.TermName(v.orderTerms[i])}", 15f);
        string underWay = HousingUI.OrderUnderWay(v);
        if (underWay.Length > 0) HousingUI.Text(card, underWay, 14f, withdrawing ? UITheme.Bad : UITheme.Gold);
        if (HousingUI.ChangeWaiting(v))
        {
            HousingUI.SubHeader(card, "Then it will be");
            int m = Math.Min(v.orderPendingHouses.Count, Math.Min(v.orderPendingTerms.Count, v.orderPendingOffersMilli.Count));
            for (int i = 0; i < m; i++)
                HousingUI.Text(card, $"{i + 1}. {HousingUI.HouseName(v, v.orderPendingHouses[i])} - {HousingUI.PerDay(v.orderPendingOffersMilli[i], cur)} by the {HousingUI.TermName(v.orderPendingTerms[i])}", 15f);
        }
        HousingUI.Text(card, $"Its deposit: {HousingUI.Money(v.orderEscrow, cur)}. Next clearing in {HousingUI.Duration(v.nextClearingIn, v.secondsPerDay, v.gameTime)}.", 14f, UITheme.Muted);
        HousingUI.Note(card, $"Change it at a realtor. Adding a house or offering more takes effect at once; anything that takes something back waits about {HousingUI.Duration(v.orderNotice, v.secondsPerDay, v.gameTime)}.");
    }

    /// <summary>"Old Town: houses usually ask x1.2 of their sizes' worth" (the median of its houses).</summary>
    private static void Districts(Transform list, RealtorView v)
    {
        int n = Math.Min(v.districts.Count, v.districtLevels.Count);
        if (n == 0) return;
        RectTransform card = HousingUI.Card(list);
        HousingUI.SectionHeader(card, "Usual rents by district");
        for (int i = 0; i < n; i++)
        {
            string d = string.IsNullOrWhiteSpace(v.districts[i]) ? "The city" : v.districts[i].Trim();
            HousingUI.Text(card, $"{d}: houses usually ask {HousingUI.Times(v.districtLevels[i])} of their sizes' worth", 15f);
        }
        HousingUI.Note(card, "Each house asks its own rent: it follows the offers made for it.");
    }
}

/// <summary>
/// The rent account in brief, for the innkeeper's window (deposits and withdrawals are made at an innkeeper too): what is
/// in it, what is set aside for renewals, the most it may hold, an amount, Deposit and Withdraw. Made once and updated in
/// place, so what is typed stays while views come; hidden while the player has no rent account to speak of.
/// </summary>
internal sealed class HousingAccountCard
{
    private readonly IHousingHost host;
    private readonly TextMeshProUGUI figures, room;
    private readonly TMP_InputField amount;
    private readonly Button depositButton, withdrawButton;

    public GameObject Root { get; }

    public HousingAccountCard(IHousingHost host, Transform parent)
    {
        this.host = host;
        RectTransform card = HousingUI.Card(parent, 16, 8f);
        Root = card.gameObject;
        HousingUI.SectionHeader(card, "Rent account");
        figures = HousingUI.Text(card, "", 15f);
        RectTransform row = HousingUI.Line(card, 8f, 40f);
        amount = HousingAccountPage.AmountField(row, 150f, 38f);
        UITheme.Spacer(row);
        depositButton = HousingUI.Small(row, "Deposit", () => HousingAccountPage.Move(host, amount, true), 110f, UITheme.ButtonStyle.Primary);
        withdrawButton = HousingUI.Small(row, "Withdraw", () => HousingAccountPage.Move(host, amount, false), 110f);
        room = HousingUI.Text(card, "", 13f, UITheme.Muted);
        Root.SetActive(false);
    }

    /// <summary>Shows <paramref name="a"/> (null: hidden); what is typed stays.</summary>
    public void Show(AccountView a)
    {
        bool any = a != null && (a.cap > 0 || a.balance > 0 || a.reserved > 0);
        if (Root.activeSelf != any) Root.SetActive(any);
        if (!any) return;
        string cur = a.currency;
        HousingUI.Set(figures, $"In it: {HousingUI.Money(a.balance, cur)}. Set aside for renewals: {HousingUI.Money(a.reserved, cur)}. It may hold {HousingUI.Money(a.cap, cur)}.");
        long left = Math.Max(0L, a.cap - a.balance - a.reserved);
        HousingUI.Set(room, $"The renewals of your houses are paid from it. You may deposit {HousingUI.Money(left, cur)} more.");
        depositButton.interactable = host.AtDesk;
        withdrawButton.interactable = host.AtDesk && a.balance > 0;
    }

    /// <summary>Forgets what was typed (the window closed).</summary>
    public void Reset()
    {
        if (amount != null) amount.SetTextWithoutNotify("");
    }

    public void Answered(HousingRequestKind kind, HousingReply reply)
    {
        if (reply != null && reply.ok && (kind == HousingRequestKind.Deposit || kind == HousingRequestKind.Withdraw)) Reset();
    }
}
