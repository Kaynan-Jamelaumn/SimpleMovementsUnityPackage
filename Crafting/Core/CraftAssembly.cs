using System;
using System.Collections.Generic;

/// <summary>What is done to a joint. Serialized as numbers: add at the END.</summary>
public enum CraftJointWork
{
    /// <summary>Punch (or drill) holes along the joint: count, how evenly spaced, how well lined up.</summary>
    Punch,
    /// <summary>Stitches: count, tension, how even the tension was.</summary>
    Stitch,
    /// <summary>Set one rivet: strikes and force of the peening.</summary>
    Rivet,
    /// <summary>Peen a tang over a pommel (or close a guard round a tang): strikes and force.</summary>
    Peen,
    /// <summary>Wind a wrap: turns, tension, evenness.</summary>
    Wrap,
    /// <summary>Drive a wedge into a haft: strikes and force.</summary>
    Wedge,
    /// <summary>Solder a joint: <c>a</c> how near the heat came to the solder's flow without overheating (0-1), <c>b</c> the flux's cover (0-1).</summary>
    Solder,
    /// <summary>Push a bezel or the prongs over the stone: <c>a</c> the pressure (0-1), <c>b</c> how evenly round (opposite sides in turn, 0-1), count pushes.</summary>
    Push,
}

/// <summary>
/// Putting an item together. A design says which parts it has and what joins them; the parts are real pieces with
/// their own state, and every joint is made by hand: holes punched (evenly or not, lined up or not), stitches pulled
/// (evenly or not), rivets set (too lightly they are loose, too hard they crush leather, split a board or crack a hard
/// rivet), a tang peened over a pommel, a wrap wound, a wedge driven (too far splits the haft), a guard fitted to its
/// tang (a gap rattles; too tight it will not go on until the slot is filed). A well-made plate badly riveted makes a
/// weak piece of armour: the evaluator reads the parts and the joints.
/// <para>The hammer's joints have an ideal blow (<see cref="IdealCharge"/>, by the materials under the hammer): a blow
/// between <see cref="GoodLow"/> and <see cref="GoodHigh"/> of it does the work well; lighter than <see cref="TooLight"/>
/// a rivet stays loose; harder than <see cref="TooHard"/> it crushes leather, splits a board, bends a tang.</para>
/// <para>A tang is measured as it was forged (<see cref="MeasureTang"/>): the run of narrow segments at the blade's end
/// before it widens at the shoulder. The guard sits on it at the shoulder, the grip along it, the pommel at its end - and
/// the pommel is peened only when the tang comes through the grip and the pommel with <see cref="PeenAllowance"/> to
/// spare.</para>
/// </summary>
public static class CraftAssembly
{
    /// <summary>A blow is good from this share of the joint's ideal blow to <see cref="GoodHigh"/>.</summary>
    public const double GoodLow = 0.85, GoodHigh = 1.15;
    /// <summary>
    /// Lighter than this share of the ideal blow a rivet stays loose; harder than <see cref="TooHard"/> it crushes leather,
    /// splits a board, bends a tang (and a hard, cold rivet cracks above the ideal).
    /// </summary>
    public const double TooLight = 0.6, TooHard = 1.25;
    /// <summary>The pommel is well peened from 1 (the tang's end spread over it) to 1.4; past that the head is overworked.</summary>
    public const double PeenGoodLow = 1.0, PeenGoodHigh = 1.4;
    /// <summary>A wedge holds from 0.6 (the joint at its best); driven past 1.3 it splits the haft. A socket's head is home on its shaft over the same span, and splits the shaft past it.</summary>
    public const double WedgeGoodLow = 0.6, WedgeGoodHigh = 1.3;
    /// <summary>A haft this much (mm) smaller than its eye or socket still sits tight once the head is on.</summary>
    public const double PlayFree = 1.0;
    /// <summary>
    /// A wedge driven home takes up this much more play (mm) between a loose eye and its haft: it spreads the haft's end in
    /// the eye. A socket has no wedge: its play stays.
    /// </summary>
    public const double WedgeTakesUp = 2.0;
    /// <summary>Peening closes a metal guard's slot onto its tang by at most this much (mm): a wider gap stays.</summary>
    public const double PeenCloseBudget = 0.8;
    /// <summary>How far the tang must come through the pommel to be peened over it (mm).</summary>
    public const double PeenAllowance = 3;
    /// <summary>The stitches a buckle sewn on takes (a riveted one takes a rivet and its hole).</summary>
    public const int StitchesPerBuckle = 4;
    /// <summary>A wrap wound at this tension (0-1) is best: its quality falls off either side (CraftEvaluator).</summary>
    public const double WrapTensionIdeal = 0.65;

    /// <summary>
    /// Can a station with <paramref name="tools"/> make every part of <paramref name="d"/>? Metal parts need an anvil or
    /// a crucible, leather parts a leather bench, cloth parts a tailor's table (or a leather bench), wooden parts files or
    /// the woodshop's tools; supplies and fasteners are bought or brought. <paramref name="missing"/> says what is lacking.
    /// </summary>
    public static bool MadeAt(CraftRules rules, CraftDesign d, CraftTools tools, out string missing)
    {
        missing = "";
        bool Has(CraftTools t) => (tools & t) != 0;
        if (d == null) return false;
        if (!Has(CraftTools.AssemblyBench | CraftTools.LeatherBench)) { missing = "a bench to assemble on"; return false; }
        bool jewel = d.kind == CraftDesignKind.Jewelry;
        if (jewel && !Has(CraftTools.JewelersBench)) { missing = "a jeweller's bench"; return false; }
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional || c.supply) continue;
            switch (c.kind)
            {
                case CraftPieceKind.Metal:
                    // jewellery is wire and strip, formed at the jeweller's bench (the stock comes drawn or rolled)
                    if (!Has(CraftTools.Anvil | CraftTools.Crucible) && !jewel) { missing = $"an anvil or a crucible ({c.Name.ToLowerInvariant()})"; return false; }
                    break;
                case CraftPieceKind.Hide:
                    CraftFabric fabric = c.leather?.fabric ?? CraftFabric.Leather;
                    if (fabric == CraftFabric.Cloth && !Has(CraftTools.TailorTable | CraftTools.LeatherBench)) { missing = $"a tailor's table ({c.Name.ToLowerInvariant()})"; return false; }
                    if (fabric != CraftFabric.Cloth && !Has(CraftTools.LeatherBench) && !(fabric == CraftFabric.Either && Has(CraftTools.TailorTable))) { missing = $"a leather bench ({c.Name.ToLowerInvariant()})"; return false; }
                    break;
                case CraftPieceKind.Wood:
                    if (!Has(CraftTools.Files | CraftTools.ChoppingBlock | CraftTools.Sawhorse)) { missing = $"woodworking tools ({c.Name.ToLowerInvariant()})"; return false; }
                    break;
            }
        }
        return true;
    }

    // ------------------------------------------------------------------ the tang
    /// <summary>
    /// The tang a blade really has: the run of segments from its end (segment 0) narrower than its blade - nearer the
    /// target's tang width than its width just past the shoulder (<paramref name="target"/>: the design's shape for that
    /// part). <paramref name="last"/> is the tang's last segment (-1: no tang - the end is as wide as the blade),
    /// <paramref name="length"/> its length (mm); <paramref name="shoulder"/>: the blade widens past it, so a guard has
    /// something to sit against. A target whose profile shows no shoulder measures the nominal tang (the segments before
    /// its <c>tangTo</c>); a target without a tang has none.
    /// </summary>
    public static bool MeasureTang(CraftShapeTarget target, CraftPiece blade, out int last, out double length, out bool shoulder)
    {
        last = -1;
        length = 0;
        shoulder = false;
        if (blade == null || blade.segments.Count == 0 || target == null || target.tangTo <= 0)
            return false;
        int n = blade.segments.Count;
        target.At(0f, out float tangWidth, out _);
        target.At(Math.Min(1f, target.tangTo + 0.02f), out float shoulderWidth, out _);
        if (target.profile != null && target.profile.Count >= 2 && shoulderWidth > tangWidth * 1.05f)
        {
            double limit = (tangWidth + shoulderWidth) * 0.5;
            while (last + 1 < n && blade.segments[last + 1].width < limit) last++;
        }
        else
            while (last + 1 < n && blade.PositionOf(last + 1) < target.tangTo) last++;
        shoulder = last >= 0 && last < n - 1;
        for (int i = 0; i <= last; i++) length += blade.segments[i].length;
        return last >= 0;
    }

    /// <summary>
    /// How thick the tang is where a part sits on it (mm, across its width and thickness): <paramref name="t"/> = 1 at the
    /// shoulder (the guard), 0.5 along the middle (the grip), 0 at its end (the pommel); and the tang's length. A blade
    /// with no tang: its end.
    /// </summary>
    public static double TangSize(CraftShapeTarget target, CraftPiece blade, double t, out double tangLength)
    {
        tangLength = 0;
        if (blade == null || blade.segments.Count == 0)
            return 0;
        MeasureTang(target, blade, out int last, out tangLength, out bool shoulder);
        // The stretch where the tang starts to widen into the shoulder is what a guard rests against, not what it slides over.
        int end = last;
        if (shoulder && last >= 1 && blade.segments[last].width > blade.segments[last - 1].width * 1.05)
            end = last - 1;
        int i = end < 0 ? 0 : (int)Math.Round(CraftMath.Clamp01(t) * end);
        return Size(blade.segments[i]);
    }

    private static double Size(MetalSegment s) => Math.Max(s.width, s.thickness) * 0.75 + Math.Min(s.width, s.thickness) * 0.25;

    /// <summary>The largest hole (eye, slot, socket) a piece has (mm).</summary>
    public static double Eye(CraftPiece p)
    {
        double e = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) e = Math.Max(e, s.hole);
        return e;
    }

    /// <summary>The target shape the design gives a part (its role), or null.</summary>
    public static CraftShapeTarget Target(CraftDesign d, string role) => d?.Component(role)?.shape;

    /// <summary>The gap of a fit joint now (mm; negative = it does not go on), measured as the design wants the parts.</summary>
    public static double FitGap(CraftWorkshop ws, CraftPiece assembly, CraftJointSpec joint)
    {
        CraftPiece a = ws.Piece(assembly.assembly.PieceIn(joint.roleA)), b = ws.Piece(assembly.assembly.PieceIn(joint.roleB));
        if (a == null || b == null)
            return 0;
        return GapOf(ws.Rules.Design(assembly.assembly.design), joint, a, b);
    }

    /// <summary>
    /// The gap between the two parts of a joint (mm; negative = it does not go on): a head's eye against the haft's end
    /// (a tight drive is good), a guard's slot against the tang at the shoulder, a grip's hole against the tang along it,
    /// a pommel's hole against the tang's end - the tang measured against the design's shape for the blade.
    /// </summary>
    public static double GapOf(CraftDesign d, CraftJointSpec joint, CraftPiece a, CraftPiece b)
    {
        if (a == null || b == null)
            return 0;
        switch (joint.kind)
        {
            case CraftJointKind.Wedge:
            case CraftJointKind.Socket:
            {
                CraftPiece head = a.kind == CraftPieceKind.Wood ? b : a, haft = a.kind == CraftPieceKind.Wood ? a : b;
                MetalSegment end = haft.segments.Count > 0 ? haft.segments[haft.segments.Count - 1] : null;
                double size = end != null ? Math.Max(end.width, end.thickness) : 0;
                return Eye(head) - size;
            }
            default:
                return Eye(b) - TangSize(Target(d, joint.roleA), a, Where(d, joint.roleB), out _);
        }
    }

    /// <summary>Where along the tang a part sits: 1 the shoulder (a guard), 0 the end (a pommel), 0.5 along it (a grip).</summary>
    private static double Where(CraftDesign d, string role)
    {
        CraftComponentSpec c = d?.Component(role);
        return c != null && c.function == CraftComponentFunction.Guard ? 1 : c != null && c.function == CraftComponentFunction.Counterweight ? 0 : 0.5;
    }

    /// <summary>
    /// How much of the tang the hilt takes (mm): every grip and counterweight fitted on it (<paramref name="pieceIn"/>:
    /// the piece in a role and place) - the tang runs through their length; peening needs <see cref="PeenAllowance"/> more.
    /// </summary>
    public static double OnTang(CraftDesign d, Func<string, int, CraftPiece> pieceIn)
    {
        double need = 0;
        if (d == null || pieceIn == null)
            return 0;
        foreach (CraftComponentSpec c in d.components)
            if (!c.supply && (c.function == CraftComponentFunction.Grip || c.function == CraftComponentFunction.Counterweight))
                for (int i = 0; i < Math.Max(1, c.count); i++)
                {
                    CraftPiece p = pieceIn(c.role, i);
                    if (p != null) need += p.Length;
                }
        return need;
    }

    /// <summary>
    /// The tang of a hilted item on the bench and what its hilt takes of it (both mm; false when it has no peened pommel
    /// joint or no blade fitted). The pommel is peened once <paramref name="tang"/> &gt;= <paramref name="need"/> +
    /// <see cref="PeenAllowance"/>.
    /// </summary>
    public static bool TangReport(CraftWorkshop ws, CraftPiece asm, out double tang, out double need)
    {
        tang = need = 0;
        CraftDesign d = ws != null && asm?.assembly != null ? ws.Rules.Design(asm.assembly.design) : null;
        CraftJointSpec peen = PeenJoint(d);
        if (peen == null)
            return false;
        CraftPiece blade = ws.Piece(asm.assembly.PieceIn(peen.roleA));
        if (blade == null)
            return false;
        MeasureTang(Target(d, peen.roleA), blade, out _, out tang, out _);
        need = OnTang(d, (role, i) => ws.Piece(asm.assembly.PieceIn(role, i)));
        return true;
    }

    /// <summary>The joint that peens a pommel over a tang (a Peen joint), or null.</summary>
    public static CraftJointSpec PeenJoint(CraftDesign d)
    {
        if (d?.joints != null)
            foreach (CraftJointSpec j in d.joints)
                if (j != null && j.kind == CraftJointKind.Peen) return j;
        return null;
    }

    // ------------------------------------------------------------------ the hammer's joints
    /// <summary>
    /// The blow that does this joint's hammer work best, as the hammer's charge (0-1, as its meter shows it; a request's
    /// <c>a</c> is 4 × the blow): a rivet through leather or cloth takes a light blow, through wood a firmer one, metal on
    /// metal the firmest; a tang is peened over its pommel firmly, a guard's slot closed a little lighter; a wedge is driven
    /// firmly.
    /// </summary>
    public static double IdealCharge(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, CraftJointWork work)
    {
        switch (work)
        {
            case CraftJointWork.Peen: return spec.kind == CraftJointKind.Fit ? 0.65 : 0.8;
            case CraftJointWork.Wedge: return 0.7;
            default:
                if (LeatherIn(ws, asm, spec)) return 0.6;
                return WoodIn(ws, asm, spec) ? 0.7 : 0.8;
        }
    }

    /// <summary>
    /// How well a rivet is set by a blow of <paramref name="r"/> × the ideal blow (0-1): best on it, falling off either side
    /// - a light blow leaves it loose, a heavy one spreads it out of true.
    /// </summary>
    public static double BlowQuality(double r)
    {
        if (!(r > 0))
            return 0;
        double off = r < 1 ? (1 - r) / 0.55 : (r - 1) / 0.45;
        return CraftMath.Clamp01(1 - off * off);
    }

    /// <summary>How much a peening blow of <paramref name="r"/> × the ideal spreads the tang's end (1 on the ideal; light taps barely move it; a harder one spreads no more than the top of the good band).</summary>
    public static double PeenEffect(double r) => Math.Pow(Math.Min(GoodHigh, Math.Max(0, r)), 2);

    /// <summary>
    /// How much of a joint's driving work a blow of <paramref name="r"/> × the ideal does - a wedge or a socketed head
    /// driven on, a guard's slot closed: in proportion to the blow up to the top of the good band (a harder one does no
    /// more, it only harms), and a blow lighter than <see cref="TooLight"/> bounces off and does less than its share.
    /// </summary>
    public static double DriveEffect(double r) => r <= 0 ? 0 : Math.Min(GoodHigh, r) * Math.Min(1, r / TooLight);

    /// <summary>
    /// How true the hammer's blows that drove, peened or closed a joint were (<see cref="JointState.blowQuality"/>, the mean
    /// of their <see cref="BlowQuality"/>): 1 for blows in the good band, down to 0.75 for wild ones - a wedge knocked in
    /// crooked, a peen hammered ragged, a guard battered on. 1 when no blow was struck (a rivet's own blows are in its
    /// quality).
    /// </summary>
    public static double StruckWell(JointState j) => j != null && j.blows > 0 ? 0.75 + 0.25 * CraftMath.Clamp01(j.blowQuality) : 1;

    /// <summary>A blow of <paramref name="r"/> × the ideal struck on a joint: it counts in the joint's <see cref="StruckWell"/>.</summary>
    private static void Struck(JointState j, double r, int strikes)
    {
        if (strikes <= 0)
            return;
        j.blowQuality = (j.blowQuality * j.blows + BlowQuality(r) * strikes) / (j.blows + strikes);
        j.blows += strikes;
    }

    /// <summary>A blow against the joint's ideal, in words.</summary>
    public static string BlowWords(double r) =>
        r < TooLight ? "Too light" : r < GoodLow ? "A little light" : r <= GoodHigh ? "A good blow" : r <= TooHard ? "A little hard" : "Too hard";

    /// <summary>
    /// The play left between a head and its haft (mm, 0 = none): the gap of a loose eye less what its wedge takes up as it
    /// is driven home (<see cref="WedgeTakesUp"/>, in proportion as far as it is driven); a socket's gap as it went on - it
    /// has no wedge. Up to <see cref="PlayFree"/> the head still sits tight.
    /// </summary>
    public static double PlayLeft(CraftJointSpec spec, JointState j)
    {
        if (j == null)
            return 0;
        double gap = Math.Max(0, j.gap);
        if (spec != null && spec.kind == CraftJointKind.Wedge)
            gap = Math.Max(0, gap - WedgeTakesUp * CraftMath.Clamp01(j.wedge / WedgeGoodLow));
        return gap;
    }

    /// <summary>How a head sits on its haft with a gap of <paramref name="gap"/> mm as it goes on, in words: what the wedge will take up, what nothing will.</summary>
    public static string HaftFitWords(CraftJointSpec spec, double gap)
    {
        bool socket = spec != null && spec.kind == CraftJointKind.Socket;
        if (gap < -1)
            return socket ? "a tight drive onto the shaft" : "a tight drive into the eye";
        if (gap <= PlayFree)
            return socket ? "it goes onto the shaft" : "it goes into the eye";
        if (socket)
            return "it is loose on the shaft - nothing takes that play up: fit a thicker shaft";
        return gap <= PlayFree + WedgeTakesUp ? "it is loose in the eye - the wedge will take up the play"
            : "it rattles in the eye - more play than a wedge takes up: fit a thicker haft";
    }

    // ------------------------------------------------------------------ joint work
    /// <summary>A stitch pulled to this tension (0-1) is best: the seam's quality falls off either side of it (CraftEvaluator).</summary>
    public const double StitchTensionIdeal = 0.6;
    /// <summary>A stitch pulled harder than this cuts into the material (the joint is damaged) and puckers it.</summary>
    public const double StitchTensionCuts = 0.9;

    public static void Work(CraftWorkshop ws, CraftPiece asm, CraftOperation op)
    {
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return;
        CraftDesign design = ws.Rules.Design(asm.assembly.design);
        CraftJointSpec spec = design?.Joint(op.id);
        JointState j = asm.assembly.Joint(op.id);
        if (spec == null || j == null)
            return;
        var work = (CraftJointWork)op.i0;
        double force = Math.Max(0, op.a);
        int strikes = Math.Max(0, op.i1);
        string said = "";
        float value = 0f;
        switch (work)
        {
            case CraftJointWork.Punch:
            {
                int n = Math.Max(0, op.i1);
                double total = j.holes + n;
                if (total > 0)
                {
                    // a fine pricking iron spaces true; a worn one wanders
                    j.spacingError = (j.spacingError * j.holes + Math.Max(0, op.a) / ws.ToolQuality * n) / total;
                    j.alignError = (j.alignError * j.holes + Math.Max(0, op.b) / ws.ToolQuality * n) / total;
                }
                j.holes += n;
                if (op.b > 0.6)
                    asm.AddDefect(CraftDefectKind.Misaligned, -1, op.b - 0.5, true, $"holes of the {spec.Name.ToLowerInvariant()} do not line up");
                break;
            }
            case CraftJointWork.Stitch:
            {
                int n = Math.Max(0, op.i1);
                double total = j.stitches + n;
                if (total > 0)
                {
                    j.tension = (j.tension * j.stitches + CraftMath.Clamp01(op.a) * n) / total;
                    j.tensionSpread = (j.tensionSpread * j.stitches + Math.Max(0, op.b) / ws.ToolQuality * n) / total;
                }
                j.stitches += n;
                // pulled so tight it cuts the leather or cloth: the stitches past the cut (flag: i2 counts how many of the
                // run were pulled past it; a run without that count - a single pull, an older history - by its mean pull)
                int cut = op.flag ? Math.Max(0, Math.Min(op.i2, n)) : op.a > StitchTensionCuts ? n : 0;
                if (cut > 0)
                    j.damage = Math.Min(1, j.damage + 0.05 * cut / Math.Max(1, spec.count));
                break;
            }
            case CraftJointWork.Rivet:
            {
                CraftPiece rivets = RivetsOf(ws, asm, design, spec);
                if (rivets == null || rivets.fasteners.Left <= 0)
                    break;
                rivets.fasteners.used++; // set into the item: still part of it
                double r = strikes * force / (4 * IdealCharge(ws, asm, spec, work));
                double q = BlowQuality(r) * rivets.fasteners.quality;
                if (j.holes < j.rivets + 1 && NeedsHoles(ws, asm, design, spec))
                    q *= 0.4; // no hole for it: forced through (older histories: the workshop asks for the hole now)
                q *= 1 - 0.5 * Math.Min(1, j.alignError);
                // A hard rivet does not spread: struck cold, it splits.
                bool hot = rivets.MeanTemperature > rivets.Profile(ws.Rules).HotWorkMin;
                double brittle = hot ? 0 : CraftMath.Clamp01((PieceHardness(ws.Rules, rivets, 0) - 300) / 250);
                string harm = "";
                if (brittle > 0.3 && r > 1)
                {
                    q *= 0.3;
                    asm.AddDefect(CraftDefectKind.CrackedRivet, -1, brittle, false, "a hard, brittle rivet cracked as it was peened cold");
                    harm = ": the hard rivet cracked";
                }
                if (r > TooHard)
                {
                    if (LeatherIn(ws, asm, spec))
                    {
                        j.damage = Math.Min(1, j.damage + 0.12 * (r - TooHard) / 0.25 / Math.Max(1, spec.count) * 4); // peened so hard it crushed the leather
                        harm += harm.Length > 0 ? ", and crushed the leather" : ": it crushed the leather round it";
                    }
                    else if (WoodIn(ws, asm, spec))
                    {
                        j.damage = Math.Min(1, j.damage + 0.1 * (r - TooHard) / 0.25 / Math.Max(1, spec.count) * 4); // and split the wood round it
                        harm += harm.Length > 0 ? ", and split the wood round it" : ": it split the wood round it";
                    }
                }
                j.rivetQuality = (j.rivetQuality * j.rivets + q) / (j.rivets + 1);
                j.rivets++;
                if (q < 0.4)
                    asm.AddDefect(CraftDefectKind.LooseJoint, -1, 0.5 - q, false, "a rivet badly set");
                said = $"{BlowWords(r)}: the rivet is {(q >= 0.85 ? "set tight" : q >= 0.6 ? "set" : q >= 0.4 ? "set, but not tight" : "loose")}{harm}.";
                value = (float)q;
                break;
            }
            case CraftJointWork.Peen:
            {
                double ideal = IdealCharge(ws, asm, spec, work);
                double r = force / (4 * ideal);
                if (spec.kind == CraftJointKind.Fit)
                {
                    CraftPiece part = ws.Piece(asm.assembly.PieceIn(spec.roleB));
                    if (part == null || part.kind != CraftPieceKind.Metal)
                        break; // a wooden grip is not peened shut: it fits as it was bored
                    // The slot is peened closed onto the tang only so far; a snug or a tight one is not opened by it. A
                    // blow in the good band closes it best: a harder one closes it no faster and dents the guard.
                    double room = Math.Max(0, PeenCloseBudget - j.peenClosed);
                    double close = j.gap > 0 ? Math.Min(Math.Min(j.gap, room), 0.32 * ideal * DriveEffect(r) * strikes) : 0;
                    j.gap -= close;
                    j.peenClosed += close;
                    Struck(j, r, strikes);
                    string dent = "";
                    if (r > TooHard)
                    {
                        j.damage = Math.Min(1, j.damage + 0.04 * strikes * (r - TooHard) / 0.25);
                        dent = ", and the blow dented the guard";
                    }
                    bool cold = part.MeanTemperature < 300;
                    if (cold && r > 1 && PieceHardness(ws.Rules, part, EyeSegment(part)) > 400)
                    {
                        asm.AddDefect(CraftDefectKind.Crack, -1, 0.3, true, "the hard guard cracked as it was closed on the tang");
                        said = $"{BlowWords(r)}: the hard guard cracked round its slot.";
                    }
                    else
                        said = $"{BlowWords(r)}: {(close <= 1e-6 ? "the slot will not close any further" : j.gap <= 0.2 ? "the slot is closed snug on the tang" : "the slot closes on the tang")}{dent}.";
                    value = (float)Math.Max(0, j.gap);
                }
                else
                {
                    CraftPiece blade = ws.Piece(asm.assembly.PieceIn(spec.roleA));
                    MeasureTang(Target(design, spec.roleA), blade, out _, out double tang, out _);
                    double need = OnTang(design, (role, i) => ws.Piece(asm.assembly.PieceIn(role, i)));
                    if (tang < need + PeenAllowance)
                    {
                        // older histories (the workshop refuses it now): the tang does not come through
                        ws.Raise(CraftEventKind.Note, asm.id, "short", "The tang is too short to come through the pommel: there is nothing to peen.");
                        break;
                    }
                    j.peen += strikes * 0.2 * PeenEffect(r);
                    Struck(j, r, strikes);
                    if (r > TooHard)
                        j.damage = Math.Min(1, j.damage + 0.03 * strikes * (r - TooHard) / 0.25); // heavy blows bend the tang in the grip
                    CraftAlloyProfile bp = blade != null ? blade.Profile(ws.Rules) : null;
                    if (j.peen > PeenGoodHigh && bp != null && blade.segments.Count > 0 && PieceHardness(ws.Rules, blade, 0) > 350)
                        asm.AddDefect(CraftDefectKind.Crack, -1, 0.3, false, "the tang cracked as it was peened");
                    said = $"{BlowWords(r)}: {(j.peen < 0.3 ? "the tang's end barely spreads" : j.peen < PeenGoodLow ? "the tang's end spreads over the pommel" : j.peen <= PeenGoodHigh ? "the tang's end is spread tight over the pommel" : "the peen is overworked: its head is cracking up")}" +
                           $"{(r > TooHard ? ", and the heavy blow bent the tang" : "")}.";
                    value = (float)j.peen;
                }
                break;
            }
            case CraftJointWork.Wrap:
            {
                int n = Math.Max(0, op.i1);
                double total = j.turns + n;
                if (total > 0)
                {
                    j.wrapTension = (j.wrapTension * j.turns + CraftMath.Clamp01(op.a) * n) / total;
                    j.wrapSpread = (j.wrapSpread * j.turns + Math.Max(0, op.b) * n) / total;
                }
                j.turns += n;
                break;
            }
            case CraftJointWork.Wedge:
            {
                // A wedge driven into the haft's end, or a socketed head driven onto its shaft: a blow in the good band
                // drives it best - a harder one drives it no further and splits the wood, a light one bounces off.
                double ideal = IdealCharge(ws, asm, spec, work);
                double r = force / (4 * ideal);
                bool socket = spec.kind == CraftJointKind.Socket;
                j.wedge += 0.24 * ideal * DriveEffect(r) * strikes * (j.gap < -1 ? 1.3 : 1);
                Struck(j, r, strikes);
                // the haft carries the split (its wood and the joint read it): no defect of its own on the item
                CraftPiece haft = ws.Piece(asm.assembly.PieceIn(spec.roleA));
                if (haft == null || haft.kind != CraftPieceKind.Wood) haft = ws.Piece(asm.assembly.PieceIn(spec.roleB));
                if (haft != null && haft.kind != CraftPieceKind.Wood) haft = null;
                string harm = "";
                if (r > TooHard && strikes > 0 && haft != null)
                {
                    haft.wood.split = Math.Min(1, haft.wood.split + 0.1 * strikes * (r - TooHard) / 0.25);
                    harm = socket ? ", and the blow split the shaft in the socket" : ", and the blow split the haft's end";
                }
                if (j.wedge > WedgeGoodHigh && haft != null)
                    haft.wood.split = Math.Min(1, haft.wood.split + (j.wedge - WedgeGoodHigh) * 0.5);
                bool loose = PlayLeft(spec, j) > PlayFree;
                string state = socket
                    ? j.wedge < 0.3 ? "the head starts onto the shaft" : j.wedge < WedgeGoodLow ? "the head is going on" : j.wedge <= WedgeGoodHigh ? loose ? "the head is home, but loose on the shaft" : "the head is home on the shaft" : "driven too far: the socket has split the shaft"
                    : j.wedge < 0.3 ? "the wedge starts in" : j.wedge < WedgeGoodLow ? "the wedge is going in" : j.wedge <= WedgeGoodHigh ? loose ? "the wedge is home, but the head still rattles" : "the wedge is home: the head sits tight" : "the wedge has split the haft";
                said = $"{BlowWords(r)}: {state}{harm}.";
                value = (float)j.wedge;
                break;
            }
            case CraftJointWork.Solder:
                CraftJewelry.Solder(ws, asm, spec, j, op);
                break;
            case CraftJointWork.Push:
                CraftJewelry.Push(ws, asm, spec, j, op);
                break;
        }
        j.done = JointDone(spec, j);
        asm.Record(ws.Elapsed, CraftOpKind.JointWork, $"{spec.Name}: {work.ToString().ToLowerInvariant()}", force);
        ws.Raise(CraftEventKind.Joined, asm.id, spec.id, said, value);
    }

    /// <summary>The hardness (HV) of a piece's segment, as it is now (its body).</summary>
    private static double PieceHardness(CraftRules rules, CraftPiece p, int segment)
    {
        if (p == null || p.segments.Count == 0)
            return 0;
        MetalSegment s = p.segments[Math.Max(0, Math.Min(p.segments.Count - 1, segment))];
        return CraftMetallurgy.Hardness(rules, p.Profile(rules), s, s.body, s.thickness);
    }

    /// <summary>The segment a piece's eye (its largest hole) is in.</summary>
    private static int EyeSegment(CraftPiece p)
    {
        int at = 0;
        for (int i = 1; i < p.segments.Count; i++)
            if (p.segments[i].hole > p.segments[at].hole) at = i;
        return at;
    }

    /// <summary>
    /// Has nothing gone into the item yet - no part fitted, no supply added, no joint worked, nothing buffed (parts fitted
    /// and taken off again leave nothing either)? Such an item - a design started by mistake - may be put aside at any
    /// station (<see cref="CraftWorkshop.PutAside"/>): nothing is lost with it.
    /// </summary>
    public static bool IsEmpty(CraftPiece asm)
    {
        if (asm == null || asm.kind != CraftPieceKind.Assembly || asm.assembly == null || asm.assembly.finished)
            return false;
        AssemblyState a = asm.assembly;
        if (a.parts.Count > 0 || a.finish > 0)
            return false;
        foreach (CraftVariable v in a.supplies)
            if (v.value > 0) return false;
        foreach (JointState j in a.joints)
            if (j.done || j.holes > 0 || j.stitches > 0 || j.rivets > 0 || j.peen > 0 || j.turns > 0 || j.wedge > 0 || j.solder > 0 || j.pushes > 0 || j.damage > 0)
                return false;
        return true;
    }

    /// <summary>Is the joint made well enough to finish the item (not how good it is: the evaluator says that)?</summary>
    public static bool JointDone(CraftJointSpec spec, JointState j)
    {
        switch (spec.kind)
        {
            case CraftJointKind.Fit: return j.gap >= 0;
            case CraftJointKind.Peen: return j.peen >= 0.3;
            case CraftJointKind.Rivet: return j.rivets >= Math.Max(1, spec.count);
            case CraftJointKind.Stitch: return j.stitches >= Math.Max(1, (int)Math.Ceiling(spec.count * 0.8));
            case CraftJointKind.Wrap: return j.turns >= Math.Max(1, (int)Math.Ceiling(spec.count * 0.7));
            case CraftJointKind.Wedge: return j.wedge >= 0.3;
            // the head driven on: a pin only adds to what holds it (a pin fitted must be set too - Missing says so)
            case CraftJointKind.Socket: return j.wedge >= 0.3;
            // every buckle on: a rivet each, or its stitches
            case CraftJointKind.Buckle: return j.rivets >= Math.Max(1, spec.count) || j.stitches >= StitchesPerBuckle * Math.Max(1, spec.count);
            case CraftJointKind.Solder:
            case CraftJointKind.BezelSet:
            case CraftJointKind.ProngSet: return CraftJewelry.JointDone(spec, j);
            default: return true;
        }
    }

    /// <summary>
    /// Is the joint as made as the work can make it - every stitch, rivet and turn it takes, the pommel peened into its good
    /// band, the wedge home, a socketed head home and (<paramref name="pins"/>: the pins or rivets fitted for it, 0 = none)
    /// pinned through? (Finishing needs less: <see cref="JointDone"/>.) A fit joint is as it went on: it is never
    /// "complete" by working it.
    /// </summary>
    public static bool JointComplete(CraftJointSpec spec, JointState j, int pins = 0)
    {
        if (spec == null || j == null)
            return false;
        switch (spec.kind)
        {
            case CraftJointKind.Fit: return false;
            case CraftJointKind.Peen: return j.peen >= PeenGoodLow;
            case CraftJointKind.Rivet: return j.rivets >= Math.Max(1, spec.count);
            case CraftJointKind.Stitch: return j.stitches >= Math.Max(1, spec.count);
            case CraftJointKind.Wrap: return j.turns >= Math.Max(1, spec.count);
            case CraftJointKind.Wedge: return j.wedge >= WedgeGoodLow;
            case CraftJointKind.Socket: return j.wedge >= WedgeGoodLow && j.rivets >= Math.Max(0, pins);
            case CraftJointKind.Buckle: return JointDone(spec, j);
            default: return JointDone(spec, j);
        }
    }

    /// <summary>
    /// The pins (or rivets) a socket takes through it once its head is home: the design's pin part's count when that part
    /// is fitted to the item (<paramref name="pieceIn"/>: the piece in a role and place), else 0 - a pin left off is
    /// optional work not done. 0 for any other joint.
    /// </summary>
    public static int PinsWanted(CraftDesign d, CraftJointSpec spec, Func<string, int, CraftPiece> pieceIn)
    {
        if (d == null || spec == null || spec.kind != CraftJointKind.Socket || pieceIn == null)
            return 0;
        CraftComponentSpec pin = RivetPart(d, spec);
        return pin != null && pieceIn(pin.role, 0) != null ? RivetsWanted(spec, d) : 0;
    }

    /// <summary>The same for an assembly on the bench.</summary>
    public static int PinsWanted(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftJointSpec spec) =>
        ws == null || asm?.assembly == null ? 0 : PinsWanted(d, spec, (role, i) => ws.Piece(asm.assembly.PieceIn(role, i)));

    // ------------------------------------------------------------------ fasteners
    /// <summary>
    /// The fastener part a joint's rivets come from: a rivet or stud part the joint names (the studs of "Studs set"), else
    /// the design's rivets (a strap riveted on, a buckle riveted to its strap), else any rivet-like part. Null: none.
    /// </summary>
    public static CraftComponentSpec RivetPart(CraftDesign design, CraftJointSpec joint)
    {
        if (design == null)
            return null;
        if (joint != null)
            foreach (string role in new[] { joint.roleA, joint.roleB })
            {
                CraftComponentSpec c = design.Component(role);
                if (c != null && c.kind == CraftPieceKind.Fasteners && c.fastener != CraftFastenerKind.Buckle) return c;
            }
        CraftComponentSpec any = null;
        foreach (CraftComponentSpec c in design.components)
            if (c.kind == CraftPieceKind.Fasteners && c.fastener != CraftFastenerKind.Buckle)
            {
                if (c.fastener == CraftFastenerKind.Rivet) return c;
                any ??= c;
            }
        return any;
    }

    /// <summary>The fastener set a joint's rivets come from (fitted to the item), or null.</summary>
    public static CraftPiece RivetsOf(CraftWorkshop ws, CraftPiece asm, CraftDesign design, CraftJointSpec joint = null)
    {
        CraftComponentSpec c = RivetPart(design, joint);
        return c != null ? ws.Piece(asm.assembly.PieceIn(c.role)) : null;
    }

    /// <summary>
    /// How many rivets a joint takes: its count (one for each buckle); a socket the pins or rivets its design's pin part
    /// holds (<paramref name="d"/>: a socket pin, a halberd's two langet rivets - one without the design).
    /// </summary>
    public static int RivetsWanted(CraftJointSpec spec, CraftDesign d = null)
    {
        if (spec.kind != CraftJointKind.Socket)
            return Math.Max(1, spec.count);
        CraftComponentSpec pin = d != null ? RivetPart(d, spec) : null;
        return Math.Max(1, pin != null ? pin.fastenerCount : 1);
    }

    /// <summary>
    /// The holes a joint takes before its rivets or stitches: a hole for each rivet, the seam's holes for its stitches, a
    /// buckle's (one for a rivet, its stitches' for a buckle sewn on - with no rivets in the design), a socket's pins.
    /// </summary>
    public static int HolesWanted(CraftDesign d, CraftJointSpec spec)
    {
        switch (spec.kind)
        {
            case CraftJointKind.Stitch:
            case CraftJointKind.Rivet: return Math.Max(1, spec.count);
            case CraftJointKind.Socket: return RivetsWanted(spec, d);
            case CraftJointKind.Buckle: return RivetPart(d, spec) != null ? Math.Max(1, spec.count) : StitchesPerBuckle * Math.Max(1, spec.count);
            default: return 0;
        }
    }

    /// <summary>Does a rivet of this joint need a hole punched for it? A stud driven into wood does not (its point goes in).</summary>
    public static bool NeedsHoles(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftJointSpec spec)
    {
        CraftComponentSpec f = RivetPart(d, spec);
        return !(f != null && f.fastener == CraftFastenerKind.Stud && WoodIn(ws, asm, spec) && !LeatherIn(ws, asm, spec));
    }

    /// <summary>
    /// Merges another set of the same fasteners into a fitted one: the counts add; the metal, its grams, the size and how
    /// well they were made mix by count.
    /// </summary>
    public static void MergeFasteners(CraftPiece into, CraftPiece from)
    {
        if (into == null || from == null)
            return;
        int a = Math.Max(0, into.fasteners.count), b = Math.Max(0, from.fasteners.count), n = a + b;
        if (n <= 0)
            return;
        double Mix(double x, double y) => (x * a + y * b) / n;
        into.fasteners.massEach = Mix(into.fasteners.massEach, from.fasteners.massEach);
        into.fasteners.quality = Mix(into.fasteners.quality, from.fasteners.quality);
        into.fasteners.diameter = Mix(into.fasteners.diameter, from.fasteners.diameter);
        into.fasteners.length = Mix(into.fasteners.length, from.fasteners.length);
        foreach (CraftAmount x in from.composition)
            into.SetGrams(x.element, into.Grams(x.element) + x.grams);
        into.CompositionChanged();
        if (into.segments.Count > 0 && from.segments.Count > 0)
        {
            MetalSegment s = into.segments[0], o = from.segments[0];
            s.edge.temp = Mix(s.edge.temp, o.edge.temp);
            s.body.temp = Mix(s.body.temp, o.body.temp);
        }
        into.fasteners.count = n;
    }

    private static bool LeatherIn(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec) =>
        ws.Piece(asm.assembly.PieceIn(spec.roleA))?.kind == CraftPieceKind.Hide || ws.Piece(asm.assembly.PieceIn(spec.roleB))?.kind == CraftPieceKind.Hide;

    private static bool WoodIn(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec) =>
        ws.Piece(asm.assembly.PieceIn(spec.roleA))?.kind == CraftPieceKind.Wood || ws.Piece(asm.assembly.PieceIn(spec.roleB))?.kind == CraftPieceKind.Wood;

    // ------------------------------------------------------------------ supplies
    /// <summary>How many more of a supply part the item takes (0: it has all it needs, or it is not a supply).</summary>
    public static int SupplyRoom(CraftDesign d, CraftPiece asm, string role)
    {
        CraftComponentSpec c = d?.Component(role);
        if (c == null || !c.supply || asm?.assembly == null)
            return 0;
        return Math.Max(0, Math.Max(1, c.supplyCount) - (int)Math.Floor(asm.assembly.Supply(c.role) + 1e-6));
    }

    /// <summary>
    /// The grams and the worth of the supplies that went into an assembly in <paramref name="spec"/>'s role - the batting
    /// quilted in, the thread in its seams, the solder in a joint: as recorded when they went in
    /// (<see cref="CraftWorkshop.GramsSuffix"/>, <see cref="CraftWorkshop.WorthSuffix"/>), or - put in before what they
    /// were was kept - as many of the first supply of their kind.
    /// </summary>
    public static void SupplyIn(CraftRules rules, AssemblyState a, CraftComponentSpec spec, out double grams, out double worth)
    {
        grams = worth = 0;
        if (rules == null || a == null || spec == null || !spec.supply)
            return;
        double count = a.Supply(spec.role);
        if (count <= 0)
            return;
        grams = a.Supply(spec.role + CraftWorkshop.GramsSuffix);
        worth = a.Supply(spec.role + CraftWorkshop.WorthSuffix);
        if (grams > 0)
            return;
        CraftSupplyDefinition first = rules.FirstSupply(spec.supplyKind);
        grams = count * (first?.ItemGrams ?? 0);
        worth = count * (first?.price ?? 0);
    }

    /// <summary>The grams of every supply that went into an assembly (<see cref="SupplyIn"/>): what it weighs beyond its parts.</summary>
    public static double SuppliesMass(CraftRules rules, CraftPiece asm)
    {
        CraftDesign d = asm?.kind == CraftPieceKind.Assembly ? rules?.Design(asm.assembly.design) : null;
        double grams = 0;
        if (d != null)
            foreach (CraftComponentSpec c in d.components)
            {
                SupplyIn(rules, asm.assembly, c, out double g, out _);
                grams += g;
            }
        return grams;
    }

    /// <summary>The thread part of a design (its first Thread supply), or null.</summary>
    public static CraftComponentSpec ThreadPart(CraftDesign d)
    {
        if (d?.components != null)
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == CraftSupplyKind.Thread) return c;
        return null;
    }

    /// <summary>How thick the thickest leather or cloth of a joint is (mm; 0 when it joins none): what its thread must suit.</summary>
    public static double SeamThickness(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec) => SeamThickness(ws, asm, spec, out _);

    /// <summary>The same, and whether that thickest part is cloth (<paramref name="cloth"/>) or leather.</summary>
    public static double SeamThickness(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, out bool cloth)
    {
        double thick = 0;
        cloth = false;
        foreach (string role in new[] { spec.roleA, spec.roleB })
        {
            if (string.IsNullOrEmpty(role)) continue;
            CraftDesign d = ws.Rules.Design(asm.assembly.design);
            CraftComponentSpec c = d?.Component(role);
            for (int i = 0; c != null && i < Math.Max(1, c.count); i++)
            {
                CraftPiece p = ws.Piece(asm.assembly.PieceIn(role, i));
                if (p == null || p.kind != CraftPieceKind.Hide) continue;
                double t = CraftLeather.MeanThickness(ws.Rules, p);
                if (t <= thick) continue;
                thick = t;
                cloth = CraftTextiles.IsCloth(ws.Rules, p);
            }
        }
        return thick;
    }

    // ------------------------------------------------------------------ what work can be done
    /// <summary>
    /// Can this work be done on the joint now? Null when it can - with <paramref name="count"/> held to what is left of it
    /// (stitches to the holes punched and the seam's length, holes and turns to the joint's) - else why not, in words. The
    /// workshop refuses with these words; the bench shows them before the hands try. <paramref name="piercing"/>: holes the
    /// needle may still pierce as it sews (<see cref="CraftWorkshop.JointWork"/>), counted with those punched.
    /// </summary>
    public static string WhyNot(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftJointSpec spec, CraftJointWork work, ref int count, int piercing = 0)
    {
        JointState j = asm.assembly.Joint(spec.id);
        if (j == null)
            return "There is no such joint.";
        string name = spec.Name.ToLowerInvariant();
        switch (work)
        {
            case CraftJointWork.Punch:
            {
                int want = HolesWanted(d, spec);
                if (want <= 0)
                    return $"The {name} is not punched.";
                if (spec.kind == CraftJointKind.Socket && j.holes < want && j.wedge < WedgeGoodLow)
                    return SocketFirst;
                if (j.holes >= want)
                    return want == 1 ? "The hole is punched." : $"All {want} holes are punched.";
                count = Math.Max(0, Math.Min(count, want - j.holes));
                return null;
            }
            case CraftJointWork.Stitch:
            {
                CraftComponentSpec thread = ThreadPart(d);
                if (thread == null)
                    return spec.kind == CraftJointKind.Buckle ? "There is no thread in this design: rivet the buckle on." : "There is no thread in this design.";
                if (asm.assembly.Supply(thread.role) <= 0)
                    return "Add thread first.";
                int want = spec.kind == CraftJointKind.Buckle ? StitchesPerBuckle * Math.Max(1, spec.count) : Math.Max(1, spec.count);
                if (j.stitches >= want)
                    return spec.kind == CraftJointKind.Buckle ? "Every buckle is sewn on." : $"The {name} is sewn all along.";
                int room = j.holes + Math.Max(0, piercing) - j.stitches;
                if (room <= 0)
                    return j.holes <= 0 ? "Punch the holes first." : "The holes are all stitched: punch more.";
                count = Math.Max(0, Math.Min(count, Math.Min(room, want - j.stitches)));
                return null;
            }
            case CraftJointWork.Wrap:
            {
                // a bowstring (a thread supply the joint names) must be there to be strung
                foreach (string role in new[] { spec.roleA, spec.roleB })
                {
                    CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
                    if (c != null && c.supply && asm.assembly.Supply(c.role) <= 0)
                        return $"Add the {c.Name.ToLowerInvariant()} first.";
                }
                int want = Math.Max(1, spec.count);
                if (j.turns >= want)
                    return $"The {name} is wound all along.";
                count = Math.Max(0, Math.Min(count, want - j.turns));
                return null;
            }
            case CraftJointWork.Rivet:
            {
                CraftComponentSpec part = RivetPart(d, spec);
                if (part == null)
                    return spec.kind == CraftJointKind.Buckle ? "There are no rivets in this design: sew the buckle on." : "There are no rivets in this design.";
                CraftPiece set = ws.Piece(asm.assembly.PieceIn(part.role));
                string what = part.fastener == CraftFastenerKind.Stud ? "studs" : "rivets";
                if (set == null)
                    return $"Fit the {part.Name.ToLowerInvariant()} first.";
                int want = RivetsWanted(spec, d);
                if (j.rivets >= want)
                    return spec.kind == CraftJointKind.Buckle ? "Every buckle is riveted on." : spec.kind == CraftJointKind.Socket && want == 1 ? "The pin is set." : $"All {want} {what} are set.";
                if (spec.kind == CraftJointKind.Socket && j.wedge < WedgeGoodLow)
                    return SocketFirst;
                if (set.fasteners.Left <= 0)
                    return $"No {what} left in the set: fit another set ({j.rivets} of {want} set).";
                if (NeedsHoles(ws, asm, d, spec) && j.holes < j.rivets + 1)
                    return $"Punch a hole for it first ({j.holes} of {want} punched).";
                return null;
            }
            case CraftJointWork.Peen:
            {
                if (spec.kind == CraftJointKind.Fit)
                {
                    CraftPiece part = ws.Piece(asm.assembly.PieceIn(spec.roleB));
                    if (part != null && part.kind != CraftPieceKind.Metal)
                        return $"The {d.Component(spec.roleB)?.Name.ToLowerInvariant() ?? "part"} is not peened: it fits as it was {(part.kind == CraftPieceKind.Wood ? "bored" : "made")}.";
                    if (j.gap < 0)
                        return "It is too tight: take it off and file the slot.";
                    if (j.gap <= 0.05)
                        return "It sits tight on the tang: there is no gap to close.";
                    if (j.peenClosed >= PeenCloseBudget - 1e-6)
                        return "The slot is closed onto the tang as far as peening goes: the play left stays.";
                    return null;
                }
                if (j.peen >= 2)
                    return "The tang's end is peened flat: there is nothing more to spread.";
                foreach (CraftComponentSpec c in d.components)
                {
                    if (c.optional || c.supply || c.function != CraftComponentFunction.Guard && c.function != CraftComponentFunction.Grip) continue;
                    for (int i = 0; i < Math.Max(1, c.count); i++)
                        if (ws.Piece(asm.assembly.PieceIn(c.role, i)) == null)
                            return $"Fit the {c.Name.ToLowerInvariant()} first: the peened pommel closes the hilt.";
                }
                CraftPiece blade = ws.Piece(asm.assembly.PieceIn(spec.roleA));
                if (!MeasureTang(Target(d, spec.roleA), blade, out _, out double tang, out _))
                    return "The blade has no tang: there is nothing to peen.";
                double need = OnTang(d, (role, i) => ws.Piece(asm.assembly.PieceIn(role, i)));
                if (tang < need + PeenAllowance)
                    return $"The tang ({tang:0} mm) does not come through the grip and the pommel ({need:0} mm, and {PeenAllowance:0} to peen over): fit a shorter grip or pommel, or forge a longer tang.";
                return null;
            }
            case CraftJointWork.Wedge:
                if (spec.kind == CraftJointKind.Socket)
                {
                    // home and pinned through (or its hole punched through socket and shaft as they sit), the head goes no
                    // further on (a head pinned before it was home - an older history - is still driven home)
                    if (j.wedge >= WedgeGoodLow && j.rivets > 0)
                        return "It is pinned: the head goes no further on.";
                    if (j.wedge >= WedgeGoodLow && j.holes > 0)
                        return "The pin's hole is through the socket and the shaft: driven further, they would no longer line up.";
                    return j.wedge >= 2 ? "The head is driven all the way on." : null;
                }
                return j.wedge >= 2 ? "The wedge is driven all the way." : null;
            default:
                return null;
        }
    }

    /// <summary>A socket's pin goes through the socket and the shaft as they sit once the head is home: not before.</summary>
    private const string SocketFirst = "Drive the head home on the shaft first: the pin goes through the socket and the shaft as they sit.";

    /// <summary>
    /// Will <paramref name="p"/> go on in the place <paramref name="spec"/> (<paramref name="index"/>)? Null when it does;
    /// else why not: a guard's slot must clear the tang at the shoulder (a blade without a tang or a shoulder takes none),
    /// a grip's hole the tang along it, a pommel's hole the tang's end - and the tang must come through the grip and the
    /// pommel to be peened; a haft must go into the head's eye (a little tight is a good drive). Nothing more goes on a
    /// tang once the pommel is peened.
    /// </summary>
    public static string WhyNotOn(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftComponentSpec spec, int index, CraftPiece p)
    {
        CraftPiece In(string role, int i) => role == spec.role && i == index ? p : ws.Piece(asm.assembly.PieceIn(role, i));
        CraftJointSpec peen = PeenJoint(d);
        if (peen != null && OnTheTang(spec, peen) && (asm.assembly.Joint(peen.id)?.peen ?? 0) > 0 && spec.role != peen.roleB)
            return "The pommel is peened on: the hilt is closed. Take the pommel off first.";
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.roleA != spec.role && j.roleB != spec.role) continue;
            CraftPiece a = In(j.roleA, 0), b = string.IsNullOrEmpty(j.roleB) ? null : In(j.roleB, 0);
            if (a == null || b == null) continue;
            string bName = d.Component(j.roleB)?.Name.ToLowerInvariant() ?? j.roleB;
            switch (j.kind)
            {
                case CraftJointKind.Fit:
                case CraftJointKind.Peen:
                {
                    CraftShapeTarget target = Target(d, j.roleA);
                    if (target == null || target.tangTo <= 0) continue;
                    bool tang = MeasureTang(target, a, out _, out _, out bool shoulder);
                    if (!tang)
                        return "The blade has no tang to fit it on: forge a tang at its end, narrower than the blade.";
                    CraftComponentSpec bs = d.Component(j.roleB);
                    bool guard = bs != null && bs.function == CraftComponentFunction.Guard;
                    if (guard && !shoulder)
                        return "The blade has no shoulder: the guard would slide down it. Forge the tang narrower than the blade.";
                    double eye = Eye(b);
                    if (eye <= 0)
                        return b.kind == CraftPieceKind.Wood ? $"The {bName} has no hole for the tang: bore one first." : $"The {bName} has no {(guard ? "slot" : "hole")} for the tang: drift one first.";
                    double size = TangSize(target, a, Where(d, j.roleB), out _);
                    if (eye < size)
                        return guard ? $"It will not go on: the slot is {eye:0.#} mm, the tang at the shoulder {size:0.#} mm. File the slot bigger."
                            : $"It will not go on: the {bName}'s hole is {eye:0.#} mm, the tang {(j.kind == CraftJointKind.Peen ? "at its end" : "along it")} {size:0.#} mm. {(b.kind == CraftPieceKind.Wood ? "Bore it wider" : "Drift it wider")}.";
                    break;
                }
                case CraftJointKind.Wedge:
                case CraftJointKind.Socket:
                {
                    CraftPiece head = a.kind == CraftPieceKind.Wood ? b : a;
                    if (Eye(head) <= 0)
                        return "The head has no eye for the haft: drift one first.";
                    double gap = GapOf(d, j, a, b);
                    if (gap < -3)
                        return $"It will not go on: the haft is {-gap:0.#} mm too thick. Rasp it down.";
                    break;
                }
            }
        }
        // The tang must come through what goes on it, with enough left to peen over the pommel.
        if (peen != null && (spec.role == peen.roleA || spec.function == CraftComponentFunction.Grip || spec.function == CraftComponentFunction.Counterweight))
        {
            CraftPiece blade = In(peen.roleA, 0);
            double need = OnTang(d, In);
            if (blade != null && need > 0 && MeasureTang(Target(d, peen.roleA), blade, out _, out double tang, out _) && tang < need + PeenAllowance)
                return $"The tang ({tang:0} mm) is too short to come through the grip and the pommel ({need:0} mm, and {PeenAllowance:0} to peen over): fit a shorter grip or pommel, or a blade with a longer tang.";
        }
        return null;
    }

    /// <summary>
    /// Is a part of the hilt closed by the peened pommel (<paramref name="peen"/>): the blade whose tang it is peened over,
    /// a guard, a grip, a counterweight on that tang?
    /// </summary>
    private static bool OnTheTang(CraftComponentSpec spec, CraftJointSpec peen) =>
        spec != null && peen != null && (spec.role == peen.roleA || spec.function == CraftComponentFunction.Guard
                                         || spec.function == CraftComponentFunction.Grip || spec.function == CraftComponentFunction.Counterweight);

    /// <summary>
    /// Why a fitted part cannot come off now (null: it can): a setting holds its stone (the stone comes out first); the
    /// blade, a guard or a grip is held in the hilt the peened pommel closes (the pommel comes off first).
    /// </summary>
    public static string HeldOn(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftComponentSpec spec)
    {
        if (d == null || spec == null)
            return null;
        if (spec.function == CraftComponentFunction.Setting)
            foreach (CraftJointSpec j in d.joints)
            {
                if (j.kind != CraftJointKind.BezelSet && j.kind != CraftJointKind.ProngSet || j.roleA != spec.role && j.roleB != spec.role) continue;
                string other = j.roleA == spec.role ? j.roleB : j.roleA;
                if (ws.Piece(asm.assembly.PieceIn(other))?.kind == CraftPieceKind.Gem)
                    return "Take the stone out of its setting first.";
            }
        CraftJointSpec peen = PeenJoint(d);
        if (peen != null && spec.role != peen.roleB && OnTheTang(spec, peen) && (asm.assembly.Joint(peen.id)?.peen ?? 0) > 0)
            return "The pommel is peened over the tang: take the pommel off first.";
        return null;
    }

    // ------------------------------------------------------------------ what is left to do
    /// <summary>
    /// Is a joint of the design made on this item: are the parts it joins there (<paramref name="hasPart"/>: is a piece
    /// fitted in that role)? A supply (the thread, the solder) always counts as there. A joint to a part that was left
    /// off - an optional grip wrap, studs not added - is not made, so it does not hold up the finish (it cannot be worked
    /// either: <see cref="CraftWorkshop.JointWork"/> asks for the part). One rule for the finish, the guide and what an
    /// unfinished item still lacks.
    /// </summary>
    public static bool JointApplies(CraftDesign d, CraftJointSpec js, Func<string, bool> hasPart)
    {
        if (d == null || js == null)
            return false;
        bool There(string role) => d.Component(role)?.supply == true || hasPart != null && hasPart(role);
        return There(js.roleA) && (string.IsNullOrEmpty(js.roleB) || There(js.roleB));
    }

    /// <summary>The same for an assembly on the bench: are the parts the joint joins fitted to it?</summary>
    public static bool JointApplies(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftJointSpec js) =>
        ws != null && asm != null && asm.assembly != null && JointApplies(d, js, role => ws.Piece(asm.assembly.PieceIn(role)) != null);

    /// <summary>
    /// What still has to be done before the item can be finished (empty = it can): every required part, and the joints
    /// between the parts that are there (<see cref="JointApplies(CraftWorkshop, CraftPiece, CraftDesign, CraftJointSpec)"/>).
    /// </summary>
    public static List<string> Missing(CraftWorkshop ws, CraftPiece asm) => Missing(ws.Rules, asm, (role, i) => ws.Piece(asm.assembly.PieceIn(role, i)));

    /// <summary>
    /// The same rule for an item wherever it is - on a bench, or carried with its parts (<paramref name="pieceIn"/>: the
    /// piece in a role and place, or null): every required part (a buckle set holding as many buckles as the design
    /// takes), every supply, and every joint between the parts that are there; jewellery is buffed at the jeweller's bench.
    /// </summary>
    public static List<string> Missing(CraftRules rules, CraftPiece asm, Func<string, int, CraftPiece> pieceIn)
    {
        var list = new List<string>();
        CraftDesign d = rules?.Design(asm?.assembly?.design);
        if (d == null)
        {
            list.Add("Unknown design.");
            return list;
        }
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional) continue;
            if (c.supply)
            {
                if (asm.assembly.Supply(c.role) < Math.Max(1, c.supplyCount)) list.Add($"{c.Name}: not added yet.");
                continue;
            }
            for (int i = 0; i < c.count; i++)
                if (pieceIn(c.role, i) == null)
                    list.Add(c.count > 1 ? $"{c.Name} {i + 1}: missing." : $"{c.Name}: missing.");
            if (c.kind == CraftPieceKind.Fasteners && c.fastener == CraftFastenerKind.Buckle)
            {
                CraftPiece set = pieceIn(c.role, 0);
                if (set != null && set.fasteners.count < Math.Max(1, c.fastenerCount))
                    list.Add($"{c.Name}: {set.fasteners.count} of {c.fastenerCount}: fit another set.");
            }
        }
        foreach (CraftJointSpec js in d.joints)
        {
            JointState j = asm.assembly.Joint(js.id);
            if (!JointApplies(d, js, role => pieceIn(role, 0) != null)) continue; // a part left off (or still to fit, listed above): no joint to make
            if (j == null || !JointDone(js, j))
                list.Add($"{js.Name}: not finished.");
            else if (PinLeft(d, js, j, pieceIn, out CraftComponentSpec pin, out int pins))
                list.Add($"{js.Name}: set its {pin.Name.ToLowerInvariant()} ({j.rivets} of {pins}), or take {(pins == 1 ? "it" : "them")} off.");
        }
        if (d.kind == CraftDesignKind.Jewelry && list.Count == 0 && asm.assembly.finish <= 0)
            list.Add("Buff it at the jeweller's bench first (Jeweller → Finish).");
        return list;
    }

    /// <summary>
    /// Is a socket driven on but its pins - fitted to the item - not all set (<paramref name="pin"/>: the pin part,
    /// <paramref name="pins"/>: how many it takes)? A part fitted is joined as any other: set them, or take them off.
    /// </summary>
    public static bool PinLeft(CraftDesign d, CraftJointSpec js, JointState j, Func<string, int, CraftPiece> pieceIn, out CraftComponentSpec pin, out int pins)
    {
        pins = PinsWanted(d, js, pieceIn);
        pin = pins > 0 ? RivetPart(d, js) : null;
        return pin != null && j != null && j.rivets < pins;
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ assembly
    /// <summary>Starts putting an item of <paramref name="design"/> together on the bench. Returns the assembly's piece id.</summary>
    public string StartAssembly(string design, out string reason)
    {
        reason = "";
        CraftDesign d = Rules.Design(design);
        if (!Has(CraftTools.AssemblyBench) && !Has(CraftTools.LeatherBench))
        {
            Refuse("There is no bench to assemble on here.", out reason);
            return null;
        }
        if (d == null)
        {
            Refuse("There is no such design.", out reason);
            return null;
        }
        if (!CraftAssembly.MadeAt(Rules, d, Tools, out string missing))
        {
            Refuse($"{d.Name} is not made here: it needs {missing}.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.StartAssembly, id = d.id };
        Record(op);
        return op.other;
    }

    private void ApplyStartAssembly(CraftOperation op)
    {
        CraftDesign d = Rules.Design(op.id);
        if (d == null)
            return;
        CraftPiece a = Create(op, CraftPieceKind.Assembly, $"{d.Name} being assembled");
        a.assembly.design = d.id;
        foreach (CraftJointSpec j in d.joints)
            if (j != null) a.assembly.joints.Add(new JointState { joint = j.id });
        TakeMeasurements(a);
        a.Record(Elapsed, op.kind, $"Started {CraftText.A(d.Name)} {d.Name.ToLowerInvariant()}");
    }

    /// <summary>
    /// Puts aside an item nothing has gone into yet (<see cref="CraftAssembly.IsEmpty"/>: a design started by mistake) -
    /// at any station, since nothing is lost with it: the measurement it took when it was started waits again for the
    /// next item started (unless a newer one of that kind already waits). Anything else is taken, not thrown away.
    /// </summary>
    public bool PutAside(string id, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(id);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("Only an item nothing has gone into yet can be put aside: take it with you.", out reason, id);
        if (!CraftAssembly.IsEmpty(asm))
            return Refuse(asm.assembly.finished ? "It is finished: take it with you." : "Its work is in it already: take it with you, or finish it.", out reason, id);
        AssemblyState a = asm.assembly;
        if (a.measured && !WaitingMeasurement(CraftJewelry.IsFinger(a.fitSize), out _, out _))
            Record(new CraftOperation { kind = CraftOpKind.MeasureFit, piece = "", a = (float)CraftMath.Clamp01(a.fit), b = (float)Math.Max(0, a.fitSize), flag = true });
        return RemovePiece(id, out reason) != null;
    }

    /// <summary>
    /// Puts a piece in a role of an assembly (the <paramref name="index"/>th of a role with several). Supplies (padding,
    /// thread) are added with <paramref name="piece"/> empty and <paramref name="supplyCount"/> (the station takes the
    /// items) - never more than the design takes; <paramref name="supply"/> names what they are (a supply id), so the item
    /// weighs and is worth what went into it (unnamed: the first supply of its kind). Another set of fasteners goes in with
    /// the set already there.
    /// </summary>
    public bool Attach(string assembly, string role, int index, string piece, out string reason, int supplyCount = 0, double supplyStrength = 0, string supply = null)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is nothing being assembled.", out reason);
        if (asm.assembly.finished)
            return Refuse("It is already finished.", out reason, assembly);
        CraftDesign d = Rules.Design(asm.assembly.design);
        CraftComponentSpec spec = d?.Component(role);
        if (spec == null)
            return Refuse($"{d?.Name ?? "It"} has no such part.", out reason, assembly);
        if (spec.supply)
        {
            int room = CraftAssembly.SupplyRoom(d, asm, role);
            if (room <= 0)
                return Refuse($"It has all the {spec.Name.ToLowerInvariant()} it needs.", out reason, assembly);
            supplyCount = Math.Min(supplyCount, room);
            if (supplyCount <= 0)
                return Refuse("Nothing to add.", out reason, assembly);
            Record(new CraftOperation { kind = CraftOpKind.Attach, piece = assembly, id = role, i1 = supplyCount, a = (float)Math.Max(0, supplyStrength), other = Rules.Supply(supply)?.id ?? "" });
            return true;
        }
        if (index < 0 || index >= spec.count)
            return Refuse("There is no such place.", out reason, assembly);
        CraftPiece there = Piece(asm.assembly.PieceIn(role, index));
        bool adding = there != null && spec.kind == CraftPieceKind.Fasteners; // another set goes in with the one there
        if (there != null && !adding)
            return Refuse($"The {spec.Name.ToLowerInvariant()} is already in place.", out reason, assembly);
        CraftPiece p = Piece(piece);
        if (p == null || p == asm || p == there)
            return Refuse("There is no such piece here.", out reason, assembly);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, piece);
        if (!Compatible(spec, p, out reason))
            return Refuse(reason, out reason, piece);
        if (adding && there.fasteners.kind != p.fasteners.kind)
            return Refuse($"Those are {p.fasteners.kind.ToString().ToLowerInvariant()}s; the set fitted is {there.fasteners.kind.ToString().ToLowerInvariant()}s.", out reason, piece);
        if (!CraftJewelry.PartFits(this, asm, d, spec, p, out reason))
            return Refuse(reason, out reason, piece);
        if (p.MaxTemperature > 200 && (spec.kind != CraftPieceKind.Fasteners))
        {
            foreach (CraftComponentSpec c in d.components)
                if (c.kind == CraftPieceKind.Hide || c.kind == CraftPieceKind.Wood)
                    return Refuse("It is too hot: it would scorch the leather and wood.", out reason, piece);
        }
        // It must go on: a guard's slot over the tang, a haft into the eye, the tang through the hilt.
        string why = adding ? null : CraftAssembly.WhyNotOn(this, asm, d, spec, index, p);
        if (why != null)
            return Refuse(why, out reason, piece);
        Record(new CraftOperation { kind = CraftOpKind.Attach, piece = assembly, id = role, i0 = index, other = piece });
        return true;
    }

    private bool Compatible(CraftComponentSpec spec, CraftPiece p, out string reason)
    {
        reason = "";
        if (spec.kind != p.kind)
        {
            reason = $"The {spec.Name.ToLowerInvariant()} must be {KindName(spec.kind)}.";
            return false;
        }
        switch (p.kind)
        {
            case CraftPieceKind.Hide:
            {
                bool cloth = CraftTextiles.IsCloth(Rules, p);
                CraftFabric fabric = spec.leather?.fabric ?? CraftFabric.Leather;
                if (fabric == CraftFabric.Leather && cloth) { reason = $"The {spec.Name.ToLowerInvariant()} is leather, not cloth."; return false; }
                if (fabric == CraftFabric.Cloth && !cloth) { reason = $"The {spec.Name.ToLowerInvariant()} is cut from cloth."; return false; }
            }
                if (!string.IsNullOrEmpty(spec.leather?.pattern) && p.hide.pattern != spec.leather.pattern)
                {
                    reason = $"That was not cut with the {Rules.Pattern(spec.leather.pattern)?.Name.ToLowerInvariant() ?? spec.leather.pattern} pattern.";
                    return false;
                }
                break;
            case CraftPieceKind.Fasteners:
                if (spec.fastener == CraftFastenerKind.Buckle ? p.fasteners.kind != CraftFastenerKind.Buckle : p.fasteners.kind == CraftFastenerKind.Buckle)
                {
                    reason = spec.fastener == CraftFastenerKind.Buckle ? "That needs a buckle." : "That needs rivets or studs.";
                    return false;
                }
                // any set will do: sets add up (a joint takes rivets until its set runs out; buckles count when it is finished)
                if (p.fasteners.Left < 1)
                {
                    reason = "The set is empty.";
                    return false;
                }
                break;
            case CraftPieceKind.Assembly:
                reason = "An unfinished item cannot be a part.";
                return false;
        }
        return true;
    }

    private static string KindName(CraftPieceKind k) => k switch
    {
        CraftPieceKind.Hide => "leather",
        CraftPieceKind.Wood => "wood",
        CraftPieceKind.Fasteners => "fasteners",
        CraftPieceKind.Metal => "metal",
        CraftPieceKind.Gem => "a gemstone",
        _ => k.ToString().ToLowerInvariant(),
    };

    /// <summary>A supply's strength is kept beside its count, under the role's name with this suffix.</summary>
    public const string StrengthSuffix = "@strength";
    /// <summary>...and the grams and the worth of what went in (<see cref="CraftSupplyDefinition.ItemGrams"/>, its price), with these.</summary>
    public const string GramsSuffix = "@grams", WorthSuffix = "@worth";

    private static void SetVariable(List<CraftVariable> list, string name, double value)
    {
        foreach (CraftVariable v in list)
            if (v.name == name) { v.value = value; return; }
        list.Add(new CraftVariable(name, value));
    }

    private void ApplyAttach(CraftOperation op)
    {
        CraftPiece asm = Piece(op.piece);
        if (asm == null)
            return;
        CraftDesign d = Rules.Design(asm.assembly.design);
        CraftComponentSpec spec = d?.Component(op.id);
        if (spec == null)
            return;
        if (spec.supply)
        {
            // How strong what went in is (thread: spun fine or coarse, linen or silk), as a running mean by count.
            if (op.a > 0)
            {
                double had = asm.assembly.Supply(spec.role), mean = asm.assembly.Supply(spec.role + StrengthSuffix);
                SetVariable(asm.assembly.supplies, spec.role + StrengthSuffix, had > 0 && mean > 0 ? (mean * had + op.a * op.i1) / (had + op.i1) : op.a);
            }
            // What it weighs and is worth: the supply named (else the first of its kind), by the count that went in - added
            // to what went in before (put in before this was kept, that counts as the first of its kind: CraftAssembly.SupplyIn).
            CraftSupplyDefinition s = Rules.Supply(op.other) ?? Rules.FirstSupply(spec.supplyKind);
            if (s != null && op.i1 > 0)
            {
                CraftAssembly.SupplyIn(Rules, asm.assembly, spec, out double grams, out double worth);
                SetVariable(asm.assembly.supplies, spec.role + GramsSuffix, grams + s.ItemGrams * op.i1);
                SetVariable(asm.assembly.supplies, spec.role + WorthSuffix, worth + s.price * op.i1);
            }
            SetVariable(asm.assembly.supplies, spec.role, asm.assembly.Supply(spec.role) + op.i1);
            asm.Record(Elapsed, op.kind, $"Added {spec.Name.ToLowerInvariant()}");
            return;
        }
        CraftPiece p = Piece(op.other);
        if (p == null)
            return;
        CraftPiece there = Piece(asm.assembly.PieceIn(op.id, op.i0));
        if (there != null && there != p && spec.kind == CraftPieceKind.Fasteners && p.kind == CraftPieceKind.Fasteners)
        {
            // Another set goes in with the one there: they are one set now.
            int added = p.fasteners.count;
            CraftAssembly.MergeFasteners(there, p);
            Destroy(p, "Added to the set on the item.");
            asm.Record(Elapsed, op.kind, $"Added {added} to the {spec.Name.ToLowerInvariant()}");
            Raise(CraftEventKind.Joined, asm.id, spec.role, "");
            return;
        }
        asm.assembly.parts.Add(new AssemblyPart { role = op.id, index = op.i0, piece = p.id });
        p.location = CraftLocation.Assembled;
        p.parent = asm.id;
        // It is judged as this design's part from now on (and keeps that purpose, and the part's edges, if it comes off again).
        p.intentDesign = d.id;
        p.intentRole = spec.role;
        p.edges = p.IntendedEdges(Rules, p.edges);
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.roleA != op.id && j.roleB != op.id) continue;
            JointState js = asm.assembly.Joint(j.id);
            if (js == null) continue;
            if (j.kind == CraftJointKind.Fit || j.kind == CraftJointKind.Wedge || j.kind == CraftJointKind.Socket)
            {
                js.gap = CraftAssembly.FitGap(this, asm, j);
                js.peenClosed = 0;
            }
            js.done = CraftAssembly.JointDone(j, js);
        }
        asm.Record(Elapsed, op.kind, $"Fitted the {spec.Name.ToLowerInvariant()}");
        Raise(CraftEventKind.Joined, asm.id, spec.role, "");
    }

    /// <summary>
    /// Takes a part off an assembly (its joints are undone: rivets drilled out and lost, stitches cut - with some damage; a
    /// set of rivets comes off with those still to set, every one set from it drilled out). What was added (thread,
    /// padding, solder) stays in it; a setting holding its stone, and the blade, a guard or a grip in a hilt closed by its
    /// peened pommel, come off only after the stone or the pommel.
    /// </summary>
    public bool Detach(string assembly, string role, int index, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is no such part on it.", out reason, assembly);
        CraftDesign d = Rules.Design(asm.assembly.design);
        CraftComponentSpec spec = d?.Component(role);
        if (spec != null && spec.supply)
            return Refuse($"The {spec.Name.ToLowerInvariant()} is in it: what was added cannot be taken back out.", out reason, assembly);
        if (asm.assembly.PieceIn(role, index) == null)
            return Refuse("There is no such part on it.", out reason, assembly);
        if (asm.assembly.finished)
            return Refuse("It is finished.", out reason, assembly);
        string held = CraftAssembly.HeldOn(this, asm, d, spec);
        if (held != null)
            return Refuse(held, out reason, assembly);
        Record(new CraftOperation { kind = CraftOpKind.Detach, piece = assembly, id = role, i0 = index });
        return true;
    }

    private void ApplyDetach(CraftOperation op)
    {
        CraftPiece asm = Piece(op.piece);
        if (asm == null)
            return;
        string id = asm.assembly.PieceIn(op.id, op.i0);
        asm.assembly.parts.RemoveAll(x => x.role == op.id && x.index == op.i0);
        CraftPiece p = Piece(id);
        if (p != null)
        {
            p.location = CraftLocation.Air;
            p.parent = "";
        }
        CraftDesign d = Rules.Design(asm.assembly.design);
        if (d != null)
            foreach (CraftJointSpec j in d.joints)
            {
                JointState js = asm.assembly.Joint(j.id);
                if (js == null) continue;
                bool named = j.roleA == op.id || j.roleB == op.id;
                // A set of rivets taken off: every rivet set from it is drilled out, wherever it went (the holes stay).
                CraftComponentSpec rivetPart = js.rivets > 0 ? CraftAssembly.RivetPart(d, j) : null;
                bool drilled = !named && p != null && p.kind == CraftPieceKind.Fasteners && rivetPart != null && rivetPart.role == op.id;
                if (!named && !drilled) continue;
                if (rivetPart != null)
                {
                    // the rivets drilled out are lost: they leave the set they were set from (older saves never counted them in it)
                    CraftPiece from = rivetPart.role == op.id ? p : Piece(asm.assembly.PieceIn(rivetPart.role));
                    if (from != null && from.kind == CraftPieceKind.Fasteners)
                    {
                        int lost = Math.Min(js.rivets, from.fasteners.used);
                        from.fasteners.count = Math.Max(0, from.fasteners.count - lost);
                        from.fasteners.used -= lost;
                    }
                }
                if (drilled)
                {
                    js.rivets = 0;
                    js.rivetQuality = 0;
                    js.done = CraftAssembly.JointDone(j, js);
                    continue;
                }
                bool hadWork = js.rivets > 0 || js.stitches > 0 || js.peen > 0 || js.wedge > 0 || js.solder > 0 || js.pushes > 0;
                hadWork |= js.holes > 0 || js.turns > 0;
                bool set = js.pushes > 0 && js.security > 0.3;
                js.rivets = 0;
                js.rivetQuality = 0;
                js.stitches = 0;
                js.peen = 0;
                js.wedge = 0;
                js.turns = 0;
                // the jeweller's: a part taken out breaks its solder, a stone taken out is unset
                js.solder = 0;
                js.overheat = 0;
                js.security = 0;
                js.pushes = 0;
                js.tilt = 0;
                // nothing of the old joint stays to be counted for the next part: its holes, its pull, its wear, its blows
                js.holes = 0;
                js.spacingError = 0;
                js.alignError = 0;
                js.tension = 0;
                js.tensionSpread = 0;
                js.wrapTension = 0;
                js.wrapSpread = 0;
                js.damage = 0;
                js.gap = 0;
                js.peenClosed = 0;
                js.blows = 0;
                js.blowQuality = 0;
                js.done = false;
                if (hadWork && p != null)
                {
                    if (p.kind == CraftPieceKind.Hide)
                        foreach (HideCell c in p.hide.cells) if (c.present) c.damage = Math.Min(1, c.damage + 0.03);
                    if (p.kind == CraftPieceKind.Gem && set)
                        p.gem.chips = CraftMath.Clamp01(p.gem.chips + 0.04); // pried out of its setting
                    p.Record(Elapsed, op.kind, "Taken off an unfinished item (its joints undone)");
                }
            }
        // a set of fasteners comes off with what was left in it (anything set from it is drilled out above)
        if (p != null && p.kind == CraftPieceKind.Fasteners)
        {
            p.fasteners.count = p.fasteners.Left;
            p.fasteners.used = 0;
        }
    }

    /// <summary>
    /// Works a joint: punch holes (<paramref name="count"/>, spacing error <paramref name="a"/>, alignment error <paramref name="b"/>),
    /// stitch (<paramref name="count"/> stitches, tension <paramref name="a"/>, unevenness <paramref name="b"/>), set a rivet
    /// (<paramref name="count"/> strikes of force <paramref name="a"/>), peen or wedge (<paramref name="count"/> strikes of
    /// force <paramref name="a"/>), wrap (<paramref name="count"/> turns, tension <paramref name="a"/>, unevenness <paramref name="b"/>).
    /// What the joint has room for is done (stitches up to the last hole punched); what cannot be done is refused with the
    /// reason (<see cref="CraftAssembly.WhyNot"/>). The needle's stitches (<paramref name="pierce"/>) pierce the holes they
    /// still need as they are drawn - with <paramref name="spacing"/> and <paramref name="align"/> errors, as a punch -
    /// so one pull is one piece of work; <paramref name="pastCut"/>: how many of the stitches were pulled so hard they cut
    /// (-1: not counted - judged by their mean pull <paramref name="a"/>).
    /// </summary>
    public bool JointWork(string assembly, string joint, CraftJointWork work, int count, double a, double b, out string reason,
                          bool pierce = false, double spacing = 0, double align = 0, int pastCut = -1)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is nothing being assembled.", out reason);
        if (asm.assembly.finished)
            return Refuse("It is already finished.", out reason, assembly); // its seams and joints are as they were judged
        CraftDesign d = Rules.Design(asm.assembly.design);
        CraftJointSpec spec = d?.Joint(joint);
        if (spec == null)
            return Refuse("There is no such joint.", out reason, assembly);
        if (asm.assembly.PieceIn(spec.roleA) == null && !(d.Component(spec.roleA)?.supply ?? false))
            return Refuse($"Put the {d.Component(spec.roleA)?.Name.ToLowerInvariant() ?? spec.roleA} in place first.", out reason, assembly);
        if (!string.IsNullOrEmpty(spec.roleB) && asm.assembly.PieceIn(spec.roleB) == null && !(d.Component(spec.roleB)?.supply ?? false))
            return Refuse($"Put the {d.Component(spec.roleB)?.Name.ToLowerInvariant() ?? spec.roleB} in place first.", out reason, assembly);
        bool fits = false;
        switch (spec.kind)
        {
            case CraftJointKind.Rivet: fits = work == CraftJointWork.Punch || work == CraftJointWork.Rivet; break;
            case CraftJointKind.Stitch: fits = work == CraftJointWork.Punch || work == CraftJointWork.Stitch; break;
            case CraftJointKind.Buckle: fits = work == CraftJointWork.Punch || work == CraftJointWork.Stitch || work == CraftJointWork.Rivet; break;
            case CraftJointKind.Fit: fits = work == CraftJointWork.Peen; break;
            case CraftJointKind.Peen: fits = work == CraftJointWork.Peen; break;
            case CraftJointKind.Wrap: fits = work == CraftJointWork.Wrap; break;
            case CraftJointKind.Wedge: fits = work == CraftJointWork.Wedge; break;
            case CraftJointKind.Socket: fits = work == CraftJointWork.Wedge || work == CraftJointWork.Punch || work == CraftJointWork.Rivet; break;
            case CraftJointKind.Solder: fits = work == CraftJointWork.Solder; break;
            case CraftJointKind.BezelSet:
            case CraftJointKind.ProngSet: fits = work == CraftJointWork.Push; break;
        }
        if (!fits)
            return Refuse($"That is not how the {spec.Name.ToLowerInvariant()} is made.", out reason, assembly);
        if (work == CraftJointWork.Punch || work == CraftJointWork.Stitch)
            foreach (string role in new[] { spec.roleA, spec.roleB })
            {
                // Quilting goes through the batting: a seam that takes a padding (any supply but its thread) is sewn once it is laid in.
                CraftComponentSpec layer = string.IsNullOrEmpty(role) ? null : d.Component(role);
                if (layer != null && layer.supply && layer.supplyKind != CraftSupplyKind.Thread && asm.assembly.Supply(role) < Math.Max(1, layer.supplyCount))
                    return Refuse($"Lay the {layer.Name.ToLowerInvariant()} in first.", out reason, assembly);
            }
        if (CraftJewelry.IsJewelJoint(spec.kind) && !Has(CraftTools.JewelersBench))
            return Refuse("That is jeweller's work: there is no jeweller's bench here.", out reason, assembly);
        if (work == CraftJointWork.Solder)
        {
            bool wants = false, have = false;
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == CraftSupplyKind.Solder)
                {
                    wants = true;
                    have |= asm.assembly.Supply(c.role) > 0;
                }
            if (wants && !have)
                return Refuse("Add solder first.", out reason, assembly);
        }
        if (work == CraftJointWork.Push)
        {
            CraftJewelry.SettingParts(this, asm, spec, out CraftPiece setting, out CraftPiece stone);
            if (setting == null || stone == null)
                return Refuse("Put the stone in its setting first.", out reason, assembly);
            if (CraftJewelry.SeatFit(setting, stone) <= 0)
                return Refuse(setting.Tag(CraftJewelry.SeatTag) <= 0 ? "Cut the seat for the stone first." : "The stone will not sit in the seat: cut it wider.", out reason, assembly);
        }
        // the needle pierces as it sews: as many holes as the seam still takes may be made with the stitches
        JointState js = asm.assembly.Joint(spec.id);
        pierce &= work == CraftJointWork.Stitch && js != null;
        int claimed = count, piercing = pierce ? Math.Max(0, CraftAssembly.HolesWanted(d, spec) - js.holes) : 0;
        string why = CraftAssembly.WhyNot(this, asm, d, spec, work, ref count, piercing);
        if (why != null)
            return Refuse(why, out reason, assembly);
        if (pierce)
        {
            int holes = Math.Max(0, count - Math.Max(0, js.holes - js.stitches)); // those the holes already there do not take
            if (holes > 0)
                Record(new CraftOperation { kind = CraftOpKind.JointWork, piece = assembly, id = spec.id, i0 = (int)CraftJointWork.Punch, i1 = holes, a = (float)spacing, b = (float)align });
        }
        // the stitches pulled past the cut, of those sewn (a run cut short keeps its share)
        bool counted = work == CraftJointWork.Stitch && pastCut >= 0;
        int cut = counted && claimed > 0 ? (int)Math.Round(Math.Min(pastCut, claimed) * (double)count / claimed) : 0;
        Record(new CraftOperation { kind = CraftOpKind.JointWork, piece = assembly, id = spec.id, i0 = (int)work, i1 = Math.Max(0, count), i2 = cut, flag = counted, a = (float)a, b = (float)b });
        return true;
    }

    /// <summary>
    /// Finishes an assembly (every required part in place, every joint made) - once. The evaluator then says what it is;
    /// <paramref name="mastery"/> is the finisher's practice in the item's craft (a master's finishing touches);
    /// <paramref name="maker"/> their name, kept with it as its maker (<see cref="AssemblyState.maker"/>). The rivets or
    /// studs left over in a set fitted to it come off it, a set of their own on the bench.
    /// </summary>
    public bool Finish(string assembly, out string reason, double mastery = 0, string maker = "")
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is nothing being assembled.", out reason);
        if (asm.assembly.finished)
            return Refuse("It is already finished.", out reason, assembly);
        List<string> missing = CraftAssembly.Missing(this, asm);
        if (missing.Count > 0)
            return Refuse(string.Join(" ", missing), out reason, assembly);
        Record(new CraftOperation { kind = CraftOpKind.Finish, piece = assembly, a = (float)CraftMath.Clamp01(mastery), label = maker ?? "" });
        return true;
    }

    private void ApplyFinish(CraftOperation op)
    {
        CraftPiece asm = Piece(op.piece);
        if (asm == null)
            return;
        asm.assembly.finished = true;
        asm.assembly.mastery = CraftMath.Clamp01(op.a);
        asm.assembly.maker = op.label ?? "";
        // The rivets or studs left over in a set fitted to it come off it, a set of their own on the bench: the item keeps
        // exactly those set into it. The operation carries the first such set's id (the next ones take it with a number),
        // so replays and the copies over the network make the same.
        int spares = 0;
        foreach (AssemblyPart ap in new List<AssemblyPart>(asm.assembly.parts))
        {
            CraftPiece set = Piece(ap.piece);
            if (set == null || set.kind != CraftPieceKind.Fasteners || set.fasteners.kind == CraftFastenerKind.Buckle || set.fasteners.used <= 0 || set.fasteners.Left <= 0)
                continue;
            if (string.IsNullOrEmpty(op.other))
                op.other = NewId();
            else if (spares == 0)
                BumpSequence(op.other);
            SpareFasteners(asm, set, spares == 0 ? op.other : $"{op.other}.{spares}");
            spares++;
        }
        asm.Record(Elapsed, op.kind, "Finished");
        Raise(CraftEventKind.Finished, asm.id, asm.assembly.design, "");
    }

    /// <summary>
    /// Takes the rivets or studs still to set off a set fitted to a finished item (<paramref name="set"/>): they lie on the
    /// bench as a set of their own (<paramref name="id"/>: its id), its metal by count; the fitted set keeps those set into
    /// the item.
    /// </summary>
    private void SpareFasteners(CraftPiece asm, CraftPiece set, string id)
    {
        int all = Math.Max(0, set.fasteners.count), left = set.fasteners.Left, kept = all - left;
        if (left <= 0 || all <= 0)
            return;
        CraftPiece spare = set.Clone();
        spare.id = id;
        spare.location = CraftLocation.Air;
        spare.parent = "";
        spare.intentDesign = "";
        spare.intentRole = "";
        spare.createdAt = Elapsed;
        spare.fasteners.count = left;
        spare.fasteners.used = 0;
        foreach (CraftAmount x in new List<CraftAmount>(set.composition))
        {
            double grams = x.grams;
            spare.SetGrams(x.element, grams * left / all);
            set.SetGrams(x.element, grams * kept / all);
        }
        spare.CompositionChanged();
        set.CompositionChanged();
        set.fasteners.count = kept;
        set.fasteners.used = kept;
        pieces.Add(spare);
        string what = $"{left} {set.fasteners.kind.ToString().ToLowerInvariant()}{(left == 1 ? "" : "s")}";
        CraftDesign d = Rules.Design(asm.assembly.design);
        spare.Record(Elapsed, CraftOpKind.Finish, $"{CraftText.Capital(what)} left over when the {(d?.Name ?? "item").ToLowerInvariant()} was finished");
        asm.Record(Elapsed, CraftOpKind.Finish, $"The {what} left over came off it");
        Raise(CraftEventKind.PieceCreated, spare.id, "", spare.origin);
    }
}
