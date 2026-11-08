#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the tannery and the leather cutting table's rules (wave 1, leather): the frame (tearing only by
    /// what a pull adds, each edge's limit, drying flat, puckered or askew, overdrawn edges thinning), the fleshing knife's
    /// angle and the pressure a cell can bear, knife scores and holes kept on their cells and going with the part cut there,
    /// oil, wax and dye used by what the leather takes in, re-soaking, the air's humidity, heat in every liquor, lime's
    /// warmth, liquor made in plain water, the paddle's steadiness, parts not cut again, slicking, the scars a hide is born
    /// with, the cut's cleanness bounded by the hands, the vats' colours, and what the item and the calipers say.
    /// </summary>
    public class CraftLeatherAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "leather");

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

        /// <summary>Fleshed, tanned in bark for <paramref name="hours"/>, lifted out onto the bench (wet).</summary>
        private static string Tanned(CraftWorkshop w, string hideId, double hours = 80)
        {
            string id = Fleshed(w, hideId);
            Assert.IsTrue(w.FillVat(0, "bark", 3, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Hours(w, hours);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            return id;
        }

        private static void DryUntilDried(CraftWorkshop w, string id, double most = 80)
        {
            for (double t = 0; t < most && !w.Piece(id).hide.dried; t += 1) Hours(w, 1);
            Assert.IsTrue(w.Piece(id).hide.dried, "it never dried");
        }

        private static double[] Limits(CraftWorkshop w, string id) => Enumerable.Range(0, 4).Select(k => CraftLeather.EdgeLimit(w.Rules, w.Piece(id).hide, k)).ToArray();

        // ------------------------------------------------------------------ the frame
        [Test]
        public void Frame_ASideSentAgainUnchanged_NeverTearsAgain()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "rabbit", 2));
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out string why), why);
            Assert.IsTrue(w.Stretch(id, 0.4, 0.4, 0.4, 0.4, out why), why);
            double torn = Mean(w, id, c => c.damage);
            Assert.IsTrue(w.Piece(id).HasDefect(CraftDefectKind.Tear), "0.4 is more than a rabbit's rim can take");
            for (int k = 0; k < 3; k++)
            {
                // what one handle's release sends: the other three sides as they were
                Assert.IsTrue(w.Stretch(id, 0.4, 0.4, 0.4, 0.4, out why), why);
                Assert.AreEqual(torn, Mean(w, id, c => c.damage), 1e-12, "the same pull sent again tears nothing more");
            }
            Assert.IsTrue(w.Stretch(id, 0.5, 0.4, 0.4, 0.4, out why), why);
            Assert.Greater(Mean(w, id, c => c.damage), torn, "pulled harder, it tears by what the pull added");
        }

        [Test]
        public void Frame_EachSideTearsPastWhatItsWeakestEdgeCellCanTake()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 2));
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out string why), why);
            double[] lim = Limits(w, id);
            Assert.That(lim, Has.All.InRange(0.25, 0.7), "a deer's rim takes about half a pull");
            Assert.IsTrue(w.Stretch(id, lim[0] * 0.97, 0, 0, 0, out why), why);
            Assert.IsFalse(w.Piece(id).HasDefect(CraftDefectKind.Tear), "just under its limit the top holds");
            Assert.IsTrue(w.Stretch(id, Math.Min(1, lim[0] * 1.3), 0, 0, 0, out why), why);
            List<CraftDefect> tears = w.Piece(id).defects.Where(d => d.kind == CraftDefectKind.Tear).ToList();
            Assert.IsNotEmpty(tears, "past it, it tears");
            int[] edges = CraftLeather.EdgeMask(w.Piece(id).hide);
            Assert.That(tears.Select(d => edges[d.at] & 1), Has.All.EqualTo(1), "only the top (north) edge, drawn at the top, tears");
            Assert.Less(CraftLeather.EdgeLimit(w.Rules, w.Piece(id).hide, 0), lim[0], "torn, that edge is weaker now");
        }

        [Test]
        public void Frame_ALacedHideStartsSlack_AndTheFrameHoldsOne()
        {
            CraftWorkshop w = Workshop();
            string a = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 2)), b = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 3));
            Assert.IsTrue(w.MovePiece(a, CraftLocation.Frame, out string why), why);
            Assert.IsTrue(w.Stretch(a, 0.2, 0.2, 0.2, 0.2, out why), why);
            Assert.IsFalse(w.MovePiece(b, CraftLocation.Frame, out why), "one hide in the frame at a time");
            StringAssert.Contains("frame already holds", why);
            Assert.IsTrue(w.MovePiece(a, CraftLocation.Air, out why), why);
            Assert.IsTrue(w.MovePiece(a, CraftLocation.Frame, out why), why);
            HideState h = w.Piece(a).hide;
            Assert.AreEqual(0, h.tensionN + h.tensionE + h.tensionS + h.tensionW, 1e-12, "laced in again, it starts slack");
        }

        /// <summary>A tanned deer laced in the frame and pulled to these shares of each side's limit (or hung on the rack: null), dried.</summary>
        private static (CraftWorkshop w, string id, double thick0) DriedFramed(double[] share)
        {
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "deer");
            double thick0 = Mean(w, id, c => c.thickness);
            if (share == null)
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out string why), why);
            else
            {
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out string why), why);
                double[] lim = Limits(w, id);
                Assert.IsTrue(w.Stretch(id, lim[0] * share[0], lim[1] * share[1], lim[2] * share[2], lim[3] * share[3], out why), why);
            }
            DryUntilDried(w, id);
            return (w, id, thick0);
        }

        [Test]
        public void Frame_TautAndEven_DriesFlat_SlackOrOnTheRack_Puckers()
        {
            var rack = DriedFramed(null);
            var slack = DriedFramed(new[] { 0.0, 0.0, 0.0, 0.0 });
            var taut = DriedFramed(new[] { 0.65, 0.65, 0.65, 0.65 });
            CraftPieceReport r = CraftEvaluator.Piece(Rules(), rack.w.Piece(rack.id));
            Assert.Greater(r.Puckered, 0.03, "unstretched on the rack it puckers as it shrinks");
            CollectionAssert.Contains(r.Defects, "dried puckered: it was not stretched");
            Assert.AreEqual(r.Puckered, CraftEvaluator.Piece(Rules(), slack.w.Piece(slack.id)).Puckered, 0.005, "laced slack is no better than the rack");
            CraftPieceReport t = CraftEvaluator.Piece(Rules(), taut.w.Piece(taut.id));
            Assert.Less(t.Puckered, 0.002, "pulled taut all round it dries flat");
            Assert.AreEqual(0, t.Distortion, 1e-9, "and square");
            Assert.IsFalse(taut.w.Piece(taut.id).HasDefect(CraftDefectKind.Tear), "within its limits it never tore");
        }

        [Test]
        public void Frame_PulledUnevenly_DriesOutOfSquare_Overdrawn_ItsEdgesThin()
        {
            var uneven = DriedFramed(new[] { 0.7, 0.6, 0.05, 0.6 });
            CraftPieceReport u = CraftEvaluator.Piece(Rules(), uneven.w.Piece(uneven.id));
            Assert.Greater(u.Distortion, 0.05, "top pulled hard, bottom slack: it dries askew");
            CollectionAssert.Contains(u.Defects, "dried out of square");
            Assert.AreEqual(u.Distortion, CraftLeather.Unevenness(new[] { 0.7, 0.6, 0.05, 0.6 }) - CraftLeather.UnevenFrom, 0.15, "by how uneven the pull was");
            var taut = DriedFramed(new[] { 0.65, 0.65, 0.65, 0.65 });
            var over = DriedFramed(new[] { 0.97, 0.97, 0.97, 0.97 });
            Assert.IsFalse(over.w.Piece(over.id).HasDefect(CraftDefectKind.Tear), "under the limit it does not tear...");
            Assert.Less(Mean(over.w, over.id, c => c.thickness), Mean(taut.w, taut.id, c => c.thickness) - 0.005, "...but overdrawn, its edges dry thin");
        }

        // ------------------------------------------------------------------ the beam
        [Test]
        public void Fleshing_ASteepKnifeBitesSooner_AFlatOneIsGentlerAndTakesLess()
        {
            double Damage(double angle, out double flesh)
            {
                CraftWorkshop w = Workshop();
                string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5));
                CraftPiece h = w.Piece(id);
                double d0 = Mean(w, id, c => c.damage), f0 = Mean(w, id, c => c.flesh);
                for (int y = 0; y < h.hide.height; y++)
                    w.Scrape(id, new float[] { 0, y + 0.5f, h.hide.width, y + 0.5f }, false, 0.6, angle, out _);
                flesh = f0 - Mean(w, id, c => c.flesh);
                return Mean(w, id, c => c.damage) - d0;
            }
            double steep = Damage(1, out double fleshSteep), flat = Damage(0, out double fleshFlat);
            Assert.Greater(steep, flat, "steep bites");
            Assert.Greater(fleshSteep, fleshFlat, "and takes more flesh");
            // the window's starting hold (0.35 pressure, 0.7 angle) fleshes a deer without scoring it
            CraftWorkshop d = Workshop();
            string deer = Fleshed(d, "deer", 5);
            Assert.IsFalse(d.Piece(deer).HasDefect(CraftDefectKind.Score));
        }

        [Test]
        public void Fleshing_ScoresACleanCellOnlyPastWhatItCanBear()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5));
            HideState h = w.Piece(id).hide;
            foreach (HideCell c in h.cells) c.flesh = 0; // scraped clean
            var stroke = new List<float> { 3, 4.5f, 6, 4.5f };
            double safe = double.MaxValue;
            CraftLeather.StrokeCells(h, stroke, CraftLeather.ScrapeReach, (i, c, wt) => safe = Math.Min(safe, CraftLeather.SafeFleshingPressure(c, 0.7)));
            double d0 = Mean(w, id, c => c.damage);
            Assert.IsTrue(w.Scrape(id, stroke, false, safe - 0.01, 0.7, out string why), why);
            Assert.AreEqual(d0, Mean(w, id, c => c.damage), 1e-12, "within what the cells can bear: no harm");
            Assert.IsTrue(w.Scrape(id, stroke, false, Math.Min(1, safe + 0.2), 0.7, out why), why);
            Assert.Greater(Mean(w, id, c => c.damage), d0, "past it the knife scores");
        }

        [Test]
        public void KnifeScores_StayOnTheirCells_AndGoWithThePartCutThere()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            CraftPiece hide = w.Piece(id);
            foreach (HideCell c in hide.hide.cells) c.flesh = 0;
            for (int k = 0; k < 3; k++)
                Assert.IsTrue(w.Scrape(id, new float[] { 2.5f, 2.5f, 5.5f, 2.5f }, false, 1, 1, out string why), why);
            List<CraftDefect> scores = hide.defects.Where(d => d.kind == CraftDefectKind.Score).ToList();
            Assert.IsNotEmpty(scores);
            Assert.That(scores.Select(d => d.at), Has.All.GreaterThanOrEqualTo(0), "each score is on the cell it was made");
            hide.AddDefect(CraftDefectKind.Rot, -1, 0.2, true, "left raw and wet too long");
            hide.AddDefect(CraftDefectKind.Score, -1, 0.75, true, "an old save's score on the whole hide");
            string far = w.CutPattern(id, "strap", 12, 9, 0, 0.9, out string why2);
            Assert.IsNotNull(far, why2);
            Assert.IsFalse(w.Piece(far).defects.Any(d => d.kind == CraftDefectKind.Score), "a slip in one corner does not follow a part cut from the other side");
            Assert.IsTrue(w.Piece(far).HasDefect(CraftDefectKind.Rot), "what the whole hide went through does");
            int before = scores.Count;
            string near = w.CutPattern(id, "strap", 2, 2, 0, 0.9, out why2);
            Assert.IsNotNull(near, why2);
            List<CraftDefect> moved = w.Piece(near).defects.Where(d => d.kind == CraftDefectKind.Score && d.at >= 0).ToList();
            Assert.IsNotEmpty(moved, "the part cut over the scores carries them");
            Assert.That(moved.Select(d => d.at), Has.All.InRange(0, 3), "where they are in the part (a strap's four cells)");
            Assert.Less(hide.defects.Count(d => d.kind == CraftDefectKind.Score && d.at >= 0), before, "and the hide no longer counts them");
        }

        [Test]
        public void Tears_GoWithThePartCutWhereTheyAre()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 2));
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Frame, out string why), why);
            Assert.IsTrue(w.Stretch(id, 1, 0, 0, 0, out why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            HideState h = w.Piece(id).hide;
            CraftPattern strap = w.Rules.Pattern("strap");
            int tearAt = w.Piece(id).defects.First(d => d.kind == CraftDefectKind.Tear).at;
            int tx = tearAt % h.width, ty = tearAt / h.width;
            int gx = Enumerable.Range(tx - 3, 4).Where(x => CraftLeather.Fits(h, strap, x, ty, 0)).DefaultIfEmpty(int.MinValue).First();
            Assume.That(gx, Is.Not.EqualTo(int.MinValue), "a strap fits over the torn cell");
            string part = w.CutPattern(id, "strap", gx, ty, 0, 0.9, out why);
            Assert.IsNotNull(part, why);
            Assert.IsTrue(w.Piece(part).defects.Any(d => d.kind == CraftDefectKind.Tear && d.at == tx - gx), "the tear is in the part, at its own cell");
        }

        [Test]
        public void OilWaxAndDye_AreUsedByWhatTheLeatherTakesIn()
        {
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "cow", 120);
            HideState h = w.Piece(id).hide;
            var row = new List<float> { 0, 6.5f, h.width, 6.5f };
            var zig = new List<float>();
            for (int y = 0; y < h.height; y++) zig.AddRange(new[] { y % 2 == 0 ? 0f : h.width, y + 0.5f, y % 2 == 0 ? h.width : 0f, y + 0.5f });
            CraftSupplyDefinition dye = w.Rules.Supply("dye_black"), oil = w.Rules.Supply("neatsfoot_oil"), wax = w.Rules.Supply("beeswax");
            Assert.Greater(CraftLeather.Uptake(h, dye, zig, 0.5), 5 * CraftLeather.Uptake(h, dye, row, 0.5), "a scribble over the hide takes far more than one row");
            double expect = CraftLeather.Uptake(h, dye, row, 0.5), was = Mean(w, id, c => c.dye) * Cells(w, id).Count();
            Assert.IsTrue(w.Dye(id, "dye_black", row, 0.5, out string why), why);
            Assert.AreEqual(expect, Mean(w, id, c => c.dye) * Cells(w, id).Count() - was, 1e-6, "the uptake is exactly what the dye put in");
            expect = CraftLeather.Uptake(h, oil, row, 0.5);
            was = Mean(w, id, c => c.oil) * Cells(w, id).Count();
            Assert.IsTrue(w.Condition(id, "neatsfoot_oil", row, 0.5, out why), why);
            Assert.AreEqual(expect, Mean(w, id, c => c.oil) * Cells(w, id).Count() - was, 1e-6, "and the oil's");
            expect = CraftLeather.Uptake(h, wax, null, 0.5);
            was = Mean(w, id, c => c.wax) * Cells(w, id).Count();
            Assert.IsTrue(w.Condition(id, "beeswax", null, 0.5, out why), why);
            Assert.AreEqual(expect, Mean(w, id, c => c.wax) * Cells(w, id).Count() - was, 1e-6, "and the wax's, worked in all over");
            Assert.Less(CraftLeather.Uptake(h, dye, row, 0.5), expect * 0.5, "oiled and waxed, the leather takes the dye poorly");
            Assert.AreEqual(12f, dye.cellsPerItem, 1e-6, "a pot of dye covers about a dozen cells");
        }

        [Test]
        public void OilWaxAndDye_ArePaidForFromThePotOpened_ByWhatIsTakenIn()
        {
            // a stroke that takes nothing in costs nothing
            Assert.AreEqual(0, CraftLeather.PotsToOpen(0, 0, 12, out double left));
            Assert.AreEqual(0, left, 1e-12);
            // the first stroke opens a pot; what is left in it is used before another is opened
            Assert.AreEqual(1, CraftLeather.PotsToOpen(5, 0, 12, out left));
            Assert.AreEqual(7, left, 1e-9);
            Assert.AreEqual(0, CraftLeather.PotsToOpen(6.5, left, 12, out left), "the open pot still covers it");
            Assert.AreEqual(0.5, left, 1e-9);
            Assert.AreEqual(1, CraftLeather.PotsToOpen(3, left, 12, out left), "it runs dry: the next is opened");
            Assert.AreEqual(9.5, left, 1e-9);
            // a scribble over a whole cow pays for all it covers: as many pots as careful strokes over the same leather
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "cow", 120);
            HideState h = w.Piece(id).hide;
            CraftSupplyDefinition dye = w.Rules.Supply("dye_black");
            var zig = new List<float>();
            for (int y = 0; y < h.height; y++) zig.AddRange(new[] { y % 2 == 0 ? 0f : h.width, y + 0.5f, y % 2 == 0 ? h.width : 0f, y + 0.5f });
            int scribble = CraftLeather.PotsToOpen(CraftLeather.Uptake(h, dye, zig, 0.5), 0, dye.cellsPerItem, out _);
            int careful = 0;
            double pot = 0;
            for (int y = 0; y < h.height; y++)
                careful += CraftLeather.PotsToOpen(CraftLeather.Uptake(h, dye, new List<float> { 0, y + 0.5f, h.width, y + 0.5f }, 0.5), pot, dye.cellsPerItem, out pot);
            Assert.Greater(scribble, 1, "one gesture over the whole hide is not one pot");
            Assert.AreEqual(careful, scribble, 1, "row by row costs about what the scribble does");
        }

        [Test]
        public void ResoakedLeather_IsNoLongerDried_AndDriesAgain()
        {
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "deer");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out string why), why);
            DryUntilDried(w, id);
            Assert.IsTrue(w.FillVat(1, "water", 1, out why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 1), why);
            Hours(w, 2);
            Assert.IsFalse(w.Piece(id).hide.dried, "sodden again, it is not dry");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out why), why);
            DryUntilDried(w, id);
        }

        [Test]
        public void Drying_ReadsTheAirAsWoodAndFibreDo()
        {
            double MoistureAfter(double? humidity)
            {
                CraftWorkshop w = Workshop();
                string id = Tanned(w, "deer");
                if (humidity.HasValue) w.SetVariable("humidity", humidity.Value);
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out string why), why);
                Hours(w, 3);
                return Mean(w, id, c => c.moisture);
            }
            double unset = MoistureAfter(null);
            Assert.AreEqual(MoistureAfter(0.5), unset, 1e-9, "no weather: the ordinary humidity (0.5), as wood and fibre read it");
            Assert.Greater(MoistureAfter(0.9), unset, "damp air dries it slower");
            Assert.Less(MoistureAfter(0.1), unset, "dry air faster");
        }

        // ------------------------------------------------------------------ the vats
        [Test]
        public void HotLime_LoosensNoFasterPast30Degrees_AndCooksARawHide()
        {
            (double loose, double shrink, double damage) Limed(double heat)
            {
                CraftWorkshop w = Workshop();
                string id = Fleshed(w, "cow");
                Assert.IsTrue(w.FillVat(0, "lime", 1, out string why), why);
                Assert.IsTrue(w.SetVatHeat(0, heat, out why), why);
                Hours(w, 1);
                double d0 = Mean(w, id, c => c.damage);
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
                Hours(w, 2);
                return (Mean(w, id, c => c.hairLoose), Mean(w, id, c => c.shrink), Mean(w, id, c => c.damage) - d0);
            }
            var cold = Limed(0);
            var warm = Limed(0.2); // about 34 °C
            var boiling = Limed(1);
            Assert.Greater(warm.loose, cold.loose, "warmth helps the lime...");
            Assert.AreEqual(warm.loose, boiling.loose, 1e-6, "...but no further past about 30 °C");
            Assert.AreEqual(2, CraftLeather.LimeWarmth(96), 1e-12);
            Assert.Greater(boiling.shrink, 0.05, "near boiling, a raw hide shrinks in lime as in water");
            Assert.Greater(boiling.damage, 0.01, "and cooks");
            Assert.AreEqual(0, warm.shrink, 1e-9);
        }

        [Test]
        public void AnyLiquor_PastTheLeathersShrinkTemperature_ShrinksIt()
        {
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "deer", 120);
            Assert.Greater(CraftLeather.MeanTannage(w.Piece(id).hide), 0.6, "well tanned");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out string why, 1, 0, 9999, 0), why);
            Assert.IsTrue(w.SetVatHeat(0, 1, out why), why);
            Hours(w, 3);
            Assert.Greater(Mean(w, id, c => c.shrink), 0.1, "boiled in bark liquor, tanned leather shrinks past its shrink temperature too");
        }

        [Test]
        public void AnAgentPouredIntoPlainWater_MakesItsLiquorThere()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.FillVat(0, "water", 1, out string why), why);
            Assert.IsTrue(w.FillVat(0, "bark", 2, out why), "water is the liquor's solvent: " + why);
            Assert.AreEqual("bark", w.Vats[0].agent);
            Assert.AreEqual(0.3, w.Vats[0].strength, 1e-6, "two doses of bark (0.15 each)");
            Assert.IsFalse(w.FillVat(0, "lime", 1, out why), "but lime into bark liquor: no");
        }

        [Test]
        public void Stirring_IsAsGoodAsThePaddleWasSteady_AndSettlesInVatTime()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.FillVat(0, "bark", 1, out string why), why);
            Assert.IsTrue(w.StirVat(0, out why, 0.4), why);
            Assert.AreEqual(0.7, w.Vats[0].stirred, 1e-6, "unsteady circles stir it only partly");
            Assert.IsTrue(w.StirVat(0, out why), why);
            Assert.AreEqual(1, w.Vats[0].stirred, 1e-9, "a stir of unknown steadiness (a quick button, an old save) stirs fully");
            Hours(w, 3);
            Assert.AreEqual(Math.Exp(-1), w.Vats[0].stirred, 0.02, "a stir settles in about three hours of vat time");
        }

        [Test]
        public void TheVats_LiquorsAreTheirOwnColours()
        {
            CraftRules r = Rules();
            Color bark = r.Agent("bark").color;
            foreach (string id in new[] { "water", "lime", "vinegar" })
                Assert.Greater(Math.Abs(r.Agent(id).color.r - bark.r) + Math.Abs(r.Agent(id).color.g - bark.g) + Math.Abs(r.Agent(id).color.b - bark.b), 0.2f, id + " is not drawn as bark liquor");
        }

        // ------------------------------------------------------------------ finishing and cutting
        [Test]
        public void ACutPart_IsNotCutAgain()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            string body = w.CutPattern(id, "jerkin_body", 4, 3, 0, 0.9, out string why);
            Assert.IsNotNull(body, why);
            Assert.IsNull(w.CutPattern(body, "strap", 1, 1, 0, 0.95, out why), "straps are not cut out of a jerkin's body");
            Assert.AreEqual(CraftWorkshop.AlreadyCut, why);
        }

        [Test]
        public void Slicking_BurnishesAPartsEdgesOnlyAlongThem_AndSealsTheGrainALittle()
        {
            double Edge(float y)
            {
                CraftWorkshop w = Workshop();
                string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
                string body = w.CutPattern(id, "jerkin_body", 4, 3, 0, 0.9, out string why);
                Assert.IsNotNull(body, why);
                Assert.IsTrue(w.Slick(body, new List<float> { 1.6f, y, 6.4f, y }, 0.8, out why), why);
                return w.Piece(body).hide.edgeFinish;
            }
            double along = Edge(0.5f), middle = Edge(3f);
            Assert.Greater(along, 3 * middle, "run along the edge, it burnishes it; across the middle hardly");
            CraftWorkshop d = Workshop();
            string deer = Tanned(d, "deer");
            double before = CraftEvaluator.Piece(Rules(), d.Piece(deer)).WaterResistance;
            HideState h = d.Piece(deer).hide;
            for (int k = 0; k < 4; k++)
                for (int y = 0; y < h.height; y++)
                    Assert.IsTrue(d.Slick(deer, new float[] { 0, y + 0.5f, h.width, y + 0.5f }, 1, out string why), why);
            Assert.Greater(CraftEvaluator.Piece(Rules(), d.Piece(deer)).WaterResistance, before, "a slicked grain sheds water a little better");
        }

        [Test]
        public void Skiving_HolesOnlyTheCellsItGoesThrough()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "rabbit", 2));
            Assert.IsTrue(w.Skive(id, new List<float> { 0.5f, 1.5f, 3.5f, 1.5f }, 1, out string why), why);
            List<CraftDefect> holes = w.Piece(id).defects.Where(d => d.kind == CraftDefectKind.Hole).ToList();
            Assert.IsNotEmpty(holes, "a rabbit's skin goes through");
            Assert.That(holes.Select(d => d.at), Has.All.GreaterThanOrEqualTo(0), "each hole on its own cell");
        }

        [Test]
        public void AHide_KeepsTheScarsItWasBornWith_ApartFromTheKnifesWork()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "boar", 4));
            List<HideCell> cells = Cells(w, id).ToList();
            Assert.IsTrue(cells.Any(c => c.flaw), "a boar is scarred");
            Assert.That(cells.Where(c => c.flaw).Select(c => c.scar - c.damage), Has.All.EqualTo(0).Within(1e-12));
            Assert.That(cells.Where(c => !c.flaw).Select(c => c.scar), Has.All.EqualTo(0));
            HideState h = w.Piece(id).hide;
            int at = h.cells.FindIndex(c => c.present && c.flaw);
            CraftPattern strap = w.Rules.Pattern("strap");
            int x = at % h.width, y = at / h.width;
            int gx = Enumerable.Range(x - 3, 4).Where(gxx => CraftLeather.Fits(h, strap, gxx, y, 0)).DefaultIfEmpty(int.MinValue).First();
            Assume.That(gx, Is.Not.EqualTo(int.MinValue));
            string part = w.CutPattern(id, "strap", gx, y, 0, 0.95, out string why);
            Assert.IsNotNull(part, why);
            Assert.Greater(w.Piece(part).hide.cells[x - gx].scar, 0, "the scar goes into the part");
        }

        [Test]
        public void Guard_ACutIsNoCleanerThanTheHands_AndThePaddleIsWithinItsRange()
        {
            var novice = new CraftRequestGuard(3, 0);
            var cut = new CraftRequest(CraftRequestKind.Cut, "h") { id = "strap", a = 1 };
            Assert.IsTrue(novice.Check(ref cut, 0, out string why), why);
            Assert.AreEqual(CraftRequestGuard.CutCap(CraftRequestGuard.FloorOf(0)), cut.a, 1e-6);
            Assert.AreEqual(0.9, cut.a, 1e-6, "a novice's cut is at best nine-tenths clean");
            var master = new CraftRequestGuard(4, 1);
            var mcut = new CraftRequest(CraftRequestKind.Cut, "h") { id = "strap", a = 1 };
            Assert.IsTrue(master.Check(ref mcut, 0, out why), why);
            Assert.AreEqual(0.96, mcut.a, 1e-6, "a master's cleaner");
            var stir = new CraftRequest(CraftRequestKind.StirVat) { i0 = 0, a = 7 };
            Assert.IsTrue(novice.Check(ref stir, 1, out why), why);
            Assert.AreEqual(1, stir.a, 1e-6);
        }

        // ------------------------------------------------------------------ what is seen of it
        [Test]
        public void AnItem_SaysWhenALeatherPartIsTooThinForItsPlace()
        {
            CraftWorkshop w = Workshop();
            string id = Tanned(w, "deer");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Rack, out string why), why);
            DryUntilDried(w, id);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            HideState h = w.Piece(id).hide;
            CraftPattern sole = w.Rules.Pattern("sole");
            int gx = -1, gy = -1;
            for (int y = 0; y < h.height && gx < 0; y++)
                for (int x = 0; x < h.width && gx < 0; x++)
                    if (CraftLeather.Fits(h, sole, x, y, 0)) { gx = x; gy = y; }
            string part = w.CutPattern(id, "sole", gx, gy, 0, 0.9, out why);
            Assert.IsNotNull(part, why);
            CraftDesign boots = w.Rules.Design("leather_boots");
            CraftItemReport item = CraftEvaluator.Item(w.Rules, boots, new Dictionary<string, List<CraftPiece>> { ["soles"] = new List<CraftPiece> { w.Piece(part) } }, null);
            Assert.IsTrue(item.Notes.Any(n => n.StartsWith("Sole: too thin") && n.Contains("wants 5-9")), string.Join(" | ", item.Notes));
            Assert.IsTrue(item.Notes.Any(n => n.StartsWith("Sole: dried puckered")), "and that it dried on the rack");
        }

        [Test]
        public void TheCalipers_ReadAHidesThickness_AndAPartShowsItsCutAndItsEdges()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            List<string> seen = CraftLook.Describe(w.Rules, w.Piece(id), null, CraftInstruments.Calipers, false, false);
            Assert.IsTrue(seen.Any(l => l.Contains("Calipers:") && l.Contains("mm thick")), string.Join(" | ", seen));
            string part = w.CutPattern(id, "strap", 7, 6, 0, 0.5, out string why);
            Assert.IsNotNull(part, why);
            List<string> lines = CraftLook.Describe(w.Rules, w.Piece(part), null, CraftInstruments.None, false, false);
            CollectionAssert.Contains(lines, "Its edges are cut ragged.");
            CollectionAssert.Contains(lines, "Its edges are raw and unburnished.");
        }
    }
}
#endif
