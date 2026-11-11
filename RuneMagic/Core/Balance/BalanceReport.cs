using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneMagic
{
    /// <summary>One number of the balance report: what it is, its value, and what it means for the player.</summary>
    public sealed class BalanceLine
    {
        public string section = "";
        public string label = "";
        public float value;
        public string unit = "";
        /// <summary>What the number says in play ("a normal kill fills a rank 1 rune a seventh").</summary>
        public string meaning = "";
    }

    /// <summary>
    /// The balance of the whole Rune Magic System read off the configurable tables (design spec 21: the loop must connect combat progression,
    /// rune acquisition, magical knowledge, synthesis and soul management; spec 23: power must come from knowledge and risk, not from
    /// collecting). Pure: it runs the same rules the server runs on the database's current content, so a designer can change a table and
    /// see what it does to the pace of the game (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Balance Report; the tests keep the built-in
    /// numbers inside the ranges that make the loop work).
    /// </summary>
    public static class BalanceReport
    {
        public static List<BalanceLine> Build(RuneCatalog c)
        {
            var lines = new List<BalanceLine>();
            RuneBalance b = c.Balance;
            void Add(string section, string label, float value, string unit, string meaning = "") =>
                lines.Add(new BalanceLine { section = section, label = label, value = value, unit = unit, meaning = meaning });

            // ---------------------------------------------------------------- progression and capacity
            foreach (int level in new[] { 1, 10, 25, 50, 100 })
                Add("Capacity", $"Soul slots at level {level}", CapacityRules.Capacity(b, level, new SoulState()), "slots");
            int levelFor7 = 0;
            for (int l = 1; l <= 500; l++)
                if (CapacityRules.Capacity(b, l, new SoulState()) >= 7) { levelFor7 = l; break; }
            Add("Capacity", "Level at which the spec's 7-rune formula fits", levelFor7, "level",
                levelFor7 <= 1 ? "from the start: the spec's worked example needs no free slot beyond the first seven" : "below this a Mage must combine runes to free slots");

            // ---------------------------------------------------------------- energy and kills
            for (int rank = 1; rank <= 5; rank++)
                Add("Energy", $"Maximum energy of a rank {rank} rune", b.MaxEnergy(rank), "energy");
            float normal0 = DropRules.EnergyReward(c, RuneDropContext.For("wind_beast", 0, MonsterRarity.Normal));
            float normal3 = DropRules.EnergyReward(c, RuneDropContext.For("wind_beast", 3, MonsterRarity.Normal));
            float boss0 = DropRules.EnergyReward(c, RuneDropContext.For("wind_beast", 0, MonsterRarity.Boss));
            Add("Energy", "Energy of a normal kill (tier 0)", normal0, "energy", "the spec's 150");
            Add("Energy", "Energy of a normal kill (tier 3)", normal3, "energy");
            Add("Energy", "Energy of a boss kill (tier 0)", boss0, "energy");
            if (normal0 > 0f)
                Add("Energy", "Normal kills to fill a rank 1 rune", b.MaxEnergy(1) / normal0, "kills", "how long a first rune takes to charge");

            // ---------------------------------------------------------------- drops
            RuneAcquisitionBalance a = b.acquisition;
            RuneDropProfile wind = c.DropProfile("wind_beast");
            if (wind != null)
            {
                float chance0 = DropRules.DropChance(a, wind, RuneDropContext.For("wind_beast", 0, MonsterRarity.Normal));
                float chance3 = DropRules.DropChance(a, wind, RuneDropContext.For("wind_beast", 3, MonsterRarity.Normal));
                if (chance0 > 0f) Add("Drops", "Normal kills per rune drop (tier 0)", 1f / chance0, "kills");
                if (chance3 > 0f) Add("Drops", "Normal kills per rune drop (tier 3)", 1f / chance3, "kills");
                Add("Drops", "Boss always drops", a.bossAlwaysDrops ? 1f : 0f, "bool");
            }

            // ---------------------------------------------------------------- synthesis against the soul
            foreach (OutcomeArchetype arch in c.Archetypes)
            {
                if (arch == null || arch.stages.Count == 0) continue;
                ArchetypeRun run = RunOf(c, arch, false);
                if (run == null) continue;
                Add("Synthesis", $"{arch.Name}: runes", run.runes, "runes");
                Add("Synthesis", $"{arch.Name}: ideal energy in all", run.energy, "energy", normal0 > 0f ? $"{run.energy / normal0:0.#} normal kills of energy" : "");
                Add("Synthesis", $"{arch.Name}: Soul Damage of a perfect synthesis", run.damagePercent, "% of the soul");
                ArchetypeRun bad = RunOf(c, arch, true);
                if (bad != null) Add("Synthesis", $"{arch.Name}: Soul Damage of a botched attempt (reversed order)", bad.damagePercent, "% of the soul");
            }

            // ---------------------------------------------------------------- casting: energy and strain
            SpellBalance sb = c.Spells;
            foreach (int rank in new[] { 1, 3, 5 })
            {
                foreach (float complexity in new[] { 0.2f, 0.5f, 0.9f })
                {
                    var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = rank, complexity = complexity, power = 100f * (float)Math.Pow(1.3f, rank - 1), stability = 90f, anchors = new List<string>() };
                    CompiledSpell cs = SpellCompiler.Compile(c, sp);
                    CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
                    string name = $"rank {rank}, complexity {complexity:0.#}";
                    Add("Casting", $"Rune Energy per cast ({name})", plan.energyCost, "energy", $"{b.MaxEnergy(rank) / Math.Max(1f, plan.energyCost):0.#} casts from one full rank {rank} rune");
                    Add("Casting", $"Soul Strain per cast ({name})", plan.strainScaled, "integrity", $"{(plan.strainScaled > 0f ? (b.maxSoulIntegrity * 0.2f) / plan.strainScaled : 0f):0.#} casts from full to Strained");
                }
            }

            // ---------------------------------------------------------------- recovery
            SoulRecoveryBalance r = b.soulRecovery;
            RecoveryContext meditate = RecoveryContext.Default;
            meditate.meditating = true;
            meditate.meditationSeconds = 600f;
            float meditation = RecoveryRules.PerMinute(r, meditate, out _);
            RecoveryContext sleep = RecoveryContext.Default;
            sleep.sleeping = true;
            float rest = RecoveryRules.PerMinute(r, sleep, out _);
            Add("Recovery", "Integrity per minute meditating", meditation, "integrity/min");
            Add("Recovery", "Integrity per minute resting", rest, "integrity/min");
            if (meditation > 0f) Add("Recovery", "Minutes of meditation to climb out of Strained (20 points)", b.maxSoulIntegrity * 0.2f / meditation, "min");
            if (rest > 0f) Add("Recovery", "Minutes of rest from 50% to full", b.maxSoulIntegrity * 0.5f / rest, "min");

            // ---------------------------------------------------------------- the Spelling System
            SpellingBalance sp2 = c.Spelling;
            foreach (int rank in new[] { 1, 3, 5 })
            {
                var spell = new SpellRecord { spellId = "x", effectId = "offense.explosion", rank = rank, complexity = 0.6f, power = 100f * (float)Math.Pow(1.3f, rank - 1), stability = 90f, anchors = new List<string>() };
                SpellingParams p = SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, spell), spell, SpellingContext.Plain, 1);
                Add("Spelling", $"Letters (rank {rank}, complexity 0.6)", p.letters, "keys");
                Add("Spelling", $"Time limit (rank {rank}, complexity 0.6)", p.EffectiveLimit, "s", $"{p.maxMistakes} mistakes allowed when lenient");
            }
            Add("Spelling", "Spelling rules (by form, effect or archetype)", c.SpellingRules.Count, "rules", "a spell no rule marks casts without spelling in Selected mode");

            // ---------------------------------------------------------------- discovery
            var unreachable = new List<ConceptDef>();
            var fragile = new List<ConceptDef>();
            DiscoveryCoverage.Find(c, unreachable, fragile);
            Add("Discovery", "Concepts no source can teach", unreachable.Count, "concepts", "must be 0");
            Add("Discovery", "Concepts only one source teaches", fragile.Count, "concepts", "a wrong answer burns that source for the concept: keep these few");
            for (int i = 0; i < c.Discovery.tiers.Count; i++)
            {
                DiscoveryTierDef t = c.Discovery.tiers[i];
                Add("Discovery", $"Band {i} ({t.Name}): glyphs to trace", t.minigameNodes, "glyphs", $"then {t.steps} riddle step(s); {t.attempts} try(ies) at the minigame per source and concept");
            }
            return lines;
        }

        private sealed class ArchetypeRun
        {
            public int runes;
            public float energy;
            public float damagePercent;
        }

        // Runs an archetype's ideal formula on a fresh soul: how many runes, how much energy, and how much of the soul the attempt costs.
        private static ArchetypeRun RunOf(RuneCatalog c, OutcomeArchetype arch, bool reversed)
        {
            RuneBalance b = c.Balance;
            var s = new RuneSoulState();
            float max = b.maxSoulIntegrity;
            SoulRules.Normalize(b, s.soul, max);
            var ids = new List<string>();
            foreach (ArchetypeStage st in arch.stages)
            {
                RuneDef def = c.Rune(st.runeId);
                if (def == null) return null;
                for (int i = 0; i < Math.Max(1, st.count); i++)
                {
                    RuneInstance r = RuneRules.Absorb(c, s, new RuneDrop { runeId = def.id, rank = Math.Max(1, def.minRank), quality = 60f, energy = 1e9f },
                        new RuneOrigin { kind = "balance" }, null, 1L);
                    if (r == null) return null;
                    ids.Add(r.instanceId);
                }
                RuneRules.Learn(s, st.runeId, st.conceptId);
            }
            SynthesisRequest req = SynthesisArchetypes.IdealRequest(arch, ids, null, SynthesisMode.Spell);
            if (req == null) return null;
            var ctx = new SynthesisContext { capacity = 99, soul = SoulRestrictions.None, integrity = max, maxIntegrity = max };
            foreach (SynthesisSlot sl in req.slots) sl.energy = 0.001f;
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(c, s, ctx, req, out _, out _);
            if (ev == null) return null;
            float total = 0f;
            for (int i = 0; i < req.slots.Count; i++)
            {
                req.slots[i].energy = Math.Min(ev.slots[i].idealEnergy, s.Rune(req.slots[i].runeInstanceId).energy);
                total += req.slots[i].energy;
            }
            if (reversed) req.slots.Reverse();
            req.seed = 3;
            SynthesisResult result = SynthesisEngine.Execute(c, s, ctx, req, 1L);
            if (!result.performed) return null;
            var soul = new SoulState { initialized = true, integrity = max };
            foreach (SoulDamageEntry d in result.soulDamage)
                SoulRules.Damage(b, soul, max, d.amount, d.kind);
            return new ArchetypeRun { runes = req.slots.Count, energy = total, damagePercent = (max - soul.integrity) / max * 100f };
        }

        /// <summary>The report as text (the editor menu prints it).</summary>
        public static string ToText(IList<BalanceLine> lines)
        {
            var sb = new StringBuilder();
            string section = null;
            foreach (BalanceLine l in lines)
            {
                if (l.section != section) { section = l.section; sb.Append('\n').Append(section.ToUpperInvariant()).Append('\n'); }
                sb.Append("  ").Append(l.label).Append(": ").Append(l.value.ToString("0.##", CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(l.unit)) sb.Append(' ').Append(l.unit);
                if (!string.IsNullOrEmpty(l.meaning)) sb.Append("  — ").Append(l.meaning);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>The first line of a section with this label (tests and tools).</summary>
        public static BalanceLine Find(IList<BalanceLine> lines, string section, string labelStart)
        {
            foreach (BalanceLine l in lines)
                if (l.section == section && l.label.StartsWith(labelStart, StringComparison.Ordinal)) return l;
            return null;
        }
    }
}
