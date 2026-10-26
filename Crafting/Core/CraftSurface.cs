using System;
using System.Collections.Generic;

/// <summary>
/// Work on the surface and the edge.
/// <list type="bullet">
/// <item><b>Descaling</b> (wire brush, scraper, stone, file): takes the scale off where it is worked; what is left
/// stays; working on past the scale takes metal (a scraper and a stone more than a brush). It never mends a crack.</item>
/// <item><b>Grinding</b>: the wheel takes metal fast - bevels at the angle the blade is held, the flats, the spine, the
/// edge line, the tang - and heats a thin edge: pressed too hard it passes its temper (the edge turns blue and
/// softens). It throws sparks that tell the steel. Ground too far, the edge goes back or the blade goes thin.</item>
/// <item><b>Filing</b>: slower and precise, no heat, cuts only what is softer than the file (it skates on hardened
/// steel); fits tangs and eyes; rasps shape wood.</item>
/// <item><b>Polishing</b>: each grit brings the roughness toward its finish, slowly when grits are skipped (the deep
/// scratches stay). A bright surface shows the colours of heat and resists rust; a coating protects it.</item>
/// <item><b>Sharpening</b>: strokes on the stone at an angle, a pressure, edge leading or trailing: each face is honed
/// toward the apex; once one face meets it, a burr rolls over to the other; the apex gets as fine as the stone - if
/// both faces were honed, the angle held steady and the burr taken off. Hard steel keeps a fine edge; a wobbling hand
/// rounds it; heavy pressure on a hard thin edge chips it, on a soft one rolls it.</item>
/// </list>
/// </summary>
public static class CraftSurface
{
    /// <summary>Clay on segments <paramref name="from"/>-<paramref name="to"/>: one thickness, or one per segment (painted on, from <paramref name="from"/>).</summary>
    public static void ApplyClay(CraftWorkshop ws, CraftPiece p, int from, int to, double thickness, IList<float> perSegment = null)
    {
        if (p == null)
            return;
        bool painted = perSegment != null && perSegment.Count > 0;
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
        {
            int k = i - from;
            double t = painted ? (k < perSegment.Count ? perSegment[k] : 0) : thickness;
            if (painted && t <= 0.01) continue; // left bare
            p.segments[i].clay = CraftMath.Clamp(t, 0, 6);
        }
        p.Record(ws.Elapsed, CraftOpKind.ApplyClay, painted ? "Clay painted on" : "Coated with clay", thickness);
    }

    private static IEnumerable<int> Range(CraftPiece p, int from, int to)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(p.segments.Count - 1, to); i++)
            yield return i;
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
        if (seg.thickness < 0.8)
            p.AddDefect(CraftDefectKind.Overground, i, 0.8 - seg.thickness + 0.2, true, "ground or filed too thin");
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
            seg.polish = Math.Max(seg.polish, a.kind == CraftAbrasiveKind.Brush ? 0.25 : 0.35);
            if (a.kind != CraftAbrasiveKind.Brush)
                seg.rough += (Math.Max(a.finish, 15) - seg.rough) * 0.5;
            seg.temperFilm = seg.Temperature;
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
        double pressure = CraftMath.Clamp01(op.a), seconds = Math.Max(0, op.b), angle = CraftMath.Clamp(op.c, 3, 60);
        bool wood = p.kind == CraftPieceKind.Wood;
        CraftAlloyProfile prof = wood ? null : p.Profile(rules);
        bool skated = false;
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            double hard = wood ? 30 : CraftMetallurgy.Hardness(rules, prof, seg, side == CraftSurfaceSide.LeftBevel || side == CraftSurfaceSide.RightBevel || side == CraftSurfaceSide.EdgeLine ? seg.edge : seg.body, seg.thickness);
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
                    bool left = side == CraftSurfaceSide.LeftBevel;
                    double area = rate * Math.Min(seg.width, 3 * seg.thickness);
                    double before = 0.5 * (seg.bevelDepthL * seg.bevelWidthL + seg.bevelDepthR * seg.bevelWidthR);
                    CraftForging.AddBevel(seg, left, area, angle);
                    double added = 0.5 * (seg.bevelDepthL * seg.bevelWidthL + seg.bevelDepthR * seg.bevelWidthR) - before;
                    if (added < area * 0.9)
                    {
                        // The bevels meet: grinding on takes the edge back.
                        double back = (area - Math.Max(0, added)) / Math.Max(0.5, seg.thickness);
                        seg.width = Math.Max(0.5, seg.width - back);
                        seg.apex = Math.Max(seg.apex, 40);
                    }
                    removed = rate * 0.5;
                    if (p.edges == 0) p.edges = 1;
                    seg.honeL = seg.honeR = 0;
                    seg.microAngle = 0;
                    seg.apex = Math.Max(seg.apex, a.finish * 6);
                    break;
                }
                case CraftSurfaceSide.Faces:
                    seg.thickness = Math.Max(0.1, seg.thickness - rate * 2);
                    seg.bevelDepthL = Math.Min(seg.bevelDepthL, seg.thickness);
                    seg.bevelDepthR = Math.Min(seg.bevelDepthR, Math.Max(0, seg.thickness - seg.bevelDepthL));
                    removed = rate;
                    break;
                case CraftSurfaceSide.EdgeLine:
                {
                    double w = rate;
                    if (seg.bevelWidthL > 1e-6) { double k = Math.Min(1, w / seg.bevelWidthL); seg.bevelDepthL *= 1 - k; seg.bevelWidthL = Math.Max(0, seg.bevelWidthL - w); }
                    if (seg.bevelWidthR > 1e-6) { double k = Math.Min(1, w / seg.bevelWidthR); seg.bevelDepthR *= 1 - k; seg.bevelWidthR = Math.Max(0, seg.bevelWidthR - w); }
                    seg.width = Math.Max(0.5, seg.width - w);
                    seg.apex = Math.Max(seg.apex, 250);
                    seg.chip *= 0.3;
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
            if (!wood)
            {
                TookMetal(p, seg, i, removed);
                if (wheel)
                {
                    double heat = rules.Settings.grindHeat * a.heat * pressure * seconds;
                    seg.edge.temp += heat / Math.Max(0.3, seg.EdgeThickness + 0.2);
                    seg.body.temp += heat * 0.25 / Math.Max(1, seg.thickness);
                }
                seg.polish = Math.Max(seg.polish, 0.3);
                seg.temperFilm = seg.Temperature;
            }
            seg.rough += (a.finish - seg.rough) * Math.Min(1, 0.6 * pressure * seconds + 0.2);
        }
        if (skated)
            ws.Raise(CraftEventKind.Note, p.id, "skate", "The file skates over it without biting: it is harder than the file.");
        string sideName = side.ToString().ToLowerInvariant();
        p.Record(ws.Elapsed, wheel ? CraftOpKind.Grind : CraftOpKind.File,
            $"{(wheel ? "Ground" : wood ? "Rasped" : "Filed")} ({sideName}{(side == CraftSurfaceSide.LeftBevel || side == CraftSurfaceSide.RightBevel ? $" at {angle:0}°" : "")})", pressure);
        if (wheel && !wood)
            ws.Raise(CraftEventKind.Ground, p.id, "sparks", CraftMetallurgy.Sparks(rules, prof), (float)pressure);
    }

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
        }
        // Forcing a deep cut, or a shaky one, splits it along the grain.
        if (depth > 1.4 || steady < 0.35)
            p.wood.split = Math.Min(1, p.wood.split + Math.Max(0, depth - 1.4) * 0.04 + Math.Max(0, 0.35 - steady) * 0.03);
        p.Record(ws.Elapsed, CraftOpKind.Carve, cut == CarveCut.Bore ? "A slot bored for the tang" : $"Carved ({cut.ToString().ToLowerInvariant()})", depth);
    }

    // ------------------------------------------------------------------ polishing and coatings
    public static void Polish(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftAbrasive a = ws.Rules.Abrasive(op.id);
        if (p == null || a == null)
            return;
        double pressure = CraftMath.Clamp01(op.a), seconds = Math.Max(0, op.b);
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            // Deep scratches from a skipped grit come out slowly.
            double rate = seg.rough > a.finish * 6 ? 0.02 : 0.3;
            seg.rough = a.finish + (seg.rough - a.finish) * Math.Exp(-seconds * pressure * rate);
            seg.scale = Math.Max(0, seg.scale - a.scale * pressure * seconds);
            seg.thickness = Math.Max(0.1, seg.thickness - 0.0015 * pressure * seconds);
            if (seg.scale <= 0.001)
                seg.polish = CraftMath.Clamp01(1 - seg.rough / 20);
            seg.temperFilm = seg.Temperature;
            seg.coating *= 0.5;
        }
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
        bool strop = stone.apex <= 1.5;
        foreach (int i in Range(p, op.i0, op.i1))
        {
            MetalSegment seg = p.segments[i];
            double hard = CraftMetallurgy.Hardness(rules, prof, seg, seg.edge, Math.Max(0.2, seg.EdgeThickness));
            double tough = CraftMetallurgy.Toughness(rules, prof, seg, seg.edge, Math.Max(0.2, seg.EdgeThickness));
            double edgeT = seg.EdgeThickness;
            for (int k = 0; k < strokes; k++)
            {
                double cut = rules.Settings.sharpenRate * pressure * (strop ? 0.2 : 1) * (leading ? 1.2 : 0.8) / (1 + hard / 700);
                double reach = cut / (1 + edgeT / 0.25);
                if (left)
                {
                    seg.microL = seg.honeL > 0 ? seg.microL + (angle - seg.microL) * Math.Min(1, cut * 3) : angle;
                    seg.honeL = Math.Min(1.5, seg.honeL + reach);
                    if (seg.honeL >= 1) seg.burr = Math.Min(1, seg.burr + reach * (strop ? 0 : 1.5));
                }
                else
                {
                    seg.microR = seg.honeR > 0 ? seg.microR + (angle - seg.microR) * Math.Min(1, cut * 3) : angle;
                    seg.honeR = Math.Min(1.5, seg.honeR + reach);
                    if (seg.honeR >= 1) seg.burr = Math.Min(1, seg.burr + reach * (strop ? 0 : 1.5));
                }
                // Light trailing strokes and the strop take the burr off.
                if (strop || !leading && pressure < 0.4)
                    seg.burr *= 1 - 0.35 * (1 - pressure);
                double both = Math.Min(Math.Min(seg.honeL, seg.honeR), 1);
                double floor = stone.apex * (1 + wobble / 3) * (1 + 1.5 * Math.Max(0, 0.6 - hard / 1000));
                double toward = cut * 2 * both;
                seg.apex = floor + (seg.apex - floor) * Math.Exp(-toward);
                if (both < 0.3)
                    seg.apex = Math.Max(seg.apex, 40 + edgeT * 60);
                // Hard thin edges chip under heavy pressure; soft ones roll.
                if (pressure > 0.7 && leading)
                    seg.chip = Math.Min(1, seg.chip + 0.003 * (pressure - 0.7) * (hard / 700) * (30 / Math.Max(5, tough)));
                else
                    seg.chip *= 1 - 0.02 * both;
                seg.width = Math.Max(0.5, seg.width - 0.0015 * cut);
            }
            if (seg.honeL > 0.5 && seg.honeR > 0.5)
                seg.microAngle = seg.microL + seg.microR + wobble * 0.5;
            seg.rough += (stone.finish - seg.rough) * 0.05;
        }
        p.Record(ws.Elapsed, CraftOpKind.Sharpen, $"Sharpened on the {stone.Name.ToLowerInvariant()} ({(left ? "left" : "right")} face at {angle:0}°)", strokes);
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

    /// <summary>Takes scale off segments <paramref name="from"/>-<paramref name="to"/> with a brush, scraper, stone or file.</summary>
    public bool Descale(string id, int from, int to, string abrasive, double pressure, double seconds, out string reason)
    {
        if (!SurfaceCheck(id, CraftTools.Files, "There are no brushes or scrapers here.", out CraftPiece p, out reason))
            return false;
        CraftAbrasive a = Rules.Abrasive(abrasive);
        if (a == null)
            return Refuse("There is no such tool.", out reason, id);
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
        if (p.MaxTemperature > 400)
            return Refuse("It is far too hot to hold on the wheel.", out reason, id);
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
        if (p.MaxTemperature > 350)
            return Refuse("It is too hot to file.", out reason, id);
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

    /// <summary>Oils or waxes a metal or wooden piece.</summary>
    public bool Coat(string id, string supply, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (p == null || p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Wood)
            return Refuse("There is nothing to coat.", out reason, id);
        if (s == null || s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That does not coat anything.", out reason, id);
        if (p.MaxTemperature > 250)
            return Refuse("It would only smoke and burn on hot metal.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Coat, piece = id, id = s.id });
        return true;
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
        if (p.MaxTemperature > 150)
            return Refuse("Let it cool first.", out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.Sharpen, piece = id, i0 = from, i1 = to, i2 = (int)side, id = a.id, a = (float)pressure, b = (float)angle, c = strokes,
            d = (float)wobble, flag = edgeLeading,
        });
        return true;
    }
}
