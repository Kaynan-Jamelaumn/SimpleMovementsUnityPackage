using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>Reserved ids of composite runes.</summary>
    public static class CompositeIds
    {
        /// <summary>The rune id of a composite rune that no archetype named.</summary>
        public const string Generic = "composite";
        /// <summary>The concept id of a composite rune's own nature (usable in a later synthesis).</summary>
        public const string Essence = "@essence";
    }

    /// <summary>A concept as a synthesis sees it: a concept of the database, or a composite rune's essence.</summary>
    public sealed class ConceptView
    {
        public string id = "";
        public string name = "";
        public List<AspectWeight> aspects = new List<AspectWeight>();
        public float complexity;
        public RuneRarity rarity;
        public bool essence;
    }

    /// <summary>One slot of a synthesis resolved against the soul: the rune, its definition, the concept, the energy.</summary>
    public sealed class ResolvedSlot
    {
        public int index;
        public RuneInstance rune;
        public RuneDef def;
        /// <summary>What makes two runes "the same rune": the rune id, or a generated composite's own name.</summary>
        public string runeKey = "";
        public string runeName = "";
        public ConceptView concept;
        public float energy;
        /// <summary>The share of the rune's concepts the player knows (1 for a composite).</summary>
        public float understanding;
        public IReadOnlyList<string> alignments = Array.Empty<string>();
        public bool primordial;
    }

    /// <summary>A request checked against the soul: everything the evaluation needs, nothing it must look up again.</summary>
    public sealed class ResolvedSynthesis
    {
        public SynthesisMode mode;
        public SynthesisRequest request;
        public IntentionDef intention;
        public DesiredEffectDef effect;
        /// <summary>Rank advancement of runes that are all the same rune: no formula, only energy.</summary>
        public bool pure;
        public List<ResolvedSlot> slots = new List<ResolvedSlot>();
        public int resultRank;
        public int consumedSlotCost;
        public int resultSlotCost;

        public int Count => slots.Count;
    }

    /// <summary>
    /// The first step of a synthesis (design spec 13.4): checks that the request can be attempted at all. Everything it
    /// refuses costs the player nothing - no energy, no soul damage, no risk: the runes are not theirs, the concept is not
    /// discovered, the soul's condition forbids it, there is no room for the result. What is incoherent is <i>not</i> refused
    /// here: a formula that cannot work is evaluated and fails with its penalty.
    /// </summary>
    public static class SynthesisResolver
    {
        /// <summary>The key that says whether two runes are the same rune (for pure rank advancement).</summary>
        public static string RuneKey(RuneInstance r)
        {
            if (r == null) return "";
            return r.IsComposite && r.runeId == CompositeIds.Generic ? "composite#" + (r.composite.name ?? "") : r.runeId;
        }

        /// <summary>The concept of a slot: a concept of the database, or the essence of a composite rune.</summary>
        public static ConceptView ConceptOf(RuneCatalog c, RuneInstance rune, string conceptId)
        {
            if (rune == null || string.IsNullOrEmpty(conceptId)) return null;
            if (conceptId == CompositeIds.Essence)
            {
                if (!rune.IsComposite) return null;
                return new ConceptView
                {
                    id = CompositeIds.Essence, name = string.IsNullOrEmpty(rune.composite.name) ? "Essence" : rune.composite.name + " (essence)",
                    aspects = rune.composite.aspects, complexity = rune.composite.complexity, rarity = RuneRarity.Rare, essence = true,
                };
            }
            ConceptDef k = c.Concept(conceptId);
            if (k == null) return null;
            return new ConceptView { id = k.id, name = k.Name, aspects = k.aspects ?? new List<AspectWeight>(), complexity = k.complexity, rarity = k.rarity };
        }

        /// <summary>
        /// Checks <paramref name="req"/> against the soul <paramref name="s"/>. On success <paramref name="rs"/> is the resolved
        /// synthesis; otherwise the refusal and a message for the player.
        /// </summary>
        public static SynthesisRefusal Resolve(RuneCatalog c, RuneSoulState s, SynthesisContext ctx, SynthesisRequest req,
            out ResolvedSynthesis rs, out string message)
        {
            rs = null;
            message = "";
            SynthesisBalance b = c?.Synthesis;
            if (c == null || s == null || b == null || req == null || req.slots == null)
            {
                message = "Nothing to synthesize.";
                return SynthesisRefusal.NoRequest;
            }
            ctx = ctx ?? new SynthesisContext();
            int n = req.slots.Count;
            SynthesisMode mode = req.mode;

            // ---- how many runes
            if (mode == SynthesisMode.RankAdvancement)
            {
                int need = c.Balance.runesPerRankAdvancement;
                if (n != need) { message = $"A rank advancement combines exactly {need} runes of the same rank (you chose {n})."; return SynthesisRefusal.WrongRuneCount; }
            }
            else
            {
                int min = mode == SynthesisMode.CompositeRune ? b.minRunesComposite : b.minRunes;
                if (n < min) { message = $"Choose at least {min} rune{(min == 1 ? "" : "s")}."; return SynthesisRefusal.TooFewRunes; }
                if (n > b.maxRunes) { message = $"A synthesis may combine at most {b.maxRunes} runes."; return SynthesisRefusal.TooManyRunes; }
            }

            // ---- the runes
            var rs0 = new ResolvedSynthesis { mode = mode, request = req };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                SynthesisSlot slot = req.slots[i];
                RuneInstance rune = slot != null ? s.Rune(slot.runeInstanceId) : null;
                if (rune == null) { message = $"Rune {i + 1} is no longer in your soul."; return SynthesisRefusal.RuneNotOwned; }
                if (!seen.Add(rune.instanceId)) { message = $"{RuneRules.DisplayName(c, rune)} is chosen twice: each rune can take one place."; return SynthesisRefusal.DuplicateRune; }
                float e = slot.energy;
                if (float.IsNaN(e) || float.IsInfinity(e) || e < 0f) { message = $"The energy given by {RuneRules.DisplayName(c, rune)} is not a valid amount."; return SynthesisRefusal.InvalidEnergy; }
                if (e > rune.energy + 0.001f)
                {
                    message = $"{RuneRules.DisplayName(c, rune)} holds only {rune.energy:N0} energy (you gave {e:N0}).";
                    return SynthesisRefusal.InvalidEnergy;
                }
                RuneDef def = c.Rune(rune.runeId);
                var rsl = new ResolvedSlot
                {
                    index = i, rune = rune, def = def, runeKey = RuneKey(rune), runeName = RuneRules.DisplayName(c, rune), energy = e,
                    alignments = RuneRules.AlignmentsOf(c, rune), primordial = def != null && def.kind == RuneKind.Primordial,
                };
                rsl.understanding = Understanding(c, s, rune, def);
                rs0.slots.Add(rsl);
            }

            // ---- anchored runes (decision D3)
            bool consumes = mode != SynthesisMode.Spell;
            foreach (ResolvedSlot sl in rs0.slots)
            {
                if (!SpellRules.IsAnchored(s, sl.rune.instanceId)) continue;
                if (consumes)
                {
                    message = $"{sl.runeName} anchors a spell: dismiss the spell before using the rune up.";
                    return SynthesisRefusal.RuneAnchored;
                }
                if (!b.runesMayAnchorSeveralSpells)
                {
                    message = $"{sl.runeName} already anchors a spell.";
                    return SynthesisRefusal.RuneAnchored;
                }
            }

            // ---- rank advancement: one rank, room to rise, pure or mixed
            bool allSame = true;
            foreach (ResolvedSlot sl in rs0.slots) if (sl.runeKey != rs0.slots[0].runeKey) { allSame = false; break; }
            if (mode == SynthesisMode.RankAdvancement)
            {
                int rank = rs0.slots[0].rune.rank;
                foreach (ResolvedSlot sl in rs0.slots)
                    if (sl.rune.rank != rank) { message = "A rank advancement needs runes of one rank."; return SynthesisRefusal.MixedRanks; }
                int cap = allSame && rs0.slots[0].def != null ? Math.Min(rs0.slots[0].def.maxRank, b.maxRuneRank) : b.maxRuneRank;
                if (rank + 1 > cap) { message = $"These runes cannot rise above rank {cap}."; return SynthesisRefusal.RankTooHigh; }
                rs0.resultRank = rank + 1;
                rs0.pure = allSame;
            }
            else
            {
                int max = 1;
                foreach (ResolvedSlot sl in rs0.slots) max = Math.Max(max, sl.rune.rank);
                rs0.resultRank = max;
            }

            // ---- the formula: intention, effect and a discovered concept per rune (not for a pure advancement)
            if (!rs0.pure)
            {
                rs0.intention = c.Intention(req.intentionId);
                if (rs0.intention == null) { message = "Choose an intention."; return SynthesisRefusal.UnknownIntention; }
                rs0.effect = c.Effect(req.effectId);
                if (rs0.effect == null) { message = "Choose a desired effect."; return SynthesisRefusal.UnknownEffect; }
                foreach (ResolvedSlot sl in rs0.slots)
                {
                    string conceptId = req.slots[sl.index].conceptId;
                    if (string.IsNullOrEmpty(conceptId)) { message = $"Choose a concept for {sl.runeName}."; return SynthesisRefusal.ConceptRequired; }
                    ConceptView view = ConceptOf(c, sl.rune, conceptId);
                    if (view == null)
                    {
                        message = conceptId == CompositeIds.Essence ? $"{sl.runeName} has no essence to draw on." : $"Unknown concept '{conceptId}'.";
                        return SynthesisRefusal.UnknownConcept;
                    }
                    if (!view.essence)
                    {
                        RuneDef owner = c.RuneOfConcept(conceptId);
                        if (owner == null || owner.id != sl.rune.runeId) { message = $"{view.name} is not a concept of {sl.runeName}."; return SynthesisRefusal.ConceptNotOfRune; }
                        if (!RuneRules.Knows(s, sl.rune.runeId, conceptId)) { message = $"You have not discovered {view.name} yet."; return SynthesisRefusal.ConceptNotKnown; }
                        ConceptDef def = c.Concept(conceptId);
                        if (def != null && def.minimumRankToUse > sl.rune.rank)
                        {
                            message = $"{view.name} needs a rune of rank {def.minimumRankToUse} (yours is {sl.rune.rank}).";
                            return SynthesisRefusal.ConceptRankTooLow;
                        }
                    }
                    sl.concept = view;
                }
            }
            else
            {
                // A pure advancement has no formula: every rune contributes only itself.
                foreach (ResolvedSlot sl in rs0.slots)
                    sl.concept = new ConceptView { id = "", name = sl.runeName, complexity = 0.3f, rarity = RuneRarity.Common };
            }

            // ---- the spellbook
            if (mode == SynthesisMode.Spell && s.spells.Count >= b.maxSpells)
            {
                message = $"Your spellbook is full ({b.maxSpells} spells): dismiss a spell first.";
                return SynthesisRefusal.SpellbookFull;
            }

            // ---- the soul's condition (design spec 14.4)
            float complexity = rs0.effect != null ? rs0.effect.complexity : 0.3f;
            if (!ctx.soul.AllowsSynthesis(n, complexity, out string why)) { message = why; return SynthesisRefusal.SoulRestriction; }

            // ---- room for the result (design spec 6.1: "available capacity for the resulting rune")
            if (consumes)
            {
                foreach (ResolvedSlot sl in rs0.slots) rs0.consumedSlotCost += RuneRules.SlotCost(c, sl.rune);
                rs0.resultSlotCost = ResultSlotCost(c, rs0);
                int used = RuneRules.UsedSlots(c, s);
                if (used - rs0.consumedSlotCost + rs0.resultSlotCost > ctx.capacity)
                {
                    message = $"Your soul has no room for the result ({rs0.resultSlotCost} slot{(rs0.resultSlotCost == 1 ? "" : "s")}): free a slot or increase your capacity.";
                    return SynthesisRefusal.NoCapacity;
                }
            }

            rs = rs0;
            return SynthesisRefusal.None;
        }

        /// <summary>Soul slots the rune a synthesis makes would occupy (an archetype's, a pure advancement's own, else one).</summary>
        public static int ResultSlotCost(RuneCatalog c, ResolvedSynthesis rs)
        {
            if (rs.pure) return RuneRules.SlotCost(c, rs.slots[0].rune);
            OutcomeArchetype a = SynthesisArchetypes.Find(c, rs);
            return a != null ? Math.Max(1, a.resultSlotCost) : 1;
        }

        private static float Understanding(RuneCatalog c, RuneSoulState s, RuneInstance rune, RuneDef def)
        {
            if (rune.IsComposite || def == null || def.concepts == null || def.concepts.Count == 0) return 1f;
            int known = RuneRules.KnownConcepts(s, rune.runeId).Count;
            return Math.Min(1f, known / (float)def.concepts.Count);
        }
    }
}
