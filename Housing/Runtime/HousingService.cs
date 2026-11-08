using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Where a request was made: what the player is standing at (the server checks it; offline it is the open window).</summary>
public struct HousingDesk
{
    /// <summary>The inn whose innkeeper the player talks to (inn requests).</summary>
    public Inn inn;
    /// <summary>The realtor the player talks to (orders).</summary>
    public Realtor realtor;
    /// <summary>At any housing NPC (claims, deposits, messages).</summary>
    public bool AtDesk => inn != null || realtor != null;
}

/// <summary>
/// Every housing action, where the rules run (offline, the server): renting and leaving inn rooms, standing orders for
/// houses, renewals, the rent account, claims of held items, letting others in - and the views the windows show. Money
/// moves whole: taken from the wallet first, then recorded in the books; given back when the books refuse; both saved
/// at once in that order, so a crash can only lose a payment, never make money. Requests carry an id: one sent twice is
/// done once.
/// </summary>
public static class HousingService
{
    /// <summary>Metres within which another player can be let into a place (standing next to the tenant).</summary>
    public const float NearbyMetres = 8f;

    private static readonly Dictionary<string, (string id, HousingReply reply, float at)> recent = new Dictionary<string, (string, HousingReply, float)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        recent.Clear();
        publicListings.Clear();
        publicBook = null;
        publicRevision = publicHouses = -1;
    }

    private static HousingBook Book => HousingDirector.Book;
    /// <summary>The time the books have reached (a request is done there, after they were brought to the clock).</summary>
    private static double Now => HousingDirector.Book?.Now ?? 0.0;
    private static CurrencyDefinition Money => HousingSettings.Instance.Currency;

    // ---------------------------------------------------------------- requests
    /// <summary>Does a request for a player at <paramref name="desk"/>; always answers (with what they may see).</summary>
    public static HousingReply Handle(CombatEntity player, HousingRequest r, HousingDesk desk)
    {
        HousingReply reply;
        try
        {
            reply = Do(player, r, desk);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            reply = HousingReply.Fail("Something went wrong.");
        }
        reply.seq = r != null ? r.seq : 0;
        return reply;
    }

    private static HousingReply Do(CombatEntity player, HousingRequest r, HousingDesk desk)
    {
        HousingBook b = Book;
        if (b == null) return HousingReply.Fail(string.IsNullOrEmpty(HousingDirector.Closed) ? "The housing office is closed." : HousingDirector.Closed);
        if (player == null || r == null) return HousingReply.Fail("Nobody to do it.");
        // Whatever is due first (a stay that just ended: its guest's things are held for them before the room is free).
        HousingDirector.CatchUp();
        // A long jump of the clock is cleared over a few frames: nothing changes until the books have caught up (a request
        // now would be judged at a time already past for the clearings still due).
        if (b.CatchingUp && (HousingRequestKind)r.kind != HousingRequestKind.View)
            return HousingReply.Fail("The housing office is catching up with the time that passed: try again in a moment.");
        string key = HousingDirector.KeyOf(player);
        if (key == null) return HousingReply.Fail("Only a saved character can do that.");
        string name = PartySystem.NameOf(player);
        // A retry of a request already done gets the same answer, and nothing is done twice.
        if (!string.IsNullOrEmpty(r.requestId) && recent.TryGetValue(key, out var last) && last.id == r.requestId) return Copy(last.reply);
        HousingReply reply = Run(b, player, key, name, r, desk);
        // Views are not remembered: a window's view refresh between a request and its retry must not hide the retry's answer.
        if (!string.IsNullOrEmpty(r.requestId) && (HousingRequestKind)r.kind != HousingRequestKind.View) recent[key] = (r.requestId, Copy(reply), Time.unscaledTime);
        if (recent.Count > 2048) recent.Clear();
        return reply;
    }

    private static HousingReply Copy(HousingReply r) => new HousingReply { ok = r.ok, message = r.message };

    private static HousingReply Run(HousingBook b, CombatEntity player, string key, string name, HousingRequest r, HousingDesk desk)
    {
        double now = Now;
        switch ((HousingRequestKind)r.kind)
        {
            case HousingRequestKind.RentRoom:
            {
                if (desk.inn == null) return HousingReply.Fail("Ask the innkeeper.");
                if (desk.inn.OutOfOrder) return HousingReply.Fail(ClosedInn(desk.inn));
                InnQuote q = b.QuoteStay(desk.inn.InnId, string.IsNullOrEmpty(r.roomId) ? null : r.roomId, key, r.nights, now);
                if (!q.ok) return HousingReply.Fail(q.reason);
                if (r.expected > 0 && r.expected != q.total) return HousingReply.Fail("The price changed: look again.");
                if (!Take(player, q.total, out CurrencyWallet w, out string why)) return HousingReply.Fail(why);
                if (!b.Stay(desk.inn.InnId, q.roomId, key, name, r.nights, q.total, now, out why))
                {
                    GiveBack(w, q.total);
                    return HousingReply.Fail(why);
                }
                Commit(w);
                return HousingReply.Ok($"{q.roomName} is yours for {r.nights} night{(r.nights == 1 ? "" : "s")}.");
            }
            case HousingRequestKind.ExtendStay:
            {
                if (desk.inn == null) return HousingReply.Fail("Ask the innkeeper.");
                if (desk.inn.OutOfOrder) return HousingReply.Fail(ClosedInn(desk.inn));
                InnQuote q = b.QuoteExtension(desk.inn.InnId, key, r.nights, now);
                if (!q.ok) return HousingReply.Fail(q.reason);
                if (r.expected > 0 && r.expected != q.total) return HousingReply.Fail("The price changed: look again.");
                if (!Take(player, q.total, out CurrencyWallet w, out string why)) return HousingReply.Fail(why);
                if (!b.Extend(desk.inn.InnId, key, r.nights, q.total, now, out why))
                {
                    GiveBack(w, q.total);
                    return HousingReply.Fail(why);
                }
                Commit(w);
                return HousingReply.Ok($"You stay {r.nights} more night{(r.nights == 1 ? "" : "s")}.");
            }
            case HousingRequestKind.LeaveRoom:
            {
                if (desk.inn == null) return HousingReply.Fail("Ask the innkeeper.");
                if (desk.inn.OutOfOrder) return HousingReply.Fail(ClosedInn(desk.inn));
                if (!b.Leave(desk.inn.InnId, key, now, out string why)) return HousingReply.Fail(why);
                Commit(null);
                return HousingReply.Ok("You gave up your room. What you left in its chest is kept for you.");
            }
            case HousingRequestKind.PlaceOrder:
            {
                if (desk.realtor == null) return HousingReply.Fail("Ask a realtor.");
                List<OrderItem> items = ItemsOf(r);
                if (items == null) return HousingReply.Fail("That order cannot be read.");
                OrderQuote q = b.QuoteOrder(key, items, now);
                if (!q.ok) return HousingReply.Fail(q.reason);
                if (r.expected != q.delta) return HousingReply.Fail("The prices changed: look again.");
                long pay = Math.Max(0, q.delta);
                if (!Take(player, pay, out CurrencyWallet w, out string why)) return HousingReply.Fail(why);
                if (!b.PlaceOrder(key, name, items, pay, now, out why))
                {
                    GiveBack(w, pay);
                    return HousingReply.Fail(why);
                }
                Commit(w);
                if (q.later)
                    return HousingReply.Ok($"Your change takes effect in {Clock(q.effectiveIn)} (offers are binding: until then your order stands as it was, and may still win).{HeldNote(b, key, now)}");
                return HousingReply.Ok($"Your order stands: it is looked at every clearing (the next in {Clock(b.NextClearing(now) - now)}).{HeldNote(b, key, now)}");
            }
            case HousingRequestKind.CancelOrder:
            {
                if (!b.CancelOrder(key, now, out string why)) return HousingReply.Fail(why);
                Commit(null);
                return HousingReply.Ok($"Your order is withdrawn in {Clock(b.OrderNoticeDays)} (offers are binding: until then it stands, and may still win); then its deposit is returned.{HeldNote(b, key, now)}");
            }
            case HousingRequestKind.SetRenewal:
            {
                if (!b.SetRenewal(r.placeId, key, r.flag, r.protection, now, out string why)) return HousingReply.Fail(why);
                Commit(null);
                return HousingReply.Ok(r.flag ? "Your lease will be renewed while your rent account pays for it." : "Your lease will end with this term.");
            }
            case HousingRequestKind.Deposit:
            {
                if (!desk.AtDesk) return HousingReply.Fail("Deposit at a realtor or an innkeeper.");
                long room = b.DepositRoom(key);
                if (r.amount <= 0) return HousingReply.Fail("Deposit something.");
                if (r.amount > room) return HousingReply.Fail(room > 0 ? $"Your rent account may take {Money.Format((int)Math.Min(int.MaxValue, room))} more." : "A rent account is for the rent of a house you have.");
                if (!Take(player, r.amount, out CurrencyWallet w, out string why)) return HousingReply.Fail(why);
                if (!b.Deposited(key, name, r.amount))
                {
                    GiveBack(w, r.amount);
                    return HousingReply.Fail("Your rent account cannot take that.");
                }
                Commit(w);
                return HousingReply.Ok($"{Money.Format((int)r.amount)} is in your rent account.");
            }
            case HousingRequestKind.Withdraw:
            {
                if (!desk.AtDesk) return HousingReply.Fail("Withdraw at a realtor or an innkeeper.");
                CurrencyWallet w = CurrencyWallet.For(player);
                if (w == null || w.IsReplica) return HousingReply.Fail("You have no purse.");
                if (w.AwaitingRestore || WalletSaves.IsRestoring(w)) return HousingReply.Fail("One moment: your purse is being fetched.");
                long room = Math.Max(0, (long)Money.MaxAmount - w.GetBalance(Money));
                long want = Math.Min(Math.Min(r.amount, room), int.MaxValue);
                if (want <= 0) return HousingReply.Fail(r.amount <= 0 ? "Withdraw something." : "Your purse is full.");
                if (!w.CanReceive(Money, (int)want)) return HousingReply.Fail("Your purse cannot take that now (no room for the coins).");
                long taken = b.Withdraw(key, want);
                if (taken <= 0) return HousingReply.Fail("There is nothing to withdraw.");
                if (!w.TryAdd(Money, (int)taken))
                {
                    b.UndoWithdraw(key, name, taken); // back where it was, whatever the account's cap
                    return HousingReply.Fail("Your purse cannot take it now.");
                }
                // Out of the books first, then into the purse: saved in that order.
                HousingDirector.SaveNow();
                WalletSaves.SaveNow(w);
                return HousingReply.Ok($"{Money.Format((int)taken)} is back in your purse.");
            }
            case HousingRequestKind.Claim:
            {
                if (!desk.AtDesk) return HousingReply.Fail("Claim it at a realtor or an innkeeper.");
                // A window that showed the fee sends it (flag): a claim is never charged other than what the player saw.
                HeldBundle held = b.Held(r.bundleId);
                if (r.flag && held != null && held.key == key && b.ClaimFee(held, now) != r.expected) return HousingReply.Fail("The fee changed: look again.");
                bool ok = HousingDirector.Claim(player, r.bundleId, out string message);
                if (ok) Commit(CurrencyWallet.For(player, false));
                return ok ? HousingReply.Ok(message) : HousingReply.Fail(message);
            }
            case HousingRequestKind.Grant:
            {
                CombatEntity other = HubWorld.PlayerIds.Find(r.playerId);
                if (other == null || other == player || other.gameObject.scene != player.gameObject.scene ||
                    (other.transform.position - player.transform.position).sqrMagnitude > NearbyMetres * NearbyMetres)
                    return HousingReply.Fail("They must stand next to you.");
                string otherKey = HousingDirector.KeyOf(other);
                if (!b.Grant(r.placeId, NullIfEmpty(r.roomId), key, otherKey, PartySystem.NameOf(other), (HousingAccess)r.access, now, out string why))
                    return HousingReply.Fail(why);
                Commit(null);
                HousingDirector.Tell(otherKey, $"{name} let you into {PlaceName(r.placeId, r.roomId)}.");
                return HousingReply.Ok($"{PartySystem.NameOf(other)} may now {Describe((HousingAccess)r.access)}.");
            }
            case HousingRequestKind.Revoke:
            {
                HousingGrant g = b.GrantByFingerprint(r.placeId, NullIfEmpty(r.roomId), r.fingerprint, now);
                if (g == null) return HousingReply.Fail("They are not let in.");
                if (!b.Grant(r.placeId, NullIfEmpty(r.roomId), key, g.key, g.name, HousingAccess.None, now, out string why)) return HousingReply.Fail(why);
                Commit(null);
                return HousingReply.Ok($"{g.name} may no longer use it.");
            }
            case HousingRequestKind.ReadNotices:
                b.ReadNotices(key);
                return HousingReply.Ok("");
            case HousingRequestKind.View:
                return HousingReply.Ok("");
            default:
                return HousingReply.Fail("That cannot be done.");
        }
    }

    private static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>Why an inn with the id of another (a copied object) lets no room.</summary>
    private static string ClosedInn(Inn inn) => $"{inn.DisplayName} is closed (it has the same Inn Id as another inn).";

    private static List<OrderItem> ItemsOf(HousingRequest r)
    {
        if (r.houses == null || r.terms == null || r.offersMilli == null) return null;
        if (r.houses.Count != r.terms.Count || r.houses.Count != r.offersMilli.Count || r.houses.Count > 32) return null;
        var items = new List<OrderItem>();
        for (int i = 0; i < r.houses.Count; i++)
            items.Add(new OrderItem { property = r.houses[i] ?? "", term = r.terms[i], rateMilli = r.offersMilli[i] });
        return items;
    }

    /// <summary>Takes money from the player's purse (all or nothing).</summary>
    private static bool Take(CombatEntity player, long amount, out CurrencyWallet wallet, out string why)
    {
        why = "";
        wallet = CurrencyWallet.For(player);
        if (amount <= 0) return true;
        if (wallet == null || wallet.IsReplica) { why = "You have no purse."; return false; }
        // (Its kept balances not back yet: what it paid would not be saved with what it bought.)
        if (wallet.AwaitingRestore || WalletSaves.IsRestoring(wallet)) { why = "One moment: your purse is being fetched."; return false; }
        if (GameNet.NetAuthority.IsNetworked && !Money.KeepBetweenSessions)
        {
            why = "Housing is paid in money that is kept between sessions (the currency's Keep Between Sessions).";
            return false;
        }
        if (amount > int.MaxValue || !wallet.TrySpend(Money, (int)amount))
        {
            why = $"You need {Money.Format((int)Math.Min(int.MaxValue, amount))}.";
            return false;
        }
        return true;
    }

    private static void GiveBack(CurrencyWallet w, long amount)
    {
        if (w != null && amount > 0 && amount <= int.MaxValue) w.TryAdd(Money, (int)amount);
    }

    /// <summary>The purse first, then the books: a crash between them can only lose the payment, never make money.</summary>
    private static void Commit(CurrencyWallet w)
    {
        if (w != null) WalletSaves.SaveNow(w);
        HousingDirector.SaveNow();
    }

    // ---------------------------------------------------------------- views
    /// <summary>What the innkeeper's window shows a player.</summary>
    public static InnView InnViewFor(CombatEntity player, Inn inn)
    {
        var v = new InnView();
        HousingBook b = Book;
        if (b == null) v.closed = string.IsNullOrEmpty(HousingDirector.Closed) ? "The housing office is closed." : HousingDirector.Closed;
        else if (inn != null && inn.OutOfOrder) v.closed = ClosedInn(inn);
        if (b == null || inn == null || inn.OutOfOrder) return v;
        double now = Now;
        string key = HousingDirector.KeyOf(player);
        v.innId = inn.InnId;
        v.name = inn.DisplayName;
        v.district = inn.District;
        v.baseNightly = Mathf.Max(1, inn.nightlyPrice);
        (double level, double occ) = b.InnLevel(inn.InnId, now);
        v.level = level;
        v.occupancy = occ;
        v.maxNights = Mathf.Max(1, b.Rules.Inns().maxNights);
        double nf = b.NextFree(inn.InnId, now);
        v.nextFreeIn = nf >= 0.0 ? nf - now : -1.0;
        foreach ((InnRoomFacts room, bool free, long nightly) in b.RoomPrices(inn.InnId, now))
            v.rooms.Add(new InnRoomView { id = room.id, name = room.name, quality = room.quality, free = free, nightly = nightly });
        RoomRecord mine = key != null ? b.RoomOf(inn.InnId, key, now) : null;
        if (mine != null)
        {
            v.myRoomId = mine.id;
            InnRoom r = inn.Room(mine.id);
            v.myRoomName = r != null ? r.DisplayName : "Your room";
            v.myEndsIn = mine.stay.end - now;
            InnQuote ext = b.QuoteExtension(inn.InnId, key, 1, now);
            v.extendNightly = ext.ok ? ext.nightly : 0;
            v.maxMoreNights = ext.ok ? b.MaxMoreNights(inn.InnId, key, now) : 0;
            v.extendReason = ext.ok ? "" : ext.reason ?? "";
            Grants(b, mine.stay, key, v.myGrants);
        }
        Nearby(b, player, v.nearby);
        v.secondsPerDay = HousingClock.SecondsPerDay(b.Rules);
        v.gameTime = HousingClock.Kind == HousingClockKind.GameTime;
        v.account = AccountFor(player, key);
        return v;
    }

    /// <summary>
    /// What a realtor's window shows a player: the realtor's houses, and those of the player's order that another realtor
    /// lets (marked). A null realtor: the player's own housing, seen from anywhere - only the houses they rent or name in
    /// their order.
    /// </summary>
    public static RealtorView RealtorViewFor(CombatEntity player, Realtor realtor)
    {
        var v = new RealtorView { own = realtor == null };
        HousingBook b = Book;
        if (b == null)
        {
            v.closed = string.IsNullOrEmpty(HousingDirector.Closed) ? "The housing office is closed." : HousingDirector.Closed;
            return v;
        }
        double now = Now;
        string key = HousingDirector.KeyOf(player);
        HousingMarketRules mr = b.Rules.Market();
        v.realtorName = realtor != null && realtor.Npc != null ? realtor.Npc.name : "";
        v.nextClearingIn = b.NextClearing(now) - now;
        v.clearingEvery = b.TickDays;
        v.orderNotice = b.OrderNoticeDays;
        v.weekDays = b.Rules.Days(LeaseTerm.Week);
        v.monthDays = b.Rules.Days(LeaseTerm.Month);
        v.maxOrderItems = mr.maxOrderItems;
        v.defaultProtection = mr.defaultProtection;
        v.maxProtection = mr.maxProtection;
        v.protectionMargin = mr.protectionMargin;
        v.protectionPremium = mr.protectionPremium;
        v.confirmAboveAsking = mr.confirmAboveAsking;
        v.mayOrder = b.MayOrder(key, out string why) ? "" : why;
        var districts = new List<string>();
        Vector3 from = realtor != null ? realtor.transform.position : player != null ? player.transform.position : Vector3.zero;
        // The houses the player rents or names in their order: shown wherever they are let.
        var theirs = new HashSet<string>();
        if (key != null)
        {
            foreach (PropertyRecord rec in b.LeasesOf(key)) theirs.Add(rec.id);
            MarketOrder mine = b.OrderOf(key);
            if (mine != null)
            {
                foreach (OrderItem it in mine.items) theirs.Add(it.property);
                if (mine.HasPending)
                    foreach (OrderItem it in mine.pending) theirs.Add(it.property); // the change waiting for the notice
            }
        }
        // What every player at this realtor sees alike is made once per change of the books (and of the city's houses), and
        // shared: each player's view adds only what is theirs. Many players at one desk then cost one build, not one each.
        List<PublicListing> shared = realtor != null ? PublicListings(b, realtor, now) : null;
        if (shared != null)
            foreach (PublicListing pl in shared)
            {
                v.listings.Add(Personal(b, pl, key, now, false));
                if (!districts.Contains(pl.property.District)) districts.Add(pl.property.District);
            }
        foreach (Property p in HousingDirector.Properties)
        {
            if (p == null || p.OutOfOrder) continue; // a copy with another's id is closed: not a second house
            bool here = realtor != null && Lists(realtor, p);
            if (here || !theirs.Contains(p.PropertyId)) continue; // this realtor's own houses came from the shared part
            PublicListing pl = MakePublic(b, p, from, now);
            if (pl == null) continue;
            v.listings.Add(Personal(b, pl, key, now, realtor != null));
            if (realtor == null && !districts.Contains(p.District)) districts.Add(p.District);
        }
        v.listings.Sort((x, y) => x.distance.CompareTo(y.distance));
        foreach (string d in districts)
        {
            v.districts.Add(d);
            v.districtLevels.Add(b.DistrictLevelOf(d));
        }
        if (key != null)
        {
            foreach (PropertyRecord rec in b.LeasesOf(key))
            {
                Property p = HousingDirector.FindProperty(rec.id);
                PropertyStatus st = b.StatusOf(rec.id, key, now);
                var lv = new LeaseView
                {
                    propertyId = rec.id, name = p != null ? p.DisplayName : "A house", endsIn = rec.lease.end - now, term = rec.term,
                    rateMilli = rec.lease.rateMilli, autoRenew = st.autoRenew, protection = st.protection, noticed = st.noticed,
                    renewRateMilli = st.renewRateMilli, renewTotal = st.renewTotal, reserved = st.reserved, movingOut = st.movingOut,
                };
                Grants(b, rec.lease, key, lv.grants);
                v.leases.Add(lv);
            }
            MarketOrder o = b.OrderOf(key);
            if (o != null)
            {
                foreach (OrderItem it in o.items)
                {
                    v.orderHouses.Add(it.property);
                    v.orderTerms.Add(it.term);
                    v.orderOffersMilli.Add(it.rateMilli);
                }
                v.orderEscrow = o.escrow;
                // A held order's change or withdrawal waits for the hold too (its house offered).
                double heldUntil = string.IsNullOrEmpty(o.pin) ? now : b.HeldUntil(o, now);
                if (o.HasPending)
                {
                    v.orderPendingIn = Math.Max(0.0, Math.Max(o.pendingAt, heldUntil) - now);
                    foreach (OrderItem it in o.pending)
                    {
                        v.orderPendingHouses.Add(it.property);
                        v.orderPendingTerms.Add(it.term);
                        v.orderPendingOffersMilli.Add(it.rateMilli);
                    }
                }
                if (o.cancelAt >= 0.0) v.orderCancelIn = Math.Max(0.0, Math.Max(o.cancelAt, heldUntil) - now);
                if (!string.IsNullOrEmpty(o.pin))
                {
                    v.orderHeldFor = o.pin;
                    v.orderHeldIn = Math.Max(0.0, b.HeldUntil(o, now) - now);
                }
            }
        }
        Nearby(b, player, v.nearby);
        v.secondsPerDay = HousingClock.SecondsPerDay(b.Rules);
        v.gameTime = HousingClock.Kind == HousingClockKind.GameTime;
        v.account = AccountFor(player, key);
        return v;
    }

    /// <summary>A house as everyone sees it (times kept as moments, so a cached one stays right as time passes).</summary>
    private sealed class PublicListing
    {
        public Property property;
        public ListingView view;
        public double leaseEnd, firstClearing;
        public readonly List<double> salesAt = new List<double>();
    }

    private static readonly Dictionary<Realtor, List<PublicListing>> publicListings = new Dictionary<Realtor, List<PublicListing>>();
    private static int publicRevision = -1, publicHouses = -1;
    private static HousingBook publicBook;

    /// <summary>A realtor's houses as everyone sees them, made again only when the books (or the city's houses) changed.</summary>
    private static List<PublicListing> PublicListings(HousingBook b, Realtor realtor, double now)
    {
        if (!ReferenceEquals(publicBook, b) || publicRevision != b.Revision || publicHouses != HousingDirector.Properties.Count)
        {
            publicListings.Clear();
            publicBook = b;
            publicRevision = b.Revision;
            publicHouses = HousingDirector.Properties.Count;
        }
        if (publicListings.TryGetValue(realtor, out List<PublicListing> list)) return list;
        list = new List<PublicListing>();
        Vector3 from = realtor.transform.position;
        foreach (Property p in HousingDirector.Properties)
        {
            if (p == null || p.OutOfOrder || !Lists(realtor, p)) continue;
            PublicListing pl = MakePublic(b, p, from, now);
            if (pl != null) list.Add(pl);
        }
        publicListings[realtor] = list;
        return list;
    }

    private static PublicListing MakePublic(HousingBook b, Property p, Vector3 from, double now)
    {
        PropertyStatus st = b.StatusOf(p.PropertyId, null, now);
        if (!st.known) return null;
        var l = new ListingView
        {
            id = p.PropertyId, name = p.DisplayName, description = p.description ?? "", district = p.District,
            houseArea = p.HouseFloorArea, houseFootprint = p.HouseFootprint, floors = Mathf.Max(1, p.floors), landArea = p.LandArea,
            gardenArea = p.ZoneArea(LandUse.Garden), zones = Zones(p), weekly = p.weekly, monthly = p.monthly || !p.weekly,
            comesFree = st.comesFree,
            askingMilli = st.askingMilli, baseMilli = st.baseMilli, vacant = st.vacant, tenant = st.tenantName,
            minOfferMilli = b.MinOfferMilli(p.PropertyId), takeAtMilli = b.TakeAtMilli(p.PropertyId),
        };
        Vector3 d = p.World(HousingGeometry.Bounds(p.land).center) - from;
        l.distance = new Vector2(d.x, d.z).magnitude;
        l.direction = Compass(d);
        var pl = new PublicListing { property = p, view = l, leaseEnd = st.leaseEnd, firstClearing = st.firstClearing };
        foreach (Sale s in b.SalesOf(p.PropertyId, 5))
        {
            l.salesMilli.Add(s.rateMilli);
            pl.salesAt.Add(s.at);
        }
        return pl;
    }

    /// <summary>One player's copy of a house as everyone sees it: the times from now, and what is theirs (their lease, their offer).</summary>
    private static ListingView Personal(HousingBook b, PublicListing pl, string key, double now, bool elsewhere)
    {
        ListingView s = pl.view;
        var l = new ListingView
        {
            id = s.id, name = s.name, description = s.description, district = s.district,
            houseArea = s.houseArea, houseFootprint = s.houseFootprint, floors = s.floors, landArea = s.landArea,
            gardenArea = s.gardenArea, zones = s.zones, weekly = s.weekly, monthly = s.monthly, comesFree = s.comesFree,
            askingMilli = s.askingMilli, baseMilli = s.baseMilli, vacant = s.vacant, tenant = s.tenant,
            minOfferMilli = s.minOfferMilli, takeAtMilli = s.takeAtMilli, distance = s.distance, direction = s.direction,
            endsIn = s.vacant ? 0.0 : pl.leaseEnd - now, firstClearingIn = s.vacant ? pl.firstClearing - now : 0.0,
            elsewhere = elsewhere,
        };
        l.salesMilli.AddRange(s.salesMilli);
        foreach (double at in pl.salesAt) l.salesAgo.Add(now - at);
        if (!string.IsNullOrEmpty(key))
        {
            l.mine = b.LeaseOf(s.id, key) != null;
            if (l.mine) l.takeAtMilli = 0;
            MarketOrder o = b.OrderOf(key);
            if (o != null)
                for (int i = 0; i < o.items.Count; i++)
                    if (o.items[i] != null && o.items[i].property == s.id)
                    {
                        l.myOfferMilli = o.items[i].rateMilli;
                        l.myTerm = o.items[i].term;
                        l.myRank = i + 1;
                    }
        }
        return l;
    }

    private static bool Lists(Realtor realtor, Property p)
    {
        if (realtor == null) return true;
        if (realtor.listings != null && realtor.listings.Count > 0 && !realtor.listings.Contains(p)) return false;
        return string.IsNullOrWhiteSpace(realtor.district) || string.Equals(realtor.district.Trim(), p.District, StringComparison.OrdinalIgnoreCase);
    }

    private static string Zones(Property p)
    {
        var parts = new List<string>();
        foreach (LandUse u in Enum.GetValues(typeof(LandUse)))
        {
            if (u == LandUse.Garden) continue;
            float a = p.ZoneArea(u);
            if (a > 0.5f) parts.Add($"{u.ToString().ToLowerInvariant()} {a:0} m²");
        }
        return string.Join(", ", parts);
    }

    private static string Compass(Vector3 d)
    {
        if (new Vector2(d.x, d.z).sqrMagnitude < 4f) return "here";
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        int i = Mathf.RoundToInt(((angle % 360f) + 360f) % 360f / 45f) % 8;
        return names[i];
    }

    private static void Grants(HousingBook b, Tenancy t, string key, List<GrantView> into)
    {
        if (t == null) return;
        bool tenant = t.key == key;
        foreach (HousingGrant g in t.grants)
            if (tenant || g.grantedBy == key || g.key == key)
                into.Add(new GrantView { name = g.name, fingerprint = b.Fingerprint(g.key), access = g.access, byMe = g.grantedBy == key });
    }

    private static void Nearby(HousingBook b, CombatEntity player, List<NearbyView> into)
    {
        if (player == null) return;
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            if (e == null || e == player || e.gameObject.scene != player.gameObject.scene) continue;
            if ((e.transform.position - player.transform.position).sqrMagnitude > NearbyMetres * NearbyMetres) continue;
            string k = HousingDirector.KeyOf(e);
            if (k == null) continue;
            into.Add(new NearbyView { playerId = HubWorld.PlayerIds.Of(e), name = PartySystem.NameOf(e), fingerprint = b.Fingerprint(k) });
        }
    }

    /// <summary>The player's account, messages, held items and the places they may use.</summary>
    public static AccountView AccountFor(CombatEntity player, string key)
    {
        var v = new AccountView();
        HousingBook b = Book;
        CurrencyDefinition money = Money;
        v.currency = money.Symbol;
        CurrencyWallet w = player != null ? CurrencyWallet.For(player, false) : null;
        v.wallet = w != null ? w.GetBalance(money) : 0;
        if (b == null || key == null) return v;
        double now = Now;
        HousingAccount a = b.Account(key);
        if (a != null)
        {
            v.balance = a.balance;
            v.owed = a.owed;
            foreach (HousingNotice n in a.notices) v.notices.Add(new NoticeView { ago = now - n.at, text = n.text });
        }
        foreach (PropertyRecord p in b.LeasesOf(key)) v.reserved += p.reserved;
        v.cap = b.AccountCap(key);
        foreach (HeldBundle h in b.HeldFor(key))
        {
            var hv = new HeldView { id = h.id, source = h.source, stacks = h.stacks.Count, items = h.Count, fee = b.ClaimFee(h, now), heldFor = now - h.createdAt };
            HeldRules hr = b.Rules.Held();
            if (hv.fee == 0 && hr.fee > 0 && !h.feePaid && hr.freeDays - hv.heldFor > 0.0) hv.freeFor = hr.freeDays - hv.heldFor;
            if (hr.keepDays > 0f) hv.discardIn = Math.Max(0.0, hr.keepDays - hv.heldFor);
            foreach (HeldStack s in h.stacks)
            {
                ItemSO item = ItemDatabase.Item(s.itemId);
                if (hv.names.Count < 12) hv.names.Add(item != null ? (s.count > 1 ? $"{item.Name} ×{s.count}" : item.Name) : "something");
            }
            v.held.Add(hv);
        }
        foreach (Property p in HousingDirector.Properties)
        {
            if (p == null || p.OutOfOrder) continue;
            HousingAccess acc = b.AccessOf(p.PropertyId, null, key, now);
            if (acc == HousingAccess.None) continue;
            Tenancy t = b.TenancyOf(p.PropertyId, null, now);
            var pv = new PlaceAccessView { placeId = p.PropertyId, name = p.DisplayName, access = (int)acc, tenant = t != null && t.key == key };
            if (!pv.tenant && (acc & HousingAccess.Manage) != 0) Grants(b, t, key, pv.grants);
            v.places.Add(pv);
        }
        foreach (Inn inn in HousingDirector.Inns)
        {
            if (inn == null || inn.OutOfOrder) continue;
            foreach (InnRoom r in inn.rooms)
            {
                if (r == null || r.OutOfOrder) continue;
                HousingAccess acc = b.AccessOf(inn.InnId, r.RoomId, key, now);
                if (acc == HousingAccess.None) continue;
                Tenancy t = b.TenancyOf(inn.InnId, r.RoomId, now);
                var pv = new PlaceAccessView { placeId = inn.InnId, roomId = r.RoomId, name = $"{r.DisplayName}, {inn.DisplayName}", access = (int)acc, tenant = t != null && t.key == key };
                if (!pv.tenant && (acc & HousingAccess.Manage) != 0) Grants(b, t, key, pv.grants);
                v.places.Add(pv);
            }
        }
        return v;
    }

    // ---------------------------------------------------------------- words
    private static string PlaceName(string placeId, string roomId)
    {
        HousingPlace place = HousingDirector.FindPlace(placeId, NullIfEmpty(roomId));
        return place.IsValid ? place.Name : "a place";
    }

    /// <summary>"enter, work the garden and open the chests".</summary>
    public static string Describe(HousingAccess a)
    {
        var parts = new List<string>();
        if ((a & HousingAccess.Enter) != 0) parts.Add("enter");
        if ((a & HousingAccess.Garden) != 0) parts.Add("work the garden");
        if ((a & HousingAccess.Storage) != 0) parts.Add("open the chests");
        if ((a & HousingAccess.Sleep) != 0) parts.Add("sleep there");
        if ((a & HousingAccess.Manage) != 0) parts.Add("let others in");
        if (parts.Count == 0) return "do nothing there";
        if (parts.Count == 1) return parts[0];
        var s = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) s.Append(i == parts.Count - 1 ? " and " : ", ");
            s.Append(parts[i]);
        }
        return s.ToString();
    }

    /// <summary>Lease days as words ("2 days 3 h", "40 min").</summary>

    /// <summary>" It is held for Rose Cottage ..." when the player's order is held for a house (a tenant's rent rests on its offer); "" otherwise.</summary>
    private static string HeldNote(HousingBook b, string key, double now)
    {
        MarketOrder o = b.OrderOf(key);
        if (o == null || string.IsNullOrEmpty(o.pin)) return "";
        PropertyFacts f = b.Property(o.pin);
        string house = f != null ? f.name : "a house";
        return $" It is held for {house} until that house is offered (in about {Clock(Math.Max(0.0, b.HeldUntil(o, now) - now))}): its tenant's rent rests on your offer, so until then it wins no other house.";
    }
    public static string Clock(double days) => Duration(days, HousingClock.SecondsPerDay(HousingDirector.Rules), HousingClock.Kind == HousingClockKind.GameTime);

    /// <summary>Lease days as words, given how long a lease day is in real seconds (game days are said in game days and hours).</summary>
    public static string Duration(double days, double secondsPerDay, bool gameTime)
    {
        if (days < 0.0) days = 0.0;
        if (gameTime)
        {
            int d = (int)Math.Floor(days);
            int h = (int)Math.Floor((days - d) * 24.0);
            return d > 0 ? $"{d} day{(d == 1 ? "" : "s")}{(h > 0 ? $" {h} h" : "")}" : h > 0 ? $"{h} h" : "under an hour";
        }
        double seconds = days * secondsPerDay;
        if (seconds >= 86400.0)
        {
            int d = (int)(seconds / 86400.0), h = (int)(seconds % 86400.0 / 3600.0);
            return $"{d} day{(d == 1 ? "" : "s")}{(h > 0 ? $" {h} h" : "")}";
        }
        if (seconds >= 3600.0) return $"{(int)(seconds / 3600.0)} h {(int)(seconds % 3600.0 / 60.0)} min";
        return seconds >= 60.0 ? $"{(int)(seconds / 60.0)} min" : "under a minute";
    }
}
