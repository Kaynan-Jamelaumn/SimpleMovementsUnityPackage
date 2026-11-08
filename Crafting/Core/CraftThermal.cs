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
    /// <summary>
    /// The heart of the forge, deep in the coals in front of the tuyere, runs hotter than the fire as a whole while the
    /// bellows blow: by this share of the fire's heat at full blast (nothing at the edge of the coals).
    /// </summary>
    public const double HeartBlast = 0.06;
    /// <summary>How much faster the face down in the coals heats than the face up, per second, while the piece lags far behind the fire.</summary>
    public const double FaceBiasRate = 0.03;
    /// <summary>Seconds for one face's lead in heat to even out through the bar.</summary>
    public const double FaceBiasSeconds = 20;
    /// <summary>Flux (0-1 of a full coat) a stretch loses each second at a red heat or more: a coat lasts some two and a half minutes in the fire.</summary>
    public const double FluxBurnRate = 0.004;
    /// <summary>Scale (mm) molten flux takes up each second at a full coat, from a bright red heat (borax melts near 740 °C).</summary>
    public const double FluxScaleRate = 0.02;
    /// <summary>Clay on the spine slows the body's quench by 1 + 3 × mm; the bare edge is not slowed by it (only through the steel).</summary>
    public const double ClayOnSpine = 3.0;

    // ------------------------------------------------------------------ fires and surroundings
    /// <summary>
    /// The draft through a fire: its own (<c>draftBase</c>) and what the bellows' air in it adds (<c>bellowsDraft</c> ×
    /// <see cref="CraftFireState.air"/>), all through its air vent (<see cref="CraftFireState.damper"/>: shut, no draft at
    /// all, the bellows' too) - what the fire burns by.
    /// </summary>
    public static double Draft(CraftSimulationSettings s, CraftFireState f) => Draft(s, f.air, f.damper);

    /// <summary><see cref="Draft"/> with the bellows' <paramref name="air"/> in the fire and its vent open <paramref name="damper"/> (0-1).</summary>
    public static double Draft(CraftSimulationSettings s, double air, double damper) => (s.draftBase + s.bellowsDraft * air) * damper;

    /// <summary>
    /// <see cref="Draft"/> as a share of the most there is (the vent open, the bellows' air full): what the fire gets, as the
    /// 3D bellows' meter shows it - its own draft alone with the vent open, more with every steady pump, none with the vent shut.
    /// </summary>
    public static double DraftShare(CraftSimulationSettings s, CraftFireState f) => DraftShare(s, f.air, f.damper);

    /// <summary><see cref="DraftShare"/> with the bellows' <paramref name="air"/> in the fire and its vent open <paramref name="damper"/> (0-1).</summary>
    public static double DraftShare(CraftSimulationSettings s, double air, double damper) =>
        CraftMath.Clamp01(Draft(s, air, damper) / Math.Max(1e-6, s.draftBase + s.bellowsDraft));

    public static void StepFire(CraftWorkshop ws, CraftFireState f, double dt, bool furnace)
    {
        CraftSimulationSettings s = ws.Rules.Settings;
        double fuelFactor = f.fuel / (f.fuel + s.fuelScale);
        double draft = Draft(s, f);
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
            v.stirred *= Math.Exp(-dt * ws.Rules.Settings.processHoursPerSecond / 3.0); // a stir settles in about three hours of vat time
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
        // steel in the fire: whether it passes the heat a magnet stops sticking to it at (told once, as it goes past)
        double hotBefore = metal && prof.Steel && prof.Magnetic && (p.location == CraftLocation.Fire || p.location == CraftLocation.Furnace) ? p.MaxTemperature : double.NaN;
        bool coloured = false;
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
                        // The clay is painted on the spine: it slows the body's quench; the edge is left bare and hardens.
                        CraftQuenchant m = rules.Quenchant(ws.Quench.medium);
                        k *= Severity(s, m, p, zone.temp) / (z == 0 ? 1 : 1 + clay * ClayOnSpine);
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
            {
                double film = seg.temperFilm;
                Surface(ws, p, prof, seg, i, oxygen, inFire, dt);
                // the first colour runs over bright steel as it warms (the look names it: CraftLook's temper colours)
                coloured |= prof.Steel && seg.temperFilm > film && TemperColourFrom > film && seg.temperFilm >= TemperColourFrom;
            }
            else
                Wood(ws, p, seg, inFire, dt);
        }
        Conduct(ws, p, dt);
        FaceBias(ws, p, dt);
        if (!double.IsNaN(hotBefore) && hotBefore <= prof.Curie && p.MaxTemperature > prof.Curie)
            ws.Raise(CraftEventKind.MagnetLost, p.id, "", ""); // shown only by a magnet (CraftLook.Magnet): the journal learns it where there is one
        if (coloured)
            ws.Raise(CraftEventKind.Note, p.id, "temper_colours", "Colours run over the bright steel as it warms: pale straw first.");
        if (metal && p.location == CraftLocation.Vat)
            Pickle(ws, p, dt);
        if (heatIntoQuench > 0)
            WarmQuench(ws, heatIntoQuench);
        if (!metal)
            return;
        CraftForging.KeepFlux(p);
        bool seamCooled = false;
        double warpBefore = Math.Abs(p.Tag(QuenchWarpTag));
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment seg = p.segments[i];
            Transform(ws, p, prof, seg, i, seg.edge, Math.Max(0.2, (seg.EdgeThickness + seg.thickness) * 0.5), true, dt);
            Transform(ws, p, prof, seg, i, seg.body, seg.thickness, false, dt);
            Overheat(ws, p, prof, seg, i, dt);
            // A forge weld's seam still open as the bar cools far below forging heat: the faces scale over inside, and it
            // will not take any more - layers that gape apart.
            if (seg.seamOpen > 0 && seg.Temperature < prof.HotWorkMin - 300)
            {
                p.AddDefect(CraftDefectKind.Delamination, i, seg.seamOpen * 1.5, seg.seamOpen > 0.6, "a forge weld not set while it was at welding heat");
                seg.seamOpen = 0;
                seamCooled = true;
            }
        }
        if (seamCooled)
        {
            ws.Raise(CraftEventKind.Note, p.id, "seam", "The seam cooled before it was set: those layers will not take now.");
            // never fluxed since it was folded or stacked: the faces scaled over (what the journal learns from it)
            if (!CraftForging.FluxedSinceSeam(p))
                ws.Raise(CraftEventKind.Note, p.id, "unfluxed", "");
        }
        // An uneven quench has bowed it as far as the eye sees (a bend of WarpShows degrees): told once an immersion.
        if (warpBefore < WarpShows && Math.Abs(p.Tag(QuenchWarpTag)) >= WarpShows)
            ws.Raise(CraftEventKind.Note, p.id, "warp", "It bows as it hardens in the tank: the quench was uneven.");
        // A fantastic quenchant lends its element to the steel it hardens (dragon's blood: fire; frostwater: ice).
        if (p.location == CraftLocation.Quench && prof.HardensByQuench && p.segments.Count > 0)
        {
            CraftQuenchant q = ws.Rules.Quenchant(ws.Quench.medium);
            if (q != null && q.affinity != ElementType.None && q.affinityStrength > 0)
            {
                // Only what hardened in it takes its element: the edge's martensite formed during this immersion (a cold,
                // already hardened blade dipped in it takes nothing).
                double mart = Math.Min(1, p.Tag(QuenchMartensiteTag));
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
                bool forge = p.location == CraftLocation.Fire;
                CraftFireState f = forge ? ws.Forge : ws.Furnace.fire;
                env = ws.Ambient + (f.temperature - ws.Ambient) * FireZone(s, f, p.depth, forge);
                transfer = s.fireTransfer * (0.4 + 0.6 * p.depth) * (0.25 + 0.75 * CraftMath.Clamp01(f.level * 2));
                oxygen = f.Oxidizing * (0.85 + 0.3 * p.depth);
                inFire = true;
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

    /// <summary>
    /// How hot a place in a fire is against the fire as a whole: the edge of the coals (<paramref name="depth"/> 0) at
    /// <see cref="CraftSimulationSettings.fireEdge"/>, the heart (1) at the fire's own heat - and in a forge, deep in front
    /// of the tuyere, up to <see cref="HeartBlast"/> more while the bellows blow.
    /// </summary>
    private static double FireZone(CraftSimulationSettings s, CraftFireState f, double depth, bool forge) =>
        CraftMath.Lerp(s.fireEdge, 1, depth) + (forge ? HeartBlast * CraftMath.Clamp01(f.air) * CraftMath.Clamp01(depth) * CraftMath.Clamp01(depth) : 0);

    /// <summary>
    /// The heat (°C) a piece lying <paramref name="depth"/> deep in the forge's coals (0 the edge - 1 the heart) drives
    /// toward now: the hottest it can get there with the fire as it is.
    /// </summary>
    public static double ForgeHeatAt(CraftWorkshop ws, double depth) =>
        ws.Ambient + (ws.Forge.temperature - ws.Ambient) * FireZone(ws.Rules.Settings, ws.Forge, depth, true);

    /// <summary>
    /// Not turning it in the fire: the face down in the coals runs ahead of the face up while the piece heats (the more,
    /// the further it lags behind the fire), and the lead evens out through the steel once it soaks or leaves the fire.
    /// <see cref="CraftPiece.faceBias"/> is face 1's lead over face 0's: turning it over only changes which face heats next.
    /// </summary>
    private static void FaceBias(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p.kind == CraftPieceKind.Wood)
            return;
        if (p.location == CraftLocation.Fire && ws.Forge.temperature > 500)
        {
            double lag = CraftMath.Clamp01((ws.Forge.temperature - p.MeanTemperature) / Math.Max(1, ws.Forge.temperature));
            p.faceBias = CraftMath.Clamp(p.faceBias + dt * FaceBiasRate * lag * (p.faceUp == 0 ? 1 : -1), -1, 1);
        }
        p.faceBias *= Math.Exp(-dt / FaceBiasSeconds);
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

    /// <summary>
    /// How hard a quenchant pulls heat where quench cracks start (under 300 °C, where the steel turns hard): its severity
    /// there (<see cref="CraftQuenchant.severity"/> × <see cref="CraftQuenchant.lowTemperatureFactor"/>) - water 1, brine
    /// 1.6, frostwater 0.5, quenching oil under 0.2.
    /// </summary>
    public static double CrackHarshness(CraftQuenchant q) => q == null ? 1 : q.severity * q.lowTemperatureFactor;

    /// <summary>
    /// A quenchant at least this harsh where cracks start (<see cref="CrackHarshness"/>: water, brine, frostwater) cracks
    /// a blade of hardening steel quenched bare - a thin edge almost always, a full one often - while oil hardens it whole:
    /// the quench window says so before it goes in.
    /// </summary>
    public const double HarshQuench = 0.4;

    /// <summary>
    /// The quenchants a station made from a preset keeps (the editor's Create menu: its Quenchants list): with a quench
    /// tank, water from the well, brine and quenching oil - water and brine crack a blade of hardening steel quenched bare
    /// (<see cref="HarshQuench"/>), oil hardens it whole; a test station every quenchant of the rules. None without a tank.
    /// </summary>
    public static List<string> PresetQuenchants(CraftRules rules, CraftTools tools, bool test)
    {
        var list = new List<string>();
        if ((tools & CraftTools.QuenchTank) == 0 || rules == null)
            return list;
        foreach (CraftQuenchant q in rules.Quenchants)
            if (q != null && (test || q.id == "water" || q.id == "brine" || q.id == "oil"))
                list.Add(q.id);
        return list;
    }

    /// <summary>
    /// Is <paramref name="p"/> a piece of hardening steel still to harden (or in the fire for it) - one that would crack
    /// quenched bare in <paramref name="tank"/>, a quenchant this harsh (<see cref="HarshQuench"/>)? The quench says so
    /// before it goes in (<see cref="CraftLook.HarshQuench"/>).
    /// </summary>
    public static bool HarshFor(CraftRules rules, CraftPiece p, CraftQuenchant tank)
    {
        if (rules == null || p == null || p.kind != CraftPieceKind.Metal || p.segments.Count == 0 || CrackHarshness(tank) < HarshQuench)
            return false;
        CraftAlloyProfile prof = p.Profile(rules);
        if (!prof.Steel || !prof.HardensByQuench)
            return false;
        double hard = 0;
        foreach (MetalSegment s in p.segments) hard += s.edge.mart;
        return p.location == CraftLocation.Fire || hard / p.segments.Count < 0.5;
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
        // The flux on this stretch: it burns away slowly at heat, and once molten it takes up the scale under it.
        if (seg.flux > 0 && t > 600)
        {
            if (t > 750 && seg.scale > 0)
                seg.scale -= Math.Min(seg.scale, dt * FluxScaleRate * Math.Min(1, seg.flux));
            seg.flux = Math.Max(0, seg.flux - dt * FluxBurnRate);
        }
        // Scale (parabolic: slower as it thickens); flux and a reducing fire keep it off.
        if (t > prof.OxidationOnset && oxygen > 0)
        {
            double rate = s.scaleRate * Math.Pow(2, (t - 1000) / s.scaleDoublingDegrees) * prof.OxidationRate * oxygen * (1 - 0.9 * CraftMath.Clamp01(seg.flux));
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
        // Temper colours on bare steel (freshly ground is bright enough): the film remembers the hottest it was since it
        // was last abraded - at its hottest zone, the thin edge first.
        double hottest = Math.Max(seg.edge.temp, seg.body.temp);
        if (seg.polish >= CraftSurface.FilmPolish - 1e-9 && hottest > seg.temperFilm)
            seg.temperFilm = hottest;
        // Oil and wax smoke off a hot surface, the faster the hotter (gone in moments at a red heat, under the scale).
        if (seg.coating > 0 && hottest > CraftSurface.CoatingBurns)
            seg.coating *= Math.Exp(-dt * (hottest - CraftSurface.CoatingBurns) / 100);
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

    /// <summary>
    /// A stretch of wood at its heat: it chars and dries. The char and the water belong to the whole piece, so each
    /// stretch adds its share of them (however finely the piece is cut into stretches); steaming wets it, it does not dry it.
    /// </summary>
    private static void Wood(CraftWorkshop ws, CraftPiece p, MetalSegment seg, bool inFire, double dt)
    {
        double t = seg.Temperature, share = 1.0 / Math.Max(1, p.segments.Count);
        if (t > 250)
        {
            p.wood.charred = Math.Min(1, p.wood.charred + dt * 0.01 * (t - 250) / 100 * share);
            seg.thickness = Math.Max(0.5, seg.thickness - dt * 0.002 * (t - 250) / 100);
        }
        if (p.wood.charred >= 1 && inFire)
            seg.length = 0; // burnt through
        if (p.location != CraftLocation.Steam)
            p.wood.moisture = Math.Max(0.02, p.wood.moisture - dt * 0.00001 * Math.Max(0, t - 20) * share);
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
                double reached = 1 - Math.Exp(-MartensiteCoefficient(prof) * (ms - t));
                if (reached > z.martReached)
                {
                    double share = (reached - z.martReached) / Math.Max(1e-9, 1 - z.martReached);
                    double formed = z.aust * share;
                    z.martReached = reached;
                    z.mart += formed;
                    z.aust -= formed;
                    z.temperP = z.mart > formed + 1e-9 ? z.temperP * (z.mart - formed) / z.mart : 0;
                    if (formed > 1e-6)
                    {
                        Quenched(ws, p, prof, seg, i, z, carbon, formed, coolRate, edgeZone);
                        if (edgeZone && p.location == CraftLocation.Quench)
                            p.SetTag(QuenchMartensiteTag, p.Tag(QuenchMartensiteTag) + formed / Math.Max(1, p.segments.Count));
                    }
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
        double uneven = 0, handled = 0;
        if (inQuench)
        {
            // How unevenly the hand quenched it (moved side to side, held still in its vapour, plunged flat), and how
            // unevenly the fire left it (one face hotter): both bend it - the hand's way toward the face up, the fire's
            // toward the face that ran cooler.
            CraftQuenchant m = ws.Rules.Quenchant(ws.Quench.medium);
            handled = (p.motion == CraftQuenchMotion.SideToSide ? 1 : p.motion == CraftQuenchMotion.Still ? (m != null ? m.vaporBlanket : 0.5) * 0.6 : 0.1)
                    + p.entryAngle;
            uneven = handled + Math.Abs(p.faceBias) * 0.8;
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
            // in the tank or out of it (an interrupted quench, an air-hardening steel), the quench's lesson
            ws.Raise(CraftEventKind.Cracked, p.id, CraftForging.CrackId + "hardening", inQuench ? "A sharp ping comes from the tank." : "Something pings as it cools.", (float)sev);
            if (d.severity > 1.5)
                d.surface = true;
        }
        // Warping: an uneven quench bends it; a single edge hardening ahead of the spine curves it toward the spine.
        // Degrees for the whole piece, shared by length.
        double share = seg.length / Math.Max(1, p.Length);
        double side = p.faceUp == 0 ? 1 : -1;
        double sway = inQuench ? handled * side + p.faceBias * 0.8 : 0;
        double swayed = s.quenchWarp * formed * sway * (edgeZone ? 0.5 : 1) * share;
        seg.bend += swayed;
        if (swayed != 0)
            p.SetTag(QuenchWarpTag, p.Tag(QuenchWarpTag) + swayed); // the bow this immersion's unevenness gave it (a single edge's own curve is no warp)
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
            // as burnt as the stretch is (told again every step it burns, never stacked on itself)
            if (!was && seg.body.burnt > 0.1)
            {
                p.HoldDefect(CraftDefectKind.Burnt, i, seg.body.burnt, true, "overheated until it burnt");
                ws.Raise(CraftEventKind.Burning, p.id, "", "It throws bright, crackling sparks: it is burning.");
            }
            else if (seg.body.burnt > 0.1)
                p.HoldDefect(CraftDefectKind.Burnt, i, seg.body.burnt, true, "overheated until it burnt");
        }
        if (t > prof.Solidus && p.location != CraftLocation.Furnace) // in the furnace it runs down into the pool (CraftSmelting.StepFurnace takes it whole)
        {
            double melt = dt * 4 * (1 + (t - prof.Solidus) / 30);
            bool first = seg.length > 0.5 && seg.length - melt <= 0.5 || i == p.segments.Count - 1 && seg.length > melt;
            seg.length = Math.Max(0, seg.length - melt);
            if (first && ws.Step % 10 == 0)
                ws.Raise(CraftEventKind.Melted, p.id, "", "It slumps and drips into the coals.");
        }
    }

    /// <summary>
    /// A piece tag: 1 while the piece went into the tank with austenite the quench turns to martensite (a hardening
    /// quench, <see cref="Hardening"/>), 0 when it went in without (a dip to cool it) - set as it goes in, kept after it
    /// comes out (saved with the piece).
    /// </summary>
    public const string QuenchHardeningTag = "quench.hardening";
    /// <summary>A piece tag: the edge's martensite formed in the tank since the piece last went in (its mean over the stretches).</summary>
    public const string QuenchMartensiteTag = "quench.martensite";
    /// <summary>A piece tag: the bend (degrees) an uneven quench gave the piece since it last went in - held still, moved side to side, one face hotter.</summary>
    public const string QuenchWarpTag = "quench.warp";
    /// <summary>A bow of this many degrees shows (the look's "slightly bent"): an uneven quench that bends a piece this far is told as warping.</summary>
    public const double WarpShows = 3;
    /// <summary>The heat bright steel takes its first temper colour at (pale straw: <see cref="CraftMetallurgy.TemperName"/>).</summary>
    public const double TemperColourFrom = 195;

    /// <summary>The share of a zone (0-1) that must turn to martensite in a quench for it to be a hardening one, not a dip to cool it.</summary>
    public const double HardeningShare = 0.02;

    /// <summary>How fast austenite turns to martensite below its start, per degree (Koistinen-Marburger): steel's, or another quench-hardening alloy's.</summary>
    private static double MartensiteCoefficient(CraftAlloyProfile prof) => prof.Steel ? 0.011 : 0.05;

    /// <summary>
    /// Has the piece austenite a quench in a tank at <paramref name="tank"/> °C would still turn to martensite (more than
    /// <see cref="HardeningShare"/> of a zone) - does going into it harden the piece? Each zone's austenite turns as
    /// <see cref="Transform"/> turns it (Koistinen-Marburger, down to the tank's heat), past what its last quench already
    /// reached: fresh austenite, from a hardening heat (or a quench cut short), turns; the few hundredths a hardened blade
    /// keeps (retained austenite, the martensite of its first quench already reached) do not, however hot the wheel got
    /// it - so dipping that blade to cool it is no quench, and teaches nothing about quenching.
    /// </summary>
    public static bool Hardening(CraftRules rules, CraftPiece p, double tank = 18)
    {
        if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count == 0)
            return false;
        CraftAlloyProfile prof = p.Profile(rules);
        if (!prof.HardensByQuench)
            return false;
        double coefficient = MartensiteCoefficient(prof);
        foreach (MetalSegment s in p.segments)
            for (int z = 0; z < 2; z++)
            {
                CraftZone zone = z == 0 ? s.edge : s.body;
                if (zone.aust <= 0.01)
                    continue;
                double thickness = z == 0 ? Math.Max(0.2, (s.EdgeThickness + s.thickness) * 0.5) : s.thickness;
                double ms = prof.Steel ? CraftMetallurgy.MartensiteStart(CraftMetallurgy.ZoneCarbon(prof, s, zone, thickness)) : prof.MartensiteStart;
                double reached = 1 - Math.Exp(-coefficient * Math.Max(0, ms - tank));
                double formed = zone.aust * Math.Max(0, reached - zone.martReached) / Math.Max(1e-9, 1 - zone.martReached);
                if (formed > HardeningShare)
                    return true;
            }
        return false;
    }

    /// <summary>A hot piece goes into the quench: steam and a hiss, or an oil flare.</summary>
    public static void OnQuenchEntry(CraftWorkshop ws, CraftPiece p)
    {
        // What this immersion is: a hardening quench or a dip to cool it, and none of its martensite formed yet.
        p.SetTag(QuenchHardeningTag, Hardening(ws.Rules, p, ws.Quench.temperature) ? 1 : 0);
        p.SetTag(QuenchMartensiteTag, 0);
        if (p.Tag(QuenchWarpTag) != 0)
            p.SetTag(QuenchWarpTag, 0);
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

    /// <summary>
    /// Empties the quench tank and fills it with <paramref name="quenchant"/> (a quenchant of the rules): fresh, at the
    /// room's heat. Not while a piece is in it. (A station also asks that it is one of its own quenchants and takes its item.)
    /// </summary>
    public bool FillTank(string quenchant, out string reason)
    {
        reason = "";
        CraftQuenchant q = Rules.Quenchant(quenchant);
        if (!Has(CraftTools.QuenchTank))
            return Refuse("There is no quench tank here.", out reason);
        if (q == null)
            return Refuse("That does not go in the quench tank.", out reason);
        foreach (CraftPiece p in Pieces)
            if (p.location == CraftLocation.Quench)
                return Refuse("Lift the piece out of the tank first.", out reason, p.id);
        Record(new CraftOperation { kind = CraftOpKind.FillTank, id = q.id, label = q.Name });
        return true;
    }

    private void ApplyFillTank(CraftOperation op)
    {
        if (Rules.Quenchant(op.id) == null)
            return;
        Quench.medium = op.id;
        Quench.temperature = Ambient;
        Raise(CraftEventKind.Note, "", op.id, $"The tank is filled with {Rules.Quenchant(op.id).Name.ToLowerInvariant()}.");
    }
}
