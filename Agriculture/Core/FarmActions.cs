using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Item ids of agricultural items (resolved by the runtime: an asset bound in the database, else an item made from the id).</summary>
public static class AgriIds
{
    public const string Prefix = "agri:";
    public static string Seed(string species) => Prefix + "seed/" + species;
    public static string Produce(string species) => Prefix + "produce/" + species;
    public static string Amendment(string id) => Prefix + "amend/" + id;
    public static string Tool(string id) => Prefix + "tool/" + id;
    public static string Instrument(string id) => Prefix + "instr/" + id;
    public static string Container(string id) => Prefix + "water/" + id;

    /// <summary>"agri:seed/tomato" → kind "seed", key "tomato".</summary>
    public static bool Parse(string id, out string kind, out string key)
    {
        kind = key = null;
        if (string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        int slash = id.IndexOf('/', Prefix.Length);
        if (slash < 0) return false;
        kind = id.Substring(Prefix.Length, slash - Prefix.Length);
        key = id.Substring(slash + 1);
        return key.Length > 0;
    }
}

/// <summary>What a player asks to do to a field. Sent as is over the network (JSON).</summary>
[Serializable]
public struct FarmRequest
{
    public FarmActionKind kind;
    /// <summary>Field-local spot (X, Z), metres.</summary>
    public Vector2 spot;
    /// <summary>The plant worked on (0 = none, the one at the spot).</summary>
    public int plant;
    /// <summary>The piece of produce (0 = none / every piece).</summary>
    public int piece;
    /// <summary>Seconds held (digging, pouring), portions (materials).</summary>
    public float amount;
    /// <summary>Prune kind, pest kind, spreading method...</summary>
    public int option;
    /// <summary>The item used, by its key (species id for seeds, material id, instrument id...).</summary>
    public string item;
    /// <summary>Text (a label).</summary>
    public string text;
    /// <summary>The asker's number for this request (its answer carries it back).</summary>
    public int seq;
}

/// <summary>A tool a worker has.</summary>
[Serializable]
public struct FarmToolProfile
{
    public string id;
    public FarmToolKind kind;
    public float quality;
    public float speed;
    /// <summary>0-1: what is left of its wear.</summary>
    public float edge;
}

/// <summary>An instrument a worker has.</summary>
[Serializable]
public struct InstrumentProfile
{
    public string id;
    public InstrumentKind kind;
    public float quality;
}

/// <summary>The water container a worker pours from.</summary>
[Serializable]
public struct WaterProfile
{
    public string id;
    public float litres;
    public WaterSourceKind kind;
    public float spread;
    public float flow;
    public bool wetsLeaves;
}

/// <summary>Pollen a brush carries (from one plant, for a day).</summary>
[Serializable]
public struct PollenState
{
    public bool has;
    public string species;
    public int genes;
    public int plant;
    public double time;
}

/// <summary>Who works and with what: their skills, tools, instruments, water, the seed copy they sow and the pollen on their brush.</summary>
public sealed class FarmWorker
{
    public int key;
    public string name = "";
    public AgriSkillData skills = new AgriSkillData();
    public readonly List<FarmToolProfile> tools = new List<FarmToolProfile>();
    public readonly List<InstrumentProfile> instruments = new List<InstrumentProfile>();
    public WaterProfile water;
    /// <summary>The code of the seed copy that will be sown (the top copy of the first stack of the seeds named in the request).</summary>
    public int seedCode;
    /// <summary>How many copies of each item (by key) the worker has: seeds, materials, stakes.</summary>
    public readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
    public PollenState pollen;

    public bool Has(FarmToolKind k) => Tool(k).HasValue;

    public FarmToolProfile? Tool(FarmToolKind k)
    {
        FarmToolProfile? best = null;
        foreach (FarmToolProfile t in tools)
            if (t.kind == k && (best == null || t.quality * (0.5f + 0.5f * t.edge) > best.Value.quality * (0.5f + 0.5f * best.Value.edge)))
                best = t;
        return best;
    }

    public InstrumentProfile? Instrument(InstrumentKind k, string id = null)
    {
        InstrumentProfile? best = null;
        foreach (InstrumentProfile i in instruments)
            if (i.kind == k && (string.IsNullOrEmpty(id) || i.id == id) && (best == null || i.quality > best.Value.quality))
                best = i;
        return best;
    }

    public int Count(string key) => key != null && counts.TryGetValue(key, out int n) ? n : 0;
}

/// <summary>Items a result gives or takes (by item id).</summary>
[Serializable]
public class FarmItemChange
{
    public string item = "";
    public int count;
    public List<int> codes = new List<int>();
}

[Serializable]
public struct FarmXp
{
    public AgriSkill skill;
    public float amount;
}

/// <summary>What came of a request: what the player is told (and sees), the items given and used, wear, water poured, experience.</summary>
[Serializable]
public class FarmResult
{
    public bool ok;
    /// <summary>The number of the request it answers (<see cref="FarmRequest.seq"/>).</summary>
    public int seq;
    /// <summary>The line shown to the player.</summary>
    public string message = "";
    /// <summary>What is seen or read (observations, instrument readings).</summary>
    public List<string> lines = new List<string>();
    /// <summary>A line the player may choose to write in their notebook (a reading).</summary>
    public string note = "";
    public List<FarmItemChange> give = new List<FarmItemChange>();
    public List<FarmItemChange> take = new List<FarmItemChange>();
    public float waterUsed;
    public string toolWorn = "";
    public int wear;
    public List<FarmXp> xp = new List<FarmXp>();
    public PollenState pollen;
    public bool pollenChanged;
    /// <summary>The plant created (sowing).</summary>
    public int plant;

    public static FarmResult Fail(string message) => new FarmResult { ok = false, message = message };

    public FarmResult Xp(AgriSkill s, float amount)
    {
        if (amount > 0f) xp.Add(new FarmXp { skill = s, amount = amount });
        return this;
    }

    public void Give(string item, int count, List<int> codes = null)
    {
        if (count <= 0) return;
        foreach (FarmItemChange c in give)
            if (c.item == item)
            {
                c.count += count;
                if (codes != null) c.codes.AddRange(codes);
                return;
            }
        give.Add(new FarmItemChange { item = item, count = count, codes = codes ?? new List<int>() });
    }

    public void Take(string item, int count)
    {
        if (count > 0) take.Add(new FarmItemChange { item = item, count = count });
    }

    public string ToJson() => JsonUtility.ToJson(this);
    public static FarmResult FromJson(string json)
    {
        try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<FarmResult>(json); }
        catch (Exception) { return null; }
    }
}

/// <summary>
/// Every action on a field, as rules: what it needs (tools, items, the state of the soil and the plant), what it does,
/// what the player is told, what it gives, uses and teaches. Where the rules run (offline, the server) the runtime
/// calls <see cref="Perform"/> and applies the result's items to the player's inventory.
/// <para>Nothing here measures anything for the player: digging makes a hole as deep as the hands made it (the player
/// sees the hole, not a number - a depth gauge reads it), pouring pours what the player held, sowing puts the seed where
/// it was put. Skill steadies the hands.</para>
/// </summary>
public static class FarmActions
{
    public const float SpotRadius = 0.06f;

    public static FarmResult Perform(FieldState f, FarmRequest r, FarmWorker w, double now, IAgriClimateSource src)
    {
        if (f == null || w == null) return FarmResult.Fail("Nothing to work on.");
        SoilGrid g = f.Grid;
        AgriSettings s = f.Settings;
        if (!g.TryIndex(r.spot, out int cell)) return FarmResult.Fail("That is not part of the field.");
        PlantState plant = r.plant > 0 ? f.Find(r.plant) : f.Nearest(r.spot, Mathf.Max(SpotRadius, g.CellSize * 0.75f));
        if (plant != null && plant.Alive) PlantSim.Advance(f, plant, now, null, src);
        g.Advance(cell, now, s);
        var rng = new AgriRandom(f.Seed ^ w.key, (int)(now * 60.0), (int)r.kind * 31 + cell);
        var date = new AgriDate(now, s);
        switch (r.kind)
        {
            case FarmActionKind.Inspect: return Inspect(f, cell, plant, w, now, src);
            case FarmActionKind.Till: return Till(f, r, cell, w, s, now);
            case FarmActionKind.ClearStones: return ClearStones(f, r, w, s, now);
            case FarmActionKind.Dig: return Dig(f, r, cell, plant, w, s, ref rng);
            case FarmActionKind.Sow: return Sow(f, r, cell, w, s, now, date, src, ref rng);
            case FarmActionKind.Cover: return Cover(f, r, cell, w);
            case FarmActionKind.Water: return Water(f, r, cell, w, s, now);
            case FarmActionKind.Amend: return Amend(f, r, w, s, now);
            case FarmActionKind.Treat: return Treat(f, r, plant, w, s, now);
            case FarmActionKind.Measure: return Measure(f, r, cell, plant, w, s, now, date);
            case FarmActionKind.Prune: return Prune(f, r, plant, w, s, date, ref rng);
            case FarmActionKind.CollectPollen: return CollectPollen(plant, w, now);
            case FarmActionKind.Pollinate: return Pollinate(plant, w, s, now, ref rng);
            case FarmActionKind.Harvest: return Harvest(f, r, plant, w, s, date, ref rng);
            case FarmActionKind.HarvestPlant: return HarvestPlant(f, r, plant, w, s, date, ref rng);
            case FarmActionKind.CollectSeeds: return CollectSeeds(f, r, plant, w, s, ref rng);
            case FarmActionKind.RemovePests: return RemovePests(f, r, plant, w, s);
            case FarmActionKind.Uproot: return Uproot(f, plant);
            case FarmActionKind.Support: return Support(f, plant, w);
            case FarmActionKind.Label: return Label(f, plant, r.text);
            default: return FarmResult.Fail("You cannot do that here.");
        }
    }

    private static float Skill(FarmWorker w, AgriSettings s, AgriSkill k) => AgriSkills.Factor(s, w.skills, k);

    // ---------------------------------------------------------------- looking
    private static FarmResult Inspect(FieldState f, int cell, PlantState plant, FarmWorker w, double now, IAgriClimateSource src)
    {
        var res = new FarmResult { ok = true };
        AgriSettings s = f.Settings;
        float botany = Skill(w, s, AgriSkill.Botany);
        bool lens = w.Instrument(InstrumentKind.Magnifier).HasValue;
        res.lines.AddRange(PlantObservation.Soil(f.Grid, cell, f.Catalog));
        if (plant != null && (plant.Emerged || plant.stage == PlantStage.Dead))
            res.lines.AddRange(PlantObservation.Plant(plant, now, s, botany, lens, w.Instrument(InstrumentKind.Magnifier)?.quality ?? 0f));
        res.message = plant != null && plant.Emerged ? $"You look closely at the {PlantObservation.NameOf(plant)}." : "You look at the soil.";
        return res.Xp(AgriSkill.Botany, plant != null && plant.Emerged ? 0.2f : 0.05f);
    }

    // ---------------------------------------------------------------- the soil
    private static FarmResult Till(FieldState f, FarmRequest r, int cell, FarmWorker w, AgriSettings s, double now)
    {
        FarmToolProfile? hoe = w.Tool(FarmToolKind.Hoe);
        FarmToolProfile? tool = hoe ?? w.Tool(FarmToolKind.Shovel);
        if (!tool.HasValue) return FarmResult.Fail("You need a hoe (or a shovel) to work the soil.");
        SoilGrid g = f.Grid;
        float radius = hoe.HasValue ? 0.3f : 0.22f;
        var cells = new List<int>();
        var weights = new List<float>();
        g.CellsInRadius(r.spot, radius, cells, weights);
        float work = (hoe.HasValue ? 0.35f : 0.25f) * Mathf.Sqrt(Mathf.Max(0.1f, tool.Value.quality)) * (0.6f + 0.4f * tool.Value.edge) *
                     (0.85f + 0.3f * Skill(w, s, AgriSkill.Agriculture));
        float stones = 0f, clay = 0f, wet = 0f;
        for (int k = 0; k < cells.Count; k++)
        {
            int c = cells[k];
            g.Advance(c, now, s);
            float ease = 0.4f + 0.6f * SoilTypes.Workability(g.TypeAt(c));
            g.Till(c, work * Mathf.Sqrt(weights[k]) * ease * (1f - 0.5f * g.Get(c, SoilChannel.Stones)));
            stones += g.Get(c, SoilChannel.Stones);
            clay += g.TypeAt(c) == SoilType.Clay ? 1f : 0f;
            wet += g.Get(c, SoilChannel.Moisture);
        }
        int n = Mathf.Max(1, cells.Count);
        stones /= n; clay /= n; wet /= n;
        // A hoe drawn over a plant hurts it; over a seed, it is gone.
        var near = new List<PlantState>();
        f.Near(r.spot, radius * 0.8f, near);
        int hurt = 0;
        foreach (PlantState p in near)
        {
            if (!p.Alive) continue;
            if (p.IsSeed) { PlantSim.Die(p, true); hurt++; }
            else if (p.size < 0.5f) { p.health -= 0.35f; p.rootRadius = -1f; hurt++; }
        }
        if (hurt > 0) f.PlantVersion++;
        var res = new FarmResult { ok = true, toolWorn = tool.Value.id, wear = stones > 0.35f ? 2 : 1 };
        res.message = stones > 0.35f ? "The blade rings on stones." : clay > 0.5f && wet > 0.75f ? "The clay clings to the blade in heavy clods." :
            clay > 0.5f && wet < 0.3f ? "The dry clay breaks into hard lumps." : g.Get(cell, SoilChannel.Tilth) > 0.75f ? "The soil crumbles, loose and fine." : "You work the soil.";
        if (hurt > 0) res.lines.Add("Something small was in the way of the blade.");
        return res.Xp(AgriSkill.Agriculture, 1f);
    }

    private static FarmResult ClearStones(FieldState f, FarmRequest r, FarmWorker w, AgriSettings s, double now)
    {
        SoilGrid g = f.Grid;
        bool shovel = w.Has(FarmToolKind.Shovel);
        var cells = new List<int>();
        var weights = new List<float>();
        g.CellsInRadius(r.spot, 0.3f, cells, weights);
        float before = 0f, after = 0f;
        for (int k = 0; k < cells.Count; k++)
        {
            int c = cells[k];
            g.Advance(c, now, s);
            float st = g.Get(c, SoilChannel.Stones);
            before += st;
            float take = st * (shovel ? 0.35f : 0.22f) * weights[k];
            g.Set(c, SoilChannel.Stones, st - take);
            g.Add(c, SoilChannel.Compaction, -0.02f * weights[k]);
            after += st - take;
        }
        g.Version++;
        var res = new FarmResult { ok = true, toolWorn = shovel ? w.Tool(FarmToolKind.Shovel).Value.id : "", wear = shovel ? 1 : 0 };
        res.message = before / Mathf.Max(1, cells.Count) < 0.04f ? "You find hardly a pebble." : shovel ? "You lever the stones out and heap them aside." : "You pick out the stones you can reach.";
        return res.Xp(AgriSkill.Agriculture, 0.5f);
    }

    private static FarmResult Dig(FieldState f, FarmRequest r, int cell, PlantState plant, FarmWorker w, AgriSettings s, ref AgriRandom rng)
    {
        FarmToolProfile? trowel = w.Tool(FarmToolKind.Trowel), dibber = w.Tool(FarmToolKind.Dibber), shovel = w.Tool(FarmToolKind.Shovel);
        FarmToolProfile? tool = trowel ?? dibber ?? shovel;
        if (!tool.HasValue) return FarmResult.Fail("You need a trowel, a dibber or a shovel to dig.");
        if (plant != null && plant.Alive && (plant.position - r.spot).magnitude < SpotRadius)
            return FarmResult.Fail("Something is already planted right here.");
        SoilGrid g = f.Grid;
        float seconds = Mathf.Clamp(r.amount, 0f, s.maxHoldSeconds);
        FarmToolKind kind = tool.Value.kind;
        float rate = kind == FarmToolKind.Dibber ? 6f : kind == FarmToolKind.Shovel ? 9f : 4f; // cm per second in easy soil
        float hard = (0.35f + 0.65f * SoilTypes.Workability(g.TypeAt(cell))) * (1f - 0.6f * g.Get(cell, SoilChannel.Compaction)) *
                     (1f - 0.5f * g.Get(cell, SoilChannel.Stones)) * (0.6f + 0.4f * g.Get(cell, SoilChannel.Tilth));
        if (g.TypeAt(cell) == SoilType.Clay && g.Get(cell, SoilChannel.Moisture) > 0.8f) hard *= 0.7f;
        float depth = rate * seconds * hard * tool.Value.speed * (0.7f + 0.3f * tool.Value.edge);
        // The hands are not a ruler: skill makes them steadier.
        float unsteady = (0.25f + 1.1f * (1f - Skill(w, s, AgriSkill.Agriculture))) * (kind == FarmToolKind.Shovel ? 1.6f : 1f);
        depth = Mathf.Max(0f, depth + rng.Gauss() * unsteady * Mathf.Min(1f, depth / 3f + 0.3f));
        float max = kind == FarmToolKind.Dibber ? 15f : kind == FarmToolKind.Shovel ? 60f : 30f;
        int total = Mathf.Clamp(Mathf.RoundToInt(g.Holes[cell] + depth), 0, (int)max);
        g.Holes[cell] = (byte)total;
        g.Set(cell, SoilChannel.Tilth, Mathf.Max(g.Get(cell, SoilChannel.Tilth), 0.5f));
        g.Version++;
        var res = new FarmResult { ok = true, toolWorn = tool.Value.id, wear = 1 };
        res.message = depth < 0.4f ? "The ground barely gives." : hard < 0.35f ? "It is hard going." : "You dig.";
        return res.Xp(AgriSkill.Agriculture, 0.3f);
    }

    private static FarmResult Sow(FieldState f, FarmRequest r, int cell, FarmWorker w, AgriSettings s, double now, AgriDate date, IAgriClimateSource src, ref AgriRandom rng)
    {
        PlantSpeciesData d = f.Catalog?.Species(r.item);
        if (d == null) return FarmResult.Fail("You have no such seeds.");
        string seedId = AgriIds.Seed(d.id);
        if (w.Count(seedId) <= 0) return FarmResult.Fail($"You have no {d.name} seeds.");
        PlantState near = f.Nearest(r.spot, 0.03f);
        if (near != null) return FarmResult.Fail("There is already a seed or a plant right here.");
        SoilGrid g = f.Grid;
        float depth = g.Holes[cell];
        depth = Mathf.Max(0f, depth + rng.Gauss() * 0.3f);
        AgriWeather weather = src != null ? src.WeatherAt(now) : f.Weather;
        var p = new PlantState
        {
            species = d.id,
            position = r.spot,
            depthCm = depth,
            covered = false,
            planted = now,
            last = now,
            genes = PlantGenes.FromCode(w.seedCode),
            viability = 1f,
            plantedAtNight = !weather.day && (date.Hour < date.Sunrise || date.Hour > date.Sunset),
            plantedMoon = date.Moon,
            lastSeason = date.Season,
            owner = w.key,
        };
        f.Add(p);
        var res = new FarmResult { ok = true, plant = p.id };
        res.Take(seedId, 1);
        res.message = depth < 0.3f ? $"You drop a {d.name} seed on the surface." : $"You drop a {d.name} seed into the hole.";
        w.skills.sown++;
        return res.Xp(AgriSkill.Agriculture, 2f);
    }

    private static FarmResult Cover(FieldState f, FarmRequest r, int cell, FarmWorker w)
    {
        SoilGrid g = f.Grid;
        PlantState seed = f.Nearest(r.spot, Mathf.Max(0.08f, g.CellSize * 0.6f));
        bool hadHole = g.Holes[cell] > 0;
        g.Holes[cell] = 0;
        g.Version++;
        if (seed != null && seed.IsSeed && !seed.covered)
        {
            seed.covered = true;
            f.PlantVersion++;
            return new FarmResult { ok = true, message = "You push the soil back over the seed and firm it." }.Xp(AgriSkill.Agriculture, 0.2f);
        }
        return hadHole ? new FarmResult { ok = true, message = "You fill the hole in." } : FarmResult.Fail("There is nothing to cover here.");
    }

    private static FarmResult Water(FieldState f, FarmRequest r, int cell, FarmWorker w, AgriSettings s, double now)
    {
        WaterProfile water = w.water;
        if (string.IsNullOrEmpty(water.id)) return FarmResult.Fail("You need a watering can or a bucket.");
        if (water.litres <= 0.01f) return FarmResult.Fail("It is empty. Fill it at a well or a water source.");
        float seconds = Mathf.Clamp(r.amount, 0.1f, s.maxHoldSeconds);
        float litres = Mathf.Min(water.litres, seconds * Mathf.Max(0.05f, water.flow));
        SoilGrid g = f.Grid;
        var cells = new List<int>();
        var weights = new List<float>();
        float sum = g.CellsInRadius(r.spot, Mathf.Max(0.05f, water.spread), cells, weights);
        WaterQuality q = s.Water(water.kind);
        float absorbed = 0f, before = 0f;
        for (int k = 0; k < cells.Count; k++)
        {
            int c = cells[k];
            g.Advance(c, now, s);
            before += g.Get(c, SoilChannel.Moisture);
            float mm = litres * weights[k] / Mathf.Max(0.001f, sum * g.CellArea);
            absorbed += g.AddWater(c, mm, q) * g.CellArea;
        }
        before /= Mathf.Max(1, cells.Count);
        if (water.wetsLeaves)
        {
            var near = new List<PlantState>();
            f.Near(r.spot, water.spread + 0.2f, near);
            foreach (PlantState p in near)
                if (p.Emerged) p.leafWet = Mathf.Max(p.leafWet, 2f);
        }
        var res = new FarmResult { ok = true, waterUsed = litres };
        float retention = g.Get(cell, SoilChannel.WaterRetention);
        res.message = before > 0.92f ? "The water pools on the surface and runs off." :
            absorbed < litres * 0.7f ? "Much of it runs away over the surface." :
            retention < 0.35f ? "The water vanishes into the soil at once." :
            before < 0.2f ? "The dry soil drinks it greedily." : "The water soaks in.";
        return res.Xp(AgriSkill.Agriculture, 0.3f);
    }

    private static FarmResult Amend(FieldState f, FarmRequest r, FarmWorker w, AgriSettings s, double now)
    {
        int index = f.Catalog?.AmendmentIndex(r.item) ?? -1;
        SoilAmendmentData a = f.Catalog?.Amendment(index);
        if (a == null) return FarmResult.Fail("You have nothing like that.");
        string id = AgriIds.Amendment(a.id);
        int have = w.Count(id);
        int portions = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1f, r.amount)), 1, 20);
        if (have <= 0) return FarmResult.Fail($"You have no {a.name}.");
        portions = Mathf.Min(portions, have);
        if (a.kind == AmendmentKind.Beneficial)
        {
            f.Beneficials.Add(new BeneficialRelease { center = r.spot, radius = 2.5f, until = now + a.beneficialDays * 24.0, amendment = index });
            var rb = new FarmResult { ok = true, message = $"You let the {a.name.ToLowerInvariant()} go among the plants." };
            rb.Take(id, portions);
            return rb.Xp(AgriSkill.PlantMedicine, 1f);
        }
        if (a.IsTreatment && !a.TouchesSoil) return FarmResult.Fail($"{a.name} is used on plants (Treat).");
        // How it is spread: wide, in a ring around a plant, or all on one spot.
        float radius = r.option == 2 ? 0.12f : r.option == 1 ? 0.25f : 0.5f;
        SoilGrid g = f.Grid;
        var cells = new List<int>();
        var weights = new List<float>();
        float sum = g.CellsInRadius(r.spot, radius, cells, weights);
        float area = Mathf.Max(g.CellArea, sum * g.CellArea);
        float skill = Skill(w, s, AgriSkill.Agriculture);
        for (int k = 0; k < cells.Count; k++)
        {
            int c = cells[k];
            g.Advance(c, now, s);
            // Unskilled hands spread unevenly: some cells get more than others.
            float even = 1f + AgriMath.Signed(f.Seed, c, (int)(now * 10.0)) * 0.35f * (1f - skill);
            g.Apply(c, index, a, portions * weights[k] / (sum * g.CellArea) * even, false);
        }
        var res = new FarmResult { ok = true };
        res.Take(id, portions);
        res.message = a.liquid ? $"You pour the {a.name.ToLowerInvariant()} over the soil." :
            a.mulch > 0f ? $"You lay the {a.name.ToLowerInvariant()} over the soil." :
            a.needsWorkingIn ? $"You scatter the {a.name.ToLowerInvariant()} over the soil." : $"You spread the {a.name.ToLowerInvariant()}.";
        if (area < 0.06f && portions >= 1 && a.kind == AmendmentKind.Fertilizer) res.lines.Add("It makes a heap in one spot.");
        res.Xp(AgriSkill.Agriculture, 1f);
        if (a.id.Contains("alchemical") || a.magicalBoost > 0f) res.Xp(AgriSkill.Alchemy, 0.5f);
        return res;
    }

    private static FarmResult Treat(FieldState f, FarmRequest r, PlantState plant, FarmWorker w, AgriSettings s, double now)
    {
        int index = f.Catalog?.AmendmentIndex(r.item) ?? -1;
        SoilAmendmentData a = f.Catalog?.Amendment(index);
        if (a == null) return FarmResult.Fail("You have nothing like that.");
        string id = AgriIds.Amendment(a.id);
        if (w.Count(id) <= 0) return FarmResult.Fail($"You have no {a.name}.");
        var targets = new List<PlantState>();
        f.Near(r.spot, 0.45f, targets);
        targets.RemoveAll(p => !p.Emerged);
        if (plant != null && plant.Emerged && !targets.Contains(plant)) targets.Add(plant);
        if (targets.Count == 0 && !a.TouchesSoil) return FarmResult.Fail("There is no plant here to treat.");
        float efficacy = 0.7f + 0.5f * Skill(w, s, AgriSkill.PlantMedicine);
        foreach (PlantState p in targets)
        {
            for (int k = 0; k < 8; k++) p.pests[k] *= 1f - Mathf.Clamp01(a.PestKill((PestKind)k) * efficacy);
            for (int k = 0; k < 6; k++) p.diseases[k] *= 1f - Mathf.Clamp01(a.DiseaseCure((DiseaseKind)k) * efficacy);
            p.health -= a.plantHarm * (1.2f - 0.4f * Skill(w, s, AgriSkill.PlantMedicine));
        }
        // Broad pesticides kill the helpers too.
        if (a.PestKill(PestKind.Aphids) > 0.6f)
            f.Beneficials.RemoveAll(b => (b.center - r.spot).sqrMagnitude < (b.radius + 0.5f) * (b.radius + 0.5f));
        if (a.TouchesSoil)
        {
            SoilGrid g = f.Grid;
            var cells = new List<int>();
            var weights = new List<float>();
            float sum = g.CellsInRadius(r.spot, 0.4f, cells, weights);
            for (int k = 0; k < cells.Count; k++)
            {
                g.Advance(cells[k], now, s);
                g.Apply(cells[k], index, a, weights[k] / (sum * g.CellArea), true);
            }
        }
        f.PlantVersion++;
        var res = new FarmResult { ok = true, message = targets.Count > 0 ? $"You treat the plant with {a.name.ToLowerInvariant()}." : $"You treat the soil with {a.name.ToLowerInvariant()}." };
        res.Take(id, 1);
        return res.Xp(AgriSkill.PlantMedicine, 1.5f);
    }

    // ---------------------------------------------------------------- instruments
    private static FarmResult Measure(FieldState f, FarmRequest r, int cell, PlantState plant, FarmWorker w, AgriSettings s, double now, AgriDate date)
    {
        InstrumentProfile? found = null;
        foreach (InstrumentProfile i in w.instruments)
            if (i.id == r.item) { found = i; break; }
        if (!found.HasValue) return FarmResult.Fail("You have no such instrument.");
        InstrumentProfile ins = found.Value;
        SoilReading reading = SoilInstruments.Read(f, cell, plant, ins, w, s, now, date);
        var res = new FarmResult { ok = true, message = reading.title };
        res.lines.AddRange(reading.lines);
        res.note = reading.Note();
        w.skills.measured++;
        AgriSkill skill = ins.kind == InstrumentKind.SoilAnalysisKit || ins.kind == InstrumentKind.AdvancedAnalyzer ? AgriSkill.Alchemy :
            ins.kind == InstrumentKind.BotanicalHandbook || ins.kind == InstrumentKind.Magnifier ? AgriSkill.Botany :
            ins.kind == InstrumentKind.DepthGauge || ins.kind == InstrumentKind.CompactionTester ? AgriSkill.Agriculture : AgriSkill.Botany;
        return res.Xp(skill, ins.kind == InstrumentKind.BotanicalHandbook || ins.kind == InstrumentKind.DepthGauge ? 0.1f : 0.6f);
    }

    // ---------------------------------------------------------------- care
    private static FarmResult Prune(FieldState f, FarmRequest r, PlantState p, FarmWorker w, AgriSettings s, AgriDate date, ref AgriRandom rng)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is no plant here to prune.");
        FarmToolProfile? tool = w.Tool(FarmToolKind.PruningShears) ?? w.Tool(FarmToolKind.Sickle);
        var kind = (PruneKind)Mathf.Clamp(r.option, 0, 5);
        if (!tool.HasValue && kind != PruneKind.Pinch) return FarmResult.Fail("You need pruning shears.");
        PlantSpeciesData d = p.data;
        float skill = Skill(w, s, AgriSkill.PlantMedicine);
        float clean = (tool?.quality ?? 0.4f) * (0.5f + 0.5f * (tool?.edge ?? 1f));
        bool woody = d.Look.shape == PlantShape.Tree || d.Look.shape == PlantShape.Bush || d.Look.shape == PlantShape.Vine;
        bool rightSeason = (d.pruneSeasons & AgriDate.Mask(date.Season)) != 0 || p.dormant;
        float wound = (0.04f + 0.08f * (1f - clean)) * (1f - 0.7f * skill);
        var res = new FarmResult { ok = true, toolWorn = tool?.id ?? "", wear = tool.HasValue ? 1 : 0 };
        switch (kind)
        {
            case PruneKind.DeadWood:
                res.message = p.deadWood > 0.1f ? "You cut out the dead wood." : "You find little dead wood to cut.";
                p.deadWood *= 0.15f;
                p.pruneBoost = Mathf.Min(1f, p.pruneBoost + (woody ? 0.1f : 0f));
                p.wounds += wound;
                break;
            case PruneKind.Diseased:
            {
                float sick = p.Disease(DiseaseKind.Fungal) + p.Disease(DiseaseKind.LeafSpot) + p.Disease(DiseaseKind.Bacterial) + p.Disease(DiseaseKind.Viral);
                float cut = 0.4f + 0.35f * skill;
                p.diseases[(int)DiseaseKind.Fungal] *= 1f - cut;
                p.diseases[(int)DiseaseKind.LeafSpot] *= 1f - cut;
                p.diseases[(int)DiseaseKind.Bacterial] *= 1f - cut * 0.8f;
                p.diseases[(int)DiseaseKind.Viral] *= 1f - cut * 0.3f;
                p.deadWood *= 0.6f;
                p.wounds += wound * 1.5f;
                if (sick < 0.05f) p.health -= 0.03f;
                res.message = sick > 0.05f ? "You cut away the spotted, blighted parts." : "You cut away what looked unwell.";
                break;
            }
            case PruneKind.Thin:
                p.canopy = Mathf.Max(0.15f, p.canopy - 0.25f);
                if (d.pruning != PruneNeed.None) p.pruneBoost = Mathf.Min(1f, p.pruneBoost + (rightSeason ? 0.15f : 0.05f));
                p.wounds += wound;
                res.message = "You open up the crown.";
                break;
            case PruneKind.Shape:
                p.size = Mathf.Max(0.25f, p.size * 0.85f);
                p.pruneBoost = Mathf.Min(1f, p.pruneBoost + 0.05f);
                RemovePieces(p, ProduceStage.Bud, ProduceStage.Mature, 0.25f, ref rng);
                p.wounds += wound;
                res.message = "You cut it back into shape.";
                break;
            case PruneKind.CutBack:
            {
                bool wants = d.pruning == PruneNeed.Regular || d.pruning == PruneNeed.Heavy;
                p.size = Mathf.Max(0.2f, p.size * 0.6f);
                if (wants && rightSeason)
                {
                    p.pruneBoost = Mathf.Min(1f, p.pruneBoost + (d.pruning == PruneNeed.Heavy ? 0.45f : 0.3f));
                    p.deadWood *= 0.3f;
                    p.canopy = Mathf.Max(0.15f, p.canopy - 0.35f);
                    res.message = "You cut it back hard.";
                }
                else
                {
                    p.health -= 0.15f * (1f - 0.5f * skill);
                    p.stress = Mathf.Min(1f, p.stress + 0.3f);
                    res.message = "You cut it back hard. Sap wells from the cuts.";
                }
                RemovePieces(p, ProduceStage.Bud, ProduceStage.Rotten, 1f, ref rng);
                p.wounds += wound * 2f;
                p.rootRadius = -1f;
                break;
            }
            default: // Pinch
                RemovePieces(p, ProduceStage.Bud, ProduceStage.Bud, d.Produce.part == ProducePart.Leaf ? 1f : 0f, ref rng);
                if (p.bolted)
                {
                    p.bolted = false;
                    p.stage = PlantStage.Vegetative;
                    p.progress = 1f;
                    p.flushTimer = 24f * 3f;
                }
                if (d.Produce.part == ProducePart.Leaf) p.pruneBoost = Mathf.Min(1f, p.pruneBoost + 0.1f);
                res.message = "You pinch out the tips.";
                break;
        }
        p.wounds = Mathf.Clamp01(p.wounds);
        if (d.pruning == PruneNeed.None && kind != PruneKind.Pinch && kind != PruneKind.Diseased && kind != PruneKind.DeadWood)
            p.health -= 0.04f;
        f.PlantVersion++;
        return res.Xp(AgriSkill.PlantMedicine, 2f);
    }

    private static void RemovePieces(PlantState p, ProduceStage from, ProduceStage to, float share, ref AgriRandom rng)
    {
        if (share <= 0f) return;
        for (int i = p.fruits.Count - 1; i >= 0; i--)
        {
            FruitState fr = p.fruits[i];
            if (fr.stage >= from && fr.stage <= to && rng.Chance(share)) p.fruits.RemoveAt(i);
        }
    }

    private static FarmResult CollectPollen(PlantState p, FarmWorker w, double now)
    {
        if (!w.Has(FarmToolKind.PollinationBrush)) return FarmResult.Fail("You need a pollination brush.");
        if (p == null || !p.Emerged) return FarmResult.Fail("There is no plant here.");
        bool flowers = false;
        foreach (FruitState fr in p.fruits) if (fr.stage == ProduceStage.Bud) { flowers = true; break; }
        if (!flowers) return FarmResult.Fail("It has no open flowers.");
        var res = new FarmResult { ok = true, message = "You brush the pollen from its flowers.", pollenChanged = true };
        res.pollen = new PollenState { has = true, species = p.species, genes = p.genes.ToCode(), plant = p.id, time = now };
        return res.Xp(AgriSkill.SeedBreeding, 0.5f);
    }

    private static FarmResult Pollinate(PlantState p, FarmWorker w, AgriSettings s, double now, ref AgriRandom rng)
    {
        FarmToolProfile? brush = w.Tool(FarmToolKind.PollinationBrush);
        if (!brush.HasValue) return FarmResult.Fail("You need a pollination brush.");
        if (p == null || !p.Emerged) return FarmResult.Fail("There is no plant here.");
        PollenState pollen = w.pollen;
        bool fresh = pollen.has && now - pollen.time <= 24.0;
        float skill = Skill(w, s, AgriSkill.SeedBreeding);
        int done = 0, open = 0;
        foreach (FruitState fr in p.fruits)
        {
            if (fr.stage != ProduceStage.Bud || fr.pollinated) continue;
            open++;
            if (!rng.Chance(0.55f + 0.4f * skill * brush.Value.quality)) continue;
            fr.pollinated = true;
            if (fresh && pollen.plant != p.id)
            {
                fr.crossed = true;
                fr.donor = PlantGenes.FromCode(pollen.genes);
                fr.donorSpecies = pollen.species ?? "";
            }
            done++;
        }
        if (open == 0) return FarmResult.Fail("It has no flowers waiting for pollen.");
        var res = new FarmResult { ok = true, message = done == 0 ? "You brush the flowers, but nothing seems to take." : fresh && pollen.plant != p.id ? "You dust the flowers with the pollen you carry." : "You brush from flower to flower." };
        return res.Xp(AgriSkill.SeedBreeding, 2f);
    }

    private static FarmResult RemovePests(FieldState f, FarmRequest r, PlantState p, FarmWorker w, AgriSettings s)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is no plant here.");
        bool tweezers = w.Has(FarmToolKind.Tweezers);
        float skill = Skill(w, s, AgriSkill.PlantMedicine);
        float picked = 0f;
        for (int k = 0; k < 8; k++)
        {
            if (r.option >= 0 && r.option < 8 && r.option != k) continue;
            var kind = (PestKind)k;
            float can = kind == PestKind.Mites || kind == PestKind.RootParasites ? 0.05f : kind == PestKind.Aphids || kind == PestKind.SapSuckers ? 0.45f : 1f;
            float frac = (0.3f + 0.45f * skill) * (tweezers ? 1.3f : 1f) * can;
            picked += p.pests[k] * Mathf.Clamp01(frac);
            p.pests[k] *= 1f - Mathf.Clamp01(frac);
        }
        f.PlantVersion++;
        var res = new FarmResult { ok = true, message = picked > 0.05f ? "You pick off every pest you can find." : "You search the leaves but find little." };
        return res.Xp(AgriSkill.PlantMedicine, picked > 0.05f ? 1f : 0.2f);
    }

    private static FarmResult Uproot(FieldState f, PlantState p)
    {
        if (p == null) return FarmResult.Fail("There is nothing here to pull up.");
        f.Remove(p);
        return new FarmResult { ok = true, message = p.IsSeed ? "You dig the seed out and throw it away." : "You pull the plant out, roots and all." }.Xp(AgriSkill.Agriculture, 0.2f);
    }

    private static FarmResult Support(FieldState f, PlantState p, FarmWorker w)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is no plant here to stake.");
        if (p.supported) return FarmResult.Fail("It already has a stake.");
        string stakes = null;
        foreach (FarmToolProfile t in w.tools)
            if (t.kind == FarmToolKind.Stakes) { stakes = t.id; break; }
        if (stakes == null) return FarmResult.Fail("You need garden stakes.");
        p.supported = true;
        f.PlantVersion++;
        var res = new FarmResult { ok = true, message = "You drive in a stake and tie the plant to it." };
        res.Take(AgriIds.Tool(stakes), 1);
        return res.Xp(AgriSkill.Agriculture, 0.5f);
    }

    private static FarmResult Label(FieldState f, PlantState p, string text)
    {
        if (p == null) return FarmResult.Fail("There is nothing here to label.");
        text = (text ?? "").Trim();
        if (text.Length > 40) text = text.Substring(0, 40);
        p.label = text;
        f.PlantVersion++;
        return new FarmResult { ok = true, message = text.Length > 0 ? $"You mark it: \"{text}\"." : "You take the label off." };
    }

    // ---------------------------------------------------------------- harvest and seeds
    /// <summary>How much of its quality a piece keeps when picked at a stage.</summary>
    public static float StageQuality(ProduceStage s)
    {
        switch (s)
        {
            case ProduceStage.Immature: return 0.45f;
            case ProduceStage.Developing: return 0.65f;
            case ProduceStage.Mature: return 0.85f;
            case ProduceStage.Ripe: return 1f;
            case ProduceStage.Overripe: return 0.6f;
            case ProduceStage.Rotten: return 0.05f;
            default: return 0.25f;
        }
    }

    /// <summary>
    /// The quality and potency of a piece picked now: what it grew through, the stage it is picked at, the plant's genes,
    /// the picker's hands; potency also the moon at picking, the hour it was sown at, magic in the soil for magical plants.
    /// </summary>
    public static void PieceValue(FieldState f, PlantState p, FruitState fr, FarmWorker w, AgriSettings s, AgriDate date, ref AgriRandom rng, out float quality, out float potency)
    {
        PlantSpeciesData d = p.data;
        ProduceSpec pr = d.Produce;
        bool herb = pr.part == ProducePart.Leaf || pr.part == ProducePart.Flower || pr.part == ProducePart.Root;
        float hands = herb ? Skill(w, s, AgriSkill.Herbalism) : Skill(w, s, AgriSkill.Agriculture);
        float grown = fr != null ? fr.Quality * 0.65f + p.Quality * 0.35f : p.Quality;
        float handling = 0.85f + 0.15f * hands;
        if (herb && rng.Chance(0.25f * (1f - hands))) handling *= 0.85f; // bruised
        float stage = fr != null ? StageQuality(fr.stage) : 1f;
        quality = Mathf.Clamp01((grown * stage + p.genes.QualityShift) * handling);
        float moon = 1f + (d.MoonPotency(date.Moon) - 1f) * d.lunarStrength;
        float time = 1f;
        if (d.plantingTime != PlantingTime.Any)
            time = (d.plantingTime == PlantingTime.Night) == p.plantedAtNight ? 1f + 0.2f * d.plantingTimeStrength : 1f - 0.3f * d.plantingTimeStrength;
        float magic = 1f;
        if (d.magicNeed > 0f && f.Grid.TryIndex(p.position, out int c))
            magic = Mathf.Lerp(0.4f, 1.25f, AgriMath.Smooth(0f, d.magicNeed * 1.5f, f.Grid.Get(c, SoilChannel.MagicEssence))) * (1f + 0.15f * Skill(w, s, AgriSkill.MagicalCultivation));
        float peak = fr == null ? 1f : fr.stage == ProduceStage.Ripe ? 1f : fr.stage == ProduceStage.Mature ? 0.8f : fr.stage == ProduceStage.Overripe ? 0.7f : 0.4f;
        float genes = Mathf.Max(p.genes.Factor(GeneTrait.MedicinalPotency), p.genes.Factor(GeneTrait.AlchemicalConcentration));
        potency = Mathf.Clamp01(pr.potency * (0.4f + 0.6f * quality) * peak * moon * time * magic * genes * (0.9f + 0.1f * hands));
    }

    private static FarmResult Harvest(FieldState f, FarmRequest r, PlantState p, FarmWorker w, AgriSettings s, AgriDate date, ref AgriRandom rng)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is nothing here to pick.");
        PlantSpeciesData d = p.data;
        ProduceSpec pr = d.Produce;
        if (PlantSim.OnePiece(pr) || (pr.wholePlant && pr.part == ProducePart.Grain))
            return HarvestPlant(f, r, p, w, s, date, ref rng);
        var codes = new List<int>();
        float best = 0f;
        for (int i = p.fruits.Count - 1; i >= 0; i--)
        {
            FruitState fr = p.fruits[i];
            if (r.piece > 0 && fr.id != r.piece) continue;
            if (!pr.Pickable(fr.stage) || fr.stage == ProduceStage.Rotten)
            {
                if (r.piece > 0) return FarmResult.Fail(fr.stage == ProduceStage.Rotten ? "It is rotten through." : "There is nothing there to pick yet.");
                continue;
            }
            PieceValue(f, p, fr, w, s, date, ref rng, out float q, out float pot);
            codes.Add(AgriItemCode.Produce(q, pot, fr.stage, p.genes.rare));
            best = Mathf.Max(best, q);
            p.fruits.RemoveAt(i);
        }
        if (codes.Count == 0) return FarmResult.Fail("There is nothing on it ready to pick.");
        p.harvests += codes.Count;
        f.PlantVersion++;
        var res = new FarmResult { ok = true, message = $"You pick {AgriText.Count(codes.Count)} {AgriText.Plural(pr.name, codes.Count).ToLowerInvariant()}." };
        res.Give(AgriIds.Produce(d.id), codes.Count, codes);
        w.skills.harvested += codes.Count;
        bool herb = pr.part == ProducePart.Leaf || pr.part == ProducePart.Flower;
        res.Xp(herb ? AgriSkill.Herbalism : AgriSkill.Agriculture, codes.Count * (0.6f + 1.5f * best));
        if (d.category == PlantCategory.Magical || d.magicNeed > 0f) res.Xp(AgriSkill.MagicalCultivation, codes.Count * 0.8f);
        return res;
    }

    private static FarmResult HarvestPlant(FieldState f, FarmRequest r, PlantState p, FarmWorker w, AgriSettings s, AgriDate date, ref AgriRandom rng)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is nothing here to harvest.");
        PlantSpeciesData d = p.data;
        ProduceSpec pr = d.Produce;
        bool grain = pr.part == ProducePart.Grain;
        if (grain && !w.Has(FarmToolKind.Sickle)) return FarmResult.Fail("You need a sickle to cut grain.");
        var codes = new List<int>();
        float best = 0f;
        if (PlantSim.OnePiece(pr))
        {
            FruitState piece = p.fruits.Count > 0 ? p.fruits[0] : null;
            if (piece == null || piece.stage < ProduceStage.Immature)
            {
                f.Remove(p);
                return new FarmResult { ok = true, message = pr.part == ProducePart.Root ? "You pull it up: the root has hardly formed." : "You cut it: there is hardly anything to it." }.Xp(AgriSkill.Agriculture, 0.2f);
            }
            if (piece.stage == ProduceStage.Rotten)
            {
                f.Remove(p);
                return new FarmResult { ok = true, message = "It has rotted where it grew." };
            }
            PieceValue(f, p, piece, w, s, date, ref rng, out float q, out float pot);
            float count = Mathf.Lerp(pr.countMin, pr.countMax, AgriMath.InverseLerp(0.35f, 0.9f, piece.Quality)) * p.genes.Factor(GeneTrait.FruitQuantity) * Mathf.Clamp01(piece.size * 1.2f + 0.2f);
            int n = Mathf.Max(1, Mathf.RoundToInt(count));
            // A shovel lifts roots whole; pulled by hand some break.
            if (pr.part == ProducePart.Root && !w.Has(FarmToolKind.Shovel) && rng.Chance(0.3f * (1f - Skill(w, s, AgriSkill.Herbalism)))) q *= 0.85f;
            for (int i = 0; i < n; i++)
            {
                float qi = Mathf.Clamp01(q + rng.Gauss() * 0.03f);
                codes.Add(AgriItemCode.Produce(qi, pot, piece.stage, p.genes.rare));
                best = Mathf.Max(best, qi);
            }
        }
        else
        {
            for (int i = p.fruits.Count - 1; i >= 0; i--)
            {
                FruitState fr = p.fruits[i];
                if (!pr.Pickable(fr.stage) || fr.stage == ProduceStage.Rotten) continue;
                PieceValue(f, p, fr, w, s, date, ref rng, out float q, out float pot);
                codes.Add(AgriItemCode.Produce(q, pot, fr.stage, p.genes.rare));
                best = Mathf.Max(best, q);
            }
        }
        f.Remove(p);
        var res = new FarmResult { ok = true };
        if (codes.Count == 0)
        {
            res.message = grain ? "You cut it down: the heads were empty." : "You clear the plant: there was nothing on it worth keeping.";
            return res.Xp(AgriSkill.Agriculture, 0.2f);
        }
        res.Give(AgriIds.Produce(d.id), codes.Count, codes);
        res.message = grain ? $"You cut it and gather {AgriText.Count(codes.Count)} heads." : pr.part == ProducePart.Root ? $"You lift {AgriText.Count(codes.Count)} {AgriText.Plural(pr.name, codes.Count).ToLowerInvariant()} from the soil." : $"You cut the {pr.name.ToLowerInvariant()}.";
        w.skills.harvested += codes.Count;
        bool herb = pr.part == ProducePart.Root && d.category != PlantCategory.Common || pr.part == ProducePart.Leaf && d.category == PlantCategory.Medicinal;
        res.Xp(herb ? AgriSkill.Herbalism : AgriSkill.Agriculture, 1f + 3f * best);
        if (d.category == PlantCategory.Magical || d.magicNeed > 0f) res.Xp(AgriSkill.MagicalCultivation, 2f);
        return res;
    }

    private static FarmResult CollectSeeds(FieldState f, FarmRequest r, PlantState p, FarmWorker w, AgriSettings s, ref AgriRandom rng)
    {
        if (p == null || !p.Emerged) return FarmResult.Fail("There is nothing here to take seeds from.");
        PlantSpeciesData d = p.data;
        ProduceSpec pr = d.Produce;
        float skill = Skill(w, s, AgriSkill.SeedBreeding);
        FruitState source = null;
        if (r.piece > 0) source = p.Fruit(r.piece);
        else
            foreach (FruitState fr in p.fruits)
                if (fr.stage >= ProduceStage.Mature && (source == null || fr.stage > source.stage)) source = fr;
        float mult;
        int viability;
        bool fromPlant = source == null || PlantSim.OnePiece(pr) && p.stage < PlantStage.Senescence && source.stage < ProduceStage.Overripe;
        if (source != null && source.stage >= ProduceStage.Mature && !(PlantSim.OnePiece(pr) && source.stage < ProduceStage.Overripe))
        {
            mult = source.stage == ProduceStage.Mature ? 0.5f : source.stage == ProduceStage.Ripe ? 1f : source.stage == ProduceStage.Overripe ? 1.2f : 0.6f;
            viability = source.stage == ProduceStage.Mature ? 1 : source.stage == ProduceStage.Ripe ? 2 : source.stage == ProduceStage.Overripe ? 3 : 1;
            fromPlant = false;
        }
        else if (p.stage == PlantStage.Senescence || (PlantSim.OnePiece(pr) && source != null && source.stage >= ProduceStage.Overripe))
        {
            // Plants without fruit give their seed as they go over (seed heads, bolted roots).
            mult = 1f;
            viability = 2;
            fromPlant = true;
        }
        else
        {
            return FarmResult.Fail(source == null ? "There is nothing ripe enough to hold seeds." : "Its seeds are not ripe yet.");
        }
        if (skill > 0.5f) viability = Mathf.Min(3, viability + 1);
        int n = Mathf.Max(1, Mathf.RoundToInt(pr.seedsPerPiece * mult * (0.6f + 0.4f * skill)));
        var bySpecies = new Dictionary<string, List<int>>();
        for (int i = 0; i < n; i++)
        {
            bool crossed = source != null && source.crossed;
            PlantGenes father = crossed ? source.donor : p.genes;
            PlantGenes child = PlantGenes.Cross(p.genes, father, ref rng, s.mutationChance, s.rareTraitChance, d.geneticVariation, skill * 0.5f, viability);
            string species = d.id;
            if (crossed && !string.IsNullOrEmpty(source.donorSpecies) && !string.Equals(source.donorSpecies, d.id, StringComparison.OrdinalIgnoreCase) && f.Catalog?.Hybrids != null)
                foreach (HybridRule h in f.Catalog.Hybrids)
                    if (h != null && h.Matches(d.id, source.donorSpecies) && f.Catalog.Species(h.child) != null && rng.Chance(h.chance * (1f + 0.5f * skill)))
                    {
                        species = h.child;
                        break;
                    }
            if (!bySpecies.TryGetValue(species, out List<int> list)) bySpecies[species] = list = new List<int>();
            list.Add(child.ToCode());
        }
        if (fromPlant) f.Remove(p);
        else
        {
            p.fruits.Remove(source);
            f.PlantVersion++;
        }
        var res = new FarmResult { ok = true };
        int total = 0;
        foreach (KeyValuePair<string, List<int>> kv in bySpecies)
        {
            res.Give(AgriIds.Seed(kv.Key), kv.Value.Count, kv.Value);
            total += kv.Value.Count;
        }
        res.message = total == 1 ? "You save a single seed." : $"You save {total} seeds.";
        if (bySpecies.Count > 1) res.lines.Add("Some of the seeds look different from the others.");
        return res.Xp(AgriSkill.SeedBreeding, 3f);
    }
}
