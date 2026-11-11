#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the Spelling System (Phase 8, design spec 16.5, directive 9), without the engine: which spells use it (the
    /// global mode, rules by form / effect / archetype), the generated difficulty (letters by rank, power and complexity; a window
    /// for each key; the soul's condition and the player's knowledge), the key pool (never movement keys, default or rebound, nor
    /// other critical keys), the sequence, the replay and the score, the Strict and Lenient models, the Soul Damage of mistakes
    /// (never automatic), what the server accepts, and the data upgrade.
    /// </summary>
    public class SpellingCoreTests
    {
        // ------------------------------------------------------------------ fixtures
        private static SpellRecord Spell(string effect = "offense.pierce", int rank = 1, float complexity = 0.5f, float power = 100f, float quality = 60f, string archetype = "") =>
            new SpellRecord { spellId = "syn_t", name = "Test", effectId = effect, rank = rank, complexity = complexity, power = power, quality = quality, stability = 90f, archetypeId = archetype };

        private static CompiledSpell Compile(RuneCatalog c, SpellRecord sp) => SpellCompiler.Compile(c, sp);

        private static SpellingParams Resolve(RuneCatalog c, SpellRecord sp, SpellingContext? ctx = null, int seed = 7, int pool = 0) =>
            SpellingGenerator.Resolve(c, Compile(c, sp), sp, ctx ?? SpellingContext.Plain, seed, pool);

        private static RuneCatalog Custom(System.Action<RuneMagicData> edit) => SynthLab.Custom(edit);

        private static SoulRestrictions Condition(RuneCatalog c, float percent)
        {
            var soul = new SoulState { initialized = true, integrity = c.Balance.maxSoulIntegrity * percent / 100f };
            return SoulRules.Restrictions(c.Balance, soul, c.Balance.maxSoulIntegrity);
        }

        private static readonly string[] Letters = { "A", "S", "D", "F" };

        // A plain sequence of 4 letters with windows of 1 second and a lenient model (2 mistakes allowed).
        private static SpellingParams Plain(SpellingFailureModel model = SpellingFailureModel.Lenient, int maxMistakes = 2) => new SpellingParams
        {
            uses = true, letters = 4, windows = new List<float> { 1f, 1f, 1f, 1f }, totalLimit = 4f, model = model, maxMistakes = maxMistakes,
            weights = new List<SpellingWeight> { new SpellingWeight("accuracy", 0.5f), new SpellingWeight("speed", 0.2f), new SpellingWeight("precision", 0.2f), new SpellingWeight("rhythm", 0.1f) },
            speedBonusThreshold = 0.55f, speedBonusMaxMistakes = 0, speedBonusPower = 0.1f, speedBonusCost = 0.1f, speedBonusStrain = 0.2f, speedBonusStability = 5f,
            powerSwing = 0.1f, powerPerMistake = 0.06f, castTimePerMistake = 0.1f, stabilityPerMistake = 5f, damageKind = "spelling_mistake", damageAmount = 2f, damageScaling = 0.5f,
        };

        private static List<SpellingPress> Presses(params (string key, float t)[] p) => p.Select(x => new SpellingPress(x.key, x.t)).ToList();

        // ------------------------------------------------------------------ the global mode and the rules
        [Test]
        public void NoneMeansNoSpellUsesItWhateverTheRulesSay()
        {
            RuneCatalog c = Custom(d => d.balance.spelling.mode = SpellingMode.None);
            Assert.IsFalse(SpellingGenerator.Uses(c, Spell("offense.explosion")), "blast is marked by a rule, but the mode is None");
        }

        [Test]
        public void AllMeansEverySpellUsesItEvenOnesARuleExcludes()
        {
            RuneCatalog c = Custom(d =>
            {
                d.balance.spelling.mode = SpellingMode.All;
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.No });
            });
            Assert.IsTrue(SpellingGenerator.Uses(c, Spell("offense.pierce")), "every spell must use it");
            Assert.IsTrue(SpellingGenerator.Uses(c, Spell("movement.dash")));
        }

        [Test]
        public void SelectedMeansOnlySpellsARuleMarksUseIt()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.AreEqual(SpellingMode.Selected, c.Spelling.mode, "decision D9: the default mode is Selected");
            Assert.IsTrue(SpellingGenerator.Uses(c, Spell("offense.explosion")), "blast form is marked");
            Assert.IsFalse(SpellingGenerator.Uses(c, Spell("offense.pierce")), "bolt form is not");
            Assert.IsFalse(SpellingGenerator.Uses(c, Spell("movement.dash")));
        }

        [Test]
        public void TheMostSpecificRuleWinsFieldByField()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Effect, id = "offense.explosion", use = SpellingUse.No, failureModel = SpellingFailureChoice.Strict });
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Archetype, id = d.archetypes[0].id, use = SpellingUse.Yes, damageMode = SpellingDamageChoice.FlatPerMistake, damageAmount = 5f });
            });
            string arch = c.Archetypes[0].id;
            SpellingRule plain = SpellingGenerator.RuleFor(c, Spell("offense.explosion"));
            Assert.AreEqual(SpellingUse.No, plain.use, "the effect's rule overrules the form's");
            Assert.AreEqual(SpellingFailureChoice.Strict, plain.failureModel);
            Assert.AreEqual(SpellingDamageChoice.OnceOnFailure, plain.damageMode, "a field the effect rule leaves open comes from the form's rule");
            SpellingRule recognised = SpellingGenerator.RuleFor(c, Spell("offense.explosion", archetype: arch));
            Assert.AreEqual(SpellingUse.Yes, recognised.use, "the archetype's rule overrules the effect's");
            Assert.AreEqual(SpellingDamageChoice.FlatPerMistake, recognised.damageMode);
            Assert.AreEqual(5f, recognised.damageAmount);
            Assert.AreEqual(SpellingFailureChoice.Strict, recognised.failureModel);
        }

        // ------------------------------------------------------------------ generated difficulty
        [Test]
        public void MoreRankPowerAndComplexityMeanMoreLetters()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            int basic = Resolve(c, Spell(rank: 1, complexity: 0.2f, power: 100f)).letters;
            Assert.Greater(Resolve(c, Spell(rank: 4, complexity: 0.2f, power: 100f)).letters, basic, "rank");
            Assert.Greater(Resolve(c, Spell(rank: 1, complexity: 0.2f, power: 300f)).letters, basic, "power");
            Assert.Greater(Resolve(c, Spell(rank: 1, complexity: 1f, power: 100f)).letters, basic, "complexity");
        }

        [Test]
        public void LettersStayWithinTheBalancesLimitsAndTheKeysThePoolAllows()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellingBalance b = c.Spelling;
            int most = Resolve(c, Spell(rank: 50, complexity: 1f, power: 5000f)).letters;
            Assert.AreEqual(b.maxLetters, most);
            RuneCatalog easy = Custom(d => { d.balance.spelling.baseLetters = 1; d.balance.spelling.lettersPerComplexity = 0f; });
            Assert.AreEqual(easy.Spelling.minLetters, Resolve(easy, Spell(rank: 1, complexity: 0f, power: 10f)).letters, "never fewer than the minimum");
            Assert.AreEqual(4, Resolve(c, Spell(rank: 50, complexity: 1f, power: 5000f), pool: 4).letters, "fewer keys than letters: fewer letters");
        }

        [Test]
        public void EveryKeyHasItsOwnWindowAndTheFirstIsLongest()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellingParams p = Resolve(c, Spell(rank: 3, complexity: 0.7f));
            Assert.AreEqual(p.letters, p.windows.Count);
            Assert.IsTrue(p.windows.Skip(1).Any(w => System.Math.Abs(w - p.windows[1]) > 1e-4f) || p.letters <= 2, "the windows differ from key to key");
            Assert.Greater(p.windows[0], p.windows.Skip(1).Average(), "the first key has time to read the letters");
            foreach (float w in p.windows) Assert.GreaterOrEqual(w, 0.2f);
            Assert.AreEqual(p.windows.Sum() * c.Spelling.totalLimitFactor, p.totalLimit, 1e-3f);
        }

        [Test]
        public void ARuleCanGiveEachKeyItsOwnWindowAndALimit()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellingRules.Clear();
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes, letters = 3, keyWindows = new List<float> { 1.5f, 0.8f, 1.2f }, totalLimit = 3.2f });
            });
            SpellingParams p = Resolve(c, Spell("offense.pierce"));
            Assert.AreEqual(3, p.letters);
            CollectionAssert.AreEqual(new[] { 1.5f, 0.8f, 1.2f }, p.windows, "the spec's example: 1.5 s, 0.8 s, 1.2 s");
            Assert.AreEqual(3.2f, p.totalLimit);
        }

        [Test]
        public void ARuleCanScaleTheWindowsAndAddOrFixLetters()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellingRules.Clear();
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes, windowMultiplier = 2f, lettersDelta = 2 });
            });
            RuneCatalog plain = Custom(d => { d.spellingRules.Clear(); d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes }); });
            SpellingParams widened = Resolve(c, Spell()), normal = Resolve(plain, Spell());
            Assert.AreEqual(normal.letters + 2, widened.letters);
            Assert.AreEqual(normal.windows[1] * 2f, widened.windows[1], 1e-3f);
        }

        [Test]
        public void TheSameSpellSoulAndSeedGiveTheSameParametersAndOtherSeedsVaryTheWindows()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellRecord sp = Spell(rank: 3, complexity: 0.8f);
            SpellingParams a = Resolve(c, sp, seed: 5), b = Resolve(c, sp, seed: 5), other = Resolve(c, sp, seed: 6);
            CollectionAssert.AreEqual(a.windows, b.windows);
            Assert.AreEqual(a.maxMistakes, b.maxMistakes);
            CollectionAssert.AreNotEqual(a.windows, other.windows);
            Assert.AreEqual(a.letters, other.letters, "the seed varies the windows, never how many letters");
        }

        [Test]
        public void AStrainedOrWorseSoulAddsLettersShortensWindowsAndRaisesPenalties()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellRecord sp = Spell(rank: 2, complexity: 0.5f);
            SpellingParams good = Resolve(c, sp, new SpellingContext { soul = Condition(c, 100f) });
            SpellingParams strained = Resolve(c, sp, new SpellingContext { soul = Condition(c, 70f) });
            SpellingParams critical = Resolve(c, sp, new SpellingContext { soul = Condition(c, 30f) });
            Assert.AreEqual(good.letters + 1, strained.letters);
            Assert.Greater(critical.letters, strained.letters);
            Assert.Less(strained.windows[1], good.windows[1], "the windows shrink (the seed's jitter is the same)");
            Assert.Less(critical.windows[1], good.windows[1]);
            Assert.Greater(critical.powerPerMistake, good.powerPerMistake);
            Assert.Greater(critical.stabilityPerMistake, good.stabilityPerMistake);
            Assert.AreEqual(good.powerPerMistake, Resolve(c, sp).powerPerMistake, "a soul without restrictions changes nothing");
        }

        [Test]
        public void KnowingTheConceptsAndRuneQualityMakeItEasier()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellRecord sp = Spell(rank: 2, complexity: 0.5f);
            SpellingParams novice = Resolve(c, sp, new SpellingContext { knowledge = 0f, quality = 0f, soul = SoulRestrictions.None });
            SpellingParams scholar = Resolve(c, sp, new SpellingContext { knowledge = 1f, quality = 100f, soul = SoulRestrictions.None });
            Assert.Greater(scholar.forgiveness, novice.forgiveness);
            Assert.Greater(scholar.windows[1], novice.windows[1]);
            Assert.GreaterOrEqual(scholar.maxMistakes, novice.maxMistakes + 1, "forgiveness allows more mistakes");
            Assert.AreEqual(novice.letters, scholar.letters);
        }

        [Test]
        public void StrictAllowsNoMistakesAndRhythmMakesRoomForTheBeat()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellingRules.Clear();
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes, failureModel = SpellingFailureChoice.Strict, rhythm = SpellingUse.Yes, keyWindow = 0.3f });
            });
            SpellingParams p = Resolve(c, Spell());
            Assert.AreEqual(SpellingFailureModel.Strict, p.model);
            Assert.AreEqual(0, p.maxMistakes);
            Assert.IsTrue(p.rhythm);
            foreach (float w in p.windows) Assert.GreaterOrEqual(w, p.tempo + p.tolerance, "a beat fits in every window");
            Assert.GreaterOrEqual(p.totalLimit, p.BeatOf(p.letters - 1));
        }

        // ------------------------------------------------------------------ the key pool (never movement keys)
        private static SpellingBalance Balance() => RuneDefaults.Spelling();

        [Test]
        public void TheDefaultPoolHoldsLettersOnlyAndLeavesOutTheKeysTheGameReadsOutsideActions()
        {
            SpellingKeyPool pool = SpellingKeyPool.Build(Balance(), new string[0], new string[0], new string[0], false);
            Assert.AreEqual(23, pool.Count, "A-Z without Y, N and T");
            foreach (string k in new[] { "Y", "N", "T" }) CollectionAssert.DoesNotContain(pool.keys, k);
            CollectionAssert.AreEquivalent(new[] { "Y", "N", "T" }, pool.leftOutReserved);
            Assert.IsTrue(pool.IsUsable(Balance()));
        }

        [Test]
        public void MovementKeysAreNeverInThePoolWhateverKeysThePlayerBoundThemTo()
        {
            SpellingBalance b = Balance();
            foreach (string[] movement in new[] { new[] { "W", "A", "S", "D" }, new[] { "UpArrow", "J", "K", "L" }, new[] { "Z", "Q", "S", "D" } })
            {
                SpellingKeyPool pool = SpellingKeyPool.Build(b, movement, new string[0], new string[0], false);
                foreach (string k in movement) CollectionAssert.DoesNotContain(pool.keys, k, "movement key " + k);
                CollectionAssert.IsSubsetOf(movement.Where(k => k.Length == 1 && b.candidateKeys.Contains(k)).ToList(), pool.leftOutMovement);
            }
        }

        [Test]
        public void AbilityKeysInteractAndEveryOtherGameplayKeyAreLeftOut()
        {
            SpellingBalance b = Balance();
            SpellingKeyPool pool = SpellingKeyPool.Build(b, new[] { "W", "A", "S", "D" }, new[] { "E", "I", "F" }, new[] { "Q", "R" }, false);
            foreach (string k in new[] { "W", "A", "S", "D", "E", "I", "F", "Q", "R" }) CollectionAssert.DoesNotContain(pool.keys, k, k);
            Assert.AreEqual(23 - 9, pool.Count);
        }

        [Test]
        public void AbilityKeysStayOutEvenWhenOtherGameplayKeysMayBeUsed()
        {
            SpellingBalance b = Balance();
            b.excludeGameplayKeys = false;
            SpellingKeyPool pool = SpellingKeyPool.Build(b, new[] { "W" }, new[] { "E", "I" }, new[] { "Q", "R" }, false);
            CollectionAssert.DoesNotContain(pool.keys, "W", "movement");
            CollectionAssert.DoesNotContain(pool.keys, "Q", "an ability's key is never asked for");
            CollectionAssert.DoesNotContain(pool.keys, "R");
            CollectionAssert.Contains(pool.keys, "E", "this balance lets other gameplay keys be used");
        }

        [Test]
        public void ASpellThatRootsTheCasterMayUseMovementKeysButNeverAbilityKeys()
        {
            SpellingBalance b = Balance();
            SpellingKeyPool moving = SpellingKeyPool.Build(b, new[] { "W", "A", "S", "D" }, new[] { "E" }, new[] { "Q" }, false);
            SpellingKeyPool rooted = SpellingKeyPool.Build(b, new[] { "W", "A", "S", "D" }, new[] { "E" }, new[] { "Q" }, true);
            CollectionAssert.DoesNotContain(moving.keys, "W");
            foreach (string k in new[] { "W", "A", "S", "D" }) CollectionAssert.Contains(rooted.keys, k);
            CollectionAssert.DoesNotContain(rooted.keys, "Q", "never the keys of other spells' slots");
            CollectionAssert.DoesNotContain(rooted.keys, "E", "nor other gameplay keys");
            b.allowMovementKeysWhenRooted = false;
            CollectionAssert.DoesNotContain(SpellingKeyPool.Build(b, new[] { "W" }, new string[0], new string[0], true).keys, "W");
        }

        [Test]
        public void ATooSmallPoolIsNotUsable()
        {
            SpellingBalance b = Balance();
            List<string> allButTwo = b.candidateKeys.Where(k => k != "A" && k != "B").ToList();
            SpellingKeyPool pool = SpellingKeyPool.Build(b, new string[0], allButTwo, new string[0], false);
            Assert.AreEqual(2, pool.Count);
            Assert.IsFalse(pool.IsUsable(b), "fewer than the balance's minimum pool size: the Spelling System is skipped");
            Assert.IsTrue(pool.IsUsable(new SpellingBalance { minPoolSize = 2 }));
            Assert.IsFalse(SpellingKeyPool.Build(b, new string[0], b.candidateKeys, new string[0], false).IsUsable(b), "every letter bound to an action");
        }

        [Test]
        public void TheServerKeepsOnlyCandidateKeysAndNoReservedOnesWhateverAClientReports()
        {
            SpellingBalance b = Balance();
            List<string> clean = SpellingKeyPool.Sanitize(b, new[] { "A", "a", "Y", "Space", "Digit1", "LeftShift", "Q", "Q", "xyz", "" });
            CollectionAssert.AreEqual(new[] { "A", "Q" }, clean, "candidates only, no reserved keys, no duplicates, in the candidates' order");
            Assert.IsEmpty(SpellingKeyPool.Sanitize(b, null));
        }

        [Test]
        public void ThePoolTravelsAsTextWithinLimits()
        {
            string text = SpellingKeyPool.ToText(new[] { "A", "S", "D" });
            Assert.AreEqual("A,S,D", text);
            CollectionAssert.AreEqual(new[] { "A", "S", "D" }, SpellingKeyPool.FromText(text));
            Assert.IsEmpty(SpellingKeyPool.FromText(""));
            Assert.LessOrEqual(SpellingKeyPool.FromText(string.Join(",", Enumerable.Range(0, 500).Select(i => "K" + i))).Count, SpellingKeyPool.MaxPoolKeys);
            Assert.AreEqual("A", SpellingKeyPool.ToText(new[] { "A", "B,C", "" }).Split(',')[0]);
        }

        // ------------------------------------------------------------------ the sequence
        [Test]
        public void ASequenceIsTheSameOnEveryMachineAndHoldsDifferentLettersWhileThePoolAllows()
        {
            var pool = Enumerable.Range(0, 12).Select(i => ((char)('A' + i)).ToString()).ToList();
            List<string> a = SpellingSequence.Generate(42, pool, 8), b = SpellingSequence.Generate(42, pool, 8);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AllItemsAreUnique(a);
            CollectionAssert.IsSubsetOf(a, pool);
            CollectionAssert.AreNotEqual(a, SpellingSequence.Generate(43, pool, 8));
        }

        [Test]
        public void ASmallPoolReusesLettersButNeverTheSameTwiceInARow()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                List<string> s = SpellingSequence.Generate(seed, new[] { "A", "S" }, 9);
                Assert.AreEqual(9, s.Count);
                for (int i = 1; i < s.Count; i++) Assert.AreNotEqual(s[i - 1], s[i]);
            }
            Assert.IsEmpty(SpellingSequence.Generate(1, new string[0], 3));
            Assert.AreEqual(3, SpellingSequence.Generate(1, new[] { "A" }, 3).Count);
        }

        // ------------------------------------------------------------------ replay and score
        [Test]
        public void ACleanFastSequenceSucceedsWithTheSpeedBonusAndAFullScore()
        {
            SpellingOutcome o = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f)));
            Assert.IsTrue(o.success);
            Assert.AreEqual(4, o.hits);
            Assert.AreEqual(0, o.Mistakes);
            Assert.IsTrue(o.speedBonus, "finished within 55% of the limit, without mistakes");
            Assert.Greater(o.performance, 0.9f);
            Assert.Greater(o.powerMultiplier, 1f);
            Assert.Less(o.costMultiplier, 1f);
            Assert.Less(o.strainMultiplier, 1f);
            Assert.Greater(o.stabilityDelta, 0f);
            Assert.AreEqual(1f, o.castTimeMultiplier, 1e-5f);
        }

        [Test]
        public void ASlowSequenceScoresLowerAndEarnsNoBonus()
        {
            SpellingOutcome fast = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f)));
            SpellingOutcome slow = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.9f), ("S", 1.8f), ("D", 2.7f), ("F", 3.6f)));
            Assert.IsTrue(slow.success);
            Assert.IsFalse(slow.speedBonus);
            Assert.Less(slow.performance, fast.performance);
            Assert.Less(slow.speed, fast.speed);
            Assert.Less(slow.precision, fast.precision);
        }

        [Test]
        public void LenientCountsAWrongKeyAsAMistakeAndTheLetterStaysAsked()
        {
            SpellingOutcome o = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f), ("X", 0.25f), ("S", 0.4f), ("D", 0.6f), ("F", 0.8f)));
            Assert.IsTrue(o.success);
            Assert.AreEqual(1, o.wrongKeys);
            Assert.AreEqual(1, o.Mistakes);
            Assert.AreEqual(4, o.hits);
            Assert.AreEqual(1, o.steps[1].wrongKeys, "the wrong key was pressed while the second letter was asked for");
            Assert.IsFalse(o.speedBonus, "no bonus with a mistake");
            Assert.Less(o.powerMultiplier, 1.1f);
            Assert.AreEqual(1.1f, o.castTimeMultiplier, 1e-4f, "each mistake adds 10% to the cast time");
        }

        [Test]
        public void AMistakeCostsPowerStabilityAndCastTimeInTheLenientModel()
        {
            SpellingParams p = Plain();
            SpellingOutcome clean = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("S", 1.2f), ("D", 1.9f), ("F", 2.6f)));
            SpellingOutcome sloppy = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("X", 0.8f), ("S", 1.2f), ("Z", 1.4f), ("D", 1.9f), ("F", 2.6f)));
            Assert.IsTrue(sloppy.success);
            Assert.AreEqual(2, sloppy.Mistakes);
            Assert.Less(sloppy.powerMultiplier, clean.powerMultiplier);
            Assert.AreEqual(1f + 2 * p.castTimePerMistake, sloppy.castTimeMultiplier, 1e-4f);
            Assert.AreEqual(clean.stabilityDelta - 2 * p.stabilityPerMistake, sloppy.stabilityDelta, 1e-4f);
        }

        [Test]
        public void TooManyMistakesFailTheLenientModelAndOneFailsTheStrictModel()
        {
            var keys = Presses(("A", 0.1f), ("X", 0.2f), ("X", 0.3f), ("X", 0.4f), ("S", 0.5f), ("D", 0.6f), ("F", 0.7f));
            SpellingOutcome lenient = SpellingEvaluator.Evaluate(Plain(maxMistakes: 2), Letters, keys);
            Assert.IsFalse(lenient.success);
            Assert.AreEqual(SpellingFailure.TooManyMistakes, lenient.failure);
            SpellingOutcome strict = SpellingEvaluator.Evaluate(Plain(SpellingFailureModel.Strict, 0), Letters, Presses(("A", 0.1f), ("X", 0.2f), ("S", 0.3f), ("D", 0.4f), ("F", 0.5f)));
            Assert.IsFalse(strict.success);
            Assert.AreEqual(SpellingFailure.Mistake, strict.failure);
            Assert.AreEqual(1, strict.Mistakes, "it stops at the first mistake");
            Assert.IsTrue(SpellingEvaluator.Evaluate(Plain(SpellingFailureModel.Strict, 0), Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f))).success);
        }

        [Test]
        public void AKeyPressedAfterItsWindowIsALateKeyAndTheNextLetterIsAsked()
        {
            // The second letter's window (1 s) closes at 1.3 s; the S at 1.6 s is late: that letter is missed, D is asked for.
            SpellingOutcome o = SpellingEvaluator.Evaluate(Plain(maxMistakes: 3), Letters, Presses(("A", 0.3f), ("S", 1.6f), ("D", 1.9f), ("F", 2.2f)));
            Assert.AreEqual(SpellingStepState.Missed, o.steps[1].state);
            Assert.AreEqual(1, o.missed);
            Assert.AreEqual(1, o.wrongKeys, "the late S is wrong now: D is asked for");
            Assert.AreEqual(SpellingStepState.Hit, o.steps[2].state);
        }

        [Test]
        public void LettersNobodyAnsweredAreMissedAndNothingCountsAfterTheTotalLimit()
        {
            SpellingParams p = Plain(maxMistakes: 4);
            SpellingOutcome stopped = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.1f), ("S", 0.3f)));
            Assert.AreEqual(2, stopped.missed);
            Assert.AreEqual(2, stopped.hits);
            p.totalLimit = 1f;
            SpellingOutcome late = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.2f), ("S", 0.4f), ("D", 0.6f), ("F", 1.4f)));
            Assert.AreEqual(3, late.hits, "F came after the 1 second limit");
            Assert.AreEqual(1, late.missed);
        }

        [Test]
        public void RhythmMakesAKeyOffTheBeatAMistakeAndScoresHowCloseItWas()
        {
            SpellingParams p = Plain(maxMistakes: 4);
            p.rhythm = true;
            p.tempo = 0.5f;
            p.tolerance = 0.1f;
            p.windows = new List<float> { 1.2f, 1.2f, 1.2f, 1.2f };
            p.totalLimit = 6f;
            SpellingOutcome onBeat = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("S", 1.0f), ("D", 1.5f), ("F", 2.0f)));
            SpellingOutcome off = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("S", 1.25f), ("D", 1.5f), ("F", 2.3f)));
            Assert.AreEqual(0, onBeat.offBeat);
            Assert.AreEqual(1f, onBeat.rhythm, 1e-4f);
            Assert.AreEqual(2, off.offBeat, "the S and the F were more than the tolerance off their beats");
            Assert.AreEqual(2, off.Mistakes);
            Assert.Less(off.rhythm, 1f);
            Assert.Less(off.performance, onBeat.performance);
            Assert.IsFalse(SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f))).steps[0].offBeat, "no rhythm: nothing is off the beat");
        }

        [Test]
        public void TheTrackerRunLiveGivesTheSameResultAsTheServersReplay()
        {
            SpellingParams p = Plain(maxMistakes: 3);
            var presses = Presses(("A", 0.2f), ("X", 0.4f), ("S", 0.6f), ("D", 2.5f), ("F", 2.8f));
            var tracker = new SpellingTracker(p, Letters);
            float clock = 0f;
            foreach (SpellingPress press in presses)
            {
                tracker.Advance(press.t);
                tracker.Press(press.key, press.t);
                clock = press.t;
            }
            tracker.Advance(clock + 5f);
            tracker.Finish();
            SpellingOutcome live = SpellingEvaluator.Complete(p, Letters, tracker);
            SpellingOutcome replay = SpellingEvaluator.Evaluate(p, Letters, presses);
            Assert.AreEqual(replay.success, live.success);
            Assert.AreEqual(replay.hits, live.hits);
            Assert.AreEqual(replay.Mistakes, live.Mistakes);
            Assert.AreEqual(replay.performance, live.performance, 1e-5f);
        }

        [Test]
        public void TheScoreIsExtensibleWithoutChangingTheEvaluator()
        {
            SpellingEvaluator.Register("test_always_zero", t => 0f);
            try
            {
                Assert.IsTrue(SpellingEvaluator.HasTerm("test_always_zero"));
                SpellingParams p = Plain();
                p.weights = new List<SpellingWeight> { new SpellingWeight("accuracy", 1f), new SpellingWeight("test_always_zero", 1f), new SpellingWeight("unknown_term", 5f) };
                SpellingOutcome o = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f)));
                Assert.AreEqual(0.5f, o.performance, 1e-4f, "a perfect accuracy and a registered term that scores 0, weighed alike; an unknown term counts for nothing");
            }
            finally { SpellingEvaluator.Register("test_always_zero", t => 0f); }
        }

        [Test]
        public void AnEvaluationIsDeterministic()
        {
            var presses = Presses(("A", 0.2f), ("X", 0.4f), ("S", 0.6f), ("D", 1.1f), ("F", 1.9f));
            SpellingOutcome a = SpellingEvaluator.Evaluate(Plain(), Letters, presses), b = SpellingEvaluator.Evaluate(Plain(), Letters, presses);
            Assert.AreEqual(a.performance, b.performance);
            Assert.AreEqual(a.powerMultiplier, b.powerMultiplier);
        }

        // ------------------------------------------------------------------ Soul Damage on mistakes (never automatic)
        [Test]
        public void MistakesDoNotHurtTheSoulUnlessTheSpellsDataSaysSo()
        {
            SpellingParams p = Plain();
            Assert.AreEqual(SpellingDamageMode.None, p.damageMode);
            Assert.AreEqual(0f, SpellingEvaluator.MistakeDamage(p, 5, true));
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.AreEqual(SpellingDamageMode.None, c.Spelling.damageMode, "the default is none");
            Assert.AreEqual(SpellingDamageMode.None, Resolve(c, Spell("offense.pierce")).damageMode, "an unmarked spell");
        }

        [Test]
        public void SoulDamageOnMistakesCanBeFlatScalingOrOnceOnFailure()
        {
            SpellingParams p = Plain();
            p.damageAmount = 2f;
            p.damageScaling = 0.5f;
            p.damageMode = SpellingDamageMode.FlatPerMistake;
            Assert.AreEqual(6f, SpellingEvaluator.MistakeDamage(p, 3, false), 1e-4f);
            p.damageMode = SpellingDamageMode.ScalingPerMistake;
            Assert.AreEqual(2f + 3f + 4f, SpellingEvaluator.MistakeDamage(p, 3, false), 1e-4f, "each further mistake costs 50% more than the one before");
            p.damageMode = SpellingDamageMode.OnceOnFailure;
            Assert.AreEqual(0f, SpellingEvaluator.MistakeDamage(p, 3, false), "no failure, no damage");
            Assert.AreEqual(2f, SpellingEvaluator.MistakeDamage(p, 3, true), 1e-4f);
            Assert.AreEqual(2f, SpellingEvaluator.MistakeDamage(p, 0, true), 1e-4f, "a failure with no counted mistakes still costs it");
        }

        [Test]
        public void TheOutcomeCarriesTheDamageOfItsMistakesAndTheirKind()
        {
            SpellingParams p = Plain();
            p.damageMode = SpellingDamageMode.FlatPerMistake;
            p.damageAmount = 1.5f;
            SpellingOutcome o = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.1f), ("X", 0.2f), ("S", 0.4f), ("D", 0.6f), ("F", 0.8f)));
            Assert.AreEqual(1.5f, o.mistakeDamage, 1e-4f);
            Assert.AreEqual("spelling_mistake", o.damageKind);
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.IsNotNull(c.Balance.DamageKind(o.damageKind), "the kind exists in the Soul Damage Kinds");
        }

        [Test]
        public void ARuleChoosesTheDamageOfItsSpells()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellingParams blast = Resolve(c, Spell("offense.explosion"));
            Assert.AreEqual(SpellingDamageMode.OnceOnFailure, blast.damageMode);
            Assert.AreEqual(3f, blast.damageAmount);
            Assert.AreEqual(SpellingDamageMode.FlatPerMistake, Resolve(c, Spell("control.immobilize")).damageMode);
            Assert.AreEqual(SpellingFailureModel.Strict, Resolve(c, Spell("defense.barrier")).model, "the wall form is strict in the examples");
            Assert.IsTrue(Resolve(c, Spell("offense.incinerate")).rhythm, "the beam form asks for rhythm in the examples");
        }

        // ------------------------------------------------------------------ what a result does to the cast
        [Test]
        public void ASuccessfulSequenceChangesTheCostTheStrainAndTheStabilityOfTheCast()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SpellRecord sp = Spell();
            CompiledSpell cs = Compile(c, sp);
            CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
            CastPlan fast = plan;
            SpellingOutcome bonus = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f)));
            SpellCasting.ApplyPerformance(ref fast, cs, bonus, c.Spells);
            Assert.Less(fast.energyCost, plan.energyCost);
            Assert.Less(fast.strain, plan.strain);
            Assert.LessOrEqual(fast.instability, plan.instability, "the bonus raises stability");

            CastPlan sloppy = plan;
            SpellingOutcome mistakes = SpellingEvaluator.Evaluate(Plain(maxMistakes: 4), Letters, Presses(("A", 0.1f), ("X", 0.2f), ("Z", 0.3f), ("S", 0.5f), ("D", 0.7f), ("F", 0.9f)));
            SpellCasting.ApplyPerformance(ref sloppy, cs, mistakes, c.Spells);
            Assert.Greater(sloppy.instability, plan.instability, "mistakes lower stability, which raises the misfire chance");
            Assert.AreEqual(plan.energyCost, sloppy.energyCost, 1e-4f, "mistakes do not cost energy");
            CastPlan untouched = plan;
            SpellCasting.ApplyPerformance(ref untouched, cs, null, c.Spells);
            Assert.AreEqual(plan.energyCost, untouched.energyCost);
        }

        [Test]
        public void ThePerformanceMovesThePowerAndTheCastTimeInTheSlotsModifiers()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            CompiledSpell cs = Compile(c, Spell());
            CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
            SpellModifiers plain = SpellCompiler.ModifiersFor(cs, plan);
            SpellingOutcome good = SpellingEvaluator.Evaluate(Plain(), Letters, Presses(("A", 0.1f), ("S", 0.3f), ("D", 0.5f), ("F", 0.7f)));
            SpellingOutcome sloppy = SpellingEvaluator.Evaluate(Plain(maxMistakes: 4), Letters, Presses(("A", 0.1f), ("X", 0.2f), ("Z", 0.3f), ("S", 0.5f), ("D", 0.7f), ("F", 0.9f)));
            SpellModifiers withGood = SpellCompiler.ModifiersFor(cs, plan, good), withSloppy = SpellCompiler.ModifiersFor(cs, plan, sloppy);
            Assert.Greater(withGood.damage, plain.damage);
            Assert.Less(withSloppy.damage, plain.damage);
            Assert.Greater(withSloppy.castTime, plain.castTime, "mistakes add casting time");
            Assert.AreEqual(plain.damage, SpellCompiler.ModifiersFor(cs, plan, null).damage);
        }

        [Test]
        public void TheTooltipSaysWhetherASpellUsesTheSpellingSystem()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            CompiledSpell cs = Compile(c, Spell("offense.explosion"));
            cs.usesSpelling = SpellingGenerator.Uses(c, Spell("offense.explosion"));
            string text = string.Join("\n", SpellCompiler.Describe(c, cs, SpellCasting.Plan(c, cs, SoulRestrictions.None)));
            StringAssert.Contains("Spelling System: yes", text);
            cs.usesSpelling = false;
            StringAssert.Contains("Spelling System: no", string.Join("\n", SpellCompiler.Describe(c, cs, SpellCasting.Plan(c, cs, SoulRestrictions.None))));
        }

        // ------------------------------------------------------------------ what the server accepts
        [Test]
        public void TheServerRefusesKeysOutOfOrderFasterThanAHandAndLongerThanTheTimeThatPassed()
        {
            SpellingBalance b = RuneDefaults.Spelling();
            string why;
            Assert.IsTrue(SpellingValidator.Check(b, Presses(("A", 0.4f), ("S", 0.9f)), 1.0f, 1.6f, out why), why);
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.9f), ("S", 0.4f)), 1.0f, 2f, out why));
            StringAssert.Contains("order", why);
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.4f), ("S", 0.42f)), 1.0f, 2f, out why));
            StringAssert.Contains("faster", why);
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.02f)), 1.0f, 2f, out why), "faster than a hand from the first prompt too");
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.4f)), 9f, 2f, out why));
            StringAssert.Contains("longer than the time", why);
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.4f), ("S", 0.9f)), 0.5f, 2f, out why));
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", float.NaN)), 1f, 2f, out why));
            Assert.IsTrue(SpellingValidator.Check(b, Presses(("A", 0.4f)), 2.5f, 2f, out why), "the latency allowance is forgiven");
        }

        [Test]
        public void AResultReportedLongAfterItEndedIsRefusedBecauseItCouldOnlyBeHidingItsTimes()
        {
            SpellingBalance b = RuneDefaults.Spelling();
            string why;
            Assert.IsTrue(SpellingValidator.Check(b, Presses(("A", 0.4f), ("S", 0.9f)), 1.0f, 1.0f + b.maxReportLag - 0.1f, out why), why);
            Assert.IsFalse(SpellingValidator.Check(b, Presses(("A", 0.4f), ("S", 0.9f)), 1.0f, 1.0f + b.maxReportLag + 1f, out why));
            StringAssert.Contains("too long after", why);
        }

        [Test]
        public void ASequenceThatWasNotPlayedIsTheWorstASuccessCanBeAndCostsTheSoulNothing()
        {
            SpellingParams p = Plain(maxMistakes: 2);
            p.damageMode = SpellingDamageMode.FlatPerMistake;
            p.damageAmount = 5f;
            SpellingOutcome skipped = SpellingEvaluator.Skipped(p);
            Assert.IsTrue(skipped.success, "it still casts");
            Assert.IsFalse(skipped.speedBonus);
            Assert.AreEqual(0f, skipped.performance);
            Assert.AreEqual(0f, skipped.mistakeDamage, "nothing was played, so no mistake costs the soul");
            SpellingOutcome clean = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("S", 1.2f), ("D", 1.9f), ("F", 2.6f)));
            SpellingOutcome sloppy = SpellingEvaluator.Evaluate(p, Letters, Presses(("A", 0.5f), ("X", 0.8f), ("S", 1.2f), ("Z", 1.4f), ("D", 1.9f), ("F", 2.6f)));
            Assert.Less(skipped.powerMultiplier, clean.powerMultiplier);
            Assert.LessOrEqual(skipped.powerMultiplier, sloppy.powerMultiplier + 1e-4f, "never better than playing it with the most mistakes allowed");
            Assert.GreaterOrEqual(skipped.castTimeMultiplier, sloppy.castTimeMultiplier - 1e-4f);
            Assert.LessOrEqual(skipped.stabilityDelta, sloppy.stabilityDelta + 1e-4f);
            // Even a Strict spell, which allows no mistake, pays for it.
            SpellingParams strict = Plain(SpellingFailureModel.Strict, 0);
            Assert.Less(SpellingEvaluator.Skipped(strict).powerMultiplier, 1f);
            Assert.Greater(SpellingEvaluator.Skipped(strict).castTimeMultiplier, 1f);
        }

        [Test]
        public void AResultRoundTripsOnTheWireAndEverythingMalformedIsRefused()
        {
            var presses = Presses(("A", 0.4f), ("LeftShift", 0.923f), ("Digit1", 1.5f));
            string text = SpellingResultCodec.Encode(7, 1.7f, presses);
            Assert.IsTrue(SpellingResultCodec.TryDecode(text, 20, out int seq, out float end, out List<SpellingPress> back, out string why), why);
            Assert.AreEqual(7, seq);
            Assert.AreEqual(1.7f, end, 1e-3f);
            Assert.AreEqual(3, back.Count);
            Assert.AreEqual("LeftShift", back[1].key);
            Assert.AreEqual(0.923f, back[1].t, 1e-3f);
            Assert.IsTrue(SpellingResultCodec.TryDecode(SpellingResultCodec.Encode(1, 0.5f, new List<SpellingPress>()), 5, out _, out _, out List<SpellingPress> none, out _));
            Assert.IsEmpty(none);

            foreach (string bad in new[] { "", "7", "7|1700", "x|1700|A=1", "7|-5|A=1", "7|1700|A", "7|1700|=5", "7|1700|A=-1", "7|1700|A=zz", "7|1700|A$=10", "7|99999999|A=1", "7|1700|A=1,B=2,C=3,D=4,E=5,F=6", new string('9', 2000) })
                Assert.IsFalse(SpellingResultCodec.TryDecode(bad, 5, out _, out _, out _, out _), "'" + (bad.Length > 30 ? bad.Substring(0, 30) : bad) + "'");
            Assert.IsFalse(SpellingResultCodec.TryDecode(text, 2, out _, out _, out _, out why), "more presses than the sequence allows");
            StringAssert.Contains("Too many", why);
        }

        [Test]
        public void TheViewSentToTheOwnerRoundTripsThroughJsonAndCompares()
        {
            var v = new SpellingView
            {
                seq = 3, state = SpellingState.Active, slot = 2, spellId = "syn_x", spellName = "Firebolt", letters = new List<string> { "A", "S", "D" },
                windows = new List<float> { 1.5f, 1f, 1.2f }, totalLimit = 3.2f, rhythm = true, tempo = 0.5f, tolerance = 0.2f, model = SpellingFailureModel.Strict, maxMistakes = 0,
            };
            SpellingView back = UnityEngine.JsonUtility.FromJson<SpellingView>(UnityEngine.JsonUtility.ToJson(v));
            Assert.IsTrue(v.SameAs(back));
            CollectionAssert.AreEqual(v.windows, back.windows);
            Assert.AreEqual(3.2f, back.EffectiveLimit, 1e-4f);
            Assert.AreEqual(1.5f, back.WindowOf(0));
            var copy = new SpellingView();
            copy.CopyFrom(v);
            Assert.IsTrue(v.SameAs(copy));
            copy.letters.Add("F");
            Assert.AreEqual(3, v.letters.Count, "a copy is a copy");
        }

        // ------------------------------------------------------------------ data
        [Test]
        public void TheBuiltInSpellingDataIsValidAndCompleteAndTheDefaultsAreSafe()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            Assert.IsEmpty(c.Warnings, string.Join("\n", c.Warnings));
            SpellingBalance b = c.Spelling;
            Assert.AreEqual(26, b.candidateKeys.Count);
            CollectionAssert.IsSupersetOf(b.reservedKeys, new[] { "Y", "N", "T", "Space", "Tab", "Enter", "Escape", "F1", "Digit1" });
            CollectionAssert.Contains(b.movementActions, "Move");
            Assert.IsTrue(b.excludeGameplayKeys);
            Assert.IsTrue(c.SpellingRules.All(r => r.use == SpellingUse.Yes));
        }

        [Test]
        public void ConditionsCarryTheirSpellingNumbers()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            SoulCondition stable = c.Balance.Condition("stable"), strained = c.Balance.Condition("strained"), critical = c.Balance.Condition("critical");
            Assert.AreEqual(0, stable.spellingExtraLetters);
            Assert.AreEqual(1f, stable.spellingWindowMultiplier);
            Assert.AreEqual(1, strained.spellingExtraLetters);
            Assert.Less(critical.spellingWindowMultiplier, strained.spellingWindowMultiplier);
            Assert.Greater(critical.spellingPenaltyMultiplier, strained.spellingPenaltyMultiplier);
            Assert.AreEqual(2, Condition(c, 30f).spellingExtraLetters, "and they reach the soul's restrictions");
            Assert.AreEqual(1f, SoulRestrictions.None.spellingWindowMultiplier);
        }

        [Test]
        public void AnOlderDatabaseIsUpgradedWithTheSpellingData()
        {
            RuneMagicData old = RuneDefaults.Create();
            old.version = 6;
            old.balance.spelling = null;
            old.spellingRules.Clear();
            foreach (SoulCondition c in old.balance.soulConditions) { c.spellingExtraLetters = 0; c.spellingWindowMultiplier = 1f; c.spellingPenaltyMultiplier = 1f; }
            Assert.IsTrue(old.Upgrade());
            Assert.IsNotNull(old.balance.spelling);
            Assert.GreaterOrEqual(old.spellingRules.Count, 4);
            Assert.AreEqual(1, old.balance.soulConditions.Find(x => x.id == "strained").spellingExtraLetters);
            Assert.AreEqual(RuneMagicData.CurrentVersion, old.version);
            Assert.IsTrue(RuneCatalog.Compile(old).IsValid);
        }

        [Test]
        public void SpellingDataRoundTripsThroughJson()
        {
            RuneMagicData d = RuneDefaults.Create();
            d.balance.spelling.mode = SpellingMode.All;
            d.balance.spelling.baseKeyWindow = 2.25f;
            d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Effect, id = "offense.pierce", use = SpellingUse.No, keyWindows = new List<float> { 1f, 2f } });
            RuneMagicData back = UnityEngine.JsonUtility.FromJson<RuneMagicData>(UnityEngine.JsonUtility.ToJson(d));
            Assert.AreEqual(SpellingMode.All, back.balance.spelling.mode);
            Assert.AreEqual(2.25f, back.balance.spelling.baseKeyWindow, 1e-6f);
            SpellingRule r = back.spellingRules.Last();
            Assert.AreEqual("offense.pierce", r.id);
            CollectionAssert.AreEqual(new[] { 1f, 2f }, r.keyWindows);
        }

        [Test]
        public void BadSpellingDataIsReported()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Form, id = "no_such_form", use = SpellingUse.Yes });
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Effect, id = "offense.pierce", keyWindows = new List<float> { 1f, 0f } });
                d.spellingRules.Add(new SpellingRule { scope = SpellingScope.Effect, id = "offense.slash", letters = 0 });
                d.balance.spelling.weights.Add(new SpellingWeight("no_such_term", 1f));
                d.balance.spelling.maxLetters = 1;
                d.balance.spelling.minLetters = 3;
            });
            Assert.IsFalse(c.IsValid);
            string errors = string.Join("\n", c.Errors);
            StringAssert.Contains("no_such_form", errors);
            StringAssert.Contains("window", errors);
            StringAssert.Contains("Letters", errors);
            StringAssert.Contains("Max Letters", errors);
            Assert.IsTrue(c.Warnings.Any(w => w.Contains("no_such_term")));
        }

        [Test]
        public void ADatabaseExtensionCanAddSpellingRulesWithoutTouchingTheBase()
        {
            var ext = new RuneMagicData
            {
                version = RuneMagicData.CurrentVersion,
                spellingRules = new List<SpellingRule> { new SpellingRule { scope = SpellingScope.Form, id = "bolt", use = SpellingUse.Yes } },
            };
            RuneCatalog c = RuneCatalog.Compile(RuneDefaults.Create(), new List<RuneMagicData> { ext });
            Assert.IsTrue(SpellingGenerator.Uses(c, Spell("offense.pierce")), "the extension marks the bolt form");
            Assert.IsTrue(SpellingGenerator.Uses(c, Spell("offense.explosion")), "the base rules are still there");
        }

        [Test]
        public void TheSpellingRequestKindsAreAppendedAndFixedOnTheWire()
        {
            Assert.AreEqual(12, (int)RuneRequestKind.SpellingStart);
            Assert.AreEqual(13, (int)RuneRequestKind.SpellingResult);
            Assert.AreEqual(RuneRequestKind.GrimoireErase, RuneNetLimits.LastKind, "the grimoire update appended four kinds after the spelling ones");
            Assert.AreEqual(RuneRequestKind.SpellingResult + 1, RuneRequestKind.GrimoireOpen);
            Assert.AreEqual(RuneNetLimits.MaxSpellingResultText, RuneNetLimits.MaxTextFor(RuneRequestKind.SpellingResult));
            Assert.GreaterOrEqual(RuneNetLimits.MaxTextFor(RuneRequestKind.SpellingStart), SpellingKeyPool.MaxPoolKeys * 2);
            Assert.AreEqual(SpellingResultCodec.MaxText, RuneNetLimits.MaxSpellingResultText);
            Assert.AreEqual(RuneNetLimits.MaxRequestText, RuneNetLimits.MaxTextFor(RuneRequestKind.Release));
        }

        [Test]
        public void ARuneUserThatNeverSpellsIsUnchangedBecauseNoneIsTheOnlyOffSwitchNeeded()
        {
            // The Spelling System only adds data: with the mode None nothing in a spell's cast, cost or tooltip changes.
            RuneCatalog c = Custom(d => d.balance.spelling.mode = SpellingMode.None);
            foreach (string effect in new[] { "offense.explosion", "defense.barrier", "movement.dash", "control.immobilize" })
                Assert.IsFalse(SpellingGenerator.Uses(c, Spell(effect)), effect);
        }
    }
}
#endif
