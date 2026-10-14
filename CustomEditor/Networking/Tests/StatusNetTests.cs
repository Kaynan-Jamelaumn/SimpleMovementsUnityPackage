#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the status replication: the pure helpers (rounding, tick conversion, flags, list difference,
    /// request validation) and the replica switches of the always-compiled status code (managers, speed, combat entity,
    /// experience, timed buffs) - what a client's copy of a networked character accepts and ignores.
    /// </summary>
    public class StatusNetTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        private T Make<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            created.Add(go);
            return go.AddComponent<T>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Object.DestroyImmediate(go);
            created.Clear();
        }

        // ------------------------------------------------------------------ helpers

        [Test]
        public void QuantizeRoundsButNeverMakesTheLivingLookDead()
        {
            Assert.AreEqual(12.3f, StatusReplication.Quantize(12.34f), 1e-4f);
            Assert.AreEqual(12.4f, StatusReplication.Quantize(12.36f), 1e-4f);
            Assert.AreEqual(0.1f, StatusReplication.Quantize(0.04f), 1e-4f, "a positive value never rounds to 0");
            Assert.AreEqual(0f, StatusReplication.Quantize(0f));
            Assert.AreEqual(0f, StatusReplication.Quantize(float.NaN));
            Assert.AreEqual(0f, StatusReplication.Quantize(float.PositiveInfinity));
            Assert.AreEqual(-5f, StatusReplication.Quantize(-5.02f), 1e-4f);
        }

        [Test]
        public void EndTicksRoundUpAndConvertBack()
        {
            const double dt = 1d / 50d;
            Assert.AreEqual(0u, StatusReplication.EndTick(9f, 10f, 1000, dt), "an expired timer is not running");
            Assert.AreEqual(0u, StatusReplication.EndTick(0f, 10f, 1000, dt));
            Assert.AreEqual(1100u, StatusReplication.EndTick(12f, 10f, 1000, dt), "2 s at 50 Hz");
            Assert.AreEqual(1001u, StatusReplication.EndTick(10.001f, 10f, 1000, dt), "never ends earlier than on the server");
            Assert.AreEqual(uint.MaxValue, StatusReplication.EndTick(float.PositiveInfinity, 10f, 1000, dt));

            // A client whose clock reads 50 and whose tick is 1050 sees the stun end 1 s later.
            Assert.AreEqual(51f, StatusReplication.LocalUntil(1100, 1050, dt, 50f), 1e-4f);
            Assert.AreEqual(0f, StatusReplication.LocalUntil(1100, 1100, dt, 50f), "over");
            Assert.AreEqual(0f, StatusReplication.LocalUntil(0, 1050, dt, 50f), "not running");
            Assert.AreEqual(1f, StatusReplication.SecondsLeft(1100, 1050, dt), 1e-4f);
            Assert.AreEqual(0f, StatusReplication.SecondsLeft(1000, 1050, dt));
        }

        [Test]
        public void SurvivalAndHitFlagsRoundTrip()
        {
            byte packed = StatusReplication.PackSurvival(true, false, true, false, true, true);
            Assert.IsTrue(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.Sleeping));
            Assert.IsFalse(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.Collapsed));
            Assert.IsTrue(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.Underwater));
            Assert.IsFalse(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.HighAltitude));
            Assert.IsTrue(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.PoorVentilation));
            Assert.IsTrue(StatusReplication.Has(packed, StatusReplication.SurvivalFlags.HasOxygenTank));

            var hit = new DamageInfo { isCritical = true, parried = true, isReflected = true };
            byte flags = StatusReplication.PackHit(hit);
            DamageInfo back = default;
            StatusReplication.UnpackHit(flags, ref back);
            Assert.IsTrue(back.isCritical);
            Assert.IsFalse(back.isPeriodic);
            Assert.IsFalse(back.blocked);
            Assert.IsTrue(back.parried);
            Assert.IsTrue(back.isReflected);
        }

        [Test]
        public void DiffTurnsTheLocalListIntoTheServerList()
        {
            var add = new List<string>();
            var remove = new List<string>();
            StatusReplication.Diff(new[] { "Glide", "Tough", "Tough", null }, new[] { "Tough", "Double Jump", "", "Double Jump" }, add, remove);
            CollectionAssert.AreEqual(new[] { "Double Jump" }, add);
            CollectionAssert.AreEqual(new[] { "Glide" }, remove);

            StatusReplication.Diff(null, new[] { "A" }, add, remove);
            CollectionAssert.AreEqual(new[] { "A" }, add);
            Assert.AreEqual(0, remove.Count);

            Assert.IsTrue(StatusReplication.SameSequence(new[] { "a", "b" }, new List<string> { "a", "b" }));
            Assert.IsFalse(StatusReplication.SameSequence(new[] { "a", "b" }, new[] { "b", "a" }));
            Assert.IsTrue(StatusReplication.SameSequence(null, new string[0]));
        }

        [Test]
        public void StatRequestsAreValidatedAndRateLimited()
        {
            var stats = new List<string> { "health", "stamina" };
            Assert.AreEqual("health", StatusReplication.ValidStatName("HEALTH", stats));
            Assert.IsNull(StatusReplication.ValidStatName("gold", stats));
            Assert.IsNull(StatusReplication.ValidStatName(null, stats));
            Assert.IsNull(StatusReplication.ValidStatName(new string('h', StatusReplication.MaxNameLength + 1), stats));

            float last = -1000f;
            Assert.IsTrue(StatusReplication.Allow(ref last, 10f, 0.25f));
            Assert.IsFalse(StatusReplication.Allow(ref last, 10.1f, 0.25f));
            Assert.IsTrue(StatusReplication.Allow(ref last, 10.3f, 0.25f));
        }

        // ------------------------------------------------------------------ replica switches

        [Test]
        public void ReplicaManagerIgnoresGameplayWritesAndTakesServerValues()
        {
            HealthManager hp = Make<HealthManager>();
            hp.SetReplica(true);
            Assert.IsTrue(hp.IsReplica);

            hp.ApplyReplicated(150f, 200f);
            Assert.AreEqual(200f, hp.MaxValue);
            Assert.AreEqual(150f, hp.CurrentValue, "max is applied first, so the clamp keeps the value");

            hp.ConsumeHP(40f);
            hp.AddCurrentValue(40f);
            hp.MaxValue = 10f;
            hp.ModifyMaxValue(-50f);
            Assert.AreEqual(150f, hp.CurrentValue);
            Assert.AreEqual(200f, hp.MaxValue);

            hp.ApplyReplicated(float.NaN, 200f);
            Assert.AreEqual(150f, hp.CurrentValue, "non-finite values are ignored");

            hp.SetReplica(false);
            hp.ConsumeHP(40f);
            Assert.Less(hp.CurrentValue, 150f, "the simulation owns it again");
        }

        [Test]
        public void WeightMayStayAboveItsMaxWhenReplicated()
        {
            WeightManager w = Make<WeightManager>();
            w.SetReplica(true);
            w.ApplyReplicated(130f, 100f);
            Assert.AreEqual(130f, w.CurrentValue, "an overloaded character stays overloaded");
            w.AddWeight(10f);
            Assert.AreEqual(130f, w.CurrentValue);
        }

        [Test]
        public void ReplicatedLimitsKeepTheValueSimulated()
        {
            ManaManager m = Make<ManaManager>();
            m.ApplyReplicated(50f, 100f);
            m.SetReplicatedLimits(true);
            Assert.IsFalse(m.IsReplica);

            m.ModifyMaxValue(500f);
            m.MaxValue = 1f;
            Assert.AreEqual(100f, m.MaxValue, "limits come from the server");

            m.ApplyReplicatedLimits(40f, 2f, 0f, 0f);
            Assert.AreEqual(40f, m.MaxValue);
            Assert.AreEqual(40f, m.CurrentValue, "the value is kept within the new max");

            m.ConsumeMana(10f);
            Assert.AreEqual(30f, m.CurrentValue, 1e-4f, "the current value is still simulated here");
        }

        [Test]
        public void ReplicaSpeedOnlyTakesTheServersSpeed()
        {
            SpeedManager s = Make<SpeedManager>();
            s.ApplyReplicated(9f, 9f, 2f, 0.5f);
            Assert.AreNotEqual(9f, s.Speed, "ignored where the rules run");

            s.SetReplica(true);
            s.ApplyReplicated(3f, 6f, 1.5f, 0.4f);
            s.ModifySpeed(-2f);
            s.SetSpeed(100f);
            s.BaseSpeed = 1f;
            s.ResetToBaseSpeed();
            Assert.AreEqual(3f, s.Speed);
            Assert.AreEqual(6f, s.BaseSpeed);
            Assert.AreEqual(1.5f, s.SpeedWhileRunningMultiplier);
            Assert.AreEqual(0.4f, s.SpeedWhileCrouchingMultiplier);
        }

        [Test]
        public void ReplicaEntityShowsDeathAndHitsWithoutGameplayEvents()
        {
            CombatEntity e = Make<CombatEntity>();
            e.SetReplica(true);

            int died = 0, damaged = 0, replicatedHits = 0, feedback = 0;
            bool? life = null;
            e.Died += _ => died++;
            e.Damaged += _ => damaged++;
            e.ReplicatedDamage += _ => replicatedHits++;
            e.DamageFeedback += _ => feedback++;
            e.ReplicatedLifeChanged += d => life = d;

            Assert.AreEqual(0f, e.ApplyDamage(new DamageInfo { amount = 50f }));
            Assert.IsFalse(e.ApplyControl(ControlType.Stun, 3f, null));

            e.PresentReplicatedDamage(new DamageInfo { amount = 12f, isCritical = true });
            Assert.AreEqual(1, replicatedHits);
            Assert.AreEqual(1, feedback);
            Assert.AreEqual(0, damaged, "no gameplay damage event on a replica");

            e.SetReplicatedDeath(true);
            Assert.IsTrue(e.IsDead);
            Assert.AreEqual(true, life);
            Assert.AreEqual(0, died, "Died never fires on a replica");

            e.Revive(1f);
            Assert.IsTrue(e.IsDead, "only the server revives");
            e.SetReplicatedDeath(false);
            Assert.IsFalse(e.IsDead);
            Assert.AreEqual(false, life);
        }

        [Test]
        public void ReplicaEntityTakesTheServersControlTimers()
        {
            CombatEntity e = Make<CombatEntity>();
            var t = new CombatEntity.ControlTimers { stunUntil = Time.time + 5f, slowUntil = Time.time + 2f, slowFraction = 2f };
            e.SetReplicatedControl(t);
            Assert.IsFalse(e.GetControlTimers().stunUntil > 0f, "ignored where the rules run");

            e.SetReplica(true);
            e.SetReplicatedControl(t);
            CombatEntity.ControlTimers got = e.GetControlTimers();
            Assert.AreEqual(t.stunUntil, got.stunUntil);
            Assert.AreEqual(0.95f, got.slowFraction, 1e-4f, "the slow is clamped");
            Assert.IsTrue(e.IsStunned);
        }

        [Test]
        public void ExperienceReplicationRaisesLevelUpOnce()
        {
            ExperienceManager xp = Make<ExperienceManager>();
            int levelUps = 0;
            xp.OnLevelUp += _ => levelUps++;

            xp.ApplyReplicated(3, 120, 1050, 2, 1, 4);
            Assert.AreEqual(3, xp.CurrentLevel);
            Assert.AreEqual(120, xp.CurrentExperience);
            Assert.AreEqual(1050, xp.ExperienceToNextLevel);
            Assert.AreEqual(4, xp.StatUpgradePoints);
            Assert.AreEqual(1, levelUps);

            xp.ApplyReplicated(3, 200, 1050, 2, 1, 4);
            Assert.AreEqual(1, levelUps, "same level: no level-up");

            xp.ApplyReplicated(0, -1, 0, 0, 0, 0);
            Assert.AreEqual(3, xp.CurrentLevel, "invalid values are ignored");
        }

        [Test]
        public void TimedModifiersShowTheServersBuffsAsDisplayOnly()
        {
            TimedStatModifiers timed = Make<TimedStatModifiers>();
            int changed = 0;
            timed.Changed += () => changed++;
            timed.SetReplicatedView(new List<TimedStatModifiers.View>
            {
                new TimedStatModifiers.View { name = "Haste", debuff = false, element = ElementType.Wind, duration = 10f, remaining = 4f },
                new TimedStatModifiers.View { name = "Burn", debuff = true, element = ElementType.Fire, duration = 5f, remaining = 0f },
                new TimedStatModifiers.View { name = null, debuff = true, element = ElementType.Dark, duration = float.NaN, remaining = 2f },
            });
            Assert.AreEqual(1, changed);
            Assert.AreEqual(2, timed.All.Count, "expired entries are skipped");
            Assert.AreEqual("Haste", timed.All[0].name);
            Assert.AreEqual(10f, timed.All[0].duration);
            Assert.AreEqual(4f, timed.All[0].Remaining, 0.05f);
            Assert.AreEqual("Debuff", timed.All[1].name);
            Assert.AreEqual(2f, timed.All[1].duration, 1e-4f);

            timed.SetReplicatedView(null);
            Assert.AreEqual(0, timed.All.Count);
        }
    }
}
#endif
