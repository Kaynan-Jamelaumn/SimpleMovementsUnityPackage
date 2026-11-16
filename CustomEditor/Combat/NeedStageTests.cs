#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalSystem.Tests
{
    /// <summary>
    /// The conditions of hunger, thirst and rest (<see cref="NeedStage"/>): which one applies at a percent, the default
    /// ladders, older "below percent" stages brought over, and regeneration changed by a percentage.
    /// Window ▸ General ▸ Test Runner ▸ EditMode.
    /// </summary>
    public class NeedStageTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private static string NameAt(IList<NeedStage> stages, float percent)
        {
            int i = NeedStages.Find(stages, percent);
            return i >= 0 ? stages[i].name : null;
        }

        // ------------------------------------------------------------------ which condition applies
        [Test]
        public void Find_TakesTheHighestConditionReached()
        {
            var stages = new List<NeedStage>
            {
                new NeedStage("Low", 0f, NeedMood.Bad), new NeedStage("High", 70f, NeedMood.Good), new NeedStage("Middle", 30f, NeedMood.Fine),
            };
            Assert.AreEqual("Low", NameAt(stages, 0f));
            Assert.AreEqual("Low", NameAt(stages, 29.9f));
            Assert.AreEqual("Middle", NameAt(stages, 30f), "at its percent the condition has begun");
            Assert.AreEqual("Middle", NameAt(stages, 69f));
            Assert.AreEqual("High", NameAt(stages, 70f));
            Assert.AreEqual("High", NameAt(stages, 100f));

            var gap = new List<NeedStage> { new NeedStage("Only Above", 40f, NeedMood.Fine) };
            Assert.IsNull(NameAt(gap, 10f), "below every condition: none");
            Assert.AreEqual(-1, NeedStages.Find(null, 50f));
            Assert.AreEqual(-1, NeedStages.Find(new List<NeedStage>(), 50f));
        }

        [Test]
        public void Runner_FollowsTheCondition_OnACopyThatRunsNoRules()
        {
            var go = new GameObject("Needs");
            created.Add(go);
            var runner = new NeedStageRunner(go.transform, "Test");
            List<NeedStage> stages = NeedStages.Hunger();
            var seen = new List<string>();
            runner.Changed += (before, now) => seen.Add($"{(before != null ? before.name : "-")}>{(now != null ? now.name : "-")}");

            runner.Tick(stages, 100f, 0.1f, false);
            Assert.AreEqual("Stuffed", runner.Current.name);
            runner.Tick(stages, 100f, 0.1f, false);
            runner.Tick(stages, 40f, 0.1f, false);
            Assert.AreEqual("Peckish", runner.Current.name);
            runner.Tick(stages, 0f, 0.1f, false);
            Assert.AreEqual("Starving", runner.Current.name);
            runner.Tick(stages, 0f, 0.1f, false, false);
            Assert.IsNull(runner.Current, "down or asleep: no condition");
            CollectionAssert.AreEqual(new[] { "->Stuffed", "Stuffed>Peckish", "Peckish>Starving", "Starving>-" }, seen);
        }

        // ------------------------------------------------------------------ the default ladders
        private static IEnumerable<TestCaseData> Ladders()
        {
            yield return new TestCaseData("Hunger", NeedStages.Hunger(), new[] { "Full", "Satisfied", "Peckish", "Hungry", "Starving" });
            yield return new TestCaseData("Thirst", NeedStages.Thirst(), new[] { "Quenched", "Dry Throat", "Thirsty", "Dehydrated" });
            yield return new TestCaseData("Rest", NeedStages.Rest(), new[] { "Rested", "Sleepy", "Tired", "Exhausted", "Falling Asleep" });
        }

        [TestCaseSource(nameof(Ladders))]
        public void Defaults_CoverTheWholeBar_EachWithItsOwnEffect(string need, List<NeedStage> stages, string[] mustHave)
        {
            var warnings = new List<string>();
            NeedStages.Validate(stages, need, warnings);
            Assert.IsEmpty(warnings, string.Join("\n", warnings));
            foreach (string name in mustHave)
                Assert.IsTrue(stages.Any(s => s.name == name), $"{need} has no '{name}' condition");

            // Best first, down to 0%, getting worse on the way.
            for (int i = 1; i < stages.Count; i++)
            {
                Assert.Less(stages[i].fromPercent, stages[i - 1].fromPercent, $"{stages[i].name} is listed under {stages[i - 1].name}");
                Assert.GreaterOrEqual((int)stages[i].mood, (int)stages[i - 1].mood, $"{stages[i].name} is not milder than {stages[i - 1].name}");
            }
            Assert.AreEqual(0f, stages[stages.Count - 1].fromPercent);
            Assert.AreEqual(NeedMood.Critical, stages[stages.Count - 1].mood, "the bottom is dangerous");
            Assert.AreEqual(NeedMood.Good, stages[0].mood, "the top is better than normal");
            Assert.IsTrue(stages.Any(s => s.mood == NeedMood.Fine && !s.HasEffect), "a normal condition that does nothing");

            // Every condition that is not the normal one does something, and no two do the same.
            var effects = new HashSet<string>();
            foreach (NeedStage s in stages)
            {
                if (s.mood == NeedMood.Fine)
                    continue;
                Assert.IsTrue(s.HasEffect, $"{s.name} does nothing");
                Assert.IsTrue(effects.Add(s.effect.Describe()), $"{s.name} does exactly what another condition does");
                Assert.AreEqual(s.mood != NeedMood.Good, s.effect.debuff, $"{s.name}: a buff or a debuff?");
            }
            // The worse it gets, the more it shows on the screen.
            Assert.Greater(stages[stages.Count - 1].screenEffect, 0.5f);
            Assert.AreEqual(0f, stages[0].screenEffect);
        }

        [TestCaseSource(nameof(Ladders))]
        public void Defaults_ChangeRegenerationByAPercentage_NotByAnAmountPerTick(string need, List<NeedStage> stages, string[] unused)
        {
            foreach (NeedStage s in stages)
                foreach (TraitModifier m in s.effect.characterStats)
                    Assert.IsNull(TraitStats.RegenRateOf(m.stat),
                        $"{need} ▸ {s.name}: {m.stat} is an amount per tick; use the Regen Rate stat for a percentage");
        }

        // ------------------------------------------------------------------ regeneration as a percentage
        [Test]
        public void RegenRateStats_ArePercentages_CombinedFromEveryModifier()
        {
            foreach (TraitStat stat in new[] { TraitStat.HealthRegenRate, TraitStat.StaminaRegenRate, TraitStat.ManaRegenRate })
            {
                Assert.IsTrue(TraitStats.IsAggregate(stat), stat.ToString());
                Assert.AreEqual(TraitStats.Modes.PercentOnly, TraitStats.Get(stat).modes, stat.ToString());
                Assert.IsTrue(TraitStats.Get(stat).higherIsBetter, stat.ToString());
            }
            Assert.AreEqual(TraitStat.StaminaRegenRate, TraitStats.RegenRateOf(TraitStat.StaminaRegen));
            Assert.AreEqual(TraitStat.HealthRegenRate, TraitStats.RegenRateOf(TraitStat.HealthRegen));
            Assert.AreEqual(TraitStat.ManaRegenRate, TraitStats.RegenRateOf(TraitStat.ManaRegen));
            Assert.IsNull(TraitStats.RegenRateOf(TraitStat.MoveSpeed));
        }

        [Test]
        public void OlderPercentModifiersOfFlatRegeneration_AreReadAsPercentages()
        {
            var old = new List<TraitModifier>
            {
                TraitModifier.Percent(TraitStat.MoveSpeed, -5f),
                TraitModifier.Percent(TraitStat.StaminaRegen, -10f), // what "Tired" used to carry: it removed 10 stamina a tick
                TraitModifier.Flat(TraitStat.StaminaRegen, 1f),      // a real flat bonus stays
                TraitModifier.Percent(TraitStat.ManaRegen, 2f),      // too small to be a percentage: left alone
            };
            IList<TraitModifier> read = TraitStats.ReadRegenPercents(old);
            Assert.AreEqual(4, read.Count);
            Assert.AreEqual(TraitStat.MoveSpeed, read[0].stat);
            Assert.AreEqual(TraitStat.StaminaRegenRate, read[1].stat);
            Assert.AreEqual(-10f, read[1].value);
            Assert.AreEqual(TraitModifierMode.Percent, read[1].EffectiveMode);
            Assert.AreEqual(TraitStat.StaminaRegen, read[2].stat);
            Assert.AreEqual(TraitStat.ManaRegen, read[3].stat);
            Assert.AreEqual(TraitStat.StaminaRegen, old[1].stat, "the list given is not changed");

            var clean = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -5f), TraitModifier.Flat(TraitStat.HealthRegen, 2f) };
            Assert.AreSame(clean, TraitStats.ReadRegenPercents(clean), "nothing to read differently: the same list");
            Assert.IsNull(TraitStats.ReadRegenPercents(null));
        }

        // [unity-only]
        [Test]
        public void RegenerationMultiplier_ScalesWhatATickGives()
        {
            var go = new GameObject("Stamina");
            created.Add(go);
            StaminaManager stamina = go.AddComponent<StaminaManager>();
            float perTick = stamina.IncrementValue;
            Assert.Greater(perTick, 0f);
            Assert.AreEqual(perTick, stamina.RegenPerTick, 1e-5f);

            object tired = new object(), hungry = new object();
            stamina.SetRegenMultiplier(tired, 0.8f);
            Assert.AreEqual(perTick * 0.8f, stamina.RegenPerTick, 1e-5f, "-20%");
            stamina.SetRegenMultiplier(hungry, 0.5f);
            Assert.AreEqual(perTick * 0.4f, stamina.RegenPerTick, 1e-5f, "two sources multiply");
            stamina.SetRegenMultiplier(tired, 0.9f);
            Assert.AreEqual(perTick * 0.45f, stamina.RegenPerTick, 1e-5f, "the same source replaces its own");
            stamina.SetRegenMultiplier(tired, 1f);
            stamina.SetRegenMultiplier(hungry, 1f);
            Assert.AreEqual(perTick, stamina.RegenPerTick, 1e-5f, "back to normal");
            Assert.AreEqual(perTick, stamina.IncrementValue, 1e-5f, "the amount per tick itself is never touched");
        }
        // [/unity-only]

        // ------------------------------------------------------------------ older Sleep Managers
        [Test]
        public void OlderTirednessStages_BecomeConditionsThatDoTheSame()
        {
            var legacy = new List<RestStage>
            {
                new RestStage { belowPercent = 50f, effect = new StatusConsequence("Tired") { characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -10f) } } },
                new RestStage { belowPercent = 10f, effect = new StatusConsequence("Exhausted") { stunChancePerMinute = 0.25f } },
                new RestStage { belowPercent = 25f, effect = new StatusConsequence("Very Tired") { sanityPerSecond = -0.03f } },
            };
            List<NeedStage> stages = NeedStages.FromBelowStages(legacy);
            CollectionAssert.AreEqual(new[] { "Rested", "Tired", "Very Tired", "Exhausted" }, stages.Select(s => s.name).ToArray());
            CollectionAssert.AreEqual(new[] { 50f, 25f, 10f, 0f }, stages.Select(s => s.fromPercent).ToArray());
            CollectionAssert.AreEqual(new[] { NeedMood.Fine, NeedMood.Low, NeedMood.Bad, NeedMood.Critical }, stages.Select(s => s.mood).ToArray());

            // The same condition at every percent as the stages gave.
            Assert.AreEqual("Rested", NameAt(stages, 80f));
            Assert.AreEqual("Rested", NameAt(stages, 50f));
            Assert.AreEqual("Tired", NameAt(stages, 49f));
            Assert.AreEqual("Very Tired", NameAt(stages, 24f));
            Assert.AreEqual("Exhausted", NameAt(stages, 9f));
            Assert.AreEqual("Exhausted", NameAt(stages, 0f));

            // Its effects come along; "-10% stamina regeneration" now is one.
            NeedStage tired = stages[1];
            Assert.AreEqual(TraitStat.StaminaRegenRate, tired.effect.characterStats[0].stat);
            Assert.AreEqual(-10f, tired.effect.characterStats[0].value);
            Assert.AreEqual(0.25f, stages[3].effect.stunChancePerMinute);
            Assert.AreEqual(TraitStat.StaminaRegen, legacy[0].effect.characterStats[0].stat, "the old stages are not changed");

            List<NeedStage> none = NeedStages.FromBelowStages(new List<RestStage>());
            Assert.AreEqual(1, none.Count, "stages that were all removed stay removed: only Rested");
            Assert.IsFalse(none[0].HasEffect);
        }
    }
}
#endif
