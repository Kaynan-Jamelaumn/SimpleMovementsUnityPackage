#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) for the equipment rules: stat math, exact revert, armor set tiers,
    /// slot rules, combos and the weapon templates. Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class InventoryEquipmentTests
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

        private CombatStats NewStats() => Track(new GameObject("Stats Test")).AddComponent<CombatStats>();

        private ArmorSO NewArmor(ArmorSlotType slot)
        {
            ArmorSO a = Track(ScriptableObject.CreateInstance<ArmorSO>());
            var so = new SerializedObject(a);
            so.FindProperty("armorSlotType").enumValueIndex = (int)slot;
            so.FindProperty("stackMax").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // ------------------------------------------------------------------ combat stats
        [Test]
        public void DefenseReductionFollowsTheCurveAndCap()
        {
            CombatStats s = NewStats();
            Assert.AreEqual(0f, s.Reduction(0f), 1e-4f);
            Assert.AreEqual(0.5f, s.Reduction(s.DefenseHalfValue), 1e-4f, "The half value halves the damage.");
            Assert.AreEqual(s.MaxDamageReduction, s.Reduction(1_000_000f), 1e-4f, "Reduction is capped.");
            Assert.Less(s.Reduction(-50f), 0f, "Negative defense increases damage.");
        }

        [Test]
        public void ModifierTokensRevertExactlyWhatTheyAdded()
        {
            CombatStats s = NewStats();
            object a = s.AddModifiers("A", new[] { new CombatStatModifier(CombatStatType.Defense, 10f) });
            object b = s.AddModifiers("B", new[] { new CombatStatModifier(CombatStatType.Defense, 20f) }, null, 0.5f);
            Assert.AreEqual(20f, s.Get(CombatStatType.Defense), 1e-4f);
            s.RemoveModifiers(a);
            Assert.AreEqual(10f, s.Get(CombatStatType.Defense), 1e-4f);
            s.RemoveModifiers(a); // removing twice does nothing
            Assert.AreEqual(10f, s.Get(CombatStatType.Defense), 1e-4f);
            s.RemoveModifiers(b);
            Assert.AreEqual(0f, s.Get(CombatStatType.Defense), 1e-4f);
        }

        [Test]
        public void WeaponLimitedModifiersOnlyCountWithThatWeapon()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Sword mastery", new[] { new CombatStatModifier(CombatStatType.WeaponDamage, 25f, WeaponCategory.Sword) });
            Assert.AreEqual(0f, s.Get(CombatStatType.WeaponDamage, WeaponCategory.Axe), 1e-4f);
            Assert.AreEqual(25f, s.Get(CombatStatType.WeaponDamage, WeaponCategory.Sword), 1e-4f);
            Assert.AreEqual(1.25f, s.WeaponDamageMultiplier(WeaponCategory.Sword, WeaponScaling.None, false), 1e-4f);
        }

        [Test]
        public void ElementalResistanceIsClamped()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Fire", null, new[] { new ElementalResistance(ElementType.Fire, 70f), new ElementalResistance(ElementType.Fire, 70f) });
            s.AddModifiers("Ice", null, new[] { new ElementalResistance(ElementType.Ice, -300f) });
            Assert.LessOrEqual(s.GetResistance(ElementType.Fire), 100f);
            Assert.Less(s.GetResistance(ElementType.Fire), 140f);
            Assert.AreEqual(-100f, s.GetResistance(ElementType.Ice), 1e-4f);
            Assert.AreEqual(0f, s.GetResistance(ElementType.None), 1e-4f);
        }

        [Test]
        public void EquipmentEffectHandlesRevertTheirStats()
        {
            CombatStats s = NewStats();
            var ctx = new EquipmentContext(s);
            var effects = new List<EquipmentEffect>
            {
                new CombatStatsEffect(new[] { new CombatStatModifier(CombatStatType.Defense, 12f) }, new[] { new ElementalResistance(ElementType.Fire, 10f) }),
            };
            var handles = new List<EquipmentEffectHandle>();
            EquipmentEffect.ApplyAll(effects, ctx, 1f, "Test", handles);
            Assert.AreEqual(12f, s.Get(CombatStatType.Defense), 1e-4f);
            Assert.AreEqual(10f, s.GetResistance(ElementType.Fire), 1e-4f);
            EquipmentEffect.RevertAll(handles);
            Assert.AreEqual(0f, s.Get(CombatStatType.Defense), 1e-4f);
            Assert.AreEqual(0f, s.GetResistance(ElementType.Fire), 1e-4f);
        }

        // ------------------------------------------------------------------ armor sets
        [Test]
        public void UpgradeGroupsKeepOnlyTheHighestReachedTier()
        {
            ArmorSet set = Track(ScriptableObject.CreateInstance<ArmorSet>());
            var dmg2 = new ArmorSetEffect { piecesRequired = 2, effectName = "Damage I", upgradeGroup = "damage" };
            var dmg4 = new ArmorSetEffect { piecesRequired = 4, effectName = "Damage II", upgradeGroup = "damage" };
            var speed3 = new ArmorSetEffect { piecesRequired = 3, effectName = "Speed" };
            set.SetEffects.AddRange(new[] { dmg2, speed3, dmg4 });

            CollectionAssert.IsEmpty(set.GetActiveEffects(1));
            CollectionAssert.AreEquivalent(new[] { dmg2 }, set.GetActiveEffects(2));
            CollectionAssert.AreEquivalent(new[] { dmg2, speed3 }, set.GetActiveEffects(3));
            CollectionAssert.AreEquivalent(new[] { speed3, dmg4 }, set.GetActiveEffects(4));
        }

        [Test]
        public void SetPiecesAreCountedOnce()
        {
            ArmorSet set = Track(ScriptableObject.CreateInstance<ArmorSet>());
            ArmorSO helmet = NewArmor(ArmorSlotType.Helmet);
            ArmorSO boots = NewArmor(ArmorSlotType.Boots);
            ArmorSO other = NewArmor(ArmorSlotType.Gloves);
            set.AddPiece(helmet);
            set.AddPiece(boots);
            Assert.AreEqual(2, set.CountPieces(new ItemSO[] { helmet, helmet, boots, other, null }));
            Assert.AreEqual(0, set.CountPieces(null));
        }

        [Test]
        public void SetCompletionUsesTheHighestTierUnlessAllPiecesAreRequired()
        {
            ArmorSet set = Track(ScriptableObject.CreateInstance<ArmorSet>());
            for (int i = 0; i < 5; i++)
                set.AddPiece(NewArmor((ArmorSlotType)i));
            set.SetEffects.Add(new ArmorSetEffect { piecesRequired = 2 });
            set.SetEffects.Add(new ArmorSetEffect { piecesRequired = 4 });
            Assert.IsFalse(set.IsSetComplete(3));
            Assert.IsTrue(set.IsSetComplete(4));

            var so = new SerializedObject(set);
            so.FindProperty("requiresAllPiecesToComplete").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(set.IsSetComplete(4));
            Assert.IsTrue(set.IsSetComplete(5));
        }

        // ------------------------------------------------------------------ slots
        [Test]
        public void ArmorGoesOnlyInItsOwnSlotOrCommonSlots()
        {
            foreach (ArmorSlotType type in System.Enum.GetValues(typeof(ArmorSlotType)))
            {
                ArmorSO a = NewArmor(type);
                SlotType own = SlotTypeHelper.ArmorSlotTypeToSlotType(type);
                if (own == SlotType.Common)
                    continue;
                Assert.IsTrue(SlotTypeHelper.CanPlace(a, own), $"{type} fits {own}");
                Assert.IsTrue(SlotTypeHelper.CanPlace(a, SlotType.Common), $"{type} fits a common slot");
                Assert.IsTrue(SlotTypeHelper.Equips(a, own), $"{type} is equipped in {own}");
                Assert.IsFalse(SlotTypeHelper.Equips(a, SlotType.Common), $"{type} is not equipped in a common slot");
                Assert.AreEqual(type, SlotTypeHelper.SlotTypeToArmorSlotType(own), $"{type} converts back");
                foreach (SlotType other in SlotTypeHelper.GetArmorSlotTypes().Where(s => s != own))
                    Assert.IsFalse(SlotTypeHelper.CanPlace(a, other), $"{type} does not fit {other}");
            }
        }

        // ------------------------------------------------------------------ weapons
        [Test]
        public void ComboSequencesMatchTheLastInputs()
        {
            var combo = new ComboSequence { requiredSequence = new[] { AttackType.Normal, AttackType.Normal, AttackType.Heavy } };
            Assert.IsTrue(combo.MatchesEnd(new[] { AttackType.Heavy, AttackType.Normal, AttackType.Normal, AttackType.Heavy }));
            Assert.IsFalse(combo.MatchesEnd(new[] { AttackType.Normal, AttackType.Heavy }));
            Assert.IsFalse(combo.MatchesEnd(new[] { AttackType.Normal, AttackType.Heavy, AttackType.Heavy }));
            Assert.IsFalse(new ComboSequence().MatchesEnd(new[] { AttackType.Normal }));
        }

        [Test]
        public void EveryWeaponTemplateProducesAValidWeapon()
        {
            foreach (WeaponTemplates.Template t in System.Enum.GetValues(typeof(WeaponTemplates.Template)))
            {
                WeaponSO weapon = Track(ScriptableObject.CreateInstance<WeaponSO>());
                var so = new SerializedObject(weapon);
                so.FindProperty("stackMax").intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                WeaponTemplates.Apply(weapon, t);

                var errors = new List<string>();
                var warnings = new List<string>();
                weapon.ValidateItem(errors, warnings);
                // Ranged templates cast an ability you assign yourself; everything else must be valid as generated.
                errors.RemoveAll(e => e.Contains("Cast Ability has no ability"));
                CollectionAssert.IsEmpty(errors, $"{t}: {string.Join(" | ", errors)}");
                Assert.IsNotNull(weapon.GetAction(AttackType.Normal), $"{t} has a Normal attack");
            }
        }
        // ------------------------------------------------------------------ presets
        [Test]
        public void ArmorMaterialsScaleBySlotAndJewelryHasNoDefense()
        {
            ArmorSO chest = NewArmor(ArmorSlotType.Chestplate);
            ArmorSO boots = NewArmor(ArmorSlotType.Boots);
            ArmorSO ring = NewArmor(ArmorSlotType.Ring);
            foreach (ArmorSO a in new[] { chest, boots, ring })
                ItemPresets.ApplyArmor(a, ItemPresets.ArmorMaterial.Steel);
            Assert.Greater(chest.DefenseValue, boots.DefenseValue);
            Assert.AreEqual(0f, ring.DefenseValue, 1e-4f);
            Assert.Greater(ring.MagicDefenseValue, 0f);
            Assert.AreEqual(1, chest.StackMax);
        }

        [Test]
        public void WeaponTiersRaiseDamage()
        {
            WeaponSO weak = Track(ScriptableObject.CreateInstance<WeaponSO>());
            WeaponSO strong = Track(ScriptableObject.CreateInstance<WeaponSO>());
            ItemPresets.ApplyWeaponTier(weak, ItemPresets.WeaponTier.Wooden);
            ItemPresets.ApplyWeaponTier(strong, ItemPresets.WeaponTier.Legendary);
            Assert.Greater(strong.MaxDamage, weak.MaxDamage);
            Assert.Greater(strong.MaxDurability, weak.MaxDurability);
        }

        [Test]
        public void ConsumablePresetsSetEffects()
        {
            ConsumableSO meat = Track(ScriptableObject.CreateInstance<ConsumableSO>());
            ItemPresets.ApplyConsumable(meat, ItemPresets.Consumable.CookedMeat);
            Assert.AreEqual(2, meat.Effects.Count);
            Assert.AreEqual(ItemType.Food, meat.ItemType);
            Assert.IsTrue(meat.HasEffect(ConsumableEffectType.Food));
        }

        [Test]
        public void ArmorSlotAndWeaponCategoryAreGuessedFromNames()
        {
            Assert.IsTrue(ItemPresets.GuessArmorSlot(" iron boots ", out ArmorSlotType slot));
            Assert.AreEqual(ArmorSlotType.Boots, slot);
            Assert.IsTrue(ItemPresets.GuessCategory(" steel dagger ", out WeaponCategory cat));
            Assert.AreEqual(WeaponCategory.Dagger, cat);
        }
    }
}
#endif
