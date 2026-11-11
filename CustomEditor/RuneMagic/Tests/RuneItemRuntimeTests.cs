#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of rune items and the mob hook (Phase 3) that need Unity's engine (scriptable objects, game objects):
    /// they run in the Unity Test Runner (category "RequiresEngine").
    /// </summary>
    [Category("RequiresEngine")]
    public class RuneItemRuntimeTests
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
        public void RuneItemsAreMadeFromTheirId()
        {
            RuneItemSO item = RuneItems.Resolve("rune:fire:1:42:50");
            Assert.IsNotNull(item);
            Assert.AreSame(item, RuneItems.Resolve("rune:fire:1:42:50"), "the same id is the same item (they stack)");
            Assert.AreEqual("rune:fire:1:42:50", item.RuntimeId);
            Assert.AreEqual("rune:fire:1:42:50", ItemDatabase.Id(item));
            StringAssert.Contains("Fire", item.Name);
            Assert.AreEqual(1, item.Drop.rank);
            Assert.AreEqual(42f, item.Drop.quality, 0.001f);
            Assert.AreEqual(50f, item.Drop.energy, 0.001f);
            Assert.IsNotNull(item.Icon, "a generated icon");
            Assert.IsNull(RuneItems.Resolve("rune:no_such_rune:1:1:1"));
            Assert.AreSame(item, ItemDatabase.Item("rune:fire:1:42:50"), "the Item Database resolves rune ids (saves, merchants, network)");

            var lines = new List<string>();
            item.AppendTooltip(lines);
            Assert.IsTrue(lines.Exists(l => l.Contains("Quality 42")), string.Join("\n", lines));
            Assert.IsTrue(lines.Exists(l => l.Contains("Energy 50 / 1,000") || l.Contains("Energy 50 / 1.000") || l.Contains("Energy 50 / 1 000")), string.Join("\n", lines));
        }

        [Test]
        public void RuneItemAssetsValidateTheirRune()
        {
            var asset = ScriptableObject.CreateInstance<RuneItemSO>();
            made.Add(asset);
            var errors = new List<string>();
            asset.ValidateItem(errors, new List<string>());
            Assert.IsFalse(errors.Exists(e => e.Contains("has no rune")), "the default (fire) exists");
            var so = new UnityEditor.SerializedObject(asset);
            so.FindProperty("runeId").stringValue = "missing_rune";
            so.ApplyModifiedPropertiesWithoutUndo();
            errors.Clear();
            asset.ValidateItem(errors, new List<string>());
            Assert.IsTrue(errors.Exists(e => e.Contains("no rune 'missing_rune'")), string.Join("\n", errors));
        }

        [Test]
        public void ARuneSourceOnlyAddsData()
        {
            var go = new GameObject("Fire Imp");
            made.Add(go);
            var src = go.AddComponent<RuneSource>();
            Assert.AreEqual(MonsterRarity.Auto, src.Rarity);
            Assert.AreEqual(-1, src.Tier, "the dungeon decides by default");
            Assert.IsFalse(src.NoRunes);
            Assert.AreSame(src, RuneSource.Of(go.transform));
        }

        [Test]
        public void ARuneSoulWithoutARuneUserHoldsNoRunes()
        {
            var go = new GameObject("Not a player");
            made.Add(go);
            var soul = go.AddComponent<RuneSoul>();
            Assert.AreEqual(0f, soul.AddPendingEnergy(500f), "no energy for non rune users");
            Assert.IsNull(soul.AbsorbDrop(new RuneDrop { runeId = "fire", rank = 1, quality = 50f }, null, out string why));
            StringAssert.Contains("Only rune users", why);
            Assert.AreEqual(0, soul.Runes.Count);
            Assert.IsFalse(soul.ReleaseRune("x", out _));
        }
    }
}
#endif
