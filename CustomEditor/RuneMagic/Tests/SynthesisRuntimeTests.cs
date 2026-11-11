#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the synthesis side of the soul component (Phase 5) that need Unity's engine (game objects): they run
    /// in the Unity Test Runner (category "RequiresEngine"). The synthesis rules themselves are tested without the engine in
    /// <see cref="SynthesisEngineTests"/>; what a Mage can do through the component is checked in play mode (see the docs).
    /// </summary>
    [Category("RequiresEngine")]
    public class SynthesisRuntimeTests
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
        public void NobodyButARuneUserCanSynthesize()
        {
            RuneSoul soul = NewSoul();
            var lab = new SynthLab();
            SynthesisRequest req = lab.Ready("WWWAVVV");

            Assert.IsFalse(soul.CanSynthesize(out string why));
            StringAssert.Contains("Only rune users", why);
            SynthesisResult r = soul.Synthesize(req);
            Assert.IsFalse(r.performed);
            Assert.AreEqual(SynthesisRefusal.NotRuneUser, r.refusal);
            StringAssert.Contains("Only rune users", r.message);
            SynthesisPreview p = soul.PreviewSynthesis(req);
            Assert.AreEqual(SynthesisRefusal.NotRuneUser, p.refusal);
            Assert.IsFalse(p.CanAttempt);
            Assert.IsNull(soul.ExplainSynthesis(req, out string reason), "no exact evaluation for a non rune user");
            StringAssert.Contains("Only rune users", reason);
            Assert.AreEqual(0, soul.Spells.Count);
        }

        [Test]
        public void TheSynthesisWindowOpensOnlyForARuneUser()
        {
            var go = new GameObject("Synthesis window");
            made.Add(go);
            var window = go.AddComponent<SynthesisWindow>();
            Assert.IsFalse(SynthesisWindow.IsOpen);
            window.Open(); // no local rune user: it says so and stays closed
            Assert.IsFalse(SynthesisWindow.IsOpen);
            SynthesisWindow.Toggle();
            Assert.IsFalse(SynthesisWindow.IsOpen);
            SynthesisWindow.Show();
            Assert.IsFalse(SynthesisWindow.IsOpen);
            Assert.DoesNotThrow(() => window.Close());
        }

        [Test]
        public void ASoulWithoutARuneUserSendsNoSynthesisAndKeepsNoReport()
        {
            RuneSoul soul = NewSoul();
            Assert.IsFalse(soul.RequestSynthesis(null));
            Assert.IsFalse(soul.RequestSynthesis(new SynthLab().Ready("WWWAVVV")), "offline, the rules run here and refuse a non rune user");
            Assert.AreEqual(0, soul.LastReport.seq);
            Assert.IsFalse(soul.LastReport.HasResult);
            Assert.IsNotNull(soul.SynthesisSnapshot());
        }

        [Test]
        public void NobodyButARuneUserCanDismissASpell()
        {
            RuneSoul soul = NewSoul();
            Assert.IsFalse(soul.DismissSpell("syn_x", out string why));
            StringAssert.Contains("Only rune users", why);
            Assert.IsFalse(soul.RequestDismissSpell("syn_x"));
            Assert.IsFalse(soul.RequestDismissSpell(""));
        }

        [Test]
        public void SpellsOfASavedSoulAreListedAndSentToTheOwner()
        {
            var lab = new SynthLab();
            SynthesisResult made = lab.Execute(lab.Ready("WWWAVVV"));
            Assert.IsTrue(made.performed);

            RuneSoul soul = NewSoul();
            int before = soul.RunesVersion;
            Assert.IsTrue(soul.ImportState(lab.Json()));
            Assert.AreEqual(1, soul.Spells.Count);
            Assert.AreEqual("Pyroclastic Resonance", soul.FindSpell(made.spell.spellId).name);
            Assert.IsNull(soul.FindSpell("nope"));
            Assert.AreNotEqual(before, soul.RunesVersion, "the owner's UI is told the runes changed");

            RuneListView view = soul.BuildRuneList();
            Assert.AreEqual(1, view.spells.Count);
            Assert.AreEqual(7, view.spells[0].anchors.Count);
            Assert.AreEqual(7, view.runes.Count);
            Assert.AreNotSame(soul.Spells[0], view.spells[0], "the list sent is a copy");
            Assert.AreEqual(soul.RunesVersion, view.version);
        }
    }
}
#endif
