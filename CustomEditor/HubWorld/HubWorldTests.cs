#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HubWorld.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) for the hub world's rules: the cycle clock, seeds and modifier
    /// rolls, the zone map, loot loss, duels, the jump's rules, the combat rule providers and the levelled world areas.
    /// Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class HubWorldTests
    {
        // ------------------------------------------------------------------ the cycle clock

        [Test]
        public void ClockRaisesWarningsInOrderThenEndsOnce()
        {
            var clock = new WorldCycleClock(600d, new[] { 60f, 300f, 10f }, 0d);
            var events = new List<CycleEvent>();
            for (double t = 0d; t < 599d; t += 1d)
                clock.Advance(t, events);
            Assert.AreEqual(3, events.Count, string.Join(", ", events));
            Assert.AreEqual(300f, events[0].SecondsLeft);
            Assert.AreEqual(60f, events[1].SecondsLeft);
            Assert.AreEqual(10f, events[2].SecondsLeft);
            events.Clear();
            clock.Advance(600d, events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CycleEventKind.Ended, events[0].Kind);
            Assert.AreEqual(0, events[0].CycleIndex);
            Assert.AreEqual(1, clock.CycleIndex);
            Assert.AreEqual(600d, clock.CycleStart, 1e-6, "the next cycle starts where the last one ended");
        }

        [Test]
        public void ClockKeepsCadenceAndSkipsLongGaps()
        {
            var clock = new WorldCycleClock(100d, new[] { 10f }, 0d);
            var events = new List<CycleEvent>();
            clock.Advance(100.5d, events);
            Assert.AreEqual(1, events.FindAll(e => e.Kind == CycleEventKind.Ended).Count);
            Assert.AreEqual(100d, clock.CycleStart, 1e-6);
            events.Clear();
            // Suspended for ten cycles: one jump, then a fresh cycle from now.
            clock.Advance(1250d, events);
            Assert.AreEqual(1, events.FindAll(e => e.Kind == CycleEventKind.Ended).Count);
            Assert.AreEqual(0, events.FindAll(e => e.Kind == CycleEventKind.Warning).Count, "stale warnings are not raised");
            Assert.AreEqual(1250d, clock.CycleStart, 1e-6);
        }

        [Test]
        public void ClockIgnoresWarningsLongerThanTheCycleAndSupportsSkipping()
        {
            var clock = new WorldCycleClock(120d, new[] { 600f, 30f }, 0d);
            var events = new List<CycleEvent>();
            clock.Advance(0d, events);
            Assert.AreEqual(0, events.Count, "a 10-minute warning in a 2-minute cycle never fires");
            clock.SetRemaining(45d, 10d);
            Assert.AreEqual(45d, clock.Remaining(10d), 1e-6);
            clock.Advance(26d, events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(30f, events[0].SecondsLeft);
            events.Clear();
            clock.EndNow(30d);
            clock.Advance(30d, events);
            Assert.AreEqual(CycleEventKind.Ended, events[events.Count - 1].Kind);
        }

        // ------------------------------------------------------------------ seeds and modifiers

        [Test]
        public void SeedsAreDeterministicNonZeroAndChangeEveryCycle()
        {
            var seen = new HashSet<int>();
            for (int cycle = 0; cycle < 200; cycle++)
            {
                int a = CycleSeeds.WorldSeed(12345, cycle), b = CycleSeeds.WorldSeed(12345, cycle);
                Assert.AreEqual(a, b);
                Assert.AreNotEqual(0, a);
                Assert.IsTrue(seen.Add(a), $"cycle {cycle} repeats a seed");
            }
            Assert.AreNotEqual(CycleSeeds.WorldSeed(1, 0), CycleSeeds.WorldSeed(2, 0), "different shards, different worlds");
        }

        [Test]
        public void WeightedPickRespectsWeightsAndNone()
        {
            var weights = new List<float> { 1f, 0f, 3f };
            int none = 0, first = 0, zero = 0, third = 0;
            for (int i = 0; i < 4000; i++)
            {
                int pick = CycleSeeds.PickWeighted(i, 7u, weights, 4f);
                if (pick < 0) none++;
                else if (pick == 0) first++;
                else if (pick == 1) zero++;
                else third++;
            }
            Assert.AreEqual(0, zero, "a weight of 0 is never picked");
            Assert.That(none, Is.InRange(1700, 2300), "none ~ 4/8");
            Assert.That(first, Is.InRange(350, 650), "first ~ 1/8");
            Assert.That(third, Is.InRange(1250, 1750), "third ~ 3/8");
            Assert.AreEqual(-1, CycleSeeds.PickWeighted(1, 1u, new List<float>(), 0f));
        }

        // ------------------------------------------------------------------ zones

        private static ZoneMap Map() => new ZoneMap(new Vector2(100f, 100f), 80f, new[]
        {
            new ZoneBand("Far", 3000f, true, 2f, 3f, 1.5f, 2f, 1.5f, Color.red),
            new ZoneBand("Safe", 600f, false, 1f, 1.2f, 0.8f, 1f, 0.8f, Color.green),
        }, 0.5f, 6f);

        [Test]
        public void ZonesFollowDistanceFromTheCity()
        {
            ZoneMap map = Map();
            Assert.AreEqual("Safe", map.Bands[0].name, "rings are sorted by radius");
            Assert.IsTrue(map.At(new Vector3(120f, 0f, 100f)).InsideCity);
            ZoneInfo safe = map.At(new Vector3(100f + 300f, 50f, 100f));
            Assert.AreEqual(0, safe.index);
            Assert.IsFalse(safe.pvp);
            ZoneInfo far = map.At(new Vector3(100f, 0f, 100f + 1000f));
            Assert.AreEqual(1, far.index);
            Assert.IsTrue(far.pvp);
            Assert.AreEqual(1, map.IndexAtDistance(100000f), "the last ring goes on forever");
        }

        [Test]
        public void DifficultyGrowsWithDistanceAndIsCapped()
        {
            ZoneMap map = Map();
            float previous = 0f;
            for (float d = 80f; d < 20000f; d += 50f)
            {
                float diff = map.AtDistance(d).dungeonDifficulty;
                Assert.GreaterOrEqual(diff + 1e-4f, previous, $"difficulty dropped at {d} m");
                previous = diff;
            }
            Assert.AreEqual(1f, map.AtDistance(80f).dungeonDifficulty, 1e-4f);
            Assert.AreEqual(1.2f, map.AtDistance(599.9f).dungeonDifficulty, 1e-2f);
            Assert.AreEqual(3.5f, map.AtDistance(4000f).dungeonDifficulty, 1e-3f, "3 + 1 km * 0.5");
            Assert.AreEqual(6f, map.AtDistance(50000f).dungeonDifficulty, 1e-4f, "capped");
            Assert.Greater(map.AtDistance(2500f).mobStrength, map.AtDistance(300f).mobStrength);
        }

        // ------------------------------------------------------------------ loot loss

        [Test]
        public void LootLossKeepsProtectedStacksAndRespectsBounds()
        {
            var stacks = new List<LootStack> { new LootStack(0, 10), new LootStack(1, 5, true), new LootStack(2, 0), new LootStack(3, 1) };
            int[] none = LootLossRule.Plan(stacks, 0f, new System.Random(1));
            Assert.AreEqual(0, LootLossRule.Total(none));
            int[] all = LootLossRule.Plan(stacks, 1f, new System.Random(1));
            CollectionAssert.AreEqual(new[] { 10, 0, 0, 1 }, all);
            for (int seed = 0; seed < 50; seed++)
            {
                int[] plan = LootLossRule.Plan(stacks, 0.37f, new System.Random(seed));
                Assert.AreEqual(0, plan[1], "protected");
                Assert.That(plan[0], Is.InRange(3, 4), "10 x 0.37 = 3.7");
                Assert.That(plan[3], Is.InRange(0, 1));
            }
            CollectionAssert.AreEqual(LootLossRule.Plan(stacks, 0.37f, new System.Random(9)), LootLossRule.Plan(stacks, 0.37f, new System.Random(9)), "deterministic");
        }

        [Test]
        public void LootLossAveragesTheFraction()
        {
            var single = new List<LootStack> { new LootStack(0, 1) };
            var random = new System.Random(42);
            int lost = 0;
            for (int i = 0; i < 4000; i++)
                lost += LootLossRule.Plan(single, 0.25f, random)[0];
            Assert.That(lost, Is.InRange(850, 1150), "a single item is lost a quarter of the time");
            Assert.AreEqual(25L, LootLossRule.CurrencyLoss(100, 0.25f));
            Assert.AreEqual(0L, LootLossRule.CurrencyLoss(-5, 0.5f));
        }

        // ------------------------------------------------------------------ duels

        [Test]
        public void DuelGoesFromChallengeToFightToDefeat()
        {
            var book = new DuelBook { ChallengeTimeout = 30d, Countdown = 3d, MaxDuration = 100d };
            var changes = new List<DuelState>();
            book.Changed += d => changes.Add(d.State);
            Assert.IsNull(book.Challenge(1, 1, 0d, out string error));
            Assert.IsNotNull(error);
            Duel duel = book.Challenge(1, 2, 0d, out error);
            Assert.IsNotNull(duel, error);
            Assert.IsNull(book.Challenge(3, 2, 0d, out _), "the challenged player is busy");
            Assert.IsNull(book.Challenge(1, 3, 0d, out _), "the challenger is busy");
            Assert.IsFalse(book.Accept(1, 1d), "only the challenged player accepts");
            Assert.IsTrue(book.Accept(2, 1d));
            Assert.IsFalse(book.AreFighting(1, 2), "not during the countdown");
            book.Update(3.9d);
            Assert.AreEqual(DuelState.Countdown, duel.State);
            book.Update(4d);
            Assert.AreEqual(DuelState.Fighting, duel.State);
            Assert.IsTrue(book.AreFighting(1, 2));
            Assert.IsTrue(book.AreFighting(2, 1));
            Assert.IsFalse(book.AreFighting(1, 3));
            Assert.IsTrue(book.End(2, DuelEndReason.Defeat, true));
            Assert.AreEqual(DuelState.Ended, duel.State);
            Assert.AreEqual(1, duel.Winner);
            Assert.AreEqual(DuelEndReason.Defeat, duel.Reason);
            Assert.IsNull(book.Of(1), "ended duels are removed");
            CollectionAssert.AreEqual(new[] { DuelState.Challenged, DuelState.Countdown, DuelState.Fighting, DuelState.Ended }, changes);
        }

        [Test]
        public void DuelChallengesExpireOrAreDeclinedAndFightsTimeOut()
        {
            var book = new DuelBook { ChallengeTimeout = 10d, Countdown = 0d, MaxDuration = 20d };
            Duel expired = book.Challenge(1, 2, 0d, out _);
            book.Update(10d);
            Assert.AreEqual(DuelEndReason.Expired, expired.Reason);
            Duel declined = book.Challenge(1, 2, 20d, out _);
            Assert.IsTrue(book.Decline(2));
            Assert.AreEqual(DuelEndReason.Declined, declined.Reason);
            Duel draw = book.Challenge(1, 2, 30d, out _);
            Assert.IsTrue(book.Accept(2, 30d));
            Assert.AreEqual(DuelState.Fighting, draw.State, "no countdown");
            book.Update(50d);
            Assert.AreEqual(DuelEndReason.TimeOut, draw.Reason);
            Assert.AreEqual(-1, draw.Winner, "a draw");
        }

        // ------------------------------------------------------------------ the jump

        [Test]
        public void JumpRulesSendTheRightPlayersHome()
        {
            Assert.IsTrue(CycleRules.LostAtJump(RealmKind.World));
            Assert.IsFalse(CycleRules.LostAtJump(RealmKind.City));
            Assert.IsFalse(CycleRules.LostAtJump(RealmKind.Dungeon), "dungeon runs carry on");
            Assert.IsFalse(CycleRules.LostAtJump(RealmKind.Transit), "still at the gate");
            Assert.IsTrue(CycleRules.ShelteredAtJump(RealmKind.Dungeon));
            Assert.AreEqual(RealmKind.World, CycleRules.DungeonExitDestination(4, 4));
            Assert.AreEqual(RealmKind.City, CycleRules.DungeonExitDestination(3, 4));
            Assert.IsTrue(CycleRules.PenalisedOnDungeonExit(3, 4));
            Assert.IsFalse(CycleRules.PenalisedOnDungeonExit(4, 4));
            Assert.IsTrue(CycleRules.GateOpen(false, 100d, 30f));
            Assert.IsFalse(CycleRules.GateOpen(false, 20d, 30f), "sealed just before the jump");
            Assert.IsFalse(CycleRules.GateOpen(true, 1000d, 30f), "sealed while the world is replaced");
            Assert.IsTrue(CycleRules.GateOpen(false, 1d, 0f));
        }

        // ------------------------------------------------------------------ combat rules

        private sealed class FixedRule : ICombatRuleProvider
        {
            public int Priority { get; set; }
            public PvpVerdict Verdict;
            public bool? VisualOnly;
            public PvpVerdict PlayerVersusPlayer(CombatEntity a, CombatEntity b) => Verdict;
            public bool? AbilitiesVisualOnly(CombatEntity caster) => VisualOnly;
        }

        [Test]
        public void CombatRuleProvidersAreAskedByPriority()
        {
            var a = new GameObject("A").AddComponent<CombatEntity>();
            var b = new GameObject("B").AddComponent<CombatEntity>();
            var city = new FixedRule { Priority = 0, Verdict = PvpVerdict.Forbidden, VisualOnly = true };
            var duel = new FixedRule { Priority = 100, Verdict = PvpVerdict.Opponents, VisualOnly = null };
            try
            {
                Assert.AreEqual(PvpVerdict.Unspecified, CombatRules.PlayerVersusPlayer(a, b), "no providers");
                CombatRules.Register(city);
                Assert.AreEqual(PvpVerdict.Forbidden, CombatRules.PlayerVersusPlayer(a, b));
                CombatRules.Register(duel);
                Assert.AreEqual(PvpVerdict.Opponents, CombatRules.PlayerVersusPlayer(a, b), "the higher priority wins");
                Assert.IsTrue(CombatRules.IsVisualOnly(a), "no opinion falls through to the next provider");
                duel.Verdict = PvpVerdict.Unspecified;
                duel.VisualOnly = false;
                Assert.AreEqual(PvpVerdict.Forbidden, CombatRules.PlayerVersusPlayer(a, b));
                Assert.IsFalse(CombatRules.IsVisualOnly(a));
                Assert.AreEqual(PvpVerdict.Unspecified, CombatRules.PlayerVersusPlayer(a, a), "never against yourself");
                Assert.AreEqual(PvpVerdict.Unspecified, CombatRules.Between(a, b), "neither is a player");
                Assert.IsTrue(CombatRules.MayHarm(a, b), "rules only apply between players");
            }
            finally
            {
                CombatRules.Unregister(city);
                CombatRules.Unregister(duel);
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
            Assert.IsFalse(CombatRules.HasProviders);
        }

        // ------------------------------------------------------------------ world areas

        [Test]
        public void LevelledAreasFlattenAndBlend()
        {
            try
            {
                WorldAreas.Set(new[] { new WorldArea(Vector2.zero, 50f, 20f, 10f, WorldAreaRules.Everything) });
                Assert.AreEqual(10f, WorldAreas.Flatten(30f, 0f, 99f), 1e-4f, "inside: levelled");
                Assert.AreEqual(99f, WorldAreas.Flatten(80f, 0f, 99f), 1e-4f, "outside the blend: untouched");
                float mid = WorldAreas.Flatten(60f, 0f, 99f);
                Assert.That(mid, Is.InRange(10f, 99f), "the blend ring is in between");
                Assert.AreEqual(10f, WorldAreas.Flatten(50.01f, 0f, 99f), 0.1f, "continuous at the edge");
                Assert.IsTrue(WorldAreas.Has(WorldAreaRules.NoMobs, 10f, 10f));
                Assert.IsFalse(WorldAreas.Has(WorldAreaRules.NoMobs, 60f, 0f), "the blend ring is not kept clear");
                Assert.IsTrue(WorldAreas.Within(WorldAreaRules.NoWater, 60f, 0f), "water is kept out of the blend ring too");
                Assert.IsFalse(WorldAreas.Within(WorldAreaRules.NoWater, 71f, 0f));
                Assert.IsTrue(WorldAreas.Touches(WorldAreaRules.NoWater, new Vector2(100f, 0f), 31f), "a lake reaching the ring");
                Assert.IsFalse(WorldAreas.Touches(WorldAreaRules.NoWater, new Vector2(100f, 0f), 29f));

                var heights = new float[4, 4];
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        heights[x, y] = 50f;
                WorldAreas.Flatten(heights, new Vector2(-2f, -2f));
                Assert.AreEqual(10f, heights[2, 2], 1e-4f);
                WorldAreas.Flatten(heights = new float[2, 2] { { 7f, 7f }, { 7f, 7f } }, new Vector2(1000f, 1000f));
                Assert.AreEqual(7f, heights[0, 0], "far blocks are skipped");
            }
            finally
            {
                WorldAreas.Clear();
            }
        }

        [Test]
        public void PhysicsQueriesUseTheDefaultSceneWithoutRegions()
        {
            Assert.AreEqual(0, ScenePhysics.RegionCount);
            Assert.IsTrue(ScenePhysics.At(new Vector3(0f, -4096f, 0f)) == Physics.defaultPhysicsScene);
        }
    }
}
#endif
