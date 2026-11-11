#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace RuneMagic.Tests
{
    /// <summary>
    /// A soul to run syntheses in, for the Edit Mode tests: runes absorbed with every concept discovered, and formulas written
    /// as letters (W wind pressure, A ash combustion, V vibration resonance, F fire temperature, w water flow). Energies are
    /// filled from the engine's own evaluation, as multiples of the ideal, so a test can say "the ideal energy" or "twice it"
    /// without knowing the numbers.
    /// </summary>
    internal sealed class SynthLab
    {
        public RuneCatalog c;
        public RuneSoulState s = new RuneSoulState();
        public SynthesisContext ctx;
        public readonly List<string> ids = new List<string>();
        private int next;

        private static readonly Dictionary<char, string[]> Letters = new Dictionary<char, string[]>
        {
            { 'W', new[] { "wind", "wind.pressure" } },
            { 'A', new[] { "ash", "ash.combustion" } },
            { 'V', new[] { "vibration", "vibration.resonance" } },
            { 'F', new[] { "fire", "fire.temperature" } },
            { 'w', new[] { "water", "water.flow" } },
        };

        public SynthLab(RuneCatalog catalog = null, int capacity = 99)
        {
            c = catalog ?? RuneDefaults.Catalog();
            ctx = new SynthesisContext { capacity = capacity };
        }

        public static RuneCatalog Custom(Action<RuneMagicData> edit)
        {
            RuneMagicData d = RuneDefaults.Create();
            edit(d);
            return RuneCatalog.Compile(d);
        }

        /// <summary>Absorbs a rune holding <paramref name="energy"/> (up to its maximum) with every concept of its definition discovered.</summary>
        public RuneInstance Add(string runeId, int rank = 1, float quality = 60f, float energy = 1e9f, bool learnAll = true)
        {
            string id = "r" + next++;
            RuneInstance r = RuneRules.Absorb(c, s, new RuneDrop { runeId = runeId, rank = rank, quality = quality, energy = energy },
                new RuneOrigin { kind = "test" }, id, 1L);
            if (learnAll)
                foreach (ConceptDef k in c.Rune(runeId).concepts) RuneRules.Learn(s, runeId, k.id);
            ids.Add(id);
            return r;
        }

        /// <summary>Absorbs the runes of a pattern like "WWWAVVV" and returns their instance ids in that order.</summary>
        public List<string> Pattern(string pattern, int rank = 1, float quality = 60f, float energy = 1e9f)
        {
            var made = new List<string>();
            foreach (char ch in pattern)
                made.Add(Add(Letters[ch][0], rank, quality, energy).instanceId);
            return made;
        }

        /// <summary>A request for the runes <paramref name="runeIds"/>, with the concepts the pattern's letters name. Energies are 0 until <see cref="Fill"/>.</summary>
        public SynthesisRequest Request(string pattern, List<string> runeIds, string intention = "attack", string effect = "offense.explosion",
            SynthesisMode mode = SynthesisMode.Spell, int seed = 1)
        {
            var req = new SynthesisRequest { mode = mode, intentionId = intention, effectId = effect, seed = seed };
            for (int i = 0; i < pattern.Length; i++)
                req.slots.Add(new SynthesisSlot(runeIds[i], Letters[pattern[i]][1], 0f));
            return req;
        }

        /// <summary>Gives every slot the ideal energy times <paramref name="mult"/> (and times <paramref name="per"/>[i] when given).</summary>
        public SynthesisRequest Fill(SynthesisRequest req, float mult = 1f, float[] per = null, bool equal = false)
        {
            var probe = req.Clone();
            foreach (SynthesisSlot sl in probe.slots) sl.energy = 0.001f;
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(c, s, ctx, probe, out SynthesisRefusal refusal, out string msg);
            if (ev == null) return req;
            for (int i = 0; i < req.slots.Count; i++)
                req.slots[i].energy = (equal ? ev.slots[0].idealEnergy : ev.slots[i].idealEnergy) * mult * (per != null ? per[i] : 1f);
            return req;
        }

        /// <summary>A ready request: pattern absorbed, energies at the ideal times <paramref name="mult"/>.</summary>
        public SynthesisRequest Ready(string pattern, float mult = 1f, string intention = "attack", string effect = "offense.explosion",
            SynthesisMode mode = SynthesisMode.Spell, int rank = 1, float quality = 60f, int seed = 1)
        {
            List<string> made = Pattern(pattern, rank, quality);
            return Fill(Request(pattern, made, intention, effect, mode, seed), mult);
        }

        public SynthesisEvaluation Evaluate(SynthesisRequest req)
        {
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(c, s, ctx, req, out SynthesisRefusal refusal, out string msg);
            if (ev == null) throw new InvalidOperationException($"refused: {refusal}: {msg}");
            return ev;
        }

        public SynthesisResult Execute(SynthesisRequest req) => SynthesisEngine.Execute(c, s, ctx, req, 100L);
        public SynthesisPreview Preview(SynthesisRequest req) => SynthesisEngine.Preview(c, s, ctx, req);

        public void SoulFailureChance(float chance)
        {
            SoulRestrictions r = SoulRestrictions.None;
            r.synthesisFailureChance = chance;
            ctx.soul = r;
        }

        public string Json() => s.ToJson();
    }
}
#endif
