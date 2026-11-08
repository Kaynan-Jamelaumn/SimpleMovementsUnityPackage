#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the leather's review (the tannery and the cutting table): leather tanned with its hair on is
    /// worth less (a hairy grain neither slicks nor dyes); the dye gives the item its colour; a raw hide does not rot in
    /// the liquor that tans it (only raw and wet in the air, in plain water or a spent liquor); a state the hide goes on
    /// being in (rot, a closed tan, shrinking) is as bad as it is, never stacked on itself; the frame dries a hide flat
    /// only as far as its slackest side holds it; a stir never unmixes; the hair knife scuffs only where hair holds;
    /// the look words the frame by its pulls; chalking leather is leatherwork; the rack hangs four hides; the bucket
    /// asks before it pours; a cut along the chalk and the part's look agree; one pass of the skiving knife.
    /// </summary>
    public class CraftReviewLeatherTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "leatherrev");

        private static void Hours(CraftWorkshop w, double hours) =>
            w.RunSteps((int)Math.Round(hours / w.Rules.Settings.processHoursPerSecond / w.Rules.Settings.stepSeconds));

        private static IEnumerable<HideCell> Cells(CraftWorkshop w, string id) => w.Piece(id).hide.cells.Where(c => c.present);
        private static double Mean(CraftWorkshop w, string id, Func<HideCell, double> of) => Cells(w, id).Average(of);

        /// <summary>Fleshed on the beam, a stroke along every row, twice.</summary>
        private static string Fleshed(CraftWorkshop w, string hideId, int seed = 2)
        {
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, hideId, seed));
            CraftPiece h = w.Piece(id);
            for (int pass = 0; pass < 2; pass++)
                for (int y = 0; y < h.hide.height; y++)
                    Assert.IsTrue(w.Scrape(id, new float[] { 0, y + 0.5f, h.hide.width, y + 0.5f }, false, 0.35, 0.7, out string why), why);
            return id;
        }

        /// <summary>Limed a day, then the hair scraped off on the beam, gently, four times over.</summary>
        private static void LimeAndUnhair(CraftWorkshop w, string id, int vat)
        {
            HideState h = w.Piece(id).hide;
            Assert.IsTrue(w.FillVat(vat, "lime", 2, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, vat), why);
            Hours(w, 24);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            for (int pass = 0; pass < 4; pass++)
                for (int y = 0; y < h.height; y++)
                    Assert.IsTrue(w.Scrape(id, new float[] { 0, y + 0.5f, h.width, y + 0.5f }, true, 0.35, 0.5, out why), why);
        }

        /// <summary>Tanned in bark (3 doses, at the workshop's warmth), lifted out.</summary>
        private static void Tan(CraftWorkshop w, string id, int vat, double hours)
        {
            Assert.IsTrue(w.FillVat(vat, "bark", 3, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, vat), why);
            Hours(w, hours);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        private static double[] Limits(CraftWorkshop w, string id) => Enumerable.Range(0, 4).Select(k => CraftLeather.EdgeLimit(w.Rules, w.Piece(id).hide, k)).ToArray();

        /// <summary>Laced in the frame and pulled to these shares of each side's limit (null: hung on the rack), dried, taken down.</summary>
        private static void FrameAndDry(CraftWorkshop w, string id, double[] share)
        {
            string why;
            if (share == null)
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out why), why);
            else
            {
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out why), why);
                double[] lim = Limits(w, id);
                Assert.IsTrue(w.Stretch(id, lim[0] * share[0], lim[1] * share[1], lim[2] * share[2], lim[3] * share[3], out why), why);
            }
            for (double t = 0; t < 80 && !w.Piece(id).hide.dried; t += 1) Hours(w, 1);
            Assert.IsTrue(w.Piece(id).hide.dried, "it never dried");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        private static CraftItemReport CapItem(CraftWorkshop w, string part) =>
            CraftEvaluator.Item(w.Rules, w.Rules.Design("leather_cap"), new Dictionary<string, List<CraftPiece>> { ["crown"] = new List<CraftPiece> { w.Piece(part) } }, null);

        private static float Distance(Color a, Color b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);

        // ------------------------------------------------------------------ leather-r01: the hair left on
        /// <summary>A cow fleshed, (limed and unhaired), tanned 120 h, framed taut and dried, a cap cut from it.</summary>
        private static (CraftItemReport item, CraftPieceReport part) Cap(bool unhair)
        {
            CraftWorkshop w = Workshop();
            string id = Fleshed(w, "cow");
            if (unhair) LimeAndUnhair(w, id, 0);
            Tan(w, id, 1, 120);
            FrameAndDry(w, id, new[] { 0.65, 0.65, 0.65, 0.65 });
            string part = w.CutPattern(id, "cap", 7, 5, 0, 0.9, out string why);
            Assert.IsNotNull(part, why);
            return (CapItem(w, part), CraftEvaluator.Piece(w.Rules, w.Piece(part)));
        }

        [Test]
        public void Hair_LeftOn_MarksTheLeatherDown_AndTheItemSaysWhy()
        {
            var bare = Cap(true);
            var hairy = Cap(false);
            Assert.AreEqual(0, bare.part.HairLeft, 1e-9, "limed and scraped, its hair is off");
            Assert.Greater(hairy.part.HairLeft, 0.9, "tanned with its hair on");
            Assert.Greater(bare.item.Quality, hairy.item.Quality + 0.03, "the hair left on makes a worse cap");
            Assert.Less(hairy.item.Workmanship, bare.item.Workmanship * 0.7, "it is half the work");
            CollectionAssert.Contains(hairy.item.Notes, "Crown: the hair was left on", "and the item says why");
            CollectionAssert.Contains(hairy.part.Defects, "the hair was left on");
            Assert.IsFalse(bare.item.Notes.Any(n => n.Contains("hair")), "nothing to say of a cap with its hair off");
        }

        [Test]
        public void Hair_KeepsTheSlickerAndTheDyeOffTheGrain()
        {
            var bare = new HideCell { hair = 0 };
            var hairy = new HideCell { hair = 0.5 };
            Assert.AreEqual(CraftLeather.DyeTake(bare, 1, 1) * 0.65, CraftLeather.DyeTake(hairy, 1, 1), 1e-9, "hair keeps a third of the dye off the grain");
            CraftWorkshop w = Workshop();
            string a = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2)), b = w.AddPiece(CraftLeather.NewHide(w.Rules, "drake", 2));
            foreach (string id in new[] { a, b })
                Assert.IsTrue(w.Slick(id, new float[] { 0, 3.5f, 10, 3.5f }, 0.6, out string why), why);
            Assert.Less(Cells(w, a).Max(c => c.smooth), Cells(w, b).Max(c => c.smooth) * 0.6, "the cow's hair keeps the slicker off its grain; the bare drake slicks");
        }

        [Test]
        public void Hair_TheWorksMeasure_IsThePathsAndTheGuides()
        {
            CraftHideDefinition cow = Rules().Hide("cow");
            Assert.AreEqual(1, CraftLeather.HairLeft(cow, cow.hair), 1e-9, "all it grew");
            Assert.AreEqual(0, CraftLeather.HairLeft(cow, CraftLeather.UnhairedBelow - 1e-6), 1e-9, "under the path's line it is unhaired");
            Assert.AreEqual(0.5, CraftLeather.HairLeft(cow, (cow.hair + CraftLeather.UnhairedBelow) / 2), 1e-9, "half way");
            Assert.AreEqual(0, CraftLeather.HairLeft(Rules().Hide("drake"), 0), 1e-9, "a drake grows none");
        }

        // ------------------------------------------------------------------ leather-r02: the dye reaches the item
        [Test]
        public void Dye_GivesTheItemItsColour()
        {
            Color plain = Color.clear, black = Color.clear;
            foreach (bool dye in new[] { false, true })
            {
                CraftWorkshop w = Workshop();
                string id = Fleshed(w, "cow");
                LimeAndUnhair(w, id, 0);
                Tan(w, id, 1, 120);
                FrameAndDry(w, id, new[] { 0.65, 0.65, 0.65, 0.65 });
                HideState h = w.Piece(id).hide;
                if (dye)
                    for (int pass = 0; pass < 6; pass++)
                        for (int y = 0; y < h.height; y++)
                            Assert.IsTrue(w.Dye(id, "dye_black", new float[] { 0, y + 0.5f, h.width, y + 0.5f }, 1, out string why), why);
                string part = w.CutPattern(id, "cap", 7, 5, 0, 0.9, out string cutWhy);
                Assert.IsNotNull(part, cutWhy);
                CraftItemReport item = CapItem(w, part);
                Assert.Less(Distance(item.Color, CraftLeather.Colour(w.Rules, w.Piece(part).hide)), 1e-4f, "the item is the colour of its leather");
                if (dye) black = item.Color; else plain = item.Color;
            }
            Color tan = Rules().Agent("bark").color, ink = Rules().Supply("dye_black").color;
            Assert.Less(Distance(plain, tan), 0.06f, "undyed, a bark-tanned cap is the tan's brown");
            Assert.Less(Distance(black, ink), 0.15f, "dyed black, it is black");
        }

        [Test]
        public void Dye_ClothIsItsOwnColour_DyedAsItTook()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 2));
            Color linen = Rules().Hide("linen_cloth").color;
            Assert.Less(Distance(CraftLeather.Colour(w.Rules, w.Piece(id).hide), linen), 1e-4f, "linen is linen-coloured, not the tanner's brown");
            HideState h = w.Piece(id).hide;
            for (int pass = 0; pass < 4; pass++)
                for (int y = 0; y < h.height; y++)
                    Assert.IsTrue(w.Dye(id, "dye_blue", new float[] { 0, y + 0.5f, h.width, y + 0.5f }, 1, out string why), why);
            Assert.Less(Distance(CraftLeather.Colour(w.Rules, h), Rules().Supply("dye_blue").color), 0.1f, "dyed with woad, it is blue");
        }

        // ------------------------------------------------------------------ leather-r03: no rot in the tan liquor
        [Test]
        public void Rot_ARawHideInATanningLiquor_IsKept_InPlainWaterOrASpentLiquorItRots()
        {
            // the path's way: fleshed, straight into a weak bark liquor at the workshop's 18 °C
            CraftWorkshop w = Workshop();
            string id = Fleshed(w, "cow");
            Assert.IsTrue(w.FillVat(0, "bark", 3, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Assert.IsTrue(CraftLeather.Preserved(w, w.Piece(id)), "a tanning liquor keeps it");
            Hours(w, 120);
            Assert.IsFalse(w.Piece(id).HasDefect(CraftDefectKind.Rot), "tanning, not rotting");
            Assert.Less(Cells(w, id).Max(c => c.rot), CraftLeather.SpoilsFrom, "no cell spoiled");

            CraftWorkshop water = Workshop();
            string soaked = Fleshed(water, "cow");
            Assert.IsTrue(water.FillVat(0, "water", 1, out why), why);
            Assert.IsTrue(water.MovePiece(soaked, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Assert.IsFalse(CraftLeather.Preserved(water, water.Piece(soaked)), "plain water does not keep a raw hide");
            Hours(water, 70);
            Assert.IsTrue(water.Piece(soaked).HasDefect(CraftDefectKind.Rot), "left raw in water, it rots");

            CraftWorkshop spent = Workshop();
            string weak = Fleshed(spent, "cow");
            Assert.IsTrue(spent.FillVat(0, "bark", 1, out why), why);
            spent.Vats[0].strength = CraftLeather.PreservesFrom * 0.5; // used up
            Assert.IsTrue(spent.MovePiece(weak, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Assert.IsFalse(CraftLeather.Preserved(spent, spent.Piece(weak)), "a spent liquor keeps nothing");
            Assert.IsTrue(CraftLeather.Rots(spent.Piece(weak).hide.cells.First(c => c.present), false));
        }

        [Test]
        public void Rot_InAWarmVat_GoesAtTheLiquorsWarmth()
        {
            Assert.AreEqual(1, CraftLeather.RotWarmth(20), 1e-9);
            Assert.AreEqual(4, CraftLeather.RotWarmth(40), 1e-9, "fastest about 40 °C");
            Assert.AreEqual(4, CraftLeather.RotWarmth(70), 1e-9, "hotter it cooks, it does not rot faster");
            double Rot(double heat)
            {
                CraftWorkshop w = Workshop();
                string id = Fleshed(w, "cow");
                Assert.IsTrue(w.FillVat(0, "water", 1, out string why), why);
                if (heat > 0) Assert.IsTrue(w.SetVatHeat(0, heat, out why), why);
                Hours(w, 3);
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
                Hours(w, 10);
                return Mean(w, id, c => c.rot);
            }
            Assert.Greater(Rot(0.2), Rot(0) * 1.5, "a raw hide in warm water rots faster than in the cold workshop's air would make it");
        }

        [Test]
        public void Rotting_IsToldOnlyWhileItRots_SpoilingOnlyPastItsLine()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            w.RunSteps(300); // half a minute on the bench
            Assert.Greater(Mean(w, id, c => c.rot), 0.05, "it has started");
            double most = CraftLeather.Rotting(w, w.Piece(id), out bool spoiling);
            Assert.Greater(most, 0, "it rots");
            Assert.IsFalse(spoiling, "but nothing spoils yet");
            Assert.Less(most, CraftLeather.SpoilsFrom);
            Assert.IsTrue(w.FillVat(0, "lime", 2, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Assert.AreEqual(0, CraftLeather.Rotting(w, w.Piece(id), out spoiling), 1e-12, "in lime it does not rot");
        }

        // ------------------------------------------------------------------ leather-r04: states held, not stacked
        [Test]
        public void HoldDefect_IsAsBadAsTheStateIs_AddDefectStacksSeparateHarms()
        {
            var p = new CraftPiece { kind = CraftPieceKind.Hide };
            p.HoldDefect(CraftDefectKind.Rot, -1, 0.3, true, "rot");
            p.HoldDefect(CraftDefectKind.Rot, -1, 0.3, true, "rot");
            Assert.AreEqual(0.3, p.defects.Single(d => d.kind == CraftDefectKind.Rot).severity, 1e-12, "told again, no worse");
            p.HoldDefect(CraftDefectKind.Rot, -1, 0.2, true, "rot");
            Assert.AreEqual(0.3, p.defects.Single(d => d.kind == CraftDefectKind.Rot).severity, 1e-12, "it does not heal by being told less");
            p.HoldDefect(CraftDefectKind.Rot, -1, 0.45, true, "rot");
            Assert.AreEqual(0.45, p.defects.Single(d => d.kind == CraftDefectKind.Rot).severity, 1e-12, "it grows as the state worsens");
            p.AddDefect(CraftDefectKind.Score, 3, 0.2, true, "score");
            p.AddDefect(CraftDefectKind.Score, 3, 0.2, true, "score");
            Assert.AreEqual(0.26, p.defects.Single(d => d.kind == CraftDefectKind.Score).severity, 1e-12, "two scores on one cell are worse than one");
        }

        [Test]
        public void Shrunk_InHotWater_IsNoWorseThanTheHideShrank()
        {
            CraftWorkshop w = Workshop();
            string deer = Fleshed(w, "deer");
            Tan(w, deer, 0, 120);
            Assert.IsTrue(w.FillVat(1, "water", 1, out string why), why);
            Assert.IsTrue(w.SetVatHeat(1, 1, out why), why);
            Hours(w, 2);
            Assert.IsTrue(w.MovePiece(deer, CraftLocation.Vat, out why, 1, 0, 9999, 1), why);
            w.RunSteps(300);
            CraftDefect shrunk = w.Piece(deer).defects.Single(d => d.kind == CraftDefectKind.Shrunk);
            Assert.LessOrEqual(shrunk.severity, Cells(w, deer).Max(c => c.shrink) + 1e-9, "as shrunk as its most shrunken cell at most");
            Assert.AreEqual(Mean(w, deer, c => c.shrink), shrunk.severity, 0.05, "about its mean shrink");
        }

        [Test]
        public void Rot_InWater_IsNoWorseThanTheHideRotted_AndACaseHardenedHideTheShareThatClosed()
        {
            CraftWorkshop w = Workshop();
            string id = Fleshed(w, "cow");
            Assert.IsTrue(w.FillVat(0, "water", 1, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Hours(w, 120);
            CraftDefect rot = w.Piece(id).defects.Single(d => d.kind == CraftDefectKind.Rot);
            Assert.LessOrEqual(rot.severity, Cells(w, id).Max(c => c.rot) + 1e-9, "no more rotten than its most rotten cell");

            CraftWorkshop s = Workshop();
            string raw = Fleshed(s, "deer");
            Assert.IsTrue(s.FillVat(0, "bark", 8, out why), why); // full strength on a raw hide: its outside closes
            Assert.IsTrue(s.MovePiece(raw, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Hours(s, 30);
            CraftDefect closed = s.Piece(raw).defects.Single(d => d.kind == CraftDefectKind.CaseHardened);
            Assert.That(closed.severity, Is.InRange(0.0, 1.0), "the share of the hide whose outside closed");
        }

        /// <summary>The same rule wherever a state is told again while it lasts: a burning stretch, scale hammered in blow after blow.</summary>
        [Test]
        public void HeldStates_BurntAndScalePitted_AreAsBadAsTheMetalIs()
        {
            CraftWorkshop w = Workshop();
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 120, 20, 5, CraftDefaults.StockMetals().First(m => m.name == "Tool steel").composition));
            Assert.IsTrue(w.AddFuel("charcoal", 10, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out why, 1), why);
            for (int t = 0; t < 3000 && w.Piece(bar) != null && !w.Piece(bar).HasDefect(CraftDefectKind.Burnt); t++) { w.Bellows(1); w.RunSteps(1); }
            for (int t = 0; t < 30 && w.Piece(bar) != null; t++) { w.Bellows(1); w.RunSteps(1); } // three seconds more of burning
            CraftPiece p = w.Piece(bar);
            Assume.That(p != null && p.HasDefect(CraftDefectKind.Burnt), "it burnt");
            foreach (CraftDefect d in p.defects.Where(d => d.kind == CraftDefectKind.Burnt && d.at >= 0 && d.at < p.segments.Count))
                Assert.LessOrEqual(d.severity, p.segments[d.at].body.burnt + 1e-9, "no more burnt than the stretch is");

            CraftWorkshop a = Workshop();
            string hot = a.AddPiece(CraftDefaults.Bar(a.Rules, 200, 30, 6, CraftDefaults.StockMetals().First(m => m.name == "Mild steel").composition));
            for (int k = 0; k < 3; k++)
            {
                foreach (MetalSegment s in a.Piece(hot).segments) { s.scale = 0.4; s.body.temp = s.edge.temp = 1000; } // a thick scale, at a forging heat
                Assert.IsTrue(a.Strike(hot, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out why), why);
            }
            CraftPiece q = a.Piece(hot);
            Assert.IsTrue(q.HasDefect(CraftDefectKind.ScalePit), "scale hammered in pits it");
            double pitted = q.segments.Max(s => s.pit);
            foreach (CraftDefect d in q.defects.Where(d => d.kind == CraftDefectKind.ScalePit))
                Assert.LessOrEqual(d.severity, pitted + 1e-9, "no more pitted than the metal is, blow after blow");
        }

        // ------------------------------------------------------------------ leather-r05: the frame's slackest side
        [Test]
        public void Frame_DriesFlat_OnlyAsFarAsItsSlackestSideHolds()
        {
            Assert.AreEqual(1, CraftLeather.Flatness(new[] { 0.65, 0.65, 0.65, 0.65 }), 1e-12, "every side in the green: flat");
            Assert.AreEqual(0.4, CraftLeather.Flatness(new[] { 0.8, 0.2, 0.8, 0.2 }), 1e-12, "two sides grey: no flatter than they hold");
            Assert.AreEqual(1, CraftLeather.Flatness(new[] { CraftLeather.FlatFrom, CraftLeather.FlatFrom, CraftLeather.FlatFrom, CraftLeather.FlatFrom }), 1e-12, "'taut and even' is flat");
            double Puckered(double[] share)
            {
                CraftWorkshop w = Workshop();
                string id = Fleshed(w, "deer");
                Tan(w, id, 0, 80);
                FrameAndDry(w, id, share);
                return CraftEvaluator.Piece(Rules(), w.Piece(id)).Puckered;
            }
            double taut = Puckered(new[] { 0.65, 0.65, 0.65, 0.65 }), twoSlack = Puckered(new[] { 0.8, 0.2, 0.8, 0.2 }), rack = Puckered(null);
            Assert.Less(taut, 0.002, "taut all round it dries flat");
            Assert.Greater(twoSlack, 0.02, "two sides left grey: it dries puckered, as the window says");
            Assert.Less(twoSlack, rack, "though less than unstretched on the rack");
        }

        // ------------------------------------------------------------------ leather-r10: a stir never unmixes
        [Test]
        public void Stir_ASloppyStirAfterASteadyOne_LeavesItAsMixed()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.FillVat(0, "bark", 2, out string why), why);
            Assert.IsTrue(w.StirVat(0, out why, 1.0), why);
            Assert.AreEqual(1, w.Vats[0].stirred, 1e-6);
            Assert.IsTrue(w.StirVat(0, out why, 0.2), why);
            Assert.AreEqual(1, w.Vats[0].stirred, 1e-6, "two more wobbly turns do not unmix it");
            Hours(w, 3);
            double settled = w.Vats[0].stirred;
            Assert.IsTrue(w.StirVat(0, out why, 0.2), why);
            Assert.AreEqual(Math.Max(settled, CraftWorkshop.StirredBy(0.2)), w.Vats[0].stirred, 1e-6, "a settled liquor stirred sloppily mixes as well as the stir does");
        }

        // ------------------------------------------------------------------ leather-r11: the hair knife on bare grain
        [Test]
        public void HairKnife_ScuffsOnlyWhereUnloosenedHairHolds()
        {
            CraftWorkshop w = Workshop();
            string drake = w.AddPiece(CraftLeather.NewHide(w.Rules, "drake", 3)), cow = Fleshed(w, "cow");
            foreach (string id in new[] { drake, cow })
            {
                HideState h = w.Piece(id).hide;
                for (int y = 0; y < h.height; y++)
                    Assert.IsTrue(w.Scrape(id, new float[] { 0, y + 0.5f, h.width, y + 0.5f }, true, 0.35, 0.5, out string why), why);
            }
            Assert.AreEqual(1, Mean(w, drake, c => c.grain), 1e-12, "a drake has no hair to hold: gentle on bare grain, no scuff");
            Assert.Less(Mean(w, cow, c => c.grain), 0.99, "an unlimed cow's hair holds and the knife scuffs its grain");
            Assert.IsFalse(CraftLeather.HairHolds(new HideCell { hair = 0, hairLoose = 0 }));
            Assert.IsTrue(CraftLeather.HairHolds(new HideCell { hair = 0.5, hairLoose = 0 }));
            Assert.IsFalse(CraftLeather.HairHolds(new HideCell { hair = 0.5, hairLoose = 0.8 }), "limed, it comes away");
        }

        // ------------------------------------------------------------------ leather-r12: the frame in words
        [Test]
        public void Look_WordsTheFrame_ByItsPulls()
        {
            string Laced(double[] share)
            {
                CraftWorkshop w = Workshop();
                string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 2));
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out string why), why);
                double[] lim = Limits(w, id);
                Assert.IsTrue(w.Stretch(id, lim[0] * share[0], lim[1] * share[1], lim[2] * share[2], lim[3] * share[3], out why), why);
                return CraftLook.Describe(w.Rules, w.Piece(id), null, CraftInstruments.None, false, false).FirstOrDefault(l => l.StartsWith("It is laced"));
            }
            Assert.AreEqual("It is laced slack in the frame.", Laced(new[] { 0.1, 0.1, 0.1, 0.1 }));
            Assert.AreEqual("It is laced taut in the frame.", Laced(new[] { 0.65, 0.65, 0.65, 0.65 }));
            Assert.AreEqual("It is laced unevenly in the frame.", Laced(new[] { 0.7, 0.6, 0.05, 0.6 }));
            Assert.AreEqual("It is laced overtight in the frame: an edge will thin as it dries.", Laced(new[] { 0.95, 0.65, 0.65, 0.65 }));
        }

        // ------------------------------------------------------------------ leather-r18: chalking leather
        [Test]
        public void Mastery_ChalkingLeather_IsLeatherwork_ChalkingCloth_Tailoring()
        {
            CraftWorkshop w = Workshop();
            string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2)), cloth = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 2));
            Assert.AreEqual(CraftDiscipline.Leatherwork, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(CraftRequestKind.LayoutPattern, hide) { id = "strap" }));
            Assert.AreEqual(CraftDiscipline.Leatherwork, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(CraftRequestKind.Cut, hide) { id = "strap" }), "as cutting it is");
            Assert.AreEqual(CraftDiscipline.Tailoring, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(CraftRequestKind.LayoutPattern, cloth) { id = "hood" }));
        }

        // ------------------------------------------------------------------ leather-r07: the rack's four places
        [Test]
        public void Rack_HangsFourHides_AFifthWaitsUntilOneIsTakenDown()
        {
            CraftWorkshop w = Workshop();
            var ids = Enumerable.Range(0, 5).Select(k => w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 10 + k))).ToList();
            string why;
            for (int k = 0; k < CraftLeather.RackHides; k++) Assert.IsTrue(w.MovePiece(ids[k], CraftLocation.Rack, out why), why);
            Assert.IsFalse(w.MovePiece(ids[4], CraftLocation.Rack, out why), "the rack is full");
            Assert.AreEqual("The rack is full: take a hide down first.", why);
            Assert.IsTrue(w.MovePiece(ids[0], CraftLocation.Air, out why), why);
            Assert.IsTrue(w.MovePiece(ids[4], CraftLocation.Rack, out why), "one taken down, another hangs: " + why);
        }

        // ------------------------------------------------------------------ leather-r19: the bucket asks first
        [Test]
        public void Vat_AnotherLiquor_IsRefusedBeforeAnythingIsPoured()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.FillVat(0, "bark", 2, out string why), why);
            int refused = 0;
            w.EventRaised += e => { if (e.Kind == CraftEventKind.Refused) refused++; };
            Assert.IsFalse(w.CanFillVat(0, "lime", out why));
            Assert.AreEqual("The vat holds something else: empty it first.", why);
            Assert.AreEqual(0, refused, "asking refuses nothing");
            Assert.IsTrue(w.CanFillVat(0, "bark", out why), "more of the same strengthens it");
            Assert.IsTrue(w.CanFillVat(0, "water", out why), "water thins it");
            Assert.IsTrue(w.CanFillVat(1, "lime", out why), "an empty vat takes anything");
            Assert.IsFalse(w.FillVat(0, "lime", 1, out string said), "and filling refuses it as the bucket said");
            Assert.AreEqual("The vat holds something else: empty it first.", said);
            Assert.AreEqual(1, refused, "refused once, by the workshop");
        }

        // ------------------------------------------------------------------ leather-r16: the chalked cut
        [Test]
        public void Chalk_ACutAlongIt_IsCleaner_AndThePartsLookSaysSo()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            Assert.IsTrue(w.LayoutPattern(id, "strap", 6, 6, 0, false, out string why), why);
            Assert.GreaterOrEqual(CraftLeather.ChalkAt(w.Piece(id).hide, "strap", 6, 6, 0), 0, "chalked there");
            Assert.Less(CraftLeather.ChalkAt(w.Piece(id).hide, "strap", 6, 6, 1), 0, "not turned so");
            string part = w.CutPattern(id, "strap", 6, 6, 0, 0.83, out why);
            Assert.IsNotNull(part, why);
            Assert.AreEqual(0.83 + CraftLeather.ChalkedCut, w.Piece(part).hide.cutQuality, 1e-6, "along the chalk it comes out cleaner");
            Assert.Less(CraftLeather.ChalkAt(w.Piece(id).hide, "strap", 6, 6, 0), 0, "the chalk is cut away");
            CollectionAssert.Contains(CraftLook.Describe(w.Rules, w.Piece(part), null, CraftInstruments.None, false, false), "Its edges are cut clean.");
        }

        // ------------------------------------------------------------------ leather-r09, r15: what marked each cell
        [Test]
        public void WorkMarks_TellTheKnifeFromTheFrameFromTheRest_AndALightScoreIsMarked()
        {
            CraftWorkshop w = Workshop();
            string cow = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            HideState h = w.Piece(cow).hide;
            foreach (HideCell c in h.cells) c.flesh = 0;
            for (int x = 6; x < 10; x++) h.Cell(x, 6).thickness = 2.0; // a thinner stretch of the belly
            Assert.IsTrue(w.Scrape(cow, new float[] { 6.5f, 6.5f, 9.5f, 6.5f }, false, 1, 1, out string why), why);
            int[] by = CraftLeather.WorkMarks(w.Piece(cow), 0.02);
            List<CraftDefect> scores = w.Piece(cow).defects.Where(d => d.kind == CraftDefectKind.Score && d.at >= 0).ToList();
            Assert.IsNotEmpty(scores);
            Assert.That(scores.Select(d => by[d.at]), Has.All.EqualTo(CraftLeather.ByKnife), "the knife's scores");
            Assert.That(scores.Select(d => h.cells[d.at].damage), Has.All.LessThan(0.3), "light ones, under the table's red rings - but marked all the same");

            string deer = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 2));
            Assert.IsTrue(w.MovePiece(deer, CraftLocation.Frame, out why), why);
            Assert.IsTrue(w.Stretch(deer, 1, 1, 0, 0, out why), why); // pulled too hard: it tears
            int[] torn = CraftLeather.WorkMarks(w.Piece(deer));
            List<CraftDefect> tears = w.Piece(deer).defects.Where(d => d.kind == CraftDefectKind.Tear).ToList();
            Assert.IsNotEmpty(tears);
            Assert.That(tears.Select(d => torn[d.at]), Has.All.EqualTo(CraftLeather.ByFrame), "the frame's tears are no knife's");
            Assert.IsFalse(torn.Contains(CraftLeather.ByKnife), "no knife touched it");

            HideCell spoilt = w.Piece(deer).hide.cells.First(c => c.present && torn[w.Piece(deer).hide.cells.IndexOf(c)] == 0);
            spoilt.damage += 0.1; // rot, a burn: the work's, not a scar
            Assert.AreEqual(CraftLeather.ByOther, CraftLeather.WorkMarks(w.Piece(deer))[w.Piece(deer).hide.cells.IndexOf(spoilt)]);
            Assert.AreEqual(0.1, CraftLeather.Worked(spoilt), 1e-9);
        }

        // ------------------------------------------------------------------ leather-r06: a stroke eased as it goes
        [Test]
        public void Fleshing_AStrokeEasedOverTheThinBelly_IsWorkedAtThePressureOfEachStretch()
        {
            // the window sends the stretch drawn so far when the wheel turns: the thick back at 0.65, then the thin belly at 0.35
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5));
            HideState h = w.Piece(id).hide;
            const int row = 4;
            for (int x = 0; x < h.width; x++)
            {
                HideCell c = h.Cell(x, row);
                Assume.That(c != null && c.present);
                c.flesh = 0;
                c.thickness = x < 5 ? 3.0 : 1.2;
            }
            Assume.That(0.65, Is.LessThan(CraftLeather.SafeFleshingPressure(h.Cell(0, row), 0.7)), "green over the back");
            Assume.That(0.35, Is.LessThan(CraftLeather.SafeFleshingPressure(h.Cell(9, row), 0.7)), "green over the belly");
            double thin0 = Enumerable.Range(5, 5).Sum(x => h.Cell(x, row).damage);
            Assert.IsTrue(w.Scrape(id, new float[] { 0.5f, row + 0.5f, 4.5f, row + 0.5f }, false, 0.65, 0.7, out string why), why);
            Assert.IsTrue(w.Scrape(id, new float[] { 4.5f, row + 0.5f, 9.5f, row + 0.5f }, false, 0.35, 0.7, out why), why);
            Assert.AreEqual(thin0, Enumerable.Range(5, 5).Sum(x => h.Cell(x, row).damage), 1e-9, "in the green all along, nothing scored");
        }

        // ------------------------------------------------------------------ leather-r14: one pass of the skiving knife
        [Test]
        public void Skive_OnePassAtTheGaugesLine_DoesNotGoThrough_PastItItDoes()
        {
            double Holes(double depth)
            {
                CraftWorkshop w = Workshop();
                string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5));
                HideState h = w.Piece(id).hide;
                const int row = 4;
                for (int x = 0; x < h.width; x++) h.Cell(x, row).thickness = 1.2;
                Assert.IsTrue(w.Skive(id, new List<float> { 0.5f, row + 0.5f, h.width - 0.5f, row + 0.5f }, depth, out string why), why);
                return w.Piece(id).defects.Count(d => d.kind == CraftDefectKind.Hole);
            }
            double line = (1.2 - CraftLeather.SkivedThrough) / CraftLeather.SkivePass; // the window's DEPTH line for these cells
            Assert.AreEqual(0, Holes(line - 0.02), "one pass in the green does not go through");
            Assert.Greater(Holes(line + 0.15), 0, "one pass well past the line does");
        }
    }
}
#endif
