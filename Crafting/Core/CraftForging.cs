using System;
using System.Collections.Generic;

/// <summary>
/// Work at the anvil. Every blow moves metal where it lands, by how hard it is struck, with what, on which face, and how
/// hot the metal is there - and the volume is kept: what a blow takes from the thickness goes into the width and the
/// length (a cross peen across the bar draws it out, along it spreads it; the flat does both; on the edge it narrows
/// and thickens). A tilted blow forges a bevel toward the edge (which pushes the edge out and curves the blade); a
/// fuller grooves it; the horn bends it; a flatter evens it; a dishing hammer dishes a plate; the hardy cuts it; a
/// drift opens an eye.
/// <para>Hot metal moves; cold metal barely does, hardens with the work and cracks. Cast iron shatters, red-short iron
/// tears at forging heat, cold-short iron when cold, burnt steel crumbles. Thick scale is hammered into pits. Upsetting
/// a thin flat bar on its edge folds it (a cold shut). Hot work refines the grain; a bloom struck at welding heat
/// closes up and squeezes out its slag - struck cold, it falls apart.</para>
/// <para>A forge weld (a bar folded on itself, or a second bar stacked on it) is set by the blows, stretch by stretch:
/// each blow of the flat face (the flatter too, the peen a little) closes the open seam where it lands by how hard it
/// is struck and how weldable that stretch is at that moment - at welding heat, fluxed, clean of scale
/// (<see cref="WeldFactors"/>). The bar can go back into the fire between heats; a seam that cools far below forging
/// heat still open stays open for good (a delamination).</para>
/// </summary>
public static class CraftForging
{
    /// <summary>A tilt of the hammer's face up to this much (of a full tilt) still strikes flat: a hand never holds it perfectly level.</summary>
    public const double FlatTilt = 0.15;

    /// <summary>
    /// The hammer face's tilt (-1..1) from how far the hand has moved up (+) or down since it pressed (<paramref name="drag"/>):
    /// level while it stays within <paramref name="level"/> of where it pressed - a hand that holds the hammer up drifts a
    /// little, and that is no tilt - then tilting over <paramref name="full"/> more to a full tilt. The window's anvil (8 and
    /// 60 of its 400 px) and the 3D anvil (2 % and 12 % of the screen's height) alike: a blow is bevelled only past
    /// <see cref="FlatTilt"/> of that, which they show before it falls.
    /// </summary>
    public static double TiltFrom(double drag, double level, double full) =>
        Math.Abs(drag) <= level ? 0 : CraftMath.Clamp((drag - Math.Sign(drag) * level) / Math.Max(1e-6, full), -1, 1);
    /// <summary>The length (mm) of a stock bar's segment (200 mm in 24): a bevel bends a single edge by its share of this.</summary>
    public const double SegmentLength = 200.0 / 24;
    /// <summary>How much of the open seam a full blow of the flat face closes on a stretch that welds perfectly.</summary>
    public const double SeamPerBlow = 1.0;
    /// <summary>A seam less open than this is set.</summary>
    public const double SeamSet = 0.02;
    /// <summary>Two bars stacked to be welded may differ in length by this share at most (a short bar is not stretched over a long one).</summary>
    public const double StackLengthTolerance = 0.2;
    /// <summary>
    /// A bloom is consolidated - hammered sound, a bar of bloom iron and no longer a bloom (<see cref="Consolidated"/>) -
    /// once no stretch of it is more porous than <see cref="SoundPores"/> and no more than <see cref="SoundSlag"/> of it
    /// is slag (the slag the furnace left in it worked out: the ore bench's 5 % flaw well under).
    /// </summary>
    public const double SoundPores = 0.02, SoundSlag = 0.02;

    /// <summary>Is <paramref name="p"/> hammered sound (<see cref="SoundPores"/>, <see cref="SoundSlag"/>): a bloom no longer?</summary>
    public static bool Consolidated(CraftRules rules, CraftPiece p)
    {
        if (p == null || p.segments.Count == 0)
            return false;
        double slag = 0;
        foreach (MetalSegment s in p.segments)
        {
            if (s.porosity > SoundPores) return false;
            slag += s.slag;
        }
        return slag <= SoundSlag * Math.Max(1e-9, p.Mass(rules));
    }

    /// <summary>The hammer face's contact length (mm) of a tool.</summary>
    public static double Contact(CraftStrikeTool tool)
    {
        switch (tool)
        {
            case CraftStrikeTool.PeenAcross:
            case CraftStrikeTool.PeenAlong: return 14;
            case CraftStrikeTool.Fuller: return 16;
            case CraftStrikeTool.Horn: return 45;
            case CraftStrikeTool.Hardy:
            case CraftStrikeTool.Chisel: return 6;
            case CraftStrikeTool.Drift: return 10;
            case CraftStrikeTool.Flatter: return 55;
            case CraftStrikeTool.Dishing: return 30;
            case CraftStrikeTool.Straighten: return 70;
            default: return 32;
        }
    }

    /// <summary>Does this tool cut where it is struck (the hardy from below, the hot chisel from above)?</summary>
    public static bool Cuts(CraftStrikeTool tool) => tool == CraftStrikeTool.Hardy || tool == CraftStrikeTool.Chisel;

    /// <summary>How plastic the metal is at <paramref name="t"/> (0 rigid - 1 moving well).</summary>
    public static double Plasticity(CraftAlloyProfile prof, double t)
    {
        double hot = CraftMath.Smooth(prof.HotWorkMin - 150, prof.HotWorkMin + 80, t);
        return Math.Max(hot, ColdPlasticity(prof));
    }

    /// <summary>How plastic the metal is cold (0-1): by its ductility - soft copper, silver and gold about 0.5, steel hardly.</summary>
    public static double ColdPlasticity(CraftAlloyProfile prof) => 0.08 + 0.45 * CraftMath.Clamp01(prof.Ductility / 50);

    /// <summary>
    /// How a stretch of metal gives under the hammer now (0 rigid - 1 moving as at forging heat), as <see cref="Strike"/>
    /// moves it: its plasticity at its heat (<see cref="Plasticity"/>: a soft metal still moves cold, steel hardly), and far
    /// less where it is hardened and cold. The anvil's words follow it.
    /// </summary>
    public static double Gives(CraftRules rules, CraftAlloyProfile prof, MetalSegment seg)
    {
        double t = seg.body.temp, plastic = Plasticity(prof, t);
        if (t < prof.HotWorkMin - 200)
        {
            double hard = CraftMetallurgy.Hardness(rules, prof, seg, seg.body, seg.thickness);
            if (hard > 300) plastic *= Math.Pow(300 / hard, 2);
        }
        return plastic;
    }

    /// <summary>A stretch that gives less than this (<see cref="Gives"/>) barely moves under the hammer.</summary>
    public const double BarelyMoves = 0.3;

    /// <summary>
    /// How stretch <paramref name="seg"/> of <paramref name="p"/> takes a blow now, as <see cref="Strike"/> moves it - in
    /// words for the blow's answer, each with a space before it (empty when it simply moves): too cold to move, steel
    /// tearing cold, a metal stiffening as it is worked cold; wood as far as it gives (<see cref="WoodGive"/>).
    /// </summary>
    public static string BlowFeel(CraftRules rules, CraftPiece p, int seg)
    {
        if (p == null || seg < 0 || seg >= p.segments.Count)
            return "";
        if (p.kind == CraftPieceKind.Wood)
            return WoodGive(p) < 0.5 ? " Cold, it mostly springs back: steamed hot from the steam box it straightens." : " Soft from the steam, it gives.";
        if (p.kind != CraftPieceKind.Metal)
            return "";
        CraftAlloyProfile prof = p.Profile(rules);
        MetalSegment s = p.segments[seg];
        double t = s.body.temp;
        if (Gives(rules, prof, s) < BarelyMoves) return " It barely moves: too cold to forge here.";
        if (prof.Steel && t < prof.HotWorkMin - 200) return " Too cold: steel tears below a dull red.";
        if (t < prof.HotWorkMin && s.body.cold > 1.2) return " It is stiffening with the work: anneal it (heat it to a dull red) before it cracks.";
        return "";
    }

    /// <summary>
    /// How readily wood gives to straightening on the anvil's face (0-1), the only work the anvil does on it: steamed
    /// through and still hot it gives like hot iron (<see cref="CraftWoodwork.Plasticity"/>); cold it mostly springs
    /// back - a little less so while it is still green.
    /// </summary>
    public static double WoodGive(CraftPiece p) => p == null || p.kind != CraftPieceKind.Wood ? 0
        : Math.Max(CraftWoodwork.Plasticity(p), 0.12 + 0.13 * CraftMath.Clamp01(p.wood.moisture / 0.3));

    public static void Strike(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.segments.Count == 0)
            return;
        CraftRules rules = ws.Rules;
        CraftSimulationSettings s = rules.Settings;
        var tool = (CraftStrikeTool)op.i0;
        var face = (CraftStrikeFace)op.i1;
        double force = Math.Max(0, op.b), tilt = CraftMath.Clamp(op.c, -1, 1);
        CraftAlloyProfile prof = p.Profile(rules);
        double total = p.Length;
        double at = CraftMath.Clamp01(op.a) * total;
        double contact = op.d > 0 ? op.d : Contact(tool);
        int centre = p.SegmentAt(op.a);
        bool woodPiece = p.kind == CraftPieceKind.Wood;
        double pos = 0;
        string sound = "";
        int splitAt = -1;
        double tCentre = p.segments[centre].body.temp;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment seg = p.segments[i];
            double mid = pos + seg.length * 0.5;
            pos += seg.length;
            double dist = Math.Abs(mid - at) - seg.length * 0.5;
            double w = dist <= 0 ? 1 : Math.Exp(-Math.Pow(dist / (contact * 0.6), 2));
            if (Cuts(tool) || tool == CraftStrikeTool.Drift)
                w = i == centre ? 1 : 0;
            if (w < 0.05)
                continue;
            double t = seg.body.temp;
            // Wood is only straightened here: as far as it gives (steamed hot, or barely, cold).
            double plastic = woodPiece ? (tool == CraftStrikeTool.Straighten ? WoodGive(p) : 0) : Plasticity(prof, t);
            double hard = woodPiece ? 60 : t > prof.HotWorkMin ? 60 : CraftMetallurgy.Hardness(rules, prof, seg, seg.body, seg.thickness);
            // Hard metal struck cold hardly moves - and a hardened piece cracks.
            bool coldHard = !woodPiece && t < prof.HotWorkMin - 200 && hard > 300;
            if (coldHard)
                plastic *= Math.Pow(300 / hard, 2);
            if (coldHard && hard > 450 && tool != CraftStrikeTool.Straighten)
                seg.crackRisk += force * w * (hard - 450) / 350 * (1 - Math.Min(1, prof.Ductility / 35));
            double hardFactor = Math.Sqrt(Math.Max(20, hard) / 120);
            double thickFactor = Math.Pow(Math.Max(0.5, face == CraftStrikeFace.Edge ? seg.width / 4 : seg.thickness) / 6, 0.7);
            double r = Math.Min(0.3, s.strikeReduction * force * plastic * ToolFactor(tool) / (hardFactor * thickFactor) * w);
            double volume = seg.Volume(p.edges);
            // An open forge weld here: the blow presses the faces together (before the anvil takes the heat of it).
            if (!woodPiece && seg.seamOpen > 0)
                SetSeam(rules, p, seg, tool, face, force * w);

            switch (tool)
            {
                case CraftStrikeTool.Hardy:
                case CraftStrikeTool.Chisel:
                    // The same cut from below (the hardy) or from above (the chisel): fast hot or soft, a quarter as fast cold and hard.
                    seg.cut += force * plastic * 0.14 / Math.Sqrt(Math.Max(0.5, seg.thickness) / 6) * (t > prof.HotWorkMin - 100 || hard < 140 ? 1 : 0.25);
                    seg.cutFromAbove = tool == CraftStrikeTool.Chisel;
                    if (seg.cut >= 1 && p.segments.Count >= 2)
                        splitAt = Math.Min(i, p.segments.Count - 2); // on the last stretch it parts just before it
                    break;
                case CraftStrikeTool.Drift:
                    if (t < prof.HotWorkMin - 50 && hard > 150)
                        seg.crackRisk += force * 0.5;
                    else
                    {
                        double grow = force * plastic * 3;
                        // The punch first goes in as deep as the blow drives it: a tap marks, a full blow opens an eye in a thin bar.
                        if (seg.hole <= 0) grow = Math.Max(grow, 4 * Math.Min(1, force * plastic) * 6 / Math.Max(6, seg.thickness));
                        seg.hole += grow;
                        seg.width += grow * 0.45;
                        if (seg.hole > 0.75 * seg.width)
                            p.AddDefect(CraftDefectKind.Crack, i, (seg.hole / seg.width - 0.7) * 2, true, "the eye was drifted too wide and split");
                    }
                    break;
                case CraftStrikeTool.Horn:
                    // Over the horn: the stretch the horn bears on bends, by its share of the contact (so a blow bends the
                    // same however finely the bar is cut into segments). The tilt's sign is the way: toward the spine (+) or the edge.
                    seg.bend += force * plastic * 14 * w * seg.length / Math.Max(1, contact) * (tilt >= 0 ? 1 : -1);
                    Thin(seg, p.edges, r * 0.2, 0.5, volume);
                    break;
                case CraftStrikeTool.Straighten:
                {
                    double k = Math.Min(1, force * plastic * 0.6 * w);
                    seg.bend -= seg.bend * k;
                    if (Math.Abs(seg.bend) < 0.05) seg.bend = 0;
                    if (!woodPiece && hard > 550 && t < 300)
                        seg.crackRisk += force * w * 0.6; // straightening hardened steel cold
                    break;
                }
                case CraftStrikeTool.Dishing:
                    seg.dish += force * plastic * 6 * w * Math.Max(0.2, Math.Min(1, 4 / Math.Max(0.5, seg.thickness)));
                    Thin(seg, p.edges, r * 0.3, 0.5, volume);
                    break;
                case CraftStrikeTool.Fuller:
                    seg.fuller = Math.Min(seg.thickness * 0.5, seg.fuller + r * seg.thickness * 0.6);
                    Thin(seg, p.edges, r * 0.3, 0.6, volume);
                    break;
                default:
                    if (face == CraftStrikeFace.Edge)
                    {
                        if (seg.width / Math.Max(0.3, seg.thickness) > 6 && force * w > 0.3)
                            p.AddDefect(CraftDefectKind.ColdShut, i, force * w * 0.6, true, "a thin flat bar upset on its edge folded over");
                        seg.width = Math.Max(1, seg.width * (1 - r));
                        Refill(seg, p.edges, volume, 0, 0.7);
                    }
                    else if (Math.Abs(tilt) > FlatTilt && tool == CraftStrikeTool.Flat && !woodPiece)
                    {
                        // A blow tilted on purpose forges a bevel on the face that is up, and pushes the edge out (a face held
                        // within FlatTilt of level strikes flat: the hands' own tremble does not bevel).
                        double lean = (Math.Abs(tilt) - FlatTilt) / (1 - FlatTilt);
                        double area = lean * r * seg.thickness * Math.Min(seg.width, 3 * seg.thickness) * 0.5;
                        double angle = 6 + 18 * Math.Abs(tilt);
                        bool left = p.faceUp == 0;
                        AddBevel(seg, left, area, angle, p.edges);
                        Thin(seg, p.edges, r * (1 - lean), 0.55 + 0.25 * lean, volume);
                        // The edge grows longer than the spine: the blade curves toward the spine (a positive bend, as
                        // a sabre's), the way a single edge hardening ahead of the spine curves it in the quench.
                        if (p.edges <= 1)
                            seg.bend += area / Math.Max(1, seg.width * seg.thickness) * 30 * seg.length / SegmentLength;
                    }
                    else
                    {
                        double spread = tool == CraftStrikeTool.PeenAcross ? 0.15 : tool == CraftStrikeTool.PeenAlong ? 0.85 : tool == CraftStrikeTool.Flatter ? 0.5 : 0.55;
                        Thin(seg, p.edges, r, spread, volume);
                    }
                    break;
            }

            if (woodPiece)
                continue;
            // Heat: the anvil takes some, the work gives a little back.
            double chill = s.anvilChill * w / Math.Max(1, seg.thickness);
            seg.body.temp -= chill - r * 15;
            seg.edge.temp -= chill * 1.4 - r * 15;
            // The surface: loose scale flies off, thick scale is hammered in.
            if (seg.scale > 0.12 && force * w > 0.5 && tool != CraftStrikeTool.Flatter)
            {
                seg.pit += seg.scale * force * w * 0.4;
                if (seg.pit > 0.15) // as pitted as the stretch is (each blow tells it again; the grinder sets it down as it grinds the pits out)
                    ws.Flaw(p, CraftDefectKind.ScalePit, i, seg.pit, true, "scale hammered into the surface", "The blow drives the scale into the metal: it leaves pits.", true);
            }
            seg.scale *= 1 - Math.Min(0.6, 0.3 * force * w);
            double mark = tool == CraftStrikeTool.Flatter ? 12 : tool == CraftStrikeTool.PeenAcross || tool == CraftStrikeTool.PeenAlong ? 60 : tool == CraftStrikeTool.Fuller ? 40 : 25;
            if (tool == CraftStrikeTool.Flatter && seg.rough > mark) seg.rough += (mark - seg.rough) * 0.4 * w;
            else if (r > 0.005) seg.rough += (mark - seg.rough) * 0.3 * w;
            if (r > 0.002) seg.polish = 0;
            // Hot work refines the grain; cold work hardens.
            if (t > prof.Recrystallization)
            {
                seg.body.grain = Math.Max(1, seg.body.grain - r * 3);
                seg.edge.grain = Math.Max(1, seg.edge.grain - r * 3);
            }
            else
            {
                seg.body.cold += r * 4;
                seg.edge.cold += r * 4.5;
            }
            // A bloom closes up and gives up its slag at welding heat - and crumbles when cold. The pores that close take
            // their room with them: the stretch sinks under the hammer by it (Compact), so the metal in it weighs what it
            // did and only the slag driven out leaves - a bloom hammered sound ends smaller and lighter, never heavier.
            if (seg.porosity > SoundPores || seg.slag > 0)
            {
                double weld = CraftMath.Smooth(prof.BurnTemperature - s.weldingMargin - 200, prof.BurnTemperature - s.weldingMargin, t);
                if (seg.porosity > 0.15 && t < 1000)
                    seg.crackRisk += force * w * 0.6;
                double pores = seg.porosity;
                seg.porosity = Math.Max(0, seg.porosity - r * 2.5 * (0.3 + 0.7 * weld));
                if (seg.porosity < pores)
                    Compact(seg, (1 - CraftMath.Clamp(pores, 0, 0.9)) / (1 - CraftMath.Clamp(seg.porosity, 0, 0.9)));
                double expelled = seg.slag * Math.Min(0.5, r * 2.2 * (0.3 + 0.7 * weld));
                seg.slag -= expelled;
            }
            // Cracking.
            if (prof.CastIron)
                seg.crackRisk += force * w * 0.8;
            if (prof.Steel && t < prof.HotWorkMin - 200)
                seg.crackRisk += r * 6 * (1 - Math.Min(1, prof.Ductility / 35)) + seg.body.cold * 0.05;
            if (seg.body.cold > 1.6)
                seg.crackRisk += r * 2 * (seg.body.cold - 1.6);
            if (prof.HotShortness > 0.3 && t > 850 && t < 1150)
                seg.crackRisk += r * 4 * prof.HotShortness;
            if (prof.ColdShortness > 0.2 && t < 400)
                seg.crackRisk += r * 4 * prof.ColdShortness;
            if (seg.body.burnt > 0.3)
            {
                seg.crackRisk += force * w;
                seg.length = Math.Max(0, seg.length - 2 * force * w);
            }
            if (seg.crackRisk > 1)
            {
                string why = CrackCause(rules, prof, seg, t, out string cause);
                p.AddDefect(CraftDefectKind.Crack, i, seg.crackRisk - 0.8, true, cause);
                seg.crackRisk -= 0.6;
                // the event's id carries the cause: the journal learns the lesson of the real one (CraftJournal.InsightOf)
                ws.Raise(CraftEventKind.Cracked, p.id, CrackId + why, why == "castiron" || why == "burnt" || why == "bloom" ? "It crumbles under the hammer." : "The metal tears.");
                sound = "crack";
            }
        }
        if (string.IsNullOrEmpty(sound))
            sound = p.DefectSeverity(CraftDefectKind.Crack, centre) > 0.2 ? "dead" : tCentre > 700 ? "thud" : "ring";
        // A bloom hammered sound - its pores closed, its slag driven out - is a bar of bloom iron from now on (what it is
        // called, how it looks): the slag the furnace left in it is no longer a flaw to work out.
        if (p.bloom && Consolidated(rules, p))
        {
            p.bloom = false;
            p.defects.RemoveAll(d => d.kind == CraftDefectKind.Inclusion && d.at < 0);
            ws.Raise(CraftEventKind.Note, p.id, "consolidated", "The bloom has closed up and its slag is driven out: it is a sound bar of iron now.");
        }
        if (!woodPiece)
        {
            p.Record(ws.Elapsed, CraftOpKind.Strike, $"Forged ({ToolName(tool)}{(face == CraftStrikeFace.Edge ? ", on edge" : "")})", tCentre);
            ws.Raise(CraftEventKind.Struck, p.id, sound, "", (float)force);
        }
        else
            p.Record(ws.Elapsed, CraftOpKind.Strike, "Straightened on the anvil's face", tCentre);
        Even(ws, p);
        if (splitAt >= 0)
            Split(ws, p, splitAt, op, tool == CraftStrikeTool.Chisel ? "cut with the hot chisel" : "cut on the hardy");
    }

    private static double ToolFactor(CraftStrikeTool tool)
    {
        switch (tool)
        {
            case CraftStrikeTool.PeenAcross:
            case CraftStrikeTool.PeenAlong: return 1.15;
            case CraftStrikeTool.Fuller: return 0.7;
            case CraftStrikeTool.Horn: return 0.3;
            case CraftStrikeTool.Flatter: return 0.25;
            case CraftStrikeTool.Dishing: return 0.4;
            case CraftStrikeTool.Straighten: return 0.15;
            default: return 1;
        }
    }

    /// <summary>
    /// The start of a crack's event id (<see cref="CraftEventKind.Cracked"/>): its cause follows - burnt, castiron, bloom,
    /// sulfur, hotshort, phosphorus, coldshort, cold, overworked (the hammer) or hardening (the steel hardening as it
    /// cooled, <see cref="CraftThermal"/>) - so what is learnt from it is the lesson of the real cause.
    /// </summary>
    public const string CrackId = "crack:";

    /// <summary>
    /// Why a blow cracked a stretch: its key (the crack event's id after <see cref="CrackId"/>) and the defect's
    /// <paramref name="cause"/> - cast iron, burnt metal, a bloom struck too cold, a hot-short metal torn at forging heat
    /// (sulfur, or the element that makes it so), a cold-short one cracked cold (phosphorus, or another), forged too cold,
    /// or worked too hard.
    /// </summary>
    private static string CrackCause(CraftRules rules, CraftAlloyProfile prof, MetalSegment seg, double t, out string cause)
    {
        if (prof.CastIron) { cause = "cast iron cannot take the hammer"; return "castiron"; }
        if (seg.body.burnt > 0.3) { cause = "burnt metal crumbled under the hammer"; return "burnt"; }
        if (seg.porosity > 0.15 && t < 1000) { cause = "the bloom was struck too cold and fell apart"; return "bloom"; }
        if (prof.HotShortness > 0.3 && t > 850 && t < 1150)
        {
            string by = CraftMetallurgy.MainEffect(rules, prof, CraftProperty.HotShortness);
            cause = $"it tore at forging heat (too much {rules.Element(by)?.Name.ToLowerInvariant() ?? "sulfur"})";
            return by == "sulfur" || by.Length == 0 ? "sulfur" : "hotshort";
        }
        if (prof.ColdShortness > 0.2 && t < 400)
        {
            string by = CraftMetallurgy.MainEffect(rules, prof, CraftProperty.ColdShortness);
            cause = $"it is brittle cold and cracked (too much {rules.Element(by)?.Name.ToLowerInvariant() ?? "phosphorus"})";
            return by == "phosphorus" ? "phosphorus" : "coldshort";
        }
        if (t < prof.HotWorkMin - 200) { cause = "forged too cold"; return "cold"; }
        cause = "worked too hard";
        return "overworked";
    }

    public static string ToolName(CraftStrikeTool tool)
    {
        switch (tool)
        {
            case CraftStrikeTool.PeenAcross: return "cross peen, drawing out";
            case CraftStrikeTool.PeenAlong: return "cross peen, spreading";
            case CraftStrikeTool.Fuller: return "fuller";
            case CraftStrikeTool.Horn: return "over the horn";
            case CraftStrikeTool.Hardy: return "on the hardy";
            case CraftStrikeTool.Drift: return "drift";
            case CraftStrikeTool.Flatter: return "flatter";
            case CraftStrikeTool.Dishing: return "dishing hammer";
            case CraftStrikeTool.Straighten: return "straightening";
            case CraftStrikeTool.Chisel: return "hot chisel";
            default: return "hammer";
        }
    }

    /// <summary>
    /// A blow on an open seam (<see cref="MetalSegment.seamOpen"/>): the flat face (and the flatter) presses the faces
    /// together, the peen a little, nothing else does; it closes the seam by <paramref name="blow"/> (the force where it
    /// lands) × how weldable the stretch is now (<see cref="SegmentWeldQuality"/>).
    /// </summary>
    private static void SetSeam(CraftRules rules, CraftPiece p, MetalSegment seg, CraftStrikeTool tool, CraftStrikeFace face, double blow)
    {
        if (face != CraftStrikeFace.Flat)
            return; // on its edge the faces are not pressed together
        double k = SeamPress(tool);
        if (k <= 0 || blow <= 0)
            return;
        seg.seamOpen = Math.Max(0, seg.seamOpen - blow * k * SeamPerBlow * SegmentWeldQuality(rules, p, seg));
        if (seg.seamOpen < SeamSet)
            seg.seamOpen = 0;
    }

    /// <summary>How much a tool presses an open seam shut (the flat face and the flatter fully, the cross peen a little, the rest not at all).</summary>
    public static double SeamPress(CraftStrikeTool tool) => tool == CraftStrikeTool.Flat || tool == CraftStrikeTool.Flatter ? 1
        : tool == CraftStrikeTool.PeenAcross || tool == CraftStrikeTool.PeenAlong ? 0.35 : 0;

    /// <summary>Thins a segment by <paramref name="r"/> of its thickness; the volume goes <paramref name="spread"/> into width, the rest into length.</summary>
    private static void Thin(MetalSegment seg, int edges, double r, double spread, double volume)
    {
        if (r <= 0)
            return;
        double k = 1 - r;
        seg.thickness = Math.Max(0.2, seg.thickness * k);
        seg.bevelDepthL *= k;
        seg.bevelDepthR *= k;
        seg.fuller *= k;
        Refill(seg, edges, volume, spread, 0);
    }

    /// <summary>
    /// Pores closed under the hammer: the stretch's section (its thickness, with its bevels' depth, its fuller and the hole
    /// through it) shrinks to <paramref name="k"/> of what it was - its volume with it, exactly, so Volume × (1 - porosity),
    /// the metal in it, stays as it was.
    /// </summary>
    private static void Compact(MetalSegment seg, double k)
    {
        if (!(k > 0) || k >= 1)
            return;
        seg.thickness *= k;
        seg.bevelDepthL *= k;
        seg.bevelDepthR *= k;
        seg.fuller *= k;
    }

    /// <summary>Puts the volume a blow displaced back: <paramref name="toWidth"/> of it into the width, <paramref name="toThickness"/> into the thickness, the rest into the length.</summary>
    private static void Refill(MetalSegment seg, int edges, double volume, double toWidth, double toThickness)
    {
        double now = seg.Volume(edges);
        double left = volume - now;
        if (left <= 0)
            return;
        double perWidth = seg.thickness * seg.length;
        if (toWidth > 0 && perWidth > 1e-9)
            seg.width += left * toWidth / perWidth;
        double perThickness = seg.width * seg.length;
        if (toThickness > 0 && perThickness > 1e-9)
            seg.thickness += left * toThickness / perThickness;
        // The rest goes into the length (exactly, so the volume is kept).
        double area = seg.Area(edges);
        double hole = seg.hole > 0 ? Math.PI * seg.hole * seg.hole * 0.25 * seg.thickness : 0;
        if (area > 1e-9)
            seg.length = Math.Max(0.5, (volume + hole) / area);
    }

    /// <summary>
    /// Adds a bevel area on one face at an angle (the area-weighted angle of what is there and what is added). On a
    /// piece with <paramref name="edges"/> 2 the face's bevel is ground on both edges alike: each reaches the middle of
    /// the width at most (the two meet in a ridge), so what more is ground takes the edges back instead.
    /// </summary>
    public static void AddBevel(MetalSegment seg, bool left, double area, double angleDeg, int edges = 1)
    {
        double depth = left ? seg.bevelDepthL : seg.bevelDepthR, width = left ? seg.bevelWidthL : seg.bevelWidthR;
        double existing = 0.5 * depth * width;
        double oldAngle = width > 1e-9 ? Math.Atan(depth / width) * 180 / Math.PI : angleDeg;
        double total = existing + area;
        if (total <= 1e-9)
            return;
        double a = (oldAngle * existing + angleDeg * area) / total;
        double tan = Math.Tan(CraftMath.Clamp(a, 1, 80) * Math.PI / 180);
        double w = Math.Sqrt(2 * total / tan), d = w * tan;
        // The bevel cannot go past the middle of the thickness (the other face's bevel takes the rest) or wider than the
        // bar - or, on a double-edged piece, than half of it.
        double widest = seg.width / Math.Max(1, edges);
        double maxDepth = Math.Max(0, seg.thickness - (left ? seg.bevelDepthR : seg.bevelDepthL));
        if (d > maxDepth)
        {
            d = maxDepth;
            w = Math.Min(widest, 2 * total / Math.Max(1e-6, d));
        }
        w = Math.Min(w, widest);
        if (left) { seg.bevelDepthL = d; seg.bevelWidthL = w; }
        else { seg.bevelDepthR = d; seg.bevelWidthR = w; }
    }

    /// <summary>
    /// The bevels after the width was taken back (ground on past where they meet): each reaches the middle at most on a
    /// double-edged piece (the edge on a single-edged one), so the section is never thinner than the bevels make it.
    /// </summary>
    public static void FitBevels(MetalSegment seg, int edges)
    {
        double widest = seg.width / Math.Max(1, edges);
        seg.bevelWidthL = Math.Min(seg.bevelWidthL, widest);
        seg.bevelWidthR = Math.Min(seg.bevelWidthR, widest);
    }

    /// <summary>Keeps the segments of even resolution as metal moves (some grow long while others do not).</summary>
    private static void Even(CraftWorkshop ws, CraftPiece p)
    {
        double min = double.MaxValue, max = 0;
        foreach (MetalSegment seg in p.segments)
        {
            min = Math.Min(min, seg.length);
            max = Math.Max(max, seg.length);
        }
        if (max > 2.5 * min && max > 15)
            p.Resample(p.segments.Count);
    }

    // ------------------------------------------------------------------ cutting
    /// <summary>Cuts a piece after segment <paramref name="at"/>: the rest becomes a new piece (the operation's created id).</summary>
    public static CraftPiece Split(CraftWorkshop ws, CraftPiece p, int at, CraftOperation op, string how)
    {
        if (p == null || at < 0 || at >= p.segments.Count - 1)
            return null;
        CraftPiece q = ws.Create(op, p.kind, p.Derived(how.StartsWith("sawn", StringComparison.Ordinal) ? "sawn" : "cut"));
        string id = q.id;
        CraftPiece copy = p.Clone();
        // The new piece is the far part: same material, same record so far.
        q.composition = copy.composition;
        q.CompositionChanged();
        q.wood = copy.wood;
        q.layers = p.layers;
        q.contrast = p.contrast;
        q.inhomogeneity = p.inhomogeneity;
        q.cast = p.cast;
        q.bloom = p.bloom;
        q.record = copy.record;
        q.tags = copy.tags;
        q.edges = p.edges;
        q.location = CraftLocation.Air;
        q.segments = copy.segments.GetRange(at + 1, copy.segments.Count - at - 1);
        p.segments.RemoveRange(at + 1, p.segments.Count - at - 1);
        foreach (MetalSegment seg in p.segments) { seg.cut = 0; seg.cutFromAbove = false; }
        foreach (MetalSegment seg in q.segments) { seg.cut = 0; seg.cutFromAbove = false; }
        if (p.kind == CraftPieceKind.Wood)
        {
            // each part has one end fresh from the cut: half of its ends is sealed, at most
            p.wood.endsSealed *= 0.5;
            q.wood.endsSealed *= 0.5;
        }
        for (int k = p.defects.Count - 1; k >= 0; k--)
        {
            CraftDefect d = p.defects[k];
            if (d.at > at)
            {
                d.at -= at + 1;
                q.defects.Add(d);
                p.defects.RemoveAt(k);
            }
            else if (d.at < 0)
                q.defects.Add(d.Clone());
        }
        // Each part keeps its stretches (twice those it took, up to the settings' count) - wood never fewer than a fresh
        // blank has: a haft or a grip sawn from the long stock is worked as finely as one from a short blank.
        int n = ws.Rules.Settings.segments;
        int least = p.kind == CraftPieceKind.Wood ? Math.Max(4, n / 2) : 4;
        int np = Math.Max(least, Math.Min(n, p.segments.Count * 2)), nq = Math.Max(least, Math.Min(n, q.segments.Count * 2));
        RemapDefects(p, np);
        RemapDefects(q, nq);
        p.Resample(np);
        q.Resample(nq);
        q.id = id;
        p.Record(ws.Elapsed, CraftOpKind.SplitPiece, CraftText.Capital(how));
        q.Record(ws.Elapsed, CraftOpKind.SplitPiece, CraftText.Capital(how));
        ws.Raise(CraftEventKind.Cut, p.id, q.id, "");
        return q;
    }

    private static void RemapDefects(CraftPiece p, int n)
    {
        int old = p.segments.Count;
        if (old <= 0)
            return;
        foreach (CraftDefect d in p.defects)
            if (d.at >= 0)
                d.at = Math.Min(n - 1, (int)((d.at + 0.5) / old * n));
    }

    // ------------------------------------------------------------------ folding and welding
    /// <summary>A metal's welding heat (°C): <see cref="CraftSimulationSettings.weldingMargin"/> below where it burns.</summary>
    public static double WeldingHeat(CraftRules rules, CraftAlloyProfile prof) => prof.BurnTemperature - rules.Settings.weldingMargin;

    /// <summary>
    /// What a forge weld needs of one stretch of a bar, each 0-1: <paramref name="heat"/> - how near welding heat it is
    /// (from 150 °C below it), <paramref name="flux"/> - the flux on it, <paramref name="clean"/> - how free of scale its
    /// faces are (none left at 0.25 mm). <see cref="SegmentWeldQuality"/> multiplies them: heat × (½ + ½ flux) × clean.
    /// </summary>
    public static void WeldFactors(CraftRules rules, CraftPiece p, MetalSegment s, out double heat, out double flux, out double clean)
    {
        double weldT = WeldingHeat(rules, p.Profile(rules));
        heat = CraftMath.Smooth(weldT - 150, weldT, s.Temperature);
        flux = CraftMath.Clamp01(s.flux);
        clean = 1 - Math.Min(1, s.scale * 4);
    }

    /// <summary>How weldable a stretch of a bar is now (0-1): see <see cref="WeldFactors"/>.</summary>
    public static double SegmentWeldQuality(CraftRules rules, CraftPiece p, MetalSegment s)
    {
        WeldFactors(rules, p, s, out double heat, out double flux, out double clean);
        return heat * (0.5 + 0.5 * flux) * clean;
    }

    /// <summary>The welding quality of a piece now: welding heat, flux, clean of scale (0-1), over the whole piece.</summary>
    public static double WeldQuality(CraftRules rules, CraftPiece p)
    {
        double weldT = WeldingHeat(rules, p.Profile(rules));
        double t = p.MeanTemperature;
        double scale = 0;
        foreach (MetalSegment s in p.segments) scale += s.scale;
        scale /= Math.Max(1, p.segments.Count);
        return CraftMath.Smooth(weldT - 150, weldT, t) * (0.5 + 0.5 * CraftMath.Clamp01(MeanFlux(p))) * (1 - Math.Min(1, scale * 4));
    }

    /// <summary>How much of a piece's forge-weld seam is still open (0 none - 1 all of it), by length.</summary>
    public static double SeamOpen(CraftPiece p)
    {
        double open = 0, l = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) { open += s.seamOpen * s.length; l += s.length; }
        return l > 1e-9 ? open / l : 0;
    }

    /// <summary>The flux on a piece, by length (0-1).</summary>
    public static double MeanFlux(CraftPiece p)
    {
        double f = 0, l = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) { f += s.flux * s.length; l += s.length; }
        return l > 1e-9 ? f / l : 0;
    }

    /// <summary>
    /// Was the piece fluxed for the seam it has now - borax put on it since the fold or stack before its last one (a
    /// smith fluxes the faces, then folds)? A seam that never had flux scales over inside and does not take.
    /// </summary>
    public static bool FluxedSinceSeam(CraftPiece p)
    {
        if (p == null)
            return false;
        int seams = 0;
        for (int k = p.record.Count - 1; k >= 0; k--)
        {
            CraftOpKind kind = p.record[k].kind;
            if (kind == CraftOpKind.AddFlux)
                return true;
            if ((kind == CraftOpKind.Fold || kind == CraftOpKind.Weld) && ++seams >= 2)
                break;
        }
        return false;
    }

    /// <summary>Keeps <see cref="CraftPiece.flux"/> - the flux on the piece as a whole - in step with the flux on its stretches.</summary>
    public static void KeepFlux(CraftPiece p)
    {
        if (p == null || p.segments.Count == 0)
            return;
        // A piece saved before flux lay on its stretches: what it had goes on all of them.
        if (p.flux > 1e-4)
        {
            bool none = true;
            foreach (MetalSegment s in p.segments) none &= s.flux <= 0;
            if (none)
                foreach (MetalSegment s in p.segments) s.flux = CraftMath.Clamp01(p.flux);
        }
        p.flux = MeanFlux(p);
    }

    /// <summary>
    /// How well a forge weld was set with the hammer (0-1: the blows covered the whole seam, hard enough, while it was
    /// at welding heat) as a factor on the weld. Operations recorded before the blows counted (0) set it fully.
    /// </summary>
    public static double SetFactor(float set) => set > 0f ? 0.35 + 0.65 * CraftMath.Clamp01(set) : 1;

    /// <summary>
    /// Folds the bar over on itself (at welding heat, fluxed): twice the layers, the slag spread thinner, a little carbon
    /// lost. <paramref name="open"/> (the work of the hands): the seam is left open, to be set by the blows that follow
    /// (<see cref="MetalSegment.seamOpen"/>); otherwise (a test station's quick fold, a script) it is judged at once from
    /// the bar's weldability now and <paramref name="set"/>.
    /// </summary>
    public static void Fold(CraftWorkshop ws, CraftPiece p, double position, float set = 0f, bool open = false)
    {
        if (p == null || p.segments.Count < 2)
            return;
        CraftRules rules = ws.Rules;
        KeepFlux(p);
        double q = WeldQuality(rules, p) * (open ? 1 : SetFactor(set));
        bool fluxed = MeanFlux(p) > 0.05;
        int n = p.segments.Count;
        p.Resample(n * 2);
        var folded = new List<MetalSegment>(n);
        for (int i = 0; i < n; i++)
        {
            MetalSegment a = p.segments[i], b = p.segments[2 * n - 1 - i];
            double va = a.Volume(0), vb = b.Volume(0);
            var s = a.Clone();
            s.length = (a.length + b.length) * 0.5;
            s.width = (a.width + b.width) * 0.5;
            s.thickness = (va + vb) / Math.Max(1e-6, s.length * s.width);
            s.bevelDepthL = s.bevelDepthR = s.bevelWidthL = s.bevelWidthR = 0;
            s.fuller = 0;
            s.hole = 0;
            s.slag = (a.slag + b.slag) * 0.85;
            s.carbonShift = (a.carbonShift + b.carbonShift) * 0.5;
            s.porosity = (a.porosity + b.porosity) * 0.5;
            s.scale = (a.scale + b.scale) * 0.5 * 0.3;
            s.bend = (a.bend - b.bend) * 0.5;
            s.flux = (a.flux + b.flux) * 0.25; // the flux between the faces, squeezed thin
            s.seamOpen = open ? 1 : 0;
            CraftZone.Mix(s.edge, a.edge, va, b.edge, vb);
            CraftZone.Mix(s.body, a.body, va, b.body, vb);
            folded.Add(s);
        }
        p.segments = folded;
        p.layers *= 2;
        p.inhomogeneity *= 0.5; // the streaks of an unstirred casting drawn out thin and laid together, as a bloom's carbon evens
        p.edges = p.IntendedEdges(rules, Math.Min(p.edges, 2)); // what it is for still says how many edges it will have
        double carbon = p.Grams(rules.Settings.steelCarbon);
        if (carbon > 0)
            p.SetGrams(rules.Settings.steelCarbon, carbon * (1 - rules.Settings.foldCarbonLoss * q));
        p.defects.RemoveAll(d => d.at >= 0 && (d.kind == CraftDefectKind.ScalePit || d.kind == CraftDefectKind.ColdShut));
        if (q < 0.75 && !open)
        {
            int seed = CraftMath.Hash(p.id) ^ p.layers;
            for (int k = 0; k < 3; k++)
                p.AddDefect(CraftDefectKind.Delamination, (int)(CraftMath.Noise(seed, k, 1) * n), (0.75 - q) * 1.5, q < 0.3, "a fold that did not weld (too cold, no flux, or scaled)");
        }
        KeepFlux(p);
        p.Record(ws.Elapsed, CraftOpKind.Fold, open ? $"Folded over ({p.layers} layers): the seam to be set at welding heat" : $"Folded and welded ({p.layers} layers)", p.MeanTemperature);
        ws.Raise(CraftEventKind.Note, p.id, "fold", open ? "It folds over on itself: now set the seam with the hammer at welding heat."
            : q > 0.75 ? "The fold takes with a shower of sparks." : "The fold does not take well.");
        if (q < 0.75 && !open && !fluxed)
            ws.Raise(CraftEventKind.Note, p.id, "unfluxed", ""); // it did not take, and there was no flux on it
    }

    /// <summary>
    /// Welds <paramref name="b"/> onto <paramref name="a"/> (stacked): the result is <paramref name="a"/>; their steels mix
    /// by mass and their layers add up. <paramref name="open"/>: the seam between them is left open, to be set by the
    /// blows (as for <see cref="Fold"/>).
    /// </summary>
    public static void Weld(CraftWorkshop ws, CraftPiece a, CraftPiece b, float set = 0f, bool open = false)
    {
        if (a == null || b == null || a == b)
            return;
        CraftRules rules = ws.Rules;
        KeepFlux(a);
        KeepFlux(b);
        double q = Math.Min(WeldQuality(rules, a), WeldQuality(rules, b)) * (open ? 1 : SetFactor(set));
        bool fluxed = MeanFlux(a) > 0.05 && MeanFlux(b) > 0.05;
        int n = a.segments.Count;
        CraftPiece bb = b.Clone();
        bb.Resample(n);
        double ma = a.Mass(rules), mb = b.Mass(rules);
        double[] fa = a.Fractions(rules), fb = b.Fractions(rules);
        for (int i = 0; i < n; i++)
        {
            MetalSegment x = a.segments[i], y = bb.segments[i];
            double vx = x.Volume(a.edges), vy = y.Volume(0);
            double l = (x.length + y.length) * 0.5;
            double w = (x.width * vx + y.width * vy) / Math.Max(1e-9, vx + vy);
            x.length = l;
            x.width = w;
            x.thickness = (vx + vy) / Math.Max(1e-6, l * w);
            x.bevelDepthL = x.bevelDepthR = x.bevelWidthL = x.bevelWidthR = 0;
            x.slag += y.slag;
            x.carbonShift = (x.carbonShift * vx + y.carbonShift * vy) / Math.Max(1e-9, vx + vy);
            x.flux = Math.Min(x.flux, y.flux) * 0.5; // both faces must be fluxed; the blows squeeze it thin
            x.seamOpen = open ? 1 : 0;
            CraftZone.Mix(x.edge, x.edge, vx, y.edge, vy);
            CraftZone.Mix(x.body, x.body, vx, y.body, vy);
        }
        // The bevels are gone into the stack: the billet has the edges its purpose gives it (a sword's blade two), else none yet.
        a.edges = a.IntendedEdges(rules, 0);
        // The new composition, by mass.
        var comp = new List<CraftAmount>();
        for (int i = 0; i < rules.ElementCount; i++)
        {
            double g = fa[i] * ma + fb[i] * mb;
            if (g > 0) comp.Add(new CraftAmount(rules.Elements[i].id, g));
        }
        a.composition = comp;
        a.CompositionChanged();
        a.inhomogeneity = ma + mb > 1e-9 ? (a.inhomogeneity * ma + b.inhomogeneity * mb) / (ma + mb) : 0;
        double dc = rules.SteelCarbon >= 0 ? Math.Abs(fa[rules.SteelCarbon] - fb[rules.SteelCarbon]) : 0;
        int ni = rules.IndexOf("nickel"), ph = rules.IndexOf("phosphorus");
        double dn = ni >= 0 ? Math.Abs(fa[ni] - fb[ni]) : 0, dp = ph >= 0 ? Math.Abs(fa[ph] - fb[ph]) : 0;
        a.contrast = Math.Min(1, Math.Max(a.contrast, Math.Max(b.contrast, dc * 120 + dn * 20 + dp * 200)));
        a.layers += b.layers;
        foreach (CraftDefect d in b.defects)
            a.defects.Add(d.Clone());
        if (q < 0.75 && !open)
        {
            int seed = CraftMath.Hash(a.id) ^ a.layers;
            for (int k = 0; k < 3; k++)
                a.AddDefect(CraftDefectKind.Delamination, (int)(CraftMath.Noise(seed, k, 2) * n), (0.75 - q) * 1.5, q < 0.3, "a weld that did not take (too cold, no flux, or scaled)");
        }
        KeepFlux(a);
        a.Record(ws.Elapsed, CraftOpKind.Weld, open ? $"Stacked on another bar ({a.layers} layers): the seam to be set at welding heat" : $"Forge-welded to another bar ({a.layers} layers)", a.MeanTemperature);
        if (open)
            ws.Raise(CraftEventKind.Note, a.id, "weld", "The bars lie one on the other: now set the seam with the hammer at welding heat.");
        else if (q < 0.75 && !fluxed)
            ws.Raise(CraftEventKind.Note, a.id, "unfluxed", ""); // it did not take, and a face had no flux on it
        ws.Destroy(b, "Welded on.");
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ the anvil
    /// <summary>
    /// A blow of the hammer on a piece lying on the anvil: at <paramref name="position"/> (0-1 along it), with
    /// <paramref name="tool"/>, on the flat or the edge, with <paramref name="force"/> (about 0-1.5: the swing and the
    /// arm), <paramref name="tilt"/> (-1 - 1 toward the edge: forges a bevel on the face that is up).
    /// </summary>
    public bool Strike(string id, double position, CraftStrikeTool tool, CraftStrikeFace face, double force, double tilt, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Anvil))
            return Refuse("There is no anvil here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Wood)
            return Refuse("That cannot be forged.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out onto the anvil first.", out reason, id);
        if (p.kind == CraftPieceKind.Wood && tool != CraftStrikeTool.Straighten)
            return Refuse("Wood is shaped with a rasp and a knife, not the hammer.", out reason, id);
        if (force <= 0)
            return Refuse("The hammer did not come down.", out reason, id);
        if (CraftForging.Cuts(tool) && p.segments.Count < 2)
            return Refuse("It is too small to cut.", out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.Strike, piece = id, i0 = (int)tool, i1 = (int)face, a = (float)CraftMath.Clamp01(position), b = (float)Math.Min(2, force),
            c = (float)CraftMath.Clamp(tilt, -1, 1),
        });
        return true;
    }

    /// <summary>
    /// Can <paramref name="p"/> be folded now: on the anvil, a bar of at least four stretches with no eye in it, its
    /// heat (the mean) at forging heat or more, and no seam of an earlier weld still open. False with the reason.
    /// </summary>
    public bool CanFold(CraftPiece p, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Anvil)) reason = "There is no anvil here.";
        else if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count < 4) reason = "That cannot be folded.";
        else if (p.location != CraftLocation.Air) reason = "Take it out onto the anvil first.";
        else if (p.segments.Exists(s => s.hole > 0.5)) reason = "It has an eye punched in it: it cannot be folded.";
        else if (CraftForging.SeamOpen(p) > 0) reason = "Its last weld is not set yet: strike it shut at welding heat first.";
        else if (p.MeanTemperature < p.Profile(Rules).HotWorkMin) reason = "It is far too cold to fold.";
        return reason.Length == 0;
    }

    /// <summary>
    /// Can <paramref name="b"/> be stacked on <paramref name="a"/> to be forge-welded now: both metal and on the anvil,
    /// both at forging heat or more, about as long as each other, no seam still open on either. False with the reason.
    /// </summary>
    public bool CanWeld(CraftPiece a, CraftPiece b, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Anvil)) reason = "There is no anvil here.";
        else if (a == null || b == null || a == b || a.kind != CraftPieceKind.Metal || b.kind != CraftPieceKind.Metal) reason = "Those cannot be welded together.";
        else if (a.location != CraftLocation.Air || b.location != CraftLocation.Air) reason = "Take both out onto the anvil first.";
        else if (CraftForging.SeamOpen(a) > 0 || CraftForging.SeamOpen(b) > 0) reason = "A weld on one of them is not set yet: strike it shut at welding heat first.";
        else if (Math.Abs(a.Length - b.Length) > CraftForging.StackLengthTolerance * Math.Max(a.Length, b.Length))
            reason = "They are not the same length: cut the longer one to the other's length first, so they lie end to end.";
        else if (a.MeanTemperature < a.Profile(Rules).HotWorkMin || b.MeanTemperature < b.Profile(Rules).HotWorkMin) reason = "They are far too cold to weld.";
        return reason.Length == 0;
    }

    /// <summary>
    /// Folds a bar on itself to forge-weld it (at welding heat, fluxed; <see cref="CanFold"/>). <paramref name="open"/>
    /// (by hand): the seam is left open and set by the blows that follow; otherwise it is welded at once, as well as the
    /// bar is weldable now and <paramref name="set"/> (0-1: how well the hammer set it along the seam) says.
    /// </summary>
    public bool Fold(string id, out string reason, double set = 1, bool open = false)
    {
        CraftPiece p = Piece(id);
        if (!CanFold(p, out reason))
            return Refuse(reason, out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Fold, piece = id, a = 0.5f, b = (float)Math.Max(0.001, CraftMath.Clamp01(set)), flag = open });
        return true;
    }

    /// <summary>
    /// Forge-welds <paramref name="other"/> onto <paramref name="id"/> (stacked; both hot and fluxed: <see cref="CanWeld"/>).
    /// <paramref name="open"/> (by hand): the seam is left open and set by the blows that follow; otherwise welded at once,
    /// with <paramref name="set"/> (0-1): how well the hammer set the weld along the seam.
    /// </summary>
    public bool Weld(string id, string other, out string reason, double set = 1, bool open = false)
    {
        if (!CanWeld(Piece(id), Piece(other), out reason))
            return Refuse(reason, out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Weld, piece = id, other = other, b = (float)Math.Max(0.001, CraftMath.Clamp01(set)), flag = open });
        return true;
    }

    private void ApplyWeld(CraftOperation op) => CraftForging.Weld(this, Piece(op.piece), Piece(op.other), op.b, op.flag);

    // ------------------------------------------------------------------ welding flux on a hot bar
    /// <summary>
    /// Can <paramref name="s"/> be sprinkled on <paramref name="p"/> as a welding flux: a flux for welds (borax, sand -
    /// not a furnace flux), on hot metal (from a dull red: below it the flux does not melt and stick). False with the reason.
    /// </summary>
    public static bool CanFluxPiece(CraftSupplyDefinition s, CraftPiece p, out string reason)
    {
        reason = "";
        if (s == null || s.kind != CraftSupplyKind.Flux) reason = "That is not a flux.";
        else if (!s.FluxFor(CraftFluxUse.Weld))
            reason = $"{s.Name} is {(s.FluxFor(CraftFluxUse.Furnace) ? "a furnace flux" : "not a welding flux")}: it does nothing for a weld.";
        else if (p == null || p.kind != CraftPieceKind.Metal) reason = "Flux goes on metal.";
        else if (p.MaxTemperature < 600) reason = "Flux only sticks to hot metal.";
        return reason.Length == 0;
    }

    /// <summary>
    /// Sprinkles a welding flux (<paramref name="count"/> of the supply) on a hot piece (<see cref="CanFluxPiece"/>), where
    /// the hand spread it: <paramref name="perSegment"/> is how well each stretch was covered (0-1), or none for an even
    /// <paramref name="coverage"/> over all of it. Bare stretches get none (they scale, and do not weld).
    /// </summary>
    public bool FluxPiece(string supplyId, int count, string piece, out string reason, double coverage = 1, IList<float> perSegment = null)
    {
        CraftSupplyDefinition s = Rules.Supply(supplyId);
        if (!CanFluxPiece(s, Piece(piece), out reason))
            return Refuse(reason, out reason, piece);
        if (count <= 0)
            return Refuse("Nothing to add.", out reason, piece);
        var op = new CraftOperation { kind = CraftOpKind.AddFlux, id = s.id, i0 = 2, i1 = count, piece = piece, label = s.Name, a = (float)CraftMath.Clamp01(coverage) };
        if (perSegment != null)
            for (int i = 0; i < perSegment.Count && i < CraftRequestGuard.MaxClayValues; i++) op.points.Add((float)CraftMath.Clamp01(perSegment[i]));
        Record(op);
        return true;
    }

    /// <summary>A welding flux lands on a piece (the <see cref="CraftOpKind.AddFlux"/> of a piece): each stretch by how well it was covered.</summary>
    internal static void Fluxed(CraftWorkshop ws, CraftPiece p, CraftSupplyDefinition s, CraftOperation op)
    {
        if (p == null || s == null || p.segments.Count == 0)
            return;
        double each = 0.6 * op.i1 * s.strength;
        int n = p.segments.Count, given = op.points != null ? op.points.Count : 0;
        for (int i = 0; i < n; i++)
        {
            // Painted stretch by stretch (by where along it, if the piece was cut up differently since), else an even coat.
            double cover = given == 0 ? Spread(op.a)
                : CraftMath.Clamp01(op.points[given == n ? i : Math.Min(given - 1, (int)(p.PositionOf(i) * given))]);
            if (cover > 0)
                p.segments[i].flux = Math.Min(1, p.segments[i].flux + each * cover);
        }
        CraftForging.KeepFlux(p);
        p.Record(ws.Elapsed, op.kind, $"Fluxed with {s.Name.ToLowerInvariant()}");
    }

    /// <summary>Cuts a piece in two at <paramref name="position"/> (0-1) with a hot chisel (hot, or soft metal). Returns the new piece's id.</summary>
    public string SplitPiece(string id, double position, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        // Metal is cut with a hot chisel on the anvil; wood with a saw at the bench.
        bool wood = p != null && p.kind == CraftPieceKind.Wood;
        if (!Has(CraftTools.Anvil) && !(wood && Has(CraftTools.Files)))
        {
            Refuse(wood ? "There is no saw here." : "There is no anvil here.", out reason, id);
            return null;
        }
        if (p == null || p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Wood || p.segments.Count < 2)
        {
            Refuse("That cannot be cut.", out reason, id);
            return null;
        }
        // Wood is sawn where it lies on the bench, exactly at the mark (not in the steam box or the stack).
        if (wood && p.location != CraftLocation.Air)
        {
            Refuse("Take it out first.", out reason, id);
            return null;
        }
        if (wood && !CraftWoodwork.CanSawAt(p, position, out reason))
        {
            Refuse(reason, out reason, id);
            return null;
        }
        if (p.kind == CraftPieceKind.Metal)
        {
            CraftAlloyProfile prof = p.Profile(Rules);
            int at = p.SegmentAt(position);
            MetalSegment s = p.segments[at];
            if (s.body.temp < prof.HotWorkMin - 100 && CraftMetallurgy.Hardness(Rules, prof, s, s.body, s.thickness) > 160)
            {
                Refuse("It is too hard to cut cold: heat it.", out reason, id);
                return null;
            }
        }
        int seg = p.SegmentAt(position);
        if (seg >= p.segments.Count - 1)
            seg = p.segments.Count - 2;
        var op = new CraftOperation { kind = CraftOpKind.SplitPiece, piece = id, i0 = seg };
        if (wood)
        {
            // a saw cuts at the mark itself (flag: the position is in c), not after the stretch it falls in
            op.flag = true;
            op.c = (float)CraftMath.Clamp01(position);
        }
        Record(op);
        return op.other;
    }

    private void ApplySplit(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        bool wood = p != null && p.kind == CraftPieceKind.Wood;
        int at = wood && op.flag ? p.InsertBoundary(op.c) : op.i0;
        CraftForging.Split(this, p, at, op, wood ? "sawn to length" : "cut with a chisel");
    }

    /// <summary>
    /// Can <paramref name="p"/> be made into fasteners of <paramref name="kind"/> at the anvil (<see cref="FormFastener"/>):
    /// metal on the anvil, a small rod - not a flat bar, not a blade or a piece meant for another part - with metal enough
    /// for one at least. <paramref name="count"/>: how many it makes. False with the reason.
    /// </summary>
    public bool CanFormFastener(CraftPiece p, CraftFastenerKind kind, out string reason, out int count)
    {
        reason = "";
        count = 0;
        string what = kind == CraftFastenerKind.Buckle ? "a buckle" : kind == CraftFastenerKind.Stud ? "studs" : "rivets";
        if (!Has(CraftTools.Anvil)) reason = "There is no anvil here.";
        else if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count == 0) reason = "Fasteners are made from metal.";
        else if (p.location != CraftLocation.Air) reason = "Take it out onto the anvil first.";
        else if (p.edges > 0 || p.segments.Exists(s => s.Beveled)) reason = $"It has an edge: a blade is not cut up into {what}.";
        else if (!string.IsNullOrEmpty(p.intentDesign) && Rules.Design(p.intentDesign)?.Component(p.intentRole) is CraftComponentSpec meant && meant.kind != CraftPieceKind.Fasteners)
            reason = $"It is meant to be {CraftText.A(meant.Name)} {meant.Name.ToLowerInvariant()}: cut a piece off it for {what}.";
        else
        {
            double mass = p.Mass(Rules), thick = 0, width = 0, thin = 0, l = 0;
            foreach (MetalSegment s in p.segments)
            {
                thick = Math.Max(thick, Math.Min(s.width, s.thickness));
                width += s.width * s.length;
                thin += s.thickness * s.length;
                l += s.length;
            }
            if (mass > 800 || thick > 14) reason = "It is too big: draw it out into a thin rod first.";
            else if (l > 0 && width > 3 * thin) reason = $"It is a flat bar: draw it into a rod first (about as wide as it is thick) for {what}.";
            else
            {
                FastenerSize(p, kind, out _, out _, out double each);
                count = Math.Max(0, (int)Math.Floor(mass / each));
                if (count == 0) reason = $"There is not metal enough in it for {(kind == CraftFastenerKind.Buckle ? "a buckle" : "one")}.";
            }
        }
        return reason.Length == 0;
    }

    /// <summary>The size of the fasteners a rod makes: their diameter (the rod's), length, and the metal each takes (g).</summary>
    private void FastenerSize(CraftPiece p, CraftFastenerKind kind, out double d, out double length, out double each)
    {
        CraftAlloyProfile prof = p.Profile(Rules);
        d = 0;
        foreach (MetalSegment s in p.segments) d += Math.Min(s.width, s.thickness);
        d = Math.Max(2, d / Math.Max(1, p.segments.Count));
        length = kind == CraftFastenerKind.Buckle ? 40 : 14;
        each = kind == CraftFastenerKind.Buckle ? Math.Max(15, Math.PI * d * d * 0.25 * 120 * prof.Density / 1000) : Math.PI * d * d * 0.25 * length * prof.Density / 1000 * 1.3;
    }

    /// <summary>
    /// Makes fasteners from a small rod at the anvil (<see cref="CanFormFastener"/>): rivets (cut and headed) or a buckle
    /// (bent over the horn) - the whole rod. Hot is best; soft metals can be worked cold. Returns the fasteners' piece id.
    /// </summary>
    public string FormFastener(string id, CraftFastenerKind kind, out string reason)
    {
        CraftPiece p = Piece(id);
        if (!CanFormFastener(p, kind, out reason, out _))
        {
            Refuse(reason, out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.FormFastener, piece = id, i0 = (int)kind };
        Record(op);
        return op.other;
    }

    private void ApplyFormFastener(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null || p.segments.Count == 0)
            return;
        CraftAlloyProfile prof = p.Profile(Rules);
        var kind = (CraftFastenerKind)op.i0;
        double mass = p.Mass(Rules);
        FastenerSize(p, kind, out double d, out double length, out double each);
        int count = Math.Max(0, (int)Math.Floor(mass / each));
        if (count == 0)
            return;
        double t = p.MeanTemperature;
        double hotEnough = t > prof.HotWorkMin - 100 ? 1 : 0.55 + 0.45 * CraftMath.Clamp01(prof.Ductility / 40);
        double cracks = Math.Min(1, p.DefectSeverity(CraftDefectKind.Crack) * 0.3 + p.DefectSeverity(CraftDefectKind.Inclusion) * 0.1);
        CraftPiece f = Create(op, CraftPieceKind.Fasteners, kind == CraftFastenerKind.Buckle ? "a buckle bent at the anvil" : "rivets cut and headed at the anvil");
        f.composition = p.Clone().composition;
        f.CompositionChanged();
        f.fasteners.kind = kind;
        f.fasteners.count = count;
        f.fasteners.massEach = each;
        f.fasteners.diameter = d;
        f.fasteners.length = length;
        f.fasteners.quality = CraftMath.Clamp01(hotEnough * (1 - cracks));
        var seg = new MetalSegment { length = length, width = d, thickness = d };
        seg.edge.CopyFrom(p.segments[0].body);
        seg.body.CopyFrom(p.segments[0].body);
        f.segments.Add(seg);
        f.Record(Elapsed, op.kind, $"Made {count} {(kind == CraftFastenerKind.Buckle ? "buckle(s)" : "rivet(s)")} at {CraftText.Celsius(t)}");
        Destroy(p, "Made into fasteners.");
    }
}
