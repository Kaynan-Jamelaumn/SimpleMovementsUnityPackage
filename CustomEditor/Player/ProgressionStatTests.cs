#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ProgressionSystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) for the stat rules shared by races, classes, traits, items and
    /// buffs: stacking, exact removal, element / ability filters, caps, scaling, class conversion, trait rules and
    /// buff / debuff detection.
    /// </summary>
    public class ProgressionStatTests
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

        private CombatStats NewStats() => Track(new GameObject("stats")).AddComponent<CombatStats>();

        private static List<CombatStatModifier> Mods(params CombatStatModifier[] m) => new List<CombatStatModifier>(m);

        // ------------------------------------------------------------------ stacking and removal
        [Test]
        public void Stats_AddFromEverySourceAndRemoveExactly()
        {
            CombatStats s = NewStats();
            object race = s.AddModifiers("Race", Mods(new CombatStatModifier(CombatStatType.Defense, 10f)));
            object item = s.AddModifiers("Item", Mods(new CombatStatModifier(CombatStatType.Defense, 15f)));
            object buff = s.AddModifiers("Buff", Mods(new CombatStatModifier(CombatStatType.Defense, 20f)), null, 0.5f);
            Assert.AreEqual(35f, s.Get(CombatStatType.Defense), 0.001f, "10 + 15 + 20 × 0.5");
            s.RemoveModifiers(buff);
            Assert.AreEqual(25f, s.Get(CombatStatType.Defense), 0.001f);
            s.RemoveModifiers(item);
            s.RemoveModifiers(item); // removing twice changes nothing
            Assert.AreEqual(10f, s.Get(CombatStatType.Defense), 0.001f);
            s.RemoveModifiers(race);
            Assert.AreEqual(0f, s.Get(CombatStatType.Defense), 0.001f);
        }

        [Test]
        public void Scaling_UsesRawTotalsAndNeverChains()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Base", Mods(new CombatStatModifier(CombatStatType.Agility, 10f)));
            object rules = s.AddScaling("Elf", new List<StatScalingRule>
            {
                new StatScalingRule(CombatStatType.Agility, CombatStatType.CriticalChance, 0.5f),
                new StatScalingRule(CombatStatType.CriticalChance, CombatStatType.CriticalDamage, 10f),
            });
            Assert.AreEqual(5f, s.Get(CombatStatType.CriticalChance), 0.001f);
            Assert.AreEqual(0f, s.Get(CombatStatType.CriticalDamage), 0.001f, "crit from Agility does not feed the second rule");
            s.RemoveModifiers(rules);
            Assert.AreEqual(0f, s.Get(CombatStatType.CriticalChance), 0.001f);
        }

        // ------------------------------------------------------------------ filters and caps
        [Test]
        public void ElementFilter_PoisonResistanceOnlyAgainstPoison()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Dwarf", Mods(CombatStatModifier.ForElement(CombatStatType.StatusResistance, 50f, ElementType.Poison)));
            Assert.AreEqual(0.5f, s.StatusTakenMultiplier(ElementType.Poison), 0.001f);
            Assert.AreEqual(1f, s.StatusTakenMultiplier(ElementType.Fire), 0.001f);
            Assert.AreEqual(0f, s.Get(CombatStatType.StatusResistance), 0.001f, "filtered modifiers stay out of the general total");
        }

        [Test]
        public void ScopeFilter_AndCooldownCap()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Ring", Mods(CombatStatModifier.ForScope(CombatStatType.CooldownReduction, 20f, StatScope.Items)));
            Assert.AreEqual(1f, s.CooldownMultiplier(StatScope.Skills), 0.001f);
            Assert.AreEqual(0.8f, s.CooldownMultiplier(StatScope.Items), 0.001f);
            s.AddModifiers("Too much", Mods(new CombatStatModifier(CombatStatType.CooldownReduction, 500f)));
            Assert.AreEqual(0.4f, s.CooldownMultiplier(StatScope.Skills), 0.001f, "capped at the default 60%");
        }

        [Test]
        public void ControlResistance_IsCapped()
        {
            CombatStats s = NewStats();
            s.AddModifiers("Stone skin", Mods(new CombatStatModifier(CombatStatType.CrowdControlResistance, 300f)));
            Assert.AreEqual(0.2f, s.ControlTakenMultiplier, 0.001f, "capped at the default 80%");
        }

        // ------------------------------------------------------------------ classes, archetypes, traits
        [Test]
        public void PlayerClass_CombatBlockConvertsToModifiers()
        {
            var c = Track(ScriptableObject.CreateInstance<PlayerClass>());
            c.strength = 12f; c.agility = 0f; c.intelligence = 0f; c.endurance = 0f; c.defense = 0f; c.magicResistance = 0f;
            c.criticalChance = 5f; c.criticalDamage = 175f; c.attackSpeed = 1.1f; c.castingSpeed = 1f;
            var mods = c.GetCombatStatModifiers();
            float Get(CombatStatType t) { foreach (var m in mods) if (m.stat == t) return m.value; return 0f; }
            Assert.AreEqual(12f, Get(CombatStatType.Strength), 0.001f);
            Assert.AreEqual(25f, Get(CombatStatType.CriticalDamage), 0.001f, "175 = 25 above the normal 150");
            Assert.AreEqual(10f, Get(CombatStatType.AttackSpeed), 0.01f, "1.1 = 10% faster");
            Assert.IsFalse(mods.Exists(m => m.stat == CombatStatType.CastingSpeed), "1 = unchanged, not listed");
        }

        [Test]
        public void TraitRules_OnlyForForbiddenAndCost()
        {
            var elf = Track(ScriptableObject.CreateInstance<CharacterArchetype>());
            var dwarf = Track(ScriptableObject.CreateInstance<CharacterArchetype>());
            var arcane = Track(ScriptableObject.CreateInstance<Trait>());
            arcane.cost = 4; arcane.type = TraitType.Magic; arcane.onlyFor.Add(elf);
            var sturdy = Track(ScriptableObject.CreateInstance<Trait>());
            sturdy.cost = 3; sturdy.type = TraitType.Physical;
            elf.traitAffinities.Add(new TraitAffinity { type = TraitType.Magic, costMultiplier = 0.5f });
            elf.forbiddenTraits.Add(sturdy);

            Assert.IsNull(TraitRules.WhyNot(arcane, new[] { elf }));
            Assert.IsNotNull(TraitRules.WhyNot(arcane, new[] { dwarf }), "only for elves");
            Assert.IsNotNull(TraitRules.WhyNot(sturdy, new[] { elf }), "forbidden for elves");
            Assert.IsNull(TraitRules.WhyNot(sturdy, new[] { dwarf }));
            Assert.AreEqual(2, TraitRules.Cost(arcane, null, new[] { elf }), "magic traits half price");
            Assert.AreEqual(3, TraitRules.Cost(sturdy, null, new[] { dwarf }));

            var drawback = Track(ScriptableObject.CreateInstance<Trait>());
            drawback.cost = -4; drawback.type = TraitType.Magic;
            Assert.AreEqual(-4, TraitRules.Cost(drawback, null, new[] { elf }), "drawbacks keep their value");

            List<Trait> offered = TraitRules.Selectable(null, new[] { elf }, new[] { arcane, sturdy });
            Assert.IsTrue(offered.Contains(arcane));
            Assert.IsFalse(offered.Contains(sturdy));
        }

        [Test]
        public void Archetypes_IncompatibilityWorksBothWays()
        {
            var orc = Track(ScriptableObject.CreateInstance<CharacterArchetype>());
            var paladin = Track(ScriptableObject.CreateInstance<CharacterArchetype>());
            paladin.incompatibleWith.Add(orc);
            Assert.IsFalse(orc.IsCompatibleWith(paladin));
            Assert.IsFalse(paladin.IsCompatibleWith(orc));
            Assert.IsNotNull(TraitRules.WhyIncompatible(new[] { orc, paladin }));
        }

        // ------------------------------------------------------------------ buffs and debuffs
        [Test]
        public void StatBuff_AutoKindFollowsItsValues()
        {
            var e = new StatModifierEffect();
            e.combatStats.Add(new CombatStatModifier(CombatStatType.Defense, -20f));
            Assert.IsTrue(e.IsDebuff, "only lowers a stat");
            Assert.IsTrue(e.IsHarmful);
            e.combatStats.Add(new CombatStatModifier(CombatStatType.WeaponDamage, 30f));
            Assert.IsFalse(e.IsDebuff, "berserk: +damage -defense counts as a buff");
            var cheaper = new StatModifierEffect();
            cheaper.combatStats.Add(new CombatStatModifier(CombatStatType.AttackStaminaCost, -20f));
            Assert.IsFalse(cheaper.IsDebuff, "cheaper attacks help");
            cheaper.kind = StatBuffKind.Debuff;
            Assert.IsTrue(cheaper.IsDebuff, "an explicit kind wins");
        }
    }
}
#endif
