using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a welfare band does to how an animal lives: multipliers and additions to its simulation.</summary>
[Serializable]
public class AnimalWelfareBandEffects
{
    [Tooltip("x how fast illness builds up.")]
    [Min(0f)] public float susceptibility = 1f;
    [Tooltip("x how fast it heals (health and illness).")]
    [Min(0f)] public float recovery = 1f;
    [Tooltip("x growth.")]
    [Min(0f)] public float growth = 1f;
    [Tooltip("Added to its stress target.")]
    public float stress;
    [Tooltip("Added to its happiness target.")]
    public float happiness;
    [Tooltip("x what it gives (Phase 3 production).")]
    [Min(0f)] public float production = 1f;
}

/// <summary>
/// How welfare is worked out: a weighted mean of health, nutrition, hygiene, comfort, happiness and affinity (the weights
/// are normalised), less a penalty for stress above a calm level, never more than its health plus <see cref="healthCap"/>;
/// then five bands, each with what it does to the animal.
/// </summary>
[Serializable]
public class AnimalWelfareSpec
{
    [Header("Weights (normalised)")]
    [Min(0f)] public float health = 0.25f;
    [Min(0f)] public float nutrition = 0.2f;
    [Min(0f)] public float hygiene = 0.15f;
    [Min(0f)] public float comfort = 0.15f;
    [Min(0f)] public float happiness = 0.15f;
    [Min(0f)] public float affinity = 0.1f;
    [Tooltip("Points of welfare taken off per point of stress above Calm Stress (0: none, as the reference formula).")]
    [Min(0f)] public float stressPenalty = 0.15f;
    [Tooltip("Stress up to which an animal counts as calm (no penalty).")]
    [Range(0f, 100f)] public float calmStress = 40f;
    [Tooltip("Welfare is never more than its health plus this (a dying animal is never Average).")]
    [Range(0f, 100f)] public float healthCap = 25f;

    [Header("Bands (welfare from which each starts)")]
    [Range(0f, 100f)] public float excellentFrom = 90f;
    [Range(0f, 100f)] public float goodFrom = 75f;
    [Range(0f, 100f)] public float averageFrom = 50f;
    [Range(0f, 100f)] public float poorFrom = 25f;

    [Header("What each band does")]
    public AnimalWelfareBandEffects excellent = new AnimalWelfareBandEffects { susceptibility = 0.7f, recovery = 1.25f, growth = 1.05f, stress = -5f, happiness = 4f, production = 1.15f };
    public AnimalWelfareBandEffects good = new AnimalWelfareBandEffects { susceptibility = 0.85f, recovery = 1.1f, growth = 1f, stress = -2f, happiness = 2f, production = 1.05f };
    public AnimalWelfareBandEffects average = new AnimalWelfareBandEffects();
    public AnimalWelfareBandEffects poor = new AnimalWelfareBandEffects { susceptibility = 1.3f, recovery = 0.85f, growth = 0.92f, stress = 8f, happiness = -4f, production = 0.8f };
    public AnimalWelfareBandEffects critical = new AnimalWelfareBandEffects { susceptibility = 1.7f, recovery = 0.6f, growth = 0.8f, stress = 15f, happiness = -8f, production = 0.5f };
}

/// <summary>
/// An animal's welfare: how well it lives, from its state (see <see cref="AnimalWelfareSpec"/>), in five bands that change
/// the simulation (illness builds faster in a Critical animal, an Excellent one heals faster). Also its life record, the
/// slow averages its remains are judged by. Pure functions, called by the simulation and the commands.
/// </summary>
public static class AnimalWelfare
{
    private static readonly AnimalWelfareSpec DefaultSpec = new AnimalWelfareSpec();
    private static readonly AnimalWelfareBandEffects Neutral = new AnimalWelfareBandEffects();

    /// <summary>
    /// W = (0.25 H + 0.20 N + 0.15 Hy + 0.15 C + 0.15 Ha + 0.10 A) / sum of weights - stressPenalty x max(0, stress -
    /// calmStress), at most health + healthCap, clamped 0-100 (with the spec's weights).
    /// </summary>
    public static float Compute(AnimalRecord a, AnimalWelfareSpec s)
    {
        if (a == null) return 0f;
        s ??= DefaultSpec;
        float wh = Mathf.Max(0f, s.health), wn = Mathf.Max(0f, s.nutrition), wy = Mathf.Max(0f, s.hygiene);
        float wc = Mathf.Max(0f, s.comfort), wa = Mathf.Max(0f, s.happiness), wf = Mathf.Max(0f, s.affinity);
        float sum = wh + wn + wy + wc + wa + wf;
        if (sum <= 1e-6f) return AnimalMath.Clamp100(a.health);
        float w = (wh * a.health + wn * a.nutrition + wy * a.hygiene + wc * a.comfort + wa * a.happiness + wf * a.affinity) / sum;
        w -= Mathf.Max(0f, s.stressPenalty) * Mathf.Max(0f, a.stress - s.calmStress);
        w = Mathf.Min(w, a.health + Mathf.Max(0f, s.healthCap));
        return AnimalMath.Clamp100(w);
    }

    /// <summary>The band a welfare falls in (the spec's thresholds: 90 / 75 / 50 / 25 by default).</summary>
    public static AnimalWelfareBand BandOf(float welfare, AnimalWelfareSpec s)
    {
        s ??= DefaultSpec;
        if (welfare >= s.excellentFrom) return AnimalWelfareBand.Excellent;
        if (welfare >= s.goodFrom) return AnimalWelfareBand.Good;
        if (welfare >= s.averageFrom) return AnimalWelfareBand.Average;
        if (welfare >= s.poorFrom) return AnimalWelfareBand.Poor;
        return AnimalWelfareBand.Critical;
    }

    /// <summary>What a band does (never null: a missing entry does nothing).</summary>
    public static AnimalWelfareBandEffects Effects(AnimalWelfareBand b, AnimalWelfareSpec s)
    {
        s ??= DefaultSpec;
        AnimalWelfareBandEffects e;
        switch (b)
        {
            case AnimalWelfareBand.Excellent: e = s.excellent; break;
            case AnimalWelfareBand.Good: e = s.good; break;
            case AnimalWelfareBand.Poor: e = s.poor; break;
            case AnimalWelfareBand.Critical: e = s.critical; break;
            default: e = s.average; break;
        }
        return e ?? Neutral;
    }

    /// <summary>What an animal's welfare band does to it now (the welfare of its last step drives the next).</summary>
    public static AnimalWelfareBandEffects EffectsOf(AnimalRecord a, AnimalRules rules)
    {
        AnimalWelfareSpec s = rules?.welfare ?? DefaultSpec;
        return Effects(BandOf(a != null ? a.welfare : 60f, s), s);
    }

    /// <summary>"Excellent", "Good", "Average", "Poor", "Critical".</summary>
    public static string Word(AnimalWelfareBand b)
    {
        switch (b)
        {
            case AnimalWelfareBand.Excellent: return "Excellent";
            case AnimalWelfareBand.Good: return "Good";
            case AnimalWelfareBand.Poor: return "Poor";
            case AnimalWelfareBand.Critical: return "Critical";
            default: return "Average";
        }
    }

    /// <summary>The word for an animal's welfare now.</summary>
    public static string WordOf(AnimalRecord a, AnimalRules rules) => Word(BandOf(a != null ? a.welfare : 60f, rules?.welfare ?? DefaultSpec));

    /// <summary>What is wrong with a welfare spec: weights (at least one above 0), bands in order, band effects that make sense.</summary>
    public static void Validate(AnimalWelfareSpec s, List<string> errors, List<string> warnings)
    {
        if (s == null)
        {
            errors?.Add("Welfare: no welfare spec.");
            return;
        }
        float sum = s.health + s.nutrition + s.hygiene + s.comfort + s.happiness + s.affinity;
        if (s.health < 0f || s.nutrition < 0f || s.hygiene < 0f || s.comfort < 0f || s.happiness < 0f || s.affinity < 0f)
            errors?.Add("Welfare: a weight is below 0.");
        if (sum <= 0f) errors?.Add("Welfare: every weight is 0 (welfare would only follow health).");
        if (!(s.excellentFrom > s.goodFrom && s.goodFrom > s.averageFrom && s.averageFrom > s.poorFrom))
            errors?.Add($"Welfare: the bands are not in order (Excellent {s.excellentFrom:0} > Good {s.goodFrom:0} > Average {s.averageFrom:0} > Poor {s.poorFrom:0}).");
        if (s.poorFrom <= 0f) warnings?.Add("Welfare: Poor starts at 0, so no animal is ever Critical.");
        if (s.excellentFrom > 100f) warnings?.Add("Welfare: Excellent starts above 100, so no animal is ever Excellent.");
        if (s.stressPenalty > 1f) warnings?.Add($"Welfare: a stress penalty of {s.stressPenalty:0.##} makes a little stress weigh more than everything else.");
        Check("Excellent", s.excellent, errors, warnings);
        Check("Good", s.good, errors, warnings);
        Check("Average", s.average, errors, warnings);
        Check("Poor", s.poor, errors, warnings);
        Check("Critical", s.critical, errors, warnings);
        if (s.excellent != null && s.critical != null && s.excellent.susceptibility > s.critical.susceptibility)
            warnings?.Add("Welfare: Excellent animals fall ill faster than Critical ones (check the bands' Susceptibility).");
    }

    private static void Check(string band, AnimalWelfareBandEffects e, List<string> errors, List<string> warnings)
    {
        if (e == null)
        {
            errors?.Add($"Welfare: the {band} band has no effects.");
            return;
        }
        if (e.susceptibility < 0f || e.recovery < 0f || e.growth < 0f || e.production < 0f) errors?.Add($"Welfare: the {band} band has a multiplier below 0.");
        if (e.recovery <= 0f) warnings?.Add($"Welfare: {band} animals never heal (Recovery 0).");
    }

    // ---------------------------------------------------------------- after a step
    /// <summary>
    /// Works out its welfare after a step (<see cref="AnimalRecord.welfare"/>). True when its owner should be told it needs
    /// help ("... is in a bad way."): its band is Critical or its health below the rules' Needs Help Below Health, and it was
    /// not told so within the rules' Needs Help Every Hours (<see cref="AnimalRecord.lastNeedsHelpAt"/> is set).
    /// </summary>
    public static bool Update(AnimalRecord a, AnimalRules rules, double now)
    {
        if (a == null) return false;
        rules ??= AnimalRules.Defaults();
        AnimalWelfareSpec s = rules.welfare ?? DefaultSpec;
        a.welfare = Compute(a, s);
        if (!rules.tellWhenCritical || !a.Alive) return false;
        bool bad = BandOf(a.welfare, s) == AnimalWelfareBand.Critical || a.health < rules.needsHelpBelowHealth;
        if (!bad) return false;
        if (a.lastNeedsHelpAt >= 0.0 && now - a.lastNeedsHelpAt < rules.needsHelpEveryHours) return false;
        a.lastNeedsHelpAt = now;
        return true;
    }

    /// <summary>What the owner is told when <see cref="Update"/> says so.</summary>
    public static string NeedsHelpText(AnimalRecord a, AnimalSpeciesData sp) => $"{AnimalSim.NameOf(a, sp)} is in a bad way.";

    /// <summary>
    /// Starts its welfare and its life record from its state (a new animal, or one from a Phase 1 save, the first time it
    /// is stepped): no animal starts its life record at the field defaults.
    /// </summary>
    public static void Prime(AnimalRecord a, AnimalRules rules)
    {
        if (a == null) return;
        a.welfare = Compute(a, rules?.welfare ?? DefaultSpec);
        a.life ??= new AnimalLife();
        if (!a.life.primed) PrimeLife(a, AnimalHealth.Worst(a));
    }

    /// <summary>Its life record starts from its state now (the averages begin where it is).</summary>
    public static void PrimeLife(AnimalRecord a, float worst)
    {
        AnimalLife l = a.life ??= new AnimalLife();
        l.primed = true;
        l.welfare = a.welfare;
        l.nutrition = a.nutrition;
        l.health = a.health;
        l.hygiene = a.hygiene;
        l.comfort = a.comfort;
        l.illness = worst;
    }

    /// <summary>
    /// Its life record over a step of <paramref name="dt"/> hours (slow averages, time constant the rules' Life Hours):
    /// welfare, nutrition, health, hygiene, comfort and illness (<paramref name="worst"/>: its worst severity now); and the
    /// hours it was ill (worst at or above the rules' Ill From), starving and alive.
    /// </summary>
    public static void StepLife(AnimalRecord a, AnimalRules rules, double dt, float worst)
    {
        if (a == null || dt <= 0.0) return;
        rules ??= AnimalRules.Defaults();
        AnimalLife l = a.life ??= new AnimalLife();
        if (!l.primed) PrimeLife(a, worst);
        float k = 1f - AnimalMath.Decay(dt, Math.Max(1.0, rules.lifeHours));
        l.welfare += (a.welfare - l.welfare) * k;
        l.nutrition += (a.nutrition - l.nutrition) * k;
        l.health += (a.health - l.health) * k;
        l.hygiene += (a.hygiene - l.hygiene) * k;
        l.comfort += (a.comfort - l.comfort) * k;
        l.illness += (worst - l.illness) * k;
        if (worst >= rules.illFrom) l.illHours += (float)dt;
        if (a.satiety <= 0f) l.starvedHours += (float)dt;
        l.hours += (float)dt;
    }

    /// <summary>
    /// The day's Phase 2 means and lowest welfare (call it from the day record with the same weight as the Phase 1 means:
    /// k = dt / the hours of the day so far, after them).
    /// </summary>
    public static void RecordDay(AnimalDay d, AnimalRecord a, float k, float worst)
    {
        if (d == null || a == null) return;
        d.hygiene += (a.hygiene - d.hygiene) * k;
        d.comfort += (a.comfort - d.comfort) * k;
        d.welfare += (a.welfare - d.welfare) * k;
        d.illness += (worst - d.illness) * k;
        d.minWelfare = Mathf.Min(d.minWelfare, a.welfare);
    }
}
