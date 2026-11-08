using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What can be seen on a plant (0-1 each): the marks its troubles leave. The simulation sets them; the look and the
/// observations show them; nothing says what caused them.
/// </summary>
public enum PlantSymptom
{
    /// <summary>Older leaves pale and yellowing.</summary>
    PaleOldLeaves,
    /// <summary>A dull purple cast on the leaves.</summary>
    PurpleLeaves,
    /// <summary>Leaf edges brown and crisp.</summary>
    ScorchedEdges,
    /// <summary>Yellow between green veins.</summary>
    YellowBetweenVeins,
    /// <summary>The newest leaves pale yellow.</summary>
    PaleNewLeaves,
    /// <summary>Growing tips dying, fruits with dark sunken ends.</summary>
    DeadTips,
    /// <summary>Leaf tips burnt.</summary>
    LeafBurn,
    /// <summary>Drooping, wilting.</summary>
    Wilting,
    /// <summary>Yellow, limp and soft.</summary>
    Waterlogged,
    /// <summary>Blackened, glassy leaves.</summary>
    FrostBitten,
    /// <summary>Bleached, scorched patches.</summary>
    Sunscald,
    /// <summary>Long, thin, pale stems reaching.</summary>
    Leggy,
    /// <summary>Small for its age.</summary>
    Stunted,
}

/// <summary>One piece of produce on a plant (a flower becoming a fruit, a leaf, a root, a head of grain), on its own timer.</summary>
[Serializable]
public class FruitState
{
    public int id;
    public ProduceStage stage;
    /// <summary>0-1 through the stage.</summary>
    public float progress;
    /// <summary>This piece's own pace (0.85-1.15): pieces of one plant never ripen all together.</summary>
    public float pace = 1f;
    /// <summary>Where it hangs on the plant (0-1, for the drawing).</summary>
    public float slot;
    /// <summary>0-1 of its full size.</summary>
    public float size;
    public bool pollinated;
    /// <summary>Pollinated by another plant (its genes and species).</summary>
    public bool crossed;
    public PlantGenes donor;
    public string donorSpecies = "";
    /// <summary>The conditions it grew in: a sum of quality and the hours summed.</summary>
    public float qualitySum, qualityHours;
    /// <summary>Hours it has been rotten (it falls).</summary>
    public float rotHours;

    public float Quality => qualityHours > 0f ? qualitySum / qualityHours : 0.5f;
    public bool Gone => stage == ProduceStage.Rotten && rotHours > 24f;
}

/// <summary>
/// One plant in a field: where it stands, its stage and health, what it went through (its history decides its quality),
/// its troubles, its genes, its produce. Saved with the field (JSON).
/// </summary>
[Serializable]
public class PlantState
{
    public const int SymptomCount = 13;

    public int id;
    public string species = "";
    /// <summary>Field-local position (X, Z), metres.</summary>
    public Vector2 position;
    public PlantStage stage;
    /// <summary>0-1 through the current stage.</summary>
    public float progress;
    public float depthCm;
    public bool covered;
    public double planted, last;
    public float ageDays;
    /// <summary>Perennials: years lived (rises every spring).</summary>
    public int years;
    public Season lastSeason;
    public float health = 1f;
    /// <summary>0-1 of its full size.</summary>
    public float size;
    /// <summary>Recent stress (0-1, about a day's memory).</summary>
    public float stress;
    /// <summary>The light it has been getting (0-1, a two-day memory of daytime light).</summary>
    public float lightAvg = 0.7f;
    public float lastTemperature = -999f;
    /// <summary>Hours of cold (below 7 °C) gathered since it last flowered.</summary>
    public float chill;
    public bool dormant;
    public bool plantedAtNight;
    public MoonPhase plantedMoon;
    public PlantGenes genes;
    /// <summary>The conditions it lived in: a weighted sum of quality and the weights summed.</summary>
    public float qualitySum, qualityHours;
    public float[] symptoms = new float[SymptomCount];
    public float[] pests = new float[8];
    public float[] diseases = new float[6];
    /// <summary>How dense the crown is (0-1): pruning opens it.</summary>
    public float canopy = 0.5f;
    /// <summary>Dead wood in the crown (0-1).</summary>
    public float deadWood;
    /// <summary>Fresh wounds (cuts, chewing): diseases get in (0-1).</summary>
    public float wounds;
    /// <summary>New growth encouraged by good pruning (0-1, wears off).</summary>
    public float pruneBoost;
    public double lastPruned = -1;
    public bool supported;
    /// <summary>Flushes of flowers this season.</summary>
    public int flushes;
    /// <summary>Hours until the next flush may open.</summary>
    public float flushTimer;
    /// <summary>Flowers still to open in this flush.</summary>
    public int flowersLeft;
    /// <summary>Flowers of this flush, opened and still to open (they open evenly through the flowering).</summary>
    public int flowersTotal;
    public int harvests;
    /// <summary>Hours the leaves stay wet (rain, watering over the leaves).</summary>
    public float leafWet;
    /// <summary>Went to flower early (leaf crops turn bitter).</summary>
    public bool bolted;
    /// <summary>Hours since it died.</summary>
    public float deadHours;
    /// <summary>What a player wrote on its label.</summary>
    public string label = "";
    /// <summary>Who planted it (player key).</summary>
    public int owner;
    public List<FruitState> fruits = new List<FruitState>();
    public int nextFruitId = 1;
    /// <summary>The seed's own chance to come up.</summary>
    public float viability = 0.86f;
    /// <summary>The seed was left uncovered in the sun and wind (needs covering) - it dries out.</summary>
    public float exposure;

    // ---------------------------------------------------------------- runtime caches (not saved)
    [NonSerialized] public PlantSpeciesData data;
    [NonSerialized] public int[] rootCells;
    [NonSerialized] public float[] rootWeights;
    [NonSerialized] public float rootRadius = -1f;
    [NonSerialized] public int bucket = -1;
    /// <summary>What it last saw of its soil and neighbours, for the quiet stretches of a catch-up.</summary>
    [NonSerialized] public PlantSurroundings surroundings;

    public bool Alive => stage != PlantStage.Dead;
    public bool IsSeed => stage == PlantStage.Seed || stage == PlantStage.Germination;
    public bool Emerged => Alive && !IsSeed;
    public float Quality => qualityHours > 0f ? qualitySum / qualityHours : 0.5f;
    public float Symptom(PlantSymptom s) => symptoms != null && (int)s < symptoms.Length ? symptoms[(int)s] : 0f;
    public float Pest(PestKind p) => pests != null && (int)p < pests.Length ? pests[(int)p] : 0f;
    public float Disease(DiseaseKind d) => diseases != null && (int)d < diseases.Length ? diseases[(int)d] : 0f;

    public float PestLoad()
    {
        float s = 0f;
        if (pests != null) foreach (float p in pests) s += p;
        return s;
    }

    public float DiseaseLoad()
    {
        float s = 0f;
        if (diseases != null) foreach (float d in diseases) s += d;
        return s;
    }

    public FruitState Fruit(int fruitId)
    {
        foreach (FruitState f in fruits)
            if (f.id == fruitId) return f;
        return null;
    }

    /// <summary>Fills arrays an old save may lack.</summary>
    public void Repair()
    {
        if (symptoms == null || symptoms.Length != SymptomCount) Array.Resize(ref symptoms, SymptomCount);
        if (pests == null || pests.Length != 8) Array.Resize(ref pests, 8);
        if (diseases == null || diseases.Length != 6) Array.Resize(ref diseases, 6);
        fruits ??= new List<FruitState>();
        label ??= "";
        species ??= "";
    }
}

/// <summary>The plants of a field (JSON-friendly).</summary>
[Serializable]
public class PlantStateList
{
    public List<PlantState> plants = new List<PlantState>();
    public List<BeneficialRelease> beneficials = new List<BeneficialRelease>();
    public int nextId = 1;
}
