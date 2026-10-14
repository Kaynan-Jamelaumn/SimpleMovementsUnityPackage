#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using EState = MobMovementStateMachine.EMobMovementState;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the pure parts of the mob networking: the replicated AI state, the speed estimate that drives
    /// client copies' animations, the realm rules the AI and the chunk spawners use, the AI rate tiers and the offline
    /// behaviour of the spawn helper.
    /// </summary>
    public class MobNetTests
    {
        private Func<CombatEntity, CombatEntity, bool> savedSeparation;
        private Func<Scene, Scene, bool> savedSceneSeparation;
        private Func<GameObject, bool> savedIdentity;

        [SetUp]
        public void SaveStatics()
        {
            savedSeparation = CombatRules.Separation;
            savedSceneSeparation = MobRealms.SceneSeparation;
            savedIdentity = MobNetSpawn.NetworkIdentityResolver;
        }

        [TearDown]
        public void RestoreStatics()
        {
            CombatRules.Separation = savedSeparation;
            MobRealms.SceneSeparation = savedSceneSeparation;
            MobNetSpawn.NetworkIdentityResolver = savedIdentity;
        }

        // ------------------------------------------------------------------ replicated state

        [Test]
        public void SpeedsAreRoundedAndBadValuesBecomeZero()
        {
            Assert.AreEqual(0f, MobReplicaState.Quantize(-3f));
            Assert.AreEqual(0f, MobReplicaState.Quantize(float.NaN));
            Assert.AreEqual(0f, MobReplicaState.Quantize(float.PositiveInfinity));
            Assert.AreEqual(3.5f, MobReplicaState.Quantize(3.51f), 1e-4f);
            Assert.AreEqual(3.55f, MobReplicaState.Quantize(3.53f), 1e-4f);
            Assert.AreEqual(1000f, MobReplicaState.Quantize(1e9f), 1e-3f);
        }

        [Test]
        public void StateCarriesTheAiStateFlagsAndSpeeds()
        {
            MobReplicaState s = MobReplicaState.Create(EState.Chasing, MobReplicaFlags.InCombat | MobReplicaFlags.Stunned, 2.01f, 6.02f);
            Assert.AreEqual(EState.Chasing, s.State);
            Assert.IsTrue(s.Has(MobReplicaFlags.InCombat));
            Assert.IsTrue(s.Has(MobReplicaFlags.Stunned));
            Assert.IsFalse(s.Has(MobReplicaFlags.Dead));
            Assert.AreEqual(MobReplicaFlags.InCombat | MobReplicaFlags.Stunned, s.Flags);
            Assert.AreEqual(2f, s.walkSpeed, 1e-4f);
            Assert.AreEqual(6f, s.runSpeed, 1e-4f);
        }

        [Test]
        public void TinySpeedChangesDoNotMakeANewState()
        {
            MobReplicaState a = MobReplicaState.Create(EState.Moving, MobReplicaFlags.None, 2.001f, 6f);
            MobReplicaState b = MobReplicaState.Create(EState.Moving, MobReplicaFlags.None, 2.004f, 6f);
            MobReplicaState c = MobReplicaState.Create(EState.Combat, MobReplicaFlags.None, 2.001f, 6f);
            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
            Assert.IsTrue(a != c);
        }

        [Test]
        public void DefaultStateIsIdleWithNoFlags()
        {
            MobReplicaState s = default;
            Assert.AreEqual(EState.Idle, s.State);
            Assert.AreEqual(MobReplicaFlags.None, s.Flags);
        }

        // ------------------------------------------------------------------ speed estimate (client copies)

        [Test]
        public void EstimateConvergesToASteadySpeed()
        {
            var m = new MobMotionEstimator();
            m.Reset(Vector3.zero);
            Vector3 p = Vector3.zero;
            Vector3 v = Vector3.zero;
            for (int i = 0; i < 120; i++)
            {
                p += new Vector3(3f, 0f, 0f) / 60f;
                v = m.Step(p, 1f / 60f);
            }
            Assert.AreEqual(3f, v.x, 0.05f);
            Assert.AreEqual(0f, v.z, 1e-3f);
        }

        [Test]
        public void TeleportsResetTheEstimate()
        {
            var m = new MobMotionEstimator();
            m.Reset(Vector3.zero);
            m.Step(new Vector3(0.05f, 0f, 0f), 1f / 60f);
            Vector3 v = m.Step(new Vector3(100f, 0f, 0f), 1f / 60f);
            Assert.AreEqual(Vector3.zero, v);
            // The next normal step measures from the new place, not from before the jump.
            v = m.Step(new Vector3(100.05f, 0f, 0f), 1f / 60f);
            Assert.Less(v.x, 3.1f);
            Assert.Greater(v.x, 0f);
        }

        [Test]
        public void BadInputKeepsThePreviousEstimate()
        {
            var m = new MobMotionEstimator();
            m.Reset(Vector3.zero);
            Vector3 before = m.Step(new Vector3(0.05f, 0f, 0f), 1f / 60f);
            Assert.AreEqual(before, m.Step(new Vector3(float.NaN, 0f, 0f), 1f / 60f));
            Assert.AreEqual(before, m.Step(new Vector3(0.1f, 0f, 0f), 0f));
            Assert.AreEqual(before, m.Step(new Vector3(0.15f, 0f, 0f), float.NaN));
        }

        [Test]
        public void FirstStepWithoutResetIsStill()
        {
            var m = new MobMotionEstimator();
            Assert.AreEqual(Vector3.zero, m.Step(new Vector3(5f, 0f, 5f), 0.016f));
        }

        // ------------------------------------------------------------------ realms

        [Test]
        public void WithoutRealmRulesNothingIsSeparated()
        {
            CombatRules.Separation = null;
            MobRealms.SceneSeparation = null;
            Scene active = SceneManager.GetActiveScene();
            Assert.IsFalse(MobRealms.Active);
            Assert.IsFalse(MobRealms.AreScenesSeparated(active, default));
            var go = new GameObject("realm probe");
            try
            {
                Assert.IsTrue(MobRealms.IsInRealm(active, go));
                Assert.IsFalse(MobRealms.IsInRealm(active, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheSameOrAnInvalidSceneIsNeverSeparated()
        {
            CombatRules.Separation = (a, b) => true;
            MobRealms.SceneSeparation = (a, b) => true;
            Scene active = SceneManager.GetActiveScene();
            Assert.IsTrue(MobRealms.Active);
            Assert.IsFalse(MobRealms.AreScenesSeparated(active, active));
            Assert.IsFalse(MobRealms.AreScenesSeparated(active, default));
            Assert.IsFalse(MobRealms.AreScenesSeparated(default, default));
        }

        [Test]
        public void NearestPlayerWithoutAMobMatchesTheCombatQuery()
        {
            CombatRules.Separation = (a, b) => true; // ignored without a mob
            Vector3 p = new Vector3(12f, 0f, -4f);
            CombatEntity a = MobRealms.NearestPlayer(null, p, out float da);
            CombatEntity b = CombatEntity.NearestPlayer(p, out float db);
            Assert.AreSame(b, a);
            Assert.AreEqual(db, da);
        }

        [Test]
        public void NullIsNeverInteractedWith()
        {
            CombatRules.Separation = null;
            Assert.IsFalse(MobRealms.CanInteract(null, null));
        }

        // ------------------------------------------------------------------ AI rate tiers

        [Test]
        public void TiersFollowTheDistanceToTheNearestPlayer()
        {
            var s = ScriptableObject.CreateInstance<CombatSettings>();
            try
            {
                s.aiNearDistance = 10f;
                s.aiFarDistance = 20f;
                s.aiSleepDistance = 30f;
                Assert.AreEqual(MobAIScheduler.Tier.Near, MobAIScheduler.TierFor(5f, s));
                Assert.AreEqual(MobAIScheduler.Tier.Near, MobAIScheduler.TierFor(10f, s));
                Assert.AreEqual(MobAIScheduler.Tier.Mid, MobAIScheduler.TierFor(15f, s));
                Assert.AreEqual(MobAIScheduler.Tier.Far, MobAIScheduler.TierFor(25f, s));
                Assert.AreEqual(MobAIScheduler.Tier.Sleep, MobAIScheduler.TierFor(31f, s));
                Assert.AreEqual(MobAIScheduler.Tier.Sleep, MobAIScheduler.TierFor(float.MaxValue, s));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(s);
            }
        }

        // ------------------------------------------------------------------ spawn helper offline

        [Test]
        public void OfflineSpawnChangesNothing()
        {
            Assume.That(NetAuthority.Mode, Is.EqualTo(NetMode.Offline));
            var parent = new GameObject("chunk");
            var mob = new GameObject("mob");
            try
            {
                mob.transform.SetParent(parent.transform, false);
                mob.transform.localPosition = new Vector3(1f, 2f, 3f);
                Assert.IsFalse(MobNetSpawn.WillNetworkSpawn(mob));
                Assert.IsFalse(MobNetSpawn.Spawn(mob, SceneManager.GetActiveScene()));
                Assert.AreSame(parent.transform, mob.transform.parent);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), mob.transform.localPosition);
                Assert.IsFalse(MobNetSpawn.Spawn(null, default(Scene)));
                Assert.IsFalse(MobNetSpawn.Spawn(mob, (Component)null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void NetworkIdentityComesFromTheInstalledResolver()
        {
            var go = new GameObject("mob");
            try
            {
                MobNetSpawn.NetworkIdentityResolver = null;
                Assert.IsFalse(MobNetSpawn.HasNetworkIdentity(go));
                MobNetSpawn.NetworkIdentityResolver = g => g == go;
                Assert.IsTrue(MobNetSpawn.HasNetworkIdentity(go));
                Assert.IsFalse(MobNetSpawn.HasNetworkIdentity(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
