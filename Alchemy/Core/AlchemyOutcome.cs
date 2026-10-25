using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A quality band of an outcome: Weak, Standard, Potent... each with its own item.</summary>
[Serializable]
public class AlchemyOutcomeTier
{
    [Tooltip("Shown after the name (\"Potent\"). Empty = no label.")]
    public string label = "";
    [Tooltip("Least quality (0-1) for this tier. Tiers are checked from the last one down.")]
    [Range(0f, 1f)] public float minQuality;
    [Tooltip("The item a bottle of this tier becomes. Empty = an item is made at runtime from the outcome's effects " +
             "(single player only: network sessions and saves need assets, see Alchemy ▸ Create Potion Items).")]
    public ItemSO item;
    [Tooltip("The outcome's effects × this (generated items, the Create Potion Items tool).")]
    [Min(0f)] public float effectMultiplier = 1f;

    public AlchemyOutcomeTier() { }

    public AlchemyOutcomeTier(string label, float minQuality, float effectMultiplier)
    {
        this.label = label;
        this.minQuality = minQuality;
        this.effectMultiplier = effectMultiplier;
    }
}

/// <summary>
/// What a preparation becomes, read from the actual state of the mixture - never from the ingredients used. Each
/// outcome lists requirements on the liquid (its main active compound, concentrations, properties, roles); the
/// matching outcome with the highest priority wins. Quality comes from potency (concentration of the Potency substance)
/// and purity (how much of the dissolved matter it is), so the same potion can be weak or superb.
/// <para>Failures are outcomes too (Incomplete, Degraded, Unstable, Murky...): every mixture becomes something.</para>
/// </summary>
[Serializable]
public class AlchemyOutcome
{
    [Tooltip("Stable id (journal, saves, items).")]
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    public AlchemyOutcomeKind kind = AlchemyOutcomeKind.Potion;
    [Tooltip("Outcomes are tried from the highest priority down; the first whose requirements are met is the result.")]
    public int priority = 50;
    [Tooltip("ALL must be met by the liquid for the preparation to be this.")]
    [SerializeReference, SubclassSelector] public List<AlchemyCondition> requirements = new List<AlchemyCondition>();

    [Header("Quality")]
    [Tooltip("What its strength is measured by (usually its active compound). Empty id = quality is always 1.")]
    public SubstanceSelector potency = new SubstanceSelector();
    [Tooltip("Share of the liquid (0-1) of the potency substance that gives full strength. Less = weaker.")]
    [Range(0.01f, 1f)] public float fullPotencyAt = 0.3f;
    [Tooltip("The quality bands, lowest first.")]
    public List<AlchemyOutcomeTier> tiers = new List<AlchemyOutcomeTier>();

    [Header("Items")]
    [Tooltip("A bottle holds this many units (0 = the database's Units Per Bottle).")]
    [Min(0f)] public float unitsPerBottle;
    [Tooltip("Off = nothing is bottled (plain water is just poured away).")]
    public bool producesItems = true;
    [Tooltip("What drinking it does (items made at runtime and by the Create Potion Items tool), × the tier's multiplier.")]
    public List<ConsumableEffect> effects = new List<ConsumableEffect>();
    [Tooltip("Value of one bottle at Standard quality (generated items).")]
    [Min(0f)] public float basePrice = 20f;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    /// <summary>The tier for <paramref name="quality"/> (index into <see cref="tiers"/>; -1 when there are none).</summary>
    public int TierFor(double quality)
    {
        if (tiers == null || tiers.Count == 0)
            return -1;
        for (int i = tiers.Count - 1; i >= 0; i--)
            if (tiers[i] != null && quality >= tiers[i].minQuality - 1e-9)
                return i;
        return 0;
    }

    /// <summary>
    /// How strong a bottle of <paramref name="quality"/> is (× the outcome's effects) - smoothly, not in steps: each
    /// tier's Effect Multiplier holds at its Min Quality and the strength runs on toward the next tier's (past the best
    /// tier, on at the same pace up to quality 1). A brew stronger than full strength (potency over 1, which quality does
    /// not count) gets up to a quarter more. So two "Potent" bottles of 68 % and 84 % are not the same.
    /// </summary>
    public double EffectMultiplierAt(double quality, double potency = 1)
    {
        if (tiers == null || tiers.Count == 0)
            return 1;
        var anchors = new List<AlchemyOutcomeTier>();
        foreach (AlchemyOutcomeTier t in tiers)
            if (t != null) anchors.Add(t);
        if (anchors.Count == 0)
            return 1;
        anchors.Sort((a, b) => a.minQuality.CompareTo(b.minQuality));
        double q = Math.Max(0, Math.Min(1, quality));
        double m;
        if (anchors.Count == 1 || q <= anchors[0].minQuality)
            m = anchors[0].effectMultiplier;
        else
        {
            int i = anchors.Count - 1;
            while (i > 0 && q < anchors[i].minQuality) i--;
            // From anchor i toward i + 1 (past the last one, at the pace of the last step).
            int a = i < anchors.Count - 1 ? i : i - 1;
            double span = Math.Max(1e-4, anchors[a + 1].minQuality - anchors[a].minQuality);
            double slope = (anchors[a + 1].effectMultiplier - anchors[a].effectMultiplier) / span;
            m = anchors[i].effectMultiplier + slope * (q - anchors[i].minQuality);
        }
        if (HasPotency && potency > 1)
            m *= 1 + 0.25 * Math.Min(1, potency - 1);
        return Math.Max(0, m);
    }

    /// <summary>"Healing Draught (Potent)".</summary>
    public string TierName(int tier)
    {
        if (tiers == null || tier < 0 || tier >= tiers.Count || tiers[tier] == null || string.IsNullOrWhiteSpace(tiers[tier].label))
            return Name;
        return $"{Name} ({tiers[tier].label})";
    }

    public bool HasPotency => potency != null && (potency.mode != SubstanceSelectorMode.Substance || !string.IsNullOrWhiteSpace(potency.id));

    // ------------------------------------------------------------------ building from code (defaults, tests)
    public AlchemyOutcome Requires(AlchemyCondition c)
    {
        requirements.Add(c);
        return this;
    }

    public AlchemyOutcome Tier(string label, float minQuality, float multiplier)
    {
        tiers.Add(new AlchemyOutcomeTier(label, minQuality, multiplier));
        return this;
    }

    public void Validate(AlchemyRules rules, List<string> errors, List<string> warnings)
    {
        string where = $"Outcome '{Name}'";
        if (string.IsNullOrWhiteSpace(id))
            errors.Add($"{where}: no id.");
        if (requirements != null)
            foreach (AlchemyCondition c in requirements)
                c?.Validate(rules, where, errors, warnings);
        if (HasPotency)
            AlchemyValidation.CheckSelector(rules, potency, where + " potency", errors, warnings);
        if (producesItems && (tiers == null || tiers.Count == 0))
            warnings.Add($"{where}: no tiers - every bottle is the same quality band.");
        if (tiers != null)
            for (int i = 1; i < tiers.Count; i++)
                if (tiers[i] != null && tiers[i - 1] != null && tiers[i].minQuality < tiers[i - 1].minQuality)
                    errors.Add($"{where}: tiers must go from the lowest Min Quality up.");
    }
}
