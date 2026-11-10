using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The built-in content of animal husbandry: eight species (chicken, duck, rabbit, sheep, goat, pig, cow, horse) with
/// three breeds each, and the foods they eat - feed made for animals and the farm's own produce; and (Phase 2, in
/// <c>AnimalDefaultsCare.cs</c>) each species' care and susceptibility, the care tools, the illnesses and the medicines.
/// All of it is data: an Animal Species asset or an Animal Food asset with the same id replaces an entry, and new ones
/// need no code. Durations are game hours (a game day is 24 of them).
/// </summary>
public static partial class AnimalDefaults
{
    // ---------------------------------------------------------------- foods
    public static List<AnimalFoodData> Foods() => new List<AnimalFoodData>
    {
        F("fresh_grass", "Fresh grass", "Cut green grass: what grazers live on. It wilts in a day or two.", 4, 9f, 4f, N(1.5f, 0.4f, 3.0f, 0.08f, 0.15f), 36f, "forage", "greens"),
        F("hay", "Hay", "Dried grass: keeps all winter, filling, a little short of vitamins.", 6, 11f, 0f, N(2.0f, 0.5f, 4.0f, 0.06f, 0.04f), 0f, "forage", "dry"),
        F("alfalfa", "Alfalfa", "A rich legume forage, high in protein.", 4, 11f, 1f, N(2.4f, 1.6f, 3.2f, 0.12f, 0.12f), 48f, "forage", "legume"),
        F("clover", "Clover", "Sweet clover: grazers love it.", 4, 9f, 3f, N(1.8f, 1.2f, 2.6f, 0.1f, 0.14f), 36f, "forage", "legume", "greens"),
        F("grain_mix", "Grain mix", "Cracked grain for fattening and for poultry.", 4, 14f, 0f, N(5.0f, 1.0f, 0.8f, 0.06f, 0.04f), 0f, "grain"),
        F("chicken_feed", "Chicken feed", "Layer pellets: grain, meal and grit, balanced for poultry.", 6, 14f, 0f, N(4.0f, 2.2f, 0.8f, 0.2f, 0.1f), 0f, "grain", "meal", "poultry"),
        F("oatmeal", "Oatmeal", "Cooked oats: a soft, warm meal.", 2, 12f, 2f, N(4.2f, 0.9f, 0.9f, 0.04f, 0.03f), 24f, "grain", "cooked"),
        F("low_quality_feed", "Cheap feed", "Bulked-out feed: it fills, it does not nourish.", 6, 12f, 0f, N(2.5f, 0.3f, 1.5f, 0.02f, 0.01f), 0f, "grain", "cheap"),
        F("spoiled_feed", "Spoiled feed", "Mouldy, sour feed.", 4, 8f, 0f, N(1.2f, 0.2f, 1.0f, 0f, 0f), 0f, "spoiled").Bad(2f, 10f),
        F("poisonous_herbs", "Poisonous herbs", "A bundle of weeds no beast should eat.", 2, 5f, 1f, N(0.5f, 0.3f, 1.0f, 0.05f, 0.1f), 24f, "poisonous", "greens").Bad(9f, 6f),
        F("kitchen_scraps", "Kitchen scraps", "Peelings, crusts and leftovers.", 3, 12f, 3f, N(3.5f, 0.9f, 1.0f, 0.1f, 0.25f), 24f, "scraps", "vegetable"),
        F("mealworms", "Dried mealworms", "A protein treat poultry go wild for.", 6, 8f, 0f, N(1.5f, 3.0f, 0.2f, 0.1f, 0.05f), 0f, "insect", "protein"),
        F("milk_replacer", "Milk replacer", "Warm milk for a calf or foal with no mother to suckle.", 4, 12f, 10f, N(3.0f, 1.5f, 0f, 0.1f, 0.1f), 24f, "milk"),
        // The farm's produce (Agriculture): its quality per copy feeds better or worse.
        F("wheat", "Wheat", "Grain straight from the field.", 1, 13f, 0f, N(4.6f, 1.1f, 1.0f, 0.05f, 0.04f), 0f, "grain").Items("agri:produce/wheat"),
        F("carrot", "Carrot", "Sweet roots.", 1, 10f, 3f, N(2.5f, 0.3f, 1.2f, 0.08f, 0.4f), 72f, "vegetable", "root").Items("agri:produce/carrot"),
        F("cabbage", "Cabbage", "Leafy greens (gassy for some).", 2, 8f, 5f, N(1.2f, 0.5f, 1.4f, 0.1f, 0.45f), 48f, "vegetable", "greens").Items("agri:produce/cabbage"),
        F("potato", "Raw potato", "Starchy, and raw it upsets many animals.", 1, 12f, 2f, N(4.0f, 0.4f, 0.6f, 0.05f, 0.1f), 96f, "vegetable", "root").Items("agri:produce/potato"),
        F("bean", "Beans", "A protein-rich legume.", 1, 11f, 0f, N(2.8f, 2.0f, 1.0f, 0.08f, 0.06f), 0f, "legume").Items("agri:produce/bean"),
        F("apple", "Apple", "A sweet treat most beasts adore.", 1, 9f, 4f, N(2.6f, 0.1f, 0.8f, 0.02f, 0.35f), 96f, "fruit").Items("agri:produce/apple"),
        F("strawberry", "Strawberries", "Soft, sweet berries.", 1, 6f, 4f, N(1.6f, 0.2f, 0.5f, 0.02f, 0.4f), 36f, "fruit").Items("agri:produce/strawberry"),
        F("comfrey", "Comfrey", "A leafy herb, rich in minerals.", 1, 7f, 3f, N(1.2f, 0.9f, 1.6f, 0.25f, 0.2f), 48f, "herb", "greens").Items("agri:produce/comfrey"),
        F("nightshade", "Nightshade", "Poisonous berries and leaves.", 1, 5f, 2f, N(0.6f, 0.2f, 0.6f, 0.02f, 0.05f), 48f, "poisonous").Items("agri:produce/nightshade").Bad(12f, 4f),
        F("mandrake", "Mandrake root", "A magical root, poisonous to beasts.", 1, 6f, 1f, N(1.0f, 0.3f, 0.8f, 0.1f, 0.05f), 0f, "poisonous", "magical").Items("agri:produce/mandrake").Bad(10f, 6f),
        // Harvested meat (any harvested food piece): only omnivores take it.
        F("meat_scraps", "Meat", "Meat and offal.", 1, 14f, 2f, N(2.0f, 3.5f, 0f, 0.2f, 0.1f), 24f, "meat").Items("harvest:*"),
    };

    // ---------------------------------------------------------------- species
    public static List<AnimalSpeciesData> Species()
    {
        var list = new List<AnimalSpeciesData> { Chicken(), Duck(), Rabbit(), Sheep(), Goat(), Pig(), Cow(), Horse() };
        // What each gives while it lives (Phase 3), how it breeds, behaves and bonds (Phase 4), what each breed is bred for
        // (Phase 5), and the version of the content they are.
        foreach (AnimalSpeciesData s in list)
        {
            s.products = ProductsOf(s.id);
            s.breeding = BreedingOf(s.id);
            s.behaviour = BehaviourOf(s.id);
            s.perks = PerksOf(s.id);
            SpecialiseBreeds(s);
            s.dataVersion = 5;
        }
        return list;
    }

    public static AnimalSpeciesData Chicken()
    {
        AnimalSpeciesData s = S("chicken", "Chicken", "Chickens", "A hen of the yard: scratches, pecks and lays.", AnimalHabitatKind.Coop, 1.5f, 15);
        s.youngName = "Chick";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 72f, 0.35f, 0.3f, protein: 1.3f),
            St(GrowthStage.Juvenile, 120f, 0.7f, 0.65f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f, production: true),
        };
        s.seniorAgeHours = 1440f;
        s.lifespanHours = 2880f;
        Needs(s, 0.2f, 3.2f, 4.5f, 0.004f, N(0.85f, 0.45f, 0.15f, 0.045f, 0.03f), N(1f, 1f, 0.3f, 0.7f, 0.5f));
        s.diet = AnimalDiet.Of(
            favorite: new[] { "mealworms", "#insect" },
            preferred: new[] { "chicken_feed", "#grain" },
            normal: new[] { "#vegetable", "#fruit", "oatmeal", "kitchen_scraps" },
            neutral: new[] { "clover", "fresh_grass", "#herb" },
            poor: new[] { "low_quality_feed", "hay", "alfalfa" },
            harmful: new[] { "spoiled_feed", "potato" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Bird, 0.4f, 0.4f, MobLandGait.Walk, 0.6f);
        s.breeds = new List<AnimalBreedData>
        {
            B("leghorn", "Leghorn", 1.05f, 0.95f, 0.9f, 0.9f, 15, 0.4f, 0.55f, 0f, C(0.96f, 0.95f, 0.9f)),
            B("rhode_island_red", "Rhode Island Red", 1f, 1f, 1.05f, 1.2f, 18, 0.6f, 0.5f, 0.1f, C(0.55f, 0.24f, 0.12f), C(0.62f, 0.3f, 0.15f)),
            B("silkie", "Silkie", 0.9f, 0.9f, 0.8f, 0.9f, 40, 0.9f, 0.6f, 0.7f, C(0.98f, 0.98f, 0.97f), C(0.12f, 0.12f, 0.13f)),
        };
        s.care = ChickenCare();
        s.susceptibility = Prone(("infection", 1.2f));
        s.harvestAnatomy = "chicken";
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Duck()
    {
        AnimalSpeciesData s = S("duck", "Duck", "Ducks", "A dabbling duck: hardy, happiest with water to paddle in.", AnimalHabitatKind.Coop | AnimalHabitatKind.Pond, 2f, 18);
        s.youngName = "Duckling";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 72f, 0.35f, 0.3f, protein: 1.3f),
            St(GrowthStage.Juvenile, 144f, 0.75f, 0.7f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f, production: true),
        };
        s.seniorAgeHours = 1800f;
        s.lifespanHours = 3600f;
        Needs(s, 0.25f, 3f, 6f, 0.005f, N(0.85f, 0.42f, 0.18f, 0.04f, 0.03f), N(1f, 1f, 0.3f, 0.6f, 0.5f));
        s.needs.toxinTolerance = 9f;
        s.diet = AnimalDiet.Of(
            favorite: new[] { "mealworms", "#insect" },
            preferred: new[] { "chicken_feed", "grain_mix", "wheat" },
            normal: new[] { "#vegetable", "oatmeal", "clover", "#grain" },
            neutral: new[] { "fresh_grass", "#fruit" },
            poor: new[] { "low_quality_feed", "hay" },
            harmful: new[] { "spoiled_feed" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Bird, 0.45f, 0.35f, MobLandGait.Walk, 0.5f);
        s.breeds = new List<AnimalBreedData>
        {
            B("pekin", "Pekin", 1.05f, 1f, 1.05f, 1f, 18, 0.6f, 0.5f, 0f, C(0.97f, 0.96f, 0.9f)),
            B("mallard", "Mallard", 0.95f, 0.9f, 0.85f, 1.25f, 16, 0.3f, 0.5f, 0.1f, C(0.45f, 0.35f, 0.25f), C(0.2f, 0.4f, 0.25f)),
            B("khaki_campbell", "Khaki Campbell", 1f, 0.95f, 0.9f, 1.05f, 24, 0.5f, 0.6f, 0.3f, C(0.62f, 0.52f, 0.36f)),
        };
        s.care = DuckCare();
        s.susceptibility = Prone(("infection", 1.2f));
        s.harvestAnatomy = "duck";
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Rabbit()
    {
        AnimalSpeciesData s = S("rabbit", "Rabbit", "Rabbits", "A hutch rabbit: grows fast, needs plenty of hay.", AnimalHabitatKind.Hutch, 1.5f, 12);
        s.youngName = "Kit";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 48f, 0.35f, 0.3f, protein: 1.2f),
            St(GrowthStage.Juvenile, 96f, 0.75f, 0.7f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f),
        };
        s.seniorAgeHours = 1440f;
        s.lifespanHours = 2400f;
        Needs(s, 0.2f, 3.5f, 4.5f, 0.004f, N(0.6f, 0.14f, 1.0f, 0.02f, 0.05f), N(0.8f, 0.6f, 1f, 0.4f, 0.6f));
        s.needs.toxinTolerance = 6f;
        s.diet = AnimalDiet.Of(
            favorite: new[] { "carrot", "clover" },
            preferred: new[] { "hay", "fresh_grass", "comfrey" },
            normal: new[] { "alfalfa", "apple", "strawberry", "#herb" },
            neutral: new[] { "oatmeal" },
            poor: new[] { "#grain", "low_quality_feed", "cabbage" },
            harmful: new[] { "spoiled_feed", "potato" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Small, 0.4f, 0.25f, MobLandGait.Hop, 1f);
        s.harvestAnatomy = "rabbit";
        s.breeds = new List<AnimalBreedData>
        {
            B("new_zealand_white", "New Zealand White", 1.1f, 1.05f, 1.1f, 1f, 12, 0.6f, 0.5f, 0f, C(0.97f, 0.97f, 0.95f)),
            B("dutch", "Dutch", 1f, 0.95f, 0.85f, 1.05f, 14, 0.7f, 0.5f, 0.1f, C(0.15f, 0.13f, 0.12f), C(0.5f, 0.4f, 0.3f)),
            B("rex", "Rex", 0.95f, 1f, 1f, 0.9f, 35, 0.5f, 0.7f, 0.6f, C(0.6f, 0.45f, 0.35f), C(0.35f, 0.35f, 0.38f)),
        };
        s.care = RabbitCare();
        s.susceptibility = Prone(("digestive", 1.4f), ("chronic_stress", 1.3f));
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Sheep()
    {
        AnimalSpeciesData s = S("sheep", "Sheep", "Sheep", "A grazing sheep: wool, mutton, and a flock's company.", AnimalHabitatKind.Pasture | AnimalHabitatKind.Barn, 6f, 60);
        s.youngName = "Lamb";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 48f, 0.35f, 0.3f, protein: 1.25f),
            St(GrowthStage.Juvenile, 96f, 0.7f, 0.65f),
            St(GrowthStage.Adolescent, 72f, 0.9f, 0.85f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f, production: true),
        };
        s.seniorAgeHours = 3000f;
        s.lifespanHours = 4800f;
        Needs(s, 1.5f, 3f, 4f, 0.08f, N(0.5f, 0.14f, 0.8f, 0.025f, 0.035f), N(1f, 0.8f, 1f, 0.4f, 0.5f));
        s.diet = AnimalDiet.Of(
            favorite: new[] { "clover", "fresh_grass" },
            preferred: new[] { "hay", "alfalfa" },
            normal: new[] { "#grain", "carrot", "apple", "#herb" },
            neutral: new[] { "oatmeal", "cabbage" },
            poor: new[] { "low_quality_feed" },
            harmful: new[] { "spoiled_feed", "potato" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Quadruped, 1.2f, 0.8f, MobLandGait.Walk, 0.9f);
        s.harvestAnatomy = "sheep";
        s.breeds = new List<AnimalBreedData>
        {
            B("merino", "Merino", 0.95f, 1f, 0.95f, 0.9f, 70, 0.6f, 0.7f, 0.1f, C(0.95f, 0.92f, 0.84f)),
            B("suffolk", "Suffolk", 1.1f, 1.1f, 1.15f, 1.05f, 60, 0.5f, 0.5f, 0f, C(0.92f, 0.9f, 0.85f)),
            B("jacob", "Jacob", 1f, 0.95f, 0.95f, 1.15f, 90, 0.4f, 0.6f, 0.6f, C(0.9f, 0.88f, 0.82f), C(0.2f, 0.17f, 0.15f)),
        };
        s.care = SheepCare();
        s.susceptibility = Prone(("parasites", 1.4f));
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Goat()
    {
        AnimalSpeciesData s = S("goat", "Goat", "Goats", "A browsing goat: tough, curious, eats almost anything green.", AnimalHabitatKind.Pasture | AnimalHabitatKind.Barn, 5f, 55);
        s.youngName = "Kid";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 48f, 0.35f, 0.3f, protein: 1.25f),
            St(GrowthStage.Juvenile, 96f, 0.7f, 0.65f),
            St(GrowthStage.Adolescent, 72f, 0.9f, 0.85f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f, production: true),
        };
        s.seniorAgeHours = 3600f;
        s.lifespanHours = 5400f;
        Needs(s, 1.3f, 3f, 3.5f, 0.08f, N(0.55f, 0.16f, 0.75f, 0.03f, 0.04f), N(1f, 0.8f, 0.9f, 0.5f, 0.5f));
        s.needs.toxinTolerance = 12f;
        s.needs.toxinClearance = 0.07f;
        s.diet = AnimalDiet.Of(
            favorite: new[] { "apple", "comfrey", "clover" },
            preferred: new[] { "alfalfa", "hay", "fresh_grass" },
            normal: new[] { "#grain", "#vegetable", "#fruit", "#herb", "kitchen_scraps" },
            neutral: new[] { "oatmeal" },
            poor: new[] { "low_quality_feed" },
            harmful: new[] { "spoiled_feed" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Quadruped, 1.1f, 0.8f, MobLandGait.Walk, 1f);
        s.harvestAnatomy = "goat";
        s.breeds = new List<AnimalBreedData>
        {
            B("saanen", "Saanen", 1f, 1.05f, 1.05f, 1f, 55, 0.7f, 0.6f, 0f, C(0.97f, 0.96f, 0.92f)),
            B("boer", "Boer", 1.1f, 1.1f, 1.2f, 1.1f, 65, 0.6f, 0.5f, 0.1f, C(0.95f, 0.93f, 0.88f), C(0.5f, 0.28f, 0.15f)),
            B("nubian", "Nubian", 1f, 1f, 1f, 1f, 75, 0.5f, 0.7f, 0.4f, C(0.45f, 0.3f, 0.2f), C(0.2f, 0.16f, 0.13f)),
        };
        s.care = GoatCare();
        s.susceptibility = Prone(("parasites", 0.8f));
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Pig()
    {
        AnimalSpeciesData s = S("pig", "Pig", "Pigs", "A farm pig: eats almost anything, grows heavy on good feed.", AnimalHabitatKind.Sty | AnimalHabitatKind.Barn | AnimalHabitatKind.Pasture, 6f, 70);
        s.youngName = "Piglet";
        s.stages = new List<AnimalStageSpec>
        {
            St(GrowthStage.Baby, 48f, 0.35f, 0.25f, protein: 1.3f),
            St(GrowthStage.Juvenile, 72f, 0.75f, 0.6f),
            St(GrowthStage.Adolescent, 72f, 0.95f, 0.85f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f),
        };
        s.seniorAgeHours = 3600f;
        s.lifespanHours = 5400f;
        Needs(s, 2.2f, 3f, 4.5f, 0.1f, N(0.9f, 0.22f, 0.2f, 0.025f, 0.04f), N(1f, 1f, 0.3f, 0.4f, 0.5f));
        s.needs.heatAbove = 22f;
        s.needs.heatThirst = 0.08f;
        s.diet = AnimalDiet.Of(
            favorite: new[] { "kitchen_scraps", "apple" },
            preferred: new[] { "grain_mix", "#vegetable", "#root" },
            normal: new[] { "#grain", "#fruit", "meat_scraps", "oatmeal", "bean" },
            neutral: new[] { "fresh_grass", "clover", "#herb" },
            poor: new[] { "hay", "low_quality_feed", "alfalfa" },
            harmful: new[] { "spoiled_feed" },
            toxic: new[] { "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Quadruped, 1.3f, 0.75f, MobLandGait.Walk, 0.8f);
        s.harvestAnatomy = "pig";
        s.breeds = new List<AnimalBreedData>
        {
            B("large_white", "Large White", 1.1f, 1.05f, 1.1f, 0.95f, 70, 0.6f, 0.5f, 0f, C(0.95f, 0.82f, 0.78f)),
            B("berkshire", "Berkshire", 1f, 1f, 1f, 1.05f, 90, 0.6f, 0.7f, 0.3f, C(0.12f, 0.11f, 0.11f)),
            B("tamworth", "Tamworth", 0.95f, 0.95f, 0.95f, 1.25f, 80, 0.4f, 0.6f, 0.4f, C(0.75f, 0.42f, 0.2f)),
        };
        s.care = PigCare();
        s.susceptibility = Prone(("infection", 1.1f), ("parasites", 0.9f));
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Cow()
    {
        AnimalSpeciesData s = S("cow", "Cow", "Cattle", "A cow of the pasture: grass in, milk (and, Phase 4, calves) out.", AnimalHabitatKind.Pasture | AnimalHabitatKind.Barn, 16f, 180);
        s.youngName = "Calf";
        AnimalStageSpec newborn = St(GrowthStage.Newborn, 24f, 0.15f, 0.2f, protein: 1.4f);
        newborn.diet = AnimalDiet.Of(favorite: new[] { "milk_replacer", "#milk" });
        s.stages = new List<AnimalStageSpec>
        {
            newborn,
            St(GrowthStage.Baby, 48f, 0.35f, 0.35f, protein: 1.25f),
            St(GrowthStage.Juvenile, 96f, 0.7f, 0.65f),
            St(GrowthStage.Adolescent, 72f, 0.9f, 0.85f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: true, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f, production: true),
        };
        s.seniorAgeHours = 4800f;
        s.lifespanHours = 7200f;
        Needs(s, 4f, 3f, 4f, 0.3f, N(0.5f, 0.15f, 0.85f, 0.025f, 0.035f), N(1f, 0.8f, 1f, 0.4f, 0.5f));
        s.diet = AnimalDiet.Of(
            favorite: new[] { "fresh_grass", "alfalfa" },
            preferred: new[] { "hay", "clover" },
            normal: new[] { "#grain", "carrot", "apple", "#herb" },
            neutral: new[] { "oatmeal", "cabbage" },
            poor: new[] { "low_quality_feed" },
            harmful: new[] { "spoiled_feed", "potato" },
            toxic: new[] { "poisonous_herbs", "#poisonous" });
        Look(s, AnimalLookSpec.Shape.Quadruped, 2.2f, 1.45f, MobLandGait.Walk, 0.9f);
        s.breeds = new List<AnimalBreedData>
        {
            B("holstein", "Holstein", 1f, 1.1f, 1.1f, 0.9f, 190, 0.6f, 0.55f, 0f, C(0.95f, 0.95f, 0.93f), C(0.1f, 0.1f, 0.1f)),
            B("angus", "Angus", 1.05f, 1f, 1.05f, 1.2f, 180, 0.5f, 0.55f, 0.1f, C(0.08f, 0.08f, 0.08f)),
            B("jersey", "Jersey", 0.95f, 0.85f, 0.85f, 1f, 210, 0.7f, 0.75f, 0.4f, C(0.7f, 0.52f, 0.34f)),
        };
        s.care = CowCare();
        s.susceptibility = Prone(("hoof_rot", 1.1f));
        s.harvestAnatomy = "cow";
        s.dataVersion = 2;
        return s;
    }

    public static AnimalSpeciesData Horse()
    {
        AnimalSpeciesData s = S("horse", "Horse", "Horses", "A horse: hay, oats and apples, and a fine bond with its rider in time.", AnimalHabitatKind.Pasture | AnimalHabitatKind.Stable, 20f, 400);
        s.youngName = "Foal";
        AnimalStageSpec newborn = St(GrowthStage.Newborn, 24f, 0.15f, 0.25f, protein: 1.4f);
        newborn.diet = AnimalDiet.Of(favorite: new[] { "milk_replacer", "#milk" });
        s.stages = new List<AnimalStageSpec>
        {
            newborn,
            St(GrowthStage.Baby, 72f, 0.35f, 0.4f, protein: 1.25f),
            St(GrowthStage.Juvenile, 120f, 0.7f, 0.7f),
            St(GrowthStage.Adolescent, 120f, 0.9f, 0.9f),
            St(GrowthStage.Adult, 0f, 1f, 1f, production: false, breeding: true),
            St(GrowthStage.Senior, 0f, 0.9f, 1f),
        };
        s.seniorAgeHours = 6000f;
        s.lifespanHours = 9600f;
        Needs(s, 4.5f, 3f, 4f, 0.3f, N(0.6f, 0.15f, 0.9f, 0.025f, 0.035f), N(1f, 0.8f, 1f, 0.5f, 0.5f));
        s.diet = AnimalDiet.Of(
            favorite: new[] { "apple", "carrot" },
            preferred: new[] { "hay", "fresh_grass", "alfalfa", "oatmeal" },
            normal: new[] { "#grain", "clover", "#herb" },
            neutral: new[] { "strawberry" },
            poor: new[] { "low_quality_feed" },
            harmful: new[] { "spoiled_feed", "cabbage" },
            toxic: new[] { "#poisonous", "potato" });
        Look(s, AnimalLookSpec.Shape.Quadruped, 2.3f, 1.6f, MobLandGait.Walk, 1.3f);
        s.breeds = new List<AnimalBreedData>
        {
            B("draft", "Draft", 0.95f, 1.15f, 1.2f, 1.15f, 380, 0.75f, 0.5f, 0f, C(0.4f, 0.25f, 0.15f), C(0.15f, 0.12f, 0.1f)),
            B("arabian", "Arabian", 1f, 0.9f, 0.9f, 1f, 600, 0.45f, 0.7f, 0.6f, C(0.78f, 0.78f, 0.78f), C(0.55f, 0.32f, 0.18f)),
            B("pony", "Pony", 1.05f, 0.75f, 0.7f, 1.2f, 250, 0.6f, 0.5f, 0.1f, C(0.6f, 0.45f, 0.3f), C(0.9f, 0.88f, 0.82f)),
        };
        s.care = HorseCare();
        s.susceptibility = Prone(("digestive", 1.2f), ("injury", 1.1f));
        s.harvestAnatomy = "horse";
        s.dataVersion = 2;
        return s;
    }

    // ---------------------------------------------------------------- builders
    private static NutrientProfile N(float e, float p, float f, float m, float v) => new NutrientProfile(e, p, f, m, v);

    private static Color C(float r, float g, float b) => new Color(r, g, b);

    private static AnimalFoodData F(string id, string name, string description, int perItem, float satiety, float hydration, NutrientProfile n, float freshHours, params string[] tags)
    {
        var f = new AnimalFoodData
        {
            id = id, name = name, description = description, portionsPerItem = perItem, satiety = satiety, hydration = hydration, nutrients = n,
            freshHours = freshHours, tags = new List<string>(tags ?? new string[0]),
        };
        f.items.Add(AnimalIds.Feed(id));
        return f;
    }

    private static AnimalFoodData Bad(this AnimalFoodData f, float toxin, float irritation)
    {
        f.toxin = toxin;
        f.irritation = irritation;
        return f;
    }

    /// <summary>The food is these items only (farm produce, harvested pieces), not a feed item of its own.</summary>
    private static AnimalFoodData Items(this AnimalFoodData f, params string[] items)
    {
        f.items.Clear();
        f.items.AddRange(items);
        return f;
    }

    private static AnimalSpeciesData S(string id, string name, string plural, string description, AnimalHabitatKind kinds, float area, int value)
    {
        var s = new AnimalSpeciesData { id = id, name = name, plural = plural, description = description };
        s.habitat = new AnimalHabitatNeeds { kinds = kinds, areaPerAnimal = area };
        return s;
    }

    private static AnimalStageSpec St(GrowthStage stage, float hours, float appetite, float size, bool production = false, bool breeding = false, float protein = 1f)
    {
        var st = new AnimalStageSpec(stage, hours, appetite, size, production, breeding);
        st.needScale.protein = protein;
        return st;
    }

    private static void Needs(AnimalSpeciesData s, float portionScale, float hunger, float thirst, float litresPerPoint, NutrientProfile need, NutrientProfile importance)
    {
        s.needs = new AnimalNeedsSpec
        {
            portionScale = portionScale, hungerPerHour = hunger, thirstPerHour = thirst, litresPerPoint = litresPerPoint, needPerHour = need, importance = importance,
        };
    }

    private static void Look(AnimalSpeciesData s, AnimalLookSpec.Shape shape, float length, float height, MobLandGait gait, float walk)
    {
        s.look = new AnimalLookSpec { shape = shape, length = length, height = height, gait = gait, walkSpeed = walk };
        if (s.breeds != null && s.breeds.Count > 0 && s.breeds[0].coats.Count > 0) s.look.coat = s.breeds[0].coats[0];
    }

    private static AnimalBreedData B(string id, string name, float growth, float appetite, float size, float hardiness, int value, float temperament, float quality, float rarity,
        params Color[] coats)
    {
        var b = new AnimalBreedData(id, name, growth, appetite, size, hardiness, value, coats) { temperament = temperament, productionQuality = quality, rarity = rarity };
        return b;
    }
}

/// <summary>
/// An in-memory catalog of the game's content (the built-in content, or tests' own): species and foods, and (Phase 2) the
/// illnesses, the medicines and the care tools. What the rules read. The runtime's <c>AnimalDatabase</c> compiles one from
/// its assets and the built-in content. Items are found by key like foods: the exact key first, then the longest prefix
/// (a key ending in *).
/// </summary>
public sealed class AnimalCatalog : IAnimalCatalog
{
    private readonly List<AnimalSpeciesData> species = new List<AnimalSpeciesData>();
    private readonly List<AnimalFoodData> foods = new List<AnimalFoodData>();
    private readonly Dictionary<string, AnimalSpeciesData> speciesById = new Dictionary<string, AnimalSpeciesData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimalFoodData> foodById = new Dictionary<string, AnimalFoodData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimalFoodData> foodByItem = new Dictionary<string, AnimalFoodData>(StringComparer.Ordinal);
    private readonly List<(string prefix, AnimalFoodData food)> foodByPrefix = new List<(string, AnimalFoodData)>();

    private readonly List<AnimalConditionData> conditions = new List<AnimalConditionData>();
    private readonly Dictionary<string, AnimalConditionData> conditionById = new Dictionary<string, AnimalConditionData>(StringComparer.OrdinalIgnoreCase);
    private readonly List<AnimalTreatmentData> treatments = new List<AnimalTreatmentData>();
    private readonly Dictionary<string, AnimalTreatmentData> treatmentById = new Dictionary<string, AnimalTreatmentData>(StringComparer.OrdinalIgnoreCase);
    private readonly ItemKeys<AnimalTreatmentData> treatmentByItem = new ItemKeys<AnimalTreatmentData>();
    private readonly List<AnimalToolData> tools = new List<AnimalToolData>();
    private readonly Dictionary<string, AnimalToolData> toolById = new Dictionary<string, AnimalToolData>(StringComparer.OrdinalIgnoreCase);
    private readonly ItemKeys<AnimalToolData> toolByItem = new ItemKeys<AnimalToolData>();

    /// <summary>Species and foods only (no illnesses, medicines or tools: a test that builds its own catalog gets none).</summary>
    public AnimalCatalog(AnimalRules rules, IEnumerable<AnimalSpeciesData> species, IEnumerable<AnimalFoodData> foods)
    {
        Rules = rules ?? AnimalRules.Defaults();
        if (species != null) foreach (AnimalSpeciesData s in species) AddSpecies(s);
        if (foods != null) foreach (AnimalFoodData f in foods) AddFood(f);
    }

    /// <summary>Species, foods, illnesses, medicines and care tools.</summary>
    public AnimalCatalog(AnimalRules rules, IEnumerable<AnimalSpeciesData> species, IEnumerable<AnimalFoodData> foods, IEnumerable<AnimalConditionData> conditions,
        IEnumerable<AnimalTreatmentData> treatments, IEnumerable<AnimalToolData> tools) : this(rules, species, foods)
    {
        if (conditions != null) foreach (AnimalConditionData c in conditions) AddCondition(c);
        if (treatments != null) foreach (AnimalTreatmentData t in treatments) AddTreatment(t);
        if (tools != null) foreach (AnimalToolData t in tools) AddTool(t);
    }

    /// <summary>The built-in content: the eight species, their foods, the nine illnesses, the medicines and the care tools.</summary>
    public static AnimalCatalog BuiltIn(AnimalRules rules = null) =>
        new AnimalCatalog(rules, AnimalDefaults.Species(), AnimalDefaults.Foods(), AnimalDefaults.Conditions(), AnimalDefaults.Treatments(), AnimalDefaults.Tools());

    public AnimalRules Rules { get; }
    public IReadOnlyList<AnimalSpeciesData> AllSpecies => species;
    public IReadOnlyList<AnimalFoodData> AllFoods => foods;
    public IReadOnlyList<AnimalConditionData> AllConditions => conditions;
    public IReadOnlyList<AnimalTreatmentData> AllTreatments => treatments;
    public IReadOnlyList<AnimalToolData> AllTools => tools;

    /// <summary>Adds a species (one with the id of a species already there replaces it).</summary>
    public void AddSpecies(AnimalSpeciesData s)
    {
        if (s == null || string.IsNullOrEmpty(s.id)) return;
        if (speciesById.TryGetValue(s.id, out AnimalSpeciesData had)) species.Remove(had);
        speciesById[s.id] = s;
        species.Add(s);
    }

    /// <summary>Adds a food (one with the id of a food already there replaces it, and takes over its items).</summary>
    public void AddFood(AnimalFoodData f)
    {
        if (f == null || string.IsNullOrEmpty(f.id)) return;
        if (foodById.TryGetValue(f.id, out AnimalFoodData had))
        {
            foods.Remove(had);
            if (had.items != null)
                foreach (string i in had.items)
                    if (i != null && foodByItem.TryGetValue(i, out AnimalFoodData x) && x == had) foodByItem.Remove(i);
            foodByPrefix.RemoveAll(p => p.food == had);
        }
        foodById[f.id] = f;
        foods.Add(f);
        if (f.items == null) return;
        foreach (string i in f.items)
        {
            if (string.IsNullOrEmpty(i)) continue;
            if (i.EndsWith("*", StringComparison.Ordinal)) foodByPrefix.Add((i.Substring(0, i.Length - 1), f));
            else foodByItem[i] = f;
        }
    }

    /// <summary>Adds an illness (one with the id of an illness already there replaces it, in its place in the order).</summary>
    public void AddCondition(AnimalConditionData c)
    {
        if (c == null || string.IsNullOrEmpty(c.id)) return;
        if (conditionById.TryGetValue(c.id, out AnimalConditionData had))
        {
            int at = conditions.IndexOf(had);
            if (at >= 0) conditions[at] = c;
            else conditions.Add(c);
        }
        else conditions.Add(c);
        conditionById[c.id] = c;
    }

    /// <summary>Adds a medicine (one with the id of a medicine already there replaces it, in its place, and takes over its items).</summary>
    public void AddTreatment(AnimalTreatmentData t)
    {
        if (t == null || string.IsNullOrEmpty(t.id)) return;
        if (treatmentById.TryGetValue(t.id, out AnimalTreatmentData had))
        {
            treatmentByItem.Remove(had);
            int at = treatments.IndexOf(had);
            if (at >= 0) treatments[at] = t;
            else treatments.Add(t);
        }
        else treatments.Add(t);
        treatmentById[t.id] = t;
        treatmentByItem.Add(t.items, t);
    }

    /// <summary>Adds a care tool or supply (one with the id of a tool already there replaces it, in its place, and takes over its items).</summary>
    public void AddTool(AnimalToolData t)
    {
        if (t == null || string.IsNullOrEmpty(t.id)) return;
        if (toolById.TryGetValue(t.id, out AnimalToolData had))
        {
            toolByItem.Remove(had);
            int at = tools.IndexOf(had);
            if (at >= 0) tools[at] = t;
            else tools.Add(t);
        }
        else tools.Add(t);
        toolById[t.id] = t;
        toolByItem.Add(t.items, t);
    }

    public AnimalSpeciesData Species(string id) => !string.IsNullOrEmpty(id) && speciesById.TryGetValue(id, out AnimalSpeciesData s) ? s : null;
    public AnimalFoodData Food(string id) => !string.IsNullOrEmpty(id) && foodById.TryGetValue(id, out AnimalFoodData f) ? f : null;
    public AnimalConditionData Condition(string id) => !string.IsNullOrEmpty(id) && conditionById.TryGetValue(id, out AnimalConditionData c) ? c : null;
    public AnimalTreatmentData Treatment(string id) => !string.IsNullOrEmpty(id) && treatmentById.TryGetValue(id, out AnimalTreatmentData t) ? t : null;
    public AnimalToolData Tool(string id) => !string.IsNullOrEmpty(id) && toolById.TryGetValue(id, out AnimalToolData t) ? t : null;

    /// <summary>The medicine an item is (by its key: the exact key, then the longest prefix), or null.</summary>
    public AnimalTreatmentData TreatmentOfItem(string itemKey) => treatmentByItem.Find(itemKey);

    /// <summary>The care tool or supply an item is (by its key: the exact key, then the longest prefix), or null.</summary>
    public AnimalToolData ToolOfItem(string itemKey) => toolByItem.Find(itemKey);

    public AnimalFoodData FoodOfItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (foodByItem.TryGetValue(itemId, out AnimalFoodData f)) return f;
        // The longest prefix wins ("harvest:boar/*" over "harvest:*").
        AnimalFoodData best = null;
        int bestLength = -1;
        foreach ((string prefix, AnimalFoodData food) in foodByPrefix)
            if (prefix.Length > bestLength && itemId.StartsWith(prefix, StringComparison.Ordinal))
            {
                best = food;
                bestLength = prefix.Length;
            }
        return best;
    }

    /// <summary>Item keys to content: exact keys, and prefixes (keys ending in *) of which the longest that fits wins.</summary>
    private sealed class ItemKeys<T> where T : class
    {
        private readonly Dictionary<string, T> exact = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, T>> prefixes = new List<KeyValuePair<string, T>>();
        private readonly List<string> scratch = new List<string>();

        public void Add(List<string> keys, T value)
        {
            if (keys == null || value == null) return;
            foreach (string k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                if (k.EndsWith("*", StringComparison.Ordinal)) prefixes.Add(new KeyValuePair<string, T>(k.Substring(0, k.Length - 1), value));
                else exact[k] = value;
            }
        }

        public void Remove(T value)
        {
            if (value == null) return;
            scratch.Clear();
            foreach (KeyValuePair<string, T> e in exact)
                if (e.Value == value) scratch.Add(e.Key);
            foreach (string k in scratch) exact.Remove(k);
            scratch.Clear();
            prefixes.RemoveAll(p => p.Value == value);
        }

        public T Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (exact.TryGetValue(key, out T v)) return v;
            T best = null;
            int bestLength = -1;
            foreach (KeyValuePair<string, T> p in prefixes)
                if (p.Key.Length > bestLength && key.StartsWith(p.Key, StringComparison.Ordinal))
                {
                    best = p.Value;
                    bestLength = p.Key.Length;
                }
            return best;
        }
    }
}
