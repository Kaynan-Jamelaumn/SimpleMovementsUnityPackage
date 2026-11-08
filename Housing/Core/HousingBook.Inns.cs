using System;
using System.Collections.Generic;

/// <summary>What a stay (or an extension of one) would be: the room, the price of a night, the total, until when.</summary>
public struct InnQuote
{
    public bool ok;
    public string reason;
    public string roomId;
    public string roomName;
    public long nightly;
    public long total;
    public int nights;
    public double end;
}

public sealed partial class HousingBook
{
    // ---------------------------------------------------------------- inns: state
    /// <summary>The state of an inn (made the first time; its rooms follow the inn's facts).</summary>
    private InnRecord InnRecordOf(string innId) => !string.IsNullOrEmpty(innId) && innIndex.TryGetValue(innId, out InnRecord r) ? r : null;

    private InnRecord InnState(string innId)
    {
        InnRecord r = InnRecordOf(innId);
        if (r == null)
        {
            r = new InnRecord { id = innId };
            save.inns.Add(r);
            innIndex[innId] = r;
        }
        InnFacts f = Inn(innId);
        if (f != null)
            foreach (InnRoomFacts room in f.rooms)
                if (r.rooms.Find(x => x.id == room.id) == null) r.rooms.Add(new RoomRecord { id = room.id });
        return r;
    }

    private RoomRecord RoomState(string innId, string roomId, bool create)
    {
        InnRecord i = create ? InnState(innId) : InnRecordOf(innId);
        return i?.rooms.Find(x => x.id == roomId);
    }

    private static int Taken(InnRecord r, double now)
    {
        int n = 0;
        foreach (RoomRecord room in r.rooms)
            if (room.Taken(now)) n++;
        return n;
    }

    /// <summary>Rooms taken and in use (their guest was there lately, or the stay just began): what prices follow.</summary>
    private int InUse(InnRecord r, double now)
    {
        InnRules ir = Rules.Inns();
        double active = ir.activeHours / 24.0;
        double justLet = Math.Min(ir.justLetHours, ir.activeHours) / 24.0;
        int n = 0;
        // A room counts while its guest (or someone they let in) was there lately; a room just let counts for a short
        // while only - rooms booked and never used do not raise anyone's price.
        foreach (RoomRecord room in r.rooms)
            if (room.Taken(now) && (now - room.stay.start <= justLet || (room.present >= 0.0 && now - room.present <= active))) n++;
        return n;
    }

    /// <summary>The guest of a room (or someone they let in) is in it or asleep in its bed now (the caller looks).</summary>
    public void Present(string innId, string roomId, double now)
    {
        RoomRecord room = RoomState(innId, roomId, false);
        if (room == null || !room.Taken(now)) return;
        room.present = now;
    }

    /// <summary>The room a player has in an inn now (null: none).</summary>
    public RoomRecord RoomOf(string innId, string key, double now)
    {
        InnRecord r = InnRecordOf(innId);
        if (r == null || string.IsNullOrEmpty(key)) return null;
        foreach (RoomRecord room in r.rooms)
            if (room.Taken(now) && room.stay.key == key) return room;
        return null;
    }

    /// <summary>How many inn rooms a player has now, in all the inns.</summary>
    public int RoomsOf(string key, double now)
    {
        int n = 0;
        foreach (InnRecord r in save.inns)
            foreach (RoomRecord room in r.rooms)
                if (room.Taken(now) && room.stay.key == key) n++;
        return n;
    }

    /// <summary>Rooms of the inn free now.</summary>
    public int FreeRooms(string innId, double now)
    {
        InnFacts f = Inn(innId);
        if (f == null) return 0;
        InnRecord r = InnState(innId);
        int free = 0;
        foreach (InnRoomFacts room in f.rooms)
        {
            RoomRecord rec = r.rooms.Find(x => x.id == room.id);
            if (rec == null || !rec.Taken(now)) free++;
        }
        return free;
    }

    /// <summary>The soonest a taken room of the inn frees up (-1 when none is taken).</summary>
    public double NextFree(string innId, double now)
    {
        InnRecord r = InnRecordOf(innId);
        double best = -1.0;
        if (r == null) return best;
        foreach (RoomRecord room in r.rooms)
            if (room.Taken(now) && (best < 0.0 || room.stay.end < best)) best = room.stay.end;
        return best;
    }

    // ---------------------------------------------------------------- inns: prices
    /// <summary>
    /// The inn's occupancy average and price level move to <paramref name="t"/>: the share of rooms in use (as it was since
    /// the last tick) is averaged over Occupancy Days, and the price level is e^(Price Response x (average - target)) within
    /// its bounds - it has no memory beyond that average, so a burst of bookings fades with it.
    /// </summary>
    private void TickInn(InnRecord r, InnFacts f, double t)
    {
        if (f == null) return;
        InnRules ir = Rules.Inns();
        int rooms = Math.Max(1, f.rooms.Count);
        if (r.lastTick < 0.0)
        {
            r.lastTick = t;
            r.occupancy = ir.targetOccupancy; // a new inn starts as full as it is meant to be: its own price
            r.level = 0.0;
            return;
        }
        double dt = t - r.lastTick;
        if (dt <= 0.0) return;
        double occ = InUse(r, r.lastTick + dt * 0.5) / (double)rooms;
        double tau = Math.Max(0.01, ir.occupancyDays);
        r.occupancy += (occ - r.occupancy) * (1.0 - Math.Exp(-dt / tau));
        double lo = Math.Log(Math.Max(0.01, ir.minLevel)), hi = Math.Log(Math.Max(ir.minLevel, ir.maxLevel));
        r.level = Math.Min(hi, Math.Max(lo, ir.priceResponse * (r.occupancy - ir.targetOccupancy)));
        r.lastTick = t;
    }

    /// <summary>The price of a night in a room with <paramref name="takenAfter"/> rooms taken once it is let.</summary>
    private long NightPrice(InnFacts f, InnRecord r, InnRoomFacts room, int takenAfter)
    {
        InnRules ir = Rules.Inns();
        int rooms = Math.Max(1, f.rooms.Count);
        double after = Math.Min(1.0, takenAfter / (double)rooms);
        double target = Math.Min(0.99, Math.Max(0.01, ir.targetOccupancy));
        double surcharge = 1.0 + ir.scarcity * Math.Max(0.0, after - target) / (1.0 - target);
        double price = Math.Max(1, f.nightlyPrice) * Math.Max(0.1, room.quality) * Math.Exp(r.level) * surcharge;
        return Math.Max(1L, (long)Math.Ceiling(price - 1e-9));
    }

    /// <summary>
    /// The hour of the game's day now (0-24), when lease days are game days: stays end at the checkout hour. Negative
    /// (real time): a stay is whole days. Set by the caller.
    /// </summary>
    public double HourOfDay { get; set; } = -1.0;

    /// <summary>When a stay of <paramref name="nights"/> begun at <paramref name="start"/> ends.</summary>
    public double StayEnd(double start, int nights)
    {
        nights = Math.Max(1, nights);
        if (HourOfDay < 0.0) return start + nights;
        InnRules ir = Rules.Inns();
        // Hours from now to the first checkout at least Min Night Hours away, then whole days.
        double hour = HourOfDay % 24.0;
        double until = ir.checkoutHour - hour;
        while (until < ir.minNightHours) until += 24.0;
        return start + until / 24.0 + (nights - 1);
    }

    // ---------------------------------------------------------------- inns: renting
    /// <summary>
    /// What a stay of <paramref name="nights"/> would be for a player: in <paramref name="roomId"/>, or (null) the cheapest
    /// free room. Refused (ok false, with a reason) when the inn is full, the player already has a room here or too many
    /// elsewhere, or the nights are out of bounds.
    /// </summary>
    public InnQuote QuoteStay(string innId, string roomId, string key, int nights, double now)
    {
        var q = new InnQuote { nights = nights };
        InnFacts f = Inn(innId);
        if (f == null || f.rooms.Count == 0) { q.reason = "This inn has no rooms to let."; return q; }
        if (string.IsNullOrEmpty(key)) { q.reason = "Only a saved character can take a room."; return q; }
        InnRules ir = Rules.Inns();
        if (nights < 1 || nights > Math.Max(1, ir.maxNights)) { q.reason = $"A stay is 1 to {Math.Max(1, ir.maxNights)} nights."; return q; }
        InnRecord r = InnState(innId);
        TickInn(r, f, now);
        if (RoomOf(innId, key, now) != null) { q.reason = "You already have a room here (you can stay longer)."; return q; }
        if (HeldStacks(key) > Rules.Held().maxStacks) { q.reason = "Claim what is kept for you first (an innkeeper or a realtor has it)."; return q; }
        if (RoomsOf(key, now) >= Math.Max(1, Rules.maxInnRooms)) { q.reason = $"You have {Rules.maxInnRooms} room(s) already."; return q; }
        int taken = Taken(r, now);
        InnRoomFacts pick = null;
        long best = long.MaxValue;
        foreach (InnRoomFacts room in f.rooms)
        {
            RoomRecord rec = r.rooms.Find(x => x.id == room.id);
            if (rec != null && rec.Taken(now)) continue;
            if (!string.IsNullOrEmpty(roomId) && room.id != roomId) continue;
            long p = NightPrice(f, r, room, taken + 1);
            if (p < best) { best = p; pick = room; }
        }
        if (pick == null)
        {
            q.reason = string.IsNullOrEmpty(roomId) ? "Every room is taken." : "That room is taken.";
            return q;
        }
        q.roomId = pick.id;
        q.roomName = pick.name;
        q.nightly = best;
        q.total = best * nights;
        if (q.total > int.MaxValue) { q.reason = "That is more than anyone can pay."; return q; }
        q.end = StayEnd(now, nights);
        q.ok = true;
        return q;
    }

    /// <summary>
    /// The player paid <paramref name="paid"/> (taken from their wallet by the caller) for a stay: they get the room. False
    /// (the caller gives the money back) when the quote changed meanwhile.
    /// </summary>
    public bool Stay(string innId, string roomId, string key, string name, int nights, long paid, double now, out string reason)
    {
        InnQuote q = QuoteStay(innId, roomId, key, nights, now);
        reason = q.reason;
        if (!q.ok) return false;
        if (q.total != paid)
        {
            reason = "The price changed: look again.";
            return false;
        }
        RoomRecord room = RoomState(innId, q.roomId, true);
        if (room.stay != null && !string.IsNullOrEmpty(room.stay.key))
        {
            // A stay that ended but was not processed yet: it ends properly first (its guest's things are held for them).
            Tenancy old = room.stay;
            room.stay = null;
            EndTenancy(old, innId, room.id, RoomSource(innId, room.id), Math.Min(now, old.end));
        }
        room.present = -1.0;
        room.stay = new Tenancy { id = NextId(), key = key, name = name ?? "", start = now, end = q.end, rateMilli = q.nightly, termDays = nights };
        ToCity(paid);
        TickInn(InnState(innId), Inn(innId), now);
        Emit(HousingEventKind.AccessChanged, innId, q.roomId, key, name, null, 0, now);
        Changed();
        return true;
    }

    /// <summary>What staying <paramref name="nights"/> more would cost (at tonight's price).</summary>
    public InnQuote QuoteExtension(string innId, string key, int nights, double now)
    {
        var q = new InnQuote { nights = nights };
        InnFacts f = Inn(innId);
        RoomRecord room = RoomOf(innId, key, now);
        if (f == null || room == null) { q.reason = "You have no room here."; return q; }
        if (nights < 1) { q.reason = "Stay at least one more night."; return q; }
        InnRoomFacts facts = f.rooms.Find(x => x.id == room.id);
        if (facts == null) { q.reason = "That room is no more."; return q; }
        InnRecord r = InnState(innId);
        TickInn(r, f, now);
        double end = room.stay.end + nights;
        int ahead = (int)Math.Ceiling(end - now - 1e-9);
        int max = Math.Max(1, Rules.Inns().maxNights);
        if (ahead > max) { q.reason = $"A stay may run at most {max} nights ahead."; return q; }
        q.roomId = room.id;
        q.roomName = facts.name;
        q.nightly = NightPrice(f, r, facts, Taken(r, now));
        q.total = q.nightly * nights;
        if (q.total > int.MaxValue) { q.reason = "That is more than anyone can pay."; return q; }
        q.end = end;
        q.ok = true;
        return q;
    }

    /// <summary>How many more nights the player's stay here may take now (Max Nights ahead at most; 0 when none or no room).</summary>
    public int MaxMoreNights(string innId, string key, double now)
    {
        RoomRecord room = RoomOf(innId, key, now);
        if (room == null) return 0;
        int ahead = (int)Math.Ceiling(room.stay.end - now - 1e-9);
        return Math.Max(0, Math.Max(1, Rules.Inns().maxNights) - ahead);
    }

    /// <summary>The player paid for more nights (the caller took it): the stay runs longer. False when the quote changed.</summary>
    public bool Extend(string innId, string key, int nights, long paid, double now, out string reason)
    {
        InnQuote q = QuoteExtension(innId, key, nights, now);
        reason = q.reason;
        if (!q.ok) return false;
        if (q.total != paid)
        {
            reason = "The price changed: look again.";
            return false;
        }
        RoomRecord room = RoomOf(innId, key, now);
        room.stay.end = q.end;
        room.stay.termDays += nights;
        ToCity(paid);
        Changed();
        return true;
    }

    /// <summary>The player gives up their room now (no refund): what is in its chest is held for them.</summary>
    public bool Leave(string innId, string key, double now, out string reason)
    {
        reason = "";
        RoomRecord room = RoomOf(innId, key, now);
        if (room == null) { reason = "You have no room here."; return false; }
        InnRecord r = InnState(innId);
        TickInn(r, Inn(innId), now);
        Tenancy t = room.stay;
        room.stay = null;
        EndTenancy(t, innId, room.id, RoomSource(innId, room.id), now);
        Changed();
        return true;
    }

    private string RoomSource(string innId, string roomId)
    {
        InnFacts f = Inn(innId);
        InnRoomFacts room = f?.rooms.Find(x => x.id == roomId);
        string inn = f != null ? f.name : "an inn";
        return room != null ? $"{room.name} at {inn}" : $"a room at {inn}";
    }

    /// <summary>A quote of what a night costs in each room of the inn now (free rooms; taken ones show 0).</summary>
    public List<(InnRoomFacts room, bool free, long nightly)> RoomPrices(string innId, double now)
    {
        var list = new List<(InnRoomFacts, bool, long)>();
        InnFacts f = Inn(innId);
        if (f == null) return list;
        InnRecord r = InnState(innId);
        TickInn(r, f, now);
        int taken = Taken(r, now);
        foreach (InnRoomFacts room in f.rooms)
        {
            RoomRecord rec = r.rooms.Find(x => x.id == room.id);
            bool free = rec == null || !rec.Taken(now);
            list.Add((room, free, free ? NightPrice(f, r, room, taken + 1) : 0));
        }
        return list;
    }

    /// <summary>The inn's price level now (1 = its own nightly price) and how full it has been lately (0-1).</summary>
    public (double level, double occupancy) InnLevel(string innId, double now)
    {
        InnFacts f = Inn(innId);
        if (f == null) return (1.0, 0.0);
        InnRecord r = InnState(innId);
        TickInn(r, f, now);
        return (Math.Exp(r.level), r.occupancy);
    }

    // ---------------------------------------------------------------- inns: time
    private void AdvanceInns(double now)
    {
        foreach (InnRecord r in save.inns)
        {
            InnFacts f = Inn(r.id);
            // Stays end at their own times, in order (the occupancy average follows each).
            while (true)
            {
                RoomRecord first = null;
                foreach (RoomRecord room in r.rooms)
                    if (room.stay != null && !string.IsNullOrEmpty(room.stay.key) && room.stay.end <= now && (first == null || room.stay.end < first.stay.end))
                        first = room;
                if (first == null) break;
                Tenancy t = first.stay;
                TickInn(r, f, t.end);
                first.stay = null;
                Tell(t.key, $"Your stay in {RoomSource(r.id, first.id)} has ended.", t.end);
                EndTenancy(t, r.id, first.id, RoomSource(r.id, first.id), t.end);
                Changed();
            }
            TickInn(r, f, now);
        }
    }
}
