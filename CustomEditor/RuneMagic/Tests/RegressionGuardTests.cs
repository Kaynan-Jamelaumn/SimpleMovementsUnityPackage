#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Phase 9: guards for "do not break existing systems". The ability system, the essence absorption and the HUD know nothing about runes:
    /// no member of the existing types they were extended in mentions a Rune Magic type, the extensions are inert until something
    /// registers (no castable, no pre-cast hook, no gate rule), and the new request kinds and saves only ever add to what existed.
    /// What cannot be checked without the engine (a Warrior's essences in play, a mob's drops) is listed in docs/09-Acceptance-Report.md.
    /// </summary>
    public class RegressionGuardTests
    {
        // The existing project types Rune Magic extended (the three hooks of the ability system, essence absorption and the ability bar).
        private static readonly Type[] ExistingTypes =
        {
            typeof(AbilitySlot), typeof(AbilityCaster), typeof(PlayerAbilityController), typeof(AbilityAbsorption), typeof(AbilityPickup),
            typeof(AbsorptionGate), typeof(ICastable), typeof(HudAbilityBar),
            // The player validators know Rune Magic only through the generic Register hook (the update that added the grimoire).
            typeof(PlayerSetupValidator), typeof(PlayerSetupValidator.Check),
        };

        private static IEnumerable<Type> TypesOf(MemberInfo m)
        {
            switch (m)
            {
                case FieldInfo f: yield return f.FieldType; break;
                case PropertyInfo p: yield return p.PropertyType; break;
                case EventInfo e: yield return e.EventHandlerType; break;
                case MethodBase mb:
                    foreach (ParameterInfo pi in mb.GetParameters()) yield return pi.ParameterType;
                    if (mb is MethodInfo mi) yield return mi.ReturnType;
                    break;
            }
        }

        private static bool MentionsRuneMagic(Type t)
        {
            while (t != null && (t.IsArray || t.IsByRef || t.IsPointer)) t = t.GetElementType();
            if (t == null) return false;
            if (t.Namespace == "RuneMagic") return true;
            return t.IsGenericType && t.GetGenericArguments().Any(MentionsRuneMagic);
        }

        [Test]
        public void TheExistingSystemsKnowNothingAboutRunes()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var offenders = new List<string>();
            foreach (Type type in ExistingTypes)
                foreach (MemberInfo m in type.GetMembers(all))
                    foreach (Type used in TypesOf(m))
                        if (MentionsRuneMagic(used)) offenders.Add($"{type.Name}.{m.Name} ({used.Name})");
            Assert.IsEmpty(offenders, "an existing type mentions a Rune Magic type:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void TheExtensionsOfTheAbilitySystemAreInertUntilSomethingUsesThem()
        {
            var slot = new AbilitySlot();
            Assert.IsNull(slot.Castable, "an ordinary ability slot has no castable");
            Assert.IsFalse(AbsorptionGate.HasBlockRules || AbsorptionGate.Blocks(null, null) || AbsorptionGate.SuppressesRolls(null),
                           "with no rule registered, absorption is exactly what it was (this test runs before anything registers)");
            FieldInfo preCast = typeof(PlayerAbilityController).GetField("PreCast");
            Assert.IsNotNull(preCast, "the generic pre-cast hook exists");
            Assert.AreEqual(typeof(Func<int, bool>), preCast.FieldType);
            Assert.IsTrue(typeof(ICastable).IsInterface);
            Assert.AreEqual(6, typeof(ICastable).GetMethods().Length, "the whole contract: has resources, can start, started, released, interrupted, describe");
        }

        [Test]
        public void RuneMagicTypesNeverLiveInTheNamespacesOfTheExistingSystems()
        {
            // Everything added by Rune Magic is in its own namespace (or the networking namespace's NetRuneMagic), never mixed into another system's.
            Type[] added = typeof(RuneSoul).Assembly.GetTypes().Where(t => t.Namespace == "RuneMagic").ToArray();
            Assert.Greater(added.Length, 100);
            foreach (Type t in added)
                Assert.IsFalse(t.Name.StartsWith("Ability") || t.Name.StartsWith("Combat"), t.Name + " is named like an ability or combat type");
        }

        [Test]
        public void ARuneMagicSavePutsNothingInTheCharacterSave()
        {
            // The soul is kept in its own store (RuneSoulState JSON), keyed by the character: no field of the existing save formats was added.
            Type save = typeof(RuneSoulState);
            Assert.AreEqual("RuneMagic", save.Namespace);
            FieldInfo version = save.GetField("version");
            Assert.AreEqual(typeof(int), version.FieldType);
            // Older saves load (fields are only ever added): every version since the first is accepted and fills what it lacks.
            for (int v = 1; v <= RuneSoulState.CurrentVersion; v++)
            {
                RuneSoulState s = RuneSoulState.FromJson("{\"version\":" + v + "}");
                s.EnsureLists();
                Assert.IsNotNull(s.runes);
                Assert.IsNotNull(s.spells);
                Assert.IsNotNull(s.spellSlots);
            }
            Assert.IsNotNull(RuneSoulState.FromJson(""));
            Assert.IsNotNull(RuneSoulState.FromJson("not json at all"));
        }

        [Test]
        public void TheNetworkRequestKindsOnlyEverGrow()
        {
            var expected = new Dictionary<string, byte>
            {
                { "None", 0 }, { "Meditate", 1 }, { "Absorb", 2 }, { "Release", 3 }, { "AssignEnergy", 4 }, { "DiscoveryMinigame", 5 }, { "DiscoveryAnswer", 6 },
                { "DiscoveryAbandon", 7 }, { "DismissSpell", 8 }, { "Synthesize", 9 }, { "AssignSpell", 10 }, { "UnassignSpell", 11 }, { "SpellingStart", 12 }, { "SpellingResult", 13 },
                { "GrimoireOpen", 14 }, { "GrimoireImprint", 15 }, { "GrimoireAbsorb", 16 }, { "GrimoireErase", 17 },
            };
            foreach (KeyValuePair<string, byte> kv in expected)
                Assert.AreEqual(kv.Value, (byte)Enum.Parse(typeof(RuneRequestKind), kv.Key), kv.Key + ": a wire value never changes");
            Assert.AreEqual(expected.Count, Enum.GetValues(typeof(RuneRequestKind)).Length, "a new kind is added here on purpose, with the next value");
        }

        [Test]
        public void AClientCannotAskForAKindTheServerDoesNotKnow()
        {
            Assert.AreEqual(17, (int)RuneNetLimits.LastKind);
            for (int k = 1; k <= (int)RuneNetLimits.LastKind; k++)
                Assert.Greater(RuneNetLimits.MaxTextFor((RuneRequestKind)k), 0);
        }
    }
}
#endif
