using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a reaction consumes: a selector and how many units per unit of reaction.</summary>
[Serializable]
public class ReactionReactant
{
    public SubstanceSelector substance = new SubstanceSelector();
    [Tooltip("Units consumed per unit of reaction.")]
    [Min(0.01f)] public float ratio = 1f;
    [Tooltip("How strongly the rate follows its concentration (1 = in proportion; 2 = needs it concentrated; 0 = only needs it present).")]
    [Range(0f, 3f)] public float order = 1f;

    public ReactionReactant() { }

    public ReactionReactant(SubstanceSelector substance, float ratio = 1f, float order = 1f)
    {
        this.substance = substance;
        this.ratio = ratio;
        this.order = order;
    }
}

/// <summary>What a reaction makes: a substance and how many units per unit of reaction.</summary>
[Serializable]
public class ReactionProduct
{
    public string substance = "";
    [Tooltip("Units made per unit of reaction (less than what is consumed = something is lost, more = it swells).")]
    [Min(0f)] public float ratio = 1f;

    public ReactionProduct() { }

    public ReactionProduct(string substance, float ratio = 1f)
    {
        this.substance = substance;
        this.ratio = ratio;
    }
}

/// <summary>
/// A substance that changes a reaction's speed without being used up: a catalyst (multiplier above 1), an inhibitor or
/// stabiliser (below 1), a blocker (0). Its effect grows with its concentration up to <see cref="fullEffectAt"/>.
/// </summary>
[Serializable]
public class ReactionModifier
{
    public SubstanceSelector substance = new SubstanceSelector();
    [Tooltip("Rate × this at full effect. 3 = three times faster (catalyst), 0.2 = five times slower (inhibitor), 0 = stops it.")]
    [Min(0f)] public float multiplier = 2f;
    [Tooltip("Share of the liquid (0-1) at which the full effect is reached; less gives part of it.")]
    [Range(0.001f, 1f)] public float fullEffectAt = 0.02f;

    public ReactionModifier() { }

    public ReactionModifier(SubstanceSelector substance, float multiplier, float fullEffectAt = 0.02f)
    {
        this.substance = substance;
        this.multiplier = multiplier;
        this.fullEffectAt = fullEffectAt;
    }

    /// <summary>The factor at concentration <paramref name="c"/>.</summary>
    public double Factor(double c)
    {
        if (c <= 0)
            return 1;
        double w = Math.Min(1.0, c / Math.Max(1e-6, fullEffectAt));
        return 1.0 + (multiplier - 1.0) * w;
    }
}

/// <summary>A condition that changes a reaction's speed while it is met: "twice as fast at night", "slower when stirred".</summary>
[Serializable]
public class ConditionalRate
{
    [SerializeReference, SubclassSelector] public AlchemyCondition condition;
    [Tooltip("Rate × this while the condition is met.")]
    [Min(0f)] public float multiplier = 2f;

    public ConditionalRate() { }

    public ConditionalRate(AlchemyCondition condition, float multiplier)
    {
        this.condition = condition;
        this.multiplier = multiplier;
    }
}

/// <summary>
/// One transformation rule: what it consumes, what it makes, how fast (by concentration and temperature), what speeds
/// it up or stops it, what else it needs, and the heat it gives off or takes. Reactions are general - they name
/// substances, properties or roles, never ingredients or recipes - so a new ingredient takes part in every reaction its
/// substances fit, and the same ingredient does different things depending on what is already in the vessel.
/// <para>Rate (units of reaction per second) = Rate × temperature factor × Π(concentration ^ order) × liquid volume ×
/// modifiers. The temperature factor is 0 outside [Min, Max] °C and doubles every Doubling degrees above Reference.</para>
/// </summary>
[Serializable]
public class AlchemyReaction
{
    [Tooltip("Stable id (journal, saves).")]
    public string id = "";
    [Tooltip("Name shown in the journal once the player understands it.")]
    public string displayName = "";
    [Tooltip("What is going on (shown in the journal once understood).")]
    [TextArea(1, 3)] public string description = "";
    [Tooltip("What the player sees while it happens before understanding it (\"the liquid blushes rose and smells of summer\").")]
    public string observation = "";
    public bool enabled = true;

    [Header("Substances")]
    public List<ReactionReactant> reactants = new List<ReactionReactant>();
    public List<ReactionProduct> products = new List<ReactionProduct>();

    [Header("Speed")]
    [Tooltip("Rate constant at the reference temperature (per second). Bigger = faster.")]
    [Min(0f)] public float rate = 0.05f;
    [Tooltip("Below this temperature (°C) it does not happen.")]
    public float minTemperature = -100f;
    [Tooltip("Above this temperature (°C) it does not happen.")]
    public float maxTemperature = 2000f;
    [Tooltip("Temperature (°C) the rate constant is given at.")]
    public float referenceTemperature = 20f;
    [Tooltip("The rate doubles every this many degrees above the reference (and halves below). 0 = temperature only gates " +
             "it. Negative = faster when colder.")]
    public float doublingDegrees = 20f;

    [Header("Modifiers")]
    [Tooltip("Substances that speed it up (×>1), slow it (×<1) or stop it (×0) without being used up.")]
    public List<ReactionModifier> modifiers = new List<ReactionModifier>();
    [Tooltip("Conditions that must ALL be met for it to happen (equipment, stirring, night...).")]
    [SerializeReference, SubclassSelector] public List<AlchemyCondition> conditions = new List<AlchemyCondition>();
    [Tooltip("Conditions that change its speed while met.")]
    public List<ConditionalRate> conditionalRates = new List<ConditionalRate>();
    [Tooltip("The defined random variable: the rate varies by up to ± this share each moment, from the preparation's seed " +
             "(the same seed and steps give the same result). 0 = fully predictable.")]
    [Range(0f, 1f)] public float rateVariance;

    [Header("Energy and effects")]
    [Tooltip("Heat given off per unit of reaction (°C × units of thermal mass; negative takes heat: it cools the mixture).")]
    public float heatPerUnit;
    [Tooltip("At this rate (units/s) or more it erupts: Spill Fraction of the contents splashes out (0 = never).")]
    [Min(0f)] public float eruptionRate;
    [Tooltip("Share of the vessel's contents lost in an eruption.")]
    [Range(0f, 1f)] public float spillFraction = 0.3f;
    [Tooltip("Presentation only: what the station shows while it runs.")]
    public ReactionVisual visual = ReactionVisual.Bubbles;

    /// <summary>Made by the rules from a substance's thermal stability (not in the database's list).</summary>
    [NonSerialized] public string generatedFrom;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public bool IsGenerated => !string.IsNullOrEmpty(generatedFrom);

    /// <summary>A copy whose modifier list can be extended without touching this one (the rules add interferences to copies).</summary>
    public AlchemyReaction CopyForRules()
    {
        var c = (AlchemyReaction)MemberwiseClone();
        c.modifiers = modifiers != null ? new List<ReactionModifier>(modifiers) : new List<ReactionModifier>();
        return c;
    }

    /// <summary>The temperature factor at <paramref name="t"/> (0 outside the window).</summary>
    public double TemperatureFactor(double t)
    {
        if (t < minTemperature || t > maxTemperature)
            return 0;
        if (Math.Abs(doublingDegrees) < 1e-4f)
            return 1;
        return Math.Min(64.0, Math.Pow(2.0, (t - referenceTemperature) / doublingDegrees));
    }

    // ------------------------------------------------------------------ building from code (defaults, tests)
    public AlchemyReaction Consumes(SubstanceSelector s, float ratio = 1f, float order = 1f)
    {
        reactants.Add(new ReactionReactant(s, ratio, order));
        return this;
    }

    public AlchemyReaction Consumes(string substance, float ratio = 1f, float order = 1f) => Consumes(SubstanceSelector.Of(substance), ratio, order);

    public AlchemyReaction Makes(string substance, float ratio = 1f)
    {
        products.Add(new ReactionProduct(substance, ratio));
        return this;
    }

    public AlchemyReaction Speed(float k, float reference, float doubling)
    {
        rate = k;
        referenceTemperature = reference;
        doublingDegrees = doubling;
        return this;
    }

    public AlchemyReaction Between(float min, float max)
    {
        minTemperature = min;
        maxTemperature = max;
        return this;
    }

    public AlchemyReaction ModifiedBy(SubstanceSelector s, float multiplier, float fullEffectAt = 0.02f)
    {
        modifiers.Add(new ReactionModifier(s, multiplier, fullEffectAt));
        return this;
    }

    public AlchemyReaction ModifiedBy(string substance, float multiplier, float fullEffectAt = 0.02f) =>
        ModifiedBy(SubstanceSelector.Of(substance), multiplier, fullEffectAt);

    public AlchemyReaction Needs(AlchemyCondition c)
    {
        conditions.Add(c);
        return this;
    }

    public AlchemyReaction FasterWhen(AlchemyCondition c, float multiplier)
    {
        conditionalRates.Add(new ConditionalRate(c, multiplier));
        return this;
    }

    public AlchemyReaction Heat(float perUnit)
    {
        heatPerUnit = perUnit;
        return this;
    }

    public AlchemyReaction Erupts(float atRate, float spill)
    {
        eruptionRate = atRate;
        spillFraction = spill;
        return this;
    }

    public AlchemyReaction Looks(ReactionVisual v, string seen)
    {
        visual = v;
        observation = seen;
        return this;
    }

    /// <summary>"Solar Sap → Vital Essence" (names of what it consumes and makes).</summary>
    public string Equation(AlchemyRules rules)
    {
        var lhs = new List<string>();
        foreach (ReactionReactant r in reactants)
            if (r?.substance != null)
                lhs.Add(r.substance.Describe(rules));
        var rhs = new List<string>();
        foreach (ReactionProduct p in products)
            if (p != null && !string.IsNullOrEmpty(p.substance))
                rhs.Add(rules != null ? rules.SubstanceName(p.substance) : p.substance);
        return (lhs.Count > 0 ? string.Join(" + ", lhs) : "?") + " → " + (rhs.Count > 0 ? string.Join(" + ", rhs) : "nothing");
    }

    /// <summary>
    /// What the journal says of it once understood: the equation, its temperature window, what speeds it up or stops
    /// it, and its other conditions.
    /// </summary>
    public string Describe(AlchemyRules rules)
    {
        var parts = new List<string> { Equation(rules) };
        bool hasMin = minTemperature > -99f, hasMax = maxTemperature < 1999f;
        if (hasMin && hasMax) parts.Add($"{minTemperature:0}–{maxTemperature:0} °C");
        else if (hasMin) parts.Add($"above {minTemperature:0} °C");
        else if (hasMax) parts.Add($"below {maxTemperature:0} °C");
        if (doublingDegrees > 0f && doublingDegrees < 60f) parts.Add("faster when hotter");
        else if (doublingDegrees < 0f && doublingDegrees > -60f) parts.Add("faster when colder");
        var faster = new List<string>();
        var slower = new List<string>();
        var stops = new List<string>();
        foreach (ReactionModifier m in modifiers)
        {
            if (m?.substance == null) continue;
            string n = m.substance.Describe(rules);
            if (m.multiplier <= 0.001f) stops.Add(n);
            else if (m.multiplier < 1f) slower.Add(n);
            else if (m.multiplier > 1f) faster.Add(n);
        }
        if (faster.Count > 0) parts.Add("faster with " + string.Join(", ", faster));
        if (slower.Count > 0) parts.Add("slowed by " + string.Join(", ", slower));
        if (stops.Count > 0) parts.Add("stopped by " + string.Join(", ", stops));
        string needs = AlchemyCondition.DescribeAll(conditions, rules);
        if (!string.IsNullOrEmpty(needs)) parts.Add("needs: " + needs);
        foreach (ConditionalRate c in conditionalRates)
            if (c?.condition != null)
                parts.Add($"×{c.multiplier:0.##} {c.condition.Describe(rules)}");
        if (heatPerUnit > 0.5f) parts.Add("gives off heat");
        else if (heatPerUnit < -0.5f) parts.Add("cools the mixture");
        if (eruptionRate > 0f) parts.Add("violent");
        return string.Join(" · ", parts);
    }

    public void Validate(AlchemyRules rules, List<string> errors, List<string> warnings)
    {
        string where = $"Reaction '{Name}'";
        if (string.IsNullOrWhiteSpace(id))
            errors.Add($"{where}: no id.");
        if (reactants == null || reactants.Count == 0)
            errors.Add($"{where}: consumes nothing.");
        else
            foreach (ReactionReactant r in reactants)
                AlchemyValidation.CheckSelector(rules, r?.substance, where, errors, warnings);
        if (products != null)
            foreach (ReactionProduct p in products)
                if (p == null || rules != null && rules.IndexOf(p.substance) < 0)
                    errors.Add($"{where}: unknown product '{p?.substance}'.");
        if (minTemperature > maxTemperature)
            errors.Add($"{where}: Min Temperature is above Max Temperature.");
        if (modifiers != null)
            foreach (ReactionModifier m in modifiers)
                AlchemyValidation.CheckSelector(rules, m?.substance, where + " modifier", errors, warnings);
        if (conditions != null)
            foreach (AlchemyCondition c in conditions)
                c?.Validate(rules, where, errors, warnings);
        if (rate <= 0f)
            warnings.Add($"{where}: Rate is 0, it never happens.");
    }
}
