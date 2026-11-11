using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneMagic
{
    /// <summary>What the simulator samples.</summary>
    public sealed class SimulationOptions
    {
        /// <summary>How many random spells to build.</summary>
        public int samples = 2000;
        /// <summary>The seed (the same seed and the same data give the same report).</summary>
        public int seed = 1;
        public int minRank = 1;
        public int maxRank = 5;
        public float minComplexity = 0.1f;
        public float maxComplexity = 1f;
        /// <summary>Cast every spell with this charge (0-1) as well as it can be charged (chargeable forms only); 0 = no charge.</summary>
        public float charge;
        /// <summary>A build is an outlier when its value is more than this many inter-quartile ranges beyond the quartiles of its rank.</summary>
        public float outlierFactor = 3f;
        /// <summary>The most outliers listed.</summary>
        public int maxOutliers = 12;
    }

    /// <summary>One random spell and what casting it costs and does.</summary>
    public sealed class SimulatedSpell
    {
        public string effectId = "", formId = "";
        public int rank;
        public float complexity, power, stability;
        /// <summary>Rune Energy per cast.</summary>
        public float energy;
        /// <summary>Soul Integrity a cast costs.</summary>
        public float strain;
        public float castTime, cooldown;
        /// <summary>How hard it hits relative to the spell's base (its damage factor, the soul's condition, a charge).</summary>
        public float punch;
        /// <summary>Punch per second of cast time and cooldown.</summary>
        public float rate;
        /// <summary>Punch per 1,000 energy (compare within a rank: the cost grows with the rank).</summary>
        public float perKiloEnergy;
        /// <summary>Punch per point of Soul Integrity.</summary>
        public float perStrain;
        /// <summary>Casts that take a full soul to Strained.</summary>
        public float castsToStrained;
        public bool chargeable;
        /// <summary>An attack (an offense effect): only attacks have a punch worth comparing; the others are measured by cost and strain.</summary>
        public bool offensive;

        public string Describe() =>
            $"rank {rank} {effectId} ({formId}) complexity {complexity.ToString("0.00", CultureInfo.InvariantCulture)} power {power.ToString("0", CultureInfo.InvariantCulture)}";
    }

    /// <summary>The spread of one number over the builds.</summary>
    public sealed class MetricSummary
    {
        public string name = "";
        public float min, p5, median, mean, p95, max, stdDev;
    }

    /// <summary>A build far outside its rank's usual range on one number.</summary>
    public sealed class SimulationOutlier
    {
        public string metric = "";
        public bool high;
        public float value, median;
        public SimulatedSpell spell;
    }

    /// <summary>The averages of a group of builds (a rank, a form).</summary>
    public sealed class GroupSummary
    {
        public string key = "";
        public int count;
        public float energy, strain, punch, rate, perKiloEnergy, perStrain, castsToStrained;
    }

    public sealed class SimulationReport
    {
        public SimulationOptions options;
        public List<SimulatedSpell> builds = new List<SimulatedSpell>();
        public List<MetricSummary> metrics = new List<MetricSummary>();
        public List<GroupSummary> byRank = new List<GroupSummary>();
        public List<GroupSummary> byForm = new List<GroupSummary>();
        public List<SimulationOutlier> outliers = new List<SimulationOutlier>();

        public MetricSummary Metric(string name) => metrics.Find(m => m.name == name);

        public string ToText()
        {
            var sb = new StringBuilder();
            CultureInfo ci = CultureInfo.InvariantCulture;
            sb.Append($"SPELL SIMULATOR: {builds.Count} random builds (seed {options.seed}, ranks {options.minRank}-{options.maxRank}, charge {options.charge.ToString("0.##", ci)})\n");
            sb.Append("\nNUMBERS (min / 5% / median / mean / 95% / max)\n");
            foreach (MetricSummary m in metrics)
                sb.Append($"  {m.name}: {Fmt(m.min)} / {Fmt(m.p5)} / {Fmt(m.median)} / {Fmt(m.mean)} / {Fmt(m.p95)} / {Fmt(m.max)}\n");
            sb.Append("\nBY RANK (average: energy, strain, punch, punch per second, punch per 1000 energy, casts to Strained)\n");
            foreach (GroupSummary g in byRank) sb.Append("  " + Row(g));
            sb.Append("\nBY FORM\n");
            foreach (GroupSummary g in byForm) sb.Append("  " + Row(g));
            sb.Append($"\nOUTLIERS (beyond {options.outlierFactor.ToString("0.#", ci)} inter-quartile ranges of their rank): {outliers.Count}\n");
            foreach (SimulationOutlier o in outliers)
                sb.Append($"  {(o.high ? "HIGH" : "LOW ")} {o.metric} {Fmt(o.value)} (rank median {Fmt(o.median)}): {o.spell.Describe()}\n");
            return sb.ToString();
        }

        private static string Fmt(float v) => v.ToString(Math.Abs(v) >= 100f ? "0" : "0.##", CultureInfo.InvariantCulture);

        private static string Row(GroupSummary g) =>
            $"{g.key} ×{g.count}: energy {Fmt(g.energy)}, strain {Fmt(g.strain)}, punch {Fmt(g.punch)}, /s {Fmt(g.rate)}, /1000e {Fmt(g.perKiloEnergy)}, casts to Strained {Fmt(g.castsToStrained)}\n";
    }

    /// <summary>
    /// The spell-build simulator: makes thousands of random spells from the database's content and balance, compiles each the way the game does,
    /// and reports power, cost and strain with the builds that stand out, so a designer sees an overpowered or useless corner of the tables without
    /// playing it. Pure: it runs the rules the server runs, on the data it is given. (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Spell Simulator.)
    /// </summary>
    public static class SpellSimulator
    {
        public static SimulationReport Run(RuneCatalog c, SimulationOptions options = null)
        {
            options = options ?? new SimulationOptions();
            var report = new SimulationReport { options = options };
            var rng = new Random(options.seed);
            int effects = c.Effects.Count;
            if (effects == 0 || options.samples <= 0) return report;
            int minRank = Math.Max(1, options.minRank), maxRank = Math.Max(minRank, options.maxRank);
            float maxIntegrity = c.Balance.maxSoulIntegrity;

            for (int i = 0; i < options.samples; i++)
            {
                DesiredEffectDef effect = c.Effects[rng.Next(effects)];
                int rank = minRank + rng.Next(maxRank - minRank + 1);
                float complexity = options.minComplexity + (float)rng.NextDouble() * Math.Max(0f, options.maxComplexity - options.minComplexity);
                // A spell's power grows with its rank (as the synthesis engine's does) and varies with the quality of what made it.
                float power = 100f * (float)Math.Pow(1.3, rank - 1) * (0.6f + 0.8f * (float)rng.NextDouble());
                float stability = 40f + 60f * (float)rng.NextDouble();
                var record = new SpellRecord { spellId = "sim", effectId = effect.id, rank = rank, complexity = complexity, power = power, stability = stability, anchors = new List<string>() };
                CompiledSpell cs = SpellCompiler.Compile(c, record);
                SpellFormDef form = c.SpellForm(cs.formId);
                CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
                bool chargeable = form != null && form.chargeable && c.Spells.charge != null && c.Spells.charge.enabled;
                float punch = cs.damage * plan.powerMultiplier;
                float held = 0f; // holding the key is time too: a charged spell is not free speed
                if (options.charge > 0f && chargeable)
                {
                    SpellCasting.ApplyCharge(ref plan, c.Spells, options.charge);
                    punch *= ChargeProfile.Scale(c.Spells.charge.damageAtFull, options.charge);
                    held = c.Spells.charge.SecondsFor(options.charge);
                }
                float cycle = Math.Max(0.1f, held + plan.castTimeMultiplier + cs.cooldown);
                var s = new SimulatedSpell
                {
                    effectId = effect.id, formId = cs.formId, rank = rank, complexity = complexity, power = power, stability = stability,
                    energy = plan.energyCost, strain = plan.strainScaled, castTime = plan.castTimeMultiplier, cooldown = cs.cooldown,
                    punch = punch, rate = punch / cycle, chargeable = chargeable, offensive = effect.category == EffectCategory.Offensive,
                };
                s.perKiloEnergy = s.energy > 0f ? punch / s.energy * 1000f : 0f;
                s.perStrain = s.strain > 0f ? punch / s.strain : 0f;
                s.castsToStrained = s.strain > 0f ? maxIntegrity * 0.2f / s.strain : float.PositiveInfinity;
                report.builds.Add(s);
            }

            AddMetric(report, "energy", s => s.energy);
            AddMetric(report, "strain", s => s.strain);
            // Only attacks have a punch worth comparing.
            AddMetric(report, "punch (attacks)", s => s.punch, s => s.offensive);
            AddMetric(report, "punch per second (attacks)", s => s.rate, s => s.offensive);
            AddMetric(report, "punch per 1000 energy (attacks)", s => s.perKiloEnergy, s => s.offensive);
            AddMetric(report, "punch per strain (attacks)", s => s.perStrain, s => s.offensive);
            AddMetric(report, "casts to Strained", s => Math.Min(s.castsToStrained, 1000f));
            Groups(report, s => "rank " + s.rank, report.byRank, true);
            Groups(report, s => s.formId, report.byForm, false);
            FindOutliers(report);
            return report;
        }

        // ------------------------------------------------------------------ numbers
        private static void AddMetric(SimulationReport r, string name, Func<SimulatedSpell, float> get, Func<SimulatedSpell, bool> include = null)
        {
            var values = new List<float>(r.builds.Count);
            foreach (SimulatedSpell s in r.builds) if (include == null || include(s)) values.Add(get(s));
            r.metrics.Add(Summarize(name, values));
        }

        public static MetricSummary Summarize(string name, List<float> values)
        {
            var m = new MetricSummary { name = name };
            if (values.Count == 0) return m;
            var sorted = new List<float>(values);
            sorted.Sort();
            double sum = 0;
            foreach (float v in sorted) sum += v;
            double mean = sum / sorted.Count, var = 0;
            foreach (float v in sorted) var += (v - mean) * (v - mean);
            m.min = sorted[0];
            m.max = sorted[sorted.Count - 1];
            m.p5 = Percentile(sorted, 0.05f);
            m.median = Percentile(sorted, 0.5f);
            m.p95 = Percentile(sorted, 0.95f);
            m.mean = (float)mean;
            m.stdDev = (float)Math.Sqrt(var / sorted.Count);
            return m;
        }

        /// <summary>The value at <paramref name="p"/> (0-1) of a sorted list, interpolated.</summary>
        public static float Percentile(List<float> sorted, float p)
        {
            if (sorted == null || sorted.Count == 0) return 0f;
            float pos = Math.Max(0f, Math.Min(1f, p)) * (sorted.Count - 1);
            int lo = (int)Math.Floor(pos), hi = Math.Min(sorted.Count - 1, lo + 1);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
        }

        private static void Groups(SimulationReport r, Func<SimulatedSpell, string> key, List<GroupSummary> into, bool ordered)
        {
            var groups = new Dictionary<string, GroupSummary>();
            foreach (SimulatedSpell s in r.builds)
            {
                string k = key(s);
                if (!groups.TryGetValue(k, out GroupSummary g)) groups[k] = g = new GroupSummary { key = k };
                g.count++;
                g.energy += s.energy; g.strain += s.strain; g.punch += s.punch; g.rate += s.rate; g.perKiloEnergy += s.perKiloEnergy; g.perStrain += s.perStrain;
                g.castsToStrained += Math.Min(s.castsToStrained, 1000f);
            }
            foreach (GroupSummary g in groups.Values)
            {
                float n = Math.Max(1, g.count);
                g.energy /= n; g.strain /= n; g.punch /= n; g.rate /= n; g.perKiloEnergy /= n; g.perStrain /= n; g.castsToStrained /= n;
                into.Add(g);
            }
            into.Sort((a, b) => ordered ? string.CompareOrdinal(a.key, b.key) : b.rate.CompareTo(a.rate));
        }

        // ------------------------------------------------------------------ outliers
        private static void FindOutliers(SimulationReport r)
        {
            var found = new List<SimulationOutlier>();
            foreach (var m in new (string name, Func<SimulatedSpell, float> get)[]
            {
                ("punch per second", s => s.rate), ("punch per 1000 energy", s => s.perKiloEnergy), ("punch per strain", s => s.perStrain),
            })
            {
                // Within a rank: the cost and the power both grow with it.
                var byRank = new Dictionary<int, List<SimulatedSpell>>();
                foreach (SimulatedSpell s in r.builds)
                {
                    if (!s.offensive) continue;
                    if (!byRank.TryGetValue(s.rank, out List<SimulatedSpell> list)) byRank[s.rank] = list = new List<SimulatedSpell>();
                    list.Add(s);
                }
                foreach (List<SimulatedSpell> list in byRank.Values)
                {
                    if (list.Count < 8) continue;
                    var values = new List<float>();
                    foreach (SimulatedSpell s in list) if (!float.IsInfinity(m.get(s))) values.Add(m.get(s));
                    values.Sort();
                    float q1 = Percentile(values, 0.25f), q3 = Percentile(values, 0.75f), median = Percentile(values, 0.5f), iqr = q3 - q1;
                    float hi = q3 + r.options.outlierFactor * iqr, lo = q1 - r.options.outlierFactor * iqr;
                    foreach (SimulatedSpell s in list)
                    {
                        float v = m.get(s);
                        if (float.IsInfinity(v) || iqr <= 0f) continue;
                        if (v > hi) found.Add(new SimulationOutlier { metric = m.name, high = true, value = v, median = median, spell = s });
                        else if (v < lo) found.Add(new SimulationOutlier { metric = m.name, high = false, value = v, median = median, spell = s });
                    }
                }
            }
            // The worst first: how many medians away.
            found.Sort((a, b) => Math.Abs(Math.Log((b.value + 1e-3) / (b.median + 1e-3))).CompareTo(Math.Abs(Math.Log((a.value + 1e-3) / (a.median + 1e-3)))));
            for (int i = 0; i < found.Count && i < r.options.maxOutliers; i++) r.outliers.Add(found[i]);
        }
    }
}
