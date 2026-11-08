#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the textile area's rules (the fibre bench, the wheel, the loom, the tailor's table): bobbins
    /// wound together for a long warp, plying that keeps every strand's rest, carding that needs the teeth in the fibre,
    /// preparation counted once, fine drafts that want prepared fibre, one set of thread tiers, the scour's soap and heat,
    /// seams that are sewn through their padding and not after the item is finished, and the helpers the tailor's table
    /// reads (chalk overlaps, the patterns wanted, the fit of a measure).
    /// </summary>
    public class CraftTextileAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, int seed = 1) =>
            new CraftWorkshop(Rules(), tools, 18, seed, 3, "water", "textile");

        private static string Batch(CraftWorkshop w, string fibre = "wool_fibre") => w.AddPiece(CraftFibre.NewFibre(w.Rules, fibre));

        /// <summary>A bobbin spun from a fresh batch of <paramref name="fibre"/> (the window's 30 g).</summary>
        private static string Bobbin(CraftWorkshop w, string fibre = "wool_fibre", double even = 0.85, double twist = 0.5, double fine = 0.5)
        {
            string y = w.SpinFibre(Batch(w, fibre), even, twist, fine, CraftFibre.BobbinGrams, out string why);
            Assert.IsNotNull(y, why);
            return y;
        }

        // ------------------------------------------------------------------ bobbins wound together, and a long warp
        [Test]
        public void Bobbins_WoundTogether_WeaveALengthNoBobbinHolds()
        {
            CraftWorkshop w = Workshop();
            string a = Bobbin(w), b = Bobbin(w, twist: 0.45), c = Bobbin(w, even: 0.6, twist: 0.55);
            YarnState ya = w.Piece(a).yarn, yb = w.Piece(b).yarn, yc = w.Piece(c).yarn;
            double la = ya.length, lb = yb.length, lc = yc.length, ga = ya.grams, gb = yb.grams;
            double ea = ya.evenness, eb = yb.evenness;
            Assert.AreEqual(0, CraftFibre.RowsFor(la, 16), "one bobbin is too little for a row 16 wide");
            Assert.IsTrue(w.JoinYarn(a, b, out string why), why);
            Assert.IsNull(w.Piece(b), "the emptied bobbin is gone");
            Assert.AreEqual(la + lb, w.Piece(a).yarn.length, 1e-9, "the lengths add");
            Assert.AreEqual(ga + gb, w.Piece(a).yarn.grams, 1e-9, "and the grams");
            Assert.AreEqual((ea * la + eb * lb) / (la + lb), w.Piece(a).yarn.evenness, 1e-9, "its evenness is the stretches', by length");
            Assert.IsTrue(w.JoinYarn(a, c, out why), why);
            double total = la + lb + lc;
            Assert.AreEqual(total, w.Piece(a).yarn.length, 1e-9);
            Assert.AreEqual(Math.Min(CraftFibre.MaxRows, (int)Math.Floor(total / (CraftFibre.MetresPerCell * 6))), CraftFibre.RowsFor(total, 6));
            int rows = CraftFibre.RowsFor(total, 6);
            Assert.Greater(rows, CraftFibre.RowsFor(la, 6), "more rows than one bobbin weaves");
            string cloth = w.WeaveCloth(a, 6, 0.9, 0.5, Enumerable.Repeat(0.9f, rows).ToList(), out why);
            Assert.IsNotNull(cloth, why);
            Assert.AreEqual(rows, w.Piece(cloth).hide.height, "every row the joined yarn allows");
            Assert.AreEqual(CraftFibre.MaxRows, CraftFibre.RowsFor(100000, 2), "a length is thirty rows at the most");
        }

        [Test]
        public void Joining_NeverPutsATwistRight_AndForgetsTheCount()
        {
            CraftWorkshop w = Workshop();
            string under = Bobbin(w, twist: 0.3), over = Bobbin(w, twist: 0.7);
            double alone = CraftFibre.Quality(w.Piece(under).yarn);
            Assert.IsTrue(w.MeasureYarn(under, 100, out string why), why);
            Assert.IsTrue(w.JoinYarn(under, over, out why), why);
            YarnState y = w.Piece(under).yarn;
            Assert.AreEqual(0.2, Math.Abs(y.twist - 0.5), 1e-6, "an under- and an over-twisted stretch stay as far off as they were");
            Assert.AreEqual(alone, CraftFibre.Quality(y), 0.03, "joined, it is no better than its stretches");
            Assert.IsFalse(y.measured, "the joined length is not counted");
            Assert.AreEqual(0.5, CraftFibre.MixedTwist(new List<(double, double)> { (0.5, 3), (0.5, 1) }), 1e-12);
            Assert.AreEqual(0.6, CraftFibre.MixedTwist(new List<(double, double)> { (0.6, 1), (0.4, 1) }), 1e-12, "the side most of it lies on (a tie: over)");
        }

        [Test]
        public void Joining_WantsTheSameYarn_AndALoomOrAWheel()
        {
            CraftWorkshop w = Workshop();
            string wool = Bobbin(w), wool2 = Bobbin(w), flax = Bobbin(w, "flax_fibre"), third = Bobbin(w);
            Assert.IsFalse(w.JoinYarn(wool, flax, out string why), "wool and linen are not wound together");
            StringAssert.Contains("same fibre", why);
            Assert.IsTrue(w.PlyYarn(wool2, third, null, 0.4, 0.9, out why), why);
            string plied = w.Piece(wool2) != null && w.Piece(wool2).yarn.plies == 2 ? wool2 : third;
            Assert.IsFalse(w.JoinYarn(wool, plied, out why), "singles and a 2-ply are not one yarn");
            StringAssert.Contains("plied", why);
            Assert.IsFalse(w.JoinYarn(wool, wool, out _), "not onto itself");
            Assert.IsTrue(w.CanJoinYarn(wool, Bobbin(w), out why), why);
            Assert.AreEqual("", why);
            CraftWorkshop table = Workshop(CraftTools.TailorTable);
            string y1 = table.AddPiece(w.Piece(wool).Clone()), y2 = table.AddPiece(w.Piece(Bobbin(w)).Clone());
            Assert.IsFalse(table.JoinYarn(y1, y2, out why), "bobbins are wound together at a loom or a wheel");
            StringAssert.Contains("loom", why);
            var ask = new CraftRequest(CraftRequestKind.JoinYarn, y1) { other = y2 };
            Assert.AreEqual(CraftDiscipline.Tailoring, CraftMastery.OfRequest(w.Rules, w, ask));
            Assert.AreEqual(0, CraftMastery.WorkExperience(ask), "winding bobbins together teaches nothing");
        }

        [Test]
        public void Guard_WindsOneBobbinAtATime_AndHoldsTheDraftOfASpinFromTheBag()
        {
            var guard = new CraftRequestGuard(5, 0);
            int granted = 0;
            for (int k = 0; k < 20; k++)
            {
                var r = new CraftRequest(CraftRequestKind.JoinYarn, "a") { other = "b" };
                if (guard.Check(ref r, 0.01 * k, out string why)) granted++;
                else StringAssert.Contains("bobbin", why);
            }
            Assert.Less(granted, 20, "not twenty windings in a fifth of a second");
            var spin = new CraftRequest(CraftRequestKind.Spin) { id = "wool_fibre", a = 0.8f, b = 0.5f, c = 7f };
            Assert.IsTrue(guard.Check(ref spin, 10, out _));
            Assert.AreEqual(1f, spin.c, "a draft is 0 thick to 1 fine");
            var bobbin = new CraftRequest(CraftRequestKind.SpinFibre, "f") { a = 0.8f, b = 0.5f, c = 0.5f, i0 = 500 };
            Assert.IsTrue(guard.Check(ref bobbin, 20, out _));
            Assert.AreEqual(CraftFibre.BobbinGrams, bobbin.i0, "a bobbin's grams");
        }

        // ------------------------------------------------------------------ plying
        [Test]
        public void Plying_WindsOnTheShorterBobbin_AndEveryOtherStrandKeepsItsRest()
        {
            CraftWorkshop w = Workshop();
            string longer = Bobbin(w, fine: 1), shorter = Bobbin(w, fine: 0.4);
            double l1 = w.Piece(longer).yarn.length, l2 = w.Piece(shorter).yarn.length, g1 = w.Piece(longer).yarn.grams;
            Assert.Greater(l1, l2 + 20);
            Assert.IsTrue(w.PlyYarn(longer, shorter, null, 0.4, 0.9, out string why), why);
            Assert.AreEqual(2, w.Piece(shorter).yarn.plies, "the ply is wound on the bobbin that ran out");
            Assert.AreEqual(l2 * 0.95, w.Piece(shorter).yarn.length, 1e-6);
            Assert.AreEqual(1, w.Piece(longer).yarn.plies, "the longer strand stays singles...");
            Assert.AreEqual(l1 - l2, w.Piece(longer).yarn.length, 1e-6, "...with what was left on it");
            Assert.AreEqual(g1 * (1 - l2 / l1), w.Piece(longer).yarn.grams, 1e-6);

            string a = Bobbin(w), b = Bobbin(w);
            Assert.IsTrue(w.PlyYarn(a, b, null, 0.4, 0.9, out why), why);
            Assert.AreEqual(2, w.Piece(a).yarn.plies, "a tie: the first bobbin takes the ply");
            Assert.IsNull(w.Piece(b), "a spent strand is gone");

            string wool = Bobbin(w), flax = Bobbin(w, "flax_fibre");
            Assert.IsFalse(w.PlyYarn(wool, flax, null, 0.4, 0.9, out why), "plied from the same fibre");
            StringAssert.Contains("same fibre", why);
        }

        [Test]
        public void Plying_HangsStraight_OnlyInsideTheBandTheWheelShows()
        {
            double singles = 0.5, at = CraftFibre.PlyBalanceAt * singles;
            Assert.Greater(CraftFibre.PlyBalance(at + CraftFibre.PlyBand * 0.95, singles), CraftFibre.PlyStraight, "inside the band: straight");
            Assert.Less(CraftFibre.PlyBalance(at + CraftFibre.PlyBand * 1.05, singles), CraftFibre.PlyStraight, "just outside: it twists on itself");
            Assert.Less(CraftFibre.PlyBalance(at - CraftFibre.PlyBand * 1.05, singles), CraftFibre.PlyStraight);
            CraftWorkshop w = Workshop();
            string a = Bobbin(w), b = Bobbin(w);
            Assert.IsTrue(w.PlyYarn(a, b, null, at + CraftFibre.PlyBand * 1.05, 0.9, out string why), why);
            StringAssert.Contains("twists on itself", w.Piece(a).record.Last().text, "the station's word is the band's");
        }

        // ------------------------------------------------------------------ carding, picking, teasing
        [Test]
        public void Carding_AlignsOnlyWithTheTeethInTheFibre_AndKnotsBeyondTheLightBand()
        {
            CraftWorkshop w = Workshop();
            string hover = Batch(w, "cotton_fibre"), light = Batch(w, "cotton_fibre"), heavy = Batch(w, "cotton_fibre");
            double before = w.Piece(hover).fibre.alignment;
            Assert.IsTrue(w.CardFibre(hover, 16, 0, 0.9, out string why), why);
            Assert.IsTrue(w.CardFibre(light, 16, CraftFibre.CardLight + 0.05, 0.9, out why), why);
            Assert.IsTrue(w.CardFibre(heavy, 16, 0.7, 0.9, out why), why);
            Assert.AreEqual(before, w.Piece(hover).fibre.alignment, 1e-9, "a card that never touches the fibre aligns nothing");
            Assert.Greater(w.Piece(light).fibre.alignment, before + 0.2, "a light stroke into the fibre aligns it");
            Assert.AreEqual(w.Piece(light).fibre.alignment, w.Piece(heavy).fibre.alignment, 1e-9, "pressing harder aligns no better...");
            Assert.Greater(w.Piece(heavy).fibre.neps, w.Piece(light).fibre.neps + 0.05, "...and knots neps");
            Assert.AreEqual(0, CraftFibre.CardGrip(0), 1e-12);
            Assert.AreEqual(1, CraftFibre.CardGrip(CraftFibre.CardLight), 1e-12);
            Assert.Less(CraftFibre.CardLight, CraftFibre.CardNeps, "the light band is a band");
        }

        [Test]
        public void Picking_WithFineTweezers_NeverLeavesLessThanNoDirt()
        {
            CraftWorkshop w = Workshop();
            w.SetVariable("toolQuality", 2);
            string f = Batch(w);
            Assert.IsTrue(w.CleanFibre(f, 1, 0, out string why), why);
            Assert.GreaterOrEqual(w.Piece(f).fibre.dirt, 0, "dirt never below zero");
            Assert.Greater(w.Piece(f).fibre.dirt, 0, "the finest tweezers leave a speck");
        }

        // ------------------------------------------------------------------ spinning
        [Test]
        public void Preparation_IsCountedOnce_ARawBatchSpinsLikeRawFibreFromTheBag()
        {
            CraftWorkshop w = Workshop();
            CraftSupplyDefinition wool = w.Rules.Supply("wool_fibre");
            double raw = CraftTextiles.RawPrep(w.Rules, "wool_fibre");
            string y = w.SpinFibre(Batch(w), 0.85, 0.5, 0, CraftFibre.BobbinGrams, out string why);
            Assert.IsNotNull(y, why);
            double q = CraftFibre.Quality(w.Piece(y).yarn);
            Assert.AreEqual(CraftTextiles.SpinQuality(0.85, 0.5) * (0.55 + 0.45 * raw), q, 1e-6, "the hands' evenness times the preparation, once");
            Assert.AreEqual(CraftTextiles.Spun(w.Rules, wool, 0.85, 0.5, raw), CraftFibre.ThreadOf(w.Rules, w.Piece(y)), "the same thread either way");
            foreach (double quality in new[] { 0.3, 0.44, 0.46, 0.71, 0.73, 0.9 })
            {
                var yarn = new YarnState { fibre = "wool_fibre", evenness = quality, twist = 0.5 };
                var p = new CraftPiece { kind = CraftPieceKind.Yarn, yarn = yarn };
                Assert.AreEqual(CraftTextiles.Spun(w.Rules, wool, quality, 0.5), CraftFibre.ThreadOf(w.Rules, p), $"one set of tiers at {quality}");
            }
        }

        [Test]
        public void FineDrafts_WantPreparedFibre_AndSpinMoreMetres()
        {
            CraftWorkshop w = Workshop();
            string rawThick = w.SpinFibre(Batch(w), 0.85, 0.5, 0, 30, out string why), rawFine = w.SpinFibre(Batch(w), 0.85, 0.5, 1, 30, out why);
            Assert.IsNotNull(rawThick, why);
            Assert.IsNotNull(rawFine, why);
            Assert.Less(w.Piece(rawFine).yarn.evenness, w.Piece(rawThick).yarn.evenness * 0.8, "a fine draft of raw fleece is lumpy");
            Assert.Greater(w.Piece(rawFine).yarn.length, w.Piece(rawThick).yarn.length * 5, "but it draws out much further");
            double prep = CraftTextiles.RawPrep(w.Rules, "wool_fibre");
            Assert.AreEqual(CraftFibre.MetresSpun(30, 1, prep), w.Piece(rawFine).yarn.length, 1e-9, "the window reads the metres from the same rule");
            Assert.Greater(CraftFibre.DraftEvenness(1, 1), 0.99, "well-prepared fibre drafts fine without harm");
            Assert.Less(CraftFibre.DraftEvenness(0.1, 1), CraftFibre.DraftEvenness(0.1, 0) * 0.8);
        }

        // ------------------------------------------------------------------ scouring
        [Test]
        public void Scouring_TwoRinsesClearTheSoap_AndTheBathsHeatIsTheFibres()
        {
            Assert.AreEqual(CraftFibre.SoapPerScour, CraftFibre.SoapAfter(0, true, 0), 1e-12);
            Assert.AreEqual(0, CraftFibre.SoapAfter(0, true, 2), 1e-12, "two rinses clear a scour's soap");
            Assert.AreEqual(0, CraftFibre.SoapAfter(0.1, false, 1), 1e-12, "water alone only rinses");
            CraftFibre.ScourHeat(Rules().Supply("wool_fibre"), out double from, out double full);
            Assert.AreEqual(35, from, 1e-12);
            Assert.AreEqual(62, full, 1e-12);
            CraftFibre.ScourHeat(Rules().Supply("silk_floss"), out from, out full);
            Assert.Greater(from, 60, "silk's gum wants near-boiling water");
            CraftWorkshop w = Workshop();
            string calm = Batch(w), shocked = Batch(w), soapy = Batch(w);
            Assert.IsTrue(w.ScourFibre(calm, 62, 30, 0, CraftFibre.ShockFrom - 1, "soap", 2, out string why), why);
            Assert.IsTrue(w.ScourFibre(shocked, 62, 30, 0, CraftFibre.ShockFelts + 2, "soap", 2, out why), why);
            Assert.IsTrue(w.ScourFibre(soapy, 62, 30, 0, 0, "soap", 1, out why), why);
            Assert.Less(w.Piece(calm).fibre.felted, 0.01, "a rinse near the bath's heat does not shock it");
            Assert.IsTrue(w.Piece(shocked).defects.Any(d => d.kind == CraftDefectKind.Felted), "past the felting shock it felts");
            Assert.AreEqual(0, w.Piece(calm).fibre.soap, 1e-12);
            Assert.Greater(w.Piece(soapy).fibre.soap, 0.1, "one rinse leaves soap in it");
        }

        // ------------------------------------------------------------------ sewing
        [Test]
        public void Quilting_IsSewnThroughItsBatting_OnceItIsLaidIn()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            string asm = w.StartAssembly("arming_cap", out string why);
            Assert.IsNotNull(asm, why);
            for (int k = 0; k < 2; k++)
            {
                string side = w.CutPattern(bolt, "coif", 0, k * 5, 0, 0.95, out why);
                Assert.IsNotNull(side, why);
                Assert.IsTrue(w.Attach(asm, "sides", k, side, out why), why);
            }
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1), why);
            Assert.IsFalse(w.JointWork(asm, "quilting", CraftJointWork.Punch, 4, 0.1, 0.1, out why), "no quilting through nothing");
            StringAssert.Contains("Lay the padding in first", why);
            Assert.IsTrue(w.Attach(asm, "padding", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "quilting", CraftJointWork.Punch, 4, 0.1, 0.1, out why), why);
            Assert.IsTrue(w.JointWork(asm, "quilting", CraftJointWork.Stitch, 4, CraftAssembly.StitchTensionIdeal, 0.05, out why), why);
            var thread = w.Rules.Design("linen_coif").joints.First(j => j.id == "crown_seam");
            Assert.AreEqual("thread", thread.roleB, "a seam whose second part is its thread...");
            Assert.IsTrue(CraftTextiles.ClothSeam(w, w.Piece(asm), w.Rules.Design("arming_cap").Joint("quilting")), "a seam of cloth parts is the needle's");
        }

        [Test]
        public void AFinishedItem_TakesNoMoreStitches()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            string asm = w.StartAssembly("linen_coif", out string why);
            Assert.IsNotNull(asm, why);
            for (int k = 0; k < 2; k++)
            {
                string side = w.CutPattern(bolt, "coif", 0, k * 5, 0, 0.95, out why);
                Assert.IsNotNull(side, why);
                Assert.IsTrue(w.Attach(asm, "sides", k, side, out why), why);
            }
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Punch, 14, 0.1, 0.1, out why), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 14, CraftAssembly.StitchTensionIdeal, 0.05, out why), why);
            Assert.IsTrue(w.Finish(asm, out why), why);
            int stitches = w.Piece(asm).assembly.Joint("crown_seam").stitches;
            Assert.IsFalse(w.JointWork(asm, "crown_seam", CraftJointWork.Punch, 6, 0.9, 0.9, out why), "its seams are as they were judged");
            StringAssert.Contains("finished", why);
            Assert.IsFalse(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 6, 1, 0.9, out _));
            Assert.AreEqual(stitches, w.Piece(asm).assembly.Joint("crown_seam").stitches);
        }

        [Test]
        public void AStitchPulledPastTheCut_DamagesTheSeam()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            string asm = w.StartAssembly("linen_coif", out string why);
            for (int k = 0; k < 2; k++) Assert.IsTrue(w.Attach(asm, "sides", k, w.CutPattern(bolt, "coif", 0, k * 5, 0, 0.95, out _), out why), why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Punch, 8, 0.1, 0.1, out why), why);
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 4, CraftAssembly.StitchTensionCuts - 0.05, 0.05, out why), why);
            Assert.AreEqual(0, w.Piece(asm).assembly.Joint("crown_seam").damage, 1e-12, "just under the cut: whole");
            Assert.IsTrue(w.JointWork(asm, "crown_seam", CraftJointWork.Stitch, 4, CraftAssembly.StitchTensionCuts + 0.05, 0.05, out why), why);
            Assert.Greater(w.Piece(asm).assembly.Joint("crown_seam").damage, 0, "past it the thread cuts the cloth");
        }

        // ------------------------------------------------------------------ the tailor's table
        [Test]
        public void TheChalkACutGoesThrough_IsTold_ButNotItsOwn()
        {
            CraftWorkshop w = Workshop();
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "canvas", 3));
            Assert.IsTrue(w.LayoutPattern(bolt, "coif", 0, 0, 0, false, out string why), why);
            Assert.IsTrue(w.LayoutPattern(bolt, "coif", 4, 0, 0, false, out why), why);
            HideState h = w.Piece(bolt).hide;
            CraftPattern coif = w.Rules.Pattern("coif");
            Assert.AreEqual(0, CraftTextiles.Overlaps(w.Rules, h, coif, 0, 0, 0), "laid on its own chalk, it overlaps it");
            Assert.AreEqual(-1, CraftTextiles.Overlaps(w.Rules, h, coif, 0, 0, 0, 0), "but no other chalk");
            Assert.AreEqual(1, CraftTextiles.Overlaps(w.Rules, h, coif, 2, 0, 0, 0), "between the two it cuts into the second");
            Assert.IsTrue(CraftTextiles.ClothPattern(w.Rules, "coif"), "the coif is cut from cloth");
        }

        [Test]
        public void ThePatternsWanted_AreTheClothPartsOfTheItemsBeingMade()
        {
            CraftWorkshop w = Workshop();
            string asm = w.StartAssembly("linen_coif", out string why);
            Assert.IsNotNull(asm, why);
            List<(string pattern, int count, string item)> wanted = CraftTextiles.PatternsWanted(w);
            Assert.AreEqual(1, wanted.Count);
            Assert.AreEqual("coif", wanted[0].pattern);
            Assert.AreEqual(2, wanted[0].count, "two sides");
            string bolt = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 3));
            Assert.IsTrue(w.Attach(asm, "sides", 0, w.CutPattern(bolt, "coif", 0, 0, 0, 0.95, out _), out why), why);
            Assert.AreEqual(1, CraftTextiles.PatternsWanted(w)[0].count, "one fitted: one to cut");
        }

        [Test]
        public void TheFitOfAMeasure_IsTheEvaluatorsRule()
        {
            Assert.AreEqual(CraftTextiles.FitUnmeasured, CraftTextiles.FitOf(true, CraftTextiles.FitWorthIt), 1e-9, "at the threshold a measure fits as well as none");
            Assert.Less(CraftTextiles.FitOf(true, CraftTextiles.FitWorthIt - 0.05), CraftTextiles.FitUnmeasured, "a careless measure fits worse than none");
            Assert.Greater(CraftTextiles.FitOf(true, 0.6), CraftTextiles.FitUnmeasured, "a rough one (60%) still fits better");
            Assert.AreEqual(0.95, CraftTextiles.FitOf(false, 1), 1e-12);
        }

        // ------------------------------------------------------------------ the loom
        [Test]
        public void TooLittleYarn_SaysHowMuchARowTakes_AndWhatTheBobbinHas()
        {
            CraftWorkshop w = Workshop();
            string y = Bobbin(w, fine: 0);
            Assert.IsNull(w.WeaveCloth(y, 16, 0.9, 0.5, new List<float> { 0.9f }, out string why));
            StringAssert.Contains($"{CraftFibre.MetresPerCell * 16:0} m a row", why);
            StringAssert.Contains("wind another bobbin on", why);
        }
    }
}
#endif
