#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the woodshop's and the carving bench's rules (wave 1): the log chain parts the larger dimension
    /// and ends in square stock; the froe refuses a log too big for it and a rived log is a log no more; a saw cuts at its
    /// mark; a bend strains by the form's radius, stays soft only for a while out of the steam, and the snath can be bent;
    /// the steam box wets and never dries; heat dries wood the same whatever its stretches and never below the air; the
    /// stack's cover is the stack's; the papers judge a skipped grit by the surface; the drawknife's share of a round;
    /// wax, oil and varnish each by their own rule; a board ripped to a taper; the species' cleave; what a grip's slot
    /// and a haft's end must fit; the saw in the hands; and the guard's bounds on the wood's requests.
    /// </summary>
    public class CraftWoodAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "woodarea");

        /// <summary>A blank steamed through in the steam box and just taken out (hot and soft).</summary>
        private static CraftPiece Steamed(CraftWorkshop w, string wood, double length, double width, double thickness)
        {
            string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, wood, length, width, thickness));
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Steam, out string why), why);
            for (int t = 0; t < 40000 && w.Piece(id).wood.steamed < 1; t++) w.RunSteps(1);
            Assert.GreaterOrEqual(w.Piece(id).wood.steamed, 1, "it steams through in time");
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            return w.Piece(id);
        }

        private static double MeanWidth(CraftPiece p) => p.segments.Average(s => s.width);
        private static double MeanThickness(CraftPiece p) => p.segments.Average(s => s.thickness);

        // ------------------------------------------------------------------ the log chain
        [Test]
        public void LogChain_SplitsTheLargerDimension_AndEndsInSquareStock()
        {
            CraftWorkshop w = Workshop();
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 240));
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out string why), why);
            CraftPiece p = w.Piece(log);
            Assert.AreEqual(119, MeanWidth(p), 0.5, "a round log parts across its width first");
            Assert.AreEqual(240, MeanThickness(p), 0.5);
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out why), why);
            Assert.AreEqual(119, MeanThickness(p), 0.5, "a half parts across its larger dimension: the thickness");
            Assert.AreEqual(119, MeanWidth(p), 0.5);
            Assert.IsNotNull(w.SplitLog(log, 0, 1, 1, 4, out why), why);
            Assert.AreEqual(58.5, MeanWidth(p), 0.5, "a square quarter parts across its width");
            Assert.AreEqual(119, MeanThickness(p), 0.5);
            Assert.IsNotNull(w.Rive(log, 0.05, 0.9, out why), why);
            Assert.AreEqual(58.5, MeanWidth(p), 0.5);
            Assert.AreEqual(58.5, MeanThickness(p), 0.6, "log, three splits and a rive: square stock a haft is carved from");
            Assert.IsNull(w.SplitLog(log, 0, 1, 1, 4, out why), "a billet under the wedges' size is riven");
            StringAssert.Contains("rive", why);
        }

        [Test]
        public void Froe_RefusesARoundLogTooBig_AndARivedLogIsALogNoMore()
        {
            CraftWorkshop w = Workshop();
            string big = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 240));
            Assert.IsNull(w.Rive(big, 0.05, 0.9, out string why));
            Assert.AreEqual("Split the round log with the wedges first.", why);
            Assert.IsFalse(CraftWoodwork.RivesWithFroe(w.Piece(big), out _), "the window asks the same before the froe is set");
            string small = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 600, 50));
            Assert.IsFalse(CraftWoodwork.SplitsWithWedges(w.Piece(small), out why));
            StringAssert.Contains("froe", why);
            string other = w.Rive(small, 0.05, 0.9, out why);
            Assert.IsNotNull(other, why);
            Assert.IsFalse(w.Piece(small).wood.log);
            Assert.IsFalse(w.Piece(other).wood.log, "both halves of a rived log are rived stock");
            Assert.Less(CraftWoodwork.BarkLeft(w.Piece(other)), 0.6, "the new faces are clean wood");
            Assert.IsFalse(CraftWoodwork.RivesWithFroe(w.Piece(w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 16, 16))), out why));
            StringAssert.Contains("too narrow", why);
        }

        [Test]
        public void Froe_SetOffTheMiddle_PartsUnevenly_AndRunsOut()
        {
            CraftWorkshop w = Workshop();
            string mid = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 60, 60)), side = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 60, 60));
            string midHalf = w.Rive(mid, 0.05, 0.9, out string why, 0.5);
            string sideHalf = w.Rive(side, 0.05, 0.9, out why, 0.2);
            Assert.IsNotNull(sideHalf, why);
            Assert.AreEqual(MeanWidth(w.Piece(mid)), MeanWidth(w.Piece(midHalf)), 0.5);
            Assert.Less(MeanWidth(w.Piece(side)), MeanWidth(w.Piece(sideHalf)) - 20, "the froe parts it where it was set");
            Assert.Greater(w.Piece(side).wood.runout, w.Piece(mid).wood.runout, "an off-centre split runs out toward the thin side");
            Assert.Greater(CraftWoodwork.RiveStray(0.1, 0.5, 0.15), CraftWoodwork.RiveStray(0.1, 0.5, 0.85) + 0.03, "interlocked grain fights the froe");
        }

        [Test]
        public void Elm_WillNotSplitTrue()
        {
            Assert.Greater(Rules().Wood("ash").cleave, Rules().Wood("elm").cleave + 0.5);
            Assert.Less(CraftWoodwork.SplitAlongOf(0.95, Rules().Wood("elm").cleave), CraftWoodwork.SplitAlong, "elm twists away from even a good line");
            Assert.AreEqual(0.95, CraftWoodwork.SplitAlongOf(0.95, Rules().Wood("oak").cleave), 1e-9, "oak cleaves true");
            CraftWorkshop w = Workshop();
            string ash = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 120, 40)), elm = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "elm", 300, 120, 40));
            for (int k = 0; k < 4; k++)
            {
                Assert.IsTrue(w.Carve(ash, 0, 3, CraftSurface.CarveCut.Width, 3, 0.2, out string why), why);
                Assert.IsTrue(w.Carve(elm, 0, 3, CraftSurface.CarveCut.Width, 3, 0.2, out why), why);
            }
            Assert.Greater(w.Piece(ash).wood.split, w.Piece(elm).wood.split * 1.5, "a forced knife splits a cleaving wood sooner");
        }

        // ------------------------------------------------------------------ the saw
        [Test]
        public void Saw_CutsExactlyAtTheMark()
        {
            CraftWorkshop w = Workshop();
            string maple = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple", 1000, 40, 40));
            string far = w.SawCut(maple, 0.11, 0.05, 0.95, out string why);
            Assert.IsNotNull(far, why);
            Assert.AreEqual(110, w.Piece(maple).Length, 0.01, "the near piece is as long as the mark said");
            Assert.AreEqual(890, w.Piece(far).Length, 0.01);
            string ash = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 2200, 40, 40));
            Assert.IsNotNull(w.SawCut(ash, 450 / 2200.0, 0.05, 0.95, out why), why);
            Assert.AreEqual(450, w.Piece(ash).Length, 0.01);
            string grip = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple", 1000, 40, 40));
            Assert.IsNotNull(w.SplitPiece(grip, 0.105, out why), why);
            Assert.AreEqual(105, w.Piece(grip).Length, 0.01, "the bench saw cuts at its mark too");
            Assert.IsNull(w.SawCut(grip, 0.001, 0.05, 0.95, out why), "nothing to cut off at the very end");
            string steamed = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 30, 12));
            Assert.IsTrue(w.MovePiece(steamed, CraftLocation.Steam, out why), why);
            Assert.IsNull(w.SplitPiece(steamed, 0.5, out why));
            Assert.AreEqual("Take it out first.", why, "a piece in the steam box is not cut where it lies");
            Assert.AreEqual(CraftDiscipline.Woodwork, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(CraftRequestKind.SawCut, maple)));
        }

        [Test]
        public void InsertBoundary_SplitsTheStretch_AndKeepsWhatIsAlongIt()
        {
            CraftPiece p = CraftDefaults.WoodBlank(Rules(), "ash", 1200, 40, 40); // 12 stretches of 100 mm
            p.segments[3].bend = 10;
            p.AddDefect(CraftDefectKind.Crack, 5, 0.3, true, "test");
            int at = p.InsertBoundary(0.325); // 390 mm: 90 mm into stretch 3
            Assert.AreEqual(3, at);
            Assert.AreEqual(13, p.segments.Count);
            Assert.AreEqual(90, p.segments[3].length, 1e-6);
            Assert.AreEqual(10, p.segments[4].length, 1e-6);
            Assert.AreEqual(10, p.segments[3].bend + p.segments[4].bend, 1e-9, "the bend is shared by length");
            Assert.AreEqual(6, p.defects[0].at, "a defect past the cut moves up with its stretch");
            Assert.AreEqual(1200, p.Length, 1e-6);
            Assert.AreEqual(4, p.InsertBoundary(400 / 1200.0), "on a boundary already there");
            Assert.AreEqual(-1, p.InsertBoundary(0.0002), "at an end there is nothing to cut off");
        }

        [Test]
        public void SawStroke_CountsStrokesByTravel_AndJudgesTheLine()
        {
            var steady = new CraftSawStroke();
            steady.Begin(CraftSawStroke.StrokesFor(40));
            double t = 0;
            int samples = 0;
            for (int stroke = 0; stroke < 40 && steady.Progress < 1; stroke++)
                for (int k = 0; k <= 10; k++, samples++)
                {
                    t += 0.03;
                    steady.Add(0, (stroke % 2 == 0 ? k : 10 - k) * 6.0, t, null);
                }
            Assert.GreaterOrEqual(steady.Progress, 1, "long, even strokes saw through");
            Assert.Less(steady.Strokes, 20);
            Assert.Less(steady.Wander, 0.05);
            Assert.Greater(steady.Square, 0.95);

            var wiggle = new CraftSawStroke();
            wiggle.Begin(CraftSawStroke.StrokesFor(40));
            t = 0;
            for (int k = 0; k < 400; k++)
            {
                t += 0.01;
                wiggle.Add(0, k % 2 == 0 ? 0 : 5, t, null);
            }
            Assert.Less(wiggle.Progress, 0.05, "a jiggle of a few pixels does not saw");

            var drift = new CraftSawStroke();
            drift.Begin(CraftSawStroke.StrokesFor(40));
            t = 0;
            for (int stroke = 0; stroke < 30; stroke++)
                for (int k = 0; k <= 10; k++)
                {
                    t += 0.03;
                    drift.Add(stroke * 1.0, (stroke % 2 == 0 ? k : 10 - k) * 6.0, t, null);
                }
            Assert.Greater(drift.Wander, 0.5, "a hand that drifts off the line wanders");
            Assert.Less(drift.Square, 0.2, "and the cut is out of square");

            // Lifted halfway and set back in its kerf, the cut goes on from where it was: nothing done is lost, nothing counted twice.
            var resumed = new CraftSawStroke();
            resumed.Begin(CraftSawStroke.StrokesFor(40));
            t = 0;
            for (int stroke = 0; stroke < 6; stroke++)
                for (int k = 0; k <= 10; k++)
                {
                    t += 0.03;
                    resumed.Add(0, (stroke % 2 == 0 ? k : 10 - k) * 6.0, t, null);
                }
            double half = resumed.Progress;
            int strokes = resumed.Strokes;
            Assert.Greater(half, 0.1);
            Assert.Less(half, 1);
            resumed.Stop();
            resumed.Add(0, 200, t + 1, null);
            Assert.AreEqual(half, resumed.Progress, 1e-9, "a lifted saw cuts nothing");
            resumed.Resume();
            t += 2;
            resumed.Add(0, 0, t, null); // the first sample of the new press is where the stroke starts, not a stroke
            Assert.AreEqual(half, resumed.Progress, 1e-9);
            Assert.AreEqual(strokes, resumed.Strokes);
            for (int stroke = 0; stroke < 40 && resumed.Progress < 1; stroke++)
                for (int k = 0; k <= 10; k++)
                {
                    t += 0.03;
                    resumed.Add(0, (stroke % 2 == 0 ? k : 10 - k) * 6.0, t, null);
                }
            Assert.GreaterOrEqual(resumed.Progress, 1, "set back in its kerf, it saws on through");
            Assert.Less(resumed.Wander, 0.05);
        }

        // ------------------------------------------------------------------ the taper
        [Test]
        public void Rip_TapersAKiteBoard_FromTheLongLindenPlank()
        {
            CraftWorkshop w = Workshop();
            Assert.GreaterOrEqual(w.Rules.Wood("linden").blankLength, 1100, "a plank long enough for the kite");
            string board = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "linden"));
            Assert.IsTrue(w.SetIntent(board, "kite_shield", "board", out string why), why);
            double before = CraftEvaluator.Piece(w.Rules, w.Piece(board)).Shape;
            Assert.IsNotNull(w.SawCut(board, 1000 / 1100.0, 0.05, 0.95, out why), why);
            Assert.IsTrue(w.RipCut(board, 0, 0.6, 500, 450, 0.05, out why), why);
            Assert.IsTrue(w.RipCut(board, 0.6, 1, 450, 60, 0.05, out why), why);
            CraftPiece p = w.Piece(board);
            Assert.AreEqual(60, p.segments[p.segments.Count - 1].width, 30, "the point");
            Assert.AreEqual(500, p.segments[0].width, 6, "the top");
            for (int k = 0; k < 3; k++)
                Assert.IsTrue(w.Carve(board, 0, p.segments.Count - 1, CraftSurface.CarveCut.Thickness, 1.3, 0.9, out why), why);
            double after = CraftEvaluator.Piece(w.Rules, p).Shape;
            Assert.Less(before, 0.1);
            Assert.Greater(after, 0.8, "sawn, ripped and thinned, the board is the kite's");
            Assert.IsFalse(w.RipCut(board, 0, 0.6, 900, 900, 0.05, out why), "a line outside the board cuts nothing off");
            Assert.IsFalse(CraftWoodwork.RipNarrows(p, 0.2, 0.21, 100, 100, out why), "too short a line");
        }

        // ------------------------------------------------------------------ bending
        [Test]
        public void Bend_StrainsByTheFormsRadius_NotTheAngle()
        {
            CraftWorkshop w = Workshop();
            CraftPiece p = Steamed(w, "ash", 500, 30, 12);
            double tight = CraftWoodwork.BendStrain(w.Rules, p, 60, 0.2, true), gentle = CraftWoodwork.BendStrain(w.Rules, p, 250, 0.2, true);
            Assert.Greater(tight, gentle * 3, "the strain is the thickness over the form's radius");
            Assert.AreEqual(Math.Max(0, gentle - 1), CraftWoodwork.BendTearOver(w.Rules, p, 250, 30, 0.2, true, out _), 1e-9);
            Assert.AreEqual(CraftWoodwork.BendTearOver(w.Rules, p, 250, 30, 0.2, true, out _), CraftWoodwork.BendTearOver(w.Rules, p, 250, 120, 0.2, true, out _), 1e-9,
                "the same form strains the fibres the same at any angle");
            Assert.Greater(CraftWoodwork.BendStrain(w.Rules, p, 120, 1, true), CraftWoodwork.BendStrain(w.Rules, p, 120, 0.1, true), "rushed, it takes less");
            Assert.Greater(CraftWoodwork.BendStrain(w.Rules, p, 120, 0.2, false), CraftWoodwork.BendStrain(w.Rules, p, 120, 0.2, true) * 2, "the strap holds the outside");
            Assert.IsTrue(w.BendWood(p.id, 4, 7, 60, 0.2, true, out string why, 250), why);
            Assert.IsFalse(p.defects.Any(d => d.kind == CraftDefectKind.Crack), "over a gentle form, steamed and strapped, it takes the bend");
            CraftPiece q = Steamed(w, "ash", 500, 30, 12);
            Assert.IsTrue(w.BendWood(q.id, 4, 7, 60, 0.2, false, out why, 60), why);
            Assert.IsTrue(q.defects.Any(d => d.kind == CraftDefectKind.Crack), "the tight form tears it without the strap");
        }

        [Test]
        public void Steamed_StaysSoftOnlyAWhile_OutOfTheBox()
        {
            CraftWorkshop w = Workshop();
            CraftPiece p = Steamed(w, "ash", 500, 30, 12);
            Assert.Greater(CraftWoodwork.Plasticity(p), 0.8);
            double window = CraftWoodwork.CoolSeconds(p);
            Assert.AreEqual(45 * 12 / 25.0, window, 1e-6, "45 s for every 25 mm");
            w.RunSteps((int)(window * 0.5 / w.Rules.Settings.stepSeconds));
            Assert.Less(CraftWoodwork.Plasticity(p), 0.6, "half the time gone, half the softness");
            w.RunSteps((int)(window * 0.6 / w.Rules.Settings.stepSeconds));
            Assert.Less(CraftWoodwork.Plasticity(p), 0.05, "a minute or so out of the steam it has set");
        }

        [Test]
        public void Snath_CanBeSteamedIntoItsSweep()
        {
            CraftDesign scythe = Rules().Design("scythe");
            CraftComponentSpec snath = scythe.Component("haft");
            Assert.AreEqual(25, snath.shape.curvature, 1e-6, "the snath wants its sweep");
            CraftWorkshop w = Workshop();
            CraftPiece p = Steamed(w, "ash", 1500, 33, 33);
            Assert.IsTrue(w.SetIntent(p.id, "scythe", "haft", out string why), why);
            double plastic = CraftWoodwork.Plasticity(p);
            double sweep = 25 / (0.45 + 0.5 * plastic);
            Assert.Less(CraftWoodwork.BendStrain(w.Rules, p, 600, 0.25, true), 1, "over the snath form, with the strap, it takes the bend");
            Assert.IsTrue(w.BendWood(p.id, 6, 12, sweep, 0.25, true, out why, 600), why);
            Assert.IsFalse(p.defects.Any(d => d.kind == CraftDefectKind.Crack));
            Assert.AreEqual(25, p.segments.Sum(s => s.bend), 1.5);
            Assert.Greater(CraftEvaluator.Piece(w.Rules, p).Shape, 0.9, "bent to its sweep, the snath scores");
        }

        // ------------------------------------------------------------------ water
        [Test]
        public void SteamBox_WetsGreenWood_ButNeverDriesIt()
        {
            CraftWorkshop w = Workshop();
            string green = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 600, 40));
            string dry = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "oak", 600, 40, 40));
            Assert.IsTrue(w.MovePiece(green, CraftLocation.Steam, out string why), why);
            Assert.IsTrue(w.MovePiece(dry, CraftLocation.Steam, out why), why);
            w.RunSteps(2000);
            Assert.GreaterOrEqual(w.Piece(green).wood.moisture, 0.6 - 1e-9, "a green log comes out no drier than it went in");
            Assert.AreEqual(CraftWoodwork.SteamWater, w.Piece(dry).wood.moisture, 1e-6, "dry wood takes water up to what steam gives");
        }

        [Test]
        public void HeatDries_TheSameWhateverTheStretches_AndTheAirGivesItBack()
        {
            CraftWorkshop w = Workshop();
            CraftPiece a = CraftDefaults.WoodBlank(w.Rules, "ash", 600, 30, 30), b = CraftDefaults.WoodBlank(w.Rules, "ash", 600, 30, 30);
            b.Resample(24);
            foreach (CraftPiece p in new[] { a, b })
            {
                p.wood.moisture = 0.3;
                foreach (MetalSegment s in p.segments) s.edge.temp = s.edge.prevTemp = s.body.temp = s.body.prevTemp = 150;
            }
            CraftPiece pa = w.Piece(w.AddPiece(a)), pb = w.Piece(w.AddPiece(b));
            w.RunSteps(300);
            double lostA = 0.3 - pa.wood.moisture, lostB = 0.3 - pb.wood.moisture;
            Assert.Greater(lostA, 0.001);
            Assert.AreEqual(lostA, lostB, lostA * 0.25, "a stretch dries its share: the piece the same however it is cut up");
            CraftPiece cold = w.Piece(w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 30, 30)));
            cold.wood.moisture = 0.03; // dried far below the air by heat, then cooled
            w.RunSteps(3000);
            Assert.Greater(cold.wood.moisture, 0.1, "cold in the air, it takes water back toward what the air holds");
            Assert.LessOrEqual(cold.wood.moisture, CraftWoodwork.Equilibrium(w.Humidity) + 1e-9, "and no more");
        }

        [Test]
        public void StackCover_IsTheStacks_ForEveryBoardInIt()
        {
            CraftWorkshop sun = Workshop(), covered = Workshop();
            string a = sun.AddPiece(CraftDefaults.WoodBlank(sun.Rules, "oak", 600, 60, 40));
            string b = covered.AddPiece(CraftDefaults.WoodBlank(covered.Rules, "oak", 600, 60, 40));
            sun.Piece(a).wood.moisture = covered.Piece(b).wood.moisture = 0.4;
            Assert.IsTrue(sun.StackWood(a, 1, 1, 0, out string why), why);
            Assert.IsTrue(covered.StackWood(b, 1, 1, 0, out why), why);
            Assert.IsNull(covered.StackCover, "never pulled: each board keeps the shade it was stacked under");
            Assert.IsTrue(covered.SetStackCover(1, out why), why);
            Assert.AreEqual(1, covered.StackCover.Value, 1e-6);
            sun.RunSteps(6000);
            covered.RunSteps(6000);
            Assert.Greater(sun.Piece(a).wood.checks, covered.Piece(b).wood.checks + 0.02, "pulled over the stack afterwards, the cover shades the boards already in it");
            Assert.IsTrue(CraftRequest.IsSetting(CraftRequestKind.StackCover), "only the latest value of a drag goes");
            Assert.IsFalse(Workshop(CraftTools.ChoppingBlock).SetStackCover(1, out _), "there is no stack to cover");
        }

        // ------------------------------------------------------------------ the papers and the brush
        [Test]
        public void Sanding_ASkippedGrit_IsJudgedByTheSurface_EveryStroke()
        {
            CraftWorkshop w = Workshop();
            string blank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30));
            int n = w.Piece(blank).segments.Count - 1;
            Assert.IsTrue(w.SandWood(blank, 0, n, 0.25, 8, 1, out string why), why);
            double coarse = w.Piece(blank).segments.Average(s => s.rough);
            Assert.IsTrue(CraftWoodwork.SkipsGrit(w.Piece(blank), 0, n, 0.85), "fine straight after coarse skips the medium");
            for (int k = 0; k < 4; k++)
                Assert.IsTrue(w.SandWood(blank, 0, n, 0.85, 4, 1, out why), why);
            Assert.Greater(w.Piece(blank).segments.Average(s => s.rough), CraftWoodwork.SandFloor(0.55) + CraftWoodwork.GritReach,
                "stroke after stroke the fine paper hardly bites the coarse scratches - it is not forgotten after one stroke");
            Assert.IsTrue(w.SandWood(blank, 0, n, 0.55, 8, 1, out why), why);
            Assert.IsFalse(CraftWoodwork.SkipsGrit(w.Piece(blank), 0, n, 0.85), "through the medium, the fine paper bites");
            Assert.IsFalse(CraftWoodwork.SkipsGrit(60, 0.25), "the coarsest paper bites anything");
            Assert.IsTrue(w.Carve(blank, 0, n, CraftSurface.CarveCut.Width, 2.5, 0.2, out why), why);
            Assert.AreEqual(-1, w.Piece(blank).wood.lastGrit, "a fresh cut: the papers start again");
            Assert.Less(coarse, 35);
        }

        [Test]
        public void Finishes_EachByItsOwnRule()
        {
            CraftWorkshop w = Workshop();
            string Blank()
            {
                string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30));
                int last = w.Piece(id).segments.Count - 1;
                w.SandWood(id, 0, last, 0.25, 10, 1, out _);
                w.SandWood(id, 0, last, 0.55, 10, 1, out _);
                w.SandWood(id, 0, last, 0.85, 10, 1, out _);
                return id;
            }
            string thin = Blank(), smear = Blank(), oiled = Blank(), varnished = Blank();
            int n = w.Piece(thin).segments.Count - 1;
            Assert.IsTrue(w.Varnish(thin, "beeswax", 0, n, 0.9, 0.3, out string why), why);
            Assert.IsTrue(w.Varnish(smear, "beeswax", 0, n, 0.2, 1, out why), why);
            Assert.Greater(w.Piece(thin).wood.wax, w.Piece(smear).wood.wax, "wax rubbed in thin and even works in; a thick, patchy smear does not");
            for (int k = 0; k < 3; k++)
            {
                Assert.IsTrue(w.Varnish(oiled, "tung_oil", 0, n, 0.9, 0.5, out why), why);
                Assert.IsTrue(w.Varnish(varnished, "spirit_varnish", 0, n, 0.9, 0.5, out why), why);
                w.RunSteps(800);
            }
            Assert.AreEqual(0, w.Piece(oiled).wood.finish, 1e-9, "oil soaks in: it builds no film");
            Assert.Greater(w.Piece(oiled).wood.oil, 0.4);
            Assert.Greater(CraftWoodwork.FinishProtection(w.Piece(varnished).wood), CraftWoodwork.FinishProtection(w.Piece(oiled).wood) + 0.1, "a film keeps out more water than oil");
            Assert.AreEqual(1, w.Rules.Supply("spirit_varnish").dryingHours, 1e-6);
            Assert.Greater(w.Rules.Supply("oil_varnish").dryingHours, w.Rules.Supply("spirit_varnish").dryingHours * 4, "an oil varnish is slow to dry");
            string fast = Blank(), slow = Blank();
            Assert.IsTrue(w.Varnish(fast, "spirit_varnish", 0, n, 0.9, 0.5, out why), why);
            Assert.IsTrue(w.Varnish(slow, "oil_varnish", 0, n, 0.9, 0.5, out why), why);
            w.RunSteps(150);
            Assert.Less(w.Piece(fast).wood.finishWet, 0.1, "a spirit varnish is dry within the hour");
            Assert.Greater(w.Piece(slow).wood.finishWet, 0.7, "an oil varnish is still tacky");
            Assert.AreEqual(0.5, CraftWoodwork.BrushUse(1, 0.5), 1e-9, "an item lays two full-length coats");
            Assert.Less(CraftWoodwork.BrushUse(0.25, 0.5), 0.2, "a short stroke takes its share");
        }

        // ------------------------------------------------------------------ the drawknife
        [Test]
        public void Debark_ARoundOfPulls_PeelsGreenBark_TightBarkTakesTwo()
        {
            CraftWorkshop w = Workshop();
            string green = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 200));
            string dry = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 200));
            w.Piece(dry).wood.moisture = 0.2;
            int n = w.Piece(green).segments.Count - 1, pulls = 0;
            while (CraftWoodwork.BarkLeft(w.Piece(green)) > 0.001 && pulls < 20)
            {
                Assert.IsTrue(w.DebarkWood(green, 0, n, 0.45, 0.9, CraftWoodwork.DebarkPerStroke, out string why), why);
                pulls++;
            }
            Assert.LessOrEqual(pulls, 2 * CraftWoodwork.LogFaces, "two pulls on every face: a green log is bare");
            for (int k = 0; k < 2 * CraftWoodwork.LogFaces; k++)
                Assert.IsTrue(w.DebarkWood(dry, 0, n, 0.45, 0.9, CraftWoodwork.DebarkPerStroke, out string why), why);
            Assert.Greater(CraftWoodwork.BarkLeft(w.Piece(dry)), 0.2, "a dry log's tight bark needs another round");
            Assert.AreEqual(CraftWoodwork.GougeGreen, CraftWoodwork.GougeDepth(w.Piece(green)), 1e-9);
            Assert.AreEqual(CraftWoodwork.GougeDry, CraftWoodwork.GougeDepth(w.Piece(dry)), 1e-9);
            var guard = new CraftRequestGuard(3, 0);
            var pull = new CraftRequest(CraftRequestKind.DebarkWood, green) { i0 = 0, i1 = n, a = 0.45f, b = 0.9f, c = 1f };
            Assert.IsTrue(guard.Check(ref pull, 10, out string note), note);
            Assert.LessOrEqual(pull.c, CraftWoodwork.DebarkPerStroke + 0.02 + 1e-6, "one pull takes one face's share, whatever is claimed");
        }

        [Test]
        public void Knife_TakesTheBarkOffWhereItCuts()
        {
            CraftWorkshop w = Workshop();
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 600, 50));
            for (int k = 0; k < 6; k++)
                Assert.IsTrue(w.Carve(log, 0, 3, CraftSurface.CarveCut.Round, 1.2, 0.9, out string why), why);
            CraftPiece p = w.Piece(log);
            Assert.Less(p.segments[1].bark, 0.1, "a knife carving the stretch takes its bark");
            Assert.Greater(p.segments[p.segments.Count - 1].bark, 0.9, "and only where it cuts");
        }

        // ------------------------------------------------------------------ what it is fitted to
        [Test]
        public void Slot_And_Eye_FromTheDesign_OrThePieceItGoesOn()
        {
            CraftWorkshop w = Workshop();
            string grip = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "maple", 120, 30, 30));
            Assert.IsTrue(w.SetIntent(grip, "dagger", "grip", out string why), why);
            Assert.IsTrue(CraftWoodwork.TangSlot(w, w.Piece(grip), out double slot, out bool measured));
            Assert.IsFalse(measured);
            Assert.AreEqual(12 * 0.75 + 6 * 0.25, slot, 1e-6, "the dagger's tang, as its design has it");
            string haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 40, 40));
            Assert.IsTrue(w.SetIntent(haft, "hatchet", "haft", out why), why);
            Assert.IsTrue(CraftWoodwork.HeadEye(w, w.Piece(haft), out double eye, out measured));
            Assert.Greater(eye, 20);
            Assert.IsFalse(CraftWoodwork.TangSlot(w, w.Piece(haft), out _, out _), "a haft goes into an eye, not on a tang");
            Assert.IsFalse(CraftWoodwork.HeadEye(w, w.Piece(grip), out _, out _));
        }

        // ------------------------------------------------------------------ the guard
        [Test]
        public void Guard_BoundsTheWoodsRequests()
        {
            var guard = new CraftRequestGuard(4, 0);
            var bend = new CraftRequest(CraftRequestKind.BendWood, "p") { i0 = 1, i1 = 4, a = 90, b = 0, c = 0 };
            Assert.IsTrue(guard.Check(ref bend, 10, out string note), note);
            Assert.GreaterOrEqual(bend.b, 4 * guard.Floor - 1e-6, "never swept steadier than these hands");
            Assert.AreEqual(120, bend.c, 1e-6, "no form named: the medium one");
            var wide = new CraftRequest(CraftRequestKind.BendWood, "p") { i0 = 1, i1 = 4, a = 90, b = 0.3f, c = 5000 };
            Assert.IsTrue(guard.Check(ref wide, 20, out note), note);
            Assert.LessOrEqual(wide.c, 800, "no gentler than the woodshop's forms");
            var rip = new CraftRequest(CraftRequestKind.RipCut, "p") { a = 0, b = 0.6f, c = 500, d = 450, i0 = 0 };
            Assert.IsTrue(guard.Check(ref rip, 30, out note), note);
            Assert.GreaterOrEqual(rip.i0, (int)Math.Ceiling(2000 * guard.Floor), "never truer to the line than these hands");
            var again = new CraftRequest(CraftRequestKind.RipCut, "p") { a = 0, b = 0.6f, c = 500, d = 450, i0 = 100 };
            var third = again;
            Assert.IsTrue(guard.Check(ref again, 30.1, out note), note);
            Assert.IsFalse(guard.Check(ref third, 30.2, out note), "one cut at a time");
            var rive = new CraftRequest(CraftRequestKind.Rive, "p") { a = 0.1f, b = 0.9f, c = 0.99f };
            Assert.IsTrue(new CraftRequestGuard(5, 0).Check(ref rive, 40, out note), note);
            Assert.LessOrEqual(rive.c, 0.95 + 1e-6, "the froe is set inside the billet");
        }
    }
}
#endif
