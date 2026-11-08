using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The vocabulary of agriculture: soils, plants, their parts and stages, pests, seasons, tools, skills.
// Every enum here is serialized as numbers: only add new values at the END.

/// <summary>The kinds of soil a terrain can have. Each has its own texture, chemistry and colour (see <see cref="SoilTypes"/>).</summary>
public enum SoilType
{
    /// <summary>Light, pale, gritty: drains fast, warms fast, holds little water or food.</summary>
    Sandy,
    /// <summary>Heavy, sticky, rich in minerals: holds water and nutrients, drains slowly, packs hard.</summary>
    Clay,
    /// <summary>Thin soil full of stones: drains well, holds little, hard to dig.</summary>
    Rocky,
    /// <summary>Dark, crumbly, full of life: the gardener's soil.</summary>
    OrganicRich,
    /// <summary>Spongy, black, acidic, almost all organic matter: holds water like a sponge.</summary>
    Peat,
    /// <summary>Dark ash soil: warm, rich in potassium, iron and magnesium, but it locks phosphorus away.</summary>
    Volcanic,
    /// <summary>Waterlogged, sour-smelling mud: little air, sulfur and iron, disease in the water.</summary>
    Swamp,
    /// <summary>Soil steeped in magical essence: some plants grow nowhere else.</summary>
    Magical,
}

/// <summary>
/// Everything a soil cell holds: the properties a designer sets (pH to Tilth, see <see cref="SoilChannels.Authored"/>) and
/// the hidden state of the simulation after them. Nutrients are available concentrations in mg/kg (ppm), organic matter in
/// %, salinity in dS/m; the rest 0-1.
/// </summary>
public enum SoilChannel
{
    Ph,
    Nitrogen,
    Phosphorus,
    Potassium,
    Calcium,
    Magnesium,
    Sulfur,
    Iron,
    Zinc,
    Copper,
    Manganese,
    Boron,
    Molybdenum,
    OrganicMatter,
    Salinity,
    WaterRetention,
    Drainage,
    Compaction,
    Moisture,
    MagicEssence,
    Pathogens,
    Stones,
    Tilth,
    // ---- hidden state (not authored)
    /// <summary>Nitrogen held in organic matter and slow fertilizers, released as the soil warms and stays moist.</summary>
    SlowNitrogen,
    SlowPhosphorus,
    SlowPotassium,
    /// <summary>pH change still under way (limestone, sulfur and ash react over days).</summary>
    PendingPh,
    /// <summary>Mulch lying on the soil (0-1): keeps the water in, rots into organic matter.</summary>
    Mulch,
}

/// <summary>The twelve plant nutrients: <c>(SoilChannel)(nutrient + 1)</c> is the soil channel holding it.</summary>
public enum Nutrient
{
    Nitrogen,
    Phosphorus,
    Potassium,
    Calcium,
    Magnesium,
    Sulfur,
    Iron,
    Zinc,
    Copper,
    Manganese,
    Boron,
    Molybdenum,
}

/// <summary>What kind of plant it is, for menus, merchants and the handbook.</summary>
public enum PlantCategory
{
    Common,
    Medicinal,
    Alchemical,
    FruitBearing,
    Ornamental,
    Magical,
    Rare,
}

/// <summary>What a plant's produce is good for.</summary>
[Flags]
public enum PlantUses
{
    None = 0,
    Food = 1,
    Medicinal = 2,
    Alchemical = 4,
    Ornamental = 8,
    Magical = 16,
}

/// <summary>How a plant lives and produces.</summary>
public enum PlantLifeCycle
{
    /// <summary>Gives its produce once (a cabbage, a carrot, a head of grain) and is done.</summary>
    SingleHarvest,
    /// <summary>Germinates, grows, produces (possibly several flushes) and dies within its season.</summary>
    Annual,
    /// <summary>Lives for years and produces again and again, resting when its season is over.</summary>
    Perennial,
}

/// <summary>
/// A plant's development. Fruit Development to Overripe describe a fruiting plant by its produce (the most advanced of it);
/// a plant whose produce is itself (a root, a head) goes through them with that one piece.
/// </summary>
public enum PlantStage
{
    Seed,
    Germination,
    Seedling,
    Juvenile,
    Vegetative,
    Flowering,
    FruitDevelopment,
    FruitMaturation,
    HarvestReady,
    Overripe,
    Senescence,
    Dead,
}

/// <summary>Which part of the plant is harvested.</summary>
public enum ProducePart
{
    /// <summary>Fruits from pollinated flowers (tomatoes, apples, berries, pods).</summary>
    Fruit,
    /// <summary>Leaves, cut as they come (herbs, greens).</summary>
    Leaf,
    /// <summary>One root or tuber per plant, pulled up (carrots, mandrakes).</summary>
    Root,
    /// <summary>The flowers themselves (chamomile, roses).</summary>
    Flower,
    /// <summary>Heads of grain or seed (wheat), cut when dry.</summary>
    Grain,
}

/// <summary>
/// The stages of one piece of produce. For fruit: flower, pollinated flower, early, immature, developing, mature, fully
/// ripe, overripe, rotten (<see cref="AgriText.Stage"/> names them for each part).
/// </summary>
public enum ProduceStage
{
    Bud,
    Set,
    Early,
    Immature,
    Developing,
    Mature,
    Ripe,
    Overripe,
    Rotten,
}

/// <summary>How flowers are pollinated.</summary>
public enum Pollination
{
    /// <summary>Each flower pollinates itself.</summary>
    Self,
    /// <summary>Bees and other insects: warm, dry, calm weather, flowers nearby help.</summary>
    Insect,
    /// <summary>The wind: dry, breezy days.</summary>
    Wind,
    /// <summary>Only by hand, with a brush.</summary>
    Hand,
    /// <summary>No pollination needed.</summary>
    None,
}

public enum PestKind
{
    Aphids,
    Caterpillars,
    Mites,
    Beetles,
    SapSuckers,
    RootParasites,
    Slugs,
    Snails,
}

public enum DiseaseKind
{
    Fungal,
    RootRot,
    Wilt,
    LeafSpot,
    Bacterial,
    Viral,
}

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

[Flags]
public enum SeasonMask
{
    None = 0,
    Spring = 1,
    Summer = 2,
    Autumn = 4,
    Winter = 8,
    All = 15,
}

public enum MoonPhase
{
    New,
    Waxing,
    Full,
    Waning,
}

/// <summary>When in the day a plant prefers to be planted.</summary>
public enum PlantingTime
{
    Any,
    Day,
    Night,
}

/// <summary>How day length decides flowering.</summary>
public enum PhotoperiodType
{
    /// <summary>Flowers whatever the length of the day.</summary>
    Neutral,
    /// <summary>Flowers only when days are at least the critical length.</summary>
    LongDay,
    /// <summary>Flowers only when days are at most the critical length.</summary>
    ShortDay,
}

/// <summary>How much pruning a plant wants.</summary>
public enum PruneNeed
{
    None,
    Light,
    Regular,
    Heavy,
}

/// <summary>What a cut aims at.</summary>
public enum PruneKind
{
    /// <summary>Cut out dead wood.</summary>
    DeadWood,
    /// <summary>Cut out diseased parts.</summary>
    Diseased,
    /// <summary>Thin the crown: light and air reach inside.</summary>
    Thin,
    /// <summary>Shape it and keep it small.</summary>
    Shape,
    /// <summary>Cut it back hard: new growth.</summary>
    CutBack,
    /// <summary>Pinch out tips and flower buds (herbs keep making leaves).</summary>
    Pinch,
}

/// <summary>How a neighbour affects a plant.</summary>
public enum CompanionEffect
{
    /// <summary>Grows better (or worse, with a strength below 1) next to it.</summary>
    Growth,
    /// <summary>Keeps pests away (the rule's pests).</summary>
    RepelPests,
    /// <summary>Brings pollinators.</summary>
    AttractPollinators,
    /// <summary>Holds it back (allelopathy, a greedy competitor).</summary>
    Inhibit,
}

/// <summary>What a material does to the soil or the plants (for menus and grouping; the effects are its numbers).</summary>
public enum AmendmentKind
{
    /// <summary>Changes the texture: sand, clay, gravel.</summary>
    Texture,
    /// <summary>Changes the pH: limestone, sulfur.</summary>
    PhModifier,
    /// <summary>Feeds the plants.</summary>
    Fertilizer,
    /// <summary>Organic matter: compost, manure, peat.</summary>
    Organic,
    /// <summary>Works on pests or diseases.</summary>
    Treatment,
    /// <summary>Living helpers: ladybirds, lacewings.</summary>
    Beneficial,
    /// <summary>Covers the soil: keeps water in.</summary>
    Mulch,
}

public enum InstrumentKind
{
    PhProbe,
    SoilAnalysisKit,
    MoistureMeter,
    Thermometer,
    LightMeter,
    Hygrometer,
    CompactionTester,
    BotanicalHandbook,
    AdvancedAnalyzer,
    DepthGauge,
    Magnifier,
}

public enum FarmToolKind
{
    Hoe,
    Shovel,
    Trowel,
    Dibber,
    PruningShears,
    Sickle,
    PollinationBrush,
    Tweezers,
    Stakes,
}

public enum WaterSourceKind
{
    Rain,
    River,
    Well,
    Lake,
    Swamp,
    Spring,
    Alchemical,
    Magical,
}

/// <summary>The agricultural skills. Serialized as numbers: add at the END.</summary>
public enum AgriSkill
{
    /// <summary>Working the soil, planting, watering and feeding evenly.</summary>
    Agriculture,
    /// <summary>Seeing what is there: ripeness, pests, the state of a plant.</summary>
    Botany,
    /// <summary>Picking herbs, flowers and roots without bruising them.</summary>
    Herbalism,
    /// <summary>Treating pests and diseases, pruning cleanly.</summary>
    PlantMedicine,
    /// <summary>Reading soil analyses and using alchemical materials.</summary>
    Alchemy,
    /// <summary>Growing magical plants.</summary>
    MagicalCultivation,
    /// <summary>Taking seeds, pollinating by hand, breeding varieties.</summary>
    SeedBreeding,
}

/// <summary>Everything a player can do to a soil field.</summary>
public enum FarmActionKind
{
    Inspect,
    Till,
    ClearStones,
    Dig,
    Sow,
    Cover,
    Water,
    Amend,
    Measure,
    Prune,
    Pollinate,
    CollectPollen,
    Harvest,
    CollectSeeds,
    RemovePests,
    Treat,
    Uproot,
    Support,
    Label,
    HarvestPlant,
}

/// <summary>How a plant is drawn.</summary>
public enum PlantShape
{
    /// <summary>A small leafy herb.</summary>
    Herb,
    /// <summary>A rounded shrub.</summary>
    Bush,
    /// <summary>A climber on a stake or trellis.</summary>
    Vine,
    /// <summary>A trunk and a crown.</summary>
    Tree,
    /// <summary>Thin stalks and heads.</summary>
    Grain,
    /// <summary>A tuft of leaves over a root.</summary>
    RootCrop,
    /// <summary>A low rosette or head of leaves (cabbage, lettuce).</summary>
    Rosette,
    /// <summary>A stem with showy flowers.</summary>
    Flower,
}

// ====================================================================================================================

/// <summary>Index helpers for <see cref="SoilChannel"/>.</summary>
public static class SoilChannels
{
    /// <summary>Every channel.</summary>
    public const int Count = (int)SoilChannel.Mulch + 1;
    /// <summary>The channels a designer sets (pH to Tilth).</summary>
    public const int Authored = (int)SoilChannel.Tilth + 1;
    public const int NutrientCount = 12;

    public static SoilChannel Of(Nutrient n) => (SoilChannel)((int)n + 1);
    public static bool IsNutrient(SoilChannel c) => c >= SoilChannel.Nitrogen && c <= SoilChannel.Molybdenum;
    public static Nutrient NutrientOf(SoilChannel c) => (Nutrient)((int)c - 1);

    /// <summary>The range a channel can take (also the range of the editor's sliders).</summary>
    public static Vector2 Range(SoilChannel c)
    {
        switch (c)
        {
            case SoilChannel.Ph: return new Vector2(3f, 10f);
            case SoilChannel.Nitrogen: return new Vector2(0f, 400f);
            case SoilChannel.Phosphorus: return new Vector2(0f, 250f);
            case SoilChannel.Potassium: return new Vector2(0f, 1000f);
            case SoilChannel.Calcium: return new Vector2(0f, 6000f);
            case SoilChannel.Magnesium: return new Vector2(0f, 1200f);
            case SoilChannel.Sulfur: return new Vector2(0f, 200f);
            case SoilChannel.Iron: return new Vector2(0f, 150f);
            case SoilChannel.Zinc: return new Vector2(0f, 30f);
            case SoilChannel.Copper: return new Vector2(0f, 20f);
            case SoilChannel.Manganese: return new Vector2(0f, 150f);
            case SoilChannel.Boron: return new Vector2(0f, 10f);
            case SoilChannel.Molybdenum: return new Vector2(0f, 2f);
            case SoilChannel.OrganicMatter: return new Vector2(0f, 95f);
            case SoilChannel.Salinity: return new Vector2(0f, 20f);
            case SoilChannel.SlowNitrogen: return new Vector2(0f, 2000f);
            case SoilChannel.SlowPhosphorus: return new Vector2(0f, 1000f);
            case SoilChannel.SlowPotassium: return new Vector2(0f, 2000f);
            case SoilChannel.PendingPh: return new Vector2(-4f, 4f);
            default: return new Vector2(0f, 1f);
        }
    }

    public static float Clamp(SoilChannel c, float v)
    {
        Vector2 r = Range(c);
        return v < r.x ? r.x : v > r.y ? r.y : v;
    }

    /// <summary>"pH", "Nitrogen (N)", "Organic matter"...</summary>
    public static string Name(SoilChannel c)
    {
        switch (c)
        {
            case SoilChannel.Ph: return "pH";
            case SoilChannel.Nitrogen: return "Nitrogen (N)";
            case SoilChannel.Phosphorus: return "Phosphorus (P)";
            case SoilChannel.Potassium: return "Potassium (K)";
            case SoilChannel.Calcium: return "Calcium (Ca)";
            case SoilChannel.Magnesium: return "Magnesium (Mg)";
            case SoilChannel.Sulfur: return "Sulfur (S)";
            case SoilChannel.Iron: return "Iron (Fe)";
            case SoilChannel.Zinc: return "Zinc (Zn)";
            case SoilChannel.Copper: return "Copper (Cu)";
            case SoilChannel.Manganese: return "Manganese (Mn)";
            case SoilChannel.Boron: return "Boron (B)";
            case SoilChannel.Molybdenum: return "Molybdenum (Mo)";
            case SoilChannel.OrganicMatter: return "Organic matter";
            case SoilChannel.Salinity: return "Salinity";
            case SoilChannel.WaterRetention: return "Water retention";
            case SoilChannel.Drainage: return "Drainage";
            case SoilChannel.Compaction: return "Compaction";
            case SoilChannel.Moisture: return "Moisture";
            case SoilChannel.MagicEssence: return "Magical essence";
            case SoilChannel.Pathogens: return "Pathogens";
            case SoilChannel.Stones: return "Stones";
            case SoilChannel.Tilth: return "Tilth (worked)";
            case SoilChannel.SlowNitrogen: return "Slow nitrogen";
            case SoilChannel.SlowPhosphorus: return "Slow phosphorus";
            case SoilChannel.SlowPotassium: return "Slow potassium";
            case SoilChannel.PendingPh: return "pH change under way";
            default: return c.ToString();
        }
    }

    /// <summary>The unit a channel is measured in ("ppm", "%", "dS/m", "" for 0-1 values).</summary>
    public static string Unit(SoilChannel c)
    {
        if (IsNutrient(c) || c == SoilChannel.SlowNitrogen || c == SoilChannel.SlowPhosphorus || c == SoilChannel.SlowPotassium) return "ppm";
        switch (c)
        {
            case SoilChannel.OrganicMatter: return "%";
            case SoilChannel.Salinity: return "dS/m";
            case SoilChannel.Ph: return "";
            default: return "";
        }
    }
}

// ====================================================================================================================

/// <summary>Small math helpers of the agriculture rules (single precision, no allocation).</summary>
public static class AgriMath
{
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    public static float InverseLerp(float a, float b, float v) => Math.Abs(b - a) < 1e-9f ? (v >= b ? 1f : 0f) : Clamp01((v - a) / (b - a));
    public static float Smooth(float a, float b, float v)
    {
        float t = InverseLerp(a, b, v);
        return t * t * (3f - 2f * t);
    }

    /// <summary>1 inside [optLow, optHigh], falling smoothly to 0 at <paramref name="min"/> and <paramref name="max"/>.</summary>
    public static float Band(float v, float min, float optLow, float optHigh, float max)
    {
        if (v >= optLow && v <= optHigh) return 1f;
        if (v < optLow) return optLow <= min ? (v >= optLow ? 1f : 0f) : Smooth(min, optLow, v);
        return max <= optHigh ? (v <= optHigh ? 1f : 0f) : 1f - Smooth(optHigh, max, v);
    }

    /// <summary>The fraction left after decaying at <paramref name="rate"/> per unit for <paramref name="amount"/> units.</summary>
    public static float Decay(float rate, float amount) => (float)Math.Exp(-Math.Max(0f, rate) * Math.Max(0f, amount));

    // ---------------------------------------------------------------- hashing and noise (repeatable on every machine)
    public static uint Mix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return h;
    }

    public static uint Hash(int a, int b = 0, int c = 0, int d = 0)
    {
        uint h = Mix((uint)a * 0x9E3779B1u + 0x7F4A7C15u);
        h = Mix(h ^ ((uint)b * 0x85EBCA77u + 0x165667B1u));
        h = Mix(h ^ ((uint)c * 0xC2B2AE3Du + 0x27D4EB2Fu));
        h = Mix(h ^ ((uint)d * 0x27D4EB2Fu + 0x9E3779B9u));
        return h;
    }

    /// <summary>A stable hash of a string (the same on every machine and run, unlike string.GetHashCode).</summary>
    public static int Hash(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        unchecked
        {
            uint h = 2166136261u;
            foreach (char ch in s)
            {
                h ^= ch;
                h *= 16777619u;
            }
            return (int)Mix(h);
        }
    }

    /// <summary>A repeatable value in [0, 1).</summary>
    public static float Noise(int a, int b = 0, int c = 0, int d = 0) => (Hash(a, b, c, d) >> 8) * (1f / 16777216f);

    /// <summary>A repeatable value in [-1, 1].</summary>
    public static float Signed(int a, int b = 0, int c = 0, int d = 0) => Noise(a, b, c, d) * 2f - 1f;

    /// <summary>A repeatable normally distributed value (mean 0, deviation 1).</summary>
    public static float Gauss(int a, int b = 0, int c = 0, int d = 0)
    {
        float u1 = Math.Max(1e-7f, Noise(a, b, c, d));
        float u2 = Noise(a ^ 0x5bd1e995, b, c + 17, d);
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    /// <summary>Smooth 2D value noise in [-1, 1] (for natural patchiness of soils).</summary>
    public static float ValueNoise(int seed, float x, float y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Signed(seed, x0, y0), b = Signed(seed, x0 + 1, y0), c = Signed(seed, x0, y0 + 1), d = Signed(seed, x0 + 1, y0 + 1);
        float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
        return ab + (cd - ab) * fy;
    }

    /// <summary>Two octaves of <see cref="ValueNoise"/> (roughly [-1, 1]).</summary>
    public static float Patchy(int seed, float x, float y) => ValueNoise(seed, x, y) * 0.7f + ValueNoise(seed + 977, x * 2.3f + 11.1f, y * 2.3f - 7.3f) * 0.3f;

    /// <summary>Is <paramref name="p"/> inside the polygon (even-odd rule)?</summary>
    public static bool InPolygon(IList<Vector2> poly, Vector2 p)
    {
        if (poly == null || poly.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Distance from <paramref name="p"/> to the polygon's outline.</summary>
    public static float DistanceToOutline(IList<Vector2> poly, Vector2 p)
    {
        if (poly == null || poly.Count < 2) return float.MaxValue;
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            best = Math.Min(best, DistanceToSegment(p, poly[j], poly[i]));
        return best;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 < 1e-12f ? 0f : Clamp01(Vector2.Dot(p - a, ab) / len2);
        return (p - (a + ab * t)).magnitude;
    }

    /// <summary>Signed distance to a polygon: negative inside.</summary>
    public static float SignedDistance(IList<Vector2> poly, Vector2 p)
    {
        float d = DistanceToOutline(poly, p);
        return InPolygon(poly, p) ? -d : d;
    }

    /// <summary>The area of a polygon (absolute).</summary>
    public static float Area(IList<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return 0f;
        float a = 0f;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            a += (poly[j].x + poly[i].x) * (poly[j].y - poly[i].y);
        return Math.Abs(a) * 0.5f;
    }
}

/// <summary>A small repeatable random generator (xorshift): the same seed gives the same numbers on every machine.</summary>
public struct AgriRandom
{
    private uint state;

    public AgriRandom(int seed) { state = AgriMath.Mix((uint)seed ^ 0xA341316Cu) | 1u; }
    public AgriRandom(int a, int b, int c = 0) { state = AgriMath.Hash(a, b, c) | 1u; }

    public uint NextUInt()
    {
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return x;
    }

    /// <summary>[0, 1).</summary>
    public float Next() => (NextUInt() >> 8) * (1f / 16777216f);
    public float Range(float a, float b) => a + (b - a) * Next();
    public int Range(int a, int bExclusive) => bExclusive <= a ? a : a + (int)(NextUInt() % (uint)(bExclusive - a));
    public bool Chance(float p) => Next() < p;

    public float Gauss()
    {
        float u1 = Math.Max(1e-7f, Next()), u2 = Next();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}

// ====================================================================================================================

/// <summary>Names and words for the agriculture types (what the player reads).</summary>
public static class AgriText
{
    public static string Soil(SoilType t)
    {
        switch (t)
        {
            case SoilType.Sandy: return "Sandy soil";
            case SoilType.Clay: return "Clay soil";
            case SoilType.Rocky: return "Rocky soil";
            case SoilType.OrganicRich: return "Organic-rich soil";
            case SoilType.Peat: return "Peat soil";
            case SoilType.Volcanic: return "Volcanic soil";
            case SoilType.Swamp: return "Swamp soil";
            case SoilType.Magical: return "Magical soil";
            default: return t.ToString();
        }
    }

    public static string Category(PlantCategory c)
    {
        switch (c)
        {
            case PlantCategory.FruitBearing: return "Fruit-bearing";
            case PlantCategory.Rare: return "Rare and exotic";
            default: return c.ToString();
        }
    }

    public static string Stage(PlantStage s)
    {
        switch (s)
        {
            case PlantStage.Juvenile: return "Juvenile growth";
            case PlantStage.Vegetative: return "Vegetative maturity";
            case PlantStage.FruitDevelopment: return "Fruit development";
            case PlantStage.FruitMaturation: return "Fruit maturation";
            case PlantStage.HarvestReady: return "Harvest readiness";
            case PlantStage.Overripe: return "Overripening";
            case PlantStage.Dead: return "Death";
            default: return s.ToString();
        }
    }

    private static readonly string[] fruitStages = { "Flower", "Pollinated flower", "Early fruit", "Immature fruit", "Developing fruit", "Mature fruit", "Fully ripe fruit", "Overripe fruit", "Rotten fruit" };
    private static readonly string[] leafStages = { "Leaf bud", "Unfurling leaf", "Young leaf", "Tender leaf", "Growing leaf", "Mature leaf", "Full leaf", "Tough leaf", "Withered leaf" };
    private static readonly string[] rootStages = { "Forming root", "Swelling root", "Small root", "Young root", "Developing root", "Mature root", "Prime root", "Woody root", "Rotting root" };
    private static readonly string[] flowerStages = { "Bud", "Swelling bud", "Opening flower", "Half-open flower", "Open flower", "Full bloom", "Peak bloom", "Fading flower", "Wilted flower" };
    private static readonly string[] grainStages = { "Flowering head", "Pollinated head", "Milky grain", "Soft grain", "Doughy grain", "Hardening grain", "Ripe grain", "Shattering head", "Spoiled head" };

    /// <summary>The name of a produce stage for a part ("Fully ripe fruit", "Peak bloom"...).</summary>
    public static string Stage(ProducePart part, ProduceStage s)
    {
        int i = Mathf.Clamp((int)s, 0, 8);
        switch (part)
        {
            case ProducePart.Leaf: return leafStages[i];
            case ProducePart.Root: return rootStages[i];
            case ProducePart.Flower: return flowerStages[i];
            case ProducePart.Grain: return grainStages[i];
            default: return fruitStages[i];
        }
    }

    public static string Part(ProducePart p, bool plural = false)
    {
        switch (p)
        {
            case ProducePart.Leaf: return plural ? "leaves" : "leaf";
            case ProducePart.Root: return plural ? "roots" : "root";
            case ProducePart.Flower: return plural ? "flowers" : "flower";
            case ProducePart.Grain: return plural ? "heads" : "head";
            default: return plural ? "fruits" : "fruit";
        }
    }

    public static string Pest(PestKind p)
    {
        switch (p)
        {
            case PestKind.SapSuckers: return "Sap-sucking insects";
            case PestKind.RootParasites: return "Root parasites";
            default: return p.ToString();
        }
    }

    public static string Disease(DiseaseKind d)
    {
        switch (d)
        {
            case DiseaseKind.Fungal: return "Fungal infection";
            case DiseaseKind.RootRot: return "Root rot";
            case DiseaseKind.LeafSpot: return "Leaf disease";
            case DiseaseKind.Bacterial: return "Bacterial infection";
            case DiseaseKind.Viral: return "Viral disease";
            default: return d.ToString();
        }
    }

    public static string Moon(MoonPhase m)
    {
        switch (m)
        {
            case MoonPhase.New: return "New moon";
            case MoonPhase.Waxing: return "Waxing moon";
            case MoonPhase.Full: return "Full moon";
            default: return "Waning moon";
        }
    }

    public static string Instrument(InstrumentKind k)
    {
        switch (k)
        {
            case InstrumentKind.PhProbe: return "pH Probe";
            case InstrumentKind.SoilAnalysisKit: return "Alchemical Soil Analysis Kit";
            case InstrumentKind.MoistureMeter: return "Soil Moisture Meter";
            case InstrumentKind.Thermometer: return "Alchemical Thermometer";
            case InstrumentKind.LightMeter: return "Light Meter";
            case InstrumentKind.Hygrometer: return "Hygrometer";
            case InstrumentKind.CompactionTester: return "Soil Compaction Tester";
            case InstrumentKind.BotanicalHandbook: return "Botanical Handbook";
            case InstrumentKind.AdvancedAnalyzer: return "Advanced Alchemical Analyzer";
            case InstrumentKind.DepthGauge: return "Depth Gauge";
            case InstrumentKind.Magnifier: return "Magnifying Lens";
            default: return k.ToString();
        }
    }

    public static string Tool(FarmToolKind k)
    {
        switch (k)
        {
            case FarmToolKind.PruningShears: return "Pruning Shears";
            case FarmToolKind.PollinationBrush: return "Pollination Brush";
            case FarmToolKind.Stakes: return "Garden Stakes";
            default: return k.ToString();
        }
    }

    public static string Water(WaterSourceKind k)
    {
        switch (k)
        {
            case WaterSourceKind.Rain: return "Rainwater";
            case WaterSourceKind.River: return "River water";
            case WaterSourceKind.Well: return "Well water";
            case WaterSourceKind.Lake: return "Lake water";
            case WaterSourceKind.Swamp: return "Swamp water";
            case WaterSourceKind.Spring: return "Spring water";
            case WaterSourceKind.Alchemical: return "Alchemical water";
            case WaterSourceKind.Magical: return "Magical water";
            default: return k.ToString();
        }
    }

    public static string Skill(AgriSkill s)
    {
        switch (s)
        {
            case AgriSkill.PlantMedicine: return "Plant Medicine";
            case AgriSkill.MagicalCultivation: return "Magical Cultivation";
            case AgriSkill.SeedBreeding: return "Seed Breeding";
            default: return s.ToString();
        }
    }

    public static string Prune(PruneKind k)
    {
        switch (k)
        {
            case PruneKind.DeadWood: return "Cut out dead wood";
            case PruneKind.Diseased: return "Cut out diseased parts";
            case PruneKind.Thin: return "Thin the crown";
            case PruneKind.Shape: return "Shape and keep small";
            case PruneKind.CutBack: return "Cut back hard";
            default: return "Pinch out tips and buds";
        }
    }

    public static string Seasons(SeasonMask m)
    {
        if (m == SeasonMask.None) return "never";
        if ((m & SeasonMask.All) == SeasonMask.All) return "all year";
        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
            if (((int)m & (1 << i)) != 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(((Season)i).ToString().ToLowerInvariant());
            }
        return sb.ToString();
    }

    public static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>"Tomato" → "Tomatoes", "Strawberry" → "Strawberries", "Bean Pods" stays (the last word is pluralised).</summary>
    public static string Plural(string name, int count = 2)
    {
        if (string.IsNullOrEmpty(name) || count == 1) return name;
        int space = name.LastIndexOf(' ');
        string head = space >= 0 ? name.Substring(0, space + 1) : "", last = space >= 0 ? name.Substring(space + 1) : name;
        string lower = last.ToLowerInvariant();
        if (lower.EndsWith("s") || lower == "wheat" || lower == "lavender" || lower == "comfrey" || lower == "chamomile") return name;
        if (lower.EndsWith("o")) return head + last + "es";
        if (lower.EndsWith("y") && last.Length > 1 && "aeiou".IndexOf(lower[lower.Length - 2]) < 0) return head + last.Substring(0, last.Length - 1) + "ies";
        if (lower.EndsWith("ch") || lower.EndsWith("sh") || lower.EndsWith("x")) return head + last + "es";
        return head + last + "s";
    }

    /// <summary>"one", "two", ... "twelve", then digits.</summary>
    public static string Count(int n)
    {
        string[] w = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
        return n >= 0 && n < w.Length ? w[n] : n.ToString();
    }

    /// <summary>"3 days", "5 hours", "40 minutes" (game time, hours in).</summary>
    public static string Duration(double hours)
    {
        if (hours >= 48) return $"{Math.Round(hours / 24.0)} days";
        if (hours >= 24) return "a day";
        if (hours >= 2) return $"{Math.Round(hours)} hours";
        if (hours >= 1) return "an hour";
        return $"{Math.Max(1, Math.Round(hours * 60))} minutes";
    }
}
