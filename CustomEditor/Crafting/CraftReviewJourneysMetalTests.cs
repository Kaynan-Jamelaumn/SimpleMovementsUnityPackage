#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the rules the metal journeys (raw stock to a finished item) showed wrong: a bloom hammered sound
    /// keeps its metal and is a bloom no longer; the grind strip asks for an edge however short of the design the stretch
    /// is, and what it calls done the stone hones; a part broken through makes a poor item; a solid charge in the crucible
    /// chills the melt (no gas until it has run); a preset station's tank keeps oil, and water is said to be harsh on a
    /// blade of hardening steel. (The journeys themselves: <see cref="CraftMetalJourneyTests"/>.)
    /// </summary>
    public class CraftReviewJourneysMetalTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, string medium = "water") => new CraftWorkshop(Rules(), tools, 18, 1, 3, medium, "metaljourneys");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static void SetHeat(CraftPiece p, double celsius)
        {
            foreach (MetalSegment s in p.segments)
                foreach (CraftZone z in new[] { s.edge, s.body })
                    z.temp = z.prevTemp = celsius;
        }

        private static double Slag(CraftPiece p) => p.segments.Sum(s => s.slag);

        /// <summary>A bloom as the furnace gives it: spongy (porosity 0.3) and slaggy, at welding heat on the anvil, no scale on it.</summary>
        private static string Bloom(CraftWorkshop w)
        {
            CraftPiece b = CraftDefaults.Bar(w.Rules, 80, 60, 35, Metal("Wrought iron"));
            b.bloom = true;
            foreach (MetalSegment s in b.segments) { s.porosity = 0.3; s.slag = 3; s.scale = 0; }
            b.AddDefect(CraftDefectKind.Inclusion, -1, 0.3, true, "slag in the bloom: work it at welding heat");
            string id = w.AddPiece(b);
            SetHeat(w.Piece(id), CraftForging.WeldingHeat(w.Rules, w.Piece(id).Profile(w.Rules)) - 20);
            return id;
        }

        /// <summary>A flat blow at welding heat (the heat kept, so only the blow works the bloom) on stretch <paramref name="k"/>.</summary>
        private static void WeldingBlow(CraftWorkshop w, string id, int k)
        {
            CraftPiece p = w.Piece(id);
            SetHeat(p, CraftForging.WeldingHeat(w.Rules, p.Profile(w.Rules)) - 20);
            foreach (MetalSegment s in p.segments) s.scale = 0;
            Assert.IsTrue(w.Strike(id, p.PositionOf(k % p.segments.Count), CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out string why), why);
        }

        // ------------------------------------------------------------------ r01: a bloom hammered sound keeps its metal
        [Test]
        public void Bloom_HammeredSound_KeepsItsMetal_EndsSmallerAndLighter()
        {
            CraftWorkshop w = Workshop();
            string id = Bloom(w);
            CraftPiece p0 = w.Piece(id);
            double mass0 = p0.Mass(w.Rules), slag0 = Slag(p0), metal0 = mass0 - slag0, volume0 = p0.segments.Sum(s => s.Volume(p0.edges));
            for (int k = 0; k < 120 && w.Piece(id).segments.Any(s => s.porosity > CraftForging.SoundPores); k++)
            {
                CraftPiece before = w.Piece(id);
                double metal = before.Mass(w.Rules) - Slag(before);
                WeldingBlow(w, id, k * 5);
                CraftPiece after = w.Piece(id);
                Assert.AreEqual(metal, after.Mass(w.Rules) - Slag(after), metal * 2e-3, $"blow {k}: the pores close, the metal stays as much as it was");
            }
            CraftPiece p = w.Piece(id);
            Assert.IsTrue(p.segments.All(s => s.porosity <= CraftForging.SoundPores), "the pores closed");
            Assert.AreEqual(metal0, p.Mass(w.Rules) - Slag(p), metal0 * 0.01, "no metal made or lost by the hammer");
            Assert.Less(p.Mass(w.Rules), mass0, "lighter: the slag driven out is gone");
            Assert.Less(p.segments.Sum(s => s.Volume(p.edges)), volume0 * 0.8, "and smaller: the pores' room went with them");
        }

        // ------------------------------------------------------------------ r11: a bloom hammered sound is a bar
        [Test]
        public void Bloom_OnceSound_IsABarOfIron_AndItsPurposeNamesItFirst()
        {
            CraftWorkshop w = Workshop();
            string id = Bloom(w);
            Assert.AreEqual("bloom", CraftLook.ShapeWord(w.Rules, w.Piece(id)), "a bloom as it comes from the furnace");
            w.Piece(id).intentDesign = "dagger";
            w.Piece(id).intentRole = "blade";
            Assert.AreEqual("blade", CraftLook.ShapeWord(w.Rules, w.Piece(id)), "what it is meant for names it before its being a bloom");
            w.Piece(id).intentDesign = w.Piece(id).intentRole = "";
            var told = new List<string>();
            w.EventRaised += e => { if (e.Kind == CraftEventKind.Note && e.Id == "consolidated") told.Add(e.Text); };
            for (int k = 0; k < 400 && w.Piece(id).bloom; k++)
                WeldingBlow(w, id, k * 7);
            CraftPiece p = w.Piece(id);
            Assert.IsFalse(p.bloom, "hammered sound at welding heat, it is a bloom no longer");
            Assert.IsTrue(CraftForging.Consolidated(w.Rules, p));
            Assert.LessOrEqual(Slag(p), CraftForging.SoundSlag * p.Mass(w.Rules) + 1e-9);
            Assert.AreEqual(1, told.Count, "said once, when it closed up");
            Assert.IsFalse(p.HasDefect(CraftDefectKind.Inclusion), "the slag the furnace left is worked out: no longer a flaw");
            Assert.AreNotEqual("bloom", CraftLook.ShapeWord(w.Rules, p));
            List<string> look = CraftLook.Describe(w.Rules, p, null, CraftInstruments.None, false, false, w);
            Assert.IsFalse(look.Any(l => l.Contains("lumpy") || l.Contains("cindery")), string.Join(" | ", look));
            // still porous, it is a bloom whatever the flag says once it is split off
            CraftWorkshop w2 = Workshop();
            string fresh = Bloom(w2);
            Assert.IsTrue(CraftLook.Bloom(w2.Rules, w2.Piece(fresh)));
            Assert.IsTrue(CraftLook.Describe(w2.Rules, w2.Piece(fresh), null, CraftInstruments.None, false, false, w2).Any(l => l.Contains("slag")));
        }

        // ------------------------------------------------------------------ r03: the strip asks for the edge however short the stretch
        private static MetalSegment Stretch(double width, double thickness, double edgeLeft = -1, double angle = 14)
        {
            var g = new MetalSegment { length = 18, width = width, thickness = thickness };
            if (edgeLeft >= 0)
            {
                double d = Math.Max(0, (thickness - edgeLeft) * 0.5);
                g.bevelDepthL = g.bevelDepthR = d;
                g.bevelWidthL = g.bevelWidthR = Math.Min(width, d / Math.Tan(angle * Math.PI / 180));
            }
            return g;
        }

        [Test]
        public void Judge_AStretchShortOfTheDesign_IsStillToGrindUntilItsEdgeIsMet()
        {
            CraftShapeTarget bt = Rules().Design("dagger").Component("blade").shape;
            bt.At(0.7f, out float tw, out float tt);
            float ta = 2f * (float)CraftSurface.EdgeHalfAngle(tw, tt, bt.edgeAngle, 2);
            double half = ta * 0.5;
            foreach (double thin in new[] { 0.0, 0.06, 0.15, 0.3 })
            {
                CraftSurface.Judgement j = CraftSurface.Judge(Stretch(tw, tt * (1 - thin)), tw, tt, ta, true, 2);
                Assert.AreEqual(CraftSurface.Progress.ToGrind, j.progress, $"{thin * 100:0}% thin, no bevel: its edge is still wanted");
                Assert.AreEqual(thin > 0.04, j.under, "the undersize is told apart");
                CraftSurface.Judgement started = CraftSurface.Judge(Stretch(tw * (1 - thin), tt, 1.5, half), tw, tt, ta, true, 2);
                Assert.AreEqual(CraftSurface.Progress.ToGrind, started.progress, $"{thin * 100:0}% narrow, bevels started: grind on");
            }
            // with the bevels met, a little short is off and far short too far - grinding cannot put it back
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(Stretch(tw, tt, 0.2, half), tw, tt, ta, true, 2).progress);
            Assert.AreEqual(CraftSurface.Progress.Off, CraftSurface.Judge(Stretch(tw, tt * 0.94, 0.2, half), tw, tt, ta, true, 2).progress);
            Assert.AreEqual(CraftSurface.Progress.TooFar, CraftSurface.Judge(Stretch(tw, tt * 0.85, 0.2, half), tw, tt, ta, true, 2).progress);
            // where no edge is wanted, the size is all there is to judge
            Assert.AreEqual(CraftSurface.Progress.TooFar, CraftSurface.Judge(Stretch(tw * 0.85, tt), tw, tt, 0, false).progress);
            Assert.AreEqual(CraftSurface.Progress.Off, CraftSurface.Judge(Stretch(tw * 0.95, tt), tw, tt, 0, false).progress);
        }

        // ------------------------------------------------------------------ r09: what the strip calls done the stone hones
        [Test]
        public void Stone_WhereTheStripIsGreen_WearsTheLastFlatAndHones()
        {
            Assert.AreEqual(CraftSurface.HoneStep.WearFlat, CraftSurface.NextHone(0, 0, 0, CraftSurface.EdgeMet + 0.05, true, 300, 7), "the bevels have not met: back to the wheel");
            Assert.AreEqual(CraftSurface.HoneStep.WearLastFlat, CraftSurface.NextHone(0, 0, 0, CraftSurface.EdgeMet, true, 300, 7), "met, as the strip says: the stone takes the last of it");
            Assert.AreEqual(CraftSurface.HoneStep.WearLastFlat, CraftSurface.NextHone(0, 0, 0, CraftSurface.FlatGone + 0.01, true, 300, 7));
            Assert.AreEqual(CraftSurface.HoneStep.HoneFace, CraftSurface.NextHone(0, 0, 0, CraftSurface.FlatGone, true, 300, 7));

            // a stretch the strip calls done, with a hair of flat left, is honed on the stone in a minute of strokes
            CraftWorkshop w = Workshop();
            CraftShapeTarget bt = w.Rules.Design("dagger").Component("blade").shape;
            CraftPiece blade = CraftDefaults.Bar(w.Rules, 430, 30, 5, Metal("Spring steel"));
            blade.intentDesign = "dagger";
            blade.intentRole = "blade";
            blade.edges = 2;
            string id = w.AddPiece(blade);
            CraftPiece p = w.Piece(id);
            int i = p.segments.Count * 3 / 4;
            bt.At((float)p.PositionOf(i), out float tw, out float tt);
            float ta = 2f * (float)CraftSurface.EdgeHalfAngle(tw, tt, bt.edgeAngle, 2);
            MetalSegment met = Stretch(tw, tt, CraftSurface.EdgeMet - 0.02, ta * 0.5);
            p.segments[i] = met;
            Assert.AreEqual(CraftSurface.Progress.Done, CraftSurface.Judge(met, tw, tt, ta, true, 2).progress, "the grind strip is green here");
            int strokes = 0;
            for (; strokes < 400 && w.Piece(id).segments[i].EdgeThickness > CraftSurface.FlatGone; strokes++)
                Assert.IsTrue(w.Sharpen(id, i, i, strokes % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "medium_stone", ta * 0.5 + 1, 0.5, 1, 1, true, out string why), why);
            Assert.Less(strokes, 400, "the stone wears the last of the flat away");
            for (int k = 0; k < 400 && Math.Min(w.Piece(id).segments[i].honeL, w.Piece(id).segments[i].honeR) < 1; k++)
                w.Sharpen(id, i, i, k % 2 == 0 ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, "medium_stone", ta * 0.5 + 1, 0.5, 1, 1, true, out _);
            Assert.AreEqual(1, Math.Min(w.Piece(id).segments[i].honeL, w.Piece(id).segments[i].honeR), 1e-9, "then both faces hone");
        }

        // ------------------------------------------------------------------ r05: one broken part, a poor item
        private static CraftPiece ToTarget(CraftRules rules, CraftShapeTarget t, double length, string metal)
        {
            CraftPiece p = CraftDefaults.Bar(rules, length, 10, 5, Metal(metal));
            double pos = 0;
            foreach (MetalSegment s in p.segments)
            {
                t.At((float)((pos + s.length * 0.5) / length), out float w, out float th);
                s.width = w;
                s.thickness = th;
                pos += s.length;
            }
            return p;
        }

        private static CraftItemReport Dagger(int bladeCracks, double crack = 0.27)
        {
            CraftRules rules = Rules();
            CraftDesign d = rules.Design("dagger");
            CraftPiece blade = ToTarget(rules, d.Component("blade").shape, 430, "Spring steel");
            blade.intentDesign = "dagger";
            blade.intentRole = "blade";
            blade.edges = 2;
            foreach (MetalSegment g in blade.segments)
            {
                g.edge.mart = g.body.mart = 0.9;
                g.edge.temperP = g.body.temperP = CraftMetallurgy.Temper(0, 220, 300, rules.Settings.temperTimeScale);
                g.apex = 3; g.honeL = g.honeR = 1; g.rough = 6; g.scale = 0;
            }
            for (int i = 0; i < bladeCracks; i++) blade.AddDefect(CraftDefectKind.Crack, 11 + i, crack, true, "cracked in the quench");
            CraftPiece guard = ToTarget(rules, d.Component("guard").shape, 90, "Mild steel");
            guard.segments[12].hole = 11;
            CraftPiece pommel = ToTarget(rules, d.Component("pommel").shape, 35, "Mild steel");
            pommel.segments[12].hole = 11;
            CraftPiece grip = CraftDefaults.WoodBlank(rules, "maple", 100, 26, 26);
            grip.segments[0].hole = 11;
            var parts = new Dictionary<string, List<CraftPiece>> { ["blade"] = new List<CraftPiece> { blade }, ["guard"] = new List<CraftPiece> { guard }, ["grip"] = new List<CraftPiece> { grip }, ["pommel"] = new List<CraftPiece> { pommel } };
            var joints = new AssemblyState { design = "dagger" };
            foreach (CraftJointSpec j in d.joints) joints.joints.Add(new JointState { joint = j.id, gap = 0.4, peen = 1.2, done = true });
            return CraftEvaluator.Item(rules, d, parts, joints);
        }

        [Test]
        public void Item_ABladeCrackedThrough_RatesPoor_SoundFittingsDoNotMakeItGood()
        {
            CraftItemReport sound = Dagger(0), nicked = Dagger(1, 0.15), broken = Dagger(13);
            Assert.Greater(sound.Quality, 0.75, sound.Tier);
            Assert.Less(CraftEvaluator.Piece(Rules(), broken.PartPieces.First(x => x.role == "blade").piece).Integrity, 0.05, "the blade is cracked through");
            Assert.Less(broken.Quality, 0.4, $"a dagger whose blade is cracked through rates {broken.Tier} ({broken.Quality:0.00})");
            Assert.Less(broken.Integrity, 0.15, "its soundness is its broken blade's, not the mean of its parts'");
            Assert.Less(broken.Durability, sound.Durability / 5, "it breaks early");
            CollectionAssert.Contains(broken.Notes, "Blade: cracked - it will break in use");
            CollectionAssert.DoesNotContain(sound.Notes, "Blade: cracked - it will break in use");
            // a small flaw costs what its soundness share says, no more
            Assert.Greater(nicked.Integrity, CraftEvaluator.Unsound);
            Assert.Greater(nicked.Quality, sound.Quality - 0.1, $"one small crack: {nicked.Quality:0.00} against {sound.Quality:0.00}");
            Assert.AreEqual(1, CraftEvaluator.SoundnessFactor(CraftEvaluator.Unsound), 1e-12);
            Assert.AreEqual(0.25, CraftEvaluator.SoundnessFactor(0), 1e-12);
        }

        // ------------------------------------------------------------------ r12: a solid charge chills the melt
        [Test]
        public void Crucible_ASolidChargeChillsTheMelt_ItTakesUpNoGasUntilItHasRun()
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            string copper = w.AddPiece(CraftDefaults.Bar(w.Rules, 120, 40, 20, Metal("Copper")));
            string tin = w.AddPiece(CraftDefaults.Bar(w.Rules, 60, 20, 12, Metal("Tin")));
            Assert.IsTrue(w.MovePiece(copper, CraftLocation.Melt, out string why), why);
            Assert.IsTrue(w.MovePiece(tin, CraftLocation.Melt, out why), why);
            // the crucible far over the tin's melting point, under the copper's: the tin runs, the copper lies solid in it
            for (int t = 0; t < 1500; t++)
            {
                w.Crucible.temperature = 1000;
                if (w.Piece(copper) != null) SetHeat(w.Piece(copper), Math.Min(1000, w.Piece(copper).MaxTemperature));
                w.RunSteps(1);
            }
            Assert.IsNull(w.Piece(tin), "the tin has run");
            Assert.IsNotNull(w.Piece(copper), "the copper is still solid");
            Assert.IsTrue(CraftSmelting.ChargeUnmelted(w));
            double superheat = w.Crucible.temperature - CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus;
            Assert.Greater(CraftSmelting.GasChange(superheat, w.Crucible.cover), 0, "the tin alone, this far over its liquidus, would take up gas");
            Assert.AreEqual(0, w.Crucible.gas, 1e-9, "chilled by the copper in it, it takes none up");
            StringAssert.Contains("solid metal still in it", CraftLook.Crucible(w.Rules, w.Crucible, null, false, w));
            // the copper melts: the bronze takes up gas as any melt held too hot does
            for (int t = 0; t < 3000 && w.Piece(copper) != null; t++)
            {
                w.Crucible.temperature = 1300;
                SetHeat(w.Piece(copper), 1300);
                w.RunSteps(1);
            }
            Assert.IsNull(w.Piece(copper), "the copper has run");
            Assert.IsFalse(CraftSmelting.ChargeUnmelted(w));
            for (int t = 0; t < 1000; t++) { w.Crucible.temperature = 1300; w.RunSteps(1); }
            superheat = w.Crucible.temperature - CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus;
            Assert.Greater(CraftSmelting.GasChange(superheat, w.Crucible.cover), 0);
            Assert.Greater(w.Crucible.gas, 0.05, "all molten and held far too hot, it takes up gas");
        }

        // ------------------------------------------------------------------ r04: a preset smithy keeps oil; water is harsh
        [Test]
        public void Quench_PresetTanksKeepOil_WaterIsSaidHarsh_OilHardensABladeWhole()
        {
            CraftRules rules = Rules();
            CollectionAssert.AreEquivalent(new[] { "water", "brine", "oil" }, CraftThermal.PresetQuenchants(rules, CraftTools.Smithy, false));
            CollectionAssert.AreEquivalent(new[] { "water", "brine", "oil" }, CraftThermal.PresetQuenchants(rules, CraftTools.Armourer, false));
            CollectionAssert.IsEmpty(CraftThermal.PresetQuenchants(rules, CraftTools.Smeltery, false), "no tank, nothing to fill it with");
            Assert.AreEqual(rules.Quenchants.Length, CraftThermal.PresetQuenchants(rules, CraftTools.Everything, true).Count, "a test station every quenchant");
            Assert.GreaterOrEqual(CraftThermal.CrackHarshness(rules.Quenchant("water")), CraftThermal.HarshQuench);
            Assert.GreaterOrEqual(CraftThermal.CrackHarshness(rules.Quenchant("brine")), CraftThermal.HarshQuench);
            Assert.Less(CraftThermal.CrackHarshness(rules.Quenchant("oil")), CraftThermal.HarshQuench);
            StringAssert.Contains("oil", CraftJournal.Insights["quench_crack"]);
            StringAssert.Contains("clay", CraftJournal.Insights["quench_crack"]);

            CraftShapeTarget bt = rules.Design("dagger").Component("blade").shape;
            CraftPiece Blade(string metal) => ToTarget(rules, bt, 430, metal);
            // the window's words: a spring-steel blade over water is warned of, mild steel (it does not harden) is not
            CraftPiece spring = Blade("Spring steel");
            string words = CraftLook.HarshQuench(rules, spring, rules.Quenchant("water"), CraftThermal.PresetQuenchants(rules, CraftTools.Smithy, false));
            StringAssert.Contains("quenching oil", words);
            StringAssert.Contains("clay its spine", words);
            Assert.AreEqual("", CraftLook.HarshQuench(rules, spring, rules.Quenchant("oil"), null), "oil is gentle");
            Assert.AreEqual("", CraftLook.HarshQuench(rules, Blade("Mild steel"), rules.Quenchant("water"), null), "mild steel does not harden");
            StringAssert.DoesNotContain("oil", CraftLook.HarshQuench(rules, spring, rules.Quenchant("water"), new[] { "water" }), "a tank kept with water only: the clay is the way");

            // what the warning says holds: water cracks the blade, oil hardens it whole
            foreach (string medium in new[] { "water", "oil" })
            {
                CraftWorkshop w = Workshop(CraftTools.Smithy, medium);
                string id = w.AddPiece(Blade("Spring steel"));
                CraftPiece p = w.Piece(id);
                foreach (MetalSegment s in p.segments) { s.edge.temp = s.body.temp = s.edge.prevTemp = s.body.prevTemp = 840; s.edge.aust = s.body.aust = 1; s.edge.pearl = s.body.pearl = 0; }
                Assert.IsTrue(CraftThermal.HarshFor(rules, p, rules.Quenchant(medium)) == (medium == "water"));
                Assert.IsTrue(w.MovePiece(id, CraftLocation.Quench, out string why), why);
                Assert.IsTrue(w.Agitate(id, CraftQuenchMotion.UpDown, out why), why);
                w.RunSteps(600);
                CraftPieceReport r = CraftEvaluator.Piece(rules, w.Piece(id));
                if (medium == "water")
                    Assert.IsTrue(w.Piece(id).HasDefect(CraftDefectKind.Crack), "water cracks a bare blade of spring steel");
                else
                {
                    Assert.IsFalse(w.Piece(id).HasDefect(CraftDefectKind.Crack), "oil does not");
                    Assert.Greater(r.Hardened, 0.3, "and hardens it");
                }
            }
        }
    }
}
#endif
