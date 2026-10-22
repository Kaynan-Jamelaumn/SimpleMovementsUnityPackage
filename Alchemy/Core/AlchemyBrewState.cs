using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An ingredient still dissolving, as saved.</summary>
[Serializable]
public class PendingIngredientState
{
    public string id = "";
    public string name = "";
    public string form = "";
    public List<SubstanceAmount> totals = new List<SubstanceAmount>();
    public List<double> exactTotals = new List<double>();
    public List<double> released = new List<double>();
    public List<double> rates = new List<double>();
    public double dissolveSeconds;
    [Tooltip("Version 1 states: the share released of every substance.")]
    public double progress;
    public double addedAt;
}

/// <summary>A named number, as saved.</summary>
[Serializable]
public class AlchemyVariableState
{
    public string name = "";
    public double value;
}

/// <summary>A reaction's tracking, as saved.</summary>
[Serializable]
public class ReactionTrackState
{
    public string id = "";
    public double total;
    public double rate;
    public bool active;
    public double cooldown;
}

/// <summary>
/// A whole preparation as plain data (JsonUtility-friendly): contents by substance id, undissolved matter, vapour,
/// distillate, the ingredients still giving up their substances, temperature and its history, agitation and stirring,
/// heat, the manual fire, the bath, the surroundings, the clock, the random stream, the reactions' tracking and the
/// history. Restoring it continues the preparation exactly where it was (older versions load with the new parts at rest).
/// </summary>
[Serializable]
public class AlchemyBrewState
{
    public int version = 3;
    public string id = "";
    public int seed;
    public AlchemyEquipmentProfile equipment;
    public double ambient;
    public double temperature;
    public double agitation;
    public double heat;
    public double elapsed;
    public long step;
    public double carry;
    public bool suspended;
    public bool ruptured;
    public bool boiling;
    public string random = "";
    // Version 3: the manual fire, continuous stirring, the bath, the temperature's history.
    public bool fireManual;
    public double fuel;
    public double fireLevel;
    public double air;
    public double damper = 1;
    public bool fireLit;
    public double stirSpeed;
    public double turbulence;
    public double aeration;
    public double emulsion;
    public int bath;
    public double temperatureMean;
    public double swing;
    public double coolingRate;
    public double splashCooldown;
    public List<string> crystallizing = new List<string>();
    public List<SubstanceAmount> contents = new List<SubstanceAmount>();
    public List<SubstanceAmount> undissolved = new List<SubstanceAmount>();
    public List<SubstanceAmount> vapor = new List<SubstanceAmount>();
    public List<SubstanceAmount> distillate = new List<SubstanceAmount>();
    public List<SubstanceAmount> vented = new List<SubstanceAmount>();
    public List<string> present = new List<string>();
    public List<string> precipitating = new List<string>();
    public List<PendingIngredientState> pending = new List<PendingIngredientState>();
    public List<AlchemyVariableState> variables = new List<AlchemyVariableState>();
    public List<ReactionTrackState> reactions = new List<ReactionTrackState>();
    public List<AlchemyOperation> history = new List<AlchemyOperation>();

    // Amounts are kept as doubles: SubstanceAmount.amount is a float, so the exact values go here.
    public List<double> exactContents = new List<double>();
    public List<double> exactUndissolved = new List<double>();
    public List<double> exactVapor = new List<double>();
    public List<double> exactDistillate = new List<double>();

    public static AlchemyBrewState From(AlchemyBrew brew)
    {
        AlchemyMixture m = brew.Mixture;
        AlchemyRules rules = brew.Rules;
        AlchemyTracker t = brew.Tracker;
        var s = new AlchemyBrewState
        {
            id = brew.Id,
            seed = brew.Seed,
            equipment = m.Equipment.Clone(),
            ambient = m.Ambient,
            temperature = m.Temperature,
            agitation = m.Agitation,
            heat = m.Heat,
            elapsed = m.Elapsed,
            step = m.Step,
            carry = brew.Carry,
            suspended = brew.IsSuspended,
            ruptured = m.Ruptured,
            boiling = t.Boiling,
            random = t.Random.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fireManual = m.FireManual,
            fuel = m.Fuel,
            fireLevel = m.FireLevel,
            air = m.Air,
            damper = m.Damper,
            fireLit = t.FireLit,
            stirSpeed = m.StirSpeed,
            turbulence = m.Turbulence,
            aeration = m.Aeration,
            emulsion = m.Emulsion,
            bath = (int)m.Bath,
            temperatureMean = m.TemperatureMean,
            swing = m.Swing,
            coolingRate = m.CoolingRate,
            splashCooldown = t.SplashCooldown,
        };
        Write(rules, m.amounts, s.contents, s.exactContents);
        Write(rules, m.undissolved, s.undissolved, s.exactUndissolved);
        Write(rules, m.vapor, s.vapor, s.exactVapor);
        Write(rules, m.distillate, s.distillate, s.exactDistillate);
        Write(rules, t.Vented, s.vented, null);
        for (int i = 0; i < t.Present.Length; i++)
        {
            if (t.Present[i]) s.present.Add(rules.Substances[i].id);
            if (t.Precipitating[i]) s.precipitating.Add(rules.Substances[i].id);
            if (t.Crystallizing[i]) s.crystallizing.Add(rules.Substances[i].id);
        }
        foreach (PendingIngredient p in m.pending)
        {
            var ps = new PendingIngredientState
            {
                id = p.Id, name = p.Name, form = p.Form, dissolveSeconds = p.DissolveSeconds, progress = p.Progress, addedAt = p.AddedAt,
            };
            for (int k = 0; k < p.Substances.Length; k++)
            {
                ps.totals.Add(new SubstanceAmount(rules.Substances[p.Substances[k]].id, (float)p.Totals[k]));
                ps.exactTotals.Add(p.Totals[k]);
                ps.released.Add(p.Released[k]);
                ps.rates.Add(p.Rates[k]);
            }
            s.pending.Add(ps);
        }
        foreach (KeyValuePair<string, double> v in m.Variables)
            s.variables.Add(new AlchemyVariableState { name = v.Key, value = v.Value });
        for (int j = 0; j < rules.Reactions.Length; j++)
            if (t.ReactionTotals[j] > 0 || t.ReactionActive[j] || t.EruptionCooldown[j] > 0)
                s.reactions.Add(new ReactionTrackState
                {
                    id = rules.Reactions[j].Definition.id, total = t.ReactionTotals[j], rate = t.ReactionRates[j],
                    active = t.ReactionActive[j], cooldown = t.EruptionCooldown[j],
                });
        foreach (AlchemyOperation op in brew.History)
            s.history.Add(op.Clone());
        return s;
    }

    private static void Write(AlchemyRules rules, double[] amounts, List<SubstanceAmount> into, List<double> exact)
    {
        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] <= 0) continue;
            into.Add(new SubstanceAmount(rules.Substances[i].id, (float)amounts[i]));
            exact?.Add(amounts[i]);
        }
    }

    private static void Read(AlchemyRules rules, List<SubstanceAmount> from, List<double> exact, double[] into)
    {
        Array.Clear(into, 0, into.Length);
        if (from == null)
            return;
        for (int k = 0; k < from.Count; k++)
        {
            int i = from[k] != null ? rules.IndexOf(from[k].substance) : -1;
            if (i < 0) continue;
            into[i] = exact != null && k < exact.Count ? exact[k] : from[k].amount;
        }
    }

    internal void ApplyTo(AlchemyBrew brew)
    {
        AlchemyMixture m = brew.Mixture;
        AlchemyRules rules = brew.Rules;
        AlchemyTracker t = brew.Tracker;
        m.Temperature = temperature;
        m.Agitation = agitation;
        m.Heat = heat;
        m.Ambient = ambient;
        m.Elapsed = elapsed;
        m.Step = step;
        m.Ruptured = ruptured;
        m.FireManual = fireManual;
        m.Fuel = fuel;
        m.FireLevel = fireLevel;
        m.Air = air;
        m.Damper = version >= 3 ? damper : 1;
        m.StirSpeed = stirSpeed;
        m.Turbulence = turbulence;
        m.Aeration = aeration;
        m.Emulsion = emulsion;
        m.Bath = (AlchemyBath)Math.Max(0, Math.Min(2, bath));
        m.TemperatureMean = version < 3 ? temperature : temperatureMean;
        m.Swing = swing;
        m.CoolingRate = coolingRate;
        t.FireLit = fireLit;
        t.SplashCooldown = splashCooldown;
        Read(rules, contents, exactContents, m.amounts);
        Read(rules, undissolved, exactUndissolved, m.undissolved);
        Read(rules, vapor, exactVapor, m.vapor);
        Read(rules, distillate, exactDistillate, m.distillate);
        Read(rules, vented, null, t.Vented);
        if (present != null)
            foreach (string id in present)
            {
                int i = rules.IndexOf(id);
                if (i >= 0) t.Present[i] = true;
            }
        if (precipitating != null)
            foreach (string id in precipitating)
            {
                int i = rules.IndexOf(id);
                if (i >= 0) t.Precipitating[i] = true;
            }
        if (crystallizing != null)
            foreach (string id in crystallizing)
            {
                int i = rules.IndexOf(id);
                if (i >= 0) t.Crystallizing[i] = true;
            }
        m.pending.Clear();
        if (pending != null)
            foreach (PendingIngredientState ps in pending)
            {
                var subs = new List<int>();
                var totals = new List<double>();
                var done = new List<double>();
                var speed = new List<double>();
                double fallbackRate = ps.dissolveSeconds > 1e-4 ? 1.0 / ps.dissolveSeconds : PendingIngredient.Instant;
                for (int k = 0; k < ps.totals.Count; k++)
                {
                    SubstanceAmount a = ps.totals[k];
                    int i = a != null ? rules.IndexOf(a.substance) : -1;
                    if (i < 0) continue;
                    double total = ps.exactTotals != null && k < ps.exactTotals.Count ? ps.exactTotals[k] : a.amount;
                    subs.Add(i);
                    totals.Add(total);
                    // Version 1 states kept one progress for the whole ingredient.
                    done.Add(ps.released != null && k < ps.released.Count ? ps.released[k] : total * ps.progress);
                    speed.Add(ps.rates != null && k < ps.rates.Count ? ps.rates[k] : fallbackRate);
                }
                m.pending.Add(new PendingIngredient(ps.id, ps.name, ps.form, subs.ToArray(), totals.ToArray(), speed.ToArray(),
                    ps.dissolveSeconds, ps.addedAt, done.ToArray()));
            }
        m.variables.Clear();
        if (variables != null)
            foreach (AlchemyVariableState v in variables)
                if (!string.IsNullOrEmpty(v.name)) m.variables[v.name] = v.value;
        if (reactions != null)
            foreach (ReactionTrackState r in reactions)
            {
                int j = rules.ReactionIndex(r.id);
                if (j < 0) continue;
                t.ReactionTotals[j] = r.total;
                t.ReactionRates[j] = r.rate;
                t.ReactionActive[j] = r.active;
                t.EruptionCooldown[j] = r.cooldown;
            }
        t.Boiling = boiling;
        if (ulong.TryParse(random, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out ulong rs) && rs != 0)
            t.Random = rs;
        brew.RestoreInternals(id, suspended, carry, history);
    }

    public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

    public static AlchemyBrewState FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonUtility.FromJson<AlchemyBrewState>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
