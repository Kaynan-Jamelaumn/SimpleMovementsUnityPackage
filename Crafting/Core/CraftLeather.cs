using System;
using System.Collections.Generic;

/// <summary>
/// Hides and leather, cell by cell.
/// <list type="bullet">
/// <item><b>Raw hides</b> vary by animal (size, thickness along the back and at the belly, fibre, hair, fat, flaws)
/// and rot when left raw and wet; lime slows it.</item>
/// <item><b>Scraping</b> at the beam: on the flesh side it takes flesh and fat off where the knife goes - pressed too
/// hard where it is clean or thin, it scores the hide; on the grain side it takes off hair loosened by lime (and scuffs
/// the grain when pressed hard or when the hair is not loose).</item>
/// <item><b>Vats</b>: water soaks (and washes out alum, and hardens and shrinks tanned leather when hot); lime loosens
/// the hair (too long and the grain goes spongy); tanning liquors tan the outside first and the core over time -
/// slower in a thick hide, where flesh was left, in the cold; a liquor too strong on a raw hide closes its outside and
/// leaves the core raw (case hardening). The liquor is used up as it tans.</item>
/// <item><b>Drying</b> (rack, frame, room) is faster when warm, moving and dry; unstretched leather shrinks and
/// stiffens as it dries unless it is staked while damp; laced in the frame it dries flat and larger - and tears where
/// the tension is more than it can take.</item>
/// <item><b>Finishing</b>: staking softens; oils make it supple and shed water (and block dye: dye first); wax sheds
/// water and stiffens; dye goes where it is brushed; slicking smooths; skiving thins (and holes it if it goes too far).</item>
/// <item><b>Cutting</b> a pattern takes those cells - flaws, thin spots and all - into a part; a careless cut leaves
/// ragged edges.</item>
/// </list>
/// </summary>
public static class CraftLeather
{
    /// <summary>The mean thickness (mm) of what is left of a hide or a piece of cloth.</summary>
    public static double MeanThickness(CraftRules rules, CraftPiece p)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in p.hide.cells)
            if (c.present)
            {
                t += c.thickness;
                n++;
            }
        return n > 0 ? t / n : 0;
    }

    /// <summary>A fresh hide of an animal (the same seed always gives the same hide).</summary>
    public static CraftPiece NewHide(CraftRules rules, string hideId, int seed)
    {
        CraftHideDefinition d = rules.Hide(hideId);
        if (d == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Hide, origin = d.textile ? $"cloth:{d.id}" : $"hide:{d.id}" };
        HideState h = p.hide;
        h.hide = d.id;
        h.seed = seed;
        h.width = d.width;
        h.height = d.height;
        h.shrinkTemperature = 60;
        double cx = (d.width - 1) * 0.5, cy = (d.height - 1) * 0.5;
        for (int y = 0; y < d.height; y++)
            for (int x = 0; x < d.width; x++)
            {
                int k = y * d.width + x;
                double ex = (x - cx) / Math.Max(0.5, cx + 0.5), ey = (y - cy) / Math.Max(0.5, cy + 0.5);
                double outline = ex * ex + ey * ey;
                // A bolt of cloth is a straight-edged rectangle; a hide has the animal's outline.
                bool present = d.textile || outline <= 1.05 + 0.2 * (CraftMath.Noise(seed, k, 1) - 0.5) || d.width * d.height <= 6;
                double back = 1 - Math.Abs(y - cy) / Math.Max(0.5, cy + 0.5);
                double thick = CraftMath.Lerp(d.thicknessBelly, d.thicknessBack, back) * (0.9 + 0.2 * CraftMath.Noise(seed, k, 2));
                if (outline > 0.75) thick *= 0.85; // legs, neck and flanks are thinner
                var c = new HideCell
                {
                    present = present, thickness = thick, flesh = CraftMath.Clamp01(d.fat * (0.7 + 0.4 * CraftMath.Noise(seed, k, 3))), hair = d.hair,
                    moisture = 0.75, grain = 1,
                };
                if (d.textile)
                {
                    // Woven and finished: nothing to flesh or tan, dry and supple; a slub or a pulled thread here and there.
                    c.thickness = CraftMath.Lerp(d.thicknessBelly, d.thicknessBack, 0.5) * (0.95 + 0.1 * CraftMath.Noise(seed, k, 2));
                    c.flesh = c.hair = 0;
                    c.tanS = c.tanC = 1;
                    c.moisture = 0.08;
                    c.stiff = 1 - d.suppleness;
                    c.smooth = 0.6;
                }
                if (CraftMath.Noise(seed, k, 4) < d.flaws)
                {
                    c.flaw = true;
                    c.damage = (d.textile ? 0.15 : 0.25) + 0.55 * CraftMath.Noise(seed, k, 5);
                }
                h.cells.Add(c);
            }
        if (d.textile)
            h.dried = true;
        return p;
    }

    private static double Hours(CraftWorkshop ws, double dt) => dt * ws.Rules.Settings.processHoursPerSecond;

    // ------------------------------------------------------------------ every step
    public static void StepHide(CraftWorkshop ws, CraftPiece p, double dt)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        double hours = Hours(ws, dt);
        if (p.location == CraftLocation.Assembled)
            return;
        double tannage = MeanTannage(h);
        bool raw = tannage < 0.3;
        if (raw)
            h.hoursRaw += hours;
        if (p.location == CraftLocation.Vat && p.vat >= 0 && p.vat < ws.Vats.Count)
            InVat(ws, p, ws.Vats[p.vat], hours);
        else
            Drying(ws, p, hours);
        // Rot: raw, wet and warm (lime keeps it away).
        double warm = Math.Pow(2, (ws.Ambient - 20) / 10);
        CraftVatState vat = p.location == CraftLocation.Vat && p.vat < ws.Vats.Count ? ws.Vats[p.vat] : null;
        bool limed = vat != null && rules.Agent(vat.agent)?.kind == CraftAgentKind.Lime;
        bool rotting = false;
        for (int i = 0; i < h.cells.Count; i++)
        {
            HideCell c = h.cells[i];
            if (!c.present || c.Tannage > 0.3 || c.moisture < 0.35 || limed) continue;
            c.rot += hours / rules.Settings.rotHours * warm * (0.5 + c.flesh);
            if (c.rot > 0.4)
            {
                c.hair = Math.Max(0, c.hair - hours * 0.05);
                c.grain = Math.Max(0, c.grain - hours * 0.01);
                c.damage = Math.Min(1, c.damage + hours * 0.004);
                rotting = true;
            }
        }
        if (rotting && ws.Step % 50 == 0)
            p.AddDefect(CraftDefectKind.Rot, -1, MeanRot(h), true, "left raw and wet too long");
    }

    public static double MeanTannage(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.Tannage; n++; }
        return n > 0 ? t / n : 0;
    }

    private static double MeanRot(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.rot; n++; }
        return n > 0 ? t / n : 0;
    }

    private static void InVat(CraftWorkshop ws, CraftPiece p, CraftVatState vat, double hours)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        CraftTanningAgent agent = rules.Agent(vat.agent);
        double temp = vat.temperature;
        double absorbed = 0;
        bool closing = false, shrinking = false, hardening = false;
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            c.moisture = Math.Min(1, c.moisture + hours * 2);
            if (agent == null) continue;
            switch (agent.kind)
            {
                case CraftAgentKind.Water:
                {
                    // Alum washes out; hot water hardens and shrinks tanned leather, and cooks raw hide.
                    CraftTanningAgent tan = rules.Agent(h.agent);
                    if (tan != null && tan.fixation < 0.99 && c.Tannage > 0)
                    {
                        double loss = (1 - tan.fixation) * hours * 0.05;
                        c.tanS *= 1 - loss;
                        c.tanC *= 1 - loss;
                    }
                    if (temp > 55 && c.Tannage > 0.5)
                    {
                        c.hard = Math.Min(1, c.hard + hours * 0.5 * (temp - 55) / 30);
                        c.stiff = Math.Max(c.stiff, c.hard);
                        c.shrink = Math.Min(0.4, c.shrink + hours * 0.08 * (temp - 55) / 30);
                        hardening = true;
                    }
                    if (temp > h.shrinkTemperature)
                    {
                        c.shrink = Math.Min(0.6, c.shrink + hours * 0.4 * (temp - h.shrinkTemperature) / 10);
                        if (c.Tannage < 0.5) c.damage = Math.Min(1, c.damage + hours * 0.3 * (temp - h.shrinkTemperature) / 10);
                        shrinking = true;
                    }
                    break;
                }
                case CraftAgentKind.Lime:
                {
                    double warm = Math.Pow(2, (temp - 20) / 10);
                    c.hairLoose = Math.Min(1, c.hairLoose + hours / Math.Max(0.1, agent.actionHours) * warm * (0.5 + vat.strength));
                    c.rot = Math.Max(0, c.rot - hours * 0.02);
                    if (c.hairLoose >= 1)
                        c.grain = Math.Max(0, c.grain - hours * 0.003 * warm); // over-limed: the grain goes spongy
                    break;
                }
                case CraftAgentKind.Acid:
                    c.damage = Math.Min(1, c.damage + hours * 0.01 * vat.strength);
                    break;
                default:
                {
                    double conc = CraftMath.Clamp01(vat.strength);
                    double warm = CraftMath.Clamp(Math.Pow(2, (temp - agent.bestTemperature) / 10), 0.1, 2);
                    double flesh = 1 - 0.8 * CraftMath.Clamp01(c.flesh);
                    double kS = 2.3 * 6 / agent.hoursThrough, kC = 2.3 / agent.hoursThrough * Math.Pow(4 / Math.Max(0.5, c.thickness), 2);
                    double s0 = c.tanS, c0 = c.tanC;
                    c.tanS += kS * conc * warm * flesh * c.open * (1 - c.tanS) * hours;
                    c.tanC += kC * Math.Max(0, c.tanS - c.tanC) * c.open * warm * (0.6 + 0.4 * vat.stirred) * hours;
                    c.tanS = Math.Min(1, c.tanS);
                    c.tanC = Math.Min(1, c.tanC);
                    if (conc > agent.caseHardeningAbove && c.tanC < 0.4)
                    {
                        c.open = Math.Max(0.05, c.open - (conc - agent.caseHardeningAbove) * hours * 0.15);
                        closing |= c.open < 0.5;
                    }
                    if (agent.kind == CraftAgentKind.Oil)
                        c.oil = Math.Min(1, c.oil + hours * 0.01 * conc);
                    if (temp > agent.maxTemperature && c.Tannage < 0.6)
                    {
                        c.damage = Math.Min(1, c.damage + hours * 0.05 * (temp - agent.maxTemperature) / 10);
                        c.shrink = Math.Min(0.5, c.shrink + hours * 0.05 * (temp - agent.maxTemperature) / 10);
                        shrinking = true;
                    }
                    absorbed += c.tanS - s0 + c.tanC - c0;
                    break;
                }
            }
        }
        if (agent != null && absorbed > 0)
        {
            vat.strength = Math.Max(0, vat.strength - absorbed * 0.0005 * 60 / Math.Max(10, vat.volume));
            double tannage = MeanTannage(h);
            if (string.IsNullOrEmpty(h.agent) || tannage > 0.1 && absorbed > 1e-4)
            {
                CraftTanningAgent was = rules.Agent(h.agent);
                if (was == null || was.kind == CraftAgentKind.Water || was.kind == CraftAgentKind.Lime || was.id == agent.id || absorbed > 0.01)
                    h.agent = agent.id;
            }
            h.shrinkTemperature = CraftMath.Lerp(60, agent.shrinkTemperature, tannage);
            if (agent.fireResistance > 0)
                h.fireResistance = Math.Max(h.fireResistance, agent.fireResistance * tannage);
            h.dried = false;
        }
        if (closing && ws.Step % 50 == 0)
            p.AddDefect(CraftDefectKind.CaseHardened, -1, 0.5, false, "tanned too strong too soon: the outside closed and the core stayed raw");
        if (shrinking && ws.Step % 50 == 0)
            p.AddDefect(CraftDefectKind.Shrunk, -1, MeanShrink(h), true, "too hot while wet: it shrank");
        if (hardening && ws.Step % 200 == 0)
            p.Record(ws.Elapsed, CraftOpKind.SetVatHeat, $"Hardened in hot water at {CraftText.Celsius(temp)}");
    }

    private static double MeanShrink(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.shrink; n++; }
        return n > 0 ? t / n : 0;
    }

    private static void Drying(CraftWorkshop ws, CraftPiece p, double hours)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        double humidity = CraftMath.Clamp01(ws.Variable("humidity"));
        double warm = 1 + Math.Max(0, ws.Ambient - 15) / 15;
        double air = p.location == CraftLocation.Rack ? 1.3 : p.location == CraftLocation.Frame ? 1.2 : 0.8;
        double rate = warm * air * (1.25 - humidity) / rules.Settings.dryingHours;
        bool framed = p.location == CraftLocation.Frame;
        CraftTanningAgent agent = rules.Agent(h.agent);
        double board = agent != null ? agent.stiffness + 0.35 : 0.95;
        bool wasDry = h.dried;
        bool allDry = true;
        double tension = (h.tensionN + h.tensionE + h.tensionS + h.tensionW) * 0.25;
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            double before = c.moisture;
            if (c.moisture > 0.08)
                c.moisture = Math.Max(0.08, c.moisture - rate * hours * (4 / Math.Max(1, c.thickness)));
            double lost = before - c.moisture;
            if (lost > 0 && before < 0.55)
            {
                // Drying through the damp stage stiffens it (unless it is staked), shrinks it unless stretched.
                c.stiff = Math.Min(Math.Max(board, c.hard), c.stiff + lost * 1.6 * (c.Tannage < 0.2 ? 1.5 : 1) * (1 - 0.6 * c.oil));
                if (framed)
                    c.stretch = Math.Max(c.stretch, tension * 0.15);
                else
                    c.shrink = Math.Min(0.3, c.shrink + lost * 0.12 * (1 - c.oil));
                if (warm > 2 && c.Tannage > 0.3 && (agent?.kind ?? CraftAgentKind.Vegetable) == CraftAgentKind.Vegetable)
                    c.grain = Math.Max(0.3, c.grain - lost * 0.05); // dried too fast and hot: the grain cracks
            }
            if (c.moisture > 0.16) allDry = false;
        }
        if (allDry && !wasDry && MeanTannage(h) > 0.3)
        {
            h.dried = true;
            ws.Raise(CraftEventKind.Dried, p.id, "", "It has dried.");
            p.Record(ws.Elapsed, CraftOpKind.Stretch, framed ? "Dried in the frame" : p.location == CraftLocation.Rack ? "Dried on the rack" : "Dried");
        }
    }

    public static void OnEnterVat(CraftWorkshop ws, CraftPiece p)
    {
        if (p.kind != CraftPieceKind.Hide || p.vat < 0 || p.vat >= ws.Vats.Count)
            return;
        CraftTanningAgent a = ws.Rules.Agent(ws.Vats[p.vat].agent);
        if (a != null)
            p.Record(ws.Elapsed, CraftOpKind.MovePiece, $"Put in {a.Name.ToLowerInvariant()} ({ws.Vats[p.vat].strength * 100:0}% strength, {CraftText.Celsius(ws.Vats[p.vat].temperature)})");
    }

    // ------------------------------------------------------------------ strokes over the cells
    /// <summary>Calls <paramref name="touch"/>(cell, weight) for the cells under a stroke (points: x, y pairs in cells) of <paramref name="radius"/> cells.</summary>
    public static void Stroke(HideState h, List<float> points, double radius, Action<HideCell, double> touch)
    {
        if (points == null || points.Count < 2)
            return;
        var weight = new double[h.cells.Count];
        void Sample(double x, double y, double w)
        {
            int x0 = (int)Math.Floor(x - radius), x1 = (int)Math.Ceiling(x + radius);
            int y0 = (int)Math.Floor(y - radius), y1 = (int)Math.Ceiling(y + radius);
            for (int cy = Math.Max(0, y0); cy <= Math.Min(h.height - 1, y1); cy++)
                for (int cx = Math.Max(0, x0); cx <= Math.Min(h.width - 1, x1); cx++)
                {
                    double dx = cx + 0.5 - x, dy = cy + 0.5 - y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;
                    weight[cy * h.width + cx] += w * (1 - d / radius * 0.5);
                }
        }
        if (points.Count == 2)
            Sample(points[0], points[1], 1);
        for (int k = 2; k + 1 < points.Count; k += 2)
        {
            double ax = points[k - 2], ay = points[k - 1], bx = points[k], by = points[k + 1];
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int n = Math.Max(1, (int)Math.Ceiling(len / 0.25));
            for (int s = 0; s < n; s++)
            {
                double t = (s + 0.5) / n;
                Sample(ax + (bx - ax) * t, ay + (by - ay) * t, len / n);
            }
        }
        for (int i = 0; i < weight.Length; i++)
            if (weight[i] > 1e-6 && h.cells[i].present)
                touch(h.cells[i], Math.Min(2, weight[i]));
    }

    public static void Scrape(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        bool grainSide = op.flag;
        double pressure = CraftMath.Clamp01(op.a), angle = CraftMath.Clamp01(op.b);
        bool scored = false;
        Stroke(p.hide, op.points, 0.7, (c, w) =>
        {
            if (!grainSide)
            {
                double before = c.flesh, damage0 = c.damage;
                c.flesh = Math.Max(0, c.flesh - pressure * w * 0.35 * (0.6 + 0.4 * angle));
                double clean = before < 0.12 ? 1 : 0;
                double excess = pressure - 0.55 * c.thickness / 3 - 0.25 * angle;
                if (excess > 0 && (clean > 0 || c.thickness < 2))
                {
                    c.damage = Math.Min(1, c.damage + excess * w * 0.15);
                    scored |= c.damage > 0.25 && damage0 <= 0.25 || c.damage - damage0 > 0.03; // the knife's work, not an old scar
                }
            }
            else
            {
                c.hair = Math.Max(0, c.hair - pressure * w * 0.5 * (0.2 + 0.8 * c.hairLoose));
                c.grain = Math.Max(0, c.grain - Math.Max(0, pressure - 0.5) * w * 0.15 - (c.hairLoose < 0.3 ? pressure * w * 0.05 : 0));
            }
        });
        if (scored)
            p.AddDefect(CraftDefectKind.Score, -1, 0.3, true, "the knife scored the hide where it was already clean or thin");
        p.Record(ws.Elapsed, CraftOpKind.Scrape, grainSide ? "Scraped the hair off" : "Fleshed", pressure);
        ws.Raise(CraftEventKind.Scraped, p.id, grainSide ? "grain" : "flesh", "");
    }

    public static void Stretch(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        HideState h = p.hide;
        h.tensionN = CraftMath.Clamp01(op.a);
        h.tensionE = CraftMath.Clamp01(op.b);
        h.tensionS = CraftMath.Clamp01(op.c);
        h.tensionW = CraftMath.Clamp01(op.d);
        CraftHideDefinition d = ws.Rules.Hide(h.hide);
        double fiber = d != null ? d.fiberStrength : 1;
        // Each side's tension pulls on its edge cells; uneven tension distorts it.
        Edge(h, 0, h.tensionN, fiber, p);
        Edge(h, 1, h.tensionE, fiber, p);
        Edge(h, 2, h.tensionS, fiber, p);
        Edge(h, 3, h.tensionW, fiber, p);
        double mean = (h.tensionN + h.tensionE + h.tensionS + h.tensionW) * 0.25;
        double uneven = Math.Abs(h.tensionN - h.tensionS) + Math.Abs(h.tensionE - h.tensionW);
        foreach (HideCell c in h.cells)
            if (c.present && c.moisture > 0.3)
                c.stretch = Math.Max(c.stretch * 0.8, mean * 0.12);
        if (uneven > 0.5)
            p.SetTag("distortion", Math.Max(p.Tag("distortion"), uneven - 0.5));
        p.Record(ws.Elapsed, CraftOpKind.Stretch, "Laced and stretched in the frame", mean);
    }

    private static void Edge(HideState h, int side, double tension, double fiber, CraftPiece p)
    {
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                bool edge = side == 0 ? y == FirstRow(h, x, true) : side == 2 ? y == FirstRow(h, x, false) : side == 1 ? x == FirstCol(h, y, false) : x == FirstCol(h, y, true);
                if (!edge) continue;
                HideCell c = h.Cell(x, y);
                if (c == null || !c.present) continue;
                double strength = fiber * (0.4 + 0.15 * c.thickness) * (1 - 0.8 * c.damage) * (1 - 0.5 * c.rot);
                if (tension > strength)
                {
                    c.damage = Math.Min(1, c.damage + (tension - strength) * 0.8);
                    p.AddDefect(CraftDefectKind.Tear, y * h.width + x, tension - strength, true, "it tore where the frame pulled harder than it could take");
                }
            }
    }

    private static int FirstRow(HideState h, int x, bool top)
    {
        for (int k = 0; k < h.height; k++)
        {
            int y = top ? k : h.height - 1 - k;
            if (h.Cell(x, y)?.present == true) return y;
        }
        return -1;
    }

    private static int FirstCol(HideState h, int y, bool left)
    {
        for (int k = 0; k < h.width; k++)
        {
            int x = left ? k : h.width - 1 - k;
            if (h.Cell(x, y)?.present == true) return x;
        }
        return -1;
    }

    public static void Stake(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double pressure = CraftMath.Clamp01(op.a);
        CraftTanningAgent agent = ws.Rules.Agent(p.hide.agent);
        bool veg = agent != null && agent.kind == CraftAgentKind.Vegetable;
        Stroke(p.hide, op.points, 0.9, (c, w) =>
        {
            double k = c.moisture > 0.55 ? 0.05 : c.moisture > 0.15 ? 0.25 : 0.08;
            c.stiff = Math.Max(c.hard * 0.9, c.stiff - pressure * w * k);
            if (c.moisture <= 0.15 && veg)
                c.grain = Math.Max(0.2, c.grain - pressure * w * 0.03);
        });
        p.Record(ws.Elapsed, CraftOpKind.Stake, "Staked (worked soft)", pressure);
    }

    public static void Condition(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        if (p == null || p.kind != CraftPieceKind.Hide || s == null)
            return;
        double amount = Math.Max(0, op.a) * Math.Max(0.1, s.strength);
        void Apply(HideCell c, double w)
        {
            if (s.kind == CraftSupplyKind.Wax)
            {
                c.wax = Math.Min(1, c.wax + amount * w * 0.5);
                c.stiff = Math.Min(1, c.stiff + amount * w * 0.05);
            }
            else
            {
                double add = Math.Min(1 - c.oil, amount * w * 0.4 * (c.moisture < 0.6 ? 1 : 0.4));
                c.oil += add;
                c.stiff = Math.Max(c.hard * 0.8, c.stiff - add * 0.5);
            }
        }
        if (op.points == null || op.points.Count < 2)
            foreach (HideCell c in p.hide.cells)
            {
                if (c.present) Apply(c, 1);
            }
        else
            Stroke(p.hide, op.points, 1.0, Apply);
        p.Record(ws.Elapsed, CraftOpKind.Condition, $"Worked in {s.Name.ToLowerInvariant()}");
    }

    public static void Dye(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        if (p == null || p.kind != CraftPieceKind.Hide || s == null)
            return;
        double amount = Math.Max(0, op.a);
        Stroke(p.hide, op.points, 0.8, (c, w) =>
        {
            double take = Math.Min(1 - c.dye, amount * w * 0.5 * (1 - 0.8 * c.oil) * (1 - 0.6 * c.wax));
            if (take <= 0) return;
            double total = c.dye + take;
            c.dyeR = (c.dyeR * c.dye + s.color.r * take) / total;
            c.dyeG = (c.dyeG * c.dye + s.color.g * take) / total;
            c.dyeB = (c.dyeB * c.dye + s.color.b * take) / total;
            c.dye = total;
        });
        p.Record(ws.Elapsed, CraftOpKind.Dye, $"Dyed {s.Name.ToLowerInvariant()}");
    }

    public static void Slick(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double pressure = CraftMath.Clamp01(op.a);
        Stroke(p.hide, op.points, 0.8, (c, w) =>
        {
            double damp = c.moisture > 0.2 && c.moisture < 0.5 ? 1.5 : 1;
            c.smooth = Math.Min(1, c.smooth + pressure * w * 0.3 * damp * c.grain);
        });
        if (!string.IsNullOrEmpty(p.hide.pattern))
            p.hide.edgeFinish = Math.Min(1, p.hide.edgeFinish + pressure * 0.2);
        p.Record(ws.Elapsed, CraftOpKind.Slick, "Slicked and burnished", pressure);
    }

    public static void Skive(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double depth = Math.Max(0, op.a);
        bool holed = false;
        Stroke(p.hide, op.points, 0.7, (c, w) =>
        {
            c.thickness = Math.Max(0.3, c.thickness - depth * w);
            if (c.thickness < 0.6)
            {
                c.damage = Math.Min(1, c.damage + 0.5 * w);
                holed = true;
            }
        });
        if (holed)
            p.AddDefect(CraftDefectKind.Hole, -1, 0.5, true, "skived through");
        p.Record(ws.Elapsed, CraftOpKind.Skive, "Skived thinner", depth);
    }

    /// <summary>Do the cells under <paramref name="pattern"/> at (x, y) all exist (are still part of the hide)?</summary>
    public static bool Fits(HideState h, CraftPattern pattern, int x, int y, int rotation)
    {
        pattern.Size(rotation, out int w, out int hh);
        for (int py = 0; py < hh; py++)
            for (int px = 0; px < w; px++)
            {
                if (!pattern.Covers(px, py, rotation)) continue;
                HideCell c = h.Cell(x + px, y + py);
                if (c == null || !c.present) return false;
            }
        return true;
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ vats
    /// <summary>Fills a vat with an agent (<paramref name="doses"/> of it), or strengthens it; water into a liquor dilutes it.</summary>
    public bool FillVat(int vat, string agent, int doses, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count)
            return Refuse("There is no such vat here.", out reason);
        CraftTanningAgent a = Rules.Agent(agent);
        if (a == null)
            return Refuse("That does not go in a vat.", out reason);
        CraftVatState v = Vats[vat];
        if (v.volume > 0.01 && !string.IsNullOrEmpty(v.agent) && v.agent != a.id && a.kind != CraftAgentKind.Water)
            return Refuse("The vat holds something else: empty it first.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.FillVat, i0 = vat, id = a.id, i1 = Math.Max(1, doses) });
        return true;
    }

    private void ApplyFillVat(CraftOperation op)
    {
        CraftTanningAgent a = Rules.Agent(op.id);
        if (a == null || op.i0 < 0 || op.i0 >= Vats.Count)
            return;
        CraftVatState v = Vats[op.i0];
        if (v.volume <= 0.01 || string.IsNullOrEmpty(v.agent))
        {
            v.agent = a.id;
            v.volume = 60;
            v.strength = a.kind == CraftAgentKind.Water ? 0 : Math.Min(1, a.dose * op.i1);
            v.temperature = Ambient;
        }
        else if (a.kind == CraftAgentKind.Water && v.agent != a.id)
            v.strength *= Math.Pow(0.6, op.i1);
        else
            v.strength = Math.Min(1, v.strength + a.dose * op.i1);
    }

    public bool DrainVat(int vat, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count)
            return Refuse("There is no such vat here.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.DrainVat, i0 = vat });
        return true;
    }

    private void ApplyDrainVat(CraftOperation op)
    {
        if (op.i0 < 0 || op.i0 >= Vats.Count)
            return;
        CraftVatState v = Vats[op.i0];
        v.agent = "";
        v.volume = 0;
        v.strength = 0;
        v.heat = 0;
        foreach (CraftPiece p in pieces)
            if (p.location == CraftLocation.Vat && p.vat == op.i0)
                p.location = CraftLocation.Air;
    }

    /// <summary>A fire under a vat (0-1): hot water for hardening leather.</summary>
    public bool SetVatHeat(int vat, double heat, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count)
            return Refuse("There is no such vat here.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.SetVatHeat, i0 = vat, a = (float)CraftMath.Clamp01(heat) });
        return true;
    }

    public bool StirVat(int vat, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count || Vats[vat].volume <= 0)
            return Refuse("There is nothing to stir.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.StirVat, i0 = vat });
        return true;
    }

    // ------------------------------------------------------------------ hide work
    private bool HideCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason, bool allowFrame = false, bool clothToo = false)
    {
        reason = "";
        p = Piece(id);
        if ((Tools & tool) == 0) // any of the tools named will do
            return Refuse(missing, out reason, id);
        if (p == null || p.kind != CraftPieceKind.Hide)
            return Refuse("That is not a hide.", out reason, id);
        if (!clothToo && CraftTextiles.IsCloth(Rules, p))
            return Refuse("It is cloth: it needs no fleshing, staking or tanning.", out reason, id);
        if (p.location != CraftLocation.Air && !(allowFrame && (p.location == CraftLocation.Frame || p.location == CraftLocation.Rack)))
            return Refuse("Take it out first.", out reason, id);
        return true;
    }

    /// <summary>A stroke of the knife at the beam along <paramref name="path"/> (cells, x/y pairs): the flesh or the grain side.</summary>
    public bool Scrape(string id, IList<float> path, bool grainSide, double pressure, double angle, out string reason)
    {
        if (!HideCheck(id, CraftTools.FleshingBeam, "There is no fleshing beam here.", out CraftPiece p, out reason))
            return false;
        if (path == null || path.Count < 2)
            return Refuse("The knife did not touch it.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.Scrape, piece = id, flag = grainSide, a = (float)pressure, b = (float)angle };
        op.points.AddRange(path);
        Record(op);
        return true;
    }

    /// <summary>The tension on each side of the frame (0-1): north, east, south, west.</summary>
    public bool Stretch(string id, double north, double east, double south, double west, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null || p.kind != CraftPieceKind.Hide || p.location != CraftLocation.Frame)
            return Refuse("Lace it in the frame first.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Stretch, piece = id, a = (float)north, b = (float)east, c = (float)south, d = (float)west });
        return true;
    }

    private bool PathOp(CraftOpKind kind, string id, IList<float> path, float a, string supply, CraftTools tool, string missing, out string reason, bool allowFrame = true, bool clothToo = false)
    {
        if (!HideCheck(id, tool, missing, out CraftPiece p, out reason, allowFrame, clothToo))
            return false;
        var op = new CraftOperation { kind = kind, piece = id, a = a, id = supply ?? "" };
        if (path != null) op.points.AddRange(path);
        Record(op);
        return true;
    }

    public bool Stake(string id, IList<float> path, double pressure, out string reason) =>
        PathOp(CraftOpKind.Stake, id, path, (float)pressure, null, FinishingTools, NoFinishing, out reason);

    /// <summary>Works oil or wax in (along a path, or all over when <paramref name="path"/> is null).</summary>
    public bool Condition(string id, string supply, IList<float> path, double amount, out string reason)
    {
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (s == null || s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That does not condition leather.", out reason, id);
        return PathOp(CraftOpKind.Condition, id, path, (float)amount, s.id, FinishingTools, NoFinishing, out reason, true, true);
    }

    public bool Dye(string id, string dye, IList<float> path, double amount, out string reason)
    {
        CraftSupplyDefinition s = Rules.Supply(dye);
        if (s == null || s.kind != CraftSupplyKind.Dye)
            return Refuse("That is not a dye.", out reason, id);
        return PathOp(CraftOpKind.Dye, id, path, (float)amount, s.id, FinishingTools, NoFinishing, out reason, true, true);
    }

    /// <summary>
    /// The finishing of leather - staking, slicking, skiving, oiling, dyeing - is done at a currier's beam in a tannery
    /// or at a leather bench: either will do.
    /// </summary>
    public const CraftTools FinishingTools = CraftTools.LeatherBench | CraftTools.FleshingBeam;
    private const string NoFinishing = "There is no leather bench or fleshing beam here to finish leather on.";

    public bool Slick(string id, IList<float> path, double pressure, out string reason) =>
        PathOp(CraftOpKind.Slick, id, path, (float)pressure, null, FinishingTools, NoFinishing, out reason);

    public bool Skive(string id, IList<float> path, double depth, out string reason) =>
        PathOp(CraftOpKind.Skive, id, path, (float)depth, null, FinishingTools, NoFinishing, out reason, false);

    /// <summary>
    /// Cuts a pattern from a hide at cell (<paramref name="x"/>, <paramref name="y"/>), rotated by quarter turns, as
    /// cleanly as <paramref name="accuracy"/> (0-1, the cutting minigame). Returns the part's piece id.
    /// </summary>
    public string CutPattern(string id, string pattern, int x, int y, int rotation, double accuracy, out string reason)
    {
        reason = "";
        // Leather is cut at the leather bench; cloth there or at the tailor's table.
        CraftPiece src = Piece(id);
        CraftTools table = CraftTextiles.IsCloth(Rules, src) && Has(CraftTools.TailorTable) ? CraftTools.TailorTable : CraftTools.LeatherBench;
        if (!HideCheck(id, table, "There is no cutting table here.", out CraftPiece p, out reason, false, true))
            return null;
        CraftPattern pat = Rules.Pattern(pattern);
        if (pat == null)
        {
            Refuse("There is no such pattern.", out reason, id);
            return null;
        }
        if (!CraftLeather.Fits(p.hide, pat, x, y, rotation))
        {
            Refuse("The pattern does not fit there: it runs off the hide.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.CutPattern, piece = id, id = pat.id, i0 = x, i1 = y, i2 = rotation, a = (float)CraftMath.Clamp01(accuracy) };
        Record(op);
        return op.other;
    }

    private void ApplyCutPattern(CraftOperation op)
    {
        CraftPiece src = Piece(op.piece);
        CraftPattern pat = Rules.Pattern(op.id);
        if (src == null || pat == null)
            return;
        pat.Size(op.i2, out int w, out int h);
        CraftPiece part = Create(op, CraftPieceKind.Hide, $"{pat.Name} cut from a {Rules.Hide(src.hide.hide)?.Name ?? "hide"}");
        HideState from = src.hide;
        // Cut along a chalked line: cleaner. Any chalked pattern it cuts through is lost.
        double accuracy = op.a;
        int chalked = from.layout.FindIndex(l => l.pattern == pat.id && l.x == op.i0 && l.y == op.i1 && l.rotation == ((op.i2 % 4) + 4) % 4);
        if (chalked >= 0)
        {
            from.layout.RemoveAt(chalked);
            accuracy = Math.Min(1, accuracy + 0.05);
        }
        int hit = CraftTextiles.Overlaps(Rules, from, pat, op.i0, op.i1, op.i2);
        while (hit >= 0)
        {
            from.layout.RemoveAt(hit);
            hit = CraftTextiles.Overlaps(Rules, from, pat, op.i0, op.i1, op.i2);
        }
        HideState to = part.hide;
        to.hide = from.hide;
        to.seed = from.seed;
        to.agent = from.agent;
        to.shrinkTemperature = from.shrinkTemperature;
        to.hoursRaw = from.hoursRaw;
        to.fireResistance = from.fireResistance;
        to.dried = from.dried;
        to.pattern = pat.id;
        to.rotation = op.i2;
        to.cutQuality = accuracy;
        to.width = w;
        to.height = h;
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                HideCell c = from.Cell(op.i0 + px, op.i1 + py);
                bool covered = pat.Covers(px, py, op.i2) && c != null && c.present;
                HideCell copy = covered ? c.Clone() : new HideCell { present = false };
                if (covered)
                {
                    c.present = false;
                    // A careless cut leaves ragged edges.
                    bool border = !pat.Covers(px - 1, py, op.i2) || !pat.Covers(px + 1, py, op.i2) || !pat.Covers(px, py - 1, op.i2) || !pat.Covers(px, py + 1, op.i2);
                    if (border) copy.damage = Math.Min(1, copy.damage + (1 - accuracy) * 0.25);
                }
                to.cells.Add(copy);
            }
        foreach (CraftRecordEntry r in src.record)
            part.record.Add(new CraftRecordEntry { time = r.time, kind = r.kind, text = r.text, count = r.count, min = r.min, max = r.max });
        foreach (CraftDefect d in src.defects)
            if (d.at < 0) part.defects.Add(d.Clone());
        part.Record(Elapsed, op.kind, chalked >= 0 ? $"Cut out along the chalk as {CraftText.A(pat.Name)} {pat.Name.ToLowerInvariant()}" : $"Cut out as {CraftText.A(pat.Name)} {pat.Name.ToLowerInvariant()}", accuracy);
        bool any = false;
        foreach (HideCell c in from.cells) any |= c.present;
        if (!any)
            Destroy(src, "Nothing is left of the hide.");
        Raise(CraftEventKind.Cut, part.id, pat.id, "");
    }
}
