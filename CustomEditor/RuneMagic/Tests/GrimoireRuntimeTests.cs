#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the Grimoire's component side that need Unity's engine (game objects, scriptable items): they run in the Unity Test
    /// Runner (category "RequiresEngine"). The rules are tested without the engine in <see cref="GrimoireCoreTests"/>; what a Mage can do with the
    /// window is checked in play mode (see the docs).
    /// </summary>
    [Category("RequiresEngine")]
    public class GrimoireRuntimeTests
    {
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp() => GrimoireStore.ResetForTests();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            GrimoireStore.ResetForTests();
        }

        private RuneSoul NewSoul()
        {
            var go = new GameObject("Not a player");
            made.Add(go);
            return go.AddComponent<RuneSoul>();
        }

        [Test]
        public void ASoulThatIsNotARuneUserCannotOpenImprintAbsorbOrErase()
        {
            RuneSoul soul = NewSoul();
            string id = GrimoireIds.Make("abc123", "Aria");
            GrimoireStore.Ledger.Create("abc123", "k", "Aria", 10, 1L);

            Assert.IsFalse(soul.OpenGrimoire(id, out string why));
            StringAssert.Contains("Only a Mage", why);
            Assert.IsFalse(soul.ImprintToGrimoire(id, 0, "anything", out why));
            StringAssert.Contains("Only a Mage", why);
            Assert.IsFalse(soul.AbsorbFromGrimoire(id, 0, out why));
            Assert.IsFalse(soul.EraseGrimoirePage(id, 0, out why));
            Assert.IsFalse(soul.RequestOpenGrimoire("rune:wind:1:50:0"), "not a grimoire id");
            Assert.IsFalse(soul.RequestImprint(id, 0, ""), "no rune named");
            Assert.AreEqual("", soul.GrimoireView.itemId, "nothing is open");
            Assert.IsEmpty(soul.HeldGrimoires(), "no inventory, no books");
        }

        [Test]
        public void AGrimoireItemIsMadeFromItsIdOnEveryMachineAndFoundThroughTheItemDatabase()
        {
            string id = GrimoireIds.Make("abc123", "Aria");
            GrimoireItemSO item = GrimoireItems.Resolve(id);
            Assert.IsNotNull(item);
            made.Add(item);
            Assert.AreEqual("abc123", item.Serial);
            Assert.AreEqual("Aria", item.Author);
            Assert.AreEqual(id, item.RuntimeId);
            Assert.AreEqual(1, item.StackMax, "a book is not stacked: its pages are its own");
            StringAssert.Contains("Aria", item.Name);
            Assert.AreSame(item, GrimoireItems.Resolve(id), "made once");
            Assert.AreSame(item, ItemDatabase.Item(id), "found by its id, as in saves, trades and the network");
            Assert.AreEqual(id, ItemDatabase.Id(item));
            Assert.IsNull(GrimoireItems.Resolve("grimoire:BAD:x"));
            Assert.IsNull(GrimoireItems.Resolve("rune:wind:1:50:0"));
            Assert.IsNotNull(item.Icon);
            var lines = new List<string>();
            item.AppendTooltip(lines);
            Assert.IsTrue(lines.Exists(l => l.Contains("Aria")));
        }

        [Test]
        public void TheWorldsLedgerKeepsABooksPagesUnderItsSerialAndCountsItsChanges()
        {
            GrimoireData book = GrimoireStore.Create("key", "Aria");
            Assert.IsNotEmpty(book.serial);
            Assert.AreSame(book, GrimoireStore.Find(book.serial));
            Assert.AreEqual(GrimoireItems.IdOf(book), GrimoireIds.Make(book.serial, "Aria"));
            int revision = book.revision;
            GrimoireStore.Changed(book);
            Assert.AreEqual(revision + 1, book.revision);
            Assert.IsNull(GrimoireStore.Find("missing"));
        }

        [Test]
        public void TheGrimoireWindowOpensOnlyForARuneUserWhoHoldsABook()
        {
            var go = new GameObject("Grimoire window");
            made.Add(go);
            go.AddComponent<GrimoireWindow>();
            Assert.IsFalse(GrimoireWindow.IsOpen);
            GrimoireWindow.Toggle(); // no local rune user: it says so and stays closed
            Assert.IsFalse(GrimoireWindow.IsOpen);
            GrimoireWindow.Show();
            Assert.IsFalse(GrimoireWindow.IsOpen);
        }
    }
}
#endif
