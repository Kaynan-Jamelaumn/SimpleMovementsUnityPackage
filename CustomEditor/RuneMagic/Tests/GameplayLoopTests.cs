#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Phase 9: the complete gameplay loop of the design spec (section 21), walked end to end through the real rules of every phase with
    /// one player's state: kills drop runes and energy, the soul's capacity is checked, runes are absorbed and given energy, a concept
    /// is discovered by the dungeon minigame and riddle, a synthesis is made from a formula, the soul is damaged and recovers, the spell
    /// is put on an ability key, cast with its Rune Energy cost, Soul Strain and casting debuffs and a key sequence, and everything is
    /// saved and loaded. Also the four independent resources (section 20) and the design philosophy (section 23). Pure: the same rules
    /// the server runs, without the engine.
    /// </summary>
    public class GameplayLoopTests
    {
        private static RuneCatalog Catalog() => RuneDefaults.Catalog();

        // The formula of the spec's worked example (section 18) as letters: W wind pressure, A ash combustion, V vibration resonance.
        private static readonly string[] Pattern = { "wind", "wind", "wind", "ash", "vibration", "vibration", "vibration" };
        private static readonly string[] Concepts = { "wind.pressure", "wind.pressure", "wind.pressure", "ash.combustion", "vibration.resonance", "vibration.resonance", "vibration.resonance" };

        private static List<string> Held(RuneSoulState s) => s.runes.Select(r => r.runeId).Distinct().ToList();

        private static List<float> PerfectPresses(MinigameChallenge ch, float minInterval)
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

        // ================================================================== the loop (spec 21)
        [Test]
        public void TheCompleteGameplayLoopRunsEndToEnd()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            var rng = new Random(2024);
            var s = new RuneSoulState();
            int level = 12;
            float maxIntegrity = b.maxSoulIntegrity;
            SoulRules.Normalize(b, s.soul, maxIntegrity);

            // ---- 1-2. Kill monsters; obtain Rune drops and magical energy.
            RuneDropContext boss = RuneDropContext.For("wind_beast", 2, MonsterRarity.Boss);
            List<RuneDrop> drops = null;
            for (int kill = 0; kill < 50 && (drops == null || drops.Count == 0); kill++)
            {
                drops = DropRules.Roll(c, boss, rng);
                EnergyRules.AddPending(b, s, DropRules.EnergyReward(c, boss));
            }
            Assert.IsNotNull(drops);
            Assert.IsNotEmpty(drops, "a boss kill drops runes");
            Assert.Greater(s.pendingEnergy, 0f, "and magical energy, waiting for the player to choose a rune");

            // ---- 3-4. Check Soul Rune Capacity; absorb runes.
            int capacity = CapacityRules.Capacity(b, level, s.soul);
            Assert.Greater(capacity, 0);
            int left = 0;
            foreach (RuneDrop d in drops)
            {
                // The player manages the collection: only the runes the formula needs are taken into the soul (the others stay in the world).
                if (!Pattern.Contains(d.runeId)) { left++; continue; }
                Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity, d.runeId, out string why), why);
                RuneRules.Absorb(c, s, d, new RuneOrigin { kind = "drop", source = "wind_beast" }, null, 1L);
            }
            Assert.AreEqual(drops.Count, s.runes.Count + left);
            // Rewards of dungeons and chests (other sources) bring the rest of the runes of the formula.
            foreach (string id in Pattern.Distinct())
            {
                int required = Pattern.Count(p => p == id);
                while (s.runes.Count(r => r.runeId == id) < required)
                {
                    Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity, id, out string why), why);
                    RuneRules.Absorb(c, s, new RuneDrop { runeId = id, rank = 1, quality = 60f, energy = 0f }, new RuneOrigin { kind = "reward", source = "chest" }, null, 2L);
                }
            }
            Assert.LessOrEqual(RuneRules.UsedSlots(c, s), capacity, "the soul's capacity is never exceeded");
            Assert.AreEqual(RuneRules.UsedSlots(c, s), s.runes.Sum(r => RuneRules.SlotCost(c, r)));

            // ---- 7-11. Explore a dungeon, find a source, complete the minigame, solve the riddle: a concept is unlocked.
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.pressure"), "hidden until discovered");
            DiscoveryStart start = DiscoveryFlow.Start(c, s, "ancient_inscription", 0, 0, "wind.pressure", Held(s), new Random(21), 100f);
            Assert.IsTrue(start.ok, start.reason);
            DiscoverySession session = start.session;
            List<float> presses = PerfectPresses(session.challenge, c.Discovery.minPressInterval);
            MinigameOutcome played = DiscoveryFlow.SubmitMinigame(c, s, session, presses, session.minigameIssuedAt + presses.Last() + 0.1f, 5L, out string playMsg);
            Assert.AreEqual(MinigameOutcome.Passed, played, playMsg);
            AnswerOutcome answered = DiscoveryFlow.SubmitAnswer(c, s, session, session.Question.correct, 9L, out string answerMsg);
            Assert.AreEqual(AnswerOutcome.Learned, answered, answerMsg);
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.pressure"));
            // The rest of the formula was discovered in earlier expeditions.
            RuneRules.Learn(s, "ash", "ash.combustion");
            RuneRules.Learn(s, "vibration", "vibration.resonance");

            // ---- 5-6. Choose which rune receives the energy; strengthen individual runes. Kills keep paying until the formula's runes are full enough.
            var ctx = new SynthesisContext { capacity = capacity, soul = SoulRules.Restrictions(b, s.soul, maxIntegrity), integrity = s.soul.integrity, maxIntegrity = maxIntegrity };
            var ids = s.runes.Select(r => r.instanceId).ToList();
            // The formula's runes in order: take the wind runes first (the dropped ones may be other runes: they stay in the soul unused).
            var lab = new SynthLab(c, capacity) { s = s };
            var chosen = new List<string>();
            foreach (string runeId in Pattern)
            {
                RuneInstance r = s.runes.First(x => x.runeId == runeId && !chosen.Contains(x.instanceId));
                chosen.Add(r.instanceId);
            }
            SynthesisRequest probe = lab.Request("WWWAVVV", chosen, "attack", "offense.explosion", SynthesisMode.Spell, 7);
            foreach (SynthesisSlot sl in probe.slots) sl.energy = 0.001f;
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(c, s, ctx, probe, out SynthesisRefusal refusal, out string refusalText);
            Assert.IsNotNull(ev, refusalText);
            for (int i = 0; i < chosen.Count; i++)
            {
                RuneInstance r = s.Rune(chosen[i]);
                float want = ev.slots[i].idealEnergy;
                int guard = 0;
                while (r.energy + 0.01f < want && guard++ < 200)
                {
                    EnergyRules.AddPending(b, s, DropRules.EnergyReward(c, boss)); // another kill
                    EnergyRules.Assign(b, s, r.instanceId, want - r.energy);
                }
                Assert.GreaterOrEqual(r.energy + 0.01f, want, "the player gave the rune the energy it needs");
            }

            // ---- 12-17. Choose an intention and a desired effect, select runes and one concept each, set the order, allocate energy.
            SynthesisRequest req = probe.Clone();
            for (int i = 0; i < req.slots.Count; i++)
            {
                Assert.AreEqual(Concepts[i], req.slots[i].conceptId);
                req.slots[i].energy = ev.slots[i].idealEnergy;
            }

            // ---- 18-19. Check Soul Integrity; perform the Rune Synthesis.
            Assert.IsTrue(ctx.soul.AllowsSynthesis(req.slots.Count, 0f, out string soulWhy), soulWhy);
            SynthesisResult made = SynthesisEngine.Execute(c, s, ctx, req, 100L);

            // ---- 20-21. Evaluate the result; a new spell (or a flawed result or a failure).
            Assert.IsTrue(made.performed, made.message);
            Assert.AreEqual(SynthesisTier.Successful, made.tier,
                "a player who understands only the one concept of each rune the formula uses makes a very good but not a perfect spell (knowledge is part of the score)");
            Assert.GreaterOrEqual(made.score, 95f, "the formula itself is the spec's worked example");
            Assert.AreEqual(1f, made.evaluation.factors.order, 1e-4f);
            Assert.AreEqual(1f, made.evaluation.factors.coverage, 1e-4f);
            Assert.Less(made.evaluation.factors.execution, 1f, "what holds it back from perfect is understanding, not energy or order");
            Assert.AreEqual(SynthesisOutput.Spell, made.output);
            SpellRecord spell = made.spell;
            Assert.IsNotNull(spell);
            Assert.AreEqual(7, spell.anchors.Count, "the spell anchors the runes it was made from");
            Assert.IsTrue(spell.anchors.All(a => s.Rune(a) != null), "they stay in the soul");

            // ---- 22. Receive Soul Damage.
            float before = s.soul.integrity;
            foreach (SoulDamageEntry d in made.soulDamage)
                SoulRules.Damage(b, s.soul, maxIntegrity, d.amount, d.kind);
            Assert.Less(s.soul.integrity, before, "a synthesis strains the soul");
            Assert.Greater(s.soul.integrity, 0f);

            // ---- 23. Recover Soul Integrity (meditation / rest / items).
            float low = s.soul.integrity;
            float restored = SoulRules.Restore(b, s.soul, maxIntegrity, 10f);
            Assert.Greater(restored, 0f);
            Assert.Greater(s.soul.integrity, low);

            // ---- 24. Assign the spell to the shared ability slots.
            var touched = new List<int>();
            Assert.IsTrue(SpellSlotRules.Assign(s, spell.spellId, 1, touched, out string slotWhy), slotWhy);
            Assert.AreEqual(1, SpellSlotRules.SlotOf(s, spell.spellId));
            CompiledSpell cs = SpellCompiler.Compile(c, spell);
            Assert.IsTrue(SpellCompiler.TryParseAbilityId(cs.abilityId, out _, out string parsed) && parsed == spell.spellId, "it runs as an ordinary ability on the existing slot");

            // ---- 24 (spelling), optionally: the developer's data says whether this spell asks for a key sequence.
            SoulRestrictions restrictions = SoulRules.Restrictions(b, s.soul, maxIntegrity);
            SpellingOutcome performance = null;
            bool spelled = SpellingGenerator.Uses(c, spell);
            Assert.IsTrue(spelled, "an explosion is a blast: the built-in rules mark it");
            SpellingKeyPool pool = SpellingKeyPool.Build(c.Spelling, new[] { "W", "A", "S", "D" }, new[] { "E", "I" }, new[] { "Q", "R" }, false);
            SpellingParams sp = SpellingGenerator.Resolve(c, cs, spell, new SpellingContext { knowledge = 1f, quality = spell.quality, soul = restrictions }, 5, pool.Count);
            List<string> letters = SpellingSequence.Generate(5, pool.keys, sp.letters);
            foreach (string k in letters) CollectionAssert.DoesNotContain(new[] { "W", "A", "S", "D", "E", "I", "Q", "R" }, k, "never a movement or critical key");
            var keys = letters.Select((k, i) => new SpellingPress(k, 0.12f + i * 0.2f)).ToList();
            performance = SpellingEvaluator.Evaluate(sp, letters, keys);
            Assert.IsTrue(performance.success, "a clean sequence succeeds");

            // ---- 25-26. Cast: consume Rune Energy; apply Soul Strain and casting debuffs.
            CastPlan plan = SpellCasting.Plan(c, cs, restrictions);
            SpellCasting.ApplyPerformance(ref plan, cs, performance, c.Spells);
            Assert.IsTrue(plan.allowed);
            // The synthesis consumed the energy the runes were given, so the new spell's runes are empty: the player goes back to
            // the dungeon (27) and earns the energy to cast with (1-2, 5-6): the loop connects casting back to combat.
            Assert.IsFalse(SpellCasting.CanAfford(s, spell, plan.energyCost, out _), "a spell with empty runes cannot be cast yet");
            for (int kill = 0; kill < 100 && SpellRules.AnchoredEnergy(s, spell) < plan.energyCost; kill++)
            {
                EnergyRules.AddPending(b, s, DropRules.EnergyReward(c, boss));
                EnergyRules.Assign(b, s, spell.anchors[kill % spell.anchors.Count], 0f);
            }
            float energyBefore = SpellRules.AnchoredEnergy(s, spell);
            Assert.IsTrue(SpellCasting.CanAfford(s, spell, plan.energyCost, out string energyWhy), energyWhy);
            CastDraw draw = SpellCasting.Draw(s, spell, plan.energyCost);
            Assert.IsNotNull(draw);
            Assert.AreEqual(energyBefore - plan.energyCost, SpellRules.AnchoredEnergy(s, spell), 0.01f, "Rune Energy is consumed from the spell's runes");
            CastStrain strain = SpellCasting.Success(b, plan);
            float integrityBeforeCast = s.soul.integrity;
            if (strain.Any) SoulRules.Damage(b, s.soul, maxIntegrity, strain.amount, strain.kind);
            Assert.LessOrEqual(s.soul.integrity, integrityBeforeCast, "casting strains the soul (when the balance says so)");

            // ---- 27. Back to the dungeon: everything persists, and the loop continues.
            RuneSoulState loaded = RuneSoulState.FromJson(s.ToJson());
            Assert.AreEqual(s.runes.Count, loaded.runes.Count);
            Assert.AreEqual(s.soul.integrity, loaded.soul.integrity, 1e-3f);
            Assert.AreEqual(1, SpellSlotRules.SlotOf(loaded, spell.spellId), "the key the spell is on is saved");
            Assert.IsNotNull(SpellRules.Find(loaded, spell.spellId));
            Assert.IsTrue(RuneRules.Knows(loaded, "wind", "wind.pressure"), "discovered concepts are permanent");
            Assert.AreEqual(SpellRules.AnchoredEnergy(s, spell), SpellRules.AnchoredEnergy(loaded, loaded.spells[0]), 0.01f, "the energy left in the runes is saved");
            Assert.IsFalse(SpellRules.RemoveBroken(loaded).Any(), "nothing the spell needs was lost");
        }

        // ================================================================== four independent resources (spec 20)
        [Test]
        public void TheFourResourcesAreIndependentAndEveryValidCombinationExists()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            float max = b.maxSoulIntegrity;

            // Full physical health (not modelled by this system: nothing here reads or writes it) with a critically damaged soul.
            var damaged = new RuneSoulState();
            SoulRules.Normalize(b, damaged.soul, max);
            SoulRules.Damage(b, damaged.soul, max, 70f, "synthesis");
            Assert.AreEqual("critical", SoulRules.Condition(b, damaged.soul, max).id);

            // A max-energy rune while the soul is damaged.
            RuneRules.Absorb(c, damaged, new RuneDrop { runeId = "fire", rank = 1, quality = 50f, energy = 1e9f }, new RuneOrigin { kind = "test" }, "r1", 1L);
            Assert.AreEqual(b.MaxEnergy(1, 50f), damaged.runes[0].energy, 0.01f);
            Assert.Less(damaged.soul.integrity, max * 0.3f);

            // High Soul Integrity but insufficient Rune Energy: the soul allows everything, the runes cannot pay.
            var empty = new RuneSoulState();
            SoulRules.Normalize(b, empty.soul, max);
            RuneRules.Absorb(c, empty, new RuneDrop { runeId = "fire", rank = 1, quality = 50f, energy = 0f }, new RuneOrigin { kind = "test" }, "r1", 1L);
            Assert.AreEqual("stable", SoulRules.Condition(b, empty.soul, max).id);
            Assert.IsTrue(SoulRules.Restrictions(b, empty.soul, max).castingAllowed);
            var spell = new SpellRecord { spellId = "syn_x", name = "X", effectId = "offense.pierce", rank = 1, complexity = 0.5f, power = 100f, stability = 90f, anchors = new List<string> { "r1" } };
            empty.spells.Add(spell);
            Assert.IsFalse(SpellCasting.CanAfford(empty, spell, SpellCasting.Plan(c, SpellCompiler.Compile(c, spell), SoulRestrictions.None).energyCost, out _));

            // Powerful runes but no free slots.
            var crowded = new RuneSoulState();
            int capacity = CapacityRules.Capacity(b, 1, crowded.soul);
            for (int i = 0; i < capacity; i++)
                RuneRules.Absorb(c, crowded, new RuneDrop { runeId = "fire", rank = 3, quality = 90f, energy = 1e9f }, new RuneOrigin { kind = "test" }, "p" + i, 1L);
            Assert.IsFalse(RuneRules.CanAbsorb(c, crowded, capacity, "wind", out string why), "no free slot");
            StringAssert.Contains("slot", why.ToLowerInvariant());

            // Correct concepts but not enough energy to synthesize.
            var lab = new SynthLab(c);
            SynthesisRequest ready = lab.Ready("WWWAVVV");
            foreach (RuneInstance r in lab.s.runes) r.energy = 1f; // the runes hold almost nothing
            for (int i = 0; i < ready.slots.Count; i++) ready.slots[i].energy = 1f; // and the player offers all of it
            SynthesisResult refused = lab.Execute(ready);
            Assert.IsFalse(refused.performed);
            Assert.AreEqual(SynthesisRefusal.InsufficientEnergy, refused.refusal);

            // And none of them is derived from another: damaging the soul does not touch a rune's energy, spending energy does not touch the soul.
            RuneSoulState both = new RuneSoulState();
            SoulRules.Normalize(b, both.soul, max);
            RuneRules.Absorb(c, both, new RuneDrop { runeId = "fire", rank = 1, quality = 50f, energy = 300f }, new RuneOrigin { kind = "test" }, "r1", 1L);
            float integrity = both.soul.integrity;
            EnergyRules.Consume(both.runes[0], 100f);
            Assert.AreEqual(integrity, both.soul.integrity, "energy does not move the soul's integrity");
            SoulRules.Damage(b, both.soul, max, 20f, "synthesis");
            Assert.AreEqual(200f, both.runes[0].energy, 1e-3f, "Soul Damage does not move a rune's energy");
            Assert.AreEqual(1, RuneRules.UsedSlots(c, both), "nor the slots in use");
        }

        // ================================================================== design philosophy (spec 23)
        [Test]
        public void PowerComesFromKnowledgeAndUnderstandingNotFromHighRankFullRunes()
        {
            RuneCatalog c = Catalog();
            // A player with high-rank, high-quality, full-energy runes but no discovered concepts cannot synthesize at all.
            var lab = new SynthLab(c);
            SynthesisRequest ok = lab.Ready("WWWAVVV", rank: 3, quality: 100f);
            foreach (RuneInstance r in lab.s.runes) { r.energy = c.Balance.MaxEnergy(r.rank, r.quality); }
            lab.s.knowledge.Clear(); // forget every concept: knowledge is the gate, not rank or energy
            for (int i = 0; i < ok.slots.Count; i++) ok.slots[i].energy = lab.s.runes[i].energy;
            SynthesisResult noKnowledge = lab.Execute(ok);
            Assert.IsFalse(noKnowledge.performed, "high rank and full energy are not enough");
            Assert.AreEqual(SynthesisRefusal.ConceptNotKnown, noKnowledge.refusal);

            // And a formula the player does not understand (the order or the proportions) is judged by what it is, whatever the runes hold.
            var knowing = new SynthLab(c);
            SynthesisRequest ideal = knowing.Ready("WWWAVVV");
            SynthesisRequest wrongOrder = ideal.Clone();
            wrongOrder.slots.Reverse();
            SynthesisRequest wrongRatios = knowing.Fill(knowing.Request("WWWAVVV", knowing.ids.Take(7).ToList()), 1f, new[] { 3f, 0.2f, 1f, 1f, 0.2f, 3f, 1f });
            float perfectScore = knowing.Evaluate(ideal).score;
            Assert.Less(knowing.Evaluate(wrongOrder).score, perfectScore, "the order matters");
            Assert.Less(knowing.Evaluate(wrongRatios).score, perfectScore, "the energy ratios matter");
        }

        [Test]
        public void UnderstandingMoreOfTheRunesRaisesTheScoreOfTheSameFormula()
        {
            RuneCatalog c = Catalog();
            var scholar = new SynthLab(c);
            float full = scholar.Evaluate(scholar.Ready("WWWAVVV")).score;

            var novice = new SynthLab(c);
            var ids = new List<string>();
            foreach (string runeId in Pattern) ids.Add(novice.Add(runeId, 1, 60f, 1e9f, learnAll: false).instanceId);
            for (int i = 0; i < Pattern.Length; i++) RuneRules.Learn(novice.s, Pattern[i], Concepts[i]);
            SynthesisRequest req = novice.Fill(novice.Request("WWWAVVV", ids), 1f);
            float partial = novice.Evaluate(req).score;
            Assert.AreEqual(100f, full, 0.001f, "knowing every concept of the runes: the worked example is perfect");
            Assert.Less(partial, full, "knowing only the one concept of each rune it uses: a little less");
            Assert.Greater(partial, 90f, "but the formula is still right");

            var lowQuality = new SynthLab(c);
            SynthesisRequest weak = lowQuality.Ready("WWWAVVV", quality: 10f);
            Assert.Less(lowQuality.Evaluate(weak).score, full, "quality is its own attribute: it matters, and it is not rank, energy or knowledge");
        }

        [Test]
        public void AnExperimentThatGoesWrongCostsTheSoulAndTheRunesAndRecoveryTakesTime()
        {
            RuneCatalog c = Catalog();
            RuneBalance b = c.Balance;
            var lab = new SynthLab(c);
            SynthesisRequest bad = lab.Ready("WWWAVVV", 1f, "attack", "offense.explosion");
            bad.slots.Reverse(); // a fundamentally incoherent order
            SynthesisResult r = lab.Execute(bad);
            Assert.IsTrue(r.performed);
            Assert.IsFalse(r.Succeeded);
            Assert.Greater(r.energyLost + r.energyConsumed, 0f, "a failed experiment loses energy");
            float max = b.maxSoulIntegrity;
            var soul = new SoulState { initialized = true, integrity = max };
            foreach (SoulDamageEntry d in r.soulDamage) SoulRules.Damage(b, soul, max, d.amount, d.kind);
            Assert.Less(soul.integrity, max, "and strains the soul");
            RecoveryContext idle = RecoveryContext.Default;
            Assert.AreEqual(0f, RecoveryRules.PerMinute(b.soulRecovery, idle, out _), 0.001f, "the soul does not recover by itself while the player does nothing about it");
            RecoveryContext sleeping = RecoveryContext.Default;
            sleeping.sleeping = true;
            Assert.Greater(RecoveryRules.PerMinute(b.soulRecovery, sleeping, out _), 0f, "rest recovers it, slowly");
            RecoveryContext meditating = RecoveryContext.Default;
            meditating.meditating = true;
            meditating.meditationSeconds = 600f;
            Assert.Greater(RecoveryRules.PerMinute(b.soulRecovery, meditating, out _), RecoveryRules.PerMinute(b.soulRecovery, sleeping, out _), "meditation is the efficient way");
        }
    }
}
#endif
