#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the rules the 3D work places share with the window (wave 2, the hands-on behaviour): the hand's
    /// path judged by time and distance, never by frames (the quench's motion, how fast the brush scrubs); brushing ore
    /// by going-overs that add up as one sweep over the same surface, each brush taking only its share however long it
    /// scrubs, and the 3D stone counting what a brush went over as the workshop does; the pour ending only when it is
    /// tipped back (or full and upright) and what it sends; the whetstone's passes taken one at a time at any frame rate;
    /// the bore going in from the nearer end; and the two waiters on one station numbering apart.
    /// </summary>
    public class CraftHandsOnRulesTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "handson");

        // ------------------------------------------------------------------ the hand's path
        /// <summary>A hand moving for <paramref name="seconds"/> at <paramref name="fps"/> frames a second, as <paramref name="at"/> says, judged at the end.</summary>
        private static CraftHandTrail Follow(double fps, double seconds, Func<double, (double x, double y)> at, double keep = 0.6, double step = 1080 * 0.005)
        {
            var trail = new CraftHandTrail(keep, step);
            for (int f = 0; f <= (int)(fps * seconds); f++)
            {
                double t = f / fps;
                (double x, double y) = at(t);
                trail.Add(t, x, y);
            }
            return trail;
        }

        [Test]
        public void HandTrail_TheQuenchMotion_IsTheSameAtEveryFrameRate()
        {
            const double H = 1080, least = 0.05 * H; // the window's 20 px of its 400 px surface, over the last 0.6 s
            foreach (double fps in new[] { 30.0, 60.0, 144.0 })
            {
                // A slow, real up-and-down (0.15 of the screen a second): moving, at every rate (it read still at 144 Hz before).
                CraftHandTrail slowUpDown = Follow(fps, 2, t => (900, 540 + 0.0375 * H * Math.Sin(t * 2 * Math.PI)));
                Assert.AreEqual(CraftQuenchMotion.UpDown, slowUpDown.Motion(least), $"slow up and down at {fps} fps");
                CraftHandTrail sideways = Follow(fps, 2, t => (900 + 80 * Math.Sin(t * 2 * Math.PI * 1.5), 540));
                Assert.AreEqual(CraftQuenchMotion.SideToSide, sideways.Motion(least), $"side to side at {fps} fps");
                CraftHandTrail round = Follow(fps, 2, t => (900 + 60 * Math.Cos(t * 2 * Math.PI), 540 + 60 * Math.Sin(t * 2 * Math.PI)));
                Assert.AreEqual(CraftQuenchMotion.Swirl, round.Motion(least), $"round at {fps} fps");
                // Too slow to break the vapour jacket (0.05 of the screen a second): held still, at every rate.
                CraftHandTrail creeping = Follow(fps, 2, t => (900, 540 + 0.0125 * H * Math.Sin(t * 2 * Math.PI)));
                Assert.AreEqual(CraftQuenchMotion.Still, creeping.Motion(least), $"a creeping hand at {fps} fps");
                // Held still after moving: the moves age out of the last 0.6 s.
                CraftHandTrail stopped = Follow(fps, 3, t => t < 1 ? (900, 540 + 80 * Math.Sin(t * 2 * Math.PI * 1.5)) : (900, 540));
                Assert.AreEqual(CraftQuenchMotion.Still, stopped.Motion(least), $"stopped for two seconds at {fps} fps");
            }
        }

        [Test]
        public void HandTrail_Speed_DoesNotDependOnTheFrameRate()
        {
            var speeds = new List<double>();
            foreach (double fps in new[] { 30.0, 60.0, 144.0 })
            {
                // Scrubbing back and forth across 200 px at 1 Hz: 400 px a second.
                CraftHandTrail t = Follow(fps, 2, s => (500 + 200 * Math.Abs(((s * 2) % 2) - 1), 300), 0.5, 1);
                speeds.Add(t.Speed);
            }
            foreach (double s in speeds)
                Assert.AreEqual(400, s, 40, "the hand's speed is the hand's, not the screen's");
            Assert.AreEqual(speeds[0], speeds[2], 30, "30 and 144 frames a second give the same speed");
        }

        // ------------------------------------------------------------------ brushing ore
        private static OreFragment FirstStone(CraftWorkshop w, string id) => w.Piece(id).ore.fragments[0];
        private static double Dirt(OreFragment f) => f.mass * f.dirt;
        private static double Rock(OreFragment f) => f.mass * f.gangue;
        private static double Mineral(OreFragment f) => f.mass * f.mineral;

        [Test]
        public void Brushing_InManyStrokes_DoesWhatOneSweepOverTheSameSurfaceDoes()
        {
            CraftWorkshop w = Workshop();
            foreach (CraftOreBrush brush in new[] { CraftOreBrush.Soft, CraftOreBrush.Stiff, CraftOreBrush.Wire })
            {
                string once = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0)), sliced = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
                OreFragment o0 = FirstStone(w, once).Clone(), s0 = FirstStone(w, sliced).Clone();
                Assert.IsTrue(w.BrushOre(once, 0, brush, 1, out string why), why);
                for (int k = 0; k < 20; k++)
                    Assert.IsTrue(w.BrushOre(sliced, 0, brush, 0.05, out why), why);
                OreFragment o = FirstStone(w, once), s = FirstStone(w, sliced);
                Assert.AreEqual(Dirt(o) / Dirt(o0), Dirt(s) / Dirt(s0), 1e-6, $"{brush}: twenty slices of the stone clean what one sweep over all of it does");
                Assert.AreEqual(Rock(o) / Rock(o0), Rock(s) / Rock(s0), 1e-6, $"{brush}: the rind too");
                Assert.AreEqual(Mineral(o) / Mineral(o0), Mineral(s) / Mineral(s0), 1e-6, $"{brush}: and the ore");
                Assert.AreEqual(1, s.Brushed(brush), 1e-9, "the stone keeps how far each brush has gone over it");
            }
        }

        [Test]
        public void Brushing_ABrushTakesOnlyItsShare_HoweverLongItScrubs()
        {
            CraftWorkshop w = Workshop();
            string clay = w.AddPiece(CraftOres.NewLump(w.Rules, "bog_iron", 9, 0));
            OreFragment c0 = FirstStone(w, clay).Clone();
            Assert.IsTrue(w.BrushOre(clay, 0, CraftOreBrush.Soft, 0.6, out string why), why);
            Assert.IsTrue(w.BrushOre(clay, 0, CraftOreBrush.Soft, 0.6, out why), why); // the last 0.4 of the stone
            Assert.IsFalse(w.BrushOre(clay, 0, CraftOreBrush.Soft, 1, out why), "the soft brush has been all over it");
            StringAssert.Contains("takes no more", why);
            StringAssert.Contains("stiff brush", why, "and the reason says what would");
            double soft = Dirt(FirstStone(w, clay)) / Dirt(c0);
            Assert.AreEqual(1 - CraftOres.BrushOnDirt(CraftOreBrush.Soft, w.Rules.Ore("bog_iron")), soft, 1e-6, "however long, the soft brush shifts only its share of the caked clay");
            Assert.IsTrue(w.BrushOre(clay, 0, CraftOreBrush.Stiff, 1, out why), why);
            Assert.Less(Dirt(FirstStone(w, clay)) / Dirt(c0), soft * 0.2, "the stiff brush takes most of what was left");
        }

        [Test]
        public void WireBrush_KnocksTheRindOffOnce_NotTheWholeRock()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            for (int f = w.Piece(id).ore.fragments.Count - 1; f >= 0; f--) w.CrushOre(id, f, 1.2, out _);
            OreFragment small = FirstStone(w, id), before = small.Clone();
            for (int k = 0; k < 5; k++)
                w.BrushOre(id, 0, CraftOreBrush.Wire, 1, out _);
            OreFragment after = FirstStone(w, id);
            double rind = before.RindShare * CraftOres.BrushOnRind(CraftOreBrush.Wire); // the rind the stone carries: its lump's, shared with its rock
            Assert.AreEqual(1 - rind, Rock(after) / Rock(before), 1e-6, "five going-overs knock off the weathered rind once: the rock under it stays");
            Assert.Greater(Mineral(after) / Mineral(before), 0.93, "and scratch the ore once, not five times");
        }

        [Test]
        public void ABreak_StartsUnbrushed()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Stiff, 1, out string why), why);
            Assert.AreEqual(1, FirstStone(w, id).brushedStiff, 1e-9);
            int before = w.Piece(id).ore.fragments.Count;
            Assert.IsTrue(w.CrushOre(id, 0, 1.5, out why), why);
            Assert.Greater(w.Piece(id).ore.fragments.Count, before, "it split");
            Assert.AreEqual(0, FirstStone(w, id).brushedStiff, 1e-9, "the fresh faces of a break have not been brushed");
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Stiff, 1, out why), why);
        }

        [Test]
        public void BrushCover_CountsWhatTheBrushCanTake_SoTheStoneAndTheWorkshopAgree()
        {
            CraftOreDefinition hematite = Rules().Ore("hematite");
            double[] takesDirt = { CraftOres.BrushOnDirt(CraftOreBrush.Soft, hematite), CraftOres.BrushOnDirt(CraftOreBrush.Stiff, hematite), CraftOres.BrushOnDirt(CraftOreBrush.Wire, hematite) };
            double[] takesRind = { CraftOres.BrushOnRind(CraftOreBrush.Soft), CraftOres.BrushOnRind(CraftOreBrush.Stiff), CraftOres.BrushOnRind(CraftOreBrush.Wire) };
            // Ten patches of the same size; the dirt is all on the first three, no rind.
            var area = new double[10];
            var dirt = new double[10];
            var rind = new double[10];
            for (int i = 0; i < 10; i++) { area[i] = 1; dirt[i] = i < 3 ? 0.8 : 0; }
            var cover = new CraftBrushCover();
            cover.Reset(area, dirt, rind, takesDirt, takesRind, new double[3]);
            double before = cover.MeanDirt;
            for (int i = 0; i < 3; i++) cover.Scrub(i, CraftOreBrush.Soft, 1);
            Assert.AreEqual(0, cover.Scrub(0, CraftOreBrush.Soft, 1), 1e-12, "a brush goes over a patch once");
            double sent = cover.GoingOver(CraftOreBrush.Soft);
            Assert.AreEqual(1, sent, 1e-9, "scrubbing just the dirty patches went over all the dirt (not 30 % of the stone)");
            // The workshop, sent that, takes what the stone shows was taken.
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            OreFragment f0 = FirstStone(w, id).Clone();
            Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Soft, sent, out string why), why);
            Assert.AreEqual(cover.MeanDirt / before, Dirt(FirstStone(w, id)) / Dirt(f0), 1e-6, "the stone shows what the workshop did");

            // Built again from a stone the soft brush already went half over: it shows the dirt as it is, the brush goes on
            // from there, and what it can still take is what the workshop still takes.
            var again = new CraftBrushCover();
            again.Reset(area, dirt, rind, takesDirt, takesRind, new[] { 0.5, 0, 0 });
            Assert.AreEqual(0.5, again.Covered(CraftOreBrush.Soft), 1e-9, "the brush's work so far, spread evenly");
            Assert.AreEqual(0.8, again.Dirt(0), 1e-9, "the dirt shown is what the stone has now");
            double shown = again.MeanDirt;
            for (int i = 0; i < 10; i++) again.Scrub(i, CraftOreBrush.Soft, 1);
            Assert.AreEqual(0.5, again.GoingOver(CraftOreBrush.Soft), 1e-9, "the other half of it");
            double k = takesDirt[0];
            Assert.AreEqual((1 - k) / (1 - k * 0.5), again.MeanDirt / shown, 1e-9, "the rest of its share, as the workshop's rule takes it");
        }

        // ------------------------------------------------------------------ the pour
        [Test]
        public void Pour_EndsWhenTippedBack_OrFullAndUpright_AndALateTipRunsOver()
        {
            CraftPourControl Run(double lateBy, out double fullAt)
            {
                var p = new CraftPourControl();
                const double dt = 1 / 60.0;
                double t = 0, full = -1;
                bool back = false;
                while (t < 20)
                {
                    if (p.Full && full < 0) full = t;
                    back = full >= 0 && t >= full + lateBy;
                    p.Tick(dt, back ? 0 : 0.85, null, 4);
                    if (!back && p.Full) Assert.IsFalse(p.Settled(false), "held tipped over a full mould it is not done: it runs over");
                    if (p.Settled(back)) break;
                    t += dt;
                }
                fullAt = full;
                return p;
            }
            CraftPourControl prompt = Run(0, out _), late = Run(0.4, out _);
            Assert.IsTrue(prompt.Full && late.Full);
            Assert.Less(prompt.OverflowShare, CraftPourControl.Flash, "tipped back as it filled: the pouring cup takes what still runs");
            Assert.Greater(late.OverflowShare, CraftPourControl.Flash, "tipped back late: it runs over the top (flash)");

            Assert.IsTrue(late.Request("ingot", false, out CraftRequest pour));
            Assert.AreEqual(CraftRequestKind.Pour, pour.kind);
            Assert.AreEqual(1000, pour.i1, "the mould full");
            Assert.AreEqual((int)Math.Round(late.Lost * 1000), pour.i2, "the metal lost on the floor, in thousandths of the mould");
            Assert.AreEqual((float)late.OverflowShare, pour.d, 1e-6);
            Assert.AreEqual((float)late.MeanRate, pour.a, 1e-6, "the stream's own mean rate");
            Assert.IsTrue(late.Request("ingot", true, out CraftRequest fromFurnace));
            Assert.AreEqual(CraftRequestKind.TakeMelt, fromFurnace.kind);
            Assert.AreEqual(late.Interruptions, fromFurnace.i0, "the furnace's stream breaks are sent too");
            Assert.IsFalse(new CraftPourControl().Request("ingot", false, out _), "a pour that never ran sends nothing");

            // Only what the crucible holds: the stream stops when it is empty, and the pour is over.
            var little = new CraftPourControl { Supply = 0.5 };
            for (int f = 0; f < 600 && !little.Settled(false); f++) little.Tick(1 / 60.0, 0.85, null, 4);
            Assert.IsTrue(little.Empty && little.Settled(false));
            Assert.AreEqual(0.5, little.Poured, 0.01);
            Assert.IsTrue(little.Request("ingot", false, out CraftRequest part));
            Assert.AreEqual(500, part.i1, 2, "half a mould");

            Assert.AreEqual(2, CraftPourControl.FillSeconds(100), 1e-9, "a row of rivets fills in two seconds");
            Assert.AreEqual(4, CraftPourControl.FillSeconds(1000), 1e-9);
            Assert.AreEqual(8, CraftPourControl.FillSeconds(9000), 1e-9, "a big mould in eight at the most");
        }

        // ------------------------------------------------------------------ the whetstone
        [Test]
        public void Whetstone_PassesAreTakenOneByOne_TheSameAtEveryFrameRate_AndNoneForAWiggle()
        {
            int Passes(double fps, double amplitude, double hz, double seconds)
            {
                var stroke = new CraftStroke();
                var taken = new List<CraftPass>();
                int sent = 0, count = 0;
                for (int f = 0; f <= (int)(fps * seconds); f++)
                {
                    double t = f / fps, x = 0.5 + amplitude * Math.Sin(t * 2 * Math.PI * hz);
                    stroke.Add(t, x, x, 15, 0.45, null);
                    stroke.TakePasses(ref sent, false, taken);
                    count += taken.Count;
                }
                stroke.TakePasses(ref sent, true, taken); // let go: the run under way too
                return count + taken.Count;
            }
            int at30 = Passes(30, 0.3, 1, 3), at60 = Passes(60, 0.3, 1, 3), at144 = Passes(144, 0.3, 1, 3);
            Assert.GreaterOrEqual(at30, 5, "real strokes across the stone, two a second");
            Assert.AreEqual(at30, at60);
            Assert.AreEqual(at30, at144, "the same strokes at 144 frames a second (one in seven counted before)");
            Assert.AreEqual(0, Passes(60, 0.02, 10, 1), "a tiny fast wiggle is no stroke");
            Assert.AreEqual(0, Passes(144, 0.03, 8, 1));
        }

        // ------------------------------------------------------------------ the bore
        [Test]
        public void Bore_GoesInFromTheNearerEnd_AStretchDeeperEachTurn()
        {
            CraftPiece p = CraftDefaults.WoodBlank(Rules(), "ash", 400, 40, 30);
            int n = p.segments.Count;
            Assert.IsTrue(CraftWoodwork.BoreSpan(p, 0, out int a, out int b));
            Assert.AreEqual((0, 0), (a, b), "the first turn bores the end stretch");
            p.segments[0].hole = 2;
            CraftWoodwork.BoreSpan(p, 0, out a, out b);
            Assert.AreEqual((0, 1), (a, b), "then a stretch deeper");
            CraftWoodwork.BoreSpan(p, 1, out a, out b);
            Assert.AreEqual((n - 1, n - 1), (a, b), "from the far end it starts at the far end");
            for (int i = 0; i < n; i++) p.segments[i].hole = 2;
            CraftWoodwork.BoreSpan(p, 0, out a, out b);
            Assert.AreEqual(0, a);
            Assert.AreEqual(Math.Max(1, n * 3 / 4) - 1, b, "never deeper than three quarters of it");
            Assert.IsFalse(CraftWoodwork.BoreSpan(new CraftPiece(), 0, out _, out _));
        }

        // ------------------------------------------------------------------ two waiters on one station
        [Test]
        public void AnswerWaits_TheHandsAndTheWindow_NumberApart()
        {
            var window = new CraftAnswerWaits();
            var hands = new CraftAnswerWaits(1 << 30);
            int w = window.Expect(null, 0), h = hands.Expect(null, 0);
            Assert.AreEqual(1, w, "the window numbers from 1, as before");
            Assert.AreEqual((1 << 30) + 1, h, "the hands from a range of their own");
            Assert.IsFalse(window.Waiting(h));
            Assert.IsFalse(hands.Waiting(w), "neither takes the other's answer");
        }
    }
}
#endif
