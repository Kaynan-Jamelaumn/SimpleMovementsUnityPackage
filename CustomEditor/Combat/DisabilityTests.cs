#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalSystem.Tests
{
    /// <summary>
    /// Disabilities: actions a trait blocks (<see cref="ActionBlocks"/>), the clock of the ones that come and go
    /// (<see cref="EpisodeSchedule"/>), and the ready-made traits (<see cref="DisabilityTraitPresets"/>).
    /// Window ▸ General ▸ Test Runner ▸ EditMode.
    /// </summary>
    public class DisabilityTests
    {
        // ------------------------------------------------------------------ what cannot be done
        [Test]
        public void Blocks_AddUp_AndEachSourceLiftsOnlyItsOwn()
        {
            var blocks = new ActionBlocks();
            Assert.IsFalse(blocks.Any);
            Assert.IsTrue(blocks.Allows(BodyAction.Sprint));

            object leg = new object(), knees = new object();
            blocks.Set(leg, BodyAction.Sprint | BodyAction.Roll, "A bad leg");
            blocks.Set(knees, BodyAction.Jump | BodyAction.Roll, "Bad knees");
            Assert.IsFalse(blocks.Allows(BodyAction.Sprint));
            Assert.IsFalse(blocks.Allows(BodyAction.Jump));
            Assert.IsFalse(blocks.Allows(BodyAction.Roll));
            Assert.IsTrue(blocks.Allows(BodyAction.Dash));
            Assert.AreEqual("A bad leg", blocks.ReasonFor(BodyAction.Sprint));
            Assert.AreEqual("Bad knees", blocks.ReasonFor(BodyAction.Jump));
            Assert.AreEqual("", blocks.ReasonFor(BodyAction.Dash));

            blocks.Clear(leg);
            Assert.IsTrue(blocks.Allows(BodyAction.Sprint));
            Assert.IsFalse(blocks.Allows(BodyAction.Roll), "the knees still stop it");
            blocks.Set(knees, BodyAction.Jump, "Bad knees");
            Assert.IsTrue(blocks.Allows(BodyAction.Roll), "the same source replaces what it blocked");
            blocks.Set(knees, BodyAction.None, "");
            Assert.IsFalse(blocks.Any, "blocking nothing removes the source");
            blocks.Clear(new object());
            blocks.Set(null, BodyAction.Jump, "ignored");
            Assert.IsFalse(blocks.Any);
        }

        [Test]
        public void Blocks_TakeTheHandsAway()
        {
            var blocks = new ActionBlocks();
            HandCapability full = HandCapability.Full;
            Assert.IsTrue(blocks.Apply(full).IsFull, "no block: both hands");

            blocks.Set("arm", BodyAction.OffHand, "You have one arm");
            HandCapability h = blocks.Apply(full);
            Assert.IsTrue(h.mainHand);
            Assert.IsFalse(h.offHand);
            Assert.IsFalse(h.twoHanded, "a two-handed weapon needs both");
            Assert.AreEqual("You have one arm", h.offReason);
            Assert.AreEqual("You have one arm", h.twoHandedReason);

            blocks.Clear("arm");
            blocks.Set("grip", BodyAction.TwoHandedWeapons, "Too heavy for you");
            h = blocks.Apply(full);
            Assert.IsTrue(h.mainHand && h.offHand);
            Assert.IsFalse(h.twoHanded);
            Assert.AreEqual("Too heavy for you", h.twoHandedReason);

            // On top of what the injuries already took: never gives a hand back.
            var injured = new HandCapability { mainHand = false, offHand = true, twoHanded = false, mainReason = "Broken hand", twoHandedReason = "Broken hand" };
            h = blocks.Apply(injured);
            Assert.IsFalse(h.mainHand);
            Assert.AreEqual("Broken hand", h.mainReason);
            Assert.AreEqual("Broken hand", h.twoHandedReason);
        }

        // ------------------------------------------------------------------ episodes
        private static Func<float> Seeded(int seed)
        {
            var random = new System.Random(seed);
            return () => (float)random.NextDouble();
        }

        [Test]
        public void Episodes_ComeAboutAsOftenAsAsked_EachWithItsOwnStrengthAndLength()
        {
            var timing = new EpisodeTiming(6f, 20f, 60f, 0.3f, 1f, 30f);
            var schedule = new EpisodeSchedule();
            Func<float> random = Seeded(7);
            const float dt = 0.25f;
            int episodes = 0;
            float quiet = 0f, lastEnd = -999f, clock = 0f, shortestGap = float.MaxValue;
            var severities = new List<float>();
            var lengths = new List<float>();
            for (int i = 0; i < 4 * 3600 * 200; i++) // 200 hours
            {
                clock += dt;
                int what = schedule.Tick(timing, dt, true, random);
                if (what == EpisodeSchedule.Started)
                {
                    episodes++;
                    severities.Add(schedule.Severity);
                    lengths.Add(schedule.Duration);
                    shortestGap = Mathf.Min(shortestGap, clock - lastEnd);
                }
                else if (what == EpisodeSchedule.Ended)
                {
                    lastEnd = clock;
                }
                if (!schedule.Active)
                    quiet += dt;
            }
            // Quiet time between two episodes: the gap, then six minutes on average.
            float averageQuiet = quiet / episodes;
            Assert.That(averageQuiet, Is.InRange(330f, 450f), $"{episodes} episodes in 200 hours, {averageQuiet:0} s of quiet between two");
            Assert.GreaterOrEqual(shortestGap, 30f - dt, "never sooner than the gap");
            Assert.That(severities.Min(), Is.InRange(0.3f, 0.36f));
            Assert.That(severities.Max(), Is.InRange(0.94f, 1f));
            Assert.That(severities.Average(), Is.InRange(0.6f, 0.7f), "spread over the whole range");
            Assert.That(lengths.Min(), Is.InRange(20f, 23f));
            Assert.That(lengths.Max(), Is.InRange(57f, 60f));
        }

        [Test]
        public void Episodes_OnlyStartWhileWhatBringsThemOnHolds_AndCanBeCutShort()
        {
            var timing = new EpisodeTiming(0.05f, 30f, 30f, 1f, 1f, 10f);
            var schedule = new EpisodeSchedule();
            Func<float> random = Seeded(3);
            for (int i = 0; i < 4000; i++)
                Assert.AreEqual(EpisodeSchedule.Nothing, schedule.Tick(timing, 0.25f, false, random), "nothing brings it on");
            int started = 0;
            for (int i = 0; i < 400 && started == 0; i++)
                if (schedule.Tick(timing, 0.25f, true, random) == EpisodeSchedule.Started) started++;
            Assert.AreEqual(1, started, "it starts soon once it can");
            Assert.IsTrue(schedule.Active);
            Assert.AreEqual(30f, schedule.Duration, 1e-3f);

            // It runs its course even when what brought it on stops...
            for (int i = 0; i < 40; i++)
                schedule.Tick(timing, 0.25f, false, random);
            Assert.IsTrue(schedule.Active);
            // ...unless it is ended.
            schedule.End(10f);
            Assert.IsFalse(schedule.Active);
            Assert.AreEqual(10f, schedule.GapLeft, 1e-3f);
            for (int i = 0; i < 39; i++)
                Assert.AreEqual(EpisodeSchedule.Nothing, schedule.Tick(timing, 0.25f, true, random), "the gap");
        }

        [Test]
        public void AConditionThatNeverLeaves_OnlyChangesStrength()
        {
            var timing = new EpisodeTiming(0f, 100f, 200f, 0f, 1f, 0f);
            Assert.IsTrue(timing.Continuous);
            var schedule = new EpisodeSchedule();
            Func<float> random = Seeded(11);
            int stretches = 0;
            float quiet = 0f;
            var strengths = new List<float>();
            for (int i = 0; i < 4 * 3600 * 20; i++)
            {
                int what = schedule.Tick(timing, 0.25f, true, random);
                if (what == EpisodeSchedule.Ended)
                    what = schedule.Tick(timing, 0.001f, true, random); // the trait starts the next one at once
                if (what == EpisodeSchedule.Started)
                {
                    stretches++;
                    strengths.Add(schedule.Severity);
                }
                if (!schedule.Active)
                    quiet += 0.25f;
            }
            Assert.That(stretches, Is.InRange(400, 560), "a new stretch every 100 to 200 seconds");
            Assert.AreEqual(0f, quiet, "never a moment without it");
            Assert.Less(strengths.Min(), 0.05f, "some stretches are hardly felt");
            Assert.Greater(strengths.Max(), 0.95f, "some are as bad as it gets");
        }

        [Test]
        public void TheNameOfAnEpisodeSaysHowBadItIs_AndAClientReadsItBack()
        {
            var range = new Vector2(0.3f, 1f);
            Assert.AreEqual("Mild Migraine", EpisodeSchedule.TierName("Migraine", 0.35f, range));
            Assert.AreEqual("Migraine", EpisodeSchedule.TierName("Migraine", 0.65f, range));
            Assert.AreEqual("Severe Migraine", EpisodeSchedule.TierName("Migraine", 0.95f, range));
            Assert.AreEqual("Sleep Attack", EpisodeSchedule.TierName("Sleep Attack", 1f, new Vector2(1f, 1f)), "one strength only: no word for it");

            foreach (float severity in new[] { 0.3f, 0.45f, 0.6f, 0.8f, 1f })
            {
                string name = EpisodeSchedule.TierName("Migraine", severity, range);
                Assert.IsTrue(EpisodeSchedule.TryReadTier(name, "Migraine", range, out float read), name);
                Assert.That(read, Is.InRange(0.3f, 1f));
                Assert.Less(Mathf.Abs(read - severity), 0.25f, $"{name}: {severity} read back as {read}");
            }
            Assert.IsTrue(EpisodeSchedule.TryReadTier("Sleep Attack", "Sleep Attack", new Vector2(1f, 1f), out float full));
            Assert.AreEqual(1f, full, 1e-4f);
            Assert.IsFalse(EpisodeSchedule.TryReadTier("Tired", "Migraine", range, out _));
            Assert.IsFalse(EpisodeSchedule.TryReadTier("Severe Pain", "Migraine", range, out _));
            Assert.IsFalse(EpisodeSchedule.TryReadTier(null, "Migraine", range, out _));
        }

        // [unity-only]
        // ------------------------------------------------------------------ the ready-made traits
        [Test]
        public void EveryDisability_IsAValidDrawback_ThatGivesItsPoints()
        {
            TraitPresets.Preset[] presets = DisabilityTraitPresets.Build();
            Assert.GreaterOrEqual(presets.Length, 50);
            var names = new HashSet<string>();
            var groups = new Dictionary<string, int>();
            var partIds = new HashSet<string>(InjuryDefaults.BodyParts().Select(p => p.id));
            foreach (TraitPresets.Preset preset in presets)
            {
                Assert.AreEqual(DisabilityTraitPresets.Category, preset.category);
                Assert.IsTrue(names.Add(preset.name), $"two presets are called {preset.name}");
                Trait trait = preset.create();
                try
                {
                    Assert.Less(trait.cost, 0, $"{preset.name} is a drawback: it gives points");
                    StringAssert.Contains($"Gives {-trait.cost} point", preset.description, preset.name);
                    var errors = new List<string>();
                    var warnings = new List<string>();
                    trait.Validate(errors, warnings);
                    Assert.IsEmpty(errors, string.Join("\n", errors));
                    Assert.IsEmpty(warnings, string.Join("\n", warnings));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(trait.GetFormattedDescription()), preset.name);
                    foreach (TraitBehaviour behaviour in trait.behaviours)
                    {
                        Assert.IsFalse(behaviour.IsActive, $"{preset.name}: a disability is not something the player triggers");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(behaviour.Describe()), preset.name);
                        if (behaviour is MissingLimbTrait limb)
                            Assert.IsTrue(partIds.Contains(limb.part), $"{preset.name}: '{limb.part}' is not a default body part");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(trait);
                }
                if (!string.IsNullOrEmpty(preset.group))
                    groups[preset.group] = groups.TryGetValue(preset.group, out int n) ? n + 1 : 1;
            }
            foreach (KeyValuePair<string, int> group in groups)
                Assert.GreaterOrEqual(group.Value, 2, $"group '{group.Key}' has a single member");
            foreach (TraitPresets.Preset preset in presets)
                Assert.IsTrue(TraitPresets.All.Any(p => p.name == preset.name), $"{preset.name} is not offered by the preset menus");
        }

        [Test]
        public void Drawbacks_AreLimitedInNumberAndInPoints()
        {
            var made = new List<Trait>();
            Trait Make(string name, int cost)
            {
                Trait t = ScriptableObject.CreateInstance<Trait>();
                t.traitName = name;
                t.cost = cost;
                made.Add(t);
                return t;
            }
            try
            {
                Trait limp = Make("Limp", -1), deaf = Make("Deaf", -3), lame = Make("Lame", -5), tough = Make("Tough", 2);
                var picked = new List<Trait> { limp, deaf, tough };
                Assert.IsNull(TraitRules.WhyTooManyDrawbacks(lame, picked, 0, 0), "no limits");
                Assert.IsNull(TraitRules.WhyTooManyDrawbacks(lame, picked, 3, 9));
                Assert.IsNotNull(TraitRules.WhyTooManyDrawbacks(lame, picked, 2, 0), "a third drawback of two");
                Assert.IsNotNull(TraitRules.WhyTooManyDrawbacks(lame, picked, 5, 8), "1 + 3 + 5 points of 8");
                Assert.IsNull(TraitRules.WhyTooManyDrawbacks(tough, picked, 1, 1), "a trait that costs points is never a drawback");
                Assert.IsNull(TraitRules.WhyTooManyDrawbacks(deaf, picked, 2, 4), "the trait itself is not counted twice");
            }
            finally
            {
                foreach (Trait t in made)
                    UnityEngine.Object.DestroyImmediate(t);
            }
        }

        [Test]
        public void ACharacter_CannotDoWhatATraitBlocks_WithoutTheInjurySystem()
        {
            var go = new GameObject("Blocked");
            try
            {
                CombatEntity entity = go.AddComponent<CombatEntity>();
                Assert.IsTrue(entity.AllowsAction(BodyAction.Sprint));
                object key = new object();
                entity.SetActionBlock(key, BodyAction.Sprint | BodyAction.OffHand, "A bad leg");
                Assert.IsFalse(entity.AllowsAction(BodyAction.Sprint));
                Assert.IsTrue(entity.AllowsAction(BodyAction.Jump));
                Assert.AreEqual("A bad leg", entity.WhyNotAllowed(BodyAction.Sprint));
                Assert.AreEqual("", entity.WhyNotAllowed(BodyAction.Jump));
                Assert.IsFalse(entity.Hands.offHand);
                Assert.IsFalse(entity.Hands.twoHanded);
                entity.ClearActionBlock(key);
                Assert.IsTrue(entity.AllowsAction(BodyAction.Sprint));
                Assert.IsTrue(entity.Hands.IsFull);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        // [/unity-only]
    }
}
#endif
