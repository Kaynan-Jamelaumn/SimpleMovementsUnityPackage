using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The innkeeper's window (built from code, one per canvas): the inn, how busy it has been and so what a night costs,
/// its rooms (free or taken, a night's price), a stay of a few nights in the chosen room or the cheapest free one; the
/// player's own room here - staying longer, giving it up, who they let in, letting in those next to them - their rent
/// account (deposits and withdrawals are made at an innkeeper too), messages, what is kept for them, the places they
/// manage (who they let in there, letting in those next to them) and their purse. It changes nothing itself: everything
/// is asked of the housing office (<see cref="HousingClient"/>), one request at a time, and shown as the answers and views
/// come back. A part of the window is made again only when what it shows changed (what is typed or selected stays);
/// while the housing office cannot answer, the window says why instead of showing an empty inn.
/// </summary>
[DisallowMultipleComponent]
public class InnWindow : NPCWindow, IHousingHost
{
    private Innkeeper keeper;
    private CombatEntity player;
    private InnView view;
    /// <summary>Why the housing office cannot answer (empty: it can); there is no view meanwhile.</summary>
    private string closed = "";
    private bool viewDirty;
    private string chosenRoom = "";
    /// <summary>Those let into the player's room may let others in (the guest's choice, before Let in).</summary>
    private bool manageRoom;
    private int nights = 1, more = 1;
    private HousingWindowCore core;
    private float nextFit;

    private TextMeshProUGUI title, subtitle, purse, freeLine, nightsLabel, totalLabel, rentNote;
    private RectTransform roomList, side;
    private HousingSection roomsSection, mineSection, restSection;
    private HousingAccountCard accountCard;
    private Button rentButton, fewerButton, moreButton;

    public override string PanelName => view != null && view.name.Length > 0 ? "Inn: " + view.name : "Inn";

    CombatEntity IHousingHost.Player => player;
    bool IHousingHost.AtDesk => true;
    void IHousingHost.Send(HousingRequest request) => core.Send(request);
    void IHousingHost.Ask(string text, string yes, Action confirmed, bool danger, Action cancelled) => core.Ask(text, yes, confirmed, danger, cancelled);
    void IHousingHost.Say(string text, bool ok) => core.Say(text, ok);

    // ------------------------------------------------------------------ opening and closing
    /// <summary>Opens the inn of <paramref name="keeper"/> for <paramref name="session"/> (on the player's canvas).</summary>
    public static InnWindow Open(Innkeeper keeper, NPCInteractionSession session)
    {
        if (keeper == null || session == null) return null;
        InnWindow w = NPCWindowCache.Get<InnWindow>(session.Canvas, null, null, Create);
        w.Show(keeper, session);
        return w;
    }

    /// <summary>Closes the inn window shown for <paramref name="session"/>.</summary>
    public static void CloseFor(Innkeeper keeper, NPCInteractionSession session)
    {
        if (session == null) return;
        InnWindow w = NPCWindowCache.TryGet<InnWindow>(session.Canvas, null, null);
        if (w != null && ReferenceEquals(w.Session, session)) w.Unbind();
    }

    private static InnWindow Create(Transform parent)
    {
        Image win = UITheme.Window("Inn Window", parent, HousingUI.WindowSize);
        InnWindow w = win.gameObject.AddComponent<InnWindow>();
        w.Build(win.transform);
        return w;
    }

    private void Show(Innkeeper innkeeper, NPCInteractionSession session)
    {
        Bind(session); // a window still showing another session lets it go first (OnUnbound)
        if (!ReferenceEquals(Session, session)) return;
        keeper = innkeeper;
        player = CombatEntity.Resolve(session.Player);
        view = null;
        closed = "";
        viewDirty = false;
        chosenRoom = "";
        manageRoom = false;
        nights = more = 1;
        accountCard.Reset();
        HousingClient.InnViewArrived -= OnView;
        HousingClient.InnViewArrived += OnView;
        HousingUI.FitToCanvas((RectTransform)transform, HousingUI.WindowSize);
        Inn inn = innkeeper.InnServed;
        HousingUI.Set(title, inn != null ? inn.DisplayName : "Inn");
        Render();
        core.Begin(player); // asks for the view (where the rules run it comes at once)
        if (player == null) core.Say("Nobody to rent a room for.", false);
    }

    protected override void OnUnbound()
    {
        HousingClient.InnViewArrived -= OnView;
        if (core != null) core.End();
        view = null;
        closed = "";
        viewDirty = false;
        keeper = null;
        player = null;
    }

    /// <summary>Close button / Escape: closes a question first, else the inn (back to the NPC's menu, or the end).</summary>
    public override void RequestClose()
    {
        if (core != null && core.Asking)
        {
            core.CloseConfirm();
            return;
        }
        NPCInteractionSession s = Session;
        if (s != null && keeper != null) s.BehaviourFinished(keeper);
        if (s == null || ReferenceEquals(Session, s)) base.RequestClose();
    }

    protected override void Update()
    {
        base.Update();
        if (Session == null) return;
        if (keeper == null || !keeper.isActiveAndEnabled)
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
        HousingClient.InnViewArrived -= OnView;
        if (core != null) core.End();
        base.OnDestroy();
    }

    private void OnView(InnView v)
    {
        if (Session == null || v == null) return;
        core.Viewed();
        viewDirty = true;
        if (!string.IsNullOrEmpty(v.closed))
        {
            // The housing office cannot answer: nothing in the view is real.
            closed = HousingUI.Clean(v.closed).Trim();
            if (closed.Length == 0) closed = "The housing office is closed.";
            view = null;
            return;
        }
        closed = "";
        view = HousingUI.Fix(v);
    }

    /// <summary>What the window says while it has no view: why the housing office does not answer, or that it is asked.</summary>
    private string Waiting => closed.Length > 0 ? closed : "Asking the innkeeper...";

    private void OnAnswered(HousingRequestKind kind, HousingReply reply)
    {
        accountCard.Answered(kind, reply);
        // Made again with the next view, whatever it brings (a toggle or a stepper shows the books' state again).
        roomsSection.Forget();
        mineSection.Forget();
        restSection.Forget();
        if (reply == null || !reply.ok) return;
        if (kind == HousingRequestKind.RentRoom)
        {
            chosenRoom = "";
            nights = 1;
        }
        else if (kind == HousingRequestKind.ExtendStay)
        {
            more = 1;
        }
    }

    // ------------------------------------------------------------------ building
    private void Build(Transform w)
    {
        HousingUI.Header(w, RequestClose, out title, out subtitle, out purse);

        // Left: the rooms, and a stay.
        Image left = HousingUI.Panel(w, "Rooms", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 60f), new Vector2(584f, -100f));
        RectTransform col = UITheme.Column("Column", left.transform, 14, 8f);
        InventoryUIFactory.Stretch(col);
        UITheme.Header(col, "Rooms");
        roomList = UITheme.ScrollList("Room List", col, 240f, out _, null, 6, 6f);
        roomsSection = new HousingSection(roomList);
        freeLine = HousingUI.Text(col, "", 14f, UITheme.Muted);
        UITheme.Header(col, "Take a room");
        RectTransform stepper = UITheme.Row("Nights", col, 40f, 8f);
        fewerButton = HousingUI.Small(stepper, "-", () => SetNights(nights - 1), 48f, UITheme.ButtonStyle.Normal, 38f);
        nightsLabel = HousingUI.Flex(HousingUI.Text(stepper, "1 night", 18f, UITheme.Text, FontStyles.Bold, TextAlignmentOptions.Center));
        moreButton = HousingUI.Small(stepper, "+", () => SetNights(nights + 1), 48f, UITheme.ButtonStyle.Normal, 38f);
        totalLabel = HousingUI.Text(col, "", 16f);
        rentButton = UITheme.Button("Rent", col, "Rent a room", Rent, UITheme.ButtonStyle.Primary, 0f, 46f);
        rentNote = HousingUI.Text(col, "", 14f, UITheme.Muted);

        // Right: the player's room, their messages, what is kept for them.
        Image right = HousingUI.Panel(w, "Yours", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(600f, 60f), new Vector2(-24f, -100f));
        side = HousingUI.FillList(right.transform, "Your Things", 10, 10f);
        mineSection = HousingSection.Under(side, "Your Room");
        accountCard = new HousingAccountCard(this, side);
        restSection = HousingSection.Under(side, "Messages And Places");

        TextMeshProUGUI status = HousingUI.StatusLine(w);
        HousingConfirm confirm = HousingUI.Confirm(w);
        core = new HousingWindowCore(status, confirm) { Answered = OnAnswered, HasView = () => view != null };
    }

    // ------------------------------------------------------------------ showing
    private void Render()
    {
        try
        {
            RenderHeader();
            RenderRooms();
            RenderStay();
            RenderSide();
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    private string Currency => view != null ? view.account.currency : "";

    private string Money(long amount) => HousingUI.Money(amount, Currency);

    private string Dur(double days) => view != null ? HousingUI.Duration(days, view.secondsPerDay, view.gameTime) : "";

    private void RenderHeader()
    {
        if (view == null)
        {
            HousingUI.Set(subtitle, Waiting);
            HousingUI.Set(purse, "");
            return;
        }
        if (view.name.Length > 0) HousingUI.Set(title, view.name);
        string where = view.district.Trim().Length > 0 ? view.district.Trim() + " - " : "";
        HousingUI.Set(subtitle, where + HousingUI.Capital(HousingUI.InnLevel(view.level)) + " - " + HousingUI.Percent(view.occupancy) + " full lately");
        HousingUI.Set(purse, "Purse: " + Money(view.account.wallet));
    }

    private InnRoomView Room(string id)
    {
        if (view == null || string.IsNullOrEmpty(id)) return null;
        foreach (InnRoomView r in view.rooms)
            if (r.id == id) return r;
        return null;
    }

    /// <summary>The cheapest free room (the first of the cheapest, as the innkeeper picks it).</summary>
    private InnRoomView Cheapest()
    {
        InnRoomView best = null;
        if (view == null) return null;
        foreach (InnRoomView r in view.rooms)
            if (r.free && r.nightly > 0 && r.id.Length > 0 && (best == null || r.nightly < best.nightly)) best = r;
        return best;
    }

    /// <summary>The room a stay would be in: the chosen free room, else the cheapest.</summary>
    private InnRoomView Chosen()
    {
        InnRoomView r = Room(chosenRoom);
        return r != null && r.free && r.nightly > 0 ? r : Cheapest();
    }

    private static string RoomName(InnRoomView r) => r != null && r.name.Trim().Length > 0 ? r.name.Trim() : "a room";

    private string MyRoomName()
    {
        if (view == null) return "your room";
        if (view.myRoomName.Trim().Length > 0) return view.myRoomName.Trim();
        InnRoomView r = Room(view.myRoomId);
        return r != null ? RoomName(r) : "your room";
    }

    /// <summary>The rooms (made again only when what they show changed).</summary>
    private void RenderRooms()
    {
        var sig = new HousingSig().Add(view == null ? Waiting : "");
        if (view != null)
        {
            InnRoomView chosen = Room(chosenRoom);
            if (chosen == null || !chosen.free) chosenRoom = "";
            sig.Add(chosenRoom).Add(view.myRoomId).Add(Currency).Add(view.rooms.Count);
            foreach (InnRoomView r in view.rooms) sig.Add(r.id).Add(r.name).Add(r.quality > 1.15f).Add(r.free).Add(r.nightly);
        }
        roomsSection.Show(sig.ToString(), BuildRooms);
    }

    private void BuildRooms(RectTransform list)
    {
        if (view == null)
        {
            HousingUI.Note(list, Waiting);
            return;
        }
        if (view.rooms.Count == 0)
        {
            HousingUI.Note(list, "This inn has no rooms to let.");
            return;
        }
        foreach (InnRoomView r in view.rooms)
        {
            string id = r.id;
            bool mine = id.Length > 0 && id == view.myRoomId;
            RectTransform row = HousingUI.Named(HousingUI.ClickCard(list, () => ChooseRoom(id), out Button b, true, 46f), "Room " + id);
            string name = r.name.Trim().Length > 0 ? r.name.Trim() : "A room";
            HousingUI.Flex(HousingUI.Text(row, r.quality > 1.15f ? name + "  (fine room)" : name, 17f, UITheme.Text, FontStyles.Bold));
            string state = mine ? "yours" : r.free ? "free - " + Money(r.nightly) + " a night" : "taken";
            HousingUI.Text(row, state, 15f, mine ? UITheme.Gold : r.free ? UITheme.Good : UITheme.Muted, FontStyles.Normal, TextAlignmentOptions.Right);
            b.interactable = r.free && id.Length > 0;
            UITheme.SetSelected(b, r.free && id == chosenRoom);
        }
    }

    private void ChooseRoom(string id)
    {
        InnRoomView r = Room(id);
        if (r == null || !r.free) return;
        chosenRoom = chosenRoom == id ? "" : id;
        RenderRooms();
        RenderStay();
    }

    private void SetNights(int n)
    {
        nights = Mathf.Clamp(n, 1, view != null ? view.maxNights : 1);
        RenderStay();
    }

    private void RenderStay()
    {
        int max = view != null ? view.maxNights : 1;
        nights = Mathf.Clamp(nights, 1, max);
        HousingUI.Set(nightsLabel, HousingUI.Nights(nights));
        fewerButton.interactable = nights > 1;
        moreButton.interactable = nights < max;
        if (view == null)
        {
            HousingUI.Set(freeLine, "");
            HousingUI.Set(totalLabel, "");
            HousingUI.Set(rentNote, "");
            HousingUI.SetLabel(rentButton, "Rent a room");
            rentButton.interactable = false;
            return;
        }
        bool anyFree = Cheapest() != null;
        bool haveRoom = view.myRoomId.Length > 0;
        InnRoomView room = Chosen();
        if (!anyFree)
            HousingUI.Set(freeLine, view.nextFreeIn >= 0.0 ? "Every room is taken. A room frees up in " + Dur(view.nextFreeIn) + "." : "Every room is taken.");
        else
            HousingUI.Set(freeLine, chosenRoom.Length == 0 ? "Choose a room, or take the cheapest free one." : "");
        HousingUI.Set(totalLabel, room != null
            ? $"{HousingUI.Capital(RoomName(room))}: {Money(room.nightly)} a night - {Money(room.nightly * nights)} for {HousingUI.Nights(nights)}."
            : "");
        HousingUI.SetLabel(rentButton, room != null ? "Rent " + RoomName(room) : "Rent a room");
        rentButton.interactable = room != null && !haveRoom && player != null;
        HousingUI.Set(rentNote, haveRoom ? "You have a room here already: stay longer instead." : "Paid now; nothing is paid back if you leave early.");
    }

    /// <summary>
    /// The player's side: their room here, their rent account (kept: what is typed stays), then their messages, what is
    /// kept for them and the places they manage - each part made again only when what it shows changed.
    /// </summary>
    private void RenderSide()
    {
        bool hasRoom = view != null && view.myRoomId.Length > 0;
        var mine = new HousingSig().Add(hasRoom);
        if (hasRoom)
            mine.Add(MyRoomName()).Add(Dur(view.myEndsIn)).Add(view.maxMoreNights).Add(view.extendNightly).Add(view.extendReason).Add(Currency)
                .Add(view.myGrants).Add(view.nearby);
        mineSection.Show(mine.ToString(), hasRoom ? MyRoom : (Action<RectTransform>)null);
        accountCard.Show(view != null ? view.account : null);
        var rest = new HousingSig().Add(view == null ? Waiting : "");
        if (view != null)
            rest.Add(HousingUI.MessagesSignature(view.account, view.secondsPerDay, view.gameTime))
                .Add(HousingUI.HeldSignature(view.account, view.secondsPerDay, view.gameTime, true))
                .Add(Manages() ? HousingUI.PlacesSignature(view.account, view.nearby) : "");
        restSection.Show(rest.ToString(), BuildRest);
    }

    /// <summary>The player may let others into a place they do not rent (they were let in with that right).</summary>
    private bool Manages() => view != null && view.account.places.Exists(HousingUI.Manages);

    private void BuildRest(RectTransform list)
    {
        if (view == null)
        {
            HousingUI.Note(list, Waiting);
            return;
        }
        HousingUI.Messages(list, view.account, view.secondsPerDay, view.gameTime, ClearMessages);
        HousingUI.Held(list, view.account, view.secondsPerDay, view.gameTime, Claim, true);
        if (Manages())
            HousingUI.Places(list, view.account, view.nearby, (p, n, a) => HousingUI.LetIn(this, p, n, a), (p, g) => HousingUI.LetOut(this, p, g));
    }

    private void MyRoom(RectTransform parent)
    {
        RectTransform card = HousingUI.Card(parent, 16, 8f);
        HousingUI.SectionHeader(card, "Your room");
        HousingUI.Text(card, $"Your room: {MyRoomName()}, for another {Dur(view.myEndsIn)}", 19f, UITheme.Gold, FontStyles.Bold);
        // A stay may run at most Max Nights ahead (the books say how many more nights it may take now, and why not).
        int maxMore = view.maxMoreNights;
        if (view.extendNightly <= 0 || maxMore < 1)
        {
            HousingUI.Note(card, view.extendReason.Length > 0 ? view.extendReason : "You cannot stay longer now.");
        }
        else
        {
            more = Mathf.Clamp(more, 1, maxMore);
            RectTransform row = HousingUI.Line(card, 8f, 40f);
            TextMeshProUGUI count = null, total = null;
            Button fewer = null, plus = null;
            fewer = HousingUI.Small(row, "-", () =>
            {
                more = Mathf.Max(1, more - 1);
                ShowMore(count, total, fewer, plus, maxMore);
            }, 44f);
            count = HousingUI.Flex(HousingUI.Text(row, "", 17f, UITheme.Text, FontStyles.Bold, TextAlignmentOptions.Center));
            plus = HousingUI.Small(row, "+", () =>
            {
                more = Mathf.Min(maxMore, more + 1);
                ShowMore(count, total, fewer, plus, maxMore);
            }, 44f);
            HousingUI.Small(row, "Stay longer", Extend, 160f, UITheme.ButtonStyle.Primary);
            total = HousingUI.Text(card, "", 15f);
            ShowMore(count, total, fewer, plus, maxMore);
        }
        UITheme.Button("Give Up", card, "Give up the room", Leave, UITheme.ButtonStyle.Danger, 0f, 40f);

        HousingUI.SubHeader(card, "Who you let in");
        HousingUI.Grants(card, view.myGrants, Revoke);
        HousingUI.SubHeader(card, "Let in");
        if (view.nearby.Count > 0)
        {
            Toggle t = UITheme.Toggle(card, "They may let others in", manageRoom);
            t.onValueChanged.AddListener(x => manageRoom = x);
        }
        HousingUI.Nearby(card, view.nearby,
            ("Friend", n => Grant(n, HousingAccess.Friend)),
            ("Visitor", n => Grant(n, HousingAccess.Enter)));
    }

    private void ShowMore(TextMeshProUGUI count, TextMeshProUGUI total, Button fewer, Button plus, int maxMore)
    {
        if (view == null) return;
        HousingUI.Set(count, more == 1 ? "1 more night" : more + " more nights");
        HousingUI.Set(total, $"{Money(view.extendNightly)} a night (tonight's price): {Money(view.extendNightly * more)} for {HousingUI.Nights(more)}.");
        if (fewer != null) fewer.interactable = more > 1;
        if (plus != null) plus.interactable = more < maxMore;
    }

    // ------------------------------------------------------------------ asking
    private void Rent()
    {
        if (view == null) return;
        if (view.myRoomId.Length > 0)
        {
            core.Say("You have a room here already: stay longer instead.", false);
            return;
        }
        InnRoomView room = Chosen();
        if (room == null)
        {
            core.Say("Every room is taken.", false);
            return;
        }
        int n = nights;
        long total = room.nightly * n;
        string id = room.id;
        core.Ask($"Rent {RoomName(room)} for {HousingUI.Nights(n)} for {Money(total)}?", "Rent", () =>
            core.Send(new HousingRequest { kind = (byte)HousingRequestKind.RentRoom, roomId = id, nights = n, expected = total }));
    }

    private void Extend()
    {
        if (view == null || view.myRoomId.Length == 0 || view.extendNightly <= 0) return;
        int n = Mathf.Max(1, more);
        long total = view.extendNightly * n;
        core.Ask($"Stay {n} more night{(n == 1 ? "" : "s")} in {MyRoomName()} for {Money(total)}?", "Stay longer", () =>
            core.Send(new HousingRequest { kind = (byte)HousingRequestKind.ExtendStay, nights = n, expected = total }));
    }

    private void Leave()
    {
        if (view == null || view.myRoomId.Length == 0) return;
        core.Ask($"Give up {MyRoomName()} now? Nothing is paid back. What is in its chest is kept for you to claim from any innkeeper or realtor.",
            "Give it up", () => core.Send(new HousingRequest { kind = (byte)HousingRequestKind.LeaveRoom }), true);
    }

    private void Grant(NearbyView n, HousingAccess access)
    {
        if (view == null || view.myRoomId.Length == 0 || n == null) return;
        if (manageRoom) access |= HousingAccess.Manage;
        HousingRequest r = HousingUI.GrantRequest(view.innId, view.myRoomId, n, access);
        if (access == HousingAccess.Enter)
        {
            core.Send(r);
            return;
        }
        core.Ask($"Let {HousingUI.Who(n.name, n.fingerprint)} into {MyRoomName()}? They may {HousingService.Describe(access)}.", "Let them in", () => core.Send(r));
    }

    private void Revoke(GrantView g)
    {
        if (view == null || g == null) return;
        core.Send(HousingUI.RevokeRequest(view.innId, view.myRoomId, g));
    }

    private void ClearMessages() => core.Send(new HousingRequest { kind = (byte)HousingRequestKind.ReadNotices });

    private void Claim(HeldView h)
    {
        HousingUI.Claim(this, h, Currency); // with the fee shown: a fee that changed meanwhile is refused
    }
}
