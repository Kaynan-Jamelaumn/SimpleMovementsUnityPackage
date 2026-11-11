#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the Mage-only gating (Phase 1): which classes are rune users, the generic absorption gate the
    /// ability system consults, and the Rune Magic rules registered with it. Tests in the "RequiresEngine" category create
    /// class and database assets in memory and need the Unity Test Runner.
    /// </summary>
    public class RuneMagicGateTests
    {
        private bool gateWasInstalled;

        [SetUp]
        public void SetUp()
        {
            // These tests own the gate's rules while they run.
            gateWasInstalled = RuneMagicGate.IsInstalled;
            RuneMagicGate.Uninstall();
        }

        [TearDown]
        public void TearDown()
        {
            RuneMagicDatabase.Override(null);
            if (gateWasInstalled)
                RuneMagicGate.Install();
        }

        // ------------------------------------------------------------------ the class rule (pure)
        [Test]
        public void MagesAreRuneUsersByDefault()
        {
            RuneMagicSettings s = RuneDefaults.Settings();
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, "Mage", "Mage"));
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, "Mage", null), "by asset name (what saves store)");
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, "Class_07", "mage"), "by display name, any case");
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, " Mage ", null), "trimmed");
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "Warrior", "Warrior"));
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "Magelike", "Mage Knight"), "whole names only");
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, null, null), "no class");
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "", "  "));
        }

        [Test]
        public void ClassArchetypesCountWhenAllowed()
        {
            RuneMagicSettings s = RuneDefaults.Settings();
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, "Battlemage", "Battlemage", new[] { "Mage" }), "a class built on the Mage archetype");
            s.matchClassArchetypes = false;
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "Battlemage", "Battlemage", new[] { "Mage" }));
        }

        [Test]
        public void TheClassListIsData()
        {
            var s = new RuneMagicSettings { runeUserClassNames = new List<string> { "Arcanist", "Rune Scholar" } };
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, "Arcanist", null));
            Assert.IsTrue(RuneUserRules.IsRuneUserClass(s, null, "rune scholar"));
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "Mage", "Mage"), "Mage is only the default");
            s.enabled = false;
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(s, "Arcanist", null), "Rune Magic disabled: nobody");
            Assert.IsFalse(RuneUserRules.IsRuneUserClass(null, "Mage", "Mage"));
        }

        [Test]
        public void NoOneIsARuneUserWithoutAPlayer()
        {
            Assert.IsFalse(RuneMagicGate.IsRuneUser((CombatEntity)null));
        }

        [Test, Category("RequiresEngine")]
        public void NothingIsARuneUserWithoutAComponent()
        {
            Assert.IsFalse(RuneMagicGate.IsRuneUser((Component)null));
        }

        // ------------------------------------------------------------------ the generic absorption gate
        [Test]
        public void GateRulesBlockOnlyWhileRegistered()
        {
            Assert.IsFalse(AbsorptionGate.Blocks(null, null), "no rules, nothing blocked");
            var seen = new List<AbilityGrant>();
            AbsorptionGate.BlockRule rule = (receiver, grant) => { seen.Add(grant); return true; };
            var g = new AbilityGrant();
            try
            {
                AbsorptionGate.AddBlockRule(rule);
                AbsorptionGate.AddBlockRule(rule); // once only
                Assert.IsTrue(AbsorptionGate.HasBlockRules);
                Assert.IsTrue(AbsorptionGate.Blocks(null, g));
                Assert.AreEqual(1, seen.Count, "a rule registered twice runs once");
                Assert.AreSame(g, seen[0]);
            }
            finally
            {
                AbsorptionGate.RemoveBlockRule(rule);
            }
            Assert.IsFalse(AbsorptionGate.Blocks(null, g));
            Assert.IsFalse(AbsorptionGate.HasBlockRules);
        }

        [Test]
        public void AFailingRuleIsLoggedAndBlocksNothing()
        {
            AbsorptionGate.BlockRule broken = (receiver, grant) => throw new InvalidOperationException("broken rule");
            AbsorptionGate.SuppressRule brokenSuppress = killer => throw new InvalidOperationException("broken suppress rule");
            try
            {
                AbsorptionGate.AddBlockRule(broken);
                AbsorptionGate.AddSuppressRule(brokenSuppress);
                LogAssert.Expect(LogType.Exception, new Regex("broken rule"));
                Assert.IsFalse(AbsorptionGate.Blocks(null, new AbilityGrant()));
                LogAssert.Expect(LogType.Exception, new Regex("broken suppress rule"));
                Assert.IsFalse(AbsorptionGate.SuppressesRolls(null));
            }
            finally
            {
                AbsorptionGate.RemoveBlockRule(broken);
                AbsorptionGate.RemoveSuppressRule(brokenSuppress);
            }
        }

        [Test]
        public void SuppressRulesAreSeparateFromBlockRules()
        {
            AbsorptionGate.SuppressRule rule = killer => true;
            try
            {
                AbsorptionGate.AddSuppressRule(rule);
                Assert.IsTrue(AbsorptionGate.SuppressesRolls(null));
                Assert.IsFalse(AbsorptionGate.Blocks(null, null), "suppressing rolls does not block receiving");
                Assert.IsFalse(AbsorptionGate.HasBlockRules);
            }
            finally
            {
                AbsorptionGate.RemoveSuppressRule(rule);
            }
            Assert.IsFalse(AbsorptionGate.SuppressesRolls(null));
        }

        [Test]
        public void TheRuneMagicRulesRegisterAndUnregister()
        {
            Assert.IsFalse(AbsorptionGate.HasBlockRules);
            RuneMagicGate.Install();
            Assert.IsTrue(RuneMagicGate.IsInstalled);
            Assert.IsTrue(AbsorptionGate.HasBlockRules);
            Assert.IsFalse(AbsorptionGate.Blocks(null, new AbilityGrant()), "no player, no block");
            RuneMagicGate.Uninstall();
            Assert.IsFalse(AbsorptionGate.HasBlockRules);
        }

        // ------------------------------------------------------------------ class assets (Unity Test Runner)
        private static PlayerClass NewClass(string assetName, string className = "")
        {
            var c = ScriptableObject.CreateInstance<PlayerClass>();
            c.name = assetName;
            c.className = className;
            return c;
        }

        private static RuneMagicDatabase NewDatabase()
        {
            var db = ScriptableObject.CreateInstance<RuneMagicDatabase>();
            db.data = RuneDefaults.Create();
            db.MarkChanged();
            return db;
        }

        [Test, Category("RequiresEngine")]
        public void ClassAssetsAreMatchedByNameReferenceAndArchetype()
        {
            RuneMagicDatabase db = NewDatabase();
            PlayerClass mage = NewClass("Mage");
            PlayerClass warrior = NewClass("Warrior");
            PlayerClass battlemage = NewClass("Battlemage");
            var mageArchetype = ScriptableObject.CreateInstance<CharacterArchetype>();
            mageArchetype.kind = ArchetypeKind.Class;
            mageArchetype.displayName = "Mage";
            battlemage.archetype = mageArchetype;
            var elf = ScriptableObject.CreateInstance<CharacterArchetype>();
            elf.kind = ArchetypeKind.Race;
            elf.displayName = "Mage"; // a race named like the class does not count
            try
            {
                Assert.IsTrue(RuneMagicGate.IsRuneUserClass(mage, null, db));
                Assert.IsFalse(RuneMagicGate.IsRuneUserClass(warrior, null, db));
                Assert.IsTrue(RuneMagicGate.IsRuneUserClass(battlemage, null, db), "class built on the Mage archetype");
                Assert.IsFalse(RuneMagicGate.IsRuneUserClass(warrior, new[] { elf }, db), "only Class archetypes");
                Assert.IsFalse(RuneMagicGate.IsRuneUserClass(null, null, db));

                db.runeUserClasses.Add(warrior);
                Assert.IsTrue(RuneMagicGate.IsRuneUserClass(warrior, null, db), "listed class asset");

                db.data.settings.enabled = false;
                db.MarkChanged();
                Assert.IsFalse(RuneMagicGate.IsRuneUserClass(mage, null, db), "Rune Magic disabled");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(db);
                UnityEngine.Object.DestroyImmediate(mage);
                UnityEngine.Object.DestroyImmediate(warrior);
                UnityEngine.Object.DestroyImmediate(battlemage);
                UnityEngine.Object.DestroyImmediate(mageArchetype);
                UnityEngine.Object.DestroyImmediate(elf);
            }
        }

        [Test, Category("RequiresEngine")]
        public void TheBuiltInDatabaseIsUsedWhenTheProjectHasNone()
        {
            RuneMagicDatabase db = NewDatabase();
            RuneMagicDatabase.Override(db);
            Assert.AreSame(db, RuneMagicDatabase.Instance);
            Assert.IsTrue(db.Catalog.IsValid, string.Join("\n", db.Catalog.Errors));
            int version = db.Version;
            db.MarkChanged();
            Assert.AreNotEqual(version, db.Version);
            UnityEngine.Object.DestroyImmediate(db);
        }
    }
}
#endif
