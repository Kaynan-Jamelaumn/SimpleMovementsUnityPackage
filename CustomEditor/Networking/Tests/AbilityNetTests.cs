#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled helpers of networked abilities: validation of client cast requests,
    /// timer conversion between machines, the seeded cosmetic randomness, rate limiting and slot state transfer.
    /// </summary>
    public class AbilityNetTests
    {
        // ------------------------------------------------------------------ request validation

        [Test]
        public void SanitizeRejectsNaNAndInfiniteVectors()
        {
            var r = new CastRequest { point = new Vector3(float.NaN, 0f, 0f), hasPoint = true };
            Assert.IsFalse(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f));

            r = new CastRequest { direction = new Vector3(0f, float.PositiveInfinity, 1f), hasDirection = true };
            Assert.IsFalse(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f));

            r = new CastRequest { point = new Vector3(1e9f, 0f, 0f), hasPoint = true };
            Assert.IsFalse(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f), "absurd coordinates are garbage");
        }

        [Test]
        public void SanitizeIgnoresVectorsThatAreNotUsed()
        {
            // A NaN in a field the request does not use is not an error, but it is cleared.
            var r = new CastRequest { point = new Vector3(float.NaN, 0f, 0f), hasPoint = false, direction = Vector3.forward, hasDirection = true };
            Assert.IsTrue(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f));
            Assert.AreEqual(Vector3.zero, r.point);
        }

        [Test]
        public void SanitizeNormalisesDirectionAndDropsZeroDirection()
        {
            var r = new CastRequest { direction = new Vector3(0f, 0f, 5f), hasDirection = true };
            Assert.IsTrue(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f));
            Assert.That(r.direction.magnitude, Is.EqualTo(1f).Within(1e-5f));

            r = new CastRequest { direction = Vector3.zero, hasDirection = true };
            Assert.IsTrue(AbilityNetMath.SanitizeRequest(ref r, Vector3.zero, 100f));
            Assert.IsFalse(r.hasDirection);
        }

        [Test]
        public void SanitizeClampsFarPoints()
        {
            Vector3 origin = new Vector3(10f, 0f, 10f);
            var r = new CastRequest { point = origin + new Vector3(0f, 0f, 500f), hasPoint = true };
            Assert.IsTrue(AbilityNetMath.SanitizeRequest(ref r, origin, 160f));
            Assert.That(Vector3.Distance(origin, r.point), Is.EqualTo(160f).Within(1e-3f));
            Assert.That(r.point.z, Is.GreaterThan(origin.z), "clamped along the same line");

            var near = new CastRequest { point = origin + Vector3.right * 3f, hasPoint = true };
            Assert.IsTrue(AbilityNetMath.SanitizeRequest(ref near, origin, 160f));
            Assert.AreEqual(origin + Vector3.right * 3f, near.point);
        }

        // ------------------------------------------------------------------ time

        [Test]
        public void RemainingAndLocalTimeConversion()
        {
            Assert.AreEqual(0f, AbilityNetMath.Remaining(0f, 50f), "unset");
            Assert.AreEqual(0f, AbilityNetMath.Remaining(40f, 50f), "already passed");
            Assert.AreEqual(5f, AbilityNetMath.Remaining(55f, 50f), 1e-5f);

            // 3 s left on the server, the message travelled 0.5 s: 2.5 s left here.
            Assert.AreEqual(102.5f, AbilityNetMath.ToLocalTime(3f, 100f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, AbilityNetMath.ToLocalTime(0.2f, 100f, 0.5f), "ran out on the way");
            Assert.AreEqual(0f, AbilityNetMath.ToLocalTime(0f, 100f, 0f), "nothing left");
        }

        [Test]
        public void TimerJumpedOnlyWhenNotTickingDown()
        {
            // Reported 5 s left at t=10; at t=12 it shows 3 s: just ticking.
            Assert.IsFalse(AbilityNetMath.TimerJumped(5f, 10f, 3f, 12f));
            // A cooldown reset (0 left) or a reduction is a jump.
            Assert.IsTrue(AbilityNetMath.TimerJumped(5f, 10f, 0f, 12f));
            Assert.IsTrue(AbilityNetMath.TimerJumped(5f, 10f, 1.5f, 12f));
            // A new cooldown started after the old one ran out.
            Assert.IsTrue(AbilityNetMath.TimerJumped(0f, 10f, 4f, 12f));
            // Both finished.
            Assert.IsFalse(AbilityNetMath.TimerJumped(1f, 10f, 0f, 12f));
        }

        [Test]
        public void SlotStateRoundTripsBetweenMachines()
        {
            var server = new AbilitySlot();
            server.ApplyNetState(AbilityPhase.InCooldown, 2f, 5f, 0, 3f, 2f);
            Assert.AreEqual(AbilityPhase.InCooldown, server.Phase);
            Assert.AreEqual(0, server.Charges);

            server.GetNetState(out AbilityPhase phase, out float phaseRemaining, out float phaseLength, out int charges, out float recharge, out float locked);
            Assert.AreEqual(AbilityPhase.InCooldown, phase);
            Assert.That(phaseRemaining, Is.EqualTo(2f).Within(0.05f));
            Assert.That(phaseLength, Is.EqualTo(5f).Within(0.05f));
            Assert.AreEqual(0, charges);
            Assert.That(recharge, Is.EqualTo(3f).Within(0.05f));
            Assert.That(locked, Is.EqualTo(2f).Within(0.05f));

            // Another machine applies it, discounting 0.5 s of travel.
            var owner = new AbilitySlot();
            owner.ApplyNetState(phase, phaseRemaining, phaseLength, charges, recharge, locked, 0.5f);
            Assert.AreEqual(AbilityPhase.InCooldown, owner.Phase);
            Assert.That(owner.LockedUntil - Time.time, Is.EqualTo(1.5f).Within(0.05f));
            Assert.That(owner.RechargeEnd - Time.time, Is.EqualTo(2.5f).Within(0.05f));
        }

        [Test]
        public void SlotStateNeverForcesCastingPhases()
        {
            // Casting phases follow the copies of the server's casts, not the timer snapshots.
            var slot = new AbilitySlot();
            slot.ApplyNetState(AbilityPhase.Casting, 1f, 1f, 1, 0f, 0f);
            Assert.AreEqual(AbilityPhase.Ready, slot.Phase);
            Assert.AreEqual(1, slot.Charges);
        }

        // ------------------------------------------------------------------ seeded randomness

        [Test]
        public void CastRandomIsDeterministicPerSeed()
        {
            var a = new CastRandom(12345);
            var b = new CastRandom(12345);
            var c = new CastRandom(12346);
            bool differs = false;
            for (int i = 0; i < 32; i++)
            {
                float va = a.NextFloat();
                Assert.AreEqual(va, b.NextFloat());
                if (!Mathf.Approximately(va, c.NextFloat()))
                    differs = true;
            }
            Assert.IsTrue(differs, "neighbouring seeds give different sequences");
        }

        [Test]
        public void CastRandomStaysInRange()
        {
            var r = new CastRandom(7);
            for (int i = 0; i < 2000; i++)
            {
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                float x = r.Range(-15f, 15f);
                Assert.That(x, Is.GreaterThanOrEqualTo(-15f).And.LessThan(15f));
                Assert.That(r.InsideUnitCircle().sqrMagnitude, Is.LessThanOrEqualTo(1.0001f));
            }
            var zero = default(CastRandom);
            Assert.That(zero.NextFloat(), Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), "a default instance still works");
        }

        [Test]
        public void CastRandomLeavesUnityRandomAlone()
        {
            Random.InitState(99);
            float expected = Random.value;
            Random.InitState(99);
            var r = new CastRandom(5);
            r.NextFloat();
            r.InsideUnitCircle();
            Assert.AreEqual(expected, Random.value);
        }

        [Test]
        public void VolleySeedsAreStableAndDistinct()
        {
            Assert.AreEqual(AbilityNetMath.VolleySeed(42, 3), AbilityNetMath.VolleySeed(42, 3));
            Assert.AreNotEqual(AbilityNetMath.VolleySeed(42, 0), AbilityNetMath.VolleySeed(42, 1));
            Assert.AreNotEqual(AbilityNetMath.VolleySeed(42, 0), AbilityNetMath.VolleySeed(43, 0));
            Assert.AreNotEqual(0, AbilityNetMath.NextSeed());
        }

        // ------------------------------------------------------------------ rate limiting and aim

        [Test]
        public void RateLimiterAllowsBurstThenRefills()
        {
            var limiter = new AbilityNetMath.RateLimiter(10f, 3f);
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsFalse(limiter.TryTake(0f), "burst used up");
            Assert.IsTrue(limiter.TryTake(0.15f), "a token back after 0.15 s at 10/s");
            Assert.IsFalse(limiter.TryTake(0.15f));
            Assert.IsTrue(limiter.TryTake(10f));
        }

        [Test]
        public void AimChangedHonoursDistanceAndAngle()
        {
            Assert.IsFalse(AbilityNetMath.AimChanged(Vector3.zero, Vector3.forward, new Vector3(0.01f, 0f, 0f), Vector3.forward, 0.05f, 0.5f));
            Assert.IsTrue(AbilityNetMath.AimChanged(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 0f), Vector3.forward, 0.05f, 0.5f));
            Assert.IsTrue(AbilityNetMath.AimChanged(Vector3.zero, Vector3.forward, Vector3.zero, Vector3.right, 0.05f, 0.5f));
            Assert.IsTrue(AbilityNetMath.AimChanged(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.right, 0.05f, 0.5f), "a direction appeared");
        }

        // ------------------------------------------------------------------ presentation and offline behaviour

        [Test]
        public void PresentationCanBeSwitchedOffAndIsOnOffline()
        {
            Assert.AreEqual(NetMode.Offline, NetAuthority.Mode);
            Assert.IsTrue(AbilityPool.PresentationEnabled);
            try
            {
                AbilityPool.PresentationEnabled = false;
                Assert.IsFalse(AbilityPool.PresentationEnabled);
                Assert.IsNull(AbilityPool.PlayVfx(new GameObject("vfx probe"), Vector3.zero, Quaternion.identity));
            }
            finally
            {
                AbilityPool.PresentationEnabled = true;
                GameObject probe = GameObject.Find("vfx probe");
                if (probe != null)
                    Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void ExplicitStatsAreUsedByCastCopies()
        {
            var def = ScriptableObject.CreateInstance<AbilityDefinition>();
            try
            {
                AbilityStats stats = AbilityStats.Identity;
                stats.area = 2.5f;
                stats.castTime = 0.5f;
                var cast = new AbilityCastInstance(null, null, null, def, null, stats);
                Assert.AreEqual(2.5f, cast.Stats.area);
                Assert.AreEqual(0.5f, cast.Stats.castTime);
                Assert.IsFalse(cast.Cosmetic, "a cast is real unless the network layer makes it a copy");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
#endif
