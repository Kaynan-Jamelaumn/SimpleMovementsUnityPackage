#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the lapidary's and the jeweller's rules as the windows show them: the hardness points bracket a
    /// stone's own hardness, a name sticks once given, the loupe finds what it shows sharp, only a cleavage worth following
    /// is cleaved, a ground preform keeps its outline when sawn and cannot be re-planned, the planned stone weighs the same
    /// whichever way it is cut, grinding heat and table flakes come pass by pass however the window sends them, a cut stone
    /// carries no crust, a tier can be asked before it is cut; the jeweller's metal is cut to the length a part needs, a
    /// seat's words follow its fit, a setting must suit its stone (collar, claws), the hands' forming counts in the item, a
    /// part taken off is unsoldered and unset, buffing is held to time on the wheel, a ring is the only jewel sized to a
    /// finger and each measurement waits for the item that takes it.
    /// </summary>
    public class CraftGemsAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "gems");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Rough(CraftWorkshop w, string gem, int seed = 7, double carats = 0) => w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, seed, carats));

        /// <summary>Grinds a stone toward its outline wherever it stands outside it, with the drip on.</summary>
        private static void GrindTrue(CraftWorkshop w, string id, double amount = 0.5, int rounds = 60)
        {
            for (int r = 0; r < rounds && CraftLapidary.Excess(w.Piece(id).gem) > 0.08; r++)
            {
                GemState g = w.Piece(id).gem;
                List<float> prof = g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);
                var touches = new List<float>();
                for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                    if (prof[k] > CraftLapidary.TargetRadius(g, k) + 0.02) { touches.Add(k * 360f / CraftLapidary.ProfileRadii); touches.Add((float)amount); }
                if (touches.Count == 0) break;
                Assert.IsTrue(w.GrindGem(id, touches, 1, 0.9, out string why), why);
            }
        }

        /// <summary>A rough oriented and sawn on four sides, ready for the wheel.</summary>
        private static string Sawn(CraftWorkshop w, string gem, string cut, int seed = 3, double carats = 8)
        {
            string id = Rough(w, gem, seed, carats);
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, cut, w.Piece(id).gem.axis, 0, 0, 0.6, out string why), why);
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            return id;
        }

        private static CraftPiece CutStone(CraftWorkshop w, string gem, string cutId, int seed = 9)
        {
            string id = Rough(w, gem, seed, 4);
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, cutId, w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            CraftGemCut cut = w.Rules.Cut(cutId);
            double n = w.Rules.Gem(gem).refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
                Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, 0, 1, out why), why);
            Assert.IsTrue(w.PolishGem(id, "alumina", 14, 0.25, 0.5, out why), why);
            return w.Piece(id);
        }

        private static string Wire(CraftWorkshop w, string metal, (string suffix, float length, float width, float thickness) form) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, form.length, form.width, form.thickness, Metal(metal)));

        // ------------------------------------------------------------------ the lapidary
        [Test]
        public void HardnessPoints_BracketEveryStonesOwnHardness()
        {
            CraftWorkshop w = Workshop();
            float[] points = { 5f, 6f, 6.5f, 7f, 7.5f, 8f, 9f, 9.5f };
            foreach (CraftGemDefinition d in w.Rules.Gems)
            {
                string id = Rough(w, d.id, 4);
                foreach (float pt in points)
                    Assert.IsTrue(w.TestGem(id, CraftGemTest.Scratch, 0.9, pt, out string why), why);
                GemState g = w.Piece(id).gem;
                Assert.LessOrEqual(g.hardLo, d.hardness, $"{d.Name}: the reading's low end is at most its hardness");
                Assert.GreaterOrEqual(g.hardHi, d.hardness, $"{d.Name}: the reading's high end is at least its hardness");
            }
        }

        [Test]
        public void Naming_RaisesNoWordsThatTellWhetherItWasRight()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "sapphire");
            Assert.IsTrue(w.TestGem(id, CraftGemTest.Loupe, 0.9, 0, out string why), why);
            Assert.IsTrue(w.TestGem(id, CraftGemTest.Refractometer, 0.9, 0, out why), why);
            var raised = new List<CraftEvent>();
            w.EventRaised += e => raised.Add(e);
            Assert.IsTrue(w.IdentifyGem(id, "ruby", out why), why);
            Assert.IsTrue(raised.Any(e => e.Id == "misidentified"), "scripts can still tell");
            Assert.IsTrue(raised.Where(e => e.Kind == CraftEventKind.Note).All(e => string.IsNullOrEmpty(e.Text)), "but the log is told nothing");
            Assert.IsFalse(w.IdentifyGem(id, "sapphire", out why), "and there is no second try");
            Assert.IsFalse(w.Piece(id).gem.identified);
        }

        [Test]
        public void Loupe_FindsExactlyWhatItShowsSharp()
        {
            CraftWorkshop w = Workshop();
            string id = null;
            for (int seed = 1; seed < 60 && id == null; seed++)
            {
                CraftPiece p = CraftLapidary.NewRough(w.Rules, "emerald", seed);
                if (p.gem.inclusions.Count >= 3) id = w.AddPiece(p);
            }
            Assert.IsNotNull(id);
            GemState g = w.Piece(id).gem;
            CraftInclusion target = g.inclusions[0];
            double steady = 0.6, reach = CraftLapidary.LoupeReach(steady, w.ToolQuality);
            Assert.AreEqual((0.1 + 0.08 * steady) * Math.Min(1.3, w.ToolQuality), reach, 1e-9);
            // a look at the inclusion, in focus: the window shows it sharp (Seen) - and the search finds it
            bool shown = CraftLapidary.Seen(g, target, target.x, target.y, target.depth, reach);
            Assert.IsTrue(w.EvaluateGem(id, new List<float> { target.x, target.y, target.depth }, steady, out string why), why);
            Assert.AreEqual(shown, w.Piece(id).gem.inclusions[0].found, "found if and only if shown");
            // out of focus it is not seen
            CraftInclusion other = g.inclusions[1];
            float far = other.depth > 0.5f ? 0f : 1f;
            Assert.IsFalse(CraftLapidary.Seen(g, other, other.x, other.y, far, reach) && Math.Abs(other.depth - far) > 0.12 + other.size);
            // under a thick crust nothing near the skin shows
            var crusted = new GemState { gem = "emerald" };
            for (int k = 0; k < CraftLapidary.CrustSectors; k++) crusted.crust.Add(1f);
            var skin = new CraftInclusion { x = 0.85f, y = 0f, depth = 0.5f, size = 0.05f };
            Assert.IsFalse(CraftLapidary.Seen(crusted, skin, 0.85, 0, 0.5, 0.2), "a thick crust is opaque");
        }

        [Test]
        public void Cleaving_OnlyACleavageWorthFollowing_AndTheRefusalNamesNoStone()
        {
            CraftWorkshop w = Workshop();
            foreach (string gem in new[] { "emerald", "aquamarine", "peridot" })
            {
                string id = Rough(w, gem, 5, 8);
                double trace = w.Piece(id).gem.cleaveTrace;
                Assert.IsFalse(w.CleaveGem(id, trace, 0.5, 0.6, out string why), $"{gem}: a poor cleavage is not followed");
                StringAssert.Contains("cleavage is too poor", why);
                StringAssert.DoesNotContain(w.Rules.Gem(gem).Name, why, "the refusal does not name the stone");
                Assert.IsTrue(CraftLapidary.Snappable(w.Rules.Gem(gem)), $"{gem}: it can be nicked and snapped instead");
            }
            string topaz = Rough(w, "topaz", 5, 10);
            Assert.IsTrue(w.CleaveGem(topaz, w.Piece(topaz).gem.cleaveTrace, 0.5, 0.6, out string ok), ok);
            Assert.AreEqual(4, CraftLapidary.CleaveTolerance(w.Rules.Gem("topaz")), 1e-9);
            Assert.AreEqual(2.5, CraftLapidary.CleaveTolerance(w.Rules.Gem("emerald")), 1e-9, "no 1° branch: a poor cleavage is never followed");
            Assert.AreEqual(3, CraftLapidary.CleaveError(33, 30), 1e-9);
            Assert.AreEqual(2, CraftLapidary.CleaveError(178, 0), 1e-9, "a line is the same line turned half round");
        }

        [Test]
        public void GroundPreform_KeepsItsOutlineWhenSawn_AndItsPlan()
        {
            CraftWorkshop w = Workshop();
            string id = Sawn(w, "rock_crystal", "round_brilliant");
            GrindTrue(w, id);
            GemState g = w.Piece(id).gem;
            double excess = CraftLapidary.Excess(g), carats = g.carats;
            Assert.LessOrEqual(excess, 0.15);
            // a saw line through rough already ground away meets nothing of the stone: refused, and nothing is lost
            Assert.IsFalse(w.SawGem(id, 90, 0.9, 0.9, 1, 0.3, out string why), "the ground outline does not reach that far");
            Assert.AreEqual(carats, g.carats, 1e-12);
            Assert.AreEqual(excess, CraftLapidary.Excess(g), 1e-12, "nor is its outline touched");
            // a saw cut across the edge of the ground outline: the outline is kept, cut back to the face, its carats with it
            double east = Enumerable.Range(0, CraftLapidary.ProfileRadii).Max(k => g.windowX + g.profile[k] * Math.Cos(k * Math.PI * 2 / CraftLapidary.ProfileRadii));
            Assert.IsTrue(w.SawGem(id, 90, east * 0.95, 0.9, 1, 0.3, out why), why);
            Assert.AreEqual(CraftLapidary.ProfileRadii, g.profile.Count, "the grinding is not forgotten");
            Assert.Less(g.carats, carats);
            Assert.Greater(g.carats, carats * 0.85, "only the slice the face took, and the kerf");
            // the plan is fixed once ground
            Assert.IsFalse(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out why), "re-marking would grind the carats off again");
            StringAssert.Contains("plan is fixed", why);
            // the wheel takes on no more than the grindable excess
            string bulky = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(bulky, "round_brilliant", 0, 0, 0, 0.4, out why), why);
            Assert.Greater(CraftLapidary.Excess(w.Piece(bulky).gem), CraftLapidary.GrindableExcess);
            Assert.IsFalse(w.GrindGem(bulky, new List<float> { 0, 0.5f }, 1, 1, out _));
        }

        [Test]
        public void PlannedStone_WeighsTheSame_GroundOrFreehand_AsThePreviewSaid()
        {
            foreach (string cut in new[] { "emerald_cut", "oval", "round_brilliant" })
            {
                CraftWorkshop w = Workshop();
                string ground = Sawn(w, "rock_crystal", cut), freehand = Sawn(w, "rock_crystal", cut);
                GrindTrue(w, ground);
                double promised = CraftLapidary.Grade(w.Rules, w.Piece(freehand)).Yield;
                double n = w.Rules.Gem("rock_crystal").refractiveIndex;
                CraftGemCut c = w.Rules.Cut(cut);
                // (facets that meet exactly: an overcut tier takes a little more weight, which no plan can foresee)
                foreach (string id in new[] { ground, freehand })
                    Assert.IsTrue(w.FacetGem(id, 0, c.tiers[0].facets, CraftLapidary.IdealAngle(c.tiers[0], n), 0.05, 0, 1, out string why), why);
                double g = w.Piece(ground).gem.carats, f = w.Piece(freehand).gem.carats;
                Assert.AreEqual(promised, f, 1e-6, $"{cut}: the rough's expected yield is what the first tier leaves");
                Assert.Less(f, g * 1.25, $"{cut}: skipping the wheel gives no more stone than grinding ({f:0.00} vs {g:0.00} ct)");
                Assert.Greater(w.Piece(freehand).gem.shapeError, w.Piece(ground).gem.shapeError, "but a lopsided one");
            }
        }

        [Test]
        public void GrindingHeat_ComesTouchByTouch_HoweverItIsSent()
        {
            double Heat(string gem, int batch, double coolant, double amount = 0.4)
            {
                CraftWorkshop w = Workshop();
                string id = Sawn(w, gem, "round_brilliant");
                var all = new List<float>();
                for (int k = 0; k < 40; k++) { all.Add(k * 9f); all.Add((float)amount); }
                for (int i = 0; i < all.Count; i += batch * 2)
                    Assert.IsTrue(w.GrindGem(id, all.GetRange(i, Math.Min(batch * 2, all.Count - i)), coolant, 0.9, out string why), why);
                return w.Piece(id).gem.heat;
            }
            double big = Heat("emerald", 10, 0), small = Heat("emerald", 2, 0);
            Assert.Greater(big, 0.1, "dry and hard against the wheel, it heats");
            Assert.AreEqual(big, small, 1e-6, "sent ten touches at a time or two, the same heat");
            Assert.AreEqual(40 * CraftLapidary.GrindHeat(0.4, 0, true), big, 1e-6);
            Assert.AreEqual(0, Heat("emerald", 10, 1), 1e-9, "the drip keeps it cool");
            Assert.AreEqual(0, Heat("emerald", 10, 0, 0.15), 1e-9, "a light touch does not heat it");
            Assert.AreEqual(big * 0.25, Heat("rock_crystal", 10, 0), 1e-6, "a stone that is not heat-sensitive heats a quarter as much");

            CraftWorkshop ww = Workshop();
            string hot = Sawn(ww, "emerald", "round_brilliant");
            var burn = new List<float>();
            for (int k = 0; k < 60; k++) { burn.Add(k * 6f); burn.Add(0.5f); }
            for (int r = 0; r < 4; r++) Assert.IsTrue(ww.GrindGem(hot, burn, 0, 0.9, out string why), why);
            CraftPiece p = ww.Piece(hot);
            CraftDefect crack = p.defects.Single(d => d.kind == CraftDefectKind.Crack);
            Assert.AreEqual(p.gem.heat, crack.severity, 1e-6, "the crack is as bad as the heat - not compounded request after request");
        }

        [Test]
        public void TableOnTheCleavage_FlakesPassByPass_AndALightLapPolishesItSafely()
        {
            CraftPiece Faceted(CraftWorkshop w, string gem)
            {
                string id = Rough(w, gem, 11, 6);
                w.Piece(id).gem.inclusions.Clear();
                Assert.IsTrue(w.OrientGem(id, "emerald_cut", 0, 0, 0, 0.75, out string why), why);
                CraftGemCut cut = w.Rules.Cut("emerald_cut");
                double n = w.Rules.Gem(gem).refractiveIndex;
                for (int t = 0; t < cut.tiers.Count; t++)
                    Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, 0, 1, out why), why);
                CraftPiece p = w.Piece(id);
                p.gem.tableAngle = p.gem.cleaveTilt; // the table laid along the cleavage
                return p;
            }
            CraftWorkshop a = Workshop(), b = Workshop(), c = Workshop();
            CraftPiece once = Faceted(a, "topaz"), often = Faceted(b, "topaz"), light = Faceted(c, "topaz");
            Assert.IsTrue(a.PolishGem(once.id, "alumina", 24, 0.5, 0.5, out string why), why);
            for (int i = 0; i < 6; i++) Assert.IsTrue(b.PolishGem(often.id, "alumina", 4, 0.5, 0.5, out why), why);
            Assert.AreEqual(once.gem.chips, often.gem.chips, 1e-6, "24 passes at once or in fours: the same flaking");
            Assert.AreEqual(once.gem.polish, often.gem.polish, 1e-3);
            Assert.Greater(once.gem.chips, 0.1);
            Assert.Less(once.gem.polishRegions[CraftLapidary.TableRegion], CraftLapidary.DullPolish, "pressed that hard, the table never comes up");
            for (int i = 0; i < 8; i++) Assert.IsTrue(c.PolishGem(light.id, "alumina", 4, CraftLapidary.FlakePressure, 0.5, out why), why);
            Assert.AreEqual(0, light.gem.chips, 1e-9, "at the flake pressure and under, it never flakes");
            Assert.AreEqual(CraftGemStage.Polished, light.gem.stage, "and it polishes, if slowly");
        }

        [Test]
        public void CutStone_CarriesNoCrust()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.Greater(CraftLapidary.Dirt(w.Piece(id).gem), 0.1, "it comes crusted");
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            double n = w.Rules.Gem("rock_crystal").refractiveIndex;
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            Assert.IsTrue(w.FacetGem(id, 0, cut.tiers[0].facets, CraftLapidary.IdealAngle(cut.tiers[0], n), 0.05, 0.05, 1, out why), why);
            GemState g = w.Piece(id).gem;
            Assert.AreEqual(0, g.crust.Count, "the lap grinds through the skin");
            Assert.AreEqual(0, CraftLapidary.Dirt(g), 1e-9);
            // the window's preview of scrubbing uses the very removal the workshop applies
            Assert.AreEqual(1.2 * 0.9 * 1.0 * (0.6 + 0.4 * 0.5), CraftLapidary.CrustRemoval(1.2, true, 1.0, 0.5), 1e-9);
            Assert.AreEqual(3 * 0.35 * 1.0, CraftLapidary.CrustRemoval(5, false, 1.0, 1), 1e-9, "work counts up to 3 a sector");
        }

        [Test]
        public void CanFacet_SaysWhatFacetGemWouldRefuse_AndChangesNothing()
        {
            CraftWorkshop w = Workshop();
            string id = Rough(w, "rock_crystal", 3, 8);
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", 0, 0, 0, 0.6, out string why), why);
            var raised = new List<CraftEvent>();
            w.EventRaised += e => raised.Add(e);
            int ops = w.History.Count;
            Assert.IsFalse(w.CanFacet(id, 0, out string reason));
            StringAssert.Contains("Saw", reason);
            Assert.AreEqual(0, raised.Count, "asking raises nothing");
            Assert.AreEqual(ops, w.History.Count, "and records nothing");
            Assert.IsFalse(w.FacetGem(id, 0, 8, 43, 0, 0, 1, out string refused));
            Assert.AreEqual(reason, refused, "the same words as the refusal");
            for (int k = 0; k < 4; k++) Assert.IsTrue(w.SawGem(id, k * 45, 0.7, 0.9, 1, 0.3, out why), why);
            Assert.IsTrue(w.CanFacet(id, 0, out reason), reason);
            Assert.IsFalse(w.CanFacet(id, 2, out reason), "the tiers in order");
            StringAssert.Contains("in order", reason);
        }

        // ------------------------------------------------------------------ the jeweller
        [Test]
        public void Forming_UsesOnlyTheLengthThePartNeeds_AndPutsTheRestBack()
        {
            CraftWorkshop w = Workshop();
            string wire = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            double grams = w.Piece(wire).Mass(w.Rules);
            int pieces = w.Pieces.Count;
            Assert.IsTrue(w.FormShank(wire, CraftJewelPart.Band, 18, 0.9, 0.05, 0, out string why), why);
            CraftPiece band = w.Piece(wire);
            double need = CraftJewelry.LengthNeeded(CraftJewelPart.Band, 18, 1.8, 0);
            Assert.AreEqual(need, band.Length, 0.5, "cut to the length the band needs");
            Assert.AreEqual(pieces + 1, w.Pieces.Count, "the rest is a piece of its own");
            CraftPiece rest = w.Pieces.Last(p => p.kind == CraftPieceKind.Metal && p.id != wire);
            Assert.AreEqual(600 - need, rest.Length, 0.5);
            Assert.AreEqual(CraftJewelPart.None, CraftJewelry.PartOf(rest), "the rest is plain wire, not a formed band");
            Assert.AreEqual(0, rest.Tag(CraftJewelry.SizeTag), 1e-9);
            Assert.AreEqual(grams, band.Mass(w.Rules) + rest.Mass(w.Rules), grams * 0.01, "no metal lost or made");
            Assert.AreEqual(CraftJewelPart.Band, CraftJewelry.PartOf(band));
            StringAssert.Contains("mm used", band.record.Last().text);
            // the rest forms the next part
            Assert.IsTrue(w.FormShank(rest.id, CraftJewelPart.Prongs, 6, 0.9, 0.05, 4, out why), why);
            Assert.AreEqual(CraftJewelry.LengthNeeded(CraftJewelPart.Prongs, 6, 1.8, 4), w.Piece(rest.id).Length, 0.5);
            // and it replays
            CraftWorkshop replay = CraftWorkshop.Replay(Rules(), w.Tools, w.Ambient, w.Seed, w.Id, w.History, w.Step);
            Assert.AreEqual(w.Capture().ToJson(), replay.Capture().ToJson());
            // a part asked of a piece too short says so before any work
            Assert.IsFalse(w.CanFormShank(rest.id, CraftJewelPart.Chain, 100, 0, out string reason));
            StringAssert.Contains("too short", reason);
        }

        [Test]
        public void Seat_IsWordedByItsFit()
        {
            Assert.AreEqual(1, CraftJewelry.SeatFitOf(0.05), 1e-9);
            StringAssert.StartsWith("snug", CraftJewelry.SeatWords(0.05));
            StringAssert.StartsWith("a little wide", CraftJewelry.SeatWords(0.12));
            StringAssert.StartsWith("too wide", CraftJewelry.SeatWords(0.3));
            StringAssert.StartsWith("still too small", CraftJewelry.SeatWords(-0.2));
            CraftWorkshop w = Workshop();
            CraftPiece stone = CutStone(w, "sapphire", "cushion", 4);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0.05, 0, out string why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.2, 0.9, 0.9, out why), why);
            Assert.AreEqual("Seat cut - " + CraftJewelry.SeatWords(0.2), w.Piece(bezel).record.Last().text, "the record says what the window says");
        }

        [Test]
        public void Setting_MustSuitItsStone_CollarAndClaws()
        {
            CraftWorkshop w = Workshop();
            CraftPiece stone = CutStone(w, "sapphire", "cushion", 4);
            string wide = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(wide, CraftJewelPart.Bezel, 18, 0.9, 0.05, 0, out string why), why);
            Assert.IsFalse(w.SeatSetting(wide, stone.id, stone.gem.girdle, 0.9, 0.9, out why), "an 18 mm bezel cannot hold a small stone");
            StringAssert.Contains("too wide", why);
            Assert.AreEqual(0, CraftJewelry.CollarFit(18 - stone.gem.girdle), 1e-9);
            Assert.AreEqual(1, CraftJewelry.CollarFit(0.1), 1e-9);
            Assert.Less(CraftJewelry.CollarFit(1), 0.5, "a collar a millimetre wide holds the stone less");
            // a head of three prongs for a design of four claws
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string head = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 18, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.FormShank(head, CraftJewelPart.Prongs, stone.gem.girdle, 0.9, 0.05, 3, out why), why);
            string asm = w.StartAssembly("solitaire_ring", out why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsFalse(w.Attach(asm, "setting", 0, head, out why), "the solitaire holds its stone in four claws");
            StringAssert.Contains("4 prongs", why);
            Assert.AreEqual(4, CraftJewelry.ClawsWanted(w.Rules.Design("solitaire_ring"), w.Rules.Design("solitaire_ring").Component("setting")));
        }

        private static CraftPiece BezelRing(CraftWorkshop w, CraftPiece stone, double roundness, double gap, double clean, double ringSize = 18)
        {
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, ringSize, roundness, gap, 0, out string why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, roundness, gap, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.02, 0.95, clean, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, 20, 0.6, 0.9, out why), why);
            Assert.IsTrue(w.FinishJewel(asm, 0.9, 0.9, out why), why);
            Assert.IsTrue(w.Finish(asm, out why), why);
            return w.Piece(asm);
        }

        [Test]
        public void TheJewellersHands_CountInTheItem()
        {
            CraftWorkshop a = Workshop(), b = Workshop();
            CraftPiece fine = BezelRing(a, CutStone(a, "ruby", "oval"), 0.95, 0.02, 0.95);
            CraftPiece rough = BezelRing(b, CutStone(b, "ruby", "oval"), 0.2, 0.2, 0.1); // (a wider seam would not take the solder at all)
            CraftItemReport f = CraftEvaluator.Item(a.Rules, a, fine), r = CraftEvaluator.Item(b.Rules, b, rough);
            Assert.Greater(f.Workmanship, r.Workmanship + 0.15, "round, closed, a clean seat: better made");
            Assert.Greater(f.Quality, r.Quality);
            Assert.IsTrue(r.Notes.Any(n => n.Contains("out of round")), string.Join("; ", r.Notes));
            Assert.IsFalse(f.Notes.Any(n => n.Contains("wants 200-300") || n.Contains("too light")), "no smith's target shape for a band: " + string.Join("; ", f.Notes));
            Assert.IsFalse(f.Notes.Any(n => n.Contains("rough surface")), "buffed, its parts are not rough: " + string.Join("; ", f.Notes));
            Assert.Greater(f.Workmanship, 0.75);
        }

        [Test]
        public void Detaching_UnsoldersAndUnsets()
        {
            CraftWorkshop w = Workshop();
            CraftPiece stone = CutStone(w, "sapphire", "cushion", 4);
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 18, 0.9, 0.05, 0, out string why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.02, 0.95, 0.9, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, 20, 0.6, 0.9, out why), why);
            AssemblyState a = w.Piece(asm).assembly;
            Assert.Greater(a.Joint("seat").solder, 0.5);
            Assert.Greater(a.Joint("set").security, 0.6);
            Assert.IsTrue(w.Detach(asm, "stone", 0, out why), why);
            Assert.AreEqual(0, a.Joint("set").security, 1e-9, "a stone taken out is unset");
            Assert.AreEqual(0, a.Joint("set").pushes);
            Assert.IsTrue(w.Detach(asm, "setting", 0, out why), why);
            Assert.AreEqual(0, a.Joint("seat").solder, 1e-9, "a part taken out breaks its solder");
            Assert.IsFalse(CraftAssembly.JointDone(w.Rules.Design("bezel_ring").Joint("seat"), a.Joint("seat")));
        }

        [Test]
        public void Solder_FlowIsTheWindowsFlow()
        {
            Assert.AreEqual(0, CraftJewelry.SolderFlow(0.84, 1, 0), 1e-9, "short of its flow nothing joins");
            Assert.Less(CraftJewelry.SolderFlow(0.95, 0.6, 0.04), 0.5, "the 'flash' at 95% of the flow with thin flux does not take");
            Assert.GreaterOrEqual(CraftJewelry.SolderFlow(1.0, 1, 0), 0.99);
            Assert.Less(CraftJewelry.FluxReach(0.2, 0), 0.5, "fluxed that thinly it never takes, however hot");
            Assert.Less(CraftJewelry.FluxReach(1, 0.3), 0.5, "nor across a gaping seam");
        }

        [Test]
        public void Guard_HoldsBuffingToTimeOnTheWheel()
        {
            var guard = new CraftRequestGuard(3, 0.5);
            var full = new CraftRequest(CraftRequestKind.FinishJewel, "a") { a = 1f, b = 1f };
            Assert.IsTrue(guard.Check(ref full, 0, out _), "a full buff is in hand (two pieces buffed before either is inspected)");
            Assert.Greater(full.a, 0.8f);
            var second = new CraftRequest(CraftRequestKind.FinishJewel, "b") { a = 1f, b = 1f };
            Assert.IsTrue(guard.Check(ref second, 0.5, out _));
            Assert.Greater(second.a, 0.8f);
            var again = new CraftRequest(CraftRequestKind.FinishJewel, "c") { a = 1f, b = 1f };
            Assert.IsTrue(guard.Check(ref again, 1, out string why));
            Assert.Less(again.a, 0.3f, "a third full buff a second later is not");
            StringAssert.Contains("Too fast", why);
            guard.Refund();
            var later = new CraftRequest(CraftRequestKind.FinishJewel, "c") { a = 0.9f, b = 1f };
            Assert.IsTrue(guard.Check(ref later, 9, out _));
            Assert.Greater(later.a, 0.8f, "after the time a buff takes, it is");
        }

        [Test]
        public void OnlyARing_IsSizedToAFinger()
        {
            CraftWorkshop w = Workshop();
            CraftPiece stone = CutStone(w, "sapphire", "cushion", 4);
            Assert.IsTrue(w.MeasureFit(null, 0.95, 17.5, out string why), why);
            // a circlet: its band is round the brow, not a finger
            string circlet = w.StartAssembly("circlet", out why);
            Assert.IsNotNull(circlet, why);
            Assert.IsFalse(w.Piece(circlet).assembly.measured, "a circlet takes no finger");
            string band = Wire(w, "Gold", CraftDefaults.JewelleryForms[0]);
            string bezel = Wire(w, "Gold", CraftDefaults.JewelleryForms[1]);
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 170, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.02, 0.95, 0.9, out why), why);
            Assert.IsTrue(w.Attach(circlet, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(circlet, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(circlet, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(circlet, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(circlet, "stone", 0, stone.id, out why), why);
            Assert.IsTrue(w.JointWork(circlet, "set", CraftJointWork.Push, 20, 0.6, 0.9, out why), why);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(circlet));
            Assert.IsFalse(r.Notes.Any(n => n.Contains("finger") || n.Contains("sized by guess")), string.Join("; ", r.Notes));
            // the finger measured still waits for a ring
            string ring = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(w.Piece(ring).assembly.measured, "the ring takes the finger");
            Assert.AreEqual(17.5, w.Piece(ring).assembly.fitSize, 1e-6);
        }

        [Test]
        public void Measurements_WaitForTheItemThatTakesThem()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.MeasureFit(null, 0.9, 960, out string why), why); // the tape: a chest
            string ring = w.StartAssembly("bezel_ring", out why);
            Assert.IsFalse(w.Piece(ring).assembly.measured, "a chest does not size a ring");
            Assert.IsTrue(w.MeasureFit(null, 0.9, 18.2, out why), why); // the sizer: a finger
            string shirt = w.StartAssembly("linen_shirt", out why);
            Assert.IsNotNull(shirt, why);
            Assert.IsTrue(w.Piece(shirt).assembly.measured, "the shirt takes the chest that waited");
            Assert.AreEqual(960, w.Piece(shirt).assembly.fitSize, 1e-6, "not the finger");
            string ring2 = w.StartAssembly("bezel_ring", out why);
            Assert.AreEqual(18.2, w.Piece(ring2).assembly.fitSize, 1e-6, "the finger waited for a ring");
            // measured for a piece that does not take that kind: it waits instead
            Assert.IsTrue(w.MeasureFit(shirt, 0.9, 17, out why), why);
            Assert.AreEqual(960, w.Piece(shirt).assembly.fitSize, 1e-6);
            Assert.IsTrue(w.WaitingMeasurement(true, out _, out double finger));
            Assert.AreEqual(17, finger, 1e-6);
            // a finger kept the old way (in the chest's place) still counts as a finger
            CraftWorkshop old = Workshop();
            old.Variables["fit"] = 0.9;
            old.Variables["fitSize"] = 16.5;
            Assert.IsFalse(old.WaitingMeasurement(false, out _, out _), "no chest");
            string oldRing = old.StartAssembly("bezel_ring", out why);
            Assert.AreEqual(16.5, old.Piece(oldRing).assembly.fitSize, 1e-6);
        }
    }
}
#endif
