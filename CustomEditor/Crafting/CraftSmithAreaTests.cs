#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the forge, the anvil, the quench and the oven (wave 1, the smith's area): a forge weld set blow
    /// by blow at welding heat, flux on the stretches it covers, the fire's depth and its heart, one face hotter in the
    /// fire, the hammer's tilt and the horn, the hardy and the hot chisel, fasteners, clay on the spine, the ash box, the
    /// quench tank refilled, the forging window, double-edged bevels.
    /// </summary>
    public class CraftSmithAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, string medium = "water") =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, medium, "smith");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 6) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static void LightForge(CraftWorkshop w, string fuel = "charcoal", int amount = 12)
        {
            Assert.IsTrue(w.AddFuel(fuel, amount, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
        }

        /// <summary>In the fire at <paramref name="depth"/>, the bellows pumped steadily, until its mean heat passes <paramref name="target"/>; then out.</summary>
        private static void Heat(CraftWorkshop w, string bar, double target, double depth = 1, string fuel = "charcoal", bool takeOut = true)
        {
            if (w.Piece(bar).location != CraftLocation.Fire)
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string why, depth), why);
            for (int t = 0; t < 8000 && w.Piece(bar).MeanTemperature < target; t++)
            {
                if (t % 6 == 0) w.Bellows(0.75);
                if (t % 600 == 599) w.AddFuel(fuel, 2, out _);
                w.RunSteps(1);
            }
            Assert.Greater(w.Piece(bar).MeanTemperature, target, "it never got hot enough");
            if (takeOut)
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out string why2), why2);
        }

        private static double Delamination(CraftPiece p) => p.defects.Where(d => d.kind == CraftDefectKind.Delamination).Sum(d => d.severity);

        // ------------------------------------------------------------------ the forge weld, set blow by blow
        /// <summary>
        /// The honest hand: flux in the fire at a red heat, up to welding heat deep in the coals, out, folded over, then
        /// setting blows along the seam a second apart; back into the fire, fluxed again, for another heat while the seam is
        /// open. Returns the heats it took (6 = never set).
        /// </summary>
        private static int HandWeld(string metal, string fuel, out CraftPiece bar, bool reflux = true)
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, metal);
            LightForge(w, fuel);
            CraftAlloyProfile prof = w.Piece(id).Profile(w.Rules);
            double target = Math.Min(prof.BurnTemperature - 40, CraftForging.WeldingHeat(w.Rules, prof) + 20);
            Heat(w, id, 900, 1, fuel, false);
            Assert.IsTrue(w.FluxPiece("borax", 1, id, out string why, 0.9), why);
            Heat(w, id, target, 1, fuel);
            w.RunSteps(15); // the fold drag
            Assert.IsTrue(w.Fold(id, out why, 1, true), why);
            bar = w.Piece(id);
            Assert.AreEqual(1, CraftForging.SeamOpen(bar), 1e-9, "folded by hand, the seam is left open");
            Assert.AreEqual(0, Delamination(bar), 1e-9, "and nothing is judged yet");
            int heats = 0;
            for (; heats < 6 && CraftForging.SeamOpen(bar) > 0; heats++)
            {
                if (heats > 0)
                {
                    Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out why, 1), why);
                    if (reflux) Assert.IsTrue(w.FluxPiece("borax", 1, id, out why, 0.9), why);
                    Heat(w, id, target, 1, fuel);
                    w.RunSteps(5);
                }
                // Seven blows a second apart, each where the strip shows the seam most open.
                for (int k = 0; k < 7 && CraftForging.SeamOpen(bar) > 0; k++)
                {
                    w.RunSteps(10);
                    int most = 0;
                    for (int i = 1; i < bar.segments.Count; i++) if (bar.segments[i].seamOpen > bar.segments[most].seamOpen) most = i;
                    Assert.IsTrue(w.Strike(id, CraftMath.Clamp(bar.PositionOf(most), 0.03, 0.97), CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.9, 0, out why), why);
                }
            }
            w.RunSteps(1500); // cooled
            return heats;
        }

        [Test]
        public void ForgeWeld_ByHand_IsSetHeatByHeat_AtWeldingHeat()
        {
            foreach ((string metal, string fuel, int most) in new[] { ("Spring steel", "charcoal", 2), ("Mild steel", "charcoal", 3), ("Mild steel", "coal", 3), ("Wrought iron", "coal", 4), ("Wrought iron", "charcoal", 5) })
            {
                int heats = HandWeld(metal, fuel, out CraftPiece bar);
                Assert.LessOrEqual(heats, most, $"{metal} in {fuel}: set within {most} heats, fluxed again each heat");
                Assert.AreEqual(0, CraftForging.SeamOpen(bar), 1e-9, $"{metal} in {fuel}: the seam is set all along");
                Assert.Less(Delamination(bar), 1.0, $"{metal} in {fuel}: a weld set at welding heat holds");
                Assert.AreEqual(2, bar.layers);
            }
        }

        [Test]
        public void ForgeWeld_SeamLeftToCool_Delaminates()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Mild steel");
            LightForge(w);
            Heat(w, id, 1260);
            Assert.IsTrue(w.Fold(id, out string why, 1, true), why);
            CraftPiece p = w.Piece(id);
            Assert.Greater(CraftForging.SeamOpen(p), 0.99);
            w.RunSteps(3000); // left on the anvil to cool, never struck
            Assert.AreEqual(0, CraftForging.SeamOpen(p), 1e-9, "a seam that cools open stays open for good");
            Assert.Greater(Delamination(p), 1.0, "layers that gape apart");
            Assert.IsTrue(w.RecentEvents.Any(e => e.Kind == CraftEventKind.Note && e.Id == "seam"), "and it is told");
        }

        [Test]
        public void ForgeWeld_OnlyFlatBlowsSetTheSeam_AndColdBlowsDoNot()
        {
            CraftWorkshop w = Workshop();
            string hot = Bar(w, "Spring steel"), edge = Bar(w, "Spring steel");
            LightForge(w);
            foreach ((string id, CraftStrikeFace face) in new[] { (hot, CraftStrikeFace.Flat), (edge, CraftStrikeFace.Edge) })
            {
                Heat(w, id, 900, 1, "charcoal", false);
                Assert.IsTrue(w.FluxPiece("borax", 1, id, out string why, 1), why);
                Heat(w, id, 1200);
                Assert.IsTrue(w.Fold(id, out why, 1, true), why);
                Assert.IsTrue(w.Strike(id, 0.5, CraftStrikeTool.Flat, face, 1, 0, out why), why);
            }
            Assert.Less(CraftForging.SeamOpen(w.Piece(hot)), 0.9, "the flat of the hammer on the flat presses the faces together");
            Assert.AreEqual(1, CraftForging.SeamOpen(w.Piece(edge)), 1e-9, "struck on its edge, it does not");
            Assert.AreEqual(0, CraftForging.SeamPress(CraftStrikeTool.Fuller), 1e-9);
            Assert.Greater(CraftForging.SeamPress(CraftStrikeTool.PeenAcross), 0);
            // The bar struck last is still at welding heat; the first has cooled on the anvil meanwhile.
            CraftPiece p = w.Piece(edge);
            MetalSegment s = p.segments[p.SegmentAt(0.1)];
            CraftForging.WeldFactors(w.Rules, p, s, out double heat, out double flux, out double clean);
            Assert.Greater(heat * (0.5 + 0.5 * flux) * clean, 0, "at welding heat it welds");
            Assert.AreEqual(CraftForging.SegmentWeldQuality(w.Rules, p, s), heat * (0.5 + 0.5 * flux) * clean, 1e-9, "the three factors are what it multiplies");
            CraftPiece cooled = w.Piece(hot);
            double open = CraftForging.SeamOpen(cooled);
            if (open > 0)
            {
                Assert.IsTrue(w.Strike(hot, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out string why), why);
                Assert.AreEqual(open, CraftForging.SeamOpen(cooled), 0.01, "below welding heat a blow does not set it");
            }
        }

        [Test]
        public void ForgeWeld_RefusesColdBarsByTheirMeanHeat_OpenSeams_AndUnequalStacks()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            CraftPiece p = w.Piece(bar);
            foreach (MetalSegment s in p.segments) s.edge.temp = s.body.temp = 70;
            p.segments[3].edge.temp = p.segments[3].body.temp = 1300; // one hot stretch, the bar cold
            Assert.IsFalse(w.Fold(bar, out string why), "one hot stretch does not make a bar weldable");
            StringAssert.Contains("cold", why);
            foreach (MetalSegment s in p.segments) s.edge.temp = s.body.temp = 1250;
            Assert.IsTrue(w.Fold(bar, out why, 1, true), why);
            foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1250;
            Assert.IsFalse(w.Fold(bar, out why, 1, true), "not folded again before the last seam is set");
            StringAssert.Contains("not set", why);

            string a = Bar(w, "Mild steel", 200), b = Bar(w, "Mild steel", 120);
            foreach (string id in new[] { a, b })
                foreach (MetalSegment s in w.Piece(id).segments) s.edge.temp = s.body.temp = 1250;
            Assert.IsFalse(w.Weld(a, b, out why, 1, true), "a short bar is not stretched over a long one");
            StringAssert.Contains("same length", why);
            string c = Bar(w, "Mild steel", 190);
            foreach (MetalSegment s in w.Piece(c).segments) s.edge.temp = s.body.temp = 1250;
            Assert.IsTrue(w.Weld(a, c, out why, 1, true), why);
            Assert.AreEqual(1, CraftForging.SeamOpen(w.Piece(a)), 1e-9, "stacked by hand: the seam is open");
            Assert.IsNull(w.Piece(c), "the bar laid on is part of it now");
        }

        [Test]
        public void ForgeWeld_AtOnce_StaysForQuickBenchesAndScripts()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1250;
            Assert.IsTrue(w.Fold(bar, out string why, 1), why);
            Assert.AreEqual(0, CraftForging.SeamOpen(w.Piece(bar)), 1e-9, "the old rule: judged at once, no seam left open");
            Assert.AreEqual(CraftOpKind.Fold, w.History.Last().kind);
            Assert.IsFalse(w.History.Last().flag);
        }

        // ------------------------------------------------------------------ flux
        [Test]
        public void Flux_BurnsOffAtTheSameRate_HoweverFinelyTheBarIsCut()
        {
            var left = new List<double>();
            foreach (int n in new[] { 12, 24, 48 })
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Mild steel");
                w.Piece(bar).Resample(n);
                LightForge(w);
                Heat(w, bar, 1000, 1, "charcoal", false);
                Assert.IsTrue(w.FluxPiece("borax", 1, bar, out string why, 1), why);
                w.RunSteps(50); // 5 s at heat
                left.Add(CraftForging.MeanFlux(w.Piece(bar)));
            }
            Assert.AreEqual(left[0], left[1], 0.02, "12 or 24 stretches: the same flux left");
            Assert.AreEqual(left[1], left[2], 0.02, "24 or 48 stretches: the same flux left");
            Assert.Greater(left[1], 0.5, "a full sprinkle lasts minutes at heat, not seconds");
        }

        [Test]
        public void Flux_LandsWhereItIsSprinkled_TakesScaleOff_AndOnlyAWeldingFluxWelds()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            LightForge(w);
            Heat(w, bar, 950, 1, "charcoal", false);
            CraftPiece p = w.Piece(bar);
            int n = p.segments.Count;
            var points = Enumerable.Range(0, n).Select(i => i < n / 2 ? 1f : 0f).ToList();
            Assert.IsTrue(w.FluxPiece("borax", 1, bar, out string why, 0.5, points), why);
            Assert.Greater(p.segments[0].flux, 0.5, "where it was sprinkled");
            Assert.AreEqual(0, p.segments[n - 1].flux, 1e-9, "a bare stretch has none");
            Assert.AreEqual(CraftForging.MeanFlux(p), p.flux, 1e-9, "the piece's flux is the mean of its stretches'");
            double scaleFluxed = p.segments[1].scale, scaleBare = p.segments[n - 2].scale;
            w.RunSteps(200);
            Assert.Less(p.segments[1].scale, p.segments[n - 2].scale, "molten flux takes the scale up, the bare stretch scales on");
            Assert.LessOrEqual(p.segments[1].scale, scaleFluxed + 1e-6);
            Assert.Greater(p.segments[n - 2].scale, scaleBare);

            Assert.IsFalse(w.FluxPiece("limestone", 1, bar, out why), "a furnace flux does nothing for a weld");
            StringAssert.Contains("furnace flux", why);
            Assert.IsFalse(w.AddFlux("limestone", 1, 2, out why, bar), "not by the old way either");
            Assert.IsTrue(w.AddFlux("sand", 1, 2, out why, bar, 1), why);
            string cold = Bar(w, "Mild steel");
            Assert.IsFalse(w.FluxPiece("borax", 1, cold, out why), "flux does not stick to cold metal");
        }

        [Test]
        public void Resample_KeepsTheSeam_TheFlux_AndAHalfMadeCut()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            CraftPiece p = w.Piece(bar);
            for (int i = 0; i < p.segments.Count; i++)
            {
                p.segments[i].seamOpen = i < 12 ? 1 : 0;
                p.segments[i].flux = 0.4;
            }
            p.segments[20].cut = 0.56;
            for (int i = 0; i < 6; i++) p.segments[i].length *= 1.7; // uneven, as after drawing one end out
            double before = CraftForging.SeamOpen(p);
            p.Resample(p.segments.Count);
            Assert.AreEqual(before, CraftForging.SeamOpen(p), 0.02, "the open seam is kept by length");
            Assert.AreEqual(0.4, CraftForging.MeanFlux(p), 1e-6, "and the flux");
            Assert.AreEqual(0.56, p.segments.Max(s => s.cut), 1e-9, "a cut half made on the hardy is not wiped");
        }

        // ------------------------------------------------------------------ the fire
        [Test]
        public void Fire_TheHeartIsHottest_AndBlownItReachesWeldingHeatInCharcoal()
        {
            CraftWorkshop w = Workshop();
            LightForge(w);
            for (int t = 0; t < 300; t++) { if (t % 6 == 0) w.Bellows(0.75); w.RunSteps(1); }
            Assert.Greater(CraftThermal.ForgeHeatAt(w, 1), CraftThermal.ForgeHeatAt(w, 0.5));
            Assert.Greater(CraftThermal.ForgeHeatAt(w, 0.5), CraftThermal.ForgeHeatAt(w, 0.2));
            Assert.Greater(CraftThermal.ForgeHeatAt(w, 1), w.Forge.temperature, "in front of the tuyere, with the bellows going, the heart beats the fire's own heat");
            CraftAlloyProfile iron = CraftDefaults.Bar(w.Rules, 200, 30, 6, Metal("Wrought iron")).Profile(w.Rules);
            Assert.Greater(CraftThermal.ForgeHeatAt(w, 1), CraftForging.WeldingHeat(w.Rules, iron) + 40, "every stock metal can be welded in charcoal");
        }

        [Test]
        public void FaceBias_GrowsWhileItHeats_EvensOut_AndTurningKeepsItSmall()
        {
            double Run(int turnEvery, out double bend)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Spring steel", 250, 35, 4);
                w.Piece(bar).edges = 1;
                LightForge(w, "charcoal", 6);
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string why, 0.7), why);
                double peak = 0;
                for (int s = 1; s <= 600; s++)
                {
                    if (s % 6 == 0) w.Bellows(0.6);
                    if (turnEvery > 0 && s % turnEvery == 0) w.TurnPiece(bar, out _);
                    w.RunSteps(1);
                    peak = Math.Max(peak, Math.Abs(w.Piece(bar).faceBias));
                }
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out why), why);
                w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
                w.RunSteps(400);
                bend = Math.Abs(w.Piece(bar).segments.Sum(x => x.bend));
                return peak;
            }
            double still = Run(0, out double bendStill), turned = Run(50, out double bendTurned);
            Assert.Less(still, 0.5, "one face runs ahead only while the bar lags behind the fire, and evens out");
            Assert.Greater(still, 0.05, "but it does run ahead");
            Assert.Less(turned, still, "turned now and then, the faces stay closer");
            Assert.Less(bendTurned, bendStill + 1e-6, "and it warps no more in the quench");

            CraftWorkshop w2 = Workshop();
            string b2 = Bar(w2, "Mild steel");
            w2.Piece(b2).faceBias = 0.4;
            Assert.IsTrue(w2.TurnPiece(b2, out _));
            Assert.AreEqual(0.4, w2.Piece(b2).faceBias, 1e-9, "turning it over does not change which face ran hotter");
        }

        // ------------------------------------------------------------------ the anvil
        [Test]
        public void Hammer_NearlyLevelStrikesFlat_ATiltedBlowBevels()
        {
            CraftPiece Blows(double tilt)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Spring steel");
                w.Piece(bar).edges = 1;
                foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1050;
                for (int k = 0; k < 10; k++)
                    Assert.IsTrue(w.Strike(bar, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, tilt, out string why), why);
                return w.Piece(bar);
            }
            CraftPiece level = Blows(0), trembling = Blows(CraftForging.FlatTilt * 0.9), tilted = Blows(0.4);
            Assert.IsFalse(level.segments.Any(s => s.Beveled));
            Assert.IsFalse(trembling.segments.Any(s => s.Beveled), "the hands' own tremble does not bevel");
            Assert.AreEqual(0, trembling.segments.Sum(s => s.bend), 1e-9, "nor bend it");
            Assert.IsTrue(tilted.segments.Any(s => s.Beveled), "a blow tilted on purpose forges a bevel");
        }

        [Test]
        public void Horn_BendsTheSame_HoweverFinelyTheBarIsCut_TheWayChosen()
        {
            double Bend(int n, double tilt)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Mild steel");
                w.Piece(bar).Resample(n);
                foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1000;
                Assert.IsTrue(w.Strike(bar, 0.5, CraftStrikeTool.Horn, CraftStrikeFace.Flat, 1, tilt, out string why), why);
                return w.Piece(bar).segments.Sum(s => s.bend);
            }
            double b12 = Bend(12, 1), b24 = Bend(24, 1), b48 = Bend(48, 1);
            Assert.AreEqual(b24, b12, b24 * 0.25, "12 or 24 stretches: about the same bend");
            Assert.AreEqual(b24, b48, b24 * 0.25, "24 or 48 stretches: about the same bend");
            Assert.Less(b24, 30, "one blow bends it some, not half way round");
            Assert.Greater(b24, 5);
            Assert.Less(Bend(24, -1), 0, "the other way, the other way");
        }

        [Test]
        public void HardyAndChisel_CutBlowByBlow_EvenAtTheLastStretch()
        {
            int BlowsToCut(CraftStrikeTool tool, double at, out CraftWorkshop w, out string bar)
            {
                w = Workshop();
                bar = Bar(w, "Mild steel");
                for (int k = 1; k <= 40; k++)
                {
                    foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1000;
                    Assert.IsTrue(w.Strike(bar, at, tool, CraftStrikeFace.Flat, 1, 0, out string why), why);
                    if (w.Pieces.Count > 1) return k;
                }
                return 99;
            }
            int hardy = BlowsToCut(CraftStrikeTool.Hardy, 0.4, out _, out _);
            int chisel = BlowsToCut(CraftStrikeTool.Chisel, 0.4, out CraftWorkshop wc, out string cb);
            Assert.Less(hardy, 20, "hot, the hardy cuts through in a few blows");
            Assert.AreEqual(hardy, chisel, "the hot chisel cuts by the same rule");
            Assert.IsTrue(wc.RecentEvents.Any(e => e.Kind == CraftEventKind.Cut));
            Assert.AreEqual(200, wc.Pieces.Sum(p => p.Length), 1.5, "nothing is lost cutting");
            Assert.Less(BlowsToCut(CraftStrikeTool.Hardy, 0.995, out _, out _), 40, "the last stretch is cut off too");
            Assert.AreEqual("hot chisel", CraftForging.ToolName(CraftStrikeTool.Chisel));
            Assert.AreEqual(CraftForging.Contact(CraftStrikeTool.Hardy), CraftForging.Contact(CraftStrikeTool.Chisel));
        }

        [Test]
        public void Drift_ATapMarks_AFullBlowOpensAnEye()
        {
            double Eye(double force)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Mild steel");
                foreach (MetalSegment s in w.Piece(bar).segments) s.edge.temp = s.body.temp = 1050;
                Assert.IsTrue(w.Strike(bar, 0.3, CraftStrikeTool.Drift, CraftStrikeFace.Flat, force, 0, out string why), why);
                return w.Piece(bar).segments.Max(s => s.hole);
            }
            Assert.Less(Eye(0.02), 0.5, "a tap of the drift barely marks it");
            Assert.GreaterOrEqual(Eye(1), 3.9, "a full blow opens an eye in a thin bar");
        }

        [Test]
        public void Fasteners_ComeFromARod_NeverFromABlade_AndNotFromNothing()
        {
            CraftWorkshop w = Workshop();
            string blade = Bar(w, "Mild steel", 200, 6, 6);
            w.Piece(blade).edges = 1;
            Assert.IsNull(w.FormFastener(blade, CraftFastenerKind.Rivet, out string why), "a blade is not cut up into rivets");
            StringAssert.Contains("edge", why);
            string flat = Bar(w, "Mild steel", 200, 30, 6);
            Assert.IsNull(w.FormFastener(flat, CraftFastenerKind.Rivet, out why), "a flat bar is drawn into a rod first");
            StringAssert.Contains("rod", why);
            string crumb = Bar(w, "Mild steel", 8, 3, 3);
            int before = w.Pieces.Count;
            Assert.IsNull(w.FormFastener(crumb, CraftFastenerKind.Buckle, out why), "not metal enough for a buckle");
            Assert.IsNotEmpty(why);
            Assert.AreEqual(before, w.Pieces.Count, "and nothing is made or lost");
            string rod = Bar(w, "Mild steel", 200, 6, 6);
            Assert.IsTrue(w.CanFormFastener(w.Piece(rod), CraftFastenerKind.Rivet, out why, out int count), why);
            string made = w.FormFastener(rod, CraftFastenerKind.Rivet, out why);
            Assert.IsNotNull(made, why);
            Assert.AreEqual(count, w.Piece(made).fasteners.count, "as many as it said");
        }

        // ------------------------------------------------------------------ the quench
        [Test]
        public void Clay_IsTheSpinesCoat_ItCanBeWipedOff_AndOnlyOnTheBench()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 200, 30, 5);
            int n = w.Piece(bar).segments.Count;
            Assert.IsTrue(w.ApplyClay(bar, 0, n - 1, 3, out string why), why);
            var wiped = Enumerable.Range(0, n).Select(i => i == n - 1 ? 0f : 3f).ToList();
            Assert.IsTrue(w.ApplyClay(bar, 0, n - 1, 3, out why, wiped), why);
            Assert.AreEqual(0, w.Piece(bar).segments[n - 1].clay, 1e-9, "a stretch wiped bare has no clay left");
            Assert.AreEqual(3, w.Piece(bar).segments[0].clay, 1e-6);
            var shortList = new List<float> { 1f, 1f };
            Assert.IsTrue(w.ApplyClay(bar, 0, n - 1, 1, out why, shortList), why);
            Assert.AreEqual(3, w.Piece(bar).segments[5].clay, 1e-6, "stretches beyond the list keep what they had");
            Assert.IsTrue(w.AddFuel("charcoal", 4, out why), why);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out why, 0.5), why);
            Assert.IsFalse(w.ApplyClay(bar, 0, n - 1, 2, out why), "clay goes on with the piece on the bench");
        }

        [Test]
        public void Clay_KeepsTheSpineTough_AndLeavesTheEdgeHard()
        {
            // A bevelled single-edged blade, clayed (or not), brought to a hardening heat in the fire and quenched moving.
            (double edge, double spine) Quench(double clay, string medium)
            {
                CraftWorkshop w = Workshop(CraftTools.Everything, medium);
                string bar = Bar(w, "Spring steel", 250, 35, 4);
                CraftPiece p = w.Piece(bar);
                p.edges = 1;
                foreach (MetalSegment s in p.segments) { s.bevelDepthL = 1.6; s.bevelWidthL = 9; }
                int n = p.segments.Count;
                if (clay > 0) Assert.IsTrue(w.ApplyClay(bar, 0, n - 1, clay, out string why), why);
                LightForge(w, "charcoal", 8);
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string w1, 0.6), w1);
                for (int t = 0; t < 4000 && p.MeanTemperature < 860; t++) { if (t % 10 == 0) w.Bellows(0.4); w.RunSteps(1); }
                w.RunSteps(60);
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out w1), w1);
                w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
                w.RunSteps(400);
                CraftPieceReport r = CraftEvaluator.Piece(w.Rules, p);
                return (r.EdgeHardness, r.BodyHardness);
            }
            (double bareEdge, double bareSpine) = Quench(0, "water");
            (double edge, double spine) = Quench(3, "water");
            Assert.Less(spine, bareSpine - 60, "the clayed spine stays softer and tougher");
            Assert.Greater(edge, bareEdge - 40, "the bare edge hardens as if there were no clay (it is slowed only through the steel)");
            (double oilEdge, double oilSpine) = Quench(3, "oil");
            Assert.Greater(oilEdge, oilSpine + 80, "a hard edge on a tough spine in oil too");
        }

        [Test]
        public void Quench_FlatOrSideToSideWarps_UprightAndUpAndDownStaysTrue()
        {
            double Bend(CraftQuenchMotion motion, double entry)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Spring steel", 200, 30, 5);
                w.Piece(bar).edges = 1;
                foreach (MetalSegment s in w.Piece(bar).segments) { s.edge.temp = s.body.temp = 860; s.edge.aust = s.body.aust = 1; s.edge.pearl = s.body.pearl = 0; }
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why, 1, 0, 9999, 0, entry), why);
                w.Agitate(bar, motion, out _);
                w.RunSteps(400);
                return Math.Abs(w.Piece(bar).segments.Sum(s => s.bend));
            }
            double good = Bend(CraftQuenchMotion.UpDown, 0);
            Assert.Greater(Bend(CraftQuenchMotion.SideToSide, 0), good * 2, "side to side warps it");
            Assert.Greater(Bend(CraftQuenchMotion.UpDown, 0.9), good * 2, "so does going in flat");
        }

        [Test]
        public void Ashes_AreWarmedOnlyByHotMetal()
        {
            CraftWorkshop w = Workshop();
            string cold = Bar(w, "Mild steel");
            Assert.IsTrue(w.MovePiece(cold, CraftLocation.Ashes, out string why), why);
            Assert.AreEqual(w.Ambient, w.AshTemperature, 1, "a cold bar in cold ashes warms nothing");
            Assert.IsTrue(w.MovePiece(cold, CraftLocation.Air, out why), why);
            string hot = Bar(w, "Mild steel");
            foreach (MetalSegment s in w.Piece(hot).segments) s.edge.temp = s.body.temp = 850;
            Assert.IsTrue(w.MovePiece(hot, CraftLocation.Ashes, out why), why);
            Assert.Greater(w.AshTemperature, 300, "hot metal buried warms them");
            Assert.LessOrEqual(w.AshTemperature, w.Rules.Settings.ashTemperature + 1e-6);
        }

        [Test]
        public void Tank_IsFilledAgain_WithAnotherQuenchant_NotWithAPieceInIt()
        {
            CraftWorkshop w = Workshop();
            Assert.AreEqual("water", w.Quench.medium);
            w.Quench.temperature = 80;
            Assert.IsTrue(w.FillTank("oil", out string why), why);
            Assert.AreEqual("oil", w.Quench.medium);
            Assert.AreEqual(w.Ambient, w.Quench.temperature, 1e-9, "fresh, at the room's heat");
            Assert.IsFalse(w.FillTank("quicksilver", out why), "only a quenchant the rules know");
            string bar = Bar(w, "Mild steel");
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out why), why);
            Assert.IsFalse(w.FillTank("water", out why), "not with a piece in it");
            StringAssert.Contains("Lift", why);
            Assert.IsFalse(Workshop(CraftTools.Forge).FillTank("water", out _), "not without a tank");
            // The fill is recorded: a copy following the history and a restored save have the same tank.
            CraftWorkshop copy = CraftWorkshop.Replay(w.Rules, w.Tools, 18, 1, w.Id, w.History, w.Step);
            Assert.AreEqual("oil", copy.Quench.medium);
            Assert.AreEqual("oil", CraftWorkshop.Restore(w.Rules, w.Capture()).Quench.medium);
        }

        // ------------------------------------------------------------------ wood, the forging window, double edges
        [Test]
        public void Wood_CharsAndDriesAtTheSameRate_HoweverFinelyItIsCut_AndSteamDoesNotDryIt()
        {
            double Char(int n)
            {
                CraftWorkshop w = Workshop();
                string blank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 40, 40));
                w.Piece(blank).Resample(n);
                foreach (MetalSegment s in w.Piece(blank).segments) s.edge.temp = s.body.temp = 400;
                w.RunSteps(10);
                return w.Piece(blank).wood.charred;
            }
            Assert.AreEqual(Char(6), Char(24), 0.01, "the char is the piece's, not each stretch's");
            CraftWorkshop ws = Workshop();
            string wet = ws.AddPiece(CraftDefaults.WoodBlank(ws.Rules, "ash", 300, 40, 40));
            Assert.IsTrue(ws.MovePiece(wet, CraftLocation.Steam, out string why), why);
            double m0 = ws.Piece(wet).wood.moisture;
            ws.RunSteps(300);
            Assert.GreaterOrEqual(ws.Piece(wet).wood.moisture, m0 - 1e-9, "in the steam box it takes water up, it does not dry");
        }

        [Test]
        public void ForgingWindow_EndsShortOfWhereTheMetalBurns()
        {
            foreach (string metal in new[] { "Bronze", "Brass", "Tin", "Orichalcum bronze", "Mild steel", "Copper" })
            {
                CraftAlloyProfile prof = CraftDefaults.Bar(Rules(), 100, 10, 5, Metal(metal)).Profile(Rules());
                Assert.LessOrEqual(prof.HotWorkMax, prof.BurnTemperature - 40 + 1e-9, $"{metal}: forged above where it burns");
                Assert.LessOrEqual(prof.HotWorkMin, prof.HotWorkMax, metal);
            }
        }

        [Test]
        public void DoubleEdgedBlade_GroundOn_GetsNarrowerNotWeightless()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Spring steel", 300, 30, 5);
            CraftPiece p = w.Piece(bar);
            p.edges = 2;
            double mass0 = p.Mass(w.Rules);
            for (int k = 0; k < 40; k++)
            {
                Assert.IsTrue(w.Grind(bar, 4, 20, CraftSurfaceSide.LeftBevel, "coarse_wheel", 14, 0.7, 2, out string why), why);
                Assert.IsTrue(w.Grind(bar, 4, 20, CraftSurfaceSide.RightBevel, "coarse_wheel", 14, 0.7, 2, out why), why);
                foreach (MetalSegment s in p.segments) s.edge.temp = s.body.temp = 18; // dipped
                MetalSegment mid = p.segments[12];
                Assert.GreaterOrEqual(mid.Area(2), mid.width * mid.thickness * 0.5 - 1e-6, "never thinner than the diamond its two bevels make");
            }
            Assert.Greater(p.Mass(w.Rules), mass0 * 0.3, "grinding on takes the edges back, not the whole blade");
            Assert.LessOrEqual(p.segments[12].bevelWidthL, p.segments[12].width * 0.5 + 1e-6, "each bevel reaches the middle at most");
        }

        // ------------------------------------------------------------------ the guard
        [Test]
        public void Guard_AWeldByHandIsOneAtATime_TheHammerAtItsPaceIsNeverHeldBack()
        {
            var guard = new CraftRequestGuard(5, 0);
            var fold = new CraftRequest(CraftRequestKind.Fold, "bar") { flag = true, b = 1, count = 1 };
            Assert.IsTrue(guard.Check(ref fold, 0, out string why), why);
            Assert.AreEqual(1f, fold.b, "a fold by hand claims no setting: its blows come as strikes");
            var again = new CraftRequest(CraftRequestKind.Weld, "bar") { flag = true, other = "b" };
            Assert.IsTrue(guard.Check(ref again, 0.1, out why), why);
            var third = new CraftRequest(CraftRequestKind.Fold, "bar") { flag = true };
            Assert.IsFalse(guard.Check(ref third, 0.2, out why), "not three at once");
            StringAssert.Contains("one", why);
            // The window's hammer comes up 0.42 s after a blow (SmithHammerClock.Recovery): never too fast for the guard.
            var hammer = new CraftRequestGuard(6, 0);
            for (int k = 0; k < 40; k++)
            {
                var blow = new CraftRequest(CraftRequestKind.Strike, "bar") { a = 0.5f, b = 0.8f, c = 0.03f };
                Assert.IsTrue(hammer.Check(ref blow, 10 + k * 0.42, out why), $"blow {k}: {why}");
            }
            var flux = new CraftRequest(CraftRequestKind.Flux, "bar") { id = "borax", i0 = 2, count = 1, a = 1, points = Enumerable.Repeat(2f, 24).ToList() };
            Assert.IsTrue(guard.Check(ref flux, 1, out why), why);
            Assert.IsTrue(flux.points.All(v => v <= 1f), "a stretch is covered once at most");
            var flood = new CraftRequest(CraftRequestKind.Flux, "bar") { id = "borax", i0 = 2, count = 1, points = Enumerable.Repeat(1f, CraftRequestGuard.MaxClayValues + 1).ToList() };
            Assert.IsFalse(guard.Check(ref flood, 2, out why));
            Assert.AreEqual(CraftRequestGuard.TooMuch, why);
        }
    }
}
#endif
