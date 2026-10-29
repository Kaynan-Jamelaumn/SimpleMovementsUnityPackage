#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of practice, craft by craft (<see cref="CraftMastery"/>): how it is earned, what each rank
    /// gives, the craft each piece of work belongs to, a master's finishing touches, and an older journal's practice.
    /// </summary>
    public class CraftMasteryTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "mastery");

        [Test]
        public void Mastery_GrowsWithExperience_EachStepHarderWon()
        {
            var s = new CraftMasterySettings();
            Assert.AreEqual(0, CraftMastery.Level(0, s), 1e-9);
            Assert.AreEqual(1 - Math.Exp(-1), CraftMastery.Level(s.experienceScale, s), 1e-9, "63% at the scale's experience");
            double a = CraftMastery.Level(10, s) - CraftMastery.Level(0, s), b = CraftMastery.Level(110, s) - CraftMastery.Level(100, s);
            Assert.Greater(a, b * 3, "the first ten points teach far more than ten more at a hundred");
            Assert.AreEqual(55, CraftMastery.ExperienceFor(CraftMastery.Level(55, s), s), 1e-6, "experience and mastery are inverses");
            Assert.AreEqual(CraftRank.Novice, CraftMastery.RankOf(0.1, s));
            Assert.AreEqual(CraftRank.Apprentice, CraftMastery.RankOf(s.apprentice, s));
            Assert.AreEqual(CraftRank.Journeyman, CraftMastery.RankOf(0.4, s));
            Assert.AreEqual(CraftRank.Expert, CraftMastery.RankOf(0.6, s));
            Assert.AreEqual(CraftRank.Master, CraftMastery.RankOf(0.9, s));
            Assert.AreEqual(0.5, CraftMastery.RankProgress((s.journeyman + s.expert) / 2, s), 1e-6);
            Assert.AreEqual("Journeyman tailor", CraftMastery.Title(CraftDiscipline.Tailoring, 0.4, s));
        }

        [Test]
        public void Practice_FromSmallWorkComesOnlySoFast_FinishedWorkAlwaysCounts()
        {
            var s = new CraftMasterySettings();
            var notes = new List<CraftMasteryNote>();
            for (int i = 0; i < 1000; i++)
                CraftMastery.Practise(notes, CraftDiscipline.Smithing, 0.01, true, 0, false, s);
            double burst = CraftMastery.Note(notes, CraftDiscipline.Smithing).xp;
            Assert.LessOrEqual(burst, s.workPerMinute * 2 + 1e-6, "a thousand idle blows at once teach no more than the allowance");
            CraftMastery.Practise(notes, CraftDiscipline.Smithing, 1, true, 600, false, s);
            Assert.AreEqual(burst + s.workPerMinute * 2, CraftMastery.Note(notes, CraftDiscipline.Smithing).xp, 1e-4, "ten minutes on, the allowance is full again (no more)");
            double before = CraftMastery.Note(notes, CraftDiscipline.Smithing).xp;
            CraftMastery.Practise(notes, CraftDiscipline.Smithing, 5, false, 600, true, s);
            Assert.AreEqual(before + 5, CraftMastery.Note(notes, CraftDiscipline.Smithing).xp, 1e-4, "finished work always counts");
            Assert.AreEqual(1, CraftMastery.Note(notes, CraftDiscipline.Smithing).finished);
            Assert.AreEqual(0, CraftMastery.Of(notes, CraftDiscipline.Tailoring, s), "and it is the smith's practice, not the tailor's");

            string line = null;
            var fresh = new List<CraftMasteryNote>();
            for (int i = 0; i < 20 && line == null; i++)
                line = CraftMastery.Practise(fresh, CraftDiscipline.Tailoring, CraftMastery.ItemExperience(Rules().Design("bezel_ring"), 0.8, s), false, 0, true, s);
            Assert.IsNotNull(line, "a new rank is announced");
            StringAssert.Contains("apprentice tailor", line);
            StringAssert.Contains("by routine", line, "with what it allows");
        }

        [Test]
        public void Ranks_GiveMoreWithEachAction_AndRoutinesAtTheirRank()
        {
            var s = new CraftMasterySettings();
            Assert.AreEqual(1, CraftMastery.StitchesPerAction(0, s));
            Assert.AreEqual(2, CraftMastery.StitchesPerAction(0.4, s));
            Assert.AreEqual(3, CraftMastery.StitchesPerAction(0.9, s));
            Assert.AreEqual(1, CraftMastery.RowsPerBeat(0.2, s));
            Assert.AreEqual(3, CraftMastery.RowsPerBeat(0.85, s));
            Assert.AreEqual(6, CraftMastery.FacetHits(16, 0, s), "a novice cuts six facets of a tier one by one");
            Assert.AreEqual(3, CraftMastery.FacetHits(16, 0.9, s), "a master three, the rest following");
            Assert.AreEqual(1, CraftMastery.FacetHits(1, 0.9, s), "a tier of one facet is one facet");
            Assert.Greater(CraftMastery.NeedlefulStitches(1), CraftMastery.NeedlefulStitches(0));
            Assert.Less(CraftMastery.FatigueFactor(1, s), CraftMastery.FatigueFactor(0, s), "a master tires less");

            Assert.IsFalse(CraftMastery.CanRoutine(CraftRoutine.WindSkein, 0.1, s), "a novice winds by hand");
            Assert.IsTrue(CraftMastery.CanRoutine(CraftRoutine.WindSkein, s.apprentice, s));
            Assert.IsFalse(CraftMastery.CanRoutine(CraftRoutine.SearchGem, s.journeyman, s), "searching a stone by routine wants an expert");
            Assert.IsTrue(CraftMastery.CanRoutine(CraftRoutine.SearchGem, s.expert, s));
            Assert.Less(CraftMastery.RoutineQuality(CraftRoutine.PickFibre, s.journeyman, s), CraftMastery.RoutineQuality(CraftRoutine.PickFibre, 1, s), "done by routine, as well as practice does it");
            Assert.LessOrEqual(CraftMastery.RoutineQuality(CraftRoutine.PickFibre, 1, s), s.routineHigh + 1e-9, "never a careful master's best");

            var hands = new CraftHands(3, 0) { fatigueRelief = s.fatigueRelief };
            var master = new CraftHands(3, 1) { fatigueRelief = s.fatigueRelief };
            for (int i = 0; i < 20; i++) { hands.Move(0.8); master.Move(0.8); }
            Assert.Less(master.fatigue, hands.fatigue * 0.7, "the same blows tire a master's arm less");
            Assert.Less(master.Unsteadiness, hands.Unsteadiness, "and it is steadier");

            List<string> benefits = CraftMastery.Benefits(CraftDiscipline.Tailoring, 0.9, s);
            Assert.IsTrue(benefits.Any(b => b.Contains("3 stitches")), string.Join(" | ", benefits));
            Assert.IsTrue(benefits.Any(b => b.Contains("By routine")));
            Assert.IsTrue(CraftMastery.Benefits(CraftDiscipline.Alchemy, 0.6, s).Any(b => b.Contains("roughly how good")));
        }

        [Test]
        public void EachPieceOfWork_BelongsToItsCraft()
        {
            CraftRules r = Rules();
            Assert.AreEqual(CraftDiscipline.Jewellery, CraftMastery.OfDesign(r.Design("bezel_ring")));
            var crafts = r.Designs.Select(CraftMastery.OfDesign).Distinct().ToList();
            foreach (CraftDiscipline c in new[] { CraftDiscipline.Smithing, CraftDiscipline.Leatherwork, CraftDiscipline.Tailoring, CraftDiscipline.Jewellery })
                Assert.Contains(c, crafts, $"some design is {c}");
            Assert.AreEqual(CraftDiscipline.Smithing, CraftMastery.OfDesign(r.Designs.First(d => d.IsWeaponLike && d.components.Any(c => c.kind == CraftPieceKind.Metal && c.importance >= 1))));

            CraftWorkshop w = Workshop();
            string cloth = w.AddPiece(CraftLeather.NewHide(r, "linen_cloth", 3));
            string leather = w.AddPiece(CraftLeather.NewHide(r, r.Hides.First(h => !h.textile).id, 3));
            string asm = w.StartAssembly("bezel_ring", out string why);
            Assert.IsNotNull(asm, why);
            Assert.AreEqual(CraftDiscipline.Tailoring, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.Cut, cloth)));
            Assert.AreEqual(CraftDiscipline.Leatherwork, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.Cut, leather)));
            Assert.AreEqual(CraftDiscipline.Jewellery, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.JointWork, asm)), "work on an item is the item's craft");
            Assert.AreEqual(CraftDiscipline.Smithing, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.Strike, "x")));
            Assert.AreEqual(CraftDiscipline.Jewellery, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.MeasureFit) { b = 18 }), "the ring sizer");
            Assert.AreEqual(CraftDiscipline.Tailoring, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.MeasureFit) { b = 950 }), "the tape");
            Assert.AreEqual(CraftDiscipline.Tailoring, CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.Routine) { i0 = (int)CraftRoutine.WindSkein }));
            Assert.IsNull(CraftMastery.OfRequest(r, w, new CraftRequest(CraftRequestKind.Bellows)), "pumping the bellows is nobody's craft");
            Assert.AreEqual(0, CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.Routine)), "work done by routine teaches nothing");
            Assert.Greater(CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.SpinFibre)), CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.Strike)), "a bobbin spun teaches more than a blow");
        }

        [Test]
        public void RawFibre_SpunFromTheBag_NeverSpinsFine()
        {
            CraftRules r = Rules();
            CraftSupplyDefinition wool = r.Supply("wool_fibre");
            double raw = CraftTextiles.RawPrep(r, "wool_fibre");
            Assert.Less(raw, 0.5, "greasy, tangled fleece is far from ready to spin");
            Assert.AreEqual(wool.spinsInto[2], CraftTextiles.Spun(r, wool, 1, 0.5), "a prepared batch spun perfectly: fine thread");
            Assert.AreNotEqual(wool.spinsInto[2], CraftTextiles.Spun(r, wool, 1, 0.5, raw), "the same hands on raw fleece: never fine - prepare it at the fibre bench");
        }

        [Test]
        public void Guard_LetsRoutinesThroughOneAtATime_AndOnlyKnownTools()
        {
            var guard = new CraftRequestGuard(5, 0);
            var routine = new CraftRequest(CraftRequestKind.Routine, "y") { i0 = (int)CraftRoutine.WindSkein };
            Assert.IsTrue(guard.Check(ref routine, 0, out _));
            int passed = 0;
            for (int i = 0; i < 10; i++)
            {
                var again = new CraftRequest(CraftRequestKind.Routine, "y") { i0 = (int)CraftRoutine.WindSkein };
                if (guard.Check(ref again, 0.01 * i, out _)) passed++;
            }
            Assert.Less(passed, 5, "not a flood of routines");
            var unknown = new CraftRequest(CraftRequestKind.Routine, "y") { i0 = 99 };
            Assert.IsFalse(guard.Check(ref unknown, 30, out _), "no such work");
            var stitch = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Stitch, count = 2, i1 = 7, a = 0.6f };
            Assert.IsTrue(guard.Check(ref stitch, 60, out _));
            Assert.AreEqual(0, stitch.i1, "an unknown tool counts as one stitch a beat");
            var needle = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Stitch, count = 2, i1 = 1, a = 0.6f };
            Assert.IsTrue(guard.Check(ref needle, 90, out _));
            Assert.AreEqual(1, needle.i1, "the needle: the station counts its run from the player's practice");
        }

        [Test]
        public void APractisedHand_DoesMoreWithEachAction_CountedWhereTheRulesRun()
        {
            var s = new CraftMasterySettings();
            CraftRequest Needle(CraftJointWork work, int tool) => new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)work, count = 1, i1 = tool };
            CraftRequest master = Needle(CraftJointWork.Stitch, 1), novice = Needle(CraftJointWork.Stitch, 1);
            CraftMastery.ByPractice(ref master, 0.9, s);
            CraftMastery.ByPractice(ref novice, 0, s);
            Assert.AreEqual(3, master.count, "a master's needle lays three stitches a click");
            Assert.AreEqual(1, novice.count, "a novice's one");
            Assert.AreEqual(0, master.i1, "the workshop sees stitches, not the tool");
            CraftRequest iron = Needle(CraftJointWork.Punch, 2);
            CraftMastery.ByPractice(ref iron, 0, s);
            Assert.AreEqual(CraftMastery.HolesPerBeat, iron.count, "the pricking iron punches all its teeth, whoever holds it");
            CraftRequest plain = Needle(CraftJointWork.Stitch, 0);
            CraftMastery.ByPractice(ref plain, 1, s);
            Assert.AreEqual(1, plain.count, "no tool named: one at a time");

            var reed = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i1 = 12, points = Enumerable.Repeat(0.9f, 12).ToList() };
            CraftMastery.ByPractice(ref reed, 0.9, s);
            Assert.AreEqual(30, reed.i1, "three rows a beat - never more than a cloth's thirty");
            Assert.AreEqual(30, reed.points.Count);
            Assert.IsTrue(reed.points.All(p => Math.Abs(p - 0.9f) < 1e-6), "each row as well beaten as its beat");
        }

        [Test]
        public void Routines_DoTheWork_AsWellAsPracticeDoesIt()
        {
            var s = new CraftMasterySettings();
            CraftWorkshop w = Workshop();
            string fleece = w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre"));
            double dirt = w.Piece(fleece).fibre.dirt, tangle = w.Piece(fleece).fibre.tangle;
            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.PickFibre, 0.5, fleece, "Ann", s, out string why), why);
            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.TeaseFibre, 0.5, fleece, "Ann", s, out why), why);
            Assert.Less(w.Piece(fleece).fibre.dirt, dirt * 0.6, "picked over");
            Assert.Less(w.Piece(fleece).fibre.tangle, tangle * 0.7, "and teased open");

            // a skein counted: closer the more practised, never more than is there
            var errors = new List<double>();
            foreach (double m in new[] { 0.2, 1.0 })
            {
                string y = w.SpinFibre(w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre")), 0.85, 0.5, 0.6, 30, out why);
                Assert.IsNotNull(y, why);
                Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.WindSkein, m, y, "Ann", s, out why), why);
                YarnState yarn = w.Piece(y).yarn;
                Assert.IsTrue(yarn.measured);
                Assert.LessOrEqual(yarn.measuredLength, yarn.length + 1e-6);
                errors.Add(yarn.measureError);
            }
            Assert.Less(errors[1], errors[0], "a master's count is closer than an apprentice's");
            Assert.Less(errors[1], 0.05, "close enough to reel it all");
            Assert.IsFalse(CraftMastery.DoRoutine(w, CraftRoutine.WindSkein, 1, fleece, "Ann", s, out why), "loose fibre is no bobbin");

            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.MeasureWearer, 0.6, null, "Ann", s, out why), why);
            Assert.AreEqual(CraftTextiles.ChestOf("Ann"), w.Variables["fitSize"], 60, "the wearer's own chest");
            Assert.GreaterOrEqual(w.Variables["fit"], s.routineLow - 0.04);
            string ring = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.SizeFinger, 0.9, ring, "Ann", s, out why), why);
            Assert.AreEqual(CraftJewelry.FingerOf("Ann"), w.Piece(ring).assembly.fitSize, 0.2, "the ring takes the wearer's finger");

            // a stone cleaned and searched by routine: everything under the crust found
            string stone = null;
            for (int seed = 1; seed < 50 && stone == null; seed++)
            {
                CraftPiece p = CraftLapidary.NewRough(w.Rules, "emerald", seed);
                if (p.gem.inclusions.Count >= 3) stone = w.AddPiece(p);
            }
            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.CleanGem, 1, stone, "Ann", s, out why), why);
            Assert.Less(CraftLapidary.Dirt(w.Piece(stone).gem), 0.05, "brushed clean");
            Assert.AreEqual(0, w.Piece(stone).gem.scratches, 1e-9, "by brush: not a mark on it");
            Assert.IsTrue(CraftMastery.DoRoutine(w, CraftRoutine.SearchGem, 1, stone, "Ann", s, out why), why);
            Assert.IsTrue(w.Piece(stone).gem.evaluated);
            Assert.IsTrue(w.Piece(stone).gem.inclusions.All(i => i.found), "an expert's methodical search misses nothing");

            CraftWorkshop replay = CraftWorkshop.Replay(Rules(), w.Tools, w.Ambient, w.Seed, w.Id, w.History, w.Step);
            Assert.AreEqual(w.Piece(stone).gem.inclusions.Count(i => i.found), replay.Piece(stone).gem.inclusions.Count(i => i.found), "and it replays");
            Assert.AreEqual(w.Piece(ring).assembly.fitSize, replay.Piece(ring).assembly.fitSize, 1e-6);
        }

        // ------------------------------------------------------------------ a master's finishing touches
        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static CraftPiece Ring(CraftWorkshop w)
        {
            string id = w.AddPiece(CraftLapidary.NewRough(w.Rules, "ruby", 9, 4));
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, "oval", w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            CraftGemCut cut = w.Rules.Cut("oval");
            double n = w.Rules.Gem("ruby").refractiveIndex;
            for (int t = 0; t < cut.tiers.Count; t++)
                Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0, 0, 1, out why), why);
            Assert.IsTrue(w.PolishGem(id, "alumina", 14, 0.4, 0.5, out why), why);
            CraftPiece stone = w.Piece(id);
            string band = w.AddPiece(CraftDefaults.Bar(w.Rules, 600, 2.5, 1.8, Metal("Gold")));
            string bezel = w.AddPiece(CraftDefaults.Bar(w.Rules, 150, 4, 0.5, Metal("Gold")));
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 18, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.05, 0.9, 0.85, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.8, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, 16, 0.6, 0.8, out why), why);
            Assert.IsTrue(w.FinishJewel(asm, 0.8, 0.8, out why), why);
            return w.Piece(asm);
        }

        [Test]
        public void AMastersFinishingTouches_RaiseWhatTheWorkLacks()
        {
            CraftWorkshop a = Workshop(), b = Workshop();
            CraftPiece novice = Ring(a), master = Ring(b);
            Assert.IsTrue(a.Finish(novice.id, out string why, 0), why);
            Assert.IsTrue(b.Finish(master.id, out why, 1), why);
            Assert.AreEqual(1, master.assembly.mastery, 1e-6, "the finisher's practice is recorded with the item");
            CraftItemReport rn = CraftEvaluator.Item(a.Rules, a, novice), rm = CraftEvaluator.Item(b.Rules, b, master);
            Assert.Greater(rm.Quality, rn.Quality, "the same work finished by a master is better");
            double touch = new CraftMasterySettings().finishingTouch;
            Assert.AreEqual(rn.Quality + (1 - rn.Quality) * touch, rm.Quality, 1e-6, "by the touch's share of what it lacked");
            Assert.IsTrue(rm.Notes.Any(n => n.Contains("master's finishing touches")));
            Assert.Greater(rm.Price, rn.Price);
            CraftWorkshop replay = CraftWorkshop.Replay(Rules(), b.Tools, b.Ambient, b.Seed, b.Id, b.History, b.Step);
            Assert.AreEqual(1, replay.Piece(master.id).assembly.mastery, 1e-6, "and it replays");
        }

        [Test]
        public void AnOlderJournal_TurnsItsWorkIntoPractice_Once()
        {
            var j = new CraftJournalData();
            j.designs.Add(new CraftDesignNote { designId = "bezel_ring", timesMade = 6, bestQuality = 0.7f });
            j.insights.Add("felting");
            j.insights.Add("quench_crack");
            j.gems.Add("ruby");
            Assert.Greater(CraftJournal.HandSkill(j), 0, "before: one skill from everything made");
            CraftMastery.Seed(j, Rules());
            Assert.Greater(CraftMastery.Of(j, CraftDiscipline.Jewellery), 0.1, "rings made: a jeweller's practice");
            Assert.Greater(CraftMastery.Of(j, CraftDiscipline.Tailoring), 0, "an insight into felting: a little tailoring");
            Assert.Greater(CraftMastery.Of(j, CraftDiscipline.Smithing), 0, "and into quenching: a little smithing");
            Assert.AreEqual(0, CraftMastery.Of(j, CraftDiscipline.Woodwork));
            double once = CraftMastery.Of(j, CraftDiscipline.Jewellery);
            CraftMastery.Seed(j, Rules());
            Assert.AreEqual(once, CraftMastery.Of(j, CraftDiscipline.Jewellery), 1e-9, "only once");
            CraftJournalData back = CraftJournalData.FromJson(j.ToJson());
            Assert.AreEqual(once, CraftMastery.Of(back, CraftDiscipline.Jewellery), 1e-6, "it is saved");
            Assert.IsTrue(back.masterySeeded);
            Assert.IsNotNull(CraftJournalData.FromJson("{\"version\":1}").mastery, "an old file reads with an empty practice");

            var fresh = new CraftJournalData();
            CraftJournal.Learn(fresh, "pavilion_angle");
            Assert.Greater(CraftMastery.Of(fresh, CraftDiscipline.Jewellery), 0, "an insight is practice in its craft");
        }
    }
}
#endif
