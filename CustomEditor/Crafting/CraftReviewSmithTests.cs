#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the smith's review (forge, anvil, quench, oven): the bellows worked steadily leave the hammer arm
    /// fit for the setting blows; a blade has the edges its purpose gives it however the purpose was given (said, cast in
    /// the part's mould, fitted to an item) and keeps them through a weld; the oven is held to its dial; the anvil
    /// straightens wood as far as it gives; the hot chisel's cut is the chisel's; wiping clay off costs no clay.
    /// </summary>
    public class CraftReviewSmithTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "smithrev");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 6) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static void LightForge(CraftWorkshop w)
        {
            Assert.IsTrue(w.AddFuel("charcoal", 12, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
        }

        /// <summary>Every bar into the fire until each is past <paramref name="target"/>; then all out on the anvil.</summary>
        private static void Heat(CraftWorkshop w, double target, params string[] bars)
        {
            foreach (string b in bars)
                Assert.IsTrue(w.MovePiece(b, CraftLocation.Fire, out string why, 1), why);
            for (int t = 0; t < 9000 && bars.Any(b => w.Piece(b).MeanTemperature < target); t++)
            {
                if (t % 6 == 0) w.Bellows(0.75);
                if (t % 600 == 599) w.AddFuel("charcoal", 2, out _);
                w.RunSteps(1);
            }
            foreach (string b in bars)
            {
                Assert.Greater(w.Piece(b).MeanTemperature, target, "it never got hot enough");
                Assert.IsTrue(w.MovePiece(b, CraftLocation.Air, out string why), why);
            }
        }

        // ------------------------------------------------------------------ the bellows and the hammer arm
        /// <summary>A bar heated to welding heat with the bellows pumped <paramref name="hz"/> times a second by the hands that will strike it.</summary>
        private static (CraftHands arm, CraftBellowsPump pump, double seconds) PumpToWeldingHeat(double hz)
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Mild steel");
            var hands = new CraftHands(3, 0);
            var pump = new CraftBellowsPump();
            Assert.IsTrue(w.AddFuel("charcoal", 12, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out why, 1), why);
            double weld = CraftForging.WeldingHeat(w.Rules, w.Piece(id).Profile(w.Rules));
            double dt = w.Rules.Settings.stepSeconds, next = 0, t = 0;
            for (; t < 400 && w.Piece(id).MeanTemperature < weld - 20; t += dt)
            {
                if (t >= next) { w.Bellows(pump.Pump(hands)); next += 1 / hz; }
                pump.Tick(dt);
                hands.Rest(dt);
                w.RunSteps(1);
            }
            Assert.Less(t, 400, $"{hz} pumps a second bring it to welding heat");
            return (hands, pump, t);
        }

        [Test]
        public void Bellows_PumpedSteadilyToAWeldingHeat_LeaveTheHammerArmFresh_FranticPumpingTiresIt()
        {
            foreach (double hz in new[] { 1.0, 2.0 })
            {
                (CraftHands arm, _, double seconds) = PumpToWeldingHeat(hz);
                Assert.Less(arm.fatigue, 0.15, $"{hz} pumps a second for {seconds:0} s: light work, the arm is fit for the setting blows");
                Assert.AreEqual(0, CraftHands.Level(arm.fatigue), "it feels fresh");
            }
            // Frantic pumping is real effort: twenty seconds of it at six a second tire the arm, for less air.
            var frantic = new CraftHands(3, 0);
            var fast = new CraftBellowsPump();
            for (int k = 0; k < 120; k++) { fast.Pump(frantic); fast.Tick(1 / 6.0); frantic.Rest(1 / 6.0); }
            Assert.GreaterOrEqual(CraftHands.Level(frantic.fatigue), 1, $"frantic pumping tires the arm ({frantic.fatigue:0.00})");
            var calm = new CraftHands(3, 0);
            var steady = new CraftBellowsPump();
            for (int k = 0; k < 40; k++) { steady.Pump(calm); steady.Tick(0.5); calm.Rest(0.5); }
            Assert.Greater(steady.Strength, fast.Strength * 1.5, "a steady rhythm builds the stronger draft");
            // Light work still costs a little, less with practice; it is no movement of the working hand.
            var light = new CraftHands(3, 0);
            light.Exert(0.01);
            Assert.AreEqual(0.01, light.fatigue, 1e-9);
            Assert.AreEqual(0, light.count, "no movement of the hand: its noise does not move on");
            var practised = new CraftHands(3, 1);
            practised.Exert(0.01);
            Assert.Less(practised.fatigue, light.fatigue, "a practised arm tires less");
        }

        // ------------------------------------------------------------------ a blade's edges follow its purpose
        [Test]
        public void Edges_FollowThePurpose_HoweverItIsGiven_AndAWeldKeepsThem()
        {
            CraftWorkshop w = Workshop();
            // said (the carving bench, the anvil's own purpose)
            string said = Bar(w, "Spring steel");
            Assert.IsTrue(w.SetIntent(said, "arming_sword", "blade", out string why), why);
            Assert.AreEqual(2, w.Piece(said).edges, "said to be a sword's blade: double-edged");
            Assert.IsTrue(w.SetIntent(said, "hatchet", "head", out why), why);
            Assert.AreEqual(1, w.Piece(said).edges, "said to be a hatchet's head: single-edged");

            // fitted to an item, and kept when it comes off again
            string fitted = Bar(w, "Spring steel", 600, 40, 5);
            string asm = w.StartAssembly("arming_sword", out why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "blade", 0, fitted, out why), why);
            Assert.AreEqual(2, w.Piece(fitted).edges, "fitted as the sword's blade: double-edged");
            Assert.IsTrue(w.Detach(asm, "blade", 0, out why), why);
            Assert.AreEqual(2, w.Piece(fitted).edges, "and still so once it is off again");

            // a billet stacked or folded for a sword's blade is still to be double-edged
            string a = Bar(w, "Spring steel"), b = Bar(w, "Mild steel");
            Assert.IsTrue(w.SetIntent(a, "arming_sword", "blade", out why), why);
            LightForge(w);
            Heat(w, 1150, a, b);
            Assert.IsTrue(w.Weld(a, b, out why, 1, true), why);
            Assert.AreEqual(2, w.Piece(a).edges, "stacked: still the sword's blade's two edges");
            string plain = Bar(w, "Mild steel"), other = Bar(w, "Mild steel");
            w.Piece(plain).edges = 1; // ground once, meant for nothing in particular
            Heat(w, 1150, plain, other);
            Assert.IsTrue(w.Weld(plain, other, out why, 1, true), why);
            Assert.AreEqual(0, w.Piece(plain).edges, "a plain billet's bevels went into the stack: no edge yet");
            string folded = Bar(w, "Spring steel");
            Assert.IsTrue(w.SetIntent(folded, "arming_sword", "blade", out why), why);
            Heat(w, 1150, folded);
            Assert.IsTrue(w.Fold(folded, out why, 1, true), why);
            Assert.AreEqual(2, w.Piece(folded).edges, "folded: still double-edged");
        }

        [Test]
        public void Edges_OfACastingInAPartsMould_AreThePartsOwn()
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            foreach ((string element, double fraction) in Metal("Bronze"))
                w.Crucible.melt.Add(new CraftAmount(element, 3000 * fraction));
            w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + 60;
            string id = w.Pour(CraftRules.PatternMoldId("arming_sword", "blade"), 0.6, 0.9, 0, out string why);
            Assert.IsNotNull(id, why);
            CraftPiece cast = w.Piece(id);
            Assert.AreEqual("arming_sword", cast.intentDesign);
            Assert.AreEqual(2, cast.edges, "cast in a sword blade's mould: double-edged, as the anvil and the wheel will show it");
            string ingot = w.Pour("ingot", 0.6, 0.9, 0, out why);
            Assert.IsNotNull(ingot, why);
            Assert.AreEqual(0, w.Piece(ingot).edges, "an ingot has no edge");
        }

        [Test]
        public void Edges_TheWheelGivesTheFirstBevel_AsManyAsThePurposeSays()
        {
            CraftWorkshop w = Workshop();
            string meant = Bar(w, "Spring steel", 600, 40, 5), plain = Bar(w, "Spring steel", 600, 40, 5);
            CraftPiece p = w.Piece(meant);
            p.intentDesign = "arming_sword"; // a purpose given without its edges (as an older save holds it)
            p.intentRole = "blade";
            Assert.AreEqual(0, p.edges);
            Assert.IsTrue(w.Grind(meant, 0, p.segments.Count - 1, CraftSurfaceSide.LeftBevel, "coarse_wheel", 25, 0.5, 1, out string why), why);
            Assert.AreEqual(2, p.edges, "ground for a sword's blade: both edges");
            Assert.IsTrue(w.Grind(plain, 0, w.Piece(plain).segments.Count - 1, CraftSurfaceSide.LeftBevel, "coarse_wheel", 25, 0.5, 1, out why), why);
            Assert.AreEqual(1, w.Piece(plain).edges, "meant for nothing: one edge, where it was ground");
        }

        /// <summary>A clayed blade meant for an arming sword, ground on both faces, hardened in water: its total bend (degrees) and edges.</summary>
        private static double QuenchedBend(bool said, out int edges)
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel", 600, 40, 5);
            CraftPiece p = w.Piece(id);
            if (said) Assert.IsTrue(w.SetIntent(id, "arming_sword", "blade", out string why0), why0);
            else { p.intentDesign = "arming_sword"; p.intentRole = "blade"; }
            for (int k = 0; k < 8; k++)
            {
                for (int t = 0; t < 20000 && p.MaxTemperature > 120; t++) w.RunSteps(1);
                Assert.IsTrue(w.Grind(id, 0, p.segments.Count - 1, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "coarse_wheel", 25, 0.5, 1, out string why), why);
            }
            for (int t = 0; t < 20000 && p.MaxTemperature > 120; t++) w.RunSteps(1);
            edges = p.edges;
            Assert.IsTrue(w.ApplyClay(id, 0, p.segments.Count - 1, 2, out string whyc), whyc);
            LightForge(w);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out string w2, 0.5), w2);
            for (int t = 0; t < 8000 && p.MeanTemperature < 830; t++) { if (t % 6 == 0) w.Bellows(0.5); w.RunSteps(1); }
            for (int t = 0; t < 150; t++) { if (t % 6 == 0) w.Bellows(0.3); w.RunSteps(1); }
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Quench, out w2, 1, 0, 9999, 0, 0), w2);
            Assert.IsTrue(w.Agitate(id, CraftQuenchMotion.UpDown, out w2), w2);
            w.RunSteps(400);
            Assert.Greater(p.segments.Average(s => s.edge.mart), 0.5, "it hardened");
            return p.segments.Sum(s => s.bend);
        }

        [Test]
        public void Quench_ADoubleEdgedBlade_DoesNotCurveLikeASabre_WhateverGaveItItsPurpose()
        {
            double said = QuenchedBend(true, out int e1), given = QuenchedBend(false, out int e2);
            Assert.AreEqual(2, e1);
            Assert.AreEqual(2, e2, "the wheel gave it the edges its purpose says");
            Assert.AreEqual(said, given, 0.05, "the same double-edged blade, the same quench, the same bend");
        }

        // ------------------------------------------------------------------ the oven
        [Test]
        public void Oven_TakesOnlyWhatItsDialCanBeSetTo()
        {
            CraftWorkshop w = Workshop();
            foreach ((double asked, double set) in new[] { (5000.0, CraftOvenState.DialMax), (220.0, 220.0), (50.0, CraftOvenState.DialMin), (0.0, 0.0), (-40.0, 0.0), (double.NaN, 0.0) })
            {
                Assert.IsTrue(w.SetOven(asked, out string why), why);
                Assert.AreEqual(set, w.Oven.setpoint, 1e-3, $"asked {asked}");
            }
            // the guard holds a request to the dial too
            var guard = new CraftRequestGuard(3, 0);
            var r = new CraftRequest(CraftRequestKind.Oven) { a = 5000 };
            Assert.IsTrue(guard.Check(ref r, 1, out _));
            Assert.AreEqual((float)CraftOvenState.DialMax, r.a);
            // and a bar left at its hottest tempers: it does not melt
            string id = Bar(w, "Spring steel");
            Assert.IsTrue(w.SetOven(5000, out string why2), why2);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Oven, out why2), why2);
            w.RunSteps(3000);
            Assert.IsNotNull(w.Piece(id), "a bar tempering in the oven does not melt");
            Assert.LessOrEqual(w.Piece(id).MaxTemperature, CraftOvenState.DialMax + 1);
        }

        // ------------------------------------------------------------------ wood on the anvil
        private static double StraightenWood(bool steamed, out CraftPiece p)
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 400, 32, 32));
            p = w.Piece(id);
            if (steamed)
            {
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Steam, out string why), why);
                for (int t = 0; t < 40000 && p.wood.steamed < 1; t++) w.RunSteps(1);
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            }
            foreach (MetalSegment s in p.segments) s.bend = 1.5;
            double before = p.segments.Sum(s => Math.Abs(s.bend));
            for (int k = 0; k < 6; k++)
            {
                w.RunSteps(5);
                Assert.IsTrue(w.Strike(id, 0.1 + 0.15 * k, CraftStrikeTool.Straighten, CraftStrikeFace.Flat, 1, 0, out string why), why);
            }
            return p.segments.Sum(s => Math.Abs(s.bend)) / before;
        }

        [Test]
        public void Anvil_StraightensWood_SteamedHotLikeHotIron_ColdItMostlySpringsBack()
        {
            double cold = StraightenWood(false, out CraftPiece dry), hot = StraightenWood(true, out CraftPiece soft);
            Assert.Less(cold, 0.95, "six blows along it take some of the bend out of cold wood");
            Assert.Greater(cold, 0.6, "but cold it mostly springs back");
            Assert.Less(hot, 0.4, "steamed through and still hot, it straightens");
            Assert.AreEqual("Straightened on the anvil's face", soft.record.Last().text);
            Assert.Greater(CraftForging.WoodGive(soft), 0.7, "fresh from the steam it gives almost as hot iron does");
            Assert.Less(CraftForging.WoodGive(dry), 0.3);
            Assert.AreEqual(0, CraftForging.WoodGive(new CraftPiece { kind = CraftPieceKind.Metal }), "metal gives by its own heat");
        }

        // ------------------------------------------------------------------ what the anvil says a cold blow does
        /// <summary>How much one full flat blow thins the bar where it lands, cold (18 °C) or at forging heat; and the words for that blow.</summary>
        private static double Thinned(string metal, bool hot, out string feel)
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, metal);
            CraftPiece p = w.Piece(id);
            if (hot)
                foreach (MetalSegment s in p.segments) s.edge.temp = s.body.temp = p.Profile(w.Rules).HotWorkMin + 150;
            int at = p.SegmentAt(0.5);
            feel = CraftForging.BlowFeel(w.Rules, p, at);
            double before = p.segments[at].thickness;
            Assert.IsTrue(w.Strike(id, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out string why), why);
            return before - p.segments[at].thickness;
        }

        [Test]
        public void Anvil_SaysWhatAColdBlowDoes_AsTheModelMovesIt()
        {
            // Copper is worked cold: it moves well over half as much as at forging heat - never "barely moves".
            double coldCopper = Thinned("Copper", false, out string copperFeel), hotCopper = Thinned("Copper", true, out string hotFeel);
            Assert.Greater(coldCopper, hotCopper * 0.4, "cold copper still moves");
            Assert.AreEqual("", copperFeel, "and nothing says it is too cold to forge");
            Assert.AreEqual("", hotFeel);
            // Tool steel cold barely moves (and is told so); spring steel is told it tears.
            double coldTool = Thinned("Tool steel", false, out string toolFeel), hotTool = Thinned("Tool steel", true, out _);
            Assert.Less(coldTool, hotTool * 0.3, "cold tool steel barely moves");
            StringAssert.Contains("barely moves", toolFeel);
            Thinned("Spring steel", false, out string springFeel);
            StringAssert.Contains("tears", springFeel);
            // A soft metal worked cold for long stiffens, and is told to anneal it.
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Copper");
            CraftPiece p = w.Piece(id);
            int seg = p.SegmentAt(0.5);
            for (int k = 0; k < 40 && p.segments[seg].body.cold <= 1.2; k++)
            {
                w.RunSteps(5);
                Assert.IsTrue(w.Strike(id, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out string why), why);
            }
            Assert.Greater(p.segments[seg].body.cold, 1.2, "worked cold, it hardens");
            StringAssert.Contains("anneal", CraftForging.BlowFeel(w.Rules, p, seg));
        }

        // ------------------------------------------------------------------ the hot chisel
        [Test]
        public void Chisel_DrivenThrough_IsTheChiselsCut_FromAbove_TheHardyCutsFromBelow()
        {
            foreach (CraftStrikeTool tool in new[] { CraftStrikeTool.Chisel, CraftStrikeTool.Hardy })
            {
                CraftWorkshop w = Workshop();
                string id = Bar(w, "Mild steel");
                LightForge(w);
                Heat(w, 1000, id);
                CraftPiece p = w.Piece(id);
                Assert.IsTrue(w.Strike(id, 0.5, tool, CraftStrikeFace.Flat, 0.3, 0, out string why), why);
                MetalSegment cut = p.segments[p.SegmentAt(0.5)];
                Assert.Greater(cut.cut, 0, "the first blow cuts in");
                Assert.AreEqual(tool == CraftStrikeTool.Chisel, cut.cutFromAbove, "the chisel from above, the hardy from below");
                for (int k = 0; k < 60 && w.Pieces.Count == 1; k++)
                {
                    w.RunSteps(5);
                    Assert.IsTrue(w.Strike(id, 0.5, tool, CraftStrikeFace.Flat, 1, 0, out why), why);
                }
                Assert.AreEqual(2, w.Pieces.Count, "it went through");
                string record = p.record.Last().text;
                Assert.AreEqual(tool == CraftStrikeTool.Chisel ? "Cut with the hot chisel" : "Cut on the hardy", record);
                Assert.IsTrue(p.segments.All(s => s.cut == 0 && !s.cutFromAbove), "both halves start uncut");
            }
        }

        // ------------------------------------------------------------------ clay: only what goes on is used
        [Test]
        public void Clay_OnlyClayThatGoesOnIsUsed_WipingItOffIsFree()
        {
            CraftWorkshop w = Workshop();
            string id = Bar(w, "Spring steel", 400, 35, 5);
            CraftPiece p = w.Piece(id);
            int n = p.segments.Count;
            Assert.IsTrue(w.ClayGoesOn(id, 0, n - 1, 2), "a bare blade coated takes clay");
            Assert.IsTrue(w.ApplyClay(id, 0, n - 1, 2, out string why), why);
            Assert.IsFalse(w.ClayGoesOn(id, 0, n - 1, 2), "the same coat again: no more clay goes on");
            var wiped = Enumerable.Repeat(2f, n).ToList();
            for (int i = n - 6; i < n; i++) wiped[i] = 0f;
            Assert.IsFalse(w.ClayGoesOn(id, 0, n - 1, 2, wiped), "wiping some off uses none");
            var thicker = new List<float>(wiped) { [3] = 3f };
            Assert.IsTrue(w.ClayGoesOn(id, 0, n - 1, 2, thicker), "a stretch painted thicker takes clay");
            Assert.IsFalse(w.ClayGoesOn(id, 0, n - 1, 2, new List<float> { 2.005f }), "no thicker than it was, within a hair");
            Assert.IsFalse(w.ClayGoesOn("nothing", 0, 3, 2));
            Assert.IsTrue(w.ApplyClay(id, 0, n - 1, 2, out why, wiped), why);
            Assert.AreEqual(0, p.segments[n - 1].clay, "it came off");
        }
    }
}
#endif
