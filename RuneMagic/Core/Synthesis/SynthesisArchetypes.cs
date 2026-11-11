using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>How a formula resembles an archetype (design spec 18: component contribution matters).</summary>
    public sealed class ArchetypeMatch
    {
        public OutcomeArchetype archetype;
        /// <summary>Share of the ideal runes the formula has (0-1).</summary>
        public float overlap;
        /// <summary>How close the counts and order are to the ideal (0-1; 1 = the ideal formula).</summary>
        public float fidelity;
        public float countFit;
        public float orderFit;
        /// <summary>The stage each slot was matched to (-1 = an extra rune the archetype does not have).</summary>
        public int[] stageOf = new int[0];
        public int extras;
    }

    /// <summary>
    /// Matching a formula to the named reference formulas (design spec 18, 19). An archetype is not a recipe: it never
    /// decides whether a formula works (the general evaluation does); it only names the result and, when the formula
    /// resembles it, supplies the ideal energy ratios and a fidelity factor (the ideal counts and order score best, near
    /// variants score less).
    /// </summary>
    public static class SynthesisArchetypes
    {
        /// <summary>The archetype a resolved synthesis matches best (null when none is recognised).</summary>
        public static OutcomeArchetype Find(RuneCatalog c, ResolvedSynthesis rs) => Match(c, rs)?.archetype;

        /// <summary>The best matching archetype with its fidelity, or null.</summary>
        public static ArchetypeMatch Match(RuneCatalog c, ResolvedSynthesis rs)
        {
            if (c == null || rs == null || rs.pure || rs.effect == null || rs.intention == null) return null;
            ArchetypeMatch best = null;
            foreach (OutcomeArchetype a in c.Archetypes)
            {
                if (a == null || a.effect != rs.effect.id || a.intention != rs.intention.id) continue;
                if (a.mode == ArchetypeMode.Spell && rs.mode != SynthesisMode.Spell) continue;
                if (a.mode == ArchetypeMode.Rune && rs.mode == SynthesisMode.Spell) continue;
                ArchetypeMatch m = MatchOne(a, rs);
                if (m == null) continue;
                if (best == null || m.overlap > best.overlap + 1e-6f ||
                    (Math.Abs(m.overlap - best.overlap) <= 1e-6f && (m.extras < best.extras || (m.extras == best.extras && string.CompareOrdinal(a.id, best.archetype.id) < 0))))
                    best = m;
            }
            return best;
        }

        private static ArchetypeMatch MatchOne(OutcomeArchetype a, ResolvedSynthesis rs)
        {
            int n = rs.slots.Count;
            var m = new ArchetypeMatch { archetype = a, stageOf = new int[n] };
            for (int i = 0; i < n; i++) m.stageOf[i] = -1;
            int total = a.TotalCount;
            if (total <= 0) return null;

            var actual = new int[a.stages.Count];
            int matched = 0;
            for (int st = 0; st < a.stages.Count; st++)
            {
                ArchetypeStage stage = a.stages[st];
                if (stage == null) continue;
                int want = Math.Max(1, stage.count);
                for (int i = 0; i < n && actual[st] < want; i++)
                {
                    if (m.stageOf[i] >= 0) continue;
                    ResolvedSlot sl = rs.slots[i];
                    if (sl.runeKey != stage.runeId) continue;
                    if (!string.IsNullOrEmpty(stage.conceptId) && (sl.concept == null || sl.concept.id != stage.conceptId)) continue;
                    m.stageOf[i] = st;
                    actual[st]++;
                    matched++;
                }
            }
            m.overlap = matched / (float)total;
            if (m.overlap + 1e-6f < a.matchThreshold) return null;
            m.extras = n - matched;

            // The counts: how far from the ideal, runes the archetype does not have included.
            float distance = m.extras;
            for (int st = 0; st < a.stages.Count; st++)
                if (a.stages[st] != null) distance += Math.Abs(actual[st] - Math.Max(1, a.stages[st].count));
            m.countFit = Clamp01(1f - distance / (Math.Max(0.1f, a.countTolerance) * total));

            // The order: the stages the runes were matched to, by place, against the ideal sequence of stages.
            m.orderFit = 1f;
            if (a.orderMatters)
            {
                int pairs = 0, inverted = 0;
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        int si = m.stageOf[i], sj = m.stageOf[j];
                        if (si < 0 || sj < 0 || si == sj) continue;
                        pairs++;
                        if (si > sj) inverted++;
                    }
                if (pairs > 0) m.orderFit = 1f - inverted / (float)pairs;
            }
            m.fidelity = m.countFit * m.orderFit;
            return m;
        }

        /// <summary>
        /// The ideal formula of an archetype as a request for runes of the given instance ids (one per stage rune, in order):
        /// for samples, tests and tools. <paramref name="energyOf"/> gives the energy of each slot (null = 0).
        /// </summary>
        public static SynthesisRequest IdealRequest(OutcomeArchetype a, IList<string> runeInstanceIds, Func<int, float> energyOf = null,
            SynthesisMode mode = SynthesisMode.Spell)
        {
            var r = new SynthesisRequest { mode = mode, intentionId = a.intention, effectId = a.effect };
            int k = 0;
            foreach (ArchetypeStage st in a.stages)
                for (int i = 0; i < Math.Max(1, st.count); i++, k++)
                    r.slots.Add(new SynthesisSlot(k < runeInstanceIds.Count ? runeInstanceIds[k] : "", st.conceptId, energyOf != null ? energyOf(k) : 0f));
            return r;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
