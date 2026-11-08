using System;
using UnityEngine;

/// <summary>
/// A soil as a designer describes it: its type and every property a plant cares about. A <see cref="SoilField"/> starts
/// from one (its base soil) and lets parts of it differ (its sections, each with a <see cref="SoilOverride"/>).
/// Nutrients are available concentrations in mg/kg (ppm), organic matter in %, salinity in dS/m, the rest 0-1.
/// </summary>
[Serializable]
public class SoilProfile
{
    [Tooltip("The kind of soil: its colour, how it digs, what plants that prefer a soil type see in it.")]
    public SoilType type = SoilType.OrganicRich;
    [Tooltip("Acidity (below 7) or alkalinity (above 7). It decides which nutrients plants can take up.")]
    [Range(3f, 10f)] public float ph = 6.5f;

    [Header("Primary nutrients (ppm)")]
    [Range(0f, 400f)] public float nitrogen = 45f;
    [Range(0f, 250f)] public float phosphorus = 35f;
    [Range(0f, 1000f)] public float potassium = 220f;

    [Header("Secondary nutrients (ppm)")]
    [Range(0f, 6000f)] public float calcium = 1800f;
    [Range(0f, 1200f)] public float magnesium = 220f;
    [Range(0f, 200f)] public float sulfur = 18f;

    [Header("Micronutrients (ppm)")]
    [Range(0f, 150f)] public float iron = 15f;
    [Range(0f, 30f)] public float zinc = 3f;
    [Range(0f, 20f)] public float copper = 1.2f;
    [Range(0f, 150f)] public float manganese = 12f;
    [Range(0f, 10f)] public float boron = 1.2f;
    [Range(0f, 2f)] public float molybdenum = 0.2f;

    [Header("Body")]
    [Tooltip("Organic matter, % of the soil. Feeds the soil slowly, holds water and nutrients, keeps it loose.")]
    [Range(0f, 95f)] public float organicMatter = 7f;
    [Tooltip("Salts in the soil (dS/m). Above about 2 most plants start to suffer.")]
    [Range(0f, 20f)] public float salinity = 0.4f;
    [Tooltip("How much water it holds (0-1).")]
    [Range(0f, 1f)] public float waterRetention = 0.65f;
    [Tooltip("How fast extra water drains away (0-1).")]
    [Range(0f, 1f)] public float drainage = 0.6f;
    [Tooltip("How packed it is (0 loose - 1 hard): roots and water struggle in packed soil.")]
    [Range(0f, 1f)] public float compaction = 0.25f;
    [Tooltip("How wet it is at the start (0 dust-dry - 1 saturated).")]
    [Range(0f, 1f)] public float moisture = 0.55f;
    [Tooltip("Magical essence (0-1). Magical plants feed on it.")]
    [Range(0f, 1f)] public float magicEssence = 0f;
    [Tooltip("Disease organisms living in the soil (0-1).")]
    [Range(0f, 1f)] public float pathogens = 0.05f;
    [Tooltip("Stones in the soil (0-1): they make it hard to work and must be cleared before planting.")]
    [Range(0f, 1f)] public float stones = 0.05f;
    [Tooltip("How worked it already is (0 untouched - 1 freshly tilled).")]
    [Range(0f, 1f)] public float tilth = 0f;

    public float Get(SoilChannel c)
    {
        switch (c)
        {
            case SoilChannel.Ph: return ph;
            case SoilChannel.Nitrogen: return nitrogen;
            case SoilChannel.Phosphorus: return phosphorus;
            case SoilChannel.Potassium: return potassium;
            case SoilChannel.Calcium: return calcium;
            case SoilChannel.Magnesium: return magnesium;
            case SoilChannel.Sulfur: return sulfur;
            case SoilChannel.Iron: return iron;
            case SoilChannel.Zinc: return zinc;
            case SoilChannel.Copper: return copper;
            case SoilChannel.Manganese: return manganese;
            case SoilChannel.Boron: return boron;
            case SoilChannel.Molybdenum: return molybdenum;
            case SoilChannel.OrganicMatter: return organicMatter;
            case SoilChannel.Salinity: return salinity;
            case SoilChannel.WaterRetention: return waterRetention;
            case SoilChannel.Drainage: return drainage;
            case SoilChannel.Compaction: return compaction;
            case SoilChannel.Moisture: return moisture;
            case SoilChannel.MagicEssence: return magicEssence;
            case SoilChannel.Pathogens: return pathogens;
            case SoilChannel.Stones: return stones;
            case SoilChannel.Tilth: return tilth;
            default: return 0f;
        }
    }

    public void Set(SoilChannel c, float v)
    {
        v = SoilChannels.Clamp(c, v);
        switch (c)
        {
            case SoilChannel.Ph: ph = v; break;
            case SoilChannel.Nitrogen: nitrogen = v; break;
            case SoilChannel.Phosphorus: phosphorus = v; break;
            case SoilChannel.Potassium: potassium = v; break;
            case SoilChannel.Calcium: calcium = v; break;
            case SoilChannel.Magnesium: magnesium = v; break;
            case SoilChannel.Sulfur: sulfur = v; break;
            case SoilChannel.Iron: iron = v; break;
            case SoilChannel.Zinc: zinc = v; break;
            case SoilChannel.Copper: copper = v; break;
            case SoilChannel.Manganese: manganese = v; break;
            case SoilChannel.Boron: boron = v; break;
            case SoilChannel.Molybdenum: molybdenum = v; break;
            case SoilChannel.OrganicMatter: organicMatter = v; break;
            case SoilChannel.Salinity: salinity = v; break;
            case SoilChannel.WaterRetention: waterRetention = v; break;
            case SoilChannel.Drainage: drainage = v; break;
            case SoilChannel.Compaction: compaction = v; break;
            case SoilChannel.Moisture: moisture = v; break;
            case SoilChannel.MagicEssence: magicEssence = v; break;
            case SoilChannel.Pathogens: pathogens = v; break;
            case SoilChannel.Stones: stones = v; break;
            case SoilChannel.Tilth: tilth = v; break;
        }
    }

    public SoilProfile Clone() => (SoilProfile)MemberwiseClone();

    /// <summary>Copies every authored property of <paramref name="other"/> (and its type).</summary>
    public void CopyFrom(SoilProfile other)
    {
        if (other == null) return;
        type = other.type;
        for (int c = 0; c < SoilChannels.Authored; c++)
            Set((SoilChannel)c, other.Get((SoilChannel)c));
    }

    /// <summary>The typical soil of a type (the starting point of a field or a section).</summary>
    public static SoilProfile Preset(SoilType t)
    {
        var p = new SoilProfile { type = t };
        switch (t)
        {
            case SoilType.Sandy:
                p.Fill(6.2f, 12f, 12f, 70f, 500f, 60f, 8f, 8f, 1.2f, 0.6f, 6f, 0.4f, 0.1f, 1.2f, 0.3f, 0.25f, 0.9f, 0.15f, 0.3f, 0f, 0.03f, 0.05f);
                break;
            case SoilType.Clay:
                p.Fill(6.8f, 28f, 18f, 260f, 2600f, 320f, 14f, 14f, 2.5f, 1.5f, 14f, 1.0f, 0.15f, 3.5f, 0.8f, 0.85f, 0.2f, 0.65f, 0.6f, 0f, 0.06f, 0.05f);
                break;
            case SoilType.Rocky:
                p.Fill(7.0f, 10f, 8f, 120f, 1400f, 160f, 8f, 9f, 1.1f, 0.8f, 9f, 0.5f, 0.12f, 1.0f, 0.4f, 0.3f, 0.85f, 0.45f, 0.3f, 0f, 0.03f, 0.8f);
                break;
            case SoilType.OrganicRich:
                p.Fill(6.5f, 45f, 35f, 220f, 1800f, 220f, 18f, 15f, 3f, 1.2f, 12f, 1.2f, 0.2f, 7f, 0.4f, 0.65f, 0.6f, 0.25f, 0.55f, 0f, 0.05f, 0.05f);
                break;
            case SoilType.Peat:
                p.Fill(4.4f, 35f, 10f, 60f, 300f, 80f, 25f, 30f, 2f, 0.4f, 18f, 0.5f, 0.03f, 65f, 0.5f, 0.95f, 0.3f, 0.1f, 0.75f, 0f, 0.08f, 0f);
                break;
            case SoilType.Volcanic:
                p.Fill(6.0f, 30f, 14f, 300f, 1500f, 400f, 20f, 45f, 4f, 2.5f, 30f, 0.8f, 0.1f, 9f, 0.6f, 0.7f, 0.75f, 0.15f, 0.5f, 0.06f, 0.04f, 0.15f);
                break;
            case SoilType.Swamp:
                p.Fill(5.2f, 18f, 9f, 90f, 700f, 120f, 60f, 60f, 2f, 1f, 40f, 0.6f, 0.05f, 25f, 1.5f, 0.95f, 0.05f, 0.35f, 0.95f, 0f, 0.35f, 0f);
                break;
            case SoilType.Magical:
                p.Fill(6.6f, 40f, 30f, 200f, 1600f, 250f, 15f, 14f, 2.5f, 1.2f, 12f, 1.1f, 0.2f, 8f, 0.3f, 0.6f, 0.6f, 0.2f, 0.55f, 0.8f, 0.03f, 0.03f);
                break;
        }
        return p;
    }

    private void Fill(float ph, float n, float p, float k, float ca, float mg, float s, float fe, float zn, float cu, float mn, float b, float mo,
        float om, float sal, float ret, float drain, float comp, float moist, float magic, float path, float stone)
    {
        this.ph = ph; nitrogen = n; phosphorus = p; potassium = k; calcium = ca; magnesium = mg; sulfur = s;
        iron = fe; zinc = zn; copper = cu; manganese = mn; boron = b; molybdenum = mo;
        organicMatter = om; salinity = sal; waterRetention = ret; drainage = drain; compaction = comp; moisture = moist;
        magicEssence = magic; pathogens = path; stones = stone; tilth = 0f;
    }
}

/// <summary>
/// What a section of a field changes: optionally its soil type, and any of the authored properties (each one only when
/// its box is ticked, so a section can be "the same soil, only sourer"). Everything not overridden comes from the soil
/// under it (the field's base soil and the sections before it).
/// </summary>
[Serializable]
public class SoilOverride
{
    [Tooltip("Replace the soil type here.")]
    public bool overrideType;
    public SoilType type = SoilType.OrganicRich;
    [Tooltip("The values used for the ticked properties.")]
    public SoilProfile values = new SoilProfile();
    [Tooltip("One bit per authored property (pH = bit 0 ... Tilth = bit 22): which values this section sets.")]
    public int mask;

    public bool Overrides(SoilChannel c) => (int)c < SoilChannels.Authored && (mask & (1 << (int)c)) != 0;

    public void SetOverride(SoilChannel c, bool on)
    {
        if ((int)c >= SoilChannels.Authored) return;
        if (on) mask |= 1 << (int)c;
        else mask &= ~(1 << (int)c);
    }

    public bool IsEmpty => !overrideType && mask == 0;

    /// <summary>Overrides everything with a soil type's typical values.</summary>
    public void UsePreset(SoilType t)
    {
        overrideType = true;
        type = t;
        values = SoilProfile.Preset(t);
        mask = (1 << SoilChannels.Authored) - 1;
        SetOverride(SoilChannel.Tilth, false);
    }

    public SoilOverride Clone()
    {
        var o = (SoilOverride)MemberwiseClone();
        o.values = values != null ? values.Clone() : new SoilProfile();
        return o;
    }
}

/// <summary>What every soil type is like beyond its numbers: colour, workability, warmth, natural magic.</summary>
public static class SoilTypes
{
    public const int Count = 8;

    /// <summary>Colour of the dry soil.</summary>
    public static Color DryColor(SoilType t)
    {
        switch (t)
        {
            case SoilType.Sandy: return new Color(0.80f, 0.70f, 0.50f);
            case SoilType.Clay: return new Color(0.62f, 0.40f, 0.27f);
            case SoilType.Rocky: return new Color(0.56f, 0.53f, 0.49f);
            case SoilType.OrganicRich: return new Color(0.38f, 0.28f, 0.19f);
            case SoilType.Peat: return new Color(0.25f, 0.19f, 0.15f);
            case SoilType.Volcanic: return new Color(0.27f, 0.23f, 0.23f);
            case SoilType.Swamp: return new Color(0.34f, 0.32f, 0.21f);
            case SoilType.Magical: return new Color(0.33f, 0.24f, 0.37f);
            default: return new Color(0.4f, 0.3f, 0.2f);
        }
    }

    /// <summary>Colour of the soaked soil.</summary>
    public static Color WetColor(SoilType t)
    {
        Color d = DryColor(t);
        return new Color(d.r * 0.5f, d.g * 0.5f, d.b * 0.48f);
    }

    /// <summary>How easily it digs (1 = easily).</summary>
    public static float Workability(SoilType t)
    {
        switch (t)
        {
            case SoilType.Sandy: return 1f;
            case SoilType.Clay: return 0.55f;
            case SoilType.Rocky: return 0.4f;
            case SoilType.OrganicRich: return 0.95f;
            case SoilType.Peat: return 1f;
            case SoilType.Volcanic: return 0.85f;
            case SoilType.Swamp: return 0.6f;
            case SoilType.Magical: return 0.9f;
            default: return 0.8f;
        }
    }

    /// <summary>Degrees warmer than the air the root zone keeps (volcanic soil is warm, wet soil cool).</summary>
    public static float Warmth(SoilType t)
    {
        switch (t)
        {
            case SoilType.Sandy: return 1f;
            case SoilType.Volcanic: return 2.5f;
            case SoilType.Swamp: return -1.5f;
            case SoilType.Peat: return -1f;
            case SoilType.Magical: return 0.5f;
            default: return 0f;
        }
    }

    /// <summary>The magical essence the soil returns to by itself (0 for ordinary soils).</summary>
    public static float MagicLevel(SoilType t) => t == SoilType.Magical ? 0.8f : t == SoilType.Volcanic ? 0.06f : 0f;

    /// <summary>How the soil looks and feels (what a player sees and touches; no numbers).</summary>
    public static string Look(SoilType t)
    {
        switch (t)
        {
            case SoilType.Sandy: return "pale, gritty soil that runs through the fingers";
            case SoilType.Clay: return "reddish-brown clay, dense and smooth between the fingers";
            case SoilType.Rocky: return "thin grey soil full of stones";
            case SoilType.OrganicRich: return "dark, crumbly soil that smells of the forest floor";
            case SoilType.Peat: return "black, spongy peat, light and fibrous";
            case SoilType.Volcanic: return "dark ash soil, fine and warm to the touch";
            case SoilType.Swamp: return "dark, sour-smelling mud";
            case SoilType.Magical: return "dark soil with a faint violet shimmer";
            default: return "soil";
        }
    }
}

/// <summary>
/// The chemistry and physics of soil: how pH changes what plants can take up, how much water it holds and when plants
/// can reach it, how fertile it is, how close it is to each soil type.
/// </summary>
public static class SoilChemistry
{
    /// <summary>Kilograms of soil in the root zone under one square metre (20 cm deep).</summary>
    public const float RootZoneKgPerM2 = 260f;
    /// <summary>Depth of the root zone the water model holds, mm.</summary>
    public const float RootZoneMm = 200f;

    /// <summary>What "adequate" means for each nutrient for an average plant (ppm): the scale of the kit's chart.</summary>
    public static float Adequate(Nutrient n)
    {
        switch (n)
        {
            case Nutrient.Nitrogen: return 30f;
            case Nutrient.Phosphorus: return 25f;
            case Nutrient.Potassium: return 180f;
            case Nutrient.Calcium: return 1200f;
            case Nutrient.Magnesium: return 150f;
            case Nutrient.Sulfur: return 14f;
            case Nutrient.Iron: return 10f;
            case Nutrient.Zinc: return 2f;
            case Nutrient.Copper: return 0.9f;
            case Nutrient.Manganese: return 9f;
            case Nutrient.Boron: return 0.9f;
            case Nutrient.Molybdenum: return 0.12f;
            default: return 1f;
        }
    }

    /// <summary>
    /// How much of a nutrient plants can take up at a pH (0-1): nitrogen, potassium and sulfur best above 6, phosphorus
    /// between 6 and 7.5, calcium and magnesium in neutral to alkaline soil, iron, manganese, zinc and copper in acidic
    /// soil, boron between 5 and 7, molybdenum in alkaline soil.
    /// </summary>
    public static float Availability(Nutrient n, float ph)
    {
        switch (n)
        {
            case Nutrient.Nitrogen: return Mathf.Lerp(0.25f, 1f, AgriMath.Band(ph, 4.2f, 6.0f, 8.0f, 9.6f));
            case Nutrient.Phosphorus: return Mathf.Lerp(0.15f, 1f, AgriMath.Band(ph, 4.6f, 6.2f, 7.3f, 8.6f));
            case Nutrient.Potassium: return Mathf.Lerp(0.35f, 1f, AgriMath.Band(ph, 4.3f, 6.0f, 9.0f, 10f));
            case Nutrient.Calcium: return Mathf.Lerp(0.25f, 1f, AgriMath.Band(ph, 4.5f, 6.8f, 8.6f, 10f));
            case Nutrient.Magnesium: return Mathf.Lerp(0.25f, 1f, AgriMath.Band(ph, 4.5f, 6.8f, 8.6f, 10f));
            case Nutrient.Sulfur: return Mathf.Lerp(0.35f, 1f, AgriMath.Band(ph, 4.5f, 6.0f, 9.0f, 10f));
            case Nutrient.Iron: return Mathf.Lerp(0.1f, 1f, AgriMath.Band(ph, 3.0f, 4.0f, 6.5f, 8.2f));
            case Nutrient.Manganese: return Mathf.Lerp(0.1f, 1f, AgriMath.Band(ph, 3.0f, 4.5f, 6.5f, 8.2f));
            case Nutrient.Zinc: return Mathf.Lerp(0.15f, 1f, AgriMath.Band(ph, 3.0f, 5.0f, 7.0f, 8.4f));
            case Nutrient.Copper: return Mathf.Lerp(0.15f, 1f, AgriMath.Band(ph, 3.0f, 5.0f, 7.0f, 8.4f));
            case Nutrient.Boron: return Mathf.Lerp(0.2f, 1f, AgriMath.Band(ph, 4.0f, 5.0f, 7.0f, 8.8f));
            case Nutrient.Molybdenum: return Mathf.Lerp(0.1f, 1f, AgriMath.Band(ph, 4.5f, 7.0f, 10f, 10f));
            default: return 1f;
        }
    }

    // ---------------------------------------------------------------- water
    /// <summary>The moisture (0-1 of saturation) left after the soil has drained: sand little, clay and peat a lot.</summary>
    public static float FieldCapacity(float retention) => 0.35f + 0.5f * AgriMath.Clamp01(retention);
    /// <summary>The moisture below which roots can draw no more water.</summary>
    public static float WiltingPoint(float retention) => 0.08f + 0.3f * AgriMath.Clamp01(retention);
    /// <summary>Millimetres of water the root zone holds when saturated.</summary>
    public static float CapacityMm(float retention) => RootZoneMm * (0.35f + 0.25f * AgriMath.Clamp01(retention));

    /// <summary>
    /// The water plants can reach: 0 at the wilting point, 1 at field capacity, above 1 when waterlogged (1.6 or so when
    /// saturated).
    /// </summary>
    public static float Available(float moisture, float retention)
    {
        float wp = WiltingPoint(retention), fc = FieldCapacity(retention);
        return (moisture - wp) / Mathf.Max(0.05f, fc - wp);
    }

    /// <summary>How strongly the soil resists a change of pH (clay and organic matter buffer it).</summary>
    public static float PhBuffer(float retention, float organic) => 1f + 1.5f * AgriMath.Clamp01(retention) + Mathf.Min(organic, 60f) / 12f;

    /// <summary>
    /// The soil's overall fertility (0-1): what it can feed (nitrogen, phosphorus, potassium within reach at its pH), what it
    /// holds in reserve, its organic matter and texture.
    /// </summary>
    public static float Fertility(float[] v, int o)
    {
        float ph = v[o + (int)SoilChannel.Ph];
        float n = Sufficiency(v[o + (int)SoilChannel.Nitrogen] * Availability(Nutrient.Nitrogen, ph), Adequate(Nutrient.Nitrogen));
        float p = Sufficiency(v[o + (int)SoilChannel.Phosphorus] * Availability(Nutrient.Phosphorus, ph), Adequate(Nutrient.Phosphorus));
        float k = Sufficiency(v[o + (int)SoilChannel.Potassium] * Availability(Nutrient.Potassium, ph), Adequate(Nutrient.Potassium));
        float reserve = AgriMath.Clamp01((v[o + (int)SoilChannel.SlowNitrogen] + v[o + (int)SoilChannel.OrganicMatter] * 10f) / 150f);
        float om = AgriMath.Band(v[o + (int)SoilChannel.OrganicMatter], 0.5f, 4f, 20f, 70f);
        float salt = 1f - AgriMath.Smooth(2f, 10f, v[o + (int)SoilChannel.Salinity]);
        float body = 1f - 0.4f * AgriMath.Clamp01(v[o + (int)SoilChannel.Compaction]) - 0.3f * AgriMath.Clamp01(v[o + (int)SoilChannel.Stones]);
        float f = (n * 0.3f + p * 0.2f + k * 0.2f + reserve * 0.15f + om * 0.15f) * salt * body;
        return AgriMath.Clamp01(f);
    }

    /// <summary>0 with none, 1 at the adequate level, a little above 1 beyond (plenty).</summary>
    public static float Sufficiency(float available, float adequate) => adequate <= 0f ? 1f : Mathf.Min(1.2f, available / adequate);

    // ---------------------------------------------------------------- type mix
    private static readonly float[][] signatures = BuildSignatures();

    private static float[][] BuildSignatures()
    {
        var s = new float[SoilTypes.Count][];
        for (int t = 0; t < SoilTypes.Count; t++)
        {
            SoilProfile p = SoilProfile.Preset((SoilType)t);
            s[t] = new[] { p.waterRetention, p.drainage, Mathf.Sqrt(p.organicMatter / 95f), p.stones, p.magicEssence, (p.iron + p.magnesium / 10f) / 85f, p.ph / 10f };
        }
        return s;
    }

    /// <summary>
    /// How much a cell is like each soil type (weights summing to 1): half from its own type, half from how its texture,
    /// organic matter, stones, magic and minerals compare with each type's typical soil - so working sand into clay
    /// slowly makes it more like a sandy soil for the plants.
    /// </summary>
    public static void TypeMix(float[] v, int o, SoilType own, float[] weights)
    {
        float r = v[o + (int)SoilChannel.WaterRetention], d = v[o + (int)SoilChannel.Drainage];
        float om = Mathf.Sqrt(Mathf.Max(0f, v[o + (int)SoilChannel.OrganicMatter]) / 95f), st = v[o + (int)SoilChannel.Stones];
        float mg = v[o + (int)SoilChannel.MagicEssence], min = (v[o + (int)SoilChannel.Iron] + v[o + (int)SoilChannel.Magnesium] / 10f) / 85f;
        float ph = v[o + (int)SoilChannel.Ph] / 10f;
        float sum = 0f;
        for (int t = 0; t < SoilTypes.Count; t++)
        {
            float[] s = signatures[t];
            float dist = Sq(r - s[0]) + Sq(d - s[1]) + Sq(om - s[2]) * 1.5f + Sq(st - s[3]) + Sq(mg - s[4]) * 2f + Sq(min - s[5]) + Sq(ph - s[6]) * 0.5f;
            float w = (float)Math.Exp(-dist / 0.05f);
            weights[t] = w;
            sum += w;
        }
        bool any = sum > 1e-6f;
        for (int t = 0; t < SoilTypes.Count; t++)
            weights[t] = (any ? 0.5f * weights[t] / sum : 0f) + (t == (int)own ? (any ? 0.5f : 1f) : 0f);
    }

    private static float Sq(float x) => x * x;
}
