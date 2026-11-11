using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// The synthesis engine (design spec 6.1, 11-13, 18-19): checks a request, evaluates the formula, and applies the
    /// outcome to the soul. Pure logic over <see cref="RuneSoulState"/> and the compiled <see cref="RuneCatalog"/>: no
    /// UnityEngine objects, no input, no UI, so the server runs it, tests run it, and the Phase 6 UI only asks it
    /// questions. The player never sees a recipe: the preview states requirements and a fuzzy estimate, the result states
    /// what happened, and the optimum stays research.
    /// <para>The order of events: <b>resolve</b> (refusals cost nothing: unowned runes, undiscovered concepts, no room,
    /// the soul's condition) → <b>evaluate</b> → <b>insufficient energy</b> (cannot proceed, no penalty) → one roll for
    /// faltering → the <b>outcome</b> by tier: a Perfect, Successful or Flawed result consumes the energy given and makes the
    /// spell or rune; a failure (including every fundamentally incoherent formula) costs energy and possibly runes by severity.
    /// Soul Damage is returned as a plan for the runtime to apply, never applied here.</para>
    /// </summary>
    public static class SynthesisEngine
    {
        // ================================================================== evaluation (tools and tests)
        /// <summary>
        /// The exact evaluation of a request (null when it is refused): for developer tools and tests. Players never see the
        /// exact score: they get <see cref="Preview"/>.
        /// </summary>
        public static SynthesisEvaluation Evaluate(RuneCatalog c, RuneSoulState s, SynthesisContext ctx, SynthesisRequest req,
            out SynthesisRefusal refusal, out string message)
        {
            ctx = ctx ?? new SynthesisContext();
            refusal = SynthesisResolver.Resolve(c, s, ctx, req, out ResolvedSynthesis rs, out message);
            return refusal == SynthesisRefusal.None ? SynthesisEvaluator.Evaluate(c, rs, ctx) : null;
        }

        /// <summary>The score a failure's severity is judged by: an incoherent formula's score is cut (the worse the formula, the harsher the penalty) and kept below the Flawed threshold.</summary>
        public static float EffectiveScore(RuneCatalog c, SynthesisEvaluation ev, float score)
        {
            if (!ev.Incoherent) return score;
            float cap = c.Balance.synthesis.flawed - 0.01f;
            return Math.Max(0f, Math.Min(score * c.Synthesis.incoherentScoreFactor, cap));
        }

        // ================================================================== preview (design spec 11.1 step 7)
        /// <summary>
        /// What the player may know before confirming: whether it can proceed, the least energy each rune needs, what each
        /// holds and was given, the soul's requirement, a fuzzy estimate of the score and its tiers, the risk, whether runes
        /// could be destroyed, the Soul Damage range and whether the soul could collapse. Never the exact score, the ideal
        /// energy, the ideal order or why a formula is incoherent. The same request always previews the same.
        /// </summary>
        public static SynthesisPreview Preview(RuneCatalog c, RuneSoulState s, SynthesisContext ctx, SynthesisRequest req)
        {
            var p = new SynthesisPreview { mode = req != null ? req.mode : SynthesisMode.Spell };
            ctx = ctx ?? new SynthesisContext();
            p.soulCondition = ctx.soul.conditionName ?? "";
            SynthesisRefusal refusal = SynthesisResolver.Resolve(c, s, ctx, req, out ResolvedSynthesis rs, out string message);
            if (refusal != SynthesisRefusal.None)
            {
                p.refusal = refusal;
                p.message = message;
                return p;
            }

            SynthesisBalance b = c.Synthesis;
            SynthesisEvaluation ev = SynthesisEvaluator.Evaluate(c, rs, ctx);
            p.output = rs.mode == SynthesisMode.Spell ? SynthesisOutput.Spell : SynthesisOutput.Rune;
            p.resultRank = rs.resultRank;
            p.riskLevel = ev.riskLevel;
            p.failureChance = ev.failureChance;
            p.destructionPossible = ev.risk >= SynthesisEvaluator.DestructionRisk(c);

            // ---- what each rune needs, holds and was given
            foreach (SlotEvaluation se in ev.slots)
            {
                bool excessive = se.state == EnergyState.Excessive;
                EnergyState shown = se.state == EnergyState.Insufficient || excessive || b.previewShowsEnergyStates ? se.state : EnergyState.Optimal;
                float min = (float)Math.Ceiling(se.minEnergy);
                p.requirements.Add(new SlotRequirement
                {
                    index = se.index, runeInstanceId = se.runeInstanceId, runeName = se.runeName, conceptName = se.conceptName,
                    minEnergy = min, available = se.available, allocated = se.energy, shortfall = Math.Max(0f, min - se.energy), shown = shown,
                });
                if (excessive) p.warnings.Add($"{se.runeName} is given far more energy than its place can hold: it will interfere.");
                if (se.available + 0.001f < se.minEnergy)
                    p.warnings.Add($"{se.runeName} holds less energy than its place needs ({se.available:N0} of at least {min:N0}): it cannot take part as it is.");
            }
            if (ev.AnyInsufficient)
            {
                p.refusal = SynthesisRefusal.InsufficientEnergy;
                p.message = InsufficientMessage(ev);
                return p;
            }

            // ---- an estimate, never the score: a range with a fixed, request-dependent offset so its middle is not the answer
            float effective = EffectiveScore(c, ev, ev.score);
            float fuzz = b.previewFuzz;
            float offset = (Hash01(req) * 2f - 1f) * 0.6f * fuzz;
            float low = Clamp(effective + offset - fuzz, 0f, 100f);
            float high = Clamp(effective + offset + fuzz, 0f, 100f);
            if (ev.Incoherent) high = Math.Min(high, c.Balance.synthesis.flawed - 0.01f);
            p.estimateLow = (float)Math.Round(low);
            p.estimateHigh = (float)(ev.Incoherent ? Math.Floor(high) : Math.Round(high)); // a doomed formula never shows the Flawed threshold
            p.tierLow = ev.Incoherent ? SynthesisTier.CompleteFailure : c.Balance.TierOf(low);
            p.tierHigh = ev.Incoherent ? SynthesisTier.CompleteFailure : c.Balance.TierOf(high);

            // ---- Soul Damage: the best plausible outcome to the worst
            SynthesisTier worst = WorstTier(ev, p.tierLow);
            p.soulDamageLow = (float)Math.Round(SynthesisOutcomes.ScaledTotal(c, ctx, SynthesisOutcomes.SoulDamagePlan(c, rs, ev, p.tierHigh)), 1);
            p.soulDamageHigh = (float)Math.Round(SynthesisOutcomes.ScaledTotal(c, ctx, SynthesisOutcomes.SoulDamagePlan(c, rs, ev, worst)), 1);
            p.soulCouldCollapse = ctx.integrity > 0f && p.soulDamageHigh >= ctx.integrity - 0.01f;
            if (p.soulCouldCollapse) p.warnings.Add("A bad outcome could collapse your soul.");
            if (p.destructionPossible) p.warnings.Add("Runes could be destroyed if this goes badly.");
            if (ctx.soul.synthesisFailureChance > 0f) p.warnings.Add($"Your soul is {(string.IsNullOrEmpty(ctx.soul.conditionName) ? "strained" : ctx.soul.conditionName.ToLowerInvariant())}: the synthesis is more likely to falter.");
            if (rs.mode != SynthesisMode.Spell)
            {
                foreach (ResolvedSlot sl in rs.slots)
                    if (sl.rune.energy - sl.energy > 0.5f)
                    {
                        p.warnings.Add("Runes used up in this synthesis lose the energy they still hold beyond what you give.");
                        break;
                    }
            }
            if (b.refuseWhenSoulCouldCollapse && p.soulCouldCollapse)
            {
                p.refusal = SynthesisRefusal.SoulWouldCollapse;
                p.message = "This synthesis could collapse your soul: recover first, or use fewer or simpler runes.";
            }
            return p;
        }

        // ================================================================== execute
        /// <summary>
        /// Performs a synthesis on <paramref name="s"/>. <paramref name="req"/>.seed is the only source of chance (the server
        /// chooses it). A refused request changes nothing; a performed one changes the runes, the spells, the log and the
        /// counters, and returns the Soul Damage to apply in <see cref="SynthesisResult.soulDamage"/>.
        /// </summary>
        public static SynthesisResult Execute(RuneCatalog c, RuneSoulState s, SynthesisContext ctx, SynthesisRequest req, long utcTicks = 0)
        {
            var result = new SynthesisResult();
            ctx = ctx ?? new SynthesisContext();
            if (s != null) s.EnsureLists();
            SynthesisRefusal refusal = SynthesisResolver.Resolve(c, s, ctx, req, out ResolvedSynthesis rs, out string message);
            if (refusal != SynthesisRefusal.None)
            {
                result.refusal = refusal;
                result.message = message;
                return result;
            }
            SynthesisBalance b = c.Synthesis;
            SynthesisEvaluation ev = SynthesisEvaluator.Evaluate(c, rs, ctx);
            result.evaluation = ev;

            // ---- insufficient energy: cannot proceed, nothing is spent (design spec 13.4)
            if (ev.AnyInsufficient)
            {
                result.refusal = SynthesisRefusal.InsufficientEnergy;
                result.message = InsufficientMessage(ev);
                return result;
            }
            if (b.refuseWhenSoulCouldCollapse && ctx.integrity > 0f &&
                SynthesisOutcomes.ScaledTotal(c, ctx, SynthesisOutcomes.SoulDamagePlan(c, rs, ev, WorstTier(ev, ev.tier))) >= ctx.integrity - 0.01f)
            {
                result.refusal = SynthesisRefusal.SoulWouldCollapse;
                result.message = "This synthesis could collapse your soul: recover first, or use fewer or simpler runes.";
                return result;
            }

            // ---- from here the synthesis is performed
            result.performed = true;
            var rng = new SynthesisRng(req.seed);
            string synthesisId = "syn_" + rng.NextId(12);
            result.synthesisId = synthesisId;

            // One roll for faltering (design spec 14.4: a strained soul fails more often); both draws are always taken so that
            // what follows does not depend on whether the magic faltered.
            float falterRoll = rng.NextFloat(), falterDraw = rng.NextFloat();
            float score = ev.score;
            if (falterRoll < ev.failureChance)
            {
                result.faltered = true;
                score = Math.Max(0f, score - (b.falterMin + (b.falterMax - b.falterMin) * falterDraw));
            }
            score = (float)Math.Round(score, 2);
            SynthesisTier tier = ev.Incoherent ? SynthesisTier.CompleteFailure : c.Balance.TierOf(score);
            result.score = score;
            result.tier = tier;
            result.incoherent = ev.Incoherent;
            result.soulDamage = SynthesisOutcomes.SoulDamagePlan(c, rs, ev, tier);

            if (tier == SynthesisTier.CompleteFailure)
                Fail(c, s, rs, ev, ref rng, result, score);
            else
                Succeed(c, s, ctx, rs, ev, ref rng, result, synthesisId, tier, score, req, utcTicks);

            Record(c, s, rs, req, result, utcTicks);
            return result;
        }

        // ------------------------------------------------------------------ a failure (design spec 13.3)
        private static void Fail(RuneCatalog c, RuneSoulState s, ResolvedSynthesis rs, SynthesisEvaluation ev, ref SynthesisRng rng, SynthesisResult result, float score)
        {
            FailureSeverity severity = c.Balance.SeverityOf(EffectiveScore(c, ev, score), ev.risk);
            result.severity = severity != null ? severity.name : "";
            var destroyed = new List<string>();
            result.energyLost = SynthesisOutcomes.ApplyFailure(c, s, rs, severity, ref rng, destroyed);
            result.destroyedRunes = destroyed;
            result.runesDestroyed = destroyed.Count > 0;
            if (result.runesDestroyed) result.lostSpells = SpellRules.RemoveBroken(s);
            s.synthesesFailed++;

            string text = "The synthesis failed" + (string.IsNullOrEmpty(result.severity) ? "" : $" ({result.severity.ToLowerInvariant()})") + ". " + Hint(ev);
            if (result.faltered) text += " The magic faltered at the last moment.";
            if (result.energyLost > 0.5f) text += $" {result.energyLost:N0} energy was lost.";
            if (result.runesDestroyed) text += " Destroyed: " + string.Join(", ", destroyed) + ".";
            if (result.lostSpells.Count > 0) text += " Lost with them: " + string.Join(", ", result.lostSpells) + ".";
            result.message = text;
        }

        // ------------------------------------------------------------------ a Perfect, Successful or Flawed result
        private static void Succeed(RuneCatalog c, RuneSoulState s, SynthesisContext ctx, ResolvedSynthesis rs, SynthesisEvaluation ev, ref SynthesisRng rng,
            SynthesisResult result, string synthesisId, SynthesisTier tier, float score, SynthesisRequest req, long utc)
        {
            SynthesisStats stats = SynthesisEvaluator.StatsAt(c, ev, ctx, score);
            var flaws = new List<SynthesisFlawDef>();
            var sideEffects = new List<string>();
            if (tier == SynthesisTier.Flawed)
            {
                flaws = SynthesisOutcomes.PickFlaws(c, ev, score, ref rng, out float severity);
                SynthesisOutcomes.ApplyFlaws(ref stats, flaws, severity, sideEffects, out _);
            }
            foreach (SynthesisFlawDef f in flaws) result.flaws.Add(f.id);
            result.stats = stats;
            string name = SynthesisOutcomes.NameOf(c, rs, ev, req);
            result.name = name;

            // The energy given is used up in every mode.
            foreach (ResolvedSlot sl in rs.slots)
            {
                sl.rune.energy = Math.Max(0f, sl.rune.energy - sl.energy);
                result.energyConsumed += sl.energy;
            }

            if (rs.mode == SynthesisMode.Spell)
            {
                SpellRecord spell = SynthesisOutcomes.BuildSpell(c, rs, ev, stats, name, UniqueSpellId(s, synthesisId), tier, score, flaws, sideEffects, utc);
                s.spells.Add(spell);
                foreach (ResolvedSlot sl in rs.slots) AddHistory(sl.rune, synthesisId);
                result.output = SynthesisOutput.Spell;
                result.spell = spell;
            }
            else
            {
                string instanceId = "r_" + rng.NextId(12);
                while (s.Rune(instanceId) != null) instanceId = "r_" + rng.NextId(12);
                RuneInstance rune = SynthesisOutcomes.BuildRune(c, rs, ev, stats, name, synthesisId, instanceId, score, flaws, utc);
                foreach (ResolvedSlot sl in rs.slots) s.runes.Remove(sl.rune); // used up: their slots are free again
                s.runes.Add(rune);
                result.output = SynthesisOutput.Rune;
                result.rune = rune;
                result.stats.rank = rune.rank;
            }

            if (tier == SynthesisTier.Perfect) s.synthesesPerfect++;
            else if (tier == SynthesisTier.Successful) s.synthesesSuccessful++;
            else s.synthesesFlawed++;

            string what = rs.mode == SynthesisMode.Spell ? $"the spell {name}" :
                rs.pure ? $"{name} (rank {result.rune.rank})" : $"the rune {name}";
            string lead = tier == SynthesisTier.Perfect ? "Perfect synthesis!" : tier == SynthesisTier.Successful ? "The synthesis succeeded." : "The synthesis worked, but it is flawed.";
            string text = $"{lead} You made {what}.";
            if (flaws.Count > 0)
            {
                var names = new List<string>();
                foreach (SynthesisFlawDef f in flaws) names.Add(f.Name);
                text += " Flaws: " + string.Join(", ", names) + ". " + Hint(ev);
            }
            if (result.faltered) text += " The magic faltered, and the result is weaker for it.";
            result.message = text;
        }

        // ------------------------------------------------------------------ the research log and the history of the runes
        private static void Record(RuneCatalog c, RuneSoulState s, ResolvedSynthesis rs, SynthesisRequest req, SynthesisResult result, long utc)
        {
            int keep = c.Synthesis.logLength;
            if (keep <= 0) return;
            string outcome = result.tier == SynthesisTier.Perfect ? "perfect" : result.tier == SynthesisTier.Successful ? "successful" :
                result.tier == SynthesisTier.Flawed ? "flawed" : "failed" + (string.IsNullOrEmpty(result.severity) ? "" : ":" + result.severity.ToLowerInvariant());
            s.synthesisLog.Add(new SynthesisLogEntry
            {
                id = result.synthesisId, utc = utc, mode = (int)rs.mode, intentionId = req.intentionId ?? "", effectId = req.effectId ?? "",
                runeCount = rs.slots.Count, score = result.score, outcome = outcome, resultName = result.name ?? "",
                note = result.evaluation != null ? Hint(result.evaluation) : "",
            });
            while (s.synthesisLog.Count > keep) s.synthesisLog.RemoveAt(0);
        }

        private static void AddHistory(RuneInstance rune, string synthesisId)
        {
            rune.history.Add(synthesisId);
            while (rune.history.Count > 50) rune.history.RemoveAt(0);
        }

        private static string UniqueSpellId(RuneSoulState s, string synthesisId)
        {
            string id = synthesisId;
            for (int i = 2; SpellRules.Find(s, id) != null; i++) id = synthesisId + "_" + i;
            return id;
        }

        // ================================================================== messages
        private static string InsufficientMessage(SynthesisEvaluation ev)
        {
            var parts = new List<string>();
            foreach (SlotEvaluation se in ev.slots)
            {
                if (se.state != EnergyState.Insufficient) continue;
                string holds = se.available + 0.001f < se.minEnergy ? $", it holds {se.available:N0}" : "";
                parts.Add($"{se.runeName} needs at least {Math.Ceiling(se.minEnergy):N0} energy for its place (you gave {se.energy:N0}{holds})");
            }
            string shown = string.Join("; ", parts.GetRange(0, Math.Min(3, parts.Count)));
            if (parts.Count > 3) shown += $"; and {parts.Count - 3} more";
            return "Not enough energy: " + shown + ". Nothing was spent.";
        }

        /// <summary>
        /// A hint for the player's research, from what held the result back: the reason a formula is fundamentally incoherent,
        /// else the weakest part of the evaluation in general words. It never states numbers or the ideal.
        /// </summary>
        public static string Hint(SynthesisEvaluation ev)
        {
            if (ev == null) return "";
            if (ev.Incoherent) return ev.incoherence[0];
            FactorScores f = ev.factors;
            float lowest = 1f;
            string hint = "";
            Consider(ref lowest, ref hint, f.coverage, "The runes did not provide enough of what the effect needs.");
            Consider(ref lowest, ref hint, f.compatibility, "The concepts worked against each other.");
            Consider(ref lowest, ref hint, f.intention, "The purpose and the desired effect did not fit well.");
            Consider(ref lowest, ref hint, f.order, "The runes were in a poor order.");
            Consider(ref lowest, ref hint, f.energy, ev.interference > 0.3f ? "A rune was given too much energy." : "The energy was badly balanced between the runes.");
            Consider(ref lowest, ref hint, f.execution, "The runes were too weak, or the concepts beyond what they can hold.");
            // A formula that resembles a known one but is not in its proportions (too many or too few of a kind).
            if (!string.IsNullOrEmpty(ev.archetypeId))
                Consider(ref lowest, ref hint, ev.archetypeFidelity, "The runes were not in the right proportions to one another.");
            return lowest < 0.95f ? hint : "";
        }

        private static void Consider(ref float lowest, ref string hint, float value, string text)
        {
            if (value < lowest - 1e-6f) { lowest = value; hint = text; }
        }

        // ================================================================== helpers
        private static SynthesisTier WorstTier(SynthesisEvaluation ev, SynthesisTier floorTier) =>
            ev.Incoherent || ev.failureChance >= 0.05f || floorTier == SynthesisTier.CompleteFailure ? SynthesisTier.CompleteFailure : floorTier;

        /// <summary>A number in [0, 1] from the content of a request (FNV-1a): the same request always gives the same number.</summary>
        private static float Hash01(SynthesisRequest req)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Mix(h, (int)req.mode);
                h = Mix(h, req.intentionId);
                h = Mix(h, req.effectId);
                foreach (SynthesisSlot sl in req.slots)
                {
                    if (sl == null) continue;
                    h = Mix(h, sl.runeInstanceId);
                    h = Mix(h, sl.conceptId);
                    h = Mix(h, (int)Math.Round(sl.energy));
                }
                return (h % 10000u) / 9999f;
            }
        }

        private static uint Mix(uint h, int v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { h = (h ^ (uint)((v >> (8 * i)) & 0xFF)) * 16777619u; }
                return h;
            }
        }

        private static uint Mix(uint h, string v)
        {
            unchecked
            {
                if (v != null) foreach (char ch in v) h = (h ^ ch) * 16777619u;
                return (h ^ 0xFFu) * 16777619u;
            }
        }

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;
    }
}
