using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One meal or drink given by a hand (the animal's feeding history keeps the last ones).</summary>
[Serializable]
public class AnimalFeeding
{
    public double at;
    public string food = "";
    public float portions;
    [Range(0f, 1f)] public float quality;
    public FoodAffinity affinity;
    [Tooltip("Who gave it (their lasting key); empty: it ate from the feeder.")]
    public string by = "";
}

/// <summary>
/// How one game day went for an animal (the care history the quality of its products is judged by, Phase 3: a cow does
/// not give premium milk for one good meal before milking). Means are over the hours it lived that day.
/// </summary>
[Serializable]
public class AnimalDay
{
    public int day;
    public float hours;
    public float satiety;
    public float hydration;
    public float nutrition;
    public float health;
    public float minHealth = 100f;
    public float happiness;
    public float stress;
    [Tooltip("What it ate that day in satiety points (100 = a full stomach), and of it its favourites, and harmful or toxic food.")]
    public float eaten, favorite, bad;
    [Tooltip("Hours it spent starving or parched.")]
    public float starvingHours, parchedHours;
    [Tooltip("Hours its owner was away (logged out).")]
    public float ownerAwayHours;

    // ---------------------------------------------------------------- Phase 2: care, welfare and health
    [Tooltip("Means over the day of how clean, comfortable and well it was (welfare: see AnimalWelfare).")]
    public float hygiene, comfort, welfare;
    [Tooltip("Its lowest welfare that day.")]
    public float minWelfare = 100f;
    [Tooltip("Mean severity of its worst illness that day (0: healthy).")]
    public float illness;
    [Tooltip("Care it was given that day (brushing, washing, petting, hooves, mucking out, medicine), and of it by its owner.")]
    public int cares, ownerCares;
    [Tooltip("Affinity with its owner care gave it that day (care gives at most the rules' Affinity Per Day).")]
    public float affinityGained;
}

/// <summary>
/// How an animal has lived over the long run (slow averages over the rules' Life Hours): what its remains are judged by
/// (and, Phase 3, its products). Primed from its state the first time it is used.
/// </summary>
[Serializable]
public class AnimalLife
{
    [Tooltip("Set once the averages started from its state (a new or Phase 1 animal starts fair, not at the field defaults).")]
    public bool primed;
    public float welfare = 60f, nutrition = 70f, health = 100f, hygiene = 80f, comfort = 60f;
    [Tooltip("Mean severity of its worst condition.")]
    public float illness;
    [Tooltip("Game hours it spent ill (worst severity 15 or more), starving (satiety 0), and lived in all.")]
    public float illHours, starvedHours, hours;
}

/// <summary>Something that happened to an animal, kept in its history.</summary>
[Serializable]
public class AnimalEvent
{
    public double at;
    public AnimalEventKind kind;
    public string text = "";
    [Tooltip("Who did it (a lasting key), if anyone.")]
    public string by = "";
}

/// <summary>
/// One animal: its identity, owner, life and condition. It is an individual that persists - saved with the world, never
/// copied (its id is unique in the world and never reused), changed only through the <see cref="AnimalBook"/> where the
/// rules run. Every number that matters (genes, nutrition, toxins) stays on the server: clients receive what can be seen.
/// <para>Time is game hours (<c>FarmClock.Now</c>). <see cref="livedTo"/> is how far its life has been worked out: the
/// simulation brings it up to the present in steps (<see cref="AnimalSim"/>), so an animal nobody watched for days lives
/// those days when it is next looked at - the same days a watched one would have lived.</para>
/// </summary>
[Serializable]
public class AnimalRecord
{
    // ---------------------------------------------------------------- identity
    [Tooltip("Unique in the world, never reused.")]
    public string id = "";
    [Tooltip("The owner's lasting key (the housing books' key of the player).")]
    public string owner = "";
    [Tooltip("The owner's name when last seen (for the window).")]
    public string ownerName = "";
    public string species = "";
    public string breed = "";
    public string name = "";
    public AnimalSex sex;
    public AnimalGenes genes = new AnimalGenes();
    [Tooltip("Game hour it was born.")]
    public double bornAt;
    [Tooltip("Game hour its owner got it.")]
    public double acquiredAt;
    [Tooltip("How it came: \"bought\", \"born\", \"traded\"...")]
    public string origin = "";
    [Tooltip("The habitat it lives in (a pen id); empty while boarded.")]
    public string pen = "";

    // ---------------------------------------------------------------- life
    public AnimalStatus status = AnimalStatus.Alive;
    public AnimalDeathCause deathCause;
    public double diedAt = -1.0;
    [Tooltip("Game hour its life has been worked out to.")]
    public double livedTo;
    [Tooltip("Game hours of growth it has had (a well kept animal one an hour, a poorly kept one less).")]
    public float development;
    public GrowthStage stage;

    // ---------------------------------------------------------------- condition (0-100)
    public float health = 100f;
    [Tooltip("How full it is (hunger is 100 minus it).")]
    public float satiety = 70f;
    public float hydration = 80f;
    [Tooltip("Its nutritional condition: how well its needs were met over the last days.")]
    public float nutrition = 70f;
    public float happiness = 60f;
    public float stress = 10f;
    public float affinity;
    [Tooltip("How clean its coat or plumage is (0-100): it settles toward a resting level its pen allows; care lifts it above.")]
    public float hygiene = 80f;
    [Tooltip("How comfortable it is (0-100): weather and shelter, crowding, the floor, bedding, hooves, coat and illness.")]
    public float comfort = 60f;
    [Tooltip("Toxins in its body.")]
    public float toxin;
    [Tooltip("Digestive upset (0-100).")]
    public float upset;

    // ---------------------------------------------------------------- nutrition memory
    [Tooltip("Nutrients taken in, fading over the nutrition window.")]
    public NutrientProfile intake;
    [Tooltip("Nutrients needed, fading the same way: intake / need is how well each need was met.")]
    public NutrientProfile need;
    [Tooltip("Satiety recorded before the first step ever: the first days count from a fair start.")]
    public bool primed;

    // ---------------------------------------------------------------- history
    public double lastFedAt = -1.0, lastWateredAt = -1.0;
    public List<AnimalFeeding> feedings = new List<AnimalFeeding>();
    public List<AnimalDay> days = new List<AnimalDay>();
    public List<AnimalEvent> events = new List<AnimalEvent>();
    [Tooltip("Bumped by every change clients could see (network pictures and the save follow it).")]
    public int revision;

    // ---------------------------------------------------------------- care, welfare and health (Phase 2)
    [Tooltip("Hoof condition (100 = sound; species without hooves stay at 100).")]
    public float hooves = 100f;
    [Tooltip("Its welfare (0-100) at its last step (see AnimalWelfare): the band it is in changes how it lives.")]
    public float welfare = 60f;
    [Tooltip("How well it digests now (its conditions): food feeds it by this.")]
    public float absorbFactor = 1f;
    [Tooltip("What made it least comfortable at its last step (its largest comfort penalty).")]
    public AnimalDiscomfort discomfort;
    [Tooltip("What stressed it most at its last step.")]
    public AnimalDiscomfort stressor;
    [Tooltip("Its illnesses now (active or recovering).")]
    public List<AnimalCondition> conditions = new List<AnimalCondition>();
    [Tooltip("The build-up of each illness's causes (no entry: none).")]
    public List<AnimalExposure> exposures = new List<AnimalExposure>();
    [Tooltip("When each care action was last done to it, and how much of it it had lately.")]
    public List<AnimalCareMark> care = new List<AnimalCareMark>();
    [Tooltip("Its care history (the last ones).")]
    public List<AnimalCareEntry> careLog = new List<AnimalCareEntry>();
    [Tooltip("Its health history (the last ones).")]
    public List<AnimalHealthEntry> healthLog = new List<AnimalHealthEntry>();
    [Tooltip("Its last dose of each medicine that did its good (when the next may do good again).")]
    public List<AnimalDose> doses = new List<AnimalDose>();
    public double lastExaminedAt = -1.0, lastVetAt = -1.0, lastOwnerCareAt = -1.0;
    [Tooltip("Game hour its owner was last told it needs help (at most once a game day).")]
    public double lastNeedsHelpAt = -1.0;
    [Tooltip("The condition that killed it (empty: none).")]
    public string deathCondition = "";
    [Tooltip("How it lived, over the long run (what its remains are judged by).")]
    public AnimalLife life = new AnimalLife();

    // ---------------------------------------------------------------- production (Phase 3)
    [Tooltip("Its making of each product its species gives: the cycle under way, and what waits to be collected.")]
    public List<AnimalProductState> products = new List<AnimalProductState>();
    [Tooltip("Its production history (the last ones): lots made and collected.")]
    public List<AnimalProductEntry> productLog = new List<AnimalProductEntry>();

    // ---------------------------------------------------------------- family, breeding and relationships (Phase 4)
    [Tooltip("Its parents (ids; empty: unknown, as for one bought), and their names when it was born.")]
    public string mother = "", father = "";
    public string motherName = "", fatherName = "";
    [Tooltip("Its grandparents: its mother's mother and father, its sire's mother and father (empty: unknown).")]
    public List<string> grandparents = new List<string>();
    [Tooltip("Generations bred on the farm (0: bought; a young is one past its elder parent).")]
    public int generation;
    [Tooltip("A mother's young on the way (active: she is expecting).")]
    public AnimalPregnancy pregnancy = new AnimalPregnancy();
    [Tooltip("Game hour from which it may breed again (after a birth, a mating, or a mating that did not take).")]
    public double breedReadyAt;
    public double lastBredAt = -1.0;
    [Tooltip("Matings tried (as a mother): each roll is a hash of them.")]
    public int breedAttempts;
    [Tooltip("Young it has had (as mother or sire).")]
    public int offspring;
    [Tooltip("Its bonds with other animals: companions, kin, mates and rivals.")]
    public List<AnimalBond> bonds = new List<AnimalBond>();
    [Tooltip("Game hour its owner was last near its pen (their time together keeps its bond from fading).")]
    public double lastOwnerNearAt = -1.0;
    [Tooltip("The game day, and the affinity its owner's time near its pen gave it that day (at most the rules' Together Per Day).")]
    public int togetherDay = -1;
    public float togetherGiven;

    // ---------------------------------------------------------------- derived
    public bool Alive => status != AnimalStatus.Dead;
    public double AgeHours(double now) => Math.Max(0.0, (status == AnimalStatus.Dead && diedAt >= 0.0 ? diedAt : now) - bornAt);
    public float Hunger => 100f - satiety;

    /// <summary>Its condition of this id (active or recovering), or null.</summary>
    public AnimalCondition Condition(string id)
    {
        if (conditions == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < conditions.Count; i++)
            if (conditions[i] != null && string.Equals(conditions[i].id, id, StringComparison.OrdinalIgnoreCase)) return conditions[i];
        return null;
    }

    /// <summary>Its build-up of the causes of this condition, or null.</summary>
    public AnimalExposure Exposure(string condition)
    {
        if (exposures == null || string.IsNullOrEmpty(condition)) return null;
        for (int i = 0; i < exposures.Count; i++)
            if (exposures[i] != null && string.Equals(exposures[i].condition, condition, StringComparison.OrdinalIgnoreCase)) return exposures[i];
        return null;
    }

    /// <summary>The last dose of this treatment it was given, or null.</summary>
    public AnimalDose LastDose(string treatment)
    {
        if (doses == null || string.IsNullOrEmpty(treatment)) return null;
        for (int i = doses.Count - 1; i >= 0; i--)
            if (doses[i] != null && string.Equals(doses[i].treatment, treatment, StringComparison.OrdinalIgnoreCase)) return doses[i];
        return null;
    }

    /// <summary>Adds an entry to its care history (the last <paramref name="keep"/> are kept).</summary>
    public void Cared(AnimalCareEntry e, int keep)
    {
        if (e == null) return;
        careLog ??= new List<AnimalCareEntry>();
        careLog.Add(e);
        if (careLog.Count > Math.Max(1, keep)) careLog.RemoveRange(0, careLog.Count - Math.Max(1, keep));
    }

    /// <summary>Adds an entry to its health history (the last <paramref name="keep"/> are kept).</summary>
    public void LogHealth(AnimalHealthEntry e, int keep)
    {
        if (e == null) return;
        healthLog ??= new List<AnimalHealthEntry>();
        healthLog.Add(e);
        if (healthLog.Count > Math.Max(1, keep)) healthLog.RemoveRange(0, healthLog.Count - Math.Max(1, keep));
    }

    /// <summary>
    /// Records a dose given: it takes the place of the last dose of the same medicine, so doses of others never make it
    /// forget one (of more than <paramref name="keep"/> medicines, at least 16, the longest ago are let go).
    /// </summary>
    public void Dosed(AnimalDose d, int keep)
    {
        if (d == null) return;
        doses ??= new List<AnimalDose>();
        doses.RemoveAll(x => x == null || string.Equals(x.treatment, d.treatment, StringComparison.OrdinalIgnoreCase));
        doses.Add(d);
        int most = Math.Max(16, keep);
        if (doses.Count > most) doses.RemoveRange(0, doses.Count - most);
    }

    public AnimalDay Today(int day)
    {
        AnimalDay last = days.Count > 0 ? days[days.Count - 1] : null;
        if (last != null && last.day == day) return last;
        var d = new AnimalDay { day = day };
        days.Add(d);
        return d;
    }

    public void Log(AnimalEventKind kind, double at, string text, string by, int keep)
    {
        events.Add(new AnimalEvent { at = at, kind = kind, text = text ?? "", by = by ?? "" });
        if (events.Count > keep) events.RemoveRange(0, events.Count - keep);
        revision++;
    }

    public void Fed(AnimalFeeding f, int keep)
    {
        feedings.Add(f);
        if (feedings.Count > keep) feedings.RemoveRange(0, feedings.Count - keep);
    }

    public void TrimDays(int keep)
    {
        if (days.Count > keep) days.RemoveRange(0, days.Count - keep);
    }

    public AnimalRecord Clone()
    {
        string json = JsonUtility.ToJson(this);
        return JsonUtility.FromJson<AnimalRecord>(json);
    }
}

/// <summary>
/// The lasting state of one habitat (a pen): its feeder and its water. The pen's shape, kind and size are the scene's
/// (the <c>AnimalPen</c> component); what is in it is the world's, saved with the animals.
/// </summary>
[Serializable]
public class AnimalPenRecord
{
    public string id = "";
    [Tooltip("The scene it is in (pens are named by scene and id).")]
    public string scene = "";
    [Tooltip("The property it stands on, if any (its tenant and those they let in work it).")]
    public string property = "";
    public List<FeederLot> feeder = new List<FeederLot>();
    [Tooltip("Litres in its water trough.")]
    [Min(0f)] public float water;
    [Tooltip("Portions of standing grass (a pasture).")]
    [Min(0f)] public float grass;
    [Tooltip("The kind of water in it (WaterSourceKind).")]
    public int waterKind;
    [Tooltip("Game hour its feeder and water were worked out to.")]
    public double livedTo;
    public int revision;
    [Tooltip("What the scene last said of it (size, stations): it goes on living while its scene is not loaded.")]
    public AnimalPenSpec spec;

    // ---------------------------------------------------------------- Phase 2: how clean it is
    [Tooltip("How soiled its floor is (droppings, soiled bedding), 0-100.")]
    [Range(0f, 100f)] public float dirt;
    [Tooltip("Fresh bedding, 0-100 (-1: not laid yet: set from the pen when the world first knows it, or at its first step).")]
    public float bedding = -1f;
    [Tooltip("How fouled its trough water is, 0-100.")]
    [Range(0f, 100f)] public float waterDirt;
    [Tooltip("Worm eggs in its ground (a pasture), 0-100.")]
    [Range(0f, 100f)] public float parasites;
    [Tooltip("Game hour it was last mucked out (-1: never), and by whom (a lasting key).")]
    public double lastCleanedAt = -1.0;
    public string cleanedBy = "";
    [Tooltip("Game hour fresh straw was last laid (-1: never): laying straw alone has its own wait.")]
    public double lastBeddedAt = -1.0;
    [Tooltip("Game hour mucking out last disturbed its animals (at most once in the rules' Disturb Hours).")]
    public double lastDisturbedAt = -1.0;
    [Tooltip("The share of a manure item forked up but not yet given (the whole part is given; the rest waits).")]
    [Min(0f)] public float manureOwed;

    // ---------------------------------------------------------------- Phase 5: its level, its machines, its upkeep
    [Tooltip("Its upgrade level (1: as built; see the rules' Upgrades).")]
    [Min(1)] public int level = 1;
    [Tooltip("Fitted with a feed hopper, an automatic waterer, egg trays.")]
    public bool hopper, waterer, trays;
    [Tooltip("Who fitted each machine (a lasting key): they, or one who may care for others' animals here, may take it out.")]
    public string hopperBy = "", watererBy = "", traysBy = "";
    [Tooltip("The feed in its hopper (sealed: it ages more slowly than in the open feeder).")]
    public List<FeederLot> hopperFeed = new List<FeederLot>();
    [Tooltip("What waits in its egg trays, each lot with its origin.")]
    public List<AnimalTrayLot> tray = new List<AnimalTrayLot>();
    [Tooltip("Upkeep it owes (money; it falls due by the game day). Below 0: paid ahead.")]
    public float upkeepOwed;
    [Tooltip("Game hour it was last improved or fitted (-1: never), and by whom (a lasting key).")]
    public double upgradedAt = -1.0;
    public string upgradedBy = "";

    public float HopperPortions
    {
        get
        {
            float n = 0f;
            if (hopperFeed != null) foreach (FeederLot l in hopperFeed) if (l != null) n += Mathf.Max(0f, l.portions);
            return n;
        }
    }

    public int TrayUnits
    {
        get
        {
            int n = 0;
            if (tray != null) foreach (AnimalTrayLot t in tray) if (t?.lot != null) n += Math.Max(0, t.lot.units);
            return n;
        }
    }

    public float FeederPortions
    {
        get
        {
            float n = 0f;
            foreach (FeederLot l in feeder) if (l != null) n += Mathf.Max(0f, l.portions);
            return n;
        }
    }
}

/// <summary>The animals of one world, as saved (one file per world where the rules run).</summary>
[Serializable]
public class AnimalWorldSave
{
    /// <summary>
    /// 5: Phase 5 (crated animals, pen levels, machines and upkeep); 4: Phase 4 (breeding and relationships); 3: Phase 3
    /// (production and the product ledger); 2: Phase 2 (care, welfare, health, habitat cleanliness). An older save loads
    /// with the new fields at their defaults.
    /// </summary>
    public const int CurrentVersion = 5;

    public int version = CurrentVersion;
    public string world = "";
    public long savedUtc;
    [Tooltip("The game hour of the save.")]
    public double now;
    [Tooltip("Counts every animal ever registered in the world (ids are never reused).")]
    public long serial;
    public List<AnimalRecord> animals = new List<AnimalRecord>();
    public List<AnimalPenRecord> pens = new List<AnimalPenRecord>();
    [Tooltip("Requests already done, by player and number (a retried request is answered, never done twice).")]
    public List<AnimalDoneRequest> done = new List<AnimalDoneRequest>();
    [Tooltip("Where every product lot collected came from (Phase 3).")]
    public AnimalProductLedger ledger = new AnimalProductLedger();
    [Tooltip("Animals in crates, waiting to be let out (Phase 5), and the last crate serial given.")]
    public List<AnimalCrated> crated = new List<AnimalCrated>();
    public long crateSerial;
}

/// <summary>A request that was done: a retry of it (after a lost answer, a reconnect, a crash) is answered without doing it again.</summary>
[Serializable]
public class AnimalDoneRequest
{
    public string player = "";
    public int seq;
    public string token = "";
    public double at;
    public bool ok;
    public string message = "";
    public string animal = "";
}
