#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the production chains added to woodworking, textiles, tailoring and jewellery: each process
    /// leaves its mark on what the next one gets, and the whole chain replays exactly.
    /// </summary>
    public class CraftProcessChainTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "chain");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        // ------------------------------------------------------------------ woodworking
        [Test]
        public void Log_DebarkedCarefully_KeepsItsWood()
        {
            CraftWorkshop w = Workshop();
            string careful = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 200));
            string hasty = w.AddPiece(CraftDefaults.Log(w.Rules, "oak", 900, 200));
            int n = w.Piece(careful).segments.Count - 1;
            Assert.Greater(CraftWoodwork.BarkLeft(w.Piece(careful)), 0.9, "a log comes in its bark");
            double width = w.Piece(careful).segments.Average(s => s.width);
            Assert.IsTrue(w.DebarkWood(careful, 0, n, 0.35, 0.95, 0.9, out string why), why);
            Assert.IsTrue(w.DebarkWood(hasty, 0, n, 0.95, 0.3, 0.3, out why), why);
            Assert.Less(CraftWoodwork.BarkLeft(w.Piece(careful)), 0.2, "the drawknife takes the bark off");
            Assert.Greater(w.Piece(hasty).wood.gouged, w.Piece(careful).wood.gouged + 0.05, "dug in hard and unsteady, it gouges the wood");
            Assert.Less(w.Piece(hasty).segments.Average(s => s.width), w.Piece(careful).segments.Average(s => s.width), "and costs the usable width");
            Assert.LessOrEqual(w.Piece(careful).segments.Average(s => s.width), width + 1e-6);
            Assert.IsFalse(Workshop(CraftTools.Sawhorse).DebarkWood(careful, 0, n, 0.5, 0.5, 0.5, out _), "it needs the shaving horse");
        }

        [Test]
        public void Log_SplitThroughTheHeart_GivesEvenStraightHalves()
        {
            CraftWorkshop w = Workshop();
            string good = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 220));
            string bad = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 220));
            string goodHalf = w.SplitLog(good, 0.02, 0.95, 0.9, 6, out string why);
            Assert.IsNotNull(goodHalf, why);
            string badHalf = w.SplitLog(bad, 0.8, 0.2, 0.3, 6, out why);
            Assert.IsNotNull(badHalf, why);
            double Share(string a, string b) => w.Piece(a).segments.Average(s => s.width) / (w.Piece(a).segments.Average(s => s.width) + w.Piece(b).segments.Average(s => s.width));
            Assert.AreEqual(0.5, Share(good, goodHalf), 0.05, "split through the heart, the halves are even");
            Assert.Greater(Math.Abs(Share(bad, badHalf) - 0.5), 0.15, "off the heart, one half is much bigger");
            Assert.Greater(w.Piece(bad).wood.runout, w.Piece(good).wood.runout, "across the rays the split runs out of the grain");
            Assert.IsFalse(w.Piece(goodHalf).wood.log, "a half is no longer a round log");
            Assert.IsNull(w.SplitLog(w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30)), 0, 1, 1, 3, out _), "a blank is riven, not split");
        }

        [Test]
        public void Seasoning_FollowsTheAirsHumidity()
        {
            CraftWorkshop dry = Workshop(), damp = Workshop();
            dry.SetVariable("humidity", 0.2);
            damp.SetVariable("humidity", 0.9);
            string a = dry.AddPiece(CraftDefaults.WoodBlank(dry.Rules, "oak", 600, 60, 40));
            string b = damp.AddPiece(CraftDefaults.WoodBlank(damp.Rules, "oak", 600, 60, 40));
            dry.Piece(a).wood.moisture = damp.Piece(b).wood.moisture = 0.4;
            Assert.IsTrue(dry.StackWood(a, 1, 1, 1, out string why), why);
            Assert.IsTrue(damp.StackWood(b, 1, 1, 1, out why), why);
            dry.RunSteps(8000);
            damp.RunSteps(8000);
            Assert.Less(dry.Piece(a).wood.moisture, damp.Piece(b).wood.moisture - 0.04, "in dry air it seasons further");
            Assert.Greater(damp.Piece(b).wood.moisture, CraftWoodwork.Equilibrium(0.9) - 0.02, "it never dries below what the air holds");
        }

        [Test]
        public void Sanding_ThroughTheGrits_AndAResinSeal_LastLongest()
        {
            CraftWorkshop w = Workshop();
            string Blank() => w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 300, 30, 30));
            string stepped = Blank(), skipped = Blank(), waxed = Blank();
            int n = w.Piece(stepped).segments.Count - 1;
            foreach (double grit in new[] { 0.2, 0.45, 0.7, 0.9 })
                Assert.IsTrue(w.SandWood(stepped, 0, n, grit, 8, 1, out string why), why);
            Assert.IsTrue(w.SandWood(skipped, 0, n, 0.2, 8, 1, out _));
            Assert.IsTrue(w.SandWood(skipped, 0, n, 0.9, 16, 1, out _));
            Assert.Less(w.Piece(stepped).segments.Average(s => s.rough), w.Piece(skipped).segments.Average(s => s.rough), "skipping grits leaves the coarse scratches");
            Assert.IsTrue(w.SandWood(waxed, 0, n, 0.2, 8, 1, out _));
            Assert.IsTrue(w.SandWood(waxed, 0, n, 0.9, 16, 1, out _));
            Assert.IsTrue(w.Varnish(stepped, "pine_resin", 0, n, 0.9, 0.5, out string reason), reason);
            Assert.IsTrue(w.Varnish(waxed, "beeswax", 0, n, 0.9, 0.5, out reason), reason);
            Assert.Greater(w.Piece(stepped).wood.resin, 0.2, "a resin sealer builds a hard film");
            Assert.Greater(w.Piece(waxed).wood.wax, 0.2, "wax is rubbed in");
            Assert.Greater(CraftWoodwork.FinishWear(w.Piece(stepped).wood), CraftWoodwork.FinishWear(w.Piece(waxed).wood) + 0.1, "resin wears far better than wax");
        }

        // ------------------------------------------------------------------ fibre
        private static string Fleece(CraftWorkshop w, string fibre = "wool_fibre") => w.AddPiece(CraftFibre.NewFibre(w.Rules, fibre));

        [Test]
        public void Scouring_HotAndChurned_FeltsTheWool()
        {
            CraftWorkshop w = Workshop();
            string gentle = Fleece(w), rough = Fleece(w), cold = Fleece(w);
            Assert.Greater(w.Piece(gentle).fibre.grease, 0.3, "fleece comes greasy");
            Assert.IsTrue(w.ScourFibre(gentle, 62, 30, 0.1, 3, "soap", 3, out string why), why);
            Assert.IsTrue(w.ScourFibre(rough, 75, 30, 0.9, 40, "soap", 3, out why), why);
            Assert.IsTrue(w.ScourFibre(cold, 20, 30, 0.1, 0, "soap", 3, out why), why);
            Assert.Less(w.Piece(gentle).fibre.grease, 0.12, "hot water and soap lift the grease");
            Assert.Greater(w.Piece(cold).fibre.grease, 0.35, "cold water does not");
            Assert.Less(w.Piece(gentle).fibre.felted, 0.1);
            Assert.Greater(w.Piece(rough).fibre.felted, 0.4, "churned in hot water and shocked cold, it felts");
            Assert.IsFalse(w.ScourFibre(gentle, 60, 10, 0, 0, "beeswax", 1, out _), "only a detergent scours");
            Assert.IsFalse(w.CardFibre(gentle, 10, 0.3, 0.9, out _), "it must dry before it is carded");
        }

        [Test]
        public void PreparedFibre_SpinsABetterYarn_AndPlyingStrengthensIt()
        {
            CraftWorkshop w = Workshop();
            string prepared = Fleece(w), raw = Fleece(w);
            Assert.IsTrue(w.CleanFibre(prepared, 0.9, 0.05, out string why), why);
            Assert.IsTrue(w.ScourFibre(prepared, 62, 30, 0.1, 3, "soap", 3, out why), why);
            w.RunSteps(6000);
            Assert.Less(w.Piece(prepared).fibre.wet, 0.3, "it dries on the rack");
            Assert.IsTrue(w.OpenFibre(prepared, 0.9, 0.2, out why), why);
            Assert.IsTrue(w.CardFibre(prepared, 12, 0.3, 0.9, out why), why);
            Assert.IsTrue(w.CombFibre(prepared, 4, 0.9, out why), why);
            Assert.Greater(w.Piece(prepared).fibre.alignment, 0.85, "combed straight");
            Assert.Greater(CraftFibre.Prep(w.Piece(prepared).fibre), CraftFibre.Prep(w.Piece(raw).fibre) + 0.3);

            string good = w.SpinFibre(prepared, 0.85, 0.5, 0.6, 30, out why);
            Assert.IsNotNull(good, why);
            string lumpy = w.SpinFibre(raw, 0.85, 0.5, 0.6, 30, out why);
            Assert.IsNotNull(lumpy, why);
            Assert.IsTrue(w.Piece(good).yarn.worsted, "spun from combed top: a worsted");
            Assert.Greater(CraftFibre.Quality(w.Piece(good).yarn), CraftFibre.Quality(w.Piece(lumpy).yarn) + 0.15, "the same hands spin better yarn from prepared fibre");
            Assert.Greater(w.Piece(good).yarn.strength, w.Piece(lumpy).yarn.strength);

            string second = w.SpinFibre(prepared, 0.85, 0.5, 0.6, 30, out why);
            Assert.IsNotNull(second, why);
            double singles = w.Piece(good).yarn.strength;
            Assert.IsTrue(w.PlyYarn(good, second, null, 0.4, 0.9, out why), why);
            Assert.AreEqual(2, w.Piece(good).yarn.plies);
            Assert.Greater(w.Piece(good).yarn.strength, singles * 1.6, "two plies hold more than one");
            Assert.Greater(w.Piece(good).yarn.balance, 0.75, "plied at the right twist, it hangs straight");

            Assert.AreEqual(0, CraftFibre.ThreadCount(w.Piece(good)), "an unmeasured skein reels nothing");
            double length = w.Piece(good).yarn.length;
            Assert.IsTrue(w.MeasureYarn(good, length + 50, out why), why);
            Assert.AreEqual((int)(length / CraftFibre.MetresPerThread), CraftFibre.ThreadCount(w.Piece(good)), "an over-count never gives more than is there");
            Assert.IsNotNull(CraftFibre.ThreadOf(w.Rules, w.Piece(good)));
        }

        [Test]
        public void ShortStaple_IsCarded_NotCombed()
        {
            CraftWorkshop w = Workshop();
            string cotton = Fleece(w, "cotton_fibre");
            Assert.IsFalse(w.CombFibre(cotton, 3, 0.9, out string why));
            StringAssert.Contains("comb", why);
            Assert.IsTrue(w.CardFibre(cotton, 10, 0.3, 0.9, out why), why);
            Assert.IsFalse(Workshop(CraftTools.TailorTable).CardFibre(cotton, 10, 0.3, 0.9, out _), "it needs the fibre bench");
        }

        // ------------------------------------------------------------------ tailoring
        [Test]
        public void Layout_RefusesOverlaps_AndCuttingOnTheChalkIsCleaner()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "canvas", 3));
            Assert.IsTrue(w.LayoutPattern(bolt, "gambeson_panel", 0, 0, 0, false, out string why), why);
            Assert.IsFalse(w.LayoutPattern(bolt, "gambeson_panel", 1, 1, 0, false, out _), "patterns may not overlap");
            Assert.Greater(CraftTextiles.Utilization(w.Rules, w.Piece(bolt).hide), 0.05);
            string chalked = w.CutPattern(bolt, "gambeson_panel", 0, 0, 0, 0.8, out why);
            Assert.IsNotNull(chalked, why);
            Assert.AreEqual(0.85, w.Piece(chalked).hide.cutQuality, 1e-6, "cut along the chalk line");
            Assert.AreEqual(0, w.Piece(bolt).hide.layout.Count, "its chalk is cut away with it");
        }

        [Test]
        public void MeasuredGarments_FitBetter_AndThreadMustSuitTheMaterial()
        {
            CraftDesign d = Rules().Design("padded_gambeson");
            Assert.IsNotNull(d);
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.MeasureFit(null, 0.95, 980, out string why), why);
            string measured = w.StartAssembly("padded_gambeson", out why);
            string guessed = w.StartAssembly("padded_gambeson", out why);
            Assert.IsTrue(w.Piece(measured).assembly.measured, "the next item takes the measurements");
            Assert.IsFalse(w.Piece(guessed).assembly.measured, "and only that one");
            Assert.AreEqual(1, CraftTextiles.ThreadSuits(0.8, 1.0), 0.05, "linen suits cloth");
            Assert.Less(CraftTextiles.ThreadSuits(4, 0.6), 0.5, "coarse wool yarn is too weak for heavy leather");
            Assert.Less(CraftTextiles.ThreadSuits(0.5, 3), 0.95, "a heavy cord puckers fine cloth");
        }

        // ------------------------------------------------------------------ lapidary
        private static string Rough(CraftWorkshop w, string gem, int seed = 7, double carats = 0) => w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, seed, carats));

        [Test]
        public void Identification_ReadsTheStonesProperties()
        {
            CraftWorkshop w = Workshop();
            string ruby = Rough(w, "ruby");
            Assert.IsFalse(w.IdentifyGem(ruby, "ruby", out _), "it must be tested first");
            Assert.IsTrue(w.TestGem(ruby, CraftGemTest.Refractometer, 0.9, 0, out string why), why);
            Assert.IsTrue(w.TestGem(ruby, CraftGemTest.Gravity, 0.9, 0, out why), why);
            Assert.IsTrue(w.TestGem(ruby, CraftGemTest.Scratch, 0.9, 8, out why), why);
            GemState g = w.Piece(ruby).gem;
            Assert.AreEqual(1.77, g.readRI, 0.08, "the refractometer reads near its index");
            Assert.AreEqual(4.0, g.readSG, 0.4, "the balance near its gravity");
            Assert.AreEqual(8, g.hardLo, 1e-6, "quartz-hard points do not scratch corundum");
            Assert.IsTrue(w.IdentifyGem(ruby, "garnet", out why), why);
            Assert.IsFalse(w.Piece(ruby).gem.identified, "a wrong name sticks, but it is wrong");
            Assert.IsTrue(w.IdentifyGem(ruby, "ruby", out why), why);
            Assert.IsTrue(w.Piece(ruby).gem.identified);
        }

        [Test]
        public void Evaluation_FindsWhatTheLoupeSweepsOver()
        {
            CraftWorkshop w = Workshop();
            string id = null;
            for (int seed = 1; seed < 50 && id == null; seed++)
            {
                CraftPiece p = CraftLapidary.NewRough(w.Rules, "emerald", seed);
                if (p.gem.inclusions.Count >= 3) id = w.AddPiece(p);
            }
            Assert.IsNotNull(id, "an emerald is always included");
            // cleaned first: crust on the skin hides what lies beneath it
            for (int k = 0; k < 2; k++)
                Assert.IsTrue(w.CleanGem(id, false, Enumerable.Repeat(3f, CraftLapidary.CrustSectors).ToList(), 1, out string cleaned), cleaned);
            var looks = new List<float>();
            for (double z = 0.05; z < 1; z += 0.12)
                for (double y = -0.9; y <= 0.9; y += 0.15)
                    for (double x = -0.9; x <= 0.9; x += 0.15)
                        looks.AddRange(new[] { (float)x, (float)y, (float)z });
            string half = w.AddPiece(w.Piece(id).Clone());
            Assert.IsTrue(w.EvaluateGem(id, looks, 0.9, out string why), why);
            Assert.IsTrue(w.Piece(id).gem.inclusions.All(i => i.found), "a thorough sweep at every depth finds everything");
            Assert.IsTrue(w.EvaluateGem(half, looks.Take(looks.Count / 8).ToList(), 0.9, out why), why);
            Assert.Less(w.Piece(half).gem.inclusions.Count(i => i.found), w.Piece(half).gem.inclusions.Count, "a glance does not");
            Assert.Greater(w.Piece(half).gem.estimate, w.Piece(id).gem.estimate, "and over-values the stone");
        }

        [Test]
        public void Cutting_AtTheRightAngles_GivesABrilliantStone()
        {
            CraftWorkshop w = Workshop();
            string good = Rough(w, "rock_crystal", 3, 8), bad = Rough(w, "rock_crystal", 3, 8);
            foreach (string id in new[] { good, bad })
            {
                Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
                Assert.IsFalse(w.FacetGem(id, 0, 8, 43, 0, 0, 1, out _), "the excess rough must be sawn off first");
                for (int k = 0; k < 4; k++)
                    Assert.IsTrue(w.SawGem(id, k * 45, 0.66, 0.9, 1, 0.3, out why), why);
                Assert.AreEqual(CraftGemStage.Preformed, w.Piece(id).gem.stage);
            }
            Assert.Greater(w.Pieces.Count(p => p.kind == CraftPieceKind.Gem), 2, "big offcuts are stones of their own");
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                double ideal = CraftLapidary.IdealAngle(cut.tiers[t], n);
                Assert.IsTrue(w.FacetGem(good, t, cut.tiers[t].facets, ideal, 0.05, 0.05, 1, out string why), why);
                Assert.IsTrue(w.FacetGem(bad, t, cut.tiers[t].facets, cut.tiers[t].kind == CraftCutTierKind.Pavilion ? ideal - 7 : ideal + 5, 0.4, -0.6, 1, out why), why);
            }
            Assert.IsFalse(w.FacetGem(good, 99, 8, 40, 0, 0, 1, out _));
            foreach (string id in new[] { good, bad })
                Assert.IsTrue(w.PolishGem(id, "cerium_oxide", 12, 0.4, 0.5, out string why), why);
            CraftGemGrade g = CraftLapidary.Grade(w.Rules, w.Piece(good)), b = CraftLapidary.Grade(w.Rules, w.Piece(bad));
            Assert.AreEqual(CraftGemStage.Polished, w.Piece(good).gem.stage);
            Assert.Greater(g.Cut, b.Cut + 0.3);
            Assert.Less(b.Brilliance, 0.8, "a shallow pavilion leaks the light");
            Assert.Greater(g.Value, b.Value * 1.5);
            Assert.Greater(w.Piece(good).gem.girdle, 3, "the girdle is measured once it is cut");
        }

        [Test]
        public void Polish_NeedsACompoundAsHardAsTheStone_AndCleavageMustBeRespected()
        {
            CraftWorkshop w = Workshop();
            CraftPiece Faceted(string gem, double tableAngle)
            {
                string id = Rough(w, gem, 11, 6);
                w.Piece(id).gem.inclusions.Clear();
                Assert.IsTrue(w.OrientGem(id, "emerald_cut", tableAngle, 0, 0, 0.75, out string why), why);
                CraftGemCut cut = w.Rules.Cut("emerald_cut");
                double n = w.Rules.Gem(gem).refractiveIndex;
                for (int t = 0; t < cut.tiers.Count; t++)
                    Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, 0, 1, out why), why);
                return w.Piece(id);
            }
            CraftPiece soft = Faceted("ruby", 0), hard = Faceted("ruby", 0);
            Assert.IsTrue(w.PolishGem(soft.id, "tripoli", 10, 0.4, 0.5, out string reason), reason);
            Assert.IsTrue(w.PolishGem(hard.id, "alumina", 10, 0.4, 0.5, out reason), reason);
            Assert.Greater(hard.gem.polish, soft.gem.polish + 0.3, "tripoli hardly touches corundum");

            CraftPiece topaz = w.Pieces.First(p => p.kind == CraftPieceKind.Gem && p.gem.gem == "ruby");
            CraftPiece along = Faceted("topaz", 0);
            along.gem.tableAngle = along.gem.cleaveTilt; // the table laid along the cleavage
            CraftPiece across = Faceted("topaz", 0);
            across.gem.tableAngle = (across.gem.cleaveTilt + 45) % 90;
            Assert.IsTrue(w.PolishGem(along.id, "alumina", 10, 0.4, 0.5, out reason), reason);
            Assert.IsTrue(w.PolishGem(across.id, "alumina", 10, 0.4, 0.5, out reason), reason);
            Assert.IsTrue(along.HasDefect(CraftDefectKind.Chip), "a table along topaz's cleavage flakes");
            Assert.IsFalse(across.HasDefect(CraftDefectKind.Chip));
            Assert.IsNotNull(topaz);
        }

        [Test]
        public void Cleaving_AlongThePlane_PartsCleanly()
        {
            CraftWorkshop w = Workshop();
            string clean = Rough(w, "topaz", 5, 10), shattered = Rough(w, "topaz", 5, 10);
            double trace = w.Piece(clean).gem.cleaveTrace;
            Assert.IsTrue(w.CleaveGem(clean, trace, 0.5, 0.6, out string why), why);
            Assert.IsTrue(w.CleaveGem(shattered, trace + 25, 0.5, 0.6, out why), why);
            Assert.IsFalse(w.Piece(clean).HasDefect(CraftDefectKind.Chip), "along its plane it parts cleanly");
            Assert.IsTrue(w.Piece(shattered).HasDefect(CraftDefectKind.Chip), "off the plane it shatters");
            Assert.Greater(w.Piece(clean).gem.carats, w.Piece(shattered).gem.carats);
            Assert.IsFalse(w.CleaveGem(Rough(w, "ruby"), 0, 0.3, 0.6, out why), "corundum has no cleavage: it is sawn");
        }

        // ------------------------------------------------------------------ jewellery
        private static CraftPiece CutStone(CraftWorkshop w, string gem, string cutId, int seed = 9)
        {
            string id = Rough(w, gem, seed, 4);
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, cutId, w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            CraftGemCut cut = w.Rules.Cut(cutId);
            double n = w.Rules.Gem(gem).refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
                Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, 0, 1, out why), why);
            Assert.IsTrue(w.PolishGem(id, "alumina", 14, 0.4, 0.5, out why), why);
            return w.Piece(id);
        }

        private static string Wire(CraftWorkshop w, string metal, (string suffix, float length, float width, float thickness) form) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, form.length, form.width, form.thickness, Metal(metal)));

        private static CraftPiece BezelRing(CraftWorkshop w, CraftPiece stone, double seatError, int pushes, double ringSize = 18)
        {
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, ringSize, 0.95, 0.02, 0, out string why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.95, 0.02, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + seatError, 0.95, 0.9, out why), why);
            Assert.IsTrue(w.MeasureFit(null, 0.95, 18, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, pushes, 0.6, 0.9, out why), why);
            return w.Piece(asm);
        }

        [Test]
        public void Ring_FromRoughToFinished_CarriesItsStone()
        {
            CraftWorkshop w = Workshop();
            CraftPiece ruby = CutStone(w, "ruby", "oval");
            CraftPiece asm = BezelRing(w, ruby, 0.02, 20);
            Assert.IsFalse(w.Finish(asm.id, out string why), "it must be buffed and inspected first");
            Assert.IsTrue(w.FinishJewel(asm.id, 0.9, 0.9, out why), why);
            Assert.IsTrue(w.Finish(asm.id, out why), why);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, asm);
            StringAssert.Contains("ruby", r.Name);
            Assert.Greater(r.MagicDefense, 4);
            Assert.IsTrue(r.Resistances.Any(x => x.element == ElementType.Fire && x.percent > 5), "a ruby carries fire");
            Assert.Greater(r.Price, CraftLapidary.Grade(w.Rules, ruby).Value, "worth more than its stone");
        }

        [Test]
        public void Setting_AWideSeat_LeavesTheStoneLoose()
        {
            CraftWorkshop w = Workshop();
            CraftPiece snug = BezelRing(w, CutStone(w, "sapphire", "cushion", 4), 0.02, 20);
            CraftPiece loose = BezelRing(w, CutStone(w, "sapphire", "cushion", 4), 0.7, 20);
            Assert.Greater(snug.assembly.Joint("set").security, loose.assembly.Joint("set").security + 0.2);
            Assert.IsTrue(w.FinishJewel(snug.id, 0.9, 0.9, out string why), why);
            Assert.IsFalse(w.FinishJewel(loose.id, 0.9, 0.9, out _), "a loose stone is not finished");
            CraftPiece stone = CutStone(w, "sapphire", "cushion", 4);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle - 0.5, 0.9, 0.9, out why), why);
            Assert.AreEqual(0, CraftJewelry.SeatFit(w.Piece(bezel), stone), "a seat too small will not take it");
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 6, Metal("Gold")));
            Assert.IsFalse(w.FormShank(bar, CraftJewelPart.Band, 18, 0.9, 0, 0, out _), "a bar is too heavy for a ring");
        }

        [Test]
        public void Jewellery_IsMadeOnlyAtAJewellersBench()
        {
            CraftDesign ring = Rules().Design("bezel_ring");
            Assert.IsNotNull(ring);
            Assert.AreEqual(CraftDesignKind.Jewelry, ring.kind);
            Assert.IsFalse(CraftAssembly.MadeAt(Rules(), ring, CraftTools.Smithy, out string missing));
            StringAssert.Contains("jeweller", missing);
            Assert.IsTrue(CraftAssembly.MadeAt(Rules(), ring, CraftTools.Jeweller, out missing), missing);
            CraftDesignMenu.Of(ring, out string category, out string group);
            Assert.AreEqual(CraftDesignMenu.Jewellery, category);
            Assert.AreEqual("Rings", group);
        }

        [Test]
        public void Guard_HoldsFacetingAndCardingToRealHands()
        {
            var guard = new CraftRequestGuard(5, 0);
            var facet = new CraftRequest(CraftRequestKind.FacetGem, "g") { i0 = 0, count = 16, a = 43f, b = 0f, c = 0f, d = 1f };
            Assert.IsTrue(guard.Check(ref facet, 0, out _), "a tier of sixteen facets, once");
            Assert.AreNotEqual(43f, facet.a, "the quill is never set truer than the hands set it");
            Assert.Greater(facet.b, 0f, "nor the index stops hit exactly");
            var again = new CraftRequest(CraftRequestKind.FacetGem, "g") { i0 = 1, count = 16, a = 43f };
            Assert.IsFalse(guard.Check(ref again, 0.1, out string why), "the next tier at once: too fast");
            StringAssert.Contains("time", why);
            var card = new CraftRequest(CraftRequestKind.CardFibre, "f") { count = 40, a = 0.3f, b = 1f };
            Assert.IsTrue(guard.Check(ref card, 1, out _));
            Assert.LessOrEqual(card.count, 8, "strokes are counted in real time");
            Assert.Less(card.b, 1f, "never perfectly even");
            var fit = new CraftRequest(CraftRequestKind.MeasureFit) { a = 1f, b = 950f };
            Assert.IsTrue(guard.Check(ref fit, 2, out _));
            Assert.Less(fit.a, 1f, "a tape never reads perfectly");
        }

        [Test]
        public void Journal_LearnsGemsAndTheNewInsights()
        {
            var d = new CraftJournalData();
            Assert.IsFalse(CraftJournal.KnowsGem(d, "ruby"));
            StringAssert.Contains("Ruby", CraftJournal.IdentifiedGem(d, Rules(), "ruby"));
            Assert.IsTrue(CraftJournal.KnowsGem(d, "ruby"));
            Assert.IsNull(CraftJournal.IdentifiedGem(d, Rules(), "ruby"), "learnt once");
            Assert.IsTrue(CraftJournalData.FromJson(d.ToJson()).gems.Contains("ruby"), "it is saved");
            foreach (string id in new[] { "gouged", "skipped_grit", "felted", "neps", "shattered", "flaked", "crazed", "soft_compound", "pavilion", "loose_stone", "crusted", "freehand", "overpolished", "skipped_pick" })
            {
                string insight = CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Note, 0, "", id, ""), null);
                Assert.IsNotNull(insight, id);
                Assert.IsTrue(CraftJournal.Insights.ContainsKey(insight), insight);
            }
        }

        [Test]
        public void WholeChain_ReplaysExactly()
        {
            CraftWorkshop a = Workshop();
            string log = a.AddPiece(CraftDefaults.Log(a.Rules, "oak", 900, 200));
            a.DebarkWood(log, 0, 99, 0.4, 0.8, 0.8, out _);
            a.SplitLog(log, 0.1, 0.8, 0.8, 5, out _);
            string fleece = Fleece(a);
            a.ScourFibre(fleece, 60, 20, 0.2, 5, "soap", 2, out _);
            a.RunSteps(3000);
            a.CardFibre(fleece, 8, 0.4, 0.8, out _);
            a.SpinFibre(fleece, 0.8, 0.5, 0.5, 40, out _);
            string gem = Rough(a, "topaz", 2, 6);
            a.TestGem(gem, CraftGemTest.Dichroscope, 0.8, 0, out _);
            a.OrientGem(gem, "emerald_cut", 30, 0.1, 0, 0.6, out _);
            a.SawGem(gem, 90, 0.7, 0.8, 0.9, 0.3, out _);
            a.CleaveGem(gem, a.Piece(gem).gem.cleaveTrace, -0.7, 0.6, out _);
            a.RunSteps(50);
            CraftWorkshop b = CraftWorkshop.Replay(Rules(), a.Tools, a.Ambient, a.Seed, a.Id, a.History, a.Step);
            Assert.AreEqual(a.Capture().ToJson(), b.Capture().ToJson());
        }
    }
}
#endif
