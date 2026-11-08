#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of grinding, polishing and sharpening (CraftSurface) as the Grind and Sharpen areas show them: the
    /// bevels are planes (they keep the angle held, the apex moves, the section loses exactly what was ground), the
    /// faces thin without skewing the edge, the wheel heats where it works and a running temper is noted, abrasion dulls
    /// a mirror and takes the coat off, a file that cannot bite is refused, the stone hones only an edge and only where
    /// it reaches the apex, light trailing strokes take the burr off, the stroke's segments are the real ones, a dip to
    /// cool is not a quench, and the window's judgements (the strip, the next step on the stone) read the model's own
    /// thresholds.
    /// </summary>
    public class CraftSurfaceAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(string medium = "water") => new CraftWorkshop(Rules(), CraftTools.Everything, 18, 1, 3, medium, "surface");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 5) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);

        /// <summary>The section's area as the model counts it (one edge).</summary>
        private static double Area(MetalSegment s) => s.width * s.thickness - 0.5 * (s.bevelDepthL * s.bevelWidthL + s.bevelDepthR * s.bevelWidthR);

        private static MetalSegment Seg(double depthL, double depthR, double angle = 15, double width = 30, double thickness = 5) => new MetalSegment
        {
            width = width, thickness = thickness, length = 8,
            bevelDepthL = depthL, bevelWidthL = depthL > 0 ? depthL / Tan(angle) : 0,
            bevelDepthR = depthR, bevelWidthR = depthR > 0 ? depthR / Tan(angle) : 0,
        };

        private static void Cool(CraftPiece p)
        {
            foreach (MetalSegment s in p.segments)
                s.edge.temp = s.body.temp = s.edge.prevTemp = s.body.prevTemp = 18;
        }

        private static void LightForge(CraftWorkshop w, int fuel = 6)
        {
            Assert.IsTrue(w.AddFuel("charcoal", fuel, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
        }

        private static void HeatTo(CraftWorkshop w, string id, double target)
        {
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out string why, 0.6), why);
            for (int t = 0; t < 4000 && w.Piece(id).MeanTemperature < target; t++)
            {
                if (t % 10 == 0) w.Bellows(0.4);
                if (t % 600 == 599) w.AddFuel("charcoal", 2, out _);
                w.RunSteps(1);
            }
            Assert.Greater(w.Piece(id).MeanTemperature, target, "it never got hot enough");
            w.RunSteps(60);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        /// <summary>A tool-steel bar hardened in the tank and left to cool to the room's heat.</summary>
        private static string HardBar(CraftWorkshop w, double thickness = 5)
        {
            string bar = Bar(w, "Tool steel", 200, 30, thickness);
            LightForge(w);
            HeatTo(w, bar, 820);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why), why);
            w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
            w.RunSteps(400);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out why), why);
            w.RunSteps(50);
            Cool(w.Piece(bar));
            return bar;
        }

        private static List<CraftEvent> Events(CraftWorkshop w)
        {
            var list = new List<CraftEvent>();
            w.EventRaised += e => list.Add(e);
            return list;
        }

        // ------------------------------------------------------------------ the bevels are planes (surface-04)
        [Test]
        public void GrindBevel_LosesExactlyTheAreaGround_InEveryCase()
        {
            // Before the bevels meet: a triangle at the angle held.
            MetalSegment fresh = Seg(0, 0);
            double a0 = Area(fresh);
            CraftSurface.GrindBevel(fresh, true, 3, 15);
            Assert.AreEqual(a0 - 3, Area(fresh), 1e-6);
            Assert.AreEqual(15, fresh.AngleL, 1e-6);
            // They have met: the apex slides along the other face's plane, toward the face being ground.
            MetalSegment met = Seg(2.5, 2.5);
            a0 = Area(met);
            double w0 = met.width;
            CraftSurface.GrindBevel(met, true, 1, 15);
            Assert.AreEqual(a0 - 1, Area(met) - (w0 - met.width) * 0, 1e-6, "the area ground comes off");
            Assert.AreEqual(a0 - 1, Area(met), 1e-6);
            Assert.AreEqual(15, met.AngleL, 0.01, "the bevel keeps the angle held");
            Assert.AreEqual(15, met.AngleR, 0.01, "and the other keeps its own");
            Assert.Less(met.bevelDepthR, 2.5, "the apex moves toward the face being ground (down the other's plane)");
            Assert.Less(met.width, w0, "the edge goes back");
            Assert.AreEqual(0, met.EdgeThickness, 1e-9, "it stays an edge");
            // A chisel grind, then the other face: a real bevel at the angle held, not a sliver.
            MetalSegment chisel = Seg(5, 0);
            a0 = Area(chisel);
            for (int k = 0; k < 10; k++) CraftSurface.GrindBevel(chisel, false, 1, 15);
            Assert.AreEqual(a0 - 10, Area(chisel), 1e-6);
            Assert.AreEqual(15, chisel.AngleR, 0.5);
            Assert.Greater(chisel.bevelDepthR, 1, "the second face gets a real share of the thickness");
            // Ground through the other face's bevel altogether: the edge goes straight back.
            MetalSegment through = Seg(2.5, 0.2);
            a0 = Area(through);
            CraftSurface.GrindBevel(through, true, 60, 15);
            Assert.AreEqual(a0 - 60, Area(through), 1e-6);
            Assert.AreEqual(0, through.bevelDepthR, 1e-9);
            Assert.AreEqual(5, through.bevelDepthL, 1e-6, "this face spans the thickness");
        }

        [Test]
        public void Bevels_KeepTheAngleHeld_PastWhereTheyMeet_OnTheWheel()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            MetalSegment s = p.segments[10];
            for (int n = 0; n < 400 && s.EdgeThickness > 0.01; n++)
            {
                Assert.IsTrue(w.Grind(id, 10, 10, n % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "coarse_wheel", 15, 0.5, 0.48, out string why), why);
                Cool(p);
            }
            Assert.Less(s.EdgeThickness, 0.01, "the bevels meet");
            double width = s.width;
            for (int k = 0; k < 40; k++)
            {
                w.Grind(id, 10, 10, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 0.5, 0.48, out _);
                Cool(p);
            }
            Assert.AreEqual(15, s.AngleL, 0.5, "ground on, the bevel keeps the angle held (it used to flatten)");
            Assert.Less(s.width, width - 0.2, "and the edge goes back");
            Assert.Greater(s.bevelDepthL, s.bevelDepthR, "the apex moved toward the face ground on");
            // Alternating keeps it in the middle.
            for (int k = 0; k < 120; k++)
            {
                w.Grind(id, 10, 10, k % 2 == 0 ? CraftSurfaceSide.RightBevel : CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 0.5, 0.48, out _);
                Cool(p);
            }
            Assert.Less(Math.Abs(s.bevelDepthL - s.bevelDepthR), Math.Abs(s.bevelDepthL + s.bevelDepthR) * 0.25, "ground in turn, the apex comes back toward the middle");
        }

        [Test]
        public void GrindBevel_AlternatingFaces_KeepsTheApexCentred_AndADoubleEdgeGoesBackOnBothEdges()
        {
            MetalSegment s = Seg(2.5, 2.5);
            for (int k = 0; k < 40; k++) CraftSurface.GrindBevel(s, k % 2 == 0, 0.5, 15);
            Assert.AreEqual(s.bevelDepthL, s.bevelDepthR, 0.15);
            MetalSegment one = Seg(2.5, 2.5), two = Seg(2.5, 2.5);
            CraftSurface.GrindBevel(one, true, 2, 15, 1);
            CraftSurface.GrindBevel(two, true, 2, 15, 2);
            Assert.AreEqual(2 * (30 - one.width), 30 - two.width, 1e-6, "a double edge: both edges go back");
            MetalSegment narrow = Seg(0, 0, 15, 12, 5);
            CraftSurface.GrindBevel(narrow, true, 40, 5, 2);
            Assert.LessOrEqual(narrow.bevelWidthL, narrow.width / 2 + 1e-9, "on both edges a bevel reaches the middle at most");
        }

        [Test]
        public void TheWheel_GrindsNoSteeperThanTheStoneIsHeld()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            Assert.IsTrue(w.Grind(id, 5, 10, CraftSurfaceSide.LeftBevel, "coarse_wheel", 45, 0.6, 1, out string why), why);
            Assert.LessOrEqual(w.Piece(id).segments[7].AngleL, CraftSurface.StoneAngleMax + 1e-6, "whatever the wheel grinds, the stone can reach");
        }

        // ------------------------------------------------------------------ the faces and the edge line (surface-14, m04)
        [Test]
        public void Faces_ThinBothBevelsAlike_KeepingTheirAngles_AndTheFlat()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 2.2;
                g.bevelWidthL = g.bevelWidthR = 2.2 / Tan(15);
            }
            double flat = p.segments[8].EdgeThickness;
            for (int k = 0; k < 6; k++)
            {
                Assert.IsTrue(w.Grind(id, 0, 23, CraftSurfaceSide.Faces, "coarse_wheel", 0, 0.6, 1, out string why), why);
                Cool(p);
            }
            MetalSegment s = p.segments[8];
            Assert.Less(s.thickness, 5 - 0.05, "the faces came down");
            Assert.AreEqual(15, s.AngleL, 0.01);
            Assert.AreEqual(15, s.AngleR, 0.01);
            Assert.AreEqual(s.bevelDepthL, s.bevelDepthR, 1e-9, "both bevels lost alike: the edge stays in the middle");
            Assert.AreEqual(flat, s.EdgeThickness, 1e-6, "thinning the faces leaves the edge's flat as it was");
        }

        [Test]
        public void EdgeLine_TakesTheHoningAndTheBurrBackWithTheEdge()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            MetalSegment s = w.Piece(id).segments[10];
            s.bevelDepthL = s.bevelDepthR = 2.5;
            s.bevelWidthL = s.bevelWidthR = 2.5 / Tan(15);
            s.honeL = s.honeR = 1.5;
            s.microL = s.microR = 15;
            s.microAngle = 30;
            s.burr = 1;
            s.apex = 8;
            Assert.IsTrue(w.Grind(id, 10, 10, CraftSurfaceSide.EdgeLine, "coarse_wheel", 0, 0.5, 0.5, out string why), why);
            Assert.AreEqual(0, s.honeL + s.honeR + s.microL + s.microR + s.microAngle + s.burr, 1e-9);
            Assert.GreaterOrEqual(s.apex, 250);
        }

        // ------------------------------------------------------------------ heat (surface-m05, surface-10)
        [Test]
        public void TheWheel_HeatsWhereItWorks()
        {
            CraftWorkshop w = Workshop();
            string bevel = Bar(w, "Spring steel"), spine = Bar(w, "Spring steel");
            foreach (string id in new[] { bevel, spine })
                foreach (MetalSegment g in w.Piece(id).segments)
                {
                    g.bevelDepthL = g.bevelDepthR = 2.3;
                    g.bevelWidthL = g.bevelWidthR = 2.3 / Tan(15);
                }
            Assert.IsTrue(w.Grind(bevel, 10, 10, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 1, 1, out string why), why);
            Assert.IsTrue(w.Grind(spine, 10, 10, CraftSurfaceSide.Spine, "coarse_wheel", 0, 1, 1, out why), why);
            MetalSegment b = w.Piece(bevel).segments[10], s = w.Piece(spine).segments[10];
            Assert.Greater(b.edge.temp, s.edge.temp + 50, "the bevels heat the thin edge");
            Assert.Greater(s.body.temp, s.edge.temp, "the spine heats the body, not the edge");
        }

        [Test]
        public void ATemperDrawnOnTheWheel_IsNoted_AndShowsItsColours()
        {
            CraftWorkshop w = Workshop();
            string bar = HardBar(w);
            CraftPiece p = w.Piece(bar);
            MetalSegment s = p.segments[12];
            Assert.Greater(s.edge.mart, 0.3, "hardened");
            double runs = CraftSurface.TemperRunsAt(w.Rules, s.edge);
            Assert.That(runs, Is.InRange(250.0, 330.0), "an untempered edge holds a little heat, not a blue heat");
            Assert.IsTrue(double.IsPositiveInfinity(CraftSurface.TemperRunsAt(w.Rules, w.Piece(Bar(w, "Spring steel")).segments[3].edge)), "unhardened steel has no temper to lose");
            List<CraftEvent> seen = Events(w);
            for (int k = 0; k < 40 && !seen.Any(e => e.Id == "temper_drawn"); k++)
                Assert.IsTrue(w.Grind(bar, 10, 14, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 1, 1, out string why), why);
            CraftEvent drawn = seen.FirstOrDefault(e => e.Kind == CraftEventKind.Note && e.Id == "temper_drawn");
            Assert.IsNotNull(drawn, "pressed hard without a dip, the wheel draws the temper - and says so");
            Assert.AreEqual("grind_heat", CraftJournal.InsightOf(drawn, p));
            Assert.Greater(p.segments.Max(g => g.temperFilm), 200, "the freshly ground steel takes the colours of the heat");
            Assert.GreaterOrEqual(p.segments[12].polish, CraftSurface.FilmPolish, "bright enough to show them");
        }

        // ------------------------------------------------------------------ the surface (surface-11, m02, m03, 23)
        [Test]
        public void Abrasion_DullsAMirror_AndPolishNeverGoesBelowFreshlyGround()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments) { g.rough = 0.3; g.polish = 0.95; g.scale = 0; }
            Assert.IsTrue(w.Grind(id, 0, 23, CraftSurfaceSide.Faces, "coarse_wheel", 0, 0.6, 1, out string why), why);
            MetalSegment s = p.segments[8];
            Assert.Greater(s.rough, 10, "the coarse wheel scratches it");
            Assert.AreEqual(CraftSurface.GroundPolish, s.polish, 1e-9, "and its mirror is gone");
            Assert.IsTrue(w.Polish(id, 0, 23, "coarse_emery", 0.5, 0.5, out why), why);
            Assert.GreaterOrEqual(s.polish, CraftSurface.GroundPolish, "the first emery pass does not make it duller than the wheel left it");
            for (int k = 0; k < 20; k++) w.Grind(id, 0, 23, CraftSurfaceSide.Faces, "fine_wheel", 0, 0.5, 1, out _);
            Assert.AreEqual(CraftSurface.PolishOf(s.rough), s.polish, 1e-9);
            Assert.Greater(s.polish, CraftSurface.PolishOf(25) + 0.05, "the fine wheel leaves it brighter than the coarse one");
        }

        [Test]
        public void SkippingAGrit_IsNoted()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments) { g.rough = 25; g.scale = 0; }
            List<CraftEvent> seen = Events(w);
            Assert.IsTrue(w.Polish(id, 0, 23, "fine_emery", 0.5, 1, out string why), why);
            CraftEvent note = seen.FirstOrDefault(e => e.Kind == CraftEventKind.Note && e.Id == "skipped_grit");
            Assert.IsNotNull(note, "the fine emery cannot take out the wheel's scratches: it says so");
            Assert.AreEqual("grits", CraftJournal.InsightOf(note, p));
        }

        [Test]
        public void ACoat_GoesWithTheSurface_AndBurnsOffHotMetal()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments) g.scale = 0;
            string oil = w.Rules.Supplies.First(s => s.kind == CraftSupplyKind.Oil).id;
            Assert.IsTrue(w.Coat(id, oil, out string why), why);
            Assert.Greater(p.segments[4].coating, 0.2);
            Assert.IsTrue(w.Grind(id, 0, 9, CraftSurfaceSide.Faces, "coarse_wheel", 0, 0.5, 0.5, out why), why);
            Assert.AreEqual(0, p.segments[4].coating, 1e-9, "ground, the oil goes with the surface");
            Assert.Greater(p.segments[15].coating, 0.2, "where it was not worked it stays");
            Assert.IsTrue(w.Descale(id, 14, 16, "wire_brush", 0.5, 1, out why), why);
            Assert.AreEqual(0, p.segments[15].coating, 1e-9, "brushed off too");
            Assert.IsTrue(w.Coat(id, oil, out why), why);
            LightForge(w);
            HeatTo(w, id, 420);
            Assert.Less(p.segments.Max(g => g.coating), 0.01, "a hot surface burns the oil off");
            string wood = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30));
            Assert.IsFalse(w.Coat(wood, oil, out why), "wood is oiled at the finishing bench");
            Assert.IsFalse(w.Polish(wood, 0, 5, "rouge", 0.5, 1, out why), "and sanded there, not polished with rouge");
        }

        // ------------------------------------------------------------------ work that does nothing (surface-18)
        [Test]
        public void AFileThatCannotBite_IsRefused_AndTeachesWhy()
        {
            CraftWorkshop w = Workshop();
            string bar = HardBar(w);
            List<CraftEvent> seen = Events(w);
            int ops = w.History.Count;
            Assert.IsFalse(w.File(bar, 0, 23, CraftSurfaceSide.Faces, "bastard_file", 0, 0.6, 1, out string why));
            StringAssert.Contains("skates", why);
            Assert.AreEqual(ops, w.History.Count, "nothing is recorded as filed");
            CraftEvent note = seen.FirstOrDefault(e => e.Kind == CraftEventKind.Note && e.Id == "skate");
            Assert.IsNotNull(note, "the journal still learns the file skates on hardened steel");
            Assert.AreEqual("file_skate", CraftJournal.InsightOf(note, w.Piece(bar)));
            string soft = Bar(w, "Mild steel");
            Assert.IsFalse(w.File(soft, 0, 23, CraftSurfaceSide.Eye, "needle_file", 0, 0.6, 1, out why), "there is no eye to file");
            StringAssert.Contains("no eye", why);
            w.Piece(soft).segments[5].hole = 6;
            Assert.IsTrue(w.File(soft, 3, 7, CraftSurfaceSide.Eye, "needle_file", 0, 0.6, 1, out why), why);
        }

        // ------------------------------------------------------------------ the whetstone (surface-05, 06, m01, 07, 08)
        [Test]
        public void TheStone_HonesOnlyWhereItReachesTheApex()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 2.5;
                g.bevelWidthL = g.bevelWidthR = 2.5 / Tan(22);
            }
            List<CraftEvent> seen = Events(w);
            for (int k = 0; k < 40; k++)
                Assert.IsTrue(w.Sharpen(id, 0, 23, CraftSurfaceSide.LeftBevel, "medium_stone", 5, 0.5, 1, 0, true, out string why), why);
            MetalSegment s = p.segments[10];
            Assert.AreEqual(0, s.honeL, 1e-9, "held far below the bevel, the stone never reaches the apex");
            CraftEvent note = seen.FirstOrDefault(e => e.Kind == CraftEventKind.Note && e.Id == "shoulder");
            Assert.IsNotNull(note, "it says it only rubs the bevel's shoulder");
            Assert.AreEqual("stone_angle", CraftJournal.InsightOf(note, p));
            for (int k = 0; k < 40; k++)
                w.Sharpen(id, 0, 23, CraftSurfaceSide.LeftBevel, "medium_stone", 22, 0.5, 1, 0, true, out _);
            Assert.Greater(s.honeL, 0.3, "at the bevel's angle it hones");
            Assert.GreaterOrEqual(s.microL, 22 - 1e-6, "and never finer than the bevel under it");
            Assert.AreEqual(1, CraftSurface.Contact(22, 22), 1e-9);
            Assert.AreEqual(0, CraftSurface.Contact(17, 22), 1e-9);
        }

        [Test]
        public void LightTrailingStrokes_TakeTheBurrOff_AndOnlyLeadingStrokesRaiseIt()
        {
            Assert.AreEqual(0, CraftSurface.BurrOff(CraftSurface.BurrOffBelow), 1e-12);
            Assert.AreEqual(0.35, CraftSurface.BurrOff(CraftSurface.BurrOffFull), 1e-12);
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 2.5;
                g.bevelWidthL = g.bevelWidthR = 2.5 / Tan(15);
                g.honeL = g.honeR = 1.2;
                g.microL = g.microR = 15;
                g.burr = 1;
            }
            MetalSegment s = p.segments[10];
            for (int k = 0; k < 20; k++)
                Assert.IsTrue(w.Sharpen(id, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "medium_stone", 15, 0.45, 1, 0, false, out string why), why);
            Assert.Less(s.burr, CraftSurface.BurrShows, "trailing strokes at the slider's own 0.45 take it off (they used to grow it)");
            double burr = s.burr;
            for (int k = 0; k < 10; k++) w.Sharpen(id, 0, 23, CraftSurfaceSide.LeftBevel, "medium_stone", 15, 0.8, 1, 0, false, out _);
            Assert.AreEqual(burr, s.burr, 1e-9, "heavy trailing strokes neither raise it nor take it off");
            for (int k = 0; k < 10; k++) w.Sharpen(id, 0, 23, CraftSurfaceSide.LeftBevel, "medium_stone", 15, 0.5, 1, 0, true, out _);
            Assert.Greater(s.burr, burr + 0.05, "edge-leading strokes on a honed face raise it");
        }

        [Test]
        public void NoEdge_NoHoning_AndAFlatIsWornAwayOnlySlowly()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            Assert.IsFalse(w.Sharpen(bar, 0, 23, CraftSurfaceSide.LeftBevel, "coarse_stone", 15, 0.5, 1, 0, true, out string why), "a square bar has no edge to hone");
            StringAssert.Contains("no edge", why);
            CraftPiece p = w.Piece(bar);
            // Bevels down, a 1.5 mm flat still along the edge.
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 1.75;
                g.bevelWidthL = g.bevelWidthR = 1.75 / Tan(15);
            }
            Assert.IsTrue(CraftSurface.HasEdge(p, 0, 23));
            MetalSegment s = p.segments[10];
            double flat = s.EdgeThickness;
            for (int k = 0; k < 300; k++)
                Assert.IsTrue(w.Sharpen(bar, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "coarse_stone", 15, 0.5, 1, 0, true, out why), why);
            Assert.Less(s.EdgeThickness, flat, "the stone wears the flat down");
            Assert.Greater(s.EdgeThickness, CraftSurface.FlatGone, "but slowly: 300 strokes do not take a 1.5 mm flat away");
            Assert.Less(CraftEvaluator.Sharpness(s), 0.3, "and with a flat there is no keen edge");
            Assert.GreaterOrEqual(s.apex, 40 + 60 * s.EdgeThickness - 1e-6);
            Assert.AreEqual(0, s.microAngle, 1e-9, "no micro-bevel while a flat shows");
        }

        [Test]
        public void HonedLongEnough_TheApexComesToTheFloorTheWindowShows()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 2.5;
                g.bevelWidthL = g.bevelWidthR = 2.5 / Tan(15);
            }
            MetalSegment s = p.segments[10];
            CraftAbrasive stone = w.Rules.Abrasive("medium_stone");
            for (int k = 0; k < 400; k++)
                w.Sharpen(id, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "medium_stone", 15, 0.5, 1, 0, k < 200, out _);
            double floor = CraftSurface.SharpenFloor(w.Rules, p, s, stone, 0);
            Assert.AreEqual(floor, s.apex, floor * 0.1, "the edge comes to the floor the window computes");
            Assert.Less(s.burr, CraftSurface.BurrShows);
            Assert.AreEqual(CraftSurface.HoneStep.AtBest, CraftSurface.NextHone(1, 1, s.burr, s.EdgeThickness, true, s.apex, floor),
                "unhardened spring steel comes to its best (the window no longer promises more strokes forever)");
            Assert.Greater(floor, stone.apex * 1.3, "a soft steel stops short of the stone's own apex");
        }

        [Test]
        public void NextHone_FollowsTheModelsOwnThresholds()
        {
            Assert.AreEqual(CraftSurface.HoneStep.NoEdge, CraftSurface.NextHone(0, 0, 0, 5, false, 300, 7));
            Assert.AreEqual(CraftSurface.HoneStep.WearFlat, CraftSurface.NextHone(0, 0, 0, 1, true, 300, 7));
            Assert.AreEqual(CraftSurface.HoneStep.HoneFace, CraftSurface.NextHone(0.97, 0, 0, 0, true, 120, 7), "under 1 the face is not honed to the apex yet");
            Assert.AreEqual(CraftSurface.HoneStep.TurnFace, CraftSurface.NextHone(1, 0, 0.5, 0, true, 120, 7), "never 'both honed' while the other face is untouched");
            Assert.AreEqual(CraftSurface.HoneStep.TakeBurrOff, CraftSurface.NextHone(1, 1, 0.5, 0, true, 20, 7));
            Assert.AreEqual(CraftSurface.HoneStep.KeepHoning, CraftSurface.NextHone(1, 1, 0, 0, true, 20, 7));
            Assert.AreEqual(CraftSurface.HoneStep.AtBest, CraftSurface.NextHone(1, 1, 0, 0, true, 7.5, 7));
            Assert.AreEqual(CraftSurface.HoneStep.StoneTooCoarse, CraftSurface.NextHone(1, 1, 0, 0, true, 3, 13));
        }

        [Test]
        public void SharpeningOnOneStone_IsOneLineInTheRecord()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            foreach (MetalSegment g in p.segments)
            {
                g.bevelDepthL = g.bevelDepthR = 2.5;
                g.bevelWidthL = g.bevelWidthR = 2.5 / Tan(15);
            }
            int lines = p.record.Count;
            for (int k = 0; k < 60; k++)
                w.Sharpen(id, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "medium_stone", 14 + k % 3, 0.5, 1, 0, true, out _);
            Assert.AreEqual(lines + 1, p.record.Count, "strokes on alternate faces fold into one line per stone");
            Assert.AreEqual(60, p.record[p.record.Count - 1].count);
        }

        // ------------------------------------------------------------------ the stroke's segments (surface-09)
        [Test]
        public void Stroke_SpansSegmentsByTheirRealLengths()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel");
            CraftPiece p = w.Piece(id);
            for (int i = 0; i < p.segments.Count; i++) p.segments[i].length = i % 2 == 0 ? 6 : 14;
            for (double x = 0.05; x < 1; x += 0.1)
            {
                var s = new CraftStroke();
                s.Add(0, x, 0.5, 15, 0.5, null);
                s.Add(0.05, x + 0.02, 0.5, 15, 0.5, null);
                s.Span(p, out int from, out int to);
                Assert.AreEqual(p.SegmentAt(x), from, $"at {x:0.00}: the segment drawn there");
                Assert.AreEqual(p.SegmentAt(x + 0.02), to);
            }
            new CraftStroke().Span(new CraftPiece(), out int a, out int b);
            Assert.AreEqual(-1, a + b + 1, "no segments, no span");
        }

        // ------------------------------------------------------------------ the strip judges as the evaluator does (surface-12)
        [Test]
        public void Judge_ReadsTheEdgeAsTheEvaluatorDoes()
        {
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(Seg(2.5, 2.5), 30, 5, 30, true).progress, "met in the middle at the design's angle");
            Assert.AreEqual(CraftSurface.Progress.ToGrind, CraftSurface.Judge(Seg(5, 0), 30, 5, 30, true).progress, "a chisel grind is not done: the other face wants its half");
            Assert.AreEqual(CraftSurface.Progress.Off, CraftSurface.Judge(Seg(2.5, 2.5, 25), 30, 5, 30, true).progress, "met at 25° a side on a 30° design: off");
            Assert.AreEqual(CraftSurface.Progress.ToGrind, CraftSurface.Judge(Seg(1.5, 1.5), 30, 5, 30, true).progress);
            Assert.AreEqual(CraftSurface.Progress.TooFar, CraftSurface.Judge(Seg(0, 0, 15, 26), 30, 5, 0, false).progress, "13% narrower is too far");
            Assert.AreEqual(CraftSurface.Progress.Off, CraftSurface.Judge(Seg(0, 0, 15, 28.5), 30, 5, 0, false).progress, "5% narrower is off");
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(Seg(0, 0, 15, 30.5), 30, 5, 0, false).progress);
            Assert.AreEqual(CraftSurface.Progress.ToGrind, CraftSurface.Judge(Seg(0, 0, 15, 33), 30, 5, 0, false).progress, "wider than the design: still to grind");
        }

        // ------------------------------------------------------------------ a dip to cool is not a quench (surface-03)
        [Test]
        public void ADipToCool_IsNotAQuench()
        {
            CraftWorkshop w = Workshop();
            string bar = HardBar(w);
            CraftPiece p = w.Piece(bar);
            Assert.AreEqual(1, p.Tag(CraftThermal.QuenchHardeningTag), 1e-9, "the hardening quench was one");
            StringAssert.StartsWith("Quenched in", p.record.Last(r => r.kind == CraftOpKind.MovePiece && r.text.Contains("Water")).text);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why), why);
            Assert.AreEqual("Dipped in Water to cool", p.record[p.record.Count - 1].text, "a cold, hardened blade is only dipped");
            Assert.AreEqual(0, p.Tag(CraftThermal.QuenchHardeningTag), 1e-9);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out why), why);
        }

        [Test]
        public void DragonsBlood_LendsItsElement_OnlyToWhatHardensInIt()
        {
            CraftWorkshop water = Workshop();
            string hard = HardBar(water);
            // The same hardened, cold blade dipped in dragon's blood takes nothing.
            CraftWorkshop blood = Workshop("dragon_blood");
            string dipped = blood.AddPiece(water.Piece(hard).Clone());
            Assert.IsTrue(blood.MovePiece(dipped, CraftLocation.Quench, out string why), why);
            blood.RunSteps(30);
            Assert.IsTrue(blood.MovePiece(dipped, CraftLocation.Air, out why), why);
            Assert.Less(blood.Piece(dipped).Tag("affinity.Fire"), 0.01, "a dip of a blade hardened elsewhere takes nothing (a trace at most)");
            // Quenched hard in it, it does.
            string quenched = Bar(blood, "Tool steel");
            LightForge(blood);
            HeatTo(blood, quenched, 820);
            Assert.IsTrue(blood.MovePiece(quenched, CraftLocation.Quench, out why), why);
            blood.Agitate(quenched, CraftQuenchMotion.UpDown, out _);
            blood.RunSteps(400);
            Assert.Greater(blood.Piece(quenched).Tag("affinity.Fire"), 0.05, "what hardens in dragon's blood takes its fire");
        }
    }
}
#endif
