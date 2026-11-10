using System;
using System.Collections.Generic;
using UnityEngine;

// ==================================================================================================== data: illnesses
/// <summary>One cause of an illness and how much it counts (exposure per game hour = the sum of weight x cause).</summary>
[Serializable]
public class AnimalConditionCause
{
    public AnimalCause cause;
    [Min(0f)] public float weight = 1f;
}

/// <summary>A stage of an illness, from a severity on: whether it shows, its symptoms, and everything it does (every stage lists every effect).</summary>
[Serializable]
public class AnimalConditionStage
{
    [Tooltip("Its name (\"Mild\").")]
    public string name = "";
    [Tooltip("Severity (0-100) where it starts.")]
    [Range(0f, 100f)] public float from;
    [Tooltip("It shows (symptoms a keeper can see). A hidden stage is found only by the veterinarian.")]
    public bool visible = true;
    [Tooltip("What a keeper sees (the first is what the owner is told).")]
    public List<string> symptoms = new List<string>();
    [Tooltip("Health lost per game hour.")]
    [Min(0f)] public float healthPerHour;
    [Tooltip("Added to its comfort target (negative: less comfortable).")]
    public float comfort;
    [Tooltip("Added to its stress target.")]
    public float stress;
    [Tooltip("Added to its happiness target.")]
    public float happiness;
    [Tooltip("Hygiene lost per game hour (loose droppings).")]
    [Min(0f)] public float hygienePerHour;
    [Tooltip("x how much it eats.")]
    [Range(0f, 2f)] public float appetite = 1f;
    [Tooltip("x how fast it grows.")]
    [Range(0f, 2f)] public float growth = 1f;
    [Tooltip("x how well its food feeds it.")]
    [Range(0f, 2f)] public float absorption = 1f;
    [Tooltip("x what it gives (Phase 3).")]
    [Range(0f, 2f)] public float production = 1f;
    [Tooltip("How well it moves (1 = sound; a lame animal less).")]
    [Range(0f, 1f)] public float mobility = 1f;

    /// <summary>Its first symptom (what the owner is told), or "".</summary>
    public string Symptom
    {
        get
        {
            if (symptoms == null) return "";
            for (int i = 0; i < symptoms.Count; i++)
                if (!string.IsNullOrWhiteSpace(symptoms[i])) return symptoms[i];
            return "";
        }
    }

    public AnimalConditionStage Clone()
    {
        var s = (AnimalConditionStage)MemberwiseClone();
        s.symptoms = symptoms != null ? new List<string>(symptoms) : new List<string>();
        return s;
    }
}

/// <summary>
/// An illness, as data: what builds it up (its causes), when it takes hold, how it worsens and mends, when it needs
/// treatment or a veterinarian, its stages with their symptoms and effects, how long recovery takes, immunity after it,
/// and whether it is catching. The simulation runs it (<see cref="AnimalHealth.Step"/>); nothing about an illness is in code.
/// </summary>
[Serializable]
public class AnimalConditionData
{
    public string id = "";
    public string name = "";
    [TextArea(2, 4)] public string description = "";
    public AnimalConditionKind kind;
    [Tooltip("What builds it up, and how much each counts (exposure per game hour = sum of weight x cause).")]
    public List<AnimalConditionCause> causes = new List<AnimalConditionCause>();
    [Tooltip("Exposure at which it takes hold (each animal a little either side: its genes).")]
    [Min(0.01f)] public float onsetAt = 20f;
    [Tooltip("Game hours over which exposure builds and fades (a cause must last about this long to count).")]
    [Min(0.1f)] public float exposureHours = 36f;
    [Tooltip("Severity it starts at.")]
    [Range(0f, 100f)] public float startSeverity = 5f;
    [Tooltip("Severity gained per game hour while its causes go on twice as strongly as it takes to start it (they set how far it goes: a weak cause plateaus at Moderate).")]
    public float worsenPerHour = 2f;
    [Tooltip("Severity per game hour once its causes are gone (negative: it mends by itself).")]
    public float courseWithoutCause = -2f;
    [Tooltip("Above this severity it never mends by itself: it must be treated (101: always can).")]
    [Range(0f, 101f)] public float needsTreatmentAbove = 101f;
    [Tooltip("Above this severity only a veterinarian can turn it (a keeper's medicine only holds it), when there is one. 101: never.")]
    [Range(0f, 101f)] public float needsVetAbove = 101f;
    [Tooltip("Game hours of recovery after it is gone (lingering weakness: the recovering stage).")]
    [Min(0f)] public float recoveryHours = 24f;
    [Tooltip("Game hours after recovery when it cannot take hold again.")]
    [Min(0f)] public float immunityHours;
    [Tooltip("Exposure per game hour it gives each pen-mate at full severity (0: not catching).")]
    [Min(0f)] public float contagion;
    [Tooltip("Only animals with hooves get it.")]
    public bool hoovedOnly;
    [Tooltip("At death its taint runs through the whole carcass (a fever, worms). Off for a complaint of one part of the body (hoof rot): the meat stays fit to eat.")]
    public bool taintsCarcass = true;
    [Tooltip("The stage (an index into Stages) from which a careful look can name it (0 = from the first).")]
    [Min(0)] public int diagnoseFrom = 1;
    [Tooltip("What its death is recorded as.")]
    public AnimalDeathCause deathCause = AnimalDeathCause.Illness;
    [Tooltip("Its stages by severity, in order (the first from 0).")]
    public List<AnimalConditionStage> stages = new List<AnimalConditionStage>();
    [Tooltip("How it is while recovering (mild lingering weakness).")]
    public AnimalConditionStage recovering = new AnimalConditionStage
    {
        name = "Recovering", visible = true, appetite = 0.9f, growth = 0.9f, production = 0.85f, comfort = -3f,
        symptoms = new List<string> { "It is mending, but still weak." },
    };

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;

    /// <summary>The last stage whose start is at or below <paramref name="severity"/> (0 if none).</summary>
    public int StageAt(float severity)
    {
        int at = 0;
        if (stages == null) return 0;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && stages[i].from <= severity) at = i;
        return at;
    }

    /// <summary>A stage by index (clamped), or the recovering stage when <paramref name="index"/> is below 0. Never null.</summary>
    public AnimalConditionStage Stage(int index)
    {
        if (index < 0) return recovering ?? AnimalHealth.NoStage;
        if (stages == null || stages.Count == 0) return AnimalHealth.NoStage;
        return stages[Mathf.Clamp(index, 0, stages.Count - 1)] ?? AnimalHealth.NoStage;
    }

    /// <summary>The stage an animal's condition is in now (its recovering stage while it recovers).</summary>
    public AnimalConditionStage StageOf(AnimalCondition k) => k == null ? AnimalHealth.NoStage : Stage(k.Recovering ? -1 : k.stage);

    /// <summary>The name of the stage it is in ("Moderate", "Recovering").</summary>
    public string StageName(AnimalCondition k)
    {
        AnimalConditionStage s = StageOf(k);
        return !string.IsNullOrWhiteSpace(s.name) ? s.name : k != null && k.Recovering ? "Recovering" : $"stage {(k != null ? k.stage + 1 : 1)}";
    }

    public bool IsLastStage(int index) => stages != null && stages.Count > 0 && index >= stages.Count - 1;

    /// <summary>How much a cause counts (the sum of its weights).</summary>
    public float Weight(AnimalCause c)
    {
        float w = 0f;
        if (causes == null) return 0f;
        for (int i = 0; i < causes.Count; i++)
            if (causes[i] != null && causes[i].cause == c) w += Mathf.Max(0f, causes[i].weight);
        return w;
    }

    /// <summary>
    /// What is wrong with it. Errors: no id, no stages, stages that do not start at 0 or do not rise, a visible stage with
    /// no symptom (the owner would be told nothing). Warnings: no causes, a stage it can never be named from, a catching
    /// illness its pen-mates cannot catch (no Contagion cause).
    /// </summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        string who = string.IsNullOrEmpty(id) ? "(a condition without an id)" : $"condition '{id}'";
        if (!AnimalIds.ValidKey(id)) errors?.Add($"{who}: the id may not be empty or hold spaces, '/', ':' or '|'.");
        if (!Enum.IsDefined(typeof(AnimalConditionKind), kind)) errors?.Add($"{who}: an unknown kind ({(int)kind}).");
        if (stages == null || stages.Count == 0) errors?.Add($"{who}: no stages.");
        else
        {
            for (int i = 0; i < stages.Count; i++)
            {
                AnimalConditionStage s = stages[i];
                if (s == null) { errors?.Add($"{who}: stage {i + 1} is empty."); continue; }
                if (i == 0 && s.from > 0f) errors?.Add($"{who}: its first stage must start at 0 (it starts at {s.from:0}).");
                if (i > 0 && stages[i - 1] != null && s.from <= stages[i - 1].from) errors?.Add($"{who}: its stages do not rise ({stages[i - 1].from:0} then {s.from:0}).");
                if (s.visible && string.IsNullOrWhiteSpace(s.Symptom)) errors?.Add($"{who}: the visible stage '{s.name}' has no symptom.");
            }
            if (diagnoseFrom >= stages.Count) warnings?.Add($"{who}: it can never be named by a look (Diagnose From {diagnoseFrom}, {stages.Count} stages).");
            bool anyVisible = false;
            foreach (AnimalConditionStage s in stages) if (s != null && s.visible) anyVisible = true;
            if (!anyVisible) warnings?.Add($"{who}: no stage shows: only the veterinarian finds it.");
        }
        if (recovering != null && recovering.visible && string.IsNullOrWhiteSpace(recovering.Symptom)) errors?.Add($"{who}: its recovering stage shows but has no symptom.");
        if (causes == null || causes.Count == 0) warnings?.Add($"{who}: nothing brings it on (no causes).");
        if (worsenPerHour < courseWithoutCause) warnings?.Add($"{who}: it worsens more slowly with its causes than without them.");
        if (contagion > 0f && Weight(AnimalCause.Contagion) <= 0f) warnings?.Add($"{who}: it is catching, but lists no Contagion cause, so no pen-mate catches it.");
        if (courseWithoutCause >= 0f && needsTreatmentAbove > 100f) warnings?.Add($"{who}: it never mends by itself but never needs treatment either.");
        if (deathCause == AnimalDeathCause.None) warnings?.Add($"{who}: an animal it kills dies of no recorded cause.");
    }
}

/// <summary>How prone a species is to one illness (unlisted: 1; 0: immune).</summary>
[Serializable]
public class AnimalSusceptibility
{
    public string condition = "";
    [Min(0f)] public float factor = 1f;
}

/// <summary>A further step of an illness's treatment (see <see cref="AnimalHealth.Dose"/>): a weaker dose still working, at its power to its end.</summary>
[Serializable]
public class AnimalDoseStep
{
    [Tooltip("Severity per game hour it takes off.")]
    public float power;
    [Tooltip("It works until this game hour.")]
    public double until;
}

/// <summary>An illness an animal has now (on its record).</summary>
[Serializable]
public class AnimalCondition
{
    public string id = "";
    [Range(0f, 100f)] public float severity;
    [Tooltip("The index of its stage (by severity).")]
    public int stage;
    [Tooltip("Game hour it took hold.")]
    public double since;
    public float peak;
    [Tooltip("What brought it on (the strongest cause when it took hold).")]
    public AnimalCause cause;
    [Tooltip("Its owner's side knows what it is (examined, or seen by the vet).")]
    public bool diagnosed;
    [Tooltip("Have its symptoms shown yet (the owner was told).")]
    public bool shown;
    [Tooltip("A treatment works on it until this game hour (the strongest dose working).")]
    public double treatedUntil = -1.0;
    [Tooltip("Severity per game hour the treatment takes off (the strongest dose working).")]
    public float treatPower;
    [Tooltip("A weaker dose given meanwhile works on after Treated Until, at this power, until Then Until.")]
    public float thenPower;
    [Tooltip("The weaker dose works until this game hour (-1: none).")]
    public double thenUntil = -1.0;
    [Tooltip("Weaker doses still, each working on after the one before it ends, at its power to its end (none: empty).")]
    public List<AnimalDoseStep> laterSteps = new List<AnimalDoseStep>();
    [Tooltip("The veterinarian has treated it (keeper medicine can then turn it too).")]
    public bool byVet;
    [Tooltip("The last treatment given for it (an id).")]
    public string treatment = "";
    [Tooltip("Recovering until this game hour (-1: not recovering).")]
    public double recoveringUntil = -1.0;

    public bool Recovering => recoveringUntil >= 0.0;
    /// <summary>Is a dose working on it at a time (each step has a power above 0, see <see cref="AnimalHealth.Dose"/>)?</summary>
    public bool Treated(double now) => PowerAt(now) > 0f;

    /// <summary>The severity per game hour its treatment takes off at a time: the strongest dose's, then each weaker one's after it, to its own end.</summary>
    public float PowerAt(double now)
    {
        if (now < treatedUntil) return treatPower;
        if (now < thenUntil) return thenPower;
        if (laterSteps != null)
            foreach (AnimalDoseStep s in laterSteps)
                if (s != null && now < s.until) return s.power;
        return 0f;
    }
}

/// <summary>The build-up of one illness's causes in an animal (no entry: none), and its immunity after it.</summary>
[Serializable]
public class AnimalExposure
{
    public string condition = "";
    public float value;
    public double immuneUntil = -1.0;
}

/// <summary>One entry of an animal's health history. History shows only entries made while the illness showed, in symptom words until it was named.</summary>
[Serializable]
public class AnimalHealthEntry
{
    public double at;
    public AnimalHealthEventKind kind;
    public string condition = "";
    public string stage = "";
    public string text = "";
    [Tooltip("Who did it (a lasting key), if anyone.")]
    public string by = "";
    [Tooltip("Made while the illness was visible to a keeper (History shows only these).")]
    public bool shown;
    [Tooltip("Made after it was named (diagnosed): its text names it.")]
    public bool named;
}

/// <summary>A dose of medicine given (for overdoses and courses).</summary>
[Serializable]
public class AnimalDose
{
    public string treatment = "";
    public double at;
    public float strength;
}

/// <summary>What a medicine does to one illness: severity off at once, then per game hour while it works.</summary>
[Serializable]
public class AnimalTreatmentEffect
{
    [Tooltip("The illness it acts on (\"*\": every illness).")]
    public string condition = "";
    [Tooltip("Severity it takes off at once.")]
    [Min(0f)] public float immediate;
    [Tooltip("Severity it takes off per game hour while it works.")]
    [Min(0f)] public float perHour;
    [Tooltip("Game hours one dose works.")]
    [Min(0f)] public float hours = 12f;
}

/// <summary>
/// A medicine for animals, as data: the items that are it (a brewed potion of an Alchemy outcome, a plain remedy), how it is
/// given, how many doses an item holds, what it does to which illnesses and besides, and the harm of too much of it.
/// </summary>
[Serializable]
public class AnimalTreatmentData
{
    public const string AnyCondition = "*";

    public string id = "";
    public string name = "";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Item keys that are this medicine: \"potion:antidote\" (any bottle of that Alchemy outcome), \"ingredient:charcoal\"; a key ending in * is a prefix.")]
    public List<string> items = new List<string>();
    [Tooltip("The copy's quality (a potion's strength and purity) scales it: x0.4 (worthless) to x2 (superior).")]
    public bool qualityMatters = true;
    [Tooltip("How it is given (a topical one works less on a filthy coat and cannot be given to a whole herd).")]
    public AnimalTreatmentRoute route = AnimalTreatmentRoute.Drench;
    [Tooltip("Doses for an average animal one item holds (a cow needs about 4 doses, a hen 0.2).")]
    [Min(0.05f)] public float dosesPerItem = 2f;
    public List<AnimalTreatmentEffect> effects = new List<AnimalTreatmentEffect>();

    [Header("Besides")]
    [Tooltip("Share of its toxin load it clears at once (at full strength).")]
    [Range(0f, 1f)] public float toxinCleared;
    [Tooltip("Digestive upset it eases, health it gives at once, and what it does to comfort, stress, satiety and hydration (all x its strength).")]
    public float upsetEased;
    public float healthNow;
    public float comfort;
    public float stress;
    public float satiety;
    public float hydration;

    [Header("Care")]
    [Tooltip("Given again sooner than this after its last dose that did its good, it is too soon: it does no more good at once, only its harm (with an Overdose Share, an overdose). With no Overdose Share it is not given at all (the keeper is told when it may be).")]
    [Min(0f)] public float minIntervalHours = 6f;
    [Tooltip("Toxin a dose given too soon gives (an overdose), as a share of the animal's toxin tolerance (at full strength): the same harm to a hen as to a cow. 0: no harm, and a dose too soon is refused.")]
    [Min(0f)] public float overdoseShare;
    [Tooltip("Stress of being dosed (a skittish animal more).")]
    [Min(0f)] public float handlingStress = 4f;

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;

    /// <summary>Is this item key this medicine (exactly, or by a key of it ending in *)?</summary>
    public bool IsItem(string key)
    {
        if (items == null || string.IsNullOrEmpty(key)) return false;
        for (int i = 0; i < items.Count; i++)
        {
            string k = items[i];
            if (string.IsNullOrEmpty(k)) continue;
            if (k.EndsWith("*", StringComparison.Ordinal) ? key.StartsWith(k.Substring(0, k.Length - 1), StringComparison.Ordinal) : string.Equals(k, key, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>What it does to an illness: its own effect for it, else its effect on every illness ("*"), else null.</summary>
    public AnimalTreatmentEffect EffectOn(string condition)
    {
        if (effects == null || string.IsNullOrEmpty(condition)) return null;
        AnimalTreatmentEffect any = null;
        for (int i = 0; i < effects.Count; i++)
        {
            AnimalTreatmentEffect e = effects[i];
            if (e == null) continue;
            if (string.Equals(e.condition, condition, StringComparison.OrdinalIgnoreCase)) return e;
            if (any == null && e.condition == AnyCondition) any = e;
        }
        return any;
    }

    /// <summary>What is wrong with it (<paramref name="cat"/>, when given, checks the illnesses it names).</summary>
    public void Validate(IAnimalCatalog cat, List<string> errors, List<string> warnings)
    {
        string who = string.IsNullOrEmpty(id) ? "(a treatment without an id)" : $"treatment '{id}'";
        if (!AnimalIds.ValidKey(id)) errors?.Add($"{who}: the id may not be empty or hold spaces, '/', ':' or '|'.");
        if (items == null || items.Count == 0) warnings?.Add($"{who}: no item is this medicine (it can never be given).");
        bool acts = toxinCleared > 0f || upsetEased > 0f || healthNow > 0f || satiety > 0f || hydration > 0f || stress < 0f || comfort > 0f;
        if (effects != null)
            foreach (AnimalTreatmentEffect e in effects)
            {
                if (e == null) continue;
                acts = true;
                if (string.IsNullOrWhiteSpace(e.condition)) errors?.Add($"{who}: an effect names no condition.");
                else if (e.condition != AnyCondition && cat != null && cat.Condition(e.condition) == null) warnings?.Add($"{who}: it acts on '{e.condition}', which is no condition.");
                if (e.immediate <= 0f && (e.perHour <= 0f || e.hours <= 0f)) warnings?.Add($"{who}: its effect on '{e.condition}' does nothing.");
            }
        if (!acts) warnings?.Add($"{who}: it does nothing.");
    }
}

// ==================================================================================================== per step
/// <summary>
/// What an animal lives through this step that its pen decides, from the step-start state (see <see cref="AnimalHabitat.Facts"/>
/// and <see cref="AnimalHabitat.BoardedFacts"/>). The dictionaries may be null (read null-safely).
/// </summary>
public struct AnimalStepFacts
{
    /// <summary>The boarding stable (no pen).</summary>
    public bool boarded;
    public AnimalHabitatKind kind;
    /// <summary>Square metres.</summary>
    public float area;
    /// <summary>Room in use / area (adult space of the living).</summary>
    public float density;
    /// <summary>Step-start pen state, 0-100 (bedding 0 if the pen takes none).</summary>
    public float dirt, waterDirt, worms, bedding;
    /// <summary>Grazing: a pen that grows grass.</summary>
    public bool usesBedding, open, grazing;
    public float shelter, warmth, shade;
    /// <summary>Litres in the trough at step start.</summary>
    public float water;
    /// <summary>The room the remains of its dead take (they soil the pen).</summary>
    public float deadSpace;
    /// <summary>Phase 5: comfort its pen's level adds to where its animals' comfort settles, and how much faster they make their products (none in disrepair).</summary>
    public float comfortBonus, productionBonus;
    /// <summary>Condition id -> exposure per game hour from every source in the pen (an animal's own share is taken off when it reads it).</summary>
    public Dictionary<string, float> contagion;
    /// <summary>Species id -> living animals of it in the pen (itself included).</summary>
    public Dictionary<string, int> companions;
    /// <summary>The living animals in the pen, by id (Phase 4: who is near whom). Null: the boarding stable.</summary>
    public HashSet<string> here;
    /// <summary>Mother id -> her young suckling from her in the pen (Phase 4), and the milk she gives them (0-1, from her nutrition).</summary>
    public Dictionary<string, int> nursing;
    public Dictionary<string, float> milk;

    public int NursingOf(string mother) => nursing != null && !string.IsNullOrEmpty(mother) && nursing.TryGetValue(mother, out int n) ? n : 0;

    public float ContagionOf(string condition) => contagion != null && !string.IsNullOrEmpty(condition) && contagion.TryGetValue(condition, out float v) ? v : 0f;
}

/// <summary>The combined effects of an animal's illnesses' stages (multipliers at most 1, except production; the rest sums).</summary>
public struct AnimalStageEffects
{
    public float appetite, growth, absorption, mobility, production;
    public float comfort, stress, happiness, hygienePerHour, healthPerHour;

    /// <summary>No illness: multipliers 1, sums 0 (a default struct is NOT this: its multipliers are 0).</summary>
    public static AnimalStageEffects Neutral => new AnimalStageEffects { appetite = 1f, growth = 1f, absorption = 1f, mobility = 1f, production = 1f };
}

/// <summary>The health per game hour one illness took this step (from its stage at the step's start).</summary>
public struct AnimalConditionDamage
{
    public string condition;
    public float perHour;
}

/// <summary>Something the owner should be told (the simulation raises it through its own Tell).</summary>
public struct AnimalHealthNotice
{
    public AnimalEventKind kind;
    public double at;
    public string text;
}

/// <summary>What one step of <see cref="AnimalHealth.Step"/> came to (a small object the caller reuses).</summary>
public sealed class AnimalHealthOutcome
{
    /// <summary>Health per game hour each illness took this step (from its step-start stage), in the catalog's order.</summary>
    public readonly List<AnimalConditionDamage> damage = new List<AnimalConditionDamage>();
    /// <summary>What to tell the owner, in order.</summary>
    public readonly List<AnimalHealthNotice> notices = new List<AnimalHealthNotice>();
    /// <summary>Its illnesses' stage effects at the step's start.</summary>
    public AnimalStageEffects effects = AnimalStageEffects.Neutral;
    /// <summary>Its worst active severity after the step (0: healthy).</summary>
    public float worst;

    /// <summary>The health per game hour all its illnesses took this step.</summary>
    public float Ill
    {
        get
        {
            float s = 0f;
            for (int i = 0; i < damage.Count; i++) s += damage[i].perHour;
            return s;
        }
    }

    /// <summary>The illness that took the most health this step (the first in catalog order on a tie): its rate, and its id.</summary>
    public float Strongest(out string condition)
    {
        condition = null;
        float best = 0f;
        for (int i = 0; i < damage.Count; i++)
            if (damage[i].perHour > best)
            {
                best = damage[i].perHour;
                condition = damage[i].condition;
            }
        return best;
    }

    public void Clear()
    {
        damage.Clear();
        notices.Clear();
        effects = AnimalStageEffects.Neutral;
        worst = 0f;
    }

    internal void Tell(AnimalEventKind kind, double at, string text) => notices.Add(new AnimalHealthNotice { kind = kind, at = at, text = text ?? "" });
}

// ==================================================================================================== logic
/// <summary>
/// The pure logic of illness: what causes it (<see cref="Causes"/>), how it takes hold, worsens, is treated, mends and
/// leaves immunity (<see cref="Step"/>), what it does (<see cref="Effects"/>), how a keeper finds it (<see cref="Examine"/>),
/// treats it (<see cref="Treat"/>) or sends for the veterinarian (<see cref="VetVisit"/>), and the health history's
/// words. Deterministic: onsets come from exposures crossing a per-animal threshold, never a chance roll.
/// </summary>
public static class AnimalHealth
{
    /// <summary>The number of causes (an array for <see cref="Causes"/> needs at least this many).</summary>
    public const int CauseCount = (int)AnimalCause.Fighting + 1;

    internal static readonly AnimalConditionStage NoStage = new AnimalConditionStage { name = "", visible = false };
    private static readonly AnimalWelfareBandEffects NeutralBand = new AnimalWelfareBandEffects();
    private static readonly List<AnimalCause> causeOrder = new List<AnimalCause>();

    /// <summary>A new array for <see cref="Causes"/>.</summary>
    public static float[] NewCauses() => new float[CauseCount];

    /// <summary>A cause in words, for a diagnosis and the history ("a dirty pen").</summary>
    public static string Phrase(AnimalCause c)
    {
        switch (c)
        {
            case AnimalCause.Malnourished: return "poor feeding";
            case AnimalCause.Parched: return "thirst";
            case AnimalCause.Toxins: return "something poisonous it ate";
            case AnimalCause.Upset: return "bad food";
            case AnimalCause.DirtyHabitat: return "a dirty pen";
            case AnimalCause.DirtyWater: return "foul water";
            case AnimalCause.LowHygiene: return "a dirty coat";
            case AnimalCause.Wound: return "an open wound";
            case AnimalCause.Chill: return "cold and wet";
            case AnimalCause.Heat: return "the heat";
            case AnimalCause.Crowding: return "crowding";
            case AnimalCause.Loneliness: return "being kept alone";
            case AnimalCause.HighStress: return "constant stress";
            case AnimalCause.HoofNeglect: return "neglected hooves";
            case AnimalCause.Worms: return "worms in the pasture";
            case AnimalCause.Contagion: return "sick pen-mates";
            case AnimalCause.Fighting: return "fights in a crowded pen";
            default: return "nothing in particular";
        }
    }

    /// <summary>How fouled water of a kind is when poured (WaterSourceKind): a swamp's 45, a lake's 20, a river's 12, the rest 0.</summary>
    public static float KindDirt(int waterKind)
    {
        switch ((WaterSourceKind)waterKind)
        {
            case WaterSourceKind.Swamp: return 45f;
            case WaterSourceKind.Lake: return 20f;
            case WaterSourceKind.River: return 12f;
            default: return 0f;
        }
    }

    // ---------------------------------------------------------------- causes
    /// <summary>
    /// Measures every cause for one animal this step (each 0-1, Toxins and Upset up to 1.5) into <paramref name="into"/>
    /// (at least <see cref="CauseCount"/> long; Contagion is not filled: it differs by illness, see <see cref="ContagionFor"/>).
    /// </summary>
    public static void Causes(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules, in AnimalStepFacts f, in AgriWeather w, float[] into)
    {
        if (into == null) return;
        Array.Clear(into, 0, into.Length);
        if (a == null) return;
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        AnimalCareSpec care = AnimalCare.CareOf(sp);
        float mal = rules.malnourishedBelow;
        Set(into, AnimalCause.Malnourished, mal > 0f && a.nutrition < mal ? (mal - a.nutrition) / mal : 0f);
        Set(into, AnimalCause.Parched, a.hydration < 20f ? (20f - a.hydration) / 20f : 0f);
        float tol = AnimalSim.Tolerance(a, sp);
        Set(into, AnimalCause.Toxins, a.toxin > tol ? Mathf.Min(1.5f, (a.toxin - tol) / Mathf.Max(1f, tol)) : 0f);
        Set(into, AnimalCause.Upset, a.upset > 20f ? Mathf.Min(1.5f, (a.upset - 20f) / 60f) : 0f);
        Set(into, AnimalCause.DirtyHabitat, f.boarded ? 0f : AnimalMath.Smooth(35f, 95f, f.dirt));
        Set(into, AnimalCause.DirtyWater, f.water > 0.05f ? AnimalMath.Smooth(25f, 90f, f.waterDirt) : 0f);
        Set(into, AnimalCause.LowHygiene, a.hygiene < 40f ? (40f - a.hygiene) / 40f : 0f);
        float wound = 0f;
        if (a.conditions != null && cat != null)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null || k.Recovering) continue;
                AnimalConditionData c = cat.Condition(k.id);
                if (c != null && c.kind == AnimalConditionKind.Injury) wound = Mathf.Max(wound, k.severity / 100f);
            }
        Set(into, AnimalCause.Wound, wound);
        float eff = AnimalCare.EffectiveTemperature(sp, in f, in w, rules);
        float wet = w.Raining ? 1f - AnimalMath.Clamp01(f.shelter) : 0f;
        Set(into, AnimalCause.Chill, AnimalMath.Smooth(0f, 10f, care.comfortMin - eff) * (0.35f + 0.65f * wet));
        Set(into, AnimalCause.Heat, AnimalMath.Smooth(0f, 10f, eff - care.comfortMax));
        float crowding = AnimalCare.Crowding(sp, in f);
        Set(into, AnimalCause.Crowding, crowding);
        Set(into, AnimalCause.Loneliness, AnimalCare.Loneliness(a, sp, in f));
        Set(into, AnimalCause.HighStress, a.stress > 55f ? (a.stress - 55f) / 45f : 0f);
        Set(into, AnimalCause.HoofNeglect, care.HasHooves && a.hooves < 40f ? (40f - a.hooves) / 40f : 0f);
        Set(into, AnimalCause.Worms, f.boarded ? 0f : AnimalMath.Clamp100(f.worms) / 100f * (f.grazing ? 1f : 0.4f));
        float temperament = a.genes != null ? a.genes.temperament : 0.5f;
        Set(into, AnimalCause.Fighting, crowding * (1f - temperament) * (a.stress / 100f + 0.3f));
    }

    private static void Set(float[] into, AnimalCause c, float v)
    {
        int i = (int)c;
        if (i >= 0 && i < into.Length) into[i] = Mathf.Max(0f, v);
    }

    private static float Get(float[] causes, AnimalCause c)
    {
        int i = (int)c;
        return causes != null && i >= 0 && i < causes.Length ? causes[i] : 0f;
    }

    /// <summary>What an illness an animal has gives its pen per game hour: c.contagion x severity/100 (recovering: x 0.3, flat).</summary>
    public static float GivesOff(AnimalCondition k, AnimalConditionData c)
    {
        if (k == null || c == null || c.contagion <= 0f) return 0f;
        return k.Recovering ? c.contagion * 0.3f : c.contagion * AnimalMath.Clamp100(k.severity) / 100f;
    }

    /// <summary>The contagion of an illness it lives in: the pen's, less its own share (never below 0).</summary>
    public static float ContagionFor(AnimalRecord a, AnimalConditionData c, in AnimalStepFacts f)
    {
        if (c == null) return 0f;
        float own = GivesOff(a?.Condition(c.id), c);
        return Mathf.Max(0f, f.ContagionOf(c.id) - own);
    }

    /// <summary>The exposure per game hour an illness's causes give (the sum of weight x cause; Contagion from <paramref name="contagion"/>).</summary>
    public static float Gain(AnimalConditionData c, float[] causes, float contagion)
    {
        if (c?.causes == null) return 0f;
        float g = 0f;
        for (int i = 0; i < c.causes.Count; i++)
        {
            AnimalConditionCause x = c.causes[i];
            if (x == null || x.weight <= 0f) continue;
            g += x.weight * (x.cause == AnimalCause.Contagion ? contagion : Get(causes, x.cause));
        }
        return g;
    }

    /// <summary>Its strongest cause of an illness now (what a diagnosis names).</summary>
    public static AnimalCause Strongest(AnimalConditionData c, float[] causes, float contagion)
    {
        AnimalCause best = AnimalCause.None;
        float most = 0f;
        if (c?.causes == null) return best;
        for (int i = 0; i < c.causes.Count; i++)
        {
            AnimalConditionCause x = c.causes[i];
            if (x == null) continue;
            float v = x.weight * (x.cause == AnimalCause.Contagion ? contagion : Get(causes, x.cause));
            if (v > most + 1e-6f)
            {
                most = v;
                best = x.cause;
            }
        }
        return best;
    }

    /// <summary>The exposure at which an illness takes hold in this animal: its onset, a little either side by its genes (±15%).</summary>
    public static float Threshold(AnimalRecord a, AnimalConditionData c) =>
        c.onsetAt * (1f + 0.15f * AnimalMath.Signed(a?.genes != null ? a.genes.seed : 0, AnimalMath.Hash(c.id)));

    // ---------------------------------------------------------------- the step
    /// <summary>
    /// Every illness of the catalog, in its order, for one animal over a step (from <paramref name="from"/> to <paramref name="to"/>):
    /// <list type="bullet">
    /// <item>s = its species' susceptibility. Not ill with it: exposure += (gain x s x band susceptibility x the rules'
    /// Exposure Scale x pace / hardiness) settling over the illness's exposure hours (exact whatever the step length); at
    /// its threshold the illness takes hold (<see cref="Begin"/>). Immune for a while after one.</item>
    /// <item>Ill with it: drive = clamp01(gain x s x band susceptibility / (2 x onset / exposure hours)); rate = course
    /// without cause + (worsen - course) x drive (worsening x pace, mending x band recovery); a treatment working takes its
    /// power off; above Needs Treatment Above it never mends untreated; held (never mends) when <see cref="Gated"/>. While it
    /// worsens it stops at 30 + 70 x drive (a weak lasting cause plateaus at Moderate). At 0 it recovers for its recovery
    /// hours, then clears, leaving immunity; a strong cause meanwhile makes it relapse (at its start severity).</item>
    /// <item>An illness its species is immune to (s 0) loses its exposure; one it already has goes on with no cause.</item>
    /// </list>
    /// Fills <paramref name="outcome"/> (may be null): each illness's damage (its step-start stage's health per hour), the
    /// step-start effects, the worst severity after, and what to tell the owner (FellIll, Worsened, Healed).
    /// </summary>
    public static void Step(AnimalRecord a, AnimalSpeciesData sp, AnimalSimContext ctx, in AnimalStepFacts f, float[] causes, double from, double to,
        float pace, AnimalWelfareBandEffects band, AnimalHealthOutcome outcome)
    {
        outcome?.Clear();
        if (a == null || ctx?.catalog == null) return;
        IAnimalCatalog cat = ctx.catalog;
        AnimalRules rules = ctx.Rules;
        if (outcome != null) outcome.effects = Effects(a, cat);
        double dtd = to - from;
        if (!rules.healthEnabled || !a.Alive || dtd <= 0.0)
        {
            if (outcome != null) outcome.worst = Worst(a, cat);
            return;
        }
        band ??= NeutralBand;
        float dt = (float)dtd;
        float hardiness = AnimalSim.Hardiness(a, sp);
        a.conditions ??= new List<AnimalCondition>();
        a.exposures ??= new List<AnimalExposure>();
        IReadOnlyList<AnimalConditionData> all = cat.AllConditions;
        if (all == null) return;
        for (int i = 0; i < all.Count; i++)
        {
            AnimalConditionData c = all[i];
            if (c == null || string.IsNullOrEmpty(c.id)) continue;
            float s = sp != null ? sp.SusceptibilityTo(c) : 1f;
            AnimalCondition k = a.Condition(c.id);
            float contagion = 0f, gain = 0f;
            if (s <= 0f)
            {
                RemoveExposure(a, c.id);
                if (k == null) continue;
            }
            else
            {
                contagion = ContagionFor(a, c, in f);
                gain = Gain(c, causes, contagion);
            }

            if (k == null)
            {
                AnimalExposure e = a.Exposure(c.id);
                if (e == null)
                {
                    if (gain <= 0f) continue;
                    e = new AnimalExposure { condition = c.id };
                    a.exposures.Add(e);
                }
                if (from < e.immuneUntil)
                {
                    e.value = 0f;
                    continue;
                }
                float g = gain * s * band.susceptibility * rules.exposureScale * pace / hardiness;
                double hours = Math.Max(0.1, c.exposureHours);
                float d = AnimalMath.Decay(dtd, hours);
                e.value = e.value * d + g * (float)hours * (1f - d);
                if (e.value >= Threshold(a, c))
                {
                    e.value = 0f;
                    Begin(a, c, Strongest(c, causes, contagion), to, rules, sp, outcome);
                }
                // Dropped only once it has faded with nothing building it: a short step adds little, but its cause still counts.
                else if (g <= 0f && e.value < 0.01f && e.immuneUntil < from) a.exposures.Remove(e);
                continue;
            }

            AnimalConditionStage atStart = c.StageOf(k);
            float drive = AnimalMath.Clamp01(gain * s * band.susceptibility / Mathf.Max(1e-4f, 2f * c.onsetAt / Mathf.Max(0.1f, c.exposureHours)));
            float rate = c.courseWithoutCause + (c.worsenPerHour - c.courseWithoutCause) * drive;
            rate = rate > 0f ? rate * pace : rate * band.recovery;
            bool treated = k.Treated(from);
            if (treated) rate -= k.PowerAt(from);
            if (!treated && k.severity >= c.needsTreatmentAbove) rate = Mathf.Max(rate, 0f);
            if (rate < 0f && Gated(k, c, rules, ctx.vetAvailable)) rate = 0f;

            if (k.Recovering)
            {
                if (rate > 0.5f)
                {
                    // Its causes are back in force: it is ill again.
                    k.recoveringUntil = -1.0;
                    k.severity = AnimalMath.Clamp100(c.startSeverity);
                    k.stage = c.StageAt(k.severity);
                    k.peak = Mathf.Max(k.peak, k.severity);
                    k.since = to;
                    AnimalCause cause = Strongest(c, causes, contagion);
                    if (cause != AnimalCause.None) k.cause = cause;
                    // Back in a stage that hides, it is hidden again until it shows (then its owner is told, as at first);
                    // it stays named, so what is logged after that calls it by its name.
                    if (!c.Stage(k.stage).visible) k.shown = false;
                    LogEvent(a, c, k, AnimalHealthEventKind.Relapsed, to, "", rules);
                    if (c.Stage(k.stage).visible)
                    {
                        if (!k.shown)
                        {
                            k.shown = true;
                            LogEvent(a, c, k, AnimalHealthEventKind.Showed, to, "", rules);
                        }
                        outcome?.Tell(AnimalEventKind.FellIll, to, $"{AnimalSim.NameOf(a, sp)}: {c.Stage(k.stage).Symptom}");
                    }
                    a.revision++;
                }
                else if (to >= k.recoveringUntil)
                {
                    a.conditions.Remove(k);
                    AnimalExposure e = a.Exposure(c.id);
                    if (e == null)
                    {
                        e = new AnimalExposure { condition = c.id };
                        a.exposures.Add(e);
                    }
                    e.value = 0f;
                    e.immuneUntil = to + Math.Max(0.0, c.immunityHours);
                    // One its keepers knew of is told over, also when it came back in a stage that hides and mended unseen.
                    k.shown = k.shown || k.diagnosed;
                    LogEvent(a, c, k, AnimalHealthEventKind.Cleared, to, "", rules);
                    if (k.shown)
                        outcome?.Tell(AnimalEventKind.Healed, to, k.diagnosed
                            ? $"{AnimalSim.NameOf(a, sp)} has got over its {c.DisplayName.ToLowerInvariant()}."
                            : $"{AnimalSim.NameOf(a, sp)} is well again.");
                    a.revision++;
                }
            }
            else
            {
                int old = k.stage;
                if (rate > 0f)
                {
                    float plateau = 30f + 70f * drive;
                    if (k.severity < plateau) k.severity = Mathf.Min(k.severity + rate * dt, plateau);
                }
                else k.severity += rate * dt;
                k.severity = AnimalMath.Clamp100(k.severity);
                k.stage = c.StageAt(k.severity);
                k.peak = Mathf.Max(k.peak, k.severity);
                if (k.stage > old)
                {
                    LogEvent(a, c, k, AnimalHealthEventKind.Worsened, to, "", rules);
                    AnimalConditionStage reached = c.Stage(k.stage);
                    if (!k.shown && reached.visible)
                    {
                        k.shown = true;
                        LogEvent(a, c, k, AnimalHealthEventKind.Showed, to, "", rules);
                        outcome?.Tell(AnimalEventKind.FellIll, to, $"{AnimalSim.NameOf(a, sp)}: {reached.Symptom}");
                    }
                    if (c.IsLastStage(k.stage) && k.shown && c.stages.Count > 1) outcome?.Tell(AnimalEventKind.Worsened, to, $"{AnimalSim.NameOf(a, sp)} is getting worse.");
                    a.revision++;
                }
                else if (k.stage < old)
                {
                    LogEvent(a, c, k, AnimalHealthEventKind.Improved, to, "", rules);
                    a.revision++;
                }
                if (k.severity <= 0f)
                {
                    k.severity = 0f;
                    k.recoveringUntil = to + c.recoveryHours / Mathf.Max(0.25f, band.recovery);
                    LogEvent(a, c, k, AnimalHealthEventKind.Recovering, to, "", rules);
                    a.revision++;
                }
            }
            if (outcome != null && atStart.healthPerHour > 0f) outcome.damage.Add(new AnimalConditionDamage { condition = c.id, perHour = atStart.healthPerHour });
        }
        if (outcome != null) outcome.worst = Worst(a, cat);
    }

    private static void RemoveExposure(AnimalRecord a, string id)
    {
        if (a?.exposures == null) return;
        for (int i = a.exposures.Count - 1; i >= 0; i--)
            if (a.exposures[i] == null || string.Equals(a.exposures[i].condition, id, StringComparison.OrdinalIgnoreCase)) a.exposures.RemoveAt(i);
    }

    /// <summary>
    /// Makes an illness take hold (the simulation at an onset; editor tools): at its start severity, with the cause that
    /// brought it on. Its onset is logged hidden; when its first stage shows, the owner is told a symptom at once
    /// (<paramref name="outcome"/>, when given, gets the notice). An illness it already has is returned as it is.
    /// </summary>
    public static AnimalCondition Begin(AnimalRecord a, AnimalConditionData c, AnimalCause cause, double now, AnimalRules rules, AnimalSpeciesData sp = null,
        AnimalHealthOutcome outcome = null)
    {
        if (a == null || c == null || string.IsNullOrEmpty(c.id)) return null;
        AnimalCondition had = a.Condition(c.id);
        if (had != null) return had;
        rules ??= AnimalRules.Defaults();
        float sev = AnimalMath.Clamp100(c.startSeverity);
        var k = new AnimalCondition { id = c.id, severity = sev, stage = c.StageAt(sev), since = now, peak = sev, cause = cause };
        a.conditions ??= new List<AnimalCondition>();
        a.conditions.Add(k);
        LogEvent(a, c, k, AnimalHealthEventKind.Onset, now, "", rules);
        AnimalConditionStage st = c.Stage(k.stage);
        if (st.visible)
        {
            k.shown = true;
            LogEvent(a, c, k, AnimalHealthEventKind.Showed, now, "", rules);
            outcome?.Tell(AnimalEventKind.FellIll, now, $"{AnimalSim.NameOf(a, sp)}: {st.Symptom}");
        }
        a.revision++;
        return k;
    }

    /// <summary>
    /// The combined effects of its illnesses' stages (active ones by their stage, recovering ones by their recovering
    /// stage): appetite, growth, absorption and mobility multiply (at most 1), production multiplies, the rest add up.
    /// Neutral when health is off in the rules.
    /// </summary>
    public static AnimalStageEffects Effects(AnimalRecord a, IAnimalCatalog cat)
    {
        AnimalStageEffects fx = AnimalStageEffects.Neutral;
        if (a?.conditions == null || a.conditions.Count == 0 || cat == null) return fx;
        if (cat.Rules != null && !cat.Rules.healthEnabled) return fx;
        float appetite = 1f, growth = 1f, absorption = 1f, mobility = 1f, production = 1f;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c == null) continue;
            AnimalConditionStage s = c.StageOf(k);
            appetite *= Mathf.Max(0f, s.appetite);
            growth *= Mathf.Max(0f, s.growth);
            absorption *= Mathf.Max(0f, s.absorption);
            mobility *= Mathf.Max(0f, s.mobility);
            production *= Mathf.Max(0f, s.production);
            fx.comfort += s.comfort;
            fx.stress += s.stress;
            fx.happiness += s.happiness;
            fx.hygienePerHour += Mathf.Max(0f, s.hygienePerHour);
            fx.healthPerHour += Mathf.Max(0f, s.healthPerHour);
        }
        fx.appetite = Mathf.Min(1f, appetite);
        fx.growth = Mathf.Min(1f, growth);
        fx.absorption = Mathf.Min(1f, absorption);
        fx.mobility = Mathf.Min(1f, mobility);
        fx.production = production;
        return fx;
    }

    /// <summary>
    /// Adds a dose of a cause at once (washing in the cold: Chill): amount x the cause's weight x its susceptibility to the
    /// exposure of every illness listing that cause that it does not have and is not immune to.
    /// </summary>
    public static void Expose(AnimalRecord a, IAnimalCatalog cat, AnimalSpeciesData sp, AnimalCause cause, float amount, double now)
    {
        if (a == null || cat?.AllConditions == null || amount <= 0f) return;
        if (cat.Rules != null && !cat.Rules.healthEnabled) return;
        a.exposures ??= new List<AnimalExposure>();
        foreach (AnimalConditionData c in cat.AllConditions)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) continue;
            float wgt = c.Weight(cause);
            if (wgt <= 0f) continue;
            float s = sp != null ? sp.SusceptibilityTo(c) : 1f;
            if (s <= 0f || a.Condition(c.id) != null) continue;
            AnimalExposure e = a.Exposure(c.id);
            if (e == null)
            {
                e = new AnimalExposure { condition = c.id };
                a.exposures.Add(e);
            }
            if (now < e.immuneUntil) continue;
            e.value += amount * wgt * s;
        }
    }

    /// <summary>Takes a share of the build-up of every illness of a kind away (brushing out lice: Parasites).</summary>
    public static void Relieve(AnimalRecord a, IAnimalCatalog cat, AnimalConditionKind kind, float share)
    {
        if (a?.exposures == null || cat == null || share <= 0f) return;
        float keep = 1f - AnimalMath.Clamp01(share);
        foreach (AnimalExposure e in a.exposures)
        {
            if (e == null) continue;
            AnimalConditionData c = cat.Condition(e.condition);
            if (c != null && c.kind == kind) e.value *= keep;
        }
    }

    /// <summary>
    /// Is a keeper's medicine only able to hold this illness (not turn it)? Past its Needs Vet Above, while the rules want a
    /// veterinarian, one is in the world (<paramref name="vetAvailable"/>) and none has treated it yet.
    /// </summary>
    public static bool Gated(AnimalCondition k, AnimalConditionData c, AnimalRules rules, bool vetAvailable) =>
        vetAvailable && k != null && c != null && (rules == null || rules.vetRequired) && !k.byVet && k.severity >= c.needsVetAbove;

    /// <summary>
    /// Its worst active severity (0 if none; a recovering illness counts as 0). With <paramref name="cat"/>, an illness the
    /// catalog no longer has does not count (it is kept on the record, and acts again if its data comes back).
    /// </summary>
    public static float Worst(AnimalRecord a, IAnimalCatalog cat = null)
    {
        float m = 0f;
        if (a?.conditions == null) return 0f;
        foreach (AnimalCondition k in a.conditions)
            if (k != null && !k.Recovering && k.severity > m && (cat == null || cat.Condition(k.id) != null)) m = k.severity;
        return m;
    }

    /// <summary>Its worst active illness of a kind (needs the catalog), or null.</summary>
    public static AnimalCondition WorstOf(AnimalRecord a, IAnimalCatalog cat, AnimalConditionKind kind)
    {
        AnimalCondition best = null;
        if (a?.conditions == null || cat == null) return null;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null || k.Recovering) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c != null && c.kind == kind && (best == null || k.severity > best.severity)) best = k;
        }
        return best;
    }

    /// <summary>Does it look sick (a visible stage that costs health, or health below 40)?</summary>
    public static bool LooksSick(AnimalRecord a, IAnimalCatalog cat)
    {
        if (a == null) return false;
        if (a.health < 40f) return true;
        if (a.conditions == null || cat == null) return false;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null || k.Recovering) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c == null) continue;
            AnimalConditionStage s = c.Stage(k.stage);
            if (s.visible && s.healthPerHour > 0f) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- the health history
    /// <summary>Adds an entry to its health history (the rules' Keep Health Log are kept).</summary>
    public static void Log(AnimalRecord a, AnimalHealthEventKind kind, double at, string condition, string stage, string text, string by, int keep,
        bool shown = true, bool named = true)
    {
        a?.LogHealth(new AnimalHealthEntry
        {
            at = at, kind = kind, condition = condition ?? "", stage = stage ?? "", text = text ?? "", by = by ?? "", shown = shown, named = named,
        }, keep);
    }

    /// <summary>
    /// Logs what happened to an illness, in the words a keeper would use: by its symptoms until it is named (diagnosed),
    /// by its name after; an entry made before it showed is kept hidden (History does not print it).
    /// </summary>
    public static void LogEvent(AnimalRecord a, AnimalConditionData c, AnimalCondition k, AnimalHealthEventKind kind, double at, string by, AnimalRules rules,
        string treatment = null)
    {
        if (a == null) return;
        bool named = k != null && k.diagnosed;
        bool shown = kind != AnimalHealthEventKind.Onset && (k == null || k.shown);
        string stage = c != null && k != null ? c.StageName(k) : "";
        Log(a, kind, at, c?.id ?? k?.id ?? "", stage, EntryText(c, k, kind, treatment), by, (rules ?? AnimalRules.Defaults()).keepHealthLog, shown, named);
    }

    /// <summary>The words of a health-history entry (§16.4): Onset "(hidden)", Showed its symptom, "It got worse: ..." / "{Name} got worse (Moderate)", and so on.</summary>
    public static string EntryText(AnimalConditionData c, AnimalCondition k, AnimalHealthEventKind kind, string treatment = null)
    {
        bool named = k != null && k.diagnosed && c != null;
        string name = c != null ? c.DisplayName : k?.id ?? "it";
        string lower = name.ToLowerInvariant();
        string stage = c != null && k != null ? c.StageName(k) : "";
        string symptom = c != null && k != null ? c.StageOf(k).Symptom : "";
        switch (kind)
        {
            case AnimalHealthEventKind.Onset: return "(hidden)";
            case AnimalHealthEventKind.Showed: return !string.IsNullOrEmpty(symptom) ? symptom : "It looks unwell.";
            case AnimalHealthEventKind.Worsened: return named ? $"{name} got worse ({stage})" : !string.IsNullOrEmpty(symptom) ? $"It got worse: {symptom}" : "It got worse.";
            case AnimalHealthEventKind.Improved: return named ? $"{name} is easing ({stage})" : "It seems better.";
            case AnimalHealthEventKind.Diagnosed: return $"Found to have {lower} ({stage})";
            case AnimalHealthEventKind.Treated: return $"Given {treatment ?? "medicine"}";
            case AnimalHealthEventKind.Vet: return "Seen by the veterinarian";
            case AnimalHealthEventKind.Recovering: return named ? $"Recovering from {lower}" : "Recovering";
            case AnimalHealthEventKind.Cleared: return named ? $"Over its {lower}" : "Well again";
            case AnimalHealthEventKind.Relapsed: return named ? $"{name} came back" : "It fell ill again";
            case AnimalHealthEventKind.Died: return named ? $"Died of {lower} ({stage})" : "Died";
            default: return "";
        }
    }

    // ---------------------------------------------------------------- what a keeper sees
    /// <summary>The symptoms a keeper sees of its illnesses: the first symptom of each that shows, then their others, at most <paramref name="max"/>.</summary>
    public static void Symptoms(AnimalRecord a, IAnimalCatalog cat, List<string> into, int max = 6)
    {
        if (a?.conditions == null || cat == null || into == null || max <= 0) return;
        int added = 0;
        // First pass: the first symptom of each illness that shows; second pass: their other symptoms.
        for (int pass = 0; pass < 2; pass++)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null || !k.shown) continue;
                AnimalConditionData c = cat.Condition(k.id);
                if (c == null) continue;
                AnimalConditionStage s = c.StageOf(k);
                if (!s.visible || s.symptoms == null) continue;
                int seen = 0;
                foreach (string sym in s.symptoms)
                {
                    if (string.IsNullOrWhiteSpace(sym)) continue;
                    bool first = seen++ == 0;
                    if (first != (pass == 0) || into.Contains(sym)) continue;
                    into.Add(sym);
                    if (++added >= max) return;
                }
            }
    }

    /// <summary>The names of the medicines that act on an illness ("Antidote, Charcoal"), or "".</summary>
    public static string TreatmentsFor(IAnimalCatalog cat, AnimalConditionData c)
    {
        if (cat?.AllTreatments == null || c == null) return "";
        var names = new List<string>();
        foreach (AnimalTreatmentData t in cat.AllTreatments)
            if (t != null && t.EffectOn(c.id) != null && !names.Contains(t.DisplayName)) names.Add(t.DisplayName);
        return string.Join(", ", names);
    }

    /// <summary>
    /// A named illness, as a careful look tells it: "{Name} ({stage}): {description} It likely comes from {cause}. Treat with:
    /// {medicines}." and, when they hold, " It needs a veterinarian." (only while one is in the world), " The treatment is
    /// working.", " It is recovering."
    /// </summary>
    public static string Diagnosis(AnimalConditionData c, AnimalCondition k, IAnimalCatalog cat, AnimalRules rules, double now, bool vetAvailable)
    {
        if (c == null || k == null) return "";
        string desc = string.IsNullOrWhiteSpace(c.description) ? "" : " " + c.description.Trim();
        string line = $"{c.DisplayName} ({c.StageName(k)}):{desc}";
        if (k.cause != AnimalCause.None) line += $" It likely comes from {Phrase(k.cause)}.";
        string meds = TreatmentsFor(cat, c);
        if (meds.Length > 0) line += $" Treat with: {meds}.";
        if (Gated(k, c, rules, vetAvailable)) line += " It needs a veterinarian.";
        if (k.Treated(now)) line += " The treatment is working.";
        if (k.Recovering) line += " It is recovering.";
        return line;
    }

    // ---------------------------------------------------------------- commands: examine, treat, the vet
    /// <summary>
    /// The whole effect of a careful look (Examine, where the rules run): every illness that shows from its diagnose-from
    /// stage on is named (diagnosed: logged once, by <paramref name="by"/>; one not yet marked shown is, with its Showed
    /// entry); the lines (at most 12) say each named one (<see cref="Diagnosis"/>; also a named one that showed and is now
    /// below its visible stages), or "Something is wrong (...); it is too early to say what.", or "It seems healthy.", then
    /// its hooves, coat, condition and welfare. Its last examination time and care history are set; affinity is not changed.
    /// Returns the message.
    /// </summary>
    public static string Examine(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules, double now, string by, string byName,
        bool vetAvailable, List<string> lines)
    {
        if (a == null) return "";
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        var said = new List<string>();
        bool any = false;
        if (a.conditions != null && cat != null)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null) continue;
                AnimalConditionData c = cat.Condition(k.id);
                if (c == null) continue;
                AnimalConditionStage s = c.StageOf(k);
                if (k.Recovering)
                {
                    if (!k.shown) continue;
                    any = true;
                    said.Add(k.diagnosed ? Diagnosis(c, k, cat, rules, now, vetAvailable) : "It is mending from an illness.");
                    continue;
                }
                if (!s.visible)
                {
                    // A named illness that showed is still told below its visible stages (treated down, mending); one
                    // never seen stays unsaid.
                    if (k.diagnosed && k.shown)
                    {
                        any = true;
                        said.Add(Diagnosis(c, k, cat, rules, now, vetAvailable));
                    }
                    continue;
                }
                any = true;
                if (!k.shown)
                {
                    // Seen now: the history says so, as when its stage first shows (no notice: the one looking sees it).
                    k.shown = true;
                    LogEvent(a, c, k, AnimalHealthEventKind.Showed, now, "", rules);
                }
                if (!k.diagnosed && k.stage >= c.diagnoseFrom)
                {
                    k.diagnosed = true;
                    LogEvent(a, c, k, AnimalHealthEventKind.Diagnosed, now, by, rules);
                }
                said.Add(k.diagnosed ? Diagnosis(c, k, cat, rules, now, vetAvailable) : $"Something is wrong ({Uncapital(s.Symptom)}); it is too early to say what.");
            }
        if (!any) said.Add("It seems healthy.");
        if (AnimalCare.CareOf(sp).HasHooves && a.hooves < 40f) said.Add("Its hooves are overgrown and cracked.");
        if (a.hygiene < 35f) said.Add("Its coat is filthy.");
        if (a.nutrition < rules.malnourishedBelow) said.Add("It is too thin.");
        said.Add($"Its welfare: {AnimalWelfare.WordOf(a, rules)}.");
        if (lines != null)
            for (int i = 0; i < said.Count && i < 12; i++) lines.Add(said[i]);
        a.lastExaminedAt = now;
        string who = string.IsNullOrWhiteSpace(byName) ? "" : $" by {byName.Trim()}";
        AnimalCare.Note(a, "examined", by, $"Looked over{who}.", now, rules);
        a.revision++;
        return $"You look {AnimalSim.NameIn(a, sp)} over.";
    }

    private static string Uncapital(string s)
    {
        if (string.IsNullOrEmpty(s)) return "it looks unwell";
        s = s.TrimEnd('.');
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>How many doses an animal takes of a medicine (its appetite: a cow about 4, a hen 0.2).</summary>
    public static float DosesFor(AnimalSpeciesData sp, AnimalRecord a) => AnimalSim.Appetite(sp, sp?.BreedOrDefault(a?.breed), a);

    /// <summary>The items of a medicine <paramref name="doses"/> doses take (at least 1; for a herd, the sum of its animals' doses).</summary>
    public static int ItemsFor(AnimalTreatmentData tr, float doses) =>
        tr == null ? 1 : Math.Max(1, Mathf.CeilToInt(Mathf.Max(0f, doses) / Mathf.Max(0.05f, tr.dosesPerItem) - 1e-4f));

    /// <summary>
    /// How strong the dose is: the mean over the copies taken of (quality matters ? 0.4 + 1.6 x quality : 1), x min(1,
    /// copies taken / copies needed). (A herd: each animal gets it.)
    /// </summary>
    public static float Strength(AnimalTreatmentData tr, AnimalTaken taken, int needed)
    {
        if (tr == null || taken.count <= 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < taken.count; i++) sum += tr.qualityMatters ? 0.4f + 1.6f * AnimalMath.Clamp01(taken.Quality(i)) : 1f;
        return sum / taken.count * Mathf.Min(1f, taken.count / (float)Math.Max(1, needed));
    }

    /// <summary>
    /// A dose of treatment on an illness: <paramref name="power"/> severity off per game hour for <paramref name="hours"/>.
    /// With doses already working, each game hour gets the strongest dose working then: the strongest works to its own end,
    /// then the next strongest still working to its own end, and so on. So a weak dose never stretches a strong one (given
    /// once or again and again), and a strong one never wastes a weak one. The record holds these steps exactly: the
    /// strongest in <see cref="AnimalCondition.treatPower"/> to <see cref="AnimalCondition.treatedUntil"/>, the next in
    /// <see cref="AnimalCondition.thenPower"/> to <see cref="AnimalCondition.thenUntil"/>, any more in
    /// <see cref="AnimalCondition.laterSteps"/> (at most <see cref="MaxDoseSteps"/> in all: past that, the weakest, which
    /// work last, are let go). A dose with no power per hour adds no step.
    /// </summary>
    public static void Dose(AnimalCondition k, double now, float power, float hours)
    {
        if (k == null) return;
        // The doses working from now on, each to its own end, this one too, strongest first (of two alike, the longer).
        doseSteps.Clear();
        doseSteps.Add((k.treatPower, k.treatedUntil));
        doseSteps.Add((k.thenPower, k.thenUntil));
        if (k.laterSteps != null)
            foreach (AnimalDoseStep s in k.laterSteps)
                if (s != null) doseSteps.Add((s.power, s.until));
        doseSteps.Add((power, now + Math.Max(0f, hours)));
        doseSteps.Sort((x, y) => x.power != y.power ? y.power.CompareTo(x.power) : y.until.CompareTo(x.until));
        // Each weaker one counts only from where the stronger ones before it end; one with no power, or ended, is none.
        int n = 0;
        for (int i = 0; i < doseSteps.Count && n < MaxDoseSteps; i++)
        {
            (float power, double until) d = doseSteps[i];
            if (d.power > 0f && d.until > now && (n == 0 || d.until > doseSteps[n - 1].until)) doseSteps[n++] = d;
        }
        k.treatPower = n > 0 ? doseSteps[0].power : 0f;
        k.treatedUntil = n > 0 ? doseSteps[0].until : now;
        k.thenPower = n > 1 ? doseSteps[1].power : 0f;
        k.thenUntil = n > 1 ? doseSteps[1].until : -1.0;
        k.laterSteps ??= new List<AnimalDoseStep>();
        k.laterSteps.Clear();
        for (int i = 2; i < n; i++) k.laterSteps.Add(new AnimalDoseStep { power = doseSteps[i].power, until = doseSteps[i].until });
    }

    /// <summary>The most steps an illness's treatment holds (<see cref="Dose"/>): more doses of different power at once than any keeper gives.</summary>
    public const int MaxDoseSteps = 8;

    private static readonly List<(float power, double until)> doseSteps = new List<(float power, double until)>(MaxDoseSteps + 2);

    /// <summary>What of an effect a dose keeps (<paramref name="good"/> 0: a dose too soon): all of a harm (below 0), and of a good (above 0) that share.</summary>
    private static float Kept(float effect, float good) => effect > 0f ? effect * good : effect;

    /// <summary>
    /// How long, in game hours, before a medicine given to an animal does its good again (0: now): its Min Interval Hours
    /// after its last dose that did its good. A dose recorded after <paramref name="now"/> (a clock set back) makes no wait,
    /// and neither do the last few milliseconds of game time (so a keeper who comes back when told is never refused for the
    /// rounding of the clock).
    /// </summary>
    public static double WaitBefore(AnimalRecord a, AnimalTreatmentData tr, double now)
    {
        AnimalDose last = a != null && tr != null ? a.LastDose(tr.id) : null;
        if (last == null || now < last.at) return 0.0;
        double since = now - last.at;
        return since < tr.minIntervalHours - 1e-6 ? tr.minIntervalHours - since : 0.0;
    }

    /// <summary>
    /// The whole effect of a dose of medicine on one animal (Treat, where the rules run; <paramref name="strength"/> from
    /// <see cref="Strength"/>). A topical one works x 0.6 on a filthy coat (hygiene below 30). A second dose of the same
    /// medicine within its interval of its last dose that did its good (<see cref="WaitBefore"/>) is too soon: it does no more
    /// good at once (no immediate severity, toxins, upset, health, comfort, calm, satiety or hydration), only harm (where its
    /// Overdose Share is above 0, toxin + overdose share x strength x its tolerance; any sting or unease it brings; the stress
    /// of being dosed), its per-hour power goes on (so it is never a quick cure given again and again), and it does not put
    /// off the next dose (the commands refuse one too soon of a medicine with no Overdose Share). Otherwise: toxins cleared,
    /// upset eased, health, comfort, stress (and the stress of being dosed), satiety and hydration; each illness it acts on
    /// loses its immediate severity (never below its Needs Vet Above while <see cref="Gated"/>). Either way each illness it
    /// acts on is treated for the effect's hours at its per-hour power (each game hour gets the strongest dose working then:
    /// see <see cref="Dose"/>). The health and care histories, today's care count, the dose (one that did its good) and (its
    /// owner, when it helped, at most once per interval) affinity are recorded. It works on
    /// hidden illnesses too, but "It does not seem to help." and "This is beyond your medicine" are judged only by those that
    /// have shown (the keeper is never told of one still hidden). Returns the message ("Daisy takes the antidote.", or for a
    /// topical one "You put the healing salve on Daisy."); <paramref name="lines"/> gets the rest.
    /// </summary>
    public static string Treat(AnimalRecord a, AnimalSpeciesData sp, AnimalTreatmentData tr, IAnimalCatalog cat, AnimalRules rules, float strength, bool owner,
        string by, string byName, double now, bool vetAvailable, List<string> lines)
    {
        if (a == null || tr == null) return "";
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        string name = AnimalSim.NameOf(a, sp);
        if (tr.route == AnimalTreatmentRoute.Topical && a.hygiene < 30f) strength *= 0.6f;
        strength = Mathf.Max(0f, strength);
        bool overdose = WaitBefore(a, tr, now) > 0.0;
        if (overdose && tr.overdoseShare > 0f)
        {
            a.toxin += tr.overdoseShare * strength * AnimalSim.Tolerance(a, sp);
            lines?.Add("Too much, too soon: the dose makes it unwell.");
        }
        else if (overdose) lines?.Add("Too soon for another dose: it does no more good yet.");
        // Given again within its interval it does no more good at once: only what it does harm (the overdose, a sting, being handled).
        float good = overdose ? 0f : 1f;
        a.toxin = Mathf.Max(0f, a.toxin * (1f - AnimalMath.Clamp01(tr.toxinCleared) * Mathf.Min(1f, strength) * good));
        a.upset = AnimalMath.Clamp100(a.upset - Kept(tr.upsetEased, good) * strength);
        a.health = AnimalMath.Clamp100(a.health + Kept(tr.healthNow, good) * strength);
        a.comfort = AnimalMath.Clamp100(a.comfort + Kept(tr.comfort, good) * strength);
        float temperament = a.genes != null ? a.genes.temperament : 0.5f;
        a.stress = AnimalMath.Clamp100(a.stress - Kept(-tr.stress, good) * strength + tr.handlingStress * (1f - temperament));
        a.satiety = AnimalMath.Clamp100(a.satiety + Kept(tr.satiety, good) * strength);
        a.hydration = AnimalMath.Clamp100(a.hydration + Kept(tr.hydration, good) * strength);
        // The dose first, then what it did (History reads newest first: "easing" above "Given ...").
        Log(a, AnimalHealthEventKind.Treated, now, "", "", EntryText(null, null, AnimalHealthEventKind.Treated, tr.DisplayName.ToLowerInvariant()), by, rules.keepHealthLog);

        // It works on hidden illnesses too, but what the keeper is told comes only from those they can know of (that showed).
        bool matched = false, helps = false, beyond = false;
        if (a.conditions != null && cat != null)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null || k.Recovering) continue;
                AnimalTreatmentEffect e = tr.EffectOn(k.id);
                if (e == null) continue;
                AnimalConditionData c = cat.Condition(k.id);
                if (c == null) continue;
                matched = true;
                if (k.shown) helps = true;
                int old = k.stage;
                if (e.immediate > 0f && !overdose)
                {
                    float sev = k.severity - e.immediate * strength;
                    if (Gated(k, c, rules, vetAvailable))
                    {
                        sev = Mathf.Max(sev, c.needsVetAbove);
                        if (k.shown) beyond = true;
                    }
                    k.severity = AnimalMath.Clamp100(Mathf.Min(k.severity, sev));
                }
                else if (Gated(k, c, rules, vetAvailable) && k.shown) beyond = true;
                Dose(k, now, e.perHour * strength, e.hours);
                k.treatment = tr.id;
                k.stage = c.StageAt(k.severity);
                if (k.stage < old) LogEvent(a, c, k, AnimalHealthEventKind.Improved, now, by, rules);
                if (k.severity <= 0f)
                {
                    k.severity = 0f;
                    k.recoveringUntil = now + c.recoveryHours;
                    LogEvent(a, c, k, AnimalHealthEventKind.Recovering, now, by, rules);
                }
            }
        if (!helps) lines?.Add("It does not seem to help.");
        if (beyond) lines?.Add("This is beyond your medicine: send for the veterinarian.");
        if (owner)
        {
            if (matched && !overdose) AnimalCare.GiveAffinity(a, 1f, now, rules);
            a.lastOwnerCareAt = now;
        }
        // Only a dose that did its good starts the wait for the next (one given too soon puts nothing off).
        // One last dose per medicine, as many as the catalog has, so none is ever forgotten while it is one (with no catalog,
        // as many as Keep Doses, 16 at least).
        if (!overdose) a.Dosed(new AnimalDose { treatment = tr.id, at = now, strength = strength }, Math.Max(rules.keepDoses, cat?.AllTreatments?.Count ?? 0));
        string who = string.IsNullOrWhiteSpace(byName) ? "" : $" by {byName.Trim()}";
        AnimalCare.Note(a, "treated", by, $"Given {tr.DisplayName.ToLowerInvariant()}{who}.", now, rules);
        AnimalCare.Count(a, now, owner, rules);
        a.revision++;
        string medicine = tr.DisplayName.ToLowerInvariant();
        // A wash or a salve is put on it; the rest it takes.
        return tr.route == AnimalTreatmentRoute.Topical ? $"You put the {medicine} on {AnimalSim.NameIn(a, sp)}." : $"{name} takes the {medicine}.";
    }

    /// <summary>
    /// The whole effect of the veterinarian's visit (CallVet, where the rules run, after the fee was paid): every illness,
    /// hidden early ones too, is named, loses the rules' Vet Immediate, is treated for Vet Hours at Vet Per Hour (a dose: the
    /// stronger dose works first; the other adds only what it would do after it ends, see <see cref="Dose"/>), and counts
    /// as seen by the vet (keeper medicine can then turn it); one brought to 0 begins recovering. The lines: "The
    /// veterinarian examines {name}.", "{Name}: treated." for each, "Mind {cause}." for each cause, or "Nothing wrong with
    /// it.". Logged in the health and care histories and the event log (SeenByVet). Returns the message.
    /// </summary>
    public static string VetVisit(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules, double now, string by, string byName, List<string> lines)
    {
        if (a == null) return "";
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        string name = AnimalSim.NameOf(a, sp), nameIn = AnimalSim.NameIn(a, sp);
        lines?.Add($"The veterinarian examines {nameIn}.");
        // The visit first, then what it did (History reads newest first: "Recovering" above "Seen by the veterinarian").
        Log(a, AnimalHealthEventKind.Vet, now, "", "", EntryText(null, null, AnimalHealthEventKind.Vet), by, rules.keepHealthLog);
        causeOrder.Clear();
        int treated = 0;
        bool known = false;
        if (a.conditions != null && cat != null)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null) continue;
                AnimalConditionData c = cat.Condition(k.id);
                if (c == null) continue;
                known = true;
                k.diagnosed = true;
                k.shown = true;
                k.byVet = true;
                if (k.Recovering)
                {
                    lines?.Add($"{c.DisplayName}: on the mend.");
                    continue;
                }
                k.severity = Mathf.Max(0f, k.severity - rules.vetImmediate);
                Dose(k, now, rules.vetPerHour, rules.vetHours);
                k.treatment = "vet";
                k.stage = c.StageAt(k.severity);
                if (k.severity <= 0f)
                {
                    k.severity = 0f;
                    k.recoveringUntil = now + c.recoveryHours;
                    LogEvent(a, c, k, AnimalHealthEventKind.Recovering, now, by, rules);
                }
                treated++;
                lines?.Add($"{c.DisplayName}: treated.");
                if (k.cause != AnimalCause.None && !causeOrder.Contains(k.cause)) causeOrder.Add(k.cause);
            }
        foreach (AnimalCause cause in causeOrder) lines?.Add($"Mind {Phrase(cause)}.");
        // Only illnesses the game still knows count (one whose data is gone is nothing the vet can find).
        if (treated == 0 && !known) lines?.Add("Nothing wrong with it.");
        causeOrder.Clear();
        a.lastVetAt = now;
        string who = string.IsNullOrWhiteSpace(byName) ? "" : $", sent for by {byName.Trim()}";
        AnimalCare.Note(a, "vet", by, $"Seen by the veterinarian{who}.", now, rules);
        AnimalCare.Count(a, now, !string.IsNullOrEmpty(by) && a.owner == by, rules);
        a.Log(AnimalEventKind.SeenByVet, now, $"{name} was seen by the veterinarian.", by, rules.keepEvents);
        return $"The veterinarian sees to {nameIn}.";
    }
}
