using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// The evaluation of a synthesis (design spec 12): whether a combination of runes, concepts, an intention, a desired
    /// effect, an order and an energy allocation is coherent, and how well it is executed, as a Synthesis Quality Score of 0
    /// to 100. It reads the aspects of the concepts, never a recipe, so every combination - even one nobody has tried - gets
    /// an honest judgement, and new runes, concepts and effects work as soon as they have aspects.
    /// <para>The factors (each 0-1) are weighted into the score, with the weakest factor dragging it down:</para>
    /// <list type="bullet">
    /// <item><b>Coverage</b>: do the concepts together provide what the effect requires?</item>
    /// <item><b>Compatibility</b>: do the concepts and the runes' alignments work together or against each other?</item>
    /// <item><b>Intention</b>: does the purpose support the effect?</item>
    /// <item><b>Order</b>: do the runes act in the sequence the effect's chain asks for?</item>
    /// <item><b>Energy</b>: does each rune get about what its place asks, with nothing overpowering the rest (insufficient, optimal and excessive energy)?</item>
    /// <item><b>Execution</b>: rune quality, rank harmony, concepts within the runes' rank, understanding.</item>
    /// </list>
    /// A formula that is fundamentally incoherent (design spec 13.2) is flagged whatever its score. Pure and deterministic:
    /// chance only enters when a synthesis is performed.
    /// </summary>
    public static class SynthesisEvaluator
    {
        public static SynthesisEvaluation Evaluate(RuneCatalog c, ResolvedSynthesis rs, SynthesisContext ctx)
        {
            ctx = ctx ?? new SynthesisContext();
            var ev = new SynthesisEvaluation { mode = rs.mode, pure = rs.pure, rank = rs.resultRank };
            SynthesisBalance b = c.Synthesis;
            int n = rs.slots.Count;
            DesiredEffectDef fx = rs.effect;
            IntentionDef intention = rs.intention;
            ev.effectComplexity = fx != null ? fx.complexity : 0.3f;
            ev.powerDemand = fx != null ? fx.powerDemand : 1f;
            ev.evaluated = n > 0;
            if (n == 0) return ev;

            // ---- the concepts as aspect vectors
            var vec = new Dictionary<string, float>[n];
            float sumComplexity = 0f, sumQuality = 0f;
            int minRank = int.MaxValue, maxRank = 0;
            for (int i = 0; i < n; i++)
            {
                ResolvedSlot sl = rs.slots[i];
                vec[i] = Vector(sl.concept);
                sumComplexity += sl.concept != null ? sl.concept.complexity : 0.3f;
                sumQuality += sl.rune.quality;
                minRank = Math.Min(minRank, sl.rune.rank);
                maxRank = Math.Max(maxRank, sl.rune.rank);
                if (sl.primordial) ev.primordialRunes++;
                ev.totalEnergy += sl.energy;
            }
            ev.averageComplexity = sumComplexity / n;
            ev.averageQuality = sumQuality / n;

            // ---- coverage and relevance
            float[] relevance = new float[n];
            float coverage = 1f;
            if (rs.pure)
            {
                for (int i = 0; i < n; i++) relevance[i] = 1f;
            }
            else
            {
                coverage = Coverage(c, fx, vec, relevance, ev, b);
                ev.supportCoverage = SupportCoverage(fx, vec);
                ev.intentionBias = IntentionBias(intention, vec);
                // Rules that tie a concept to the effect itself.
                for (int i = 0; i < n; i++)
                    foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.ConceptEffect, rs.slots[i].concept.id, fx.id))
                    {
                        if (r.incoherent) ev.incoherence.Add(string.IsNullOrEmpty(r.note) ? $"{rs.slots[i].concept.name} cannot serve {fx.Name}." : r.note);
                        else if (r.value > 0f) relevance[i] = Clamp01(relevance[i] + r.value);
                    }
            }

            bool[] dead = DeadWeight(rs, relevance, b);

            // ---- compatibility (concepts, aspects, alignments)
            float compatibility = rs.pure ? 1f : Compatibility(c, rs, vec, dead, ev, b);

            // ---- intention
            float intentionFactor = rs.pure ? 1f : IntentionFactor(c, rs, ev);

            // ---- order
            var stage = new float[n];
            float order = rs.pure ? 1f : Order(rs, vec, ev, b, stage);

            // ---- the named formula this resembles (ideal energy ratios and fidelity)
            ArchetypeMatch match = SynthesisArchetypes.Match(c, rs);
            if (match != null)
            {
                ev.archetypeId = match.archetype.id;
                ev.archetypeFidelity = match.fidelity;
            }

            // ---- energy
            float energy = Energy(c, rs, relevance, dead, stage, match, ev, b);

            // ---- execution (rank, quality, understanding)
            float execution = Execution(rs, ev, minRank, maxRank, b);

            // ---- the score
            var f = new FactorScores
            {
                coverage = Clamp01(coverage), compatibility = Clamp01(compatibility), intention = Clamp01(intentionFactor),
                order = Clamp01(order), energy = Clamp01(energy), execution = Clamp01(execution),
            };
            ev.factors = f;
            float wSum = b.weightCoverage + b.weightCompatibility + b.weightIntention + b.weightOrder + b.weightEnergy + b.weightExecution;
            if (wSum <= 0f) wSum = 1f;
            float mean = (b.weightCoverage * f.coverage + b.weightCompatibility * f.compatibility + b.weightIntention * f.intention +
                          b.weightOrder * f.order + b.weightEnergy * f.energy + b.weightExecution * f.execution) / wSum;
            float s01 = (1f - b.minBlend) * mean + b.minBlend * f.Min;
            if (float.IsNaN(s01)) s01 = 0f; // bad numbers in the balance must not poison a score
            if (match != null)
            {
                float k = Clamp01(match.archetype.strictness);
                s01 *= (1f - k) + k * match.fidelity;
            }
            ev.score = (float)Math.Round(Math.Max(0f, Math.Min(100f, s01 * 100f)), 2);
            DistinctInPlace(ev.incoherence);
            // The tier a score earns; a fundamentally incoherent formula fails whatever its score (design spec 12.2, 13.2).
            ev.tier = ev.Incoherent ? SynthesisTier.CompleteFailure : c.Balance.TierOf(ev.score);

            // ---- risk and the chance of faltering
            ev.risk = Risk(ev, rs, b);
            ev.riskLevel = ev.risk >= DestructionRisk(c) ? RiskLevel.Extreme : ev.risk >= b.riskHigh ? RiskLevel.High : ev.risk >= b.riskHigh * 0.5f ? RiskLevel.Moderate : RiskLevel.Low;
            ev.failureChance = Clamp01(ctx.soul.synthesisFailureChance + b.riskFailureChance * ev.risk + b.interferenceFailureChance * ev.interference);

            ev.stats = StatsAt(c, ev, ctx, ev.score);
            return ev;
        }

        // ================================================================== coverage
        private static Dictionary<string, float> Vector(ConceptView k)
        {
            var v = new Dictionary<string, float>(StringComparer.Ordinal);
            if (k?.aspects == null) return v;
            foreach (AspectWeight a in k.aspects)
                if (a != null && !string.IsNullOrEmpty(a.aspect) && a.weight > 0f)
                    v[a.aspect] = (v.TryGetValue(a.aspect, out float w) ? w : 0f) + a.weight;
            return v;
        }

        private static float Cov(Dictionary<string, float> v, IList<string> anyOf)
        {
            float sum = 0f;
            if (anyOf == null) return 0f;
            foreach (string a in anyOf)
                if (v.TryGetValue(a, out float w)) sum += w;
            return sum;
        }

        /// <summary>
        /// How well the concepts together provide what the effect requires (the mean over its requirements of the share met),
        /// and each rune's relevance: the share of the requirements, and of the supporting aspects, it provides on its own.
        /// A requirement met by less than the floor makes the effect impossible to produce (design spec 13.2).
        /// </summary>
        private static float Coverage(RuneCatalog c, DesiredEffectDef fx, Dictionary<string, float>[] vec, float[] relevance, SynthesisEvaluation ev, SynthesisBalance b)
        {
            int n = vec.Length;
            var reqs = fx.required;
            float coverage = 1f;
            if (reqs != null && reqs.Count > 0)
            {
                float sum = 0f;
                int counted = 0;
                var slotSat = new float[n];
                foreach (AspectRequirement r in reqs)
                {
                    if (r == null) continue;
                    float min = Math.Max(0.0001f, r.minCoverage);
                    float cov = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float own = Cov(vec[i], r.anyOf);
                        cov += own;
                        slotSat[i] += Math.Min(1f, own / min);
                    }
                    float sat = Math.Min(1f, cov / min);
                    sum += sat;
                    counted++;
                    if (sat < b.hardCoverageFloor)
                        ev.incoherence.Add($"Nothing in this combination provides what {fx.Name} needs ({AspectNames(c, r.anyOf)}): the effect cannot be produced.");
                }
                if (counted > 0)
                {
                    coverage = sum / counted;
                    for (int i = 0; i < n; i++) relevance[i] = slotSat[i] / counted;
                }
            }
            else
                for (int i = 0; i < n; i++) relevance[i] = 1f;

            // Supporting aspects add a little to what a rune is worth to the effect.
            if (fx.supporting != null && fx.supporting.Count > 0 && reqs != null && reqs.Count > 0)
                for (int i = 0; i < n; i++)
                {
                    float sup = 0f;
                    int k = 0;
                    foreach (AspectWeight w in fx.supporting)
                    {
                        if (w == null) continue;
                        k++;
                        sup += vec[i].TryGetValue(w.aspect, out float have) ? Math.Min(1f, have / Math.Max(0.0001f, w.weight)) : 0f;
                    }
                    if (k > 0) relevance[i] = 0.8f * relevance[i] + 0.2f * (sup / k);
                }
            return coverage;
        }

        private static float SupportCoverage(DesiredEffectDef fx, Dictionary<string, float>[] vec)
        {
            if (fx.supporting == null || fx.supporting.Count == 0) return 0f;
            float sum = 0f;
            int k = 0;
            foreach (AspectWeight w in fx.supporting)
            {
                if (w == null) continue;
                float cov = 0f;
                foreach (Dictionary<string, float> v in vec) if (v.TryGetValue(w.aspect, out float have)) cov += have;
                sum += Math.Min(1f, cov / Math.Max(0.0001f, w.weight));
                k++;
            }
            return k > 0 ? sum / k : 0f;
        }

        private static float IntentionBias(IntentionDef intention, Dictionary<string, float>[] vec)
        {
            if (intention?.aspectBias == null || intention.aspectBias.Count == 0) return 0f;
            float total = 0f, got = 0f;
            foreach (AspectWeight w in intention.aspectBias)
            {
                if (w == null) continue;
                total += w.weight;
                float cov = 0f;
                foreach (Dictionary<string, float> v in vec) if (v.TryGetValue(w.aspect, out float have)) cov += have;
                got += w.weight * Math.Min(1f, cov);
            }
            return total > 0f ? Clamp01(got / (0.4f * total)) : 0f;
        }

        // ================================================================== compatibility
        private static float Compatibility(RuneCatalog c, ResolvedSynthesis rs, Dictionary<string, float>[] vec, bool[] dead, SynthesisEvaluation ev, SynthesisBalance b)
        {
            int n = rs.slots.Count;
            float maxConflict = 0f, sumConflict = 0f, sumSynergy = 0f;
            int pairs = 0;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    ResolvedSlot a = rs.slots[i], z = rs.slots[j];
                    float s = 0f;
                    foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.ConceptPair, a.concept.id, z.concept.id))
                    {
                        if (r.incoherent) ev.incoherence.Add(string.IsNullOrEmpty(r.note) ? $"{a.concept.name} and {z.concept.name} cannot be combined." : r.note);
                        else s += r.value;
                    }
                    foreach (KeyValuePair<string, float> pa in vec[i])
                        foreach (KeyValuePair<string, float> pz in vec[j])
                            foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.AspectPair, pa.Key, pz.Key))
                            {
                                if (r.incoherent)
                                {
                                    if (pa.Value >= b.incoherentAspectMin && pz.Value >= b.incoherentAspectMin)
                                        ev.incoherence.Add(string.IsNullOrEmpty(r.note) ? $"{AspectName(c, pa.Key)} and {AspectName(c, pz.Key)} cannot be combined." : r.note);
                                }
                                else s += r.value * pa.Value * pz.Value;
                            }
                    foreach (string al in a.alignments)
                        foreach (string bl in z.alignments)
                            foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.AlignmentPair, al, bl))
                            {
                                if (r.incoherent) ev.incoherence.Add(string.IsNullOrEmpty(r.note) ? $"{a.runeName} and {z.runeName} cannot be combined." : r.note);
                                else s += r.value;
                            }
                    s = Math.Max(-1f, Math.Min(1f, s));
                    pairs++;
                    if (s < 0f) { maxConflict = Math.Max(maxConflict, -s); sumConflict += -s; }
                    else sumSynergy += s;
                }
            float meanConflict = pairs > 0 ? sumConflict / pairs : 0f;
            float load = pairs > 0 ? 0.6f * maxConflict + 0.4f * meanConflict : 0f;
            ev.synergy = pairs > 0 ? Clamp01(sumSynergy / pairs) : 0f;

            // A concept that clashes with the effect itself counts as a conflict too.
            float effectClash = 0f;
            for (int i = 0; i < n; i++)
                foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.ConceptEffect, rs.slots[i].concept.id, rs.effect.id))
                    if (!r.incoherent && r.value < 0f) effectClash += -r.value / n;
            load = Clamp01(load + effectClash);
            ev.conflict = load;

            // Runes that add nothing are noise.
            int noise = 0;
            for (int i = 0; i < n; i++) if (dead[i]) noise++;
            return Clamp01(1f - load - 0.5f * (noise / (float)n));
        }

        /// <summary>
        /// The runes that add (almost) nothing to what the effect needs: less than the balance's share of an average rune's
        /// relevance. They are noise: they get little energy and count against compatibility.
        /// </summary>
        private static bool[] DeadWeight(ResolvedSynthesis rs, float[] relevance, SynthesisBalance b)
        {
            int n = relevance.Length;
            var dead = new bool[n];
            if (rs.pure || n < 2) return dead;
            float avg = 0f;
            foreach (float r in relevance) avg += r;
            avg /= n;
            for (int i = 0; i < n; i++) dead[i] = relevance[i] < b.deadWeightRelevance * avg;
            return dead;
        }

        private static void DistinctInPlace(List<string> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
                if (!seen.Add(list[i])) list.RemoveAt(i--);
        }

        private static float IntentionFactor(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev)
        {
            float support = c.IntentionSupport(rs.intention.id, rs.effect.id);
            if (support <= 0f)
            {
                ev.incoherence.Add($"{rs.intention.Name} does not support {rs.effect.Name}: the purpose and the result do not belong together.");
                return 0f;
            }
            float adjust = 0f;
            int n = rs.slots.Count;
            foreach (ResolvedSlot sl in rs.slots)
                foreach (CompatibilityRule r in c.RulesBetween(CompatibilityScope.ConceptIntention, sl.concept.id, rs.intention.id))
                {
                    if (r.incoherent) ev.incoherence.Add(string.IsNullOrEmpty(r.note) ? $"{sl.concept.name} cannot serve the intention {rs.intention.Name}." : r.note);
                    else adjust += r.value / n;
                }
            return Clamp01(support + 0.5f * adjust);
        }

        // ================================================================== order (design spec 11.3)
        /// <summary>
        /// Where in the effect's chain each concept acts (the average stage its aspects belong to), and how well the runes'
        /// sequence follows it. Swapping runes changes the score in proportion to how far apart in the chain they act.
        /// </summary>
        private static float Order(ResolvedSynthesis rs, Dictionary<string, float>[] vec, SynthesisEvaluation ev, SynthesisBalance b, float[] stage)
        {
            List<ChainStage> chain = rs.effect.EffectiveChain();
            int n = rs.slots.Count;
            if (chain == null || chain.Count < 2 || n < 2) return 1f;
            float[] centroid = stage; // filled for the energy step and the window (0 = off the chain)
            int on = 0;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float total = 0f, weighted = 0f;
                for (int g = 0; g < chain.Count; g++)
                {
                    float aff = chain[g] != null ? Cov(vec[i], chain[g].aspects) : 0f;
                    total += aff;
                    weighted += aff * (g + 1);
                }
                centroid[i] = total > 0f ? weighted / total : 0f;
                if (total > 0f)
                {
                    on++;
                    lo = Math.Min(lo, centroid[i]);
                    hi = Math.Max(hi, centroid[i]);
                }
            }
            if (on < 2) return 1f;
            float spread = hi - lo;
            float distance = 0f, wrong = 0f;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (centroid[i] <= 0f || centroid[j] <= 0f) continue;
                    float d = Math.Abs(centroid[i] - centroid[j]);
                    distance += d;
                    if (centroid[i] > centroid[j]) wrong += d;
                }
            if (spread < b.orderMinSpread || distance <= 1e-5f) return 1f; // the runes act at nearly the same point: order hardly matters
            float order = 1f - wrong / distance;
            if (order <= b.orderIncoherentBelow)
                ev.incoherence.Add($"The runes act in the opposite sequence from the one {rs.effect.Name} needs: the magic unwinds before it can form.");
            return order;
        }

        // ================================================================== energy (design spec 11.4, 11.5)
        private static float Energy(RuneCatalog c, ResolvedSynthesis rs, float[] relevance, bool[] dead, float[] stage, ArchetypeMatch match,
            SynthesisEvaluation ev, SynthesisBalance b)
        {
            int n = rs.slots.Count;
            float frac = b.EnergyFraction(rs.mode);
            float power = rs.effect != null ? rs.effect.powerDemand : 1f;

            // The ideal share of the energy per rune: from the archetype's ideal ratios when the formula resembles one, else
            // from how much each concept adds to the effect (a concept that adds nothing is given little).
            var share = new float[n];
            float sum = 0f;
            if (match != null)
            {
                float stageSum = 0f;
                int stages = 0;
                foreach (ArchetypeStage st in match.archetype.stages) if (st != null) { stageSum += Math.Max(0.05f, st.energyWeight); stages++; }
                float extra = stages > 0 ? 0.25f * stageSum / stages : 1f;
                for (int i = 0; i < n; i++)
                {
                    int st = match.stageOf[i];
                    share[i] = st >= 0 ? Math.Max(0.05f, match.archetype.stages[st].energyWeight) : extra;
                    sum += share[i];
                }
            }
            else
            {
                for (int i = 0; i < n; i++) { share[i] = relevance[i] + b.irrelevantShareFloor; sum += share[i]; }
            }
            for (int i = 0; i < n; i++) share[i] = sum > 1e-6f ? share[i] / sum : 1f / n;

            float magnitude = 0f, maxInterference = 0f, meanInterference = 0f;
            for (int i = 0; i < n; i++)
            {
                ResolvedSlot sl = rs.slots[i];
                float x = sl.concept != null ? Clamp01(sl.concept.complexity) : 0.3f;
                float cap = c.Balance.MaxEnergy(sl.rune.rank, sl.rune.quality);
                float cplx = 1f - b.complexityEnergy * 0.5f + b.complexityEnergy * x;
                float ideal = Math.Min(frac * cap * power * cplx * n * share[i], b.maxIdealShare * cap);
                float tol = b.optimalTolerance * (1f - b.complexityNarrowing * x);
                float safe = 1f + tol + (b.maxSafeShare - 1f - b.optimalTolerance) * (1f - b.complexityNarrowing * x);

                var se = new SlotEvaluation
                {
                    index = i, runeInstanceId = sl.rune.instanceId, runeId = sl.rune.runeId, runeName = sl.runeName,
                    conceptId = sl.concept != null ? sl.concept.id : "", conceptName = sl.concept != null ? sl.concept.name : "",
                    rank = sl.rune.rank, quality = sl.rune.quality, available = sl.rune.energy, energy = sl.energy,
                    relevance = relevance[i], idealShare = share[i], idealEnergy = ideal, minEnergy = b.minEnergyShare * ideal,
                    maxSafeEnergy = safe * ideal, understanding = sl.understanding,
                    overreach = Math.Max(0f, x - (b.conceptCapacityBase + b.conceptCapacityPerRank * (sl.rune.rank - 1))),
                    deadWeight = dead[i], stage = stage[i],
                };
                float g = 1f, interference = 0f;
                if (ideal <= 1e-6f)
                    se.state = EnergyState.Optimal;
                else
                {
                    float r = sl.energy / ideal;
                    float min = b.minEnergyShare;
                    if (r < min - 1e-6f) { se.state = EnergyState.Insufficient; g = 0f; }
                    else if (r < 1f - tol)
                    {
                        se.state = EnergyState.Low;
                        g = Lerp(0.55f, 1f, (r - min) / Math.Max(1e-4f, 1f - tol - min));
                    }
                    else if (r <= 1f + tol + 1e-6f) se.state = EnergyState.Optimal;
                    else if (r <= safe + 1e-6f)
                    {
                        se.state = EnergyState.High;
                        g = Lerp(1f, 0.7f, (r - (1f + tol)) / Math.Max(1e-4f, safe - (1f + tol)));
                    }
                    else
                    {
                        se.state = EnergyState.Excessive;
                        float over = Clamp01((r - safe) / 1f);
                        g = Math.Max(0f, 0.7f - 0.7f * over);
                        interference = 0.3f + 0.7f * over;
                    }
                }
                se.energyScore = g;
                se.interference = interference;
                magnitude += share[i] * g;
                maxInterference = Math.Max(maxInterference, interference);
                meanInterference += share[i] * interference;
                ev.slots.Add(se);
            }
            // A rune that carries much more than its share overpowers the others.
            float totalE = ev.totalEnergy;
            foreach (SlotEvaluation se in ev.slots)
                if (totalE > 0f && se.energy / totalE - se.idealShare > 0.25f && (se.state == EnergyState.High || se.state == EnergyState.Excessive)) se.overpowering = true;
            ev.interference = Clamp01(0.7f * maxInterference + 0.3f * meanInterference);
            return Clamp01(magnitude) * (1f - b.interferenceWeight * ev.interference);
        }

        // ================================================================== execution
        private static float Execution(ResolvedSynthesis rs, SynthesisEvaluation ev, int minRank, int maxRank, SynthesisBalance b)
        {
            int n = rs.slots.Count;
            float quality = Clamp01(ev.averageQuality / Math.Max(1f, b.fullCreditQuality));
            float understanding = 0f;
            foreach (ResolvedSlot sl in rs.slots) understanding += sl.understanding;
            understanding = Clamp01(understanding / n / Math.Max(0.05f, b.fullCreditKnowledge));
            float harmony = Clamp01(1f - b.rankHarmonyLoss * Math.Max(0, maxRank - minRank));
            float over = 0f;
            foreach (SlotEvaluation se in ev.slots) over += se.overreach;
            ev.overreach = n > 0 ? over / n : 0f;
            float within = Clamp01(1f - 1.5f * ev.overreach);
            return 0.4f * quality + 0.2f * understanding + 0.2f * harmony + 0.2f * within;
        }

        // ================================================================== risk (design spec 13.3)
        private static float Risk(SynthesisEvaluation ev, ResolvedSynthesis rs, SynthesisBalance b)
        {
            int n = rs.slots.Count;
            float primordial = Clamp01(2f * ev.primordialRunes / Math.Max(1, n));
            float r = b.riskEffectComplexity * ev.effectComplexity + b.riskConceptComplexity * Clamp01(ev.averageComplexity + ev.overreach) +
                      b.riskPrimordial * primordial + b.riskInterference * ev.interference + b.riskRuneCount * Math.Min(1f, (n - 1) / 10f);
            if (rs.mode == SynthesisMode.RankAdvancement) r += 0.1f;
            return Clamp01(r);
        }

        /// <summary>The risk (0-1) from which a failure can destroy runes: the lowest minimum risk of a severity that destroys (2 = never).</summary>
        public static float DestructionRisk(RuneCatalog c)
        {
            float min = 2f;
            foreach (FailureSeverity f in c.Balance.failureSeverities)
                if (f != null && f.destroysRunes) min = Math.Min(min, f.minimumRisk);
            return min > 1f ? 2f : min;
        }

        // ================================================================== what a score is worth
        /// <summary>The stats a result earns at <paramref name="score"/> (before flaws): power, stability, cost, quality.</summary>
        public static SynthesisStats StatsAt(RuneCatalog c, SynthesisEvaluation ev, SynthesisContext ctx, float score)
        {
            SynthesisBalance b = c.Synthesis;
            float pw = CurvePoint.Evaluate(b.powerCurve, score);
            float st = CurvePoint.Evaluate(b.stabilityCurve, score);
            float cost = CurvePoint.Evaluate(b.costCurve, score);
            float effectPower = (float)Math.Sqrt(Math.Max(0.05f, ev.powerDemand));
            float bonus = b.bonusPower * Clamp01(0.5f * ev.synergy + 0.3f * ev.supportCoverage + 0.2f * ev.intentionBias);
            float rankPower = (float)Math.Pow(b.powerPerRank, Math.Max(0, ev.rank - 1));
            float soulStability = ctx != null ? ctx.soul.synthesisStability : 1f;
            float order = ev.factors.order;
            return new SynthesisStats
            {
                rank = ev.rank,
                power = b.baseSpellPower * rankPower * pw * effectPower * (1f + bonus) * (1f + b.powerPerRune * Math.Max(0, ev.slots.Count - 1)),
                stability = Math.Max(0f, Math.Min(100f, 100f * st * (0.7f + 0.3f * ev.averageQuality / 100f) * soulStability *
                                                      (1f - 0.6f * ev.interference) * (0.6f + 0.4f * order))),
                energyCostMultiplier = cost * (1.3f - 0.3f * order),
                quality = Math.Max(0f, Math.Min(100f, ev.averageQuality * (0.6f + 0.4f * score / 100f) + (score >= 100f ? 10f : 0f))),
                controlPenalty = 0f,
            };
        }

        // ================================================================== helpers
        private static string AspectName(RuneCatalog c, string id)
        {
            AspectDef a = c.Aspect(id);
            return a != null ? a.Name.ToLowerInvariant() : id;
        }

        private static string AspectNames(RuneCatalog c, IList<string> ids)
        {
            var names = new List<string>();
            foreach (string id in ids) names.Add(AspectName(c, id));
            return string.Join(" or ", names);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
