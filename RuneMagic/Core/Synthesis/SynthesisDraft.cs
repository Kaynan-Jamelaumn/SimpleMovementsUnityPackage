using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>The nine steps of the synthesis flow (design spec 11.1).</summary>
    public enum SynthesisStep : byte
    {
        /// <summary>1. Select the intention.</summary>
        Intention = 0,
        /// <summary>2. Select the desired effect.</summary>
        Effect = 1,
        /// <summary>3. Select the input runes.</summary>
        Runes = 2,
        /// <summary>4. Select one concept from each rune.</summary>
        Concepts = 3,
        /// <summary>5. Arrange the order of the runes.</summary>
        Order = 4,
        /// <summary>6. Allocate the energy of each rune.</summary>
        Energy = 5,
        /// <summary>7. Review the estimated requirements.</summary>
        Review = 6,
        /// <summary>8. Confirm.</summary>
        Confirm = 7,
        /// <summary>9. The result.</summary>
        Result = 8,
    }

    /// <summary>What the draft reads: the data, the runes of the soul (the server's, or the owner's replica) and what the soul allows.</summary>
    public sealed class SynthesisEnv
    {
        public readonly RuneCatalog c;
        public readonly RuneSoulState s;
        public readonly SynthesisContext ctx;

        public SynthesisEnv(RuneCatalog catalog, RuneSoulState state, SynthesisContext context)
        {
            c = catalog;
            s = state ?? new RuneSoulState();
            ctx = context ?? new SynthesisContext();
        }

        public SynthesisBalance Balance => c.Synthesis;
    }

    /// <summary>One rune in the draft, in order: the rune, the concept it contributes and the energy it gives.</summary>
    public sealed class DraftRune
    {
        public string runeInstanceId = "";
        public string conceptId = "";
        public float energy;
    }

    /// <summary>A rune of the soul as step 3 lists it (design spec 11.1: its energy, rank, concepts).</summary>
    public sealed class RuneChoice
    {
        public string instanceId = "";
        public string name = "";
        public int rank;
        public float quality;
        public float energy;
        public float maxEnergy;
        public int slotCost;
        public bool composite;
        public int knownConcepts;
        public int totalConcepts;
        public bool chosen;
        /// <summary>The names of the spells this rune anchors.</summary>
        public List<string> anchors = new List<string>();
        public bool usable = true;
        public string reason = "";
    }

    /// <summary>A concept a rune may contribute (step 4).</summary>
    public sealed class ConceptChoice
    {
        public string id = "";
        public string name = "";
        public string description = "";
        public int minimumRank = 1;
        public bool usable = true;
        public string reason = "";
    }

    /// <summary>A desired effect as step 2 lists it, with how well the chosen intention supports it.</summary>
    public sealed class EffectChoice
    {
        public DesiredEffectDef def;
        /// <summary>0 = not supported, 1 = fully supported (design spec 7).</summary>
        public float support;
        public bool available = true;
        public string reason = "";

        public string SupportLabel => support >= 0.999f ? "supported" : support > 0f ? "partly supported" : "not supported";
    }

    /// <summary>
    /// The player's synthesis in the making (design spec 11.1): the mode, the intention, the desired effect, and the runes in
    /// order with a concept and an energy each. Pure logic with no UI: which steps apply, what each offers, what completes it,
    /// and the request it builds. The window shows it; the engine judges the request it makes. Nothing here knows the ideal
    /// energy, the ideal order or what makes a formula coherent: only what the player may know.
    /// <para>A rank advancement of seven runes of one kind needs no formula, so its steps are only the runes and the energy;
    /// any other rank advancement starts with the runes and then asks for a formula.</para>
    /// </summary>
    public sealed class SynthesisDraft
    {
        public SynthesisMode mode = SynthesisMode.Spell;
        public string intentionId = "";
        public string effectId = "";
        public string name = "";
        public readonly List<DraftRune> runes = new List<DraftRune>();

        // ================================================================== the steps
        /// <summary>Is the formula (intention, effect, concepts) irrelevant: a rank advancement of runes that are all the same rune?</summary>
        public bool IsPure(SynthesisEnv e)
        {
            if (mode != SynthesisMode.RankAdvancement || runes.Count == 0) return false;
            string key = null;
            foreach (DraftRune d in runes)
            {
                RuneInstance r = e.s.Rune(d.runeInstanceId);
                if (r == null) return false;
                string k = SynthesisResolver.RuneKey(r);
                if (key == null) key = k;
                else if (k != key) return false;
            }
            return true;
        }

        /// <summary>The steps that apply now, in order (the last is always the result).</summary>
        public List<SynthesisStep> Steps(SynthesisEnv e)
        {
            var steps = new List<SynthesisStep>();
            bool formula = !(mode == SynthesisMode.RankAdvancement && (runes.Count == 0 || IsPure(e)));
            if (mode == SynthesisMode.RankAdvancement)
            {
                steps.Add(SynthesisStep.Runes);
                if (formula) { steps.Add(SynthesisStep.Intention); steps.Add(SynthesisStep.Effect); steps.Add(SynthesisStep.Concepts); steps.Add(SynthesisStep.Order); }
            }
            else
            {
                steps.Add(SynthesisStep.Intention);
                steps.Add(SynthesisStep.Effect);
                steps.Add(SynthesisStep.Runes);
                steps.Add(SynthesisStep.Concepts);
                steps.Add(SynthesisStep.Order);
            }
            steps.Add(SynthesisStep.Energy);
            steps.Add(SynthesisStep.Review);
            steps.Add(SynthesisStep.Confirm);
            steps.Add(SynthesisStep.Result);
            return steps;
        }

        /// <summary>The step after <paramref name="step"/> (the same one at the end).</summary>
        public SynthesisStep Next(SynthesisEnv e, SynthesisStep step)
        {
            List<SynthesisStep> steps = Steps(e);
            int i = steps.IndexOf(step);
            return i < 0 || i + 1 >= steps.Count ? step : steps[i + 1];
        }

        /// <summary>The step before <paramref name="step"/> (the same one at the start).</summary>
        public SynthesisStep Previous(SynthesisEnv e, SynthesisStep step)
        {
            List<SynthesisStep> steps = Steps(e);
            int i = steps.IndexOf(step);
            return i <= 0 ? step : steps[i - 1];
        }

        /// <summary>Is <paramref name="step"/> done? (The reason when it is not, for the player.)</summary>
        public bool Check(SynthesisEnv e, SynthesisStep step, out string why)
        {
            why = "";
            RuneCatalog c = e.c;
            switch (step)
            {
                case SynthesisStep.Intention:
                    if (c.Intention(intentionId) == null) { why = "Choose the purpose of the synthesis."; return false; }
                    return true;
                case SynthesisStep.Effect:
                {
                    DesiredEffectDef fx = c.Effect(effectId);
                    if (fx == null) { why = "Choose the effect you want."; return false; }
                    if (fx.complexity > e.ctx.soul.maxSynthesisComplexity + 1e-4f)
                    {
                        why = "That effect is too complex for your soul in its present condition.";
                        return false;
                    }
                    return true;
                }
                case SynthesisStep.Runes:
                    return CheckRunes(e, out why);
                case SynthesisStep.Concepts:
                    if (IsPure(e)) return true;
                    foreach (DraftRune d in runes)
                    {
                        RuneInstance r = e.s.Rune(d.runeInstanceId);
                        string label = r != null ? RuneRules.DisplayName(c, r) : "A rune";
                        if (string.IsNullOrEmpty(d.conceptId)) { why = $"Choose a concept for {label}."; return false; }
                        ConceptChoice ch = Concepts(e, d.runeInstanceId).Find(k => k.id == d.conceptId);
                        if (ch == null || !ch.usable) { why = ch != null ? ch.reason : $"{label} cannot contribute that concept."; return false; }
                    }
                    return true;
                case SynthesisStep.Order:
                    return CheckRunes(e, out why);
                case SynthesisStep.Energy:
                    foreach (DraftRune d in runes)
                    {
                        RuneInstance r = e.s.Rune(d.runeInstanceId);
                        if (r == null || float.IsNaN(d.energy) || d.energy < 0f || d.energy > r.energy + 0.001f)
                        {
                            why = "Give each rune an energy it holds.";
                            return false;
                        }
                    }
                    return true;
                default:
                    return true;
            }
        }

        private bool CheckRunes(SynthesisEnv e, out string why)
        {
            why = "";
            CountRule(e, out int min, out int max);
            if (runes.Count < min || runes.Count > max)
            {
                why = CountText(e);
                return false;
            }
            var seen = new HashSet<string>();
            int rank = 0;
            foreach (DraftRune d in runes)
            {
                RuneInstance r = e.s.Rune(d.runeInstanceId);
                if (r == null || !seen.Add(d.runeInstanceId)) { why = "A chosen rune is no longer available."; return false; }
                if (mode == SynthesisMode.RankAdvancement)
                {
                    if (rank == 0) rank = r.rank;
                    else if (r.rank != rank) { why = "A rank advancement needs runes of one rank."; return false; }
                }
            }
            return true;
        }

        /// <summary>The first step that is not done (the last before the result when all are).</summary>
        public SynthesisStep FirstIncomplete(SynthesisEnv e)
        {
            foreach (SynthesisStep s in Steps(e))
            {
                if (s == SynthesisStep.Review || s == SynthesisStep.Confirm || s == SynthesisStep.Result) return SynthesisStep.Review;
                if (!Check(e, s, out _)) return s;
            }
            return SynthesisStep.Review;
        }

        // ================================================================== intentions and effects (steps 1, 2)
        public static IReadOnlyList<IntentionDef> Intentions(RuneCatalog c) => c.Intentions;

        /// <summary>
        /// The desired effects, by category, with the support of the chosen intention. <paramref name="onlySupported"/> leaves out
        /// the ones the intention does not support at all. An effect the soul's condition is too weak for is listed but unavailable.
        /// </summary>
        public List<EffectChoice> Effects(SynthesisEnv e, bool onlySupported)
        {
            var list = new List<EffectChoice>();
            foreach (DesiredEffectDef fx in e.c.Effects)
            {
                if (fx == null) continue;
                float support = string.IsNullOrEmpty(intentionId) ? 1f : e.c.IntentionSupport(intentionId, fx.id);
                if (onlySupported && support <= 0f && fx.id != effectId) continue;
                var ch = new EffectChoice { def = fx, support = support };
                if (fx.complexity > e.ctx.soul.maxSynthesisComplexity + 1e-4f)
                {
                    ch.available = false;
                    ch.reason = "Too complex for your soul in its present condition.";
                }
                list.Add(ch);
            }
            list.Sort((a, b) =>
            {
                int cat = a.def.category.CompareTo(b.def.category);
                if (cat != 0) return cat;
                int sup = b.support.CompareTo(a.support);
                return sup != 0 ? sup : string.CompareOrdinal(a.def.Name, b.def.Name);
            });
            return list;
        }

        public static string CategoryName(EffectCategory category)
        {
            switch (category)
            {
                case EffectCategory.CreationTransformation: return "Creation & transformation";
                case EffectCategory.UtilityExploration: return "Utility & exploration";
                default: return category.ToString();
            }
        }

        public static string ModeName(SynthesisMode mode) =>
            mode == SynthesisMode.Spell ? "Spell" : mode == SynthesisMode.CompositeRune ? "New rune" : "Rank advancement";

        // ================================================================== runes and concepts (steps 3, 4)
        /// <summary>The fewest and most runes the mode and the soul's condition allow.</summary>
        public void CountRule(SynthesisEnv e, out int min, out int max)
        {
            SynthesisBalance b = e.Balance;
            if (mode == SynthesisMode.RankAdvancement)
            {
                min = max = e.c.Balance.runesPerRankAdvancement;
                return;
            }
            min = mode == SynthesisMode.CompositeRune ? b.minRunesComposite : b.minRunes;
            max = b.maxRunes;
            if (e.ctx.soul.maxSynthesisRunes > 0) max = Math.Min(max, e.ctx.soul.maxSynthesisRunes);
        }

        /// <summary>"Choose 2 to 12 runes", "Exactly 7 runes of the same rank".</summary>
        public string CountText(SynthesisEnv e)
        {
            CountRule(e, out int min, out int max);
            if (mode == SynthesisMode.RankAdvancement) return $"Choose exactly {min} runes of the same rank.";
            return min == max ? $"Choose {min} rune{(min == 1 ? "" : "s")}." : $"Choose {min} to {max} runes.";
        }

        /// <summary>Every rune of the soul, with whether it can be used in this synthesis and why not.</summary>
        public List<RuneChoice> RuneChoices(SynthesisEnv e)
        {
            RuneCatalog c = e.c;
            var list = new List<RuneChoice>();
            int firstRank = 0;
            if (mode == SynthesisMode.RankAdvancement && runes.Count > 0)
            {
                RuneInstance first = e.s.Rune(runes[0].runeInstanceId);
                if (first != null) firstRank = first.rank;
            }
            foreach (RuneInstance r in e.s.runes)
            {
                if (r == null) continue;
                RuneDef def = r.IsComposite ? null : c.Rune(r.runeId);
                var ch = new RuneChoice
                {
                    instanceId = r.instanceId, name = RuneRules.DisplayName(c, r), rank = r.rank, quality = r.quality, energy = r.energy,
                    maxEnergy = c.Balance.MaxEnergy(r.rank, r.quality), slotCost = RuneRules.SlotCost(c, r), composite = r.IsComposite,
                    chosen = IndexOf(r.instanceId) >= 0,
                    totalConcepts = def != null ? def.concepts.Count : 0,
                    knownConcepts = def != null ? RuneRules.KnownConcepts(e.s, r.runeId).Count : 0,
                };
                foreach (SpellRecord sp in SpellRules.AnchoredBy(e.s, r.instanceId)) ch.anchors.Add(sp.name);
                bool consumes = mode != SynthesisMode.Spell;
                if (ch.anchors.Count > 0 && consumes)
                    Refuse(ch, $"It anchors {(ch.anchors.Count == 1 ? "the spell " + ch.anchors[0] : ch.anchors.Count + " spells")}: dismiss {(ch.anchors.Count == 1 ? "it" : "them")} first.");
                else if (ch.anchors.Count > 0 && !e.Balance.runesMayAnchorSeveralSpells)
                    Refuse(ch, "It already anchors a spell.");
                else if (mode == SynthesisMode.RankAdvancement && r.rank >= e.Balance.maxRuneRank)
                    Refuse(ch, $"It cannot rise above rank {e.Balance.maxRuneRank}.");
                else if (mode == SynthesisMode.RankAdvancement && firstRank > 0 && !ch.chosen && r.rank != firstRank)
                    Refuse(ch, "A rank advancement needs runes of one rank.");
                list.Add(ch);
            }
            return list;
        }

        private static void Refuse(RuneChoice ch, string reason)
        {
            ch.usable = false;
            ch.reason = reason;
        }

        /// <summary>The concepts a rune may contribute: the ones the player has discovered (a composite rune: its essence).</summary>
        public List<ConceptChoice> Concepts(SynthesisEnv e, string runeInstanceId)
        {
            var list = new List<ConceptChoice>();
            RuneInstance r = e.s.Rune(runeInstanceId);
            if (r == null) return list;
            if (r.IsComposite)
            {
                list.Add(new ConceptChoice { id = CompositeIds.Essence, name = "Essence", description = "What this rune is: the nature of what it was made from." });
                return list;
            }
            RuneDef def = e.c.Rune(r.runeId);
            if (def == null) return list;
            foreach (ConceptDef k in def.concepts)
            {
                if (k == null || !RuneRules.Knows(e.s, r.runeId, k.id)) continue;
                var ch = new ConceptChoice { id = k.id, name = k.Name, description = k.description, minimumRank = k.minimumRankToUse };
                if (k.minimumRankToUse > r.rank)
                {
                    ch.usable = false;
                    ch.reason = $"{k.Name} needs a rune of rank {k.minimumRankToUse} (this one is rank {r.rank}).";
                }
                list.Add(ch);
            }
            return list;
        }

        // ================================================================== editing
        public int IndexOf(string runeInstanceId)
        {
            for (int i = 0; i < runes.Count; i++)
                if (runes[i].runeInstanceId == runeInstanceId) return i;
            return -1;
        }

        /// <summary>Changes what is made. Runes that no longer fit (an anchoring rune in a consuming mode, another rank) are dropped.</summary>
        public void SetMode(SynthesisEnv e, SynthesisMode newMode)
        {
            mode = newMode;
            Sanitize(e);
        }

        /// <summary>Adds a rune at the end. False with the reason when it cannot be (already chosen, anchoring, too many...).</summary>
        public bool Add(SynthesisEnv e, string runeInstanceId, out string why)
        {
            why = "";
            if (IndexOf(runeInstanceId) >= 0) { why = "That rune is already chosen."; return false; }
            RuneChoice ch = RuneChoices(e).Find(r => r.instanceId == runeInstanceId);
            if (ch == null) { why = "That rune is no longer in your soul."; return false; }
            if (!ch.usable) { why = ch.reason; return false; }
            CountRule(e, out _, out int max);
            if (runes.Count >= max) { why = $"At most {max} rune{(max == 1 ? "" : "s")} here."; return false; }
            var d = new DraftRune { runeInstanceId = runeInstanceId, energy = (float)Math.Round(ch.energy / 2f) };
            List<ConceptChoice> options = Concepts(e, runeInstanceId).FindAll(k => k.usable);
            if (options.Count == 1) d.conceptId = options[0].id; // nothing to choose
            runes.Add(d);
            return true;
        }

        public void Remove(int index)
        {
            if (index >= 0 && index < runes.Count) runes.RemoveAt(index);
        }

        /// <summary>Moves a rune by <paramref name="delta"/> places (the order matters, design spec 11.3).</summary>
        public bool Move(int index, int delta)
        {
            int to = index + delta;
            if (index < 0 || index >= runes.Count || to < 0 || to >= runes.Count || delta == 0) return false;
            DraftRune d = runes[index];
            runes.RemoveAt(index);
            runes.Insert(to, d);
            return true;
        }

        public bool SetConcept(SynthesisEnv e, int index, string conceptId)
        {
            if (index < 0 || index >= runes.Count) return false;
            ConceptChoice ch = Concepts(e, runes[index].runeInstanceId).Find(k => k.id == conceptId);
            if (ch == null || !ch.usable) return false;
            runes[index].conceptId = conceptId;
            return true;
        }

        /// <summary>Sets what a rune gives: never more than it holds, never below 0.</summary>
        public void SetEnergy(SynthesisEnv e, int index, float energy)
        {
            if (index < 0 || index >= runes.Count) return;
            RuneInstance r = e.s.Rune(runes[index].runeInstanceId);
            float held = r != null ? r.energy : 0f;
            if (float.IsNaN(energy)) energy = 0f;
            runes[index].energy = Math.Max(0f, Math.Min(held, (float)Math.Round(energy)));
        }

        /// <summary>
        /// Brings the draft in line with the soul as it is now (runes may have been used, released or anchored): drops runes that
        /// are gone or no longer usable, concepts that are no longer valid, and energies above what a rune holds.
        /// </summary>
        public void Sanitize(SynthesisEnv e)
        {
            List<RuneChoice> choices = RuneChoices(e);
            for (int i = runes.Count - 1; i >= 0; i--)
            {
                DraftRune d = runes[i];
                RuneChoice ch = choices.Find(r => r.instanceId == d.runeInstanceId);
                if (ch == null || !ch.usable) { runes.RemoveAt(i); continue; }
                if (!string.IsNullOrEmpty(d.conceptId))
                {
                    ConceptChoice k = Concepts(e, d.runeInstanceId).Find(x => x.id == d.conceptId);
                    if (k == null || !k.usable) d.conceptId = "";
                }
                d.energy = Math.Max(0f, Math.Min(d.energy, ch.energy));
            }
            CountRule(e, out _, out int max);
            while (runes.Count > max) runes.RemoveAt(runes.Count - 1);
        }

        // ================================================================== the request
        /// <summary>The request this draft makes (no seed: the server chooses the chance). A pure advancement carries no formula.</summary>
        public SynthesisRequest BuildRequest(SynthesisEnv e)
        {
            bool pure = IsPure(e);
            var req = new SynthesisRequest { mode = mode, name = name ?? "" };
            if (!pure)
            {
                req.intentionId = intentionId ?? "";
                req.effectId = effectId ?? "";
            }
            foreach (DraftRune d in runes)
                req.slots.Add(new SynthesisSlot(d.runeInstanceId, pure ? "" : d.conceptId ?? "", d.energy));
            return req;
        }

        // ================================================================== what the soul asks (design spec 11.1: Soul Integrity requirements)
        /// <summary>What the soul's condition means for a synthesis now, as lines for the player.</summary>
        public static List<string> SoulRequirementLines(SynthesisEnv e)
        {
            var lines = new List<string>();
            SoulRestrictions r = e.ctx.soul;
            string condition = string.IsNullOrEmpty(r.conditionName) ? "" : r.conditionName;
            lines.Add($"Soul Integrity {e.ctx.integrity:0.#} / {e.ctx.maxIntegrity:0.#}" + (condition.Length > 0 ? $" ({condition})" : ""));
            if (r.collapseLocked) lines.Add("Your soul has collapsed: no synthesis until it recovers.");
            else if (!r.synthesisAllowed) lines.Add("Your soul is too unstable to synthesize.");
            if (r.maxSynthesisRunes > 0) lines.Add($"In this condition: at most {r.maxSynthesisRunes} runes.");
            if (r.maxSynthesisComplexity < 1f) lines.Add("In this condition: complex effects are unavailable.");
            if (r.synthesisFailureChance > 0f) lines.Add($"In this condition: the magic falters more often (+{r.synthesisFailureChance * 100f:0}%).");
            if (r.synthesisStability < 1f) lines.Add($"In this condition: results are less stable (×{r.synthesisStability:0.##}).");
            return lines;
        }
    }
}
