using System;
using System.Collections.Generic;

/// <summary>
/// Brings a field up to a time the way it would have lived it: the weather and every plant together, in steps of at most
/// <see cref="AgriSettings.maxStepHours"/> - each step's weather first (rain, drying, warmth into the soil's totals, the
/// soil's temperature, the light of the day), then every plant to the step's end, then the dead that have lain long
/// enough are gone, as they would have been. The seasons follow from the date of each step, and the weather of the past
/// is the climate's (deterministic: the same hours give the same weather), so a garden that slept for a season wakes as if
/// someone had watched it all along.
/// <para>A sleeping garden is woken with it and kept living in the background with it. The work can be spread over
/// frames: <see cref="Run"/> stops when its time is up and goes on where it stopped; the target may move on while it runs
/// (the clock does).</para>
/// <para>Two shortcuts keep it light without changing what it gives (each can be turned off in the settings). A garden
/// with no living plant takes steps of <see cref="BareSteps"/> at once (<see cref="AgriSettings.fastForwardBareGardens"/>):
/// its soil is lazy - a cell is worked out when something touches it, from the running totals of the weather - so the
/// steps were only ever there for the plants; the weather is still sampled exactly as before. And in a quiet stretch a
/// plant goes on with what it last saw of its soil and neighbours (<see cref="AgriSettings.quietSteps"/>, see
/// <see cref="PlantSurroundings"/>). The steps themselves stay short: a plant's step sees the weather of its last hour,
/// so a twelve-hour step would see one hour's frost, heat or sun for twelve - the frosts, the ripening and the harvest
/// windows are why they are short.</para>
/// </summary>
public sealed class FieldCatchUp
{
    /// <summary>Hours before <see cref="LiveNow"/> within which a live (non-historic) weather source is used as it is.</summary>
    public const double LiveHours = 2.0;

    /// <summary>Steps a garden with no living plant takes at once (each the length of <see cref="AgriSettings.maxStepHours"/>).</summary>
    public const int BareSteps = 16;

    private readonly FieldState subject;
    private readonly IAgriClimateSource climate, history;
    private readonly PlantScratch scratch = new PlantScratch();
    private double stepEnd = -1.0;
    private int cursor;

    /// <param name="field">The field to bring up to date.</param>
    /// <param name="climate">The field's weather.</param>
    /// <param name="history">Stands in for <paramref name="climate"/> where it cannot tell the past (a live Weather System
    /// only knows now): the city's climate. Null: the climate of the field's settings.</param>
    /// <param name="target">The time to reach (game hours).</param>
    public FieldCatchUp(FieldState field, IAgriClimateSource climate, IAgriClimateSource history, double target)
    {
        subject = field ?? throw new ArgumentNullException(nameof(field));
        this.climate = climate;
        this.history = history ?? new CityClimateSource(subject.Settings);
        Target = target;
        LiveNow = target;
        // What the plants saw before (awake, or in another catch-up) is not carried into this one.
        subject.QuietEpoch++;
    }

    public FieldState Field => subject;

    /// <summary>The time to reach (game hours). It may be raised while the work runs.</summary>
    public double Target { get; set; }

    /// <summary>The present: a live weather source is used only for its last <see cref="LiveHours"/>.</summary>
    public double LiveNow { get; set; }

    /// <summary>Plant steps done so far.</summary>
    public int PlantSteps { get; private set; }

    /// <summary>Weather steps done so far.</summary>
    public int WeatherSteps { get; private set; }

    /// <summary>Plant steps that went on with what the plant last saw of its soil and neighbours (a quiet stretch).</summary>
    public int QuietPlantSteps => scratch.kept;

    /// <summary>Hours caught up in the long steps of a garden with no living plant.</summary>
    public double BareHours { get; private set; }

    /// <summary>The field has reached the target.</summary>
    public bool Done => stepEnd < 0.0 && subject.EnvTime >= Target - 1e-6;

    /// <summary>
    /// Works until the target is reached (true) or until <paramref name="outOfTime"/> says stop (false: call again to go
    /// on). <paramref name="outOfTime"/> null: all at once.
    /// </summary>
    public bool Run(Func<bool> outOfTime = null)
    {
        AgriSettings s = subject.Settings;
        double chunk = Math.Max(0.25, s.maxStepHours);
        scratch.quiet = s.quietSteps;
        while (true)
        {
            if (stepEnd < 0.0)
            {
                if (subject.EnvTime >= Target - 1e-6) return true;
                double from = subject.EnvTime < 0.0 ? Target : subject.EnvTime;
                stepEnd = Math.Min(Target, from + chunk);
                if (s.fastForwardBareGardens && from >= 0.0 && Bare())
                {
                    // Nothing living: many steps at once. The weather inside is sampled as it always is; a long step
                    // stays clear of a live weather's last hours, where which source each short step had matters.
                    double far = Math.Min(Target, from + chunk * BareSteps);
                    if (climate == null || climate.Historic || LiveNow - far > LiveHours)
                    {
                        BareHours += far - from;
                        stepEnd = far;
                    }
                }
                subject.TickEnvironment(stepEnd, SourceFor(stepEnd));
                WeatherSteps++;
                cursor = 0;
            }
            List<PlantState> plants = subject.Plants;
            IAgriClimateSource source = SourceFor(stepEnd);
            while (cursor < plants.Count)
            {
                if (outOfTime != null && outOfTime()) return false;
                PlantState p = plants[cursor++];
                if (p == null || p.last >= stepEnd - 1e-9) continue;
                PlantSim.Advance(subject, p, stepEnd, scratch, source);
                PlantSteps++;
            }
            subject.SweepDead(s.deadPlantDays * 24f);
            stepEnd = -1.0;
            if (outOfTime != null && outOfTime()) return Done;
        }
    }

    /// <summary>No plant is alive (none, or only the dead waiting to be gone: a dead plant only counts its hours).</summary>
    private bool Bare()
    {
        List<PlantState> plants = subject.Plants;
        for (int i = 0; i < plants.Count; i++)
            if (plants[i] != null && plants[i].Alive) return false;
        return true;
    }

    /// <summary>The weather for a step ending at <paramref name="t"/>: a live source only for the present, the climate for the past.</summary>
    private IAgriClimateSource SourceFor(double t)
    {
        if (climate == null) return history;
        if (climate.Historic || LiveNow - t <= LiveHours) return climate;
        return history;
    }

    /// <summary>Plant steps a catch-up of <paramref name="hours"/> costs for a field (an estimate for budgets and tools).</summary>
    public static long Cost(FieldState field, double hours)
    {
        if (field == null || hours <= 0.0) return 0;
        double chunk = Math.Max(0.25, field.Settings.maxStepHours);
        return (long)Math.Ceiling(hours / chunk) * Math.Max(1, field.Plants.Count);
    }
}
