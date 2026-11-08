using System;
using System.Collections.Generic;

/// <summary>
/// The woodshop: debarking a log on the shaving horse, splitting it with wedges and a maul, riving a billet along its
/// grain, sawing to a line (across it, or ripping a taper along it), steaming and bending over a form, seasoning in a
/// drying stack, sanding through the grits and sealing (varnish, oil, wax, resin). Wood is fibres: a piece split along
/// them is strong (its <see cref="WoodState.runout"/> stays low), one whose fibres run out of it breaks along them;
/// steamed long enough for its thickness it bends - too fast, too cold or over too tight a form and the outside of the
/// bend tears, and out of the steam it stays soft for only a minute or so; dried too fast it checks; a finish keeps the
/// water out, if it goes on dry wood in thin, even coats over a sanded surface.
/// <para>Splitting and riving halve the larger of a piece's two cross dimensions - a log's diameter one way, then the
/// other - so the chain log, halves, quarters, billets ends in square stock a haft or a grip is carved from.</para>
/// </summary>
public static class CraftWoodwork
{
    // ------------------------------------------------------------------ water
    /// <summary>Moisture of seasoned wood in ordinary air (it ends near this indoors).</summary>
    public const double Seasoned = 0.12;
    /// <summary>Moisture of a freshly felled log.</summary>
    public const double Green = 0.55;
    /// <summary>
    /// The most water wood may hold to go into a fit: wetter, it is still green and shrinks loose as it dries - the
    /// finished item says so, the look and the stack call it damp, the guide sends it to the stack.
    /// </summary>
    public const double DryEnough = 0.2;
    /// <summary>The water steam brings wood up to (it never takes it further, nor lower).</summary>
    public const double SteamWater = 0.32;
    /// <summary>How fast wood loose in the air takes water back when it is drier than the air (a share of the gap per process hour).</summary>
    public const double AirRegain = 0.2;

    // ------------------------------------------------------------------ sizes
    /// <summary>The least width (mm) a log can be split with wedges (narrower: rive it with the froe).</summary>
    public const double MinSplitWidth = 60;
    /// <summary>The least its lesser cross dimension (mm) may be to take a steel wedge: a thinner board is rived or ripped with the saw.</summary>
    public const double MinWedgeThickness = 40;
    /// <summary>The least width (mm) a half can be after riving.</summary>
    public const double MinRiveWidth = 8;

    // ------------------------------------------------------------------ the drawknife
    /// <summary>The drawknife's depth (0 the bark's surface - 1 deep in the wood) from which the bark lifts whole; shallower it shaves only the outer bark.</summary>
    public const double PeelDepth = 0.3;
    /// <summary>The depth past which the drawknife gouges the wood: green wood (sap up) peels deeper than dry.</summary>
    public const double GougeGreen = 0.62, GougeDry = 0.52;
    /// <summary>The faces a log is turned through on the shaving horse.</summary>
    public const int LogFaces = 4;
    /// <summary>The share of the way round the log one pull on a face takes (two pulls a face, every face: the whole round).</summary>
    public const double DebarkPerStroke = 1.0 / (2 * LogFaces);

    // ------------------------------------------------------------------ the wedges and the froe
    /// <summary>How much more of the split one half takes for each unit the wedge's line runs off the heart.</summary>
    public const double SplitShare = 0.38;
    /// <summary>A split this far off the heart or less (0-1) and this true to a ray or more (0-1) parts cleanly in even halves.</summary>
    public const double SplitEven = 0.15, SplitAlong = 0.75;
    /// <summary>A line less true to the rays than this twists out across the grain.</summary>
    public const double TwistsOut = 0.4;
    /// <summary>A rive that strayed less than this followed the grain; more than <see cref="RiveRunsOut"/> it ran out of the billet.</summary>
    public const double RiveClean = 0.15, RiveRunsOut = 0.45;

    // ------------------------------------------------------------------ the saw
    /// <summary>A saw cut that wandered less than this (0-1) is square on its line.</summary>
    public const double SawSquare = 0.15;
    /// <summary>The least the saw leaves at either end of a piece (mm): a mark closer to an end cuts nothing off.</summary>
    public const double SawEndMargin = 2;

    // ------------------------------------------------------------------ the steam box and the form
    /// <summary>Seconds out of the steam a piece 25 mm thick stays soft enough to bend (thinner sets sooner).</summary>
    public const double CoolSecondsPer25mm = 45;
    /// <summary>The heat (°C, the piece's mean) from which the steam soaks into it: before that it is heating through.</summary>
    public const double SoakHeat = 85;

    // ------------------------------------------------------------------ the drying stack
    /// <summary>Stickers laid less evenly than this (0-1) let it dry unevenly: it warps.</summary>
    public const double EvenStickers = 0.7;
    /// <summary>
    /// How far in from an end (a share of the board's length) its end sticker may sit and still hold the end up: further
    /// in, the end droops off it (<see cref="StickerEvenness"/>), until <see cref="StickerOverhang"/> + 0.1 holds nothing.
    /// </summary>
    public const double StickerOverhang = 0.06;
    /// <summary>The thickness (mm) up to which wood seasons at the stack's full pace: thicker, the water has further to come out - about a year an inch.</summary>
    public const double SeasonThickness = 40;
    /// <summary>Ends sealed this well or better (0-1) hardly check; a cover this far over the stack or more is shade.</summary>
    public const double WellSealed = 0.8, WellShaded = 0.7;
    /// <summary>The workshop variable that holds how far the cover is pulled over the drying stack (0 open sun - 1 shade).</summary>
    public const string CoverVariable = "stackCover";

    // ------------------------------------------------------------------ the knife
    /// <summary>A knife stroke deeper than this (mm, as the workshop takes it), or shakier than <see cref="CarveSplitSteady"/>, splits the wood along its grain.</summary>
    public const double CarveSplitDepth = 1.4, CarveSplitSteady = 0.35;

    /// <summary>
    /// How far a stroke strayed from its own line (in the units of its samples): the farthest of its samples from the
    /// straight line that fits them best, whichever way that runs - a straight stroke along a sloping target is steady, one
    /// that wavers is not. 0 for fewer than three samples.
    /// </summary>
    public static double StrokeWander(IReadOnlyList<UnityEngine.Vector2> pts) => StrokeWander(pts, out _);

    /// <summary>
    /// <see cref="StrokeWander"/>, and how far the newest sample lies off that line (<paramref name="last"/>, signed: + to
    /// the left of the line's way, so above it for a stroke drawn sideways) - where the hand is now against the stroke's
    /// own line, as the 3D carving bench carries the knife.
    /// </summary>
    public static double StrokeWander(IReadOnlyList<UnityEngine.Vector2> pts, out double last)
    {
        last = 0;
        int n = pts?.Count ?? 0;
        if (n < 3)
            return 0;
        double mx = 0, my = 0;
        foreach (UnityEngine.Vector2 q in pts) { mx += q.x; my += q.y; }
        mx /= n;
        my /= n;
        double sxx = 0, syy = 0, sxy = 0;
        foreach (UnityEngine.Vector2 q in pts)
        {
            double x = q.x - mx, y = q.y - my;
            sxx += x * x;
            syy += y * y;
            sxy += x * y;
        }
        // the line's direction: the samples' main axis
        double a = 0.5 * Math.Atan2(2 * sxy, sxx - syy), cx = Math.Cos(a), cy = Math.Sin(a), most = 0;
        foreach (UnityEngine.Vector2 q in pts) most = Math.Max(most, Math.Abs(-(q.x - mx) * cy + (q.y - my) * cx));
        UnityEngine.Vector2 end = pts[n - 1];
        last = -(end.x - mx) * cy + (end.y - my) * cx;
        return most;
    }

    /// <summary>
    /// How steady a knife stroke drawn by hand is (0-1), the window's and the 3D bench's one rule: how far it strayed from
    /// its own line (<see cref="StrokeWander"/>) against <paramref name="tolerance"/> (in the samples' units: a stroke that
    /// strays that far is no steady stroke at all), times what the hands' tremble (<paramref name="unsteadiness"/>) leaves.
    /// Under <see cref="CarveSplitSteady"/> the knife tears along the grain and splits the wood.
    /// </summary>
    public static double StrokeSteady(IReadOnlyList<UnityEngine.Vector2> pts, double tolerance, double unsteadiness) =>
        CraftMath.Clamp01(1 - StrokeWander(pts) / Math.Max(1e-6, tolerance)) * CraftMath.Clamp01(1 - unsteadiness * 5);

    /// <summary>A knife stroke this steady or steadier (<see cref="StrokeSteady"/>) shaves cleanly ("a clean, steady shaving"); below it, it leaves the surface rough.</summary>
    public const double CleanStroke = 0.75;

    // ------------------------------------------------------------------ the papers
    /// <summary>
    /// A paper this coarse or coarser (0 coarse - 1 fine) has none coarser before it: it cuts at its full rate on any
    /// surface. A finer one cuts properly only where the surface is no rougher than the paper <see cref="GritStep"/>
    /// coarser leaves (plus <see cref="GritReach"/>): deeper scratches it hardly touches (<see cref="SkippedRate"/>).
    /// </summary>
    public const double CoarsestGrit = 0.4, GritStep = 0.3, GritReach = 3, SkippedRate = 0.1;
    /// <summary>Sanded last with a paper this fine or finer, it is sanded smooth through the grits (the look: "smooth as silk").</summary>
    public const double FineGrit = 0.8;

    // ------------------------------------------------------------------ the brush
    /// <summary>Process hours a coat takes to dry when its supply does not say.</summary>
    public const double DefaultDryingHours = 4;
    /// <summary>Full-length strokes of an ordinary coat one item of a finish lays (a pot of varnish, a flask of oil, a cake of wax).</summary>
    public const double StrokesPerItem = 2;

    /// <summary>What wood settles to in air of <paramref name="humidity"/> (0-1): 8 % in dry air, about 12 % in ordinary air, 20 % in damp.</summary>
    public static double Equilibrium(double humidity) => 0.06 + 0.12 * CraftMath.Clamp01(humidity) + (humidity > 0.8 ? (humidity - 0.8) * 0.3 : 0);

    /// <summary>The mean bark left on a piece (0-1).</summary>
    public static double BarkLeft(CraftPiece p)
    {
        if (p == null || p.segments.Count == 0) return 0;
        double b = 0;
        foreach (MetalSegment s in p.segments) b += s.bark;
        return b / p.segments.Count;
    }

    /// <summary>How readily the piece's wood cleaves along its grain (0 interlocked - 1 splits straight; 0.6 when unknown).</summary>
    public static double Cleave(CraftRules rules, CraftPiece p)
    {
        CraftWoodDefinition d = rules != null && p != null ? rules.Wood(p.wood.wood) : null;
        return d != null ? CraftMath.Clamp01(d.cleave) : 0.6;
    }

    // ------------------------------------------------------------------ the piece's size
    /// <summary>The largest cross dimension anywhere along it (mm): what the wedges and the froe must part.</summary>
    public static double CrossSize(CraftPiece p)
    {
        double m = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) m = Math.Max(m, Math.Max(s.width, s.thickness));
        return m;
    }

    /// <summary>
    /// Which way a split or a rive parts the piece: across its larger dimension (on average along it) - its thickness
    /// when that is the larger, its width otherwise (a round log, square stock: the width).
    /// </summary>
    public static bool HalvesThickness(CraftPiece p)
    {
        if (p == null || p.segments.Count == 0) return false;
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        return t > w + 1e-6;
    }

    /// <summary>The lesser of a piece's two cross dimensions (mm, each its mean along it): a board's thickness, a log's diameter.</summary>
    public static double LesserSize(CraftPiece p)
    {
        if (p == null || p.segments.Count == 0) return 0;
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        return Math.Min(w, t) / p.segments.Count;
    }

    /// <summary>
    /// Can the wedges split it (a log or a large half)? False, with the tool that fits it, when it is too small - or too
    /// thin for a steel wedge to be driven into its end (a board: rived with the froe or ripped with the saw).
    /// </summary>
    public static bool SplitsWithWedges(CraftPiece p, out string reason)
    {
        reason = "";
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0) { reason = "That is not wood."; return false; }
        if (CrossSize(p) < MinSplitWidth) { reason = "It is small enough to rive with the froe."; return false; }
        if (LesserSize(p) < MinWedgeThickness) { reason = $"It is too thin for the wedges ({LesserSize(p):0} mm): rive it with the froe, or rip it with the saw."; return false; }
        return true;
    }

    /// <summary>
    /// Does a wedge's line drawn on the end with the direction (<paramref name="dx"/> across its width,
    /// <paramref name="dy"/> across its thickness) run across the dimension the wedges part (the larger:
    /// <see cref="HalvesThickness"/>)? A section near square - a round log, a quarter - parts the same whichever way, so
    /// any line will do. False, with what to draw instead, for a line along the larger side (a slab the wedges do not cut).
    /// </summary>
    public static bool WedgeLineAcross(CraftPiece p, double dx, double dy, out string reason)
    {
        reason = "";
        if (p == null || p.segments.Count == 0)
            return false;
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        w /= p.segments.Count;
        t /= p.segments.Count;
        if (Math.Abs(w - t) <= 0.1 * Math.Max(w, t))
            return true;
        // parting the thickness, the line runs along the width; parting the width, along the thickness
        bool ok = t > w ? Math.Abs(dx) >= Math.Abs(dy) : Math.Abs(dy) >= Math.Abs(dx);
        if (!ok) reason = $"The wedges part it across its {Math.Max(w, t):0} mm side: draw the line across that side (through the heart).";
        return ok;
    }

    /// <summary>Can the froe rive it? A round log too big for it goes to the wedges first; a slat too narrow cannot be rived again.</summary>
    public static bool RivesWithFroe(CraftPiece p, out string reason)
    {
        reason = "";
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0) { reason = "That is not wood."; return false; }
        double size = CrossSize(p);
        if (p.wood.log && size >= MinSplitWidth) { reason = "Split the round log with the wedges first."; return false; }
        if (size < MinRiveWidth * 2 + 2) { reason = "It is too narrow to rive again."; return false; }
        return true;
    }

    /// <summary>
    /// The length (mm) a wooden part of a design is judged by: its shape's, when the design gives it one (two or more
    /// profile points: the shape the evaluator scores and the carving bench dashes over the blank), else the wooden part's
    /// own range. False for a part that is not of wood.
    /// </summary>
    public static bool LengthWanted(CraftComponentSpec part, out double min, out double max)
    {
        min = max = 0;
        if (part == null || part.kind != CraftPieceKind.Wood)
            return false;
        CraftShapeTarget shape = part.shape;
        if (shape != null && shape.profile != null && shape.profile.Count >= 2)
        {
            min = shape.lengthMin;
            max = Math.Max(shape.lengthMin, shape.lengthMax);
        }
        else if (part.wood != null)
        {
            min = part.wood.lengthMin;
            max = Math.Max(part.wood.lengthMin, part.wood.lengthMax);
        }
        return max > 0;
    }

    /// <summary>Is a length (mm) off a wooden part's range by as much as the evaluator notes (too short, too long)?</summary>
    public static bool LengthOff(CraftComponentSpec part, double length) =>
        LengthWanted(part, out double min, out double max) && CraftEvaluator.LengthScore(length, min, max) < CraftEvaluator.LengthNoted;

    /// <summary>Can a saw cut it at <paramref name="position"/> (0-1)? Not where it would cut nothing off.</summary>
    public static bool CanSawAt(CraftPiece p, double position, out string reason)
    {
        reason = "";
        double l = p != null ? p.Length : 0, at = CraftMath.Clamp01(position) * l;
        if (at < SawEndMargin || at > l - SawEndMargin) { reason = "Mark it further from the end: there is nothing to cut off there."; return false; }
        return true;
    }

    /// <summary>The width (mm) a taper line from (<paramref name="t0"/>, <paramref name="w0"/>) to (<paramref name="t1"/>, <paramref name="w1"/>) has at <paramref name="t"/> (0-1 along the piece).</summary>
    public static double RipLineAt(double t0, double w0, double t1, double w1, double t) =>
        Math.Abs(t1 - t0) < 1e-9 ? Math.Min(w0, w1) : w0 + (w1 - w0) * CraftMath.Clamp01((t - t0) / (t1 - t0));

    /// <summary>
    /// Would a taper ripped along the line from <paramref name="from"/> (0-1 along it) at <paramref name="widthFrom"/> mm
    /// to <paramref name="to"/> at <paramref name="widthTo"/> mm take anything off? False, with why, when the line is too
    /// short to saw along, would leave nothing, or lies outside the piece all the way.
    /// </summary>
    public static bool RipNarrows(CraftPiece p, double from, double to, double widthFrom, double widthTo, out string reason)
    {
        reason = "";
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0) { reason = "That cannot be sawn."; return false; }
        double t0 = CraftMath.Clamp01(from), t1 = CraftMath.Clamp01(to);
        if (Math.Abs(t1 - t0) * p.Length < 20) { reason = "Mark the line along the board, from one point to another."; return false; }
        if (Math.Min(widthFrom, widthTo) < 3) { reason = "That would leave nothing of it there."; return false; }
        double lo = Math.Min(t0, t1), hi = Math.Max(t0, t1);
        for (int i = 0; i < p.segments.Count; i++)
        {
            double t = p.PositionOf(i);
            if (t >= lo && t <= hi && p.segments[i].width > RipLineAt(t0, widthFrom, t1, widthTo, t) + 0.5)
                return true;
        }
        reason = "The line is outside the board there: it would cut nothing off.";
        return false;
    }

    // ------------------------------------------------------------------ every step
    /// <summary>
    /// Steaming soaks heat and water in; out of the steam the wood sets again within a minute or so; in the stack it
    /// seasons; a coat dries; loose in the air it takes back water it lost to heat.
    /// </summary>
    public static void Step(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        WoodState w = p.wood;
        double hours = dt * ws.Rules.Settings.processHoursPerSecond;
        double t = MeanTemperature(p);
        double humid = ws.Humidity, settle = Equilibrium(humid);
        if (p.location == CraftLocation.Steam)
        {
            // It heats through first; then about an hour of steam for every 25 mm of thickness softens it through.
            double soak = Math.Max(0.25, Thickness(p) / 25.0);
            if (t > SoakHeat) w.steamed = Math.Min(1, w.steamed + hours / soak);
            // It takes water up from the steam (to what steam can give), never loses any there.
            if (w.moisture < SteamWater)
                w.moisture = Math.Min(SteamWater, w.moisture + hours * 0.08);
            // Left far too long it goes soft and mushy: the fibres weaken.
            if (w.steamed >= 1) w.checks = Math.Min(1, w.checks + hours * 0.004);
        }
        else if (w.steamed > 0)
            w.steamed = Math.Max(0, w.steamed - dt / CoolSeconds(p)); // out of the steam it sets again, thin wood soonest
        // A coat dries, at its own pace.
        if (w.finishWet > 0) w.finishWet = Math.Max(0, w.finishWet - hours / DryingHours(w));
        // Bark left on wet wood keeps the water in and lets rot and beetles in.
        w.bark = BarkLeft(p);
        if (w.bark > 0.2 && w.moisture > 0.25 && p.location != CraftLocation.Steam)
            w.rot = Math.Min(1, w.rot + hours * 0.002 * w.bark * (w.moisture - 0.2) * 4);
        // Seasoning: in the stack (or hung on the rack) the water goes; too fast and the surface and ends check.
        if (p.location == CraftLocation.Stack || p.location == CraftLocation.Rack)
        {
            bool stack = p.location == CraftLocation.Stack;
            // The stack's cover is the stack's: as far as it is pulled now (a stack never covered: as the board was stacked).
            double even = stack ? w.stackEven : 0.4, shade = stack ? ws.StackCover ?? w.shade : 0.5, ends = stack ? w.endsSealed : 0;
            // Damp air dries it slower and to a wetter end; bark left on holds the water in; thick stock dries slowly
            // (Seasoning: a whole log far slower than the billets rived from it).
            double through = Seasoning(p);
            double rate = 0.08 * (0.6 + 0.8 * even) * (1.5 - shade) * (1 + Math.Max(0, ws.Ambient - 18) / 30) * (1.35 - 0.7 * humid) * (1 - 0.6 * w.bark) * through;
            double before = w.moisture;
            w.moisture = before > settle
                ? Math.Max(settle, w.moisture - (w.moisture - settle) * (1 - Math.Exp(-rate * hours)))
                : Math.Min(settle, w.moisture + (settle - w.moisture) * (1 - Math.Exp(-rate * 0.5 * hours)));
            double lost = before - w.moisture;
            if (lost > 0 && before > 0.2)
            {
                // Water leaving faster than the wood can shrink evenly: checks, worst at unsealed ends, in dense wood. A
                // stack laid well - shaded, the ends sealed, on even stickers - dries it slower than that; thick stock
                // only as slowly as it dries.
                CraftWoodDefinition d = ws.Rules.Wood(w.wood);
                double safe = 0.03 * (1.3 - (d?.density ?? 0.7) * 0.5) * (0.5 + 0.5 * CraftMath.Clamp01(ends)) * through;
                double speed = lost / Math.Max(1e-6, hours);
                if (speed > safe)
                    w.checks = Math.Min(1, w.checks + (speed - safe) * hours * 8 * (1 - 0.75 * ends) * (0.6 + 0.4 * (1 - shade)));
                // Unevenly stickered, it dries unevenly and warps.
                if (even < EvenStickers)
                    for (int i = 0; i < p.segments.Count; i++)
                        p.segments[i].bend += (EvenStickers - even) * lost * 30 * (CraftMath.Noise(CraftMath.Hash(p.id ?? ""), i, 41) - 0.5);
            }
        }
        else if ((p.location == CraftLocation.Air || p.location == CraftLocation.Assembled) && w.moisture < settle)
            // Loose in the air it does not season (the stack does that), but neither does it stay drier than the air holds:
            // wood dried by heat takes the water back as it cools.
            w.moisture = Math.Min(settle, w.moisture + (settle - w.moisture) * (1 - Math.Exp(-AirRegain * hours)));
    }

    public static double MeanTemperature(CraftPiece p)
    {
        double t = 0;
        foreach (MetalSegment s in p.segments) t += s.body.temp;
        return p.segments.Count > 0 ? t / p.segments.Count : 20;
    }

    /// <summary>The thickness that bends, steams and seasons (mm): the lesser of its width and thickness, the largest along it.</summary>
    public static double Thickness(CraftPiece p)
    {
        double t = 0;
        foreach (MetalSegment s in p.segments) t = Math.Max(t, Math.Min(s.width, s.thickness));
        return t;
    }

    /// <summary>
    /// The pace (0-1) a piece seasons at in the stack for its thickness: 1 up to <see cref="SeasonThickness"/>, slower in
    /// proportion past it - a 240 mm log six times slower than a billet: rive it first.
    /// </summary>
    public static double Seasoning(CraftPiece p) => p == null ? 1 : Math.Min(1, SeasonThickness / Math.Max(1, Thickness(p)));

    /// <summary>How readily it bends now (0 stiff - 1 fully softened): steamed through, and still hot.</summary>
    public static double Plasticity(CraftPiece p) =>
        p.kind == CraftPieceKind.Wood ? CraftMath.Clamp01(p.wood.steamed) * CraftMath.Smooth(55, 95, MeanTemperature(p)) : 0;

    /// <summary>Seconds a piece steamed through stays soft out of the steam: 45 s for 25 mm, thinner sooner (no less than 18 s).</summary>
    public static double CoolSeconds(CraftPiece p) => CoolSecondsPer25mm * Math.Max(0.4, Thickness(p) / 25.0);

    /// <summary>Process hours the piece's last coat takes to dry.</summary>
    public static double DryingHours(WoodState w) => w.finishHours > 0 ? w.finishHours : DefaultDryingHours;

    // ------------------------------------------------------------------ riving
    /// <summary>
    /// How far a rive strays from the grain (0-1) with the froe's own stray <paramref name="stray"/>, set on a line at
    /// <paramref name="line"/> (0-1 across the billet: 0.5 the middle) in wood that cleaves <paramref name="cleave"/>: an
    /// off-centre split runs out toward the thin side, and interlocked grain fights the froe.
    /// </summary>
    public static double RiveStray(double stray, double line, double cleave)
    {
        double off = Math.Abs(CraftMath.Clamp01(line) - 0.5) * 2;
        return CraftMath.Clamp01(CraftMath.Clamp01(stray) * (1 + 0.8 * Math.Max(0, 0.7 - CraftMath.Clamp01(cleave))) + off * off * 0.3);
    }

    /// <summary>What a rive of <paramref name="stray"/> (as <see cref="RiveStray"/> gives it) did, in words.</summary>
    public static string RiveWords(double stray) =>
        stray < RiveClean ? "Rived cleanly along the grain" : stray < RiveRunsOut ? "Rived, the split wandering off the grain" : "Rived: the split ran out of the billet";

    /// <summary>
    /// Rives the piece in two along its length with the froe. <c>a</c> = how far the split strayed from the grain
    /// (0 followed it - 1 cut straight across it), <c>b</c> = how steady the froe was driven, <c>c</c> = where across the
    /// billet the froe was set (0-1; 0 = the middle, an older record). It parts the larger cross dimension. The halves
    /// keep the grain the split followed: a good split leaves them straighter-grained than the stock was.
    /// </summary>
    public static void Rive(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double line = op.c > 0 ? CraftMath.Clamp(op.c, 0.05, 0.95) : 0.5;
        double stray = RiveStray(op.a, line, Cleave(ws.Rules, p)), steady = CraftMath.Clamp01(op.b);
        bool thick = HalvesThickness(p);
        CraftPiece q = ws.Create(op, CraftPieceKind.Wood, p.Derived("rived"));
        string id = q.id;
        CraftPiece copy = p.Clone();
        q.wood = copy.wood;
        q.segments = copy.segments;
        q.record = copy.record;
        q.tags = copy.tags;
        q.location = CraftLocation.Air;
        q.id = id;
        // Following the grain straightens it; straying from it cuts across the fibres.
        double runout = CraftMath.Clamp01(p.wood.runout * (0.35 + 0.65 * stray) + stray * 0.7);
        bool first = true;
        foreach (CraftPiece half in new[] { p, q })
        {
            half.wood.runout = runout;
            half.wood.log = false; // a small round log rived is a log no more
            half.wood.lastGrit = -1; // fresh faces
            foreach (MetalSegment s in half.segments)
            {
                double d = thick ? s.thickness : s.width;
                // the froe parts it where it was set (each half at least the least a rive leaves)
                double share = d > MinRiveWidth * 2 + 2 ? CraftMath.Clamp(line, (MinRiveWidth + 1) / d, 1 - (MinRiveWidth + 1) / d) : 0.5;
                double size = Math.Max(MinRiveWidth, d * (first ? share : 1 - share) - 1);
                if (thick) s.thickness = size; else s.width = size;
                s.rough = Math.Max(s.rough, 25 + 30 * stray);
                s.bark *= 0.5; // the new faces are clean wood
            }
            half.wood.bark = BarkLeft(half);
            first = false;
        }
        // A split that ran out of the billet: the second half tapers off short.
        if (stray > RiveRunsOut)
        {
            int keep = Math.Max(1, (int)Math.Round(q.segments.Count * (1 - (stray - RiveRunsOut) * 1.2)));
            if (keep < q.segments.Count) q.segments.RemoveRange(keep, q.segments.Count - keep);
            for (int i = 0; i < q.segments.Count; i++)
            {
                MetalSegment s = q.segments[i];
                double tapered = (thick ? s.thickness : s.width) * (1 - 0.5 * i / Math.Max(1.0, q.segments.Count));
                if (thick) s.thickness = Math.Max(MinRiveWidth * 0.6, tapered); else s.width = Math.Max(MinRiveWidth * 0.6, tapered);
            }
        }
        // A jerky drive bruises and checks the faces.
        if (steady < 0.5)
        {
            p.wood.checks = Math.Min(1, p.wood.checks + (0.5 - steady) * 0.2);
            q.wood.checks = Math.Min(1, q.wood.checks + (0.5 - steady) * 0.2);
        }
        string how = RiveWords(stray);
        p.Record(ws.Elapsed, CraftOpKind.Rive, how, stray);
        q.Record(ws.Elapsed, CraftOpKind.Rive, how, stray);
        ws.Raise(CraftEventKind.Cut, p.id, q.id, "");
    }

    // ------------------------------------------------------------------ sawing
    /// <summary>What a saw cut that wandered <paramref name="wander"/> (0-1) and is <paramref name="square"/> (0-1) did, as the record says it.</summary>
    public static string SawWords(double wander, double square) =>
        wander < SawSquare && square >= 0.8 ? "sawn square" : wander < 0.5 ? "sawn, wandering off the line" : "sawn crooked, the end torn";

    /// <summary>
    /// Saws across the piece: at <c>c</c> (0-1 along it) when <c>flag</c> is set - exactly at the mark - or, an older
    /// record, after segment <c>i0</c>; <c>a</c> = how far the saw wandered from the line (0-1), <c>b</c> = how square
    /// the cut is (0-1). A wandering, ragged cut leaves torn, rough ends.
    /// </summary>
    public static CraftPiece SawCut(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null)
            return null;
        int at = op.flag ? p.InsertBoundary(op.c) : op.i0;
        CraftPiece q = CraftForging.Split(ws, p, at, op, SawWords(op.a, op.b));
        if (q == null)
            return null;
        double tear = 20 + 70 * CraftMath.Clamp01(op.a) + 40 * (1 - CraftMath.Clamp01(op.b));
        if (p.segments.Count > 0) p.segments[p.segments.Count - 1].rough = Math.Max(p.segments[p.segments.Count - 1].rough, tear);
        if (q.segments.Count > 0) q.segments[0].rough = Math.Max(q.segments[0].rough, tear);
        return q;
    }

    /// <summary>
    /// Rips the piece to a taper along a marked line: from <c>a</c> (0-1 along it) at <c>c</c> mm wide to <c>b</c> at
    /// <c>d</c> mm - both edges are sawn to the line, so no stretch between is left wider than the line there (nothing
    /// is added where it is already narrower). <c>e</c> = how far the saw wandered from the line (0-1): a wavy, rough edge.
    /// </summary>
    public static void RipCut(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double lo = CraftMath.Clamp01(Math.Min(op.a, op.b)), hi = CraftMath.Clamp01(Math.Max(op.a, op.b));
        double wander = CraftMath.Clamp01(op.e);
        int seed = CraftMath.Hash(p.id ?? "");
        for (int i = 0; i < p.segments.Count; i++)
        {
            double t = p.PositionOf(i);
            if (t < lo - 1e-6 || t > hi + 1e-6) continue;
            double line = Math.Max(3, RipLineAt(op.a, op.c, op.b, op.d, t));
            // a wandering saw leaves the edge wavy: here a little inside the line, there a little outside it
            double target = Math.Max(3, line + wander * 3 * (CraftMath.Noise(seed, i, 61) * 2 - 1));
            MetalSegment s = p.segments[i];
            if (target >= s.width) continue;
            s.width = target;
            s.rough = Math.Max(s.rough, 25 + 60 * wander);
        }
        p.wood.lastGrit = -1;
        p.Record(ws.Elapsed, CraftOpKind.RipCut, wander < SawSquare ? "Ripped to a taper along the line" : "Ripped to a taper, the saw wandering off the line", wander);
        ws.Raise(CraftEventKind.Cut, p.id, "", "");
    }

    // ------------------------------------------------------------------ bending
    /// <summary>
    /// Bends segments <c>i0</c>-<c>i1</c> over the form by <c>a</c> degrees in all, at a speed <c>b</c> (0 slow and
    /// steady - 1 forced at once), over a form of radius <c>c</c> mm (0: the radius the bent length and angle make - an
    /// older record), with the strap (<c>flag</c>) holding the outside fibres. Soft (steamed through and still hot) it
    /// takes the bend and keeps most of it; stiff, cross-grained, rushed or over too tight a form, the outside tears.
    /// </summary>
    public static void Bend(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        double angle = Math.Max(0, op.a), speed = CraftMath.Clamp01(op.b);
        double length = 0;
        for (int i = a; i <= b; i++) length += p.segments[i].length;
        double radius = op.c > 0 ? op.c : BentRadius(length, angle);
        double plastic = Plasticity(p);
        double tear = BendTearOver(ws.Rules, p, radius, angle, speed, op.flag, out double kept);
        int n = b - a + 1;
        for (int i = a; i <= b; i++)
            p.segments[i].bend += kept / n;
        if (tear > 0)
        {
            p.wood.split = Math.Min(1, p.wood.split + Math.Min(1, tear * 0.25));
            p.AddDefect(CraftDefectKind.Crack, (a + b) / 2, Math.Min(1, tear * 0.5), true, plastic < 0.3 ? "bent cold: the outside of the bend tore" : speed > 0.6 ? "bent too fast: the outside of the bend tore" : "the fibres on the outside of the bend tore");
        }
        p.Record(ws.Elapsed, CraftOpKind.BendWood, tear > 0 ? $"Bent {kept:0}°, and it cracked" : $"Bent {kept:0}° over the form", kept);
    }

    /// <summary>The radius (mm) a bend of <paramref name="angle"/> degrees over <paramref name="length"/> mm has.</summary>
    public static double BentRadius(double length, double angle) => Math.Max(1, length) / Math.Max(1e-3, Math.Max(0, angle) * Math.PI / 180);

    /// <summary>
    /// How hard a bend over a form of <paramref name="radius"/> mm strains the outside fibres against what they can take
    /// now (1 = the limit: past it they tear). The strain is the thickness over the radius - the same for any angle; what
    /// the fibres take grows with the steaming (while it is hot), the wood's flexibility and the strap, and shrinks for
    /// cross grain, checks and haste: dry wood takes about 1:80, steamed through about 1:25, and 1:10 with the strap.
    /// </summary>
    public static double BendStrain(CraftRules rules, CraftPiece p, double radius, double speed, bool strap)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return 0;
        double plastic = Plasticity(p);
        double flex = rules.Wood(p.wood.wood)?.flexibility ?? 0.4;
        double ratio = Thickness(p) / Math.Max(1, radius);
        double tolerance = (0.012 + 0.045 * plastic) * (0.6 + 0.8 * flex) * (strap ? 2.5 : 1)
                           * (1 - 0.6 * CraftMath.Clamp01(p.wood.runout)) * (1 - 0.5 * CraftMath.Clamp01(p.wood.checks)) * (1 - 0.5 * CraftMath.Clamp01(speed));
        return ratio / Math.Max(1e-4, tolerance);
    }

    /// <summary>
    /// How far a bend of <paramref name="angle"/> degrees over a form of <paramref name="radius"/> mm goes past what the
    /// fibres take (0 = it takes it; above 0 the outside tears: <see cref="BendStrain"/> - 1), and how much of the bend it
    /// keeps after springing back (<see cref="Kept"/>).
    /// </summary>
    public static double BendTearOver(CraftRules rules, CraftPiece p, double radius, double angle, double speed, bool strap, out double kept)
    {
        kept = 0;
        if (p == null || p.kind != CraftPieceKind.Wood || angle <= 0)
            return 0;
        double tear = Math.Max(0, BendStrain(rules, p, radius, speed, strap) - 1);
        kept = Kept(angle, Plasticity(p), tear);
        return tear;
    }

    /// <summary>The bend over <paramref name="length"/> mm of the piece (an older record's way: its radius from the length and the angle).</summary>
    public static double BendTear(CraftRules rules, CraftPiece p, double length, double angle, double speed, bool strap, out double kept) =>
        BendTearOver(rules, p, BentRadius(length, angle), angle, speed, strap, out kept);

    /// <summary>What a bend of <paramref name="angle"/> degrees keeps after springing back: less the stiffer it was, and less again once it tore badly.</summary>
    public static double Kept(double angle, double plastic, double tear) => Math.Max(0, angle) * (0.45 + 0.5 * CraftMath.Clamp01(plastic)) * (tear > 1 ? 0.6 : 1);

    // ------------------------------------------------------------------ the drying stack
    /// <summary>
    /// How well stickers laid at <paramref name="stickers"/> (each 0-1 along the board) hold it to dry flat (0-1), as a
    /// stack is laid: one under each end and the rest evenly between - enough of them (four or more), even gaps between
    /// them, and an end sticker within <see cref="StickerOverhang"/> of each end (further in, the end droops off it).
    /// </summary>
    public static double StickerEvenness(IReadOnlyList<float> stickers)
    {
        if (stickers == null || stickers.Count < 2)
            return 0;
        var xs = new List<double>();
        foreach (float s in stickers) xs.Add(CraftMath.Clamp01(s));
        xs.Sort();
        // the gaps between the stickers: as even as the stack is
        double mean = (xs[xs.Count - 1] - xs[0]) / (xs.Count - 1), v = 0;
        for (int i = 1; i < xs.Count; i++) v += Math.Pow(xs[i] - xs[i - 1] - mean, 2);
        double cv = Math.Sqrt(v / (xs.Count - 1)) / Math.Max(1e-3, mean);
        // the ends held up: the worse end's overhang past the sticker under it
        double overhang = Math.Max(xs[0], 1 - xs[xs.Count - 1]);
        double ends = CraftMath.Clamp01(1 - (overhang - StickerOverhang) / 0.1);
        double enough = CraftMath.Clamp01((xs.Count - 1) / 3.0);
        return CraftMath.Clamp01(1 - cv * 1.5) * ends * enough;
    }

    /// <summary>Stacks it to season: <c>a</c> = how evenly the stickers were laid, <c>b</c> = ends sealed, <c>c</c> = the cover (shade) as it was then.</summary>
    public static void Stack(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        p.location = CraftLocation.Stack;
        p.wood.stackEven = CraftMath.Clamp01(op.a);
        p.wood.endsSealed = Math.Max(p.wood.endsSealed, CraftMath.Clamp01(op.b));
        p.wood.shade = CraftMath.Clamp01(op.c);
        p.Record(ws.Elapsed, CraftOpKind.StackWood, "Stacked to season", p.wood.moisture);
    }

    // ------------------------------------------------------------------ sanding and finishing
    /// <summary>The smoothest a paper of <paramref name="grit"/> (0 coarse - 1 fine) leaves wood, stroked <paramref name="along"/> the grain (1) or across it (0).</summary>
    public static double SandFloor(double grit, double along = 1) => 4 + 30 * (1 - CraftMath.Clamp01(grit)) + 25 * (1 - CraftMath.Clamp01(along));

    /// <summary>
    /// Is a surface of <paramref name="rough"/> too rough for a paper of <paramref name="grit"/>: scratches deeper than
    /// the paper a step coarser leaves, that this one hardly touches? (Never for the coarsest papers.)
    /// </summary>
    public static bool SkipsGrit(double rough, double grit) => grit > CoarsestGrit + 1e-4 && rough > SandFloor(grit - GritStep) + GritReach;

    /// <summary>Does a paper of <paramref name="grit"/> skip a grit anywhere on segments <paramref name="from"/>-<paramref name="to"/>?</summary>
    public static bool SkipsGrit(CraftPiece p, int from, int to, double grit)
    {
        if (p == null) return false;
        int a = Math.Max(0, Math.Min(from, to)), b = Math.Min(p.segments.Count - 1, Math.Max(from, to));
        for (int i = a; i <= b; i++)
            if (SkipsGrit(p.segments[i].rough, grit)) return true;
        return false;
    }

    /// <summary>
    /// Sands segments <c>i0</c>-<c>i1</c>: <c>a</c> = the grit (0 coarse - 1 fine), <c>b</c> = strokes' worth,
    /// <c>c</c> = how much along the grain (1) rather than across it (0, it leaves scratches). Through the grits in order:
    /// each paper clears only the scratches of the paper before it - where the surface is rougher than that (a grit
    /// skipped, a fresh cut) a fine paper hardly bites and the deep scratches stay under it.
    /// </summary>
    public static void Sand(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        double grit = CraftMath.Clamp01(op.a), work = Math.Max(0, op.b), along = CraftMath.Clamp01(op.c);
        double floor = SandFloor(grit, along);
        // Sharp tools (and paper) cut faster.
        work *= ws.ToolQuality;
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        bool skipped = false;
        for (int i = a; i <= b; i++)
        {
            MetalSegment s = p.segments[i];
            double rate = 1;
            if (SkipsGrit(s.rough, grit)) { rate = SkippedRate; skipped = true; }
            s.rough = floor + (s.rough - floor) * Math.Exp(-work * rate * (0.15 + 0.3 * (1 - grit)));
            if (s.rough < floor) s.rough += (floor - s.rough) * 0.3;
        }
        // Sanding knocks down the nibs of a dry coat (and cuts into a wet one).
        if (p.wood.finish > 0)
        {
            if (p.wood.finishWet > 0.3) p.wood.finishEven *= 0.85;
            else p.wood.finishEven = Math.Min(1, p.wood.finishEven + 0.1 * along * grit);
            p.wood.finish = Math.Max(0, p.wood.finish - 0.03 * work * (1 - grit));
        }
        p.wood.lastGrit = grit; // the last paper used (what the window names)
        if (skipped) ws.Raise(CraftEventKind.Note, p.id, "skipped_grit", "Deep scratches from a coarser paper (or the knife) stay under this one: go back a grit.");
        p.Record(ws.Elapsed, CraftOpKind.SandWood, skipped ? "Sanded with too fine a paper: the deeper scratches stay" : along > 0.7 ? "Sanded with the grain" : "Sanded across the grain", grit);
    }

    /// <summary>
    /// The items a stroke of the brush uses (0-1 of one): its share of the piece's length, more for a thicker coat - one
    /// item lays <see cref="StrokesPerItem"/> full-length strokes of an ordinary coat.
    /// </summary>
    public static double BrushUse(double share, double thick) => CraftMath.Clamp01(share) * (0.4 + 0.6 * CraftMath.Clamp01(thick)) / 0.7 / StrokesPerItem;

    /// <summary>
    /// Brushes or rubs a finish on segments <c>i0</c>-<c>i1</c>: <c>id</c> = the varnish, oil or wax, <c>a</c> = how
    /// evenly it was laid (0-1), <c>b</c> = how thick (0-1). A varnish builds a film: it takes on dry, smooth wood; on
    /// damp wood it clouds and lifts, over a rough surface it is blotchy, laid too thick it runs, over a wet coat it
    /// wrinkles. An oil soaks into the grain (no film). Wax is rubbed into the surface: even, thin rubbing works it in; a
    /// thick, uneven smear does not. Each coat dries at its supply's pace.
    /// </summary>
    public static void Varnish(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        double even = CraftMath.Clamp01(op.a), thick = CraftMath.Clamp01(op.b), strength = s?.strength ?? 1;
        WoodState w = p.wood;
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        double share = p.segments.Count > 0 ? (b - a + 1) / (double)p.segments.Count : 1;
        double rough = 0;
        for (int i = a; i <= b; i++) rough += p.segments[i].rough;
        rough /= Math.Max(1, b - a + 1);
        double adhesion = w.moisture > DryEnough ? 0.35 : 1;
        double smooth = CraftMath.Smooth(60, 10, rough);
        double coat = 0.35 * (0.4 + 0.6 * thick) * adhesion * strength * share;
        // Laid on too thick it runs and sags (a film) or smears (wax).
        double runs = thick > 0.75 ? 1 - (thick - 0.75) * 1.6 : 1;
        string name = s?.Name.ToLowerInvariant() ?? "varnish";
        if (s != null && s.kind == CraftSupplyKind.Wax)
        {
            // Wax is rubbed into the surface, not built up as a film: it sheds water and wears away.
            w.wax = Math.Min(1, w.wax + coat * 1.4 * (0.5 + 0.5 * smooth) * (0.5 + 0.5 * even) * runs);
            for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat * 0.5 / Math.Max(0.05, share));
            p.Record(ws.Elapsed, CraftOpKind.Varnish, adhesion < 1 ? "Wax rubbed on damp wood: it smears" : thick > 0.75 ? "Wax laid on thick: it smears" : even < 0.5 ? "Wax rubbed in unevenly" : "Wax rubbed in", coat);
            return;
        }
        w.finishHours = s != null && s.dryingHours > 0 ? s.dryingHours : DefaultDryingHours;
        bool oil = s != null && s.kind == CraftSupplyKind.Oil;
        if (oil)
        {
            // Oil soaks into the grain (best into a smooth, dry surface): it keeps water out without a film, and it has to dry.
            w.oil = Math.Min(1, w.oil + coat * (0.6 + 0.4 * smooth));
            w.finishWet = Math.Max(w.finishWet, 0.6);
            for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat * 0.6 / Math.Max(0.05, share));
            p.Record(ws.Elapsed, CraftOpKind.Varnish, adhesion < 1 ? $"Oil on damp wood: little of the {name} soaks in" : $"Oiled with {name}", coat);
            return;
        }
        double quality = even * (0.5 + 0.5 * smooth) * (w.finishWet > 0.4 ? 0.7 : 1) * runs;
        double before = w.finish;
        w.finish = Math.Min(1, w.finish + coat);
        w.finishEven = before + coat > 1e-6 ? (w.finishEven * before + quality * coat) / (before + coat) : quality;
        w.finishWet = 1;
        // Where it went on (each segment's coat: what the eye sees).
        for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat / Math.Max(0.05, share));
        // A resin sealer builds a hard film (it wears well).
        if (s != null && s.id.IndexOf("resin", StringComparison.OrdinalIgnoreCase) >= 0) w.resin = Math.Min(1, w.resin + coat);
        if (thick > 0.75)
            for (int i = a; i <= b; i++) p.segments[i].rough = Math.Max(p.segments[i].rough, 20 + 60 * (thick - 0.75)); // runs and sags
        string how = adhesion < 1 ? "A coat on damp wood: it clouds and will lift" : thick > 0.75 ? "A thick coat: it runs" : even < 0.5 ? "A patchy coat" : "An even coat";
        p.Record(ws.Elapsed, CraftOpKind.Varnish, $"{how} of {name}", coat);
    }

    /// <summary>
    /// How well the finish keeps water out (0-1): a film (varnish, resin) by how even it is - the best - then oil soaked
    /// into the grain, and wax on top.
    /// </summary>
    public static double FinishProtection(WoodState w) =>
        CraftMath.Clamp01(w.finish * (0.4 + 0.6 * w.finishEven) + 0.35 * w.oil * (1 - w.finish) + 0.2 * w.wax * (1 - w.finish * 0.5));

    /// <summary>Is there a finish on it, as the look sees one: a coat of varnish or resin, oil soaked into its grain, or a wax sheen?</summary>
    public static bool Sealed(WoodState w) => w != null && (w.finish > 0.1 || w.oil > 0.2 || w.wax > 0.3);

    /// <summary>How well the finish stands wear and weather (0-1): a hard resin or varnish film best, wax least.</summary>
    public static double FinishWear(WoodState w) => CraftMath.Clamp01(w.finish * (0.5 + 0.5 * w.finishEven) * (0.7 + 0.3 * w.resin) + 0.15 * w.oil + 0.05 * w.wax);

    // ------------------------------------------------------------------ logs
    /// <summary>The depth (0-1) past which the drawknife gouges this log: green wood peels deeper.</summary>
    public static double GougeDepth(CraftPiece p) => p != null && p.wood.moisture > 0.3 ? GougeGreen : GougeDry;

    /// <summary>
    /// Takes the bark off segments <c>i0</c>-<c>i1</c> with the drawknife: <c>a</c> = how deep the knife bit (0 skimming
    /// the outer bark - 1 deep into the wood: <see cref="PeelDepth"/> lifts it, <see cref="GougeDepth"/> gouges),
    /// <c>b</c> = how steady the stroke was (0-1), <c>c</c> = how much of the way round the log the stroke went (one pull
    /// on a face: <see cref="DebarkPerStroke"/>). Green wood (sap up) peels clean; a dry log's bark is tight and needs
    /// digging (gouges). Too deep gouges the wood itself: it loses thickness and is rough there.
    /// </summary>
    public static void Debark(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double depth = CraftMath.Clamp01(op.a), steady = CraftMath.Clamp01(op.b), round = CraftMath.Clamp01(op.c);
        double tq = ws.ToolQuality;
        bool green = p.wood.moisture > 0.3;
        double peel = (green ? 1.25 : 0.65) * Math.Min(1.2, tq);
        // Too shallow takes only the outer bark; at the cambium it all lifts; deeper still cuts into the wood.
        double take = depth < PeelDepth ? depth / PeelDepth * 0.5 : 1;
        double gouge = Math.Max(0, depth - GougeDepth(p)) * 1.8 * (1.2 - 0.5 * steady) / Math.Max(0.5, tq);
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        double gouged = 0;
        for (int i = a; i <= b; i++)
        {
            MetalSegment s = p.segments[i];
            s.bark = Math.Max(0, s.bark - round * take * peel);
            if (gouge > 0)
            {
                double g = gouge * round;
                s.width = Math.Max(5, s.width - g * 6);
                s.thickness = Math.Max(5, s.thickness - g * 6);
                s.rough = Math.Max(s.rough, 40 + 80 * g);
                gouged += g;
            }
            if (steady < 0.4) s.rough = Math.Max(s.rough, 30 + 40 * (0.4 - steady)); // chatter marks
        }
        if (b >= a) p.wood.gouged = CraftMath.Clamp01(p.wood.gouged + gouged / (b - a + 1) * 0.5);
        if (gouge > 0.1) ws.Raise(CraftEventKind.Note, p.id, "gouged", "The drawknife bites into the wood under the bark.");
        p.wood.bark = BarkLeft(p);
        string how = gouge > 0.1 ? "Debarked, the drawknife gouging into the wood" : depth < PeelDepth ? "Shaved the outer bark: the inner bark is still on" : green ? "Peeled the bark clean" : "Worked the tight bark off";
        p.Record(ws.Elapsed, CraftOpKind.DebarkWood, how, p.wood.bark);
        if (p.wood.bark < 0.05) ws.Raise(CraftEventKind.Note, p.id, "debarked", "The last of the bark comes away.");
    }

    /// <summary>
    /// How true to the rays a wedge's line really runs (0 across - 1 along) when it was set <paramref name="along"/> in
    /// wood that cleaves <paramref name="cleave"/>: interlocked grain (elm) will not follow even a good line.
    /// </summary>
    public static double SplitAlongOf(double along, double cleave) => CraftMath.Clamp01(along) * (1 - 0.5 * Math.Max(0, 0.7 - CraftMath.Clamp01(cleave)));

    /// <summary>What a split <paramref name="off"/> the heart, <paramref name="along"/> the rays (as <see cref="SplitAlongOf"/> gives it) did, in words.</summary>
    public static string SplitWords(double off, double along) =>
        off < SplitEven && along > SplitAlong ? "Split cleanly through the heart" : off >= SplitEven ? "Split off the heart: the halves are uneven" : "Split, the line twisting across the grain";

    /// <summary>The share of a split's larger half, for a wedge set <paramref name="off"/> the heart (0-1).</summary>
    public static double SplitShareOf(double off) => 0.5 + SplitShare * CraftMath.Clamp01(off);

    /// <summary>
    /// Splits a log (or a large half) with wedges and the maul: <c>a</c> = how far off the heart the wedge was set
    /// (0 through the pith - 1 at the bark: how unevenly its line divides the end), <c>b</c> = how true the line followed
    /// a check or ray (0 across - 1 along), <c>c</c> = how square and solid the blows landed (0-1), <c>i0</c> = blows it
    /// took. It parts the larger cross dimension. Through the heart and along the rays it splits straight into two even
    /// halves whose grain follows them; off the heart the halves are uneven, and a line across the rays - or wood with
    /// interlocked grain - twists out and leaves runout and splinters.
    /// </summary>
    public static void SplitLog(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double off = CraftMath.Clamp01(op.a), solid = CraftMath.Clamp01(op.c);
        double along = SplitAlongOf(op.b, Cleave(ws.Rules, p));
        int blows = Math.Max(1, op.i0);
        bool thick = HalvesThickness(p);
        CraftPiece q = ws.Create(op, CraftPieceKind.Wood, p.Derived("split"));
        string id = q.id;
        CraftPiece copy = p.Clone();
        q.wood = copy.wood;
        q.segments = copy.segments;
        q.record = copy.record;
        q.tags = copy.tags;
        q.location = CraftLocation.Air;
        q.id = id;
        // Off the heart the halves are uneven; the smaller one is the far side of the wedge.
        double share = SplitShareOf(off);
        double runout = CraftMath.Clamp01(p.wood.runout * (0.5 + 0.5 * (1 - along)) + (1 - along) * 0.5 + off * 0.1);
        bool first = true;
        foreach (CraftPiece half in new[] { p, q })
        {
            double f = first ? share : 1 - share;
            first = false;
            half.wood.log = false;
            half.wood.runout = runout;
            half.wood.lastGrit = -1;
            foreach (MetalSegment s in half.segments)
            {
                if (thick) s.thickness = Math.Max(6, s.thickness * f - 1);
                else s.width = Math.Max(6, s.width * f - 1);
                s.rough = Math.Max(s.rough, 30 + 50 * (1 - along) + 30 * (1 - solid));
                s.bark *= 0.5; // the split faces are clean wood
            }
            half.wood.bark = BarkLeft(half);
            // Glancing, uneven blows (and many of them) bruise and check the ends.
            if (solid < 0.5 || blows > 8)
                half.wood.checks = Math.Min(1, half.wood.checks + (0.5 - Math.Min(0.5, solid)) * 0.3 + Math.Max(0, blows - 8) * 0.01);
        }
        // A line across the rays twists out: the halves end in splinters for part of their length.
        if (along < TwistsOut)
        {
            int n = q.segments.Count;
            int bad = Math.Max(1, (int)Math.Round(n * (TwistsOut - along)));
            for (int i = n - bad; i < n; i++)
            {
                MetalSegment s = q.segments[i];
                if (thick) s.thickness = Math.Max(6, s.thickness * 0.6); else s.width = Math.Max(6, s.width * 0.6);
            }
            q.AddDefect(CraftDefectKind.Crack, n - 1, TwistsOut - along, true, "the split twisted out across the grain");
        }
        string how = SplitWords(off, along);
        p.Record(ws.Elapsed, CraftOpKind.SplitLog, how, off);
        q.Record(ws.Elapsed, CraftOpKind.SplitLog, how, off);
        ws.Raise(CraftEventKind.Cut, p.id, q.id, how + ".");
    }

    // ------------------------------------------------------------------ what a part is fitted to
    /// <summary>
    /// The slot (mm) a wooden part fitted on a tang needs - a grip on a blade's tang: the tang of the design's blade where
    /// one is in the workshop (<paramref name="measured"/>), else the tang the design asks for. False when its design
    /// fits it on no tang.
    /// </summary>
    public static bool TangSlot(CraftWorkshop ws, CraftPiece wood, out double size, out bool measured)
    {
        size = 0;
        measured = false;
        if (ws == null || wood == null || string.IsNullOrEmpty(wood.intentDesign))
            return false;
        CraftDesign d = ws.Rules.Design(wood.intentDesign);
        if (d == null)
            return false;
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.kind != CraftJointKind.Fit) continue;
            string other = j.roleB == wood.intentRole ? j.roleA : j.roleA == wood.intentRole ? j.roleB : null;
            CraftComponentSpec blade = other != null ? d.Component(other) : null;
            if (blade == null || blade.kind != CraftPieceKind.Metal || blade.shape == null || blade.shape.profile == null || blade.shape.profile.Count == 0)
                continue;
            CraftComponentSpec mine = d.Component(wood.intentRole);
            double where = mine != null && mine.function == CraftComponentFunction.Guard ? 1 : mine != null && mine.function == CraftComponentFunction.Counterweight ? 0 : 0.5;
            foreach (CraftPiece m in ws.Pieces)
                if (m.kind == CraftPieceKind.Metal && m.intentDesign == d.id && m.intentRole == other && m.segments.Count > 0)
                {
                    size = CraftAssembly.TangSize(blade.shape, m, where, out _);
                    measured = true;
                    return size > 0;
                }
            float tangTo = blade.shape.tangTo > 0 ? blade.shape.tangTo : 0.15f;
            blade.shape.At((float)(where * tangTo), out float bw, out float bt);
            size = Math.Max(bw, bt) * 0.75 + Math.Min(bw, bt) * 0.25;
            return size > 0;
        }
        return false;
    }

    /// <summary>
    /// The stretches the next turn of the bit bores into <paramref name="p"/>, going in from <paramref name="end"/> (0 the
    /// near end, 1 the far end): the ones already bored from that end and one more - a stretch deeper each turn - up to
    /// three quarters of its length. Read from the piece itself, so the window and the hands at the bench go on from
    /// wherever the other left the slot; <paramref name="ahead"/> turns sent and not yet answered reach that many stretches
    /// further (a turn on its way has not bored its stretch yet). False for a piece with no stretches.
    /// </summary>
    public static bool BoreSpan(CraftPiece p, int end, out int from, out int to, int ahead = 0)
    {
        from = to = -1;
        int n = p != null ? p.segments.Count : 0;
        if (n == 0)
            return false;
        int most = Math.Max(1, n * 3 / 4), bored = 0;
        for (int k = 0; k < most; k++)
        {
            if (p.segments[end == 1 ? n - 1 - k : k].hole <= 0) break;
            bored++;
        }
        int reach = Math.Min(most, bored + 1 + Math.Max(0, ahead));
        from = end == 1 ? n - reach : 0;
        to = end == 1 ? n - 1 : reach - 1;
        return true;
    }

    /// <summary>
    /// The eye (mm) a haft's end goes into - an axe's, a hammer's, a spear's head: the head of the design where one is in
    /// the workshop (<paramref name="measured"/>), else the eye the design asks for. False when its design puts no head
    /// on it. Its last stretch is the end that goes in (see <see cref="CraftAssembly.FitGap"/>).
    /// </summary>
    public static bool HeadEye(CraftWorkshop ws, CraftPiece wood, out double eye, out bool measured)
    {
        eye = 0;
        measured = false;
        if (ws == null || wood == null || string.IsNullOrEmpty(wood.intentDesign))
            return false;
        CraftDesign d = ws.Rules.Design(wood.intentDesign);
        if (d == null)
            return false;
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.kind != CraftJointKind.Wedge && j.kind != CraftJointKind.Socket) continue;
            string other = j.roleB == wood.intentRole ? j.roleA : j.roleA == wood.intentRole ? j.roleB : null;
            CraftComponentSpec head = other != null ? d.Component(other) : null;
            if (head == null || head.kind != CraftPieceKind.Metal) continue;
            foreach (CraftPiece m in ws.Pieces)
                if (m.kind == CraftPieceKind.Metal && m.intentDesign == d.id && m.intentRole == other && CraftAssembly.Eye(m) > 0)
                {
                    eye = CraftAssembly.Eye(m);
                    measured = true;
                    return true;
                }
            if (head.shape == null || !head.shape.eye || head.shape.eyeDiameter <= 0) continue;
            eye = head.shape.eyeDiameter;
            return true;
        }
        return false;
    }
}

/// <summary>
/// A saw in the hands, worked back and forth along its stroke while it is held on its line: the woodshop's saw across a
/// board or along a taper, the bench saw that cuts a blank to length. Feed it the hand's position each sample: how far
/// it has strayed from where it was pressed (<c>off</c>, pixels - the blade starts on the line wherever the hand first
/// pressed, and a press after the saw was lifted goes on from where the kerf ran), and where it is along the stroke
/// (pixels). A stroke counts by its travel (a short jiggle does little), the rhythm evens the work, and the tremble of
/// the hands - worse in short, snatched strokes - moves the blade off the line.
/// </summary>
public sealed class CraftSawStroke
{
    /// <summary>Pixels of travel that make a full stroke; a stroke counts for its travel over this, up to one and a half.</summary>
    public const double StrokeTravel = 40;
    /// <summary>The least travel (pixels) that is a stroke at all.</summary>
    public const double MinTravel = 8;
    /// <summary>Pixels off the line that are a crooked cut (wander 1).</summary>
    public const double FullWander = 14;
    /// <summary>Pixels of drift between the start and the end of the cut that are fully out of square.</summary>
    public const double SquareDrift = 18;
    /// <summary>Strokes (of full travel, in rhythm) a cut through wood <paramref name="mm"/> thick takes (a rip along its thickness).</summary>
    public static double StrokesFor(double mm) => 6 + Math.Max(0, mm) / 5;
    /// <summary>
    /// Strokes a cut across a section <paramref name="width"/> by <paramref name="thickness"/> mm takes: by the section's
    /// size (the two's geometric mean) - the kerf has the whole width to cross, so a wide board takes longer than a stick,
    /// and a square section as many as a rip through its side (<see cref="StrokesFor(double)"/> of the side: the 40 mm
    /// ash stave 14, not 40).
    /// </summary>
    public static double StrokesFor(double width, double thickness) => StrokesFor(Math.Sqrt(Math.Max(0, width) * Math.Max(0, thickness)));

    private readonly List<double> offs = new List<double>();
    private readonly List<double> gaps = new List<double>();
    private double needed = 10, done, lastAlong = double.NaN, runFrom = double.NaN, runFar = double.NaN, lastTurn = double.NaN, runStart = double.NaN;
    private int dir, strokes;
    private double wanderSum;
    private int wanderN;
    /// <summary>Where the kerf ran when the saw was last lifted (pixels off the line), and where the hand has the blade now (without the tremble).</summary>
    private double carry, lastHand;

    /// <summary>Is a cut under way (set on its line)?</summary>
    public bool Active { get; private set; }
    /// <summary>How far through (0-1).</summary>
    public double Progress => Math.Min(1, done / Math.Max(1e-6, needed));
    /// <summary>Strokes made (counted by their travel).</summary>
    public int Strokes => strokes;
    /// <summary>Where the blade is now against the line (pixels; + one way, - the other).</summary>
    public double Blade { get; private set; }

    /// <summary>0-1: strokes of an even length of time (0.5 until there are enough to tell).</summary>
    public double Rhythm
    {
        get
        {
            if (gaps.Count < 3) return 0.5;
            double mean = 0, v = 0;
            foreach (double g in gaps) mean += g;
            mean /= gaps.Count;
            foreach (double g in gaps) v += (g - mean) * (g - mean);
            v /= gaps.Count;
            return CraftMath.Clamp01(1 - Math.Sqrt(v) / Math.Max(0.05, mean) * 1.5);
        }
    }

    /// <summary>How far off the line the blade ran, on the whole (0 on it - 1 a crooked cut).</summary>
    public double Wander => CraftMath.Clamp01(wanderSum / Math.Max(1, wanderN) / FullWander);

    /// <summary>How square the cut is (1 square - 0 badly out): the drift of the blade between the first and the last third of the cut.</summary>
    public double Square
    {
        get
        {
            int n = offs.Count;
            if (n < 6) return 1;
            int third = n / 3;
            double a = 0, b = 0;
            for (int i = 0; i < third; i++) { a += offs[i]; b += offs[n - 1 - i]; }
            return 1 - CraftMath.Clamp01(Math.Abs(b - a) / third / SquareDrift);
        }
    }

    /// <summary>Sets the saw on its line for a cut <paramref name="strokesNeeded"/> full strokes long, going on from <paramref name="already"/> (0-1) done.</summary>
    public void Begin(double strokesNeeded, double already = 0)
    {
        needed = Math.Max(1, strokesNeeded);
        done = CraftMath.Clamp01(already) * needed;
        offs.Clear();
        gaps.Clear();
        wanderSum = 0;
        wanderN = 0;
        strokes = 0;
        dir = 0;
        lastAlong = runFrom = runFar = lastTurn = runStart = double.NaN;
        carry = lastHand = 0;
        Blade = 0;
        Active = true;
    }

    /// <summary>Lifts the saw: the cut stays as far as it went (<see cref="Progress"/>), the strokes are not counted on.</summary>
    public void Stop() => Active = false;

    /// <summary>
    /// Sets the saw back in its kerf for another press: the cut, its strokes and how true it ran so far go on - and the
    /// blade goes on from where the kerf ran (a press set back on the mark does not put a wandering cut back on the line).
    /// </summary>
    public void Resume()
    {
        Active = true;
        dir = 0;
        lastAlong = runFrom = runFar = lastTurn = runStart = double.NaN;
        carry = lastHand;
        Blade = carry;
    }

    /// <summary>
    /// The hand at <paramref name="off"/> pixels from where it pressed (across the line: the blade is that far from where
    /// the kerf ran at the press) and <paramref name="along"/> pixels along the stroke, at <paramref name="time"/> seconds
    /// (the sample's own time). The hands tremble the blade off its line, more in short, snatched strokes; each stroke, by
    /// its travel and the rhythm, takes the cut deeper.
    /// </summary>
    public void Add(double off, double along, double time, CraftHands hands)
    {
        if (!Active)
            return;
        if (double.IsNaN(lastAlong))
        {
            lastAlong = runFrom = runFar = along;
            runStart = time;
        }
        // Which way the stroke goes; a turn of more than a few pixels ends the stroke.
        double d = along - lastAlong;
        lastAlong = along;
        int s = Math.Abs(d) < 1e-6 ? 0 : d > 0 ? 1 : -1;
        if (dir == 0 && s != 0) dir = s;
        if (dir != 0 && (dir > 0 ? along > runFar : along < runFar)) runFar = along;
        if (dir != 0 && Math.Abs(runFar - along) >= 3 && (dir > 0 ? along < runFar : along > runFar))
        {
            Stroke(Math.Abs(runFar - runFrom), time, hands);
            runFrom = runFar;
            runFar = along;
            dir = -dir;
        }
        // Short, quick strokes shake the blade more than long, easy ones.
        double seconds = double.IsNaN(runStart) ? 0.5 : Math.Max(0.05, time - runStart);
        double snatched = CraftMath.Clamp01(1 - seconds / 0.35);
        double tremble = hands != null ? hands.Tremble(offs.Count + 71) * 60 * (1 + snatched) : 0;
        lastHand = carry + off; // a press goes on from where the kerf ran
        Blade = lastHand + tremble;
        offs.Add(Blade);
        wanderSum += Math.Abs(Blade);
        wanderN++;
    }

    private void Stroke(double travel, double time, CraftHands hands)
    {
        if (travel < MinTravel)
            return;
        strokes++;
        if (!double.IsNaN(lastTurn)) gaps.Add(time - lastTurn);
        lastTurn = time;
        runStart = time;
        done += Math.Min(1.5, travel / StrokeTravel) * (0.5 + 0.5 * Rhythm);
        hands?.Move(0.3);
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ the woodshop
    private bool WoodCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason)
    {
        reason = "";
        p = Piece(id);
        if (!Has(tool))
            return Refuse(missing, out reason, id);
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return Refuse("That is not wood.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        return true;
    }

    /// <summary>
    /// The cover over the drying stack (0 open sun - 1 the whole stack shaded), as it was last pulled; null when it never
    /// was here (each board then keeps the shade it was stacked under).
    /// </summary>
    public double? StackCover => Variables.TryGetValue(CraftWoodwork.CoverVariable, out double c) ? CraftMath.Clamp01(c) : (double?)null;

    /// <summary>Pulls the cover over the drying stack to <paramref name="cover"/> (0-1): every board in the stack dries under it from now.</summary>
    public bool SetStackCover(double cover, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.DryingStack))
            return Refuse("There is no drying stack here.", out reason);
        SetVariable(CraftWoodwork.CoverVariable, CraftMath.Clamp01(cover), 0.004);
        return true;
    }

    /// <summary>
    /// Rives a piece of wood in two along its grain (see <see cref="CraftWoodwork.Rive"/>), the froe set at
    /// <paramref name="line"/> (0-1 across it; the middle unless given). Returns the other half's id.
    /// </summary>
    public string Rive(string id, double stray, double steadiness, out string reason, double line = 0.5)
    {
        if (!WoodCheck(id, CraftTools.ChoppingBlock, "There is no froe and block here.", out CraftPiece p, out reason))
            return null;
        if (!CraftWoodwork.RivesWithFroe(p, out string why))
        {
            Refuse(why, out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.Rive, piece = id, a = (float)CraftMath.Clamp01(stray), b = (float)CraftMath.Clamp01(steadiness), c = (float)CraftMath.Clamp(line, 0.05, 0.95) };
        Record(op);
        return op.other;
    }

    /// <summary>Takes bark off segments <paramref name="from"/>-<paramref name="to"/> with the drawknife (see <see cref="CraftWoodwork.Debark"/>).</summary>
    public bool DebarkWood(string id, int from, int to, double depth, double steadiness, double round, out string reason)
    {
        if (!WoodCheck(id, CraftTools.ShavingHorse, "There is no shaving horse and drawknife here.", out CraftPiece p, out reason))
            return false;
        if (CraftWoodwork.BarkLeft(p) <= 0.001)
            return Refuse("There is no bark on it.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.DebarkWood, piece = id, i0 = from, i1 = to, a = (float)CraftMath.Clamp01(depth), b = (float)CraftMath.Clamp01(steadiness), c = (float)CraftMath.Clamp01(round) });
        return true;
    }

    /// <summary>Splits a log in two with wedges and the maul (see <see cref="CraftWoodwork.SplitLog"/>). Returns the other half's id.</summary>
    public string SplitLog(string id, double offHeart, double alongRays, double solid, int blows, out string reason)
    {
        if (!WoodCheck(id, CraftTools.ChoppingBlock, "There are no wedges and maul here.", out CraftPiece p, out reason))
            return null;
        if (!CraftWoodwork.SplitsWithWedges(p, out string why))
        {
            Refuse(why, out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.SplitLog, piece = id, a = (float)CraftMath.Clamp01(offHeart), b = (float)CraftMath.Clamp01(alongRays), c = (float)CraftMath.Clamp01(solid), i0 = Math.Max(1, Math.Min(99, blows)) };
        Record(op);
        return op.other;
    }

    /// <summary>Saws a piece of wood across exactly at <paramref name="position"/> (0-1). Returns the far part's id.</summary>
    public string SawCut(string id, double position, double wander, double square, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Sawhorse) && !Has(CraftTools.Files))
        {
            Refuse("There is no saw here.", out reason, id);
            return null;
        }
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count < 2)
        {
            Refuse("That cannot be sawn.", out reason, id);
            return null;
        }
        if (p.location != CraftLocation.Air)
        {
            Refuse("Take it out first.", out reason, id);
            return null;
        }
        if (!CraftWoodwork.CanSawAt(p, position, out string why))
        {
            Refuse(why, out reason, id);
            return null;
        }
        int seg = Math.Min(p.segments.Count - 2, p.SegmentAt(CraftMath.Clamp01(position)));
        var op = new CraftOperation
        {
            kind = CraftOpKind.SawCut, piece = id, i0 = seg, a = (float)CraftMath.Clamp01(wander), b = (float)CraftMath.Clamp01(square),
            flag = true, c = (float)CraftMath.Clamp01(position), // the cut is made at the mark itself
        };
        Record(op);
        return op.other;
    }

    /// <summary>
    /// Rips a piece of wood to a taper along a line from <paramref name="from"/> (0-1 along it) at <paramref name="widthFrom"/>
    /// mm wide to <paramref name="to"/> at <paramref name="widthTo"/> mm (see <see cref="CraftWoodwork.RipCut"/>).
    /// </summary>
    public bool RipCut(string id, double from, double to, double widthFrom, double widthTo, double wander, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Sawhorse) && !Has(CraftTools.Files))
            return Refuse("There is no saw here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return Refuse("That cannot be sawn.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        if (!CraftWoodwork.RipNarrows(p, from, to, widthFrom, widthTo, out string why))
            return Refuse(why, out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.RipCut, piece = id, a = (float)CraftMath.Clamp01(from), b = (float)CraftMath.Clamp01(to),
            c = (float)Math.Max(3, widthFrom), d = (float)Math.Max(3, widthTo), e = (float)CraftMath.Clamp01(wander),
        });
        return true;
    }

    /// <summary>
    /// Bends wood over the form (see <see cref="CraftWoodwork.Bend"/>): <paramref name="radius"/> = the form's (mm; 0 = the
    /// radius the bent stretch and angle make).
    /// </summary>
    public bool BendWood(string id, int from, int to, double degrees, double speed, bool strap, out string reason, double radius = 0)
    {
        if (!WoodCheck(id, CraftTools.SteamBox, "There are no bending forms here.", out CraftPiece p, out reason))
            return false;
        if (degrees <= 0)
            return Refuse("It was not bent.", out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.BendWood, piece = id, i0 = from, i1 = to, a = (float)Math.Min(180, degrees), b = (float)CraftMath.Clamp01(speed), flag = strap,
            c = radius > 0 ? (float)CraftMath.Clamp(radius, 20, 2000) : 0f,
        });
        return true;
    }

    /// <summary>Stacks wood in the drying stack to season (see <see cref="CraftWoodwork.Stack"/>).</summary>
    public bool StackWood(string id, double stickers, double sealedEnds, double shade, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.DryingStack))
            return Refuse("There is no drying stack here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Wood)
            return Refuse("Only wood seasons in the stack.", out reason, id);
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Stack)
            return Refuse("Take it out first.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.StackWood, piece = id, a = (float)CraftMath.Clamp01(stickers), b = (float)CraftMath.Clamp01(sealedEnds), c = (float)CraftMath.Clamp01(shade) });
        return true;
    }

    /// <summary>Sands wood (see <see cref="CraftWoodwork.Sand"/>).</summary>
    public bool SandWood(string id, int from, int to, double grit, double strokes, double alongGrain, out string reason)
    {
        if (!WoodCheck(id, CraftTools.FinishingBench, "There is no sandpaper here.", out _, out reason))
            return false;
        if (strokes <= 0)
            return Refuse("The paper did not touch it.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.SandWood, piece = id, i0 = from, i1 = to, a = (float)CraftMath.Clamp01(grit), b = (float)Math.Min(20, strokes), c = (float)CraftMath.Clamp01(alongGrain) });
        return true;
    }

    /// <summary>Brushes a coat of varnish or oil, or rubs in wax, on wood (see <see cref="CraftWoodwork.Varnish"/>).</summary>
    public bool Varnish(string id, string supply, int from, int to, double evenness, double thickness, out string reason)
    {
        if (!WoodCheck(id, CraftTools.FinishingBench, "There is no finishing bench here.", out _, out reason))
            return false;
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (s == null || s.kind != CraftSupplyKind.Varnish && s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That is not a finish for wood.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Varnish, piece = id, id = s.id, i0 = from, i1 = to, a = (float)CraftMath.Clamp01(evenness), b = (float)CraftMath.Clamp01(thickness) });
        return true;
    }
}
