using System;
using System.Collections.Generic;
using System.Text;

namespace RuneMagic
{
    /// <summary>A concept a source can teach a player now, with its chance weight.</summary>
    public struct ConceptCandidate
    {
        public ConceptDef concept;
        public RuneDef rune;
        public float weight;
    }

    /// <summary>One question of a discovery (a riddle with its options): the answer stays on the server.</summary>
    public sealed class DiscoveryQuestion
    {
        public string riddleId = "";
        public bool generated;
        public string text = "";
        public List<string> options = new List<string>();
        public int correct;
    }

    /// <summary>
    /// The rules of concept discovery (design spec 8, 17) that need no state machine: which concepts a source can teach a
    /// player, how difficulty maps from the dungeon and the concept's rarity, and how riddles are built (written riddles
    /// first, generated ones from the concept's aspects otherwise) with their options shuffled. Pure.
    /// </summary>
    public static class DiscoveryRules
    {
        // ------------------------------------------------------------------ keys
        /// <summary>The key a wrong answer burns: this source definition for this concept (decision D7).</summary>
        public static string BurnKey(string sourceId, string conceptId) => sourceId + "|" + conceptId;

        public static bool IsBurned(RuneSoulState s, string sourceId, string conceptId) =>
            s?.discovery?.burned != null && s.discovery.burned.Contains(BurnKey(sourceId, conceptId));

        public static int AttemptsUsed(RuneSoulState s, string sourceId, string conceptId)
        {
            string key = BurnKey(sourceId, conceptId);
            if (s?.discovery?.attempts != null)
                foreach (KeyCount a in s.discovery.attempts)
                    if (a != null && a.key == key) return a.count;
            return 0;
        }

        // ------------------------------------------------------------------ tiers
        /// <summary>The difficulty band of a source in a dungeon of tier <paramref name="dungeonTier"/> (an override of 0 or more wins).</summary>
        public static int TierIndex(RuneCatalog c, int dungeonTier, int tierOverride = -1)
        {
            DiscoveryBalance b = c?.Discovery;
            if (b == null || b.tiers.Count == 0) return -1;
            if (tierOverride >= 0) return Math.Min(tierOverride, b.tiers.Count - 1);
            return b.TierIndexFor(dungeonTier);
        }

        // ------------------------------------------------------------------ which concept
        /// <summary>
        /// The concepts <paramref name="src"/> can teach at band <paramref name="tierIndex"/> to a player with this
        /// <paramref name="state"/>: not known, not burned for this source, of a rarity the band teaches, and (when the
        /// balance says so) of a rune the player has absorbed (<paramref name="heldRunes"/>; null = no restriction).
        /// <paramref name="whyNone"/> says why the list is empty.
        /// </summary>
        public static List<ConceptCandidate> Candidates(RuneCatalog c, RuneSoulState s, DiscoverySourceDef src, int tierIndex,
            IList<string> heldRunes, out string whyNone)
        {
            whyNone = "";
            var list = new List<ConceptCandidate>();
            DiscoveryTierDef tier = c?.Discovery?.Tier(tierIndex);
            if (c == null || src == null || tier == null) { whyNone = "This place has nothing to teach."; return list; }

            var runes = new List<RuneDef>();
            var seenRunes = new HashSet<string>(StringComparer.Ordinal);
            void AddRune(RuneDef r) { if (r != null && seenRunes.Add(r.id)) runes.Add(r); }
            if (src.runes == null || src.runes.Count == 0) foreach (RuneDef r in c.Runes) AddRune(r);
            else foreach (string id in src.runes) AddRune(c.Rune(id));
            var extra = new HashSet<string>(src.concepts ?? new List<string>(), StringComparer.Ordinal);
            foreach (string cid in extra) AddRune(c.RuneOfConcept(cid));

            bool restrict = c.Discovery.onlyRunesInSoul && heldRunes != null;
            var held = new HashSet<string>(StringComparer.Ordinal);
            if (restrict) foreach (string h in heldRunes) if (!string.IsNullOrEmpty(h)) held.Add(h);

            int inScope = 0, known = 0, burned = 0, tooRare = 0;
            foreach (RuneDef rune in runes)
            {
                if (restrict && !held.Contains(rune.id)) continue;
                bool whole = src.runes == null || src.runes.Count == 0 || src.runes.Contains(rune.id);
                foreach (ConceptDef k in rune.concepts)
                {
                    if (k == null || (!whole && !extra.Contains(k.id))) continue;
                    inScope++;
                    if (RuneRules.Knows(s, rune.id, k.id)) { known++; continue; }
                    if (IsBurned(s, src.id, k.id)) { burned++; continue; }
                    float w = tier.WeightOf(k.rarity);
                    if (w <= 0f) { tooRare++; continue; }
                    list.Add(new ConceptCandidate { concept = k, rune = rune, weight = w });
                }
            }
            if (list.Count == 0)
            {
                if (restrict && inScope == 0)
                    whyNone = "The glyphs speak of runes you do not carry. Absorb the runes they speak of, or find another place.";
                else if (known > 0 && known + burned + tooRare >= inScope && burned == 0 && tooRare == 0)
                    whyNone = "You already understand everything this place can teach.";
                else if (burned > 0 && tooRare == 0)
                    whyNone = "This place has nothing more to teach you: what it knew, you have already failed to grasp. Find another source.";
                else if (tooRare > 0)
                    whyNone = "What remains here is beyond what a place of this depth can teach. Seek a deeper dungeon.";
                else whyNone = "This place has nothing to teach you.";
            }
            return list;
        }

        /// <summary>Picks a concept by weight.</summary>
        public static ConceptCandidate Pick(IList<ConceptCandidate> candidates, Random rng)
        {
            float total = 0f;
            foreach (ConceptCandidate k in candidates) total += k.weight;
            double pick = rng.NextDouble() * total;
            ConceptCandidate chosen = candidates[candidates.Count - 1];
            foreach (ConceptCandidate k in candidates)
            {
                pick -= k.weight;
                if (pick < 0) { chosen = k; break; }
            }
            return chosen;
        }

        // ------------------------------------------------------------------ questions
        /// <summary>
        /// The questions of a discovery for <paramref name="concept"/> at band <paramref name="tierIndex"/>: one per step of the
        /// band. The first is a written riddle when there is one (avoiding ones already seen), the rest generated; options are
        /// shuffled. The ids of written riddles used are returned in <paramref name="usedRiddles"/>.
        /// </summary>
        public static List<DiscoveryQuestion> BuildQuestions(RuneCatalog c, RuneSoulState s, ConceptDef concept, int tierIndex, Random rng,
            List<string> usedRiddles = null)
        {
            var questions = new List<DiscoveryQuestion>();
            DiscoveryTierDef tier = c?.Discovery?.Tier(tierIndex);
            if (c == null || concept == null || tier == null) return questions;
            RuneDef rune = c.RuneOfConcept(concept.id);
            for (int step = 0; step < Math.Max(1, tier.steps); step++)
            {
                var q = new DiscoveryQuestion();
                RiddleDef written = step == 0 ? PickWritten(c, s, concept.id, tierIndex, rng) : null;
                List<ConceptDef> distractors = Distractors(c, rune, concept, written, Math.Max(1, tier.options - 1), rng);
                q.options.Add(concept.Name);
                foreach (ConceptDef d in distractors) q.options.Add(d.Name);
                if (written != null)
                {
                    q.riddleId = written.id;
                    q.text = written.text.Trim();
                    usedRiddles?.Add(written.id);
                }
                else
                {
                    q.generated = true;
                    q.riddleId = "gen:" + concept.id + ":" + step;
                    q.text = GenerateRiddle(c, concept, distractors, tierIndex, step);
                }
                Shuffle(q.options, rng, out q.correct);
                questions.Add(q);
            }
            return questions;
        }

        private static RiddleDef PickWritten(RuneCatalog c, RuneSoulState s, string conceptId, int tierIndex, Random rng)
        {
            var usable = new List<RiddleDef>();
            foreach (RiddleDef r in c.RiddlesOf(conceptId))
                if (r != null && r.WorksAt(tierIndex)) usable.Add(r);
            if (usable.Count == 0) return null;
            if (c.Discovery.avoidSeenRiddles && s?.discovery != null)
            {
                var unseen = usable.FindAll(r => !s.discovery.seenRiddles.Contains(r.id));
                if (unseen.Count == 0) return null; // every written riddle was seen: a generated one is new
                usable = unseen;
            }
            return usable[rng.Next(usable.Count)];
        }

        /// <summary>Wrong answers: the riddle's own, else the rune's other concepts, topped up from related runes, then any.</summary>
        public static List<ConceptDef> Distractors(RuneCatalog c, RuneDef rune, ConceptDef answer, RiddleDef written, int count, Random rng)
        {
            var result = new List<ConceptDef>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { answer.Name };
            void TryAdd(ConceptDef k)
            {
                if (k == null || k.id == answer.id || result.Count >= count || !names.Add(k.Name)) return;
                result.Add(k);
            }
            if (written != null && written.distractors != null && written.distractors.Count > 0)
            {
                var own = new List<ConceptDef>();
                foreach (string id in written.distractors) own.Add(c.Concept(id));
                ShuffleList(own, rng);
                foreach (ConceptDef k in own) TryAdd(k);
            }
            var sameRune = new List<ConceptDef>(rune != null ? rune.concepts : new List<ConceptDef>());
            ShuffleList(sameRune, rng);
            foreach (ConceptDef k in sameRune) TryAdd(k);
            if (result.Count < count)
            {
                var related = new List<ConceptDef>();
                var other = new List<ConceptDef>();
                foreach (RuneDef r in c.Runes)
                {
                    if (r == null || r == rune) continue;
                    bool near = false;
                    if (rune != null && rune.alignments != null && r.alignments != null)
                        foreach (string a in r.alignments) if (rune.alignments.Contains(a)) near = true;
                    (near ? related : other).AddRange(r.concepts);
                }
                ShuffleList(related, rng);
                ShuffleList(other, rng);
                foreach (ConceptDef k in related) TryAdd(k);
                foreach (ConceptDef k in other) TryAdd(k);
            }
            return result;
        }

        /// <summary>
        /// A riddle built from what the concept's aspects mean (the aspects' own descriptions), so it asks for reasoning about
        /// principles: clues about what the concept is bound to and, at the lower bands, what it is <i>not</i> (the strongest
        /// aspect of a wrong answer that this concept lacks). Higher bands give fewer clues; later steps use other aspects.
        /// </summary>
        public static string GenerateRiddle(RuneCatalog c, ConceptDef concept, IList<ConceptDef> distractors, int tierIndex, int step)
        {
            DiscoveryTierDef tier = c.Discovery.Tier(tierIndex);
            var sb = new StringBuilder();
            sb.Append(step > 0 ? "The glyphs rearrange and ask again, more deeply:\n" : "The glyphs settle into a riddle:\n");

            var aspects = new List<AspectWeight>(concept.aspects ?? new List<AspectWeight>());
            aspects.Sort((x, y) => y.weight.CompareTo(x.weight));
            int clues = Math.Max(1, tier != null ? tier.clues : 3);
            string lead = tierIndex <= 0 && step == 0 && !string.IsNullOrEmpty(concept.description)
                ? "I let a Mage " + LowerFirst(concept.description.TrimEnd('.')) + ".\n" : "";
            if (lead.Length > 0 && !NamesAnswer(lead, concept.Name)) sb.Append(lead);
            int written = 0;
            for (int i = 0; i < aspects.Count && written < clues; i++)
            {
                AspectDef a = c.Aspect(aspects[(i + step) % aspects.Count].aspect);
                if (a == null) continue;
                string d = string.IsNullOrEmpty(a.description) ? a.Name.ToLowerInvariant() : LowerFirst(a.description.TrimEnd('.'));
                string line = written == 0 ? $"At my heart lies {a.Name.ToLowerInvariant()}: {d}.\n" : $"I also lean on {a.Name.ToLowerInvariant()}: {d}.\n";
                if (NamesAnswer(line, concept.Name)) continue; // a clue must never say the answer
                sb.Append(line);
                written++;
            }
            int negatives = Math.Max(0, 3 - tierIndex);
            var mine = new HashSet<string>(StringComparer.Ordinal);
            foreach (AspectWeight w in aspects) mine.Add(w.aspect);
            int told = 0;
            foreach (ConceptDef d in distractors)
            {
                if (told >= negatives) break;
                AspectWeight strongest = null;
                if (d.aspects != null)
                    foreach (AspectWeight w in d.aspects)
                        if (!mine.Contains(w.aspect) && (strongest == null || w.weight > strongest.weight)) strongest = w;
                AspectDef a = strongest != null ? c.Aspect(strongest.aspect) : null;
                if (a == null) continue;
                string no = $"I am not about {a.Name.ToLowerInvariant()}: that is {d.Name}'s way.\n";
                if (NamesAnswer(no, concept.Name)) continue; // a wrong answer named like the right one would give it away
                sb.Append(no);
                told++;
            }
            sb.Append("What Concept am I?");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Does <paramref name="text"/> contain <paramref name="name"/> as a whole word (any case)?</summary>
        private static bool NamesAnswer(string text, string name)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(name)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"\b" + System.Text.RegularExpressions.Regex.Escape(name) + @"\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static string LowerFirst(string t) => string.IsNullOrEmpty(t) ? t : char.ToLowerInvariant(t[0]) + t.Substring(1);

        private static void ShuffleList<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        /// <summary>Shuffles the options; <paramref name="correct"/> becomes the new index of the first one (the answer).</summary>
        private static void Shuffle(List<string> options, Random rng, out int correct)
        {
            int answer = 0;
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string t = options[i]; options[i] = options[j]; options[j] = t;
                if (answer == i) answer = j;
                else if (answer == j) answer = i;
            }
            correct = answer;
        }
    }

    /// <summary>
    /// Which concepts which sources can teach at which bands (design spec 17.2; the validator's coverage report): a concept
    /// that no source can ever teach is a content bug, and one with a single source is fragile, since a wrong answer burns it.
    /// </summary>
    public static class DiscoveryCoverage
    {
        /// <summary>The ids of the sources that can teach <paramref name="concept"/> at band <paramref name="tierIndex"/>.</summary>
        public static List<string> SourcesOf(RuneCatalog c, ConceptDef concept, int tierIndex)
        {
            var list = new List<string>();
            DiscoveryTierDef tier = c.Discovery.Tier(tierIndex);
            RuneDef rune = c.RuneOfConcept(concept.id);
            if (tier == null || rune == null || tier.WeightOf(concept.rarity) <= 0f) return list;
            foreach (DiscoverySourceDef s in c.DiscoverySources)
            {
                if (s == null || !s.WorksAt(tierIndex)) continue;
                bool open = s.runes == null || s.runes.Count == 0;
                if (open || s.runes.Contains(rune.id) || (s.concepts != null && s.concepts.Contains(concept.id))) list.Add(s.id);
            }
            return list;
        }

        /// <summary>The ids of the sources that can teach <paramref name="concept"/> at any band.</summary>
        public static List<string> SourcesOfAnyBand(RuneCatalog c, ConceptDef concept)
        {
            var set = new List<string>();
            for (int t = 0; t < c.Discovery.tiers.Count; t++)
                foreach (string id in SourcesOf(c, concept, t)) if (!set.Contains(id)) set.Add(id);
            return set;
        }

        /// <summary>Concepts nothing can teach, and concepts a single source teaches.</summary>
        public static void Find(RuneCatalog c, List<ConceptDef> unreachable, List<ConceptDef> fragile)
        {
            foreach (RuneDef r in c.Runes)
                foreach (ConceptDef k in r.concepts)
                {
                    if (k == null) continue;
                    int n = SourcesOfAnyBand(c, k).Count;
                    if (n == 0) unreachable?.Add(k);
                    else if (n == 1) fragile?.Add(k);
                }
        }
    }
}
