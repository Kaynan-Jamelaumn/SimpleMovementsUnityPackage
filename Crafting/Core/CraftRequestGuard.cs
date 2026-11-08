using System;
using System.Collections.Generic;

/// <summary>
/// What a player's requests may claim, so nobody works faster than hands can or with steadier hands than they have. The
/// minigames run on the player's machine and send what the hands did (a blow's force and tilt, seconds on the wheel,
/// strokes on the stone, the steadiness of a pour); the station keeps one guard per player and checks every request
/// against real time and the player's own practice - the same for a player offline, the host and a network client (a
/// test station, whose quick buttons send whole bundles, checks nobody's hands: only how fast a player on another
/// machine sends, <see cref="Paced"/>):
/// <list type="bullet">
/// <item><b>Allowances</b> refill in real time, one for each kind of work: blows of the hammer, pumps of the bellows,
/// seconds of grinding, filing, brushing and polishing (times the stretches they work), seconds of rinsing, passes on
/// the whetstone, card strokes, stitches and pushes, beats of the reed, hits on the faceting bar, strokes of the paper,
/// samples of a drawn path, pours, cuts, a fleece's minutes in the scouring bath. Each is sized to the largest single
/// request honest hands send (a whole weave, a tier of facets, a run of stitches), so honest work never meets them. Work
/// that comes in a bundle and is all or nothing (a weave, a tier of facets) is granted whole or refused, and a refusal
/// uses up nothing; work that adds up (seconds, strokes) gets what is left, with a note saying so. Work the guard let
/// through that the station then refuses (no yarn for that row, a mould already full) is given back
/// (<see cref="Refund"/>), so the retry is not held back either.</item>
/// <item><b>The hammer</b> must be raised to strike hard: a blow's force (a rivet's, a peen's, a wedge's too) is held to
/// what the time since the last one could charge (with room for network jitter), and to what the arm has left: the
/// guard tires and rests its own model of the arm with every blow, so nobody keeps striking full blows with a spent
/// arm.</item>
/// <item><b>The hands</b> are never steadier than the player's practice allows (<see cref="Floor"/>): the wobble of a
/// whetstone stroke, the steadiness of a pour, the timing of stitches. The guard only clamps: it adds no tremble to
/// what honest hands did. Blows whose tilt never trembles (a client that strips the tremble) are noticed over a dozen
/// strikes; from then on the guard adds the hands' tremble itself, to blows and to the lapidary's lines and settings.</item>
/// <item><b>Shape</b>: ids and lists have a size limit and every number must be finite; each kind checks its own list
/// (x,y pairs for a drawn path, one value a segment, a sector or a row).</item>
/// </list>
/// Every refusal says why, in a few words of the workshop ("Too fast: the hammer has not come up yet."), and so does
/// every request granted only in part.
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
    /// <summary>Most rows a loom weaves in one go (the beats one weave carries).</summary>
    public const int MaxRows = 30;
    /// <summary>Most facets a tier has (the workshop takes no more).</summary>
    public const int MaxFacets = 96;
    /// <summary>Most values a clay coat carries, one a segment (the workshop takes no more).</summary>
    public const int MaxClayValues = 512;
    /// <summary>How much of a stone a brush can go over in a second of brisk scrubbing (the window's and the 3D brush stay under it).</summary>
    public const double BrushCoverPerSecond = 1.5;
    /// <summary>Minutes of soak the scouring bath grants beyond the real time since the hands came (room for the network's lag).</summary>
    public const double SoakSlack = 5;

    /// <summary>What a request the station could not read is answered.</summary>
    public const string Garbled = "The station could not make sense of that.";
    /// <summary>What a request carrying more than one request can is answered.</summary>
    public const string TooMuch = "That was too much to take in at once.";
    /// <summary>What a request beyond the pace of requests (<see cref="Paced"/>) is answered.</summary>
    public const string SlowDown = "Slow down.";

    /// <summary>A refilling allowance: <see cref="rate"/> per second, up to <see cref="cap"/>.</summary>
    public struct Allowance
    {
        public double rate, cap, level, last;

        public Allowance(double rate, double cap) : this(rate, cap, cap) { }

        /// <summary>An allowance that starts at <paramref name="level"/> (0: it fills from the guard's first request on).</summary>
        public Allowance(double rate, double cap, double level)
        {
            this.rate = rate;
            this.cap = cap;
            this.level = Math.Max(0, Math.Min(cap, level));
            last = double.NaN;
        }

        private void Refill(double now)
        {
            if (!double.IsNaN(last) && now > last)
                level = Math.Min(cap, level + (now - last) * rate);
            last = double.IsNaN(last) ? now : Math.Max(last, now);
        }

        /// <summary>Starts (or keeps) the allowance's clock at <paramref name="now"/>, taking nothing.</summary>
        public void Touch(double now) => Refill(now);

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

        /// <summary>Takes as many whole units of <paramref name="each"/> as there are, up to <paramref name="want"/>; returns how many.</summary>
        public int TakeUnits(int want, double each, double now)
        {
            Refill(now);
            if (want <= 0 || !(each > 0))
                return 0;
            int n = (int)Math.Max(0, Math.Min(want, Math.Floor(level / each + 1e-9)));
            level = Math.Max(0, level - n * each);
            return n;
        }
    }

    private Allowance requests = new Allowance(20, 30);
    private Allowance blows = new Allowance(1 / 0.4, 2);
    private Allowance pumps = new Allowance(5, 4);
    private Allowance work = new Allowance(1.25, 3);
    private Allowance washing = new Allowance(2.5, 30);
    private Allowance scrubbing = new Allowance(6, 12);
    private Allowance strokes = new Allowance(4, 8);
    private Allowance whetstone = new Allowance(4, 16);
    private Allowance cards = new Allowance(4, 8);
    private Allowance beats = new Allowance(2.5, 16);
    private Allowance loom = new Allowance(2, MaxRows + 2, 0);
    private Allowance facets = new Allowance(3, 6);
    private Allowance sanding = new Allowance(6, 15);
    private Allowance path = new Allowance(400, 800);
    private Allowance pours = new Allowance(0.5, 1);
    private Allowance cuts = new Allowance(0.75, 2);
    private Allowance setting = new Allowance(2.5, 14);
    private Allowance spinning = new Allowance(0.25, 1);
    /// <summary>Minutes of a fleece's soak in the scouring bath: a minute a second at the bath (<see cref="CraftFibre.SoakMinutesPerSecond"/>), filling from the window's opening (with a few minutes' room for the network's lag).</summary>
    private Allowance soaking = new Allowance(CraftFibre.SoakMinutesPerSecond, CraftFibre.MaxSoakMinutes, SoakSlack);
    private Allowance routines = new Allowance(1, 2);
    /// <summary>The jeweller's buffing wheel, in full buffs: as fast as the best tools buff (<see cref="CraftJewelry.BuffPerSecond"/>), two pieces' worth at most.</summary>
    private Allowance buffing = new Allowance(CraftJewelry.BuffPerSecond * 1.2, 2);
    private double lastStrike = double.NegativeInfinity, lastSeen = double.NaN;
    private readonly Queue<double> tilts = new Queue<double>();
    private double tiltSum;

    /// <summary>
    /// What the allowances of the work (every one but the pace of requests) and the hammer's rise held before the request
    /// the guard last let through: <see cref="Refund"/> puts it back. An allowance added above is added here too.
    /// </summary>
    private struct Before
    {
        public Allowance blows, pumps, work, washing, scrubbing, strokes, whetstone, cards, beats, loom, facets, sanding, path, pours, cuts, setting, spinning, routines, soaking;
        public Allowance buffing;
        public double lastStrike;
    }
    private Before before;
    private bool refundable;

    /// <summary>The guard's model of the player's hands: its skill comes from the player's journal where the rules run.</summary>
    public CraftHands Hands { get; }

    /// <summary>The player's blows were too steady to be real: the guard now adds the hands' tremble to them (and to the lapidary's lines and settings).</summary>
    public bool AddsTremble { get; private set; }

    /// <summary>The rules' mastery settings (how many hits a tier of facets takes at each rank); null = the defaults.</summary>
    public CraftMasterySettings Settings { get; set; }

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
    public double Floor => FloorOf(Hands.skill);

    /// <summary>
    /// The steadiest hands of <paramref name="skill"/> practice can be (a window that shows what the hands will do
    /// applies the same floor, so that what it shows is what the station does).
    /// </summary>
    public static double FloorOf(double skill) => Math.Max(0.004, 0.025 * (1 - 0.6 * CraftMath.Clamp01(skill)));

    /// <summary>The cleanest a cut (the leather knife's, the shears') can be for hands of this <paramref name="floor"/>: a novice's 0.90, a master's 0.96.</summary>
    public static double CutCap(double floor) => 1 - 4 * floor;

    /// <summary>
    /// The most thorough shaking of the sieve hands of this <paramref name="floor"/> can give (a novice's 0.90, a master's
    /// 0.96): the window rings the stones that fall through with that (<see cref="CraftWorkshop.SieveReach"/>), so what it
    /// shows falling is what does.
    /// </summary>
    public static double SieveCap(double floor) => 1 - 4 * floor;

    /// <summary>
    /// The truest hands of this <paramref name="floor"/> beat a row in with the reed, tension a warp or lay the tape round
    /// a wearer (a novice's 0.90, a master's 0.96): the loom's and the tape's words say what the station keeps.
    /// </summary>
    public static double TrueCap(double floor) => 1 - 4 * floor;

    /// <summary>The share of a full blow the arm has left (lenient: the player's own arm tires on more than blows).</summary>
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
    /// The player is at the station from <paramref name="now"/> (the window opened): the allowances that fill from
    /// nothing (the loom's, a soak's) start filling, so the work done before the first request counts.
    /// </summary>
    public void Touch(double now)
    {
        loom.Touch(now);
        soaking.Touch(now);
    }

    /// <summary>
    /// Checks <paramref name="r"/> at <paramref name="now"/> (seconds, real time) and bounds what it claims. False = refused,
    /// with the reason; true with a reason = granted only in part (the reason says what counted). <paramref name="ws"/>,
    /// the workshop it is sent to, tells an item nothing has gone into yet - which may be put aside anywhere - from the
    /// rest, which only a test station throws away (<see cref="TestStationOnly(in CraftRequest, CraftWorkshop)"/>).
    /// </summary>
    public bool Check(ref CraftRequest r, double now, out string reason, CraftWorkshop ws = null)
    {
        refundable = false;
        Fit(ref r, out string note);
        if (!WellFormed(r, out reason))
            return false;
        if (!Paced(now, out reason))
            return false;
        if (!double.IsNaN(lastSeen) && now > lastSeen)
            Hands.Rest(now - lastSeen);
        lastSeen = double.IsNaN(lastSeen) ? now : Math.Max(lastSeen, now);
        Touch(now);
        Keep();
        if (!Bound(ref r, now, ws, out reason))
            return false;
        refundable = true;
        if (reason.Length == 0)
            reason = note;
        return true;
    }

    /// <summary>
    /// The pace of requests alone - no more than twenty a second, thirty at once, every request counting whether it is
    /// granted or not: all a test station (whose quick buttons send whole bundles of work) asks of a player on another
    /// machine, so that nobody floods the server. False = <see cref="SlowDown"/>.
    /// </summary>
    public bool Paced(double now, out string reason)
    {
        reason = "";
        if (requests.TakeWhole(1, now))
            return true;
        reason = SlowDown;
        return false;
    }

    /// <summary>
    /// The station refused what the guard last let through (the workshop said no: no yarn for that row, a mould already
    /// full, a tier it would not cut): what that request took from the allowances, and the hammer's rise, are given back,
    /// so the honest retry is not held back by work that was never done. The request itself still counts in the pace of
    /// requests, and the arm stays as tired as it got - the hands did move. Only once, and only right after a
    /// <see cref="Check"/> that granted; anything else does nothing.
    /// </summary>
    public void Refund()
    {
        if (!refundable)
            return;
        refundable = false;
        Before b = before;
        blows = b.blows; pumps = b.pumps; work = b.work; washing = b.washing; scrubbing = b.scrubbing; strokes = b.strokes;
        whetstone = b.whetstone; cards = b.cards; beats = b.beats; loom = b.loom; facets = b.facets; sanding = b.sanding;
        path = b.path; pours = b.pours; cuts = b.cuts; setting = b.setting; spinning = b.spinning; routines = b.routines;
        soaking = b.soaking; buffing = b.buffing;
        lastStrike = b.lastStrike;
    }

    /// <summary>Keeps what the allowances of the work and the hammer's rise hold now, before a request takes from them (for <see cref="Refund"/>).</summary>
    private void Keep() => before = new Before
    {
        blows = blows, pumps = pumps, work = work, washing = washing, scrubbing = scrubbing, strokes = strokes,
        whetstone = whetstone, cards = cards, beats = beats, loom = loom, facets = facets, sanding = sanding,
        path = path, pours = pours, cuts = cuts, setting = setting, spinning = spinning, routines = routines,
        soaking = soaking, buffing = buffing,
        lastStrike = lastStrike,
    };

    /// <summary>The kind's own rules: its allowance, its list, its numbers.</summary>
    private bool Bound(ref CraftRequest r, double now, CraftWorkshop ws, out string reason)
    {
        reason = "";
        switch (r.kind)
        {
            case CraftRequestKind.Strike:
            {
                if (!blows.TakeWhole(1, now))
                {
                    reason = "Too fast: the hammer has not come up yet.";
                    return false;
                }
                double since = now - lastStrike + Jitter;
                lastStrike = now;
                r.b = (float)Math.Max(0, Math.Min(r.b, MaxForce(since) * ArmLeft));
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
                    reason = "Too fast: the hammer has not come up yet.";
                    return false;
                }
                double since = now - lastStrike + Jitter;
                lastStrike = now;
                r.a = (float)Math.Max(0, Math.Min(r.a, 1.5 * MaxForce(since) * ArmLeft));
                Hands.Move(r.a / 1.5);
                return true;
            }
            case CraftRequestKind.Bellows:
                if (!pumps.TakeWhole(1, now))
                {
                    reason = "Too fast: the bellows have not filled again.";
                    return false;
                }
                r.a = (float)CraftMath.Clamp01(r.a);
                return true;
            case CraftRequestKind.Oven:
                r.a = (float)CraftOvenState.Dial(r.a); // the dial's range: off, or a tempering heat
                return true;
            // Seconds on every stretch worked: the tool is on one place at a time, so the allowance pays for the seconds
            // times the stretches (a request over the whole blade costs what it works).
            case CraftRequestKind.Grind:
                return SpendOver(ref work, ref r.c, Stretches(r, ws), now, "the wheel", out reason);
            case CraftRequestKind.File:
                return SpendOver(ref work, ref r.c, Stretches(r, ws), now, "the file", out reason);
            case CraftRequestKind.Descale:
                return SpendOver(ref work, ref r.b, Stretches(r, ws), now, "the brush", out reason);
            case CraftRequestKind.Polish:
                return SpendOver(ref work, ref r.b, Stretches(r, ws), now, "the buff", out reason);
            case CraftRequestKind.Wash:
                // seconds of swirling; the swirl never steadier than these hands
                if (!Spend(ref washing, ref r.b, now, "the rinse", out reason))
                    return false;
                r.a = (float)CraftMath.Clamp01(r.a);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 4 * Floor);
                return true;
            case CraftRequestKind.Sharpen:
            {
                int claimed = Math.Max(1, Math.Min(MaxCount, r.count));
                int n = whetstone.TakeUnits(claimed, 1, now);
                if (n < 1)
                {
                    reason = "Too fast: the stone takes one stroke at a time.";
                    return false;
                }
                if (n < claimed)
                    reason = PartOf(n, claimed, "strokes");
                r.count = n;
                r.c = (float)Math.Max(r.c, 11.5 * Floor); // a stroke's wobble: never steadier than these hands
                return true;
            }
            case CraftRequestKind.JointWork:
                return JointWork(ref r, now, out reason);
            case CraftRequestKind.Scrape:
            case CraftRequestKind.Stake:
            case CraftRequestKind.Slick:
            case CraftRequestKind.Skive:
            case CraftRequestKind.Dye:
            case CraftRequestKind.Condition:
            {
                // a stroke over the hide: x,y pairs, never harder than the hand presses
                if (r.points == null || r.points.Count % 2 != 0)
                {
                    reason = Garbled;
                    return false;
                }
                int pairs = r.points.Count / 2;
                if (pairs < 2)
                {
                    reason = "Too short a stroke to work the hide.";
                    return false;
                }
                if (!path.TakeWhole(2, now))
                {
                    reason = "Too many strokes too fast: give the arm a moment.";
                    return false;
                }
                int n = 2 + path.TakeUnits(pairs - 2, 1, now);
                if (n < pairs)
                {
                    r.points = r.points.GetRange(0, n * 2);
                    reason = "Too fast: only the first part of that stroke counted.";
                }
                r.a = (float)CraftMath.Clamp01(r.a);
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
                r.i0 = Math.Max(0, Math.Min(MaxCount, r.i0)); // the stream's breaks
                r.i1 = Math.Max(0, Math.Min(1000, r.i1));
                r.i2 = Math.Max(0, Math.Min(3000, r.i2)); // metal on the floor, thousandths of the mould
                return true;
            case CraftRequestKind.Cut:
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one cut at a time.";
                    return false;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), CutCap(Floor)); // a knife or the shears follow the line no truer than these hands
                return true;
            case CraftRequestKind.StirVat:
                r.a = (float)CraftMath.Clamp01(r.a); // the paddle's steadiness
                return true;
            case CraftRequestKind.Fold:
            case CraftRequestKind.Weld:
            {
                // By hand (flag) the fold or the stack is laid and its seam left open: the blows that set it come as strikes,
                // each under the hammer's own bound. One at a time.
                if (r.flag)
                {
                    if (!cuts.TakeWhole(1, now))
                    {
                        reason = r.kind == CraftRequestKind.Fold ? "Too fast: one fold at a time." : "Too fast: one weld at a time.";
                        return false;
                    }
                    return true;
                }
                // At once (a quick bench): the weld is set with hammer blows, so a set it claims is worth only the blows that real time allowed.
                int claimed = Math.Max(1, Math.Min(MaxCount, r.count));
                double granted = setting.Take(claimed, now);
                r.b = (float)(CraftMath.Clamp01(r.b > 0 ? r.b : 1) * Math.Min(1, granted / claimed) * Math.Min(1, granted / MinWeldBlows));
                if (r.b <= 0) r.b = 0.001f;
                if (granted < claimed - 1e-6)
                    reason = "Too fast: the weld took only some of those blows.";
                return true;
            }
            case CraftRequestKind.Sieve:
                // a shaking never more thorough than these hands (a mesh the sieve has)
                r.a = (float)CraftMath.Clamp(r.a, 0.1, 10);
                r.b = (float)Math.Max(0.01, Math.Min(CraftMath.Clamp01(r.b), SieveCap(Floor))); // 0 would read as an old, unmeasured sieving
                return true;
            case CraftRequestKind.Rive:
            case CraftRequestKind.SawCut:
            case CraftRequestKind.BendWood:
                // A split, a cut, a bend: from the cuts allowance; never straighter, squarer or steadier than these hands.
                if (!cuts.TakeWhole(1, now))
                {
                    reason = r.kind == CraftRequestKind.Rive ? "Too fast: one split at a time." : r.kind == CraftRequestKind.BendWood ? "Too fast: one bend at a time." : "Too fast: one cut at a time.";
                    return false;
                }
                if (r.kind == CraftRequestKind.Rive)
                {
                    r.a = (float)Math.Max(CraftMath.Clamp01(r.a), 2 * Floor); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                    r.c = r.c > 0 ? (float)CraftMath.Clamp(r.c, 0.05, 0.95) : 0.5f; // where across the billet the froe was set
                }
                else if (r.kind == CraftRequestKind.SawCut) { r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor); r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 4 * Floor); }
                else
                {
                    // a bend: never steadier than these hands sweep it, over a form the woodshop has (the medium one if none is named)
                    r.a = (float)CraftMath.Clamp(r.a, 0, 180); r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 4 * Floor);
                    r.c = r.c > 0 ? (float)CraftMath.Clamp(r.c, 40, 800) : 120f;
                }
                return true;
            case CraftRequestKind.Split:
                // A bar cut through with the hot chisel (a saw through wood) in one go: one cut at a time, from the cuts
                // allowance. Where it is cut is where the hands marked it - kept as sent (a saw cuts wood exactly there).
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one cut at a time.";
                    return false;
                }
                r.a = (float)CraftMath.Clamp01(r.a);
                return true;
            case CraftRequestKind.StackWood:
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)CraftMath.Clamp01(r.b); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.RipCut:
                // a taper ripped along a line: one cut from the cuts allowance, never truer to its line than these hands
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one cut at a time.";
                    return false;
                }
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)CraftMath.Clamp01(r.b);
                r.c = (float)CraftMath.Clamp(r.c, 3, 2000); r.d = (float)CraftMath.Clamp(r.d, 3, 2000);
                r.i0 = Math.Max((int)Math.Ceiling(2000 * Floor), Math.Min(1000, r.i0));
                return true;
            case CraftRequestKind.SandWood:
                // Strokes of the paper, in the window's measure (a pass of the block is one and a half).
                if (!Spend(ref sanding, ref r.b, now, "the paper", out reason))
                    return false;
                r.a = (float)CraftMath.Clamp01(r.a); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.Varnish:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one stroke of the brush at a time.";
                    return false;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 3 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            case CraftRequestKind.Spin:
                // a bobbin spun from a batch laid from the bag: one bobbin's work, like SpinFibre
                if (!spinning.TakeWhole(1, now))
                {
                    reason = "The wheel is still turning out the last bobbin.";
                    return false;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 6 * Floor); r.b = (float)CraftMath.Clamp01(r.b); r.c = (float)CraftMath.Clamp01(r.c);
                return true;
            case CraftRequestKind.BrushOre:
                // Seconds of brushing (from the washing allowance); a brush goes over at most a stone and a half a second.
                if (!Spend(ref washing, ref r.b, now, "the brush", out reason))
                    return false;
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), r.b * BrushCoverPerSecond);
                return true;
            case CraftRequestKind.Carve:
                // A knife stroke: from the strokes allowance; never deeper than a knife bites, never steadier than these hands.
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one stroke of the knife at a time.";
                    return false;
                }
                r.a = (float)CraftMath.Clamp(r.a, 0, 3);
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                return true;
            // ---- the woodshop's logs
            case CraftRequestKind.DebarkWood:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one pull of the drawknife at a time.";
                    return false;
                }
                // one pull takes one face's share of the way round, no more
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), CraftWoodwork.DebarkPerStroke + 0.02);
                return true;
            case CraftRequestKind.SplitLog:
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one split at a time.";
                    return false;
                }
                r.a = (float)Math.Max(CraftMath.Clamp01(r.a), 2 * Floor); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), ArmLeft); r.i0 = Math.Max(1, Math.Min(30, r.i0));
                Hands.Move(r.c);
                return true;
            // ---- fibre
            case CraftRequestKind.CleanFibre:
            case CraftRequestKind.OpenFibre:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = r.kind == CraftRequestKind.CleanFibre ? "Too fast: one pick at a time." : "Too fast: one teasing at a time.";
                    return false;
                }
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor);
                return true;
            case CraftRequestKind.ScourFibre:
            {
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one bath at a time.";
                    return false;
                }
                // the soak: no longer than the fleece can have lain in the bath since the hands came (a minute a second)
                double claimed = CraftMath.Clamp(r.b, 0, CraftFibre.MaxSoakMinutes), soaked = soaking.Take(claimed, now);
                if (soaked < claimed - 0.5)
                    reason = $"Too fast: it soaked {soaked:0} of those {claimed:0} minutes.";
                r.a = (float)CraftMath.Clamp(r.a, 0, 100); r.b = (float)soaked; r.c = (float)CraftMath.Clamp01(r.c); r.d = (float)CraftMath.Clamp(r.d, 0, 100);
                r.i0 = Math.Max(0, Math.Min(9, r.i0));
                return true;
            }
            case CraftRequestKind.CardFibre:
            case CraftRequestKind.CombFibre:
            {
                int claimed = Math.Max(1, Math.Min(MaxCount, r.count));
                int n = cards.TakeUnits(claimed, 1, now);
                if (n < 1)
                {
                    reason = r.kind == CraftRequestKind.CardFibre ? "Too fast: the cards take one stroke at a time." : "Too fast: the comb takes one pass at a time.";
                    return false;
                }
                if (n < claimed)
                    reason = PartOf(n, claimed, r.kind == CraftRequestKind.CardFibre ? "strokes" : "passes");
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
                if (r.kind == CraftRequestKind.SpinFibre)
                {
                    r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 6 * Floor); r.b = (float)CraftMath.Clamp01(r.b); r.c = (float)CraftMath.Clamp01(r.c);
                    r.i0 = Math.Max(1, Math.Min(CraftFibre.BobbinGrams, r.i0)); // grams: one bobbin of work spins one bobbin
                }
                else { r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); }
                return true;
            case CraftRequestKind.MeasureYarn:
            case CraftRequestKind.ReelYarn:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one skein at a time.";
                    return false;
                }
                return true;
            case CraftRequestKind.JoinYarn:
                // winding one bobbin onto another: no hand skill in it, one at a time
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one bobbin at a time.";
                    return false;
                }
                return true;
            case CraftRequestKind.MeasureFit:
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), TrueCap(Floor));
                return true;
            // ---- the lapidary
            case CraftRequestKind.TestGem:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one test at a time.";
                    return false;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor); r.b = (float)CraftMath.Clamp(r.b, 1, 10);
                return true;
            case CraftRequestKind.EvaluateGem:
            {
                // the loupe's looks: x, y and the focus each
                int looks = r.points != null ? r.points.Count / 3 : 0;
                if (looks < 1)
                {
                    reason = "Look through the loupe first.";
                    return false;
                }
                int n = path.TakeUnits(looks, 1, now);
                if (n < 1)
                {
                    reason = "Too fast: the eye needs time to look.";
                    return false;
                }
                if (n < looks)
                    reason = "Too fast: only the first of those looks counted.";
                if (r.points.Count != n * 3)
                    r.points = r.points.GetRange(0, n * 3);
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                return true;
            }
            case CraftRequestKind.CleaveGem:
            {
                if (!blows.TakeWhole(1, now))
                {
                    reason = "Too fast: the mallet has not come up yet.";
                    return false;
                }
                // Where the chisel sits is where the hands put it (their tremble is the window's); a client that strips the tremble gets it back.
                if (AddsTremble)
                    r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 21) * 2 - 1) * Floor * 40);
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), MaxForce(now - lastStrike + Jitter) * ArmLeft);
                r.i0 = r.i0 == 1 ? 1 : 0; // along the plane, or nicked and snapped
                lastStrike = now;
                Hands.Move(r.c * 0.5);
                return true;
            }
            case CraftRequestKind.SawGem:
                if (!cuts.TakeWhole(1, now))
                {
                    reason = "Too fast: one cut at a time.";
                    return false;
                }
                r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 4 * Floor); r.d = (float)CraftMath.Clamp01(r.d); r.i0 = Math.Max(0, Math.Min(1000, r.i0));
                r.i1 = Math.Max(0, Math.Min(50, r.i1)); // the blade's misses
                return true;
            case CraftRequestKind.Routine:
                // done by routine: the station works out how well from the player's practice; one at a time
                if (!Enum.IsDefined(typeof(CraftRoutine), r.i0))
                {
                    reason = "There is no such work.";
                    return false;
                }
                if (!routines.TakeWhole(1, now))
                {
                    reason = "One thing at a time.";
                    return false;
                }
                return true;
            case CraftRequestKind.InspectItem:
                // an item from the bag tried at the bench: flexed, struck, its straps pulled - one at a time
                if (!routines.TakeWhole(1, now))
                {
                    reason = "One thing at a time.";
                    return false;
                }
                return true;
            case CraftRequestKind.CleanGem:
            {
                // one value a sector round the stone; no more scrubbing than the hands could do
                if (r.points != null && r.points.Count > CraftLapidary.CrustSectors)
                {
                    reason = Garbled;
                    return false;
                }
                double total = 0;
                if (r.points != null)
                    for (int k = 0; k < r.points.Count; k++) { r.points[k] = (float)CraftMath.Clamp(r.points[k], 0, 3); total += r.points[k]; }
                if (total > 0)
                {
                    double got = scrubbing.Take(total, now);
                    if (got < Math.Min(total, 0.02) - 1e-9) // a little scrubbing the allowance covers is granted
                    {
                        reason = "Too fast: the brush has not caught up.";
                        return false;
                    }
                    if (got < total - 1e-6)
                    {
                        for (int k = 0; k < r.points.Count; k++) r.points[k] *= (float)(got / total);
                        reason = "Too fast: the brush did only part of that.";
                    }
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 4 * Floor);
                r.i0 = r.i0 == 1 ? 1 : 0;
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.GrindGem:
            {
                // (angle, amount) pairs: the stone's touches on the wheel
                if (r.points == null || r.points.Count % 2 != 0)
                {
                    reason = Garbled;
                    return false;
                }
                int pairs = r.points.Count / 2;
                if (pairs < 1)
                {
                    reason = "Touch it to the wheel.";
                    return false;
                }
                int n = path.TakeUnits(pairs, 1, now);
                if (n < 1)
                {
                    reason = "Too fast: the wheel has not caught up.";
                    return false;
                }
                if (n < pairs)
                {
                    r.points = r.points.GetRange(0, n * 2);
                    reason = "Too fast: the wheel did only part of that.";
                }
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor);
                Hands.Move(0.05 * n / 20.0);
                return true;
            }
            case CraftRequestKind.WeaveCloth:
            {
                // one value a beat of the reed: as many as the rows claimed, every one a throw of the shuttle and a beat
                int given = r.points != null ? r.points.Count : 0;
                if (r.i1 < 1 || given < r.i1)
                {
                    reason = "Beat the rows in first.";
                    return false;
                }
                if (r.i1 > MaxRows)
                {
                    reason = $"A loom takes {MaxRows} rows at the most in one go.";
                    return false;
                }
                if (!loom.TakeWhole(r.i1, now))
                {
                    reason = "Too fast: each row takes a throw of the shuttle and a beat of the reed.";
                    return false;
                }
                if (given > r.i1)
                    r.points = r.points.GetRange(0, r.i1);
                // a beat is never truer than these hands
                for (int k = 0; k < r.points.Count; k++) r.points[k] = (float)Math.Min(CraftMath.Clamp01(r.points[k]), TrueCap(Floor));
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), TrueCap(Floor)); r.b = (float)CraftMath.Clamp01(r.b);
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.FacetGem:
            {
                // A tier is cut with a few hits on the bar (the rest of its facets follow the same setting): those hits take
                // time - as many as the tier's own facets need, whatever count the request claims (the workshop cuts that many).
                int tierFacets = ws?.TierFacets(r.piece, r.i0) ?? 0;
                r.count = tierFacets > 0 ? tierFacets : Math.Max(1, Math.Min(MaxFacets, r.count));
                int hits = CraftMastery.FacetHits(r.count, Hands.skill, Settings);
                if (!facets.TakeWhole(hits, now))
                {
                    reason = "Too fast: each facet takes time on the lap.";
                    return false;
                }
                // The quill is set where the hands set it; a client that strips the tremble gets it back.
                if (AddsTremble)
                    r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 22) * 2 - 1) * Floor * 30);
                r.b = (float)Math.Max(CraftMath.Clamp01(r.b), 2 * Floor);
                r.c = (float)CraftMath.Clamp(r.c, -1, 1);
                if (Math.Abs(r.c) < 2 * Floor) r.c = (float)(r.c < 0 ? -2 * Floor : 2 * Floor);
                r.d = (float)CraftMath.Clamp01(r.d);
                // the botched facets, and how unevenly they came (never perfectly alike from real hands)
                r.i1 = Math.Max(0, Math.Min(r.count, r.i1));
                r.i2 = Math.Max((int)Math.Ceiling(2 * Floor * 1000), Math.Min(1000, r.i2));
                Hands.Move(0.1);
                return true;
            }
            case CraftRequestKind.PolishGem:
            {
                // passes on the lap; one value a region of the stone (or none)
                if (r.points != null && r.points.Count > CraftLapidary.PolishRegions)
                {
                    reason = Garbled;
                    return false;
                }
                int claimed = Math.Max(1, Math.Min(30, r.count));
                int n = strokes.TakeUnits(claimed, 1, now);
                if (n < 1)
                {
                    reason = "Too fast: the lap takes one pass at a time.";
                    return false;
                }
                if (n < claimed)
                    reason = PartOf(n, claimed, "passes");
                r.count = n;
                r.a = (float)CraftMath.Clamp01(r.a); r.b = (float)CraftMath.Clamp01(r.b);
                if (r.points != null)
                    for (int k = 0; k < r.points.Count; k++) r.points[k] = (float)CraftMath.Clamp(r.points[k], 0, 3);
                return true;
            }
            // ---- the jeweller
            case CraftRequestKind.FormShank:
                if (!beats.TakeWhole(1, now))
                {
                    reason = "Too fast: one shaping at a time.";
                    return false;
                }
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); r.c = (float)Math.Max(CraftMath.Clamp(r.c, 0, 5), Floor * 2);
                return true;
            case CraftRequestKind.SeatSetting:
                if (!strokes.TakeWhole(1, now))
                {
                    reason = "Too fast: one cut of the burr at a time.";
                    return false;
                }
                // The burr is as true as the hands held it; a client that strips the tremble gets it back.
                if (AddsTremble)
                    r.a += (float)((CraftMath.Noise(Hands.seed, Hands.count, 23) * 2 - 1) * Floor * 0.8);
                r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); r.c = (float)Math.Min(CraftMath.Clamp01(r.c), 1 - 3 * Floor);
                return true;
            case CraftRequestKind.FinishJewel:
            {
                // the buff claimed (0-1) is held to what time on the wheel can give: a full buff takes the best tools
                // about seven seconds; one already given is not given again until the wheel has had that time
                double want = CraftMath.Clamp01(r.a);
                if (want > 0)
                {
                    double got = buffing.Take(want, now);
                    if (got < want - 1e-6)
                        reason = "Too fast: the wheel has not buffed it that bright yet.";
                    r.a = (float)got;
                }
                r.a = (float)Math.Min(CraftMath.Clamp01(r.a), 1 - 3 * Floor); r.b = (float)CraftMath.Clamp01(r.b);
                return true;
            }
            case CraftRequestKind.Clay:
                // one thickness a segment (or none: an even coat)
                if (r.points != null && r.points.Count > MaxClayValues)
                {
                    reason = TooMuch;
                    return false;
                }
                return true;
            case CraftRequestKind.PutItem:
            case CraftRequestKind.AddFuel:
            case CraftRequestKind.Flux:
            case CraftRequestKind.FillVat:
            case CraftRequestKind.Attach:
                r.count = Math.Max(0, Math.Min(MaxCount, r.count));
                if (r.kind == CraftRequestKind.Flux)
                {
                    r.a = (float)CraftMath.Clamp01(r.a);
                    // flux sprinkled on a bar: how well each stretch was covered (one value a segment, like a clay coat)
                    if (r.points != null && r.points.Count > MaxClayValues)
                    {
                        reason = TooMuch;
                        return false;
                    }
                    if (r.points != null)
                        for (int k = 0; k < r.points.Count; k++) r.points[k] = (float)CraftMath.Clamp01(r.points[k]);
                }
                return true;
            case CraftRequestKind.Suspend:
            case CraftRequestKind.Resume:
            case CraftRequestKind.ScrapPiece:
                reason = TestStationOnly(r, ws);
                if (reason != null)
                    return false;
                // an item nothing has gone into yet, put aside: one at a time
                if (!routines.TakeWhole(1, now))
                {
                    reason = "One thing at a time.";
                    return false;
                }
                reason = "";
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Joint work: a hammer's (rivets, peening, wedges) is a blow like any other; the rest - stitches, holes, wraps,
    /// pushes, a solder - comes from the bench's allowance, the needle's pierce and pull half a stitch each.
    /// </summary>
    private bool JointWork(ref CraftRequest r, double now, out string reason)
    {
        reason = "";
        var w = (CraftJointWork)r.i0;
        r.count = Math.Max(1, Math.Min(MaxCount, r.count));
        if (w == CraftJointWork.Rivet || w == CraftJointWork.Peen || w == CraftJointWork.Wedge)
        {
            if (!blows.TakeWhole(1, now))
            {
                reason = "Too fast: the hammer has not come up yet.";
                return false;
            }
            double since = now - lastStrike + Jitter;
            lastStrike = now;
            double most = 4 * MaxForce(since) * ArmLeft; // a hammer's force × 4
            double force = CraftMath.Clamp(r.a, 0, most);
            if (AddsTremble)
                force = CraftMath.Clamp(force * (1 + (CraftMath.Noise(Hands.seed, Hands.count, 13) * 2 - 1) * Hands.Unsteadiness * 0.6), 0, most);
            r.a = (float)force;
            r.count = 1; // one blow at a time
            r.i1 = 0;
            Hands.Move(force / 4);
            return true;
        }
        // the needle (1), or the pricking iron and awl (2): the station counts their work from the player's practice
        r.i1 = r.i1 == 1 || r.i1 == 2 ? r.i1 : 0;
        bool needle = r.i1 == 1 && (w == CraftJointWork.Punch || w == CraftJointWork.Stitch);
        // the needle's pull that pierces the cloth as it draws the thread (flag): one request for the whole pull
        bool pierces = needle && w == CraftJointWork.Stitch && r.flag;
        r.flag = pierces;
        int claimed = r.count;
        // a pull of the needle pierces and draws the thread: each half of it is half a stitch's time (both at once: a whole one)
        int n = beats.TakeUnits(claimed, pierces ? 1 : needle ? 0.5 : 1, now);
        string what = needle ? "pulls" : w == CraftJointWork.Push ? "pushes" : w == CraftJointWork.Wrap ? "turns" : w == CraftJointWork.Solder ? "heats" : r.i1 == 2 ? "beats" : "stitches";
        if (n < 1)
        {
            reason = w == CraftJointWork.Push ? "Too fast: one push at a time." : w == CraftJointWork.Solder ? "Too fast: let the joint cool first." : "Too fast: each stitch takes its time.";
            return false;
        }
        if (n < claimed)
            reason = PartOf(n, claimed, what);
        // the pulls past the cut (a stitch's i2), of those granted
        r.i2 = w == CraftJointWork.Stitch ? (int)Math.Round(Math.Max(0, Math.Min(r.i2, claimed)) * (double)n / claimed) : 0;
        r.count = n;
        if (w == CraftJointWork.Punch || w == CraftJointWork.Stitch || w == CraftJointWork.Wrap)
        {
            if (w == CraftJointWork.Punch) r.a = (float)Math.Max(r.a, 0.5 * Floor); // spacing error
            r.b = (float)Math.Max(r.b, 0.4 * Floor); // spread
            if (pierces)
            {
                // the holes the pull pierces: spaced and lined up no truer than these hands, as a punch's
                r.c = (float)Math.Max(r.c, 0.5 * Floor);
                r.d = (float)Math.Max(r.d, 0.4 * Floor);
            }
        }
        else if (w == CraftJointWork.Solder)
        {
            r.a = (float)CraftMath.Clamp(r.a, 0, 1.6); // the peak heat, as a share of the solder's flow
            r.b = (float)CraftMath.Clamp01(r.b);
        }
        else if (w == CraftJointWork.Push)
        {
            r.a = (float)CraftMath.Clamp01(r.a);
            r.b = (float)Math.Min(CraftMath.Clamp01(r.b), 1 - 4 * Floor); // never pushed more evenly than these hands
        }
        return true;
    }

    /// <summary>
    /// Seconds (or strokes) of work from an allowance: what is left of it, with a note when that was not all. Refused
    /// when nothing was claimed, or when the allowance fell short with next to nothing left (under 0.02) - a claim that
    /// small itself (a touch of the wheel, a frame of it as the press ends) is granted while the allowance covers it.
    /// </summary>
    private static bool Spend(ref Allowance a, ref float amount, double now, string tool, out string reason)
    {
        reason = "";
        if (!(amount > 0))
        {
            reason = $"Too short: {tool} did nothing.";
            return false;
        }
        double want = amount;
        double got = a.Take(want, now);
        if (got < Math.Min(want, 0.02) - 1e-9)
        {
            reason = $"Too fast: {tool} has not caught up.";
            return false;
        }
        if (got < want - 1e-6)
            reason = $"Too fast: {tool} did only part of that.";
        amount = (float)got;
        return true;
    }

    /// <summary>
    /// <see cref="Spend"/> for <paramref name="seconds"/> on each of <paramref name="stretches"/> stretches: the allowance
    /// pays for the seconds times the stretches, and what it grants is spread back over them.
    /// </summary>
    private static bool SpendOver(ref Allowance a, ref float seconds, int stretches, double now, string tool, out string reason)
    {
        int n = Math.Max(1, stretches);
        float total = seconds * n;
        if (!Spend(ref a, ref total, now, tool, out reason))
            return false;
        seconds = total / n;
        return true;
    }

    /// <summary>
    /// How many stretches a request on segments <c>i0</c>..<c>i1</c> works: those its piece has there, as the workshop
    /// takes the range (1 at the least). Without the workshop at hand, the request's own range, up to
    /// <see cref="MaxClayValues"/>.
    /// </summary>
    private static int Stretches(in CraftRequest r, CraftWorkshop ws)
    {
        CraftPiece p = ws?.Piece(r.piece);
        int count = p != null ? p.segments.Count : MaxClayValues;
        int from = Math.Max(0, r.i0), to = Math.Min(count - 1, r.i1);
        return Math.Max(1, to - from + 1);
    }

    /// <summary>"Too fast: 4 of those 6 strokes counted."</summary>
    private static string PartOf(int granted, int claimed, string what) => $"Too fast: {granted} of those {claimed} {what} counted.";

    /// <summary>Why a station refuses what only a test station does (stopping its clock, throwing a piece away); null for anything else.</summary>
    public static string TestStationOnly(CraftRequestKind kind)
    {
        switch (kind)
        {
            case CraftRequestKind.Suspend:
            case CraftRequestKind.Resume:
                return "Time passes as it will: only a test station's clock stops.";
            case CraftRequestKind.ScrapPiece:
                return "Only a test station throws pieces away: take it with you.";
            default:
                return null;
        }
    }

    /// <summary>
    /// <see cref="TestStationOnly(CraftRequestKind)"/> for this request at the workshop <paramref name="ws"/>: an item
    /// nothing has gone into yet (<see cref="CraftAssembly.IsEmpty"/>: a design started by mistake) may be put aside at
    /// any station - nothing is lost with it. Without a workshop, as for the kind alone.
    /// </summary>
    public static string TestStationOnly(in CraftRequest r, CraftWorkshop ws)
    {
        if (r.kind == CraftRequestKind.ScrapPiece && ws != null && CraftAssembly.IsEmpty(ws.Piece(r.piece)))
            return null;
        return TestStationOnly(r.kind);
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

    /// <summary>
    /// A drawn path longer than one request carries (<see cref="MaxPathFloats"/>: a long stroke over the hide, a long
    /// press on the wheel, many looks through the loupe) keeps its first part; <paramref name="note"/> says so. False
    /// when nothing was cut off. The station does this before a network client sends the request, so it always fits.
    /// </summary>
    public static bool Fit(ref CraftRequest r, out string note)
    {
        note = "";
        if (r.points == null || r.points.Count <= MaxPathFloats)
            return false;
        switch (r.kind)
        {
            case CraftRequestKind.Scrape:
            case CraftRequestKind.Stake:
            case CraftRequestKind.Slick:
            case CraftRequestKind.Skive:
            case CraftRequestKind.Dye:
            case CraftRequestKind.Condition:
                r.points = r.points.GetRange(0, MaxPathFloats);
                note = "That stroke was too long to take in at once: only its first part counted.";
                return true;
            case CraftRequestKind.GrindGem:
                r.points = r.points.GetRange(0, MaxPathFloats);
                note = "Only the first part of that counted: lift the stone from the wheel now and then.";
                return true;
            case CraftRequestKind.EvaluateGem:
                r.points = r.points.GetRange(0, MaxPathFloats / 3 * 3);
                note = "Only the first of those looks counted.";
                return true;
            default:
                return false;
        }
    }

    /// <summary>Ids short enough, every number finite, a list within its limit (each kind checks its own list's shape).</summary>
    public static bool WellFormed(in CraftRequest r) => WellFormed(r, out _);

    /// <summary><see cref="WellFormed(in CraftRequest)"/>, saying why not.</summary>
    public static bool WellFormed(in CraftRequest r, out string reason)
    {
        reason = Garbled;
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
            if (r.points.Count > MaxPathFloats)
            {
                reason = TooMuch;
                return false;
            }
            foreach (float f in r.points)
                if (!Finite(f) || Math.Abs(f) > 4096)
                    return false;
        }
        reason = "";
        return true;
    }

    private static bool TooLong(string s) => s != null && s.Length > MaxIdLength;
    private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
