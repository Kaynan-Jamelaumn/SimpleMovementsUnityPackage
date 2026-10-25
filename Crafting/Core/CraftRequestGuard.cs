using System;
using System.Collections.Generic;

/// <summary>
/// What a server lets a remote player's requests claim, so a modified client cannot work faster than hands can or
/// with steadier hands than the player has. The minigames run on the player's machine and send what the hands did (a
/// blow's force and tilt, seconds on the wheel, strokes on the stone, the steadiness of a pour); the server keeps one
/// guard per player at a station and checks every request against real time and the player's own practice:
/// <list type="bullet">
/// <item><b>Allowances</b> refill in real time: blows of the hammer, pumps of the bellows, seconds of grinding, filing,
/// brushing and polishing, seconds of rinsing, strokes on the whetstone, stitches and punches, samples of a drawn path,
/// pours, cuts. A request claiming more gets what is left (grinding 60 s in one request grinds for the seconds that
/// really passed); one with nothing left is refused.</item>
/// <item><b>The hammer</b> must be raised to strike hard: a blow's force is held to what the time since the last one
/// could charge (with room for network jitter).</item>
/// <item><b>The hands</b> are never steadier than the player's practice allows (<see cref="Floor"/>): the wobble of a
/// whetstone stroke, the steadiness of a pour, the timing of stitches. Blows whose tilt never trembles (a client that
/// strips the tremble) are noticed over a dozen strikes; from then on the server adds the hands' tremble itself.</item>
/// <item><b>Shape</b>: ids and paths have a size limit and every number must be finite.</item>
/// </list>
/// Honest clients never meet these limits: their minigames already measure real hands in real time.
/// </summary>
public sealed class CraftRequestGuard
{
    public const int MaxIdLength = 64;
    public const int MaxPathFloats = 1024;
    public const int MaxCount = 50;
    /// <summary>Seconds a request may arrive early because of network jitter.</summary>
    public const double Jitter = 0.3;
    /// <summary>Strikes looked at to notice a tremble that is too small to be real.</summary>
    public const int TiltWindow = 12;

    /// <summary>A refilling allowance: <see cref="rate"/> per second, up to <see cref="cap"/>.</summary>
    public struct Allowance
    {
        public double rate, cap, level, last;

        public Allowance(double rate, double cap)
        {
            this.rate = rate;
            this.cap = cap;
            level = cap;
            last = double.NaN;
        }

        private void Refill(double now)
        {
            if (!double.IsNaN(last) && now > last)
                level = Math.Min(cap, level + (now - last) * rate);
            last = double.IsNaN(last) ? now : Math.Max(last, now);
        }

        /// <summary>Takes up to <paramref name="want"/>; returns what was granted.</summary>
        public double Take(double want, double now)
        {
            Refill(now);
            double got = Math.Max(0, Math.Min(want, level));
            level -= got;
            return got;
        }

        /// <summary>Takes exactly <paramref name="want"/> or nothing.</summary>
        public bool TakeWhole(double want, double now)
        {
            Refill(now);
            if (level + 1e-9 < want)
                return false;
            level -= want;
            return true;
        }
    }

    private Allowance requests = new Allowance(20, 30);
    private Allowance blows = new Allowance(1 / 0.4, 2);
    private Allowance pumps = new Allowance(5, 4);
    private Allowance work = new Allowance(1.25, 3);
    private Allowance washing = new Allowance(2.5, 30);
    private Allowance strokes = new Allowance(3, 8);
    private Allowance beats = new Allowance(2, 16);
    private Allowance path = new Allowance(150, 600);
    private Allowance pours = new Allowance(0.5, 1);
    private Allowance cuts = new Allowance(0.75, 2);
    private double lastStrike = double.NegativeInfinity;
    private readonly Queue<double> tilts = new Queue<double>();
    private double tiltSum;

    /// <summary>The server's model of the player's hands: its skill comes from the player's journal on the server.</summary>
    public CraftHands Hands { get; }

    /// <summary>The client's blows were too steady to be real: the server now adds the hands' tremble to them.</summary>
    public bool AddsTremble { get; private set; }

    public CraftRequestGuard(int seed, double skill)
    {
        Hands = new CraftHands(seed, skill);
    }

    /// <summary>The player's practice (0 novice .. 1 master).</summary>
    public double Skill
    {
        get => Hands.skill;
        set => Hands.skill = CraftMath.Clamp01(value);
    }

    /// <summary>The steadiest the player's hands can be: fresh, with their practice (see <see cref="CraftHands.Unsteadiness"/>).</summary>
    public double Floor => Math.Max(0.004, 0.025 * (1 - 0.6 * Hands.skill));

    /// <summary>The strongest blow a hammer raised for <paramref name="seconds"/> can strike (see <see cref="CraftHammer.Charge"/>).</summary>
    public static double MaxForce(double seconds)
    {
        if (double.IsInfinity(seconds) || seconds > 10) return 1.2;
        double c = Math.Min(1, Math.Max(0, seconds) / 0.9);
        c = 1 - (1 - c) * (1 - c);
        return Math.Min(1.2, c * 1.15 + 0.05);
    }

    /// <summary>
    /// Checks <paramref name="r"/> at <paramref name="now"/> (seconds, real time) and bounds what it claims. False (with
    /// a reason, empty when it is not worth telling) = refused.
    /// </summary>
    public bool Check(ref CraftRequest r, double now, out string reason)
    {
        reason = "";
        if (!WellFormed(r))
            return false;
        if (!requests.TakeWhole(1, now))
        {
            reason = "Slow down.";
            return false;
        }
        switch (r.kind)
        {
            case CraftRequestKind.Strike:
            {
                if (!blows.TakeWhole(1, now))
                {
                    reason = "Let the hammer fall before the next blow.";
                    return false;
                }
                double since = now - lastStrike + Jitter;
                lastStrike = now;
                r.b = (float)Math.Min(r.b, MaxForce(since));
                NoteTilt(r.c);
                if (AddsTremble)
                {
                    double shake = Hands.Unsteadiness * 1.2;
                    r.a = (float)CraftMath.Clamp01(r.a + (CraftMath.Noise(Hands.seed, Hands.count, 11) * 2 - 1) * shake * 0.6);
                    r.c = (float)CraftMath.Clamp(r.c + (CraftMath.Noise(Hands.seed, Hands.count, 12) * 2 - 1) * shake * 2.5, -1, 1);
                }
                Hands.Move(r.b);
                return true;
            }
            case CraftRequestKind.Crush:
            {
                if (!blows.TakeWhole(1, now))
                {
                    reason = "Let the hammer fall before the next blow.";
                    return false;
                }
                double since = now - lastStrike + Jitter;
                lastStrike = now;
                r.a = (float)Math.Min(r.a, 1.5 * MaxForce(since));
                return true;
            }
            case CraftRequestKind.Bellows:
                return pumps.TakeWhole(1, now);
            case CraftRequestKind.Grind:
            case CraftRequestKind.File:
                return Spend(ref work, ref r.c, now);
            case CraftRequestKind.Descale:
            case CraftRequestKind.Polish:
                return Spend(ref work, ref r.b, now);
            case CraftRequestKind.Wash:
                return Spend(ref washing, ref r.b, now);
            case CraftRequestKind.Sharpen:
            {
                int n = (int)Math.Floor(strokes.Take(Math.Max(1, r.count), now) + 1e-9);
                if (n < 1)
                    return false;
                r.count = n;
                r.c = (float)Math.Max(r.c, 11.5 * Floor); // a stroke's wobble: never steadier than these hands
                return true;
            }
            case CraftRequestKind.JointWork:
            {
                int n = (int)Math.Floor(beats.Take(Math.Max(1, r.count), now) + 1e-9);
                if (n < 1)
                    return false;
                r.count = n;
                var work = (CraftJointWork)r.i0;
                if (work == CraftJointWork.Punch || work == CraftJointWork.Stitch || work == CraftJointWork.Wrap)
                {
                    if (work == CraftJointWork.Punch) r.a = (float)Math.Max(r.a, 0.5 * Floor); // spacing error
                    r.b = (float)Math.Max(r.b, 0.4 * Floor); // spread
                }
                else
                    r.a = (float)Math.Min(r.a, 4 * MaxForce(99)); // a hammer's force × 4
                return true;
            }
            case CraftRequestKind.Scrape:
            case CraftRequestKind.Stake:
            case CraftRequestKind.Slick:
            case CraftRequestKind.Skive:
            case CraftRequestKind.Dye:
            case CraftRequestKind.Condition:
            {
                int pairs = r.points != null ? r.points.Count / 2 : 0;
                int n = (int)Math.Floor(path.Take(pairs, now) + 1e-9);
                if (n < 2)
                    return false;
                if (n < pairs)
                    r.points = r.points.GetRange(0, n * 2);
                return true;
            }
            case CraftRequestKind.Pour:
            case CraftRequestKind.TakeMelt:
                if (!pours.TakeWhole(1, now))
                {
                    reason = "The metal is still running.";
                    return false;
                }
                r.b = (float)Math.Min(r.b, 1 - 1.6 * Floor);
                return true;
            case CraftRequestKind.Cut:
                if (!cuts.TakeWhole(1, now))
                    return false;
                r.a = (float)Math.Min(r.a, 0.995);
                return true;
            case CraftRequestKind.PutItem:
            case CraftRequestKind.AddFuel:
            case CraftRequestKind.Flux:
            case CraftRequestKind.FillVat:
            case CraftRequestKind.Attach:
                r.count = Math.Max(0, Math.Min(MaxCount, r.count));
                return true;
            default:
                return true;
        }
    }

    private static bool Spend(ref Allowance a, ref float seconds, double now)
    {
        if (!(seconds > 0))
            return false;
        double got = a.Take(seconds, now);
        if (got < 0.02)
            return false;
        seconds = (float)got;
        return true;
    }

    /// <summary>Blows whose tilt never strays: honest hands always tremble by about 1.5 × <see cref="Floor"/> on average.</summary>
    private void NoteTilt(float tilt)
    {
        double t = Math.Abs(tilt);
        tilts.Enqueue(t);
        tiltSum += t;
        if (tilts.Count > TiltWindow)
            tiltSum -= tilts.Dequeue();
        if (!AddsTremble && tilts.Count >= TiltWindow && tiltSum / tilts.Count < 0.3 * Floor)
            AddsTremble = true;
    }

    /// <summary>Ids short enough, every number finite, a path of pairs within its limit.</summary>
    public static bool WellFormed(in CraftRequest r)
    {
        if (!Enum.IsDefined(typeof(CraftRequestKind), r.kind) || !Enum.IsDefined(typeof(CraftLocation), r.location))
            return false;
        if (TooLong(r.piece) || TooLong(r.other) || TooLong(r.id) || TooLong(r.itemId))
            return false;
        if (!Finite(r.a) || !Finite(r.b) || !Finite(r.c) || !Finite(r.d))
            return false;
        if (r.count < 0 || r.count > 100000)
            return false;
        if (r.points != null)
        {
            if (r.points.Count > MaxPathFloats || r.points.Count % 2 != 0)
                return false;
            foreach (float f in r.points)
                if (!Finite(f) || Math.Abs(f) > 4096)
                    return false;
        }
        return true;
    }

    private static bool TooLong(string s) => s != null && s.Length > MaxIdLength;
    private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
