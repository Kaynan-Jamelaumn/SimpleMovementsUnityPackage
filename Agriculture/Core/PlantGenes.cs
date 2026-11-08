using System;
using UnityEngine;

/// <summary>The heritable traits of a plant. Serialized as numbers: add at the END (the seed code holds 12).</summary>
public enum GeneTrait
{
    GrowthSpeed,
    ColdResistance,
    HeatResistance,
    DroughtResistance,
    DiseaseResistance,
    FruitQuantity,
    FruitQuality,
    MedicinalPotency,
    AlchemicalConcentration,
    MaxSize,
    MaturationSpeed,
    NutrientEfficiency,
}

/// <summary>
/// A plant's genes: each of the twelve <see cref="GeneTrait"/>s a level from -2 to +2 (0 = the species as it is in the
/// wild), plus a rare trait and the seed's vigour (viability). They travel with the seed as its per-copy code
/// (<see cref="ToCode"/>), pass from parent plants to the seeds they make (<see cref="Cross"/>), drift by mutation, and
/// can be bred up over generations by choosing the best plants.
/// </summary>
[Serializable]
public struct PlantGenes
{
    public const int TraitCount = 12;
    public const int MinLevel = -2, MaxLevel = 2;

    /// <summary>4 bits per trait, level + 8.</summary>
    public long bits;
    /// <summary>A rare trait: a lustre and a strength its kind does not usually have.</summary>
    public bool rare;
    /// <summary>The seed's vigour, 0-3 (how likely it is to come up).</summary>
    public byte viability;

    public static PlantGenes Wild => FromLevels(new int[TraitCount], false, 2);

    public int Level(GeneTrait t)
    {
        if (bits == 0) return 0; // default(PlantGenes): the wild type
        return (int)((bits >> (4 * (int)t)) & 15L) - 8;
    }

    public void SetLevel(GeneTrait t, int level)
    {
        if (bits == 0) bits = WildBits;
        level = Mathf.Clamp(level, MinLevel, MaxLevel);
        int shift = 4 * (int)t;
        bits = (bits & ~(15L << shift)) | ((long)(level + 8) << shift);
    }

    private static readonly long WildBits = BuildWild();

    private static long BuildWild()
    {
        long b = 0;
        for (int t = 0; t < TraitCount; t++) b |= 8L << (4 * t);
        return b;
    }

    public static PlantGenes FromLevels(int[] levels, bool rare, int viability)
    {
        var g = new PlantGenes { bits = WildBits, rare = rare, viability = (byte)Mathf.Clamp(viability, 0, 3) };
        if (levels != null)
            for (int t = 0; t < TraitCount && t < levels.Length; t++)
                g.SetLevel((GeneTrait)t, levels[t]);
        return g;
    }

    /// <summary>The trait's effect as a multiplier around 1 (growth +8% per level, yield +12%...).</summary>
    public float Factor(GeneTrait t)
    {
        int l = Level(t);
        switch (t)
        {
            case GeneTrait.GrowthSpeed: return 1f + 0.08f * l;
            case GeneTrait.FruitQuantity: return 1f + 0.12f * l;
            case GeneTrait.MedicinalPotency:
            case GeneTrait.AlchemicalConcentration: return (1f + 0.12f * l) * (rare ? 1.25f : 1f);
            case GeneTrait.MaxSize: return 1f + 0.1f * l;
            case GeneTrait.MaturationSpeed: return 1f + 0.1f * l;
            case GeneTrait.NutrientEfficiency: return 1f - 0.07f * l;
            case GeneTrait.DiseaseResistance: return 1f - 0.15f * l;
            default: return 1f + 0.1f * l;
        }
    }

    /// <summary>Degrees the cold or heat it survives moves (cold: lower, heat: higher).</summary>
    public float ColdShift => -1.5f * Level(GeneTrait.ColdResistance);
    public float HeatShift => 1.5f * Level(GeneTrait.HeatResistance);
    /// <summary>Added to its drought tolerance.</summary>
    public float DroughtShift => 0.1f * Level(GeneTrait.DroughtResistance);
    /// <summary>Added to the quality of its produce.</summary>
    public float QualityShift => 0.04f * Level(GeneTrait.FruitQuality) + (rare ? 0.08f : 0f);
    /// <summary>Chance the seed comes up, before the conditions.</summary>
    public float Viability => viability == 0 ? 0.55f : viability == 1 ? 0.72f : viability == 2 ? 0.86f : 0.95f;

    // ---------------------------------------------------------------- the seed code
    /// <summary>
    /// The seed's per-copy code: 2 + (the twelve levels in base 5, 28 bits | viability &lt;&lt; 28 | rare &lt;&lt; 30). Codes
    /// 0 and 1 (a seed bought, looted or spawned without one) are wild seeds of normal vigour.
    /// </summary>
    public int ToCode()
    {
        int v = 0;
        for (int t = TraitCount - 1; t >= 0; t--)
            v = v * 5 + (Level((GeneTrait)t) + 2);
        return 2 + (v | ((viability & 3) << 28) | ((rare ? 1 : 0) << 30));
    }

    public static PlantGenes FromCode(int code)
    {
        if (code < 2) return Wild;
        int x = code - 2;
        int v = x & ((1 << 28) - 1);
        var levels = new int[TraitCount];
        for (int t = 0; t < TraitCount; t++)
        {
            levels[t] = v % 5 - 2;
            v /= 5;
        }
        return FromLevels(levels, ((x >> 30) & 1) != 0, (x >> 28) & 3);
    }

    // ---------------------------------------------------------------- inheritance
    /// <summary>
    /// The seed of <paramref name="mother"/> pollinated by <paramref name="father"/> (itself, for a self-pollinated
    /// flower): each trait from one parent or between the two, sometimes a step up or down (mutation, more for species
    /// that vary a lot; <paramref name="favour"/> 0-1 tilts mutations upwards - the breeder's eye), rarely a rare trait.
    /// </summary>
    public static PlantGenes Cross(PlantGenes mother, PlantGenes father, ref AgriRandom rng, float mutationChance, float rareChance, float variation, float favour, int viability)
    {
        var levels = new int[TraitCount];
        for (int t = 0; t < TraitCount; t++)
        {
            int a = mother.Level((GeneTrait)t), b = father.Level((GeneTrait)t);
            float r = rng.Next();
            int l = r < 0.4f ? a : r < 0.8f ? b : Mathf.RoundToInt((a + b) * 0.5f + (rng.Next() - 0.5f) * 0.5f);
            if (rng.Chance(mutationChance * Mathf.Max(0f, variation)))
                l += rng.Chance(0.5f + 0.25f * AgriMath.Clamp01(favour)) ? 1 : -1;
            levels[t] = l;
        }
        bool rare = (mother.rare && father.rare) || ((mother.rare || father.rare) && rng.Chance(0.5f)) || rng.Chance(rareChance * Mathf.Max(0.2f, variation));
        return FromLevels(levels, rare, viability);
    }

    /// <summary>Sum of the levels (how far the line has been bred up overall).</summary>
    public int Score()
    {
        int s = 0;
        for (int t = 0; t < TraitCount; t++) s += Level((GeneTrait)t);
        return s;
    }

    /// <summary>"Growth +1, Yield +2, rare" (for tests and the editor; players never see genes).</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        for (int t = 0; t < TraitCount; t++)
        {
            int l = Level((GeneTrait)t);
            if (l == 0) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append((GeneTrait)t).Append(l > 0 ? " +" : " ").Append(l);
        }
        if (rare) sb.Append(sb.Length > 0 ? ", rare" : "rare");
        return sb.Length > 0 ? sb.ToString() : "wild";
    }

    public bool SameAs(PlantGenes other) => ToCode() == other.ToCode();
}
