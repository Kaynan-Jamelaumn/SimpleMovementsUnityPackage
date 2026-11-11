#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of spellcasting (Phase 7) that need Unity's engine (game objects, scriptable objects): they run in the Unity Test
    /// Runner (category "RequiresEngine"). The rules of casting are tested without the engine in <see cref="SpellsCoreTests"/>; what a
    /// Mage can do with the keys, the bar and the Spellbook is checked in play mode (see docs/07-Spells-and-Slots.md).
    /// </summary>
    [Category("RequiresEngine")]
    public class SpellsRuntimeTests
    {
        private readonly List<Object> made = new List<Object>();
        private System.Func<string, AbilityDefinition> savedResolver;

        [SetUp]
        public void SetUp()
        {
            savedResolver = AbilityDatabase.FallbackResolver;
            RuneSpellAbilities.ForgetBuilt();
        }

        [TearDown]
        public void TearDown()
        {
            AbilityDatabase.FallbackResolver = savedResolver;
            RuneMagicDatabase.Override(null);
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            RuneSpellAbilities.ForgetBuilt();
        }

        private RuneSoul NewSoul()
        {
            var go = new GameObject("Not a player");
            made.Add(go);
            return go.AddComponent<RuneSoul>();
        }

        private RuneMagicDatabase NewDatabase(System.Action<RuneMagicData> edit = null)
        {
            RuneMagicDatabase db = ScriptableObject.CreateInstance<RuneMagicDatabase>();
            db.data = RuneDefaults.Create();
            edit?.Invoke(db.data);
            db.MarkChanged();
            made.Add(db);
            RuneMagicDatabase.Override(db);
            return db;
        }

        // ------------------------------------------------------------------ the slot
        [Test]
        public void AnOrdinaryAbilitySlotHasNoCastableAndAReplacedOneLosesIt()
        {
            var slot = new AbilitySlot();
            Assert.IsNull(slot.Castable, "an ordinary ability is unchanged");
            slot.Castable = new RuneSpellCastable(null, "syn_x");
            slot.Set(null, null);
            Assert.IsNull(slot.Castable, "a slot given another ability does not keep the old spell's costs");
        }

        [Test]
        public void ACastableWithoutASoulNeverAllowsACastAndSaysNothing()
        {
            var castable = new RuneSpellCastable(null, "syn_x");
            Assert.IsFalse(castable.HasResources(null));
            Assert.AreEqual("", castable.Describe());
            Assert.DoesNotThrow(() => castable.OnCastStarted(null));
            Assert.DoesNotThrow(() => castable.OnCastReleased(null));
            Assert.DoesNotThrow(() => castable.OnCastInterrupted(null, CastInterruptReason.Manual));
        }

        // ------------------------------------------------------------------ the abilities of spells
        [Test]
        public void EveryBuiltInFormBuildsAnAbilityThatCostsOnlyRuneEnergy()
        {
            NewDatabase();
            SpellBalance sb = RuneMagicDatabase.Instance.Catalog.Spells;
            foreach (SpellFormDef f in RuneMagicDatabase.Instance.Catalog.SpellForms)
            {
                string id = SpellCompiler.AbilityId(f.id, "syn_t");
                AbilityDefinition def = RuneSpellAbilities.Resolve(id);
                Assert.IsNotNull(def, f.id);
                made.Add(def);
                Assert.AreEqual(id, def.Id, f.id);
                Assert.AreEqual(0f, def.manaCost + def.staminaCost + def.healthCost, f.id + ": a spell costs Rune Energy, not mana, stamina or health");
                Assert.IsFalse(def.refundCostOnInterrupt, f.id + ": the spell's own castable decides about the refund");
                Assert.GreaterOrEqual(def.castTime * sb.castTimeBase, sb.minCastTime - 1e-4f, f.id + ": a spell is shaped, not clicked");
                Assert.AreSame(def, RuneSpellAbilities.Resolve(id), "built once");
            }
        }

        [Test]
        public void TheAbilityOfASpellIsFoundByItsIdOnEveryMachine()
        {
            NewDatabase();
            RuneSpellAbilities.Install();
            string id = SpellCompiler.AbilityId("bolt", "syn_abcdef012345");
            AbilityDefinition def = AbilityDatabase.Resolve(id);
            Assert.IsNotNull(def, "the fallback resolver builds it: a client finds what the server names");
            made.Add(def);
            Assert.AreEqual(id, def.Id);
            Assert.IsNull(RuneSpellAbilities.Resolve("rune:nosuchform:syn_x"));
            Assert.IsNull(RuneSpellAbilities.Resolve("runtime:Some other ability"));
            Assert.IsNull(RuneSpellAbilities.Resolve(""));
        }

        [Test]
        public void ARuneSpellFormWithoutAPresetIsAnEmptyCastThatCostsEnergyAndDoesNothing()
        {
            NewDatabase();
            AbilityDefinition ritual = RuneSpellAbilities.Resolve(SpellCompiler.AbilityId("ritual", "syn_r"));
            Assert.IsNotNull(ritual);
            made.Add(ritual);
            Assert.AreEqual(0, ritual.actions.Count);
            Assert.AreEqual(AbilityTargetingMode.Self, ritual.targeting.mode);
            Assert.AreEqual(CasterMovementRule.Slowed, ritual.movementWhileCasting, "the ritual form lets the caster move");
        }

        [Test]
        public void ADesignersTemplateReplacesTheBuiltInPresetAndItsCostsAreIgnored()
        {
            var template = ScriptableObject.CreateInstance<AbilityDefinition>();
            made.Add(template);
            template.manaCost = 50f;
            template.castTime = 2f;
            RuneMagicDatabase db = NewDatabase();
            db.spellFormTemplates.Add(new SpellFormTemplate { formId = "bolt", ability = template });
            AbilityDefinition def = RuneSpellAbilities.Resolve(SpellCompiler.AbilityId("bolt", "syn_tpl"));
            Assert.IsNotNull(def);
            made.Add(def);
            Assert.AreNotSame(template, def, "the template asset is never modified");
            Assert.AreEqual(2f, def.castTime, 1e-4f);
            Assert.AreEqual(0f, def.manaCost);
            Assert.AreEqual(50f, template.manaCost);
        }

        [Test]
        public void AMissingPresetWarnsOnceAndFallsBackToAnEmptyCast()
        {
            NewDatabase(d => d.spellForms.Find(f => f.id == "bolt").preset = "No Such Preset");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No Such Preset"));
            AbilityDefinition def = RuneSpellAbilities.Resolve(SpellCompiler.AbilityId("bolt", "syn_one"));
            Assert.IsNotNull(def);
            made.Add(def);
            Assert.AreEqual(0, def.actions.Count);
            AbilityDefinition other = RuneSpellAbilities.Resolve(SpellCompiler.AbilityId("bolt", "syn_two"));
            made.Add(other);
            LogAssert.NoUnexpectedReceived(); // the second one does not warn again
        }

        [Test]
        public void TheSlotsModifiersCarryTheSpellsNumbersAndTheSoulsDebuffs()
        {
            var c = RuneDefaults.Catalog();
            var sp = new SpellRecord { spellId = "syn_m", name = "M", effectId = "offense.pierce", rank = 2, power = 150f, stability = 70f, complexity = 0.6f };
            CompiledSpell cs = SpellCompiler.Compile(c, sp);
            var damaged = new CastPlan { allowed = true, castTimeMultiplier = cs.castTime * 1.15f, powerMultiplier = 0.85f };
            AbilityModifierSet m = RuneSpellAbilities.Modifiers(cs, damaged);
            Assert.AreEqual(cs.damage * 0.85f, m.damageMultiplier, 1e-4f);
            Assert.AreEqual(cs.castTime * 1.15f, m.castTimeMultiplier, 1e-4f);
            Assert.AreEqual(cs.cooldown, m.cooldownMultiplier, 1e-4f);
            Assert.AreEqual(cs.range, m.rangeMultiplier, 1e-4f);
            Assert.AreEqual(cs.area, m.areaMultiplier, 1e-4f);
            Assert.AreEqual(cs.duration, m.durationMultiplier, 1e-4f);
        }

        // ------------------------------------------------------------------ nothing changes for a character that is not a rune user
        [Test]
        public void ACharacterThatIsNotARuneUserCannotCastRuneSpellsOrAssignThem()
        {
            RuneSoul soul = NewSoul();
            var lab = new SynthLab();
            Assert.IsTrue(lab.Execute(lab.Ready("WWWAVVV")).performed);
            Assert.IsTrue(soul.ImportState(lab.Json()));
            string spell = soul.Spells[0].spellId;

            Assert.IsFalse(soul.CanCastSpell(spell, out string why));
            StringAssert.Contains("Only rune users", why);
            Assert.IsFalse(soul.AssignSpell(spell, 0, out why));
            StringAssert.Contains("Only rune users", why);
            Assert.IsFalse(soul.UnassignSpell(0, out why));
            Assert.IsFalse(soul.RequestAssignSpell(spell, 0));
            Assert.IsFalse(soul.RequestAssignSpell("", 0));
            Assert.IsFalse(soul.RequestAssignSpell(spell, -1));
            Assert.IsFalse(soul.RequestUnassignSpell(300));
            Assert.AreEqual(-1, soul.SpellSlotOf(spell));
            var keys = new List<SpellSlotInfo>();
            soul.GetSpellSlots(keys);
            Assert.AreEqual(0, keys.Count, "no spell slots for anyone but a rune user: the ability bar is untouched");
            Assert.AreEqual(0, soul.StateForSave.spellSlots.Count);
        }

        [Test]
        public void TheSpellbookOpensOnlyForARuneUser()
        {
            var go = new GameObject("Spellbook window");
            made.Add(go);
            var window = go.AddComponent<SpellbookWindow>();
            Assert.IsFalse(SpellbookWindow.IsOpen);
            window.Open(); // no local rune user: it says so and stays closed
            Assert.IsFalse(SpellbookWindow.IsOpen);
            SpellbookWindow.Toggle();
            Assert.IsFalse(SpellbookWindow.IsOpen);
            SpellbookWindow.Show();
            Assert.IsFalse(SpellbookWindow.IsOpen);
            Assert.DoesNotThrow(() => window.Close());
        }

        [Test]
        public void TheSpellbookReadsANamedInputActionAndNoKey()
        {
            Assert.AreEqual("Spellbook", RuneMagicInput.SpellbookAction);
            var s = new RuneMagicSettings();
            s.spellbookAction = "  Book ";
            Assert.IsTrue(new List<string>(s.InputActionNames()).Contains("Book"));
        }
    }
}
#endif
