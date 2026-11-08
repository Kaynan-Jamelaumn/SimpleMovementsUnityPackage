using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>One house of an order: which, for which term, at most how much a day.</summary>
[Serializable]
public class OrderItem
{
    public string property = "";
    /// <summary>The term (<see cref="LeaseTerm"/>).</summary>
    public int term;
    /// <summary>The offer per day, in thousandths of the currency.</summary>
    public long rateMilli;
    /// <summary>The term's lease days when the order was placed (what its deposit was taken for; 0 in old saves: the days now).</summary>
    public int days;
}

/// <summary>
/// A player's standing order: up to a few houses in order of preference, each with an offer per day. Sealed (nobody else
/// ever sees it), binding (if it wins it is paid) and paid in advance (the escrow covers the dearest item). At each
/// clearing it can win at most one house; winning ends the order.
/// <para>Adding a house or offering more takes effect at once. Anything that takes something back - a house dropped, an
/// offer lowered, a term changed, the order withdrawn - takes effect only after the order notice (longer than a house
/// takes from being known to come free to being offered): until then the order stands as it was. So an offer that moves
/// prices can always be taken at its word.</para>
/// </summary>
[Serializable]
public class MarketOrder
{
    public long id;
    public string key = "";
    public string name = "";
    /// <summary>Favourite first.</summary>
    public List<OrderItem> items = new List<OrderItem>();
    /// <summary>Money held for it: the largest item's term total.</summary>
    public long escrow;
    /// <summary>When it first stood (editing keeps it: the older order wins ties).</summary>
    public double placedAt;
    public double editedAt;
    /// <summary>A change that takes something back, waiting for the order notice (empty: none); it replaces <see cref="items"/> at <see cref="pendingAt"/>.</summary>
    public List<OrderItem> pending = new List<OrderItem>();
    public double pendingAt = -1.0;
    /// <summary>The deposit the pending change needs (the larger of the two is held until it takes effect).</summary>
    public long pendingEscrow;
    /// <summary>When the order is withdrawn (it is being taken back and stands until then); -1: it is not.</summary>
    public double cancelAt = -1.0;
    /// <summary>
    /// The house the order is held for (empty: none), since <see cref="pinAt"/>: when a tenant's rent was fixed, this order's
    /// offer was the one that let it be that high. Until that house is offered (its lease end; its first clearing if it comes
    /// free) the order wins no other house, and at that clearing it is tried for it first - so the offer that set the rent
    /// takes the house if the tenant leaves.
    /// </summary>
    public string pin = "";
    public double pinAt = -1.0;

    public bool HasPending => pending != null && pending.Count > 0 && pendingAt >= 0.0;
}

/// <summary>A price actually paid for a house (public: the realtor shows them).</summary>
[Serializable]
public class Sale
{
    public double at;
    public long rateMilli;
    public int days;
    /// <summary>A sitting tenant renewed (the rent, without the protection premium).</summary>
    public bool renewal;
}

/// <summary>A house's state in the books.</summary>
[Serializable]
public class PropertyRecord
{
    public string id = "";
    public Tenancy lease;
    /// <summary>The lease's term (<see cref="LeaseTerm"/>).</summary>
    public int term;
    public bool autoRenew = true;
    /// <summary>The tenant's protection, times the renewal rent (0: the default).</summary>
    public float protection;
    /// <summary>The renewal is fixed (the notice was given): rent, protection and price below.</summary>
    public bool noticed;
    public long renewRateMilli;
    public long protectionMilli;
    public long renewTotal;
    /// <summary>Money set aside from the rent account for the renewal.</summary>
    public long reserved;
    /// <summary>The tenant won another house: this lease ends at its term.</summary>
    public bool movingOut;
    /// <summary>Free: when it was first offered (its first clearing waits a little). -1 while let.</summary>
    public double offeredSince = -1.0;
    /// <summary>The house's price level: its asking rent is its base rent times e^level (natural log; 0: the base rent).</summary>
    public double level;
    /// <summary>The renewal's term in lease days, fixed with the notice.</summary>
    public int renewDays;
    /// <summary>The last clearing the city had it (its facts known); -1: not seen since this was recorded.</summary>
    public double seenAt = -1.0;
    /// <summary>Its district when last seen (normalised): districts are counted from every house the city has had, loaded or not.</summary>
    public string district = "";
    public List<Sale> sales = new List<Sale>();

    public void Repair()
    {
        sales ??= new List<Sale>();
        if (lease != null) lease.grants ??= new List<HousingGrant>();
        if (lease != null && string.IsNullOrEmpty(lease.key)) lease = null;
    }

    public void Shift(double delta)
    {
        HousingSave.ShiftTenancy(lease, delta);
        if (offeredSince >= 0.0) offeredSince += delta;
        if (seenAt >= 0.0) seenAt += delta;
        foreach (Sale s in sales) s.at += delta;
    }
}

/// <summary>The market's part of the save.</summary>
[Serializable]
public class MarketSave
{
    public List<MarketOrder> orders = new List<MarketOrder>();
    public double lastTick = -1.0;
    /// <summary>The lease days between clearings the grid was laid with (0: an old save): a change of Tick Hours moves lease ends onto the new grid.</summary>
    public double tickDays;
    public long ticks;
    /// <summary>Random, made once: breaks ties nobody can predict, and makes the fingerprints shown next to names.</summary>
    public long secret;

    public void Repair()
    {
        orders ??= new List<MarketOrder>();
        orders.RemoveAll(o => o == null || string.IsNullOrEmpty(o.key));
        foreach (MarketOrder o in orders)
        {
            o.items ??= new List<OrderItem>();
            o.pending ??= new List<OrderItem>();
            o.pin ??= "";
        }
        if (secret == 0)
        {
            var b = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            secret = BitConverter.ToInt64(b, 0) | 1L;
        }
    }

    public void Shift(double delta)
    {
        if (lastTick >= 0.0) lastTick += delta;
        foreach (MarketOrder o in orders)
        {
            o.placedAt += delta;
            o.editedAt += delta;
            if (o.pendingAt >= 0.0) o.pendingAt += delta;
            if (o.cancelAt >= 0.0) o.cancelAt += delta;
            if (o.pinAt >= 0.0) o.pinAt += delta;
        }
    }
}

/// <summary>What placing (or changing) an order would take: the new escrow and what the player must pay (or gets back).</summary>
public struct OrderQuote
{
    public bool ok;
    public string reason;
    /// <summary>The deposit held once placed (while a change waits for the order notice: the larger of the old and the new).</summary>
    public long escrow;
    /// <summary>Positive: to pay now; negative: given back.</summary>
    public long delta;
    /// <summary>The change takes something back: it takes effect in <see cref="effectiveIn"/> lease days (the order stands as it was until then).</summary>
    public bool later;
    public double effectiveIn;
}

/// <summary>Everything the realtor shows about one house to one player.</summary>
public struct PropertyStatus
{
    public bool known;
    public bool vacant;
    public bool mine;
    public string tenantName;
    public double leaseEnd;
    public LeaseTerm term;
    /// <summary>What it asks now, per day (thousandths).</summary>
    public long askingMilli;
    public long baseMilli;
    /// <summary>Free: the first clearing it can be let at (a newly offered house waits a little).</summary>
    public double firstClearing;
    public double nextClearing;
    /// <summary>Let, but its lease will not be renewed (public from the renewal notice): it comes free when its term ends.</summary>
    public bool comesFree;
    /// <summary>The player's own offer on it (0: none), its term and its place in their order.</summary>
    public long myOfferMilli;
    public LeaseTerm myTerm;
    public int myRank;
    // The tenant's own lease
    public bool autoRenew;
    public float protection;
    public bool noticed;
    public long renewTotal;
    public long reserved;
    public long renewRateMilli;
    public bool movingOut;
}

public sealed partial class HousingBook
{
    /// <summary>Set by the caller in a network session: orders count only from players online long enough (alts cannot rent at once).</summary>
    public bool RequireResidency { get; set; }

    private MarketSave Market => save.market;
    private HousingMarketRules MR => Rules.Market();

    /// <summary>
    /// Real milliseconds one <see cref="Advance"/> may spend on clearings (0: all that are due, at once). The world sets it so
    /// a long jump of the clock is cleared over a few frames; the tests and tools leave it at 0.
    /// </summary>
    public double ClearingBudgetMs { get; set; }

    private double budgetMs => ClearingBudgetMs;

    /// <summary>
    /// Clearings are still due from a jump of the clock (the last <see cref="Advance"/> ran out of its budget): the next
    /// ones go on with them. Until then nothing may change the market (a request would be judged at the wrong time).
    /// </summary>
    public bool CatchingUp { get; private set; }

    /// <summary>Lease days between two clearings.</summary>
    public double TickDays => Math.Max(1.0 / 24.0, MR.tickHours / 24.0);

    /// <summary>The next clearing after <paramref name="now"/>.</summary>
    public double NextClearing(double now)
    {
        double td = TickDays;
        double last = Market.lastTick >= 0.0 ? Market.lastTick : Math.Floor(Math.Min(save.lastAdvance, now) / td) * td;
        double next = last + td;
        while (next <= now - 1e-9) next += td;
        return next;
    }

    /// <summary>A time moved to the clearing at or after it (leases end on clearings: the grid runs from the last clearing).</summary>
    private double Snap(double t)
    {
        double td = TickDays;
        double origin = Market.lastTick >= 0.0 ? Market.lastTick : 0.0;
        return origin + Math.Ceiling((t - origin) / td - 1e-9) * td;
    }

    // ---------------------------------------------------------------- state
    private PropertyRecord PropertyState(string id, bool create)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (propertyIndex.TryGetValue(id, out PropertyRecord p)) return p;
        if (!create) return null;
        p = new PropertyRecord { id = id };
        save.properties.Add(p);
        propertyIndex[id] = p;
        propertyRank[id] = save.properties.Count - 1;
        return p;
    }

    // ---------------------------------------------------------------- indexes
    // The market looks things up by player and by house many times per clearing and per window: kept in indexes instead
    // of searching every order and every house each time (a city's cost then grows with its size, not its square). The
    // books never change orders or leases but through the few places below, and the indexes are rebuilt when a save loads.
    private readonly Dictionary<string, MarketOrder> orderIndex = new Dictionary<string, MarketOrder>();
    /// <summary>House -> the orders whose items name it (what a house's offers are read from).</summary>
    private readonly Dictionary<string, List<MarketOrder>> offerIndex = new Dictionary<string, List<MarketOrder>>();
    /// <summary>Player -> the houses they rent.</summary>
    private readonly Dictionary<string, List<PropertyRecord>> leaseIndex = new Dictionary<string, List<PropertyRecord>>();
    /// <summary>Each house's place among the records (lists of leases keep the books' order).</summary>
    private readonly Dictionary<string, int> propertyRank = new Dictionary<string, int>();
    private static readonly List<MarketOrder> NoOrders = new List<MarketOrder>();

    private void ReindexMarket()
    {
        orderIndex.Clear();
        offerIndex.Clear();
        leaseIndex.Clear();
        propertyRank.Clear();
        for (int i = 0; i < save.properties.Count; i++)
        {
            PropertyRecord p = save.properties[i];
            if (!propertyRank.ContainsKey(p.id)) propertyRank[p.id] = i;
            if (p.lease != null) IndexLease(p);
        }
        foreach (MarketOrder o in Market.orders)
            if (!orderIndex.ContainsKey(o.key)) IndexOrder(o);
    }

    private void IndexOrder(MarketOrder o)
    {
        orderIndex[o.key] = o;
        IndexOffers(o);
    }

    private void UnindexOrder(MarketOrder o)
    {
        if (orderIndex.TryGetValue(o.key, out MarketOrder cur) && ReferenceEquals(cur, o)) orderIndex.Remove(o.key);
        UnindexOffers(o);
    }

    private void IndexOffers(MarketOrder o)
    {
        foreach (OrderItem it in o.items)
        {
            if (it == null || string.IsNullOrEmpty(it.property)) continue;
            if (!offerIndex.TryGetValue(it.property, out List<MarketOrder> list)) offerIndex[it.property] = list = new List<MarketOrder>();
            if (!list.Contains(o)) list.Add(o);
        }
    }

    private void UnindexOffers(MarketOrder o)
    {
        foreach (OrderItem it in o.items)
            if (it != null && !string.IsNullOrEmpty(it.property) && offerIndex.TryGetValue(it.property, out List<MarketOrder> list))
                list.Remove(o);
    }

    /// <summary>An order's items are replaced (the offers index follows).</summary>
    private void SetItems(MarketOrder o, List<OrderItem> items)
    {
        UnindexOffers(o);
        o.items = items;
        IndexOffers(o);
    }

    private void RemoveOrder(MarketOrder o)
    {
        Market.orders.Remove(o);
        UnindexOrder(o);
    }

    /// <summary>The orders whose items name a house.</summary>
    private List<MarketOrder> OffersFor(string propertyId) =>
        propertyId != null && offerIndex.TryGetValue(propertyId, out List<MarketOrder> list) ? list : NoOrders;

    private void IndexLease(PropertyRecord p)
    {
        string key = p.lease?.key;
        if (string.IsNullOrEmpty(key)) return;
        if (!leaseIndex.TryGetValue(key, out List<PropertyRecord> list)) leaseIndex[key] = list = new List<PropertyRecord>();
        if (!list.Contains(p)) list.Add(p);
    }

    private void UnindexLease(PropertyRecord p)
    {
        string key = p.lease?.key;
        if (!string.IsNullOrEmpty(key) && leaseIndex.TryGetValue(key, out List<PropertyRecord> list)) list.Remove(p);
    }

    /// <summary>A house's lease is replaced (null: it is free); the lease index follows.</summary>
    private void SetLease(PropertyRecord p, Tenancy lease)
    {
        UnindexLease(p);
        p.lease = lease;
        IndexLease(p);
    }

    /// <summary>The lease a player holds on a house (null: not theirs).</summary>
    public PropertyRecord LeaseOf(string propertyId, string key)
    {
        PropertyRecord p = PropertyState(propertyId, false);
        return p != null && p.lease != null && p.lease.key == key ? p : null;
    }

    /// <summary>The houses a player rents.</summary>
    public List<PropertyRecord> LeasesOf(string key)
    {
        var list = new List<PropertyRecord>();
        if (string.IsNullOrEmpty(key) || !leaseIndex.TryGetValue(key, out List<PropertyRecord> held)) return list;
        foreach (PropertyRecord p in held)
            if (p.lease != null && p.lease.key == key) list.Add(p);
        // In the books' order, as a search of every house would give them.
        if (list.Count > 1) list.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        return list;
    }

    private int Rank(PropertyRecord p) => propertyRank.TryGetValue(p.id, out int r) ? r : int.MaxValue;

    public MarketOrder OrderOf(string key) => !string.IsNullOrEmpty(key) && orderIndex.TryGetValue(key, out MarketOrder o) ? o : null;

    /// <summary>
    /// When the house an order is held for is offered: its lease end while it is let (its first clearing after that if it
    /// comes free), else its first clearing. <paramref name="now"/> when the order is held for nothing.
    /// </summary>
    public double HeldUntil(MarketOrder o, double now)
    {
        PropertyRecord p = o != null && !string.IsNullOrEmpty(o.pin) ? PropertyState(o.pin, false) : null;
        if (p == null) return now;
        double delay = Math.Max(0f, MR.firstClearingDelayHours) / 24.0;
        double next = NextClearing(now);
        if (p.lease != null)
        {
            // A lease that ends unrenewed frees the house at its end; it is first offered a clearing later at the soonest.
            bool comesFree = p.noticed && (!p.autoRenew || p.movingOut || p.renewTotal <= 0);
            return Math.Max(next, comesFree ? Snap(p.lease.end + Math.Max(delay, TickDays)) : p.lease.end);
        }
        double first = p.offeredSince >= 0.0 ? p.offeredSince + delay : now + delay;
        return Math.Max(next, Snap(first));
    }

    /// <summary>The world changed (properties came or went): their records exist, with their district.</summary>
    private void WorldChanged()
    {
        foreach (PropertyFacts f in properties.Values)
        {
            PropertyRecord p = PropertyState(f.id, true);
            p.district = NormDistrict(f.district);
            if (p.seenAt < 0.0) p.seenAt = save.lastAdvance;
        }
    }

    private static string NormDistrict(string d) => string.IsNullOrWhiteSpace(d) ? "" : d.Trim().ToLowerInvariant();

    // ---------------------------------------------------------------- prices
    /// <summary>A house's base rent per day (its sizes, quality and district).</summary>
    public double BaseDaily(string propertyId)
    {
        PropertyFacts f = Property(propertyId);
        return f != null ? HousingPricing.BaseDaily(f, Rules) : 1.0;
    }

    /// <summary>What a house asks now, per day (thousandths): its base rent times its own price level, within the bounds.</summary>
    public long AskingMilli(string propertyId)
    {
        PropertyFacts f = Property(propertyId);
        if (f == null) return 1000;
        return AskingAt(f, PropertyState(propertyId, true).level);
    }

    /// <summary>What a house asks at a price level (the bounds applied).</summary>
    private long AskingAt(PropertyFacts f, double level) =>
        Math.Max(1L, (long)Math.Ceiling(HousingPricing.BaseDaily(f, Rules) * Math.Exp(Clamp(level, LevelLo, LevelHi)) * 1000.0 - 1e-6));

    /// <summary>What a house asks at its base rent (its sizes, quality and district).</summary>
    private long BaseAskMilli(PropertyFacts f) => AskingAt(f, 0.0);

    /// <summary>The highest price level at which a house asks no more than <paramref name="rateMilli"/> a day (-infinity: none).</summary>
    private double LevelAt(PropertyFacts f, long rateMilli)
    {
        double baseMilli = HousingPricing.BaseDaily(f, Rules) * 1000.0;
        if (rateMilli <= 0 || baseMilli <= 0.0) return double.NegativeInfinity;
        double level = Math.Log(rateMilli / baseMilli);
        // Rounding must never leave the asking a thousandth above the offer it was set from (that offer would no longer reach it).
        for (int i = 0; i < 40 && level > LevelLo && AskingAt(f, level) > rateMilli; i++) level -= 1e-12 * Math.Pow(2.0, i);
        return level;
    }

    // ---------------------------------------------------------------- what prices rest on
    /// <summary>
    /// What an order's offer for a house stands behind now, per day (0: nothing). An offer being taken back moves no price from
    /// the moment it is taken back - nothing of an order being withdrawn, nothing for a house a waiting change drops, the lower
    /// offer of the two while a change waits - though it stays binding (it may still win) until the order notice ends.
    /// </summary>
    private static long SupportOf(MarketOrder o, OrderItem it)
    {
        if (o == null || it == null || o.cancelAt >= 0.0) return 0;
        if (!o.HasPending) return it.rateMilli;
        OrderItem later = o.pending.Find(x => x != null && x.property == it.property);
        return later == null ? 0 : Math.Min(it.rateMilli, later.rateMilli);
    }

    /// <summary>
    /// The best offer for a house that someone besides its tenant stands behind now (per day, thousandths; 0: none), and the
    /// order it is in (ties: the older order). <paramref name="free"/>: only orders free to take the house - not held for
    /// another one (an order held for a house wins nothing else until that house is offered).
    /// </summary>
    private long BestSupport(PropertyRecord p, bool free, out MarketOrder by)
    {
        by = null;
        long best = 0;
        if (p == null) return 0;
        string tenant = p.lease != null ? p.lease.key : null;
        foreach (MarketOrder o in OffersFor(p.id))
        {
            if (o.key == tenant || (free && !string.IsNullOrEmpty(o.pin) && o.pin != p.id)) continue;
            foreach (OrderItem it in o.items)
            {
                if (it == null || it.property != p.id) continue;
                long s = SupportOf(o, it);
                if (s > best || (s == best && s > 0 && by != null && Older(o, by))) { best = s; by = o; }
            }
        }
        return best;
    }

    private static bool Older(MarketOrder a, MarketOrder b) => a.placedAt != b.placedAt ? a.placedAt < b.placedAt : a.id < b.id;

    /// <summary>
    /// What a house really asks now: its asking rent - but more than its base rent only as far as an offer stands behind it
    /// from someone free to take the house (an offer being taken back, or one in an order held for another house, holds no
    /// price up). A sitting tenant's renewal is fixed from this, so no rent rests on an offer that could not take the house.
    /// </summary>
    private long HonestAskingMilli(PropertyRecord p)
    {
        PropertyFacts f = Property(p.id);
        long asking = AskingMilli(p.id);
        if (f == null) return asking;
        long baseAsk = BaseAskMilli(f);
        if (asking <= baseAsk) return asking;
        return Math.Min(asking, Math.Max(baseAsk, BestSupport(p, true, out _)));
    }

    private double LevelLo => Math.Log(Math.Max(0.01f, MR.minLevel));
    private double LevelHi => Math.Log(Math.Max(MR.minLevel, MR.maxLevel));

    /// <summary>A district's usual price level now: the median, over its houses, of what each asks against its base rent (1 = as the sizes say).</summary>
    public double DistrictLevelOf(string district) => MedianRatio(NormDistrict(district));

    private double MedianRatio(string district)
    {
        var ratios = new List<double>();
        foreach (PropertyFacts f in properties.Values)
            if (NormDistrict(f.district) == district)
            {
                PropertyRecord p = PropertyState(f.id, true);
                ratios.Add(Math.Exp(Clamp(p.level, LevelLo, LevelHi)));
            }
        if (ratios.Count == 0) return 1.0;
        ratios.Sort();
        int n = ratios.Count;
        return n % 2 == 1 ? ratios[n / 2] : 0.5 * (ratios[n / 2 - 1] + ratios[n / 2]);
    }

    /// <summary>Let, but known to come free: its renewal notice was given and it will not be renewed.</summary>
    private static bool ComingFree(PropertyRecord p) => p.lease != null && p.noticed && (!p.autoRenew || p.movingOut || p.renewTotal <= 0);

    /// <summary>
    /// The least offer a house takes, per day (thousandths). A free house, or one known to come free, asks what it really
    /// asks (<see cref="HonestAskingMilli"/>). A house that stays let takes offers from its base rent (or its asking, when
    /// that is lower): what others offer for a let house never makes anyone's offer dearer - at its lease end its
    /// tenant's protection decides, and if it comes free it asks only what stands behind it then.
    /// </summary>
    public long MinOfferMilli(string propertyId)
    {
        PropertyFacts f = Property(propertyId);
        if (f == null) return 1000;
        PropertyRecord p = PropertyState(propertyId, true);
        if (p.lease == null || ComingFree(p)) return HonestAskingMilli(p);
        return Math.Min(AskingMilli(propertyId), BaseAskMilli(f));
    }

    /// <summary>
    /// The least offer that takes a let house from its tenant at its lease end, per day (thousandths; 0: not known yet - its
    /// renewal is not fixed, or it will not be renewed). Public: anyone may make it, so a house never passes at its lease
    /// end to an offer only a friend knew would do.
    /// </summary>
    public long TakeAtMilli(string propertyId)
    {
        PropertyRecord p = PropertyState(propertyId, false);
        if (p == null || p.lease == null || !p.noticed || ComingFree(p) || Property(propertyId) == null) return 0;
        return Math.Max(HousingTenure.BeatMilli(p.protectionMilli, MR.protectionMargin) + 1, MinOfferMilli(propertyId));
    }

    /// <summary>A house the city has not had for a whole term (its scene gone, the house removed).</summary>
    private bool GoneForATerm(PropertyRecord p, double t) =>
        Property(p.id) == null && (p.seenAt < 0.0 || t - p.seenAt > Rules.Days((LeaseTerm)p.term));

    /// <summary>
    /// A sitting tenant's renewal rent per day: what the house really asks (<see cref="HonestAskingMilli"/>), but never rising
    /// faster than Max Rise Rate.
    /// </summary>
    private long RenewRateMilli(PropertyRecord p)
    {
        // A house whose scene is not loaded is not known here now: it renews at the rent it has.
        if (Property(p.id) == null) return p.lease != null ? p.lease.rateMilli : 1000;
        long asking = HonestAskingMilli(p);
        if (p.lease == null) return asking;
        int days = Rules.Days((LeaseTerm)p.term);
        double cap = p.lease.rateMilli * Math.Exp(MR.maxRiseRatePerDay * days);
        return Math.Max(1L, Math.Min(asking, (long)Math.Ceiling(cap - 1e-6)));
    }

    private static long Total(long rateMilli, int days) => (long)Math.Ceiling(rateMilli * (double)days / 1000.0 - 1e-9);

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>The sales of a district's houses, newest first (what the realtor shows of the market).</summary>
    public List<(string property, Sale sale)> SalesIn(string district, int max)
    {
        var list = new List<(string, Sale)>();
        string d = NormDistrict(district);
        foreach (PropertyRecord p in save.properties)
        {
            PropertyFacts f = Property(p.id);
            if (f == null || (district != null && NormDistrict(f.district) != d)) continue;
            foreach (Sale s in p.sales) list.Add((p.id, s));
        }
        list.Sort((a, b) => b.Item2.at.CompareTo(a.Item2.at));
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }

    /// <summary>A house's own last prices (newest first, renewals included), at most <paramref name="max"/>.</summary>
    public List<Sale> SalesOf(string propertyId, int max)
    {
        var list = new List<Sale>();
        PropertyRecord p = PropertyState(propertyId, false);
        if (p == null || p.sales == null) return list;
        for (int i = 0; i < p.sales.Count && list.Count < max; i++)
            if (p.sales[i] != null) list.Add(p.sales[i]);
        return list;
    }

    private void RecordSale(PropertyRecord p, Sale s)
    {
        p.sales.Insert(0, s);
        if (p.sales.Count > 8) p.sales.RemoveRange(8, p.sales.Count - 8);
    }

    // ---------------------------------------------------------------- what a player sees
    public PropertyStatus StatusOf(string propertyId, string key, double now)
    {
        var st = new PropertyStatus();
        PropertyFacts f = Property(propertyId);
        // A house the city does not have now (its scene not loaded, or removed) is not listed - but its tenant still sees
        // their own lease as the books hold it.
        PropertyRecord p = PropertyState(propertyId, f != null);
        if (p == null) return st;
        st.known = f != null;
        st.vacant = p.lease == null;
        st.mine = !st.vacant && p.lease.key == key;
        st.tenantName = st.vacant ? "" : p.lease.name;
        st.leaseEnd = st.vacant ? 0.0 : p.lease.end;
        st.term = (LeaseTerm)p.term;
        st.comesFree = !st.vacant && p.noticed && (!p.autoRenew || p.movingOut || p.renewTotal <= 0);
        st.nextClearing = NextClearing(now);
        if (f != null)
        {
            st.askingMilli = AskingMilli(propertyId);
            st.baseMilli = (long)Math.Ceiling(HousingPricing.BaseDaily(f, Rules) * 1000.0);
            double first = p.offeredSince >= 0.0 ? p.offeredSince + MR.firstClearingDelayHours / 24.0 : now + MR.firstClearingDelayHours / 24.0;
            st.firstClearing = st.vacant ? Math.Max(st.nextClearing, Snap(first)) : 0.0;
        }
        MarketOrder o = OrderOf(key);
        if (o != null)
            for (int i = 0; i < o.items.Count; i++)
                if (o.items[i].property == propertyId)
                {
                    st.myOfferMilli = o.items[i].rateMilli;
                    st.myTerm = (LeaseTerm)o.items[i].term;
                    st.myRank = i + 1;
                }
        if (st.mine)
        {
            st.autoRenew = p.autoRenew && !p.movingOut;
            st.protection = p.protection > 0f ? p.protection : MR.defaultProtection;
            st.noticed = p.noticed;
            st.renewTotal = p.renewTotal;
            st.reserved = p.reserved;
            st.renewRateMilli = p.noticed ? p.renewRateMilli : f != null ? RenewRateMilli(p) : p.lease.rateMilli;
            st.movingOut = p.movingOut;
        }
        return st;
    }

    // ---------------------------------------------------------------- orders
    /// <summary>May this player's orders count (residency, limits, what is held for them)? The reason when not.</summary>
    public bool MayOrder(string key, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(key)) { reason = "Only a saved character can rent a house."; return false; }
        HousingAccount a = Account(key);
        if (RequireResidency && MR.minOnlineHours > 0f && (a == null || a.onlineSeconds < MR.minOnlineHours * 3600.0))
        {
            reason = $"Newcomers wait: you may rent a house after {MR.minOnlineHours:0.#} hours in the city.";
            return false;
        }
        int leases = 0, moving = 0;
        foreach (PropertyRecord p in LeasesOf(key))
        {
            leases++;
            if (p.movingOut) moving++;
        }
        if (leases - moving > Math.Max(1, MR.maxHouses) || leases > Math.Max(1, MR.maxHouses))
        {
            reason = "You are moving already: wait until your old lease ends.";
            return false;
        }
        if (HeldStacks(key) > Rules.Held().maxStacks)
        {
            reason = "Claim what is kept for you first (an innkeeper or a realtor has it).";
            return false;
        }
        return true;
    }

    /// <summary>What placing these items as the player's order would take (validated against the houses and the rules).</summary>
    public OrderQuote QuoteOrder(string key, List<OrderItem> items, double now)
    {
        var q = new OrderQuote();
        if (!MayOrder(key, out q.reason)) return q;
        if (items == null || items.Count == 0) { q.reason = "Name at least one house."; return q; }
        if (items.Count > Math.Max(1, MR.maxOrderItems)) { q.reason = $"An order names at most {MR.maxOrderItems} houses."; return q; }
        var seen = new HashSet<string>();
        long escrow = 0;
        foreach (OrderItem it in items)
        {
            PropertyFacts f = it != null ? Property(it.property) : null;
            if (f == null) { q.reason = "One of those houses is not let by the city."; return q; }
            if (!seen.Add(it.property)) { q.reason = $"{f.name} is named twice."; return q; }
            if (it.term != (int)LeaseTerm.Week && it.term != (int)LeaseTerm.Month || !f.Allows((LeaseTerm)it.term))
            {
                q.reason = $"{f.name} is not let by the {((LeaseTerm)it.term == LeaseTerm.Week ? "week" : "month")}.";
                return q;
            }
            PropertyRecord p = PropertyState(it.property, true);
            if (p.lease != null && p.lease.key == key) { q.reason = $"You rent {f.name} already."; return q; }
            long least = MinOfferMilli(it.property);
            if (it.rateMilli < least) { q.reason = $"{f.name} takes offers of at least {Format(least)} a day."; return q; }
            double cap = HousingPricing.BaseDaily(f, Rules) * Math.Max(2f, MR.maxOfferBase) * 1000.0;
            if (it.rateMilli > cap) { q.reason = $"That offer for {f.name} is far too high."; return q; }
            long total = Total(it.rateMilli, Rules.Days((LeaseTerm)it.term));
            if (total > int.MaxValue) { q.reason = "That is more than anyone can pay."; return q; }
            escrow = Math.Max(escrow, total);
        }
        MarketOrder old = OrderOf(key);
        q.escrow = escrow;
        if (old == null) q.delta = escrow;
        else if (OnlyAdds(old.items, items)) q.delta = escrow - old.escrow;
        else
        {
            // It takes something back: the order stands as it was until the notice ends; the larger deposit is held.
            q.later = true;
            q.effectiveIn = OrderNoticeDays;
            q.escrow = Math.Max(old.escrow, escrow);
            q.delta = q.escrow - old.escrow;
        }
        q.ok = true;
        return q;
    }

    /// <summary>
    /// Lease days a change that takes something back (or withdrawing an order) waits: longer than a house takes from being
    /// known to come free (its tenant's notice) to its first clearing, so no offer can be pulled out of the way of a house
    /// that comes free.
    /// </summary>
    public double OrderNoticeDays => (Math.Max(0f, MR.renewalNoticeHours) + Math.Max(0f, MR.firstClearingDelayHours) + 2.0 * MR.tickHours) / 24.0;

    /// <summary>Does the new list only add houses or offer more (every house of the old one kept, at its term, for no less)?</summary>
    private static bool OnlyAdds(List<OrderItem> old, List<OrderItem> now)
    {
        if (old == null) return true;
        foreach (OrderItem o in old)
        {
            if (o == null) continue;
            OrderItem n = now.Find(x => x != null && x.property == o.property);
            if (n == null || n.term != o.term || n.rateMilli < o.rateMilli) return false;
        }
        return true;
    }

    /// <summary>The order's items as they will stand (with the term's days of now: what the deposit is taken for).</summary>
    private List<OrderItem> Built(List<OrderItem> items)
    {
        var list = new List<OrderItem>();
        foreach (OrderItem it in items)
            list.Add(new OrderItem { property = it.property, term = it.term, rateMilli = it.rateMilli, days = Rules.Days((LeaseTerm)it.term) });
        return list;
    }

    private static long EscrowOf(List<OrderItem> items)
    {
        long e = 0;
        foreach (OrderItem it in items) e = Math.Max(e, Total(it.rateMilli, Math.Max(1, it.days)));
        return e;
    }

    /// <summary>
    /// Changes and withdrawals whose notice has ended take effect (before a clearing, and as time passes) - but not those of
    /// an order held for a house: it stands as it is until that house has been offered (a hold usually ends well within
    /// the notice; one that lasts longer - its house's scene not loaded, or a renewal given up at the house limit - keeps
    /// the order to its word until then).
    /// </summary>
    private void ApplyOrderChanges(double t)
    {
        for (int i = Market.orders.Count - 1; i >= 0; i--)
        {
            MarketOrder o = Market.orders[i];
            if (!string.IsNullOrEmpty(o.pin)) continue;
            if (o.cancelAt >= 0.0 && o.cancelAt <= t + 1e-9)
            {
                Market.orders.RemoveAt(i);
                UnindexOrder(o);
                Owe(o.key, o.name, o.escrow, t, "your order's deposit (the order is withdrawn)");
                Changed();
                continue;
            }
            if (o.HasPending && o.pendingAt <= t + 1e-9)
            {
                List<OrderItem> next = o.pending;
                long need = EscrowOf(next);
                SetItems(o, next);
                o.pending = new List<OrderItem>();
                o.pendingAt = -1.0;
                if (o.escrow > need) Owe(o.key, o.name, o.escrow - need, t, "the rest of your order's deposit");
                o.escrow = need;
                Changed();
            }
        }
    }

    /// <summary>
    /// Places (or replaces) the player's order. The caller took <paramref name="paid"/> (= the quote's delta when positive)
    /// from their wallet; a negative delta is owed back to them. False (the caller gives the money back) when the quote changed.
    /// </summary>
    public bool PlaceOrder(string key, string name, List<OrderItem> items, long paid, double now, out string reason)
    {
        OrderQuote q = QuoteOrder(key, items, now);
        reason = q.reason;
        if (!q.ok) return false;
        if (paid != Math.Max(0, q.delta)) { reason = "The price changed: look again."; return false; }
        MarketOrder o = OrderOf(key);
        if (o == null)
        {
            o = new MarketOrder { id = NextId(), key = key, placedAt = now };
            Market.orders.Add(o);
            IndexOrder(o);
        }
        o.name = name ?? "";
        o.editedAt = now;
        if (!q.later)
        {
            // Adding houses or offering more: at once (a withdrawal or a change under way is called off).
            SetItems(o, Built(items));
            o.escrow = q.escrow;
            o.pending = new List<OrderItem>();
            o.pendingAt = -1.0;
            o.cancelAt = -1.0;
            if (q.delta < 0) Owe(key, name, -q.delta, now, "the rest of your order's deposit");
        }
        else
        {
            // Taking something back: the order stands as it is until the notice ends. A new change that takes back nothing
            // beyond the change already waiting keeps its time; one that takes back more waits the whole notice again.
            bool noMore = o.HasPending && OnlyAdds(o.pending, items);
            o.pending = Built(items);
            o.pendingAt = noMore ? o.pendingAt : now + q.effectiveIn;
            o.pendingEscrow = EscrowOf(o.pending);
            o.escrow = q.escrow;
            o.cancelAt = -1.0;
        }
        EnsureAccount(key, name);
        Changed();
        return true;
    }

    /// <summary>
    /// Takes the player's order back: it stands until the order notice ends (it is binding: it may still win a house until
    /// then), then it is withdrawn and its deposit owed back to them. Calling it again changes nothing.
    /// </summary>
    public bool CancelOrder(string key, double now, out string reason)
    {
        reason = "";
        MarketOrder o = OrderOf(key);
        if (o == null) { reason = "You have no order."; return false; }
        if (o.cancelAt >= 0.0) return true;
        o.cancelAt = now + OrderNoticeDays;
        o.pending = new List<OrderItem>();
        o.pendingAt = -1.0;
        Changed();
        return true;
    }

    // ---------------------------------------------------------------- the tenant's renewal
    /// <summary>The tenant's renewal settings: renew or not, and their protection (times the renewal rent). Fixed once the notice is given (only stopping is allowed then).</summary>
    public bool SetRenewal(string propertyId, string key, bool autoRenew, float protection, double now, out string reason)
    {
        reason = "";
        PropertyRecord p = LeaseOf(propertyId, key);
        if (p == null) { reason = "That is not your house."; return false; }
        if (autoRenew && p.movingOut) { reason = "You are moving out: this lease ends at its term."; return false; }
        if (p.noticed && autoRenew && (!p.autoRenew || Math.Abs(protection - EffectiveProtection(p)) > 1e-4f))
        {
            reason = "The renewal is fixed already (it was set a day before the lease ends).";
            return false;
        }
        protection = Math.Max(1f, Math.Min(Math.Max(1f, MR.maxProtection), protection <= 0f ? MR.defaultProtection : protection));
        if (!autoRenew && p.noticed && p.reserved > 0)
        {
            // What was set aside goes back to the account.
            HousingAccount a = EnsureAccount(key, p.lease.name);
            a.balance = Add(a.balance, p.reserved);
            p.reserved = 0;
            p.renewTotal = 0;
        }
        bool stops = p.noticed && p.autoRenew && !autoRenew;
        p.autoRenew = autoRenew;
        p.protection = protection;
        // Stopped after its notice: it comes free at its lease end, so from now on it is priced, and held to, as such.
        if (stops) ComesFreeNow(p, now);
        Changed();
        return true;
    }

    private float EffectiveProtection(PropertyRecord p) => p.protection > 0f ? p.protection : MR.defaultProtection;

    /// <summary>
    /// The most a player's rent account may hold: Max Prepaid Terms of renewals of their dearest lease, the protection premium
    /// included (once the notice is given, the renewal fixed then); 0 without a lease.
    /// </summary>
    public long AccountCap(string key)
    {
        long cap = 0;
        foreach (PropertyRecord p in LeasesOf(key))
        {
            long renewal;
            if (p.noticed && p.renewTotal > 0) renewal = p.renewTotal;
            else
            {
                int days = Rules.Days((LeaseTerm)p.term);
                long rate = Math.Max(p.lease.rateMilli, RenewRateMilli(p));
                renewal = HousingTenure.RenewTotal(rate, HousingTenure.ProtectionMilli(rate, EffectiveProtection(p)), days, MR.protectionPremium);
            }
            cap = Math.Max(cap, renewal * Math.Max(1, MR.maxPrepaidTerms));
        }
        return cap;
    }

    // ---------------------------------------------------------------- time
    private void AdvanceMarket(double now, long utcNow)
    {
        double td = TickDays;
        // The first time: the clearings run from where the books last were (a new book: from its start).
        if (Market.lastTick < 0.0) Market.lastTick = Math.Floor(Math.Min(save.lastAdvance, now) / td) * td;
        if (Market.tickDays <= 0.0) Market.tickDays = td; // an old save: its grid was this one
        else if (Math.Abs(Market.tickDays - td) > 1e-9)
        {
            // Tick Hours changed: every lease end moves onto the new grid (never earlier), so leases still end on clearings.
            Market.tickDays = td;
            foreach (PropertyRecord p in save.properties)
                if (p.lease != null && p.lease.end > Market.lastTick + 1e-9) p.lease.end = Snap(p.lease.end);
            Changed();
        }
        double noticeDays = MR.renewalNoticeHours / 24.0;
        var due = new List<PropertyRecord>();
        long started = budgetMs > 0.0 ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        CatchingUp = false;
        for (int guard = 0; guard < 100000; guard++)
        {
            double nextTick = Market.lastTick + td;
            // Every renewal notice due before the next clearing, in time order: one look over the houses for all of them
            // (Notice makes no lease, so none can fall due meanwhile).
            double limit = Math.Min(now, nextTick);
            due.Clear();
            foreach (PropertyRecord p in save.properties)
                if (p.lease != null && !p.noticed && p.lease.end - noticeDays <= limit) due.Add(p);
            if (due.Count > 0)
            {
                due.Sort((a, b) =>
                {
                    int c = a.lease.end.CompareTo(b.lease.end);
                    return c != 0 ? c : Rank(a).CompareTo(Rank(b));
                });
                foreach (PropertyRecord p in due)
                {
                    if (p.lease == null || p.noticed) continue;
                    try
                    {
                        Notice(p, Math.Max(p.lease.end - noticeDays, p.lease.start), utcNow);
                    }
                    catch (Exception e)
                    {
                        p.noticed = true; // a bug must not stop the market: this lease is noticed without a renewal
                        UnityEngine.Debug.LogException(e);
                    }
                }
            }
            if (nextTick > now)
            {
                ApplyOrderChanges(now);
                break;
            }
            Market.lastTick = nextTick;
            Market.ticks++;
            try
            {
                Clear(nextTick, utcNow);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            // A long jump of the clock (a sleep, a skip on game time) is cleared over several frames, not all in one.
            if (budgetMs > 0.0 && Market.lastTick + td <= now &&
                (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency >= budgetMs)
            {
                CatchingUp = true;
                break;
            }
        }
    }

    /// <summary>A day before a lease ends: its renewal is fixed and the money set aside from the rent account.</summary>
    private void Notice(PropertyRecord p, double at, long utcNow)
    {
        p.noticed = true;
        Tenancy t = p.lease;
        PropertyFacts f = Property(p.id);
        string house = f != null ? f.name : "your house";
        HousingAccount a = EnsureAccount(t.key, t.name);
        bool away = MR.abandonAfterDays > 0f && a.lastSeenAt >= 0.0 && at - a.lastSeenAt > MR.abandonAfterDays;
        // A house the city has not had for a whole term (its scene gone, the house removed) is not let again.
        int termDays = Rules.Days((LeaseTerm)p.term);
        bool gone = f == null && (p.seenAt < 0.0 || at - p.seenAt > termDays);
        if (!p.autoRenew || p.movingOut || away || gone)
        {
            p.renewTotal = 0;
            Tell(t.key, gone
                ? $"The city no longer lets {house}: your lease ends with this term (your rent account keeps its money)."
                : $"Your lease of {house} ends with this term and will not be renewed.", at);
            if (away || gone) p.autoRenew = false;
            if (!gone) ComesFreeNow(p, at);
            Changed();
            return;
        }
        int days = termDays;
        p.renewDays = days;
        p.renewRateMilli = RenewRateMilli(p);
        p.protectionMilli = HousingTenure.ProtectionMilli(p.renewRateMilli, EffectiveProtection(p));
        p.renewTotal = HousingTenure.RenewTotal(p.renewRateMilli, p.protectionMilli, days, MR.protectionPremium);
        // The renewal rests on an offer above the base rent: that offer is held until the house is offered (its lease end - and
        // its first clearing, if the tenant then leaves), so leaving at this price always finds it standing.
        Hold(p, p.renewRateMilli, at, true);
        long take = Math.Min(a.balance, p.renewTotal - p.reserved);
        if (take > 0)
        {
            a.balance -= take;
            p.reserved += take;
        }
        long missing = p.renewTotal - p.reserved;
        Tell(t.key, missing > 0
            ? $"Your lease of {house} renews at the end of this term for {p.renewTotal}: {p.reserved} is set aside from your rent account; deposit {missing} more at a realtor, or it ends."
            : $"Your lease of {house} renews at the end of this term for {p.renewTotal}, paid from your rent account.", at);
        Changed();
    }

    /// <summary>
    /// A house's tenant has their renewal fixed (or the house is to come free) at <paramref name="priceMilli"/> a day, more than
    /// its base rent: the order whose offer that price rests on - the best offer for it from someone free to take it - is held
    /// for the house until it is offered. Until then the order wins no other house, and at that clearing it is tried for this
    /// house first, at its offer, whatever the house asks then. So a price someone made by offering is always theirs to pay if
    /// the tenant leaves: winning a house added for the purpose, or anything else, cannot get them out of it.
    /// </summary>
    private void Hold(PropertyRecord p, long priceMilli, double at, bool renews)
    {
        PropertyFacts f = Property(p.id);
        if (f == null || priceMilli <= BaseAskMilli(f)) return; // no premium: nothing rests on an offer
        long s = BestSupport(p, true, out MarketOrder by);
        if (by == null || s < priceMilli || by.pin == p.id) return;
        by.pin = p.id;
        by.pinAt = at;
        Tell(by.key, (renews ? $"The rent of {f.name}'s tenant was just fixed on your offer" : $"{f.name} comes free at the price your offer stands behind") +
            $": your order is held for {f.name} until it is offered. Until then it wins no other house; then it is tried for {f.name} first, " +
            "so if it is free, your offer takes it.", at);
        Changed();
    }

    /// <summary>
    /// A lease became known to come free - at its notice, or after it (its tenant stopped renewing, or won another house and
    /// is moving): from now on its house is priced as a free one (a premium only as far as an order free to take it stands
    /// behind it), and the offer that price rests on is held to take it.
    /// </summary>
    private void ComesFreeNow(PropertyRecord p, double at)
    {
        PropertyFacts f = Property(p.id);
        if (f == null || p.lease == null) return;
        long honest = HonestAskingMilli(p);
        if (AskingMilli(p.id) > honest) p.level = Math.Min(p.level, LevelAt(f, honest));
        Hold(p, honest, at, false);
    }

    /// <summary>
    /// Holds that are over: the house was offered at this clearing, or is no longer to be (renewed, dropped from the order,
    /// gone from the city for a whole term). A free house that was not offered now has not been offered since it came free
    /// (it waits for its first clearing, came free at this one, or its scene is not loaded): the hold stands until it is.
    /// </summary>
    private void ReleaseHolds(double t, HashSet<string> offeredIds)
    {
        foreach (MarketOrder o in Market.orders)
        {
            if (string.IsNullOrEmpty(o.pin)) continue;
            PropertyRecord p = PropertyState(o.pin, false);
            PropertyFacts f = p != null ? Property(p.id) : null;
            bool coming = p != null && o.items.Exists(x => x != null && x.property == o.pin) &&
                (p.lease != null ? p.noticed && p.lease.key != o.key : p.offeredSince >= 0.0 && !GoneForATerm(p, t));
            if (coming && !offeredIds.Contains(o.pin)) continue;
            if (f != null && offeredIds.Contains(o.pin))
                Tell(o.key, $"{f.name} was offered and is not yours: your order is no longer held for it.", t);
            o.pin = "";
            o.pinAt = -1.0;
            Changed();
        }
    }

    private struct Holder
    {
        public string key;
        public long rate;
        public bool incumbent;
        public double placedAt;
        public long tie;
        public Proposer who;
        public int item;
    }

    /// <summary>
    /// One side of the matching at a clearing: a standing order (its houses in turn), or a sitting tenant renewing their own
    /// house (each funded renewal on its own: a player may keep every house they renew).
    /// </summary>
    private sealed class Proposer
    {
        public string key;
        public MarketOrder order;
        /// <summary>The house a sitting tenant renews now (null: this is an order).</summary>
        public PropertyRecord renewing;
        /// <summary>An order's items to try, in turn (a held order: its house first).</summary>
        public List<int> items;
        public int next;
        public int Count => renewing != null ? 1 : items.Count;
    }

    /// <summary>
    /// A clearing: every free house that has waited long enough, and every lease that ends now and is renewed, goes to the
    /// best offer for it. Each order wins at most one house - its houses in its order of preference; an order held for a
    /// house (<see cref="Hold"/>) tries that one first when it is offered, and nothing before. Each funded renewal holds its
    /// own house against offers within the tenant's protection (1.1 times it), whatever else its tenant wins. A house keeps
    /// the best proposal (deferred acceptance, so the result does not depend on any order of processing): ties to the tenant,
    /// then the older order, then a draw nobody can predict. Then the house limit is applied (a player who won a house while
    /// renting as many as they may gives up a house that ends now first - it comes free like any other - else the lease that
    /// ends soonest at its term), winners pay what they offered, tenants who keep their house pay the renewal fixed at the
    /// notice, and every decision is applied from the state at the start of the clearing.
    /// <para>A lease that ends without being renewed is not auctioned at its end: the house is free from then and offered
    /// after the first-clearing delay like any house that came free, so everyone sees it first (nobody can hand it on).
    /// Then every house's asking rent follows its own demand (<see cref="UpdatePrices"/>), whether or not it was offered.</para>
    /// </summary>
    private void Clear(double t, long utcNow)
    {
        ApplyOrderChanges(t);
        double firstDelay = MR.firstClearingDelayHours / 24.0;
        // The houses the city has now are seen (a lease of a house gone for a whole term is not renewed).
        foreach (PropertyFacts f in properties.Values)
        {
            PropertyRecord seen = PropertyState(f.id, true);
            seen.seenAt = t;
            seen.district = NormDistrict(f.district);
        }
        var offered = new List<PropertyRecord>();
        var renewing = new List<PropertyRecord>();
        foreach (PropertyRecord p in save.properties)
        {
            PropertyFacts f = Property(p.id);
            if (p.lease != null)
            {
                if (p.lease.end > t + 1e-9) continue;
                if (!p.noticed) Notice(p, t, utcNow);
                if (RenewalFunded(p))
                {
                    offered.Add(p);
                    renewing.Add(p);
                    continue;
                }
                string house = f != null ? f.name : "your house";
                bool unfunded = p.autoRenew && !p.movingOut && p.renewTotal > 0;
                EndLease(p, t, unfunded
                    ? $"Your lease of {house} has ended: your rent account did not hold the renewal."
                    : $"Your lease of {house} has ended.");
                // EndLease made it free from now: its first clearing waits the delay, as a house that came free.
            }
            else if (f != null)
            {
                if (p.offeredSince < 0.0) p.offeredSince = t;
                if (t - p.offeredSince + 1e-9 >= firstDelay) offered.Add(p);
            }
        }
        var offeredIds = new HashSet<string>();
        foreach (PropertyRecord p in offered) offeredIds.Add(p.id);
        var unlet = new HashSet<string>();
        HashSet<string> released = offered.Count > 0 ? Match(t, offered, offeredIds, renewing, unlet) : null;
        UpdatePrices(unlet);
        // A renewal given up at the house limit came free now: it was not offered, so its holds stand until its first clearing.
        if (released != null) offeredIds.ExceptWith(released);
        ReleaseHolds(t, offeredIds);
        Changed();
    }

    /// <summary>The matching of a clearing and its results (see <see cref="Clear"/>); returns the renewals given up at the house limit.</summary>
    private HashSet<string> Match(double t, List<PropertyRecord> offered, HashSet<string> offeredIds, List<PropertyRecord> renewing, HashSet<string> unlet)
    {
        // The least offer each house takes at the start of the clearing: the offers are judged against it.
        var askedAtStart = new Dictionary<string, long>();
        foreach (PropertyRecord p in offered)
            if (Property(p.id) != null) askedAtStart[p.id] = MinOfferMilli(p.id);
        var renewingIds = new HashSet<string>();
        foreach (PropertyRecord p in renewing) renewingIds.Add(p.id);

        // The proposers: every standing order (binding: nothing the player does now - claiming, moving - keeps it from
        // counting), and every funded renewal on its own.
        var queue = new List<Proposer>();
        foreach (MarketOrder o in Market.orders)
        {
            var seq = new List<int>();
            int held = string.IsNullOrEmpty(o.pin) ? -1 : o.items.FindIndex(x => x != null && x.property == o.pin);
            if (held >= 0)
            {
                // Held for a house: it tries nothing until that house is offered, and then that house first.
                if (!offeredIds.Contains(o.pin)) continue;
                seq.Add(held);
                if (renewingIds.Contains(o.pin))
                {
                    // At its lease end, that house only: should its tenant keep it, the order is free from the next clearing;
                    // should the renewal be given up at the house limit, the house comes free - and the order is still there.
                    queue.Add(new Proposer { key = o.key, order = o, items = seq });
                    continue;
                }
            }
            for (int i = 0; i < o.items.Count; i++)
                if (i != held && o.items[i] != null) seq.Add(i);
            queue.Add(new Proposer { key = o.key, order = o, items = seq });
        }
        foreach (PropertyRecord p in renewing) queue.Add(new Proposer { key = p.lease.key, renewing = p });
        // Any order gives the same matching; this one is fixed.
        queue.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.key, b.key);
            if (c != 0) return c;
            if ((a.renewing == null) != (b.renewing == null)) return a.renewing == null ? -1 : 1;
            return a.renewing == null ? 0 : string.CompareOrdinal(a.renewing.id, b.renewing.id);
        });
        var free = new Queue<Proposer>(queue);
        var holders = new Dictionary<string, Holder>();
        while (free.Count > 0)
        {
            Proposer pr = free.Dequeue();
            while (pr.next < pr.Count)
            {
                int i = pr.next++;
                Holder mine;
                string house;
                if (pr.order != null)
                {
                    OrderItem it = pr.order.items[pr.items[i]];
                    house = it.property;
                    if (!offeredIds.Contains(house) || !askedAtStart.TryGetValue(house, out long asking)) continue;
                    PropertyRecord p = PropertyState(house, false);
                    if (p.lease != null && p.lease.key == pr.key) continue; // one's own house is renewed, never bid on
                    // A held order's offer is taken at its word, whatever the house asks now.
                    if (it.rateMilli < asking && pr.order.pin != house) continue;
                    mine = new Holder { key = pr.key, rate = it.rateMilli, placedAt = pr.order.placedAt, tie = Tie(t, pr.key), who = pr, item = pr.items[i] };
                }
                else
                {
                    PropertyRecord own = pr.renewing;
                    house = own.id;
                    mine = new Holder { key = pr.key, rate = HousingTenure.BeatMilli(own.protectionMilli, MR.protectionMargin), incumbent = true, who = pr, item = -1 };
                }
                if (holders.TryGetValue(house, out Holder cur) && !Beats(mine, cur)) continue;
                if (holders.TryGetValue(house, out cur)) free.Enqueue(cur.who);
                holders[house] = mine;
                break;
            }
        }

        // The house limit, decided from the state at the start of the clearing. A player who won a house while renting as
        // many as they may gives up a house whose lease ends now first (its renewal is not taken; the house comes free like
        // any other: everyone sees it before its first clearing, so nobody is handed it), then the lease that ends soonest
        // (not the new house) at its term. With Max Houses 1: the old house.
        int maxHouses = Math.Max(1, MR.maxHouses);
        var released = new HashSet<string>();
        var nowComingFree = new List<PropertyRecord>();
        var winners = new List<string>();
        foreach (KeyValuePair<string, Holder> kv in holders)
            if (!kv.Value.incumbent && !winners.Contains(kv.Value.key)) winners.Add(kv.Value.key);
        winners.Sort(string.CompareOrdinal);
        foreach (string key in winners)
        {
            var now = new List<PropertyRecord>();
            var later = new List<PropertyRecord>();
            foreach (PropertyRecord other in LeasesOf(key))
            {
                if (other.lease.end <= t + 1e-9)
                {
                    // Ends now: kept only if its renewal holds it (else it ends anyway).
                    if (holders.TryGetValue(other.id, out Holder h) && h.incumbent && h.key == key) now.Add(other);
                }
                else if (!other.movingOut) later.Add(other);
            }
            int excess = now.Count + later.Count + 1 - maxHouses;
            if (excess <= 0) continue;
            // The newest lease ending now goes first (a player's longest-held home is the last they give up).
            now.Sort((a, b) => a.lease.start != b.lease.start ? b.lease.start.CompareTo(a.lease.start) : string.CompareOrdinal(a.id, b.id));
            for (int i = 0; i < now.Count && excess > 0; i++, excess--)
            {
                holders.Remove(now[i].id);
                released.Add(now[i].id);
            }
            later.Sort((a, b) => a.lease.end != b.lease.end ? a.lease.end.CompareTo(b.lease.end) : string.CompareOrdinal(a.id, b.id));
            for (int i = 0; i < later.Count && excess > 0; i++, excess--)
            {
                PropertyRecord other = later[i];
                if (other.noticed && !ComingFree(other)) nowComingFree.Add(other);
                other.movingOut = true;
                other.autoRenew = false;
                if (other.noticed && other.reserved > 0)
                {
                    HousingAccount acc = EnsureAccount(key, other.lease.name);
                    acc.balance = Add(acc.balance, other.reserved);
                    other.reserved = 0;
                    other.renewTotal = 0;
                }
            }
        }

        offered.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        foreach (PropertyRecord p in offered)
        {
            try
            {
                Settle(p, t, holders, released, unlet);
            }
            catch (Exception e)
            {
                // A bug must not stop the market for everyone: this house waits, the others go on.
                UnityEngine.Debug.LogException(e);
            }
        }
        // Leases whose tenant is moving after their notice come free at their end: priced and held to as such (the orders
        // that won at this clearing are settled by now, so none of them is held).
        foreach (PropertyRecord other in nowComingFree)
            if (other.lease != null && ComingFree(other)) ComesFreeNow(other, t);
        return released;
    }

    /// <summary>What came of one house offered at a clearing.</summary>
    private void Settle(PropertyRecord p, double t, Dictionary<string, Holder> holders, HashSet<string> released, HashSet<string> unlet)
    {
        PropertyFacts f = Property(p.id);
        double firstDelay = MR.firstClearingDelayHours / 24.0;
        string name = f != null ? f.name : "your house";
        if (released.Contains(p.id))
        {
            EndLease(p, t, $"You won another house, and may rent {Math.Max(1, MR.maxHouses)} at most: your lease of {name} has ended " +
                "(its renewal is back in your rent account).");
            // It came free: as at a notice of a house that comes free, the offer its price rests on is held to take it.
            if (f != null) Hold(p, HonestAskingMilli(p), t, false);
            return;
        }
        if (!holders.TryGetValue(p.id, out Holder h))
        {
            if (p.lease != null)
            {
                // A renewal always holds its house; should one not, the house comes free like any other.
                EndLease(p, t, $"Your lease of {name} has ended.");
                return;
            }
            // Nobody took it: it asks a little less (UpdatePrices) and is offered again at every clearing from now on.
            if (f == null) return;
            p.offeredSince = Math.Min(p.offeredSince >= 0.0 ? p.offeredSince : t, t - firstDelay);
            unlet.Add(p.id);
            return;
        }
        if (h.incumbent)
        {
            Renew(p, t);
            return;
        }
        // A new tenant, at what they offered.
        MarketOrder order = h.who.order;
        OrderItem won = order.items[h.item];
        // The term's days as they were when the order was placed (what its deposit was taken for).
        int days = won.days > 0 ? won.days : Rules.Days((LeaseTerm)won.term);
        long total = Total(won.rateMilli, days);
        RemoveOrder(order);
        ToCity(total);
        if (order.escrow > total) Owe(h.key, order.name, order.escrow - total, t, "the rest of your order's deposit");
        if (p.lease != null) EndLease(p, t, f != null ? $"Someone offered more for {f.name}: your lease has ended." : "Your lease has ended.");
        SetLease(p, new Tenancy { id = NextId(), key = h.key, name = order.name, start = t, end = Snap(t + days), rateMilli = won.rateMilli, termDays = days });
        p.term = won.term;
        p.autoRenew = true;
        p.protection = 0f;
        p.noticed = false;
        p.reserved = 0;
        p.renewTotal = 0;
        p.movingOut = false;
        p.offeredSince = -1.0;
        // What the winner offered is theirs to pay for this term; it teaches the market nothing (a winner who offered much
        // more than the asking rent only paid more) - the asking rent follows the offers that still stand, below.
        RecordSale(p, new Sale { at = t, rateMilli = won.rateMilli, days = days });
        Tell(h.key, f != null ? $"{f.name} is yours from now until the lease ends." : "A house is yours.", t);
        Emit(HousingEventKind.AccessChanged, p.id, null, h.key, order.name, null, 0, t);
    }

    private bool RenewalFunded(PropertyRecord p)
    {
        if (p.lease == null || !p.autoRenew || p.movingOut || p.renewTotal <= 0) return false;
        if (p.reserved < p.renewTotal)
        {
            // Deposited since the notice: set aside now.
            HousingAccount a = Account(p.lease.key);
            long take = a != null ? Math.Min(a.balance, p.renewTotal - p.reserved) : 0;
            if (take > 0)
            {
                a.balance -= take;
                p.reserved += take;
            }
        }
        return p.reserved >= p.renewTotal;
    }

    private void Renew(PropertyRecord p, double t)
    {
        Tenancy l = p.lease;
        // The days the renewal was fixed for at the notice (what was paid for).
        int days = p.renewDays > 0 ? p.renewDays : Rules.Days((LeaseTerm)p.term);
        ToCity(p.renewTotal);
        if (p.reserved > p.renewTotal)
        {
            HousingAccount a = EnsureAccount(l.key, l.name);
            a.balance = Add(a.balance, p.reserved - p.renewTotal);
        }
        l.start = t;
        l.end = Snap(t + days);
        l.rateMilli = p.renewRateMilli;
        l.termDays = days;
        RecordSale(p, new Sale { at = t, rateMilli = p.renewRateMilli, days = days, renewal = true });
        PropertyFacts f = Property(p.id);
        Tell(l.key, $"Your lease of {(f != null ? f.name : "your house")} is renewed.", t);
        p.noticed = false;
        p.reserved = 0;
        p.renewTotal = 0;
    }

    private void EndLease(PropertyRecord p, double t, string why)
    {
        Tenancy l = p.lease;
        if (l == null) return;
        if (p.reserved > 0)
        {
            HousingAccount a = EnsureAccount(l.key, l.name);
            a.balance = Add(a.balance, p.reserved);
        }
        PropertyFacts f = Property(p.id);
        SetLease(p, null);
        p.noticed = false;
        p.reserved = 0;
        p.renewTotal = 0;
        p.movingOut = false;
        p.autoRenew = true;
        p.protection = 0f;
        p.offeredSince = t;
        Tell(l.key, why, t);
        EndTenancy(l, p.id, null, f != null ? f.name : "a house", t);
    }

    /// <summary>
    /// After each clearing, every house's asking rent follows its own demand - and only offers that can be taken at their
    /// word move it: an order's offers are binding (taking one back waits for the order notice, and moves no price from the
    /// moment it is asked for), and the offer a tenant's rent rests on holds its order for the house (<see cref="Hold"/>).
    /// Its base rent (what its sizes are worth) is the anchor:
    /// <list type="bullet">
    /// <item>a free house offered and left unlet asks Unlet Step less - below its base too, down to Min Level;</item>
    /// <item>a let house someone besides its tenant offers more for than it asks asks up to Rise Step more - towards the
    /// best such offer, never past it (the offer lost to the tenant's protection, but would take the house the moment it
    /// came free). Its tenant's renewal follows, never faster than Max Rise Rate Per Day;</item>
    /// <item>a house asks more than its base rent only as far as an offer stands behind it: when the best offer goes (it won
    /// another house, or is being taken back) the premium goes with it, down to the next offer or the base rent. A free
    /// house counts only offers from orders free to take it now (not held for another house).</item>
    /// </list>
    /// No level is shared between houses: one empty house never reprices its neighbours, and a price moves only where
    /// someone made a binding offer, or nobody would take it.
    /// </summary>
    private void UpdatePrices(HashSet<string> unlet)
    {
        // The best offer standing behind each house from players who do not rent it: from any order, and from orders free
        // to take it now.
        var any = new Dictionary<string, long>();
        var free = new Dictionary<string, long>();
        foreach (MarketOrder o in Market.orders)
        {
            if (o.items == null) continue;
            foreach (OrderItem it in o.items)
            {
                if (it == null || Property(it.property) == null) continue;
                PropertyRecord rec = PropertyState(it.property, false);
                if (rec == null || (rec.lease != null && rec.lease.key == o.key)) continue;
                long s = SupportOf(o, it);
                if (s <= 0) continue;
                any[it.property] = any.TryGetValue(it.property, out long b) ? Math.Max(b, s) : s;
                if (string.IsNullOrEmpty(o.pin) || o.pin == it.property)
                    free[it.property] = free.TryGetValue(it.property, out long c) ? Math.Max(c, s) : s;
            }
        }
        double down = Math.Log(1.0 + Math.Max(0f, MR.unletStep)), up = Math.Log(1.0 + Math.Max(0f, MR.riseStep));
        foreach (PropertyRecord p in save.properties)
        {
            PropertyFacts f = Property(p.id);
            if (f == null) continue;
            // A house known to come free is priced as a free one: only offers from orders free to take it, and no rise.
            bool let = p.lease != null && !ComingFree(p);
            long offer = (let ? any : free).TryGetValue(p.id, out long v) ? v : 0;
            double target = offer > 0 ? LevelAt(f, offer) : double.NegativeInfinity;
            double level = p.level;
            // A premium only as far as an offer stands behind it.
            if (level > 0.0 && target < level) level = Math.Max(0.0, target);
            if (unlet.Contains(p.id)) level -= down;
            else if (let && target > level) level = Math.Min(level + up, target); // towards the best offer, never past it
            level = Clamp(level, LevelLo, LevelHi);
            if (level != p.level)
            {
                p.level = level;
                Changed();
            }
        }
    }

    /// <summary>Does <paramref name="a"/> beat the house's current holder <paramref name="b"/>? (A sitting tenant keeps ties.)</summary>
    private static bool Beats(Holder a, Holder b)
    {
        if (a.incumbent) return a.rate >= b.rate; // a sitting tenant keeps ties
        if (b.incumbent) return a.rate > b.rate;
        if (a.rate != b.rate) return a.rate > b.rate;
        if (a.placedAt != b.placedAt) return a.placedAt < b.placedAt; // the older order
        return a.tie < b.tie;
    }

    /// <summary>A tie-break nobody can predict or race: a hash of the books' secret, the clearing and the player.</summary>
    private long Tie(double t, string key)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes($"{Market.secret}|{(long)Math.Round(t * 1440.0)}|{key}"));
            return BitConverter.ToInt64(h, 0) & long.MaxValue;
        }
    }

    /// <summary>A short mark next to a player's name in every housing list (two players called alike are told apart).</summary>
    public string Fingerprint(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        using (SHA256 sha = SHA256.Create())
        {
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes($"fp|{Market.secret}|{key}"));
            var b = new StringBuilder(4);
            for (int i = 0; i < 4; i++) b.Append(alphabet[h[i] % alphabet.Length]);
            return b.ToString();
        }
    }

    /// <summary>A rent per day as words, rounded UP to the cent (a minimum named is one that is accepted), in any culture: "7.14".</summary>
    private static string Format(long milli)
    {
        long cents = (Math.Max(0L, milli) + 9) / 10;
        string whole = (cents / 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return cents % 100 == 0 ? whole : whole + "." + (cents % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
