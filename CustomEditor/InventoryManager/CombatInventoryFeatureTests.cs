#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) for the grid inventory model, the hand rules, shield coverage,
    /// body-part location, bow draw strength and the suggested grid sizes. Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class CombatInventoryFeatureTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private sealed class Item
        {
            public readonly string Name;
            public Item(string name) { Name = name; }
            public override string ToString() => Name;
        }

        // ------------------------------------------------------------------ grid model
        [Test]
        public void Grid_PlacesWithoutOverlapAndInsideBounds()
        {
            var m = new GridInventoryModel<Item>(5, 4);
            var sword = new Item("sword");
            var helmet = new Item("helmet");
            Assert.IsTrue(m.Place(sword, new GridRect(0, 0, 1, 3)));
            Assert.IsFalse(m.Place(helmet, new GridRect(0, 1, 2, 2)), "overlaps the sword");
            Assert.IsFalse(m.Place(helmet, new GridRect(4, 0, 2, 2)), "sticks out on the right");
            Assert.IsFalse(m.Place(helmet, new GridRect(0, 3, 2, 2)), "sticks out at the bottom");
            Assert.IsFalse(m.Place(helmet, new GridRect(-1, 0, 2, 2)), "negative position");
            Assert.IsTrue(m.Place(helmet, new GridRect(1, 0, 2, 2)));
            Assert.AreSame(sword, m.At(0, 2));
            Assert.AreSame(helmet, m.At(2, 1));
            Assert.IsNull(m.At(3, 3));
            Assert.AreEqual(20 - 3 - 4, m.FreeCells);
            Assert.IsTrue(m.IsConsistent(out string problem), problem);
        }

        [Test]
        public void Grid_MoveIgnoresItsOwnCells()
        {
            var m = new GridInventoryModel<Item>(4, 4);
            var a = new Item("a");
            m.Place(a, new GridRect(0, 0, 2, 2));
            Assert.IsTrue(m.Move(a, new GridRect(1, 1, 2, 2)), "overlapping only itself");
            Assert.IsNull(m.At(0, 0));
            Assert.AreSame(a, m.At(2, 2));
            Assert.IsTrue(m.IsConsistent(out string problem), problem);
        }

        [Test]
        public void Grid_FindSpaceTurnsWhenOnlySidewaysFits()
        {
            var m = new GridInventoryModel<Item>(3, 2);
            Assert.IsTrue(m.FindSpace(1, 3, false, out _, out _) == false, "1×3 upright cannot fit a 2-row grid");
            Assert.IsTrue(m.FindSpace(1, 3, true, out GridRect r, out bool rotated));
            Assert.IsTrue(rotated);
            Assert.AreEqual(3, r.W);
            Assert.AreEqual(1, r.H);
        }

        [Test]
        public void Grid_SwapOnlyWhenBothFit()
        {
            var m = new GridInventoryModel<Item>(4, 2);
            var a = new Item("a");
            var b = new Item("b");
            m.Place(a, new GridRect(0, 0, 2, 2));
            m.Place(b, new GridRect(2, 0, 2, 2));
            Assert.IsTrue(m.Swap(a, new GridRect(2, 0, 2, 2), b, new GridRect(0, 0, 2, 2)));
            Assert.AreSame(b, m.At(0, 0));
            Assert.AreSame(a, m.At(3, 1));
            Assert.IsFalse(m.Swap(a, new GridRect(0, 0, 2, 2), b, new GridRect(1, 0, 2, 2)), "the targets overlap");
            Assert.AreSame(b, m.At(0, 0), "a refused swap changes nothing");
            Assert.IsTrue(m.IsConsistent(out string problem), problem);
        }

        [Test]
        public void Grid_RemoveFreesCells()
        {
            var m = new GridInventoryModel<Item>(3, 3);
            var a = new Item("a");
            m.Place(a, new GridRect(0, 0, 3, 1));
            Assert.IsTrue(m.Remove(a));
            Assert.AreEqual(9, m.FreeCells);
            Assert.IsFalse(m.Contains(a));
        }

        // ------------------------------------------------------------------ hands
        private WeaponSO Weapon(WeaponCategory category)
        {
            var w = Track(ScriptableObject.CreateInstance<WeaponSO>());
            w.SetStats(category, 5f, 10f, 0f, 1.5f, 0f, WeaponScaling.None);
            return w;
        }

        private ArmorSO Shield()
        {
            var a = Track(ScriptableObject.CreateInstance<ArmorSO>());
            var so = new SerializedObject(a);
            so.FindProperty("armorSlotType").enumValueIndex = (int)ArmorSlotType.Shield;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        [Test]
        public void Hands_TwoHandedFromCategoryAndGrip()
        {
            Assert.IsTrue(Weapon(WeaponCategory.Greatsword).IsTwoHanded);
            Assert.IsTrue(Weapon(WeaponCategory.Bow).IsTwoHanded);
            Assert.IsFalse(Weapon(WeaponCategory.Sword).IsTwoHanded);
            WeaponSO sword = Weapon(WeaponCategory.Sword);
            sword.SetHandling(WeaponGrip.TwoHanded, OffHandUse.Auto);
            Assert.IsTrue(sword.IsTwoHanded, "the grip overrides the category");
            Assert.IsFalse(sword.CanBeOffHand, "two-handed weapons never go in the off hand");
        }

        [Test]
        public void Hands_OffHandRules()
        {
            Assert.IsTrue(HandRules.CanHoldInOffHand(Shield(), out _));
            Assert.IsTrue(HandRules.CanHoldInOffHand(Weapon(WeaponCategory.Dagger), out _));
            Assert.IsFalse(HandRules.CanHoldInOffHand(Weapon(WeaponCategory.Greatsword), out string why));
            Assert.IsNotEmpty(why);
        }

        [Test]
        public void Hands_TwoHandedMainStowsTheShield()
        {
            ArmorSO shield = Shield();
            HandState twoHanded = HandRules.Evaluate(Weapon(WeaponCategory.Hammer), shield);
            Assert.IsTrue(twoHanded.offHandSuppressed);
            Assert.IsNull(twoHanded.activeOffHand);

            HandState oneHanded = HandRules.Evaluate(Weapon(WeaponCategory.Sword), shield);
            Assert.IsFalse(oneHanded.offHandSuppressed);
            Assert.AreSame(shield, oneHanded.OffHandShield);

            HandState dual = HandRules.Evaluate(Weapon(WeaponCategory.Sword), Weapon(WeaponCategory.Dagger));
            Assert.IsTrue(dual.IsDualWielding);
        }

        // ------------------------------------------------------------------ shields
        [Test]
        public void Shield_CoversTheFrontArcOnly()
        {
            ShieldDefense round = ShieldDefense.Round(); // 120°
            Vector3 forward = Vector3.forward;
            Assert.IsTrue(round.Covers(forward, Vector3.back), "a hit from straight ahead travels backwards");
            Assert.IsFalse(round.Covers(forward, Vector3.forward), "a hit from behind");
            Assert.IsFalse(round.Covers(forward, Vector3.left), "a hit from the right side (outside 60°)");
            Assert.IsTrue(ShieldDefense.Tower().Covers(forward, (Vector3.back + Vector3.left * 0.9f).normalized));
        }

        [Test]
        public void Shield_TrueDamageIsNeverBlocked()
        {
            Assert.AreEqual(0f, ShieldDefense.Kite().Reduction(DamageType.True, ElementType.None));
            Assert.Greater(ShieldDefense.Kite().Reduction(DamageType.Physical, ElementType.None), 0.5f);
        }

        // ------------------------------------------------------------------ body parts
        [Test]
        public void BodyParts_HumanoidBands()
        {
            BodyPartProfile p = Track(BodyPartProfile.CreateHumanoid());
            Assert.AreEqual("Head", p.Locate(0.95f, 0f).name);
            Assert.AreEqual("Torso", p.Locate(0.6f, 0f).name);
            Assert.AreEqual("Legs", p.Locate(0.2f, 0f).name);
            Assert.AreEqual("Arms", p.Locate(0.6f, 0.95f).name, "the side of the torso height is an arm");
            Assert.Greater(p.Find("Head").damageMultiplier, p.Find("Torso").damageMultiplier);
            Assert.IsTrue(p.Find("Head").IsProtectedBy(ArmorSlotType.Helmet));
            Assert.IsFalse(p.Find("Head").IsProtectedBy(ArmorSlotType.Boots));
        }

        // ------------------------------------------------------------------ ranged
        [Test]
        public void Bow_FullDrawIsStrongerFasterAndMoreAccurate()
        {
            var bow = new DrawMechanic { drawTime = 1f };
            RangedShot weak = bow.Shot(new RangedShotInput { heldSeconds = 0.3f });
            RangedShot full = bow.Shot(new RangedShotInput { heldSeconds = 1f });
            Assert.Greater(full.damageMultiplier, weak.damageMultiplier);
            Assert.Greater(full.velocity, weak.velocity);
            Assert.Less(full.spread, weak.spread);
            RangedShot shaking = bow.Shot(new RangedShotInput { heldSeconds = 1f + bow.steadyHoldTime + 2f });
            Assert.Greater(shaking.spread, full.spread, "holding a full draw too long makes the aim shake");
        }

        [Test]
        public void Bow_StylesHandleTapsAndHolds()
        {
            var quickOrDrawn = new DrawMechanic { style = DrawStyle.QuickOrDrawn, drawTime = 1f, quickShotTime = 0.2f, quickDamage = 0.6f, maxDamage = 1.5f };
            Assert.IsTrue(quickOrDrawn.IsQuickShot(0.1f), "a tap is a quick shot");
            Assert.IsFalse(quickOrDrawn.IsQuickShot(0.5f));
            Assert.AreEqual(0.6f, quickOrDrawn.Shot(new RangedShotInput { heldSeconds = 0.05f }).damageMultiplier, 1e-4f);
            Assert.AreEqual(1.5f, quickOrDrawn.Shot(new RangedShotInput { heldSeconds = 1f }).damageMultiplier, 1e-4f, "a full draw");
            Assert.IsTrue(quickOrDrawn.OverrideCharge(null).enabled, "holding draws");

            var quickOnly = new DrawMechanic { style = DrawStyle.QuickOnly, repeatWhileHeld = true };
            Assert.IsFalse(quickOnly.OverrideCharge(null).enabled, "fires on press");
            Assert.IsTrue(quickOnly.Automatic);
            Assert.AreEqual(quickOnly.quickDamage, quickOnly.Shot(new RangedShotInput { heldSeconds = 3f }).damageMultiplier, 1e-4f);

            var holdToDraw = new DrawMechanic();
            Assert.AreEqual(ChargeSettings.EarlyRelease.Cancel, holdToDraw.OverrideCharge(null).earlyRelease, "a tap below the minimum is cancelled");
        }

        [Test]
        public void Ammo_MatchesByTypeIgnoringCase()
        {
            var ammo = Track(ScriptableObject.CreateInstance<AmmoSO>());
            var req = new AmmoRequirement { ammoType = "arrow" };
            Assert.IsTrue(req.Accepts(ammo)); // AmmoSO defaults to "Arrow"
            req.ammoType = "Bolt";
            Assert.IsFalse(req.Accepts(ammo));
        }

        // ------------------------------------------------------------------ item sizes
        [Test]
        public void GridSize_SuggestedByKind()
        {
            Assert.AreEqual(new Vector2Int(1, 3), ItemPresets.SuggestGridSize(Weapon(WeaponCategory.Sword)));
            Assert.AreEqual(new Vector2Int(2, 4), ItemPresets.SuggestGridSize(Weapon(WeaponCategory.Greatsword)));
            Assert.AreEqual(new Vector2Int(1, 1), ItemPresets.SuggestGridSize(Track(ScriptableObject.CreateInstance<ConsumableSO>())));
            WeaponSO sword = Weapon(WeaponCategory.Sword);
            sword.SetGridSize(new Vector2Int(20, 0));
            Assert.AreEqual(new Vector2Int(10, 1), sword.GridSize, "sizes are clamped to 1-10");
        }
    }
}
#endif
