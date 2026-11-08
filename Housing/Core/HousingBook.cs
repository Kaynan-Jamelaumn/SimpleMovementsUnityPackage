using System;
using System.Collections.Generic;

/// <summary>Someone's stay in an inn room or lease of a house: who, from when to when, at what rate, and who they let in.</summary>
[Serializable]
public class Tenancy
{
    /// <summary>Names this tenancy (a renewal keeps it, a new tenant gets a new one): what is in its chests is theirs.</summary>
    public long id;
    public string key = "";
    public string name = "";
    public double start;
    public double end;
    /// <summary>Rent per day in thousandths of the currency (a house) or the price of a night (an inn room, whole currency).</summary>
    public long rateMilli;
    /// <summary>Days of the term (a house: 7 or 28...; a room: the nights paid).</summary>
    public int termDays;
    public List<HousingGrant> grants = new List<HousingGrant>();

    public bool Active(double now) => !string.IsNullOrEmpty(key) && now < end;
}

/// <summary>A message kept for a player until they read it (a lease ends, an order won, items held...).</summary>
[Serializable]
public class HousingNotice
{
    public double at;
    public string text = "";
}

/// <summary>A player's account with the city's housing office: prepaid rent, money owed to them, their messages.</summary>
[Serializable]
public class HousingAccount
{
    public string key = "";
    public string name = "";
    /// <summary>Prepaid rent (deposited by the player; renewals are paid from it; withdrawable).</summary>
    public long balance;
    /// <summary>Owed to the player (refunds, returned deposits): paid into their wallet whenever they are online.</summary>
    public long owed;
    /// <summary>When the player was first and last seen (UTC ticks, for display), when last seen in lease days (absence is counted only
    /// while the world runs), and how long they have been online in all.</summary>
    public long firstSeenUtc;
    public long lastSeenUtc;
    public double lastSeenAt = -1.0;
    public double onlineSeconds;
    /// <summary>Messages not read yet (the newest last; the oldest go beyond the limit).</summary>
    public List<HousingNotice> notices = new List<HousingNotice>();
}

/// <summary>An inn's state: how full it has been, its price level, its rooms' stays.</summary>
[Serializable]
public class InnRecord
{
    public string id = "";
    /// <summary>Occupancy (of rooms in use) averaged over the last few days (0-1).</summary>
    public double occupancy;
    /// <summary>Natural log of the inn's price level (0 = the inn's own nightly price), from the occupancy.</summary>
    public double level;
    public double lastTick = -1.0;
    public List<RoomRecord> rooms = new List<RoomRecord>();
}

[Serializable]
public class RoomRecord
{
    public string id = "";
    public Tenancy stay;
    /// <summary>When the guest (or someone they let in) was last in the room or asleep in its bed (lease days).</summary>
    public double present = -1.0;

    public bool Taken(double now) => stay != null && stay.Active(now);
}

/// <summary>What the rules did that the world must follow (move a chest's contents, lock doors, tell a player).</summary>
public enum HousingEventKind
{
    /// <summary>A stay or lease ended: the place's chests are emptied into held items for <see cref="HousingEvent.key"/>; access is gone.</summary>
    TenancyEnded,
    /// <summary>A place has a new tenant (or lost one): doors, beds, chests and gardens follow.</summary>
    AccessChanged,
    /// <summary>A message for a player (when they are online).</summary>
    Notice,
    /// <summary>Money became owed to a player (paid out when they are online).</summary>
    Owed,
    /// <summary>Held items were thrown away (kept too long).</summary>
    HeldDiscarded,
}

/// <summary>One thing the world must follow.</summary>
public struct HousingEvent
{
    public HousingEventKind kind;
    /// <summary>The place: a property id, or an inn id with <see cref="roomId"/>.</summary>
    public string placeId;
    public string roomId;
    public string key;
    public string name;
    public string text;
    public long amount;
    public double at;
}

/// <summary>
/// The housing office's books, as plain C#: the city's properties and inns as the rules see them, every tenancy, the
/// accounts, what is held for players, the market. Times are lease days (a clock the caller passes in), money is whole
/// currency in <c>long</c>. Nothing here touches a wallet or a chest: the caller pays and takes, then records it here;
/// what the world must follow comes out as <see cref="Events"/>. Saved and loaded as one <see cref="HousingSave"/>.
/// </summary>
public sealed partial class HousingBook
{
    private HousingSave save = new HousingSave();
    private readonly Dictionary<string, PropertyFacts> properties = new Dictionary<string, PropertyFacts>();
    private readonly Dictionary<string, InnFacts> inns = new Dictionary<string, InnFacts>();

    public HousingBook(HousingRules rules)
    {
        Rules = rules ?? HousingRules.Defaults();
    }

    public HousingRules Rules { get; set; }

    /// <summary>What the world must follow, in the order it happened (the caller empties it).</summary>
    public readonly List<HousingEvent> Events = new List<HousingEvent>();

    /// <summary>Changes on every change of the books (views and saves follow it).</summary>
    public int Revision { get; private set; }

    public HousingSave Save => save;

    private void Changed() => Revision++;

    private long NextId() => save.nextId++;

    private void Emit(HousingEventKind kind, string placeId, string roomId, string key, string name, string text, long amount, double at)
    {
        Events.Add(new HousingEvent { kind = kind, placeId = placeId, roomId = roomId, key = key, name = name, text = text, amount = amount, at = at });
    }

    /// <summary>Messages kept per player.</summary>
    public const int MaxNotices = 30;

    /// <summary>Tells a player: kept in their messages (they read them at any innkeeper or realtor) and shown at once if they are online.</summary>
    private void Tell(string key, string text, double at)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
        HousingAccount a = EnsureAccount(key, null);
        a.notices.Add(new HousingNotice { at = at, text = text });
        if (a.notices.Count > MaxNotices) a.notices.RemoveRange(0, a.notices.Count - MaxNotices);
        Emit(HousingEventKind.Notice, null, null, key, null, text, 0, at);
    }

    /// <summary>The player read their messages.</summary>
    public void ReadNotices(string key)
    {
        HousingAccount a = Account(key);
        if (a == null || a.notices.Count == 0) return;
        a.notices.Clear();
        Changed();
    }

    // ---------------------------------------------------------------- the world
    /// <summary>The properties and inns of the city as they are now (the components' facts); call after loading.</summary>
    public void SetWorld(IEnumerable<PropertyFacts> props, IEnumerable<InnFacts> innList)
    {
        properties.Clear();
        inns.Clear();
        if (props != null)
            foreach (PropertyFacts p in props)
                if (p != null && !string.IsNullOrEmpty(p.id) && !properties.ContainsKey(p.id)) properties[p.id] = p;
        if (innList != null)
            foreach (InnFacts i in innList)
                if (i != null && !string.IsNullOrEmpty(i.id) && !inns.ContainsKey(i.id)) inns[i.id] = i;
        foreach (InnFacts i in inns.Values) InnState(i.id);
        WorldChanged();
        Changed();
    }

    public PropertyFacts Property(string id) => id != null && properties.TryGetValue(id, out PropertyFacts f) ? f : null;
    public InnFacts Inn(string id) => id != null && inns.TryGetValue(id, out InnFacts f) ? f : null;
    public IEnumerable<PropertyFacts> AllProperties => properties.Values;
    public IEnumerable<InnFacts> AllInns => inns.Values;

    // ---------------------------------------------------------------- saving
    /// <summary>Takes a save (the facts of the world come from <see cref="SetWorld"/>).</summary>
    public void Load(HousingSave s)
    {
        save = s ?? new HousingSave();
        save.Repair();
        Reindex();
        Changed();
    }

    // Lookups by id (records are only ever removed when a save is repaired at load: the indexes are rebuilt then).
    private readonly Dictionary<string, HousingAccount> accountIndex = new Dictionary<string, HousingAccount>();
    private readonly Dictionary<string, PropertyRecord> propertyIndex = new Dictionary<string, PropertyRecord>();
    private readonly Dictionary<string, InnRecord> innIndex = new Dictionary<string, InnRecord>();

    private void Reindex()
    {
        accountIndex.Clear();
        foreach (HousingAccount a in save.accounts)
            if (!accountIndex.ContainsKey(a.key)) accountIndex[a.key] = a;
        propertyIndex.Clear();
        foreach (PropertyRecord p in save.properties)
            if (!propertyIndex.ContainsKey(p.id)) propertyIndex[p.id] = p;
        innIndex.Clear();
        foreach (InnRecord i in save.inns)
            if (!innIndex.ContainsKey(i.id)) innIndex[i.id] = i;
        ReindexMarket();
    }

    // ---------------------------------------------------------------- accounts
    public HousingAccount Account(string key) => !string.IsNullOrEmpty(key) && accountIndex.TryGetValue(key, out HousingAccount a) ? a : null;

    public HousingAccount EnsureAccount(string key, string name)
    {
        if (string.IsNullOrEmpty(key)) return null;
        HousingAccount a = Account(key);
        if (a == null)
        {
            a = new HousingAccount { key = key, name = name ?? "" };
            save.accounts.Add(a);
            accountIndex[key] = a;
            Changed();
        }
        else if (!string.IsNullOrEmpty(name) && a.name != name)
        {
            a.name = name;
            Changed();
        }
        return a;
    }

    /// <summary>
    /// A player was seen (their name may have changed; their records follow it); <paramref name="onlineSeconds"/> more of
    /// their time online counts towards their residency.
    /// </summary>
    public void Seen(string key, string name, long utcTicks, double onlineSeconds = 0.0)
    {
        HousingAccount a = EnsureAccount(key, name);
        if (a == null) return;
        if (a.firstSeenUtc == 0) a.firstSeenUtc = utcTicks;
        a.lastSeenUtc = utcTicks;
        a.lastSeenAt = Math.Max(a.lastSeenAt, save.lastAdvance);
        if (onlineSeconds > 0.0) a.onlineSeconds += onlineSeconds;
        if (string.IsNullOrEmpty(name)) return;
        foreach (PropertyRecord p in save.properties)
            if (p.lease != null && p.lease.key == key) p.lease.name = name;
        foreach (InnRecord i in save.inns)
            foreach (RoomRecord r in i.rooms)
                if (r.stay != null && r.stay.key == key) r.stay.name = name;
    }

    /// <summary>Money owed to a player (refunds): paid out when they are online.</summary>
    public void Owe(string key, string name, long amount, double at, string why = null)
    {
        if (amount <= 0 || string.IsNullOrEmpty(key)) return;
        HousingAccount a = EnsureAccount(key, name);
        a.owed = Add(a.owed, amount);
        Emit(HousingEventKind.Owed, null, null, key, name, why, amount, at);
        Changed();
    }

    /// <summary>The caller paid <paramref name="amount"/> of what is owed into the player's wallet.</summary>
    public void PaidOut(string key, long amount)
    {
        HousingAccount a = Account(key);
        if (a == null || amount <= 0) return;
        a.owed = Math.Max(0, a.owed - amount);
        Changed();
    }

    /// <summary>How much more the player may deposit (rent accounts hold at most a few terms, and only for tenants).</summary>
    public long DepositRoom(string key)
    {
        HousingAccount a = Account(key);
        long reserved = 0;
        foreach (PropertyRecord p in LeasesOf(key)) reserved += p.reserved;
        return Math.Max(0, AccountCap(key) - (a != null ? a.balance : 0) - reserved);
    }

    /// <summary>The caller took <paramref name="amount"/> from the player's wallet into their account (false: more than it may hold).</summary>
    public bool Deposited(string key, string name, long amount)
    {
        if (amount <= 0 || amount > DepositRoom(key)) return false;
        HousingAccount a = EnsureAccount(key, name);
        a.balance = Add(a.balance, amount);
        Changed();
        return true;
    }

    /// <summary>Takes up to <paramref name="amount"/> out of the account for the caller to put in the wallet (what was taken).</summary>
    public long Withdraw(string key, long amount)
    {
        HousingAccount a = Account(key);
        if (a == null || amount <= 0) return 0;
        long take = Math.Min(amount, a.balance);
        a.balance -= take;
        if (take > 0) Changed();
        return take;
    }

    /// <summary>
    /// Puts back what <see cref="Withdraw"/> took when the purse could not take it (no cap: the money was in the account a
    /// moment ago, and a rent account may hold more than its cap once a lease ended).
    /// </summary>
    public void UndoWithdraw(string key, string name, long amount)
    {
        if (string.IsNullOrEmpty(key) || amount <= 0) return;
        HousingAccount a = EnsureAccount(key, name);
        a.balance = Add(a.balance, amount);
        Changed();
    }

    /// <summary>Money the city kept (rents, fees): a sink, recorded for the books.</summary>
    private void ToCity(long amount)
    {
        if (amount > 0) save.citySink = Add(save.citySink, amount);
    }

    private static long Add(long a, long b)
    {
        long s = a + b;
        return s < a ? long.MaxValue : s;
    }

    // ---------------------------------------------------------------- held items
    /// <summary>Holds what was left in a place's chests for its tenant (nothing when it is empty). The id, or 0.</summary>
    public long Hold(string key, string name, string source, List<HeldStack> stacks, double now)
    {
        if (string.IsNullOrEmpty(key) || stacks == null || stacks.Count == 0) return 0;
        var b = new HeldBundle { id = NextId(), key = key, name = name ?? "", source = source ?? "", createdAt = now, stacks = stacks };
        save.held.Add(b);
        Tell(key, $"What you left in {source} is kept for you: claim it from any innkeeper or realtor.", now);
        Changed();
        return b.id;
    }

    /// <summary>Everything held for a player.</summary>
    public List<HeldBundle> HeldFor(string key) => save.held.FindAll(b => b.key == key);

    /// <summary>How many stacks are held for a player.</summary>
    public int HeldStacks(string key)
    {
        int n = 0;
        foreach (HeldBundle b in save.held)
            if (b.key == key) n += b.stacks.Count;
        return n;
    }

    public HeldBundle Held(long id) => save.held.Find(b => b.id == id);

    /// <summary>What a claim of a bundle costs now (free within Free Hold Days of when it was held).</summary>
    public long ClaimFee(HeldBundle b, double now)
    {
        if (b == null || b.feePaid) return 0; // paid once already: the rest of a partial claim is free
        HeldRules h = Rules.Held();
        return now - b.createdAt <= h.freeDays ? 0 : h.fee;
    }

    /// <summary>
    /// The caller gave the player what fitted of a bundle: <paramref name="left"/> is what is still held (null or empty:
    /// all of it was taken, the bundle is gone).
    /// </summary>
    public void Claimed(long id, List<HeldStack> left)
    {
        HeldBundle b = Held(id);
        if (b == null) return;
        // The caller took the fee when one was due now: what is left of the bundle has been paid for.
        if (ClaimFee(b, Now) > 0) b.feePaid = true;
        if (left == null || left.Count == 0) save.held.Remove(b);
        else b.stacks = left;
        Changed();
    }

    private void ExpireHeld(double now, long utcNow)
    {
        HeldRules h = Rules.Held();
        for (int i = save.held.Count - 1; i >= 0; i--)
        {
            HeldBundle b = save.held[i];
            bool tooOld = h.keepDays > 0 && now - b.createdAt > h.keepDays;
            bool forgotten = false;
            if (h.forgetAfterDays > 0)
            {
                // Absence counts only while the world runs: a server down for a month forgets nobody.
                HousingAccount a = Account(b.key);
                forgotten = a != null && a.lastSeenAt >= 0.0 && now - a.lastSeenAt > h.forgetAfterDays;
            }
            if (!tooOld && !forgotten) continue;
            save.held.RemoveAt(i);
            // Kept in their messages (they are most likely away): they read it at any innkeeper or realtor.
            Tell(b.key, $"What was kept for you from {b.source} was thrown away: it was not claimed in time.", now);
            Emit(HousingEventKind.HeldDiscarded, null, null, b.key, b.name, b.source, 0, now);
            Changed();
        }
    }

    // ---------------------------------------------------------------- permissions
    /// <summary>The tenancy of a place now (a property's lease, an inn room's stay), or null.</summary>
    public Tenancy TenancyOf(string placeId, string roomId, double now)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            PropertyRecord p = PropertyState(placeId, false);
            return p?.lease != null && p.lease.Active(now) ? p.lease : null;
        }
        RoomRecord r = RoomState(placeId, roomId, false);
        return r != null && r.Taken(now) ? r.stay : null;
    }

    /// <summary>What a player may do in a place now: everything for its tenant, what they were let do for others.</summary>
    public HousingAccess AccessOf(string placeId, string roomId, string key, double now)
    {
        if (string.IsNullOrEmpty(key)) return HousingAccess.None;
        Tenancy t = TenancyOf(placeId, roomId, now);
        if (t == null) return HousingAccess.None;
        if (t.key == key) return HousingAccess.All;
        foreach (HousingGrant g in t.grants)
            if (g.key == key) return g.Access;
        return HousingAccess.None;
    }

    /// <summary>
    /// Lets someone in, changes what they may do, or (<see cref="HousingAccess.None"/>) lets them out. The tenant may give
    /// anything. Someone with Manage may give only what they may do themselves (never Manage), and change or remove only
    /// those they let in; removing a manager removes everyone they let in. At most Max Grants people, two of them managers.
    /// </summary>
    public bool Grant(string placeId, string roomId, string byKey, string toKey, string toName, HousingAccess access, double now, out string reason)
    {
        reason = "";
        Tenancy t = TenancyOf(placeId, roomId, now);
        if (t == null) { reason = "Nobody rents this place."; return false; }
        if (string.IsNullOrEmpty(toKey)) { reason = "That player cannot be given a key (they have no saved character)."; return false; }
        if (toKey == t.key) { reason = "That is the tenant."; return false; }
        if (toKey == byKey) { reason = "You cannot change your own key."; return false; }
        bool tenant = byKey == t.key;
        HousingAccess mine = AccessOf(placeId, roomId, byKey, now);
        if (!tenant && (mine & HousingAccess.Manage) == 0) { reason = "Only the tenant (or someone they let manage) can do that."; return false; }
        access &= HousingAccess.All;
        HousingGrant existing = t.grants.Find(g => g.key == toKey);
        if (!tenant)
        {
            if ((access & ~(mine & ~HousingAccess.Manage)) != 0) { reason = "You can only give what you may do yourself (never Manage)."; return false; }
            if (existing != null && existing.grantedBy != byKey) { reason = "Only the tenant can change someone you did not let in."; return false; }
        }
        if (access == HousingAccess.None)
        {
            if (existing == null) { reason = "They were not let in."; return false; }
            Revoke(t, existing, placeId, roomId, now);
        }
        else if (existing != null)
        {
            bool wasManager = (existing.Access & HousingAccess.Manage) != 0;
            if (!wasManager && (access & HousingAccess.Manage) != 0 && Managers(t) >= 2) { reason = "At most two people can manage a place."; return false; }
            existing.Access = access;
            if (tenant) existing.grantedBy = t.key; // the tenant's own choice now: it no longer goes with the manager who made it
            if (!string.IsNullOrEmpty(toName)) existing.name = toName;
            if (wasManager && (access & HousingAccess.Manage) == 0) Cascade(t, existing.key, placeId, roomId, now);
            Emit(HousingEventKind.AccessChanged, placeId, roomId, toKey, toName, null, 0, now);
        }
        else
        {
            if (t.grants.Count >= Math.Max(1, Rules.maxGrants)) { reason = $"At most {Rules.maxGrants} people can be let in."; return false; }
            if ((access & HousingAccess.Manage) != 0 && Managers(t) >= 2) { reason = "At most two people can manage a place."; return false; }
            t.grants.Add(new HousingGrant { key = toKey, name = toName ?? "", Access = access, grantedBy = byKey });
            Emit(HousingEventKind.AccessChanged, placeId, roomId, toKey, toName, null, 0, now);
        }
        if (!tenant) Tell(t.key, $"{NameOf(byKey)} changed who may use {PlaceName(placeId, roomId)}.", now);
        Changed();
        return true;
    }

    /// <summary>The grant whose fingerprint is <paramref name="fingerprint"/> (how windows name someone let in, never by their key).</summary>
    public HousingGrant GrantByFingerprint(string placeId, string roomId, string fingerprint, double now)
    {
        Tenancy t = TenancyOf(placeId, roomId, now);
        return t?.grants.Find(g => Fingerprint(g.key) == fingerprint);
    }

    private static int Managers(Tenancy t)
    {
        int n = 0;
        foreach (HousingGrant g in t.grants)
            if ((g.Access & HousingAccess.Manage) != 0) n++;
        return n;
    }

    private void Revoke(Tenancy t, HousingGrant g, string placeId, string roomId, double now)
    {
        t.grants.Remove(g);
        Emit(HousingEventKind.AccessChanged, placeId, roomId, g.key, g.name, null, 0, now);
        if ((g.Access & HousingAccess.Manage) != 0) Cascade(t, g.key, placeId, roomId, now);
    }

    /// <summary>Everyone a manager let in goes with the manager's right to manage.</summary>
    /// <summary>Everyone a manager let in goes with the manager's right to manage - and those they let in, all the way down.</summary>
    private void Cascade(Tenancy t, string managerKey, string placeId, string roomId, double now)
    {
        var gone = new Queue<string>();
        gone.Enqueue(managerKey);
        var seen = new HashSet<string> { managerKey };
        while (gone.Count > 0)
        {
            string by = gone.Dequeue();
            for (int i = t.grants.Count - 1; i >= 0; i--)
            {
                HousingGrant g = t.grants[i];
                if (g.grantedBy != by) continue;
                t.grants.RemoveAt(i);
                Emit(HousingEventKind.AccessChanged, placeId, roomId, g.key, g.name, null, 0, now);
                if ((g.Access & HousingAccess.Manage) != 0 && seen.Add(g.key)) gone.Enqueue(g.key);
            }
        }
    }

    private string NameOf(string key)
    {
        HousingAccount a = Account(key);
        return a != null && !string.IsNullOrEmpty(a.name) ? a.name : "Someone";
    }

    private string PlaceName(string placeId, string roomId)
    {
        if (!string.IsNullOrEmpty(roomId)) return RoomSource(placeId, roomId);
        PropertyFacts f = Property(placeId);
        return f != null ? f.name : "your house";
    }

    // ---------------------------------------------------------------- time
    /// <summary>
    /// The books move to <paramref name="now"/>: stays and leases that ended, auctions that closed, renewals, the inns'
    /// prices, held items kept too long - each at the time it happened, in order. <paramref name="utcNow"/> is the real
    /// time (for forgetting players gone for good).
    /// </summary>
    /// <summary>The time the books have reached (every check, quote and view reads this).</summary>
    public double Now => save.lastAdvance;

    public void Advance(double now, long utcNow)
    {
        if (now < save.lastAdvance) now = save.lastAdvance; // never backwards
        AdvanceMarket(now, utcNow);
        AdvanceInns(now);
        ExpireHeld(now, utcNow);
        save.lastAdvance = now;
    }

    private void EndTenancy(Tenancy t, string placeId, string roomId, string source, double at)
    {
        if (t == null || string.IsNullOrEmpty(t.key)) return;
        Emit(HousingEventKind.TenancyEnded, placeId, roomId, t.key, t.name, source, 0, at);
        Emit(HousingEventKind.AccessChanged, placeId, roomId, t.key, t.name, null, 0, at);
        foreach (HousingGrant g in t.grants) Emit(HousingEventKind.AccessChanged, placeId, roomId, g.key, g.name, null, 0, at);
    }
}
