#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the assembly's review: a socketed head is driven home on its shaft before its pins go through
    /// (a pin alone is no joint, a pin fitted is set before the item is finished), and is driven over the wedge's span,
    /// splitting the shaft past it in the shaft's own words; the hammer's band is the rule for every joint it drives - a
    /// wedge, a socket, a guard's slot, a pommel (a light blow bounces, a hard one splits, dents or bends, and the joint is
    /// as true as its blows); the peened hilt holds its blade; rivets set stay part of the item (its set, its weight and
    /// worth), and those drilled out are lost; a joint in progress is rated by the work done on it so far; misaligned
    /// holes cost a riveted joint once; a clasp sewn onto cloth is cloth work; a wedge takes up a loose eye's play (a
    /// socket's stays); the finisher is the item's maker; a blunt head's inspection rates its striking face.
    /// </summary>
    public class CraftReviewAssemblyTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(int seed = 1) => new CraftWorkshop(Rules(), CraftTools.Everything, 18, seed, 3, "water", "assemblyrev");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A request's force for a blow of <paramref name="r"/> × the joint's ideal (a request's force is 4 × the hammer's charge).</summary>
        private static double Blow(CraftWorkshop w, string asm, string joint, CraftJointWork work, double r)
        {
            CraftPiece a = w.Piece(asm);
            CraftJointSpec spec = w.Rules.Design(a.assembly.design).Joint(joint);
            return 4 * CraftAssembly.IdealCharge(w, a, spec, work) * r;
        }

        private static CraftPiece Holed(CraftPiece p, double diameter)
        {
            p.segments[p.segments.Count / 2].hole = diameter;
            return p;
        }

        private static CraftPiece ToTarget(CraftRules rules, CraftShapeTarget t, double length)
        {
            CraftPiece p = CraftDefaults.Bar(rules, length, 10, 5, Metal("Mild steel"));
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

        private static CraftPiece Rivets(CraftRules rules, CraftFastenerKind kind, int n) =>
            CraftDefaults.FastenerSet(rules, kind, n, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002));

        private static Dictionary<string, List<CraftPiece>> Parts(CraftWorkshop w, CraftPiece asm)
        {
            var d = new Dictionary<string, List<CraftPiece>>();
            foreach (AssemblyPart ap in asm.assembly.parts)
            {
                if (!d.TryGetValue(ap.role, out List<CraftPiece> l)) d[ap.role] = l = new List<CraftPiece>();
                l.Add(w.Piece(ap.piece));
            }
            return d;
        }

        /// <summary>How well a joint is made (<paramref name="soFar"/>: the work done on it so far).</summary>
        private static double Q(CraftWorkshop w, string asm, string joint, bool soFar = false)
        {
            CraftPiece a = w.Piece(asm);
            CraftDesign d = w.Rules.Design(a.assembly.design);
            return CraftEvaluator.JointQuality(w.Rules, d.Joint(joint), a.assembly.Joint(joint), d, Parts(w, a), a.assembly, soFar);
        }

        private static string LastJoined(CraftWorkshop w) => w.RecentEvents.LastOrDefault(e => e.Kind == CraftEventKind.Joined).Text ?? "";

        /// <summary>A spear on the bench: a head with a socket of <paramref name="socket"/> mm on an ash shaft 25 mm across, its pin fitted when <paramref name="pin"/>.</summary>
        private static string Spear(CraftWorkshop w, bool pin, double socket = 26)
        {
            string head = w.AddPiece(Holed(CraftDefaults.Bar(w.Rules, 250, 30, 8, Metal("Mild steel")), socket));
            string shaft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 1800, 25, 25));
            string asm = w.StartAssembly("spear", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "head", 0, head, out why), why);
            Assert.IsTrue(w.Attach(asm, "haft", 0, shaft, out why), why);
            if (pin)
                Assert.IsTrue(w.Attach(asm, "rivets", 0, w.AddPiece(Rivets(w.Rules, CraftFastenerKind.Rivet, 1)), out why), why);
            return asm;
        }

        /// <summary>A hatchet on the bench: a head with an eye of <paramref name="eye"/> mm on an ash haft 25 mm across.</summary>
        private static string Hatchet(CraftWorkshop w, double eye = 26)
        {
            string head = w.AddPiece(Holed(CraftDefaults.Bar(w.Rules, 160, 50, 20, Metal("Mild steel")), eye));
            string haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 25, 25));
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "head", 0, head, out why), why);
            Assert.IsTrue(w.Attach(asm, "haft", 0, haft, out why), why);
            return asm;
        }

        /// <summary>A dagger on the bench: a blade forged to its shortest target, a guard, a grip and a pommel made for its tang.</summary>
        private static string Dagger(CraftWorkshop w)
        {
            CraftDesign d = w.Rules.Design("dagger");
            CraftShapeTarget t = d.Component("blade").shape;
            string blade = w.AddPiece(ToTarget(w.Rules, t, t.lengthMin));
            string guard = w.AddPiece(Holed(CraftDefaults.Bar(w.Rules, 90, 14, 8, Metal("Mild steel")), d.Component("guard").shape.eyeDiameter));
            string grip = w.AddPiece(Holed(CraftDefaults.WoodBlank(w.Rules, "ash", 100, 26, 26), 11));
            string pommel = w.AddPiece(Holed(CraftDefaults.Bar(w.Rules, 40, 30, 25, Metal("Mild steel")), d.Component("pommel").shape.eyeDiameter));
            string asm = w.StartAssembly("dagger", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "blade", 0, blade, out why), why);
            Assert.IsTrue(w.Attach(asm, "guard", 0, guard, out why), why);
            Assert.IsTrue(w.Attach(asm, "grip", 0, grip, out why), why);
            Assert.IsTrue(w.Attach(asm, "pommel", 0, pommel, out why), why);
            return asm;
        }

        /// <summary>Drives a wedge or a socketed head with blows of <paramref name="r"/> × its ideal until it is home (or <paramref name="most"/> blows); the blows struck.</summary>
        private static int DriveHome(CraftWorkshop w, string asm, double r, int most = 30)
        {
            int blows = 0;
            JointState j = w.Piece(asm).assembly.Joint("head_haft");
            while (j.wedge < CraftAssembly.WedgeGoodLow && blows < most && w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, Blow(w, asm, "head_haft", CraftJointWork.Wedge, r), 0, out _))
                blows++;
            return blows;
        }

        // ------------------------------------------------------------------ the socket
        [Test]
        public void ASocketedHead_IsDrivenHomeFirst_ThenPinned_AndAPinAloneIsNoJoint()
        {
            CraftWorkshop w = Workshop();
            string asm = Spear(w, true);
            CraftDesign spear = w.Rules.Design("spear");
            CraftJointSpec spec = spear.Joint("head_haft");
            JointState j = w.Piece(asm).assembly.Joint("head_haft");

            // the pin goes through socket and shaft as they sit once the head is home: not before
            Assert.IsFalse(w.JointWork(asm, "head_haft", CraftJointWork.Punch, 1, 0, 0, out string why), "no pin's hole before the head is home");
            StringAssert.Contains("Drive the head home on the shaft first", why);
            Assert.IsFalse(w.JointWork(asm, "head_haft", CraftJointWork.Rivet, 1, Blow(w, asm, "head_haft", CraftJointWork.Rivet, 1), 0, out why), "nor a pin");
            StringAssert.Contains("Drive the head home on the shaft first", why);
            // a pin alone (an older history) is not the head on its shaft: it is neither done nor complete
            var pinnedOnly = new JointState { joint = "head_haft", rivets = 1, rivetQuality = 1, holes = 1 };
            Assert.IsFalse(CraftAssembly.JointDone(spec, pinnedOnly), "a socket is done once the head is driven on");
            Assert.IsFalse(CraftAssembly.JointComplete(spec, pinnedOnly, 1));
            CollectionAssert.Contains(CraftAssembly.Missing(w, w.Piece(asm)), "Head on the haft: not finished.");

            // driven home: done, but its pin - fitted - is still to set: not complete, and the item waits for it
            Assert.Greater(DriveHome(w, asm, 1), 0);
            Assert.That(j.wedge, Is.InRange(CraftAssembly.WedgeGoodLow, CraftAssembly.WedgeGoodHigh));
            Assert.AreEqual(1, CraftAssembly.PinsWanted(w, w.Piece(asm), spear, spec));
            Assert.IsTrue(CraftAssembly.JointDone(spec, j));
            Assert.IsFalse(CraftAssembly.JointComplete(spec, j, 1), "home, its pin not yet set");
            Assert.IsTrue(CraftAssembly.Missing(w, w.Piece(asm)).Any(m => m.Contains("set its socket pin (0 of 1)")), string.Join(" ", CraftAssembly.Missing(w, w.Piece(asm))));
            Assert.IsFalse(w.Finish(asm, out why), "a pin fitted is set (or taken off) before it is finished");

            // its hole through socket and shaft, then the pin; the head is driven no further once the hole is through
            Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Punch, 1, 0, 0, out why), why);
            Assert.IsFalse(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, Blow(w, asm, "head_haft", CraftJointWork.Wedge, 1), 0, out why), "the hole would no longer line up");
            StringAssert.Contains("would no longer line up", why);
            Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Rivet, 1, Blow(w, asm, "head_haft", CraftJointWork.Rivet, 1), 0, out why), why);
            Assert.IsTrue(CraftAssembly.JointComplete(spec, j, 1), "home and pinned");
            CollectionAssert.IsEmpty(CraftAssembly.Missing(w, w.Piece(asm)));
            double pinned = Q(w, asm, "head_haft");

            // the pin only adds to the hold: a spear driven home alike, with no pin fitted, holds no better
            CraftWorkshop bare = Workshop();
            string other = Spear(bare, false);
            DriveHome(bare, other, 1);
            Assert.AreEqual(0, CraftAssembly.PinsWanted(bare, bare.Piece(other), spear, spec), "no pin fitted: none wanted");
            Assert.IsTrue(CraftAssembly.JointComplete(spec, bare.Piece(other).assembly.Joint("head_haft"), 0));
            Assert.GreaterOrEqual(pinned, Q(bare, other, "head_haft") - 1e-9, "the pin never makes it worse");
            Assert.Greater(pinned, 0.9, "home with good blows and pinned: a good joint");
            Assert.IsTrue(w.Finish(asm, out why), why);
        }

        [Test]
        public void ASocketsPins_AreItsPinPartsOwnCount()
        {
            CraftRules rules = Rules();
            CraftDesign halberd = rules.Design("halberd"), spear = rules.Design("spear");
            Assert.AreEqual(2, CraftAssembly.RivetsWanted(halberd.Joint("head_haft"), halberd), "a halberd's two langet rivets");
            Assert.AreEqual(2, CraftAssembly.HolesWanted(halberd, halberd.Joint("head_haft")), "a hole for each");
            Assert.AreEqual(1, CraftAssembly.RivetsWanted(spear.Joint("head_haft"), spear), "a spear's one pin");
            Assert.AreEqual(1, CraftAssembly.RivetsWanted(spear.Joint("head_haft")), "one without the design");
        }

        [Test]
        public void ASocketDrivenPastHome_SplitsTheShaft_InTheShaftsOwnWords()
        {
            CraftWorkshop w = Workshop();
            string asm = Spear(w, false);
            CraftPiece shaft = w.Piece(w.Piece(asm).assembly.PieceIn("haft"));
            JointState j = w.Piece(asm).assembly.Joint("head_haft");
            int blows = 0;
            // good blows over the span the gauge draws green (home 0.6-1.3): the shaft holds
            while (j.wedge + 0.2 < CraftAssembly.WedgeGoodHigh && blows < 20)
            {
                Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, Blow(w, asm, "head_haft", CraftJointWork.Wedge, 1), 0, out string why), why);
                blows++;
            }
            Assert.That(j.wedge, Is.InRange(CraftAssembly.WedgeGoodLow, CraftAssembly.WedgeGoodHigh));
            Assert.AreEqual(0, shaft.wood.split, 1e-9, "home inside the band: no split");
            string said = LastJoined(w);
            StringAssert.Contains("home on the shaft", said);
            StringAssert.DoesNotContain("wedge", said, "a socket has no wedge");
            // and on past it, as the gauge's red shows: the shaft splits in the socket
            for (int k = 0; k < 6 && j.wedge <= CraftAssembly.WedgeGoodHigh + 0.2; k++)
                Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, Blow(w, asm, "head_haft", CraftJointWork.Wedge, 1), 0, out string why), why);
            Assert.Greater(shaft.wood.split, 0, "driven past home: the shaft splits");
            StringAssert.Contains("split the shaft", LastJoined(w));
            Assert.Less(Q(w, asm, "head_haft"), 0.95);
        }

        // ------------------------------------------------------------------ the hammer's band
        [Test]
        public void AWedge_IsBestDrivenWithBlowsInTheBand_AHardOneSplits_ALightOneBounces()
        {
            CraftWorkshop good = Workshop(), hard = Workshop(), light = Workshop();
            string ag = Hatchet(good), ah = Hatchet(hard), al = Hatchet(light);
            int gb = DriveHome(good, ag, 1), hb = DriveHome(hard, ah, 1.43);
            double qg = Q(good, ag, "head_haft"), qh = Q(hard, ah, "head_haft");
            double split = hard.Piece(hard.Piece(ah).assembly.PieceIn("haft")).wood.split;
            Assert.Greater(qg, 0.97, $"home in {gb} good blows");
            Assert.Less(qh, qg - 0.2, $"home in {hb} blows past the band: driven crooked and split");
            Assert.Greater(split, 0, "a blow too hard splits the haft at once");
            StringAssert.StartsWith("Too hard", LastJoined(hard));
            StringAssert.Contains("split the haft", LastJoined(hard));
            // a blow under the band bounces: less than its share of the work
            Assert.IsTrue(light.JointWork(al, "head_haft", CraftJointWork.Wedge, 1, Blow(light, al, "head_haft", CraftJointWork.Wedge, 0.3), 0, out string lw), lw);
            double one = 0.24 * CraftAssembly.IdealCharge(light, light.Piece(al), light.Rules.Design("hatchet").Joint("head_haft"), CraftJointWork.Wedge);
            Assert.Less(light.Piece(al).assembly.Joint("head_haft").wedge, 0.3 * one * 0.75, "a light blow does less than its share");
            Assert.AreEqual(CraftAssembly.GoodHigh, CraftAssembly.DriveEffect(2), 1e-12, "and a hard one no more than the top of the band");
        }

        [Test]
        public void AGuardsSlot_IsClosedBestWithBlowsInTheBand_AHardOneDentsIt()
        {
            CraftWorkshop band = Workshop(), full = Workshop();
            string ab = Dagger(band), af = Dagger(full);
            int bb = 0, fb = 0;
            while (bb < 20 && band.JointWork(ab, "guard_fit", CraftJointWork.Peen, 1, Blow(band, ab, "guard_fit", CraftJointWork.Peen, 1), 0, out _)) bb++;
            while (fb < 20 && full.JointWork(af, "guard_fit", CraftJointWork.Peen, 1, 4 * 1.0, 0, out _)) fb++;
            JointState jb = band.Piece(ab).assembly.Joint("guard_fit"), jf = full.Piece(af).assembly.Joint("guard_fit");
            Assert.Greater(bb, 0);
            Assert.Greater(fb, 0);
            Assert.AreEqual(0, jb.damage, 1e-9, "blows in the band do no harm");
            Assert.Greater(jf.damage, 0, "full blows past the band dent the guard");
            StringAssert.Contains("dented the guard", LastJoined(full));
            Assert.Less(Q(full, af, "guard_fit"), Q(band, ab, "guard_fit") - 0.1, $"closed in {fb} full blows against {bb} in the band");
        }

        [Test]
        public void APommel_IsBestPeenedWithBlowsInTheBand()
        {
            CraftWorkshop band = Workshop(), full = Workshop();
            string ab = Dagger(band), af = Dagger(full);
            CraftJointSpec spec = band.Rules.Design("dagger").Joint("pommel_peen");
            int bb = 0, fb = 0;
            while (!CraftAssembly.JointComplete(spec, band.Piece(ab).assembly.Joint("pommel_peen")) && bb < 30
                   && band.JointWork(ab, "pommel_peen", CraftJointWork.Peen, 1, Blow(band, ab, "pommel_peen", CraftJointWork.Peen, 1), 0, out _)) bb++;
            while (!CraftAssembly.JointComplete(spec, full.Piece(af).assembly.Joint("pommel_peen")) && fb < 30
                   && full.JointWork(af, "pommel_peen", CraftJointWork.Peen, 1, 4 * 1.0, 0, out _)) fb++;
            Assert.Greater(Q(band, ab, "pommel_peen"), 0.97, $"peened in {bb} blows in the band");
            Assert.Less(Q(full, af, "pommel_peen"), Q(band, ab, "pommel_peen") - 0.05, $"peened in {fb} full-charge blows: ragged");
            Assert.AreEqual(CraftAssembly.PeenEffect(CraftAssembly.GoodHigh), CraftAssembly.PeenEffect(1.25), 1e-12, "a harder blow spreads no more than the top of the band");
        }

        [Test]
        public void AJoint_IsAsTrueAsItsBlows_AndAPartTakenOffForgetsThem()
        {
            Assert.AreEqual(1, CraftAssembly.StruckWell(new JointState()), 1e-12, "no blow struck (an older save): nothing against it");
            CraftWorkshop w = Workshop();
            string asm = Hatchet(w);
            for (int k = 0; k < 2; k++)
                Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, Blow(w, asm, "head_haft", CraftJointWork.Wedge, 0.5), 0, out string why), why);
            JointState j = w.Piece(asm).assembly.Joint("head_haft");
            Assert.AreEqual(2, j.blows);
            Assert.AreEqual(CraftAssembly.BlowQuality(0.5), j.blowQuality, 1e-6, "the mean of its blows' quality");
            Assert.That(CraftAssembly.StruckWell(j), Is.InRange(0.75, 0.99));
            Assert.IsTrue(w.Detach(asm, "haft", 0, out string off), off);
            Assert.AreEqual(0, j.blows, "the joint starts over");
            Assert.AreEqual(0, j.blowQuality, 1e-12);
        }

        // ------------------------------------------------------------------ the peened hilt
        [Test]
        public void APeenedHilt_HoldsItsBlade_TillThePommelComesOff()
        {
            CraftWorkshop w = Workshop();
            string asm = Dagger(w);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(w.JointWork(asm, "pommel_peen", CraftJointWork.Peen, 1, Blow(w, asm, "pommel_peen", CraftJointWork.Peen, 1), 0, out string why), why);
            Assert.Greater(w.Piece(asm).assembly.Joint("pommel_peen").peen, 0.9);
            CraftDesign d = w.Rules.Design("dagger");
            Assert.IsFalse(w.Detach(asm, "blade", 0, out string held), "the blade is held in the peened hilt");
            StringAssert.Contains("take the pommel off first", held);
            Assert.AreEqual(held, CraftAssembly.HeldOn(w, w.Piece(asm), d, d.Component("blade")), "the bench's own words");
            Assert.IsFalse(w.Detach(asm, "guard", 0, out _), "as the guard is");
            Assert.IsNotNull(w.Piece(asm).assembly.PieceIn("blade"));
            // another blade will not go in under a closed hilt either
            CraftShapeTarget t = d.Component("blade").shape;
            string spare = w.AddPiece(ToTarget(w.Rules, t, t.lengthMin));
            Assert.IsNotNull(CraftAssembly.WhyNotOn(w, w.Piece(asm), d, d.Component("blade"), 0, w.Piece(spare)), "the hilt is closed");
            // the pommel comes off (the peen undone), and then the blade does
            Assert.IsTrue(w.Detach(asm, "pommel", 0, out string why2), why2);
            Assert.IsTrue(w.Detach(asm, "blade", 0, out why2), why2);
        }

        // ------------------------------------------------------------------ rivets set stay in the item
        /// <summary>A belt with its buckle riveted on from a set of <paramref name="rivets"/>: (workshop, item, the set).</summary>
        private static (CraftWorkshop w, string asm, string set) Belt(int rivets)
        {
            CraftWorkshop w = Workshop();
            CraftPiece strap = CraftLeather.NewHide(w.Rules, "cow", 5);
            strap.hide.pattern = "long_strap";
            string st = w.AddPiece(strap);
            string buckle = w.AddPiece(Rivets(w.Rules, CraftFastenerKind.Buckle, 1));
            string set = w.AddPiece(Rivets(w.Rules, CraftFastenerKind.Rivet, rivets));
            string asm = w.StartAssembly("belt", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "strap", 0, st, out why), why);
            Assert.IsTrue(w.Attach(asm, "buckle", 0, buckle, out why), why);
            Assert.IsTrue(w.Attach(asm, "rivets", 0, set, out why), why);
            Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Punch, 1, 0, 0, out why), why);
            Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Rivet, 1, Blow(w, asm, "buckle", CraftJointWork.Rivet, 1), 0, out why), why);
            return (w, asm, set);
        }

        [Test]
        public void RivetsSetIntoAnItem_StayPartOfIt_ItsWeightAndWorth_TheRestComeOffAtTheFinish()
        {
            var (w, asm, set) = Belt(2);
            FastenerState f = w.Piece(set).fasteners;
            Assert.AreEqual(2, f.count, "the set still counts the rivet set into the belt");
            Assert.AreEqual(1, f.used);
            Assert.AreEqual(1, f.Left, "one left to set");
            Assert.AreEqual(2 * f.massEach, w.Piece(set).Mass(w.Rules), 1e-9, "set or not, the set weighs all its rivets");
            double grams = w.Piece(set).composition.Sum(x => x.grams);
            int before = w.Pieces.Count;

            // finished: the rivet left over comes off it, a set of its own on the bench; the belt keeps the one set into it
            Assert.IsTrue(w.Finish(asm, out string why), why);
            Assert.AreEqual(1, f.count, "the belt's set holds the rivet set into it");
            Assert.AreEqual(1, f.used);
            Assert.AreEqual(0, f.Left);
            Assert.AreEqual(before + 1, w.Pieces.Count, "one set more on the bench");
            CraftPiece spare = w.Pieces.First(p => p.kind == CraftPieceKind.Fasteners && p.location == CraftLocation.Air);
            Assert.AreEqual(1, spare.fasteners.count);
            Assert.AreEqual(0, spare.fasteners.used);
            Assert.AreEqual("", spare.parent);
            Assert.AreEqual(grams, spare.composition.Sum(x => x.grams) + w.Piece(set).composition.Sum(x => x.grams), 1e-6, "the metal shared by count");
            Assert.AreEqual(grams / 2, spare.composition.Sum(x => x.grams), 1e-6);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            CraftItemPart rp = r.PartPieces.First(p => p.role == "rivets");
            Assert.AreEqual(f.massEach, rp.mass, 1e-9, "the item weighs the rivet set into it");
            List<string> lines = CraftEvaluator.KnownParts(w.Rules, r, new CraftJournalData());
            Assert.IsTrue(lines.Any(l => l.StartsWith("1 rivet ")), string.Join("; ", lines));
            Assert.Greater(CraftValue.Of(w.Rules, w.Piece(set)), 0, "and is worth it");
            // the history makes the same (the finish carries the new set's id)
            CraftWorkshop replayed = CraftWorkshop.Replay(Rules(), w.Tools, w.Ambient, w.Seed, w.Id, w.History, w.Step);
            Assert.IsNotNull(replayed.Piece(spare.id));
            Assert.AreEqual(1, replayed.Piece(spare.id).fasteners.count);
            Assert.AreEqual(1, replayed.Piece(set).fasteners.count);

            // the last of the set: nothing left over, nothing comes off
            var (w1, a1, s1) = Belt(1);
            Assert.AreEqual(1, w1.Piece(s1).fasteners.count);
            Assert.AreEqual(0, w1.Piece(s1).fasteners.Left);
            int had = w1.Pieces.Count;
            Assert.IsTrue(w1.Finish(a1, out why), why);
            Assert.AreEqual(had, w1.Pieces.Count);
            Assert.AreEqual(1, CraftEvaluator.Item(w1.Rules, w1, w1.Piece(a1)).PartPieces.Count(p => p.role == "rivets" && p.mass > 0), "one rivet, set, still weighs");
        }

        [Test]
        public void RivetsDrilledOut_AreLost_ASetTakenOffKeepsThoseStillToSet()
        {
            // the set taken off: what was set from it is drilled out (lost), what was left comes off with it; the holes stay
            var (w, asm, set) = Belt(3);
            JointState j = w.Piece(asm).assembly.Joint("buckle");
            Assert.IsTrue(w.Detach(asm, "rivets", 0, out string why), why);
            FastenerState f = w.Piece(set).fasteners;
            Assert.AreEqual(2, f.count, "the two still to set come off with it; the one set is drilled out and lost");
            Assert.AreEqual(0, f.used);
            Assert.AreEqual(0, j.rivets, "the buckle's rivet is drilled out");
            Assert.AreEqual(1, j.holes, "its hole stays");

            // a part a joint joins taken off: its rivets are drilled out of the set on the item and lost
            var (w2, asm2, set2) = Belt(3);
            Assert.IsTrue(w2.Detach(asm2, "strap", 0, out why), why);
            FastenerState f2 = w2.Piece(set2).fasteners;
            Assert.AreEqual(2, f2.count);
            Assert.AreEqual(0, f2.used);
            Assert.AreEqual(2, f2.Left);
            Assert.AreEqual(0, w2.Piece(asm2).assembly.Joint("buckle").rivets);
        }

        // ------------------------------------------------------------------ a joint in progress
        [Test]
        public void AJointInProgress_IsRatedByTheWorkDoneSoFar()
        {
            CraftWorkshop w = Workshop();
            CraftPiece crown = CraftLeather.NewHide(w.Rules, "cow", 3);
            crown.hide.pattern = "cap";
            string c = w.AddPiece(crown);
            string asm = w.StartAssembly("leather_cap", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "crown", 0, c, out why), why);
            CraftDesign d = w.Rules.Design("leather_cap");
            double need = CraftTextiles.ThreadNeed(CraftAssembly.SeamThickness(w, w.Piece(asm), d.Joint("seam")));
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, need), why);
            // the holes alone, punched true: as true as they are
            Assert.IsTrue(w.JointWork(asm, "seam", CraftJointWork.Punch, 20, 0, 0, out why), why);
            Assert.Greater(Q(w, asm, "seam", true), 0.95, "holes punched true, nothing in them yet");
            Assert.AreEqual(0, Q(w, asm, "seam"), 1e-9, "as a finished seam, it holds nothing yet");
            // four stitches of twenty, as well as hands pull them
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(w.JointWork(asm, "seam", CraftJointWork.Stitch, 1, CraftAssembly.StitchTensionIdeal, 0, out why), why);
            Assert.GreaterOrEqual(Q(w, asm, "seam", true), 0.75, "good work so far reads good");
            Assert.Less(Q(w, asm, "seam"), 0.45, "the seam as it would be judged now: a fifth of it");

            // holes punched off true read as they are
            CraftWorkshop w2 = Workshop();
            CraftPiece crown2 = CraftLeather.NewHide(w2.Rules, "cow", 3);
            crown2.hide.pattern = "cap";
            string asm2 = w2.StartAssembly("leather_cap", out why);
            Assert.IsTrue(w2.Attach(asm2, "crown", 0, w2.AddPiece(crown2), out why), why);
            Assert.IsTrue(w2.JointWork(asm2, "seam", CraftJointWork.Punch, 20, 0.6, 0.6, out why), why);
            Assert.Less(Q(w2, asm2, "seam", true), 0.6, "holes that wander and do not line up");
        }

        // ------------------------------------------------------------------ misaligned holes, once
        [Test]
        public void MisalignedHoles_CostARivetedJointOnce_AsAStitchedSeam()
        {
            double Riveted(double align)
            {
                CraftWorkshop w = Workshop();
                string plate = w.AddPiece(CraftDefaults.Bar(w.Rules, 300, 200, 2, Metal("Mild steel")));
                CraftPiece s1 = CraftLeather.NewHide(w.Rules, "cow", 3);
                s1.hide.pattern = "strap";
                CraftPiece s2 = CraftLeather.NewHide(w.Rules, "cow", 4);
                s2.hide.pattern = "strap";
                string st1 = w.AddPiece(s1), st2 = w.AddPiece(s2);
                string buck = w.AddPiece(Rivets(w.Rules, CraftFastenerKind.Buckle, 2));
                string riv = w.AddPiece(Rivets(w.Rules, CraftFastenerKind.Rivet, 10));
                string asm = w.StartAssembly("breastplate", out string why);
                Assert.IsNotNull(asm, why);
                Assert.IsTrue(w.Attach(asm, "plate", 0, plate, out why), why);
                Assert.IsTrue(w.Attach(asm, "straps", 0, st1, out why), why);
                Assert.IsTrue(w.Attach(asm, "straps", 1, st2, out why), why);
                Assert.IsTrue(w.Attach(asm, "buckles", 0, buck, out why), why);
                Assert.IsTrue(w.Attach(asm, "rivets", 0, riv, out why), why);
                Assert.IsTrue(w.JointWork(asm, "straps_riveted", CraftJointWork.Punch, 8, 0, align, out why), why);
                for (int i = 0; i < 8; i++)
                    Assert.IsTrue(w.JointWork(asm, "straps_riveted", CraftJointWork.Rivet, 1, Blow(w, asm, "straps_riveted", CraftJointWork.Rivet, 1), 0, out why), why);
                return Q(w, asm, "straps_riveted");
            }
            double straight = Riveted(0), off = Riveted(0.4);
            Assert.AreEqual(1 - 0.5 * 0.4, off / straight, 0.02, $"riveted through true holes {straight:0.000}, through holes 0.4 out of line {off:0.000}");
        }

        // ------------------------------------------------------------------ cloth
        [Test]
        public void AClaspSewnOntoCloth_IsSewnAsCloth()
        {
            CraftWorkshop w = Workshop();
            CraftDesign d = w.Rules.Design("wool_cloak");
            string cloth = w.Rules.Hides.First(h => h.textile).id;
            CraftPiece h1 = CraftLeather.NewHide(w.Rules, cloth, 3);
            h1.hide.pattern = "cloak_half";
            CraftPiece h2 = CraftLeather.NewHide(w.Rules, cloth, 4);
            h2.hide.pattern = "cloak_half";
            string clasp = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 1, 10, (CraftDefaults.Iron, 1.0)));
            string asm = w.StartAssembly("wool_cloak", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "halves", 0, w.AddPiece(h1), out why), why);
            Assert.IsTrue(w.Attach(asm, "halves", 1, w.AddPiece(h2), out why), why);
            Assert.IsTrue(w.Attach(asm, "clasp", 0, clasp, out why), why);
            CraftPiece a = w.Piece(asm);
            Assert.IsTrue(CraftTextiles.ClothSeam(w, a, d.Joint("back_seam")), "precondition: the halves are cloth");
            Assert.IsTrue(CraftTextiles.ClothSeam(w, a, d.Joint("clasp")), "a clasp sewn onto cloth is sewn as its cloth is: with the needle");
            Assert.Greater(CraftAssembly.SeamThickness(w, a, d.Joint("clasp"), out bool isCloth), 0);
            Assert.IsTrue(isCloth, "the thread's advice speaks of the cloth");
        }

        // ------------------------------------------------------------------ a loose eye
        [Test]
        public void ALooseEye_IsTakenUpByTheWedge_ASocketsPlayIsNot()
        {
            CraftWorkshop w = Workshop();
            string asm = Hatchet(w, 28); // 3 mm of play on the 25 mm haft
            CraftDesign hatchet = w.Rules.Design("hatchet");
            JointState j = w.Piece(asm).assembly.Joint("head_haft");
            Assert.AreEqual(3, j.gap, 0.05);
            StringAssert.Contains("the wedge will take up the play", CraftAssembly.HaftFitWords(hatchet.Joint("head_haft"), j.gap));
            Assert.Greater(CraftAssembly.PlayLeft(hatchet.Joint("head_haft"), j), CraftAssembly.PlayFree, "loose before the wedge goes in");
            DriveHome(w, asm, 1);
            Assert.LessOrEqual(CraftAssembly.PlayLeft(hatchet.Joint("head_haft"), j), CraftAssembly.PlayFree, "the wedge driven home takes the play up");
            Assert.AreEqual(3, j.gap, 0.05, "the eye is as it was made");
            Assert.Greater(Q(w, asm, "head_haft"), 0.95);

            // far more play than a wedge takes up: said so, and it stays weak however well it is wedged
            CraftWorkshop w2 = Workshop();
            string loose = Hatchet(w2, 35);
            JointState j2 = w2.Piece(loose).assembly.Joint("head_haft");
            StringAssert.Contains("more play than a wedge takes up", CraftAssembly.HaftFitWords(hatchet.Joint("head_haft"), j2.gap));
            DriveHome(w2, loose, 1);
            Assert.Less(Q(w2, loose, "head_haft"), 0.1);

            // a socket has no wedge: its play stays, and it is said so
            CraftWorkshop w3 = Workshop();
            string spear = Spear(w3, false, 28);
            CraftJointSpec socket = w3.Rules.Design("spear").Joint("head_haft");
            JointState j3 = w3.Piece(spear).assembly.Joint("head_haft");
            StringAssert.Contains("nothing takes that play up", CraftAssembly.HaftFitWords(socket, j3.gap));
            DriveHome(w3, spear, 1);
            Assert.AreEqual(j3.gap, CraftAssembly.PlayLeft(socket, j3), 1e-9);
            Assert.Less(Q(w3, spear, "head_haft"), 0.5, "loose on its shaft");
            StringAssert.Contains("loose on the shaft", LastJoined(w3));
        }

        // ------------------------------------------------------------------ the maker
        [Test]
        public void TheFinisher_IsTheItemsMaker_AndAReplayKeepsIt()
        {
            CraftWorkshop w = Workshop();
            string asm = Hatchet(w);
            DriveHome(w, asm, 1);
            Assert.IsTrue(w.Finish(asm, out string why, 0, "Ana"), why);
            Assert.AreEqual("Ana", w.Piece(asm).assembly.maker);
            CraftWorkshop replayed = CraftWorkshop.Replay(Rules(), w.Tools, w.Ambient, w.Seed, w.Id, w.History, w.Step);
            Assert.AreEqual("Ana", replayed.Piece(asm).assembly.maker);
            // an item finished with no name kept (an older save) has none
            CraftWorkshop w2 = Workshop();
            string other = Hatchet(w2);
            DriveHome(w2, other, 1);
            Assert.IsTrue(w2.Finish(other, out why), why);
            Assert.AreEqual("", w2.Piece(other).assembly.maker);
        }

        // ------------------------------------------------------------------ the inspection
        [Test]
        public void ABluntWeaponsInspection_RatesItsStrikingFace_AnEdgedOnesItsEdge()
        {
            CraftWorkshop w = Workshop();
            Assert.AreEqual(CraftEdgeKind.None, w.Rules.Design("mace").MainComponent.shape.edge, "precondition: a mace head has no edge");
            CraftPiece head = Holed(CraftDefaults.Bar(w.Rules, 120, 60, 60, Metal("Mild steel")), 28);
            string asm = w.StartAssembly("mace", out string why);
            Assert.IsTrue(w.Attach(asm, "head", 0, w.AddPiece(head), out why), why);
            Assert.IsTrue(w.Attach(asm, "haft", 0, w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 600, 27, 27)), out why), why);
            DriveHome(w, asm, 1);
            Assert.IsTrue(w.Finish(asm, out why), why);
            List<string> lines = CraftEvaluator.InspectionLines(CraftEvaluator.Item(w.Rules, w, w.Piece(asm)));
            StringAssert.StartsWith("Striking face:", lines[1]);

            CraftWorkshop w2 = Workshop();
            string hatchet = Hatchet(w2);
            DriveHome(w2, hatchet, 1);
            Assert.IsTrue(w2.Finish(hatchet, out why), why);
            StringAssert.StartsWith("Edge:", CraftEvaluator.InspectionLines(CraftEvaluator.Item(w2.Rules, w2, w2.Piece(hatchet)))[1]);
        }
    }
}
#endif
