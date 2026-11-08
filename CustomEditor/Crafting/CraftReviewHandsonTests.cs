#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the 3D work places' review (CraftHandsOn) - the rules they measure the hands by and show: the bit
    /// counts the circles the hand draws round their own middle, wherever they start (CraftHandCircles); the hammer's face is
    /// level while the hand drifts a little (CraftForging.TiltFrom); a knife stroke is judged and shown against its own line
    /// (CraftWoodwork.StrokeSteady, StrokeWander's newest offset); the bellows' meter is the draft the fire gets
    /// (CraftThermal.DraftShare) and the pump's rhythm is said (CraftBellowsPump.Rhythm); the ore bench's Clean meter is the
    /// station's own dirt share (CraftOres.BrushTo); the blow's meter is full at a full blow (CraftHammer.FullBlow).
    /// </summary>
    public class CraftReviewHandsonTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();
        private static CraftWorkshop Workshop() => new CraftWorkshop(Rules(), CraftTools.Everything, 18, 1, 3, "water", "handsonrev");

        // ------------------------------------------------------------------ the bit's circles (handson-r01)
        /// <summary>
        /// The turns the bore sends for a hand pressed at <paramref name="press"/> and then at <paramref name="at"/>(t), sampled at
        /// <paramref name="fps"/> (one CraftHandCircles.Add a frame, as the 3D bench; each whole turn spent and sent with its steadiness).
        /// </summary>
        private static List<double> Bore(Vector2 press, Func<double, Vector2> at, double seconds, double fps, double minStep = 2)
        {
            var circles = new CraftHandCircles(minStep);
            var sent = new List<double>();
            circles.Begin(press.x, press.y);
            for (int f = 0; f <= (int)(seconds * fps); f++)
            {
                Vector2 p = at(f / fps);
                circles.Add(p.x, p.y);
                if (circles.Turns >= 1)
                {
                    circles.Spend(1);
                    sent.Add(circles.Steadiness);
                }
            }
            return sent;
        }

        private static string Show(List<double> s) => $"{s.Count} turns{(s.Count > 0 ? ", steadiness " + string.Join(" ", s.ConvertAll(v => v.ToString("0.00"))) : "")}";

        [Test]
        public void Bore_SteadyCirclesCount_WhereverTheHandStartsThem()
        {
            var press = new Vector2(1500f, 540f);
            var report = new List<string>();
            foreach (float r in new[] { 40f, 80f })
                foreach (double fps in new[] { 30.0, 60.0, 144.0 })
                {
                    double w = 3 * Math.PI; // 1.5 circles a second, for 4 s: six circles
                    Vector2 Circle(double t, float offX) => press + new Vector2(r * (float)Math.Cos(w * t) + offX, r * (float)Math.Sin(w * t));
                    List<double> fromPress = Bore(press, t => Circle(t, -r), 4, fps);      // the press on the circle: the hand circles from where it is
                    List<double> roundPress = Bore(press, t => Circle(t, 0f), 4, fps);     // the press in the middle
                    List<double> offMiddle = Bore(press, t => Circle(t, -0.5f * r), 4, fps); // the press half a radius off the middle
                    report.Add($"R {r:0} px, {fps:0} fps: from the press {Show(fromPress)}; round it {Show(roundPress)}; half a radius off {Show(offMiddle)}");
                    foreach (List<double> sent in new[] { fromPress, roundPress, offMiddle })
                    {
                        Assert.GreaterOrEqual(sent.Count, 5, "six steady circles bore five turns at least (the first lap counts once the path shows its middle): " + report[report.Count - 1]);
                        Assert.LessOrEqual(sent.Count, 6, "no more turns than circles: " + report[report.Count - 1]);
                        foreach (double s in sent)
                            Assert.Greater(s, 0.9, "a steady circle reads steady, far from splitting the wood: " + report[report.Count - 1]);
                    }
                }
            Console.WriteLine(string.Join("\n", report));
        }

        [Test]
        public void Bore_ADragOrABackAndForth_TurnsNothing()
        {
            var press = new Vector2(600f, 400f);
            Assert.AreEqual(0, Bore(press, t => press + new Vector2((float)(300 * t), (float)(40 * t)), 2, 60).Count, "a straight drag is no circle");
            // Back and forth along a line (with a hand's small wobble across it): no circle either.
            List<double> wiggle = Bore(press, t => press + new Vector2((float)(80 * Math.Sin(8 * t)), (float)(2 * Math.Sin(31 * t))), 4, 60);
            Assert.AreEqual(0, wiggle.Count, "a back-and-forth turns the bit no way: " + Show(wiggle));
        }

        [Test]
        public void Bore_AnUnevenLoop_ReadsUnsteady_ASteadyOneDoesNotSplit()
        {
            var press = new Vector2(600f, 400f);
            // A loop three times as long as it is wide: it turns the bit, but wobbling - each such turn splits the wood a little.
            List<double> squashed = Bore(press, t => press + new Vector2((float)(90 * Math.Cos(3 * Math.PI * t) - 90), (float)(30 * Math.Sin(3 * Math.PI * t))), 4, 60);
            Assert.Greater(squashed.Count, 0, "a squashed loop still turns the bit");
            foreach (double s in squashed)
                Assert.Less(s, CraftWoodwork.CarveSplitSteady, "an uneven loop reads unsteady: " + Show(squashed));
            // The workshop: a turn as steady as a true circle reads does not split the blank.
            CraftWorkshop w = Workshop();
            string blank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 400, 40, 30));
            List<double> round = Bore(press, t => press + new Vector2((float)(50 * Math.Cos(3 * Math.PI * t) - 50), (float)(50 * Math.Sin(3 * Math.PI * t))), 4, 60);
            foreach (double s in round)
                Assert.IsTrue(w.Carve(blank, 0, 0, CraftSurface.CarveCut.Bore, 1.2 * 0.5 + 0.3, s, out string why), why);
            Assert.AreEqual(0, w.Piece(blank).wood.split, 1e-9, "turns drawn as steady circles bore it without splitting it");
        }

        // ------------------------------------------------------------------ the hammer's tilt (handson-r04)
        [Test]
        public void Tilt_AHandThatDriftsALittle_StrikesFlat_ADeliberateTiltBevels()
        {
            const float screen = 1080f;
            double Tilt3D(double px) => CraftForging.TiltFrom(px, screen * 0.02, screen * 0.12); // the 3D anvil
            Assert.AreEqual(0, Tilt3D(20), 1e-9, "a 20 px drift on a 1080 px screen is level");
            Assert.AreEqual(0, Tilt3D(-15), 1e-9, "and so is one down");
            Assert.Less(Math.Abs(Tilt3D(22)), 0.01, "22 px all but level");
            Assert.LessOrEqual(Math.Abs(Tilt3D(30)), CraftForging.FlatTilt, "30 px still strikes flat");
            Assert.Greater(Tilt3D(70), CraftForging.FlatTilt, "a hand moved up on purpose tilts the face");
            Assert.Less(Tilt3D(-70), -CraftForging.FlatTilt, "or down");
            Assert.AreEqual(1, Tilt3D(400), 1e-9, "a full tilt at most");
            Assert.AreEqual(0, CraftForging.TiltFrom(8, 8, 60), 1e-9, "the window's anvil: level within 8 px");
            Assert.AreEqual(0.5, CraftForging.TiltFrom(38, 8, 60), 1e-9, "then 60 px more to a full tilt");

            // What the workshop forges from them: the drift strikes flat, the deliberate tilt bevels the face that is up.
            CraftWorkshop w = Workshop();
            (string, double)[] steel = CraftDefaults.StockMetals().Find(m => m.name == "Spring steel").composition;
            string drifted = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 6, steel)), tilted = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 6, steel));
            foreach (string id in new[] { drifted, tilted })
            {
                w.Piece(id).edges = 1;
                foreach (MetalSegment s in w.Piece(id).segments) s.edge.temp = s.body.temp = 1000;
            }
            Assert.IsTrue(w.Strike(drifted, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.9, Tilt3D(22), out string why), why);
            Assert.IsTrue(w.Strike(tilted, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.9, Tilt3D(90), out why), why);
            double Bevel(string id) { double b = 0; foreach (MetalSegment s in w.Piece(id).segments) b += s.bevelDepthL + s.bevelDepthR; return b; }
            Assert.AreEqual(0, Bevel(drifted), 1e-9, "the drifting hand's blow is flat");
            Assert.Greater(Bevel(tilted), 0.05, "the tilted blow forges a bevel");
        }

        // ------------------------------------------------------------------ the knife's line (handson-r05)
        [Test]
        public void Knife_AStrokeIsJudgedAgainstItsOwnLine_AndTheHandShownOffIt()
        {
            var hands = new CraftHands(1);
            double tolerance = 1080 * 0.04; // the 3D bench: 4 % of the screen's height
            // A straight stroke down a slope is steady (only the hands' tremble takes anything off).
            var slope = new List<Vector2>();
            for (int i = 0; i <= 40; i++) slope.Add(new Vector2(200 + i * 15, 300 + i * 6));
            Assert.AreEqual(Math.Max(0, 1 - hands.Unsteadiness * 5), CraftWoodwork.StrokeSteady(slope, tolerance, hands.Unsteadiness), 1e-9, "straight down a slope: steady");
            Assert.AreEqual(0, CraftWoodwork.StrokeWander(slope, out double off), 1e-3);
            Assert.AreEqual(0, off, 1e-3, "the hand on the stroke's own line");
            // The hand wandering 55 px up and down across it: below the split band - the meter's red band is where the workshop splits it.
            var wander = new List<Vector2>();
            for (int i = 0; i <= 40; i++) wander.Add(new Vector2(200 + i * 15, 300 + 27.5f * (float)Math.Sin(i * 0.6)));
            double steady = CraftWoodwork.StrokeSteady(wander, tolerance, hands.Unsteadiness);
            Assert.Less(steady, CraftWoodwork.CarveSplitSteady, $"a 55 px wander reads {steady:0.00}: in the red band");
            CraftWorkshop w = Workshop();
            string blank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 400, 40, 30));
            Assert.IsTrue(w.Carve(blank, 2, 5, CraftSurface.CarveCut.Width, 1.0, steady, out string why), why);
            Assert.Greater(w.Piece(blank).wood.split, 0, "and the workshop splits it, as the red band said");
            // Where the hand is now against the stroke's line: up (+) when the newest sample is above it.
            var up = new List<Vector2>(slope) { new Vector2(200 + 41 * 15, 300 + 41 * 6 + 20) };
            CraftWoodwork.StrokeWander(up, out off);
            Assert.Greater(off, 10, "the hand above the line");
            var down = new List<Vector2>(slope) { new Vector2(200 + 41 * 15, 300 + 41 * 6 - 20) };
            CraftWoodwork.StrokeWander(down, out off);
            Assert.Less(off, -10, "the hand below it");
        }

        // ------------------------------------------------------------------ the bellows (handson-r10)
        [Test]
        public void Bellows_TheMeterIsTheDraftTheFireGets()
        {
            CraftWorkshop w = Workshop();
            CraftSimulationSettings s = w.Rules.Settings;
            var pump = new CraftBellowsPump();
            var hands = new CraftHands(1);
            double still = CraftThermal.DraftShare(s, w.Forge);
            Assert.AreEqual(s.draftBase / (s.draftBase + s.bellowsDraft), still, 1e-6, "the vent open, no air pumped: the fire's own draft");
            pump.Tick(5);
            double airBefore = w.Forge.air;
            w.Bellows(pump.Pump(hands));
            double gained = w.Forge.air - airBefore;
            Assert.Greater(gained, 0);
            Assert.AreEqual(still + s.bellowsDraft * gained / (s.draftBase + s.bellowsDraft), CraftThermal.DraftShare(s, w.Forge), 1e-6, "a squeeze raises the meter by what it gave the fire");
            Assert.AreEqual(CraftThermal.Draft(s, w.Forge), (s.draftBase + s.bellowsDraft * w.Forge.air) * w.Forge.damper, 1e-9, "the fire's draft");
            // The vent shut: whatever is pumped, the fire gets no draft - nor does the meter show any.
            w.SetDamper(0);
            for (int k = 0; k < 6; k++) { pump.Tick(0.8); w.Bellows(pump.Pump(hands)); }
            Assert.AreEqual(0, CraftThermal.DraftShare(s, w.Forge), 1e-9, "the vent shut: no draft");
            Assert.AreEqual(1, pump.Rhythm, 1e-9, "pumped every 0.8 s: a steady rhythm");
            pump.Tick(0.1);
            pump.Pump(hands);
            Assert.AreEqual(0.35, pump.Rhythm, 1e-9, "a pump straight after: frantic");
            Assert.AreEqual(0, pump.SinceLast, 1e-9);
        }

        // ------------------------------------------------------------------ the ore bench's Clean meter (handson-r11)
        [Test]
        public void OreBench_TheCleanMeter_IsTheStationsDirtShare()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "bog_iron", 9, 0));
            CraftPiece lump = w.Piece(id);
            CraftOreDefinition o = w.Rules.Ore(lump.ore.ore);
            OreFragment f = lump.ore.fragments[0];
            f.dirt = 0.2; // bog iron's dirtMax in the default content
            f.gangue = Math.Max(0, 1 - f.mineral - f.dirt);
            OreFragment atBench = f.Clone(); // the stone as it was put on the bench
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Stiff, 0.8334, out string why), why);
            OreFragment shown = atBench.Clone();
            CraftOres.BrushTo(shown, o, CraftOreBrush.Stiff, 0.8334); // what the 3D has gone over with the stiff brush
            OreFragment after = w.Piece(id).ore.fragments[0];
            Assert.AreEqual(after.dirt, shown.dirt, 1e-6, $"the meter shows {shown.dirt * 100:0.0} % dirt, the station has {after.dirt * 100:0.0} %");
            Assert.Greater(after.dirt, 0.05, "and both still call it dirty (clean is 5 % or less)");
            // More brushes in another order: the same stone in the end, as the station has it.
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Wire, 0.6, out why), why);
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Soft, 0.9, out why), why);
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Wire, 0.3, out why), why);
            OreFragment again = atBench.Clone();
            CraftOres.BrushTo(again, o, CraftOreBrush.Soft, 0.9);
            CraftOres.BrushTo(again, o, CraftOreBrush.Wire, 0.9);
            CraftOres.BrushTo(again, o, CraftOreBrush.Stiff, 0.8334);
            after = w.Piece(id).ore.fragments[0];
            Assert.AreEqual(after.dirt, again.dirt, 1e-6, "the dirt share");
            Assert.AreEqual(after.mass, again.mass, 1e-6 * after.mass, "the stone's mass");
            Assert.AreEqual(after.mineral, again.mineral, 1e-6, "the ore in it");
            Assert.AreEqual(after.RindShare, again.RindShare, 1e-6, "the rind");
        }

        // ------------------------------------------------------------------ the blow's meter (handson-r12)
        [Test]
        public void Anvil_TheBlowMeter_IsFullAtAFullBlow_AndHeldTooLongTheArmWavers()
        {
            var hammer = new CraftHammer();
            var hands = new CraftHands(1);
            hammer.Raise();
            double most = 0;
            bool wavered = false;
            for (int f = 0; f < 120; f++)
            {
                hammer.Tick(1 / 60.0);
                most = Math.Max(most, hammer.Expected(hands) / hammer.FullBlow);
                wavered |= hammer.Wavers;
            }
            Assert.AreEqual(1, most, 1e-6, "held long enough, the blow's meter fills: a full blow");
            Assert.IsTrue(wavered, "held on past it, the arm wavers (the meter turns red)");
            Assert.AreEqual(1.2, new CraftHammer { weight = 2 }.FullBlow, 1e-9, "a heavier hammer's full blow is harder");
        }
    }
}
#endif
