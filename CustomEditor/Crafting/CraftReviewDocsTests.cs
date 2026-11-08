#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of what the documentation's review found the code doing otherwise than the guide says: a full pour
    /// into any mould - the walkthrough's own hatchet head among them - is a full casting, and its words say so (a mould's
    /// stretches add up to its length; cut in float they came up a hair short, and a full pour read "it came out short").
    /// </summary>
    public class CraftReviewDocsTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A crucible holding <paramref name="grams"/> of <paramref name="metal"/>, <paramref name="superheat"/> over its liquidus.</summary>
        private static CraftWorkshop Melt(double grams, string metal = "Bronze", double superheat = 60)
        {
            CraftWorkshop w = new CraftWorkshop(Rules(), CraftTools.Forge | CraftTools.Crucible, 18, 1, 0, "water", "docsrev");
            foreach ((string element, double fraction) in Metal(metal))
                w.Crucible.melt.Add(new CraftAmount(element, grams * fraction));
            w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + superheat;
            return w;
        }

        /// <summary>The grams of <paramref name="metal"/> a full <paramref name="mold"/> takes, as the pour window works it out.</summary>
        private static double Need(CraftMold mold, string metal = "Bronze")
        {
            CraftWorkshop probe = Melt(100, metal);
            return CraftSmelting.MoldMass(probe.Rules, mold, CraftSmelting.MeltProfile(probe.Rules, probe.Crucible).Density);
        }

        /// <summary>
        /// The pour as the window and the 3D crucible run it: the crucible's metal is the stream's supply (in mould-fulls),
        /// a steady stream in the FLOW bar's green band until the mould is full, then let go - it tips back with its lip
        /// over the cup and the stream dies away; what the hands did goes to the workshop as the pour request.
        /// </summary>
        private static CraftPourControl PourFull(CraftWorkshop w, CraftMold mold, double need, out CraftPiece cast, out string why)
        {
            var pour = new CraftPourControl { Supply = w.Crucible.Mass / need };
            double fill = CraftPourControl.FillSeconds(need);
            const double dt = 1 / 60.0;
            for (int k = 0; k < 6000 && !pour.Full && !pour.Empty; k++) pour.Tick(dt, 0.6, null, fill);
            Assert.IsTrue(pour.Full, $"{mold.Name}: the mould filled");
            for (int k = 0; k < 600 && !pour.Settled(true); k++) pour.Tick(dt, 0, null, fill);
            Assert.IsTrue(pour.Settled(true), $"{mold.Name}: the stream died away");
            Assert.IsTrue(pour.Request(mold.id, false, out CraftRequest r));
            Assert.AreEqual(1000, r.i1, $"{mold.Name}: the pour brought a whole mould");
            string id = w.Pour(r.id, r.a, r.b, r.i0, out why, r.c, r.d, r.i1 / 1000.0, r.i2 / 1000.0);
            cast = id != null ? w.Piece(id) : null;
            return pour;
        }

        private static string Causes(CraftPiece p) => string.Join("; ", p.defects.Select(d => $"{d.kind} {d.severity:0.######}: {d.cause}"));

        // ------------------------------------------------------------------ r01: a full pour is a full casting
        [Test]
        public void MoldSegments_AddUpToTheMouldsLength()
        {
            foreach (CraftMold m in Rules().AllMolds())
            {
                if (m.fasteners)
                    continue;
                List<MetalSegment> segs = CraftSmelting.MoldSegments(Rules(), m);
                Assert.AreEqual(m.length, segs.Sum(s => s.length), 1e-9, $"{m.id}: the stretches are the whole mould");
            }
        }

        [Test]
        public void FullPour_IntoEveryMould_IsAFullCasting_AndItsWordsSaySo()
        {
            var shortOnes = new List<string>();
            foreach (CraftMold m in Rules().AllMolds())
            {
                double need = Need(m);
                CraftWorkshop w = Melt(need * 1.2 + 50);
                CraftPourControl pour = PourFull(w, m, need, out CraftPiece cast, out string why);
                Assert.IsNotNull(cast, $"{m.id}: {why}");
                string verdict = CraftSmelting.PourVerdict(w.Rules, cast, pour, need, m, false);
                if (!m.fasteners && (cast.defects.Any(d => d.kind == CraftDefectKind.Misrun) || cast.Length < m.length - 1e-6))
                    shortOnes.Add($"{m.id} \"{m.Name}\": {cast.Length:0.######} of {m.length} mm ({Causes(cast)})");
                StringAssert.StartsWith("A clean pour:", verdict, $"{m.id} \"{m.Name}\": {Causes(cast)}");
                StringAssert.EndsWith("full and sound.", verdict, m.id);
            }
            Assert.IsEmpty(shortOnes, "a full pour is never short:\n" + string.Join("\n", shortOnes));
        }

        /// <summary>
        /// Doc 01's walkthrough, step 2: two stock Bronze bars melted, covered with borax and stirred, poured in a steady
        /// stream into the hatchet's own pattern mould (616 g: two 315 g bars fill it) and tipped back as soon as it is
        /// full: "a steady, unbroken stream in the FLOW bar's green band fills the mould soundly" - and the words say so.
        /// </summary>
        [Test]
        public void Walkthrough_TwoBronzeBars_FillTheHatchetHead_AndThePourSaysSo()
        {
            CraftRules rules = Rules();
            CraftMold head = rules.Mold(CraftRules.PatternMoldId("hatchet", "head"));
            Assert.IsNotNull(head, "the hatchet's pattern mould");
            double bar = CraftDefaults.Bar(rules, 200, 30, 6, Metal("Bronze")).Mass(rules);
            double need = Need(head);
            Assert.AreEqual(315, bar, 3, "a stock Bronze bar");
            Assert.AreEqual(616, need, 3, "the hatchet head takes 616 g of bronze");
            Assert.Greater(2 * bar, need, "two bars fill it");

            CraftWorkshop w = Melt(2 * bar);
            Assert.IsTrue(w.AddFlux("borax", 1, 1, out string why), "cover: " + why);
            Assert.IsTrue(w.StirMelt(0.8, out why), "stir: " + why);
            CraftPourControl pour = PourFull(w, head, need, out CraftPiece cast, out why);
            Assert.IsNotNull(cast, why);
            Assert.IsFalse(cast.defects.Any(d => d.kind == CraftDefectKind.Misrun), "full, not short: " + Causes(cast));
            Assert.AreEqual(head.length, cast.Length, 1e-6, "the whole head");
            string verdict = CraftSmelting.PourVerdict(rules, cast, pour, need, head, false);
            Console.WriteLine("verdict: " + verdict);
            StringAssert.StartsWith("A clean pour: a casting of ", verdict);
            StringAssert.DoesNotContain("short", verdict);
        }

        /// <summary>A melt too small for the mould is still short, and says why: the fix makes a full pour full, no more.</summary>
        [Test]
        public void APourFromAMeltTooSmall_IsStillShort_AndSaysWhy()
        {
            CraftMold head = Rules().Mold(CraftRules.PatternMoldId("hatchet", "head"));
            double need = Need(head);
            CraftWorkshop w = Melt(need * 0.7);
            var pour = new CraftPourControl { Supply = w.Crucible.Mass / need };
            double fill = CraftPourControl.FillSeconds(need);
            const double dt = 1 / 60.0;
            for (int k = 0; k < 6000 && !pour.Settled(false); k++) pour.Tick(dt, 0.6, null, fill);
            Assert.IsTrue(pour.Empty, "the crucible gave all it had");
            Assert.IsTrue(pour.Request(head.id, false, out CraftRequest r));
            CraftPiece cast = w.Piece(w.Pour(r.id, r.a, r.b, r.i0, out string why, r.c, r.d, r.i1 / 1000.0, r.i2 / 1000.0));
            Assert.IsNotNull(cast, why);
            Assert.Less(cast.Length, head.length * 0.8, "a short casting");
            StringAssert.Contains("it came out short - ", CraftSmelting.PourVerdict(Rules(), cast, pour, need, head, false));
        }
    }
}
#endif
