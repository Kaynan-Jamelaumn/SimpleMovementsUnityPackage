#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the textile's review (the fibre bench, the wheel, the loom and the tailor's table): a cloth is
    /// as sound as the yarn it was woven from and each row as its beat; plying forgets every strand's count; a chest's
    /// measure goes into a garment (what its fit counts for), never a shield started first; an item says when its thread
    /// was too weak for the cloth; the reed's beats and the tape are no truer than the hands, in the window and at the
    /// station alike; a scour soaks no longer than the real time it had.
    /// </summary>
    public class CraftReviewTextileTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "textilerev");

        private static string Batch(CraftWorkshop w, string fibre = "wool_fibre") => w.AddPiece(CraftFibre.NewFibre(w.Rules, fibre));

        private static string Bobbin(CraftWorkshop w, double even = 0.85, double twist = 0.5, double fine = 0.5)
        {
            string y = w.SpinFibre(Batch(w), even, twist, fine, CraftFibre.BobbinGrams, out string why);
            Assert.IsNotNull(y, why);
            return y;
        }

        /// <summary>A length of cloth 2 wide woven from one bobbin spun with <paramref name="twist"/>, every row beaten <paramref name="beat"/>.</summary>
        private static (CraftWorkshop w, string cloth, YarnState yarn) Woven(double twist, float beat, double even = 0.85, int seed = 7)
        {
            CraftWorkshop w = Workshop(seed: seed);
            string y = Bobbin(w, even, twist, 1);
            YarnState spun = w.Piece(y).yarn;
            var copy = new YarnState { fibre = spun.fibre, twist = spun.twist, evenness = spun.evenness, strength = spun.strength, length = spun.length, plies = spun.plies };
            int rows = CraftFibre.RowsFor(spun.length, 2);
            string cloth = w.WeaveCloth(y, 2, 0.9, 0.5, Enumerable.Repeat(beat, rows).ToList(), out string why);
            Assert.IsNotNull(cloth, why);
            return (w, cloth, copy);
        }

        // ------------------------------------------------------------------ the loom
        [Test]
        public void ACloth_IsAsSoundAsItsYarn_AKinkedYarnWeavesAWeakerCloth()
        {
            var right = Woven(0.5, 0.85f);
            var kinked = Woven(0.95, 0.85f);
            Assert.Less(kinked.yarn.strength, right.yarn.strength * 0.75, "precondition: the over-twisted yarn is much weaker");
            CraftPieceReport r = CraftEvaluator.Piece(right.w.Rules, right.w.Piece(right.cloth)), k = CraftEvaluator.Piece(kinked.w.Rules, kinked.w.Piece(kinked.cloth));
            Assert.Less(k.Strength, r.Strength * 0.97, "cloth woven of a weak, kinked yarn is weaker than cloth of a rightly twisted one");
            Assert.Greater(k.Stiffness, r.Stiffness, "an over-twisted yarn weaves a harsh, wiry cloth");
            Assert.Less(CraftFibre.WeaveSoundness(right.w.Rules, kinked.yarn), CraftFibre.WeakYarn, "precondition: the kinked yarn is called weak");
            StringAssert.Contains("of a weak, kinked yarn", kinked.w.Piece(kinked.cloth).record.Last().text, "the cloth's record says why");
            StringAssert.DoesNotContain("weak", right.w.Piece(right.cloth).record.Last().text);
        }

        [Test]
        public void TruerBeats_WeaveASounderCloth()
        {
            // the beat bar: Poor 0.26 (just above a skipped pick) against Perfect on every row - the same yarn, the same loom
            var poor = Woven(0.5, 0.26f);
            var perfect = Woven(0.5, 1f);
            CraftPieceReport p = CraftEvaluator.Piece(poor.w.Rules, poor.w.Piece(poor.cloth)), q = CraftEvaluator.Piece(perfect.w.Rules, perfect.w.Piece(perfect.cloth));
            Assert.Greater(q.Strength, p.Strength, "rows beaten true are packed close: a stronger cloth");
            Assert.LessOrEqual(q.Damage, p.Damage, "and no more flawed");
            Assert.Greater(q.Integrity, p.Integrity * 0.999, "nor less whole");
            Assert.AreEqual(1, CraftFibre.RowSoundness(1), 1e-9);
            Assert.Less(CraftFibre.RowSoundness(0.26), CraftFibre.RowSoundness(0.8));
        }

        [Test]
        public void AWovenCloth_LooksAsSoundAsItWasWoven()
        {
            var kinked = Woven(0.95, 0.3f);
            var right = Woven(0.5, 1f, 0.95);
            List<string> weak = CraftLook.Describe(kinked.w.Rules, kinked.w.Piece(kinked.cloth), null, CraftInstruments.None, false, false),
                sound = CraftLook.Describe(right.w.Rules, right.w.Piece(right.cloth), null, CraftInstruments.None, false, false);
            Assert.IsTrue(weak.Any(l => l.Contains("loose and weak")), "a cloth of a kinked yarn, beaten unevenly: " + string.Join(" | ", weak));
            Assert.IsFalse(sound.Any(l => l.Contains("loose and weak")), "a cloth of a good yarn, beaten true: " + string.Join(" | ", sound));
        }

        // ------------------------------------------------------------------ plying
        [Test]
        public void Plying_ForgetsTheCountOfEveryStrand()
        {
            CraftWorkshop w = Workshop();
            string shortOne = Bobbin(w, fine: 0.2), longOne = Bobbin(w, fine: 0.8);
            Assert.IsTrue(w.MeasureYarn(longOne, w.Piece(longOne).yarn.length, out string why), why);
            Assert.IsTrue(w.PlyYarn(shortOne, longOne, null, 0.4, 0.9, out why), why);
            YarnState rest = w.Piece(longOne).yarn;
            Assert.Greater(rest.length, 1, "precondition: the longer bobbin keeps its rest");
            Assert.IsFalse(rest.measured, "what is left on the bobbin is not the skein that was counted");
            Assert.AreEqual(0, CraftFibre.ThreadCount(w.Piece(longOne)), "it reels nothing until it is counted again");
            Assert.IsFalse(w.Piece(shortOne).yarn.measured, "nor is the plied yarn counted");
        }

        // ------------------------------------------------------------------ the measure
        [Test]
        public void AChestMeasure_WaitsForAGarment_NotAShieldStartedFirst()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.MeasureFit(null, 0.9, CraftTextiles.ChestOf("wearer"), out string why), why);
            string shield = w.StartAssembly("round_shield", out why);
            Assert.IsNotNull(shield, why);
            Assert.IsFalse(w.Piece(shield).assembly.measured, "a shield is not made to measure");
            string cloak = w.StartAssembly("wool_cloak", out why);
            Assert.IsNotNull(cloak, why);
            Assert.IsTrue(w.Piece(cloak).assembly.measured, "the cloak takes the measure taken for it");
            // measured with the shield picked: the measure waits for the next garment
            Assert.IsTrue(w.MeasureFit(shield, 0.85, CraftTextiles.ChestOf("wearer"), out why), why);
            Assert.IsFalse(w.Piece(shield).assembly.measured);
            Assert.IsTrue(w.WaitingMeasurement(false, out double fit, out _), "it waits");
            Assert.AreEqual(0.85, fit, 1e-6);
        }

        [Test]
        public void WhatTakesAChestMeasure_IsWhatItsFitCountsFor()
        {
            int garments = 0;
            foreach (CraftDesign d in Rules().Designs)
            {
                bool cloth = d.kind != CraftDesignKind.Jewelry && d.components.Any(c => c != null && c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric == CraftFabric.Cloth);
                Assert.AreEqual(cloth, CraftJewelry.Takes(d, false), $"{d.id}: takes a chest's measure only where its fit counts");
                Assert.AreEqual(cloth, CraftTextiles.MadeToMeasure(d), d.id);
                if (cloth) garments++;
            }
            Assert.Greater(garments, 3, "the tailor's garments take it");
            Assert.IsFalse(CraftJewelry.Takes(Rules().Design("round_shield"), false));
        }

        // ------------------------------------------------------------------ the thread
        /// <summary>A wool hood with both sides fitted, its thread in (strength <paramref name="thread"/>), the crown seam punched and sewn at the ideal pull.</summary>
        private static CraftItemReport Hood(double thread)
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "wool_cloth", 3));
            string asm = w.StartAssembly("cloth_hood", out string why);
            Assert.IsNotNull(asm, why);
            for (int k = 0; k < 2; k++)
            {
                string side = w.CutPattern(bolt, "hood", 0, k * 5, 0, 0.95, out why);
                Assert.IsNotNull(side, why);
                Assert.IsTrue(w.Attach(asm, "sides", k, side, out why), why);
            }
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, thread), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Punch, 18, 0.05, 0.05, out why), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 18, CraftAssembly.StitchTensionIdeal, 0.03, out why), why);
            return CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
        }

        [Test]
        public void ASeamSewnWithTooWeakAThread_SaysWhy()
        {
            CraftItemReport weak = Hood(0.6), strong = Hood(1.05);
            Assert.Less(weak.Assembly, strong.Assembly * 0.8, "precondition: the thread costs the seam a lot");
            string notes = string.Join(" | ", weak.Notes);
            Assert.IsTrue(weak.Notes.Any(n => n.Contains("too weak a thread for the cloth")), "the item says its thread was too weak for the cloth: " + notes);
            Assert.IsFalse(strong.Notes.Any(n => n.Contains("thread")), "a thread that suits it is not mentioned: " + string.Join(" | ", strong.Notes));
        }

        // ------------------------------------------------------------------ the needle
        /// <summary>A linen coif with both sides fitted, its thread in unless <paramref name="thread"/> is false, nothing punched.</summary>
        private static string Coif(CraftWorkshop w, bool thread = true)
        {
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            string asm = w.StartAssembly("linen_coif", out string why);
            Assert.IsNotNull(asm, why);
            for (int k = 0; k < 2; k++)
            {
                string side = w.CutPattern(bolt, "coif", 0, k * 5, 0, 0.95, out why);
                Assert.IsNotNull(side, why);
                Assert.IsTrue(w.Attach(asm, "sides", k, side, out why), why);
            }
            if (thread) Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1), why);
            return asm;
        }

        [Test]
        public void TheNeedlesPull_PiercesTheHolesItsStitchesNeed_InOneRequest()
        {
            CraftWorkshop w = Workshop();
            string asm = Coif(w);
            JointState j() => w.Piece(asm).assembly.Joint("crown_seam");
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 0.6, 0.05, out string why, true, 0.1, 0.05, 0), why);
            Assert.AreEqual(6, j().stitches, "the stitches go in");
            Assert.AreEqual(6, j().holes, "through the holes the needle pierced as it drew them");
            Assert.AreEqual(0.1 / w.ToolQuality, j().spacingError, 1e-6, "spaced as the hand spaced them");
            // holes punched before (an awl, an earlier pierce) are used first: only the rest are pierced
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Punch, 3, 0.05, 0.05, out why), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 5, 0.6, 0.05, out why, true, 0.1, 0.05, 0), why);
            Assert.AreEqual(11, j().stitches);
            Assert.AreEqual(11, j().holes, "three holes were there: two more pierced");
            // never more holes than the seam takes, never more stitches
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 9, 0.6, 0.05, out why, true, 0.1, 0.05, 0), why);
            Assert.AreEqual(14, j().stitches, "sewn all along");
            Assert.AreEqual(14, j().holes);
            Assert.IsFalse(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 1, 0.6, 0.05, out why, true), "nothing more to sew");
            StringAssert.Contains("sewn all along", why);
        }

        [Test]
        public void ANeedlesPullRefused_PiercesNothing_AndWithoutItThePunchComesFirst()
        {
            CraftWorkshop w = Workshop();
            string asm = Coif(w, thread: false);
            Assert.IsFalse(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 0.6, 0.05, out string why, true, 0.1, 0.05, 0), "no thread in it yet");
            StringAssert.Contains("thread", why);
            Assert.AreEqual(0, w.Piece(asm).assembly.Joint("crown_seam").holes, "a refused pull pierces nothing");
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1), why);
            Assert.IsFalse(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 0.6, 0.05, out why), "the awl's stitch wants its holes punched first");
            StringAssert.Contains("Punch the holes first", why);
        }

        [Test]
        public void AFlushIsOneRequest_ItsStitchesLandBeforeTheItemIsTaken()
        {
            // the window's flush as the item is taken: the thread, then the pulls, then the Take - in order, nothing waiting on an answer
            CraftWorkshop w = Workshop();
            string asm = Coif(w, thread: false);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out string why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 4, 0.6, 0.05, out why, true, 0.1, 0.05, 0), why);
            Assert.AreEqual(4, w.Piece(asm).assembly.Joint("crown_seam").stitches, "the four stitches are on the seam before the item goes into the bag");
        }

        [Test]
        public void OnlyTheStitchesPulledPastTheCut_CutTheSeam()
        {
            CraftWorkshop w = Workshop();
            string asm = Coif(w);
            double each = 0.05 / 14.0;
            // one pull of six past the cut (0.95, the rest 0.6): its mean 0.658 is under the cut, the one stitch still cuts
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 0.658, 0.13, out string why, true, 0.05, 0.05, 1), why);
            Assert.AreEqual(each, w.Piece(asm).assembly.Joint("crown_seam").damage, 1e-9, "the stitch the PULL gauge showed red cut the cloth");
            // three of six past it (0.95 ×3, 0.86 ×3: mean 0.905 over the cut) - three cut, not six
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 0.905, 0.045, out why, true, 0.05, 0.05, 3), why);
            Assert.AreEqual(4 * each, w.Piece(asm).assembly.Joint("crown_seam").damage, 1e-9);
            // a run without the count (a single pull, an older history) is judged by its pull
            CraftWorkshop old = Workshop();
            string asm2 = Coif(old);
            Assert.IsTrue(old.JointWork(asm2, "crown_seam", CraftJointWork.Punch, 2, 0.05, 0.05, out why), why);
            Assert.IsTrue(old.JointWork(asm2, "crown_seam", CraftJointWork.Stitch, 2, 0.95, 0.01, out why), why);
            Assert.AreEqual(2 * each, old.Piece(asm2).assembly.Joint("crown_seam").damage, 1e-9);
        }

        [Test]
        public void Guard_TheNeedlesPierceAndDraw_IsAWholeStitchsTime_AndCountsItsHardPulls()
        {
            var guard = new CraftRequestGuard(5, 0);
            CraftRequest Pull(int pulls, int hard) => new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Stitch, count = pulls, i1 = 1, flag = true, i2 = hard, a = 0.6f, b = 0.1f, c = 0f, d = 0f };
            double t = 0;
            for (int k = 0; k < 15; k++)
            {
                // sewing at one and a half pulls a second for a minute, flushed every six pulls: all of it counts
                t += 6 / 1.5;
                CraftRequest r = Pull(6, 2);
                Assert.IsTrue(guard.Check(ref r, t, out string why), why);
                Assert.AreEqual(6, r.count, "every pull the window drew is laid");
                Assert.IsTrue(r.flag, "it pierces as it draws");
                Assert.AreEqual(2, r.i2, "its hard pulls");
                Assert.Greater(r.c, 0f, "the pierce is spaced no truer than the hands");
                Assert.Greater(r.d, 0f);
            }
            // a burst: sixteen pulls at once, then more at once - a pull is a whole stitch's time, never free
            var burst = new CraftRequestGuard(6, 0);
            CraftRequest b16 = Pull(16, 8);
            Assert.IsTrue(burst.Check(ref b16, 0, out _));
            Assert.AreEqual(16, b16.count);
            CraftRequest more = Pull(10, 10);
            Assert.IsTrue(burst.Check(ref more, 2, out string part), part);
            Assert.Less(more.count, 10, "too fast: only part of those pulls counted");
            Assert.AreEqual(more.count, more.i2, "the hard pulls of those granted");
            // only the needle's stitch pierces
            var awl = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Stitch, count = 1, i1 = 2, flag = true };
            Assert.IsTrue(guard.Check(ref awl, 120, out _));
            Assert.IsFalse(awl.flag, "the awl's stitch goes through holes punched for it");
        }

        [Test]
        public void APractisedNeedle_LaysARunOfStitches_AndARunOfHardOnes()
        {
            var s = new CraftMasterySettings();
            var pull = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Stitch, count = 2, i1 = 1, flag = true, i2 = 1 };
            CraftMastery.ByPractice(ref pull, 0.9, s);
            Assert.AreEqual(6, pull.count, "a master's two pulls lay six stitches");
            Assert.AreEqual(3, pull.i2, "the hard pull's run of three cuts");
            Assert.IsTrue(pull.flag, "and pierces as it draws");
            var punch = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Punch, count = 1, i1 = 1, flag = true };
            CraftMastery.ByPractice(ref punch, 0.9, s);
            Assert.IsFalse(punch.flag, "only the needle's stitch pierces as it draws");
            Assert.AreEqual(0.02, CraftMastery.WorkExperience(pull), 1e-9, "a pull that pierces and draws teaches as a punch and a stitch did");
        }

        // ------------------------------------------------------------------ what the station keeps of the hands
        [Test]
        public void TheReedAndTheTape_AreNoTruerThanTheHands_AsTheWindowSaysIt()
        {
            var guard = new CraftRequestGuard(5, 0);
            float truest = (float)CraftRequestGuard.TrueCap(CraftRequestGuard.FloorOf(0));
            var fit = new CraftRequest(CraftRequestKind.MeasureFit) { a = 0.98f, b = 1000 };
            Assert.IsTrue(guard.Check(ref fit, 0, out string why), why);
            Assert.AreEqual(truest, fit.a, 1e-6, "the tape: what the window's ACCURACY gauge and words show");
            guard.Touch(0);
            var weave = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i0 = 2, i1 = 10, a = 1, b = 0.5f, points = Enumerable.Repeat(1f, 10).ToList() };
            Assert.IsTrue(guard.Check(ref weave, 20, out why), why);
            Assert.IsTrue(weave.points.All(p => Math.Abs(p - truest) < 1e-6), "each beat: what the loom's words say it was beaten");
            Assert.AreEqual(truest, weave.a, 1e-6);
            Assert.AreEqual(CraftRequestGuard.CutCap(CraftRequestGuard.FloorOf(0)), CraftRequestGuard.TrueCap(CraftRequestGuard.FloorOf(0)), 1e-9, "one measure of steady hands");
        }

        [Test]
        public void TheClothsRecord_KeepsTheTruenessTheStationWove()
        {
            CraftWorkshop w = Workshop();
            string a = Bobbin(w, fine: 1), b = Bobbin(w, fine: 1);
            Assert.IsTrue(w.JoinYarn(a, b, out string why), why);
            var guard = new CraftRequestGuard(5, 0);
            guard.Touch(0);
            var r = new CraftRequest(CraftRequestKind.WeaveCloth, a) { i0 = 2, i1 = 10, a = 1, b = 0.5f, points = Enumerable.Repeat(1f, 10).ToList() };
            Assert.IsTrue(guard.Check(ref r, 20, out why), why);
            string cloth = w.WeaveCloth(a, r.i0, r.a, r.b, r.points, out why);
            Assert.IsNotNull(cloth, why);
            CraftRecordEntry woven = w.Piece(cloth).record.FindLast(e => e.kind == CraftOpKind.WeaveCloth);
            Assert.AreEqual(CraftRequestGuard.TrueCap(CraftRequestGuard.FloorOf(0)), woven.min, 1e-6, "the window words the cloth from its record: what the station kept");
            StringAssert.Contains("90% true", woven.text);
        }

        // ------------------------------------------------------------------ the wheel and the fibre bench
        /// <summary>A bobbin of fully prepared, combed wool spun with a novice's best draft (the guard's cap) at <paramref name="twist"/>: the thread it reels.</summary>
        private static string ThreadAt(double twist, out double quality)
        {
            CraftWorkshop w = Workshop(seed: 3);
            CraftPiece p = CraftFibre.NewFibre(w.Rules, "wool_fibre");
            FibreState f = p.fibre;
            f.dirt = 0; f.grease = 0; f.tangle = 0; f.alignment = 0.95; f.shortFibre = 0; f.neps = 0; f.combed = f.carded = f.cleaned = f.scoured = f.opened = true;
            string batch = w.AddPiece(p);
            string y = w.SpinFibre(batch, 1 - 6 * CraftRequestGuard.FloorOf(0), twist, 0.5, CraftFibre.BobbinGrams, out string why);
            Assert.IsNotNull(y, why);
            quality = CraftFibre.Quality(w.Piece(y).yarn);
            return CraftFibre.ThreadOf(w.Rules, w.Piece(y));
        }

        [Test]
        public void TheWheelsGreenTwistBand_IsWhereTheYarnStaysWellTwisted()
        {
            Assert.GreaterOrEqual(CraftTextiles.SpinQuality(1, 0.5 + CraftTextiles.TwistTrue), 0.9 - 1e-9, "the band's edge keeps nine-tenths of the quality");
            Assert.GreaterOrEqual(CraftTextiles.SpinQuality(1, 0.5 - CraftTextiles.TwistTrue), 0.9 - 1e-9);
            string right = ThreadAt(0.5, out double qm), inBand = ThreadAt(0.5 - CraftTextiles.TwistTrue * 0.9, out double qb), outside = ThreadAt(0.32, out double qo);
            Assert.AreEqual(right, inBand, $"a twist in the green band reels the band's middle's thread ({qb:0.00} vs {qm:0.00})");
            Assert.AreNotEqual(right, outside, $"0.32 - amber now - reels a coarser thread ({qo:0.00})");
            Assert.Greater(Math.Abs(0.32 - 0.5), CraftTextiles.TwistTrue, "and it is outside the green band");
        }

        [Test]
        public void APartAlreadyCut_IsNotLaidOutAgain()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            string side = w.CutPattern(bolt, "coif", 0, 0, 0, 0.95, out string why);
            Assert.IsNotNull(side, why);
            Assert.IsFalse(w.LayoutPattern(side, "strap", 0, 0, 0, false, out why), "no chalk on a cut part");
            StringAssert.Contains("already cut to a pattern", why);
            Assert.IsTrue(w.LayoutPattern(bolt, "coif", 0, 8, 0, false, out why), "the length it was cut from takes chalk: " + why);
        }

        [Test]
        public void AMissPicksNothing_AndWetFibreOpensAsTheBenchShowsIt()
        {
            Assert.AreEqual(0, CraftFibre.PickedOut(0, 1), 1e-12, "a click that found no burr takes none of the dirt out");
            Assert.Less(CraftFibre.PickedOut(1, 2), 1, "never every last speck");
            CraftWorkshop w = Workshop();
            string id = Batch(w);
            double dirt = w.Piece(id).fibre.dirt;
            Assert.IsTrue(w.CleanFibre(id, 0, 0.2, out string why), why);
            Assert.AreEqual(dirt, w.Piece(id).fibre.dirt, 1e-9, "only misses: the dirt stays");
            // teased wet, the locks give a little over half as far (what the window draws and words)
            CraftPiece dry = CraftFibre.NewFibre(w.Rules, "wool_fibre"), wet = CraftFibre.NewFibre(w.Rules, "wool_fibre");
            wet.fibre.wet = 1;
            Assert.AreEqual(0.6 * CraftFibre.OpenGive(dry.fibre), CraftFibre.OpenGive(wet.fibre), 1e-9);
            string a = w.AddPiece(dry), b = w.AddPiece(wet);
            Assert.IsTrue(w.OpenFibre(a, 1, 0, out why), why);
            Assert.IsTrue(w.OpenFibre(b, 1, 0, out why), why);
            Assert.AreEqual(0.8 * (1 - 0.7 * 0.6), w.Piece(b).fibre.tangle, 1e-9, "the station opens it as far as the bench showed");
            Assert.Less(w.Piece(a).fibre.tangle, w.Piece(b).fibre.tangle);
        }

        // ------------------------------------------------------------------ the scour
        [Test]
        public void ASoak_IsNoLongerThanTheRealTimeItHad()
        {
            var guard = new CraftRequestGuard(5, 0);
            guard.Touch(0); // the window opened
            var quick = new CraftRequest(CraftRequestKind.ScourFibre, "f") { a = 62, b = 45, c = 0, d = 3, i0 = 2 };
            Assert.IsTrue(guard.Check(ref quick, 10, out string why), why);
            Assert.AreEqual(10 + CraftRequestGuard.SoakSlack, quick.b, 0.01, "ten seconds at the bath soak ten minutes (and a few for the lag)");
            StringAssert.Contains("soaked", why, "it says so");
            var honest = new CraftRequest(CraftRequestKind.ScourFibre, "f") { a = 62, b = 45, c = 0, d = 3, i0 = 2 };
            Assert.IsTrue(guard.Check(ref honest, 60, out why), why);
            Assert.AreEqual(45, honest.b, 1e-6, "forty-five seconds in the bath: the whole soak");
            Assert.IsEmpty(why);
            var longest = new CraftRequest(CraftRequestKind.ScourFibre, "f") { a = 62, b = 999 };
            Assert.IsTrue(guard.Check(ref longest, 2000, out why), why);
            Assert.AreEqual(CraftFibre.MaxSoakMinutes, longest.b, 1e-6, "never longer than the bath takes");
        }
    }
}
#endif
