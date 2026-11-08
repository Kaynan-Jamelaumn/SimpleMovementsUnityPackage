using System;
using System.Collections.Generic;

/// <summary>
/// The hand's recent path at a 3D work place, by time: what a moving hand did over the last moment, the same however
/// often the screen samples it. A point is kept only once the hand has moved <see cref="MinStep"/> from the last one kept
/// (so a fast display adds no more points to the same movement, as the window's pointer is sampled by distance), and only
/// the moves of the last <see cref="Keep"/> seconds count. From it: how far and how fast the hand moved, and how it moved a
/// piece in the quench tank - up and down, side to side, round, or held still - by the window's own rule
/// (<see cref="Motion"/>).
/// </summary>
public sealed class CraftHandTrail
{
    // The points kept, oldest first: the first one is where the oldest move that still counts started from.
    private readonly List<(double t, double x, double y)> points = new List<(double, double, double)>();
    private double now = double.NegativeInfinity;

    /// <param name="keep">Seconds of the path that count.</param>
    /// <param name="minStep">The least move (in the caller's units, e.g. pixels) that adds a point.</param>
    public CraftHandTrail(double keep, double minStep)
    {
        Keep = Math.Max(1e-3, keep);
        MinStep = Math.Max(0, minStep);
    }

    /// <summary>Seconds of the path that count.</summary>
    public double Keep { get; }
    /// <summary>The least move that adds a point (the caller keeps it in step with the screen's size).</summary>
    public double MinStep { get; set; }

    /// <summary>The moves that count now (each from one point kept to the next).</summary>
    public int Moves
    {
        get
        {
            int n = 0;
            for (int i = 1; i < points.Count; i++)
                if (points[i].t >= now - Keep) n++;
            return n;
        }
    }

    /// <summary>The hand is at (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="time"/> (seconds).</summary>
    public void Add(double time, double x, double y)
    {
        now = Math.Max(now, time);
        if (points.Count == 0)
        {
            points.Add((time, x, y));
            return;
        }
        (double t, double x, double y) last = points[points.Count - 1];
        double dx = x - last.x, dy = y - last.y;
        if (dx * dx + dy * dy >= MinStep * MinStep && dx * dx + dy * dy > 0)
            points.Add((time, x, y));
        // The moves that ended before the last Keep seconds no longer count: their starting points go.
        while (points.Count > 2 && points[1].t < now - Keep)
            points.RemoveAt(0);
    }

    /// <summary>Time passes with the hand where it was (it is held still): the moves age.</summary>
    public void Tick(double time) => now = Math.Max(now, time);

    /// <summary>Forgets the path (the hand let go).</summary>
    public void Clear()
    {
        points.Clear();
        now = double.NegativeInfinity;
    }

    /// <summary>How far the hand moved along x and along y in the last <see cref="Keep"/> seconds (each summed as it went, back and forth).</summary>
    public void Travel(out double alongX, out double alongY)
    {
        alongX = alongY = 0;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].t < now - Keep) continue;
            alongX += Math.Abs(points[i].x - points[i - 1].x);
            alongY += Math.Abs(points[i].y - points[i - 1].y);
        }
    }

    /// <summary>The length of the path of the last <see cref="Keep"/> seconds.</summary>
    public double Length
    {
        get
        {
            double s = 0;
            for (int i = 1; i < points.Count; i++)
                if (points[i].t >= now - Keep)
                {
                    double dx = points[i].x - points[i - 1].x, dy = points[i].y - points[i - 1].y;
                    s += Math.Sqrt(dx * dx + dy * dy);
                }
            return s;
        }
    }

    /// <summary>How fast the hand moved over the last <see cref="Keep"/> seconds (units a second).</summary>
    public double Speed => Length / Keep;

    /// <summary>
    /// How the hand moved a piece in the tank over the last <see cref="Keep"/> seconds - the window's rule, by distance and
    /// time: held still unless it travelled at least <paramref name="least"/> (x and y summed) in at least three moves;
    /// round when it kept turning, else up and down when it went mostly up and down (half again as much as sideways), side
    /// to side when mostly sideways, round for a mixed path.
    /// </summary>
    public CraftQuenchMotion Motion(double least)
    {
        double dx = 0, dy = 0, turn = 0;
        int moves = 0;
        double px = 0, py = 0;
        bool havePrev = false;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].t < now - Keep) continue;
            double mx = points[i].x - points[i - 1].x, my = points[i].y - points[i - 1].y;
            dx += Math.Abs(mx);
            dy += Math.Abs(my);
            if (havePrev) turn += px * my - py * mx;
            px = mx;
            py = my;
            havePrev = true;
            moves++;
        }
        if (moves < 3 || dx + dy <= least)
            return CraftQuenchMotion.Still;
        if (Math.Abs(turn) > (dx + dy) * 6) return CraftQuenchMotion.Swirl;
        if (dy > dx * 1.5) return CraftQuenchMotion.UpDown;
        if (dx > dy * 1.5) return CraftQuenchMotion.SideToSide;
        return CraftQuenchMotion.Swirl;
    }
}

/// <summary>
/// The circles a hand draws turning a bit in its brace: how many turns, and how steady - counted round the middle of the
/// circles themselves (the circle that best fits the last lap of the hand's path), so a circle counts wherever it is
/// drawn and from wherever the hand starts it: the press need be at no centre, and the hand need see none (the 3D bench
/// hides the pointer; in the window the hand presses at the end and circles from there). A point is kept only once the
/// hand has moved <see cref="MinStep"/> from the last one (any frame rate draws the same path). A lap of a steady circle
/// is one turn and reads steady (near 1) - the first lap counts once the path is round enough to show its middle; a path
/// along a line (a drag, a back-and-forth) turns nothing; an uneven loop - a wobbling radius, a squashed one - reads less
/// steady.
/// </summary>
public sealed class CraftHandCircles
{
    // The points of the last lap (and a little more), oldest first, each with the turning counted up to it.
    private readonly List<(double x, double y, double turned)> lap = new List<(double, double, double)>();
    private double travelled, turned, wound, centreX, centreY, lastX, lastY;
    private bool round;

    /// <summary>The most points kept (a very large, slow circle is judged by its last part).</summary>
    public const int MaxPoints = 720;
    /// <summary>
    /// The path of the last lap turns round its middle only once it spreads both ways - its narrower spread at least this
    /// share of the whole, squared (about a seventh as wide as it is long, a third of a circle): along a line it is no circle.
    /// </summary>
    public const double RoundEnough = 0.02;

    /// <param name="minStep">The least move (in the caller's units, e.g. pixels) that adds a point.</param>
    public CraftHandCircles(double minStep = 2) { MinStep = minStep; }

    /// <summary>The least move that adds a point (the caller keeps it in step with the screen's size).</summary>
    public double MinStep { get; set; }
    /// <summary>Circles are being counted (from <see cref="Begin"/> until <see cref="Clear"/>).</summary>
    public bool Active { get; private set; }
    /// <summary>Whole turns since <see cref="Begin"/> (either way round), less those <see cref="Spend"/> took off.</summary>
    public double Turns => Math.Abs(travelled) / (2 * Math.PI);
    /// <summary>How far round the hand has wound since <see cref="Begin"/> (radians, signed; never taken off): what turns a bit drawn turning.</summary>
    public double Wound => wound;
    /// <summary>The turning counted since <see cref="Begin"/> (radians, either way): from half a circle (π) on, <see cref="Steadiness"/> has a lap to judge.</summary>
    public double Turned => turned;
    /// <summary>The middle of the circles drawn now (the caller's units).</summary>
    public double CentreX => centreX;
    public double CentreY => centreY;

    /// <summary>
    /// 0-1: the radius held even round the middle of the last lap (1 a true circle; 0 while there is no circle to judge).
    /// Under <see cref="CraftWoodwork.CarveSplitSteady"/> a bit turned so splits the wood.
    /// </summary>
    public double Steadiness
    {
        get
        {
            int n = lap.Count;
            if (n < 8 || !round)
                return 0;
            double s = 0, s2 = 0;
            foreach ((double x, double y, double turned) q in lap)
            {
                double r = Math.Sqrt((q.x - centreX) * (q.x - centreX) + (q.y - centreY) * (q.y - centreY));
                s += r;
                s2 += r * r;
            }
            double mean = s / n, var = Math.Max(0, s2 / n - mean * mean);
            return CraftMath.Clamp01(1 - Math.Sqrt(var) / Math.Max(1, mean) * 2.5);
        }
    }

    /// <summary>The hand presses at (<paramref name="x"/>, <paramref name="y"/>): circles are counted from here.</summary>
    public void Begin(double x, double y)
    {
        lap.Clear();
        travelled = turned = wound = 0;
        round = false;
        Active = true;
        lastX = centreX = x;
        lastY = centreY = y;
    }

    /// <summary>The hand is at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void Add(double x, double y)
    {
        if (!Active)
            return;
        double dx = x - lastX, dy = y - lastY;
        if (dx * dx + dy * dy < Math.Max(1e-9, MinStep * MinStep))
            return;
        // The step's turning round the middle of the circles drawn so far (both its ends from the same middle, so the
        // middle's own drift adds nothing) - none while the path lies along a line, nor for a step that passes close by the
        // middle (it tells no way round).
        double ax = lastX - centreX, ay = lastY - centreY, bx = x - centreX, by = y - centreY, near = 1.5 * MinStep;
        if (round && ax * ax + ay * ay >= near * near && bx * bx + by * by >= near * near)
        {
            double d = Math.Atan2(ax * by - ay * bx, ax * bx + ay * by);
            travelled += d;
            wound += d;
            turned += Math.Abs(d);
        }
        lastX = x;
        lastY = y;
        lap.Add((x, y, turned));
        // Only the last lap tells where the middle is now (a hand's circles drift): what lies more than a turn behind goes.
        while (lap.Count > 3 && turned - lap[0].turned > 2 * Math.PI) lap.RemoveAt(0);
        while (lap.Count > MaxPoints) lap.RemoveAt(0);
        Middle();
    }

    /// <summary>Takes <paramref name="turns"/> off the count (they were sent).</summary>
    public void Spend(double turns) => travelled -= Math.Sign(travelled) * turns * 2 * Math.PI;

    /// <summary>The hand let go.</summary>
    public void Clear()
    {
        Active = round = false;
        lap.Clear();
        travelled = turned = wound = 0;
    }

    /// <summary>
    /// Whether the lap spreads both ways (<see cref="RoundEnough"/>: a loop, not a line), and its middle: the centre of the
    /// circle that fits its points best (least squares - a third of a circle already shows where its middle is), the
    /// mean of the points while they lie too nearly along a line to tell.
    /// </summary>
    private void Middle()
    {
        int n = lap.Count;
        double mx = 0, my = 0;
        foreach ((double x, double y, double turned) q in lap) { mx += q.x; my += q.y; }
        mx /= n;
        my /= n;
        double suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
        foreach ((double x, double y, double turned) q in lap)
        {
            double u = q.x - mx, v = q.y - my;
            suu += u * u;
            svv += v * v;
            suv += u * v;
            suuu += u * u * u;
            svvv += v * v * v;
            suvv += u * v * v;
            svuu += v * u * u;
        }
        double spread = suu + svv, det = suu * svv - suv * suv;
        round = n >= 5 && spread > 1e-9 && det >= RoundEnough * spread * spread;
        centreX = mx;
        centreY = my;
        if (!round)
            return;
        double bu = 0.5 * (suuu + suvv), bv = 0.5 * (svvv + svuu);
        centreX = mx + (bu * svv - bv * suv) / det;
        centreY = my + (bv * suu - bu * suv) / det;
    }
}

/// <summary>
/// The 3D ore stone's surface as the brushes work it, patch by patch (its vertices): each patch stands for its share of
/// the surface and carries the dirt and the weathered rind shown on it. Each brush can go over each patch once - it takes
/// its share of the dirt there (<see cref="CraftOres.BrushOnDirt"/>) and of the rind (<see cref="CraftOres.BrushOnRind"/>)
/// and no more, however long it scrubs, as the workshop's own rule says (<see cref="CraftOres.Brush"/>: what a brush went
/// over is kept on the stone, <see cref="OreFragment.Brushed"/>). What a brush went over is counted as the workshop counts
/// it - weighted by what the brush can take there - so a going-over sends (<see cref="GoingOver"/>) exactly what the stone
/// shows it did: scrubbing just the dirty patches cleans the dirt, and the workshop agrees.
/// </summary>
public sealed class CraftBrushCover
{
    private const int Brushes = 3;
    private double[] area = new double[0], dirt0 = new double[0], rind0 = new double[0];
    private readonly double[][] done = new double[Brushes][];
    private readonly double[] takesDirt = new double[Brushes], takesRind = new double[Brushes], start = new double[Brushes];

    /// <summary>Patches on the stone.</summary>
    public int Count => area.Length;

    /// <summary>
    /// The stone as it is now: each patch's share of the surface (any scale: they are made to add up to 1), the dirt and
    /// the rind shown on it now (0-1), what each brush takes of the dirt and of the rind where it goes (by
    /// <see cref="CraftOreBrush"/>), and how far each brush has gone over the stone already (the fragment's
    /// <see cref="OreFragment.Brushed"/>: spread evenly, since where it went is not known). A going-over starts.
    /// </summary>
    public void Reset(IList<double> areas, IList<double> dirtNow, IList<double> rindNow, IList<double> dirtTaken, IList<double> rindTaken, IList<double> brushed)
    {
        int n = areas?.Count ?? 0;
        area = new double[n];
        dirt0 = new double[n];
        rind0 = new double[n];
        double sum = 0;
        for (int i = 0; i < n; i++) sum += Math.Max(0, areas[i]);
        double dirtLeft = 1, rindLeft = 1;
        for (int b = 0; b < Brushes; b++)
        {
            takesDirt[b] = CraftMath.Clamp(dirtTaken != null && b < dirtTaken.Count ? dirtTaken[b] : 0, 0, 0.999);
            takesRind[b] = CraftMath.Clamp(rindTaken != null && b < rindTaken.Count ? rindTaken[b] : 0, 0, 0.999);
            double c = CraftMath.Clamp01(brushed != null && b < brushed.Count ? brushed[b] : 0);
            done[b] = new double[n];
            for (int i = 0; i < n; i++) done[b][i] = c;
            dirtLeft *= 1 - takesDirt[b] * c;
            rindLeft *= 1 - takesRind[b] * c;
        }
        // What each patch held before any brush went over it: what it shows now, with what the brushes took given back.
        for (int i = 0; i < n; i++)
        {
            area[i] = sum > 0 ? Math.Max(0, areas[i]) / sum : 1.0 / n;
            dirt0[i] = CraftMath.Clamp01(dirtNow != null && i < dirtNow.Count ? dirtNow[i] : 0) / Math.Max(1e-6, dirtLeft);
            rind0[i] = CraftMath.Clamp01(rindNow != null && i < rindNow.Count ? rindNow[i] : 0) / Math.Max(1e-6, rindLeft);
        }
        Begin();
    }

    /// <summary>A going-over starts here: what each brush goes over from now is what it sends (<see cref="GoingOver"/>).</summary>
    public void Begin()
    {
        for (int b = 0; b < Brushes; b++) start[b] = Covered((CraftOreBrush)b);
    }

    /// <summary>The dirt shown on patch <paramref name="i"/> now (0-1): what it held, less what each brush took where it went.</summary>
    public double Dirt(int i)
    {
        double d = dirt0[i];
        for (int b = 0; b < Brushes; b++) d *= 1 - takesDirt[b] * done[b][i];
        return d;
    }

    /// <summary>The weathered rind shown on patch <paramref name="i"/> now (0-1).</summary>
    public double Rind(int i)
    {
        double r = rind0[i];
        for (int b = 0; b < Brushes; b++) r *= 1 - takesRind[b] * done[b][i];
        return r;
    }

    /// <summary>How far brush <paramref name="b"/> has gone over patch <paramref name="i"/> (0-1).</summary>
    public double Done(int i, CraftOreBrush b) => done[(int)b][i];

    /// <summary>The dirt shown over the whole stone (by area, 0-1).</summary>
    public double MeanDirt
    {
        get
        {
            double s = 0;
            for (int i = 0; i < area.Length; i++) s += area[i] * Dirt(i);
            return s;
        }
    }

    /// <summary>The rind shown over the whole stone (by area, 0-1).</summary>
    public double MeanRind
    {
        get
        {
            double s = 0;
            for (int i = 0; i < area.Length; i++) s += area[i] * Rind(i);
            return s;
        }
    }

    /// <summary>
    /// Brush <paramref name="b"/> goes over patch <paramref name="i"/> by up to <paramref name="amount"/> (0-1 of it): it
    /// goes over each patch once at most. Returns how much more of the patch it went over.
    /// </summary>
    public double Scrub(int i, CraftOreBrush b, double amount)
    {
        double[] d = done[(int)b];
        double take = Math.Min(1 - d[i], Math.Max(0, amount));
        if (take <= 0)
            return 0;
        d[i] += take;
        return take;
    }

    /// <summary>
    /// How far brush <paramref name="b"/> has gone over the stone, as the workshop counts it (0-1): each patch by what the
    /// brush can take there - its dirt and its rind, as the other brushes left them - or by its area on a stone with
    /// nothing on it for that brush.
    /// </summary>
    public double Covered(CraftOreBrush b)
    {
        int k = (int)b;
        double[] d = done[k];
        if (d == null || d.Length == 0)
            return 0;
        double sw = 0, swd = 0, sa = 0, sad = 0;
        for (int i = 0; i < area.Length; i++)
        {
            double dirt = dirt0[i], rind = rind0[i];
            for (int o = 0; o < Brushes; o++)
                if (o != k)
                {
                    dirt *= 1 - takesDirt[o] * done[o][i];
                    rind *= 1 - takesRind[o] * done[o][i];
                }
            double w = area[i] * (takesDirt[k] * dirt + takesRind[k] * rind);
            sw += w;
            swd += w * d[i];
            sa += area[i];
            sad += area[i] * d[i];
        }
        return sw > 1e-9 ? CraftMath.Clamp01(swd / sw) : sa > 0 ? CraftMath.Clamp01(sad / sa) : 0;
    }

    /// <summary>What brush <paramref name="b"/> went over in this going-over (since <see cref="Begin"/>), as the workshop counts it: what the going-over sends.</summary>
    public double GoingOver(CraftOreBrush b) => Math.Max(0, Covered(b) - start[(int)b]);
}
