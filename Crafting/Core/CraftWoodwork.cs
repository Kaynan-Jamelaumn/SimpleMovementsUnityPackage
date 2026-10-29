using System;
using System.Collections.Generic;

/// <summary>
/// The woodshop: debarking a log on the shaving horse, splitting it with wedges and a maul, riving a billet along its
/// grain, sawing to a line, steaming and bending over a form, seasoning in a drying stack, sanding through the grits and
/// sealing (varnish, oil, wax, resin). Wood is fibres: a piece split along them is strong (its <see cref="WoodState.runout"/>
/// stays low), one whose fibres run out of it breaks along them; steamed long enough for its thickness it bends - too
/// fast, too cold or too cross-grained and the outside of the bend tears; dried too fast it checks; a finish keeps the
/// water out, if it goes on dry wood in thin, even coats over a sanded surface.
/// </summary>
public static class CraftWoodwork
{
    /// <summary>Moisture of seasoned wood in ordinary air (it ends near this indoors).</summary>
    public const double Seasoned = 0.12;
    /// <summary>Moisture of a freshly felled log.</summary>
    public const double Green = 0.55;
    /// <summary>The least width (mm) a log can be split with wedges (narrower: rive it with the froe).</summary>
    public const double MinSplitWidth = 60;

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
        // Bark left on wet wood keeps the water in and lets rot and beetles in.
        w.bark = BarkLeft(p);
        if (w.bark > 0.2 && w.moisture > 0.25 && p.location != CraftLocation.Steam)
            w.rot = Math.Min(1, w.rot + hours * 0.002 * w.bark * (w.moisture - 0.2) * 4);
        // Seasoning: in the stack (or hung on the rack) the water goes; too fast and the surface and ends check.
        if (p.location == CraftLocation.Stack || p.location == CraftLocation.Rack)
        {
            bool stack = p.location == CraftLocation.Stack;
            double even = stack ? w.stackEven : 0.4, shade = stack ? w.shade : 0.5, ends = stack ? w.endsSealed : 0;
            // Damp air dries it slower and to a wetter end; bark left on holds the water in.
            double humid = ws.Humidity, settle = Equilibrium(humid);
            double rate = 0.08 * (0.6 + 0.8 * even) * (1.5 - shade) * (1 + Math.Max(0, ws.Ambient - 18) / 30) * (1.35 - 0.7 * humid) * (1 - 0.6 * w.bark);
            double before = w.moisture;
            w.moisture = before > settle
                ? Math.Max(settle, w.moisture - (w.moisture - settle) * (1 - Math.Exp(-rate * hours)))
                : Math.Min(settle, w.moisture + (settle - w.moisture) * (1 - Math.Exp(-rate * 0.5 * hours)));
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
        // Through the grits in order: a fine paper straight after a coarse one cannot take out the coarse scratches
        // (they stay under the polish); each paper only clears what the one before it left.
        double last = p.wood.lastGrit;
        bool skipped = last >= 0 && grit - last > 0.4;
        if (skipped) floor = Math.Max(floor, 4 + 30 * (1 - last) * 0.7);
        // Sanding through bark is wasted paper; sharp tools (and paper) cut faster.
        work *= ws.ToolQuality;
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
        p.wood.lastGrit = Math.Max(last, grit);
        if (skipped) ws.Raise(CraftEventKind.Note, p.id, "skipped_grit", "The coarse scratches still show under the fine paper.");
        p.Record(ws.Elapsed, CraftOpKind.SandWood, skipped ? "Sanded with a fine paper straight after a coarse one: the coarse scratches stay" : along > 0.7 ? "Sanded with the grain" : "Sanded across the grain", grit);
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
        if (s != null && s.kind == CraftSupplyKind.Wax)
        {
            // Wax is rubbed into the surface, not built up as a film: it sheds water and wears away.
            w.wax = Math.Min(1, w.wax + coat * 1.4 * (0.5 + 0.5 * smooth));
            for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat * 0.5 / Math.Max(0.05, share));
            p.Record(ws.Elapsed, CraftOpKind.Varnish, adhesion < 1 ? "Wax rubbed on damp wood: it smears" : "Wax rubbed in", coat);
            return;
        }
        double before = w.finish;
        w.finish = Math.Min(1, w.finish + coat);
        w.finishEven = before + coat > 1e-6 ? (w.finishEven * before + quality * coat) / (before + coat) : quality;
        w.finishWet = 1;
        // Where it went on (each segment's coat: what the eye sees).
        for (int i = a; i <= b; i++) p.segments[i].coating = Math.Min(1, p.segments[i].coating + coat / Math.Max(0.05, share));
        bool oil = s != null && s.kind == CraftSupplyKind.Oil;
        if (oil) w.oil = Math.Min(1, w.oil + coat);
        // Wax sits on the surface (it sheds water, wears off); a resin sealer builds a hard film (it wears well).
        if (s != null && s.id.IndexOf("resin", StringComparison.OrdinalIgnoreCase) >= 0) w.resin = Math.Min(1, w.resin + coat);
        if (thick > 0.75)
            for (int i = a; i <= b; i++) p.segments[i].rough = Math.Max(p.segments[i].rough, 20 + 60 * (thick - 0.75)); // runs and sags
        string how = adhesion < 1 ? "A coat on damp wood: it clouds and will lift" : thick > 0.75 ? "A thick coat: it runs" : even < 0.5 ? "A patchy coat" : "An even coat";
        p.Record(ws.Elapsed, CraftOpKind.Varnish, $"{how} of {s?.Name.ToLowerInvariant() ?? "varnish"}", coat);
    }

    /// <summary>
    /// How well the finish keeps water out (0-1): the film (varnish, resin) by how even it is, oil soaked in, wax on top.
    /// </summary>
    public static double FinishProtection(WoodState w) =>
        CraftMath.Clamp01(w.finish * (0.4 + 0.6 * w.finishEven) + 0.25 * w.oil * (1 - w.finish) + 0.2 * w.wax * (1 - w.finish * 0.5));

    /// <summary>How well the finish stands wear and weather (0-1): a hard resin or varnish film best, wax least.</summary>
    public static double FinishWear(WoodState w) => CraftMath.Clamp01(w.finish * (0.5 + 0.5 * w.finishEven) * (0.7 + 0.3 * w.resin) + 0.15 * w.oil + 0.05 * w.wax);

    // ------------------------------------------------------------------ logs
    /// <summary>
    /// Takes the bark off segments <c>i0</c>-<c>i1</c> with the drawknife: <c>a</c> = how deep the knife bit (0 skimming
    /// the outer bark - 0.5 right under it, at the sapwood - 1 deep into the wood), <c>b</c> = how steady the stroke
    /// was (0-1), <c>c</c> = how much of the way round the log the strokes went (0-1). Green wood (sap up) peels clean;
    /// a dry log's bark is tight and needs digging (gouges). Too deep gouges the wood itself: it loses thickness and is
    /// rough there.
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
        double take = depth < 0.3 ? depth / 0.3 * 0.5 : 1;
        double gouge = Math.Max(0, depth - (green ? 0.62 : 0.52)) * 1.8 * (1.2 - 0.5 * steady) / Math.Max(0.5, tq);
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
        string how = gouge > 0.1 ? "Debarked, the drawknife gouging into the wood" : depth < 0.3 ? "Shaved the outer bark: the inner bark is still on" : green ? "Peeled the bark clean" : "Worked the tight bark off";
        p.Record(ws.Elapsed, CraftOpKind.DebarkWood, how, p.wood.bark);
        if (p.wood.bark < 0.05) ws.Raise(CraftEventKind.Note, p.id, "debarked", "The last of the bark comes away.");
    }

    /// <summary>
    /// Splits a log (or a large half) with wedges and the maul: <c>a</c> = how far off the heart the wedge was set
    /// (0 through the pith - 1 at the bark), <c>b</c> = how true the line followed a check or ray (0 across - 1 along),
    /// <c>c</c> = how square and solid the blows landed (0-1), <c>i0</c> = blows it took. Through the heart and along the
    /// rays it splits straight into two even halves whose grain follows them; off the heart the halves are uneven, and a
    /// line across the rays twists out and leaves runout and splinters.
    /// </summary>
    public static void SplitLog(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0)
            return;
        double off = CraftMath.Clamp01(op.a), along = CraftMath.Clamp01(op.b), solid = CraftMath.Clamp01(op.c);
        int blows = Math.Max(1, op.i0);
        CraftPiece q = ws.Create(op, CraftPieceKind.Wood, $"split from {p.origin}");
        string id = q.id;
        CraftPiece copy = p.Clone();
        q.wood = copy.wood;
        q.segments = copy.segments;
        q.record = copy.record;
        q.tags = copy.tags;
        q.location = CraftLocation.Air;
        q.id = id;
        // Off the heart the halves are uneven; the smaller one is the far side of the wedge.
        double share = 0.5 + 0.38 * off;
        double runout = CraftMath.Clamp01(p.wood.runout * (0.5 + 0.5 * (1 - along)) + (1 - along) * 0.5 + off * 0.1);
        bool first = true;
        foreach (CraftPiece half in new[] { p, q })
        {
            double f = first ? share : 1 - share;
            first = false;
            half.wood.log = false;
            half.wood.runout = runout;
            foreach (MetalSegment s in half.segments)
            {
                s.width = Math.Max(6, s.width * f - 1);
                s.rough = Math.Max(s.rough, 30 + 50 * (1 - along) + 30 * (1 - solid));
            }
            // Glancing, uneven blows (and many of them) bruise and check the ends.
            if (solid < 0.5 || blows > 8)
                half.wood.checks = Math.Min(1, half.wood.checks + (0.5 - Math.Min(0.5, solid)) * 0.3 + Math.Max(0, blows - 8) * 0.01);
        }
        // A line across the rays twists out: the halves end in splinters for part of their length.
        if (along < 0.4)
        {
            int n = q.segments.Count;
            int bad = Math.Max(1, (int)Math.Round(n * (0.4 - along)));
            for (int i = n - bad; i < n; i++) q.segments[i].width = Math.Max(6, q.segments[i].width * 0.6);
            q.AddDefect(CraftDefectKind.Crack, n - 1, 0.4 - along, true, "the split twisted out across the grain");
        }
        string how = off < 0.15 && along > 0.75 ? "Split cleanly through the heart" : off >= 0.15 ? "Split off the heart: the halves are uneven" : "Split, the line twisting across the grain";
        p.Record(ws.Elapsed, CraftOpKind.SplitLog, how, off);
        q.Record(ws.Elapsed, CraftOpKind.SplitLog, how, off);
        ws.Raise(CraftEventKind.Cut, p.id, q.id, how);
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
        double w = 0;
        foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
        if (w < CraftWoodwork.MinSplitWidth)
        {
            Refuse("It is small enough to rive with the froe.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.SplitLog, piece = id, a = (float)CraftMath.Clamp01(offHeart), b = (float)CraftMath.Clamp01(alongRays), c = (float)CraftMath.Clamp01(solid), i0 = Math.Max(1, Math.Min(99, blows)) };
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
        if (s == null || s.kind != CraftSupplyKind.Varnish && s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That is not a finish for wood.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Varnish, piece = id, id = s.id, i0 = from, i1 = to, a = (float)CraftMath.Clamp01(evenness), b = (float)CraftMath.Clamp01(thickness) });
        return true;
    }
}
