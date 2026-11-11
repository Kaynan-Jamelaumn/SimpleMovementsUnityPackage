#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of what the synthesis window stands on (Phase 6; design spec 11.1): the nine-step draft, what each step
    /// offers and needs, the request on the wire and how the server reads it, the result report, and the guarantee that
    /// a client's preview is the server's. Pure rules: no window, no scene.
    /// </summary>
    public class SynthesisUiCoreTests
    {
        private const string Pyro = "WWWAVVV";

        private static SynthesisEnv Env(SynthLab lab) => new SynthesisEnv(lab.c, lab.s, lab.ctx);

        /// <summary>A draft of a Pyroclastic spell built only with what the window can do (choose, add, pick a concept, set energy).</summary>
        private static SynthesisDraft PyroDraft(SynthLab lab, SynthesisMode mode = SynthesisMode.Spell)
        {
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { mode = mode, intentionId = "attack", effectId = "offense.explosion" };
            foreach (string id in lab.Pattern(Pyro))
                Assert.IsTrue(d.Add(e, id, out string why), why);
            string[] concepts = { "wind.pressure", "wind.pressure", "wind.pressure", "ash.combustion", "vibration.resonance", "vibration.resonance", "vibration.resonance" };
            for (int i = 0; i < 7; i++) Assert.IsTrue(d.SetConcept(e, i, concepts[i]));
            return d;
        }

        // ------------------------------------------------------------------ the nine steps (design spec 11.1)
        [Test]
        public void TheNineStepsOfASpellFollowTheSpecOrder()
        {
            var lab = new SynthLab();
            var d = new SynthesisDraft();
            CollectionAssert.AreEqual(new[]
            {
                SynthesisStep.Intention, SynthesisStep.Effect, SynthesisStep.Runes, SynthesisStep.Concepts, SynthesisStep.Order,
                SynthesisStep.Energy, SynthesisStep.Review, SynthesisStep.Confirm, SynthesisStep.Result,
            }, d.Steps(Env(lab)));
            Assert.AreEqual(9, d.Steps(Env(lab)).Count);
            d.mode = SynthesisMode.CompositeRune;
            Assert.AreEqual(9, d.Steps(Env(lab)).Count, "a new rune has the same steps");
        }

        [Test]
        public void ARankAdvancementOfOneKindOfRuneNeedsNoFormula()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { mode = SynthesisMode.RankAdvancement };
            CollectionAssert.AreEqual(new[] { SynthesisStep.Runes, SynthesisStep.Energy, SynthesisStep.Review, SynthesisStep.Confirm, SynthesisStep.Result }, d.Steps(e),
                "before any rune is chosen, only the runes and the energy");
            foreach (string id in lab.Pattern("WWWWWWW")) Assert.IsTrue(d.Add(e, id, out string why), why);
            Assert.IsTrue(d.IsPure(e));
            CollectionAssert.AreEqual(new[] { SynthesisStep.Runes, SynthesisStep.Energy, SynthesisStep.Review, SynthesisStep.Confirm, SynthesisStep.Result }, d.Steps(e));
            Assert.IsTrue(d.Check(e, SynthesisStep.Concepts, out _), "no concepts to choose");
            SynthesisRequest req = d.BuildRequest(e);
            Assert.AreEqual("", req.intentionId);
            Assert.AreEqual("", req.effectId);
            foreach (SynthesisSlot s in req.slots) Assert.AreEqual("", s.conceptId);
            Assert.AreEqual(SynthesisMode.RankAdvancement, req.mode);
        }

        [Test]
        public void AMixedRankAdvancementAsksForAFormulaAfterTheRunes()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { mode = SynthesisMode.RankAdvancement };
            foreach (string id in lab.Pattern(Pyro)) Assert.IsTrue(d.Add(e, id, out string why), why);
            Assert.IsFalse(d.IsPure(e));
            CollectionAssert.AreEqual(new[]
            {
                SynthesisStep.Runes, SynthesisStep.Intention, SynthesisStep.Effect, SynthesisStep.Concepts, SynthesisStep.Order,
                SynthesisStep.Energy, SynthesisStep.Review, SynthesisStep.Confirm, SynthesisStep.Result,
            }, d.Steps(e));
        }

        [Test]
        public void EachStepIsCompleteOnlyWhenItsChoicesAreMade()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft();
            Assert.IsFalse(d.Check(e, SynthesisStep.Intention, out string why));
            StringAssert.Contains("purpose", why);
            d.intentionId = "attack";
            Assert.IsTrue(d.Check(e, SynthesisStep.Intention, out _));
            Assert.IsFalse(d.Check(e, SynthesisStep.Effect, out why));
            d.effectId = "offense.explosion";
            Assert.IsTrue(d.Check(e, SynthesisStep.Effect, out _));
            Assert.IsFalse(d.Check(e, SynthesisStep.Runes, out why), "no runes yet");
            StringAssert.Contains("Choose", why);

            foreach (string id in lab.Pattern("WAV")) d.Add(e, id, out _);
            Assert.IsTrue(d.Check(e, SynthesisStep.Runes, out _));
            Assert.IsFalse(d.Check(e, SynthesisStep.Concepts, out why), "the runes know several concepts: the player chooses");
            StringAssert.Contains("Choose a concept", why);
            Assert.IsTrue(d.Check(e, SynthesisStep.Order, out _));
            Assert.IsTrue(d.Check(e, SynthesisStep.Energy, out _), "every rune starts with an energy it holds");
            d.runes[0].energy = 1e7f;
            Assert.IsFalse(d.Check(e, SynthesisStep.Energy, out why));
            StringAssert.Contains("holds", why);
        }

        [Test]
        public void TheFirstIncompleteStepIsWhereTheWindowSendsThePlayer()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft();
            Assert.AreEqual(SynthesisStep.Intention, d.FirstIncomplete(e));
            d.intentionId = "attack";
            Assert.AreEqual(SynthesisStep.Effect, d.FirstIncomplete(e));
            d.effectId = "offense.explosion";
            Assert.AreEqual(SynthesisStep.Runes, d.FirstIncomplete(e));
            SynthesisDraft full = PyroDraft(lab);
            Assert.AreEqual(SynthesisStep.Review, full.FirstIncomplete(e));
        }

        [Test]
        public void NextAndPreviousWalkTheStepsThatApply()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft();
            Assert.AreEqual(SynthesisStep.Effect, d.Next(e, SynthesisStep.Intention));
            Assert.AreEqual(SynthesisStep.Intention, d.Previous(e, SynthesisStep.Intention), "no step before the first");
            Assert.AreEqual(SynthesisStep.Result, d.Next(e, SynthesisStep.Result));
            d.mode = SynthesisMode.RankAdvancement;
            Assert.AreEqual(SynthesisStep.Energy, d.Next(e, SynthesisStep.Runes), "a pure advancement skips the formula");
        }

        // ------------------------------------------------------------------ intentions and effects (steps 1, 2)
        [Test]
        public void EffectsAreListedByCategoryWithTheSupportOfTheIntention()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { intentionId = "attack" };
            List<EffectChoice> supported = d.Effects(e, true);
            List<EffectChoice> all = d.Effects(e, false);
            Assert.AreEqual(lab.c.Effects.Count, all.Count);
            Assert.Less(supported.Count, all.Count, "the intention does not support every effect");
            EffectChoice explosion = supported.Find(x => x.def.id == "offense.explosion");
            Assert.IsNotNull(explosion);
            Assert.AreEqual(1f, explosion.support, 1e-4f);
            Assert.AreEqual("supported", explosion.SupportLabel);
            Assert.IsNull(supported.Find(x => x.def.id == "defense.barrier"), "an effect the intention does not support is left out");
            EffectChoice barrier = all.Find(x => x.def.id == "defense.barrier");
            Assert.AreEqual(0f, barrier.support);
            Assert.AreEqual("not supported", barrier.SupportLabel);
            for (int i = 1; i < supported.Count; i++)
                Assert.LessOrEqual(supported[i - 1].def.category, supported[i].def.category, "grouped by category");
        }

        [Test]
        public void TheChosenEffectStaysListedEvenWhenTheIntentionChanges()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { intentionId = "defense", effectId = "offense.explosion" };
            Assert.IsNotNull(d.Effects(e, true).Find(x => x.def.id == "offense.explosion"), "so the player can see and change it");
        }

        [Test]
        public void AnEffectTooComplexForTheSoulIsListedButUnavailable()
        {
            var lab = new SynthLab();
            SoulRestrictions r = SoulRestrictions.None; r.maxSynthesisComplexity = 0.2f; r.conditionName = "Damaged";
            lab.ctx.soul = r;
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { intentionId = "attack", effectId = "offense.explosion" };
            EffectChoice explosion = d.Effects(e, true).Find(x => x.def.id == "offense.explosion");
            Assert.IsFalse(explosion.available);
            StringAssert.Contains("omplex", explosion.reason);
            Assert.IsFalse(d.Check(e, SynthesisStep.Effect, out string why));
            StringAssert.Contains("omplex", why);
        }

        [Test]
        public void EveryEffectCategoryHasAName()
        {
            foreach (EffectCategory c in Enum.GetValues(typeof(EffectCategory)))
                Assert.IsNotEmpty(SynthesisDraft.CategoryName(c));
            foreach (SynthesisMode m in Enum.GetValues(typeof(SynthesisMode)))
                Assert.IsNotEmpty(SynthesisDraft.ModeName(m));
        }

        // ------------------------------------------------------------------ runes and concepts (steps 3, 4)
        [Test]
        public void EachRuneShowsItsEnergyRankQualityAndConcepts()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            lab.Add("wind", 2, 70f, 300f, false);
            RuneRules.Learn(lab.s, "wind", "wind.pressure");
            RuneChoice ch = new SynthesisDraft().RuneChoices(e)[0];
            Assert.AreEqual(2, ch.rank);
            Assert.AreEqual(70f, ch.quality);
            Assert.AreEqual(300f, ch.energy);
            Assert.AreEqual(lab.c.Balance.MaxEnergy(2, 70f), ch.maxEnergy);
            Assert.AreEqual(1, ch.knownConcepts);
            Assert.AreEqual(lab.c.Rune("wind").concepts.Count, ch.totalConcepts);
            Assert.AreEqual(1, ch.slotCost);
            Assert.IsTrue(ch.usable);
            Assert.IsNotEmpty(ch.name);
        }

        [Test]
        public void OnlyDiscoveredConceptsAreOffered()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            string id = lab.Add("wind", 1, 60f, 500f, false).instanceId;
            var d = new SynthesisDraft();
            Assert.IsEmpty(d.Concepts(e, id), "nothing discovered: nothing to choose");
            RuneRules.Learn(lab.s, "wind", "wind.pressure");
            List<ConceptChoice> offered = d.Concepts(e, id);
            Assert.AreEqual(1, offered.Count);
            Assert.AreEqual("wind.pressure", offered[0].id);
            Assert.IsNotEmpty(offered[0].name);
            Assert.IsTrue(d.Add(e, id, out _));
            Assert.AreEqual("wind.pressure", d.runes[0].conceptId, "a single choice is made for the player");
        }

        [Test]
        public void AConceptBeyondTheRunesRankIsOfferedButNotUsable()
        {
            var lab = new SynthLab();
            ConceptDef demanding = null;
            foreach (RuneDef def in lab.c.Runes)
                foreach (ConceptDef k in def.concepts)
                    if (k.minimumRankToUse > 1 && demanding == null) demanding = k;
            if (demanding == null) { Assert.Pass("no concept in the data needs a higher rank"); return; }
            RuneDef owner = lab.c.RuneOfConcept(demanding.id);
            string id = lab.Add(owner.id).instanceId;
            SynthesisEnv e = Env(lab);
            ConceptChoice ch = new SynthesisDraft().Concepts(e, id).Find(k => k.id == demanding.id);
            Assert.IsFalse(ch.usable);
            StringAssert.Contains("rank", ch.reason);
            var d = new SynthesisDraft();
            d.Add(e, id, out _);
            Assert.IsFalse(d.SetConcept(e, 0, demanding.id));
        }

        [Test]
        public void ACompositeRuneOffersItsEssence()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            SynthesisEnv e = Env(lab);
            List<ConceptChoice> offered = new SynthesisDraft().Concepts(e, made.rune.instanceId);
            Assert.AreEqual(1, offered.Count);
            Assert.AreEqual(CompositeIds.Essence, offered[0].id);
            RuneChoice ch = new SynthesisDraft().RuneChoices(e).Find(r => r.instanceId == made.rune.instanceId);
            Assert.IsTrue(ch.composite);
            Assert.AreEqual("Pyroclastic Resonance", ch.name);
            Assert.AreEqual(2, ch.rank);
        }

        [Test]
        public void TheNumberOfRunesFollowsTheModeAndTheSoulsCondition()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft();
            d.CountRule(e, out int min, out int max);
            Assert.AreEqual(lab.c.Synthesis.minRunes, min);
            Assert.AreEqual(lab.c.Synthesis.maxRunes, max);
            StringAssert.Contains("to", d.CountText(e));
            d.mode = SynthesisMode.CompositeRune;
            d.CountRule(e, out min, out _);
            Assert.AreEqual(lab.c.Synthesis.minRunesComposite, min);
            d.mode = SynthesisMode.RankAdvancement;
            d.CountRule(e, out min, out max);
            Assert.AreEqual(7, min);
            Assert.AreEqual(7, max);
            StringAssert.Contains("exactly 7", d.CountText(e));

            SoulRestrictions r = SoulRestrictions.None; r.maxSynthesisRunes = 3;
            lab.ctx.soul = r;
            d.mode = SynthesisMode.Spell;
            d.CountRule(Env(lab), out _, out max);
            Assert.AreEqual(3, max, "a strained soul can combine fewer runes");
        }

        [Test]
        public void RunesAreAddedMovedAndRemovedInOrder()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            List<string> ids = lab.Pattern("WAV");
            var d = new SynthesisDraft();
            foreach (string id in ids) Assert.IsTrue(d.Add(e, id, out _));
            Assert.IsFalse(d.Add(e, ids[0], out string why), "a rune takes one place");
            StringAssert.Contains("already", why);
            Assert.IsFalse(d.Add(e, "ghost", out why));
            CollectionAssert.AreEqual(ids, d.runes.ConvertAll(x => x.runeInstanceId));

            Assert.IsTrue(d.Move(0, 2));
            CollectionAssert.AreEqual(new[] { ids[1], ids[2], ids[0] }, d.runes.ConvertAll(x => x.runeInstanceId));
            Assert.IsFalse(d.Move(0, -1), "no place before the first");
            Assert.IsFalse(d.Move(2, 1), "no place after the last");
            Assert.IsFalse(d.Move(1, 0));
            d.Remove(1);
            CollectionAssert.AreEqual(new[] { ids[1], ids[0] }, d.runes.ConvertAll(x => x.runeInstanceId));
            d.Remove(9); // nothing happens
            Assert.AreEqual(2, d.runes.Count);
        }

        [Test]
        public void TheDraftNeverTakesMoreRunesThanAreAllowed()
        {
            var lab = new SynthLab(SynthLab.Custom(x => x.balance.synthesisEngine.maxRunes = 3));
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft();
            List<string> ids = lab.Pattern("WWWW");
            Assert.IsTrue(d.Add(e, ids[0], out _));
            Assert.IsTrue(d.Add(e, ids[1], out _));
            Assert.IsTrue(d.Add(e, ids[2], out _));
            Assert.IsFalse(d.Add(e, ids[3], out string why));
            StringAssert.Contains("At most 3", why);
        }

        [Test]
        public void EnergyIsSetWithinWhatTheRuneHolds()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            string id = lab.Add("wind", 1, 60f, 400f).instanceId;
            var d = new SynthesisDraft();
            d.Add(e, id, out _);
            Assert.AreEqual(200f, d.runes[0].energy, "a rune starts giving half of what it holds");
            d.SetEnergy(e, 0, 1e6f);
            Assert.AreEqual(400f, d.runes[0].energy);
            d.SetEnergy(e, 0, -20f);
            Assert.AreEqual(0f, d.runes[0].energy);
            d.SetEnergy(e, 0, 123.6f);
            Assert.AreEqual(124f, d.runes[0].energy, "whole numbers");
            d.SetEnergy(e, 0, float.NaN);
            Assert.AreEqual(0f, d.runes[0].energy);
            d.SetEnergy(e, 5, 10f); // nothing happens
        }

        [Test]
        public void ARuneThatAnchorsASpellCannotBeUsedUpButCanBeUsedInAnotherSpell()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));
            SynthesisEnv e = Env(lab);
            var spell = new SynthesisDraft { mode = SynthesisMode.Spell };
            foreach (RuneChoice ch in spell.RuneChoices(e))
            {
                Assert.IsTrue(ch.usable, "a spell may share the runes of another");
                Assert.AreEqual(1, ch.anchors.Count);
                Assert.AreEqual("Pyroclastic Resonance", ch.anchors[0]);
            }
            var composite = new SynthesisDraft { mode = SynthesisMode.CompositeRune };
            foreach (RuneChoice ch in composite.RuneChoices(e))
            {
                Assert.IsFalse(ch.usable);
                StringAssert.Contains("Pyroclastic Resonance", ch.reason);
                StringAssert.Contains("dismiss", ch.reason);
            }
            Assert.IsFalse(composite.Add(e, lab.s.runes[0].instanceId, out string why));
            StringAssert.Contains("dismiss", why);

            var strict = new SynthLab(SynthLab.Custom(x => x.balance.synthesisEngine.runesMayAnchorSeveralSpells = false));
            strict.Execute(strict.Ready(Pyro));
            Assert.IsFalse(new SynthesisDraft().RuneChoices(Env(strict))[0].usable);
        }

        [Test]
        public void ARankAdvancementOnlyOffersRunesOfTheRankOfTheFirstOne()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            string a = lab.Add("wind", 1).instanceId;
            string b = lab.Add("ash", 2).instanceId;
            var d = new SynthesisDraft { mode = SynthesisMode.RankAdvancement };
            Assert.IsTrue(d.Add(e, a, out _));
            RuneChoice other = d.RuneChoices(e).Find(r => r.instanceId == b);
            Assert.IsFalse(other.usable);
            StringAssert.Contains("one rank", other.reason);
            Assert.IsFalse(d.Add(e, b, out _));
            Assert.IsTrue(d.RuneChoices(e).Find(r => r.instanceId == a).chosen);
        }

        [Test]
        public void ARuneAtTheHighestRankCannotAdvance()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            string id = lab.Add("wind", lab.c.Synthesis.maxRuneRank).instanceId;
            RuneChoice ch = new SynthesisDraft { mode = SynthesisMode.RankAdvancement }.RuneChoices(e).Find(r => r.instanceId == id);
            Assert.IsFalse(ch.usable);
            StringAssert.Contains("cannot rise", ch.reason);
        }

        [Test]
        public void ChangingTheModeDropsRunesThatNoLongerFit()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));
            SynthesisEnv e = Env(lab);
            var d = new SynthesisDraft { mode = SynthesisMode.Spell };
            for (int i = 0; i < 3; i++) Assert.IsTrue(d.Add(e, lab.s.runes[i].instanceId, out _));
            d.SetMode(e, SynthesisMode.CompositeRune);
            Assert.IsEmpty(d.runes, "the runes anchor a spell: a new rune would use them up");
            d.SetMode(e, SynthesisMode.Spell);
            Assert.IsEmpty(d.runes, "and they are not brought back");
        }

        [Test]
        public void TheDraftFollowsChangesToTheSoul()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            SynthesisDraft d = PyroDraft(lab);
            RuneRules.Release(lab.s, d.runes[3].runeInstanceId); // a rune was released meanwhile
            lab.s.Rune(d.runes[0].runeInstanceId).energy = 10f;  // one has spent its energy
            d.runes[1].conceptId = "no.such";
            d.Sanitize(e);
            Assert.AreEqual(6, d.runes.Count);
            Assert.LessOrEqual(d.runes[0].energy, 10f);
            Assert.AreEqual("", d.runes[1].conceptId, "a concept the rune cannot give is cleared");
            Assert.IsFalse(d.Check(e, SynthesisStep.Concepts, out _));
        }

        // ------------------------------------------------------------------ the request the window makes
        [Test]
        public void ADraftBuiltWithTheWindowsOperationsIsAPerfectSynthesis()
        {
            var lab = new SynthLab();
            SynthesisEnv e = Env(lab);
            SynthesisDraft d = PyroDraft(lab);
            SynthesisRequest ideal = lab.Fill(d.BuildRequest(e));
            for (int i = 0; i < 7; i++) d.SetEnergy(e, i, ideal.slots[i].energy);
            Assert.IsTrue(d.Check(e, SynthesisStep.Energy, out _));
            SynthesisRequest req = d.BuildRequest(e);
            Assert.AreEqual(7, req.slots.Count);
            Assert.AreEqual("attack", req.intentionId);
            Assert.AreEqual(0, req.seed, "the player never chooses the chance");
            SynthesisPreview p = lab.Preview(req);
            Assert.IsTrue(p.CanAttempt, p.message);
            SynthesisResult r = lab.Execute(req);
            Assert.IsTrue(r.performed);
            Assert.AreEqual("Pyroclastic Resonance", r.name);
            Assert.GreaterOrEqual((int)r.tier, (int)SynthesisTier.Successful, "energies rounded to whole numbers stay within the optimal band");
        }

        [Test]
        public void TheSoulsRequirementsAreShownAsLines()
        {
            var lab = new SynthLab();
            lab.ctx.integrity = 64f;
            List<string> calm = SynthesisDraft.SoulRequirementLines(Env(lab));
            Assert.AreEqual(1, calm.Count);
            StringAssert.Contains("64", calm[0]);

            SoulRestrictions r = SoulRestrictions.None;
            r.conditionName = "Damaged"; r.maxSynthesisRunes = 4; r.maxSynthesisComplexity = 0.5f; r.synthesisFailureChance = 0.15f; r.synthesisStability = 0.8f;
            lab.ctx.soul = r;
            List<string> strained = SynthesisDraft.SoulRequirementLines(Env(lab));
            string all = string.Join("\n", strained);
            StringAssert.Contains("Damaged", all);
            StringAssert.Contains("at most 4 runes", all);
            StringAssert.Contains("complex effects", all);
            StringAssert.Contains("+15%", all);
            StringAssert.Contains("less stable", all);

            r = SoulRestrictions.None; r.collapseLocked = true; r.synthesisAllowed = false;
            lab.ctx.soul = r;
            StringAssert.Contains("collapsed", string.Join("\n", SynthesisDraft.SoulRequirementLines(Env(lab))));
        }

        // ------------------------------------------------------------------ the request on the wire
        [Test]
        public void ARequestSurvivesTheWireWithoutItsSeed()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            req.seed = 1234;
            req.name = "Ash Storm";
            string json = SynthesisRequestCodec.Encode(req);
            Assert.IsTrue(SynthesisRequestCodec.TryDecode(json, out SynthesisRequest back, out string why), why);
            Assert.AreEqual(0, back.seed, "the server chooses the chance");
            Assert.AreEqual(req.mode, back.mode);
            Assert.AreEqual(req.intentionId, back.intentionId);
            Assert.AreEqual(req.effectId, back.effectId);
            Assert.AreEqual("Ash Storm", back.name);
            Assert.AreEqual(7, back.slots.Count);
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(req.slots[i].runeInstanceId, back.slots[i].runeInstanceId);
                Assert.AreEqual(req.slots[i].conceptId, back.slots[i].conceptId);
                Assert.AreEqual(req.slots[i].energy, back.slots[i].energy, 1e-3f);
            }
            Assert.AreEqual(1234, req.seed, "encoding does not change the request");
        }

        [Test]
        public void TheServerPerformsADecodedRequestExactlyAsTheOriginal()
        {
            SynthesisResult run(bool viaWire)
            {
                var lab = new SynthLab();
                SynthesisRequest req = lab.Ready("WWAAVVV", 1f, "attack", "offense.explosion", SynthesisMode.Spell, 1, 60f, 99);
                if (viaWire)
                {
                    Assert.IsTrue(SynthesisRequestCodec.TryDecode(SynthesisRequestCodec.Encode(req), out SynthesisRequest back, out string why), why);
                    back.seed = 99;
                    req = back;
                }
                SynthesisResult r = lab.Execute(req);
                r.name += "|" + lab.Json();
                return r;
            }
            SynthesisResult a = run(false), b = run(true);
            Assert.AreEqual(a.name, b.name, "the wire loses nothing the engine reads");
            Assert.AreEqual(a.score, b.score);
        }

        [Test]
        public void TheLongestRequestFitsTheNetworkLimit()
        {
            var req = new SynthesisRequest { mode = SynthesisMode.Spell, intentionId = new string('i', 64), effectId = new string('e', 64), name = new string('n', 64) };
            for (int i = 0; i < RuneNetLimits.MaxSynthesisSlots; i++)
                req.slots.Add(new SynthesisSlot(new string('a', 64), new string('c', 64), 123456789.123f));
            string json = SynthesisRequestCodec.Encode(req);
            Assert.LessOrEqual(json.Length, RuneNetLimits.MaxSynthesisRequestText, "even slots with the longest ids fit");
            Assert.IsTrue(SynthesisRequestCodec.TryDecode(json, out _, out string why), why);

            var typical = new SynthLab().Ready(Pyro);
            Assert.Less(SynthesisRequestCodec.Encode(typical).Length, RuneNetLimits.MaxSynthesisRequestText / 2);
            Assert.AreEqual(RuneNetLimits.MaxSynthesisRequestText, RuneNetLimits.MaxTextFor(RuneRequestKind.Synthesize));
            Assert.AreEqual(RuneNetLimits.MaxRequestText, RuneNetLimits.MaxTextFor(RuneRequestKind.Release));
            Assert.AreEqual(RuneNetLimits.MaxRequestText, RuneNetLimits.MaxTextFor(RuneRequestKind.DismissSpell));
        }

        [Test]
        public void TheServerRefusesWhatItWouldNotTrust()
        {
            void Bad(string json, string what)
            {
                Assert.IsFalse(SynthesisRequestCodec.TryDecode(json, out SynthesisRequest r, out string why), what);
                Assert.IsNull(r, what);
                Assert.IsNotEmpty(why, what);
            }
            string slot = "{\"runeInstanceId\":\"r1\",\"conceptId\":\"wind.pressure\",\"energy\":10}";
            string ok = "{\"mode\":0,\"intentionId\":\"attack\",\"effectId\":\"offense.explosion\",\"slots\":[" + slot + "],\"seed\":5,\"name\":\"\"}";
            Assert.IsTrue(SynthesisRequestCodec.TryDecode(ok, out SynthesisRequest good, out _));
            Assert.AreEqual(0, good.seed);

            Bad(null, "null");
            Bad("", "empty");
            Bad("not json at all", "garbage");
            Bad("{\"mode\":0,\"slots\":[", "truncated");
            Bad(new string(' ', RuneNetLimits.MaxSynthesisRequestText + 1), "too long");
            Bad(ok.Replace("\"mode\":0", "\"mode\":9"), "an unknown mode");
            Bad("{\"mode\":0,\"intentionId\":\"a\",\"effectId\":\"b\",\"slots\":[],\"seed\":0,\"name\":\"\"}", "no runes");
            Bad(ok.Replace("\"energy\":10", "\"energy\":-5"), "negative energy");
            Bad(ok.Replace("\"energy\":10", "\"energy\":1e12"), "absurd energy");
            Bad(ok.Replace("\"runeInstanceId\":\"r1\"", "\"runeInstanceId\":\"\""), "a rune without an id");
            Bad(ok.Replace("\"runeInstanceId\":\"r1\"", "\"runeInstanceId\":\"" + new string('x', 65) + "\""), "an id that is too long");
            Bad(ok.Replace("\"conceptId\":\"wind.pressure\"", "\"conceptId\":\"a\\u0007b\""), "a control character");
            Bad(ok.Replace("\"name\":\"\"", "\"name\":\"" + new string('n', 65) + "\""), "a name that is too long");
            Bad(ok.Replace("\"intentionId\":\"attack\"", "\"intentionId\":\"" + new string('i', 65) + "\""), "an intention that is too long");

            var many = new System.Text.StringBuilder("{\"mode\":0,\"intentionId\":\"a\",\"effectId\":\"b\",\"slots\":[");
            for (int i = 0; i <= RuneNetLimits.MaxSynthesisSlots; i++) many.Append(i > 0 ? "," : "").Append(slot);
            many.Append("],\"seed\":0,\"name\":\"\"}");
            Bad(many.ToString(), "more runes than a request may carry");
        }

        [Test]
        public void TheSynthesisWindowReadsANamedInputActionAndNoKey()
        {
            var settings = new RuneMagicSettings();
            Assert.AreEqual("Synthesis", settings.synthesisAction);
            var names = new List<string>(settings.InputActionNames());
            CollectionAssert.Contains(names, "Synthesis", "the editor tool adds it to the Input Actions asset, without a key");
            CollectionAssert.Contains(names, "RuneSoul");
            CollectionAssert.AllItemsAreUnique(names);
            settings.synthesisAction = "  Lab ";
            CollectionAssert.Contains(new List<string>(settings.InputActionNames()), "Lab");
            settings.synthesisAction = "";
            CollectionAssert.DoesNotContain(new List<string>(settings.InputActionNames()), "");
            Assert.AreEqual(5, new List<string>(settings.InputActionNames()).Count, "Meditate, RuneSoul, the Spellbook (Phase 7), the Grimoire (the grimoire update) and the debug panel");
        }

        [Test]
        public void TheBalanceWarnsWhenMaxRunesExceedsWhatTheNetworkCarries()
        {
            RuneCatalog c = SynthLab.Custom(d => d.balance.synthesisEngine.maxRunes = RuneNetLimits.MaxSynthesisSlots + 1);
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            Assert.IsTrue(new List<string>(c.Warnings).Exists(w => w.Contains("network request")), string.Join("\n", c.Warnings));
            Assert.IsEmpty(RuneDefaults.Catalog().Warnings);
        }

        // ------------------------------------------------------------------ the report (step 9)
        [Test]
        public void TheReportOfAPerformedSynthesisHoldsWhatTheResultScreenShows()
        {
            var lab = new SynthLab();
            SynthesisResult ok = lab.Execute(lab.Ready(Pyro));
            SynthesisReport r = SynthesisReport.From(lab.c, ok, SynthesisMode.Spell, 7, 3);
            Assert.IsTrue(r.HasResult);
            Assert.AreEqual(3, r.seq);
            Assert.AreEqual(SynthesisTier.Perfect, r.Tier);
            Assert.AreEqual(SynthesisOutput.Spell, r.Output);
            Assert.AreEqual(SynthesisMode.Spell, r.Mode);
            Assert.AreEqual("Pyroclastic Resonance", r.name);
            Assert.AreEqual(100f, r.score, 0.01f);
            Assert.AreEqual(7, r.runeCount);
            Assert.AreEqual(1, r.rank);
            Assert.Greater(r.power, 0f);
            Assert.Greater(r.stability, 0f);
            Assert.Greater(r.energyConsumed, 0f);
            Assert.IsFalse(r.Failed);
            Assert.IsEmpty(r.flaws);
            StringAssert.StartsWith("Perfect", r.message);

            var lab2 = new SynthLab();
            SynthesisResult flawed = lab2.Execute(lab2.Ready("WWAAVVV"));
            SynthesisReport fr = SynthesisReport.From(lab2.c, flawed, SynthesisMode.Spell, 7, 1);
            Assert.AreEqual(SynthesisTier.Flawed, fr.Tier);
            Assert.GreaterOrEqual(fr.flaws.Count, 1);
            foreach (string f in fr.flaws) Assert.IsNull(lab2.c.Flaw(f), "flaws are shown by name, not by id");
            Assert.IsNotEmpty(fr.hint);

            var lab3 = new SynthLab();
            SynthesisResult failed = lab3.Execute(lab3.Ready("WWWWWWW"));
            SynthesisReport xr = SynthesisReport.From(lab3.c, failed, SynthesisMode.Spell, 7, 1);
            Assert.IsTrue(xr.Failed);
            Assert.IsTrue(xr.incoherent);
            Assert.IsNotEmpty(xr.severity);
            Assert.Greater(xr.energyLost, 0f);
            Assert.AreEqual(SynthesisOutput.None, xr.Output);
            Assert.IsNotEmpty(xr.hint, "the player learns what kind of problem it was");
        }

        [Test]
        public void ARefusedSynthesisHasNoReport()
        {
            var lab = new SynthLab();
            SynthesisResult refused = lab.Execute(lab.Ready(Pyro, 0.2f));
            SynthesisReport r = SynthesisReport.From(lab.c, refused, SynthesisMode.Spell, 7, 4);
            Assert.IsFalse(r.HasResult);
            Assert.AreEqual(4, r.seq);
            Assert.IsFalse(r.performed);
            Assert.IsFalse(SynthesisReport.From(lab.c, null, SynthesisMode.Spell, 0, 1).HasResult);
            Assert.IsFalse(new SynthesisReport().HasResult);
        }

        [Test]
        public void TheReportTravelsWithTheRuneListAndIsCopiedNotShared()
        {
            var lab = new SynthLab();
            SynthesisResult flawed = lab.Execute(lab.Ready("WWAAVVV"));
            var view = new RuneListView { version = 5 };
            foreach (RuneInstance r in lab.s.runes) view.runes.Add(r.Clone());
            view.report = SynthesisReport.From(lab.c, flawed, SynthesisMode.Spell, 7, 2);
            string json = UnityEngine.JsonUtility.ToJson(view);
            RuneListView back = UnityEngine.JsonUtility.FromJson<RuneListView>(json);
            Assert.AreEqual(2, back.report.seq);
            Assert.AreEqual(view.report.name, back.report.name);
            CollectionAssert.AreEqual(view.report.flaws, back.report.flaws);
            Assert.AreEqual(view.report.score, back.report.score, 1e-3f);

            SynthesisReport copy = view.report.Clone();
            copy.flaws.Add("extra");
            Assert.AreNotEqual(copy.flaws.Count, view.report.flaws.Count);
            Assert.IsFalse(new RuneListView().report.HasResult, "a list without a result has seq 0");
        }

        // ------------------------------------------------------------------ the client's preview is the server's
        private static void AssertSamePreview(SynthesisPreview a, SynthesisPreview b, string what)
        {
            Assert.AreEqual(a.refusal, b.refusal, what);
            Assert.AreEqual(a.message, b.message, what);
            Assert.AreEqual(a.mode, b.mode, what);
            Assert.AreEqual(a.output, b.output, what);
            Assert.AreEqual(a.resultRank, b.resultRank, what);
            Assert.AreEqual(a.estimateLow, b.estimateLow, what);
            Assert.AreEqual(a.estimateHigh, b.estimateHigh, what);
            Assert.AreEqual(a.tierLow, b.tierLow, what);
            Assert.AreEqual(a.tierHigh, b.tierHigh, what);
            Assert.AreEqual(a.riskLevel, b.riskLevel, what);
            Assert.AreEqual(a.destructionPossible, b.destructionPossible, what);
            Assert.AreEqual(a.soulCouldCollapse, b.soulCouldCollapse, what);
            Assert.AreEqual(a.failureChance, b.failureChance, 1e-6f, what);
            Assert.AreEqual(a.soulDamageLow, b.soulDamageLow, 1e-3f, what);
            Assert.AreEqual(a.soulDamageHigh, b.soulDamageHigh, 1e-3f, what);
            CollectionAssert.AreEqual(a.warnings, b.warnings, what);
            Assert.AreEqual(a.requirements.Count, b.requirements.Count, what);
            for (int i = 0; i < a.requirements.Count; i++)
            {
                Assert.AreEqual(a.requirements[i].minEnergy, b.requirements[i].minEnergy, what);
                Assert.AreEqual(a.requirements[i].available, b.requirements[i].available, 1e-3f, what);
                Assert.AreEqual(a.requirements[i].allocated, b.requirements[i].allocated, 1e-3f, what);
                Assert.AreEqual(a.requirements[i].shown, b.requirements[i].shown, what);
                Assert.AreEqual(a.requirements[i].runeName, b.requirements[i].runeName, what);
            }
        }

        [Test]
        public void AClientsPreviewIsExactlyTheServersPreview()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));                                              // a spell anchoring seven runes
            lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement)); // and a composite rune
            var requests = new Dictionary<string, SynthesisRequest>
            {
                ["ideal"] = lab.Ready(Pyro),
                ["insufficient"] = lab.Ready(Pyro, 0.3f),
                ["excessive"] = lab.Ready(Pyro, 3f),
                ["counts"] = lab.Ready("WWAAVVV"),
                ["incoherent"] = lab.Ready("WWWWWWW"),
                ["reversed"] = lab.Ready("VVVAWWW"),
                ["wrong purpose"] = lab.Ready(Pyro, 1f, "defense", "offense.explosion"),
                ["advancement"] = lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement),
                ["composite"] = lab.Ready("WAV", 1f, "attack", "offense.explosion", SynthesisMode.CompositeRune),
            };
            var anchored = new SynthesisRequest { mode = SynthesisMode.CompositeRune, intentionId = "attack", effectId = "offense.explosion" };
            anchored.slots.Add(new SynthesisSlot(lab.s.spells[0].anchors[0], "wind.pressure", 100f));
            anchored.slots.Add(new SynthesisSlot(lab.s.spells[0].anchors[3], "ash.combustion", 100f));
            requests["anchored"] = anchored;
            var ghost = lab.Ready("WAV");
            ghost.slots[0].runeInstanceId = "ghost";
            requests["unknown rune"] = ghost;

            // What the server sends the owner, and what the client builds from it.
            var view = new RuneListView { version = 9 };
            foreach (RuneInstance r in lab.s.runes) view.runes.Add(r.Clone());
            foreach (ConceptKnowledge k in lab.s.knowledge) view.knowledge.Add(new ConceptKnowledge { runeId = k.runeId, concepts = new List<string>(k.concepts) });
            foreach (SpellRecord sp in lab.s.spells) view.spells.Add(sp.Clone());
            RuneListView received = UnityEngine.JsonUtility.FromJson<RuneListView>(UnityEngine.JsonUtility.ToJson(view));
            RuneSoulState replica = SynthesisReplica.StateFrom(received);

            foreach (KeyValuePair<string, SynthesisRequest> kv in requests)
            {
                SynthesisPreview server = SynthesisEngine.Preview(lab.c, lab.s, lab.ctx, kv.Value);
                SynthesisPreview client = SynthesisEngine.Preview(lab.c, replica, lab.ctx, kv.Value);
                AssertSamePreview(server, client, kv.Key);
            }
        }

        [Test]
        public void ThePreviewAnnouncesWhatTheServerThenDoes()
        {
            // No chance of faltering, so that what the preview estimates is what happens.
            RuneCatalog steady = SynthLab.Custom(d =>
            {
                d.balance.synthesisEngine.riskFailureChance = 0f;
                d.balance.synthesisEngine.interferenceFailureChance = 0f;
                d.balance.synthesisEngine.previewFuzz = 12f;
            });
            var cases = new List<Func<SynthLab, SynthesisRequest>>
            {
                lab => lab.Ready(Pyro),
                lab => lab.Ready(Pyro, 0.6f),
                lab => lab.Ready(Pyro, 1.4f),
                lab => lab.Ready(Pyro, 3f),
                lab => lab.Ready("WWAAVVV"),
                lab => lab.Ready("WWWAAVVV"),
                lab => lab.Ready("WWWVVVA"),
                lab => lab.Ready("WVWAVWV"),
                lab => lab.Ready("WWWWWWW"),
                lab => lab.Ready("VVVAWWW"),
                lab => lab.Ready(Pyro, 1f, "defense", "offense.explosion"),
                lab => lab.Ready(Pyro, 0.3f),
                lab => lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement),
                lab => lab.Ready("WAV", 1f, "attack", "offense.explosion", SynthesisMode.CompositeRune),
                lab => lab.Ready("WAV"),
            };
            int i = 0;
            foreach (Func<SynthLab, SynthesisRequest> make in cases)
            {
                var lab = new SynthLab(steady);
                SynthesisRequest req = make(lab);
                string what = "case " + i++;
                SynthesisPreview p = lab.Preview(req);
                SynthesisResult r = lab.Execute(req);
                if (!p.CanAttempt)
                {
                    Assert.IsFalse(r.performed, what);
                    Assert.AreEqual(p.refusal, r.refusal, what + ": the same refusal");
                    continue;
                }
                Assert.IsTrue(r.performed, what + ": " + r.message);
                Assert.IsFalse(r.faltered, what);
                if (r.incoherent)
                {
                    Assert.AreEqual(SynthesisTier.CompleteFailure, p.tierHigh, what + ": a doomed formula is estimated to fail");
                    Assert.AreEqual(SynthesisTier.CompleteFailure, r.tier, what);
                }
                else
                {
                    Assert.GreaterOrEqual(r.score, p.estimateLow - 1f, what + ": the estimate holds the score");
                    Assert.LessOrEqual(r.score, p.estimateHigh + 1f, what);
                    Assert.GreaterOrEqual((int)r.tier, (int)p.tierLow, what);
                    Assert.LessOrEqual((int)r.tier, (int)p.tierHigh, what);
                }
                for (int k = 0; k < p.requirements.Count; k++)
                    Assert.LessOrEqual(p.requirements[k].minEnergy - 1f, req.slots[k].energy + 1e-3f, what + ": a performed synthesis met every minimum (shown rounded up)");
                if (r.runesDestroyed) Assert.IsTrue(p.destructionPossible, what + ": destruction was warned of");
                float taken = 0f;
                foreach (SoulDamageEntry d in r.soulDamage) taken += d.amount;
                Assert.LessOrEqual(taken, p.soulDamageHigh + 1f, what + ": the damage is within the announced range");
            }
        }
    }
}
#endif
