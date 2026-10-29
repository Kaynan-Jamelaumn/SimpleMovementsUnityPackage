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

    /// <summary>How unsteady the hands are now (a standard-deviation-like scale).</summary>
    public double Unsteadiness => (0.025 + 0.09 * fatigue) * (1 - 0.6 * skill);

    /// <summary>A signed tremble (-1..1 scaled by <see cref="Unsteadiness"/>) for one movement.</summary>
    public double Tremble(int salt)
    {
        double n = CraftMath.Noise(seed, count, salt) * 2 - 1;
        return n * Unsteadiness;
    }

    /// <summary>How much of a fresh arm's strength is left: 1 fresh .. 0.45 spent.</summary>
    public double Strength => 1 - 0.55 * fatigue * fatigue;

    /// <summary>How tired the arm feels, in words (empty while it is fresh).</summary>
    public string Feeling => fatigue < 0.35 ? "" : fatigue < 0.6 ? "Your arm is tiring: the blows land lighter and less true."
        : fatigue < 0.85 ? "Your arm is heavy: weak, wandering blows, and the hammer may slip. Rest a moment."
        : "Your arm is spent: it trembles, the tools slip. Put it down and rest.";

    /// <summary>
    /// Whether this movement slips (seeded, so repeatable): never while the arm is fresh, more and more as it tires,
    /// less with practice, more when rushed.
    /// </summary>
    public bool Slips(int salt, double rushed = 0)
    {
        double chance = Math.Max(0, fatigue - 0.4) * 0.6 * (1 - 0.6 * skill) + CraftMath.Clamp01(rushed) * 0.15 * fatigue;
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
        // A tired arm cannot lift the hammer as high: the blow lands lighter. A heavier hammer tires it more.
        double strength = Math.Max(0.2, hands.Strength - 0.1 * (weight - 1));
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
        // A tired arm cannot press as hard, however high the hand is held.
        pressures.Add(CraftMath.Clamp01((pressure + jitter * 0.5) * (hands == null ? 1 : 0.55 + 0.45 * hands.Strength)));
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
/// starting again, which leaves a cold shut in the casting. Metal that misses the mould's mouth is
/// <see cref="Spilled"/>; metal still poured once the mould is full runs over it (<see cref="Overflow"/>).
/// </summary>
public sealed class CraftPourControl
{
    public double Rate { get; private set; }
    public double Poured { get; private set; }
    public int Interruptions { get; private set; }
    /// <summary>Metal that missed the mould (in mould-fulls).</summary>
    public double Spilled { get; private set; }
    /// <summary>Metal poured after the mould was full (in mould-fulls): it runs over the top.</summary>
    public double Overflow { get; private set; }
    public bool Started => Poured > 0;
    public bool Full => Poured >= 1;
    private double sum, time, waver, lastTarget = -1;
    private bool flowing;

    /// <summary>Spilled metal for each mould-full cast (what the workshop's pour takes as lost).</summary>
    public double SpillShare => Poured > 0.05 ? Math.Min(2, Spilled / Poured) : 0;

    /// <summary>
    /// One frame of pouring; returns the fraction of the mould filled this frame (rate × dt / fill time).
    /// <paramref name="onTarget"/>: the stream lands in the mould's mouth (else it spills on the floor).
    /// </summary>
    public double Tick(double dt, double tilt, CraftHands hands, double fillSeconds = 4, bool onTarget = true)
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
        if (!onTarget) { Spilled += add; return 0; }
        double room = Math.Max(0, 1 - Poured);
        if (add > room) Overflow += add - room;
        Poured = Math.Min(1, Poured + add);
        return Math.Min(add, room);
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
        pullSum += CraftMath.Clamp01((pull + (hands?.Tremble(Hits + Misses + 42) ?? 0)) * (hands == null ? 1 : 0.6 + 0.4 * hands.Strength));
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
        Strength = Math.Min(1, Strength * 0.6 + 0.45 * steady * (hands == null ? 1 : 0.5 + 0.5 * hands.Strength));
        hands?.Move(0.4);
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
