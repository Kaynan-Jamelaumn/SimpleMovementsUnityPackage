#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the discovery components (Phase 4) that need Unity's engine: they run in the Unity Test Runner
    /// (category "RequiresEngine").
    /// </summary>
    [Category("RequiresEngine")]
    public class ConceptDiscoveryRuntimeTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        [Test]
        public void ASourceNamesItsKindAndNeverStartsByStandingNextToIt()
        {
            var go = new GameObject("Inscription");
            made.Add(go);
            var src = go.AddComponent<ConceptDiscoverySource>();
            Assert.AreEqual("ancient_inscription", src.SourceId);
            Assert.AreEqual(-1, src.TierOverride, "the dungeon's tier by default");
            Assert.AreEqual("Ancient Inscription", src.DisplayName);
            Assert.AreEqual(0, src.DungeonTier, "outside a dungeon: the first band");
            src.SourceId = "wind_inscription";
            Assert.AreEqual("Gale Inscription", src.DisplayName);
            Assert.IsFalse(src.UsedUp, "a source is never used up");
        }

        [Test]
        public void NobodyButARuneUserCanBeginADiscovery()
        {
            var go = new GameObject("Not a player");
            made.Add(go);
            var soul = go.AddComponent<RuneSoul>();
            Assert.IsFalse(soul.BeginDiscovery("ancient_inscription", 0, -1, null, null, out string why));
            StringAssert.Contains("Only rune users", why);
            Assert.IsFalse(soul.IsDiscovering);
            Assert.AreEqual(DiscoveryPhase.None, soul.DiscoveryView.phase);
            Assert.IsFalse(soul.RequestDiscoveryAnswer(0));
            Assert.AreEqual(MinigameOutcome.None, soul.SubmitDiscoveryMinigame(new List<float> { 1f }));
            Assert.AreEqual(AnswerOutcome.None, soul.AnswerDiscovery(0));
            Assert.IsFalse(soul.LearnConcept("wind.pressure"), "no soul, no knowledge");
        }
    }
}
#endif
