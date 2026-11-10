using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a player sees of an animal: its body (the window draws it, the network sends it), never its hidden numbers. Its
/// condition reaches a client as coarse levels (0 = dire ... 4 = fine) and words, as anyone looking at the animal would
/// judge it; genes, toxin loads and exact nutrition stay where the rules run. Phase 2 adds how clean, comfortable, calm and
/// well it lives, its hooves and how its body looks; and, for its owner and carers only, the illnesses they know of (by
/// name and stage, never a severity or an exposure), the care it takes and the veterinarian's fee.
/// </summary>
[Serializable]
public class AnimalView
{
    public string id = "";
    public string name = "";
    public string species = "";
    public string speciesName = "";
    public string breedName = "";
    public AnimalSex sex;
    public GrowthStage stage;
    public AnimalStatus status;
    public AnimalDeathCause deathCause;
    [Tooltip("Age in game days.")]
    public float ageDays;
    public string ownerName = "";
    public bool mine;
    [Tooltip("Coarse levels 0-4: how full, how watered, how well, how happy, how well fed over the days it looks.")]
    public int hunger, thirst, health, mood, condition;
    [Tooltip("How grown it is (0-1 of an adult's size), for its body.")]
    public float size = 1f;
    public int coat;
    public int pattern;
    public string summary = "";
    public List<string> signs = new List<string>();
    [Tooltip("Bumped when anything shown changed.")]
    public int revision;

    // ---------------------------------------------------------------- Phase 2: care, welfare and health
    [Tooltip("Coarse levels 0-4: how clean it is, how comfortable, how calm (100 - stress) and how well it lives (its welfare).")]
    public int hygiene, comfort, calm, welfare;
    [Tooltip("Its welfare band in a word (Excellent, Good, Average, Poor, Critical).")]
    public string welfareWord = "";
    [Tooltip("Its hooves' level 0-4 (-1: it has no hooves to care for).")]
    public int hooves = -1;
    [Tooltip("How its body looks (AnimalLooks flags: dirty, sick, lame, stressed, thin, content).")]
    public int looks;
    [Tooltip("The illnesses its keepers know of (named by a careful look or the veterinarian): for its owner and carers only.")]
    public List<AnimalConditionView> conditions = new List<AnimalConditionView>();
    [Tooltip("The care its species takes, and whether each can be done now: for its owner and carers only (none while it is boarded).")]
    public List<AnimalCareView> care = new List<AnimalCareView>();
    [Tooltip("The veterinarian's fee to see it (0 also when there is no veterinarian, or it is not the viewer's to send for: see Vet Can Come).")]
    public long vetFee;
    [Tooltip("The viewer may send for the veterinarian to see it (Vet Fee is then the fee; 0: a free visit).")]
    public bool vetCanCome;
    [Tooltip("The family of the care it was given last, + 1 (AnimalCareKind; 0: none), and when (game hour; -1: never): its body reacts when they change.")]
    public int lastCare;
    public double lastCareAt = -1.0;

    // ---------------------------------------------------------------- Phase 3: production
    [Tooltip("What it gives and what waits to be collected: for its owner and carers only.")]
    public List<AnimalProductView> products = new List<AnimalProductView>();
    [Tooltip("How far its fleece has grown (0-1; -1: it grows none): its body shows it.")]
    public float fleece = -1f;

    // ---------------------------------------------------------------- Phase 4: family, breeding, the bond, behaviour
    [Tooltip("Its bond with its owner (its owner only): coarse 0-4 (-1: not shown), in words, and the perks it has unlocked.")]
    public int bond = -1;
    public string bondWord = "";
    public List<string> perks = new List<string>();
    [Tooltip("Its family (owner and carers): its parents' names, its generation, the young it has had.")]
    public string motherName = "", fatherName = "";
    public int generation, offspring;
    [Tooltip("Its inheritance in words (its owner only).")]
    public List<string> traits = new List<string>();
    [Tooltip("Expecting, in words (\"in calf: due in about 9 days\"; owner and carers; empty: not).")]
    public string expecting = "";
    [Tooltip("It can be bred now, on its own account (its owner only), and why not.")]
    public bool breedReady;
    public string breedHint = "";
    [Tooltip("The partners it could be bred with in this pen (its owner only; a pen's picture fills it).")]
    public List<AnimalPartnerView> partners = new List<AnimalPartnerView>();
    // ---------------------------------------------------------------- Phase 5: trading
    [Tooltip("It can be crated now (its owner only; a pen's picture fills it), and why not.")]
    public bool crateReady;
    public string crateHint = "";
    [Tooltip("Its grade (0-15) and worth against a young one of its breed as it would be crated now, in words (its owner only).")]
    public int grade = -1;
    public float worth;
    public string worthWord = "";
    [Tooltip("Its relationships in words (owner and carers): \"close to Bella\", \"a fierce rival of Ferdinand\".")]
    public List<string> relations = new List<string>();
    [Tooltip("How its body behaves, for every viewer: shy of people (a skittish temper: as plain as its ways), its mother here (a young keeps to her), its closest companion here, the ribbon of a deep bond, heavy with young.")]
    public bool shy;
    public string motherHere = "", friend = "";
    public bool ribbon, heavy;
    [Tooltip("What it does for its owner (true only in its owner's own picture): follows them, greets them, comes when called.")]
    public bool follows, greets, comesWhenCalled;
}

/// <summary>A partner an animal could be bred with (its owner's window offers it), ready or not and why.</summary>
[Serializable]
public class AnimalPartnerView
{
    public string id = "";
    public string name = "";
    public bool ready;
    [Tooltip("Why not (empty: ready), or a note (\"related\").")]
    public string hint = "";
}

/// <summary>A product as its keeper's window shows it (a Collect request names it by <see cref="id"/>).</summary>
[Serializable]
public class AnimalProductView
{
    public string id = "";
    public string name = "";
    [Tooltip("Collecting it, as a button reads (\"Collect\", \"Milk\", \"Shear\").")]
    public string verb = "";
    [Tooltip("Units waiting, and in words (\"2 eggs\").")]
    public int waiting;
    public string waitingText = "";
    [Tooltip("Something waits to be collected.")]
    public bool ready;
    [Tooltip("The tier of what waits (\"Good\"; empty: nothing waits).")]
    public string tier = "";
    [Tooltip("How it makes it, in words (\"laying\", \"full: collect it\", \"too poorly fed to give any\").")]
    public string status = "";
    [Tooltip("How far the next lot is, coarse 0-4.")]
    public int progress;
    [Tooltip("The tool tags collecting it needs (empty: by hand), and in words.")]
    public List<string> tools = new List<string>();
    public string toolText = "";
}

/// <summary>An illness as its keepers know it (named by a careful look or the veterinarian): its name and stage in words, never its severity.</summary>
[Serializable]
public class AnimalConditionView
{
    public string id = "";
    public string name = "";
    [Tooltip("Its stage in a word (\"Moderate\", \"Recovering\").")]
    public string stage = "";
    [Tooltip("A treatment works on it now.")]
    public bool treated;
    public bool recovering;
    [Tooltip("Only the veterinarian can turn it now (shown only while there is one in the world).")]
    public bool needsVet;
}

/// <summary>One care action an animal's species takes, as its keeper's window shows it (a Care request names it by <see cref="id"/>).</summary>
[Serializable]
public class AnimalCareView
{
    [Tooltip("The action's id (a Care request sends it in its text).")]
    public string id = "";
    [Tooltip("Its family (AnimalCareKind), for its icon.")]
    public int kind;
    public string label = "";
    [Tooltip("It can be done now (old enough, not done lately).")]
    public bool ready;
    [Tooltip("Why not, in words: \"brushed 2 hours ago\", \"too young\" (empty: ready).")]
    public string hint = "";
    [Tooltip("The tool tags it needs, any one of them (empty: bare hands).")]
    public List<string> tools = new List<string>();
    [Tooltip("Those tools in words (\"a soft brush or a curry comb\"; empty: bare hands).")]
    public string toolText = "";
    [Tooltip("The litres of water it takes for this animal (0: none).")]
    public float waterLitres;
    [Tooltip("The supply tag that makes it work better (\"soap\"; empty: none).")]
    public string supply = "";
    [Tooltip("It can be done, but does this animal no good.")]
    public bool inappropriate;
}

/// <summary>What a player sees of a pen and its animals.</summary>
[Serializable]
public class AnimalPenView
{
    public string id = "";
    public string name = "";
    public AnimalHabitatKind kind;
    public float area;
    public float spaceUsed;
    public string feeder = "";
    public string water = "";
    [Tooltip("The pasture's grass as it looks (empty: no grass grows in it).")]
    public string grass = "";
    public float feederShare, waterShare, grassShare;
    public List<AnimalView> animals = new List<AnimalView>();
    [Tooltip("The player's own animals elsewhere: those that could be brought here, and those at the boarding stable (to see to from here, or their remains to clear).")]
    public List<AnimalView> elsewhere = new List<AnimalView>();
    [Tooltip("What the player may do here (AnimalRights).")]
    public int rights;
    [Tooltip("The game hour of the picture (a client sets its farm clock by it).")]
    public double now;
    public int revision;

    // ---------------------------------------------------------------- Phase 2: how clean it is
    [Tooltip("How clean its floor is, its bedding and its trough water, in words (the water's word is also in the water text).")]
    public string cleanliness = "", beddingState = "", waterState = "";
    [Tooltip("Coarse levels 0-4: how soiled its floor is (4: filthy), how fresh its bedding is (4: fresh; -1: it takes none), how fouled its trough water is (4: foul).")]
    public int dirtLevel, beddingLevel = -1, waterLevel;
    public bool usesBedding;
    [Tooltip("There is something to muck out or straw to lay, it was not done just now, and the viewer may tend the pen (a fork or shovel is still needed).")]
    public bool cleanReady;
    [Tooltip("Its trough water is fouled enough to change, and the viewer may scrub it out (CleanPen with the text \"trough\").")]
    public bool scrubReady;
    [Tooltip("Its shelter and the weather now, in words.")]
    public string shelter = "", weather = "";

    // ---------------------------------------------------------------- Phase 3: what waits to be collected
    [Tooltip("What waits to be collected from the animals the viewer may collect from, in words (\"5 eggs, 6 jugs of milk\"; empty: nothing).")]
    public string productsWaiting = "";
    [Tooltip("Something waits that the viewer may collect (a Collect request for the whole pen).")]
    public bool collectReady;

    // ---------------------------------------------------------------- Phase 5: its level, machines and upkeep
    [Tooltip("Its area as built (Area is its usable area at its level).")]
    public float baseArea;
    public int level = 1, maxLevel = 1;
    public string levelName = "";
    [Tooltip("The next level: its name, its price (-1: none, the pen is at the top) and what it does, in words.")]
    public string nextLevelName = "";
    public long nextLevelPrice = -1;
    public string nextLevelText = "";
    [Tooltip("Pens can be improved here (the rules' Upgrades are on).")]
    public bool upgradesOn;
    [Tooltip("The viewer may build in it (they keep animals here), and pay its upkeep (they tend it).")]
    public bool canBuild, canPay;
    public bool hopper, waterer, trays;
    [Tooltip("The viewer may take out each machine fitted (who fitted it, or one who may care for others' animals here).")]
    public bool removeHopper, removeWaterer, removeTrays;
    [Tooltip("What fitting each machine costs.")]
    public long hopperPrice, watererPrice, traysPrice;
    [Tooltip("How full its hopper is (0-1), and in words.")]
    public float hopperShare;
    public string hopperText = "";
    public string watererText = "";
    [Tooltip("What waits in its egg trays (all of it; the viewer's share is in Products Waiting), and how much they hold.")]
    public int trayUnits, trayCapacity;
    public string traysText = "";
    [Tooltip("Its upkeep a day, what it owes now (rounded up: the sum Pay Upkeep asks), and whether it is in disrepair.")]
    public float upkeepPerDay;
    public long upkeepOwed;
    public bool disrepair;
    public string upkeepText = "";
    [Tooltip("What paying what it owes and a week ahead costs (a PayUpkeep request with the count 7), and the days it is paid ahead.")]
    public long upkeepWeekPrice;
    public float paidAheadDays;

    public string ToJson() => JsonUtility.ToJson(this);
    public static AnimalPenView FromJson(string json) => string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AnimalPenView>(json);
}

/// <summary>Turns the rules' numbers into what can be seen.</summary>
public static class AnimalObservation
{
    /// <summary>0 (dire) to 4 (fine) from a 0-100 value.</summary>
    public static int Level(float v) => v >= 80f ? 4 : v >= 60f ? 3 : v >= 40f ? 2 : v >= 20f ? 1 : 0;

    /// <summary>What anyone sees of an animal (no rights in its pen, no veterinarian): its owner still sees what is theirs to know.</summary>
    public static AnimalView View(AnimalRecord a, IAnimalCatalog cat, double now, string viewer) => View(a, cat, now, viewer, AnimalRights.None, null, false);

    /// <summary>
    /// What a viewer sees of an animal. Everyone: its body, its coarse levels and words, its signs and how it looks. Its
    /// owner, and those who may care for others' animals in its pen (<paramref name="rights"/>): the illnesses they know of,
    /// the care it takes and the veterinarian's fee (<paramref name="vetFee"/>; null: there is no veterinarian; with one,
    /// <see cref="AnimalView.vetCanCome"/> is set, whatever the fee, so a free visit can be sent for). "Needs a
    /// veterinarian" is shown only while there is one (<paramref name="vetAvailable"/>).
    /// </summary>
    public static AnimalView View(AnimalRecord a, IAnimalCatalog cat, double now, string viewer, AnimalRights rights,
        Func<AnimalRecord, AnimalSpeciesData, long> vetFee, bool vetAvailable)
    {
        AnimalSpeciesData sp = cat?.Species(a.species);
        AnimalBreedData br = sp?.BreedOrDefault(a.breed);
        AnimalRules rules = cat?.Rules ?? AnimalRules.Defaults();
        var v = new AnimalView
        {
            id = a.id,
            name = AnimalSim.NameOf(a, sp),
            species = a.species,
            speciesName = sp != null ? sp.DisplayName : a.species,
            breedName = br != null ? br.name : "",
            sex = a.sex,
            stage = a.stage,
            status = a.status,
            deathCause = a.deathCause,
            ageDays = (float)(a.AgeHours(now) / 24.0),
            ownerName = a.ownerName ?? "",
            mine = !string.IsNullOrEmpty(viewer) && a.owner == viewer,
            hunger = Level(a.satiety),
            thirst = Level(a.hydration),
            health = Level(a.health),
            mood = Level(a.happiness - a.stress * 0.5f + 15f),
            condition = Level(a.nutrition),
            size = sp != null ? Mathf.Clamp(sp.StageOrNearest(a.stage)?.size ?? 1f, 0.05f, 1.5f) * (br != null ? br.size : 1f) * AnimalGenes.Scale(a.genes != null ? a.genes.size : 0.5f, 0.15f) : 1f,
            coat = a.genes != null ? a.genes.coat : 0,
            pattern = a.genes != null ? a.genes.pattern : 0,
            revision = a.revision,
        };
        v.summary = Summary(a, sp);
        SignsOf(a, sp, rules, cat, v.signs);
        LastCare(a, sp, out int lastKind, out double lastAt);
        v.lastCare = lastKind;
        v.lastCareAt = lastAt;
        if (!a.Alive) return v;
        v.hygiene = Level(a.hygiene);
        v.comfort = Level(a.comfort);
        v.calm = Level(100f - a.stress);
        v.welfare = Level(a.welfare);
        v.welfareWord = AnimalWelfare.WordOf(a, rules);
        v.hooves = AnimalCare.CareOf(sp).HasHooves ? Level(a.hooves) : -1;
        v.looks = (int)Looks(a, sp, cat, rules);
        v.fleece = FleeceOf(a, sp);
        bool carer = v.mine || (rights & AnimalRights.CareForOthers) != 0;
        Behaviour(a, sp, rules, now, v);
        Family(a, sp, rules, now, v, carer);
        if (!carer) return v;
        ConditionViews(a, cat, rules, now, vetAvailable, v.conditions);
        CareViews(a, sp, cat, now, v.care);
        // What it gives: for its owner, and for its carers where they may collect it.
        if (v.mine || rules.production == null || rules.production.othersCollect) ProductViews(a, sp, cat, rules, v.products);
        if (vetFee != null && sp != null)
        {
            v.vetFee = Math.Max(0L, vetFee(a, sp));
            v.vetCanCome = true;
        }
        return v;
    }

    /// <summary>
    /// What its body shows every viewer of its ways (Phase 4): its temper, the mother a young keeps to and its closest
    /// companion when they are with it, a deep bond's ribbon, a mother heavy with young; and, in its owner's own picture,
    /// whether it follows, greets and comes when called by them.
    /// </summary>
    private static void Behaviour(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now, AnimalView v)
    {
        // Only what its ways show anyone: shy of people or not (the threshold its owner's words use for "skittish").
        v.shy = a.genes != null && a.genes.temperament < 0.38f;
        AnimalBehaviourSpec b = sp?.behaviour;
        double recent = a.livedTo - Math.Max(1.5, (rules?.stepHours ?? 1f) * 1.5);
        if (b != null && b.youngFollowMother && !string.IsNullOrEmpty(a.mother) && a.stage < GrowthStage.Adolescent)
        {
            AnimalBond m = AnimalSocial.BondWith(a, a.mother);
            if (m != null && m.lastTogether >= recent) v.motherHere = a.mother;
        }
        float best = 0f;
        if (a.bonds != null)
            foreach (AnimalBond bond in a.bonds)
                if (bond != null && bond.kind != AnimalBondKind.Rival && bond.lastTogether >= recent && bond.level > best && bond.level >= 20f)
                {
                    best = bond.level;
                    v.friend = bond.other;
                }
        v.ribbon = AnimalSocial.HasPerk(a, sp, AnimalPerkKind.Ribbon);
        if (AnimalBreeding.Expecting(a))
        {
            double length = Math.Max(1.0, a.pregnancy.dueAt - a.pregnancy.conceivedAt);
            v.heavy = (now - a.pregnancy.conceivedAt) / length >= 0.6;
        }
        if (v.mine)
        {
            v.follows = AnimalSocial.HasPerk(a, sp, AnimalPerkKind.Follow);
            v.greets = AnimalSocial.HasPerk(a, sp, AnimalPerkKind.Greet);
            v.comesWhenCalled = AnimalSocial.HasPerk(a, sp, AnimalPerkKind.ComeWhenCalled);
        }
    }

    /// <summary>
    /// Its family, breeding and bonds, for those who keep it: its parents, generation and young, whether it is expecting and
    /// its relationships (owner and carers); its bond with its owner, its perks, its inheritance in words and whether it can
    /// be bred now (its owner).
    /// </summary>
    private static void Family(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now, AnimalView v, bool carer)
    {
        if (!carer) return;
        v.motherName = a.motherName ?? "";
        v.fatherName = a.fatherName ?? "";
        v.generation = a.generation;
        v.offspring = a.offspring;
        if (AnimalBreeding.Expecting(a)) v.expecting = $"{AnimalBreeding.SpecOf(sp).expectingWord}: {AnimalBreeding.DueIn(a, now)}";
        double recent = a.livedTo - Math.Max(1.5, (rules?.stepHours ?? 1f) * 1.5);
        if (a.bonds != null)
        {
            var sorted = new List<AnimalBond>(a.bonds);
            sorted.Sort((x, y) =>
            {
                int c = y.level.CompareTo(x.level);
                return c != 0 ? c : string.CompareOrdinal(x.other, y.other);
            });
            foreach (AnimalBond b in sorted)
            {
                if (b == null || v.relations.Count >= 4) continue;
                string word = AnimalSocial.RelationWord(b, rules?.social, b.lastTogether >= recent);
                if (!string.IsNullOrEmpty(word)) v.relations.Add(word);
            }
        }
        if (!v.mine) return;
        v.bond = Level(a.affinity);
        v.bondWord = AnimalSocial.BondWord(a.affinity);
        var perks = new List<AnimalAffinityPerk>();
        AnimalSocial.Perks(a, sp, perks);
        foreach (AnimalAffinityPerk p in perks) v.perks.Add(p.name);
        AnimalBreeding.TraitWords(a.genes, v.traits);
        string why = null;
        v.breedReady = a.Alive && AnimalBreeding.Ready(a, sp, rules, now, out why);
        v.breedHint = why ?? "";
    }

    /// <summary>
    /// The partners the viewer's animals in a pen could be bred with (their own animals there of the same kind and the other
    /// sex), each ready or not and why, as the rules would answer a Breed request.
    /// </summary>
    private static void Partners(AnimalHerd h, IAnimalCatalog cat, AnimalRules rules, string viewer, double now, AnimalPenView v)
    {
        if (string.IsNullOrEmpty(viewer) || h == null) return;
        foreach (AnimalView av in v.animals)
        {
            if (av == null || !av.mine || av.status == AnimalStatus.Dead) continue;
            AnimalRecord a = null;
            foreach (AnimalRecord x in h.animals)
                if (x != null && x.id == av.id) a = x;
            AnimalSpeciesData sp = a != null ? cat?.Species(a.species) : null;
            if (a == null || sp == null || !AnimalBreeding.SpecOf(sp).enabled) continue;
            foreach (AnimalRecord b in h.animals)
            {
                if (b == null || b == a || !b.Alive || b.owner != viewer || b.species != a.species) continue;
                if (sp.sexed && b.sex == a.sex) continue;
                var pv = new AnimalPartnerView { id = b.id, name = AnimalSim.NameOf(b, sp) };
                string why = null;
                bool ok = AnimalBreeding.Compatible(a, b, sp, rules, out AnimalRecord mother, out AnimalRecord father, out why)
                          && AnimalBreeding.Ready(mother, sp, rules, now, out why) && AnimalBreeding.Ready(father, sp, rules, now, out why);
                pv.ready = ok;
                if (!ok) pv.hint = why ?? "";
                else if (AnimalBreeding.Kinship(a, b) > 0f) pv.hint = "related: their young may be weaker";
                av.partners.Add(pv);
            }
        }
    }

    /// <summary>
    /// How an animal's body looks (for its body in the world): Dirty (hygiene below 30), Sick (an illness that shows and costs
    /// health, or health below 40), Lame (moving badly, or cracked hooves), Stressed (stress 60 or more), Thin (malnourished),
    /// Content (an Excellent life).
    /// </summary>
    public static AnimalLooks Looks(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules = null)
    {
        if (a == null || !a.Alive) return AnimalLooks.None;
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        AnimalLooks l = AnimalLooks.None;
        if (a.hygiene < 30f) l |= AnimalLooks.Dirty;
        if (AnimalHealth.LooksSick(a, cat)) l |= AnimalLooks.Sick;
        float mobility = cat != null ? AnimalHealth.Effects(a, cat).mobility : 1f;
        if (mobility < 0.7f || (AnimalCare.CareOf(sp).HasHooves && a.hooves < 30f)) l |= AnimalLooks.Lame;
        if (a.stress >= 60f) l |= AnimalLooks.Stressed;
        if (a.nutrition < rules.malnourishedBelow) l |= AnimalLooks.Thin;
        if (AnimalWelfare.BandOf(a.welfare, rules.welfare) == AnimalWelfareBand.Excellent) l |= AnimalLooks.Content;
        return l;
    }

    /// <summary>The illnesses its keepers know of (diagnosed, and shown: one that came back in a stage that hides stays unknown until it shows), in words.</summary>
    private static void ConditionViews(AnimalRecord a, IAnimalCatalog cat, AnimalRules rules, double now, bool vetAvailable, List<AnimalConditionView> into)
    {
        if (a.conditions == null || cat == null) return;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null || !k.diagnosed || !k.shown) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c == null) continue;
            into.Add(new AnimalConditionView
            {
                id = c.id,
                name = c.DisplayName,
                stage = c.StageName(k),
                treated = k.Treated(now),
                recovering = k.Recovering,
                needsVet = AnimalHealth.Gated(k, c, rules, vetAvailable),
            });
        }
    }

    /// <summary>
    /// What it gives, as its keeper sees it: each product it makes (or has waiting), how much waits and of what tier, how it
    /// is making it in words, how far the next lot is (coarse), and the tool collecting it needs.
    /// </summary>
    private static void ProductViews(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules, List<AnimalProductView> into)
    {
        if (sp?.products == null) return;
        foreach (AnimalProductSpec p in sp.products)
        {
            if (p == null) continue;
            AnimalProductState st = AnimalProduction.State(a, p.id, false);
            bool makes = AnimalProduction.Makes(a, sp, p);
            int waiting = st != null ? st.Waiting : 0;
            if (!makes && waiting <= 0) continue;
            var pv = new AnimalProductView
            {
                id = p.id,
                name = p.DisplayName,
                verb = p.Verb,
                waiting = waiting,
                waitingText = waiting > 0 ? p.Count(waiting) : "",
                ready = waiting > 0,
                progress = st != null ? Mathf.Clamp(Mathf.FloorToInt(AnimalMath.Clamp01(st.progress) * 5f), 0, 4) : 0,
                toolText = p.NeedsTool ? AnimalCare.ToolNames(cat, p.tools) : "",
            };
            if (p.tools != null) pv.tools.AddRange(p.tools);
            if (waiting > 0)
            {
                float q = 0f;
                foreach (AnimalProductLot l in st.waiting)
                    if (l != null) q += l.quality * l.units;
                pv.tier = AnimalProduction.TierWord(AnimalProduction.Tier(AnimalItemCode.Hundredth(q / waiting), AnimalProduction.Rare(a), rules.production));
            }
            AnimalProductStatus status = st != null ? st.status : makes ? AnimalProductStatus.Producing : AnimalProductStatus.Resting;
            pv.status = status == AnimalProductStatus.Producing ? "making it" : AnimalProduction.StatusText(status, p);
            into.Add(pv);
        }
    }

    /// <summary>How far a fleece has grown on it (0-1; -1: its species grows none, or it is dead).</summary>
    public static float FleeceOf(AnimalRecord a, AnimalSpeciesData sp)
    {
        if (a == null || sp?.products == null || !a.Alive) return -1f;
        foreach (AnimalProductSpec p in sp.products)
        {
            if (p == null || p.tags == null || !p.tags.Contains("fleece")) continue;
            AnimalProductState st = AnimalProduction.State(a, p.id, false);
            if (st == null) return 0f;
            return st.Waiting > 0 ? 1f : AnimalMath.Clamp01(st.progress);
        }
        return -1f;
    }

    /// <summary>The care its species takes, each with whether it can be done now (none for the dead and the boarded: the stable sees to them).</summary>
    private static void CareViews(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, double now, List<AnimalCareView> into)
    {
        if (!a.Alive || a.status == AnimalStatus.Boarded || string.IsNullOrEmpty(a.pen)) return;
        List<AnimalCareActionSpec> actions = sp?.care?.actions;
        if (actions == null) return;
        foreach (AnimalCareActionSpec s in actions)
        {
            if (s == null) continue;
            var tools = new List<string>();
            if (s.tools != null)
                foreach (string t in s.tools)
                    if (!string.IsNullOrWhiteSpace(t)) tools.Add(t.Trim());
            into.Add(new AnimalCareView
            {
                id = s.Id,
                kind = (int)s.kind,
                label = s.Label,
                ready = AnimalCare.CanDo(a, sp, s, now, out _),
                hint = AnimalCare.Hint(a, s, now),
                tools = tools,
                toolText = tools.Count > 0 ? AnimalCare.ToolNames(cat, tools) : "",
                waterLitres = AnimalCare.WaterFor(s, sp, a),
                supply = s.supply ?? "",
                inappropriate = s.inappropriate,
            });
        }
    }

    /// <summary>The care it was given last: its family + 1 (0: none) and when (-1: never).</summary>
    private static void LastCare(AnimalRecord a, AnimalSpeciesData sp, out int kind, out double at)
    {
        kind = 0;
        at = -1.0;
        AnimalCareMark last = LastMark(a);
        if (last == null) return;
        AnimalCareActionSpec s = sp?.CareAction(last.id);
        kind = (s != null ? (int)s.kind : (int)AnimalCareKind.Custom) + 1;
        at = last.at;
    }

    private static AnimalCareMark LastMark(AnimalRecord a)
    {
        AnimalCareMark last = null;
        if (a?.care == null) return null;
        foreach (AnimalCareMark m in a.care)
            if (m != null && m.at >= 0.0 && (last == null || m.at > last.at)) last = m;
        return last;
    }

    /// <summary>The words for its state at a glance ("Hungry and listless", "Content", "Dead: starved").</summary>
    public static string Summary(AnimalRecord a, AnimalSpeciesData sp)
    {
        if (a.status == AnimalStatus.Dead) return $"Dead - {AnimalSim.Cause(a.deathCause)}";
        var parts = new List<string>();
        if (a.satiety <= 0f) parts.Add("starving");
        else if (a.satiety < 30f) parts.Add("hungry");
        if (a.hydration <= 0f) parts.Add("parched");
        else if (a.hydration < 30f) parts.Add("thirsty");
        if (a.health < 30f) parts.Add("very weak");
        else if (a.health < 60f) parts.Add("unwell");
        if (parts.Count == 0)
        {
            float mood = a.happiness - a.stress * 0.5f;
            parts.Add(mood >= 55f ? "content" : mood >= 30f ? "calm" : "unsettled");
        }
        string s = string.Join(", ", parts);
        s = char.ToUpperInvariant(s[0]) + s.Substring(1);
        if (a.status == AnimalStatus.Boarded) s += " (at the boarding stable)";
        return s;
    }

    /// <summary>What a watchful keeper notices: symptoms, never their cause.</summary>
    public static void Signs(AnimalRecord a, AnimalSpeciesData sp, List<string> into) => SignsOf(a, sp, null, null, into);

    /// <summary>What a watchful keeper notices, by the same thresholds the rules harm the animal by (no catalog: without its illnesses' symptoms).</summary>
    public static void Signs(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, List<string> into) => SignsOf(a, sp, rules, null, into);

    /// <summary>
    /// What a watchful keeper notices: hunger, thirst, poison, upsets, condition, age and nerves; the symptoms of its
    /// illnesses that show (the first of each, then the others: at most 6); cracked hooves, its coat, what makes it
    /// uncomfortable (comfort below 60) or stressed (above 35), and "It needs help." when it is in a bad way (its welfare is
    /// Critical, or its health below the rules' Needs Help Below Health: as its owner is told).
    /// </summary>
    public static void Signs(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, List<string> into) => SignsOf(a, sp, cat?.Rules, cat, into);

    private static void SignsOf(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, IAnimalCatalog cat, List<string> into)
    {
        into.Clear();
        if (!a.Alive) return;
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        // A mother late in carrying her young shows it.
        if (AnimalBreeding.Expecting(a) && (a.livedTo - a.pregnancy.conceivedAt) / Math.Max(1.0, a.pregnancy.dueAt - a.pregnancy.conceivedAt) >= 0.6)
            into.Add("She is heavy with young.");
        // An illness that shows says it better than the general sign of the same trouble (with a catalog to know it by).
        bool poisoned = Shows(a, cat, AnimalConditionKind.Poisoning), upset = Shows(a, cat, AnimalConditionKind.Digestive);
        bool parched = Shows(a, cat, AnimalConditionKind.Dehydration), wasting = Shows(a, cat, AnimalConditionKind.Malnutrition);
        if (a.satiety <= 0f) into.Add("Its ribs show and it bawls for food.");
        else if (a.satiety < 30f) into.Add("It noses the ground for anything to eat.");
        if (a.hydration <= 0f)
        {
            if (!parched) into.Add("Its eyes are sunken and its mouth is dry.");
        }
        else if (a.hydration < 30f) into.Add("It keeps going back to the trough.");
        float tolerance = AnimalSim.Tolerance(a, sp);
        if (!poisoned)
        {
            if (a.toxin > tolerance) into.Add("It is trembling and drooling.");
            else if (a.toxin > tolerance * 0.5f) into.Add("It seems dull and sluggish.");
        }
        if (!upset)
        {
            if (a.upset >= 40f) into.Add("Its droppings are loose and it stands hunched.");
            else if (a.upset >= 15f) into.Add("It shifts as if its belly troubles it.");
        }
        if (a.nutrition < rules.malnourishedBelow)
        {
            if (!wasting) into.Add("Its coat is dull and it looks thin.");
        }
        else if (a.nutrition >= 85f && a.health >= 80f) into.Add("Its coat shines and it carries good flesh.");
        if (a.stage == GrowthStage.Senior) into.Add("It moves stiffly with age.");
        if (a.stress >= 60f) into.Add("It startles at every sound.");
        if (a.happiness >= 80f && a.stress < 25f) into.Add("It seems at ease.");

        // Phase 2: its illnesses' symptoms, hooves, coat, what bothers it, and a cry for help.
        if (cat != null) AnimalHealth.Symptoms(a, cat, into, 6);
        bool limps = AnimalCare.CareOf(sp).HasHooves && a.hooves < 30f;
        if (limps) Add(into, "It limps on cracked hooves.");
        bool caked = a.hygiene < 30f;
        if (caked) Add(into, "Its coat is caked with dirt.");
        else if (a.hygiene > 85f) Add(into, "It looks clean and well kept.");
        string discomfort = a.comfort < 60f ? Bother(a.discomfort, caked, limps) : "";
        if (discomfort.Length > 0) Add(into, discomfort);
        else if (a.comfort < 35f) Add(into, "It looks uncomfortable.");
        string stressor = a.stress > 35f ? Bother(a.stressor, caked, limps) : "";
        if (stressor.Length > 0) Add(into, stressor);
        // As its owner is told it is in a bad way: Critical welfare, or health below the rules' Needs Help Below Health.
        if (AnimalWelfare.BandOf(a.welfare, rules.welfare) == AnimalWelfareBand.Critical || a.health < rules.needsHelpBelowHealth) Add(into, "It needs help.");
    }

    /// <summary>What bothers it in a sentence, unless a sign already says it (a caked coat, cracked hooves).</summary>
    private static string Bother(AnimalDiscomfort d, bool caked, bool limps)
    {
        if ((d == AnimalDiscomfort.Coat && caked) || (d == AnimalDiscomfort.Hooves && limps)) return "";
        return DiscomfortSentence(d);
    }

    /// <summary>
    /// What makes an animal uncomfortable or stressed, as a keeper sees it ("It huddles against the cold."); "" for none and
    /// for illness (its symptoms say it).
    /// </summary>
    public static string DiscomfortSentence(AnimalDiscomfort d)
    {
        switch (d)
        {
            case AnimalDiscomfort.Cold: return "It huddles against the cold.";
            case AnimalDiscomfort.Heat: return "It pants in the heat.";
            case AnimalDiscomfort.Rain: return "It stands miserable in the rain.";
            case AnimalDiscomfort.Crowding: return "It is jostled in a crowded pen.";
            case AnimalDiscomfort.Dirt: return "It picks its way through a filthy pen.";
            case AnimalDiscomfort.Bedding: return "It has nothing dry to lie on.";
            case AnimalDiscomfort.Hooves: return "It shifts its weight off sore hooves.";
            case AnimalDiscomfort.Coat: return "Its coat is caked and itchy.";
            case AnimalDiscomfort.Loneliness: return "It calls for others of its kind.";
            case AnimalDiscomfort.Handling: return "It is wary of being handled.";
            default: return "";
        }
    }

    /// <summary>Does an illness of this kind show now (its keeper was told)? Needs the catalog.</summary>
    private static bool Shows(AnimalRecord a, IAnimalCatalog cat, AnimalConditionKind kind)
    {
        if (cat == null || a?.conditions == null) return false;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null || !k.shown || k.Recovering) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c != null && c.kind == kind && c.Stage(k.stage).visible) return true;
        }
        return false;
    }

    private static void Add(List<string> into, string s)
    {
        if (!string.IsNullOrEmpty(s) && !into.Contains(s)) into.Add(s);
    }

    /// <summary>The lines of a close look at one animal (the Inspect answer).</summary>
    public static void Describe(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, double now, bool owner, List<string> lines)
    {
        lines.Clear();
        AnimalBreedData br = sp?.BreedOrDefault(a.breed);
        string kind = sp != null ? sp.DisplayName : a.species;
        lines.Add($"{(br != null ? br.name + " " : "")}{kind.ToLowerInvariant()}, {AnimalCommands.Sex(a.sex)}, {AnimalSim.Stage(a.stage)}, {Age(a.AgeHours(now))} old.");
        lines.Add(Summary(a, sp) + ".");
        var signs = new List<string>();
        Signs(a, sp, cat, signs);
        lines.AddRange(signs);
        if (!a.Alive) return;
        AnimalRules rules = cat?.Rules ?? AnimalRules.Defaults();
        lines.Add(AnimalCare.CareOf(sp).HasHooves
            ? $"Its coat is {CoatWord(a.hygiene)}, it seems {ComfortWord(a.comfort)}, and its hooves are {HoofWord(a.hooves)}."
            : $"Its coat is {CoatWord(a.hygiene)} and it seems {ComfortWord(a.comfort)}.");
        lines.Add($"Its welfare: {AnimalWelfare.WordOf(a, rules)}.");
        if (owner)
        {
            if (AnimalBreeding.Expecting(a)) lines.Add($"She is {AnimalBreeding.SpecOf(sp).expectingWord} ({AnimalBreeding.DueIn(a, now)}).");
            if (a.affinity >= 60f) lines.Add("It comes to you as soon as it sees you.");
            else if (a.affinity >= 25f) lines.Add("It knows you.");
            else lines.Add("It does not know you well yet.");
            if (!string.IsNullOrEmpty(a.mother) || !string.IsNullOrEmpty(a.father))
                lines.Add($"Bred here: out of {(string.IsNullOrWhiteSpace(a.motherName) ? "a dam" : a.motherName)} by {(string.IsNullOrWhiteSpace(a.fatherName) ? "a sire" : a.fatherName)} (generation {a.generation}).");
            // What its owner knows it has (named by a careful look or the vet, and shown): in words, never a severity.
            if (a.conditions != null && cat != null)
                foreach (AnimalCondition k in a.conditions)
                {
                    if (k == null || !k.diagnosed || !k.shown) continue;
                    AnimalConditionData c = cat.Condition(k.id);
                    if (c == null) continue;
                    lines.Add($"Known illness: {c.DisplayName.ToLowerInvariant()} ({c.StageName(k)}){(k.Treated(now) ? ", being treated" : "")}.");
                }
        }
        if (a.lastFedAt >= 0.0) lines.Add($"Last fed by hand {Age(now - a.lastFedAt)} ago.");
        AnimalCareMark last = LastMark(a);
        AnimalCareActionSpec done = last != null ? sp?.CareAction(last.id) : null;
        if (done != null) lines.Add($"Last {done.PastVerb} {Age(now - last.at)} ago.");
    }

    /// <summary>Its coat in a word: clean, fairly clean, dirty, filthy.</summary>
    public static string CoatWord(float hygiene) => hygiene >= 75f ? "clean" : hygiene >= 50f ? "fairly clean" : hygiene >= 30f ? "dirty" : "filthy";

    /// <summary>How comfortable it seems: comfortable, fairly comfortable, uncomfortable, miserable.</summary>
    public static string ComfortWord(float comfort) => comfort >= 75f ? "comfortable" : comfort >= 50f ? "fairly comfortable" : comfort >= 30f ? "uncomfortable" : "miserable";

    /// <summary>Its hooves in words: sound, a little worn, overgrown, cracked and overgrown.</summary>
    public static string HoofWord(float hooves) => hooves >= 75f ? "sound" : hooves >= 50f ? "a little worn" : hooves >= 30f ? "overgrown" : "cracked and overgrown";

    /// <summary>
    /// The lines of an animal's history (the History answer, at most 30): "Health:" and the last 10 entries of its health
    /// history a keeper saw (newest first; in symptom words until the illness was named, nothing from before it showed),
    /// "Care:" and the last 7 of its care history, "Production:" and the last 4 of what it made and what was collected of
    /// it (for a species that gives something, given <paramref name="sp"/>), "Days:" and how its last 5 days went (fed,
    /// clean, content, healthy).
    /// </summary>
    public static void History(AnimalRecord a, AnimalRules rules, double now, List<string> lines, AnimalSpeciesData sp = null)
    {
        if (lines == null) return;
        lines.Clear();
        if (a == null) return;
        rules ??= AnimalRules.Defaults();
        lines.Add("Health:");
        int n = 0;
        if (a.healthLog != null)
            for (int i = a.healthLog.Count - 1; i >= 0 && n < 10; i--)
            {
                AnimalHealthEntry e = a.healthLog[i];
                if (e == null || !e.shown || string.IsNullOrWhiteSpace(e.text)) continue;
                lines.Add($"- {Ago(now - e.at)}: {Sentence(e.text)}");
                n++;
            }
        if (n == 0) lines.Add("- Nothing of note.");
        lines.Add("Care:");
        n = 0;
        if (a.careLog != null)
            for (int i = a.careLog.Count - 1; i >= 0 && n < 7; i--)
            {
                AnimalCareEntry e = a.careLog[i];
                if (e == null || string.IsNullOrWhiteSpace(e.text)) continue;
                lines.Add($"- {Ago(now - e.at)}: {Sentence(e.text)}");
                n++;
            }
        if (n == 0) lines.Add("- None yet.");
        if (sp?.products != null && sp.products.Count > 0)
        {
            lines.Add("Production:");
            n = 0;
            bool rare = AnimalProduction.Rare(a);
            if (a.productLog != null)
                for (int i = a.productLog.Count - 1; i >= 0 && n < 4; i--)
                {
                    AnimalProductEntry e = a.productLog[i];
                    AnimalProductSpec p = e != null ? AnimalProduction.Spec(sp, e.product) : null;
                    if (p == null || e.units <= 0) continue;
                    string tier = AnimalProduction.TierWord(AnimalProduction.Tier(e.quality, rare, rules.production));
                    string what = e.kind == AnimalProductEntryKind.Collected ? $"{p.Count(e.units)} collected" : $"{p.Count(e.units)} ready";
                    lines.Add($"- {Ago(now - e.at)}: {Sentence($"{what} ({tier.ToLowerInvariant()})")}");
                    n++;
                }
            if (n == 0) lines.Add("- Nothing yet.");
        }
        lines.Add("Days:");
        n = 0;
        int today = (int)Math.Floor(now / 24.0);
        if (a.days != null)
            for (int i = a.days.Count - 1; i >= 0 && n < 5; i--)
            {
                AnimalDay d = a.days[i];
                if (d == null || d.hours <= 0f) continue;
                lines.Add($"- {DayName(today - d.day)}: {DayWords(d, rules)}.");
                n++;
            }
        if (n == 0) lines.Add("- None recorded yet.");
    }

    /// <summary>"3 hours ago", "Less than an hour ago".</summary>
    private static string Ago(double hours)
    {
        string s = Age(hours) + " ago";
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>A history entry as a sentence (a capital first, a full stop last).</summary>
    private static string Sentence(string text)
    {
        string s = text.Trim();
        if (s.Length == 0) return s;
        s = char.ToUpperInvariant(s[0]) + s.Substring(1);
        char end = s[s.Length - 1];
        return end == '.' || end == '!' || end == '?' ? s : s + ".";
    }

    private static string DayName(int daysAgo) => daysAgo <= 0 ? "Today" : daysAgo == 1 ? "Yesterday" : $"{daysAgo} days ago";

    /// <summary>How a day went, from its means: fed well or short of food, clean or dirty (days kept since Phase 2), content or unsettled, healthy or ill.</summary>
    private static string DayWords(AnimalDay d, AnimalRules rules)
    {
        var parts = new List<string>(4);
        parts.Add(d.starvingHours > 0f || d.satiety < 30f || d.nutrition < rules.malnourishedBelow ? "short of food" : "fed well");
        // A day from a Phase 1 save kept no hygiene (nor comfort, nor welfare): it says nothing of its coat.
        if (d.welfare > 0f || d.hygiene > 0f || d.comfort > 0f) parts.Add(d.hygiene >= 40f ? "clean" : "dirty");
        parts.Add(d.happiness - d.stress * 0.5f >= 30f ? "content" : "unsettled");
        // Ill by what showed: its health, or an illness most of the day past the stages that hide.
        parts.Add(d.health < 60f || d.minHealth < 40f || d.illness >= 20f ? "ill" : "healthy");
        return string.Join(", ", parts);
    }

    /// <summary>"3 days", "5 hours", "1 hour", "less than an hour" (days from 2 on).</summary>
    public static string Age(double hours)
    {
        if (hours < 1.0) return "less than an hour";
        if (hours < 2.0) return "1 hour";
        if (hours < 48.0) return $"{Math.Floor(hours):0} hours";
        return $"{Math.Floor(hours / 24.0):0} days";
    }

    /// <summary>
    /// A wait in words, rounded up so that one told "in 2 hours" may come back then: "less than an hour", "1 hour",
    /// "6 hours", "3 days" (<see cref="Age"/> rounds down, for what is past).
    /// </summary>
    public static string Wait(double hours)
    {
        if (hours < 1.0) return "less than an hour";
        double whole = Math.Ceiling(hours - 1e-9);
        if (whole < 2.0) return "1 hour";
        if (whole < 48.0) return $"{whole:0} hours";
        return $"{Math.Ceiling(hours / 24.0 - 1e-9):0} days";
    }

    /// <summary>
    /// The picture of a pen for one viewer: its animals as seen, the feeder and trough as they look, how clean it is, its
    /// shelter and the weather (<paramref name="weather"/>: the words the caller has from the climate, see
    /// <see cref="WeatherWord(AnimalSimContext, double)"/>), and (for its keepers) their animals elsewhere. The veterinarian's fee
    /// (<paramref name="vetFee"/>; null: there is none) is shown for the viewer's own animals and, where they may care for
    /// others' animals, the others'; "needs a veterinarian" only while there is one (<paramref name="vetAvailable"/>).
    /// </summary>
    public static AnimalPenView PenView(AnimalBook book, AnimalHerd h, string viewer, AnimalRights rights, double now,
        Func<AnimalRecord, AnimalSpeciesData, long> vetFee = null, bool vetAvailable = false, string weather = "")
    {
        IAnimalCatalog cat = book.Catalog;
        AnimalRules rules = book.Rules;
        AnimalPenSpec spec = h.spec ?? new AnimalPenSpec();
        var v = new AnimalPenView
        {
            id = h.pen.id,
            name = spec.name,
            kind = spec.kind,
            area = AnimalUpgrades.Area(h.pen, spec, rules),
            baseArea = spec.area,
            spaceUsed = book.SpaceUsed(h.pen.id),
            rights = (int)rights,
            now = now,
            revision = h.pen.revision,
        };
        float portions = h.pen.FeederPortions;
        v.feederShare = spec.feederCapacity > 0f ? Mathf.Clamp01(portions / spec.feederCapacity) : 0f;
        v.waterShare = spec.waterCapacity > 0f ? Mathf.Clamp01(h.pen.water / spec.waterCapacity) : 0f;
        v.feeder = Feeder(h.pen, cat, v.feederShare);
        float grassMax = spec.GrassMax;
        if (grassMax > 0f)
        {
            v.grassShare = Mathf.Clamp01(h.pen.grass / grassMax);
            v.grass = v.grassShare < 0.1f ? "The grass is grazed bare." : v.grassShare < 0.4f ? "The grass is short and patchy." : v.grassShare < 0.8f ? "There is good grass." : "The grass is lush.";
        }
        v.water = v.waterShare <= 0.001f ? "The trough is dry." : v.waterShare < 0.25f ? "A little water left in the trough." : v.waterShare < 0.75f ? "The trough is half full." : "The trough is full.";

        // How clean it is (a bedded pen never yet laid counts as freshly laid, as it will be at its first step).
        AnimalPenRecord pen = h.pen;
        v.usesBedding = spec.UsesBedding;
        float bedding = pen.bedding >= 0f ? pen.bedding : v.usesBedding ? rules.freshBedding : 0f;
        v.dirtLevel = Level(pen.dirt);
        v.cleanliness = Cleanliness(pen.dirt, spec.Open);
        if (v.usesBedding)
        {
            v.beddingLevel = Level(bedding);
            v.beddingState = BeddingWord(bedding);
        }
        if (pen.water > 0.0001f)
        {
            v.waterLevel = Level(pen.waterDirt);
            v.waterState = WaterWord(pen.waterDirt);
            v.water += $" The water is {v.waterState}.";
        }
        v.shelter = spec.Open ? "Open to the weather." : "Roofed and sheltered.";
        v.weather = weather ?? "";
        bool tend = (rights & AnimalRights.Tend) != 0;
        bool rested = pen.lastCleanedAt < 0.0 || now - pen.lastCleanedAt >= rules.cleanCooldownHours;
        // Straw to lay has its own wait (as the command has).
        bool beddable = pen.lastBeddedAt < 0.0 || now - pen.lastBeddedAt >= rules.beddingCooldownHours;
        v.cleanReady = tend && rested && (pen.dirt >= 3f || (v.usesBedding && bedding < 90f && beddable));
        v.scrubReady = tend && AnimalHabitat.CanScrub(pen, rules) && ((rights & AnimalRights.CareForOthers) != 0 || KeepsAnimalIn(h, viewer));

        foreach (AnimalRecord a in h.animals)
        {
            if (a == null) continue;
            AnimalView av = View(a, cat, now, viewer, rights, vetFee, vetAvailable);
            if (av.mine && a.Alive)
            {
                // Whether it may be crated (its owner only), and what it would be worth.
                AnimalSpeciesData sp = cat.Species(a.species);
                string why = AnimalTrade.WhyNotCrate(a, sp, h, cat, rules);
                av.crateReady = why == null;
                av.crateHint = why ?? "";
                av.grade = AnimalTrade.Grade(a, rules);
                av.worth = AnimalTrade.ValueFactor(a.stage, av.grade, rules);
                av.worthWord = AnimalTrade.GradeWord(av.grade);
            }
            v.animals.Add(av);
        }
        Upgrades(h, spec, rules, viewer, rights, v);
        Waiting(h, cat, viewer, rights, v);
        Partners(h, cat, rules, viewer, now, v);
        // The viewer's animals elsewhere: the living (to bring here, where they may keep animals), and those at the boarding
        // stable (to look over, treat or send the vet for from here; the remains of those that died there, to clear).
        if (!string.IsNullOrEmpty(viewer))
            foreach (AnimalRecord a in book.OwnedBy(viewer))
            {
                if (a.pen == h.pen.id) continue;
                bool boarded = string.IsNullOrEmpty(a.pen);
                bool movable = a.Alive && (rights & AnimalRights.Keep) != 0;
                if (!movable && !boarded) continue;
                AnimalView seen = View(a, cat, now, viewer, rights, boarded ? vetFee : null, vetAvailable);
                // Care is done where it lives (in its pen's window), not from here.
                if (!boarded) seen.care.Clear();
                v.elsewhere.Add(seen);
            }
        return v;
    }

    /// <summary>What waits to be collected in a pen, from the animals the viewer may collect from (owner, or a carer of all).</summary>
    private static void Waiting(AnimalHerd h, IAnimalCatalog cat, string viewer, AnimalRights rights, AnimalPenView v)
    {
        var units = new Dictionary<string, int>();
        var specs = new List<AnimalProductSpec>();
        AnimalProductionRules pr = cat?.Rules?.production;
        bool carer = (rights & AnimalRights.CareForOthers) != 0 && (pr == null || pr.othersCollect);
        foreach (AnimalRecord a in h.animals)
        {
            if (a == null || !a.Alive || (!carer && (string.IsNullOrEmpty(viewer) || a.owner != viewer))) continue;
            AnimalSpeciesData sp = cat.Species(a.species);
            if (sp?.products == null) continue;
            foreach (AnimalProductSpec p in sp.products)
            {
                AnimalProductState st = p != null ? AnimalProduction.State(a, p.id, false) : null;
                int n = st != null ? st.Waiting : 0;
                if (n <= 0) continue;
                if (!units.ContainsKey(p.id))
                {
                    units[p.id] = 0;
                    specs.Add(p);
                }
                units[p.id] += n;
            }
        }
        // What waits in the egg trays for the viewer (a pen's Collect takes it too).
        if (h.pen.tray != null)
            foreach (AnimalTrayLot t in h.pen.tray)
            {
                if (t?.lot == null || t.lot.units <= 0 || !AnimalUpgrades.MayCollect(t, viewer, rights, cat?.Rules)) continue;
                AnimalProductSpec p = AnimalProduction.Spec(cat?.Species(t.species), t.product);
                if (p == null) continue;
                if (!units.ContainsKey(p.id))
                {
                    units[p.id] = 0;
                    specs.Add(p);
                }
                units[p.id] += t.lot.units;
            }
        var parts = new List<string>();
        foreach (AnimalProductSpec p in specs) parts.Add(p.Count(units[p.id]));
        v.productsWaiting = string.Join(", ", parts);
        v.collectReady = parts.Count > 0;
    }

    /// <summary>A pen's level, machines and upkeep, as its picture shows them (Phase 5).</summary>
    private static void Upgrades(AnimalHerd h, AnimalPenSpec spec, AnimalRules rules, string viewer, AnimalRights rights, AnimalPenView v)
    {
        AnimalPenRecord pen = h.pen;
        AnimalUpgradeRules u = AnimalUpgrades.RulesOf(rules);
        v.upgradesOn = u.enabled;
        v.level = AnimalUpgrades.Level(pen, rules);
        v.maxLevel = AnimalUpgrades.MaxLevel(rules);
        v.levelName = AnimalUpgrades.LevelName(v.level, rules);
        v.canBuild = (rights & AnimalRights.Keep) != 0;
        v.canPay = (rights & AnimalRights.Tend) != 0;
        AnimalPenLevelSpec next = AnimalUpgrades.LevelSpec(v.level + 1, rules);
        if (next != null)
        {
            v.nextLevelName = AnimalUpgrades.LevelName(v.level + 1, rules);
            v.nextLevelPrice = AnimalUpgrades.NextLevelPrice(pen, spec, rules);
            v.nextLevelText = $"room +{Mathf.RoundToInt(next.space * 100f)}%, comfort +{next.comfort:0}, dirt -{Mathf.RoundToInt((1f - next.dirt) * 100f)}%, products +{Mathf.RoundToInt(next.production * 100f)}%; upkeep {next.upkeepPerDay * AnimalUpgrades.SizeScale(spec, rules):0.#} a day";
        }
        v.hopper = pen.hopper;
        v.waterer = pen.waterer;
        v.trays = pen.trays;
        v.removeHopper = AnimalUpgrades.MayRemove(pen, AnimalDevice.Hopper, viewer, rights);
        v.removeWaterer = AnimalUpgrades.MayRemove(pen, AnimalDevice.Waterer, viewer, rights);
        v.removeTrays = AnimalUpgrades.MayRemove(pen, AnimalDevice.Trays, viewer, rights);
        v.hopperPrice = AnimalUpgrades.DevicePrice(AnimalDevice.Hopper, rules);
        v.watererPrice = AnimalUpgrades.DevicePrice(AnimalDevice.Waterer, rules);
        v.traysPrice = AnimalUpgrades.DevicePrice(AnimalDevice.Trays, rules);
        v.upkeepPerDay = AnimalUpgrades.UpkeepPerDay(pen, spec, rules);
        v.upkeepOwed = AnimalUpgrades.OwedPrice(pen);
        v.disrepair = AnimalUpgrades.InDisrepair(pen, spec, rules);
        v.upkeepWeekPrice = AnimalUpgrades.PayPrice(pen, spec, rules, AnimalUpgrades.WeekAhead);
        v.paidAheadDays = AnimalUpgrades.PaidAheadDays(pen, spec, rules);
        string stopped = v.disrepair ? " (stopped: upkeep unpaid)" : "";
        if (pen.hopper)
        {
            float held = pen.HopperPortions;
            v.hopperShare = Mathf.Clamp01(held / Mathf.Max(1f, u.hopperCapacity));
            v.hopperText = (held <= 0.05f ? "The hopper is empty." : $"The hopper holds {held:0} of {u.hopperCapacity:0} portions.") + stopped;
        }
        if (pen.waterer) v.watererText = "The waterer keeps the trough topped up." + stopped;
        if (pen.trays)
        {
            v.trayUnits = pen.TrayUnits;
            v.trayCapacity = Math.Max(1, u.trayCapacity);
            v.traysText = (v.trayUnits <= 0 ? "The egg trays are empty." : $"{v.trayUnits} of {v.trayCapacity} in the egg trays.") + stopped;
        }
        if (v.upkeepPerDay > 0f || v.upkeepOwed > 0)
            v.upkeepText = v.disrepair ? $"In disrepair: it owes {v.upkeepOwed} in upkeep ({v.upkeepPerDay:0.#} a day)."
                : v.upkeepOwed > 0 ? $"Upkeep {v.upkeepPerDay:0.#} a day; it owes {v.upkeepOwed}."
                : v.paidAheadDays >= 0.05f ? $"Upkeep {v.upkeepPerDay:0.#} a day; paid {v.paidAheadDays:0.#} days ahead."
                : $"Upkeep {v.upkeepPerDay:0.#} a day.";
    }

    /// <summary>The weather now, in words, for a pen's picture: the context's climate at that hour (none: mild).</summary>
    public static string WeatherWord(AnimalSimContext ctx, double now) => WeatherWord(AnimalCare.WeatherAt(ctx, now));

    /// <summary>The weather in words: "A storm is blowing.", "It is cold and wet.", "It is raining.", "It is freezing.", "It is cold.", "It is hot.", "It is warm.", "Mild weather."</summary>
    public static string WeatherWord(AgriWeather w)
    {
        if (w.storm) return "A storm is blowing.";
        if (w.Raining) return w.temperature < 8f ? "It is cold and wet." : "It is raining.";
        if (w.temperature <= 0f) return "It is freezing.";
        if (w.temperature < 8f) return "It is cold.";
        if (w.temperature > 28f) return "It is hot.";
        if (w.temperature > 24f) return "It is warm.";
        return "Mild weather.";
    }

    /// <summary>How soiled a pen's floor (an open pen's ground) looks: clean, a little soiled, soiled (it wants mucking out), filthy.</summary>
    public static string Cleanliness(float dirt, bool open)
    {
        string floor = open ? "The ground" : "The floor";
        if (dirt < 10f) return $"{floor} is clean.";
        if (dirt < 35f) return $"{floor} is a little soiled.";
        if (dirt < 65f) return $"{floor} is soiled: it wants mucking out.";
        return $"{floor} is filthy.";
    }

    /// <summary>How its bedding looks: fresh, flattened, spent, none.</summary>
    public static string BeddingWord(float bedding) =>
        bedding >= 70f ? "Fresh bedding." : bedding >= 35f ? "The bedding is flattened." : bedding > 5f ? "The bedding is spent." : "No bedding.";

    /// <summary>How its trough water looks: clear, a little murky, murky, foul.</summary>
    public static string WaterWord(float waterDirt) => waterDirt < 15f ? "clear" : waterDirt < 40f ? "a little murky" : waterDirt < 70f ? "murky" : "foul";

    /// <summary>Does the player (a lasting key) own a living animal in the pen?</summary>
    private static bool KeepsAnimalIn(AnimalHerd h, string key)
    {
        if (h == null || string.IsNullOrEmpty(key)) return false;
        foreach (AnimalRecord a in h.animals)
            if (a != null && a.Alive && a.owner == key) return true;
        return false;
    }

    private static string Feeder(AnimalPenRecord pen, IAnimalCatalog cat, float share)
    {
        if (pen.feeder.Count == 0 || share <= 0.001f) return "The feeder is empty.";
        var names = new List<string>();
        foreach (FeederLot l in pen.feeder)
        {
            if (l == null || l.portions <= 0f) continue;
            string n = cat?.Food(l.food)?.DisplayName ?? l.food;
            if (!names.Contains(n)) names.Add(n);
        }
        string how = share < 0.15f ? "A few scraps" : share < 0.5f ? "Some" : share < 0.9f ? "Plenty of" : "A full feeder of";
        return $"{how} {string.Join(", ", names).ToLowerInvariant()}.";
    }
}
