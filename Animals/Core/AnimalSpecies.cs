using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One stage of a species' life: how long a well kept animal spends in it and what it may do in it.</summary>
[Serializable]
public class AnimalStageSpec
{
    public GrowthStage stage = GrowthStage.Adult;
    [Tooltip("Game hours of growth a well fed, healthy animal needs to leave this stage (a poorly kept one takes longer). " +
             "0 on the last stage it grows into: it stays there (only age makes it Senior).")]
    [Min(0f)] public float durationHours;
    [Tooltip("It gives products in this stage (eggs, milk, wool: Phase 3).")]
    public bool productionEnabled;
    [Tooltip("It may breed in this stage (Phase 4).")]
    public bool breedingEnabled;
    [Tooltip("How much it eats and drinks compared to an adult (0.3 = a third).")]
    [Range(0.05f, 2f)] public float appetite = 1f;
    [Tooltip("Its size compared to an adult of its breed.")]
    [Range(0.05f, 1.5f)] public float size = 1f;
    [Tooltip("What it needs of each nutrient compared to an adult, per unit of appetite (a chick needs more protein).")]
    public NutrientProfile needScale = NutrientProfile.All(1f);
    [Tooltip("Its diet in this stage (empty lists: the species' diet). A calf drinks milk.")]
    public AnimalDiet diet = new AnimalDiet();

    public AnimalStageSpec() { }

    public AnimalStageSpec(GrowthStage stage, float hours, float appetite, float size, bool production = false, bool breeding = false)
    {
        this.stage = stage; durationHours = hours; this.appetite = appetite; this.size = size; productionEnabled = production; breedingEnabled = breeding;
    }
}

/// <summary>How fast an animal of a species gets hungry and thirsty, what it needs to eat, and how it suffers without it.</summary>
[Serializable]
public class AnimalNeedsSpec
{
    [Header("Hunger and thirst")]
    [Tooltip("How much an adult eats compared to an average animal (a cow 4, a hen 0.2): portions of food, nutrients and toxin doses all count by it.")]
    [Min(0.01f)] public float portionScale = 1f;
    [Tooltip("Satiety (0-100) an adult loses per game hour.")]
    [Min(0f)] public float hungerPerHour = 3f;
    [Tooltip("Hydration (0-100) an adult loses per game hour, in comfortable weather.")]
    [Min(0f)] public float thirstPerHour = 4f;
    [Tooltip("It goes to the feeder when its satiety falls below this.")]
    [Range(10f, 95f)] public float eatBelow = 65f;
    [Tooltip("It goes to the water when its hydration falls below this.")]
    [Range(10f, 95f)] public float drinkBelow = 70f;
    [Tooltip("Litres of water for one point of hydration (an adult). A cow drinks buckets, a hen a cupful.")]
    [Min(0.0001f)] public float litresPerPoint = 0.4f;
    [Tooltip("Air temperature (°C) above which it gets thirstier.")]
    public float heatAbove = 24f;
    [Tooltip("Extra thirst per °C above Heat Above (0.05 = 5% more per degree).")]
    [Range(0f, 0.3f)] public float heatThirst = 0.05f;

    [Header("Nutrition")]
    [Tooltip("What an adult needs per game hour of each nutrient (in the units of the foods' portions).")]
    public NutrientProfile needPerHour = new NutrientProfile(1f, 0.3f, 0.8f, 0.05f, 0.05f);
    [Tooltip("How much each nutrient counts in its nutritional condition (they are normalised).")]
    public NutrientProfile importance = new NutrientProfile(1f, 0.8f, 0.8f, 0.4f, 0.4f);

    [Header("Harm and recovery")]
    [Tooltip("Health lost per game hour with an empty stomach.")]
    [Min(0f)] public float starvationDamage = 1.2f;
    [Tooltip("Health lost per game hour with no water left in it.")]
    [Min(0f)] public float dehydrationDamage = 2.5f;
    [Tooltip("Health lost per game hour while its nutritional condition is poor (below 35).")]
    [Min(0f)] public float malnutritionDamage = 0.25f;
    [Tooltip("Health regained per game hour while it is fed, watered and well nourished.")]
    [Min(0f)] public float recoveryPerHour = 0.6f;
    [Tooltip("Toxin load it bears without harm.")]
    [Min(0f)] public float toxinTolerance = 8f;
    [Tooltip("Share of its toxin load it clears per game hour.")]
    [Range(0f, 1f)] public float toxinClearance = 0.05f;
    [Tooltip("Health lost per game hour per point of toxin above its tolerance.")]
    [Min(0f)] public float toxinDamage = 0.35f;
}

/// <summary>
/// What a species eats. Each list holds food ids ("hay") or tags ("#grain", "#meat"); a food takes the best category a
/// list of it names (an id beats a tag), otherwise <see cref="otherwise"/>.
/// </summary>
[Serializable]
public class AnimalDiet
{
    public List<string> favorite = new List<string>();
    public List<string> preferred = new List<string>();
    [Tooltip("Acceptable foods.")]
    public List<string> normal = new List<string>();
    public List<string> neutral = new List<string>();
    public List<string> poor = new List<string>();
    public List<string> harmful = new List<string>();
    public List<string> toxic = new List<string>();
    [Tooltip("What every other food is to it.")]
    public FoodAffinity otherwise = FoodAffinity.Inedible;

    public bool IsEmpty =>
        Empty(favorite) && Empty(preferred) && Empty(normal) && Empty(neutral) && Empty(poor) && Empty(harmful) && Empty(toxic);

    private static bool Empty(List<string> l) => l == null || l.Count == 0;

    public List<string> Entries(FoodAffinity a)
    {
        switch (a)
        {
            case FoodAffinity.Favorite: return favorite ??= new List<string>();
            case FoodAffinity.Preferred: return preferred ??= new List<string>();
            case FoodAffinity.Normal: return normal ??= new List<string>();
            case FoodAffinity.Neutral: return neutral ??= new List<string>();
            case FoodAffinity.Poor: return poor ??= new List<string>();
            case FoodAffinity.Harmful: return harmful ??= new List<string>();
            case FoodAffinity.Toxic: return toxic ??= new List<string>();
            default: return null;
        }
    }

    /// <summary>
    /// What a food is to this diet. A food named by id takes that category (the worst one, if a careless author named it
    /// twice: safety first); otherwise the worst category any of its tags falls in; otherwise <see cref="otherwise"/>.
    /// </summary>
    public FoodAffinity AffinityOf(AnimalFoodData food)
    {
        if (food == null) return FoodAffinity.Inedible;
        FoodAffinity byId = FoodAffinity.Favorite;
        bool idFound = false;
        FoodAffinity byTag = FoodAffinity.Favorite;
        bool tagFound = false;
        for (int a = (int)FoodAffinity.Favorite; a <= (int)FoodAffinity.Toxic; a++)
        {
            List<string> l = Entries((FoodAffinity)a);
            if (l == null) continue;
            for (int i = 0; i < l.Count; i++)
            {
                string e = l[i];
                if (string.IsNullOrEmpty(e)) continue;
                if (e[0] == '#')
                {
                    if (food.HasTag(e.Substring(1)))
                    {
                        byTag = (FoodAffinity)Math.Max((int)byTag, a);
                        tagFound = true;
                    }
                }
                else if (string.Equals(e, food.id, StringComparison.OrdinalIgnoreCase))
                {
                    byId = (FoodAffinity)Math.Max((int)byId, a);
                    idFound = true;
                }
            }
        }
        if (idFound) return byId;
        if (tagFound) return byTag;
        return otherwise;
    }

    public AnimalDiet Clone()
    {
        var d = new AnimalDiet { otherwise = otherwise };
        for (int a = (int)FoodAffinity.Favorite; a <= (int)FoodAffinity.Toxic; a++)
            d.Entries((FoodAffinity)a).AddRange(Entries((FoodAffinity)a));
        return d;
    }

    public static AnimalDiet Of(string[] favorite = null, string[] preferred = null, string[] normal = null, string[] neutral = null,
        string[] poor = null, string[] harmful = null, string[] toxic = null, FoodAffinity otherwise = FoodAffinity.Inedible)
    {
        var d = new AnimalDiet { otherwise = otherwise };
        if (favorite != null) d.favorite.AddRange(favorite);
        if (preferred != null) d.preferred.AddRange(preferred);
        if (normal != null) d.normal.AddRange(normal);
        if (neutral != null) d.neutral.AddRange(neutral);
        if (poor != null) d.poor.AddRange(poor);
        if (harmful != null) d.harmful.AddRange(harmful);
        if (toxic != null) d.toxic.AddRange(toxic);
        return d;
    }
}

/// <summary>Where a species can live.</summary>
[Serializable]
public class AnimalHabitatNeeds
{
    [Tooltip("The kinds of habitat it can live in.")]
    public AnimalHabitatKind kinds = AnimalHabitatKind.Pasture | AnimalHabitatKind.Barn;
    [Tooltip("Square metres of the pen one adult needs (young ones need less, by their size). A pen's capacity counts by it.")]
    [Min(0.1f)] public float areaPerAnimal = 6f;

    public bool Accepts(AnimalHabitatKind kind) => kind != AnimalHabitatKind.None && (kinds & kind) != 0;
}

/// <summary>How an animal of a species looks and moves (a placeholder body is built from it when no body prefab is given).</summary>
[Serializable]
public class AnimalLookSpec
{
    public enum Shape { Quadruped, Bird, Small }

    public Shape shape = Shape.Quadruped;
    [Tooltip("An adult's body length, metres.")]
    [Min(0.05f)] public float length = 1.4f;
    [Tooltip("An adult's height at the back, metres.")]
    [Min(0.05f)] public float height = 1.1f;
    [Tooltip("The coat when the breed gives none.")]
    public Color coat = new Color(0.55f, 0.4f, 0.3f);
    public Color accent = new Color(0.9f, 0.9f, 0.85f);
    [Tooltip("How it moves on the ground (the mobs' gaits: a rabbit hops).")]
    public MobLandGait gait = MobLandGait.Walk;
    [Tooltip("Walking speed of an adult, m/s.")]
    [Min(0.05f)] public float walkSpeed = 1.1f;
}

/// <summary>A breed of a species: how it differs from the species' typical animal.</summary>
[Serializable]
public class AnimalBreedData
{
    public string id = "";
    public string name = "";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Growth speed (1.2 = grows 20% faster).")]
    [Range(0.25f, 3f)] public float growth = 1f;
    [Tooltip("How much it eats and drinks (1.1 = 10% more).")]
    [Range(0.25f, 3f)] public float appetite = 1f;
    [Tooltip("Its adult size.")]
    [Range(0.25f, 3f)] public float size = 1f;
    [Tooltip("How well it bears hardship: harm from hunger, thirst and toxins is divided by it.")]
    [Range(0.25f, 3f)] public float hardiness = 1f;
    [Tooltip("Mean temperament of its animals (0 = skittish, 1 = docile).")]
    [Range(0f, 1f)] public float temperament = 0.5f;
    [Tooltip("Mean quality of what its animals give (Phase 3).")]
    [Range(0f, 1f)] public float productionQuality = 0.5f;
    [Tooltip("How rare it is (0 = common): rare breeds carry rarer genes.")]
    [Range(0f, 1f)] public float rarity;
    [Tooltip("Coats its animals are born with.")]
    public List<Color> coats = new List<Color>();
    [Tooltip("What a young animal of this breed costs (the livestock item's price).")]
    [Min(0)] public int value = 50;
    [Tooltip("Phase 5, its specialisation: how much faster it makes its Specialty (or every product, when it names none): 1.2 = 20% more.")]
    [Range(0.25f, 3f)] public float productYield = 1f;
    [Tooltip("The product id its Product Yield is for (\"egg\", \"milk\", \"wool\"); empty: every product it gives.")]
    public string specialty = "";

    public AnimalBreedData() { }

    public AnimalBreedData(string id, string name, float growth = 1f, float appetite = 1f, float size = 1f, float hardiness = 1f, int value = 50, params Color[] coats)
    {
        this.id = id; this.name = name; this.growth = growth; this.appetite = appetite; this.size = size; this.hardiness = hardiness; this.value = value;
        if (coats != null) this.coats.AddRange(coats);
    }
}

/// <summary>
/// A species of animal, as data: its life stages, needs, diet, habitat, looks and breeds. Every rule of the core reads
/// these numbers; nothing about a species is written in code, so a new species is a new asset (or a new entry of the
/// built-in content).
/// </summary>
[Serializable]
public class AnimalSpeciesData
{
    public string id = "";
    public string name = "";
    [Tooltip("\"Chickens\".")]
    public string plural = "";
    [Tooltip("What a young one is called (\"Chick\", \"Calf\"): the livestock crate's name.")]
    public string youngName = "";
    [TextArea(2, 5)] public string description = "";

    [Header("Life")]
    [Tooltip("Its animals are female or male (Phase 4 breeding needs both).")]
    public bool sexed = true;
    [Range(0f, 1f)] public float femaleShare = 0.5f;
    [Tooltip("Its stages, in order (Newborn... Adult). The last one an animal grows into lasts until age makes it Senior.")]
    public List<AnimalStageSpec> stages = new List<AnimalStageSpec>();
    [Tooltip("Game hours of age at which an adult becomes Senior (0 = never).")]
    [Min(0f)] public float seniorAgeHours;
    [Tooltip("Game hours an animal can live: past it, old age wears its health away (0 = never).")]
    [Min(0f)] public float lifespanHours;

    [Header("Needs")]
    public AnimalNeedsSpec needs = new AnimalNeedsSpec();
    public AnimalDiet diet = new AnimalDiet();
    public AnimalHabitatNeeds habitat = new AnimalHabitatNeeds();

    [Header("Look")]
    public AnimalLookSpec look = new AnimalLookSpec();

    [Header("Breeds")]
    public List<AnimalBreedData> breeds = new List<AnimalBreedData>();

    [Header("Care")]
    [Tooltip("How it keeps clean and comfortable, what bothers it, and the care it takes.")]
    public AnimalCareSpec care = new AnimalCareSpec();
    [Tooltip("How prone it is to each condition (unlisted: 1; 0: immune).")]
    public List<AnimalSusceptibility> susceptibility = new List<AnimalSusceptibility>();

    [Header("Remains")]
    [Tooltip("The Harvesting anatomy its remains are butchered as (deaths and slaughter go through the Harvesting system; empty: its remains can only be cleared).")]
    public string harvestAnatomy = "";

    [Header("Products")]
    [Tooltip("What its animals give while they live (eggs, milk, wool), in its productive stages.")]
    public List<AnimalProductSpec> products = new List<AnimalProductSpec>();

    [Header("Breeding, behaviour and the bond (Phase 4)")]
    [Tooltip("How it breeds: gestation, litters, cooldowns, conception, what a parent needs, the cost, nursing, rival males, the words.")]
    public AnimalBreedingSpec breeding = new AnimalBreedingSpec();
    [Tooltip("How it behaves: company, grooming, shyness and curiosity, herding, young keeping to their mother.")]
    public AnimalBehaviourSpec behaviour = new AnimalBehaviourSpec();
    [Tooltip("What a bond with its owner unlocks (each from an affinity).")]
    public List<AnimalAffinityPerk> perks = new List<AnimalAffinityPerk>();

    [Tooltip("The version of the built-in content it was made from (4: Phase 4). An older species asset takes what it lacks from the built-in species of its id: before 2 its care, susceptibility and anatomy, before 3 its products, before 4 its breeding, behaviour and perks.")]
    public int dataVersion;

    // ---------------------------------------------------------------- stages
    /// <summary>The stage spec of a stage (null when the species does not use it).</summary>
    public AnimalStageSpec Stage(GrowthStage s)
    {
        if (stages == null) return null;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && stages[i].stage == s) return stages[i];
        return null;
    }

    /// <summary>The spec of a stage, or the nearest stage below it the species uses, or its first.</summary>
    public AnimalStageSpec StageOrNearest(GrowthStage s)
    {
        AnimalStageSpec best = null;
        if (stages != null)
            foreach (AnimalStageSpec st in stages)
                if (st != null && st.stage <= s && (best == null || st.stage > best.stage)) best = st;
        return best ?? (stages != null && stages.Count > 0 ? stages[0] : null);
    }

    /// <summary>The first stage the species uses (where a newborn starts).</summary>
    public GrowthStage FirstStage => stages != null && stages.Count > 0 && stages[0] != null ? stages[0].stage : GrowthStage.Adult;

    /// <summary>
    /// The stage reached with <paramref name="development"/> game hours of growth (the stages grown through by then),
    /// and how far through it (0-1; 1 in the last growing stage). Senior is not reached by growth (see <see cref="seniorAgeHours"/>).
    /// </summary>
    public GrowthStage StageAt(float development, out float progress)
    {
        progress = 1f;
        if (stages == null || stages.Count == 0) return GrowthStage.Adult;
        float acc = 0f;
        for (int i = 0; i < stages.Count; i++)
        {
            AnimalStageSpec st = stages[i];
            if (st == null || st.stage == GrowthStage.Senior) continue;
            bool last = st.durationHours <= 0f || i == stages.Count - 1 || NextGrowing(i) < 0;
            if (last || development < acc + st.durationHours)
            {
                progress = last ? 1f : AnimalMath.Clamp01((development - acc) / Math.Max(0.001f, st.durationHours));
                return st.stage;
            }
            acc += st.durationHours;
        }
        return stages[stages.Count - 1].stage;
    }

    private int NextGrowing(int i)
    {
        for (int k = i + 1; k < stages.Count; k++)
            if (stages[k] != null && stages[k].stage != GrowthStage.Senior) return k;
        return -1;
    }

    /// <summary>Game hours of growth at which <paramref name="s"/> starts (0 for the first stage).</summary>
    public float DevelopmentAtStart(GrowthStage s)
    {
        float acc = 0f;
        if (stages == null) return 0f;
        foreach (AnimalStageSpec st in stages)
        {
            if (st == null || st.stage == GrowthStage.Senior) continue;
            if (st.stage >= s) return acc;
            acc += st.durationHours;
        }
        return acc;
    }

    /// <summary>Game hours of growth to adulthood (the sum of the growing stages' durations).</summary>
    public float HoursToAdult => DevelopmentAtStart(GrowthStage.Adult);

    // ---------------------------------------------------------------- breeds
    public AnimalBreedData Breed(string breedId)
    {
        if (breeds == null || breeds.Count == 0) return null;
        if (!string.IsNullOrEmpty(breedId))
            foreach (AnimalBreedData b in breeds)
                if (b != null && string.Equals(b.id, breedId, StringComparison.OrdinalIgnoreCase)) return b;
        return null;
    }

    /// <summary>The breed, or the first breed (the species' common one), or null.</summary>
    public AnimalBreedData BreedOrDefault(string breedId) => Breed(breedId) ?? (breeds != null && breeds.Count > 0 ? breeds[0] : null);

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;
    public string Plural => string.IsNullOrWhiteSpace(plural) ? DisplayName + "s" : plural;

    /// <summary>The diet of a stage (its own when it has one, else the species').</summary>
    public AnimalDiet DietAt(GrowthStage s)
    {
        AnimalStageSpec st = StageOrNearest(s);
        return st != null && st.diet != null && !st.diet.IsEmpty ? st.diet : diet ?? new AnimalDiet();
    }

    // ---------------------------------------------------------------- care and health
    /// <summary>How prone it is to an illness: 0 for a hooves-only illness when it has none, else its listed factor, else 1.</summary>
    public float SusceptibilityTo(AnimalConditionData c)
    {
        if (c == null) return 0f;
        if (c.hoovedOnly && (care == null || !care.HasHooves)) return 0f;
        if (susceptibility != null)
            for (int i = 0; i < susceptibility.Count; i++)
                if (susceptibility[i] != null && string.Equals(susceptibility[i].condition, c.id, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Max(0f, susceptibility[i].factor);
        return 1f;
    }

    /// <summary>Its care action of this id ("brush"), or null.</summary>
    public AnimalCareActionSpec CareAction(string actionId) => care?.Action(actionId);

    /// <summary>What is wrong with the data (empty: nothing). Errors make the species unusable; the database skips it.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        string who = string.IsNullOrEmpty(id) ? "(a species without an id)" : $"'{id}'";
        if (string.IsNullOrWhiteSpace(id)) errors?.Add($"{who}: no id.");
        if (stages == null || stages.Count == 0) errors?.Add($"{who}: no growth stages.");
        else
        {
            for (int i = 1; i < stages.Count; i++)
                if (stages[i] != null && stages[i - 1] != null && stages[i].stage <= stages[i - 1].stage)
                    errors?.Add($"{who}: the stages are not in order ({stages[i - 1].stage} before {stages[i].stage}).");
            for (int i = 0; i < stages.Count - 1; i++)
                if (stages[i] != null && stages[i].stage != GrowthStage.Senior && stages[i].durationHours <= 0f && NextGrowing(i) >= 0)
                    warnings?.Add($"{who}: {stages[i].stage} lasts 0 hours, so its animals never grow past it.");
        }
        if (needs == null) errors?.Add($"{who}: no needs.");
        else if (needs.needPerHour.Sum <= 0f) warnings?.Add($"{who}: it needs no nutrients (nutrition never matters).");
        if (diet == null || diet.IsEmpty) warnings?.Add($"{who}: its diet names no food.");
        if (habitat == null || habitat.kinds == AnimalHabitatKind.None) errors?.Add($"{who}: it can live in no habitat.");
        if (breeds == null || breeds.Count == 0) warnings?.Add($"{who}: no breeds (a plain breed is assumed).");
        if (lifespanHours > 0f && seniorAgeHours > lifespanHours) warnings?.Add($"{who}: it would become Senior after its lifespan.");
        if (care == null) warnings?.Add($"{who}: no care data (the defaults are used).");
        else
        {
            care.Validate(who, errors, warnings);
            if (care.actions == null || care.actions.Count == 0) warnings?.Add($"{who}: it takes no care actions.");
        }
        if (susceptibility != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimalSusceptibility s in susceptibility)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.condition)) { warnings?.Add($"{who}: a susceptibility names no condition."); continue; }
                if (!seen.Add(s.condition)) warnings?.Add($"{who}: two susceptibilities to '{s.condition}' (the first counts).");
                if (s.factor < 0f) errors?.Add($"{who}: a negative susceptibility to '{s.condition}'.");
            }
        }
        if (products != null && products.Count > 0)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimalProductSpec p in products)
            {
                if (p == null) { warnings?.Add($"{who}: an empty entry in Products."); continue; }
                if (!seen.Add(p.id ?? "")) errors?.Add($"{who}: two products are called '{p.id}'.");
                p.Validate(who, errors, warnings);
            }
            bool productive = false;
            if (stages != null)
                foreach (AnimalStageSpec st in stages)
                    if (st != null && st.productionEnabled) productive = true;
            if (!productive) warnings?.Add($"{who}: it gives products, but in no stage of its life (no stage has Production Enabled).");
        }
        if (breeding != null && breeding.enabled)
        {
            breeding.Validate(who, errors, warnings);
            bool breeds = false;
            if (stages != null)
                foreach (AnimalStageSpec st in stages)
                    if (st != null && st.breedingEnabled) breeds = true;
            if (!breeds) warnings?.Add($"{who}: it breeds, but in no stage of its life (no stage has Breeding Enabled).");
        }
        if (perks != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimalAffinityPerk p in perks)
            {
                if (p == null) { warnings?.Add($"{who}: an empty entry in Perks."); continue; }
                if (string.IsNullOrWhiteSpace(p.id)) errors?.Add($"{who}: a perk has no id.");
                else if (!seen.Add(p.id)) errors?.Add($"{who}: two perks are called '{p.id}'.");
            }
        }
    }
}
