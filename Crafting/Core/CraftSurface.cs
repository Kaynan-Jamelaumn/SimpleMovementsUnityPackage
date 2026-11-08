using System;
using System.Collections.Generic;

/// <summary>
/// Work on the surface and the edge.
/// <list type="bullet">
/// <item><b>Descaling</b> (the wire brush, the scraper): takes the scale off where it is worked; what is left stays;
/// working on past the scale takes metal (a scraper more than a brush) and scratches the bare metal to the tool's own
/// finish. It never mends a crack. (The wheel grinds scale off too, with its heat.)</item>
/// <item><b>Grinding</b>: the wheel takes metal fast - bevels at the angle the blade is held, the flats, the spine, the
/// edge line, the tang - and heats where it works, a thin edge most: pressed too hard on a hardened edge it draws the
/// temper (the edge flashes straw, then blue, and softens). It throws sparks that tell the steel. The bevels are
/// planes: each grows at the angle held until it meets the other; ground on, the apex slides toward the face being
/// ground and the edge goes back. Ground too far, the blade goes narrow or thin.</item>
/// <item><b>Filing</b>: slower and precise, no heat, cuts only what is softer than the file (it skates on hardened
/// steel); fits tangs and eyes; rasps shape wood.</item>
/// <item><b>Polishing</b>: each grit brings the roughness toward its finish, slowly when grits are skipped (the deep
/// scratches stay). A bright surface shows the colours of heat and resists rust; a coating of oil or wax protects it
/// until abrasion or heat takes it off.</item>
/// <item><b>Sharpening</b>: strokes on the stone at an angle, a pressure, edge leading or trailing. Only an edge is
/// honed (a flat left along it is worn away first, slowly), and only where the stone reaches the apex - held at the
/// bevel's angle or steeper (lower, it rubs the bevel's shoulder). Each face is honed toward the apex; once a face meets
/// it, edge-leading strokes roll a burr over to the other; the apex gets as fine as the stone - if both faces were
/// honed, the angle held steady and the burr taken off with light trailing strokes or the strop. Hard steel keeps a
/// fine edge, a soft metal does not; a wobbling hand rounds it; heavy pressure on a hard thin edge chips it.</item>
/// </list>
/// </summary>
public static class CraftSurface
{
    /// <summary>
    /// Clay on segments <paramref name="from"/>-<paramref name="to"/>: one thickness, or one per segment (painted on, from
    /// <paramref name="from"/>): the coat as the brush left it - a stretch painted bare or wiped clean has none; stretches
    /// beyond the painted list keep what they had.
    /// </summary>
    public static void ApplyClay(CraftWorkshop ws, CraftPiece p, int from, int to, double thickness, IList<float> perSegment = null)
    {
        if (p == null)
            return;
        bool painted = perSegment != null && perSegment.Count > 0;
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
        {
            int k = i - from;
            if (painted && k >= perSegment.Count) break; // not in the coat: as it was
            double t = painted ? perSegment[k] : thickness;
            p.segments[i].clay = t <= 0.01 ? 0 : CraftMath.Clamp(t, 0, 6);
        }
        p.Record(ws.Elapsed, CraftOpKind.ApplyClay, painted ? "Clay painted on" : "Coated with clay", thickness);
    }

    private static IEnumerable<int> Range(CraftPiece p, int from, int to)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
            yield return i;
    }

    // ------------------------------------------------------------------ the rules (the window shows the same numbers)
    /// <summary>The steepest a bevel is ground (degrees a side): no steeper than the stone can be held, so whatever the wheel grinds the stone can hone.</summary>
    public const double MaxBevelAngle = 35;
    /// <summary>The lowest and the steepest the blade is held on the whetstone (degrees a side).</summary>
    public const double StoneAngleMin = 5, StoneAngleMax = MaxBevelAngle;
    /// <summary>The heat (°C) above which the wheel, the file and the whetstone can no longer work a piece (too hot to hold).</summary>
    public const double WheelTooHot = 400, FileTooHot = 350, StoneTooHot = 150;
    /// <summary>The least polish of bare metal fresh from an abrasive (the wheel's grey): finer scratches leave it brighter.</summary>
    public const double GroundPolish = 0.3;
    /// <summary>From this polish up a bare surface takes the colours of heat (a freshly ground one already does).</summary>
    public const double FilmPolish = GroundPolish;
    /// <summary>A grit clears scratches up to this many times its own finish at its pace; deeper ones (a grit skipped) come out slowly.</summary>
    public const double GritReach = 6;
    /// <summary>Oil and wax smoke off a surface hotter than this (°C), the faster the hotter.</summary>
    public const double CoatingBurns = 250;
    /// <summary>A burr this big (0-1) shows - the white line along the edge, the curl in the section - and is talked about.</summary>
    public const double BurrShows = 0.2;
    /// <summary>A trailing stroke starts to take the burr off below this pressure, and takes it off fastest from <see cref="BurrOffFull"/> down.</summary>
    public const double BurrOffBelow = 0.6, BurrOffFull = 0.2;
    /// <summary>An edge flat thinner than this (mm) counts as an edge the stone hones; a thicker one it must wear away first.</summary>
    public const double FlatGone = 0.15;
    /// <summary>A stretch has an edge ground on it when what is left at the edge is under this share of its thickness (the evaluator's own test).</summary>
    public const double EdgeGround = 0.6;
    /// <summary>The bevels have met when what is left of the edge is no thicker than this (mm): the grinding of that stretch is done.</summary>
    public const double EdgeMet = 0.3;
    /// <summary>The section (mm²) one stroke of a coarse stone wears off a flat edge at full pressure on soft metal (finer stones less, the strop nothing).</summary>
    public const double StoneBite = 0.06;
    /// <summary>The temper a blade's edge is best left at, and past which it is drawn soft (the evaluator's ideal and its tolerance).</summary>
    public const double IdealTemper = 0.22, DrawnSoft = 0.42;
    /// <summary>How long (s) the heat of the wheel lingers on an edge, for <see cref="TemperRunsAt"/>.</summary>
    public const double TemperSeconds = 5;

    /// <summary>The ends of the scale a surface's polish is read on (µm): rough from the hammer or the mould, and the mirror the finest grit (the rouge) leaves.</summary>
    public const double RoughFinish = 60, MirrorFinish = 0.2;

    /// <summary>
    /// The polish a bare surface of <paramref name="rough"/> µm shows (<see cref="GroundPolish"/> at the least): one rule
    /// for every abrasive. The eye reads scratches on a log scale - each grit finer looks brighter by about as much: the
    /// wheels' grey (0.3, 0.4), the emeries' smooth grey and bright (0.5, 0.7), the rouge's mirror (1).
    /// </summary>
    public static double PolishOf(double rough) =>
        Math.Max(GroundPolish, CraftMath.Clamp01(Math.Log10(RoughFinish / Math.Max(1e-3, rough)) / Math.Log10(RoughFinish / MirrorFinish)));

    /// <summary>How a bare surface's scratches read, roughest first (<see cref="FinishOf(double)"/>).</summary>
    public enum Finish { Rough, CoarseScratches, FineScratches, SmoothGrey, Bright, Mirror }

    /// <summary>
    /// How a bare surface of <paramref name="rough"/> µm reads - one ladder for the Grind area, the Sharpen area and what
    /// a piece looks like: rough (hammer or mould marks, 40 µm up), coarse scratches (a coarse wheel's, 15 up), fine
    /// scratches (the fine wheel's, 4 up), a smooth grey finish (the coarse emery's, 1.5 up), bright (the fine emery's,
    /// 0.5 up), a mirror (the rouge's).
    /// </summary>
    public static Finish FinishOf(double rough) =>
        rough >= 40 ? Finish.Rough : rough >= 15 ? Finish.CoarseScratches : rough >= 4 ? Finish.FineScratches : rough >= 1.5 ? Finish.SmoothGrey : rough >= 0.5 ? Finish.Bright : Finish.Mirror;

    /// <summary>
    /// How a bare surface reads from its scratches and its polish together: by its scratches (<see cref="FinishOf(double)"/>),
    /// or brighter where something brightened it past what its scratches give (a jeweller's buffing wheel) - its polish
    /// read on the same ladder (<see cref="PolishOf"/> of each step's roughest).
    /// </summary>
    public static Finish FinishOf(double rough, double polish)
    {
        Finish byRough = FinishOf(rough);
        Finish byPolish = polish > PolishOf(0.5) ? Finish.Mirror : polish > PolishOf(1.5) ? Finish.Bright : polish > PolishOf(4) ? Finish.SmoothGrey : Finish.Rough;
        return byPolish > byRough ? byPolish : byRough;
    }

    /// <summary><see cref="FinishOf(double)"/> in a few words ("coarse scratches", "a smooth grey finish").</summary>
    public static string FinishWords(double rough) => FinishOf(rough) switch
    {
        Finish.Rough => "rough, hammer- or mould-marked",
        Finish.CoarseScratches => "coarse scratches",
        Finish.FineScratches => "fine scratches",
        Finish.SmoothGrey => "a smooth grey finish",
        Finish.Bright => "bright",
        _ => "a mirror",
    };

    /// <summary>
    /// The scratches (µm) at which <paramref name="grit"/> has done what it can: within 1.5 × its finish - and within the
    /// reach of <paramref name="next"/>, the next finer grit to come (<see cref="GritReach"/> × its finish), so that one
    /// takes them at its pace and nothing reads as a grit skipped; never finer than 1.05 × its own finish, which it only
    /// nears.
    /// </summary>
    public static double GritDone(CraftAbrasive grit, CraftAbrasive next)
    {
        if (grit == null)
            return 0;
        double reach = next != null && next.finish < grit.finish ? next.finish * GritReach : double.PositiveInfinity;
        return Math.Max(1.05 * grit.finish, Math.Min(1.5 * grit.finish, reach));
    }

    /// <summary>The sharpness (0-1, <see cref="CraftEvaluator.Sharpness"/>) from which an edge reads as more than blunt, as keen, as razor-keen (<see cref="KeenStep"/>).</summary>
    public const double NotBlunt = 0.2, Keen = 0.5, RazorKeen = 0.8;

    /// <summary>
    /// How keen an edge of <paramref name="sharpness"/> reads: 0 blunt (light glints along it), 1 sharp enough to cut but
    /// not keen, 2 keen, 3 razor-keen - one ladder for the stone, the 3D stone, the item's notes, the guide (an edge is
    /// honed until it is keen) and what a piece looks like.
    /// </summary>
    public static int KeenStep(double sharpness) => sharpness >= RazorKeen ? 3 : sharpness >= Keen ? 2 : sharpness >= NotBlunt ? 1 : 0;

    /// <summary><see cref="KeenStep"/> in a word or two ("blunt", "sharp, not keen", "keen", "razor-keen").</summary>
    public static string KeenWords(double sharpness) => KeenStep(sharpness) switch { 3 => "razor-keen", 2 => "keen", 1 => "sharp, not keen", _ => "blunt" };

    /// <summary>How much of the burr a trailing stroke at <paramref name="pressure"/> takes off (0 at <see cref="BurrOffBelow"/> and above, 0.35 at <see cref="BurrOffFull"/> and below).</summary>
    public static double BurrOff(double pressure) => 0.35 * CraftMath.Clamp01((BurrOffBelow - pressure) / (BurrOffBelow - BurrOffFull));

    /// <summary>
    /// Would a dip in the tank to cool it quench the piece - can it still harden (<see cref="CraftThermal.Hardening"/>, in
    /// this workshop's tank)? The wheel's dip only cools what the wheel heated: a piece at a hardening heat is quenched at
    /// the tank, as a quench is done, or left to cool in the air.
    /// </summary>
    public static bool DipQuenches(CraftWorkshop ws, CraftPiece p) => ws != null && CraftThermal.Hardening(ws.Rules, p, ws.Quench.temperature);

    /// <summary>Why the wheel's dip is refused to a piece that can still harden (<see cref="DipQuenches"/>): where it is quenched (<paramref name="quenchAt"/>), or cooled.</summary>
    public static string DipWouldQuench(string quenchAt, bool ashes) =>
        $"It is still hot enough to harden: a dip would quench it - quench it {quenchAt}, or let it cool in the air{(ashes ? " (or in the ashes, to anneal it)" : "")}.";

    /// <summary>Does this stretch have an edge ground on it (<see cref="EdgeGround"/>)?</summary>
    public static bool HasEdge(MetalSegment s) => s != null && s.EdgeThickness < EdgeGround * s.thickness;

    /// <summary>Has any stretch from <paramref name="from"/> to <paramref name="to"/> an edge the stone can hone?</summary>
    public static bool HasEdge(CraftPiece p, int from, int to)
    {
        if (p == null)
            return false;
        foreach (int i in Range(p, from, to))
            if (HasEdge(p.segments[i]))
                return true;
        return false;
    }

    /// <summary>
    /// How much of the stone reaches the apex, the blade held at <paramref name="held"/> degrees on a face whose bevel is
    /// <paramref name="bevel"/>: all of it at the bevel's angle or steeper (a degree of slack), none 4° below it - lower,
    /// it only rubs the bevel's shoulder.
    /// </summary>
    public static double Contact(double held, double bevel) => bevel <= 0 || held >= bevel - 1 ? 1 : Math.Max(0, 1 - (bevel - 1 - held) / 3);

    /// <summary>The section (mm²) a stroke of <paramref name="stone"/> wears off a flat edge at full pressure on soft metal.</summary>
    public static double Bite(CraftAbrasive stone) => stone == null || IsStrop(stone) ? 0 : StoneBite * Math.Min(1, stone.finish / 8);

    /// <summary>
    /// Is <paramref name="stone"/> a strop (leather: it leaves an apex of 1.5 µm or finer)? It is drawn spine first, edge
    /// trailing: edge leading, the edge slices into the leather and hones nothing.
    /// </summary>
    public static bool IsStrop(CraftAbrasive stone) => stone != null && stone.apex <= 1.5;

    /// <summary>The hardness (HV) an abrasive meets working <paramref name="side"/> of a stretch: the edge's zone for the bevels and the edge line, the body's elsewhere.</summary>
    public static double WorkedHardness(CraftRules rules, CraftPiece p, MetalSegment seg, CraftSurfaceSide side)
    {
        if (p.kind == CraftPieceKind.Wood)
            return 30;
        bool edge = side == CraftSurfaceSide.LeftBevel || side == CraftSurfaceSide.RightBevel || side == CraftSurfaceSide.EdgeLine;
        return CraftMetallurgy.Hardness(rules, p.Profile(rules), seg, edge ? seg.edge : seg.body, seg.thickness);
    }

    /// <summary>
    /// The finest apex (µm) <paramref name="stone"/> brings this stretch to, the hand wobbling <paramref name="wobble"/>
    /// degrees: the stone's own apex, coarser for a wobbling hand and for a soft metal (it does not hold a fine edge).
    /// The stone works toward it from either side - a stone coarser than the edge only coarsens it.
    /// </summary>
    public static double SharpenFloor(CraftRules rules, CraftPiece p, MetalSegment seg, CraftAbrasive stone, double wobble)
    {
        double hard = CraftMetallurgy.Hardness(rules, p.Profile(rules), seg, seg.edge, Math.Max(0.2, seg.EdgeThickness));
        return stone.apex * (1 + Math.Max(0, wobble) / 3) * SoftFactor(hard);
    }

    /// <summary>How much coarser a metal of <paramref name="hardness"/> HV leaves the finest apex (1 from 600 HV up, 1.9 for the softest).</summary>
    public static double SoftFactor(double hardness) => 1 + 1.5 * Math.Max(0, 0.6 - hardness / 1000);

    /// <summary>What to do next on the stone (<see cref="NextHone"/>).</summary>
    public enum HoneStep
    {
        /// <summary>No edge ground there: nothing to hone.</summary>
        NoEdge,
        /// <summary>
        /// A flat thicker than the wheel leaves where the bevels meet (<see cref="EdgeMet"/>: the grind strip is not green
        /// there) still shows along the edge: the stone wears it only slowly - grind the bevels until they meet.
        /// </summary>
        WearFlat,
        /// <summary>
        /// The bevels have met (the grind strip is green: <see cref="EdgeMet"/>), but a hair of flat still shows - thicker
        /// than the stone hones (<see cref="FlatGone"/>): strokes on each face wear it away first, then hone the face.
        /// </summary>
        WearLastFlat,
        /// <summary>The face on the stone is not yet honed to the apex.</summary>
        HoneFace,
        /// <summary>The face on the stone is honed; the other is not.</summary>
        TurnFace,
        /// <summary>Both faces honed, a burr shows: light trailing strokes or the strop.</summary>
        TakeBurrOff,
        /// <summary>Both honed, no burr, the apex still well above what this stone reaches.</summary>
        KeepHoning,
        /// <summary>The edge is already finer than this stone leaves it: it would only coarsen it.</summary>
        StoneTooCoarse,
        /// <summary>As keen as this stone, these hands and this metal make it.</summary>
        AtBest,
    }

    /// <summary>
    /// What to do next on the stone, by the station's own thresholds: no edge, nothing to hone; a flat where the bevels
    /// have not met (<see cref="EdgeMet"/>, what the grind strip calls done), back to the wheel; the hair of flat the
    /// wheel leaves where they met, worn away by the strokes (<see cref="FlatGone"/>); the face on the stone honed until it
    /// meets the apex (hone 1), then the other face; the burr off once it shows (<see cref="BurrShows"/>); then on toward
    /// the floor the stone reaches (<see cref="SharpenFloor"/>, within 25 %) - unless that floor is coarser than the edge
    /// already is.
    /// </summary>
    public static HoneStep NextHone(double honeThis, double honeOther, double burr, double flat, bool hasEdge, double apex, double floor)
    {
        if (!hasEdge) return HoneStep.NoEdge;
        if (flat > EdgeMet) return HoneStep.WearFlat;
        if (flat > FlatGone) return HoneStep.WearLastFlat;
        if (honeThis < 1 - 1e-9) return HoneStep.HoneFace;
        if (honeOther < 1 - 1e-9) return HoneStep.TurnFace;
        if (burr > BurrShows) return HoneStep.TakeBurrOff;
        if (floor > apex * 1.15) return HoneStep.StoneTooCoarse;
        if (apex > floor * 1.25) return HoneStep.KeepHoning;
        return HoneStep.AtBest;
    }

    /// <summary>
    /// The heat (°C) at which a few seconds (<see cref="TemperSeconds"/>) on the wheel start to draw this zone's temper -
    /// past where it was tempered, or, straight from the quench, past a blade's temper (<see cref="IdealTemper"/>):
    /// infinite for a zone with too little martensite to soften (unhardened steel, bronze, iron).
    /// </summary>
    public static double TemperRunsAt(CraftRules rules, CraftZone z)
    {
        if (z == null || z.mart < 0.3)
            return double.PositiveInfinity;
        double scale = rules?.Settings != null ? rules.Settings.temperTimeScale : 30;
        double want = Math.Max(CraftMetallurgy.TemperFraction(z.temperP), IdealTemper) + 0.04;
        if (want >= 1)
            return double.PositiveInfinity;
        double lo = 120, hi = 727;
        if (CraftMetallurgy.TemperFraction(CraftMetallurgy.Temper(z.temperP, hi, TemperSeconds, scale)) < want)
            return hi;
        for (int k = 0; k < 24; k++)
        {
            double mid = (lo + hi) * 0.5;
            if (CraftMetallurgy.TemperFraction(CraftMetallurgy.Temper(z.temperP, mid, TemperSeconds, scale)) >= want) hi = mid;
            else lo = mid;
        }
        return hi;
    }

    /// <summary>
    /// How far a stretch is from what the design wants there, judged as the evaluator judges it
    /// (<see cref="Judge"/>).
    /// </summary>
    public enum Progress
    {
        /// <summary>The piece has no design (or none here): nothing to judge against.</summary>
        NoTarget,
        /// <summary>
        /// Still to grind (the share done in <see cref="Judgement.done"/>) - an edge whose bevels have not met is, however
        /// narrow or thin the stretch already is (<see cref="Judgement.under"/> says that apart).
        /// </summary>
        ToGrind,
        /// <summary>At the design: both bevels met at its angle, no wider or thicker than it (an edge), or the width and thickness within 4 %.</summary>
        Done,
        /// <summary>Off the design and not by grinding on: the bevels met at another angle, or a little narrower or thinner than it wants.</summary>
        Off,
        /// <summary>More than 12 % narrower or thinner than the design (forged so, or ground away), with nothing left to grind there: grinding cannot put it back.</summary>
        TooFar,
    }

    /// <summary>A stretch judged against the design (<see cref="Judge"/>).</summary>
    public struct Judgement
    {
        public Progress progress;
        /// <summary>How much of the way it is (0-1): the shallower bevel's depth over half the thickness, or how close the shape is.</summary>
        public double done;
        /// <summary>Each face's bevel against the design's angle: its share of the depth it needs (0-1), and its angle (degrees).</summary>
        public double doneL, doneR, angleL, angleR;
        /// <summary>Is each face's angle within 2° of the angle wanted (<see cref="want"/>)?</summary>
        public bool angleOkL, angleOkR;
        /// <summary>The angle a side (degrees) the bevels are judged against here: the design's, as its section carries it (<see cref="EdgeHalfAngle"/>).</summary>
        public double want;
        /// <summary>Width and thickness against the design: + wider/thicker, - narrower/thinner (shares).</summary>
        public double widthOff, thicknessOff;
        /// <summary>
        /// Narrower or thinner than the design by more than 4 % (the hammer left it so, or the wheel took it): grinding
        /// cannot put it back. On a stretch whose edge is still to grind it is said apart from the progress (still
        /// <see cref="Progress.ToGrind"/>: the edge is wanted all the same).
        /// </summary>
        public bool under;
    }

    /// <summary>
    /// The angle a side (degrees) an edge of the design's own section carries at a stretch <paramref name="tw"/> wide and
    /// <paramref name="tt"/> thick, with <paramref name="edges"/> edges: the design's (<paramref name="ta"/> inclusive, no
    /// steeper than <see cref="MaxBevelAngle"/> a side, the steepest a bevel is ground) - or steeper where the section is
    /// too narrow for it: a bevel reaches no further than the width over the edges (<see cref="GrindBevel"/>: a double
    /// edge's to the middle), so where they meet there they meet at least this steep (a narrow blade, the stretch toward a
    /// point). The judge, the stone and the evaluator ask for this one, never for an angle the section cannot carry.
    /// </summary>
    public static double EdgeHalfAngle(double tw, double tt, double ta, int edges)
    {
        double design = CraftMath.Clamp(ta * 0.5, 0, MaxBevelAngle);
        if (tw <= 0 || tt <= 0)
            return design;
        double steepest = Math.Atan(0.5 * tt / Math.Max(0.1, tw / Math.Max(1, edges))) * 180 / Math.PI;
        return Math.Max(design, steepest);
    }

    /// <summary>
    /// A stretch against the design's width <paramref name="tw"/>, thickness <paramref name="tt"/> and, where it wants an
    /// edge (<paramref name="edged"/>, <paramref name="edges"/> of them), its inclusive angle <paramref name="ta"/> - by the
    /// evaluator's own measures: an edge is done once the bevels have met (<see cref="EdgeMet"/>) with each face carrying
    /// its half of the thickness (an apex in the middle: the evaluator's symmetry) at the angle the design's section
    /// carries there (<see cref="EdgeHalfAngle"/>, ± 2°), no wider or thicker than the design (4 %: the shape score counts
    /// an error either way - grinding the bevels on takes the edge back, the faces thin it); elsewhere the width and
    /// thickness within 4 %. An edge not yet met (or met off-centre) is still to grind, however much narrower or thinner
    /// than the design the stretch already is - the hammer often leaves a stretch a little thin, and it wants its edge all
    /// the same (<see cref="Judgement.under"/> tells the undersize apart). With nothing left to grind, more than 12 %
    /// narrower or thinner is too far; a little under it, or the bevels met at another angle, is off.
    /// </summary>
    public static Judgement Judge(MetalSegment s, double tw, double tt, double ta, bool edged, int edges = 1)
    {
        var j = new Judgement { progress = Progress.NoTarget };
        if (s == null || tw <= 0 || tt <= 0)
            return j;
        j.widthOff = (s.width - tw) / Math.Max(5, tw);
        j.thicknessOff = (s.thickness - tt) / Math.Max(1, tt);
        double under = Math.Max(-j.widthOff, -j.thicknessOff), over = Math.Max(j.widthOff, j.thicknessOff);
        j.under = under > 0.04;
        if (!edged)
        {
            j.done = CraftMath.Clamp01(1 - Math.Max(0, over) * 4);
            j.progress = under > 0.12 ? Progress.TooFar : under > 0.04 ? Progress.Off : over < 0.04 ? Progress.Done : Progress.ToGrind;
            return j;
        }
        double half = Math.Max(0.05, 0.5 * s.thickness), want = EdgeHalfAngle(tw, tt, ta, edges);
        j.want = want;
        j.doneL = CraftMath.Clamp01(s.bevelDepthL / half);
        j.doneR = CraftMath.Clamp01(s.bevelDepthR / half);
        j.angleL = s.AngleL;
        j.angleR = s.AngleR;
        j.angleOkL = s.bevelWidthL > 1e-6 && Math.Abs(s.AngleL - want) <= 2;
        j.angleOkR = s.bevelWidthR > 1e-6 && Math.Abs(s.AngleR - want) <= 2;
        j.done = Math.Min(j.doneL, j.doneR);
        // The bevels have met (the edge is ground) and each face carries its half of the thickness (a centred apex).
        bool met = s.EdgeThickness <= EdgeMet, centred = j.doneL >= 0.8 && j.doneR >= 0.8;
        if (met && centred)
        {
            if (under > 0.12)
                j.progress = Progress.TooFar; // the edge is ground, the stretch far short of the design: nothing puts it back
            else if (!j.angleOkL || !j.angleOkR || under > 0.04)
                j.progress = Progress.Off; // met at another angle (or a little short of the design): grinding on does not mend it
            else if (over > 0.04)
            {
                // At the angle, but wider or thicker than the design: the bevels ground on take the edge back, the faces thin it.
                j.progress = Progress.ToGrind;
                j.done = Math.Min(j.done, CraftMath.Clamp01(1 - over * 4));
            }
            else
                j.progress = Progress.Done;
        }
        else
            j.progress = Progress.ToGrind; // the edge is still to grind (an apex off-centre is mended by grinding the shallower face), short of the design or not
        return j;
    }

    /// <summary>
    /// Grinds <paramref name="area"/> mm² off face <paramref name="left"/>'s bevel held at <paramref name="angle"/>
    /// degrees, as planes do. The bevel grows at the angle held (area-weighted with what is there) until it meets the
    /// other face's bevel; ground on, its plane moves in and the apex slides along the other face's plane toward the
    /// face being ground (the other bevel shortens, the edge goes back); once the other face has no bevel left - or never
    /// had one - the edge goes straight back. On a piece with <paramref name="edges"/> 2 a face's bevel is ground on both
    /// edges alike (both go back) and reaches the middle of the width at most. Returns how far each edge went back (mm).
    /// </summary>
    public static double GrindBevel(MetalSegment seg, bool left, double area, double angle, int edges = 1)
    {
        if (seg == null || area <= 0)
            return 0;
        int n = Math.Max(1, edges);
        double T = Math.Max(0.1, seg.thickness);
        double dF = left ? seg.bevelDepthL : seg.bevelDepthR, wF = left ? seg.bevelWidthL : seg.bevelWidthR;
        double dO = left ? seg.bevelDepthR : seg.bevelDepthL, wO = left ? seg.bevelWidthR : seg.bevelWidthL;
        double held = CraftMath.Clamp(angle, 1, 80);
        double existing = dF > 1e-9 && wF > 1e-9 ? 0.5 * dF * wF : 0;
        double oldAngle = existing > 0 ? Math.Atan(dF / wF) * 180 / Math.PI : held;
        double tF = Math.Tan((oldAngle * existing + held * area) / (existing + area) * Math.PI / 180);
        double tO = dO > 1e-9 && wO > 1e-9 ? dO / wO : 0;
        double S = existing + area, room = Math.Max(0, T - dO), meet = 0.5 * room * room / tF, back = 0;
        if (S <= meet)
        {
            // Before the bevels meet: a triangle at the angle held.
            dF = Math.Sqrt(2 * S * tF);
            wF = dF / tF;
        }
        else
        {
            // They meet: the face's plane moves in by u (across the width); the apex slides down the other face's plane.
            double rest = S - meet;
            dF = room;
            wF = dF / tF;
            if (tO > 0)
            {
                double k = tF * tO / (tF + tO);
                double u = (Math.Sqrt(dF * dF + 2 * k * rest) - dF) / k;
                if (dO - k * u >= 0)
                {
                    back += u * tF / (tF + tO);
                    dO -= k * u;
                    rest = 0;
                }
                else
                {
                    // The other bevel is ground away: from there this face spans the thickness.
                    double all = dO / k;
                    rest = Math.Max(0, rest - (all * dF + 0.5 * k * all * all));
                    back += all * tF / (tF + tO);
                    dO = 0;
                }
                dF = T - dO;
                wF = dF / tF;
                wO = dO > 1e-9 ? dO / tO : 0;
            }
            if (rest > 1e-12)
            {
                dF = T - dO;
                wF = dF / tF;
                back += rest / T; // this face spans the thickness: the edge goes straight back
            }
        }
        seg.width = Math.Max(0.5, seg.width - back * n);
        // A bevel is no wider than the bar - or, ground on both edges, than half of it (the two meet in a ridge).
        double widest = seg.width / n;
        wF = Math.Min(wF, widest);
        wO = Math.Min(wO, widest);
        if (left) { seg.bevelDepthL = dF; seg.bevelWidthL = wF; seg.bevelDepthR = dO; seg.bevelWidthR = wO; }
        else { seg.bevelDepthR = dF; seg.bevelWidthR = wF; seg.bevelDepthL = dO; seg.bevelWidthL = wO; }
        return back;
    }

    /// <summary>The faces thinned by <paramref name="rate"/> mm each: a bevel keeps its plane (its angle) and loses that depth; true when it is ground away.</summary>
    private static bool ThinBevel(ref double depth, ref double width, double rate)
    {
        if (depth <= 1e-9 || width <= 1e-9)
            return false;
        double tan = depth / width;
        depth -= rate;
        if (depth <= 1e-9)
        {
            depth = width = 0;
            return true;
        }
        width = depth / tan;
        return false;
    }

    /// <summary>Metal (mm of thickness) lost: pits, decarburised skin and scale pits go with it.</summary>
    private static void TookMetal(CraftPiece p, MetalSegment seg, int i, double mm)
    {
        if (mm <= 0)
            return;
        seg.pit = Math.Max(0, seg.pit - mm);
        seg.edge.decarb *= Math.Max(0, 1 - mm / 0.4);
        seg.body.decarb *= Math.Max(0, 1 - mm / 0.8);
        foreach (CraftDefect d in p.defects)
            if (d.at == i && d.kind == CraftDefectKind.ScalePit)
                d.severity = seg.pit;
        p.defects.RemoveAll(d => d.at == i && d.kind == CraftDefectKind.ScalePit && d.severity < 0.05);
        // A shallow surface crack can be ground out; a deep one cannot.
        foreach (CraftDefect d in p.defects)
            if (d.at == i && d.kind == CraftDefectKind.Crack && d.surface && d.severity < 0.4)
                d.severity = Math.Max(0, d.severity - mm * 0.5);
        p.defects.RemoveAll(d => d.at == i && d.kind == CraftDefectKind.Crack && d.severity <= 0.01);
        if (seg.thickness < 0.8) // as thin as it is now (each stroke tells it again, never stacked on itself)
            p.HoldDefect(CraftDefectKind.Overground, i, 0.8 - seg.thickness + 0.2, true, "ground or filed too thin");
    }

    // ------------------------------------------------------------------ descaling
    public static void Descale(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftAbrasive a = ws.Rules.Abrasive(op.id);
        if (p == null || a == null)
            return;
        double pressure = CraftMath.Clamp01(op.a), seconds = Math.Max(0, op.b);
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            double take = a.scale * pressure * seconds;
            seg.clay = 0;
            seg.coating = 0; // the oil or wax goes with what is brushed off
            if (seg.scale >= take)
            {
                seg.scale -= take;
                continue;
            }
            double left = seconds * (1 - seg.scale / Math.Max(1e-9, take));
            seg.scale = 0;
            double metal = a.cut * pressure * left * 0.3;
            seg.thickness = Math.Max(0.1, seg.thickness - metal);
            TookMetal(p, seg, i, metal);
            // Worked on past the scale it scratches the bare metal to its own finish (a scraper or a stone coarser, a brush
            // a satin): one rule for the polish of every abrasive.
            seg.rough += (Math.Max(a.finish, a.kind == CraftAbrasiveKind.Brush ? a.finish : 15) - seg.rough) * Math.Min(1, 0.6 * pressure * left + 0.2);
            seg.polish = PolishOf(seg.rough);
            seg.temperFilm = Math.Max(seg.edge.temp, seg.body.temp);
        }
        p.Record(ws.Elapsed, CraftOpKind.Descale, $"Descaled ({a.Name.ToLowerInvariant()})", pressure);
    }

    // ------------------------------------------------------------------ the wheel and the file
    public static void Grind(CraftWorkshop ws, CraftPiece p, CraftOperation op) => Abrade(ws, p, op, true);

    public static void File(CraftWorkshop ws, CraftPiece p, CraftOperation op) => Abrade(ws, p, op, false);

    private static void Abrade(CraftWorkshop ws, CraftPiece p, CraftOperation op, bool wheel)
    {
        CraftRules rules = ws.Rules;
        CraftAbrasive a = rules.Abrasive(op.id);
        if (p == null || a == null)
            return;
        var side = (CraftSurfaceSide)op.i2;
        double pressure = CraftMath.Clamp01(op.a), seconds = Math.Max(0, op.b), angle = CraftMath.Clamp(op.c, 3, MaxBevelAngle);
        bool wood = p.kind == CraftPieceKind.Wood;
        CraftAlloyProfile prof = wood ? null : p.Profile(rules);
        bool skated = false, drawn = false, rasped = false;
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            double hard = WorkedHardness(rules, p, seg, side);
            if (hard > a.maxHardness)
            {
                skated = true;
                continue;
            }
            double rate = a.cut * pressure * seconds * (wood ? 3 : 1.2 - 0.6 * Math.Min(1, hard / 900));
            double removed = 0;
            seg.scale = Math.Max(0, seg.scale - a.scale * pressure * seconds);
            seg.clay = 0;
            switch (side)
            {
                case CraftSurfaceSide.LeftBevel:
                case CraftSurfaceSide.RightBevel:
                {
                    // Planes: the bevel grows at the angle held until it meets the other; ground on, the apex moves. A
                    // bevel gives it an edge: as many as its purpose says (a sword's blade two), else one at least.
                    p.edges = p.IntendedEdges(rules, Math.Max(1, p.edges));
                    GrindBevel(seg, side == CraftSurfaceSide.LeftBevel, rate * Math.Min(seg.width, 3 * seg.thickness), angle, p.edges);
                    removed = rate * 0.5;
                    seg.honeL = seg.honeR = 0;
                    seg.microAngle = 0;
                    seg.apex = Math.Max(seg.apex, a.finish * 6);
                    break;
                }
                case CraftSurfaceSide.Faces:
                {
                    // Both faces come down toward the middle: each bevel keeps its plane (its angle) and loses the depth its
                    // face lost, so the edge keeps what it had; a bevel ground away lets its face reach the apex.
                    seg.thickness = Math.Max(0.1, seg.thickness - rate * 2);
                    bool lostL = ThinBevel(ref seg.bevelDepthL, ref seg.bevelWidthL, rate);
                    bool lostR = ThinBevel(ref seg.bevelDepthR, ref seg.bevelWidthR, rate);
                    if (lostL) { seg.honeL = 0; seg.microL = 0; }
                    if (lostR) { seg.honeR = 0; seg.microR = 0; }
                    if (lostL || lostR)
                    {
                        seg.microAngle = 0;
                        seg.apex = Math.Max(seg.apex, a.finish * 6);
                    }
                    removed = rate;
                    break;
                }
                case CraftSurfaceSide.EdgeLine:
                {
                    double w = rate;
                    if (seg.bevelWidthL > 1e-6) { double k = Math.Min(1, w / seg.bevelWidthL); seg.bevelDepthL *= 1 - k; seg.bevelWidthL = Math.Max(0, seg.bevelWidthL - w); }
                    if (seg.bevelWidthR > 1e-6) { double k = Math.Min(1, w / seg.bevelWidthR); seg.bevelDepthR *= 1 - k; seg.bevelWidthR = Math.Max(0, seg.bevelWidthR - w); }
                    seg.width = Math.Max(0.5, seg.width - w);
                    seg.apex = Math.Max(seg.apex, 250);
                    seg.chip *= 0.3;
                    // Taken back to a flat: what was honed, the micro-bevel and the burr go with the old edge.
                    seg.honeL = seg.honeR = 0;
                    seg.microL = seg.microR = seg.microAngle = 0;
                    seg.burr = 0;
                    removed = 0;
                    break;
                }
                case CraftSurfaceSide.Spine:
                    seg.width = Math.Max(0.5, seg.width - rate);
                    break;
                case CraftSurfaceSide.Tang:
                    seg.width = Math.Max(0.5, seg.width - rate * 0.6);
                    seg.thickness = Math.Max(0.5, seg.thickness - rate * 0.4);
                    removed = rate * 0.4;
                    break;
                case CraftSurfaceSide.Eye:
                    if (seg.hole > 0) seg.hole = Math.Min(seg.width * 0.9, seg.hole + rate);
                    else if (wood) seg.hole = rate; // a slot burnt or carved in a grip
                    break;
            }
            seg.rough += (a.finish - seg.rough) * Math.Min(1, 0.6 * pressure * seconds + 0.2);
            if (wood && side != CraftSurfaceSide.Eye)
            {
                // The rasp takes the bark off where it cuts, by the depth it takes - as the carving knife does.
                seg.bark *= Math.Exp(-rate / 1.5);
                rasped = true;
            }
            if (!wood)
            {
                TookMetal(p, seg, i, removed);
                if (wheel)
                {
                    // The wheel heats where it works: the thin edge most; the spine and the tang the body (the edge only as
                    // the heat spreads through the steel); the broad faces both, each by its own thickness.
                    double heat = rules.Settings.grindHeat * a.heat * pressure * seconds;
                    double before = seg.edge.temp, runs = TemperRunsAt(rules, seg.edge);
                    switch (side)
                    {
                        case CraftSurfaceSide.LeftBevel:
                        case CraftSurfaceSide.RightBevel:
                        case CraftSurfaceSide.EdgeLine:
                            seg.edge.temp += heat / Math.Max(0.3, seg.EdgeThickness + 0.2);
                            seg.body.temp += heat * 0.25 / Math.Max(1, seg.thickness);
                            break;
                        case CraftSurfaceSide.Spine:
                        case CraftSurfaceSide.Tang:
                            seg.body.temp += heat / Math.Max(1, seg.thickness);
                            break;
                        default:
                            seg.edge.temp += heat * 0.6 / Math.Max(0.5, (seg.EdgeThickness + seg.thickness) * 0.5);
                            seg.body.temp += heat * 0.6 / Math.Max(1, seg.thickness);
                            break;
                    }
                    if (before < runs && seg.edge.temp >= runs)
                        drawn = true;
                }
                // Bare metal again: as bright as its new scratches allow (a mirror ground on goes grey), its oil or wax
                // gone with the surface, and its colours begin again at the heat it has now.
                seg.coating = 0;
                if (seg.scale <= 0.001)
                    seg.polish = PolishOf(seg.rough);
                seg.temperFilm = Math.Max(seg.edge.temp, seg.body.temp);
            }
        }
        if (rasped)
        {
            p.wood.bark = CraftWoodwork.BarkLeft(p);
            p.wood.lastGrit = -1; // a fresh cut: the papers start again
        }
        if (skated)
            ws.Raise(CraftEventKind.Note, p.id, "skate", Skates(a));
        if (drawn)
            ws.Raise(CraftEventKind.Note, p.id, "temper_drawn", "The edge flashes straw, then blue: its temper has run.");
        p.Record(ws.Elapsed, wheel ? CraftOpKind.Grind : CraftOpKind.File,
            $"{(wheel ? "Ground" : wood ? "Rasped" : "Filed")} ({SideWord(side)}{(side == CraftSurfaceSide.LeftBevel || side == CraftSurfaceSide.RightBevel ? $" at {angle:0}°" : "")})", pressure);
        if (wheel && !wood)
            ws.Raise(CraftEventKind.Ground, p.id, "sparks", $"Off the wheel: {CraftMetallurgy.Sparks(rules, prof)}.", (float)pressure);
    }

    /// <summary>What an abrasive that does not bite does ("The file skates over it...").</summary>
    public static string Skates(CraftAbrasive a) => a != null && a.kind == CraftAbrasiveKind.Wheel
        ? "The wheel only glazes over it: it is harder than the wheel."
        : "The file skates over it without biting: it is harder than the file.";

    /// <summary>A side of the section in words ("left bevel").</summary>
    public static string SideWord(CraftSurfaceSide side) => side switch
    {
        CraftSurfaceSide.LeftBevel => "left bevel",
        CraftSurfaceSide.RightBevel => "right bevel",
        CraftSurfaceSide.EdgeLine => "edge line",
        _ => side.ToString().ToLowerInvariant(),
    };

    // ------------------------------------------------------------------ carving wood
    /// <summary>What a knife stroke on wood takes off: the width, the thickness, both (rounding it), or a bore into the end (a tang slot).</summary>
    public enum CarveCut { Width, Thickness, Round, Bore }

    /// <summary>
    /// A stroke of the carving knife along segments <c>i0</c>-<c>i1</c> (<c>i2</c> the cut, <c>a</c> how deep in mm,
    /// <c>b</c> how steady the hand). A deep stroke against the grain splits the wood; a steady one leaves it smooth.
    /// It never goes below 3 mm.
    /// </summary>
    public static void Carve(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        var cut = (CarveCut)op.i2;
        double depth = CraftMath.Clamp(op.a, 0, 3), steady = CraftMath.Clamp01(op.b);
        double take = depth * (0.7 + 0.3 * steady);
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment s = p.segments[i];
            switch (cut)
            {
                case CarveCut.Width: s.width = Math.Max(3, s.width - take); break;
                case CarveCut.Thickness: s.thickness = Math.Max(3, s.thickness - take); break;
                case CarveCut.Round:
                    // Taking the corners off: both shrink toward the smaller, the section rounds.
                    s.width = Math.Max(3, s.width - take * (s.width >= s.thickness ? 0.8 : 0.4));
                    s.thickness = Math.Max(3, s.thickness - take * (s.thickness >= s.width ? 0.8 : 0.4));
                    break;
                case CarveCut.Bore: s.hole = Math.Min(Math.Min(s.width, s.thickness) * 0.7, s.hole + take); break;
            }
            s.rough += (Math.Max(12, 40 - 25 * steady) - s.rough) * 0.5;
            if (cut != CarveCut.Bore) s.bark *= Math.Exp(-take / 1.5); // the knife takes the bark off where it cuts
        }
        if (cut != CarveCut.Bore) p.wood.lastGrit = -1; // a fresh cut: the papers start again
        p.wood.bark = CraftWoodwork.BarkLeft(p);
        // Forcing a deep cut, or a shaky one, splits it along the grain - the more readily the more the wood cleaves.
        if (depth > CraftWoodwork.CarveSplitDepth || steady < CraftWoodwork.CarveSplitSteady)
            p.wood.split = Math.Min(1, p.wood.split + (Math.Max(0, depth - CraftWoodwork.CarveSplitDepth) * 0.04 + Math.Max(0, CraftWoodwork.CarveSplitSteady - steady) * 0.03)
                                                     * (0.4 + CraftWoodwork.Cleave(ws.Rules, p)));
        p.Record(ws.Elapsed, CraftOpKind.Carve, cut == CarveCut.Bore ? "A slot bored for the tang" : $"Carved ({cut.ToString().ToLowerInvariant()})", depth);
    }

    // ------------------------------------------------------------------ polishing and coatings
    public static void Polish(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftAbrasive a = ws.Rules.Abrasive(op.id);
        if (p == null || a == null)
            return;
        double pressure = CraftMath.Clamp01(op.a), seconds = Math.Max(0, op.b);
        bool skipped = false;
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            // Deep scratches from a skipped grit come out slowly (and are said to, once the scale is off and they show).
            bool deep = seg.rough > a.finish * GritReach;
            skipped |= deep && seg.scale <= 0.005;
            double rate = deep ? 0.02 : 0.3;
            seg.rough = a.finish + (seg.rough - a.finish) * Math.Exp(-seconds * pressure * rate);
            seg.scale = Math.Max(0, seg.scale - a.scale * pressure * seconds);
            seg.thickness = Math.Max(0.1, seg.thickness - 0.0015 * pressure * seconds);
            if (seg.scale <= 0.001)
                seg.polish = PolishOf(seg.rough); // the one rule: never duller than freshly ground bare metal
            seg.temperFilm = Math.Max(seg.edge.temp, seg.body.temp);
            seg.coating *= 0.5;
        }
        if (skipped)
            ws.Raise(CraftEventKind.Note, p.id, "skipped_grit", "Deep scratches from a coarser grit stay under this one: go back to the grit before it.");
        p.Record(ws.Elapsed, CraftOpKind.Polish, $"Polished ({a.Name.ToLowerInvariant()})", a.finish);
    }

    public static void Coat(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        if (p == null || s == null)
            return;
        if (p.kind == CraftPieceKind.Wood)
            p.wood.oil = Math.Min(1, p.wood.oil + 0.5 * s.strength);
        else
            foreach (MetalSegment seg in p.segments)
                seg.coating = Math.Min(1, seg.coating + 0.5 * s.strength * (seg.scale > 0.05 ? 0.4 : 1));
        p.Record(ws.Elapsed, CraftOpKind.Coat, $"Coated with {s.Name.ToLowerInvariant()}");
    }

    // ------------------------------------------------------------------ the whetstone
    public static void Sharpen(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftRules rules = ws.Rules;
        CraftAbrasive stone = rules.Abrasive(op.id);
        if (p == null || stone == null)
            return;
        CraftAlloyProfile prof = p.Profile(rules);
        bool left = (CraftSurfaceSide)op.i2 == CraftSurfaceSide.LeftBevel;
        double pressure = CraftMath.Clamp01(op.a), angle = CraftMath.Clamp(op.b, 3, 45);
        int strokes = Math.Max(1, (int)Math.Round(op.c));
        double wobble = Math.Max(0, op.d);
        bool leading = op.flag;
        bool strop = IsStrop(stone);
        bool shoulder = false, intoLeather = false;
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            double hard = CraftMetallurgy.Hardness(rules, prof, seg, seg.edge, Math.Max(0.2, seg.EdgeThickness));
            double tough = CraftMetallurgy.Toughness(rules, prof, seg, seg.edge, Math.Max(0.2, seg.EdgeThickness));
            for (int k = 0; k < strokes; k++)
            {
                if (strop && leading)
                {
                    // Edge leading, the edge slices into the leather: it hones nothing, takes no burr off, and dulls a little.
                    if (seg.EdgeThickness <= FlatGone)
                        seg.apex = Math.Min(300, seg.apex * (1 + 0.03 * pressure));
                    intoLeather = true;
                    continue;
                }
                // A stone cuts more edge leading; the strop is drawn spine first (trailing), at its own gentle rate.
                double cut = rules.Settings.sharpenRate * pressure * (strop ? 0.2 : leading ? 1.2 : 0.8) / (1 + hard / 700);
                double flat = seg.EdgeThickness;
                if (flat > FlatGone)
                {
                    // No edge to hone yet: the stone wears the flat away first, slowly, cutting this face's bevel at the
                    // angle held (the strop cuts nothing). Nothing is honed to an apex while a flat shows.
                    if (!strop)
                        GrindBevel(seg, left, Bite(stone) * pressure / (1 + hard / 700), angle, p.edges);
                    seg.apex = Math.Max(seg.apex, 40 + 60 * seg.EdgeThickness);
                    continue;
                }
                // Only where the stone reaches the apex: held below the bevel's angle it rubs the bevel's shoulder.
                double bevel = left ? seg.AngleL : seg.AngleR;
                double contact = Contact(angle, bevel);
                if (contact < 0.3)
                    shoulder = true;
                double reach = cut / (1 + flat / 0.25) * contact;
                // The micro-bevel follows the angle held, never finer than the bevel under it.
                double target = Math.Max(angle, bevel);
                // A face is honed to the apex at 1 (the bar full): strokes past it hone no further.
                if (left)
                {
                    if (reach > 0) seg.microL = seg.honeL > 0 ? seg.microL + (target - seg.microL) * Math.Min(1, cut * 3 * contact) : target;
                    seg.honeL = Math.Min(1, seg.honeL + reach);
                    if (seg.honeL >= 1 && leading) seg.burr = Math.Min(1, seg.burr + reach * 1.5);
                }
                else
                {
                    if (reach > 0) seg.microR = seg.honeR > 0 ? seg.microR + (target - seg.microR) * Math.Min(1, cut * 3 * contact) : target;
                    seg.honeR = Math.Min(1, seg.honeR + reach);
                    if (seg.honeR >= 1 && leading) seg.burr = Math.Min(1, seg.burr + reach * 1.5);
                }
                // Edge-leading strokes raise the burr; light trailing strokes take it off (the lighter the more), the strop at any pressure.
                if (strop)
                    seg.burr *= 1 - 0.35 * (1 - pressure);
                else if (!leading)
                    seg.burr *= 1 - BurrOff(pressure) * contact;
                double both = Math.Min(Math.Min(seg.honeL, seg.honeR), 1);
                double floor = SharpenFloor(rules, p, seg, stone, wobble);
                double toward = cut * 2 * both * contact;
                seg.apex = floor + (seg.apex - floor) * Math.Exp(-toward);
                if (both < 0.3)
                    seg.apex = Math.Max(seg.apex, 40 + flat * 60);
                // Hard thin edges chip under heavy pressure on a stone (the strop's leather gives).
                if (pressure > 0.7 && leading && !strop)
                    seg.chip = Math.Min(1, seg.chip + 0.003 * (pressure - 0.7) * (hard / 700) * (30 / Math.Max(5, tough)));
                else
                    seg.chip *= 1 - 0.02 * both;
                seg.width = Math.Max(0.5, seg.width - 0.0015 * cut);
            }
            if (seg.honeL > 0.5 && seg.honeR > 0.5 && seg.EdgeThickness <= FlatGone)
                seg.microAngle = seg.microL + seg.microR + wobble * 0.5;
            seg.rough += (stone.finish - seg.rough) * (1 - Math.Pow(0.9975, strokes));
        }
        if (shoulder)
            ws.Raise(CraftEventKind.Note, p.id, "shoulder", "The stone only rubs the bevel's shoulder: hold the blade steeper, at the bevel's own angle.");
        if (intoLeather)
            ws.Raise(CraftEventKind.Note, p.id, "strop_lead", "The edge bites into the leather: draw it over the strop spine first, edge trailing.");
        // One line for the work on one stone, whichever face (the strokes add up in it).
        p.Record(ws.Elapsed, CraftOpKind.Sharpen, $"Sharpened on the {stone.Name.ToLowerInvariant()}", strokes);
        ws.Raise(CraftEventKind.Sharpened, p.id, stone.id, "", strokes);
    }
}

public sealed partial class CraftWorkshop
{
    private bool SurfaceCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason)
    {
        reason = "";
        p = Piece(id);
        if (!Has(tool))
            return Refuse(missing, out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Wood)
            return Refuse("There is nothing to work there.", out reason, id);
        // Finishing work can be done on a part already fitted (a blade in its hilt, a head on its haft).
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            return Refuse("Take it out first.", out reason, id);
        return true;
    }

    /// <summary>Does the abrasive bite anywhere from <paramref name="from"/> to <paramref name="to"/> (on the zone the side works)?</summary>
    private bool Bites(CraftPiece p, int from, int to, CraftSurfaceSide side, CraftAbrasive a)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
            if (CraftSurface.WorkedHardness(Rules, p, p.segments[i], side) <= a.maxHardness)
                return true;
        return false;
    }

    /// <summary>
    /// Refused: the abrasive skates over all of it. The note goes out first (no operation is recorded, so the journal
    /// learns it from the request): without words, the refusal says them.
    /// </summary>
    private bool Skated(CraftPiece p, CraftAbrasive a, out string reason)
    {
        Raise(CraftEventKind.Note, p.id, "skate", "");
        return Refuse(CraftSurface.Skates(a), out reason, p.id);
    }

    /// <summary>Is there an eye or a slot anywhere from <paramref name="from"/> to <paramref name="to"/>?</summary>
    private static bool HasHole(CraftPiece p, int from, int to)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
            if (p.segments[i].hole > 0)
                return true;
        return false;
    }

    /// <summary>
    /// Takes scale off segments <paramref name="from"/>-<paramref name="to"/> with a brush or a scraper (the files' bench:
    /// the wheel grinds it off, with its heat, through <see cref="Grind"/>).
    /// </summary>
    public bool Descale(string id, int from, int to, string abrasive, double pressure, double seconds, out string reason)
    {
        if (!SurfaceCheck(id, CraftTools.Files, "There are no brushes or scrapers here.", out CraftPiece p, out reason))
            return false;
        CraftAbrasive a = Rules.Abrasive(abrasive);
        if (a == null)
            return Refuse("There is no such tool.", out reason, id);
        if (a.kind != CraftAbrasiveKind.Brush)
            return Refuse("That is not a brush or a scraper.", out reason, id);
        if (p.kind == CraftPieceKind.Wood)
            return Refuse("There is no scale on wood.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Descale, piece = id, i0 = from, i1 = to, id = a.id, a = (float)pressure, b = (float)seconds });
        return true;
    }

    /// <summary>Works a piece on the grinding wheel: a side, at an angle (bevels), a pressure, seconds per segment.</summary>
    public bool Grind(string id, int from, int to, CraftSurfaceSide side, string wheel, double angle, double pressure, double seconds, out string reason)
    {
        if (!SurfaceCheck(id, CraftTools.Grinder, "There is no grinding wheel here.", out CraftPiece p, out reason))
            return false;
        CraftAbrasive a = Rules.Abrasive(wheel);
        if (a == null || a.kind != CraftAbrasiveKind.Wheel)
            return Refuse("That is not a wheel.", out reason, id);
        if (side == CraftSurfaceSide.Eye)
            return Refuse("A wheel cannot reach inside an eye: use a file.", out reason, id);
        if (p.kind == CraftPieceKind.Wood && !a.wood)
            return Refuse("The wheel would only burn the wood.", out reason, id);
        if (p.MaxTemperature > CraftSurface.WheelTooHot)
            return Refuse("It is far too hot to hold on the wheel: cool it first.", out reason, id);
        if (!Bites(p, from, to, side, a))
            return Skated(p, a, out reason);
        Record(new CraftOperation { kind = CraftOpKind.Grind, piece = id, i0 = from, i1 = to, i2 = (int)side, id = a.id, a = (float)pressure, b = (float)seconds, c = (float)angle });
        return true;
    }

    /// <summary>Files (or rasps) a piece: a side, a file, a pressure, seconds per segment.</summary>
    public bool File(string id, int from, int to, CraftSurfaceSide side, string file, double angle, double pressure, double seconds, out string reason)
    {
        if (!SurfaceCheck(id, CraftTools.Files, "There are no files here.", out CraftPiece p, out reason))
            return false;
        CraftAbrasive a = Rules.Abrasive(file);
        if (a == null || a.kind != CraftAbrasiveKind.File)
            return Refuse("That is not a file.", out reason, id);
        if (p.kind == CraftPieceKind.Wood && !a.wood)
            return Refuse("Use a rasp or a knife on wood.", out reason, id);
        if (p.MaxTemperature > CraftSurface.FileTooHot)
            return Refuse("It is too hot to file.", out reason, id);
        if (side == CraftSurfaceSide.Eye && p.kind != CraftPieceKind.Wood && !HasHole(p, from, to))
            return Refuse("There is no eye or slot there: punch or drift one first.", out reason, id);
        if (!Bites(p, from, to, side, a))
            return Skated(p, a, out reason);
        Record(new CraftOperation { kind = CraftOpKind.File, piece = id, i0 = from, i1 = to, i2 = (int)side, id = a.id, a = (float)pressure, b = (float)seconds, c = (float)angle });
        return true;
    }

    public bool Polish(string id, int from, int to, string grit, double pressure, double seconds, out string reason)
    {
        if (!SurfaceCheck(id, CraftTools.Polishing, "There is nothing to polish with here.", out CraftPiece p, out reason))
            return false;
        CraftAbrasive a = Rules.Abrasive(grit);
        if (a == null || a.kind != CraftAbrasiveKind.Polish)
            return Refuse("That is not a polishing grit.", out reason, id);
        if (p.kind == CraftPieceKind.Wood)
            return Refuse("Sand and finish wood at the finishing bench.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Polish, piece = id, i0 = from, i1 = to, id = a.id, a = (float)pressure, b = (float)seconds });
        return true;
    }

    /// <summary>
    /// A stroke of the carving knife on a wooden piece (on the bench): segments <paramref name="from"/>-<paramref name="to"/>,
    /// what it cuts, how deep (mm, up to 3) and how steadily.
    /// </summary>
    public bool Carve(string id, int from, int to, CraftSurface.CarveCut cut, double depth, double steadiness, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Files))
            return Refuse("There is no carving knife here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Wood)
            return Refuse("Only wood is carved.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it onto the bench first.", out reason, id);
        if (depth <= 0)
            return Refuse("The knife did not bite.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Carve, piece = id, i0 = from, i1 = to, i2 = (int)cut, a = (float)CraftMath.Clamp(depth, 0, 3), b = (float)CraftMath.Clamp01(steadiness) });
        return true;
    }

    /// <summary>Oils or waxes a metal piece in the hands or fitted (wood is oiled at the finishing bench, by the brush).</summary>
    public bool Coat(string id, string supply, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (p != null && p.kind == CraftPieceKind.Wood)
            return Refuse("Sand and finish wood at the finishing bench.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal)
            return Refuse("There is nothing to coat.", out reason, id);
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            return Refuse("Take it out first.", out reason, id);
        if (s == null || s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That does not coat anything.", out reason, id);
        if (p.MaxTemperature > CraftSurface.CoatingBurns)
            return Refuse("It would only smoke and burn on hot metal.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Coat, piece = id, id = s.id });
        return true;
    }

    /// <summary>
    /// Would <see cref="ApplyClay"/> with these arguments put clay on - some stretch coated thicker than it is now? Only
    /// clay that goes on is taken from the bag: wiping it off, or a coat no thicker than the one on it, uses none.
    /// </summary>
    public bool ClayGoesOn(string id, int from, int to, double thickness, IList<float> perSegment = null)
    {
        CraftPiece p = Piece(id);
        if (p == null)
            return false;
        bool painted = perSegment != null && perSegment.Count > 0;
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
        {
            int k = i - from;
            if (painted && k >= perSegment.Count) break; // as CraftSurface.ApplyClay lays it
            double t = painted ? perSegment[k] : thickness;
            if ((t <= 0.01 ? 0 : CraftMath.Clamp(t, 0, 6)) > p.segments[i].clay + 0.01)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Coats segments with clay before hardening (thicker on the spine: a soft spine, a hard edge): one thickness, or
    /// <paramref name="perSegment"/> thicknesses (mm, from <paramref name="from"/>) as it was painted on.
    /// </summary>
    public bool ApplyClay(string id, int from, int to, double thickness, out string reason, IList<float> perSegment = null)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null || p.kind != CraftPieceKind.Metal)
            return Refuse("Clay goes on a blade.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Clay goes on with the piece on the bench.", out reason, id);
        if (p.MaxTemperature > 200)
            return Refuse("Clay it while it is cold.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.ApplyClay, piece = id, i0 = from, i1 = to, a = (float)thickness };
        if (perSegment != null)
            for (int i = 0; i < perSegment.Count && i < 512; i++) op.points.Add((float)CraftMath.Clamp(perSegment[i], 0, 6));
        Record(op);
        return true;
    }

    /// <summary>
    /// Strokes on a whetstone: one face (<paramref name="side"/> Left or Right Bevel), the angle held, pressure, how many
    /// strokes, how much the angle wobbled (degrees), edge leading or trailing.
    /// </summary>
    public bool Sharpen(string id, int from, int to, CraftSurfaceSide side, string stone, double angle, double pressure, int strokes, double wobble, bool edgeLeading,
        out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Whetstone))
            return Refuse("There are no whetstones here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal)
            return Refuse("There is nothing to sharpen.", out reason, id);
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            return Refuse("Take it out first.", out reason, id);
        CraftAbrasive a = Rules.Abrasive(stone);
        if (a == null || a.kind != CraftAbrasiveKind.Whetstone)
            return Refuse("That is not a whetstone.", out reason, id);
        if (side != CraftSurfaceSide.LeftBevel && side != CraftSurfaceSide.RightBevel)
            return Refuse("Hold one face of the edge to the stone.", out reason, id);
        if (p.MaxTemperature > CraftSurface.StoneTooHot)
            return Refuse("Let it cool first.", out reason, id);
        if (!CraftSurface.HasEdge(p, from, to))
            return Refuse("There is no edge there yet: grind or forge the bevels first.", out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.Sharpen, piece = id, i0 = from, i1 = to, i2 = (int)side, id = a.id, a = (float)pressure, b = (float)angle, c = strokes,
            d = (float)wobble, flag = edgeLeading,
        });
        return true;
    }
}
