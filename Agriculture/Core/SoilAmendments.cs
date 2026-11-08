using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One change a material makes to the soil: <see cref="amount"/> added to a channel per portion spread over one square metre.</summary>
[Serializable]
public struct SoilEffect
{
    public SoilChannel channel;
    [Tooltip("Added per portion over one square metre (ppm for nutrients, % for organic matter, dS/m for salinity, 0-1 for the rest).")]
    public float amount;

    public SoilEffect(SoilChannel channel, float amount) { this.channel = channel; this.amount = amount; }
}

/// <summary>
/// A material put on or into the soil: a texture amendment (sand, clay, gravel, peat), a pH modifier (limestone,
/// sulfur, ash), a fertilizer (manure, compost, bone meal, mineral, liquid, slow-release, alchemical, magical), a mulch,
/// a treatment for pests and diseases, or living helpers. Its numbers say what one portion does to one square metre:
/// a portion dumped on one spot is many times stronger there (and can burn).
/// </summary>
[Serializable]
public class SoilAmendmentData
{
    public string id = "";
    public string name = "";
    public AmendmentKind kind = AmendmentKind.Fertilizer;
    [TextArea(2, 5)] public string description = "";
    [Tooltip("Weight of one portion (one handful, scoop or dose), kg.")]
    [Min(0.01f)] public float portionKg = 0.5f;

    [Header("Soil")]
    [Tooltip("What one portion adds to one square metre.")]
    public List<SoilEffect> effects = new List<SoilEffect>();
    [Tooltip("pH units one portion moves one square metre (before the soil's buffering): + raises, - lowers. It acts over days.")]
    public float phEffect;
    [Tooltip("Nitrogen, phosphorus and potassium go into the soil's slow reserve and are released over days instead of at once.")]
    public bool slowRelease;
    [Tooltip("It must be worked into the soil (hoe) to act fully; left on top it works in slowly by itself.")]
    public bool needsWorkingIn = true;
    [Tooltip("A liquid: it soaks in at once (and wets the soil a little).")]
    public bool liquid;
    [Tooltip("Mulch it lays on the soil per portion and square metre (0-1).")]
    [Range(0f, 1f)] public float mulch;

    [Header("Plants")]
    [Tooltip("Fraction of each pest it kills on a treated plant (by Pest Kind; empty = none).")]
    public float[] pestKill = new float[0];
    [Tooltip("Fraction of each disease it cures on a treated plant (by Disease Kind; empty = none).")]
    public float[] diseaseCure = new float[0];
    [Tooltip("Damage it does to the treated plant itself (0-1).")]
    [Range(0f, 1f)] public float plantHarm;
    [Tooltip("Living helpers: game days they keep pests down where they were released (0 = none).")]
    [Min(0f)] public float beneficialDays;
    [Tooltip("How strongly the helpers keep each pest down (by Pest Kind).")]
    public float[] beneficialSuppression = new float[0];
    [Tooltip("Growth boost for magical plants it gives the soil (magical compost).")]
    [Range(0f, 1f)] public float magicalBoost;

    [Header("Look and trade")]
    [Tooltip("How it looks lying on the soil.")]
    public Color color = new Color(0.3f, 0.22f, 0.14f);
    [Min(0f)] public float value = 4f;

    public bool IsTreatment => kind == AmendmentKind.Treatment || kind == AmendmentKind.Beneficial || (pestKill != null && pestKill.Length > 0) || (diseaseCure != null && diseaseCure.Length > 0);
    public bool TouchesSoil => (effects != null && effects.Count > 0) || Mathf.Abs(phEffect) > 1e-5f || mulch > 0f;

    public float PestKill(PestKind p) => pestKill != null && (int)p < pestKill.Length ? pestKill[(int)p] : 0f;
    public float DiseaseCure(DiseaseKind d) => diseaseCure != null && (int)d < diseaseCure.Length ? diseaseCure[(int)d] : 0f;
    public float Suppression(PestKind p) => beneficialSuppression != null && (int)p < beneficialSuppression.Length ? beneficialSuppression[(int)p] : 0f;

    public float Effect(SoilChannel c)
    {
        if (effects == null) return 0f;
        float sum = 0f;
        foreach (SoilEffect e in effects)
            if (e.channel == c) sum += e.amount;
        return sum;
    }

    public SoilAmendmentData Clone()
    {
        var a = (SoilAmendmentData)MemberwiseClone();
        a.effects = effects != null ? new List<SoilEffect>(effects) : new List<SoilEffect>();
        a.pestKill = pestKill != null ? (float[])pestKill.Clone() : new float[0];
        a.diseaseCure = diseaseCure != null ? (float[])diseaseCure.Clone() : new float[0];
        a.beneficialSuppression = beneficialSuppression != null ? (float[])beneficialSuppression.Clone() : new float[0];
        return a;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(id)) errors.Add("No id.");
        if (string.IsNullOrWhiteSpace(name)) warnings.Add("No name.");
        if (!TouchesSoil && !IsTreatment && beneficialDays <= 0f) warnings.Add($"{name}: it does nothing (no soil effects, no treatment).");
        if (effects != null)
            foreach (SoilEffect e in effects)
                if ((int)e.channel >= SoilChannels.Count) errors.Add($"{name}: an effect on an unknown channel.");
    }
}

/// <summary>A release of living helpers (ladybirds, lacewings) over part of a field.</summary>
[Serializable]
public struct BeneficialRelease
{
    public Vector2 center;
    public float radius;
    public double until;
    public int amendment;
}
