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
}

/// <summary>
/// Putting an item together. A design says which parts it has and what joins them; the parts are real pieces with
/// their own state, and every joint is made by hand: holes punched (evenly or not, lined up or not), stitches pulled
/// (evenly or not), rivets peened (too lightly they are loose, too hard they crush leather or crack a brittle rivet), a
/// tang peened over a pommel, a wrap wound, a wedge driven (too far splits the haft), a guard fitted to its tang (a gap
/// rattles; too tight it will not go on until the slot is filed). A well-made plate badly riveted makes a weak piece
/// of armour: the evaluator reads the parts and the joints.
/// </summary>
public static class CraftAssembly
{
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
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional || c.supply) continue;
            switch (c.kind)
            {
                case CraftPieceKind.Metal:
                    if (!Has(CraftTools.Anvil | CraftTools.Crucible)) { missing = $"an anvil or a crucible ({c.Name.ToLowerInvariant()})"; return false; }
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

    /// <summary>The size (mm) of the tang where a part sits on it (at <paramref name="t"/> along the tang), and the tang's length.</summary>
    public static double TangSize(CraftRules rules, CraftPiece blade, double t, out double tangLength)
    {
        tangLength = 0;
        if (blade == null || blade.segments.Count == 0)
            return 0;
        CraftComponentSpec spec = rules.Design(blade.intentDesign)?.Component(blade.intentRole);
        double tangTo = spec?.shape?.tangTo ?? 0.15;
        tangLength = blade.Length * tangTo;
        int i = blade.SegmentAt(CraftMath.Clamp(t, 0, 1) * tangTo);
        MetalSegment s = blade.segments[i];
        return Math.Max(s.width, s.thickness) * 0.75 + Math.Min(s.width, s.thickness) * 0.25;
    }

    /// <summary>The largest hole (eye, slot, socket) a piece has (mm).</summary>
    public static double Eye(CraftPiece p)
    {
        double e = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) e = Math.Max(e, s.hole);
        return e;
    }

    /// <summary>The gap of a fit joint now (mm; negative = it does not go on).</summary>
    public static double FitGap(CraftWorkshop ws, CraftPiece assembly, CraftJointSpec joint)
    {
        CraftPiece a = ws.Piece(assembly.assembly.PieceIn(joint.roleA)), b = ws.Piece(assembly.assembly.PieceIn(joint.roleB));
        if (a == null || b == null)
            return 0;
        CraftDesign d = ws.Rules.Design(assembly.assembly.design);
        CraftComponentSpec sb = d?.Component(joint.roleB);
        double where = sb != null && sb.function == CraftComponentFunction.Guard ? 1 : sb != null && sb.function == CraftComponentFunction.Counterweight ? 0 : 0.5;
        switch (joint.kind)
        {
            case CraftJointKind.Wedge:
            case CraftJointKind.Socket:
            {
                // A head on a haft: the haft's end against the head's eye (a tight drive is good).
                CraftPiece head = a.kind == CraftPieceKind.Wood ? b : a, haft = a.kind == CraftPieceKind.Wood ? a : b;
                MetalSegment end = haft.segments.Count > 0 ? haft.segments[haft.segments.Count - 1] : null;
                double size = end != null ? Math.Max(end.width, end.thickness) : 0;
                return Eye(head) - size;
            }
            default:
                return Eye(b) - TangSize(ws.Rules, a, where, out _);
        }
    }

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
        switch (work)
        {
            case CraftJointWork.Punch:
            {
                int n = Math.Max(0, op.i1);
                double total = j.holes + n;
                if (total > 0)
                {
                    j.spacingError = (j.spacingError * j.holes + Math.Max(0, op.a) * n) / total;
                    j.alignError = (j.alignError * j.holes + Math.Max(0, op.b) * n) / total;
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
                    j.tensionSpread = (j.tensionSpread * j.stitches + Math.Max(0, op.b) * n) / total;
                }
                j.stitches += n;
                if (op.a > 0.9)
                    j.damage = Math.Min(1, j.damage + 0.05 * n / Math.Max(1, spec.count)); // pulled so tight it cuts the leather
                break;
            }
            case CraftJointWork.Rivet:
            {
                CraftPiece rivets = RivetsOf(ws, asm, design);
                if (rivets == null || rivets.fasteners.count <= 0)
                    break;
                rivets.fasteners.count--;
                CraftAlloyProfile prof = rivets.Profile(ws.Rules);
                double blows = strikes * force;
                double q = CraftMath.Clamp01(1 - Math.Abs(Math.Log(Math.Max(0.05, blows) / 4)) * 0.45) * rivets.fasteners.quality;
                if (j.holes < j.rivets + 1)
                    q *= 0.4; // no hole for it: forced through
                q *= 1 - 0.5 * Math.Min(1, j.alignError);
                bool hot = rivets.MeanTemperature > prof.HotWorkMin;
                double brittle = (prof.Hardness > 300 && !hot ? 1 : 0) * (1 - Math.Min(1, prof.Ductility / 12));
                if (brittle > 0.3 && blows > 5)
                {
                    q *= 0.3;
                    asm.AddDefect(CraftDefectKind.CrackedRivet, -1, brittle, false, "a hard, brittle rivet cracked as it was peened cold");
                }
                if (blows > 8 && LeatherIn(ws, asm, spec))
                    j.damage = Math.Min(1, j.damage + 0.04 * (blows - 8) / 4); // peened so hard it crushed the leather
                j.rivetQuality = (j.rivetQuality * j.rivets + q) / (j.rivets + 1);
                j.rivets++;
                if (q < 0.4)
                    asm.AddDefect(CraftDefectKind.LooseJoint, -1, 0.5 - q, false, "a rivet badly set");
                break;
            }
            case CraftJointWork.Peen:
            {
                if (spec.kind == CraftJointKind.Fit)
                {
                    j.gap = Math.Max(0, j.gap - force * strikes * 0.08);
                    CraftPiece guard = ws.Piece(asm.assembly.PieceIn(spec.roleB));
                    if (guard != null && guard.kind == CraftPieceKind.Metal)
                    {
                        CraftAlloyProfile gp = guard.Profile(ws.Rules);
                        if (gp.Hardness > 400 && guard.MeanTemperature < 300 && force * strikes > 4)
                            asm.AddDefect(CraftDefectKind.Crack, -1, 0.3, true, "the hard guard cracked as it was closed on the tang");
                    }
                }
                else
                {
                    CraftPiece blade = ws.Piece(asm.assembly.PieceIn(spec.roleA));
                    TangSize(ws.Rules, blade, 0, out double tang);
                    double need = 0;
                    foreach (CraftComponentSpec c in design.components)
                        if (c.function == CraftComponentFunction.Grip || c.function == CraftComponentFunction.Counterweight)
                        {
                            CraftPiece part = ws.Piece(asm.assembly.PieceIn(c.role));
                            if (part != null) need += part.Length;
                        }
                    if (tang < need + 3)
                    {
                        ws.Raise(CraftEventKind.Note, asm.id, "short", "The tang is too short to come through the pommel: there is nothing to peen.");
                        break;
                    }
                    j.peen += force * strikes * 0.05;
                    CraftAlloyProfile bp = blade != null ? blade.Profile(ws.Rules) : null;
                    if (j.peen > 1.4 && bp != null && bp.Hardness > 350)
                        asm.AddDefect(CraftDefectKind.Crack, -1, 0.3, false, "the tang cracked as it was peened");
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
                j.wedge += force * strikes * 0.06 * (j.gap < -1 ? 1.3 : 1);
                if (j.wedge > 1.3)
                {
                    CraftPiece haft = ws.Piece(asm.assembly.PieceIn(spec.roleA));
                    if (haft == null || haft.kind != CraftPieceKind.Wood) haft = ws.Piece(asm.assembly.PieceIn(spec.roleB));
                    if (haft != null && haft.kind == CraftPieceKind.Wood)
                        haft.wood.split = Math.Min(1, haft.wood.split + (j.wedge - 1.3) * 0.5);
                    asm.AddDefect(CraftDefectKind.Crack, -1, j.wedge - 1.3, true, "the wedge was driven so far it split the haft");
                }
                break;
            }
        }
        j.done = JointDone(spec, j);
        asm.Record(ws.Elapsed, CraftOpKind.JointWork, $"{spec.Name}: {work.ToString().ToLowerInvariant()}", force);
        ws.Raise(CraftEventKind.Joined, asm.id, spec.id, "");
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
            case CraftJointKind.Socket: return j.wedge >= 0.3 || j.rivets >= 1;
            case CraftJointKind.Buckle: return j.holes >= 1 && (j.rivets >= 1 || j.stitches >= 2);
            default: return true;
        }
    }

    /// <summary>The fasteners piece of an assembly (its first Fastener part).</summary>
    public static CraftPiece RivetsOf(CraftWorkshop ws, CraftPiece asm, CraftDesign design)
    {
        if (design == null)
            return null;
        foreach (CraftComponentSpec c in design.components)
            if (c.kind == CraftPieceKind.Fasteners && c.fastener != CraftFastenerKind.Buckle)
            {
                CraftPiece p = ws.Piece(asm.assembly.PieceIn(c.role));
                if (p != null) return p;
            }
        return null;
    }

    private static bool LeatherIn(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec) =>
        ws.Piece(asm.assembly.PieceIn(spec.roleA))?.kind == CraftPieceKind.Hide || ws.Piece(asm.assembly.PieceIn(spec.roleB))?.kind == CraftPieceKind.Hide;

    /// <summary>What still has to be done before the item can be finished (empty = it can).</summary>
    public static List<string> Missing(CraftWorkshop ws, CraftPiece asm)
    {
        var list = new List<string>();
        CraftDesign d = ws.Rules.Design(asm.assembly.design);
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
                if (ws.Piece(asm.assembly.PieceIn(c.role, i)) == null)
                    list.Add(c.count > 1 ? $"{c.Name} {i + 1}: missing." : $"{c.Name}: missing.");
        }
        foreach (CraftJointSpec js in d.joints)
        {
            JointState j = asm.assembly.Joint(js.id);
            bool partsThere = ws.Piece(asm.assembly.PieceIn(js.roleA)) != null && (string.IsNullOrEmpty(js.roleB) || ws.Piece(asm.assembly.PieceIn(js.roleB)) != null || d.Component(js.roleB)?.supply == true || d.Component(js.roleB)?.optional == true);
            if (!partsThere) continue;
            if (j == null || !JointDone(js, j))
                list.Add($"{js.Name}: not finished.");
        }
        return list;
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
        a.Record(Elapsed, op.kind, $"Started {CraftText.A(d.Name)} {d.Name.ToLowerInvariant()}");
    }

    /// <summary>
    /// Puts a piece in a role of an assembly (the <paramref name="index"/>th of a role with several). Supplies (padding,
    /// thread) are added with <paramref name="piece"/> empty and <paramref name="supplyCount"/> (the station takes the items).
    /// </summary>
    public bool Attach(string assembly, string role, int index, string piece, out string reason, int supplyCount = 0, double supplyStrength = 0)
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
            if (supplyCount <= 0)
                return Refuse("Nothing to add.", out reason, assembly);
            Record(new CraftOperation { kind = CraftOpKind.Attach, piece = assembly, id = role, i1 = supplyCount, a = (float)Math.Max(0, supplyStrength) });
            return true;
        }
        if (index < 0 || index >= spec.count)
            return Refuse("There is no such place.", out reason, assembly);
        if (asm.assembly.PieceIn(role, index) != null)
            return Refuse($"The {spec.Name.ToLowerInvariant()} is already in place.", out reason, assembly);
        CraftPiece p = Piece(piece);
        if (p == null || p == asm)
            return Refuse("There is no such piece here.", out reason, assembly);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, piece);
        if (!Compatible(spec, p, out reason))
            return Refuse(reason, out reason, piece);
        if (p.MaxTemperature > 200 && (spec.kind != CraftPieceKind.Fasteners))
        {
            foreach (CraftComponentSpec c in d.components)
                if (c.kind == CraftPieceKind.Hide || c.kind == CraftPieceKind.Wood)
                    return Refuse("It is too hot: it would scorch the leather and wood.", out reason, piece);
        }
        // A fit joint: the part must go on.
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.kind != CraftJointKind.Fit && j.kind != CraftJointKind.Wedge && j.kind != CraftJointKind.Socket) continue;
            string other = j.roleA == role ? j.roleB : j.roleB == role ? j.roleA : null;
            if (other == null) continue;
            CraftPiece q = Piece(asm.assembly.PieceIn(other));
            if (q == null) continue;
            double gap = GapWith(asm, j, role, p, other, q);
            double tight = j.kind == CraftJointKind.Fit ? 0 : -3;
            if (gap < tight)
                return Refuse(j.kind == CraftJointKind.Fit
                    ? $"It will not go on: {-gap:0.#} mm too tight. File the slot bigger or the tang thinner."
                    : $"It will not go on: the haft is {-gap:0.#} mm too thick. Rasp it down.", out reason, piece);
        }
        Record(new CraftOperation { kind = CraftOpKind.Attach, piece = assembly, id = role, i0 = index, other = piece });
        return true;
    }

    private double GapWith(CraftPiece asm, CraftJointSpec j, string role, CraftPiece p, string otherRole, CraftPiece q)
    {
        // Temporarily place it to measure.
        var part = new AssemblyPart { role = role, index = 0, piece = p.id };
        asm.assembly.parts.Add(part);
        double gap = CraftAssembly.FitGap(this, asm, j);
        asm.assembly.parts.Remove(part);
        return gap;
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
                if (p.fasteners.count < Math.Max(1, spec.fastenerCount))
                {
                    reason = $"It needs {spec.fastenerCount} of them.";
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
        _ => k.ToString().ToLowerInvariant(),
    };

    /// <summary>A supply's strength is kept beside its count, under the role's name with this suffix.</summary>
    public const string StrengthSuffix = "@strength";

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
            foreach (CraftVariable v in asm.assembly.supplies)
                if (v.name == spec.role)
                {
                    v.value += op.i1;
                    return;
                }
            asm.assembly.supplies.Add(new CraftVariable(spec.role, op.i1));
            asm.Record(Elapsed, op.kind, $"Added {spec.Name.ToLowerInvariant()}");
            return;
        }
        CraftPiece p = Piece(op.other);
        if (p == null)
            return;
        asm.assembly.parts.Add(new AssemblyPart { role = op.id, index = op.i0, piece = p.id });
        p.location = CraftLocation.Assembled;
        p.parent = asm.id;
        if (string.IsNullOrEmpty(p.intentDesign))
        {
            p.intentDesign = d.id;
            p.intentRole = spec.role;
        }
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.roleA != op.id && j.roleB != op.id) continue;
            JointState js = asm.assembly.Joint(j.id);
            if (js == null) continue;
            if (j.kind == CraftJointKind.Fit || j.kind == CraftJointKind.Wedge || j.kind == CraftJointKind.Socket)
                js.gap = CraftAssembly.FitGap(this, asm, j);
            js.done = CraftAssembly.JointDone(j, js);
        }
        asm.Record(Elapsed, op.kind, $"Fitted the {spec.Name.ToLowerInvariant()}");
        Raise(CraftEventKind.Joined, asm.id, spec.role, "");
    }

    /// <summary>Takes a part off an assembly (its joints are undone: rivets drilled out, stitches cut - with some damage).</summary>
    public bool Detach(string assembly, string role, int index, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly || asm.assembly.PieceIn(role, index) == null)
            return Refuse("There is no such part on it.", out reason, assembly);
        if (asm.assembly.finished)
            return Refuse("It is finished.", out reason, assembly);
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
                if (j.roleA != op.id && j.roleB != op.id) continue;
                JointState js = asm.assembly.Joint(j.id);
                if (js == null) continue;
                bool hadWork = js.rivets > 0 || js.stitches > 0 || js.peen > 0 || js.wedge > 0;
                js.rivets = 0;
                js.rivetQuality = 0;
                js.stitches = 0;
                js.peen = 0;
                js.wedge = 0;
                js.turns = 0;
                js.done = false;
                if (hadWork && p != null)
                {
                    if (p.kind == CraftPieceKind.Hide)
                        foreach (HideCell c in p.hide.cells) if (c.present) c.damage = Math.Min(1, c.damage + 0.03);
                    p.Record(Elapsed, op.kind, "Taken off an unfinished item (its joints undone)");
                }
            }
    }

    /// <summary>
    /// Works a joint: punch holes (<paramref name="count"/>, spacing error <paramref name="a"/>, alignment error <paramref name="b"/>),
    /// stitch (<paramref name="count"/> stitches, tension <paramref name="a"/>, unevenness <paramref name="b"/>), set a rivet
    /// (<paramref name="count"/> strikes of force <paramref name="a"/>), peen or wedge (<paramref name="count"/> strikes of
    /// force <paramref name="a"/>), wrap (<paramref name="count"/> turns, tension <paramref name="a"/>, unevenness <paramref name="b"/>).
    /// </summary>
    public bool JointWork(string assembly, string joint, CraftJointWork work, int count, double a, double b, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is nothing being assembled.", out reason);
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
        }
        if (!fits)
            return Refuse($"That is not how the {spec.Name.ToLowerInvariant()} is made.", out reason, assembly);
        if (work == CraftJointWork.Rivet)
        {
            CraftPiece r = CraftAssembly.RivetsOf(this, asm, d);
            if (r == null || r.fasteners.count <= 0)
                return Refuse("There are no rivets left.", out reason, assembly);
        }
        if (work == CraftJointWork.Stitch)
        {
            bool thread = false;
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == CraftSupplyKind.Thread && asm.assembly.Supply(c.role) > 0) thread = true;
            bool wantsThread = false;
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == CraftSupplyKind.Thread) wantsThread = true;
            if (wantsThread && !thread)
                return Refuse("Add thread first.", out reason, assembly);
            JointState js = asm.assembly.Joint(joint);
            if (js != null && js.holes < js.stitches + count)
                return Refuse("Punch the holes first.", out reason, assembly);
        }
        Record(new CraftOperation { kind = CraftOpKind.JointWork, piece = assembly, id = spec.id, i0 = (int)work, i1 = Math.Max(0, count), a = (float)a, b = (float)b });
        return true;
    }

    /// <summary>Finishes an assembly (every required part in place, every joint made). The evaluator then says what it is.</summary>
    public bool Finish(string assembly, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return Refuse("There is nothing being assembled.", out reason);
        List<string> missing = CraftAssembly.Missing(this, asm);
        if (missing.Count > 0)
            return Refuse(string.Join(" ", missing), out reason, assembly);
        Record(new CraftOperation { kind = CraftOpKind.Finish, piece = assembly });
        return true;
    }

    private void ApplyFinish(CraftOperation op)
    {
        CraftPiece asm = Piece(op.piece);
        if (asm == null)
            return;
        asm.assembly.finished = true;
        asm.Record(Elapsed, op.kind, "Finished");
        Raise(CraftEventKind.Finished, asm.id, asm.assembly.design, "");
    }
}
