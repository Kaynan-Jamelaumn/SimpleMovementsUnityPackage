using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// Decides which spells use the Spelling System and how hard it is for them (design spec 16.5, directive 9). Pure and
    /// deterministic: the same spell, soul and seed give the same sequence parameters on every machine.
    /// </summary>
    public static class SpellingGenerator
    {
        // ------------------------------------------------------------------ the rules that apply to a spell
        /// <summary>
        /// The rules of the database that apply to a spell merged field by field, the most specific first: the archetype it was
        /// recognised as, its desired effect, its form. A field no rule sets stays at "inherit" (the generated value).
        /// </summary>
        public static SpellingRule RuleFor(RuneCatalog c, SpellRecord sp)
        {
            var merged = new SpellingRule { windowMultiplier = 1f };
            if (c == null || sp == null) return merged;
            SpellFormDef form = c.FormOfEffect(sp.effectId);
            RuleOf(c, SpellingScope.Archetype, sp.archetypeId, merged, true);
            RuleOf(c, SpellingScope.Effect, sp.effectId, merged, false);
            if (form != null) RuleOf(c, SpellingScope.Form, form.id, merged, false);
            return merged;
        }

        private static void RuleOf(RuneCatalog c, SpellingScope scope, string id, SpellingRule into, bool first)
        {
            if (string.IsNullOrEmpty(id)) return;
            SpellingRule r = null;
            foreach (SpellingRule x in c.SpellingRules)
                if (x != null && x.scope == scope && x.id == id) { r = x; break; }
            if (r == null) return;
            if (into.use == SpellingUse.Inherit) into.use = r.use;
            if (into.letters < 0 && r.letters > 0) into.letters = r.letters;
            if (into.lettersDelta == 0) into.lettersDelta = r.lettersDelta;
            if (into.keyWindow < 0f && r.keyWindow > 0f) into.keyWindow = r.keyWindow;
            if ((into.keyWindows == null || into.keyWindows.Count == 0) && r.keyWindows != null && r.keyWindows.Count > 0) into.keyWindows = new List<float>(r.keyWindows);
            if (Math.Abs(into.windowMultiplier - 1f) < 1e-6f) into.windowMultiplier = r.windowMultiplier;
            if (into.totalLimit < 0f && r.totalLimit > 0f) into.totalLimit = r.totalLimit;
            if (into.rhythm == SpellingUse.Inherit) into.rhythm = r.rhythm;
            if (into.rhythmTempo < 0f) into.rhythmTempo = r.rhythmTempo;
            if (into.rhythmTolerance < 0f) into.rhythmTolerance = r.rhythmTolerance;
            if (into.failureModel == SpellingFailureChoice.Inherit) into.failureModel = r.failureModel;
            if (into.maxMistakes < 0) into.maxMistakes = r.maxMistakes;
            if (into.speedBonusThreshold < 0f) into.speedBonusThreshold = r.speedBonusThreshold;
            if (into.damageMode == SpellingDamageChoice.Inherit) into.damageMode = r.damageMode;
            if (into.damageAmount < 0f) into.damageAmount = r.damageAmount;
        }

        /// <summary>
        /// Does this spell use the Spelling System? None: never. All: always. Selected: when a rule marks it (and none overrules it).
        /// A server decides this; a player has no say (design spec 16.5).
        /// </summary>
        public static bool Uses(RuneCatalog c, SpellRecord sp)
        {
            SpellingBalance b = c?.Spelling;
            if (b == null || sp == null) return false;
            switch (b.mode)
            {
                case SpellingMode.None: return false;
                case SpellingMode.All: return true;
                default: return RuleFor(c, sp).use == SpellingUse.Yes;
            }
        }

        // ------------------------------------------------------------------ the parameters of a sequence
        /// <summary>
        /// The sequence parameters of a spell: letters scaled by rank, power and complexity; windows (a different one per key); the
        /// total limit; rhythm; the failure model; what mistakes cost. The soul's condition adds letters, shortens the windows and
        /// raises the penalties; the player's concept knowledge and the spell's quality forgive. <paramref name="seed"/> varies the
        /// windows from key to key. <paramref name="poolSize"/> (0 = unknown) limits the letters to the keys that may be asked for.
        /// </summary>
        public static SpellingParams Resolve(RuneCatalog c, CompiledSpell cs, SpellRecord sp, in SpellingContext ctx, int seed, int poolSize = 0)
        {
            SpellingBalance b = c.Spelling;
            SpellingRule r = RuleFor(c, sp);
            SoulRestrictions soul = ctx.soul;
            var p = new SpellingParams { uses = Uses(c, sp) };

            float forgiveness = Clamp01(Clamp01(ctx.knowledge) * b.knowledgeForgiveness + Clamp01(ctx.quality / 100f) * b.qualityForgiveness);
            p.forgiveness = forgiveness;

            // ---- letters (scaled by rank, power and concept complexity)
            float reference = Math.Max(1f, c.Spells.referencePower);
            float letters = r.letters >= 1
                ? r.letters
                : b.baseLetters + b.lettersPerRank * (Math.Max(1, cs.rank) - 1) + b.lettersPerPower * Math.Max(0f, cs.power / reference - 1f) +
                  b.lettersPerComplexity * Clamp01(cs.complexity);
            letters += r.lettersDelta + soul.spellingExtraLetters;
            int count = Math.Max(b.minLetters, Math.Min(b.maxLetters, (int)Math.Round(letters, MidpointRounding.AwayFromZero)));
            if (poolSize > 0) count = Math.Min(count, poolSize);
            p.letters = Math.Max(1, count);

            // ---- windows (a different one for each key)
            float baseWindow = r.keyWindow > 0f ? r.keyWindow : Math.Max(b.minKeyWindow, b.baseKeyWindow - b.windowPerComplexity * Clamp01(cs.complexity));
            float multiplier = Math.Max(0.1f, r.windowMultiplier) * Math.Max(0.1f, soul.spellingWindowMultiplier == 0f ? 1f : soul.spellingWindowMultiplier) *
                               (1f + forgiveness * b.forgivenessWindow);
            bool rhythm = r.rhythm == SpellingUse.Yes;
            p.rhythm = rhythm;
            p.tempo = r.rhythmTempo > 0f ? r.rhythmTempo : b.rhythmTempo;
            p.tolerance = r.rhythmTolerance > 0f ? r.rhythmTolerance : b.rhythmTolerance;
            var rng = new SynthesisRng(seed);
            for (int i = 0; i < p.letters; i++)
            {
                float w;
                bool explicitWindow = r.keyWindows != null && i < r.keyWindows.Count && r.keyWindows[i] > 0f;
                if (explicitWindow) w = r.keyWindows[i];
                else
                {
                    float jitter = 1f + b.windowVariation * (rng.NextFloat() * 2f - 1f);
                    w = baseWindow * jitter;
                    if (i == 0) w += b.firstKeyExtra;
                }
                w *= multiplier;
                if (rhythm) w = Math.Max(w, p.tempo + p.tolerance + 0.1f); // a beat must fit in the window
                p.windows.Add(Math.Max(0.2f, w));
            }
            float sum = 0f;
            foreach (float w in p.windows) sum += w;
            p.totalLimit = r.totalLimit > 0f ? r.totalLimit : sum * b.totalLimitFactor;
            if (rhythm) p.totalLimit = Math.Max(p.totalLimit, p.BeatOf(p.letters - 1) + p.tolerance + 0.2f);

            // ---- the failure model and what mistakes cost
            p.model = r.failureModel == SpellingFailureChoice.Strict ? SpellingFailureModel.Strict
                : r.failureModel == SpellingFailureChoice.Lenient ? SpellingFailureModel.Lenient : b.failureModel;
            int generatedMax = 1 + (int)Math.Floor(p.letters * b.mistakesPerLetterShare);
            p.maxMistakes = p.model == SpellingFailureModel.Strict ? 0
                : (r.maxMistakes >= 0 ? r.maxMistakes : generatedMax) + (int)Math.Round(forgiveness * b.forgivenessMistakes, MidpointRounding.AwayFromZero);

            SpellingDamageChoice dm = r.damageMode;
            p.damageMode = dm == SpellingDamageChoice.Inherit ? b.damageMode : (SpellingDamageMode)(dm - 1);
            p.damageAmount = r.damageAmount >= 0f ? r.damageAmount : b.damageAmount;
            p.damageScaling = b.damageScaling;
            p.damageKind = b.damageKind ?? "";

            float penalty = soul.spellingPenaltyMultiplier <= 0f ? 1f : soul.spellingPenaltyMultiplier;
            p.powerPerMistake = Math.Min(0.5f, b.powerPerMistake * penalty);
            p.castTimePerMistake = b.castTimePerMistake * penalty;
            p.stabilityPerMistake = b.stabilityPerMistake * penalty;

            // ---- scoring
            foreach (SpellingWeight w in b.weights) if (w != null) p.weights.Add(new SpellingWeight(w.term, w.weight));
            p.speedBonusThreshold = r.speedBonusThreshold > 0f ? r.speedBonusThreshold : b.speedBonusThreshold;
            p.speedBonusMaxMistakes = b.speedBonusMaxMistakes;
            p.speedBonusPower = b.speedBonusPower;
            p.speedBonusCost = b.speedBonusCost;
            p.speedBonusStrain = b.speedBonusStrain;
            p.speedBonusStability = b.speedBonusStability;
            p.powerSwing = b.powerSwing;

            p.energyLossOnFailure = b.energyLossOnFailure;
            p.failureLockSeconds = b.failureLockSeconds;
            p.failureDamageFactor = b.failureDamageFactor;
            return p;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
