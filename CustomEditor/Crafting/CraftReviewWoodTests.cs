#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the wood's review (the woodshop and the carving bench): the guide breaks wood down along the
    /// grain while it pays (a log split on into billets before it seasons, stock meant for a part rived while each half
    /// still holds it) and goes on past the length to the shape, the slot, the papers and the seal; the wedges refuse a
    /// board too thin for them; a stack laid well (shaded, sealed, on even stickers - one under each end) hardly checks,
    /// and thick stock seasons slower; a saw lifted and set back goes on in its kerf; a cut across a wide board takes more
    /// strokes than one across a stick; a part sawn from the long stock is worked in as many stretches as a fresh blank;
    /// a knife stroke is judged against its own line; a sawn piece has one fresh, unsealed end; the wedge's line runs
    /// across the side the wedges part; the bit goes on from the slot already in the piece.
    /// </summary>
    public class CraftReviewWoodTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "woodrev");

        private static CraftGuide.Step? Next(CraftWorkshop w, string id, CraftTools tools = CraftTools.Woodshop) =>
            CraftGuide.Next(w, tools, w.Piece(id), new CraftJournalData());

        private static double MeanWidth(CraftPiece p) => p.segments.Average(s => s.width);
        private static double MeanThickness(CraftPiece p) => p.segments.Average(s => s.thickness);

        private static void Debark(CraftWorkshop w, string id)
        {
            foreach (MetalSegment s in w.Piece(id).segments) s.bark = 0;
            w.Piece(id).wood.bark = 0;
        }

        // ------------------------------------------------------------------ the guide (wood-r01)
        [Test]
        public void Guide_ALog_IsSplitOnIntoBillets_BeforeItSeasons()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash")); // 2200 x 240, green, in its bark
            StringAssert.Contains("then split it", Next(w, log).Value.text);
            Debark(w, log);
            var said = new List<string>();
            for (int k = 0; k < 8; k++)
            {
                CraftGuide.Step s = Next(w, log).Value;
                if (s.place != "Split") break;
                said.Add($"{s.text} ({MeanWidth(w.Piece(log)):0} x {MeanThickness(w.Piece(log)):0})");
                Assert.AreEqual(CraftGuide.Woodshop, s.area);
                Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out string why), why);
            }
            Console.WriteLine(string.Join("\n", said));
            Assert.AreEqual(4, said.Count, "log, halves, quarters, billets: split green before it seasons");
            StringAssert.Contains("into quarters", said[1], "the half is split again");
            StringAssert.Contains("green wood is split before it seasons", said[2]);
            CraftPiece billet = w.Piece(log);
            Assert.Less(CraftWoodwork.CrossSize(billet), CraftWoodwork.MinSplitWidth, "it ends in billets under the wedges' size");
            CraftGuide.Step dry = Next(w, log).Value;
            Assert.AreEqual("Dry", dry.place, dry.text);
            // meant for nothing yet and seasoned, a billet is the woodworker's own
            billet.wood.moisture = CraftWoodwork.Seasoned;
            Assert.IsNull(Next(w, log));
            // a half seasoned whole (the guide never said so) is left to the woodworker too: it is not green any more
            string half = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 240));
            Debark(w, half);
            Assert.IsNotNull(w.SplitLog(half, 0, 1, 1, 4, out string why2), why2);
            Assert.AreEqual("Split", Next(w, half).Value.place, "a green half is split again");
            w.Piece(half).wood.moisture = CraftWoodwork.Seasoned;
            Assert.IsNull(Next(w, half));
        }

        [Test]
        public void Guide_StockMeantForAPart_IsRivedWhileEachHalfStillHoldsIt()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            // a hatchet's haft (32 x 32 mm) from a log: split to the square billet, no further - each half of it would be too thin
            string haft = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 240));
            Assert.IsTrue(w.SetIntent(haft, "hatchet", "haft", out string why), why);
            Debark(w, haft);
            int splits = 0, rives = 0;
            for (int k = 0; k < 10; k++)
            {
                CraftGuide.Step s = Next(w, haft).Value;
                if (s.place == "Split") { StringAssert.Contains("the haft wants 32 x 32 mm", s.text); Assert.IsNotNull(w.SplitLog(haft, 0, 1, 1, 4, out why), why); splits++; }
                else if (s.place == "Rive") { Assert.IsNotNull(w.Rive(haft, 0.05, 0.9, out why), why); rives++; }
                else break;
            }
            Assert.AreEqual(4, splits);
            Assert.AreEqual(0, rives, "a 58 mm billet rived would leave halves under 32 mm");
            Assert.GreaterOrEqual(Math.Min(MeanWidth(w.Piece(haft)), MeanThickness(w.Piece(haft))), 32, "it still holds the haft");
            // a throwing axe's short haft (27 x 27 mm) is rived on from that billet, twice
            string light = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 900, 58.5, 58.5));
            w.Piece(light).wood.moisture = 0.55;
            Assert.IsTrue(w.SetIntent(light, "throwing_axe", "haft", out why), why);
            for (int k = 0; k < 4; k++)
            {
                CraftGuide.Step s = Next(w, light).Value;
                if (s.place != "Rive") break;
                StringAssert.Contains("Rive it with the froe: the short haft wants 27 x 27 mm", s.text);
                Assert.IsNotNull(w.Rive(light, 0.05, 0.9, out why), why);
                rives++;
            }
            Assert.AreEqual(2, rives);
            CraftPiece l = w.Piece(light);
            Assert.GreaterOrEqual(Math.Min(MeanWidth(l), MeanThickness(l)), 27, "riven no thinner than the part");
            Assert.Less(Math.Max(MeanWidth(l), MeanThickness(l)), 30);
            // without a chopping block: somewhere else, no Go
            string other = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 900, 58.5, 58.5));
            Assert.IsTrue(w.SetIntent(other, "throwing_axe", "haft", out why), why);
            CraftGuide.Step away = CraftGuide.Next(w, CraftTools.Woodshop & ~CraftTools.ChoppingBlock, w.Piece(other), new CraftJournalData()).Value;
            Assert.AreEqual("", away.area);
            StringAssert.Contains("chopping block", away.text);
            // a linden plank meant for a shield's board, or for nothing, is never sent to the wedges or the froe
            string plank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "linden"));
            Assert.IsNull(Next(w, plank), "a seasoned plank meant for nothing: the woodworker's own");
            Assert.IsTrue(w.SetIntent(plank, "round_shield", "board", out why), why);
            StringAssert.StartsWith("Saw it to length", Next(w, plank).Value.text);
        }

        [Test]
        public void Guide_TheStockStave_IsSawnCarvedSandedAndSealed()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash")); // 2200 x 40 x 40, seasoned
            Assert.IsTrue(w.SetIntent(haft, "hatchet", "haft", out string why), why);
            CraftGuide.Step saw = Next(w, haft).Value;
            Assert.AreEqual("Saw", saw.place, saw.text);
            Assert.IsNotNull(w.SawCut(haft, 430 / 2200.0, 0.05, 0.95, out why), why);
            CraftPiece p = w.Piece(haft);
            CraftGuide.Step carve = Next(w, haft).Value;
            Console.WriteLine(carve.text);
            Assert.AreEqual(CraftGuide.Carve, carve.area);
            Assert.AreEqual("Width", carve.place, "the width is the more off (it counts the more)");
            StringAssert.StartsWith("Carve it to its shape", carve.text);
            foreach (MetalSegment s in p.segments) s.width = 32.5;
            Assert.AreEqual("Thickness", Next(w, haft).Value.place);
            foreach (MetalSegment s in p.segments) s.thickness = 32.5;
            CraftGuide.Step sand = Next(w, haft).Value;
            Console.WriteLine(sand.text);
            Assert.AreEqual(CraftGuide.Woodshop, sand.area);
            Assert.AreEqual("Finish", sand.place);
            StringAssert.Contains("(optional", sand.text);
            p.wood.lastGrit = 0.85;
            CraftGuide.Step seal = Next(w, haft).Value;
            StringAssert.StartsWith("Seal the haft (optional)", seal.text);
            p.wood.oil = 0.3;
            Assert.IsTrue(CraftWoodwork.Sealed(p.wood));
            Assert.IsNull(Next(w, haft), "carved, sanded and sealed: ready to fit");
            // carved too thin somewhere, the knife cannot put it right: nothing is asked of it
            foreach (MetalSegment s in p.segments) s.width = 27;
            Assert.IsNull(Next(w, haft), "no knife makes it wider");
            Assert.Less(CraftEvaluator.ShapeScore(w.Rules, p, w.Rules.Design("hatchet").Component("haft").shape), 0.95, "the item notes it");
            // at a station without the carving bench or the finishing bench: somewhere else
            string other = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 430, 40, 40));
            Assert.IsTrue(w.SetIntent(other, "hatchet", "haft", out why), why);
            CraftGuide.Step away = CraftGuide.Next(w, CraftTools.Sawhorse | CraftTools.ChoppingBlock, w.Piece(other), new CraftJournalData()).Value;
            Assert.AreEqual("", away.area);
            StringAssert.Contains("carving bench", away.text);
        }

        [Test]
        public void Guide_AGrip_IsBoredForItsTang_AfterItIsCarved()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string grip = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple"));
            Assert.IsTrue(w.SetIntent(grip, "arming_sword", "grip", out string why), why);
            Assert.IsNotNull(w.SawCut(grip, 0.11, 0.05, 0.95, out why), why);
            CraftPiece p = w.Piece(grip);
            Assert.AreEqual(110, p.Length, 1);
            Assert.AreEqual("Width", Next(w, grip).Value.place);
            foreach (MetalSegment s in p.segments) { s.width = 30; s.thickness = 30; }
            Assert.IsTrue(CraftWoodwork.TangSlot(w, p, out double need, out _));
            CraftGuide.Step bore = Next(w, grip).Value;
            Console.WriteLine(bore.text);
            Assert.AreEqual(CraftGuide.Carve, bore.area);
            Assert.AreEqual("Bore", bore.place);
            StringAssert.Contains($"the tang needs {need:0} mm", bore.text);
            foreach (MetalSegment s in p.segments.Take(8)) s.hole = need;
            Assert.AreEqual("Finish", Next(w, grip).Value.place, "bored to the tang: the papers next");
        }

        // ------------------------------------------------------------------ the wedges (wood-r13)
        [Test]
        public void Wedges_RefuseABoardTooThinForThem_AndStillSplitLogsHalvesAndQuarters()
        {
            CraftWorkshop w = Workshop();
            string plank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "linden")); // 1100 x 650 x 16
            Assert.IsFalse(CraftWoodwork.SplitsWithWedges(w.Piece(plank), out string why));
            StringAssert.Contains("too thin for the wedges", why);
            Assert.IsNull(w.SplitLog(plank, 0.02, 0.95, 0.9, 5, out why), "the workshop refuses it the same");
            StringAssert.Contains("too thin", why);
            Assert.IsTrue(CraftWoodwork.RivesWithFroe(w.Piece(plank), out _), "the froe takes it");
            string pine = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "pine")); // 800 x 300 x 20
            Assert.IsFalse(CraftWoodwork.SplitsWithWedges(w.Piece(pine), out _));
            // the log's chain is the wedges' all the way to the square billet
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 240));
            for (int k = 0; k < 4; k++)
            {
                Assert.IsTrue(CraftWoodwork.SplitsWithWedges(w.Piece(log), out why), $"split {k + 1}: {why}");
                Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out why), why);
            }
            Assert.IsFalse(CraftWoodwork.SplitsWithWedges(w.Piece(log), out why));
            StringAssert.Contains("rive", why);
            // a slab split off the heart is too thin for another wedge: it is rived
            string slab = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 240));
            string thick = w.SplitLog(slab, 1, 1, 1, 4, out why);
            Assert.IsNotNull(thick, why);
            Assert.Less(CraftWoodwork.LesserSize(w.Piece(thick)), CraftWoodwork.MinWedgeThickness, "the thin side of a split far off the heart");
            Assert.IsFalse(CraftWoodwork.SplitsWithWedges(w.Piece(thick), out _));
            Assert.IsTrue(CraftWoodwork.RivesWithFroe(w.Piece(thick), out _));
        }

        // ------------------------------------------------------------------ seasoning (wood-r04, wood-r10)
        /// <summary>A green billet stacked so, seasoned until it is dry enough for a fit: its checks and the seconds it took.</summary>
        private static (double checks, double seconds) Season(double even, double sealed_, double cover, string wood = "ash", double size = 40)
        {
            CraftWorkshop w = Workshop();
            string b = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, wood, 600, size, size));
            w.Piece(b).wood.moisture = 0.55;
            Assert.IsTrue(w.SetStackCover(cover, out string why), why);
            Assert.IsTrue(w.StackWood(b, even, sealed_, cover, out why), why);
            int steps = 0;
            while (w.Piece(b).wood.moisture > CraftWoodwork.DryEnough && steps < 400000) { w.RunSteps(100); steps += 100; }
            return (w.Piece(b).wood.checks, steps * w.Rules.Settings.stepSeconds);
        }

        [Test]
        public void Seasoning_AStackLaidWell_HardlyChecks_AndEachShortfallChecksIt()
        {
            (double best, _) = Season(1, 1, 1);
            (double oak, _) = Season(1, 1, 1, "oak");
            (double edges, _) = Season(CraftWoodwork.EvenStickers, CraftWoodwork.WellSealed, CraftWoodwork.WellShaded);
            (double unsealed, _) = Season(1, 0, 1);
            (double sun, _) = Season(1, 1, 0);
            (double worst, _) = Season(0.3, 0, 0);
            Console.WriteLine($"checks: best {best:0.000}, oak {oak:0.000}, every gauge at its tick {edges:0.000}, unsealed {unsealed:0.000}, open sun {sun:0.000}, all wrong {worst:0.000}");
            Assert.Less(best, 0.03, "shaded, sealed, on even stickers ('Stacked well'): it hardly checks");
            Assert.Less(oak, 0.03);
            Assert.Less(edges, 0.08, "every gauge just in its green band: under what the look calls checked");
            Assert.Greater(unsealed, 0.08, "its ends not sealed: they check (the look sees it)");
            Assert.Greater(sun, 0.08, "in the sun it dries too fast and checks");
            Assert.Greater(worst, Math.Max(unsealed, sun), "all of it wrong, worst of all");
        }

        [Test]
        public void Seasoning_ThickStock_TakesLonger_ABilletNoLongerThanTheStock()
        {
            (_, double stock) = Season(1, 1, 1, "oak", 40);
            (_, double board) = Season(1, 1, 1, "oak", 16);
            (_, double billet) = Season(1, 1, 1, "oak", 58.5);
            CraftWorkshop w = Workshop();
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 240));
            Debark(w, log);
            w.Piece(log).wood.moisture = 0.55;
            Assert.IsTrue(w.SetStackCover(1, out string why), why);
            Assert.IsTrue(w.StackWood(log, 1, 1, 1, out why), why);
            int steps = 0;
            while (w.Piece(log).wood.moisture > CraftWoodwork.DryEnough && steps < 400000) { w.RunSteps(100); steps += 100; }
            double whole = steps * w.Rules.Settings.stepSeconds;
            Console.WriteLine($"dry enough after: a 16 mm board {board:0} s, 40 mm stock {stock:0} s, a 58 mm billet {billet:0} s, a whole 240 mm log {whole:0} s");
            Assert.AreEqual(board, stock, stock * 0.05, "up to the stock's 40 mm, at the stack's full pace");
            Assert.Greater(billet, stock * 1.3);
            Assert.Greater(whole, board * 4, "a whole log seasons far slower than the billets rived from it");
            Assert.AreEqual(1, CraftWoodwork.Seasoning(w.Piece(w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash")))), 1e-9);
            Assert.Less(w.Piece(log).wood.checks, 0.03, "stacked well, even a whole log hardly checks: it is only slow");
        }

        // ------------------------------------------------------------------ the stickers (wood-r03)
        [Test]
        public void Stickers_UnderEachEnd_AndEvenlyBetween_ReadEven_AndKeepItStraight()
        {
            double ends = CraftWoodwork.StickerEvenness(new List<float> { 0.03f, 0.35f, 0.65f, 0.97f });
            double five = CraftWoodwork.StickerEvenness(new List<float> { 0.03f, 0.265f, 0.5f, 0.735f, 0.97f });
            double floating = CraftWoodwork.StickerEvenness(new List<float> { 0.2f, 0.4f, 0.6f, 0.8f });
            double bunched = CraftWoodwork.StickerEvenness(new List<float> { 0.02f, 0.1f, 0.2f, 0.98f });
            double three = CraftWoodwork.StickerEvenness(new List<float> { 0.03f, 0.5f, 0.97f });
            Console.WriteLine($"STICKERS: ends and evenly between {ends:0.00}; five so {five:0.00}; floating 20% in {floating:0.00}; bunched {bunched:0.00}; three {three:0.00}");
            Assert.Greater(ends, 0.9);
            Assert.Greater(five, 0.9);
            Assert.Less(floating, CraftWoodwork.EvenStickers, "the ends droop off stickers set 20% in");
            Assert.Less(bunched, CraftWoodwork.EvenStickers, "uneven gaps");
            Assert.Less(three, CraftWoodwork.EvenStickers, "at least four");
            Assert.AreEqual(0, CraftWoodwork.StickerEvenness(new List<float> { 0.5f }), 1e-9);
            // a green ash haft stacked on the end layout comes out straight
            CraftWorkshop w = Workshop();
            string h = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 430, 34, 34));
            w.Piece(h).wood.moisture = 0.55;
            Assert.IsTrue(w.SetIntent(h, "hatchet", "haft", out string why), why);
            Assert.IsTrue(w.SetStackCover(1, out why), why);
            Assert.IsTrue(w.StackWood(h, ends, 1, 1, out why), why);
            for (int k = 0; k < 4000 && w.Piece(h).wood.moisture > CraftWoodwork.DryEnough; k++) w.RunSteps(50);
            Assert.Less(w.Piece(h).segments.Sum(s => Math.Abs(s.bend)), 1, "it dries flat");
        }

        // ------------------------------------------------------------------ the saw (wood-r05, wood-r12)
        /// <summary>A cut sawn as the windows saw it (the press sets where the hand's offset counts from), the hand drifting a pixel a stroke; <paramref name="relift"/>: lifted and pressed on the mark again every so many strokes.</summary>
        private static CraftSawStroke SawWithDrift(int relift, double drift = 0.1)
        {
            var saw = new CraftSawStroke();
            saw.Begin(CraftSawStroke.StrokesFor(40));
            double t = 0, handX = 300, pressX = handX;
            for (int stroke = 0; stroke < 60 && saw.Progress < 1; stroke++)
            {
                if (relift > 0 && stroke > 0 && stroke % relift == 0)
                {
                    saw.Stop();
                    handX = 302;
                    pressX = handX;
                    saw.Resume();
                }
                for (int k = 0; k <= 10; k++)
                {
                    t += 0.03;
                    handX += drift;
                    saw.Add(handX - pressX, (stroke % 2 == 0 ? k : 10 - k) * 6.0, t, null);
                }
            }
            return saw;
        }

        [Test]
        public void Saw_SetBackInItsKerf_GoesOnFromWhereTheKerfRan()
        {
            CraftSawStroke one = SawWithDrift(0), relifted = SawWithDrift(3);
            Console.WriteLine($"one press: wander {one.Wander:0.00}, square {one.Square:0.00}; lifted and set back every 3 strokes: wander {relifted.Wander:0.00}, square {relifted.Square:0.00}");
            // (a stroke under way when the saw is lifted is not counted, so the lifted cut takes longer and the hand drifts on)
            Assert.GreaterOrEqual(relifted.Wander, one.Wander * 0.95, "lifting the saw does not put a wandering cut back on the line");
            Assert.LessOrEqual(relifted.Square, one.Square + 0.05, "nor square it up");
            Assert.AreNotEqual("sawn square", CraftWoodwork.SawWords(relifted.Wander, relifted.Square));
            // set back, the blade is where the kerf ran
            var saw = new CraftSawStroke();
            saw.Begin(20);
            saw.Add(0, 0, 0.0, null);
            saw.Add(5, 30, 0.2, null);
            saw.Stop();
            saw.Resume();
            Assert.AreEqual(5, saw.Blade, 1e-9);
            saw.Add(-5, 0, 1.0, null); // the hand steers back the way it went: the blade comes back onto the line
            Assert.AreEqual(0, saw.Blade, 1e-9);
            // a straight hand stays straight however often it is set back
            Assert.Less(SawWithDrift(3, 0).Wander, 0.05);
        }

        [Test]
        public void Saw_AcrossAWideBoard_TakesMoreStrokesThanAcrossAStick()
        {
            double plank = CraftSawStroke.StrokesFor(650, 16), stave = CraftSawStroke.StrokesFor(40, 40), log = CraftSawStroke.StrokesFor(240, 240);
            Console.WriteLine($"strokes across: the linden plank {plank:0.0}, the ash stave {stave:0.0}, a log {log:0.0}");
            Assert.Greater(plank, stave * 1.5, "the kerf has the whole 650 mm to cross");
            Assert.AreEqual(CraftSawStroke.StrokesFor(40), stave, 1e-9, "a square section: as many as a rip through its side (the 40 mm stave 14)");
            Assert.AreEqual(CraftSawStroke.StrokesFor(240), log, 1e-9);
            Assert.Greater(CraftSawStroke.StrokesFor(60, 40), CraftSawStroke.StrokesFor(40, 40));
        }

        // ------------------------------------------------------------------ the stretches after a cut (wood-r08)
        [Test]
        public void ACut_LeavesAWoodenPart_AsManyStretchesAsAFreshBlank()
        {
            CraftWorkshop w = Workshop();
            int fresh = CraftDefaults.WoodBlank(w.Rules, "ash").segments.Count;
            string ash = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash"));      // 2200 mm
            string maple = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple"));  // 1000 mm
            string grip = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple"));
            Assert.IsNotNull(w.SawCut(ash, 430 / 2200.0, 0.05, 0.95, out string why), why);
            Assert.IsNotNull(w.SawCut(maple, 0.43, 0.05, 0.95, out why), why);
            Assert.IsNotNull(w.SawCut(grip, 0.1, 0.05, 0.95, out why), why);
            Console.WriteLine($"a fresh blank {fresh}; the haft from the stave {w.Piece(ash).segments.Count}, from the maple {w.Piece(maple).segments.Count}; the grip {w.Piece(grip).segments.Count}");
            Assert.AreEqual(w.Piece(maple).segments.Count, w.Piece(ash).segments.Count, "the same haft from either blank");
            Assert.GreaterOrEqual(w.Piece(ash).segments.Count, fresh);
            Assert.GreaterOrEqual(w.Piece(grip).segments.Count, fresh, "a short grip keeps enough stretches to carve and bore");
            // metal keeps its own rule
            CraftWorkshop m = Workshop();
            string bar = m.AddPiece(CraftDefaults.Bar(m.Rules, 200, 20, 6, ("copper", 1.0)));
            string end = m.SplitPiece(bar, 0.9, out why);
            Assert.IsNotNull(end, why);
            Assert.Less(m.Piece(end).segments.Count, fresh, "a short end of a bar is not given a blank's stretches");
        }

        // ------------------------------------------------------------------ the knife (wood-r06)
        [Test]
        public void Knife_AStraightStrokeAlongASlope_IsSteady_AWaveringOneIsNot()
        {
            // the kite board's dashed taper in Carve's view from above: 47 px of fall over 189 px
            var slope = Enumerable.Range(0, 40).Select(i => new Vector2(324f + i * 189f / 39f, 349f - i * 47f / 39f)).ToList();
            var wave = Enumerable.Range(0, 40).Select(i => new Vector2(324f + i * 5f, 300f + (float)Math.Sin(i * 0.6) * 8f)).ToList();
            double straight = CraftWoodwork.StrokeWander(slope), wavering = CraftWoodwork.StrokeWander(wave);
            Console.WriteLine($"wander: the straight sloping stroke {straight:0.0} px, the wavering one {wavering:0.0} px");
            Assert.Less(straight, 0.5, "a straight stroke keeps to its own line, whichever way it runs");
            Assert.AreEqual(8, wavering, 1, "a wavering stroke strays as far as it wavers");
            Assert.AreEqual(0, CraftWoodwork.StrokeWander(slope.Take(2).ToList()), 1e-9);
            // the window's steadiness from it (CraftCarveMode: 1 - wander / 30, times the hands): the sloping stroke does not split the board
            float steady = Mathf.Clamp01(1f - (float)straight / 30f) * Mathf.Clamp01(1f - (float)new CraftHands(1, 0).Unsteadiness * 5f);
            Assert.Greater(steady, CraftWoodwork.CarveSplitSteady);
            CraftWorkshop w = Workshop();
            string board = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "linden"));
            Assert.IsNotNull(w.SawCut(board, 1000 / 1100.0, 0.05, 0.95, out string why), why);
            Assert.IsTrue(w.RipCut(board, 0.6, 1, 450, 60, 0.05, out why), why);
            CraftPiece p = w.Piece(board);
            int n = p.segments.Count - 1, from = p.SegmentAt(0.6);
            for (int k = 0; k < 20; k++)
                Assert.IsTrue(w.Carve(board, from, n, CraftSurface.CarveCut.Width, 0.5, steady, out why), why);
            Assert.AreEqual(0, p.wood.split, 1e-9, "steady strokes along the line it is shown do not split it");
        }

        // ------------------------------------------------------------------ the ends after a cut (wood-r14)
        [Test]
        public void ACutAcross_LeavesEachPartOneFreshUnsealedEnd()
        {
            CraftWorkshop w = Workshop();
            string b = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "oak", 600, 60, 40));
            w.Piece(b).wood.moisture = 0.5;
            Assert.IsTrue(w.StackWood(b, 1, 1, 1, out string why), why);
            Assert.IsTrue(w.MovePiece(b, CraftLocation.Air, out why), why);
            Assert.AreEqual(1, w.Piece(b).wood.endsSealed, 1e-9, "taken off the stack its ends are still sealed");
            Assert.IsTrue(w.StackWood(b, 1, 0, 1, out why), why);
            Assert.AreEqual(1, w.Piece(b).wood.endsSealed, 1e-9, "stacked again unsealed: the seal stays (the window shows it from the piece)");
            Assert.IsTrue(w.MovePiece(b, CraftLocation.Air, out why), why);
            string far = w.SawCut(b, 0.5, 0.05, 0.95, out why);
            Assert.IsNotNull(far, why);
            Assert.AreEqual(0.5, w.Piece(b).wood.endsSealed, 1e-9, "one end old and sealed, one fresh from the saw");
            Assert.AreEqual(0.5, w.Piece(far).wood.endsSealed, 1e-9);
        }

        // ------------------------------------------------------------------ the wedge's line (wood-r09)
        [Test]
        public void WedgeLine_RunsAcrossTheSideTheWedgesPart()
        {
            CraftWorkshop w = Workshop();
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 240));
            Assert.IsTrue(CraftWoodwork.WedgeLineAcross(w.Piece(log), 0, 1, out _), "a round log parts the same whichever way");
            Assert.IsTrue(CraftWoodwork.WedgeLineAcross(w.Piece(log), 1, 0.2, out _));
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out string why), why);
            CraftPiece half = w.Piece(log); // 119 x 240: the wedges part its 240 mm thickness
            Assert.IsTrue(CraftWoodwork.HalvesThickness(half));
            Assert.IsTrue(CraftWoodwork.WedgeLineAcross(half, 1, 0.3, out why), "a line from the flat face out, through the heart: radial");
            Assert.IsFalse(CraftWoodwork.WedgeLineAcross(half, 0.2, 1, out why), "a line along the flat face would slab it: the wedges do not cut that");
            StringAssert.Contains("240 mm side", why);
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out why), why);
            Assert.IsTrue(CraftWoodwork.WedgeLineAcross(w.Piece(log), 1, 1, out _), "a square quarter: either way");
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out why), why);
            CraftPiece billet = w.Piece(log); // 58.5 x 119
            Assert.IsTrue(CraftWoodwork.WedgeLineAcross(billet, 1, 0, out _));
            Assert.IsFalse(CraftWoodwork.WedgeLineAcross(billet, 0, 1, out _));
        }

        // ------------------------------------------------------------------ the bore (wood-r15)
        [Test]
        public void Bore_GoesOnFromTheSlotInThePiece_AndReachesPastTurnsOnTheirWay()
        {
            CraftWorkshop w = Workshop();
            string grip = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple", 110, 30, 30));
            CraftPiece p = w.Piece(grip);
            int n = p.segments.Count, most = n * 3 / 4;
            for (int i = 0; i < 4; i++) p.segments[i].hole = 6; // bored four turns at the bench
            Assert.IsTrue(CraftWoodwork.BoreSpan(p, 0, out int from, out int to));
            Assert.AreEqual(0, from);
            Assert.AreEqual(4, to, "the next turn bores a stretch deeper than the slot already goes");
            Assert.IsTrue(CraftWoodwork.BoreSpan(p, 0, out from, out to, 2));
            Assert.AreEqual(6, to, "two turns sent and not yet answered: this one reaches past them");
            Assert.IsTrue(CraftWoodwork.BoreSpan(p, 0, out from, out to, 99));
            Assert.AreEqual(most - 1, to, "never past three quarters of its length");
            Assert.IsTrue(CraftWoodwork.BoreSpan(p, 1, out from, out to));
            Assert.AreEqual(n - 1, from, "from the other end: a fresh slot");
            Assert.AreEqual(n - 1, to);
        }
    }
}
#endif
