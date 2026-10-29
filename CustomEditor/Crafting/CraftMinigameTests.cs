#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the shared timing bar and of the lapidary's and the weaver's minigame processes: cleaning the
    /// rough, splitting by technique, sawing stroke by stroke, grinding the preform to its outline, faceting tier by tier,
    /// polishing region by region, and weaving yarn into cloth. Each step leaves its mark on what the next one gets.
    /// </summary>
    public class CraftMinigameTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "minigames");

        private static string Rough(CraftWorkshop w, string gem, int seed = 7, double carats = 0) => w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, seed, carats));

        // ------------------------------------------------------------------ the timing bar
        [Test]
        public void TimingBar_ScoresByDistance_ThroughConfigurableThresholds()
        {
            var s = new CraftTimingSettings();
            var bar = new CraftTimingBar(s);
            var t = new CraftTimingTarget(0.5f);
            Assert.AreEqual(1f, bar.Score(0f, t), 1e-5, "on the ideal point: 100%");
            Assert.AreEqual(s.perfectScore, bar.Score(s.perfectZone, t), 1e-4, "the edge of each zone scores what the settings say");
            Assert.AreEqual(s.goodScore, bar.Score(s.goodZone, t), 1e-4);
            float last = 1.01f;
            for (float d = 0f; d < 0.5f; d += 0.01f)
            {
                float score = bar.Score(d, t);
                Assert.Less(score, last + 1e-6, "the score never rises with the distance");
                last = score;
            }
            Assert.Greater(bar.Score(0.03f, t), bar.Score(0.05f, t), "within one zone the score still falls with the distance - it is not fixed per zone");
            Assert.AreEqual(CraftTimingGrade.Perfect, bar.Grade(bar.Score(0.01f, t), 0.01f, t));
            Assert.AreEqual(CraftTimingGrade.Excellent, bar.Grade(bar.Score(0.04f, t), 0.04f, t));
            Assert.AreEqual(CraftTimingGrade.Good, bar.Grade(bar.Score(0.1f, t), 0.1f, t));
            Assert.AreEqual(CraftTimingGrade.Poor, bar.Grade(bar.Score(0.2f, t), 0.2f, t));
            Assert.AreEqual(CraftTimingGrade.Miss, bar.Grade(bar.Score(0.3f, t), 0.3f, t));

            // the thresholds are the settings, not constants
            var wide = new CraftTimingSettings { perfectZone = 0.06f, excellentZone = 0.1f };
            Assert.AreEqual(CraftTimingGrade.Perfect, new CraftTimingBar(wide).Grade(new CraftTimingBar(wide).Score(0.04f, t), 0.04f, t));
            // a smaller target, a harder material, a narrower zone
            Assert.Less(bar.Score(0.05f, new CraftTimingTarget(0.5f, 0.5f)), bar.Score(0.05f, t), "a smaller target is harder");
            var hard = new CraftTimingBar(s) { difficulty = 1.5f };
            Assert.Less(hard.Zone(CraftTimingGrade.Good, t), bar.Zone(CraftTimingGrade.Good, t), "harder material narrows the zones");
            var master = new CraftTimingBar(s) { skill = 1f };
            Assert.Greater(master.Zone(CraftTimingGrade.Good, t), bar.Zone(CraftTimingGrade.Good, t), "practice widens them");
            Assert.Less(bar.Score(0.05f, new CraftTimingTarget(0.5f, 1.8f, 0.7f)), 0.71f, "a wide, safe target gives less");
        }

        [Test]
        public void TimingBar_IndicatorTurnsBackAtTheEnds_AndHitsAreRepeatable()
        {
            var bar = new CraftTimingBar(new CraftTimingSettings { speed = 1f });
            for (int i = 0; i < 300; i++)
            {
                bar.Tick(0.05f);
                Assert.That(bar.position, Is.InRange(0f, 1f));
            }
            CraftTimingHit Run(int seed)
            {
                var b = new CraftTimingBar(new CraftTimingSettings());
                b.NewTargets(seed, 3, true);
                var hands = new CraftHands(seed, 0.2);
                for (int i = 0; i < 17; i++) b.Tick(0.05f);
                return b.Hit(hands);
            }
            CraftTimingHit a = Run(5), c = Run(5);
            Assert.AreEqual(a.score, c.score, 1e-6, "the same moments give the same hit");
            Assert.AreEqual(a.grade, c.grade);
            var bad = new CraftTimingBar(new CraftTimingSettings());
            bad.targets.Add(new CraftTimingTarget(0.9f));
            bad.position = 0.05f;
            CraftTimingHit miss = bad.Hit();
            Assert.AreEqual(CraftTimingGrade.Miss, miss.grade);
            Assert.IsTrue(miss.damage, "far off the target the work is harmed");
            StringAssert.Contains("%", miss.Words, "the score shows as a percentage");
        }

        // ------------------------------------------------------------------ cleaning
        [Test]
        public void Cleaning_TakesTheCrustOff_AndThePickMarksBareStone()
        {
            CraftWorkshop w = Workshop();
            string brushed = Rough(w, "amethyst", 4), picked = Rough(w, "amethyst", 4), dirty = Rough(w, "amethyst", 4);
            Assert.Greater(CraftLapidary.Dirt(w.Piece(brushed).gem), 0.15, "a rough stone comes crusted from the ground");
            var even = Enumerable.Repeat(1.5f, CraftLapidary.CrustSectors).ToList();
            var hard = Enumerable.Repeat(3f, CraftLapidary.CrustSectors).ToList();
            for (int k = 0; k < 3; k++)
                Assert.IsTrue(w.CleanGem(brushed, false, even, 0.9, out string why), why);
            Assert.IsTrue(w.CleanGem(picked, true, hard, 0.2, out string why2), why2);
            Assert.IsTrue(w.CleanGem(picked, true, hard, 0.2, out why2), why2);
            Assert.Less(CraftLapidary.Dirt(w.Piece(brushed).gem), 0.05, "scrubbed thoroughly, it is clean");
            Assert.Less(CraftLapidary.Dirt(w.Piece(picked).gem), 0.05);
            Assert.AreEqual(0, w.Piece(brushed).gem.scratches, 1e-9, "the brush never harms the stone");
            Assert.Greater(w.Piece(picked).gem.scratches, 0.05, "the pick worked on bare stone scratches it");
            Assert.Greater(w.Piece(brushed).gem.cleaned, w.Piece(picked).gem.cleaned, "and the cleaning is judged by both");

            // what the crust hides
            Assert.IsTrue(w.TestGem(brushed, CraftGemTest.Gravity, 0.9, 0, out string why3), why3);
            Assert.IsTrue(w.TestGem(dirty, CraftGemTest.Gravity, 0.9, 0, out why3), why3);
            double sg = w.Rules.Gem("amethyst").specificGravity;
            Assert.Less(w.Piece(dirty).gem.readSG, sg - 0.08, "matrix stuck to it reads light on the balance");
            Assert.AreEqual(sg, w.Piece(brushed).gem.readSG, 0.25, "clean, it reads true");
            Assert.IsFalse(Workshop(CraftTools.FacetingMachine).CleanGem(brushed, false, even, 1, out _), "it is cleaned at a bench");
        }

        [Test]
        public void Crust_HidesInclusionsFromTheLoupe()
        {
            CraftWorkshop w = Workshop();
            string id = null;
            for (int seed = 1; seed < 80 && id == null; seed++)
            {
                CraftPiece p = CraftLapidary.NewRough(w.Rules, "emerald", seed);
                if (p.gem.inclusions.Any(i => CraftLapidary.CrustAt(p.gem, i.x, i.y) > 0.5)) id = w.AddPiece(p);
            }
            Assert.IsNotNull(id);
            string clean = w.AddPiece(w.Piece(id).Clone());
            Assert.IsTrue(w.CleanGem(clean, false, Enumerable.Repeat(3f, 16).ToList(), 1, out string why), why);
            Assert.IsTrue(w.CleanGem(clean, false, Enumerable.Repeat(3f, 16).ToList(), 1, out why), why);
            // a sweep as an ordinary search makes it: a look every fifth of the stone, at every depth
            var looks = new List<float>();
            for (double z = 0.05; z < 1; z += 0.12)
                for (double y = -0.9; y <= 0.9; y += 0.2)
                    for (double x = -0.9; x <= 0.9; x += 0.2)
                        looks.AddRange(new[] { (float)x, (float)y, (float)z });
            Assert.IsTrue(w.EvaluateGem(id, looks, 0.5, out why), why);
            Assert.IsTrue(w.EvaluateGem(clean, looks, 0.5, out why), why);
            Assert.Less(w.Piece(id).gem.inclusions.Count(i => i.found), w.Piece(clean).gem.inclusions.Count(i => i.found), "under the crust the loupe misses what is there");
        }

        // ------------------------------------------------------------------ splitting
        [Test]
        public void Splitting_DependsOnTheStone_AndOnTheBlow()
        {
            CraftWorkshop w = Workshop();
            CraftGemDefinition topazDef = w.Rules.Gem("topaz");
            float ideal = (float)CraftLapidary.SplitForce(topazDef, false);
            string trueBlow = Rough(w, "topaz", 5, 10), offBlow = Rough(w, "topaz", 5, 10), wildBlow = Rough(w, "topaz", 5, 10);
            double trace = w.Piece(trueBlow).gem.cleaveTrace;
            Assert.IsTrue(w.CleaveGem(trueBlow, trace, 0.5, ideal, out string why), why);
            Assert.IsTrue(w.CleaveGem(offBlow, trace, 0.5, ideal + 0.16, out why), why);
            Assert.IsTrue(w.CleaveGem(wildBlow, trace, 0.5, 1.0, out why), why);
            Assert.IsFalse(w.Piece(trueBlow).HasDefect(CraftDefectKind.Chip), "the right blow along the plane parts it cleanly");
            Assert.Greater(w.Piece(trueBlow).gem.carats, w.Piece(offBlow).gem.carats, "a blow off its measure loses more");
            Assert.IsTrue(w.Piece(wildBlow).HasDefect(CraftDefectKind.Chip), "far too hard, it shatters");
            Assert.Greater(w.Piece(offBlow).gem.carats, w.Piece(wildBlow).gem.carats);
            StringAssert.Contains("100%", w.Piece(trueBlow).record.Last(r => r.kind == CraftOpKind.CleaveGem).text, "the blow is judged as a percentage");

            // not every stone splits the same way
            Assert.IsFalse(w.CleaveGem(Rough(w, "ruby"), 0, 0.3, 0.6, out why), "corundum has no cleavage");
            Assert.IsFalse(w.CleaveGem(Rough(w, "ruby"), 0, 0.3, 0.6, out why, true), "and is too tough to snap: it is sawn");
            StringAssert.Contains("saw", why);
            Assert.IsFalse(w.CleaveGem(Rough(w, "topaz"), 0, 0.3, 0.6, out why, true), "a stone that cleaves is split along its plane");
            string quartz = Rough(w, "rock_crystal", 3, 8), quartz2 = Rough(w, "rock_crystal", 3, 8);
            Assert.IsFalse(w.CleaveGem(quartz, 0, 0.5, 0.6, out why), "quartz has no cleavage to follow");
            float snap = (float)CraftLapidary.SplitForce(w.Rules.Gem("rock_crystal"), true);
            Assert.IsTrue(w.CleaveGem(quartz, 0, 0.5, snap, out why, true), why);
            Assert.IsTrue(w.CleaveGem(quartz2, 0, 0.5, 0.31, out why, true), why);
            Assert.Greater(w.Piece(quartz).gem.carats, w.Piece(quartz2).gem.carats, "nicked and snapped well, less is lost");
            Assert.Greater(w.Piece(quartz).gem.chips, 0, "but it never breaks clean");
        }

        // ------------------------------------------------------------------ sawing
        [Test]
        public void Sawing_MissedStrokesCostWeightAndChipTheFace()
        {
            CraftWorkshop w = Workshop();
            string clean = Rough(w, "aquamarine", 6, 8), jumped = Rough(w, "aquamarine", 6, 8);
            Assert.IsTrue(w.SawGem(clean, 30, 0.6, 0.9, 1, 0.3, out string why), why);
            Assert.IsTrue(w.SawGem(jumped, 30, 0.6, 0.9, 1, 0.3, out why, 3), why);
            Assert.Greater(w.Piece(clean).gem.carats, w.Piece(jumped).gem.carats, "every miss costs weight");
            Assert.IsTrue(w.Piece(jumped).HasDefect(CraftDefectKind.Chip), "and chips the face");
            Assert.IsFalse(w.Piece(clean).HasDefect(CraftDefectKind.Chip));
            StringAssert.Contains("%", w.Piece(clean).record.Last(r => r.kind == CraftOpKind.SawGem).text);
        }

        // ------------------------------------------------------------------ grinding
        /// <summary>Grinds a stone toward its outline: touches wherever it stands outside it, until little is left.</summary>
        private static void GrindTrue(CraftWorkshop w, string id, double amount = 0.5, double margin = 0.02, int rounds = 60)
        {
            for (int r = 0; r < rounds && CraftLapidary.Excess(w.Piece(id).gem) > 0.08; r++)
            {
                GemState g = w.Piece(id).gem;
                List<float> prof = g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);
                var touches = new List<float>();
                for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                    if (prof[k] > CraftLapidary.TargetRadius(g, k) + margin) { touches.Add(k * 360f / CraftLapidary.ProfileRadii); touches.Add((float)amount); }
                if (touches.Count == 0) break;
                Assert.IsTrue(w.GrindGem(id, touches, 1, 0.9, out string why), why);
            }
        }

        [Test]
        public void Grinding_ToTheOutline_PreparesItForFaceting()
        {
            CraftWorkshop w = Workshop();
            string ground = Rough(w, "rock_crystal", 3, 8), over = Rough(w, "rock_crystal", 3, 8), freehand = Rough(w, "rock_crystal", 3, 8);
            foreach (string id in new[] { ground, over, freehand })
            {
                Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
                for (int k = 0; k < 4; k++)
                    Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            }
            Assert.IsFalse(w.GrindGem(Rough(w, "rock_crystal"), new List<float> { 0, 0.5f }, 1, 1, out _), "it is ground to a planned outline");
            GrindTrue(w, ground);
            GemState g = w.Piece(ground).gem;
            Assert.LessOrEqual(CraftLapidary.Excess(g), 0.15, "ground down to its outline");
            Assert.AreEqual(CraftGemStage.Preformed, g.stage);
            Assert.Less(g.shapeError, 0.15, "ground steadily, the outline is true");
            // pressed on past the outline
            GrindTrue(w, over);
            var all = new List<float>();
            for (int k = 0; k < CraftLapidary.ProfileRadii; k += 4) { all.Add(k * 360f / CraftLapidary.ProfileRadii); all.Add(1f); }
            for (int r = 0; r < 6; r++) Assert.IsTrue(w.GrindGem(over, all, 1, 0.9, out string why), why);
            GemState o = w.Piece(over).gem;
            Assert.Greater(o.overground, 0.05, "ground into the planned stone");
            Assert.Greater(o.shapeError, g.shapeError + 0.1, "and lopsided");
            Assert.Less(o.carats, g.carats, "and smaller");

            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            foreach (string id in new[] { ground, freehand })
                for (int t = 0; t < cut.tiers.Count; t++)
                    Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0.05, 0.05, 1, out string why), why);
            Assert.AreEqual(0.25, w.Piece(freehand).gem.shapeError, 1e-6, "never ground, the lap grinds it freehand: lopsided");
            Assert.Greater(CraftLapidary.Grade(w.Rules, w.Piece(ground)).Cut, CraftLapidary.Grade(w.Rules, w.Piece(freehand)).Cut + 0.1, "a ground preform cuts a better stone");

            // grinding not finished: faceting waits
            string half = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(half, "round_brilliant", 0, 0, 0, 0.6, out string why2), why2);
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(half, k * 45, 0.7, 0.9, 1, 0.3, out why2), why2);
            Assert.IsTrue(w.GrindGem(half, new List<float> { 0, 0.2f }, 1, 0.9, out why2), why2);
            if (CraftLapidary.Excess(w.Piece(half).gem) > 0.15)
                Assert.IsFalse(w.FacetGem(half, 0, 8, 43, 0, 0, 1, out _), "grind it closer first");
        }

        // ------------------------------------------------------------------ faceting and polishing
        private static CraftPiece Preform(CraftWorkshop w, string gem = "rock_crystal")
        {
            string id = Rough(w, gem, 3, 8);
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            GrindTrue(w, id);
            return w.Piece(id);
        }

        [Test]
        public void Faceting_UnevenOrBotchedFacets_CutAWorseStone()
        {
            CraftWorkshop w = Workshop();
            CraftPiece even = Preform(w), uneven = Preform(w);
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                double ideal = CraftLapidary.IdealAngle(cut.tiers[t], n);
                Assert.IsTrue(w.FacetGem(even.id, t, cut.tiers[t].facets, ideal, 0.05, 0.05, 1, out string why, 0.05, 0), why);
                Assert.IsTrue(w.FacetGem(uneven.id, t, cut.tiers[t].facets, ideal, 0.05, 0.05, 1, out why, 0.7, t == 0 ? 2 : 0), why);
            }
            Assert.AreEqual(2, uneven.gem.facets[0].misses);
            Assert.IsTrue(uneven.HasDefect(CraftDefectKind.Chip), "a botched facet chips");
            CraftGemGrade a = CraftLapidary.Grade(w.Rules, even), b = CraftLapidary.Grade(w.Rules, uneven);
            Assert.Greater(a.Cut, b.Cut + 0.2, "uneven facets, botched ones, a worse cut");
            Assert.IsTrue(b.Notes.Any(x => x.Contains("botched")), "the grade says why");
            Assert.Less(uneven.gem.carats, even.gem.carats);
            // a recut tier: costs weight, but the botched facets are gone
            double before = uneven.gem.carats;
            Assert.IsTrue(w.FacetGem(uneven.id, 0, cut.tiers[0].facets, CraftLapidary.IdealAngle(cut.tiers[0], n), 0.05, 0.05, 1, out string why2, 0.05, 0), why2);
            Assert.AreEqual(0, uneven.gem.facets[0].misses);
            Assert.Less(uneven.gem.carats, before, "recutting takes more off");
        }

        [Test]
        public void Polishing_EveryRegion_Brightens_ButNeverMendsTheCut()
        {
            CraftWorkshop w = Workshop();
            CraftPiece patchy = Preform(w), thorough = Preform(w), over = Preform(w), badCut = Preform(w);
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            foreach (CraftPiece p in new[] { patchy, thorough, over, badCut })
                for (int t = 0; t < cut.tiers.Count; t++)
                {
                    double ideal = CraftLapidary.IdealAngle(cut.tiers[t], n) + (p == badCut ? 6 : 0);
                    Assert.IsTrue(w.FacetGem(p.id, t, cut.tiers[t].facets, ideal, 0.05, 0.05, 1, out string why), why);
                }
            var corner = new List<float> { 1, 0, 0, 0, 0, 0, 0, 0, 0 };
            var everywhere = Enumerable.Repeat(1f, 9).ToList();
            Assert.IsTrue(w.PolishGem(patchy.id, "cerium_oxide", 12, 0.4, 0.5, out string why2, corner), why2);
            Assert.IsTrue(w.PolishGem(thorough.id, "cerium_oxide", 12, 0.4, 0.5, out why2, everywhere), why2);
            Assert.Greater(thorough.gem.polish, patchy.gem.polish + 0.3, "every region polished, it shines all over");
            Assert.IsTrue(CraftLapidary.Grade(w.Rules, patchy).Notes.Any(x => x.Contains("dull")), "a patchy polish leaves dull patches");
            for (int k = 0; k < 5; k++) Assert.IsTrue(w.PolishGem(over.id, "cerium_oxide", 30, 0.4, 0.5, out why2, everywhere), why2);
            Assert.Less(over.gem.carats, thorough.gem.carats, "polished on past bright, it loses weight");
            Assert.Less(over.gem.facets.Average(f => f.meet), thorough.gem.facets.Average(f => f.meet), "and the facet edges round");

            double angleBefore = badCut.gem.facets[0].angleError, cutBefore = CraftLapidary.Grade(w.Rules, badCut).Cut;
            for (int k = 0; k < 3; k++) Assert.IsTrue(w.PolishGem(badCut.id, "cerium_oxide", 12, 0.4, 0.5, out why2, everywhere), why2);
            Assert.AreEqual(angleBefore, badCut.gem.facets[0].angleError, 1e-9, "the polish never puts a bad angle right");
            Assert.LessOrEqual(CraftLapidary.Grade(w.Rules, badCut).Cut, cutBefore + 1e-6);
        }

        // ------------------------------------------------------------------ weaving
        private static string Yarn(CraftWorkshop w, string fibre = "wool_fibre", int grams = 100)
        {
            string f = w.AddPiece(CraftFibre.NewFibre(w.Rules, fibre));
            string y = w.SpinFibre(f, 0.85, 0.5, 0.6, grams, out string why);
            Assert.IsNotNull(y, why);
            return y;
        }

        [Test]
        public void Weaving_TurnsYarnIntoCloth_AsTheRowsWereBeaten()
        {
            CraftWorkshop w = Workshop();
            string goodYarn = Yarn(w), badYarn = Yarn(w);
            double length = w.Piece(goodYarn).yarn.length;
            var trueBeats = Enumerable.Repeat(0.95f, 8).ToList();
            var badBeats = Enumerable.Repeat(0.1f, 8).ToList();
            string good = w.WeaveCloth(goodYarn, 4, 0.95, 0.5, trueBeats, out string why);
            Assert.IsNotNull(good, why);
            string bad = w.WeaveCloth(badYarn, 4, 0.2, 0.5, badBeats, out why);
            Assert.IsNotNull(bad, why);
            CraftPiece g = w.Piece(good), b = w.Piece(bad);
            Assert.AreEqual(CraftPieceKind.Hide, g.kind, "woven cloth is a length the tailor lays out and cuts");
            Assert.AreEqual("wool_cloth", g.hide.hide, "wool weaves into wool cloth");
            Assert.AreEqual(4, g.hide.width);
            Assert.AreEqual(8, g.hide.height, "as many rows as were beaten in");
            Assert.AreEqual(length - 4 * 8 * CraftFibre.MetresPerCell, w.Piece(goodYarn).yarn.length, 1e-6, "the yarn is used, the rest stays on the bobbin");
            Assert.Greater(b.hide.cells.Count(c => c.flaw), g.hide.cells.Count(c => c.flaw) + 8, "badly beaten rows leave skipped picks");
            Assert.Greater(g.hide.cells.Average(c => c.smooth), b.hide.cells.Average(c => c.smooth));
            Assert.IsTrue(w.LayoutPattern(good, w.Rules.Patterns[0].id, 0, 0, 0, false, out why) || why.Length > 0, "the cloth goes on to the tailor's table");

            Assert.IsNull(Workshop(CraftTools.Tailor & ~CraftTools.Loom).WeaveCloth(goodYarn, 4, 1, 0.5, trueBeats, out why), "it needs a loom");
            StringAssert.Contains("loom", why);
            string little = Yarn(w, "wool_fibre", 2);
            Assert.IsNull(w.WeaveCloth(little, 16, 1, 0.5, trueBeats, out why), "too little yarn for a row");
            Assert.AreEqual("linen_cloth", CraftFibre.ClothOf(w.Rules, w.Piece(Yarn(w, "flax_fibre"))).id, "flax weaves into linen");
        }

        // ------------------------------------------------------------------ the guard and the replay
        [Test]
        public void Guard_HoldsTheNewWorkToRealHands()
        {
            var guard = new CraftRequestGuard(5, 0);
            var clean = new CraftRequest(CraftRequestKind.CleanGem, "g") { a = 1f, i0 = 7, points = Enumerable.Repeat(3f, 16).ToList() };
            Assert.IsTrue(guard.Check(ref clean, 0, out _));
            Assert.Less(clean.a, 1f, "never perfectly controlled");
            Assert.AreEqual(0, clean.i0, "a brush or a pick, nothing else");
            Assert.LessOrEqual(clean.points.Sum(), 30.0 + 1e-3, "no more scrubbing than the hands could do");
            var weave = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i0 = 4, i1 = 30, a = 1f, points = Enumerable.Repeat(1f, 30).ToList() };
            Assert.IsFalse(guard.Check(ref weave, 0.1, out string why), "thirty rows at once: too fast");
            StringAssert.Contains("row", why);
            var weave2 = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i0 = 4, i1 = 4, a = 1f, points = Enumerable.Repeat(1f, 4).ToList() };
            Assert.IsTrue(guard.Check(ref weave2, 3, out _));
            Assert.IsTrue(weave2.points.All(p => p < 1f), "no beat is perfectly true");
            var facet = new CraftRequest(CraftRequestKind.FacetGem, "g") { i0 = 0, count = 8, a = 43f, i1 = -3, i2 = 0 };
            Assert.IsTrue(guard.Check(ref facet, 20, out _));
            Assert.AreEqual(0, facet.i1);
            Assert.Greater(facet.i2, 0, "no tier of real facets is perfectly symmetrical");
            var saw = new CraftRequest(CraftRequestKind.SawGem, "g") { c = 1f, i1 = -5 };
            Assert.IsTrue(guard.Check(ref saw, 30, out _));
            Assert.AreEqual(0, saw.i1);
            var cleave = new CraftRequest(CraftRequestKind.CleaveGem, "g") { c = 0.5f, i0 = 9 };
            Assert.IsTrue(guard.Check(ref cleave, 40, out _));
            Assert.AreEqual(0, cleave.i0);
        }

        [Test]
        public void TheNewChain_ReplaysExactly()
        {
            CraftWorkshop a = Workshop();
            string gem = Rough(a, "rock_crystal", 3, 8);
            a.CleanGem(gem, false, Enumerable.Repeat(1.2f, 16).ToList(), 0.8, out _);
            a.CleanGem(gem, true, Enumerable.Repeat(1f, 16).ToList(), 0.4, out _);
            a.OrientGem(gem, "round_brilliant", 0, 0, 0, 0.6, out _);
            for (int k = 0; k < 4; k++) a.SawGem(gem, k * 45, 0.7, 0.8, 1, 0.3, out _, k % 2);
            GrindTrue(a, gem);
            a.FacetGem(gem, 0, 8, 43, 0.1, 0.1, 1, out _, 0.3, 1);
            string topaz = Rough(a, "topaz", 2, 6);
            a.CleaveGem(topaz, a.Piece(topaz).gem.cleaveTrace, 0.4, 0.55, out _);
            string y = Yarn(a);
            a.WeaveCloth(y, 3, 0.7, 0.6, new List<float> { 0.9f, 0.2f, 0.6f, 0.8f }, out _);
            a.RunSteps(30);
            CraftWorkshop b = CraftWorkshop.Replay(Rules(), a.Tools, a.Ambient, a.Seed, a.Id, a.History, a.Step);
            Assert.AreEqual(a.Capture().ToJson(), b.Capture().ToJson());
        }

        // ------------------------------------------------------------------ accessories
        [Test]
        public void Accessories_AreMadeForEverySlotAGemSuits()
        {
            var slots = Rules().Designs.Where(d => d.kind == CraftDesignKind.Jewelry).Select(d => d.armorSlot).Distinct().ToList();
            foreach (ArmorSlotType s in new[] { ArmorSlotType.Ring, ArmorSlotType.Amulet, ArmorSlotType.Trinket, ArmorSlotType.Bracers, ArmorSlotType.Helmet })
                Assert.Contains(s, slots, $"a jewel for the {s} slot");
            CraftDesign brooch = Rules().Design("brooch");
            Assert.IsNotNull(brooch);
            Assert.IsTrue(brooch.components.Any(c => c.kind == CraftPieceKind.Gem), "it carries a stone");
        }
    }
}
#endif
