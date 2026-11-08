using System;
using System.Collections.Generic;

/// <summary>Everything the housing books keep between sessions (one JSON file where the rules run).</summary>
[Serializable]
public class HousingSave
{
    public int version = 1;
    /// <summary>The clock the times are on (<see cref="HousingClockKind"/>).</summary>
    public int clock;
    /// <summary>The lease day the books last moved to.</summary>
    public double lastAdvance;
    public long savedUtc;
    public long nextId = 1;
    /// <summary>Money the city kept (rents, fees): a sink, for the record.</summary>
    public long citySink;
    public List<PropertyRecord> properties = new List<PropertyRecord>();
    public List<InnRecord> inns = new List<InnRecord>();
    public List<HousingAccount> accounts = new List<HousingAccount>();
    public List<HeldBundle> held = new List<HeldBundle>();
    public List<ChestContents> chests = new List<ChestContents>();
    public MarketSave market = new MarketSave();

    /// <summary>Fills what an older or broken save lacks.</summary>
    public void Repair()
    {
        properties ??= new List<PropertyRecord>();
        inns ??= new List<InnRecord>();
        accounts ??= new List<HousingAccount>();
        held ??= new List<HeldBundle>();
        chests ??= new List<ChestContents>();
        market ??= new MarketSave();
        market.Repair();
        properties.RemoveAll(p => p == null || string.IsNullOrEmpty(p.id));
        foreach (PropertyRecord p in properties) p.Repair();
        inns.RemoveAll(i => i == null || string.IsNullOrEmpty(i.id));
        foreach (InnRecord i in inns)
        {
            i.rooms ??= new List<RoomRecord>();
            i.rooms.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
            foreach (RoomRecord r in i.rooms)
                if (r.stay != null) r.stay.grants ??= new List<HousingGrant>();
        }
        accounts.RemoveAll(a => a == null || string.IsNullOrEmpty(a.key));
        held.RemoveAll(h => h == null || string.IsNullOrEmpty(h.key));
        foreach (HeldBundle h in held) h.stacks ??= new List<HeldStack>();
        chests.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
        foreach (ChestContents c in chests)
        {
            c.slots ??= new List<ChestSlot>();
            c.pending ??= new List<HeldStack>();
        }
        if (nextId < 1) nextId = 1;
    }

    /// <summary>Moves every time by <paramref name="delta"/> lease days (the clock changed between sessions: what was left of each stay is kept).</summary>
    public void Shift(double delta)
    {
        if (Math.Abs(delta) < 1e-12) return;
        lastAdvance += delta;
        foreach (PropertyRecord p in properties) p.Shift(delta);
        foreach (InnRecord i in inns)
        {
            if (i.lastTick >= 0.0) i.lastTick += delta;
            foreach (RoomRecord r in i.rooms) ShiftTenancy(r.stay, delta);
        }
        foreach (HeldBundle h in held) h.createdAt += delta;
        market.Shift(delta);
    }

    internal static void ShiftTenancy(Tenancy t, double delta)
    {
        if (t == null) return;
        t.start += delta;
        t.end += delta;
    }
}
