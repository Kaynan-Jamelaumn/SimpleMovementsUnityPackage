using System.Collections.Generic;
using UnityEngine;

/// <summary>A farming tool as content (the item is made from it).</summary>
[System.Serializable]
public class FarmToolPreset
{
    public string id = "";
    public string name = "";
    public FarmToolKind kind;
    [Range(0f, 1f)] public float quality = 0.6f;
    [Min(0.1f)] public float speed = 1f;
    [Min(1)] public int durability = 120;
    public float value = 8f, weight = 1f;
    [TextArea(1, 3)] public string description = "";
}

/// <summary>A measuring instrument as content.</summary>
[System.Serializable]
public class InstrumentPreset
{
    public string id = "";
    public string name = "";
    public InstrumentKind kind;
    [Tooltip("0 a crude instrument (rough classes, can be wrong) - 1 a precise one.")]
    [Range(0f, 1f)] public float quality = 0.5f;
    public float value = 15f, weight = 0.4f;
    [TextArea(1, 3)] public string description = "";
}

/// <summary>A water container as content.</summary>
[System.Serializable]
public class ContainerPreset
{
    public string id = "";
    public string name = "";
    [Tooltip("Litres it holds.")]
    [Min(0.5f)] public float capacity = 5f;
    [Tooltip("How wide the water spreads when poured, metres.")]
    [Min(0.05f)] public float spread = 0.4f;
    [Tooltip("Litres poured per second.")]
    [Min(0.1f)] public float flow = 1f;
    [Tooltip("It wets the leaves of the plants it is poured over.")]
    public bool wetsLeaves = true;
    public float value = 6f, weight = 1f;
    [TextArea(1, 3)] public string description = "";
}

/// <summary>
/// The built-in content: nineteen plant species of every category (a hybrid among them, bred from two others), the soil
/// materials, fertilizers and treatments, the instruments, the tools and the water containers. An Agriculture
/// Database uses them for every id its own assets do not give.
/// </summary>
public static class AgriDefaults
{
    // ---------------------------------------------------------------- species
    public static List<PlantSpeciesData> Species()
    {
        var list = new List<PlantSpeciesData>
        {
            Wheat(), Carrot(), Cabbage(), Bean(), Potato(), Tomato(), Strawberry(), Apple(), Grape(), Chamomile(), Comfrey(),
            Lavender(), Marigold(), Mandrake(), Nightshade(), Moonpetal(), Emberroot(), Frostbloom(), TwilightBloom(),
        };
        return list;
    }

    public static List<HybridRule> Hybrids() => new List<HybridRule>
    {
        new HybridRule { parentA = "moonpetal", parentB = "frostbloom", child = "twilight_bloom", chance = 0.15f },
    };

    private static float[] A(params float[] v) => v;
    private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    private static ProduceSpec P(ProducePart part, string name, PlantUses uses, int min, int max, float[] days, Pollination pollination,
        int seeds, float value, float weight, float nutrition, float potency, Color young, Color ripe, Color over, Color rotten, float size,
        float flushDays = 0f, int flushes = 1, bool whole = false, bool regrows = false, string description = "", string alchemy = "") => new ProduceSpec
        {
            alchemyIngredient = alchemy,
            part = part, name = name, uses = uses, countMin = min, countMax = max, stageDays = days, pollination = pollination, seedsPerPiece = seeds,
            value = value, weight = weight, nutrition = nutrition, potency = potency, colorYoung = young, colorRipe = ripe, colorOverripe = over,
            colorRotten = rotten, pieceSize = size, flushIntervalDays = flushDays, maxFlushes = flushes, wholePlant = whole, regrows = regrows,
            description = description,
        };

    private static PlantLook L(PlantShape shape, float height, float width, Color leaf, Color flower, Color? stem = null, Color? glow = null) => new PlantLook
    {
        shape = shape, height = height, width = width, leaf = leaf, flower = flower, stem = stem ?? new Color(leaf.r * 1.2f, leaf.g * 0.85f, leaf.b * 0.8f),
        glow = glow ?? new Color(0f, 0f, 0f, 0f),
    };

    private static PlantSpeciesData Wheat() => new PlantSpeciesData
    {
        id = "wheat", name = "Wheat", category = PlantCategory.Common, lifeCycle = PlantLifeCycle.Annual,
        handbook = "The bread grain. Sown in autumn it stands through the winter and ripens early; sown in spring it ripens late. " +
                   "Cut it when the heads are golden and the grain hard under the thumbnail - left too long, the heads shatter.",
        germinationDays = 1.5f, seedlingDays = 2f, juvenileDays = 3f, vegetativeDays = 4f, floweringDays = 1.5f, senescenceDays = 3f,
        soilAffinity = A(0.6f, 0.85f, 0.35f, 1f, 0.4f, 0.8f, 0.2f, 0.7f),
        phMin = 5.5f, phLow = 6.2f, phHigh = 7.5f, phMax = 8.5f, nutrientDemand = 1f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 1.2f) },
        salinityTolerance = 4f, compactionTolerance = 0.55f, minTilth = 0.35f,
        waterLow = 0.3f, waterHigh = 0.9f, droughtTolerance = 0.45f, waterlogTolerance = 0.2f, waterUse = 0.9f,
        tempMin = -6f, tempLow = 12f, tempHigh = 24f, tempMax = 33f, germinationMin = 4f, germinationMax = 30f, frostSensitivity = 0.2f, shockSensitivity = 0.2f,
        humidityLow = 0.3f, humidityHigh = 0.75f, lightLow = 0.6f, lightHigh = 1f, photoperiod = PhotoperiodType.LongDay, criticalDayLength = 12.5f,
        sowingSeasons = SeasonMask.Autumn | SeasonMask.Spring, floweringSeasons = SeasonMask.Spring | SeasonMask.Summer,
        depthCm = 3f, depthToleranceCm = 1.5f, spacing = 0.15f, rowSpacing = 0.2f, rootRadius = 0.12f,
        produce = P(ProducePart.Grain, "Wheat", PlantUses.Food, 2, 5, A(1f, 0.5f, 1.5f, 1.5f, 1.5f, 1.5f, 3f, 2f, 2f), Pollination.Wind, 12, 1.2f, 0.1f, 3f, 0f,
            C(0.45f, 0.62f, 0.3f), C(0.86f, 0.72f, 0.35f), C(0.62f, 0.52f, 0.3f), C(0.35f, 0.33f, 0.28f), 0.05f, whole: true,
            description: "Heads of wheat. Threshed, the grain is flour; kept, it is next year's seed.", alchemy: "Wild Barley"),
        pestSusceptibility = A(1.1f, 0.4f, 0.5f, 0.8f, 0.7f, 0.6f, 0.5f, 0.3f), diseaseSusceptibility = A(1.2f, 0.4f, 0.2f, 1f, 0.3f, 0.5f),
        look = L(PlantShape.Grain, 0.9f, 0.18f, C(0.42f, 0.6f, 0.25f), C(0.8f, 0.75f, 0.5f)), seedValue = 0.2f, seedStack = 100,
    };

    private static PlantSpeciesData Carrot() => new PlantSpeciesData
    {
        id = "carrot", name = "Carrot", category = PlantCategory.Common, lifeCycle = PlantLifeCycle.SingleHarvest,
        handbook = "A root for light, deep, stone-free soil. Sow thinly and shallowly; it is slow to come up. In heavy or stony ground the roots fork and twist.",
        germinationDays = 3f, seedlingDays = 2f, juvenileDays = 3f, vegetativeDays = 4f, senescenceDays = 2f,
        soilAffinity = A(1f, 0.45f, 0.3f, 0.9f, 0.7f, 0.8f, 0.2f, 0.7f),
        phMin = 5.5f, phLow = 6f, phHigh = 7f, phMax = 8f, nutrientDemand = 0.8f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 0.8f), new NutrientNeed(Nutrient.Potassium, 1.3f), new NutrientNeed(Nutrient.Boron, 1.3f) },
        salinityTolerance = 1f, compactionTolerance = 0.3f, minTilth = 0.55f,
        waterLow = 0.35f, waterHigh = 0.9f, droughtTolerance = 0.3f, waterlogTolerance = 0.15f, waterUse = 0.8f,
        tempMin = -3f, tempLow = 12f, tempHigh = 22f, tempMax = 30f, germinationMin = 7f, germinationMax = 30f, frostSensitivity = 0.3f, shockSensitivity = 0.2f,
        humidityLow = 0.3f, humidityHigh = 0.8f, lightLow = 0.5f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer | SeasonMask.Autumn,
        depthCm = 1f, depthToleranceCm = 0.8f, spacing = 0.08f, rowSpacing = 0.25f, rootRadius = 0.07f,
        produce = P(ProducePart.Root, "Carrot", PlantUses.Food, 1, 1, A(1f, 1f, 1.5f, 1.5f, 2f, 2f, 3f, 3f, 2f), Pollination.None, 8, 1f, 0.1f, 6f, 0f,
            C(0.9f, 0.55f, 0.2f), C(0.95f, 0.45f, 0.1f), C(0.75f, 0.42f, 0.18f), C(0.3f, 0.22f, 0.12f), 0.04f, description: "A sweet orange root."),
        pestSusceptibility = A(0.6f, 0.3f, 0.3f, 0.4f, 0.4f, 1.4f, 0.6f, 0.4f), diseaseSusceptibility = A(0.6f, 1f, 0.3f, 0.6f, 0.5f, 0.3f),
        look = L(PlantShape.RootCrop, 0.3f, 0.2f, C(0.25f, 0.55f, 0.2f), C(1f, 1f, 0.95f)), seedValue = 0.15f, seedStack = 100,
    };

    private static PlantSpeciesData Cabbage() => new PlantSpeciesData
    {
        id = "cabbage", name = "Cabbage", category = PlantCategory.Common, lifeCycle = PlantLifeCycle.SingleHarvest,
        handbook = "A hungry leaf crop for rich, firm, limey ground. Caterpillars, slugs and snails love it as much as cooks do. It stands some frost.",
        germinationDays = 1.5f, seedlingDays = 2f, juvenileDays = 3f, vegetativeDays = 4f, senescenceDays = 3f,
        soilAffinity = A(0.5f, 0.9f, 0.3f, 1f, 0.5f, 0.8f, 0.3f, 0.7f),
        phMin = 6f, phLow = 6.5f, phHigh = 7.5f, phMax = 8.2f, nutrientDemand = 1.5f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 1.3f), new NutrientNeed(Nutrient.Calcium, 1.2f), new NutrientNeed(Nutrient.Boron, 1.2f) },
        salinityTolerance = 2.5f, compactionTolerance = 0.6f, minTilth = 0.3f,
        waterLow = 0.45f, waterHigh = 0.95f, droughtTolerance = 0.2f, waterlogTolerance = 0.25f, waterUse = 1.2f,
        tempMin = -5f, tempLow = 10f, tempHigh = 20f, tempMax = 28f, germinationMin = 5f, germinationMax = 30f, frostSensitivity = 0.15f, shockSensitivity = 0.2f,
        humidityLow = 0.4f, humidityHigh = 0.85f, lightLow = 0.55f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer | SeasonMask.Autumn,
        depthCm = 1.2f, depthToleranceCm = 0.8f, spacing = 0.45f, rowSpacing = 0.5f, rootRadius = 0.25f,
        produce = P(ProducePart.Leaf, "Cabbage", PlantUses.Food, 1, 1, A(1f, 1f, 1.5f, 1.5f, 2f, 1.5f, 3f, 2f, 2f), Pollination.None, 10, 2.5f, 1f, 12f, 0f,
            C(0.4f, 0.65f, 0.35f), C(0.55f, 0.75f, 0.45f), C(0.65f, 0.7f, 0.4f), C(0.35f, 0.3f, 0.15f), 0.22f, whole: true, description: "A firm head of leaves."),
        pestSusceptibility = A(1.2f, 1.8f, 0.4f, 1f, 0.6f, 0.8f, 1.5f, 1.3f), diseaseSusceptibility = A(0.8f, 0.7f, 0.4f, 0.8f, 0.9f, 0.5f),
        look = L(PlantShape.Rosette, 0.35f, 0.5f, C(0.4f, 0.62f, 0.38f), C(1f, 0.95f, 0.5f)), seedValue = 0.2f,
    };

    private static PlantSpeciesData Bean() => new PlantSpeciesData
    {
        id = "bean", name = "Climbing Bean", category = PlantCategory.Common, lifeCycle = PlantLifeCycle.Annual,
        handbook = "A climbing bean. It feeds the soil it grows in, needs warm soil to sprout and a pole to climb. Pick the pods young and often.",
        germinationDays = 1.5f, seedlingDays = 1.5f, juvenileDays = 2f, vegetativeDays = 2.5f, floweringDays = 2f, senescenceDays = 2f,
        soilAffinity = A(0.75f, 0.65f, 0.4f, 1f, 0.4f, 0.8f, 0.2f, 0.7f),
        phMin = 5.8f, phLow = 6.2f, phHigh = 7.2f, phMax = 8f, nutrientDemand = 0.7f, nitrogenFixer = true,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Phosphorus, 1.2f), new NutrientNeed(Nutrient.Molybdenum, 2f) },
        salinityTolerance = 1f, compactionTolerance = 0.5f, minTilth = 0.3f,
        waterLow = 0.4f, waterHigh = 0.9f, droughtTolerance = 0.25f, waterlogTolerance = 0.15f, waterUse = 1f,
        tempMin = 2f, tempLow = 16f, tempHigh = 27f, tempMax = 33f, germinationMin = 12f, germinationMax = 32f, frostSensitivity = 1f, shockSensitivity = 0.4f,
        humidityLow = 0.4f, humidityHigh = 0.8f, lightLow = 0.65f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer,
        depthCm = 4f, depthToleranceCm = 1.5f, spacing = 0.15f, rowSpacing = 0.45f, rootRadius = 0.15f, needsSupport = true,
        produce = P(ProducePart.Fruit, "Bean Pods", PlantUses.Food, 4, 12, A(1f, 0.5f, 0.8f, 1f, 1f, 1f, 1.5f, 3f, 2f), Pollination.Self, 5, 0.6f, 0.05f, 3f, 0f,
            C(0.45f, 0.7f, 0.3f), C(0.35f, 0.6f, 0.25f), C(0.7f, 0.65f, 0.4f), C(0.3f, 0.25f, 0.15f), 0.08f, 3f, 3, description: "Green pods of beans."),
        pestSusceptibility = A(1.4f, 0.5f, 1f, 0.8f, 0.8f, 0.6f, 1.2f, 1f), diseaseSusceptibility = A(1f, 0.8f, 0.5f, 1f, 0.8f, 0.9f),
        look = L(PlantShape.Vine, 1.6f, 0.35f, C(0.3f, 0.58f, 0.25f), C(0.95f, 0.95f, 0.9f)), crossGroup = "bean", seedValue = 0.25f,
    };

    private static PlantSpeciesData Potato() => new PlantSpeciesData
    {
        id = "potato", name = "Potato", category = PlantCategory.Common, lifeCycle = PlantLifeCycle.SingleHarvest,
        handbook = "Grown from a piece of last year's tubers, buried deep in loose, rather sour soil. Blight comes with warm, wet weather.",
        germinationDays = 2f, seedlingDays = 2f, juvenileDays = 3f, vegetativeDays = 4f, senescenceDays = 2f,
        soilAffinity = A(0.9f, 0.5f, 0.3f, 1f, 0.8f, 0.9f, 0.2f, 0.7f),
        phMin = 4.8f, phLow = 5.2f, phHigh = 6.5f, phMax = 7.5f, nutrientDemand = 1.2f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Potassium, 1.4f) },
        salinityTolerance = 1.7f, compactionTolerance = 0.4f, minTilth = 0.5f,
        waterLow = 0.4f, waterHigh = 0.9f, droughtTolerance = 0.25f, waterlogTolerance = 0.15f, waterUse = 1.1f,
        tempMin = -1f, tempLow = 14f, tempHigh = 22f, tempMax = 30f, germinationMin = 7f, germinationMax = 28f, frostSensitivity = 0.9f, shockSensitivity = 0.3f,
        humidityLow = 0.4f, humidityHigh = 0.85f, lightLow = 0.6f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer,
        depthCm = 10f, depthToleranceCm = 3f, spacing = 0.35f, rowSpacing = 0.6f, rootRadius = 0.25f,
        produce = P(ProducePart.Root, "Potato", PlantUses.Food, 3, 9, A(1f, 1.5f, 1.5f, 1.5f, 2f, 2f, 4f, 3f, 2f), Pollination.None, 2, 0.8f, 0.2f, 8f, 0f,
            C(0.85f, 0.75f, 0.55f), C(0.8f, 0.68f, 0.45f), C(0.65f, 0.6f, 0.4f), C(0.3f, 0.25f, 0.15f), 0.07f, description: "Tubers dug from the soil."),
        pestSusceptibility = A(0.8f, 0.3f, 0.4f, 1.8f, 0.6f, 1.2f, 1f, 0.5f), diseaseSusceptibility = A(1.5f, 0.9f, 0.7f, 0.9f, 0.8f, 1f),
        look = L(PlantShape.Bush, 0.6f, 0.5f, C(0.3f, 0.52f, 0.24f), C(0.95f, 0.9f, 0.95f)), seedValue = 0.4f, seedStack = 30,
    };

    private static PlantSpeciesData Tomato() => new PlantSpeciesData
    {
        id = "tomato", name = "Tomato", category = PlantCategory.FruitBearing, lifeCycle = PlantLifeCycle.Annual,
        handbook = "A tender fruit of hot summers: sow it warm, stake it, water it evenly. Fruits that blacken at the base were watered unevenly - or so the old gardeners say.",
        germinationDays = 1.5f, seedlingDays = 1.5f, juvenileDays = 2f, vegetativeDays = 2.5f, floweringDays = 2f, senescenceDays = 3f,
        soilAffinity = A(0.7f, 0.6f, 0.4f, 1f, 0.5f, 0.9f, 0.2f, 0.8f),
        phMin = 5.5f, phLow = 6.2f, phHigh = 6.8f, phMax = 7.8f, nutrientDemand = 1.4f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Potassium, 1.4f), new NutrientNeed(Nutrient.Calcium, 1.5f), new NutrientNeed(Nutrient.Magnesium, 1.2f) },
        salinityTolerance = 2.5f, compactionTolerance = 0.5f, minTilth = 0.35f,
        waterLow = 0.5f, waterHigh = 0.95f, droughtTolerance = 0.2f, waterlogTolerance = 0.2f, waterUse = 1.3f,
        tempMin = 3f, tempLow = 20f, tempHigh = 28f, tempMax = 35f, germinationMin = 15f, germinationMax = 32f, frostSensitivity = 1f, shockSensitivity = 0.5f,
        humidityLow = 0.45f, humidityHigh = 0.75f, lightLow = 0.75f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer,
        depthCm = 0.8f, depthToleranceCm = 0.5f, spacing = 0.5f, rowSpacing = 0.7f, rootRadius = 0.3f, needsSupport = true,
        produce = P(ProducePart.Fruit, "Tomato", PlantUses.Food, 3, 10, A(1.2f, 0.4f, 0.8f, 1.2f, 1.5f, 0.8f, 1.5f, 1.5f, 1f), Pollination.Self, 25, 1.5f, 0.15f, 5f, 0f,
            C(0.35f, 0.6f, 0.25f), C(0.85f, 0.12f, 0.08f), C(0.5f, 0.06f, 0.04f), C(0.3f, 0.22f, 0.12f), 0.07f, 2.5f, 4, description: "A juicy red fruit."),
        pestSusceptibility = A(1.2f, 1.2f, 1.2f, 0.6f, 1.3f, 0.9f, 0.8f, 0.6f), diseaseSusceptibility = A(1.4f, 0.9f, 1.3f, 1.2f, 1f, 1f),
        look = L(PlantShape.Vine, 1.4f, 0.55f, C(0.28f, 0.52f, 0.22f), C(1f, 0.9f, 0.2f)), crossGroup = "tomato", seedValue = 0.3f,
    };

    private static PlantSpeciesData Strawberry() => new PlantSpeciesData
    {
        id = "strawberry", name = "Strawberry", category = PlantCategory.FruitBearing, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A low perennial. Its berries rot in damp weather and slugs find them first; straw under the plants keeps both away.",
        germinationDays = 3f, seedlingDays = 3f, juvenileDays = 4f, vegetativeDays = 4f, floweringDays = 2.5f, senescenceDays = 4f,
        yearsToFullSize = 0.5f, lifespanYears = 4f,
        soilAffinity = A(0.8f, 0.5f, 0.3f, 1f, 0.8f, 0.8f, 0.2f, 0.8f),
        phMin = 5f, phLow = 5.5f, phHigh = 6.5f, phMax = 7.5f, nutrientDemand = 1f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Potassium, 1.2f) },
        salinityTolerance = 1f, compactionTolerance = 0.4f, minTilth = 0.35f,
        waterLow = 0.45f, waterHigh = 0.9f, droughtTolerance = 0.25f, waterlogTolerance = 0.2f, waterUse = 1f,
        tempMin = -14f, tempLow = 15f, tempHigh = 25f, tempMax = 32f, germinationMin = 10f, germinationMax = 28f, frostSensitivity = 0.2f, shockSensitivity = 0.3f,
        humidityLow = 0.4f, humidityHigh = 0.75f, lightLow = 0.65f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer, floweringSeasons = SeasonMask.Spring | SeasonMask.Summer, dormancySeasons = SeasonMask.Winter, chillHours = 40f,
        depthCm = 0.2f, depthToleranceCm = 0.3f, surfaceSown = true, spacing = 0.3f, rowSpacing = 0.45f, rootRadius = 0.15f,
        produce = P(ProducePart.Fruit, "Strawberry", PlantUses.Food, 4, 14, A(1.5f, 0.5f, 1f, 1f, 1.5f, 1f, 1.2f, 1f, 1f), Pollination.Insect, 30, 1.2f, 0.03f, 2f, 0f,
            C(0.75f, 0.85f, 0.55f), C(0.9f, 0.1f, 0.15f), C(0.55f, 0.05f, 0.08f), C(0.3f, 0.25f, 0.18f), 0.035f, 3f, 3, description: "A sweet red berry."),
        pestSusceptibility = A(1f, 0.4f, 1.3f, 0.7f, 0.7f, 0.8f, 1.8f, 1.6f), diseaseSusceptibility = A(1.4f, 1f, 1f, 1.1f, 0.6f, 0.8f),
        look = L(PlantShape.Herb, 0.2f, 0.35f, C(0.25f, 0.55f, 0.25f), C(1f, 1f, 1f)), crossGroup = "strawberry", seedValue = 0.4f,
    };

    private static PlantSpeciesData Apple() => new PlantSpeciesData
    {
        id = "apple", name = "Apple Tree", category = PlantCategory.FruitBearing, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A tree of temperate lands. It rests in winter and needs a proper cold one to blossom well. Pruned in winter it fruits better; left alone it grows a tangle of wood.",
        germinationDays = 4f, seedlingDays = 4f, juvenileDays = 6f, vegetativeDays = 8f, floweringDays = 3f, senescenceDays = 10f,
        yearsToFruit = 1, yearsToFullSize = 3f,
        soilAffinity = A(0.6f, 0.8f, 0.5f, 1f, 0.4f, 0.8f, 0.1f, 0.8f),
        phMin = 5.5f, phLow = 6f, phHigh = 7f, phMax = 8f, nutrientDemand = 1f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Calcium, 1.3f), new NutrientNeed(Nutrient.Boron, 1.2f) },
        salinityTolerance = 1.5f, compactionTolerance = 0.5f, minTilth = 0.2f,
        waterLow = 0.35f, waterHigh = 0.9f, droughtTolerance = 0.4f, waterlogTolerance = 0.15f, waterUse = 1f,
        tempMin = -26f, tempLow = 15f, tempHigh = 24f, tempMax = 35f, germinationMin = 4f, germinationMax = 24f, frostSensitivity = 0.05f, shockSensitivity = 0.2f,
        humidityLow = 0.4f, humidityHigh = 0.75f, lightLow = 0.7f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring, floweringSeasons = SeasonMask.Spring, dormancySeasons = SeasonMask.Winter, chillHours = 120f,
        depthCm = 2f, depthToleranceCm = 1.5f, spacing = 3f, rowSpacing = 4f, rootRadius = 1.5f,
        produce = P(ProducePart.Fruit, "Apple", PlantUses.Food, 6, 30, A(2f, 1f, 2f, 3f, 4f, 3f, 3f, 3f, 2f), Pollination.Insect, 6, 1.2f, 0.2f, 6f, 0f,
            C(0.45f, 0.7f, 0.3f), C(0.8f, 0.15f, 0.1f), C(0.6f, 0.2f, 0.1f), C(0.35f, 0.25f, 0.12f), 0.08f, description: "A crisp apple."),
        pestSusceptibility = A(1.2f, 1.3f, 1f, 0.6f, 0.8f, 0.5f, 0.1f, 0.1f), diseaseSusceptibility = A(1.3f, 0.8f, 0.4f, 1.2f, 0.9f, 0.5f),
        pruning = PruneNeed.Regular, pruneSeasons = SeasonMask.Winter,
        look = L(PlantShape.Tree, 3.5f, 3f, C(0.25f, 0.5f, 0.2f), C(1f, 0.9f, 0.95f), C(0.4f, 0.3f, 0.2f)), crossGroup = "apple", seedValue = 1f, seedStack = 20,
    };

    private static PlantSpeciesData Grape() => new PlantSpeciesData
    {
        id = "grape", name = "Grapevine", category = PlantCategory.FruitBearing, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A climbing vine that loves stony, poor, sunny slopes and hates wet feet. Cut it back hard every winter. Mildew follows damp, still air.",
        germinationDays = 4f, seedlingDays = 4f, juvenileDays = 5f, vegetativeDays = 6f, floweringDays = 2f, senescenceDays = 8f,
        yearsToFruit = 1, yearsToFullSize = 2f,
        soilAffinity = A(0.8f, 0.5f, 1f, 0.8f, 0.3f, 1f, 0.1f, 0.8f),
        phMin = 5.5f, phLow = 6.5f, phHigh = 7.8f, phMax = 8.5f, nutrientDemand = 0.8f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Potassium, 1.3f), new NutrientNeed(Nutrient.Nitrogen, 0.8f) },
        salinityTolerance = 2f, compactionTolerance = 0.6f, minTilth = 0.2f,
        waterLow = 0.25f, waterHigh = 0.8f, droughtTolerance = 0.6f, waterlogTolerance = 0.1f, waterUse = 0.9f,
        tempMin = -18f, tempLow = 18f, tempHigh = 30f, tempMax = 38f, germinationMin = 12f, germinationMax = 30f, frostSensitivity = 0.1f, shockSensitivity = 0.3f,
        humidityLow = 0.3f, humidityHigh = 0.65f, lightLow = 0.8f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring, floweringSeasons = SeasonMask.Spring | SeasonMask.Summer, dormancySeasons = SeasonMask.Winter, chillHours = 60f,
        depthCm = 1.5f, depthToleranceCm = 1f, spacing = 1.5f, rowSpacing = 2f, rootRadius = 0.9f, needsSupport = true,
        produce = P(ProducePart.Fruit, "Grapes", PlantUses.Food | PlantUses.Alchemical, 4, 16, A(2f, 1f, 2f, 2.5f, 3f, 2f, 3f, 3f, 2f), Pollination.Self, 4, 2f, 0.3f, 6f, 0.1f,
            C(0.5f, 0.7f, 0.35f), C(0.35f, 0.12f, 0.35f), C(0.25f, 0.08f, 0.22f), C(0.25f, 0.2f, 0.12f), 0.1f, description: "A bunch of dark grapes.", alchemy: "Sourfruit"),
        pestSusceptibility = A(0.6f, 0.7f, 1f, 0.8f, 1f, 1.2f, 0.2f, 0.3f), diseaseSusceptibility = A(1.6f, 0.8f, 0.5f, 1.2f, 0.6f, 0.7f),
        pruning = PruneNeed.Heavy, pruneSeasons = SeasonMask.Winter,
        look = L(PlantShape.Vine, 1.8f, 1.2f, C(0.3f, 0.55f, 0.25f), C(0.75f, 0.8f, 0.5f), C(0.45f, 0.33f, 0.22f)), crossGroup = "grape", seedValue = 1f, seedStack = 20,
    };

    private static PlantSpeciesData Chamomile() => new PlantSpeciesData
    {
        id = "chamomile", name = "Chamomile", category = PlantCategory.Medicinal, lifeCycle = PlantLifeCycle.Annual,
        handbook = "A small daisy-flowered herb whose flower heads soothe and calm. Its seed is dust-fine and wants light to sprout.",
        germinationDays = 2f, seedlingDays = 2f, juvenileDays = 2f, vegetativeDays = 2f, floweringDays = 3f, senescenceDays = 2f,
        soilAffinity = A(1f, 0.5f, 0.8f, 0.7f, 0.5f, 0.8f, 0.3f, 0.9f),
        phMin = 5.6f, phLow = 6.5f, phHigh = 7.5f, phMax = 8.5f, nutrientDemand = 0.5f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 0.6f) },
        salinityTolerance = 3f, compactionTolerance = 0.6f, minTilth = 0.3f,
        waterLow = 0.25f, waterHigh = 0.85f, droughtTolerance = 0.55f, waterlogTolerance = 0.15f, waterUse = 0.7f,
        tempMin = -4f, tempLow = 12f, tempHigh = 24f, tempMax = 32f, germinationMin = 7f, germinationMax = 26f, frostSensitivity = 0.4f, shockSensitivity = 0.2f,
        humidityLow = 0.3f, humidityHigh = 0.7f, lightLow = 0.7f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer | SeasonMask.Autumn,
        lunarStrength = 0.3f, moonPotency = A(0.9f, 1.05f, 1.15f, 0.95f), plantingTime = PlantingTime.Day, plantingTimeStrength = 0.2f,
        depthCm = 0f, depthToleranceCm = 0.4f, surfaceSown = true, spacing = 0.2f, rowSpacing = 0.3f, rootRadius = 0.1f,
        companions = new List<CompanionRule>
        {
            new CompanionRule("*", CompanionEffect.AttractPollinators, 0.5f, 2f),
            new CompanionRule("cabbage", CompanionEffect.Growth, 1.08f, 0.8f),
        },
        produce = P(ProducePart.Flower, "Chamomile Flowers", PlantUses.Medicinal | PlantUses.Alchemical, 6, 20, A(1f, 0.7f, 0.7f, 0.7f, 1f, 1f, 1f, 1.5f, 1f), Pollination.Insect, 20, 0.6f, 0.01f, 0f, 0.5f,
            C(0.95f, 0.95f, 0.9f), C(1f, 1f, 0.95f), C(0.85f, 0.8f, 0.6f), C(0.45f, 0.4f, 0.25f), 0.02f, 2f, 4, description: "Small white flower heads with a golden heart.", alchemy: "Chamomile"),
        pestSusceptibility = A(1f, 0.3f, 0.5f, 0.4f, 0.5f, 0.3f, 0.5f, 0.4f), diseaseSusceptibility = A(0.8f, 0.6f, 0.3f, 0.5f, 0.3f, 0.3f),
        look = L(PlantShape.Flower, 0.35f, 0.25f, C(0.4f, 0.62f, 0.3f), C(1f, 1f, 0.95f)), seedValue = 0.3f,
    };

    private static PlantSpeciesData Comfrey() => new PlantSpeciesData
    {
        id = "comfrey", name = "Comfrey", category = PlantCategory.Medicinal, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "Knitbone. A deep-rooted perennial whose leaves are laid on bruises and breaks. Cut it and it grows again.",
        germinationDays = 3f, seedlingDays = 3f, juvenileDays = 4f, vegetativeDays = 4f, floweringDays = 2f, senescenceDays = 6f, yearsToFullSize = 0.5f,
        soilAffinity = A(0.5f, 1f, 0.4f, 1f, 0.7f, 0.8f, 0.6f, 0.8f),
        phMin = 5.5f, phLow = 6f, phHigh = 7.5f, phMax = 8.2f, nutrientDemand = 1.4f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 1.2f), new NutrientNeed(Nutrient.Potassium, 1.5f) },
        salinityTolerance = 2f, compactionTolerance = 0.7f, minTilth = 0.2f,
        waterLow = 0.45f, waterHigh = 1.05f, droughtTolerance = 0.45f, waterlogTolerance = 0.5f, waterUse = 1.2f,
        tempMin = -20f, tempLow = 10f, tempHigh = 24f, tempMax = 32f, germinationMin = 8f, germinationMax = 25f, frostSensitivity = 0.1f, shockSensitivity = 0.2f,
        humidityLow = 0.45f, humidityHigh = 0.9f, lightLow = 0.4f, lightHigh = 1f, photoperiod = PhotoperiodType.LongDay, criticalDayLength = 14f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer | SeasonMask.Autumn, floweringSeasons = SeasonMask.Summer, dormancySeasons = SeasonMask.Winter,
        depthCm = 1.5f, depthToleranceCm = 1f, spacing = 0.6f, rowSpacing = 0.8f, rootRadius = 0.45f,
        produce = P(ProducePart.Leaf, "Comfrey Leaves", PlantUses.Medicinal, 3, 10, A(0.5f, 0.5f, 0.7f, 0.7f, 1f, 1.5f, 2f, 3f, 1f), Pollination.None, 3, 0.5f, 0.05f, 1f, 0.45f,
            C(0.4f, 0.62f, 0.3f), C(0.25f, 0.5f, 0.22f), C(0.35f, 0.42f, 0.2f), C(0.3f, 0.25f, 0.12f), 0.12f, 1f, 1, regrows: true, description: "Broad, hairy leaves.", alchemy: "Sage Leaves"),
        pestSusceptibility = A(0.6f, 0.4f, 0.3f, 0.3f, 0.4f, 0.2f, 0.8f, 0.7f), diseaseSusceptibility = A(1f, 0.3f, 0.2f, 0.6f, 0.2f, 0.2f),
        look = L(PlantShape.Herb, 0.8f, 0.6f, C(0.25f, 0.48f, 0.2f), C(0.65f, 0.45f, 0.75f)), seedValue = 0.5f,
    };

    private static PlantSpeciesData Lavender() => new PlantSpeciesData
    {
        id = "lavender", name = "Lavender", category = PlantCategory.Medicinal, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A grey, scented shrub of dry, sunny, limey slopes. It drowns in wet ground. Bees cannot leave it alone.",
        germinationDays = 4f, seedlingDays = 3f, juvenileDays = 4f, vegetativeDays = 5f, floweringDays = 4f, senescenceDays = 8f, yearsToFullSize = 1f, lifespanYears = 6f,
        soilAffinity = A(1f, 0.25f, 1f, 0.6f, 0.2f, 0.9f, 0.05f, 0.8f),
        phMin = 6.5f, phLow = 7f, phHigh = 8f, phMax = 8.8f, nutrientDemand = 0.4f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 0.5f), new NutrientNeed(Nutrient.Potassium, 0.5f) },
        salinityTolerance = 3f, compactionTolerance = 0.6f, minTilth = 0.2f,
        waterLow = 0.15f, waterHigh = 0.7f, droughtTolerance = 0.85f, waterlogTolerance = 0.05f, waterUse = 0.6f,
        tempMin = -15f, tempLow = 15f, tempHigh = 30f, tempMax = 38f, germinationMin = 12f, germinationMax = 26f, frostSensitivity = 0.2f, shockSensitivity = 0.2f,
        humidityLow = 0.2f, humidityHigh = 0.6f, lightLow = 0.8f, lightHigh = 1f, photoperiod = PhotoperiodType.LongDay, criticalDayLength = 13.5f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer, floweringSeasons = SeasonMask.Summer, dormancySeasons = SeasonMask.Winter,
        depthCm = 0.2f, depthToleranceCm = 0.3f, surfaceSown = true, spacing = 0.5f, rowSpacing = 0.7f, rootRadius = 0.35f,
        companions = new List<CompanionRule>
        {
            new CompanionRule("*", CompanionEffect.RepelPests, 0.45f, 1.2f, PestKind.Aphids, PestKind.Caterpillars, PestKind.SapSuckers),
            new CompanionRule("*", CompanionEffect.AttractPollinators, 0.8f, 2.5f),
        },
        produce = P(ProducePart.Flower, "Lavender Sprigs", PlantUses.Medicinal | PlantUses.Ornamental | PlantUses.Alchemical, 8, 25, A(1.5f, 1f, 1f, 1f, 1f, 1.5f, 1.5f, 2f, 1f), Pollination.Insect, 15, 0.8f, 0.01f, 0f, 0.55f,
            C(0.55f, 0.5f, 0.75f), C(0.55f, 0.35f, 0.8f), C(0.5f, 0.42f, 0.6f), C(0.4f, 0.35f, 0.3f), 0.03f, description: "Fragrant purple spikes.", alchemy: "Lavender"),
        pestSusceptibility = A(0.3f, 0.2f, 0.4f, 0.3f, 0.6f, 0.3f, 0.2f, 0.2f), diseaseSusceptibility = A(0.6f, 1.8f, 0.4f, 0.4f, 0.3f, 0.2f),
        pruning = PruneNeed.Light, pruneSeasons = SeasonMask.Summer | SeasonMask.Autumn,
        look = L(PlantShape.Bush, 0.55f, 0.6f, C(0.45f, 0.55f, 0.45f), C(0.55f, 0.35f, 0.8f)), seedValue = 0.6f,
    };

    private static PlantSpeciesData Marigold() => new PlantSpeciesData
    {
        id = "marigold", name = "Marigold", category = PlantCategory.Ornamental, lifeCycle = PlantLifeCycle.Annual,
        handbook = "A cheerful orange flower. Old gardeners plant it among their vegetables and swear by it - against what, they never quite agree.",
        germinationDays = 1.5f, seedlingDays = 1.5f, juvenileDays = 2f, vegetativeDays = 2f, floweringDays = 4f, senescenceDays = 2f,
        soilAffinity = A(0.9f, 0.7f, 0.7f, 0.9f, 0.5f, 0.9f, 0.3f, 0.9f),
        phMin = 5.8f, phLow = 6.2f, phHigh = 7.5f, phMax = 8.2f, nutrientDemand = 0.6f,
        salinityTolerance = 2.5f, compactionTolerance = 0.6f, minTilth = 0.25f,
        waterLow = 0.3f, waterHigh = 0.85f, droughtTolerance = 0.5f, waterlogTolerance = 0.15f, waterUse = 0.8f,
        tempMin = 0f, tempLow = 18f, tempHigh = 30f, tempMax = 36f, germinationMin = 12f, germinationMax = 32f, frostSensitivity = 1f, shockSensitivity = 0.3f,
        humidityLow = 0.3f, humidityHigh = 0.75f, lightLow = 0.7f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Summer,
        depthCm = 0.8f, depthToleranceCm = 0.6f, spacing = 0.25f, rowSpacing = 0.3f, rootRadius = 0.15f,
        companions = new List<CompanionRule>
        {
            new CompanionRule("*", CompanionEffect.RepelPests, 0.5f, 1.2f, PestKind.Aphids, PestKind.RootParasites, PestKind.SapSuckers),
            new CompanionRule("*", CompanionEffect.AttractPollinators, 0.4f, 2f),
        },
        produce = P(ProducePart.Flower, "Marigold Flowers", PlantUses.Ornamental | PlantUses.Medicinal, 5, 15, A(1f, 0.7f, 0.7f, 0.7f, 1f, 1.5f, 1.5f, 1.5f, 1f), Pollination.Insect, 15, 0.4f, 0.01f, 0f, 0.25f,
            C(0.95f, 0.75f, 0.2f), C(1f, 0.55f, 0.05f), C(0.75f, 0.45f, 0.15f), C(0.4f, 0.3f, 0.15f), 0.035f, 2f, 5, description: "Bright orange blooms.", alchemy: "Sunpetal"),
        pestSusceptibility = A(0.7f, 0.4f, 0.9f, 0.4f, 0.6f, 0.1f, 1f, 0.9f), diseaseSusceptibility = A(0.7f, 0.6f, 0.4f, 0.5f, 0.3f, 0.3f),
        look = L(PlantShape.Flower, 0.4f, 0.3f, C(0.3f, 0.55f, 0.25f), C(1f, 0.6f, 0.05f)), seedValue = 0.2f,
    };

    private static PlantSpeciesData Mandrake() => new PlantSpeciesData
    {
        id = "mandrake", name = "Mandrake", category = PlantCategory.Alchemical, lifeCycle = PlantLifeCycle.SingleHarvest,
        handbook = "Of the mandrake little is written that can be trusted. The herbals that mention it contradict one another, and their authors are mostly dead.",
        germinationDays = 4f, seedlingDays = 3f, juvenileDays = 5f, vegetativeDays = 6f, senescenceDays = 4f,
        soilAffinity = A(0.7f, 0.5f, 0.4f, 0.9f, 1f, 0.7f, 0.6f, 1f),
        phMin = 5f, phLow = 5.8f, phHigh = 6.8f, phMax = 7.6f, nutrientDemand = 0.9f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Phosphorus, 1.3f) },
        salinityTolerance = 1.2f, compactionTolerance = 0.45f, minTilth = 0.45f,
        waterLow = 0.4f, waterHigh = 0.95f, droughtTolerance = 0.3f, waterlogTolerance = 0.3f, waterUse = 0.8f,
        tempMin = -5f, tempLow = 10f, tempHigh = 20f, tempMax = 28f, germinationMin = 6f, germinationMax = 22f, frostSensitivity = 0.3f, shockSensitivity = 0.4f,
        humidityLow = 0.5f, humidityHigh = 0.9f, lightLow = 0.15f, lightHigh = 0.55f, lightExcess = 0.6f,
        sowingSeasons = SeasonMask.Autumn | SeasonMask.Winter | SeasonMask.Spring,
        lunarStrength = 0.8f, moonGermination = A(1.5f, 1.1f, 0.6f, 0.9f), moonPotency = A(1.3f, 1f, 0.8f, 1.1f), plantingTime = PlantingTime.Night, plantingTimeStrength = 0.7f,
        depthCm = 3f, depthToleranceCm = 1f, spacing = 0.4f, rowSpacing = 0.5f, rootRadius = 0.25f,
        produce = P(ProducePart.Root, "Mandrake Root", PlantUses.Alchemical | PlantUses.Medicinal | PlantUses.Magical, 1, 1, A(2f, 2f, 2f, 2f, 3f, 3f, 4f, 4f, 3f), Pollination.None, 6, 12f, 0.25f, 0f, 0.7f,
            C(0.75f, 0.65f, 0.5f), C(0.65f, 0.5f, 0.35f), C(0.5f, 0.4f, 0.3f), C(0.25f, 0.2f, 0.12f), 0.08f, description: "A forked, man-shaped root.", alchemy: "Mandrake Root"),
        pestSusceptibility = A(0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 1f, 0.8f, 0.8f), diseaseSusceptibility = A(0.6f, 1.3f, 0.4f, 0.4f, 0.4f, 0.4f),
        look = L(PlantShape.RootCrop, 0.3f, 0.35f, C(0.2f, 0.38f, 0.18f), C(0.6f, 0.45f, 0.75f)), seedValue = 4f, seedStack = 20,
    };

    private static PlantSpeciesData Nightshade() => new PlantSpeciesData
    {
        id = "nightshade", name = "Nightshade", category = PlantCategory.Alchemical, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A shade-loving herb of chalky woods with glossy black berries. Every part of it is poison, and every alchemist wants some.",
        germinationDays = 3f, seedlingDays = 3f, juvenileDays = 4f, vegetativeDays = 5f, floweringDays = 3f, senescenceDays = 5f, yearsToFullSize = 1f, lifespanYears = 5f,
        soilAffinity = A(0.4f, 0.8f, 0.5f, 1f, 0.6f, 0.7f, 0.5f, 0.9f),
        phMin = 6.5f, phLow = 7f, phHigh = 8f, phMax = 8.6f, nutrientDemand = 1f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Nitrogen, 1.2f) },
        salinityTolerance = 1.5f, compactionTolerance = 0.5f, minTilth = 0.3f,
        waterLow = 0.45f, waterHigh = 0.95f, droughtTolerance = 0.25f, waterlogTolerance = 0.3f, waterUse = 1f,
        tempMin = -10f, tempLow = 12f, tempHigh = 24f, tempMax = 32f, germinationMin = 10f, germinationMax = 26f, frostSensitivity = 0.3f, shockSensitivity = 0.3f,
        humidityLow = 0.5f, humidityHigh = 0.9f, lightLow = 0.2f, lightHigh = 0.6f, lightExcess = 0.4f,
        sowingSeasons = SeasonMask.Spring | SeasonMask.Autumn, floweringSeasons = SeasonMask.Summer, dormancySeasons = SeasonMask.Winter, chillHours = 30f,
        lunarStrength = 0.4f, moonPotency = A(1.2f, 1f, 0.9f, 1.1f), plantingTime = PlantingTime.Night, plantingTimeStrength = 0.3f,
        depthCm = 0.5f, depthToleranceCm = 0.5f, spacing = 0.5f, rowSpacing = 0.7f, rootRadius = 0.3f,
        produce = P(ProducePart.Fruit, "Nightshade Berries", PlantUses.Alchemical | PlantUses.Medicinal, 5, 20, A(2f, 1f, 1.5f, 2f, 2f, 1.5f, 2f, 2f, 1.5f), Pollination.Insect, 10, 5f, 0.01f, 0f, 0.6f,
            C(0.4f, 0.6f, 0.3f), C(0.1f, 0.08f, 0.15f), C(0.12f, 0.06f, 0.12f), C(0.2f, 0.18f, 0.12f), 0.02f, description: "Glossy black berries. Poisonous.", alchemy: "Nightshade"),
        pestSusceptibility = A(0.6f, 0.4f, 0.5f, 0.9f, 0.4f, 0.4f, 0.4f, 0.4f), diseaseSusceptibility = A(0.8f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f),
        look = L(PlantShape.Bush, 0.9f, 0.7f, C(0.22f, 0.42f, 0.2f), C(0.55f, 0.35f, 0.55f)), seedValue = 2f, seedStack = 30,
    };

    private static PlantSpeciesData Moonpetal() => new PlantSpeciesData
    {
        id = "moonpetal", name = "Moonpetal", category = PlantCategory.Magical, lifeCycle = PlantLifeCycle.Annual,
        handbook = "Silver petals said to open for the moon. The few who grew it would not say how.",
        germinationDays = 3f, seedlingDays = 3f, juvenileDays = 3f, vegetativeDays = 4f, floweringDays = 3f, senescenceDays = 3f,
        soilAffinity = A(0.3f, 0.4f, 0.3f, 0.6f, 0.6f, 0.5f, 0.4f, 1f),
        phMin = 5.5f, phLow = 6.2f, phHigh = 7f, phMax = 7.8f, nutrientDemand = 0.8f, magicNeed = 0.35f,
        salinityTolerance = 0.8f, compactionTolerance = 0.5f, minTilth = 0.4f,
        waterLow = 0.45f, waterHigh = 0.95f, droughtTolerance = 0.2f, waterlogTolerance = 0.2f, waterUse = 0.9f,
        tempMin = 0f, tempLow = 10f, tempHigh = 20f, tempMax = 27f, germinationMin = 6f, germinationMax = 22f, frostSensitivity = 0.5f, shockSensitivity = 0.6f,
        humidityLow = 0.5f, humidityHigh = 0.9f, lightLow = 0.1f, lightHigh = 0.45f, lightExcess = 0.8f, photoperiod = PhotoperiodType.ShortDay, criticalDayLength = 12f,
        sowingSeasons = SeasonMask.All,
        lunarStrength = 1f, moonGermination = A(0.5f, 1f, 1.8f, 0.9f), moonPotency = A(0.6f, 1f, 1.6f, 0.9f), plantingTime = PlantingTime.Night, plantingTimeStrength = 1f,
        depthCm = 0.5f, depthToleranceCm = 0.3f, spacing = 0.3f, rowSpacing = 0.4f, rootRadius = 0.15f,
        produce = P(ProducePart.Flower, "Moonpetal", PlantUses.Magical | PlantUses.Alchemical, 2, 7, A(1.5f, 1f, 1f, 1f, 1f, 1f, 1f, 1.5f, 1f), Pollination.Insect, 8, 30f, 0.01f, 0f, 0.9f,
            C(0.75f, 0.8f, 0.95f), C(0.85f, 0.9f, 1f), C(0.65f, 0.7f, 0.8f), C(0.35f, 0.35f, 0.4f), 0.04f, 3f, 2, description: "A pale silver flower.", alchemy: "Moonbloom"),
        pestSusceptibility = A(0.5f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 1.2f, 1f), diseaseSusceptibility = A(1f, 0.8f, 0.4f, 0.4f, 0.4f, 0.4f),
        look = L(PlantShape.Flower, 0.4f, 0.25f, C(0.35f, 0.45f, 0.5f), C(0.85f, 0.9f, 1f), null, C(0.6f, 0.75f, 1f, 0.6f)), crossGroup = "lunaria", seedValue = 8f, seedStack = 20,
    };

    private static PlantSpeciesData Emberroot() => new PlantSpeciesData
    {
        id = "emberroot", name = "Emberroot", category = PlantCategory.Magical, lifeCycle = PlantLifeCycle.SingleHarvest,
        handbook = "A root found where the earth is warm and black with ash. It glows faintly when cut.",
        germinationDays = 2f, seedlingDays = 2f, juvenileDays = 3f, vegetativeDays = 4f, senescenceDays = 2f,
        soilAffinity = A(0.5f, 0.2f, 0.6f, 0.4f, 0.1f, 1f, 0.05f, 0.7f),
        phMin = 5f, phLow = 5.5f, phHigh = 6.5f, phMax = 7.5f, nutrientDemand = 1.1f, magicNeed = 0.1f,
        needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Potassium, 1.5f), new NutrientNeed(Nutrient.Iron, 1.5f), new NutrientNeed(Nutrient.Magnesium, 1.3f) },
        salinityTolerance = 2.5f, compactionTolerance = 0.5f, minTilth = 0.35f,
        waterLow = 0.15f, waterHigh = 0.6f, droughtTolerance = 0.7f, waterlogTolerance = 0.05f, waterUse = 0.6f,
        tempMin = 6f, tempLow = 24f, tempHigh = 36f, tempMax = 44f, germinationMin = 18f, germinationMax = 40f, frostSensitivity = 1f, shockSensitivity = 0.4f,
        humidityLow = 0.1f, humidityHigh = 0.5f, lightLow = 0.7f, lightHigh = 1f,
        sowingSeasons = SeasonMask.Summer | SeasonMask.Spring, lunarStrength = 0.2f,
        depthCm = 4f, depthToleranceCm = 1.5f, spacing = 0.3f, rowSpacing = 0.45f, rootRadius = 0.2f,
        produce = P(ProducePart.Root, "Emberroot", PlantUses.Magical | PlantUses.Alchemical, 1, 2, A(1f, 1.5f, 1.5f, 1.5f, 2f, 2f, 3f, 3f, 2f), Pollination.None, 4, 25f, 0.15f, 0f, 0.85f,
            C(0.9f, 0.45f, 0.15f), C(1f, 0.35f, 0.05f), C(0.6f, 0.25f, 0.1f), C(0.2f, 0.15f, 0.1f), 0.05f, description: "A root that glows like a coal.", alchemy: "Emberroot"),
        pestSusceptibility = A(0.2f, 0.2f, 0.2f, 0.8f, 0.2f, 0.8f, 0.2f, 0.2f), diseaseSusceptibility = A(0.3f, 1.5f, 0.8f, 0.3f, 0.3f, 0.3f),
        look = L(PlantShape.RootCrop, 0.35f, 0.3f, C(0.55f, 0.35f, 0.15f), C(1f, 0.5f, 0.1f), null, C(1f, 0.4f, 0.1f, 0.4f)), seedValue = 6f, seedStack = 20,
    };

    private static PlantSpeciesData Frostbloom() => new PlantSpeciesData
    {
        id = "frostbloom", name = "Frostbloom", category = PlantCategory.Rare, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "A legend of the high snows: a flower that blooms in the dead of winter and withers in the summer sun.",
        germinationDays = 4f, seedlingDays = 4f, juvenileDays = 4f, vegetativeDays = 5f, floweringDays = 4f, senescenceDays = 6f, yearsToFullSize = 1f, lifespanYears = 8f,
        soilAffinity = A(0.3f, 0.5f, 0.6f, 0.7f, 1f, 0.4f, 0.6f, 0.9f),
        phMin = 4f, phLow = 4.6f, phHigh = 5.6f, phMax = 6.5f, nutrientDemand = 0.6f, needs = new List<NutrientNeed> { new NutrientNeed(Nutrient.Iron, 1.3f) },
        salinityTolerance = 1f, compactionTolerance = 0.5f, minTilth = 0.3f,
        waterLow = 0.45f, waterHigh = 1f, droughtTolerance = 0.2f, waterlogTolerance = 0.4f, waterUse = 0.7f,
        tempMin = -28f, tempLow = -4f, tempHigh = 10f, tempMax = 24f, germinationMin = -2f, germinationMax = 12f, frostSensitivity = 0f, shockSensitivity = 0.3f,
        humidityLow = 0.5f, humidityHigh = 0.95f, lightLow = 0.3f, lightHigh = 0.8f,
        sowingSeasons = SeasonMask.Autumn | SeasonMask.Winter, floweringSeasons = SeasonMask.Winter, dormancySeasons = SeasonMask.Summer,
        lunarStrength = 0.5f, moonGermination = A(1.3f, 1f, 1.2f, 0.8f), moonPotency = A(1.2f, 1f, 1.3f, 0.9f),
        depthCm = 1f, depthToleranceCm = 0.6f, spacing = 0.35f, rowSpacing = 0.45f, rootRadius = 0.2f,
        produce = P(ProducePart.Flower, "Frostbloom", PlantUses.Alchemical | PlantUses.Magical | PlantUses.Medicinal, 2, 6, A(2f, 1.5f, 1.5f, 1.5f, 1.5f, 2f, 2f, 2f, 1.5f), Pollination.Insect, 5, 40f, 0.01f, 0f, 0.9f,
            C(0.8f, 0.92f, 1f), C(0.7f, 0.9f, 1f), C(0.75f, 0.8f, 0.9f), C(0.4f, 0.42f, 0.45f), 0.04f, description: "An ice-blue flower, cold to the touch.", alchemy: "Frostmint"),
        pestSusceptibility = A(0.2f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.4f, 0.4f), diseaseSusceptibility = A(0.7f, 0.6f, 0.2f, 0.3f, 0.2f, 0.2f),
        look = L(PlantShape.Flower, 0.3f, 0.25f, C(0.4f, 0.55f, 0.6f), C(0.75f, 0.92f, 1f), null, C(0.7f, 0.9f, 1f, 0.3f)), crossGroup = "lunaria", seedValue = 12f, seedStack = 10,
    };

    private static PlantSpeciesData TwilightBloom() => new PlantSpeciesData
    {
        id = "twilight_bloom", name = "Twilight Bloom", category = PlantCategory.Rare, lifeCycle = PlantLifeCycle.Perennial,
        handbook = "",
        germinationDays = 3f, seedlingDays = 3f, juvenileDays = 4f, vegetativeDays = 4f, floweringDays = 3f, senescenceDays = 5f, yearsToFullSize = 1f, lifespanYears = 6f,
        soilAffinity = A(0.4f, 0.4f, 0.4f, 0.7f, 0.8f, 0.4f, 0.5f, 1f),
        phMin = 4.8f, phLow = 5.4f, phHigh = 6.6f, phMax = 7.4f, nutrientDemand = 0.8f, magicNeed = 0.2f,
        salinityTolerance = 1f, compactionTolerance = 0.5f, minTilth = 0.35f,
        waterLow = 0.45f, waterHigh = 0.95f, droughtTolerance = 0.25f, waterlogTolerance = 0.3f, waterUse = 0.8f,
        tempMin = -12f, tempLow = 2f, tempHigh = 16f, tempMax = 24f, germinationMin = 2f, germinationMax = 18f, frostSensitivity = 0.1f, shockSensitivity = 0.4f,
        humidityLow = 0.5f, humidityHigh = 0.95f, lightLow = 0.15f, lightHigh = 0.6f, lightExcess = 0.5f, photoperiod = PhotoperiodType.ShortDay, criticalDayLength = 11f,
        sowingSeasons = SeasonMask.Autumn | SeasonMask.Winter | SeasonMask.Spring, dormancySeasons = SeasonMask.Summer,
        lunarStrength = 0.9f, moonGermination = A(1.2f, 1f, 1.4f, 0.9f), moonPotency = A(1.4f, 1.1f, 1.5f, 1f), plantingTime = PlantingTime.Night, plantingTimeStrength = 0.8f,
        depthCm = 0.8f, depthToleranceCm = 0.5f, spacing = 0.35f, rowSpacing = 0.45f, rootRadius = 0.2f,
        produce = P(ProducePart.Flower, "Twilight Bloom", PlantUses.Magical | PlantUses.Alchemical, 2, 6, A(1.5f, 1f, 1f, 1f, 1.5f, 1.5f, 1.5f, 1.5f, 1f), Pollination.Insect, 5, 80f, 0.01f, 0f, 1f,
            C(0.55f, 0.45f, 0.85f), C(0.45f, 0.35f, 0.95f), C(0.4f, 0.35f, 0.6f), C(0.3f, 0.28f, 0.35f), 0.045f, description: "A dusk-violet flower that seems to hold its own light.", alchemy: "Starlight Lotus"),
        pestSusceptibility = A(0.3f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.6f, 0.6f), diseaseSusceptibility = A(0.8f, 0.6f, 0.3f, 0.3f, 0.3f, 0.3f),
        look = L(PlantShape.Flower, 0.38f, 0.28f, C(0.32f, 0.4f, 0.5f), C(0.45f, 0.35f, 0.95f), null, C(0.6f, 0.5f, 1f, 0.6f)), crossGroup = "lunaria", seedValue = 25f, seedStack = 10,
    };

    // ---------------------------------------------------------------- materials
    private static SoilAmendmentData M(string id, string name, AmendmentKind kind, float kg, string description, float value, Color color, params SoilEffect[] effects) => new SoilAmendmentData
    {
        id = id, name = name, kind = kind, portionKg = kg, description = description, value = value, color = color,
        effects = new List<SoilEffect>(effects),
    };

    private static SoilEffect E(SoilChannel c, float amount) => new SoilEffect(c, amount);

    private static float[] Pests(float aphids, float caterpillars, float mites, float beetles, float sap, float roots, float slugs, float snails) =>
        new[] { aphids, caterpillars, mites, beetles, sap, roots, slugs, snails };

    public static List<SoilAmendmentData> Amendments()
    {
        var list = new List<SoilAmendmentData>();
        SoilAmendmentData a;

        a = M("sand", "Sand", AmendmentKind.Texture, 5f, "Coarse river sand. Worked into heavy soil, it opens it up.", 1f, new Color(0.85f, 0.78f, 0.6f),
            E(SoilChannel.Drainage, 0.04f), E(SoilChannel.WaterRetention, -0.035f), E(SoilChannel.Compaction, -0.03f));
        list.Add(a);
        a = M("clay", "Clay", AmendmentKind.Texture, 5f, "Heavy clay. Worked into light soil, it holds water and food.", 1f, new Color(0.62f, 0.4f, 0.28f),
            E(SoilChannel.WaterRetention, 0.04f), E(SoilChannel.Drainage, -0.035f), E(SoilChannel.Compaction, 0.02f), E(SoilChannel.Potassium, 6f), E(SoilChannel.Calcium, 40f), E(SoilChannel.Magnesium, 8f));
        list.Add(a);
        a = M("gravel", "Gravel", AmendmentKind.Texture, 5f, "Small stones. Water runs through it.", 1f, new Color(0.6f, 0.58f, 0.55f),
            E(SoilChannel.Drainage, 0.05f), E(SoilChannel.Compaction, -0.04f), E(SoilChannel.Stones, 0.05f), E(SoilChannel.WaterRetention, -0.02f));
        list.Add(a);
        a = M("peat", "Peat", AmendmentKind.Organic, 3f, "Dark, fibrous peat from the bogs.", 2f, new Color(0.22f, 0.16f, 0.12f),
            E(SoilChannel.OrganicMatter, 1.2f), E(SoilChannel.WaterRetention, 0.03f), E(SoilChannel.Compaction, -0.02f), E(SoilChannel.Nitrogen, 1f));
        a.phEffect = -0.12f;
        list.Add(a);
        a = M("compost", "Organic Compost", AmendmentKind.Organic, 3f, "Well-rotted garden compost. It changes the soil slowly and for good.", 2f, new Color(0.25f, 0.18f, 0.11f),
            E(SoilChannel.OrganicMatter, 0.6f), E(SoilChannel.Nitrogen, 10f), E(SoilChannel.Phosphorus, 3f), E(SoilChannel.Potassium, 8f), E(SoilChannel.Calcium, 15f),
            E(SoilChannel.Magnesium, 3f), E(SoilChannel.Sulfur, 1f), E(SoilChannel.WaterRetention, 0.015f), E(SoilChannel.Compaction, -0.02f), E(SoilChannel.Iron, 0.3f), E(SoilChannel.Zinc, 0.05f));
        a.slowRelease = true;
        a.phEffect = 0.02f;
        list.Add(a);
        a = M("wood_ash", "Wood Ash", AmendmentKind.PhModifier, 0.5f, "Grey ash from the hearth.", 0.5f, new Color(0.7f, 0.7f, 0.68f),
            E(SoilChannel.Potassium, 25f), E(SoilChannel.Calcium, 60f), E(SoilChannel.Magnesium, 5f), E(SoilChannel.Phosphorus, 2f), E(SoilChannel.Salinity, 0.05f));
        a.phEffect = 0.12f;
        list.Add(a);
        a = M("limestone", "Limestone", AmendmentKind.PhModifier, 1f, "Ground limestone.", 1.5f, new Color(0.9f, 0.9f, 0.86f),
            E(SoilChannel.Calcium, 120f), E(SoilChannel.Magnesium, 10f));
        a.phEffect = 0.25f;
        list.Add(a);
        a = M("sulfur", "Sulfur", AmendmentKind.PhModifier, 0.2f, "Yellow sulfur powder.", 2f, new Color(0.9f, 0.85f, 0.25f),
            E(SoilChannel.Sulfur, 30f));
        a.phEffect = -0.25f;
        list.Add(a);
        a = M("manure", "Animal Manure", AmendmentKind.Fertilizer, 3f, "Stable manure. Strong, and not always clean.", 1f, new Color(0.3f, 0.22f, 0.12f),
            E(SoilChannel.Nitrogen, 20f), E(SoilChannel.Phosphorus, 6f), E(SoilChannel.Potassium, 15f), E(SoilChannel.OrganicMatter, 0.5f), E(SoilChannel.Salinity, 0.12f),
            E(SoilChannel.Pathogens, 0.03f), E(SoilChannel.Magnesium, 2f), E(SoilChannel.Sulfur, 2f));
        a.slowRelease = true;
        list.Add(a);
        a = M("bone_meal", "Bone Meal", AmendmentKind.Fertilizer, 0.3f, "Ground bones.", 2f, new Color(0.9f, 0.86f, 0.75f),
            E(SoilChannel.Phosphorus, 25f), E(SoilChannel.Calcium, 80f), E(SoilChannel.Nitrogen, 3f));
        a.slowRelease = true;
        list.Add(a);
        a = M("mineral_fertilizer", "Mineral Fertilizer", AmendmentKind.Fertilizer, 0.1f, "Grey granules that dissolve in the soil water.", 3f, new Color(0.75f, 0.75f, 0.8f),
            E(SoilChannel.Nitrogen, 16f), E(SoilChannel.Phosphorus, 8f), E(SoilChannel.Potassium, 16f), E(SoilChannel.Sulfur, 3f), E(SoilChannel.Salinity, 0.15f));
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("alchemical_fertilizer", "Alchemical Fertilizer", AmendmentKind.Fertilizer, 0.1f, "A shimmering powder from an alchemist's bench.", 8f, new Color(0.55f, 0.85f, 0.6f),
            E(SoilChannel.Nitrogen, 14f), E(SoilChannel.Phosphorus, 10f), E(SoilChannel.Potassium, 14f), E(SoilChannel.Magnesium, 4f), E(SoilChannel.Sulfur, 3f), E(SoilChannel.Iron, 2f),
            E(SoilChannel.Zinc, 0.5f), E(SoilChannel.Manganese, 1f), E(SoilChannel.Boron, 0.2f), E(SoilChannel.MagicEssence, 0.02f), E(SoilChannel.Salinity, 0.05f));
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("liquid_fertilizer", "Liquid Fertilizer", AmendmentKind.Fertilizer, 0.5f, "A strong-smelling feed, poured on.", 2f, new Color(0.3f, 0.35f, 0.2f),
            E(SoilChannel.Nitrogen, 8f), E(SoilChannel.Potassium, 8f), E(SoilChannel.Phosphorus, 3f));
        a.liquid = true;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("slow_fertilizer", "Slow-Release Fertilizer", AmendmentKind.Fertilizer, 0.15f, "Coated pellets that feed for weeks.", 4f, new Color(0.7f, 0.65f, 0.5f),
            E(SoilChannel.Nitrogen, 18f), E(SoilChannel.Phosphorus, 9f), E(SoilChannel.Potassium, 16f));
        a.slowRelease = true;
        list.Add(a);
        a = M("magical_compost", "Magical Compost", AmendmentKind.Organic, 2f, "Compost that glitters faintly in the dark.", 15f, new Color(0.3f, 0.2f, 0.35f),
            E(SoilChannel.OrganicMatter, 0.5f), E(SoilChannel.MagicEssence, 0.08f), E(SoilChannel.Nitrogen, 6f), E(SoilChannel.Phosphorus, 2f), E(SoilChannel.Potassium, 5f));
        a.slowRelease = true;
        a.magicalBoost = 0.3f;
        list.Add(a);
        a = M("fish_fertilizer", "Fish Fertilizer", AmendmentKind.Fertilizer, 0.5f, "Fish, rotted down in water. Smells as you would expect.", 2f, new Color(0.35f, 0.32f, 0.22f),
            E(SoilChannel.Nitrogen, 7f), E(SoilChannel.Phosphorus, 4f), E(SoilChannel.Potassium, 2f), E(SoilChannel.Calcium, 5f), E(SoilChannel.Magnesium, 1f), E(SoilChannel.Iron, 0.3f),
            E(SoilChannel.Zinc, 0.1f), E(SoilChannel.Copper, 0.03f), E(SoilChannel.Manganese, 0.1f), E(SoilChannel.Boron, 0.02f), E(SoilChannel.Molybdenum, 0.002f));
        a.liquid = true;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("herbal_fertilizer", "Herbal Fertilizer", AmendmentKind.Fertilizer, 0.5f, "Leaves steeped in water until they break down.", 1.5f, new Color(0.25f, 0.32f, 0.15f),
            E(SoilChannel.Potassium, 9f), E(SoilChannel.Nitrogen, 3f), E(SoilChannel.Calcium, 4f));
        a.liquid = true;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("straw_mulch", "Straw Mulch", AmendmentKind.Mulch, 1f, "Loose straw, laid on the soil.", 0.5f, new Color(0.85f, 0.75f, 0.45f));
        a.mulch = 0.35f;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("alchemical_pesticide", "Alchemical Pesticide", AmendmentKind.Treatment, 0.05f, "A bitter spray. Insects die where it falls - all of them.", 6f, new Color(0.6f, 0.75f, 0.85f));
        a.pestKill = Pests(0.85f, 0.8f, 0.7f, 0.75f, 0.8f, 0.3f, 0.4f, 0.4f);
        a.plantHarm = 0.04f;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("herbal_spray", "Herbal Pest Spray", AmendmentKind.Treatment, 0.1f, "A spray of bitter herbs. Mild.", 2f, new Color(0.45f, 0.6f, 0.35f));
        a.pestKill = Pests(0.5f, 0.35f, 0.4f, 0.2f, 0.45f, 0f, 0.1f, 0.1f);
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("fungicide", "Fungicide", AmendmentKind.Treatment, 0.05f, "A copper and sulfur wash.", 4f, new Color(0.35f, 0.65f, 0.7f), E(SoilChannel.Copper, 0.4f), E(SoilChannel.Sulfur, 1f));
        a.diseaseCure = new[] { 0.6f, 0.1f, 0.05f, 0.6f, 0.5f, 0f };
        a.plantHarm = 0.01f;
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("soil_cleanser", "Soil Cleanser", AmendmentKind.Treatment, 0.3f, "An alchemical powder that kills what lives in the soil - the good with the bad.", 6f, new Color(0.85f, 0.85f, 0.95f),
            E(SoilChannel.Pathogens, -0.25f), E(SoilChannel.MagicEssence, -0.01f));
        a.diseaseCure = new[] { 0f, 0.3f, 0.2f, 0f, 0f, 0f };
        list.Add(a);
        a = M("ladybirds", "Ladybirds", AmendmentKind.Beneficial, 0.05f, "A box of hungry ladybirds.", 5f, new Color(0.85f, 0.15f, 0.1f));
        a.beneficialDays = 6f;
        a.beneficialSuppression = Pests(0.8f, 0.2f, 0.5f, 0f, 0.6f, 0f, 0f, 0f);
        a.needsWorkingIn = false;
        list.Add(a);
        a = M("slug_bait", "Slug Bait", AmendmentKind.Treatment, 0.05f, "Bran soaked in something slugs should not eat.", 2f, new Color(0.55f, 0.45f, 0.3f));
        a.pestKill = Pests(0f, 0f, 0f, 0f, 0f, 0f, 0.8f, 0.8f);
        a.needsWorkingIn = false;
        list.Add(a);
        return list;
    }

    // ---------------------------------------------------------------- instruments, tools, containers
    public static List<InstrumentPreset> Instruments() => new List<InstrumentPreset>
    {
        new InstrumentPreset { id = "ph_strip", name = "Crude pH Probe", kind = InstrumentKind.PhProbe, quality = 0.2f, value = 4f, description = "A probe of uncertain make. It tells sour from sweet, roughly." },
        new InstrumentPreset { id = "ph_probe", name = "pH Probe", kind = InstrumentKind.PhProbe, quality = 0.75f, value = 25f, description = "A reliable glass-tipped probe." },
        new InstrumentPreset { id = "soil_kit", name = "Alchemical Soil Analysis Kit", kind = InstrumentKind.SoilAnalysisKit, quality = 0.5f, value = 40f, description = "Reagents and a colour chart for the soil's nutrients." },
        new InstrumentPreset { id = "moisture_meter", name = "Soil Moisture Meter", kind = InstrumentKind.MoistureMeter, quality = 0.6f, value = 18f, description = "A needle that swings with the wetness of the soil." },
        new InstrumentPreset { id = "thermometer", name = "Alchemical Thermometer", kind = InstrumentKind.Thermometer, quality = 0.7f, value = 15f, description = "Measures the air and, pushed in, the soil." },
        new InstrumentPreset { id = "light_meter", name = "Light Meter", kind = InstrumentKind.LightMeter, quality = 0.6f, value = 22f, description = "A crystal that glows with the light falling on it." },
        new InstrumentPreset { id = "hygrometer", name = "Hygrometer", kind = InstrumentKind.Hygrometer, quality = 0.6f, value = 16f, description = "A hair hygrometer for the dampness of the air." },
        new InstrumentPreset { id = "compaction_tester", name = "Soil Compaction Tester", kind = InstrumentKind.CompactionTester, quality = 0.5f, value = 20f, description = "A spring-loaded rod pushed into the soil." },
        new InstrumentPreset { id = "handbook", name = "Botanical Handbook", kind = InstrumentKind.BotanicalHandbook, quality = 1f, value = 30f, weight = 1f, description = "A well-thumbed book of plants." },
        new InstrumentPreset { id = "advanced_analyzer", name = "Advanced Alchemical Analyzer", kind = InstrumentKind.AdvancedAnalyzer, quality = 0.9f, value = 250f, weight = 2f, description = "A brass apparatus that reads everything in a pinch of soil - magic included." },
        new InstrumentPreset { id = "depth_gauge", name = "Depth Gauge", kind = InstrumentKind.DepthGauge, quality = 0.9f, value = 3f, weight = 0.1f, description = "A notched stick for measuring holes." },
        new InstrumentPreset { id = "magnifier", name = "Magnifying Lens", kind = InstrumentKind.Magnifier, quality = 0.7f, value = 12f, weight = 0.1f, description = "Small things become plain." },
    };

    public static List<FarmToolPreset> Tools() => new List<FarmToolPreset>
    {
        new FarmToolPreset { id = "hoe", name = "Hoe", kind = FarmToolKind.Hoe, quality = 0.6f, speed = 1f, durability = 200, value = 8f, weight = 1.5f, description = "For breaking and working the soil." },
        new FarmToolPreset { id = "shovel", name = "Shovel", kind = FarmToolKind.Shovel, quality = 0.55f, speed = 1.4f, durability = 220, value = 9f, weight = 2f, description = "For digging deep and moving earth and stones." },
        new FarmToolPreset { id = "trowel", name = "Trowel", kind = FarmToolKind.Trowel, quality = 0.65f, speed = 0.8f, durability = 150, value = 4f, weight = 0.4f, description = "A small blade for careful planting holes." },
        new FarmToolPreset { id = "dibber", name = "Dibber", kind = FarmToolKind.Dibber, quality = 0.7f, speed = 1.2f, durability = 300, value = 2f, weight = 0.2f, description = "A pointed stick for quick, neat holes." },
        new FarmToolPreset { id = "shears", name = "Pruning Shears", kind = FarmToolKind.PruningShears, quality = 0.65f, speed = 1f, durability = 160, value = 10f, weight = 0.4f, description = "Sharp shears for clean cuts." },
        new FarmToolPreset { id = "sickle", name = "Sickle", kind = FarmToolKind.Sickle, quality = 0.6f, speed = 1f, durability = 180, value = 7f, weight = 0.6f, description = "A curved blade for cutting grain and stalks." },
        new FarmToolPreset { id = "brush", name = "Pollination Brush", kind = FarmToolKind.PollinationBrush, quality = 0.7f, speed = 1f, durability = 400, value = 2f, weight = 0.05f, description = "A soft brush for carrying pollen by hand." },
        new FarmToolPreset { id = "tweezers", name = "Tweezers", kind = FarmToolKind.Tweezers, quality = 0.7f, speed = 1f, durability = 400, value = 3f, weight = 0.05f, description = "For picking small pests off leaves." },
        new FarmToolPreset { id = "stakes", name = "Garden Stakes", kind = FarmToolKind.Stakes, quality = 0.6f, speed = 1f, durability = 1, value = 0.5f, weight = 0.3f, description = "A stake and twine for one climbing or heavy plant." },
    };

    public static List<ContainerPreset> Containers() => new List<ContainerPreset>
    {
        new ContainerPreset { id = "watering_can", name = "Watering Can", capacity = 6f, spread = 0.4f, flow = 0.8f, wetsLeaves = true, value = 7f, weight = 1f, description = "A can with a rose: a gentle, even shower." },
        new ContainerPreset { id = "bucket", name = "Bucket", capacity = 10f, spread = 0.65f, flow = 2.5f, wetsLeaves = true, value = 4f, weight = 1.2f, description = "Holds a lot. Pours it all at once." },
        new ContainerPreset { id = "flask", name = "Alchemist's Flask", capacity = 1f, spread = 0.12f, flow = 0.25f, wetsLeaves = false, value = 10f, weight = 0.3f, description = "For precious water, drop by drop at the roots." },
    };
}
