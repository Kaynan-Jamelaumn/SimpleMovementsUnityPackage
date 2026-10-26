using System;
using System.Collections.Generic;

/// <summary>
/// Heat in the workshop, step by step: the fires (fuel, flames, bellows air, the air vent), the oven, the ash box, the
/// quench tank and the vats; every metal and wooden piece segment by segment - its edge and body heating and cooling
/// at their own pace by where they are, heat creeping along the bar, scale growing and eating metal, carbon lost from
/// the surface or gained in a reducing fire, grain growing when held too hot, burning and melting - and the steel's
/// transformations as it heats and cools (austenite, pearlite, martensite, tempering), with the stress, cracks and
/// warping a quench can bring.
/// </summary>
public static class CraftThermal
{
    // ------------------------------------------------------------------ fires and surroundings
    public static void StepFire(CraftWorkshop ws, CraftFireState f, double dt, bool furnace)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        double fuelFactor = f.fuel / (f.fuel + s.fuelScale);
        double draft = (s.draftBase + s.bellowsDraft * f.air) * f.damper;
        double target = f.fuel > 1e-6 ? CraftMath.Clamp01(fuelFactor * draft * 1.25) : 0;
        double k = 1 - Math.Exp(-dt / (target > f.level ? s.fireRiseSeconds : s.fireFallSeconds));
        f.level += (target - f.level) * k;
        if (f.fuel > 0)
        {
            f.fuel = Math.Max(0, f.fuel - s.fuelBurnRate * f.burnRate * (0.15 + f.level) * dt);
            if (f.fuel <= 0 && f.lit)
            {
                f.lit = false;
                ws.Raise(CraftEventKind.FireOut, "", furnace ? "furnace" : "forge", furnace ? "The furnace has burnt down." : "The forge fire has burnt down to ashes.");
            }
        }
        f.air *= Math.Exp(-dt / s.bellowsDecaySeconds);
        double hot = ws.Ambient + (f.maxTemperature - ws.Ambient) * Math.Pow(CraftMath.Clamp01(f.level), 0.8);
        f.temperature += (hot - f.temperature) * (1 - Math.Exp(-dt / 3.0));
    }

    public static void StepEnvironment(CraftWorkshop ws, double dt)
    {
        CraftOvenState o = ws.Oven;
        double ovenTarget = o.setpoint > ws.Ambient ? o.setpoint : ws.Ambient;
        o.temperature += (ovenTarget - o.temperature) * (1 - Math.Exp(-dt / (o.setpoint > ws.Ambient ? 40.0 : 150.0)));
        ws.CoolAshes(dt);
        CraftQuenchState q = ws.Quench;
        q.temperature += (ws.Ambient - q.temperature) * (1 - Math.Exp(-dt / 900.0));
        foreach (CraftVatState v in ws.Vats)
        {
            // Vats run on process time (hours pass quickly), so they warm and cool on it too.
            double target = ws.Ambient + v.heat * (100 - ws.Ambient) * 0.95;
            v.temperature += (target - v.temperature) * (1 - Math.Exp(-dt * ws.Rules.Settings.processHoursPerSecond / 0.4));
            v.stirred *= Math.Exp(-dt / 30.0);
        }
    }

    // ------------------------------------------------------------------ pieces
    /// <summary>One step of a metal, wooden or fastener piece where it is.</summary>
    public static void StepPiece(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p.segments.Count == 0)
            return;
        CraftRules rules = ws.Rules;
        CraftSimulationSettings s = rules.Settings;
        // At rest: cold, in the air, nothing left to transform.
        if ((p.location == CraftLocation.Air || p.location == CraftLocation.Assembled) && Resting(ws, p))
            return;
        bool metal = p.kind != CraftPieceKind.Wood;
        CraftAlloyProfile prof = metal ? p.Profile(rules) : null;
        double heatIntoQuench = 0;
        int n = p.segments.Count;
        for (int i = 0; i < n; i++)
        {
            MetalSegment seg = p.segments[i];
            Environment(ws, p, i, out double env, out double transfer, out double oxygen, out bool inQuench, out bool inFire);
            double clay = seg.clay;
            for (int z = 0; z < 2; z++)
            {
                CraftZone zone = z == 0 ? seg.edge : seg.body;
                double exposure = z == 0 ? seg.EdgeExposure : seg.BodyExposure;
                zone.prevTemp = zone.temp;
                double k;
                if (transfer < 0)
                    k = AirCoefficient(s, zone.temp, env) * exposure;
                else
                {
                    k = transfer * exposure;
                    if (inQuench)
                    {
                        CraftQuenchant m = rules.Quenchant(ws.Quench.medium);
                        k *= Severity(s, m, p, zone.temp) / (1 + clay * (z == 0 ? 0.5 : 3.0));
                    }
                }
                double before = zone.temp;
                zone.temp += (env - zone.temp) * (1 - Math.Exp(-k * dt));
                if (inQuench && metal)
                    heatIntoQuench += (before - zone.temp) * p.SegmentMass(rules, i) * 0.5;
            }
            // The edge and the body even out.
            double mix = (1 - Math.Exp(-s.zoneConduction * dt)) * 0.5;
            double d = (seg.edge.temp - seg.body.temp) * mix;
            seg.edge.temp -= d;
            seg.body.temp += d;
            if (metal)
                Surface(ws, p, prof, seg, i, oxygen, inFire, dt);
            else
                Wood(ws, p, seg, inFire, dt);
        }
        Conduct(ws, p, dt);
        if (metal && p.location == CraftLocation.Vat)
            Pickle(ws, p, dt);
        if (heatIntoQuench > 0)
            WarmQuench(ws, heatIntoQuench);
        if (!metal)
            return;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment seg = p.segments[i];
            Transform(ws, p, prof, seg, i, seg.edge, Math.Max(0.2, (seg.EdgeThickness + seg.thickness) * 0.5), true, dt);
            Transform(ws, p, prof, seg, i, seg.body, seg.thickness, false, dt);
            Overheat(ws, p, prof, seg, i, dt);
        }
        // A fantastic quenchant lends its element to the steel it hardens (dragon's blood: fire; frostwater: ice).
        if (p.location == CraftLocation.Quench && prof.HardensByQuench && p.segments.Count > 0)
        {
            CraftQuenchant q = ws.Rules.Quenchant(ws.Quench.medium);
            if (q != null && q.affinity != ElementType.None && q.affinityStrength > 0)
            {
                double mart = 0;
                foreach (MetalSegment seg in p.segments) mart += seg.edge.mart;
                mart /= p.segments.Count;
                string tag = "affinity." + q.affinity;
                double took = q.affinityStrength * mart;
                if (took > p.Tag(tag) + 1e-6)
                    p.SetTag(tag, took);
            }
        }
        // Burnt and melted segments may have gone.
        for (int i = p.segments.Count - 1; i >= 0; i--)
            if (p.segments[i].length < 0.5)
                p.segments.RemoveAt(i);
        if (p.segments.Count == 0)
            ws.Destroy(p, "It melted away.");
    }

    private static bool Resting(CraftWorkshop ws, CraftPiece p)
    {
        foreach (MetalSegment seg in p.segments)
        {
            if (Math.Abs(seg.edge.temp - ws.Ambient) > 0.5 || Math.Abs(seg.body.temp - ws.Ambient) > 0.5)
                return false;
            if (seg.edge.aust > 1e-4 || seg.body.aust > 1e-4)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Where segment <paramref name="i"/> is: the temperature it drives toward, the transfer (negative = cooling in air),
    /// how much oxygen reaches its surface.
    /// </summary>
    private static void Environment(CraftWorkshop ws, CraftPiece p, int i, out double env, out double transfer, out double oxygen, out bool inQuench, out bool inFire)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        env = ws.Ambient;
        transfer = -1;
        oxygen = 0.7;
        inQuench = inFire = false;
        switch (p.location)
        {
            case CraftLocation.Fire:
            case CraftLocation.Furnace:
            {
                if (i < p.from || i > p.to)
                    return;
                CraftFireState f = p.location == CraftLocation.Fire ? ws.Forge : ws.Furnace.fire;
                double zone = CraftMath.Lerp(s.fireEdge, 1, p.depth);
                env = ws.Ambient + (f.temperature - ws.Ambient) * zone;
                transfer = s.fireTransfer * (0.4 + 0.6 * p.depth) * (0.25 + 0.75 * CraftMath.Clamp01(f.level * 2));
                oxygen = f.Oxidizing * (0.85 + 0.3 * p.depth);
                inFire = true;
                // Not turning it: one face sits in the heat.
                if (p.location == CraftLocation.Fire && f.temperature > 500)
                    p.faceBias = CraftMath.Clamp(p.faceBias + 0.0004 * (p.faceUp == 0 ? 1 : -1), -1, 1);
                return;
            }
            case CraftLocation.Melt:
                env = ws.Crucible.temperature;
                transfer = 0.08;
                oxygen = 0.05; // a lidded crucible keeps most of the air off
                return;
            case CraftLocation.Quench:
                if (p.PositionOf(i) < 1 - p.depth - 1e-6)
                    return; // above the medium
                env = ws.Quench.temperature;
                transfer = s.quenchTransfer;
                oxygen = 0;
                inQuench = true;
                return;
            case CraftLocation.Oven:
                env = ws.Oven.temperature;
                transfer = s.ovenTransfer;
                oxygen = 0.5;
                return;
            case CraftLocation.Ashes:
                env = ws.AshTemperature;
                transfer = s.ashTransfer * (2 - CraftMath.Clamp01(p.depth)); // well buried (1) it cools slowest; half covered faster
                oxygen = 0.15;
                return;
            case CraftLocation.Steam:
                env = 100;
                transfer = s.ovenTransfer * 1.5;
                oxygen = 0;
                return;
            case CraftLocation.Vat:
            {
                CraftVatState v = p.vat >= 0 && p.vat < ws.Vats.Count ? ws.Vats[p.vat] : null;
                env = v != null ? v.temperature : ws.Ambient;
                transfer = s.quenchTransfer * 0.8;
                oxygen = 0;
                return;
            }
        }
    }

    /// <summary>A metal in an acid vat: the pickle eats the scale, then etches the bright metal (a pattern-welded blade shows its layers).</summary>
    private static void Pickle(CraftWorkshop ws, CraftPiece p, double dt)
    {
        CraftVatState v = p.vat >= 0 && p.vat < ws.Vats.Count ? ws.Vats[p.vat] : null;
        CraftTanningAgent a = v != null ? ws.Rules.Agent(v.agent) : null;
        if (a == null || a.kind != CraftAgentKind.Acid)
            return;
        double hours = dt * ws.Rules.Settings.processHoursPerSecond;
        double eat = hours * 0.08 * v.strength / Math.Max(0.1, a.actionHours / 6);
        bool etching = false;
        foreach (MetalSegment seg in p.segments)
        {
            double take = Math.Min(seg.scale, eat);
            seg.scale -= take;
            seg.clay = 0;
            double left = eat - take;
            if (left > 0)
            {
                seg.thickness = Math.Max(0.1, seg.thickness - left * 0.02);
                seg.polish = Math.Min(seg.polish, 0.6);
                seg.rough = Math.Max(seg.rough, 2);
                etching = true;
            }
        }
        if (etching)
            p.SetTag("etched", Math.Min(1, p.Tag("etched") + eat * 2));
    }

    /// <summary>Cooling in air (convection + radiation, linearised for the exponential step).</summary>
    private static double AirCoefficient(CraftSimulationSettings s, double t, double ambient)
    {
        double dT = t - ambient;
        if (Math.Abs(dT) < 1e-6)
            return s.airConvection;
        double rad = (Math.Pow(t + 273.15, 4) - Math.Pow(ambient + 273.15, 4)) / 1e9;
        return Math.Max(0, s.airConvection + s.airRadiation * rad / dT);
    }

    /// <summary>How hard the quench pulls heat at <paramref name="t"/>: the vapour jacket when held still, gentler at the low end.</summary>
    private static double Severity(CraftSimulationSettings s, CraftQuenchant m, CraftPiece p, double t)
    {
        if (m == null)
            return 1;
        double sev = m.severity;
        if (p.motion == CraftQuenchMotion.Still)
        {
            if (t > 400) sev *= 1 - m.vaporBlanket;
        }
        else
            sev *= s.quenchMotionBoost;
        if (t < 300)
            sev *= m.lowTemperatureFactor;
        return sev;
    }

    private static void WarmQuench(CraftWorkshop ws, double joules)
    {
        CraftQuenchant m = ws.Rules.Quenchant(ws.Quench.medium);
        double capacity = Math.Max(1, ws.Quench.volume) * 4184 * (m != null ? m.heatCapacity : 1);
        double max = m != null ? m.boilingPoint : 100;
        ws.Quench.temperature = Math.Min(max, ws.Quench.temperature + joules / capacity);
    }

    /// <summary>Heat creeps along the bar between neighbouring segments.</summary>
    private static void Conduct(CraftWorkshop ws, CraftPiece p, double dt)
    {
        double a = ws.Rules.Settings.conduction;
        for (int i = 0; i + 1 < p.segments.Count; i++)
        {
            MetalSegment x = p.segments[i], y = p.segments[i + 1];
            double l = Math.Max(1, (x.length + y.length) * 0.5);
            double k = Math.Min(0.25, a / (l * l) * dt);
            double dBody = (x.body.temp - y.body.temp) * k, dEdge = (x.edge.temp - y.edge.temp) * k;
            x.body.temp -= dBody;
            y.body.temp += dBody;
            x.edge.temp -= dEdge;
            y.edge.temp += dEdge;
        }
    }

    // ------------------------------------------------------------------ the surface
    private static void Surface(CraftWorkshop ws, CraftPiece p, CraftAlloyProfile prof, MetalSegment seg, int i, double oxygen, bool inFire, double dt)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        double t = seg.Temperature;
        if (p.flux > 0 && t > 600)
            p.flux = Math.Max(0, p.flux - dt * 0.002);
        // Scale (parabolic: slower as it thickens); flux and a reducing fire keep it off.
        if (t > prof.OxidationOnset && oxygen > 0)
        {
            double rate = s.scaleRate * Math.Pow(2, (t - 1000) / s.scaleDoublingDegrees) * prof.OxidationRate * oxygen * (1 - 0.9 * CraftMath.Clamp01(p.flux));
            double grown = rate / (1 + seg.scale / 0.08) * dt;
            if (grown > 0)
            {
                seg.scale += grown;
                double loss = grown * s.scaleMetalLoss * 2;
                seg.thickness = Math.Max(0.1, seg.thickness - loss);
                seg.width = Math.Max(0.5, seg.width - loss * 0.3);
                seg.polish *= Math.Exp(-grown * 40);
                seg.rough = Math.Max(seg.rough, 8 + seg.scale * 120);
            }
        }
        // Temper colours on bright steel: the film remembers the hottest it was since it was polished.
        if (seg.polish > 0.4 && t > seg.temperFilm)
            seg.temperFilm = t;
        if (!prof.Steel)
            return;
        // Carbon: lost from the surface in oxygen, gained deep in a reducing charcoal fire.
        if (t > 700)
        {
            double heat = Math.Pow(2, (t - 1000) / 100);
            if (oxygen > 0.25)
            {
                double d = s.decarbRate * heat * oxygen * dt;
                seg.edge.decarb += (1 - seg.edge.decarb) * d * 1.2;
                seg.body.decarb += (1 - seg.body.decarb) * d * 0.6;
            }
            else if (inFire && t > 850)
            {
                CraftFireState f = p.location == CraftLocation.Furnace ? ws.Furnace.fire : ws.Forge;
                double gain = s.carburizeRate * heat * f.carburizing / Math.Max(0.5, seg.thickness) * dt;
                seg.carbonShift = Math.Min(0.012, seg.carbonShift + gain);
                seg.edge.decarb = Math.Max(0, seg.edge.decarb - gain * 300);
                seg.body.decarb = Math.Max(0, seg.body.decarb - gain * 300);
            }
        }
        // Sulfur from a coal fire goes into hot iron (once a second, so the composition does not churn).
        if (inFire && t > 900 && i == 0 && ws.Step % Math.Max(1, (int)Math.Round(1 / s.stepSeconds)) == 0)
        {
            CraftFireState f = p.location == CraftLocation.Furnace ? ws.Furnace.fire : ws.Forge;
            if (f.sulfur > 0.01)
            {
                double mass = p.Mass(ws.Rules);
                p.SetGrams("sulfur", p.Grams("sulfur") + f.sulfur * 2e-6 * mass * NormalizeComposition(ws.Rules, p));
            }
        }
    }

    /// <summary>Grams of composition per gram of the piece (compositions are relative).</summary>
    private static double NormalizeComposition(CraftRules rules, CraftPiece p)
    {
        double total = 0;
        foreach (CraftAmount a in p.composition) total += a.grams;
        double mass = p.Mass(rules);
        return mass > 1e-9 ? total / mass : 1;
    }

    private static void Wood(CraftWorkshop ws, CraftPiece p, MetalSegment seg, bool inFire, double dt)
    {
        double t = seg.Temperature;
        if (t > 250)
        {
            p.wood.charred = Math.Min(1, p.wood.charred + dt * 0.01 * (t - 250) / 100);
            seg.thickness = Math.Max(0.5, seg.thickness - dt * 0.002 * (t - 250) / 100);
        }
        if (p.wood.charred >= 1 && inFire)
            seg.length = 0; // burnt through
        p.wood.moisture = Math.Max(0.02, p.wood.moisture - dt * 0.00001 * Math.Max(0, t - 20));
    }

    // ------------------------------------------------------------------ the steel's transformations
    private static void Transform(CraftWorkshop ws, CraftPiece p, CraftAlloyProfile prof, MetalSegment seg, int i, CraftZone z, double thickness, bool edgeZone, double dt)
    {
        CraftRules rules = ws.Rules;
        CraftSimulationSettings s = rules.Settings;
        double t = z.temp;
        double coolRate = (z.prevTemp - t) / dt;

        // Cold work recrystallises (anneals) when hot enough; grain grows when held too hot.
        if (z.cold > 0 && t > prof.Recrystallization)
            z.cold *= Math.Exp(-dt * 0.2 * Math.Pow(2, (t - prof.Recrystallization) / 50));
        if (t > prof.GrainGrowth)
            z.grain = Math.Min(8, z.grain + s.grainGrowthRate * Math.Pow(2, (t - prof.GrainGrowth - 100) / 50) * dt);

        if (!prof.HardensByQuench)
            return;
        double carbon = prof.Steel ? CraftMetallurgy.ZoneCarbon(prof, seg, z, thickness) : 0;
        double critical, ms, a1;
        if (prof.Steel)
        {
            critical = carbon < s.eutectoidCarbon ? s.a3Iron - (s.a3Iron - s.a1) * carbon / s.eutectoidCarbon
                     : s.a1 + (carbon - s.eutectoidCarbon) / Math.Max(1e-6, s.castIronCarbon - s.eutectoidCarbon) * (1148 - s.a1);
            ms = CraftMetallurgy.MartensiteStart(carbon);
            a1 = s.a1;
        }
        else
        {
            critical = prof.CriticalTemperature;
            ms = prof.MartensiteStart;
            a1 = prof.Recrystallization;
        }

        if (t > a1 && t > z.prevTemp - 1e-9)
        {
            // Heating: it turns into austenite (fully above the critical temperature).
            double want = t >= critical ? 1 : CraftMath.Clamp01((t - a1) / Math.Max(1, critical - a1));
            if (want > z.aust)
            {
                double gain = Math.Min(want - z.aust, dt / s.austenitizeSeconds * (1 + Math.Max(0, t - critical) / 40));
                double from = z.mart + z.pearl;
                if (from > 1e-9)
                {
                    double km = z.mart / from;
                    z.mart = Math.Max(0, z.mart - gain * km);
                    z.pearl = Math.Max(0, z.pearl - gain * (1 - km));
                }
                bool completing = z.aust < 0.9 && z.aust + gain >= 0.9;
                z.aust = Math.Min(1, z.aust + gain);
                z.progress = 0;
                z.martReached = 0;
                if (z.mart < 1e-4)
                    z.temperP = 0;
                if (completing)
                    z.grain = 1 + (z.grain - 1) * 0.3; // fresh austenite: the coarse grain is mostly reset (normalising)
            }
            if (z.aust > 0.98)
                z.soak += dt;
        }
        else if (z.aust > 1e-6 && t < critical - 10)
        {
            if (t > ms)
            {
                // Cooling above Ms: the austenite turns to pearlite once it has had time to (soonest near 550 °C).
                double tau = prof.Steel ? CraftMetallurgy.PearliteIncubation(s, prof, carbon, t, z.grain) : 4.0 * prof.Hardenability;
                double before = 1 - Math.Exp(-z.progress * z.progress);
                z.progress += dt / Math.Max(1e-3, tau);
                double after = 1 - Math.Exp(-z.progress * z.progress);
                if (after > before && before < 0.999999)
                {
                    double share = (after - before) / (1 - before);
                    double formed = z.aust * share;
                    double fineNew = CraftMath.Clamp01((700 - t) / 180);
                    z.fine = z.pearl + formed > 1e-9 ? (z.fine * z.pearl + fineNew * formed) / (z.pearl + formed) : fineNew;
                    z.pearl += formed;
                    z.aust -= formed;
                    if (z.aust < 1e-6)
                    {
                        z.pearl += z.aust;
                        z.aust = 0;
                    }
                }
            }
            else
            {
                // Below Ms: martensite, the further below the more (Koistinen-Marburger).
                double coefficient = prof.Steel ? 0.011 : 0.05;
                double reached = 1 - Math.Exp(-coefficient * (ms - t));
                if (reached > z.martReached)
                {
                    double share = (reached - z.martReached) / Math.Max(1e-9, 1 - z.martReached);
                    double formed = z.aust * share;
                    z.martReached = reached;
                    z.mart += formed;
                    z.aust -= formed;
                    z.temperP = z.mart > formed + 1e-9 ? z.temperP * (z.mart - formed) / z.mart : 0;
                    if (formed > 1e-6)
                        Quenched(ws, p, prof, seg, i, z, carbon, formed, coolRate, edgeZone);
                }
                if (t < ms - 220 && z.aust > 0 && !prof.Steel)
                {
                    z.mart += z.aust;
                    z.aust = 0;
                }
            }
        }

        // Tempering: the martensite softens and toughens; stress is relieved; retained austenite transforms.
        if (t > 120 && t < a1)
        {
            if (z.mart > 1e-6)
            {
                double before = CraftMetallurgy.TemperFraction(z.temperP);
                z.temperP = CraftMetallurgy.Temper(z.temperP, t, dt, s.temperTimeScale);
                double after = CraftMetallurgy.TemperFraction(z.temperP);
                if (before < 0.05 && after >= 0.05 && edgeZone && i == p.segments.Count / 2)
                    ws.Raise(CraftEventKind.Tempered, p.id, "", "");
            }
            if (z.aust > 0 && t > 200 && t < ms + 100)
            {
                double conv = Math.Min(z.aust, dt * 0.01 * s.temperTimeScale / 30 * Math.Pow(2, (t - 200) / 50));
                z.aust -= conv;
                z.mart += conv;
            }
            if (z.stress > 0)
                z.stress *= Math.Exp(-dt * s.temperTimeScale * 0.0004 * Math.Pow(2, (t - 150) / 50));
        }
    }

    /// <summary>Martensite formed: residual stress (more for high carbon, fast cooling, abrupt sections), cracks, warping.</summary>
    private static void Quenched(CraftWorkshop ws, CraftPiece p, CraftAlloyProfile prof, MetalSegment seg, int i, CraftZone z, double carbon, double formed,
        double coolRate, bool edgeZone)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        bool inQuench = p.location == CraftLocation.Quench;
        double rateFactor = CraftMath.Clamp(coolRate / 120, 0.08, 1.5);
        double geom = 1;
        int n = p.segments.Count;
        if (i > 0) geom += Math.Abs(seg.thickness - p.segments[i - 1].thickness) / Math.Max(0.5, seg.thickness) * 0.5;
        if (i + 1 < n) geom += Math.Abs(seg.thickness - p.segments[i + 1].thickness) / Math.Max(0.5, seg.thickness) * 0.5;
        if (seg.hole > 0) geom += 0.5;
        if (edgeZone && seg.Beveled) geom += 0.3;
        double uneven = 0;
        if (inQuench)
        {
            CraftQuenchant m = ws.Rules.Quenchant(ws.Quench.medium);
            uneven = (p.motion == CraftQuenchMotion.SideToSide ? 1 : p.motion == CraftQuenchMotion.Still ? (m != null ? m.vaporBlanket : 0.5) * 0.6 : 0.1)
                   + p.entryAngle + Math.Abs(p.faceBias) * 0.8;
            geom += 0.3 * uneven;
        }
        double carbonFactor = prof.Steel ? 0.3 + 60 * carbon : 0.25;
        z.stress += formed * s.quenchStress * carbonFactor * rateFactor * geom;
        // Every segment has its own small flaws: the weakest cracks first, and the crack relieves the rest.
        double flaw = 0.85 + 0.3 * CraftMath.Noise(CraftMath.Hash(p.id), i, edgeZone ? 7 : 8);
        double threshold = s.crackStress * flaw / (1 + 0.3 * Math.Max(0, z.grain - 1)) * (1 - 0.5 * CraftMath.Clamp01(z.burnt));
        if (seg.slag > 0.02 * p.SegmentMass(ws.Rules, i)) threshold *= 0.8;
        if (z.stress > threshold)
        {
            double sev = z.stress - threshold + 0.2;
            CraftDefect d = p.AddDefect(CraftDefectKind.Crack, i, sev, sev > 0.35, inQuench ? "cracked in the quench" : "cracked as it hardened");
            z.stress -= 0.5;
            // A crack relieves its neighbours.
            if (i > 0) { p.segments[i - 1].edge.stress *= 0.6; p.segments[i - 1].body.stress *= 0.6; }
            if (i + 1 < n) { p.segments[i + 1].edge.stress *= 0.6; p.segments[i + 1].body.stress *= 0.6; }
            ws.Raise(CraftEventKind.Cracked, p.id, "crack", inQuench ? "A sharp ping comes from the tank." : "Something pings as it cools.", (float)sev);
            if (d.severity > 1.5)
                d.surface = true;
        }
        // Warping: an uneven quench bends it; a single edge hardening ahead of the spine curves it toward the spine.
        // Degrees for the whole piece, shared by length.
        double share = seg.length / Math.Max(1, p.Length);
        double side = p.faceUp == 0 ? 1 : -1;
        seg.bend += s.quenchWarp * formed * uneven * side * (edgeZone ? 0.5 : 1) * share;
        if (p.edges == 1 && edgeZone)
            seg.bend += s.quenchWarp * formed * 0.35 * share;
        else if (p.edges == 1 && !edgeZone)
            seg.bend -= s.quenchWarp * formed * 0.2 * share;
    }

    /// <summary>Burning above the burn temperature, melting above the solidus.</summary>
    private static void Overheat(CraftWorkshop ws, CraftPiece p, CraftAlloyProfile prof, MetalSegment seg, int i, double dt)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        if (p.location == CraftLocation.Melt)
            return; // in the crucible it melts into the melt (the crucible takes it), it does not burn or drip away
        double t = Math.Max(seg.edge.temp, seg.body.temp);
        // Burning needs the fire's heat and air on the metal: a casting cooling in its mould or a bar cooling in the air does not burn.
        bool heating = p.location == CraftLocation.Fire || p.location == CraftLocation.Furnace || t > Math.Max(seg.edge.prevTemp, seg.body.prevTemp) + 1e-6;
        if (t > prof.BurnTemperature && heating)
        {
            double b = s.burnRate * Math.Pow(2, (t - prof.BurnTemperature - 50) / 50) * dt;
            bool was = seg.body.burnt > 0.1;
            seg.edge.burnt = Math.Min(1, seg.edge.burnt + b * 1.3);
            seg.body.burnt = Math.Min(1, seg.body.burnt + b);
            if (!was && seg.body.burnt > 0.1)
            {
                p.AddDefect(CraftDefectKind.Burnt, i, seg.body.burnt, true, "overheated until it burnt");
                ws.Raise(CraftEventKind.Burning, p.id, "", "It throws bright, crackling sparks: it is burning.");
            }
            else if (seg.body.burnt > 0.1)
                p.AddDefect(CraftDefectKind.Burnt, i, seg.body.burnt, true, "overheated until it burnt");
        }
        if (t > prof.Solidus)
        {
            double melt = dt * 4 * (1 + (t - prof.Solidus) / 30);
            bool first = seg.length > 0.5 && seg.length - melt <= 0.5 || i == p.segments.Count - 1 && seg.length > melt;
            seg.length = Math.Max(0, seg.length - melt);
            if (first && ws.Step % 10 == 0)
                ws.Raise(CraftEventKind.Melted, p.id, "", "It slumps and drips into the coals.");
        }
    }

    /// <summary>A hot piece goes into the quench: steam and a hiss, or an oil flare.</summary>
    public static void OnQuenchEntry(CraftWorkshop ws, CraftPiece p)
    {
        CraftQuenchant m = ws.Rules.Quenchant(ws.Quench.medium);
        double t = p.MaxTemperature;
        if (m == null || t < 150)
            return;
        if (m.flashPoint > 0 && (t > m.flashPoint + 450 && p.depth < 0.9 || ws.Quench.temperature > m.flashPoint))
        {
            ws.Raise(CraftEventKind.QuenchFlare, p.id, m.id, "The oil flares up in flames around it.");
            foreach (MetalSegment s in p.segments) s.scale += 0.01;
        }
        else
            ws.Raise(CraftEventKind.Quenched, p.id, m.id, m.observation);
    }
}

public sealed partial class CraftWorkshop
{
    internal void CoolAshes(double dt) => AshTemperature += (Ambient - AshTemperature) * (1 - Math.Exp(-dt / 1200.0));

    // ------------------------------------------------------------------ the quench
    /// <summary>
    /// How a piece in the quench tank is moved: up and down (the right way for a blade), side to side (it warps), swirled,
    /// or held still (a vapour jacket clings to it).
    /// </summary>
    public bool Agitate(string id, CraftQuenchMotion motion, out string reason, double depth = -1)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null || p.location != CraftLocation.Quench)
            return Refuse("It is not in the quench tank.", out reason, id);
        if (p.motion == motion && (depth < 0 || Math.Abs(depth - p.depth) < 0.01))
            return true;
        Record(new CraftOperation { kind = CraftOpKind.Agitate, piece = id, i0 = (int)motion, a = (float)depth });
        return true;
    }

    private void ApplyAgitate(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        p.motion = (CraftQuenchMotion)op.i0;
        if (op.a >= 0)
            p.depth = CraftMath.Clamp01(op.a);
    }
}
