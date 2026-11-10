using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the rules know of a pen from the scene: its kind, size and stations (the <c>AnimalPen</c> component makes one).</summary>
[Serializable]
public class AnimalPenSpec
{
    public string id = "";
    public string name = "Pen";
    public AnimalHabitatKind kind = AnimalHabitatKind.Pasture;
    [Tooltip("Square metres inside its fence.")]
    [Min(0f)] public float area = 24f;
    [Tooltip("Portions its feeder holds.")]
    [Min(0f)] public float feederCapacity = 120f;
    [Tooltip("Litres its water trough holds.")]
    [Min(0f)] public float waterCapacity = 80f;
    [Tooltip("Square metres of open water surface (rain falls in, the sun dries it).")]
    [Min(0f)] public float troughSurface = 0.6f;
    [Tooltip("Most animals it takes (0 = from its area and each species' space).")]
    [Min(0)] public int capacityOverride;
    [Tooltip("Portions of fresh grass a square metre of it grows per game day in good growing weather (a pasture: 1; a coop or barn floor: 0).")]
    [Min(0f)] public float grassGrowth;
    [Tooltip("Most portions of standing grass a square metre holds.")]
    [Min(0f)] public float grassCapacity = 3f;
    [Tooltip("The food its grass is (grazers eat it before the feeder when they like it as much).")]
    public string grassFood = "fresh_grass";

    [Tooltip("Litres a spring, a pipe or a stream brings into its trough per game hour (0 = only rain and buckets).")]
    [Min(0f)] public float refillLitresPerHour;

    [Header("Shelter and bedding")]
    [Tooltip("Roof and walls, 0-1 (-1: from its kind).")]
    public float shelter = -1f;
    [Tooltip("°C its shelter adds in the cold, at full shelter (-1: from its kind).")]
    public float warmth = -1f;
    [Tooltip("Shade in the heat, 0-1 (-1: from its kind).")]
    public float shade = -1f;
    [Tooltip("It has a bedded floor: -1 from its kind, 0 no, 1 yes.")]
    public int bedding = -1;
    [Tooltip("Game hours in which its ground clears itself of dung (a pasture), 0: never (-1: from its kind).")]
    public float selfCleanHours = -1f;

    /// <summary>The most standing grass it holds (none in a pen that grows none: a coop or barn floor is not a lawn).</summary>
    public float GrassMax => grassGrowth > 0f ? Mathf.Max(0f, grassCapacity) * Mathf.Max(0f, area) : 0f;

    /// <summary>Roof and walls, 0-1 (the scene's value, or its kind's).</summary>
    public float Shelter => shelter >= 0f ? Mathf.Clamp01(shelter) : KindShelter(kind);
    /// <summary>°C its shelter adds in the cold, at full shelter.</summary>
    public float Warmth => warmth >= 0f ? warmth : KindWarmth(kind);
    /// <summary>Shade in the heat, 0-1 (a roof shades as it shelters).</summary>
    public float Shade => shade >= 0f ? Mathf.Clamp01(shade) : KindShelter(kind);
    /// <summary>It has a bedded floor (straw is laid in it; its animals miss it when it is spent).</summary>
    public bool UsesBedding => bedding >= 0 ? bedding > 0 : KindBedded(kind);
    /// <summary>Game hours in which its ground clears itself of dung (0: never; open ground does, at the rules' pace).</summary>
    public float SelfCleanHours(AnimalRules r) => selfCleanHours >= 0f ? selfCleanHours : KindOpen(kind) ? (r ?? AnimalRules.Defaults()).pastureCleanHours : 0f;
    /// <summary>Open to the weather (rain and mud reach its animals).</summary>
    public bool Open => Shelter < 0.5f;

    // A pen's kind decides its shelter when the scene does not (a pen of several kinds takes its best sheltered one).
    private static readonly AnimalHabitatKind[] Kinds =
    {
        AnimalHabitatKind.Pasture, AnimalHabitatKind.Coop, AnimalHabitatKind.Barn, AnimalHabitatKind.Stable, AnimalHabitatKind.Sty,
        AnimalHabitatKind.Hutch, AnimalHabitatKind.Pond, AnimalHabitatKind.Hive, AnimalHabitatKind.Magical,
    };

    private static float ShelterOf(AnimalHabitatKind k)
    {
        switch (k)
        {
            case AnimalHabitatKind.Coop: return 0.9f;
            case AnimalHabitatKind.Barn: return 1f;
            case AnimalHabitatKind.Stable: return 1f;
            case AnimalHabitatKind.Sty: return 0.7f;
            case AnimalHabitatKind.Hutch: return 0.8f;
            case AnimalHabitatKind.Hive: return 1f;
            case AnimalHabitatKind.Magical: return 1f;
            default: return 0f; // pasture, pond: open
        }
    }

    private static float WarmthOf(AnimalHabitatKind k)
    {
        switch (k)
        {
            case AnimalHabitatKind.Coop: return 4f;
            case AnimalHabitatKind.Barn: return 5f;
            case AnimalHabitatKind.Stable: return 5f;
            case AnimalHabitatKind.Sty: return 3f;
            case AnimalHabitatKind.Hutch: return 3f;
            case AnimalHabitatKind.Hive: return 2f;
            case AnimalHabitatKind.Magical: return 5f;
            default: return 0f;
        }
    }

    private static bool BeddedOf(AnimalHabitatKind k) =>
        k == AnimalHabitatKind.Coop || k == AnimalHabitatKind.Barn || k == AnimalHabitatKind.Stable || k == AnimalHabitatKind.Sty ||
        k == AnimalHabitatKind.Hutch || k == AnimalHabitatKind.Magical;

    /// <summary>The kind of habitat (of those it is) that shelters best: the one its defaults come from.</summary>
    public static AnimalHabitatKind MainKind(AnimalHabitatKind kind)
    {
        AnimalHabitatKind best = AnimalHabitatKind.None;
        float bestShelter = -1f;
        foreach (AnimalHabitatKind k in Kinds)
        {
            if ((kind & k) == 0) continue;
            float s = ShelterOf(k);
            if (s > bestShelter) { best = k; bestShelter = s; }
        }
        return best;
    }

    /// <summary>A kind's roof and walls: pasture and pond 0, sty 0.7, hutch 0.8, coop 0.9, barn, stable, hive and magical 1.</summary>
    public static float KindShelter(AnimalHabitatKind kind) => ShelterOf(MainKind(kind));
    /// <summary>A kind's warmth in the cold: barn, stable and magical 5, coop 4, sty and hutch 3, hive 2, open kinds 0.</summary>
    public static float KindWarmth(AnimalHabitatKind kind) => WarmthOf(MainKind(kind));
    /// <summary>A kind is bedded: coop, barn, stable, sty, hutch and magical.</summary>
    public static bool KindBedded(AnimalHabitatKind kind) => BeddedOf(MainKind(kind));
    /// <summary>A kind's ground clears itself of dung: pasture and pond (open ground).</summary>
    public static bool KindOpen(AnimalHabitatKind kind)
    {
        AnimalHabitatKind k = MainKind(kind);
        return k == AnimalHabitatKind.Pasture || k == AnimalHabitatKind.Pond;
    }
}

/// <summary>A pen and the animals that live in it: the unit the simulation advances (in lockstep, so they share its feeder and water truthfully).</summary>
public sealed class AnimalHerd
{
    public AnimalPenRecord pen;
    public AnimalPenSpec spec;
    public readonly List<AnimalRecord> animals = new List<AnimalRecord>();
}

/// <summary>
/// What the rules have of the game's content: the species and the foods, and (Phase 2) the illnesses, the medicines and
/// the care tools (the <c>AnimalDatabase</c> is one; tests make their own: see <see cref="AnimalCatalog"/>).
/// </summary>
public interface IAnimalCatalog
{
    AnimalRules Rules { get; }
    AnimalSpeciesData Species(string id);
    AnimalFoodData Food(string id);
    /// <summary>The food an item is (by item id), or null.</summary>
    AnimalFoodData FoodOfItem(string itemId);
    IReadOnlyList<AnimalSpeciesData> AllSpecies { get; }
    IReadOnlyList<AnimalFoodData> AllFoods { get; }

    /// <summary>An illness by id, or null.</summary>
    AnimalConditionData Condition(string id);
    /// <summary>Every illness, in a fixed order (the simulation works them in it).</summary>
    IReadOnlyList<AnimalConditionData> AllConditions { get; }
    /// <summary>A medicine by id, or null.</summary>
    AnimalTreatmentData Treatment(string id);
    /// <summary>The medicine an item is (by its key: "potion:antidote", "ingredient:charcoal"), or null.</summary>
    AnimalTreatmentData TreatmentOfItem(string itemKey);
    IReadOnlyList<AnimalTreatmentData> AllTreatments { get; }
    /// <summary>A care tool or supply by id, or null.</summary>
    AnimalToolData Tool(string id);
    /// <summary>The tool or supply an item is (by its key: "animal:tool/soft_brush", "agri:tool/shovel"), or null.</summary>
    AnimalToolData ToolOfItem(string itemKey);
    IReadOnlyList<AnimalToolData> AllTools { get; }
}

/// <summary>Something the world should hear about (the owner is told, bodies follow): raised by the simulation and the commands.</summary>
public struct AnimalNotice
{
    public string animal;
    public string owner;
    public string pen;
    public AnimalEventKind kind;
    public string text;
}

/// <summary>The surroundings of a simulation run: the content, the weather, who is online, and where notices go.</summary>
public sealed class AnimalSimContext
{
    public IAnimalCatalog catalog;
    /// <summary>The weather of any hour (the city's climate: the same hours give the same weather, so catch-ups are truthful).</summary>
    public IAgriClimateSource climate;
    /// <summary>Is the owner (a lasting key) in the world at this game hour? Null: always (single player).</summary>
    public Func<string, double, bool> ownerPresent;
    /// <summary>Things that happened, for the world to act on (null: dropped).</summary>
    public List<AnimalNotice> notices;
    /// <summary>Animal-steps worked out (the director's budget counts them).</summary>
    public int steps;
    /// <summary>
    /// A veterinarian is in the world (the director sets it every frame; tests set it). Without one, keeper medicine can
    /// cure every illness (see <see cref="AnimalHealth.Gated"/>) and nobody is told an animal needs a vet.
    /// </summary>
    public bool vetAvailable;
    /// <summary>
    /// The world's registry, where young are born into (Phase 4: a birth takes fresh ids from its serial). The book sets it
    /// when it brings pens and boarders up to date; without one, young due wait to be born until there is one.
    /// </summary>
    public AnimalBook book;
    /// <summary>
    /// The real hour now (Phase 5: a crate's wait is counted in real days; the director sets it; 0: unknown, a crate's wait
    /// starts when the world first counts it).
    /// </summary>
    public double realHours;

    public AnimalRules Rules => catalog?.Rules ?? AnimalRules.Defaults();

    public bool Present(string owner, double t) => ownerPresent == null || string.IsNullOrEmpty(owner) || ownerPresent(owner, t);

    public void Notice(AnimalRecord a, AnimalEventKind kind, string text)
    {
        notices?.Add(new AnimalNotice { animal = a.id, owner = a.owner, pen = a.pen, kind = kind, text = text });
    }
}

/// <summary>
/// The life of animals, hour by hour - pure C#, deterministic (no randomness: the same start and the same weather give
/// the same result on every machine). An animal grows from what it lives through: hunger and thirst grow by the hour
/// (faster for a thirsty animal in the heat), it eats from its pen's feeder what it likes best of what is there (harmful
/// food only when hungry, toxic food only when starving), drinks from the trough (rain fills it, the sun dries it), and
/// its <b>nutritional condition</b> is judged over days of what it actually took in against what it needed - so a full
/// animal can still be badly fed, and one good meal does not make up for a week of poor ones. Starving, thirst, poor
/// nutrition and toxins wear its health; good keeping heals it; health and nutrition decide how fast it grows; old age
/// takes it in the end. While its owner is logged out its needs grow more slowly and hardship cannot take its health
/// below a floor (see <see cref="AnimalRules"/>).
/// <para>Its pen gets dirty as it is lived in (droppings soil the floor and spend the bedding, open ground clears itself,
/// the trough fouls, a stocked pasture gathers worm eggs: see <see cref="AnimalHabitat"/>), and its animals live by it:
/// a coat settles toward what the pen allows, hooves wear, comfort follows the weather, the shelter, crowding, the floor,
/// the bedding, hooves and coat (see <see cref="AnimalCare"/>); what an animal lives through builds up illnesses that take
/// hold, worsen, are treated and mend (see <see cref="AnimalHealth"/>); illness harms it like hardship and keeps it from
/// healing; its welfare (see <see cref="AnimalWelfare"/>) decides how it lives the next step; its life record keeps how
/// it lived, for its remains.</para>
/// <para>Time moves in steps of at most <see cref="AnimalRules.stepHours"/>, so a week nobody watched is lived as a week
/// of meals, thirst and harm, not as one big step. A pen's animals live each step together: what their pen decides for
/// them (its dirt, how full it is, the illness its animals give off) is taken from the state at the start of the step,
/// so the order they are stepped in does not matter.</para>
/// </summary>
public static class AnimalSim
{
    private const double Epsilon = 1e-7;
    /// <summary>However ill, an animal eats until it is this full (see <see cref="FullAt"/>).</summary>
    private const float SickFullFloor = 20f;
    private static readonly List<AnimalRecord> order = new List<AnimalRecord>();
    private static readonly AgriWeather Mild = new AgriWeather { temperature = 15f, humidity = 0.6f, clouds = 0.4f, light = 0.5f, day = true };

    // Scratch, never state (filled again for every step and every animal; nothing here is saved or outlives its step):
    // the dictionaries a step's facts point to, the causes one animal lives through, and what its illnesses did.
    private static readonly Dictionary<string, float> stepContagion = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> stepCompanions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> boardedCompanions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly float[] causes = new float[AnimalHealth.CauseCount];
    private static readonly AnimalHealthOutcome outcome = new AnimalHealthOutcome();
    private static readonly List<AnimalProductSpec> filled = new List<AnimalProductSpec>();
    private static readonly HashSet<string> stepHere = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> stepNursing = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> stepMilk = new Dictionary<string, float>(StringComparer.Ordinal);
    private static readonly List<AnimalRecord> due = new List<AnimalRecord>();

    // ---------------------------------------------------------------- herds and boarders
    /// <summary>
    /// Brings a herd up to <paramref name="to"/> in steps; false when <paramref name="outOfTime"/> stopped it first (it goes
    /// on from there next time: every step is saved in the records as it is made). Each step: the trough and the grass,
    /// then the facts its animals live by (from the step-start state: see <see cref="AnimalHabitat.Facts"/>), then each
    /// animal, then the pen itself (its floor, bedding, trough and ground: see <see cref="AnimalHabitat.StepHabitat"/>).
    /// </summary>
    public static bool AdvanceHerd(AnimalHerd herd, double to, AnimalSimContext ctx, Func<bool> outOfTime = null)
    {
        if (herd?.pen == null || ctx?.catalog == null) return true;
        AnimalRules rules = ctx.Rules;
        AnimalPenRecord pen = herd.pen;
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        if (pen.livedTo <= 0.0 || pen.livedTo > to + 1e-3)
        {
            // A new pen starts now; a pen "ahead" of the clock (a world whose clock was reset) is taken at its word.
            if (pen.livedTo <= 0.0) pen.livedTo = to;
            return true;
        }
        double stepHours = Math.Max(0.1, rules.stepHours);
        while (pen.livedTo < to - Epsilon)
        {
            if (outOfTime != null && outOfTime()) return false;
            double start = pen.livedTo;
            double end = Math.Min(to, start + stepHours);
            double dt = end - start;
            AgriWeather w = ctx.climate != null ? ctx.climate.WeatherAt(start + dt * 0.5) : Mild;
            // Phase 5: its machines at work (none in disrepair, judged from the step-start state): the hopper drops feed into
            // the feeder before the animals eat, the waterer tops up the trough.
            bool hopperWorks = AnimalUpgrades.Working(pen, spec, rules, AnimalDevice.Hopper);
            bool traysWork = AnimalUpgrades.Working(pen, spec, rules, AnimalDevice.Trays);
            float dirtFactor = AnimalUpgrades.DirtFactor(pen, spec, rules);
            if (hopperWorks) AnimalUpgrades.Dispense(pen, spec, rules, start);
            StepWater(pen, spec, w, dt, AnimalUpgrades.WatererInflow(pen, spec, rules, dt));
            StepGrass(pen, spec, w, dt);

            // What the pen decides for its animals this step, from the step-start state (so the order they are stepped in
            // does not matter): its dirt, bedding, water and worms, its shelter, how full it is, its company, and the
            // illness its animals (and catching remains) give off.
            AnimalStepFacts f = AnimalHabitat.Facts(herd, spec, ctx.catalog, rules, end, stepContagion, stepCompanions, stepHere, stepNursing, stepMilk);

            // The hungriest eat first (deterministic: then by id), so a short feeder is shared the same way every time.
            order.Clear();
            foreach (AnimalRecord a in herd.animals)
                if (a != null && a.livedTo < end - Epsilon) order.Add(a);
            order.Sort((x, y) =>
            {
                int c = x.satiety.CompareTo(y.satiety);
                return c != 0 ? c : string.CompareOrdinal(x.id, y.id);
            });
            float soiling = 0f;
            foreach (AnimalRecord a in order)
            {
                double from = Math.Max(start, a.livedTo);
                if (!a.Alive)
                {
                    a.livedTo = end;
                    continue;
                }
                // What it soils the pen with, for the share of the step it lived in it.
                soiling += StepAnimal(a, ctx, pen, spec, w, in f, from, end) * (float)((end - from) / dt);
            }
            // The pen after the step: what its animals and any remains left on the floor and the bedding, its trough, its ground.
            AnimalHabitat.StepHabitat(pen, spec, rules, in w, dt, soiling * dirtFactor / Mathf.Max(1f, spec.area), f.deadSpace);
            // Phase 5: what waits in the egg trays ages; then what was laid this step rolls into them; the upkeep falls due.
            AnimalUpgrades.AgeTrays(pen, ctx.catalog, rules, (float)dt, end);
            if (traysWork) AnimalUpgrades.RollToTrays(herd, ctx.catalog, rules, end);
            AnimalUpgrades.Accrue(pen, spec, rules, dt);
            // Who it lived the step with (its relationships), then the young due by its end, born into the pen.
            AnimalSocial.StepHerd(herd, ctx.catalog, rules, (float)dt, end);
            Births(herd.animals, ctx, end);
            pen.livedTo = end;
            pen.revision++;
        }
        return true;
    }

    private static readonly HashSet<string> stable = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Its owner's other living animals at the boarding stable (its company there), or null without a book.</summary>
    private static HashSet<string> StableCompany(AnimalRecord a, AnimalSimContext ctx)
    {
        if (ctx?.book == null) return null;
        stable.Clear();
        foreach (AnimalRecord o in ctx.book.Boarded)
            if (o != null && o != a && o.Alive && o.owner == a.owner) stable.Add(o.id);
        return stable;
    }

    /// <summary>The young of every mother among these whose time has come by <paramref name="end"/> (in id order).</summary>
    private static void Births(List<AnimalRecord> animals, AnimalSimContext ctx, double end)
    {
        if (ctx?.book == null || animals == null) return;
        due.Clear();
        foreach (AnimalRecord m in animals)
            if (m != null && m.Alive && AnimalBreeding.Expecting(m) && m.pregnancy.dueAt <= end + Epsilon) due.Add(m);
        if (due.Count == 0) return;
        due.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
        // Copied: the young are added to the very list read.
        var mothers = new List<AnimalRecord>(due);
        foreach (AnimalRecord m in mothers) AnimalBreeding.Deliver(ctx.book, m, ctx.catalog.Species(m.species), ctx, end);
    }

    /// <summary>
    /// Brings a boarded animal (no pen: the boarding stable feeds and waters it, keeps it clean and trims its hooves) up to
    /// <paramref name="to"/>. The stable's facts are the same every step (see <see cref="AnimalHabitat.BoardedFacts"/>).
    /// </summary>
    public static bool AdvanceAlone(AnimalRecord a, double to, AnimalSimContext ctx, Func<bool> outOfTime = null)
    {
        if (a == null || ctx?.catalog == null) return true;
        if (!a.Alive || a.livedTo <= 0.0)
        {
            if (a.livedTo <= 0.0 || !a.Alive) a.livedTo = Math.Max(a.livedTo, to);
            return true;
        }
        double stepHours = Math.Max(0.1, ctx.Rules.stepHours);
        AnimalStepFacts f = AnimalHabitat.BoardedFacts(ctx.catalog.Species(a.species), boardedCompanions);
        while (a.livedTo < to - Epsilon)
        {
            if (outOfTime != null && outOfTime()) return false;
            double start = a.livedTo, end = Math.Min(to, start + stepHours);
            AgriWeather w = ctx.climate != null ? ctx.climate.WeatherAt(start + (end - start) * 0.5) : Mild;
            StepAnimal(a, ctx, null, null, w, in f, start, end);
            if (!a.Alive)
            {
                // It died: its remains are not lived on.
                a.livedTo = Math.Max(a.livedTo, to);
                return true;
            }
            AnimalSocial.StepAlone(a, ctx.Rules, (float)(end - start), end, StableCompany(a, ctx));
            if (ctx.book != null && AnimalBreeding.Expecting(a) && a.pregnancy.dueAt <= end + Epsilon)
                AnimalBreeding.Deliver(ctx.book, a, ctx.catalog.Species(a.species), ctx, end);
        }
        return true;
    }

    /// <summary>
    /// Rain into the trough, the sun out of it, and the kind of water it becomes (rain mixed in). Clean water coming in (rain,
    /// a spring) mixes the trough's dirt down while it fills and washes it out while it overflows, whatever the step length
    /// (see <see cref="AnimalHabitat.DiluteTrough"/>); a dry trough is clean.
    /// </summary>
    private static void StepWater(AnimalPenRecord pen, AnimalPenSpec spec, in AgriWeather w, double dt, float waterer = 0f)
    {
        float surface = Mathf.Max(0f, spec.troughSurface);
        float inflow = Mathf.Max(0f, spec.refillLitresPerHour) * (float)dt + Mathf.Max(0f, waterer);
        if (surface <= 0f && inflow <= 0f)
        {
            if (pen.water <= 0.0001f) pen.waterDirt = 0f;
            return;
        }
        // 1 mm over 1 m² is a litre.
        float rain = Mathf.Max(0f, w.rain) * surface * (float)dt;
        float dry = AgriEnvironment.Evaporation(w) * surface * (float)dt;
        float before = pen.water;
        float capacity = Mathf.Max(0f, spec.waterCapacity);
        pen.water = Mathf.Clamp(pen.water + rain + inflow - dry, 0f, capacity);
        AnimalHabitat.DiluteTrough(pen, before, rain + inflow, capacity);
        // An automatic waterer's well water becomes the trough's kind once it brings more an hour than was there (judged by
        // the rate, so short steps and long ones agree), or once it runs through a full trough (fresh water flowing over).
        if (waterer > 0f && (waterer / (float)Math.Max(1e-6, dt) > before || pen.water >= capacity - 0.01f)) pen.waterKind = (int)WaterSourceKind.Well;
        // The kind of water follows what fills it (judged by the rate, so short steps and long ones agree).
        if (inflow <= 0f && w.Raining && surface > 0f && (before <= 0.05f || w.rain * surface > before)) pen.waterKind = (int)WaterSourceKind.Rain;
    }

    /// <summary>
    /// A pasture's grass grows back (by the day, in the warmth: nothing below about 3 °C, best from 10 to 26 °C, slowing in
    /// great heat and in a dry spell) up to what its ground holds.
    /// </summary>
    private static void StepGrass(AnimalPenRecord pen, AnimalPenSpec spec, in AgriWeather w, double dt)
    {
        if (spec.grassGrowth <= 0f || spec.area <= 0f) return;
        float warmth = AnimalMath.Smooth(3f, 10f, w.temperature) * (1f - AnimalMath.Smooth(28f, 38f, w.temperature));
        float moisture = 0.55f + 0.45f * AnimalMath.Clamp01(w.humidity + (w.Raining ? 0.3f : 0f));
        float grow = spec.grassGrowth * spec.area * warmth * moisture * (float)dt / 24f;
        pen.grass = Mathf.Clamp(pen.grass + grow, 0f, spec.GrassMax);
    }

    // ---------------------------------------------------------------- one animal, one step
    /// <summary>
    /// One animal over one step (<paramref name="from"/> to <paramref name="to"/>; <paramref name="pen"/> null: the
    /// boarding stable). <paramref name="f"/> is what its pen decides this step, from the step-start state. In order: its
    /// welfare band (from its last step) and its illnesses' effects as the step begins; hunger and thirst; eating (a sick
    /// animal is full sooner) and drinking; nutrition; toxins and upsets; its coat, hooves and comfort; its illnesses; harm
    /// and healing (illness harms as hardship does, an animal seriously ill does not heal, its band speeds or slows
    /// healing); growth (x its band's and its illnesses'); mood (its comfort, crowding, company, illnesses and band count);
    /// affinity fading without its owner's care; welfare; its life record; the day's record; what is worth telling; death
    /// (of the largest harm: an illness's when it was). Returns what it soiled its pen with per game hour.
    /// </summary>
    private static float StepAnimal(AnimalRecord a, AnimalSimContext ctx, AnimalPenRecord pen, AnimalPenSpec spec, in AgriWeather w, in AnimalStepFacts f,
        double from, double to)
    {
        double dtd = to - from;
        if (dtd <= Epsilon) return 0f;
        if (!a.Alive)
        {
            // The remains of the dead are not lived on.
            a.livedTo = Math.Max(a.livedTo, to);
            return 0f;
        }
        float dt = (float)dtd;
        ctx.steps++;
        AnimalRules rules = ctx.Rules;
        IAnimalCatalog cat = ctx.catalog;
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null)
        {
            // Its species left the game's content: it is kept as it is (never lost), and lives on when it comes back.
            a.livedTo = to;
            return 0f;
        }
        AnimalBreedData br = sp.BreedOrDefault(a.breed);
        AnimalNeedsSpec needs = sp.needs ?? new AnimalNeedsSpec();
        AnimalStageSpec stage = sp.StageOrNearest(a.stage);
        // A mother expecting, or nursing young, eats and needs more.
        float appetite = Appetite(sp, br, a) * AnimalBreeding.AppetiteFactor(a, rules, f.NursingOf(a.id));
        bool present = ctx.Present(a.owner, from + dtd * 0.5);
        float pace = present ? 1f : rules.awayNeedsRate;
        bool boarded = pen == null;
        float healthBefore = a.health, satietyBefore = a.satiety, hydrationBefore = a.hydration, toxinBefore = a.toxin;
        Prime(a, sp, rules, appetite);
        // A record that never had its welfare and life record worked out (a Phase 1 save) starts them from where it is.
        if (a.life == null || !a.life.primed) AnimalWelfare.Prime(a, rules);

        // The welfare of its last step drives this one; its illnesses act by the stages they are in as the step begins.
        AnimalWelfareBandEffects band = AnimalWelfare.EffectsOf(a, rules);
        AnimalStageEffects fx = AnimalHealth.Effects(a, cat);

        // Hunger and thirst grow (a thirsty animal in the heat dries faster).
        float heat = Mathf.Max(0f, w.temperature - needs.heatAbove) * needs.heatThirst;
        a.satiety -= needs.hungerPerHour * pace * dt;
        a.hydration -= needs.thirstPerHour * (1f + heat) * pace * dt;

        // It eats and drinks when it wants to (illness spoils its appetite: it is full sooner).
        float full = FullAt(rules, fx.appetite);
        float eaten = 0f, favourites = 0f, bad = 0f;
        if (a.satiety < needs.eatBelow)
        {
            if (boarded) BoardingMeal(a, sp, needs, rules, appetite, full, ref eaten);
            // A young with its mother in its pen suckles (as well nourished as her milk is).
            else if (f.milk != null && AnimalBreeding.Suckles(a, sp) && f.milk.TryGetValue(a.mother, out float milk)) Nurse(a, sp, needs, rules, appetite, full, milk, ref eaten);
            else EatFromFeeder(a, sp, ctx, pen, spec, appetite, full, to, ref eaten, ref favourites, ref bad);
        }
        if (a.hydration < needs.drinkBelow)
        {
            if (boarded) a.hydration = 100f;
            else Drink(a, needs, pen, appetite);
        }
        a.satiety = AnimalMath.Clamp100(a.satiety);
        a.hydration = AnimalMath.Clamp100(a.hydration);

        // What it needed meanwhile, and its nutritional condition over the window.
        float fade = AnimalMath.Decay(dtd, rules.nutritionWindowHours);
        a.intake = a.intake * fade;
        a.need = a.need * fade + Need(sp, stage, appetite) * (pace * dt);
        a.nutrition = Nutrition(a.intake, a.need, needs.importance, rules);

        // Toxins clear and upsets settle.
        a.toxin *= (float)Math.Pow(1.0 - AnimalMath.Clamp01(needs.toxinClearance), dtd);
        a.upset *= (float)Math.Pow(1.0 - AnimalMath.Clamp01(rules.upsetRecovery), dtd);
        if (a.toxin < 0.01f) a.toxin = 0f;
        if (a.upset < 0.01f) a.upset = 0f;

        // Its coat settles toward what its pen allows, its hooves wear, its comfort follows its surroundings.
        AnimalCare.StepHygiene(a, sp, in f, in w, rules, dt, pace, in fx);
        AnimalCare.StepHooves(a, sp, in f, rules, dt, pace);
        AnimalCare.StepComfort(a, sp, in f, in w, rules, dt, in fx);

        // Its illnesses: what it lives through builds them up; they take hold, worsen, are treated and mend.
        AnimalHealth.Causes(a, sp, cat, rules, in f, in w, causes);
        AnimalHealth.Step(a, sp, ctx, in f, causes, from, to, pace, band, outcome);
        // Its worst illness now (with health off in the rules, illnesses it had do nothing: they do not stop it healing).
        float worst = rules.healthEnabled ? outcome.worst : 0f;

        // Harm, and the healing of a well kept animal (illness harms as hardship does; one seriously ill does not heal).
        float hardiness = Hardiness(a, sp);
        float tolerance = needs.toxinTolerance * hardiness;
        float starve = a.satiety <= 0f ? needs.starvationDamage : 0f;
        float parched = a.hydration <= 0f ? needs.dehydrationDamage : 0f;
        float malnourished = a.nutrition < rules.malnourishedBelow
            ? needs.malnutritionDamage * (rules.malnourishedBelow - a.nutrition) / Mathf.Max(1f, rules.malnourishedBelow) : 0f;
        float poisoned = a.toxin > tolerance ? needs.toxinDamage * (a.toxin - tolerance) : 0f;
        float ill = Mathf.Max(0f, fx.healthPerHour);
        float hardship = (starve + parched + malnourished + poisoned + ill) * dt / hardiness;
        float aged = OldAge(a, sp, to) * dt;
        float health = a.health - hardship;
        if (!present && rules.awayHealthFloor > 0f) health = Mathf.Max(health, Mathf.Min(a.health, rules.awayHealthFloor));
        // What hardship really took (the floor holds it back while its owner is away): old age can be the larger harm.
        float hardshipTaken = a.health - health;
        health -= aged;
        bool canHeal = a.satiety > 20f && a.hydration > 20f && a.toxin <= tolerance && a.upset < 50f && starve + parched + poisoned <= 0f
                       && worst < rules.illnessHealBlockAbove;
        if (canHeal && a.nutrition >= rules.malnourishedBelow)
        {
            float k = AnimalMath.Smooth(rules.malnourishedBelow, rules.wellNourishedAbove, a.nutrition);
            float cap = a.stage == GrowthStage.Senior ? 80f : 100f;
            if (health < cap) health = Mathf.Min(cap, health + needs.recoveryPerHour * k * Mathf.Max(0f, band.recovery) * dt);
        }
        a.health = AnimalMath.Clamp100(health);
        // A mother in poor health loses her young.
        AnimalBreeding.Miscarry(a, sp, rules, to, ctx);

        // Growth follows how it lives (age does not: a starved calf is still a year old); its band and its illnesses count.
        Grow(a, sp, br, rules, dt, Mathf.Max(0f, band.growth) * Mathf.Max(0f, fx.growth), ctx, to);

        // Mood: hunger, thirst, upsets and toxins weigh; favourites lift it; so do its comfort, company, illnesses and band.
        AnimalCare.MoodTargets(a, sp, in f, rules, band, in fx, out float happinessAdd, out float stressAdd);
        // Its company: close companions near lift and calm it; one just parted from it misses; a fierce rival stresses it.
        AnimalSocial.MoodTargets(a, sp, rules, boarded ? StableCompany(a, ctx) : f.here, to, ref happinessAdd, ref stressAdd, ctx);
        Mood(a, rules, dt, favourites, bad, boarded, happinessAdd, stressAdd);

        // Its bond with its owner fades when its owner has neither cared for it nor spent time near it in a while.
        double lastOwner = Math.Max(a.lastOwnerCareAt, a.lastOwnerNearAt);
        if (lastOwner < 0.0 || to - lastOwner > rules.affinityNeglectHours)
            a.affinity = Mathf.Max(0f, a.affinity - Mathf.Max(0f, rules.affinityFadePerDay) / 24f * dt);

        // Its welfare now (its band drives its next step); its owner is told when it is in a bad way (not as it dies), and
        // only while they are here to hear it: a warning raised while they are away would hold back the next one, so the
        // first step after they come back tells them.
        double lastHelp = a.lastNeedsHelpAt;
        bool needsHelp = false;
        if (a.health > 0f && present) needsHelp = AnimalWelfare.Update(a, rules, to);
        else a.welfare = AnimalWelfare.Compute(a, rules.welfare);

        // How it lives over the long run (its remains are judged by it), and the day's care history.
        AnimalWelfare.StepLife(a, rules, dtd, worst);
        Record(a, rules, from, dt, eaten, favourites, bad, present, worst);

        // What it gives (eggs, milk, wool): its cycles take in how it lived this step, and go on at the pace it lives.
        filled.Clear();
        AnimalProduction.Step(a, sp, in f, rules, band, in fx, dt, pace, from, to, worst, filled);
        foreach (AnimalProductSpec p in filled)
            ctx.Notice(a, AnimalEventKind.ProductsReady, $"{NameOf(a, sp)}: {p.Count(AnimalProduction.State(a, p.id, false)?.Waiting ?? 0)} to {p.Verb.ToLowerInvariant()}.");

        // Things worth telling.
        if (satietyBefore > 0f && a.satiety <= 0f) Tell(a, ctx, rules, AnimalEventKind.Starving, to, $"{NameOf(a, sp)} is starving.");
        if (hydrationBefore > 0f && a.hydration <= 0f) Tell(a, ctx, rules, AnimalEventKind.Thirsty, to, $"{NameOf(a, sp)} has no water left in it.");
        if (toxinBefore <= tolerance && a.toxin > tolerance) Tell(a, ctx, rules, AnimalEventKind.Poisoned, to, $"{NameOf(a, sp)} is trembling and drooling.");
        if (healthBefore < 60f && a.health >= 60f && a.health > healthBefore) Tell(a, ctx, rules, AnimalEventKind.Recovered, to, $"{NameOf(a, sp)} looks better.");
        for (int i = 0; i < outcome.notices.Count; i++)
        {
            AnimalHealthNotice n = outcome.notices[i];
            Tell(a, ctx, rules, n.kind, n.at, n.text);
        }
        if (needsHelp)
        {
            // Its history keeps the first warning of a spell (none within two warnings' time before it); the daily
            // reminders of a long one go to its owner only, so they do not push the rest of its history out.
            string help = AnimalWelfare.NeedsHelpText(a, sp);
            if (lastHelp >= 0.0 && to - lastHelp <= 2.0 * rules.needsHelpEveryHours) ctx.Notice(a, AnimalEventKind.NeedsHelp, help);
            else Tell(a, ctx, rules, AnimalEventKind.NeedsHelp, to, help);
        }

        // How well it digests from now on (its illnesses as they are after this step): food given by hand meanwhile uses it.
        a.absorbFactor = AnimalHealth.Effects(a, cat).absorption;

        if (a.health <= 0f)
        {
            // It dies of the largest harm: old age, a need (thirst, hunger, poison, poor feeding), or an illness.
            AnimalConditionData killer = null;
            AnimalDeathCause cause;
            if (aged > hardshipTaken) cause = AnimalDeathCause.OldAge;
            else
            {
                float worstNeed = Mathf.Max(Mathf.Max(parched, starve), Mathf.Max(poisoned, malnourished));
                float illness = outcome.Strongest(out string killerId);
                if (illness > worstNeed && !string.IsNullOrEmpty(killerId)) killer = cat.Condition(killerId);
                if (killer != null) cause = killer.deathCause != AnimalDeathCause.None ? killer.deathCause : AnimalDeathCause.Illness;
                else
                    cause = parched >= starve && parched >= poisoned && parched >= malnourished ? AnimalDeathCause.Dehydration
                        : starve >= poisoned && starve >= malnourished ? AnimalDeathCause.Starvation
                        : poisoned >= malnourished ? AnimalDeathCause.Poisoning : AnimalDeathCause.Malnutrition;
            }
            if (killer != null)
            {
                a.deathCondition = killer.id;
                AnimalHealth.LogEvent(a, killer, a.Condition(killer.id), AnimalHealthEventKind.Died, to, "", rules);
            }
            // A death no illness caused ends its health history too ("Died (of thirst)").
            else AnimalHealth.Log(a, AnimalHealthEventKind.Died, to, "", "", $"Died ({Cause(cause)})", "", rules.keepHealthLog);
            Die(a, cause, to, ctx, sp);
        }
        a.livedTo = to;
        a.revision++;
        return AnimalHabitat.Soiling(sp, a, pace);
    }

    /// <summary>How much an animal bears of hardship (its breed's hardiness and its resistance).</summary>
    public static float Hardiness(AnimalRecord a, AnimalSpeciesData sp)
    {
        AnimalBreedData br = sp?.BreedOrDefault(a?.breed);
        return Mathf.Max(0.1f, (br != null ? br.hardiness : 1f) * AnimalGenes.Scale(a?.genes != null ? a.genes.resistance : 0.5f, 0.3f));
    }

    /// <summary>The toxin load an animal bears without harm (the signs of poisoning show above it, as the harm does).</summary>
    public static float Tolerance(AnimalRecord a, AnimalSpeciesData sp) => (sp?.needs != null ? sp.needs.toxinTolerance : 8f) * Hardiness(a, sp);

    /// <summary>How much an animal eats and needs compared to an average adult of an average species (its stage, breed and size).</summary>
    public static float Appetite(AnimalSpeciesData sp, AnimalBreedData br, AnimalRecord a)
    {
        AnimalStageSpec st = sp?.StageOrNearest(a != null ? a.stage : GrowthStage.Adult);
        float stage = st != null ? st.appetite : 1f;
        float breed = br != null ? br.appetite : 1f;
        float size = AnimalGenes.Scale(a?.genes != null ? a.genes.size : 0.5f, 0.15f);
        float scale = sp?.needs != null ? Mathf.Max(0.01f, sp.needs.portionScale) : 1f;
        return Mathf.Max(0.01f, stage * breed * size * scale);
    }

    /// <summary>
    /// The satiety an animal eats up to: the rules' Full At, less while illness spoils its appetite (x
    /// <paramref name="illnessAppetite"/>: <see cref="AnimalStageEffects.appetite"/>), never below 20.
    /// </summary>
    public static float FullAt(AnimalRules rules, float illnessAppetite)
    {
        float fullAt = (rules ?? AnimalRules.Defaults()).fullAt;
        return Mathf.Min(fullAt, Mathf.Max(SickFullFloor, fullAt * AnimalMath.Clamp01(illnessAppetite)));
    }

    /// <summary>What it needs of each nutrient per game hour.</summary>
    public static NutrientProfile Need(AnimalSpeciesData sp, AnimalStageSpec stage, float appetite)
    {
        NutrientProfile n = sp.needs != null ? sp.needs.needPerHour : new NutrientProfile(0.75f, 0.25f, 0.5f, 0.025f, 0.025f);
        if (stage != null)
            for (int i = 0; i < NutrientProfile.Count; i++) n[i] *= Mathf.Max(0f, stage.needScale[i]);
        return n * appetite;
    }

    /// <summary>
    /// The nutritional condition (0-100) from what was taken in against what was needed, nutrient by nutrient: short of a
    /// need scores less and less (half the need scores well under half), too much hurts a little; the nutrients count by
    /// the species' importance. No need at all scores 100.
    /// </summary>
    public static float Nutrition(NutrientProfile intake, NutrientProfile need, NutrientProfile importance, AnimalRules rules)
    {
        float sum = 0f, weight = 0f;
        for (int i = 0; i < NutrientProfile.Count; i++)
        {
            float w = Mathf.Max(0f, importance[i]);
            if (w <= 0f || need[i] <= 1e-6f) continue;
            float b = intake[i] / need[i];
            float score = b <= 1f ? (float)Math.Pow(Mathf.Max(0f, b), 1.3) : 1f - Mathf.Min(0.6f, (b - 1f) * rules.excessPenalty);
            sum += w * score;
            weight += w;
        }
        return weight <= 0f ? 100f : AnimalMath.Clamp100(100f * sum / weight);
    }

    /// <summary>
    /// A new animal's memory of what it ate starts fair (as if it had been fed as its <see cref="AnimalRecord.nutrition"/>
    /// says before it came), not empty; its welfare and its life record start from its state (see
    /// <see cref="AnimalWelfare.Prime"/>). Done when it is registered, and before anything else touches it (set its hygiene,
    /// comfort and hooves first). What is primed already is left as it is (an animal from a Phase 1 save gets only its
    /// welfare and life record).
    /// </summary>
    public static void Prime(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules)
    {
        if (a == null || sp == null) return;
        rules ??= AnimalRules.Defaults();
        if (!a.primed) Prime(a, sp, rules, Appetite(sp, sp.BreedOrDefault(a.breed), a));
        if (a.life == null || !a.life.primed) AnimalWelfare.Prime(a, rules);
    }

    private static void Prime(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, float appetite)
    {
        if (a.primed) return;
        a.primed = true;
        NutrientProfile need = Need(sp, sp.StageOrNearest(a.stage), appetite) * rules.nutritionWindowHours;
        a.need = need;
        float start = Mathf.Clamp(a.nutrition, 0f, 100f) / 100f;
        a.intake = need * (float)Math.Pow(Mathf.Max(0.01f, start), 1.0 / 1.3);
        a.nutrition = Nutrition(a.intake, a.need, sp.needs != null ? sp.needs.importance : NutrientProfile.All(1f), rules);
    }

    // ---------------------------------------------------------------- eating and drinking
    /// <summary>The standing grass of a pasture, as the lot it is to a grazer (one shared object: its portions are written back).</summary>
    private static readonly FeederLot grassLot = new FeederLot();

    /// <summary>
    /// Eats until full (<paramref name="full"/>: less for a sick animal, see <see cref="FullAt"/>) or nothing there it will
    /// take: the best liked first, then the best kept. A pasture's grass counts among what is there (fresh, of fair
    /// quality), so grazers live on it while it lasts and turn to the feeder after.
    /// </summary>
    private static void EatFromFeeder(AnimalRecord a, AnimalSpeciesData sp, AnimalSimContext ctx, AnimalPenRecord pen, AnimalPenSpec spec, float appetite, float full,
        double now, ref float eaten, ref float favourites, ref float bad)
    {
        AnimalRules rules = ctx.Rules;
        AnimalDiet diet = sp.DietAt(a.stage);
        bool grazing = pen.grass > 0.01f && spec != null && !string.IsNullOrEmpty(spec.grassFood);
        if (grazing)
        {
            grassLot.food = spec.grassFood;
            grassLot.portions = pen.grass;
            grassLot.quality = 0.75f;
            grassLot.since = now;
            grassLot.by = "";
        }
        for (int guard = 0; guard < 16 && a.satiety < full; guard++)
        {
            FeederLot best = null;
            AnimalFoodData bestFood = null;
            FoodAffinity bestAffinity = FoodAffinity.Inedible;
            float bestQuality = 0f;
            int count = pen.feeder.Count + (grazing ? 1 : 0);
            for (int li = 0; li < count; li++)
            {
                FeederLot lot = li < pen.feeder.Count ? pen.feeder[li] : grassLot;
                if (lot == null || lot.portions <= 0.0001f) continue;
                AnimalFoodData food = ctx.catalog.Food(lot.food);
                if (food == null || food.satiety <= 0f) continue;
                FoodAffinity aff = diet.AffinityOf(food);
                if (!Takes(aff, a.satiety, rules)) continue;
                float q = food.QualityAfter(lot.quality, now - lot.since);
                // Equally liked: the pasture's own grass first (it grows back), then the best kept.
                bool better = best == null || aff < bestAffinity ||
                              (aff == bestAffinity && (lot == grassLot ? best != grassLot : best != grassLot && q > bestQuality + 1e-4f));
                if (better)
                {
                    best = lot;
                    bestFood = food;
                    bestAffinity = aff;
                    bestQuality = q;
                }
            }
            if (best == null) break;
            float per = bestFood.satiety / appetite;
            // Harmful or toxic food is eaten only as far as the hunger that drove it to it (then better food, or none);
            // nothing past what it can take now.
            float limit = bestAffinity == FoodAffinity.Toxic ? rules.toxicBelow : bestAffinity == FoodAffinity.Harmful ? rules.harmfulBelow : full;
            limit = Mathf.Min(limit, full);
            float want = (limit - a.satiety) / Mathf.Max(0.0001f, per);
            float take = Mathf.Min(best.portions, Mathf.Max(0.05f, want));
            best.portions -= take;
            if (best.portions <= 0.0001f) best.portions = 0f;
            Take(a, sp, rules, bestFood, bestAffinity, take, bestQuality, appetite, out float fav, out float harm);
            eaten += take * bestFood.satiety / appetite;
            favourites += fav;
            bad += harm;
        }
        if (grazing) pen.grass = Mathf.Max(0f, grassLot.portions);
        pen.feeder.RemoveAll(l => l == null || l.portions <= 0.0001f);
    }

    /// <summary>Does an animal with this satiety take a food it holds in this regard (from a feeder)?</summary>
    public static bool Takes(FoodAffinity aff, float satiety, AnimalRules rules)
    {
        switch (aff)
        {
            case FoodAffinity.Inedible: return false;
            case FoodAffinity.Toxic: return satiety < rules.toxicBelow;
            case FoodAffinity.Harmful: return satiety < rules.harmfulBelow;
            default: return satiety < rules.fullAt;
        }
    }

    /// <summary>
    /// The animal takes in <paramref name="portions"/> of a food (from a feeder or a hand): it fills it by its size, feeds
    /// it by the food's nutrients, quality and the regard it holds it in, and by how well it digests now (an upset, its
    /// illnesses: <see cref="AnimalRecord.absorbFactor"/>), and does what else the food does.
    /// </summary>
    public static void Take(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, AnimalFoodData food, FoodAffinity aff, float portions, float quality,
        float appetite, out float favourite, out float harmful)
    {
        favourite = harmful = 0f;
        if (portions <= 0f || food == null) return;
        float q = food.qualityMatters ? AnimalMath.Clamp01(quality) : 1f;
        float absorb = Absorption(aff, rules) * (1f - AnimalMath.Clamp(a.upset, 0f, 100f) / 200f) * AnimalGenes.Scale(a.genes != null ? a.genes.efficiency : 0.5f, 0.15f)
                       * AnimalMath.Clamp01(a.absorbFactor);
        a.intake = a.intake + food.nutrients * (portions * (0.5f + 0.5f * q) * absorb);
        a.satiety = AnimalMath.Clamp100(a.satiety + portions * food.satiety / appetite);
        a.hydration = AnimalMath.Clamp100(a.hydration + portions * food.hydration / appetite);
        float dose = portions * (food.toxin + (aff == FoodAffinity.Toxic ? rules.toxicDose : 0f)) / appetite;
        if (dose > 0f) a.toxin += dose;
        float irritation = portions * (food.irritation + (aff == FoodAffinity.Harmful ? rules.harmfulUpset : 0f) + (aff == FoodAffinity.Toxic ? rules.harmfulUpset * 0.5f : 0f)) / appetite;
        if (irritation > 0f) a.upset = AnimalMath.Clamp100(a.upset + irritation);
        // Counted in satiety points (what the meal was to the animal), so a hen's meal and a cow's weigh the same.
        float points = portions * food.satiety / appetite;
        if (aff == FoodAffinity.Favorite) favourite = points;
        if (aff == FoodAffinity.Harmful || aff == FoodAffinity.Toxic) harmful = points;
    }

    /// <summary>How well a food in this regard is absorbed.</summary>
    public static float Absorption(FoodAffinity aff, AnimalRules rules)
    {
        switch (aff)
        {
            case FoodAffinity.Favorite: return rules.favoriteAbsorption;
            case FoodAffinity.Preferred: return 1f;
            case FoodAffinity.Normal: return 0.92f;
            case FoodAffinity.Neutral: return 0.7f;
            case FoodAffinity.Poor: return rules.poorAbsorption;
            case FoodAffinity.Harmful: return rules.poorAbsorption * 0.8f;
            case FoodAffinity.Toxic: return rules.poorAbsorption * 0.6f;
            default: return 0f;
        }
    }

    /// <summary>Drinks from the trough until it has drunk its fill or the trough is dry.</summary>
    private static void Drink(AnimalRecord a, AnimalNeedsSpec needs, AnimalPenRecord pen, float appetite)
    {
        if (pen.water <= 0f) return;
        float perPoint = Mathf.Max(0.0001f, needs.litresPerPoint) * appetite / Mathf.Max(0.01f, needs.portionScale);
        float wanted = (100f - a.hydration) * perPoint;
        float drunk = Mathf.Min(wanted, pen.water);
        pen.water -= drunk;
        a.hydration = AnimalMath.Clamp100(a.hydration + drunk / perPoint);
    }

    /// <summary>A young suckles from its mother: to full (a sick one less: <paramref name="full"/>), nourished as well as her milk is (0.3-1: her own nutrition).</summary>
    private static void Nurse(AnimalRecord a, AnimalSpeciesData sp, AnimalNeedsSpec needs, AnimalRules rules, float appetite, float full, float milk, ref float eaten)
    {
        float before = a.satiety;
        a.satiety = Mathf.Max(a.satiety, Mathf.Min(rules.fullAt, full));
        float share = (a.satiety - before) / Mathf.Max(0.01f, needs.hungerPerHour);
        a.intake = a.intake + Need(sp, sp.StageOrNearest(a.stage), appetite) * (share * Mathf.Clamp(milk, 0.3f, 1f));
        eaten += a.satiety - before;
    }

    /// <summary>A boarded animal is fed a plain diet: full (a sick one less: <paramref name="full"/>), decently (not finely) nourished.</summary>
    private static void BoardingMeal(AnimalRecord a, AnimalSpeciesData sp, AnimalNeedsSpec needs, AnimalRules rules, float appetite, float full, ref float eaten)
    {
        float before = a.satiety;
        a.satiety = Mathf.Max(a.satiety, Mathf.Min(rules.fullAt * 0.9f, full));
        float share = (a.satiety - before) / Mathf.Max(0.01f, needs.hungerPerHour);
        // What a plain diet gives over the hours this meal covers: the need of those hours, at the stable's value.
        a.intake = a.intake + Need(sp, sp.StageOrNearest(a.stage), appetite) * (share * rules.boardingDiet);
        eaten += a.satiety - before;
    }

    // ---------------------------------------------------------------- growth, age, mood, history
    /// <summary>Growth over a step: by how well it is fed and how healthy it is, x <paramref name="scale"/> (its welfare band's and its illnesses').</summary>
    private static void Grow(AnimalRecord a, AnimalSpeciesData sp, AnimalBreedData br, AnimalRules rules, float dt, float scale, AnimalSimContext ctx, double now)
    {
        float pace = rules.poorGrowth + (1f - rules.poorGrowth) * AnimalMath.Smooth(rules.malnourishedBelow * 0.6f, rules.wellNourishedAbove, a.nutrition);
        if (a.health < 40f) pace *= 0.5f;
        if (a.satiety <= 0f || a.hydration <= 0f) pace = 0f;
        float speed = rules.growthSpeed * (br != null ? br.growth : 1f) * AnimalGenes.Scale(a.genes != null ? a.genes.growth : 0.5f, 0.2f);
        a.development += dt * pace * speed * scale;
        GrowthStage before = a.stage;
        GrowthStage grown = sp.StageAt(a.development, out _);
        GrowthStage next = grown;
        if (grown == GrowthStage.Adult || (sp.Stage(GrowthStage.Adult) == null && grown >= before))
        {
            float senior = sp.seniorAgeHours * AnimalGenes.Scale(a.genes != null ? a.genes.longevity : 0.5f, 0.15f);
            if (sp.seniorAgeHours > 0f && sp.Stage(GrowthStage.Senior) != null && a.AgeHours(now) >= senior) next = GrowthStage.Senior;
        }
        if (before == GrowthStage.Senior) next = GrowthStage.Senior; // nobody grows young again
        if (next > before)
        {
            a.stage = next;
            Tell(a, ctx, rules, AnimalEventKind.StageReached, now, $"{NameOf(a, sp)} is now {Stage(next)}.");
        }
    }

    /// <summary>Health old age takes per game hour (past the lifespan, faster as the years go on).</summary>
    private static float OldAge(AnimalRecord a, AnimalSpeciesData sp, double now)
    {
        if (sp.lifespanHours <= 0f) return 0f;
        float life = sp.lifespanHours * AnimalGenes.Scale(a.genes != null ? a.genes.longevity : 0.5f, 0.15f);
        double over = a.AgeHours(now) - life;
        if (over <= 0.0) return 0f;
        return (float)(0.05 + 2.0 * over / Math.Max(1.0, life * 0.1));
    }

    /// <summary>
    /// Happiness and stress settle toward where its life puts them: hunger, thirst, upsets, poor health and nutrition, a
    /// favourite or a bad meal, plus what its comfort, company, illnesses and welfare band add (<paramref name="happinessAdd"/>
    /// and <paramref name="stressAdd"/>: see <see cref="AnimalCare.MoodTargets"/>).
    /// </summary>
    private static void Mood(AnimalRecord a, AnimalRules rules, float dt, float favourites, float bad, bool boarded, float happinessAdd, float stressAdd)
    {
        float target = boarded ? rules.boardingHappiness : 62f;
        target -= Mathf.Max(0f, 40f - a.satiety) * 0.6f;
        target -= Mathf.Max(0f, 40f - a.hydration) * 0.7f;
        target -= a.upset * 0.3f;
        target -= a.health < 50f ? (50f - a.health) * 0.4f : 0f;
        target += (a.nutrition - 60f) * 0.15f;
        target += happinessAdd;
        float k = AnimalMath.Clamp01(dt / Mathf.Max(1f, rules.moodHours));
        // A favourite meal lifts it, a bad one sours it - by the meal (satiety points), whatever the length of the step.
        float lift = Mathf.Min(favourites / 20f, 2f) * rules.favoriteHappiness;
        float sour = Mathf.Min(bad / 20f, 3f) * 2f;
        a.happiness = AnimalMath.Clamp100(a.happiness + (target - a.happiness) * k + lift - sour);
        float stressTarget = 8f + Mathf.Max(0f, 30f - a.satiety) * 1.2f + Mathf.Max(0f, 30f - a.hydration) * 1.4f + a.upset * 0.25f + (a.health < 40f ? 20f : 0f) + (boarded ? 12f : 0f)
                             + stressAdd;
        a.stress = AnimalMath.Clamp100(a.stress + (stressTarget - a.stress) * k);
    }

    /// <summary>The day's record: its means over the hours it lived that day, what it ate, its hard hours, and (Phase 2) how clean, comfortable, well and ill it was.</summary>
    private static void Record(AnimalRecord a, AnimalRules rules, double from, float dt, float eaten, float favourites, float bad, bool present, float worst)
    {
        int day = (int)Math.Floor(from / 24.0);
        AnimalDay d = a.Today(day);
        float w = d.hours + dt;
        float k = dt / Mathf.Max(0.0001f, w);
        d.satiety += (a.satiety - d.satiety) * k;
        d.hydration += (a.hydration - d.hydration) * k;
        d.nutrition += (a.nutrition - d.nutrition) * k;
        d.health += (a.health - d.health) * k;
        d.happiness += (a.happiness - d.happiness) * k;
        d.stress += (a.stress - d.stress) * k;
        d.minHealth = Mathf.Min(d.minHealth, a.health);
        d.hours = w;
        d.eaten += eaten;
        d.favorite += favourites;
        d.bad += bad;
        if (a.satiety <= 0f) d.starvingHours += dt;
        if (a.hydration <= 0f) d.parchedHours += dt;
        if (!present) d.ownerAwayHours += dt;
        AnimalWelfare.RecordDay(d, a, k, worst);
        a.TrimDays(rules.keepDays);
    }

    // ---------------------------------------------------------------- death and words
    /// <summary>
    /// The animal dies (its record stays, with the cause; its remains are dealt with by the world). Its owner is told and
    /// its history gets "died", unless <paramref name="tell"/> is false (a slaughter: its owner did it, and the command logs
    /// it once, as slaughtered).
    /// </summary>
    public static void Die(AnimalRecord a, AnimalDeathCause cause, double at, AnimalSimContext ctx, AnimalSpeciesData sp, bool tell = true)
    {
        if (a == null || !a.Alive) return;
        a.status = AnimalStatus.Dead;
        a.deathCause = cause;
        a.diedAt = at;
        a.health = 0f;
        if (tell) Tell(a, ctx, ctx?.Rules ?? AnimalRules.Defaults(), AnimalEventKind.Died, at, $"{NameOf(a, sp)} died ({Cause(cause)}).");
        else a.revision++;
    }

    private static void Tell(AnimalRecord a, AnimalSimContext ctx, AnimalRules rules, AnimalEventKind kind, double at, string text)
    {
        a.Log(kind, at, text, "", rules.keepEvents);
        ctx?.Notice(a, kind, text);
    }

    /// <summary>An animal at the start of a sentence: its name, or "A cow" (in the middle of one: <see cref="NameIn"/>).</summary>
    public static string NameOf(AnimalRecord a, AnimalSpeciesData sp) =>
        !string.IsNullOrWhiteSpace(a.name) ? a.name : $"A {(sp != null ? sp.DisplayName.ToLowerInvariant() : "animal")}";

    /// <summary>An animal in the middle of a sentence: its name, or "the cow" ("You look the cow over.").</summary>
    public static string NameIn(AnimalRecord a, AnimalSpeciesData sp) =>
        a != null && !string.IsNullOrWhiteSpace(a.name) ? a.name : "the " + (sp != null ? sp.DisplayName.ToLowerInvariant() : "animal");

    public static string Stage(GrowthStage s)
    {
        switch (s)
        {
            case GrowthStage.Newborn: return "a newborn";
            case GrowthStage.Baby: return "a baby";
            case GrowthStage.Juvenile: return "a juvenile";
            case GrowthStage.Adolescent: return "nearly grown";
            case GrowthStage.Adult: return "fully grown";
            default: return "old";
        }
    }

    public static string Cause(AnimalDeathCause c)
    {
        switch (c)
        {
            case AnimalDeathCause.Starvation: return "starved";
            case AnimalDeathCause.Dehydration: return "of thirst";
            case AnimalDeathCause.Poisoning: return "poisoned";
            case AnimalDeathCause.Malnutrition: return "wasted away from poor feeding";
            case AnimalDeathCause.OldAge: return "of old age";
            case AnimalDeathCause.Illness: return "of illness";
            case AnimalDeathCause.Injury: return "of its injuries";
            case AnimalDeathCause.Slaughtered: return "slaughtered";
            default: return "unknown";
        }
    }
}
