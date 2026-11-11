using System;
using System.Collections.Generic;
using System.Text;

namespace RuneMagic
{
    /// <summary>
    /// A small deterministic random generator (SplitMix64): the same seed gives the same chances on every machine and runtime,
    /// so a synthesis can be reproduced from its seed (tests, bug reports) and the server's roll is the only roll.
    /// </summary>
    internal struct SynthesisRng
    {
        private ulong state;

        public SynthesisRng(int seed)
        {
            unchecked { state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL; }
        }

        public ulong NextULong()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>A value in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) / 16777216f;

        public int NextInt(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextULong() % (ulong)maxExclusive);

        public string NextId(int length)
        {
            const string hex = "0123456789abcdef";
            var sb = new StringBuilder(length);
            ulong v = 0;
            for (int i = 0; i < length; i++)
            {
                if (i % 16 == 0) v = NextULong();
                sb.Append(hex[(int)(v & 0xF)]);
                v >>= 4;
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// What a synthesis does to the soul once its score is known (design spec 13, 14): the Soul Damage it causes, the flaws of
    /// a flawed result, the name, the spell or rune it makes, and the penalties of a failure. Pure functions over
    /// <see cref="RuneSoulState"/>; <see cref="SynthesisEngine"/> decides which of them to apply.
    /// </summary>
    internal static class SynthesisOutcomes
    {
        // ================================================================== soul damage (design spec 14.1)
        /// <summary>
        /// The Soul Damage a synthesis that ends in <paramref name="tier"/> causes, by kind (amounts before the kinds' and the
        /// condition's multipliers): the strain of the synthesis itself (more with more runes, higher ranks, harder effects, a
        /// rank advancement, a riskier attempt; a worse outcome strains more), Primordial runes, concepts beyond their rune's
        /// rank, and a dangerous attempt.
        /// </summary>
        public static List<SoulDamageEntry> SoulDamagePlan(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev, SynthesisTier tier)
        {
            SynthesisBalance b = c.Synthesis;
            var plan = new List<SoulDamageEntry>();
            int n = rs.slots.Count;
            bool failed = tier == SynthesisTier.CompleteFailure;
            float baseDamage = b.soulDamageBase * (1f + b.soulDamagePerRune * Math.Max(0, n - 1)) *
                               (1f + b.soulDamagePerRank * Math.Max(0, rs.resultRank - 1)) *
                               (rs.mode == SynthesisMode.RankAdvancement ? b.soulDamageAdvancement : 1f) *
                               (0.5f + ev.effectComplexity);
            float tierFactor = tier == SynthesisTier.Perfect ? b.soulDamagePerfect : tier == SynthesisTier.Successful ? b.soulDamageSuccessful :
                tier == SynthesisTier.Flawed ? b.soulDamageFlawed : b.soulDamageFailure;
            float amount = baseDamage * tierFactor * (1f + b.soulDamageRisk * ev.risk);
            Add(plan, failed ? "synthesis_failure" : "synthesis", amount, failed ? "failed synthesis" : "synthesis");

            if (ev.primordialRunes > 0)
                Add(plan, "primordial", b.primordialDamage * ev.primordialRunes * (failed ? 1.5f : 1f), "primordial runes");

            float overreach = 0f;
            foreach (SlotEvaluation se in ev.slots) overreach += se.overreach;
            if (overreach > 0.05f)
                Add(plan, "concept_overreach", b.overreachDamage * overreach * (failed ? 1.5f : 1f), "concepts beyond the runes' rank");

            float danger = b.riskHigh < 1f ? Clamp01((ev.risk - b.riskHigh) / (1f - b.riskHigh)) : 0f;
            if (danger > 0f)
                Add(plan, "experiment", baseDamage * 0.5f * danger * (failed ? 1.5f : 1f), "dangerous synthesis");
            return plan;
        }

        private static void Add(List<SoulDamageEntry> plan, string kind, float amount, string source)
        {
            amount = (float)Math.Round(Math.Max(0f, amount), 1);
            if (amount > 0f) plan.Add(new SoulDamageEntry { kind = kind, amount = amount, source = source });
        }

        /// <summary>What a plan costs the soul in the given condition: each amount times its kind's multiplier (and the condition's, when the kind says so).</summary>
        public static float ScaledTotal(RuneCatalog c, SynthesisContext ctx, List<SoulDamageEntry> plan)
        {
            float total = 0f;
            foreach (SoulDamageEntry e in plan)
            {
                SoulDamageKind kind = c.Balance.DamageKind(e.kind);
                float m = kind != null ? kind.multiplier : 1f;
                if ((kind == null || kind.scaledByCondition) && ctx != null) m *= ctx.soul.soulDamageMultiplier;
                total += e.amount * Math.Max(0f, m);
            }
            return total;
        }

        // ================================================================== flaws (design spec 13.1)
        /// <summary>
        /// The flaws of a Flawed result: the factors that held the score back decide the causes (a poor order, badly spread
        /// energy...), the score's place in the Flawed band the number of flaws (one to three) and <paramref name="severity"/>
        /// (0 at the top of the band, 1 at the bottom), and chance picks among the flaws of a cause.
        /// </summary>
        public static List<SynthesisFlawDef> PickFlaws(RuneCatalog c, SynthesisEvaluation ev, float score, ref SynthesisRng rng, out float severity)
        {
            SynthesisThresholds t = c.Balance.synthesis;
            severity = Clamp01((t.successful - score) / Math.Max(0.01f, t.successful - t.flawed));
            var picked = new List<SynthesisFlawDef>();

            FactorScores f = ev.factors;
            var deficit = new float[7];
            deficit[(int)FlawCause.Coverage] = 1f - f.coverage;
            deficit[(int)FlawCause.Compatibility] = 1f - f.compatibility;
            deficit[(int)FlawCause.Intention] = 1f - f.intention;
            deficit[(int)FlawCause.Order] = 1f - f.order;
            deficit[(int)FlawCause.EnergyExcess] = ev.interference;
            float ratio = Math.Max(0f, 1f - f.energy - 0.5f * ev.interference);
            if (!string.IsNullOrEmpty(ev.archetypeId)) ratio = Math.Max(ratio, 1f - ev.archetypeFidelity);
            deficit[(int)FlawCause.EnergyImbalance] = ratio;
            deficit[(int)FlawCause.Execution] = 1f - f.execution;

            int count = 1 + (severity >= 1f / 3f ? 1 : 0) + (severity >= 2f / 3f ? 1 : 0); // one flaw in the top third of the band, three in the bottom
            var used = new bool[7];
            for (int k = 0; k < count; k++)
            {
                int best = -1;
                float bestDeficit = 0.02f;
                for (int cause = 0; cause < 7; cause++)
                    if (!used[cause] && deficit[cause] > bestDeficit) { best = cause; bestDeficit = deficit[cause]; }
                if (best < 0) break;
                used[best] = true;
                SynthesisFlawDef flaw = PickByWeight(c, (FlawCause)best, ref rng);
                if (flaw != null) picked.Add(flaw);
            }
            if (picked.Count == 0)
            {
                // Whatever held the score back left no clear cause (or the data has no flaw for it): the most deficient cause
                // that has a flaw, else any flaw.
                var order = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
                order.Sort((x, y) => deficit[y].CompareTo(deficit[x]));
                foreach (int cause in order)
                {
                    SynthesisFlawDef flaw = PickByWeight(c, (FlawCause)cause, ref rng);
                    if (flaw != null) { picked.Add(flaw); break; }
                }
            }
            return picked;
        }

        private static SynthesisFlawDef PickByWeight(RuneCatalog c, FlawCause cause, ref SynthesisRng rng)
        {
            float total = 0f;
            SynthesisFlawDef last = null;
            foreach (SynthesisFlawDef f in c.Flaws)
            {
                if (f == null || f.cause != cause) continue;
                total += Math.Max(0f, f.weight);
                last = f;
            }
            if (last == null || total <= 0f) return last;
            float roll = rng.NextFloat() * total;
            foreach (SynthesisFlawDef f in c.Flaws)
            {
                if (f == null || f.cause != cause) continue;
                roll -= Math.Max(0f, f.weight);
                if (roll <= 0f) return f;
            }
            return last;
        }

        /// <summary>
        /// Applies the flaws to the stats of the result: each flaw's multipliers at the strength the severity gives
        /// (a light flaw still has a third of its full effect). <paramref name="qualityFactor"/> is how much the flaws take off a rune's quality.
        /// </summary>
        public static void ApplyFlaws(ref SynthesisStats stats, List<SynthesisFlawDef> flaws, float severity, List<string> sideEffects, out float qualityFactor)
        {
            float strength = 0.35f + 0.65f * Clamp01(severity);
            qualityFactor = 1f;
            foreach (SynthesisFlawDef f in flaws)
            {
                stats.power *= 1f + (f.powerMultiplier - 1f) * strength;
                float stability = 1f + (f.stabilityMultiplier - 1f) * strength;
                stats.stability *= stability;
                qualityFactor *= stability;
                stats.energyCostMultiplier *= 1f + (f.costMultiplier - 1f) * strength;
                stats.controlPenalty += f.controlPenalty * strength;
                if (!string.IsNullOrEmpty(f.sideEffect) && !sideEffects.Contains(f.sideEffect)) sideEffects.Add(f.sideEffect);
            }
            stats.power = Math.Max(0f, stats.power);
            stats.stability = Math.Max(0f, Math.Min(100f, stats.stability));
            stats.energyCostMultiplier = Math.Max(0.1f, stats.energyCostMultiplier);
            stats.controlPenalty = Clamp01(stats.controlPenalty);
            stats.quality = Math.Max(0f, Math.Min(100f, stats.quality * qualityFactor));
        }

        // ================================================================== names
        /// <summary>A player-chosen name made safe to show: no markup or control characters, at most 40 characters (null when nothing is left).</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sb = new StringBuilder(name.Length);
            bool space = false;
            foreach (char ch in name)
            {
                if (char.IsControl(ch) || ch == '<' || ch == '>' || ch == '|') continue;
                if (char.IsWhiteSpace(ch))
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(ch);
                if (sb.Length >= 40) break;
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>The name of a rune without its rank title ("Wind"), or a composite rune's own name.</summary>
        public static string BaseName(RuneCatalog c, RuneInstance r)
        {
            if (r.IsComposite && !string.IsNullOrEmpty(r.composite.name)) return r.composite.name;
            RuneDef def = c.Rune(r.runeId);
            return def != null ? def.Name : string.IsNullOrEmpty(r.runeId) ? "Rune" : r.runeId;
        }

        /// <summary>
        /// What a synthesis' result is called: the player's name, else (a rank advancement of one rune) the rune at its new
        /// rank, else the name of the archetype the formula matched, else one built from the runes that carry the formula and the effect.
        /// </summary>
        public static string NameOf(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev, SynthesisRequest req)
        {
            string custom = CleanName(req.name);
            if (custom != null) return custom;
            if (rs.pure)
            {
                RuneInstance first = rs.slots[0].rune;
                return first.IsComposite ? first.composite.name : RuneRules.DisplayName(c, first.runeId, rs.resultRank);
            }
            OutcomeArchetype a = string.IsNullOrEmpty(ev.archetypeId) ? null : c.Archetype(ev.archetypeId);
            if (a != null) return rs.mode == SynthesisMode.Spell ? a.Name : a.RuneName;

            // The (at most) two runes that carry the formula most (the most relevant, with the most energy), named in the order
            // they stand in the formula.
            var byWeight = new List<int>();
            for (int i = 0; i < rs.slots.Count; i++) byWeight.Add(i);
            byWeight.Sort((x, y) =>
            {
                float wx = ev.slots[x].relevance * (1f + ev.slots[x].energy), wy = ev.slots[y].relevance * (1f + ev.slots[y].energy);
                int cmp = wy.CompareTo(wx);
                return cmp != 0 ? cmp : x.CompareTo(y);
            });
            var chosen = new List<int>();
            var names = new List<string>();
            foreach (int i in byWeight)
            {
                string name = BaseName(c, rs.slots[i].rune);
                if (names.Contains(name)) continue;
                names.Add(name);
                chosen.Add(i);
                if (names.Count == 2) break;
            }
            chosen.Sort();
            names.Clear();
            foreach (int i in chosen) names.Add(BaseName(c, rs.slots[i].rune));
            return string.Join("-", names) + " " + rs.effect.Name;
        }

        // ================================================================== results
        /// <summary>The shared fields of a synthesis' ingredients, in order.</summary>
        public static List<SynthesisIngredient> Ingredients(ResolvedSynthesis rs)
        {
            var list = new List<SynthesisIngredient>();
            foreach (ResolvedSlot sl in rs.slots)
                list.Add(new SynthesisIngredient
                {
                    runeInstanceId = sl.rune.instanceId, runeId = sl.rune.runeId, rank = sl.rune.rank,
                    conceptId = sl.concept != null ? sl.concept.id : "", energy = sl.energy,
                });
            return list;
        }

        /// <summary>The spell a successful or flawed synthesis makes (design spec 11; the runes stay in the soul and anchor it, decision D3).</summary>
        public static SpellRecord BuildSpell(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev, SynthesisStats stats, string name,
            string synthesisId, SynthesisTier tier, float score, List<SynthesisFlawDef> flaws, List<string> sideEffects, long utc)
        {
            var spell = new SpellRecord
            {
                spellId = synthesisId, name = name, intentionId = rs.intention.id, effectId = rs.effect.id, archetypeId = ev.archetypeId ?? "",
                rank = rs.resultRank, tier = (int)tier, score = score, quality = stats.quality, power = stats.power, stability = stats.stability,
                energyCostMultiplier = stats.energyCostMultiplier, controlPenalty = stats.controlPenalty, complexity = ev.effectComplexity,
                investedEnergy = ev.totalEnergy, flawed = tier == SynthesisTier.Flawed, synthesisId = synthesisId, createdUtc = utc,
                ingredients = Ingredients(rs), sideEffects = new List<string>(sideEffects),
            };
            foreach (SynthesisFlawDef f in flaws) spell.flaws.Add(f.id);
            foreach (ResolvedSlot sl in rs.slots) if (!spell.anchors.Contains(sl.rune.instanceId)) spell.anchors.Add(sl.rune.instanceId);
            return spell;
        }

        /// <summary>
        /// The rune a rank advancement or a composite synthesis makes: seven runes of one rune become that rune one rank
        /// higher; any other combination becomes a composite rune with its own name, essence and slot cost.
        /// </summary>
        public static RuneInstance BuildRune(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev, SynthesisStats stats, string name,
            string synthesisId, string instanceId, float score, List<SynthesisFlawDef> flaws, long utc)
        {
            var rune = new RuneInstance
            {
                instanceId = instanceId, rank = rs.resultRank, quality = stats.quality, acquiredUtc = utc,
                origin = new RuneOrigin { kind = "synthesis", source = synthesisId, utc = utc },
                flawed = flaws.Count > 0,
            };
            foreach (SynthesisFlawDef f in flaws) rune.flaws.Add(f.id);
            rune.history.Add(synthesisId);

            if (rs.pure)
            {
                RuneInstance source = rs.slots[0].rune;
                rune.runeId = source.runeId;
                if (source.IsComposite)
                {
                    rune.composite = source.composite.Clone();
                    rune.composite.synthesisId = synthesisId;
                    rune.composite.score = score;
                }
            }
            else
            {
                OutcomeArchetype a = string.IsNullOrEmpty(ev.archetypeId) ? null : c.Archetype(ev.archetypeId);
                rune.runeId = a != null && !string.IsNullOrEmpty(a.resultRuneId) ? a.resultRuneId : CompositeIds.Generic;
                rune.composite = BuildComposite(c, rs, ev, a, name, synthesisId, score);
            }
            float start = c.Synthesis.resultEnergyShare * c.Balance.MaxEnergy(rune.rank, rune.quality) * Clamp01(score / 100f);
            rune.energy = Math.Max(0f, Math.Min(start, c.Balance.MaxEnergy(rune.rank, rune.quality)));
            return rune;
        }

        private static CompositeProfile BuildComposite(RuneCatalog c, ResolvedSynthesis rs, SynthesisEvaluation ev, OutcomeArchetype a, string name,
            string synthesisId, float score)
        {
            var p = new CompositeProfile
            {
                active = true, name = name, archetypeId = ev.archetypeId ?? "", intentionId = rs.intention.id, effectId = rs.effect.id,
                slotCost = Math.Max(1, rs.resultSlotCost), ingredients = Ingredients(rs), synthesisId = synthesisId, score = score,
                complexity = Clamp01(0.5f * ev.averageComplexity + 0.5f * ev.effectComplexity),
            };

            // Its alignments are those of the runes it is made of.
            foreach (ResolvedSlot sl in rs.slots)
                foreach (string al in sl.alignments)
                    if (!p.alignments.Contains(al)) p.alignments.Add(al);

            // Its essence: the aspects of the concepts used (a rune that contributed little counts for half), saturating below 2,
            // weaker for a worse synthesis. Usable as the concept "@essence" of a later synthesis.
            var sum = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 0; i < rs.slots.Count; i++)
            {
                ConceptView concept = rs.slots[i].concept;
                if (concept?.aspects == null) continue;
                float weight = 0.5f + 0.5f * Clamp01(ev.slots[i].relevance);
                foreach (AspectWeight aw in concept.aspects)
                    if (aw != null && !string.IsNullOrEmpty(aw.aspect))
                        sum[aw.aspect] = (sum.TryGetValue(aw.aspect, out float have) ? have : 0f) + aw.weight * weight;
            }
            float scale = 0.5f + 0.5f * Clamp01(score / 100f);
            var aspects = new List<AspectWeight>();
            foreach (KeyValuePair<string, float> kv in sum)
            {
                float w = kv.Value <= 1f ? kv.Value : 1f + (1f - (float)Math.Exp(-(kv.Value - 1f)));
                w = (float)Math.Round(w * scale, 2);
                if (w >= 0.05f) aspects.Add(new AspectWeight(kv.Key, w));
            }
            aspects.Sort((x, y) =>
            {
                int cmp = y.weight.CompareTo(x.weight);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.aspect, y.aspect);
            });
            if (aspects.Count > 8) aspects.RemoveRange(8, aspects.Count - 8);
            p.aspects = aspects;

            p.description = a != null && !string.IsNullOrEmpty(a.resultProperties) ? a.resultProperties : Describe(c, rs, p);
            return p;
        }

        private static string Describe(RuneCatalog c, ResolvedSynthesis rs, CompositeProfile p)
        {
            var names = new List<string>();
            foreach (ResolvedSlot sl in rs.slots)
            {
                string n = BaseName(c, sl.rune);
                if (!names.Contains(n)) names.Add(n);
                if (names.Count == 3) break;
            }
            var traits = new List<string>();
            foreach (AspectWeight aw in p.aspects)
            {
                AspectDef def = c.Aspect(aw.aspect);
                traits.Add((def != null ? def.Name : aw.aspect).ToLowerInvariant());
                if (traits.Count == 2) break;
            }
            string made = "Made from " + string.Join(", ", names) + (rs.slots.Count > names.Count ? " and others" : "") + ".";
            return traits.Count > 0 ? made + " Its essence leans towards " + string.Join(" and ", traits) + "." : made;
        }

        // ================================================================== failure (design spec 13.3)
        /// <summary>
        /// The penalties of a failed synthesis (energy depletion, and rune destruction when the severity says so), applied to
        /// the soul. The runes are not used up: a failure only costs what the severity takes. Returns the energy lost; the runes
        /// destroyed are removed from the soul and listed in <paramref name="destroyed"/>.
        /// </summary>
        public static float ApplyFailure(RuneCatalog c, RuneSoulState s, ResolvedSynthesis rs, FailureSeverity severity, ref SynthesisRng rng,
            List<string> destroyed)
        {
            float lost = 0f;
            if (severity == null) return 0f;
            foreach (ResolvedSlot sl in rs.slots)
            {
                float take = Math.Min(sl.rune.energy, sl.energy * Clamp01(severity.energyLoss));
                sl.rune.energy = Math.Max(0f, sl.rune.energy - take);
                lost += take;
            }
            if (severity.destroysRunes && rs.slots.Count > 0)
            {
                float chance = Clamp01(c.Synthesis.destroyChancePerRune);
                var doomed = new List<ResolvedSlot>();
                foreach (ResolvedSlot sl in rs.slots) if (rng.NextFloat() < chance) doomed.Add(sl);
                if (doomed.Count == 0) doomed.Add(rs.slots[rng.NextInt(rs.slots.Count)]); // a catastrophe always costs a rune
                foreach (ResolvedSlot sl in doomed)
                {
                    destroyed.Add(sl.runeName);
                    s.runes.Remove(sl.rune);
                }
            }
            return lost;
        }

        // ================================================================== helpers
        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
