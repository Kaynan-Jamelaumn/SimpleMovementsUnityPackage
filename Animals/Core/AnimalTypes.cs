using System;
using UnityEngine;

/// <summary>An animal's sex (None: a species that is not sexed, a colony).</summary>
public enum AnimalSex
{
    None,
    Female,
    Male,
}

/// <summary>Where an animal is in its life. A species uses the stages it lists, in this order (not every species uses them all).</summary>
public enum GrowthStage
{
    Newborn,
    Baby,
    Juvenile,
    Adolescent,
    Adult,
    Senior,
}

/// <summary>Is the animal alive, and where.</summary>
public enum AnimalStatus
{
    /// <summary>Living in a habitat (a pen).</summary>
    Alive,
    /// <summary>Living, but kept at the boarding stable: its owner can no longer reach its pen (the lease ended). It is fed a plain diet until it is brought home.</summary>
    Boarded,
    /// <summary>Dead: its record stays (history, cause) and its remains are in its pen until they are dealt with.</summary>
    Dead,
    /// <summary>Phase 5: in a crate (to be sold or traded), out of the world: it lives again from the moment it is let out.</summary>
    Crated,
}

/// <summary>Why an animal died.</summary>
public enum AnimalDeathCause
{
    None,
    Starvation,
    Dehydration,
    Poisoning,
    Malnutrition,
    OldAge,
    Illness,
    Injury,
    Slaughtered,
}

/// <summary>
/// What a food is to one species (its diet decides). The order matters: lower is better. <see cref="Inedible"/> foods are
/// refused outright (meat for a cow).
/// </summary>
public enum FoodAffinity
{
    /// <summary>Loved: happier, a little better absorbed.</summary>
    Favorite,
    /// <summary>Good for it: full nutritional value.</summary>
    Preferred,
    /// <summary>Acceptable: keeps it fed.</summary>
    Normal,
    /// <summary>Fills it, nothing more.</summary>
    Neutral,
    /// <summary>Fills it but feeds it badly (unbalanced or too little of what it needs).</summary>
    Poor,
    /// <summary>Upsets its digestion: eaten only when hungry, it absorbs less for a while.</summary>
    Harmful,
    /// <summary>Poisons it: eaten only when starving (or from a careless hand), it must then be watched and treated.</summary>
    Toxic,
    /// <summary>Not food for it: refused.</summary>
    Inedible,
}

/// <summary>The nutrients an animal needs (one channel each in <see cref="NutrientProfile"/>).</summary>
public enum AnimalNutrient
{
    /// <summary>Energy: grain, roots, fruit.</summary>
    Energy,
    /// <summary>Protein: legumes, clover, insects, meal.</summary>
    Protein,
    /// <summary>Fibre: grass, hay, straw (grazers cannot do without it).</summary>
    Fiber,
    /// <summary>Minerals: grit, greens, a lick.</summary>
    Minerals,
    /// <summary>Vitamins: fresh greens, fruit, vegetables.</summary>
    Vitamins,
}

/// <summary>
/// Amounts of the five nutrients (per portion of a food, a species' need per game hour, or how well a need is met). All
/// channels are plain floats so the struct serializes with JsonUtility and copies cheaply.
/// </summary>
[Serializable]
public struct NutrientProfile
{
    public const int Count = 5;

    [Min(0f)] public float energy;
    [Min(0f)] public float protein;
    [Min(0f)] public float fiber;
    [Min(0f)] public float minerals;
    [Min(0f)] public float vitamins;

    public NutrientProfile(float energy, float protein, float fiber, float minerals, float vitamins)
    {
        this.energy = energy; this.protein = protein; this.fiber = fiber; this.minerals = minerals; this.vitamins = vitamins;
    }

    public float this[int i]
    {
        get
        {
            switch (i)
            {
                case 0: return energy;
                case 1: return protein;
                case 2: return fiber;
                case 3: return minerals;
                default: return vitamins;
            }
        }
        set
        {
            switch (i)
            {
                case 0: energy = value; break;
                case 1: protein = value; break;
                case 2: fiber = value; break;
                case 3: minerals = value; break;
                default: vitamins = value; break;
            }
        }
    }

    public float this[AnimalNutrient n] { get => this[(int)n]; set => this[(int)n] = value; }

    public float Sum => energy + protein + fiber + minerals + vitamins;

    public static NutrientProfile operator *(NutrientProfile a, float k) => new NutrientProfile(a.energy * k, a.protein * k, a.fiber * k, a.minerals * k, a.vitamins * k);

    public static NutrientProfile operator +(NutrientProfile a, NutrientProfile b) =>
        new NutrientProfile(a.energy + b.energy, a.protein + b.protein, a.fiber + b.fiber, a.minerals + b.minerals, a.vitamins + b.vitamins);

    public static NutrientProfile All(float v) => new NutrientProfile(v, v, v, v, v);
}

/// <summary>
/// What kind of place a habitat is. A species lists the kinds it can live in (<see cref="AnimalHabitatNeeds.kinds"/>); a
/// pen is one kind.
/// </summary>
[Flags]
public enum AnimalHabitatKind
{
    None = 0,
    /// <summary>Open fenced grass: cows, sheep, goats, horses.</summary>
    Pasture = 1,
    /// <summary>A coop with a run: chickens, ducks.</summary>
    Coop = 2,
    /// <summary>A barn: cows, goats, pigs, sheep (winter quarters).</summary>
    Barn = 4,
    /// <summary>A stable: horses.</summary>
    Stable = 8,
    /// <summary>A sty: pigs.</summary>
    Sty = 16,
    /// <summary>A hutch or small enclosure: rabbits.</summary>
    Hutch = 32,
    /// <summary>A pond with a bank: ducks, geese.</summary>
    Pond = 64,
    /// <summary>A beehive (colonies; Phase 5).</summary>
    Hive = 128,
    /// <summary>A magical habitat (magical species).</summary>
    Magical = 256,
}

/// <summary>What is recorded in an animal's history.</summary>
public enum AnimalEventKind
{
    Acquired,
    Born,
    StageReached,
    Fed,
    Refused,
    Watered,
    Poisoned,
    Upset,
    Starving,
    Thirsty,
    Recovered,
    Renamed,
    Moved,
    Boarded,
    Rehomed,
    OwnerChanged,
    Died,
    // Phase 2 (care, welfare and health). Saved and sent as numbers: add new kinds at the END only.
    /// <summary>Brushed, washed, petted, its hooves cleaned (the care history keeps these; not the event log).</summary>
    Cared,
    /// <summary>An illness showed (the owner learns a symptom, not its name).</summary>
    FellIll,
    /// <summary>An illness reached its worst stage.</summary>
    Worsened,
    Diagnosed,
    Treated,
    /// <summary>It got over an illness.</summary>
    Healed,
    SeenByVet,
    Slaughtered,
    /// <summary>Its welfare fell to Critical or its health very low (at most once a game day).</summary>
    NeedsHelp,
    // Phase 3 (production).
    /// <summary>What it gives filled its storage (a nest, an udder, a full fleece): its owner is told (not kept in its history).</summary>
    ProductsReady,
    /// <summary>Its products were collected (by someone other than its owner, its owner is told).</summary>
    Collected,
    // Phase 4 (breeding and relationships).
    /// <summary>It was bred (whether it took or not).</summary>
    Mated,
    /// <summary>She gave birth (or her clutch hatched).</summary>
    GaveBirth,
    /// <summary>She lost her young.</summary>
    Miscarried,
    /// <summary>It misses a close companion it was parted from.</summary>
    MissesCompanion,
    // Phase 5 (trading).
    /// <summary>It was put in a crate (to be sold or traded).</summary>
    Crated,
    /// <summary>It changed hands: let out of a crate by a new owner.</summary>
    ChangedHands,
}

/// <summary>
/// The family of a care action (its defaults: label, verb, message, icon). A species' care actions are data, named by id
/// ("brush", "wash", "pet", "clean_hooves" or a species' own); <see cref="Custom"/> is any other. Saved as numbers: add at the END.
/// </summary>
public enum AnimalCareKind
{
    Brush,
    Wash,
    Pet,
    CleanHooves,
    Custom,
}

/// <summary>How well an animal lives, in five bands (each changes the simulation: see <see cref="AnimalWelfareBandEffects"/>).</summary>
public enum AnimalWelfareBand
{
    Critical,
    Poor,
    Average,
    Good,
    Excellent,
}

/// <summary>What kind of illness a condition is (what treats it, what it does to the remains). Saved as numbers: add at the END.</summary>
public enum AnimalConditionKind
{
    Malnutrition,
    Dehydration,
    Infection,
    Digestive,
    Parasites,
    Stress,
    Injury,
    Poisoning,
}

/// <summary>
/// What makes an animal ill. Each is measured 0-1 (a few may reach 1.5) every step from what the animal lives through
/// (see <see cref="AnimalHealth.Causes"/>). Saved as numbers: add at the END.
/// </summary>
public enum AnimalCause
{
    None,
    Malnourished,
    Parched,
    Toxins,
    Upset,
    DirtyHabitat,
    DirtyWater,
    LowHygiene,
    Wound,
    Chill,
    Heat,
    Crowding,
    Loneliness,
    HighStress,
    HoofNeglect,
    Worms,
    Contagion,
    Fighting,
}

/// <summary>What an entry of an animal's health history records. Saved as numbers: add at the END.</summary>
public enum AnimalHealthEventKind
{
    Onset,
    Showed,
    Worsened,
    Improved,
    Diagnosed,
    Treated,
    Vet,
    Recovering,
    Cleared,
    Relapsed,
    Died,
}

/// <summary>How a medicine is given: poured down its throat, put on its skin or hooves, or mixed into its feed.</summary>
public enum AnimalTreatmentRoute
{
    Drench,
    Topical,
    Feed,
}

/// <summary>How an animal's body looks, for its body in the world (flags; sent to clients as an int). Add new flags at the END.</summary>
[Flags]
public enum AnimalLooks
{
    None = 0,
    Dirty = 1,
    Sick = 2,
    Lame = 4,
    Stressed = 8,
    Thin = 16,
    /// <summary>An Excellent-band animal: calmer idling, more lying in the sun.</summary>
    Content = 32,
}

/// <summary>
/// Why an animal is uncomfortable or stressed, as a keeper could tell (the largest comfort penalty and the largest stress
/// source of its last step: see <see cref="AnimalRecord.discomfort"/>). Saved as numbers: add at the END.
/// </summary>
public enum AnimalDiscomfort
{
    None,
    Cold,
    Heat,
    Rain,
    Crowding,
    Dirt,
    Bedding,
    Hooves,
    Coat,
    Illness,
    Loneliness,
    Handling,
}

/// <summary>Small deterministic helpers of the animal rules (no UnityEngine randomness: every machine and every replay agree).</summary>
public static class AnimalMath
{
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    public static float Clamp100(float v) => v < 0f ? 0f : v > 100f ? 100f : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    /// <summary>0 below a, 1 above b, smooth between.</summary>
    public static float Smooth(float a, float b, float v)
    {
        if (Math.Abs(b - a) < 1e-9f) return v >= b ? 1f : 0f;
        float t = Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>How much of an amount is left after <paramref name="hours"/> with a time constant of <paramref name="tau"/> hours.</summary>
    public static float Decay(double hours, double tau) => tau <= 0.0 ? 0f : (float)Math.Exp(-Math.Max(0.0, hours) / tau);

    public static uint Mix(uint h)
    {
        h ^= h >> 16;
        h *= 0x7feb352d;
        h ^= h >> 15;
        h *= 0x846ca68b;
        h ^= h >> 16;
        return h;
    }

    public static uint Hash(int a, int b = 0, int c = 0, int d = 0)
    {
        uint h = Mix((uint)a + 0x9e3779b9u);
        h = Mix(h ^ ((uint)b + 0x85ebca6bu));
        h = Mix(h ^ ((uint)c + 0xc2b2ae35u));
        h = Mix(h ^ ((uint)d + 0x27d4eb2fu));
        return h;
    }

    /// <summary>A stable hash of a string (the same on every machine and run - not <c>string.GetHashCode</c>).</summary>
    public static int Hash(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        unchecked
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return (int)Mix(h);
        }
    }

    /// <summary>A number in [0, 1) from the inputs.</summary>
    public static float Noise(int a, int b = 0, int c = 0, int d = 0) => (Hash(a, b, c, d) >> 8) * (1f / 16777216f);

    /// <summary>A number in (-1, 1) from the inputs.</summary>
    public static float Signed(int a, int b = 0, int c = 0, int d = 0) => Noise(a, b, c, d) * 2f - 1f;

    /// <summary>A roughly normal number (mean 0, deviation about 1) from the inputs.</summary>
    public static float Gauss(int a, int b = 0, int c = 0)
    {
        float s = 0f;
        for (int i = 0; i < 4; i++) s += Noise(a, b, c, 101 + i);
        return (s - 2f) * 1.732f;
    }
}
