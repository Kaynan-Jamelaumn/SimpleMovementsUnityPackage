using System;
using System.Collections.Generic;

/// <summary>
/// What a preparation remembers between steps beyond its physical state: each reaction's current rate and how much of
/// it happened so far, what is running and present (for the log), boiling, the random stream and vented vapour.
/// Saved with the preparation so a restored one continues exactly.
/// </summary>
public sealed class AlchemyTracker
{
    public readonly double[] ReactionRates;
    public readonly double[] ReactionTotals;
    public readonly bool[] ReactionActive;
    public readonly double[] EruptionCooldown;
    public readonly bool[] Present;
    /// <summary>Is the substance coming out of solution now (for the log)?</summary>
    public readonly bool[] Precipitating;
    /// <summary>Is the substance crystallising now (for the log)?</summary>
    public readonly bool[] Crystallizing;
    public readonly double[] Vented;
    public bool Boiling;
    public double BoilRate;
    /// <summary>A manual fire is burning (for the "the fire went out" event).</summary>
    public bool FireLit;
    /// <summary>Seconds before another "it slops over" event.</summary>
    public double SplashCooldown;
    public ulong Random;

    // Scratch buffers (one step at a time).
    internal readonly double[] extent;
    internal readonly double[] demand;
    internal readonly double[] delta;
    internal readonly double[] pools;
    // Each store's amounts at the start of the step: only traces that are not growing are cleaned.
    internal readonly double[] wasAmount, wasUndissolved, wasVapor, wasDistillate;
    // Reaction context of the step: contamination, mixing by stirring, the layers.
    internal readonly double[] oilShare;
    internal bool layered;
    internal double layerAqueous, layerOily, contamination = 1, mixing = 1;

    public AlchemyTracker(AlchemyRules rules, int seed)
    {
        int r = rules.Reactions.Length, n = rules.SubstanceCount;
        ReactionRates = new double[r];
        ReactionTotals = new double[r];
        ReactionActive = new bool[r];
        EruptionCooldown = new double[r];
        Present = new bool[n];
        Precipitating = new bool[n];
        Crystallizing = new bool[n];
        Vented = new double[n];
        oilShare = new double[n];
        extent = new double[r];
        demand = new double[n];
        delta = new double[n];
        wasAmount = new double[n];
        wasUndissolved = new double[n];
        wasVapor = new double[n];
        wasDistillate = new double[n];
        int maxTerms = 1;
        foreach (CompiledReaction c in rules.Reactions)
            maxTerms = Math.Max(maxTerms, c.ReactantMatches.Length);
        pools = new double[maxTerms];
        Random = SeedState(seed);
    }

    public static ulong SeedState(int seed)
    {
        unchecked
        {
            ulong s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL;
            return s == 0 ? 0x9E3779B97F4A7C15UL : s;
        }
    }

    /// <summary>The next number of the preparation's random stream, 0-1 (xorshift64*: the same seed gives the same stream on every machine).</summary>
    public double NextRandom()
    {
        unchecked
        {
            ulong x = Random;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            Random = x;
            return ((x * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}

/// <summary>
/// The rules of change: moves a <see cref="AlchemyMixture"/> forward by one fixed step. Nothing here depends on frame
/// rate, rendering or the UI; a step is a pure function of the state, the rules and the random stream, so preparations
/// can be simulated continuously, replayed, tested and run on a server.
/// <para>Order of a step: a manual fire burns its fuel → ingredients release their substances (each at its own rate,
/// some faster at their pH) → reactions (breakdown, clashes, stirring that brings reactants together, layers that keep
/// them apart, contaminants that get in the way) → heat (fire, baths, cooling, surroundings, reaction heat) → boiling and
/// evaporation (vent, distil - a column sends the less volatile part back - or keep as vapour; both take heat) →
/// condensation and pressure (sealed vessels) → solubility and crystals (what the liquid cannot hold comes out as crystals
/// or powder, what it can hold again dissolves) → stirring (agitation follows the stirring speed, slops over, beats in
/// air, emulsifies layers) → temperature history (cooling rate, swing) → traces are cleaned.</para>
/// </summary>
public static class AlchemySimulator
{
    public static void Step(AlchemyMixture m, AlchemyTracker t, List<AlchemyEvent> events)
    {
        AlchemyRules rules = m.Rules;
        AlchemySimulationSettings s = rules.Settings;
        double dt = s.stepSeconds;
        Array.Copy(m.amounts, t.wasAmount, m.amounts.Length);
        Array.Copy(m.undissolved, t.wasUndissolved, m.undissolved.Length);
        Array.Copy(m.vapor, t.wasVapor, m.vapor.Length);
        Array.Copy(m.distillate, t.wasDistillate, m.distillate.Length);
        double before = m.Temperature;

        Fire(m, t, s, dt, events);
        Dissolve(m, s, dt, events);
        double reactionHeat = React(m, t, s, dt, events);
        Heat(m, s, dt, reactionHeat);
        Boil(m, t, s, dt, events);
        Condense(m, s, dt);
        CheckPressure(m, t, s, events);
        Solubility(m, t, s, dt, events);
        Stirring(m, t, s, dt, events);
        Track(m, dt, before);
        Clean(m, t, s, events);
        m.Elapsed += dt;
        m.Step++;
    }

    // ------------------------------------------------------------------ extraction
    /// <summary>
    /// Every ingredient gives up each of its substances at that substance's rate (form × extractability), × 2 per
    /// Dissolve Doubling Degrees above 20 °C (¼ … 8×), × (1 + boost × agitation), much slower without liquid. Released
    /// matter enters the liquid; what the liquid cannot hold is sorted out by <see cref="Solubility"/>.
    /// </summary>
    private static void Dissolve(AlchemyMixture m, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        if (m.pending.Count == 0)
            return;
        double liquid = m.LiquidVolume;
        double heat = Clamp(Math.Pow(2.0, (m.Temperature - 20.0) / Math.Max(1.0, s.dissolveDoublingDegrees)), 0.25, 8.0);
        double stir = 1.0 + s.agitationDissolveBoost * m.Agitation;
        double wet = liquid < 0.5 ? s.dryDissolveFactor : 1.0;
        double ph = double.NaN;
        AlchemySubstance[] subs = m.Rules.Substances;
        for (int i = 0; i < m.pending.Count; i++)
        {
            PendingIngredient p = m.pending[i];
            for (int k = 0; k < p.Substances.Length; k++)
            {
                double left = p.Totals[k] - p.Released[k];
                if (left <= 0) continue;
                double acidity = 1.0;
                AlchemySubstance sub = subs[p.Substances[k]];
                if (sub.phSensitivity > 0f)
                {
                    // Closer to its best pH than neutral is: faster (an alkaloid comes out of berries in acid).
                    if (double.IsNaN(ph)) ph = m.Ph;
                    double best = sub.extractionPh;
                    acidity = Clamp(Math.Pow(2.0, sub.phSensitivity * (Math.Abs(7.0 - best) - Math.Abs(ph - best)) / 2.0), 0.1, 8.0);
                }
                double share = p.Rates[k] >= PendingIngredient.Instant * 0.5 ? 1.0 : p.Rates[k] * dt * heat * stir * wet * acidity;
                double x = Math.Min(left, p.Totals[k] * share);
                m.amounts[p.Substances[k]] += x;
                p.Released[k] += x;
            }
            if (p.Done)
            {
                m.pending.RemoveAt(i--);
                events?.Add(new AlchemyEvent(AlchemyEventKind.IngredientDissolved, m.Elapsed + dt, p.Id, p.Name));
            }
        }
    }

    // ------------------------------------------------------------------ solubility
    /// <summary>
    /// Substances with a solubility limit: what the liquid holds beyond its capacity comes out of solution (Precipitation
    /// Rate per second) and settles undissolved; undissolved matter goes back in while there is room (Redissolve Rate,
    /// faster when stirred). Undissolved matter is not bottled and takes no part in reactions.
    /// </summary>
    private static void Solubility(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        AlchemyRules rules = m.Rules;
        bool[] limited = rules.LimitedSolubility;
        double outShare = Math.Min(1.0, s.precipitationRate * dt);
        double inShare = Math.Min(1.0, s.redissolveRate * dt * (1.0 + 2.0 * m.Agitation));
        double trace = s.traceAmount;
        for (int i = 0; i < limited.Length; i++)
        {
            if (!limited[i])
                continue;
            double cap = m.Capacity(i);
            double dissolved = m.amounts[i];
            int crystal = rules.CrystalOf[i];
            if (dissolved > cap && crystal >= 0)
            {
                // Crystals grow from a saturated liquid left still and cooled slowly - at once on a seed crystal, by
                // themselves past the nucleation point; below it the liquid stays supersaturated. Stirred or crash-cooled,
                // it comes out as a fine powder instead (below).
                AlchemySubstance sub = rules.Substances[i];
                if (m.SolventVolume <= 1e-6)
                {
                    // No liquid at all: what is left dries into crystals where it is.
                    double dry = dissolved * outShare;
                    m.amounts[i] -= dry;
                    m.amounts[crystal] += dry;
                    continue;
                }
                bool calm = m.Agitation <= sub.crystalStillness && m.CoolingRate <= sub.maxCoolingRate;
                if (calm)
                {
                    t.Precipitating[i] = false;
                    if (m.amounts[crystal] > trace || dissolved >= cap * sub.nucleation)
                    {
                        double x = (dissolved - cap) * Math.Min(1.0, sub.crystalGrowth * dt);
                        m.amounts[i] -= x;
                        m.amounts[crystal] += x;
                        if (!t.Crystallizing[i] && x / dt >= s.activeRateThreshold * 0.5)
                        {
                            t.Crystallizing[i] = true;
                            events?.Add(new AlchemyEvent(AlchemyEventKind.CrystalsForming, m.Elapsed + dt, sub.id,
                                rules.Substances[crystal].observation, (float)m.amounts[crystal]));
                        }
                    }
                    else
                        t.Crystallizing[i] = false;
                    continue;
                }
                t.Crystallizing[i] = false;
            }
            if (dissolved > cap)
            {
                double x = (dissolved - cap) * outShare;
                m.amounts[i] -= x;
                m.undissolved[i] += x;
                if (!t.Precipitating[i] && x / dt >= s.activeRateThreshold)
                {
                    t.Precipitating[i] = true;
                    events?.Add(new AlchemyEvent(AlchemyEventKind.Precipitated, m.Elapsed + dt, rules.Substances[i].id,
                        rules.Substances[i].observation, (float)m.undissolved[i]));
                }
            }
            else
            {
                t.Precipitating[i] = false;
                t.Crystallizing[i] = false;
                if (m.undissolved[i] > 0)
                {
                    double x = Math.Min(m.undissolved[i], cap - dissolved) * inShare;
                    if (x > 0)
                    {
                        m.undissolved[i] -= x;
                        m.amounts[i] += x;
                    }
                }
                // Crystals dissolve again where there is room (slower than a powder).
                if (crystal >= 0 && m.amounts[crystal] > 0)
                {
                    double room = cap - m.amounts[i];
                    double x = Math.Min(m.amounts[crystal], room) * inShare * s.crystalRedissolveFactor;
                    if (x > 0)
                    {
                        m.amounts[crystal] -= x;
                        m.amounts[i] += x;
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ reactions
    private static double React(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        AlchemyRules rules = m.Rules;
        CompiledReaction[] reactions = rules.Reactions;
        double[] a = m.amounts;
        double liquid = m.LiquidVolume;
        double basis = liquid > 1e-9 ? liquid : m.Volume;
        int n = a.Length;
        Array.Clear(t.demand, 0, n);
        Array.Clear(t.delta, 0, n);
        Context(m, t, s);

        // Rates from the state at the start of the step (every reaction sees the same mixture).
        bool any = false;
        for (int j = 0; j < reactions.Length; j++)
        {
            double r = basis > 1e-9 ? Rate(m, t, reactions[j], basis) : 0;
            t.extent[j] = r * dt;
            any |= r > 0;
        }

        double heat = 0;
        if (any)
        {
            // What every reaction wants of each substance (shared reactants are split by availability).
            for (int j = 0; j < reactions.Length; j++)
            {
                double e = t.extent[j];
                if (e <= 0) continue;
                CompiledReaction c = reactions[j];
                for (int k = 0; k < c.ReactantMatches.Length; k++)
                {
                    int[] match = c.ReactantMatches[k];
                    double pool = Pool(a, match);
                    if (pool <= 0) continue;
                    for (int q = 0; q < match.Length; q++)
                        t.demand[match[q]] += e * c.ReactantRatios[k] * a[match[q]] / pool;
                }
            }
            // Never take more than there is: a reaction short of a reactant runs only as far as it can.
            for (int j = 0; j < reactions.Length; j++)
            {
                double e = t.extent[j];
                if (e <= 0) continue;
                CompiledReaction c = reactions[j];
                double scale = 1;
                for (int k = 0; k < c.ReactantMatches.Length; k++)
                    foreach (int i in c.ReactantMatches[k])
                        if (a[i] > 0 && t.demand[i] > a[i])
                            scale = Math.Min(scale, a[i] / t.demand[i]);
                t.extent[j] = e * scale;
            }
            for (int j = 0; j < reactions.Length; j++)
            {
                double e = t.extent[j];
                if (e <= 0) continue;
                CompiledReaction c = reactions[j];
                for (int k = 0; k < c.ReactantMatches.Length; k++)
                {
                    int[] match = c.ReactantMatches[k];
                    double pool = Pool(a, match);
                    if (pool <= 0) continue;
                    for (int q = 0; q < match.Length; q++)
                        t.delta[match[q]] -= e * c.ReactantRatios[k] * a[match[q]] / pool;
                }
                for (int k = 0; k < c.Products.Length; k++)
                    t.delta[c.Products[k]] += e * c.ProductRatios[k];
                heat += e * c.Definition.heatPerUnit;
            }
            for (int i = 0; i < n; i++)
            {
                double v = a[i] + t.delta[i];
                a[i] = v > 0 ? v : 0;
            }
        }

        // Rates, totals, start / stop and eruptions.
        for (int j = 0; j < reactions.Length; j++)
        {
            double rate = t.extent[j] / dt;
            t.ReactionRates[j] = rate;
            t.ReactionTotals[j] += t.extent[j];
            AlchemyReaction def = reactions[j].Definition;
            if (!t.ReactionActive[j] && rate >= s.activeRateThreshold)
            {
                t.ReactionActive[j] = true;
                events?.Add(new AlchemyEvent(AlchemyEventKind.ReactionStarted, m.Elapsed + dt, def.id, def.observation, (float)rate));
            }
            else if (t.ReactionActive[j] && rate < s.activeRateThreshold * 0.5)
            {
                t.ReactionActive[j] = false;
                events?.Add(new AlchemyEvent(AlchemyEventKind.ReactionStopped, m.Elapsed + dt, def.id, def.observation));
            }
            if (t.EruptionCooldown[j] > 0)
                t.EruptionCooldown[j] -= dt;
            if (def.eruptionRate > 0f && rate >= def.eruptionRate && t.EruptionCooldown[j] <= 0)
            {
                t.EruptionCooldown[j] = 3.0;
                Spill(m, def.spillFraction);
                events?.Add(new AlchemyEvent(AlchemyEventKind.Eruption, m.Elapsed + dt, def.id, def.observation, def.spillFraction));
            }
        }
        return heat;
    }

    /// <summary>Current rate (units of reaction per second) of <paramref name="c"/>.</summary>
    public static double Rate(AlchemyMixture m, AlchemyTracker t, CompiledReaction c, double basis)
    {
        AlchemyReaction def = c.Definition;
        if (!c.Valid || def.rate <= 0f)
            return 0;
        double tf = def.TemperatureFactor(m.Temperature);
        if (tf <= 0)
            return 0;
        double[] a = m.amounts;
        double trace = m.Rules.Settings.traceAmount;
        double r = def.rate * tf;
        for (int k = 0; k < c.ReactantMatches.Length; k++)
        {
            double pool = Pool(a, c.ReactantMatches[k]);
            if (pool <= trace)
                return 0;
            double order = c.ReactantOrders[k];
            if (order > 0)
                r *= order == 1.0 ? pool / basis : Math.Pow(pool / basis, order);
        }
        r *= basis;
        if (!AlchemyCondition.AllMet(def.conditions, m))
            return 0;
        for (int k = 0; k < c.ModifierMatches.Length; k++)
        {
            ReactionModifier mod = def.modifiers[k];
            if (mod == null) continue;
            r *= mod.Factor(Pool(a, c.ModifierMatches[k]) / basis);
        }
        if (def.conditionalRates != null)
            foreach (ConditionalRate cr in def.conditionalRates)
                if (cr?.condition != null && cr.condition.IsMet(m))
                    r *= cr.multiplier;
        if (t != null)
        {
            if (c.ReactantMatches.Length >= 2)
            {
                r *= t.mixing;
                if (t.layered)
                    r *= Contact(m, t, c);
            }
            if (!def.IsGenerated && !c.ConsumesContaminant)
                r *= t.contamination;
        }
        if (def.rateVariance > 0f && t != null && r > 0)
            r *= 1.0 + def.rateVariance * (t.NextRandom() * 2.0 - 1.0);
        return r > 0 ? r : 0;
    }

    /// <summary>What the reactions of this step share: contamination, mixing by stirring, the layers.</summary>
    private static void Context(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s)
    {
        double dirt = Math.Max(0.0, m.ContaminantLevel());
        t.contamination = 1.0 / (1.0 + s.contaminantInterference * dirt / 0.05);
        t.mixing = 1.0 + s.mixingBoost * m.Agitation;
        t.layered = m.Layers(out t.layerAqueous, out t.layerOily);
        if (!t.layered)
            return;
        double[] a = m.amounts;
        for (int i = 0; i < a.Length; i++)
            t.oilShare[i] = a[i] > 0 ? m.OilShare(i, t.layerAqueous, t.layerOily) : 0;
    }

    /// <summary>
    /// How well a reaction's reactants meet in a layered liquid, relative to one mixed liquid: 0 when they sit in
    /// different layers, above 1 when crowded into the same small layer. Σ over layers of share × Π (reactant's share in
    /// it ÷ layer's share) ^ order; an emulsion (stirring, egg yolk) brings it back toward 1.
    /// </summary>
    private static double Contact(AlchemyMixture m, AlchemyTracker t, CompiledReaction c)
    {
        double total = t.layerAqueous + t.layerOily;
        if (total <= 1e-9)
            return 1;
        double wo = t.layerOily / total, ww = 1 - wo;
        double po = wo, pw = ww;
        double[] a = m.amounts;
        for (int k = 0; k < c.ReactantMatches.Length; k++)
        {
            double order = c.ReactantOrders[k];
            if (order <= 0) continue;
            double pool = 0, oil = 0;
            foreach (int i in c.ReactantMatches[k])
            {
                pool += a[i];
                oil += a[i] * t.oilShare[i];
            }
            double so = pool > 1e-12 ? oil / pool : wo;
            po *= Math.Pow(so / Math.Max(1e-6, wo), order);
            pw *= Math.Pow((1 - so) / Math.Max(1e-6, ww), order);
        }
        double contact = Math.Min(4.0, po + pw);
        return contact + (1.0 - contact) * Clamp(m.Emulsion, 0, 1);
    }

    private static double Pool(double[] a, int[] match)
    {
        double p = 0;
        for (int q = 0; q < match.Length; q++) p += a[match[q]];
        return p;
    }

    /// <summary>Loses <paramref name="fraction"/> of everything in the vessel (eruptions, bursts).</summary>
    internal static void Spill(AlchemyMixture m, double fraction)
    {
        fraction = Clamp(fraction, 0, 1);
        double keep = 1 - fraction;
        for (int i = 0; i < m.amounts.Length; i++)
        {
            m.amounts[i] *= keep;
            m.undissolved[i] *= keep;
        }
        foreach (PendingIngredient p in m.pending)
            for (int k = 0; k < p.Totals.Length; k++)
            {
                p.Totals[k] *= keep;
                p.Released[k] *= keep;
            }
    }

    // ------------------------------------------------------------------ heat
    private static void Heat(AlchemyMixture m, AlchemySimulationSettings s, double dt, double reactionHeat)
    {
        AlchemyEquipmentProfile eq = m.Equipment;
        double mass = Math.Max(0.1, m.ThermalMass);
        double h = m.EffectiveHeat;
        double target = eq.TargetTemperature(h, m.Ambient);
        bool driven = h > 1e-6 || h < -1e-6 && eq.canCool;
        double k = driven ? eq.heatTransfer : eq.ambientTransfer;
        switch (m.Bath)
        {
            case AlchemyBath.Water:
                // Gentle, even heat: the water around the vessel never goes much above its boiling point.
                if (h > 0) target = Math.Min(target, s.waterBathMax);
                if (driven) k *= s.waterBathTransfer;
                break;
            case AlchemyBath.Ice:
            {
                // The ice wins: the contents are pulled toward its temperature; the fire only melts some of it.
                double fire = driven ? 0.3 * k : 0;
                target = (s.iceBathTemperature * s.iceBathTransfer + target * fire) / Math.Max(1e-6, s.iceBathTransfer + fire);
                k = s.iceBathTransfer + fire;
                break;
            }
        }
        double f = Math.Min(1.0, k * dt / mass);
        double temp = m.Temperature + (target - m.Temperature) * f;
        temp += reactionHeat / mass;
        m.Temperature = Clamp(temp, s.minTemperature, s.maxTemperature);
    }

    // ------------------------------------------------------------------ boiling
    private static void Boil(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        AlchemySubstance[] subs = m.Rules.Substances;
        double[] a = m.amounts;
        double[] e = t.delta;  // scratch: what boils off this step
        double[] v = t.demand; // scratch: what evaporates below its boiling point
        double temp = m.Temperature;
        double absorbed = 0, floor = double.MaxValue, lowest = double.MaxValue;
        double surface = Math.Max(0.0, m.Equipment.surface) * s.evaporationRate;
        for (int i = 0; i < a.Length; i++)
        {
            e[i] = v[i] = 0;
            AlchemySubstance sub = subs[i];
            if (sub.phase != SubstancePhase.Liquid || a[i] <= 0)
                continue;
            if (temp > sub.boilingPoint)
            {
                e[i] = Math.Min(a[i], s.boilRate * sub.evaporationMultiplier * a[i] * (temp - sub.boilingPoint) * dt);
                absorbed += e[i] * s.latentHeat;
                floor = Math.Min(floor, sub.boilingPoint);
            }
            else if (surface > 0)
            {
                // Below the boiling point it still evaporates, the more the closer it is: volatile oils leave first.
                v[i] = Math.Min(a[i], surface * sub.evaporationMultiplier * a[i] * Math.Pow(2.0, (temp - sub.boilingPoint) / s.evaporationDoublingDegrees) * dt);
            }
            if ((e[i] > 0 || v[i] > 1e-7) && sub.boilingPoint < lowest)
                lowest = sub.boilingPoint;
        }
        double scale = 1;
        double mass = Math.Max(0.1, m.ThermalMass);
        if (absorbed > 0)
        {
            // Boiling takes heat: never more than the contents have above the lowest boiling point (energy is kept).
            double available = Math.Max(0.0, (temp - floor) * mass);
            scale = absorbed > available ? available / absorbed : 1.0;
        }
        VaporHandling mode = m.VaporMode;
        double column = mode == VaporHandling.Condense ? Math.Max(0.0, m.Equipment.fractionation) : 0;
        double boiled = 0, taken = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double x = e[i] * scale + v[i];
            if (x <= 0) continue;
            // A fractionating column sends back what boils well above the most volatile part: it drips back in.
            if (column > 0)
                x /= 1.0 + column * Math.Max(0.0, subs[i].boilingPoint - lowest) / 10.0;
            x = Math.Min(x, a[i]);
            a[i] -= x;
            if (e[i] > 0) boiled += x;
            taken += x * s.latentHeat;
            switch (mode)
            {
                case VaporHandling.Condense: m.distillate[i] += x; break;
                case VaporHandling.Sealed: m.vapor[i] += x; break;
                default: t.Vented[i] += x; break;
            }
        }
        if (taken > 0)
            m.Temperature = absorbed > 0 ? Math.Max(temp - taken / mass, Math.Min(floor, temp)) : temp - taken / mass;
        t.BoilRate = boiled / dt;
        if (!t.Boiling && t.BoilRate >= 0.01)
        {
            t.Boiling = true;
            events?.Add(new AlchemyEvent(AlchemyEventKind.BoilingStarted, m.Elapsed + dt, "", "", (float)t.BoilRate));
        }
        else if (t.Boiling && t.BoilRate < 0.003)
        {
            t.Boiling = false;
            events?.Add(new AlchemyEvent(AlchemyEventKind.BoilingStopped, m.Elapsed + dt, "", ""));
        }
    }

    private static void Condense(AlchemyMixture m, AlchemySimulationSettings s, double dt)
    {
        if (m.VaporMode != VaporHandling.Sealed)
            return;
        AlchemySubstance[] subs = m.Rules.Substances;
        double released = 0, ceiling = double.MinValue;
        for (int i = 0; i < m.vapor.Length; i++)
        {
            if (m.vapor[i] <= 0 || m.Temperature >= subs[i].boilingPoint)
                continue;
            double c = m.vapor[i] * Math.Min(1.0, s.condenseRate * dt);
            m.vapor[i] -= c;
            m.amounts[i] += c;
            released += c * s.latentHeat;
            ceiling = Math.Max(ceiling, subs[i].boilingPoint);
        }
        if (released > 0)
            m.Temperature = Math.Min(m.Temperature + released / Math.Max(0.1, m.ThermalMass), ceiling);
    }

    private static void CheckPressure(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, List<AlchemyEvent> events)
    {
        if (m.VaporMode != VaporHandling.Sealed || m.Pressure <= m.Equipment.maxPressure)
            return;
        float pressure = (float)m.Pressure;
        for (int i = 0; i < m.vapor.Length; i++)
        {
            t.Vented[i] += m.vapor[i];
            m.vapor[i] = 0;
        }
        Spill(m, s.ruptureSpill);
        m.Ruptured = true;
        events?.Add(new AlchemyEvent(AlchemyEventKind.VesselRuptured, m.Elapsed + s.stepSeconds, "", "", pressure));
    }

    // ------------------------------------------------------------------ the manual fire
    /// <summary>
    /// A fire tended by hand: its flames rise toward what the fuel and the draft allow (fuel × (base draft + bellows air),
    /// choked by the air vent) and die down when either runs short; it burns fuel in proportion to the flames.
    /// </summary>
    private static void Fire(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        if (!m.FireManual)
            return;
        double fuel = 1.0 - Math.Exp(-m.Fuel / Math.Max(0.01, s.fuelScale));
        double draft = Clamp(m.Damper * (s.draftBase + s.bellowsDraft * m.Air), 0, 1);
        double target = fuel * draft;
        double tau = target > m.FireLevel ? s.fireRiseSeconds : s.fireFallSeconds;
        m.FireLevel = Clamp(m.FireLevel + (target - m.FireLevel) * Math.Min(1.0, dt / Math.Max(0.05, tau)), 0, 1);
        m.Fuel = Math.Max(0.0, m.Fuel - s.fuelBurnRate * m.FireLevel * dt);
        m.Air *= Math.Exp(-dt / Math.Max(0.1, s.bellowsDecaySeconds));
        if (m.Air < 1e-4) m.Air = 0;
        if (m.FireLevel < 0.005 && m.Fuel < 1e-4)
        {
            m.FireLevel = 0;
            m.Fuel = 0;
            if (t.FireLit)
            {
                t.FireLit = false;
                events?.Add(new AlchemyEvent(AlchemyEventKind.FireOut, m.Elapsed + dt, "", ""));
            }
        }
        else if (m.FireLevel > 0.02)
            t.FireLit = true;
    }

    // ------------------------------------------------------------------ stirring
    /// <summary>
    /// Agitation follows the stirring speed (and settles by itself when nobody stirs); uneven stirring leaves turbulence;
    /// hard stirring beats air in and slops an open vessel over; stirring emulsifies two layers, which separate again when
    /// it stops (slowly with an emulsifier).
    /// </summary>
    private static void Stirring(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, double dt, List<AlchemyEvent> events)
    {
        AlchemyEquipmentProfile eq = m.Equipment;
        double target = m.StirSpeed > 1e-4 ? Clamp(m.StirSpeed * (0.5 + eq.stirEfficiency), 0, 1) : 0;
        if (target > m.Agitation)
            m.Agitation += (target - m.Agitation) * Math.Min(1.0, dt / s.stirResponseSeconds);
        else
            m.Agitation = target + (m.Agitation - target) * Math.Exp(-dt / Math.Max(0.1, s.agitationDecaySeconds));
        if (m.Agitation < 1e-4) m.Agitation = 0;
        m.Turbulence *= Math.Exp(-dt / s.turbulenceDecaySeconds);
        if (m.Turbulence < 1e-4) m.Turbulence = 0;
        double air = Math.Max(0.0, m.Agitation - 0.5) * 2.0;
        m.Aeration += (air - m.Aeration) * Math.Min(1.0, dt / s.aerationSeconds);
        if (m.Aeration < 1e-4) m.Aeration = 0;

        if (t.SplashCooldown > 0) t.SplashCooldown -= dt;
        if (m.Agitation > s.splashAgitation && m.VaporMode != VaporHandling.Sealed && s.splashAgitation < 1f)
        {
            double share = s.splashRate * (m.Agitation - s.splashAgitation) / (1.0 - s.splashAgitation) * dt;
            Spill(m, share);
            if (t.SplashCooldown <= 0)
            {
                t.SplashCooldown = 3;
                events?.Add(new AlchemyEvent(AlchemyEventKind.Splashed, m.Elapsed + dt, "", "", (float)share));
            }
        }

        if (!m.IsLayered)
        {
            m.Emulsion = 0;
            return;
        }
        if (m.Agitation >= s.emulsifyAgitation)
            m.Emulsion += (1.0 - m.Emulsion) * Math.Min(1.0, dt / s.emulsionSeconds * m.Agitation / Math.Max(0.05, s.emulsifyAgitation));
        else
        {
            double emulsifier = Math.Max(0.0, m.PropertyValue(s.emulsifierProperty));
            double hold = 1.0 + s.emulsifierStability * Math.Min(1.0, emulsifier / 0.01);
            m.Emulsion -= m.Emulsion * Math.Min(1.0, dt / (s.emulsionBreakSeconds * hold));
        }
        m.Emulsion = Clamp(m.Emulsion, 0, 1);
    }

    /// <summary>The temperature's history: how fast it cools, its recent average and how far it strays from it.</summary>
    private static void Track(AlchemyMixture m, double dt, double before)
    {
        double rate = (m.Temperature - before) / dt;
        m.CoolingRate += (-rate - m.CoolingRate) * Math.Min(1.0, dt / 3.0);
        m.TemperatureMean += (m.Temperature - m.TemperatureMean) * Math.Min(1.0, dt / 20.0);
        m.Swing += (Math.Abs(m.Temperature - m.TemperatureMean) - m.Swing) * Math.Min(1.0, dt / 10.0);
    }

    // ------------------------------------------------------------------ cleanup and presence
    private static void Clean(AlchemyMixture m, AlchemyTracker t, AlchemySimulationSettings s, List<AlchemyEvent> events)
    {
        double trace = s.traceAmount;
        double notice = Math.Max(trace, s.noticeableAmount);
        for (int i = 0; i < m.amounts.Length; i++)
        {
            // A trace that grew this step is a trickle building up (a slowly released substance, a slow product, the
            // first drops of a distillate): it stays. Only what is dwindling or standing still is cleaned.
            if (m.amounts[i] < trace && m.amounts[i] <= t.wasAmount[i]) m.amounts[i] = 0;
            if (m.undissolved[i] < trace && m.undissolved[i] <= t.wasUndissolved[i]) m.undissolved[i] = 0;
            if (m.vapor[i] < trace && m.vapor[i] <= t.wasVapor[i]) m.vapor[i] = 0;
            if (m.distillate[i] < trace && m.distillate[i] <= t.wasDistillate[i]) m.distillate[i] = 0;
            if (!t.Present[i] && m.amounts[i] >= notice)
            {
                t.Present[i] = true;
                events?.Add(new AlchemyEvent(AlchemyEventKind.SubstanceAppeared, m.Elapsed + s.stepSeconds, m.Rules.Substances[i].id,
                    m.Rules.Substances[i].observation, (float)m.amounts[i]));
            }
            else if (t.Present[i] && m.amounts[i] < notice * 0.4)
            {
                t.Present[i] = false;
                events?.Add(new AlchemyEvent(AlchemyEventKind.SubstanceDepleted, m.Elapsed + s.stepSeconds, m.Rules.Substances[i].id,
                    m.Rules.Substances[i].observation));
            }
        }
    }

    internal static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
}
