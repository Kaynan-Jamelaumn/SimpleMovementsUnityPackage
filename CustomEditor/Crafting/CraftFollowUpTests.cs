#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the rules the areas asked of each other once each had done its own: an item nothing has gone into
    /// may be put aside at any station (its measurement waiting again), a cut in one go is bounded like the other cuts and
    /// keeps where it was marked, one open pot of each oil or wax serves every bench, and the rasp takes the bark off wood
    /// as the knife does (and starts the papers again).
    /// </summary>
    public class CraftFollowUpTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "followups");

        // ------------------------------------------------------------------ an item put aside
        [Test]
        public void AnItemNothingHasGoneInto_IsEmpty_UntilAPartOrASupplyGoesIn()
        {
            CraftWorkshop w = Workshop(CraftTools.Leatherworks);
            string cap = w.StartAssembly("leather_cap", out string why);
            Assert.IsNotNull(cap, why);
            Assert.IsTrue(CraftAssembly.IsEmpty(w.Piece(cap)), "just started: nothing has gone into it");
            string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            Assert.IsFalse(CraftAssembly.IsEmpty(w.Piece(hide)), "a hide is no item");
            Assert.IsTrue(w.Attach(cap, "thread", 0, "", out why, 1), why);
            Assert.IsFalse(CraftAssembly.IsEmpty(w.Piece(cap)), "thread added to it cannot be taken back out");
            Assert.IsFalse(w.PutAside(cap, out why), "so it is not put aside");
            StringAssert.Contains("take it with you", why);
        }

        [Test]
        public void AnEmptyItem_IsPutAsideAnywhere_AndItsMeasurementWaitsAgain()
        {
            CraftWorkshop w = Workshop(CraftTools.Tailor);
            Assert.IsTrue(w.MeasureFit("", 0.9, 980, out string why), why);
            string tunic = w.StartAssembly("wool_tunic", out why);
            Assert.IsNotNull(tunic, why);
            Assert.IsTrue(w.Piece(tunic).assembly.measured, "the item started takes the measurement that waited");
            Assert.IsFalse(w.WaitingMeasurement(false, out _, out _));
            Assert.IsTrue(w.PutAside(tunic, out why), why);
            Assert.IsNull(w.Piece(tunic), "it is gone");
            Assert.IsTrue(w.WaitingMeasurement(false, out double fit, out double size), "its measurement waits for the next item");
            Assert.AreEqual(0.9, fit, 1e-6);
            Assert.AreEqual(980, size, 1e-3);
            string coat = w.StartAssembly("wool_tunic", out why);
            Assert.IsTrue(w.Piece(coat).assembly.measured, "and the next item started takes it");
        }

        [Test]
        public void Guard_PutsAsideAnEmptyItemAnywhere_ButThrowsNothingElseAway()
        {
            CraftWorkshop w = Workshop(CraftTools.Leatherworks);
            string cap = w.StartAssembly("leather_cap", out string why);
            string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            var guard = new CraftRequestGuard(5, 0);
            var aside = new CraftRequest(CraftRequestKind.ScrapPiece, cap);
            Assert.IsNull(CraftRequestGuard.TestStationOnly(aside, w), "an empty item may be put aside at any station");
            Assert.IsTrue(guard.Check(ref aside, 0, out why, w), why);
            var scrap = new CraftRequest(CraftRequestKind.ScrapPiece, hide);
            Assert.AreEqual(CraftRequestGuard.TestStationOnly(CraftRequestKind.ScrapPiece), CraftRequestGuard.TestStationOnly(scrap, w));
            Assert.IsFalse(guard.Check(ref scrap, 1, out why, w), "a hide is taken, not thrown away");
            Assert.AreEqual(CraftRequestGuard.TestStationOnly(CraftRequestKind.ScrapPiece), why);
            var blind = new CraftRequest(CraftRequestKind.ScrapPiece, cap);
            Assert.IsFalse(guard.Check(ref blind, 2, out why), "without the workshop the guard cannot tell: refused as before");
            var again = new CraftRequest(CraftRequestKind.ScrapPiece, cap);
            Assert.IsTrue(guard.Check(ref again, 3, out why, w), why);
            var flood = new CraftRequest(CraftRequestKind.ScrapPiece, cap);
            Assert.IsTrue(guard.Check(ref flood, 3, out why, w), why);
            var third = new CraftRequest(CraftRequestKind.ScrapPiece, cap);
            Assert.IsFalse(guard.Check(ref third, 3, out why, w), "one thing at a time");
            Assert.AreEqual("One thing at a time.", why);
        }

        // ------------------------------------------------------------------ a cut in one go
        [Test]
        public void Guard_ACutInOneGo_IsOneCutAtATime_AndKeepsItsMark()
        {
            var guard = new CraftRequestGuard(5, 0);
            var first = new CraftRequest(CraftRequestKind.Split, "bar") { a = 0.37f };
            Assert.IsTrue(guard.Check(ref first, 0, out string why), why);
            Assert.AreEqual(0.37f, first.a, "cut where it was marked: no tremble added, nothing moved");
            var second = new CraftRequest(CraftRequestKind.Split, "bar") { a = 1.7f };
            Assert.IsTrue(guard.Check(ref second, 0.1, out why), why);
            Assert.AreEqual(1f, second.a, "only kept on the piece");
            var third = new CraftRequest(CraftRequestKind.Split, "bar") { a = 0.5f };
            Assert.IsFalse(guard.Check(ref third, 0.2, out why), "the cuts allowance holds two at once");
            Assert.AreEqual("Too fast: one cut at a time.", why);
            var later = new CraftRequest(CraftRequestKind.Split, "bar") { a = 0.5f };
            Assert.IsTrue(guard.Check(ref later, 2, out why), why);
        }

        [Test]
        public void ASplitOfWood_CutsAtTheMarkItself()
        {
            CraftWorkshop w = Workshop();
            string board = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 40, 30));
            string off = w.SplitPiece(board, 0.37, out string why);
            Assert.IsNotNull(off, why);
            Assert.AreEqual(600 * 0.37, w.Piece(board).Length, 0.6, "the guard keeps the mark, and the saw cuts at it");
        }

        // ------------------------------------------------------------------ one pot of each oil and wax
        [Test]
        public void OpenPots_OnePotOfEachSupply_ServesEveryBench()
        {
            var pots = new CraftOpenPots<string>();
            Assert.AreEqual(1, pots.ToOpen("ana", "beeswax", 0.3), "nothing open yet: a cake is opened");
            pots.Use("ana", "beeswax", 0.3); // a hide waxed: 0.3 of a cake (cell-loads over cellsPerItem)
            Assert.AreEqual(0.7, pots.Left("ana", "beeswax"), 1e-9);
            Assert.AreEqual(0, pots.ToOpen("ana", "beeswax", 0.5), "the same cake waxes a bow stave at the finishing bench");
            pots.Use("ana", "beeswax", 0.5);
            Assert.AreEqual(0.2, pots.Left("ana", "beeswax"), 1e-9);
            Assert.AreEqual(1, pots.ToOpen("ana", "beeswax", 0.5), "what is left will not cover it: the next cake");
            pots.Use("ana", "beeswax", 0.5);
            Assert.AreEqual(0.7, pots.Left("ana", "beeswax"), 1e-9);
            Assert.AreEqual(1, pots.ToOpen("bo", "beeswax", 0.1), "each player opens their own");
            Assert.AreEqual(1, pots.ToOpen("ana", "linseed_oil", 0.1), "and a pot of each supply");
            Assert.AreEqual(2, pots.ToOpen("ana", "beeswax", 2.5), "a long stroke over a whole hide may open several");
            pots.Use("bo", "beeswax", 0.1);
            pots.Forget("ana");
            Assert.AreEqual(0, pots.Left("ana", "beeswax"), "a player gone takes their pots");
            Assert.AreEqual(0.9, pots.Left("bo", "beeswax"), 1e-9);
            pots.Prune(p => p == "bo");
            Assert.AreEqual(0, pots.Count);
        }

        // ------------------------------------------------------------------ the rasp on wood
        [Test]
        public void TheRasp_TakesTheBarkOffWhereItCuts_AndThePapersStartAgain()
        {
            CraftWorkshop w = Workshop();
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 600, 50));
            Assert.IsTrue(w.SandWood(log, 0, 9, 0.4, 3, 1, out string why), why);
            CraftPiece p = w.Piece(log);
            Assert.AreEqual(0.4, p.wood.lastGrit, 1e-6, "sanded with the coarse paper");
            double before = p.wood.bark;
            for (int k = 0; k < 40; k++)
                Assert.IsTrue(w.File(log, 0, 3, CraftSurfaceSide.Faces, "rasp", 0, 1, 20, out why), why);
            Assert.Less(p.segments[1].bark, 0.5, "the rasp takes the bark where it cuts");
            Assert.Greater(p.segments[p.segments.Count - 1].bark, 0.9, "and only there");
            Assert.Less(p.wood.bark, before, "the piece's bark is what is left on its stretches");
            Assert.AreEqual(CraftWoodwork.BarkLeft(p), p.wood.bark, 1e-9);
            Assert.AreEqual(-1, p.wood.lastGrit, 1e-9, "a fresh cut: the papers start again from the coarsest");
            // a slot rasped into the end takes no bark off the outside
            double outside = p.segments[8].bark;
            Assert.IsTrue(w.File(log, 8, 9, CraftSurfaceSide.Eye, "rasp", 0, 1, 5, out why), why);
            Assert.AreEqual(outside, p.segments[8].bark, 1e-12);
        }
    }
}
#endif
