using System;
using System.Collections.Generic;

/// <summary>
/// The player's hands: a seeded, repeatable unsteadiness that grows as they tire and shrinks with practice. Every
/// minigame passes its input through the hands, so the same input from the same state always gives the same result
/// (replays and tests stay exact) while still never being perfect.
/// <para>A tired arm is weaker (<see cref="Strength"/>: blows land lighter, the bellows puff less, the wheel is pressed
/// less), less precise (<see cref="Unsteadiness"/>) and now and then slips (<see cref="Slips"/>: a glancing blow, a
/// knife that skids). It rests while it is not working.</para>
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
    /// <summary>Seconds since the last movement (resting starts in earnest after a moment).</summary>
    public double idle;
    /// <summary>How much less a master tires (fatigue × (1 - this × skill); from the mastery settings).</summary>
    public double fatigueRelief = 0.4;

    public CraftHands() { }
    public CraftHands(int seed, double skill = 0) { this.seed = seed; this.skill = CraftMath.Clamp01(skill); }

    /// <summary>How much steadier a master's hands are (the tremble and the slips × (1 - this × skill)).</summary>
    public const double SkillSteadies = 0.6;

    /// <summary>How unsteady the hands are now (a standard-deviation-like scale).</summary>
    public double Unsteadiness => (0.025 + 0.09 * fatigue) * (1 - SkillSteadies * skill);

    /// <summary>A signed tremble (-1..1 scaled by <see cref="Unsteadiness"/>) for one movement.</summary>
    public double Tremble(int salt)
    {
        double n = CraftMath.Noise(seed, count, salt) * 2 - 1;
        return n * Unsteadiness;
    }

    /// <summary>How much of a fresh arm's strength is left: 1 fresh .. 0.45 spent.</summary>
    public double Strength => 1 - 0.55 * fatigue * fatigue;

    /// <summary>
    /// How tired an arm is, in steps: 0 fresh, 1 tiring (from 0.35), 2 heavy (from 0.6), 3 spent (from 0.85). Its words
    /// change at these steps, and the arm's bar takes its colour from them (green, gold, orange, red and trembling).
    /// </summary>
    public static int Level(double fatigue) => fatigue >= 0.85 ? 3 : fatigue >= 0.6 ? 2 : fatigue >= 0.35 ? 1 : 0;

    /// <summary>The fatigue a <see cref="Level"/> starts at.</summary>
    public static double LevelFrom(int level) => level >= 3 ? 0.85 : level == 2 ? 0.6 : level == 1 ? 0.35 : 0;

    /// <summary>
    /// How an arm at <paramref name="level"/> feels, in words for the work in hand: blows (<paramref name="blows"/>: a
    /// hammer, a maul, a mallet), or any other work of the hands (a draft, a stroke, a turn); empty while it is fresh.
    /// </summary>
    public static string FeelingAt(int level, bool blows) => level <= 0 ? ""
        : level == 1 ? (blows ? "Your arm is tiring: the blows land lighter and less true." : "Your arm is tiring: the hand works lighter and less true.")
        : level == 2 ? (blows ? "Your arm is heavy: weak, wandering blows, and one may glance off. Rest a moment." : "Your arm is heavy: the hand wanders and may slip. Rest a moment.")
        : "Your arm is spent: it trembles, the tools slip. Put it down and rest.";

    /// <summary>How tired the arm feels, in words for blows (empty while it is fresh): <see cref="FeelingAt"/> at its <see cref="Level"/>.</summary>
    public string Feeling => FeelingAt(Level(fatigue), true);

    /// <summary>
    /// Whether this movement slips (seeded, so repeatable): never while the arm is fresh, more and more as it tires,
    /// less with practice, more when rushed.
    /// </summary>
    public bool Slips(int salt, double rushed = 0)
    {
        double chance = Math.Max(0, fatigue - 0.4) * 0.6 * (1 - SkillSteadies * skill) + CraftMath.Clamp01(rushed) * 0.15 * fatigue;
        return chance > 0 && CraftMath.Noise(seed, count, salt + 97) < chance;
    }

    /// <summary>One movement made with <paramref name="effort"/> (0-1).</summary>
    public void Move(double effort)
    {
        count++;
        idle = 0;
        // a practised arm's movements are sure and economical: it tires less
        fatigue = Math.Min(1, fatigue + (0.004 + 0.02 * effort * effort) * (1 - fatigueRelief * CraftMath.Clamp01(skill)));
    }

    /// <summary>
    /// Light work that is no movement of the working hand (the bellows worked in a steady rhythm): it tires the arm by
    /// <paramref name="fatigueAdded"/> (less with practice) without Move's cost per movement, and the arm still rests
    /// between its own movements - as a smith's hammer arm rests while the bar heats.
    /// </summary>
    public void Exert(double fatigueAdded)
    {
        fatigue = Math.Min(1, fatigue + Math.Max(0, fatigueAdded) * (1 - fatigueRelief * CraftMath.Clamp01(skill)));
    }

    /// <summary>
    /// Time passing: the arm recovers slowly while it works between movements and properly once it has been still for a
    /// couple of seconds (about half the tiredness goes in twenty seconds of rest).
    /// </summary>
    public void Rest(double seconds)
    {
        if (seconds <= 0) return;
        idle += seconds;
        double rate = idle > 2 ? 1 : 0.2;
        fatigue = Math.Max(0, fatigue * Math.Exp(-seconds * rate / 28));
    }
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
    /// <summary>The hammer slipped (a tired arm): a glancing blow, off its mark, weak and tilted.</summary>
    public bool slipped;

    /// <summary>How the blow felt, in words.</summary>
    public string Words => slipped ? "The hammer slipped: a glancing blow, off its mark."
        : $"{(force > 0.9 ? "A heavy" : force > 0.5 ? "A firm" : "A light")} blow{(rushed > 0.3 ? ", rushed" : "")}{(miss > 0.03 ? ", off its mark" : "")}.";
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

    /// <summary>A full blow held past <see cref="holdBeforeWaver"/>: the arm wavers, and the blow it lets go weakens.</summary>
    public bool Wavers => Raised && Held > fullCharge + holdBeforeWaver;

    /// <summary>The <see cref="Charge"/> of a full blow of this hammer (its weight's): where a meter of the blow is full.</summary>
    public double FullBlow => 0.8 + 0.2 * weight;

    /// <summary>
    /// How rushed a blow let go now is (0-1): struck sooner after the last one than the rhythm allows, or held up so long
    /// that the arm wavers - either way it lands lighter and less true.
    /// </summary>
    public double Rushed
    {
        get
        {
            double rushed = sinceLast < rhythm ? 1 - sinceLast / rhythm : 0;
            double over = Math.Max(0, Held - fullCharge - holdBeforeWaver);
            return Math.Max(rushed, Math.Min(1, over * 0.5));
        }
    }

    /// <summary>A tired arm cannot lift the hammer as high: the share of the charge it puts into the blow (a heavier hammer tires it more).</summary>
    private double ArmOf(CraftHands hands) => hands != null ? Math.Max(0.2, hands.Strength - 0.1 * (weight - 1)) : 1;

    /// <summary>
    /// The blow as it would land if let go now, before the hands' tremble: the charge, less what a tired arm cannot lift
    /// and what holding on too long (or striking too soon) takes out of it - what a bar over the work shows, so that the
    /// band it shows is the band the blow lands in.
    /// </summary>
    public double Expected(CraftHands hands) => Charge * ArmOf(hands) * (1 - 0.6 * Rushed);

    /// <summary>Strikes at <paramref name="aim"/> (0-1 along the piece) with <paramref name="tilt"/> (-1..1).</summary>
    public CraftBlow Release(double aim, double tilt, CraftHands hands)
    {
        double charge = Charge;
        double rushed = Rushed;
        double shake = hands.Unsteadiness * (0.7 + 0.5 * weight) * (1 + rushed);
        double dAim = (CraftMath.Noise(hands.seed, hands.count, 11) * 2 - 1) * shake * 0.6;
        double dTilt = (CraftMath.Noise(hands.seed, hands.count, 12) * 2 - 1) * shake * 2.5;
        double dForce = (CraftMath.Noise(hands.seed, hands.count, 13) * 2 - 1) * shake * 1.5;
        // A tired arm cannot lift the hammer as high: the blow lands lighter. A heavier hammer tires it more.
        double strength = ArmOf(hands);
        bool slipped = hands.Slips(14, rushed);
        if (slipped)
        {
            // The hammer glances off: it lands off the mark, tilted, with little of its force.
            double side = CraftMath.Noise(hands.seed, hands.count, 15) * 2 - 1;
            dAim += Math.Sign(side == 0 ? 1 : side) * (0.05 + 0.08 * Math.Abs(side));
            dTilt += (CraftMath.Noise(hands.seed, hands.count, 16) < 0.5 ? -1 : 1) * (0.5 + 0.4 * hands.fatigue);
            strength *= 0.35 + 0.25 * CraftMath.Noise(hands.seed, hands.count, 17);
        }
        var blow = new CraftBlow
        {
            position = CraftMath.Clamp01(aim + dAim),
            tilt = CraftMath.Clamp(tilt + dTilt, -1, 1),
            force = CraftMath.Clamp(charge * strength * (1 - 0.6 * rushed) * (1 + dForce), 0.02, 1.2),
            miss = Math.Abs(dAim),
            rushed = rushed,
            slipped = slipped,
        };
        hands.Move(blow.force * weight);
        Raised = false;
        Held = 0;
        sinceLast = 0;
        return blow;
    }
}

/// <summary>
/// One pass along the work (<see cref="CraftStroke.PassRuns"/>): a run in one direction long enough to be a stroke -
/// where it went from and to along x (0-1), its samples and their times, and how the hands held it (the mean angle and
/// its wobble, the mean pressure, and whether it moved into the edge).
/// </summary>
public struct CraftPass
{
    public double from, to;
    /// <summary>The samples it spans (indices into the stroke) and their times (seconds).</summary>
    public int first, last;
    public double start, end;
    public double angle, wobble, pressure;
    /// <summary>It moved into the edge (y rising toward 1) at least as much as away from it.</summary>
    public bool leading;

    public double Length => Math.Abs(to - from);
}

/// <summary>
/// A stroke tracker for the abrasive and leather benches: the player drags across the work and this measures what the
/// hands did - the span covered, the angle held and how much it wobbled, the pressure, how long, how many passes,
/// and whether the edge led. Feed it samples in the work's coordinates (x along 0-1, y across 0-1).
/// <para>The arm tires with the time it works - one movement for every <see cref="TireEvery"/> seconds of the samples'
/// own times - not with how often it is sampled, so a fast screen tires it no faster. A pass is a run along x that
/// turns (x comes back at least <see cref="Turn"/> from the furthest point of the run) after at least the stroke's
/// pass length; a wiggle is no pass, and no more than <see cref="PassesPerSecond"/> passes come from a second of
/// stroking (and one to start with).</para>
/// </summary>
public sealed class CraftStroke
{
    /// <summary>How far x must come back from the furthest point of a run for the run to have turned (0-1 of the work).</summary>
    public const double Turn = 0.01;
    /// <summary>The least run along x that is a pass, by default (0-1 of the work): a stroke, not a wiggle.</summary>
    public const double DefaultPassLength = 0.2;
    /// <summary>The most passes a second of stroking gives (on top of the first): faster than this is scrubbing.</summary>
    public const double PassesPerSecond = 3;
    /// <summary>Seconds of stroking per movement of the arm (it tires by the time it works).</summary>
    public const double TireEvery = 0.1;

    private readonly List<double> xs = new List<double>(), ys = new List<double>(), ts = new List<double>(), angles = new List<double>(), pressures = new List<double>();
    private readonly List<CraftPass> passes = new List<CraftPass>();
    private readonly double passLength;
    // The run under way: the sample it started from, the furthest one so far, its direction (0: not yet clear) - and,
    // while the direction is not yet clear, the lowest and highest samples seen.
    private int runStart, runFar, runDir, lowest, highest;
    // The arm's clock: stroke time not yet turned into a movement, and the last sample's time (both kept across Clear,
    // so work cleared and sent in short batches tires the arm as one long stroke).
    private double tireClock, lastTime = double.NaN;

    /// <param name="passLength">The least run along x (0-1 of the work) that is a pass (a whetstone wants a real stroke).</param>
    public CraftStroke(double passLength = DefaultPassLength)
    {
        this.passLength = Math.Max(Turn, passLength);
    }

    public bool Active => xs.Count > 0;
    public int Samples => xs.Count;
    /// <summary>The least run along x that is a pass on this stroke.</summary>
    public double PassLength => passLength;

    /// <summary>Starts a new stroke (the arm's clock goes on).</summary>
    public void Clear()
    {
        xs.Clear(); ys.Clear(); ts.Clear(); angles.Clear(); pressures.Clear();
        passes.Clear();
        runStart = runFar = runDir = lowest = highest = 0;
    }

    /// <summary>Adds one sample; the hands' tremble is added to the angle (degrees) and the pressure (0-1).</summary>
    public void Add(double time, double x, double y, double angle, double pressure, CraftHands hands)
    {
        double jitter = hands == null ? 0 : hands.Tremble(xs.Count + 21);
        xs.Add(CraftMath.Clamp01(x));
        ys.Add(CraftMath.Clamp01(y));
        ts.Add(time);
        angles.Add(angle + jitter * 40);
        // A tired arm cannot press as hard, however high the hand is held.
        pressures.Add(CraftMath.Clamp01((pressure + jitter * 0.5) * (hands == null ? 1 : 0.55 + 0.45 * hands.Strength)));
        // The arm tires by the time it works: a gap (the hand resting, or a new press) counts no more than one movement's worth.
        if (!double.IsNaN(lastTime) && time > lastTime)
            tireClock += Math.Min(TireEvery, time - lastTime);
        lastTime = time;
        while (tireClock >= TireEvery)
        {
            tireClock -= TireEvery;
            hands?.Move(pressure * 0.5);
        }
        Follow(xs.Count - 1);
    }

    /// <summary>Follows the run under way with sample <paramref name="i"/>: a turn ends the run (a pass, if it was one).</summary>
    private void Follow(int i)
    {
        if (i == 0)
        {
            runStart = runFar = lowest = highest = 0;
            runDir = 0;
            return;
        }
        double x = xs[i];
        if (runDir == 0)
        {
            // Which way it goes is clear once it has moved a turn's worth from the lowest or the highest point.
            if (x < xs[lowest]) lowest = i;
            if (x > xs[highest]) highest = i;
            if (x - xs[lowest] >= Turn) { runDir = 1; runStart = lowest; runFar = i; }
            else if (xs[highest] - x >= Turn) { runDir = -1; runStart = highest; runFar = i; }
            return;
        }
        if (runDir > 0 ? x >= xs[runFar] : x <= xs[runFar])
        {
            runFar = i;
            return;
        }
        if (Math.Abs(xs[runFar] - x) < Turn)
            return;
        // It turned: the run from its start to its furthest point is done.
        if (Counts(runStart, runFar))
            passes.Add(Pass(runStart, runFar));
        runStart = runFar;
        runFar = i;
        runDir = -runDir;
    }

    /// <summary>Is the run from sample <paramref name="a"/> to <paramref name="b"/> a pass: long enough, and not more passes than the stroking's time allows?</summary>
    private bool Counts(int a, int b) =>
        Math.Abs(xs[b] - xs[a]) >= passLength - 1e-9 && passes.Count < 1 + PassesPerSecond * Math.Max(0, ts[b] - ts[0]) + 1e-6;

    private CraftPass Pass(int a, int b)
    {
        double angle = 0, pressure = 0, toward = 0, away = 0;
        int n = b - a + 1;
        for (int k = a; k <= b; k++) { angle += angles[k]; pressure += pressures[k]; }
        angle /= n;
        pressure /= n;
        double s = 0;
        for (int k = a; k <= b; k++) s += (angles[k] - angle) * (angles[k] - angle);
        for (int k = a + 1; k <= b; k++)
        {
            double d = ys[k] - ys[k - 1];
            if (d > 0) toward += d; else away -= d;
        }
        return new CraftPass
        {
            from = xs[a], to = xs[b], first = a, last = b, start = ts[a], end = ts[b],
            angle = angle, wobble = Math.Sqrt(s / n), pressure = pressure, leading = toward >= away,
        };
    }

    /// <summary>
    /// The passes that have turned, in order (each long enough and within the pace of <see cref="PassesPerSecond"/>):
    /// an area sends each one as it comes. The run under way is not among them until it turns (see <see cref="Current"/>).
    /// </summary>
    public IReadOnlyList<CraftPass> PassRuns => passes;

    /// <summary>The run under way, if it is already a pass (letting go now ends it as one).</summary>
    public bool Current(out CraftPass pass)
    {
        pass = default;
        if (runDir == 0 || xs.Count < 2 || !Counts(runStart, runFar))
            return false;
        pass = Pass(runStart, runFar);
        return true;
    }

    /// <summary>
    /// The passes made since the first <paramref name="sent"/> were taken, into <paramref name="into"/> (cleared first) -
    /// each goes to the station as its own request - and, when the stroke ends (<paramref name="ending"/>), the run under
    /// way if it is already a pass. <paramref name="sent"/> moves on past them. A wiggle gives none.
    /// </summary>
    public void TakePasses(ref int sent, bool ending, List<CraftPass> into)
    {
        into.Clear();
        sent = Math.Max(0, sent);
        while (sent < passes.Count)
            into.Add(passes[sent++]);
        if (ending && Current(out CraftPass last))
            into.Add(last);
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

    /// <summary>
    /// How many passes along the work: the runs that turned, and the one under way, that were long enough to be strokes
    /// (see <see cref="PassRuns"/>; 0 for a wiggle or a dab).
    /// </summary>
    public int Passes => passes.Count + (Current(out _) ? 1 : 0);

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

    /// <summary>
    /// The segments covered on <paramref name="p"/> by their real lengths (forging leaves them uneven): the ones drawn,
    /// highlighted and worked under the stroke's x (0-1 of the piece's length). -1, -1 for a piece with no segments.
    /// </summary>
    public void Span(CraftPiece p, out int from, out int to)
    {
        from = to = -1;
        if (p == null || p.segments.Count == 0)
            return;
        from = p.SegmentAt(From);
        to = Math.Max(from, p.SegmentAt(To));
    }

    /// <summary>
    /// The time a tool held to <paramref name="p"/> for <paramref name="window"/> seconds spent on each stretch under the
    /// stroke (<see cref="Span(CraftPiece, out int, out int)"/>, into <paramref name="from"/> and <paramref name="to"/>):
    /// the window spread over them - the wheel is on one place at a time, so moving along the blade shares its time out,
    /// and holding still keeps all of it on one stretch. 0 for a piece with no segments.
    /// </summary>
    public double Dwell(CraftPiece p, double window, out int from, out int to)
    {
        Span(p, out from, out to);
        return from < 0 ? 0 : Math.Max(0, window) / (to - from + 1);
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
/// Pouring: the player tilts the crucible (0 upright .. 1 tipped over). The rate follows the tilt with a lag, and the
/// hands' tremble moves the tilt (an upright crucible never pours by itself); the steadiness is how little the hands
/// moved the tilt while the metal ran; an interruption is the stream stopping short of a full mould and starting again,
/// which leaves a cold shut in the casting (one unbroken stream stopped early only leaves it short). Metal that misses
/// the mould's mouth is <see cref="Spilled"/>; metal still poured once the mould is full first fills the pouring cup
/// (<see cref="Reserve"/>: a prompt tip-back is clean), then runs over it (<see cref="Overflow"/>). The stream stops when
/// the crucible has nothing more to give (<see cref="Supply"/>, in mould-fulls) - that is not an interruption.
/// <para>How far out the stream reaches (<see cref="Reach"/>) follows the tilt as the hand holds it: a stream building up
/// already falls where it will run, one eased back draws in as it slows. Once the hand lets go and the crucible tips back,
/// the dying stream falls from the lip still held over the cup: the window and the 3D pour keep the landing point they
/// had at that moment.</para>
/// </summary>
public sealed class CraftPourControl
{
    /// <summary>The pouring cup and the runner take this much (mould-fulls) past full before the metal runs over the mould.</summary>
    public const double Reserve = 0.05;
    /// <summary>The run-over (<see cref="OverflowShare"/>) above which the casting has fins of flash - the same rule for the words and the workshop.</summary>
    public const double Flash = 0.02;
    /// <summary>The stream flows above this rate.</summary>
    public const double Flowing = 0.04;

    public double Rate { get; private set; }
    public double Poured { get; private set; }
    public int Interruptions { get; private set; }
    /// <summary>Metal that missed the mould (in mould-fulls).</summary>
    public double Spilled { get; private set; }
    /// <summary>Metal poured after the mould and its pouring cup were full (in mould-fulls): it runs over the top.</summary>
    public double Overflow { get; private set; }
    /// <summary>Metal standing in the pouring cup past full (mould-fulls, up to <see cref="Reserve"/>): it is cut off and melted again.</summary>
    public double InCup { get; private set; }
    /// <summary>The metal the crucible holds, in mould-fulls (infinity: as much as is wanted). The stream stops when it is gone.</summary>
    public double Supply { get; set; } = double.PositiveInfinity;
    public bool Started => Poured > 0;
    public bool Full => Poured >= 1;
    /// <summary>The crucible has given all it had.</summary>
    public bool Empty => Given >= Supply - 1e-9;
    /// <summary>Everything that has left the crucible (mould-fulls): into the mould, the cup, onto the sand, over the top.</summary>
    public double Given => Poured + InCup + Spilled + Overflow;
    /// <summary>The rate the tilt asks for now (0-1, before the hands' tremble): the stream follows it with a lag.</summary>
    public double Aim { get; private set; }
    /// <summary>The rate a tilt (0 upright .. 1 tipped over) asks for: nothing until the metal reaches the lip, then more as it tips further.</summary>
    public static double AimOf(double tilt) => CraftMath.Clamp01((tilt - 0.25) / 0.6);
    /// <summary>
    /// How far out the stream reaches from the lip, as a rate (0-1): as far as the tilt throws it while it builds up (the
    /// head of metal at the lip is there at once), then as the stream slows when it is eased back.
    /// </summary>
    public double Reach => Math.Max(Rate, Aim);
    private double sum, time, waver, lastTarget = -1;
    private bool flowing, broke;

    /// <summary>Spilled metal for each mould-full cast (what the workshop's pour takes as lost when it is not told the amount).</summary>
    public double SpillShare => Poured > 1e-6 ? Math.Min(2, Spilled / Poured) : Spilled > 0 ? 2 : 0;

    /// <summary>How much ran over, as the pour request carries it (flash above <see cref="Flash"/>).</summary>
    public double OverflowShare => Math.Min(1, Overflow * 2);

    /// <summary>Everything that missed the mould or ran over it (mould-fulls): lost on the floor.</summary>
    public double Lost => Spilled + Overflow;

    /// <summary>
    /// One frame of pouring; returns the fraction of the mould filled this frame (rate × dt / fill time).
    /// <paramref name="onTarget"/>: the stream lands in the mould's mouth (else it spills on the floor).
    /// </summary>
    public double Tick(double dt, double tilt, CraftHands hands, double fillSeconds = 4, bool onTarget = true)
    {
        // The hands drift slowly (a new waver every quarter second), not frame by frame: they move the tilt, so a crucible
        // held upright never pours by itself.
        double tremble = hands != null ? hands.Tremble((int)((time + Poured) * 4) + 31) * 0.8 : 0;
        double target = Empty ? 0 : AimOf(tilt + tremble);
        Aim = Empty ? 0 : AimOf(tilt);
        if (Empty) Rate = 0; // nothing more to give: the stream stops at once
        else Rate += (target - Rate) * Math.Min(1, dt * 6);
        bool now = Rate > Flowing;
        // A stream that stops short of a full mould leaves the metal in it to set: started again, the new metal meets it
        // half frozen - a cold shut. Stopped for good (tipped back early, or the crucible empty), it is only short.
        if (flowing && !now && Poured > 0.02 && Poured < 1 && !Empty) broke = true;
        if (!flowing && now && broke)
        {
            Interruptions++;
            broke = false;
        }
        flowing = now;
        if (now)
        {
            if (lastTarget >= 0) waver += Math.Abs(target - lastTarget);
            sum += Rate * dt;
            time += dt;
        }
        lastTarget = target;
        double add = now ? Rate * dt / Math.Max(0.5, fillSeconds) : 0;
        if (!double.IsPositiveInfinity(Supply)) add = Math.Min(add, Math.Max(0, Supply - Given));
        if (!onTarget) { Spilled += add; return 0; }
        double room = Math.Max(0, 1 - Poured);
        if (add > room)
        {
            double past = add - room, cup = Math.Min(past, Math.Max(0, Reserve - InCup));
            InCup += cup;
            Overflow += past - cup;
        }
        Poured = Math.Min(1, Poured + add);
        return Math.Min(add, room);
    }

    /// <summary>The mean rate while the metal ran (0-1).</summary>
    public double MeanRate => time > 0 ? sum / time : 0;

    /// <summary>1 a steady stream .. 0 a sputtering one (how much the tilt moved per second of pouring).</summary>
    public double Steadiness => time > 0 ? CraftMath.Clamp01(1 - waver / time * 0.6) : 0;

    /// <summary>
    /// The pour is over: the stream has died away (below <see cref="Flowing"/>) and the crucible is tipping back
    /// (<paramref name="tippingBack"/>: the hand let go), or the mould is full and the crucible upright again, or it had
    /// nothing more to give. A crucible still tipped over a full mould is not done: the metal runs over the top.
    /// </summary>
    public bool Settled(bool tippingBack) => Rate < Flowing && (tippingBack || Empty || Full);

    /// <summary>Seconds a steady stream takes to fill a mould that takes <paramref name="grams"/> of metal: a big mould longer than a row of rivets.</summary>
    public static double FillSeconds(double grams) => CraftMath.Clamp(1.5 + Math.Max(0, grams) / 400, 2, 8);

    /// <summary>
    /// What the pour sends to the station once it is over (<see cref="CraftRequestKind.Pour"/>, or
    /// <see cref="CraftRequestKind.TakeMelt"/> from the furnace): the mean rate and the steadiness, the stream's breaks, what
    /// missed the cup and what ran over, the share of the mould filled (1000 = full) and the metal lost on the floor, in
    /// thousandths of a mould. False when nothing ever left the crucible.
    /// </summary>
    public bool Request(string mold, bool fromFurnace, out CraftRequest request)
    {
        request = default;
        if (!Started && Lost <= 1e-6)
            return false;
        int share = Poured >= 0.9995 ? 1000 : (int)Math.Max(1, Math.Min(999, Math.Round(Poured * 1000)));
        int lost = (int)Math.Max(0, Math.Min(3000, Math.Round(Lost * 1000)));
        request = new CraftRequest(fromFurnace ? CraftRequestKind.TakeMelt : CraftRequestKind.Pour)
        {
            id = mold, a = (float)MeanRate, b = (float)Steadiness, i0 = Interruptions, c = (float)SpillShare, d = (float)OverflowShare, i1 = share, i2 = lost,
        };
        return true;
    }
}

/// <summary>
/// A rhythm for stitching, punching and wrapping: beats come at a steady pace and the player answers each one - once:
/// a second answer to the same beat does not count. The timing error of each answer (how far from the beat, against
/// <see cref="window"/> either side: further off it is a miss) becomes the spacing error (punching), the spread of
/// tension (stitching, wrapping); how hard they pull is the tension.
/// </summary>
public sealed class CraftRhythm
{
    public double interval = 0.7;
    /// <summary>Seconds either side of a beat an answer still counts (further off it is a miss).</summary>
    public double window = 0.25;
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    /// <summary>The last answer's timing error (0 on the beat .. 1 a miss; -1 none yet) and how far off it was (seconds, negative = early).</summary>
    public double LastError { get; private set; } = -1;
    public double LastOffset { get; private set; }
    private double clock, errSum, errSq, pullSum;
    private long answered = long.MinValue;

    public void Tick(double dt) { clock += dt; }

    /// <summary>How far through the current beat (0 just after a beat .. 1 the next beat).</summary>
    public double Phase => clock % interval / interval;

    /// <summary>The beat nearest now (its number) and how far from it now is (seconds, negative = before it).</summary>
    public long Beat => (long)Math.Round(clock / interval);
    public double Offset => clock - Beat * interval;

    /// <summary>The beat nearest now was answered already: another answer to it does not count.</summary>
    public bool Answered => Beat == answered;

    /// <summary>
    /// The player answers the nearest beat with <paramref name="pull"/> (0-1). Returns the timing error (0 perfect .. 1 a
    /// miss), or -1 when that beat was answered already (nothing counts: the hands wait for the next beat).
    /// </summary>
    public double Answer(double pull, CraftHands hands)
    {
        long beat = Beat;
        if (beat == answered)
            return -1;
        answered = beat;
        double off = Offset; // seconds from the nearest beat
        double err = CraftMath.Clamp01(Math.Abs(off) / Math.Max(1e-3, window));
        if (hands != null) err = CraftMath.Clamp01(err + Math.Abs(hands.Tremble(Hits + Misses + 41)) * 2);
        if (err >= 1) Misses++; else Hits++;
        errSum += err;
        errSq += err * err;
        pullSum += CraftMath.Clamp01((pull + (hands?.Tremble(Hits + Misses + 42) ?? 0)) * (hands == null ? 1 : 0.6 + 0.4 * hands.Strength));
        hands?.Move(0.3);
        LastError = err;
        LastOffset = off;
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
        answered = long.MinValue;
        LastError = -1;
        LastOffset = 0;
    }
}

/// <summary>
/// The bellows: each full pump (down then up) adds a puff of air; pumping in a steady rhythm builds a strong draft,
/// frantic pumping only tires the arms.
/// </summary>
public sealed class CraftBellowsPump
{
    /// <summary>Pumps closer together than this (seconds) are frantic: little air, and real effort for the arm.</summary>
    public const double Frantic = 0.25;
    /// <summary>What a pump in a steady rhythm takes out of the arm: light work (a welding heat at 1-2 pumps a second leaves it fresh).</summary>
    public const double SteadyEffort = 0.0008;

    private double lastPump = -9, clock;
    public double Strength { get; private set; }
    /// <summary>
    /// How the last pump kept the rhythm (what it put into <see cref="Strength"/>): 1 a steady one (0.5-1.5 s after the one
    /// before), 0.8 a slow one (the air dies down between), 0.75 a hurried one, 0.35 a frantic one (under
    /// <see cref="Frantic"/>: little air, and real effort).
    /// </summary>
    public double Rhythm { get; private set; } = 1;
    /// <summary>Seconds since the last pump.</summary>
    public double SinceLast => clock - lastPump;

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
        double steady = gap < Frantic ? 0.35 : gap < 0.5 ? 0.75 : gap < 1.5 ? 1 : 0.8;
        Rhythm = steady;
        Strength = Math.Min(1, Strength * 0.6 + 0.45 * steady * (hands == null ? 1 : 0.5 + 0.5 * hands.Strength));
        // A steady rhythm is light work: the arm that strikes still rests while the bar heats. Pumped frantically it is
        // real effort, and tires the arm like any hurried movement.
        if (gap < Frantic) hands?.Move(0.4);
        else hands?.Exert(SteadyEffort);
        return Strength;
    }
}

/// <summary>How a timing-bar hit was judged. Serialized as numbers: add at the END.</summary>
public enum CraftTimingGrade { Miss, Poor, Good, Excellent, Perfect }

/// <summary>One target on a timing bar: where its ideal point is, how wide its zones are (×), the best score it gives.</summary>
[Serializable]
public struct CraftTimingTarget
{
    public float ideal, size, best;

    public CraftTimingTarget(float ideal, float size = 1f, float best = 1f)
    {
        this.ideal = ideal;
        this.size = size;
        this.best = best;
    }
}

/// <summary>A timing-bar hit: its score (0-1), grade, signed offset from the ideal (in Poor-zone widths: -1..1 and past), whether it did damage.</summary>
public struct CraftTimingHit
{
    public float score, offset, position;
    public CraftTimingGrade grade;
    public bool damage;
    public int target;

    public string Words => grade == CraftTimingGrade.Miss ? $"Miss ({score * 100:0}%)" : $"{grade} ({score * 100:0}%)";
}

/// <summary>
/// The timing bar the precision work shares (sawing, faceting, splitting, weaving): an indicator runs along a bar and back,
/// the bar carries one or more targets with nested zones (Perfect, Excellent, Good, Poor), and the player strikes when
/// the indicator is on a target. The score comes from the real distance to the target's ideal point - 100% on it - not
/// from which zone was touched; below the damage threshold the work is harmed. Harder material (difficulty) narrows the
/// zones and speeds the indicator; practice widens them; the hands' tremble moves the hit. Pure and seeded: the same
/// moments give the same hits.
/// </summary>
public sealed class CraftTimingBar
{
    public readonly CraftTimingSettings settings;
    public readonly List<CraftTimingTarget> targets = new List<CraftTimingTarget>();
    /// <summary>The indicator (0-1 along the bar) and its direction (+1 right, -1 left).</summary>
    public float position, direction = 1f;
    /// <summary>1 ordinary; above 1 harder (narrower zones, a faster indicator).</summary>
    public float difficulty = 1f;
    /// <summary>The player's practice (0-1): wider zones.</summary>
    public float skill;
    public int hits;

    public CraftTimingBar(CraftTimingSettings settings)
    {
        this.settings = settings ?? new CraftTimingSettings();
    }

    /// <summary>The half-width of a zone of a target, after difficulty and practice.</summary>
    public float Zone(CraftTimingGrade grade, CraftTimingTarget t)
    {
        float z = grade switch
        {
            CraftTimingGrade.Perfect => settings.perfectZone,
            CraftTimingGrade.Excellent => settings.excellentZone,
            CraftTimingGrade.Good => settings.goodZone,
            _ => settings.poorZone,
        };
        return z * t.size * (float)Math.Pow(Math.Max(0.2, difficulty), -settings.narrowing) * (1f + settings.skillWidening * Math.Max(0f, Math.Min(1f, skill)));
    }

    /// <summary>The indicator moves (and turns back at either end).</summary>
    public void Tick(float dt)
    {
        float v = settings.speed * (0.75f + 0.25f * Math.Max(0.5f, difficulty));
        position += direction * v * dt;
        for (int k = 0; k < 4 && (position < 0f || position > 1f); k++)
        {
            if (position > 1f) { position = 2f - position; direction = -1f; }
            if (position < 0f) { position = -position; direction = 1f; }
        }
    }

    /// <summary>The score of a hit <paramref name="distance"/> from a target's ideal point (falls through each zone's edge score).</summary>
    public float Score(float distance, CraftTimingTarget t)
    {
        float d = Math.Abs(distance);
        float zp = Zone(CraftTimingGrade.Perfect, t), ze = Zone(CraftTimingGrade.Excellent, t), zg = Zone(CraftTimingGrade.Good, t), zo = Zone(CraftTimingGrade.Poor, t);
        float s;
        if (d <= zp) s = Lerp(1f, settings.perfectScore, d / zp);
        else if (d <= ze) s = Lerp(settings.perfectScore, settings.excellentScore, (d - zp) / Math.Max(1e-6f, ze - zp));
        else if (d <= zg) s = Lerp(settings.excellentScore, settings.goodScore, (d - ze) / Math.Max(1e-6f, zg - ze));
        else if (d <= zo) s = Lerp(settings.goodScore, settings.poorScore, (d - zg) / Math.Max(1e-6f, zo - zg));
        else s = Math.Max(0f, settings.poorScore * (1f - (d - zo) / Math.Max(1e-6f, zo)));
        return s * Math.Max(0f, Math.Min(1f, t.best));
    }

    public CraftTimingGrade Grade(float score, float distance, CraftTimingTarget t)
    {
        if (Math.Abs(distance) > Zone(CraftTimingGrade.Poor, t)) return CraftTimingGrade.Miss;
        if (score >= settings.perfectScore * t.best - 1e-4f && Math.Abs(distance) <= Zone(CraftTimingGrade.Perfect, t)) return CraftTimingGrade.Perfect;
        if (score >= settings.excellentScore * t.best - 1e-4f) return CraftTimingGrade.Excellent;
        if (score >= settings.goodScore * t.best - 1e-4f) return CraftTimingGrade.Good;
        return CraftTimingGrade.Poor;
    }

    /// <summary>
    /// The player strikes now: the indicator (moved by the hands' tremble) against the best target. Without targets
    /// every strike is a miss.
    /// </summary>
    public CraftTimingHit Hit(CraftHands hands = null)
    {
        float at = position + (hands != null ? (float)hands.Tremble(9000 + hits) * 0.6f : 0f);
        hits++;
        var best = new CraftTimingHit { grade = CraftTimingGrade.Miss, offset = 2f, position = at, target = -1 };
        for (int i = 0; i < targets.Count; i++)
        {
            CraftTimingTarget t = targets[i];
            float dist = at - t.ideal, score = Score(dist, t);
            if (best.target >= 0 && score <= best.score) continue;
            best = new CraftTimingHit { score = score, grade = Grade(score, dist, t), offset = dist / Math.Max(1e-6f, Zone(CraftTimingGrade.Poor, t)), position = at, target = i };
        }
        best.damage = best.score < settings.damageBelow;
        hands?.Move(0.05);
        return best;
    }

    /// <summary>A new set of targets: a main one (its ideal set by the seed) and, with <paramref name="safe"/>, a wider target that gives less.</summary>
    public void NewTargets(int seed, int round, bool safe = false)
    {
        targets.Clear();
        float main = 0.2f + 0.6f * (float)CraftMath.Noise(seed, round, 77);
        targets.Add(new CraftTimingTarget(main));
        if (safe)
        {
            float other = main < 0.5f ? Math.Min(0.88f, main + 0.4f) : Math.Max(0.12f, main - 0.4f);
            targets.Add(new CraftTimingTarget(other, 1.8f, 0.7f));
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
}
