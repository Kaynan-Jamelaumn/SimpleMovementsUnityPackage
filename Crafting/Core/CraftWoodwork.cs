using System;
using System.Collections.Generic;

/// <summary>
/// The woodshop: riving a billet along its grain, sawing to a line, steaming and bending over a form, seasoning in a
/// drying stack, sanding and varnishing. Wood is fibres: a piece split along them is strong (its <see cref="WoodState.runout"/>
/// stays low), one whose fibres run out of it breaks along them; steamed long enough for its thickness it bends - too
/// fast, too cold or too cross-grained and the outside of the bend tears; dried too fast it checks; a finish keeps the
/// water out, if it goes on dry wood in thin, even coats over a sanded surface.
/// </summary>
public static class CraftWoodwork
{
    /// <summary>Moisture of seasoned wood (it ends near this indoors).</summary>
    public const double Seasoned = 0.12;
    /// <summary>The least width (mm) a half can be after riving.</summary>
    public const double MinRiveWidth = 8;

    // ------------------------------------------------------------------ every step
    /// <summary>Steaming soaks heat and water in; out of the steam the wood cools and sets; in the stack it seasons.</summary>
    public static void Step(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        WoodState w = p.wood;
        double hours = dt * ws.Rules.Settings.processHoursPerSecond;
        double t = MeanTemperature(p);
        if (p.location == CraftLocation.Steam)
        {
            // About an hour of steam for every 25 mm of thickness (once it is hot).
            double soak = Math.Max(0.25, Thickness(p) / 25.0);
            if (t > 85) w.steamed = Math.Min(1, w.steamed + hours / soak);
            w.moisture = Math.Min(0.32, w.moisture + hours * 0.08);
            // Left far too long it goes soft and mushy: the fibres weaken.
            if (w.steamed >= 1) w.checks = Math.Min(1, w.checks + hours * 0.004);
        }
        else if (t < 70)
            w.steamed = Math.Max(0, w.steamed - hours * 0.6); // cooled, it sets again
        // A finish dries.
        if (w.finishWet > 0) w.finishWet = Math.Max(0, w.finishWet - hours * 0.25);
        // Seasoning: in the stack (or hung on the rack) the water goes; too fast and the surface and ends check.
        if (p.location == CraftLocation.Stack || p.location == CraftLocation.Rack)
        {
            bool stack = p.location == CraftLocation.Stack;
            double even = stack ? w.stackEven : 0.4, shade = stack ? w.shade : 0.5, ends = stack ? w.endsSealed : 0;
            double rate = 0.08 * (0.6 + 0.8 * even) * (1.5 - shade) * (1 + Math.Max(0, ws.Ambient - 18) / 30);
            double before = w.moisture;
            w.moisture = Math.Max(Seasoned, w.moisture - (w.moisture - Seasoned) * (1 - Math.Exp(-rate * hours)));
            double lost = before - w.moisture;
            if (lost > 0 && before > 0.2)
            {
                // Water leaving faster than the wood can shrink evenly: checks, worst at unsealed ends, in dense wood.
                CraftWoodDefinition d = ws.Rules.Wood(w.wood);
                double safe = 0.012 * (1.3 - (d?.density ?? 0.7) * 0.5);
                double speed = lost / Math.Max(1e-6, hours);
                if (speed > safe)
                    w.checks = Math.Min(1, w.checks + (speed - safe) * hours * 8 * (1 - 0.75 * ends) * (0.6 + 0.4 * (1 - shade)));
                // Unevenly stickered, it dries unevenly and warps.
                if (even < 0.7)
                    for (int i = 0; i < p.segments.Count; i++)
                        p.segments[i].bend += (0.7 - even) * lost * 30 * (CraftMath.Noise(CraftMath.Hash(p.id ?? ""), i, 41) - 0.5);
            }
        }
    }

    public static double MeanTemperature(CraftPiece p)
    {
        double t = 0;
        foreach (MetalSegment s in p.segments) t += s.body.temp;
        return p.segments.Count > 0 ? t / p.segments.Count : 20;
    }

    public static double Thickness(CraftPiece p)
    {
        double t = 0;
        foreach (MetalSegment s in p.segments) t = Math.Max(t, Math.Min(s.width, s.thickness));
        return t;
    }

    /// <summary>How readily it bends now (0 stiff - 1 fully softened): steamed through, and still hot.</summary>
    public static double Plasticity(CraftPiece p) =>
        p.kind == CraftPieceKind.Wood ? CraftMath.Clamp01(p.wood.steamed) * CraftMath.Smooth(55, 95, MeanTemperature(p)) : 0;

    // ------------------------------------------------------------------ riving
    /// <summary>
    /// Rives the piece in two along its length with the froe. <c>a</c> = how far the split strayed from the grain
    /// (0 followed it - 1 cut straight across it), <c>b</c> = how steady the froe was driven. The halves keep the grain
    /// the split followed: a good split leaves them straighter-grained than the stock was.
    /// </summary>
    public static void Rive(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double stray = CraftMath.Clamp01(op.a), steady = CraftMath.Clamp01(op.b);
        CraftPiece q = ws.Create(op, CraftPieceKind.Wood, $"rived from {p.origin}");
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
        foreach (CraftPiece half in new[] { p, q })
        {
            half.wood.runout = runout;
            foreach (MetalSegment s in half.segments)
            {
                s.width = Math.Max(MinRiveWidth, s.width * 0.5 - 1);
                s.rough = Math.Max(s.rough, 25 + 30 * stray);
            }
        }
        // A split that ran out of the billet: the second half tapers off short.
        if (stray > 0.45)
        {
            int keep = Math.Max(1, (int)Math.Round(q.segments.Count * (1 - (stray - 0.45) * 1.2)));
            if (keep < q.segments.Count) q.segments.RemoveRange(keep, q.segments.Count - keep);
            for (int i = 0; i < q.segments.Count; i++)
                q.segments[i].width = Math.Max(MinRiveWidth * 0.6, q.segments[i].width * (1 - 0.5 * i / Math.Max(1.0, q.segments.Count)));
        }
        // A jerky drive bruises and checks the faces.
        if (steady < 0.5)
        {
            p.wood.checks = Math.Min(1, p.wood.checks + (0.5 - steady) * 0.2);
            q.wood.checks = Math.Min(1, q.wood.checks + (0.5 - steady) * 0.2);
        }
        string how = stray < 0.15 ? "Rived cleanly along the grain" : stray < 0.45 ? "Rived, the split wandering off the grain" : "Rived: the split ran out of the billet";
        p.Record(ws.Elapsed, CraftOpKind.Rive, how, stray);
        q.Record(ws.Elapsed, CraftOpKind.Rive, how, stray);
        ws.Raise(CraftEventKind.Cut, p.id, q.id, "");
    }

    // ------------------------------------------------------------------ sawing
    /// <summary>
    /// Saws across the piece at <c>i0</c> (the split segment), <c>a</c> = how far the saw wandered from the line (0-1),
    /// <c>b</c> = how square the cut is (0-1). A wandering, ragged cut leaves torn, rough ends.
    /// </summary>
    public static CraftPiece SawCut(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftPiece q = CraftForging.Split(ws, p, op.i0, op, op.a < 0.15 ? "sawn square" : "sawn, wandering off the line");
        if (q == null)
            return null;
        double tear = 20 + 70 * CraftMath.Clamp01(op.a) + 40 * (1 - CraftMath.Clamp01(op.b));
        if (p.segments.Count > 0) p.segments[p.segments.Count - 1].rough = Math.Max(p.segments[p.segments.Count - 1].rough, tear);
        if (q.segments.Count > 0) q.segments[0].rough = Math.Max(q.segments[0].rough, tear);
        return q;
    }

    // ------------------------------------------------------------------ bending
    /// <summary>
    /// Bends segments <c>i0</c>-<c>i1</c> over the form by <c>a</c> degrees in all, at a speed <c>b</c> (0 slow and
    /// steady - 1 forced at once), with the strap (<c>flag</c>) holding the outside fibres. Soft (steamed through and
    /// still hot) it takes the bend and keeps most of it; stiff, cross-grained or rushed, the outside tears.
    /// </summary>
    public static void Bend(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        double angle = Math.Max(0, op.a), speed = CraftMath.Clamp01(op.b);
        double length = 0;
        for (int i = a; i <= b; i++) length += p.segments[i].length;
        double plastic = Plasticity(p);
        double tear = BendTear(ws.Rules, p, length, angle, speed, op.flag, out double kept);
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

    /// <summary>
    /// How far a bend of <paramref name="angle"/> degrees over <paramref name="length"/> mm of the piece goes past what it
    /// can take now (0 = it takes it; above 0 the outside tears), and how much of the bend it keeps after springing back.
    /// The bend's tightness is its thickness over its radius: dry wood takes about 1:80; steamed through about 1:25, and
    /// 1:10 with a strap holding the outside fibres; rushed, cross-grained or checked, less.
    /// </summary>
    public static double BendTear(CraftRules rules, CraftPiece p, double length, double angle, double speed, bool strap, out double kept)
    {
        kept = 0;
        if (p == null || p.kind != CraftPieceKind.Wood || angle <= 0)
            return 0;
        double plastic = Plasticity(p);
        double flex = rules.Wood(p.wood.wood)?.flexibility ?? 0.4;
        double radius = Math.Max(1, length) / Math.Max(1e-3, angle * Math.PI / 180);
        double ratio = Thickness(p) / Math.Max(1, radius);
        double tolerance = (0.012 + 0.045 * plastic) * (0.6 + 0.8 * flex) * (strap ? 2.5 : 1)
                           * (1 - 0.6 * CraftMath.Clamp01(p.wood.runout)) * (1 - 0.5 * CraftMath.Clamp01(p.wood.checks)) * (1 - 0.5 * CraftMath.Clamp01(speed));
        double tear = Math.Max(0, ratio / Math.Max(1e-4, tolerance) - 1);
        // It springs back part of the way: less when it was soft.
        kept = angle * (0.45 + 0.5 * plastic) * (tear > 1 ? 0.6 : 1);
        return tear;
    }

    // ------------------------------------------------------------------ the drying stack
    /// <summary>Stacks it to season: <c>a</c> = how evenly the stickers were laid, <c>b</c> = ends sealed, <c>c</c> = the cover (shade).</summary>
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
    /// <summary>
    /// Sands segments <c>i0</c>-<c>i1</c>: <c>a</c> = the grit (0 coarse - 1 fine), <c>b</c> = strokes' worth,
    /// <c>c</c> = how much along the grain (1) rather than across it (0, it leaves scratches).
    /// </summary>
    public static void Sand(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood)
            return;
        double grit = CraftMath.Clamp01(op.a), work = Math.Max(0, op.b), along = CraftMath.Clamp01(op.c);
        double floor = 4 + 30 * (1 - grit) + 25 * (1 - along);
        int a = Math.Max(0, Math.Min(op.i0, op.i1)), b = Math.Min(p.segments.Count - 1, Math.Max(op.i0, op.i1));
        for (int i = a; i <= b; i++)
        {
            MetalSegment s = p.segments[i];
            s.rough = floor + (s.rough - floor) * Math.Exp(-work * (0.15 + 0.3 * (1 - grit)));
            if (s.rough < floor) s.rough += (floor - s.rough) * 0.3;
        }
        // Sanding knocks down the nibs of a dry coat (and cuts into a wet one).
        if (p.wood.finish > 0)
        {
            if (p.wood.finishWet > 0.3) p.wood.finishEven *= 0.85;
            else p.wood.finishEven = Math.Min(1, p.wood.finishEven + 0.1 * along * grit);
            p.wood.finish = Math.Max(0, p.wood.finish - 0.03 * work * (1 - grit));
        }
        p.Record(ws.Elapsed, CraftOpKind.SandWood, along > 0.7 ? "Sanded with the grain" : "Sanded across the grain", grit);
    }

    /// <summary>
    /// Brushes a coat on segments <c>i0</c>-<c>i1</c>: <c>id</c> = the varnish or oil, <c>a</c> = how evenly it was laid
    /// (0-1), <c>b</c> = how thick (0-1). It takes on dry, smooth wood; on damp wood it clouds and lifts, over a rough
    /// surface it is blotchy, laid too thick it runs, over a wet coat it wrinkles.
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
        double adhesion = w.moisture > 0.2 ? 0.35 : 1;
        double smooth = CraftMath.Smooth(60, 10, rough);
        double coat = 0.35 * (0.4 + 0.6 * thick) * adhesion * strength * share;
        double quality = even * (0.5 + 0.5 * smooth) * (w.finishWet > 0.4 ? 0.7 : 1) * (thick > 0.75 ? 1 - (thick - 0.75) * 1.6 : 1);
        double before = w.finish;
        w.finish = Math.Min(1, w.finish + coat);
        w.finishEven = before + coat > 1e-6 ? (w.finishEven * before + quality * coat) / (before + coat) : quality;
        w.finishWet = 1;
        // Where it went on (each segment's coat: what the eye sees).
        for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat / Math.Max(0.05, share));
        bool oil = s != null && s.kind == CraftSupplyKind.Oil;
        if (oil) w.oil = Math.Min(1, w.oil + coat);
        if (thick > 0.75)
            for (int i = a; i <= b; i++) p.segments[i].rough = Math.Max(p.segments[i].rough, 20 + 60 * (thick - 0.75)); // runs and sags
        string how = adhesion < 1 ? "A coat on damp wood: it clouds and will lift" : thick > 0.75 ? "A thick coat: it runs" : even < 0.5 ? "A patchy coat" : "An even coat";
        p.Record(ws.Elapsed, CraftOpKind.Varnish, $"{how} of {s?.Name.ToLowerInvariant() ?? "varnish"}", coat);
    }

    /// <summary>How well the finish keeps water out (0-1).</summary>
    public static double FinishProtection(WoodState w) => CraftMath.Clamp01(w.finish * (0.4 + 0.6 * w.finishEven));
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

    /// <summary>Rives a piece of wood in two along its grain (see <see cref="CraftWoodwork.Rive"/>). Returns the other half's id.</summary>
    public string Rive(string id, double stray, double steadiness, out string reason)
    {
        if (!WoodCheck(id, CraftTools.ChoppingBlock, "There is no froe and block here.", out CraftPiece p, out reason))
            return null;
        double w = 0;
        foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
        if (w < CraftWoodwork.MinRiveWidth * 2 + 2)
        {
            Refuse("It is too narrow to rive again.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.Rive, piece = id, a = (float)CraftMath.Clamp01(stray), b = (float)CraftMath.Clamp01(steadiness) };
        Record(op);
        return op.other;
    }

    /// <summary>Saws a piece of wood across at <paramref name="position"/> (0-1). Returns the far part's id.</summary>
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
        int seg = Math.Min(p.segments.Count - 2, p.SegmentAt(CraftMath.Clamp01(position)));
        var op = new CraftOperation { kind = CraftOpKind.SawCut, piece = id, i0 = seg, a = (float)CraftMath.Clamp01(wander), b = (float)CraftMath.Clamp01(square) };
        Record(op);
        return op.other;
    }

    /// <summary>Bends wood over the form (see <see cref="CraftWoodwork.Bend"/>).</summary>
    public bool BendWood(string id, int from, int to, double degrees, double speed, bool strap, out string reason)
    {
        if (!WoodCheck(id, CraftTools.SteamBox, "There are no bending forms here.", out CraftPiece p, out reason))
            return false;
        if (degrees <= 0)
            return Refuse("It was not bent.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.BendWood, piece = id, i0 = from, i1 = to, a = (float)Math.Min(180, degrees), b = (float)CraftMath.Clamp01(speed), flag = strap });
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

    /// <summary>Brushes a coat of varnish or oil on wood (see <see cref="CraftWoodwork.Varnish"/>).</summary>
    public bool Varnish(string id, string supply, int from, int to, double evenness, double thickness, out string reason)
    {
        if (!WoodCheck(id, CraftTools.FinishingBench, "There is no finishing bench here.", out _, out reason))
            return false;
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (s == null || s.kind != CraftSupplyKind.Varnish && s.kind != CraftSupplyKind.Oil)
            return Refuse("That is not a finish for wood.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Varnish, piece = id, id = s.id, i0 = from, i1 = to, a = (float)CraftMath.Clamp01(evenness), b = (float)CraftMath.Clamp01(thickness) });
        return true;
    }
}
