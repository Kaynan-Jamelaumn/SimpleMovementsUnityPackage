using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One care action as a species takes it: its id (unique in the species: "brush", "wash", "pet", "clean_hooves", or a
/// species' own), the tools, water and supply it uses, how often it can be done, what it does, and the harm of too much of
/// it or of the wrong kind. Its <see cref="kind"/> is its family (the defaults for its label, verb, message and icon).
/// </summary>
[Serializable]
public class AnimalCareActionSpec
{
    [Tooltip("Its id, unique in its species (\"brush\", \"wash\", \"pet\", \"clean_hooves\" or a species' own): requests name it. Empty: from the kind.")]
    public string id = "";
    [Tooltip("Its family: the default label, verb, message and icon.")]
    public AnimalCareKind kind;
    [Tooltip("Shown on its button (empty: from the kind).")]
    public string label = "";
    [Tooltip("What was done, for \"Daisy was brushed 2 hours ago\" (empty: from the kind).")]
    public string pastVerb = "";
    [Tooltip("What is said when it is done and there is no reaction ({name} is the animal: its name, or \"the cow\"; the first letter is made a capital; empty: from the kind).")]
    public string message = "";
    [Tooltip("Tool tags, any one of them (empty: bare hands: no tool is needed or worn).")]
    public List<string> tools = new List<string>();
    [Tooltip("Litres of water for an average adult (a young one needs less, by its size). 0: none.")]
    [Min(0f)] public float waterLitres;
    [Tooltip("A supply tag that makes it work better when the player chooses to use one (\"soap\"). Empty: none.")]
    public string supply = "";
    [Tooltip("x its cleaning (and parasite relief) with the supply.")]
    [Min(1f)] public float supplyBoost = 1.5f;
    [Tooltip("Game hours before it can be done again (refused before).")]
    [Min(0f)] public float cooldownHours = 6f;
    [Tooltip("The youngest stage it can be done to.")]
    public GrowthStage minStage = GrowthStage.Newborn;

    [Header("What it does")]
    public float hygiene;
    public float comfort;
    public float happiness;
    [Tooltip("Affinity with its owner (only when its owner does it; at most the rules' Affinity Per Day in a day).")]
    public float affinity;
    public float stress;
    [Tooltip("Hoof condition (only for species with hooves).")]
    public float hooves;
    [Tooltip("Share of the build-up of worms and lice it removes.")]
    [Range(0f, 1f)] public float parasiteRelief;
    [Tooltip("Extra stress for a skittish or unfamiliar animal (x (1 - temperament) x (1 - affinity/100)).")]
    [Min(0f)] public float handlingStress;

    [Header("Too much of it")]
    [Tooltip("Game hours over which repeats count.")]
    [Min(0.5f)] public float overuseHours = 24f;
    [Tooltip("Repeats within that time before it does harm.")]
    [Min(1)] public int overuseAfter = 2;
    [Tooltip("Stress, comfort and hygiene each repeat beyond Overuse After adds (negative comfort: it hurts).")]
    public float overuseStress;
    public float overuseComfort;
    public float overuseHygiene;

    [Header("Cold and heat")]
    [Tooltip("°C below which this chills it (washing in the cold). -100: never.")]
    public float chillBelow = -100f;
    [Tooltip("Comfort lost per °C below Chill Below (at most 10 °C count). An unsuitable action's own comfort may already hold the chill: then 0.")]
    [Min(0f)] public float chillComfort = 3f;
    [Tooltip("°C above which it is a pleasure (a pig's wallow on a hot day). 100: never.")]
    public float heatAbove = 100f;
    [Tooltip("Comfort gained per °C above Heat Above (at most 10 °C count).")]
    [Min(0f)] public float heatComfort;

    [Header("Wrong for it")]
    [Tooltip("It is allowed, but bad for this species (a bathed rabbit panics): the player is told.")]
    public bool inappropriate;
    [Tooltip("What is seen ({name} is the animal, as in Message). Empty: its message.")]
    public string reaction = "";

    /// <summary>Its id (its kind's when none is given).</summary>
    public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : AnimalCare.KindId(kind);
    public string Label => !string.IsNullOrWhiteSpace(label) ? label : AnimalCare.KindLabel(kind);
    public string PastVerb => !string.IsNullOrWhiteSpace(pastVerb) ? pastVerb : AnimalCare.KindVerb(kind);
    public bool NeedsTool => tools != null && tools.Count > 0;
    public bool UsesWater => waterLitres > 0f;
    public bool TakesSupply => !string.IsNullOrWhiteSpace(supply);
}

/// <summary>How a species keeps clean and comfortable, what bothers it, and the care it takes.</summary>
[Serializable]
public class AnimalCareSpec
{
    [Header("Hygiene")]
    [Tooltip("Hygiene an adult loses per game hour while it is cleaner than its resting level (faster on a dirty floor).")]
    [Min(0f)] public float hygieneLossPerHour = 0.4f;
    [Tooltip("Hygiene it regains on its own per game hour (preening, dust baths, licking) while it is below its resting level.")]
    [Min(0f)] public float selfGroomPerHour = 0.5f;
    [Tooltip("Its resting level: the hygiene it keeps itself at in a clean pen without care (a dirty floor, mud and loose droppings lower it).")]
    [Range(0f, 100f)] public float selfGroomCap = 55f;
    [Tooltip("How much it soils its pen (1 = typical; a pig 1.3).")]
    [Min(0f)] public float messiness = 1f;
    [Tooltip("How much mud on an open pen in the rain dirties it.")]
    [Min(0f)] public float mudSensitivity = 1f;
    [Tooltip("How much fresh, dry bedding lets it clean itself (hens dust-bathe): up to +25 on its resting level.")]
    [Range(0f, 1f)] public float dustBath;
    [Tooltip("How much a pond lets it clean itself (ducks preen): +25 x this on its resting level in a pond.")]
    [Range(0f, 1f)] public float bathes;

    [Header("Hooves")]
    [Tooltip("Hoof condition lost per game hour (faster on a dirty floor; 0: no hooves to care for).")]
    [Min(0f)] public float hoofWearPerHour;

    [Header("Comfort")]
    [Tooltip("Comfortable air temperature, °C: from...")]
    public float comfortMin;
    [Tooltip("...to.")]
    public float comfortMax = 25f;
    [Tooltip("How much it minds rain without shelter (0-1).")]
    [Range(0f, 1f)] public float rainDislike = 0.4f;
    [Tooltip("How much it misses bedding in a bedded pen (0-1).")]
    [Range(0f, 1f)] public float beddingNeed = 0.5f;
    [Tooltip("Share of its pen's room in use from which it feels crowded.")]
    [Range(0.1f, 1.5f)] public float crowdedFrom = 0.8f;
    [Tooltip("Others of its kind it wants in its pen (0: content alone).")]
    [Min(0)] public int companionsWanted;

    [Header("Care it takes")]
    public List<AnimalCareActionSpec> actions = new List<AnimalCareActionSpec>();

    public bool HasHooves => hoofWearPerHour > 0f;

    /// <summary>Its care action of this id (case-insensitive), or null.</summary>
    public AnimalCareActionSpec Action(string id)
    {
        if (actions == null || string.IsNullOrWhiteSpace(id)) return null;
        string want = id.Trim();
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] != null && string.Equals(actions[i].Id, want, StringComparison.OrdinalIgnoreCase)) return actions[i];
        return null;
    }

    /// <summary>Its first care action of this family, or null.</summary>
    public AnimalCareActionSpec Action(AnimalCareKind k)
    {
        if (actions == null) return null;
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] != null && actions[i].kind == k) return actions[i];
        return null;
    }

    /// <summary>What is wrong with it (<paramref name="who"/> names the species in the messages).</summary>
    public void Validate(string who, List<string> errors, List<string> warnings)
    {
        if (comfortMin >= comfortMax) errors?.Add($"{who}: its comfortable range is empty ({comfortMin:0.#} to {comfortMax:0.#} °C).");
        if (selfGroomPerHour < hygieneLossPerHour)
            warnings?.Add($"{who}: it grooms itself back ({selfGroomPerHour:0.##}/h) slower than it gets dirty ({hygieneLossPerHour:0.##}/h): rain and mud keep it dirty for long.");
        if (actions == null) return;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < actions.Count; i++)
        {
            AnimalCareActionSpec s = actions[i];
            if (s == null) { warnings?.Add($"{who}: an empty care action."); continue; }
            if (!Enum.IsDefined(typeof(AnimalCareKind), s.kind)) errors?.Add($"{who}: care action {i + 1} is of an unknown kind ({(int)s.kind}).");
            string id = s.Id;
            if (s.kind == AnimalCareKind.Custom && string.IsNullOrWhiteSpace(s.id)) errors?.Add($"{who}: a custom care action needs an id.");
            else if (!AnimalIds.ValidKey(id)) errors?.Add($"{who}: the care action id '{id}' may not be empty or hold spaces, '/', ':' or '|'.");
            else if (!ids.Add(id)) errors?.Add($"{who}: two care actions are called '{id}'.");
            if (s.cooldownHours <= 0f) warnings?.Add($"{who}: '{id}' has no cooldown: it can be repeated at once.");
            if (s.overuseAfter < 1) errors?.Add($"{who}: '{id}' does harm before it is ever done (Overuse After below 1).");
            if (s.waterLitres < 0f) errors?.Add($"{who}: '{id}' takes negative water.");
            if (s.hooves != 0f && !HasHooves) warnings?.Add($"{who}: '{id}' cares for hooves, but it has none (Hoof Wear Per Hour 0).");
            if (s.TakesSupply && s.supplyBoost <= 1f) warnings?.Add($"{who}: '{id}' takes {s.supply}, which does nothing (Supply Boost 1).");
            if (s.tools != null)
                foreach (string t in s.tools)
                    if (string.IsNullOrWhiteSpace(t)) warnings?.Add($"{who}: '{id}' names an empty tool tag.");
        }
    }
}

/// <summary>
/// A tool or supply used in care (a brush, a pitchfork, soap, straw), matched by item key like foods: its tags say what it
/// is used as; a tool is worn (its uses are the item's durability), a supply used up.
/// </summary>
[Serializable]
public class AnimalToolData
{
    public string id = "";
    public string name = "";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("What it is used as: brush, soft_brush, curry_comb, hoof_pick, fork, shovel, soap, bedding.")]
    public List<string> tags = new List<string>();
    [Tooltip("Item keys that are this tool (\"animal:tool/soft_brush\", \"agri:tool/shovel\"; a key ending in * is a prefix).")]
    public List<string> items = new List<string>();
    [Tooltip("Used up one per use (a supply), instead of worn (a tool).")]
    public bool consumed;
    [Tooltip("How well it does the work (see AnimalCare.ToolFactor).")]
    [Range(0f, 1f)] public float quality = 0.6f;
    [Tooltip("Uses before it is worn out (tools of animal husbandry's own; 0 for other systems' items).")]
    [Min(0)] public int uses = 150;
    [Tooltip("What it costs (an item of animal husbandry's own).")]
    [Min(0)] public int value = 10;
    [Min(0f)] public float weight = 0.6f;

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;

    public bool HasTag(string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag)) return false;
        for (int i = 0; i < tags.Count; i++)
            if (string.Equals(tags[i], tag.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool HasAny(IReadOnlyList<string> want)
    {
        if (want == null) return false;
        for (int i = 0; i < want.Count; i++)
            if (HasTag(want[i])) return true;
        return false;
    }

    /// <summary>Is this item key this tool (exactly, or by a key of it ending in *)?</summary>
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

    public void Validate(List<string> errors, List<string> warnings)
    {
        string who = string.IsNullOrEmpty(id) ? "(a tool without an id)" : $"tool '{id}'";
        if (!AnimalIds.ValidKey(id)) errors?.Add($"{who}: the id may not be empty or hold spaces, '/', ':' or '|'.");
        if (tags == null || tags.Count == 0) warnings?.Add($"{who}: no tags (no care action can use it).");
        if (items == null || items.Count == 0) warnings?.Add($"{who}: no item is this tool (it can never be used).");
    }
}

/// <summary>The last time a care action (by id) was done to an animal, and how much of it it had lately (overuse).</summary>
[Serializable]
public class AnimalCareMark
{
    public string id = "";
    public double at = -1.0;
    [Tooltip("Repeats lately, fading over the action's Overuse Hours.")]
    public float recent;
}

/// <summary>One entry of an animal's care history.</summary>
[Serializable]
public class AnimalCareEntry
{
    public double at;
    [Tooltip("What was done (a care action's id, \"cleaned\", \"fed\", \"examined\", \"treated\", \"vet\").")]
    public string what = "";
    [Tooltip("Who did it (a lasting key).")]
    public string by = "";
    public string text = "";
}

/// <summary>The best tool a player carries for a job (<see cref="has"/> false: none).</summary>
public struct AnimalToolUse
{
    public bool has;
    /// <summary>Its item key (to wear it after the job).</summary>
    public string key;
    /// <summary>The tool's id (<see cref="AnimalToolData.id"/>).</summary>
    public string tool;
    public float quality;
    /// <summary>What is left of it, 0-1 (0: worn out).</summary>
    public float edge;

    public AnimalToolUse(string key, string tool, float quality, float edge)
    {
        has = true;
        this.key = key ?? "";
        this.tool = tool ?? "";
        this.quality = quality;
        this.edge = edge;
    }
}

/// <summary>
/// The pure logic of care, used by the commands (one care action on one animal: <see cref="CanDo"/> then <see cref="Apply"/>)
/// and the simulation (an animal's hygiene, hooves, comfort and the stress its surroundings give it, each step). Every
/// number comes from the species' <see cref="AnimalCareSpec"/> and the <see cref="AnimalRules"/>.
/// </summary>
public static class AnimalCare
{
    /// <summary>The weather when there is no climate (15 °C, dry).</summary>
    public static readonly AgriWeather Mild = new AgriWeather { temperature = 15f, humidity = 0.6f, clouds = 0.4f, light = 0.5f, day = true };

    private static readonly AnimalCareSpec DefaultCare = new AnimalCareSpec();

    /// <summary>The share of the water a wash planned that must come (less: it is refused, nothing done).</summary>
    public const float LeastWater = 0.3f;

    /// <summary>The weather at an hour (the context's climate, or mild weather).</summary>
    public static AgriWeather WeatherAt(AnimalSimContext ctx, double now) => ctx?.climate != null ? ctx.climate.WeatherAt(now) : Mild;

    /// <summary>A species' care spec (never null).</summary>
    public static AnimalCareSpec CareOf(AnimalSpeciesData sp) => sp?.care ?? DefaultCare;

    // ---------------------------------------------------------------- the families' defaults
    public static string KindId(AnimalCareKind k)
    {
        switch (k)
        {
            case AnimalCareKind.Brush: return "brush";
            case AnimalCareKind.Wash: return "wash";
            case AnimalCareKind.Pet: return "pet";
            case AnimalCareKind.CleanHooves: return "clean_hooves";
            default: return "care";
        }
    }

    public static string KindLabel(AnimalCareKind k)
    {
        switch (k)
        {
            case AnimalCareKind.Brush: return "Brush";
            case AnimalCareKind.Wash: return "Wash";
            case AnimalCareKind.Pet: return "Pet";
            case AnimalCareKind.CleanHooves: return "Clean hooves";
            default: return "Care for it";
        }
    }

    public static string KindVerb(AnimalCareKind k)
    {
        switch (k)
        {
            case AnimalCareKind.Brush: return "brushed";
            case AnimalCareKind.Wash: return "washed";
            case AnimalCareKind.Pet: return "petted";
            case AnimalCareKind.CleanHooves: return "given hoof care";
            default: return "cared for";
        }
    }

    public static string KindMessage(AnimalCareKind k)
    {
        switch (k)
        {
            case AnimalCareKind.Brush: return "You brush {name}.";
            case AnimalCareKind.Wash: return "You wash {name}.";
            case AnimalCareKind.Pet: return "You pet {name}.";
            case AnimalCareKind.CleanHooves: return "You clean {name}'s hooves.";
            default: return "You see to {name}.";
        }
    }

    private static string Fill(string text, string name) => string.IsNullOrEmpty(text) ? "" : text.Replace("{name}", name);

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ---------------------------------------------------------------- tools
    /// <summary>
    /// How well a tool does the work: bare hands (or no tool needed) 1; a tool 0.6 + 0.6 x quality x (0.5 + 0.5 x edge)
    /// (a new soft brush 0.96, a new curry comb 1.08); a worn-out tool (edge 0) the rules' Worn Tool Factor, flat.
    /// </summary>
    public static float ToolFactor(AnimalToolUse use, AnimalRules rules)
    {
        if (!use.has) return 1f;
        if (use.edge <= 0f) return rules != null ? rules.wornToolFactor : 0.4f;
        return 0.6f + 0.6f * AnimalMath.Clamp01(use.quality) * (0.5f + 0.5f * AnimalMath.Clamp01(use.edge));
    }

    /// <summary>"a soft brush or a curry comb": the tools (not supplies) with any of the tags, else the tags in words.</summary>
    public static string ToolNames(IAnimalCatalog cat, IReadOnlyList<string> tags)
    {
        var names = new List<string>();
        if (tags != null && cat?.AllTools != null)
            foreach (AnimalToolData t in cat.AllTools)
                if (t != null && !t.consumed && t.HasAny(tags))
                {
                    string n = t.DisplayName.ToLowerInvariant();
                    if (!names.Contains(n)) names.Add(n);
                }
        if (names.Count == 0 && tags != null)
            for (int i = 0; i < tags.Count; i++)
                if (!string.IsNullOrWhiteSpace(tags[i])) names.Add(tags[i].Trim().Replace('_', ' ').ToLowerInvariant());
        if (names.Count == 0) return "a tool";
        for (int i = 0; i < names.Count; i++) names[i] = Article(names[i]) + " " + names[i];
        if (names.Count == 1) return names[0];
        return string.Join(", ", names.GetRange(0, names.Count - 1)) + " or " + names[names.Count - 1];
    }

    private static string Article(string word) => !string.IsNullOrEmpty(word) && "aeiou".IndexOf(word[0]) >= 0 ? "an" : "a";

    // ---------------------------------------------------------------- marks, sizes, water
    /// <summary>Its mark for a care action (by id), or null (reads only: for plans and views).</summary>
    public static AnimalCareMark FindMark(AnimalRecord a, string id)
    {
        if (a?.care == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < a.care.Count; i++)
            if (a.care[i] != null && string.Equals(a.care[i].id, id, StringComparison.OrdinalIgnoreCase)) return a.care[i];
        return null;
    }

    /// <summary>Its mark for a care action (by id), made when it has none (for commits).</summary>
    public static AnimalCareMark Mark(AnimalRecord a, string id)
    {
        AnimalCareMark m = FindMark(a, id);
        if (m != null || a == null) return m;
        a.care ??= new List<AnimalCareMark>();
        m = new AnimalCareMark { id = id ?? "" };
        a.care.Add(m);
        return m;
    }

    /// <summary>Its body's size against an average adult of its species: its stage's size x its breed's x its genes'.</summary>
    public static float SizeOf(AnimalSpeciesData sp, AnimalRecord a)
    {
        if (a == null) return 1f;
        float stage = sp?.StageOrNearest(a.stage)?.size ?? 1f;
        float breed = sp?.BreedOrDefault(a.breed)?.size ?? 1f;
        return Mathf.Max(0.01f, stage * breed * AnimalGenes.Scale(a.genes != null ? a.genes.size : 0.5f, 0.15f));
    }

    /// <summary>The litres a care action wants for this animal (its water for an average adult x its size).</summary>
    public static float WaterFor(AnimalCareActionSpec spec, AnimalSpeciesData sp, AnimalRecord a) =>
        spec != null && spec.waterLitres > 0f ? spec.waterLitres * SizeOf(sp, a) : 0f;

    /// <summary>The share of the planned water that came (1 when none was planned).</summary>
    public static float WaterFraction(float planned, float taken) => planned > 0f ? AnimalMath.Clamp01(taken / planned) : 1f;

    /// <summary>Did enough of the planned water come (at least <see cref="LeastWater"/> of it)?</summary>
    public static bool EnoughWater(float planned, float taken) => planned <= 0f || taken >= planned * LeastWater - 1e-4f;

    // ---------------------------------------------------------------- readiness (plans and views)
    /// <summary>
    /// Can this care action be done to it now? False and why: dead, boarded (the stable sees to it), its species does not
    /// take it, too young, or done lately (its cooldown). An unsuitable action (<see cref="AnimalCareActionSpec.inappropriate"/>)
    /// can be done: it only does harm. Changes nothing.
    /// </summary>
    public static bool CanDo(AnimalRecord a, AnimalSpeciesData sp, AnimalCareActionSpec spec, double now, out string why)
    {
        why = null;
        if (a == null) { why = "That animal is not here."; return false; }
        if (!a.Alive) { why = "It is dead."; return false; }
        if (a.status == AnimalStatus.Boarded || string.IsNullOrEmpty(a.pen)) { why = "The boarding stable sees to it."; return false; }
        if (spec == null) { why = $"{(sp != null ? sp.Plural : "They")} do not need that."; return false; }
        if (a.stage < spec.minStage) { why = "It is too young for that."; return false; }
        AnimalCareMark m = FindMark(a, spec.Id);
        if (m != null && m.at >= 0.0 && now - m.at < spec.cooldownHours)
        {
            why = $"{AnimalSim.NameOf(a, sp)} was {spec.PastVerb} {AnimalObservation.Age(now - m.at)} ago.";
            return false;
        }
        return true;
    }

    /// <summary>The window's hint for a care action: "brushed 2 hours ago", "too young", or "" (ready).</summary>
    public static string Hint(AnimalRecord a, AnimalCareActionSpec spec, double now)
    {
        if (a == null || spec == null) return "";
        if (a.stage < spec.minStage) return "too young";
        AnimalCareMark m = FindMark(a, spec.Id);
        if (m != null && m.at >= 0.0 && now - m.at < spec.cooldownHours) return $"{spec.PastVerb} {AnimalObservation.Age(now - m.at)} ago";
        return "";
    }

    // ---------------------------------------------------------------- one care action, committed
    /// <summary>
    /// The whole effect of ONE care action on ONE animal (where the rules run, after the plan said it may be done and the
    /// caller took the water and the supply). <paramref name="water"/> is the share of the planned water that came (1 when
    /// none was planned: see <see cref="WaterFraction"/>), <paramref name="supplied"/> whether the supply came, and
    /// <paramref name="owner"/> whether its owner did it. Returns the message; <paramref name="lines"/> (may be null) gets
    /// what else is worth saying (too much of it, the cold, an unsuitable action, a worn-out tool).
    /// <list type="bullet">
    /// <item>k = <see cref="ToolFactor"/>; boost = the supply's boost when supplied; repeats lately = recent x Decay(since the
    /// last time, overuse hours) + 1; over = max(0, repeats - overuse after); dim = 1 / (1 + over).</item>
    /// <item>hygiene += hygiene x k x water x boost x dim + overuse hygiene x over; comfort += comfort x k x water x dim +
    /// overuse comfort x over; happiness += happiness x dim; affinity (its owner only) += affinity x dim, at most the rules'
    /// Affinity Per Day in a game day; stress += stress + handling x (1 - temperament) x (1 - affinity/100) + overuse stress x
    /// over; hooves += hooves x k (hooved species); parasite build-up -= relief x k x boost.</item>
    /// <item>Cold: below its Chill Below, comfort -= chill comfort x chill and Chill exposure += chill / 10 x 6 (chill at most
    /// 10 °C). Heat: above its Heat Above, comfort += heat comfort x heat (at most 10 °C).</item>
    /// <item>Its mark, the care history, today's care counts and (its owner) <see cref="AnimalRecord.lastOwnerCareAt"/> are set.</item>
    /// </list>
    /// </summary>
    public static string Apply(AnimalRecord a, AnimalSpeciesData sp, AnimalCareActionSpec spec, IAnimalCatalog cat, AnimalRules rules, in AgriWeather w,
        AnimalToolUse tool, float water, bool supplied, bool owner, string by, string byName, double now, List<string> lines)
    {
        if (a == null || spec == null) return "";
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        AnimalCareSpec care = CareOf(sp);
        // {name} may fall anywhere in a message ("You brush the cow.", "Rabbits are not bathed: the rabbit panics."): the
        // finished message starts with a capital.
        string name = AnimalSim.NameIn(a, sp);
        float k = spec.NeedsTool ? ToolFactor(tool, rules) : 1f;
        float wet = spec.UsesWater ? AnimalMath.Clamp01(water) : 1f;
        float boost = supplied && spec.TakesSupply ? Mathf.Max(1f, spec.supplyBoost) : 1f;

        // Too much of it: repeats within its overuse hours count, fading.
        AnimalCareMark mark = Mark(a, spec.Id);
        mark.recent = mark.at < 0.0 ? 1f : mark.recent * AnimalMath.Decay(now - mark.at, Math.Max(0.5, spec.overuseHours)) + 1f;
        float over = Mathf.Max(0f, mark.recent - Mathf.Max(1, spec.overuseAfter));
        float dim = 1f / (1f + over);

        float stressBefore = a.stress;
        a.hygiene = AnimalMath.Clamp100(a.hygiene + spec.hygiene * k * wet * boost * dim + spec.overuseHygiene * over);
        a.comfort = AnimalMath.Clamp100(a.comfort + spec.comfort * k * wet * dim + spec.overuseComfort * over);
        a.happiness = AnimalMath.Clamp100(a.happiness + spec.happiness * dim);
        if (owner) GiveAffinity(a, spec.affinity * dim, now, rules);
        float temperament = a.genes != null ? a.genes.temperament : 0.5f;
        // Handling stresses a skittish or unfamiliar animal; its owner's handling less as their bond grows (none once it is calm with them).
        float handling = AnimalSocial.HandlingFactor(a, sp, owner, rules);
        a.stress = AnimalMath.Clamp100(a.stress + spec.stress + spec.handlingStress * (1f - temperament) * handling + spec.overuseStress * over);
        if (care.HasHooves && spec.hooves != 0f) a.hooves = AnimalMath.Clamp100(a.hooves + spec.hooves * k);
        if (spec.parasiteRelief > 0f) AnimalHealth.Relieve(a, cat, AnimalConditionKind.Parasites, spec.parasiteRelief * k * boost);

        bool chilled = false;
        if (w.temperature < spec.chillBelow)
        {
            float chill = Mathf.Min(10f, spec.chillBelow - w.temperature);
            a.comfort = AnimalMath.Clamp100(a.comfort - spec.chillComfort * chill);
            AnimalHealth.Expose(a, cat, sp, AnimalCause.Chill, chill / 10f * 6f, now);
            chilled = true;
        }
        bool cooled = false;
        if (w.temperature > spec.heatAbove && spec.heatComfort > 0f)
        {
            float heat = Mathf.Min(10f, w.temperature - spec.heatAbove);
            a.comfort = AnimalMath.Clamp100(a.comfort + spec.heatComfort * heat);
            cooled = true;
        }
        if (a.stress - stressBefore >= 3f) a.stressor = AnimalDiscomfort.Handling;

        mark.at = now;
        if (owner) a.lastOwnerCareAt = now;
        string who = string.IsNullOrWhiteSpace(byName) ? "" : $" by {byName.Trim()}";
        Note(a, spec.Id, by, $"{Capital(spec.PastVerb)}{who}.", now, rules);
        Count(a, now, owner, rules);
        a.revision++;

        string message = Capital(Fill(!string.IsNullOrWhiteSpace(spec.reaction) ? spec.reaction : !string.IsNullOrWhiteSpace(spec.message) ? spec.message : KindMessage(spec.kind), name));
        if (lines != null)
        {
            if (over > 0f) lines.Add("It has had enough of that for now.");
            if (chilled) lines.Add("It shivers: it is too cold to be wet.");
            if (cooled) lines.Add("The cool water is a relief in the heat.");
            if (spec.inappropriate) lines.Add("This does it no good.");
            if (spec.NeedsTool && tool.has && tool.edge <= 0f)
            {
                string t = cat?.Tool(tool.tool)?.DisplayName ?? "tool";
                lines.Add($"Your {t.ToLowerInvariant()} is worn out: it does a poor job.");
            }
        }
        return message;
    }

    /// <summary>Adds an entry to its care history (what, by whom, the text).</summary>
    public static void Note(AnimalRecord a, string what, string by, string text, double now, AnimalRules rules)
    {
        if (a == null) return;
        a.Cared(new AnimalCareEntry { at = now, what = what ?? "", by = by ?? "", text = text ?? "" }, (rules ?? AnimalRules.Defaults()).keepCareLog);
    }

    /// <summary>Counts care in today's record (and as its owner's care when <paramref name="owner"/>).</summary>
    public static void Count(AnimalRecord a, double now, bool owner, AnimalRules rules)
    {
        if (a == null) return;
        a.days ??= new List<AnimalDay>();
        AnimalDay d = a.Today((int)Math.Floor(now / 24.0));
        d.cares++;
        if (owner) d.ownerCares++;
        a.TrimDays((rules ?? AnimalRules.Defaults()).keepDays);
    }

    /// <summary>
    /// Raises its affinity with its owner by care or medicine: at most the rules' Affinity Per Day in a game day (today's
    /// record keeps count). A loss is taken whole. Returns what was given.
    /// </summary>
    public static float GiveAffinity(AnimalRecord a, float amount, double now, AnimalRules rules)
    {
        if (a == null || amount == 0f) return 0f;
        if (amount < 0f)
        {
            float before = a.affinity;
            a.affinity = AnimalMath.Clamp100(a.affinity + amount);
            return a.affinity - before;
        }
        a.days ??= new List<AnimalDay>();
        AnimalDay d = a.Today((int)Math.Floor(now / 24.0));
        float room = Mathf.Max(0f, (rules ?? AnimalRules.Defaults()).affinityPerDay - d.affinityGained);
        float give = Mathf.Min(amount, room, 100f - a.affinity);
        if (give <= 0f) return 0f;
        a.affinity = AnimalMath.Clamp100(a.affinity + give);
        d.affinityGained += give;
        return give;
    }

    // ---------------------------------------------------------------- each step (the simulation)
    /// <summary>
    /// The temperature it feels: in the cold its shelter's warmth helps (never above its comfortable minimum) and wind
    /// chills it where it is open; in the heat shade helps (never below its comfortable maximum). One rule for its comfort
    /// and for the Chill and Heat causes.
    /// </summary>
    public static float EffectiveTemperature(AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w, AnimalRules rules)
    {
        AnimalCareSpec care = CareOf(sp);
        rules ??= AnimalRules.Defaults();
        float t = w.temperature;
        float eff = t;
        float shelter = AnimalMath.Clamp01(f.shelter);
        if (t < care.comfortMin) eff = Mathf.Min(care.comfortMin, t + f.warmth * shelter) - rules.windChill * Mathf.Max(0f, w.wind) * (1f - shelter);
        else if (t > care.comfortMax) eff = Mathf.Max(care.comfortMax, t - 5f * AnimalMath.Clamp01(f.shade));
        return eff;
    }

    /// <summary>How crowded it is, 0-1 (none at the boarding stable).</summary>
    public static float Crowding(AnimalSpeciesData sp, in AnimalStepFacts f)
    {
        if (f.boarded) return 0f;
        AnimalCareSpec care = CareOf(sp);
        return AnimalMath.Smooth(care.crowdedFrom, care.crowdedFrom + 0.4f, f.density);
    }

    /// <summary>How lonely it is, 0-1: the share of the company its kind wants that it lacks (none at the boarding stable).</summary>
    public static float Loneliness(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f)
    {
        if (f.boarded) return 0f;
        AnimalCareSpec care = CareOf(sp);
        if (care.companionsWanted <= 0) return 0f;
        int same = 0;
        if (f.companions != null && a != null && f.companions.TryGetValue(a.species ?? "", out int n)) same = n;
        int others = Mathf.Max(0, same - 1);
        return AnimalMath.Clamp01((care.companionsWanted - others) / (float)care.companionsWanted);
    }

    /// <summary>Mud on it, 0-1: an open pen in the rain (less under what shelter it has).</summary>
    public static float Mud(AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w)
    {
        if (f.boarded || !f.open || !w.Raining) return 0f;
        return AnimalMath.Clamp01(CareOf(sp).mudSensitivity * (1f - AnimalMath.Clamp01(f.shelter)) * Mathf.Min(1f, w.rain / 2f));
    }

    /// <summary>
    /// The hygiene it settles toward on its own: its resting level, lowered by a dirty floor, mud and loose droppings,
    /// raised by fresh bedding (a dust-bather) or a pond (a bather). At the boarding stable: the rules' Boarding Hygiene.
    /// </summary>
    public static float HygieneRest(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w, AnimalRules rules)
    {
        rules ??= AnimalRules.Defaults();
        if (f.boarded) return rules.boardingHygiene;
        AnimalCareSpec care = CareOf(sp);
        float dirt = AnimalMath.Clamp100(f.dirt);
        float rest = care.selfGroomCap * (1f - 0.6f * dirt / 100f) * Mathf.Max(0f, 1f - 0.5f * Mud(sp, in f, in w));
        if (f.usesBedding) rest += 25f * care.dustBath * AnimalMath.Clamp100(f.bedding) / 100f;
        if (AnimalPenSpec.MainKind(f.kind) == AnimalHabitatKind.Pond) rest += 25f * care.bathes;
        rest -= 30f * AnimalMath.Clamp100(a != null ? a.upset : 0f) / 100f;
        return AnimalMath.Clamp100(rest);
    }

    /// <summary>
    /// Its hygiene over a step: above its resting level it gets dirtier (its loss rate, faster on a dirty floor, slower
    /// while its owner is away: <paramref name="pace"/>); below it, it grooms itself back; its illnesses (loose droppings)
    /// take their share.
    /// </summary>
    public static void StepHygiene(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w, AnimalRules rules, float dt, float pace,
        in AnimalStageEffects fx)
    {
        if (a == null || dt <= 0f) return;
        AnimalCareSpec care = CareOf(sp);
        float rest = HygieneRest(a, sp, in f, in w, rules);
        float dirt = AnimalMath.Clamp100(f.dirt);
        float h = a.hygiene;
        if (h > rest) h -= Mathf.Min(h - rest, care.hygieneLossPerHour * (1f + dirt / 100f) * pace * dt);
        else h += Mathf.Min(rest - h, care.selfGroomPerHour * dt);
        h -= Mathf.Max(0f, fx.hygienePerHour) * dt;
        a.hygiene = AnimalMath.Clamp100(h);
    }

    /// <summary>
    /// Its hooves over a step: worn by the hour (faster on a dirty floor, slower while its owner is away); at the boarding
    /// stable they settle at the rules' Boarding Hooves (the stable trims them). Species without hooves stay at 100.
    /// </summary>
    public static void StepHooves(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, AnimalRules rules, float dt, float pace)
    {
        if (a == null || dt <= 0f) return;
        AnimalCareSpec care = CareOf(sp);
        rules ??= AnimalRules.Defaults();
        if (!care.HasHooves)
        {
            a.hooves = 100f;
            return;
        }
        if (f.boarded) a.hooves = rules.boardingHooves + (a.hooves - rules.boardingHooves) * AnimalMath.Decay(dt, rules.boardingHoofHours);
        else a.hooves = AnimalMath.Clamp100(a.hooves - care.hoofWearPerHour * (1f + AnimalMath.Clamp100(f.dirt) / 100f) * pace * dt);
    }

    /// <summary>
    /// Where its comfort settles now (0-100): 100 less the cold and the heat (after shelter, warmth, shade and wind), rain
    /// on it unsheltered (a storm worse), crowding, a dirty floor, no bedding in a bedded pen, sore hooves and a dirty coat,
    /// plus its illnesses' share and its pen level's comfort (Phase 5); at the boarding stable the rules' Boarding Comfort plus
    /// its illnesses'. <paramref name="worst"/>
    /// is its largest penalty (what a keeper sees bothers it), or None.
    /// </summary>
    public static float ComfortTarget(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w, AnimalRules rules, in AnimalStageEffects fx,
        out AnimalDiscomfort worst)
    {
        rules ??= AnimalRules.Defaults();
        worst = AnimalDiscomfort.None;
        float illness = Mathf.Max(0f, -fx.comfort);
        if (f.boarded)
        {
            if (illness > 0f) worst = AnimalDiscomfort.Illness;
            return AnimalMath.Clamp100(rules.boardingComfort + fx.comfort);
        }
        AnimalCareSpec care = CareOf(sp);
        float eff = EffectiveTemperature(sp, in f, in w, rules);
        float shelter = AnimalMath.Clamp01(f.shelter);
        float cold = rules.coldPenalty * Mathf.Max(0f, care.comfortMin - eff);
        float heat = rules.heatPenalty * Mathf.Max(0f, eff - care.comfortMax);
        float rain = w.Raining ? rules.rainPenalty * care.rainDislike * (1f - shelter) * (w.storm ? 1.5f : 1f) : 0f;
        float crowding = rules.crowdingPenalty * Crowding(sp, in f);
        float dirt = rules.dirtPenalty * AnimalMath.Clamp100(f.dirt);
        float bedding = f.usesBedding ? rules.beddingPenalty * care.beddingNeed * (1f - AnimalMath.Clamp100(f.bedding) / 100f) : 0f;
        float hooves = care.HasHooves && a != null ? rules.hoofPain * Mathf.Max(0f, rules.hoofPainBelow - a.hooves) : 0f;
        float coat = a != null ? rules.dirtyCoatPenalty * Mathf.Max(0f, rules.dirtyCoatBelow - a.hygiene) : 0f;
        float most = 0f;
        Worse(ref worst, ref most, AnimalDiscomfort.Cold, cold);
        Worse(ref worst, ref most, AnimalDiscomfort.Heat, heat);
        Worse(ref worst, ref most, AnimalDiscomfort.Rain, rain);
        Worse(ref worst, ref most, AnimalDiscomfort.Crowding, crowding);
        Worse(ref worst, ref most, AnimalDiscomfort.Dirt, dirt);
        Worse(ref worst, ref most, AnimalDiscomfort.Bedding, bedding);
        Worse(ref worst, ref most, AnimalDiscomfort.Hooves, hooves);
        Worse(ref worst, ref most, AnimalDiscomfort.Coat, coat);
        Worse(ref worst, ref most, AnimalDiscomfort.Illness, illness);
        // Phase 5: an improved pen eases what bothers them (draughts, damp, a hard floor) by its level's comfort - never past 100.
        return AnimalMath.Clamp100(100f - cold - heat - rain - crowding - dirt - bedding - hooves - coat + fx.comfort + Mathf.Max(0f, f.comfortBonus));
    }

    private static void Worse(ref AnimalDiscomfort worst, ref float most, AnimalDiscomfort what, float penalty)
    {
        if (penalty > most + 1e-4f)
        {
            most = penalty;
            worst = what;
        }
    }

    /// <summary>
    /// Its comfort over a step: it settles toward <see cref="ComfortTarget"/> (time constant the rules' Comfort Hours,
    /// whatever the step length); <see cref="AnimalRecord.discomfort"/> is set to its largest penalty.
    /// </summary>
    public static void StepComfort(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, in AgriWeather w, AnimalRules rules, float dt, in AnimalStageEffects fx)
    {
        if (a == null || dt <= 0f) return;
        rules ??= AnimalRules.Defaults();
        float target = ComfortTarget(a, sp, in f, in w, rules, in fx, out AnimalDiscomfort worst);
        a.comfort = AnimalMath.Clamp100(target + (a.comfort - target) * AnimalMath.Decay(dt, rules.comfortHours));
        a.discomfort = worst;
    }

    /// <summary>
    /// What its surroundings add to its mood targets this step (the simulation adds them to its Phase 1 targets):
    /// happiness += (comfort - 60) x 0.15 + its band's + its illnesses'; stress += discomfort stress x max(0, 60 - comfort) +
    /// crowding stress x crowding + loneliness stress x loneliness + its illnesses' + its band's + (0.5 - temperament) x 10.
    /// <see cref="AnimalRecord.stressor"/> is set to the largest of discomfort (as <see cref="AnimalRecord.discomfort"/>),
    /// crowding, loneliness and illness (None when none adds stress).
    /// </summary>
    public static void MoodTargets(AnimalRecord a, AnimalSpeciesData sp, in AnimalStepFacts f, AnimalRules rules, AnimalWelfareBandEffects band, in AnimalStageEffects fx,
        out float happinessAdd, out float stressAdd)
    {
        happinessAdd = stressAdd = 0f;
        if (a == null) return;
        rules ??= AnimalRules.Defaults();
        float bandHappiness = band != null ? band.happiness : 0f, bandStress = band != null ? band.stress : 0f;
        happinessAdd = (a.comfort - 60f) * 0.15f + bandHappiness + fx.happiness;
        float discomfort = rules.discomfortStress * Mathf.Max(0f, 60f - a.comfort);
        float crowding = rules.crowdingStress * Crowding(sp, in f);
        float loneliness = rules.lonelinessStress * Loneliness(a, sp, in f);
        float illness = Mathf.Max(0f, fx.stress);
        float temperament = a.genes != null ? a.genes.temperament : 0.5f;
        stressAdd = discomfort + crowding + loneliness + fx.stress + bandStress + (0.5f - temperament) * 10f;
        AnimalDiscomfort worst = AnimalDiscomfort.None;
        float most = 0f;
        if (a.discomfort != AnimalDiscomfort.None) Worse(ref worst, ref most, a.discomfort, discomfort);
        Worse(ref worst, ref most, AnimalDiscomfort.Crowding, crowding);
        Worse(ref worst, ref most, AnimalDiscomfort.Loneliness, loneliness);
        Worse(ref worst, ref most, AnimalDiscomfort.Illness, illness);
        a.stressor = worst;
    }
}

/// <summary>What a mucking out did (the commands say it, and give the manure).</summary>
public struct AnimalCleanOutcome
{
    /// <summary>Points of floor dirt forked out.</summary>
    public float removed;
    /// <summary>Whole manure items to give the player (the fraction waits on the pen).</summary>
    public int manure;
    public bool strawLaid;
    public float beddingBefore, beddingAfter;
    /// <summary>Living animals that were disturbed by it (0: none: it was quick, or they were disturbed lately).</summary>
    public int disturbed;
    /// <summary>Living animals whose care history got the entry.</summary>
    public int animals;
}

/// <summary>
/// The pure logic of a pen's habitat: the facts its animals live by each step (<see cref="Facts"/>), how its floor, bedding,
/// trough and ground change after each step (<see cref="StepHabitat"/>), and what mucking out and water do to it. The
/// simulation and the commands only call these.
/// </summary>
public static class AnimalHabitat
{
    // Scratch (never state): the animals (living, and catching remains) whose illnesses the pen catches this step, summed in id order.
    private static readonly List<AnimalRecord> sources = new List<AnimalRecord>();
    private static readonly Dictionary<string, double> catching = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> catchingKeys = new List<string>();

    /// <summary>A bedded pen whose bedding was never laid (-1: a new pen, or one from a Phase 1 save) gets the rules' fresh bedding; another 0.</summary>
    public static void ResolveBedding(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        if (pen == null || pen.bedding >= 0f) return;
        AnimalPenSpec s = spec ?? pen.spec;
        pen.bedding = s != null && s.UsesBedding ? (rules ?? AnimalRules.Defaults()).freshBedding : 0f;
    }

    /// <summary>
    /// The facts a herd lives by this step, from the step-start state (so the order its animals are stepped in does not
    /// matter): the pen's dirt, bedding, water and worms, its shelter, how full it is, who is in it, and what illness its
    /// animals give off (contagion, per condition id: an active illness c.contagion x severity/100, a recovering one
    /// c.contagion x 0.3, the remains of an animal a catching illness killed c.contagion x 0.8 for the rules' Remains
    /// Contagion Hours after it died: summed over the animals in id order, in double). The two dictionaries are the caller's
    /// scratch (cleared here; never state). <paramref name="at"/> is the step's end.
    /// </summary>
    public static AnimalStepFacts Facts(AnimalHerd herd, AnimalPenSpec spec, IAnimalCatalog cat, AnimalRules rules, double at,
        Dictionary<string, float> contagion, Dictionary<string, int> companions, HashSet<string> here = null, Dictionary<string, int> nursing = null,
        Dictionary<string, float> milk = null)
    {
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        AnimalPenRecord pen = herd?.pen;
        spec ??= herd?.spec ?? pen?.spec ?? new AnimalPenSpec();
        contagion?.Clear();
        companions?.Clear();
        here?.Clear();
        nursing?.Clear();
        milk?.Clear();
        var f = new AnimalStepFacts
        {
            boarded = false,
            kind = spec.kind,
            // Its usable area (Phase 5: its level's room counts): crowding is judged against it.
            area = Mathf.Max(0.01f, AnimalUpgrades.Area(pen, spec, rules)),
            usesBedding = spec.UsesBedding,
            open = spec.Open,
            grazing = spec.grassGrowth > 0f,
            shelter = spec.Shelter,
            warmth = spec.Warmth,
            shade = spec.Shade,
            contagion = contagion,
            companions = companions,
            here = here,
            nursing = nursing,
            milk = milk,
        };
        if (pen == null) return f;
        ResolveBedding(pen, spec, rules);
        f.comfortBonus = AnimalUpgrades.ComfortBonus(pen, spec, rules);
        f.productionBonus = AnimalUpgrades.ProductionBonus(pen, spec, rules);
        f.dirt = pen.dirt;
        f.waterDirt = pen.waterDirt;
        f.worms = pen.parasites;
        f.bedding = f.usesBedding ? Mathf.Max(0f, pen.bedding) : 0f;
        f.water = pen.water;

        sources.Clear();
        catching.Clear();
        float space = 0f, dead = 0f;
        foreach (AnimalRecord a in herd.animals)
        {
            if (a == null) continue;
            AnimalSpeciesData sp = cat?.Species(a.species);
            if (!a.Alive)
            {
                dead += AnimalBook.SpaceOf(sp, a);
                AnimalConditionData dc = string.IsNullOrEmpty(a.deathCondition) ? null : cat?.Condition(a.deathCondition);
                if (dc != null && dc.contagion > 0f && a.diedAt >= 0.0 && at - a.diedAt <= rules.remainsContagionHours) sources.Add(a);
                continue;
            }
            space += AnimalBook.SpaceOf(sp, a);
            sources.Add(a);
            if (companions != null && !string.IsNullOrEmpty(a.species))
                companions[a.species] = companions.TryGetValue(a.species, out int n) ? n + 1 : 1;
            here?.Add(a.id);
        }
        // Who suckles from whom (a young whose mother lives in its pen), and the milk each mother gives (her nutrition).
        if (nursing != null && here != null)
            foreach (AnimalRecord a in herd.animals)
            {
                if (a == null || !a.Alive || !here.Contains(a.mother ?? "")) continue;
                if (!AnimalBreeding.Suckles(a, cat?.Species(a.species))) continue;
                nursing[a.mother] = nursing.TryGetValue(a.mother, out int k) ? k + 1 : 1;
            }
        if (milk != null && nursing != null)
            foreach (AnimalRecord a in herd.animals)
                if (a != null && a.Alive && nursing.ContainsKey(a.id))
                    milk[a.id] = AnimalMath.Clamp(a.nutrition / 100f, 0.3f, 1f) * (a.satiety > 0f && a.hydration > 0f ? 1f : 0.3f);
        f.density = space / f.area;
        f.deadSpace = dead;
        if (contagion != null && cat != null)
        {
            sources.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
            foreach (AnimalRecord a in sources)
            {
                if (!a.Alive)
                {
                    AnimalConditionData dc = cat.Condition(a.deathCondition);
                    if (dc != null) Add(dc.id, dc.contagion * 0.8);
                    continue;
                }
                if (a.conditions == null) continue;
                foreach (AnimalCondition k in a.conditions)
                {
                    if (k == null) continue;
                    AnimalConditionData c = cat.Condition(k.id);
                    if (c == null || c.contagion <= 0f) continue;
                    Add(c.id, AnimalHealth.GivesOff(k, c));
                }
            }
            catchingKeys.Clear();
            catchingKeys.AddRange(catching.Keys);
            catchingKeys.Sort(StringComparer.Ordinal);
            foreach (string id in catchingKeys) contagion[id] = (float)catching[id];
        }
        sources.Clear();
        return f;
    }

    private static void Add(string id, double v)
    {
        if (string.IsNullOrEmpty(id) || v <= 0.0) return;
        catching[id] = catching.TryGetValue(id, out double had) ? had + v : v;
    }

    /// <summary>
    /// The facts of the boarding stable (no pen): clean (dirt 15, water 5), roofed (shelter 1, warmth 5, shade 1), bedded
    /// (70), no worms, room enough (0.5), no contagion, and the company its kind wants (in <paramref name="companions"/>
    /// when given: Loneliness and Crowding are 0 there anyway).
    /// </summary>
    public static AnimalStepFacts BoardedFacts(AnimalSpeciesData sp, Dictionary<string, int> companions = null)
    {
        companions?.Clear();
        if (companions != null && sp != null && !string.IsNullOrEmpty(sp.id)) companions[sp.id] = AnimalCare.CareOf(sp).companionsWanted + 1;
        return new AnimalStepFacts
        {
            boarded = true, kind = AnimalHabitatKind.Stable, area = 100f, density = 0.5f, dirt = 15f, waterDirt = 5f, worms = 0f,
            usesBedding = true, bedding = 70f, open = false, grazing = false, shelter = 1f, warmth = 5f, shade = 1f, water = 100f,
            contagion = null, companions = companions, deadSpace = 0f,
        };
    }

    /// <summary>What one animal soils its pen with per game hour (its species' messiness x the room it takes x its pace).</summary>
    public static float Soiling(AnimalSpeciesData sp, AnimalRecord a, float pace) => AnimalCare.CareOf(sp).messiness * AnimalBook.SpaceOf(sp, a) * Mathf.Max(0f, pace);

    /// <summary>
    /// The pen after a step of <paramref name="dt"/> hours (call after the animal loop). <paramref name="load"/> = the sum
    /// of <see cref="Soiling"/> over its living animals / max(1, area) (about 1 at full stocking); <paramref name="deadSpace"/>
    /// = the room the remains of its dead take (<see cref="AnimalStepFacts.deadSpace"/>).
    /// <list type="bullet">
    /// <item>mess = dirt per hour at capacity x load + remains mess x dead space / area. Bedding takes up its share of it and
    /// is spent; open ground clears itself of dung (its self-clean hours, faster in the rain); dirt 0-100.</item>
    /// <item>The trough fouls by the animals and the floor (x 100 / litres, 0.25-2) and by algae in the warmth (an open
    /// trough: x 200 x surface / litres, at most 1); a dry trough is clean.</item>
    /// <item>Worm eggs build up in the ground of a pasture from its dirt and stocking in the warmth, and die off (faster in
    /// frost).</item>
    /// </list>
    /// </summary>
    public static void StepHabitat(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules, in AgriWeather w, double dt, float load, float deadSpace)
    {
        if (pen == null || dt <= 0.0) return;
        rules ??= AnimalRules.Defaults();
        spec ??= pen.spec ?? new AnimalPenSpec();
        ResolveBedding(pen, spec, rules);
        float h = (float)dt;
        float area = Mathf.Max(1f, spec.area);
        load = Mathf.Max(0f, load);
        float mess = rules.dirtPerHourAtCapacity * load + rules.remainsMessPerHour * Mathf.Max(0f, deadSpace) / area;
        float dirtIn = mess;
        if (spec.UsesBedding)
        {
            float b = AnimalMath.Clamp01(pen.bedding / 100f);
            dirtIn = mess * (1f - rules.beddingAbsorbs * b);
            pen.bedding = Mathf.Max(0f, pen.bedding - mess * rules.beddingSoiling * h);
        }
        double tau = spec.SelfCleanHours(rules) / (1.0 + Math.Min(2.0, Math.Max(0.0, w.rain)));
        if (tau > 0.0)
        {
            float e = AnimalMath.Decay(dt, tau);
            pen.dirt = pen.dirt * e + dirtIn * (float)tau * (1f - e);
        }
        else pen.dirt += dirtIn * h;
        pen.dirt = AnimalMath.Clamp100(pen.dirt);

        if (pen.water > 0.0001f)
        {
            float capacity = Mathf.Max(1f, spec.waterCapacity);
            float scale = Mathf.Clamp(100f / capacity, 0.25f, 2f);
            float algae = w.temperature > rules.algaeAbove
                ? (w.temperature - rules.algaeAbove) * rules.algaePerDegree * AnimalMath.Clamp01(200f * Mathf.Max(0f, spec.troughSurface) / capacity) : 0f;
            pen.waterDirt = AnimalMath.Clamp100(pen.waterDirt + ((rules.troughFoulingPerHour * load + pen.dirt / 100f * rules.troughDirtFouling) * scale + algae) * h);
        }
        else pen.waterDirt = 0f;

        if (spec.grassGrowth > 0f || AnimalPenSpec.MainKind(spec.kind) == AnimalHabitatKind.Pasture)
        {
            float t = w.temperature;
            float warm = AnimalMath.Smooth(4f, 14f, t) * (0.6f + 0.4f * AnimalMath.Clamp01(w.humidity));
            float rise = rules.wormRate * pen.dirt / 100f * Mathf.Min(1.5f, load) * warm;
            double fade = t <= 0f ? rules.frostFadeHours : rules.wormFadeHours;
            float e = AnimalMath.Decay(dt, Math.Max(1.0, fade));
            pen.parasites = AnimalMath.Clamp100(pen.parasites * e + rise * (float)Math.Max(1.0, fade) * (1f - e));
        }
    }

    /// <summary>
    /// The trough after clean water (rain, a spring) came in, from <paramref name="before"/> litres: its dirt is mixed down
    /// while it fills, and washed out while it overflows (x exp(-overflow / capacity), whatever the step length). A dry
    /// trough is clean. Call after the trough's litres were updated.
    /// </summary>
    public static void DiluteTrough(AnimalPenRecord pen, float before, float cleanLitres, float capacity)
    {
        if (pen == null) return;
        if (pen.water <= 0.0001f)
        {
            pen.waterDirt = 0f;
            return;
        }
        if (cleanLitres <= 0f || pen.waterDirt <= 0f) return;
        float room = Mathf.Max(0f, capacity - Mathf.Max(0f, before));
        float fill = Mathf.Min(room, cleanLitres);
        float over = cleanLitres - fill;
        if (fill > 0f) pen.waterDirt *= Mathf.Max(0f, before) / Mathf.Max(0.001f, Mathf.Max(0f, before) + fill);
        if (over > 0f && capacity > 0f) pen.waterDirt *= (float)Math.Exp(-over / capacity);
    }

    /// <summary>
    /// Pours water into the trough (up to its capacity) and mixes its dirt: (litres before x dirt + litres poured x
    /// <paramref name="dirtOfWater"/>) / litres after (well water 0; a swamp's 45: see <see cref="AnimalHealth.KindDirt"/>).
    /// Returns the litres that went in.
    /// </summary>
    public static float Pour(AnimalPenRecord pen, float litres, float dirtOfWater, float capacity)
    {
        if (pen == null || litres <= 0f) return 0f;
        float before = Mathf.Max(0f, pen.water);
        float put = Mathf.Min(Mathf.Max(0f, capacity - before), litres);
        if (put <= 0f) return 0f;
        pen.waterDirt = AnimalMath.Clamp100((before * pen.waterDirt + put * AnimalMath.Clamp100(dirtOfWater)) / Mathf.Max(0.001f, before + put));
        pen.water = before + put;
        pen.revision++;
        return put;
    }

    /// <summary>Is the trough's water fouled enough to change (the rules' Tip Out Above)?</summary>
    public static bool CanScrub(AnimalPenRecord pen, AnimalRules rules) => pen != null && pen.water > 0.0001f && pen.waterDirt >= (rules ?? AnimalRules.Defaults()).tipOutAbove;

    /// <summary>The fouled water is tipped out and the trough scrubbed: empty and clean (fill it again after).</summary>
    public static void ScrubTrough(AnimalPenRecord pen)
    {
        if (pen == null) return;
        pen.water = 0f;
        pen.waterDirt = 0f;
        pen.revision++;
    }

    /// <summary>Straw items that make a bedded pen's bedding fresh (at least 1).</summary>
    public static int StrawNeeded(AnimalPenRecord pen, AnimalPenSpec spec, AnimalRules rules)
    {
        rules ??= AnimalRules.Defaults();
        spec ??= pen?.spec ?? new AnimalPenSpec();
        float bedding = pen != null && pen.bedding >= 0f ? pen.bedding : spec.UsesBedding ? rules.freshBedding : 0f;
        return Math.Max(1, Mathf.CeilToInt((100f - AnimalMath.Clamp100(bedding)) / 100f * Mathf.Max(0f, spec.area) * rules.strawPerSquareMetre - 1e-4f));
    }

    /// <summary>Uses of a fork or shovel one mucking out wears (one per 20 m², at least 1).</summary>
    public static int CleanWear(AnimalPenSpec spec) => Math.Max(1, Mathf.CeilToInt(Mathf.Max(0f, spec != null ? spec.area : 0f) / 20f - 1e-4f));

    /// <summary>
    /// The whole effect of mucking out a pen (where the rules run; the plan checked the tool, the cooldown and the straw):
    /// share = min(0.95, clean share x <paramref name="toolFactor"/>) of the floor's dirt is forked out, worm eggs x (1 - 0.3
    /// x share); a bedded pen keeps its bedding but the soiled share (bedding x (1 - share x dirt/100)), and fresh straw
    /// (<paramref name="straw"/> items) tops it up (never below what it had); the manure forked up (removed/100 x area x
    /// manure per m², fractions kept) is owed to the player; the animals are disturbed (stress + disturb stress x min(1,
    /// removed/20), at most once in Disturb Hours); every living animal's care history gets "The pen was mucked out by ..."
    /// ("Fresh straw was laid by ..." when only straw was laid) and a care count (its owner's when <paramref name="by"/>
    /// owns it: then it also counts as its owner's care, so its affinity does not fade).
    /// </summary>
    public static AnimalCleanOutcome CleanPen(AnimalHerd herd, IAnimalCatalog cat, AnimalRules rules, float toolFactor, int straw, double now, string by, string byName)
    {
        var o = new AnimalCleanOutcome();
        AnimalPenRecord pen = herd?.pen;
        if (pen == null) return o;
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        AnimalPenSpec spec = herd.spec ?? pen.spec ?? new AnimalPenSpec();
        ResolveBedding(pen, spec, rules);
        float area = Mathf.Max(0f, spec.area);
        float share = Mathf.Min(0.95f, Mathf.Max(0f, rules.cleanShare * toolFactor));
        float dirt0 = AnimalMath.Clamp100(pen.dirt);
        o.removed = dirt0 * share;
        pen.dirt = Mathf.Max(0f, dirt0 - o.removed);
        pen.parasites = Mathf.Max(0f, pen.parasites * (1f - 0.3f * share));
        o.beddingBefore = o.beddingAfter = pen.bedding;
        if (spec.UsesBedding)
        {
            float b = Mathf.Max(0f, pen.bedding);
            float kept = b * (1f - share * dirt0 / 100f);
            if (straw > 0)
            {
                kept = Mathf.Max(b, Mathf.Min(100f, kept + 100f * straw / Mathf.Max(1f, area * rules.strawPerSquareMetre)));
                pen.lastBeddedAt = now;
                o.strawLaid = true;
            }
            pen.bedding = AnimalMath.Clamp100(kept);
            o.beddingAfter = pen.bedding;
        }
        float disturb = rules.disturbStress * Mathf.Min(1f, o.removed / 20f);
        bool disturbs = disturb > 0.01f && (pen.lastDisturbedAt < 0.0 || now - pen.lastDisturbedAt >= rules.disturbHours);
        if (disturbs) pen.lastDisturbedAt = now;
        pen.manureOwed = Mathf.Max(0f, pen.manureOwed) + o.removed / 100f * area * rules.manurePerSquareMetre;
        o.manure = Mathf.FloorToInt(pen.manureOwed + 1e-4f);
        pen.manureOwed = Mathf.Max(0f, pen.manureOwed - o.manure);
        pen.lastCleanedAt = now;
        pen.cleanedBy = by ?? "";
        string who = string.IsNullOrWhiteSpace(byName) ? "" : $" by {byName.Trim()}";
        // Only straw laid on a clean floor (nothing forked out) is said as that (as the command's answer is).
        string note = o.removed <= 0.01f && o.strawLaid ? $"Fresh straw was laid{who}." : $"The pen was mucked out{who}.";
        foreach (AnimalRecord a in herd.animals)
        {
            if (a == null || !a.Alive) continue;
            if (disturbs)
            {
                a.stress = AnimalMath.Clamp100(a.stress + disturb);
                o.disturbed++;
            }
            bool owner = !string.IsNullOrEmpty(by) && a.owner == by;
            AnimalCare.Note(a, "cleaned", by, note, now, rules);
            AnimalCare.Count(a, now, owner, rules);
            if (owner) a.lastOwnerCareAt = now;
            a.revision++;
            o.animals++;
        }
        pen.revision++;
        return o;
    }
}
