#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RuneMagic.EditorTools;
using Check = PlayerSetupValidator.Check;
using Importance = PlayerSetupValidator.Importance;

namespace RuneMagic.Tests
{
    /// <summary>
    /// The project's player validators know the scripts a player needs. The update that added the grimoire made the list extensible (a
    /// system registers its own, generically: the validator never names a Rune Magic type) and added the journals of the crafting, alchemy and
    /// harvesting systems. These tests check the list, the registration and what Rune Magic registers; adding a script to a prefab needs the
    /// engine and is checked in the editor (see the docs).
    /// </summary>
    public class PlayerSetupRegistrationTests
    {
        private sealed class Marker { }

        [TearDown]
        public void TearDown() => Reset();

        private static void Reset() =>
            typeof(PlayerSetupValidator).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

        [Test]
        public void TheCraftingAlchemyAndHarvestingJournalsAreListedAsAutomaticPlayerScripts()
        {
            foreach (Type t in new[] { typeof(CraftKnowledge), typeof(AlchemyKnowledge), typeof(HarvestKnowledge) })
            {
                Check c = PlayerSetupValidator.Checks.FirstOrDefault(x => x.type == t);
                Assert.IsNotNull(c, t.Name + " is not in the Player Setup Validator");
                Assert.AreEqual(Importance.Automatic, c.importance, t.Name + " is added at run time when missing: it is only listed so its settings can be changed");
                Assert.IsNotEmpty(c.purpose);
            }
            Assert.IsFalse(PlayerSetupValidator.Checks.Any(c => c.importance == Importance.Required && c.type == typeof(CraftKnowledge)), "never required");
        }

        [Test]
        public void ASystemRegistersItsPlayerScriptsAfterTheBuiltInOnesAndRegisteringAgainReplaces()
        {
            int builtIn = PlayerSetupValidator.Checks.Count;
            PlayerSetupValidator.Register(new Check { type = typeof(Marker), importance = Importance.Recommended, purpose = "first", group = "Mine" });
            Assert.AreEqual(builtIn + 1, PlayerSetupValidator.Checks.Count);
            Assert.AreEqual(typeof(Marker), PlayerSetupValidator.Checks[PlayerSetupValidator.Checks.Count - 1].type, "after the built-in ones");
            PlayerSetupValidator.Register(new Check { type = typeof(Marker), importance = Importance.Optional, purpose = "second" });
            Assert.AreEqual(builtIn + 1, PlayerSetupValidator.Checks.Count, "the same type is replaced, not listed twice");
            Assert.AreEqual("second", PlayerSetupValidator.Checks.Last().purpose);
            PlayerSetupValidator.Register(null);
            PlayerSetupValidator.Register(new Check());
            Assert.AreEqual(builtIn + 1, PlayerSetupValidator.Checks.Count, "nothing without a type is registered");
        }

        [Test]
        public void ACheckWithoutAPredicateAlwaysApplies()
        {
            var always = new Check { type = typeof(Marker), importance = Importance.Recommended };
            Assert.IsTrue(PlayerSetupValidator.Applies(null, always), "no predicate: always");
            Assert.IsFalse(PlayerSetupValidator.Applies(null, null));
            Assert.IsFalse(PlayerSetupValidator.Applies(null, new Check { type = typeof(Marker), applies = _ => false }));
            Assert.IsTrue(PlayerSetupValidator.Applies(null, new Check { type = typeof(Marker), applies = _ => true }));
        }

        // Missing() asks a game object whether it holds a script (a native call), so these two run in Unity's Test Runner.
        [Test, Category("RequiresEngine")]
        public void ACheckThatDoesNotApplyIsNeitherListedNorReported()
        {
            PlayerSetupValidator.Register(new Check { type = typeof(Marker), importance = Importance.Recommended, applies = _ => false });
            Assert.IsFalse(PlayerSetupValidator.Missing(null, Importance.Recommended).Any(c => c.type == typeof(Marker)), "not applicable: not missing");
            PlayerSetupValidator.Register(new Check { type = typeof(Marker), importance = Importance.Recommended, applies = _ => true });
            Assert.IsTrue(PlayerSetupValidator.Missing(null, Importance.Recommended).Any(c => c.type == typeof(Marker)), "applicable and absent: missing");
            Assert.IsEmpty(PlayerSetupValidator.Missing(null, Importance.Required).Where(c => c.type == typeof(CraftKnowledge)), "the journals are never required");
        }

        [Test, Category("RequiresEngine")]
        public void AFailingPredicateDoesNotHideAScript()
        {
            var boom = new Check { type = typeof(Marker), importance = Importance.Recommended, applies = _ => throw new InvalidOperationException("boom") };
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { Assert.IsTrue(PlayerSetupValidator.Applies(null, boom)); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void RuneMagicRegistersItsPlayerScriptsWithAnAddThatDoesTheWholeSetup()
        {
            RuneMagicPlayerSetup.Register();
            Check soul = PlayerSetupValidator.Checks.FirstOrDefault(c => c.type == typeof(RuneSoul));
            Check meditation = PlayerSetupValidator.Checks.FirstOrDefault(c => c.type == typeof(MeditationController));
            Check gauge = PlayerSetupValidator.Checks.FirstOrDefault(c => c.type == typeof(SoulIntegrityManager));
            Assert.IsNotNull(soul);
            Assert.IsNotNull(meditation);
            Assert.IsNotNull(gauge);
            Assert.AreEqual(Importance.Recommended, soul.importance, "a player prefab should have it: the HUD gauge and the network need it");
            Assert.AreEqual(Importance.Recommended, meditation.importance);
            Assert.AreEqual(Importance.Automatic, gauge.importance, "added with the Rune Soul");
            Assert.AreEqual("Rune Magic", soul.group);
            Assert.IsNotNull(soul.add, "Add runs the Add Rune Magic to Player tool (the network part too)");
            Assert.IsNotNull(meditation.add);
            Assert.IsNotNull(soul.applies, "nothing is listed while Rune Magic is off");

            // The network module is registered when the project has it (a Fish-Net project).
            Type net = Type.GetType("GameNet.NetRuneMagic, Assembly-CSharp");
            if (net != null)
            {
                Check n = PlayerSetupValidator.Checks.FirstOrDefault(c => c.type == net);
                Assert.IsNotNull(n);
                Assert.IsNotNull(n.applies);
            }

            int count = PlayerSetupValidator.Checks.Count;
            RuneMagicPlayerSetup.Register();
            Assert.AreEqual(count, PlayerSetupValidator.Checks.Count, "registering again is harmless");
        }

        [Test]
        public void ThePlayerValidatorsKnowNothingAboutRunesTheirOwnTypesNeverNameOne()
        {
            foreach (Type t in new[] { typeof(PlayerSetupValidator), typeof(Check) })
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    Type used = m is FieldInfo f ? f.FieldType : m is PropertyInfo p ? p.PropertyType : m is MethodInfo mi ? mi.ReturnType : null;
                    Assert.IsFalse(used != null && used.Namespace == "RuneMagic", t.Name + "." + m.Name);
                }
        }
    }
}
#endif
