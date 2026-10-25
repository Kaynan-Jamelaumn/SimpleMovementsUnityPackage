using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a liquid is: its outcome, quality band and quality (from potency and purity), how many bottles it fills, and
/// why (notes such as "diluted", "impure", "raw ingredients left"). Made by <see cref="AlchemyEvaluator"/>.
/// </summary>
public sealed class AlchemyResult
{
    public AlchemyOutcome Outcome;
    public string OutcomeId => Outcome != null ? Outcome.id : "";
    public AlchemyOutcomeKind Kind => Outcome != null ? Outcome.kind : AlchemyOutcomeKind.Failed;
    /// <summary>Index into the outcome's tiers (-1 = it has none).</summary>
    public int Tier = -1;
    /// <summary>0-1.</summary>
    public double Quality;
    /// <summary>Concentration of the potency substance over the full-strength one (can exceed 1).</summary>
    public double Potency;
    /// <summary>Share of the dissolved (non-solvent) matter that is the potency substance.</summary>
    public double Purity;
    /// <summary>Units of liquid evaluated.</summary>
    public double LiquidUnits;
    /// <summary>Bottles it fills (or, after <see cref="AlchemyBrew.Bottle"/>, the bottles taken).</summary>
    public int Bottles;
    /// <summary>Units removed by <see cref="AlchemyBrew.Bottle"/>.</summary>
    public double UnitsTaken;
    public Color LiquidColor;
    /// <summary>Why it came out like this.</summary>
    public readonly List<string> Notes = new List<string>();

    public string DisplayName => Outcome != null ? Outcome.TierName(Tier) : "Nothing";

    public AlchemyOutcomeTier TierData =>
        Outcome?.tiers != null && Tier >= 0 && Tier < Outcome.tiers.Count ? Outcome.tiers[Tier] : null;

    public bool IsFailure => Kind != AlchemyOutcomeKind.Potion && Kind != AlchemyOutcomeKind.Material;

    public override string ToString() => $"{DisplayName} q{Quality:0.00} ×{Bottles}";
}

/// <summary>
/// Reads a liquid's actual composition and decides what it is: the first outcome (highest priority first) whose
/// requirements the liquid meets, then its quality = potency × (1 − purity influence × impurity), and the tier for that
/// quality. Outcomes are classifiers of the mixture's state, not recipes: any way of reaching the state gives the same
/// result, and the same ingredients give something else when the process changed the state.
/// </summary>
public static class AlchemyEvaluator
{
    private static AlchemyOutcome murky;

    /// <summary>The outcome given when a mixture matches none (a database without a catch-all outcome).</summary>
    public static AlchemyOutcome FallbackOutcome => murky ??= new AlchemyOutcome
    {
        id = "murky_mixture",
        displayName = "Murky Mixture",
        description = "A cloudy liquid that is nothing in particular.",
        kind = AlchemyOutcomeKind.Failed,
        priority = int.MinValue,
        tiers = new List<AlchemyOutcomeTier> { new AlchemyOutcomeTier("", 0f, 1f) },
        basePrice = 1f,
    };

    public static AlchemyResult Evaluate(AlchemyRules rules, double[] amounts, IAlchemyState surroundings)
    {
        var liquid = new double[amounts.Length];
        for (int i = 0; i < amounts.Length; i++)
            if (rules.Substances[i].phase == SubstancePhase.Liquid)
                liquid[i] = amounts[i];
        var sample = new AlchemySample(rules, liquid, surroundings);
        var r = new AlchemyResult { LiquidUnits = sample.LiquidVolume, LiquidColor = sample.LiquidColor };

        foreach (AlchemyOutcome o in rules.Outcomes)
        {
            if (o != null && AlchemyCondition.AllMet(o.requirements, sample))
            {
                r.Outcome = o;
                break;
            }
        }
        if (r.Outcome == null)
            r.Outcome = FallbackOutcome;

        Grade(rules, r, sample);
        double per = rules.UnitsPerBottle(r.Outcome);
        r.Bottles = r.Outcome.producesItems ? Bottles(r.LiquidUnits, per, rules.Settings.minFillForBottle) : 0;
        Explain(rules, r, sample, surroundings as AlchemyMixture);
        return r;
    }

    /// <summary>Bottles <paramref name="units"/> fill (the last one only when at least Min Fill of it is there).</summary>
    public static int Bottles(double units, double perBottle, double minFill)
    {
        if (units <= 1e-9 || perBottle <= 0)
            return 0;
        double n = units / perBottle;
        int whole = (int)Math.Floor(n + 1e-9);
        if (n - whole >= minFill - 1e-9) whole++;
        return whole;
    }

    private static void Grade(AlchemyRules rules, AlchemyResult r, AlchemySample sample)
    {
        AlchemyOutcome o = r.Outcome;
        if (!o.HasPotency)
        {
            r.Potency = 1;
            r.Purity = 1;
            r.Quality = 1;
        }
        else
        {
            double c = sample.Concentration(o.potency);
            double dissolved = 0;
            double liquid = sample.LiquidVolume;
            double[] a = sample.Amounts;
            for (int i = 0; i < a.Length; i++)
                if (rules.Substances[i].role != SubstanceRole.Solvent && rules.Substances[i].phase == SubstancePhase.Liquid)
                    dissolved += a[i];
            r.Potency = c / Math.Max(0.001, o.fullPotencyAt);
            double ofPotency = c * liquid;
            r.Purity = dissolved > 1e-9 ? Math.Min(1.0, ofPotency / dissolved) : 0;
            double purityInfluence = rules.Settings.purityInfluence;
            r.Quality = AlchemySimulator.Clamp(Math.Min(1.0, r.Potency) * (1.0 - purityInfluence * (1.0 - r.Purity)), 0, 1);
        }
        r.Tier = o.TierFor(r.Quality);
    }

    private static void Explain(AlchemyRules rules, AlchemyResult r, AlchemySample sample, AlchemyMixture vessel)
    {
        if (r.LiquidUnits <= rules.Settings.traceAmount)
        {
            r.Notes.Add("There is no liquid to bottle.");
            return;
        }
        if (r.Outcome.HasPotency && (r.Kind == AlchemyOutcomeKind.Potion || r.Kind == AlchemyOutcomeKind.Material))
        {
            if (r.Potency < 0.5) r.Notes.Add("Weak: the active compound is diluted.");
            else if (r.Potency > 1.15) r.Notes.Add("Concentrated: stronger than a standard brew.");
            if (r.Purity < 0.6) r.Notes.Add("Impure: other compounds are mixed in.");
        }
        if (vessel != null && vessel.PendingVolume > 0.25)
        {
            // Mostly given up, the rest locked in (a whole herb's bitter latex): say so, or players would wait for it.
            bool releasing = false;
            foreach (PendingIngredient p in vessel.Pending)
                releasing |= p.Progress < 0.85;
            r.Notes.Add(releasing ? "Some of the ingredients do not look spent yet."
                                  : "The ingredients look nearly spent.");
        }
        if (vessel != null && vessel.UndissolvedVolume > 0.25)
            r.Notes.Add("Part of it does not seem to dissolve.");
        if (vessel != null)
        {
            double suspended = 0;
            for (int i = 0; i < rules.SubstanceCount; i++)
                if (vessel.UndissolvedAmount(i) > 0)
                    suspended += vessel.UndissolvedAmount(i) * vessel.SuspendedShare(i);
            if (suspended > 0.2)
                r.Notes.Add("It looks cloudy: something seems to drift in it.");
            if (vessel.IsLayered)
                r.Notes.Add(vessel.Emulsion > 0.5 ? "Its two parts seem blended into one, for now." : "It appears to sit in two layers.");
            double dirt = sample.PropertyValue(rules.Settings.contaminantProperty);
            if (dirt >= 0.01)
                r.Notes.Add("It looks gritty, as if dirt got into it.");
        }
        double raw = sample.RoleShare(SubstanceRole.Raw, true);
        if (raw > 0.3 && r.Kind != AlchemyOutcomeKind.Incomplete)
            r.Notes.Add("Much of it is still raw ingredient matter.");
        double residue = sample.RoleShare(SubstanceRole.Residue, true);
        if (residue > 0.2 && r.Kind != AlchemyOutcomeKind.Degraded)
            r.Notes.Add("Residues are spoiling it.");
    }

    /// <summary>
    /// What can be harvested from <paramref name="amounts"/>: every substance of <paramref name="phase"/> with at least
    /// one item's worth of units (an item 2 % short still counts: what boiled dry is never quite all there).
    /// <see cref="AlchemyHarvestItem.Item"/> is its Harvest Item, or null when it has none
    /// (the station may still give a matching item, see <see cref="AlchemyItems.HarvestItem"/>).
    /// </summary>
    public static void Harvest(AlchemyRules rules, double[] amounts, SubstancePhase phase, List<AlchemyHarvestItem> into)
    {
        for (int i = 0; i < amounts.Length; i++)
        {
            AlchemySubstance s = rules.Substances[i];
            if (s.phase != phase || amounts[i] <= 0 || s.role == SubstanceRole.Solvent && s.harvestItem == null)
                continue;
            int count = (int)Math.Floor(amounts[i] / Math.Max(0.01, s.unitsPerItem) + 0.02);
            if (count > 0)
                into.Add(new AlchemyHarvestItem { Substance = s.id, Item = s.harvestItem, Count = count });
        }
    }

    /// <summary>
    /// What harvesting would give by substance, with or without harvest items (the station window shows "3 Moon
    /// Crystals at the bottom" even when the project has no item for them yet).
    /// </summary>
    public static void HarvestableUnits(AlchemyRules rules, double[] amounts, SubstancePhase phase, List<(int substance, double units)> into)
    {
        for (int i = 0; i < amounts.Length; i++)
            if (rules.Substances[i].phase == phase && amounts[i] > rules.Settings.noticeableAmount)
                into.Add((i, amounts[i]));
    }
}
