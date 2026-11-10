using System;
using System.Collections.Generic;
using UnityEngine;

// ==================================================================== Phase 5: trading, habitat upgrades, automation, upkeep
/// <summary>The machines a pen can be fitted with (Phase 5: only these three). Saved and sent as numbers: add at the END.</summary>
public enum AnimalDevice
{
    None,
    /// <summary>A sealed hopper over the feeder: it drops feed into the feeder as the animals empty it.</summary>
    Hopper,
    /// <summary>A float valve on the pen's water supply: it keeps the trough topped up with clean water.</summary>
    Waterer,
    /// <summary>Roll-away nest trays: eggs roll out of the nest as they are laid, so the nest never fills.</summary>
    Trays,
}

/// <summary>
/// One level a pen can be improved to (level 2 is the first of the rules' list): what it costs, and what it does - each
/// number is the whole effect at that level, not a step on the last.
/// </summary>
[Serializable]
public class AnimalPenLevelSpec
{
    [Tooltip("What the pen is called at this level (\"Improved\", \"Fine\").")]
    public string name = "Improved";
    [Tooltip("Money to improve a pen of the rules' Reference Area to this level from the one below (a larger pen costs more, a smaller less: x area / Reference Area, 0.5-4).")]
    [Min(0)] public int cost = 400;
    [Tooltip("Room it makes (stalls, perches, a better layout): its usable area x (1 + this). More animals fit, and they are less crowded.")]
    [Range(0f, 2f)] public float space = 0.25f;
    [Tooltip("Comfort its animals settle toward is this much higher (draught-proofing, dry floors, better light).")]
    [Range(0f, 40f)] public float comfort = 6f;
    [Tooltip("How much its floor is soiled, as a share of an unimproved pen's (drains, raised floors: 0.85 = 15% less dirt).")]
    [Range(0.1f, 1f)] public float dirt = 0.85f;
    [Tooltip("Its animals make their products this much faster (x 1 + this).")]
    [Range(0f, 1f)] public float production = 0.05f;
    [Tooltip("Money it costs to keep up per game day, for a pen of the Reference Area (scaled as the cost is).")]
    [Min(0f)] public float upkeepPerDay = 2f;

    public AnimalPenLevelSpec() { }

    public AnimalPenLevelSpec(string name, int cost, float space, float comfort, float dirt, float production, float upkeepPerDay)
    {
        this.name = name; this.cost = cost; this.space = space; this.comfort = comfort; this.dirt = dirt; this.production = production;
        this.upkeepPerDay = upkeepPerDay;
    }
}

/// <summary>
/// How pens are improved and fitted with machines, and what keeping them up costs (Phase 5). Upkeep falls due as the
/// game's days go by; a pen that owes more than its Grace Days falls into disrepair: its machines stop and its level's
/// comfort, cleanliness and production do not count (its room stays: nobody is turned out) until what it owes is paid.
/// </summary>
[Serializable]
public class AnimalUpgradeRules
{
    [Tooltip("Pens can be improved and fitted with machines.")]
    public bool enabled = true;
    [Tooltip("The levels above the pen as built (level 1): the first is level 2.")]
    public List<AnimalPenLevelSpec> levels = DefaultLevels();
    [Tooltip("The area (m²) a level's cost and upkeep are given for: a pen of another size pays x its area / this (0.5-4).")]
    [Min(1f)] public float referenceArea = 24f;

    [Header("Hopper")]
    [Min(0)] public int hopperCost = 300;
    [Min(0f)] public float hopperUpkeepPerDay = 1f;
    [Tooltip("Portions of feed the hopper holds (above what the feeder holds).")]
    [Min(1f)] public float hopperCapacity = 240f;
    [Tooltip("It drops feed into the feeder while the feeder holds less than this share of what it can.")]
    [Range(0.05f, 1f)] public float hopperKeepsFeeder = 0.5f;
    [Tooltip("Feed ages this much as fast in the sealed hopper as it would in the open feeder (0.5: half as fast).")]
    [Range(0f, 1f)] public float hopperAgePace = 0.5f;

    [Header("Automatic waterer")]
    [Min(0)] public int watererCost = 250;
    [Min(0f)] public float watererUpkeepPerDay = 1f;
    [Tooltip("Litres of clean well water it brings into the trough per game hour (up to what the trough holds).")]
    [Min(0.1f)] public float watererLitresPerHour = 8f;

    [Header("Egg trays")]
    [Min(0)] public int traysCost = 150;
    [Min(0f)] public float traysUpkeepPerDay = 0.5f;
    [Tooltip("Units the trays hold (eggs roll in as they are laid: the nest never fills while there is room).")]
    [Min(1)] public int trayCapacity = 36;

    [Header("Upkeep")]
    [Tooltip("Days of upkeep a pen may owe before it falls into disrepair.")]
    [Min(0f)] public float graceDays = 3f;
    [Tooltip("Most days of upkeep a pen can owe (what falls due after is not counted), and can be paid ahead.")]
    [Min(1f)] public float maxOwedDays = 30f;

    public static List<AnimalPenLevelSpec> DefaultLevels() => new List<AnimalPenLevelSpec>
    {
        new AnimalPenLevelSpec("Improved", 400, 0.25f, 6f, 0.85f, 0.05f, 2f),
        new AnimalPenLevelSpec("Fine", 1200, 0.5f, 12f, 0.7f, 0.1f, 5f),
    };

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (levels == null) return;
        for (int i = 0; i < levels.Count; i++)
        {
            AnimalPenLevelSpec l = levels[i];
            string who = $"pen level {i + 2}";
            if (l == null)
            {
                errors?.Add($"{who}: empty.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(l.name)) warnings?.Add($"{who}: no name.");
            AnimalPenLevelSpec below = i > 0 ? levels[i - 1] : null;
            if (below != null && (l.space < below.space || l.comfort < below.comfort || l.dirt > below.dirt || l.production < below.production))
                warnings?.Add($"{who} ({l.name}): it does less than level {i + 1} in some way (each level's numbers are its whole effect).");
        }
    }
}

/// <summary>
/// How animals change hands (Phase 5): crating an animal to sell it or trade it, and what a crated animal is worth. A crate
/// is a livestock item whose per-copy code names the animal it holds: whoever lets it out owns it.
/// </summary>
[Serializable]
public class AnimalTradeRules
{
    [Tooltip("Owners may crate their animals (to sell them to a merchant or trade them to a player).")]
    public bool allowCrating = true;
    [Tooltip("An animal weaker than this is not fit to travel in a crate.")]
    [Range(0f, 100f)] public float crateMinHealth = 30f;
    [Tooltip("Real days a crated animal waits to be let out; after them it is gone (the carrier sold it on). 0: it waits for ever.")]
    [Min(0f)] public float crateKeepDays = 14f;
    [Tooltip("The share of its affinity an animal keeps with a new owner who lets it out (its crater letting it out again keeps all of it).")]
    [Range(0f, 1f)] public float affinityKept = 0.25f;

    [Header("Worth (x the breed's price for a young one)")]
    [Min(0f)] public float newbornWorth = 0.7f;
    [Min(0f)] public float babyWorth = 1f;
    [Min(0f)] public float juvenileWorth = 1.2f;
    [Min(0f)] public float adolescentWorth = 1.6f;
    [Min(0f)] public float adultWorth = 2.4f;
    [Min(0f)] public float seniorWorth = 1f;
    [Tooltip("How much its grade moves its worth: grade 0 is worth x (1 - this), grade 15 x (1 + this).")]
    [Range(0f, 0.9f)] public float gradeSpread = 0.5f;

    [Header("Its grade (0-15), judged when it is crated")]
    [Range(0f, 1f)] public float healthWeight = 0.25f;
    [Range(0f, 1f)] public float nutritionWeight = 0.15f;
    [Range(0f, 1f)] public float welfareWeight = 0.15f;
    [Range(0f, 1f)] public float geneticsWeight = 0.3f;
    [Range(0f, 1f)] public float rareWeight = 0.15f;

    /// <summary>What an animal of a stage is worth against a young one of its breed (its crate's price).</summary>
    public float StageWorth(GrowthStage s)
    {
        switch (s)
        {
            case GrowthStage.Newborn: return newbornWorth;
            case GrowthStage.Baby: return babyWorth;
            case GrowthStage.Juvenile: return juvenileWorth;
            case GrowthStage.Adolescent: return adolescentWorth;
            case GrowthStage.Adult: return adultWorth;
            default: return seniorWorth;
        }
    }
}

/// <summary>A lot of a product that rolled out of a nest into a pen's egg trays: the lot, and whose animal laid it (its origin is kept whole).</summary>
[Serializable]
public class AnimalTrayLot
{
    public string product = "";
    public string animal = "", animalName = "", species = "", breed = "", owner = "";
    [Tooltip("Its animal carries a rare trait (a Special lot is possible).")]
    public bool rare;
    public AnimalProductLot lot = new AnimalProductLot();
}

/// <summary>An animal in a crate (out of the world: it lives again from the moment it is let out), kept by its crate's serial.</summary>
[Serializable]
public class AnimalCrated
{
    public long serial;
    [Tooltip("Game hour it was crated, and the real hour (its wait is counted in real days).")]
    public double cratedAt, cratedAtReal;
    [Tooltip("Who crated it (their lasting key) and their name.")]
    public string by = "", byName = "";
    [Range(0, 15)] public int grade;
    public AnimalRecord animal;
}

/// <summary>
/// The pure logic of Phase 5's pens: their level and machines, the upkeep they owe, what a level and a machine cost, the
/// hopper, the waterer and the egg trays at work. Deterministic (no randomness): the simulation calls it every step, in
/// the same order every time, so a caught-up stretch ends as a watched one.
/// </summary>
public static class AnimalUpgrades
{
    /// <summary>The texts of an Upgrade request.</summary>
    public const string LevelText = "level", HopperText = "hopper", WatererText = "waterer", TraysText = "trays", RemovePrefix = "remove:";
    /// <summary>The days a window's "pay a week ahead" pays (a PayUpkeep request's count).</summary>
    public const int WeekAhead = 7;

    public static AnimalUpgradeRules RulesOf(AnimalRules rules) => rules?.upgrades ?? new AnimalUpgradeRules();

    // ---------------------------------------------------------------- level
    /// <summary>A pen's level (1: as built), within the rules' levels; 1 while the rules' Upgrades are off (nothing of a level counts then).</summary>
    public static int Level(AnimalPenRecord pen, AnimalRules rules)
    {
        if (pen == null || !RulesOf(rules).enabled) return 1;
        return Math.Max(1, Math.Min(MaxLevel(rules), pen.level));
    }

    /// <summary>The highest level a pen can be improved to.</summary>
    public static int MaxLevel(AnimalRules rules)
    {
        AnimalUpgradeRules u = RulesOf(rules);
        return 1 + (u.levels != null ? u.levels.Count : 0);
    }

    /// <summary>A level's spec (null for level 1, as built).</summary>
    public static AnimalPenLevelSpec LevelSpec(int level, AnimalRules rules)
    {
        AnimalUpgradeRules u = RulesOf(rules);
        int i = level - 2;
        return u.levels != null && i >= 0 && i < u.levels.Count ? u.levels[i] : null;
    }

    /// <summary>What a level is called ("Basic" for level 1).</summary>
    public static string LevelName(int level, AnimalRules rules)
    {
        AnimalPenLevelSpec l = LevelSpec(level, rules);
        return l == null ? "Basic" : string.IsNullOrWhiteSpace(l.name) ? $"Level {level}" : l.name.Trim();
    }

    /// <summary>A pen's cost and level upkeep scale with its area against the rules' Reference Area (0.5-4).</summary>
    public static float SizeScale(AnimalPenSpec spec, AnimalRules rules)
    {
        float area = spec != null ? Mathf.Max(0f, spec.area) : 0f;
        return Mathf.Clamp(area / Mathf.Max(1f, RulesOf(rules).referenceArea), 0.5f, 4f);
    }

    /// <summary>The usable area of a pen: its area x (1 + its level's space). Room and crowding are judged against it.</summary>
    public static float Area(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        float area = spec != null ? Mathf.Max(0f, spec.area) : 0f;
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        return l == null ? area : area * (1f + Mathf.Max(0f, l.space));
    }

    /// <summary>The most animals a pen with a set capacity takes at its level (0: none set, its area decides).</summary>
    public static int Capacity(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        if (spec == null || spec.capacityOverride <= 0) return 0;
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        return l == null ? spec.capacityOverride : Mathf.FloorToInt(spec.capacityOverride * (1f + Mathf.Max(0f, l.space)) + 1e-4f);
    }

    /// <summary>The comfort its level adds (none in disrepair).</summary>
    public static float ComfortBonus(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        return l == null || InDisrepair(pen, spec, rules) ? 0f : Mathf.Max(0f, l.comfort);
    }

    /// <summary>Its floor's soiling against an unimproved pen's (1 in disrepair).</summary>
    public static float DirtFactor(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        return l == null || InDisrepair(pen, spec, rules) ? 1f : Mathf.Clamp(l.dirt, 0.1f, 1f);
    }

    /// <summary>How much faster its animals make their products (0 in disrepair).</summary>
    public static float ProductionBonus(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        return l == null || InDisrepair(pen, spec, rules) ? 0f : Mathf.Max(0f, l.production);
    }

    // ---------------------------------------------------------------- machines
    public static bool Has(AnimalPenRecord pen, AnimalDevice d)
    {
        if (pen == null) return false;
        switch (d)
        {
            case AnimalDevice.Hopper: return pen.hopper;
            case AnimalDevice.Waterer: return pen.waterer;
            case AnimalDevice.Trays: return pen.trays;
            default: return false;
        }
    }

    /// <summary>Fits a machine (by whom: a lasting key) or takes it out.</summary>
    public static void Set(AnimalPenRecord pen, AnimalDevice d, bool on, string by = "")
    {
        if (pen == null) return;
        string who = on ? by ?? "" : "";
        switch (d)
        {
            case AnimalDevice.Hopper: pen.hopper = on; pen.hopperBy = who; break;
            case AnimalDevice.Waterer: pen.waterer = on; pen.watererBy = who; break;
            case AnimalDevice.Trays: pen.trays = on; pen.traysBy = who; break;
        }
    }

    /// <summary>Who fitted a machine (empty: nobody known - an old save, or the editor).</summary>
    public static string FittedBy(AnimalPenRecord pen, AnimalDevice d)
    {
        if (pen == null) return "";
        switch (d)
        {
            case AnimalDevice.Hopper: return pen.hopperBy ?? "";
            case AnimalDevice.Waterer: return pen.watererBy ?? "";
            case AnimalDevice.Trays: return pen.traysBy ?? "";
            default: return "";
        }
    }

    /// <summary>
    /// May one take a machine out? Its fitter, or one who may care for others' animals here (a tenant and those they let
    /// in): not a passer-by on public land, who may keep animals there but did not pay for it.
    /// </summary>
    public static bool MayRemove(AnimalPenRecord pen, AnimalDevice d, string key, AnimalRights rights)
    {
        if (!Has(pen, d) || string.IsNullOrEmpty(key)) return false;
        if ((rights & AnimalRights.CareForOthers) != 0) return true;
        string by = FittedBy(pen, d);
        return !string.IsNullOrEmpty(by) && by == key && (rights & AnimalRights.Keep) != 0;
    }

    /// <summary>Why a pen cannot take a machine (null: it can): a hopper needs a feeder, a waterer a trough.</summary>
    public static string WhyNotFit(AnimalPenSpec spec, AnimalDevice d)
    {
        if (d == AnimalDevice.Hopper && (spec == null || spec.feederCapacity <= 0f)) return "The pen has no feeder for a hopper to feed.";
        if (d == AnimalDevice.Waterer && (spec == null || spec.waterCapacity <= 0f)) return "The pen has no trough for a waterer to fill.";
        return null;
    }

    /// <summary>A machine at work: fitted, and the pen not in disrepair.</summary>
    public static bool Working(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, AnimalDevice d) =>
        RulesOf(rules).enabled && Has(pen, d) && !InDisrepair(pen, spec, rules);

    public static string DeviceName(AnimalDevice d)
    {
        switch (d)
        {
            case AnimalDevice.Hopper: return "Feed hopper";
            case AnimalDevice.Waterer: return "Automatic waterer";
            case AnimalDevice.Trays: return "Egg trays";
            default: return "";
        }
    }

    public static string DeviceText(AnimalDevice d)
    {
        switch (d)
        {
            case AnimalDevice.Hopper: return HopperText;
            case AnimalDevice.Waterer: return WatererText;
            case AnimalDevice.Trays: return TraysText;
            default: return "";
        }
    }

    /// <summary>The machine an Upgrade request's text names (None: the level, or nothing).</summary>
    public static AnimalDevice DeviceOf(string text)
    {
        string t = (text ?? "").Trim().ToLowerInvariant();
        if (t.StartsWith(RemovePrefix, StringComparison.Ordinal)) t = t.Substring(RemovePrefix.Length).Trim();
        switch (t)
        {
            case HopperText: return AnimalDevice.Hopper;
            case WatererText: return AnimalDevice.Waterer;
            case TraysText: return AnimalDevice.Trays;
            default: return AnimalDevice.None;
        }
    }

    public static bool IsRemoval(string text) => (text ?? "").Trim().StartsWith(RemovePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>What a machine costs to fit.</summary>
    public static long DevicePrice(AnimalDevice d, AnimalRules rules)
    {
        AnimalUpgradeRules u = RulesOf(rules);
        switch (d)
        {
            case AnimalDevice.Hopper: return Math.Max(0, u.hopperCost);
            case AnimalDevice.Waterer: return Math.Max(0, u.watererCost);
            case AnimalDevice.Trays: return Math.Max(0, u.traysCost);
            default: return 0L;
        }
    }

    public static float DeviceUpkeep(AnimalDevice d, AnimalRules rules)
    {
        AnimalUpgradeRules u = RulesOf(rules);
        switch (d)
        {
            case AnimalDevice.Hopper: return Mathf.Max(0f, u.hopperUpkeepPerDay);
            case AnimalDevice.Waterer: return Mathf.Max(0f, u.watererUpkeepPerDay);
            case AnimalDevice.Trays: return Mathf.Max(0f, u.traysUpkeepPerDay);
            default: return 0f;
        }
    }

    /// <summary>What improving a pen to its next level costs (-1: it is at the top level).</summary>
    public static long NextLevelPrice(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules) + 1, rules);
        if (l == null) return -1L;
        return (long)Math.Round(Math.Max(0, l.cost) * (double)SizeScale(spec, rules));
    }

    /// <summary>Money a pen costs to keep up per game day: its level's (x its size) and its machines'.</summary>
    public static float UpkeepPerDay(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        if (pen == null || !RulesOf(rules).enabled) return 0f;
        float day = 0f;
        AnimalPenLevelSpec l = LevelSpec(Level(pen, rules), rules);
        if (l != null) day += Mathf.Max(0f, l.upkeepPerDay) * SizeScale(spec, rules);
        if (pen.hopper) day += DeviceUpkeep(AnimalDevice.Hopper, rules);
        if (pen.waterer) day += DeviceUpkeep(AnimalDevice.Waterer, rules);
        if (pen.trays) day += DeviceUpkeep(AnimalDevice.Trays, rules);
        return day;
    }

    /// <summary>What paying a pen's upkeep costs now (what it owes, rounded up; nothing while it is paid ahead).</summary>
    public static long OwedPrice(AnimalPenRecord pen) => pen == null ? 0L : (long)Math.Ceiling(Math.Max(0f, pen.upkeepOwed) - 1e-4);

    /// <summary>
    /// What paying a pen's upkeep up to <paramref name="daysAhead"/> game days ahead costs (what it owes, and that many
    /// days' upkeep beyond, less what is paid ahead already; rounded up). At most the rules' Max Owed Days ahead.
    /// </summary>
    public static long PayPrice(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, int daysAhead)
    {
        if (pen == null) return 0L;
        float day = UpkeepPerDay(pen, spec, rules);
        int days = Math.Max(0, Math.Min(daysAhead, Mathf.FloorToInt(Mathf.Max(1f, RulesOf(rules).maxOwedDays))));
        double due = pen.upkeepOwed + (double)day * days;
        return due <= 1e-4 ? 0L : (long)Math.Ceiling(due - 1e-4);
    }

    /// <summary>Game days of upkeep paid ahead (0: none, or it owes).</summary>
    public static float PaidAheadDays(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        float day = UpkeepPerDay(pen, spec, rules);
        return pen == null || pen.upkeepOwed >= 0f || day <= 0f ? 0f : -pen.upkeepOwed / day;
    }

    /// <summary>It owes more than its grace days' upkeep: its machines stop and its level counts for nothing but room.</summary>
    public static bool InDisrepair(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        if (pen == null || pen.upkeepOwed <= 0f) return false;
        float day = UpkeepPerDay(pen, spec, rules);
        if (day <= 0f) return false;
        return pen.upkeepOwed > Mathf.Max(0f, RulesOf(rules).graceDays) * day + 1e-4f;
    }

    /// <summary>The upkeep that falls due over a step (at most the rules' Max Owed Days of it; what was paid ahead is spent first).</summary>
    public static void Accrue(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, double dt)
    {
        if (pen == null || dt <= 0.0) return;
        float day = UpkeepPerDay(pen, spec, rules);
        if (day <= 0f) return;
        float cap = day * Mathf.Max(1f, RulesOf(rules).maxOwedDays);
        if (pen.upkeepOwed >= cap) return;
        pen.upkeepOwed = Mathf.Min(cap, pen.upkeepOwed + day * (float)(dt / 24.0));
    }

    // ---------------------------------------------------------------- the hopper
    /// <summary>Room left in the hopper (portions).</summary>
    public static float HopperRoom(AnimalPenRecord pen, AnimalRules rules) =>
        pen == null ? 0f : Mathf.Max(0f, RulesOf(rules).hopperCapacity - pen.HopperPortions);

    /// <summary>Puts portions of a food in the hopper (lots of nearly the same quality and age kept together; what does not fit is lost).</summary>
    public static float StockHopper(AnimalPenRecord pen, AnimalRules rules, AnimalFoodData food, float portions, float quality, string by, double now)
    {
        if (pen == null || food == null) return 0f;
        float put = Mathf.Min(HopperRoom(pen, rules), Mathf.Max(0f, portions));
        if (put <= 0f) return 0f;
        AddLot(pen.hopperFeed, food.id, put, quality, now, by);
        pen.revision++;
        return put;
    }

    /// <summary>
    /// Adds portions to a store of lots (a feeder, a hopper): to a lot of the same food, nearly the same quality and age,
    /// or as a lot of its own.
    /// </summary>
    public static void AddLot(List<FeederLot> lots, string food, float portions, float quality, double since, string by)
    {
        if (lots == null || portions <= 0f) return;
        quality = AnimalMath.Clamp01(quality);
        foreach (FeederLot l in lots)
        {
            if (l == null || l.food != food || Mathf.Abs(l.quality - quality) > 0.05f || Math.Abs(l.since - since) > 6.0) continue;
            float total = l.portions + portions;
            l.quality = (l.quality * l.portions + quality * portions) / total;
            l.since = (l.since * l.portions + since * portions) / total;
            l.portions = total;
            return;
        }
        lots.Add(new FeederLot { food = food ?? "", portions = portions, quality = quality, since = since, by = by ?? "" });
    }

    /// <summary>
    /// The hopper at work (before its animals eat, every step): while the feeder holds less than the rules' share of what it
    /// can, feed drops into it from the hopper, the oldest first. Feed ages in the sealed hopper at the rules' pace: the hours
    /// it lay there count for less once it is in the open feeder (its time in the hopper x Hopper Age Pace).
    /// </summary>
    public static float Dispense(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, double now)
    {
        if (pen?.hopperFeed == null || pen.hopperFeed.Count == 0 || spec == null) return 0f;
        AnimalUpgradeRules u = RulesOf(rules);
        float capacity = Mathf.Max(0f, spec.feederCapacity);
        float level = capacity * Mathf.Clamp01(u.hopperKeepsFeeder);
        float have = pen.FeederPortions;
        if (have >= level - 1e-4f) return 0f;
        float want = Mathf.Min(level - have, capacity - have);
        float moved = 0f;
        // Oldest first (by when it went in; then by food id, so the order is the same on every machine).
        pen.hopperFeed.RemoveAll(l => l == null || l.portions <= 0.0001f);
        pen.hopperFeed.Sort((x, y) =>
        {
            int c = x.since.CompareTo(y.since);
            return c != 0 ? c : string.CompareOrdinal(x.food, y.food);
        });
        float pace = Mathf.Clamp01(u.hopperAgePace);
        for (int i = 0; i < pen.hopperFeed.Count && want > 1e-4f; i++)
        {
            FeederLot l = pen.hopperFeed[i];
            float take = Mathf.Min(l.portions, want);
            if (take <= 0f) continue;
            double age = Math.Max(0.0, now - l.since) * pace;
            AddLot(pen.feeder, l.food, take, l.quality, now - age, l.by);
            l.portions -= take;
            want -= take;
            moved += take;
        }
        pen.hopperFeed.RemoveAll(l => l == null || l.portions <= 0.0001f);
        return moved;
    }

    /// <summary>
    /// A hopper taken out: its feed tips into the feeder as far as it fits (the rest is lost). Returns the portions that
    /// went into the feeder; <paramref name="lost"/> those that did not fit.
    /// </summary>
    public static float EmptyHopper(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, double now, out float lost)
    {
        lost = 0f;
        if (pen?.hopperFeed == null) return 0f;
        float room = Mathf.Max(0f, (spec != null ? spec.feederCapacity : 0f) - pen.FeederPortions);
        float pace = Mathf.Clamp01(RulesOf(rules).hopperAgePace);
        float put = 0f;
        // Oldest first, as it is dispensed (what does not fit is the newest).
        pen.hopperFeed.RemoveAll(l => l == null || l.portions <= 0.0001f);
        pen.hopperFeed.Sort((x, y) =>
        {
            int c = x.since.CompareTo(y.since);
            return c != 0 ? c : string.CompareOrdinal(x.food, y.food);
        });
        foreach (FeederLot l in pen.hopperFeed)
        {
            if (l == null || l.portions <= 0f) continue;
            float take = Mathf.Min(room, l.portions);
            if (take > 0f) AddLot(pen.feeder, l.food, take, l.quality, now - Math.Max(0.0, now - l.since) * pace, l.by);
            room -= take;
            put += take;
            lost += l.portions - take;
        }
        pen.hopperFeed.Clear();
        return put;
    }

    /// <summary>What a pen's waterer brings into its trough over a step (0 when it has none or it is not working).</summary>
    public static float WatererInflow(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, double dt) =>
        Working(pen, spec, rules, AnimalDevice.Waterer) ? Mathf.Max(0f, RulesOf(rules).watererLitresPerHour) * (float)Math.Max(0.0, dt) : 0f;

    // ---------------------------------------------------------------- the egg trays
    /// <summary>Room left in the trays (units).</summary>
    public static int TrayRoom(AnimalPenRecord pen, AnimalRules rules) => pen == null ? 0 : Math.Max(0, RulesOf(rules).trayCapacity - pen.TrayUnits);

    /// <summary>
    /// The lots that wait in its animals' nests and roll away (products marked Trays) go into the trays, as far as there is
    /// room (a lot that does not fit whole is split: the rest waits in the nest). In id order, so the same eggs roll in on
    /// every machine. Returns the units that rolled in.
    /// </summary>
    public static int RollToTrays(AnimalHerd herd, IAnimalCatalog cat, AnimalRules rules, double now = -1.0)
    {
        AnimalPenRecord pen = herd?.pen;
        if (pen == null || cat == null) return 0;
        int room = TrayRoom(pen, rules);
        if (room <= 0) return 0;
        int rolled = 0;
        foreach (AnimalRecord a in herd.animals)
        {
            if (room <= 0) break;
            if (a == null || !a.Alive || a.products == null) continue;
            AnimalSpeciesData sp = cat.Species(a.species);
            if (sp?.products == null) continue;
            foreach (AnimalProductSpec p in sp.products)
            {
                if (room <= 0) break;
                if (p == null || !p.trays) continue;
                AnimalProductState st = AnimalProduction.State(a, p.id, false);
                if (st?.waiting == null || st.waiting.Count == 0) continue;
                int i = 0;
                while (i < st.waiting.Count && room > 0)
                {
                    AnimalProductLot lot = st.waiting[i];
                    if (lot == null || lot.units <= 0)
                    {
                        st.waiting.RemoveAt(i);
                        continue;
                    }
                    int take = Math.Min(room, lot.units);
                    AnimalProductLot moved = take == lot.units ? lot : Copy(lot, take);
                    pen.tray.Add(new AnimalTrayLot
                    {
                        product = p.id, animal = a.id, animalName = a.name ?? "", species = a.species ?? "", breed = a.breed ?? "", owner = a.owner ?? "",
                        rare = AnimalProduction.Rare(a), lot = moved,
                    });
                    room -= take;
                    rolled += take;
                    if (take == lot.units) st.waiting.RemoveAt(i);
                    else
                    {
                        lot.units -= take;
                        i++;
                    }
                }
                // Its nest has room again: a full nest's cycle goes on (a cycle not begun starts now, as after a collection).
                if (st.status == AnimalProductStatus.Full && st.Waiting < Math.Max(1, p.capacity))
                {
                    st.status = AnimalProductStatus.Producing;
                    if (st.hours <= 0f && now >= 0.0) st.cycleStart = now;
                }
            }
        }
        return rolled;
    }

    private static AnimalProductLot Copy(AnimalProductLot l, int units) => new AnimalProductLot
    {
        units = units, quality = l.quality, cycle = l.cycle, cycleStart = l.cycleStart, madeAt = l.madeAt,
        nutrition = l.nutrition, welfare = l.welfare, health = l.health, environment = l.environment, hygiene = l.hygiene,
    };

    /// <summary>What waits in the trays ages as it would in the nest (see <see cref="AnimalProduction.AgeLot"/>).</summary>
    public static void AgeTrays(AnimalPenRecord pen, IAnimalCatalog cat, AnimalRules rules, float dt, double now)
    {
        if (pen?.tray == null || pen.tray.Count == 0 || cat == null) return;
        AnimalProductionRules r = rules?.production ?? new AnimalProductionRules();
        // A lot of a product its species no longer gives (the content changed) can never be collected: it goes.
        pen.tray.RemoveAll(t => t?.lot == null || t.lot.units <= 0 || AnimalProduction.Spec(cat.Species(t.species), t.product) == null);
        foreach (AnimalTrayLot t in pen.tray)
            AnimalProduction.AgeLot(t.lot, AnimalProduction.Spec(cat.Species(t.species), t.product), r, dt, now);
    }

    /// <summary>One may collect a tray lot: its animal's owner, or (Others Collect on) one who may care for others' animals here.</summary>
    public static bool MayCollect(AnimalTrayLot t, string key, AnimalRights rights, AnimalRules rules) =>
        t != null && !string.IsNullOrEmpty(key) &&
        (t.owner == key || ((rules?.production == null || rules.production.othersCollect) && (rights & AnimalRights.CareForOthers) != 0));
}

/// <summary>
/// What an animal is worth in trade (Phase 5): its grade (0-15, judged when it is crated: its health, nutrition and welfare,
/// its genes, a rare trait) and the worth of its stage. The grade travels in its crate's code, so a merchant and a client
/// price it the same without seeing the animal.
/// </summary>
public static class AnimalTrade
{
    public static AnimalTradeRules RulesOf(AnimalRules rules) => rules?.trade ?? new AnimalTradeRules();

    /// <summary>
    /// Its grade, 0-15: the rules' weighted health, nutrition and welfare (0-100), genetic merit (the mean of its
    /// production, efficiency, resistance, fertility and longevity genes, less half its inbreeding) and a rare trait.
    /// </summary>
    public static int Grade(AnimalRecord a, AnimalRules rules)
    {
        if (a == null) return 0;
        AnimalTradeRules t = RulesOf(rules);
        float wH = Mathf.Max(0f, t.healthWeight), wN = Mathf.Max(0f, t.nutritionWeight), wW = Mathf.Max(0f, t.welfareWeight);
        float wG = Mathf.Max(0f, t.geneticsWeight), wR = Mathf.Max(0f, t.rareWeight);
        float sum = wH + wN + wW + wG + wR;
        if (sum <= 0f) return 7;
        float worth = (wH * AnimalMath.Clamp01(a.health / 100f) + wN * AnimalMath.Clamp01(a.nutrition / 100f) + wW * AnimalMath.Clamp01(a.welfare / 100f)
                       + wG * Merit(a.genes) + wR * (AnimalProduction.Rare(a) ? 1f : 0f)) / sum;
        return Mathf.Clamp(Mathf.RoundToInt(AnimalMath.Clamp01(worth) * 15f), 0, 15);
    }

    /// <summary>Genetic merit 0-1: the mean of its production quality and rate, efficiency, resistance, fertility and longevity genes, less half its inbreeding.</summary>
    public static float Merit(AnimalGenes g)
    {
        if (g == null) return 0.5f;
        float m = (g.productionQuality + g.productionRate + g.efficiency + g.resistance + g.fertility + g.longevity) / 6f;
        return AnimalMath.Clamp01(m - 0.5f * AnimalMath.Clamp01(g.inbreeding));
    }

    /// <summary>What a crated animal is worth against its crate's price (a young one of its breed): its stage's worth x its grade's.</summary>
    public static float ValueFactor(GrowthStage stage, int grade, AnimalRules rules)
    {
        AnimalTradeRules t = RulesOf(rules);
        float g = 1f + Mathf.Clamp(t.gradeSpread, 0f, 0.9f) * (Mathf.Clamp(grade, 0, 15) / 15f - 0.5f) * 2f;
        return Mathf.Max(0f, t.StageWorth(stage)) * Mathf.Max(0f, g);
    }

    /// <summary>A grade in words.</summary>
    public static string GradeWord(int grade) =>
        grade >= 13 ? "exceptional" : grade >= 10 ? "fine" : grade >= 7 ? "sound" : grade >= 4 ? "plain" : "poor";

    /// <summary>Why an animal cannot be crated now (null: it can).</summary>
    public static string WhyNotCrate(AnimalRecord a, AnimalSpeciesData sp, AnimalHerd herd, IAnimalCatalog cat, AnimalRules rules)
    {
        AnimalTradeRules t = RulesOf(rules);
        if (!t.allowCrating) return "Animals are not crated here.";
        if (a == null || !a.Alive) return "It is dead.";
        string name = AnimalSim.NameOf(a, sp);
        if (AnimalBreeding.Expecting(a)) return $"{name} is expecting: she does not travel now.";
        if (a.health < t.crateMinHealth) return $"{name} is too weak to travel.";
        if (herd != null)
            foreach (AnimalRecord y in herd.animals)
                if (y != null && y.Alive && y.mother == a.id && AnimalBreeding.Suckles(y, cat?.Species(y.species)))
                    return $"{name} is nursing her young: she is not parted from them.";
        return null;
    }
}
