#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the surface's review (the wheel, the file, the brush and the buff; the whetstone): the guard
    /// grants a short touch of the wheel it can cover and charges the wheel for every stretch it works; the wheel's time
    /// is shared out over the stretch it is moved along; a dip of a hardened blade the wheel heated is a dip, and the
    /// wheel's dip is refused to a piece that can still harden; the strip judges an edge at the angle the design's own
    /// section carries (and never calls one wider or thicker than the design done); a grit is done where the next one
    /// takes over; the look, the stone and the Grind area say the same of a surface and an edge; a full bar on the stone
    /// is full, and the strop is drawn spine first; descaling takes a brush, and a coat a piece in the hands.
    /// </summary>
    public class CraftReviewSurfaceTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(string medium = "water") => new CraftWorkshop(Rules(), CraftTools.Everything, 18, 1, 3, medium, "surfacerev");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 5) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);

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

        /// <summary>Into the fire until the bar is past <paramref name="target"/> through, then out into the air.</summary>
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

        /// <summary>A tool-steel bar hardened in water (worked up and down) and cold again: martensite, a few hundredths of retained austenite.</summary>
        private static string HardBar(CraftWorkshop w)
        {
            string bar = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, bar, 820);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why), why);
            Assert.AreEqual(1, w.Piece(bar).Tag(CraftThermal.QuenchHardeningTag), "from a hardening heat: a quench");
            w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
            w.RunSteps(400);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out why), why);
            w.RunSteps(50);
            Cool(w.Piece(bar));
            return bar;
        }

        // ------------------------------------------------------------------ the guard and the wheel's time
        private static CraftRequest Work(CraftRequestKind kind, string piece, int from, int to, float seconds)
        {
            var r = new CraftRequest(kind, piece) { i0 = from, i1 = to, a = 15f, b = 0.5f };
            if (kind == CraftRequestKind.Grind || kind == CraftRequestKind.File) r.c = seconds;
            else { r.a = 0.5f; r.b = seconds; }
            return r;
        }

        private static float Seconds(in CraftRequest r) => r.kind == CraftRequestKind.Grind || r.kind == CraftRequestKind.File ? r.c : r.b;

        private static readonly CraftRequestKind[] SurfaceKinds = { CraftRequestKind.Grind, CraftRequestKind.File, CraftRequestKind.Descale, CraftRequestKind.Polish };

        /// <summary>
        /// A touch of the wheel (or the file, the brush, the buff) is sent as a frame of it (1/60 s), and so is a press let
        /// go - or slid off the blade - in the frame after a periodic send: while the allowance covers it, it is granted as
        /// claimed, not refused as "too fast". Once the allowance is spent, it is refused.
        /// </summary>
        [Test]
        public void Guard_GrantsAShortTouchTheAllowanceCovers_RefusesItOnceSpent()
        {
            foreach (CraftRequestKind kind in SurfaceKinds)
                foreach (float touch in new[] { 1f / 60f, 1f / 144f, 0.019f })
                {
                    var g = new CraftRequestGuard(1, 0.5);
                    CraftRequest r = Work(kind, "bar", 12, 12, touch);
                    Assert.IsTrue(g.Check(ref r, 100.0, out string why), $"{kind}: a touch of {touch:0.000} s with a full allowance was refused: '{why}'");
                    Assert.AreEqual(touch, Seconds(r), 1e-6, $"{kind}: granted as claimed");
                    Assert.AreEqual("", why, $"{kind}: nothing to say about a touch granted whole");
                }
            // Spent (a whole allowance claimed at once): a touch right after it is too fast.
            var spent = new CraftRequestGuard(1, 0.5);
            CraftRequest all = Work(CraftRequestKind.Grind, "bar", 0, 0, 60f);
            Assert.IsTrue(spent.Check(ref all, 100.0, out _));
            CraftRequest touchAfter = Work(CraftRequestKind.Grind, "bar", 0, 0, 1f / 60f);
            Assert.IsFalse(spent.Check(ref touchAfter, 100.001, out string late), "nothing left");
            StringAssert.StartsWith("Too fast", late);
        }

        /// <summary>
        /// The wheel works every stretch of the request for its seconds: the guard charges the seconds times the stretches
        /// (as many as the piece has there), so a request over the whole blade costs 24 times one over a stretch, and what is
        /// granted is spread back over them. Claimed for the whole blade every 0.2 s, the wheel works no more than the
        /// allowance holds and refills - not 24 times what the hands can do.
        /// </summary>
        [Test]
        public void Guard_ChargesTheToolForEveryStretchItWorks()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            int n = w.Piece(bar).segments.Count;
            foreach (CraftRequestKind kind in SurfaceKinds)
            {
                var g = new CraftRequestGuard(1, 0.5);
                double worked = 0, claimed = 0;
                for (int k = 0; k < 10; k++)
                {
                    CraftRequest r = Work(kind, bar, 0, n - 1, 0.2f);
                    claimed += 0.2 * n;
                    if (g.Check(ref r, 100.0 + 0.2 * k, out _, w))
                        worked += Seconds(r) * n;
                }
                // what the allowance held at first (3 s) and what 1.8 s of real time refilled (1.25 a second)
                Assert.LessOrEqual(worked, 3 + 1.25 * 1.8 + 1e-3, $"{kind}: {worked:0.0} of the {claimed:0} segment-seconds claimed in 2 s were worked");
            }
            // The stretches are the piece's: a range past its ends is charged for what is there.
            var past = new CraftRequestGuard(1, 0.5);
            CraftRequest wide = Work(CraftRequestKind.Grind, bar, -500, 500, 0.1f);
            Assert.IsTrue(past.Check(ref wide, 100.0, out string why, w), why);
            Assert.AreEqual(0.1f, wide.c, 1e-6, $"{n} stretches at 0.1 s: 2.4 segment-seconds, within the allowance");
            CraftRequest more = Work(CraftRequestKind.Grind, bar, -500, 500, 0.1f);
            Assert.IsTrue(past.Check(ref more, 100.0, out why, w));
            Assert.AreEqual(0.6 / n, more.c, 1e-4, "what was left (0.6 segment-seconds) spread over the blade");
            StringAssert.Contains("only part", why);
        }

        /// <summary>
        /// The window and the 3D wheel share the wheel's time out over the stretch it was moved along: held still on one
        /// stretch, it all goes there; moved slowly over two or three stretches in a batch, each gets its share - the
        /// stretches together never get more of the wheel than the time it was held to them. So an honest press, held
        /// still or swept, is never cut by the guard.
        /// </summary>
        [Test]
        public void Wheel_TimeIsSharedOutOverTheStretchItMovesAlong_AndTheGuardGrantsItWhole()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            CraftPiece p = w.Piece(bar);
            int n = p.segments.Count;
            var g = new CraftRequestGuard(1, 0.5);
            double now = 100, total = 0;
            // 30 s of grinding at 60 frames a second, a batch every 0.2 s, swept at speeds from still to brisk (segments a batch)
            double x = 0.3;
            int dir = 1;
            for (int batch = 0; batch < 150; batch++)
            {
                double speed = new[] { 0.0, 0.5, 1.05, 1.5, 3, 8 }[batch % 6] / n; // of the length, per batch
                var stroke = new CraftStroke();
                for (int f = 0; f < 12; f++)
                {
                    x += dir * speed / 12;
                    if (x > 0.95 || x < 0.05) { dir = -dir; x = CraftMath.Clamp(x, 0.05, 0.95); }
                    stroke.Add(now + f / 60.0, x, 0.5, 15, 0.5, null);
                }
                double dwell = stroke.Dwell(p, 0.2, out int from, out int to);
                Assert.LessOrEqual(from, to);
                Assert.AreEqual(0.2, dwell * (to - from + 1), 1e-9, $"batch {batch}: {to - from + 1} stretches share the 0.2 s");
                if (speed == 0) Assert.AreEqual(from, to, "held still: one stretch has it all");
                var r = new CraftRequest(CraftRequestKind.Grind, bar) { i0 = from, i1 = to, a = 15, b = 0.5f, c = (float)dwell };
                Assert.IsTrue(g.Check(ref r, now, out string why, w), why);
                Assert.AreEqual("", why, $"batch {batch}: an honest press is granted whole");
                Assert.AreEqual((float)dwell, r.c, 1e-6);
                total += r.c * (to - from + 1);
                now += 0.2;
            }
            Assert.AreEqual(30, total, 1e-3, "30 s of wheel, all of it worked");
        }

        // ------------------------------------------------------------------ dips and quenches
        /// <summary>
        /// The Grind area's dip cools an edge the wheel heated. A hardened tool-steel edge ground to near the HEAT gauge's
        /// red (some 280 °C, above its martensite start) still holds the few hundredths of austenite its quench left - but
        /// the quench already turned all of it the water's heat reaches: dipping it is a dip, recorded and learnt as one,
        /// and the wheel's dip takes it.
        /// </summary>
        [Test]
        public void ADip_OfAHardenedBladeTheWheelHeated_IsADipNotAQuench()
        {
            CraftWorkshop w = Workshop();
            string bar = HardBar(w);
            CraftPiece p = w.Piece(bar);
            for (int k = 0; k < 400 && p.segments[12].EdgeThickness > 0.3; k++)
            {
                w.Grind(bar, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "coarse_wheel", 15, 0.5, 0.6, out _);
                Cool(p);
            }
            for (int k = 0; k < 40 && p.MaxTemperature < 280; k++)
            {
                Assert.IsTrue(w.Grind(bar, 12, 12, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 0.5, 0.6, out string why), why);
                w.RunSteps((int)Math.Round(0.2 / w.Rules.Settings.stepSeconds));
            }
            MetalSegment g = p.segments[12];
            CraftAlloyProfile prof = p.Profile(w.Rules);
            double ms = CraftMetallurgy.MartensiteStart(CraftMetallurgy.ZoneCarbon(prof, g, g.edge, Math.Max(0.2, (g.EdgeThickness + g.thickness) * 0.5)));
            Assert.Greater(g.edge.aust, 0.01, "the quench left some austenite in the edge");
            Assert.Greater(g.edge.temp, ms, "and the wheel took the edge back above its martensite start");
            Assert.IsFalse(CraftSurface.DipQuenches(w, p), "the wheel's dip may cool it");
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why2, 1), why2);
            Assert.AreEqual("Dipped in Water to cool", p.record.Last().text, $"edge austenite {g.edge.aust:0.000} at {g.edge.temp:0} °C (Ms {ms:0})");
            Assert.AreEqual(0, p.Tag(CraftThermal.QuenchHardeningTag), "a dip: lifted out, nothing is learnt about quenching");
            w.RunSteps(60);
            Assert.Less(p.Tag(CraftThermal.QuenchMartensiteTag), 0.005, "no martensite forms in the dip");
        }

        /// <summary>
        /// From a hardening heat the wheel's 4-second dip would be a quench held still and cut short (out well above a red
        /// heat's end, unhardened, recorded as quenched and learnt as a soft one): the Grind area refuses it - quench it at
        /// the tank, or let it cool. Going into the tank from there is a quench, and so is a quench cut short going back in.
        /// A cold bar never hardened is only dipped.
        /// </summary>
        [Test]
        public void AtAHardeningHeat_TheWheelsDipIsRefused_ARealQuenchIsStillAQuench()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            Assert.IsFalse(CraftSurface.DipQuenches(w, p), "a cold bar never hardened: nothing to harden");
            LightForge(w);
            HeatTo(w, bar, 850);
            Assert.IsTrue(CraftSurface.DipQuenches(w, p), $"at {p.MaxTemperature:0} °C in the air it can still harden: the wheel's dip is refused");
            StringAssert.Contains("Quench area", CraftSurface.DipWouldQuench("at the tank (the Quench area)", true));
            // The quench itself, at the tank: a quench - cut short while the core is still turning, it goes back in as one.
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why), why);
            Assert.AreEqual("Quenched in Water", p.record.Last().text);
            w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
            MetalSegment g = p.segments[12];
            double ms = CraftMetallurgy.MartensiteStart(CraftMetallurgy.ZoneCarbon(p.Profile(w.Rules), g, g.body, g.thickness));
            for (int t = 0; t < 4000 && g.body.temp > ms - 40; t++) w.RunSteps(1);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out why), why);
            Assert.Greater(g.body.aust, 0.1, $"cut short at {g.body.temp:0} °C: austenite still to turn");
            Assert.IsTrue(CraftThermal.Hardening(w.Rules, p, w.Quench.temperature), "back into the tank it hardens on");
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out why), why);
            Assert.AreEqual("Quenched in Water", p.record.Last().text);
            Assert.AreEqual(1, p.Tag(CraftThermal.QuenchHardeningTag));
            // And a cold bar never hardened, dipped, is dipped.
            string cold = Bar(w, "Tool steel");
            Assert.IsTrue(w.MovePiece(cold, CraftLocation.Quench, out why), why);
            Assert.AreEqual("Dipped in Water to cool", w.Piece(cold).record.Last().text);
        }

        // ------------------------------------------------------------------ the strip: the design's edge
        /// <summary>A stretch of the design's own section, both bevels ground in turn at <paramref name="angle"/> a side until they meet.</summary>
        private static MetalSegment GroundToMeet(double tw, double tt, double angle, int edges)
        {
            var s = new MetalSegment { width = tw, thickness = tt, length = 10 };
            double pass = 0.003 * tt * tt; // a little at a time, for a thin section or a thick one
            for (int k = 0; k < 20000 && s.EdgeThickness > CraftSurface.EdgeMet * 0.5; k++)
                CraftSurface.GrindBevel(s, k % 2 == 0, pass, angle, edges);
            Assert.LessOrEqual(s.EdgeThickness, CraftSurface.EdgeMet, "the bevels met");
            return s;
        }

        /// <summary>
        /// Every built-in edge, stretch by stretch: forged to the design's own section and ground at the angle the strip
        /// and the advice ask for there, both faces in turn until the bevels meet, it is at the design - a narrow blade's
        /// section (a rapier's, a javelin's) and the stretch toward a point carry a steeper edge than a broad blade's, and
        /// that is the one asked for; never one the section cannot carry, never steeper than the stone is held.
        /// </summary>
        [Test]
        public void Judge_EveryBuiltInEdge_GroundAtTheAngleAskedFor_IsAtTheDesign()
        {
            CraftRules rules = Rules();
            int stretches = 0;
            foreach (CraftDesign d in rules.Designs)
                foreach (CraftComponentSpec c in d.components)
                {
                    CraftShapeTarget sh = c?.shape;
                    if (c == null || c.kind != CraftPieceKind.Metal || sh == null || sh.edge == CraftEdgeKind.None || sh.profile == null || sh.profile.Count < 2) continue;
                    int edges = sh.edge == CraftEdgeKind.Double ? 2 : 1;
                    for (int i = 0; i < 24; i++)
                    {
                        float t = (i + 0.5f) / 24f;
                        if (t < sh.edgeFrom || t > sh.edgeTo) continue;
                        sh.At(t, out float tw, out float tt);
                        double want = CraftSurface.EdgeHalfAngle(tw, tt, sh.edgeAngle, edges);
                        Assert.LessOrEqual(want, CraftSurface.StoneAngleMax + 1e-6, $"{d.id} at {t:0.00}: {want:0.0}° a side asked for - steeper than the stone is held");
                        Assert.GreaterOrEqual(want, Math.Min(sh.edgeAngle * 0.5, CraftSurface.MaxBevelAngle) - 1e-6, $"{d.id}: never shallower than the design's own angle");
                        MetalSegment s = GroundToMeet(tw, tt, want, edges);
                        CraftSurface.Judgement j = CraftSurface.Judge(s, tw, tt, sh.edgeAngle, true, edges);
                        Assert.AreEqual(CraftSurface.Progress.Done, j.progress,
                            $"{d.id} at {t:0.00}: design {tw:0.0} x {tt:0.0} mm, {sh.edgeAngle:0}°; ground at {want:0.0}° a side the bevels met at {s.AngleL:0.0}°/{s.AngleR:0.0}°, {s.width:0.0} mm wide");
                        Assert.AreEqual(want, j.want, 1e-9, "the angle the strip judged by is the one asked for");
                        stretches++;
                    }
                }
            Assert.Greater(stretches, 300, "every edged part's stretches were tried");
        }

        /// <summary>
        /// Content that asks a narrow section for too acute an edge (the rapier as it was: 17 x 5 mm at mid-blade, 24°) is
        /// judged at the angle its section carries, not at one the bevels can never meet at: ground as asked it is at the
        /// design, and the advice never grinds it away. The evaluator's ideal is the same angle, and the content check says so.
        /// </summary>
        [Test]
        public void Judge_ANarrowSectionAskedForTooAcuteAnEdge_IsJudgedAtTheAngleItCarries()
        {
            double tw = 17.3, tt = 5.2;
            double want = CraftSurface.EdgeHalfAngle(tw, tt, 24, 2);
            Assert.AreEqual(Math.Atan(2.6 / 8.65) * 180 / Math.PI, want, 0.05, "the bevels meet in the middle: each half the width");
            MetalSegment asked = GroundToMeet(tw, tt, want, 2);
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(asked, tw, tt, 24, true, 2).progress);
            // Ground at the design's own 12° a side, the bevels are held to half the width and meet at the section's angle too.
            MetalSegment atTwelve = GroundToMeet(tw, tt, 12, 2);
            Assert.AreEqual(want, atTwelve.AngleL, 1.0);
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(atTwelve, tw, tt, 24, true, 2).progress, "met where the section lets them: at the design");
            // The content check names it.
            CraftContent c = CraftDefaults.Content();
            CraftShapeTarget rapier = c.designs.First(d => d.id == "rapier").MainComponent.shape;
            rapier.edgeAngle = 24;
            CraftShapeTarget stiletto = c.designs.First(d => d.id == "stiletto").MainComponent.shape;
            stiletto.profile[stiletto.profile.Count - 1] = new CraftProfilePoint(1, 3, 3);
            List<string> w = CraftRules.Compile(new CraftSimulationSettings(), c).CompileWarnings;
            string all = string.Join("\n", w);
            Assert.IsTrue(w.Any(x => x.Contains("Design 'rapier', blade: its section is too narrow for its 24° edge")), all);
            Assert.IsTrue(w.Any(x => x.Contains("Design 'stiletto', blade:") && x.Contains("steeper than a whetstone is held")), all);
        }

        /// <summary>The evaluator scores the edge's angle against the one the design's section carries along it, as the strip judges it.</summary>
        [Test]
        public void Evaluator_IdealEdgeAngle_IsTheOneTheSectionCarries()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 1100, 24, 6);
            CraftPiece p = w.Piece(bar);
            Assert.IsTrue(w.SetIntent(bar, "rapier", "blade", out string why), why);
            CraftShapeTarget sh = w.Rules.Design("rapier").MainComponent.shape;
            double ideal = 0, length = 0;
            for (int i = 0; i < p.segments.Count; i++)
            {
                float t = (float)p.PositionOf(i);
                if (t < sh.edgeFrom || t > sh.edgeTo) continue;
                sh.At(t, out float tw, out float tt);
                ideal += 2 * CraftSurface.EdgeHalfAngle(tw, tt, sh.edgeAngle, 2) * p.segments[i].length;
                length += p.segments[i].length;
            }
            CraftPieceReport r = CraftEvaluator.Piece(w.Rules, p);
            Assert.AreEqual(ideal / length, r.IdealEdgeAngle, 1e-6);
            Assert.Greater(r.IdealEdgeAngle, sh.edgeAngle, "toward the point the section carries a steeper edge than the design's");
        }

        /// <summary>
        /// An edge whose bevels met centred at the design's angle but which is 20 % wider than the design is not at the
        /// design (the evaluator's shape score counts the width either way): still to grind - and grinding both bevels on,
        /// in turn, takes the edge back to it. Thicker than the design, the same.
        /// </summary>
        [Test]
        public void Judge_AnEdgeWiderOrThickerThanTheDesign_IsStillToGrind_AndTheBevelsGroundOnBringItThere()
        {
            var s = new MetalSegment { width = 36, thickness = 5, length = 8, bevelDepthL = 2.5, bevelDepthR = 2.5, bevelWidthL = 2.5 / Tan(15), bevelWidthR = 2.5 / Tan(15) };
            CraftSurface.Judgement j = CraftSurface.Judge(s, 30, 5, 30, true);
            Assert.AreEqual(CraftSurface.Progress.ToGrind, j.progress, $"widthOff {j.widthOff:+0.00}: still to grind");
            Assert.Less(j.done, 1, "and the strip says how far it is");
            int k = 0;
            for (; k < 20000 && CraftSurface.Judge(s, 30, 5, 30, true).progress == CraftSurface.Progress.ToGrind; k++)
                CraftSurface.GrindBevel(s, k % 2 == 0, 0.05, 15);
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(s, 30, 5, 30, true).progress, $"after {k} passes: {s.width:0.0} mm, {s.AngleL:0.0}°/{s.AngleR:0.0}°");
            Assert.AreEqual(30, s.width, 30 * 0.041, "the edge went back to the design's width");
            var thick = new MetalSegment { width = 30, thickness = 6, length = 8, bevelDepthL = 3, bevelDepthR = 3, bevelWidthL = 3 / Tan(15), bevelWidthR = 3 / Tan(15) };
            Assert.AreEqual(CraftSurface.Progress.ToGrind, CraftSurface.Judge(thick, 30, 5, 30, true).progress, "20 % thicker: still to grind");
        }

        /// <summary>
        /// A design asking for an edge steeper than bevels are ground (80°: 40° a side, past the wheel's 35°) is named by the
        /// content check, and the strip asks for no more than the wheel holds - met there, it is at the design.
        /// </summary>
        [Test]
        public void Judge_ADesignAngleSteeperThanTheWheelGrinds_AsksForNoMore_AndTheCheckSaysSo()
        {
            Assert.AreEqual(CraftSurface.MaxBevelAngle, CraftSurface.EdgeHalfAngle(60, 8, 80, 1), 1e-9, "a broad section: the steepest the wheel grinds");
            MetalSegment s = GroundToMeet(60, 8, CraftSurface.MaxBevelAngle, 1);
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(s, 60, 8, 80, true).progress);
            CraftContent c = CraftDefaults.Content();
            c.designs.First(d => d.id == "dagger").MainComponent.shape.edgeAngle = 80;
            List<string> w = CraftRules.Compile(new CraftSimulationSettings(), c).CompileWarnings;
            Assert.IsTrue(w.Any(x => x.Contains("Design 'dagger', blade: its edge angle (80°) is steeper than bevels are ground")), string.Join("\n", w));
        }

        // ------------------------------------------------------------------ the grits and the words
        private static void Surface(CraftPiece p, double rough, double scale = 0)
        {
            foreach (MetalSegment s in p.segments) { s.rough = rough; s.scale = scale; s.polish = CraftSurface.PolishOf(rough); }
        }

        /// <summary>
        /// The polish window calls a grit done ("has done what it can - go on to ...") once its scratches are where the next
        /// finer grit takes them at its pace (<see cref="CraftSurface.GritDone"/>): worked grit by grit as the window
        /// says, no grit ever reads as skipped, and each one gets there.
        /// </summary>
        [Test]
        public void Polish_WhatTheWindowCallsDone_IsWithinTheNextGritsReach()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            Surface(p, 6); // off the fine wheel
            var notes = new List<string>();
            w.EventRaised += e => { if (e.Kind == CraftEventKind.Note) notes.Add(e.Id); };
            string[] grits = { "coarse_emery", "fine_emery", "rouge" };
            for (int k = 0; k < grits.Length; k++)
            {
                CraftAbrasive grit = w.Rules.Abrasive(grits[k]), next = k + 1 < grits.Length ? w.Rules.Abrasive(grits[k + 1]) : null;
                double done = CraftSurface.GritDone(grit, next);
                if (next != null)
                    Assert.LessOrEqual(done, next.finish * CraftSurface.GritReach, $"{grit.Name} is done where {next.Name} reaches");
                int passes = 0;
                for (; passes < 400 && p.segments[0].rough > done; passes++)
                    Assert.IsTrue(w.Polish(bar, 0, 23, grit.id, 0.5, 0.5, out string why), why);
                Assert.LessOrEqual(p.segments[0].rough, done, $"{grit.Name} gets there ({passes} passes of 0.5 s)");
                CollectionAssert.DoesNotContain(notes, "skipped_grit", $"worked grit by grit to {grit.Name}: nothing skipped");
            }
            Assert.AreEqual(CraftSurface.Finish.Mirror, CraftSurface.FinishOf(p.segments[0].rough), "the rouge leaves a mirror");
            // Straight from the fine wheel to the rouge is a grit skipped, as it should be.
            Surface(p, 6);
            Assert.IsTrue(w.Polish(bar, 0, 23, "rouge", 0.5, 0.5, out string skipped), skipped);
            CollectionAssert.Contains(notes, "skipped_grit");
        }

        /// <summary>
        /// What a piece looks like says what the Grind area says of the same surface (one ladder, CraftSurface.FinishOf):
        /// the coarse wheel's coarse scratches, the fine wheel's fine ones, the coarse emery's smooth grey finish, the fine
        /// emery's bright, and a mirror only off the rouge (or a jeweller's buff).
        /// </summary>
        [Test]
        public void Look_SaysWhatTheGrindAreaSaysOfTheSurface()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            foreach (var (rough, words) in new[] { (25.0, "coarse scratches"), (6.0, "fine scratches"), (3.0, "smooth grey finish"), (1.0, "polished bright"), (0.2, "mirror") })
            {
                Surface(p, rough);
                StringAssert.Contains(words, CraftSurface.FinishWords(rough) == "bright" ? "polished bright" : CraftSurface.FinishWords(rough), "the window's words");
                string look = string.Join(" | ", CraftLook.Describe(w.Rules, p, new CraftJournalData(), CraftInstruments.None, false, false));
                StringAssert.Contains(words, look.ToLowerInvariant(), $"{rough} µm");
                Assert.AreEqual(rough < 0.5, look.Contains("mirror"), $"{rough} µm: a mirror only off the rouge: {look}");
            }
            // A ring buffed bright on the jeweller's wheel shines as its polish says, whatever scratches it was cast with.
            foreach (MetalSegment s in p.segments) { s.rough = 4; s.polish = 1; }
            StringAssert.Contains("mirror", string.Join(" | ", CraftLook.Describe(w.Rules, p, new CraftJournalData(), CraftInstruments.None, false, false)));
        }

        /// <summary>
        /// The burr the Sharpen area draws and names (from <see cref="CraftSurface.BurrShows"/>) is the burr the look sees;
        /// and the stone's words for how keen the edge is are the look's (one ladder, CraftSurface.KeenStep).
        /// </summary>
        [Test]
        public void Look_SeesTheBurrAndTheKeennessTheStoneShows()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            p.edges = 1;
            foreach (MetalSegment s in p.segments)
            {
                s.bevelDepthL = s.bevelDepthR = 2.5; s.bevelWidthL = s.bevelWidthR = 2.5 / Tan(15);
                s.honeL = s.honeR = 1; s.apex = 8; s.scale = 0; s.rough = 3; s.polish = CraftSurface.PolishOf(3);
            }
            foreach (double burr in new[] { 0.15, 0.25 })
            {
                foreach (MetalSegment s in p.segments) s.burr = burr;
                bool seen = CraftLook.Describe(w.Rules, p, new CraftJournalData(), CraftInstruments.None, false, false).Any(l => l.Contains("burr"));
                Assert.AreEqual(burr > CraftSurface.BurrShows, seen, $"burr {burr}: the window {(burr > CraftSurface.BurrShows ? "draws" : "does not draw")} it");
            }
            foreach (MetalSegment s in p.segments) s.burr = 0;
            string[] look = { "blunt", "sharp enough to cut, not keen", "is keen", "razor-keen" };
            string[] stone = { "blunt", "sharp, not keen", "keen", "razor-keen" };
            foreach (double apex in new[] { 150.0, 30, 8, 1.5 })
            {
                foreach (MetalSegment s in p.segments) s.apex = apex;
                double sharp = CraftEvaluator.Sharpness(p.segments[12]);
                int step = CraftSurface.KeenStep(sharp);
                Assert.AreEqual(stone[step], CraftSurface.KeenWords(sharp));
                string seen = string.Join(" | ", CraftLook.Describe(w.Rules, p, new CraftJournalData(), CraftInstruments.None, false, false));
                StringAssert.Contains(look[step], seen, $"apex {apex} µm (sharpness {sharp:0.00}): the stone says '{CraftSurface.KeenWords(sharp)}'");
            }
        }

        /// <summary>
        /// Each grit the window asks for looks brighter and keeps rust off better than the one before it: the polish follows
        /// the scratches on the eye's own (log) scale, so the coarse emery's 3 µm is a smooth grey, not yet a mirror, and
        /// the rouge's 0.2 µm is.
        /// </summary>
        [Test]
        public void Polish_EachGritLooksBrighter_AndKeepsRustOffBetter()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            double lastPolish = 0, lastCorrosion = 0;
            foreach (string id in new[] { "coarse_wheel", "fine_wheel", "coarse_emery", "fine_emery", "rouge" })
            {
                double finish = w.Rules.Abrasive(id).finish;
                Surface(p, finish);
                double polish = CraftSurface.PolishOf(finish), corrosion = CraftEvaluator.Piece(w.Rules, p).Corrosion;
                if (id != "coarse_wheel")
                {
                    Assert.Greater(polish, lastPolish + 0.08, $"{id}: brighter than the grit before it");
                    Assert.Greater(corrosion, lastCorrosion + 0.02, $"{id}: keeps rust off better");
                }
                lastPolish = polish;
                lastCorrosion = corrosion;
            }
            Assert.AreEqual(CraftSurface.GroundPolish, CraftSurface.PolishOf(25), 1e-9, "the coarse wheel's grey");
            Assert.Less(CraftSurface.PolishOf(3), 0.6, "the coarse emery's smooth grey is no mirror");
            Assert.AreEqual(1, CraftSurface.PolishOf(0.2), 1e-9, "the rouge's mirror");
        }

        // ------------------------------------------------------------------ the stone
        /// <summary>A bar with both bevels ground at 15° to an edge (the faces not yet honed).</summary>
        private static (CraftWorkshop w, string bar, CraftPiece p) EdgedBar(double apex = 6)
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            CraftPiece p = w.Piece(bar);
            p.edges = 1;
            foreach (MetalSegment s in p.segments)
            {
                s.bevelDepthL = s.bevelDepthR = 2.5; s.bevelWidthL = s.bevelWidthR = 2.5 / Tan(15);
                s.apex = apex;
            }
            return (w, bar, p);
        }

        /// <summary>
        /// A face's bar fills at 1 and the window shows both faces at 100 %: strokes past a full bar on one face (taking the
        /// burr off with that face in hand) hone no further, and cost no symmetry the window cannot show - nor does a face
        /// honed past full in an older save.
        /// </summary>
        [Test]
        public void Stone_StrokesPastAFullBar_HoneNoFurther_AndCostNoHiddenSymmetry()
        {
            var (w, bar, p) = EdgedBar(3);
            foreach (MetalSegment s in p.segments) { s.honeL = s.honeR = 1; s.microL = s.microR = 15; s.microAngle = 30; }
            for (int k = 0; k < 60; k++)
                Assert.IsTrue(w.Sharpen(bar, 0, 23, CraftSurfaceSide.RightBevel, "fine_stone", 16, 0.15, 1, 0.3, false, out string why), why);
            Assert.AreEqual(1, p.segments[12].honeR, 1e-9, "a full bar is full");
            CraftPieceReport r = CraftEvaluator.Piece(w.Rules, p);
            foreach (MetalSegment s in p.segments) s.honeL = 1.5; // as an older save may have it
            CraftPieceReport old = CraftEvaluator.Piece(w.Rules, p);
            Assert.AreEqual(r.Symmetry, old.Symmetry, 1e-9, "both bars full: the same symmetry");
        }

        /// <summary>
        /// A strop is drawn spine first, edge trailing: edge leading, the edge slices into the leather - it hones nothing,
        /// takes no burr off, dulls a little and is said to; it never chips, however hard it is pressed.
        /// </summary>
        [Test]
        public void Strop_EdgeLeadingStrokes_CutTheLeather_TrailingOnesHone()
        {
            double Apex(bool leading, double pressure, out double chip, out bool said)
            {
                var (w, bar, p) = EdgedBar(6);
                foreach (MetalSegment s in p.segments) { s.honeL = s.honeR = 1; s.burr = 0.3; }
                bool told = false;
                w.EventRaised += e => told |= e.Kind == CraftEventKind.Note && e.Id == "strop_lead" && !string.IsNullOrEmpty(e.Text);
                for (int k = 0; k < 40; k++)
                    Assert.IsTrue(w.Sharpen(bar, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "strop", 16, pressure, 1, 0.3, leading, out string why), why);
                chip = p.segments[12].chip;
                said = told;
                Assert.AreEqual(leading ? 0.3 : 0, p.segments[12].burr, leading ? 1e-9 : 0.05, leading ? "edge leading takes no burr off" : "drawn spine first, the strop takes the burr off");
                return p.segments[12].apex;
            }
            double trail = Apex(false, 0.4, out _, out bool saidTrail), lead = Apex(true, 0.4, out _, out bool saidLead);
            Assert.Less(trail, 6, "spine first, the strop hones");
            Assert.Greater(lead, 6, $"edge leading it dulls (leading {lead:0.00} µm, trailing {trail:0.00} µm)");
            Assert.IsTrue(saidLead, "and the strop says why");
            Assert.IsFalse(saidTrail);
            Apex(true, 0.95, out double chip, out _);
            Assert.AreEqual(0, chip, 1e-9, "the leather gives: no chips");
        }

        /// <summary>
        /// The stone hones the edge at the angle it is held, never finer than the bevel: held well above the bevel the honed
        /// edge is steeper than the design's (the window warns of it), and the item's edge suffers for it.
        /// </summary>
        [Test]
        public void Stone_HeldWellAboveTheBevel_HonesASteeperEdge()
        {
            double Honed(double held)
            {
                var (w, bar, p) = EdgedBar(150);
                foreach (string stone in new[] { "coarse_stone", "medium_stone" })
                    for (int k = 0; k < 120; k++)
                        Assert.IsTrue(w.Sharpen(bar, 0, 23, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, stone, held, 0.45, 1, 0.3, k % 4 < 2, out string why), why);
                return p.segments[12].microAngle;
            }
            double atBevel = Honed(15), steep = Honed(25);
            Assert.AreEqual(30, atBevel, 2, "held at the bevel: the bevel's own edge");
            Assert.Greater(steep, atBevel + 15, $"held at 25°: honed at {steep:0}°");
        }

        // ------------------------------------------------------------------ requests the window never sends
        /// <summary>
        /// Descaling is the brush's and the scraper's work (the wheel grinds scale off with its heat, at the wheel): a wheel,
        /// a file or a stone named for it is refused. A coat goes on a piece in the hands or fitted - not one in the tank
        /// or the oven.
        /// </summary>
        [Test]
        public void Descale_TakesOnlyABrush_AndCoat_OnlyAPieceInTheHands()
        {
            CraftWorkshop w = new CraftWorkshop(Rules(), CraftTools.Files | CraftTools.QuenchTank | CraftTools.Oven, 18, 1, 3, "water", "surfacerev");
            string bar = Bar(w, "Mild steel");
            foreach (MetalSegment s in w.Piece(bar).segments) s.scale = 0.05;
            foreach (string id in new[] { "coarse_wheel", "bastard_file", "fine_stone", "rouge" })
                Assert.IsFalse(w.Descale(bar, 0, 23, id, 0.5, 1, out string why), $"{id}: descaled");
            Assert.IsTrue(w.Descale(bar, 0, 23, "wire_brush", 0.5, 1, out string ok), ok);
            Assert.IsTrue(w.Descale(bar, 0, 23, "scraper", 0.5, 1, out ok), ok);
            string oil = w.Rules.Supplies.First(x => x.kind == CraftSupplyKind.Oil).id;
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string moved), moved);
            Assert.IsFalse(w.Coat(bar, oil, out string inTank), "oiled in the tank");
            StringAssert.Contains("Take it out", inTank);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out moved), moved);
            Assert.IsTrue(w.Coat(bar, oil, out string coated), coated);
        }
    }
}
#endif
