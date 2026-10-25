using System;
using System.Collections.Generic;

/// <summary>
/// The player's hands: a seeded, repeatable unsteadiness that grows as they tire and shrinks with practice. Every
/// minigame passes its input through the hands, so the same input from the same state always gives the same result
/// (replays and tests stay exact) while still never being perfect.
/// </summary>
[Serializable]
public sealed class CraftHands
{
    public int seed = 1;
    /// <summary>Movements made (advances the noise).</summary>
    public int count;
    /// <summary>0 fresh .. 1 exhausted.</summary>
    public double fatigue;
    /// <summary>0 novice .. 1 master (from practice; the station sets it from the journal).</summary>
    public double skill;

    public CraftHands() { }
    public CraftHands(int seed, double skill = 0) { this.seed = seed; this.skill = CraftMath.Clamp01(skill); }

    /// <summary>How unsteady the hands are now (a standard-deviation-like scale).</summary>
    public double Unsteadiness => (0.025 + 0.09 * fatigue) * (1 - 0.6 * skill);

    /// <summary>A signed tremble (-1..1 scaled by <see cref="Unsteadiness"/>) for one movement.</summary>
    public double Tremble(int salt)
    {
        double n = CraftMath.Noise(seed, count, salt) * 2 - 1;
        return n * Unsteadiness;
    }

    /// <summary>One movement made with <paramref name="effort"/> (0-1).</summary>
    public void Move(double effort)
    {
        count++;
        fatigue = Math.Min(1, fatigue + 0.004 + 0.02 * effort * effort);
    }

    public void Rest(double seconds) => fatigue = Math.Max(0, fatigue * Math.Exp(-seconds / 25));
}

/// <summary>A hammer blow as the minigame produced it, ready for <see cref="CraftWorkshop.Strike"/>.</summary>
public struct CraftBlow
{
    public double position;
    public double force;
    public double tilt;
    /// <summary>How far the blow strayed from where the player aimed (0-1 of the piece).</summary>
    public double miss;
    /// <summary>0 smooth swing .. 1 snatched (struck too soon after the last blow, or overcharged).</summary>
    public double rushed;
}

/// <summary>
/// The hammer: aim along the piece, hold to raise the hammer (the longer, the harder, until the arm tires and the
/// blow wavers), tilt it to draw a bevel, release to strike. A blow snatched too soon after the last one has not had
/// time to fall from height.
/// </summary>
public sealed class CraftHammer
{
    /// <summary>Seconds to reach a full blow.</summary>
    public double fullCharge = 0.9;
    /// <summary>Seconds a full blow can be held before it wavers.</summary>
    public double holdBeforeWaver = 0.6;
    /// <summary>The fastest comfortable rhythm (seconds between blows).</summary>
    public double rhythm = 0.45;
    /// <summary>The hammer's weight (heavier: harder blows, more tiring, less accurate).</summary>
    public double weight = 1;

    public bool Raised { get; private set; }
    public double Held { get; private set; }
    private double sinceLast = 99;

    public void Raise() { Raised = true; Held = 0; }
    public void Lower() { Raised = false; Held = 0; }

    public void Tick(double dt)
    {
        sinceLast += dt;
        if (Raised) Held += dt;
    }

    /// <summary>The blow's strength if released now (0-1.2), before the hands' tremble.</summary>
    public double Charge
    {
        get
        {
            if (!Raised) return 0;
            double c = Math.Min(1, Held / Math.Max(0.05, fullCharge));
            c = 1 - (1 - c) * (1 - c); // eases in
            double over = Held - fullCharge - holdBeforeWaver;
            if (over > 0) c *= 1 - Math.Min(0.4, over * 0.3) * (0.5 + 0.5 * Math.Sin(over * 9)); // the arm wavers
            return c * (0.8 + 0.2 * weight);
        }
    }

    /// <summary>Strikes at <paramref name="aim"/> (0-1 along the piece) with <paramref name="tilt"/> (-1..1).</summary>
    public CraftBlow Release(double aim, double tilt, CraftHands hands)
    {
        double charge = Charge;
        double rushed = sinceLast < rhythm ? 1 - sinceLast / rhythm : 0;
        double over = Math.Max(0, Held - fullCharge - holdBeforeWaver);
        rushed = Math.Max(rushed, Math.Min(1, over * 0.5));
        double shake = hands.Unsteadiness * (0.7 + 0.5 * weight) * (1 + rushed);
        double dAim = (CraftMath.Noise(hands.seed, hands.count, 11) * 2 - 1) * shake * 0.6;
        double dTilt = (CraftMath.Noise(hands.seed, hands.count, 12) * 2 - 1) * shake * 2.5;
        double dForce = (CraftMath.Noise(hands.seed, hands.count, 13) * 2 - 1) * shake * 1.5;
        var blow = new CraftBlow
        {
            position = CraftMath.Clamp01(aim + dAim),
            tilt = CraftMath.Clamp(tilt + dTilt, -1, 1),
            force = CraftMath.Clamp(charge * (1 - 0.6 * rushed) * (1 + dForce), 0.02, 1.2),
            miss = Math.Abs(dAim),
            rushed = rushed,
        };
        hands.Move(blow.force * weight);
        Raised = false;
        Held = 0;
        sinceLast = 0;
        return blow;
    }
}

/// <summary>
/// A stroke tracker for the abrasive and leather benches: the player drags across the work and this measures what the
/// hands did - the span covered, the angle held and how much it wobbled, the pressure, how long, how many passes,
/// and whether the edge led. Feed it samples in the work's coordinates (x along 0-1, y across 0-1).
/// </summary>
public sealed class CraftStroke
{
    private readonly List<double> xs = new List<double>(), ys = new List<double>(), ts = new List<double>(), angles = new List<double>(), pressures = new List<double>();
    public bool Active => xs.Count > 0;
    public int Samples => xs.Count;

    public void Clear()
    {
        xs.Clear(); ys.Clear(); ts.Clear(); angles.Clear(); pressures.Clear();
    }

    /// <summary>Adds one sample; the hands' tremble is added to the angle (degrees) and the pressure (0-1).</summary>
    public void Add(double time, double x, double y, double angle, double pressure, CraftHands hands)
    {
        double jitter = hands == null ? 0 : hands.Tremble(xs.Count + 21);
        xs.Add(CraftMath.Clamp01(x));
        ys.Add(CraftMath.Clamp01(y));
        ts.Add(time);
        angles.Add(angle + jitter * 40);
        pressures.Add(CraftMath.Clamp01(pressure + jitter * 0.5));
        if (hands != null && xs.Count % 6 == 0) hands.Move(pressure * 0.5);
    }

    public double From { get { double m = 1; foreach (double x in xs) m = Math.Min(m, x); return xs.Count > 0 ? m : 0; } }
    public double To { get { double m = 0; foreach (double x in xs) m = Math.Max(m, x); return m; } }
    public double Seconds => ts.Count > 1 ? Math.Max(0, ts[ts.Count - 1] - ts[0]) : 0;
    public double Pressure => Mean(pressures);
    public double Angle => Mean(angles);

    /// <summary>How unsteady the angle was (degrees, standard deviation).</summary>
    public double Wobble
    {
        get
        {
            if (angles.Count < 2) return 0;
            double m = Angle, s = 0;
            foreach (double a in angles) s += (a - m) * (a - m);
            return Math.Sqrt(s / angles.Count);
        }
    }

    /// <summary>How many passes along the work (direction reversals along x, plus one).</summary>
    public int Passes
    {
        get
        {
            if (xs.Count < 3) return xs.Count > 0 ? 1 : 0;
            int passes = 1, dir = 0;
            for (int i = 1; i < xs.Count; i++)
            {
                double d = xs[i] - xs[i - 1];
                if (Math.Abs(d) < 0.01) continue;
                int s = Math.Sign(d);
                if (dir != 0 && s != dir) passes++;
                dir = s;
            }
            return passes;
        }
    }

    /// <summary>Did the stroke move into the edge (y rising toward 1, where the edge is drawn) more than away from it?</summary>
    public bool EdgeLeading
    {
        get
        {
            double toward = 0, away = 0;
            for (int i = 1; i < ys.Count; i++)
            {
                double d = ys[i] - ys[i - 1];
                if (d > 0) toward += d; else away -= d;
            }
            return toward >= away;
        }
    }

    /// <summary>The segment range covered on a piece of <paramref name="segments"/> segments.</summary>
    public void Span(int segments, out int from, out int to)
    {
        from = Math.Max(0, (int)Math.Floor(From * segments));
        to = Math.Min(segments - 1, (int)Math.Floor(To * segments));
        if (to < from) to = from;
    }

    /// <summary>The path as x, y pairs in hide cells (for the leather operations).</summary>
    public List<float> Path(int width, int height)
    {
        var path = new List<float>(xs.Count * 2);
        for (int i = 0; i < xs.Count; i++)
        {
            path.Add((float)(xs[i] * width));
            path.Add((float)(ys[i] * height));
        }
        return path;
    }

    private static double Mean(List<double> v)
    {
        if (v.Count == 0) return 0;
        double s = 0;
        foreach (double x in v) s += x;
        return s / v.Count;
    }
}

/// <summary>
/// Pouring: the player tilts the crucible (0 upright .. 1 tipped over). The rate follows the tilt with a lag; the
/// steadiness is how little the hands moved the tilt while the metal ran; an interruption is the stream stopping and
/// starting again, which leaves a cold shut in the casting.
/// </summary>
public sealed class CraftPourControl
{
    public double Rate { get; private set; }
    public double Poured { get; private set; }
    public int Interruptions { get; private set; }
    public bool Started => Poured > 0;
    private double sum, time, waver, lastTarget = -1;
    private bool flowing;

    /// <summary>One frame of pouring; returns the fraction of the mould filled this frame (rate × dt / fill time).</summary>
    public double Tick(double dt, double tilt, CraftHands hands, double fillSeconds = 4)
    {
        double target = CraftMath.Clamp01((tilt - 0.25) / 0.6);
        // The hands drift slowly (a new waver every quarter second), not frame by frame.
        if (hands != null) target = CraftMath.Clamp01(target + hands.Tremble((int)((time + Poured) * 4) + 31) * 2);
        Rate += (target - Rate) * Math.Min(1, dt * 6);
        bool now = Rate > 0.04;
        if (flowing && !now && Poured < 1) Interruptions++;
        flowing = now;
        if (now)
        {
            if (lastTarget >= 0) waver += Math.Abs(target - lastTarget);
            sum += Rate * dt;
            time += dt;
        }
        lastTarget = target;
        double add = now ? Rate * dt / Math.Max(0.5, fillSeconds) : 0;
        Poured = Math.Min(1, Poured + add);
        return add;
    }

    /// <summary>The mean rate while the metal ran (0-1).</summary>
    public double MeanRate => time > 0 ? sum / time : 0;

    /// <summary>1 a steady stream .. 0 a sputtering one (how much the tilt moved per second of pouring).</summary>
    public double Steadiness => time > 0 ? CraftMath.Clamp01(1 - waver / time * 0.6) : 0;
}

/// <summary>
/// A rhythm for stitching, punching and wrapping: beats come at a steady pace and the player answers each one. The
/// timing error of each answer becomes the spacing error (punching), the spread of tension (stitching, wrapping);
/// how hard they pull is the tension.
/// </summary>
public sealed class CraftRhythm
{
    public double interval = 0.7;
    public double window = 0.35;
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    private double clock, errSum, errSq, pullSum;

    public void Tick(double dt) { clock += dt; }

    /// <summary>How far through the current beat (0 just after a beat .. 1 the next beat).</summary>
    public double Phase => clock % interval / interval;

    /// <summary>The player answers a beat with <paramref name="pull"/> (0-1). Returns the timing error (0 perfect .. 1).</summary>
    public double Answer(double pull, CraftHands hands)
    {
        double phase = Phase;
        double off = Math.Min(phase, 1 - phase) * interval; // seconds from the nearest beat
        double err = CraftMath.Clamp01(off / window);
        if (hands != null) err = CraftMath.Clamp01(err + Math.Abs(hands.Tremble(Hits + Misses + 41)) * 2);
        if (err >= 1) Misses++; else Hits++;
        errSum += err;
        errSq += err * err;
        pullSum += CraftMath.Clamp01(pull + (hands?.Tremble(Hits + Misses + 42) ?? 0));
        hands?.Move(0.3);
        return err;
    }

    public int Count => Hits + Misses;
    public double MeanError => Count > 0 ? errSum / Count : 0;
    public double Spread => Count > 0 ? Math.Sqrt(Math.Max(0, errSq / Count - MeanError * MeanError)) + MeanError * 0.5 : 0;
    public double Pull => Count > 0 ? pullSum / Count : 0;

    public void Clear()
    {
        Hits = Misses = 0;
        clock = errSum = errSq = pullSum = 0;
    }
}

/// <summary>
/// The bellows: each full pump (down then up) adds a puff of air; pumping in a steady rhythm builds a strong draft,
/// frantic pumping only tires the arms.
/// </summary>
public sealed class CraftBellowsPump
{
    private double lastPump = -9, clock;
    public double Strength { get; private set; }

    public void Tick(double dt)
    {
        clock += dt;
        Strength *= Math.Exp(-dt / 1.5);
    }

    /// <summary>One pump. Returns the strength to pass to <see cref="CraftWorkshop.Bellows"/>.</summary>
    public double Pump(CraftHands hands)
    {
        double gap = clock - lastPump;
        lastPump = clock;
        double steady = gap < 0.25 ? 0.35 : gap < 0.5 ? 0.75 : gap < 1.5 ? 1 : 0.8;
        Strength = Math.Min(1, Strength * 0.6 + 0.45 * steady);
        hands?.Move(0.4);
        return Strength;
    }
}
