using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every animal and every pen of one world, where the rules run (offline, the server): the registry the commands change
/// and the simulation advances. Pure C# - it never touches an inventory, a wallet or a scene: the caller takes the items,
/// then records what was done (see <see cref="AnimalCommands"/>), and what the world must follow comes out as notices.
/// <list type="bullet">
/// <item><b>Identity</b>: an animal's id is made once from the world's serial (never reused, never copied); nothing else
/// adds animals. A request already done is remembered (by player and request token) and answered again, never done twice.</item>
/// <item><b>Time</b>: <see cref="Advance"/> brings the pens (each with its animals, in lockstep) and the boarded animals up to
/// the present, round robin, at most every <see cref="AnimalRules.tickHours"/>, within a budget. Commands bring their pen
/// up to date first, so every decision reads the present.</item>
/// <item><b>Saving</b>: <see cref="Export"/> / <see cref="Import"/> - the whole world in one versioned document.</item>
/// </list>
/// </summary>
public sealed class AnimalBook
{
    private readonly Dictionary<string, AnimalRecord> byId = new Dictionary<string, AnimalRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimalHerd> herds = new Dictionary<string, AnimalHerd>(StringComparer.Ordinal);
    private readonly List<AnimalRecord> boarded = new List<AnimalRecord>();
    private readonly List<AnimalHerd> herdList = new List<AnimalHerd>();
    private readonly List<AnimalDoneRequest> done = new List<AnimalDoneRequest>();
    private readonly Dictionary<string, AnimalDoneRequest> doneByKey = new Dictionary<string, AnimalDoneRequest>(StringComparer.Ordinal);
    private readonly Dictionary<long, AnimalCrated> crated = new Dictionary<long, AnimalCrated>();
    private int herdCursor, boardCursor;

    public AnimalBook(IAnimalCatalog catalog, string world = "")
    {
        Catalog = catalog;
        World = world ?? "";
    }

    public IAnimalCatalog Catalog { get; set; }
    public AnimalRules Rules => Catalog?.Rules ?? AnimalRules.Defaults();
    public string World { get; private set; }
    /// <summary>Counts every animal ever registered in this world (ids come from it).</summary>
    public long Serial { get; private set; }
    /// <summary>Bumped by every change (the director saves when it moves).</summary>
    public int Revision { get; private set; }
    /// <summary>A trade (a release, a move) happened since the last save: the director writes at once.</summary>
    public bool Urgent { get; set; }
    /// <summary>Where every product lot collected came from (saved with the animals): a product copy's code names its serial.</summary>
    public AnimalProductLedger Ledger { get; private set; } = new AnimalProductLedger();

    /// <summary>The last crate serial given (Phase 5: a crate's code names its animal by it; never reused).</summary>
    public long CrateSerial { get; private set; }

    /// <summary>The animals in crates (out of the world: no pen, no step), by serial.</summary>
    public IEnumerable<AnimalCrated> AllCrated => crated.Values;
    public int CratedCount => crated.Count;

    /// <summary>The animal a crate's serial names, or null (never crated, let out already, or gone after its wait).</summary>
    public AnimalCrated Crated(long serial) => serial > 0 && crated.TryGetValue(serial, out AnimalCrated c) ? c : null;

    public IEnumerable<AnimalRecord> All => byId.Values;
    public int Count => byId.Count;
    public IReadOnlyList<AnimalHerd> Herds => herdList;
    public IReadOnlyList<AnimalRecord> Boarded => boarded;

    public void Touch() => Revision++;

    // ---------------------------------------------------------------- queries
    public AnimalRecord Find(string id) => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out AnimalRecord a) ? a : null;

    public AnimalHerd Herd(string penId) => !string.IsNullOrEmpty(penId) && herds.TryGetValue(penId, out AnimalHerd h) ? h : null;

    public AnimalPenRecord Pen(string penId) => Herd(penId)?.pen;

    /// <summary>The animals of a pen (alive and the remains of the dead), in a stable order.</summary>
    public IReadOnlyList<AnimalRecord> InPen(string penId)
    {
        AnimalHerd h = Herd(penId);
        return h != null ? h.animals : (IReadOnlyList<AnimalRecord>)Array.Empty<AnimalRecord>();
    }

    /// <summary>The animals a player owns (alive and dead, in pens or boarded).</summary>
    public List<AnimalRecord> OwnedBy(string key, List<AnimalRecord> into = null)
    {
        into ??= new List<AnimalRecord>();
        into.Clear();
        if (string.IsNullOrEmpty(key)) return into;
        foreach (AnimalRecord a in byId.Values)
            if (a.owner == key) into.Add(a);
        into.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
        return into;
    }

    /// <summary>How many living animals a player owns.</summary>
    public int LivingOwnedBy(string key)
    {
        int n = 0;
        if (string.IsNullOrEmpty(key)) return 0;
        foreach (AnimalRecord a in byId.Values)
            if (a.owner == key && a.Alive) n++;
        return n;
    }

    /// <summary>Square metres of a pen its living animals take (each as an adult of its species). The dead do not count.</summary>
    public float SpaceUsed(string penId)
    {
        float used = 0f;
        AnimalHerd h = Herd(penId);
        if (h == null) return 0f;
        foreach (AnimalRecord a in h.animals)
        {
            if (a == null || !a.Alive) continue;
            AnimalSpeciesData sp = Catalog?.Species(a.species);
            used += SpaceOf(sp, a);
        }
        return used;
    }

    /// <summary>
    /// Square metres an animal takes in a pen: its species' space for an adult, whatever its age (a calf grows into a cow:
    /// room is never promised that growing up would take away).
    /// </summary>
    public static float SpaceOf(AnimalSpeciesData sp, AnimalRecord a)
    {
        if (sp?.habitat == null) return 1f;
        return Mathf.Max(0.05f, sp.habitat.areaPerAnimal);
    }

    public int LivingInPen(string penId)
    {
        int n = 0;
        AnimalHerd h = Herd(penId);
        if (h != null) foreach (AnimalRecord a in h.animals) if (a != null && a.Alive) n++;
        return n;
    }

    // ---------------------------------------------------------------- pens
    /// <summary>
    /// A pen of the scene is known to the world: its record is made the first time (empty feeder, its trough full of well
    /// water), and the scene's facts (size, stations) are kept in it, so the simulation goes on while its scene is not loaded.
    /// </summary>
    public AnimalHerd EnsurePen(AnimalPenSpec spec, string scene, string property, double now)
    {
        if (spec == null || string.IsNullOrEmpty(spec.id)) return null;
        if (!herds.TryGetValue(spec.id, out AnimalHerd h))
        {
            h = new AnimalHerd
            {
                pen = new AnimalPenRecord
                {
                    id = spec.id, scene = scene ?? "", property = property ?? "", water = spec.waterCapacity, waterKind = (int)WaterSourceKind.Well,
                    grass = spec.GrassMax, livedTo = now,
                },
            };
            herds[spec.id] = h;
            herdList.Add(h);
            Revision++;
        }
        h.spec = spec;
        h.pen.scene = scene ?? h.pen.scene;
        h.pen.property = property ?? "";
        h.pen.spec = Copy(spec);
        if (h.pen.water > spec.waterCapacity) h.pen.water = spec.waterCapacity;
        if (h.pen.grass > spec.GrassMax) h.pen.grass = spec.GrassMax;
        // A pen the world has just come to know is laid with fresh bedding when it is a bedded one (never left at -1).
        AnimalHabitat.ResolveBedding(h.pen, spec, Rules);
        return h;
    }

    private static AnimalPenSpec Copy(AnimalPenSpec s) => s == null ? null : JsonUtility.FromJson<AnimalPenSpec>(JsonUtility.ToJson(s));

    // ---------------------------------------------------------------- registering and moving (the commands call these)
    /// <summary>A fresh, never used id ("a" + world serial + a check of the world and species).</summary>
    public string NewId(string species)
    {
        Serial++;
        uint check = AnimalMath.Hash(AnimalMath.Hash(World), (int)(Serial & 0x7fffffff), AnimalMath.Hash(species ?? ""));
        string id = $"a{Serial:x}-{check & 0xffffff:x6}";
        while (byId.ContainsKey(id))
        {
            Serial++;
            id = $"a{Serial:x}-{AnimalMath.Hash((int)Serial, 7) & 0xffffff:x6}";
        }
        return id;
    }

    /// <summary>Adds an animal made by a command (it must carry a fresh id from <see cref="NewId"/>). False when the id is taken.</summary>
    internal bool Add(AnimalRecord a)
    {
        if (a == null || string.IsNullOrEmpty(a.id) || byId.ContainsKey(a.id)) return false;
        byId[a.id] = a;
        Place(a);
        Revision++;
        return true;
    }

    /// <summary>Removes an animal's record for good (only the remains of the dead are removed: the living are never deleted).</summary>
    internal bool Remove(AnimalRecord a)
    {
        if (a == null || a.Alive || !byId.Remove(a.id)) return false;
        Unplace(a);
        Revision++;
        return true;
    }

    /// <summary>Puts an animal in a pen (or the boarding stable when <paramref name="penId"/> is empty).</summary>
    internal void MoveTo(AnimalRecord a, string penId)
    {
        if (a == null) return;
        Unplace(a);
        a.pen = penId ?? "";
        if (a.Alive) a.status = string.IsNullOrEmpty(a.pen) ? AnimalStatus.Boarded : AnimalStatus.Alive;
        Place(a);
        a.revision++;
        Revision++;
    }

    /// <summary>
    /// Puts a living animal in a crate (Phase 5): it leaves its pen and the world's registry (it is not stepped, found or
    /// counted any more) and waits, as it is, in the crated store under a fresh serial. 0: no serial is left (the world
    /// has given <see cref="AnimalItemCode.MaxCrateSerial"/> of them) or it is not a living animal of the world.
    /// </summary>
    internal long Crate(AnimalRecord a, double now, double realNow, int grade, string by, string byName)
    {
        if (a == null || !a.Alive || a.status == AnimalStatus.Crated || !byId.TryGetValue(a.id ?? "", out AnimalRecord known) || known != a) return 0L;
        if (CrateSerial >= AnimalItemCode.MaxCrateSerial) return 0L;
        CrateSerial++;
        Unplace(a);
        byId.Remove(a.id);
        a.status = AnimalStatus.Crated;
        a.pen = "";
        a.revision++;
        crated[CrateSerial] = new AnimalCrated
        {
            serial = CrateSerial, cratedAt = now, cratedAtReal = realNow, by = by ?? "", byName = byName ?? "", grade = Math.Max(0, Math.Min(15, grade)), animal = a,
        };
        Revision++;
        return CrateSerial;
    }

    /// <summary>
    /// Lets a crated animal out into a pen (Phase 5): it leaves the crated store and lives in the world again, from
    /// <paramref name="livedTo"/>. False when the serial names no crated animal or its id is taken.
    /// </summary>
    internal bool Uncrate(long serial, string penId, double livedTo)
    {
        AnimalCrated c = Crated(serial);
        AnimalRecord a = c?.animal;
        if (a == null || string.IsNullOrEmpty(a.id) || byId.ContainsKey(a.id) || string.IsNullOrEmpty(penId)) return false;
        crated.Remove(serial);
        a.status = AnimalStatus.Alive;
        a.pen = penId;
        a.livedTo = livedTo;
        a.revision++;
        byId[a.id] = a;
        Place(a);
        Revision++;
        return true;
    }

    /// <summary>
    /// Undoes a crating that could not be finished (the caller could not give its crate, or the commit failed after it):
    /// the animal leaves the crated store and lives in its pen again, from where it was. Its serial is not given again.
    /// </summary>
    internal bool UndoCrate(AnimalRecord a, string penId)
    {
        if (a == null || string.IsNullOrEmpty(penId) || byId.ContainsKey(a.id ?? "")) return false;
        long serial = 0;
        foreach (AnimalCrated c in crated.Values)
            if (ReferenceEquals(c.animal, a)) { serial = c.serial; break; }
        if (serial <= 0) return false;
        crated.Remove(serial);
        a.status = AnimalStatus.Alive;
        a.pen = penId;
        a.revision++;
        byId[a.id] = a;
        Place(a);
        Revision++;
        return true;
    }

    /// <summary>Undoes a letting out that could not be finished: the animal goes back into its crate, under its own serial, as it was.</summary>
    internal bool UndoUncrate(AnimalCrated c)
    {
        AnimalRecord a = c?.animal;
        if (a == null || c.serial <= 0 || crated.ContainsKey(c.serial) || !byId.TryGetValue(a.id ?? "", out AnimalRecord known) || known != a) return false;
        Unplace(a);
        byId.Remove(a.id);
        a.status = AnimalStatus.Crated;
        a.pen = "";
        a.revision++;
        crated[c.serial] = c;
        Revision++;
        return true;
    }

    /// <summary>
    /// Drops the crated animals that waited longer than the rules' Crate Keep Days (real days; 0: they wait for ever): the
    /// carrier sold them on. One crated with no real hour (an old save) starts its wait now. Returns how many went
    /// (<paramref name="gone"/>, when given, gets each).
    /// </summary>
    public int PruneCrates(double realHoursNow, List<AnimalCrated> goneInto = null)
    {
        if (crated.Count == 0 || realHoursNow <= 0.0) return 0;
        float days = AnimalTrade.RulesOf(Rules).crateKeepDays;
        var gone = new List<long>();
        foreach (AnimalCrated c in crated.Values)
        {
            if (c.cratedAtReal <= 0.0)
            {
                c.cratedAtReal = realHoursNow;
                continue;
            }
            if (days > 0f && realHoursNow - c.cratedAtReal > days * 24.0) gone.Add(c.serial);
        }
        if (gone.Count == 0) return 0;
        gone.Sort();
        foreach (long g in gone)
        {
            goneInto?.Add(crated[g]);
            crated.Remove(g);
        }
        Revision++;
        return gone.Count;
    }

    private void Place(AnimalRecord a)
    {
        if (!string.IsNullOrEmpty(a.pen) && herds.TryGetValue(a.pen, out AnimalHerd h))
        {
            if (!h.animals.Contains(a))
            {
                h.animals.Add(a);
                h.animals.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
            }
            return;
        }
        if (!string.IsNullOrEmpty(a.pen))
        {
            // A pen the world has no record of (an old save, a pen removed from the scene): it is made from what is known,
            // so the animal keeps living there until the scene says more.
            AnimalHerd made = EnsurePen(new AnimalPenSpec { id = a.pen }, "", "", a.livedTo);
            made.animals.Add(a);
            return;
        }
        if (!boarded.Contains(a)) boarded.Add(a);
    }

    private void Unplace(AnimalRecord a)
    {
        if (!string.IsNullOrEmpty(a.pen) && herds.TryGetValue(a.pen, out AnimalHerd h)) h.animals.Remove(a);
        boarded.Remove(a);
    }

    // ---------------------------------------------------------------- time
    /// <summary>
    /// Brings what is due up to <paramref name="now"/>: pens round robin, each when it lags <see cref="AnimalRules.tickHours"/>
    /// or more (with its animals), then the boarded animals - until <paramref name="outOfTime"/> says the frame's budget is
    /// spent or <paramref name="maxSteps"/> animal-steps are done. Work left goes on next time from where it stopped.
    /// </summary>
    public void Advance(double now, AnimalSimContext ctx, Func<bool> outOfTime, int maxSteps)
    {
        if (ctx == null) return;
        ctx.book = this; // young are born into this world
        double tick = Math.Max(0.05, Rules.tickHours);
        int start = ctx.steps;
        int n = herdList.Count;
        for (int i = 0; i < n; i++)
        {
            if (herdCursor >= herdList.Count) herdCursor = 0;
            AnimalHerd h = herdList[herdCursor];
            if (h != null && now - h.pen.livedTo >= tick)
            {
                // An empty pen costs only its water (rain and sun): its steps are not counted against the budget.
                bool finished = AnimalSim.AdvanceHerd(h, now, ctx, () => (outOfTime != null && outOfTime()) || ctx.steps - start >= maxSteps);
                Revision++;
                if (!finished) return; // the same pen goes on next time
            }
            herdCursor++;
            if ((outOfTime != null && outOfTime()) || ctx.steps - start >= maxSteps) return;
        }
        int b = boarded.Count;
        for (int i = 0; i < b; i++)
        {
            if (boardCursor >= boarded.Count) boardCursor = 0;
            if (boarded.Count == 0) break;
            AnimalRecord a = boarded[boardCursor];
            if (a != null && now - a.livedTo >= tick)
            {
                bool finished = AnimalSim.AdvanceAlone(a, now, ctx, () => (outOfTime != null && outOfTime()) || ctx.steps - start >= maxSteps);
                Revision++;
                if (!finished) return;
            }
            boardCursor++;
            if ((outOfTime != null && outOfTime()) || ctx.steps - start >= maxSteps) return;
        }
    }

    /// <summary>Brings one pen and its animals right up to <paramref name="now"/> (before a command reads or changes them).</summary>
    public void BringUpToDate(AnimalHerd h, double now, AnimalSimContext ctx)
    {
        if (h == null) return;
        if (ctx != null) ctx.book = this;
        AnimalSim.AdvanceHerd(h, now, ctx);
        Revision++;
    }

    public void BringUpToDate(AnimalRecord a, double now, AnimalSimContext ctx)
    {
        if (a == null) return;
        if (ctx != null) ctx.book = this;
        AnimalHerd h = Herd(a.pen);
        if (h != null) BringUpToDate(h, now, ctx);
        else AnimalSim.AdvanceAlone(a, now, ctx);
        Revision++;
    }

    // ---------------------------------------------------------------- requests done
    private static string DoneKey(string player, string token) => player + "|" + token;

    /// <summary>Was this request (by its player and token) done already? Its answer is given back.</summary>
    public bool WasDone(string player, string token, out AnimalDoneRequest answer)
    {
        answer = null;
        if (string.IsNullOrEmpty(token)) return false;
        return doneByKey.TryGetValue(DoneKey(player ?? "", token), out answer);
    }

    /// <summary>Remembers a request that changed the world (its retries are answered from this).</summary>
    public void RememberDone(string player, string token, int seq, double realHoursNow, bool ok, string message, string animal)
    {
        if (string.IsNullOrEmpty(token)) return;
        var d = new AnimalDoneRequest { player = player ?? "", token = token, seq = seq, at = realHoursNow, ok = ok, message = message ?? "", animal = animal ?? "" };
        doneByKey[DoneKey(d.player, token)] = d;
        done.Add(d);
        Revision++;
    }

    /// <summary>Forgets requests older than the rules' memory (real hours).</summary>
    public void ForgetOldRequests(double realHoursNow)
    {
        double keep = Math.Max(0.1, Rules.rememberRequestsHours);
        int cut = 0;
        while (cut < done.Count && realHoursNow - done[cut].at > keep) cut++;
        if (done.Count - cut > 20000) cut = done.Count - 20000;
        if (cut <= 0) return;
        for (int i = 0; i < cut; i++) doneByKey.Remove(DoneKey(done[i].player, done[i].token));
        done.RemoveRange(0, cut);
    }

    // ---------------------------------------------------------------- saving
    public AnimalWorldSave Export(double now, long utc)
    {
        var s = new AnimalWorldSave { world = World, savedUtc = utc, now = now, serial = Serial };
        foreach (AnimalHerd h in herdList) s.pens.Add(h.pen);
        foreach (AnimalRecord a in byId.Values) s.animals.Add(a);
        s.animals.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
        s.done.AddRange(done);
        Ledger.Prune(now, Rules.production);
        s.ledger = Ledger;
        foreach (AnimalCrated c in crated.Values) s.crated.Add(c);
        s.crated.Sort((x, y) => x.serial.CompareTo(y.serial));
        s.crateSerial = CrateSerial;
        return s;
    }

    /// <summary>
    /// Takes a world's save (replacing what the book held). A save of a newer version than this game knows is refused
    /// (thrown): it is never read half and written back over. Records an old save lacks are filled in: a version 1 (Phase 1)
    /// save loads with every Phase 2 field at its default (hooves 100, welfare 60, an unprimed life record that starts at
    /// its first step, a bedded pen's bedding laid fresh), and the Phase 2 means of the day it was saved in start from its
    /// state (see <see cref="SeedToday"/>); a version 2 save loads with no production yet (each animal starts its first
    /// cycle at its next step) and an empty product ledger. A current record is left exactly as it is.
    /// </summary>
    public void Import(AnimalWorldSave s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        if (s.version > AnimalWorldSave.CurrentVersion) throw new InvalidOperationException($"the animals were saved by a newer version ({s.version}).");
        byId.Clear();
        herds.Clear();
        herdList.Clear();
        boarded.Clear();
        done.Clear();
        doneByKey.Clear();
        crated.Clear();
        World = string.IsNullOrEmpty(s.world) ? World : s.world;
        Serial = Math.Max(0, s.serial);
        // The product ledger (none before version 3: no lot was collected yet).
        Ledger = s.ledger ?? new AnimalProductLedger();
        Ledger.entries ??= new List<AnimalProductRecord>();
        Ledger.entries.RemoveAll(e => e == null);
        Ledger.Reindex();
        if (s.pens != null)
            foreach (AnimalPenRecord p in s.pens)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || herds.ContainsKey(p.id)) continue;
                p.feeder ??= new List<FeederLot>();
                p.spec ??= new AnimalPenSpec();
                p.spec.id = p.id;
                p.cleanedBy ??= "";
                // Phase 5 (none before version 5: as built, no machines, nothing owed).
                if (p.level < 1) p.level = 1;
                p.hopperFeed ??= new List<FeederLot>();
                p.hopperFeed.RemoveAll(l => l == null || l.portions <= 0f);
                p.tray ??= new List<AnimalTrayLot>();
                p.tray.RemoveAll(t => t?.lot == null || t.lot.units <= 0 || string.IsNullOrEmpty(t.product));
                p.upgradedBy ??= "";
                p.hopperBy ??= "";
                p.watererBy ??= "";
                p.traysBy ??= "";
                if (s.version < 5 || float.IsNaN(p.upkeepOwed) || float.IsInfinity(p.upkeepOwed)) p.upkeepOwed = 0f;
                AnimalHabitat.ResolveBedding(p, p.spec, Rules);
                var h = new AnimalHerd { pen = p, spec = p.spec };
                herds[p.id] = h;
                herdList.Add(h);
            }
        if (s.animals != null)
            foreach (AnimalRecord a in s.animals)
            {
                if (a == null || string.IsNullOrEmpty(a.id) || byId.ContainsKey(a.id)) continue;
                Repair(a);
                if (s.version < 2) SeedToday(a);
                byId[a.id] = a;
                Place(a);
            }
        // The crated animals (none before version 5): kept as they were crated, out of the world; one whose id the world
        // knows already (a hand-made file) is left out, never let out twice.
        CrateSerial = Math.Max(0L, s.crateSerial);
        var cratedIds = new HashSet<string>(StringComparer.Ordinal);
        if (s.crated != null)
            foreach (AnimalCrated c in s.crated)
            {
                if (c?.animal == null || c.serial <= 0 || string.IsNullOrEmpty(c.animal.id) || crated.ContainsKey(c.serial) || byId.ContainsKey(c.animal.id)
                    || !cratedIds.Add(c.animal.id)) continue;
                Repair(c.animal);
                c.animal.status = AnimalStatus.Crated;
                c.animal.pen = "";
                c.by ??= "";
                c.byName ??= "";
                crated[c.serial] = c;
                if (c.serial > CrateSerial) CrateSerial = c.serial;
            }
        if (s.done != null)
            foreach (AnimalDoneRequest d in s.done)
            {
                if (d == null || string.IsNullOrEmpty(d.token)) continue;
                done.Add(d);
                doneByKey[DoneKey(d.player, d.token)] = d;
            }
        Revision++;
    }

    /// <summary>
    /// A version 1 (Phase 1) record's day still being lived kept no hygiene, comfort or welfare: they start from its state
    /// now, so the rest of the day is not averaged with zeros. Its earlier days are left as they are (complete, they say
    /// nothing of its coat).
    /// </summary>
    private void SeedToday(AnimalRecord a)
    {
        AnimalDay d = a.days.Count > 0 ? a.days[a.days.Count - 1] : null;
        if (d == null || d.hours <= 0f || d.day != (int)Math.Floor(a.livedTo / 24.0)) return;
        d.hygiene = a.hygiene;
        d.comfort = a.comfort;
        d.welfare = AnimalWelfare.Compute(a, Rules.welfare);
        d.minWelfare = Mathf.Min(d.minWelfare, d.welfare);
    }

    /// <summary>Fills in what a record lacks (an old save, a hand-made file); a current record is left exactly as it is.</summary>
    private static void Repair(AnimalRecord a)
    {
        a.genes ??= new AnimalGenes();
        a.feedings ??= new List<AnimalFeeding>();
        a.days ??= new List<AnimalDay>();
        a.events ??= new List<AnimalEvent>();
        a.conditions ??= new List<AnimalCondition>();
        a.exposures ??= new List<AnimalExposure>();
        a.care ??= new List<AnimalCareMark>();
        a.careLog ??= new List<AnimalCareEntry>();
        a.healthLog ??= new List<AnimalHealthEntry>();
        a.doses ??= new List<AnimalDose>();
        a.life ??= new AnimalLife();
        a.products ??= new List<AnimalProductState>();
        a.products.RemoveAll(x => x == null || string.IsNullOrEmpty(x.product));
        foreach (AnimalProductState st in a.products) st.waiting ??= new List<AnimalProductLot>();
        a.productLog ??= new List<AnimalProductEntry>();
        a.mother ??= "";
        a.father ??= "";
        a.motherName ??= "";
        a.fatherName ??= "";
        a.grandparents ??= new List<string>();
        a.pregnancy ??= new AnimalPregnancy();
        a.pregnancy.fatherGenes ??= new AnimalGenes();
        a.pregnancy.young ??= new List<string>();
        a.bonds ??= new List<AnimalBond>();
        a.bonds.RemoveAll(x => x == null || string.IsNullOrEmpty(x.other));
        a.deathCondition ??= "";
        a.pen ??= "";
        a.owner ??= "";
        if (a.status == AnimalStatus.Alive && string.IsNullOrEmpty(a.pen)) a.status = AnimalStatus.Boarded;
    }
}
