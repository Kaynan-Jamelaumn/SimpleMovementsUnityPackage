#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the Spelling System (Phase 8) that need Unity's engine (game objects, the Input System): they run in the Unity
    /// Test Runner (category "RequiresEngine"). The rules themselves (modes, difficulty, the key pool, the replay and the score, what the
    /// server accepts) are tested without the engine in <see cref="SpellingCoreTests"/>; what a Mage sees and presses is checked in play
    /// mode (see docs/08-Spelling-System.md).
    /// </summary>
    [Category("RequiresEngine")]
    public class SpellingRuntimeTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private RuneSoul NewSoul()
        {
            var go = new GameObject("Not a player");
            made.Add(go);
            return go.AddComponent<RuneSoul>();
        }

        [Test]
        public void ACharacterThatIsNotARuneUserNeverSpellsAndNothingIsRequired()
        {
            RuneSoul soul = NewSoul();
            Assert.IsFalse(soul.SpellingRequired("syn_x"), "no such spell");
            Assert.IsFalse(soul.HasReadySpelling("syn_x"));
            Assert.IsFalse(soul.HasReadySpelling(""));
            Assert.IsTrue(soul.CanStartSpell("syn_x", out string why), "nothing stands between a normal ability and its cast: " + why);
            Assert.IsFalse(soul.BeginSpelling(0), "no ability key holds a rune spell");
            Assert.IsFalse(soul.SubmitSpelling(1, 1f, new List<SpellingPress>()), "no sequence was asked for");
            Assert.AreEqual(SpellingState.None, soul.SpellingView.state);
            Assert.IsFalse(soul.IsSpelling);
            Assert.DoesNotThrow(() => soul.CancelSpelling());
            Assert.AreEqual(0, soul.SpellingPoolKeys.Count);
        }

        [Test]
        public void ControlPathsOfTheKeyboardBecomeKeyNamesAndOtherDevicesAreNotKeys()
        {
            Assert.IsTrue(SpellingKeyProvider.TryKeyName("<Keyboard>/w", out string w));
            Assert.AreEqual("W", w);
            Assert.IsTrue(SpellingKeyProvider.TryKeyName("<Keyboard>/leftShift", out string shift));
            Assert.AreEqual("LeftShift", shift);
            Assert.IsTrue(SpellingKeyProvider.TryKeyName("<Keyboard>/upArrow", out string up));
            Assert.AreEqual("UpArrow", up);
            Assert.IsFalse(SpellingKeyProvider.TryKeyName("<Gamepad>/buttonSouth", out _));
            Assert.IsFalse(SpellingKeyProvider.TryKeyName("<Mouse>/leftButton", out _));
            Assert.IsFalse(SpellingKeyProvider.TryKeyName("", out _));
            Assert.IsFalse(SpellingKeyProvider.TryKeyName(null, out _));
        }

        [Test]
        public void KeyNamesAreTheNamesOfTheInputSystemsKeyEnum()
        {
            Assert.IsTrue(SpellingKeyProvider.TryKey("W", out _));
            Assert.IsTrue(SpellingKeyProvider.TryKey("w", out _), "case does not matter");
            Assert.IsTrue(SpellingKeyProvider.TryKey("LeftShift", out _));
            Assert.IsFalse(SpellingKeyProvider.TryKey("NotAKey", out _));
            Assert.IsFalse(SpellingKeyProvider.TryKey("", out _));
            foreach (string candidate in RuneDefaults.Spelling().candidateKeys)
                Assert.IsTrue(SpellingKeyProvider.TryKey(candidate, out _), "the default candidate '" + candidate + "' is a Key");
            foreach (string reserved in RuneDefaults.Spelling().reservedKeys)
                Assert.IsTrue(SpellingKeyProvider.TryKey(reserved, out _), "the default reserved key '" + reserved + "' is a Key");
        }

        [Test]
        public void AKeyIsShownByItsNameWhenThereIsNoKeyboardLayoutToAsk()
        {
            string shown = SpellingKeyProvider.Label("A");
            Assert.IsFalse(string.IsNullOrEmpty(shown));
            Assert.AreEqual("NOTAKEY", SpellingKeyProvider.Label("NotAKey").ToUpperInvariant());
            Assert.DoesNotThrow(() => SpellingKeyProvider.PressedThisFrame("A"));
            Assert.IsFalse(SpellingKeyProvider.PressedThisFrame("NotAKey"));
        }

        [Test]
        public void ThePoolOfThisMachineNeverHoldsAReservedKeyAndNeverMoreThanTheCandidates()
        {
            SpellingBalance b = RuneDefaults.Spelling();
            SpellingKeyPool pool = SpellingKeyProvider.BuildPool(b, false, null);
            SpellingKeyPool rooted = SpellingKeyProvider.BuildPool(b, true, null);
            foreach (string reserved in b.reservedKeys)
            {
                CollectionAssert.DoesNotContain(pool.keys, reserved);
                CollectionAssert.DoesNotContain(rooted.keys, reserved);
            }
            Assert.LessOrEqual(pool.Count, b.candidateKeys.Count);
            Assert.GreaterOrEqual(rooted.Count, pool.Count, "a spell that roots the caster may use the movement keys too");
            foreach (string k in pool.keys) CollectionAssert.Contains(rooted.keys, k);
        }

        [Test]
        public void TheOverlayDoesNothingWithoutALocalRuneUser()
        {
            var go = new GameObject("Spelling overlay");
            made.Add(go);
            go.AddComponent<SpellingOverlay>();
            Assert.IsFalse(SpellingOverlay.IsActive);
        }
    }
}
#endif
