using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ingredient in the vessel still giving up its substances. Each substance comes out at its own rate (from the
/// ingredient's form and the substance's extractability), so a whole herb can have given all its sap and little of its
/// bitter latex.
/// </summary>
public sealed class PendingIngredient
{
    /// <summary>Rate meaning "at once" (liquids, powders): finite so it saves as plain JSON.</summary>
    public const double Instant = 1e9;

    public string Id;
    public string Name;
    /// <summary>The form it went in as (whole, ground, extract...).</summary>
    public string Form;
    /// <summary>Substance index of each release.</summary>
    public readonly int[] Substances;
    /// <summary>Units of each substance it releases in total.</summary>
    public readonly double[] Totals;
    /// <summary>Units of each released so far.</summary>
    public readonly double[] Released;
    /// <summary>Share of each total released per second at 20 °C, still, in liquid (<see cref="Instant"/> = at once).</summary>
    public readonly double[] Rates;
    public readonly double DissolveSeconds;
    public double AddedAt;

    public PendingIngredient(string id, string name, string form, int[] substances, double[] totals, double[] rates, double dissolveSeconds,
        double addedAt, double[] released = null)
    {
        Id = id;
        Name = name;
        Form = form ?? "";
        Substances = substances;
        Totals = totals;
        Rates = rates;
        Released = released ?? new double[totals.Length];
        DissolveSeconds = dissolveSeconds;
        AddedAt = addedAt;
    }

    /// <summary>0 = just added, 1 = everything released.</summary>
    public double Progress
    {
        get
        {
            double t = 0, r = 0;
            for (int i = 0; i < Totals.Length; i++)
            {
                t += Totals[i];
                r += Released[i];
            }
            return t > 1e-9 ? r / t : 1;
        }
    }

    /// <summary>Units still inside the ingredient.</summary>
    public double Remaining
    {
        get
        {
            double t = 0;
            for (int i = 0; i < Totals.Length; i++) t += Math.Max(0, Totals[i] - Released[i]);
            return t;
        }
    }

    public bool Done
    {
        get
        {
            for (int i = 0; i < Totals.Length; i++)
                if (Totals[i] - Released[i] > 1e-9) return false;
            return true;
        }
    }
}

/// <summary>
/// The physical state of a preparation at one moment: how much of each substance is in the vessel (and as vapour in a
/// sealed one, and collected in an alembic's flask), the ingredients still dissolving, the temperature, agitation and
/// heat setting, the surroundings' variables and the time. <see cref="AlchemySimulator"/> moves it forward; conditions
/// and outcomes read it through <see cref="IAlchemyState"/>.
/// </summary>
public sealed class AlchemyMixture : IAlchemyState
{
    public AlchemyRules Rules { get; }
    public AlchemyEquipmentProfile Equipment { get; internal set; }

    internal readonly double[] amounts;
    /// <summary>What the liquid cannot hold (wrong or too little solvent, too cold, boiled down): settled or floating, not in solution.</summary>
    internal readonly double[] undissolved;
    internal readonly double[] vapor;
    internal readonly double[] distillate;
    internal readonly List<PendingIngredient> pending = new List<PendingIngredient>();
    internal readonly SortedDictionary<string, double> variables = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public double Temperature { get; internal set; }
    public double Agitation { get; internal set; }
    /// <summary>The heat setting: -1 full cooling, 0 off, 1 full heat (a manual fire uses its flames instead, see <see cref="EffectiveHeat"/>).</summary>
    public double Heat { get; internal set; }

    // Manual fire: fed with fuel, fanned with the bellows, choked with the air vent.
    /// <summary>The fire is tended by hand (fuel was added): its flames drive the heat, not the heat setting.</summary>
    public bool FireManual { get; internal set; }
    /// <summary>Fuel on the fire (units: one firewood ≈ 50 s at full fire).</summary>
    public double Fuel { get; internal set; }
    /// <summary>The flames, 0-1.</summary>
    public double FireLevel { get; internal set; }
    /// <summary>Air from the bellows, 0-1 (fades within seconds).</summary>
    public double Air { get; internal set; }
    /// <summary>The air vent, 0 (choked) - 1 (open).</summary>
    public double Damper { get; internal set; } = 1;

    // Continuous stirring.
    /// <summary>How fast it is being stirred now, 0-1 (0 = not stirring; a single stir is an impulse of agitation).</summary>
    public double StirSpeed { get; internal set; }
    /// <summary>How unevenly it has been stirred lately (changes of speed; fades over seconds).</summary>
    public double Turbulence { get; internal set; }
    /// <summary>Air beaten into the liquid by hard stirring, 0-1.</summary>
    public double Aeration { get; internal set; }
    /// <summary>How mixed two liquid layers are, 0 (apart) - 1 (emulsified).</summary>
    public double Emulsion { get; internal set; }
    /// <summary>What the vessel stands in.</summary>
    public AlchemyBath Bath { get; internal set; }

    // Temperature history (fire control): its recent average, how far it strays from it, how fast it cools.
    public double TemperatureMean { get; internal set; }
    public double Swing { get; internal set; }
    public double CoolingRate { get; internal set; }

    /// <summary>What drives the temperature now: the manual fire's flames (plus any cooling), else the heat setting.</summary>
    public double EffectiveHeat => FireManual ? FireLevel + Math.Min(0.0, Heat) : Heat;
    public double Ambient { get; internal set; }
    public double Elapsed { get; internal set; }
    /// <summary>Simulation steps done.</summary>
    public long Step { get; internal set; }
    /// <summary>A sealed vessel that burst: it vents from then on.</summary>
    public bool Ruptured { get; internal set; }

    public AlchemyMixture(AlchemyRules rules, AlchemyEquipmentProfile equipment, double ambient)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Equipment = equipment ?? AlchemyEquipmentProfile.DefaultCauldron();
        int n = rules.SubstanceCount;
        amounts = new double[n];
        undissolved = new double[n];
        vapor = new double[n];
        distillate = new double[n];
        Ambient = ambient;
        Temperature = ambient;
        TemperatureMean = ambient;
    }

    /// <summary>What happens to vapour now (a burst sealed vessel vents).</summary>
    public VaporHandling VaporMode => Ruptured && Equipment.vapor == VaporHandling.Sealed ? VaporHandling.Vent : Equipment.vapor;

    // ------------------------------------------------------------------ amounts
    public double Amount(int index) => index >= 0 && index < amounts.Length ? amounts[index] : 0;
    public double Amount(string substanceId) => Amount(Rules.IndexOf(substanceId));
    public double VaporAmount(int index) => index >= 0 && index < vapor.Length ? vapor[index] : 0;
    /// <summary>Units of substance <paramref name="index"/> that are not in solution (settled or floating).</summary>
    public double UndissolvedAmount(int index) => index >= 0 && index < undissolved.Length ? undissolved[index] : 0;
    public double DistillateAmount(int index) => index >= 0 && index < distillate.Length ? distillate[index] : 0;
    public IReadOnlyList<PendingIngredient> Pending => pending;

    public double Amount(SubstanceSelector selector)
    {
        double t = 0;
        foreach (int i in Rules.Match(selector))
            t += amounts[i];
        return t;
    }

    /// <summary>Everything in the vessel but the dissolving ingredients and the vapour: the liquid, solids, undissolved matter.</summary>
    public double Volume
    {
        get
        {
            double t = 0;
            for (int i = 0; i < amounts.Length; i++) t += amounts[i] + undissolved[i];
            return t;
        }
    }

    public double UndissolvedVolume
    {
        get
        {
            double t = 0;
            for (int i = 0; i < undissolved.Length; i++) t += undissolved[i];
            return t;
        }
    }

    /// <summary>The solvents in the liquid (units).</summary>
    public double SolventVolume
    {
        get
        {
            double t = 0;
            foreach (int k in Rules.Solvents) t += amounts[k];
            return t;
        }
    }

    /// <summary>
    /// Units of substance <paramref name="index"/> the liquid can hold now: the solvents' amount × the blend's
    /// solubility (log-linear: Σ share × log solubility), × 2 per Solubility Doubling Degrees above 20 °C. Infinite for
    /// substances without a limit; 0 without solvent.
    /// </summary>
    public double Capacity(int index)
    {
        if (index < 0 || !Rules.LimitedSolubility[index])
            return double.PositiveInfinity;
        int[] solvents = Rules.Solvents;
        double total = 0;
        for (int k = 0; k < solvents.Length; k++) total += amounts[solvents[k]];
        if (total <= 1e-9)
            return 0;
        double log = 0;
        for (int k = 0; k < solvents.Length; k++)
            log += amounts[solvents[k]] / total * Rules.LogSolubility[index, k];
        double capacity;
        if (Layers(out double aqueous, out double oily))
        {
            // Two layers hold what each holds: watery solvents in one, oily ones in the other (spirit shared).
            LayerLogSolubility(index, out double lw, out double lo);
            capacity = aqueous * Math.Exp(lw) + oily * Math.Exp(lo);
        }
        else
            capacity = total * Math.Exp(log);
        float doubling = Rules.Substances[index].solubilityDoublingDegrees;
        if (Math.Abs(doubling) > 1e-4f)
            capacity *= AlchemySimulator.Clamp(Math.Pow(2.0, (Temperature - 20.0) / doubling), 0.05, 50.0);
        int[][] curdledBy = Rules.CurdledBy[index];
        if (curdledBy.Length > 0)
        {
            double basis = Math.Max(1e-9, LiquidVolume);
            for (int k = 0; k < curdledBy.Length; k++)
            {
                double c = 0;
                foreach (int j in curdledBy[k]) c += amounts[j];
                capacity /= 1.0 + Rules.CurdleStrength[index][k] * c / basis * 100.0;
            }
        }
        return capacity;
    }

    // ------------------------------------------------------------------ layers, density, acidity
    /// <summary>
    /// Does the liquid form two layers (a watery and an oily solvent, each at least Layer Min Share of the solvent)? Gives
    /// the solvent units of each layer (spirit and other mixing solvents are shared in proportion).
    /// </summary>
    public bool Layers(out double aqueous, out double oily)
    {
        double w = 0, o = 0, mix = 0;
        foreach (int k in Rules.Solvents)
        {
            switch (Rules.Layer[k])
            {
                case LiquidLayer.Aqueous: w += amounts[k]; break;
                case LiquidLayer.Oily: o += amounts[k]; break;
                default: mix += amounts[k]; break;
            }
        }
        double total = w + o + mix;
        double min = Rules.Settings.layerMinShare * total;
        if (total <= 1e-9 || w <= 1e-9 || o <= 1e-9 || w < min || o < min)
        {
            aqueous = total;
            oily = 0;
            return false;
        }
        aqueous = w + mix * w / (w + o);
        oily = o + mix * o / (w + o);
        return true;
    }

    public bool IsLayered => Layers(out _, out _);

    /// <summary>The log of what one unit of each layer's solvent holds of substance <paramref name="index"/> (blend of that layer's solvents).</summary>
    private void LayerLogSolubility(int index, out double watery, out double oily)
    {
        double w = 0, o = 0, mix = 0;
        foreach (int k in Rules.Solvents)
        {
            switch (Rules.Layer[k])
            {
                case LiquidLayer.Aqueous: w += amounts[k]; break;
                case LiquidLayer.Oily: o += amounts[k]; break;
                default: mix += amounts[k]; break;
            }
        }
        double fw = w + o > 1e-9 ? w / (w + o) : 0.5;
        double sw = 0, so = 0, nw = 0, no = 0;
        int[] solvents = Rules.Solvents;
        for (int q = 0; q < solvents.Length; q++)
        {
            int k = solvents[q];
            double a = amounts[k];
            if (a <= 0) continue;
            double ls = Rules.LogSolubility[index, q];
            switch (Rules.Layer[k])
            {
                case LiquidLayer.Aqueous: sw += a * ls; nw += a; break;
                case LiquidLayer.Oily: so += a * ls; no += a; break;
                default:
                    sw += a * fw * ls; nw += a * fw;
                    so += a * (1 - fw) * ls; no += a * (1 - fw);
                    break;
            }
        }
        watery = nw > 1e-9 ? sw / nw : Math.Log(1e-4);
        oily = no > 1e-9 ? so / no : Math.Log(1e-4);
    }

    /// <summary>
    /// Share (0-1) of substance <paramref name="index"/> in the oily layer while the liquid is layered: solvents by their
    /// layer, other substances by where they dissolve best (or by the layers' volumes when they dissolve anywhere).
    /// </summary>
    public double OilShare(int index, double aqueous, double oily)
    {
        double byVolume = aqueous + oily > 1e-9 ? oily / (aqueous + oily) : 0;
        switch (Rules.Layer[index])
        {
            case LiquidLayer.Aqueous: return 0;
            case LiquidLayer.Oily: return 1;
        }
        if (Rules.Substances[index].role == SubstanceRole.Solvent || !Rules.LimitedSolubility[index])
            return byVolume;
        LayerLogSolubility(index, out double lw, out double lo);
        double inOil = oily * Math.Exp(lo), inWater = aqueous * Math.Exp(lw);
        return inOil + inWater > 1e-12 ? inOil / (inOil + inWater) : byVolume;
    }

    /// <summary>Units of liquid in the top and bottom layers and their densities (layered liquids only).</summary>
    public bool LayerSummary(out double topUnits, out double topDensity, out double bottomUnits, out double bottomDensity, out bool oilOnTop)
    {
        topUnits = topDensity = bottomUnits = bottomDensity = 0;
        oilOnTop = true;
        if (!Layers(out double aq, out double oil))
            return false;
        double ou = 0, om = 0, wu = 0, wm = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] <= 0 || Rules.Substances[i].phase != SubstancePhase.Liquid) continue;
            double p = OilShare(i, aq, oil);
            ou += amounts[i] * p;
            om += amounts[i] * p * Rules.Density[i];
            wu += amounts[i] * (1 - p);
            wm += amounts[i] * (1 - p) * Rules.Density[i];
        }
        double od = ou > 1e-9 ? om / ou : 0.9, wd = wu > 1e-9 ? wm / wu : 1;
        oilOnTop = od <= wd;
        topUnits = oilOnTop ? ou : wu;
        topDensity = oilOnTop ? od : wd;
        bottomUnits = oilOnTop ? wu : ou;
        bottomDensity = oilOnTop ? wd : od;
        return true;
    }

    /// <summary>The density of the liquid (dissolved liquids, by amount).</summary>
    public double LiquidDensity
    {
        get
        {
            double u = 0, mass = 0;
            for (int i = 0; i < amounts.Length; i++)
            {
                if (amounts[i] <= 0 || Rules.Substances[i].phase != SubstancePhase.Liquid) continue;
                u += amounts[i];
                mass += amounts[i] * Rules.Density[i];
            }
            return u > 1e-9 ? mass / u : 1;
        }
    }

    /// <summary>The liquid's pH (0-14, 7 neutral) from its acid and alkali properties.</summary>
    public double Ph => AlchemyComposition.Ph(Rules, amounts);

    /// <summary>
    /// Share (0-1) of the undissolved substance <paramref name="index"/> that floats in the liquid and would pour out with
    /// it: stirred-up particles, and those about as dense as the liquid; clarifiers (alum) make them settle.
    /// </summary>
    public double SuspendedShare(int index)
    {
        if (index < 0 || undissolved[index] <= 0)
            return 0;
        double diff = Math.Abs(Rules.Density[index] - LiquidDensity);
        double share = AlchemySimulator.Clamp((diff <= 0.08 ? 0.5 : 0.0) + Agitation * 1.5, 0, 1);
        double clarifier = PropertyValue(Rules.Settings.clarifierProperty);
        if (clarifier > 0)
            share /= 1.0 + Rules.Settings.clarifierStrength * clarifier;
        return share;
    }

    /// <summary>
    /// How dirty the liquid is: the contaminant property of what is dissolved and of the particles floating in it (and a
    /// third of what has settled at the bottom), per unit of liquid.
    /// </summary>
    public double ContaminantLevel()
    {
        int p = Rules.PropertyIndex(Rules.Settings.contaminantProperty);
        double liquid = LiquidVolume;
        if (p < 0 || liquid <= 1e-9)
            return 0;
        double sum = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            double v = Rules.PropertyMatrix[i, p];
            if (v <= 0) continue;
            double settled = Rules.Substances[i].phase == SubstancePhase.Solid ? amounts[i] : 0;
            double floating = undissolved[i] > 0 ? undissolved[i] * SuspendedShare(i) : 0;
            sum += v * (Rules.Substances[i].phase == SubstancePhase.Liquid ? amounts[i] + floating + (undissolved[i] - floating) / 3.0 : settled / 3.0);
        }
        return sum / liquid;
    }

    /// <summary>Is the undissolved substance lighter than the liquid (it floats: skim it)?</summary>
    public bool Floats(int index) => index >= 0 && Rules.Density[index] < LiquidDensity - 0.02;

    public double LiquidVolume
    {
        get
        {
            double t = 0;
            for (int i = 0; i < amounts.Length; i++)
                if (Rules.Substances[i].phase == SubstancePhase.Liquid) t += amounts[i];
            return t;
        }
    }

    public double SolidVolume => Volume - LiquidVolume;

    /// <summary>Units of ingredients still dissolving.</summary>
    public double PendingVolume
    {
        get
        {
            double t = 0;
            foreach (PendingIngredient p in pending) t += p.Remaining;
            return t;
        }
    }

    public double VaporVolume
    {
        get
        {
            double t = 0;
            for (int i = 0; i < vapor.Length; i++) t += vapor[i];
            return t;
        }
    }

    public double DistillateVolume
    {
        get
        {
            double t = 0;
            for (int i = 0; i < distillate.Length; i++) t += distillate[i];
            return t;
        }
    }

    /// <summary>Everything the vessel holds (contents, dissolving ingredients) — compared with the capacity.</summary>
    public double FilledVolume => Volume + PendingVolume;

    public bool IsEmpty => FilledVolume < Rules.Settings.traceAmount && VaporVolume < Rules.Settings.traceAmount;

    /// <summary>Thermal mass: the vessel plus its contents.</summary>
    public double ThermalMass => Equipment.vesselMass + FilledVolume * Rules.Settings.heatCapacityPerUnit;

    /// <summary>Pressure in a sealed vessel (0 = none): the vapour held, over the free space, × the absolute temperature.</summary>
    public double Pressure
    {
        get
        {
            if (VaporMode != VaporHandling.Sealed)
                return 0;
            // Vapour in the free space, hotter vapour pushing harder (ideal gas, relative to 100 °C).
            double free = Math.Max(1.0, Equipment.capacity - FilledVolume);
            double heat = Math.Max(0.1, (Temperature + 273.15) / 373.15);
            return VaporVolume / free * Rules.Settings.pressureScale * heat;
        }
    }

    // ------------------------------------------------------------------ IAlchemyState
    public bool HasTag(string tag) => Equipment.HasTag(tag);

    public double Variable(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;
        switch (name.ToLowerInvariant())
        {
            case AlchemyVariables.Temperature: return Temperature;
            case AlchemyVariables.Agitation: return Agitation;
            case AlchemyVariables.Pressure: return Pressure;
            case AlchemyVariables.Volume: return Volume;
            case AlchemyVariables.Liquid: return LiquidVolume;
            case AlchemyVariables.Time: return Elapsed;
            case AlchemyVariables.Heat: return EffectiveHeat;
            case AlchemyVariables.Ambient: return Ambient;
            case AlchemyVariables.Ph: return Ph;
            case AlchemyVariables.Fire: return FireManual ? FireLevel : Math.Max(0.0, Heat);
            case AlchemyVariables.Fuel: return Fuel;
            case AlchemyVariables.Turbulence: return Turbulence;
            case AlchemyVariables.Aeration: return Aeration;
            case AlchemyVariables.Emulsion: return Emulsion;
            case AlchemyVariables.Cooling: return CoolingRate;
            case AlchemyVariables.Swing: return Swing;
            case AlchemyVariables.Bath: return (int)Bath;
            case AlchemyVariables.Layered: return IsLayered ? 1 : 0;
        }
        if (variables.TryGetValue(name, out double v))
            return v;
        return Equipment.TryVariable(name, out double e) ? e : 0;
    }

    /// <summary>The surroundings' variables set on the preparation (night, humidity...).</summary>
    public IEnumerable<KeyValuePair<string, double>> Variables => variables;

    private double Basis
    {
        get
        {
            double l = LiquidVolume;
            return l > 1e-9 ? l : Volume;
        }
    }

    public double Concentration(SubstanceSelector selector)
    {
        double basis = Basis;
        return basis > 1e-9 ? Amount(selector) / basis : 0;
    }

    public double Concentration(int index)
    {
        double basis = Basis;
        return basis > 1e-9 && index >= 0 ? amounts[index] / basis : 0;
    }

    public double PropertyValue(string propertyId) => AlchemyComposition.PropertyValue(Rules, amounts, propertyId);

    public double RoleShare(SubstanceRole role, bool ofNonSolvent) => AlchemyComposition.RoleShare(Rules, amounts, role, ofNonSolvent);

    public int Dominant(SubstanceRole role) => AlchemyComposition.Dominant(Rules, amounts, role);

    /// <summary>The colour of the liquid (substances blended by amount × colour strength).</summary>
    public Color LiquidColor => AlchemyComposition.Color(Rules, amounts, true);

    /// <summary>A copy of the vessel contents (substance index → units).</summary>
    public double[] CopyAmounts() => (double[])amounts.Clone();

    public double[] CopyUndissolved() => (double[])undissolved.Clone();

    public double[] CopyDistillate() => (double[])distillate.Clone();
}

/// <summary>
/// Composition maths on an array of amounts (the vessel, a bottled portion, the distillate): properties, roles,
/// dominant compound, colour. A <see cref="AlchemySample"/> is a frozen portion that conditions can read.
/// </summary>
public static class AlchemyComposition
{
    public static double Liquid(AlchemyRules rules, double[] amounts)
    {
        double t = 0;
        for (int i = 0; i < amounts.Length; i++)
            if (rules.Substances[i].phase == SubstancePhase.Liquid) t += amounts[i];
        return t;
    }

    public static double PropertyValue(AlchemyRules rules, double[] amounts, string propertyId)
    {
        int p = rules.PropertyIndex(propertyId);
        if (p < 0)
            return 0;
        double liquid = 0, sum = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            if (rules.Substances[i].phase != SubstancePhase.Liquid || amounts[i] <= 0) continue;
            liquid += amounts[i];
            sum += amounts[i] * rules.PropertyMatrix[i, p];
        }
        return liquid > 1e-9 ? sum / liquid : 0;
    }

    /// <summary>pH = 7 - 7 × tanh((acid - alkali) / pH Scale), from the liquid's acid and alkali properties.</summary>
    public static double Ph(AlchemyRules rules, double[] amounts)
    {
        AlchemySimulationSettings s = rules.Settings;
        double net = PropertyValue(rules, amounts, s.acidProperty) - PropertyValue(rules, amounts, s.alkaliProperty);
        return 7.0 - 7.0 * Math.Tanh(net / Math.Max(0.001, s.phScale));
    }

    public static double RoleShare(AlchemyRules rules, double[] amounts, SubstanceRole role, bool ofNonSolvent)
    {
        double basis = 0, of = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            AlchemySubstance s = rules.Substances[i];
            if (s.phase != SubstancePhase.Liquid || amounts[i] <= 0) continue;
            if (ofNonSolvent && s.role == SubstanceRole.Solvent) continue;
            basis += amounts[i];
            if (s.role == role) of += amounts[i];
        }
        return basis > 1e-9 ? of / basis : 0;
    }

    public static int Dominant(AlchemyRules rules, double[] amounts, SubstanceRole role)
    {
        int best = -1;
        double most = rules.Settings.traceAmount;
        for (int i = 0; i < amounts.Length; i++)
        {
            AlchemySubstance s = rules.Substances[i];
            if (s.role != role || s.phase != SubstancePhase.Liquid) continue;
            if (amounts[i] > most)
            {
                most = amounts[i];
                best = i;
            }
        }
        return best;
    }

    public static Color Color(AlchemyRules rules, double[] amounts, bool liquidOnly)
    {
        double r = 0, g = 0, b = 0, w = 0;
        for (int i = 0; i < amounts.Length; i++)
        {
            AlchemySubstance s = rules.Substances[i];
            if (amounts[i] <= 0 || liquidOnly && s.phase != SubstancePhase.Liquid) continue;
            double k = amounts[i] * Math.Max(0.0, s.colorStrength);
            r += s.color.r * k;
            g += s.color.g * k;
            b += s.color.b * k;
            w += k;
        }
        if (w <= 1e-9)
            return new Color(0.5f, 0.55f, 0.6f, 0f);
        return new Color((float)(r / w), (float)(g / w), (float)(b / w), 1f);
    }
}

/// <summary>A frozen portion of a mixture (bottled liquid, the distillate) that conditions and outcomes can read.</summary>
public sealed class AlchemySample : IAlchemyState
{
    private readonly double[] amounts;
    private readonly IAlchemyState surroundings;

    public AlchemyRules Rules { get; }

    /// <param name="surroundings">Where variables, tags and the temperature come from (the vessel); may be null.</param>
    public AlchemySample(AlchemyRules rules, double[] amounts, IAlchemyState surroundings)
    {
        Rules = rules;
        this.amounts = amounts;
        this.surroundings = surroundings;
    }

    public double[] Amounts => amounts;
    public double Temperature => surroundings?.Temperature ?? Rules.Settings.defaultAmbient;
    public double Agitation => 0;
    public double Pressure => 0;
    public double Elapsed => surroundings?.Elapsed ?? 0;

    public double Volume
    {
        get
        {
            double t = 0;
            foreach (double a in amounts) t += a;
            return t;
        }
    }

    public double LiquidVolume => AlchemyComposition.Liquid(Rules, amounts);
    public double Variable(string name)
    {
        if (string.Equals(name, AlchemyVariables.Ph, StringComparison.OrdinalIgnoreCase))
            return AlchemyComposition.Ph(Rules, amounts); // the portion's own (a distillate is not the vessel)
        return surroundings?.Variable(name) ?? 0;
    }
    public bool HasTag(string tag) => surroundings != null && surroundings.HasTag(tag);
    public double Amount(int index) => index >= 0 && index < amounts.Length ? amounts[index] : 0;

    public double Amount(SubstanceSelector selector)
    {
        double t = 0;
        foreach (int i in Rules.Match(selector)) t += amounts[i];
        return t;
    }

    public double Concentration(SubstanceSelector selector)
    {
        double l = LiquidVolume;
        double basis = l > 1e-9 ? l : Volume;
        return basis > 1e-9 ? Amount(selector) / basis : 0;
    }

    public double PropertyValue(string propertyId) => AlchemyComposition.PropertyValue(Rules, amounts, propertyId);
    public double RoleShare(SubstanceRole role, bool ofNonSolvent) => AlchemyComposition.RoleShare(Rules, amounts, role, ofNonSolvent);
    public int Dominant(SubstanceRole role) => AlchemyComposition.Dominant(Rules, amounts, role);
    public Color LiquidColor => AlchemyComposition.Color(Rules, amounts, true);
}
