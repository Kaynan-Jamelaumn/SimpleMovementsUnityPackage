#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the assembly bench and the finished item: hilted blades whose tang - measured from the piece -
    /// takes their hilt, guards and pommels with eyes made for the tang, a guard's slot closed onto it only so far, the
    /// pommel peened into its good band, optional parts that hold nothing up, supplies the item takes no more of than it
    /// needs, fastener sets that add up, buckles riveted where a design has no thread, rivets set by the right blow (too
    /// hard crushes the leather), a wedge driven past home splitting its haft once, joints that start over when a part
    /// comes off, finishing once; the rhythm's one answer a beat; threads that suit the thickest leather; the item's name
    /// as the player knows it, its notes all told, an overweight head no harder a hitter, a wooden weapon rated by its wood;
    /// and the guard's hold on trying items one at a time.
    /// </summary>
    public class CraftAssemblyAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "assembly");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A piece forged to a design's target at <paramref name="length"/>: each segment as wide and thick as the target there.</summary>
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

        /// <summary>A leather part cut to a pattern (a cow hide standing in for the cut piece).</summary>
        private static CraftPiece Cut(CraftWorkshop w, string pattern, int seed = 3)
        {
            CraftPiece h = CraftLeather.NewHide(w.Rules, "cow", seed);
            h.hide.pattern = pattern;
            return h;
        }

        /// <summary>A piece with a hole (an eye, a slot, a bore) of <paramref name="diameter"/> in its middle.</summary>
        private static CraftPiece Holed(CraftPiece p, double diameter)
        {
            p.segments[p.segments.Count / 2].hole = diameter;
            return p;
        }

        /// <summary>A dagger put together on the bench: a blade forged to the shortest target, a guard, a grip and a pommel made for its tang.</summary>
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

        // ------------------------------------------------------------------ hilts
        [Test]
        public void HiltedDesigns_TheShortestBladeTakesTheLongestHilt_AndTheirEyesFitTheTang()
        {
            CraftRules rules = Rules();
            Assert.IsFalse(rules.CompileWarnings.Any(x => x.Contains("tang of its shortest blade")), string.Join("\n", rules.CompileWarnings));
            int hilted = 0;
            foreach (CraftDesign d in rules.Designs)
            {
                CraftJointSpec peen = CraftAssembly.PeenJoint(d);
                if (peen == null) continue;
                hilted++;
                CraftShapeTarget blade = d.Component(peen.roleA).shape;
                double hilt = CraftAssembly.PeenAllowance;
                foreach (CraftComponentSpec c in d.components)
                    if (!c.supply && (c.function == CraftComponentFunction.Grip || c.function == CraftComponentFunction.Counterweight))
                        hilt += c.shape.lengthMax;
                foreach (float length in new[] { blade.lengthMin, blade.lengthMax })
                {
                    CraftPiece made = ToTarget(rules, blade, length);
                    Assert.IsTrue(CraftAssembly.MeasureTang(blade, made, out _, out double tang, out bool shoulder), d.id);
                    Assert.IsTrue(shoulder, $"{d.id}: the blade widens past its tang, so a guard sits on it");
                    Assert.GreaterOrEqual(tang, hilt, $"{d.id} at {length} mm: its tang comes through its longest grip and pommel, and is peened");
                }
                // the guard's slot and the pommel's hole are made for the tang: about half a millimetre over it
                CraftPiece shortest = ToTarget(rules, blade, blade.lengthMin);
                foreach (CraftComponentSpec c in d.components)
                    if (c.function == CraftComponentFunction.Guard || c.function == CraftComponentFunction.Counterweight)
                    {
                        double size = CraftAssembly.TangSize(blade, shortest, c.function == CraftComponentFunction.Guard ? 1 : 0, out _);
                        Assert.That(c.shape.eyeDiameter - size, Is.InRange(0.3, 0.7), $"{d.id} {c.role}: a snug eye on the tang");
                    }
            }
            Assert.GreaterOrEqual(hilted, 10, "every hilted blade checked");
        }

        [Test]
        public void AHilt_GoesTogether_ItsSlotClosesOnlySoFar_ThePommelIsPeened_AndItFinishesOnce()
        {
            CraftWorkshop w = Workshop();
            CraftDesign dagger = w.Rules.Design("dagger");
            string asm = Dagger(w);
            AssemblyState s = w.Piece(asm).assembly;
            JointState guard = s.Joint("guard_fit");
            Assert.That(guard.gap, Is.InRange(0.3, 0.7), "the guard's slot made for the tang: a little play");

            // the wooden grip fits as it was bored; the steel guard's slot is closed onto the tang - never into it, and only so far
            Assert.IsFalse(w.JointWork(asm, "grip_fit", CraftJointWork.Peen, 1, 2.6, 0, out string why));
            StringAssert.Contains("not peened", why);
            int blows = 0;
            while (blows < 12 && w.JointWork(asm, "guard_fit", CraftJointWork.Peen, 1, 2.6, 0, out why)) blows++;
            Assert.Greater(blows, 0);
            Assert.GreaterOrEqual(guard.gap, 0, "closed onto the tang, not into it");
            Assert.Less(guard.gap, 0.3);
            Assert.LessOrEqual(guard.peenClosed, CraftAssembly.PeenCloseBudget + 1e-9);

            // the wrap is optional: left off it holds nothing up, and there is nothing to wind
            Assert.IsFalse(w.JointWork(asm, "wrap", CraftJointWork.Wrap, 1, 0.65, 0.1, out why));
            StringAssert.Contains("grip wrap", why);
            CollectionAssert.AreEqual(new[] { "Pommel peened on: not finished." }, CraftAssembly.Missing(w, w.Piece(asm)));

            // firm blows on the ideal spread the tang's end into its good band
            blows = 0;
            while (!CraftAssembly.JointComplete(dagger.Joint("pommel_peen"), s.Joint("pommel_peen")) && blows < 20)
            {
                Assert.IsTrue(w.JointWork(asm, "pommel_peen", CraftJointWork.Peen, 1, 4 * 0.8, 0, out why), why);
                blows++;
            }
            Assert.AreEqual(5, blows, "five blows on the ideal");
            Assert.IsEmpty(CraftAssembly.Missing(w, w.Piece(asm)));
            Assert.IsFalse(w.Detach(asm, "guard", 0, out why), "the peened pommel holds the hilt together");
            StringAssert.Contains("pommel", why);

            Assert.IsTrue(w.Finish(asm, out why), why);
            Assert.IsFalse(w.Finish(asm, out why), "finished once");
            StringAssert.Contains("already finished", why);
            Assert.IsFalse(w.JointWork(asm, "pommel_peen", CraftJointWork.Peen, 1, 3.2, 0, out why), "its joints are as they were judged");
            StringAssert.Contains("already finished", why);
        }

        [Test]
        public void TheTang_IsMeasuredFromTheBlade_NoTangTakesNoGuard_AndAShortOneSaysByHowMuch()
        {
            CraftWorkshop w = Workshop();
            CraftDesign dagger = w.Rules.Design("dagger");
            CraftShapeTarget t = dagger.Component("blade").shape;
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 430, 30, 5, Metal("Mild steel")));
            string guard = w.AddPiece(Holed(CraftDefaults.Bar(w.Rules, 90, 14, 8, Metal("Mild steel")), 11));
            string asm = w.StartAssembly("dagger", out string why);
            Assert.IsTrue(w.Attach(asm, "blade", 0, bar, out why), why);
            Assert.IsFalse(w.Attach(asm, "guard", 0, guard, out why), "a bar as wide all along has no tang");
            StringAssert.Contains("no tang", why);

            // the tang the smith forged is what is measured: a longer one is longer
            CraftPiece forged = ToTarget(w.Rules, t, 430), longer = ToTarget(w.Rules, t, 430);
            for (int i = 0; i < 14; i++) longer.segments[i].width = 12;
            CraftAssembly.MeasureTang(t, forged, out _, out double a, out _);
            CraftAssembly.MeasureTang(t, longer, out _, out double b, out _);
            Assert.Greater(b, a);

            // a stub of a tang: the grip will not go on it, and the refusal gives the numbers
            CraftPiece stub = CraftDefaults.Bar(w.Rules, 430, 30, 5, Metal("Mild steel"));
            for (int i = 0; i < 4; i++) stub.segments[i].width = 12;
            string stubbed = w.StartAssembly("dagger", out why);
            Assert.IsTrue(w.Attach(stubbed, "blade", 0, w.AddPiece(stub), out why), why);
            string grip = w.AddPiece(Holed(CraftDefaults.WoodBlank(w.Rules, "ash", 100, 26, 26), 11));
            Assert.IsFalse(w.Attach(stubbed, "grip", 0, grip, out why));
            StringAssert.Contains("too short", why);
            StringAssert.Contains("72 mm", why);
        }

        [Test]
        public void TheRulesCheck_WarnsOfATangTooShortForItsHilt()
        {
            CraftContent content = CraftDefaults.Content();
            content.designs.First(d => d.id == "dagger").components.First(c => c.role == "blade").shape.tangTo = 0.3f;
            CraftRules rules = CraftRules.Compile(new CraftSimulationSettings(), content);
            Assert.IsTrue(rules.CompileWarnings.Any(x => x.Contains("'dagger'") && x.Contains("tang")), string.Join("\n", rules.CompileWarnings));
        }

        // ------------------------------------------------------------------ supplies and fasteners
        [Test]
        public void Supplies_TheItemTakesNoMoreThanItNeeds_AndWhatIsAddedStaysIn()
        {
            CraftWorkshop w = Workshop();
            string asm = w.StartAssembly("leather_gloves", out string why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 6, 1.3), why);
            AssemblyState s = w.Piece(asm).assembly;
            Assert.AreEqual(1, s.Supply("thread"), 1e-9, "the gloves take one thread: no more is used");
            Assert.AreEqual(1.3, s.Supply("thread" + CraftWorkshop.StrengthSuffix), 1e-6, "the thread's strength is kept");
            Assert.IsFalse(w.Attach(asm, "thread", 0, null, out why, 1, 1.0));
            StringAssert.Contains("all the thread it needs", why);
            Assert.IsFalse(w.Detach(asm, "thread", 0, out why));
            StringAssert.Contains("cannot be taken back out", why);
        }

        [Test]
        public void FastenerSets_AddUp_ButStudsAreNotRivets()
        {
            CraftWorkshop w = Workshop();
            string asm = w.StartAssembly("studded_armor", out string why);
            Assert.IsNotNull(asm, why);
            string ten = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Stud, 10, 4, Metal("Mild steel")));
            string fourteen = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Stud, 14, 4, Metal("Mild steel")));
            string rivets = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Rivet, 5, 5, Metal("Mild steel")));
            Assert.IsTrue(w.Attach(asm, "studs", 0, ten, out why), "a set short of the count goes on: " + why);
            Assert.IsTrue(w.Attach(asm, "studs", 0, fourteen, out why), "another set goes in with it: " + why);
            Assert.AreEqual(24, w.Piece(w.Piece(asm).assembly.PieceIn("studs")).fasteners.count);
            Assert.IsNull(w.Piece(fourteen), "the second set went into the first");
            Assert.IsFalse(w.Attach(asm, "studs", 0, rivets, out why));
            StringAssert.Contains("studs", why);
        }

        [Test]
        public void ABuckleWithNoThread_IsRivetedOn_AndItsRivetsAreCounted()
        {
            foreach (CraftDesign d in Rules().Designs)
            {
                if (CraftAssembly.ThreadPart(d) != null) continue;
                foreach (CraftJointSpec js in d.joints)
                {
                    if (js.kind != CraftJointKind.Buckle) continue;
                    CraftComponentSpec rivets = CraftAssembly.RivetPart(d, js);
                    Assert.IsNotNull(rivets, $"{d.id}: a buckle with no thread to sew it is riveted on");
                    int want = 0;
                    foreach (CraftJointSpec o in d.joints)
                        if ((o.kind == CraftJointKind.Rivet || o.kind == CraftJointKind.Buckle) && CraftAssembly.RivetPart(d, o) == rivets)
                            want += CraftAssembly.RivetsWanted(o);
                    Assert.GreaterOrEqual(rivets.fastenerCount, want, $"{d.id}: a rivet for every strap's hole and for each buckle");
                }
            }
        }

        /// <summary>A belt's buckle riveted on with one blow of <paramref name="blow"/> (a share of a full one).</summary>
        private static (double quality, double damage, string said) Riveted(double blow)
        {
            CraftWorkshop w = Workshop();
            string strap = w.AddPiece(Cut(w, "long_strap"));
            string buckle = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 1, 30, Metal("Brass")));
            string rivets = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Rivet, 1, 5, Metal("Mild steel")));
            string asm = w.StartAssembly("belt", out string why);
            Assert.IsTrue(w.Attach(asm, "strap", 0, strap, out why), why);
            Assert.IsTrue(w.Attach(asm, "buckle", 0, buckle, out why), why);
            Assert.IsTrue(w.Attach(asm, "rivets", 0, rivets, out why), why);
            Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Punch, 1, 0, 0, out why), why);
            string said = "";
            w.EventRaised += e => { if (e.Kind == CraftEventKind.Joined && e.Text.Length > 0) said = e.Text; };
            Assert.IsTrue(w.JointWork(asm, "buckle", CraftJointWork.Rivet, 1, 4 * blow, 0, out why), why);
            JointState j = w.Piece(asm).assembly.Joint("buckle");
            return (j.rivetQuality, j.damage, said);
        }

        [Test]
        public void ARivet_IsSetTightByABlowInItsBand_TooHardCrushesTheLeather_TooLightLeavesItLoose()
        {
            var good = Riveted(0.6);
            Assert.Greater(good.quality, 0.9, good.said);
            Assert.AreEqual(0, good.damage, 1e-9);
            StringAssert.Contains("set tight", good.said);
            var hard = Riveted(1.0);
            Assert.Greater(hard.damage, 0, "a full blow through leather crushes it");
            StringAssert.Contains("crushed the leather", hard.said);
            var light = Riveted(0.3);
            Assert.Less(light.quality, 0.5, light.said);
            StringAssert.Contains("light", light.said.ToLowerInvariant());
        }

        // ------------------------------------------------------------------ joints
        [Test]
        public void Stitches_NeverPassTheirHoles_AndAJointTakenApartStartsOver()
        {
            CraftWorkshop w = Workshop();
            string asm = w.StartAssembly("leather_gloves", out string why);
            Assert.IsTrue(w.Attach(asm, "gloves", 0, w.AddPiece(Cut(w, "glove", 4)), out why), why);
            Assert.IsTrue(w.Attach(asm, "gloves", 1, w.AddPiece(Cut(w, "glove", 5)), out why), why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1.0), why);
            JointState j = w.Piece(asm).assembly.Joint("seams");
            Assert.IsFalse(w.JointWork(asm, "seams", CraftJointWork.Stitch, 2, 0.6, 0.1, out why), "no hole to stitch yet");
            StringAssert.Contains("Punch the holes first", why);
            Assert.IsTrue(w.JointWork(asm, "seams", CraftJointWork.Punch, 4, 0.1, 0.1, out why), why);
            Assert.IsTrue(w.JointWork(asm, "seams", CraftJointWork.Stitch, 6, 0.6, 0.1, out why), why);
            Assert.AreEqual(4, j.stitches, "as many stitches as there are holes");
            Assert.IsFalse(w.JointWork(asm, "seams", CraftJointWork.Stitch, 1, 0.6, 0.1, out why));
            StringAssert.Contains("punch more", why);

            Assert.IsTrue(w.Detach(asm, "gloves", 1, out why), why);
            Assert.AreEqual(0, j.holes);
            Assert.AreEqual(0, j.stitches);
            Assert.AreEqual(0, j.tension, 1e-9, "nothing of the old seam is counted for the next glove");
        }

        [Test]
        public void AWedgeDrivenPastHome_SplitsTheHaft_AndIsCountedOnce()
        {
            CraftWorkshop w = Workshop();
            CraftPiece h = CraftDefaults.Bar(w.Rules, 150, 40, 20, Metal("Mild steel"));
            h.segments[h.SegmentAt(0.15f)].hole = 26;
            string head = w.AddPiece(h);
            string haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 430, 29, 29));
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsTrue(w.Attach(asm, "head", 0, head, out why), why);
            Assert.IsTrue(w.Attach(asm, "haft", 0, haft, out why), why);
            for (int k = 0; k < 8; k++)
                Assert.IsTrue(w.JointWork(asm, "head_haft", CraftJointWork.Wedge, 1, 4 * 0.7, 0, out why), why);
            Assert.Greater(w.Piece(asm).assembly.Joint("head_haft").wedge, CraftAssembly.WedgeGoodHigh);
            Assert.Greater(w.Piece(haft).wood.split, 0, "driven past home, the wedge splits the haft");
            Assert.IsFalse(w.Piece(asm).defects.Any(df => df.kind == CraftDefectKind.Crack), "the split is the haft's: not a crack of the item as well");
        }

        [Test]
        public void TheRhythm_TakesOneAnswerABeat_AndHalfABeatOffIsAMiss()
        {
            var r = new CraftRhythm();
            r.Tick(0.7);
            Assert.AreEqual(0, r.Answer(0.6, null), 1e-9, "on the beat");
            Assert.AreEqual(-1, r.Answer(0.6, null), 1e-9, "a second answer to the same beat does not count");
            Assert.AreEqual(1, r.Count);
            r.Tick(0.36);
            Assert.AreEqual(1, r.Answer(0.6, null), 1e-9, "half a beat off: a miss");
            Assert.AreEqual(1, r.Misses);
            r.Tick(0.28);
            Assert.AreEqual(-1, r.Answer(0.6, null), 1e-9, "still that beat's");
        }

        [Test]
        public void Threads_TheStrongestSuitsTheThickestSole_ClothAsBefore()
        {
            Assert.AreEqual(1.0, CraftTextiles.ThreadSuits(9, 1.5), 1e-9, "silk suits a 9 mm sole");
            Assert.AreEqual(1.0, CraftTextiles.ThreadSuits(2, 1.0), 1e-9, "linen suits 2 mm leather");
            Assert.Less(CraftTextiles.ThreadSuits(5, 1.0), 0.75, "linen is weak for a 5 mm sole");
            Assert.AreEqual(1.3, CraftTextiles.ThreadNeed(5), 1e-9, "waxed thread to 5 mm");
            Assert.AreEqual(0.6 + 0.2 * 1.5, CraftTextiles.ThreadNeed(1.5), 1e-9, "cloth wants what it always did");
        }

        // ------------------------------------------------------------------ the finished item
        [Test]
        public void ItemNames_SayTheLeatherOnce_AndTheClothItIsSewnOf()
        {
            CraftRules rules = Rules();
            var cow = new CraftPiece { kind = CraftPieceKind.Hide };
            cow.hide.hide = "cow";
            Assert.AreEqual("Cow leather cap", CraftEvaluator.ItemName(rules, rules.Design("leather_cap"), "Cow leather", cow));
            Assert.AreEqual("Hardened cow leather cap", CraftEvaluator.ItemName(rules, rules.Design("leather_cap"), "Hardened cow leather", cow));
            var wool = new CraftPiece { kind = CraftPieceKind.Hide };
            wool.hide.hide = "wool_cloth";
            Assert.AreEqual("Wool shirt", CraftEvaluator.ItemName(rules, rules.Design("linen_shirt"), "Wool", wool));
            Assert.AreEqual("Steel dagger", CraftEvaluator.ItemName(rules, rules.Design("dagger"), "Steel", null));
        }

        [Test]
        public void TheItem_IsNamedAsThePlayerKnowsIt_AndTryingItTellsEveryNote()
        {
            CraftWorkshop w = Workshop();
            string asm = Dagger(w);
            for (int k = 0; k < 5; k++) Assert.IsTrue(w.JointWork(asm, "pommel_peen", CraftJointWork.Peen, 1, 3.2, 0, out string why), why);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            StringAssert.EndsWith("dagger", r.Name.ToLowerInvariant());
            Assert.AreEqual("Dagger", CraftEvaluator.KnownName(w.Rules, r, new CraftJournalData()), "a metal the player does not know is not named");
            Assert.AreEqual(4, CraftEvaluator.KnownParts(w.Rules, r, new CraftJournalData()).Count, "blade, guard, grip and pommel");

            var report = new CraftItemReport();
            report.Notes.Add("Pommel peened on: weak (21%)");
            report.Notes.Add("a soft edge: it will dull and roll");
            report.Notes.Add("a soft edge: it will dull and roll");
            List<string> lines = CraftEvaluator.InspectionLines(report);
            CollectionAssert.Contains(lines, "Pommel peened on: weak (21%).", "a note with a colon in it is told too");
            Assert.AreEqual(1, lines.Count(l => l == "A soft edge: it will dull and roll."), "each note once");
        }

        [Test]
        public void AnOverweightHead_SwingsSlower_AndHitsNoHarder()
        {
            CraftRules rules = Rules();
            CraftDesign d = rules.Design("hatchet");
            CraftItemReport Rate(double width, double thickness)
            {
                var parts = new Dictionary<string, List<CraftPiece>>
                {
                    ["head"] = new List<CraftPiece> { CraftDefaults.Bar(rules, 150, width, thickness, Metal("Mild steel")) },
                    ["haft"] = new List<CraftPiece> { CraftDefaults.WoodBlank(rules, "ash", 430, 30, 30) },
                };
                return CraftEvaluator.Item(rules, d, parts, new AssemblyState { design = d.id });
            }
            CraftItemReport light = Rate(40, 20), heavy = Rate(80, 50);
            Assert.Less(heavy.AttackSpeed, light.AttackSpeed, "a head five times as heavy swings slower");
            Assert.LessOrEqual(heavy.DamageMax, light.DamageMax * 1.15f, "and hits no harder than the heaviest the design allows");
        }

        [Test]
        public void AWoodenWeapon_IsRatedByItsWood_NotAnEdgeOrATemper()
        {
            CraftRules rules = Rules();
            CraftDesign club = rules.Design("club");
            var parts = new Dictionary<string, List<CraftPiece>> { ["club"] = new List<CraftPiece> { CraftDefaults.WoodBlank(rules, "oak", 700, 50, 50) } };
            CraftItemReport r = CraftEvaluator.Item(rules, club, parts, new AssemblyState { design = club.id });
            Assert.IsTrue(r.Wooden);
            Assert.Greater(r.Wood, 0);
            Assert.AreEqual(0, r.Edge, 1e-9, "a club has no edge");
            List<string> lines = CraftEvaluator.InspectionLines(r);
            Assert.IsTrue(lines.Any(l => l.StartsWith("Wood:")), string.Join(" | ", lines));
            Assert.IsFalse(lines.Any(l => l.Contains("heat treatment")), string.Join(" | ", lines));
        }

        [Test]
        public void TheGuard_TriesItemsFromTheBagOneAtATime()
        {
            var guard = new CraftRequestGuard(5, 0);
            int granted = 0;
            string last = "";
            for (int k = 0; k < 6; k++)
            {
                var r = new CraftRequest(CraftRequestKind.InspectItem) { itemId = "crafted:x" };
                if (guard.Check(ref r, 1, out string why)) granted++;
                else last = why;
            }
            Assert.Less(granted, 6);
            Assert.AreEqual("One thing at a time.", last);
        }
    }
}
#endif
