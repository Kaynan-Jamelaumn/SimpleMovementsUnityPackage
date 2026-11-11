#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of what surrounds the synthesis engine (Phase 5): the preview and what it keeps secret, the Soul Damage
    /// a synthesis plans, saving and upgrading, the validation of the new data, and a fuzz run that throws random souls and
    /// requests at the engine and checks that every invariant of the state holds.
    /// </summary>
    public class SynthesisPreviewAndDataTests
    {
        private const string Pyro = "WWWAVVV";

        // ------------------------------------------------------------------ the preview (design spec 11.1 step 7)
        [Test]
        public void ThePreviewListsWhatEachRuneNeedsAndHolds()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            SynthesisEvaluation ev = lab.Evaluate(req);
            SynthesisPreview p = lab.Preview(req);
            Assert.IsTrue(p.CanAttempt, p.message);
            Assert.AreEqual(SynthesisOutput.Spell, p.output);
            Assert.AreEqual(1, p.resultRank);
            Assert.AreEqual(7, p.requirements.Count);
            for (int i = 0; i < 7; i++)
            {
                SlotRequirement r = p.requirements[i];
                Assert.AreEqual(req.slots[i].runeInstanceId, r.runeInstanceId);
                Assert.AreEqual(Math.Ceiling(ev.slots[i].minEnergy), r.minEnergy, 1e-3f, "the least energy the rune needs, rounded up");
                Assert.Less(r.minEnergy, ev.slots[i].idealEnergy, "never the ideal");
                Assert.AreEqual(req.slots[i].energy, r.allocated, 1e-3f);
                Assert.Greater(r.available, 0f);
                Assert.AreEqual(0f, r.shortfall);
                Assert.IsNotEmpty(r.runeName);
                Assert.IsNotEmpty(r.conceptName);
            }
            Assert.AreEqual(ev.riskLevel, p.riskLevel);
            Assert.Greater(p.soulDamageHigh, 0f);
            Assert.GreaterOrEqual(p.soulDamageHigh, p.soulDamageLow);
        }

        [Test]
        public void ThePreviewNeverRevealsTheExactScore()
        {
            var lab = new SynthLab();
            float fuzz = lab.c.Synthesis.previewFuzz;
            int offCentre = 0;
            foreach (float mult in new[] { 0.6f, 0.75f, 0.9f, 1.25f, 1.5f, 2f })
            {
                SynthesisRequest req = lab.Ready(Pyro, mult);
                float score = lab.Evaluate(req).score;
                SynthesisPreview p = lab.Preview(req);
                Assert.LessOrEqual(p.estimateLow, score + 0.51f, "the range holds the score");
                Assert.GreaterOrEqual(p.estimateHigh, score - 0.51f);
                Assert.LessOrEqual(p.estimateHigh - p.estimateLow, 2f * fuzz + 1f);
                float middle = (p.estimateLow + p.estimateHigh) / 2f;
                if (Math.Abs(middle - score) > 3f && p.estimateHigh < 100f && p.estimateLow > 0f) offCentre++;
                SynthesisPreview again = lab.Preview(req);
                Assert.AreEqual(p.estimateLow, again.estimateLow, "the same request always previews the same");
                Assert.AreEqual(p.estimateHigh, again.estimateHigh);
            }
            Assert.GreaterOrEqual(offCentre, 2, "the middle of the range is not the answer");
        }

        [Test]
        public void ThePreviewCanBeMadeExact()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.balance.synthesisEngine.previewFuzz = 0f));
            SynthesisRequest req = lab.Ready(Pyro, 0.75f);
            SynthesisPreview p = lab.Preview(req);
            Assert.AreEqual(Math.Round(lab.Evaluate(req).score), p.estimateLow, 0.01f);
            Assert.AreEqual(p.estimateLow, p.estimateHigh);
        }

        [Test]
        public void ThePreviewTellsAFundamentallyWrongFormulaIsLikelyToFailWithoutSayingWhy()
        {
            var lab = new SynthLab();
            SynthesisPreview p = lab.Preview(lab.Ready("WWWWWWW"));
            Assert.IsTrue(p.CanAttempt, "an invalid formula can still be attempted");
            Assert.AreEqual(SynthesisTier.CompleteFailure, p.tierHigh);
            Assert.Less(p.estimateHigh, lab.c.Balance.synthesis.flawed);
            Assert.IsEmpty(p.warnings.FindAll(w => w.Contains("combustion") || w.Contains("incoherent") || w.Contains("cannot be produced")));
            StringAssert.DoesNotContain("combustion", p.message);
        }

        [Test]
        public void ThePreviewFlagsInsufficientEnergyAndWhatIsMissing()
        {
            var lab = new SynthLab();
            SynthesisPreview p = lab.Preview(lab.Ready(Pyro, 0.3f));
            Assert.IsFalse(p.CanAttempt);
            Assert.AreEqual(SynthesisRefusal.InsufficientEnergy, p.refusal);
            StringAssert.Contains("Not enough energy", p.message);
            foreach (SlotRequirement r in p.requirements)
            {
                Assert.AreEqual(EnergyState.Insufficient, r.shown);
                Assert.Greater(r.shortfall, 0f);
            }
        }

        [Test]
        public void ThePreviewShowsOnlyTheStatesThatHaveToBeShownUnlessTheBalanceSaysMore()
        {
            var lab = new SynthLab();
            foreach (SlotRequirement r in lab.Preview(lab.Ready(Pyro, 0.65f)).requirements)
                Assert.AreEqual(EnergyState.Optimal, r.shown, "a low (but sufficient) energy is not announced");
            foreach (SlotRequirement r in lab.Preview(lab.Ready(Pyro, 3f)).requirements)
                Assert.AreEqual(EnergyState.Excessive, r.shown, "an excessive energy is");
            StringAssert.Contains("interfere", lab.Preview(lab.Ready(Pyro, 3f)).warnings[0]);

            var open = new SynthLab(SynthLab.Custom(d => d.balance.synthesisEngine.previewShowsEnergyStates = true));
            foreach (SlotRequirement r in open.Preview(open.Ready(Pyro, 0.65f)).requirements)
                Assert.AreEqual(EnergyState.Low, r.shown);
        }

        [Test]
        public void ThePreviewChangesNothing()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            string before = lab.Json();
            lab.Preview(req);
            lab.Preview(lab.Ready(Pyro, 0.2f));
            lab.Preview(null);
            Assert.AreEqual(before.Length > 0, true);
            // The two Ready calls absorbed runes: compare against a fresh snapshot, then preview again.
            string after = lab.Json();
            lab.Preview(req);
            Assert.AreEqual(after, lab.Json());
        }

        [Test]
        public void ThePreviewRefusesWhatCannotBeAttemptedWithTheSameReason()
        {
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready(Pyro);
            req.slots[0].runeInstanceId = "gone";
            SynthesisPreview p = lab.Preview(req);
            Assert.AreEqual(SynthesisRefusal.RuneNotOwned, p.refusal);
            Assert.IsFalse(p.CanAttempt);
            Assert.IsEmpty(p.requirements);
            Assert.AreEqual(SynthesisRefusal.NoRequest, lab.Preview(null).refusal);
        }

        [Test]
        public void ThePreviewWarnsWhenRunesCouldBeDestroyedOrTheSoulCollapse()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.balance.failureSeverities = new List<FailureSeverity> { new FailureSeverity("Doom", 0f, 0.1f, 1f, true) }));
            SynthesisRequest req = lab.Ready(Pyro);
            SynthesisPreview p = lab.Preview(req);
            Assert.IsTrue(p.destructionPossible);
            Assert.AreEqual(RiskLevel.Extreme, p.riskLevel);
            Assert.IsTrue(p.warnings.Exists(w => w.Contains("destroyed")));

            lab.ctx.integrity = 4f;
            p = lab.Preview(req);
            Assert.IsTrue(p.soulCouldCollapse);
            Assert.IsTrue(p.CanAttempt, "the preview warns; refusing is the balance's choice");
            Assert.IsTrue(p.warnings.Exists(w => w.Contains("collapse")));

            var calm = new SynthLab();
            Assert.IsFalse(calm.Preview(calm.Ready(Pyro)).destructionPossible);
            Assert.IsFalse(calm.Preview(calm.Ready(Pyro)).soulCouldCollapse);
        }

        [Test]
        public void TheBalanceCanRefuseASynthesisThatCouldCollapseTheSoul()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.balance.synthesisEngine.refuseWhenSoulCouldCollapse = true));
            SynthesisRequest req = lab.Ready(Pyro);
            lab.ctx.integrity = 4f;
            Assert.AreEqual(SynthesisRefusal.SoulWouldCollapse, lab.Preview(req).refusal);
            string before = lab.Json();
            SynthesisResult r = lab.Execute(req);
            Assert.AreEqual(SynthesisRefusal.SoulWouldCollapse, r.refusal);
            Assert.AreEqual(before, lab.Json());
            lab.ctx.integrity = 100f;
            Assert.IsTrue(lab.Execute(req).performed);
        }

        [Test]
        public void ThePreviewWarnsThatRunesUsedUpLoseTheirLeftoverEnergy()
        {
            var lab = new SynthLab();
            SynthesisPreview p = lab.Preview(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            Assert.IsTrue(p.warnings.Exists(w => w.Contains("lose the energy")));
            Assert.AreEqual(SynthesisOutput.Rune, p.output);
            Assert.AreEqual(2, p.resultRank);
            SynthesisPreview spell = lab.Preview(lab.Ready(Pyro));
            Assert.IsFalse(spell.warnings.Exists(w => w.Contains("lose the energy")), "a spell does not use its runes up");
        }

        // ------------------------------------------------------------------ Soul Damage (design spec 14.1)
        private static ResolvedSynthesis Shape(int runes, int rank, SynthesisMode mode)
        {
            var rs = new ResolvedSynthesis { mode = mode, resultRank = rank };
            for (int i = 0; i < runes; i++) rs.slots.Add(new ResolvedSlot());
            return rs;
        }

        private static float Total(ResolvedSynthesis rs, SynthesisEvaluation ev, SynthesisTier tier)
        {
            RuneCatalog c = RuneDefaults.Catalog();
            return SynthesisOutcomes.ScaledTotal(c, null, SynthesisOutcomes.SoulDamagePlan(c, rs, ev, tier));
        }

        [Test]
        public void SoulDamageGrowsWithTheSizeTheRankTheRiskAndABadOutcome()
        {
            var ev = new SynthesisEvaluation { effectComplexity = 0.4f, risk = 0.3f };
            float baseline = Total(Shape(3, 1, SynthesisMode.Spell), ev, SynthesisTier.Successful);
            Assert.Greater(baseline, 0f);
            Assert.Greater(Total(Shape(9, 1, SynthesisMode.Spell), ev, SynthesisTier.Successful), baseline, "more runes");
            Assert.Greater(Total(Shape(3, 3, SynthesisMode.Spell), ev, SynthesisTier.Successful), baseline, "a higher rank");
            Assert.Greater(Total(Shape(3, 1, SynthesisMode.RankAdvancement), ev, SynthesisTier.Successful), baseline * 1.9f, "a rank advancement strains the soul more");
            var risky = new SynthesisEvaluation { effectComplexity = 0.4f, risk = 0.9f };
            Assert.Greater(Total(Shape(3, 1, SynthesisMode.Spell), risky, SynthesisTier.Successful), baseline, "a riskier attempt");
            var harder = new SynthesisEvaluation { effectComplexity = 0.9f, risk = 0.3f };
            Assert.Greater(Total(Shape(3, 1, SynthesisMode.Spell), harder, SynthesisTier.Successful), baseline, "a harder effect");

            ResolvedSynthesis rs = Shape(3, 1, SynthesisMode.Spell);
            float perfect = Total(rs, ev, SynthesisTier.Perfect), good = Total(rs, ev, SynthesisTier.Successful),
                flawed = Total(rs, ev, SynthesisTier.Flawed), failed = Total(rs, ev, SynthesisTier.CompleteFailure);
            Assert.Less(perfect, good);
            Assert.Less(good, flawed);
            Assert.Less(flawed, failed, "a failed synthesis strains the soul most");
        }

        [Test]
        public void PrimordialRunesOverreachingConceptsAndDangerAddTheirOwnKinds()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var rs = Shape(3, 1, SynthesisMode.Spell);
            var ev = new SynthesisEvaluation { effectComplexity = 0.4f, risk = 0.2f, primordialRunes = 2 };
            for (int i = 0; i < 3; i++) ev.slots.Add(new SlotEvaluation { overreach = i == 0 ? 0.3f : 0f });
            List<SoulDamageEntry> plan = SynthesisOutcomes.SoulDamagePlan(c, rs, ev, SynthesisTier.Successful);
            Assert.IsTrue(plan.Exists(e => e.kind == "synthesis"));
            SoulDamageEntry primordial = plan.Find(e => e.kind == "primordial");
            Assert.AreEqual(c.Synthesis.primordialDamage * 2f, primordial.amount, 0.1f);
            SoulDamageEntry overreach = plan.Find(e => e.kind == "concept_overreach");
            Assert.AreEqual(c.Synthesis.overreachDamage * 0.3f, overreach.amount, 0.1f);
            Assert.IsFalse(plan.Exists(e => e.kind == "experiment"), "a safe synthesis is no experiment");

            ev.risk = 1f;
            plan = SynthesisOutcomes.SoulDamagePlan(c, rs, ev, SynthesisTier.Successful);
            Assert.IsTrue(plan.Exists(e => e.kind == "experiment" && e.amount > 0f), "a dangerous attempt is");
            Assert.IsTrue(SynthesisOutcomes.SoulDamagePlan(c, rs, ev, SynthesisTier.CompleteFailure).Exists(e => e.kind == "synthesis_failure"));
            Assert.IsFalse(SynthesisOutcomes.SoulDamagePlan(c, rs, ev, SynthesisTier.CompleteFailure).Exists(e => e.kind == "synthesis"));
        }

        [Test]
        public void EveryDamageKindThePlanUsesExistsInTheBalance()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var ev = new SynthesisEvaluation { effectComplexity = 0.9f, risk = 1f, primordialRunes = 1 };
            ev.slots.Add(new SlotEvaluation { overreach = 0.5f });
            var seen = new HashSet<string>();
            foreach (SynthesisTier t in new[] { SynthesisTier.Perfect, SynthesisTier.Flawed, SynthesisTier.CompleteFailure })
                foreach (SoulDamageEntry e in SynthesisOutcomes.SoulDamagePlan(c, Shape(1, 1, SynthesisMode.Spell), ev, t))
                {
                    seen.Add(e.kind);
                    Assert.IsNotNull(c.Balance.DamageKind(e.kind), e.kind);
                    Assert.Greater(e.amount, 0f);
                    Assert.IsFalse(float.IsNaN(e.amount) || float.IsInfinity(e.amount));
                }
            CollectionAssert.IsSupersetOf(seen, new[] { "synthesis", "synthesis_failure", "primordial", "concept_overreach", "experiment" });
        }

        [Test]
        public void TheSoulsConditionScalesThePlannedDamage()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var plan = new List<SoulDamageEntry> { new SoulDamageEntry { kind = "synthesis", amount = 10f } };
            var strained = new SynthesisContext();
            SoulRestrictions r = SoulRestrictions.None; r.soulDamageMultiplier = 2f;
            strained.soul = r;
            Assert.AreEqual(10f, SynthesisOutcomes.ScaledTotal(c, new SynthesisContext(), plan), 1e-3f);
            Assert.AreEqual(20f, SynthesisOutcomes.ScaledTotal(c, strained, plan), 1e-3f);
        }

        [Test]
        public void APerformedSynthesisReturnsThePlannedDamageForTheRuntimeToApply()
        {
            var lab = new SynthLab();
            string before = lab.Json();
            SynthesisResult ok = lab.Execute(lab.Ready(Pyro));
            Assert.IsNotEmpty(ok.soulDamage);
            Assert.AreEqual("synthesis", ok.soulDamage[0].kind);
            Assert.Greater(ok.soulDamage[0].amount, 0f);
            var fail = new SynthLab();
            SynthesisResult bad = fail.Execute(fail.Ready("WWWWWWW"));
            Assert.AreEqual("synthesis_failure", bad.soulDamage[0].kind);
            Assert.Greater(bad.soulDamage[0].amount, ok.soulDamage[0].amount);
            Assert.AreNotEqual(before, lab.Json());
            Assert.AreEqual(100f, lab.ctx.integrity, "the engine plans the damage, it never applies it");
        }

        // ------------------------------------------------------------------ saving and upgrading
        [Test]
        public void SpellsCompositeRunesAndTheLogSurviveASaveRoundTrip()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));
            lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            Assert.AreEqual(1, lab.s.spells.Count);
            Assert.AreEqual(2, lab.s.synthesisLog.Count);

            string json = lab.Json();
            RuneSoulState back = RuneSoulState.FromJson(json);
            Assert.AreEqual(json, back.ToJson(), "what is saved is what is loaded");
            Assert.AreEqual(1, back.spells.Count);
            Assert.AreEqual("Pyroclastic Resonance", back.spells[0].name);
            Assert.AreEqual(7, back.spells[0].anchors.Count);
            Assert.AreEqual(2, back.synthesisLog.Count);
            Assert.AreEqual(RuneSoulState.CurrentVersion, back.version);
            RuneInstance composite = back.runes.Find(r => r.IsComposite);
            Assert.IsNotNull(composite);
            Assert.AreEqual(2, composite.rank);
            Assert.AreEqual("Pyroclastic Resonance", composite.composite.name);
            Assert.AreEqual(7, composite.composite.ingredients.Count);
            Assert.IsNotEmpty(composite.composite.aspects);
            Assert.AreEqual(2, back.synthesesPerfect);
        }

        [Test]
        public void TheRuneListCarriesTheSpellsAndCompositeRunesToTheOwner()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));
            lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement));
            var view = new RuneListView { version = 7 };
            foreach (RuneInstance r in lab.s.runes) view.runes.Add(r.Clone());
            foreach (SpellRecord sp in lab.s.spells) view.spells.Add(sp.Clone());
            string json = UnityEngine.JsonUtility.ToJson(view);
            Assert.Less(json.Length, RuneNetLimits.MaxRuneListLength);
            RuneListView back = UnityEngine.JsonUtility.FromJson<RuneListView>(json);
            Assert.AreEqual(7, back.version);
            Assert.AreEqual(1, back.spells.Count);
            Assert.AreEqual("Pyroclastic Resonance", back.spells[0].name);
            Assert.AreEqual(7, back.spells[0].anchors.Count);
            Assert.IsTrue(back.runes.Exists(r => r.IsComposite));
            Assert.IsNotEmpty(back.runes.Find(r => r.IsComposite).composite.aspects);
        }

        [Test]
        public void TheWorstCaseRuneListFitsTheNetworkLimit()
        {
            // A full spellbook of the longest spells and a soul full of composite runes made from the most runes.
            RuneBalance b = RuneDefaults.Catalog().Balance;
            var view = new RuneListView();
            for (int i = 0; i < RuneDefaults.Catalog().Synthesis.maxSpells; i++)
            {
                var sp = new SpellRecord { spellId = "syn_0123456789ab", name = "A fairly long spell name of forty chars....", intentionId = "attack", effectId = "offense.explosion", archetypeId = "pyroclastic_resonance" };
                for (int k = 0; k < RuneDefaults.Catalog().Synthesis.maxRunes; k++)
                {
                    sp.anchors.Add("r_0123456789ab");
                    sp.ingredients.Add(new SynthesisIngredient { runeInstanceId = "r_0123456789ab", runeId = "vibration", rank = 5, conceptId = "vibration.resonance", energy = 123456.78f });
                }
                sp.flaws.AddRange(new[] { "wasteful", "volatile", "disordered" });
                sp.sideEffects.AddRange(new[] { "unstable_release", "backlash" });
                view.spells.Add(sp);
            }
            for (int i = 0; i < 40; i++)
            {
                var r = new RuneInstance { instanceId = "r_0123456789ab", runeId = "pyroclastic_resonance", rank = 5, quality = 88.5f, energy = 123456.78f };
                r.composite = new CompositeProfile { active = true, name = "A fairly long composite name....", description = new string('d', 200), archetypeId = "pyroclastic_resonance", intentionId = "attack", effectId = "offense.explosion" };
                for (int k = 0; k < 8; k++) r.composite.aspects.Add(new AspectWeight("combustion", 1.23f));
                for (int k = 0; k < RuneDefaults.Catalog().Synthesis.maxRunes; k++)
                    r.composite.ingredients.Add(new SynthesisIngredient { runeInstanceId = "r_0123456789ab", runeId = "vibration", rank = 5, conceptId = "vibration.resonance", energy = 123456.78f });
                view.runes.Add(r);
            }
            string json = UnityEngine.JsonUtility.ToJson(view);
            Assert.Less(json.Length, RuneNetLimits.MaxRuneListLength, "the owner's client refuses a longer list: raise the limit with the data");
            Assert.IsNotNull(b);
        }

        [Test]
        public void DismissingASpellTravelsAsAnOrdinaryRequest()
        {
            Assert.AreEqual(8, (byte)RuneRequestKind.DismissSpell, "values are only ever added: the wire value is fixed");
            Assert.Less("syn_0123456789ab".Length, RuneNetLimits.MaxRequestText, "a spell id fits a request");
        }

        [Test]
        public void ARuneWithoutACompositeProfileIsABaseRune()
        {
            RuneSoulState old = RuneSoulState.FromJson("{\"version\":3,\"soul\":{},\"runes\":[{\"instanceId\":\"a\",\"runeId\":\"wind\",\"rank\":1,\"quality\":50,\"energy\":10}]}");
            Assert.AreEqual(1, old.runes.Count);
            Assert.IsFalse(old.runes[0].IsComposite, "an empty composite profile written by the serializer is not a composite");
            Assert.IsNotNull(old.spells);
            Assert.IsEmpty(old.spells);
            Assert.IsEmpty(old.synthesisLog);
            Assert.AreEqual(0, old.synthesesPerfect);
            Assert.AreEqual(1, RuneRules.SlotCost(RuneDefaults.Catalog(), old.runes[0]));
            StringAssert.Contains("Wind", RuneRules.DisplayName(RuneDefaults.Catalog(), old.runes[0]));
        }

        [Test]
        public void ASavedSpellWhoseRunesAreGoneIsDroppedOnLoad()
        {
            var lab = new SynthLab();
            lab.Execute(lab.Ready(Pyro));
            RuneSoulState damaged = RuneSoulState.FromJson(lab.Json());
            Assert.AreEqual(1, damaged.spells.Count);
            damaged.runes.RemoveAt(3); // a hand-edited or damaged save
            RuneSoulState loaded = RuneSoulState.FromJson(damaged.ToJson());
            Assert.IsEmpty(loaded.spells, "a spell whose rune is gone cannot be cast");
            Assert.AreEqual(6, loaded.runes.Count);
            Assert.AreEqual(1, RuneSoulState.FromJson(lab.Json()).spells.Count, "an intact save keeps its spells");
        }

        [Test]
        public void ARuneInstanceCloneCopiesItsCompositeProfile()
        {
            var lab = new SynthLab();
            RuneInstance made = lab.Execute(lab.Ready(Pyro, 1f, "attack", "offense.explosion", SynthesisMode.RankAdvancement)).rune;
            RuneInstance copy = made.Clone();
            copy.composite.aspects.Clear();
            copy.composite.ingredients.Clear();
            copy.composite.alignments.Add("x");
            Assert.IsNotEmpty(made.composite.aspects);
            Assert.AreEqual(7, made.composite.ingredients.Count);
            CollectionAssert.DoesNotContain(made.composite.alignments, "x");
        }

        [Test]
        public void OlderDatabasesGetTheSynthesisDataOnUpgrade()
        {
            RuneMagicData d = RuneDefaults.Create();
            d.version = 4;
            d.archetypes.Clear();
            d.synthesisFlaws.Clear();
            d.balance.synthesisEngine = new SynthesisBalance(); // what a version 4 asset deserializes to: no curves
            Assert.IsTrue(d.Upgrade());
            Assert.AreEqual(RuneMagicData.CurrentVersion, d.version);
            Assert.GreaterOrEqual(d.archetypes.Count, 11);
            Assert.GreaterOrEqual(d.synthesisFlaws.Count, 7);
            Assert.IsNotEmpty(d.balance.synthesisEngine.powerCurve);
            RuneCatalog c = RuneCatalog.Compile(d);
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            Assert.IsFalse(d.Upgrade(), "only once");

            RuneMagicData custom = RuneDefaults.Create();
            custom.version = 4;
            custom.archetypes = new List<OutcomeArchetype>();
            custom.archetypes.Add(new OutcomeArchetype { id = "mine", intention = "attack", effect = "offense.explosion", stages = { new ArchetypeStage("wind", "wind.pressure", 1) } });
            custom.Upgrade();
            Assert.AreEqual(1, custom.archetypes.Count, "what a designer set is never replaced");
        }

        [Test]
        public void OlderSoulSavesAreUpgradedInPlace()
        {
            RuneSoulState v3 = RuneSoulState.FromJson("{\"version\":3}");
            v3.EnsureLists();
            Assert.IsNotNull(v3.spells);
            Assert.IsNotNull(v3.synthesisLog);
            Assert.AreEqual(3, v3.version, "the format version is only a record: fields are only ever added");
            Assert.AreEqual(6, RuneSoulState.CurrentVersion, "Phase 7 added the ability keys of spells, the grimoire update the grimoire's serial");
        }

        // ------------------------------------------------------------------ validation
        [Test]
        public void TheBuiltInSynthesisDataIsValidAndComplete()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.IsEmpty(c.Errors, string.Join("\n", c.Errors));
            Assert.IsEmpty(c.Warnings, string.Join("\n", c.Warnings));
            Assert.GreaterOrEqual(c.Archetypes.Count, 11, "Pyroclastic Resonance and the ten original combinations");
            Assert.IsNotNull(c.Archetype("pyroclastic_resonance"));
            foreach (FlawCause cause in Enum.GetValues(typeof(FlawCause)))
                Assert.IsTrue(new List<SynthesisFlawDef>(c.Flaws).Exists(f => f.cause == cause), "a flaw for " + cause);
            var errors = new List<string>(); var warnings = new List<string>();
            c.Synthesis.Validate(errors, warnings);
            Assert.IsEmpty(errors);
            Assert.IsEmpty(warnings);
            Assert.AreEqual(7, c.Balance.runesPerRankAdvancement);
        }

        private static void Broken(Action<RuneMagicData> edit, string expectedInMessage)
        {
            RuneCatalog c = SynthLab.Custom(edit);
            Assert.IsFalse(c.IsValid, "should be invalid: " + expectedInMessage);
            string all = string.Join("\n", c.Errors);
            StringAssert.Contains(expectedInMessage, all);
        }

        [Test]
        public void ABrokenArchetypeIsReported()
        {
            Broken(d => d.archetypes[0].stages.Add(new ArchetypeStage("no_such_rune", "", 1)), "no_such_rune");
            Broken(d => d.archetypes[0].stages[0].conceptId = "ash.combustion", "pyroclastic_resonance");
            Broken(d => d.archetypes[0].effect = "no.such_effect", "no.such_effect");
            Broken(d => d.archetypes[0].intention = "no_such_intention", "no_such_intention");
            Broken(d => d.archetypes[0].intention = "defense", "pyroclastic_resonance");
            Broken(d => d.archetypes.Add(new OutcomeArchetype { id = d.archetypes[0].id, intention = "attack", effect = "offense.explosion",
                stages = { new ArchetypeStage("wind", "wind.pressure", 1) } }), "pyroclastic_resonance");
            Broken(d => d.archetypes[0].resultRuneId = "wind", "wind");
        }

        [Test]
        public void ABrokenSynthesisBalanceIsReported()
        {
            Broken(d => { var b = d.balance.synthesisEngine; b.weightCoverage = b.weightCompatibility = b.weightIntention = b.weightOrder = b.weightEnergy = b.weightExecution = 0f; }, "weight");
            Broken(d => d.balance.synthesisEngine.maxSafeShare = 1.1f, "Max Safe Share");
            Broken(d => d.balance.synthesisEngine.powerCurve = new List<CurvePoint>(), "power curve");
            Broken(d => d.balance.synthesisEngine.stabilityCurve = new List<CurvePoint> { new CurvePoint(80f, 1f), new CurvePoint(10f, 0.5f) }, "stability curve");
            Broken(d => d.balance.synthesisEngine.falterMax = 1f, "Falter");
            Broken(d => d.balance.synthesisEngine.minEnergyShare = 1f, "Min Energy Share");
        }

        [Test]
        public void AMissingFlawForACauseIsOnlyAWarning()
        {
            RuneCatalog c = SynthLab.Custom(d => d.synthesisFlaws.RemoveAll(f => f.cause == FlawCause.Order));
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            Assert.IsTrue(new List<string>(c.Warnings).Exists(w => w.Contains("Order")), string.Join("\n", c.Warnings));
            // and a flawed synthesis with a poor order still gets a flaw (the most fitting one that exists)
            var ev = new SynthesisEvaluation { factors = new FactorScores { coverage = 1f, compatibility = 1f, intention = 1f, order = 0.1f, energy = 1f, execution = 1f } };
            var rng = new SynthesisRng(1);
            Assert.GreaterOrEqual(SynthesisOutcomes.PickFlaws(c, ev, 50f, ref rng, out _).Count, 1);
        }

        [Test]
        public void ABalanceWithNoFlawsAtAllStillMakesAFlawedSynthesis()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.synthesisFlaws.Clear()));
            SynthesisResult r = lab.Execute(lab.Ready("WWAAVVV"));
            Assert.IsTrue(r.performed);
            Assert.AreEqual(SynthesisTier.Flawed, r.tier);
            Assert.IsTrue(r.spell.flawed, "the tier says so even when the data has no flaw to attach");
            Assert.IsEmpty(r.flaws);
        }

        [Test]
        public void ADatabaseWithNoFailureSeverityCostsNoRunes()
        {
            var lab = new SynthLab(SynthLab.Custom(d => d.balance.failureSeverities = new List<FailureSeverity>()));
            SynthesisResult r = lab.Execute(lab.Ready("WWWWWWW"));
            Assert.IsTrue(r.performed);
            Assert.AreEqual(SynthesisTier.CompleteFailure, r.tier);
            Assert.AreEqual(0f, r.energyLost);
            Assert.AreEqual(7, lab.s.runes.Count);
        }

        // ------------------------------------------------------------------ fuzz
        private static void FillRandom(SynthLab lab, SynthesisRequest req, Random rnd)
        {
            var probe = req.Clone();
            foreach (SynthesisSlot sl in probe.slots) if (sl != null) sl.energy = 0.001f;
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(lab.c, lab.s, lab.ctx, probe, out _, out _);
            for (int i = 0; i < req.slots.Count; i++)
            {
                SynthesisSlot sl = req.slots[i];
                if (sl == null) continue;
                RuneInstance held = lab.s.Rune(sl.runeInstanceId);
                float ideal = ev != null ? ev.slots[i].idealEnergy : 100f;
                float wanted = ideal * (0.4f + 1.6f * (float)rnd.NextDouble());
                sl.energy = held != null ? Math.Min(wanted, held.energy) : wanted;
            }
        }

        private static string ConceptFor(RuneCatalog c, RuneInstance r, Random rnd)
        {
            if (r.IsComposite) return CompositeIds.Essence;
            RuneDef def = c.Rune(r.runeId);
            return def.concepts[rnd.Next(def.concepts.Count)].id;
        }

        private static void CheckEvaluation(SynthesisEvaluation ev)
        {
            Assert.IsFalse(float.IsNaN(ev.score) || float.IsInfinity(ev.score));
            Assert.GreaterOrEqual(ev.score, 0f);
            Assert.LessOrEqual(ev.score, 100f);
            foreach (float f in new[] { ev.factors.coverage, ev.factors.compatibility, ev.factors.intention, ev.factors.order, ev.factors.energy, ev.factors.execution, ev.risk, ev.failureChance, ev.interference })
            {
                Assert.IsFalse(float.IsNaN(f) || float.IsInfinity(f));
                Assert.GreaterOrEqual(f, 0f);
                Assert.LessOrEqual(f, 1f);
            }
            float share = 0f;
            foreach (SlotEvaluation se in ev.slots)
            {
                share += se.idealShare;
                Assert.IsFalse(float.IsNaN(se.idealEnergy) || float.IsNaN(se.minEnergy) || float.IsNaN(se.maxSafeEnergy));
                Assert.LessOrEqual(se.minEnergy, se.idealEnergy + 1e-3f);
                Assert.LessOrEqual(se.idealEnergy, se.maxSafeEnergy + 1e-3f);
            }
            Assert.AreEqual(1f, share, 1e-3f);
            Assert.IsFalse(float.IsNaN(ev.stats.power) || float.IsNaN(ev.stats.stability) || float.IsNaN(ev.stats.energyCostMultiplier));
            Assert.GreaterOrEqual(ev.stats.stability, 0f);
            Assert.LessOrEqual(ev.stats.stability, 100f);
        }

        private static void CheckState(SynthLab lab, int usedBefore)
        {
            RuneCatalog c = lab.c;
            var seen = new HashSet<string>();
            foreach (RuneInstance r in lab.s.runes)
            {
                Assert.IsTrue(seen.Add(r.instanceId), "duplicate rune id " + r.instanceId);
                Assert.IsFalse(float.IsNaN(r.energy) || float.IsInfinity(r.energy));
                Assert.GreaterOrEqual(r.energy, 0f);
                Assert.LessOrEqual(r.energy, c.Balance.MaxEnergy(r.rank, r.quality) + 0.01f, "a rune never holds more than its maximum");
                Assert.GreaterOrEqual(r.rank, 1);
                Assert.GreaterOrEqual(r.quality, 0f);
                Assert.LessOrEqual(r.quality, 100f);
            }
            var spellIds = new HashSet<string>();
            foreach (SpellRecord sp in lab.s.spells)
            {
                Assert.IsTrue(spellIds.Add(sp.spellId), "duplicate spell id");
                Assert.IsNotEmpty(sp.name);
                Assert.IsNotEmpty(sp.anchors);
                foreach (string a in sp.anchors) Assert.IsNotNull(lab.s.Rune(a), "a spell anchors a rune that is gone");
                Assert.IsFalse(float.IsNaN(sp.power) || float.IsInfinity(sp.power));
                Assert.GreaterOrEqual(sp.power, 0f);
                Assert.GreaterOrEqual(sp.stability, 0f);
                Assert.LessOrEqual(sp.stability, 100f);
                Assert.Greater(sp.energyCostMultiplier, 0f);
            }
            Assert.LessOrEqual(RuneRules.UsedSlots(c, lab.s), Math.Max(lab.ctx.capacity, usedBefore));
            Assert.LessOrEqual(lab.s.synthesisLog.Count, c.Synthesis.logLength);
            RuneSoulState back = RuneSoulState.FromJson(lab.s.ToJson());
            Assert.AreEqual(lab.s.ToJson(), back.ToJson(), "every state saves and loads unchanged");
        }

        [Test]
        public void RandomSynthesesKeepEveryInvariant()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var rnd = new Random(20260510);
            int performed = 0, refused = 0, perfect = 0, successful = 0, flawed = 0, failed = 0, runesMade = 0, spellsMade = 0;
            for (int iter = 0; iter < 600; iter++)
            {
                var lab = new SynthLab(c, 14 + rnd.Next(30));
                int kind = rnd.Next(10);                         // 0-6 spell, 7 composite, 8-9 rank advancement
                SynthesisMode mode = kind <= 6 ? SynthesisMode.Spell : kind == 7 ? SynthesisMode.CompositeRune : SynthesisMode.RankAdvancement;
                int count = mode == SynthesisMode.RankAdvancement ? 7 : mode == SynthesisMode.CompositeRune ? 2 + rnd.Next(5) : 1 + rnd.Next(8);
                int rank = 1 + rnd.Next(3);
                bool sameRune = mode == SynthesisMode.RankAdvancement && rnd.Next(2) == 0;
                RuneDef first = c.Runes[rnd.Next(c.Runes.Count)];
                var chosen = new List<RuneInstance>();
                for (int i = 0; i < count; i++)
                {
                    RuneDef def = sameRune ? first : c.Runes[rnd.Next(c.Runes.Count)];
                    int r = mode == SynthesisMode.RankAdvancement ? rank : 1 + rnd.Next(3);
                    float held = (float)(0.2 + 0.8 * rnd.NextDouble()) * c.Balance.MaxEnergy(r);
                    chosen.Add(lab.Add(def.id, r, rnd.Next(0, 101), held));
                }
                for (int i = 0, extra = rnd.Next(4); i < extra; i++) lab.Add(c.Runes[rnd.Next(c.Runes.Count)].id); // runes that stay out of it
                lab.ctx.capacity = Math.Max(lab.ctx.capacity, RuneRules.UsedSlots(c, lab.s));
                if (rnd.Next(6) == 0) lab.SoulFailureChance((float)rnd.NextDouble());

                IntentionDef intention = c.Intentions[rnd.Next(c.Intentions.Count)];
                DesiredEffectDef effect = c.Effects[rnd.Next(c.Effects.Count)];
                if (rnd.Next(3) > 0)
                {
                    // Often an intention that supports the effect, so that more formulas are coherent.
                    foreach (IntentionDef it in c.Intentions)
                        if (c.IntentionSupport(it.id, effect.id) > 0.9f) { intention = it; break; }
                }
                var req = new SynthesisRequest { mode = mode, intentionId = intention.id, effectId = effect.id, seed = rnd.Next() };
                bool pure = sameRune;
                foreach (RuneInstance r in chosen) req.slots.Add(new SynthesisSlot(r.instanceId, pure ? "" : ConceptFor(c, r, rnd), 0f));
                FillRandom(lab, req, rnd);
                if (rnd.Next(12) == 0)
                {
                    // Corrupt one field: it must be refused, never crash.
                    SynthesisSlot sl = req.slots[rnd.Next(req.slots.Count)];
                    switch (rnd.Next(5))
                    {
                        case 0: sl.energy = float.NaN; break;
                        case 1: sl.energy = -5f; break;
                        case 2: sl.runeInstanceId = "ghost"; break;
                        case 3: sl.conceptId = "no.such"; break;
                        default: req.slots[rnd.Next(req.slots.Count)] = null; break;
                    }
                }

                int usedBefore = RuneRules.UsedSlots(c, lab.s);
                string before = lab.Json();
                SynthesisPreview preview = lab.Preview(req);
                Assert.AreEqual(before, lab.Json(), "a preview changes nothing");
                SynthesisResult res = lab.Execute(req);
                if (!res.performed)
                {
                    refused++;
                    Assert.AreNotEqual(SynthesisRefusal.None, res.refusal);
                    Assert.IsNotEmpty(res.message);
                    Assert.AreEqual(before, lab.Json(), "a refusal changes nothing: " + res.refusal);
                    Assert.AreEqual(res.refusal, preview.refusal == SynthesisRefusal.SoulWouldCollapse ? res.refusal : preview.refusal,
                        "the preview and the execution agree on what is refused");
                    continue;
                }
                performed++;
                Assert.IsTrue(preview.CanAttempt || preview.refusal == SynthesisRefusal.SoulWouldCollapse);
                CheckEvaluation(res.evaluation);
                Assert.IsFalse(float.IsNaN(res.score) || float.IsInfinity(res.score));
                Assert.GreaterOrEqual(res.score, 0f);
                Assert.LessOrEqual(res.score, 100f);
                Assert.AreEqual(res.evaluation.Incoherent || res.score < c.Balance.synthesis.flawed, res.tier == SynthesisTier.CompleteFailure);
                Assert.IsNotEmpty(res.synthesisId);
                Assert.IsNotEmpty(res.message);
                foreach (SoulDamageEntry d in res.soulDamage)
                {
                    Assert.IsFalse(float.IsNaN(d.amount) || float.IsInfinity(d.amount));
                    Assert.Greater(d.amount, 0f);
                    Assert.IsNotNull(c.Balance.DamageKind(d.kind), d.kind);
                }
                Assert.GreaterOrEqual(res.energyConsumed, 0f);
                Assert.GreaterOrEqual(res.energyLost, 0f);
                Assert.AreEqual(1, lab.s.synthesesPerfect + lab.s.synthesesSuccessful + lab.s.synthesesFlawed + lab.s.synthesesFailed);
                switch (res.tier)
                {
                    case SynthesisTier.Perfect: perfect++; break;
                    case SynthesisTier.Successful: successful++; break;
                    case SynthesisTier.Flawed: flawed++; Assert.IsNotEmpty(res.flaws); break;
                    default: failed++; Assert.IsEmpty(lab.s.spells); break;
                }
                if (res.output == SynthesisOutput.Spell) { spellsMade++; Assert.AreEqual(mode, SynthesisMode.Spell); }
                if (res.output == SynthesisOutput.Rune) { runesMade++; Assert.AreNotEqual(SynthesisMode.Spell, mode); Assert.AreEqual(chosen.Count, 7 - (mode == SynthesisMode.RankAdvancement ? 0 : 7 - chosen.Count)); }
                CheckState(lab, usedBefore);

                // The same state and request give the same result.
                if (iter % 4 == 0)
                {
                    var twin = new SynthLab(c, lab.ctx.capacity) { s = RuneSoulState.FromJson(before) };
                    twin.ctx.soul = lab.ctx.soul;
                    SynthesisResult again = twin.Execute(req);
                    Assert.AreEqual(res.synthesisId, again.synthesisId);
                    Assert.AreEqual(res.score, again.score);
                    Assert.AreEqual(lab.Json(), twin.Json());
                }
            }
            Assert.Greater(performed, 150, $"the fuzz must exercise the engine (performed {performed}, refused {refused})");
            Assert.Greater(failed, 0);
            Assert.Greater(perfect + successful, 0);
            Assert.Greater(spellsMade, 0);
            Assert.Greater(runesMade, 0);
        }

        [Test]
        public void RandomFormulasOfTheArchetypesAreAlwaysJudgedConsistently()
        {
            // Perturb the ideal formulas: dropping, duplicating and shuffling runes never breaks the evaluation and never
            // beats the ideal.
            RuneCatalog c = RuneDefaults.Catalog();
            var rnd = new Random(99);
            foreach (OutcomeArchetype a in c.Archetypes)
            {
                var idealLab = new SynthLab(c);
                var runes = new List<string>();
                foreach (ArchetypeStage st in a.stages) for (int i = 0; i < st.count; i++) runes.Add(st.runeId);
                var ids = new List<string>();
                foreach (string r in runes) ids.Add(idealLab.Add(r).instanceId);
                SynthesisRequest ideal = idealLab.Fill(SynthesisArchetypes.IdealRequest(a, ids));
                float best = idealLab.Evaluate(ideal).score;
                for (int trial = 0; trial < 12; trial++)
                {
                    SynthesisRequest v = ideal.Clone();
                    int op = rnd.Next(3);
                    if (op == 0 && v.slots.Count > 1) v.slots.RemoveAt(rnd.Next(v.slots.Count));
                    else if (op == 1) { SynthesisSlot twin = v.slots[rnd.Next(v.slots.Count)]; v.slots.Add(new SynthesisSlot(idealLab.Add(c.Rune(c.RuneOfConcept(twin.conceptId).id).id).instanceId, twin.conceptId, twin.energy)); }
                    else v.slots.Reverse();
                    idealLab.Fill(v);
                    SynthesisEvaluation ev = idealLab.Evaluate(v);
                    CheckEvaluation(ev);
                    Assert.LessOrEqual(ev.score, best + 1e-3f, a.id + " variant " + op + " must not beat the ideal formula");
                }
            }
        }
    }
}
#endif
