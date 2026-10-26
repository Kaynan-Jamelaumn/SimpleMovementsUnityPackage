#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the smithing, metallurgy and leatherworking simulation on the
    /// built-in rules: reproducibility (same operations, replay, saved state), material conservation, process boundaries
    /// and refused operations, heat treatment, ores and smelting, alloying, forging, surface work and sharpening,
    /// leather, assembly compatibility, the item evaluator, what can be observed, the journal and the hand-skill models.
    /// </summary>
    public class CraftingTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1, string medium = "water", string id = "test") =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, medium, id);

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 6) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static void LightForge(CraftWorkshop w, int fuel = 6)
        {
            Assert.IsTrue(w.AddFuel("charcoal", fuel, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
        }

        /// <summary>Into the fire until its mean temperature passes <paramref name="target"/>, then out onto the anvil.</summary>
        private static void HeatTo(CraftWorkshop w, string id, double target, double depth = 0.6)
        {
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out string why, depth), why);
            for (int t = 0; t < 4000 && w.Piece(id).MeanTemperature < target; t++)
            {
                if (t % 10 == 0) w.Bellows(0.4);
                if (t % 600 == 599) w.AddFuel("charcoal", 2, out _);
                w.RunSteps(1);
            }
            Assert.Greater(w.Piece(id).MeanTemperature, target, "it never got hot enough");
            w.RunSteps(60); // soak
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        private static void QuenchIt(CraftWorkshop w, string id, CraftQuenchMotion motion = CraftQuenchMotion.UpDown)
        {
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Quench, out string why), why);
            w.Agitate(id, motion, out _);
            w.RunSteps(400);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            w.RunSteps(50);
        }

        private static void Temper(CraftWorkshop w, string id, double celsius)
        {
            Assert.IsTrue(w.SetOven(celsius, out string why), why);
            w.RunSteps(400);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Oven, out why), why);
            w.RunSteps(1500);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
            w.RunSteps(800);
        }

        private static CraftPieceReport Report(CraftWorkshop w, string id) => CraftEvaluator.Piece(w.Rules, w.Piece(id));

        /// <summary>
        /// Grinds a span in passes the way a smith does: a second on each segment, the time it takes passing on the
        /// workshop clock, and a dip in the tank whenever it gets too hot to hold (unless <paramref name="dip"/> is off).
        /// </summary>
        private static void GrindFor(CraftWorkshop w, string id, int from, int to, CraftSurfaceSide side, string wheel, double angle, double pressure, int passes, bool dip = true)
        {
            for (int k = 0; k < passes; k++)
            {
                if (dip && w.Piece(id).MaxTemperature > 120)
                {
                    Assert.IsTrue(w.MovePiece(id, CraftLocation.Quench, out string qw), qw);
                    w.RunSteps(30);
                    Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out qw), qw);
                }
                Assert.IsTrue(w.Grind(id, from, to, side, wheel, angle, pressure, 1, out string why), why);
                w.RunSteps((to - from + 1) * 10);
            }
        }

        /// <summary>Both bevels of a blade along segments <paramref name="from"/>-23.</summary>
        private static void Bevels(CraftWorkshop w, string id, int from, double angle, int passes)
        {
            for (int k = 0; k < passes; k++)
            {
                GrindFor(w, id, from, 23, CraftSurfaceSide.LeftBevel, "coarse_wheel", angle, 0.6, 1);
                GrindFor(w, id, from, 23, CraftSurfaceSide.RightBevel, "coarse_wheel", angle, 0.6, 1);
            }
        }

        /// <summary>A thin tool-steel bar quenched hard in agitated brine: it cracks.</summary>
        private static string CrackedBar(CraftWorkshop w)
        {
            string bar = Bar(w, "Tool steel", 200, 30, 5);
            LightForge(w);
            HeatTo(w, bar, 850);
            QuenchIt(w, bar, CraftQuenchMotion.UpDown);
            Assert.IsTrue(w.Piece(bar).HasDefect(CraftDefectKind.Crack), "agitated brine cracks a thin bar of tool steel");
            return bar;
        }

        private static string PiecesJson(CraftWorkshop w)
        {
            var s = w.Capture();
            s.history.Clear();
            return s.ToJson();
        }

        /// <summary>A short smithing session: heat, forge, quench, temper.</summary>
        private static void Session(CraftWorkshop w)
        {
            string bar = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, bar, 950);
            for (int k = 0; k < 12; k++) { w.Strike(bar, 0.3 + 0.04 * k, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.8, 0.2, out _); w.RunSteps(4); }
            HeatTo(w, bar, 800);
            QuenchIt(w, bar);
            Temper(w, bar, 220);
        }

        // ------------------------------------------------------------------ configuration
        [Test]
        public void BuiltInRules_CompileWithoutErrors()
        {
            CraftRules rules = Rules();
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            Assert.Greater(rules.Designs.Length, 10);
            Assert.GreaterOrEqual(rules.SteelBase, 0);
            Assert.GreaterOrEqual(rules.SteelCarbon, 0);
            foreach (CraftDesign d in rules.Designs)
            {
                Assert.IsNotNull(d.MainComponent, d.id);
                foreach (CraftJointSpec j in d.joints)
                {
                    Assert.IsNotNull(d.Component(j.roleA), $"{d.id}.{j.id} roleA {j.roleA}");
                    Assert.IsNotNull(d.Component(j.roleB), $"{d.id}.{j.id} roleB {j.roleB}");
                }
            }
        }

        [Test]
        public void LeatherFinishing_AtTheBeamOrTheBench()
        {
            foreach (CraftTools tools in new[] { CraftTools.Tannery, CraftTools.Leatherworks })
            {
                CraftWorkshop w = Workshop(tools);
                string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
                var path = new List<float> { 2, 2, 6, 2 };
                Assert.IsTrue(w.Slick(hide, path, 0.5, out string why), $"{tools}: {why}");
                Assert.IsTrue(w.Skive(hide, path, 0.3, out why), $"{tools}: {why}");
                Assert.IsTrue(w.Stake(hide, path, 0.5, out why), $"{tools}: {why}");
            }
            Assert.IsFalse(Workshop(CraftTools.Smithy).Slick("x", new List<float> { 0, 0 }, 0.5, out string none));
            StringAssert.Contains("leather bench", none);
        }

        [Test]
        public void DesignMenu_GroupsByCategoryAndMaterial()
        {
            CraftRules rules = Rules();
            void Is(string id, string category, string group)
            {
                CraftDesignMenu.Of(rules.Design(id), out string c, out string g);
                Assert.AreEqual(category + "/" + group, c + "/" + g, id);
            }
            Is("longsword", CraftDesignMenu.Weapons, "Swords");
            Is("halberd", CraftDesignMenu.Weapons, "Spears and polearms");
            Is("breastplate", CraftDesignMenu.Armour, "Plate");
            Is("leather_jerkin", CraftDesignMenu.Armour, "Leather");
            Is("padded_gambeson", CraftDesignMenu.Armour, "Padded");
            Is("linen_shirt", CraftDesignMenu.Clothing, "Body");
            Is("kite_shield", CraftDesignMenu.Shields, "Wooden");
            Is("scythe", CraftDesignMenu.Tools, "Blades");
            CraftDesign custom = rules.Design("dagger");
            custom.menuGroup = "Heirlooms/Knives";
            CraftDesignMenu.Of(custom, out string cc, out string gg);
            Assert.AreEqual("Heirlooms/Knives", cc + "/" + gg, "a design's own menu group wins");
        }

        [Test]
        public void Content_AddMissing_CatchesUpWithoutTouchingChanges()
        {
            CraftContent old = CraftDefaults.Content();
            old.designs.RemoveAll(d => d.id != "dagger");
            old.designs[0].price = 999;
            int before = old.designs.Count;
            int added = old.AddMissing(CraftDefaults.Content());
            Assert.Greater(added, 50);
            Assert.AreEqual(999, old.designs.Find(d => d.id == "dagger").price, "the project's own change stays");
            Assert.IsNotNull(old.designs.Find(d => d.id == "longsword"));
            Assert.AreEqual(0, old.AddMissing(CraftDefaults.Content()), "nothing twice");
            Assert.IsEmpty(CraftRules.Compile(new CraftSimulationSettings(), old).CompileErrors);
            Assert.Greater(old.designs.Count, before);
        }

        [Test]
        public void Content_ExtendsWithoutCodeChanges()
        {
            CraftContent content = CraftDefaults.Content();
            var extra = new CraftContent();
            extra.elements.Add(new CraftElement { id = "moonsilver", displayName = "Moonsilver", density = 6, meltingPoint = 900, hardness = 90, toughness = 40, ductility = 30, corrosion = 0.95f, observation = "a cold white metal" });
            extra.classes.Add(new CraftMaterialClass { id = "moon_bronze", displayName = "Moon bronze", baseElement = "copper", priority = 99, observation = "a pale bronze" }.With("moonsilver", 0.05f, 0.5f));
            content.Merge(extra);
            CraftRules rules = CraftRules.Compile(new CraftSimulationSettings(), content);
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            CraftPiece bar = CraftDefaults.Bar(rules, 100, 20, 5, ("copper", 0.8), ("moonsilver", 0.2));
            Assert.AreEqual("moon_bronze", bar.Profile(rules).Class.id);
        }

        // ------------------------------------------------------------------ reproducibility and saving
        [Test]
        public void SameOperations_GiveTheSameWorkshop()
        {
            CraftWorkshop a = Workshop(), b = Workshop();
            Session(a);
            Session(b);
            Assert.AreEqual(a.Step, b.Step);
            Assert.AreEqual(a.Capture().ToJson(), b.Capture().ToJson());
        }

        [Test]
        public void Replay_RebuildsTheWorkshopFromItsHistory()
        {
            CraftWorkshop a = Workshop();
            Session(a);
            CraftWorkshop b = CraftWorkshop.Replay(Rules(), a.Tools, a.Ambient, a.Seed, a.Id, a.History, a.Step);
            Assert.AreEqual(a.Step, b.Step);
            Assert.AreEqual(PiecesJson(a), PiecesJson(b));
        }

        [Test]
        public void CaptureAndRestore_ContinueExactly()
        {
            CraftWorkshop a = Workshop();
            string bar = Bar(a, "Spring steel");
            LightForge(a);
            HeatTo(a, bar, 700);
            Assert.IsTrue(a.MovePiece(bar, CraftLocation.Fire, out string why), why);
            string json = a.Capture().ToJson();
            CraftWorkshop b = CraftWorkshop.Restore(Rules(), CraftWorkshopState.FromJson(json));
            foreach (CraftWorkshop w in new[] { a, b })
            {
                w.RunSteps(200);
                w.MovePiece(bar, CraftLocation.Air, out _);
                w.Strike(bar, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out _);
                w.RunSteps(50);
                QuenchIt(w, bar);
            }
            Assert.AreEqual(PiecesJson(a), PiecesJson(b));
            Assert.AreEqual(a.History.Count, b.History.Count);
        }

        [Test]
        public void SavedState_SurvivesJson()
        {
            CraftWorkshop w = Workshop();
            Session(w);
            w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 3));
            w.AddPiece(CraftOres.NewLump(w.Rules, "malachite", 4, 2));
            string json = w.Capture().ToJson();
            CraftWorkshopState s = CraftWorkshopState.FromJson(json);
            Assert.IsNotNull(s);
            Assert.AreEqual(json, s.ToJson());
            Assert.AreEqual(w.Pieces.Count, s.pieces.Count);
            Assert.IsNull(CraftWorkshopState.FromJson("not json"));
        }

        [Test]
        public void Deposits_AreRepeatableAndVary()
        {
            CraftRules rules = Rules();
            string A(int deposit, int lump) => JsonUtilityOf(CraftOres.NewLump(rules, "hematite", deposit, lump));
            Assert.AreEqual(A(5, 1), A(5, 1));
            Assert.AreNotEqual(A(5, 1), A(5, 2));
            Assert.AreNotEqual(A(5, 1), A(6, 1));
            double Iron(int deposit) => Enumerable.Range(0, 6).Average(k => CraftOres.NewLump(rules, "hematite", deposit, k).ore.Bound("iron"));
            var deposits = Enumerable.Range(0, 8).Select(Iron).ToList();
            Assert.Greater(deposits.Max() - deposits.Min(), 0.005, "deposits of the same ore should differ in richness");
        }

        private static string JsonUtilityOf(CraftPiece p) => UnityEngine.JsonUtility.ToJson(p);

        // ------------------------------------------------------------------ conservation
        [Test]
        public void Forging_ConservesMetal()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel", 200, 30, 12);
            LightForge(w);
            HeatTo(w, bar, 1050, 0.8);
            CraftPiece p = w.Piece(bar);
            double m0 = p.Mass(w.Rules), l0 = p.Length, t0 = p.segments[12].thickness;
            for (int k = 0; k < 30; k++)
            {
                Assert.IsTrue(w.Strike(bar, 0.2 + 0.6 * (k % 13) / 12.0, CraftStrikeTool.PeenAcross, CraftStrikeFace.Flat, 0.9, 0, out string why), why);
                w.RunSteps(5);
            }
            double m1 = p.Mass(w.Rules);
            Assert.LessOrEqual(m1, m0 + 1e-6, "forging cannot create metal");
            Assert.Greater(m1, m0 * 0.97, "only scale should be lost");
            Assert.Greater(p.Length, l0 + 5, "peening across should draw it out");
            Assert.Less(p.segments[12].thickness, t0);
        }

        [Test]
        public void Splitting_ConservesMass()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Copper", 200, 20, 6); // soft: cuts cold
            double m0 = w.Piece(bar).Mass(w.Rules);
            string other = w.SplitPiece(bar, 0.4, out string why);
            Assert.IsNotNull(other, why);
            Assert.AreEqual(m0, w.Piece(bar).Mass(w.Rules) + w.Piece(other).Mass(w.Rules), m0 * 1e-6);
        }

        [Test]
        public void Crushing_ConservesMass_AndWashingCannotRemoveBoundImpurities()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "bog_iron", 9, 0));
            CraftPiece ore = w.Piece(id);
            double m0 = ore.ore.Mass, p0 = ore.ore.Bound("phosphorus");
            for (int f = ore.ore.fragments.Count - 1; f >= 0; f--)
                Assert.IsTrue(w.CrushOre(id, f, 1.2, out string why), why);
            Assert.Greater(ore.ore.fragments.Count, 1);
            Assert.AreEqual(m0, ore.ore.Mass, m0 * 1e-6, "crushing moves mass between fragments, it does not make or lose any");
            double mineralShare0 = ore.ore.MineralMass / ore.ore.Mass;
            Assert.IsTrue(w.WashOre(id, 0.6, 20, 0.7, out string w2), w2);
            Assert.Less(ore.ore.Mass, m0, "washing carries dirt and light rock away");
            Assert.GreaterOrEqual(ore.ore.MineralMass / ore.ore.Mass, mineralShare0 - 1e-9, "washing enriches the ore");
            Assert.AreEqual(p0, ore.ore.Bound("phosphorus"), 1e-12, "phosphorus bound in the mineral does not wash out");
        }

        [Test]
        public void Crucible_ConservesMetal_AndAlloysByProportion()
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            string cu = w.AddPiece(CraftDefaults.Bar(w.Rules, 120, 30, 20, ("copper", 1.0)));
            string sn = w.AddPiece(CraftDefaults.Bar(w.Rules, 40, 20, 10, ("tin", 1.0)));
            double cuMass = w.Piece(cu).Mass(w.Rules), snMass = w.Piece(sn).Mass(w.Rules);
            // Copper first; the tin goes in once it is liquid, under a cover of borax, so it does not burn away.
            Assert.IsTrue(w.MovePiece(cu, CraftLocation.Melt, out string why), why);
            LightForge(w, 10);
            void MeltAll()
            {
                for (int t = 0; t < 6000 && w.Pieces.Any(p => p.location == CraftLocation.Melt); t++) { if (t % 5 == 0) w.Bellows(0.8); if (t % 900 == 899) w.AddFuel("charcoal", 3, out _); w.RunSteps(1); }
            }
            MeltAll();
            Assert.IsTrue(w.AddFlux("borax", 1, 1, out why), why);
            Assert.IsTrue(w.MovePiece(sn, CraftLocation.Melt, out why), why);
            MeltAll();
            Assert.IsFalse(w.Pieces.Any(p => p.location == CraftLocation.Melt), "both should have melted");
            double melt = w.Crucible.Mass;
            Assert.LessOrEqual(melt, cuMass + snMass + 1e-6);
            Assert.Greater(melt, (cuMass + snMass) * 0.95, "melting loses only a little to dross");
            w.StirMelt(1, out _);
            for (int t = 0; t < 100; t++) { if (t % 5 == 0) w.Bellows(0.6); w.RunSteps(1); }
            string ingot = w.Pour("ingot", 0.6, 0.9, 0, out why);
            Assert.IsNotNull(ingot, why);
            CraftPiece cast = w.Piece(ingot);
            Assert.LessOrEqual(cast.Mass(w.Rules) + w.Crucible.Mass, melt + 1e-6);
            CraftAlloyProfile prof = cast.Profile(w.Rules);
            Assert.AreEqual("bronze", prof.Class?.id, "copper with about a tenth of tin is bronze, whatever it was called before");
            Assert.Greater(prof.Hardness, CraftDefaults.Bar(w.Rules, 10, 10, 10, ("copper", 1.0)).Profile(w.Rules).Hardness, "bronze is harder than copper");
        }

        [Test]
        public void CuttingAPattern_MovesCellsFromTheHide()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            CraftPiece hide = w.Piece(id);
            int before = hide.hide.cells.Count(c => c.present);
            CraftPattern strap = w.Rules.Pattern("strap");
            string part = w.CutPattern(id, "strap", 2, 2, 0, 0.9, out string why);
            Assert.IsNotNull(part, why);
            int cut = w.Piece(part).hide.cells.Count(c => c.present);
            Assert.AreEqual(strap.Area, cut);
            Assert.AreEqual(before - cut, hide.hide.cells.Count(c => c.present));
            Assert.IsNull(w.CutPattern(id, "strap", 2, 2, 0, 0.9, out why), "the leather there is gone");
        }

        // ------------------------------------------------------------------ boundaries and refusals
        [Test]
        public void RefusedOperations_ChangeNothing()
        {
            CraftWorkshop smithy = Workshop(CraftTools.Forge | CraftTools.Anvil);
            string bar = Bar(smithy, "Mild steel");
            int ops = smithy.History.Count;
            string before = smithy.Capture().ToJson();
            Assert.IsFalse(smithy.Sharpen(bar, 0, 23, CraftSurfaceSide.LeftBevel, "fine_stone", 15, 0.5, 10, 0, true, out string why));
            Assert.IsNotEmpty(why);
            Assert.IsFalse(smithy.Pour("ingot", 0.5, 0.5, 0, out why) != null);
            Assert.IsFalse(smithy.MovePiece(bar, CraftLocation.Quench, out why), "no quench tank here");
            Assert.IsFalse(smithy.Strike(bar, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0, 0, out why), "a blow with no force");
            Assert.IsFalse(smithy.AddFuel("no_such_fuel", 1, out why));
            Assert.IsFalse(smithy.TapSlag(out why));
            Assert.IsFalse(smithy.FillVat(0, "bark", 1, out why));
            Assert.IsNull(smithy.StartAssembly("dagger", out why));
            Assert.AreEqual(ops, smithy.History.Count);
            Assert.AreEqual(before, smithy.Capture().ToJson());

            CraftWorkshop w = Workshop();
            string hot = Bar(w, "Tool steel");
            LightForge(w);
            Assert.IsTrue(w.MovePiece(hot, CraftLocation.Fire, out why), why);
            Assert.IsFalse(w.Grind(hot, 0, 23, CraftSurfaceSide.Faces, "coarse_wheel", 0, 0.5, 5, out why), "it is in the fire");
            Assert.IsFalse(w.Strike(hot, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out why), "it is in the fire");
            w.RunSteps(300);
            w.MovePiece(hot, CraftLocation.Air, out _);
            Assert.IsFalse(w.Sharpen(hot, 0, 23, CraftSurfaceSide.LeftBevel, "fine_stone", 15, 0.5, 10, 0, true, out why), "too hot to sharpen");
            Assert.IsFalse(w.Sharpen(hot, 0, 23, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 0.5, 10, 0, true, out why), "a wheel is not a whetstone");
            Assert.IsFalse(w.Fold(Bar(w, "Mild steel"), out why), "cold steel cannot be folded");
        }

        [Test]
        public void ColdForging_HardensSoftMetal_AndCracksHardSteel()
        {
            CraftWorkshop w = Workshop();
            string cu = Bar(w, "Bronze", 150, 25, 6);
            double h0 = Report(w, cu).BodyHardness;
            for (int k = 0; k < 20; k++) { w.Strike(cu, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.6, 0, out _); w.RunSteps(5); }
            Assert.Greater(Report(w, cu).BodyHardness, h0 + 10, "bronze hardens when hammered cold");

            string steel = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, steel, 800);
            QuenchIt(w, steel);
            for (int k = 0; k < 15; k++) { w.Strike(steel, 0.5, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out _); w.RunSteps(5); }
            Assert.IsTrue(w.Piece(steel).HasDefect(CraftDefectKind.Crack), "hardened steel cracks under the hammer");
        }

        [Test]
        public void Overheating_BurnsSteel()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 120, 20, 5);
            LightForge(w, 10);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string why, 1), why);
            bool burning = false;
            w.EventRaised += e => burning |= e.Kind == CraftEventKind.Burning;
            for (int t = 0; t < 3000; t++) { w.Bellows(1); w.RunSteps(1); }
            Assert.IsTrue(w.Piece(bar) == null || w.Piece(bar).HasDefect(CraftDefectKind.Burnt), "left in a roaring fire, high-carbon steel burns");
            Assert.IsTrue(burning, "burning throws sparks");
        }

        [Test]
        public void Descaling_RemovesScaleButNotCracks()
        {
            CraftWorkshop w = Workshop(medium: "brine");
            string bar = CrackedBar(w);
            CraftPiece p = w.Piece(bar);
            double scale0 = Report(w, bar).Scale;
            int cracks = p.defects.Count(d => d.kind == CraftDefectKind.Crack);
            Assert.IsTrue(w.Descale(bar, 0, 23, "wire_brush", 0.7, 30, out string why), why);
            Assert.Less(Report(w, bar).Scale, scale0 + 1e-9);
            Assert.AreEqual(cracks, p.defects.Count(d => d.kind == CraftDefectKind.Crack), "brushing does not mend cracks");
        }

        // ------------------------------------------------------------------ heat treatment
        [Test]
        public void WaterQuench_HardensHighCarbonSteel_NotMildSteel()
        {
            CraftWorkshop w = Workshop();
            string hard = Bar(w, "Tool steel"), mild = Bar(w, "Mild steel");
            LightForge(w);
            HeatTo(w, hard, 820);
            QuenchIt(w, hard);
            HeatTo(w, mild, 900);
            QuenchIt(w, mild);
            Assert.Greater(Report(w, hard).BodyHardness, 650);
            Assert.Less(Report(w, mild).BodyHardness, 420);
        }

        [Test]
        public void QuenchFromBelowTheCriticalHeat_DoesNotHarden()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, bar, 600);
            Assume.That(w.Piece(bar).MaxTemperature, Is.LessThan(w.Piece(bar).Profile(w.Rules).CriticalTemperature));
            QuenchIt(w, bar);
            Assert.Less(Report(w, bar).BodyHardness, 350);
        }

        [Test]
        public void Tempering_TradesHardnessForToughness()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, bar, 820);
            QuenchIt(w, bar);
            CraftPieceReport q = Report(w, bar);
            Temper(w, bar, 230);
            CraftPieceReport t = Report(w, bar);
            Assert.Less(t.BodyHardness, q.BodyHardness);
            Assert.Greater(t.Toughness, q.Toughness);
            Assert.Greater(t.BodyHardness, 500, "a low temper keeps most of the hardness");
            Temper(w, bar, 450);
            Assert.Less(Report(w, bar).BodyHardness, t.BodyHardness, "a hotter temper softens it more");
        }

        [Test]
        public void AnnealingInAshes_SoftensMoreThanAirCooling()
        {
            CraftWorkshop a = Workshop(), b = Workshop();
            string ba = Bar(a, "Tool steel"), bb = Bar(b, "Tool steel");
            LightForge(a);
            LightForge(b);
            HeatTo(a, ba, 820);
            HeatTo(b, bb, 820);
            a.RunSteps(3000);
            Assert.IsTrue(b.MovePiece(bb, CraftLocation.Ashes, out string why), why);
            b.RunSteps(20000);
            Assert.Less(Report(b, bb).BodyHardness, Report(a, ba).BodyHardness);
        }

        [Test]
        public void Quenchants_DifferInSeverity()
        {
            double Hardness(string medium, string metal)
            {
                CraftWorkshop w = Workshop(medium: medium);
                string bar = Bar(w, metal);
                LightForge(w);
                HeatTo(w, bar, 820);
                QuenchIt(w, bar);
                return Report(w, bar).BodyHardness;
            }
            Assert.Greater(Hardness("water", "Spring steel"), Hardness("oil", "Spring steel") + 50, "oil is gentler: shallow-hardening steel stays softer");
            Assert.Greater(Hardness("oil", "Starmetal steel"), 650, "an alloy steel hardens even in oil");
        }

        [Test]
        public void FantasticQuenchant_LendsItsElement()
        {
            CraftWorkshop w = Workshop(medium: "dragon_blood");
            string bar = Bar(w, "Starmetal steel");
            LightForge(w);
            HeatTo(w, bar, 820);
            QuenchIt(w, bar);
            Assert.Greater(w.Piece(bar).Tag("affinity.Fire"), 0.1, "steel hardened in dragon's blood takes on fire");
            CraftWorkshop plain = Workshop();
            string other = Bar(plain, "Starmetal steel");
            LightForge(plain);
            HeatTo(plain, other, 820);
            QuenchIt(plain, other);
            Assert.AreEqual(0, plain.Piece(other).Tag("affinity.Fire"), 1e-9);
        }

        [Test]
        public void UnevenQuench_Warps()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 300, 30, 5);
            LightForge(w);
            HeatTo(w, bar, 820, 0.8);
            QuenchIt(w, bar, CraftQuenchMotion.Still);
            Assert.Greater(Math.Abs(Report(w, bar).Bend), 1, "a long thin blade dipped flat warps");
        }

        // ------------------------------------------------------------------ smelting
        [Test]
        public void Smelting_CleanedOreYieldsMoreIron()
        {
            double Yield(bool clean)
            {
                CraftWorkshop w = Workshop(CraftTools.Everything, 1);
                w.AddFuel("charcoal", 10, out _, true);
                for (int k = 0; k < 4; k++)
                {
                    string oid = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 77, k));
                    CraftPiece ore = w.Piece(oid);
                    if (!clean) continue;
                    for (int pass = 0; pass < 3; pass++)
                        for (int f = ore.ore.fragments.Count - 1; f >= 0; f--) w.CrushOre(oid, f, 1.2, out _);
                    for (int f = ore.ore.fragments.Count - 1; f >= 0; f--)
                        if (ore.ore.fragments[f].mineral < 0.45 && ore.ore.fragments.Count > 1) w.DiscardFragment(oid, f, out _);
                    w.WashOre(oid, 0.6, 20, 0.7, out _);
                }
                double iron = w.Pieces.Sum(p => p.ore.MineralMass * p.ore.Bound("iron"));
                foreach (CraftPiece p in w.Pieces.ToList()) Assert.IsTrue(w.MovePiece(p.id, CraftLocation.Furnace, out string why), why);
                Assert.IsTrue(w.AddFlux("limestone", 4, 0, out string fw), fw);
                for (int t = 0; t < 700; t++)
                {
                    if (w.Furnace.fire.temperature < 1200) w.Bellows(0.7, true);
                    if (t % 60 == 0) w.AddFuel("charcoal", 2, out _, true);
                    w.RunSteps(10);
                    if (t % 150 == 0) w.TapSlag(out _);
                }
                string bloom = w.ExtractBloom(out string bw);
                Assert.IsNotNull(bloom, bw);
                CraftPiece b = w.Piece(bloom);
                double got = b.Grams("iron");
                Assert.LessOrEqual(got, iron + 1e-6, "the furnace cannot make iron");
                Assert.IsTrue(b.Profile(w.Rules).Class.baseElement == "iron");
                return got / iron;
            }
            double raw = Yield(false), cleaned = Yield(true);
            Assert.Greater(raw, 0.2, "a raw charge still gives iron");
            Assert.Greater(cleaned, raw, "cleaned ore loses less iron to the slag");
        }

        // ------------------------------------------------------------------ surface work and sharpening
        [Test]
        public void Grinding_MakesAnEdge_AndSharpeningKeensIt()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Spring steel", 200, 30, 5);
            Assert.IsTrue(w.SetIntent(bar, "dagger", "blade", out string why), why);
            CraftPiece p = w.Piece(bar);
            Assert.IsFalse(Report(w, bar).HasEdge && Report(w, bar).Sharpness > 0.3);
            Bevels(w, bar, 6, 12, 40);
            CraftPieceReport ground = Report(w, bar);
            Assert.IsTrue(ground.HasEdge);
            Assert.Less(p.Mass(w.Rules), CraftDefaults.Bar(w.Rules, 200, 30, 5, Metal("Spring steel")).Mass(w.Rules), "grinding takes metal off");
            for (int k = 0; k < 6; k++)
            {
                Assert.IsTrue(w.Sharpen(bar, 6, 23, CraftSurfaceSide.LeftBevel, "medium_stone", 15, 0.5, 20, 0, true, out why), why);
                Assert.IsTrue(w.Sharpen(bar, 6, 23, CraftSurfaceSide.RightBevel, "medium_stone", 15, 0.5, 20, 0, true, out why), why);
            }
            for (int k = 0; k < 4; k++)
            {
                w.Sharpen(bar, 6, 23, CraftSurfaceSide.LeftBevel, "fine_stone", 15, 0.3, 20, 0, false, out _);
                w.Sharpen(bar, 6, 23, CraftSurfaceSide.RightBevel, "fine_stone", 15, 0.3, 20, 0, false, out _);
            }
            CraftPieceReport keen = Report(w, bar);
            Assert.Greater(keen.Sharpness, ground.Sharpness + 0.1);
        }

        [Test]
        public void ShakyHands_GiveAWorseEdge()
        {
            double Edge(double wobble)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Spring steel", 200, 30, 5);
                w.SetIntent(bar, "dagger", "blade", out _);
                Bevels(w, bar, 6, 12, 40);
                for (int k = 0; k < 8; k++)
                {
                    w.Sharpen(bar, 6, 23, CraftSurfaceSide.LeftBevel, "fine_stone", 15, 0.4, 20, wobble, true, out _);
                    w.Sharpen(bar, 6, 23, CraftSurfaceSide.RightBevel, "fine_stone", 15, 0.4, 20, wobble, true, out _);
                }
                return Report(w, bar).Sharpness;
            }
            Assert.Greater(Edge(0), Edge(8));
        }

        [Test]
        public void HeavyGrinding_DrawsTheTemper()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 200, 30, 3);
            LightForge(w);
            HeatTo(w, bar, 820);
            QuenchIt(w, bar);
            double h0 = Report(w, bar).EdgeHardness;
            bool hot = false;
            for (int k = 0; k < 20 && w.Piece(bar).MaxTemperature < 380; k++)
            {
                GrindFor(w, bar, 8, 16, CraftSurfaceSide.Faces, "coarse_wheel", 0, 1, 1, false);
                Assert.IsTrue(w.Grind(bar, 8, 16, CraftSurfaceSide.Faces, "coarse_wheel", 0, 1, 4, out string why), why); // bearing down without a dip
                hot |= w.Piece(bar).MaxTemperature > 250;
            }
            w.RunSteps(600);
            Assert.IsTrue(hot, "pressing hard on the wheel for long heats the steel");
            Assert.Less(Report(w, bar).EdgeHardness, h0 - 30, "and the heat draws the temper");
        }

        // ------------------------------------------------------------------ leather
        private static string Flesh(CraftWorkshop w, string hide, double pressure = 0.6)
        {
            CraftPiece h = w.Piece(hide);
            for (int pass = 0; pass < 2; pass++)
                for (int y = 0; y < h.hide.height; y++)
                    Assert.IsTrue(w.Scrape(hide, new float[] { 0, y + 0.5f, h.hide.width, y + 0.5f }, false, pressure, 0.8, out string why), why);
            return hide;
        }

        private static void Hours(CraftWorkshop w, double hours) =>
            w.RunSteps((int)Math.Round(hours / w.Rules.Settings.processHoursPerSecond / w.Rules.Settings.stepSeconds));

        [Test]
        public void Tanning_ReachesTheCoreWithTime()
        {
            CraftWorkshop w = Workshop();
            string hide = Flesh(w, w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5)));
            Assert.IsTrue(w.FillVat(0, "bark", 2, out string why), why);
            Assert.IsTrue(w.MovePiece(hide, CraftLocation.Vat, out why, 1, 0, 9999, 0), why);
            Hours(w, 8);
            CraftPieceReport early = Report(w, hide);
            Hours(w, 52);
            CraftPieceReport late = Report(w, hide);
            Assert.Greater(early.Tannage, 0.05);
            Assert.Greater(late.CoreTannage, early.CoreTannage + 0.2, "the core tans slowly");
            Assert.Greater(late.Tannage, 0.5);
        }

        [Test]
        public void ScrapingTooHard_ScoresTheHide()
        {
            CraftWorkshop gentle = Workshop(), rough = Workshop();
            string a = Flesh(gentle, gentle.AddPiece(CraftLeather.NewHide(Rules(), "deer", 5)), 0.5);
            string b = rough.AddPiece(CraftLeather.NewHide(Rules(), "deer", 5));
            for (int k = 0; k < 3; k++) Flesh(rough, b, 1);
            Assert.IsFalse(gentle.Piece(a).HasDefect(CraftDefectKind.Score));
            Assert.IsTrue(rough.Piece(b).HasDefect(CraftDefectKind.Score));
            Assert.Greater(Report(rough, b).Damage, Report(gentle, a).Damage);
        }

        [Test]
        public void Staking_KeepsDryingLeatherSupple()
        {
            CraftPieceReport Dried(bool stake)
            {
                CraftWorkshop w = Workshop();
                string hide = Flesh(w, w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5)));
                w.FillVat(0, "bark", 2, out _);
                w.MovePiece(hide, CraftLocation.Vat, out _, 1, 0, 9999, 0);
                Hours(w, 60);
                Assert.IsTrue(w.MovePiece(hide, CraftLocation.Rack, out string why), why);
                CraftPiece h = w.Piece(hide);
                for (int k = 0; k < 12; k++)
                {
                    Hours(w, 2);
                    if (!stake) continue;
                    for (int y = 0; y < h.hide.height; y++)
                        Assert.IsTrue(w.Stake(hide, new float[] { 0, y + 0.5f, h.hide.width, y + 0.5f }, 0.6, out why), why);
                }
                Hours(w, 10);
                return Report(w, hide);
            }
            CraftPieceReport staked = Dried(true), left = Dried(false);
            Assert.Less(staked.Stiffness, left.Stiffness);
        }

        [Test]
        public void HotVat_ShrinksTannedLeather()
        {
            CraftWorkshop w = Workshop();
            string hide = Flesh(w, w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 5)));
            w.FillVat(0, "bark", 2, out _);
            w.MovePiece(hide, CraftLocation.Vat, out _, 1, 0, 9999, 0);
            Hours(w, 30);
            Assert.IsTrue(w.SetVatHeat(0, 1, out string why), why);
            Hours(w, 6);
            Assert.Greater(Report(w, hide).Shrink, 0.1, "leather heated past its shrink temperature shrinks");
        }

        // ------------------------------------------------------------------ assembly
        /// <summary>A bronze hatchet head cast and cooled, and an ash haft: the parts of a hatchet.</summary>
        private static void HatchetParts(CraftWorkshop w, out string head, out string haft, double haftSize = 29)
        {
            string ingot = w.AddPiece(CraftDefaults.Bar(w.Rules, 150, 40, 25, Metal("Bronze")));
            Assert.IsTrue(w.MovePiece(ingot, CraftLocation.Melt, out string why), why);
            LightForge(w, 10);
            for (int t = 0; t < 5000 && w.Crucible.Mass < 1; t++) { if (t % 5 == 0) w.Bellows(0.8); w.RunSteps(1); }
            for (int t = 0; t < 200; t++) { if (t % 5 == 0) w.Bellows(0.5); w.RunSteps(1); }
            head = w.Pour("axe_head", 0.6, 0.9, 0, out why);
            Assert.IsNotNull(head, why);
            Assert.IsTrue(w.SetIntent(head, "hatchet", "head", out why), why);
            Assert.IsTrue(w.MovePiece(head, CraftLocation.Quench, out why), why);
            w.RunSteps(600);
            Assert.IsTrue(w.MovePiece(head, CraftLocation.Air, out why), why);
            w.RunSteps(50);
            haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 430, haftSize, haftSize));
            Assert.IsTrue(w.SetIntent(haft, "hatchet", "haft", out why), why);
        }

        [Test]
        public void Assembly_ChecksPartsAndFit()
        {
            CraftWorkshop w = Workshop(seed: 4);
            HatchetParts(w, out string head, out string haft, 34);
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsFalse(w.Attach(asm, "head", 0, haft, out why), "wood is not a hatchet head");
            Assert.IsFalse(w.Attach(asm, "spike", 0, head, out why), "the design has no such part");
            Assert.IsTrue(w.Attach(asm, "head", 0, head, out why), why);
            Assert.IsFalse(w.Attach(asm, "haft", 0, haft, out why), "a haft thicker than the eye does not go in");
            StringAssert.Contains("too thick", why);
            Assert.IsFalse(w.Finish(asm, out why), "the haft is missing");
            // Rasp the haft's end down until it fits.
            for (int k = 0; k < 40 && !w.Attach(asm, "haft", 0, haft, out why); k++)
                Assert.IsTrue(w.File(haft, 9, 11, CraftSurfaceSide.Tang, "rasp", 0, 0.8, 20, out string fw), fw); // round the end down
            Assert.AreEqual(CraftLocation.Assembled, w.Piece(haft).location, why);
            Assert.IsFalse(w.Finish(asm, out why), "the head is not wedged yet");
            Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 7, 0.8, 0, out why), why);
            Assert.IsTrue(w.Finish(asm, out why), why);
            Assert.IsNull(w.RemovePiece(head, out why), "a fitted part comes off only by taking the item apart");
            List<CraftPiece> taken = w.RemovePiece(asm, out why);
            Assert.IsNotNull(taken, why);
            Assert.AreEqual(3, taken.Count, "the item comes out with its parts");
        }

        [Test]
        public void UnfinishedAssembly_MovesBetweenStationsWithItsParts()
        {
            CraftWorkshop a = Workshop(id: "bench");
            string asm = a.StartAssembly("belt", out string why);
            Assert.IsNotNull(asm, why);
            string buckle = a.AddPiece(CraftDefaults.FastenerSet(a.Rules, CraftFastenerKind.Buckle, 1, 30, Metal("Brass")));
            Assert.IsTrue(a.Attach(asm, "buckle", 0, buckle, out why), why);
            List<CraftPiece> carried = a.RemovePiece(asm, out why);
            Assert.IsNotNull(carried, why);
            Assert.AreEqual(2, carried.Count);

            // Another station with the same id already uses those piece ids.
            CraftWorkshop b = Workshop(id: "bench");
            Bar(b, "Mild steel");
            Bar(b, "Mild steel");
            Bar(b, "Mild steel");
            string moved = b.AddPiece(carried[0], carried.GetRange(1, 1));
            CraftPiece m = b.Piece(moved);
            Assert.IsNotNull(m);
            List<CraftPiece> parts = b.Components(m);
            Assert.AreEqual(1, parts.Count, "the buckle came with the belt");
            Assert.AreEqual(parts[0].id, m.assembly.PieceIn("buckle"), "and is still fitted to it");
            Assert.AreEqual(CraftLocation.Assembled, parts[0].location);
            Assert.AreEqual(5, b.Pieces.Count);
            CraftWorkshop replayed = CraftWorkshop.Replay(Rules(), b.Tools, b.Ambient, b.Seed, b.Id, b.History, b.Step);
            Assert.AreEqual(PiecesJson(b), PiecesJson(replayed));
        }

        [Test]
        public void Assembly_RefusesHotPartsNearWood()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Mild steel");
            LightForge(w);
            HeatTo(w, bar, 700);
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsFalse(w.Attach(asm, "head", 0, bar, out why));
            StringAssert.Contains("hot", why);
        }

        [Test]
        public void OverdrivenWedge_SplitsTheHaft()
        {
            CraftWorkshop w = Workshop(seed: 4);
            HatchetParts(w, out string head, out string haft);
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsTrue(w.Attach(asm, "head", 0, head, out why), why);
            Assert.IsTrue(w.Attach(asm, "haft", 0, haft, out why), why);
            Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 40, 1, 0, out why), why);
            Assert.Greater(w.Piece(haft).wood.split, 0, "driven too far, the wedge splits the haft");
        }

        [Test]
        public void Belt_IsMadeFromTannedLeatherAndABuckle()
        {
            CraftWorkshop w = Workshop();
            string hide = Flesh(w, w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2)));
            w.FillVat(0, "bark", 3, out _);
            w.MovePiece(hide, CraftLocation.Vat, out _, 1, 0, 9999, 0);
            Hours(w, 70);
            w.MovePiece(hide, CraftLocation.Rack, out _);
            Hours(w, 24);
            w.MovePiece(hide, CraftLocation.Air, out _);
            string strap = w.CutPattern(hide, "long_strap", 1, 3, 0, 0.9, out string why);
            Assert.IsNotNull(strap, why);
            string buckle = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 1, 30, Metal("Brass")));
            string asm = w.StartAssembly("belt", out why);
            Assert.IsFalse(w.Attach(asm, "strap", 0, hide, out why), "the rest of the hide was not cut to the belt pattern");
            Assert.IsTrue(w.Attach(asm, "strap", 0, strap, out why), why);
            Assert.IsTrue(w.Attach(asm, "buckle", 0, buckle, out why), why);
            CraftJointSpec joint = w.Rules.Design("belt").Joint("buckle");
            if (!w.Finish(asm, out why))
            {
                Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Punch, 4, 0.1, 0.1, out why), why);
                Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Stitch, 4, 0.6, 0.1, out why), why);
            }
            Assert.IsTrue(w.Finish(asm, out why), why + " " + joint?.kind);
            CraftItemReport item = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Assert.Greater(item.Defense, 0);
            Assert.Greater(item.Durability, 0);
            StringAssert.Contains("belt", item.Name.ToLowerInvariant());
        }

        // ------------------------------------------------------------------ evaluation
        [Test]
        public void BetterWork_MakesABetterItem()
        {
            CraftItemReport Hatchet(bool finish)
            {
                CraftWorkshop w = Workshop(seed: 4);
                HatchetParts(w, out string head, out string haft);
                if (finish)
                {
                    // The edge runs from where the design says it starts to the end of the head.
                    CraftPiece p = w.Piece(head);
                    int from = p.SegmentAt(w.Rules.Design("hatchet").Component("head").shape.edgeFrom), to = p.segments.Count - 1;
                    w.Descale(head, 0, to, "wire_brush", 0.6, 20, out _);
                    for (int k = 0; k < 30; k++)
                    {
                        GrindFor(w, head, from, to, CraftSurfaceSide.LeftBevel, "coarse_wheel", 15, 0.6, 1);
                        GrindFor(w, head, from, to, CraftSurfaceSide.RightBevel, "coarse_wheel", 15, 0.6, 1);
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        w.Sharpen(head, from, to, CraftSurfaceSide.LeftBevel, "medium_stone", 15, 0.5, 20, 0, true, out _);
                        w.Sharpen(head, from, to, CraftSurfaceSide.RightBevel, "medium_stone", 15, 0.5, 20, 0, true, out _);
                    }
                }
                string asm = w.StartAssembly("hatchet", out _);
                w.Attach(asm, "head", 0, head, out _);
                w.Attach(asm, "haft", 0, haft, out _);
                w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 7, 0.8, 0, out _);
                Assert.IsTrue(w.Finish(asm, out string why), why);
                return CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            }
            CraftItemReport rough = Hatchet(false), done = Hatchet(true);
            Assert.Greater(done.Edge, rough.Edge);
            Assert.Greater(done.Quality, rough.Quality);
            Assert.GreaterOrEqual(done.DamageMax, rough.DamageMax);
        }

        [Test]
        public void IncompleteItems_RateLow()
        {
            CraftWorkshop w = Workshop(seed: 4);
            HatchetParts(w, out string head, out _);
            string asm = w.StartAssembly("hatchet", out _);
            w.Attach(asm, "head", 0, head, out _);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Assert.Less(r.Quality, 0.3);
            Assert.IsTrue(r.Notes.Any(n => n.Contains("missing")));
        }

        // ------------------------------------------------------------------ observation and knowledge
        [Test]
        public void Looking_RevealsNoNumbersWithoutInstruments()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel");
            LightForge(w);
            HeatTo(w, bar, 820);
            var know = new CraftJournalData();
            List<string> bare = CraftLook.Describe(w.Rules, w.Piece(bar), know, CraftInstruments.None, true, false);
            Assert.IsFalse(bare.Any(l => l.Contains("°C")), string.Join("\n", bare));
            Assert.IsFalse(bare.Any(l => l.Contains("HV") || l.Contains("%")), string.Join("\n", bare));
            Assert.IsFalse(bare.Any(l => l.Contains("High-carbon") || l.Contains("high-carbon")), "it is not named before the player knows it");
            List<string> gauged = CraftLook.Describe(w.Rules, w.Piece(bar), know, CraftInstruments.Thermometer | CraftInstruments.Magnet, true, false);
            Assert.IsTrue(gauged.Any(l => l.Contains("°C")));
            Assert.IsTrue(gauged.Any(l => l.Contains("magnet")), "a magnet stops pulling at the hardening heat");
            List<string> revealed = CraftLook.Describe(w.Rules, w.Piece(bar), know, CraftInstruments.None, true, true);
            Assert.IsTrue(revealed.Any(l => l.Contains("HV")), "the test station shows the numbers");
        }

        [Test]
        public void Journal_LearnsMetalsByWorkingAndAssay()
        {
            CraftRules rules = Rules();
            var d = new CraftJournalData();
            CraftAlloyProfile steel = CraftDefaults.Bar(rules, 100, 20, 5, Metal("Tool steel")).Profile(rules);
            CraftAlloyProfile silver = CraftDefaults.Bar(rules, 100, 20, 5, Metal("Sterling silver")).Profile(rules);
            Assert.IsFalse(CraftJournal.KnowsClass(d, rules, steel.Class));
            for (int k = 0; k < rules.Settings.workingsToRecognise; k++) CraftJournal.Worked(d, rules, steel);
            Assert.IsTrue(CraftJournal.KnowsClass(d, rules, steel.Class));
            Assert.IsFalse(CraftJournal.KnowsClass(d, rules, silver.Class));
            Assert.IsNotNull(CraftJournal.Assayed(d, rules, silver));
            Assert.IsTrue(CraftJournal.KnowsClass(d, rules, silver.Class));
            CraftJournalData back = CraftJournalData.FromJson(d.ToJson());
            Assert.IsTrue(CraftJournal.KnowsClass(back, rules, steel.Class));
            Assert.AreEqual(d.ToJson(), back.ToJson());
        }

        [Test]
        public void Journal_LearnsFromWhatHappens()
        {
            CraftWorkshop w = Workshop(medium: "brine");
            var d = new CraftJournalData();
            var learnt = new List<string>();
            w.EventRaised += e =>
            {
                string id = CraftJournal.InsightOf(e, w.Piece(e.Piece));
                if (id != null && CraftJournal.Learn(d, id) != null) learnt.Add(id);
            };
            CrackedBar(w);
            Assert.IsTrue(CraftJournal.HasInsight(d, "quench_crack"), string.Join(", ", learnt));
            Assert.AreEqual(learnt.Distinct().Count(), learnt.Count, "an insight is learnt once");
        }

        // ------------------------------------------------------------------ hand-skill models
        [Test]
        public void Hammer_IsRepeatable_AndRewardsAFullSwing()
        {
            CraftBlow Swing(double hold, int seed, double fatigue = 0)
            {
                var hands = new CraftHands(seed) { fatigue = fatigue };
                var hammer = new CraftHammer();
                hammer.Tick(2);
                hammer.Raise();
                for (double t = 0; t < hold; t += 0.02) hammer.Tick(0.02);
                return hammer.Release(0.5, 0, hands);
            }
            Assert.AreEqual(Swing(0.9, 7).force, Swing(0.9, 7).force);
            Assert.AreEqual(Swing(0.9, 7).position, Swing(0.9, 7).position);
            Assert.Greater(Swing(0.9, 7).force, Swing(0.2, 7).force + 0.2);
            double Spread(double fatigue) => Enumerable.Range(0, 30).Select(s => Math.Abs(Swing(0.9, s, fatigue).position - 0.5)).Average();
            Assert.Greater(Spread(1), Spread(0), "tired hands miss more");

            var h = new CraftHands(1);
            var rushed = new CraftHammer();
            rushed.Raise();
            for (int k = 0; k < 45; k++) rushed.Tick(0.02);
            CraftBlow first = rushed.Release(0.5, 0, h);
            rushed.Raise();
            for (int k = 0; k < 10; k++) rushed.Tick(0.02);
            CraftBlow snatched = rushed.Release(0.5, 0, h);
            Assert.Greater(snatched.rushed, 0);
            Assert.Less(snatched.force, first.force);
        }

        [Test]
        public void TiredArm_StrikesWeaker_LessTrue_AndSlips()
        {
            List<CraftBlow> Blows(double fatigue)
            {
                var list = new List<CraftBlow>();
                for (int seed = 0; seed < 60; seed++)
                {
                    var hands = new CraftHands(seed) { fatigue = fatigue };
                    var hammer = new CraftHammer();
                    hammer.Tick(2);
                    hammer.Raise();
                    for (double t = 0; t < 0.9; t += 0.02) hammer.Tick(0.02);
                    list.Add(hammer.Release(0.5, 0, hands));
                }
                return list;
            }
            List<CraftBlow> fresh = Blows(0), spent = Blows(0.95);
            Assert.AreEqual(0, fresh.Count(b => b.slipped), "a fresh arm does not slip");
            Assert.Greater(spent.Count(b => b.slipped), 3, "a spent arm slips now and then");
            Assert.Greater(fresh.Average(b => b.force), spent.Where(b => !b.slipped).Average(b => b.force) + 0.25, "a tired arm cannot lift it as high");
            CraftBlow slip = spent.First(b => b.slipped);
            Assert.Greater(slip.miss, 0.04, "a slipped blow lands off its mark");
            Assert.Greater(Math.Abs(slip.tilt), 0.3, "and glances off at a tilt");
            Assert.IsNotEmpty(slip.Words);

            // It rests: slowly while it works, properly once it is still.
            var arm = new CraftHands(1) { fatigue = 0.8 };
            arm.Rest(1);
            double working = arm.fatigue;
            Assert.Greater(working, 0.79);
            arm.Rest(20);
            Assert.Less(arm.fatigue, 0.45, "twenty seconds of rest takes away about half");
            Assert.Greater(new CraftHands(1).Strength, new CraftHands(1) { fatigue = 0.9 }.Strength + 0.4);
        }

        [Test]
        public void Stroke_MeasuresWhatTheHandsDid()
        {
            var s = new CraftStroke();
            var hands = new CraftHands(2, 1);
            for (int pass = 0; pass < 3; pass++)
                for (int k = 0; k <= 20; k++)
                {
                    double x = pass % 2 == 0 ? 0.2 + 0.03 * k : 0.8 - 0.03 * k;
                    s.Add(pass + k * 0.05, x, 0.5 + 0.01 * k, 15, 0.5, hands);
                }
            Assert.AreEqual(3, s.Passes);
            Assert.AreEqual(15, s.Angle, 1.5);
            Assert.Less(s.Wobble, 2);
            s.Span(24, out int from, out int to);
            Assert.AreEqual(4, from);
            Assert.AreEqual(19, to);
            Assert.AreEqual(s.Samples * 2, s.Path(18, 13).Count);

            var shaky = new CraftStroke();
            var tired = new CraftHands(2) { fatigue = 1 };
            for (int k = 0; k <= 60; k++) shaky.Add(k * 0.05, k / 60.0, 0.5, 15, 0.5, tired);
            Assert.Greater(shaky.Wobble, s.Wobble);
        }

        [Test]
        public void Casting_TakesTheShapeOfTheDesignsPart_AndAPoorPourCosts()
        {
            CraftWorkshop Melted(out double melt)
            {
                CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
                string ingot = w.AddPiece(CraftDefaults.Bar(w.Rules, 300, 60, 30, Metal("Bronze")));
                Assert.IsTrue(w.MovePiece(ingot, CraftLocation.Melt, out string why), why);
                LightForge(w, 10);
                for (int t = 0; t < 5000 && w.Crucible.Mass < 1; t++) { if (t % 5 == 0) w.Bellows(0.8); w.RunSteps(1); }
                for (int t = 0; t < 200; t++) { if (t % 5 == 0) w.Bellows(0.5); w.RunSteps(1); }
                melt = w.Crucible.Mass;
                return w;
            }
            CraftWorkshop a = Melted(out double before);
            string id = CraftRules.PatternMoldId("hatchet", "head");
            CraftMold mold = a.Rules.Mold(id);
            Assert.IsNotNull(mold, "every castable metal part has a mould in its shape");
            Assert.IsTrue(a.Rules.AllMolds().Any(m => m.id == id));
            string head = a.Pour(id, 0.6, 0.9, 0, out string why);
            Assert.IsNotNull(head, why);
            CraftPiece cast = a.Piece(head);
            Assert.AreEqual("hatchet", cast.intentDesign, "what is cast in the part's mould is meant for that part");
            Assert.AreEqual("head", cast.intentRole);
            CraftShapeTarget shape = a.Rules.Design("hatchet").Component("head").shape;
            Assert.That(cast.Length, Is.InRange(shape.lengthMin * 0.95, shape.lengthMax * 1.05), "it comes out the part's length");
            shape.At(0.9f, out float w9, out _);
            Assert.AreEqual(w9, cast.segments[cast.SegmentAt(0.9)].width, w9 * 0.15, "and its outline");

            // A careless pour: metal on the floor, a mould that ran over, a mould left half full.
            CraftWorkshop b = Melted(out _);
            string sloppy = b.Pour(id, 0.6, 0.9, 0, out why, 0.5, 0.6);
            Assert.IsNotNull(sloppy, why);
            Assert.Less(b.Crucible.Mass, a.Crucible.Mass - b.Piece(sloppy).Mass(b.Rules) * 0.3, "what splashed on the sand is lost");
            Assert.Greater(b.Piece(sloppy).segments.Average(g => g.rough), cast.segments.Average(g => g.rough), "metal that ran over leaves flash to file off");
            CraftWorkshop c = Melted(out _);
            string half = c.Pour(id, 0.6, 0.9, 0, out why, 0, 0, 0.5);
            Assert.IsNotNull(half, why);
            Assert.Less(c.Piece(half).Length, cast.Length * 0.7, "a pour stopped halfway is a short casting");
            Assert.IsTrue(c.Piece(half).defects.Any(d => d.kind == CraftDefectKind.Misrun));
        }

        [Test]
        public void PourControl_SpillsOffTheMouth_AndRunsOverWhenFull()
        {
            var p = new CraftPourControl();
            for (int i = 0; i < 100; i++) p.Tick(0.05, 0.9, null, 4, i % 2 == 0);
            Assert.Greater(p.Spilled, 0.1, "the stream that misses the mouth splashes on the sand");
            for (int i = 0; i < 200; i++) p.Tick(0.05, 0.9, null);
            Assert.IsTrue(p.Full);
            Assert.Greater(p.Overflow, 0.1, "poured on after it is full, it runs over");
            Assert.Greater(p.SpillShare, 0.1);
        }

        [Test]
        public void Pour_CountsInterruptions()
        {
            var steady = new CraftPourControl();
            for (int k = 0; k < 400 && steady.Poured < 1; k++) steady.Tick(0.02, 0.7, null);
            Assert.AreEqual(0, steady.Interruptions);
            Assert.Greater(steady.Steadiness, 0.8);
            Assert.AreEqual(1, steady.Poured, 1e-9);

            var halting = new CraftPourControl();
            for (int k = 0; k < 600 && halting.Poured < 1; k++) halting.Tick(0.02, k / 40 % 2 == 0 ? 0.8 : 0, null);
            Assert.Greater(halting.Interruptions, 0, "stopping and starting the stream");
        }

        [Test]
        public void Rhythm_RewardsTiming()
        {
            var good = new CraftRhythm();
            var bad = new CraftRhythm();
            for (int k = 0; k < 10; k++)
            {
                good.Tick(good.interval);
                good.Answer(0.6, null);
                bad.Tick(bad.interval * (k % 2 == 0 ? 0.6 : 1.4));
                bad.Answer(0.6, null);
            }
            Assert.Less(good.MeanError, 0.05);
            Assert.Greater(bad.MeanError, good.MeanError);
            Assert.AreEqual(0.6, good.Pull, 1e-9);
        }

        // ------------------------------------------------------------------ the work of the hands
        [Test]
        public void Carving_ShapesWood_AndForcingItSplits()
        {
            CraftWorkshop w = Workshop();
            string blank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 140, 40, 40));
            double w0 = w.Piece(blank).segments[2].width;
            for (int k = 0; k < 4; k++)
                Assert.IsTrue(w.Carve(blank, 0, w.Piece(blank).segments.Count - 1, CraftSurface.CarveCut.Width, 1.0, 0.9, out string why), why);
            CraftPiece p = w.Piece(blank);
            Assert.Less(p.segments[2].width, w0 - 3, "steady strokes take the width down");
            Assert.AreEqual(40, p.segments[2].thickness, 1e-9, "a width stroke leaves the thickness");
            Assert.Less(p.wood.split, 0.01, "steady, moderate strokes do not split it");
            for (int k = 0; k < 6; k++)
                Assert.IsTrue(w.Carve(blank, 0, 3, CraftSurface.CarveCut.Round, 3, 0.2, out string why), why);
            Assert.Greater(w.Piece(blank).wood.split, 0.1, "forcing deep, shaky cuts splits it along the grain");
            Assert.IsTrue(w.Carve(blank, 0, 0, CraftSurface.CarveCut.Bore, 2, 0.8, out string bw), bw);
            Assert.Greater(w.Piece(blank).segments[0].hole, 1, "boring opens a slot for the tang");
            Assert.IsFalse(w.Carve(Bar(w, "Mild steel"), 0, 3, CraftSurface.CarveCut.Width, 1, 1, out _), "metal is not carved");
            Assert.IsNotNull(w.SplitPiece(blank, 0.8, out string sw), sw); // the saw needs no anvil for wood
        }

        [Test]
        public void Crucible_SolidPieceCanBeLiftedOut()
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            string cu = w.AddPiece(CraftDefaults.Bar(w.Rules, 60, 20, 10, ("copper", 1.0)));
            Assert.IsTrue(w.MovePiece(cu, CraftLocation.Melt, out string why), why);
            w.RunSteps(5);
            Assert.IsFalse(w.MovePiece(cu, CraftLocation.Fire, out _), "from the crucible only back onto the bench");
            Assert.IsTrue(w.MovePiece(cu, CraftLocation.Air, out why), "a piece that has not melted is lifted out with the tongs: " + why);
            Assert.AreEqual(CraftLocation.Air, w.Piece(cu).location);
        }

        [Test]
        public void ForgeWeld_SetPoorly_Delaminates()
        {
            double Delamination(double set)
            {
                CraftWorkshop w = Workshop();
                string bar = Bar(w, "Mild steel", 200, 30, 6);
                LightForge(w, 12);
                // To welding heat: deep in a hard-blown fire, fluxed, then folded straight out of it.
                CraftAlloyProfile prof = w.Piece(bar).Profile(w.Rules);
                double weld = prof.BurnTemperature - w.Rules.Settings.weldingMargin - 40;
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string mw, 1), mw);
                for (int t = 0; t < 8000 && w.Piece(bar).MeanTemperature < weld; t++)
                {
                    if (t % 3 == 0) w.Bellows(1);
                    if (t % 500 == 499) w.AddFuel("charcoal", 3, out _);
                    w.RunSteps(1);
                }
                Assert.IsTrue(w.AddFlux("borax", 2, 2, out string fw, bar, 1), fw);
                Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out mw), mw);
                Assert.Greater(CraftForging.WeldQuality(w.Rules, w.Piece(bar)), 0.3, "at welding heat");
                Assert.IsTrue(w.Fold(bar, out string why, set), why);
                return w.Piece(bar).defects.Where(d => d.kind == CraftDefectKind.Delamination).Sum(d => d.severity);
            }
            Assert.Greater(Delamination(0.1), Delamination(1) + 0.1, "a weld the hammer barely set does not take");
        }

        [Test]
        public void HandSpreadFlux_AndSieving_CountTheHands()
        {
            CraftWorkshop w = Workshop();
            string a = Bar(w, "Mild steel"), b = Bar(w, "Mild steel");
            LightForge(w, 10);
            HeatTo(w, a, 800);
            Assert.IsTrue(w.AddFlux("borax", 1, 2, out string fa, a, 1), fa);
            double fluxA = w.Piece(a).flux; // it burns off while the other bar heats
            HeatTo(w, b, 800);
            Assert.IsTrue(w.AddFlux("borax", 1, 2, out string fb, b, 0.3), fb);
            Assert.Greater(fluxA, w.Piece(b).flux + 0.2, "flux sprinkled over a third of it does a third of the work");

            string lazy = w.AddPiece(CraftOres.NewLump(w.Rules, "bog_iron", 9, 0)), thorough = w.AddPiece(CraftOres.NewLump(w.Rules, "bog_iron", 9, 0));
            foreach (string id in new[] { lazy, thorough })
                for (int f = w.Piece(id).ore.fragments.Count - 1; f >= 0; f--) w.CrushOre(id, f, 1.2, out _);
            double mesh = w.Piece(thorough).ore.fragments.Max(f => f.size) * 0.7;
            string fl = w.SieveOre(lazy, mesh, out _, 0.1), ft = w.SieveOre(thorough, mesh, out _, 1);
            int Count(string id) => id != null ? w.Piece(id).ore.fragments.Count : 0;
            Assert.GreaterOrEqual(Count(ft), Count(fl), "a thorough shaking lets more of the small fragments through");
        }

        [Test]
        public void Brushing_CleansAStone_AndTheWireBrushTakesTheRind()
        {
            CraftWorkshop w = Workshop();
            OreFragment Stone(string ore, CraftOreBrush brush, double cover, out OreFragment before)
            {
                string id = w.AddPiece(CraftOres.NewLump(w.Rules, ore, 4, 0));
                before = w.Piece(id).ore.fragments[0].Clone();
                Assert.IsTrue(w.BrushOre(id, 0, brush, cover, out string why), why);
                return w.Piece(id).ore.fragments[0];
            }
            double Dirt(OreFragment f) => f.mass * f.dirt;
            double Rock(OreFragment f) => f.mass * f.gangue;
            double Mineral(OreFragment f) => f.mass * f.mineral;

            OreFragment soft = Stone("hematite", CraftOreBrush.Soft, 1, out OreFragment s0);
            Assert.Less(Dirt(soft), Dirt(s0) * 0.5, "a soft brush takes the loose dirt");
            Assert.AreEqual(Rock(s0), Rock(soft), 1e-9, "but leaves the rock");
            Assert.AreEqual(Mineral(s0), Mineral(soft), 1e-9, "and loses no ore (nothing washes away)");

            OreFragment wire = Stone("hematite", CraftOreBrush.Wire, 1, out OreFragment w0);
            Assert.Less(Rock(wire), Rock(w0) * 0.97, "a wire brush knocks the weathered rind off");
            Assert.Less(Mineral(wire), Mineral(w0), "and scratches a little ore away with it");
            Assert.Greater(wire.mineral, w0.mineral, "the stone is richer for it");

            OreFragment clayWithSoft = Stone("bog_iron", CraftOreBrush.Soft, 1, out OreFragment c0);
            OreFragment clayWithStiff = Stone("bog_iron", CraftOreBrush.Stiff, 1, out OreFragment c1);
            Assert.Greater(Dirt(clayWithSoft) / Dirt(c0), Dirt(clayWithStiff) / Dirt(c1) + 0.3, "caked clay needs a stiff brush");

            OreFragment half = Stone("hematite", CraftOreBrush.Stiff, 0.3, out OreFragment h0);
            OreFragment full = Stone("hematite", CraftOreBrush.Stiff, 1, out OreFragment h1);
            Assert.Greater(Dirt(half) / Dirt(h0), Dirt(full) / Dirt(h1), "the brush cleans what it went over");
            Assert.IsFalse(w.BrushOre("nope", 0, CraftOreBrush.Soft, 1, out _));
        }

        // ------------------------------------------------------------------ the woodshop
        [Test]
        public void Riving_AlongTheGrain_GivesStraightStrongStock()
        {
            CraftWorkshop w = Workshop();
            string good = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 40, 40)), bad = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 40, 40));
            string goodHalf = w.Rive(good, 0.05, 0.9, out string why);
            Assert.IsNotNull(goodHalf, why);
            string badHalf = w.Rive(bad, 0.7, 0.9, out why);
            Assert.IsNotNull(badHalf, why);
            Assert.Less(w.Piece(good).wood.runout, 0.2, "following the grain leaves it straighter-grained than sawn stock");
            Assert.Greater(w.Piece(bad).wood.runout, 0.45, "a split that wandered cuts across the fibres");
            Assert.Less(w.Piece(badHalf).Length, w.Piece(goodHalf).Length - 50, "a split that ran out leaves a short, tapering half");
            Assert.AreEqual(19, w.Piece(good).segments[0].width, 0.5, "each half is half as wide");
            Assert.Greater(CraftEvaluator.Piece(w.Rules, w.Piece(good)).WoodToughness, CraftEvaluator.Piece(w.Rules, w.Piece(bad)).WoodToughness + 0.1);
            Assert.IsNull(w.Rive(w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 14, 14)), 0, 1, out why), "too narrow to rive again");
        }

        [Test]
        public void SteamBending_BendsHotWood_AndCrackesColdOrForcedWood()
        {
            CraftWorkshop w = Workshop();
            string Steamed()
            {
                string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 500, 30, 12));
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Steam, out string why), why);
                for (int t = 0; t < 20000 && w.Piece(id).wood.steamed < 1; t++) w.RunSteps(1);
                Assert.GreaterOrEqual(w.Piece(id).wood.steamed, 1, "it steams through in time");
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
                Assert.Greater(CraftWoodwork.Plasticity(w.Piece(id)), 0.6, "out of the steam it is soft");
                return id;
            }
            double Bent(string id) => w.Piece(id).segments.Sum(g => g.bend);
            bool Cracked(string id) => w.Piece(id).defects.Any(d => d.kind == CraftDefectKind.Crack);

            string cold = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 500, 30, 12));
            Assert.IsTrue(w.BendWood(cold, 2, 6, 90, 0.3, true, out string why2), why2);
            Assert.IsTrue(Cracked(cold), "bent cold, the outside of the bend tears");

            string slow = Steamed();
            Assert.IsTrue(w.BendWood(slow, 2, 6, 90, 0.2, true, out why2), why2);
            Assert.IsFalse(Cracked(slow), "steamed through and bent steadily over the form with the strap, it takes the bend");
            Assert.Greater(Bent(slow), 70, "and keeps most of it");
            Assert.Greater(Bent(slow), Bent(cold) + 15);

            string rushed = Steamed();
            Assert.IsTrue(w.BendWood(rushed, 2, 6, 120, 1, false, out why2), why2);
            Assert.IsTrue(Cracked(rushed), "forced round at once without the strap, it tears");
            w.RunSteps(3000);
            Assert.Less(CraftWoodwork.Plasticity(w.Piece(slow)), 0.2, "cooled, it sets");
        }

        [Test]
        public void Seasoning_TooFastChecksTheWood()
        {
            CraftWorkshop w = Workshop();
            string Green()
            {
                string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "oak", 600, 60, 40));
                w.Piece(id).wood.moisture = 0.35;
                return id;
            }
            string careful = Green(), hasty = Green();
            Assert.IsTrue(w.StackWood(careful, 1, 1, 1, out string why), why);
            Assert.IsTrue(w.StackWood(hasty, 0.2, 0, 0, out why), why);
            Assert.AreEqual(CraftLocation.Stack, w.Piece(careful).location);
            w.RunSteps(6000);
            Assert.Less(w.Piece(careful).wood.moisture, 0.25, "it dries in the stack");
            Assert.Less(w.Piece(hasty).wood.moisture, w.Piece(careful).wood.moisture, "in the open sun, faster");
            Assert.Greater(w.Piece(hasty).wood.checks, w.Piece(careful).wood.checks + 0.03, "and dried too fast, unsealed, it checks");
            Assert.Greater(w.Piece(hasty).segments.Sum(g => Math.Abs(g.bend)), w.Piece(careful).segments.Sum(g => Math.Abs(g.bend)), "unevenly stickered, it warps");
        }

        [Test]
        public void Varnish_TakesOnDrySandedWood()
        {
            CraftWorkshop w = Workshop();
            string Blank() => w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30));
            string sanded = Blank(), rough = Blank(), damp = Blank();
            int n = w.Piece(sanded).segments.Count - 1;
            Assert.IsTrue(w.SandWood(sanded, 0, n, 0.4, 10, 1, out string why), why);
            Assert.IsTrue(w.SandWood(sanded, 0, n, 0.9, 10, 1, out why), why);
            Assert.Less(w.Piece(sanded).segments.Average(g => g.rough), 20, "sanded fine along the grain it is smooth");
            w.Piece(damp).wood.moisture = 0.3;
            foreach (string id in new[] { sanded, rough, damp })
                Assert.IsTrue(w.Varnish(id, "spirit_varnish", 0, n, 0.9, 0.5, out why), why);
            Assert.Greater(w.Piece(sanded).wood.finishEven, w.Piece(rough).wood.finishEven + 0.15, "over a rough surface it is blotchy");
            Assert.Less(w.Piece(damp).wood.finish, w.Piece(sanded).wood.finish * 0.6, "on damp wood it does not take");
            Assert.Greater(CraftWoodwork.FinishProtection(w.Piece(sanded).wood), CraftWoodwork.FinishProtection(w.Piece(rough).wood));
            Assert.IsFalse(w.Varnish(sanded, "borax", 0, n, 1, 0.5, out _), "only a varnish or an oil");
        }

        // ------------------------------------------------------------------ the tailor's
        [Test]
        public void Spinning_EvenDraftGivesFineThread()
        {
            CraftRules r = Rules();
            CraftSupplyDefinition wool = r.Supply("wool_fibre");
            Assert.IsNotNull(wool);
            Assert.AreEqual("fine_wool_yarn", CraftTextiles.Spun(r, wool, 0.95, 0.5));
            Assert.AreEqual("wool_yarn", CraftTextiles.Spun(r, wool, 0.7, 0.45));
            Assert.AreEqual("coarse_wool_yarn", CraftTextiles.Spun(r, wool, 0.9, 0.05), "under-twisted, it is coarse whatever the draft");
            Assert.AreEqual("coarse_wool_yarn", CraftTextiles.Spun(r, wool, 0.3, 0.5));
            Assert.Greater(r.Supply("fine_wool_yarn").strength, r.Supply("coarse_wool_yarn").strength);
        }

        [Test]
        public void Cloth_IsCutOnItsGrain_AndSewnIntoAGambeson()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "canvas", 3));
            Assert.IsTrue(CraftTextiles.IsCloth(w.Rules, w.Piece(bolt)));
            Assert.IsTrue(w.Piece(bolt).hide.cells.All(c => c.present), "a bolt is a straight-edged rectangle");
            Assert.IsFalse(w.Scrape(bolt, new List<float> { 1, 1, 5, 1 }, false, 0.5, 0.5, out _), "cloth needs no fleshing");
            Assert.IsFalse(w.MovePiece(bolt, CraftLocation.Vat, out _), "nor tanning");
            string a = w.CutPattern(bolt, "gambeson_panel", 0, 0, 0, 0.95, out string why);
            Assert.IsNotNull(a, why);
            string b = w.CutPattern(bolt, "gambeson_panel", 0, 8, 1, 0.95, out why);
            Assert.IsNotNull(b, why);
            CraftPieceReport onGrain = CraftEvaluator.Piece(w.Rules, w.Piece(a)), across = CraftEvaluator.Piece(w.Rules, w.Piece(b));
            Assert.Greater(onGrain.Strength, across.Strength, "cut across the grain it is weaker");
            Assert.IsTrue(across.Defects.Contains("cut across the grain"));

            string asm = w.StartAssembly("padded_gambeson", out why);
            Assert.IsNotNull(asm, why);
            string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 4));
            Assert.IsFalse(w.Attach(asm, "shell", 0, hide, out why), "the shell is cut from cloth, not a hide");
            Assert.IsTrue(w.Attach(asm, "shell", 0, a, out why), why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 2, 1.25), why);
            Assert.AreEqual(1.25, w.Piece(asm).assembly.Supply("thread" + CraftWorkshop.StrengthSuffix), 1e-6, "the thread's strength goes in with it");
        }

        [Test]
        public void Clay_IsPaintedPerSegment()
        {
            CraftWorkshop w = Workshop();
            string bar = Bar(w, "Tool steel", 200, 30, 5);
            int n = w.Piece(bar).segments.Count;
            var painted = new List<float>();
            for (int i = 0; i < n; i++) painted.Add(i < n / 2 ? 3f : 0f);
            Assert.IsTrue(w.ApplyClay(bar, 0, n - 1, 3, out string why, painted), why);
            Assert.AreEqual(3, w.Piece(bar).segments[0].clay, 1e-6, "where it was painted");
            Assert.AreEqual(0, w.Piece(bar).segments[n - 1].clay, 1e-6, "bare where it was not");
        }
    }
}
#endif
