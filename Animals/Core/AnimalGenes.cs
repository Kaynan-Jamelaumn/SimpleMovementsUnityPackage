using System;
using UnityEngine;

/// <summary>
/// An animal's inherited characteristics, each 0-1 (0.5 = the breed's typical animal). They are fixed at birth (or when
/// a young animal is first registered) from its breed and a seed - the same seed always gives the same animal - and are
/// never sent to clients as numbers: players learn them from what the animal does (its owner, in words). A young born on
/// the farm inherits them from both parents, with configurable mutations (see <see cref="AnimalBreeding.Inherit"/>).
/// </summary>
[Serializable]
public class AnimalGenes
{
    [Tooltip("Adult size.")]
    [Range(0f, 1f)] public float size = 0.5f;
    [Tooltip("Growth speed.")]
    [Range(0f, 1f)] public float growth = 0.5f;
    [Tooltip("Feed efficiency (more from the same food).")]
    [Range(0f, 1f)] public float efficiency = 0.5f;
    [Tooltip("Resistance to hardship, toxins and (Phase 2) disease.")]
    [Range(0f, 1f)] public float resistance = 0.5f;
    [Tooltip("Fertility (Phase 4).")]
    [Range(0f, 1f)] public float fertility = 0.5f;
    [Tooltip("Temperament: 0 skittish, 1 docile.")]
    [Range(0f, 1f)] public float temperament = 0.5f;
    [Tooltip("Quality of what it gives (Phase 3).")]
    [Range(0f, 1f)] public float productionQuality = 0.5f;
    [Tooltip("How much it gives (Phase 3).")]
    [Range(0f, 1f)] public float productionRate = 0.5f;
    [Tooltip("Longevity.")]
    [Range(0f, 1f)] public float longevity = 0.5f;
    [Tooltip("Which of the breed's coats it wears.")]
    public int coat;
    [Tooltip("Its coat pattern (0 = plain).")]
    public int pattern;
    [Tooltip("A rare trait it carries (0 = none).")]
    public int rare;
    [Tooltip("How closely its parents were related (0: not; 0.25: parent and young, or full siblings).")]
    [Range(0f, 1f)] public float inbreeding;
    [Tooltip("The seed it was made from.")]
    public int seed;

    /// <summary>A multiplier around 1 from a gene: 0 gives 1 - spread, 0.5 gives 1, 1 gives 1 + spread.</summary>
    public static float Scale(float gene, float spread) => 1f + (AnimalMath.Clamp01(gene) - 0.5f) * 2f * spread;

    public AnimalGenes Clone() => (AnimalGenes)MemberwiseClone();

    /// <summary>
    /// The genes of a new animal of a breed from a seed: each trait a little around the breed's typical one (the breed's
    /// temperament and production quality set their means), rarer breeds carrying rare traits more often.
    /// </summary>
    public static AnimalGenes FromSeed(AnimalBreedData breed, int seed, float spread = 0.16f)
    {
        var g = new AnimalGenes { seed = seed };
        float temperamentMean = breed != null ? breed.temperament : 0.5f;
        float qualityMean = breed != null ? breed.productionQuality : 0.5f;
        g.size = Trait(seed, 1, 0.5f, spread);
        g.growth = Trait(seed, 2, 0.5f, spread);
        g.efficiency = Trait(seed, 3, 0.5f, spread);
        g.resistance = Trait(seed, 4, 0.5f, spread);
        g.fertility = Trait(seed, 5, 0.5f, spread);
        g.temperament = Trait(seed, 6, temperamentMean, spread);
        g.productionQuality = Trait(seed, 7, qualityMean, spread);
        g.productionRate = Trait(seed, 8, 0.5f, spread);
        g.longevity = Trait(seed, 9, 0.5f, spread);
        int coats = breed != null && breed.coats != null ? breed.coats.Count : 0;
        g.coat = coats > 0 ? (int)(AnimalMath.Hash(seed, 10) % (uint)coats) : 0;
        g.pattern = AnimalMath.Noise(seed, 11) < 0.25f ? 1 + (int)(AnimalMath.Hash(seed, 12) % 3u) : 0;
        float rareChance = 0.01f + 0.09f * (breed != null ? breed.rarity : 0f);
        g.rare = AnimalMath.Noise(seed, 13) < rareChance ? 1 + (int)(AnimalMath.Hash(seed, 14) % 4u) : 0;
        return g;
    }

    private static float Trait(int seed, int channel, float mean, float spread) =>
        AnimalMath.Clamp(mean + AnimalMath.Gauss(seed, channel) * spread, 0.02f, 0.98f);

    /// <summary>A coarse word for a trait, as a keen eye would put it after watching the animal a while.</summary>
    public static string Word(float gene)
    {
        if (gene >= 0.78f) return "exceptional";
        if (gene >= 0.62f) return "good";
        if (gene >= 0.38f) return "ordinary";
        if (gene >= 0.22f) return "weak";
        return "poor";
    }
}
