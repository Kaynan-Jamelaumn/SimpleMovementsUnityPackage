using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player (or a script) did to a preparation. Serialized as numbers: only add new values at the END.</summary>
public enum AlchemyOpKind
{
    AddIngredient,
    AddSubstance,
    SetHeat,
    Stir,
    SetVariable,
    Suspend,
    Resume,
    Bottle,
    CollectDistillate,
    SetAmbient,
    AddFuel,
    Bellows,
    SetDamper,
    SetStirring,
    SetBath,
    Filter,
    Decant,
    Skim,
}

/// <summary>
/// One operation of a preparation's history, stamped with the simulation step it happened before. The history is
/// everything needed to reproduce a preparation: replaying it on the same rules and equipment gives the same mixture
/// (<see cref="AlchemyBrew.Replay"/>). Ingredients are stored with their profile, so a history stays valid even if the
/// ingredient assets change later.
/// </summary>
[Serializable]
public class AlchemyOperation
{
    public long step;
    public AlchemyOpKind kind;
    [Tooltip("Ingredient / substance / variable id.")]
    public string id = "";
    [Tooltip("Readable name (ingredient name...).")]
    public string label = "";
    public float value;
    public int count;
    [Tooltip("AddIngredient: the form it went in as when not its own (ground at the station). Empty = the profile's form.")]
    public string form = "";
    [Tooltip("AddIngredient: how evenly it was ground (1 = all at that form; less = part coarser, part finer).")]
    [Range(0f, 1f)] public float evenness = 1f;
    [Tooltip("AddFuel: the flare of the fuel (a burst of draft when it catches).")]
    public float value2;
    public AlchemyIngredientProfile profile;

    public AlchemyOperation Clone()
    {
        var c = (AlchemyOperation)MemberwiseClone();
        c.profile = profile?.Clone();
        return c;
    }
}

/// <summary>
/// A preparation in progress: the mixture, its clock and history, its events. The <see cref="AlchemyStation"/> drives
/// one, but it is a plain object: tests, editor tools and servers can make and run preparations without a scene.
/// <list type="bullet">
/// <item><b>Operations</b> (add an ingredient or a substance, set the heat, stir, set a surroundings variable, bottle)
/// change the mixture at once and are recorded with the current step.</item>
/// <item><b>Time</b>: <see cref="Advance"/> runs whole fixed steps (the remainder carries over): the same operations at
/// the same steps always give the same mixture. While <see cref="IsSuspended"/>, time does not pass for it.</item>
/// <item><b>Results</b>: <see cref="Evaluate"/> says what the liquid would be if bottled now; <see cref="Bottle"/>
/// takes it out (all or part: the rest keeps transforming); <see cref="CollectDistillate"/> and <see cref="Empty"/>.</item>
/// <item><b>Saving</b>: <see cref="Capture"/> / <see cref="Restore"/> keep everything, so an interrupted preparation
/// continues exactly where it was.</item>
/// </list>
/// </summary>
public sealed class AlchemyBrew
{
    private const int MaxRecentEvents = 80;

    private readonly List<AlchemyOperation> history = new List<AlchemyOperation>();
    private readonly List<AlchemyEvent> recent = new List<AlchemyEvent>();
    private readonly List<AlchemyEvent> stepEvents = new List<AlchemyEvent>();
    private double carry;
    /// <summary>What the last filter, decant or skim took out (substance index → units).</summary>
    private double[] lastTaken;

    public string Id { get; private set; }
    public AlchemyRules Rules { get; }
    public AlchemyMixture Mixture { get; }
    public AlchemyTracker Tracker { get; private set; }
    public int Seed { get; }
    public bool IsSuspended { get; private set; }
    /// <summary>The operations so far, in order.</summary>
    public IReadOnlyList<AlchemyOperation> History => history;
    /// <summary>The last events (newest last).</summary>
    public IReadOnlyList<AlchemyEvent> RecentEvents => recent;
    /// <summary>Raised for every event (ingredients dissolving, reactions starting, boiling, eruptions, bottling...).</summary>
    public event Action<AlchemyEvent> EventRaised;

    public double Elapsed => Mixture.Elapsed;
    public bool IsEmpty => Mixture.IsEmpty;
    public AlchemyEquipmentProfile Equipment => Mixture.Equipment;

    public AlchemyBrew(AlchemyRules rules, AlchemyEquipmentProfile equipment, double ambient = double.NaN, int seed = 0)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Seed = seed;
        Id = Guid.NewGuid().ToString("N");
        Mixture = new AlchemyMixture(rules, equipment != null ? equipment.Clone() : AlchemyEquipmentProfile.DefaultCauldron(),
            double.IsNaN(ambient) ? rules.Settings.defaultAmbient : ambient);
        Tracker = new AlchemyTracker(rules, seed);
    }

    // ------------------------------------------------------------------ operations
    /// <summary>Room left in the vessel (units).</summary>
    public double FreeCapacity => Math.Max(0.0, Equipment.capacity - Mixture.FilledVolume);

    /// <summary>How many of <paramref name="ingredient"/> fit (by units released).</summary>
    public int HowManyFit(IAlchemyIngredient ingredient)
    {
        float per = ingredient?.AlchemyProfile?.UnitsPerItem ?? 0f;
        if (per <= 1e-4f) return 999;
        return (int)Math.Floor(FreeCapacity / per + 1e-6);
    }

    public bool AddIngredient(IAlchemyIngredient ingredient, int count, out string reason, string form = null, float evenness = 1f)
    {
        if (ingredient == null)
        {
            reason = "Nothing to add.";
            return false;
        }
        return AddIngredient(ingredient.AlchemyId, ingredient.AlchemyName, ingredient.AlchemyProfile, count, out reason, form, evenness);
    }

    /// <summary>
    /// Puts <paramref name="count"/> of an ingredient into the vessel, in its own form or <paramref name="form"/> (an
    /// Ingredient Form id: "coarse", "ground", "powder" when ground at the station). It starts giving up its substances -
    /// each at its own rate - and its temperature mixes with the contents'. What it does depends on what is already
    /// there: its substances join whatever reactions they fit. An uneven grind (<paramref name="evenness"/> below 1) goes
    /// in partly at the next coarser and the next finer form.
    /// </summary>
    public bool AddIngredient(string id, string name, AlchemyIngredientProfile profile, int count, out string reason, string form = null, float evenness = 1f)
    {
        reason = "";
        if (profile == null || profile.releases == null || profile.releases.Count == 0)
        {
            reason = $"{name} does nothing in a vessel.";
            return false;
        }
        if (count <= 0)
        {
            reason = "Nothing to add.";
            return false;
        }
        double units = profile.UnitsPerItem * count;
        if (units > FreeCapacity + 1e-6)
        {
            reason = $"Not enough room in the {Equipment.displayName} ({AlchemyText.Units(FreeCapacity)} free).";
            Raise(new AlchemyEvent(AlchemyEventKind.Refused, Elapsed, id, reason));
            return false;
        }
        if (!string.IsNullOrEmpty(form) && !Rules.HasForm(form))
        {
            reason = $"Unknown ingredient form '{form}'.";
            return false;
        }
        var op = new AlchemyOperation
        {
            step = Mixture.Step, kind = AlchemyOpKind.AddIngredient, id = id ?? "", label = name ?? id, count = count,
            form = string.Equals(form, profile.form, StringComparison.OrdinalIgnoreCase) ? "" : form ?? "", profile = profile.Clone(),
            evenness = Mathf.Clamp01(evenness),
        };
        Apply(op);
        history.Add(op);
        return true;
    }

    /// <summary>
    /// Pours <paramref name="amount"/> units of one substance in (a water source, scripts, the residue a dirty vessel
    /// starts with: <paramref name="note"/> says so in the log and the procedure).
    /// </summary>
    public bool AddSubstance(string substanceId, double amount, double temperature, out string reason, string note = null)
    {
        reason = "";
        if (Rules.IndexOf(substanceId) < 0)
        {
            reason = $"Unknown substance '{substanceId}'.";
            return false;
        }
        if (amount <= 0)
        {
            reason = "Nothing to add.";
            return false;
        }
        if (amount > FreeCapacity + 1e-6)
        {
            reason = $"Not enough room in the {Equipment.displayName} ({AlchemyText.Units(FreeCapacity)} free).";
            Raise(new AlchemyEvent(AlchemyEventKind.Refused, Elapsed, substanceId, reason));
            return false;
        }
        var op = new AlchemyOperation
        {
            step = Mixture.Step, kind = AlchemyOpKind.AddSubstance, id = substanceId, label = Rules.SubstanceName(substanceId),
            value = (float)amount, profile = new AlchemyIngredientProfile { temperature = (float)temperature }, form = note ?? "",
        };
        Apply(op);
        history.Add(op);
        return true;
    }

    /// <summary>
    /// Sets the fire: -1 full cooling (vessels that can cool), 0 off, 1 full heat. A positive setting is a regulated
    /// burner: it replaces a manual fire. With a manual fire, use 0 or below to set the cooling only.
    /// </summary>
    public void SetHeat(double level)
    {
        level = AlchemySimulator.Clamp(level, Equipment.canCool ? -1 : 0, 1);
        if (Math.Abs(level - Mixture.Heat) < 1e-4 && !(level > 1e-6 && Mixture.FireManual))
            return;
        Record(new AlchemyOperation
        {
            step = Mixture.Step, kind = AlchemyOpKind.SetHeat, value = (float)level,
            label = AlchemyText.Celsius(Equipment.TargetTemperature(level, Mixture.Ambient)),
        });
    }

    /// <summary>Stirs the mixture once (agitation helps ingredients dissolve; some reactions need it, others need stillness).</summary>
    public void Stir(double strength = 1)
    {
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Stir, value = (float)Math.Max(0, strength) });
    }

    /// <summary>
    /// Stirs continuously at <paramref name="speed"/> (0-1; 0 stops): the agitation follows it. Changing the speed
    /// often makes the stirring uneven (turbulence). Recorded only when it changes by more than <paramref name="epsilon"/>.
    /// </summary>
    public void SetStirring(double speed, double epsilon = 0.01)
    {
        speed = AlchemySimulator.Clamp(speed, 0, 1);
        if (Math.Abs(speed - Mixture.StirSpeed) <= epsilon && !(speed == 0 && Mixture.StirSpeed > 0))
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.SetStirring, value = (float)speed });
    }

    /// <summary>Feeds the fire <paramref name="count"/> × a fuel of the rules (it becomes a manual fire).</summary>
    public bool AddFuel(string fuelId, int count, out string reason)
    {
        reason = "";
        AlchemyFuel fuel = Rules.Fuel(fuelId);
        if (fuel == null)
        {
            reason = $"Unknown fuel '{fuelId}'.";
            return false;
        }
        if (count <= 0)
        {
            reason = "Nothing to add.";
            return false;
        }
        Record(new AlchemyOperation
        {
            step = Mixture.Step, kind = AlchemyOpKind.AddFuel, id = fuel.id, label = fuel.Name, count = count,
            value = fuel.energy * count, value2 = fuel.flare,
        });
        return true;
    }

    /// <summary>One pump of the bellows (0-1): the flames rise for a few seconds.</summary>
    public void Bellows(double strength = 1)
    {
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Bellows, value = (float)AlchemySimulator.Clamp(strength, 0, 1) });
    }

    /// <summary>The air vent of a manual fire: 1 open, 0 choked (the flames die down).</summary>
    public void SetDamper(double open)
    {
        open = AlchemySimulator.Clamp(open, 0, 1);
        if (Math.Abs(open - Mixture.Damper) < 1e-3)
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.SetDamper, value = (float)open });
    }

    /// <summary>Stands the vessel in a water bath (gentle heat, at most ~96 °C, nothing scorches), an ice bath, or neither.</summary>
    public void SetBath(AlchemyBath bath)
    {
        if (bath == Mixture.Bath)
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.SetBath, value = (int)bath, label = bath.ToString() });
    }

    /// <summary>
    /// Sets a surroundings variable (night, humidity, your own). Recorded only when it changes by more than
    /// <paramref name="epsilon"/>, so a replay sees the same surroundings at the same steps.
    /// </summary>
    public void SetVariable(string name, double value, double epsilon = 0.02)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (Mixture.variables.TryGetValue(name, out double old) && Math.Abs(old - value) <= epsilon)
            return;
        if (!Mixture.variables.ContainsKey(name) && Math.Abs(value) <= epsilon && epsilon > 0)
        {
            Mixture.variables[name] = value; // a variable that starts at 0 is not worth a history line
            return;
        }
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.SetVariable, id = name, value = (float)value });
    }

    /// <summary>Sets the surroundings' temperature (recorded when it changes by more than half a degree).</summary>
    public void SetAmbient(double celsius)
    {
        if (Math.Abs(celsius - Mixture.Ambient) <= 0.5)
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.SetAmbient, value = (float)celsius });
    }

    /// <summary>Interrupts the preparation: time stops for it (its state is kept exactly) until <see cref="Resume"/>.</summary>
    public void Suspend()
    {
        if (IsSuspended)
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Suspend });
    }

    public void Resume()
    {
        if (!IsSuspended)
            return;
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Resume });
    }

    private void Record(AlchemyOperation op)
    {
        Apply(op);
        history.Add(op);
    }

    /// <summary>Applies an operation to the mixture (live and in replays).</summary>
    private void Apply(AlchemyOperation op)
    {
        AlchemyMixture m = Mixture;
        switch (op.kind)
        {
            case AlchemyOpKind.AddIngredient:
            {
                string formId = string.IsNullOrEmpty(op.form) ? op.profile.form : op.form;
                List<(AlchemyIngredientForm form, double share)> parts = GrindParts(Rules.Form(formId), op.evenness);
                double units = 0;
                foreach ((AlchemyIngredientForm form, double share) in parts)
                    units += AddPending(op, form, share, false);
                MixHeat(units, op.profile.temperature); // before the parts count in the thermal mass
                foreach ((AlchemyIngredientForm form, double share) in parts)
                    AddPending(op, form, share, true);
                Raise(new AlchemyEvent(AlchemyEventKind.IngredientAdded, m.Elapsed, op.id, op.profile.observation, op.count));
                break;
            }
            case AlchemyOpKind.AddSubstance:
            {
                int i = Rules.IndexOf(op.id);
                if (i < 0) break;
                MixHeat(op.value, op.profile != null ? op.profile.temperature : m.Ambient);
                m.amounts[i] += op.value;
                Raise(new AlchemyEvent(AlchemyEventKind.SubstanceAdded, m.Elapsed, op.id, op.form, op.value));
                break;
            }
            case AlchemyOpKind.SetHeat:
                m.Heat = op.value;
                if (op.value > 1e-6) m.FireManual = false; // a regulated burner replaces the fire
                break;
            case AlchemyOpKind.AddFuel:
                if (!m.FireManual)
                {
                    m.FireManual = true;
                    m.Heat = Math.Min(0.0, m.Heat);
                }
                m.Fuel += Math.Max(0f, op.value);
                m.Air = Math.Min(1.0, m.Air + Math.Max(0f, op.value2));
                Tracker.FireLit = true;
                break;
            case AlchemyOpKind.Bellows:
                m.Air = Math.Min(1.0, m.Air + Rules.Settings.bellowsGain * Math.Max(0f, op.value));
                break;
            case AlchemyOpKind.SetDamper:
                m.Damper = AlchemySimulator.Clamp(op.value, 0, 1);
                break;
            case AlchemyOpKind.SetStirring:
            {
                double speed = AlchemySimulator.Clamp(op.value, 0, 1);
                m.Turbulence += Rules.Settings.turbulenceGain * Math.Abs(speed - m.StirSpeed);
                m.StirSpeed = speed;
                break;
            }
            case AlchemyOpKind.SetBath:
                m.Bath = (AlchemyBath)Math.Max(0, Math.Min(2, (int)Math.Round(op.value)));
                break;
            case AlchemyOpKind.Filter:
                lastTaken = ApplyFilter(Rules.Filter(op.id));
                break;
            case AlchemyOpKind.Decant:
                lastTaken = ApplyDecant();
                break;
            case AlchemyOpKind.Skim:
                lastTaken = ApplySkim();
                break;
            case AlchemyOpKind.Stir:
                m.Agitation = Math.Min(1.0, m.Agitation + op.value * Equipment.stirEfficiency);
                break;
            case AlchemyOpKind.SetVariable:
                m.variables[op.id] = op.value;
                break;
            case AlchemyOpKind.SetAmbient:
                m.Ambient = op.value;
                if (m.Step == 0 && m.FilledVolume < Rules.Settings.traceAmount)
                    m.Temperature = op.value; // an empty vessel just set up is at the surroundings' temperature
                break;
            case AlchemyOpKind.Suspend:
                IsSuspended = true;
                Raise(new AlchemyEvent(AlchemyEventKind.Suspended, m.Elapsed, "", ""));
                break;
            case AlchemyOpKind.Resume:
                IsSuspended = false;
                Raise(new AlchemyEvent(AlchemyEventKind.Resumed, m.Elapsed, "", ""));
                break;
            case AlchemyOpKind.Bottle:
                TakeLiquid(op.value);
                break;
            case AlchemyOpKind.CollectDistillate:
                Array.Clear(m.distillate, 0, m.distillate.Length);
                break;
        }
    }

    /// <summary>
    /// How an ingredient ground to <paramref name="form"/> goes in: all of it at that form when evenly ground, else
    /// 0.4 + 0.6 × evenness of it, the rest split between the next coarser and the next finer form (lumps and dust).
    /// </summary>
    private List<(AlchemyIngredientForm form, double share)> GrindParts(AlchemyIngredientForm form, float evenness)
    {
        var parts = new List<(AlchemyIngredientForm, double)>();
        double even = AlchemySimulator.Clamp(evenness, 0, 1);
        AlchemyIngredientForm coarser = even < 0.999 ? Rules.CoarserForm(form.id) : null;
        AlchemyIngredientForm finer = even < 0.999 ? Rules.FinerForm(form.id) : null;
        if (coarser == null && finer == null)
        {
            parts.Add((form, 1.0));
            return parts;
        }
        double main = 0.4 + 0.6 * even, rest = 1.0 - main;
        parts.Add((form, main));
        if (coarser != null && finer != null)
        {
            parts.Add((coarser, rest / 2));
            parts.Add((finer, rest / 2));
        }
        else
            parts.Add((coarser ?? finer, rest));
        return parts;
    }

    /// <summary>The units <paramref name="share"/> of an added ingredient at <paramref name="form"/> puts in (and, when <paramref name="add"/>, adds it as giving up its substances).</summary>
    private double AddPending(AlchemyOperation op, AlchemyIngredientForm form, double share, bool add)
    {
        var subs = new List<int>();
        var totals = new List<double>();
        var rates = new List<double>();
        double units = 0;
        foreach (IngredientRelease r in op.profile.releases)
        {
            int i = r != null ? Rules.IndexOf(r.substance) : -1;
            if (i < 0 || r.amount <= 0) continue;
            // Grinding very fine warms it: part of its volatile oils is lost to the air before it goes in.
            double total = r.amount * (double)op.count * share * (1.0 - form.LossFor(Rules.Substances[i]));
            subs.Add(i);
            totals.Add(total);
            rates.Add(Math.Min(PendingIngredient.Instant, form.RatePerSecond(op.profile.dissolveSeconds, r.extractability)));
            units += total;
        }
        if (add)
            Mixture.pending.Add(new PendingIngredient(op.id, op.label, form.id, subs.ToArray(), totals.ToArray(), rates.ToArray(),
                op.profile.dissolveSeconds, Mixture.Elapsed));
        return units;
    }

    /// <summary>The contents' temperature after adding <paramref name="units"/> at <paramref name="temperature"/>.</summary>
    private void MixHeat(double units, double temperature)
    {
        AlchemyMixture m = Mixture;
        double before = m.ThermalMass;
        double added = units * Rules.Settings.heatCapacityPerUnit;
        if (before + added > 1e-9)
            m.Temperature = (m.Temperature * before + temperature * added) / (before + added);
    }

    // ------------------------------------------------------------------ time
    /// <summary>
    /// Lets <paramref name="seconds"/> of preparation time pass (nothing while suspended). Runs whole steps; the
    /// remainder is kept for the next call. Returns the steps run.
    /// </summary>
    public int Advance(double seconds, int maxSteps = 2000)
    {
        if (IsSuspended || seconds <= 0)
            return 0;
        double step = Rules.Settings.stepSeconds;
        carry += seconds;
        int n = (int)Math.Floor(carry / step + 1e-9);
        if (n > maxSteps)
        {
            n = maxSteps;
            carry = 0;
        }
        else
        {
            carry -= n * step;
        }
        RunSteps(n);
        return n;
    }

    /// <summary>Runs exactly <paramref name="steps"/> simulation steps (ignores suspension: tools and replays).</summary>
    public void RunSteps(int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            stepEvents.Clear();
            AlchemySimulator.Step(Mixture, Tracker, stepEvents);
            for (int k = 0; k < stepEvents.Count; k++)
                Raise(stepEvents[k]);
        }
    }

    /// <summary>Runs until the clock reaches <paramref name="seconds"/> of preparation time.</summary>
    public void RunUntil(double seconds)
    {
        double step = Rules.Settings.stepSeconds;
        int n = (int)Math.Round((seconds - Elapsed) / step);
        if (n > 0) RunSteps(n);
    }

    private void Raise(AlchemyEvent e)
    {
        recent.Add(e);
        if (recent.Count > MaxRecentEvents)
            recent.RemoveRange(0, recent.Count - MaxRecentEvents);
        EventRaised?.Invoke(e);
    }

    // ------------------------------------------------------------------ queries
    /// <summary>A reaction's current rate (units per second).</summary>
    public double RateOf(int reaction) => reaction >= 0 && reaction < Tracker.ReactionRates.Length ? Tracker.ReactionRates[reaction] : 0;

    /// <summary>How much of a reaction happened in this preparation (units).</summary>
    public double TotalOf(int reaction) => reaction >= 0 && reaction < Tracker.ReactionTotals.Length ? Tracker.ReactionTotals[reaction] : 0;

    public bool IsReacting(int reaction) => reaction >= 0 && reaction < Tracker.ReactionActive.Length && Tracker.ReactionActive[reaction];

    public bool IsBoiling => Tracker.Boiling;

    /// <summary>What the liquid would be if it were bottled now (nothing is taken): with the particles floating in it.</summary>
    public AlchemyResult Evaluate() => AlchemyEvaluator.Evaluate(Rules, PouredPortion(), Mixture);

    /// <summary>What pouring the liquid out takes: the liquid, and the undissolved matter suspended in it.</summary>
    private double[] PouredPortion()
    {
        double[] a = Mixture.CopyAmounts();
        for (int i = 0; i < a.Length; i++)
            if (Mixture.undissolved[i] > 0)
                a[i] += Mixture.undissolved[i] * Mixture.SuspendedShare(i);
        return a;
    }

    /// <summary>What the collected distillate is (alembics).</summary>
    public AlchemyResult EvaluateDistillate() => AlchemyEvaluator.Evaluate(Rules, Mixture.distillate, Mixture);

    /// <summary>How many bottles the liquid fills now (at most <paramref name="limit"/>).</summary>
    public int BottlesAvailable(int limit = int.MaxValue) => Math.Min(limit, Evaluate().Bottles);

    // ------------------------------------------------------------------ taking things out
    /// <summary>
    /// Bottles up to <paramref name="maxBottles"/> of the liquid: returns what they are (outcome, tier, quality) and
    /// removes their liquid from the vessel - the same share of every liquid substance. Liquid left behind keeps
    /// transforming (bottle half now, let the rest go further). Solids and dissolving ingredients stay.
    /// </summary>
    public AlchemyResult Bottle(int maxBottles = int.MaxValue)
    {
        AlchemyResult r = Evaluate();
        int bottles = Math.Min(Math.Max(0, maxBottles), r.Bottles);
        if (bottles <= 0 || r.Outcome == null || !r.Outcome.producesItems)
        {
            r.Bottles = 0;
            return r;
        }
        double units = Math.Min(Mixture.LiquidVolume, bottles * Rules.UnitsPerBottle(r.Outcome));
        r.Bottles = bottles;
        r.UnitsTaken = units;
        var op = new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Bottle, value = (float)units, count = bottles, label = r.DisplayName };
        Apply(op);
        history.Add(op);
        Raise(new AlchemyEvent(AlchemyEventKind.Bottled, Elapsed, r.OutcomeId, r.DisplayName, bottles));
        return r;
    }

    /// <summary>Removes <paramref name="units"/> of liquid, the same share of every liquid substance.</summary>
    private void TakeLiquid(double units)
    {
        double liquid = Mixture.LiquidVolume;
        if (liquid <= 1e-9 || units <= 0)
            return;
        double keep = Math.Max(0, 1 - units / liquid);
        double taken = 1 - keep;
        for (int i = 0; i < Mixture.amounts.Length; i++)
        {
            if (Rules.Substances[i].phase != SubstancePhase.Liquid) continue;
            // The particles floating in the liquid go with it (an unfiltered brew is cloudy).
            if (Mixture.undissolved[i] > 0)
                Mixture.undissolved[i] -= Mixture.undissolved[i] * Mixture.SuspendedShare(i) * taken;
            Mixture.amounts[i] *= keep;
        }
    }

    // ------------------------------------------------------------------ separating
    /// <summary>
    /// Pours the liquid through <paramref name="filterId"/> (one of the rules' Filters): it strains out the ingredients
    /// still in the vessel, catches undissolved matter and solids (returned as what can be collected: crystals, salts...),
    /// holds back what it adsorbs and soaks up a little liquid. Null when there is nothing to filter.
    /// </summary>
    public AlchemyHarvest Filter(string filterId, out string reason)
    {
        reason = "";
        AlchemyFilter f = Rules.Filter(filterId);
        if (f == null)
        {
            reason = $"Unknown filter '{filterId}'.";
            return null;
        }
        if (Mixture.LiquidVolume <= Rules.Settings.traceAmount)
        {
            reason = "There is no liquid to filter.";
            return null;
        }
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Filter, id = f.id, label = f.Name });
        var h = new AlchemyHarvest { Liquid = Evaluate() };
        AlchemyEvaluator.Harvest(Rules, lastTaken, SubstancePhase.Solid, h.Items);
        AlchemyEvaluator.Harvest(Rules, lastTaken, SubstancePhase.Liquid, h.Items);
        h.Taken = lastTaken;
        Raise(new AlchemyEvent(AlchemyEventKind.Filtered, Elapsed, f.id, f.Name));
        return h;
    }

    /// <summary>
    /// Pours off the top layer of a layered liquid (oil over water...): returns what it is (bottle it when it is a potion)
    /// and, when it bottles nothing, its liquids to collect (recovered oil, an essential oil). Null when it is not layered.
    /// </summary>
    public AlchemyHarvest Decant(out string reason)
    {
        reason = "";
        if (!Mixture.IsLayered)
        {
            reason = "The liquid is not in layers: there is nothing to pour off.";
            return null;
        }
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Decant });
        var h = new AlchemyHarvest { Liquid = AlchemyEvaluator.Evaluate(Rules, lastTaken, Mixture), Taken = lastTaken };
        if (h.Liquid.Outcome == null || !h.Liquid.Outcome.producesItems)
        {
            h.Liquid.Bottles = 0;
            AlchemyEvaluator.Harvest(Rules, lastTaken, SubstancePhase.Liquid, h.Items);
        }
        Raise(new AlchemyEvent(AlchemyEventKind.Decanted, Elapsed, h.Liquid.OutcomeId, h.Liquid.DisplayName));
        return h;
    }

    /// <summary>Skims off what floats: undissolved matter and solids lighter than the liquid (wax, charcoal, ash). Null when nothing floats.</summary>
    public AlchemyHarvest Skim(out string reason)
    {
        reason = "";
        bool any = false;
        for (int i = 0; i < Mixture.amounts.Length; i++)
            any |= Mixture.Floats(i) && (Mixture.undissolved[i] > Rules.Settings.noticeableAmount ||
                                         Rules.Substances[i].phase == SubstancePhase.Solid && Mixture.amounts[i] > Rules.Settings.noticeableAmount);
        if (!any)
        {
            reason = "Nothing floats on the liquid.";
            return null;
        }
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.Skim });
        var h = new AlchemyHarvest { Liquid = Evaluate(), Taken = lastTaken };
        AlchemyEvaluator.Harvest(Rules, lastTaken, SubstancePhase.Solid, h.Items);
        AlchemyEvaluator.Harvest(Rules, lastTaken, SubstancePhase.Liquid, h.Items);
        Raise(new AlchemyEvent(AlchemyEventKind.Skimmed, Elapsed, "", ""));
        return h;
    }

    private double[] ApplyFilter(AlchemyFilter f)
    {
        AlchemyMixture m = Mixture;
        var caught = new double[m.amounts.Length];
        if (f == null)
            return caught;
        if (f.strainsIngredients)
            m.pending.Clear(); // the spent herbs, with what they still hold
        for (int i = 0; i < caught.Length; i++)
        {
            if (Rules.Substances[i].phase == SubstancePhase.Solid)
            {
                double x = m.amounts[i] * f.catchesSolids;
                m.amounts[i] -= x;
                caught[i] += x;
            }
            else if (m.undissolved[i] > 0)
            {
                double x = m.undissolved[i] * f.catchesUndissolved;
                m.undissolved[i] -= x;
                caught[i] += x;
            }
        }
        if (f.adsorbs != null)
            foreach (FilterAdsorption ad in f.adsorbs)
            {
                if (ad?.substance == null) continue;
                foreach (int i in Rules.Match(ad.substance))
                    if (Rules.Substances[i].phase == SubstancePhase.Liquid && Rules.Substances[i].role != SubstanceRole.Solvent)
                        m.amounts[i] *= 1.0 - ad.share;
            }
        double keep = 1.0 - f.liquidLoss;
        for (int i = 0; i < caught.Length; i++)
            if (Rules.Substances[i].phase == SubstancePhase.Liquid)
                m.amounts[i] *= keep;
        m.Agitation = Math.Max(m.Agitation, 0.2); // poured through and back
        return caught;
    }

    private double[] ApplyDecant()
    {
        AlchemyMixture m = Mixture;
        var taken = new double[m.amounts.Length];
        if (!m.Layers(out double aq, out double oil) || !m.LayerSummary(out double top, out _, out double bottom, out _, out bool oilOnTop))
            return taken;
        // An emulsion pours off as a blend of both layers. Every share is read before anything is poured.
        double blend = AlchemySimulator.Clamp(m.Emulsion, 0, 1);
        double topShare = top + bottom > 1e-9 ? top / (top + bottom) : 0;
        for (int i = 0; i < taken.Length; i++)
        {
            if (m.amounts[i] <= 0 || Rules.Substances[i].phase != SubstancePhase.Liquid) continue;
            double p = m.OilShare(i, aq, oil);
            taken[i] = m.amounts[i] * ((oilOnTop ? p : 1 - p) * (1 - blend) + topShare * blend);
        }
        for (int i = 0; i < taken.Length; i++)
            m.amounts[i] -= taken[i];
        return taken;
    }

    private double[] ApplySkim()
    {
        AlchemyMixture m = Mixture;
        var taken = new double[m.amounts.Length];
        for (int i = 0; i < taken.Length; i++)
        {
            if (!m.Floats(i)) continue;
            taken[i] = m.undissolved[i] + (Rules.Substances[i].phase == SubstancePhase.Solid ? m.amounts[i] : 0);
            m.undissolved[i] = 0;
            if (Rules.Substances[i].phase == SubstancePhase.Solid)
                m.amounts[i] = 0;
        }
        return taken;
    }

    /// <summary>
    /// Takes the alembic's distillate: what it is (outcome; the station bottles it when the outcome makes items) and,
    /// when it bottles nothing, its harvestable liquids (a distilled spirit...).
    /// </summary>
    public AlchemyHarvest CollectDistillate()
    {
        var h = new AlchemyHarvest { Liquid = EvaluateDistillate() };
        if (Mixture.DistillateVolume <= Rules.Settings.traceAmount)
        {
            h.Liquid.Bottles = 0;
            return h;
        }
        if (h.Liquid.Outcome == null || !h.Liquid.Outcome.producesItems)
        {
            h.Liquid.Bottles = 0;
            AlchemyEvaluator.Harvest(Rules, Mixture.distillate, SubstancePhase.Liquid, h.Items);
        }
        Record(new AlchemyOperation { step = Mixture.Step, kind = AlchemyOpKind.CollectDistillate, label = h.Liquid.DisplayName });
        Raise(new AlchemyEvent(AlchemyEventKind.DistillateCollected, Elapsed, h.Liquid.OutcomeId, h.Liquid.DisplayName, h.Liquid.Bottles));
        return h;
    }

    /// <summary>
    /// Ends the preparation: what the liquid was (poured away unless you bottle it first), the solids harvested
    /// from the bottom (crystals, ash...) and the distillate. The brew is spent afterwards.
    /// </summary>
    public AlchemyHarvest Empty()
    {
        var h = new AlchemyHarvest { Liquid = Evaluate() };
        // A film of residues and contaminants stays in the vessel (a station's next preparation starts with it unless cleaned).
        double film = Rules.Settings.vesselResidueShare;
        int dirt = Rules.PropertyIndex(Rules.Settings.contaminantProperty);
        if (film > 0)
            for (int i = 0; i < Mixture.amounts.Length; i++)
            {
                AlchemySubstance s = Rules.Substances[i];
                bool sticks = s.role == SubstanceRole.Residue || dirt >= 0 && Rules.PropertyMatrix[i, dirt] > 0;
                double units = (Mixture.amounts[i] + Mixture.undissolved[i]) * film;
                if (sticks && units >= Rules.Settings.noticeableAmount * 0.2)
                    h.Leftovers.Add(new SubstanceAmount(s.id, (float)units));
            }
        AlchemyEvaluator.Harvest(Rules, Mixture.amounts, SubstancePhase.Solid, h.Items);
        AlchemyEvaluator.Harvest(Rules, Mixture.undissolved, SubstancePhase.Liquid, h.Items); // what settled out of solution
        if (Mixture.DistillateVolume > Rules.Settings.traceAmount)
        {
            h.Distillate = EvaluateDistillate();
            if (h.Distillate.Outcome == null || !h.Distillate.Outcome.producesItems)
                AlchemyEvaluator.Harvest(Rules, Mixture.distillate, SubstancePhase.Liquid, h.Items);
        }
        Array.Clear(Mixture.amounts, 0, Mixture.amounts.Length);
        Array.Clear(Mixture.undissolved, 0, Mixture.undissolved.Length);
        Array.Clear(Mixture.vapor, 0, Mixture.vapor.Length);
        Array.Clear(Mixture.distillate, 0, Mixture.distillate.Length);
        Mixture.pending.Clear();
        Raise(new AlchemyEvent(AlchemyEventKind.Emptied, Elapsed, h.Liquid.OutcomeId, h.Liquid.DisplayName));
        return h;
    }

    // ------------------------------------------------------------------ saving and replaying
    /// <summary>Everything about the preparation, ready for JsonUtility (saves, the network, the editor).</summary>
    public AlchemyBrewState Capture() => AlchemyBrewState.From(this);

    /// <summary>A preparation exactly as captured (missing substances of older rules are dropped).</summary>
    public static AlchemyBrew Restore(AlchemyRules rules, AlchemyBrewState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        var brew = new AlchemyBrew(rules, state.equipment, state.ambient, state.seed);
        state.ApplyTo(brew);
        return brew;
    }

    internal void RestoreInternals(string id, bool suspended, double carried, List<AlchemyOperation> ops)
    {
        Id = string.IsNullOrEmpty(id) ? Id : id;
        IsSuspended = suspended;
        carry = carried;
        history.Clear();
        if (ops != null)
            foreach (AlchemyOperation op in ops)
                if (op != null) history.Add(op.Clone());
    }

    internal double Carry => carry;

    /// <summary>
    /// Rebuilds a preparation from its history: the operations are applied at their steps, the time in between is
    /// simulated. With the same rules, equipment, surroundings and seed the result is the same mixture - the proof that
    /// a discovery can be made again. Runs until <paramref name="untilStep"/> (or the last operation).
    /// </summary>
    public static AlchemyBrew Replay(AlchemyRules rules, AlchemyEquipmentProfile equipment, double ambient, int seed,
        IEnumerable<AlchemyOperation> operations, long untilStep = -1)
    {
        var brew = new AlchemyBrew(rules, equipment, ambient, seed);
        if (operations != null)
            foreach (AlchemyOperation op in operations)
            {
                if (op == null) continue;
                if (op.step > brew.Mixture.Step)
                    brew.RunSteps((int)(op.step - brew.Mixture.Step));
                AlchemyOperation copy = op.Clone();
                brew.Apply(copy);
                brew.history.Add(copy);
            }
        if (untilStep > brew.Mixture.Step)
            brew.RunSteps((int)(untilStep - brew.Mixture.Step));
        return brew;
    }
}

/// <summary>What a preparation gave when emptied or when its distillate was collected.</summary>
public sealed class AlchemyHarvest
{
    /// <summary>What the liquid (or distillate) is.</summary>
    public AlchemyResult Liquid;
    /// <summary><see cref="AlchemyBrew.Empty"/>: what the distillate left in the alembic's flask was (null when there was none).</summary>
    public AlchemyResult Distillate;
    /// <summary>What can be collected: substance id → count, with its Harvest Item (null = none set: the station decides).</summary>
    public readonly List<AlchemyHarvestItem> Items = new List<AlchemyHarvestItem>();
    /// <summary>Filter, decant, skim: what was taken out (substance index → units).</summary>
    public double[] Taken;
    /// <summary><see cref="AlchemyBrew.Empty"/>: the film of residues left in the vessel (the station keeps it for its next preparation).</summary>
    public readonly List<SubstanceAmount> Leftovers = new List<SubstanceAmount>();
}

public struct AlchemyHarvestItem
{
    public string Substance;
    public ItemSO Item;
    public int Count;
}
