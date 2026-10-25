using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a composition makes of a metal: its base, density, melting range, colour, hardness, toughness, ductility,
/// corrosion resistance, castability, how it hardens (steel by carbon, some metals of legend by a quench), its forging
/// window, where it burns, how it scales, its magic and affinities - and its name (<see cref="CraftMaterialClass"/>).
/// Computed from the elements and their <see cref="CraftAlloyEffect"/>s, never from what the metal was called.
/// </summary>
public sealed class CraftAlloyProfile
{
    public double[] Fractions;
    /// <summary>Index of the base element (-1 = nothing).</summary>
    public int Base = -1;
    public CraftElement BaseElement;
    public double Density = 7.8;
    /// <summary>Starts to melt (°C), and is fully liquid.</summary>
    public double Solidus = 1500, Liquidus = 1520;
    public double Hardness = 80, Toughness = 60, Ductility = 30, Corrosion = 0.3, Castability = 0.5;
    public double Magic, Supernatural, WorkHardening = 60, QuenchHardness;
    public double HotShortness, ColdShortness;
    /// <summary>Multiplies how long a cooling steel takes to start forming pearlite (more = easier to harden).</summary>
    public double Hardenability = 1;
    /// <summary>Carbon (fraction) when the base is the steel base.</summary>
    public double Carbon;
    /// <summary>It hardens by quenching: a steel (enough carbon) or a metal with Quench Hardness.</summary>
    public bool Steel, HardensByQuench, CastIron;
    /// <summary>Above this it turns into austenite (steel) or into a solution that a quench can freeze (others), °C.</summary>
    public double CriticalTemperature = 900;
    /// <summary>Martensite starts forming below this (°C).</summary>
    public double MartensiteStart = 220;
    public double OxidationOnset = 570, OxidationRate = 1, Recrystallization = 550;
    public double HotWorkMin = 800, HotWorkMax = 1250, BurnTemperature = 1350, GrainGrowth = 1000;
    public bool Magnetic;
    public double Curie = 770;
    public Color Color = new Color(0.6f, 0.6f, 0.62f);
    public ElementType Affinity = ElementType.None;
    public double AffinityStrength;
    public double PricePerGram = 0.01;
    public CraftMaterialClass Class;

    public string ClassName => Class != null ? Class.Name : BaseElement != null ? $"{BaseElement.Name} alloy" : "Unknown metal";

    /// <summary>The fraction of an element (by index).</summary>
    public double F(int i) => Fractions != null && i >= 0 && i < Fractions.Length ? Fractions[i] : 0;
}

/// <summary>
/// The physical metallurgy of the simulation: alloy profiles from compositions, the steel model (austenite, pearlite
/// by a TTT-like incubation, martensite below Ms, tempering by the Hollomon-Jaffe parameter, grain growth, burning),
/// hardness, toughness and ductility of a segment's zones, the colours of heat and of temper films, the spark test.
/// Pure and deterministic.
/// </summary>
public static class CraftMetallurgy
{
    // ------------------------------------------------------------------ alloys
    /// <summary>What a composition (mass fractions over the rules' elements) makes of a metal.</summary>
    public static CraftAlloyProfile Analyze(CraftRules rules, double[] f)
    {
        var p = new CraftAlloyProfile { Fractions = f };
        int n = rules.ElementCount;
        if (f == null || f.Length != n || n == 0)
            return p;
        // The base: the most abundant metal.
        double best = 0;
        for (int i = 0; i < n; i++)
        {
            CraftElement e = rules.Elements[i];
            if ((e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy) && f[i] > best)
            {
                best = f[i];
                p.Base = i;
            }
        }
        if (p.Base < 0)
            for (int i = 0; i < n; i++)
                if (f[i] > best) { best = f[i]; p.Base = i; }
        if (p.Base < 0)
            return p;
        CraftElement b = rules.Elements[p.Base];
        p.BaseElement = b;

        // Density by volume; colour by mass of the metals.
        double inv = 0, cr = 0, cg = 0, cb = 0, cw = 0, price = 0;
        for (int i = 0; i < n; i++)
        {
            if (f[i] <= 0) continue;
            CraftElement e = rules.Elements[i];
            inv += f[i] / Math.Max(0.1, e.density);
            price += f[i] * e.pricePerGram;
            if (e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy)
            {
                cr += e.color.r * f[i];
                cg += e.color.g * f[i];
                cb += e.color.b * f[i];
                cw += f[i];
            }
        }
        p.Density = inv > 0 ? 1.0 / inv : b.density;
        p.PricePerGram = price;
        Color color = cw > 0 ? new Color((float)(cr / cw), (float)(cg / cw), (float)(cb / cw)) : b.color;

        // Properties: the base's, the other metals mixed in by mass, then the effects of what is alloyed in.
        p.Hardness = Mixed(rules, f, p.Base, CraftProperty.Hardness);
        p.Toughness = Math.Max(0, Mixed(rules, f, p.Base, CraftProperty.Toughness));
        p.Ductility = Math.Max(0, Mixed(rules, f, p.Base, CraftProperty.Ductility));
        p.Corrosion = CraftMath.Clamp01(Mixed(rules, f, p.Base, CraftProperty.Corrosion));
        p.Castability = CraftMath.Clamp01(Mixed(rules, f, p.Base, CraftProperty.Castability));
        p.Magic = CraftMath.Clamp01(Mixed(rules, f, p.Base, CraftProperty.Magic));
        p.Supernatural = CraftMath.Clamp01(Mixed(rules, f, p.Base, CraftProperty.Supernatural));
        p.WorkHardening = Math.Max(0, Mixed(rules, f, p.Base, CraftProperty.WorkHardening));
        p.QuenchHardness = Math.Max(0, Effects(rules, f, p.Base, CraftProperty.QuenchHardness));
        p.HotShortness = Math.Max(0, Effects(rules, f, p.Base, CraftProperty.HotShortness));
        p.ColdShortness = CraftMath.Clamp(Effects(rules, f, p.Base, CraftProperty.ColdShortness), 0, 0.95);
        double melt = Mixed(rules, f, p.Base, CraftProperty.MeltingPoint);
        double minor = 1 - f[p.Base];
        p.Solidus = Math.Max(150, melt);
        p.Liquidus = p.Solidus + Math.Min(140, minor * 400);
        p.Hardenability = 1;
        foreach (var (el, e) in rules.EffectsOn(p.Base, CraftProperty.Hardenability))
            p.Hardenability *= Math.Max(0.05, 1 + e.At(f[el]));
        foreach (var (el, e) in rules.EffectsOn(p.Base, CraftProperty.Tint))
        {
            double k = CraftMath.Clamp01(e.At(f[el]));
            color = Color.Lerp(color, e.tint, (float)k);
        }
        p.Color = color;

        // How it is worked.
        p.OxidationOnset = b.oxidationOnset;
        p.OxidationRate = b.oxidationRate * (1 - 0.8 * Math.Max(0, p.Corrosion - b.corrosion));
        p.Recrystallization = b.recrystallization;
        p.HotWorkMin = b.hotWorkMin;
        p.HotWorkMax = b.hotWorkMax;
        p.BurnTemperature = Math.Min(b.burnTemperature, p.Solidus - 40);
        p.GrainGrowth = b.grainGrowth;
        p.Magnetic = b.magnetic;
        p.Curie = b.curieTemperature;

        // Steel: hardenable by carbon.
        CraftSimulationSettings s = rules.Settings;
        if (p.Base == rules.SteelBase && rules.SteelCarbon >= 0)
        {
            double c = f[rules.SteelCarbon];
            p.Carbon = c;
            p.CastIron = c >= s.castIronCarbon;
            p.Steel = c >= s.hardenableCarbon && !p.CastIron;
            p.CriticalTemperature = c < s.eutectoidCarbon
                ? s.a3Iron - (s.a3Iron - s.a1) * c / s.eutectoidCarbon
                : s.a1 + (c - s.eutectoidCarbon) / Math.Max(1e-6, s.castIronCarbon - s.eutectoidCarbon) * (1148 - s.a1);
            p.MartensiteStart = MartensiteStart(c);
            // Carbon lowers where steel burns and where its grain grows.
            p.BurnTemperature = Math.Min(p.BurnTemperature, b.burnTemperature - 25000 * c);
            p.HotWorkMax = Math.Min(p.HotWorkMax, p.BurnTemperature - 90);
            p.GrainGrowth = Math.Min(p.GrainGrowth, p.CriticalTemperature + 120);
            p.HardensByQuench = p.Steel;
        }
        else if (p.QuenchHardness > 1)
        {
            p.HardensByQuench = true;
            p.CriticalTemperature = p.Recrystallization + 150;
            p.MartensiteStart = p.Recrystallization;
        }

        // Affinities: the strongest (fantasy metals carry theirs even in small amounts).
        double strongest = 0;
        for (int i = 0; i < n; i++)
        {
            CraftElement e = rules.Elements[i];
            if (f[i] <= 0 || e.affinity == ElementType.None) continue;
            double a = Math.Min(1, f[i] * e.affinityStrength * 3);
            if (a > strongest)
            {
                strongest = a;
                p.Affinity = e.affinity;
            }
        }
        p.AffinityStrength = strongest;
        p.Class = Classify(rules, f, p.Base);
        return p;
    }

    /// <summary>The base's value, the other metals mixed in by mass, and every alloy effect.</summary>
    private static double Mixed(CraftRules rules, double[] f, int baseIndex, CraftProperty property)
    {
        CraftElement b = rules.Elements[baseIndex];
        double v = b.BaseValue(property);
        for (int i = 0; i < f.Length; i++)
        {
            if (i == baseIndex || f[i] <= 0) continue;
            CraftElement e = rules.Elements[i];
            if (e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy)
                v += (e.BaseValue(property) - b.BaseValue(property)) * f[i];
        }
        return v + Effects(rules, f, baseIndex, property);
    }

    private static double Effects(CraftRules rules, double[] f, int baseIndex, CraftProperty property)
    {
        double v = 0;
        foreach (var (el, e) in rules.EffectsOn(baseIndex, property))
            if (el != baseIndex)
                v += e.At(f[el]);
        return v;
    }

    /// <summary>The name of a composition: the first material class (by priority) it falls into.</summary>
    public static CraftMaterialClass Classify(CraftRules rules, double[] f, int baseIndex)
    {
        if (baseIndex < 0)
            return null;
        string baseId = rules.Elements[baseIndex].id;
        foreach (CraftMaterialClass c in rules.Classes)
        {
            if (!string.Equals(c.baseElement, baseId, StringComparison.OrdinalIgnoreCase))
                continue;
            bool ok = true;
            if (c.ranges != null)
                foreach (CraftElementRange r in c.ranges)
                {
                    int i = rules.IndexOf(r.element);
                    if (i < 0 || !r.Contains(f[i]))
                    {
                        ok = false;
                        break;
                    }
                }
            if (ok)
                return c;
        }
        return null;
    }

    // ------------------------------------------------------------------ the steel model
    /// <summary>Carbon (fraction) in a zone: the piece's, its segment's shift, less what the surface lost (thin parts lose most).</summary>
    public static double ZoneCarbon(CraftAlloyProfile p, MetalSegment s, CraftZone z, double thickness)
    {
        double c = Math.Max(0, p.Carbon + s.carbonShift);
        double surface = Math.Min(1, 1.2 / Math.Max(0.3, thickness));
        return c * (1 - CraftMath.Clamp01(z.decarb) * surface);
    }

    /// <summary>Where martensite starts forming (°C) at a carbon fraction (flattening above 0.6 %).</summary>
    public static double MartensiteStart(double carbon)
    {
        double pct = carbon * 100;
        return Math.Max(90, 539 - 423 * Math.Min(pct, 0.6) - 150 * Math.Max(0, pct - 0.6));
    }

    /// <summary>Hardness of fresh martensite (HV) at a carbon fraction.</summary>
    public static double MartensiteHardness(double carbon)
    {
        double pct = carbon * 100;
        return Math.Min(880, 120 + 950 * (1 - Math.Exp(-2.3 * pct)));
    }

    /// <summary>Hardness of ferrite and pearlite (HV): coarse (annealed) to fine (normalized).</summary>
    public static double PearliteHardness(double carbon, double fine)
    {
        double pct = carbon * 100;
        double h = 95 + 110 * Math.Min(pct, 0.8) / 0.8 + 30 * Math.Max(0, pct - 0.8);
        return h * (1 + 0.4 * CraftMath.Clamp01(fine));
    }

    /// <summary>How far martensite is tempered (0 untempered - 1 fully, as at 650 °C for an hour), from its Hollomon-Jaffe parameter.</summary>
    public static double TemperFraction(double temperP) => temperP <= 0 ? 0 : CraftMath.Clamp01((temperP - 7600) / (18460 - 7600));

    /// <summary>
    /// The tempering parameter after <paramref name="seconds"/> more at <paramref name="celsius"/>, from
    /// <paramref name="p"/> (time at different temperatures adds up through the equivalent time).
    /// </summary>
    public static double Temper(double p, double celsius, double seconds, double timeScale)
    {
        if (celsius < 120 || seconds <= 0)
            return p;
        double k = celsius + 273.15;
        double hours = seconds * timeScale / 3600.0;
        double tEq = p > 0 ? Math.Pow(10, p / k - 20) : 0;
        double next = k * (20 + Math.Log10(Math.Max(1e-9, tEq + hours)));
        return Math.Max(p, next);
    }

    /// <summary>Seconds pearlite needs to start forming at <paramref name="celsius"/> in steel of <paramref name="carbon"/> (the TTT curve's nose at 550 °C).</summary>
    public static double PearliteIncubation(CraftSimulationSettings s, CraftAlloyProfile p, double carbon, double celsius, double grain)
    {
        double eut = s.eutectoidCarbon;
        double c = carbon < eut ? Math.Pow(Math.Max(0.02, carbon / eut), 1.5) : Math.Max(0.4, 1 - 0.3 * (carbon - eut) * 100);
        double nose = s.pearliteNoseSeconds * c * p.Hardenability * Math.Sqrt(Math.Max(1, grain));
        double x = (celsius - 550) / 120;
        return nose * Math.Exp(x * x);
    }

    // ------------------------------------------------------------------ properties of a zone
    /// <summary>Hardness (HV) of a zone of a segment.</summary>
    public static double Hardness(CraftRules rules, CraftAlloyProfile p, MetalSegment s, CraftZone z, double thickness)
    {
        double h;
        if (p.Steel)
        {
            double c = ZoneCarbon(p, s, z, thickness);
            double hm = MartensiteHardness(c);
            double floor = PearliteHardness(c, 0.2) * 1.05;
            double ht = hm - (hm - floor) * TemperFraction(z.temperP);
            double hp = PearliteHardness(c, z.fine);
            h = z.mart * ht + z.pearl * hp + z.aust * 250;
            // Other alloying elements harden the ferrite (phosphorus, manganese, nickel...).
            h += Math.Max(0, p.Hardness - p.BaseElement.hardness);
        }
        else
        {
            h = p.Hardness;
            if (p.HardensByQuench)
                h += p.QuenchHardness * z.mart * (1 - 0.5 * TemperFraction(z.temperP));
        }
        h += p.WorkHardening * z.cold / (1 + 0.4 * z.cold);
        h -= 8 * Math.Max(0, z.grain - 1);
        h *= 1 - 0.5 * CraftMath.Clamp01(z.burnt);
        return Math.Max(5, h);
    }

    /// <summary>Toughness (J) of a zone of a segment (before slag, porosity and cracks, which the evaluator adds).</summary>
    public static double Toughness(CraftRules rules, CraftAlloyProfile p, MetalSegment s, CraftZone z, double thickness)
    {
        double b = p.Toughness, t;
        if (p.Steel || p.HardensByQuench)
        {
            double x = TemperFraction(z.temperP);
            // Blue brittleness: a dip around a third of the way (tempering near 300 °C).
            double dip = 0.25 * Math.Exp(-Math.Pow((x - 0.33) / 0.09, 2));
            double tm = b * (0.06 + 0.8 * Math.Pow(x, 0.8) * (1 - dip));
            double tp = b * (0.75 + 0.35 * z.fine);
            t = z.mart * tm + z.pearl * tp + z.aust * b;
        }
        else
            t = b;
        t *= 1 - 0.3 * Math.Min(1, z.cold);
        t /= 1 + 0.35 * Math.Max(0, z.grain - 1);
        t *= 1 - 0.9 * CraftMath.Clamp01(z.burnt);
        t *= 1 - 0.3 * Math.Min(1, z.stress);
        if (z.temp < 200)
            t *= 1 - p.ColdShortness;
        return Math.Max(0.5, t);
    }

    /// <summary>Ductility (% elongation) of a zone.</summary>
    public static double Ductility(CraftRules rules, CraftAlloyProfile p, MetalSegment s, CraftZone z, double thickness)
    {
        double d = p.Ductility;
        if (p.Steel || p.HardensByQuench)
        {
            double x = TemperFraction(z.temperP);
            d = z.pearl * d + z.mart * d * (0.03 + 0.5 * x) + z.aust * d;
        }
        d *= 1 / (1 + 1.2 * z.cold);
        d /= 1 + 0.25 * Math.Max(0, z.grain - 1);
        d *= 1 - 0.9 * CraftMath.Clamp01(z.burnt);
        return Math.Max(0, d);
    }

    // ------------------------------------------------------------------ what heat looks like
    private static readonly (double t, Color c)[] HeatStops =
    {
        (480, new Color(0.15f, 0.02f, 0.01f)),
        (550, new Color(0.45f, 0.04f, 0.02f)),
        (630, new Color(0.62f, 0.07f, 0.03f)),
        (700, new Color(0.75f, 0.12f, 0.04f)),
        (780, new Color(0.88f, 0.2f, 0.05f)),
        (850, new Color(0.97f, 0.32f, 0.07f)),
        (920, new Color(1f, 0.46f, 0.1f)),
        (1000, new Color(1f, 0.64f, 0.2f)),
        (1100, new Color(1f, 0.82f, 0.42f)),
        (1200, new Color(1f, 0.93f, 0.7f)),
        (1300, new Color(1f, 0.98f, 0.92f)),
    };

    /// <summary>The colour hot metal glows at (alpha = how bright; clear below 480 °C).</summary>
    public static Color HeatColor(double celsius)
    {
        if (celsius < HeatStops[0].t)
            return Color.clear;
        for (int i = 1; i < HeatStops.Length; i++)
            if (celsius <= HeatStops[i].t)
            {
                float k = (float)((celsius - HeatStops[i - 1].t) / (HeatStops[i].t - HeatStops[i - 1].t));
                Color c = Color.Lerp(HeatStops[i - 1].c, HeatStops[i].c, k);
                c.a = Mathf.Clamp01((float)((celsius - 480) / 500));
                return c;
            }
        Color w = HeatStops[HeatStops.Length - 1].c;
        w.a = 1f;
        return w;
    }

    /// <summary>What the glow looks like in words (in the dark some heats show that do not show by day).</summary>
    public static string HeatName(double celsius, bool dark)
    {
        if (celsius < (dark ? 470 : 540)) return celsius > 200 ? "black heat" : "";
        if (celsius < 600) return "a faint blood red";
        if (celsius < 680) return "dark cherry red";
        if (celsius < 760) return "medium cherry red";
        if (celsius < 830) return "cherry red";
        if (celsius < 900) return "bright cherry red";
        if (celsius < 970) return "orange";
        if (celsius < 1060) return "yellow-orange";
        if (celsius < 1150) return "yellow";
        if (celsius < 1260) return "light yellow";
        return "white, throwing sparks";
    }

    /// <summary>The oxide colour bright steel takes at a temperature (temper colours; clear below 200 °C).</summary>
    public static Color TemperColor(double celsius)
    {
        if (celsius < 195) return Color.clear;
        if (celsius < 215) return new Color(0.93f, 0.87f, 0.62f);
        if (celsius < 235) return new Color(0.86f, 0.72f, 0.4f);
        if (celsius < 250) return new Color(0.74f, 0.55f, 0.28f);
        if (celsius < 265) return new Color(0.6f, 0.4f, 0.22f);
        if (celsius < 280) return new Color(0.5f, 0.26f, 0.36f);
        if (celsius < 295) return new Color(0.36f, 0.2f, 0.52f);
        if (celsius < 315) return new Color(0.2f, 0.25f, 0.62f);
        if (celsius < 340) return new Color(0.42f, 0.58f, 0.74f);
        return new Color(0.5f, 0.52f, 0.55f);
    }

    public static string TemperName(double celsius)
    {
        if (celsius < 195) return "";
        if (celsius < 215) return "a pale straw colour";
        if (celsius < 235) return "straw";
        if (celsius < 250) return "dark straw";
        if (celsius < 265) return "bronze";
        if (celsius < 280) return "brown turning purple";
        if (celsius < 295) return "purple";
        if (celsius < 315) return "deep blue";
        if (celsius < 340) return "pale blue";
        return "grey";
    }

    /// <summary>The sparks it throws on the grinding wheel - the smith's way to tell carbon (no numbers).</summary>
    public static string Sparks(CraftRules rules, CraftAlloyProfile p)
    {
        if (p.Base != rules.SteelBase || rules.SteelBase < 0)
            return p.BaseElement != null && p.BaseElement.kind == CraftElementKind.Fantasy ? "a few cold, blue-white sparks" : "no sparks at all";
        double c = p.Carbon * 100;
        int nickel = rules.IndexOf("nickel");
        string ni = nickel >= 0 && p.F(nickel) > 0.02 ? ", short and orange at the ends" : "";
        if (p.CastIron) return "short dull-red streaks with a few small bursts" + ni;
        if (c < 0.1) return "long straw-coloured streaks, hardly forking" + ni;
        if (c < 0.3) return "long streaks with a few forks" + ni;
        if (c < 0.6) return "bright streaks that fork and burst into small stars" + ni;
        if (c < 1.0) return "a bushy shower of bursting stars" + ni;
        return "a dense, bright shower of stars bursting on stars" + ni;
    }
}
