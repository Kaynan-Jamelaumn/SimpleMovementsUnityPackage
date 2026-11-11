#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the soul resources (Phase 2): Soul Integrity conditions and their restrictions, soul damage and
    /// deep damage, Soul Collapse, recovery rates, Soul Rune Capacity, the saved state and the request guard. Pure rules:
    /// no scene or asset is needed.
    /// </summary>
    public class RuneSoulRulesTests
    {
        private const float Eps = 0.0001f;

        private static RuneBalance Balance() => RuneDefaults.Balance();

        private static SoulState Fresh(RuneBalance b, float max = 100f)
        {
            var s = new SoulState();
            SoulRules.Normalize(b, s, max);
            return s;
        }

        private static SoulState At(RuneBalance b, float integrity, float max = 100f)
        {
            SoulState s = Fresh(b, max);
            s.integrity = integrity;
            return s;
        }

        // ------------------------------------------------------------------ data
        [Test]
        public void BuiltInSoulDataCoversTheSpecSources()
        {
            RuneBalance b = Balance();
            var ids = SoulRules.DamageKindIds(b);
            foreach (string id in new[] { "synthesis", "synthesis_failure", "incompatible_absorption", "overuse", "spell_cast",
                         "spell_failure", "spelling_mistake", "concept_overreach", "primordial", "experiment" })
                Assert.Contains(id, ids, "design spec 14.1 source " + id);
            Assert.AreEqual(0f, b.DamageKind("synthesis").deepShare, Eps, "ordinary synthesis damage heals with meditation (15.2)");
            Assert.Greater(b.DamageKind("primordial").deepShare, 0.5f, "Primordial damage needs specialized healing (15.2)");
            Assert.AreEqual(0f, b.soulRecovery.passivePerMinute, Eps, "the soul does not heal by itself (15.2)");
            Assert.Greater(b.soulRecovery.meditationPerMinute, b.soulRecovery.restPerMinute, "meditation is the efficient method (15.2)");

            var errors = new List<string>();
            var warnings = new List<string>();
            b.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsTrue(RuneDefaults.Catalog().IsValid, string.Join("\n", RuneDefaults.Catalog().Errors));
        }

        [Test]
        public void ValidationReportsBrokenSoulData()
        {
            RuneBalance b = Balance();
            b.soulDamageKinds.Add(new SoulDamageKind("primordial", "Again", 0f, ""));
            b.soulRecovery.minModifierMultiplier = 5f;
            b.soulRecovery.maxModifierMultiplier = 2f;
            var errors = new List<string>();
            b.Validate(errors, new List<string>());
            Assert.IsTrue(errors.Exists(e => e.Contains("'primordial' is listed twice")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("Min Modifier Multiplier")), string.Join("\n", errors));
        }

        [Test]
        public void OlderDatabasesGetTheSoulDataOnUpgrade()
        {
            RuneMagicData data = RuneDefaults.Create();
            data.version = 1;
            data.balance.soulDamageKinds.Clear();
            Assert.IsTrue(data.Upgrade());
            Assert.AreEqual(RuneMagicData.CurrentVersion, data.version);
            Assert.AreEqual(RuneDefaults.SoulDamageKinds().Count, data.balance.soulDamageKinds.Count);
            Assert.IsFalse(data.Upgrade(), "only once");

            RuneMagicData old = RuneDefaults.Create();
            old.version = 1;
            old.balance.soulDamageKinds.Clear();
            RuneCatalog c = RuneCatalog.Compile(old);
            Assert.IsNotNull(c.Balance.DamageKind("primordial"), "the catalog of an older asset has the soul data");
            Assert.AreEqual(1, old.version, "compiling does not change the asset's own data");

            RuneMagicData current = RuneDefaults.Create();
            current.balance.soulDamageKinds.Clear();
            current.Upgrade();
            Assert.AreEqual(0, current.balance.soulDamageKinds.Count, "a current asset that cleared the list keeps it empty");
        }

        // ------------------------------------------------------------------ conditions (design spec 14.3)
        [TestCase(100f, "stable")]
        [TestCase(80f, "stable")]
        [TestCase(79.9f, "strained")]
        [TestCase(60f, "strained")]
        [TestCase(59.9f, "damaged")]
        [TestCase(40f, "damaged")]
        [TestCase(39.9f, "critical")]
        [TestCase(20f, "critical")]
        [TestCase(19.9f, "near_collapse")]
        [TestCase(1f, "near_collapse")]
        [TestCase(0.01f, "near_collapse")]
        [TestCase(0f, "collapse")]
        public void ConditionsFollowTheSpecThresholds(float integrity, string expected)
        {
            RuneBalance b = Balance();
            Assert.AreEqual(expected, SoulRules.Condition(b, At(b, integrity), 100f).id);
            // Thresholds are percents of the maximum, whatever the maximum is.
            Assert.AreEqual(expected, SoulRules.Condition(b, At(b, integrity * 2f, 200f), 200f).id);
        }

        [Test]
        public void RestrictionsWeakenWithTheCondition()
        {
            RuneBalance b = Balance();
            SoulRestrictions stable = SoulRules.Restrictions(b, At(b, 100f), 100f);
            Assert.IsTrue(stable.synthesisAllowed);
            Assert.IsTrue(stable.castingAllowed);
            Assert.AreEqual(1f, stable.powerMultiplier, Eps);
            Assert.IsTrue(stable.AllowsSynthesis(7, 1f, out _));

            SoulRestrictions damaged = SoulRules.Restrictions(b, At(b, 50f), 100f);
            Assert.Greater(damaged.synthesisFailureChance, 0f, "Damaged: increased synthesis failure chance");

            SoulRestrictions critical = SoulRules.Restrictions(b, At(b, 30f), 100f);
            Assert.IsFalse(critical.AllowsSynthesis(4, 0.2f, out string why), "Critical: at most 3 runes");
            StringAssert.Contains("at most 3 runes", why);
            Assert.IsTrue(critical.AllowsSynthesis(3, 0.5f, out _));
            Assert.IsFalse(critical.AllowsSynthesis(2, 0.9f, out _), "complex combinations unavailable (14.4)");

            SoulRestrictions near = SoulRules.Restrictions(b, At(b, 10f), 100f);
            Assert.IsFalse(near.synthesisAllowed, "Near Collapse: synthesis disabled");
            Assert.IsTrue(near.AllowsCasting(1, out _));
            Assert.IsFalse(near.AllowsCasting(2, out _), "casting heavily penalized: rank 1 only");
            Assert.Less(near.powerMultiplier, critical.powerMultiplier);

            SoulRestrictions collapse = SoulRules.Restrictions(b, At(b, 0f), 100f);
            Assert.IsTrue(collapse.collapsed);
            Assert.IsFalse(collapse.AllowsCasting(1, out _));
            Assert.IsFalse(collapse.AllowsSynthesis(1, 0f, out _));
        }

        // ------------------------------------------------------------------ damage
        [Test]
        public void ANewSoulStartsFull()
        {
            RuneBalance b = Balance();
            SoulState s = Fresh(b);
            Assert.IsTrue(s.initialized);
            Assert.AreEqual(100f, s.integrity, Eps);
            Assert.AreEqual(0f, s.deepDamage, Eps);
            s.integrity = 40f;
            SoulRules.Normalize(b, s, 100f);
            Assert.AreEqual(40f, s.integrity, Eps, "normalizing never refills a soul");
        }

        [Test]
        public void DamageScalesWithKindConditionAndLossFactor()
        {
            RuneBalance b = Balance();
            SoulState s = Fresh(b);
            SoulDamageResult r = SoulRules.Damage(b, s, 100f, 10f, "synthesis");
            Assert.AreEqual(10f, r.applied, Eps);
            Assert.AreEqual(0f, r.deepAdded, Eps);
            Assert.AreEqual(90f, s.integrity, Eps);

            SoulState weak = At(b, 30f); // Critical: soul damage ×1.25
            r = SoulRules.Damage(b, weak, 100f, 10f, "synthesis");
            Assert.AreEqual(12.5f, r.applied, Eps, "a weakened soul is hurt more");
            Assert.AreEqual("critical", r.conditionBefore);

            SoulState resisted = Fresh(b);
            SoulRules.Damage(b, resisted, 100f, 10f, "", -50f);
            Assert.AreEqual(95f, resisted.integrity, Eps, "an unknown kind is ordinary damage; the loss factor applies");

            SoulState none = Fresh(b);
            r = SoulRules.Damage(b, none, 100f, -5f, "synthesis");
            Assert.AreEqual(0f, r.applied, Eps, "negative damage does nothing");
            Assert.AreEqual(100f, none.integrity, Eps);
        }

        [Test]
        public void PrimordialDamageScarsTheSoulDeeply()
        {
            RuneBalance b = Balance();
            SoulState s = Fresh(b);
            SoulDamageResult r = SoulRules.Damage(b, s, 100f, 10f, "primordial");
            Assert.AreEqual(90f, s.integrity, Eps);
            Assert.AreEqual(6f, r.deepAdded, Eps);
            Assert.AreEqual(94f, SoulRules.Ceiling(s, 100f), Eps);

            float restored = SoulRules.Restore(b, s, 100f, 100f);
            Assert.AreEqual(4f, restored, Eps, "rest and meditation stop at the ceiling");
            Assert.AreEqual(94f, s.integrity, Eps);

            Assert.AreEqual(6f, SoulRules.HealDeep(b, s, 100f, 50f), Eps, "specialized healing removes the deep damage");
            Assert.AreEqual(94f, s.integrity, Eps, "healing deep damage raises the ceiling, not the integrity");
            SoulRules.Restore(b, s, 100f, 100f);
            Assert.AreEqual(100f, s.integrity, Eps);
        }

        [Test]
        public void DeepDamageStaysWithinItsBounds()
        {
            RuneBalance b = Balance();
            var rng = new System.Random(1234);
            SoulState s = Fresh(b);
            string[] kinds = { "primordial", "synthesis", "experiment", "", "concept_overreach" };
            for (int i = 0; i < 2000; i++)
            {
                switch (rng.Next(4))
                {
                    case 0: SoulRules.Damage(b, s, 100f, (float)rng.NextDouble() * 40f, kinds[rng.Next(kinds.Length)]); break;
                    case 1: SoulRules.Restore(b, s, 100f, (float)rng.NextDouble() * 30f); break;
                    case 2: SoulRules.HealDeep(b, s, 100f, (float)rng.NextDouble() * 10f); break;
                    default: SoulRules.Normalize(b, s, 100f); break;
                }
                Assert.GreaterOrEqual(s.deepDamage, 0f);
                Assert.LessOrEqual(s.deepDamage, 60f + Eps, "Max Deep Damage Fraction");
                Assert.GreaterOrEqual(s.integrity, 0f);
                Assert.LessOrEqual(s.integrity, 100f - s.deepDamage + Eps, "deep damage is part of what is missing");
            }
        }

        // ------------------------------------------------------------------ collapse (decision D8)
        [Test]
        public void ReachingZeroCollapsesTheSoulAndLocksIt()
        {
            RuneBalance b = Balance();
            SoulState s = At(b, 5f);
            SoulDamageResult r = SoulRules.Damage(b, s, 100f, 50f, "synthesis", 0f, 0f, 42L);
            Assert.IsTrue(r.collapsedNow);
            Assert.AreEqual("collapse", r.conditionAfter);
            Assert.AreEqual(0f, s.integrity, Eps);
            Assert.AreEqual(1, s.collapses);
            Assert.AreEqual(42L, s.lastCollapseUtc);
            Assert.IsTrue(s.collapseLocked);
            Assert.AreEqual(5f, s.deepDamage, Eps, "the collapse itself scars the soul (5% of the maximum)");

            r = SoulRules.Damage(b, s, 100f, 10f, "synthesis");
            Assert.IsFalse(r.collapsedNow, "a collapsed soul does not collapse again");
            Assert.AreEqual(1, s.collapses);

            SoulRules.Restore(b, s, 100f, 19f);
            Assert.AreEqual("near_collapse", SoulRules.Condition(b, s, 100f).id);
            Assert.IsTrue(s.collapseLocked, "still locked below 20%");
            SoulRestrictions locked = SoulRules.Restrictions(b, s, 100f);
            Assert.IsFalse(locked.castingAllowed, "no casting until recovered, whatever the condition allows");
            Assert.IsFalse(locked.AllowsCasting(1, out string why));
            StringAssert.Contains("collapsed", why);

            SoulRules.Restore(b, s, 100f, 1f);
            Assert.IsFalse(s.collapseLocked, "released at Lock Until Percent");
            Assert.IsTrue(SoulRules.Restrictions(b, s, 100f).castingAllowed);
        }

        // ------------------------------------------------------------------ maximum and bonuses
        [Test]
        public void MaximumBonusesRaiseAndLowerTheMaximum()
        {
            RuneBalance b = Balance();
            SoulState s = Fresh(b);
            Assert.IsTrue(SoulRules.SetMaxBonus(s, "relic", 20f));
            Assert.IsFalse(SoulRules.SetMaxBonus(s, "relic", 20f), "same value: no change");
            Assert.AreEqual(120f, SoulRules.MaxIntegrity(b, s), Eps);
            Assert.AreEqual(125f, SoulRules.MaxIntegrity(b, s, 5f), Eps, "plus a runtime modifier");
            SoulRules.Restore(b, s, 120f, 50f);
            Assert.AreEqual(120f, s.integrity, Eps);
            Assert.IsTrue(SoulRules.SetMaxBonus(s, "relic", 0f), "0 removes it");
            SoulRules.Normalize(b, s, SoulRules.MaxIntegrity(b, s));
            Assert.AreEqual(100f, s.integrity, Eps, "a lower maximum clamps the integrity");
            Assert.AreEqual(1f, SoulRules.MaxIntegrity(b, s, -500f), Eps, "never below 1");
        }

        // ------------------------------------------------------------------ recovery (design spec 15)
        private static float Rate(RuneBalance b, RecoveryContext c) => RecoveryRules.PerMinute(b.soulRecovery, c, out _);

        [Test]
        public void TheSoulDoesNotRecoverByItself()
        {
            RuneBalance b = Balance();
            Assert.AreEqual(0f, Rate(b, RecoveryContext.Default), Eps, "no automatic return to full after a fight (15.2)");
        }

        [Test]
        public void RestIsGradualAndFollowsTheSleepQuality()
        {
            RuneBalance b = Balance();
            RecoveryContext c = RecoveryContext.Default;
            c.sleeping = true;
            Assert.AreEqual(3f, Rate(b, c), Eps);
            c.sleepQuality = 0.5f;
            Assert.AreEqual(1.5f, Rate(b, c), Eps, "the ground is worse than a bed");
            b.soulRecovery.restUsesSleepQuality = false;
            Assert.AreEqual(3f, Rate(b, c), Eps);
        }

        [Test]
        public void MeditationIsEfficientOnceSettled()
        {
            RuneBalance b = Balance();
            RecoveryContext c = RecoveryContext.Default;
            c.meditating = true;
            c.meditationSeconds = 1f;
            Assert.AreEqual(0f, Rate(b, c), Eps, "settling in");
            Assert.AreEqual(1f / 3f, RecoveryRules.MeditationSettled(b.soulRecovery, 1f), 0.001f);
            c.meditationSeconds = 3f;
            Assert.AreEqual(10f, Rate(b, c), Eps);
            RecoveryContext rest = RecoveryContext.Default;
            rest.sleeping = true;
            Assert.Greater(Rate(b, c), Rate(b, rest), "meditation is more efficient than rest");
        }

        [Test]
        public void SanctuariesCityModifiersAndFactorsScaleRecovery()
        {
            RuneBalance b = Balance();
            RecoveryContext c = RecoveryContext.Default;
            c.meditating = true;
            c.meditationSeconds = 10f;
            c.inSanctuary = true;
            c.sanctuaryMultiplier = 2f;
            c.sanctuaryPassivePerMinute = 1f;
            float rate = RecoveryRules.PerMinute(b.soulRecovery, c, out RecoveryBreakdown br);
            Assert.AreEqual(21f, rate, Eps, "meditation ×2 in the sanctuary + 1 for being there");
            Assert.AreEqual(2f, br.placeMultiplier, Eps);
            StringAssert.Contains("meditation", br.Describe());

            c.inCombat = true;
            Assert.AreEqual(20f, Rate(b, c), Eps, "no sanctuary trickle in combat");
            c.inCombat = false;

            c.inCity = true;
            b.soulRecovery.cityMultiplier = 3f;
            Assert.AreEqual(31f, Rate(b, c), Eps, "the best place counts, they do not stack");
            c.inCity = false;

            c.modifierMultiplier = 1.5f;
            c.modifierFlatPerMinute = 2f;
            Assert.AreEqual((20f + 1f + 2f) * 1.5f, Rate(b, c), Eps, "abilities and traits");
            c.modifierMultiplier = 100f;
            Assert.AreEqual((20f + 1f + 2f) * 10f, Rate(b, c), Eps, "clamped to Max Modifier Multiplier");
            c.modifierMultiplier = 1f;
            c.gainFactorPercent = 100f;
            Assert.AreEqual((20f + 1f + 2f) * 2f, Rate(b, c), Eps, "the status's Increment Factor");

            c.alive = false;
            Assert.AreEqual(0f, Rate(b, c), Eps, "the dead and the downed recover nothing");
        }

        [Test]
        public void PassiveRecoveryIsOptionalData()
        {
            RuneBalance b = Balance();
            b.soulRecovery.passivePerMinute = 0.5f;
            RecoveryContext c = RecoveryContext.Default;
            Assert.AreEqual(0.5f, Rate(b, c), Eps);
            c.inCombat = true;
            Assert.AreEqual(0f, Rate(b, c), Eps, "natural recovery only out of combat");
            b.soulRecovery.passiveOnlyOutOfCombat = false;
            Assert.AreEqual(0.5f, Rate(b, c), Eps);
        }

        // ------------------------------------------------------------------ capacity (design spec 4)
        [TestCase(1, 7)]
        [TestCase(19, 7)]
        [TestCase(20, 14)]
        [TestCase(39, 14)]
        [TestCase(40, 21)]
        [TestCase(60, 28)]
        [TestCase(200, 28)]
        public void CapacityGrowsWithProgression(int level, int capacity)
        {
            RuneBalance b = Balance();
            Assert.AreEqual(capacity, CapacityRules.Capacity(b, level, new SoulState()));
        }

        [Test]
        public void CapacityBonusesAndFitting()
        {
            RuneBalance b = Balance();
            var s = new SoulState();
            Assert.IsTrue(CapacityRules.SetBonus(s, "breakthrough:first", 2));
            Assert.IsFalse(CapacityRules.SetBonus(s, "breakthrough:first", 2), "same source and value: no change");
            Assert.AreEqual(9, CapacityRules.Capacity(b, 1, s));
            Assert.AreEqual(10, CapacityRules.Capacity(b, 1, s, 1), "plus equipment / abilities");
            Assert.IsTrue(CapacityRules.SetBonus(s, "breakthrough:first", 0));
            Assert.AreEqual(7, CapacityRules.Capacity(b, 1, s));

            Assert.IsTrue(CapacityRules.CanFit(7, 6, 1));
            Assert.IsFalse(CapacityRules.CanFit(7, 6, 2), "the system prevents unlimited absorption (4.1)");
            Assert.AreEqual(0, CapacityRules.Free(7, 9));
            Assert.IsTrue(CapacityRules.Overfull(7, 9));

            CapacityStage next = CapacityRules.NextStage(b, 1);
            Assert.AreEqual(20, next.fromLevel);
            Assert.AreEqual(14, next.capacity);
            Assert.IsNull(CapacityRules.NextStage(b, 60));
        }

        // ------------------------------------------------------------------ independence (design spec 20)
        [Test]
        public void TheSoulIsItsOwnResource()
        {
            // The soul holds no health, mana, stamina or rune energy: none of them can change with it.
            string json = UnityEngine.JsonUtility.ToJson(new SoulState());
            foreach (string other in new[] { "health", "mana", "stamina", "energy", "hp" })
                StringAssert.DoesNotContain("\"" + other, json.ToLowerInvariant());
            // And a full soul with no capacity left, or a broken soul with free slots, are both valid states.
            RuneBalance b = Balance();
            SoulState broken = At(b, 5f);
            Assert.AreEqual(7, CapacityRules.Capacity(b, 1, broken), "capacity does not depend on Soul Integrity");
        }

        // ------------------------------------------------------------------ saved state
        [Test]
        public void TheSavedStateRoundTrips()
        {
            var st = new RuneSoulState();
            st.soul.initialized = true;
            st.soul.integrity = 62f;
            st.soul.deepDamage = 7.5f;
            st.soul.collapseLocked = true;
            st.soul.collapses = 2;
            st.soul.lastCollapseUtc = 638000000000000000L;
            st.soul.maxBonuses.Add(new IntegrityBonus { source = "relic", amount = 10f });
            st.soul.capacityBonuses.Add(new CapacityBonus { source = "breakthrough", slots = 3 });
            RuneSoulState back = RuneSoulState.FromJson(st.ToJson());
            Assert.AreEqual(62f, back.soul.integrity, Eps);
            Assert.AreEqual(7.5f, back.soul.deepDamage, Eps);
            Assert.IsTrue(back.soul.collapseLocked);
            Assert.AreEqual(2, back.soul.collapses);
            Assert.AreEqual(638000000000000000L, back.soul.lastCollapseUtc);
            Assert.AreEqual("relic", back.soul.maxBonuses[0].source);
            Assert.AreEqual(3, back.soul.capacityBonuses[0].slots);
            Assert.AreEqual(RuneSoulState.CurrentVersion, back.version);

            RuneSoulState clone = st.Clone();
            clone.soul.integrity = 1f;
            Assert.AreEqual(62f, st.soul.integrity, Eps, "a clone is independent");
        }

        [Test]
        public void BrokenOrPartialSavesGiveAUsableState()
        {
            RuneSoulState broken = RuneSoulState.FromJson("{ this is not json");
            Assert.IsNotNull(broken.soul);
            Assert.IsFalse(broken.soul.initialized, "a fresh soul (starts full once normalized)");
            RuneSoulState partial = RuneSoulState.FromJson("{\"version\":1,\"soul\":{\"initialized\":true,\"integrity\":33}}");
            Assert.AreEqual(33f, partial.soul.integrity, Eps);
            Assert.IsNotNull(partial.soul.maxBonuses);
            Assert.IsNotNull(partial.soul.capacityBonuses);
            Assert.IsNotNull(RuneSoulState.FromJson(null).soul);
        }

        [Test]
        public void TheViewComparesWithATolerance()
        {
            var a = new RuneSoulView { runeUser = true, integrity = 50f, maxIntegrity = 100f, conditionId = "damaged" };
            RuneSoulView b = a.Clone();
            Assert.IsTrue(a.SameAs(b));
            b.integrity = 50.005f;
            Assert.IsTrue(a.SameAs(b), "hundredths are not worth a network update");
            b.integrity = 50.5f;
            Assert.IsFalse(a.SameAs(b));
            b.CopyFrom(a);
            b.messageSeq++;
            Assert.IsFalse(a.SameAs(b), "a new message is a change");
            Assert.AreEqual(50f, a.Percent, Eps);
            Assert.AreEqual(100f, a.Ceiling, Eps);
        }

        // ------------------------------------------------------------------ network guard
        [Test]
        public void TheRequestGuardLimitsFloods()
        {
            var g = new RuneRequestGuard(2f, 3);
            Assert.IsTrue(g.Allow(0f));
            Assert.IsTrue(g.Allow(0f));
            Assert.IsTrue(g.Allow(0f));
            Assert.IsFalse(g.Allow(0f), "burst used up");
            Assert.IsFalse(g.Allow(0.25f), "half a token");
            Assert.IsTrue(g.Allow(1f), "refilled at 2 per second");
            Assert.IsTrue(g.Allow(1f));
            Assert.IsFalse(g.Allow(1f));
            Assert.IsTrue(g.Allow(10f));
            Assert.IsTrue(g.Allow(10f));
            Assert.IsTrue(g.Allow(10f));
            Assert.IsFalse(g.Allow(10f), "never more than the burst");
            Assert.IsFalse(g.Allow(float.NaN));
            g.Reset();
            Assert.IsTrue(g.Allow(10f));
        }
    }
}
#endif
