using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A plant's appetite for one nutrient, relative to an average plant (2 = twice as hungry for it).</summary>
[Serializable]
public struct NutrientNeed
{
    public Nutrient nutrient;
    [Min(0f)] public float multiplier;

    public NutrientNeed(Nutrient nutrient, float multiplier) { this.nutrient = nutrient; this.multiplier = multiplier; }
}

/// <summary>What a plant does to the plants around it.</summary>
[Serializable]
public class CompanionRule
{
    [Tooltip("The plants it acts on: a species id, 'category:Medicinal' (any of a category) or * (every other plant).")]
    public string target = "*";
    public CompanionEffect effect = CompanionEffect.Growth;
    [Tooltip("Growth: a multiplier (1.15 = grows 15% better, 0.85 = worse). Repel Pests, Attract Pollinators, Inhibit: strength 0-1.")]
    public float strength = 1.1f;
    [Tooltip("How far it reaches, metres.")]
    [Min(0.1f)] public float radius = 1f;
    [Tooltip("The pests kept away (Repel Pests).")]
    public List<PestKind> pests = new List<PestKind>();

    public CompanionRule() { }

    public CompanionRule(string target, CompanionEffect effect, float strength, float radius, params PestKind[] pests)
    {
        this.target = target; this.effect = effect; this.strength = strength; this.radius = radius;
        this.pests = new List<PestKind>(pests ?? new PestKind[0]);
    }

    public bool Applies(PlantSpeciesData self, PlantSpeciesData other)
    {
        if (other == null || self == null) return false;
        if (string.IsNullOrEmpty(target) || target == "*") return other.id != self.id;
        if (target.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            return string.Equals(target.Substring(9).Trim(), other.category.ToString(), StringComparison.OrdinalIgnoreCase);
        return string.Equals(target, other.id, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>What a plant gives and how its produce develops (each piece on its own timer).</summary>
[Serializable]
public class ProduceSpec
{
    public ProducePart part = ProducePart.Fruit;
    [Tooltip("Name of a piece (the item).")]
    public string name = "Fruit";
    [TextArea(2, 4)] public string description = "";
    public PlantUses uses = PlantUses.Food;
    [Tooltip("Pieces in a flush: what a poorly grown plant gives, and a well grown one.")]
    [Min(0)] public int countMin = 2;
    [Min(1)] public int countMax = 6;
    [Tooltip("Game days between flushes of flowers while the season lasts (0 = one flush).")]
    [Min(0f)] public float flushIntervalDays;
    [Tooltip("Most flushes in a season (perennials: each year).")]
    [Min(1)] public int maxFlushes = 1;
    [Tooltip("Game days each stage of a piece lasts in good conditions: Bud (flower), Set, Early, Immature, Developing, Mature, Ripe, Overripe, then Rotten (until it falls).")]
    public float[] stageDays = { 1.5f, 0.5f, 1f, 1f, 1.5f, 1f, 1.5f, 1.5f, 1f };
    public Pollination pollination = Pollination.Insect;
    [Tooltip("Harvesting takes the whole plant (roots, heads, grain cut down).")]
    public bool wholePlant;
    [Tooltip("Leaves: cut pieces grow back.")]
    public bool regrows;
    [Tooltip("Seeds a piece gives (from a ripe piece; fewer from an unripe one).")]
    [Min(0)] public int seedsPerPiece = 6;
    [Tooltip("Value of one piece of top quality.")]
    [Min(0f)] public float value = 3f;
    [Min(0.001f)] public float weight = 0.15f;
    [Tooltip("Food: how much a piece of top quality sates (hunger points).")]
    [Min(0f)] public float nutrition = 8f;
    [Tooltip("Medicinal, alchemical and magical strength of a perfect piece (0-1).")]
    [Range(0f, 1f)] public float potency = 0.3f;
    public Color colorYoung = new Color(0.35f, 0.65f, 0.25f);
    public Color colorRipe = new Color(0.85f, 0.15f, 0.1f);
    public Color colorOverripe = new Color(0.45f, 0.08f, 0.06f);
    public Color colorRotten = new Color(0.28f, 0.22f, 0.12f);
    [Tooltip("Size of a full-grown piece, metres.")]
    [Min(0.005f)] public float pieceSize = 0.06f;
    [Tooltip("An alchemy ingredient (by name, e.g. 'Chamomile') whose substances a piece gives in a vessel. Empty = none.")]
    public string alchemyIngredient = "";

    public float StageDays(ProduceStage s)
    {
        int i = (int)s;
        return stageDays != null && i < stageDays.Length ? Mathf.Max(0.05f, stageDays[i]) : 1f;
    }

    /// <summary>Is <paramref name="s"/> a stage the piece can be picked at for its use (not a flower or a set fruit).</summary>
    public bool Pickable(ProduceStage s) => part == ProducePart.Flower ? s >= ProduceStage.Early : s >= ProduceStage.Immature;

    /// <summary>The colour of a piece at a stage (blending between the young, ripe, overripe and rotten colours).</summary>
    public Color ColorAt(ProduceStage s, float progress)
    {
        switch (s)
        {
            case ProduceStage.Bud:
            case ProduceStage.Set:
            case ProduceStage.Early:
            case ProduceStage.Immature: return colorYoung;
            case ProduceStage.Developing: return Color.Lerp(colorYoung, colorRipe, 0.15f + 0.25f * progress);
            case ProduceStage.Mature: return Color.Lerp(colorYoung, colorRipe, 0.45f + 0.4f * progress);
            case ProduceStage.Ripe: return Color.Lerp(colorRipe, colorOverripe, 0.15f * progress);
            case ProduceStage.Overripe: return Color.Lerp(colorRipe, colorOverripe, 0.3f + 0.7f * progress);
            default: return Color.Lerp(colorOverripe, colorRotten, 0.4f + 0.6f * progress);
        }
    }
}

/// <summary>How a plant is drawn.</summary>
[Serializable]
public class PlantLook
{
    public PlantShape shape = PlantShape.Herb;
    [Tooltip("Height of a full-grown plant, metres.")]
    [Min(0.02f)] public float height = 0.5f;
    [Tooltip("Width of a full-grown plant, metres.")]
    [Min(0.02f)] public float width = 0.4f;
    public Color leaf = new Color(0.24f, 0.52f, 0.2f);
    public Color stem = new Color(0.36f, 0.44f, 0.2f);
    public Color flower = new Color(1f, 0.93f, 0.45f);
    [Tooltip("A faint glow of magical plants (alpha = strength).")]
    public Color glow = new Color(0f, 0f, 0f, 0f);
}

/// <summary>
/// A plant species: everything about how it lives, what it needs at each stage and what it gives. The numbers are its
/// true nature: no window ever shows them to the player, who learns them by growing it (the <see cref="handbook"/> text is
/// what the Botanical Handbook says of it, written by the designer, and may be vague, incomplete or empty).
/// </summary>
[Serializable]
public class PlantSpeciesData
{
    [Header("Identity")]
    public string id = "plant";
    public string name = "Plant";
    public PlantCategory category = PlantCategory.Common;
    [Tooltip("What the Botanical Handbook says (leave empty for plants the books do not know).")]
    [TextArea(3, 8)] public string handbook = "";

    [Header("Life")]
    public PlantLifeCycle lifeCycle = PlantLifeCycle.Annual;
    [Tooltip("Game days from sowing to the shoot breaking the surface, in good conditions.")]
    [Min(0.1f)] public float germinationDays = 2f;
    [Min(0.1f)] public float seedlingDays = 2f;
    [Min(0.1f)] public float juvenileDays = 3f;
    [Min(0.1f)] public float vegetativeDays = 3f;
    [Tooltip("Game days the flowers keep opening.")]
    [Min(0.1f)] public float floweringDays = 2f;
    [Tooltip("Game days from the end of production (or an annual's season) to death.")]
    [Min(0.1f)] public float senescenceDays = 3f;
    [Tooltip("Perennials: years before the first flowers.")]
    [Min(0)] public int yearsToFruit;
    [Tooltip("Perennials: years to reach full size.")]
    [Min(0f)] public float yearsToFullSize = 1f;
    [Tooltip("Perennials: years it lives (0 = indefinitely).")]
    [Min(0f)] public float lifespanYears;

    [Header("Soil")]
    [Tooltip("How well it does in each soil type (Sandy, Clay, Rocky, Organic-Rich, Peat, Volcanic, Swamp, Magical), 0-1.")]
    public float[] soilAffinity = { 0.7f, 0.6f, 0.4f, 1f, 0.5f, 0.8f, 0.3f, 0.8f };
    [Tooltip("pH it survives (min), thrives in (low-high), survives (max).")]
    public float phMin = 5f, phLow = 6f, phHigh = 7.2f, phMax = 8.2f;
    [Tooltip("How hungry it is: 0.5 a light feeder, 1 average, 1.6 a heavy feeder.")]
    [Min(0f)] public float nutrientDemand = 1f;
    [Tooltip("Nutrients it needs more or less of than others.")]
    public List<NutrientNeed> needs = new List<NutrientNeed>();
    [Tooltip("Salinity it bears without harm, dS/m.")]
    [Min(0f)] public float salinityTolerance = 2f;
    [Tooltip("Compaction its roots push through (0-1).")]
    [Range(0f, 1f)] public float compactionTolerance = 0.5f;
    [Tooltip("Worked soil it needs to come up well (0-1).")]
    [Range(0f, 1f)] public float minTilth = 0.3f;
    [Tooltip("Magical essence it needs in the soil (0 = none).")]
    [Range(0f, 1f)] public float magicNeed;
    [Tooltip("Fixes nitrogen from the air into the soil around its roots (legumes).")]
    public bool nitrogenFixer;

    [Header("Water")]
    [Tooltip("Water in reach it likes: 0 = wilting point, 1 = soil holding all it can after draining, above 1 = waterlogged.")]
    public float waterLow = 0.35f, waterHigh = 0.95f;
    [Tooltip("How well it bears drought (0-1).")]
    [Range(0f, 1f)] public float droughtTolerance = 0.3f;
    [Tooltip("How well it bears waterlogged roots (0-1).")]
    [Range(0f, 1f)] public float waterlogTolerance = 0.2f;
    [Tooltip("How much water it drinks, relative to an average plant of its size.")]
    [Min(0f)] public float waterUse = 1f;
    [Tooltip("How much salty or dirty water harms it (0-1).")]
    [Range(0f, 1f)] public float waterQualitySensitivity = 0.5f;

    [Header("Temperature (°C)")]
    [Tooltip("Survives (min), thrives (low-high), survives (max).")]
    public float tempMin = 2f, tempLow = 15f, tempHigh = 26f, tempMax = 35f;
    [Tooltip("Soil temperature its seeds germinate between.")]
    public float germinationMin = 8f, germinationMax = 32f;
    [Tooltip("How badly frost hurts it (0 hardy - 1 dies at the first frost).")]
    [Range(0f, 1f)] public float frostSensitivity = 0.8f;
    [Tooltip("How badly sudden changes of temperature hurt it (0-1).")]
    [Range(0f, 1f)] public float shockSensitivity = 0.3f;

    [Header("Air and light")]
    [Range(0f, 1f)] public float humidityLow = 0.35f, humidityHigh = 0.8f;
    [Tooltip("Light it likes (1 = full sun, 0.5 = partial shade, 0.2 = shade).")]
    [Range(0f, 1f)] public float lightLow = 0.55f, lightHigh = 1f;
    [Tooltip("How much light beyond what it likes scorches it (0 = never).")]
    [Range(0f, 1f)] public float lightExcess;
    public PhotoperiodType photoperiod = PhotoperiodType.Neutral;
    [Tooltip("Day length deciding flowering (hours).")]
    [Range(6f, 18f)] public float criticalDayLength = 13f;

    [Header("Seasons")]
    [Tooltip("Seasons its seeds germinate in (none = any).")]
    public SeasonMask sowingSeasons = SeasonMask.Spring | SeasonMask.Summer;
    [Tooltip("Seasons it flowers in (none = any).")]
    public SeasonMask floweringSeasons = SeasonMask.None;
    [Tooltip("Seasons it rests in (perennials; none = never).")]
    public SeasonMask dormancySeasons = SeasonMask.None;
    [Tooltip("Hours below 7 °C it needs over the cold season before it flowers (0 = none).")]
    [Min(0f)] public float chillHours;

    [Header("Moon and time")]
    [Tooltip("How much the moon matters to it (0 = not at all).")]
    [Range(0f, 1f)] public float lunarStrength;
    [Tooltip("Germination under each phase (New, Waxing, Full, Waning): above 1 better.")]
    public float[] moonGermination = { 1f, 1f, 1f, 1f };
    [Tooltip("Potency of what is picked under each phase.")]
    public float[] moonPotency = { 1f, 1f, 1f, 1f };
    public PlantingTime plantingTime = PlantingTime.Any;
    [Tooltip("How much planting at the right time of day matters (0-1).")]
    [Range(0f, 1f)] public float plantingTimeStrength;

    [Header("Planting")]
    [Tooltip("Best sowing depth, cm.")]
    [Min(0f)] public float depthCm = 2f;
    [Tooltip("How far from it a seed still comes up well, cm.")]
    [Min(0.1f)] public float depthToleranceCm = 1.5f;
    [Tooltip("Needs light to germinate: sow on the surface, do not cover.")]
    public bool surfaceSown;
    [Tooltip("Room each plant needs, metres.")]
    [Min(0.02f)] public float spacing = 0.3f;
    [Tooltip("Room between rows, metres.")]
    [Min(0.02f)] public float rowSpacing = 0.45f;
    [Tooltip("Reach of the roots of a full-grown plant, metres.")]
    [Min(0.02f)] public float rootRadius = 0.2f;
    [Tooltip("Needs a stake or trellis (climbers, heavy croppers).")]
    public bool needsSupport;

    [Header("Neighbours")]
    [Tooltip("What it does to plants around it.")]
    public List<CompanionRule> companions = new List<CompanionRule>();
    [Tooltip("Plants of the same group can pollinate each other (varieties, hybrids).")]
    public string crossGroup = "";

    [Header("Produce")]
    public ProduceSpec produce = new ProduceSpec();

    [Header("Pests and diseases")]
    [Tooltip("How much each pest likes it (Aphids, Caterpillars, Mites, Beetles, Sap-suckers, Root parasites, Slugs, Snails), 0-2.")]
    public float[] pestSusceptibility = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    [Tooltip("How prone it is to each disease (Fungal, Root rot, Wilt, Leaf disease, Bacterial, Viral), 0-2.")]
    public float[] diseaseSusceptibility = { 1f, 1f, 1f, 1f, 1f, 1f };

    [Header("Pruning")]
    public PruneNeed pruning = PruneNeed.None;
    [Tooltip("Seasons it is best pruned in.")]
    public SeasonMask pruneSeasons = SeasonMask.Winter;

    [Header("Genetics")]
    [Tooltip("How much its seeds differ from the parent (1 = normal).")]
    [Range(0f, 3f)] public float geneticVariation = 1f;

    [Header("Look")]
    public PlantLook look = new PlantLook();

    [Header("Seeds")]
    [Min(0f)] public float seedValue = 1f;
    [Tooltip("Copies in a stack of seeds.")]
    [Min(1)] public int seedStack = 50;

    // ---------------------------------------------------------------- reading
    public float SoilAffinity(SoilType t) => soilAffinity != null && (int)t < soilAffinity.Length ? Mathf.Clamp01(soilAffinity[(int)t]) : 0.6f;
    public float PestSusceptibility(PestKind p) => pestSusceptibility != null && (int)p < pestSusceptibility.Length ? Mathf.Max(0f, pestSusceptibility[(int)p]) : 1f;
    public float DiseaseSusceptibility(DiseaseKind d) => diseaseSusceptibility != null && (int)d < diseaseSusceptibility.Length ? Mathf.Max(0f, diseaseSusceptibility[(int)d]) : 1f;
    public float MoonGermination(MoonPhase m) => moonGermination != null && (int)m < moonGermination.Length ? Mathf.Max(0f, moonGermination[(int)m]) : 1f;
    public float MoonPotency(MoonPhase m) => moonPotency != null && (int)m < moonPotency.Length ? Mathf.Max(0f, moonPotency[(int)m]) : 1f;
    public bool Perennial => lifeCycle == PlantLifeCycle.Perennial;
    public ProduceSpec Produce => produce ??= new ProduceSpec();
    public PlantLook Look => look ??= new PlantLook();

    /// <summary>Its appetite for a nutrient relative to an average plant (demand × the nutrient's own multiplier).</summary>
    public float Need(Nutrient n)
    {
        float m = 1f;
        if (needs != null)
            foreach (NutrientNeed x in needs)
                if (x.nutrient == n) m *= x.multiplier;
        if (nitrogenFixer && n == Nutrient.Nitrogen) m *= 0.35f;
        return Mathf.Max(0f, nutrientDemand) * m;
    }

    /// <summary>Game days of a growth stage in good conditions.</summary>
    public float StageDays(PlantStage s)
    {
        switch (s)
        {
            case PlantStage.Seed:
            case PlantStage.Germination: return germinationDays;
            case PlantStage.Seedling: return seedlingDays;
            case PlantStage.Juvenile: return juvenileDays;
            case PlantStage.Vegetative: return vegetativeDays;
            case PlantStage.Flowering: return floweringDays;
            case PlantStage.Senescence: return senescenceDays;
            default: return vegetativeDays;
        }
    }

    public PlantSpeciesData Clone()
    {
        var c = (PlantSpeciesData)MemberwiseClone();
        c.soilAffinity = soilAffinity != null ? (float[])soilAffinity.Clone() : null;
        c.needs = needs != null ? new List<NutrientNeed>(needs) : new List<NutrientNeed>();
        c.moonGermination = moonGermination != null ? (float[])moonGermination.Clone() : null;
        c.moonPotency = moonPotency != null ? (float[])moonPotency.Clone() : null;
        c.companions = new List<CompanionRule>();
        if (companions != null)
            foreach (CompanionRule r in companions)
                if (r != null) c.companions.Add(new CompanionRule(r.target, r.effect, r.strength, r.radius, r.pests != null ? r.pests.ToArray() : null));
        c.pestSusceptibility = pestSusceptibility != null ? (float[])pestSusceptibility.Clone() : null;
        c.diseaseSusceptibility = diseaseSusceptibility != null ? (float[])diseaseSusceptibility.Clone() : null;
        c.produce = (ProduceSpec)Produce.MemberwiseCloneProduce();
        c.look = (PlantLook)Look.MemberwiseCloneLook();
        return c;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        string n = string.IsNullOrWhiteSpace(name) ? id : name;
        if (string.IsNullOrWhiteSpace(id)) errors.Add("No id.");
        else if (id.IndexOfAny(new[] { '/', ':', ' ' }) >= 0) errors.Add($"{n}: the id may not contain spaces, '/' or ':'.");
        if (!(phMin <= phLow && phLow <= phHigh && phHigh <= phMax)) errors.Add($"{n}: the pH range must go min ≤ low ≤ high ≤ max.");
        if (!(tempMin <= tempLow && tempLow <= tempHigh && tempHigh <= tempMax)) errors.Add($"{n}: the temperatures must go min ≤ low ≤ high ≤ max.");
        if (germinationMin >= germinationMax) errors.Add($"{n}: germination needs a warmer maximum than minimum.");
        if (waterLow >= waterHigh) errors.Add($"{n}: the water it likes needs Low below High.");
        if (lightLow > lightHigh) errors.Add($"{n}: the light it likes needs Low at or below High.");
        if (humidityLow > humidityHigh) errors.Add($"{n}: the humidity it likes needs Low at or below High.");
        if (soilAffinity == null || soilAffinity.Length < SoilTypes.Count) warnings.Add($"{n}: Soil Affinity should have {SoilTypes.Count} values.");
        if (pestSusceptibility == null || pestSusceptibility.Length < 8) warnings.Add($"{n}: Pest Susceptibility should have 8 values.");
        if (diseaseSusceptibility == null || diseaseSusceptibility.Length < 6) warnings.Add($"{n}: Disease Susceptibility should have 6 values.");
        ProduceSpec p = Produce;
        if (p.countMax < Math.Max(1, p.countMin)) errors.Add($"{n}: Count Max must be at least Count Min.");
        if (p.stageDays == null || p.stageDays.Length < 9) errors.Add($"{n}: the produce needs 9 stage durations.");
        if (p.wholePlant && p.countMax > 1 && p.part != ProducePart.Grain) warnings.Add($"{n}: whole-plant produce usually gives one piece (Count Max 1).");
        if (spacing < rootRadius * 0.8f) warnings.Add($"{n}: its spacing is smaller than its roots' reach (plants will crowd at that spacing).");
        if (lifeCycle != PlantLifeCycle.Perennial && dormancySeasons != SeasonMask.None) warnings.Add($"{n}: only perennials rest (Dormancy Seasons is ignored).");
        if (p.pollination == Pollination.Hand && p.part == ProducePart.Fruit) warnings.Add($"{n}: it only sets fruit pollinated by hand.");
    }
}

/// <summary>Copies used by <see cref="PlantSpeciesData.Clone"/> (MemberwiseClone is protected).</summary>
internal static class AgriCloneExtensions
{
    public static object MemberwiseCloneProduce(this ProduceSpec p)
    {
        var c = new ProduceSpec
        {
            part = p.part, name = p.name, description = p.description, uses = p.uses, countMin = p.countMin, countMax = p.countMax,
            flushIntervalDays = p.flushIntervalDays, maxFlushes = p.maxFlushes, stageDays = p.stageDays != null ? (float[])p.stageDays.Clone() : null,
            pollination = p.pollination, wholePlant = p.wholePlant, regrows = p.regrows, seedsPerPiece = p.seedsPerPiece, value = p.value,
            weight = p.weight, nutrition = p.nutrition, potency = p.potency, colorYoung = p.colorYoung, colorRipe = p.colorRipe,
            colorOverripe = p.colorOverripe, colorRotten = p.colorRotten, pieceSize = p.pieceSize, alchemyIngredient = p.alchemyIngredient,
        };
        return c;
    }

    public static object MemberwiseCloneLook(this PlantLook l) =>
        new PlantLook { shape = l.shape, height = l.height, width = l.width, leaf = l.leaf, stem = l.stem, flower = l.flower, glow = l.glow };
}
