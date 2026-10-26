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
/// </summary>
public static class CraftForging
{
    /// <summary>The hammer face's contact length (mm) of a tool.</summary>
    public static double Contact(CraftStrikeTool tool)
    {
        switch (tool)
        {
            case CraftStrikeTool.PeenAcross:
            case CraftStrikeTool.PeenAlong: return 14;
            case CraftStrikeTool.Fuller: return 16;
            case CraftStrikeTool.Horn: return 45;
            case CraftStrikeTool.Hardy: return 6;
            case CraftStrikeTool.Drift: return 10;
            case CraftStrikeTool.Flatter: return 55;
            case CraftStrikeTool.Dishing: return 30;
            case CraftStrikeTool.Straighten: return 70;
            default: return 32;
        }
    }

    /// <summary>How plastic the metal is at <paramref name="t"/> (0 rigid - 1 moving well).</summary>
    public static double Plasticity(CraftAlloyProfile prof, double t)
    {
        double hot = CraftMath.Smooth(prof.HotWorkMin - 150, prof.HotWorkMin + 80, t);
        double cold = 0.08 + 0.45 * CraftMath.Clamp01(prof.Ductility / 50);
        return Math.Max(hot, cold);
    }

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
            if (tool == CraftStrikeTool.Hardy || tool == CraftStrikeTool.Drift)
                w = i == centre ? 1 : 0;
            if (w < 0.05)
                continue;
            double t = seg.body.temp;
            double plastic = woodPiece ? 0 : Plasticity(prof, t);
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

            switch (tool)
            {
                case CraftStrikeTool.Hardy:
                    seg.cut += force * plastic * 0.14 / Math.Sqrt(Math.Max(0.5, seg.thickness) / 6) * (t > prof.HotWorkMin - 100 || hard < 140 ? 1 : 0.25);
                    if (seg.cut >= 1 && i < p.segments.Count - 1 && i >= 0)
                        splitAt = i;
                    break;
                case CraftStrikeTool.Drift:
                    if (t < prof.HotWorkMin - 50 && hard > 150)
                        seg.crackRisk += force * 0.5;
                    else
                    {
                        double grow = force * plastic * 3;
                        if (seg.hole <= 0) grow = Math.Max(grow, 4);
                        seg.hole += grow;
                        seg.width += grow * 0.45;
                        if (seg.hole > 0.75 * seg.width)
                            p.AddDefect(CraftDefectKind.Crack, i, (seg.hole / seg.width - 0.7) * 2, true, "the eye was drifted too wide and split");
                    }
                    break;
                case CraftStrikeTool.Horn:
                    seg.bend += force * plastic * 12 * w * (tilt >= 0 ? 1 : -1);
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
                    else if (Math.Abs(tilt) > 0.05 && tool == CraftStrikeTool.Flat && !woodPiece)
                    {
                        // A tilted blow forges a bevel on the face that is up, and pushes the edge out.
                        double area = Math.Abs(tilt) * r * seg.thickness * Math.Min(seg.width, 3 * seg.thickness) * 0.5;
                        double angle = 6 + 18 * Math.Abs(tilt);
                        bool left = p.faceUp == 0;
                        AddBevel(seg, left, area, angle);
                        Thin(seg, p.edges, r * (1 - Math.Abs(tilt)), 0.8, volume);
                        if (p.edges <= 1)
                            seg.bend -= area / Math.Max(1, seg.width * seg.thickness) * 30; // the edge grows longer than the spine
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
                if (seg.pit > 0.15)
                    p.AddDefect(CraftDefectKind.ScalePit, i, seg.pit, true, "scale hammered into the surface");
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
            // A bloom closes up and gives up its slag at welding heat - and crumbles when cold.
            if (seg.porosity > 0.02 || seg.slag > 0)
            {
                double weld = CraftMath.Smooth(prof.BurnTemperature - s.weldingMargin - 200, prof.BurnTemperature - s.weldingMargin, t);
                if (seg.porosity > 0.15 && t < 1000)
                    seg.crackRisk += force * w * 0.6;
                seg.porosity = Math.Max(0, seg.porosity - r * 2.5 * (0.3 + 0.7 * weld));
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
                string cause = prof.CastIron ? "cast iron cannot take the hammer" : seg.body.burnt > 0.3 ? "burnt metal crumbled under the hammer"
                             : seg.porosity > 0.15 && t < 1000 ? "the bloom was struck too cold and fell apart"
                             : prof.HotShortness > 0.3 && t > 850 && t < 1150 ? "it tore at forging heat (too much sulfur)"
                             : t < prof.HotWorkMin - 200 ? "forged too cold" : "worked too hard";
                p.AddDefect(CraftDefectKind.Crack, i, seg.crackRisk - 0.8, true, cause);
                seg.crackRisk -= 0.6;
                ws.Raise(CraftEventKind.Cracked, p.id, "crack", prof.CastIron || seg.body.burnt > 0.3 ? "It crumbles under the hammer." : "The metal tears.");
                sound = "crack";
            }
        }
        if (string.IsNullOrEmpty(sound))
            sound = p.DefectSeverity(CraftDefectKind.Crack, centre) > 0.2 ? "dead" : tCentre > 700 ? "thud" : "ring";
        if (!woodPiece)
        {
            p.Record(ws.Elapsed, CraftOpKind.Strike, $"Forged ({ToolName(tool)}{(face == CraftStrikeFace.Edge ? ", on edge" : "")})", tCentre);
            ws.Raise(CraftEventKind.Struck, p.id, sound, "", (float)force);
        }
        Even(ws, p);
        if (splitAt >= 0)
            Split(ws, p, splitAt, op, "cut on the hardy");
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
            default: return "hammer";
        }
    }

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

    /// <summary>Adds a bevel area on one face at an angle (the area-weighted angle of what is there and what is added).</summary>
    public static void AddBevel(MetalSegment seg, bool left, double area, double angleDeg)
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
        // The bevel cannot go past the middle of the thickness (the other face's bevel takes the rest) or wider than the bar.
        double maxDepth = Math.Max(0, seg.thickness - (left ? seg.bevelDepthR : seg.bevelDepthL));
        if (d > maxDepth)
        {
            d = maxDepth;
            w = Math.Min(seg.width, 2 * total / Math.Max(1e-6, d));
        }
        w = Math.Min(w, seg.width);
        if (left) { seg.bevelDepthL = d; seg.bevelWidthL = w; }
        else { seg.bevelDepthR = d; seg.bevelWidthR = w; }
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
        CraftPiece q = ws.Create(op, p.kind, $"{how} from {p.origin}");
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
        foreach (MetalSegment seg in p.segments) seg.cut = 0;
        foreach (MetalSegment seg in q.segments) seg.cut = 0;
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
        int n = ws.Rules.Settings.segments;
        int np = Math.Max(4, Math.Min(n, p.segments.Count * 2)), nq = Math.Max(4, Math.Min(n, q.segments.Count * 2));
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
    /// <summary>The welding quality of a piece now: welding heat, flux, clean of scale (0-1).</summary>
    public static double WeldQuality(CraftRules rules, CraftPiece p)
    {
        CraftAlloyProfile prof = p.Profile(rules);
        double weldT = prof.BurnTemperature - rules.Settings.weldingMargin;
        double t = p.MeanTemperature;
        double scale = 0;
        foreach (MetalSegment s in p.segments) scale += s.scale;
        scale /= Math.Max(1, p.segments.Count);
        return CraftMath.Smooth(weldT - 150, weldT, t) * (0.5 + 0.5 * CraftMath.Clamp01(p.flux)) * (1 - Math.Min(1, scale * 4));
    }

    /// <summary>
    /// How well a forge weld was set with the hammer (0-1: the blows covered the whole seam, hard enough, while it was
    /// at welding heat) as a factor on the weld. Operations recorded before the blows counted (0) set it fully.
    /// </summary>
    public static double SetFactor(float set) => set > 0f ? 0.35 + 0.65 * CraftMath.Clamp01(set) : 1;

    /// <summary>Folds the bar over on itself and welds it (at welding heat, fluxed): twice the layers, the slag spread thinner, a little carbon lost.</summary>
    public static void Fold(CraftWorkshop ws, CraftPiece p, double position, float set = 0f)
    {
        if (p == null || p.segments.Count < 2)
            return;
        CraftRules rules = ws.Rules;
        double q = WeldQuality(rules, p) * SetFactor(set);
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
            CraftZone.Mix(s.edge, a.edge, va, b.edge, vb);
            CraftZone.Mix(s.body, a.body, va, b.body, vb);
            folded.Add(s);
        }
        p.segments = folded;
        p.layers *= 2;
        p.edges = Math.Min(p.edges, 2);
        double carbon = p.Grams(rules.Settings.steelCarbon);
        if (carbon > 0)
            p.SetGrams(rules.Settings.steelCarbon, carbon * (1 - rules.Settings.foldCarbonLoss * q));
        p.defects.RemoveAll(d => d.at >= 0 && (d.kind == CraftDefectKind.ScalePit || d.kind == CraftDefectKind.ColdShut));
        if (q < 0.75)
        {
            int seed = CraftMath.Hash(p.id) ^ p.layers;
            for (int k = 0; k < 3; k++)
                p.AddDefect(CraftDefectKind.Delamination, (int)(CraftMath.Noise(seed, k, 1) * n), (0.75 - q) * 1.5, q < 0.3, "a fold that did not weld (too cold, no flux, or scaled)");
        }
        p.flux *= 0.5;
        p.Record(ws.Elapsed, CraftOpKind.Fold, $"Folded and welded ({p.layers} layers)", p.MeanTemperature);
        ws.Raise(CraftEventKind.Note, p.id, "fold", q > 0.75 ? "The fold takes with a shower of sparks." : "The fold does not take well.");
    }

    /// <summary>Welds <paramref name="b"/> onto <paramref name="a"/> (stacked): the result is <paramref name="a"/>; their steels mix by mass and their layers add up.</summary>
    public static void Weld(CraftWorkshop ws, CraftPiece a, CraftPiece b, float set = 0f)
    {
        if (a == null || b == null || a == b)
            return;
        CraftRules rules = ws.Rules;
        double q = Math.Min(WeldQuality(rules, a), WeldQuality(rules, b)) * SetFactor(set);
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
            CraftZone.Mix(x.edge, x.edge, vx, y.edge, vy);
            CraftZone.Mix(x.body, x.body, vx, y.body, vy);
        }
        a.edges = 0;
        // The new composition, by mass.
        var comp = new List<CraftAmount>();
        for (int i = 0; i < rules.ElementCount; i++)
        {
            double g = fa[i] * ma + fb[i] * mb;
            if (g > 0) comp.Add(new CraftAmount(rules.Elements[i].id, g));
        }
        a.composition = comp;
        a.CompositionChanged();
        double dc = rules.SteelCarbon >= 0 ? Math.Abs(fa[rules.SteelCarbon] - fb[rules.SteelCarbon]) : 0;
        int ni = rules.IndexOf("nickel"), ph = rules.IndexOf("phosphorus");
        double dn = ni >= 0 ? Math.Abs(fa[ni] - fb[ni]) : 0, dp = ph >= 0 ? Math.Abs(fa[ph] - fb[ph]) : 0;
        a.contrast = Math.Min(1, Math.Max(a.contrast, Math.Max(b.contrast, dc * 120 + dn * 20 + dp * 200)));
        a.layers += b.layers;
        foreach (CraftDefect d in b.defects)
            a.defects.Add(d.Clone());
        if (q < 0.75)
        {
            int seed = CraftMath.Hash(a.id) ^ a.layers;
            for (int k = 0; k < 3; k++)
                a.AddDefect(CraftDefectKind.Delamination, (int)(CraftMath.Noise(seed, k, 2) * n), (0.75 - q) * 1.5, q < 0.3, "a weld that did not take (too cold, no flux, or scaled)");
        }
        a.flux *= 0.5;
        a.Record(ws.Elapsed, CraftOpKind.Weld, $"Forge-welded to another bar ({a.layers} layers)", a.MeanTemperature);
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
        if (tool == CraftStrikeTool.Hardy && p.segments.Count < 2)
            return Refuse("It is too small to cut.", out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.Strike, piece = id, i0 = (int)tool, i1 = (int)face, a = (float)CraftMath.Clamp01(position), b = (float)Math.Min(2, force),
            c = (float)CraftMath.Clamp(tilt, -1, 1),
        });
        return true;
    }

    /// <summary>
    /// Folds a bar on itself and welds it (at welding heat, fluxed). <paramref name="set"/> (0-1): how well the hammer
    /// set the weld along the seam.
    /// </summary>
    public bool Fold(string id, out string reason, double set = 1)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Anvil))
            return Refuse("There is no anvil here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count < 4)
            return Refuse("That cannot be folded.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out onto the anvil first.", out reason, id);
        foreach (MetalSegment s in p.segments)
            if (s.hole > 0)
                return Refuse("It has an eye punched in it: it cannot be folded.", out reason, id);
        if (p.MaxTemperature < p.Profile(Rules).HotWorkMin)
            return Refuse("It is far too cold to fold.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Fold, piece = id, a = 0.5f, b = (float)Math.Max(0.001, CraftMath.Clamp01(set)) });
        return true;
    }

    /// <summary>
    /// Forge-welds <paramref name="other"/> onto <paramref name="id"/> (stacked; both hot and fluxed).
    /// <paramref name="set"/> (0-1): how well the hammer set the weld along the seam.
    /// </summary>
    public bool Weld(string id, string other, out string reason, double set = 1)
    {
        reason = "";
        CraftPiece a = Piece(id), b = Piece(other);
        if (!Has(CraftTools.Anvil))
            return Refuse("There is no anvil here.", out reason, id);
        if (a == null || b == null || a == b || a.kind != CraftPieceKind.Metal || b.kind != CraftPieceKind.Metal)
            return Refuse("Those cannot be welded together.", out reason, id);
        if (a.location != CraftLocation.Air || b.location != CraftLocation.Air)
            return Refuse("Take both out onto the anvil first.", out reason, id);
        if (a.MaxTemperature < a.Profile(Rules).HotWorkMin || b.MaxTemperature < b.Profile(Rules).HotWorkMin)
            return Refuse("They are far too cold to weld.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Weld, piece = id, other = other, b = (float)Math.Max(0.001, CraftMath.Clamp01(set)) });
        return true;
    }

    private void ApplyWeld(CraftOperation op) => CraftForging.Weld(this, Piece(op.piece), Piece(op.other), op.b);

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
        Record(op);
        return op.other;
    }

    private void ApplySplit(CraftOperation op) => CraftForging.Split(this, Piece(op.piece), op.i0, op, "cut with a chisel");

    /// <summary>
    /// Makes fasteners from a small rod or bar at the anvil: rivets (cut and headed) or a buckle (bent over the horn).
    /// Hot is best; soft metals can be worked cold. Returns the fasteners' piece id.
    /// </summary>
    public string FormFastener(string id, CraftFastenerKind kind, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Anvil))
        {
            Refuse("There is no anvil here.", out reason, id);
            return null;
        }
        if (p == null || p.kind != CraftPieceKind.Metal)
        {
            Refuse("Fasteners are made from metal.", out reason, id);
            return null;
        }
        double mass = p.Mass(Rules), thick = 0;
        foreach (MetalSegment s in p.segments) thick = Math.Max(thick, Math.Min(s.width, s.thickness));
        if (mass > 800 || thick > 14)
        {
            Refuse("It is too big: draw it out into a thin rod first.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.FormFastener, piece = id, i0 = (int)kind };
        Record(op);
        return op.other;
    }

    private void ApplyFormFastener(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        CraftAlloyProfile prof = p.Profile(Rules);
        var kind = (CraftFastenerKind)op.i0;
        double mass = p.Mass(Rules);
        double d = 0;
        foreach (MetalSegment s in p.segments) d += Math.Min(s.width, s.thickness);
        d = Math.Max(2, d / Math.Max(1, p.segments.Count));
        double length = kind == CraftFastenerKind.Buckle ? 40 : 14;
        double each = kind == CraftFastenerKind.Buckle ? Math.Max(15, Math.PI * d * d * 0.25 * 120 * prof.Density / 1000) : Math.PI * d * d * 0.25 * length * prof.Density / 1000 * 1.3;
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
