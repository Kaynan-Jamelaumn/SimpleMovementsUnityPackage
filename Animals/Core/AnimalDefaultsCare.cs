using System;
using System.Collections.Generic;

/// <summary>
/// The built-in content of Phase 2: the care tools and supplies, the nine illnesses (every stage lists every effect: none
/// is inherited from the stage before), the medicines (brewed Alchemy outcomes and plain remedies a merchant sells), and
/// each species' care and susceptibility (used by the species builders of <c>AnimalDefaults.cs</c>). All of it is data: an
/// entry of the database with the same id replaces one.
/// </summary>
public static partial class AnimalDefaults
{
    // ================================================================ tools and supplies
    /// <summary>The care tools of animal husbandry (items of its own) and the other systems' items it uses (a shovel, soap, straw).</summary>
    public static List<AnimalToolData> Tools()
    {
        List<AnimalToolData> list = CareTools();
        list.AddRange(ProductTools());
        return list;
    }

    private static List<AnimalToolData> CareTools() => new List<AnimalToolData>
    {
        OwnTool("soft_brush", "Soft brush", "A soft-bristled body brush: for any coat, and gentle enough for a rabbit.", 0.6f, 150, 12, 0.3f, "brush", "soft_brush"),
        OwnTool("curry_comb", "Curry comb", "A rubber-toothed comb that lifts mud and loose hair from a thick coat.", 0.8f, 200, 25, 0.4f, "brush", "curry_comb"),
        OwnTool("hoof_pick", "Hoof pick", "A hooked pick and a rasp, to clean out and trim hooves.", 0.7f, 250, 15, 0.2f, "hoof_pick"),
        OwnTool("pitchfork", "Pitchfork", "A long-tined fork for mucking out and laying straw.", 0.75f, 300, 30, 2f, "fork"),
        OtherTool("shovel", "Shovel", "A shovel moves muck as well as earth.", 0.6f, false, new[] { "shovel" }, "agri:tool/shovel"),
        OtherTool("soap", "Soap", "Soft soap lifts grease and grime from a coat in the wash.", 0.6f, true, new[] { "soap" }, "craftstock:Supply:soap", "potion:soft_soap"),
        OtherTool("straw", "Straw", "Clean, dry straw for bedding.", 0.6f, true, new[] { "bedding" }, "agri:amend/straw_mulch"),
    };

    private static AnimalToolData OwnTool(string id, string name, string description, float quality, int uses, int value, float weight, params string[] tags)
    {
        var t = new AnimalToolData { id = id, name = name, description = description, quality = quality, uses = uses, value = value, weight = weight, consumed = false };
        t.tags.AddRange(tags);
        t.items.Add(AnimalIds.Tool(id));
        return t;
    }

    private static AnimalToolData OtherTool(string id, string name, string description, float quality, bool consumed, string[] tags, params string[] items)
    {
        var t = new AnimalToolData { id = id, name = name, description = description, quality = quality, uses = 0, value = 0, weight = 0.5f, consumed = consumed };
        t.tags.AddRange(tags);
        t.items.AddRange(items);
        return t;
    }

    // ================================================================ illnesses
    /// <summary>The nine built-in illnesses (DESIGN §14 with revision 1: drive and plateau, explicit stages, symptoms, diagnose-from).</summary>
    public static List<AnimalConditionData> Conditions() => new List<AnimalConditionData>
    {
        Cond("malnutrition", "Malnutrition", AnimalConditionKind.Malnutrition,
                "It has gone short of what it needs for days.",
                onset: 6f, hours: 24f, worsen: 2f, course: -4f, treatment: 101f, vet: 101f, recovery: 48f, immunity: 0f, contagion: 0f, diagnoseFrom: 0,
                death: AnimalDeathCause.Malnutrition, causes: new[] { C(AnimalCause.Malnourished, 1f) })
            .With(
                Stage("Mild", 0f, 0f, "Its coat is dull and its hips show.", growth: 0.8f),
                Stage("Moderate", 35f, 0.1f, "It is thin and slow to move.", growth: 0.5f, stress: 5f),
                Stage("Severe", 70f, 0.3f, "It is wasting away and can barely stand.", growth: 0.2f, stress: 5f))
            .Mending("It is filling out again."),

        Cond("dehydration", "Dehydration", AnimalConditionKind.Dehydration,
                "It has gone too long without water.",
                onset: 3f, hours: 6f, worsen: 6f, course: -10f, treatment: 101f, vet: 101f, recovery: 6f, immunity: 0f, contagion: 0f, diagnoseFrom: 0,
                death: AnimalDeathCause.Dehydration, causes: new[] { C(AnimalCause.Parched, 1f) })
            .With(
                Stage("Mild", 0f, 0f, "Its eyes look dull and its mouth is dry.", comfort: -10f),
                Stage("Moderate", 40f, 0.3f, "Its eyes are sunken; its skin stays tented when pinched.", comfort: -10f),
                Stage("Severe", 70f, 1f, "It staggers, weak and parched.", comfort: -10f))
            .Mending("Its eyes are bright again, though it is still weak."),

        Cond("infection", "Infection", AnimalConditionKind.Infection,
                "A fever that spreads to pen-mates.",
                onset: 24f, hours: 36f, worsen: 2.5f, course: -1f, treatment: 45f, vet: 80f, recovery: 24f, immunity: 120f, contagion: 0.6f, diagnoseFrom: 2,
                death: AnimalDeathCause.Illness,
                causes: new[]
                {
                    C(AnimalCause.DirtyHabitat, 1f), C(AnimalCause.DirtyWater, 1.5f), C(AnimalCause.LowHygiene, 0.6f), C(AnimalCause.Wound, 2f), C(AnimalCause.Chill, 0.8f),
                    C(AnimalCause.Contagion, 1.5f),
                })
            .With(
                // "Slight": the hidden start of a fever, and the tail of one that is mending.
                Stage("Slight", 0f, 0f, null, visible: false),
                Stage("Mild", 15f, 0.05f, "It coughs and its nose runs.", appetite: 0.9f),
                Stage("Moderate", 40f, 0.3f, "It is feverish and off its food.", appetite: 0.7f, comfort: -10f),
                Stage("Severe", 70f, 0.9f, "It burns with fever and breathes hard.", appetite: 0.4f, comfort: -20f))
            .Mending("Its cough is easing."),

        Cond("digestive", "Digestive upset", AnimalConditionKind.Digestive,
                "Its gut is upset by bad or spoiled food. A mild bout passes; a bad one must be settled.",
                onset: 4f, hours: 8f, worsen: 5f, course: -5f, treatment: 60f, vet: 101f, recovery: 12f, immunity: 24f, contagion: 0f, diagnoseFrom: 0,
                death: AnimalDeathCause.Illness, causes: new[] { C(AnimalCause.Upset, 1f) })
            .With(
                Stage("Mild", 0f, 0f, "Its droppings are loose.", absorption: 0.85f, hygiene: 1f),
                Stage("Moderate", 35f, 0.2f, "It stands hunched and kicks at its belly.", appetite: 0.7f, absorption: 0.7f, hygiene: 1f),
                Stage("Severe", 65f, 1.2f, "It rolls and groans with colic.", appetite: 0.4f, absorption: 0.7f, hygiene: 1f))
            .Mending("Its droppings are firming up."),

        Cond("parasites", "Worms and lice", AnimalConditionKind.Parasites,
                "Worms picked up on a fouled pasture, and lice in a dirty coat. They never clear by themselves, and spread to pen-mates.",
                onset: 30f, hours: 96f, worsen: 0.6f, course: 0.1f, treatment: 0f, vet: 101f, recovery: 24f, immunity: 72f, contagion: 0.4f, diagnoseFrom: 2,
                death: AnimalDeathCause.Illness, causes: new[] { C(AnimalCause.Worms, 1f), C(AnimalCause.LowHygiene, 0.5f), C(AnimalCause.Contagion, 1f) })
            .With(
                Stage("Light", 0f, 0f, null, visible: false),
                Stage("Mild", 20f, 0.02f, "It scratches and rubs against the fence.", absorption: 0.8f, comfort: -5f),
                Stage("Moderate", 50f, 0.1f, "Its coat is patchy and it is thin despite eating.", absorption: 0.65f, comfort: -5f),
                Stage("Severe", 80f, 0.4f, "Its gums are pale and it tires at once.", absorption: 0.5f, comfort: -5f))
            .Mending("Its coat is growing back."),

        Cond("chronic_stress", "Chronic stress", AnimalConditionKind.Stress,
                "It has been frightened, crowded or unsettled for too long.",
                onset: 16f, hours: 24f, worsen: 3f, course: -3f, treatment: 101f, vet: 101f, recovery: 12f, immunity: 0f, contagion: 0f, diagnoseFrom: 1,
                death: AnimalDeathCause.Illness, causes: new[] { C(AnimalCause.HighStress, 1f), C(AnimalCause.Crowding, 0.5f), C(AnimalCause.Loneliness, 0.25f) })
            .With(
                Stage("Mild", 0f, 0f, "It paces and will not settle.", appetite: 0.9f),
                Stage("Moderate", 40f, 0.05f, "It nips at others and picks at its food.", appetite: 0.8f, stress: 8f),
                Stage("Severe", 70f, 0.2f, "It trembles and hides in a corner.", appetite: 0.8f, stress: 8f, growth: 0.7f))
            .Mending("It is calmer, though still watchful."),

        Cond("injury", "Injury", AnimalConditionKind.Injury,
                "A hurt leg. A light one mends with rest; a bad one must be treated.",
                onset: 16f, hours: 24f, worsen: 1f, course: -2f, treatment: 50f, vet: 70f, recovery: 48f, immunity: 0f, contagion: 0f, diagnoseFrom: 0,
                death: AnimalDeathCause.Injury, causes: new[] { C(AnimalCause.HoofNeglect, 0.8f), C(AnimalCause.Fighting, 1.2f) })
            .With(
                Stage("Mild", 0f, 0f, "It favours one leg.", mobility: 0.85f, comfort: -5f),
                Stage("Moderate", 30f, 0.1f, "It limps badly.", mobility: 0.6f, comfort: -15f),
                Stage("Severe", 65f, 0.5f, "It cannot bear weight on the leg.", mobility: 0.3f, comfort: -30f))
            .Mending("It walks gingerly on the mending leg."),

        Cond("hoof_rot", "Hoof rot", AnimalConditionKind.Infection,
                "Rot in the hoof from standing in wet filth on neglected hooves.",
                onset: 20f, hours: 48f, worsen: 1.2f, course: -0.5f, treatment: 30f, vet: 85f, recovery: 36f, immunity: 48f, contagion: 0f, diagnoseFrom: 1,
                death: AnimalDeathCause.Illness, causes: new[] { C(AnimalCause.HoofNeglect, 1.2f), C(AnimalCause.DirtyHabitat, 0.8f) }, hoovedOnly: true,
                taintsCarcass: false)
            .With(
                Stage("Mild", 0f, 0f, "One hoof smells foul.", mobility: 0.85f),
                Stage("Moderate", 35f, 0.1f, "It limps on a swollen, foul-smelling hoof.", mobility: 0.6f, comfort: -15f),
                Stage("Severe", 70f, 0.4f, "The hoof is rotting and it will not stand.", mobility: 0.4f, comfort: -15f))
            .Mending("The hoof is healing clean."),

        Cond("food_poisoning", "Food poisoning", AnimalConditionKind.Poisoning,
                "It ate something poisonous.",
                onset: 2f, hours: 4f, worsen: 10f, course: -6f, treatment: 101f, vet: 101f, recovery: 12f, immunity: 0f, contagion: 0f, diagnoseFrom: 0,
                death: AnimalDeathCause.Poisoning, causes: new[] { C(AnimalCause.Toxins, 1f) })
            .With(
                Stage("Mild", 0f, 0f, "It drools and trembles.", appetite: 0.6f),
                Stage("Moderate", 40f, 0.2f, "It staggers and retches.", appetite: 0.4f),
                Stage("Severe", 70f, 0.6f, "It convulses.", appetite: 0.2f))
            .Mending("It is steadier on its feet."),
    };

    private static AnimalConditionCause C(AnimalCause cause, float weight) => new AnimalConditionCause { cause = cause, weight = weight };

    private static AnimalConditionData Cond(string id, string name, AnimalConditionKind kind, string description, float onset, float hours, float worsen, float course,
        float treatment, float vet, float recovery, float immunity, float contagion, int diagnoseFrom, AnimalDeathCause death, AnimalConditionCause[] causes,
        bool hoovedOnly = false, bool taintsCarcass = true)
    {
        var c = new AnimalConditionData
        {
            id = id, name = name, kind = kind, description = description, onsetAt = onset, exposureHours = hours, startSeverity = 5f, worsenPerHour = worsen,
            courseWithoutCause = course, needsTreatmentAbove = treatment, needsVetAbove = vet, recoveryHours = recovery, immunityHours = immunity,
            contagion = contagion, diagnoseFrom = diagnoseFrom, deathCause = death, hoovedOnly = hoovedOnly, taintsCarcass = taintsCarcass,
        };
        if (causes != null) c.causes.AddRange(causes);
        return c;
    }

    private static AnimalConditionData With(this AnimalConditionData c, params AnimalConditionStage[] stages)
    {
        c.stages.Clear();
        if (stages != null) c.stages.AddRange(stages);
        return c;
    }

    /// <summary>Its recovering stage: mild lingering weakness (appetite and growth x0.9, production x0.85, comfort -3) and what is seen of it.</summary>
    private static AnimalConditionData Mending(this AnimalConditionData c, string symptom)
    {
        c.recovering = Stage("Recovering", 0f, 0f, symptom, appetite: 0.9f, growth: 0.9f, production: 0.85f, comfort: -3f);
        return c;
    }

    private static AnimalConditionStage Stage(string name, float from, float healthPerHour, string symptom, bool visible = true, float appetite = 1f, float growth = 1f,
        float absorption = 1f, float mobility = 1f, float comfort = 0f, float stress = 0f, float hygiene = 0f, float production = 1f, float happiness = 0f)
    {
        var s = new AnimalConditionStage
        {
            name = name, from = from, visible = visible, healthPerHour = healthPerHour, appetite = appetite, growth = growth, absorption = absorption,
            mobility = mobility, comfort = comfort, stress = stress, hygienePerHour = hygiene, production = production, happiness = happiness,
        };
        if (!string.IsNullOrWhiteSpace(symptom)) s.symptoms.Add(symptom);
        return s;
    }

    // ================================================================ medicines
    /// <summary>
    /// The built-in medicines: Alchemy's potions that help animals ("potion:&lt;outcome&gt;", any bottle of that outcome),
    /// the three veterinary outcomes, and plain remedies a merchant sells (charcoal, wormwood, vinegar, lavender).
    /// </summary>
    public static List<AnimalTreatmentData> Treatments() => new List<AnimalTreatmentData>
    {
        Med("antidote", "Antidote", "Fights poison in the blood.", AnimalTreatmentRoute.Drench, 2f, 6f, 0f, true, Keys("potion:antidote"),
            E("food_poisoning", 40f, 8f, 6f), E("digestive", 0f, 1f, 6f)).Besides(toxin: 0.5f),
        Med("charcoal", "Charcoal", "Powdered charcoal in its feed soaks up poisons in the gut.", AnimalTreatmentRoute.Feed, 1f, 4f, 0f, false,
            Keys("ingredient:charcoal", "craftstock:Fuel:charcoal"),
            E("food_poisoning", 15f, 2f, 6f), E("digestive", 5f, 2f, 6f)).Besides(toxin: 0.35f, upset: 15f),
        Med("healing_draught", "Healing draught", "Speeds healing from fever and hurts.", AnimalTreatmentRoute.Drench, 2f, 6f, 0f, true, Keys("potion:healing_draught"),
            E("infection", 0f, 2.5f, 12f), E("injury", 0f, 1.5f, 12f)).Besides(health: 8f),
        Med("restoration_elixir", "Restoration elixir", "A strong restorative.", AnimalTreatmentRoute.Drench, 2f, 8f, 0f, true, Keys("potion:restoration_elixir"),
            E("infection", 10f, 3f, 16f), E("injury", 5f, 2f, 16f)).Besides(health: 15f),
        Med("healing_salve", "Healing salve", "Rubbed into wounds and hooves.", AnimalTreatmentRoute.Topical, 3f, 6f, 0f, true, Keys("potion:healing_salve"),
            E("injury", 10f, 3f, 24f), E("hoof_rot", 10f, 3f, 24f), E("infection", 0f, 0.5f, 12f)).Besides(comfort: 8f),
        Med("willowbark", "Willowbark draught", "Eases pain.", AnimalTreatmentRoute.Drench, 2f, 6f, 0.5f, true, Keys("potion:willowbark_draught"),
            E("injury", 0f, 0.5f, 12f)).Besides(comfort: 15f, stress: -10f),
        Med("calming_tonic", "Calming tonic", "Settles a frightened animal.", AnimalTreatmentRoute.Drench, 2f, 8f, 0.5f, true, Keys("potion:calming_tonic"),
            E("chronic_stress", 10f, 4f, 12f)).Besides(stress: -25f),
        Med("lavender", "Lavender tincture", "A mild calmer.", AnimalTreatmentRoute.Drench, 2f, 6f, 0f, true, Keys("potion:lavender_tincture"),
            E("chronic_stress", 5f, 2f, 12f)).Besides(stress: -12f),
        Med("ginger_tonic", "Ginger tonic", "Settles an upset gut.", AnimalTreatmentRoute.Drench, 2f, 6f, 0f, true, Keys("potion:ginger_tonic"),
            E("digestive", 15f, 6f, 8f)).Besides(upset: 30f),
        Med("broth", "Traveller's broth", "Feeds and waters a weak animal.", AnimalTreatmentRoute.Feed, 2f, 4f, 0f, true, Keys("potion:travellers_broth"),
            E("malnutrition", 0f, 2f, 24f), E("dehydration", 10f, 8f, 4f)).Besides(satiety: 20f, hydration: 30f),
        Med("panacea", "Panacea", "Cures almost anything, at a price.", AnimalTreatmentRoute.Drench, 2f, 12f, 1.2f, true, Keys("potion:panacea"),
            E(AnimalTreatmentData.AnyCondition, 30f, 5f, 24f)).Besides(health: 20f, toxin: 0.5f),
        Med("vermifuge", "Vermifuge", "The veterinary wormer: clears worms and lice.", AnimalTreatmentRoute.Drench, 2f, 24f, 1.5f, true, Keys("potion:vet_vermifuge"),
            E("parasites", 20f, 6f, 12f)),
        Med("antiseptic", "Antiseptic wash", "The veterinary wash for wounds, hooves and skin.", AnimalTreatmentRoute.Topical, 3f, 6f, 0f, true, Keys("potion:vet_antiseptic"),
            E("infection", 5f, 3f, 12f), E("hoof_rot", 10f, 4f, 12f), E("injury", 0f, 1f, 12f)),
        Med("electrolytes", "Electrolytes", "The veterinary drench for a parched or purged animal.", AnimalTreatmentRoute.Drench, 2f, 3f, 0f, true, Keys("potion:vet_electrolytes"),
            E("dehydration", 30f, 10f, 4f), E("digestive", 0f, 1f, 6f)).Besides(hydration: 40f),
        Med("wormwood", "Wormwood", "The bitter herb in its feed drives out worms, more slowly than a vermifuge.", AnimalTreatmentRoute.Feed, 1f, 24f, 0.6f, false,
            Keys("ingredient:wormwood"), E("parasites", 5f, 2f, 12f)),
        Med("vinegar_wash", "Vinegar wash", "Sour vinegar washed into a sore or a rotting hoof. It stings.", AnimalTreatmentRoute.Topical, 2f, 6f, 0f, false,
            Keys("ingredient:vinegar"), E("infection", 5f, 2f, 12f), E("hoof_rot", 5f, 2f, 12f)).Besides(comfort: -3f),
        Med("lavender_herb", "Lavender", "Lavender flowers in its feed calm it a little.", AnimalTreatmentRoute.Feed, 1f, 6f, 0f, false,
            Keys("ingredient:lavender", "agri:produce/lavender"), E("chronic_stress", 3f, 1f, 12f)).Besides(stress: -8f),
    };

    private static string[] Keys(params string[] keys) => keys;

    private static AnimalTreatmentEffect E(string condition, float immediate, float perHour, float hours) =>
        new AnimalTreatmentEffect { condition = condition, immediate = immediate, perHour = perHour, hours = hours };

    private static AnimalTreatmentData Med(string id, string name, string description, AnimalTreatmentRoute route, float dosesPerItem, float intervalHours, float overdoseShare,
        bool qualityMatters, string[] items, params AnimalTreatmentEffect[] effects)
    {
        var t = new AnimalTreatmentData
        {
            id = id, name = name, description = description, route = route, dosesPerItem = dosesPerItem, minIntervalHours = intervalHours, overdoseShare = overdoseShare,
            qualityMatters = qualityMatters, handlingStress = route == AnimalTreatmentRoute.Feed ? 1f : 4f,
        };
        if (items != null) t.items.AddRange(items);
        if (effects != null) t.effects.AddRange(effects);
        return t;
    }

    private static AnimalTreatmentData Besides(this AnimalTreatmentData t, float toxin = 0f, float upset = 0f, float health = 0f, float comfort = 0f, float stress = 0f,
        float satiety = 0f, float hydration = 0f)
    {
        t.toxinCleared = toxin;
        t.upsetEased = upset;
        t.healthNow = health;
        t.comfort = comfort;
        t.stress = stress;
        t.satiety = satiety;
        t.hydration = hydration;
        return t;
    }

    // ================================================================ species' care (the species builders use these)
    private static AnimalCareSpec CareSpec(float loss, float groom, float rest, float messiness, float hoofWear, float comfortMin, float comfortMax, float rain,
        float bedding, float crowded, int companions, params AnimalCareActionSpec[] actions)
    {
        var c = new AnimalCareSpec
        {
            hygieneLossPerHour = loss, selfGroomPerHour = groom, selfGroomCap = rest, messiness = messiness, mudSensitivity = 1f, hoofWearPerHour = hoofWear,
            comfortMin = comfortMin, comfortMax = comfortMax, rainDislike = rain, beddingNeed = bedding, crowdedFrom = crowded, companionsWanted = companions,
        };
        if (actions != null) c.actions.AddRange(actions);
        return c;
    }

    private static List<AnimalSusceptibility> Prone(params (string condition, float factor)[] list)
    {
        var l = new List<AnimalSusceptibility>();
        if (list != null)
            foreach ((string condition, float factor) in list) l.Add(new AnimalSusceptibility { condition = condition, factor = factor });
        return l;
    }

    /// <summary>Brushing: hygiene +18, comfort +10, affinity +2, happiness +2, a quarter of the lice out; every 8 h, harm past 2 in 24 h.</summary>
    private static AnimalCareActionSpec BrushAction(string reaction, params string[] tools)
    {
        var a = new AnimalCareActionSpec
        {
            id = "brush", kind = AnimalCareKind.Brush, hygiene = 18f, comfort = 10f, happiness = 2f, affinity = 2f, parasiteRelief = 0.25f, cooldownHours = 8f,
            overuseHours = 24f, overuseAfter = 2, overuseStress = 4f, overuseComfort = -8f, reaction = reaction ?? "",
        };
        a.tools.AddRange(tools != null && tools.Length > 0 ? tools : new[] { "brush" });
        return a;
    }

    /// <summary>Petting (bare hands): affinity +3, happiness +3, stress -3 (a skittish animal bears it less); every hour, harm past 3 in 6 h.</summary>
    private static AnimalCareActionSpec PetAction(string reaction) => new AnimalCareActionSpec
    {
        id = "pet", kind = AnimalCareKind.Pet, happiness = 3f, affinity = 3f, stress = -3f, handlingStress = 6f, cooldownHours = 1f, overuseHours = 6f,
        overuseAfter = 3, overuseStress = 6f, reaction = reaction ?? "",
    };

    /// <summary>Washing: hygiene +30 and comfort +5 (both x the water that came), soap x1.5; once a day, harm past 2 in 72 h; it chills below <paramref name="chillBelow"/> °C.</summary>
    private static AnimalCareActionSpec WashAction(float litres, float chillBelow, bool soap, string reaction) => new AnimalCareActionSpec
    {
        id = "wash", kind = AnimalCareKind.Wash, waterLitres = litres, supply = soap ? "soap" : "", supplyBoost = 1.5f, hygiene = 30f, comfort = 5f, cooldownHours = 24f,
        overuseHours = 72f, overuseAfter = 2, overuseComfort = -10f, chillBelow = chillBelow, chillComfort = 3f, reaction = reaction ?? "",
    };

    /// <summary>Cleaning hooves (a hoof pick): hooves +60, comfort +5; once a day.</summary>
    private static AnimalCareActionSpec HoovesAction(string reaction)
    {
        var a = new AnimalCareActionSpec
        {
            id = "clean_hooves", kind = AnimalCareKind.CleanHooves, hooves = 60f, comfort = 5f, handlingStress = 6f, cooldownHours = 24f, overuseHours = 24f,
            overuseAfter = 2, overuseStress = 4f, reaction = reaction ?? "",
        };
        a.tools.Add("hoof_pick");
        return a;
    }

    private static AnimalCareSpec ChickenCare()
    {
        AnimalCareActionSpec wash = WashAction(1f, 22f, false, "A wet hen shivers and squawks.");
        wash.inappropriate = true;
        wash.hygiene = 12f;
        wash.comfort = -20f;
        wash.stress = 18f;
        wash.chillComfort = 0f; // its comfort loss already holds the chill
        AnimalCareSpec c = CareSpec(0.5f, 0.7f, 50f, 1f, 0f, 8f, 26f, 0.8f, 0.6f, 0.8f, 2, PetAction("{name} clucks softly and settles in your arms."), wash);
        c.dustBath = 1f;
        return c;
    }

    private static AnimalCareSpec DuckCare()
    {
        AnimalCareSpec c = CareSpec(0.6f, 0.85f, 55f, 1.3f, 0f, 4f, 26f, 0.1f, 0.4f, 0.8f, 1,
            PetAction("{name} quacks and nibbles at your fingers."), WashAction(1f, 10f, false, "{name} splashes about and preens."));
        c.mudSensitivity = 0.6f;
        c.dustBath = 0.3f;
        c.bathes = 1f;
        return c;
    }

    private static AnimalCareSpec RabbitCare()
    {
        AnimalCareActionSpec wash = WashAction(1f, 30f, false, "Rabbits are not bathed: {name} panics.");
        wash.inappropriate = true;
        wash.hygiene = 10f;
        wash.comfort = -25f;
        wash.stress = 25f;
        wash.chillComfort = 0f; // its comfort loss already holds the chill
        return CareSpec(0.3f, 0.5f, 60f, 0.7f, 0f, 5f, 24f, 0.9f, 0.8f, 0.75f, 0,
            BrushAction("{name} flattens its ears and lets you brush it.", "soft_brush"), PetAction("{name} twitches its nose and leans into your hand."), wash);
    }

    private static AnimalCareSpec SheepCare()
    {
        AnimalCareActionSpec wash = WashAction(5f, 14f, true, "You work the water through {name}'s fleece.");
        wash.overuseAfter = 1;
        return CareSpec(0.45f, 0.65f, 55f, 0.8f, 0.08f, -5f, 22f, 0.3f, 0.4f, 0.85f, 2,
            BrushAction("You work the brush through {name}'s fleece."), wash, PetAction("{name} bleats and nudges your hand."),
            HoovesAction("{name} kicks a little, then lets you trim its hooves."));
    }

    private static AnimalCareSpec GoatCare()
    {
        AnimalCareActionSpec wash = WashAction(4f, 16f, false, "{name} bleats and stamps: goats hate a wash.");
        wash.happiness = -6f;
        wash.comfort = -4f;
        wash.stress = 6f;
        return CareSpec(0.35f, 0.5f, 55f, 0.8f, 0.07f, 0f, 26f, 0.9f, 0.5f, 0.8f, 1,
            BrushAction("{name} wriggles under the brush, then holds still."), wash, PetAction("{name} butts your hand for more."),
            HoovesAction("{name} lifts each hoof in turn."));
    }

    private static AnimalCareSpec PigCare()
    {
        AnimalCareActionSpec wash = WashAction(6f, 12f, false, "{name} wallows and grunts in the water.");
        wash.heatAbove = 22f;
        wash.heatComfort = 2f;
        AnimalCareSpec c = CareSpec(0.7f, 1f, 50f, 1.3f, 0.04f, 8f, 24f, 0.3f, 0.6f, 0.8f, 0,
            BrushAction("{name} grunts happily as you scrub its bristles."), wash, PetAction("{name} flops over for a belly rub."),
            HoovesAction("{name} squeals, then lets you trim its trotters."));
        c.mudSensitivity = 0.5f;
        return c;
    }

    private static AnimalCareSpec CowCare() => CareSpec(0.45f, 0.65f, 55f, 1.1f, 0.08f, -5f, 24f, 0.3f, 0.5f, 0.85f, 1,
        BrushAction("{name} leans into the comb and sighs.", "brush", "curry_comb"), WashAction(8f, 12f, true, "{name} stands patiently while you wash it."),
        PetAction("{name} licks your sleeve."), HoovesAction("{name} shifts its weight and lets you lift each hoof."));

    private static AnimalCareSpec HorseCare() => CareSpec(0.45f, 0.65f, 55f, 1f, 0.1f, -5f, 25f, 0.4f, 0.6f, 0.85f, 1,
        BrushAction("{name} stretches its neck as you brush it."), WashAction(8f, 14f, true, "{name} snorts as the water runs down its flanks."),
        PetAction("{name} nuzzles your shoulder."), HoovesAction("{name} lifts each hoof in turn for the pick."));
}
