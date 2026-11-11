#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the synthesis engine (Phase 5; design spec 6.1, 11-13, 18-19): the score and its tiers, order and
    /// energy, insufficient energy against an invalid formula, what each outcome does to the runes and the soul, rank
    /// advancement and composite runes, anchoring, determinism. Pure rules: no scene, no UnityEngine objects.
    /// </summary>
    public class SynthesisEngineTests
    {
        private const string Pyro = "WWWAVVV";

        // ------------------------------------------------------------------ the score and its tiers (design spec 12)
        [Test]
        public void TheTierBoundariesFollowTheBalance()
        {
            RuneBalance b = RuneDefaults.Catalog().Balance;
            Assert.AreEqual(SynthesisTier.Perfect, b.TierOf(100f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(99.99f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(70f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(69.99f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(30f));
            Assert.AreEqual(SynthesisTier.CompleteFailure, b.TierOf(29.99f));
            Assert.AreEqual(SynthesisTier.CompleteFailure, b.TierOf(0f));
        }

        [Test]
        public void TheIdealPyroclasticFormulaIsAPerfectSynthesis()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready(Pyro));
            Assert.AreEqual(100f, ev.score, 0.001f);
            Assert.AreEqual(SynthesisTier.Perfect, ev.tier);
            Assert.AreEqual("pyroclastic_resonance", ev.archetypeId);
            Assert.AreEqual(1f, ev.archetypeFidelity, 1e-4f);
            Assert.IsFalse(ev.Incoherent);
            Assert.IsFalse(ev.AnyInsufficient);
            foreach (float f in new[] { ev.factors.coverage, ev.factors.compatibility, ev.factors.intention, ev.factors.order, ev.factors.energy, ev.factors.execution })
                Assert.AreEqual(1f, f, 1e-4f);
        }

        [Test]
        public void ChangingTheCountsOfThePyroclasticFormulaLowersTheScore()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ideal = lab.Evaluate(lab.Ready("WWWAVVV"));
            SynthesisEvaluation oneOff = lab.Evaluate(lab.Ready("WWWAAVVV"));
            SynthesisEvaluation twoOff = lab.Evaluate(lab.Ready("WWAAVVV"));
            Assert.AreEqual(SynthesisTier.Perfect, ideal.tier);
            Assert.AreEqual(SynthesisTier.Successful, oneOff.tier, "3 + 2 + 3 still works, not flawlessly");
            Assert.AreEqual(SynthesisTier.Flawed, twoOff.tier, "2 + 2 + 3 is flawed (design spec 18)");
            Assert.Greater(ideal.score, oneOff.score);
            Assert.Greater(oneOff.score, twoOff.score);
            Assert.IsFalse(twoOff.Incoherent, "a flawed formula is still a coherent one");
        }

        [Test]
        public void TheEvaluationIsDeterministic()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("WWWVVVA", 1.2f);
            SynthesisEvaluation a = lab.Evaluate(req), b = lab.Evaluate(req);
            Assert.AreEqual(a.score, b.score);
            Assert.AreEqual(a.risk, b.risk);
            Assert.AreEqual(a.factors.order, b.factors.order);
            Assert.AreEqual(a.slots.Count, b.slots.Count);
            for (int i = 0; i < a.slots.Count; i++) Assert.AreEqual(a.slots[i].idealEnergy, b.slots[i].idealEnergy);
        }

        // ------------------------------------------------------------------ order (design spec 11.3)
        [Test]
        public void TheOrderOfTheRunesChangesTheScoreInProportionToHowFarOffItIs()
        {
            var lab = new SynthLab();
            float ideal = lab.Evaluate(lab.Ready("WWWAVVV")).score;
            float ashLast = lab.Evaluate(lab.Ready("WWWVVVA")).score;
            float interleaved = lab.Evaluate(lab.Ready("WVWAVWV")).score;
            Assert.Greater(ideal, ashLast, "a rune out of place costs score");
            Assert.Greater(ashLast, interleaved, "more runes out of place cost more");
            Assert.Greater(interleaved, 30f, "a muddled order is still a working formula");
        }

        [Test]
        public void AFullyReversedSequenceIsFundamentallyIncoherent()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready("VVVAWWW"));
            Assert.IsTrue(ev.Incoherent);
            Assert.AreEqual(SynthesisTier.CompleteFailure, ev.tier, "whatever the score, it fails (design spec 13.2)");
            Assert.LessOrEqual(ev.factors.order, lab.c.Synthesis.orderIncoherentBelow);
            StringAssert.Contains("sequence", ev.incoherence[0]);
        }

        [Test]
        public void TheOrderOnlyMattersWhenTheRunesActAtDifferentPointsOfTheChain()
        {
            // Armor and crystal both act at the first stage of a barrier: swapping them changes nothing.
            var lab = new SynthLab();
            lab.Add("earth"); lab.Add("crystal"); lab.Add("armor");
            var a = new SynthesisRequest { intentionId = "defense", effectId = "defense.barrier" };
            a.slots.Add(new SynthesisSlot("r0", "earth.solidity", 0f));
            a.slots.Add(new SynthesisSlot("r1", "crystal.growth", 0f));
            a.slots.Add(new SynthesisSlot("r2", "armor.fortification", 0f));
            lab.Fill(a);
            SynthesisRequest b = a.Clone();
            b.slots.Reverse();
            Assert.AreEqual(lab.Evaluate(a).factors.order, lab.Evaluate(b).factors.order, 1e-4f);
        }

        // ------------------------------------------------------------------ energy (design spec 11.4, 11.5)
        [Test]
        public void EnergyWithinTheOptimalBandIsPerfectAndMoreOrLessIsWorse()
        {
            var lab = new SynthLab();
            float at(float mult) => lab.Evaluate(lab.Ready(Pyro, mult)).score;
            Assert.AreEqual(100f, at(1f), 0.001f);
            Assert.AreEqual(100f, at(0.9f), 0.001f, "the optimum is a band, not a point");
            Assert.AreEqual(100f, at(1.1f), 0.001f);
            Assert.Less(at(0.75f), 100f);
            Assert.Less(at(0.6f), at(0.75f));
            Assert.Less(at(1.4f), at(1.25f));
            Assert.Less(at(2f), at(1.4f));
        }

        [Test]
        public void TheEnergyRatiosBetweenTheRunesMatterNotOnlyTheAmounts()
        {
            var lab = new SynthLab();
            SynthesisRequest ideal = lab.Ready(Pyro);
            List<string> ids = lab.Pattern(Pyro);
            SynthesisRequest equal = lab.Fill(lab.Request(Pyro, ids), 1f, null, true);
            SynthesisRequest ashHeavy = lab.Fill(lab.Request(Pyro, lab.Pattern(Pyro)), 1f, new[] { 1f, 1f, 1f, 2.5f, 1f, 1f, 1f });
            float s0 = lab.Evaluate(ideal).score;
            Assert.Greater(s0, lab.Evaluate(equal).score, "the same energy for every rune is not the ideal spread");
            Assert.Greater(s0, lab.Evaluate(ashHeavy).score, "one rune carrying too much");
        }

        [Test]
        public void ExcessiveEnergyInterfersAndDestabilizes()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ideal = lab.Evaluate(lab.Ready(Pyro));
            SynthesisEvaluation excess = lab.Evaluate(lab.Ready(Pyro, 3f));
            foreach (SlotEvaluation se in excess.slots) Assert.AreEqual(EnergyState.Excessive, se.state);
            Assert.Greater(excess.interference, 0.5f);
            Assert.Greater(excess.risk, ideal.risk, "interference raises the risk");
            Assert.Greater(excess.failureChance, ideal.failureChance, "and the chance the magic falters (design spec 11.5)");
            Assert.Less(excess.stats.stability, ideal.stats.stability);
            Assert.IsFalse(excess.Incoherent, "excess energy is not an invalid formula");
        }

        [Test]
        public void ARuneThatGetsLessThanTheLeastItNeedsCannotTakePart()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready(Pyro, 0.3f));
            Assert.IsTrue(ev.AnyInsufficient);
            foreach (SlotEvaluation se in ev.slots)
            {
                Assert.AreEqual(EnergyState.Insufficient, se.state);
                Assert.Less(se.energy, se.minEnergy);
                Assert.Greater(se.maxSafeEnergy, se.idealEnergy);
                Assert.Greater(se.idealEnergy, se.minEnergy);
            }
        }

        // ------------------------------------------------------------------ insufficient energy against an invalid formula (design spec 13.4)
        [Test]
        public void InsufficientEnergyCannotProceedAndCostsNothing()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro, 0.3f);
            string before = lab.Json();
            SynthesisResult r = lab.Execute(req);
            Assert.IsFalse(r.performed);
            Assert.AreEqual(SynthesisRefusal.InsufficientEnergy, r.refusal);
            StringAssert.Contains("Nothing was spent", r.message);
            Assert.AreEqual(before, lab.Json(), "no energy, no rune, no log entry");
            Assert.IsEmpty(r.soulDamage, "and no Soul Damage");
        }

        [Test]
        public void InsufficientEnergyIsCheckedBeforeTheFormulaIsJudged()
        {
            // The formula cannot work (nothing provides combustion), but the player is told about the energy first, at no cost.
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("WWWWWWW", 0.1f);
            string before = lab.Json();
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.InsufficientEnergy, r.refusal);
            Assert.AreEqual(before, lab.Json());
        }

        [Test]
        public void AnInvalidFormulaFailsWithPenaltiesWhateverTheEnergy()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("WWWWWWW"); // an explosion with nothing that burns or resonates
            float held = 0f;
            foreach (RuneInstance r in lab.s.runes) held += r.energy;
            SynthesisResult result = lab.Execute(req);
            Assert.IsTrue(result.performed, "an invalid formula is attempted, and fails");
            Assert.IsTrue(result.incoherent);
            Assert.AreEqual(SynthesisTier.CompleteFailure, result.tier);
            Assert.IsNotEmpty(result.severity);
            Assert.Greater(result.energyLost, 0f, "energy is lost");
            float after = 0f;
            foreach (RuneInstance r in lab.s.runes) after += r.energy;
            Assert.AreEqual(held - result.energyLost, after, 0.05f);
            Assert.IsEmpty(lab.s.spells);
            Assert.AreEqual(1, lab.s.synthesesFailed);
            Assert.IsTrue(result.soulDamage.Exists(d => d.kind == "synthesis_failure" && d.amount > 0f), "and the soul is strained");
            Assert.AreEqual(1, lab.s.synthesisLog.Count);
            StringAssert.StartsWith("failed", lab.s.synthesisLog[0].outcome);
            StringAssert.Contains("failed", result.message);
        }

        [Test]
        public void APurposeThatDoesNotSupportTheEffectFailsCompletely()
        {
            var lab = new SynthLab();
            SynthesisResult r = lab.Execute(lab.Ready(Pyro, 1f, "defense", "offense.explosion"));
            Assert.IsTrue(r.performed);
            Assert.AreEqual(SynthesisTier.CompleteFailure, r.tier);
            Assert.IsTrue(r.incoherent);
            StringAssert.Contains("does not support", r.evaluation.incoherence[0]);
            StringAssert.Contains("does not support", r.message, "the player learns why a formula is fundamentally wrong");
        }

        [Test]
        public void ConceptsThatCannotProvideTheEffectFailTheFormulaNotTheEnergy()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready("FFww")); // fire and water carry no pressure or compression
            Assert.IsTrue(ev.Incoherent);
            Assert.Less(ev.factors.coverage, lab.c.Synthesis.hardCoverageFloor + 0.2f);
            StringAssert.Contains("cannot be produced", ev.incoherence[0]);
        }

        // ------------------------------------------------------------------ the outcomes (design spec 12.2, 13)
        [Test]
        public void ASuccessfulSynthesisMakesASpellThatAnchorsItsRunes()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            var before = new List<float>();
            foreach (RuneInstance r in lab.s.runes) before.Add(r.energy);
            SynthesisResult result = lab.Execute(req);

            Assert.IsTrue(result.performed);
            Assert.AreEqual(SynthesisTier.Perfect, result.tier);
            Assert.AreEqual(SynthesisOutput.Spell, result.output);
            Assert.AreEqual("Pyroclastic Resonance", result.name);
            Assert.AreEqual(1, lab.s.spells.Count);
            SpellRecord spell = lab.s.spells[0];
            Assert.AreSame(spell, result.spell);
            Assert.AreEqual(7, spell.anchors.Count, "the runes stay in the soul, anchoring the spell (decision D3)");
            Assert.AreEqual(7, lab.s.runes.Count);
            Assert.AreEqual("attack", spell.intentionId);
            Assert.AreEqual("offense.explosion", spell.effectId);
            Assert.AreEqual("pyroclastic_resonance", spell.archetypeId);
            Assert.AreEqual((int)SynthesisTier.Perfect, spell.tier);
            Assert.IsFalse(spell.flawed);
            Assert.Greater(spell.power, 0f);
            Assert.Greater(spell.stability, 0f);
            Assert.AreEqual(7, spell.ingredients.Count);

            float given = 0f;
            for (int i = 0; i < 7; i++)
            {
                given += req.slots[i].energy;
                Assert.AreEqual(before[i] - req.slots[i].energy, lab.s.runes[i].energy, 0.01f, "the energy given is used up");
                CollectionAssert.Contains(lab.s.runes[i].history, result.synthesisId);
            }
            Assert.AreEqual(given, result.energyConsumed, 0.05f);
            Assert.AreEqual(given, spell.investedEnergy, 0.05f);
            Assert.AreEqual(1, lab.s.synthesesPerfect);
            StringAssert.StartsWith("Perfect synthesis!", result.message);
        }

        [Test]
        public void AFlawedSynthesisCarriesFlawsThatWeakenIt()
        {
            var lab = new SynthLab();
            SynthesisResult flawed = lab.Execute(lab.Ready("WWAAVVV"));
            Assert.AreEqual(SynthesisTier.Flawed, flawed.tier);
            Assert.IsTrue(flawed.spell.flawed);
            Assert.GreaterOrEqual(flawed.flaws.Count, 1);
            foreach (string id in flawed.flaws) Assert.IsNotNull(lab.c.Flaw(id), id);
            CollectionAssert.AreEqual(flawed.flaws, flawed.spell.flaws);
            Assert.AreEqual(1, lab.s.synthesesFlawed);
            StringAssert.Contains("flawed", flawed.message);

            var other = new SynthLab();
            SynthesisResult perfect = other.Execute(other.Ready(Pyro));
            Assert.Less(flawed.spell.power, perfect.spell.power);
            Assert.Less(flawed.spell.stability, perfect.spell.stability);
            Assert.GreaterOrEqual(flawed.spell.energyCostMultiplier, perfect.spell.energyCostMultiplier);
        }

        [Test]
        public void ThePerfectTierIsOnlyReachedByAFlawlessFormula()
        {
            var lab = new SynthLab();
            foreach (string pattern in new[] { "WWWAAVVV", "WWWVVVA", "WVWAVWV", "WWWAVVF" })
                Assert.AreNotEqual(SynthesisTier.Perfect, lab.Evaluate(lab.Ready(pattern)).tier, pattern);
            Assert.AreNotEqual(SynthesisTier.Perfect, lab.Evaluate(lab.Ready(Pyro, 1.4f)).tier);
            Assert.AreNotEqual(SynthesisTier.Perfect, lab.Evaluate(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 20f)).tier,
                "poor runes cannot make a perfect synthesis");
        }

        [Test]
        public void FlawsGrowInNumberAndStrengthWithTheDepthInTheFlawedBand()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var ev = new SynthesisEvaluation
            {
                factors = new FactorScores { coverage = 0.5f, compatibility = 0.5f, intention = 0.5f, order = 0.5f, energy = 0.5f, execution = 0.5f },
                interference = 0.5f,
            };
            var rng = new SynthesisRng(5);
            List<SynthesisFlawDef> shallow = SynthesisOutcomes.PickFlaws(c, ev, 69f, ref rng, out float shallowSeverity);
            List<SynthesisFlawDef> deep = SynthesisOutcomes.PickFlaws(c, ev, 31f, ref rng, out float deepSeverity);
            Assert.Less(shallowSeverity, 0.1f);
            Assert.Greater(deepSeverity, 0.9f);
            Assert.AreEqual(1, shallow.Count);
            Assert.AreEqual(3, deep.Count);
            Assert.AreEqual(deep.Count, new HashSet<string>(deep.ConvertAll(f => f.id)).Count, "no flaw twice");

            var weak = new SynthesisStats { power = 100f, stability = 80f, energyCostMultiplier = 1f, quality = 60f };
            var strong = weak;
            var effects = new List<string>();
            SynthesisOutcomes.ApplyFlaws(ref weak, deep, 0.1f, effects, out _);
            SynthesisOutcomes.ApplyFlaws(ref strong, deep, 1f, effects, out _);
            Assert.Greater(weak.power, strong.power, "the same flaws bite harder at the bottom of the band");
            Assert.Greater(weak.stability, strong.stability);
        }

        [Test]
        public void ThePickedFlawsMatchWhatHeldTheScoreBack()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var ev = new SynthesisEvaluation { factors = new FactorScores { coverage = 1f, compatibility = 1f, intention = 1f, order = 0.2f, energy = 1f, execution = 1f } };
            var rng = new SynthesisRng(9);
            List<SynthesisFlawDef> flaws = SynthesisOutcomes.PickFlaws(c, ev, 60f, ref rng, out _);
            Assert.AreEqual(1, flaws.Count);
            Assert.AreEqual(FlawCause.Order, flaws[0].cause, "a poor order gives an order flaw");

            ev.factors = new FactorScores { coverage = 1f, compatibility = 1f, intention = 1f, order = 1f, energy = 0.3f, execution = 1f };
            ev.interference = 0.9f;
            flaws = SynthesisOutcomes.PickFlaws(c, ev, 60f, ref rng, out _);
            Assert.AreEqual(FlawCause.EnergyExcess, flaws[0].cause, "energy beyond what the runes hold gives a volatile result");
        }

        [Test]
        public void AFailureLosesEnergyByItsSeverity()
        {
            RuneCatalog c = SynthLab.Custom(d => d.balance.failureSeverities = new List<FailureSeverity> { new FailureSeverity("Only", 0f, 0f, 0.4f, false) });
            var lab = new SynthLab(c);
            SynthesisRequest req = lab.Ready("WWWWWWW");
            var before = new List<float>();
            foreach (RuneInstance r in lab.s.runes) before.Add(r.energy);
            SynthesisResult result = lab.Execute(req);
            Assert.AreEqual("Only", result.severity);
            float lost = 0f;
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(before[i] - 0.4f * req.slots[i].energy, lab.s.runes[i].energy, 0.05f);
                lost += 0.4f * req.slots[i].energy;
            }
            Assert.AreEqual(lost, result.energyLost, 0.1f);
            Assert.AreEqual(7, lab.s.runes.Count, "a failure that does not destroy keeps every rune");
            Assert.IsFalse(result.runesDestroyed);
        }

        [Test]
        public void ASeverityIsChosenByTheScoreAndTheRisk()
        {
            RuneBalance b = RuneDefaults.Catalog().Balance;
            Assert.AreEqual("Minor", b.SeverityOf(29f, 0.1f).name, "a near miss is punished lightly");
            Assert.AreEqual("Moderate", b.SeverityOf(15f, 0.1f).name);
            Assert.AreEqual("Severe", b.SeverityOf(5f, 0.1f).name);
            Assert.AreEqual("Catastrophic", b.SeverityOf(5f, 0.9f).name, "destruction is reserved for risky attempts that fail badly");
            Assert.AreEqual("Minor", b.SeverityOf(29f, 0.9f).name, "a near miss is never catastrophic");
        }

        [Test]
        public void ACatastrophicFailureDestroysRunesAndAlwaysAtLeastOne()
        {
            Action<RuneMagicData, float> setup = (d, chance) =>
            {
                d.balance.failureSeverities = new List<FailureSeverity> { new FailureSeverity("Doom", 0f, 0f, 1f, true) };
                d.balance.synthesisEngine.destroyChancePerRune = chance;
            };
            var all = new SynthLab(SynthLab.Custom(d => setup(d, 1f)));
            SynthesisResult r = all.Execute(all.Ready("WWWWWWW"));
            Assert.IsTrue(r.runesDestroyed);
            Assert.AreEqual(7, r.destroyedRunes.Count);
            Assert.IsEmpty(all.s.runes);

            var one = new SynthLab(SynthLab.Custom(d => setup(d, 0f)));
            r = one.Execute(one.Ready("WWWWWWW"));
            Assert.AreEqual(1, r.destroyedRunes.Count, "a catastrophe always costs a rune");
            Assert.AreEqual(6, one.s.runes.Count);
        }

        [Test]
        public void DestroyingAnAnchoringRuneLosesTheSpellItAnchored()
        {
            RuneCatalog c = SynthLab.Custom(d =>
            {
                d.balance.failureSeverities = new List<FailureSeverity> { new FailureSeverity("Doom", 0f, 0f, 1f, true) };
                d.balance.synthesisEngine.destroyChancePerRune = 1f;
            });
            var lab = new SynthLab(c);
            SynthesisResult made = lab.Execute(lab.Ready(Pyro));
            Assert.AreEqual(1, lab.s.spells.Count);

            // A doomed second synthesis that uses one of the anchoring runes.
            string anchoring = lab.s.spells[0].anchors[0];
            string another = lab.Add("wind").instanceId;
            var bad = new SynthesisRequest { intentionId = "attack", effectId = "offense.explosion", seed = 3 };
            bad.slots.Add(new SynthesisSlot(anchoring, "wind.pressure", 0f));
            bad.slots.Add(new SynthesisSlot(another, "wind.pressure", 0f));
            lab.Fill(bad);
            SynthesisResult r = lab.Execute(bad);
            Assert.IsTrue(r.runesDestroyed, r.message);
            CollectionAssert.Contains(r.lostSpells, "Pyroclastic Resonance");
            Assert.IsEmpty(lab.s.spells);
            StringAssert.Contains("Lost with them", r.message);
        }

        // ------------------------------------------------------------------ refusals cost nothing
        [Test]
        public void ARefusedSynthesisChangesNothing()
        {
            var lab = new SynthLab();
            SynthesisRequest ok = lab.Ready(Pyro);
            string before = lab.Json();

            void Refused(SynthesisRequest req, SynthesisRefusal expected)
            {
                SynthesisResult r = lab.Execute(req);
                Assert.IsFalse(r.performed, expected.ToString());
                Assert.AreEqual(expected, r.refusal, r.message);
                Assert.IsNotEmpty(r.message);
                Assert.AreEqual(before, lab.Json(), "refused: " + expected);
            }

            SynthesisRequest q = ok.Clone(); q.slots[0].runeInstanceId = "nope"; Refused(q, SynthesisRefusal.RuneNotOwned);
            q = ok.Clone(); q.slots[1].runeInstanceId = q.slots[0].runeInstanceId; Refused(q, SynthesisRefusal.DuplicateRune);
            q = ok.Clone(); q.slots[0].energy = 1e7f; Refused(q, SynthesisRefusal.InvalidEnergy);
            q = ok.Clone(); q.slots[0].energy = -1f; Refused(q, SynthesisRefusal.InvalidEnergy);
            q = ok.Clone(); q.slots[0].energy = float.NaN; Refused(q, SynthesisRefusal.InvalidEnergy);
            q = ok.Clone(); q.slots[0].conceptId = ""; Refused(q, SynthesisRefusal.ConceptRequired);
            q = ok.Clone(); q.slots[0].conceptId = "no.such"; Refused(q, SynthesisRefusal.UnknownConcept);
            q = ok.Clone(); q.slots[0].conceptId = "ash.combustion"; Refused(q, SynthesisRefusal.ConceptNotOfRune);
            q = ok.Clone(); q.slots[0].conceptId = "@essence"; Refused(q, SynthesisRefusal.UnknownConcept);
            q = ok.Clone(); q.intentionId = "no_such"; Refused(q, SynthesisRefusal.UnknownIntention);
            q = ok.Clone(); q.effectId = "no.such"; Refused(q, SynthesisRefusal.UnknownEffect);
            q = ok.Clone(); q.slots.Clear(); Refused(q, SynthesisRefusal.TooFewRunes);
            Refused(null, SynthesisRefusal.NoRequest);
        }

        [Test]
        public void ADiscoveredConceptIsRequired()
        {
            var lab = new SynthLab();
            string id = lab.Add("wind", 1, 60f, 1e9f, false).instanceId; // no concept discovered
            var req = new SynthesisRequest { intentionId = "attack", effectId = "offense.explosion" };
            req.slots.Add(new SynthesisSlot(id, "wind.pressure", 10f));
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.ConceptNotKnown, r.refusal);
            StringAssert.Contains("discovered", r.message);
        }

        [Test]
        public void TooManyRunesAreRefused()
        {
            var lab = new SynthLab();
            int max = lab.c.Synthesis.maxRunes;
            string pattern = new string('W', max + 1);
            SynthesisRequest req = lab.Fill(lab.Request(pattern, lab.Pattern(pattern)));
            Assert.AreEqual(SynthesisRefusal.TooManyRunes, lab.Execute(req).refusal);
        }

        [Test]
        public void ARuneCannotBeConceptlessOrOutOfRank()
        {
            var lab = new SynthLab();
            // A concept that needs a higher rune than the one used.
            ConceptDef demanding = null;
            foreach (RuneDef def in lab.c.Runes)
                foreach (ConceptDef k in def.concepts)
                    if (k.minimumRankToUse > 1) { demanding = k; break; }
            if (demanding == null) { Assert.Pass("no concept in the data needs a higher rank"); return; }
            RuneDef owner = lab.c.RuneOfConcept(demanding.id);
            string id = lab.Add(owner.id).instanceId;
            var req = new SynthesisRequest { intentionId = "attack", effectId = "offense.explosion" };
            req.slots.Add(new SynthesisSlot(id, demanding.id, 10f));
            Assert.AreEqual(SynthesisRefusal.ConceptRankTooLow, lab.Execute(req).refusal);
        }

        // ------------------------------------------------------------------ determinism and chance
        [Test]
        public void TheSameSeedGivesTheSameSynthesis()
        {
            string run(int seed)
            {
                var lab = new SynthLab();
                SynthesisResult r = lab.Execute(lab.Ready("WWAAVVV", 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 60f, seed));
                return r.synthesisId + "|" + r.score + "|" + string.Join(",", r.flaws) + "|" + lab.Json();
            }
            Assert.AreEqual(run(77), run(77));
            Assert.AreNotEqual(run(77), run(78), "the seed is the only source of chance");
        }

        [Test]
        public void ASoulUnderStrainMayFalterAndLoseScore()
        {
            var lab = new SynthLab();
            lab.SoulFailureChance(1f);
            SynthesisResult r = lab.Execute(lab.Ready(Pyro));
            Assert.IsTrue(r.faltered);
            Assert.Less(r.score, 100f - lab.c.Synthesis.falterMin + 0.01f);
            Assert.GreaterOrEqual(r.score, 100f - lab.c.Synthesis.falterMax - 0.01f);
            Assert.AreNotEqual(SynthesisTier.Perfect, r.tier);
            StringAssert.Contains("faltered", r.message);
        }

        [Test]
        public void WithNoChanceOfFalteringTheMagicNeverFalters()
        {
            RuneCatalog c = SynthLab.Custom(d =>
            {
                d.balance.synthesisEngine.riskFailureChance = 0f;
                d.balance.synthesisEngine.interferenceFailureChance = 0f;
            });
            for (int seed = 1; seed <= 40; seed++)
            {
                var lab = new SynthLab(c);
                SynthesisResult r = lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 60f, seed));
                Assert.IsFalse(r.faltered, "seed " + seed);
                Assert.AreEqual(SynthesisTier.Perfect, r.tier);
            }
        }

        // ------------------------------------------------------------------ rank advancement (design spec 6.1)
        [Test]
        public void ARankAdvancementCombinesExactlySevenRunes()
        {
            var lab = new SynthLab();
            foreach (int n in new[] { 1, 6, 8 })
            {
                string pattern = new string('W', n);
                SynthesisRequest req = lab.Fill(lab.Request(pattern, lab.Pattern(pattern), "attack", "offense.explosion", SynthesisMode.RankAdvancement));
                SynthesisResult r = lab.Execute(req);
                Assert.AreEqual(SynthesisRefusal.WrongRuneCount, r.refusal, n + " runes");
            }
        }

        [Test]
        public void ARankAdvancementNeedsRunesOfOneRank()
        {
            var lab = new SynthLab();
            var ids = new List<string>();
            for (int i = 0; i < 7; i++) ids.Add(lab.Add("wind", i == 3 ? 2 : 1).instanceId);
            SynthesisRequest req = lab.Request("WWWWWWW", ids, "attack", "offense.explosion", SynthesisMode.RankAdvancement);
            Assert.AreEqual(SynthesisRefusal.MixedRanks, lab.Execute(lab.Fill(req)).refusal);
        }

        [Test]
        public void SevenRunesOfTheSameKindAdvanceToTheNextRank()
        {
            var lab = new SynthLab();
            List<string> ids = lab.Pattern("WWWWWWW");
            var req = new SynthesisRequest { mode = SynthesisMode.RankAdvancement, seed = 4 };
            foreach (string id in ids) req.slots.Add(new SynthesisSlot(id, "", 0f)); // no formula: a pure advancement needs only energy
            lab.Fill(req);
            string beforeOthers = lab.Json();

            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed, r.message);
            Assert.AreNotEqual(SynthesisTier.CompleteFailure, r.tier);
            Assert.AreEqual(SynthesisOutput.Rune, r.output);
            Assert.AreEqual(1, lab.s.runes.Count, "seven runes became one");
            RuneInstance rune = lab.s.runes[0];
            Assert.AreSame(rune, r.rune);
            Assert.AreEqual("wind", rune.runeId);
            Assert.AreEqual(2, rune.rank);
            Assert.IsFalse(rune.IsComposite, "a pure advancement is the same rune, one rank higher");
            Assert.AreEqual(RuneRules.DisplayName(lab.c, "wind", 2), r.name);
            Assert.AreEqual(1, RuneRules.UsedSlots(lab.c, lab.s), "the other six slots are free again");
            Assert.LessOrEqual(rune.energy, lab.c.Balance.MaxEnergy(2, rune.quality) + 1e-3f);
            Assert.AreEqual("synthesis", rune.origin.kind);
            CollectionAssert.Contains(rune.history, r.synthesisId);
            Assert.AreEqual(2, r.stats.rank);
            Assert.AreNotEqual(beforeOthers, lab.Json());
        }

        [Test]
        public void SevenDifferentRunesAdvanceToACompositeRune()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement);
            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed, r.message);
            Assert.AreEqual(SynthesisTier.Perfect, r.tier);
            Assert.AreEqual(1, lab.s.runes.Count);
            RuneInstance rune = lab.s.runes[0];
            Assert.IsTrue(rune.IsComposite);
            Assert.AreEqual(2, rune.rank, "the example of design spec 18: a rank 2 rune");
            Assert.AreEqual("pyroclastic_resonance", rune.runeId);
            Assert.AreEqual("Pyroclastic Resonance", r.name);
            Assert.AreEqual("Pyroclastic Resonance", RuneRules.DisplayName(lab.c, rune));
            Assert.AreEqual("pyroclastic_resonance", rune.composite.archetypeId);
            Assert.AreEqual(7, rune.composite.ingredients.Count);
            Assert.IsNotEmpty(rune.composite.aspects, "its essence");
            Assert.IsNotEmpty(rune.composite.description);
            Assert.AreEqual(r.synthesisId, rune.composite.synthesisId);
            Assert.AreEqual(1, RuneRules.UsedSlots(lab.c, lab.s));
            Assert.IsNull(lab.c.Rune(rune.runeId), "composite runes are not part of the database");
            foreach (AspectWeight a in rune.composite.aspects)
            {
                Assert.IsNotNull(lab.c.Aspect(a.aspect), a.aspect);
                Assert.Greater(a.weight, 0f);
                Assert.Less(a.weight, 2.0001f);
            }
        }

        [Test]
        public void ARankAdvancementCannotRiseAboveTheHighestRank()
        {
            var lab = new SynthLab();
            int top = Math.Min(lab.c.Rune("wind").maxRank, lab.c.Synthesis.maxRuneRank);
            var ids = new List<string>();
            for (int i = 0; i < 7; i++) ids.Add(lab.Add("wind", top).instanceId);
            var req = new SynthesisRequest { mode = SynthesisMode.RankAdvancement };
            foreach (string id in ids) req.slots.Add(new SynthesisSlot(id, "", 0f));
            Assert.AreEqual(SynthesisRefusal.RankTooHigh, lab.Execute(lab.Fill(req)).refusal);
        }

        [Test]
        public void TheResultOfAConsumingSynthesisMustFitTheSoul()
        {
            RuneCatalog c = SynthLab.Custom(d => d.archetypes.Find(a => a.id == "pyroclastic_resonance").resultSlotCost = 50);
            var lab = new SynthLab(c, 7);
            SynthesisRequest req = lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement);
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.NoCapacity, r.refusal);
            StringAssert.Contains("no room", r.message);
            Assert.AreEqual(7, lab.s.runes.Count);
        }

        [Test]
        public void ACompositeRuneOccupiesItsOwnSlotCost()
        {
            RuneCatalog c = SynthLab.Custom(d => d.archetypes.Find(a => a.id == "pyroclastic_resonance").resultSlotCost = 3);
            var lab = new SynthLab(c, 20);
            SynthesisResult r = lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            Assert.IsTrue(r.performed, r.message);
            Assert.AreEqual(3, RuneRules.SlotCost(c, r.rune));
            Assert.AreEqual(3, RuneRules.UsedSlots(c, lab.s));
        }

        [Test]
        public void AnyCombinationOfRunesCanBeCombinedIntoACompositeRune()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("WAV", 1f, "attack", "offense.explosion", SynthesisMode.CompositeRune);
            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed, r.message);
            Assert.AreEqual(1, lab.s.runes.Count);
            Assert.IsTrue(lab.s.runes[0].IsComposite);
            Assert.AreEqual(1, lab.s.runes[0].rank, "a composite of rank 1 runes is rank 1: only a rank advancement raises the rank");
            Assert.AreEqual("composite", lab.s.runes[0].runeId, "no archetype named it");
            Assert.AreEqual("Wind-Vibration Explosion", r.name, "a name from the runes that carry the formula and the effect");
        }

        [Test]
        public void ACompositeRuneNeedsAtLeastTwoRunes()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("W", 1f, "attack", "offense.explosion", SynthesisMode.CompositeRune);
            Assert.AreEqual(SynthesisRefusal.TooFewRunes, lab.Execute(req).refusal);
        }

        [Test]
        public void ACompositeRuneIsUsedLaterThroughItsEssence()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            RuneInstance composite = made.rune;
            lab.ids.Add(composite.instanceId);

            var req = new SynthesisRequest { intentionId = "attack", effectId = "offense.explosion", seed = 2 };
            // A new composite rune starts with a tenth of its maximum energy: it gives what it holds.
            req.slots.Add(new SynthesisSlot(composite.instanceId, CompositeIds.Essence, composite.energy));
            SynthesisEvaluation ev = lab.Evaluate(req);
            Assert.AreEqual(1, ev.slots.Count);
            StringAssert.Contains("essence", ev.slots[0].conceptName);
            Assert.Greater(ev.factors.coverage, 0.9f, "the essence carries what the seven runes did");
            SynthesisResult spell = lab.Execute(req);
            Assert.IsTrue(spell.performed, spell.message);
            Assert.AreEqual(SynthesisOutput.Spell, spell.output);
            Assert.AreEqual(composite.instanceId, spell.spell.anchors[0]);
        }

        [Test]
        public void SevenIdenticalCompositeRunesAdvanceTogether()
        {
            var lab = new SynthLab();
            var ids = new List<string>();
            for (int i = 0; i < 7; i++)
            {
                var lab2 = new SynthLab();
                SynthesisResult made = lab2.Execute(lab2.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
                RuneInstance copy = made.rune.Clone();
                copy.instanceId = "c" + i;
                copy.energy = lab.c.Balance.MaxEnergy(copy.rank); // charged, as runes made earlier would have been
                lab.s.runes.Add(copy);
                ids.Add(copy.instanceId);
            }
            var req = new SynthesisRequest { mode = SynthesisMode.RankAdvancement, seed = 9 };
            foreach (string id in ids) req.slots.Add(new SynthesisSlot(id, "", 0f));
            lab.Fill(req);
            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed, r.message);
            Assert.AreEqual(1, lab.s.runes.Count);
            Assert.AreEqual(3, lab.s.runes[0].rank);
            Assert.IsTrue(lab.s.runes[0].IsComposite, "the same composite rune, one rank higher");
            Assert.AreEqual("Pyroclastic Resonance", r.name);
        }

        [Test]
        public void AFlawedAdvancementMakesAWeakerRune()
        {
            var good = new SynthLab();
            var weak = new SynthLab();
            SynthesisRequest a = good.Ready("WWWWWWW", 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement);
            foreach (SynthesisSlot sl in a.slots) sl.conceptId = "";
            SynthesisRequest b = weak.Ready("WWWWWWW", 2.5f, "attack", "offense.explosion", SynthesisMode.RankAdvancement);
            foreach (SynthesisSlot sl in b.slots) sl.conceptId = "";
            SynthesisResult perfect = good.Execute(a), rough = weak.Execute(b);
            Assert.IsTrue(perfect.performed && rough.performed);
            Assert.Greater(perfect.score, rough.score);
            if (rough.tier != SynthesisTier.CompleteFailure)
                Assert.GreaterOrEqual(perfect.rune.quality, rough.rune.quality);
        }

        // ------------------------------------------------------------------ anchoring (decision D3)
        [Test]
        public void ARuneThatAnchorsASpellCannotBeReleasedOrUsedUp()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready(Pyro));
            string anchored = made.spell.anchors[0];

            Assert.IsFalse(RuneRules.CanRelease(lab.c.Balance.acquisition, lab.s, anchored, false, out string why));
            StringAssert.Contains("Pyroclastic Resonance", why);
            StringAssert.Contains("dismiss", why);

            var req = new SynthesisRequest { mode = SynthesisMode.CompositeRune, intentionId = "attack", effectId = "offense.explosion" };
            req.slots.Add(new SynthesisSlot(anchored, "wind.pressure", 0f));
            req.slots.Add(new SynthesisSlot(lab.Add("vibration").instanceId, "vibration.resonance", 0f));
            lab.Fill(req);
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.RuneAnchored, r.refusal, "using an anchoring rune up would lose the spell");

            Assert.IsNotNull(SpellRules.Dismiss(lab.s, made.spell.spellId));
            Assert.IsTrue(RuneRules.CanRelease(lab.c.Balance.acquisition, lab.s, anchored, false, out _), "free again once the spell is dismissed");
            Assert.IsNull(SpellRules.Dismiss(lab.s, made.spell.spellId), "only once");
        }

        [Test]
        public void SeveralSpellsMayShareARuneUnlessTheBalanceSaysOtherwise()
        {
            var lab = new SynthLab();
            List<string> ids = lab.Pattern(Pyro);
            lab.Execute(lab.Fill(lab.Request(Pyro, ids, seed: 1)));
            SynthesisResult second = lab.Execute(lab.Fill(lab.Request(Pyro, ids, seed: 2)));
            Assert.IsTrue(second.performed, second.message);
            Assert.AreEqual(2, lab.s.spells.Count);
            Assert.AreEqual(2, SpellRules.AnchoredBy(lab.s, ids[0]).Count);

            var strict = new SynthLab(SynthLab.Custom(d => d.balance.synthesisEngine.runesMayAnchorSeveralSpells = false));
            List<string> sids = strict.Pattern(Pyro);
            strict.Execute(strict.Fill(strict.Request(Pyro, sids, seed: 1)));
            Assert.AreEqual(SynthesisRefusal.RuneAnchored, strict.Execute(strict.Fill(strict.Request(Pyro, sids, seed: 2))).refusal);
        }

        [Test]
        public void TheSpellbookHasALimit()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.balance.synthesisEngine.maxSpells = 1));
            List<string> ids = lab.Pattern(Pyro);
            Assert.IsTrue(lab.Execute(lab.Fill(lab.Request(Pyro, ids, seed: 1))).performed);
            SynthesisResult r = lab.Execute(lab.Fill(lab.Request(Pyro, ids, seed: 2)));
            Assert.AreEqual(SynthesisRefusal.SpellbookFull, r.refusal);
            SpellRules.Dismiss(lab.s, lab.s.spells[0].spellId);
            Assert.IsTrue(lab.Execute(lab.Fill(lab.Request(Pyro, ids, seed: 3))).performed);
        }

        [Test]
        public void ASpellWhoseRuneIsGoneIsRemoved()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready(Pyro));
            Assert.IsEmpty(SpellRules.RemoveBroken(lab.s));
            RuneRules.Release(lab.s, made.spell.anchors[2]);
            List<string> lost = SpellRules.RemoveBroken(lab.s);
            CollectionAssert.AreEqual(new[] { "Pyroclastic Resonance" }, lost);
            Assert.IsEmpty(lab.s.spells);
        }

        // ------------------------------------------------------------------ no recipes (design spec 18, 19)
        [Test]
        public void ArchetypesNameAResultTheyAreNotRecipes()
        {
            // Without any archetype the very same formula scores the same: they only name results and refine fidelity.
            var lab = new SynthLab(SynthLab.Custom(d => d.archetypes.Clear()));
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready(Pyro));
            Assert.AreEqual(100f, ev.score, 0.001f);
            Assert.AreEqual("", ev.archetypeId);
            SynthesisResult r = lab.Execute(lab.Ready(Pyro));
            Assert.AreEqual("Wind-Vibration Explosion", r.name, "an unnamed formula gets a generated name");
        }

        [Test]
        public void ADifferentFormulaForTheSameEffectWorksToo()
        {
            var lab = new SynthLab();
            SynthesisEvaluation three = lab.Evaluate(lab.Ready("WAV"));
            Assert.AreEqual(100f, three.score, 0.001f, "one rune of each also makes a sound explosion");
            Assert.AreEqual("", three.archetypeId, "it is not the Pyroclastic Resonance, just another explosion");
            SynthesisEvaluation withFire = lab.Evaluate(lab.Ready("WWFVV"));
            Assert.IsFalse(withFire.Incoherent);
            Assert.Greater(withFire.score, 30f);
        }

        [Test]
        public void EveryOriginalCombinationOfTheSpecIsACoherentFormula()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.GreaterOrEqual(c.Archetypes.Count, 11);
            foreach (OutcomeArchetype a in c.Archetypes)
            {
                var lab = new SynthLab(c);
                var runes = new List<string>();
                foreach (ArchetypeStage st in a.stages) for (int i = 0; i < st.count; i++) runes.Add(st.runeId);
                var made = new List<string>();
                foreach (string r in runes) made.Add(lab.Add(r).instanceId);
                SynthesisRequest req = SynthesisArchetypes.IdealRequest(a, made);
                lab.Fill(req);
                SynthesisEvaluation ev = lab.Evaluate(req);
                Assert.IsFalse(ev.Incoherent, a.id + ": " + string.Join(" ", ev.incoherence));
                Assert.GreaterOrEqual(ev.score, 70f, a.id + " should at least be a successful synthesis");
                Assert.AreEqual(a.id, ev.archetypeId);
                SynthesisResult r2 = lab.Execute(req);
                Assert.IsTrue(r2.performed, a.id + ": " + r2.message);
                Assert.AreEqual(a.Name, r2.name);
            }
        }

        [Test]
        public void TheOriginalCombinationsHaveVariantsThatAlsoWork()
        {
            // Gravity Well asks for attraction, matter and distance; other runes that bring the same aspects make a well too.
            var lab = new SynthLab();
            RuneCatalog c = lab.c;
            OutcomeArchetype well = c.Archetype("gravity_well");
            var made = new List<string> { lab.Add("gravity").instanceId, lab.Add("matter").instanceId, lab.Add("space").instanceId };
            SynthesisRequest ideal = SynthesisArchetypes.IdealRequest(well, made);
            Assert.GreaterOrEqual(lab.Evaluate(lab.Fill(ideal)).score, 70f);

            SynthesisRequest twoRunes = SynthesisArchetypes.IdealRequest(well, made);
            twoRunes.slots.RemoveAt(1); // without matter: still a working, if different, well
            lab.Fill(twoRunes);
            SynthesisEvaluation ev = lab.Evaluate(twoRunes);
            Assert.IsFalse(ev.Incoherent, string.Join(" ", ev.incoherence));
            Assert.Greater(ev.score, 30f);
        }

        // ------------------------------------------------------------------ independent attributes (directive 7)
        [Test]
        public void RankQualityEnergyAndKnowledgeAreIndependent()
        {
            var lab = new SynthLab();
            SynthesisEvaluation baseline = lab.Evaluate(lab.Ready(Pyro));
            SynthesisEvaluation poor = lab.Evaluate(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 15f));
            Assert.AreEqual(baseline.factors.coverage, poor.factors.coverage);
            Assert.AreEqual(baseline.factors.order, poor.factors.order);
            Assert.AreEqual(baseline.factors.energy, poor.factors.energy, "quality does not change what energy a place asks for");
            Assert.Less(poor.factors.execution, baseline.factors.execution, "quality only counts in the execution");

            SynthesisEvaluation rank2 = lab.Evaluate(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.Spell, 2));
            Assert.AreEqual(baseline.factors.coverage, rank2.factors.coverage);
            Assert.AreEqual(baseline.factors.energy, rank2.factors.energy, 1e-4f, "a higher rank asks for proportionally more energy");
            Assert.Greater(rank2.slots[0].idealEnergy, baseline.slots[0].idealEnergy);
            Assert.Greater(rank2.stats.power, baseline.stats.power, "and makes a stronger spell");

            // Knowledge: a player who knows fewer concepts of a rune is less certain in using it, nothing else changes.
            var lab2 = new SynthLab();
            var ids = new List<string>();
            foreach (char ch in Pyro) ids.Add(lab2.Add(ch == 'W' ? "wind" : ch == 'A' ? "ash" : "vibration", 1, 60f, 1e9f, false).instanceId);
            foreach (string[] k in new[] { new[] { "wind", "wind.pressure" }, new[] { "ash", "ash.combustion" }, new[] { "vibration", "vibration.resonance" } })
                RuneRules.Learn(lab2.s, k[0], k[1]);
            SynthesisEvaluation partial = lab2.Evaluate(lab2.Fill(lab2.Request(Pyro, ids)));
            Assert.AreEqual(baseline.factors.coverage, partial.factors.coverage);
            Assert.AreEqual(baseline.factors.order, partial.factors.order);
            Assert.LessOrEqual(partial.factors.execution, baseline.factors.execution);
        }

        [Test]
        public void ConceptsBeyondTheRuneRankOverreach()
        {
            var lab = new SynthLab();
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready("WWWAVVV"));
            Assert.AreEqual(0f, ev.overreach, 1e-4f, "simple concepts are within a rank 1 rune's reach");
            // soul_chain's shade-binding concept is complex: a rank 1 rune overreaches, a higher rank does not.
            OutcomeArchetype chain = lab.c.Archetype("soul_chain");
            var made = new List<string>();
            foreach (ArchetypeStage st in chain.stages) made.Add(lab.Add(st.runeId).instanceId);
            SynthesisEvaluation low = lab.Evaluate(lab.Fill(SynthesisArchetypes.IdealRequest(chain, made)));
            Assert.Greater(low.overreach, 0f);
            var lab2 = new SynthLab();
            var made2 = new List<string>();
            foreach (ArchetypeStage st in chain.stages) made2.Add(lab2.Add(st.runeId, 3).instanceId);
            SynthesisEvaluation high = lab2.Evaluate(lab2.Fill(SynthesisArchetypes.IdealRequest(chain, made2)));
            Assert.AreEqual(0f, high.overreach, 1e-4f);
            Assert.Greater(high.score, low.score);
        }

        // ------------------------------------------------------------------ names
        [Test]
        public void ResultsAreNamedByThePlayerTheArchetypeOrTheirParts()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            req.name = "  Ash <b>Storm</b> | now  ";
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual("Ash bStorm/b now", r.name, "markup and separators are stripped, spaces collapsed");
            Assert.AreEqual("Ash bStorm/b now", lab.s.spells[0].name);

            var lab2 = new SynthLab();
            SynthesisRequest long1 = lab2.Ready(Pyro);
            long1.name = new string('x', 100);
            Assert.AreEqual(40, lab2.Execute(long1).name.Length);

            var lab3 = new SynthLab();
            SynthesisRequest blank = lab3.Ready(Pyro);
            blank.name = " <> | ";
            Assert.AreEqual("Pyroclastic Resonance", lab3.Execute(blank).name, "nothing left of the name: the archetype's");
            Assert.IsNull(SynthesisOutcomes.CleanName(null));
            Assert.IsNull(SynthesisOutcomes.CleanName("   "));
        }

        // ------------------------------------------------------------------ balance knobs
        [Test]
        public void TheWorseAnIncoherentFormulaTheHarsherItsFailure()
        {
            var lab = new SynthLab();
            RuneCatalog c = lab.c;
            SynthesisEvaluation incoherent = lab.Evaluate(lab.Ready("WWWWWWW"));
            Assert.IsTrue(incoherent.Incoherent);
            float flawed = c.Balance.synthesis.flawed;
            Assert.Less(SynthesisEngine.EffectiveScore(c, incoherent, incoherent.score), flawed, "an incoherent formula is never a near-miss of Flawed");
            Assert.Less(SynthesisEngine.EffectiveScore(c, incoherent, 20f), SynthesisEngine.EffectiveScore(c, incoherent, 50f), "worse formula, harsher severity");
            Assert.AreEqual(10f, SynthesisEngine.EffectiveScore(c, incoherent, 20f), 1e-3f, "the score × the balance's factor");
            SynthesisEvaluation fine = lab.Evaluate(lab.Ready(Pyro));
            Assert.AreEqual(100f, SynthesisEngine.EffectiveScore(c, fine, 100f), "a coherent formula is judged by its score");

            RuneCatalog harsh = SynthLab.Custom(d => d.balance.synthesisEngine.incoherentScoreFactor = 0f);
            var lab2 = new SynthLab(harsh);
            SynthesisResult r = lab2.Execute(lab2.Ready("WWWWWWW"));
            Assert.AreEqual("Severe", r.severity, "with the factor at 0 every incoherent formula is judged at the worst score");
        }

        [Test]
        public void MoreRunesCarryMorePowerButNotWithoutLimit()
        {
            var lab = new SynthLab();
            SynthesisEvaluation three = lab.Evaluate(lab.Ready("WAV"));
            SynthesisEvaluation seven = lab.Evaluate(lab.Ready(Pyro));
            Assert.AreEqual(100f, three.score, 0.001f);
            Assert.AreEqual(100f, seven.score, 0.001f);
            Assert.Greater(seven.stats.power, three.stats.power, "more runes carry more raw magic");
            Assert.Less(seven.stats.power, three.stats.power * 7f / 3f, "but less than in proportion");
            Assert.Greater(seven.risk, three.risk, "and risk more");
        }

        [Test]
        public void TheIdealEnergyOfARuneIsAlwaysWithinWhatItCanHold()
        {
            RuneCatalog c = SynthLab.Custom(d => d.effects.Find(e => e.id == "offense.explosion").powerDemand = 3f);
            var lab = new SynthLab(c);
            SynthesisEvaluation ev = lab.Evaluate(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            float cap = c.Balance.MaxEnergy(1);
            foreach (SlotEvaluation se in ev.slots)
                Assert.LessOrEqual(se.idealEnergy, c.Synthesis.maxIdealShare * cap + 1e-3f, "the optimum is always reachable");
            Assert.AreEqual(100f, ev.score, 0.001f, "and reaching it is a perfect synthesis");
        }

        [Test]
        public void ASynthesisInCombatIsAWorldRuleNotAnEngineRule()
        {
            // The engine has no notion of combat: the soul component enforces it with the balance's flag.
            var lab = new SynthLab();
            Assert.IsFalse(lab.c.Synthesis.allowInCombat);
            Assert.IsTrue(lab.Execute(lab.Ready(Pyro)).performed);
        }

        // ------------------------------------------------------------------ the soul's condition (design spec 14.4)
        [Test]
        public void TheSoulsConditionCanForbidASynthesis()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            string before = lab.Json();

            SoulRestrictions r = SoulRestrictions.None; r.collapseLocked = true;
            lab.ctx.soul = r;
            Assert.AreEqual(SynthesisRefusal.SoulRestriction, lab.Execute(req).refusal);

            r = SoulRestrictions.None; r.synthesisAllowed = false; r.conditionName = "Critical";
            lab.ctx.soul = r;
            SynthesisResult res = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.SoulRestriction, res.refusal);
            StringAssert.Contains("Critical", res.message);

            r = SoulRestrictions.None; r.maxSynthesisRunes = 3;
            lab.ctx.soul = r;
            Assert.AreEqual(SynthesisRefusal.SoulRestriction, lab.Execute(req).refusal, "too many runes for a strained soul");

            r = SoulRestrictions.None; r.maxSynthesisComplexity = 0.2f;
            lab.ctx.soul = r;
            Assert.AreEqual(SynthesisRefusal.SoulRestriction, lab.Execute(req).refusal, "too complex for a strained soul");
            Assert.AreEqual(before, lab.Json());
        }

        [Test]
        public void AStrainedSoulMakesWeakerMagic()
        {
            var calm = new SynthLab();
            var strained = new SynthLab();
            SoulRestrictions r = SoulRestrictions.None; r.synthesisStability = 0.6f;
            strained.ctx.soul = r;
            SynthesisEvaluation a = calm.Evaluate(calm.Ready(Pyro)), b = strained.Evaluate(strained.Ready(Pyro));
            Assert.AreEqual(a.score, b.score, 1e-4f, "the score is the formula's");
            Assert.Less(b.stats.stability, a.stats.stability, "the soul's condition shows in the stability");
        }
    }
}
#endif
