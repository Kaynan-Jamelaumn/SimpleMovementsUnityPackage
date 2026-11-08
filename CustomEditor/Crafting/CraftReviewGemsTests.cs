#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the gems' review (the lapidary and the jeweller): a line is sawn or parted where it was drawn,
    /// whichever way it was drawn, and a line that misses what is left of the stone is refused and costs nothing; neither
    /// heat nor polishing on past bright ever mends a bad cut; the lap is charged with the compound a stone is polished
    /// with, and a compound too soft for the stone is said pass by pass; a pass polishes as long as it was on the stone;
    /// an offcut is rough; a recut grinds a tier's botched facets away, chips and all; facets cut alternately steep and
    /// shallow are uneven; a tier is cut, and timed, by its own facet count; one line says when a stone is set.
    /// </summary>
    public class CraftReviewGemsTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "gemsrev");

        private static string Rough(CraftWorkshop w, string gem, int seed = 7, double carats = 0)
        {
            string id = w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, seed, carats));
            w.Piece(id).gem.inclusions.Clear();
            return id;
        }

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Wire(CraftWorkshop w, string metal, (string suffix, float length, float width, float thickness) form) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, form.length, form.width, form.thickness, Metal(metal)));

        /// <summary>Every tier of a planned stone cut at its chart angle, its facets meeting as <paramref name="meet"/> says.</summary>
        private static CraftPiece Faceted(CraftWorkshop w, string gem, string cutId, int seed = 9, double carats = 4, double meet = 0, double table = double.NaN)
        {
            string id = Rough(w, gem, seed, carats);
            GemState g = w.Piece(id).gem;
            Assert.IsTrue(w.OrientGem(id, cutId, double.IsNaN(table) ? g.axis : table, 0, 0, 0.75, out string why), why);
            CraftGemCut cut = w.Rules.Cut(cutId);
            double n = w.Rules.Gem(gem).refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
                Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, meet, 1, out why), why);
            return w.Piece(id);
        }

        // ------------------------------------------------------------------ a line, where it was drawn (gems-r01)
        [Test]
        public void NormalLine_KeepsTheLine_ItsDirectionInAHalfTurn()
        {
            foreach ((double angle, double offset) in new[] { (180.0, 0.5), (-90.0, 0.3), (270.0, -0.2), (359.0, 0.4), (-180.0, 0.1), (45.0, 0.6), (0.0, -0.5), (540.0, 0.25) })
            {
                CraftLapidary.NormalLine(angle, offset, out double a, out double b);
                Assert.That(a, Is.GreaterThanOrEqualTo(0).And.LessThan(180), $"{angle}°");
                // the same line: every point of the one is on the other
                double r0 = angle * Math.PI / 180, r1 = a * Math.PI / 180;
                for (double s = -1; s <= 1; s += 0.5)
                {
                    double x = Math.Sin(r0) * offset + Math.Cos(r0) * s, y = -Math.Cos(r0) * offset + Math.Sin(r0) * s;
                    Assert.AreEqual(b, Math.Sin(r1) * x - Math.Cos(r1) * y, 1e-9, $"{angle}°, {offset}: a point of the line drawn is on the line cut");
                }
            }
        }

        [Test]
        public void Saw_ALineDrawnEitherWay_IsCutWhereItWasDrawn()
        {
            // across the top of the rough (plan y = +0.5), drawn left to right and right to left
            foreach ((double ax, double bx) in new[] { (-0.93, 0.93), (0.93, -0.93) })
            {
                CraftWorkshop w = Workshop();
                string id = Rough(w, "rock_crystal", 3, 8);
                CraftLapidary.LineThrough(ax, 0.5, bx, 0.5, out double angle, out double offset);
                Assert.IsTrue(w.SawGem(id, angle, offset, 0.9, 1, 0.3, out string why), why);
                GemState g = w.Piece(id).gem;
                Assert.IsFalse(CraftLapidary.Inside(g, 0, 0.8), $"drawn {(ax < bx ? "left to right" : "right to left")}: the top is sawn off");
                Assert.IsTrue(CraftLapidary.Inside(g, 0, -0.8), "the bottom, which the line never crossed, is kept");
            }
            // a planned stone off the centre: a line drawn downward beside it is sawn there - never through the plan
            CraftWorkshop ws = Workshop();
            string stone = Rough(ws, "rock_crystal", 3, 8);
            Assert.IsTrue(ws.OrientGem(stone, "round_brilliant", 0, -0.3, 0, 0.4, out string why2), why2);
            double planned = CraftLapidary.WindowRadius(ws.Piece(stone).gem);
            CraftLapidary.LineThrough(0.3, 0.9, 0.3, -0.9, out double down, out double at);
            Assert.IsTrue(ws.SawGem(stone, down, at, 0.9, 1, 0.3, out why2), why2);
            Assert.AreEqual(planned, CraftLapidary.WindowRadius(ws.Piece(stone).gem), 1e-6, "the planned stone is untouched");
            Assert.IsFalse(CraftLapidary.Inside(ws.Piece(stone).gem, 0.6, 0), "the rough beyond the line drawn is sawn off");
        }

        [Test]
        public void Cleave_ALineDrawnEitherWay_PartsWhereItWasDrawn()
        {
            foreach (bool reversed in new[] { false, true })
            {
                CraftWorkshop w = Workshop();
                string id = Rough(w, "topaz", 5, 10);
                GemState g = w.Piece(id).gem;
                // along the cleavage's trace, through the plan point half way out along its normal
                double t = g.cleaveTrace * Math.PI / 180, nx = Math.Sin(t), ny = -Math.Cos(t), dx = Math.Cos(t), dy = Math.Sin(t);
                double px = nx * 0.5, py = ny * 0.5, k = reversed ? -0.9 : 0.9;
                CraftLapidary.LineThrough(px - dx * k, py - dy * k, px + dx * k, py + dy * k, out double angle, out double offset);
                CraftTimingBar bar = CraftLapidary.SplitBar(w.Rules, w.Rules.Gem("topaz"), false);
                Assert.IsTrue(w.CleaveGem(id, angle, offset, bar.targets[0].ideal, out string why), why);
                StringAssert.Contains("along its plane", w.Piece(id).record.Last().text, "the line drawn lies on the cleavage: it parts cleanly");
                Assert.IsFalse(CraftLapidary.Inside(g, nx * 0.8, ny * 0.8), $"{(reversed ? "drawn the other way" : "drawn")}: the side beyond the line is parted off");
                Assert.IsTrue(CraftLapidary.Inside(g, -nx * 0.8, -ny * 0.8), "the far side, which the line never crossed, is kept");
            }
        }

        // ------------------------------------------------------------------ a line through nothing (gems-r05)
        [Test]
        public void Saw_ALineThroughNothingOfTheStone_IsRefused_AndCostsNothing()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.SawGem(id, 0, 0.5, 0.9, 1, 0.3, out string why), why); // the bottom off (y = -0.5)
            GemState g = w.Piece(id).gem;
            double carats = g.carats, area = CraftLapidary.Area(g), chips = g.chips, heat = g.heat;
            int pieces = w.Pieces.Count, faces = g.bounds.Count;
            Assert.IsFalse(CraftLapidary.LineCuts(g, 0, 0.6), "y = -0.6 runs below the face already sawn");
            // dry, hard and jumping the kerf: none of it happens, for the blade meets no stone
            Assert.IsFalse(w.SawGem(id, 0, 0.6, 0.2, 0, 1, out why, 5), "a line through nothing of the stone is refused");
            StringAssert.Contains("misses the stone", why);
            Assert.IsFalse(w.CleaveGem(id, 180, -0.6, 0.6, out why, true), "nor is it nicked and snapped there (the same line drawn the other way)");
            StringAssert.Contains("misses the stone", why);
            Assert.AreEqual(carats, g.carats, 1e-12, "no weight lost");
            Assert.AreEqual(area, CraftLapidary.Area(g), 1e-12);
            Assert.AreEqual(faces, g.bounds.Count);
            Assert.AreEqual(pieces, w.Pieces.Count, "no offcut");
            Assert.AreEqual(chips, g.chips, 1e-12, "no chips");
            Assert.AreEqual(heat, g.heat, 1e-12, "no heat");
            // the same line drawn the other way is the same line: refused too
            Assert.IsFalse(w.SawGem(id, 180, -0.6, 0.9, 1, 0.3, out why));
            // a line across what is left is sawn
            Assert.IsTrue(CraftLapidary.LineCuts(g, 0, 0.3));
            Assert.IsTrue(w.SawGem(id, 0, 0.3, 0.9, 1, 0.3, out why), why);
            Assert.Less(g.carats, carats);
        }

        [Test]
        public void Saw_AGroundStone_IsCutOnlyWhereItsGroundOutlineReaches()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            for (int r = 0; r < 60 && CraftLapidary.Excess(w.Piece(id).gem) > 0.08; r++)
            {
                GemState s = w.Piece(id).gem;
                List<float> prof = s.profile.Count == CraftLapidary.ProfileRadii ? s.profile : CraftLapidary.Outline(s);
                var touches = new List<float>();
                for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                    if (prof[k] > CraftLapidary.TargetRadius(s, k) + 0.02) { touches.Add(k * 360f / CraftLapidary.ProfileRadii); touches.Add(0.5f); }
                if (touches.Count == 0) break;
                Assert.IsTrue(w.GrindGem(id, touches, 1, 0.9, out why), why);
            }
            GemState g = w.Piece(id).gem;
            Assert.AreEqual(CraftLapidary.ProfileRadii, g.profile.Count);
            double carats = g.carats;
            // through the rough the wheel ground away: nothing of the stone is there
            Assert.IsFalse(w.SawGem(id, 90, 0.9, 0.9, 1, 0.3, out why), "the ground outline does not reach x = 0.9");
            StringAssert.Contains("misses the stone", why);
            Assert.AreEqual(carats, g.carats, 1e-12);
            // across the ground outline: sawn, the outline cut back to the face
            double East() => Enumerable.Range(0, CraftLapidary.ProfileRadii).Max(k => g.windowX + g.profile[k] * Math.Cos(k * Math.PI * 2 / CraftLapidary.ProfileRadii));
            double reach = East();
            Assert.IsTrue(w.SawGem(id, 90, reach * 0.8, 0.9, 1, 0.3, out why), why);
            Assert.AreEqual(CraftLapidary.ProfileRadii, g.profile.Count, "the grinding is not forgotten");
            Assert.LessOrEqual(East(), reach * 0.8 + 1e-6, "cut back to the new face");
            Assert.Less(g.carats, carats);
        }

        // ------------------------------------------------------------------ polishing never mends a cut (gems-r02)
        [Test]
        public void Polish_NeitherHeatNorPolishingOn_EverMendsABadCut()
        {
            Assert.AreEqual(-0.7, CraftLapidary.WorseMeet(-0.6, 0.1), 1e-12, "short facets fall shorter");
            Assert.AreEqual(0.7, CraftLapidary.WorseMeet(0.6, 0.1), 1e-12, "overcut ones deeper");
            Assert.AreEqual(-0.1, CraftLapidary.WorseMeet(0, 0.1), 1e-12, "true ones round off: they no longer meet");
            Assert.AreEqual(1, CraftLapidary.WorseMeet(0.95, 0.2), 1e-12);
            foreach (double meet in new[] { -0.6, 0.0, 0.6 })
            {
                // pressed hard on a fast lap (pressure × speed 1, far over the heat limit)
                CraftWorkshop a = Workshop(), b = Workshop();
                CraftPiece cool = Faceted(a, "sapphire", "cushion", meet: meet), hot = Faceted(b, "sapphire", "cushion", meet: meet);
                double cut = CraftLapidary.Grade(a.Rules, cool).Cut;
                for (int k = 0; k < 20; k++)
                {
                    Assert.IsTrue(a.PolishGem(cool.id, "alumina", 1, 0.45, 0.9, out string why), why);
                    Assert.IsTrue(b.PolishGem(hot.id, "alumina", 1, 1.0, 1.0, out why), why);
                }
                Assert.Greater(hot.gem.heat, 0, "the hot lap heated it");
                Assert.Less(CraftLapidary.Grade(b.Rules, hot).Cut, CraftLapidary.Grade(a.Rules, cool).Cut, $"meet {meet}: polished hot, the facets meet worse");
                Assert.LessOrEqual(CraftLapidary.Grade(a.Rules, cool).Cut, cut + 1e-9, "polished cool, it is as well cut as it was");
                Assert.GreaterOrEqual(Math.Abs(hot.gem.facets[0].meet), Math.Abs(meet), "the meet goes no nearer true");
            }
            // polished on and on past bright: an overcut tier gets no better
            CraftWorkshop w = Workshop();
            CraftPiece over = Faceted(w, "sapphire", "cushion", meet: 0.6);
            double before = CraftLapidary.Grade(w.Rules, over).Cut;
            for (int k = 0; k < 40; k++) Assert.IsTrue(w.PolishGem(over.id, "alumina", 1, 0.45, 0.9, out string why), why);
            Assert.Less(CraftLapidary.Grade(w.Rules, over).Cut, before, $"polished past bright, the edges round (meet {over.gem.facets[0].meet:0.00})");
            Assert.Greater(over.gem.facets[0].meet, 0.6);
        }

        // ------------------------------------------------------------------ the compound on the lap (gems-r06, r07)
        [Test]
        public void Polish_TheLapIsChargedWithTheStonesCompound_AnotherChargesItAfresh()
        {
            CraftWorkshop w = Workshop();
            CraftPiece p = Faceted(w, "sapphire", "cushion");
            Assert.IsTrue(CraftLapidary.ChargesLap(p.gem, "alumina"), "the first pass charges the lap");
            Assert.IsTrue(w.PolishGem(p.id, "alumina", 1, 0.45, 0.5, out string why), why);
            Assert.AreEqual("alumina", p.gem.compound);
            Assert.IsFalse(CraftLapidary.ChargesLap(p.gem, "alumina"), "passes with the same compound need nothing more");
            Assert.IsFalse(CraftLapidary.ChargesLap(p.gem, "Alumina"));
            Assert.IsTrue(CraftLapidary.ChargesLap(p.gem, "diamond_paste"), "another compound charges it afresh (from the bag, or it is refused)");
            Assert.IsTrue(w.PolishGem(p.id, "diamond_paste", 1, 0.45, 0.5, out why), why);
            Assert.AreEqual("diamond_paste", p.gem.compound);
            Assert.IsTrue(CraftLapidary.ChargesLap(p.gem, "alumina"), "and back again");
            // the stone's compound travels with it (a copy of the piece, a replay)
            Assert.AreEqual("diamond_paste", p.Clone().gem.compound);
            Assert.AreEqual("diamond_paste", p.gem.Clone().compound);
        }

        [Test]
        public void Polish_ACompoundTooSoft_IsSaid_PassByPass()
        {
            CraftWorkshop w = Workshop();
            CraftPiece p = Faceted(w, "ruby", "oval");
            var raised = new List<CraftEvent>();
            w.EventRaised += e => raised.Add(e);
            for (int k = 0; k < 6; k++) Assert.IsTrue(w.PolishGem(p.id, "tripoli", 1, 0.45, 0.5, out string why), why);
            Assert.AreEqual(6, raised.Count(e => e.Id == "soft_compound"), "one pass at a time, as the window sends them: each says the lap slides over it");
            Assert.Less(p.gem.polish, 0.3, "and it hardly polishes");
            raised.Clear();
            Assert.IsTrue(w.PolishGem(p.id, "alumina", 1, 0.45, 0.5, out string ok), ok);
            Assert.IsFalse(raised.Any(e => e.Id == "soft_compound"), "a compound as hard as the stone is not too soft");
        }

        // ------------------------------------------------------------------ time on the stone (gems-r14)
        [Test]
        public void Polish_APass_PolishesAsLongAsItWasOnTheStone()
        {
            var graze = new List<float>(new float[CraftLapidary.PolishRegions]);
            var half = new List<float>(new float[CraftLapidary.PolishRegions]);
            var claimed = new List<float>(new float[CraftLapidary.PolishRegions]);
            graze[0] = 0.02f;  // 20 ms on a corner, the rest of the half second off the stone
            half[0] = 0.5f;    // the whole half second on it
            claimed[0] = 3f;   // more time than a pass takes: no more is counted
            CraftWorkshop a = Workshop(), b = Workshop(), c = Workshop();
            CraftPiece grazed = Faceted(a, "sapphire", "cushion"), worked = Faceted(b, "sapphire", "cushion"), over = Faceted(c, "sapphire", "cushion");
            Assert.IsTrue(a.PolishGem(grazed.id, "alumina", 1, 0.45, 0.5, out string why, graze), why);
            Assert.IsTrue(b.PolishGem(worked.id, "alumina", 1, 0.45, 0.5, out why, half), why);
            Assert.IsTrue(c.PolishGem(over.id, "alumina", 1, 0.45, 0.5, out why, claimed), why);
            Assert.AreEqual(worked.gem.polishRegions[0] * 0.02 / 0.5, grazed.gem.polishRegions[0], 1e-4, "a graze polishes as much as it touched");
            Assert.AreEqual(worked.gem.polishRegions[0], over.gem.polishRegions[0], 1e-6, "a pass is half a second, however long it claims");
            // a pass evenly over the stone is a whole pass on every region, as one sent without regions
            CraftWorkshop d = Workshop(), e = Workshop();
            CraftPiece even = Faceted(d, "sapphire", "cushion"), plain = Faceted(e, "sapphire", "cushion");
            var spread = Enumerable.Repeat((float)(CraftLapidary.PassSeconds / CraftLapidary.PolishRegions), CraftLapidary.PolishRegions).ToList();
            Assert.IsTrue(d.PolishGem(even.id, "alumina", 1, 0.45, 0.5, out why, spread), why);
            Assert.IsTrue(e.PolishGem(plain.id, "alumina", 1, 0.45, 0.5, out why), why);
            Assert.AreEqual(plain.gem.polish, even.gem.polish, 1e-6);
        }

        [Test]
        public void Polish_ATableOnTheCleavage_FlakesOnlyWhereTheLapPressesOnIt()
        {
            CraftWorkshop a = Workshop(), b = Workshop();
            CraftPiece corners = Faceted(a, "topaz", "emerald_cut", 11, 6), table = Faceted(b, "topaz", "emerald_cut", 11, 6);
            corners.gem.tableAngle = corners.gem.cleaveTilt;
            table.gem.tableAngle = table.gem.cleaveTilt;
            Assert.IsTrue(CraftLapidary.TableOnCleavage(a.Rules.Gem("topaz"), corners.gem));
            var offTable = new List<float>(new float[CraftLapidary.PolishRegions]);
            var onTable = new List<float>(new float[CraftLapidary.PolishRegions]);
            offTable[0] = offTable[2] = 0.25f;
            onTable[CraftLapidary.TableRegion] = 0.5f;
            for (int k = 0; k < 4; k++)
            {
                Assert.IsTrue(a.PolishGem(corners.id, "alumina", 1, 0.6, 0.5, out string why, offTable), why);
                Assert.IsTrue(b.PolishGem(table.id, "alumina", 1, 0.6, 0.5, out why, onTable), why);
            }
            Assert.AreEqual(0, corners.gem.chips, 1e-12, "pressed hard on the corners, the table is never touched: it does not flake");
            Assert.Greater(table.gem.chips, 0, "pressed hard on the table, it flakes");
        }

        // ------------------------------------------------------------------ an offcut is rough (gems-r10)
        [Test]
        public void Offcut_IsRough_AndIsSawnLikeAnyRough()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 12);
            GemState g = w.Piece(id).gem;
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, -0.35, 0, 0.6, out string why), why);
            for (int k = 0; k < 3; k++) Assert.IsTrue(w.SawGem(id, 90 + k * 60, 0.62, 0.9, 1, 0.3, out why), why);
            Assert.AreEqual(CraftGemStage.Preformed, g.stage, "the stone itself is a preform now");
            int before = w.Pieces.Count;
            Assert.IsTrue(w.SawGem(id, 90, -0.3, 0.9, 1, 0.3, out why), why); // a big slab off its far side
            CraftPiece offcut = w.Pieces.Skip(before).FirstOrDefault(q => q.kind == CraftPieceKind.Gem);
            Assert.IsNotNull(offcut, "a slab big enough to cut is a stone of its own");
            Assert.AreEqual(CraftGemStage.Rough, offcut.gem.stage, "an offcut is rough: it was never preformed as a stone of its own");
            Assert.AreEqual(CraftGemStage.Preformed, g.stage, "the stone it came off stays a preform");
            Assert.IsTrue(w.OrientGem(offcut.id, "round_brilliant", 0, 0.3, 0, 0.3, out why), why);
            Assert.Greater(CraftLapidary.Excess(offcut.gem), CraftLapidary.PreformExcess, "most of the slab lies outside the planned stone");
            Assert.IsFalse(w.CanFacet(offcut.id, 0, out string reason), "it is not faceted with most of it outside its plan");
            StringAssert.Contains("Saw", reason);
        }

        // ------------------------------------------------------------------ a recut grinds the botch away (gems-r11)
        [Test]
        public void Facet_RecuttingABotchedTier_GrindsItsBotchAway_ChipsAndAll()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            GemState g = w.Piece(id).gem;
            Assert.IsTrue(w.OrientGem(id, "cushion", g.axis, 0, 0, 0.75, out string why), why);
            Assert.IsTrue(w.SawGem(id, 0, 0.9, 0.2, 1, 0.3, out why, 2), why); // the blade jumped twice: chips of the saw's own
            double sawn = g.chips;
            CraftPiece p = w.Piece(id);
            Assert.Greater(sawn, 0);
            Assert.AreEqual(1, p.defects.Count(d => d.kind == CraftDefectKind.Chip));
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            CraftGemCut cut = w.Rules.Cut("cushion");
            double angle = CraftLapidary.IdealAngle(cut.tiers[0], n);
            Assert.IsTrue(w.FacetGem(id, 0, 8, angle, 0, 0, 1, out why, 0, 4), why); // four facets botched
            Assert.Greater(g.chips, sawn);
            Assert.IsTrue(p.defects.Any(d => d.cause.Contains("recut the tier")), "the record asks for the tier to be recut");
            Assert.IsTrue(w.FacetGem(id, 0, 8, angle, 0, 0, 1, out why, 0, 0), why); // recut true, as asked
            Assert.AreEqual(0, g.facets[0].misses);
            Assert.IsFalse(p.defects.Any(d => d.cause.Contains("recut the tier")), "the botch is ground away with its facets");
            Assert.AreEqual(sawn, g.chips, 1e-9, "its chips with it - and only its own: the saw's stay");
            Assert.AreEqual(1, p.defects.Count(d => d.kind == CraftDefectKind.Chip), "the saw's chip is still a flaw");
            // botched again on the recut: a flaw of the tier's own once more
            Assert.IsTrue(w.FacetGem(id, 0, 8, angle, 0, 0, 1, out why, 0, 2), why);
            Assert.AreEqual(1, p.defects.Count(d => d.cause.Contains("recut the tier")));
            StringAssert.Contains("2 facets botched", p.defects.Single(d => d.cause.Contains("recut the tier")).cause);
        }

        // ------------------------------------------------------------------ uneven facets (gems-r13)
        private static CraftTimingHit Hit(float offset, float score, bool damage = false) => new CraftTimingHit { offset = offset, score = score, damage = damage };

        [Test]
        public void Facet_FacetsCutAlternatelySteepAndShallow_AreUneven()
        {
            // six Good hits, alternately half a Poor zone either side of the target: each facet 1.25° off its angle
            var zigzag = new List<CraftTimingHit>();
            for (int k = 0; k < 6; k++) zigzag.Add(Hit(k % 2 == 0 ? 0.5f : -0.5f, 0.55f));
            CraftLapidary.TierFromHits(zigzag, out double angle, out double index, out double meet, out double asym, out int misses);
            Assert.AreEqual(0, angle, 1e-6, "on the whole the tier lies at its angle");
            Assert.AreEqual(0, meet, 1e-6);
            Assert.Greater(asym, 0.35, "but its facets are cut steep, shallow, steep...: uneven");
            Assert.AreEqual(0, misses);
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(id, "cushion", w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            double ideal = CraftLapidary.IdealAngle(w.Rules.Cut("cushion").tiers[0], w.Rules.Gem("rock_crystal").refractiveIndex);
            Assert.IsTrue(w.FacetGem(id, 0, 8, ideal + angle, index, meet, 1, out why, asym, misses), why);
            StringAssert.Contains("cut unevenly", w.Piece(id).record.Last().text);
            // all on the one side: no more uneven than the hits' scores say (the tier lies off its angle instead)
            var aside = Enumerable.Repeat(Hit(0.5f, 0.55f), 6).ToList();
            CraftLapidary.TierFromHits(aside, out double angle2, out _, out double meet2, out double asym2, out _);
            Assert.AreEqual(1.25, angle2, 1e-6);
            Assert.AreEqual(0.4, meet2, 1e-6);
            Assert.AreEqual((1 - 0.55) * 0.3, asym2, 1e-6);
            // true hits are true
            CraftLapidary.TierFromHits(Enumerable.Repeat(Hit(0f, 1f), 6).ToList(), out double a3, out double i3, out double m3, out double s3, out _);
            Assert.AreEqual(0, a3 + i3 + m3 + s3, 1e-9);
            // a miss is a botched facet
            CraftLapidary.TierFromHits(new List<CraftTimingHit> { Hit(0f, 1f), Hit(1.6f, 0f, true) }, out _, out _, out _, out _, out int botched);
            Assert.AreEqual(1, botched);
        }

        // ------------------------------------------------------------------ the loupe's search (gems-r12)
        /// <summary>Looks along rows about <paramref name="rows"/> apart, edge to edge (a look every 10 px of a drag), at five depths.</summary>
        private static List<float> Sweep(double rows)
        {
            var looks = new List<float>();
            int n = (int)Math.Ceiling(1.96 / rows);
            foreach (float z in new[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f })
                for (int r = 0; r <= n; r++)
                    for (double x = -0.98; x <= 0.99; x += 10.0 / 150)
                    {
                        looks.Add((float)x); looks.Add((float)(-0.98 + r * 1.96 / n)); looks.Add(z);
                    }
            return looks;
        }

        private static double Searched(GemState g, IList<float> looks, double reach)
        {
            var searched = new bool[CraftLapidary.SearchCells * CraftLapidary.SearchCells * CraftLapidary.SearchDepths];
            for (int k = 0; k + 2 < looks.Count; k += 3) CraftLapidary.MarkSearched(g, searched, looks[k], looks[k + 1], looks[k + 2], reach);
            return CraftLapidary.SearchedShare(g, searched);
        }

        [Test]
        public void Evaluate_TheSearchedMeter_CountsOnlyWhatTheLoupeTrulyWentOver()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            GemState g = w.Piece(id).gem;
            for (int k = 0; k < g.crust.Count; k++) g.crust[k] = 0f; // cleaned
            double reach = CraftLapidary.LoupeReach(0.6, w.ToolQuality);
            // the reviewer's sweep: looks 0.4 apart at five depths leave gaps between the loupe's rings
            var sparse = new List<float>();
            foreach (float z in new[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f })
                for (float y = -0.8f; y <= 0.81f; y += 0.4f)
                    for (float x = -0.8f; x <= 0.81f; x += 0.4f) { sparse.Add(x); sparse.Add(y); sparse.Add(z); }
            g.inclusions.Add(new CraftInclusion { kind = CraftInclusionKind.Crystal, x = 0.2f, y = 0.2f, depth = 0.5f, size = 0.03f });
            double covered = Searched(g, sparse, reach);
            Assert.IsTrue(w.EvaluateGem(id, sparse, 0.6, out string why), why);
            Assert.IsFalse(g.inclusions[0].found, "the crystal between the rings is not found");
            Assert.Less(covered, 0.5, $"and the meter does not call that search done ({covered * 100:0}%)");
            // one focus finds only what lies near it: a look at mid-depth searches none of the stone's top or bottom
            var one = new bool[CraftLapidary.SearchCells * CraftLapidary.SearchCells * CraftLapidary.SearchDepths];
            CraftLapidary.MarkSearched(g, one, 0, 0, 0.5, reach);
            for (int k = 0; k < CraftLapidary.SearchDepths; k++)
            {
                double lo = (double)k / CraftLapidary.SearchDepths, hi = (k + 1.0) / CraftLapidary.SearchDepths;
                bool inFocus = lo >= 0.5 - CraftLapidary.FocusDepth - 1e-9 && hi <= 0.5 + CraftLapidary.FocusDepth + 1e-9;
                int middle = (k * CraftLapidary.SearchCells + CraftLapidary.SearchCells / 2) * CraftLapidary.SearchCells + CraftLapidary.SearchCells / 2;
                Assert.AreEqual(inFocus, one[middle], $"depth {lo:0.00}-{hi:0.00}");
            }
            // rows close enough for the rings to meet: the meter fills, and then every inclusion is found
            foreach (int seed in new[] { 3, 5, 8, 13, 21 })
            {
                CraftWorkshop ws = Workshop();
                string stone = ws.AddPiece(CraftLapidary.NewRough(ws.Rules, "emerald", seed, 3)); // a well included stone
                GemState s = ws.Piece(stone).gem;
                for (int k = 0; k < s.crust.Count; k++) s.crust[k] = 0f;
                List<float> dense = Sweep(0.22);
                Assert.AreEqual(1, Searched(s, dense, reach), 1e-9, "every cell, at every depth");
                for (int at = 0; at < dense.Count; at += 1500)
                    Assert.IsTrue(ws.EvaluateGem(stone, dense.GetRange(at, Math.Min(1500, dense.Count - at)), 0.6, out why), why);
                Assert.IsTrue(s.inclusions.All(i => i.found), $"seed {seed}: a search the meter calls complete finds what is there ({s.inclusions.Count(i => !i.found)} of {s.inclusions.Count} missed)");
            }
            // a thick crust hides what lies beneath it: the meter does not count it searched
            CraftWorkshop wc = Workshop();
            string crusted = Rough(wc, "rock_crystal", 3, 8);
            GemState gc = wc.Piece(crusted).gem;
            for (int k = 0; k < gc.crust.Count; k++) gc.crust[k] = 1f;
            Assert.Less(Searched(gc, Sweep(0.22), reach), 0.9, "under a thick crust the loupe sees nothing");
            // a cut stone is searched over the stone itself (its planned outline), not the rough that was ground away
            CraftWorkshop wf = Workshop();
            CraftPiece cut = Faceted(wf, "sapphire", "cushion");
            Assert.AreEqual(1, Searched(cut.gem, Sweep(0.22), reach), 1e-9);
            Assert.IsFalse(CraftLapidary.InStone(cut.gem, 0.95, 0), "outside the cut stone's outline");
        }

        // ------------------------------------------------------------------ one line for "set" (gems-r04)
        /// <summary>A bezel ring with its stone in it, soldered, the seat cut <paramref name="seatOver"/> mm wider than the girdle.</summary>
        private static string BezelRing(CraftWorkshop w, out CraftPiece stone, double seatOver = 0.02)
        {
            stone = Faceted(w, "sapphire", "cushion", 4);
            Assert.IsTrue(w.PolishGem(stone.id, "alumina", 14, 0.25, 0.5, out string why), why);
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 18, 0.95, 0.02, 0, out why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.95, 0.02, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + seatOver, 0.95, 0.95, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            return asm;
        }

        [Test]
        public void Setting_AStoneTheSetPlaceCallsSet_TheInspectionAndTheItemCallSetToo()
        {
            CraftWorkshop w = Workshop();
            string asm = BezelRing(w, out CraftPiece stone);
            CraftJointSpec spec = w.Rules.Design("bezel_ring").Joint("set");
            JointState j = w.Piece(asm).assembly.Joint("set");
            // firm, even pushes one by one, as the window sends them: the joint is done exactly when the stone is secure
            int pushes = 0;
            while (!CraftAssembly.JointDone(spec, j) && pushes < 60)
            {
                Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, 1, 0.6, 0.95, out string why), why);
                pushes++;
                // short of the line, the stone is loose: neither set nor to be finished
                if (!CraftAssembly.JointDone(spec, j))
                {
                    Assert.Less(j.security, CraftJewelry.SetSecure);
                    Assert.IsTrue(CraftEvaluator.Item(w.Rules, w, w.Piece(asm)).Notes.Any(n => n.Contains("loose")), $"after {pushes} pushes (security {j.security:0.000}) it is loose");
                    Assert.IsFalse(w.FinishJewel(asm, 0.9, 0.9, out _), "and not inspected as finished work");
                }
            }
            Assert.GreaterOrEqual(j.security, CraftJewelry.SetSecure, $"set after {pushes} pushes");
            Assert.IsTrue(w.FinishJewel(asm, 0.9, 0.9, out string ok), ok);
            StringAssert.DoesNotContain("moves in its setting", w.Piece(asm).record.Last().text, "the inspection agrees: it holds");
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Assert.IsFalse(r.Notes.Any(n => n.Contains("loose")), "the item agrees too: " + string.Join("; ", r.Notes));
            // the seat's words: "start a new setting" exactly where no pushing could ever set it
            for (double err = -0.1; err < 0.6; err += 0.01)
            {
                bool never = (0.35 + 0.65 * CraftJewelry.SeatFitOf(err)) < CraftJewelry.SetSecure;
                Assert.AreEqual(never, CraftJewelry.SeatWords(err).Contains("start a new setting"), $"a seat {err:0.00} mm wide of the girdle");
            }
        }

        // ------------------------------------------------------------------ the tier's own facets (gems-r17)
        [Test]
        public void Facet_ATierIsCutAndTimed_ByItsOwnFacetCount()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            Assert.AreEqual(8, w.TierFacets(id, 0));
            Assert.AreEqual(16, w.TierFacets(id, 1));
            Assert.AreEqual(0, w.TierFacets(id, 9), "no such tier");
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            // a client that claims one facet for an eight-facet tier
            var r = new CraftRequest(CraftRequestKind.FacetGem, id) { i0 = 0, count = 1, a = (float)CraftLapidary.IdealAngle(cut.tiers[0], n), b = 0.1f, d = 1f, i2 = 100 };
            var guard = new CraftRequestGuard(3, 0);
            Assert.IsTrue(guard.Check(ref r, 0, out why, w), why);
            Assert.AreEqual(8, r.count, "the guard times the tier by its own facets");
            var next = new CraftRequest(CraftRequestKind.FacetGem, id) { i0 = 1, count = 1, a = r.a, b = 0.1f, d = 1f, i2 = 100 };
            Assert.IsFalse(guard.Check(ref next, 0.2, out why, w), "a novice's six hits on the lap take time: the next tier cannot follow at once");
            StringAssert.Contains("Too fast", why);
            Assert.IsTrue(w.FacetGem(id, 0, 1, r.a, r.b, 0, 1, out why), why);
            Assert.AreEqual(8, w.Piece(id).gem.facets[0].facets, "the workshop cuts the tier's own eight facets, whatever was claimed");
            Assert.IsTrue(w.FacetGem(id, 1, 1, CraftLapidary.IdealAngle(cut.tiers[1], n), 0.1, 0, 1, out why, 0, 9), why);
            Assert.AreEqual(16, w.Piece(id).gem.facets[1].facets);
            Assert.AreEqual(9, w.Piece(id).gem.facets[1].misses, "the misses are counted against the tier's own facets");
        }
    }
}
#endif
