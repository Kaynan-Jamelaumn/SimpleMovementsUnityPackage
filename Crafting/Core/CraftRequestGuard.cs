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
/// could charge (with room for network jitter), and to what the arm has left: the server tires and rests its own
/// model of the arm with every blow, so a client cannot keep striking full blows with a spent arm.</item>
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
    /// <summary>Blows it takes at the least to set a forge weld along a whole seam.</summary>
    public const int MinWeldBlows = 6;

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
    private Allowance setting = new Allowance(2.5, 14);
    private Allowance spinning = new Allowance(0.25, 1);
    private double lastStrike = double.NegativeInfinity, lastSeen = double.NaN;
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

    /// <summary>The share of a full blow the arm has left (lenient: the client's own arm tires on more than blows).</summary>
    public double ArmLeft => Math.Min(1, Hands.Strength + 0.15);

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
        if (!double.IsNaN(lastSeen) && now > lastSeen)
            Hands.Rest(now - lastSeen);
        lastSeen = double.IsNaN(lastSeen) ? now : Math.Max(lastSeen, now);
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
                r.b = (float)Math.Min(r.b, MaxForce(since) * ArmLeft);
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
                r.a = (float)Math.Min(r.a, 1.5 * MaxForce(since) * ArmLeft);
                Hands.Move(r.a / 1.5);
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
                r.i1 = r.i1 == 1 || r.i1 == 2 ? r.i1 : 0; // the needle, or the pricking iron and awl: the station counts their work
                var work = (CraftJointWork)r.i0;
                if (work == CraftJointWork.Punch || work == CraftJointWork.Stitch || work == CraftJointWork.Wrap)
                {
                    if (work == CraftJointWork.Punch) r.a = (float)Math.Max(r.a, 0.5 * Floor); // spacing error
                    r.b = (float)Math.Max(r.b, 0.4 * Floor); // spread
                }
                else if (work == CraftJointWork.Solder)
                {
                    r.a = (float)CraftMath.Clamp(r.a, 0, 1.6); // the peak heat, as a share of the solder's flow
                    r.b = (float)CraftMath.Clamp01(r.b);
                }
                else if (work == CraftJointWork.Push)
                {
                    r.a = (float)CraftMath.Clamp01(r.a);
                    r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); // never pushed more evenly than these hands
                }
                else
                    r.a = (float)Math.Min(r.a, 4 * MaxForce(99) * ArmLeft); // a hammer's force × 4
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
                r.c = (float)CraftMath.Clamp(r.c, 0, 2);
                r.d = (float)CraftMath.Clamp01(r.d);
                r.i1 = Math.Max(0, Math.Min(1000, r.i1));
                return true;
            case CraftRequestKind.Cut:
                if (!cuts.TakeWhole(1, now))
                    return false;
                r.a = (float)Math.Min(r.a, 0.995);
                return true;
            case CraftRequestKind.Fold:
            case CraftRequestKind.Weld:
            {
                // The weld is set with hammer blows: a set it claims is worth only the blows that real time allowed.
                int claimed = Math.Max(1, Math.Min(MaxCount, r.count));
                double granted = setting.Take(claimed, now);
                r.b = (float)(CraftMath.Clamp01(r.b > 0 ? r.b : 1) * Math.Min(1, granted / claimed) * Math.Min(1, granted / MinWeldBlows));
                if (r.b <= 0) r.b = 0.001f;
                return true;
            }
            case CraftRequestKind.Sieve:
                r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            case CraftRequestKind.Rive:
            case CraftRequestKind.SawCut:
            case CraftRequestKind.BendWood:
                // A split, a cut, a bend: from the cuts allowance; never straighter, squarer or steadier than these hands.
                if (!cuts.TakeWhole(1, now))
                    return false;
                if (r.kind == CraftRequestKind.Rive) { r.a = (float)Math.Max(CraftMath.Clamp01(r.a), 2 * Floor); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); }
                else if (r.kind == CraftRequestKind.SawCut) { r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor); r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 4 * Floor); }
                else { r.a = (float)CraftMath.Clamp(r.a, 0, 180); r.b = (float)CraftMath.Clamp01(r.b); }
                return true;
            case CraftRequestKind.StackWood:
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)CraftMath.Clamp01(r.b); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.SandWood:
                if (!Spend(ref work, ref r.b, now))
                    return false;
                r.a = (float)CraftMath.Clamp01(r.a); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.Varnish:
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 3 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            case CraftRequestKind.Spin:
                if (!spinning.TakeWhole(1, now))
                {
                    reason = "The wheel is still turning out the last hank.";
                    return false;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 6 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            case CraftRequestKind.BrushOre:
                // Seconds of brushing (from the washing allowance); a brush covers at most about two-fifths of a stone a second.
                if (!Spend(ref washing, ref r.b, now))
                    return false;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), r.b * 0.4);
                return true;
            case CraftRequestKind.Carve:
                // A knife stroke: from the strokes allowance; never deeper than a knife bites, never steadier than these hands.
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a = (float)CraftMath.Clamp(r.a, 0, 3);
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                return true;
            // ---- the woodshop's logs
            case CraftRequestKind.DebarkWood:
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.SplitLog:
                if (!cuts.TakeWhole(1, now))
                    return false;
                r.a = (float)Math.Max(CraftMath.Clamp01(r.a), 2 * Floor); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), ArmLeft); r.i0 = Math.Max(1, Math.Min(30, r.i0));
                Hands.Move(r.c);
                return true;
            // ---- fibre
            case CraftRequestKind.CleanFibre:
            case CraftRequestKind.OpenFibre:
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor);
                return true;
            case CraftRequestKind.ScourFibre:
                if (!cuts.TakeWhole(1, now))
                    return false;
                r.a = (float)CraftMath.Clamp(r.a, 0, 100); r.b = (float)CraftMath.Clamp(r.b, 0, 240); r.c = (float)CraftMath.Clamp01(r.c); r.d = (float)CraftMath.Clamp(r.d, 0, 100);
                r.i0 = Math.Max(0, Math.Min(9, r.i0));
                return true;
            case CraftRequestKind.CardFibre:
            case CraftRequestKind.CombFibre:
            {
                int n = (int)Math.Floor(strokes.Take(Math.Max(1, r.count), now) + 1e-9);
                if (n < 1)
                    return false;
                r.count = n;
                if (r.kind == CraftRequestKind.CardFibre) { r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); }
                else r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                return true;
            }
            case CraftRequestKind.SpinFibre:
            case CraftRequestKind.PlyYarn:
                if (!spinning.TakeWhole(1, now))
                {
                    reason = "The wheel is still turning out the last bobbin.";
                    return false;
                }
                if (r.kind == CraftRequestKind.SpinFibre) { r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 6 * Floor); r.b = (float)CraftMath.Clamp01(r.b); r.c = (float)CraftMath.Clamp01(r.c); }
                else { r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); }
                return true;
            case CraftRequestKind.MeasureYarn:
            case CraftRequestKind.ReelYarn:
                return strokes.Take(1, now) >= 1 - 1e-9;
            case CraftRequestKind.MeasureFit:
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                return true;
            // ---- the lapidary
            case CraftRequestKind.TestGem:
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor); r.b = (float)CraftMath.Clamp(r.b, 1, 10);
                return true;
            case CraftRequestKind.EvaluateGem:
            {
                int floats = r.points != null ? r.points.Count : 0;
                int n = (int)Math.Floor(path.Take(floats / 2, now) + 1e-9);
                if (n < 1)
                    return false;
                if (n * 2 < floats)
                    r.points = r.points.GetRange(0, n * 2);
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                return true;
            }
            case CraftRequestKind.CleaveGem:
            {
                if (!blows.TakeWhole(1, now))
                {
                    reason = "Let the mallet come up before the next blow.";
                    return false;
                }
                // Where the chisel sits: never truer than these hands (a degree for a novice, less for a master).
                r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 21) * 2 - 1) * Floor * 40);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), MaxForce(now - lastStrike + Jitter) * ArmLeft);
                r.i0 = r.i0 == 1 ? 1 : 0; // along the plane, or nicked and snapped
                lastStrike = now;
                Hands.Move(r.c * 0.5);
                return true;
            }
            case CraftRequestKind.SawGem:
                if (!cuts.TakeWhole(1, now))
                    return false;
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 4 * Floor); r.d = (float)CraftMath.Clamp01(r.d); r.i0 = Math.Max(0, Math.Min(1000, r.i0));
                r.i1 = Math.Max(0, Math.Min(50, r.i1)); // the blade's misses
                return true;
            case CraftRequestKind.Routine:
                // done by routine: the station works out how well from the player's practice; one at a time
                if (!work.TakeWhole(1, now))
                {
                    reason = "One thing at a time.";
                    return false;
                }
                return Enum.IsDefined(typeof(CraftRoutine), r.i0);
            case CraftRequestKind.CleanGem:
            {
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                // scrubbing takes time: no more work round the stone than the hands could do
                double total = 0;
                if (r.points != null)
                    for (int k = 0; k < r.points.Count; k++) { r.points[k] = (float)CraftMath.Clamp(r.points[k], 0, 3); total += r.points[k]; }
                if (total > 0)
                {
                    double got = washing.Take(total, now);
                    if (got < total)
                        for (int k = 0; k < r.points.Count; k++) r.points[k] *= (float)(got / total);
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                r.i0 = r.i0 == 1 ? 1 : 0;
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.GrindGem:
            {
                int floats = r.points != null ? r.points.Count : 0;
                int n = (int)Math.Floor(path.Take(floats / 2, now) + 1e-9);
                if (n < 1)
                    return false;
                if (n * 2 < floats)
                    r.points = r.points.GetRange(0, n * 2);
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                Hands.Move(0.05 * n / 20.0);
                return true;
            }
            case CraftRequestKind.WeaveCloth:
            {
                int rows = r.points != null ? Math.Min(r.points.Count, Math.Max(0, r.i1)) : 0;
                // every row is a beat of the reed; a beat is never truer than these hands
                int n = (int)Math.Floor(beats.Take(Math.Max(1, rows), now) + 1e-9);
                if (n < Math.Max(1, rows))
                {
                    reason = "Each row takes a throw of the shuttle and a beat of the reed.";
                    return false;
                }
                for (int k = 0; k < rows; k++) r.points[k] = (float)Math.Min(CraftMath.Clamp01(r.points[k]), 1 - 4 * Floor);
                r.i1 = rows;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.FacetGem:
            {
                // Every facet takes time on the lap; the quill is never set truer than these hands set it.
                int n = (int)Math.Floor(beats.Take(Math.Max(1, r.count), now) + 1e-9);
                if (n < Math.Max(1, r.count))
                {
                    reason = "Each facet takes time on the lap.";
                    return false;
                }
                r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 22) * 2 - 1) * Floor * 30);
                r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor);
                r.c = (float)CraftMath.Clamp(r.c, -1, 1);
                if (Math.Abs(r.c) < 2 * Floor) r.c = (float)(r.c < 0 ? -2 * Floor : 2 * Floor);
                r.d = (float)CraftMath.Clamp01(r.d);
                // the botched facets, and how unevenly they came (never perfectly alike from real hands)
                r.i1 = Math.Max(0, Math.Min(Math.Max(1, r.count), r.i1));
                r.i2 = Math.Max((int)Math.Ceiling(2 * Floor * 1000), Math.Min(1000, r.i2));
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.PolishGem:
            {
                int n = (int)Math.Floor(strokes.Take(Math.Max(1, r.count), now) + 1e-9);
                if (n < 1)
                    return false;
                r.count = n;
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)CraftMath.Clamp01(r.b);
                if (r.points != null)
                    for (int k = 0; k < r.points.Count; k++) r.points[k] = (float)CraftMath.Clamp(r.points[k], 0, 3);
                return true;
            }
            // ---- the jeweller
            case CraftRequestKind.FormShank:
                if (!beats.TakeWhole(1, now))
                    return false;
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); r.c = (float)Math.Max(CraftMath.Clamp(r.c, 0, 5), Floor * 2);
                return true;
            case CraftRequestKind.SeatSetting:
                if (strokes.Take(1, now) < 1 - 1e-9)
                    return false;
                r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 23) * 2 - 1) * Floor * 0.8); // a burr is never truer than these hands
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 3 * Floor);
                return true;
            case CraftRequestKind.FinishJewel:
                if (!Spend(ref work, ref r.d, now) && r.d > 0)
                    return false;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 3 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            case CraftRequestKind.PutItem:
            case CraftRequestKind.AddFuel:
            case CraftRequestKind.Flux:
            case CraftRequestKind.FillVat:
            case CraftRequestKind.Attach:
                r.count = Math.Max(0, Math.Min(MaxCount, r.count));
                if (r.kind == CraftRequestKind.Flux) r.a = (float)CraftMath.Clamp01(r.a);
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
