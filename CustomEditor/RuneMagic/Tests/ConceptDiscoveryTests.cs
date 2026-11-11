#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of concept discovery (Phase 4; design spec 8, 17): difficulty bands, which concepts a source can teach,
    /// riddles (written and generated) and their options, the rune-tracing minigame and its server-side validation, the
    /// state machine (permanent unlock, burning, attempts, abandoning, multi-step), secrecy of the answer, saving. Pure rules.
    /// </summary>
    public class ConceptDiscoveryTests
    {
        private static RuneCatalog Catalog() => RuneDefaults.Catalog();

        private static RuneCatalog Custom(Action<RuneMagicData> edit)
        {
            RuneMagicData d = RuneDefaults.Create();
            edit(d);
            return RuneCatalog.Compile(d);
        }

        private static RuneSoulState SoulWith(RuneCatalog c, params string[] runes)
        {
            var s = new RuneSoulState();
            foreach (string r in runes)
                RuneRules.Absorb(c, s, new RuneDrop { runeId = r, rank = 1, quality = 50f }, new RuneOrigin { kind = "test" }, null, 1L);
            return s;
        }

        private static List<string> Held(RuneSoulState s)
        {
            var h = new List<string>();
            foreach (RuneInstance r in s.runes) if (!h.Contains(r.runeId)) h.Add(r.runeId);
            return h;
        }

        private static DiscoverySession StartAt(RuneCatalog c, RuneSoulState s, string source, int band, int seed, string concept = null)
        {
            DiscoveryStart st = DiscoveryFlow.Start(c, s, source, 0, band, concept, Held(s), new Random(seed), 100f);
            Assert.IsTrue(st.ok, st.reason);
            return st.session;
        }

        /// <summary>The report of a player who seals every glyph in the middle of its target, as early as the rules allow.</summary>
        private static List<float> Perfect(MinigameChallenge ch, float minInterval)
        {
            var presses = new List<float>();
            float t = 0f;
            foreach (TraceNode n in ch.nodes)
            {
                float lapsToCentre = ((n.center - ch.phase) % 1f + 1f) % 1f;
                float at = lapsToCentre / ch.speed;
                float lap = 1f / ch.speed;
                while (at < t + minInterval) at += lap;
                presses.Add(at);
                t = at;
            }
            return presses;
        }

        private static List<float> Hasty(MinigameChallenge ch)
        {
            var p = new List<float>();
            for (int i = 0; i < ch.nodes.Count; i++) p.Add(0.5f + i * 0.3f);
            return p;
        }

        private static void PassMinigame(RuneCatalog c, RuneSoulState s, DiscoverySession session)
        {
            List<float> presses = Perfect(session.challenge, c.Discovery.minPressInterval);
            float now = session.minigameIssuedAt + presses[presses.Count - 1] + 0.1f;
            MinigameOutcome o = DiscoveryFlow.SubmitMinigame(c, s, session, presses, now, 5L, out string msg);
            Assert.AreEqual(MinigameOutcome.Passed, o, msg);
        }

        // ------------------------------------------------------------------ content (design spec 17.2, 17.3)
        [Test]
        public void TheBuiltInDiscoveryContentIsValidAndCoversEveryConcept()
        {
            RuneCatalog c = Catalog();
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            var unreachable = new List<ConceptDef>();
            var fragile = new List<ConceptDef>();
            DiscoveryCoverage.Find(c, unreachable, fragile);
            Assert.AreEqual(0, unreachable.Count, "every concept can be taught by some source: " + (unreachable.Count > 0 ? unreachable[0].id : ""));
            Assert.AreEqual(0, fragile.Count, "every concept has at least two sources, so a wrong answer never locks one: " + (fragile.Count > 0 ? fragile[0].id : ""));
        }

        [Test]
        public void TheFiveBandsFollowTheSpec()
        {
            DiscoveryBalance d = Catalog().Discovery;
            CollectionAssert.AreEqual(new[] { "Beginner", "Intermediate", "Advanced", "Expert", "Legendary" }, d.tiers.ConvertAll(t => t.Name));
            Assert.AreEqual(1, d.tiers[0].steps, "Beginner: straightforward questions");
            Assert.Greater(d.tiers[2].steps, 1, "Advanced: multi-step challenges");
            for (int i = 1; i < d.tiers.Count; i++)
            {
                Assert.GreaterOrEqual(d.tiers[i].minigameNodes, d.tiers[i - 1].minigameNodes, "minigames get more complex");
                Assert.LessOrEqual(d.tiers[i].nodeWidth, d.tiers[i - 1].nodeWidth, "and narrower");
                Assert.GreaterOrEqual(d.tiers[i].options, d.tiers[i - 1].options);
            }
            Assert.AreEqual(0f, d.tiers[0].WeightOf(RuneRarity.Rare), "rare concepts need harder challenges");
            Assert.AreEqual(0f, d.tiers[1].WeightOf(RuneRarity.Rare));
            Assert.Greater(d.tiers[2].WeightOf(RuneRarity.Rare), 0f);
            Assert.AreEqual(0f, d.tiers[2].WeightOf(RuneRarity.Legendary));
            Assert.Greater(d.tiers[4].WeightOf(RuneRarity.Legendary), 0f, "the most powerful concepts need the most dangerous dungeons");
        }

        [Test]
        public void TheSpecPressureRiddleIsSampleContent()
        {
            RuneCatalog c = Catalog();
            RiddleDef r = c.Riddle("wind.pressure.1");
            Assert.IsNotNull(r);
            Assert.AreEqual("wind.pressure", r.concept);
            StringAssert.Contains("I cannot be seen, yet I can crush stone.", r.text);
            StringAssert.Contains("I can be trapped within a vessel, and I grow stronger when confined.", r.text);
            StringAssert.Contains("I am neither the wind's movement nor its direction.", r.text);
            StringAssert.Contains("What Concept am I?", r.text);
            RuneSoulState s = SoulWith(c, "wind");
            List<DiscoveryQuestion> q = DiscoveryRules.BuildQuestions(c, s, c.Concept("wind.pressure"), 0, new Random(4));
            // The spec's options, whatever the band's option count: Flow, Pressure, Movement, Turbulence (4 at Intermediate).
            List<DiscoveryQuestion> q4 = DiscoveryRules.BuildQuestions(c, s, c.Concept("wind.pressure"), 1, new Random(4));
            CollectionAssert.AreEquivalent(new[] { "Flow", "Pressure", "Movement", "Turbulence" }, q4[0].options);
            Assert.AreEqual("Pressure", q4[0].options[q4[0].correct]);
            Assert.AreEqual(3, q[0].options.Count, "Beginner: three options");
            StringAssert.Contains("crush stone", q[0].text);
        }

        // ------------------------------------------------------------------ bands and candidates
        [Test]
        public void TheDungeonTierSetsTheBand()
        {
            RuneCatalog c = Catalog();
            Assert.AreEqual(0, DiscoveryRules.TierIndex(c, 0));
            Assert.AreEqual(2, DiscoveryRules.TierIndex(c, 2));
            Assert.AreEqual(4, DiscoveryRules.TierIndex(c, 99), "deeper than the last band: the last");
            Assert.AreEqual(3, DiscoveryRules.TierIndex(c, 0, 3), "an override wins");
            Assert.AreEqual(4, DiscoveryRules.TierIndex(c, 0, 50), "clamped");
            RuneCatalog late = Custom(d => { d.balance.discovery.tiers[0].minDungeonTier = 5; d.balance.discovery.tiers[1].minDungeonTier = 6; });
            Assert.AreEqual(0, DiscoveryRules.TierIndex(late, 0), "below every minimum: the easiest band");
        }

        [Test]
        public void ASourceTeachesOnlyWhatThePlayerCanLearnAtThatDepth()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind", "fire");
            DiscoverySourceDef src = c.DiscoverySource("ancient_inscription");
            List<ConceptCandidate> beginner = DiscoveryRules.Candidates(c, s, src, 0, Held(s), out _);
            Assert.Greater(beginner.Count, 0);
            foreach (ConceptCandidate k in beginner)
            {
                Assert.That(k.concept.rarity, Is.EqualTo(RuneRarity.Common).Or.EqualTo(RuneRarity.Uncommon), "Beginner teaches common and uncommon concepts");
                Assert.That(k.rune.id, Is.EqualTo("wind").Or.EqualTo("fire"), "only runes in the soul");
            }
            List<ConceptCandidate> legend = DiscoveryRules.Candidates(c, s, src, 4, Held(s), out _);
            Assert.IsTrue(legend.Exists(k => k.concept.id == "fire.plasma"), "epic concepts at the top band");
            Assert.IsFalse(legend.Exists(k => k.concept.rarity == RuneRarity.Common), "Legendary teaches none of the common ones");
            Assert.IsFalse(beginner.Exists(k => k.concept.id == "fire.plasma"));
        }

        [Test]
        public void KnownConceptsAreNeverTaughtAgain()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            RuneRules.Learn(s, "wind", "wind.pressure");
            DiscoverySourceDef src = c.DiscoverySource("ancient_inscription");
            Assert.IsFalse(DiscoveryRules.Candidates(c, s, src, 1, Held(s), out _).Exists(k => k.concept.id == "wind.pressure"));
            // A source that can only teach what the player already knows says so, in plain words.
            var tiny = new DiscoverySourceDef { id = "tiny", concepts = new List<string> { "wind.pressure" }, runes = new List<string> { "wind" } };
            RuneCatalog pack = Custom(d => d.discoverySources.Add(tiny));
            RuneSoulState all = SoulWith(pack, "wind");
            foreach (ConceptDef k in pack.Rune("wind").concepts) RuneRules.Learn(all, "wind", k.id);
            List<ConceptCandidate> none = DiscoveryRules.Candidates(pack, all, tiny, 1, Held(all), out string why);
            Assert.AreEqual(0, none.Count);
            StringAssert.Contains("already understand everything", why);
        }

        [Test]
        public void ARuneNotInTheSoulTeachesNothingUnlessTheBalanceAllowsIt()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySourceDef fire = c.DiscoverySource("flame_inscription");
            Assert.AreEqual(0, DiscoveryRules.Candidates(c, s, fire, 0, Held(s), out string why).Count);
            StringAssert.Contains("runes you do not carry", why);
            RuneCatalog open = Custom(d => d.balance.discovery.onlyRunesInSoul = false);
            Assert.Greater(DiscoveryRules.Candidates(open, s, open.DiscoverySource("flame_inscription"), 0, Held(s), out _).Count, 0);
            var none = new RuneSoulState();
            DiscoveryStart st = DiscoveryFlow.Start(c, none, "ancient_inscription", 0, 0, null, Held(none), new Random(1), 0f);
            Assert.IsFalse(st.ok);
            StringAssert.Contains("runes you do not carry", st.reason, "a Mage with no runes learns why nothing happens");
        }

        [Test]
        public void ChoosingAConceptFollowsTheRarityWeights()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind", "fire");
            List<ConceptCandidate> list = DiscoveryRules.Candidates(c, s, c.DiscoverySource("ancient_inscription"), 2, Held(s), out _);
            var rng = new Random(9);
            int common = 0, rare = 0;
            for (int i = 0; i < 2000; i++)
            {
                RuneRarity r = DiscoveryRules.Pick(list, rng).concept.rarity;
                if (r == RuneRarity.Common) common++;
                if (r == RuneRarity.Rare) rare++;
            }
            Assert.Greater(rare, 0, "Advanced can teach rare concepts");
            Assert.Greater(common, 0);
            // Per concept the weights are 0.5 / 1 / 1: a rare concept is picked about as often as an uncommon one, a common one half as often.
            DiscoverySourceDef src = c.DiscoverySource("ancient_inscription");
            float w = c.Discovery.Tier(2).WeightOf(RuneRarity.Common);
            Assert.AreEqual(0.5f, w, 0.0001f);
            Assert.AreEqual(1f, list.Find(k => k.concept.rarity == RuneRarity.Rare).weight, 0.0001f);
        }

        [Test]
        public void AFixedConceptIsHonoredWhenItCanBeTaught()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 3, "wind.flow");
            Assert.AreEqual("wind.flow", session.conceptId);
            DiscoveryStart no = DiscoveryFlow.Start(c, s, "ancient_inscription", 0, 0, "wind.vortex", Held(s), new Random(1), 0f);
            Assert.IsTrue(no.ok);
            Assert.AreNotEqual("wind.vortex", no.session.conceptId, "a rare concept cannot be forced at the Beginner band");
            Assert.IsFalse(DiscoveryFlow.Start(c, s, "no_such_source", 0, 0, null, Held(s), new Random(1), 0f).ok);
            Assert.IsFalse(DiscoveryFlow.Start(c, s, "sealed_chamber", 0, 0, null, Held(s), new Random(1), 0f).ok, "a sealed chamber does not work at the Beginner band");
        }

        // ------------------------------------------------------------------ riddles
        [Test]
        public void OptionsAreUniqueShuffledAndContainTheAnswerOnce()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            ConceptDef pressure = c.Concept("wind.compression");
            var positions = new HashSet<int>();
            for (int seed = 0; seed < 60; seed++)
            {
                DiscoveryQuestion q = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), pressure, 3, new Random(seed))[0];
                Assert.AreEqual(c.Discovery.Tier(3).options, q.options.Count);
                Assert.AreEqual(q.options.Count, new HashSet<string>(q.options, StringComparer.OrdinalIgnoreCase).Count, "no duplicate labels");
                Assert.AreEqual("Compression", q.options[q.correct]);
                int answers = 0;
                foreach (string o in q.options) if (o == "Compression") answers++;
                Assert.AreEqual(1, answers);
                positions.Add(q.correct);
            }
            Assert.Greater(positions.Count, 2, "the correct answer is not always in the same place");
        }

        [Test]
        public void ConceptsWithTheSameNameInTwoRunesAreNotOfferedTwice()
        {
            RuneCatalog c = Catalog();
            ConceptDef fireCombustion = c.Concept("fire.combustion");
            for (int seed = 0; seed < 40; seed++)
            {
                DiscoveryQuestion q = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), fireCombustion, 4, new Random(seed))[0];
                int n = 0;
                foreach (string o in q.options) if (string.Equals(o, "Combustion", StringComparison.OrdinalIgnoreCase)) n++;
                Assert.AreEqual(1, n, "ash.combustion is not a second 'Combustion'");
            }
        }

        [Test]
        public void GeneratedRiddlesAskAboutPrinciplesAndNeverNameTheAnswer()
        {
            RuneCatalog c = Catalog();
            int checkedConcepts = 0;
            foreach (RuneDef rune in c.Runes)
                foreach (ConceptDef k in rune.concepts)
                {
                    if (c.RiddlesOf(k.id).Count > 0) continue;
                    for (int tier = 0; tier < c.Discovery.tiers.Count; tier++)
                    {
                        DiscoveryQuestion q = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), k, tier, new Random(tier + 1))[0];
                        Assert.IsTrue(q.generated);
                        StringAssert.EndsWith("What Concept am I?", q.text);
                        StringAssert.Contains("At my heart lies", q.text, "a generated riddle reasons from the concept's aspects");
                        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(q.text, @"\b" + System.Text.RegularExpressions.Regex.Escape(k.Name) + @"\b",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase), $"{k.id} at band {tier} names its own answer:\n{q.text}");
                        checkedConcepts++;
                    }
                }
            Assert.Greater(checkedConcepts, 1000);
        }

        [Test]
        public void HigherBandsGiveFewerCluesAndLaterStepsAskAgain()
        {
            RuneCatalog c = Catalog();
            ConceptDef k = c.Concept("ash.residue");
            string beginner = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), k, 0, new Random(1))[0].text;
            string expert = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), k, 3, new Random(1))[0].text;
            Assert.Greater(beginner.Split('\n').Length, expert.Split('\n').Length, "fewer clues at the higher band");
            StringAssert.Contains("I let a Mage", beginner, "straightforward at the Beginner band");
            List<DiscoveryQuestion> advanced = DiscoveryRules.BuildQuestions(c, new RuneSoulState(), c.Concept("wind.turbulence"), 2, new Random(1));
            Assert.AreEqual(2, advanced.Count, "multi-step at the Advanced band");
            StringAssert.Contains("ask again", advanced[1].text);
            Assert.AreNotEqual(advanced[0].text, advanced[1].text);
        }

        [Test]
        public void RiddlesAlreadySeenAreAvoided()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            var used = new List<string>();
            DiscoveryQuestion first = DiscoveryRules.BuildQuestions(c, s, c.Concept("wind.pressure"), 0, new Random(2), used)[0];
            Assert.IsFalse(first.generated);
            Assert.AreEqual("wind.pressure.1", used[0]);
            s.discovery.seenRiddles.Add("wind.pressure.1");
            DiscoveryQuestion second = DiscoveryRules.BuildQuestions(c, s, c.Concept("wind.pressure"), 0, new Random(2))[0];
            Assert.IsTrue(second.generated, "the written riddle was seen: a new one is generated");
            RuneCatalog allowRepeat = Custom(d => d.balance.discovery.avoidSeenRiddles = false);
            Assert.IsFalse(DiscoveryRules.BuildQuestions(allowRepeat, s, allowRepeat.Concept("wind.pressure"), 0, new Random(2))[0].generated);
        }

        // ------------------------------------------------------------------ the minigame
        [Test]
        public void TheRuneTraceMinigameIsDeterministicAndScalesWithTheBand()
        {
            RuneCatalog c = Catalog();
            IDiscoveryMinigame game = DiscoveryMinigames.Get("");
            Assert.AreEqual(DiscoveryMinigames.RuneTrace, game.Id);
            MinigameChallenge a = game.Create(77, c.Discovery.Tier(0), 0.2f, c.Discovery);
            MinigameChallenge b = game.Create(77, c.Discovery.Tier(0), 0.2f, c.Discovery);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(a), UnityEngine.JsonUtility.ToJson(b));
            Assert.AreEqual(c.Discovery.Tier(0).minigameNodes, a.nodes.Count);
            MinigameChallenge hard = game.Create(77, c.Discovery.Tier(4), 0.2f, c.Discovery);
            Assert.Greater(hard.nodes.Count, a.nodes.Count);
            Assert.Less(hard.nodes[0].width, a.nodes[0].width);
            MinigameChallenge complex = game.Create(77, c.Discovery.Tier(0), 1f, c.Discovery);
            Assert.Less(complex.nodes[0].width, a.nodes[0].width, "complex concepts have narrower targets");
            for (int i = 1; i < a.nodes.Count; i++)
                Assert.GreaterOrEqual(RuneTraceMinigame.CircularDistance(a.nodes[i].center, a.nodes[i - 1].center), 0.1f, "successive glyphs are not on top of each other");
            Assert.AreEqual(0.1f, RuneTraceMinigame.CircularDistance(0.95f, 0.05f), 0.0001f, "the circle wraps");
        }

        [Test]
        public void ThePerfectPlayerPassesAndTheCarelessOneFails()
        {
            RuneCatalog c = Catalog();
            IDiscoveryMinigame game = DiscoveryMinigames.Get("");
            for (int tier = 0; tier < 5; tier++)
            {
                MinigameChallenge ch = game.Create(11 + tier, c.Discovery.Tier(tier), 0.3f, c.Discovery);
                List<float> good = Perfect(ch, c.Discovery.minPressInterval);
                MinigameResult r = game.Evaluate(ch, good, good[good.Count - 1] + 0.2f, c.Discovery);
                Assert.IsTrue(r.valid, r.reason);
                Assert.IsTrue(r.passed, $"band {tier}: score {r.score}");
                Assert.AreEqual(ch.nodes.Count, r.hits);
                Assert.AreEqual(1f, r.score, 0.001f);
            }
            MinigameChallenge e = game.Create(5, c.Discovery.Tier(3), 0f, c.Discovery);
            // Seals at times where the marker is far from every target.
            var bad = new List<float>();
            float t = 0f;
            foreach (TraceNode n in e.nodes)
            {
                float away = (n.center + 0.5f) % 1f;
                float at = (((away - e.phase) % 1f + 1f) % 1f) / e.speed;
                while (at < t + 0.3f) at += 1f / e.speed;
                bad.Add(at);
                t = at;
            }
            MinigameResult worst = game.Evaluate(e, bad, bad[bad.Count - 1] + 0.1f, c.Discovery);
            Assert.IsTrue(worst.valid);
            Assert.IsFalse(worst.passed);
            Assert.AreEqual(0, worst.hits);
        }

        [Test]
        public void TheServerRefusesReportsThatCannotHaveBeenPlayed()
        {
            RuneCatalog c = Catalog();
            IDiscoveryMinigame game = DiscoveryMinigames.Get("");
            MinigameChallenge ch = game.Create(3, c.Discovery.Tier(1), 0f, c.Discovery);
            List<float> good = Perfect(ch, c.Discovery.minPressInterval);
            float last = good[good.Count - 1];

            Assert.IsTrue(game.Evaluate(ch, good, last + 0.1f, c.Discovery).valid);
            Assert.IsFalse(game.Evaluate(ch, good, last - 5f, c.Discovery).valid, "the server's clock says it cannot have taken that long");
            Assert.IsTrue(game.Evaluate(ch, good, last - 1f, c.Discovery).valid, "within the clock slack");
            Assert.IsFalse(game.Evaluate(ch, good.GetRange(0, good.Count - 1), last + 1f, c.Discovery).valid, "not every glyph sealed");
            Assert.IsFalse(game.Evaluate(ch, null, last, c.Discovery).valid);
            var fast = new List<float>();
            for (int i = 0; i < ch.nodes.Count; i++) fast.Add(i * 0.05f);
            Assert.IsFalse(game.Evaluate(ch, fast, 100f, c.Discovery).valid, "faster than a hand");
            var nan = new List<float>(good) { [0] = float.NaN };
            Assert.IsFalse(game.Evaluate(ch, nan, 100f, c.Discovery).valid);
            var negative = new List<float>(good) { [0] = -1f };
            Assert.IsFalse(game.Evaluate(ch, negative, 100f, c.Discovery).valid);
            var late = new List<float>(good);
            late[late.Count - 1] = ch.timeLimit + 5f;
            Assert.IsFalse(game.Evaluate(ch, late, 1000f, c.Discovery).valid, "after the time limit");
        }

        private sealed class AlwaysPass : IDiscoveryMinigame
        {
            public string Id => "always_pass";
            public MinigameChallenge Create(int seed, DiscoveryTierDef tier, float narrowing, DiscoveryBalance balance) =>
                new MinigameChallenge { minigameId = Id, seed = seed };
            public MinigameResult Evaluate(MinigameChallenge c, IList<float> p, float e, DiscoveryBalance b) =>
                new MinigameResult { valid = true, passed = true, score = 1f };
        }

        [Test]
        public void MinigamesArePluggable()
        {
            DiscoveryMinigames.Register(new AlwaysPass());
            try
            {
                RuneCatalog c = Custom(d => d.discoverySources.Find(s => s.id == "ancient_inscription").minigame = "always_pass");
                RuneSoulState s = SoulWith(c, "wind");
                DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 1);
                Assert.AreEqual("always_pass", session.challenge.minigameId);
                Assert.AreEqual(MinigameOutcome.Passed, DiscoveryFlow.SubmitMinigame(c, s, session, new List<float> { 0f }, 100f, 1L, out _));
            }
            finally { DiscoveryMinigames.Unregister("always_pass"); }
            Assert.AreEqual(DiscoveryMinigames.RuneTrace, DiscoveryMinigames.Get("always_pass").Id, "an unknown id falls back to rune tracing");
        }

        // ------------------------------------------------------------------ the state machine (design spec 17.1)
        [Test]
        public void ACorrectAnswerUnlocksTheConceptForGood()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 21, "wind.pressure");
            Assert.AreEqual(DiscoveryPhase.Minigame, session.phase);
            PassMinigame(c, s, session);
            Assert.AreEqual(DiscoveryPhase.Riddle, session.phase);
            DiscoveryQuestion q = session.Question;
            AnswerOutcome o = DiscoveryFlow.SubmitAnswer(c, s, session, q.correct, 9L, out string msg);
            Assert.AreEqual(AnswerOutcome.Learned, o);
            StringAssert.Contains("Pressure", msg);
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.pressure"), "permanently added to the player's knowledge");
            Assert.AreEqual(1, s.discovery.learned);
            Assert.AreEqual(0, s.discovery.burned.Count);
            Assert.AreEqual("learned", s.discovery.log[s.discovery.log.Count - 1].outcome);

            RuneSoulState saved = RuneSoulState.FromJson(s.ToJson());
            Assert.IsTrue(RuneRules.Knows(saved, "wind", "wind.pressure"), "and saved");
            RuneRules.Release(s, s.runes[0].instanceId);
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.pressure"), "a released rune does not forget what was discovered");
            Assert.IsTrue(session.phase == DiscoveryPhase.Learned);
        }

        [Test]
        public void AWrongAnswerBurnsTheSourceForThatConcept()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 21, "wind.pressure");
            PassMinigame(c, s, session);
            int wrong = (session.Question.correct + 1) % session.Question.options.Count;
            AnswerOutcome o = DiscoveryFlow.SubmitAnswer(c, s, session, wrong, 9L, out string msg);
            Assert.AreEqual(AnswerOutcome.Wrong, o);
            StringAssert.Contains("another place", msg);
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.pressure"), "the concept stays undiscovered");
            Assert.IsTrue(DiscoveryRules.IsBurned(s, "ancient_inscription", "wind.pressure"));
            Assert.Contains("ancient_inscription|wind.pressure", s.discovery.burned);
            Assert.AreEqual(1, s.discovery.failed);

            // No indefinite retries on the same source.
            DiscoveryStart again = DiscoveryFlow.Start(c, s, "ancient_inscription", 0, 0, "wind.pressure", Held(s), new Random(1), 0f);
            Assert.IsTrue(again.ok);
            Assert.AreNotEqual("wind.pressure", again.session.conceptId, "the burned concept is not offered by this source again");
            for (int i = 0; i < 80; i++)
                Assert.AreNotEqual("wind.pressure", StartAt(c, s, "ancient_inscription", 1, 100 + i).conceptId);

            // Another source of the same concept can still teach it.
            Assert.IsFalse(DiscoveryRules.IsBurned(s, "shrine_of_principles", "wind.pressure"));
            DiscoverySession other = StartAt(c, s, "shrine_of_principles", 1, 5, "wind.pressure");
            Assert.AreEqual("wind.pressure", other.conceptId);

            RuneSoulState saved = RuneSoulState.FromJson(s.ToJson());
            Assert.IsTrue(DiscoveryRules.IsBurned(saved, "ancient_inscription", "wind.pressure"), "the burn is saved");
        }

        [Test]
        public void ASourceWithNothingLeftSaysSo()
        {
            RuneCatalog c = Custom(d =>
            {
                var only = new DiscoverySourceDef { id = "single", concepts = new List<string> { "wind.flow" }, runes = new List<string> { "wind" } };
                d.discoverySources.Add(only);
                // Restrict it to one concept by giving it a rune set that has just that concept: a custom rune.
                d.runes.Add(new RuneDef
                {
                    id = "lone", displayName = "Lone", alignments = new List<string> { "wind" },
                    concepts = new List<ConceptDef> { new ConceptDef { id = "lone.only", displayName = "Only", rarity = RuneRarity.Common, aspects = new List<AspectWeight> { new AspectWeight("motion", 1f) } } },
                });
                only.runes = new List<string> { "lone" };
                only.concepts.Clear();
            });
            RuneSoulState s = SoulWith(c, "lone");
            DiscoverySession session = StartAt(c, s, "single", 0, 1);
            Assert.AreEqual("lone.only", session.conceptId);
            PassMinigame(c, s, session);
            DiscoveryFlow.SubmitAnswer(c, s, session, (session.Question.correct + 1) % session.Question.options.Count, 1L, out _);
            DiscoveryStart none = DiscoveryFlow.Start(c, s, "single", 0, 0, null, Held(s), new Random(1), 0f);
            Assert.IsFalse(none.ok);
            StringAssert.Contains("Find another source", none.reason);
        }

        [Test]
        public void FailingTheMinigameUsesAnAttemptAndTheLastOneBurns()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            int attempts = c.Discovery.Tier(0).attempts;
            Assert.AreEqual(3, attempts);
            string concept = null;
            for (int i = 1; i <= attempts; i++)
            {
                DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 40, "wind.flow");
                concept = session.conceptId;
                MinigameOutcome o = DiscoveryFlow.SubmitMinigame(c, s, session, Hasty(session.challenge), 100f, 1L, out string msg);
                Assert.AreEqual(i < attempts ? MinigameOutcome.Failed : MinigameOutcome.Burned, o, msg);
                Assert.AreEqual(i, DiscoveryRules.AttemptsUsed(s, "ancient_inscription", concept));
                if (i < attempts) { StringAssert.Contains("try again", msg); Assert.IsFalse(DiscoveryRules.IsBurned(s, "ancient_inscription", concept)); }
            }
            Assert.IsTrue(DiscoveryRules.IsBurned(s, "ancient_inscription", concept), "the last failed try burns the source for the concept");
            Assert.IsFalse(RuneRules.Knows(s, "wind", concept));
        }

        [Test]
        public void AnInvalidReportCountsAsAFailedTry()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 40, "wind.flow");
            MinigameOutcome o = DiscoveryFlow.SubmitMinigame(c, s, session, new List<float> { 0f }, 100f, 1L, out string msg);
            Assert.AreEqual(MinigameOutcome.Failed, o);
            StringAssert.Contains("not all sealed", msg);
            Assert.AreEqual(1, DiscoveryRules.AttemptsUsed(s, "ancient_inscription", "wind.flow"), "probing the server with bad reports costs tries");
            Assert.AreEqual(MinigameOutcome.None, DiscoveryFlow.SubmitMinigame(c, s, session, new List<float>(), 100f, 1L, out _), "no minigame is being played now");
        }

        [Test]
        public void AdvancedChallengesNeedEveryStepRight()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 2, 8, "wind.turbulence");
            PassMinigame(c, s, session);
            Assert.AreEqual(2, session.questions.Count);
            Assert.AreEqual(AnswerOutcome.NextStep, DiscoveryFlow.SubmitAnswer(c, s, session, session.Question.correct, 1L, out _));
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.turbulence"), "not yet: one more step");
            Assert.AreEqual(DiscoveryPhase.Riddle, session.phase);
            int wrong = (session.Question.correct + 1) % session.Question.options.Count;
            Assert.AreEqual(AnswerOutcome.Wrong, DiscoveryFlow.SubmitAnswer(c, s, session, wrong, 1L, out _));
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.turbulence"));
            Assert.IsTrue(DiscoveryRules.IsBurned(s, "ancient_inscription", "wind.turbulence"));

            DiscoverySession win = StartAt(c, s, "shrine_of_principles", 2, 8, "wind.turbulence");
            PassMinigame(c, s, win);
            DiscoveryFlow.SubmitAnswer(c, s, win, win.Question.correct, 1L, out _);
            Assert.AreEqual(AnswerOutcome.Learned, DiscoveryFlow.SubmitAnswer(c, s, win, win.Question.correct, 1L, out _));
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.turbulence"));
        }

        [Test]
        public void LeavingARiddleCountsAsWrongAndLeavingTheMinigameCostsATry()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession riddle = StartAt(c, s, "ancient_inscription", 0, 3, "wind.flow");
            PassMinigame(c, s, riddle);
            Assert.IsTrue(DiscoveryFlow.Abandon(c, s, riddle, false, 1L, out string msg), msg);
            Assert.IsTrue(DiscoveryRules.IsBurned(s, "ancient_inscription", "wind.flow"), "reading the riddle and walking away cannot be used to re-roll it");
            Assert.AreEqual("abandoned", s.discovery.log[s.discovery.log.Count - 1].outcome);

            DiscoverySession game = StartAt(c, s, "shrine_of_principles", 0, 3, "wind.flow");
            Assert.IsFalse(DiscoveryFlow.Abandon(c, s, game, true, 1L, out _), "the first try only");
            Assert.AreEqual(1, DiscoveryRules.AttemptsUsed(s, "shrine_of_principles", "wind.flow"));
            Assert.AreEqual("expired", s.discovery.log[s.discovery.log.Count - 1].outcome);
            Assert.IsFalse(DiscoveryFlow.Abandon(c, s, game, false, 1L, out _), "already over");

            RuneCatalog forgiving = Custom(d => d.balance.discovery.abandonBurns = false);
            RuneSoulState t = SoulWith(forgiving, "wind");
            DiscoverySession kind = StartAt(forgiving, t, "ancient_inscription", 0, 3, "wind.flow");
            PassMinigame(forgiving, t, kind);
            Assert.IsFalse(DiscoveryFlow.Abandon(forgiving, t, kind, false, 1L, out _));
            Assert.IsFalse(DiscoveryRules.IsBurned(t, "ancient_inscription", "wind.flow"));
        }

        [Test]
        public void ASystemInterruptionCostsNothing()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession riddle = StartAt(c, s, "ancient_inscription", 0, 3, "wind.flow");
            PassMinigame(c, s, riddle);
            DiscoveryFlow.Cancel(riddle);
            Assert.IsFalse(riddle.Active);
            Assert.AreEqual(0, s.discovery.burned.Count, "dying at a riddle does not burn the source");
            Assert.AreEqual(0, s.discovery.failed);
        }

        // ------------------------------------------------------------------ secrecy (design spec 17.1; the answer never reaches the client)
        [Test]
        public void TheViewSentToThePlayerHoldsNoSecret()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, "wind");
            DiscoverySession session = StartAt(c, s, "ancient_inscription", 0, 21, "wind.pressure");
            DiscoveryView mini = DiscoveryFlow.ViewOf(c, s, session, "", 1);
            string json = UnityEngine.JsonUtility.ToJson(mini);
            Assert.IsNotNull(mini.challenge);
            StringAssert.DoesNotContain("wind.pressure", json);
            StringAssert.DoesNotContain("Pressure", json, "the minigame view does not reveal which concept is at stake");
            Assert.AreEqual(DiscoveryPhase.Minigame, mini.phase);

            PassMinigame(c, s, session);
            DiscoveryView riddle = DiscoveryFlow.ViewOf(c, s, session, "", 2);
            string rjson = UnityEngine.JsonUtility.ToJson(riddle);
            Assert.IsNull(riddle.challenge);
            Assert.AreEqual(session.Question.options.Count, riddle.options.Count);
            StringAssert.DoesNotContain("wind.pressure", rjson, "no concept id");
            StringAssert.DoesNotContain("correct", rjson.ToLowerInvariant(), "no field says which answer is right");
            StringAssert.DoesNotContain("ancient_inscription|", rjson, "no burn key");
            // Nothing on the view type can carry the answer: its fields are the whole of what is sent.
            foreach (var f in typeof(DiscoveryView).GetFields())
                Assert.That(f.Name, Does.Not.Contain("concept").IgnoreCase.And.Not.Contain("correct").IgnoreCase.And.Not.Contain("answer").IgnoreCase);
            Assert.IsNull(riddle.learnedName == "" ? null : riddle.learnedName, "the name appears only once learned");

            DiscoveryFlow.SubmitAnswer(c, s, session, session.Question.correct, 1L, out _);
            DiscoveryView done = DiscoveryFlow.ViewOf(c, s, session, "ok", 3);
            Assert.AreEqual(DiscoveryPhase.Learned, done.phase);
            Assert.AreEqual("Pressure", done.learnedName);
        }

        // ------------------------------------------------------------------ data
        [Test]
        public void ContentPacksAddSourcesAndRiddlesWithoutCodeChanges()
        {
            RuneMagicData pack = new RuneMagicData();
            pack.discoverySources.Add(new DiscoverySourceDef { id = "moon_well", displayName = "Moon Well", runes = new List<string> { "water" } });
            pack.riddles.Add(new RiddleDef { id = "water.condensation.pack", concept = "water.flow", text = "I follow the lowest path.\nWhat Concept am I?" });
            RuneMagicData main = RuneDefaults.Create();
            RuneCatalog c = RuneCatalog.Compile(main, new List<RuneMagicData> { pack });
            Assert.IsNotNull(c.DiscoverySource("moon_well"));
            Assert.AreEqual(1, c.RiddlesOf("water.flow").Count);
            Assert.AreEqual(c.DiscoverySources.Count, RuneDefaults.DiscoverySources().Count + 1);
            // The same id replaces.
            pack.discoverySources[0].id = "ancient_inscription";
            pack.discoverySources[0].displayName = "Replaced";
            RuneCatalog r = RuneCatalog.Compile(main, new List<RuneMagicData> { pack });
            Assert.AreEqual("Replaced", r.DiscoverySource("ancient_inscription").Name);
        }

        [Test]
        public void ValidationReportsBrokenDiscoveryData()
        {
            RuneCatalog c = Custom(d =>
            {
                d.discoverySources.Add(new DiscoverySourceDef { id = "bad|id" });
                d.discoverySources.Add(new DiscoverySourceDef { id = "ghost", runes = new List<string> { "no_rune" }, concepts = new List<string> { "no.concept" } });
                d.riddles.Add(new RiddleDef { id = "r1", concept = "no.concept", text = "x" });
                d.riddles.Add(new RiddleDef { id = "r2", concept = "wind.flow", text = "", distractors = new List<string> { "wind.flow", "nope.x" } });
                d.balance.discovery.tiers[0].rarityWeights = new List<float> { 0, 0, 0, 0, 0 };
            });
            string all = string.Join("\n", c.Errors);
            StringAssert.Contains("may not contain '|'", all);
            StringAssert.Contains("unknown rune 'no_rune'", all);
            StringAssert.Contains("unknown concept 'no.concept'", all);
            StringAssert.Contains("Riddle 'r2' has no text", all);
            StringAssert.Contains("offers its own answer", all);
            StringAssert.Contains("offers the unknown concept 'nope.x'", all);
            StringAssert.Contains("can teach no concept", all);
        }

        [Test]
        public void CoverageFindsConceptsNothingTeaches()
        {
            RuneCatalog c = Custom(d => d.discoverySources.RemoveAll(s => s.id != "ancient_inscription"));
            var unreachable = new List<ConceptDef>();
            var fragile = new List<ConceptDef>();
            DiscoveryCoverage.Find(c, unreachable, fragile);
            Assert.AreEqual(0, unreachable.Count, "the general inscription still teaches everything");
            Assert.Greater(fragile.Count, 100, "but each concept has one source only");
            Assert.IsTrue(new List<string>(c.Warnings).Exists(w => w.Contains("single source")), string.Join("\n", c.Warnings));
            RuneCatalog none = Custom(d => d.discoverySources.Clear());
            Assert.IsTrue(new List<string>(none.Warnings).Exists(w => w.Contains("No discovery sources")));
        }

        [Test]
        public void OlderDatabasesGetTheDiscoveryDataOnUpgrade()
        {
            RuneMagicData d = RuneDefaults.Create();
            d.version = 3;
            d.discoverySources.Clear();
            d.riddles.Clear();
            d.balance.discovery.tiers.Clear();
            Assert.IsTrue(d.Upgrade());
            Assert.AreEqual(5, d.balance.discovery.tiers.Count);
            Assert.Greater(d.discoverySources.Count, 5);
            Assert.Greater(d.riddles.Count, 5);
            Assert.AreEqual(RuneMagicData.CurrentVersion, d.version);
        }

        [Test]
        public void TheSavedDiscoveryStateRoundTrips()
        {
            var s = new RuneSoulState();
            s.discovery.burned.Add("ancient_inscription|wind.pressure");
            s.discovery.seenRiddles.Add("wind.pressure.1");
            s.discovery.attempts.Add(new KeyCount { key = "shrine_of_principles|wind.flow", count = 2 });
            s.discovery.log.Add(new DiscoveryLogEntry { utc = 5, source = "a", concept = "wind.flow", tier = 2, outcome = "wrong" });
            s.discovery.learned = 3;
            s.discovery.failed = 4;
            RuneSoulState back = RuneSoulState.FromJson(s.ToJson());
            Assert.AreEqual("ancient_inscription|wind.pressure", back.discovery.burned[0]);
            Assert.AreEqual("wind.pressure.1", back.discovery.seenRiddles[0]);
            Assert.AreEqual(2, back.discovery.attempts[0].count);
            Assert.AreEqual("wrong", back.discovery.log[0].outcome);
            Assert.AreEqual(3, back.discovery.learned);
            RuneSoulState older = RuneSoulState.FromJson("{\"version\":2,\"soul\":{}}");
            Assert.IsNotNull(older.discovery, "a save from before discovery loads with an empty discovery state");
            Assert.AreEqual(0, older.discovery.burned.Count);
        }

        [Test]
        public void TheLogIsTrimmed()
        {
            RuneCatalog c = Custom(d => d.balance.discovery.logLength = 3);
            RuneSoulState s = SoulWith(c, "wind");
            for (int i = 0; i < 6; i++)
            {
                RuneSoulState fresh = s;
                DiscoverySession session = StartAt(c, fresh, i % 2 == 0 ? "ancient_inscription" : "shrine_of_principles", 0, i + 1);
                DiscoveryFlow.Abandon(c, fresh, session, false, i, out _);
            }
            Assert.LessOrEqual(s.discovery.log.Count, 3);
            Assert.AreEqual(5L, s.discovery.log[s.discovery.log.Count - 1].utc, "the newest entries are kept");
        }

        [Test]
        public void TheRequestKindsAreAppendOnly()
        {
            Assert.AreEqual(5, (int)RuneRequestKind.DiscoveryMinigame);
            Assert.AreEqual(6, (int)RuneRequestKind.DiscoveryAnswer);
            Assert.AreEqual(7, (int)RuneRequestKind.DiscoveryAbandon);
            Assert.AreEqual(8, (int)RuneRequestKind.DismissSpell, "added by Phase 5, after the discovery kinds");
            Assert.AreEqual(9, (int)RuneRequestKind.Synthesize, "added by Phase 6, after DismissSpell");
            Assert.AreEqual(10, (int)RuneRequestKind.AssignSpell, "added by Phase 7, after Synthesize");
            Assert.AreEqual(11, (int)RuneRequestKind.UnassignSpell);
            Assert.AreEqual(12, (int)RuneRequestKind.SpellingStart, "added by Phase 8, after UnassignSpell");
            Assert.AreEqual(13, (int)RuneRequestKind.SpellingResult);
            Assert.AreEqual(14, (int)RuneRequestKind.GrimoireOpen, "added by the grimoire update, after SpellingResult");
            Assert.AreEqual(15, (int)RuneRequestKind.GrimoireImprint);
            Assert.AreEqual(16, (int)RuneRequestKind.GrimoireAbsorb);
            Assert.AreEqual(17, (int)RuneRequestKind.GrimoireErase);
            Assert.AreEqual(RuneRequestKind.GrimoireErase, RuneNetLimits.LastKind);
            Assert.AreEqual(1, (int)RuneRequestKind.Meditate, "earlier values are unchanged");
        }
    }
}
#endif
