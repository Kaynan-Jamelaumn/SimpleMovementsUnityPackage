#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Phase 9: the acceptance checklist of the design spec (section 22), one test per item, named ACnn after the item's number in
    /// docs/09-Acceptance-Report.md. Each asserts the requirement itself through the rules the server runs; the detailed behaviour of each
    /// system has its own tests (see the report's evidence column). What can only be seen in the running game (the windows, the network,
    /// the ability bar) is listed in the report as such.
    /// </summary>
    public class AcceptanceChecklistTests
    {
        private const string Pyro = "WWWAVVV";

        private static RuneCatalog Catalog() => RuneDefaults.Catalog();
        private static RuneCatalog Custom(Action<RuneMagicData> edit) => SynthLab.Custom(edit);

        private static RuneSoulState Soul(RuneCatalog c, params (string rune, int rank, float energy)[] runes)
        {
            var s = new RuneSoulState();
            SoulRules.Normalize(c.Balance, s.soul, c.Balance.maxSoulIntegrity);
            foreach ((string rune, int rank, float energy) r in runes)
                RuneRules.Absorb(c, s, new RuneDrop { runeId = r.rune, rank = r.rank, quality = 50f, energy = r.energy }, new RuneOrigin { kind = "test" }, null, 1L);
            return s;
        }

        private static SpellRecord Spell(string effect = "offense.pierce", int rank = 1, float complexity = 0.5f, float power = 100f) =>
            new SpellRecord { spellId = "syn_t", name = "Test", effectId = effect, rank = rank, complexity = complexity, power = power, stability = 90f, quality = 60f };

        // ================================================================== Rune Acquisition and Management
        [Test]
        public void AC01_RunesComeFromMonstersDungeonsAndOtherSources()
        {
            RuneCatalog c = Catalog();
            RuneAcquisitionBalance a = c.Balance.acquisition;
            // Monsters: a boss kill drops runes of its species.
            var rng = new Random(1);
            List<RuneDrop> drops = Enumerable.Range(0, 30).SelectMany(_ => DropRules.Roll(c, RuneDropContext.For("fire_elemental", 0, MonsterRarity.Boss), rng)).ToList();
            Assert.IsNotEmpty(drops, "monsters drop runes");
            CollectionAssert.IsSubsetOf(drops.Select(d => d.runeId).Distinct().ToList(), c.Runes.Select(r => r.id).ToList());
            // Dungeons: deeper dungeons raise the chance (and the rank, quality and energy).
            RuneDropProfile profile = c.DropProfile("fire_elemental");
            Assert.Greater(DropRules.DropChance(a, profile, RuneDropContext.For("fire_elemental", 3, MonsterRarity.Normal)),
                           DropRules.DropChance(a, profile, RuneDropContext.For("fire_elemental", 0, MonsterRarity.Normal)));
            // Other sources (chests, merchants, quests, rewards): a rune of any origin is absorbed the same way.
            var s = new RuneSoulState();
            foreach (string origin in new[] { "drop", "item", "reward", "chest", "merchant", "quest" })
                Assert.IsNotNull(RuneRules.Absorb(c, s, new RuneDrop { runeId = "wind", rank = 1, quality = 50f }, new RuneOrigin { kind = origin }, null, 1L), origin);
            CollectionAssert.AreEquivalent(new[] { "drop", "item", "reward", "chest", "merchant", "quest" }, s.runes.Select(r => r.origin.kind).ToList());
        }

        [Test]
        public void AC02_RunesCanBeAbsorbedIntoTheSoul()
        {
            RuneCatalog c = Catalog();
            var s = new RuneSoulState();
            RuneInstance r = RuneRules.Absorb(c, s, new RuneDrop { runeId = "fire", rank = 2, quality = 70f, energy = 120f }, new RuneOrigin { kind = "drop" }, "r1", 5L);
            Assert.AreEqual(1, s.runes.Count);
            Assert.AreEqual("r1", r.instanceId);
            Assert.AreEqual(2, r.rank);
            Assert.AreEqual(70f, r.quality);
            Assert.AreEqual(120f, r.energy, 1e-3f);
        }

        [Test]
        public void AC03_SoulRuneCapacityIsLimitedAndEveryRuneOccupiesASlot()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            int capacity = CapacityRules.Capacity(b, 1, new SoulState());
            Assert.Greater(capacity, 0);
            Assert.Greater(CapacityRules.Capacity(b, 60, new SoulState()), capacity, "capacity grows with progression");
            var s = new RuneSoulState();
            for (int i = 0; i < capacity; i++)
            {
                Assert.AreEqual(i, RuneRules.UsedSlots(c, s));
                Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity, "wind", out string why), why);
                RuneRules.Absorb(c, s, new RuneDrop { runeId = "wind", rank = 1, quality = 50f }, new RuneOrigin { kind = "test" }, null, 1L);
            }
            Assert.AreEqual(capacity, RuneRules.UsedSlots(c, s), "each absorbed rune occupies a slot");
            Assert.IsFalse(RuneRules.CanAbsorb(c, s, capacity, "wind", out string full), "no free slot");
            StringAssert.Contains("room", full);
        }

        [Test]
        public void AC04_ThePlayerManagesTheRuneCollection()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 300f), ("fire", 1, 100f));
            string id = s.runes[0].instanceId;
            Assert.IsTrue(RuneRules.CanRelease(c.Balance.acquisition, s, id, false, out string why), why);
            Assert.IsFalse(RuneRules.CanRelease(c.Balance.acquisition, s, id, true, out _), "not in combat");
            RuneInstance released = RuneRules.Release(s, id);
            Assert.IsNotNull(released);
            Assert.AreEqual(1, s.runes.Count);
            Assert.AreEqual(1, RuneRules.UsedSlots(c, s), "the slot is free again");
            // A rune that anchors a spell is not released until the spell is dismissed.
            RuneSoulState anchored = Soul(c, ("wind", 1, 300f));
            anchored.spells.Add(new SpellRecord { spellId = "syn_x", name = "X", anchors = new List<string> { anchored.runes[0].instanceId } });
            Assert.IsFalse(RuneRules.CanRelease(c.Balance.acquisition, anchored, anchored.runes[0].instanceId, false, out string reason));
            StringAssert.Contains("spell", reason.ToLowerInvariant());
        }

        [Test]
        public void AC05_CombiningRunesCanFreeSlots()
        {
            var lab = new SynthLab();
            List<string> ids = lab.Pattern("WWWWWWW");
            var req = new SynthesisRequest { mode = SynthesisMode.RankAdvancement, seed = 4 };
            foreach (string id in ids) req.slots.Add(new SynthesisSlot(id, "", 0f));
            lab.Fill(req);
            Assert.AreEqual(7, RuneRules.UsedSlots(lab.c, lab.s));
            Assert.IsTrue(lab.Execute(req).performed);
            Assert.AreEqual(1, RuneRules.UsedSlots(lab.c, lab.s), "seven runes became one: six slots are free");
        }

        [Test]
        public void AC06_RuneRankAndQualityAreSupported()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            RuneSoulState s = Soul(c, ("fire", 3, 0f));
            Assert.AreEqual(3, s.runes[0].rank);
            Assert.Greater(b.MaxEnergy(3, 50f), b.MaxEnergy(1, 50f), "a higher rank holds more energy");
            Assert.IsNotEmpty(b.rankTitles);
            Assert.IsNotEmpty(b.qualityTiers);
            var weak = new SynthLab();
            var strong = new SynthLab();
            Assert.Less(weak.Evaluate(weak.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 10f)).score,
                        strong.Evaluate(strong.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 60f)).score, "quality counts in a synthesis");
        }

        // ================================================================== Rune Energy
        [Test]
        public void AC07_KillsGrantMagicalEnergyAndThePlayerChoosesTheReceivingRune()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 0f), ("fire", 1, 0f));
            float reward = DropRules.EnergyReward(c, RuneDropContext.For("wind_beast", 0, MonsterRarity.Normal));
            Assert.Greater(reward, 0f, "a kill grants energy");
            EnergyRules.AddPending(c.Balance, s, reward);
            Assert.AreEqual(reward, s.pendingEnergy, 1e-3f, "it waits until the player chooses");
            EnergyRules.Assign(c.Balance, s, s.runes[1].instanceId, 0f);
            Assert.AreEqual(0f, s.runes[0].energy, "only the chosen rune received it");
            Assert.AreEqual(reward, s.runes[1].energy, 1e-3f);
        }

        [Test]
        public void AC08_EveryRuneHasAMaximumEnergyAndAFullRuneCannotReceiveMore()
        {
            RuneCatalog c = Catalog();
            float max = c.Balance.MaxEnergy(1, 50f);
            RuneSoulState s = Soul(c, ("fire", 1, max));
            Assert.IsTrue(EnergyRules.IsFull(c.Balance, s.runes[0]));
            EnergyRules.AddPending(c.Balance, s, 100f);
            EnergyTransfer t = EnergyRules.Assign(c.Balance, s, s.runes[0].instanceId, 0f);
            Assert.AreEqual(EnergyRefusal.Full, t.refusal);
            Assert.AreEqual(max, s.runes[0].energy, 1e-3f);
            Assert.AreEqual(100f, s.pendingEnergy, 1e-3f, "nothing is lost: choose another rune");
        }

        [Test]
        public void AC09_FullEnergyDoesNotTriggerRankAdvancement()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("fire", 1, 0f));
            EnergyRules.AddPending(c.Balance, s, 1e6f);
            EnergyRules.Assign(c.Balance, s, s.runes[0].instanceId, 0f);
            Assert.IsTrue(EnergyRules.IsFull(c.Balance, s.runes[0]));
            Assert.AreEqual(1, s.runes[0].rank, "the rank never changes by itself");
            Assert.AreEqual(1, s.runes.Count);
        }

        [Test]
        public void AC10_CombinationsConsumeEnergyFromTheInputRunes()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            var before = lab.s.runes.ToDictionary(r => r.instanceId, r => r.energy);
            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed, r.message);
            foreach (SynthesisSlot slot in req.slots)
                Assert.AreEqual(before[slot.runeInstanceId] - slot.energy, lab.s.Rune(slot.runeInstanceId).energy, 0.05f, "the allocated energy left the rune");
            Assert.Greater(r.energyConsumed, 0f);
        }

        [Test]
        public void AC11_InsufficientEnergyCausesSynthesisFailureItCannotProceed()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro, 0.3f);
            string before = lab.Json();
            SynthesisResult r = lab.Execute(req);
            Assert.IsFalse(r.performed);
            Assert.AreEqual(SynthesisRefusal.InsufficientEnergy, r.refusal);
            Assert.AreEqual(before, lab.Json(), "it cannot proceed: nothing changed");
        }

        [Test]
        public void AC12_ExcessiveEnergyMayInterfereAndEnergyRatiosInfluenceTheOutcome()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ideal = lab.Evaluate(lab.Ready(Pyro));
            SynthesisEvaluation excess = lab.Evaluate(lab.Ready(Pyro, 3f));
            Assert.Greater(excess.interference, 0f);
            Assert.Less(excess.score, ideal.score);
            SynthesisEvaluation skewed = lab.Evaluate(lab.Fill(lab.Request(Pyro, lab.Pattern(Pyro)), 1f, new[] { 1f, 1f, 1f, 2.5f, 1f, 1f, 1f }));
            Assert.Less(skewed.score, ideal.score, "ratios, not only amounts");
        }

        // ================================================================== Rune Synthesis
        [Test]
        public void AC13_SynthesisSelectsIntentionEffectRunesAndOneConceptPerRune()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            Assert.AreEqual("attack", req.intentionId);
            Assert.AreEqual("offense.explosion", req.effectId);
            Assert.AreEqual(7, req.slots.Count);
            Assert.IsTrue(req.slots.All(s => !string.IsNullOrEmpty(s.conceptId)), "one concept chosen for each rune");
            SynthesisRequest noConcept = req.Clone();
            noConcept.slots[2].conceptId = "";
            SynthesisResult refused = lab.Execute(noConcept);
            Assert.IsFalse(refused.performed);
            Assert.AreEqual(SynthesisRefusal.ConceptRequired, refused.refusal);
            // Multiple runes: 2 up to the balance's maximum are accepted, more are refused (a single rune is allowed too, so that a composite
            // rune, which already embodies many, can be used alone through its essence: Spell Balance... Synthesis Engine ▸ Min Runes).
            Assert.IsTrue(lab.Execute(lab.Ready("WA")).performed, "two runes");
            var big = new SynthLab();
            big.Pattern(new string('W', lab.c.Synthesis.maxRunes + 1));
            SynthesisRequest tooMany = big.Request(new string('W', lab.c.Synthesis.maxRunes + 1), big.ids);
            foreach (SynthesisSlot sl in tooMany.slots) sl.energy = 10f;
            Assert.AreEqual(SynthesisRefusal.TooManyRunes, big.Execute(tooMany).refusal);
            Assert.GreaterOrEqual(lab.c.Synthesis.maxRunes, 7, "enough runes for the spec's seven-rune example");
        }

        [Test]
        public void AC14_RuneOrderingAndEnergyAllocationAreDecidedByThePlayerAndMatter()
        {
            var lab = new SynthLab();
            SynthesisRequest ideal = lab.Ready(Pyro);
            SynthesisRequest reversed = ideal.Clone();
            reversed.slots.Reverse();
            Assert.Less(lab.Evaluate(reversed).score, lab.Evaluate(ideal).score, "the order matters");
            SynthesisRequest less = lab.Fill(lab.Request(Pyro, lab.Pattern(Pyro)), 0.75f);
            Assert.Less(lab.Evaluate(less).score, lab.Evaluate(ideal).score, "the allocation matters");
        }

        [Test]
        public void AC15_TheSystemEvaluatesCompatibilityAndFeasibility()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ok = lab.Evaluate(lab.Ready(Pyro));
            Assert.AreEqual(1f, ok.factors.compatibility, 1e-3f);
            Assert.AreEqual(1f, ok.factors.coverage, 1e-3f, "feasibility: the concepts can provide the effect");
            SynthesisEvaluation infeasible = lab.Evaluate(lab.Ready("FFww"));
            Assert.IsTrue(infeasible.Incoherent);
            SynthesisResult wrongPurpose = lab.Execute(lab.Ready(Pyro, 1f, "defense", "offense.explosion"));
            Assert.AreEqual(SynthesisTier.CompleteFailure, wrongPurpose.tier, "a purpose that does not support the effect");
            Assert.IsNotNull(SynthesisEngine.Preview(lab.c, lab.s, lab.ctx, lab.Ready(Pyro)), "and the player can preview the requirements");
        }

        [Test]
        public void AC16_OutcomesFollowTheQualityScoreTiers()
        {
            RuneBalance b = Catalog().Balance;
            Assert.AreEqual(SynthesisTier.Perfect, b.TierOf(100f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(99f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(70f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(69f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(30f));
            Assert.AreEqual(SynthesisTier.CompleteFailure, b.TierOf(29f));
            var lab = new SynthLab();
            SynthesisResult failure = lab.Execute(lab.Ready("WWWWWWW"));
            Assert.AreEqual(SynthesisTier.CompleteFailure, failure.tier);
            Assert.Greater(failure.energyLost, 0f, "a failure has consequences");
            Assert.IsNotEmpty(failure.soulDamage);
        }

        [Test]
        public void AC17_ComplexCombinationsBeyondFixedRecipesAreSupported()
        {
            var lab = new SynthLab();
            Assert.AreEqual("", lab.Evaluate(lab.Ready("WAV")).archetypeId, "a formula that is not a recipe");
            Assert.AreEqual(100f, lab.Evaluate(lab.Ready("WAV")).score, 0.001f, "still a perfect explosion");
            Assert.IsFalse(lab.Evaluate(lab.Ready("WWFVV")).Incoherent);
            Assert.AreEqual("pyroclastic_resonance", lab.Evaluate(lab.Ready(Pyro)).archetypeId, "and the spec's example is recognised by name, not required");
        }

        // ================================================================== Concept Discovery
        [Test]
        public void AC18_EveryRuneHasMultipleHiddenConceptsDiscoveredIndividually()
        {
            RuneCatalog c = Catalog();
            Assert.IsTrue(c.Runes.All(r => r.concepts.Count >= 2), "multiple concepts per rune");
            RuneSoulState s = Soul(c, ("wind", 1, 0f));
            Assert.IsEmpty(RuneRules.KnownConcepts(s, "wind"), "hidden at first");
            RuneRules.Learn(s, "wind", "wind.pressure");
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.pressure"));
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.flow"), "each concept is discovered on its own");
        }

        [Test]
        public void AC19_DiscoveryThroughDungeonSourcesMinigamesAndRiddles()
        {
            RuneCatalog c = Catalog();
            Assert.IsNotEmpty(c.DiscoverySources, "dungeon knowledge sources exist");
            RuneSoulState s = Soul(c, ("wind", 1, 0f));
            DiscoveryStart st = DiscoveryFlow.Start(c, s, "ancient_inscription", 0, 0, "wind.pressure", new List<string> { "wind" }, new Random(21), 100f);
            Assert.IsTrue(st.ok, st.reason);
            Assert.AreEqual(DiscoveryPhase.Minigame, st.session.phase, "the minigame first");
            Assert.IsNotNull(st.session.challenge);
            List<float> presses = Perfect(st.session.challenge, c.Discovery.minPressInterval);
            Assert.AreEqual(MinigameOutcome.Passed, DiscoveryFlow.SubmitMinigame(c, s, st.session, presses, st.session.minigameIssuedAt + presses.Last() + 0.1f, 5L, out _));
            Assert.AreEqual(DiscoveryPhase.Riddle, st.session.phase, "then a question about the hidden concept");
            Assert.GreaterOrEqual(st.session.Question.options.Count, 2);
        }

        private static List<float> Perfect(MinigameChallenge ch, float minInterval)
        {
            var presses = new List<float>();
            float t = 0f;
            foreach (TraceNode n in ch.nodes)
            {
                float at = (((n.center - ch.phase) % 1f + 1f) % 1f) / ch.speed;
                while (at < t + minInterval) at += 1f / ch.speed;
                presses.Add(at);
                t = at;
            }
            return presses;
        }

        private static DiscoverySession Pass(RuneCatalog c, RuneSoulState s, string source, int band, string concept, int seed)
        {
            DiscoveryStart st = DiscoveryFlow.Start(c, s, source, 0, band, concept, s.runes.Select(r => r.runeId).Distinct().ToList(), new Random(seed), 100f);
            Assert.IsTrue(st.ok, st.reason);
            List<float> presses = Perfect(st.session.challenge, c.Discovery.minPressInterval);
            Assert.AreEqual(MinigameOutcome.Passed, DiscoveryFlow.SubmitMinigame(c, s, st.session, presses, st.session.minigameIssuedAt + presses.Last() + 0.1f, 5L, out _));
            return st.session;
        }

        [Test]
        public void AC20_CorrectAnswersUnlockForGoodAndIncorrectOnesInvalidateTheSource()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 0f));
            DiscoverySession right = Pass(c, s, "ancient_inscription", 0, "wind.pressure", 21);
            Assert.AreEqual(AnswerOutcome.Learned, DiscoveryFlow.SubmitAnswer(c, s, right, right.Question.correct, 9L, out _));
            Assert.IsTrue(RuneRules.Knows(RuneSoulState.FromJson(s.ToJson()), "wind", "wind.pressure"), "permanent, and saved");

            RuneSoulState t = Soul(c, ("wind", 1, 0f));
            DiscoverySession wrong = Pass(c, t, "ancient_inscription", 0, "wind.pressure", 21);
            Assert.AreEqual(AnswerOutcome.Wrong, DiscoveryFlow.SubmitAnswer(c, t, wrong, (wrong.Question.correct + 1) % wrong.Question.options.Count, 9L, out _));
            Assert.IsFalse(RuneRules.Knows(t, "wind", "wind.pressure"));
            Assert.IsTrue(DiscoveryRules.IsBurned(t, "ancient_inscription", "wind.pressure"), "the source is invalidated for that concept");
        }

        [Test]
        public void AC21_RetryingRequiresAnotherDungeonSource()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 0f));
            DiscoverySession wrong = Pass(c, s, "ancient_inscription", 0, "wind.pressure", 21);
            DiscoveryFlow.SubmitAnswer(c, s, wrong, (wrong.Question.correct + 1) % wrong.Question.options.Count, 9L, out _);
            for (int seed = 0; seed < 60; seed++)
            {
                DiscoveryStart again = DiscoveryFlow.Start(c, s, "ancient_inscription", 0, 1, null, new List<string> { "wind" }, new Random(seed), 0f);
                if (again.ok) Assert.AreNotEqual("wind.pressure", again.session.conceptId, "the same source never offers it again");
            }
            DiscoveryStart other = DiscoveryFlow.Start(c, s, "shrine_of_principles", 0, 1, "wind.pressure", new List<string> { "wind" }, new Random(5), 0f);
            Assert.IsTrue(other.ok, other.reason);
            Assert.AreEqual("wind.pressure", other.session.conceptId, "another source can teach it");
        }

        [Test]
        public void AC22_RareConceptsNeedHarderChallenges()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 0f));
            ConceptDef rare = c.Rune("wind").concepts.First(k => k.rarity == RuneRarity.Rare);
            DiscoverySourceDef src = c.DiscoverySource("ancient_inscription");
            var held = new List<string> { "wind" };
            bool beginner = DiscoveryRules.Candidates(c, s, src, 0, held, out _).Any(k => k.concept.id == rare.id);
            bool advanced = DiscoveryRules.Candidates(c, s, src, 2, held, out _).Any(k => k.concept.id == rare.id);
            Assert.IsFalse(beginner, "a rare concept is not taught at the Beginner band");
            Assert.IsTrue(advanced, "it needs a harder band");
            DiscoveryTierDef easy = c.Discovery.Tier(0), hard = c.Discovery.Tier(c.Discovery.tiers.Count - 1);
            Assert.Greater(hard.minigameNodes, easy.minigameNodes, "the harder band asks more of the minigame");
            Assert.Greater(hard.steps, easy.steps, "and more riddle steps");
            Assert.Less(hard.clues, easy.clues, "with fewer clues");
        }

        // ================================================================== Soul Integrity
        [Test]
        public void AC23_SoulIntegrityIsIndependentOfPhysicalHealthAndRuneEnergy()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            RuneSoulState s = Soul(c, ("fire", 1, 400f));
            s.pendingEnergy = 50f;
            float max = b.maxSoulIntegrity;
            SoulRules.Damage(b, s.soul, max, 30f, "synthesis");
            Assert.AreEqual(400f, s.runes[0].energy, 1e-3f, "Soul Damage does not touch rune energy");
            Assert.AreEqual(50f, s.pendingEnergy, 1e-3f);
            float integrity = s.soul.integrity;
            EnergyRules.Consume(s.runes[0], 100f);
            Assert.AreEqual(integrity, s.soul.integrity, "spending rune energy does not touch the soul");
            // Nothing in the soul's data refers to health: the soul is its own resource.
            Assert.IsFalse(typeof(SoulState).GetFields(BindingFlags.Public | BindingFlags.Instance).Any(f => f.Name.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void AC24_SynthesisCausesSoulDamageAndSpellcastingCausesSoulStrain()
        {
            var lab = new SynthLab();
            SynthesisResult r = lab.Execute(lab.Ready(Pyro));
            Assert.IsNotEmpty(r.soulDamage, "synthesis causes Soul Damage");
            Assert.IsTrue(r.soulDamage.Any(d => d.amount > 0f));
            RuneCatalog c = Catalog();
            CompiledSpell cs = SpellCompiler.Compile(c, Spell());
            CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
            Assert.Greater(SpellCasting.Success(c.Balance, plan).amount, 0f, "casting causes Soul Strain by default");
            Assert.Greater(plan.strainScaled, 0f);
        }

        [Test]
        public void AC25_LowIntegrityRestrictsSynthesisAndWeakensCasting()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            SoulRestrictions Cond(float percent) =>
                SoulRules.Restrictions(b, new SoulState { initialized = true, integrity = b.maxSoulIntegrity * percent / 100f }, b.maxSoulIntegrity);
            SoulRestrictions stable = Cond(100f), critical = Cond(30f), nearCollapse = Cond(10f), collapsed = Cond(0f);
            Assert.IsTrue(stable.synthesisAllowed);
            Assert.IsFalse(critical.AllowsSynthesis(7, 0.5f, out _), "too many runes for a critical soul");
            Assert.IsFalse(nearCollapse.synthesisAllowed, "synthesis is restricted");
            Assert.IsFalse(collapsed.castingAllowed);
            Assert.Less(critical.powerMultiplier, stable.powerMultiplier, "weaker casting");
            Assert.Greater(critical.castTimeMultiplier, stable.castTimeMultiplier);
            Assert.Greater(critical.energyCostMultiplier, stable.energyCostMultiplier);
        }

        [Test]
        public void AC26_IntegrityRecoversThroughSpecificMechanicsAndSevereDamageNeedsSpecializedHealing()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            float max = b.maxSoulIntegrity;
            var soul = new SoulState { initialized = true, integrity = max };
            SoulRules.Damage(b, soul, max, 40f, "primordial"); // severe: scars the soul deeply
            Assert.Greater(soul.deepDamage, 0f);
            float ceiling = SoulRules.Ceiling(soul, max);
            Assert.Less(ceiling, max, "deep damage lowers what ordinary recovery can reach");
            SoulRules.Restore(b, soul, max, 1000f);
            Assert.AreEqual(ceiling, soul.integrity, 1e-3f, "rest, meditation and items stop at the ceiling");
            Assert.Greater(SoulRules.HealDeep(b, soul, max, 1000f), 0f, "specialized healing removes deep damage");
            SoulRules.Restore(b, soul, max, 1000f);
            Assert.AreEqual(max, soul.integrity, 1e-3f);
        }

        // ================================================================== Spellcasting
        [Test]
        public void AC27_CreatedEffectsCanBeCast()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready(Pyro));
            Assert.IsNotNull(made.spell);
            CompiledSpell cs = SpellCompiler.Compile(lab.c, made.spell);
            CastPlan plan = SpellCasting.Plan(lab.c, cs, SoulRestrictions.None);
            foreach (RuneInstance r in lab.s.runes) r.energy = 500f;
            Assert.IsTrue(plan.allowed);
            Assert.IsNotNull(SpellCasting.Draw(lab.s, made.spell, plan.energyCost), "the spell can be cast");
            Assert.IsNotNull(lab.c.FormOfEffect(made.spell.effectId), "its effect has an executable form");
        }

        [Test]
        public void AC28_EachSpellHasARuneEnergyCostAndSoulStrainAndPowerfulSpellsStrainMore()
        {
            RuneCatalog c = Catalog();
            CompiledSpell light = SpellCompiler.Compile(c, Spell(rank: 1, complexity: 0.2f));
            CompiledSpell powerful = SpellCompiler.Compile(c, Spell(rank: 4, complexity: 0.9f, power: 250f));
            Assert.Greater(light.energyCost, 0f);
            Assert.Greater(light.strain, 0f);
            Assert.Greater(powerful.energyCost, light.energyCost * 3f);
            Assert.Greater(powerful.strain, light.strain * 2f, "powerful spells may cause significant Soul Damage");
        }

        [Test]
        public void AC29_LowSoulIntegrityCausesCastingDebuffs()
        {
            RuneCatalog c = Catalog();
            CompiledSpell cs = SpellCompiler.Compile(c, Spell());
            RuneBalance b = c.Balance;
            CastPlan Plan(float percent) => SpellCasting.Plan(c, cs, SoulRules.Restrictions(b, new SoulState { initialized = true, integrity = b.maxSoulIntegrity * percent / 100f }, b.maxSoulIntegrity));
            CastPlan good = Plan(100f), bad = Plan(30f);
            Assert.Greater(bad.castTimeMultiplier, good.castTimeMultiplier);
            Assert.Less(bad.powerMultiplier, good.powerMultiplier);
            Assert.Greater(bad.energyCost, good.energyCost);
            Assert.Greater(bad.instability, good.instability, "and spells behave unstably");
        }

        [Test]
        public void AC30_SpellsSupportPowerStabilityCastingTimeRangeDurationAndArea()
        {
            RuneCatalog c = Catalog();
            CompiledSpell lowRank = SpellCompiler.Compile(c, Spell(rank: 1));
            CompiledSpell highRank = SpellCompiler.Compile(c, Spell(rank: 5, power: 200f));
            Assert.Greater(highRank.damage, lowRank.damage, "power");
            Assert.AreEqual(90f, lowRank.stability, "stability");
            Assert.Greater(lowRank.castTime, 0f, "casting time");
            Assert.Greater(highRank.range, lowRank.range, "range");
            Assert.Greater(highRank.area, lowRank.area, "area of effect");
            Assert.Greater(lowRank.duration, 0f, "duration");
            SpellModifiers m = SpellCompiler.ModifiersFor(highRank, SpellCasting.Plan(c, highRank, SoulRestrictions.None));
            Assert.Greater(m.range, 1f);
            Assert.Greater(m.area, 1f);
            Assert.Greater(m.damage, 1f);
        }

        [Test]
        public void AC31_SpellsCanBeAssignedToTheExistingAbilityUiSlots()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c);
            var sp = Spell();
            s.spells.Add(sp);
            Assert.IsTrue(SpellSlotRules.Assign(s, sp.spellId, 2, null, out string why), why);
            Assert.AreEqual(2, SpellSlotRules.SlotOf(s, sp.spellId));
            Assert.IsTrue(SpellCompiler.TryParseAbilityId(SpellCompiler.Compile(c, sp).abilityId, out _, out _), "it runs as an ordinary ability (the ability system's slot, hotkey and bar)");
            Assert.IsTrue(SpellSlotRules.Unassign(s, 2));
            Assert.IsNotNull(SpellRules.Find(s, sp.spellId), "unassigning keeps it in the spellbook");
        }

        [Test]
        public void AC32_TheSpellingSystemIsConfiguredByTheDeveloperNotThePlayer()
        {
            Assert.AreEqual(3, Enum.GetValues(typeof(SpellingMode)).Length, "All / Selected / None");
            RuneCatalog none = Custom(d => d.balance.spelling.mode = SpellingMode.None);
            RuneCatalog all = Custom(d => d.balance.spelling.mode = SpellingMode.All);
            RuneCatalog selected = Catalog();
            Assert.IsFalse(SpellingGenerator.Uses(none, Spell("offense.explosion")));
            Assert.IsTrue(SpellingGenerator.Uses(all, Spell("movement.dash")));
            Assert.IsTrue(SpellingGenerator.Uses(selected, Spell("offense.explosion")), "a per-spell flag (the rules table)");
            Assert.IsFalse(SpellingGenerator.Uses(selected, Spell("movement.dash")));
            // No player-facing option: nothing the player owns or sends carries a spelling setting.
            foreach (Type t in new[] { typeof(RuneSoulState), typeof(SoulState), typeof(RuneSoulView), typeof(RuneSoulRequest), typeof(SpellRecord), typeof(SpellSlotAssignment) })
                Assert.IsFalse(t.GetFields(BindingFlags.Public | BindingFlags.Instance).Any(f => f.Name.IndexOf("spelling", StringComparison.OrdinalIgnoreCase) >= 0 && f.Name.IndexOf("mode", StringComparison.OrdinalIgnoreCase) >= 0), t.Name);
            Assert.IsFalse(typeof(RuneMagicSettings).GetFields().Any(f => f.Name.IndexOf("spelling", StringComparison.OrdinalIgnoreCase) >= 0 && f.FieldType == typeof(bool)), "and no on/off switch for players in the settings either");
        }

        [Test]
        public void AC33_PerSpellDifficultyLettersWindowsRhythmSpeedBonusAndExtensibleEvaluation()
        {
            RuneCatalog c = Catalog();
            SpellRecord easy = Spell("offense.explosion", 1, 0.1f), hard = Spell("offense.explosion", 5, 1f, 250f);
            SpellingParams pe = SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, easy), easy, SpellingContext.Plain, 3);
            SpellingParams ph = SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, hard), hard, SpellingContext.Plain, 3);
            Assert.Greater(ph.letters, pe.letters, "variable letter count");
            Assert.Less(ph.windows[1], pe.windows[1], "time windows");
            SpellRecord beam = Spell("offense.incinerate");
            Assert.IsTrue(SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, beam), beam, SpellingContext.Plain, 3).rhythm, "rhythm");
            Assert.Greater(pe.speedBonusThreshold, 0f, "speed bonus");
            Assert.IsTrue(SpellingEvaluator.HasTerm("accuracy") && SpellingEvaluator.HasTerm("rhythm"));
            SpellingEvaluator.Register("ac33_extra", t => 1f);
            Assert.IsTrue(SpellingEvaluator.HasTerm("ac33_extra"), "the evaluation is extensible");
        }

        [Test]
        public void AC34_PerKeyTimeWindowsDifferWithinASpellAndBetweenSpells()
        {
            RuneCatalog c = Catalog();
            SpellRecord one = Spell("offense.explosion", 3, 0.7f), two = Spell("offense.explosion", 3, 0.2f);
            SpellingParams a = SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, one), one, SpellingContext.Plain, 11);
            SpellingParams b = SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, two), two, SpellingContext.Plain, 11);
            Assert.Greater(a.windows.Distinct().Count(), 1, "different windows for the keys of one spell");
            CollectionAssert.AreNotEqual(a.windows, b.windows, "and between spells");
            RuneCatalog ruled = Custom(d => { d.spellingRules.Clear(); d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes, letters = 3, keyWindows = new List<float> { 1.5f, 0.8f, 1.2f } }); });
            SpellingParams explicitly = SpellingGenerator.Resolve(ruled, SpellCompiler.Compile(ruled, Spell()), Spell(), SpellingContext.Plain, 1);
            CollectionAssert.AreEqual(new[] { 1.5f, 0.8f, 1.2f }, explicitly.windows, "the spec's example, as data");
        }

        [Test]
        public void AC35_SoulDamageOnSuccessMistakesAndFailureIsConfigurablePerSpellAndNeverAutomatic()
        {
            RuneCatalog c = Catalog();
            // (a) on a successful cast: a form that never strains the soul.
            RuneCatalog noStrain = Custom(d => d.spellForms.First(f => f.id == "bolt").strainMultiplier = 0f);
            CompiledSpell free = SpellCompiler.Compile(noStrain, Spell("offense.pierce"));
            Assert.AreEqual(0f, SpellCasting.Success(noStrain.Balance, SpellCasting.Plan(noStrain, free, SoulRestrictions.None)).amount);
            Assert.Greater(SpellCasting.Success(c.Balance, SpellCasting.Plan(c, SpellCompiler.Compile(c, Spell("offense.pierce")), SoulRestrictions.None)).amount, 0f, "other spells do");
            // (b) on mistakes in the Spelling System: none unless the spell's data says so.
            SpellRecord plain = Spell("offense.pierce"), marked = Spell("control.immobilize");
            Assert.AreEqual(SpellingDamageMode.None, SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, plain), plain, SpellingContext.Plain, 1).damageMode);
            Assert.AreEqual(SpellingDamageMode.FlatPerMistake, SpellingGenerator.Resolve(c, SpellCompiler.Compile(c, marked), marked, SpellingContext.Plain, 1).damageMode);
            // (c) on a failed cast: removable.
            RuneCatalog noFailure = Custom(d => { d.balance.spells.failureDamageFactor = 0f; d.balance.spelling.failureDamageFactor = 0f; });
            Assert.IsFalse(SpellCasting.Failure(noFailure.Balance, SpellCasting.Plan(noFailure, SpellCompiler.Compile(noFailure, Spell()), SoulRestrictions.None)).Any);
        }

        [Test]
        public void AC36_KeySequenceCastingRandomLettersRightInputActivatesWrongInputFailsOrSlowsTheCast()
        {
            RuneCatalog c = Catalog();
            SpellingKeyPool pool = SpellingKeyPool.Build(c.Spelling, new string[0], new string[0], new string[0], false);
            List<string> a = SpellingSequence.Generate(1, pool.keys, 5), b = SpellingSequence.Generate(2, pool.keys, 5);
            CollectionAssert.AreNotEqual(a, b, "random letters");
            SpellingParams lenient = new SpellingParams { letters = 3, windows = new List<float> { 1f, 1f, 1f }, totalLimit = 3f, model = SpellingFailureModel.Lenient, maxMistakes = 2, castTimePerMistake = 0.1f,
                weights = new List<SpellingWeight> { new SpellingWeight("accuracy", 1f) } };
            var letters = new List<string> { "A", "S", "D" };
            Assert.IsTrue(SpellingEvaluator.Evaluate(lenient, letters, new List<SpellingPress> { new SpellingPress("A", 0.1f), new SpellingPress("S", 0.3f), new SpellingPress("D", 0.5f) }).success, "correct input activates");
            SpellingOutcome sloppy = SpellingEvaluator.Evaluate(lenient, letters, new List<SpellingPress> { new SpellingPress("A", 0.1f), new SpellingPress("X", 0.2f), new SpellingPress("S", 0.4f), new SpellingPress("D", 0.6f) });
            Assert.IsTrue(sloppy.success);
            Assert.Greater(sloppy.castTimeMultiplier, 1f, "lenient: a wrong key increases the casting time");
            SpellingParams strict = lenient.Clone();
            strict.model = SpellingFailureModel.Strict;
            strict.maxMistakes = 0;
            Assert.IsFalse(SpellingEvaluator.Evaluate(strict, letters, new List<SpellingPress> { new SpellingPress("A", 0.1f), new SpellingPress("X", 0.2f), new SpellingPress("S", 0.4f), new SpellingPress("D", 0.6f) }).success, "strict: a wrong key fails it");
        }

        [Test]
        public void AC37_SpellsThatAllowMovementNeverUseMovementKeys()
        {
            RuneCatalog c = Catalog();
            foreach (SpellFormDef f in c.SpellForms.Where(f => f.canMoveWhileCasting))
                Assert.IsNotEmpty(f.effects.Concat(new[] { "" }), f.id); // forms that let the caster move exist (mending, ritual)
            Assert.IsTrue(c.SpellForms.Any(f => f.canMoveWhileCasting));
            foreach (string[] movement in new[] { new[] { "W", "A", "S", "D" }, new[] { "E", "S", "D", "F" }, new[] { "UpArrow", "LeftArrow", "DownArrow", "RightArrow" }, new[] { "Z", "Q", "S", "D" } })
            {
                SpellingKeyPool pool = SpellingKeyPool.Build(c.Spelling, movement, new[] { "E", "I" }, new[] { "Q", "R" }, false);
                foreach (string key in movement) CollectionAssert.DoesNotContain(pool.keys, key, "movement key " + key);
                foreach (string key in new[] { "Q", "R", "E", "I" }) CollectionAssert.DoesNotContain(pool.keys, key, "ability / interact key " + key);
                for (int seed = 0; seed < 200; seed++)
                    foreach (string letter in SpellingSequence.Generate(seed, pool.keys, 8))
                        CollectionAssert.DoesNotContain(movement, letter, "a sequence never asks for a movement key");
            }
        }
    }
}
#endif
