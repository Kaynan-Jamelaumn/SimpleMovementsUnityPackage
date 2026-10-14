#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled helpers of networked weapons: validation of client aim and inputs,
    /// packing, rate limiting, the seeded behaviour rolls shared by server and clients, and the attack numbering that
    /// names an attack in network messages.
    /// </summary>
    public class WeaponNetTests
    {
        // ------------------------------------------------------------------ aim validation

        [Test]
        public void SanitizeAimRejectsGarbage()
        {
            Vector3 o = Vector3.zero, d = new Vector3(float.NaN, 0f, 1f);
            Assert.IsFalse(WeaponNetMath.SanitizeAim(ref o, ref d, Vector3.zero, 15f), "NaN direction");

            o = new Vector3(0f, float.PositiveInfinity, 0f);
            d = Vector3.forward;
            Assert.IsFalse(WeaponNetMath.SanitizeAim(ref o, ref d, Vector3.zero, 15f), "infinite origin");

            o = new Vector3(1e9f, 0f, 0f);
            d = Vector3.forward;
            Assert.IsFalse(WeaponNetMath.SanitizeAim(ref o, ref d, Vector3.zero, 15f), "absurd coordinates");

            o = Vector3.zero;
            d = Vector3.zero;
            Assert.IsFalse(WeaponNetMath.SanitizeAim(ref o, ref d, Vector3.zero, 15f), "no direction");
        }

        [Test]
        public void SanitizeAimNormalisesAndPullsTheOriginBack()
        {
            Vector3 anchor = new Vector3(10f, 0f, 10f);
            Vector3 o = anchor + new Vector3(0f, 0f, 100f);
            Vector3 d = new Vector3(0f, 0f, 5f);
            Assert.IsTrue(WeaponNetMath.SanitizeAim(ref o, ref d, anchor, 15f));
            Assert.AreEqual(1f, d.magnitude, 1e-5f);
            Assert.AreEqual(15f, Vector3.Distance(o, anchor), 1e-3f, "an origin far from the player is pulled back to the limit");

            Vector3 near = anchor + new Vector3(1f, 2f, -3f);
            Vector3 kept = near;
            d = Vector3.right;
            Assert.IsTrue(WeaponNetMath.SanitizeAim(ref kept, ref d, anchor, 15f));
            Assert.AreEqual(near, kept, "an origin near the player is kept");
        }

        [Test]
        public void AimChangedFollowsBothThresholds()
        {
            Vector3 o = Vector3.zero, d = Vector3.forward;
            Assert.IsFalse(WeaponNetMath.AimChanged(o, d, o + Vector3.right * 0.01f, d, 0.02f, 0.25f));
            Assert.IsTrue(WeaponNetMath.AimChanged(o, d, o + Vector3.right * 0.05f, d, 0.02f, 0.25f));
            Vector3 turned = Quaternion.Euler(0f, 0.1f, 0f) * d;
            Assert.IsFalse(WeaponNetMath.AimChanged(o, d, o, turned, 0.02f, 0.25f));
            turned = Quaternion.Euler(0f, 1f, 0f) * d;
            Assert.IsTrue(WeaponNetMath.AimChanged(o, d, o, turned, 0.02f, 0.25f));
            Assert.IsTrue(WeaponNetMath.AimChanged(o, Vector3.zero, o, d, 0.02f, 0.25f), "the first aim always counts");
        }

        // ------------------------------------------------------------------ inputs

        [Test]
        public void KnownKindsAndInputs()
        {
            Assert.IsTrue(WeaponNetMath.IsKnownKind((byte)WeaponInputKind.Press));
            Assert.IsTrue(WeaponNetMath.IsKnownKind((byte)WeaponInputKind.CancelCharge));
            Assert.IsFalse(WeaponNetMath.IsKnownKind(200));
            Assert.IsTrue(WeaponNetMath.IsKnownInput((byte)AttackType.Normal));
            Assert.IsTrue(WeaponNetMath.IsKnownInput((byte)AttackType.OffHand));
            Assert.IsFalse(WeaponNetMath.IsKnownInput((byte)AttackType.OffHand + 1));
        }

        [Test]
        public void HandAndInputPackingRoundTrips()
        {
            foreach (WeaponHandSide hand in new[] { WeaponHandSide.Main, WeaponHandSide.Off })
            {
                foreach (AttackType input in (AttackType[])System.Enum.GetValues(typeof(AttackType)))
                {
                    byte packed = WeaponNetMath.PackHandInput(hand, input);
                    Assert.IsTrue(WeaponNetMath.UnpackHandInput(packed, out WeaponHandSide h, out AttackType i));
                    Assert.AreEqual(hand, h);
                    Assert.AreEqual(input, i);
                }
            }
            Assert.IsFalse(WeaponNetMath.UnpackHandInput(0x7F, out _, out _), "an unknown input is refused");
        }

        [Test]
        public void RateLimiterAllowsABurstThenRefills()
        {
            var limiter = new WeaponNetMath.RateLimiter(10f, 3f);
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsTrue(limiter.TryTake(0f));
            Assert.IsFalse(limiter.TryTake(0f), "the burst is spent");
            Assert.IsTrue(limiter.TryTake(0.11f), "one token after a tenth of a second");
            Assert.IsFalse(limiter.TryTake(0.11f));
        }

        // ------------------------------------------------------------------ seeded behaviour rolls

        [Test]
        public void RollIsDeterministicAndInRange()
        {
            for (int seed = -50; seed < 50; seed++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = WeaponNetMath.Roll(seed * 7919, i, 3, 2);
                    Assert.AreEqual(a, WeaponNetMath.Roll(seed * 7919, i, 3, 2), "same inputs, same roll on every machine");
                    Assert.GreaterOrEqual(a, 0f);
                    Assert.Less(a, 1f);
                }
            }
        }

        [Test]
        public void RollIsRoughlyUniform()
        {
            const int n = 20000;
            double sum = 0d;
            int below = 0;
            for (int i = 0; i < n; i++)
            {
                float r = WeaponNetMath.Roll(12345, i, 1, 0);
                sum += r;
                if (r < 0.25f) below++;
            }
            Assert.AreEqual(0.5d, sum / n, 0.02d, "mean");
            Assert.AreEqual(0.25d, below / (double)n, 0.02d, "a 25% chance runs about a quarter of the time");
            Assert.AreNotEqual(WeaponNetMath.Roll(1, 0, 0, 0), WeaponNetMath.Roll(2, 0, 0, 0), "different seeds differ");
            Assert.AreNotEqual(WeaponNetMath.Roll(1, 0, 6, 1), WeaponNetMath.Roll(1, 0, 6, 2), "each hit rolls again");
        }

        [Test]
        public void NewSeedIsNeverZero()
        {
            for (int i = 0; i < 200; i++)
                Assert.AreNotEqual(0, WeaponNetMath.NewSeed());
        }

        // ------------------------------------------------------------------ attack catalog

        [Test]
        public void CatalogHandlesMissingWeaponsAndAttacks()
        {
            Assert.AreEqual(WeaponAttackCatalog.None, WeaponAttackCatalog.IndexOf(null, null));
            Assert.IsFalse(WeaponAttackCatalog.TryGet(null, 0, out _));
            var list = new List<WeaponAttackCatalog.Entry>();
            WeaponAttackCatalog.Collect(null, list);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void CatalogNumbersEveryAttackStably()
        {
            WeaponSO weapon = ScriptableObject.CreateInstance<WeaponSO>();
            try
            {
                var normal = new AttackAction();
                var v1 = new AttackVariation();
                var v2 = new AttackVariation();
                normal.variations.Add(v1);
                normal.variations.Add(v2);
                var heavy = new AttackAction();
                var finisher = new AttackAction();
                SetField(weapon, "normalAction", normal);
                SetField(weapon, "lightAction", null);
                SetField(weapon, "heavyAction", heavy);
                SetField(weapon, "specialAction", null);
                SetField(weapon, "alternateAction", null);
                SetField(weapon, "comboSequences", new List<ComboSequence> { new ComboSequence { specialAction = finisher } });
                SetField(weapon, "comboTree", null);

                // Normal, its two variations, Light (empty slot), Heavy, Special (empty), Alternate (empty), the sequence.
                Assert.AreEqual(0, WeaponAttackCatalog.IndexOf(weapon, normal));
                Assert.AreEqual(1, WeaponAttackCatalog.IndexOf(weapon, v1));
                Assert.AreEqual(2, WeaponAttackCatalog.IndexOf(weapon, v2));
                Assert.AreEqual(4, WeaponAttackCatalog.IndexOf(weapon, heavy));
                Assert.AreEqual(7, WeaponAttackCatalog.IndexOf(weapon, finisher));
                Assert.AreEqual(WeaponAttackCatalog.None, WeaponAttackCatalog.IndexOf(weapon, new AttackAction()), "an attack of another weapon");

                Assert.IsTrue(WeaponAttackCatalog.TryGet(weapon, 2, out WeaponAttackCatalog.Entry e));
                Assert.AreSame(v2, e.attack);
                Assert.AreSame(normal, e.action, "a variation knows its action");
                Assert.IsTrue(WeaponAttackCatalog.TryGet(weapon, 7, out e));
                Assert.AreSame(finisher, e.attack);
                Assert.IsNotNull(e.sequence, "a combo finisher knows its sequence");
                Assert.IsFalse(WeaponAttackCatalog.TryGet(weapon, 3, out _), "an empty slot names no attack");
                Assert.IsFalse(WeaponAttackCatalog.TryGet(weapon, 99, out _));
                Assert.IsFalse(WeaponAttackCatalog.TryGet(weapon, -1, out _));
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"WeaponSO.{name} not found");
            f.SetValue(target, value);
        }
    }
}
#endif
