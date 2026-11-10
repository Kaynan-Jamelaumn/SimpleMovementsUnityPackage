using System;
using System.Collections.Generic;
using UnityEngine;

// ==================================================================== enums
/// <summary>Why an animal is or is not making a product now (saved and sent as numbers: add at the END).</summary>
public enum AnimalProductStatus
{
    None,
    /// <summary>It is making it.</summary>
    Producing,
    /// <summary>What waits fills its storage (a nest, an udder, a full fleece): it makes no more until collected.</summary>
    Full,
    /// <summary>Not at this stage of life or not of this sex (a pullet, a ram's milk): it does not make it at all.</summary>
    Resting,
    /// <summary>Too poorly fed over the days to make it.</summary>
    Underfed,
    /// <summary>Too ill or weak.</summary>
    Ill,
    /// <summary>Starving or parched.</summary>
    Starving,
    /// <summary>At the boarding stable (it makes nothing there).</summary>
    Boarded,
    /// <summary>Production is off in the rules.</summary>
    Off,
}

/// <summary>The quality tiers of products (FR-07): saved and sent as numbers.</summary>
public enum AnimalProductTier
{
    None,
    Poor,
    Common,
    Good,
    Excellent,
    Premium,
    /// <summary>Premium from an animal carrying a rare trait (see <see cref="AnimalProductionRules.specialFrom"/>).</summary>
    Special,
}

/// <summary>What happened in an animal's production history (saved as numbers: add at the END).</summary>
public enum AnimalProductEntryKind
{
    Made,
    Collected,
}

// ==================================================================== data
/// <summary>
/// A product a species gives while it lives (eggs, milk, wool...), as data: how long one lot takes a well kept animal,
/// how much a lot holds, how much can wait to be collected, what collecting it needs, how it keeps, and what it is worth.
/// Its item is "animal:product/&lt;id&gt;" (the id is unique among the game's products); a new species declares its own.
/// </summary>
[Serializable]
public class AnimalProductSpec
{
    [Tooltip("Unique among the game's products: its item is \"animal:product/<id>\".")]
    public string id = "";
    [Tooltip("\"Egg\".")]
    public string name = "";
    [Tooltip("\"Eggs\".")]
    public string plural = "";
    [Tooltip("What one unit is called when it is not the name: \"jug\" (of milk), \"fleece\" (of wool). Empty: the name.")]
    public string unit = "";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Only females give it (eggs, milk).")]
    public bool femaleOnly;
    [Tooltip("Game hours a well kept animal takes to make one lot (how well it lives speeds or slows it).")]
    [Min(0.5f)] public float cycleHours = 24f;
    [Tooltip("Units in one lot.")]
    [Min(1)] public int perCycle = 1;
    [Tooltip("How much a lot holds follows how well the animal lived through it (milk, wool); off: always Per Cycle (an egg is an egg).")]
    public bool yieldScales;
    [Tooltip("Units that can wait to be collected (a nest, an udder, a fleece): once full it makes no more until they are.")]
    [Min(1)] public int capacity = 6;
    [Tooltip("The tool tags collecting it needs, any one of them (\"milking\": a milking pail; \"shears\"; empty: by hand).")]
    public List<string> tools = new List<string>();
    [Tooltip("The verb of collecting it: \"Collect\", \"Milk\", \"Shear\".")]
    public string verb = "Collect";
    [Tooltip("Game hours it keeps fresh while it waits to be collected (0: it keeps); after that it loses Stale Loss Per Day of its quality.")]
    [Min(0f)] public float freshHours;
    [Range(0f, 1f)] public float staleLossPerDay = 0.15f;
    [Tooltip("How much the animal's cleanliness through the cycle counts in its quality (a dirty fleece is worth less).")]
    [Range(0f, 1f)] public float hygieneWeight = 0.25f;
    [Tooltip("What one unit of the best quality is worth (its item's price; a copy is worth less by its quality).")]
    [Min(0)] public int value = 3;
    [Tooltip("Words other systems may read to know it (\"egg\", \"dairy\", \"fiber\").")]
    public List<string> tags = new List<string>();
    [Tooltip("Stress collecting it gives a skittish animal (milking, shearing: x 1 - temperament).")]
    [Min(0f)] public float handlingStress;
    [Tooltip("Players eat or drink it (eggs, milk: a food item, a fine copy sating more); off: a material (wool).")]
    public bool edible;
    [Tooltip("Satiety a copy of the best quality gives a player who eats it (a poor one half); Drink: it quenches thirst too.")]
    [Min(0f)] public float nutrition = 8f;
    public bool drink;
    [Tooltip("The Alchemy ingredient (a preset's name or id) a copy brews as: \"Egg Yolk\", \"Goat Milk\" (empty: none).")]
    public string alchemyIngredient = "";
    [Tooltip("Phase 5: it rolls out of the nest into a pen's egg trays as it is laid (eggs), so the nest never fills while the trays have room.")]
    public bool trays;

    public string Item => AnimalIds.Product(id);
    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;
    public bool NeedsTool => tools != null && tools.Count > 0;
    public string Verb => string.IsNullOrWhiteSpace(verb) ? "Collect" : verb;

    /// <summary>"1 egg", "2 eggs", "1 jug of milk", "3 bundles of wool".</summary>
    public string Count(int n)
    {
        string what = DisplayName.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(unit)) return $"{n} {unit}{(n == 1 ? "" : "s")} of {what}";
        string many = string.IsNullOrWhiteSpace(plural) ? what + "s" : plural.ToLowerInvariant();
        return $"{n} {(n == 1 ? what : many)}";
    }

    /// <summary>Its problems (errors stop it from being made; warnings do not).</summary>
    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (!AnimalIds.ValidKey(id)) errors?.Add($"{owner}: a product has an invalid id '{id}' (no spaces, slashes or colons).");
        if (perCycle > capacity) warnings?.Add($"{owner}: product '{id}' makes {perCycle} a lot but only {capacity} can wait: it would never make one.");
        if (cycleHours < 0.5f) errors?.Add($"{owner}: product '{id}' has a cycle under half an hour.");
    }
}

/// <summary>One lot made and waiting to be collected (or, in the ledger, collected): how much, how good, and how its animal lived meanwhile.</summary>
[Serializable]
public class AnimalProductLot
{
    public int units;
    [Range(0f, 1f)] public float quality;
    [Tooltip("Its production cycle (the animal's count of lots of this product, from 1).")]
    public int cycle;
    public double cycleStart, madeAt;
    [Tooltip("Means through the cycle (0-100): nutrition, welfare, health, environment (comfort and a clean floor), hygiene.")]
    public float nutrition, welfare, health, environment, hygiene;
}

/// <summary>An animal's making of one product: the cycle under way (and what it has lived through so far), and what waits to be collected.</summary>
[Serializable]
public class AnimalProductState
{
    public string product = "";
    [Tooltip("How far the current cycle is (0-1).")]
    public float progress;
    [Tooltip("Lots made so far (the next lot's cycle is this + 1).")]
    public int cycle;
    public double cycleStart = -1.0, lastMadeAt = -1.0, lastCollectedAt = -1.0;
    public AnimalProductStatus status;
    [Tooltip("The cycle's weighted sums so far (hours, rate, and each of the means of a lot).")]
    public float hours, rate, nutrition, welfare, health, environment, hygiene, ownerCare;
    [Tooltip("Lots made and waiting to be collected (oldest first).")]
    public List<AnimalProductLot> waiting = new List<AnimalProductLot>();

    public int Waiting
    {
        get
        {
            int n = 0;
            if (waiting != null)
                foreach (AnimalProductLot l in waiting)
                    if (l != null) n += Math.Max(0, l.units);
            return n;
        }
    }
}

/// <summary>One entry of an animal's production history.</summary>
[Serializable]
public class AnimalProductEntry
{
    public double at;
    public string product = "";
    public AnimalProductEntryKind kind;
    public int units;
    [Range(0f, 1f)] public float quality;
    public int cycle;
    [Tooltip("Who collected it (a lasting key; empty: made).")]
    public string by = "";
}

/// <summary>
/// A lot that left its animal (collected): where every copy of it came from. The per-copy code of its items names it by
/// <see cref="serial"/> (see <see cref="AnimalItemCode.Product"/>), so a cook, a crafter or a trader can ask where an egg
/// came from (see <see cref="AnimalProductLedger.Find"/>).
/// </summary>
[Serializable]
public class AnimalProductRecord
{
    public long serial;
    public string product = "";
    public string animal = "", animalName = "", species = "", breed = "", owner = "";
    public int cycle;
    public double cycleStart, madeAt, collectedAt;
    [Tooltip("Who collected it (a lasting key).")]
    public string by = "";
    public int units;
    [Range(0f, 1f)] public float quality;
    public AnimalProductTier tier;
    [Tooltip("Means through its cycle (0-100).")]
    public float nutrition, welfare, health, environment, hygiene;
}

/// <summary>
/// The world's product ledger (saved with the animals): a never reused serial for every lot collected, and its origin.
/// Old entries are let go (see <see cref="AnimalProductionRules.keepLedgerDays"/>): an item older than that still keeps its
/// quality, only where it came from is forgotten.
/// </summary>
[Serializable]
public class AnimalProductLedger
{
    [Tooltip("The last serial given (the next lot gets the one after it).")]
    public long serial;
    public List<AnimalProductRecord> entries = new List<AnimalProductRecord>();

    [NonSerialized] private Dictionary<long, AnimalProductRecord> bySerial;

    /// <summary>A new serial (1 to <see cref="AnimalItemCode.MaxProductSerial"/>, then round again: by then the first are long forgotten).</summary>
    public long NextSerial()
    {
        serial = serial >= AnimalItemCode.MaxProductSerial || serial < 0 ? 1 : serial + 1;
        return serial;
    }

    public void Add(AnimalProductRecord r)
    {
        if (r == null) return;
        entries ??= new List<AnimalProductRecord>();
        Index();
        if (bySerial.TryGetValue(r.serial, out AnimalProductRecord old)) entries.Remove(old);
        entries.Add(r);
        bySerial[r.serial] = r;
    }

    /// <summary>The lot of a serial (and of this product, when one is named: a serial of another product is not its origin), or null.</summary>
    public AnimalProductRecord Find(long s, string product = null)
    {
        if (s <= 0 || entries == null) return null;
        Index();
        if (!bySerial.TryGetValue(s, out AnimalProductRecord r)) return null;
        return string.IsNullOrEmpty(product) || string.Equals(r.product, product, StringComparison.OrdinalIgnoreCase) ? r : null;
    }

    /// <summary>Lets go of entries collected more than the rules' Keep Ledger Days ago, and the oldest past Max Ledger.</summary>
    public void Prune(double now, AnimalProductionRules rules)
    {
        if (entries == null || rules == null) return;
        double before = now - Math.Max(1.0, rules.keepLedgerDays) * 24.0;
        int removed = entries.RemoveAll(e => e == null || e.collectedAt < before);
        int over = entries.Count - Math.Max(100, rules.maxLedger);
        if (over > 0)
        {
            entries.RemoveRange(0, over);
            removed += over;
        }
        if (removed > 0) bySerial = null;
    }

    private void Index()
    {
        if (bySerial != null) return;
        bySerial = new Dictionary<long, AnimalProductRecord>();
        if (entries == null) return;
        foreach (AnimalProductRecord r in entries)
            if (r != null) bySerial[r.serial] = r;
    }

    /// <summary>Forget the index (after the entries were replaced from a save).</summary>
    public void Reindex() => bySerial = null;
}

/// <summary>
/// How products are made and judged (FR-06, FR-07). Quality follows the configurable formula
/// Q = nutrition x 0.30 + welfare x 0.25 + genetics x 0.20 + health x 0.15 + environment x 0.10 (normalised), each judged
/// over the product's own cycle - never at the moment it is collected - within the limits, and is told in six tiers.
/// </summary>
[Serializable]
public class AnimalProductionRules
{
    [Tooltip("Animals give products (off: none make any).")]
    public bool enabled = true;

    [Header("Quality: Q = weights x (nutrition, welfare, genetics, health, environment), over the cycle")]
    [Range(0f, 1f)] public float nutritionWeight = 0.30f;
    [Range(0f, 1f)] public float welfareWeight = 0.25f;
    [Range(0f, 1f)] public float geneticsWeight = 0.20f;
    [Range(0f, 1f)] public float healthWeight = 0.15f;
    [Range(0f, 1f)] public float environmentWeight = 0.10f;
    [Tooltip("The lowest and highest quality a product can have.")]
    [Range(0f, 1f)] public float qualityMin = 0.05f;
    [Range(0f, 1f)] public float qualityMax = 1f;
    [Tooltip("Quality added when its owner cared for it every day of the cycle (less, for fewer days).")]
    [Range(0f, 0.3f)] public float ownerCareBonus = 0.04f;
    [Tooltip("Quality of what a senior animal gives (added) and how much it gives (x).")]
    [Range(-0.5f, 0.5f)] public float seniorQuality = -0.05f;
    [Range(0f, 1f)] public float seniorRate = 0.6f;

    [Header("Tiers (from these qualities)")]
    [Range(0f, 1f)] public float commonFrom = 0.35f;
    [Range(0f, 1f)] public float goodFrom = 0.52f;
    [Range(0f, 1f)] public float excellentFrom = 0.68f;
    [Range(0f, 1f)] public float premiumFrom = 0.82f;
    [Tooltip("Special: at least this quality, from an animal that carries a rare trait (else it is Premium).")]
    [Range(0f, 1f)] public float specialFrom = 0.92f;

    [Header("When it makes nothing")]
    [Tooltip("Below this health an animal makes nothing.")]
    [Range(0f, 100f)] public float minHealth = 35f;
    [Tooltip("Below this nutritional condition an animal makes nothing.")]
    [Range(0f, 100f)] public float minNutrition = 30f;
    [Tooltip("How far its production-rate gene moves how fast it makes things (0.25: from x0.75 to x1.25).")]
    [Range(0f, 1f)] public float geneSpread = 0.25f;
    [Tooltip("Comfort from which it makes things at full pace (below: down to x0.7 at 20).")]
    [Range(0f, 100f)] public float comfortFull = 60f;

    [Header("Collecting")]
    [Tooltip("One who may care for others' animals in a pen (a farm hand) may collect what they made, into their own bag (its owner is told). Off: only its owner collects.")]
    public bool othersCollect = true;

    [Header("History and ledger")]
    [Tooltip("Entries of production history kept per animal.")]
    [Range(4, 200)] public int keepProductLog = 24;
    [Tooltip("Game days the ledger remembers where a collected lot came from.")]
    [Range(1f, 10000f)] public float keepLedgerDays = 120f;
    [Range(100, 1000000)] public int maxLedger = 20000;

    /// <summary>Its problems.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (nutritionWeight + welfareWeight + geneticsWeight + healthWeight + environmentWeight <= 0f) errors?.Add("Production: every quality weight is 0.");
        if (qualityMin > qualityMax) errors?.Add("Production: Quality Min is above Quality Max.");
        if (!(commonFrom <= goodFrom && goodFrom <= excellentFrom && excellentFrom <= premiumFrom && premiumFrom <= specialFrom))
            warnings?.Add("Production: the tiers' qualities are not in order (Common <= Good <= Excellent <= Premium <= Special).");
    }
}

// ==================================================================== rules
/// <summary>
/// The making of products (FR-06, FR-07, FR-14), pure C# and deterministic: worked out in each step of an animal's life
/// (see <see cref="AnimalSim"/>), so a week nobody watched makes what a watched week would.
/// <para><b>How fast</b>: an animal makes a product only in a productive stage of its life (its species' stages), of the
/// right sex, fed (not starving or parched, its nutrition at least Min Nutrition), well enough (health at least Min Health),
/// in a pen (not boarded), with room for what it makes. It makes it at a rate that is a product of how it lives - its welfare
/// band, its illnesses, its production-rate gene, its age, its nutrition and its comfort - so no single thing done well
/// is enough. While its owner is away it lives slower, and makes slower too.</para>
/// <para><b>How good</b>: each lot is judged by the means of its own cycle (nutrition, welfare, health, environment,
/// hygiene, the owner's care) and the animal's genes, never by the moment it is collected: a cow fed its favourite food just
/// before milking does not give premium milk.</para>
/// </summary>
public static class AnimalProduction
{
    /// <summary>Its state for a product (made when <paramref name="create"/>), or null.</summary>
    public static AnimalProductState State(AnimalRecord a, string product, bool create)
    {
        if (a == null || string.IsNullOrEmpty(product)) return null;
        a.products ??= new List<AnimalProductState>();
        foreach (AnimalProductState s in a.products)
            if (s != null && string.Equals(s.product, product, StringComparison.OrdinalIgnoreCase)) return s;
        if (!create) return null;
        var st = new AnimalProductState { product = product };
        a.products.Add(st);
        return st;
    }

    /// <summary>Does it make a product at all at this point of its life (its stage, its sex)?</summary>
    public static bool Makes(AnimalRecord a, AnimalSpeciesData sp, AnimalProductSpec p)
    {
        if (a == null || sp == null || p == null || !a.Alive) return false;
        AnimalStageSpec stage = sp.StageOrNearest(a.stage);
        if (stage == null || !stage.productionEnabled) return false;
        return !p.femaleOnly || a.sex == AnimalSex.Female;
    }

    /// <summary>
    /// One step of an animal's production (see <see cref="AnimalSim"/>): for each product of its species, the cycle under way
    /// takes in how it lived the step, goes on at its rate, and makes a lot when it is complete - a step that completes one
    /// cycle and goes into the next shares its hours between them, so a long step makes what short ones would; what waits
    /// past its freshness loses quality. A full store pauses the cycle: nothing is made and none of those hours count in it
    /// (<paramref name="filled"/> gets each product whose store a lot filled this step). Returns nothing: the record changes.
    /// </summary>
    public static void Step(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, AnimalRules rules, AnimalWelfareBandEffects band, in AnimalStageEffects fx,
        float dt, float pace, double from, double to, float worst, List<AnimalProductSpec> filled = null)
    {
        if (a == null || sp?.products == null || sp.products.Count == 0 || dt <= 0f) return;
        AnimalProductionRules r = rules?.production ?? new AnimalProductionRules();
        foreach (AnimalProductSpec p in sp.products)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            bool makes = Makes(a, sp, p);
            AnimalProductState st = State(a, p.id, makes);
            if (st == null) continue;
            Age(st, p, r, dt, to);
            if (!r.enabled)
            {
                st.status = AnimalProductStatus.Off;
                continue;
            }
            if (!makes)
            {
                st.status = AnimalProductStatus.Resting;
                Reset(st, to);
                continue;
            }
            if (f.boarded)
            {
                st.status = AnimalProductStatus.Boarded;
                continue;
            }
            if (st.cycleStart < 0.0) st.cycleStart = from;
            st.status = Gate(a, p, r, st);
            // A full store: the cycle waits as it is, until what waits is collected.
            if (st.status == AnimalProductStatus.Full) continue;

            // The cycle takes in how it lived this step (the bad hours too: a cycle is judged whole), and goes on only
            // while it is producing.
            float w = dt * Mathf.Max(0.05f, pace);
            bool producing = st.status == AnimalProductStatus.Producing;
            float rate = producing ? Rate(a, sp, r, band, in fx) : 0f;
            // Phase 5: its breed's yield of it and its pen's level (none in disrepair: see AnimalUpgrades) speed its cycles;
            // how much a lot holds still follows how it lives (the rate below), so neither counts twice.
            float pace5 = BreedYield(sp.BreedOrDefault(a.breed), p) * (1f + Mathf.Max(0f, f.productionBonus));
            // Progress per unit of the step's weight.
            float perWeight = producing ? pace * rate * pace5 / Mathf.Max(0.5f, p.cycleHours) * (dt / w) : 0f;
            var lived = new Lived
            {
                nutrition = a.nutrition,
                welfare = a.welfare,
                health = Mathf.Max(0f, a.health - 0.5f * worst),
                environment = 0.5f * (a.comfort + 100f - f.dirt),
                hygiene = a.hygiene,
                ownerCare = a.lastOwnerCareAt >= 0.0 && to - a.lastOwnerCareAt <= 24.0,
                rate = rate,
            };
            float left = w;
            double length = to - from;
            while (left > 0f)
            {
                // The weight of the step that completes the cycle (none: it does not complete in this step).
                float need = perWeight > 0f ? Mathf.Max(0f, 1f - st.progress) / perWeight : float.PositiveInfinity;
                bool completes = need <= left;
                float take = completes ? need : left;
                Absorb(st, in lived, take);
                left -= take;
                if (!completes)
                {
                    st.progress += take * perWeight;
                    break;
                }
                // Complete: the lot (as much as there is room for), made at the point of the step where its cycle ended.
                double madeAt = from + length * Math.Min(1.0, (w - left) / w);
                AnimalProductLot lot = Lot(a, p, r, st, madeAt);
                lot.units = Math.Min(lot.units, Math.Max(1, p.capacity) - st.Waiting);
                st.waiting.Add(lot);
                st.cycle = lot.cycle;
                st.lastMadeAt = madeAt;
                st.progress = 0f;
                Log(a, new AnimalProductEntry { at = madeAt, product = p.id, kind = AnimalProductEntryKind.Made, units = lot.units, quality = lot.quality, cycle = lot.cycle }, r);
                Reset(st, madeAt);
                if (st.Waiting >= Mathf.Max(1, p.capacity))
                {
                    // Its store is full: the rest of the step is not counted in the next cycle.
                    st.status = AnimalProductStatus.Full;
                    filled?.Add(p);
                    break;
                }
            }
        }
    }

    /// <summary>How the animal lived through a step, as a cycle takes it in.</summary>
    private struct Lived
    {
        public float nutrition, welfare, health, environment, hygiene, rate;
        public bool ownerCare;
    }

    /// <summary>A share of a step taken into the cycle under way (its weight: hours x pace).</summary>
    private static void Absorb(AnimalProductState st, in Lived l, float weight)
    {
        if (weight <= 0f) return;
        st.hours += weight;
        st.rate += l.rate * weight;
        st.nutrition += l.nutrition * weight;
        st.welfare += l.welfare * weight;
        st.health += l.health * weight;
        st.environment += l.environment * weight;
        st.hygiene += l.hygiene * weight;
        if (l.ownerCare) st.ownerCare += weight;
    }

    /// <summary>Why it does not make it now (Producing: it does). A full store first: it waits for that whatever else.</summary>
    private static AnimalProductStatus Gate(AnimalRecord a, AnimalProductSpec p, AnimalProductionRules r, AnimalProductState st)
    {
        if (st.Waiting >= Mathf.Max(1, p.capacity)) return AnimalProductStatus.Full;
        if (a.satiety <= 0f || a.hydration <= 0f) return AnimalProductStatus.Starving;
        if (a.health < r.minHealth) return AnimalProductStatus.Ill;
        if (a.nutrition < r.minNutrition) return AnimalProductStatus.Underfed;
        return AnimalProductStatus.Producing;
    }

    /// <summary>
    /// A breed's yield of a product (Phase 5, its specialisation): its Product Yield, for its Specialty only when it names
    /// one (a Leghorn lays more, a Jersey milks more); 1 for every other product and breed.
    /// </summary>
    public static float BreedYield(AnimalBreedData breed, AnimalProductSpec p)
    {
        if (breed == null || p == null) return 1f;
        if (!string.IsNullOrWhiteSpace(breed.specialty) && !string.Equals(breed.specialty.Trim(), p.id, StringComparison.OrdinalIgnoreCase)) return 1f;
        return Mathf.Clamp(breed.productYield <= 0f ? 1f : breed.productYield, 0.25f, 3f);
    }

    /// <summary>
    /// How fast it makes things now (1: a well kept, typical animal): its welfare band x its illnesses x its production-rate
    /// gene x its age x its nutrition (0.5 at Min Nutrition, 1 from Well Nourished Above) x its comfort (0.7 at 20, 1 from
    /// Comfort Full).
    /// </summary>
    public static float Rate(AnimalRecord a, AnimalSpeciesData sp, AnimalProductionRules r, AnimalWelfareBandEffects band, in AnimalStageEffects fx)
    {
        if (a == null) return 0f;
        r ??= new AnimalProductionRules();
        float gene = AnimalGenes.Scale(a.genes != null ? a.genes.productionRate : 0.5f, r.geneSpread);
        float age = a.stage == GrowthStage.Senior ? r.seniorRate : 1f;
        float fed = 0.5f + 0.5f * AnimalMath.Smooth(r.minNutrition, Mathf.Max(r.minNutrition + 1f, 75f), a.nutrition);
        float comfy = 0.7f + 0.3f * AnimalMath.Smooth(20f, Mathf.Max(21f, r.comfortFull), a.comfort);
        float b = band != null ? Mathf.Max(0f, band.production) : 1f;
        return Mathf.Max(0f, b * Mathf.Max(0f, fx.production) * gene * age * fed * comfy);
    }

    /// <summary>The lot a complete cycle makes: its units (by the cycle's mean rate where the yield scales) and its quality from the cycle's means.</summary>
    private static AnimalProductLot Lot(AnimalRecord a, AnimalProductSpec p, AnimalProductionRules r, AnimalProductState st, double now)
    {
        float h = Mathf.Max(0.0001f, st.hours);
        var lot = new AnimalProductLot
        {
            cycle = st.cycle + 1,
            cycleStart = st.cycleStart >= 0.0 ? st.cycleStart : now,
            madeAt = now,
            nutrition = st.nutrition / h,
            welfare = st.welfare / h,
            health = st.health / h,
            environment = st.environment / h,
            hygiene = st.hygiene / h,
        };
        int units = Mathf.Max(1, p.perCycle);
        if (p.yieldScales) units = Mathf.Max(1, Mathf.RoundToInt(units * Mathf.Clamp(st.rate / h, 0.3f, 1.3f)));
        lot.units = Mathf.Min(units, Mathf.Max(1, p.capacity));
        lot.quality = Quality(lot, a, p, r, st.ownerCare / h);
        return lot;
    }

    /// <summary>
    /// The quality of a lot (0-1) from its cycle's means: the rules' weighted formula of nutrition, welfare, genetics (the
    /// animal's production-quality gene), health and environment, plus the owner's care (the share of the cycle they had
    /// cared for it within a day), less a senior's, scaled by the animal's hygiene as much as the product's Hygiene Weight,
    /// within the rules' limits.
    /// </summary>
    public static float Quality(AnimalProductLot means, AnimalRecord a, AnimalProductSpec p, AnimalProductionRules r, float ownerCareShare)
    {
        if (means == null) return 0f;
        r ??= new AnimalProductionRules();
        float wN = Mathf.Max(0f, r.nutritionWeight), wW = Mathf.Max(0f, r.welfareWeight), wG = Mathf.Max(0f, r.geneticsWeight);
        float wH = Mathf.Max(0f, r.healthWeight), wE = Mathf.Max(0f, r.environmentWeight);
        float sum = wN + wW + wG + wH + wE;
        float genetics = a?.genes != null ? AnimalMath.Clamp01(a.genes.productionQuality) : 0.5f;
        float q = sum <= 0f ? 0.5f
            : (wN * means.nutrition / 100f + wW * means.welfare / 100f + wG * genetics + wH * means.health / 100f + wE * means.environment / 100f) / sum;
        q += r.ownerCareBonus * AnimalMath.Clamp01(ownerCareShare);
        if (a != null && a.stage == GrowthStage.Senior) q += r.seniorQuality;
        float hw = p != null ? AnimalMath.Clamp01(p.hygieneWeight) : 0f;
        q *= 1f - hw + hw * (0.55f + 0.45f * AnimalMath.Clamp01(means.hygiene / 100f));
        return Mathf.Clamp(q, Mathf.Min(r.qualityMin, r.qualityMax), Mathf.Max(r.qualityMin, r.qualityMax));
    }

    /// <summary>The tier of a quality (Special only from an animal with a rare trait; <paramref name="rare"/> false: at most Premium).</summary>
    public static AnimalProductTier Tier(float q, bool rare, AnimalProductionRules r)
    {
        r ??= new AnimalProductionRules();
        if (q >= r.specialFrom && rare) return AnimalProductTier.Special;
        if (q >= r.premiumFrom) return AnimalProductTier.Premium;
        if (q >= r.excellentFrom) return AnimalProductTier.Excellent;
        if (q >= r.goodFrom) return AnimalProductTier.Good;
        if (q >= r.commonFrom) return AnimalProductTier.Common;
        return AnimalProductTier.Poor;
    }

    public static string TierWord(AnimalProductTier t) => t == AnimalProductTier.None ? "" : t.ToString();

    /// <summary>Does it carry a rare trait (what makes a Special product)?</summary>
    public static bool Rare(AnimalRecord a) => a?.genes != null && a.genes.rare != 0;

    /// <summary>
    /// What waits past its freshness loses its quality a little every hour (down to the rules' least): of a step ending at
    /// <paramref name="now"/>, only its hours past the lot's freshness count, so long steps age it as short ones do.
    /// </summary>
    private static void Age(AnimalProductState st, AnimalProductSpec p, AnimalProductionRules r, float dt, double now)
    {
        if (st.waiting == null || p.freshHours <= 0f || p.staleLossPerDay <= 0f) return;
        foreach (AnimalProductLot l in st.waiting) AgeLot(l, p, r, dt, now);
    }

    /// <summary>
    /// A lot waiting over a step of <paramref name="dt"/> hours to <paramref name="now"/>: past its Fresh Hours it loses
    /// Stale Loss Per Day of its quality (pro rata), never below the rules' Quality Min (in a nest or a pen's egg trays alike).
    /// </summary>
    public static void AgeLot(AnimalProductLot l, AnimalProductSpec p, AnimalProductionRules r, float dt, double now)
    {
        if (l == null || p == null || p.freshHours <= 0f || p.staleLossPerDay <= 0f) return;
        r ??= new AnimalProductionRules();
        double stale = now - l.madeAt - p.freshHours;
        if (stale <= 0.0) return;
        float hours = Mathf.Min(dt, (float)stale);
        l.quality = Mathf.Max(Mathf.Min(r.qualityMin, l.quality), l.quality - p.staleLossPerDay / 24f * hours);
    }

    /// <summary>
    /// Debug and tests: every cycle under way completes now, from how the animal lived through it so far (one it has not
    /// lived any of yet, from how it is now, its pen floor as clean as <paramref name="dirt"/> says), and its lot waits - as
    /// much as there is room for. Only for what it makes at this point of its life. Returns the lots made.
    /// </summary>
    public static int Ripen(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now, float dirt = 0f)
    {
        if (a == null || !a.Alive || sp?.products == null) return 0;
        AnimalProductionRules r = rules?.production ?? new AnimalProductionRules();
        int made = 0;
        foreach (AnimalProductSpec p in sp.products)
        {
            if (p == null || string.IsNullOrEmpty(p.id) || !Makes(a, sp, p)) continue;
            AnimalProductState st = State(a, p.id, true);
            if (st.hours <= 0f)
            {
                st.hours = 1f;
                st.rate = 1f;
                st.nutrition = a.nutrition;
                st.welfare = a.welfare;
                st.health = a.health;
                st.environment = 0.5f * (a.comfort + 100f - AnimalMath.Clamp(dirt, 0f, 100f));
                st.hygiene = a.hygiene;
                st.ownerCare = a.lastOwnerCareAt >= 0.0 && now - a.lastOwnerCareAt <= 24.0 ? 1f : 0f;
            }
            if (st.Waiting >= Mathf.Max(1, p.capacity))
            {
                st.status = AnimalProductStatus.Full;
                continue;
            }
            if (st.cycleStart < 0.0) st.cycleStart = now;
            AnimalProductLot lot = Lot(a, p, r, st, now);
            lot.units = Math.Min(lot.units, Math.Max(1, p.capacity) - st.Waiting);
            st.waiting.Add(lot);
            st.cycle = lot.cycle;
            st.lastMadeAt = now;
            st.progress = 0f;
            Log(a, new AnimalProductEntry { at = now, product = p.id, kind = AnimalProductEntryKind.Made, units = lot.units, quality = lot.quality, cycle = lot.cycle }, r);
            Reset(st, now);
            st.status = st.Waiting >= Mathf.Max(1, p.capacity) ? AnimalProductStatus.Full : AnimalProductStatus.Producing;
            made++;
        }
        if (made > 0) a.revision++;
        return made;
    }

    private static void Reset(AnimalProductState st, double now)
    {
        st.hours = st.rate = st.nutrition = st.welfare = st.health = st.environment = st.hygiene = st.ownerCare = 0f;
        st.cycleStart = now;
    }

    public static void Log(AnimalRecord a, AnimalProductEntry e, AnimalProductionRules r)
    {
        if (a == null || e == null) return;
        a.productLog ??= new List<AnimalProductEntry>();
        a.productLog.Add(e);
        int keep = Math.Max(1, r != null ? r.keepProductLog : 24);
        if (a.productLog.Count > keep) a.productLog.RemoveRange(0, a.productLog.Count - keep);
    }

    // ---------------------------------------------------------------- collecting
    /// <summary>
    /// Takes every lot waiting of a product from an animal (the commit of a Collect request): each is written in the ledger
    /// under a new serial with where it came from, and its units become item copies whose codes carry its quality (to the
    /// hundredth: the ledger keeps it so too, so its tier is the same wherever it is read), whether its animal has a rare
    /// trait, and its serial. Returns the units taken (0: nothing waited); <paramref name="codes"/> gets one code per unit.
    /// </summary>
    public static int Collect(AnimalRecord a, AnimalSpeciesData sp, AnimalProductSpec p, AnimalProductLedger ledger, AnimalProductionRules r, string by, double now,
        List<int> codes, out float meanQuality, out AnimalProductTier bestTier)
    {
        meanQuality = 0f;
        bestTier = AnimalProductTier.None;
        AnimalProductState st = State(a, p?.id, false);
        if (st?.waiting == null || st.waiting.Count == 0) return 0;
        int total = 0;
        float qSum = 0f;
        bool rare = Rare(a);
        foreach (AnimalProductLot lot in st.waiting)
        {
            if (lot == null || lot.units <= 0) continue;
            float q = AnimalItemCode.Hundredth(lot.quality);
            AnimalProductTier tier = Tier(q, rare, r);
            long serial = ledger != null ? ledger.NextSerial() : 0L;
            ledger?.Add(new AnimalProductRecord
            {
                serial = serial, product = p.id, animal = a.id, animalName = a.name ?? "", species = a.species, breed = a.breed, owner = a.owner,
                cycle = lot.cycle, cycleStart = lot.cycleStart, madeAt = lot.madeAt, collectedAt = now, by = by ?? "", units = lot.units,
                quality = q, tier = tier, nutrition = lot.nutrition, welfare = lot.welfare, health = lot.health, environment = lot.environment,
                hygiene = lot.hygiene,
            });
            int code = AnimalItemCode.Product(q, serial, rare);
            for (int i = 0; i < lot.units; i++) codes?.Add(code);
            total += lot.units;
            qSum += q * lot.units;
            if (tier > bestTier) bestTier = tier;
            Log(a, new AnimalProductEntry { at = now, product = p.id, kind = AnimalProductEntryKind.Collected, units = lot.units, quality = q, cycle = lot.cycle, by = by ?? "" }, r);
        }
        st.waiting.Clear();
        st.lastCollectedAt = now;
        if (st.status == AnimalProductStatus.Full)
        {
            // Its store was full, its cycle paused: the next one starts now.
            st.status = AnimalProductStatus.Producing;
            if (st.hours <= 0f) st.cycleStart = now;
        }
        meanQuality = total > 0 ? qSum / total : 0f;
        return total;
    }

    /// <summary>A product's spec on a species, by id (null: it gives none such).</summary>
    public static AnimalProductSpec Spec(AnimalSpeciesData sp, string product)
    {
        if (sp?.products == null || string.IsNullOrEmpty(product)) return null;
        foreach (AnimalProductSpec p in sp.products)
            if (p != null && string.Equals(p.id, product, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    // ---------------------------------------------------------------- words
    /// <summary>Why it is not making it, in words ("She is too poorly fed to lay."); empty while it makes it.</summary>
    public static string StatusText(AnimalProductStatus s, AnimalProductSpec p)
    {
        string what = p != null ? p.DisplayName.ToLowerInvariant() : "anything";
        switch (s)
        {
            case AnimalProductStatus.Full: return p != null && p.capacity <= p.perCycle ? "ready" : "full: collect it";
            case AnimalProductStatus.Resting: return $"gives no {what} now";
            case AnimalProductStatus.Underfed: return "too poorly fed to give any";
            case AnimalProductStatus.Ill: return "too weak to give any";
            case AnimalProductStatus.Starving: return "starving: it gives nothing";
            case AnimalProductStatus.Boarded: return "gives nothing at the boarding stable";
            case AnimalProductStatus.Off: return "gives nothing: production is off";
            default: return "";
        }
    }

    /// <summary>A lot's conditions in a word, for its origin ("well fed", "poorly fed").</summary>
    public static string ConditionWord(float v) => v >= 80f ? "excellent" : v >= 65f ? "good" : v >= 45f ? "fair" : v >= 25f ? "poor" : "very poor";
}
